using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DocsForge;

public enum GenerationOutcome { Generated, Updated, Removed, Skipped, Error }

public sealed record GenerationEvent(GenerationOutcome Outcome, string RelativePath, TimeSpan Elapsed, string? Message = null);

/// <summary>Owns the mapping from _bmad-output/*.md to docs/live/*.html, and keeps the generated index,
/// nav, and epics/story pages in sync.</summary>
public sealed class SiteGenerator
{
    private static readonly Regex ArtifactFilenamePattern = new(@"^(?<epic>\d+)-(?<story>\d+)-", RegexOptions.Compiled);

    private readonly ForgeOptions _options;
    private readonly Dictionary<string, DocModel> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private SiteNav? _nav;
    private EpicsModel? _epicsModel;
    private ProgressModel? _progress;
    private RequirementsModel? _requirements;

    public SiteGenerator(ForgeOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<GenerationEvent> GenerateAll()
    {
        var files = EnumerateSourceFiles();
        var sourceRelatives = files.Select(ToSourceRelative).ToList();

        var events = new List<GenerationEvent>();
        lock (_gate)
        {
            // A full rebuild starts from a clean slate so renamed/removed source docs don't leave
            // orphaned HTML behind (incremental watch-mode updates never wipe the whole tree).
            if (Directory.Exists(_options.OutputRoot))
            {
                Directory.Delete(_options.OutputRoot, recursive: true);
            }

            EnsureScaffold();
            _docs.Clear();

            var nav = SiteNav.Build(sourceRelatives, _options.SiteTitle);
            _nav = nav;

            var epicsSourceFile = FindEpicsSourceFile(files);
            var artifactMap = BuildArtifactMap(files);
            var consumedArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (epicsSourceFile is not null)
            {
                events.AddRange(GenerateEpicsInternal(epicsSourceFile, files, artifactMap, consumedArtifacts, nav));
            }

            foreach (var file in files)
            {
                if (epicsSourceFile is not null && string.Equals(file, epicsSourceFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // handled by GenerateEpicsInternal above
                }

                var rel = ToSourceRelative(file);
                if (consumedArtifacts.Contains(rel))
                {
                    continue; // rendered as a story detail page already
                }

                events.Add(GenerateOneInternal(file, nav));
            }

            WriteIndex(nav);
        }
        return events;
    }

    public GenerationEvent GenerateOne(string sourceFullPath)
    {
        lock (_gate)
        {
            EnsureScaffold();
            var nav = _nav ?? SiteNav.Build(Array.Empty<string>(), _options.SiteTitle);
            var ev = GenerateOneInternal(sourceFullPath, nav);
            WriteIndex(nav);
            return ev;
        }
    }

    public GenerationEvent RemoveFor(string sourceFullPath)
    {
        var sw = Stopwatch.StartNew();
        var relative = ToSourceRelative(sourceFullPath);

        lock (_gate)
        {
            if (_docs.TryGetValue(relative, out var doc))
            {
                var outputFullPath = Path.Combine(_options.OutputRoot, doc.OutputRelativePath);
                if (File.Exists(outputFullPath))
                {
                    File.Delete(outputFullPath);
                }

                _docs.Remove(relative);
                WriteIndex(_nav ?? SiteNav.Build(Array.Empty<string>(), _options.SiteTitle));
                return new GenerationEvent(GenerationOutcome.Removed, relative, sw.Elapsed);
            }

            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "not tracked");
        }
    }

    /// <summary>True for epics.md itself, or any file under implementation-artifacts/ — both feed the
    /// epics/story pages, so either kind of change should trigger a full <see cref="RegenerateEpics"/>
    /// rather than the generic single-file path.</summary>
    public bool IsEpicsRelated(string sourceFullPath)
    {
        if (string.Equals(Path.GetFileName(sourceFullPath), "epics.md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentDir = Path.GetFileName(Path.GetDirectoryName(sourceFullPath) ?? string.Empty);
        return string.Equals(parentDir, "implementation-artifacts", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Re-parses epics.md and rewrites epics.html + every epics/epic-N.html + epics/story-N-M.html.
    /// Also refreshes the nav and the artifact map, so added/removed/renamed story files self-heal without
    /// needing a full restart.</summary>
    public GenerationEvent RegenerateEpics()
    {
        var sw = Stopwatch.StartNew();
        lock (_gate)
        {
            EnsureScaffold();

            var files = EnumerateSourceFiles();
            var nav = SiteNav.Build(files.Select(ToSourceRelative).ToList(), _options.SiteTitle);
            _nav = nav;

            var epicsSourceFile = FindEpicsSourceFile(files);
            if (epicsSourceFile is null)
            {
                WriteIndex(nav);
                return new GenerationEvent(GenerationOutcome.Skipped, "epics.md", sw.Elapsed, "epics.md not found");
            }

            var artifactMap = BuildArtifactMap(files);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var epicsEvents = GenerateEpicsInternal(epicsSourceFile, files, artifactMap, consumed, nav);
            WriteIndex(nav);

            var errored = epicsEvents.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, ToSourceRelative(epicsSourceFile), sw.Elapsed, $"{consumed.Count} stories");
        }
    }

    private List<GenerationEvent> GenerateEpicsInternal(
        string epicsFullPath,
        List<string> files,
        IReadOnlyDictionary<string, string> artifactMap,
        HashSet<string> consumedArtifacts,
        SiteNav nav)
    {
        var events = new List<GenerationEvent>();
        var sw = Stopwatch.StartNew();
        var epicsSourceRelative = ToSourceRelative(epicsFullPath);

        try
        {
            var raw = MarkdownConverter.ReadAllTextShared(epicsFullPath);
            var model = EpicsParser.Parse(raw);

            foreach (var epic in model.Epics)
            {
                foreach (var story in epic.Stories)
                {
                    if (artifactMap.TryGetValue(story.Id, out var artifactFullPath))
                    {
                        story.ArtifactOutputPath = $"epics/story-{story.Id.Replace('.', '-')}.html";
                        story.ArtifactSourcePath = PathUtil.NormalizeSlashes(ToSourceRelative(artifactFullPath));
                        consumedArtifacts.Add(ToSourceRelative(artifactFullPath));
                    }
                }
            }

            // Computed after artifacts are resolved (above) so task counts land on each StoryInfo before
            // any page renders. Git is invoked once here, not per-page.
            var gitPulse = GitMetrics.TryCompute(_options.RepoRoot);
            var progress = ProgressCalculator.Compute(model, artifactMap, gitPulse);
            _epicsModel = model;
            _progress = progress;
            // Requirements come from the same epics.md and roll their progress up from the epics above,
            // so they're parsed here and cached before any page is linkified against them.
            var requirements = RequirementsParser.Parse(raw, model, progress);
            _requirements = requirements;
            var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
            var referenceMap = BuildReferenceMap(files, model, artifactMap, PathUtil.NormalizeSlashes(epicsSourceRelative));

            File.WriteAllText(Path.Combine(_options.OutputRoot, "epics.html"), ApplyRequirementLinks(EpicsTemplater.RenderIndex(model, progress, nav), "epics.html"));

            var epicsDir = Path.Combine(_options.OutputRoot, "epics");
            Directory.CreateDirectory(epicsDir);

            WriteRequirements(requirements, model, progress, nav);

            foreach (var epic in model.Epics)
            {
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyRequirementLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav), $"epics/epic-{epic.Number}.html"));

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null) continue;

                    var artifactFullPath = artifactMap[story.Id];
                    var artifactRelative = ToSourceRelative(artifactFullPath);
                    var artifactRaw = MarkdownConverter.ReadAllTextShared(artifactFullPath);
                    var tasks = TaskListParser.Parse(artifactRaw);
                    var (blurbHtml, remainderHtml) = EpicsParser.SplitStoryArtifact(artifactRaw);
                    var devAgentRecord = EpicsParser.ExtractDevAgentRecord(artifactRaw);

                    // Turn "[Source: _bmad-output/path.md]" citations into real links to the generated page.
                    var storyPrefix = PathUtil.RelativePrefix(story.ArtifactOutputPath);
                    blurbHtml = SourceLinkifier.Linkify(blurbHtml, referenceMap, storyPrefix);
                    remainderHtml = SourceLinkifier.Linkify(remainderHtml, referenceMap, storyPrefix);
                    devAgentRecord = devAgentRecord
                        .Select(e => (e.Label, ContentHtml: SourceLinkifier.Linkify(e.ContentHtml, referenceMap, storyPrefix)))
                        .ToList();

                    // story.Status/TasksDone were filled by ProgressCalculator above — no re-read needed.
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, artifactRelative, blurbHtml, remainderHtml, devAgentRecord, tasks, nav);
                    File.WriteAllText(Path.Combine(_options.OutputRoot, "epics", $"story-{story.Id.Replace('.', '-')}.html"), ApplyRequirementLinks(storyHtml, story.ArtifactOutputPath!));
                }
            }

