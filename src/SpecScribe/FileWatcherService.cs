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
        foreach (var w in _watchers) w.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        foreach (var w in _watchers) w.EnableRaisingEvents = false;
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
        foreach (var w in _watchers) w.Dispose();
        foreach (var kv in _pending)
        {
            kv.Value.Dispose();
        }
        _pending.Clear();
    }
}
