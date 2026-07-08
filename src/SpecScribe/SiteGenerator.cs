using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SpecScribe;

public enum GenerationOutcome { Generated, Updated, Removed, Skipped, Error }

public sealed record GenerationEvent(GenerationOutcome Outcome, string RelativePath, TimeSpan Elapsed, string? Message = null);

/// <summary>Owns the mapping from _bmad-output/*.md to SpecScribeOutput/*.html, and keeps the generated index,
/// nav, and epics/story pages in sync.</summary>
public sealed class SiteGenerator
{
    private static readonly Regex ArtifactFilenamePattern = new(@"^(?<epic>\d+)-(?<story>\d+)-", RegexOptions.Compiled);
    private static readonly Regex AdrNumberPattern = new(@"^(?<num>\d+)", RegexOptions.Compiled);
    private static readonly Regex AdrStatusPattern = new(@"^\*\*Status:\*\*\s*(?<status>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkPattern = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);

    private readonly ForgeOptions _options;
    private readonly Dictionary<string, DocModel> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private SiteNav? _nav;
    private ModuleContext _module = ModuleContext.None;
    private EpicsModel? _epicsModel;
    private ProgressModel? _progress;
    private RequirementsModel? _requirements;
    private List<AdrEntry> _adrs = new();
    private List<CommitDayEntry> _commitDays = new();
    private SprintStatus? _sprint;
    private List<RetroModel> _retros = new();

    public SiteGenerator(ForgeOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<GenerationEvent> GenerateAll(IGenerationReporter? reporter = null)
    {
        reporter?.BeginPhase(GenerationPhase.Scan);
        var files = EnumerateSourceFiles();
        var sourceRelatives = files.Select(ToSourceRelative).ToList();
        reporter?.EndPhase(GenerationPhase.Scan);

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

            // Parse the sprint tracking file once, up front, so its presence drives the nav gate and both the
            // sprint page and the home widget read the same parsed instance. Missing/malformed → null → the
            // page, widget, and nav item all omit cleanly. [Story 2.3 Task 1/5]
            _sprint = SprintStatusParser.ParseFile(SprintSourcePath);

            var nav = BuildNav(sourceRelatives);
            _nav = nav;

            // Render the README up front so that, if it fails, we can drop the Readme nav entry before any
            // other page is written — the site never links to a readme.html that wasn't actually produced.
            GenerationEvent? readmeEvent = null;
            if (ReadmeAvailable)
            {
                readmeEvent = GenerateReadmeInternal(nav);
                if (readmeEvent is { Outcome: GenerationOutcome.Error })
                {
                    nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), hasReadme: false, hasSprint: SprintAvailable);
                    _nav = nav;
                }
            }

            var epicsSourceFile = FindEpicsSourceFile(files);
            var artifactMap = BuildArtifactMap(files);
            var consumedArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (epicsSourceFile is not null)
            {
                reporter?.BeginPhase(GenerationPhase.Epics);
                events.AddRange(GenerateEpicsInternal(epicsSourceFile, files, artifactMap, consumedArtifacts, nav));
                reporter?.EndPhase(GenerationPhase.Epics);
            }

            // Retrospective notes (epic-N-retro-*.md) are a first-class artifact class: render each as a
            // dedicated stylized page (RetroTemplater) — needs the epics model above for the epic link — and
            // consume them so the generic pages loop doesn't also render them. [Story 2.3 retro pages]
            var retroFiles = files.Where(RetroParser.IsRetroFile).ToList();
            foreach (var rf in retroFiles) consumedArtifacts.Add(ToSourceRelative(rf));
            WriteRetros(retroFiles, nav);

            // Epic/story artifacts were rendered as detail pages above; everything else renders standalone.
            var pageFiles = files
                .Where(file => epicsSourceFile is null || !string.Equals(file, epicsSourceFile, StringComparison.OrdinalIgnoreCase))
                .Where(file => !consumedArtifacts.Contains(ToSourceRelative(file)))
                .ToList();

            reporter?.BeginPhase(GenerationPhase.Pages, pageFiles.Count);
            foreach (var file in pageFiles)
            {
                events.Add(GenerateOneInternal(file, nav));
                reporter?.Tick(GenerationPhase.Pages);
            }
            reporter?.EndPhase(GenerationPhase.Pages);

            reporter?.BeginPhase(GenerationPhase.Adrs);
            events.AddRange(GenerateAdrsInternal(nav));
            reporter?.EndPhase(GenerationPhase.Adrs);

            // Per-day commit pages the heatmap links to. Git is only computed inside GenerateEpicsInternal,
            // so a project without an epics.md has no pulse here — which is consistent: no heatmap renders
            // either, so there's nothing to link to.
            if (_progress?.Git is { } gitPulse)
            {
                reporter?.BeginPhase(GenerationPhase.CommitDays);
                events.AddRange(GenerateCommitDaysInternal(gitPulse, nav));
                reporter?.EndPhase(GenerationPhase.CommitDays);
            }

            if (readmeEvent is not null)
            {
                events.Add(readmeEvent);
            }

            // The sprint page reads the epics model (for real story/epic titles + links), so it's written
            // after the epics phase. Gated on parsed sprint data; a no-op when there is none. [Story 2.3 Task 3/5]
            WriteSprint(nav);
            WriteRetroIndex(nav);
            WriteActionItems(nav);

            reporter?.BeginPhase(GenerationPhase.Index);
            WriteIndex(nav);
            reporter?.EndPhase(GenerationPhase.Index);
        }
        return events;
    }

    public GenerationEvent GenerateOne(string sourceFullPath)
    {
        lock (_gate)
        {
            EnsureScaffold();
            var nav = _nav ?? BuildNav(Array.Empty<string>());
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
                WriteIndex(_nav ?? BuildNav(Array.Empty<string>()));
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
            var nav = BuildNav(files.Select(ToSourceRelative).ToList());
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

    /// <summary>True for any file under the ADR source root — routes watch-mode events to
    /// <see cref="RegenerateAdrs"/> rather than the _bmad-output pipeline.</summary>
    public bool IsAdr(string sourceFullPath)
    {
        var full = Path.GetFullPath(sourceFullPath);
        var root = Path.GetFullPath(_options.AdrSourceRoot);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Re-renders every ADR page (and the index that lists them). ADRs cross-link to one another,
    /// so a single edit is rebuilt as a set rather than one page in isolation.</summary>
    public GenerationEvent RegenerateAdrs()
    {
        var sw = Stopwatch.StartNew();
        lock (_gate)
        {
            EnsureScaffold();
            var nav = _nav ?? BuildNav(Array.Empty<string>());
            var events = GenerateAdrsInternal(nav);
            WriteIndex(nav);

            var errored = events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, "adrs", sw.Elapsed, $"{_adrs.Count} ADRs");
        }
    }

    /// <summary>Renders each hand-authored record under <c>docs/adrs</c> into <c>SpecScribeOutput/adrs</c>. README.md
    /// becomes the landing page (index.html); numbered records also become cards on the home index. The whole
    /// ADR output directory is rebuilt each pass so a deleted or renamed record can't leave a stale page behind.</summary>
    private List<GenerationEvent> GenerateAdrsInternal(SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var adrOutputDir = Path.Combine(_options.OutputRoot, ForgeOptions.AdrOutputSubdir);
        if (Directory.Exists(adrOutputDir))
        {
            Directory.Delete(adrOutputDir, recursive: true);
        }

        var entries = new List<AdrEntry>();
        foreach (var file in EnumerateAdrFiles())
        {
            var sw = Stopwatch.StartNew();
            var fileName = Path.GetFileName(file);
            var isReadme = string.Equals(fileName, "README.md", StringComparison.OrdinalIgnoreCase);

            // README is the landing page; numbered files are the records; everything else (e.g. TEMPLATE.md)
            // still renders so its cross-links resolve, but never becomes a card.
            var outputName = isReadme ? "index.html" : Path.ChangeExtension(fileName, ".html");
            var outputRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{outputName}");
            var sourceRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{fileName}");

            try
            {
                var raw = MarkdownConverter.ReadAllTextShared(file);
                var parsed = MarkdownConverter.Convert(file, sourceRelative, outputRelative);
                var doc = new DocModel
                {
                    SourceRelativePath = parsed.SourceRelativePath,
                    OutputRelativePath = parsed.OutputRelativePath,
                    Title = parsed.Title,
                    Frontmatter = parsed.Frontmatter,
                    BodyHtml = AdrLinkRewriter.Rewrite(parsed.BodyHtml),
                    Headings = parsed.Headings,
                    HasMermaid = parsed.HasMermaid,
                };

                var outputFullPath = Path.Combine(_options.OutputRoot, ForgeOptions.AdrOutputSubdir, outputName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);
                File.WriteAllText(outputFullPath, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

                var number = ParseAdrNumber(fileName);
                if (!isReadme && number is not null)
                {
                    entries.Add(new AdrEntry(doc.Title, outputRelative, sourceRelative, ExtractAdrStatus(raw), number));
                }

                events.Add(new GenerationEvent(GenerationOutcome.Generated, sourceRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, sourceRelative, sw.Elapsed, ex.Message));
            }
        }

        _adrs = entries.OrderBy(e => e.Number).ToList();
        return events;
    }

    /// <summary>Emits one <c>commits/{yyyy-MM-dd}.html</c> page per linked day (the exact set the heatmap
    /// links to, via <see cref="Charts.LinkedCommitDays"/>), each listing that day's commits with prev/next
    /// links to the adjacent active days. Mirrors <see cref="GenerateAdrsInternal"/>: wipe+recreate the dir,
    /// render a bespoke page, run reference-linkification so "Story N.M"/"FR25" mentions in subjects become
    /// links, and write.</summary>
    private List<GenerationEvent> GenerateCommitDaysInternal(GitPulse git, SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var commitsDir = Path.Combine(_options.OutputRoot, "commits");
        if (Directory.Exists(commitsDir))
        {
            Directory.Delete(commitsDir, recursive: true);
        }

        var days = Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, DateOnly.FromDateTime(DateTime.Now));
        if (days.Count == 0) return events;

        Directory.CreateDirectory(commitsDir);
        var entries = new List<CommitDayEntry>();
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var sw = Stopwatch.StartNew();
            var outputRelative = PathUtil.NormalizeSlashes($"commits/{Charts.D(day)}.html");
            try
            {
                var prevDay = i > 0 ? days[i - 1] : (DateOnly?)null;
                var nextDay = i < days.Count - 1 ? days[i + 1] : (DateOnly?)null;
                var html = CommitDayTemplater.RenderPage(day, git.CommitsByDay[day], prevDay, nextDay, nav);

                File.WriteAllText(
                    Path.Combine(commitsDir, $"{Charts.D(day)}.html"),
                    ApplyReferenceLinks(html, outputRelative));

                entries.Add(new CommitDayEntry(day, outputRelative));
                events.Add(new GenerationEvent(GenerationOutcome.Generated, outputRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, outputRelative, sw.Elapsed, ex.Message));
            }
        }

        _commitDays = entries;
        return events;
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

            File.WriteAllText(Path.Combine(_options.OutputRoot, "epics.html"), ApplyReferenceLinks(EpicsTemplater.RenderIndex(model, progress, nav, _module.Commands), "epics.html"));

            // Rebuild the epics output dir each pass so a story removed or renumbered in epics.md — or an
            // undrafted story that got a placeholder and then vanished — can't leave a stale page behind,
            // mirroring the ADR output dir's rebuild. GenerateAll already wiped OutputRoot (no-op here); this
            // matters for watch-mode RegenerateEpics, which doesn't wipe the whole tree.
            var epicsDir = Path.Combine(_options.OutputRoot, "epics");
            if (Directory.Exists(epicsDir)) Directory.Delete(epicsDir, recursive: true);
            Directory.CreateDirectory(epicsDir);

            WriteRequirements(requirements, model, progress, nav);

            foreach (var epic in model.Epics)
            {
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyReferenceLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav, _module.Commands), $"epics/epic-{epic.Number}.html", skipEpicNumber: epic.Number));

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null)
                    {
                        // Undrafted story: emit a placeholder page at the exact path its real page will
                        // use, so "Story N.M" mentions always have a live target and a later-drafted
                        // artifact overwrites it in place. ArtifactOutputPath stays null — placeholders
                        // must never count as detailed stories anywhere progress is computed.
                        var placeholderPath = StoryEpicLinkifier.StoryPagePath(story.Id);
                        var placeholderHtml = EpicsTemplater.RenderStoryPlaceholder(epic, story, nav, _module.Commands);
                        File.WriteAllText(Path.Combine(_options.OutputRoot, placeholderPath.Replace('/', Path.DirectorySeparatorChar)), ApplyReferenceLinks(placeholderHtml, placeholderPath, skipStoryId: story.Id));
                        continue;
                    }

                    var artifactFullPath = artifactMap[story.Id];
                    var artifactRelative = ToSourceRelative(artifactFullPath);
                    var artifactRaw = MarkdownConverter.ReadAllTextShared(artifactFullPath);
                    var tasks = TaskListParser.Parse(artifactRaw);
                    var (blurbHtml, remainderHtml) = EpicsParser.SplitStoryArtifact(artifactRaw);
                    var acceptanceCriteria = EpicsParser.ExtractAcceptanceCriteria(artifactRaw);
                    var devAgentRecord = EpicsParser.ExtractDevAgentRecord(artifactRaw);
                    var reviewFindingsHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Review Findings");
                    var changeLogHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Change Log");

                    // Turn "[Source: _bmad-output/path.md]" citations into real links to the generated page.
                    var storyPrefix = PathUtil.RelativePrefix(story.ArtifactOutputPath);
                    blurbHtml = SourceLinkifier.Linkify(blurbHtml, referenceMap, storyPrefix);
                    remainderHtml = SourceLinkifier.Linkify(remainderHtml, referenceMap, storyPrefix);
                    reviewFindingsHtml = SourceLinkifier.Linkify(reviewFindingsHtml, referenceMap, storyPrefix);
                    changeLogHtml = SourceLinkifier.Linkify(changeLogHtml, referenceMap, storyPrefix);
                    acceptanceCriteria = acceptanceCriteria
                        .Select(ac => ac with { Html = SourceLinkifier.Linkify(ac.Html, referenceMap, storyPrefix) })
                        .ToList();
                    devAgentRecord = devAgentRecord
                        .Select(e => (e.Label, ContentHtml: SourceLinkifier.Linkify(e.ContentHtml, referenceMap, storyPrefix)))
                        .ToList();

                    // Deep-link every "(AC: #N)" reference in the plan to its criterion panel above.
                    var criteriaByNumber = acceptanceCriteria.ToDictionary(ac => ac.Number, ac => ac.PlainText);
                    remainderHtml = EpicsParser.LinkifyAcReferences(remainderHtml, criteriaByNumber);

                    // story.Status/TasksDone were filled by ProgressCalculator above — no re-read needed.
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, artifactRelative, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord, tasks, reviewFindingsHtml, changeLogHtml, nav, _module.Commands);
                    File.WriteAllText(Path.Combine(_options.OutputRoot, "epics", $"story-{story.Id.Replace('.', '-')}.html"), ApplyReferenceLinks(storyHtml, story.ArtifactOutputPath!, skipStoryId: story.Id));
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
            doc.Companions = ResolveSpecCompanions(doc);

            var outputFullPath = Path.Combine(_options.OutputRoot, outputRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);
            File.WriteAllText(outputFullPath, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

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
        var work = WorkInventory.Build(docs);
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands, work, _sprint, _retros);
        File.WriteAllText(indexPath, ApplyReferenceLinks(html, "index.html"));
    }

    /// <summary>Renders each retrospective note into its dedicated <see cref="RetroTemplater"/> page (at the
    /// same <c>implementation-artifacts/…html</c> path the generic pipeline would have used, so existing links
    /// resolve), reference-linkified like every page, and caches the parsed set for the sprint modal + home
    /// Retrospectives section. [Story 2.3 retro pages]</summary>
    private void WriteRetros(IReadOnlyList<string> retroFiles, SiteNav nav)
    {
        var retros = new List<RetroModel>();
        foreach (var file in retroFiles)
        {
            var sourceRel = ToSourceRelative(file);
            var outputRel = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(sourceRel));
            var retro = RetroParser.Parse(file, sourceRel, outputRel);

            var outputFull = Path.Combine(_options.OutputRoot, outputRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputFull)!);
            File.WriteAllText(outputFull, ApplyReferenceLinks(RetroTemplater.RenderPage(retro, _epicsModel, nav), outputRel));
            retros.Add(retro);
        }
        _retros = retros.OrderBy(r => r.EpicNumber).ThenBy(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Maps an epic number to the output path of its (latest, by filename) retrospective page — the
    /// link target for an open action item tagged with that epic. [Story 2.3 retro pages]</summary>
    private IReadOnlyDictionary<int, string> EpicRetroMap =>
        _retros.GroupBy(r => r.EpicNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).First().OutputRelativePath);

    /// <summary>Path to the sprint tracking file — located by well-known name anywhere under
    /// <see cref="ForgeOptions.SourceRoot"/> (it is a <c>.yaml</c>, so it is NOT in the <c>*.md</c> source
    /// enumeration). Null when absent, which drives full graceful omission. [Story 2.3 Task 1]</summary>
    private string? SprintSourcePath =>
        Directory.Exists(_options.SourceRoot)
            ? Directory.EnumerateFiles(_options.SourceRoot, "sprint-status.yaml", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;

    /// <summary>True once the sprint tracking file parsed into usable data — the single signal the sprint
    /// page, home widget, and nav item all gate on (a present-but-malformed file parses to null and omits
    /// everywhere, matching NFR2 graceful degradation). [Story 2.3 Task 5]</summary>
    private bool SprintAvailable => _sprint is not null;

    /// <summary>Writes <c>sprint.html</c> from the cached parsed sprint status, reusing the epics model for
    /// real titles/links. Reference-linkified like every other page. Omitted entirely (no page) when there is
    /// no sprint data. [Story 2.3 Task 3/5]</summary>
    private void WriteSprint(SiteNav nav)
    {
        if (_sprint is null) return;
        var html = SprintTemplater.RenderIndex(_sprint, _epicsModel, nav, _module.Commands, _retros);
        File.WriteAllText(Path.Combine(_options.OutputRoot, SiteNav.SprintOutputPath), ApplyReferenceLinks(html, SiteNav.SprintOutputPath));
    }

    /// <summary>Writes the retrospectives index (<c>retros.html</c>) when any retro exists — the target of the
    /// sprint page's "Retros" link. [Story 2.3 polish #5]</summary>
    private void WriteRetroIndex(SiteNav nav)
    {
        if (_retros.Count == 0) return;
        var html = RetroTemplater.RenderIndex(_retros, nav);
        File.WriteAllText(Path.Combine(_options.OutputRoot, SiteNav.RetrosOutputPath), ApplyReferenceLinks(html, SiteNav.RetrosOutputPath));
    }

    /// <summary>Writes the open-action-items page (<c>action-items.html</c>) when the sprint tracks open items —
    /// the target of the sprint page's flag button and the home retro callout. Each item links to its epic's
    /// retro page and offers a quick-dev "Resolve with AI" command. [Story 2.3 polish #5]</summary>
    private void WriteActionItems(SiteNav nav)
    {
        var open = _sprint?.OpenActionItems;
        if (open is null || open.Count == 0) return;
        // NOT reference-linkified: the "Resolve with AI" data-copy payload embeds the action text (which can
        // contain "Epic N"/"Story N.M" mentions); the linkifier would wrap those in <a> tags INSIDE the
        // attribute value and corrupt the copyable command. [Story 2.3 polish #5]
        var html = ActionItemsTemplater.RenderPage(open, EpicRetroMap, _module.Commands, nav);
        File.WriteAllText(Path.Combine(_options.OutputRoot, SiteNav.ActionItemsOutputPath), html);
    }

    /// <summary>Path to the repo-root README that feeds the optional Readme page.</summary>
    private string ReadmeSourcePath => Path.Combine(_options.RepoRoot, "README.md");

    /// <summary>True when a README page should be produced: the feature is enabled and the file exists.
    /// The nav is derived from this so a missing/disabled README simply omits the card and link.</summary>
    private bool ReadmeAvailable => _options.IncludeReadme && File.Exists(ReadmeSourcePath);

    /// <summary>Renders the repo-root README.md into a standalone stylized <c>readme.html</c>. Kept out of
    /// <see cref="_docs"/> so it never doubles up as a document-grid card — it is surfaced only via the nav
    /// and the Readme quick link. Returns null (no event) when the feature is disabled or no README exists.</summary>
    private GenerationEvent? GenerateReadmeInternal(SiteNav nav)
    {
        if (!ReadmeAvailable) return null;

        var sw = Stopwatch.StartNew();
        try
        {
            var doc = MarkdownConverter.Convert(ReadmeSourcePath, "README.md", SiteNav.ReadmeOutputPath);
            var outputFullPath = Path.Combine(_options.OutputRoot, SiteNav.ReadmeOutputPath);
            File.WriteAllText(outputFullPath, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), SiteNav.ReadmeOutputPath));
            return new GenerationEvent(GenerationOutcome.Generated, "README.md", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, "README.md", sw.Elapsed, ex.Message);
        }
    }

    /// <summary>Writes requirements.html plus one detail page per FR/NFR. Each page is linkified against the
    /// requirement set (the detail page skips its own id so it never self-links).</summary>
    private void WriteRequirements(RequirementsModel requirements, EpicsModel model, ProgressModel progress, SiteNav nav)
    {
        File.WriteAllText(
            Path.Combine(_options.OutputRoot, "requirements.html"),
            ApplyReferenceLinks(RequirementsTemplater.RenderIndex(requirements, progress, nav), "requirements.html"));

        var requirementsDir = Path.Combine(_options.OutputRoot, "requirements");
        Directory.CreateDirectory(requirementsDir);

        foreach (var req in requirements.All)
        {
            var coveringEpic = req.CoverageEpicNumber is { } n
                ? model.Epics.FirstOrDefault(e => e.Number == n)
                : null;
            var outputRelative = $"requirements/{req.Slug}.html";
            var html = RequirementsTemplater.RenderRequirement(req, coveringEpic, progress, nav);
            File.WriteAllText(Path.Combine(requirementsDir, $"{req.Slug}.html"), ApplyReferenceLinks(html, outputRelative, skipRequirementId: req.Id));
        }
    }

    /// <summary>Whole-page pass that turns every "FR25"/"NFR7" reference into a link to its detail page,
    /// and every "Story N.M"/"Epic N" mention into a link to that story/epic page. A no-op until epics.md
    /// has been parsed (so <see cref="_requirements"/>/<see cref="_epicsModel"/> exist); safe to call on
    /// every page since both linkifiers skip tokens already inside links. Story/epic pages pass their own
    /// id so a page never links to itself.</summary>
    private string ApplyReferenceLinks(string html, string outputRelativePath, string? skipRequirementId = null, string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        if (_requirements is not null)
        {
            html = RequirementLinkifier.Linkify(html, _requirements, prefix, skipRequirementId);
        }
        if (_epicsModel is not null)
        {
            html = StoryEpicLinkifier.Linkify(html, _epicsModel, prefix, skipStoryId, skipEpicNumber);
        }
        return html;
    }

    private void EnsureScaffold()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        // The stylesheet + script ship as embedded resources so the global-tool package needs no loose asset files.
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.css", ForgeOptions.StylesheetName);
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.js", ForgeOptions.ScriptName);
    }

    private void CopyEmbeddedAsset(string resourceName, string outputFileName)
    {
        var dest = Path.Combine(_options.OutputRoot, outputFileName);
        using var source = typeof(SiteGenerator).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded asset '{resourceName}' is missing from the assembly.");
        using var destStream = File.Create(dest);
        source.CopyTo(destStream);
    }

    private List<string> EnumerateSourceFiles() =>
        Directory.Exists(_options.SourceRoot)
            ? Directory.EnumerateFiles(_options.SourceRoot, "*.md", SearchOption.AllDirectories)
                .Where(p => !IsIgnored(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    private List<string> EnumerateAdrFiles() =>
        Directory.Exists(_options.AdrSourceRoot)
            ? Directory.EnumerateFiles(_options.AdrSourceRoot, "*.md", SearchOption.TopDirectoryOnly)
                .Where(p => !IsIgnored(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    /// <summary>Builds the site nav, folding in whether any ADRs exist (they live outside the _bmad-output
    /// file list the nav is otherwise derived from). Also detects the active BMad module so the nav shows
    /// that module's planning docs and the templaters emit that module's workflow commands.</summary>
    private SiteNav BuildNav(IReadOnlyList<string> sourceRelatives)
    {
        _module = ModuleContext.Detect(_options.RepoRoot, sourceRelatives);
        return SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable);
    }

    private bool AdrsExist() => EnumerateAdrFiles().Any(f => ParseAdrNumber(Path.GetFileName(f)) is not null);

    private static int? ParseAdrNumber(string fileName)
    {
        var m = AdrNumberPattern.Match(fileName);
        return m.Success && int.TryParse(m.Groups["num"].Value, out var n) ? n : null;
    }

    /// <summary>Pulls the "**Status:** …" line out of an ADR body and flattens any markdown link in it to plain
    /// text (e.g. "Superseded by [0002](0002-x.md)" → "Superseded by 0002"), for the index card.</summary>
    private static string? ExtractAdrStatus(string raw)
    {
        var m = AdrStatusPattern.Match(raw);
        if (!m.Success) return null;
        var status = MarkdownLinkPattern.Replace(m.Groups["status"].Value, "${text}").Trim();
        return status.Length == 0 ? null : status;
    }

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

    /// <summary>Resolves a spec-kernel doc's frontmatter <c>companions:</c>/<c>sources:</c> references to real
    /// generated pages, for the "Companion documents" cross-link block. Only docs under <c>specs/</c> carry
    /// these; every other doc yields an empty list (no block). Each reference is resolved relative to the spec
    /// doc's own directory; it becomes a link ONLY when the target file exists on disk, sits inside
    /// <see cref="ForgeOptions.SourceRoot"/>, and isn't an ignored file (so a listed-but-missing companion, or
    /// an ignored <c>.memlog.md</c> that never generates a page, is silently omitted rather than emitting a
    /// broken link — AC #2 / NFR2). Resolution is by file existence, not <see cref="_docs"/> membership, so it
    /// is order-independent during the full-rebuild pass. [Story 2.2 Task 4]</summary>
    private IReadOnlyList<(string Label, string Href)> ResolveSpecCompanions(DocModel doc)
    {
        var sourceRel = PathUtil.NormalizeSlashes(doc.SourceRelativePath);
        if (!sourceRel.StartsWith("specs/", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<(string, string)>();
        }

        var references = doc.Frontmatter.Companions.Concat(doc.Frontmatter.Sources).ToList();
        if (references.Count == 0)
        {
            return Array.Empty<(string, string)>();
        }

        var prefix = PathUtil.RelativePrefix(doc.OutputRelativePath);
        var sourceRootFull = Path.GetFullPath(_options.SourceRoot);
        var sourceDir = Path.GetDirectoryName(Path.Combine(sourceRootFull, sourceRel.Replace('/', Path.DirectorySeparatorChar))) ?? sourceRootFull;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<(string Label, string Href)>();
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference)) continue;

            var candidateFull = Path.GetFullPath(Path.Combine(sourceDir, reference.Replace('/', Path.DirectorySeparatorChar)));

            // Inside SourceRoot, a markdown file that actually gets a generated page, and not ignored.
            // Otherwise omit — never a broken link (only *.md sources are converted into pages).
            if (!candidateFull.StartsWith(sourceRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(Path.GetExtension(candidateFull), ".md", StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(candidateFull) || IsIgnored(candidateFull)) continue;

            var candidateRel = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.SourceRoot, candidateFull));
            // Don't cross-link a doc to itself, and list each target once even if named as both companion and source.
            if (string.Equals(candidateRel, sourceRel, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(candidateRel)) continue;

            var href = prefix + PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(candidateRel));
            resolved.Add((PrettyLabel(candidateRel), href));
        }

        return resolved;
    }

    // Known acronym filename tokens preserved verbatim by PrettyLabel. A blanket "all-caps token" rule also
    // catches full English words authored in all-caps (e.g. EXPERIENCE.md, DESIGN.md), producing shouty labels —
    // so only this explicit allowlist is exempted from title-casing. [Story 2.2 review]
    private static readonly HashSet<string> AcronymLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRD", "SPEC", "UX", "API", "FR", "NFR", "AC", "TOC",
    };

    /// <summary>A human, title-cased label for a related-doc cross-link, derived from its filename
    /// (e.g. <c>requirements-catalog.md</c> → "Requirements Catalog"). Kept order-independent — it never reads
    /// the target's own title, which may not have been generated yet during the rebuild pass. [Story 2.2 Task 4]</summary>
    private static string PrettyLabel(string sourceRelativePath)
    {
        var ti = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        var words = Path.GetFileNameWithoutExtension(sourceRelativePath)
            .Split('-', '_', ' ')
            .Where(w => w.Length > 0)
            // Preserve only known acronym tokens (PRD, SPEC, ...) as-is; title-case everything else, including
            // other all-caps filenames (EXPERIENCE.md → "Experience", not "EXPERIENCE").
            .Select(w => AcronymLabels.Contains(w) ? w.ToUpperInvariant() : ti.ToTitleCase(w.ToLowerInvariant()));
        return string.Join(" ", words);
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
