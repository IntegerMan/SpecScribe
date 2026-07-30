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
public sealed record GenerationEvent(GenerationOutcome Outcome, string RelativePath, TimeSpan Elapsed, string? Message = null, bool FromAdapterDiagnostic = false, bool FromAdrDiagnostic = false, DiagnosticAnchorRoot? DiagnosticAnchor = null);

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

    /// <summary>Story ids whose artifact became unreadable during <see cref="BuildFamilySurfaces"/> and were
    /// degraded to a placeholder in the IR while the static page kept its full content. Populated by that
    /// catch, drained into ONE <see cref="GenerationEvent"/> after the emit. Exists because the degrade was
    /// silent before the Story 22.4 code review: it forks the IR from the static HTML at <c>errors=0</c>, and
    /// Story 23.4 renders the site from the IR.</summary>
    private readonly List<string> _familyDegradedStories = new();

    /// <summary>The one <see cref="SurfacePrelude"/> instance both surface consumers share within a single
    /// generator operation. Null between operations. See <see cref="BuildSurfacePrelude"/> for the lifetime
    /// rule and why this is not a <c>Lazy&lt;&gt;</c>. [Story 22.4 code review]</summary>
    private SurfacePrelude? _prelude;

    // The ingestion seam (AD-1): all framework-specific discovery/parsing lives behind this adapter, so the
    // generator only ever consumes the normalized ArtifactBundle. Held as the concrete type (not
    // IArtifactAdapter) because the watch paths also need its scoped epics re-ingest; the adapter registry
    // that selects among IArtifactAdapter implementations arrives with Stories 4.3+. [Story 4.1]
    private readonly BmadArtifactAdapter _adapter = new();

    private SiteNav? _nav;
    private ModuleContext _module = ModuleContext.None;
    private EpicsModel? _epicsModel;
    private ProgressModel? _progress;

    /// <summary>The ONE calendar day this pass treats as "today" for the date-page cutoff, resolved from
    /// <see cref="ForgeOptions.DateCutoff"/> by <see cref="Charts.ResolveToday"/> and shared by every consumer: the
    /// linked-day set (<see cref="Charts.LinkedCommitDays"/>), the generated <c>commits/{date}.html</c> set, the
    /// artifact-by-day future-skew guard, the heatmap grid extent, and every guarded date link.
    /// <para>A field rather than five independent resolutions BY DESIGN. <see cref="Charts.LinkedCommitDays"/>'s
    /// guarantee — a linked cell always has a generated page, and vice versa — holds only while every consumer
    /// filters on the same value. Before Story 5.5 the sites agreed by accident (they all evaluated the same
    /// <c>DateTime.Now</c> expression); once "today" is a POLICY, independent re-resolution becomes a real drift
    /// hazard: a <c>Utc</c> midnight crossed mid-run, or a re-read series under <c>LastCommit</c>, would produce a
    /// dead link or an orphaned page. Resolve once, thread everywhere. [Story 5.5]</para></summary>
    private DateOnly _today;

    /// <summary>Re-resolves <see cref="_today"/> from the current policy and git series. Called exactly where
    /// <see cref="_progress"/> is (re)assigned — that is the moment git metrics become known, and it is BEFORE any
    /// consumer runs on every path. Within a single pass the value never moves.</summary>
    private void RefreshToday() => _today = Charts.ResolveToday(_options.DateCutoff, _progress?.Git?.DailySeries);

    private RequirementsModel? _requirements;
    private List<AdrEntry> _adrs = new();

    // The epic-scoped work graph (Story 19.2), projected once BEFORE nav.Build so the Insights "Work Graph" entry
    // and the page write share one gate (a non-empty model) — the link never dangles. Cached so the written page
    // is byte-identical to what the gate saw; watch-mode BuildNav reuses last full run's model like _progress.
    private WorkGraphModel _workGraph = WorkGraphModel.Empty;

    // Forge session workspaces (Story 18.4), discovered once BEFORE nav.Build so the Project "Ideas" entry, the
    // ideas.html write, and the detail pages all share one gate (a non-empty model) — the link never dangles.
    // Also consumed by BuildMemlogMap, which excludes these workspaces' memlogs from the coverage panel's
    // freshness enrichment (see that method for why). Watch-mode BuildNav reuses the last full run's model, like
    // _workGraph/_progress: forge activity is invisible to the watcher by design (see IsDataSource).
    private IdeasModel _ideas = IdeasModel.Empty;

    // Methodology-module test artifacts (Story 18.5), discovered once BEFORE nav.Build so the Delivery "Test
    // Artifacts" entry, the test-artifacts.html write and the dashboard's Module Coverage panel all share one gate
    // (a non-empty model) — the link never dangles. Discovery is gated on the module being INSTALLED
    // (ModuleContext.IsModulePresent on the code string "tea"), never on filenames, and it reads two JSON files the
    // *.md source list cannot reveal (ADR 0020). Watch-mode BuildNav reuses the last full run's model like
    // _workGraph/_ideas/_progress.
    private TestArtifactsModel _testArtifacts = TestArtifactsModel.Empty;

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
    // Ordinal (not IgnoreCase): matches the git path-key policy (Epic 8) so two case-differing files on a
    // case-sensitive checkout stay distinct instead of silently colliding. Windows citation case mismatches
    // degrade to unlink — same class as ProgressCalculator git-date misses. [spec-7-1-deferred-debt-cleanup]
    private Dictionary<string, string> _codePages = new(StringComparer.Ordinal);

    // Story 7.2: discovered ONCE up front (DiscoverCodeReferences), before any citing page is linkified, so
    // ApplyReferenceLinks can resolve citations against a populated _codePages on every page — including the
    // story/doc/ADR pages that render BEFORE the code pages themselves. _codeReferenced is the deterministic set
    // of repo-relative files that get an in-portal page (empty in external mode); _codeReverseMap is the
    // file -> citing artifacts back-map that drives each code page's "Referenced by" block (AC #2). Keyed on the
    // citing artifact's SOURCE-relative path (resolved to its output URL at render time, once _referenceMap exists).
    private List<string> _codeReferenced = new();
    private Dictionary<string, List<(string CitingSourceRelative, string Title)>> _codeReverseMap = new(StringComparer.Ordinal);

    // Forward map (citing artifact SOURCE-relative path -> the set of repo-relative code files it cites), built in
    // the SAME discovery pass as _codeReverseMap (no second scan) — lets the "Show relationships" reference-graph
    // toggle answer "does this citing story ALSO cite one of the center file's related files?" without re-reading
    // any artifact. [reference-graph epic grouping + relationships]
    private Dictionary<string, HashSet<string>> _citerToFiles = new(StringComparer.Ordinal);

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

    /// <summary>Story 22.5: every renderable source path the last completed pass actually saw — BOTH watched
    /// roots, since ADRs live outside <see cref="ForgeOptions.SourceRoot"/> and strand their own surfaces when
    /// one is deleted. This is the "before" half of <see cref="ClassifyRebuildScope"/>'s comparison; the "after"
    /// half is <see cref="File.Exists(string)"/> at fire time. Full paths, case-insensitive, because that is what
    /// a watcher event carries.
    /// <para>Written from the set a pass actually RENDERED — never by re-walking the disk — so it always describes
    /// what the OUTPUT reflects rather than what the disk currently holds. That distinction is the whole mechanism:
    /// comparing a watch event against the disk tells you nothing, because the disk is the thing that just
    /// changed.</para>
    /// <para><b>Why the narrow routes mutate ONE path instead of re-walking (code review 2026-07-29, owner
    /// decision).</b> They used to call a parameterless refresh that walked <see cref="EnumerateSourceFiles"/>,
    /// which made the inventory equal the disk and collapsed
    /// <see cref="ClassifyRebuildScope"/>'s comparison to disk-vs-disk. <see cref="FileWatcherService"/> arms one
    /// debounce timer PER changed path, so two files touched in one window (a <c>git pull</c>, an agent writing
    /// two files, a save shortly before a create) run two passes: the first one's re-walk recorded the SECOND
    /// one's brand-new file as "rendered", so that file then classified <see cref="RebuildScope.Narrow"/> and its
    /// page landed with no nav entry and no whole-tree surface refreshed — the exact staleness class the
    /// classifier exists to escalate.
    /// <para>The narrow routes therefore record only what they handled. Note this is normally a no-op BY
    /// CONSTRUCTION, which is the invariant to preserve: a narrow route only ever runs for a path whose existence
    /// already agrees with this set (that is what <c>existsNow == wasRendered</c> means), so there is nothing to
    /// change. The per-path mutation is kept as a cheap self-heal for the guard-clause paths
    /// (<see cref="RebuildScope.Narrow"/> returned early for a non-markdown or ignored path) and for any future
    /// drift — never as a discovery mechanism.</para></para></summary>
    private HashSet<string> _sourceInventory = new(SourcePathComparer);

    /// <summary>Path comparer for <see cref="_sourceInventory"/>. Case-insensitive ONLY where the filesystem is:
    /// on Linux/macOS (ADR 0006's npx delivery target) <c>a.md</c> and <c>A.md</c> are two files, and folding them
    /// would let a genuinely new sibling classify <see cref="RebuildScope.Narrow"/> — a missed escalation. Windows
    /// must stay insensitive, since a watcher event's casing need not match the enumerated form.
    /// [code review 2026-07-29]</summary>
    private static StringComparer SourcePathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private SprintStatus? _sprint;
    // Story 8.3: portal-wide count ledger — built once after progress/sprint/workInventory are known and
    // threaded into every summary surface so no render site recounts. Null until the index/work phase.
    private ProjectCounts? _counts;
    // Story 21.2: delivery-cadence dataset (done-story completion days + cycle-times) — built ONCE in GenerateAll
    // after ProgressCalculator has filled each story's LastUpdatedDate, then shared by WriteCadence and the
    // dashboard strip so the (bounded, per-done-story) first-touch git lookups run only once. Null until built.
    private DeliveryCadenceData? _cadence;
    // Story 21.3: planning ↔ code impact correlation (epic/story → touched code files), built ONCE after code
    // references are discovered (so the code-page link gate is populated) and before the epics pages render (so
    // the epic/story "Code areas touched" widgets share the SAME instance the dedicated impact-map.html page uses).
    // Empty when --deep-git didn't run (no commits to mine) or nothing correlated. Reused verbatim by
    // RenderEpicsPages / RenderWebviewSurfaces / RenderSpaBundle and WriteImpactMap so every surface agrees.
    private PlanningCodeImpactData _planningImpact = PlanningCodeImpactData.Empty;
    private List<RetroModel> _retros = new();
    private ArtifactCoverage _coverage = ArtifactCoverage.Empty;

    // When --spa is active OR CapturePages is set, every long-tail page's finished HTML is captured here as it is
    // written (output path → full page string) so the SPA bundle / webview bundle can slice its content region from
    // the render pipeline's OWN output rather than re-reading the generated site off disk (AD-1/AD-2). Null on a
    // normal run — no capture, no overhead, and the static output stays byte-identical (AC #3/#5). The
    // dashboard/epics families are NOT captured here; the SPA and webview re-render them from their view models
    // (RenderSpaBundle / RenderWebviewSurfaces) for strongest parity. [Story 6.7; spec-webview-doc-page-surfaces]
    private Dictionary<string, string>? _spaCapture;

    /// <summary>One captured page's own <see cref="PageView"/> plus the reference-link skip identity its full-page
    /// linkify pass used — everything needed to recompose its content region WITHOUT slicing a rendered document.
    /// <para>The skip ids are part of the capture and not re-derivable: <see cref="ApplyReferenceLinks"/> takes
    /// them so a page never links to itself, and a region linkified without them would grow a self-link the sliced
    /// region does not have. Capturing the page and dropping its skip identity is the one way to make this path
    /// look byte-equal on the fixture (which has no self-referencing prose) and diverge on the real
    /// corpus. [Story 23.4 AC #3]</para></summary>
    /// <param name="Page">The page's own view model — the source of the structural projections the slicer used to
    /// scrape out of a rendered document (<c>Title</c>, <c>Breadcrumb</c>, <c>MetaDescription</c>).</param>
    /// <param name="Region">The composed content region, <b>computed eagerly at write time</b> — see
    /// <see cref="WritePage"/>. This is deliberately a finished string rather than a recipe.
    /// <para>⚠️ <b>Composing the region lazily is a real defect, and it is invisible until the corpus proof runs.</b>
    /// <see cref="ApplyReferenceLinks"/> reads MUTABLE generator state — <see cref="_codePages"/> grows as the code
    /// pass emits pages — and <c>CodeReferenceLinkifier</c> is state-dependent in TWO directions: it no-ops entirely
    /// while the map is empty, and once populated it STRIPS view-source anchors it cannot resolve. So a page written
    /// before the code pass (<c>readme.html</c>) keeps a relative anchor in its document, while the same region
    /// recomposed after that pass loses it. Measured: 77 bytes on <c>readme.html</c>, and it was the only unexpected
    /// delta in 1,408 pages. Composing at the same instant the document is linkified removes the whole class.</para></param>
    private readonly record struct CapturedPageView(PageView Page, string Region);

    // Story 23.4 AC #3: the REPLACEMENT for _spaCapture's slice-a-rendered-page path. Populated at the same write
    // seam and under the same conditions, but with the page's own view model rather than its finished HTML string,
    // so the content region can be COMPOSED (nav + wayfinding + body) instead of sliced back out of a document.
    // Both run in parallel while the byte-equality proof stands (RegionCompositionDeltas); _spaCapture and the
    // SpaDelivery.Extract* family retire once it is green on the real corpus, not on the fixture alone.
    private Dictionary<string, CapturedPageView>? _spaPageViews;

    /// <summary>Opt-in page capture WITHOUT the SPA delivery outputs: when true, <see cref="GenerateAll"/> fills the
    /// same write-seam capture <c>--spa</c> uses, and <see cref="RenderWebviewSurfaces"/> turns every captured
    /// long-tail page (docs, ADRs, requirements, sprint, retros…) into a navigable webview surface so the panel's
    /// header nav links work in-editor. Set BEFORE <see cref="GenerateAll"/> (the webview command does). Memory-only:
    /// written bytes are identical either way (the golden gate). [spec-webview-doc-page-surfaces]</summary>
    public bool CapturePages { get; set; }

    /// <summary>Opt-in WATCH-MODE delta sidecar: when true, every <c>--spa</c> emit also writes
    /// <see cref="SpaDelivery.DeltaPath"/> naming what changed since the previous emit — AD-8's "sidecar polling"
    /// transport clause. Set BEFORE the watch loop starts (<see cref="WatchCommand.RunWatchLoop"/> and
    /// <c>webview --serve</c> do). [Story 22.6 AC #2]
    /// <para><b>Why this is a separate switch and not implied by <c>--spa</c></b>: a one-shot
    /// <c>generate --spa</c> must emit NO delta at all. The document carries a wall-clock <c>generatedAt</c> by
    /// nature, and a cold build has to stay byte-reproducible (NFR9) — letting <c>--spa</c> alone turn this on is
    /// exactly how a timestamp gets into a CI artifact.</para></summary>
    public bool EmitDeltaSidecar { get; set; }

    /// <summary>The PREVIOUS emit's manifest JSON — the basis <see cref="SpaDelivery.BuildDelta"/> diffs against.
    /// Null until the first emit of a session (⇒ that emit's delta is a <c>full</c> marker).
    /// <para><b>Captured at the EMIT seam, deliberately, and NOT gated on a route's reported outcome.</b> Story
    /// 22.6's Dev Notes said not to advance the basis on <see cref="GenerationOutcome.Skipped"/> "because the
    /// generator's in-memory state is unchanged". Task 1's measurement falsified that premise for one route:
    /// <see cref="RegenerateFromDataSource"/> calls <see cref="GenerateAll"/> on its FIRST line — a complete
    /// rebuild including this emit — and only afterwards inspects the event list to decide what to report, so an
    /// unparseable <c>sprint-status.yaml</c> returns <c>Skipped</c> having already rewritten the whole IR (the
    /// harness observed <c>code-map.html</c>'s hash moving on exactly that path). A basis gated on the outcome
    /// would refuse to advance there and the next genuine push would diff against a stale manifest, emitting a
    /// false "unchanged" — the failure AC #7 names as worse than a false "changed". Capturing here makes the
    /// basis track what was EMITTED rather than what was REPORTED, and the route stops being a special case.</para>
    /// <para><b>Thread safety</b>: read and replaced only inside <see cref="EmitSpaSite"/>, which every one of its
    /// six call sites reaches while holding <see cref="_gate"/> — so the two concurrent debounce timers
    /// <see cref="FileWatcherService"/> can fire (one per distinct changed path, each on its own thread-pool
    /// thread) are already serialized against each other here. No second lock is added; a second lock around
    /// state the gate already covers would be the deadlock risk, not the protection.</para></summary>
    private string? _previousManifestJson;

    /// <summary>Monotonic delta sequence within one watch session, starting at 1. Lets a polling consumer detect a
    /// MISSED delta — a gap in the sequence means refetch — without comparing clocks. Same
    /// <see cref="_gate"/> serialization as <see cref="_previousManifestJson"/>; never persisted, so it resets at
    /// session start exactly as the contract says.</summary>
    private long _deltaSequence;

    /// <summary>The path whose change triggered the current pass, used ONLY as the delta's <c>trigger</c> LABEL.
    /// Set by <see cref="FileWatcherService.RunDebouncedPass"/> immediately before it dispatches a route.
    /// <para><b>This is a label, never a correctness input.</b> The changed/added/removed lists are computed
    /// entirely from the two manifests under <see cref="_gate"/> and do not consult this field at all. That
    /// matters because the field IS racy in one narrow case: two files saved inside the same debounce window
    /// dispatch on two threads, and the second setter can win before the first acquires the gate — so a delta may
    /// name the sibling path. The lists stay correct either way. Making the label exact would mean threading a
    /// trigger argument through five public route signatures (<see cref="RegenerateEpics"/> and
    /// <see cref="RegenerateAdrs"/> take no path today); that surface change was judged out of proportion to a
    /// diagnostic string, and is recorded here rather than left to be rediscovered.</para></summary>
    private string? _watchTrigger;

    /// <summary>Set by <see cref="RegenerateTopology"/> so the delta it emits is a <c>full</c> marker, and cleared
    /// by <see cref="EmitDelta"/> the moment it is consumed. Separate from <see cref="_watchTrigger"/> ON PURPOSE:
    /// the trigger is a racy diagnostic label, and Story 22.6's live verification caught a topology pass emitting
    /// <c>full: false</c> because a concurrent save had overwritten the label between the route setting it and the
    /// emit reading it. Set and consumed under <see cref="_gate"/>, held continuously by
    /// <see cref="RegenerateTopology"/> across its own write AND its <see cref="GenerateAll"/> call (a reentrant
    /// acquisition, not two separate ones) — a code review found the original version set this flag BEFORE
    /// acquiring <see cref="_gate"/> at all, reproducing the exact race this field exists to close. [Story 22.6
    /// AC #7]</summary>
    private bool _nextEmitIsFullDelta;

    /// <summary>Sets the <c>trigger</c> label for the next emit's delta document — see <see cref="_watchTrigger"/>
    /// for why this is a label only. Internal: <see cref="FileWatcherService"/> is the only caller.</summary>
    internal void SetWatchTrigger(string? trigger) => _watchTrigger = trigger;

    public SiteGenerator(ForgeOptions options)
    {
        _options = options;
        // Seeded before any pass runs so a consumer reached on a git-less path still reads a resolved day rather
        // than default(DateOnly). RefreshToday() re-resolves once the series is known. [Story 5.5]
        RefreshToday();
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
            // Story 7.6: the source-code walk gates the Code Map nav item/page. Enumerated once here (before the nav
            // is built) and cached so WriteCodeMap reuses the exact same set — never a broken "Code Map" link.
            // Inside _gate (review-fix, Story 5.3): RegenerateEpics/GenerateOne/RegenerateAdrs read _codeFiles via
            // BuildNav inside their own locked sections, so assigning it outside this lock raced them the moment
            // RegenerateTopology gave GenerateAll a caller other than watch startup.
            _codeFiles = EnumerateCodeFiles();

            // A full rebuild starts from a clean slate so renamed/removed source docs don't leave
            // orphaned HTML behind (incremental watch-mode updates never wipe the whole tree).
            if (Directory.Exists(_options.OutputRoot))
            {
                Directory.Delete(_options.OutputRoot, recursive: true);
            }

            EnsureScaffold();
            _docs.Clear();
            _prelude = null; // see BuildSurfacePrelude's lifetime rule
            ResetDerivedStateForFullRebuild();

            // Fresh capture buffer for this full build when the opt-in SPA form is on OR the webview asked for
            // page capture (null otherwise → no capture). Capture is memory-only either way.
            _spaCapture = _options.EmitSpa || CapturePages
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : null;
            // Story 23.4 AC #3: the composed-region capture runs on exactly the same condition, so the two paths
            // are always comparable on any run that produces an IR at all.
            _spaPageViews = _spaCapture is null
                ? null
                : new Dictionary<string, CapturedPageView>(StringComparer.OrdinalIgnoreCase);

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

            var navDiagnostics = new List<AdapterDiagnostic>();
            // Deep-git availability is knowable here: ComputeProgress ran in the Ingest callback above.
            // Gate Insights children on the data signal (not later render success) — same tradeoff Sprint/ADRs
            // accept; see SiteNav.Build remarks. [Story 10.1]
            var hasGitInsights = progress?.DeepGit?.Insights is not null;
            var hasDeepAnalytics = progress?.DeepGit is not null;
            var hasActionItems = _sprint?.OpenActionItems.Count > 0;
            var deferredWorkPath = SiteNav.FindDeferredWorkOutputPath(sourceRelatives, navDiagnostics);
            var hasDeferredWork = deferredWorkPath is not null;
            // Project the epic-scoped work graph now (Story 19.2) — BEFORE nav — so the Insights entry and the page
            // write share one gate (a non-empty model). Reads deferred/quick-dev from source (via ResolveFollowUpWork)
            // since _docs isn't populated yet; cached in _workGraph and reused verbatim by WriteWorkGraph.
            _workGraph = BuildWorkGraphModel(bundle.Epics, progress, bundle.Requirements, files);
            var hasWorkGraph = !_workGraph.IsEmpty;
            // Discover forge session workspaces now (Story 18.4) — BEFORE nav — so the Project "Ideas" entry and the
            // page write share one gate (a non-empty model). Workspaces are found by a `.memlog.md` dotfile scan,
            // which the `*.md` source list cannot reveal, so the signal has to be surfaced by this caller exactly
            // like hasSprint/hasWorkGraph. Its own diagnostics ride the shared adapter channel below.
            var ideaDiagnostics = new List<AdapterDiagnostic>();
            _ideas = IdeaDiscovery.Discover(_options.SourceRoot, ideaDiagnostics);
            var hasIdeas = !_ideas.IsEmpty;
            // Discover methodology-module test artifacts now (Story 18.5) — BEFORE nav — for the same
            // one-gate reason. ModuleContext.Detect is deliberately NOT called again (Story 18.2 made detection
            // once-per-run, and re-detecting silently reintroduces the undiagnosed-detection bug); discovery
            // instead reads the covered module's OWN catalog by code via ModuleContext.ForCode, because in a
            // BMM+TEA repo `_module` is BMad Method and naming TEA's artifacts from it would misattribute them.
            var testArtifactDiagnostics = new List<AdapterDiagnostic>();
            _testArtifacts = TestArtifactDiscovery.Discover(
                _options.RepoRoot, _options.SourceRoot, testArtifactDiagnostics);
            var hasTestArtifacts = !_testArtifacts.IsEmpty;
            var nav = SiteNav.Build(
                sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable,
                hasCodeMap: _codeFiles.Count > 0,
                hasGitInsights: hasGitInsights,
                hasDeepAnalytics: hasDeepAnalytics,
                hasActionItems: hasActionItems,
                hasDeferredWork: hasDeferredWork,
                hasWorkGraph: hasWorkGraph,
                hasIdeas: hasIdeas,
                hasTestArtifacts: hasTestArtifacts,
                deferredWorkOutputPath: deferredWorkPath,
                diagnostics: navDiagnostics);
            _nav = nav;

            // Render the README up front so that, if it fails, we can drop the Readme nav entry before any
            // other page is written — the site never links to a readme.html that wasn't actually produced.
            GenerationEvent? readmeEvent = null;
            if (ReadmeAvailable)
            {
                readmeEvent = GenerateReadmeInternal(nav);
                if (readmeEvent is { Outcome: GenerationOutcome.Error })
                {
                    navDiagnostics.Clear();
                    nav = SiteNav.Build(
                        sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), hasReadme: false,
                        hasSprint: SprintAvailable, hasCodeMap: _codeFiles.Count > 0,
                        hasGitInsights: hasGitInsights, hasDeepAnalytics: hasDeepAnalytics,
                        hasActionItems: hasActionItems, hasDeferredWork: hasDeferredWork,
                        hasWorkGraph: hasWorkGraph, hasIdeas: hasIdeas, hasTestArtifacts: hasTestArtifacts,
                        deferredWorkOutputPath: deferredWorkPath, diagnostics: navDiagnostics);
                    _nav = nav;
                }
            }

            // Files the adapter consumed into dedicated surfaces (story artifacts, retro notes) — the generic
            // pages loop below must not also render them.
            var epicsSourceFile = bundle.EpicsSourceFullPath;
            var consumedArtifacts = new HashSet<string>(bundle.ConsumedSourceRelatives, StringComparer.OrdinalIgnoreCase);

            // A discovered forge workspace's markdown (forged-idea.md, and anything else the session left there) is
            // consumed by the idea's own ideas/{slug}.html detail page, so the generic pages loop must not ALSO
            // render it — without this, a hardened idea would get two pages: the detail page and an orphan
            // forge/{slug}/forged-idea.html linked from nowhere (exactly today's undesigned behavior). Extended at
            // the generator level rather than through bundle.ConsumedSourceRelatives because Ideas discovery
            // deliberately does not route through the framework adapter: `bmad-forge-idea` ships in BMad's `core`,
            // the one module ModuleContext.Detect excludes, so ideas must never touch module identity. [Story 18.4]
            foreach (var idea in _ideas.Ideas)
            {
                var workspacePrefix = idea.WorkspaceSourceRelative + "/";
                foreach (var rel in sourceRelatives)
                {
                    if (PathUtil.NormalizeSlashes(rel).StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        consumedArtifacts.Add(rel);
                    }
                }
            }

            // Cache the ingested models with the same granularity the inline parse chain had: epics+progress
            // only once the projection enrichment ran to completion, requirements only when parsed — so a
            // mid-chain failure leaves the previous (or no) values in place rather than nulling everything.
            // Deliberately AFTER the README render above: the README has always rendered before the epics
            // parse, so it is linkified against the PREVIOUS run's models (null on a first run). [Story 4.1]
            if (progress is not null)
            {
                _epicsModel = bundle.Epics;
                _progress = progress;
                // Git metrics are now known, so the run's one "today" can be resolved (it matters only under
                // DatePolicy.LastCommit, which derives from the daily series). [Story 5.5]
                RefreshToday();
                TagEpicRetrospectives();
            }
            if (bundle.Requirements is not null)
            {
                _requirements = bundle.Requirements;
            }

            // Adapter diagnostics surface on the same event channel per-file failures always used — the typed
            // AdapterDiagnostic is the only report for a failure the adapter caught (never double-reported).
            events.AddRange(MapDiagnostics(bundle.Diagnostics));

            // Nav-build diagnostics (duplicate well-known module docs) ride the same channel. Rebuilding nav
            // after a failed README render (above) replaces navDiagnostics wholesale rather than appending, so
            // this merge always reflects exactly the SiteNav actually in play. [spec-epic2-deferred-debt-cleanup]
            events.AddRange(MapDiagnostics(navDiagnostics));

            // Forge-workspace discovery notices (unparseable memlog, duplicate slug, a report that failed the
            // self-contained carry gate or the size cap) ride the same channel. Every one anchors to
            // DiagnosticAnchorRoot.Source — every forge path is under the source root. [Story 18.4]
            events.AddRange(MapDiagnostics(ideaDiagnostics));

            // Module test-artifact discovery notices (an unreadable or unknown-schema TEA JSON, a matrix that
            // doesn't follow the documented structure, artifact families SpecScribe doesn't model, or a module
            // installed with its output directory outside the scanned tree) ride the same channel. Every one
            // anchors to DiagnosticAnchorRoot.Source — every test-artifacts path is under the source root, so
            // Story 18.2's DiagnosticAnchorRoot.Repo is deliberately NOT reused here. [Story 18.5]
            events.AddRange(MapDiagnostics(testArtifactDiagnostics));

            // The D2 join can only be completed now: it needs BOTH the requirements model and the epics model,
            // and this is the first point at which both are cached. Inadmissible bases and unresolvable ids are
            // judged inside the derivation, never here. [Story 18.5 D2]
            _testArtifacts = TestArtifactDiscovery.WithJoin(_testArtifacts, _requirements, _epicsModel);

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

            // Story 21.3: correlate epics/stories with the code files their commits touched, mined from the SAME
            // bounded --deep-git numstat fetch (no second git call). Built HERE — after DiscoverCodeReferences so
            // the code-page link gate (CodePageHref) is populated, and before RenderEpicsPages so the epic/story
            // "Code areas touched" widgets consume the same instance the dedicated impact-map.html page later reuses.
            // Empty (never null) when --deep-git didn't run: DeepGit is null → no commits → an honest empty result,
            // so the widgets and page all omit/degrade cleanly (AC #2). Never-throw by the builder's own contract.
            _planningImpact = _epicsModel is { } impactEpics && progress?.DeepGit?.Commits is { Count: > 0 } impactCommits
                ? PlanningCodeImpact.Build(impactEpics, impactCommits, CodePageHref)
                : PlanningCodeImpactData.Empty;

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
            // the same reason).
            RefreshDatePagesAndTimeline(nav, events, reporter);

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
                    WritePage(DeepAnalyticsTemplater.BuildPage(deepPulse, nav, fileHref: CodeItemHref));
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
            // The panel omission above is silent on the page by design, so record WHY here — once per generate
            // run, on the events path, next to the other run-level notice. [Story 18.6 owner decision D3]
            AppendUnmodeledCoverageNotice(events, _module);

            // Built once and shared with WriteSprint / WriteActionItems / WriteIndex. Lifted above WriteSprint
            // so the sprint page can consume the shared ledger without moving the sprint write later in the
            // phase order (diagnostics event ordering is load-bearing for the golden fingerprint). [Story 2.3 review; Story 8.3]
            var workInventory = WorkInventory.Build(_docs.Values.ToList());
            // Story 8.3: one portal-wide count ledger. Divergence → exactly one Unsupported AdapterDiagnostic
            // (same channel as Story 8.2 / 4.1).
            _counts = ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, workInventory, _epicsModel, _requirements);
            AppendCountDivergenceNotice(events, _counts);
            // Sprint page reads the epics model (titles/links) + the shared ledger (tracked totals). [Story 2.3; 8.3]
            WriteSprint(nav);
            // The source-code treemap reads the cached source-code walk + the (now-populated) deep-git per-file
            // metrics, so it's written after the pages/git phases — like WriteSprint, gated on the same source-code
            // signal as its nav item. Replaced the retired Story 3.4 structure tree. [Story 7.6]
            var fullCodeMap = WriteCodeMap(nav);
            // The refactor-target risk quadrant (Story 7.10) — split out to its own Insights nav entry (review
            // pass) so it isn't buried at the bottom of the (already long) Code Map page. Reuses WriteCodeMap's
            // already-built unfiltered map (Story 7.10 review-fix) instead of re-walking _codeFiles a second time.
            WriteRiskQuadrant(nav, fullCodeMap);
            // The requirement traceability matrix (Story 21.1) — shares Requirements' hasEpics gate; needs
            // _counts (built just above) for its ledger-sourced legend/ranking caption.
            WriteTraceability(nav);
            // The planning ↔ code impact map (Story 21.3) — writes the SAME _planningImpact instance the epic/story
            // widgets already consumed, so the page and widgets can never disagree. Gated on the combined
            // hasEpics && hasDeepAnalytics signal the nav entry used (a deep-git run must have happened at all).
            WriteImpactMap(nav);
            // The epic-scoped work graph (Story 19.2) — the model was projected + gated before nav; writing the
            // SAME cached instance keeps the Insights entry and the page in lockstep.
            WriteWorkGraph(nav);
            // The forged-ideas surface (Story 18.4) — the model was discovered + gated before nav; writing from the
            // SAME cached instance keeps the Project entry and the pages in lockstep. Written HERE, after the pages
            // phase, because AC #2's forward links can only be resolved once _docs knows which targets actually got
            // a page (a target with no page is dropped, never emitted as a dead link).
            events.AddRange(WriteIdeas(nav));
            events.AddRange(WriteTestArtifacts(nav));
            // Delivery cadence (Story 21.2) — built ONCE here (after ProgressCalculator filled LastUpdatedDate),
            // then shared by WriteCadence and the dashboard strip (WriteIndex reads _cadence). The bounded
            // per-done-story first-touch git lookups happen exactly once, in this build.
            _cadence = DeliveryCadence.Build(_epicsModel, DeliveryCadence.GitFirstTouchResolver(_options.RepoRoot));
            WriteCadence(nav);
            WriteRetroIndex(nav);
            WriteActionItems(nav, workInventory);
            RefreshFollowUpSurfaces(nav, workInventory);

            reporter?.BeginPhase(GenerationPhase.Index);
            WriteIndex(nav, workInventory);
            // Conditional page assets discovered during the index write (Story 20.5's plotly bundle) report here —
            // before diagnostics is written below, so a copy failure lands on the run's own notice page.
            events.AddRange(DrainPendingAssetEvents());
            reporter?.EndPhase(GenerationPhase.Index);

            // Diagnostics + About are the whole-run reporting surface (Story 4.8): written LAST, after every
            // phase has appended its events, so the diagnostics page reflects the COMPLETE non-fatal notice set.
            // Both are always written on a full run (the diagnostics page's zero-notice case renders an all-clear
            // state, never a gated-away page), so the site-wide footer "About" link — and the About page's link
            // on to the run log — can never 404. Each write's own Generated event is appended AFTER the
            // diagnostics page reads the notice list, so it never self-references. [Story 4.8 Task 6]
            events.Add(WriteDiagnostics(nav, events));
            events.Add(WriteAbout(nav));
            events.Add(WriteHowToRead(nav));
            events.Add(WriteDesignSystem(nav));
            events.AddRange(WriteAboutSdd(nav));

            // Opt-in JSON+SPA delivery form, emitted LAST so every captured page is present. Strictly additive:
            // writes only its own files under OutputRoot, leaving every static page byte-identical (AC #3/#5/#6).
            if (_options.EmitSpa)
            {
                _familyDegradedStories.Clear();
                EmitSpaSite(nav);
                // ONE event, only when a degrade actually happened — so the normal path's event stream (and
                // therefore the diagnostics page and GoldenContentFingerprint, which are ordering-sensitive)
                // is byte-unchanged, while a real IR/static fork can never again pass at errors=0.
                // [Story 22.4 code review]
                if (_familyDegradedStories.Count > 0)
                {
                    events.Add(new GenerationEvent(
                        GenerationOutcome.Error,
                        _epicsSourcePath ?? "epics.md",
                        TimeSpan.Zero,
                        $"{_familyDegradedStories.Count} story artifact(s) became unreadable during the SPA/webview " +
                        $"bundle build and were degraded to placeholders in the IR while the static pages kept their " +
                        $"full content: {string.Join("; ", _familyDegradedStories)}"));
                }
            }

            // Story 22.5: install the rebuild-scope baseline from the set this pass RENDERED — LAST, after the
            // render, not before the output-root wipe.
            //
            // Ordering is the whole point (code review 2026-07-29). Seeded before the wipe, a GenerateAll that threw
            // partway — the wipe and the embedded-asset copy both race scanners, editors and open browser tabs, which
            // is why RunGuarded exists — left an inventory asserting every source file was rendered on top of an empty
            // or partial output root. Since WatchCommand deliberately does NOT fail-fast on an initial-build error,
            // the session then classified every subsequent save Narrow forever and could never escalate its way back
            // to a coherent site. Installing on success only means a failed pass keeps the PREVIOUS known-good
            // inventory, so the next event still compares against something real.
            //
            // `watch` and `serve` both run a full generation before their first dispatch (WatchCommand /
            // RunServeLoop), so by the time ClassifyRebuildScope is ever consulted this is populated — a watch
            // session can never open with an empty inventory and read every save as a brand-new file.
            if (TryBuildSourceInventory(files, out var renderedInventory))
            {
                _sourceInventory = renderedInventory;
            }
            else
            {
                events.Add(new GenerationEvent(
                    GenerationOutcome.Error,
                    SiteNav.DiagnosticsOutputPath,
                    TimeSpan.Zero,
                    "the rebuild-scope inventory could not be re-read after this pass; watch mode will escalate " +
                    "conservatively until the next successful full rebuild"));
            }
        }
        return events;
    }

    public GenerationEvent GenerateOne(string sourceFullPath)
    {
        lock (_gate)
        {
            EnsureScaffold();
            _prelude = null; // see BuildSurfacePrelude's lifetime rule
            // Same lifetime rule, same reason: this map is built from _docs/_adrs/_referenceMap, all of which
            // this pass is about to change, and RefreshCodeSurfaces below re-links every code-map cell through
            // it. Cached across a pass, it answers with the PREVIOUS pass's hrefs. [Story 22.5]
            _artifactHrefByRepoRel = null;
            var nav = _nav ?? BuildNav(Array.Empty<string>());
            var ev = GenerateOneInternal(sourceFullPath, nav);
            RefreshCoverage();
            RefreshCodeSurfaces(nav);
            var inventory = RefreshFollowUpSurfaces(nav);
            RefreshDatePagesAndTimeline(nav, new List<GenerationEvent>());
            WriteIndex(nav, inventory);
            // Record only the path this pass handled — never a disk re-walk, which would absorb a sibling's pending
            // topology change and classify it Narrow. See _sourceInventory. [code review 2026-07-29]
            if (ev.Outcome is GenerationOutcome.Generated or GenerationOutcome.Updated)
            {
                RecordSourceRendered(sourceFullPath);
            }
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
            _prelude = null; // see BuildSurfacePrelude's lifetime rule
            // Same lifetime rule, same reason: this map is built from _docs/_adrs/_referenceMap, all of which
            // this pass is about to change, and RefreshCodeSurfaces below re-links every code-map cell through
            // it. Cached across a pass, it answers with the PREVIOUS pass's hrefs. [Story 22.5]
            _artifactHrefByRepoRel = null;
            // The dispatch only reaches here when the file is gone from disk, so it is no longer rendered whichever
            // branch runs below. Recorded for the one path, never by re-walking. [code review 2026-07-29]
            RecordSourceRemoved(sourceFullPath);
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
                _spaPageViews?.Remove(PathUtil.NormalizeSlashes(doc.OutputRelativePath));
                RefreshCoverage();
                var nav = _nav ?? BuildNav(Array.Empty<string>());
                RefreshCodeSurfaces(nav);
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

    /// <summary>Re-walks the source code and rewrites the three surfaces projected from it —
    /// <c>code-map.html</c>, <c>risk-quadrant.html</c> and <c>git-insights.html</c>. The narrow counterpart of
    /// <see cref="RefreshCoverage"/>, and called from the same routes for the same reason. [Story 22.5 AC #4]
    ///
    /// <para><b>Why a CONTENT edit needs this, which Story 22.1 did not find.</b> The Code Map is a treemap of the
    /// source walk, and the walk carries each file's LINE COUNT — which is also what sizes every cell and what the
    /// page's own subtitle states ("N files across M directories · L lines of code"). So editing ONE tracked file,
    /// changing nothing else, is already enough to make the cached page wrong. Measured against the oracle, this was
    /// the single surviving divergence on <i>every</i> change class once Story 22.4 closed the work-graph one:
    /// content-doc and content-story diverged here exactly as add/rename/delete did. Story 22.1 listed
    /// <c>code-map.html</c> under topology changes only and warned its list was a lower bound; this is one of the
    /// entries that bound was hiding.</para>
    ///
    /// <para>Escalation is NOT the fix for the content case: a save is the dominant edit class in a live watch
    /// session, and rebuilding the whole site on every keystroke-save is precisely the cost owner decision D3
    /// declined to pay. The topology case still escalates (<see cref="ClassifyRebuildScope"/>) — there the page set
    /// itself changes and far more than these two surfaces is stranded.</para>
    ///
    /// <para><b>⚠ The nav gate CAN flip here — the original claim that it could not was wrong (code review
    /// 2026-07-29).</b> <c>hasCodeMap</c> is <c>_codeFiles.Count &gt; 0</c> and these routes reuse a cached
    /// <see cref="_nav"/>, so a walk that went from non-empty to empty leaves a nav entry pointing at a page that no
    /// longer renders. The original reasoning was that "a change in the FILE SET is a topology change and escalated
    /// before reaching any narrow route", but <see cref="_codeFiles"/> is the git-tracked CODE set: no watcher
    /// observes <c>.cs</c>/<c>.ts</c>/<c>.css</c> at all, and <see cref="ClassifyRebuildScope"/> returns
    /// <see cref="RebuildScope.Narrow"/> for every non-markdown path by construction — so that set changes freely
    /// (a branch switch, a build, a new source file) with nothing escalating. In practice the flip leaves the two
    /// pages STALE rather than broken, which is what watch mode did before this method existed, so it is not
    /// corrected here beyond stating it honestly; a genuine fix belongs with whatever gives the code walk its own
    /// change signal.</para>
    ///
    /// <para><b>Failures are contained here, not allowed to abort the pass.</b> <see cref="WriteCodeMap"/> is NOT
    /// never-throw, contrary to what this comment used to assert: its own <c>try</c> covers only
    /// <see cref="CodeMap.BuildVariants"/>, while <c>CodeMapTemplater.RenderPage</c>,
    /// <see cref="ApplyReferenceLinks"/>, <see cref="EnsureHierarchyEngine"/> and <see cref="WriteOutput"/> (a bare
    /// <see cref="File.WriteAllText(string,string)"/>, no retry) sit outside it. Since this method runs BEFORE
    /// <c>WriteIndex</c>, the inventory record and the SPA emit in every narrow route, one <see cref="IOException"/>
    /// on the 1.5 MB <c>code-map.html</c> — a file an open browser tab or a scanner can easily hold — turned an
    /// ordinary doc save into a pass with a stale index and an un-re-emitted IR. Before this method existed a save
    /// never touched that page, so this failure mode was introduced with it. It is now caught and reported.</para></summary>
    private void RefreshCodeSurfaces(SiteNav nav, List<GenerationEvent>? events = null)
    {
        try
        {
            RefreshCodeSurfacesCore(nav, events);
        }
        catch (Exception ex)
        {
            // Contained deliberately: these three pages are a REFRESH of surfaces the pass did not otherwise touch,
            // so failing to update them must not cost the caller its own page write, its index, or its IR emit.
            events?.Add(new GenerationEvent(
                GenerationOutcome.Error, SiteNav.CodeMapOutputPath, TimeSpan.Zero, ex.Message));
        }
    }

    private void RefreshCodeSurfacesCore(SiteNav nav, List<GenerationEvent>? events)
    {
        _codeFiles = EnumerateCodeFiles();
        WriteRiskQuadrant(nav, WriteCodeMap(nav));
        // git-insights.html is the THIRD surface on this walk, and the one a non-git fixture cannot reveal: its
        // ownership section builds `CodeMap.Build(_codeFiles, …)` for the same reason the Code Map page does (the
        // hub's own GitInsightsData.Files is top-N-capped, wrong for a whole-tree sunburst), so it inherits the
        // line-count dependence verbatim. It surfaced only in the deep-git-ON matrix at repo scale — which is
        // exactly why Story 22.5 AC #1 required that re-measure instead of trusting Story 22.1's deep-git-OFF list.
        // Self-gating on `_progress?.DeepGit`, so with --deep-git off this is a return, and it re-uses the deep-git
        // pulse already in memory rather than shelling out to git again (no new subprocess, no 3s-budget exposure).
        // deep-analytics.html is deliberately NOT here: it takes no CodeMap, and the matrix measured it clean.
        //
        // The caller's event list is threaded in where it has one (code review 2026-07-29). This used to pass a
        // throwaway `new List<GenerationEvent>()`, so a write failure on this page was reported NOWHERE: the Error
        // event went into the discarded list while the catch disabled the surface for the rest of the session.
        GenerateGitInsightsInternal(nav, events ?? new List<GenerationEvent>(), reporter: null);
    }

    /// <summary>True for a non-markdown DATA SOURCE the site reads but the <c>.md</c> dispatch routes don't refresh:
    /// the sprint tracking file (<c>sprint-status.yaml</c> — the sprint board / Now &amp; Next data) and the project
    /// config (<c>_bmad/config.toml</c> — the site branding). The watch dispatch checks this FIRST (before
    /// <see cref="IsAdr"/>/<see cref="IsEpicsRelated"/>) because <c>sprint-status.yaml</c> lives under
    /// <c>implementation-artifacts/</c>, so <see cref="IsEpicsRelated"/> would otherwise claim it and route to
    /// <see cref="RegenerateEpics"/>, which by design never re-parses sprint state (AD-5). Classifies via the shared
    /// <see cref="BmadArtifactAdapter.SprintStatusFileName"/> / <see cref="ForgeOptions.ConfigFileName"/> conventions,
    /// never a second literal. [Story 6.11]
    /// <para><b>Forge activity is deliberately NOT routed here (Story 18.4 non-goal).</b> A forge session's
    /// <c>.memlog.md</c> is dotfile-ignored and its <c>forge-report.html</c> is not <c>.md</c>, so watch mode does
    /// not react to a session starting, progressing, or completing; a full <c>generate</c> picks everything up
    /// (it wipes and rebuilds the output root). Extending THIS method is the mechanism a follow-on would use —
    /// but a forge session writes continuously mid-conversation, so making the portal live-follow it is a
    /// debounce/ordering decision of its own, not a line added here.</para></summary>
    public bool IsDataSource(string sourceFullPath) =>
        BmadArtifactAdapter.IsSprintStatusFile(sourceFullPath) || IsProjectConfigFile(sourceFullPath);

    /// <summary>True when <paramref name="path"/> is <c>_bmad/config.toml</c>: the config file NAME under a
    /// <c>_bmad</c> directory SEGMENT (any depth, either slash style) — the same location-tolerant, segment-based
    /// discipline <see cref="BmadArtifactAdapter.IsUnderImplementationArtifacts"/> uses, so a stray <c>config.toml</c>
    /// elsewhere isn't mistaken for the project config. <b>Classification only</b> — <see cref="FileWatcherService"/>
    /// does not watch every <c>_bmad</c> segment this matches; it watches the repo-root <c>_bmad</c> dir only (one
    /// per project in practice), so a nested <c>_bmad</c> dir elsewhere would classify here but never actually fire
    /// a watch event. [Story 6.11] [Story 6.11 deferred-work cleanup: narrowed this comment's "any depth" claim]</summary>
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

    /// <summary>THE rebuild-scope decision for one watch-mode change event: does the family route suffice, or must
    /// this escalate to a full rebuild? One method, so the rule is readable and testable in one place instead of
    /// being implied by which route a path happens to reach. [Story 22.5 AC #3]
    ///
    /// <para><b>The rule.</b> A change is TOPOLOGY when the file's existence at fire time disagrees with whether the
    /// last completed pass rendered it (<see cref="_sourceInventory"/>) — i.e. a page must now appear or disappear.
    /// Everything else is CONTENT: the same pages exist, they just say something different. Deliberately ONE rule
    /// rather than a per-family one: "did the page set change" is the question every stranded surface actually turns
    /// on, and a family-shaped rule would need re-deciding each time a family is added.</para>
    ///
    /// <para><b>Ground truth at fire time, never the event kind</b> — the same discipline
    /// <see cref="FileWatcherService.RunDebouncedPass"/> already applies. A single editor save emits
    /// Changed/Created/Deleted in any order before the debounce settles, so believing the event kind would
    /// classify an ordinary save as a delete roughly whenever the editor writes via a temp-file swap.</para>
    ///
    /// <para><b>Why escalating is correct and not merely conservative:</b> the surfaces a file-level add / rename /
    /// delete strands are derived from the WHOLE tree — the Code Map and its risk quadrant, delivery cadence, the
    /// reference/citation seam (<c>_referenceMap</c> / <c>_codeReverseMap</c>), the per-file code-view pages — and
    /// no narrow route re-renders them. Worse, a delete leaves ORPHANED output that only
    /// <see cref="GenerateAll"/>'s output-root wipe removes. Story 22.1 measured every one of those.</para>
    ///
    /// <para><b>Non-markdown returns <see cref="RebuildScope.Narrow"/></b> so this can never manufacture a full
    /// rebuild from a file that would never have become a page. The DATA SOURCES (<c>sprint-status.yaml</c>,
    /// <c>_bmad/config.toml</c>) are the one exception, and they are classified by <see cref="IsDataSource"/>
    /// BEFORE this method is consulted — they escalate through <see cref="RegenerateFromDataSource"/>, which is
    /// already a full rebuild. Ignored files (editor temp files, dotfiles) likewise never become pages, so they are
    /// excluded on exactly the predicate the source walk itself applies; without that, a lock file appearing beside
    /// an artifact would trigger a whole rebuild.</para>
    ///
    /// <para><b><c>epics.md</c> is NOT exempt, and that was measured rather than assumed (Story 22.5 Trap 4).</b>
    /// Its deletion has a deliberate, tested teardown of its own — <see cref="RegenerateEpics"/>'s
    /// <c>ingest.SourceFullPath is null</c> branch → <see cref="ClearEpicsFamilyOutputs"/> (Story 5.3 AC #3) — whose
    /// reporting is strictly more honest than an escalation's: <see cref="GenerationOutcome.Removed"/> with a page
    /// count when it actually tore something down, and the long-standing <see cref="GenerationOutcome.Skipped"/>
    /// no-op for a project that simply never had an <c>epics.md</c>. Exempting it to preserve that was the obvious
    /// call, so it was tried first and diffed against the oracle: the teardown left <b>16 pages stale and 3
    /// missing</b>. The missing ones are the point — once <c>epics.md</c> is gone the story artifacts stop being
    /// consumed by the epics family and a full rebuild renders them as ORDINARY DOCS, which no teardown of the
    /// epics family could ever produce. So the honest resolution is escalation, at the cost of the watch log
    /// reading <c>&lt;directory change&gt;</c> instead of "epics.md removed; N stale page(s) deleted".
    /// <see cref="ClearEpicsFamilyOutputs"/> and its 8 tests are untouched and still reachable through the public
    /// <see cref="RegenerateEpics"/> API (which is how they drive it) — this narrows only what the WATCH DISPATCH
    /// chooses.</para>
    ///
    /// <para>Takes <see cref="_gate"/> because it reads <see cref="_sourceInventory"/>, which a concurrent route may
    /// be replacing. It calls no route, so it cannot deadlock the way a caller wrapping
    /// <see cref="GenerateAll"/> in this lock would.</para></summary>
    public RebuildScope ClassifyRebuildScope(string sourceFullPath)
    {
        // Guard the public surface: the production caller always passes an absolute path from a watcher event, but
        // a null would have thrown out of the dispatch and a relative path would have resolved against the process
        // CWD rather than the repo, silently answering about a different file. [code review 2026-07-29]
        if (string.IsNullOrEmpty(sourceFullPath)) return RebuildScope.Narrow;
        if (!sourceFullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return RebuildScope.Narrow;
        if (IsIgnored(sourceFullPath)) return RebuildScope.Narrow;

        string full;
        try
        {
            full = Path.GetFullPath(sourceFullPath, _options.RepoRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // An unresolvable path cannot be matched against the inventory, and guessing "topology" here would turn
            // a pathological path into a whole rebuild. Narrow keeps the existing family routing, which is what
            // happened before this classifier existed. (NFR2 — degrade, never amplify.)
            return RebuildScope.Narrow;
        }

        // A path the ENUMERATORS structurally skip can never enter the inventory, so `existsNow != wasRendered` would
        // hold forever and every save of it would trigger a whole-site rebuild that changes nothing — a
        // non-convergent escalation loop, the one failure mode worse than a missed rebuild. The ADR watcher is
        // recursive (IncludeSubdirectories = true) while EnumerateAdrFiles is bounded to the root plus ONE level, so
        // this is reachable the moment anyone adopts the nested `decisions/2024/0007-x.md` scheme that enumerator's
        // own doc comment invites. The source half needs no such guard: EnumerateSourceFiles is AllDirectories.
        // [code review 2026-07-29]
        if (!IsWithinAdrEnumerationDepth(full)) return RebuildScope.Narrow;

        lock (_gate)
        {
            var existsNow = File.Exists(full);
            var wasRendered = _sourceInventory.Contains(full);
            return existsNow == wasRendered ? RebuildScope.Narrow : RebuildScope.Full;
        }
    }

    /// <summary>False only for a resolved path under the ADR root that lies DEEPER than
    /// <see cref="EnumerateAdrFiles"/> reaches (root + one level), i.e. one the ADR pass can never render. Pure path
    /// arithmetic — no disk access, since this sits on the watch hot path. Non-ADR paths are always true.
    /// [code review 2026-07-29]</summary>
    private bool IsWithinAdrEnumerationDepth(string resolvedFullPath)
    {
        string adrRoot;
        try
        {
            adrRoot = Path.GetFullPath(_options.AdrSourceRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return true;
        }

        var prefix = adrRoot + Path.DirectorySeparatorChar;
        if (!resolvedFullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;

        // Segments between the root and the file name. 0 => directly in the root; 1 => the one nested level
        // EnumerateAdrFiles also walks; 2+ => invisible to it.
        var tail = resolvedFullPath[prefix.Length..];
        return tail.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length <= 2;
    }

    /// <summary>Drops every model this generator DERIVED from a previous pass, so a full rebuild renders from
    /// source alone. Called from <see cref="GenerateAll"/> beside <c>_docs.Clear()</c>. [Story 22.5 AC #3]
    ///
    /// <para><b>The bug this closes, and why it only appears on a REUSED generator.</b> Watch mode holds ONE
    /// <see cref="SiteGenerator"/> for the life of the session, so an escalated rebuild is
    /// <see cref="GenerateAll"/> called a second, third, n-th time on an instance that already has models. A cold
    /// <c>specscribe generate</c> — the oracle — starts from nulls. Every field below was therefore stale-able in
    /// watch mode and unreachable from any test that built a fresh generator, which is why this survived: measured
    /// against the oracle, deleting <c>epics.md</c> and then escalating still left <c>cadence.html</c> and
    /// <c>traceability.html</c> ORPHANED (written from an <c>_epicsModel</c> whose source no longer existed) and
    /// the Code Map still linking story artifacts to <c>epics/story-N-M.html</c> pages that were gone.</para>
    ///
    /// <para><b>Why this does not contradict the partial-failure caching rule</b> that keeps the last good models
    /// when a mid-edit save fails to parse. That rule protects the INCREMENTAL routes, which leave the rest of the
    /// output tree in place and need the cached models to keep linkifying against it. <see cref="GenerateAll"/> has
    /// already deleted <see cref="ForgeOptions.OutputRoot"/> by this point, so there is no surviving page for a
    /// stale model to keep consistent — retaining one can only write a page whose source is gone. The assignments
    /// downstream are unchanged and still conditional; they now start from "nothing known" exactly as a cold run
    /// does.</para>
    ///
    /// <para><b>Byte-parity note:</b> on a FRESH generator every field below is already at the value assigned here,
    /// so a cold <c>generate</c> — and therefore <c>GoldenContentFingerprint</c> — cannot move. This narrows only
    /// what a SECOND pass on a live generator inherits.</para></summary>
    private void ResetDerivedStateForFullRebuild()
    {
        _epicsModel = null;
        _requirements = null;
        _counts = null;
        _cadence = null;
        _progress = null;
        _workGraph = WorkGraphModel.Empty;
        _planningImpact = PlanningCodeImpactData.Empty;
        _storyEpicByOutputPath = null;
        _referenceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _codeReverseMap = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
        // The one that produced the measured `readme.html` divergence, and the clearest illustration of the whole
        // class. README.md renders EARLY — before the code-page phase populates this map — so on a cold run its
        // source citations resolve against an EMPTY map and stay plain text. On a reused generator they resolved
        // against the PREVIOUS pass's map and came out linkified, making watch mode's readme.html permanently
        // different from `specscribe generate`'s. Ordering, not content, is the whole of the difference.
        _codePages = new Dictionary<string, string>(StringComparer.Ordinal);
        // Lazily built and cached "once per generation run" — but nothing enforced the per-RUN part, so on a reused
        // generator the second run answered every code-item link out of the FIRST run's map.
        _artifactHrefByRepoRel = null;
        // The deep-git trio, missed by the original sweep (code review 2026-07-29). All three are assigned only
        // INSIDE GenerateAll's deep-git gate, so a second pass whose deep-git yield is empty would resolve
        // CommitHref against the FIRST pass's map — linking at commit/*.html pages the output-root wipe removed.
        // The realistic trigger is already documented in this repo: GitMetrics' hard-coded 3,000 ms budget silently
        // drops the whole deep-git payload at errors=0, measured at ~6,500 ms cold. Latent today only because
        // `_progress = null` above gates the consumers off as well — which is exactly the kind of accidental
        // second guard that stops being true when someone fixes the first one.
        _commitDays = new List<CommitDayEntry>();
        _commitDetails = new List<CommitDetailEntry>();
        _commitPages = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Installs <see cref="_sourceInventory"/> from the set a FULL rebuild rendered. <see cref="GenerateAll"/>
    /// is the only caller by design — it is the only pass that renders the whole tree, so it is the only one that can
    /// honestly describe the whole output. The narrow routes use
    /// <see cref="RecordSourceRendered"/>/<see cref="RecordSourceRemoved"/>; see <see cref="_sourceInventory"/> for
    /// why re-walking the disk from a narrow route was a defect rather than a self-heal.
    /// <para><paramref name="sourceFiles"/> is the list the caller is rendering, so no second walk is paid for; the
    /// ADR half is enumerated here, since no caller has it.</para>
    /// <para>Returns <c>false</c> when the walk broke, so the caller can leave the PREVIOUS (last known-good)
    /// inventory in place instead of installing a partial one. Installing a partial set silently escalates every
    /// subsequent save for the rest of the session — output-correct, but a permanent invisible cost.</para></summary>
    private bool TryBuildSourceInventory(IReadOnlyList<string> sourceFiles, out HashSet<string> inventory)
    {
        inventory = new HashSet<string>(SourcePathComparer);
        try
        {
            foreach (var file in sourceFiles) inventory.Add(Path.GetFullPath(file));
            foreach (var file in EnumerateAdrFiles()) inventory.Add(Path.GetFullPath(file));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or PathTooLongException or NotSupportedException)
        {
            // A tree that moved under the walk, or a path Path.GetFullPath cannot resolve. The last three are the
            // ones ClassifyRebuildScope already catches for exactly the same call — omitting them here let an
            // ordinary pathological filename escape the route entirely. [code review 2026-07-29]
            return false;
        }
    }

    /// <summary>Records that this pass rendered <paramref name="sourceFullPath"/>. Normally a no-op — a narrow route
    /// only runs for a path already in agreement with the inventory — but it keeps the guard-clause paths honest
    /// without a disk walk. Caller holds <see cref="_gate"/>. [code review 2026-07-29]</summary>
    private void RecordSourceRendered(string sourceFullPath) => MutateSourceInventory(sourceFullPath, rendered: true);

    /// <summary>Records that <paramref name="sourceFullPath"/> is no longer rendered. Caller holds
    /// <see cref="_gate"/>. [code review 2026-07-29]</summary>
    private void RecordSourceRemoved(string sourceFullPath) => MutateSourceInventory(sourceFullPath, rendered: false);

    private void MutateSourceInventory(string sourceFullPath, bool rendered)
    {
        if (string.IsNullOrEmpty(sourceFullPath)) return;
        string full;
        try
        {
            full = Path.GetFullPath(sourceFullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // Same posture as ClassifyRebuildScope: an unresolvable path cannot be matched against the inventory,
            // and leaving the set alone biases the next event toward escalation, which is output-correct. [NFR2]
            return;
        }

        if (rendered) _sourceInventory.Add(full);
        else _sourceInventory.Remove(full);
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
            _prelude = null; // see BuildSurfacePrelude's lifetime rule
            // Same lifetime rule, same reason: this map is built from _docs/_adrs/_referenceMap, all of which
            // this pass is about to change, and RefreshCodeSurfaces below re-links every code-map cell through
            // it. Cached across a pass, it answers with the PREVIOUS pass's hrefs. [Story 22.5]
            _artifactHrefByRepoRel = null;

            var files = EnumerateSourceFiles();
            var navDiagnostics = new List<AdapterDiagnostic>();
            var nav = BuildNav(files.Select(ToSourceRelative).ToList(), navDiagnostics);
            _nav = nav;

            // Scoped re-ingest through the adapter: exactly the epics + story-artifact + requirements re-parse
            // this path always did, without touching the sprint/retro/module state it never refreshed (AD-5
            // watch parity). [Story 4.1]
            ProgressModel? progress = null;
            var ingest = _adapter.IngestEpics(_options, files, (model, artifactsById) => progress = ComputeProgress(model, artifactsById));

            if (ingest.SourceFullPath is null)
            {
                // Story 5.3 AC #3: epics.md is GONE (deleted or renamed away) but — unlike a full GenerateAll, which
                // wipes OutputRoot first — the incremental watch path never removed anything, so the ENTIRE
                // epics-derived output family was left on disk pointing at a source that no longer exists, and the
                // cached models kept claiming it did. Clear the epics state and delete those pages BEFORE the nav is
                // rebuilt below (SiteNav's Work Graph gate reads _workGraph), so no nav entry can survive pointing at
                // a page this branch just removed.
                var removedPages = ClearEpicsFamilyOutputs();
                // hasEpics is derived from the on-disk source list, so a fresh BuildNav is what actually drops the
                // Epics/Requirements/Traceability/Cadence entries — the nav built above still had them.
                nav = BuildNav(files.Select(ToSourceRelative).ToList());
                _nav = nav;

                RefreshCoverage();
                // Still refresh deferred/follow-up HTML — deferred-work.md is under implementation-artifacts/
                // and routes here, not GenerateOne. [spec-epic9-watch-followup-surface-refresh]
                var skippedInventory = RefreshFollowUpSurfaces(nav, sourceFiles: files);
                if (removedPages > 0)
                {
                    // Open Question #2's chosen default — the sprint / action-items pages DEGRADE IN PLACE rather
                    // than disappearing (their own source, sprint-status.yaml, is still there). "In place" has to
                    // mean re-rendered, not left alone: both were written with live links into the epics pages this
                    // branch just deleted, so leaving the old bytes would swap one dangling-link class for another.
                    // Both templaters already accept a null EpicsModel and simply drop the cross-links.
                    // RefreshFollowUpSurfaces above rebuilt _counts against the now-null model, so these two pick up
                    // the corrected tallies. [Story 5.3 AC #3]
                    WriteSprint(nav);
                    WriteActionItems(nav, skippedInventory);
                }
                RefreshDatePagesAndTimeline(nav, new List<GenerationEvent>());
                RefreshCodeSurfaces(nav);
                WriteIndex(nav, skippedInventory);
                // No inventory write: a family route runs only for a path whose existence already agrees with the
                // inventory, and `files` is a fresh disk walk — installing it here absorbed a sibling's pending
                // add/delete and classified it Narrow. See _sourceInventory. [code review 2026-07-29]
                if (_options.EmitSpa) EmitSpaSite(nav);

                // Only a pass that actually tore down a stale output family reports Removed — a project that simply
                // has no epics.md (and never did) keeps reporting the long-standing Skipped no-op, so the common
                // "not a BMad epics project" case is behaviourally unchanged. [Story 5.3]
                if (removedPages > 0)
                {
                    return new GenerationEvent(
                        GenerationOutcome.Removed, BmadArtifactAdapter.EpicsFileName, sw.Elapsed,
                        $"{BmadArtifactAdapter.EpicsFileName} removed; {removedPages} stale page(s) deleted");
                }

                var skippedMsg = $"{BmadArtifactAdapter.EpicsFileName} not found";
                if (_counts is { HasDivergence: true } skippedDivergent)
                    skippedMsg += $"; [Unsupported] {skippedDivergent.DivergenceMessage()}";
                return new GenerationEvent(GenerationOutcome.Skipped, BmadArtifactAdapter.EpicsFileName, sw.Elapsed, skippedMsg);
            }

            // Same partial-failure caching rules as GenerateAll: a broken mid-edit save must leave the last
            // good models in place so the rest of the site keeps linkifying against them.
            if (progress is not null)
            {
                _epicsModel = ingest.Epics;
                _progress = progress;
                // Same as the full-build path: re-resolve the pass's "today" as soon as git metrics land, so an
                // incremental regen's linked set and generated set are filtered on one shared value. [Story 5.5]
                RefreshToday();
                // Watch-mode re-ingest also re-stamps HasRetrospective from the (preserved) retro map, so a retro'd
                // all-done epic doesn't flip to "In review" on the sunburst/donut/badge after an unrelated story
                // edit — the flag must not reset to false when the model is rebuilt. [spec-sunburst-retro]
                TagEpicRetrospectives();
            }
            if (ingest.Requirements is not null)
            {
                _requirements = ingest.Requirements;
            }

            // deferred-work.md edits land here (IsEpicsRelated). Sync BodyHtml/open tallies before epic
            // sunburst render so wedges and later follow-up writers agree with on-disk content.
            SyncDeferredDocFromDisk(files);
            _counts = null;

            var epicsEvents = new List<GenerationEvent>(MapDiagnostics(ingest.Diagnostics));
            epicsEvents.AddRange(MapDiagnostics(navDiagnostics));
            if (ingest is { Epics: { } epicsModel, Requirements: { } requirementsModel } && progress is not null)
            {
                // Refresh the work-graph model on every incremental pass too (Story 19.2 review) — it was
                // previously cached ONCE per full GenerateAll() and never touched again here, so EpicSubgraph
                // (reading _workGraph) could disagree with StorySubgraph (built fresh from followUps inside
                // RenderEpicsPages below) after a single watch-mode edit to deferred/action attribution. Same
                // partial-failure caching rule as elsewhere in this method: only refreshed when the re-ingest
                // actually produced a usable model.
                _workGraph = BuildWorkGraphModel(epicsModel, progress, requirementsModel, files);
                // Refresh the planning<->code impact correlation on every incremental pass too (Story 21.3
                // review) — same staleness class as _workGraph above: it was previously built ONCE per full
                // GenerateAll() and never touched again here, so the epic/story "Code areas touched" widgets
                // rendered by RenderEpicsPages below (and impact-map.html) could keep showing a stale snapshot
                // after a watch-mode edit. [Review][Patch]
                _planningImpact = progress.DeepGit?.Commits is { Count: > 0 } impactCommits
                    ? PlanningCodeImpact.Build(epicsModel, impactCommits, CodePageHref)
                    : PlanningCodeImpactData.Empty;
                epicsEvents.AddRange(RenderEpicsPages(ingest.SourceFullPath, files, ingest.StoryArtifactsById, epicsModel, requirementsModel, progress, nav));
                WriteWorkGraph(nav);
                WriteImpactMap(nav);
            }
            RefreshCoverage();
            var followUpInventory = RefreshFollowUpSurfaces(nav, sourceFiles: files);
            // RefreshFollowUpSurfaces rebuilds _counts when null; re-emit Unsupported divergence onto this
            // path's events (GenerateAll already emitted via AppendCountDivergenceNotice). [spec-epic8-deferred-debt-cleanup]
            if (_counts is not null)
                AppendCountDivergenceNotice(epicsEvents, _counts);
            RefreshDatePagesAndTimeline(nav, epicsEvents);
            RefreshCodeSurfaces(nav, epicsEvents);
            WriteIndex(nav, followUpInventory);
            // No inventory write — see the sibling branch above and _sourceInventory. [code review 2026-07-29]
            if (_options.EmitSpa) EmitSpaSite(nav);

            var errored = epicsEvents.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
            if (errored is not null)
            {
                return errored;
            }

            var summary = $"{ingest.ConsumedSourceRelatives.Count} stories";
            if (_counts is { HasDivergence: true } divergent)
                summary += $"; [Unsupported] {divergent.DivergenceMessage()}";
            return new GenerationEvent(GenerationOutcome.Updated, ToSourceRelative(ingest.SourceFullPath), sw.Elapsed, summary);
        }
    }

    /// <summary>Every output page whose write is gated on <see cref="_epicsModel"/> being non-null — the pages that
    /// exist ONLY because epics.md was parsed. Deleted together when epics.md disappears (Story 5.3 AC #3). Kept as
    /// the <see cref="SiteNav"/> path constants (never literals) so a page that gets renamed there can't silently
    /// fall out of this cleanup set.</summary>
    private static readonly string[] EpicsFamilyPages =
    {
        SiteNav.EpicsOutputPath,          // gated: RenderEpicsPages only runs with a parsed model
        SiteNav.RequirementsOutputPath,   // gated: WriteRequirements is called from RenderEpicsPages
        SiteNav.TraceabilityOutputPath,   // gated: _epicsModel is null || _requirements is null || _counts is null
        SiteNav.CadenceOutputPath,        // gated: _epicsModel is null || _cadence is null
        SiteNav.ImpactMapOutputPath,      // gated: _epicsModel is null || _progress?.DeepGit is null
        SiteNav.WorkGraphOutputPath,      // gated: _workGraph.IsEmpty (and the graph is projected FROM epics)
    };

    /// <summary>Output subdirectories written only by the epics family — the per-epic/per-story pages and the
    /// per-requirement detail pages.</summary>
    private static readonly string[] EpicsFamilyDirectories = { "epics", "requirements" };

    /// <summary>Drops the epics-derived models and deletes the output family they produced, returning how many files
    /// were actually removed. The asymmetry this fixes: every writer above no-ops when its model is null, which
    /// prevents WRITING a stale page but never REMOVES one already on disk — harmless under
    /// <see cref="GenerateAll"/> (which wipes <see cref="ForgeOptions.OutputRoot"/> first) and precisely the watch-mode
    /// gap in AC #3.
    /// <para><see cref="_progress"/> is SPLIT rather than nulled, departing from the story task's literal
    /// "<c>_progress = null</c>": it carries two unrelated payloads. The epic/story/task roll-ups are epics-derived
    /// and must go — the dashboard's "Progress by Epic" mosaic reads <see cref="ProgressModel.PerEpic"/>, NOT
    /// <see cref="_epicsModel"/>, so leaving them intact keeps a panel of cards linking into the <c>epics/</c>
    /// subtree this method just deleted (the exact dangling-link class of AC #3, found by test). The
    /// <see cref="ProgressModel.Git"/> / <see cref="ProgressModel.DeepGit"/> payloads are mined from the git repo,
    /// not epics.md, and gate the git-insights / deep-analytics pages that are still valid and still on disk —
    /// nulling those would strip their nav entries and orphan the pages, the same divergence pointing the other
    /// way. So: zero the roll-ups, keep the git pulse. [Story 5.3]</para></summary>
    private int ClearEpicsFamilyOutputs()
    {
        _epicsModel = null;
        _requirements = null;
        _counts = null;
        _cadence = null;
        _workGraph = WorkGraphModel.Empty;
        _planningImpact = PlanningCodeImpactData.Empty;
        _storyEpicByOutputPath = null;
        _progress = _progress is null
            ? null
            : new ProgressModel
            {
                EpicsTotal = 0,
                EpicsDrafted = 0,
                EpicsPending = 0,
                StoriesTotal = 0,
                StoriesWithArtifact = 0,
                TasksDone = 0,
                TasksTotal = 0,
                PerEpic = Array.Empty<EpicProgress>(),
                Git = _progress.Git,
                DeepGit = _progress.DeepGit,
            };

        var removed = 0;
        foreach (var page in EpicsFamilyPages)
        {
            if (DeleteOutputFile(page)) removed++;
        }
        foreach (var dir in EpicsFamilyDirectories)
        {
            removed += DeleteOutputDirectory(dir);
        }
        return removed;
    }

    /// <summary>Deletes one output-relative page if present and evicts it from the SPA capture, so a removed page
    /// can't linger in the next bundle (the same pairing <see cref="RemoveFor"/> already does). Returns whether a
    /// file was actually deleted. Best-effort: an IO failure (file held open by a reader) is swallowed so a watch
    /// pass degrades rather than crashing the loop (NFR2). [Story 5.3]</summary>
    private bool DeleteOutputFile(string outputRelativePath)
    {
        var key = PathUtil.NormalizeSlashes(outputRelativePath);
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
        {
            // Already gone from disk (e.g. an earlier partial cleanup) — still converge a stale capture entry.
            _spaCapture?.Remove(key);
            _spaPageViews?.Remove(key);
            return false;
        }
        try
        {
            File.Delete(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Delete failed — the page is still really on disk, so the capture must keep reflecting that too
            // (review-fix, Story 5.3: evicting unconditionally here left static-site and --spa visitors diverged
            // whenever the delete lost a race with a reader holding the file open).
            return false;
        }
        _spaCapture?.Remove(key);
        _spaPageViews?.Remove(key);
        return true;
    }

    /// <summary>Recursively deletes one output-relative subdirectory and evicts every captured page beneath it,
    /// returning the file count removed. Mirrors the existing rebuild-the-whole-subtree pattern
    /// (<c>GenerateAdrsInternal</c> / <c>RenderEpicsPages</c>) rather than inventing a second one. [Story 5.3]</summary>
    private int DeleteOutputDirectory(string outputRelativeDir)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(full))
        {
            // Already gone from disk — still converge any stale capture entries left over from an earlier partial
            // cleanup, rather than leaving them to linger forever. [Story 5.3 review-fix]
            ReconcileSpaCapturePrefix(outputRelativeDir);
            return 0;
        }

        try
        {
            var count = Directory.GetFiles(full, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(full, recursive: true);
            ReconcileSpaCapturePrefix(outputRelativeDir);
            return count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A partial delete may have removed some files but not all (one locked file mid-tree). Reconcile the
            // capture against what's genuinely still on disk instead of assuming the whole subtree went — evicting
            // unconditionally here (the pre-review-fix behavior) could drop capture entries for pages that never
            // actually left disk. [Story 5.3 review-fix]
            ReconcileSpaCapturePrefix(outputRelativeDir);
            return 0;
        }
    }

    /// <summary>Evicts every <see cref="_spaCapture"/> entry under <paramref name="outputRelativeDir"/> whose file no
    /// longer exists on disk, so the capture converges to real disk state even after a partial directory delete
    /// (some files gone, some not) rather than being evicted or kept as an all-or-nothing block. [Story 5.3
    /// review-fix]</summary>
    private void ReconcileSpaCapturePrefix(string outputRelativeDir)
    {
        if (_spaCapture is null) return;

        var prefix = PathUtil.NormalizeSlashes(outputRelativeDir) + "/";
        var fullDir = Path.Combine(_options.OutputRoot, outputRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        foreach (var key in _spaCapture.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var candidate = Path.Combine(fullDir, key[prefix.Length..].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidate))
            {
                _spaCapture.Remove(key);
                _spaPageViews?.Remove(key);
            }
        }
    }

    /// <summary>Watch-mode route for a topology change under a watched root — the scope escalation itself. Originally
    /// DIRECTORY-level only (a folder created, renamed or deleted — Story 5.3 AC #2/#5), because the per-file
    /// incremental routes structurally cannot handle a folder operation: they key every decision off a single changed
    /// path, and a folder either reports no usable path at all or (on a rename) strands every page beneath the old
    /// name with nothing to re-derive them from. Story 22.5 gave it a second caller —
    /// <see cref="ClassifyRebuildScope"/> now routes FILE-level adds, renames and deletes here too — so the
    /// "directory-level" framing no longer describes it (corrected in code review 2026-07-29).
    /// <para>The correct scope escalation is the full rebuild <see cref="GenerateAll"/> already implements — wipe
    /// <see cref="ForgeOptions.OutputRoot"/>, rescan, rebuild — so this is a thin collapse of its event list to the
    /// single event the watch log shows, matching <see cref="RegenerateFromDataSource"/>'s shape and the
    /// <c>Regenerate*</c> naming convention rather than calling <c>GenerateAll</c> from the watcher directly.</para>
    /// <para><paramref name="label"/> is what the ONE collapsed event is reported under. A directory
    /// event has no single honest path so it keeps <see cref="FileWatcherService.TopologyEventLabel"/>; a file-level
    /// escalation passes the path that fired, because reporting a named file's add or delete as
    /// <c>&lt;directory change&gt;</c> is a log the user cannot act on — and that label's own contract is to mean "a
    /// directory changed, so do not attribute this to some arbitrary contained file". Still exactly one event either
    /// way, which is what AC #7 requires. [code review 2026-07-29]</para></summary>
    public GenerationEvent RegenerateTopology(string? label = null)
    {
        var sw = Stopwatch.StartNew();
        // The delta sidecar this pass emits must be a `full` marker (AC #7 / Trap 5): a literal page diff over a
        // whole-site rebuild produces a thousand-entry `changed` list, larger and slower than the full payload it
        // was meant to replace. Flagged HERE rather than inferred from the trigger LABEL, and that distinction was
        // forced by live evidence, not theory — during Story 22.6's Task 8 verification a concurrent session's
        // save re-set the label between this route's own SetWatchTrigger and its emit, and the topology pass
        // emitted `full: false` with that sibling's path as its trigger. The label is racy by construction (one
        // debounce Timer per changed path, each on its own thread) and is documented as a diagnostic; deriving a
        // correctness decision from it contradicted that and was defeated exactly as the race predicts.
        //
        // CODE REVIEW FIX (Story 22.6): the flag write below used to happen BEFORE this call site entered
        // GenerateAll()'s own `lock (_gate)`, which reproduced the exact class of race this comment describes —
        // a concurrent debounce-Timer thread could win `_gate` first and consume/clear a flag meant for THIS
        // pass, before this pass's own GenerateAll() ever reached its emit. `_gate` is a reentrant Monitor lock
        // (`lock` supports same-thread re-entry), so acquiring it HERE, across both the flag write and the
        // GenerateAll() call, closes that window completely: GenerateAll()'s own internal `lock (_gate)` simply
        // re-enters on this same thread, and no other thread can acquire `_gate` in between. The flag is now
        // GENUINELY set-and-consumed under one lock acquisition, not merely commented as if it were. The
        // `finally` clears it even if GenerateAll() throws (or never reaches EmitDelta because `--spa` is off),
        // so a mid-rebuild failure can never leave it armed for some later, unrelated emit to consume.
        IReadOnlyList<GenerationEvent> events;
        lock (_gate)
        {
            _nextEmitIsFullDelta = true;
            try
            {
                events = GenerateAll();
            }
            finally
            {
                _nextEmitIsFullDelta = false;
            }
        }

        var errored = events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error);
        if (errored is not null)
        {
            return errored;
        }

        return new GenerationEvent(
            GenerationOutcome.Updated, label ?? FileWatcherService.TopologyEventLabel, sw.Elapsed, "full rebuild");
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
            _prelude = null; // see BuildSurfacePrelude's lifetime rule
            // Same lifetime rule, same reason: this map is built from _docs/_adrs/_referenceMap, all of which
            // this pass is about to change, and RefreshCodeSurfaces below re-links every code-map cell through
            // it. Cached across a pass, it answers with the PREVIOUS pass's hrefs. [Story 22.5]
            _artifactHrefByRepoRel = null;
            var nav = _nav ?? BuildNav(Array.Empty<string>());
            var events = GenerateAdrsInternal(nav);
            RefreshCoverage();
            RefreshCodeSurfaces(nav, events);
            WriteIndex(nav);
            // No inventory write — see RegenerateEpics and _sourceInventory. [code review 2026-07-29]
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

        // Every ADR-family output path actually WRITTEN during this pass — records AND non-records (README,
        // template scaffolding) AND the synthesized landing when it fires — so the end-of-method _spaCapture
        // prune (below) can tell a page that's still live from one that's genuinely gone, without conflating
        // "record" (which is all _adrs ever holds) with "renders a page" (which also includes non-records).
        // [deferred-work: story-6-7 watch-mode _spaCapture drift; review-patch: pruning by _adrs alone evicted
        // non-record pages — README/template — the SAME pass that (re)wrote them]
        var writtenAdrPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        Created = parsed.Frontmatter.Created,
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
                    WritePage(HtmlTemplater.BuildDocPage(doc, nav));
                    writtenAdrPaths.Add(PathUtil.NormalizeSlashes(outputRelative));
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
                // Story 10.10: the white sub-header band's local context for an ADR page — the SAME _adrs list
                // already passed to EntityPager.FromSequence above, not a second query.
                var adrLocalContext = new NavLocalContext(
                    "ADRs",
                    _adrs.Select(e => new NavLocalItem(
                        e.Title,
                        prefix + e.OutputRelativePath,
                        string.Equals(e.OutputRelativePath, entry.OutputRelativePath, StringComparison.OrdinalIgnoreCase))).ToList());
                WritePage(HtmlTemplater.BuildDocPage(recordDoc, nav, pager, localContext: adrLocalContext));
                writtenAdrPaths.Add(PathUtil.NormalizeSlashes(entry.OutputRelativePath));
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
                // Story 10.8: the most primitive of the six list-shaped indexes — routed onto the shared
                // ListRow anatomy (summary, status badge, metadata chip, one primary link) instead of the
                // old bare "<a>title</a> — status" line.
                body.Append("<p>Architecture decision records for this project.</p>\n<ul class=\"adr-landing-list list-rows-list js-listable\">\n");
                foreach (var adr in _adrs)
                {
                    // Records live under the adrs/ output subdir alongside this landing — the href is the
                    // record's output path with that shared prefix stripped (nested records keep their subpath).
                    var prefix = ForgeOptions.AdrOutputSubdir + "/";
                    var href = adr.OutputRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        ? adr.OutputRelativePath[prefix.Length..]
                        : adr.OutputRelativePath;

                    var summaryHtml = adr.Summary is { Length: > 0 } summary
                        ? $"<strong>{PathUtil.Html(adr.Title)}</strong> — {PathUtil.Html(summary)}"
                        : PathUtil.Html(adr.Title);
                    var badgeHtml = !string.IsNullOrWhiteSpace(adr.Status) ? StatusStyles.FreeTextBadge(adr.Status) : null;
                    var chips = adr.Date is { } date
                        ? new[] { ListRow.Chip(PathUtil.Html(PortalDates.Day(date))) }
                        : Array.Empty<string>();
                    var primaryLink = ListRow.PrimaryLink(PathUtil.Html(href), "View record");
                    // The left accent bar reflects each record's real status (Accepted → done, Superseded →
                    // deferred, …) instead of the follow-up family's fixed review color; unknown states keep the
                    // neutral default. [Story 10.8 review]
                    var accentToken = StatusStyles.AdrAccentToken(adr.Status);
                    var accentClass = accentToken is null ? null : $"list-row-accent-{accentToken}";

                    ListRow.Render(
                        body, summaryHtml, badgeHtml, chips, primaryLink,
                        extraRowClass: accentClass,
                        sortName: adr.Title,
                        sortDate: adr.Date is { } sortDate ? PortalDates.IsoDay(sortDate) : null,
                        sortStatus: !string.IsNullOrWhiteSpace(adr.Status) ? StatusStyles.ForSprint(adr.Status) : null);
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
                WritePage(HtmlTemplater.BuildDocPage(landing, nav));
                writtenAdrPaths.Add(PathUtil.NormalizeSlashes(SiteNav.AdrsLandingOutputPath));
                events.Add(new GenerationEvent(GenerationOutcome.Generated,
                    $"{ForgeOptions.AdrOutputSubdir}/index.html (synthesized landing)", sw.Elapsed));
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.AdrsLandingOutputPath, sw.Elapsed, ex.Message));
            }
        }

        // Prune stale SPA-capture entries for any ADR-family page NOT actually (re)written this pass — a
        // renamed/deleted record, README, or template file, OR a landing page that no longer has anything to
        // write it (the last ADR was removed and no record occupies the landing slot). The physical adrs/
        // OUTPUT DIRECTORY is wiped and rebuilt above (self-healing the static site), but _spaCapture is a
        // separate in-memory map that pass 2 only OVERWRITES for pages still present — using writtenAdrPaths
        // (every WriteOutput call above, not just _adrs/record entries) rather than _adrs alone avoids evicting
        // a still-live non-record page (README/template) the SAME pass just wrote it.
        // [deferred-work: story-6-7 watch-mode _spaCapture drift]
        if (_spaCapture is { } captureAfterAdrs)
        {
            var stalePrefix = ForgeOptions.AdrOutputSubdir + "/";
            var staleKeys = captureAfterAdrs.Keys
                .Where(k => k.StartsWith(stalePrefix, StringComparison.OrdinalIgnoreCase) && !writtenAdrPaths.Contains(k))
                .ToList();
            foreach (var key in staleKeys)
            {
                captureAfterAdrs.Remove(key);
                _spaPageViews?.Remove(key);
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
            ? Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, _today)
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
            var outputRelative = DayPageOutputPath(day);
            try
            {
                var dayCommits = commitsByDay.TryGetValue(day, out var c) ? c : Array.Empty<CommitInfo>();
                var dayArtifacts = artifactsByDay.TryGetValue(day, out var a) ? a : Array.Empty<(string, string)>();
                var prefix = PathUtil.RelativePrefix(outputRelative);
                var pager = EntityPager.FromSequence(days, i, d => prefix + DayPageOutputPath(d), Charts.DReadable);
                // Story 10.10: the white sub-header band's local context — the SAME day family already built
                // above for the pager, not a second query.
                var dayLocalContext = new NavLocalContext(
                    "Recent activity",
                    days.Select(d => new NavLocalItem(Charts.DReadable(d), prefix + DayPageOutputPath(d), d == day)).ToList());
                WritePage(CommitDayTemplater.BuildPage(
                    day, dayCommits, dayArtifacts, pager, nav, commitHref, dayLocalContext));

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
    private IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> BuildArtifactsByDay(
        List<GenerationEvent>? events = null)
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

            // The run's shared cutoff, not a second clock read: this skip guard and LinkedCommitDays must exclude
            // the same days or an artifact change lands on a date page that was never generated. [Story 5.5]
            var today = _today;
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
                            labelCache[art.SourceRel] = label = ArtifactLabel(art.SourceRel, art.FullPath, events);
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
    /// exists. A read failure (permission/I-O) records one <see cref="GenerationOutcome.Skipped"/> event on
    /// <paramref name="events"/> when non-null, matching most other per-item failure paths in this file instead of
    /// swallowing it silently. [Story 7.3; spec-7-3-deferred-debt-cleanup]</summary>
    private static string ArtifactLabel(string sourceRelative, string fullPath, List<GenerationEvent>? events)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceRelative);
        try
        {
            return ExtractArtifactTitle(MarkdownConverter.ReadAllTextShared(fullPath), stem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            events?.Add(new GenerationEvent(GenerationOutcome.Skipped, sourceRelative, TimeSpan.Zero,
                $"artifact title read failed, using filename: {ex.Message}"));
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
            WritePage(TimelineTemplater.BuildPage(git, newestFirst, commitsByDay, artifactsByDay, nav, _today));
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
                // Story 10.10: the white sub-header band's local context — the SAME slots family already built
                // above for the pager, not a second query.
                var commitLocalContext = new NavLocalContext(
                    "Recent commits",
                    slots.Select(s => new NavLocalItem(
                        CommitPagerLabel(s.Commit),
                        prefix + s.OutputRelative,
                        string.Equals(s.OutputRelative, outputRelative, StringComparison.OrdinalIgnoreCase))).ToList());
                WritePage(CommitDetailTemplater.BuildPage(commit, nav, CodePageHref, pager, commitLocalContext));

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
    /// collision, so prefix — not equality — is the safe rule). Returns null (→ plain hash) when no page exists or
    /// when ≥2 full hashes share the prefix (fail closed — a wrong History link is worse than plain text).
    /// ≤300 entries, so the linear prefix scan is trivial. [spec-7-1-deferred-debt-cleanup]</summary>
    private string? CommitHref(string hash) => ResolveCommitPageHref(_commitPages, hash);

    /// <summary>Pure commit-hash → page resolver used by <see cref="CommitHref"/>. Extracted so unit tests can pin
    /// the exact-match / unique-prefix / ambiguous-prefix contract without spinning up a full generation.</summary>
    internal static string? ResolveCommitPageHref(IReadOnlyDictionary<string, string> commitPages, string hash)
    {
        if (hash.Length == 0 || commitPages.Count == 0) return null;
        if (commitPages.TryGetValue(hash, out var exact)) return exact;
        string? match = null;
        foreach (var kv in commitPages)
        {
            if (!kv.Key.StartsWith(hash, StringComparison.Ordinal)) continue;
            if (match is not null) return null; // ambiguous — fail closed
            match = kv.Value;
        }
        return match;
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

    /// <summary>The single formula for a day's output path — shared by <see cref="GenerateDatePagesInternal"/> and
    /// <see cref="ChangeLogDayHref"/> so the two can never drift apart. [date links]</summary>
    private static string DayPageOutputPath(DateOnly date) => PathUtil.NormalizeSlashes($"commits/{Charts.D(date)}.html");

    /// <summary>Guarded date→href resolver for a story's Change Log entries, usable BEFORE
    /// <see cref="GenerateDatePagesInternal"/> runs (story pages render earlier in the pipeline than date pages, so
    /// <see cref="DayHref"/>/<see cref="_commitDays"/> aren't populated yet). Calls
    /// <see cref="Charts.LinkedCommitDays"/> directly — the same commit-day computation
    /// <see cref="GenerateDatePagesInternal"/> uses as ONE HALF of its actual day set (the other half,
    /// <c>artifactsByDay</c>, needs <see cref="_docs"/>/<see cref="_referenceMap"/>/<see cref="_adrs"/> populated,
    /// which isn't true yet at story-render time). This is therefore a PROVABLE SUBSET, never a superset, of the
    /// real day set: it can safely under-link (degrade to plain text — the same accepted behavior as "no page
    /// exists") on the rare artifact-only-day case where a commit touched a recognized artifact but nothing in the
    /// shallow pulse's day series, but it can never claim a date is linkable when the eventual page won't exist —
    /// i.e. it can never dead-link. Two earlier review loops tried narrower per-condition checks (an early
    /// git-only day-set forecast, then a two-condition shallow-or-deep-git check) and both introduced real dead-link
    /// gaps that this subset approach structurally avoids — see the "review loop 1/2/3" Design Notes in
    /// spec-change-log-date-links.md. [date links]</summary>
    private string? ChangeLogDayHref(DateOnly date) =>
        _progress?.Git is { } git &&
        Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, _today).Contains(date)
            ? DayPageOutputPath(date)
            : null;

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
        // BMad artifact source files that already have a MORE MEANINGFUL rendered page than the generic
        // code/…html syntax view route there FIRST (Story 7.10 review: sprint-status.yaml and other BMad
        // markdown sources were routing high-churn hits on the risk quadrant to a raw-text code page instead of
        // the actual sprint board / epic / doc / ADR page that already exists for them). Checked before
        // CodePageHref so a file that is BOTH a recognized source-code-walk entry (this repo's own _bmad-output
        // is itself version-controlled, so its yaml/markdown shows up in every git-analytics surface) AND a
        // rendered artifact never routes to the less useful raw view.
        if (_sprint is not null && BmadArtifactAdapter.IsSprintStatusFile(repoRelativePath))
            return SiteNav.SprintOutputPath;
        if (_epicsModel is not null && BmadArtifactAdapter.IsEpicsFile(repoRelativePath))
            return SiteNav.EpicsOutputPath;
        if (ArtifactHrefByRepoRel().TryGetValue(PathUtil.NormalizeSlashes(repoRelativePath), out var artifactHref))
            return artifactHref;

        var page = CodePageHref(repoRelativePath);
        if (page is not null) return page;
        // Existence-gate the external fallback: TopChangedFiles/Hotspots rank over a history window, so a deleted
        // or renamed file routinely appears — a blob/<branch>/<deleted-path> link would 404. A doc under sourceRoot
        // has no code page but is a real on-disk file, so it keeps its external link.
        var full = Path.GetFullPath(Path.Combine(_options.RepoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(full) ? BuildExternalSourceUrl(repoRelativePath) : null;
    }

    // Lazily built + cached: repo-relative source path → the already-generated portal page it renders as, for
    // every recognized BMad artifact (epic/story pages via _referenceMap, standalone docs via _docs, ADRs via
    // _adrs). Mirrors BuildArtifactsByDay's Track helper (same three sources, same root-reconciliation logic) —
    // deliberately NOT unified into one shared field since that method's dictionary also carries the resolved
    // full path for its own commit-walk join, a different value shape than this one needs. Built once per
    // generation run (all three sources are done changing by the time code pages/git-analytics surfaces render),
    // not per-lookup — cheap, but no reason to redo it per file. [Story 7.10 review]
    private Dictionary<string, string>? _artifactHrefByRepoRel;

    private IReadOnlyDictionary<string, string> ArtifactHrefByRepoRel()
    {
        if (_artifactHrefByRepoRel is { } cached) return cached;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Track(string sourceRel, string href, string root)
        {
            try
            {
                var full = Path.GetFullPath(Path.Combine(root, sourceRel.Replace('/', Path.DirectorySeparatorChar)));
                var repoRel = PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.RepoRoot, full));
                if (repoRel.StartsWith("..", StringComparison.Ordinal)) return; // outside the repo
                // TryAdd (not an overwriting indexer): the first artifact found for a given repo-relative path
                // keeps the mapping rather than silently losing it to whichever source iterates last.
                map.TryAdd(repoRel, PathUtil.NormalizeSlashes(href));
            }
            catch
            {
                // A single malformed source path contributes no mapping — never aborts generation (AD-4).
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

        _artifactHrefByRepoRel = map;
        return map;
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
        _codePages = new Dictionary<string, string>(StringComparer.Ordinal);
        _codeReferenced = new List<string>();
        _codeReverseMap = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
        _citerToFiles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        var repoFull = Path.GetFullPath(_options.RepoRoot);
        var sourceFull = Path.GetFullPath(_options.SourceRoot);
        var referenced = new SortedSet<string>(StringComparer.Ordinal);

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
                    : _citerToFiles[citingRelative] = new HashSet<string>(StringComparer.Ordinal);
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

                // Story 10.10: the white sub-header band's local context for a code page — the SAME sibling-file
                // family already assembled above for the pager, not a second directory listing.
                var siblingDir = slash < 0 ? string.Empty : rel[..slash];
                var localContext = new NavLocalContext(
                    string.IsNullOrEmpty(siblingDir) ? "Files in this directory" : $"Files in {siblingDir}",
                    siblings.Select(p => new NavLocalItem(
                        Path.GetFileName(p),
                        codePrefix + PathUtil.NormalizeSlashes($"code/{p}.html"),
                        string.Equals(p, rel, StringComparison.OrdinalIgnoreCase))).ToList());

                PageView page;
                GenerationOutcome outcome;
                if (new FileInfo(full).Length > MaxCodeFileBytes)
                {
                    page = CodeFileTemplater.BuildPlaceholderPage(repoRelative, outputRelative,
                        "This file is too large to render inline.", nav, referencedBy, externalUrl, pager: pager,
                        localContext: localContext, insight: insight, coupledFileHref: CodePageHref,
                        commitHref: CommitHref, dayHref: DayHref,
                        storyRelatedEdges: storyRelatedEdges, relatedRelatedEdges: relatedRelatedEdges);
                    outcome = GenerationOutcome.Skipped;
                }
                else if (TryReadCodeText(full, out var text))
                {
                    var lines = SplitCodeLines(text);
                    page = CodeFileTemplater.BuildPage(repoRelative, outputRelative, lines, nav, referencedBy, externalUrl,
                        insight, CodePageHref, CommitHref, dayHref: DayHref, pager: pager,
                        storyRelatedEdges: storyRelatedEdges, relatedRelatedEdges: relatedRelatedEdges, localContext: localContext);
                    outcome = GenerationOutcome.Generated;
                }
                else
                {
                    page = CodeFileTemplater.BuildPlaceholderPage(repoRelative, outputRelative,
                        "This file is not a readable text file and can't be shown inline.", nav, referencedBy, externalUrl,
                        pager: pager, localContext: localContext, insight: insight, coupledFileHref: CodePageHref,
                        commitHref: CommitHref, dayHref: DayHref,
                        storyRelatedEdges: storyRelatedEdges, relatedRelatedEdges: relatedRelatedEdges);
                    outcome = GenerationOutcome.Skipped;
                }

                WritePage(page, linkify: false);
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
    /// every story/drafted artifact to its non-naive page). A citer missing from <see cref="_referenceMap"/> falls
    /// back to <see cref="PathUtil.ToOutputRelative"/>'s naive extension swap rather than being omitted — an
    /// "omit rather than guess" patch was tried during the Story 7.2 review and reverted: without an
    /// <c>epics.md</c>, <see cref="_referenceMap"/> is never populated, and the naive formula is exactly correct
    /// for ordinary docs in that case (identical to <see cref="GenerateOneInternal"/>'s own output-path
    /// computation), so omitting would have thrown away legitimate back-links. The guess is only unreliable for
    /// entities with a non-naive output path, and those are always overridden in <see cref="_referenceMap"/> when
    /// present. Order is deterministic (reverse-map insertion follows the sorted source walk).</summary>
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

    /// <summary>Streams a source file to count lines without allocating the full string — used by the code-map walk
    /// so oversized tracked files still contribute LOC. Same binary/UTF-8 guards as <see cref="TryReadCodeText"/>;
    /// line-count semantics match <see cref="SplitCodeLines"/> (CRLF/CR normalized, trailing phantom line dropped).
    /// [spec-7-1-deferred-debt-cleanup]</summary>
    internal static bool TryCountCodeLines(string fullPath, out long lines)
    {
        lines = 0;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[64 * 1024];
            var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
            var charBuf = new char[64 * 1024];
            long newlineCount = 0;
            var anyContent = false;
            var endsWithNewline = false;
            var pendingCr = false;

            bool ProcessChar(char c)
            {
                if (c == '\0') return false;

                if (pendingCr)
                {
                    pendingCr = false;
                    if (c == '\n')
                    {
                        newlineCount++;
                        endsWithNewline = true;
                        anyContent = true;
                        return true;
                    }

                    newlineCount++;
                    endsWithNewline = true;
                }

                anyContent = true;
                if (c == '\r')
                {
                    pendingCr = true;
                    return true;
                }

                if (c == '\n')
                {
                    newlineCount++;
                    endsWithNewline = true;
                }
                else
                {
                    endsWithNewline = false;
                }

                return true;
            }

            bool DecodeBytes(byte[] src, int offset, int count)
            {
                for (var i = offset; i < offset + count; i++)
                {
                    if (src[i] == 0) return false;
                }

                int charsDecoded;
                try
                {
                    charsDecoded = decoder.GetChars(src, offset, count, charBuf, 0, flush: false);
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }

                for (var i = 0; i < charsDecoded; i++)
                {
                    if (!ProcessChar(charBuf[i])) return false;
                }

                return true;
            }

            // Peek up to 3 bytes for a UTF-8 BOM before the main loop so a short first Read can't leave EF BB BF
            // in the decode stream (matches TryReadCodeText's whole-buffer strip).
            var bom = new byte[3];
            var bomRead = 0;
            while (bomRead < 3)
            {
                var n = stream.Read(bom, bomRead, 3 - bomRead);
                if (n == 0) break;
                bomRead += n;
            }

            if (bomRead == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                // BOM discarded.
            }
            else if (bomRead > 0)
            {
                if (!DecodeBytes(bom, 0, bomRead)) return false;
            }

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (!DecodeBytes(buffer, 0, read)) return false;
            }

            try
            {
                var flushed = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuf, 0, flush: true);
                for (var i = 0; i < flushed; i++)
                {
                    if (!ProcessChar(charBuf[i])) return false;
                }
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            if (pendingCr)
            {
                newlineCount++;
                endsWithNewline = true;
                anyContent = true;
            }

            if (!anyContent)
            {
                lines = 0;
                return true;
            }

            // Match SplitCodeLines: drop a trailing phantom empty entry from a terminating newline.
            lines = endsWithNewline ? newlineCount : newlineCount + 1;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            // Story 7.11 rewrite: the hub's ownership section needs the SAME uncapped whole-tree CodeMap the
            // Code Map/Risk Quadrant pages build (GitInsightsData.Files is top-N-capped — wrong for a whole-tree
            // sunburst) plus the bounded, deterministic top-author roster for the discrete-palette mode.
            var codeMapMetrics = _progress.DeepGit.CodeMapMetrics;
            var codeMap = CodeMap.Build(_codeFiles, codeMapMetrics);
            // capN matches Charts.OwnershipTopAuthorPaletteSize, not the default GitMetrics.CodeMapFileContributorCap —
            // this roster feeds a fixed-color discrete PALETTE (owner feedback: must draw from the SAME 7-hue
            // categorical scheme Story 7.9's file-type legend uses), a different bound than "how many contributors
            // show up per file."
            var topAuthors = GitMetrics.BuildTopAuthors(_progress.DeepGit.Commits, capN: Charts.OwnershipTopAuthorPaletteSize);
            var html = WritePage(GitInsightsTemplater.BuildPage(
                insights, _progress.Git, nav, codeMap, topAuthors, fileHref: CodeItemHref, today: _today));
            // Story 20.9: this page now hosts a Hierarchy Explorer, so it needs the vendored engine on disk in its
            // own right. It cannot rely on the dashboard having copied it first — a repo with git history but no
            // epics renders this page and no dashboard chart. Idempotent, and it reads the FINISHED html rather
            // than a belief about it.
            EnsureHierarchyEngine(html);
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.GitInsightsOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.GitInsightsOutputPath, sw.Elapsed, ex.Message));
            // Clear Insights only when the page really is absent, so the dashboard's "View all git insights" link
            // (gated on _progress.DeepGit.Insights) can't point at a page that doesn't exist.
            //
            // The File.Exists check matters because this method has TWO callers with opposite starting states (code
            // review 2026-07-29). On a full rebuild the output root was just wiped, so a failure here means no page
            // — clear, exactly as before. But RefreshCodeSurfaces calls this on the NARROW routes, where the previous
            // pass's page is still on disk and still valid; clearing there stripped the nav entry and the dashboard
            // link for a page that exists, and because this method's first line early-returns on a null Insights, one
            // transient write failure during a single debounced save disabled the surface for the whole session.
            if (!File.Exists(Path.Combine(_options.OutputRoot, SiteNav.GitInsightsOutputPath)))
            {
                _progress.DeepGit.Insights = null;
            }
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
            // _docs isn't populated until the later pages loop, so build the follow-up work inventory
            // (deferred entry + parsed model) directly from source here — otherwise this project sunburst
            // would silently omit deferred items that index.html (rendered after _docs fills) shows, and
            // full-gen would diverge from watch-mode RegenerateEpics. Read-only: no _docs mutation, no output.
            var workForFollowUps = ResolveFollowUpWork(files);
            var deferredModel = ResolveDeferredModel(workForFollowUps, files);
            var epicsCounts = _counts ?? ProjectCounts.Build(progress, _sprint, workForFollowUps, model, _requirements);
            var followUps = BuildFollowUpGeometry(workForFollowUps, epicsCounts, deferredModel);
            var unplanned = UnplannedWorkGeometry.From(workForFollowUps, followUps, model, retros: _retros);
            File.WriteAllText(Path.Combine(_options.OutputRoot, "epics.html"), ApplyReferenceLinks(EpicsTemplater.RenderIndex(model, progress, nav, _module.Commands, epicsCounts, followUps, unplanned), "epics.html"));

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
                File.WriteAllText(Path.Combine(epicsDir, $"epic-{epic.Number}.html"), ApplyReferenceLinks(EpicsTemplater.RenderEpic(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath, EpicPager(model, epic), followUps, unplanned, _planningImpact, EpicSubgraph(epic.Number)), $"epics/epic-{epic.Number}.html", skipEpicNumber: epic.Number));

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
                    var storyHtml = EpicsTemplater.RenderStory(epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria, f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, f.Evidence, f.ChangeSurface, nav, _module.Commands, epicRetroPath, StoryPager(model, story), followUps, _planningImpact, StorySubgraph(epic, story, followUps));
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

        // Turn "[Source: _bmad-output/path.md]" citations into real links to the generated page. Only drafted
        // stories reach here (both callers guard on ArtifactOutputPath before resolving the artifact path).
        var storyPrefix = PathUtil.RelativePrefix(story.ArtifactOutputPath!);

        // Change Log entry dates link through the same guarded DayHref-style resolver the code page's History tab
        // uses — plain text when there's no known commit on that date. [date links]
        var changeLogHtml = EpicsParser.ExtractChangeLogHtml(artifactRaw, ChangeLogDayHref, storyPrefix);
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
    /// <c>_bmad-output</c> or other path-structure literal is ever duplicated host-side (Story 6.10 AC #1).
    /// Returns null when <paramref name="absolutePath"/> doesn't resolve to somewhere inside the repo root:
    /// <c>Path.GetRelativePath</c> silently returns the input unchanged (still absolute) when there's no common
    /// root — e.g. a misconfigured <see cref="ForgeOptions.RepoRoot"/> or a different drive on Windows — and a
    /// leading <c>..</c> would still escape the repo the TS-side <c>resolveWorkspacePath</c> containment guard
    /// enforces. Every caller already treats a null source path as "no reveal-source affordance," the same
    /// degrade the dashboard's aggregate pages use, so an escape now surfaces as an honestly hidden button
    /// instead of silently shipping a path the host guard would reject anyway. [Story 6.10 deferred-work fix]
    /// Both sides are resolved through <see cref="PathUtil.ResolveRealPath"/> before the relative-path math, so a
    /// symlinked <c>RepoRoot</c> or an artifact path whose own resolution traverses a symlink no longer computes a
    /// misleading (or falsely-escaping) relative path from THIS method's own lexical-only comparison — the same
    /// symlink-aware INPUT resolution the TS-side <c>resolveWorkspacePath</c> containment guard already applies
    /// via <c>fs.realpathSync</c> (Story 6.9). This does not by itself guarantee the two sides compose correctly
    /// for every symlink topology end-to-end: the TS side re-joins the computed relative string onto the NOMINAL
    /// (non-realpath'd) workspace root, not the real one, so a topology with an additional symlink hop partway
    /// down the artifact's own path (rather than only at the repo root) is not covered by this fix or its tests —
    /// tracked as a follow-up. [6-10-deferred-debt-cleanup; Blind Hunter, spec-epic6 review]</summary>
    private string? RepoRelative(string absolutePath)
    {
        _realRepoRoot ??= PathUtil.ResolveRealPath(_options.RepoRoot);
        var rel = PathUtil.NormalizeSlashes(Path.GetRelativePath(_realRepoRoot, PathUtil.ResolveRealPath(absolutePath)));
        return PathUtil.EscapesRepoRoot(rel) ? null : rel;
    }

    /// <summary>Cached real (symlink-resolved) repo root — computed once per generator instance since
    /// <see cref="ForgeOptions.RepoRoot"/> never changes after construction. [6-10-deferred-debt-cleanup]</summary>
    private string? _realRepoRoot;

    // ===== Story 22.4: ONE region seam, two filtered projections ============================================
    //
    // Until 22.4 the SPA bundle (BuildSpaBundle) and the webview bundle (RenderWebviewSurfaces) were two ~200-line
    // builders that each rebuilt the SAME prelude, iterated the epics family with the SAME rules, and sliced the
    // SAME captured regions — and had to be edited in lockstep forever. Story 22.2's codeItemHref fix had to be
    // applied twice, in both files, and both carried apology comments saying so.
    //
    // The three members below are that seam, singular. Both surfaces are now FILTERED PROJECTIONS of it:
    //   · BuildSurfacePrelude  — docs → work → counts → followUps → unplanned → dashboard PageView
    //   · BuildFamilySurfaces  — the dashboard + epics-family PageView sequence, in order, with the linkify skip
    //                            keys and the identity the webview's outline tree needs
    //   · CapturedRegions      — one (path, title, region, breadcrumb, description, degraded) per captured page
    //
    // What stays surface-specific is ONLY what genuinely differs — see RenderWebviewSurfaces' filter. This is
    // ADR 0008 §Decision 2's "co-equal projections" made true: the webview stopped being a rival builder.

    /// <summary>The shared state every dashboard/epics surface is composed from, built ONCE per bundle so the SPA
    /// and the webview cannot disagree by construction. [Story 22.4 AC #1]</summary>
    private sealed record SurfacePrelude(
        List<DocModel> Docs,
        WorkInventory Work,
        ProjectCounts Counts,
        FollowUpGeometry? FollowUps,
        UnplannedWorkGeometry? Unplanned,
        PageView DashboardPage);

    /// <summary>Builds <see cref="SurfacePrelude"/>. This is the ONE place the dashboard's inputs are assembled.
    /// <para>⚠️ <c>CodeItemHref</c> is passed EXPLICITLY and positionally-correctly to
    /// <see cref="HtmlTemplater.BuildIndexPage"/>. The named arguments start at <c>counts:</c>, so an omission
    /// silently skips the positional <c>codeItemHref</c> and leaves it null — which makes
    /// <c>Charts.CodeItemLink</c> degrade the Git Pulse top-changed-file bar labels from links to plain text and
    /// drops 5 anchors, the ENTIRE 277-byte parity delta Story 23.1 measured. Both former call sites carried a
    /// comment about it because both had made the mistake; unifying them is exactly the moment it could have been
    /// lost, so it is stated once, here, at the only call site left. [Story 22.2 AC #4; Story 22.4 Task 4]</para>
    /// </summary>
    private SurfacePrelude BuildSurfacePrelude(SiteNav nav)
    {
        // ONE INSTANCE, not two equal ones. Both consumers call this, and before the Story 22.4 code review a
        // `--webview` run built the prelude TWICE — once inside GenerateAll's EmitSpaSite and once in
        // RenderWebviewSurfaces — from identical inputs. That is exactly the "equal-but-separately-constructed
        // is the same defect waiting to drift again" case Task 3 named, and it was live in the seam that story
        // built to end it.
        //
        // ⚠️ LIFETIME RULE, and the reason this is a field and not a Lazy<>: the prelude closes over _docs,
        // _counts, _progress, _epicsModel, _requirements, _sprint, _retros, _coverage, _workGraph, _cadence,
        // _testArtifacts and nav. A cache that outlived a state change would ship a STALE dashboard — a new
        // correctness bug in the name of deduplication. So it is invalidated at the START of every MUTATING
        // entry point (GenerateAll, GenerateOne, RemoveFor, RegenerateEpics/Adrs/Topology/FromDataSource) and
        // never anywhere else. RenderWebviewSurfaces is a pure read and deliberately does NOT invalidate, which
        // is what lets it reuse the instance GenerateAll's emit already built. [Story 22.4 code review]
        if (_prelude is { } cached) return cached;

        var docs = _docs.Values.ToList();
        var work = WorkInventory.Build(docs);
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, work, _epicsModel, _requirements);
        var followUps = BuildFollowUpGeometry(work, counts);
        var unplanned = UnplannedWorkGeometry.From(work, followUps, _epicsModel, retros: _retros);
        var dashboardPage = HtmlTemplater.BuildIndexPage(
            docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands,
            work, _sprint, _retros, _coverage, _timelinePath is not null, CodeItemHref, counts: counts, followUps: followUps, unplanned: unplanned, cadence: _cadence, workGraph: _workGraph, dateCutoff: _today, testArtifacts: _testArtifacts);
        return _prelude = new SurfacePrelude(docs, work, counts, followUps, unplanned, dashboardPage);
    }

    /// <summary>One dashboard/epics family surface: the <see cref="PageView"/> both consumers render, plus the
    /// reference-linkify skip keys and the identity only the webview's outline tree needs. Carrying the identity
    /// here (rather than rebuilding the tree in a second loop) is what guarantees an outline node's
    /// <c>SurfacePath</c> comes from the SAME PageView the surface was rendered from — Story 6.9 fact #5.
    /// [Story 22.4 AC #1]</summary>
    private sealed record FamilySurface(
        PageView Page,
        string? SkipStoryId = null,
        int? SkipEpicNumber = null,
        /// <summary>The webview's reveal-source target for the SURFACE (falls back to the epics file).</summary>
        string? SurfaceSourcePath = null,
        /// <summary>The story's own artifact path, or null for a placeholder — the OUTLINE's stricter rule,
        /// which omits "Open Source" entirely rather than falling back to the epics file.</summary>
        string? ArtifactSourcePath = null,
        EpicInfo? Epic = null,
        StoryInfo? Story = null);

    /// <summary>The dashboard + epics family <see cref="PageView"/> sequence, in emission order. Formerly
    /// duplicated verbatim in <see cref="BuildSpaBundle"/> and <see cref="RenderWebviewSurfaces"/> — same models,
    /// same retro map, same pagers, same placeholder rule, same fragment pipeline.
    /// <para><b>Trap 4, resolved deliberately.</b> The two former loops were NOT identical: the webview wrapped its
    /// <see cref="BuildStoryPageFragments"/> call in a <c>catch (IOException or UnauthorizedAccessException)</c>
    /// that degrades ONE story to a placeholder, and the SPA had no catch at all. The unified loop KEEPS the
    /// catch, so the SPA gains resilience it did not have. That direction was chosen because the alternative is
    /// strictly worse: without it, an artifact deleted or ACL-denied mid-render aborts the ENTIRE SPA emit, where
    /// the HTML path (<see cref="RenderEpicsPages"/>) already degrades gracefully. It can only change behaviour in
    /// a case that previously threw. [Story 22.4 AC #1/#2]</para></summary>
    private List<FamilySurface> BuildFamilySurfaces(SiteNav nav, SurfacePrelude prelude)
    {
        var families = new List<FamilySurface> { new(prelude.DashboardPage) };
        if (_epicsModel is not { } model || _progress is not { } progress) return families;

        var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);
        families.Add(new FamilySurface(
            EpicsTemplater.BuildIndexPage(model, progress, nav, _module.Commands, prelude.Counts, prelude.FollowUps, prelude.Unplanned),
            SurfaceSourcePath: _epicsSourcePath));

        foreach (var epic in model.Epics)
        {
            var epicRetroPath = EpicRetroMap.TryGetValue(epic.Number, out var erp) ? erp : null;
            families.Add(new FamilySurface(
                EpicsTemplater.BuildEpicPage(epic, progressByEpic[epic.Number], nav, _module.Commands, epicRetroPath, EpicPager(model, epic), prelude.FollowUps, prelude.Unplanned, _planningImpact, EpicSubgraph(epic.Number)),
                SkipEpicNumber: epic.Number,
                SurfaceSourcePath: _epicsSourcePath,
                Epic: epic));

            foreach (var story in epic.Stories)
            {
                PageView storyPage;
                string? artifactSourcePath = null;
                if (story.ArtifactOutputPath is null || !_storyArtifactsById.TryGetValue(story.Id, out var artifactFullPath))
                {
                    storyPage = EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath, StoryPager(model, story));
                }
                else
                {
                    try
                    {
                        artifactSourcePath = RepoRelative(artifactFullPath);
                        var f = BuildStoryPageFragments(story, artifactFullPath, _referenceMap);
                        storyPage = EpicsTemplater.BuildStoryPage(
                            epic, story, f.ArtifactRelative, f.BlurbHtml, f.RemainderHtml, f.AcceptanceCriteria,
                            f.DevAgentRecord, f.Tasks, f.ReviewFindingsHtml, f.ChangeLogHtml, f.Evidence, f.ChangeSurface, nav,
                            _module.Commands, epicRetroPath, StoryPager(model, story), prelude.FollowUps, _planningImpact, StorySubgraph(epic, story, prelude.FollowUps));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // The artifact was deleted, ACL-denied, or otherwise became unreadable between
                        // GenerateAll() and this bundle build — degrade this ONE story to a placeholder rather
                        // than aborting the whole bundle.
                        //
                        // ⚠️ This is NOT a mirror of RenderEpicsPages' behaviour, despite what this comment
                        // claimed until the Story 22.4 code review corrected it. RenderEpicsPages has no
                        // per-story degrade at all: its only handler is method-level and emits
                        // GenerationOutcome.Error, abandoning the rest of the epics family. So the static page
                        // on disk keeps the story's full content while the IR carries a placeholder for the
                        // same path — a real fork between the two surfaces, and Story 23.4 renders the site
                        // FROM the IR. Recording it is therefore mandatory: a story whose entire premise is
                        // "one producer, so there is no second place to disagree" must not introduce a silent
                        // disagreement. Reported as ONE event after the emit (see GenerateAll), never per page.
                        // [Deferred item, Story 6.4 review; widened to the SPA by Story 22.4 — see Trap 4 above;
                        //  made visible by the Story 22.4 code review]
                        _familyDegradedStories.Add($"{story.Id} ({ex.GetType().Name}: {ex.Message})");
                        artifactSourcePath = null;
                        storyPage = EpicsTemplater.BuildStoryPlaceholderPage(epic, story, nav, _module.Commands, epicRetroPath, StoryPager(model, story));
                    }
                }

                families.Add(new FamilySurface(
                    storyPage,
                    SkipStoryId: story.Id,
                    SurfaceSourcePath: artifactSourcePath ?? _epicsSourcePath,
                    ArtifactSourcePath: artifactSourcePath,
                    Epic: epic,
                    Story: story));
            }
        }

        return families;
    }

    /// <summary>One captured page's COMPOSED content region and everything either consumer projects from it.
    /// <para>↻ <b>Trap 3 is GONE as of Story 23.4, and this note is kept to say so.</b> While the region was
    /// sliced, <see cref="SpaDelivery.ExtractContentRegion"/> signalled "this page carries no landmark" by
    /// returning the nav-markup INSTANCE it was handed, and the webview detected that with a REFERENCE
    /// comparison — a fragile contract in which any caller that copied or re-concatenated the nav markup would
    /// silently break the detection with no test failing. The composed path computes <see cref="Degraded"/>
    /// structurally instead (does the region contain <see cref="SpaDelivery.MainLandmark"/>), so the hazard is
    /// designed out rather than documented around. Still read this flag rather than re-deriving it — one
    /// producer, one answer — but a copy can no longer corrupt it.</para></summary>
    private readonly record struct CapturedRegion(
        string Path,
        string Title,
        string Region,
        IReadOnlyList<BreadcrumbCrumb> Breadcrumb,
        string? MetaDescription,
        bool Degraded);

    /// <summary>COMPOSES every captured page's content region that is not already a family surface — the ONE
    /// region producer, consumed unfiltered by the SPA and filtered by the webview.
    /// <para>⚠️ <b>Story 23.4 AC #3 landed here: this no longer SLICES a rendered document.</b> Each region is
    /// composed from the page's own <see cref="PageView"/> at write time (<see cref="WritePage"/>) and read back
    /// from <see cref="_spaPageViews"/>. <see cref="SpaDelivery.ExtractContentRegion"/> and the
    /// <c>SpaDelivery.Extract*</c> scrapers are no longer on the IR's path — they survive only as the equality
    /// ORACLE that <see cref="RegionCompositionDeltas"/> proves this producer against (1,469 pages, 0 unexpected
    /// deltas), and as the pre-23.4 comparison a future run can still check.</para>
    /// <para><b>Two things get structurally better, not just cleaner.</b> Title, breadcrumb and meta description
    /// now come from the view model instead of being regex-scraped back out of finished HTML. And the region no
    /// longer truncates at <c>&lt;/main&gt;</c>, which is what restores <c>deep-analytics.html</c>'s
    /// <c>:target</c> lightbox — content the slice had been silently dropping, leaving that page's "Expand" link
    /// resolving to nothing in both the SPA and the webview.</para>
    /// [Story 22.4 AC #1/#3; Story 23.4 AC #3]</summary>
    /// <param name="skip">A consumer's exclusion predicate, applied to the normalized path BEFORE any slicing.
    /// Optional: the SPA passes none and consumes the sequence whole. It exists because a consumer that filters
    /// afterwards still pays for what it discards — <see cref="CapturedNavMarkup"/> (a Singleline regex over the
    /// entire page), <see cref="SpaDelivery.ExtractContentRegion"/> (a full nav+band+main string concatenation)
    /// and three more extractions run per page. The webview's exclusions are precisely the classes that scale
    /// with the TARGET repo — the whole <c>_codePages</c> set (bounded only by <c>MaxCodeMapFiles</c> = 25,000),
    /// every commit-day page and every <c>commit/</c> page — so on a large repo filtering afterwards means
    /// materialising and dropping tens of thousands of region strings. Pre-22.4 those were cheap set lookups
    /// that short-circuited first; this parameter restores that ordering without reintroducing a second
    /// producer. [Story 22.4 code review]</param>
    private IEnumerable<CapturedRegion> CapturedRegions(
        SiteNav nav, HashSet<string> familyPaths, Func<string, bool>? skip = null)
    {
        if (_spaPageViews is not { } views) yield break;

        // A page whose finished HTML was captured but whose VIEW MODEL was not is a page still on the
        // pre-23.4 `WriteOutput(path, ApplyReferenceLinks(...))` path. It would simply be absent from the IR —
        // no error, no log, just a route the SPA and webview cannot reach while its .html sits on disk beside
        // them. That is the exact silent-gap class this story exists to close, so it throws.
        if (_spaCapture is { } capture && capture.Count != views.Count)
        {
            var missing = capture.Keys
                .Select(PathUtil.NormalizeSlashes)
                .Where(k => !views.ContainsKey(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{missing.Count} captured page(s) have no PageView and would be silently missing from the "
                    + "IR — they are still written through WriteOutput rather than WritePage. Route them "
                    + $"through WritePage (Story 23.4 AC #3). First: {string.Join(", ", missing.Take(5))}");
            }
        }

        foreach (var (path, captured) in views)
        {
            var normalized = PathUtil.NormalizeSlashes(path);
            if (familyPaths.Contains(normalized)) continue;
            if (skip is not null && skip(normalized)) continue;
            var page = captured.Page;
            yield return new CapturedRegion(
                normalized,
                page.Title,
                captured.Region,
                page.Breadcrumb.Crumbs,
                page.MetaDescription,
                // Computed STRUCTURALLY now, replacing the ReferenceEquals sentinel the slice used to signal
                // with. A composed region degrades only if the page's own body carries no landmark — which is
                // the same condition, asked of the thing itself rather than inferred from an object identity
                // that any copy or re-concatenation would have silently broken.
                Degraded: !captured.Region.Contains(SpaDelivery.MainLandmark, StringComparison.Ordinal));
        }
    }

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

            // ONE shared prelude + ONE family sequence, identical to the SPA bundle's (Story 22.4 AC #1). The
            // webview's dashboard can never disagree with the generated index.html, and — new in 22.4 — it can
            // never disagree with the IR either, because there is no second builder left to drift.
            var prelude = BuildSurfacePrelude(nav);
            var families = BuildFamilySurfaces(nav, prelude);
            var surfaces = new List<WebviewSurface>(families.Count);
            foreach (var f in families)
            {
                surfaces.Add(WebviewSurfaceFor(f.Page, f.SurfaceSourcePath, f.SkipStoryId, f.SkipEpicNumber));
            }

            // The outline tree is projected from the SAME FamilySurface list the surfaces were rendered from, so
            // every node's SurfacePath is the PageView's own path and always matches a surfaces[...] key a tree
            // click can push() to (Story 6.9 fact #5). Data only; no HTML, no re-parse.
            var outline = BuildOutline(families, prelude.FollowUps);

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
            if (_spaCapture is not null)
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

                // THE WEBVIEW'S FILTER over the shared region producer — this, and only this, is what makes the
                // webview a different projection from the SPA (Story 22.4 AC #1/#2):
                //   1. the exclusion set above (code pages, commit-day pages, the commit/ prefix)
                //   2. the degrade skip
                //   3. the JSON-island strip
                //   4. the SourcePath join
                // The SPA consumes the same sequence unfiltered.
                // Filter 1 runs INSIDE the producer (as a skip predicate) rather than over its output, so an
                // excluded page is never sliced at all. Same set, same result, none of the discarded work.
                // [Story 22.4 code review]
                bool WebviewExcludes(string path) =>
                    excluded.Contains(path) || path.StartsWith("commit/", StringComparison.OrdinalIgnoreCase);

                foreach (var captured in CapturedRegions(nav, familyPaths, WebviewExcludes))
                {
                    if (captured.Degraded)
                    {
                        // No <main> landmark → the slice degraded to nav-only. A silently BLANK surface is
                        // worse than the shim's honest toast, so skip it (the SPA keeps its nav-only degrade:
                        // a browser tab is escapable; a status panel claiming "links work" is not).
                        //
                        // The flag is computed at the slice, inside CapturedRegions, against the nav-markup
                        // REFERENCE — so the strip below can no longer defeat it by ordering, which is how this
                        // check used to be fragile (Trap 3).
                        continue;
                    }
                    // Same rule the PageView path applies in WebviewRenderAdapter.RenderContent, which this path
                    // never ran: this surface ships no specscribe.js, so an inline JSON island is dead weight it
                    // can never read — and after Story 20.9 that is 4.5 MB of it on code-map.html alone. The
                    // `data-island` host exception already described the webview as carrying none.
                    var region = WebviewRenderAdapter.StripDataIslands(captured.Region);
                    sourceByOutput.TryGetValue(captured.Path, out var capturedSource);
                    surfaces.Add(new WebviewSurface(captured.Path, captured.Title, region, capturedSource));
                }
            }

            // The entry document embeds the dashboard's ALREADY-linkified content, so wrapping happens after
            // linkification and the linkifier never walks the shell's CSS/bridge-script text. The dashboard's
            // SourcePath is null (it aggregates many artifacts), so the reveal button paints hidden — the bridge
            // shows it only once an update swaps in a surface that carries a source (Story 6.10).
            var entry = surfaces[0];
            var entryDocument = WebviewRenderAdapter.Shared.WrapDocument(prelude.DashboardPage, entry.ContentHtml, entry.SourcePath);
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

        // RepoRelative itself returns null for a source that escapes the repo root; omitting the entry
        // (button hidden) is the honest degrade — the host containment guard would reject it anyway.
        void Add(string outputRelative, string sourceFullPath)
        {
            if (RepoRelative(sourceFullPath) is not { } rel) return;
            map[PathUtil.NormalizeSlashes(outputRelative)] = rel;
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

    /// <summary>Projects the webview's outline tree from the SAME <see cref="FamilySurface"/> sequence the
    /// surfaces were rendered from — so a node's <c>SurfacePath</c> is always the PageView's own path and always
    /// matches a <c>surfaces[...]</c> key a tree click can <c>push()</c> to (Story 6.9 fact #5). Formerly built
    /// inline, interleaved with the surface loop; extracting it is what let that loop become shared.
    /// <para>Note the deliberate asymmetry the <see cref="FamilySurface"/> record preserves: a placeholder
    /// story's SURFACE still reveals the epics file, but its outline node omits "Open Source" entirely — so this
    /// reads <see cref="FamilySurface.ArtifactSourcePath"/>, never <c>SurfaceSourcePath</c>. [Story 22.4]</para>
    /// </summary>
    private ProjectOutline BuildOutline(IReadOnlyList<FamilySurface> families, FollowUpGeometry? followUps)
    {
        var outlineEpics = new List<OutlineEpic>();
        List<OutlineStory>? stories = null;
        EpicInfo? currentEpic = null;
        PageView? currentEpicPage = null;

        void FlushEpic()
        {
            if (currentEpic is not { } epic || currentEpicPage is not { } epicPage) return;
            var epicStage = StatusStyles.ForEpicWithRetrospective(epic);
            outlineEpics.Add(new OutlineEpic(
                epic.Number, epic.Title, epicStage, StatusStyles.EpicLabel(epicStage),
                PathUtil.NormalizeSlashes(epicPage.OutputRelativePath),
                StoriesTotal: epic.Stories.Count,
                StoriesDone: epic.Stories.Count(s => StatusStyles.ForStory(s) == "done"),
                stories ?? new List<OutlineStory>()));
        }

        foreach (var f in families)
        {
            if (f.Story is { } story && f.Epic is not null)
            {
                var storyStage = StatusStyles.ForStory(story);
                // `commands` = full Next Steps set (incl. done's muted correct-course hatch when exposed);
                // `helperCommand` = PrimaryStoryCommand (null for done unless Address deferred is primary —
                // hatch is never a primary). [spec-vscode-sidebar-shortcuts-…-quickpick; Story 8.5;
                // spec-address-deferred-next-steps]
                var storyOpenDeferred = followUps?.DeferredForSource(story.Id)
                    ?.Where(s => !s.Item.Resolved).ToList();
                // INVARIANT, not a null-check: BuildFamilySurfaces always emits an epic's page surface before
                // that epic's story surfaces, which is what initialises `stories`. A null here means the family
                // sequence arrived out of order — and ADR 0024 §Decision 2 explicitly licenses a consumer to
                // FILTER the sequence, so that is a reachable mistake, not a theoretical one. The former
                // `stories?.Add(...)` swallowed it: the story vanished from the outline tree AND from
                // BuildOutlineSummary's counts (which tally the same list), with no exception and no failing
                // test. Fail loudly instead — a webview outline that silently omits stories is worse than one
                // that does not build. [Story 22.4 code review]
                if (stories is null)
                {
                    throw new InvalidOperationException(
                        $"BuildOutline received story '{story.Id}' before any epic page surface for epic " +
                        $"{f.Epic.Number}. The FamilySurface sequence must place an epic's page before its " +
                        "stories; a filtered sequence that drops epic pages breaks this contract.");
                }
                stories.Add(new OutlineStory(
                    story.Id, story.Title, storyStage, StatusStyles.StoryLabel(storyStage),
                    PathUtil.NormalizeSlashes(f.Page.OutputRelativePath),
                    f.ArtifactSourcePath,
                    story.TasksDone, story.TasksTotal,
                    BmadCommands.PrimaryStoryCommand(story, _module.Commands, storyOpenDeferred),
                    BmadCommands.StoryCommands(story, _module.Commands, storyOpenDeferred)));
                continue;
            }

            if (f.Epic is { } newEpic)
            {
                FlushEpic();
                currentEpic = newEpic;
                currentEpicPage = f.Page;
                stories = new List<OutlineStory>();
            }
        }
        FlushEpic();

        return new ProjectOutline(outlineEpics, BuildOutlineSummary(outlineEpics));
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
    /// <param name="capture">Whether the written page joins <see cref="_spaCapture"/>. True for every page this
    /// generator COMPOSED — capture slices their universal <c>&lt;main id="main-content"&gt;</c> landmark. False for
    /// a page carried in verbatim from outside (Story 18.4's <c>forge-report.html</c>): a foreign document has no
    /// such landmark, and <see cref="SpaDelivery.ExtractContentRegion"/> degrades a landmark-less page to
    /// nav-markup-only — so capturing it would put a CONTENT-EMPTY route in the SPA/webview bundle while the real
    /// file sits perfectly readable on disk beside it. Excluded, the link resolves to that static file, which is
    /// exactly right for a deliberate dead-end leaf. [Story 18.4]</param>
    private void WriteOutput(string outputRelativePath, string html, bool capture = true)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(full, html);
        if (capture && _spaCapture is not null)
        {
            _spaCapture[PathUtil.NormalizeSlashes(outputRelativePath)] = html;
        }
    }

    /// <summary>The Story 23.4 write seam: renders one page from its OWN <see cref="PageView"/>, reference-linkifies
    /// the finished document exactly as the pre-23.4 call sites did, writes it, and captures BOTH forms — the
    /// finished HTML string (<see cref="_spaCapture"/>, today's slice oracle) and the page view plus its skip
    /// identity (<see cref="_spaPageViews"/>, the composed-region producer).
    /// <para><b>Why the full-page linkify stays here.</b> The static page must not change a byte while the region
    /// path is being proven — the golden fingerprint is the gate and AC #5 inverts only once Task 2 ends. So this
    /// seam is deliberately NOT "linkify the region and assemble a page from it": it is the old behaviour, plus a
    /// second capture. The composed region is derived in <see cref="RegionCompositionDeltas"/> and consumed by
    /// <see cref="CapturedRegions"/>; nothing about the written document depends on it.</para>
    /// <para>Replaces the <c>WriteOutput(path, ApplyReferenceLinks(SomeTemplater.RenderPage(...), path))</c>
    /// idiom at every migrated call site. A page that is carried in verbatim from outside (Story 18.4's
    /// <c>forge-report.html</c>) has no <see cref="PageView"/> and stays on <see cref="WriteOutput"/>.</para>
    /// [Story 23.4 AC #3]</summary>
    /// <param name="linkify">False for the pages that deliberately do NOT run through
    /// <see cref="ApplyReferenceLinks"/> — see <see cref="CapturedPageView.Linkify"/>. The flag is carried into the
    /// capture so the composed region reproduces the same decision.</param>
    /// <returns>The finished document exactly as written — so a caller that must inspect its own output
    /// (<see cref="EnsureHierarchyEngine"/>'s host-marker scan) keeps byte-identical semantics instead of
    /// re-deriving the question from the view model.</returns>
    private string WritePage(
        PageView page,
        string? skipRequirementId = null,
        string? skipStoryId = null,
        int? skipEpicNumber = null,
        bool capture = true,
        bool linkify = true)
    {
        var path = page.OutputRelativePath;
        var rendered = HtmlRenderAdapter.Shared.Render(page).Content;
        var html = linkify
            ? ApplyReferenceLinks(
                rendered, path,
                skipRequirementId: skipRequirementId, skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber)
            : rendered;
        WriteOutput(path, html, capture);
        if (capture && _spaPageViews is not null)
        {
            // Composed HERE, in the same breath as the document's own linkify pass, so both observe identical
            // generator state. See CapturedPageView.Region for why deferring this is a defect and not an
            // optimisation. TrimEnd is replication of the slice's `</main>` boundary, not taste — same note.
            var region = JsonSpaRenderAdapter.Shared.RenderContent(page).TrimEnd();
            if (linkify)
            {
                region = ApplyReferenceLinks(
                    region, path,
                    skipRequirementId: skipRequirementId, skipStoryId: skipStoryId, skipEpicNumber: skipEpicNumber);
            }
            _spaPageViews[PathUtil.NormalizeSlashes(path)] = new CapturedPageView(page, region);
        }
        return html;
    }

    /// <summary>One page on which the composed region and the sliced region disagree — the unit of AC #1's
    /// "every non-zero delta enumerated with its cause and attributed".</summary>
    /// <param name="Path">The page's normalized output-relative path.</param>
    /// <param name="Composed">What <see cref="JsonSpaRenderAdapter.RenderContent"/> + <see cref="ApplyReferenceLinks"/>
    /// produce from the page's own view model — the Story 23.4 producer.</param>
    /// <param name="Sliced">What <see cref="SpaDelivery.ExtractContentRegion"/> produces from the rendered
    /// document — the pre-23.4 producer, and the oracle.</param>
    public readonly record struct RegionParityDelta(string Path, string Composed, string Sliced)
    {
        /// <summary>The 0-based index of the first differing char, or -1 when one string is a prefix of the
        /// other. Reported so a delta is diagnosable from the harness output without re-running a generate.</summary>
        public int FirstDifferenceAt
        {
            get
            {
                var shared = Math.Min(Composed.Length, Sliced.Length);
                for (var i = 0; i < shared; i++)
                {
                    if (Composed[i] != Sliced[i]) return i;
                }
                return Composed.Length == Sliced.Length ? -1 : shared;
            }
        }
    }

    /// <summary>Story 23.4 AC #3's byte-equality proof: for every captured page, compares the region COMPOSED from
    /// its <see cref="PageView"/> against the region SLICED out of its rendered document, and returns only the
    /// pages that disagree. An empty result is the licence to delete
    /// <see cref="SpaDelivery.ExtractContentRegion"/> and the full-page capture behind it.
    /// <para>⚠️ <b>This must be run against a real <c>--deep-git --spa</c> generate, not only the fixture.</b> The
    /// test fixture cites no real repo files, so it emits no <c>code/</c> page at all and exercises neither the
    /// 254-page <c>CodeFileTemplater</c> family nor the 300 <c>commit/</c> pages. A green fixture run is a
    /// necessary condition, never a sufficient one.</para>
    /// <para>Pages captured as HTML but with no view model are reported as deltas with an empty
    /// <see cref="RegionParityDelta.Composed"/> — a page still on the un-migrated write path is exactly the kind of
    /// silent gap this proof exists to catch, so it must not be skipped.</para></summary>
    public IReadOnlyList<RegionParityDelta> RegionCompositionDeltas()
    {
        lock (_gate)
        {
            if (_spaCapture is not { } capture || _spaPageViews is not { } views)
            {
                throw new InvalidOperationException(
                    "RegionCompositionDeltas requires a completed GenerateAll() pass with --spa (or CapturePages) "
                    + "on this generator, so both the sliced and the composed region are available to compare.");
            }
            var nav = _nav ?? throw new InvalidOperationException(
                "RegionCompositionDeltas requires a completed GenerateAll() pass on this generator.");

            var deltas = new List<RegionParityDelta>();
            foreach (var (path, fullHtml) in capture)
            {
                var normalized = PathUtil.NormalizeSlashes(path);
                var sliced = SpaDelivery.ExtractContentRegion(
                    fullHtml, CapturedNavMarkup(fullHtml, nav, normalized));
                var composed = views.TryGetValue(normalized, out var captured)
                    ? captured.Region
                    : string.Empty;
                if (!string.Equals(composed, sliced, StringComparison.Ordinal))
                {
                    deltas.Add(new RegionParityDelta(normalized, composed, sliced));
                }
            }
            return deltas.OrderBy(d => d.Path, StringComparer.Ordinal).ToList();
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
        // airtight (AC #4). The prelude and the family sequence are the SAME ones RenderWebviewSurfaces consumes
        // (Story 22.4 AC #1): there is no second builder to keep in step any more.
        var prelude = BuildSurfacePrelude(nav);
        foreach (var f in BuildFamilySurfaces(nav, prelude))
        {
            AddSpaSurface(pages, familyPaths, f.Page, f.SkipStoryId, f.SkipEpicNumber);
        }

        // 2) Every OTHER captured page: slice its content region via the landmark. Families are already covered
        // above (skipped here). The nav is re-rendered fresh (byte-identical to the page's own, minus the inline
        // toggle script the client owns); the wayfinding band + <main> come from the page's own captured output.
        // The breadcrumb is ALSO recovered structurally (from that same captured string — never re-read from
        // disk) so the manifest's drill parent/child data covers the whole site, not just the 5 view-model
        // families.
        //
        // The SPA consumes the shared producer UNFILTERED — including a page whose region degraded to nav-only,
        // which the webview drops. That asymmetry is deliberate and preserved: a browser tab is escapable, a
        // status panel claiming "links work" is not.
        foreach (var captured in CapturedRegions(nav, familyPaths))
        {
            pages.Add(new SpaPage(
                captured.Path, captured.Title, captured.Region, captured.Breadcrumb, captured.MetaDescription));
        }

        return new SpaBundle(_options.SiteTitle, "index.html", nav.Items, pages);
    }

    /// <summary>The nav markup a CAPTURED page's content region should carry: the page's OWN rendered
    /// <c>&lt;nav class="site-nav"&gt;</c>, sliced out of the captured string, falling back to a fresh re-render
    /// only when the page carries none.
    /// <para>Why the slice and not the re-render (Story 22.2 AC #5): the re-render path calls
    /// <c>nav.ToNavigationView(path)</c>, which takes no local-context argument, so every captured page lost the
    /// page-local context band its static twin shows — an ADR page shipped the generic key-views nav instead of
    /// its <c>aria-label="ADRs"</c> band (Story 23.1's enumerated difference #2). There is no path →
    /// <see cref="NavLocalContext"/> resolver to re-derive it from: every producer (ADRs, commit days, commits,
    /// insights, delivery, SDD, epics, requirements) builds one inline at render time and discards it, so
    /// threading it here would mean ~8 call sites of plumbing for a value the captured string already holds
    /// verbatim.</para>
    /// <para>Both callers pass the RESULT into <see cref="SpaDelivery.ExtractContentRegion"/>, which returns that
    /// same instance when a page carries no <c>&lt;main&gt;</c> landmark — the reference the webview loop's
    /// <c>ReferenceEquals</c> degrade check depends on. Keep it one instance per page.</para>
    /// [Story 22.2]</summary>
    private static string CapturedNavMarkup(string fullPageHtml, SiteNav nav, string normalizedPath) =>
        SpaDelivery.ExtractNavMarkup(fullPageHtml)
        ?? HtmlRenderAdapter.Shared.RenderNavMarkup(nav.ToNavigationView(normalizedPath));

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
        // A family page already carries its description structurally (PageView.MetaDescription, nullable) — no
        // extraction needed, and the IR's head projection resolves the same title fallback the static head does.
        pages.Add(new SpaPage(path, page.Title, region, page.Breadcrumb.Crumbs, page.MetaDescription));
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

        try
        {
            EmitDelta(dataFiles);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The delta sidecar is an OPTIONAL, additive, watch-only file (AC #2/#4) — a failure writing it (a
            // lost File.Move race under real concurrent watch activity, a locked temp file, a full disk) must
            // never turn an otherwise-successful IR/site emit into a reported Error for the calling route. NFR2:
            // degrade, never amplify. (code review, Story 22.6)
            Console.Error.WriteLine($"[delta] failed to write the watch-mode delta sidecar: {ex.Message}");
        }
    }

    /// <summary>Writes the watch-mode delta sidecar (<see cref="SpaDelivery.DeltaPath"/>) and advances the basis —
    /// AD-8's "sidecar polling" clause. Called from <see cref="EmitSpaSite"/> and nowhere else, so the basis can
    /// only ever advance when the IR was actually re-emitted. [Story 22.6 AC #2/#7]
    /// <para><b>The basis advances even when the sidecar is off.</b> Otherwise a run that toggled the switch
    /// mid-session would diff against a manifest from an arbitrary earlier point. Cheap — it is one string
    /// reference.</para></summary>
    private void EmitDelta(IReadOnlyList<SpaDelivery.OutputFile> dataFiles)
    {
        var manifestJson = dataFiles.First(f => f.OutputRelativePath == SpaDelivery.ManifestPath).Content;
        var previous = _previousManifestJson;
        _previousManifestJson = manifestJson;
        // Consumed exactly once, and cleared even when the sidecar is off — otherwise a topology rebuild with the
        // switch off would leave the flag armed and make some LATER, unrelated emit a spurious full marker.
        var forceFull = _nextEmitIsFullDelta;
        _nextEmitIsFullDelta = false;

        // A one-shot `generate --spa` writes NO delta at all: the document carries a wall clock, and a cold build
        // must stay byte-reproducible (NFR9). Gated on watch/serve, never on --spa. [AC #2/#4]
        if (!EmitDeltaSidecar) return;

        var delta = SpaDelivery.BuildDelta(
            previous,
            manifestJson,
            ++_deltaSequence,
            // No trigger set means no watcher event drove this pass — the session's own initial build, or a
            // programmatic caller. <directory change> is the established constant for "a whole-site rebuild not
            // attributable to one file", which is exactly that; reusing it adds no fourth spelling.
            _watchTrigger ?? FileWatcherService.TopologyEventLabel,
            DateTimeOffset.UtcNow,
            // A topology pass is a whole-site rebuild: a literal diff would produce a thousand-entry `changed`
            // list, larger and slower than the full payload it replaces. The signal is the FLAG the route itself
            // set, never the trigger label — see _nextEmitIsFullDelta for the live-caught race that decided this.
            //
            // A missing or unrecognized trigger deliberately does NOT force full. AC #7 enumerates the degrade
            // conditions and an unknown label is not among them: the basis's trustworthiness does not depend on
            // whether we can name what moved, and conflating a diagnostic gap with a correctness gap would make
            // every programmatic regen re-ship the whole IR for nothing. The genuinely basis-less case (a
            // session's first emit) is already covered inside BuildDelta by `previous is null`.
            forceFull);

        WriteSpaFileAtomic(SpaDelivery.DeltaPath, delta);
    }

    /// <summary>Writes one SPA output file so a CONCURRENT reader never observes a torn or partial document:
    /// content lands in a temp file beside the target, which is then atomically moved over it.
    /// <para>A sibling of <see cref="WriteSpaFile"/> rather than a change to it. That one uses a plain
    /// <see cref="File.WriteAllText(string,string)"/> — fine for the manifest and chunks, which no consumer polls
    /// while they are being written, and changing it would put a rename in the hot path of every one of this
    /// repo's ~1,400 IR pages for no benefit. The delta sidecar is the one file explicitly designed to be POLLED,
    /// so it is the one file that needs this.</para>
    /// <para>The temp file is created under the SAME directory as the target — a cross-volume
    /// <see cref="File.Move(string,string,bool)"/> degrades to copy+delete and stops being atomic — and is cleaned
    /// up on a failed move so a crashed write cannot litter the output root. NFR5's "never take a write lock on
    /// the watched tree" is untouched: this writes under the OUTPUT root, which is not watched.</para></summary>
    private void WriteSpaFileAtomic(string outputRelativePath, string content)
    {
        var full = Path.Combine(_options.OutputRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        var temp = full + ".tmp";
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, full, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best-effort cleanup */ }
            throw;
        }
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

            WritePage(HtmlTemplater.BuildDocPage(doc, nav));

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
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel, _requirements);
        var followUps = BuildFollowUpGeometry(inventory, counts);
        var unplanned = UnplannedWorkGeometry.From(inventory, followUps, _epicsModel, retros: _retros);
        // `_workGraph` is handed in verbatim — the SAME instance WriteWorkGraph renders — so the Story 20.3
        // related-work pane is a pure read over the already-computed model, never a second projection. [Story 20.3]
        var html = HtmlTemplater.RenderIndex(docs, nav, _progress ?? ProgressModel.Empty, _epicsModel, _requirements, _adrs, _module.Commands, inventory, _sprint, _retros, _coverage, _timelinePath is not null, CodeItemHref, counts, followUps, unplanned, cadence: _cadence, workGraph: _workGraph, dateCutoff: _today, testArtifacts: _testArtifacts);
        WriteTextWithRetry(indexPath, ApplyReferenceLinks(html, "index.html"));
        EnsureHierarchyEngine(html);
    }

    /// <summary>Pending asset-copy events, drained by <c>GenerateAll</c> after the Index phase. A conditional asset
    /// is discovered mid-page-write, where the surrounding method has no event list of its own; parking the outcome
    /// here keeps a failure visible on the diagnostics page instead of vanishing into a catch. Incremental/watch
    /// regeneration paths do not drain it (they surface no phase events either) — the next full run does.</summary>
    private readonly List<GenerationEvent> _pendingAssetEvents = new();

    private List<GenerationEvent> DrainPendingAssetEvents()
    {
        var drained = _pendingAssetEvents.ToList();
        _pendingAssetEvents.Clear();
        return drained;
    }

    /// <summary>Copies the vendored plotly.js hierarchy bundle to the output root — but ONLY once the rendered page
    /// proves it is needed, exactly as the Prism bundle ships only where in-portal code pages exist, "so a site
    /// with no code pages stays byte-identical". The gate reads the FINISHED HTML rather than a caller's belief
    /// about it, so it can never disagree with the page (the same rule <see cref="AssetManifest"/> follows).
    ///
    /// <para><b>Unconditional emission is not an option:</b> the bundle is 1.2 MB and would land in every fixture
    /// and every code-free site. Guarded like every other per-file write — a missing or misconfigured embedded
    /// resource degrades to a reported error rather than throwing out of the phase (NFR2). Idempotent, so Story
    /// 20.7's other surfaces can each call it. [Story 20.5]</para></summary>
    private void EnsureHierarchyEngine(string renderedHtml)
    {
        if (!HierarchyExplorer.ContainsHost(renderedHtml)) return;
        // The flag alone is NOT sufficient, and the difference is a real defect rather than a tidiness point: a
        // topology change during a watch session triggers a full rebuild that wipes and recreates the output root,
        // which deletes an asset this generator instance has already "copied". Trusting the flag there leaves the
        // freshly written index.html pointing at a 1.2 MB script that is no longer on disk — the chart silently
        // vanishes mid-session and only a hard regenerate brings it back. So the flag is an optimization (skip a
        // 1.2 MB write on every incremental pass) and the disk is the truth.
        var dest = Path.Combine(_options.OutputRoot, ForgeOptions.HierarchyEngineScriptName);
        if (_hierarchyEngineCopied && File.Exists(dest)) return;
        try
        {
            CopyEmbeddedAsset("SpecScribe.assets.plotly-hierarchy.min.js", ForgeOptions.HierarchyEngineScriptName);
            _hierarchyEngineCopied = true;
        }
        catch (Exception ex)
        {
            // DEDUPED BY PATH, deliberately. This list is drained only by GenerateAll, but EnsureHierarchyEngine is
            // reached from WriteIndex — which every incremental path calls (GenerateOne, RemoveFor, RegenerateEpics,
            // RegenerateAdrs). A persistently failing copy (locked output root, read-only disk) therefore appended
            // one record per debounced save for the life of a watch session, and the next full generate replayed
            // all of them onto its diagnostics page as though they had happened during that run. One record per
            // asset says exactly as much and stays bounded. [Story 20.5 review]
            if (!_pendingAssetEvents.Any(e => string.Equals(e.RelativePath, ForgeOptions.HierarchyEngineScriptName, StringComparison.Ordinal)))
            {
                _pendingAssetEvents.Add(new GenerationEvent(
                    GenerationOutcome.Error, ForgeOptions.HierarchyEngineScriptName, TimeSpan.Zero, ex.Message));
            }
        }
    }

    private bool _hierarchyEngineCopied;

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
    /// always reported), unsupported/skipped/informational shapes as <see cref="GenerationOutcome.Skipped"/>.
    /// Always non-fatal — the run has already continued past whatever these describe (AC #2). [Story 4.1]
    /// <para>The message is prefixed with the fine <see cref="AdapterDiagnosticCategory"/> word (e.g.
    /// <c>[Unsupported]</c>) so the coarse <see cref="GenerationOutcome"/> collapse (five categories → two
    /// outcomes) doesn't lose the distinction the Story 4.8 diagnostics page shows — including telling a benign
    /// <c>Informational</c> structural notice apart from a genuine <c>Unsupported</c> ingestion failure, even
    /// though both land on <see cref="GenerationOutcome.Skipped"/> [deferred-diagnostic-severity-bucketing].
    /// Additive and harmless on the console path (which already prints messages); recovered by
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
            d.RelativePath, TimeSpan.Zero, $"[{d.Category}] {d.Message}", FromAdapterDiagnostic: true, FromAdrDiagnostic: fromAdr,
            // A diagnostic that declares a non-default anchor keeps it; everything else stays on the
            // source-root contract via DiagnosticNotice's existing derivation. [Story 18.2]
            DiagnosticAnchor: d.Anchor == DiagnosticAnchorRoot.Source ? null : d.Anchor));

    /// <summary>Appends the Story 8.3 Unsupported count-divergence notice when the ledger is divergent.
    /// Shared by <see cref="GenerateAll"/> and <see cref="RegenerateEpics"/> so watch rebuilds re-emit.
    /// Callers must not invoke twice for the same run with the same list. [spec-epic8-deferred-debt-cleanup]</summary>
    private static void AppendCountDivergenceNotice(List<GenerationEvent> events, ProjectCounts counts)
    {
        if (!counts.HasDivergence) return;
        events.AddRange(MapDiagnostics(new[]
        {
            new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported,
                BmadArtifactAdapter.SprintStatusFileName,
                counts.DivergenceMessage()),
        }));
    }

    /// <summary>Appends the Story 18.6 <see cref="AdapterDiagnosticCategory.Informational"/> notice recording that
    /// the dashboard's Planning Artifacts panel was omitted because the detected primary module declares no
    /// modeled artifact-family set. Owner decision D3: the omission is SILENT on the page (no markup, no
    /// acknowledgement line, no new <c>DashboardView</c> field — the dashboard is already dense) but must not be
    /// invisible, so the diagnostics page explains it rather than the panel vanishing without trace.
    /// <para>Deliberately a SEPARATE notice from <see cref="ModuleContext"/>'s <c>ReportUnmodeledPrimary</c>,
    /// which covers the docs/glossary omission: the subject is a different surface, and <c>ModuleContext</c>
    /// should not learn what a dashboard panel is.</para>
    /// <para><b>Emitted from the GenerateAll events path only — never from <see cref="BuildArtifactCoverage"/>.</b>
    /// <see cref="RefreshCoverage"/> calls that on EVERY watch incremental (<c>GenerateOne</c> / <c>RemoveFor</c> /
    /// <c>RegenerateEpics</c> / <c>RegenerateAdrs</c>), so emitting there would accumulate a diagnostics row per
    /// keystroke — the exact failure ADR 0015 Decision 2d's at-most-one-per-run rule forbids. Unlike
    /// <see cref="AppendCountDivergenceNotice"/>, this one is NOT re-emitted from <c>RegenerateEpics</c>: the
    /// count ledger genuinely changes on a watch rebuild, whereas the detected module is fixed for the life of
    /// the run (detect-once-per-run, Story 18.2), so a re-emission could only duplicate a still-correct row.</para>
    /// [Story 18.6; ADR 0015 Decisions 2d + 5a]</summary>
    private static void AppendUnmodeledCoverageNotice(List<GenerationEvent> events, ModuleContext module)
    {
        if (!module.IsUnmodeled) return;

        var code = module.Code ?? string.Empty;
        events.AddRange(MapDiagnostics(new[]
        {
            new AdapterDiagnostic(
                AdapterDiagnosticCategory.Informational,
                // Reuses ModuleContext's centralized repo-relative path rather than re-literalizing it.
                ModuleContext.RepoRelativeCsv(code),
                $"Primary BMad module '{code}' ({module.Commands.ModuleLabel}) has no modeled "
                + "planning-artifact family set, so the dashboard's Planning Artifacts panel is omitted.",
                DiagnosticAnchorRoot.Repo),
        }));
    }

    /// <summary>One <see cref="AdapterDiagnosticCategory.Informational"/> notice per top-level SourceRoot folder
    /// outside the well-known set — the "unrecognized structure degrades, visibly" half of the grouping contract.
    /// Derived from SourceRoot relatives only. When <see cref="ForgeOptions.AdrSourceRoot"/> is outside SourceRoot
    /// (normal BMad: SourceRoot=<c>_bmad-output</c>, ADRs at <c>docs/adrs</c>), ADR files never appear here; if
    /// SourceRoot contains the ADR tree (e.g. repo-root as source), the first-segment folder (often <c>docs</c>)
    /// is still evaluated like any other SourceRoot top. A top-level folder whose files are ENTIRELY under a nested
    /// implementation-artifacts segment (e.g. <c>tracking/implementation-artifacts/1-1-x.md</c>) is excluded:
    /// those paths are already covered by the known Implementation Artifacts prefix, so flagging the wrapper as
    /// "unrecognized" would contradict Task 4's location tolerance.
    /// <para><see cref="AdapterDiagnosticCategory.Informational"/> (not <c>Unsupported</c>) deliberately keeps this
    /// benign "renders fine, just not in a well-known group" notice out of the same diagnostics-page bucket as a
    /// genuine per-artifact ingestion failure (e.g. an unusable <c>sprint-status.yaml</c>) — both are non-fatal,
    /// but only one needs a human's attention. [deferred-diagnostic-severity-bucketing]</para>
    /// [Story 4.2 Task 5] [Review][Patch] [spec-close-known-index-groups-misdiagnosis]</summary>
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
                AdapterDiagnosticCategory.Informational, f + "/",
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
        //
        // FANNED OUT over every epic a retro covers, not just one: a joint retrospective must appear against
        // ALL its epics, or the ones it misses read as "In review" forever (StatusStyles.ForEpicWithRetrospective
        // gates on HasRetrospective, which is stamped from this map). One retro can therefore be the value for
        // several keys — that is the point, not a duplicate. [spec-multi-epic-retro-attribution]
        // Winner is the LATEST retro for the epic, by DATE. Filename descending used to be a valid proxy for
        // that, because every retro for an epic shared the `epic-N-retro-` prefix so name order was date order.
        // Joint retros break that proxy: `epic-19-retro-2026-07-01` sorts ABOVE `epic-19-21-retro-2026-08-01`
        // ('r' > '2'), so the name-only tiebreak would hand Epic 19 its OLDER retrospective. Date first, then
        // filename descending as before, then Ordinal to break exact ties deterministically (OrdinalIgnoreCase
        // alone can tie two real files on a case-sensitive filesystem and leave the winner to enumeration
        // order — a nondeterminism the golden fingerprint would catch only intermittently).
        _epicRetroMap = _retros
            .SelectMany(r => r.EpicNumbers.Select(n => (Epic: n, Retro: r)))
            .GroupBy(x => x.Epic)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => PortalDates.TryParseDay(x.Retro.DateText ?? string.Empty, out var d) ? d : DateOnly.MinValue)
                      .ThenByDescending(x => x.Retro.SourceRelativePath, StringComparer.OrdinalIgnoreCase)
                      .ThenByDescending(x => x.Retro.SourceRelativePath, StringComparer.Ordinal)
                      .First().Retro.OutputRelativePath);
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
        var ordered = _retros.OrderBy(r => r.PrimaryEpicNumber ?? int.MaxValue).ToList();
        foreach (var retro in _retros)
        {
            var outputRel = retro.OutputRelativePath;
            var prefix = PathUtil.RelativePrefix(outputRel);
            var pager = EntityPager.FromSequence(ordered, ordered.FindIndex(r => ReferenceEquals(r, retro)),
                r => prefix + r.OutputRelativePath,
                r => r.Title);
            WritePage(RetroTemplater.BuildPage(retro, _epicsModel, nav, pager));
        }
    }

    /// <summary>Maps an epic number to the output path of its LATEST retrospective page (by authored date, then
    /// filename) — one retro can be the value for SEVERAL epics when it is a joint retrospective. This is the
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
        var work = WorkInventory.Build(_docs.Values.ToList());
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, work, _epicsModel, _requirements);
        var followUps = BuildFollowUpGeometry(work, counts);
        var unplanned = UnplannedWorkGeometry.From(work, followUps, _epicsModel, retros: _retros);
        WritePage(SprintTemplater.BuildIndexPage(
            _sprint, _epicsModel, nav, _module.Commands, _retros, counts, unplanned));
    }

    /// <summary>Writes <c>code-map.html</c> — the source-code treemap (Story 7.6). Builds the pure
    /// <see cref="CodeMap"/> over the cached source-code walk (<see cref="_codeFiles"/>) joined to the deep-git
    /// per-file metrics (<see cref="DeepGitPulse.CodeMapMetrics"/>, empty when <c>--deep-git</c> is off → sized-by-LOC
    /// with a neutral fill + the "git data unavailable" notice), computes the squarified layout, and renders. Gated
    /// on the same source-code signal as its nav item, so a Code Map link is never emitted to a page that wasn't
    /// produced. Wrapped never-throw → any failure degrades to "surface omitted, generation still succeeds"
    /// (AD-4 / NFR2), matching the old <c>WriteStructure</c> and every insight provider. Files route to their
    /// in-portal code page via the same guarded <see cref="CodeItemHref"/> resolver the deep-analytics and
    /// git-insights surfaces use. Returns the unfiltered ("full") variant's <see cref="CodeMap"/> (or
    /// <c>null</c> when the page was omitted) so <see cref="WriteRiskQuadrant"/> can reuse the exact same built
    /// map instead of re-walking <see cref="_codeFiles"/> a second time. [Story 7.6; Story 7.10 review-fix]</summary>
    private CodeMap? WriteCodeMap(SiteNav nav)
    {
        if (_codeFiles.Count == 0) return null;

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
        if (full is null || full.Map.IsEmpty) return null;

        var html = WritePage(CodeMapTemplater.BuildPage(variants, nav, fileHref: CodeItemHref));
        // Story 20.9: four Hierarchy Explorer instances live on this page, so the engine has to be on disk even in
        // a source-only repo with no epics and therefore no dashboard chart. See EnsureHierarchyEngine — the flag
        // is an optimization and the disk is the truth.
        EnsureHierarchyEngine(html);
        return full.Map;
    }

    /// <summary>Writes <c>risk-quadrant.html</c> — the refactor-target risk quadrant (Story 7.10). Takes the
    /// SAME unfiltered <see cref="CodeMap"/> <see cref="WriteCodeMap"/> already built (rather than re-walking
    /// <see cref="_codeFiles"/> + rejoining deep-git metrics a second time — a Story 7.10 review-fix; the two
    /// pages share one source-code walk, not two). <c>null</c>/empty (no source files, or <c>--deep-git</c> off
    /// enough that <see cref="WriteCodeMap"/> itself omitted) → this page omits too, never a broken link (shared
    /// with Code Map — <see cref="SiteNav.RiskQuadrantOutputPath"/>'s doc comment). Never-throw by construction:
    /// the only fallible step (building the map) already happened, and failed there, inside
    /// <see cref="WriteCodeMap"/>'s own try/catch (AD-4 / NFR2).</summary>
    private void WriteRiskQuadrant(SiteNav nav, CodeMap? fullMap)
    {
        if (fullMap is null || fullMap.IsEmpty) return;

        WritePage(RiskQuadrantTemplater.BuildPage(fullMap, nav, fileHref: CodeItemHref));
    }

    /// <summary>Writes <c>traceability.html</c> — the requirement × covering-epic traceability matrix (Story
    /// 21.1). Shares <see cref="SiteNav.RequirementsOutputPath"/>'s <c>hasEpics</c> availability guard (both are
    /// parsed out of epics.md), so this omits exactly when Requirements would. Needs <see cref="_counts"/> for
    /// its ledger-sourced legend/ranking caption — called after <c>_counts</c> is built in <see cref="GenerateAll"/>.</summary>
    private void WriteTraceability(SiteNav nav)
    {
        if (_epicsModel is null || _requirements is null || _counts is null) return;

        WritePage(TraceabilityTemplater.BuildPage(_requirements, _epicsModel, nav, _counts));
    }

    /// <summary>Writes <c>impact-map.html</c> — the planning ↔ code impact map (Story 21.3). Rides the combined
    /// <c>hasEpics &amp;&amp; hasDeepAnalytics</c> gate the nav entry uses: written when an epics roster exists AND
    /// <c>--deep-git</c> ran (<c>_progress.DeepGit is not null</c>) — the structural condition, matching
    /// <see cref="SiteNav.Build"/>'s combined gate exactly. The gate is deep-git having run at all, NOT whether
    /// anything correlated: when it ran but nothing matched, the page still writes with <see cref="Charts.ImpactMapBody"/>'s
    /// honest empty note (AC #2's data-thin degrade, the same split Traceability uses). Renders the SAME cached
    /// <see cref="_planningImpact"/> the epic/story widgets consumed, so page and widgets can never disagree.</summary>
    private void WriteImpactMap(SiteNav nav)
    {
        if (_epicsModel is null || _progress?.DeepGit is null) return;

        WritePage(ImpactMapTemplater.BuildPage(_epicsModel, _planningImpact, nav));
    }

    /// <summary>Projects the epic-scoped work graph (Story 19.2) from already-parsed models. Reads
    /// deferred/quick-dev from <see cref="ResolveFollowUpWork"/> (source-backed, since <c>_docs</c> may be empty at
    /// nav-build time) and builds the same ledger-backed <see cref="FollowUpGeometry"/> the sunburst uses — never a
    /// second parse or count. Returns <see cref="WorkGraphModel.Empty"/> on any failure so a malformed note never
    /// fails generation (AD-4 / NFR2). Called once, before nav, so the gate and the write agree.</summary>
    private WorkGraphModel BuildWorkGraphModel(
        EpicsModel? epics, ProgressModel? progress, RequirementsModel? requirements, IReadOnlyList<string> files)
    {
        if (epics is null || epics.Epics.Count == 0) return WorkGraphModel.Empty;
        try
        {
            var work = ResolveFollowUpWork(files);
            var counts = ProjectCounts.Build(progress ?? ProgressModel.Empty, _sprint, work, epics, requirements);
            // ResolveDeferredModel (not TryParseDeferredWork) so the deferred note is parsed from SOURCE when _docs
            // isn't populated yet — this runs before the pages loop, so without it the graph would see zero deferred
            // provenance and draw only action items. [Story 19.2]
            var deferredModel = ResolveDeferredModel(work, files);
            var geometry = FollowUpGeometry.From(
                _sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
                counts, work, linkPrefix: "", deferredModel, epics, _retros);
            return WorkGraphBuilder.Build(epics, geometry, _epicRetroMap);
        }
        catch
        {
            return WorkGraphModel.Empty;
        }
    }

    /// <summary>The already-projected epic subgraph for an epic's own page tab (Story 19.2) — root-relative; the
    /// templater re-prefixes it for the <c>epics/</c> page depth. Null when the epic carries no graph signal (no
    /// tab is shown). Excludes the synthetic Unattributed bucket (it has no epic page).</summary>
    private WorkGraphEpic? EpicSubgraph(int epicNumber) =>
        _workGraph.Epics.FirstOrDefault(e => e.BucketLabel is null && e.EpicNumber == epicNumber);

    /// <summary>A story-scoped provenance subgraph for a story's own page tab (Story 19.2): the deferred work that
    /// stemmed from this story + resolvers. Null when nothing stemmed from it (no tab). Built on demand from the
    /// caller's geometry (root-relative; the templater re-prefixes).</summary>
    private static WorkGraphEpic? StorySubgraph(EpicInfo epic, StoryInfo story, FollowUpGeometry? followUps) =>
        WorkGraphBuilder.BuildStory(story, PathUtil.StripHtmlTags(epic.Title), followUps);

    /// <summary>Writes <c>work-graph.html</c> — the epic-scoped provenance subgraph page (Story 19.2) — from the
    /// <see cref="_workGraph"/> model already projected + gated before nav. Empty model → no page (NFR8; the nav
    /// entry was omitted on the same gate). NOT reference-linkified: the SVG carries deferred/action summaries in
    /// <c>aria-label</c>/<c>title</c> attributes that may name "Epic N"/"Story N.M", which the linkifier would wrap
    /// in <c>&lt;a&gt;</c> INSIDE the attribute and corrupt (same reason <see cref="WriteActionItems"/> skips it) —
    /// the graph's own node links already carry navigation. Rides <see cref="WriteOutput"/> so SPA capture is
    /// automatic.</summary>
    private void WriteWorkGraph(SiteNav nav)
    {
        if (_workGraph.IsEmpty) return;
        WritePage(WorkGraphTemplater.BuildPage(_workGraph, nav), linkify: false);
    }

    /// <summary>Writes <c>ideas.html</c>, one <c>ideas/{slug}.html</c> per discovered forge session, and each
    /// idea's carried-over <c>ideas/{slug}-report.html</c>. Gated on the SAME non-empty
    /// <see cref="_ideas"/> model the nav entry gates on, so no forge workspace ⇒ no page, no nav entry, no quick
    /// link (AC #3 / NFR8 — absent, not an empty page).
    /// <para>The carried report is written STRAIGHT through <see cref="WriteOutput"/> as a leaf: it is a complete
    /// foreign <c>&lt;html&gt;</c> document with its own inline CSS, so wrapping it in
    /// <see cref="HtmlTemplater.RenderPage"/> would nest documents. <see cref="WriteOutput"/> already takes a
    /// <c>string</c> and creates the directory, so "carry the report" needs no new copy mechanism and no asset
    /// pipeline — but it IS written with <c>capture: false</c>, because a foreign document carries no
    /// <c>&lt;main id="main-content"&gt;</c> landmark for the SPA/webview slicer. Its safety gate (AC #6) and size
    /// cap ran in <see cref="IdeaDiscovery"/>: a report that failed either has a null
    /// <see cref="IdeaEntry.CarriedReportHtml"/> here, and the detail page simply omits the link.</para>
    /// [Story 18.4]</summary>
    private IReadOnlyList<GenerationEvent> WriteIdeas(SiteNav nav)
    {
        var events = new List<GenerationEvent>();
        if (_ideas.IsEmpty) return events;

        var linkDiagnostics = new List<AdapterDiagnostic>();
        var resolved = new IdeasModel(
            _ideas.Ideas.Select(i => i with
            {
                ForwardLinks = ResolveForwardLinks(i, linkDiagnostics),
                ForgedIdeaHtml = i.ForgedIdeaHtml is { Length: > 0 } handoff
                    ? RewriteHandoffLinks(handoff, i.WorkspaceSourceRelative)
                    : i.ForgedIdeaHtml,
            }).ToList())
        { WorkspaceSourceRelatives = _ideas.WorkspaceSourceRelatives };
        _ideas = resolved;
        events.AddRange(MapDiagnostics(linkDiagnostics));

        // [Story 18.4 review] Detail pages (+ carried reports) write FIRST, and only the ideas that actually made
        // it to disk are handed to the list page below — so a mid-loop write failure can never leave ideas.html
        // linking to a detail page that was never written. Same discipline the report-before-detail ordering
        // already applied one level down.
        var writtenIdeas = new List<IdeaEntry>();
        foreach (var idea in resolved.Ideas)
        {
            var detailSw = Stopwatch.StartNew();
            try
            {
                // Report FIRST, so a failed carry can never leave the detail page linking a file that isn't there.
                if (idea is { ReportOutputPath: { } reportPath, CarriedReportHtml: { } reportHtml })
                {
                    // capture: false — see WriteOutput's <param>. A foreign document has no <main id="main-content">
                    // for the SPA/webview slicer to find, so capturing it would ship a content-empty route.
                    WriteOutput(reportPath, reportHtml, capture: false);
                }

                WritePage(IdeasTemplater.BuildDetailPage(idea, nav));
                events.Add(new GenerationEvent(GenerationOutcome.Generated, idea.DetailOutputPath, detailSw.Elapsed));
                writtenIdeas.Add(idea);
            }
            catch (Exception ex)
            {
                events.Add(new GenerationEvent(GenerationOutcome.Error, idea.DetailOutputPath, detailSw.Elapsed, ex.Message));
            }
        }

        var listModel = writtenIdeas.Count == resolved.Ideas.Count
            ? resolved
            : new IdeasModel(writtenIdeas) { WorkspaceSourceRelatives = resolved.WorkspaceSourceRelatives };

        var sw = Stopwatch.StartNew();
        try
        {
            WritePage(IdeasTemplater.BuildListPage(listModel, nav));
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.IdeasOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.IdeasOutputPath, sw.Elapsed, ex.Message));
        }

        return events;
    }

    /// <summary>Writes <c>test-artifacts.html</c> when a methodology module contributed any artifact. Gated on the
    /// SAME non-empty <see cref="_testArtifacts"/> model the nav entry and the dashboard panel gate on, so no
    /// module artifacts ⇒ no page, no nav entry, no quick link, no panel (NFR8 — absent, not an empty page). A
    /// BMM-only repo never reaches the write.
    ///
    /// <para><b>No markdown is consumed here, deliberately.</b> Every markdown artifact keeps the page the generic
    /// <c>*.md</c> pass writes for it, and this page LINKS that page rather than re-rendering it — which is what
    /// makes the <see cref="CoverageTier.Rendered"/> label true. Adding these paths to
    /// <c>ArtifactBundle.ConsumedSourceRelatives</c> (the shape Story 18.4 needed, because an idea's detail page
    /// re-renders its workspace markdown) would suppress exactly the page each row links to and turn every
    /// <c>Rendered</c> row into a dangling link. The hazard that instruction guards against — one artifact rendered
    /// twice — is discharged by not rendering it twice, and pinned by a test.</para> [Story 18.5]</summary>
    private IReadOnlyList<GenerationEvent> WriteTestArtifacts(SiteNav nav)
    {
        var events = new List<GenerationEvent>();
        if (_testArtifacts.IsEmpty) return events;

        var sw = Stopwatch.StartNew();
        try
        {
            WritePage(TestArtifactsTemplater.BuildListPage(_testArtifacts, nav));
            events.Add(new GenerationEvent(GenerationOutcome.Generated, SiteNav.TestArtifactsOutputPath, sw.Elapsed));
        }
        catch (Exception ex)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, SiteNav.TestArtifactsOutputPath, sw.Elapsed, ex.Message));
        }

        return events;
    }

    /// <summary>Completes AC #2's forward links from the ONLY two evidence sources §9 admits, in order:
    /// (1) a markdown link inside <c>forged-idea.md</c> that resolves to a source file which actually HAS a
    /// generated page, and (2) a downstream doc whose frontmatter <c>sources:</c> names this workspace or its
    /// <c>forged-idea.md</c>. Anything else — slug-vs-title similarity, date proximity, folder-name resemblance —
    /// is forbidden (owner decision D4): a false provenance chain is worse than none, and Story 21.1's review
    /// already caught that class (a "phantom-covered" requirement counted covered and drawn blank). No evidence ⇒
    /// an empty list ⇒ NO forward-link element at all (NFR8: absent, not "none found").</summary>
    private IReadOnlyList<IdeaForwardLink> ResolveForwardLinks(IdeaEntry idea, List<AdapterDiagnostic> diagnostics)
    {
        var links = new List<IdeaForwardLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sourceRelative, label) in idea.ForwardLinkCandidates)
        {
            if (ResolveSourceRelativeToPage(sourceRelative) is not { } outputPath)
            {
                diagnostics.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped,
                    $"{idea.WorkspaceSourceRelative}/{IdeaDiscovery.ForgedIdeaFileName}",
                    $"Forward link '{sourceRelative}' from idea '{idea.Slug}' has no generated page; the link is omitted."));
                continue;
            }

            if (seen.Add(outputPath))
            {
                links.Add(new IdeaForwardLink(label, outputPath, $"Linked from {IdeaDiscovery.ForgedIdeaFileName}"));
            }
        }

        // (2) The reverse direction: a brief/PRD/spec that declares this workspace in its own `sources:` list.
        // Frontmatter already models Sources, so this is a read of parsed state, never a new scan.
        var workspaceKey = PathUtil.NormalizeSlashes(idea.WorkspaceSourceRelative);
        foreach (var doc in _docs.Values.OrderBy(d => d.OutputRelativePath, StringComparer.Ordinal))
        {
            foreach (var source in doc.Frontmatter.Sources)
            {
                var normalized = PathUtil.NormalizeSlashes(source.Trim()).TrimStart('.', '/');
                if (normalized.Length == 0) continue;
                if (!normalized.Equals(workspaceKey, StringComparison.OrdinalIgnoreCase)
                    && !normalized.StartsWith(workspaceKey + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var outputPath = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
                if (seen.Add(outputPath))
                {
                    links.Add(new IdeaForwardLink(doc.Title, outputPath, $"Declared in this document's sources: {source.Trim()}"));
                }
                break;
            }
        }

        return links;
    }

    private static readonly Regex HandoffAnchorPattern = new(
        @"<a\s+href=""(?<href>[^""]*)""(?<rest>[^>]*)>(?<text>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>Re-points every in-tree link inside a rendered <c>forged-idea.md</c> body at the page its target
    /// actually produced, and UNWRAPS any link whose target has no page into plain text. Without this the hand-off
    /// block would ship the author's raw <c>../../planning-artifacts/epics.md</c> hrefs verbatim — dead links on
    /// every hardened idea's page. Links are resolved through
    /// <see cref="IdeaDiscovery.TryResolveWorkspaceHref"/>, the SAME path math the forward-link harvest uses, and
    /// keyed off the RENDERED href rather than the markdown source so a Markdig-normalized href still matches.
    /// External URLs and in-page anchors pass through untouched. Detail pages live under <c>ideas/</c>, so a
    /// resolved root-relative page path is emitted with a <c>../</c> prefix. [Story 18.4 §9]</summary>
    private string RewriteHandoffLinks(string html, string workspaceRelative)
    {
        const string prefix = "../";
        return HandoffAnchorPattern.Replace(html, m =>
        {
            var href = System.Net.WebUtility.HtmlDecode(m.Groups["href"].Value);
            if (!IdeaDiscovery.TryResolveWorkspaceHref(workspaceRelative, href, out var sourceRelative))
            {
                return m.Value; // external / mailto / in-page anchor — not ours to touch
            }

            if (ResolveSourceRelativeToPage(sourceRelative) is { } outputPath)
            {
                return $"<a href=\"{PathUtil.Html(prefix + outputPath)}\"{m.Groups["rest"].Value}>{m.Groups["text"].Value}</a>";
            }

            // NFR8: the words the author wrote survive; only the dead affordance goes.
            return $"<span class=\"idea-link-unresolved\">{m.Groups["text"].Value}</span>";
        });
    }

    /// <summary>The output page for a source-relative path, or null when that source produced no page. Honours the
    /// same special routing <see cref="ResolveFamilyHref"/> uses — epics.md and the requirements catalog are
    /// rendered as curated pages, not generic ones — so a forward link into either lands where a reader expects
    /// rather than on a 404. [Story 18.4 §9; reuses Story 18.3 §7's resolution chain]</summary>
    private string? ResolveSourceRelativeToPage(string sourceRelative)
    {
        var normalized = PathUtil.NormalizeSlashes(sourceRelative);
        if (BmadArtifactAdapter.IsEpicsFile(normalized))
        {
            return _epicsModel is not null ? SiteNav.EpicsOutputPath : null;
        }

        // [Story 18.4 review] Retros are consumed out of _docs (like epics.md) but DO each get their own page —
        // a link into one fell through to the final _docs check and was always treated as dead. (ADRs are NOT
        // checked here: they live outside SourceRoot, in docs/adrs/, and the only two callers of this method
        // resolve hrefs through IdeaDiscovery.TryResolveWorkspaceHref, which refuses to resolve anything that
        // escapes SourceRoot — so a real ADR reference can never reach this method in the first place.)
        foreach (var retro in _retros)
        {
            if (string.Equals(PathUtil.NormalizeSlashes(retro.SourceRelativePath), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return PathUtil.NormalizeSlashes(retro.OutputRelativePath);
            }
        }

        // A link INTO another idea's workspace routes to that idea's detail page — its workspace markdown was
        // consumed by that page and has no generic page of its own.
        foreach (var idea in _ideas.Ideas)
        {
            if (normalized.StartsWith(idea.WorkspaceSourceRelative + "/", StringComparison.OrdinalIgnoreCase))
            {
                return idea.DetailOutputPath;
            }
        }

        return _docs.Keys.Any(k => string.Equals(PathUtil.NormalizeSlashes(k), normalized, StringComparison.OrdinalIgnoreCase))
            ? PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(normalized))
            : null;
    }

    /// <summary>Writes <c>cadence.html</c> — the delivery-cadence page (story-completion heatmap + cycle-time
    /// histogram, Story 21.2). Shares <see cref="SiteNav.RequirementsOutputPath"/>'s <c>hasEpics</c> gate (the
    /// cadence reads the epics roster's done-story dates), so it omits exactly when Epics/Requirements would; when
    /// <c>hasEpics</c> is true but no story is done yet, the page still writes with its honest empty-state charts.
    /// Reads the shared <see cref="_cadence"/> dataset (built once in <see cref="GenerateAll"/>). Bounds the heatmap
    /// grid with the single per-run "today" so a from-scratch regen is byte-identical (FR31).</summary>
    private void WriteCadence(SiteNav nav)
    {
        if (_epicsModel is null || _cadence is null) return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        WritePage(CadenceTemplater.BuildPage(_cadence, nav, today));
    }
    /// <summary>Writes the retrospectives index (<c>retros.html</c>) when any retro exists — the target of the
    /// sprint page's "Retros" link. [Story 2.3 polish #5]</summary>
    private void WriteRetroIndex(SiteNav nav)
    {
        if (_retros.Count == 0) return;
        WritePage(RetroTemplater.BuildIndexPage(_retros, nav));
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
        var counts = _counts ?? ProjectCounts.Build(_progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel, _requirements);
        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, _docs.Values);
        WritePage(
            ActionItemsTemplater.BuildPage(
                open, EpicRetroMap, _module.Commands, nav, deferredHref, counts, _epicsModel, hrefMap,
                allActionItemsForSlugs: _sprint?.ActionItems),
            linkify: false);
    }

    /// <summary>Rebuilds and rewrites the date pages + activity timeline (Story 7.3) from current
    /// on-disk/git state. Shared by <see cref="GenerateAll"/>, <see cref="GenerateOne"/>, and
    /// <see cref="RegenerateEpics"/> — previously only <see cref="GenerateAll"/> ran this block, so a watch-mode
    /// edit left the timeline/date pages stale until the next full generate. Reuses whatever <see cref="_progress"/>
    /// already holds (never re-runs git); date pages are generated for the UNION of the git commit days and the
    /// days any recognized artifact was last touched, so an artifact-only day still gets a page for the timeline to
    /// link to (<see cref="ActivityModel.UnionDays"/>). Everything degrades non-fatally: no git AND no artifacts →
    /// no pages, no timeline, no dashboard link, no error (AC #2). [spec-7-3-deferred-debt-cleanup]</summary>
    private void RefreshDatePagesAndTimeline(SiteNav nav, List<GenerationEvent> events, IGenerationReporter? reporter = null)
    {
        _timelinePath = null;
        var artifactsByDay = BuildArtifactsByDay(events);
        var gitPulse = _progress?.Git;
        if (gitPulse is null && artifactsByDay.Count == 0) return;

        reporter?.BeginPhase(GenerationPhase.CommitDays);
        events.AddRange(GenerateDatePagesInternal(gitPulse, artifactsByDay, nav, CommitHref));
        reporter?.EndPhase(GenerationPhase.CommitDays);

        GenerateTimelineInternal(gitPulse, artifactsByDay, nav, events, reporter);
    }

    /// <summary>Rewrites deferred list + follow-up detail/group pages + quick-dev chrome from current
    /// on-disk deferred content. Shared by <see cref="GenerateAll"/>, <see cref="GenerateOne"/>, and
    /// <see cref="RegenerateEpics"/> so watch edits don't leave deep links stale.
    /// Returns the inventory used for the writes (callers can pass it to <see cref="WriteIndex"/>).
    /// [spec-epic9-watch-followup-surface-refresh]</summary>
    private WorkInventory RefreshFollowUpSurfaces(
        SiteNav nav,
        WorkInventory? work = null,
        IReadOnlyList<string>? sourceFiles = null)
    {
        if (work is null)
        {
            SyncDeferredDocFromDisk(sourceFiles);
            work = WorkInventory.Build(_docs.Values.ToList());
            // Watch paths may still hold a GenerateAll-era ledger; rebuild so open tallies match the note.
            _counts = ProjectCounts.Build(
                _progress ?? ProgressModel.Empty, _sprint, work, _epicsModel, _requirements);
        }

        WriteDeferredWork(nav, work);
        WriteFollowUpDetails(nav, work);
        WriteFollowUpGroupPages(nav, work);
        RewriteQuickDevPages(nav, work);
        return work;
    }

    /// <summary>Re-converts <c>deferred-work.md</c> into <see cref="_docs"/> so open tallies and
    /// <see cref="TryParseDeferredWork"/> fallback BodyHtml match the on-disk note after watch edits.
    /// Clears prior deferred-work <see cref="_docs"/> entries first so a deleted or moved note cannot
    /// leave stale inventory for the writers. Unreadable note: leave cleared (NFR2).
    /// [spec-epic9-watch-followup-surface-refresh]</summary>
    private void SyncDeferredDocFromDisk(IReadOnlyList<string>? sourceFiles = null)
    {
        foreach (var key in _docs.Keys.Where(IsDeferredWorkDocKey).ToList())
            _docs.Remove(key);

        var files = sourceFiles ?? EnumerateSourceFiles();
        foreach (var file in files)
        {
            var relative = ToSourceRelative(file);
            var norm = PathUtil.NormalizeSlashes(relative);
            if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) continue;
            var slash = norm.LastIndexOf('/');
            var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
            if (!string.Equals(fileName, "deferred-work.md", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                if (!File.Exists(file)) return;
                var outputRelative = PathUtil.ToOutputRelative(relative);
                _docs[relative] = MarkdownConverter.Convert(file, relative, outputRelative);
            }
            catch (IOException) { /* NFR2 — inventory stays without deferred */ }
            catch (UnauthorizedAccessException) { /* NFR2 */ }
            return;
        }
    }

    private static bool IsDeferredWorkDocKey(string sourceRelative)
    {
        var norm = PathUtil.NormalizeSlashes(sourceRelative);
        var slash = norm.LastIndexOf('/');
        var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
        return string.Equals(fileName, "deferred-work.md", StringComparison.OrdinalIgnoreCase);
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
        // NOT reference-linkified: the list-batch pane's Address/Close data-copy payloads embed raw item
        // text (which can contain "Story N.M"/"Epic N"/"FR-N" mentions) — the linkifier would wrap those
        // in <a> tags INSIDE the attribute value and corrupt the copyable command, the same trap
        // WriteActionItems already avoids. [spec-follow-up-list-batch-actions]
        WritePage(
            DeferredWorkTemplater.BuildPage(
                model, nav, outputPath, doc.Title, _module.Commands, _epicsModel, hrefMap),
            linkify: false);
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
        WritePage(DiagnosticsTemplater.BuildPage(notices, config, nav), linkify: false);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.DiagnosticsOutputPath, sw.Elapsed);
    }

    /// <summary>Writes <c>about.html</c> — SpecScribe's own product-metadata page (version/description/author/
    /// repository, read from the assembly) plus the prominent link to the diagnostics run log. Static (no run
    /// dependency); written alongside the diagnostics page on every full run so the footer's About link always
    /// resolves. Returns its own <see cref="GenerationOutcome.Generated"/> event. [Story 4.8 Task 6]</summary>
    private GenerationEvent WriteAbout(SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        WritePage(AboutTemplater.BuildPage(nav), linkify: false);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.AboutOutputPath, sw.Elapsed);
    }

    /// <summary>Writes <c>how-to-read.html</c> — How to use SpecScribe (reading order + glossary). Static;
    /// written on every full run alongside About/Diagnostics. Deliberately NOT run through
    /// <see cref="ApplyReferenceLinks"/> — it defines the glossary terms, so it must not self-expand them
    /// into nested &lt;abbr&gt;. [Story 10.3; How to use SpecScribe]</summary>
    private GenerationEvent WriteHowToRead(SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        WritePage(HowToReadTemplater.BuildPage(nav, _module), linkify: false);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.HowToReadOutputPath, sw.Elapsed);
    }

    /// <summary>Writes <c>design-system.html</c> — the portal's status/motion token families and shared visual
    /// primitives. Static (no run dependency); written on every full run alongside About/How-to-read so its
    /// Help-nav link always resolves. Rides <see cref="WriteOutput"/> so the SPA and webview
    /// <c>CapturePages</c> forms pick it up like any other page. Deliberately NOT run through
    /// <see cref="ApplyReferenceLinks"/> — the page's subject IS the portal's vocabulary, so it must not
    /// self-expand its own terms into reference chips. [Story 23.2 AC #6]</summary>
    private GenerationEvent WriteDesignSystem(SiteNav nav)
    {
        var sw = Stopwatch.StartNew();
        WritePage(DesignSystemTemplater.BuildPage(nav), linkify: false);
        return new GenerationEvent(GenerationOutcome.Generated, SiteNav.DesignSystemOutputPath, sw.Elapsed);
    }

    /// <summary>Writes About Spec-Driven Development hub + framework sub-pages. [About SDD]</summary>
    private IEnumerable<GenerationEvent> WriteAboutSdd(SiteNav nav)
    {
        var methodPresent = ModuleContext.IsMethodPresent(_options.RepoRoot);
        var gdsPresent = ModuleContext.IsGdsPresent(_options.RepoRoot);

        var sw = Stopwatch.StartNew();
        WritePage(AboutSddTemplater.BuildHubPage(nav, methodPresent, gdsPresent), linkify: false);
        yield return new GenerationEvent(GenerationOutcome.Generated, SiteNav.AboutSddOutputPath, sw.Elapsed);

        foreach (var fw in AboutSddTemplater.Frameworks)
        {
            sw.Restart();
            WritePage(
                AboutSddTemplater.BuildFrameworkPage(nav, fw.Id, methodPresent, gdsPresent), linkify: false);
            yield return new GenerationEvent(GenerationOutcome.Generated, fw.OutputPath, sw.Elapsed);
        }
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
            WritePage(HtmlTemplater.BuildDocPage(doc, nav));
            return new GenerationEvent(GenerationOutcome.Generated, "README.md", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new GenerationEvent(GenerationOutcome.Error, "README.md", sw.Elapsed, ex.Message);
        }
    }

    /// <summary>Writes filtered follow-up group list pages under <c>follow-ups/group-*.html</c>
    /// (Follow-ups orphan, Unplanned, epic-N). Membership matches sunburst sets; no empty pages (NFR8).
    /// No Resolve <c>data-copy</c> on group pages. [Story 9.13]</summary>
    private void WriteFollowUpGroupPages(SiteNav nav, WorkInventory? work = null)
    {
        var inventory = work ?? WorkInventory.Build(_docs.Values.ToList());
        var counts = _counts ?? ProjectCounts.Build(
            _progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel, _requirements);
        var deferredModel = TryParseDeferredWork(inventory);
        var followUps = BuildFollowUpGeometry(inventory, counts, deferredModel);
        var unplanned = UnplannedWorkGeometry.From(inventory, followUps, _epicsModel, retros: _retros);
        var groups = FollowUpGroupPages.Enumerate(followUps, unplanned, _epicsModel);

        var followUpsDir = Path.Combine(_options.OutputRoot, FollowUpSlug.Folder);
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groups.Count > 0)
            Directory.CreateDirectory(followUpsDir);

        foreach (var group in groups)
        {
            // No ApplyReferenceLinks — the list-batch pane's data-copy payloads embed raw item text
            // (same corruption trap as WriteActionItems / WriteDeferredWork); row summaries stay plain.
            WritePage(FollowUpGroupTemplater.BuildPage(group, nav, _module.Commands), linkify: false);
            emitted.Add(Path.GetFileName(group.OutputPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        // Prune stale group-* pages when membership shrinks (watch + full generate without wipe of follow-ups/).
        if (Directory.Exists(followUpsDir))
        {
            foreach (var file in Directory.EnumerateFiles(followUpsDir, "group-*.html"))
            {
                var name = Path.GetFileName(file);
                if (emitted.Contains(name)) continue;
                File.Delete(file);
                if (_spaCapture is not null)
                    _spaCapture.Remove(PathUtil.NormalizeSlashes(Path.Combine(FollowUpSlug.Folder, name)));
                _spaPageViews?.Remove(PathUtil.NormalizeSlashes(Path.Combine(FollowUpSlug.Folder, name)));
            }
        }
    }

    /// <summary>Re-renders quick-dev (<c>route: one-shot</c>) doc pages with parent breadcrumb + reverse
    /// deferred panel once follow-up geometry is known. Runs after group pages so Unplanned hrefs exist when
    /// membership is non-empty. [artifact-review-nav-and-deferred]</summary>
    private void RewriteQuickDevPages(SiteNav nav, WorkInventory work)
    {
        var counts = _counts ?? ProjectCounts.Build(
            _progress ?? ProgressModel.Empty, _sprint, work, _epicsModel, _requirements);
        var deferredModel = TryParseDeferredWork(work);
        var followUps = BuildFollowUpGeometry(work, counts, deferredModel);
        var unplanned = UnplannedWorkGeometry.From(work, followUps, _epicsModel, retros: _retros);

        foreach (var doc in _docs.Values)
        {
            var chrome = BuildQuickDevChrome(doc, followUps, unplanned, _epicsModel, _retros);
            if (chrome is null) continue;
            var outputRelative = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
            WritePage(HtmlTemplater.BuildDocPage(doc, nav, quickDev: chrome));
        }
    }

    /// <summary>Builds optional chrome for a quick-dev doc page, or null for ordinary docs.</summary>
    private static HtmlTemplater.QuickDevPageChrome? BuildQuickDevChrome(
        DocModel doc,
        FollowUpGeometry followUps,
        UnplannedWorkGeometry unplanned,
        EpicsModel? epics,
        IReadOnlyList<RetroModel>? retros = null)
    {
        var norm = PathUtil.NormalizeSlashes(doc.SourceRelativePath);
        if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) return null;
        var slash = norm.LastIndexOf('/');
        var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
        if (!fileName.StartsWith("spec-", StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.Equals(doc.Frontmatter.Route?.Trim(), "one-shot", StringComparison.OrdinalIgnoreCase))
            return null;

        var output = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
        var prefix = PathUtil.RelativePrefix(output);
        var stem = Path.GetFileNameWithoutExtension(output);
        var deferred = followUps.DeferredForSource(stem, prefix);

        var entry = new QuickDevEntry(
            doc.Title, output, doc.Frontmatter.Status, doc.Frontmatter.Type, doc.Frontmatter.AuthoredDay());
        var epicNum = UnplannedWorkGeometry.ResolveQuickDevEpic(entry, epics, followUps, retros);

        var deferredListHref = followUps.DeferredHref is { Length: > 0 } dh
            ? FollowUpGeometry.ApplyLinkPrefix(prefix, dh)
            : null;

        if (epicNum is { } en && epics is not null)
        {
            var epic = epics.Epics.FirstOrDefault(e => e.Number == en);
            if (epic is not null)
            {
                return new HtmlTemplater.QuickDevPageChrome(
                    deferred,
                    EpicNumber: en,
                    EpicCrumbLabel: EpicsTemplater.EpicCrumbLabel(epic),
                    EpicHref: $"epics/epic-{en}.html",
                    DeferredListHref: deferredListHref);
            }
        }

        string? unplannedHref = null;
        if (unplanned.HasUnplanned)
            unplannedHref = FollowUpGroupPages.UnplannedPath;

        return new HtmlTemplater.QuickDevPageChrome(
            deferred, UnplannedHref: unplannedHref, DeferredListHref: deferredListHref);
    }

    /// <summary>Writes one detail page per action item and deferred-work item under
    /// <c>follow-ups/{slug}.html</c>, mirroring <see cref="WriteRequirements"/>. Rides
    /// <see cref="WriteOutput"/> so SPA/webview capture picks them up. Neither action nor deferred
    /// detail pages run through <see cref="ApplyReferenceLinks"/> (both embed Resolve/Address
    /// <c>data-copy</c>). Structured and unstructured deferred list items both get pages. NFR8:
    /// no items → no folder. [Story 9.11]</summary>
    private void WriteFollowUpDetails(SiteNav nav, WorkInventory? work = null)
    {
        var actionItems = _sprint?.ActionItems ?? Array.Empty<SprintActionItem>();
        var inventory = work ?? WorkInventory.Build(_docs.Values.ToList());
        var deferredModel = TryParseDeferredWork(inventory);
        var deferredPairs = CollectDeferredDetailPairs(deferredModel);

        if (actionItems.Count == 0 && deferredPairs.Count == 0) return;

        var followUpsDir = Path.Combine(_options.OutputRoot, FollowUpSlug.Folder);
        Directory.CreateDirectory(followUpsDir);

        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, _docs.Values);
        var deferredHref = inventory.Deferred?.OutputPath;
        // Match the list page: near-dupe cross-links only among open items.
        var openForCrossLinks = actionItems.Where(a => !FollowUpGeometry.IsDone(a)).ToList();
        var crossLinks = openForCrossLinks.Count > 0
            ? ActionItemsTemplater.FindNearDuplicates(openForCrossLinks)
            : (IReadOnlyDictionary<SprintActionItem, IReadOnlyList<int>>)
                new Dictionary<SprintActionItem, IReadOnlyList<int>>(ReferenceEqualityComparer.Instance);
        var actionSlugs = FollowUpSlug.AssignActionSlugs(actionItems);

        // Story 10.10: the white sub-header band's local context for a follow-up detail page — the SAME
        // filtered group-page membership Story 9.13 already computes (Follow-ups orphan / Unplanned /
        // epic-N), reused here rather than a parallel same-epic recount, so "This group" on a detail page
        // always matches the group page it links back to. Computed once up front (mirrors
        // WriteFollowUpGroupPages) and shared by both the action-item and deferred-item loops below.
        var counts = _counts ?? ProjectCounts.Build(
            _progress ?? ProgressModel.Empty, _sprint, inventory, _epicsModel, _requirements);
        var geometry = BuildFollowUpGeometry(inventory, counts, deferredModel);
        var unplanned = UnplannedWorkGeometry.From(inventory, geometry, _epicsModel, retros: _retros);
        var groupSpecs = FollowUpGroupPages.Enumerate(geometry, unplanned, _epicsModel);
        var groupByHref = new Dictionary<string, FollowUpGroupSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in groupSpecs)
            foreach (var member in spec.Members)
                if (member.DetailHref is { Length: > 0 })
                {
                    var key = NormalizeFollowUpHref(member.DetailHref);
                    // Enumerate's three membership sources (orphans / unplanned / per-epic) partition follow-up
                    // items disjointly by construction; this makes that invariant observable in Debug builds
                    // instead of only silently letting the last-write-wins indexer below hide a collision.
                    // [Story 10.10 deferred debt]
                    Debug.Assert(!groupByHref.ContainsKey(key) || ReferenceEquals(groupByHref[key], spec),
                        $"FollowUpGroupPages.Enumerate's membership sources must stay mutually exclusive, but '{key}' appears in more than one group.");
                    groupByHref[key] = spec;
                }

        foreach (var item in actionItems)
        {
            if (!actionSlugs.TryGetValue(item, out var slug)) continue;
            var outputRelative = FollowUpSlug.OutputPath(slug);
            var actionLocalContext = BuildFollowUpGroupLocalContext(groupByHref, outputRelative);
            // No ApplyReferenceLinks — Resolve-with-AI data-copy must stay raw.
            WritePage(
                FollowUpDetailTemplater.BuildActionPage(
                    item, slug, nav, _module.Commands, EpicRetroMap, deferredHref,
                    _epicsModel, hrefMap, crossLinks, actionLocalContext),
                linkify: false);
        }

        if (deferredPairs.Count > 0)
        {
            var deferredSlugs = FollowUpSlug.AssignDeferredSlugs(
                deferredPairs.Select(p => (p.Item, p.ProvenanceLabel)).ToList());
            var listPath = inventory.Deferred?.OutputPath ?? "deferred-work.html";

            // Geometry carries resolved EpicNumber (source story / quick-dev inherit) for the epic pill.
            var deferredEpicByItem = new Dictionary<DeferredWorkItem, int?>(ReferenceEqualityComparer.Instance);
            foreach (var p in deferredPairs)
                deferredEpicByItem[p.Item] = geometry.DeferredItems.FirstOrDefault(s => s.Item == p.Item)?.EpicNumber;

            foreach (var (item, provenanceLabel, sourceHref) in deferredPairs)
            {
                if (!deferredSlugs.TryGetValue(item, out var slug)) continue;
                var outputRelative = FollowUpSlug.OutputPath(slug);
                var epicNumber = deferredEpicByItem[item];
                var deferredLocalContext = BuildFollowUpGroupLocalContext(groupByHref, outputRelative);
                // No ApplyReferenceLinks — Address/Close data-copy must stay raw.
                WritePage(
                    FollowUpDetailTemplater.BuildDeferredPage(
                        item, provenanceLabel, sourceHref, slug, nav, listPath, _module.Commands, epicNumber,
                        deferredLocalContext),
                    linkify: false);
            }
        }
    }

    /// <summary>The white sub-header band's local context for a follow-up detail page: the SAME
    /// <see cref="FollowUpGroupSpec"/> membership Story 9.13's filtered group pages already enumerate,
    /// looked up by this item's own detail href. Null when the item isn't a member of any enumerated group
    /// (shouldn't happen in practice — every action/deferred item lands on either the Follow-ups orphan or an
    /// epic-N group). A group with only one member is still returned here — the NFR8 "falls back to the
    /// generic band" rule for that case is enforced centrally by <see cref="HtmlRenderAdapter"/>'s
    /// <c>Items.Any(i =&gt; !i.IsActive)</c> guard, not by this method. [Story 10.10]</summary>
    private static NavLocalContext? BuildFollowUpGroupLocalContext(
        IReadOnlyDictionary<string, FollowUpGroupSpec> groupByHref, string outputRelative)
    {
        if (!groupByHref.TryGetValue(NormalizeFollowUpHref(outputRelative), out var spec)) return null;

        var prefix = PathUtil.RelativePrefix(outputRelative);
        var current = NormalizeFollowUpHref(outputRelative);
        var items = spec.Members
            .Where(m => m.DetailHref is { Length: > 0 })
            .Select(m => new NavLocalItem(
                m.RawSummary ?? PathUtil.StripHtmlTags(m.SummaryHtml),
                prefix + NormalizeFollowUpHref(m.DetailHref),
                string.Equals(NormalizeFollowUpHref(m.DetailHref), current, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new NavLocalContext(spec.Title, items);
    }

    /// <summary>Site-root-relative form of a follow-up member/detail href — strips any page-depth prefix a
    /// caller-supplied <c>linkPrefix</c> may have baked in, mirroring <see cref="FollowUpGroupTemplater"/>'s
    /// own defensive stripping, so hrefs built at different call depths still compare equal. [Story 10.10]</summary>
    private static string NormalizeFollowUpHref(string href)
    {
        var normalized = PathUtil.NormalizeSlashes(href);
        while (normalized.StartsWith("../", StringComparison.Ordinal))
            normalized = normalized[3..];
        if (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized;
    }

    /// <summary>Structured group items, or unstructured top-level list items when the note has no
    /// Deferred-from headings. [Story 9.11]</summary>
    private static List<(DeferredWorkItem Item, string ProvenanceLabel, string? SourceStoryHref)> CollectDeferredDetailPairs(
        DeferredWorkModel? deferredModel)
    {
        if (deferredModel is { IsStructured: true })
        {
            return deferredModel.Groups
                .SelectMany(g => g.Items.Select(i => (Item: i, ProvenanceLabel: g.ProvenanceLabel, SourceStoryHref: g.SourceStoryHref)))
                .ToList();
        }

        return FollowUpGeometry.UnstructuredItems(deferredModel?.PlainBodyHtml)
            .Select(i => (Item: i, ProvenanceLabel: "Deferred work", SourceStoryHref: (string?)null))
            .ToList();
    }

    /// <summary>Ledger-backed follow-up geometry with per-item deferred attribution when the deferred-work
    /// note parses as structured. [Story 9.7]</summary>
    private FollowUpGeometry BuildFollowUpGeometry(WorkInventory work, ProjectCounts counts, string linkPrefix = "") =>
        BuildFollowUpGeometry(work, counts, TryParseDeferredWork(work), linkPrefix);

    private FollowUpGeometry BuildFollowUpGeometry(WorkInventory work, ProjectCounts counts, DeferredWorkModel? deferredModel, string linkPrefix = "") =>
        FollowUpGeometry.From(
            _sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
            counts,
            work,
            linkPrefix,
            deferredModel,
            _epicsModel,
            _retros);

    /// <summary>Work inventory for follow-up + unplanned sunburst geometry. Uses the populated
    /// <see cref="_docs"/> when available; otherwise (e.g. during <see cref="RenderEpicsPages"/>, before the
    /// pages loop fills <see cref="_docs"/>) locates and converts <c>deferred-work.md</c> and open
    /// <c>route: one-shot</c> specs from source read-only so both deferred and quick-dev are available.</summary>
    private WorkInventory ResolveFollowUpWork(IReadOnlyList<string> files)
    {
        var fromDocs = WorkInventory.Build(_docs.Values.ToList());
        var deferred = fromDocs.Deferred;
        if (deferred is null && TryConvertDeferredDoc(files) is { } doc)
        {
            deferred = new DeferredWorkEntry(
                doc.Title,
                PathUtil.NormalizeSlashes(doc.OutputRelativePath),
                WorkInventory.CountOpenItems(doc.BodyHtml));
        }

        var quickDev = fromDocs.QuickDev.Count > 0
            ? fromDocs.QuickDev
            : ConvertQuickDevFromSource(files);

        return new WorkInventory
        {
            QuickDev = quickDev,
            Deferred = deferred,
        };
    }

    /// <summary>The <c>(sourceRelative, outputRelative)</c> pairs the follow-up href map's doc half needs —
    /// from the populated <see cref="_docs"/> when it exists, and otherwise derived straight from the source file
    /// list. <see cref="PathUtil.ToOutputRelative"/> is a pure function of the source-relative path, so the
    /// derived pairs are the SAME pairs <see cref="_docs"/> yields once the pages loop has filled it — which is
    /// what makes the pre-loop and post-loop maps agree instead of silently differing.
    /// <para>Deliberately NOT a <see cref="_docs"/> pre-population (Story 22.4 Trap 2): filling <c>_docs</c>
    /// early would flip <see cref="GenerateOneInternal"/>'s <c>alreadyExisted</c> and turn every
    /// <c>Generated</c> diagnostic into <c>Updated</c>, moving the golden fingerprint for a reason that has
    /// nothing to do with the work inventory. This reads paths only — no conversion, no cache mutation.</para>
    /// <para><see cref="IsIgnored"/> is applied for the same reason the pages loop applies it: an ignored file
    /// never becomes a page, so mapping a resolver onto its would-be href would manufacture a dangling
    /// link.</para>
    /// [Story 22.4 AC #5]</summary>
    private IEnumerable<(string SourceRelativePath, string OutputRelativePath)> ResolveFollowUpDocPairs(
        IReadOnlyList<string> files)
    {
        if (_docs.Count > 0)
        {
            return _docs.Values.Select(d => (d.SourceRelativePath, d.OutputRelativePath)).ToList();
        }

        var pairs = new List<(string, string)>();
        foreach (var file in files)
        {
            if (IsIgnored(file)) continue;
            var relative = ToSourceRelative(file);
            pairs.Add((relative, PathUtil.ToOutputRelative(relative)));
        }
        return pairs;
    }

    /// <summary>Read-only conversion of <c>spec-*.md</c> one-shots when <see cref="_docs"/> is not yet
    /// populated — keeps epics.html Unplanned wedges aligned with index.html. [Story 9.12]</summary>
    private IReadOnlyList<QuickDevEntry> ConvertQuickDevFromSource(IReadOnlyList<string> files)
    {
        var list = new List<QuickDevEntry>();
        foreach (var file in files)
        {
            var relative = ToSourceRelative(file);
            var norm = PathUtil.NormalizeSlashes(relative);
            if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) continue;
            var slash = norm.LastIndexOf('/');
            var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
            if (!fileName.StartsWith("spec-", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                if (!File.Exists(file)) continue;
                var outputRelative = PathUtil.ToOutputRelative(relative);
                var doc = MarkdownConverter.Convert(file, relative, outputRelative);
                if (!string.Equals(doc.Frontmatter.Route?.Trim(), "one-shot", StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(new QuickDevEntry(
                    doc.Title, PathUtil.NormalizeSlashes(outputRelative), doc.Frontmatter.Status, doc.Frontmatter.Type,
                    doc.Frontmatter.AuthoredDay()));
            }
            catch (IOException) { /* NFR2 */ }
            catch (UnauthorizedAccessException) { /* NFR2 */ }
        }

        return list.OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Parses the deferred-work note for the follow-up geometry, preferring the <see cref="_docs"/>
    /// entry and falling back to a read-only source conversion when <see cref="_docs"/> isn't populated yet.</summary>
    private DeferredWorkModel? ResolveDeferredModel(WorkInventory work, IReadOnlyList<string> files)
    {
        if (TryParseDeferredWork(work) is { } fromDocs) return fromDocs;
        if (work.Deferred is null || TryConvertDeferredDoc(files) is not { } doc) return null;

        var prefix = PathUtil.RelativePrefix(PathUtil.NormalizeSlashes(work.Deferred.OutputPath));
        // Story 22.4 AC #5: this used to pass `_docs.Values`, which is EMPTY on the RenderEpicsPages route (the
        // pages loop has not run yet) — so every spec resolver's href came back null and WorkGraph.BuildStory
        // dropped the resolver node and its edge from 46 static story pages while the IR, built after the loop,
        // drew them. Resolving the map through the source-derived route makes both sides see the same one.
        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, ResolveFollowUpDocPairs(files));
        return DeferredWorkParser.Parse(doc.Markdown, hrefMap, prefix, doc.BodyHtml);
    }

    /// <summary>Read-only conversion of the source <c>deferred-work.md</c> (no <see cref="_docs"/> mutation,
    /// no output written), plus its raw markdown, for the follow-up geometry when the pages loop hasn't run.
    /// Returns null when absent or unreadable (NFR2).</summary>
    private (string Title, string OutputRelativePath, string BodyHtml, string Markdown)? TryConvertDeferredDoc(IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            var relative = ToSourceRelative(file);
            var norm = PathUtil.NormalizeSlashes(relative);
            if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) continue;
            var slash = norm.LastIndexOf('/');
            var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
            if (!string.Equals(fileName, "deferred-work.md", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                if (!File.Exists(file)) return null;
                var outputRelative = PathUtil.ToOutputRelative(relative);
                var doc = MarkdownConverter.Convert(file, relative, outputRelative);
                return (doc.Title, outputRelative, doc.BodyHtml, File.ReadAllText(file));
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        return null;
    }

    /// <summary>Parses the deferred-work note when present; returns null when absent or unreadable.
    /// Shared by <see cref="WriteDeferredWork"/> and <see cref="WriteFollowUpDetails"/>. [Story 9.11]</summary>
    private DeferredWorkModel? TryParseDeferredWork(WorkInventory inventory)
    {
        var deferred = inventory.Deferred;
        if (deferred is null) return null;

        var doc = _docs.Values.FirstOrDefault(d =>
            string.Equals(
                PathUtil.NormalizeSlashes(d.OutputRelativePath),
                PathUtil.NormalizeSlashes(deferred.OutputPath),
                StringComparison.OrdinalIgnoreCase));
        if (doc is null) return null;

        var sourceFull = Path.Combine(
            _options.SourceRoot,
            doc.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string? markdown = null;
        try
        {
            if (File.Exists(sourceFull))
                markdown = File.ReadAllText(sourceFull);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        var outputPath = PathUtil.NormalizeSlashes(deferred.OutputPath);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var hrefMap = FollowUpRefs.BuildHrefMap(_epicsModel, _docs.Values);
        return DeferredWorkParser.Parse(markdown, hrefMap, prefix, doc.BodyHtml);
    }

    /// <summary>Writes requirements.html plus one detail page per FR/NFR. Each page is linkified against the
    /// requirement set (the detail page skips its own id so it never self-links).</summary>
    private void WriteRequirements(
        RequirementsModel requirements, EpicsModel model, ProgressModel progress, SiteNav nav, WorkInventory? work = null)
    {
        WritePage(RequirementsTemplater.BuildIndexPage(requirements, model, progress, nav, _counts));

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
            WritePage(
                RequirementsTemplater.BuildRequirementPage(
                    req, progress, nav, model, EpicRetroMap, deferredWorkHref, requirements),
                skipRequirementId: req.Id);
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
        // Story 10.5 AC1: [[wiki-link]]/[ASSUMPTION: …]/bare file:line chips. Runs AFTER CodeReferenceLinkifier
        // so a citation it already resolved into a real <a> is left untouched (never re-linked/double-wrapped).
        html = ReferenceChipRenderer.Render(html);
        // Story 10.3: first-use <abbr> expansion for bare acronyms (FR/AC/ADR/...). Runs LAST so it never
        // rewrites text inside any anchor the linkifiers above just created; a no-op when the detected
        // module publishes no glossary (NFR8 — undetected frameworks stay byte-unchanged).
        html = AbbreviationExpander.Expand(html, _module.Glossary);
        return html;
    }

    private void EnsureScaffold()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        // The stylesheet + script ship as embedded resources so the global-tool package needs no loose asset files.
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.css", ForgeOptions.StylesheetName);
        CopyEmbeddedAsset("SpecScribe.assets.specscribe.js", ForgeOptions.ScriptName);
    }

    /// <summary>Writes one embedded asset (the stylesheet / script) into the output root. Re-run by EVERY
    /// regeneration route, so it is the one file a live watch session rewrites on every single save — and the one
    /// most likely to lose a race with something else holding it open (a virus scanner sweeping the freshly-written
    /// tree, a browser tab that has the script loaded, an editor previewing the site). A sharing violation there is
    /// transient by nature, so retry briefly; if the file is still contended AND a usable copy is already on disk,
    /// accept it rather than failing the whole pass over an asset that is already correct — its bytes are fixed at
    /// build time, so an existing non-empty copy cannot be stale. Only a genuine first write that never succeeds
    /// still throws. [Story 5.3 Task 5 — NFR2 graceful degradation]</summary>
    private void CopyEmbeddedAsset(string resourceName, string outputFileName)
    {
        var dest = Path.Combine(_options.OutputRoot, outputFileName);
        // Copy to a sibling temp file and atomically move it into place, so a failure mid-copy (disk full, an
        // interrupted write) never leaves dest itself truncated/corrupt — dest is only ever replaced once a copy has
        // FULLY landed. Without this, File.Create's implicit truncation of dest on each attempt meant a partial
        // CopyTo on the final retry could leave a non-empty-but-corrupt file that the fallback below would then
        // silently accept as "already correct". [Story 5.3 review-fix]
        var tempDest = dest + ".tmp";
        using var source = typeof(SiteGenerator).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded asset '{resourceName}' is missing from the assembly.");

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using (var destStream = File.Create(tempDest))
                {
                    source.CopyTo(destStream);
                }
                File.Move(tempDest, dest, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDeleteTempAsset(tempDest);
                if (attempt >= AssetWriteRetries)
                {
                    // dest itself is never touched by a failed attempt (only tempDest is), so an existing non-empty
                    // copy here is genuinely a complete prior write, not a partial one from this call.
                    if (new FileInfo(dest) is { Exists: true, Length: > 0 }) return;
                    throw;
                }

                Thread.Sleep(AssetWriteRetryDelayMs);
                source.Position = 0;
            }
        }
    }

    private static void TryDeleteTempAsset(string tempDest)
    {
        try
        {
            File.Delete(tempDest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup only — a leftover .tmp file is overwritten by File.Create on the next attempt.
        }
    }

    // Bounded, deliberately small: enough to ride out a scanner's momentary handle, short enough that a real
    // failure surfaces well inside one debounce interval rather than stalling the watch loop.
    private const int AssetWriteRetries = 3;
    private const int AssetWriteRetryDelayMs = 25;

    /// <summary>Writes a page with the same brief transient-contention retry <see cref="CopyEmbeddedAsset"/> uses,
    /// and for the same reason (NFR2). <c>index.html</c> is the most-rewritten file in a watch session — every
    /// debounced save regenerates it — so it is the likeliest to lose a race with something holding it open for a
    /// moment: a virus scanner sweeping the freshly-written tree, a browser tab reloading it, an editor preview.
    /// A sharing violation there is transient by nature and should cost a few milliseconds, not a reported error.
    ///
    /// <para>Surfaced by a real burst-of-saves failure: the home page grew substantially in Story 20.5 (the
    /// Hierarchy Explorer's island and text twin, and a details-rail card per selectable node), which lengthened
    /// each write enough to turn a pre-existing, rarely-hit race into an intermittent one. The size change exposed
    /// the gap; the missing retry was always the defect. [Story 20.5 owner round]</para>
    ///
    /// <para><b>Write-to-temp then atomic <see cref="File.Move(string,string,bool)"/> — the half this was missing.</b>
    /// Its doc claimed parity with <see cref="CopyEmbeddedAsset"/> while calling bare <c>File.WriteAllText</c>, which
    /// TRUNCATES the target before writing: a sharing violation part-way through the final attempt therefore left a
    /// truncated page on disk, and for <c>index.html</c> in a watch session that is the worst possible file to
    /// corrupt. <c>CopyEmbeddedAsset</c> has written through a <c>.tmp</c> and swapped since a Story 5.3 review fix,
    /// for exactly this reason. Now they genuinely match. [Story 20.5 review]</para></summary>
    private static void WriteTextWithRetry(string path, string content)
    {
        var temp = path + ".tmp";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.WriteAllText(temp, content);
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDeleteTempAsset(temp);
                if (attempt >= AssetWriteRetries) throw;
                Thread.Sleep(AssetWriteRetryDelayMs);
            }
        }
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

    /// <summary>Dependency lockfiles are MACHINE-WRITTEN inventories, not source. A code page for one is
    /// thousands of lines nobody reads, and its size dominates whatever bucket the Code Map puts it in — this
    /// repo's own portal had an 11,132-line <c>web/package-lock.json</c> page swamping the Config bucket.
    /// <para>Applies to every documented repo, not just this one: `git ls-files` has no extension filter (the
    /// Story 23.2 completion notes assumed a "code-page extension set" that does not exist), so any project
    /// with a lockfile hit this. Matched by FILENAME so a source file that merely lives in a <c>lock/</c>
    /// directory is unaffected. [Story 23.2 re-review 2026-07-28]</para></summary>
    private static bool IsGeneratedLockFile(string repoRelativePath)
    {
        var name = repoRelativePath[(repoRelativePath.LastIndexOf('/') + 1)..];
        return name.EndsWith("-lock.json", StringComparison.OrdinalIgnoreCase)   // package-lock, npm-shrinkwrap forks
            || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)        // yarn.lock, Cargo.lock, poetry.lock, Gemfile.lock
            || name.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("composer.lock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The source-code walk for the treemap (Story 7.6): git-tracked files + their line counts,
    /// repo-relative + forward-slash. Prefers <c>git ls-files</c> (tracked files only → excludes <c>bin/</c>,
    /// <c>obj/</c>, <c>.git/</c>, <c>node_modules/</c>, and everything <c>.gitignore</c> covers, defining "the
    /// codebase" the way git does); falls back to a bounded directory walk with an explicit exclude list when git is
    /// unavailable / not a repo. Line counts are streamed (no full-string allocation); binary/unreadable files still
    /// skip, but oversized text files contribute LOC so the treemap isn't silently incomplete — the 1MB cap remains
    /// for inline code-page rendering only. Bounded (<see cref="MaxCodeMapFiles"/>) and wrapped never-throw
    /// (NFR1/NFR2): any failure yields an empty list, so the whole surface omits and generation still succeeds.
    /// <para>⚠ No longer "runs once per full generation": Story 22.5's <see cref="RefreshCodeSurfaces"/> calls this on
    /// every narrow watch route too, so the result is re-computed and the <see cref="_codeFiles"/> cache re-assigned
    /// mid-session. See that method's remarks for what that does and does not guarantee.
    /// [spec-7-1-deferred-debt-cleanup; re-scoped note added in the 2026-07-29 code review]</para></summary>
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

                if (IsGeneratedLockFile(normalized)) continue;

                try
                {
                    if (!File.Exists(full)) continue;
                    if (!TryCountCodeLines(full, out var lines)) continue; // binary / unreadable → skip
                    result.Add((normalized, lines));
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

            // Sorted ordinally so the walk — and therefore which files land inside MaxCodeMapFiles's cap on a
            // large tree, and in what order — is deterministic across filesystems. Directory.GetFileSystemEntries
            // makes NO ordering guarantee, and NTFS vs. ext4/APFS enumerate the SAME directory differently in
            // practice: an unsorted walk fed a genuine portability bug into every downstream consumer of the
            // resulting file list (code-map.html, and transitively any fingerprint/hash taken over it) — stable
            // on one OS, but different between Windows and Linux for byte-identical source. (code review finding)
            Array.Sort(entries, StringComparer.Ordinal);

            var subdirs = new List<string>();
            foreach (var entry in entries)
            {
                if (results.Count >= MaxCodeMapFiles) break;
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules") continue;
                    subdirs.Add(entry);
                }
                else if (!PathUtil.IsIgnoredSourceFile(entry))
                {
                    results.Add(PathUtil.NormalizeSlashes(Path.GetRelativePath(repoRoot, entry)));
                }
            }
            // Pushed in REVERSE sorted order so the stack (LIFO) pops them back out in ASCENDING order — a
            // deterministic depth-first walk, not one that happens to depend on push order inverting cleanly.
            for (var i = subdirs.Count - 1; i >= 0; i--) stack.Push(subdirs[i]);
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
    /// <param name="diagnostics">Optional sink for nav-build diagnostics (e.g. duplicate well-known module
    /// docs) — callers with an event stream to merge into pass a list; incremental callers that rebuild nav
    /// from an empty source list (before the first full generate) have nothing to discover and can omit it.
    /// [spec-epic2-deferred-debt-cleanup]</param>
    private SiteNav BuildNav(IReadOnlyList<string> sourceRelatives, List<AdapterDiagnostic>? diagnostics = null)
    {
        // Detection runs ONCE per run, in the adapter's Ingest, and _module holds the result. It used to be
        // re-derived here as well — and this second, adapter-free detection is the one that actually fed nav,
        // the glossary and AbbreviationExpander, while the bundle's (diagnosed) detection was overwritten.
        // Worse, 4 of this method's 5 call sites pass an EMPTY source list, so the BMM-vs-GDS source-shape
        // tie-break was unconditionally false on every incremental and watch rebuild: a dual-install game repo
        // silently fell back to BMM mid-session. Consuming the cached context fixes both.
        // [Story 18.2; ADR 0015 Decisions 2d, 4c]
        // CodeMapAvailable = the cached source-code walk is non-empty — the SINGLE signal that gates both the nav
        // item/quick link here and the WriteCodeMap page write, so a Code Map link is never emitted to a page that
        // wasn't produced. The incremental watch paths that call BuildNav reuse the last full run's _codeFiles (the
        // treemap only regenerates on a full rebuild). [Story 7.6 Subtask 3.4]
        // Insights / Follow-ups gates reuse the last full run's _progress / _sprint / source list the same way.
        // [Story 10.1]
        var deferredWorkPath = SiteNav.FindDeferredWorkOutputPath(sourceRelatives, diagnostics);
        return SiteNav.Build(
            sourceRelatives, _options.SiteTitle, _module.Docs, AdrsExist(), ReadmeAvailable, SprintAvailable,
            hasCodeMap: _codeFiles.Count > 0,
            hasGitInsights: _progress?.DeepGit?.Insights is not null,
            hasDeepAnalytics: _progress?.DeepGit is not null,
            hasActionItems: _sprint?.OpenActionItems.Count > 0,
            hasDeferredWork: deferredWorkPath is not null,
            // Reuse the last full run's projected model (the treemap/insights gates reuse last-run signals the same
            // way); the work graph only re-projects on a full rebuild. [Story 19.2]
            hasWorkGraph: !_workGraph.IsEmpty,
            // Same reuse-the-last-full-run rule: forge workspaces are re-discovered only on a full rebuild, which
            // is exactly right because watch mode cannot see forge activity at all (a `.memlog.md` is dotfile-
            // ignored and a `forge-report.html` is not `.md`, so neither reaches the dispatch — see IsDataSource,
            // the seam a follow-on would extend). [Story 18.4]
            hasIdeas: !_ideas.IsEmpty,
            // Same reuse-the-last-full-run rule again: a module's test artifacts are re-discovered only on a full
            // rebuild. The two TEA JSON files are not `.md` and so never reach the watch dispatch (IsDataSource),
            // and the module-presence gate reads `_bmad/` which is outside the watched source tree entirely.
            // [Story 18.5]
            hasTestArtifacts: !_testArtifacts.IsEmpty,
            deferredWorkOutputPath: deferredWorkPath,
            diagnostics: diagnostics);
    }

    private static readonly IReadOnlyDictionary<string, DateOnly> EmptyDates = new Dictionary<string, DateOnly>();

    // Story 7.3: shared empty maps for the date-page / timeline paths (git-absent, artifact-absent branches).
    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> EmptyArtifactsByDay
        = new Dictionary<DateOnly, IReadOnlyList<(string Label, string Href)>>();
    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> EmptyCommitsByDay
        = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>();

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
            // The DETECTED module decides the canonical family set (Story 18.6 / ADR 0015 Decision 5a). Read
            // from the cached _module — never a second ModuleContext.Detect, which Story 18.2 made
            // once-per-run on purpose. _module is assigned from the adapter bundle well before this runs, and
            // persists across watch incrementals, so RefreshCoverage()'s rebuilds stay module-aware too.
            var module = _module.Module;
            var discovered = ArtifactCoverage.Build(sourceRelatives, module, EmptyDates, EmptyDates, today);

            // Stat every candidate matching ANY family (not just each family's provisional first match) so an
            // OR-predicate family (UX: DESIGN.md or EXPERIENCE.md) has both mtimes available when Build picks
            // the freshest one as canonical below. [Story 3.3 review]
            var mtimes = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in ArtifactCoverage.AllCandidatePaths(sourceRelatives, module))
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

            var coverage = ArtifactCoverage.Build(sourceRelatives, module, mtimes, memlogByFamily, today);

            // Enrich each family with presentation data the pure Build can't know: the page a PRESENT family
            // links to, and the create command a MISSING family surfaces for the detected module. Resolved
            // here because both depend on page routing and the module — never on the source-derived truth.
            var enriched = coverage.Families
                .Select(f => f with
                {
                    Href = f.Present ? ResolveFamilyHref(f) : null,
                    CreateCommand = !f.Present && ArtifactCoverage.CreateStepKeysFor(module).TryGetValue(f.Label, out var step)
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
    /// every family's primary Present/LastModified value is unchanged (AC #2). [Story 3.3 Subtask 2.3]
    /// <para><b>Forge session memlogs are excluded</b> (Story 18.4, decision recorded). A forge workspace's memlog
    /// is <em>scoped</em> (<c>forge/{slug}</c>), and <see cref="SelectMemlogUpdatedByFamily"/> demotes a root-level
    /// memlog from every-family fallback the moment ANY scoped memlog exists. So in a repo whose only decision
    /// journal is <c>_bmad-output/.memlog.md</c>, running the forge once would silently strip the journal date from
    /// every coverage card — an unrelated surface changing because a different tool ran. A forge memlog also could
    /// never legitimately WIN a family (no family's source path lives under <c>forge/{slug}/</c>), so excluding it
    /// removes only that spurious flag flip and adds nothing back. Ideas have their own dedicated surface for this
    /// same data now, so nothing is lost.</para></summary>
    private IReadOnlyDictionary<string, DateOnly> BuildMemlogMap(ArtifactCoverage discovered)
    {
        var result = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_options.SourceRoot)) return result;

        // [Story 18.4 review] Keyed off WorkspaceSourceRelatives (every proven forge workspace), NOT Ideas — a
        // slug-collision loser never becomes an IdeaEntry, but its memlog is still on disk and must still be
        // excluded here, or it keeps flipping hasScopedMemlog for the rest of the portal.
        var ideaWorkspaces = new HashSet<string>(
            _ideas.WorkspaceSourceRelatives, StringComparer.OrdinalIgnoreCase);

        var memlogs = new List<(string Dir, DateOnly Updated)>();
        foreach (var full in Directory.EnumerateFiles(_options.SourceRoot, Memlog.FileName, SearchOption.AllDirectories))
        {
            DateOnly updated;
            try
            {
                if (Memlog.ParseUpdated(MarkdownConverter.ReadAllTextShared(full)) is not { } parsed)
                {
                    continue; // no parseable updated: date → this memlog adds no enrichment
                }
                updated = parsed;
            }
            catch
            {
                continue;
            }

            var dir = PathUtil.NormalizeSlashes(Path.GetDirectoryName(ToSourceRelative(full)) ?? string.Empty);
            if (ideaWorkspaces.Contains(dir)) continue; // see the forge-exclusion note above
            memlogs.Add((dir, updated));
        }

        if (memlogs.Count == 0) return result; // strictly additive: the no-memlog primary picture is unchanged

        return SelectMemlogUpdatedByFamily(memlogs, discovered.Families);
    }

    /// <summary>The pure ancestor-selection core of <see cref="BuildMemlogMap"/>, split out so it's directly
    /// unit-testable without disk I/O: for each family with a known source path, picks the memlog whose
    /// directory is the closest (longest matching) ancestor. A root-level memlog (<c>Dir.Length == 0</c>) only
    /// stands in as every family's fallback when it's the ONLY memlog in the tree — there, it genuinely is the
    /// project's one decision journal. Once any nested, family-scoped memlog exists, the root one no longer
    /// blanket-applies to families with no closer match, so an unrelated project-root journal can't be
    /// misattributed as a specific family's own enrichment. [Story 3.3 review]</summary>
    internal static IReadOnlyDictionary<string, DateOnly> SelectMemlogUpdatedByFamily(
        IReadOnlyList<(string Dir, DateOnly Updated)> memlogs, IReadOnlyList<ArtifactFamily> families)
    {
        var result = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var hasScopedMemlog = memlogs.Any(ml => ml.Dir.Length > 0);

        foreach (var family in families)
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
    /// treats as absent).
    /// <para>Internal (was private) so <see cref="IdeaDerivation"/> can put a forge memlog's free-text
    /// <c>idea:</c>/<c>goal:</c> values through the SAME one-line treatment rather than inventing a second
    /// truncator with its own cut length and its own grapheme handling. [Story 18.4]</para></summary>
    internal static string CollapseSummary(string text)
    {
        var noLinks = MarkdownLinkPattern.Replace(text, "${text}");
        var plain = PathUtil.StripHtmlTags(noLinks).Replace("*", string.Empty).Replace("`", string.Empty);
        var collapsed = Regex.Replace(plain, @"\s+", " ").Trim();
        // Strip a leading list/quote/table/image marker so a Context that opens with "- "/"> "/"| "/"1. "/"!" doesn't
        // leak the marker into the card summary.
        collapsed = Regex.Replace(collapsed, @"^(?:[-*+>|!]\s*|\d+\.\s+)+", string.Empty).Trim();
        const int max = 160;
        if (collapsed.Length <= max) return collapsed;
        // Cut on a grapheme-cluster boundary so ZWJ / combining sequences stay whole (UTF-16 Length alone can
        // split them). Budget leaves one code unit for the ellipsis. [Story 10.4 deferred-debt]
        const int budget = max - 1;
        var kept = new StringBuilder(budget);
        var elements = StringInfo.GetTextElementEnumerator(collapsed);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (kept.Length + element.Length > budget) break;
            kept.Append(element);
        }
        return kept.ToString().TrimEnd() + "…";
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

        // The loop above optimistically assumes every *.md source gets a generic page at ToOutputRelative(rel).
        // That holds for everything EXCEPT files a dedicated surface consumed — story artifacts (fixed up just
        // below) and, since Story 18.4, a forge workspace's markdown, which renders as the idea's own
        // ideas/{slug}.html instead. Without this fix-up every citation of a consumed forged-idea.md — including
        // the Code Map / Risk Quadrant tiles, which reach this map through ArtifactHrefByRepoRel — would point at
        // a forge/{slug}/forged-idea.html that is deliberately never written. Route them to the meaningful page,
        // the same special-routing discipline sprint-status.yaml and epics.md already get. [Story 18.4]
        foreach (var idea in _ideas.Ideas)
        {
            var workspacePrefix = idea.WorkspaceSourceRelative + "/";
            foreach (var key in map.Keys
                         .Where(k => k.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                map[key] = idea.DetailOutputPath;
            }
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
