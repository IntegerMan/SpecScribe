using System.Collections.Concurrent;

namespace SpecScribe;

/// <summary>Watches _bmad-output (and the hand-authored docs/adrs) for *.md changes — plus the non-markdown DATA
/// SOURCES the site reads (<c>sprint-status.yaml</c> under the source root and <c>_bmad/config.toml</c> at the repo
/// root) — and drives the SiteGenerator, debouncing the burst of events a single save typically produces. Reads are
/// always shared (see MarkdownConverter / ReadAllTextShared), so this never takes a write lock on anything under the
/// watched tree, including the newly-watched yaml/toml (NFR5). [Story 6.11 widened the watched set]</summary>
public sealed class FileWatcherService : IDisposable
{
    /// <summary>The file extensions a watch event is admitted for. <c>.md</c> is the markdown source; the yaml/toml
    /// set carries the non-markdown data sources (<c>sprint-status.yaml</c>, <c>_bmad/config.toml</c>) whose changes
    /// must refresh the live view too (the shipped R6.1 gap). [Story 6.11]</summary>
    private static readonly string[] WatchedExtensions = { ".md", ".yaml", ".yml", ".toml" };

    /// <summary>The <see cref="_pending"/> key a DIRECTORY-level change debounces under (Story 5.3). Deliberately not
    /// a path: a folder create/rename/delete has no single file whose fate could be classified, and the escalation is
    /// whole-tree anyway, so every directory event in a burst must coalesce onto ONE key. <c>&lt;</c>/<c>&gt;</c> are
    /// illegal in Windows paths and never produced by <see cref="Path.GetFullPath"/> elsewhere, so this can never
    /// collide with a real watched file's key.</summary>
    private const string TopologySentinelKey = "<topology>";

    /// <summary>The relative-path label the escalated full rebuild is reported under, so the watch log names a
    /// directory change as such instead of attributing it to some arbitrary contained file. Shared with
    /// <see cref="SiteGenerator.RegenerateTopology"/> so the two can never drift. [Story 5.3]</summary>
    internal const string TopologyEventLabel = "<directory change>";

    /// <summary>Per-watcher labels for events that belong to a WATCHER rather than to any one artifact — used for
    /// both the <see cref="FileSystemWatcher.Error"/> channel and the <see cref="SafeHandle"/> crash guard, so a
    /// failure is attributable to the specific watcher that produced it rather than to a generic "<c>&lt;watcher&gt;</c>".
    /// [Story 5.3 follow-up: promoted from scattered literals]</summary>
    private const string FileWatcherLabel = "<watcher>";
    private const string DirectoryWatcherLabel = "<directory-watcher>";
    private const string BmadDirWatcherLabel = "<bmad-dir-watcher>";

    private readonly ForgeOptions _options;
    private readonly SiteGenerator _generator;
    private readonly Action<GenerationEvent> _onEvent;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, Timer> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _watchersLock = new();
    private bool _started;
    private bool _disposed;
    private bool _configWatcherRegistered;
    private FileSystemWatcher? _configDirDetector;

    /// <summary>Number of live <see cref="FileSystemWatcher"/> instances — test seam only, so the dynamic
    /// <c>_bmad</c>-dir registration (<see cref="OnConfigDirCreated"/>) can be asserted deterministically without
    /// waiting on real FS-event timing.</summary>
    internal int WatcherCount { get { lock (_watchersLock) { return _watchers.Count; } } }

