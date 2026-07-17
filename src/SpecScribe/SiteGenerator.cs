using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

public enum GenerationOutcome { Generated, Updated, Removed, Skipped, Error }

/// <param name="FromAdapterDiagnostic">True only for events produced by <see cref="SiteGenerator.MapDiagnostics"/>
/// — the provenance <see cref="DiagnosticNotice.FromEvents"/> checks before trusting a leading
/// <c>[Category]</c> token in <paramref name="Message"/> as a real category tag, rather than sniffing arbitrary
/// message text (which could coincidentally start with the same bracket shape). [Review][Patch]</param>
/// <param name="FromAdrDiagnostic">True only for the subset of <paramref name="FromAdapterDiagnostic"/> events
/// whose <see cref="RelativePath"/> is relative to the ADR output subdir / <c>AdrSourceRoot</c> (the
/// unnumbered-ADR notice), rather than the source root — the "which root do I anchor to" bit
/// <see cref="DiagnosticNotice"/> needs so the <c>webview</c> command's Problems-panel channel resolves the real
/// file instead of combining an ADR-relative path with <c>SourceRoot</c>. [Story 6.12] [Review][Patch]</param>
public sealed record GenerationEvent(GenerationOutcome Outcome, string RelativePath, TimeSpan Elapsed, string? Message = null, bool FromAdapterDiagnostic = false, bool FromAdrDiagnostic = false);