            events.Add(new GenerationEvent(GenerationOutcome.Generated, epicsSourceRelative, sw.Elapsed, $"{model.Epics.Count} epics"));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, epicsSourceRelative, sw.Elapsed, ex.Message));
        }

        return events;
    }

    private GenerationEvent GenerateOneInternal(string sourceFullPath, SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        var relative = ToSourceRelative(sourceFullPath);

        if (IsIgnored(sourceFullPath))
        {
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "ignored file");
        }

        if (!File.Exists(sourceFullPath))
        {
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "no longer exists");
        }

        try
        {
            var alreadyExisted = _docs.ContainsKey(relative);
            var outputRelative = PathUtil.ToOutputRelative(relative);
            var doc = MarkdownConverter.Convert(sourceFullPath, relative, outputRelative);

            var outputFullPath = Path.Combine(_options.OutputRoot, outputRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);
            File.WriteAllText(outputFullPath, ApplyRequirementLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

            _docs[relative] = doc;
            var outcome = alreadyExisted ? GenerationOutcome.Updated : GenerationOutcome.Generated;
            return new GenerationEvent(outcome, relative, sw.Elapsed);
        }
        catch (IOException)
        {
            // Mid-write from an editor/BMad tool — the watcher's next debounced event will retry.
            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "file busy, will retry");
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, relative, sw.Elapsed, ex.Message);
        }
    }

    private void WriteIndex(SiteNav nav)
    {
        var indexPath = Path.Combine(_options.OutputRoot, "index.html");
        var docs = _docs.Values.ToList();
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements);
        File.WriteAllText(indexPath, ApplyRequirementLinks(html, "index.html"));
    }

    /// <summary>Writes requirements.html plus one detail page per FR/NFR. Each page is linkified against the
    /// requirement set (the detail page skips its own id so it never self-links).</summary>
    private void WriteRequirements(RequirementsModel requirements, EpicsModel model, ProgressModel progress, SiteNav nav)
    {
        File.WriteAllText(
            Path.Combine(_options.OutputRoot, "requirements.html"),
            ApplyRequirementLinks(RequirementsTemplater.RenderIndex(requirements, progress, nav), "requirements.html"));

        var requirementsDir = Path.Combine(_options.OutputRoot, "requirements");
        Directory.CreateDirectory(requirementsDir);

        foreach (var req in requirements.All)
        {
            var coveringEpic = req.CoverageEpicNumber is { } n
                ? model.Epics.FirstOrDefault(e => e.Number == n)
                : null;
            var outputRelative = $"requirements/{req.Slug}.html";
            var html = RequirementsTemplater.RenderRequirement(req, coveringEpic, progress, nav);
            File.WriteAllText(Path.Combine(requirementsDir, $"{req.Slug}.html"), ApplyRequirementLinks(html, outputRelative, req.Id));
        }
    }

    /// <summary>Whole-page pass that turns every "FR25"/"NFR7" reference into a link to its detail page.
    /// A no-op until epics.md has been parsed (so <see cref="_requirements"/> exists); safe to call on
    /// every page since <see cref="RequirementLinkifier"/> skips tokens already inside links.</summary>
    private string ApplyRequirementLinks(string html, string outputRelativePath, string? skipId = null)
    {
        if (_requirements is null) return html;
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        return RequirementLinkifier.Linkify(html, _requirements, prefix, skipId);
    }

    private void EnsureScaffold()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        var cssDest = Path.Combine(_options.OutputRoot, ForgeOptions.StylesheetName);
        if (File.Exists(_options.StylesheetSourcePath))
        {
            File.Copy(_options.StylesheetSourcePath, cssDest, overwrite: true);
        }
    }

    private List<string> EnumerateSourceFiles() =>
        Directory.Exists(_options.SourceRoot)
            ? Directory.EnumerateFiles(_options.SourceRoot, "*.md", SearchOption.AllDirectories)
                .Where(p => !IsIgnored(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    private static string? FindEpicsSourceFile(IEnumerable<string> files) =>
        files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), "epics.md", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string> BuildArtifactMap(IEnumerable<string> files)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            var parentDir = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (!string.Equals(parentDir, "implementation-artifacts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            var m = ArtifactFilenamePattern.Match(name);
            if (!m.Success) continue;

            var key = $"{int.Parse(m.Groups["epic"].Value)}.{int.Parse(m.Groups["story"].Value)}";
            map[key] = path;
        }
        return map;
    }

    /// <summary>Maps every known source path (normalized, "_bmad-output/"-relative) to the URL its content
    /// actually lives at — most are a straight extension swap, but epics.md points at the generated
    /// epics.html and a consumed implementation-artifact points at its story page, not their generic
    /// mirrored render. Powers <see cref="SourceLinkifier"/> so "[Source: ...]" citations become real links.</summary>
    private Dictionary<string, string> BuildReferenceMap(
        List<string> files,
        EpicsModel model,
        IReadOnlyDictionary<string, string> artifactMap,
        string epicsSourceRelative)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var rel = PathUtil.NormalizeSlashes(ToSourceRelative(file));
            map[rel] = string.Equals(rel, epicsSourceRelative, StringComparison.OrdinalIgnoreCase)
                ? SiteNav.EpicsOutputPath
                : PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(rel));
        }

        foreach (var epic in model.Epics)
        {
            foreach (var story in epic.Stories)
            {
                if (story.ArtifactOutputPath is { } ap && artifactMap.TryGetValue(story.Id, out var artifactFullPath))
                {
                    map[PathUtil.NormalizeSlashes(ToSourceRelative(artifactFullPath))] = ap;
                }
            }
        }

        return map;
    }

    private string ToSourceRelative(string fullPath) =>
        Path.GetRelativePath(_options.SourceRoot, fullPath);

    private static bool IsIgnored(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("~$", StringComparison.Ordinal)
            || name.StartsWith('.')
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".crswap", StringComparison.OrdinalIgnoreCase);
    }
}