    public FileWatcherService(ForgeOptions options, SiteGenerator generator, Action<GenerationEvent> onEvent)
    {
        _options = options;
        _generator = generator;
        _onEvent = onEvent;

        // The source root also holds sprint-status.yaml, so admit the data-source extensions here (not just *.md).
        Directory.CreateDirectory(options.SourceRoot);
        _watchers.Add(CreateWatcher(options.SourceRoot, "*.md", "*.yaml", "*.yml", "*.toml"));

        // The hand-authored ADRs are a second, read-only source; watch them too so edits live-reload. Markdown only —
        // no data source lives here.
        Directory.CreateDirectory(options.AdrSourceRoot);
        _watchers.Add(CreateWatcher(options.AdrSourceRoot, "*.md"));

        // DIRECTORY topology, per root (Story 5.3). The file watchers above cannot see this: their Filters match file
        // NAMES, and a bare folder name matches none of them — so renaming/creating/deleting a whole directory of
        // artifacts produces no watch event at all today, and on Windows a folder rename does not enumerate its
        // children as separate file events either. Separate watchers rather than widening the pair above, so the
        // NotifyFilter stays narrowly DirectoryName and ordinary file edits keep their existing per-file routing.
        _watchers.Add(CreateDirectoryWatcher(options.SourceRoot));
        _watchers.Add(CreateDirectoryWatcher(options.AdrSourceRoot));

        // _bmad/config.toml (project branding) lives at the repo root under _bmad — under NEITHER source root above.
        // Watch its containing dir when it exists so a config edit live-refreshes too; never CREATE _bmad (that would
        // be an unexpected write to the project structure — there'd be no config.toml to watch anyway). [Story 6.11]
        var configDir = Path.Combine(options.RepoRoot, ForgeOptions.ConfigDirName);
        if (Directory.Exists(configDir))
        {
            _watchers.Add(CreateWatcher(configDir, ForgeOptions.ConfigFileName));
            _configWatcherRegistered = true;
        }
        else
        {
            // _bmad doesn't exist yet at construction time. Without this fallback, a project scaffolded (or a repo
            // cloned) AFTER `specscribe watch` starts would never get its config.toml watched for the rest of that
            // watch session — the gap the 6.11 review deferred. Watch the repo root (non-recursive, directory-name
            // events only) for `_bmad` appearing, then register the real config watcher on demand. This narrows the
            // original gap but does not eliminate every race (the window between construction and Start(), and a
            // delete-then-recreate of `_bmad`, are accepted residual limitations — see deferred-work.md).
            // [Story 6.11 deferred-work cleanup]
            _configDirDetector = CreateConfigDirWatcher(options.RepoRoot);
            _watchers.Add(_configDirDetector);
        }
    }