/// <summary>Owns the mapping from _bmad-output/*.md to SpecScribeOutput/*.html, and keeps the generated index,
/// nav, and epics/story pages in sync.</summary>
public sealed class SiteGenerator
{
    // ADR numbers derive from several mainstream filename schemes — 0001-x, ADR-0001-x, adr-1-x, adr_001_x,
    // 1-x: an optional adr token, separators, then the first integer. A filename with no derivable number
    // still renders (sorted last) rather than dropping. [Story 4.2 Task 2]
    private static readonly Regex AdrNumberPattern = new(@"^(?:adr)?[-_\s]*(?<num>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AdrStatusPattern = new(@"^\*\*Status:\*\*\s*(?<status>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    // The MADR-style "## Status" (or "### Status") section heading whose next non-blank line carries the
    // status value — the second of the three tolerated status shapes. [Story 4.2 Task 2]
    private static readonly Regex AdrStatusHeadingPattern = new(@"^#{2,3}\s+Status\s*$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarkdownLinkPattern = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);

    // ADR date + summary extraction (Story 10.4), mirroring the tolerant status shapes: a "**Date:**" bold line
    // (the shape all six real ADRs use), a "## Date" MADR heading fallback, and the "## Context" section whose
    // first prose paragraph becomes the one-line summary (the most prevalent shape across the real ADRs).
    private static readonly Regex AdrDatePattern = new(@"^\*\*Date:\*\*\s*(?<date>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex AdrDateHeadingPattern = new(@"^#{2,3}\s+Date\s*$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AdrContextHeadingPattern = new(@"^#{2,3}\s+Context\s*$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ForgeOptions _options;
    private readonly Dictionary<string, DocModel> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    // The ingestion seam (AD-1): all framework-specific discovery/parsing lives behind this adapter, so the
    // generator only ever consumes the normalized ArtifactBundle. Held as the concrete type (not
    // IArtifactAdapter) because the watch paths also need its scoped epics re-ingest; the adapter registry
    // that selects among IArtifactAdapter implementations arrives with Stories 4.3+. [Story 4.1]
    private readonly BmadArtifactAdapter _adapter = new();

    private SiteNav? _nav;
    private ModuleContext _module = ModuleContext.None;
    private EpicsModel? _epicsModel;
    private ProgressModel? _progress;
    private RequirementsModel? _requirements;
    private List<AdrEntry> _adrs = new();
    private List<CommitDayEntry> _commitDays = new();

    // Story 7.5: full %H commit hash -> per-commit detail page output-relative path (commit/{shortHash}.html).
    // Populated by GenerateCommitDetailsInternal (only under --deep-git); the CommitHref resolver matches keys
    // exactly, then by prefix, so both the hub's full-%H link and the day page's abbreviated-%h link resolve.
    // Empty (never null) when the flag is off / git is absent → the day-page + hub hashes stay plain (guarded).
    // The shared seam Stories 7.3 (date pages) / 7.4 (change history) consume once landed.
    private List<CommitDetailEntry> _commitDetails = new();
    private Dictionary<string, string> _commitPages = new(StringComparer.Ordinal);

    // Story 7.3: the chronological activity timeline output path once written (else null), so WriteIndex / the
    // dashboard Git Pulse panel render the "View activity timeline →" link ONLY when the page exists — the same
    // cache-and-guard pattern _progress.DeepGit(.Insights) uses for the deep-analytics / git-insights links.
    private string? _timelinePath;

    // Story 7.1: repo-relative source path (forward slashes) -> code-page output-relative path (code/<path>.html).
    // Populated by GenerateCodePagesInternal, cached the same way _adrs/_commitDays are so Story 7.2 can reuse it
    // to linkify source citations without re-running discovery. Empty in external mode (--code-url) and when no
    // referenced source files exist.
    private Dictionary<string, string> _codePages = new(StringComparer.OrdinalIgnoreCase);

    // Story 7.2: discovered ONCE up front (DiscoverCodeReferences), before any citing page is linkified, so
    // ApplyReferenceLinks can resolve citations against a populated _codePages on every page — including the
    // story/doc/ADR pages that render BEFORE the code pages themselves. _codeReferenced is the deterministic set
    // of repo-relative files that get an in-portal page (empty in external mode); _codeReverseMap is the
    // file -> citing artifacts back-map that drives each code page's "Referenced by" block (AC #2). Keyed on the
    // citing artifact's SOURCE-relative path (resolved to its output URL at render time, once _referenceMap exists).
    private List<string> _codeReferenced = new();
    private Dictionary<string, List<(string CitingSourceRelative, string Title)>> _codeReverseMap = new(StringComparer.OrdinalIgnoreCase);

    // Forward map (citing artifact SOURCE-relative path -> the set of repo-relative code files it cites), built in
    // the SAME discovery pass as _codeReverseMap (no second scan) — lets the "Show relationships" reference-graph
    // toggle answer "does this citing story ALSO cite one of the center file's related files?" without re-reading
    // any artifact. [reference-graph epic grouping + relationships]
    private Dictionary<string, HashSet<string>> _citerToFiles = new(StringComparer.OrdinalIgnoreCase);

    // Story page path (output-relative, normalized) -> owning epic (number + title), resolved ONCE from
    // _epicsModel right before code pages render (BuildStoryEpicLookup) — the reference graph's "Group by epic"
    // toggle joins each citing story's OutputUrl against this map; a miss (ADR/doc citer, or a story with no
    // resolvable page) simply means "no epic", never a throw. [reference-graph epic grouping + relationships]
    private Dictionary<string, (int Number, string Title)>? _storyEpicByOutputPath;

    // Story 7.6: the source-code walk (repo-relative path + line count), enumerated ONCE per full generation
    // (EnumerateCodeFiles — git ls-files with a bounded fallback) and cached so both the nav gate (built early)
    // and WriteCodeMap (written after the pages phase) read the same set. Empty when no readable source files were
    // found → the "Code Map" nav item, quick link, and page all omit together (one signal, never a broken link).
    private IReadOnlyList<(string RepoRelativePath, long Lines)> _codeFiles = Array.Empty<(string, long)>();

    private SprintStatus? _sprint;
    // Story 8.3: portal-wide count ledger — built once after progress/sprint/workInventory are known and
    // threaded into every summary surface so no render site recounts. Null until the index/work phase.
    private ProjectCounts? _counts;
    private List<RetroModel> _retros = new();
    private ArtifactCoverage _coverage = ArtifactCoverage.Empty;

    // When --spa is active OR CapturePages is set, every long-tail page's finished HTML is captured here as it is
    // written (output path → full page string) so the SPA bundle / webview bundle can slice its content region from
    // the render pipeline's OWN output rather than re-reading the generated site off disk (AD-1/AD-2). Null on a
    // normal run — no capture, no overhead, and the static output stays byte-identical (AC #3/#5). The
    // dashboard/epics families are NOT captured here; the SPA and webview re-render them from their view models
    // (RenderSpaBundle / RenderWebviewSurfaces) for strongest parity. [Story 6.7; spec-webview-doc-page-surfaces]
    private Dictionary<string, string>? _spaCapture;

    /// <summary>Opt-in page capture WITHOUT the SPA delivery outputs: when true, <see cref="GenerateAll"/> fills the
    /// same write-seam capture <c>--spa</c> uses, and <see cref="RenderWebviewSurfaces"/> turns every captured
    /// long-tail page (docs, ADRs, requirements, sprint, retros…) into a navigable webview surface so the panel's
    /// header nav links work in-editor. Set BEFORE <see cref="GenerateAll"/> (the webview command does). Memory-only:
    /// written bytes are identical either way (the golden gate). [spec-webview-doc-page-surfaces]</summary>
    public bool CapturePages { get; set; }

    public SiteGenerator(ForgeOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<GenerationEvent> GenerateAll(IGenerationReporter? reporter = null)
    {
        reporter?.BeginPhase(GenerationPhase.Scan);
        var files = EnumerateSourceFiles();
        var sourceRelatives = files.Select(ToSourceRelative).ToList();
        // Story 7.6: the source-code walk gates the Code Map nav item/page. Enumerated once here (before the nav is
        // built) and cached so WriteCodeMap reuses the exact same set — never a broken "Code Map" link.
        _codeFiles = EnumerateCodeFiles();
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

            // Fresh capture buffer for this full build when the opt-in SPA form is on OR the webview asked for
            // page capture (null otherwise → no capture). Capture is memory-only either way.
            _spaCapture = _options.EmitSpa || CapturePages
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : null;

            // All planning-artifact ingestion (sprint, module detection, retros, epics → requirements) runs
            // through the framework adapter, which returns one normalized bundle + non-fatal diagnostics.
            // Progress/git enrichment stays HERE (projection path, AD-4) and is handed in as a callback so the
            // adapter can keep BMad's epics-before-requirements ordering without owning git. [Story 4.1]
            ProgressModel? progress = null;
            var bundle = _adapter.Ingest(_options, files, (model, artifactsById) => progress = ComputeProgress(model, artifactsById));

            // Sprint presence drives the nav gate, and the sprint page + home widget read this same parsed
            // instance. Missing/malformed → null → the page, widget, and nav item all omit cleanly. [Story 2.3]
            _sprint = bundle.Sprint;
            _module = bundle.Module;
            SetRetros(bundle.Retros);

            var nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable, hasCodeMap: _codeFiles.Count > 0);
            _nav = nav;

            // Render the README up front so that, if it fails, we can drop the Readme nav entry before any
            // other page is written — the site never links to a readme.html that wasn't actually produced.
            GenerationEvent? readmeEvent = null;
            if (ReadmeAvailable)
            {
                readmeEvent = GenerateReadmeInternal(nav);
                if (readmeEvent is { Outcome: GenerationOutcome.Error })
                {
                    nav = SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), hasReadme: false, hasSprint: SprintAvailable, hasCodeMap: _codeFiles.Count > 0);
                    _nav = nav;
                }
            }

            // Files the adapter consumed into dedicated surfaces (story artifacts, retro notes) — the generic
            // pages loop below must not also render them.
            var epicsSourceFile = bundle.EpicsSourceFullPath;
            var consumedArtifacts = new HashSet<string>(bundle.ConsumedSourceRelatives, StringComparer.OrdinalIgnoreCase);

            // Cache the ingested models with the same granularity the inline parse chain had: epics+progress
            // only once the projection enrichment ran to completion, requirements only when parsed — so a
            // mid-chain failure leaves the previous (or no) values in place rather than nulling everything.
            // Deliberately AFTER the README render above: the README has always rendered before the epics
            // parse, so it is linkified against the PREVIOUS run's models (null on a first run). [Story 4.1]
            if (progress is not null)
            {
                _epicsModel = bundle.Epics;
                _progress = progress;
                TagEpicRetrospectives();
            }
            if (bundle.Requirements is not null)
            {
                _requirements = bundle.Requirements;
            }

            // Adapter diagnostics surface on the same event channel per-file failures always used — the typed
            // AdapterDiagnostic is the only report for a failure the adapter caught (never double-reported).
            events.AddRange(MapDiagnostics(bundle.Diagnostics));

            // Unrecognized top-level source folders render coherently (each gets its own home-index band, see
            // HtmlTemplater.RenderIndex) AND are reported as categorized non-fatal structure notices on the
            // same diagnostic channel — the input Story 4.8's diagnostics page will render. [Story 4.2 Task 5]
            events.AddRange(MapDiagnostics(UnrecognizedTopLevelFolders(sourceRelatives)));

            // Story 7.2: discover the referenced code-file set up front — BEFORE the epic/story/doc/ADR pages are
            // rendered — so ApplyReferenceLinks resolves every source citation against a populated _codePages on
            // each of those pages. The code pages themselves are still rendered later (GenerateCodePagesInternal),
            // once _referenceMap exists to route each page's "Referenced by" back-links. Pure/disk-read only; no
            // output written here. In-portal pages are ALWAYS discovered now (Story 7.7 made --code-url additive),
            // so _codePages is populated even with an external base set — the link mode is chosen at render time.
            DiscoverCodeReferences(files);

            // Render the epic/story/requirements pages only when the whole ingest chain produced its models —
            // the exact set of writes the previous single try/catch performed after a successful parse.
            if (epicsSourceFile is not null && bundle is { Epics: { } epicsModel, Requirements: { } requirementsModel } && progress is not null)
            {
                reporter?.BeginPhase(GenerationPhase.Epics);
                events.AddRange(RenderEpicsPages(epicsSourceFile, files, bundle.StoryArtifactsById, epicsModel, requirementsModel, progress, nav));
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

            // Per-commit detail pages (Story 7.5, FR-10). Runs BEFORE the code pages, the per-day pages, and the
            // Git Insights hub so _commitPages is populated when those render their guarded hash links (the code
            // pages' Story 7.4 "Advanced coverage" change-history links resolve against it too). Gated on the same
            // opt-in deep-git data (Commits ride the --deep-git-bounded fetch): flag off / git absent → DeepGit null
            // → Commits empty → no commit/ dir, no pages, and every hash stays plain (AC #2).
            if (_progress?.DeepGit is { Commits.Count: > 0 } deepCommits)
            {
                events.AddRange(GenerateCommitDetailsInternal(deepCommits.Commits, nav));
            }

            // Date pages + activity timeline (Story 7.3). Runs BEFORE the code pages so _commitDays is populated
            // when those render the History tab's date links (mirrors the commit-detail pages' ordering above, for
            // the same reason). Date pages are generated for the UNION of the git commit days and the days any
            // recognized artifact was last edited (filesystem mtime), so an artifact-only day (a doc edited with no
            // commit) still gets a page for the timeline to link to. Both surfaces derive their day set from
            // ActivityModel.UnionDays, so no link can point at a missing page. The commit-detail resolver lights up
            // each day's hash as a link into commit/ when a per-commit page exists (Story 7.5, plain otherwise).
            // Everything degrades non-fatally: no git AND no artifacts → no pages, no timeline, no dashboard link,
            // no error, the rest of the site still generates (AC #2).
            _timelinePath = null;
            var artifactsByDay = BuildArtifactsByDay();
            var gitPulse = _progress?.Git;
            if (gitPulse is not null || artifactsByDay.Count > 0)
            {
                reporter?.BeginPhase(GenerationPhase.CommitDays);
                events.AddRange(GenerateDatePagesInternal(gitPulse, artifactsByDay, nav, CommitHref));
                reporter?.EndPhase(GenerationPhase.CommitDays);

                GenerateTimelineInternal(gitPulse, artifactsByDay, nav, events, reporter);
            }

            // In-portal code file pages for source files referenced by planning/implementation artifacts (Story 7.1,
            // FR15) plus the git-analytics file sets (see DiscoverCodeReferences). Additive: a page class under code/,
            // that Stories 7.2–7.4 build on. Generated regardless of --code-url (that base is now an additive per-page
            // link). When --deep-git produced per-file insights each page gains an opt-in "Advanced coverage" section
            // (Story 7.4); a null insight leaves it baseline.
            events.AddRange(GenerateCodePagesInternal(files, nav, reporter));

            // Opt-in deep-git analytics page (hotspots + change-coupling graph). Generated only when --deep-git
            // produced data (DeepGit is only non-null when the flag gated the deep pass on); the dashboard's Git
            // Pulse panel links here. Non-fatal: a null DeepGit simply means no page and no link. [Story 3.2]
            if (_progress?.DeepGit is { } deepPulse)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var html = DeepAnalyticsTemplater.RenderPage(deepPulse, nav, fileHref: CodeItemHref);
                    WriteOutput(SiteNav.DeepAnalyticsOutputPath, ApplyReferenceLinks(html, SiteNav.DeepAnalyticsOutputPath));
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

            // Aggregate Git Insights hub (file change frequency, activity over time, contributor attribution)
            // — Story 3.8's deep-git surface. Gated on the same opt-in data as the deep-analytics page above:
            // Insights is only non-null when --deep-git gated TryComputeDeep on AND the shared numstat fetch
            // produced data, so with the flag off this block (and every byte of git work behind it) never runs
            // and baseline generation is untouched (AC #2). Runs after the commit-days phase so the reused
            // heatmap's per-day links always have their target pages. The file/commit detail-link resolvers
            // stay unwired until Stories 7.1/7.4/7.5 land their cached page maps — the templater renders those
            // cells as plain text (guard-all-links-on-target-availability). [Story 3.8]
            GenerateGitInsightsInternal(nav, events, reporter);

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

            // Built once and shared with WriteSprint / WriteActionItems / WriteIndex. Lifted above WriteSprint
            // so the sprint page can consume the shared ledger without moving the sprint write later in the
            // phase order (diagnostics event ordering is load-bearing for the golden fingerprint). [Story 2.3 review; Story 8.3]
            var workInventory = WorkInventory.Build(_docs.Values.ToList());
            // Story 8.3: one portal-wide count ledger. Divergence → exactly one Unsupported AdapterDiagnostic
            // (same channel as Story 8.2 / 4.1).
            _counts = ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, workInventory, _epicsModel);
            if (_counts.HasDivergence)
            {
                events.AddRange(MapDiagnostics(new[]
                {
                    new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Unsupported,
                        BmadArtifactAdapter.SprintStatusFileName,
                        _counts.DivergenceMessage()),
                }));
            }
            // Sprint page reads the epics model (titles/links) + the shared ledger (tracked totals). [Story 2.3; 8.3]
            WriteSprint(nav);
            // The source-code treemap reads the cached source-code walk + the (now-populated) deep-git per-file
            // metrics, so it's written after the pages/git phases — like WriteSprint, gated on the same source-code
            // signal as its nav item. Replaced the retired Story 3.4 structure tree. [Story 7.6]
            WriteCodeMap(nav);
            WriteRetroIndex(nav);
            WriteActionItems(nav, workInventory);
            WriteDeferredWork(nav, workInventory);

            reporter?.BeginPhase(GenerationPhase.Index);
            WriteIndex(nav, workInventory);
            reporter?.EndPhase(GenerationPhase.Index);

            // Diagnostics + About are the whole-run reporting surface (Story 4.8): written LAST, after every
            // phase has appended its events, so the diagnostics page reflects the COMPLETE non-fatal notice set.
            // Both are always written on a full run (the diagnostics page's zero-notice case renders an all-clear
            // state, never a gated-away page), so the site-wide footer "About" link — and the About page's link
            // on to the run log — can never 404. Each write's own Generated event is appended AFTER the
            // diagnostics page reads the notice list, so it never self-references. [Story 4.8 Task 6]
            events.Add(WriteDiagnostics(nav, events));
            events.Add(WriteAbout(nav));

            // Opt-in JSON+SPA delivery form, emitted LAST so every captured page is present. Strictly additive:
            // writes only its own files under OutputRoot, leaving every static page byte-identical (AC #3/#5/#6).
            if (_options.EmitSpa)
            {
                EmitSpaSite(nav);
            }
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
            // Keep the opt-in SPA form in sync in watch mode: _spaCapture already holds the fresh page (captured by
            // GenerateOneInternal's WriteOutput), so re-emitting rebuilds the manifest/chunks from current state.
            if (_options.EmitSpa) EmitSpaSite(nav);
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
                // Drop the removed page from the SPA capture so a stale region can't linger in the next bundle.
                if (_spaCapture is not null) _spaCapture.Remove(PathUtil.NormalizeSlashes(doc.OutputRelativePath));
                RefreshCoverage();
                var nav = _nav ?? BuildNav(Array.Empty<string>());
                WriteIndex(nav);
                if (_options.EmitSpa) EmitSpaSite(nav);
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

    /// <summary>True for a non-markdown DATA SOURCE the site reads but the <c>.md</c> dispatch routes don't refresh:
    /// the sprint tracking file (<c>sprint-status.yaml</c> — the sprint board / Now &amp; Next data) and the project
    /// config (<c>_bmad/config.toml</c> — the site branding). The watch dispatch checks this FIRST (before
    /// <see cref="IsAdr"/>/<see cref="IsEpicsRelated"/>) because <c>sprint-status.yaml</c> lives under
    /// <c>implementation-artifacts/</c>, so <see cref="IsEpicsRelated"/> would otherwise claim it and route to
    /// <see cref="RegenerateEpics"/>, which by design never re-parses sprint state (AD-5). Classifies via the shared
    /// <see cref="BmadArtifactAdapter.SprintStatusFileName"/> / <see cref="ForgeOptions.ConfigFileName"/> conventions,
    /// never a second literal. [Story 6.11]</summary>
    public bool IsDataSource(string sourceFullPath) =>
        BmadArtifactAdapter.IsSprintStatusFile(sourceFullPath) || IsProjectConfigFile(sourceFullPath);

    /// <summary>True when <paramref name="path"/> is <c>_bmad/config.toml</c>: the config file NAME under a
    /// <c>_bmad</c> directory SEGMENT (any depth, either slash style) — the same location-tolerant, segment-based
    /// discipline <see cref="BmadArtifactAdapter.IsUnderImplementationArtifacts"/> uses, so a stray <c>config.toml</c>
    /// elsewhere isn't mistaken for the project config. [Story 6.11]</summary>
    private static bool IsProjectConfigFile(string path)
    {
        if (!string.Equals(Path.GetFileName(path), ForgeOptions.ConfigFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(dir)) return false;
        return dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(s => string.Equals(s, ForgeOptions.ConfigDirName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True for the epics file itself, or any file under an implementation-artifacts/ ancestor —
    /// both feed the epics/story pages, so either kind of change should trigger a full
    /// <see cref="RegenerateEpics"/> rather than the generic single-file path. Classifies via the adapter's
    /// shared conventions so watch routing can never disagree with what ingestion discovers. [Story 4.2 Task 4]</summary>
    public bool IsEpicsRelated(string sourceFullPath) =>
        BmadArtifactAdapter.IsEpicsFile(sourceFullPath)
        || BmadArtifactAdapter.IsUnderImplementationArtifacts(sourceFullPath);

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

            // Scoped re-ingest through the adapter: exactly the epics + story-artifact + requirements re-parse
            // this path always did, without touching the sprint/retro/module state it never refreshed (AD-5
            // watch parity). [Story 4.1]
            ProgressModel? progress = null;
            var ingest = _adapter.IngestEpics(_options, files, (model, artifactsById) => progress = ComputeProgress(model, artifactsById));

            if (ingest.SourceFullPath is null)
            {
                RefreshCoverage();
                WriteIndex(nav);
                if (_options.EmitSpa) EmitSpaSite(nav);
                return new GenerationEvent(GenerationOutcome.Skipped, BmadArtifactAdapter.EpicsFileName, sw.Elapsed, $"{BmadArtifactAdapter.EpicsFileName} not found");
            }

            // Same partial-failure caching rules as GenerateAll: a broken mid-edit save must leave the last
            // good models in place so the rest of the site keeps linkifying against them.
            if (progress is not null)
            {
                _epicsModel = ingest.Epics;
                _progress = progress;
                // Watch-mode re-ingest also re-stamps HasRetrospective from the (preserved) retro map, so a retro'd
                // all-done epic doesn't flip to "In review" on the sunburst/donut/badge after an unrelated story
                // edit — the flag must not reset to false when the model is rebuilt. [spec-sunburst-retro]
                TagEpicRetrospectives();
            }
            if (ingest.Requirements is not null)
            {
                _requirements = ingest.Requirements;
            }

            var epicsEvents = new List<GenerationEvent>(MapDiagnostics(ingest.Diagnostics));
            if (ingest is { Epics: { } epicsModel, Requirements: { } requirementsModel } && progress is not null)
            {
                epicsEvents.AddRange(RenderEpicsPages(ingest.SourceFullPath, files, ingest.StoryArtifactsById, epicsModel, requirementsModel, progress, nav));
            }
            RefreshCoverage();
            WriteIndex(nav);
            if (_options.EmitSpa) EmitSpaSite(nav);

            var errored = epicsEvents.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, ToSourceRelative(ingest.SourceFullPath), sw.Elapsed, $"{ingest.ConsumedSourceRelatives.Count} stories");
        }
    }

    /// <summary>Watch-mode route for a non-markdown DATA SOURCE change (<c>sprint-status.yaml</c>,
    /// <c>_bmad/config.toml</c> — see <see cref="IsDataSource"/>). The surfaces these feed (the dashboard's
    /// Now &amp; Next / sprint board, the sprint page, the project branding) are NOT markdown artifacts, so none of
    /// the <c>.md</c> routes refresh them — worse, <c>sprint-status.yaml</c> mis-routes to
    /// <see cref="RegenerateEpics"/>, which deliberately skips sprint state (AD-5). A full <see cref="GenerateAll"/>
    /// is the simplest correct refresh: it re-parses <c>_sprint</c> (and everything else) and rewrites the surfaces,
    /// matching what the VS Code extension already does on every spawn. Scoping the re-render to just the changed
    /// family is the deferred R6.4 perf follow-up, not correctness.
    /// <para><b>config.toml caveat:</b> a full pass re-renders, but <see cref="ForgeOptions.SiteTitle"/> is fixed at
    /// <see cref="ForgeOptions.Resolve"/> time, so a project-NAME change needs a core <c>watch</c> restart to
    /// re-brand (the extension re-resolves options on each fresh spawn and has no such limit). [Story 6.11]</para></summary>
    public GenerationEvent RegenerateFromDataSource(string sourceFullPath)
    {
        var sw = Stopwatch.StartNew();
        // GenerateAll takes _gate itself, so no outer lock here — a full rebuild is the whole point of this route.
        var events = GenerateAll();
        var relative = Path.GetRelativePath(_options.RepoRoot, sourceFullPath).Replace('\\', '/');

        var errored = events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
        if (errored is not null)
        {
            return errored;
        }

        // A Skipped notice means the data source itself didn't actually parse (e.g. malformed sprint-status.yaml) —
        // report that rather than claiming Updated, so the watch-mode log/event stream never misrepresents a
        // silently-unapplied edit as a success. Match by filename, not the full relative path: ingest diagnostics
        // report source-root-relative paths (BmadArtifactAdapter.ToSourceRelative) while `relative` here is
        // repo-root-relative — the two conventions differ, but IsDataSource already narrows this method to exactly
        // sprint-status.yaml / config.toml, so filename equality is unambiguous. [Story 6.11 review]
        var fileName = Path.GetFileName(sourceFullPath);
        var skipped = events.FirstOrDefault(e =>
            e.Outcome == GenerationOutcome.Skipped && string.Equals(Path.GetFileName(e.RelativePath), fileName, StringComparison.OrdinalIgnoreCase));
        if (skipped is not null)
        {
            return skipped;
        }

        return new GenerationEvent(GenerationOutcome.Updated, relative, sw.Elapsed, "data source");
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
            if (_options.EmitSpa) EmitSpaSite(nav);

            var errored = events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            return new GenerationEvent(GenerationOutcome.Updated, "adrs", sw.Elapsed, $"{_adrs.Count} ADRs");
        }
    }

    /// <summary>Renders each hand-authored record under the resolved ADR root into <c>SpecScribeOutput/adrs</c>.
    /// The ADR root's own README.md becomes the landing page (index.html); every other record becomes a card
    /// on the home index — numbered or not (an underivable number sorts the card last rather than dropping it,
    /// AC #2), with README/template scaffolding files still rendering for cross-links but never carded. Records
    /// nested one level (e.g. <c>decisions/2024/0007-x.md</c>) keep their subpath in the output so authored
    /// relative cross-links survive the .md → .html swap. The whole ADR output directory is rebuilt each pass
    /// so a deleted or renamed record can't leave a stale page behind. [Story 4.2 Tasks 1–2]</summary>
    private List<GenerationEvent> GenerateAdrsInternal(SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var adrOutputDir = Path.Combine(_options.OutputRoot, ForgeOptions.AdrOutputSubdir);
        if (Directory.Exists(adrOutputDir))
        {
            Directory.Delete(adrOutputDir, recursive: true);
        }

        var entries = new List<AdrEntry>();
        // Record pages are written AFTER the whole set is ordered (pass 2 below) so each can carry a prev/next pager
        // across all records by number; their DocModels are stashed here by output path meanwhile. [Prev/next navigation]
        var recordDocsByOutput = new Dictionary<string, DocModel>(StringComparer.Ordinal);
        // True once a real (non-synthesized) page has actually been WRITTEN to the landing's output slot — the
        // ADR root's README normally, but ANY ADR file that happens to land at the same output path (e.g. a
        // stray "index.md") counts too, since either way the synthesized landing below must not clobber it. Set
        // only on a successful write (not on filename match alone): a README that exists but fails to parse/render
        // must NOT suppress the fallback synthesis, or the landing the nav always links stays unwritten (a 404).
        var landingPathAlreadyWritten = false;
        foreach (var file in EnumerateAdrFiles())
        {
            var sw = Stopwatch.StartNew();
            var fileName = Path.GetFileName(file);
            var relativeToRoot = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.AdrSourceRoot, file));
            // README-as-landing applies to the ADR root's README only; a nested README renders as a regular page.
            var isReadme = string.Equals(relativeToRoot, "README.md", StringComparison.OrdinalIgnoreCase);

            var outputName = isReadme ? "index.html" : Path.ChangeExtension(relativeToRoot, ".html");
            var outputRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{outputName}");
            var sourceRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{relativeToRoot}");

            try
            {
                var raw = MarkdownConverter.ReadAllTextShared(file);
                var parsed = MarkdownConverter.Convert(file, sourceRelative, outputRelative);
                var isRecord = IsAdrRecordFile(relativeToRoot);

                // Computed once, record files only, so the record's own page and its home-index card agree —
                // both derive status from the same three tolerated shapes (bold line / MADR heading /
                // frontmatter) rather than the page reading frontmatter alone. Non-record files (README,
                // template) keep their parsed frontmatter untouched — they were never eligible for a status
                // badge and a coincidental "**Status:**" line in prose shouldn't grant them one. [Review][Patch]
                var tolerantStatus = isRecord ? ExtractAdrStatus(raw, parsed.Frontmatter) : parsed.Frontmatter.Status;
                var frontmatter = tolerantStatus == parsed.Frontmatter.Status
                    ? parsed.Frontmatter
                    : new Frontmatter
                    {
                        Title = parsed.Frontmatter.Title,
                        Project = parsed.Frontmatter.Project,
                        Date = parsed.Frontmatter.Date,
                        Author = parsed.Frontmatter.Author,
                        Version = parsed.Frontmatter.Version,
                        Status = tolerantStatus,
                        Route = parsed.Frontmatter.Route,
                        Type = parsed.Frontmatter.Type,
                        Id = parsed.Frontmatter.Id,
                        Companions = parsed.Frontmatter.Companions,
                        Sources = parsed.Frontmatter.Sources,
                    };
                var doc = new DocModel
                {
                    SourceRelativePath = parsed.SourceRelativePath,
                    OutputRelativePath = parsed.OutputRelativePath,
                    Title = parsed.Title,
                    Frontmatter = frontmatter,
                    BodyHtml = AdrLinkRewriter.Rewrite(parsed.BodyHtml, PathUtil.RelativePrefix(outputRelative)),
                    Headings = parsed.Headings,
                    HasMermaid = parsed.HasMermaid,
                };

                if (isRecord)
                {
                    // Defer the record page write to pass 2 (below) so its pager can span the fully-ordered record set.
                    recordDocsByOutput[outputRelative] = doc;
                }
                else
                {
                    WriteOutput(outputRelative, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));
                    if (string.Equals(outputRelative, SiteNav.AdrsLandingOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        landingPathAlreadyWritten = true;
                    }
                }

                if (isRecord)
                {
                    var number = ParseAdrNumber(fileName);
                    // Date + one-line summary extracted from the same raw body, once, so the card and any future
                    // page reuse of them agree — tolerant, null when the body carries neither (Story 10.4).
                    var date = ExtractAdrDate(raw, parsed.Frontmatter);
                    var summary = ExtractAdrSummary(raw, doc.Title);
                    entries.Add(new AdrEntry(doc.Title, outputRelative, sourceRelative, tolerantStatus, number, date, summary));
                    if (number is null)
                    {
                        // The unnumbered shape is tolerated, not silent: one categorized non-fatal notice on
                        // the same channel adapter diagnostics ride, for Story 4.8's diagnostics page to
                        // render. The record itself still generated above. [Story 4.2 Task 5]
                        events.AddRange(MapDiagnostics(new[]
                        {
                            new AdapterDiagnostic(AdapterDiagnosticCategory.Unsupported, sourceRelative,
                                "no ADR number derivable from the filename; record rendered unnumbered and sorted last"),
                        }, fromAdr: true));
                    }
                }

                // Records emit their Generated event in pass 2, once actually written; non-records write above.
                if (!isRecord)
                {
                    events.Add(new GenerationEvent(GenerationOutcome.Generated, sourceRelative, sw.Elapsed));
                }
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, sourceRelative, sw.Elapsed, ex.Message));
            }
        }

        // Numbered records keep their numeric order; unnumbered ones sort after them, alphabetically by
        // title — deterministic without inventing a number. [Story 4.2 Task 2]
        _adrs = entries
            .OrderBy(e => e.Number is null)
            .ThenBy(e => e.Number)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pass 2: write each record page now that sibling order is known, giving it a prev/next pager across records
        // (Prev = lower number, Next = higher; unnumbered sort last), disabled at the ends. [Prev/next navigation]
        for (var i = 0; i < _adrs.Count; i++)
        {
            var entry = _adrs[i];
            if (!recordDocsByOutput.TryGetValue(entry.OutputRelativePath, out var recordDoc)) continue;
            var sw = Stopwatch.StartNew();
            try
            {
                var prefix = PathUtil.RelativePrefix(entry.OutputRelativePath);
                var pager = EntityPager.FromSequence(_adrs, i,
                    e => prefix + e.OutputRelativePath,
                    e => e.Title);
                WriteOutput(entry.OutputRelativePath, ApplyReferenceLinks(HtmlTemplater.RenderPage(recordDoc, nav, pager), entry.OutputRelativePath));
                // A record occupying the landing slot (e.g. an `index.md`) must, on successful write, suppress the
                // synthesized landing below so it isn't clobbered — same "only on a successful write" rule the
                // non-record branch above follows. [Prev/next navigation]
                if (string.Equals(entry.OutputRelativePath, SiteNav.AdrsLandingOutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    landingPathAlreadyWritten = true;
                }
                events.Add(new GenerationEvent(GenerationOutcome.Generated, entry.SourceRelativePath, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, entry.SourceRelativePath, sw.Elapsed, ex.Message));
            }
        }

        // The nav (and the dashboard quick link) point at adrs/index.html whenever records exist, but only a
        // successfully-rendered root README (or a same-slot page) ever produced that landing — README-less repos,
        // AND repos whose README exists but fails to parse/render, shipped a 404 in the static site and a dead
        // surface in the webview. Synthesize a minimal landing from the already-ordered records so the link always
        // resolves (owner decision 2026-07-12, spec-webview-doc-page-surfaces review; [Review][Patch] widened
        // 2026-07-13 to also cover the failed-README case). An intentional render ADDITION on repos that didn't
        // already get a landing written: repos WITH a successfully-rendered root README (including the golden
        // fixture) are byte-identical, so the golden gate is unaffected.
        if (!landingPathAlreadyWritten && _adrs.Count > 0)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var body = new StringBuilder();
                body.Append("<p>Architecture decision records for this project.</p>\n<ul class=\"adr-landing-list\">\n");
                foreach (var adr in _adrs)
                {
                    // Records live under the adrs/ output subdir alongside this landing — the href is the
                    // record's output path with that shared prefix stripped (nested records keep their subpath).
                    var prefix = ForgeOptions.AdrOutputSubdir + "/";
                    var href = adr.OutputRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        ? adr.OutputRelativePath[prefix.Length..]
                        : adr.OutputRelativePath;
                    body.Append($"<li><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(adr.Title)}</a>");
                    if (!string.IsNullOrWhiteSpace(adr.Status))
                    {
                        body.Append($" — {PathUtil.Html(adr.Status)}");
                    }
                    body.Append("</li>\n");
                }
                body.Append("</ul>\n");

                var landing = new DocModel
                {
                    SourceRelativePath = ForgeOptions.AdrOutputSubdir,
                    OutputRelativePath = SiteNav.AdrsLandingOutputPath,
                    Title = "Architecture Decisions",
                    Frontmatter = new Frontmatter { Title = "Architecture Decisions" },
                    BodyHtml = body.ToString(),
                    Headings = Array.Empty<Heading>(),
                };
                WriteOutput(SiteNav.AdrsLandingOutputPath,
                    ApplyReferenceLinks(HtmlTemplater.RenderPage(landing, nav), SiteNav.AdrsLandingOutputPath));
                events.Add(new GenerationEvent(GenerationOutcome.Generated,
                    $"{ForgeOptions.AdrOutputSubdir}/index.html (synthesized landing)", sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.AdrsLandingOutputPath, sw.Elapsed, ex.Message));
            }
        }
        return events;
    }

    // Matches common ADR template-scaffolding stems: "template", "adr-template"/"adr_template", and a
    // numbered variant like "0000-template" — an optional leading digits+separator, an optional "adr"
    // token+separator, then "template". [Review][Patch]
    private static readonly Regex AdrTemplateStemPattern = new(@"^(?:\d+[-_])?(?:adr[-_])?template$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True when an ADR-tree file (path relative to the ADR root) is a decision RECORD — i.e. a card
    /// on the home index and a hit for the <see cref="AdrsExist"/> nav gate. READMEs (landing/index prose at
    /// any depth) and template scaffolding files still render as pages so cross-links resolve, but are never
    /// records — the same treatment TEMPLATE.md always had. [Story 4.2 Task 2]</summary>
    private static bool IsAdrRecordFile(string relativeToRoot)
    {
        if (string.Equals(Path.GetFileName(relativeToRoot), "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(relativeToRoot);
        return !AdrTemplateStemPattern.IsMatch(stem);
    }

    /// <summary>Emits one <c>commits/{yyyy-MM-dd}.html</c> page per linked day (the exact set the heatmap
    /// links to, via <see cref="Charts.LinkedCommitDays"/>), each listing that day's commits with prev/next
    /// links to the adjacent active days. Mirrors <see cref="GenerateAdrsInternal"/>: wipe+recreate the dir,
    /// render a bespoke page, run reference-linkification so "Story N.M"/"FR25" mentions in subjects become
    /// links, and write.</summary>
    /// <summary>Generates one date page (<c>commits/{yyyy-MM-dd}.html</c>) per day in the UNION of the git
    /// commit days (<see cref="Charts.LinkedCommitDays"/>) and the artifact-change days (<paramref name="artifactsByDay"/>),
    /// ascending, with prev/next walking the same union — so a day with only an artifact edit (no commit) still
    /// gets a page, and no future-skewed or empty day is ever emitted (AC #1). Each page carries that day's
    /// commits (may be empty) and artifact changes (may be empty); the <paramref name="commitHref"/> resolver
    /// lights up per-commit hash links when Story 7.5's pages exist. Preserves the original commit-days phase's
    /// discipline: wipe+recreate the <c>commits/</c> dir (atomic rebuild), <see cref="ApplyReferenceLinks"/> per
    /// page, per-page try/catch → <see cref="GenerationEvent"/>, and recording the generated entries. Generalized
    /// from the former <c>GenerateCommitDaysInternal</c>. [Story 7.3]</summary>
    private List<GenerationEvent> GenerateDatePagesInternal(
        GitPulse? git,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> artifactsByDay,
        SiteNav nav,
        Func<string, string?>? commitHref = null)
    {
        var events = new List<GenerationEvent>();

        var commitsDir = Path.Combine(_options.OutputRoot, "commits");
        if (Directory.Exists(commitsDir))
        {
            Directory.Delete(commitsDir, recursive: true);
        }

        var commitsByDay = git?.CommitsByDay ?? EmptyCommitsByDay;
        var commitDays = git is not null
            ? Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, DateOnly.FromDateTime(DateTime.Now))
            : (IReadOnlyList<DateOnly>)Array.Empty<DateOnly>();
        var days = ActivityModel.UnionDays(commitDays, artifactsByDay.Keys);
        if (days.Count == 0) return events;

        Directory.CreateDirectory(commitsDir);
        var entries = new List<CommitDayEntry>();
        // Owner-requested exception to EntityPager's usual "display order" rule (see EntityPager's own doc comment):
        // for the two chronological commit surfaces — this one and GenerateCommitDetailsInternal's — Prev/Next read
        // as calendar direction (Prev = earlier, Next = later) rather than list-display direction, even though the
        // underlying lists (and every other pager family: epics, stories, ADRs, retros, code files) stay in their
        // existing display order. Days come ascending (oldest→newest) already, so no reordering is needed here —
        // Prev = the earlier day, Next = the later one. [Prev/next navigation]
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var sw = Stopwatch.StartNew();
            var outputRelative = PathUtil.NormalizeSlashes($"commits/{Charts.D(day)}.html");
            try
            {
                var dayCommits = commitsByDay.TryGetValue(day, out var c) ? c : Array.Empty<CommitInfo>();
                var dayArtifacts = artifactsByDay.TryGetValue(day, out var a) ? a : Array.Empty<(string, string)>();
                var prefix = PathUtil.RelativePrefix(outputRelative);
                var pager = EntityPager.FromSequence(days, i,
                    d => prefix + PathUtil.NormalizeSlashes($"commits/{Charts.D(d)}.html"),
                    Charts.DReadable);
                var html = CommitDayTemplater.RenderPage(day, dayCommits, dayArtifacts, pager, nav, commitHref);

                WriteOutput(outputRelative, ApplyReferenceLinks(html, outputRelative));

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

    /// <summary>Builds the "artifacts updated" signal (Story 7.3 bug fix): every recognized source artifact
    /// (one that resolves to a generated page — epic/story family via <see cref="_referenceMap"/>, standalone
    /// docs via <see cref="_docs"/>, ADRs via <see cref="_adrs"/>) grouped by the day a git commit actually
    /// changed it. Derived from the ONE shared <c>--deep-git</c> per-file numstat fetch
    /// (<see cref="DeepGitPulse.Commits"/> — commit <c>Timestamp</c> + touched <c>Files</c>), never from filesystem
    /// mtime: <see cref="File.GetLastWriteTime"/> collapses to the checkout/generation day in a fresh clone, which
    /// made the most-recent date falsely claim nearly every artifact changed then. When <c>--deep-git</c> is off
    /// (<see cref="ProgressModel.DeepGit"/> null), git can't tell us which files changed when, so we emit NO
    /// artifact signal rather than fabricate one — the timeline/date pages still render from the commit-day set,
    /// just without the per-artifact detail. Because an artifact only changes inside a commit, these days are a
    /// subset of the commit days, so the union never gains a page that git history doesn't back. Reading from all
    /// three page families (not just <see cref="_referenceMap"/>, which only exists when <c>epics.md</c> renders)
    /// means a docs/ADR-only project still gets the signal (Review finding: was silently epics.md-gated). Never
    /// throws (AD-4); empty without deep-git data or any recognized page. [Story 7.3 / 10.4]</summary>
    private IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> BuildArtifactsByDay()
    {
        try
        {
            if (_progress?.DeepGit is not { Commits.Count: > 0 } deep) return EmptyArtifactsByDay;

            // Reconcile source roots up front and map each recognized artifact's repo-relative path → (source path,
            // output href, resolved full path) once, so the commit walk is a cheap dictionary lookup. Pulled from
            // every page family that's actually available this run — epics/story pages resolve against SourceRoot
            // via _referenceMap (only populated when epics.md renders); standalone doc pages resolve against
            // SourceRoot via _docs (always populated); ADR pages resolve against AdrSourceRoot via _adrs (always
            // populated) — their SourceRelativePath carries a synthetic "adrs/" bookkeeping prefix that must be
            // stripped before joining to AdrSourceRoot. Case-insensitive to tolerate Windows casing.
            var repoRelToArtifact = new Dictionary<string, (string SourceRel, string Href, string FullPath)>(StringComparer.OrdinalIgnoreCase);
            void Track(string sourceRel, string href, string root)
            {
                try
                {
                    var full = Path.GetFullPath(Path.Combine(root, sourceRel.Replace('/', Path.DirectorySeparatorChar)));
                    var repoRel = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.RepoRoot, full));
                    if (repoRel.StartsWith("..", StringComparison.Ordinal)) return; // outside the repo — git never reports it
                    // TryAdd (not an overwriting indexer): if two artifacts normalize to the same repo-relative path
                    // (e.g. case-only variants under the OrdinalIgnoreCase comparer), the first one found keeps the
                    // git-day attribution rather than silently losing it to whichever entry happened to iterate last.
                    repoRelToArtifact.TryAdd(repoRel, (sourceRel, PathUtil.NormalizeSlashes(href), full));
                }
                catch
                {
                    // A single malformed source path contributes no mapping — never aborts the whole signal (AD-4).
                }
            }

            foreach (var (sourceRel, href) in _referenceMap) Track(sourceRel, href, _options.SourceRoot);
            foreach (var doc in _docs.Values) Track(doc.SourceRelativePath, doc.OutputRelativePath, _options.SourceRoot);
            foreach (var adr in _adrs)
            {
                var adrPrefix = ForgeOptions.AdrOutputSubdir + "/";
                var relativeToAdrRoot = adr.SourceRelativePath.StartsWith(adrPrefix, StringComparison.Ordinal)
                    ? adr.SourceRelativePath[adrPrefix.Length..]
                    : adr.SourceRelativePath;
                Track(relativeToAdrRoot, adr.OutputRelativePath, _options.AdrSourceRoot);
            }

            if (repoRelToArtifact.Count == 0) return EmptyArtifactsByDay;

            var today = DateOnly.FromDateTime(DateTime.Now);
            var labelCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var items = new List<(DateOnly Day, string Label, string Href)>();
            foreach (var commit in deep.Commits)
            {
                if (commit.Timestamp is not { } stamp) continue; // undated commit can't be placed on a day
                var day = DateOnly.FromDateTime(stamp);
                // Skip (don't clamp) a future-skewed commit: LinkedCommitDays excludes day > today too, so clamping to
                // today would attribute an artifact change to a date page whose commit list never mentions that commit.
                if (day > today) continue;

                foreach (var file in commit.Files)
                {
                    try
                    {
                        var repoRel = PathUtil.NormalizeSlashes(file.Path);
                        if (!repoRelToArtifact.TryGetValue(repoRel, out var art)) continue; // not a recognized artifact

                        if (!labelCache.TryGetValue(art.SourceRel, out var label))
                        {
                            labelCache[art.SourceRel] = label = ArtifactLabel(art.SourceRel, art.FullPath);
                        }
                        items.Add((day, label, art.Href));
                    }
                    catch
                    {
                        // One unreadable/awkward file entry contributes nothing — the rest of the signal survives (AD-4).
                    }
                }
            }

            // GroupArtifactsByDay dedups a (label, href) that recurs across commits on the same day.
            return ActivityModel.GroupArtifactsByDay(items);
        }
        catch
        {
            return EmptyArtifactsByDay;
        }
    }

    /// <summary>A human-readable name for an artifact in the "Artifacts updated" list: its Markdown H1 title
    /// (a cheap single-line read, like the existing <see cref="ExtractArtifactTitle"/> uses), falling back to the
    /// file-name stem when the doc has no heading or can't be read — never a raw source path where a title
    /// exists. [Story 7.3]</summary>
    private static string ArtifactLabel(string sourceRelative, string fullPath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceRelative);
        try
        {
            return ExtractArtifactTitle(MarkdownConverter.ReadAllTextShared(fullPath), stem);
        }
        catch
        {
            return stem;
        }
    }

    /// <summary>Writes the chronological activity timeline (<c>timeline.html</c>) — the reused commit heatmap over
    /// a newest-first list of the union date-page days, each linking to its date page — and caches
    /// <see cref="_timelinePath"/> so the dashboard renders "View activity timeline →" only when the page exists.
    /// Gated by the caller on there being data to show (git pulse OR artifact days), and always called right after
    /// <see cref="GenerateDatePagesInternal"/> in the same phase. Sources its day set from <see cref="_commitDays"/>
    /// (the pages that phase actually wrote) rather than independently recomputing the union — so a day whose page
    /// failed to write, or a same-run midnight rollover between two separately-read "now"s, can never leave the
    /// timeline linking to a page that doesn't exist (Review finding: dead-link race). Mirrors the deep-analytics /
    /// git-insights cache-and-guard pattern: a write failure clears <see cref="_timelinePath"/> so the dashboard
    /// link can never dangle (AD-4 / NFR2). [Story 7.3]</summary>
    private void GenerateTimelineInternal(
        GitPulse? git,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> artifactsByDay,
        SiteNav nav,
        List<GenerationEvent> events,
        IGenerationReporter? reporter)
    {
        var commitsByDay = git?.CommitsByDay ?? EmptyCommitsByDay;
        var days = _commitDays.Select(e => e.Date).ToList();
        if (days.Count == 0)
        {
            _timelinePath = null;
            return;
        }

        reporter?.BeginPhase(GenerationPhase.Timeline);
        var sw = Stopwatch.StartNew();
        try
        {
            var newestFirst = days.OrderByDescending(d => d).ToList();
            var html = TimelineTemplater.RenderPage(git, newestFirst, commitsByDay, artifactsByDay, nav);
            WriteOutput(SiteNav.TimelineOutputPath, ApplyReferenceLinks(html, SiteNav.TimelineOutputPath));
            _timelinePath = SiteNav.TimelineOutputPath;
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.TimelineOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.TimelineOutputPath, sw.Elapsed, ex.Message));
            // The page was never written — clear the cache so the dashboard's "View activity timeline" link
            // (gated on _timelinePath) doesn't point at a page that doesn't exist.
            _timelinePath = null;
        }
        reporter?.EndPhase(GenerationPhase.Timeline);
    }

    /// <summary>Emits one <c>commit/{shortHash}.html</c> detail page per commit in the bounded (<c>-n 300</c>),
    /// <c>--deep-git</c>-gated <see cref="DeepGitPulse.Commits"/> window (Story 7.5, FR-10) and populates
    /// <see cref="_commitPages"/> (full <c>%H</c> hash → output-relative path) so the per-day pages and the Git
    /// Insights hub can light up their guarded hash links. Mirrors <see cref="GenerateCommitDaysInternal"/>/
    /// <see cref="GenerateAdrsInternal"/>: wipe+recreate the <c>commit/</c> dir (atomic rebuild, AD-5), per-commit
    /// try/catch → <see cref="GenerationEvent"/> so one malformed commit never throws out of the phase (NFR-2), and
    /// <see cref="ApplyReferenceLinks"/> so "Story N.M"/"FR-9" mentions in the subject and body become links (AC #1).
    /// File-path cells resolve to Story 7.1's <c>code/…html</c> pages via the guarded <see cref="CodePageHref"/>
    /// resolver (plain when a file has no in-portal page). Bounding is contractual: the phase runs only under
    /// <c>--deep-git</c> and only over the capped fetch window, so ≤300 pages are ever generated (AC #2).</summary>
    private List<GenerationEvent> GenerateCommitDetailsInternal(IReadOnlyList<DeepCommit> commits, SiteNav nav)
    {
        var events = new List<GenerationEvent>();

        var commitDir = Path.Combine(_options.OutputRoot, "commit");
        if (Directory.Exists(commitDir))
        {
            Directory.Delete(commitDir, recursive: true);
        }

        var entries = new List<CommitDetailEntry>();
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        // Track the short-hash filenames used this pass so a (astronomically unlikely) 7-char collision between two
        // full hashes in the ≤300 window widens the abbreviation instead of overwriting the earlier page.
        var usedFileHashes = new HashSet<string>(StringComparer.Ordinal);

        Directory.CreateDirectory(commitDir);

        // Pass 1: assign each commit its (collision-safe) short-hash page path up front, so a page's prev/next pager
        // can point at a neighbor's page before that neighbor is rendered. Hashing is deterministic slicing; a
        // malformed commit is isolated to its own Error and excluded from the sequence (no page → never a pager
        // target), preserving the per-commit isolation contract (NFR-2, AC #2). [Prev/next navigation]
        var slots = new List<(DeepCommit Commit, string OutputRelative)>();
        foreach (var commit in commits)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var shortHash = UniqueShortHash(commit.Hash, usedFileHashes);
                slots.Add((commit, PathUtil.NormalizeSlashes($"commit/{shortHash}.html")));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, string.Empty, sw.Elapsed, ex.Message));
            }
        }

        // Pass 2: render each page with its pager. Commits arrive newest-first (git-log order) in `slots`, but the
        // pager reads chronologically — Prev = the earlier commit, Next = the later one — so it takes the raw
        // FromSequence result against the original (newest-first) order and swaps sides, rather than keeping a
        // second, reversed copy of the slot list in sync. The tooltip names the sibling by subject (short hash when
        // the subject is empty). [Prev/next navigation]
        for (var i = 0; i < slots.Count; i++)
        {
            var (commit, outputRelative) = slots[i];
            var sw = Stopwatch.StartNew();
            try
            {
                var prefix = PathUtil.RelativePrefix(outputRelative);
                var raw = EntityPager.FromSequence(slots, i,
                    s => prefix + s.OutputRelative,
                    s => CommitPagerLabel(s.Commit));
                var pager = new EntityPager(raw.Next, raw.Prev);
                var html = CommitDetailTemplater.RenderPage(commit, nav, CodePageHref, pager);
                WriteOutput(outputRelative, ApplyReferenceLinks(html, outputRelative));

                entries.Add(new CommitDetailEntry(commit.Hash, outputRelative));
                // Key on the full %H so the resolver can match the hub's full hash exactly and the day page's
                // abbreviated %h by prefix. A duplicate full hash in one window (impossible in practice) keeps the
                // first page's path rather than throwing.
                pages.TryAdd(commit.Hash, outputRelative);
                events.Add(new GenerationEvent(GenerationOutcome.Generated, outputRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, outputRelative, sw.Elapsed, ex.Message));
            }
        }

        _commitDetails = entries;
        _commitPages = pages;
        return events;
    }

    /// <summary>The sibling-pager tooltip for a commit: its subject, or its short hash when the subject is empty
    /// (so the tooltip always names something). [Prev/next navigation]</summary>
    private static string CommitPagerLabel(DeepCommit commit) =>
        string.IsNullOrWhiteSpace(commit.Subject)
            ? CommitDetailTemplater.ShortHash(commit.Hash)
            : commit.Subject;

    /// <summary>The prev/next pager for an epic page — adjacent epics by ascending number (Prev = lower, Next =
    /// higher), disabled at the ends. Hrefs are relative to the current epic page. [Prev/next navigation]</summary>
    private static EntityPager EpicPager(EpicsModel model, EpicInfo epic)
    {
        var ordered = model.Epics.OrderBy(e => e.Number).ToList();
        // Identity match (not e.Number ==): a malformed model with a duplicate epic number must not mis-locate the
        // neighbors onto the first occurrence — the ordered list holds the same EpicInfo instances. [review-patch]
        var index = ordered.FindIndex(e => ReferenceEquals(e, epic));
        var prefix = PathUtil.RelativePrefix($"epics/epic-{epic.Number}.html");
        return EntityPager.FromSequence(ordered, index,
            e => prefix + $"epics/epic-{e.Number}.html",
            e => $"Epic {e.Number}: {PathUtil.StripHtmlTags(e.Title)}");
    }

    /// <summary>The prev/next pager for a story page — adjacent stories in global epic→story order (Prev = earlier,
    /// Next = later), disabled at the ends. Placeholder and drafted stories share one sequence (both are real pages);
    /// hrefs are relative to the current story page. [Prev/next navigation]</summary>
    private static EntityPager StoryPager(EpicsModel model, StoryInfo story)
    {
        // Order epics ascending before flattening so the global story sequence is deterministic even if model.Epics
        // is ever unsorted (mirrors EpicPager); identity match avoids mis-locating on a duplicate story id. [review-patch]
        var all = model.Epics.OrderBy(e => e.Number).SelectMany(e => e.Stories).ToList();
        var index = all.FindIndex(s => ReferenceEquals(s, story));
        var currentPath = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
        var prefix = PathUtil.RelativePrefix(currentPath);
        return EntityPager.FromSequence(all, index,
            s => prefix + (s.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(s.Id)),
            s => $"Story {s.Id}: {PathUtil.StripHtmlTags(s.Title)}");
    }

    /// <summary>The per-commit page filename stem: <see cref="CommitDetailTemplater.ShortHash"/> normally, widened
    /// one char at a time only when two full hashes share the same abbreviation in this window (belt-and-suspenders
    /// against a 7-char collision the display hash would otherwise mask). Records the chosen stem in
    /// <paramref name="used"/>.</summary>
    private static string UniqueShortHash(string fullHash, HashSet<string> used)
    {
        var length = CommitDetailTemplater.ShortHashLength;
        var candidate = CommitDetailTemplater.ShortHash(fullHash);
        while (!used.Add(candidate))
        {
            length++;
            if (length >= fullHash.Length)
            {
                // Exhausted the full hash (identical hashes) — fall back to a suffix so the write never clobbers.
                candidate = $"{fullHash}-{used.Count}";
                used.Add(candidate);
                break;
            }
            candidate = fullHash[..length];
        }
        return candidate;
    }

    /// <summary>The guarded per-commit-detail resolver (Story 7.5): a commit hash → its <c>commit/…html</c> page,
    /// output-relative. Matches <see cref="_commitPages"/> keys (full <c>%H</c>) exactly first, then by prefix so an
    /// abbreviated <c>%h</c> from the baseline day-page fetch still resolves (git can widen <c>%h</c> past 7 chars on
    /// collision, so prefix — not equality — is the safe rule). Returns null (→ plain hash) when no page exists.
    /// ≤300 entries, so the linear prefix scan is trivial.</summary>
    private string? CommitHref(string hash)
    {
        if (hash.Length == 0 || _commitPages.Count == 0) return null;
        if (_commitPages.TryGetValue(hash, out var exact)) return exact;
        foreach (var kv in _commitPages)
        {
            if (kv.Key.StartsWith(hash, StringComparison.Ordinal)) return kv.Value;
        }
        return null;
    }

    /// <summary>The guarded per-day resolver: a commit date → its <c>commits/{date}.html</c> page, output-relative.
    /// Populated by <see cref="GenerateDatePagesInternal"/>, which runs before the code pages precisely so this
    /// resolves when the code page's History tab links a change's date (mirrors <see cref="CommitHref"/>). Returns
    /// null (→ plain date text) when no day page was generated for that date. A linear scan, same as
    /// <see cref="CommitHref"/>: <see cref="_commitDays"/> is bounded by the deep-git commit window (≤300 distinct
    /// days) and each call site's own history is capped even smaller (≤15 rows/file), so the worst case per code
    /// page is trivial.</summary>
    private string? DayHref(DateOnly date) =>
        _commitDays.FirstOrDefault(e => e.Date == date)?.OutputRelativePath;

    /// <summary>The guarded code-page resolver for a commit's changed-file path (Story 7.1/7.5): a repo-relative
    /// source path → its <c>code/…html</c> page, output-relative, when that file has an in-portal page — i.e. it is
    /// cited by an artifact OR surfaced by a git-analytics widget (<see cref="_codePages"/>, populated regardless of
    /// <c>--code-url</c>). Returns null (→ plain <c>&lt;code&gt;</c> path) when the file has no page.</summary>
    private string? CodePageHref(string repoRelativePath) =>
        _codePages.TryGetValue(PathUtil.NormalizeSlashes(repoRelativePath), out var path) ? path : null;

    /// <summary>The guarded file→code-item resolver for the git-analytics surfaces (deep-analytics coupling
    /// graph/table + hotspots, the git-insights file table, the code-map treemap, and the dashboard Git Pulse
    /// top-changed files). Resolves the in-portal <c>code/…html</c> page FIRST — its related-files/insights are
    /// the point of the source view — guarded on the page existing in <see cref="_codePages"/>
    /// (<see cref="CodePageHref"/>), which now covers the analytics file sets too (see
    /// <see cref="DiscoverCodeReferences"/>). Falls back to the external hosted source
    /// (<see cref="BuildExternalSourceUrl"/>, only when a <c>--code-url</c> base is configured/detected) for a file
    /// with no in-portal page but that STILL EXISTS on disk (e.g. an <c>_bmad-output</c> doc, excluded from code
    /// pages but a real file). A path that no longer exists — a deleted/renamed file still surfacing in the
    /// change-frequency history — degrades to plain text rather than an external link that would 404: never a dead
    /// link, external included. The external link also stays reachable as each code page's own additive "view source
    /// online" button. Whole-file link (no <c>#L{n}</c>), matching BuildExternalSourceUrl. Both pages sit at the
    /// output root, so the href is used as-is (no page prefix to apply).</summary>
    private string? CodeItemHref(string repoRelativePath)
    {
        var page = CodePageHref(repoRelativePath);
        if (page is not null) return page;
        // Existence-gate the external fallback: TopChangedFiles/Hotspots rank over a history window, so a deleted
        // or renamed file routinely appears — a blob/<branch>/<deleted-path> link would 404. A doc under sourceRoot
        // has no code page but is a real on-disk file, so it keeps its external link.
        var full = Path.GetFullPath(Path.Combine(_options.RepoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(full) ? BuildExternalSourceUrl(repoRelativePath) : null;
    }

    // Above this size a referenced source file is treated as too large to render inline and degraded to a
    // placeholder page (never read into memory in full). A seed value, not a contract — a future settings toggle
    // (Epic 5 / AD-3) could surface it; there is deliberately no knob for it yet. [Story 7.1]
    private const long MaxCodeFileBytes = 1_048_576; // ~1 MB

    /// <summary>Story 7.2 Phase A — discovers the referenced code-file set from the source-artifact corpus AND the
    /// ADR tree (<see cref="EnumerateAdrFiles"/>) and populates <see cref="_codePages"/> (forward map) +
    /// <see cref="_codeReverseMap"/> (file → citing artifacts, for each code page's "Referenced by" block) UP FRONT,
    /// before any citing page is rendered. [Review][Patch] ADR files are scanned here too — every ADR page runs
    /// through <see cref="ApplyReferenceLinks"/> the same as story/doc pages, so an ADR-only code citation must be
    /// discovered here or it silently never resolves. This is what lets <see cref="ApplyReferenceLinks"/> resolve
    /// citations on the story/doc/ADR pages that render before the code pages themselves. Pure discovery: reads the
    /// small <c>*.md</c> set (shared access) and touches no output. In-portal pages are ALWAYS discovered — the
    /// external source base (<c>--code-url</c>, Story 7.7) is additive (a link out from each page), so it no longer
    /// suppresses this pass.</summary>
    private void DiscoverCodeReferences(List<string> sourceFiles)
    {
        _codePages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _codeReferenced = new List<string>();
        _codeReverseMap = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        _citerToFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var repoFull = Path.GetFullPath(_options.RepoRoot);
        var sourceFull = Path.GetFullPath(_options.SourceRoot);
        var referenced = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        // [Review][Patch] One shared scan body for every corpus that can carry a code citation and is run through
        // ApplyReferenceLinks (CodeReferenceLinkifier included) — the _bmad-output planning/implementation corpus
        // AND the ADR tree (GenerateAdrsInternal linkifies every ADR page too, so an ADR-only citation of a file
        // must be discovered here as well or it silently never resolves). citingRelative identifies the citer for
        // BuildReferencedBy's back-nav lookup (which safely omits any citer BuildReferencedBy can't map).
        void ScanArtifact(string file, string citingRelative)
        {
            string raw;
            string dir;
            try
            {
                dir = Path.GetDirectoryName(Path.GetFullPath(file)) ?? _options.SourceRoot;
                raw = MarkdownConverter.ReadAllTextShared(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Non-fatal: an artifact we can't read simply contributes no citations.
                return;
            }

            var citingTitle = ExtractArtifactTitle(raw, citingRelative);

            foreach (var citation in CodeReferenceScanner.ExtractTargets(raw))
            {
                if (!CodeReferenceScanner.TryResolveCitation(citation, dir, repoFull, sourceFull, out var repoRel))
                {
                    continue;
                }
                referenced.Add(repoRel);

                // Reverse map: one entry per citing artifact per referenced file, deduped so an artifact that cites
                // the same file twice lists once. Order is deterministic (source-file iteration is sorted upstream).
                var list = _codeReverseMap.TryGetValue(repoRel, out var existing)
                    ? existing
                    : _codeReverseMap[repoRel] = new List<(string, string)>();
                if (!list.Any(e => string.Equals(e.CitingSourceRelative, citingRelative, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add((citingRelative, citingTitle));
                }

                // Forward map (same pass, no extra scan): this citer also cites repoRel — feeds the "Show
                // relationships" story<->related-file cross edge.
                var files = _citerToFiles.TryGetValue(citingRelative, out var existingFiles)
                    ? existingFiles
                    : _citerToFiles[citingRelative] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                files.Add(PathUtil.NormalizeSlashes(repoRel));
            }
        }

        foreach (var file in sourceFiles)
        {
            ScanArtifact(file, PathUtil.NormalizeSlashes(ToSourceRelative(file)));
            ScanFileListPaths(file);
        }

        void ScanFileListPaths(string artifactFile)
        {
            string raw;
            try { raw = MarkdownConverter.ReadAllTextShared(artifactFile); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }

            foreach (var path in ChangeSurface.ExtractFileList(raw))
            {
                if (CodeReferenceScanner.TryResolveRepoFile(path, repoFull, sourceFull, out var repoRel))
                    referenced.Add(repoRel);
            }
        }

        foreach (var file in EnumerateAdrFiles())
        {
            var adrRelative = PathUtil.NormalizeSlashes($"{ForgeOptions.AdrOutputSubdir}/{Path.GetRelativePath(_options.AdrSourceRoot, file)}");
            ScanArtifact(file, adrRelative);
        }

        // In-portal pages are ALSO minted for the files surfaced by the git-analytics widgets — the dashboard
        // Git Pulse top-changed list, the deep-git hotspots + change-coupling pairs, and the Git Insights hub
        // file table — even when no artifact cites them, so their file links (CodeItemHref) resolve to a code
        // page with its related-files/insights rather than only jumping out to the host. Each candidate must
        // still be a real, non-ignored repository file NOT under sourceRoot (TryResolveRepoFile) — so
        // _bmad-output docs and ignored/binary files never get a page. Every source is a bounded top-N set (no
        // filesystem walk); the SortedSet dedupes and keeps deterministic order. Null-safe on an absent/failed
        // git pass. The whole-codebase code-map metrics are deliberately NOT included (that would be a page per
        // repo file — NFR1); treemap tiles for uncited files fall back to the external link. [in-portal code links]
        void AddAnalyticsCandidate(string candidatePath)
        {
            if (CodeReferenceScanner.TryResolveRepoFile(candidatePath, repoFull, sourceFull, out var repoRel))
            {
                referenced.Add(repoRel);
            }
        }

        if (_progress?.Git is { } analyticsPulse)
        {
            foreach (var (path, _) in analyticsPulse.TopChangedFiles) AddAnalyticsCandidate(path);
        }
        if (_progress?.DeepGit is { } analyticsDeep)
        {
            foreach (var (path, _) in analyticsDeep.Hotspots) AddAnalyticsCandidate(path);
            foreach (var (fileA, fileB, _) in analyticsDeep.Coupling)
            {
                AddAnalyticsCandidate(fileA);
                AddAnalyticsCandidate(fileB);
            }
            if (analyticsDeep.Insights is { } insights)
            {
                foreach (var stat in insights.Files) AddAnalyticsCandidate(stat.Path);
            }
        }

        _codeReferenced = referenced.ToList();
        foreach (var repoRel in _codeReferenced)
        {
            // The page exists for every discovered (real, non-ignored) file — rendered as a full page or a
            // placeholder, both written to this path — so citations can resolve now, before the pages render.
            _codePages[PathUtil.NormalizeSlashes(repoRel)] = PathUtil.NormalizeSlashes($"code/{repoRel}.html");
        }
    }

    // First Markdown H1 ("# Title") as an artifact's display title for the "Referenced by" back-links; falls back
    // to the source-relative path when a doc has no heading. A cheap single-line read (like the project_name /
    // memlog-date reads), not a full parse.
    private static readonly Regex ArtifactTitlePattern = new(
        @"^\s{0,3}#\s+(?<title>.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex FenceMarker = new(@"^\s{0,3}(```|~~~)", RegexOptions.Compiled);

    /// <summary>The first real ATX heading in the document, skipping lines inside a fenced code block (``` or ~~~)
    /// and a leading YAML frontmatter block (--- ... ---) — [Review][Patch] a "# ..." line inside either of those
    /// isn't the artifact's title and previously produced a misleading "Referenced by"/timeline label.</summary>
    private static string ExtractArtifactTitle(string markdown, string fallbackRelative)
    {
        var lines = markdown.Split('\n');
        var inFence = false;
        var i = 0;

        if (i < lines.Length && lines[i].TrimEnd('\r').Trim() == "---")
        {
            var closing = -1;
            for (var j = i + 1; j < lines.Length; j++)
            {
                if (lines[j].TrimEnd('\r').Trim() == "---") { closing = j; break; }
            }
            if (closing >= 0) i = closing + 1;
        }

        for (; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (FenceMarker.IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence) continue;

            var m = ArtifactTitlePattern.Match(line);
            if (m.Success) return m.Groups["title"].Value.Trim();
        }

        return fallbackRelative;
    }

    /// <summary>Renders each referenced, readable repository source file into <c>code/&lt;repo-relative-path&gt;.html</c>
    /// — a line-numbered, escaped, monospace page with a stable <c>id="L{n}"</c> anchor per line (Story 7.1, FR15) and
    /// a "Referenced by" back-link block to the artifacts that cite it (Story 7.2, AC #2). The referenced set is
    /// discovered up front by <see cref="DiscoverCodeReferences"/> (NOT a filesystem walk), so only the small
    /// purposeful set renders (NFR1) and non-referenced files are omitted with no broken navigation (AC #1). Mirrors
    /// <see cref="GenerateDatePagesInternal"/>/<see cref="GenerateAdrsInternal"/>: wipe+recreate the output dir each
    /// pass (atomic rebuild, AD-5) and per-file try/catch → <see cref="GenerationEvent"/> so one bad file never
    /// throws out of the phase (NFR2). When an external source base is configured/detected (<c>--code-url</c>,
    /// Story 7.7) each page additionally carries an additive "view source online" link — the pages still generate.</summary>
    private List<GenerationEvent> GenerateCodePagesInternal(List<string> sourceFiles, SiteNav nav, IGenerationReporter? reporter)
    {
        var events = new List<GenerationEvent>();

        // "Group by epic" join key — built once, right before any code page renders. [reference-graph epic grouping + relationships]
        BuildStoryEpicLookup();

        var codeDir = Path.Combine(_options.OutputRoot, "code");
        if (Directory.Exists(codeDir))
        {
            Directory.Delete(codeDir, recursive: true);
        }

        // External mode (no pages) or no referenced files — nothing to render. The set was discovered up front.
        if (_codeReferenced.Count == 0)
        {
            return events;
        }

        // The syntax highlighter ships only where it is used: copy the vendored Prism bundle + theme to the output
        // root exactly when in-portal code pages are generated, so a site with no code pages stays byte-identical
        // (and the golden fixtures, which cite no real repo files, never gain these assets). Guarded like every
        // per-file write below — a missing/misconfigured embedded resource degrades to a reported error instead of
        // throwing out of the whole phase (NFR2).
        try
        {
            CopyEmbeddedAsset("SpecScribe.assets.prism.js", ForgeOptions.CodeHighlightScriptName);
            CopyEmbeddedAsset("SpecScribe.assets.prism.css", ForgeOptions.CodeHighlightStyleName);
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, ForgeOptions.CodeHighlightScriptName, TimeSpan.Zero, ex.Message));
        }

        var referenced = _codeReferenced;
        var outputRootFull = Path.GetFullPath(_options.OutputRoot);

        // Sibling pager for code pages: files in the SAME directory, ordered alphabetically (Prev = previous file,
        // Next = next). Grouped once so each page's neighbors are known regardless of write order. [Prev/next navigation]
        var codeSiblingsByDir = referenced
            .Select(PathUtil.NormalizeSlashes)
            .GroupBy(p => { var i = p.LastIndexOf('/'); return i < 0 ? string.Empty : p[..i]; })
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList());

        reporter?.BeginPhase(GenerationPhase.CodePages, referenced.Count);
        foreach (var repoRelative in referenced)
        {
            var sw = Stopwatch.StartNew();
            // Append ".html" to the FULL path (extension included) so X.cs and X.ts in one dir never collide.
            var outputRelative = PathUtil.NormalizeSlashes($"code/{repoRelative}.html");

            try
            {
                var full = Path.GetFullPath(Path.Combine(_options.RepoRoot, repoRelative.Replace('/', Path.DirectorySeparatorChar)));

                // Defense in depth: the output path must stay inside the output root (the repo-relative path was
                // already traversal-checked in discovery, but never trust it twice).
                var outputFull = Path.GetFullPath(Path.Combine(_options.OutputRoot, outputRelative.Replace('/', Path.DirectorySeparatorChar)));
                if (!outputFull.StartsWith(outputRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // No page will be written — drop the forward-map entry Phase A optimistically added so no
                    // citation resolves to a page that does not exist.
                    _codePages.Remove(PathUtil.NormalizeSlashes(repoRelative));
                    events.Add(new GenerationEvent(GenerationOutcome.Skipped, outputRelative, sw.Elapsed, "output path escapes the output root"));
                    reporter?.Tick(GenerationPhase.CodePages);
                    continue;
                }

                // Story 7.2 (AC #2): the artifacts that cite this file, resolved to their generated pages.
                var referencedBy = BuildReferencedBy(repoRelative);
                // Story 7.7: additive link to the same file on its hosting platform (null unless a base is set/detected).
                var externalUrl = BuildExternalSourceUrl(repoRelative);
                // Story 7.4: opt-in per-file deep-git insight (null when --deep-git is off / no data → baseline page).
                // Keyed by the same forward-slash repo path the numstat rows carry, so this joins cleanly.
                var insight = _progress?.DeepGit?.FileInsights.GetValueOrDefault(PathUtil.NormalizeSlashes(repoRelative));

                // "Show relationships" cross edges (opt-in toggle): reuses the SAME citer list BuildReferencedBy
                // just walked (index-aligned) and the SAME shared numstat pair map — no new git call, no re-read.
                var citersForRelative = _codeReverseMap.TryGetValue(repoRelative, out var citersList)
                    ? citersList
                    : new List<(string, string)>();
                var storyRelatedEdges = BuildStoryRelatedEdges(citersForRelative, insight);
                var relatedRelatedEdges = BuildRelatedRelatedEdges(insight, _progress?.DeepGit?.CoChangePairs);

                // Prev/next across sibling files in this file's directory (tooltip = filename).
                var rel = PathUtil.NormalizeSlashes(repoRelative);
                var slash = rel.LastIndexOf('/');
                var siblings = codeSiblingsByDir[slash < 0 ? string.Empty : rel[..slash]];
                var codePrefix = PathUtil.RelativePrefix(outputRelative);
                var pager = EntityPager.FromSequence(siblings,
                    siblings.FindIndex(p => string.Equals(p, rel, StringComparison.OrdinalIgnoreCase)),
                    p => codePrefix + PathUtil.NormalizeSlashes($"code/{p}.html"),
                    Path.GetFileName);

                string html;
                GenerationOutcome outcome;
                if (new FileInfo(full).Length > MaxCodeFileBytes)
                {
                    html = CodeFileTemplater.RenderPlaceholder(repoRelative, outputRelative,
                        "This file is too large to render inline.", nav, referencedBy, externalUrl, pager);
                    outcome = GenerationOutcome.Skipped;
                }
                else if (TryReadCodeText(full, out var text))
                {
                    var lines = SplitCodeLines(text);
                    html = CodeFileTemplater.RenderPage(repoRelative, outputRelative, lines, nav, referencedBy, externalUrl,
                        insight, CodePageHref, CommitHref, dayHref: DayHref, pager: pager,
                        storyRelatedEdges: storyRelatedEdges, relatedRelatedEdges: relatedRelatedEdges);
                    outcome = GenerationOutcome.Generated;
                }
                else
                {
                    html = CodeFileTemplater.RenderPlaceholder(repoRelative, outputRelative,
                        "This file is not a readable text file and can't be shown inline.", nav, referencedBy, externalUrl, pager);
                    outcome = GenerationOutcome.Skipped;
                }

                WriteOutput(outputRelative, html);
                events.Add(new GenerationEvent(outcome, outputRelative, sw.Elapsed));
            }
            catch (Exception ex)
            {
                // The page was not written — drop Phase A's forward-map entry so no citation links to a 404.
                _codePages.Remove(PathUtil.NormalizeSlashes(repoRelative));
                events.Add(new GenerationEvent(GenerationOutcome.Error, outputRelative, sw.Elapsed, ex.Message));
            }
            reporter?.Tick(GenerationPhase.CodePages);
        }
        reporter?.EndPhase(GenerationPhase.CodePages);

        return events;
    }

    /// <summary>Resolves a code file's citing artifacts (captured by <see cref="DiscoverCodeReferences"/>) to the
    /// output-relative URLs of their generated pages + a display title — the "Referenced by" back-navigation on the
    /// code page (Story 7.2, AC #2). Routing reuses the epics render pass's <see cref="_referenceMap"/> (which maps
    /// every source file, story artifacts included, to its page). [Review][Patch] A citer missing from
    /// <see cref="_referenceMap"/> is OMITTED rather than guessed via a naive extension swap — never link to a page
    /// we can't confirm was actually mapped. Order is deterministic (reverse-map insertion follows the sorted source
    /// walk).</summary>
    private IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)> BuildReferencedBy(string repoRelative)
    {
        if (!_codeReverseMap.TryGetValue(repoRelative, out var citers) || citers.Count == 0)
        {
            return Array.Empty<(string, string, (int, string)?)>();
        }

        var result = new List<(string OutputUrl, string Title, (int, string)? Epic)>(citers.Count);
        foreach (var (citingSourceRelative, title) in citers)
        {
            var url = _referenceMap.TryGetValue(citingSourceRelative, out var mapped)
                ? mapped
                : PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(citingSourceRelative));
            var epic = _storyEpicByOutputPath is { } lookup &&
                       lookup.TryGetValue(PathUtil.NormalizeSlashes(url), out var e)
                ? e
                : ((int, string)?)null;
            result.Add((url, title, epic));
        }
        return result;
    }

    /// <summary>Resolves each story's generated page path (<c>ArtifactOutputPath</c>, falling back to the
    /// synthesized <see cref="StoryEpicLinkifier.StoryPagePath"/> a placeholder story page uses) to its owning
    /// epic's number/title — the "Group by epic" reference-graph toggle's join key. Built ONCE, right before code
    /// pages render (so <see cref="_epicsModel"/> is fully populated), no new parsing of titles/paths (reuses the
    /// same fields the epics render pass already carries). A null/absent <see cref="_epicsModel"/> (no epics.md)
    /// leaves the lookup null — every citer then resolves to "no epic", the graceful non-story-shaped degradation.
    /// [reference-graph epic grouping + relationships]</summary>
    private void BuildStoryEpicLookup()
    {
        if (_epicsModel is null)
        {
            _storyEpicByOutputPath = null;
            return;
        }

        var lookup = new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var epic in _epicsModel.Epics)
        {
            foreach (var story in epic.Stories)
            {
                // TryAdd (not the indexer): a duplicate output path is a pre-existing data anomaly upstream
                // (two stories resolving to the same page), not something to silently overwrite — the first
                // epic seen (in the model's own iteration order) wins deterministically.
                var path = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                lookup.TryAdd(PathUtil.NormalizeSlashes(path), (epic.Number, epic.Title));
            }
        }
        _storyEpicByOutputPath = lookup;
    }

    /// <summary>"Show relationships" edge #1 (reference-graph epic grouping + relationships): for each citing
    /// artifact (index-aligned with <paramref name="citers"/>, the SAME order <see cref="BuildReferencedBy"/> walks
    /// so the caller's ref index lines up with the graph's artifact-node index), checks whether that artifact ALSO
    /// cites one of this file's related/coupled files (index-aligned with <paramref name="insight"/>'s
    /// <see cref="FileInsight.CoupledFiles"/>, the same order <c>CodeFileTemplater.BuildRelatedNodes</c> renders) —
    /// using the forward map <see cref="_citerToFiles"/> built in the SAME discovery pass as the reverse map (no
    /// re-read of any artifact). Returns empty (never throws) when there is no insight/coupling or no citers.</summary>
    private IReadOnlyList<(int RefIndex, int RelatedIndex)> BuildStoryRelatedEdges(
        IReadOnlyList<(string CitingSourceRelative, string Title)> citers, FileInsight? insight)
    {
        if (insight is null || insight.CoupledFiles.Count == 0 || citers.Count == 0)
        {
            return Array.Empty<(int, int)>();
        }

        var result = new List<(int, int)>();
        for (var i = 0; i < citers.Count; i++)
        {
            if (!_citerToFiles.TryGetValue(citers[i].CitingSourceRelative, out var citedFiles) || citedFiles.Count == 0)
            {
                continue;
            }
            for (var j = 0; j < insight.CoupledFiles.Count; j++)
            {
                if (citedFiles.Contains(PathUtil.NormalizeSlashes(insight.CoupledFiles[j].Path)))
                {
                    result.Add((i, j));
                }
            }
        }
        return result;
    }

    /// <summary>"Show relationships" edge #2 (reference-graph epic grouping + relationships): every pair of this
    /// file's related/coupled files (<see cref="FileInsight.CoupledFiles"/>, index-aligned with the graph's
    /// related-node index) that are THEMSELVES frequently co-changed — reusing <see cref="GitMetrics.CoChangeCount"/>
    /// over the already-computed <see cref="DeepGitPulse.CoChangePairs"/> map (no new git call, no re-scan). "Frequently"
    /// mirrors the SAME &gt;= 2 threshold <see cref="GitMetrics.ParseNumstatLog"/>'s own top-level coupling view uses,
    /// so a related-file pair only earns a cross edge here when it would also have qualified for the hub's coupling
    /// list. Returns empty (never throws) when there is no insight, fewer than two related files, or no pair data.</summary>
    private static IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)> BuildRelatedRelatedEdges(
        FileInsight? insight, IReadOnlyDictionary<(string FileA, string FileB), int>? pairs)
    {
        if (insight is null || insight.CoupledFiles.Count < 2 || pairs is null || pairs.Count == 0)
        {
            return Array.Empty<(int, int)>();
        }

        var files = insight.CoupledFiles;
        var normalized = new string[files.Count];
        for (var i = 0; i < files.Count; i++) normalized[i] = PathUtil.NormalizeSlashes(files[i].Path);

        var result = new List<(int, int)>();
        for (var a = 0; a < files.Count; a++)
        {
            for (var b = a + 1; b < files.Count; b++)
            {
                var count = GitMetrics.CoChangeCount(pairs, normalized[a], normalized[b]);
                if (count >= 2) result.Add((a, b));
            }
        }
        return result;
    }

    /// <summary>The additive "view source online" URL for a code page (Story 7.7): the configured/detected external
    /// base joined with the repo-relative path, or null when no base is set (in-portal only). No <c>#L{n}</c> here —
    /// this is a whole-file link; per-line deep links stay in-portal via <see cref="CodeReferenceLinkifier"/>.</summary>
    private string? BuildExternalSourceUrl(string repoRelative) =>
        _options.CodeSourceBaseUrl is { Length: > 0 } baseUrl
            ? baseUrl.TrimEnd('/') + "/" + CodeSourceUrlResolver.EscapeUrlSegments(PathUtil.NormalizeSlashes(repoRelative))
            : null;

    /// <summary>Reads a source file as UTF-8 text with shared access, returning false when it is binary — a NUL byte
    /// anywhere or a strict-UTF-8 decode failure, the same dependency-free heuristic git uses. A leading UTF-8 BOM is
    /// stripped so it never lands as a stray character on line 1. [Story 7.1]</summary>
    private static bool TryReadCodeText(string fullPath, out string text)
    {
        text = string.Empty;
        byte[] bytes;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            bytes = ms.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return false;
        }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes, offset, bytes.Length - offset);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Normalizes <c>\r\n</c>/<c>\r</c> to <c>\n</c> so line numbers match editors, then splits into lines.
    /// A single trailing empty entry from a terminating newline is dropped (editors/GitHub don't number a phantom
    /// final line); a genuine blank line in the body is preserved so anchors stay 1:1 with source lines. [Story 7.1]</summary>
    private static IReadOnlyList<string> SplitCodeLines(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lines.Length > 1 && lines[^1].Length == 0)
        {
            return lines[..^1];
        }
        return lines;
    }


    /// pass produced its hub aggregates (<see cref="DeepGitPulse.Insights"/>). A no-op otherwise: with
    /// <c>--deep-git</c> off the deep pass never ran, <c>DeepGit</c> is null, and no hub work (or page)
    /// happens at all — that gate IS the AC #2 performance guarantee. Mirrors the deep-analytics page's
    /// non-fatal contract: a render/write failure logs an <see cref="GenerationOutcome.Error"/> event, clears
    /// <see cref="DeepGitPulse.Insights"/> so the dashboard's "View all git insights" link can't dangle, and
    /// never fails the run (AD-4 / NFR2). [Story 3.8]</summary>
    private void GenerateGitInsightsInternal(SiteNav nav, List<GenerationEvent> events, IGenerationReporter? reporter)
    {
        if (_progress?.DeepGit is not { Insights: { } insights }) return;

        reporter?.BeginPhase(GenerationPhase.GitInsights);
        var sw = Stopwatch.StartNew();
        try
        {
            // Wire the guarded commit-detail resolver (Story 7.5): the hub's "latest {hash}" links light up as
            // links into commit/ when a per-commit page exists, plain otherwise. The hub sits at the output root,
            // so the resolver's root-relative path is used as-is (the templater applies no prefix there).
            var html = GitInsightsTemplater.RenderPage(insights, _progress.Git, nav, fileHref: CodeItemHref, commitHref: CommitHref);
            WriteOutput(SiteNav.GitInsightsOutputPath, ApplyReferenceLinks(html, SiteNav.GitInsightsOutputPath));
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.GitInsightsOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.GitInsightsOutputPath, sw.Elapsed, ex.Message));
            // The page was never written — clear Insights so the dashboard's "View all git insights" link
            // (gated on _progress.DeepGit.Insights) doesn't point at a page that doesn't exist.
            _progress.DeepGit.Insights = null;
        }
        reporter?.EndPhase(GenerationPhase.GitInsights);
    }

    /// <summary>Renders every epics-phase page (epics.html, requirements pages, per-epic and per-story pages)
    /// from the adapter-ingested models — the render half of what used to be one inline parse+render pass in
    /// this class. Every templater call and write target is unchanged (AC #1: template and page generators
    /// remain framework-agnostic); only where the models come from moved. Note the per-story artifact
    /// FRAGMENT extraction (task list, blurb/remainder split, AC/dev-record sections) deliberately stays in
    /// this render loop: those calls produce page-shaped HTML fragments on demand, and re-seating them is a
    /// contract question for Story 4.2/6.1, not this seam. [Story 4.1]</summary>
    private List<GenerationEvent> RenderEpicsPages(
        string epicsFullPath,
        List<string> files,
        IReadOnlyDictionary<string, string> artifactMap,
        EpicsModel model,
        RequirementsModel requirements,
        ProgressModel progress,
        SiteNav nav)
    {
        var events = new List<GenerationEvent>();
        var sw = Stopwatch.StartNew();
        var epicsSourceRelative = ToSourceRelative(epicsFullPath);

        try
        {
            var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
            var referenceMap = BuildReferenceMap(files, model, artifactMap, PathUtil.NormalizeSlashes(epicsSourceRelative));

            // Cached for the webview delivery path (Story 6.4): RenderWebviewSurfaces re-renders these same
            // epics/story pages through the WebviewRenderAdapter from the generator's cached models, and needs
            // the artifact + reference maps this render loop already resolved. Both full and watch-mode passes
            // come through here, so the cache can never lag the rendered site.
            _storyArtifactsById = artifactMap;
            _referenceMap = referenceMap;
            // The epics file's REPO-relative source path (Story 6.10 reveal-source): epic/index/placeholder
            // surfaces resolve their "Open source" target to it. Repo-relative (not source-root-relative like
            // ToSourceRelative) so the host joins it to the workspace folder with the same one convention the
            // story `.md` and `configuredOutputRoot` use — no `_bmad-output` literal anywhere host-side.
            _epicsSourcePath = RepoRelative(epicsFullPath);

            // Counts ledger may not exist yet (built after workInventory in GenerateAll) — provisional Build
            // shares the same Defined/Tracked fields the final ledger will. Work inventory is rebuilt here so
            // the sunburst follow-up band can see deferred open counts without waiting for WriteIndex.
            // [Story 8.3; Story 9.7]
            var workForFollowUps = WorkInventory.Build(_docs.Values.ToList());
            var epicsCounts = _counts ?? ProjectCounts.Build(progress, _sprint, workForFollowUps, model);
            var followUps = FollowUpGeometry.From(
                _sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
                epicsCounts,
                workForFollowUps);
            File.WriteAllText(Path.Combine(_options.OutputRoot, "epics.html"), ApplyReferenceLinks(EpicsTemplater.RenderIndex(model, progress, nav, _module.Commands, epicsCounts, followUps), "epics.html"));

            // Rebuild the epics output dir each pass so a story removed or renumbered in epics.md — or an
            // undrafted story that got a placeholder and then vanished — can't leave a stale page behind,
            // mirroring the ADR output dir's rebuild. GenerateAll already wiped OutputRoot (no-op here); this
            // matters for watch-mode RegenerateEpics, which doesn't wipe the whole tree.
            var epicsDir = Path.Combine(_options.OutputRoot, "epics");
            if (Directory.Exists(epicsDir)) Directory.Delete(epicsDir, recursive: true);
            Directory.CreateDirectory(epicsDir);

            WriteRequirements(requirements, model, progress, nav, workForFollowUps);

            foreach (var epic in model.Epics)
            {
                var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyReferenceLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath, EpicPager(model, epic), followUps), $"epics/epic-{epic.Number}.html", skipEpicNumber: epic.Number));

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null)
                    {
                        // Undrafted story: emit a placeholder page at the exact path its real page will
                        // use, so "Story N.M" mentions always have a live target and a later-drafted
                        // artifact overwrites it in place. ArtifactOutputPath stays null — placeholders
                        // must never count as detailed stories anywhere progress is computed.
                        var placeholderPath = StoryEpicLinkifier.StoryPagePath(story.Id);
                        var placeholderHtml = EpicsTemplater.RenderStoryPlaceholder(epic, story, nav, _module.Commands, epicRetroPath, StoryPager(model, story));
                        File.WriteAllText(Path.Combine(_options.OutputRoot, placeholderPath.Replace('/', Path.DirectorySeparatorChar)), ApplyReferenceLinks(placeholderHtml, placeholderPath, skipStoryId: story.Id));
                        continue;
                    }

                    // story.Status/TasksDone were filled by ProgressCalculator above — no re-read needed.
                    var f = BuildStoryPageFragments(story, artifactMap[story.Id], referenceMap);
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria, f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, f.Evidence, f.ChangeSurface, nav, _module.Commands, epicRetroPath, StoryPager(model, story));
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

    /// <summary>The per-story artifact fragments a drafted story page renders — the extraction + linkify pipeline
    /// that lived inline in <see cref="RenderEpicsPages"/>, re-homed so the webview delivery path
    /// (<see cref="RenderWebviewSurfaces"/>) composes the story page from the IDENTICAL fragments rather than a
    /// drift-prone copy. [Story 6.4]</summary>
    private sealed record StoryPageFragments(
        string ArtifactRelative,
        string BlurbHtml,
        string RemainderHtml,
        IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria,
        IReadOnlyList<(string Label, string ContentHtml)> DevAgentRecord,
        IReadOnlyList<TaskItem> Tasks,
        string ReviewFindingsHtml,
        string ChangeLogHtml,
        StoryEvidence Evidence,
        StoryChangeSurface ChangeSurface);

    /// <summary>Reads one drafted story's artifact and produces its page fragments (task list, blurb/remainder
    /// split, AC / dev-record / review / change-log sections), source-citation-linkified against
    /// <paramref name="referenceMap"/> and with "(AC: #N)" plan references deep-linked. A verbatim re-homing of
    /// the fragment block from <see cref="RenderEpicsPages"/> — bytes unchanged (the golden regression is the
    /// gate). [Story 4.1 fragments; re-homed Story 6.4; evidence strip Story 9.4]</summary>
    private StoryPageFragments BuildStoryPageFragments(StoryInfo story, string artifactFullPath, Dictionary<string, string> referenceMap)
    {
        var artifactRelative = ToSourceRelative(artifactFullPath);
        var artifactRaw = MarkdownConverter.ReadAllTextShared(artifactFullPath);
        var tasks = TaskListParser.Parse(artifactRaw);
        var (blurbHtml, remainderHtml) = EpicsParser.SplitStoryArtifact(artifactRaw);
        var acceptanceCriteria = EpicsParser.ExtractAcceptanceCriteria(artifactRaw);
        var devAgentRecord = EpicsParser.ExtractDevAgentRecord(artifactRaw);
        var reviewFindingsHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Review Findings");
        var changeLogHtml = EpicsParser.ExtractNamedSectionHtml(artifactRaw, "## Change Log");

        // Turn "[Source: _bmad-output/path.md]" citations into real links to the generated page. Only drafted
        // stories reach here (both callers guard on ArtifactOutputPath before resolving the artifact path).
        var storyPrefix = PathUtil.RelativePrefix(story.ArtifactOutputPath!);
        blurbHtml = SourceLinkifier.Linkify(blurbHtml, referenceMap, storyPrefix);
        remainderHtml = SourceLinkifier.Linkify(remainderHtml, referenceMap, storyPrefix);
        reviewFindingsHtml = SourceLinkifier.Linkify(reviewFindingsHtml, referenceMap, storyPrefix);
        changeLogHtml = SourceLinkifier.Linkify(changeLogHtml, referenceMap, storyPrefix);
        acceptanceCriteria = acceptanceCriteria
            .Select(ac => ac with { Html = SourceLinkifier.Linkify(ac.Html, referenceMap, storyPrefix) })
            .ToList();
        string? CodePageHrefForStory(string repoRelativePath)
        {
            var norm = PathUtil.NormalizeSlashes(ChangeSurface.NormalizeFileListPath(repoRelativePath));
            if (!_codePages.TryGetValue(norm, out var page)) return null;
            return storyPrefix + page;
        }

        var fileResolver = new ChangeSurfaceFileResolver(storyPrefix, referenceMap, CodePageHrefForStory);

        devAgentRecord = devAgentRecord
            .Select(e =>
            {
                var html = SourceLinkifier.Linkify(e.ContentHtml, referenceMap, storyPrefix);
                if (e.Label == "File List")
                    html = FileListLinkifier.LinkifyHtml(html, fileResolver.ResolveForDevRecord);
                return (e.Label, ContentHtml: html);
            })
            .ToList();

        // Deep-link every "(AC: #N)" reference in the plan to its criterion panel above.
        var criteriaByNumber = acceptanceCriteria.ToDictionary(ac => ac.Number, ac => ac.PlainText);
        remainderHtml = EpicsParser.LinkifyAcReferences(remainderHtml, criteriaByNumber);

        // Collapse Dev Notes / References last — after every flat-HTML transform — so the <details>
        // insertion + buried-H3 id-strip is the final mutation. Shared fragment → HTML/webview/SPA stay
        // byte-identical. No-match degrades to the unchanged remainder (NFR8). [Story 9.5]
        remainderHtml = CollapsibleSections.WrapStoryRemainder(remainderHtml);

        // TasksDone/Total already filled by ProgressCalculator — one source of truth (Story 8.2). Tests +
        // verified date are best-effort free-text heuristics; no new authoring schema. [Story 9.4]
        var changelog = EpicsParser.ExtractChangeLogVerification(artifactRaw);
        var evidence = new StoryEvidence(
            story.TasksDone,
            story.TasksTotal,
            EpicsParser.ExtractTestEvidence(artifactRaw),
            changelog?.Date,
            changelog?.IsVerification ?? false);

        var verifyBeforeReviewHtml = EpicsParser.ExtractSubsectionHtml(artifactRaw, "### Verify before marking review");
        if (verifyBeforeReviewHtml.Length > 0)
            verifyBeforeReviewHtml = SourceLinkifier.Linkify(verifyBeforeReviewHtml, referenceMap, storyPrefix);
        else
            verifyBeforeReviewHtml = null;

        var changeSurface = ChangeSurface.Build(
            artifactRaw, acceptanceCriteria, fileResolver.Resolve, verifyBeforeReviewHtml);

        return new StoryPageFragments(
            artifactRelative, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord, tasks,
            reviewFindingsHtml, changeLogHtml, evidence, changeSurface);
    }

    /// <summary>The artifact + reference maps the last epics render pass resolved, cached for
    /// <see cref="RenderWebviewSurfaces"/> (both <see cref="GenerateAll"/> and <see cref="RegenerateEpics"/>
    /// route through <see cref="RenderEpicsPages"/>, which refreshes them). [Story 6.4]</summary>
    private IReadOnlyDictionary<string, string> _storyArtifactsById = new Dictionary<string, string>();
    private Dictionary<string, string> _referenceMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The epics file's repo-relative source path (forward-slashed), captured in
    /// <see cref="RenderEpicsPages"/> so <see cref="RenderWebviewSurfaces"/> can point epic/index/placeholder
    /// surfaces at it for the Story 6.10 reveal-source affordance. Empty until the first epics render pass. [Story 6.10]</summary>
    private string? _epicsSourcePath;

    /// <summary>Expresses an absolute path relative to the repo root with forward slashes — the ONE source-path
    /// convention the VS Code host joins to the workspace folder (matching <c>configuredOutputRoot</c>), so no
    /// <c>_bmad-output</c> or other path-structure literal is ever duplicated host-side (Story 6.10 AC #1). [Story 6.10]</summary>
    private string RepoRelative(string absolutePath) =>
        PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.RepoRoot, absolutePath));

    /// <summary>Renders the webview's navigable surface set — dashboard, epics index, every epic page, and every
    /// story page/placeholder (the five Story 6.2 surface families) — through the
    /// <see cref="WebviewRenderAdapter"/>, from the SAME cached models, builders, and fragment pipeline the HTML
    /// site was generated from. This is the Story 6.4 delivery seam ADR 0005 ratified: the extension gets
    /// finished, CSP-safe HTML produced entirely in C#; it re-parses no markdown and scrapes no generated site
    /// (AD-1/AD-2). Requires a completed <see cref="GenerateAll"/> pass on this instance (the webview CLI command
    /// runs one first); a pure READ of cached state plus source story artifacts — it writes nothing (AC #6).
    /// [Story 6.4]</summary>
    public WebviewBundle RenderWebviewSurfaces()
    {
        lock (_gate)
        {
            var nav = _nav ?? throw new InvalidOperationException(
                "RenderWebviewSurfaces requires a completed GenerateAll() pass on this generator.");

            var surfaces = new List<WebviewSurface>();
            // Built in lock-step with the surfaces below so every outline SurfacePath is captured from the SAME
            // PageView the surface is rendered from — never re-derived — guaranteeing it matches a surfaces[...]
            // key a tree click can push() to (Story 6.9 fact #5). Data only; no HTML, no re-parse.
            var outlineEpics = new List<OutlineEpic>();

            // Dashboard — the same inputs WriteIndex hands the templater, so the webview dashboard can never
            // disagree with the generated index.html.
            var docs = _docs.Values.ToList();
            var work = WorkInventory.Build(docs);
            var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, work, _epicsModel);
            var followUps = FollowUpGeometry.From(
                _sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
                counts,
                work);
            var dashboardPage = HtmlTemplater.BuildIndexPage(
                docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands,
                work, _sprint, _retros, _coverage, _timelinePath is not null, counts: counts);
            surfaces.Add(WebviewSurfaceFor(dashboardPage));

            // Epics family — mirrors RenderEpicsPages' iteration exactly (same retro map, same per-epic
            // progress, same placeholder rule, same fragment pipeline), rendered to webview content instead of
            // written to disk.
            if (_epicsModel is { } model && _progress is { } progress)
            {
                var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
                surfaces.Add(WebviewSurfaceFor(EpicsTemplater.BuildIndexPage(model, progress, nav, _module.Commands, counts, followUps), _epicsSourcePath));

                foreach (var epic in model.Epics)
                {
                    var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                    var epicPage = EpicsTemplater.BuildEpicPage(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath, EpicPager(model, epic), followUps);
                    surfaces.Add(WebviewSurfaceFor(epicPage, _epicsSourcePath, skipEpicNumber: epic.Number));

                    var outlineStories = new List<OutlineStory>();
                    foreach (var story in epic.Stories)
                    {
                        PageView storyPage;
                        // A drafted story's REPO-relative `.md` — the reveal-source target (Story 6.10) AND the
                        // harmonized 6.9 tree "Open Source" path (one convention, host-joined to the workspace
                        // folder). Null for a placeholder (no artifact yet); its surface still reveals the epics
                        // file (the placeholder's source IS the epic), but the outline node omits "Open Source".
                        string? storySourcePath = null;
                        if (story.ArtifactOutputPath is null || !_storyArtifactsById.TryGetValue(story.Id, out var artifactFullPath))
                        {
                            storyPage = EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath, StoryPager(model, story));
                        }
                        else
                        {
                            storySourcePath = RepoRelative(artifactFullPath);
                            var f = BuildStoryPageFragments(story, artifactFullPath, _referenceMap);
                            storyPage = EpicsTemplater.BuildStoryPage(
                                epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria,
                                f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, f.Evidence, f.ChangeSurface, nav,
                                _module.Commands, epicRetroPath, StoryPager(model, story));
                        }
                        surfaces.Add(WebviewSurfaceFor(storyPage, storySourcePath ?? _epicsSourcePath, skipStoryId: story.Id));

                        var storyStage = StatusStyles.ForStory(story);
                        // `commands` = full Next Steps set (incl. done's muted correct-course hatch when exposed);
                        // `helperCommand` = PrimaryStoryCommand (null for done — hatch is not a primary).
                        // [spec-vscode-sidebar-shortcuts-…-quickpick; Story 8.5]
                        var storyCommands = BmadCommands.StoryCommands(story, _module.Commands);
                        outlineStories.Add(new OutlineStory(
                            story.Id, story.Title, storyStage, StatusStyles.StoryLabel(storyStage),
                            PathUtil.NormalizeSlashes(storyPage.OutputRelativePath),
                            storySourcePath,
                            story.TasksDone, story.TasksTotal,
                            BmadCommands.PrimaryStoryCommand(story, _module.Commands),
                            storyCommands));
                    }

                    var epicStage = StatusStyles.ForEpicWithRetrospective(epic);
                    outlineEpics.Add(new OutlineEpic(
                        epic.Number, epic.Title, epicStage, StatusStyles.EpicLabel(epicStage),
                        PathUtil.NormalizeSlashes(epicPage.OutputRelativePath),
                        StoriesTotal: epic.Stories.Count,
                        StoriesDone: epic.Stories.Count(s => StatusStyles.ForStory(s) == "done"),
                        outlineStories));
                }
            }

            var outline = new ProjectOutline(outlineEpics, BuildOutlineSummary(outlineEpics));

            // Long-tail pages (docs, ADRs, requirements, sprint, retros, about, diagnostics…) captured at the
            // WriteOutput seam during this run's GenerateAll (CapturePages — the webview command turns it on):
            // every header nav link and index drill gets a live in-panel target instead of the shim's
            // "isn't part of the in-editor view" toast. Regions are sliced from the render pipeline's OWN
            // output via the Story 6.7 landmark extraction (never a disk read-back — AD-1/AD-2), with fresh
            // per-page nav markup so active-state travels with the surface exactly like the family surfaces.
            // The dashboard/epics families above keep their view-model render path (strongest parity) and are
            // never shadowed.
            // Deliberate EXCLUSIONS (owner decision 2026-07-12) — the page classes that scale with the TARGET
            // repo rather than its planning artifacts: code pages (Story 7.1 — matched as the exact _codePages
            // set, not a path prefix, so a source folder literally named code/ still surfaces), commit-day
            // pages (Story 7.3 — one per active day, unbounded on old repos), and Story 7.5's commit/ detail
            // pages (prefix — that story's cache is concurrent in-flight work). In-editor those clicks toast
            // honestly; 7.2 citations already open the real file via revealSource. [spec-webview-doc-page-surfaces]
            if (CapturePages && _spaCapture is null)
            {
                throw new InvalidOperationException(
                    "CapturePages was set after GenerateAll(); set it before generating so the write seam captures pages.");
            }
            if (_spaCapture is { } capture)
            {
                var familyPaths = new HashSet<string>(
                    surfaces.Select(s => PathUtil.NormalizeSlashes(s.OutputRelativePath)),
                    StringComparer.OrdinalIgnoreCase);
                var excluded = new HashSet<string>(_codePages.Values, StringComparer.OrdinalIgnoreCase);
                foreach (var day in _commitDays)
                {
                    excluded.Add(PathUtil.NormalizeSlashes(day.OutputRelativePath));
                }
                var sourceByOutput = BuildCapturedSourceMap();
                foreach (var (path, fullHtml) in capture)
                {
                    var normalized = PathUtil.NormalizeSlashes(path);
                    if (familyPaths.Contains(normalized)) continue;
                    if (excluded.Contains(normalized)) continue;
                    if (normalized.StartsWith("commit/", StringComparison.OrdinalIgnoreCase)) continue;
                    var navMarkup = HtmlRenderAdapter.Shared.RenderNavMarkup(nav.ToNavigationView(normalized));
                    var region = SpaDelivery.ExtractContentRegion(fullHtml, navMarkup);
                    if (ReferenceEquals(region, navMarkup))
                    {
                        // No <main> landmark → the slice degraded to nav-only. A silently BLANK surface is
                        // worse than the shim's honest toast, so skip it (the SPA keeps its nav-only degrade:
                        // a browser tab is escapable; a status panel claiming "links work" is not).
                        continue;
                    }
                    sourceByOutput.TryGetValue(normalized, out var capturedSource);
                    surfaces.Add(new WebviewSurface(
                        normalized,
                        SpaDelivery.ExtractTitle(fullHtml),
                        region,
                        capturedSource));
                }
            }

            // The entry document embeds the dashboard's ALREADY-linkified content, so wrapping happens after
            // linkification and the linkifier never walks the shell's CSS/bridge-script text. The dashboard's
            // SourcePath is null (it aggregates many artifacts), so the reveal button paints hidden — the bridge
            // shows it only once an update swaps in a surface that carries a source (Story 6.10).
            var entry = surfaces[0];
            var entryDocument = WebviewRenderAdapter.Shared.WrapDocument(dashboardPage, entry.ContentHtml, entry.SourcePath);
            return new WebviewBundle(_options.SiteTitle, entry.OutputRelativePath, entryDocument, surfaces, outline);
        }
    }

    /// <summary>Repo-relative source <c>.md</c> for each captured long-tail page that has ONE obvious source —
    /// generic docs (<see cref="DocModel"/>, keyed by output path) and ADR pages (<see cref="AdrEntry"/>) — for
    /// the webview reveal-source affordance, joined host-side with the SAME one repo-relative convention the
    /// story surfaces use (Story 6.10). Aggregate pages (sprint, requirements index, about, diagnostics…) are
    /// simply absent → null <c>SourcePath</c> → the reveal button stays hidden, exactly like the dashboard.
    /// [spec-webview-doc-page-surfaces]</summary>
    private Dictionary<string, string> BuildCapturedSourceMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Never ship a sourcePath that escapes the repo root: RepoRelative degrades to an absolute path when
        // the source lives outside RepoRoot (known deferred quirk), and the host containment guard would
        // reject it anyway — omitting the entry (button hidden) is the honest degrade.
        void Add(string outputRelative, string sourceFullPath)
        {
            var rel = RepoRelative(sourceFullPath);
            if (Path.IsPathRooted(rel) || rel.StartsWith("..", StringComparison.Ordinal)) return;
            map[PathUtil.NormalizeSlashes(outputRelative)] = PathUtil.NormalizeSlashes(rel);
        }

        foreach (var doc in _docs.Values)
        {
            Add(doc.OutputRelativePath, Path.Combine(_options.SourceRoot, doc.SourceRelativePath));
        }
        var adrDisplayPrefix = ForgeOptions.AdrOutputSubdir + "/";
        foreach (var adr in _adrs)
        {
            // AdrEntry.SourceRelativePath is DISPLAY-prefixed with the output subdir ("adrs/<file>.md" — see
            // GenerateAdrsInternal); the real file lives at AdrSourceRoot + the un-prefixed remainder.
            var rel = adr.SourceRelativePath.StartsWith(adrDisplayPrefix, StringComparison.OrdinalIgnoreCase)
                ? adr.SourceRelativePath[adrDisplayPrefix.Length..]
                : adr.SourceRelativePath;
            Add(adr.OutputRelativePath, Path.Combine(_options.AdrSourceRoot, rel));
        }
        // The repo README renders straight from ReadmeSourcePath (never via _docs) — the spec's first-named
        // page shouldn't be the one surface missing its reveal affordance.
        if (ReadmeAvailable)
        {
            Add(SiteNav.ReadmeOutputPath, ReadmeSourcePath);
        }
        return map;
    }

    /// <summary>Tallies the status-bar summary from the assembled outline — stories by stage across all epics,
    /// computed core-side so the shim does no counting (Story 6.9, R3.2). Routes every count through the stage
    /// strings <see cref="StatusStyles.ForStory"/> already produced (carried on each <see cref="OutlineStory"/>),
    /// so it can never disagree with the tree's per-node icons.</summary>
    private static OutlineSummary BuildOutlineSummary(IReadOnlyList<OutlineEpic> epics)
    {
        var stages = epics.SelectMany(e => e.Stories).Select(s => s.Stage).ToList();
        return new OutlineSummary(
            Active: stages.Count(s => s == "active"),
            Review: stages.Count(s => s == "review"),
            Done: stages.Count(s => s == "done"),
            Total: stages.Count);
    }

    /// <summary>Renders one page's webview content region and reference-linkifies it with the same skip rules
    /// the HTML surface uses (a page never self-links its own story/epic mentions). <paramref name="sourcePath"/>
    /// is the repo-relative artifact the surface was rendered from (the Story 6.10 reveal-source target), or null
    /// for a source-less surface (the dashboard). [Story 6.4]</summary>
    private WebviewSurface WebviewSurfaceFor(PageView page, string? sourcePath = null, string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var content = ApplyReferenceLinks(
            WebviewRenderAdapter.Shared.RenderContent(page), page.OutputRelativePath,
            skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber);
        return new WebviewSurface(PathUtil.NormalizeSlashes(page.OutputRelativePath), page.Title, content, sourcePath);
    }

    // ===== Story 6.7: JSON + client-renderer (SPA) delivery form =============================================

    /// <summary>The page write seam for pages the SPA form consolidates: writes <paramref name="html"/> to
    /// <paramref name="outputRelativePath"/> under the output root (creating parent dirs), and — ONLY when
    /// <c>--spa</c> is active — captures the finished page string in memory (<see cref="_spaCapture"/>) for the SPA
    /// bundle. The capture consumes the render pipeline's OWN output at the instant it is produced, one step before
    /// it becomes a file: it never reads a generated <c>.html</c> back off disk and never re-parses a source
    /// <c>.md</c>, so AD-1/AD-2 hold (Story 6.7 Dev Notes "Is landmark-slicing scraping?"). The dashboard/epics
    /// families deliberately do NOT route through here — the SPA re-renders them from their view models (strongest
    /// parity, see <see cref="BuildSpaBundle"/>). Bytes written are identical to the prior direct
    /// <c>File.WriteAllText</c>, so the golden gate is unaffected (AC #5). [Story 6.7]</summary>
    private void WriteOutput(string outputRelativePath, string html)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(full, html);
        if (_spaCapture is not null)
        {
            _spaCapture[PathUtil.NormalizeSlashes(outputRelativePath)] = html;
        }
    }

    /// <summary>Builds the whole-site SPA bundle: the five dashboard/epics families rendered through their view
    /// models (the same strongest-parity path the webview uses), plus EVERY other page's content region sliced from
    /// the render output captured at the write seam (<see cref="_spaCapture"/>) via the universal
    /// <c>&lt;main id="main-content"&gt;</c> landmark. Requires a completed capture pass (only populated under
    /// <c>--spa</c>). A pure read of cached state + captured strings — it writes nothing (AC #6). [Story 6.7]</summary>
    public SpaBundle RenderSpaBundle()
    {
        lock (_gate)
        {
            var nav = _nav ?? throw new InvalidOperationException(
                "RenderSpaBundle requires a completed GenerateAll() pass with --spa on this generator.");
            return BuildSpaBundle(nav);
        }
    }

    private SpaBundle BuildSpaBundle(SiteNav nav)
    {
        var pages = new List<SpaPage>();
        var familyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Dashboard + epics families via their view models — true view-model renders, so section-fact parity is
        // airtight (AC #4). Mirrors RenderWebviewSurfaces' iteration exactly (same models, retro map, placeholder
        // rule, fragment pipeline).
        var docs = _docs.Values.ToList();
        var work = WorkInventory.Build(docs);
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, work, _epicsModel);
        var followUps = FollowUpGeometry.From(
            _sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
            counts,
            work);
        var dashboardPage = HtmlTemplater.BuildIndexPage(
            docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands,
            work, _sprint, _retros, _coverage, _timelinePath is not null, counts: counts);
        AddSpaSurface(pages, familyPaths, dashboardPage);

        if (_epicsModel is { } model && _progress is { } progress)
        {
            var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
            AddSpaSurface(pages, familyPaths, EpicsTemplater.BuildIndexPage(model, progress, nav, _module.Commands, counts, followUps));

            foreach (var epic in model.Epics)
            {
                var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
                AddSpaSurface(pages, familyPaths,
                    EpicsTemplater.BuildEpicPage(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath, EpicPager(model, epic), followUps),
                    skipEpicNumber: epic.Number);

                foreach (var story in epic.Stories)
                {
                    if (story.ArtifactOutputPath is null || !_storyArtifactsById.TryGetValue(story.Id, out var artifactFullPath))
                    {
                        AddSpaSurface(pages, familyPaths,
                            EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath, StoryPager(model, story)),
                            skipStoryId: story.Id);
                        continue;
                    }

                    var f = BuildStoryPageFragments(story, artifactFullPath, _referenceMap);
                    AddSpaSurface(pages, familyPaths,
                        EpicsTemplater.BuildStoryPage(
                            epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria,
                            f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, f.Evidence, f.ChangeSurface, nav,
                            _module.Commands, epicRetroPath, StoryPager(model, story)),
                        skipStoryId: story.Id);
                }
            }
        }

        // 2) Every OTHER captured page: slice its content region via the landmark. Families are already covered
        // above (skipped here). The nav is re-rendered fresh (byte-identical to the page's own, minus the inline
        // toggle script the client owns); the breadcrumb + <main> come from the page's own captured output. The
        // breadcrumb is ALSO recovered structurally (from that same captured string — never re-read from disk)
        // so the manifest's drill parent/child data covers the whole site, not just the 5 view-model families.
        if (_spaCapture is { } capture)
        {
            foreach (var (path, fullHtml) in capture)
            {
                var normalized = PathUtil.NormalizeSlashes(path);
                if (familyPaths.Contains(normalized)) continue;
                var navMarkup = HtmlRenderAdapter.Shared.RenderNavMarkup(nav.ToNavigationView(normalized));
                var region = SpaDelivery.ExtractContentRegion(fullHtml, navMarkup);
                var breadcrumb = SpaDelivery.ExtractBreadcrumb(fullHtml, normalized);
                pages.Add(new SpaPage(normalized, SpaDelivery.ExtractTitle(fullHtml), region, breadcrumb));
            }
        }

        return new SpaBundle(_options.SiteTitle, "index.html", nav.Items, pages);
    }

    /// <summary>Renders one dashboard/epics family page's SPA content region (nav + breadcrumb + body) through
    /// <see cref="JsonSpaRenderAdapter"/>, reference-linkified with the SAME skip rules the static page uses (a page
    /// never self-links its own story/epic mentions), records the path as a family (so the landmark-slice pass skips
    /// it), and appends it to the bundle. [Story 6.7]</summary>
    private void AddSpaSurface(List<SpaPage> pages, HashSet<string> familyPaths, PageView page,
        string? skipStoryId = null, int? skipEpicNumber = null)
    {
        var region = ApplyReferenceLinks(
            JsonSpaRenderAdapter.Shared.RenderContent(page), page.OutputRelativePath,
            skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber);
        var path = PathUtil.NormalizeSlashes(page.OutputRelativePath);
        familyPaths.Add(path);
        pages.Add(new SpaPage(path, page.Title, region, page.Breadcrumb.Crumbs));
    }

    /// <summary>Writes the opt-in SPA delivery files — the client script, the manifest + content chunks, and the
    /// entry shell — ALL under <see cref="ForgeOptions.OutputRoot"/>, strictly alongside the untouched static site
    /// (AC #3/#5/#6). Called at the end of a full generate and after each incremental watch update when <c>--spa</c>
    /// is on. Never touches a source artifact or an existing static page. [Story 6.7]</summary>
    private void EmitSpaSite(SiteNav nav)
    {
        var bundle = BuildSpaBundle(nav);
        var dataFiles = SpaDelivery.BuildDataFiles(bundle);

        // Guard against a real doc's output path colliding with one of the SPA form's own reserved paths (e.g. a
        // doc slug that happens to render to "app.html" or "spa/pages-root.json"): writing anyway would silently
        // overwrite either the legitimate static page or the SPA's own delivery file with no diagnostic. Loud and
        // early beats a corrupted output tree. [Story 6.7 review]
        var reservedPaths = dataFiles.Select(f => f.OutputRelativePath)
            .Append(SpaDelivery.ScriptName)
            .Append(SpaDelivery.EntryFileName);
        var bundlePaths = new HashSet<string>(bundle.Pages.Select(p => p.OutputRelativePath), StringComparer.OrdinalIgnoreCase);
        var collision = reservedPaths.FirstOrDefault(bundlePaths.Contains);
        if (collision is not null)
        {
            throw new InvalidOperationException(
                $"--spa cannot be emitted: a generated page already claims the reserved SPA output path "
                + $"'{collision}'. Rename the conflicting source artifact so its output path no longer collides "
                + "with the SPA delivery form's own files (app.html, specscribe-spa.js, spa/*.json).");
        }

        // The client renderer, embedded and copied exactly like specscribe.css/js.
        CopyEmbeddedAsset("SpecScribe.assets.specscribe-spa.js", SpaDelivery.ScriptName);

        // Manifest + bounded content chunks.
        foreach (var file in dataFiles)
        {
            WriteSpaFile(file.OutputRelativePath, file.Content);
        }

        // The entry shell inlines the dashboard region for instant first paint AND the no-JS fallback (AC #2).
        var entryRegion = bundle.Pages.First(p => p.OutputRelativePath == bundle.EntryPath).ContentHtml;
        WriteSpaFile(SpaDelivery.EntryFileName, SpaDelivery.BuildEntryShell(bundle.SiteTitle, entryRegion));
    }

    /// <summary>Writes one SPA output file under the output root (creating parent dirs). Unlike
    /// <see cref="WriteOutput"/> this never captures — SPA files are the delivery output, not site pages to
    /// consolidate. [Story 6.7]</summary>
    private void WriteSpaFile(string outputRelativePath, string content)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
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

            WriteOutput(outputRelative, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), outputRelative));

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
        // Incremental paths may call WriteIndex without going through GenerateAll's ledger build — rebuild then.
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel);
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands, inventory, _sprint, _retros, _coverage, _timelinePath is not null, CodeItemHref, counts);
        File.WriteAllText(indexPath, ApplyReferenceLinks(html, "index.html"));
    }

    /// <summary>The projection-side enrichment handed to the adapter (see <see cref="ProgressProjection"/>):
    /// task/story roll-ups plus the git pulse, computed on the freshly ingested epics model. Kept in the
    /// generator — never the adapter — so insight enrichment stays additive and non-blocking (AD-4).
    /// Git is invoked once per ingest, not per-page. [Story 4.1]</summary>
    private ProgressModel ComputeProgress(EpicsModel model, IReadOnlyDictionary<string, string> artifactsById)
    {
        var gitPulse = GitMetrics.TryCompute(_options.RepoRoot);
        // Deep git analytics are strictly opt-in: when the flag is off this ternary short-circuits so
        // TryComputeDeep — and its extra git process — never runs, and baseline generation timing cannot
        // regress. That gate IS the FR-10 performance guarantee (AC #1). [Story 3.2]
        var deepGit = _options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(_options.RepoRoot) : null;
        return ProgressCalculator.Compute(model, artifactsById, gitPulse, deepGit);
    }

    /// <summary>Surfaces adapter diagnostics on the existing event/reporter channel: malformed artifacts and
    /// ingest errors report as <see cref="GenerationOutcome.Error"/> (exactly how per-file parse failures
    /// always reported), unsupported/skipped shapes as <see cref="GenerationOutcome.Skipped"/>. Always
    /// non-fatal — the run has already continued past whatever these describe (AC #2). [Story 4.1]
    /// <para>The message is prefixed with the fine <see cref="AdapterDiagnosticCategory"/> word (e.g.
    /// <c>[Unsupported]</c>) so the coarse <see cref="GenerationOutcome"/> collapse (four categories → two
    /// outcomes) doesn't lose the distinction the Story 4.8 diagnostics page shows. Additive and harmless on
    /// the console path (which already prints messages); recovered by
    /// <see cref="DiagnosticsTemplater"/> without needing a second channel. [Story 4.8 Task 2]</para></summary>
    /// <param name="fromAdr">True when <paramref name="diagnostics"/>' <see cref="AdapterDiagnostic.RelativePath"/>
    /// is relative to the ADR output subdir / <c>AdrSourceRoot</c> rather than the source root (the
    /// unnumbered-ADR notice) — carried onto the resulting <see cref="GenerationEvent.FromAdrDiagnostic"/> so
    /// <see cref="DiagnosticNotice.FromEvents"/> anchors it to the right root. [Story 6.12] [Review][Patch]</param>
    private static IEnumerable<GenerationEvent> MapDiagnostics(IReadOnlyList<AdapterDiagnostic> diagnostics, bool fromAdr = false) =>
        diagnostics.Select(d => new GenerationEvent(
            d.Category is AdapterDiagnosticCategory.Malformed or AdapterDiagnosticCategory.Error
                ? GenerationOutcome.Error
                : GenerationOutcome.Skipped,
            d.RelativePath, TimeSpan.Zero, $"[{d.Category}] {d.Message}", FromAdapterDiagnostic: true, FromAdrDiagnostic: fromAdr));

    /// <summary>One <see cref="AdapterDiagnosticCategory.Unsupported"/> notice per top-level source folder
    /// outside the well-known home-index set — the "unrecognized structure degrades, visibly" half of the
    /// grouping contract. Derived from the source tree (not the rendered bands) so a folder whose docs were
    /// all consumed into dedicated surfaces still reports its unrecognized shape once. A top-level folder
    /// whose files are ENTIRELY under a nested implementation-artifacts segment (e.g.
    /// <c>tracking/implementation-artifacts/1-1-x.md</c>) is excluded: those docs land in the known
    /// Implementation Artifacts band (see <see cref="HtmlTemplater.RenderIndex"/>'s ancestor-tolerant match),
    /// so flagging the wrapper folder as "unrecognized" would contradict Task 4's location tolerance and the
    /// notice's own claim that the docs "render in their own home-index section." [Story 4.2 Task 5] [Review][Patch]</summary>
    private static IReadOnlyList<AdapterDiagnostic> UnrecognizedTopLevelFolders(IReadOnlyList<string> sourceRelatives)
    {
        var normalized = sourceRelatives.Select(PathUtil.NormalizeSlashes).ToList();
        return normalized
            .Where(p => p.Contains('/'))
            .Select(p => p[..p.IndexOf('/')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !HtmlTemplater.IsWellKnownTopLevelFolder(f))
            .Where(f => !normalized
                .Where(p => p.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase))
                .All(BmadArtifactAdapter.IsUnderImplementationArtifacts))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported, f + "/",
                "unrecognized top-level folder; its documents render in their own home-index section"))
            .ToList();
    }

    /// <summary>Caches the adapter-ingested retros (already ordered by epic, then filename) so
    /// <see cref="EpicRetroMap"/> is available to the epic/story pages and the sprint/home surfaces. The
    /// dedicated pages are written later by <see cref="RenderRetroPages"/>. [Story 2.3 retro pages; ingest
    /// moved behind the adapter in Story 4.1]</summary>
    private void SetRetros(IReadOnlyList<RetroModel> retros)
    {
        _retros = retros.ToList();
        // Computed once here rather than on every access (EpicRetroMap used to be a computed property
        // re-grouping all retros on every epic in the epics render loop). [Story 2.3 review]
        _epicRetroMap = _retros.GroupBy(r => r.EpicNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SourceRelativePath, StringComparer.OrdinalIgnoreCase).First().OutputRelativePath);
    }

    /// <summary>Stamps each epic's <see cref="EpicInfo.HasRetrospective"/> from <see cref="EpicRetroMap"/> — the
    /// SAME signal the epic/story pages' retro link reads — so the retro-gated "In review" tier
    /// (<see cref="StatusStyles.ForEpicWithRetrospective"/>) and those links can never disagree. Called after the
    /// epics model is (re)cached in BOTH <see cref="GenerateAll"/> and the incremental <see cref="RegenerateEpics"/>,
    /// so a watch-mode rebuild doesn't reset the flag to false and wrongly downgrade a retro'd epic to "In review".
    /// [spec-sunburst-retro]</summary>
    private void TagEpicRetrospectives()
    {
        if (_epicsModel is null) return;
        foreach (var epic in _epicsModel.Epics)
        {
            epic.HasRetrospective = _epicRetroMap.ContainsKey(epic.Number);
        }
    }

    /// <summary>Writes each parsed retrospective into its dedicated <see cref="RetroTemplater"/> page (at the
    /// same <c>implementation-artifacts/…html</c> path the generic pipeline would have used, so existing links
    /// resolve), reference-linkified like every page. Runs after the epics phase — the page needs the epics
    /// model for its epic link and "Stories in this Epic" section. [Story 2.3 retro pages]</summary>
    private void RenderRetroPages(SiteNav nav)
    {
        // Retros navigate in ascending epic order (Prev = lower epic, Next = higher); write order is unchanged. [Prev/next navigation]
        var ordered = _retros.OrderBy(r => r.EpicNumber).ToList();
        foreach (var retro in _retros)
        {
            var outputRel = retro.OutputRelativePath;
            var prefix = PathUtil.RelativePrefix(outputRel);
            var pager = EntityPager.FromSequence(ordered, ordered.FindIndex(r => ReferenceEquals(r, retro)),
                r => prefix + r.OutputRelativePath,
                r => r.Title);
            WriteOutput(outputRel, ApplyReferenceLinks(RetroTemplater.RenderPage(retro, _epicsModel, nav, pager), outputRel));
        }
    }

    /// <summary>Maps an epic number to the output path of its (latest, by filename) retrospective page — the
    /// link target for an open action item tagged with that epic. Computed once in <see cref="SetRetros"/>.
    /// [Story 2.3 retro pages]</summary>
    private IReadOnlyDictionary<int, string> _epicRetroMap = new Dictionary<int, string>();
    private IReadOnlyDictionary<int, string> EpicRetroMap => _epicRetroMap;

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
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, WorkInventory.Empty, _epicsModel);
        var html = SprintTemplater.RenderIndex(_sprint, _epicsModel, nav, _module.Commands, _retros, counts);
        WriteOutput(SiteNav.SprintOutputPath, ApplyReferenceLinks(html, SiteNav.SprintOutputPath));
    }

    /// <summary>Writes <c>structure.html</c> — the interactive project/artifact structure tree. Gated on the
    /// same source-artifact signal as its nav item (<c>sourceRelatives.Count &gt; 0</c>) so the page and its link
    /// are one signal; a project with no <c>_bmad-output</c> <c>*.md</c> files omits both. Builds the
    /// output-href map from the fully-populated <see cref="_docs"/> (plus the special <c>epics.md → epics.html</c>
    /// mapping) so navigable leaves route to real pages and non-navigable ones render as plain text — never a
    /// broken link. The whole gather+build is wrapped never-throw → <see cref="ProjectTree.Empty"/> so any failure
    /// degrades to omission and generation still succeeds (AD-4 / NFR2). [Story 3.4 Task 3]</summary>
    /// <summary>Writes <c>code-map.html</c> — the source-code treemap (Story 7.6). Builds the pure
    /// <see cref="CodeMap"/> over the cached source-code walk (<see cref="_codeFiles"/>) joined to the deep-git
    /// per-file metrics (<see cref="DeepGitPulse.CodeMapMetrics"/>, empty when <c>--deep-git</c> is off → sized-by-LOC
    /// with a neutral fill + the "git data unavailable" notice), computes the squarified layout, and renders. Gated
    /// on the same source-code signal as its nav item, so a Code Map link is never emitted to a page that wasn't
    /// produced. Wrapped never-throw → any failure degrades to "surface omitted, generation still succeeds"
    /// (AD-4 / NFR2), matching the old <c>WriteStructure</c> and every insight provider. Files route to their
    /// in-portal code page via the same guarded <see cref="CodeItemHref"/> resolver the deep-analytics and
    /// git-insights surfaces use. [Story 7.6]</summary>
    private void WriteCodeMap(SiteNav nav)
    {
        if (_codeFiles.Count == 0) return;

        IReadOnlyList<CodeMapVariant> variants;
        try
        {
            var metrics = _progress?.DeepGit?.CodeMapMetrics
                ?? (IReadOnlyDictionary<string, CodeFileMetrics>)new Dictionary<string, CodeFileMetrics>(StringComparer.Ordinal);
            variants = CodeMap.BuildVariants(_codeFiles, metrics);
        }
        catch (Exception)
        {
            variants = Array.Empty<CodeMapVariant>();
        }

        // Gate on the unfiltered ("full") variant only — the two exclude checkboxes are a view onto the same
        // underlying source walk, not a second independent surface, so they share one nav/page gate (AC parity
        // with every other insight provider's single-signal gate).
        var full = variants.FirstOrDefault(v => v.Key == "full");
        if (full is null || full.Map.IsEmpty) return;

        var html = CodeMapTemplater.RenderPage(variants, nav, fileHref: CodeItemHref);
        WriteOutput(SiteNav.CodeMapOutputPath, ApplyReferenceLinks(html, SiteNav.CodeMapOutputPath));
    }

    /// <summary>Writes the retrospectives index (<c>retros.html</c>) when any retro exists — the target of the
    /// sprint page's "Retros" link. [Story 2.3 polish #5]</summary>
    private void WriteRetroIndex(SiteNav nav)
    {
        if (_retros.Count == 0) return;
        var html = RetroTemplater.RenderIndex(_retros, nav);
        WriteOutput(SiteNav.RetrosOutputPath, ApplyReferenceLinks(html, SiteNav.RetrosOutputPath));
    }

    /// <summary>Writes the open-action-items page (<c>action-items.html</c>) when the sprint tracks open items —
    /// the target of the sprint page's flag button and the home retro callout. Each item links to its epic's
    /// retro page and offers a quick-dev "Resolve with AI" command. [Story 2.3 polish #5] [Story 9.6]</summary>
    private void WriteActionItems(SiteNav nav, WorkInventory? work = null)
    {
        var open = _sprint?.OpenActionItems;
        if (open is null || open.Count == 0) return;
        // Debt-related items link to the deferred-work backlog page when one exists (root-relative — this page
        // is at the site root). Reuses the caller's inventory when supplied instead of rebuilding it. [Story 2.3 review]
        var inventory = work ?? WorkInventory.Build(_docs.Values.ToList());
        var deferredHref = inventory.Deferred?.OutputPath;
        // NOT reference-linkified: the "Resolve with AI" data-copy payload embeds the action text (which can
        // contain "Epic N"/"Story N.M" mentions); the linkifier would wrap those in <a> tags INSIDE the
        // attribute value and corrupt the copyable command. Visible text is linkified inside the templater
        // only. [Story 2.3 polish #5; Story 9.6]
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel);
        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, _docs.Values);
        var html = ActionItemsTemplater.RenderPage(
            open, EpicRetroMap, _module.Commands, nav, deferredHref, counts, _epicsModel, hrefMap);
        WriteOutput(SiteNav.ActionItemsOutputPath, html);
    }

    /// <summary>Overwrites the deferred-work doc page with the structured card template when a
    /// <c>deferred-work.md</c> exists. Keeps the doc in <c>_docs</c> (home callout + open count) and the
    /// same output path (Story 10.1 Follow-ups nav). Last-write-wins on the SPA capture. [Story 9.6]</summary>
    private void WriteDeferredWork(SiteNav nav, WorkInventory? work = null)
    {
        var deferred = (work ?? WorkInventory.Build(_docs.Values.ToList())).Deferred;
        if (deferred is null) return;

        var doc = _docs.Values.FirstOrDefault(d =>
            string.Equals(
                PathUtil.NormalizeSlashes(d.OutputRelativePath),
                PathUtil.NormalizeSlashes(deferred.OutputPath),
                StringComparison.OrdinalIgnoreCase));
        if (doc is null) return;

        var sourceFull = Path.Combine(
            _options.SourceRoot,
            doc.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string? markdown = null;
        try
        {
            if (File.Exists(sourceFull))
                markdown = File.ReadAllText(sourceFull);
        }
        catch (IOException) { /* degrade to unstructured body */ }
        catch (UnauthorizedAccessException) { }

        var outputPath = PathUtil.NormalizeSlashes(deferred.OutputPath);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, _docs.Values);
        var model = DeferredWorkParser.Parse(markdown, hrefMap, prefix, doc.BodyHtml);
        var html = DeferredWorkTemplater.RenderPage(model, nav, outputPath, doc.Title);
        // Structured cards have no copyable command payload — ApplyReferenceLinks is safe here and
        // catches inline "Story N.M"/"Epic N"/"FR-N" mentions the parser doesn't own.
        WriteOutput(outputPath, ApplyReferenceLinks(html, outputPath));
    }

    /// <summary>Writes <c>diagnostics.html</c> — the whole-run report of the run's non-fatal notices plus the
    /// effective configuration + detection results (AC #1/#2). Projects the notice list off the single
    /// accumulated <paramref name="events"/> list (no double-count) and reads the config from already-resolved
    /// <see cref="_options"/>/<see cref="_module"/> (local-first — no I/O, no remote calls). Deliberately NOT
    /// run through <see cref="ApplyReferenceLinks"/>: an exception message can embed "Story N.M"/"FR-9"
    /// fragments the linkifier would wrap and distort — the same trap <see cref="WriteActionItems"/> avoids.
    /// Returns its own <see cref="GenerationOutcome.Generated"/> event for the run summary / output inventory.
    /// [Story 4.8 Task 6]</summary>
    private GenerationEvent WriteDiagnostics(SiteNav nav, IReadOnlyList<GenerationEvent> events)
    {
        var sw = Stopwatch.StartNew();
        var notices = DiagnosticNotice.FromEvents(events);
        var config = DiagnosticsConfig.FromRun(_options, _module);
        var html = DiagnosticsTemplater.RenderPage(notices, config, nav);
        WriteOutput(SiteNav.DiagnosticsOutputPath, html);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.DiagnosticsOutputPath, sw.Elapsed);
    }

    /// <summary>Writes <c>about.html</c> — SpecScribe's own product-metadata page (version/description/author/
    /// repository, read from the assembly) plus the prominent link to the diagnostics run log. Static (no run
    /// dependency); written alongside the diagnostics page on every full run so the footer's About link always
    /// resolves. Returns its own <see cref="GenerationOutcome.Generated"/> event. [Story 4.8 Task 6]</summary>
    private GenerationEvent WriteAbout(SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        var html = AboutTemplater.RenderPage(nav);
        WriteOutput(SiteNav.AboutOutputPath, html);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.AboutOutputPath, sw.Elapsed);
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
            WriteOutput(SiteNav.ReadmeOutputPath, ApplyReferenceLinks(HtmlTemplater.RenderPage(doc, nav), SiteNav.ReadmeOutputPath));
            return new GenerationEvent(GenerationOutcome.Generated, "README.md", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, "README.md", sw.Elapsed, ex.Message);
        }
    }

    /// <summary>Writes requirements.html plus one detail page per FR/NFR. Each page is linkified against the
    /// requirement set (the detail page skips its own id so it never self-links).</summary>
    private void WriteRequirements(
        RequirementsModel requirements, EpicsModel model, ProgressModel progress, SiteNav nav, WorkInventory? work = null)
    {
        WriteOutput("requirements.html",
            ApplyReferenceLinks(RequirementsTemplater.RenderIndex(requirements, model, progress, nav), "requirements.html"));

        var requirementsDir = Path.Combine(_options.OutputRoot, "requirements");
        Directory.CreateDirectory(requirementsDir);

        // Deferral-source inputs for a deferred requirement's best-effort link (AC #2): the SAME epic→retro map
        // (EpicRetroMap) and deferred-work-page href already resolved for WriteActionItems / follow-up geometry —
        // prefer a caller-supplied WorkInventory (GenerateAll / RegenerateEpics) and only Build when absent
        // (same optional-work pattern as WriteActionItems). Both hrefs are output-root-relative; RenderRequirement
        // prefixes them to the detail page's depth. [Story 9.3 Task 5; review]
        var deferredWorkHref = (work ?? WorkInventory.Build(_docs.Values.ToList())).Deferred?.OutputPath;

        // Everything (FR+NFR+Design) so UX-DR detail pages generate alongside FR/NFR. [Story 9.2 Task 5]
        foreach (var req in requirements.Everything)
        {
            var outputRelative = $"requirements/{req.Slug}.html";
            var html = RequirementsTemplater.RenderRequirement(req, progress, nav, model, EpicRetroMap, deferredWorkHref);
            WriteOutput(outputRelative, ApplyReferenceLinks(html, outputRelative, skipRequirementId: req.Id));
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
        // Story 7.2: resolve source-code citations + view-source links to Story 7.1's code pages. Runs AFTER the
        // FR/Story linkifiers and is anchor-aware, so it never touches the links they emitted; a no-op when there are
        // no code pages. Citations always resolve to the in-portal pages now (Story 7.7 made the external base
        // additive, not a replacement) — the code page itself carries the "view source online" link — so pass no
        // external base here: a configured/detected --code-url never diverts a citation from its in-portal page.
        html = CodeReferenceLinkifier.Linkify(
            html, _codePages, codeSourceBaseUrl: null, prefix, _options.RepoRoot, _options.SourceRoot);
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

    // Story 7.6: defensive upper bound on the source files the code-map walk sizes, so a pathological tree can't
    // turn the treemap into an unbounded job (NFR1). Far above any real repo's tracked-file count.
    private const int MaxCodeMapFiles = 25_000;

    /// <summary>The source-code walk for the treemap (Story 7.6): git-tracked files + their line counts,
    /// repo-relative + forward-slash. Prefers <c>git ls-files</c> (tracked files only → excludes <c>bin/</c>,
    /// <c>obj/</c>, <c>.git/</c>, <c>node_modules/</c>, and everything <c>.gitignore</c> covers, defining "the
    /// codebase" the way git does); falls back to a bounded directory walk with an explicit exclude list when git is
    /// unavailable / not a repo. Each file's line count is read with the SAME size cap + binary/UTF-8 guard the code
    /// pages use (<see cref="TryReadCodeText"/>) — binary/oversized/unreadable files have no meaningful LOC and are
    /// skipped. Bounded (<see cref="MaxCodeMapFiles"/>) and wrapped never-throw (NFR1/NFR2): any failure yields an
    /// empty list, so the whole surface omits and generation still succeeds. Runs once per full generation.</summary>
    private IReadOnlyList<(string RepoRelativePath, long Lines)> EnumerateCodeFiles()
    {
        try
        {
            var repoRoot = Path.GetFullPath(_options.RepoRoot);
            var relatives = GitMetrics.TryListFiles(repoRoot) ?? FallbackCodeWalk(repoRoot);

            var result = new List<(string, long)>();
            foreach (var rel in relatives)
            {
                if (result.Count >= MaxCodeMapFiles) break;
                if (string.IsNullOrWhiteSpace(rel)) continue;

                var normalized = PathUtil.NormalizeSlashes(rel);
                string full;
                try
                {
                    full = Path.GetFullPath(Path.Combine(repoRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch
                {
                    continue;
                }

                // Defense in depth: a stray "../" in the list must never escape the repo root.
                if (!full.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    if (!File.Exists(full)) continue;
                    if (new FileInfo(full).Length > MaxCodeFileBytes) continue;   // oversized → no meaningful LOC
                    if (!TryReadCodeText(full, out var text)) continue;           // binary / unreadable → skip
                    result.Add((normalized, SplitCodeLines(text).Count));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A file that vanished mid-walk or is locked — skip it, never fail the whole walk.
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<(string, long)>();
        }
    }

    /// <summary>A bounded, git-free fallback source walk for the code-map when the repo isn't a git checkout: a
    /// depth-first enumeration under the repo root, skipping dot-directories (<c>.git</c>, <c>.vs</c>,
    /// <c>.claude</c>…) and the usual build/dependency dirs (<c>bin</c>, <c>obj</c>, <c>node_modules</c>), and the
    /// shared editor-temp/dotfile ignore set. Capped at <see cref="MaxCodeMapFiles"/> so it can't run away on a huge
    /// tree. Returns repo-relative, forward-slash paths. [Story 7.6]</summary>
    private static IReadOnlyList<string> FallbackCodeWalk(string repoRoot)
    {
        if (!Directory.Exists(repoRoot)) return Array.Empty<string>();

        var results = new List<string>();
        var stack = new Stack<string>();
        stack.Push(repoRoot);
        while (stack.Count > 0 && results.Count < MaxCodeMapFiles)
        {
            var dir = stack.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(dir);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (results.Count >= MaxCodeMapFiles) break;
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules") continue;
                    stack.Push(entry);
                }
                else if (!PathUtil.IsIgnoredSourceFile(entry))
                {
                    results.Add(PathUtil.NormalizeSlashes(Path.GetRelativePath(repoRoot, entry)));
                }
            }
        }

        return results;
    }

    /// <summary>Markdown files in the resolved ADR root, plus ONE level of subdirectories — enough for nested
    /// year/topic schemes (<c>decisions/2024/0007-x.md</c>) without walking a whole tree of unrelated prose
    /// into the ADR section (deliberately bounded — the same window the ForgeOptions probe reads). [Story 4.2 Task 1]</summary>
    private List<string> EnumerateAdrFiles() =>
        Directory.Exists(_options.AdrSourceRoot)
            ? Directory.EnumerateFiles(_options.AdrSourceRoot, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateDirectories(_options.AdrSourceRoot)
                    .SelectMany(d => Directory.EnumerateFiles(d, "*.md", SearchOption.TopDirectoryOnly)))
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
        // CodeMapAvailable = the cached source-code walk is non-empty — the SINGLE signal that gates both the nav
        // item/quick link here and the WriteCodeMap page write, so a Code Map link is never emitted to a page that
        // wasn't produced. The incremental watch paths that call BuildNav reuse the last full run's _codeFiles (the
        // treemap only regenerates on a full rebuild). [Story 7.6 Subtask 3.4]
        return SiteNav.Build(sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable, hasCodeMap: _codeFiles.Count > 0);
    }

    private static readonly IReadOnlyDictionary<string, DateOnly> EmptyDates = new Dictionary<string, DateOnly>();

    // Story 7.3: shared empty maps for the date-page / timeline paths (git-absent, artifact-absent branches).
    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> EmptyArtifactsByDay
        = new Dictionary<DateOnly, IReadOnlyList<(string Label, string Href)>>();
    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> EmptyCommitsByDay
        = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>();

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
    /// enumeration — scanned separately, like the adapter's sprint-file discovery), reads each one's
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

    /// <summary>The nav/section gate: any renderable RECORD present (numbered or not), rather than the old
    /// "any numbered file" — an ADR home holding only non-standard-named records still surfaces its section
    /// (AC #2), while a README-only (or template-only) directory still gates the section off. [Story 4.2 Task 2]</summary>
    private bool AdrsExist() => EnumerateAdrFiles()
        .Any(f => IsAdrRecordFile(PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.AdrSourceRoot, f))));

    private static int? ParseAdrNumber(string fileName)
    {
        var m = AdrNumberPattern.Match(fileName);
        return m.Success && int.TryParse(m.Groups["num"].Value, out var n) ? n : null;
    }

    /// <summary>Derives an ADR's status tolerantly, first derivable value wins: (a) the "**Status:** …" bold
    /// line, (b) a MADR-style "## Status" section's first non-blank line, (c) a <c>status:</c> frontmatter
    /// key. Markdown links flatten to plain text (e.g. "Superseded by [0002](0002-x.md)" → "Superseded by
    /// 0002") and wrapping bold markers are stripped, for the index card. Not derivable ⇒ null — the record
    /// still renders, just without a status badge (AC #2). The raw status STRING renders as-is; mapping status
    /// vocabulary to the canonical model is Story 8.1's domain, not this method's. [Story 4.2 Task 2]</summary>
    private static string? ExtractAdrStatus(string raw, Frontmatter frontmatter)
    {
        var bold = AdrStatusPattern.Match(raw);
        if (bold.Success && CleanAdrStatus(bold.Groups["status"].Value) is { } fromBoldLine)
        {
            return fromBoldLine;
        }

        var heading = AdrStatusHeadingPattern.Match(raw);
        if (heading.Success)
        {
            foreach (var line in raw[(heading.Index + heading.Length)..].Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith('#')) break; // empty Status section — fall through to frontmatter
                if (IsDecorativeLine(trimmed)) continue; // e.g. a "---" rule right under the heading
                if (CleanAdrStatus(trimmed) is { } fromHeading) return fromHeading;
                break;
            }
        }

        return frontmatter.Status is { Length: > 0 } fromFrontmatter ? CleanAdrStatus(fromFrontmatter) : null;
    }

    /// <summary>The ADR's decision date, tolerantly extracted (Story 10.4), mirroring <see cref="ExtractAdrStatus"/>'s
    /// three-shape tolerance: a "**Date:**" bold line (what all the real ADRs use), a "## Date" MADR heading, then
    /// frontmatter <c>Date</c>. Parsed invariantly against a small set of common shapes; <c>null</c> when absent or
    /// unparseable so the card shows no date rather than a wrong one.</summary>
    private static DateOnly? ExtractAdrDate(string raw, Frontmatter frontmatter)
    {
        var bold = AdrDatePattern.Match(raw);
        if (bold.Success && TryParseAdrDate(bold.Groups["date"].Value) is { } fromBoldLine)
        {
            return fromBoldLine;
        }

        var heading = AdrDateHeadingPattern.Match(raw);
        if (heading.Success)
        {
            // Scan every line within the section (stop only at the next heading) rather than giving up after the
            // first substantive line — a "## Date" section that opens with intro prose before the actual date
            // would otherwise be missed.
            foreach (var line in raw[(heading.Index + heading.Length)..].Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith('#')) break;
                if (IsDecorativeLine(trimmed)) continue;
                if (TryParseAdrDate(trimmed) is { } fromHeading) return fromHeading;
            }
        }

        return frontmatter.Date is { Length: > 0 } fromFrontmatter ? TryParseAdrDate(fromFrontmatter) : null;
    }

    /// <summary>Tolerant date parse for the ADR date shapes, routed through the SINGLE <see cref="PortalDates.TryParseDay"/>
    /// tolerance so the ADR path can't diverge from the retro/doc path (Story 10.4 review). Strips wrapping bold/backtick
    /// markers, then also tries the string with a trailing parenthetical/semicolon note removed ("2026-07-10 (ratified …)"
    /// or "July 10, 2026 (ratified)") — never splitting on '-', which is part of the ISO date. <c>null</c> when none match.</summary>
    private static DateOnly? TryParseAdrDate(string value)
    {
        var cleaned = value.Trim().Trim('*', '`', ' ');
        var tail = cleaned.IndexOfAny(new[] { '(', ';' });
        var head = (tail >= 0 ? cleaned[..tail] : cleaned).Trim();
        foreach (var candidate in new[] { cleaned, head })
        {
            if (PortalDates.TryParseDay(candidate, out var parsed)) return parsed;
        }
        return null;
    }

    /// <summary>A one-line ADR summary (Story 10.4): the first prose paragraph under "## Context" (the shape all the
    /// real ADRs share), tag-/markdown-stripped and collapsed to a single line; falling back to the H1 title's
    /// post-em-dash tail when there is no Context prose; <c>null</c> when neither exists (card shows title + date
    /// only, never an empty line). Truncated so a long paragraph stays a one-liner.</summary>
    private static string? ExtractAdrSummary(string raw, string title)
    {
        var heading = AdrContextHeadingPattern.Match(raw);
        if (heading.Success)
        {
            var paragraph = new StringBuilder();
            foreach (var line in raw[(heading.Index + heading.Length)..].Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    if (paragraph.Length > 0) break; // end of the first paragraph
                    continue; // skip blank lines between the heading and the prose
                }
                if (trimmed.StartsWith('#')) break; // hit the next section before any prose
                if (IsDecorativeLine(trimmed)) continue;
                if (paragraph.Length > 0) paragraph.Append(' ');
                paragraph.Append(trimmed);
            }
            if (CollapseSummary(paragraph.ToString()) is { Length: > 0 } fromContext) return fromContext;
        }

        // Fallback: the descriptive tail some ADR titles carry after a dash ("ADR 0006: … — JSON + SPA + npx …").
        // Accept an em-dash, an en-dash, or a spaced ASCII hyphen so hyphenated titles get a summary too. Uses the
        // LAST dash occurrence, not the first — a title can carry an earlier dash that isn't the title/description
        // separator (e.g. a numeric range like "2020–2025"), and the intended separator is always the final one.
        var dashIndex = title.LastIndexOfAny(new[] { '—', '–' });
        if (dashIndex < 0)
        {
            var spaced = title.LastIndexOf(" - ", StringComparison.Ordinal);
            if (spaced >= 0) dashIndex = spaced + 1; // point at the hyphen so the +1 below skips it
        }
        if (dashIndex >= 0 && CollapseSummary(title[(dashIndex + 1)..]) is { Length: > 0 } fromTitle) return fromTitle;

        return null;
    }

    /// <summary>Strips markdown links/bold/backticks/leading structural markers and any HTML tags from a paragraph,
    /// collapses interior whitespace, and truncates to a single readable line for the ADR card. Empty ⇒ empty (caller
    /// treats as absent).</summary>
    private static string CollapseSummary(string text)
    {
        var noLinks = MarkdownLinkPattern.Replace(text, "${text}");
        var plain = PathUtil.StripHtmlTags(noLinks).Replace("*", string.Empty).Replace("`", string.Empty);
        var collapsed = Regex.Replace(plain, @"\s+", " ").Trim();
        // Strip a leading list/quote/table/image marker so a Context that opens with "- "/"> "/"| "/"1. "/"!" doesn't
        // leak the marker into the card summary.
        collapsed = Regex.Replace(collapsed, @"^(?:[-*+>|!]\s*|\d+\.\s+)+", string.Empty).Trim();
        const int max = 160;
        if (collapsed.Length <= max) return collapsed;
        var cut = max - 1;
        if (char.IsHighSurrogate(collapsed[cut - 1])) cut--; // don't split an astral char (emoji) across the ellipsis
        return collapsed[..cut].TrimEnd() + "…";
    }

    /// <summary>True for a line made up solely of a single repeated markdown rule/separator character (e.g.
    /// <c>---</c>, <c>***</c>, <c>___</c>) — never a genuine status value, so the MADR heading scan skips
    /// past it rather than capturing it verbatim. [Review][Patch]</summary>
    private static bool IsDecorativeLine(string trimmed) =>
        trimmed.Length >= 3 && trimmed.Distinct().Count() == 1 && "-=*_".Contains(trimmed[0]);

    private static string? CleanAdrStatus(string value)
    {
        var status = MarkdownLinkPattern.Replace(value, "${text}").Trim().Trim('*', '_').Trim();
        return status.Length == 0 ? null : status;
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

    // The predicate itself moved to PathUtil so the framework adapters share it (ignored files are neither
    // rendered nor reported as unsupported, wherever discovery happens). [Story 4.1]
    private static bool IsIgnored(string path) => PathUtil.IsIgnoredSourceFile(path);
}
