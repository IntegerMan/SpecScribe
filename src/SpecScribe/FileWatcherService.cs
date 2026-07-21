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
        watcher.Created += (_, e) => OnConfigDirCreated(e.FullPath);
        watcher.Renamed += (_, e) => OnConfigDirCreated(e.FullPath);
        // Tagged distinctly from CreateWatcher's generic "<watcher>" label so a failure of this specific fallback
        // watcher (repo-root, directory-name events) is distinguishable from the source/ADR/config watchers'
        // errors in the emitted GenerationEvent. [Story 6.11 deferred-work cleanup]
        watcher.Error += (_, e) =>
            _onEvent(new GenerationEvent(GenerationOutcome.Error, "<bmad-dir-watcher>", TimeSpan.Zero, e.GetException().Message));
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
    internal void OnConfigDirCreated(string fullPath)
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
                watcher = CreateWatcher(fullPath, ForgeOptions.ConfigFileName);
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException)
            {
                // _bmad vanished again between the Directory.Exists check above and here — leave the flag clear so
                // a future re-creation can retry; report the miss instead of crashing the watcher-event thread.
                _onEvent(new GenerationEvent(GenerationOutcome.Error, "<bmad-dir-watcher>", TimeSpan.Zero, ex.Message));
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

        watcher.Changed += (_, e) => Debounce(e.FullPath);
        watcher.Created += (_, e) => Debounce(e.FullPath);
        watcher.Deleted += (_, e) => Debounce(e.FullPath);
        watcher.Renamed += (_, e) =>
        {
            Debounce(e.OldFullPath);
            Debounce(e.FullPath);
        };
        watcher.Error += (_, e) =>
            _onEvent(new GenerationEvent(GenerationOutcome.Error, "<watcher>", TimeSpan.Zero, e.GetException().Message));
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

    private Timer CreateTimer(string fullPath)
    {
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            _pending.TryRemove(fullPath, out Timer? _);
            timer?.Dispose();

            // Decide the action from ground truth at fire time, not from which event triggered it —
            // a save can emit Changed/Created/Deleted in any order before the debounce settles.
            // IsDataSource is checked FIRST: sprint-status.yaml is under implementation-artifacts/, so
            // IsEpicsRelated would otherwise claim it and route to RegenerateEpics (which skips sprint state). [Story 6.11]
            // The generic GenerateOne/RemoveFor fallback assumes a markdown artifact; the widened Filters/WatchedExtensions
            // admit yaml/toml across the whole source root (not just the two named data sources), so a stray non-data-source
            // yaml/toml file must be skipped here rather than mis-handled as markdown. [Story 6.11 review]
            var ev = _generator.IsDataSource(fullPath)
                ? _generator.RegenerateFromDataSource(fullPath)
                : _generator.IsAdr(fullPath)
                    ? _generator.RegenerateAdrs()
                    : _generator.IsEpicsRelated(fullPath)
                        ? _generator.RegenerateEpics()
                        : !fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                            ? new GenerationEvent(GenerationOutcome.Skipped, Path.GetRelativePath(_options.RepoRoot, fullPath).Replace('\\', '/'), TimeSpan.Zero, "non-markdown, not a recognized data source")
                            : File.Exists(fullPath)
                                ? _generator.GenerateOne(fullPath)
                                : _generator.RemoveFor(fullPath);
            _onEvent(ev);
        }, null, ForgeOptions.DebounceInterval, Timeout.InfiniteTimeSpan);
        return timer;
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