    private FileSystemWatcher CreateConfigDirWatcher(string repoRoot)
    {
        var watcher = new FileSystemWatcher(repoRoot)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName,
            InternalBufferSize = 65536,
        };
        watcher.Filters.Add(ForgeOptions.ConfigDirName);
        watcher.Created += (_, e) => SafeHandle(() => OnConfigDirCreated(e.FullPath), BmadDirWatcherLabel);
        watcher.Renamed += (_, e) => SafeHandle(() => OnConfigDirCreated(e.FullPath), BmadDirWatcherLabel);
        // Tagged distinctly from CreateWatcher's generic "<watcher>" label so a failure of this specific fallback
        // watcher (repo-root, directory-name events) is distinguishable from the source/ADR/config watchers'
        // errors in the emitted GenerationEvent. [Story 6.11 deferred-work cleanup]
        watcher.Error += (_, e) =>
            SafeNotify(new GenerationEvent(GenerationOutcome.Error, BmadDirWatcherLabel, TimeSpan.Zero, e.GetException().Message));
        return watcher;
    }

    /// <summary>Fires when the repo-root watcher observes something named <c>_bmad</c> appear. Registers the real
    /// config-dir watcher exactly once (idempotent — a Created and an echoing Renamed for the same directory must
    /// not double-register; also a no-op after <see cref="Dispose"/>, so a queued event arriving just after teardown
    /// can't leak a live, never-disposed watcher). The registration flag is set only AFTER the watcher construction
    /// succeeds — if <c>_bmad</c> is deleted between the existence check below and construction (a real, if narrow,
    /// TOCTOU window), the failure is reported as a <see cref="GenerationOutcome.Error"/> event rather than crashing
    /// the watcher thread, and the flag stays clear so a later re-creation of <c>_bmad</c> can still succeed. Once
    /// registered, the repo-root fallback watcher that called this is retired (disabled + disposed) — its job is
    /// done and nothing should keep polling directory-name events for the rest of the session. Internal so the test
    /// suite can drive it deterministically instead of racing a real FileSystemWatcher event.
    /// [Story 6.11 deferred-work cleanup]</summary>
    internal void OnConfigDirCreated(string fullPath) =>
        OnConfigDirCreated(fullPath, () => CreateWatcher(fullPath, ForgeOptions.ConfigFileName));

    /// <summary>Test seam overload: <paramref name="watcherFactory"/> defaults to the real
    /// <see cref="CreateWatcher"/> call above but lets tests inject a throwing factory to deterministically exercise
    /// the TOCTOU catch branch below, which a real filesystem race cannot be landed on reliably in a single-threaded
    /// test. [Story 5.3 review-fix]</summary>
    internal void OnConfigDirCreated(string fullPath, Func<FileSystemWatcher> watcherFactory)
    {
        if (!string.Equals(Path.GetFileName(fullPath), ForgeOptions.ConfigDirName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        lock (_watchersLock)
        {
            if (_disposed || _configWatcherRegistered)
            {
                return;
            }

            FileSystemWatcher watcher;
            try
            {
                watcher = watcherFactory();
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException)
            {
                // _bmad vanished again between the Directory.Exists check above and here — leave the flag clear so
                // a future re-creation can retry; report the miss instead of crashing the watcher-event thread.
                SafeNotify(new GenerationEvent(GenerationOutcome.Error, BmadDirWatcherLabel, TimeSpan.Zero, ex.Message));
                return;
            }

            _configWatcherRegistered = true;
            _watchers.Add(watcher);
            if (_started)
            {
                watcher.EnableRaisingEvents = true;
            }

            // The fallback detector has done its job — retire it so it isn't left running indefinitely just to hit
            // the _configWatcherRegistered early-return on every future _bmad-adjacent directory event.
            if (_configDirDetector is { } detector)
            {
                _watchers.Remove(detector);
                detector.EnableRaisingEvents = false;
                detector.Dispose();
                _configDirDetector = null;
            }
        }
    }

    /// <summary>A filter-less, DirectoryName-only watcher over <paramref name="root"/> whose create/rename/delete
    /// events all debounce onto the single <see cref="TopologySentinelKey"/> and escalate to a full rebuild. No
    /// <c>Filters</c> entries at all — the point is to match ANY directory name, which is exactly what the
    /// name-filtered file watchers structurally cannot do. [Story 5.3]</summary>
    private FileSystemWatcher CreateDirectoryWatcher(string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName,
            InternalBufferSize = 65536,
        };

        watcher.Created += (_, e) => SafeHandle(() => DebounceTopology(e.FullPath), DirectoryWatcherLabel);
        watcher.Deleted += (_, e) => SafeHandle(() => DebounceTopology(e.FullPath), DirectoryWatcherLabel);
        watcher.Renamed += (_, e) => SafeHandle(() =>
        {
            DebounceTopology(e.OldFullPath);
            DebounceTopology(e.FullPath);
        }, DirectoryWatcherLabel);
        // Tagged distinctly from CreateWatcher's generic "<watcher>" label so a directory-watcher failure (buffer
        // overflow on a large rename-refactor is the realistic case) is distinguishable in the event stream. Also
        // forces a fallback rebuild (review-fix, Story 5.3): an overflow means the OS dropped events outright, so
        // logging alone could leave output silently stale — exactly the AC #5 failure this watcher exists to close.
        watcher.Error += (_, e) => SafeHandle(() =>
        {
            SafeNotify(new GenerationEvent(GenerationOutcome.Error, DirectoryWatcherLabel, TimeSpan.Zero, e.GetException().Message));
            ForceTopologyRebuild();
        }, DirectoryWatcherLabel);
        return watcher;
    }

    private FileSystemWatcher CreateWatcher(string root, params string[] filters)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            InternalBufferSize = 65536,
        };
        // Filters collection (net10.0) rather than the single Filter property, so one watcher can admit the whole
        // data-source extension set; Debounce re-guards on the same set at fire time. [Story 6.11]
        foreach (var filter in filters)
        {
            watcher.Filters.Add(filter);
        }

        watcher.Changed += (_, e) => SafeHandle(() => Debounce(e.FullPath), FileWatcherLabel);
        watcher.Created += (_, e) => SafeHandle(() => Debounce(e.FullPath), FileWatcherLabel);
        watcher.Deleted += (_, e) => SafeHandle(() => Debounce(e.FullPath), FileWatcherLabel);
        watcher.Renamed += (_, e) => SafeHandle(() =>
        {
            Debounce(e.OldFullPath);
            Debounce(e.FullPath);
        }, FileWatcherLabel);
        watcher.Error += (_, e) =>
            SafeNotify(new GenerationEvent(GenerationOutcome.Error, FileWatcherLabel, TimeSpan.Zero, e.GetException().Message));
        return watcher;
    }

    public void Start()
    {
        lock (_watchersLock)
        {
            _started = true;
            foreach (var w in _watchers) w.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        lock (_watchersLock)
        {
            _started = false;
            foreach (var w in _watchers) w.EnableRaisingEvents = false;
        }
    }

    private void Debounce(string fullPath)
    {
        // Second gate (the watcher Filters are the first): drop anything outside the watched extension set even if a
        // watcher fires for it. Widened from *.md only to include the yaml/toml data sources. [Story 6.11]
        if (!WatchedExtensions.Any(ext => fullPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _pending.AddOrUpdate(
            fullPath,
            addValueFactory: CreateTimer,
            updateValueFactory: (_, existing) =>
            {
                existing.Change(ForgeOptions.DebounceInterval, Timeout.InfiniteTimeSpan);
                return existing;
            });
    }

    /// <summary>Coalesces every directory create/rename/delete in a burst onto one sentinel-keyed timer, reusing the
    /// same <see cref="_pending"/> dictionary and <see cref="ForgeOptions.DebounceInterval"/> as the per-file path —
    /// so an IDE rename-refactor touching many nested folders still settles into a single rebuild.
    /// <para>Changes under the generated output root are ignored: <see cref="SiteGenerator.GenerateAll"/> recreates
    /// that whole tree on every rebuild, so if a user points <c>--output</c> at a directory INSIDE a watched source
    /// root, a self-triggered rebuild loop would otherwise run forever. The file watchers never had this hazard (they
    /// admit only source extensions, and generation writes <c>.html</c>).</para></summary>
    private void DebounceTopology(string fullPath)
    {
        if (IsUnderOutputRoot(fullPath))
        {
            return;
        }

        ForceTopologyRebuild();
    }

    /// <summary>Arms (or re-arms) the topology debounce timer unconditionally. Factored out of
    /// <see cref="DebounceTopology"/> so the directory watcher's <c>Error</c> handler (a buffer overflow — no single
    /// path is known, since the OS-level event queue is what overflowed) can still force a fallback rebuild rather
    /// than silently trusting an event stream that just proved incomplete. [Story 5.3 review-fix]</summary>
    private void ForceTopologyRebuild()
    {
        _pending.AddOrUpdate(
            TopologySentinelKey,
            addValueFactory: _ => CreateTopologyTimer(),
            updateValueFactory: (_, existing) =>
            {
                existing.Change(ForgeOptions.DebounceInterval, Timeout.InfiniteTimeSpan);
                return existing;
            });
    }

    private bool IsUnderOutputRoot(string fullPath)
    {
        try
        {
            var full = Path.GetFullPath(fullPath);
            var outputRoot = Path.GetFullPath(_options.OutputRoot);
            return full.Equals(outputRoot, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // An unresolvable path can't be shown to be inside the output root; treat it as watchable rather than
            // silently dropping a real topology change (NFR2 — degrade, don't lose the event).
            return false;
        }
    }

    private Timer CreateTopologyTimer()
    {
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            _pending.TryRemove(TopologySentinelKey, out Timer? _);
            timer?.Dispose();
            RunTopologyPass();
        }, null, ForgeOptions.DebounceInterval, Timeout.InfiniteTimeSpan);
        return timer;
    }

    /// <summary>The topology timer's body, minus the timer bookkeeping. No single path is classified here by design —
    /// a folder operation's "changed file" is every file beneath it — so this escalates to the full rebuild, the only
    /// scope that stays coherent across a rename (pages appear at the new location AND the stale ones at the old are
    /// gone). Internal so tests can drive it ON THE CALLING THREAD: see <see cref="RunDebouncedPass"/>'s remarks for
    /// why exercising it through a real <see cref="Timer"/> would be a hostile way to test the crash guard.
    /// [Story 5.3]
    /// <para><b>Sets the delta sidecar's trigger label before escalating</b> (code review, Story 22.6) — this is
    /// the ONLY topology entry point <see cref="RunDebouncedPass"/>'s file-level <c>RunTopology</c> local does not
    /// cover, so without this a genuine directory-level rebuild (a folder create/rename/delete, or the watcher's
    /// own buffer-overflow fallback) left <c>_watchTrigger</c> holding whatever unrelated file last set it, and the
    /// sidecar's <c>trigger</c> field named a stale path instead of the shared
    /// <see cref="TopologyEventLabel"/> sentinel. Label only — <see cref="SiteGenerator.RegenerateTopology"/>'s own
    /// <c>_nextEmitIsFullDelta</c> flag, not this label, decides the delta's <c>full</c> marker.</para></summary>
    internal void RunTopologyPass()
    {
        _generator.SetWatchTrigger(TopologyEventLabel);
        SafeNotify(RunGuarded(() => _generator.RegenerateTopology(), TopologyEventLabel));
    }

    private Timer CreateTimer(string fullPath)
    {
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            _pending.TryRemove(fullPath, out Timer? _);
            timer?.Dispose();
            RunDebouncedPass(fullPath);
        }, null, ForgeOptions.DebounceInterval, Timeout.InfiniteTimeSpan);
        return timer;
    }

    /// <summary>The per-file timer's body, minus the timer bookkeeping. Decides the action from ground truth at fire
    /// time, not from which event triggered it — a save can emit Changed/Created/Deleted in any order before the
    /// debounce settles. <c>IsDataSource</c> is checked FIRST: <c>sprint-status.yaml</c> lives under
    /// <c>implementation-artifacts/</c>, so <c>IsEpicsRelated</c> would otherwise claim it and route to
    /// <c>RegenerateEpics</c> (which skips sprint state). [Story 6.11] The generic <c>GenerateOne</c>/<c>RemoveFor</c>
    /// fallback assumes a markdown artifact; the widened Filters/WatchedExtensions admit yaml/toml across the whole
    /// source root (not just the two named data sources), so a stray non-data-source yaml/toml file is skipped here
    /// rather than mis-handled as markdown. [Story 6.11 review]
    /// <para><b>Internal as a test seam.</b> The crash guards below only matter on a ThreadPool thread, where there is
    /// no caller — which is precisely what makes them hostile to test through a real <see cref="Timer"/>: a
    /// REGRESSION would not fail a test, it would take down the whole test host with it, turning one broken assertion
    /// into a lost suite run. Driving the same body synchronously means an unguarded throw surfaces as an ordinary
    /// test failure on the calling thread. Mirrors the <see cref="OnConfigDirCreated"/> seam Story 6.11 added for the
    /// same reason. [Story 5.3 follow-up]</para></summary>
    internal void RunDebouncedPass(string fullPath)
    {
        // A topology escalation rebuilds the WHOLE site, so attributing its DELTA to the one path that happened to
        // fire would be a lie. The sidecar trigger is therefore relabelled to the SHARED constant RegenerateTopology
        // and the watch log already report, rather than inventing a third spelling.
        // <para>LABEL ONLY. The sidecar's degrade-to-full for this pass is decided by a flag RegenerateTopology
        // sets on itself, NOT by this string — Story 22.6's live verification caught a concurrent save overwriting
        // this label between the set and the emit, which would have silently defeated the guard had correctness
        // depended on it. [Story 22.6 AC #7, Trap 5]</para>
        // <para>The EVENT label is a separate question, and the two are deliberately different (code review
        // 2026-07-29). Story 22.5 routed file-level adds/renames/deletes here, and reporting those under
        // &lt;directory change&gt; inverted that constant's own contract — it exists to say "a directory changed, so do
        // not attribute this to some arbitrary contained file", which is exactly backwards when a named file IS the
        // whole event. A directory pass still has no single honest path and keeps the sentinel; a file pass reports
        // the path that fired. One event either way, as AC #7 requires.</para>
        GenerationEvent RunTopology(string? eventLabel)
        {
            _generator.SetWatchTrigger(TopologyEventLabel);
            return _generator.RegenerateTopology(eventLabel);
        }

        GenerationEvent ev;
        try
        {
            var relative = Path.GetRelativePath(_options.RepoRoot, fullPath).Replace('\\', '/');
            // The delta sidecar's `trigger` LABEL for whatever this pass emits — set here because this is the one
            // place that knows which path fired, and cleared by the topology branch below to the shared
            // <directory change> constant. Label only: the delta's page lists are computed from the manifests, not
            // from this. See SiteGenerator._watchTrigger for the narrow race this accepts and why. [Story 22.6]
            _generator.SetWatchTrigger(relative);
            ev = RunGuarded(() => _generator.IsDataSource(fullPath)
                ? _generator.RegenerateFromDataSource(fullPath)
                // Story 22.5 AC #3: the SCOPE question is answered once, by one named classifier, BEFORE the family
                // question — a topology change strands cross-artifact surfaces no family route re-renders, so which
                // family the path belongs to is not the deciding fact. RegenerateTopology is reused rather than a
                // second full-rebuild path being added: it already IS "collapse GenerateAll's event list to one
                // event", which is exactly the single coherent GenerationEvent AC #7 requires an escalated pass to
                // report. It deliberately takes no outer lock (GenerateAll takes _gate itself), so nothing here may
                // wrap it in one either. IsDataSource stays FIRST and unchanged — sprint-status.yaml already
                // escalates through its own route, and its precedence over IsEpicsRelated is load-bearing.
                : _generator.ClassifyRebuildScope(fullPath) == RebuildScope.Full
                ? RunTopology(relative)
                : _generator.IsAdr(fullPath)
                ? _generator.RegenerateAdrs()
                : _generator.IsEpicsRelated(fullPath)
                ? _generator.RegenerateEpics()
                : !fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? new GenerationEvent(GenerationOutcome.Skipped, relative, TimeSpan.Zero, "non-markdown, not a recognized data source")
                : File.Exists(fullPath)
                ? _generator.GenerateOne(fullPath)
                : _generator.RemoveFor(fullPath), relative);
        }
        catch (Exception ex)
        {
            // Path.GetRelativePath sat outside RunGuarded's boundary — a throw there (a pathological path under
            // resource pressure) would otherwise still escape this Timer callback with no caller, exactly the crash
            // class RunGuarded exists to close. Falls back to the raw full path as the event's label since the
            // relative one couldn't be computed. [Story 5.3 review-fix]
            ev = new GenerationEvent(GenerationOutcome.Error, fullPath, TimeSpan.Zero, ex.Message);
        }
        SafeNotify(ev);
    }

    /// <summary>Runs one regeneration route and converts any escaping exception into an
    /// <see cref="GenerationOutcome.Error"/> event instead of letting it out.
    /// <para>Load-bearing, not defensive decoration: these routes run on a <see cref="Timer"/> callback, i.e. a
    /// ThreadPool thread with no caller to catch anything. An unhandled exception there does not fail one rebuild —
    /// it terminates the whole <c>watch</c> process, losing the live-reload session over a transient the next save
    /// would have fixed. The generator's per-file paths already swallow their own <see cref="IOException"/>s
    /// ("file busy, will retry"), but the whole-tree routes reach filesystem work — the output-root wipe, the
    /// embedded-asset copy — that a scanner, an editor, or an open browser tab can lose a race with. NFR2's
    /// "degrades gracefully with non-fatal notices" is the contract; this is where it is enforced for watch mode.
    /// [Story 5.3 Task 5]</para></summary>
    private GenerationEvent RunGuarded(Func<GenerationEvent> route, string relativePath)
    {
        try
        {
            return route();
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, relativePath, TimeSpan.Zero, ex.Message);
        }
    }

    /// <summary>The single exit through which every emitted event leaves this class, so a throwing REPORTER cannot
    /// kill the watch loop either.
    /// <para><see cref="RunGuarded"/> alone was an incomplete fix: it guards the generator call, then hands the
    /// result to <c>_onEvent</c> — a caller-supplied delegate, invoked on the same unguarded ThreadPool thread. In
    /// production that delegate is <c>ConsoleUi.LogEvent</c>, which writes to the console; a closed or broken stdout
    /// (piping <c>specscribe watch</c> into a process that exits first is the ordinary way to get one) throws
    /// <see cref="IOException"/> from the write and takes the process down. The failure is worse than the one
    /// <see cref="RunGuarded"/> fixed, because it fires on the SUCCESS path — a perfectly good rebuild kills the
    /// session while reporting itself.</para>
    /// <para>The swallow is deliberate and has nowhere better to go: the reporting channel is the thing that just
    /// failed, so re-reporting through it would be the same throw again. Losing one log line is strictly better than
    /// losing the watch session. [Story 5.3 follow-up]</para></summary>
    private void SafeNotify(GenerationEvent ev)
    {
        try
        {
            _onEvent(ev);
        }
        catch
        {
            // Intentionally swallowed — see remarks. The reporter is the failure; there is no second channel.
        }
    }

    /// <summary>Wraps a raw <see cref="FileSystemWatcher"/> event handler body. These run on the watcher's own
    /// event-dispatch thread — the same no-caller situation as the timer callbacks, so the same rule applies: an
    /// escaping exception is a process kill, not a dropped event. Cheap because the bodies are tiny (classify the
    /// path, arm a timer), but <c>new Timer</c> and the path helpers can throw under resource pressure or on a
    /// pathological path, and "rare" is not "never" for a process that is meant to run for hours.
    /// [Story 5.3 follow-up]</summary>
    private void SafeHandle(Action handler, string label)
    {
        try
        {
            handler();
        }
        catch (Exception ex)
        {
            SafeNotify(new GenerationEvent(GenerationOutcome.Error, label, TimeSpan.Zero, ex.Message));
        }
    }

    public void Dispose()
    {
        lock (_watchersLock)
        {
            // Set BEFORE disposing so a _bmad-creation event already queued on the ThreadPool, which acquires this
            // same lock inside OnConfigDirCreated after Dispose released it, is a no-op instead of constructing and
            // enabling a new watcher that would never be torn down (a leaked OS watch handle). [Story 6.11 deferred-work cleanup]
            _disposed = true;
            foreach (var w in _watchers) w.Dispose();
        }
        foreach (var kv in _pending)
        {
            kv.Value.Dispose();
        }
        _pending.Clear();
    }
}
