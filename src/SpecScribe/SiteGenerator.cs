using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    private ArtifactCoverage _coverage = ArtifactCoverage.Empty;

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
                    nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), hasReadme: false, hasSprint: SprintAvailable, hasStructure: sourceRelatives.Count > 0);
                    _nav = nav;
                }
            }

            var epicsSourceFile = FindEpicsSourceFile(files);
            var artifactMap = BuildArtifactMap(files);
            var consumedArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Retrospective notes (epic-N-retro-*.md) are a first-class artifact class. Parse them FIRST (parse
            // needs no epics model), so the epic/story pages below can cross-link to their epic's retro via
            // EpicRetroMap; consume them so the generic pages loop doesn't also render them. Their dedicated
            // pages are written after the epics phase (RenderRetroPages needs the epics model). [Story 2.3 retro pages]
            var retroFiles = files.Where(RetroParser.IsRetroFile).ToList();
            foreach (var rf in retroFiles) consumedArtifacts.Add(ToSourceRelative(rf));
            ParseRetros(retroFiles);

            if (epicsSourceFile is not null)
            {
                reporter?.BeginPhase(GenerationPhase.Epics);
                events.AddRange(GenerateEpicsInternal(epicsSourceFile, files, artifactMap, consumedArtifacts, nav));
                reporter?.EndPhase(GenerationPhase.Epics);
            }

            RenderRetroPages(nav);

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

            // Opt-in deep-git analytics page (hotspots + change-coupling graph). Generated only when --deep-git
            // produced data (DeepGit is only non-null when the flag gated the deep pass on); the dashboard's Git
            // Pulse panel links here. Non-fatal: a null DeepGit simply means no page and no link. [Story 3.2]
            if (_progress?.DeepGit is { } deepPulse)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var html = DeepAnalyticsTemplater.RenderPage(deepPulse, nav);
                    File.WriteAllText(
                        Path.Combine(_options.OutputRoot, SiteNav.DeepAnalyticsOutputPath),
                        ApplyReferenceLinks(html, SiteNav.DeepAnalyticsOutputPath));
                    events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.DeepAnalyticsOutputPath, sw.Elapsed));
                }
                catch (Exception ex)
                {
                    events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.DeepAnalyticsOutputPath, sw.Elapsed, ex.Message));
                    // The page was never written — clear DeepGit so the dashboard's "View Deep Analytics" link
                    // (gated on _progress.DeepGit is not null) doesn't point at a page that doesn't exist.
                    _progress!.DeepGit = null;
                }
            }

            if (readmeEvent is not null)
            {
                events.Add(readmeEvent);
            }

            // Artifact-family coverage insight — computed here, AFTER epics/pages have run, so ResolveFamilyHref
            // can check the now-fully-populated _docs/_epicsModel/_requirements before linking a "present"
            // family to a page (never a broken link — same ordering WriteStructure relies on for _docs below).
            // Cached so every WriteIndex call shares one instance. Never-throw: any failure degrades to Empty,
            // the panel omits, and generation still succeeds (AD-4 / NFR2). [Story 3.3 Task 2; review: reordered]
            _coverage = BuildArtifactCoverage(sourceRelatives);

            // The sprint page reads the epics model (for real story/epic titles + links), so it's written
            // after the epics phase. Gated on parsed sprint data; a no-op when there is none. [Story 2.3 Task 3/5]
            WriteSprint(nav);
            // The structure tree reads the fully-populated _docs (source→output hrefs), so it's written after the
            // pages phase — like WriteSprint, gated on the same source-artifact signal as its nav item. [Story 3.4]
            WriteStructure(nav, sourceRelatives);
            WriteRetroIndex(nav);
            // Built once and shared with WriteIndex below — both used to rebuild it independently. [Story 2.3 review]
            var workInventory = WorkInventory.Build(_docs.Values.ToList());
            WriteActionItems(nav, workInventory);

            reporter?.BeginPhase(GenerationPhase.Index);
            WriteIndex(nav, workInventory);
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
            RefreshCoverage();
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
                RefreshCoverage();
                WriteIndex(_nav ?? BuildNav(Array.Empty<string>()));
                return new GenerationEvent(GenerationOutcome.Removed, relative, sw.Elapsed);
            }

            return new GenerationEvent(GenerationOutcome.Skipped, relative, sw.Elapsed, "not tracked");
        }
    }

    /// <summary>Recomputes and re-caches <see cref="_coverage"/> from the current on-disk source tree. Called
    /// by every watch-mode incremental path (<see cref="GenerateOne"/>, <see cref="RemoveFor"/>,
    /// <see cref="RegenerateEpics"/>, <see cref="RegenerateAdrs"/>) right before their own <c>WriteIndex</c>
    /// call, so a live edit (new/changed/removed planning artifact) is reflected in the Planning Artifacts
    /// panel immediately rather than only after the next full <c>generate</c>. [Story 3.3 review]</summary>
    private void RefreshCoverage()
    {
        _coverage = BuildArtifactCoverage(EnumerateSourceFiles().Select(ToSourceRelative).ToList());
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
                RefreshCoverage();
                WriteIndex(nav);
                return new GenerationEvent(GenerationOutcome.Skipped, "epics.md", sw.Elapsed, "epics.md not found");
            }

            var artifactMap = BuildArtifactMap(files);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var epicsEvents = GenerateEpicsInternal(epicsSourceFile, files, artifactMap, consumed, nav);
            RefreshCoverage();
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
            RefreshCoverage();
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
            // Deep git analytics are strictly opt-in: when the flag is off this ternary short-circuits so
            // TryComputeDeep — and its extra git process — never runs, and baseline generation timing cannot
            // regress. That gate IS the FR-10 performance guarantee (AC #1). [Story 3.2]
            var deepGit = _options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(_options.RepoRoot) : null;
            var progress = ProgressCalculator.Compute(model, artifactMap, gitPulse, deepGit);
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
                var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyReferenceLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath), $"epics/epic-{epic.Number}.html", skipEpicNumber: epic.Number));

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null)
                    {
                        // Undrafted story: emit a placeholder page at the exact path its real page will
                        // use, so "Story N.M" mentions always have a live target and a later-drafted
                        // artifact overwrites it in place. ArtifactOutputPath stays null — placeholders
                        // must never count as detailed stories anywhere progress is computed.
                        var placeholderPath = StoryEpicLinkifier.StoryPagePath(story.Id);
                        var placeholderHtml = EpicsTemplater.RenderStoryPlaceholder(epic, story, nav, _module.Commands, epicRetroPath);
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
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, artifactRelative, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord, tasks, reviewFindingsHtml, changeLogHtml, nav, _module.Commands, epicRetroPath);
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

    private void WriteIndex(SiteNav nav, WorkInventory? work = null)
    {
        var indexPath = Path.Combine(_options.OutputRoot, "index.html");
        var docs = _docs.Values.ToList();
        var inventory = work ?? WorkInventory.Build(docs);
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands, inventory, _sprint, _retros, _coverage);
        File.WriteAllText(indexPath, ApplyReferenceLinks(html, "index.html"));
    }

    /// <summary>Parses the retrospective notes into <see cref="_retros"/> (ordered by epic, then filename) so
    /// <see cref="EpicRetroMap"/> is available to the epic/story pages and the sprint/home surfaces. Parse only —
    /// the dedicated pages are written later by <see cref="RenderRetroPages"/>. [Story 2.3 retro pages]</summary>
    private void ParseRetros(IReadOnlyList<string> retroFiles)
    {
        var retros = new List<RetroModel>();
        foreach (var file in retroFiles)
        {
            var sourceRel = ToSourceRelative(file);
            var outputRel = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(sourceRel));
            retros.Add(RetroParser.Parse(file, sourceRel, outputRel));
        }
        _retros = retros.OrderBy(r => r.EpicNumber).ThenBy(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        // Computed once here rather than on every access (EpicRetroMap used to be a computed property
        // re-grouping all retros on every epic in the GenerateEpicsInternal loop). [Story 2.3 review]
        _epicRetroMap = _retros.GroupBy(r => r.EpicNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).First().OutputRelativePath);
    }

    /// <summary>Writes each parsed retrospective into its dedicated <see cref="RetroTemplater"/> page (at the
    /// same <c>implementation-artifacts/…html</c> path the generic pipeline would have used, so existing links
    /// resolve), reference-linkified like every page. Runs after the epics phase — the page needs the epics
    /// model for its epic link and "Stories in this Epic" section. [Story 2.3 retro pages]</summary>
    private void RenderRetroPages(SiteNav nav)
    {
        foreach (var retro in _retros)
        {
            var outputRel = retro.OutputRelativePath;
            var outputFull = Path.Combine(_options.OutputRoot, outputRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputFull)!);
            File.WriteAllText(outputFull, ApplyReferenceLinks(RetroTemplater.RenderPage(retro, _epicsModel, nav), outputRel));
        }
    }

    /// <summary>Maps an epic number to the output path of its (latest, by filename) retrospective page — the
    /// link target for an open action item tagged with that epic. Computed once in <see cref="ParseRetros"/>.
    /// [Story 2.3 retro pages]</summary>
    private IReadOnlyDictionary<int, string> _epicRetroMap = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, string> EpicRetroMap => _epicRetroMap;

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

    /// <summary>Writes <c>structure.html</c> — the interactive project/artifact structure tree. Gated on the
    /// same source-artifact signal as its nav item (<c>sourceRelatives.Count &gt; 0</c>) so the page and its link
    /// are one signal; a project with no <c>_bmad-output</c> <c>*.md</c> files omits both. Builds the
    /// output-href map from the fully-populated <see cref="_docs"/> (plus the special <c>epics.md → epics.html</c>
    /// mapping) so navigable leaves route to real pages and non-navigable ones render as plain text — never a
    /// broken link. The whole gather+build is wrapped never-throw → <see cref="ProjectTree.Empty"/> so any failure
    /// degrades to omission and generation still succeeds (AD-4 / NFR2). [Story 3.4 Task 3]</summary>
    private void WriteStructure(SiteNav nav, IReadOnlyList<string> sourceRelatives)
    {
        if (sourceRelatives.Count == 0) return;

        ProjectTree tree;
        try
        {
            tree = ProjectTree.Build(sourceRelatives, BuildStructureHrefMap(sourceRelatives));
        }
        catch (Exception)
        {
            tree = ProjectTree.Empty;
        }

        if (tree.IsEmpty) return;

        var html = RenderStructurePage(tree, nav);
        File.WriteAllText(
            Path.Combine(_options.OutputRoot, SiteNav.StructureOutputPath),
            ApplyReferenceLinks(html, SiteNav.StructureOutputPath));
    }

    /// <summary>Maps each source-artifact path that has a generated page to its output-relative URL, from the
    /// already-populated <see cref="_docs"/> (source→output) plus <c>epics.md → epics.html</c> (which renders
    /// specially and is never a generic <see cref="_docs"/> entry). Consumed story artifacts and any file with no
    /// generated page are simply absent, so <see cref="ProjectTree"/> renders them as plain text. [Story 3.4]</summary>
    private IReadOnlyDictionary<string, string> BuildStructureHrefMap(IReadOnlyList<string> sourceRelatives)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in _docs.Values)
        {
            map[PathUtil.NormalizeSlashes(doc.SourceRelativePath)] = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
        }

        // epics.md is consumed into epics.html, so it never appears in _docs — add it only when the epics page
        // was actually produced (epics.md parsed into a model).
        if (_epicsModel is not null)
        {
            var epicsSource = sourceRelatives.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), "epics.md", StringComparison.OrdinalIgnoreCase));
            if (epicsSource is not null)
            {
                map[PathUtil.NormalizeSlashes(epicsSource)] = SiteNav.EpicsOutputPath;
            }
        }

        return map;
    }

    /// <summary>Assembles the standalone <c>structure.html</c> shell — the same <see cref="PathUtil.RenderHeadOpen"/>
    /// + nav + breadcrumb + <c>&lt;main id="main-content"&gt;</c> + footer every other <c>Write*</c> page uses —
    /// with the tree living in a <c>chart-panel</c>. The tree markup itself comes from the pure, no-JS
    /// <see cref="Charts.ProjectStructureTree"/>. [Story 3.4 Subtask 3.3]</summary>
    private string RenderStructurePage(ProjectTree tree, SiteNav nav)
    {
        var outputPath = SiteNav.StructureOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — structure.html is at the output root.

        var fileWord = Charts.Plural(tree.FileCount, "file", "files");
        var dirWord = Charts.Plural(tree.DirectoryCount, "directory", "directories");
        var headline = $"{tree.FileCount} {fileWord} across {tree.DirectoryCount} {dirWord}";

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Project Structure — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Interactive tree of the project and artifact structure for {nav.SiteTitle} — expand and collapse directories and jump to the generated pages."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Structure", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Project Structure</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {PathUtil.Html(headline)}</p>\n\n");
        sb.Append("<section class=\"chart-panel\">\n");
        sb.Append("  <h3>Project &amp; Artifact Structure</h3>\n");
        sb.Append(Charts.ProjectStructureTree(tree));
        sb.Append("</section>\n\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
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
    private void WriteActionItems(SiteNav nav, WorkInventory? work = null)
    {
        var open = _sprint?.OpenActionItems;
        if (open is null || open.Count == 0) return;
        // Debt-related items link to the deferred-work backlog page when one exists (root-relative — this page
        // is at the site root). Reuses the caller's inventory when supplied instead of rebuilding it. [Story 2.3 review]
        var deferredHref = (work ?? WorkInventory.Build(_docs.Values.ToList())).Deferred?.OutputPath;
        // NOT reference-linkified: the "Resolve with AI" data-copy payload embeds the action text (which can
        // contain "Epic N"/"Story N.M" mentions); the linkifier would wrap those in <a> tags INSIDE the
        // attribute value and corrupt the copyable command. [Story 2.3 polish #5]
        var html = ActionItemsTemplater.RenderPage(open, EpicRetroMap, _module.Commands, nav, deferredHref);
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
        // StructureAvailable = the source-artifact file set is non-empty — the SINGLE signal that gates both the
        // nav item/quick link here and the WriteStructure page write, so a Structure link is never emitted to a
        // page that wasn't produced. [Story 3.4 Subtask 3.4]
        return SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable, hasStructure: sourceRelatives.Count > 0);
    }

    private static readonly IReadOnlyDictionary<string, DateOnly> EmptyDates = new Dictionary<string, DateOnly>();

    // The memlog frontmatter's single "updated: <date>" field — a one-line regex read (like ForgeOptions'
    // project_name read), NOT a full YAML parse. Captures just the yyyy-MM-dd prefix of the timestamp.
    private static readonly Regex MemlogUpdatedPattern = new(
        @"^\s*updated:\s*(?<date>\d{4}-\d{2}-\d{2})", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Gathers the inputs for and builds the cached <see cref="ArtifactCoverage"/> insight. IO lives
    /// here — source-file last-write-times (the primary freshness signal) and memlog discovery (the secondary
    /// enrichment); the pure <see cref="ArtifactCoverage.Build"/> owns the coverage/freshness/staleness rules.
    /// Never throws: any failure degrades to <see cref="ArtifactCoverage.Empty"/> so the panel omits and
    /// baseline generation still succeeds (AD-4: insight providers never own baseline success; NFR2). [Story 3.3]</summary>
    private ArtifactCoverage BuildArtifactCoverage(IReadOnlyList<string> sourceRelatives)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            // First pass with empty maps discovers which canonical families are present and their matched
            // source paths, so we stat ONLY those files (not every markdown doc). All family-matching logic
            // stays in ArtifactCoverage — the single coverage seam Epic 4 generalizes.
            var discovered = ArtifactCoverage.Build(sourceRelatives, EmptyDates, EmptyDates, today);

            // Stat every candidate matching ANY family (not just each family's provisional first match) so an
            // OR-predicate family (UX: DESIGN.md or EXPERIENCE.md) has both mtimes available when Build picks
            // the freshest one as canonical below. [Story 3.3 review]
            var mtimes = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in ArtifactCoverage.AllCandidatePaths(sourceRelatives))
            {
                try
                {
                    var full = Path.Combine(_options.SourceRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    mtimes[rel] = DateOnly.FromDateTime(File.GetLastWriteTime(full));
                }
                catch
                {
                    // A single unreadable file degrades that one family's freshness to null — never aborts
                    // the pass (partial data beats none, AD-4).
                }
            }

            var memlogByFamily = BuildMemlogMap(discovered);

            var coverage = ArtifactCoverage.Build(sourceRelatives, mtimes, memlogByFamily, today);

            // Enrich each family with presentation data the pure Build can't know: the page a PRESENT family
            // links to, and the create command a MISSING family surfaces for the detected module. Resolved
            // here because both depend on page routing and the module — never on the source-derived truth.
            var enriched = coverage.Families
                .Select(f => f with
                {
                    Href = f.Present ? ResolveFamilyHref(f) : null,
                    CreateCommand = !f.Present && ArtifactCoverage.CreateStepKeys.TryGetValue(f.Label, out var step)
                        ? _module.Commands.Command(step)
                        : null,
                })
                .ToList();

            return new ArtifactCoverage { Families = enriched };
        }
        catch (Exception)
        {
            return ArtifactCoverage.Empty;
        }
    }

    /// <summary>The page a present family's card links to. Epics/Stories point at the epics-and-stories view and
    /// Requirements at the curated FR/NFR page (both special-routed, not generic source pages); every other
    /// family links to its generated page (<c>ToOutputRelative</c> of its matched source, which the generic
    /// page pipeline writes verbatim). Labels mirror <see cref="ArtifactCoverage"/>'s canonical family set.
    /// Only returns an href when the target page was actually produced — <c>_docs</c>/<c>_epicsModel</c>/
    /// <c>_requirements</c> are populated exclusively on a successful page write, so a source-file conversion
    /// failure (or an epics-parse failure) degrades the card to non-linked "present" text instead of a broken
    /// link. Mirrors the same <c>_docs</c>-gated guard <see cref="BuildStructureHrefMap"/> already applies to
    /// the structure tree. [Story 3.3 review]</summary>
    private string? ResolveFamilyHref(ArtifactFamily f)
    {
        if (f.SourcePath is not { } src) return null;
        return f.Label switch
        {
            "Epics" or "Stories" => _epicsModel is not null ? SiteNav.EpicsOutputPath : null,
            "Requirements" => _requirements is not null ? SiteNav.RequirementsOutputPath : null,
            // _docs keys are raw Path.GetRelativePath (OS-native separators); f.SourcePath is normalized to
            // forward slashes, so both sides must be normalized before comparing (same reasoning
            // BuildStructureHrefMap already applies). [Story 3.3 review]
            _ => _docs.Keys.Any(k => string.Equals(PathUtil.NormalizeSlashes(k), src, StringComparison.OrdinalIgnoreCase))
                ? PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(src))
                : null,
        };
    }

    /// <summary>Discovers <c>.memlog.md</c> journals (dotfiles, so excluded from the <c>*.md</c> source
    /// enumeration — scanned separately like <see cref="SprintSourcePath"/>), reads each one's
    /// <c>updated:</c> date, and associates it with the family whose canonical file sits in the closest
    /// ancestor directory (longest matching memlog dir wins). Strictly additive: no memlogs → an empty map, so
    /// every family's primary Present/LastModified value is unchanged (AC #2). [Story 3.3 Subtask 2.3]</summary>
    private IReadOnlyDictionary<string, DateOnly> BuildMemlogMap(ArtifactCoverage discovered)
    {
        var result = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_options.SourceRoot)) return result;

        var memlogs = new List<(string Dir, DateOnly Updated)>();
        foreach (var full in Directory.EnumerateFiles(_options.SourceRoot, ".memlog.md", SearchOption.AllDirectories))
        {
            DateOnly updated;
            try
            {
                var text = MarkdownConverter.ReadAllTextShared(full);
                var m = MemlogUpdatedPattern.Match(text);
                if (!m.Success || !DateOnly.TryParseExact(
                        m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out updated))
                {
                    continue; // no parseable updated: date → this memlog adds no enrichment
                }
            }
            catch
            {
                continue;
            }

            var dir = PathUtil.NormalizeSlashes(Path.GetDirectoryName(ToSourceRelative(full)) ?? string.Empty);
            memlogs.Add((dir, updated));
        }

        if (memlogs.Count == 0) return result; // strictly additive: the no-memlog primary picture is unchanged

        // A root-level memlog (Dir.Length == 0) only stands in as every family's fallback when it's the ONLY
        // memlog in the tree — there, it genuinely is the project's one decision journal. Once any nested,
        // family-scoped memlog exists, the root one no longer blanket-applies to families with no closer
        // match, so an unrelated project-root journal can't be misattributed as a specific family's own
        // enrichment. [Story 3.3 review]
        var hasScopedMemlog = memlogs.Any(ml => ml.Dir.Length > 0);

        foreach (var family in discovered.Families)
        {
            if (family.SourcePath is not { } rel) continue;
            var best = memlogs
                .Where(ml => ml.Dir.Length == 0
                    ? !hasScopedMemlog
                    : rel.StartsWith(ml.Dir + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(ml => ml.Dir.Length)
                .Select(ml => (DateOnly?)ml.Updated)
                .FirstOrDefault();
            if (best is { } d) result[family.Label] = d;
        }

        return result;
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
