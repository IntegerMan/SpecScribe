using System.Collections.Concurrent;

namespace SpecScribe;

/// <summary>Watches _bmad-output (and the hand-authored docs/adrs) for *.md changes and drives the
/// SiteGenerator, debouncing the burst of events a single save typically produces. Reads are always shared
/// (see MarkdownConverter), so this never takes a write lock on anything under the watched tree.</summary>
public sealed class FileWatcherService : IDisposable
{
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

        Directory.CreateDirectory(options.SourceRoot);
        _watchers.Add(CreateWatcher(options.SourceRoot));

        // The hand-authored ADRs are a second, read-only source; watch them too so edits live-reload.
        Directory.CreateDirectory(options.AdrSourceRoot);
        _watchers.Add(CreateWatcher(options.AdrSourceRoot));
    }

    private FileSystemWatcher CreateWatcher(string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            Filter = "*.md",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            InternalBufferSize = 65536,
        };

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
        if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
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
            var ev = _generator.IsAdr(fullPath)
                ? _generator.RegenerateAdrs()
                : _generator.IsEpicsRelated(fullPath)
                    ? _generator.RegenerateEpics()
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
