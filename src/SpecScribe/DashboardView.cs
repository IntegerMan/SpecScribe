namespace SpecScribe;

/// <summary>One headline stat-tile as pure DATA — the four inputs <see cref="Charts.StatCard"/> takes. The
/// dashboard's stat-grid row is a list of these; the tasks/commits FORKS (e.g. "—/none tracked" when no tasks
/// exist) are RESOLVED into the tile's field values at build time, so the adapter just renders each tile with no
/// branching. Mirrors the <c>StatCard</c> argument tuple exactly so re-homing is byte-identical. [Story 6.2]</summary>
/// <param name="Number">The big headline value (already formatted, e.g. "12/18" or "—").</param>
/// <param name="Label">The tile label (already pluralized).</param>
/// <param name="Sub">The optional sub-line, or null for none.</param>
/// <param name="Tooltip">The optional on-brand tooltip, or null for none.</param>
public sealed record StatTile(string Number, string Label, string? Sub = null, string? Tooltip = null);

/// <summary>One "Overall Progress" bar as pure DATA — the inputs <see cref="Charts.ProgressBar"/> takes. The
/// Planning/Implementation fork (the "not started" 0/0 bar when no tasks are tracked) is resolved into these
/// values at build time. [Story 6.2]</summary>
/// <param name="Label">The bar's left label.</param>
/// <param name="Value">The filled amount.</param>
/// <param name="Max">The bar's total.</param>
/// <param name="RightLabel">The optional right-hand caption, or null.</param>
public sealed record ProgressBarView(string Label, int Value, int Max, string? RightLabel = null);

/// <summary>One "Now &amp; Next" card as pure DATA — the css accent class, kicker, title, and drill href a
/// <c>now-next-card</c> renders. Used for the DERIVED in-dev/review/up-next/next-to-draft view (the non-sprint
/// fallback). The sprint-board mode carries its domain input instead (see <see cref="DashboardNowNext"/>). [Story 6.2]</summary>
public sealed record NowNextCard(string CssClass, string Kicker, string Title, string Href);

/// <summary>The "Now &amp; Next" panel view. This panel has two mutually exclusive modes (memory:
/// now-and-next-is-the-sprint-board): when a sprint is tracked it BECOMES the sprint board — rendered by
/// <see cref="SprintTemplater"/> from the carried <see cref="SprintBoard"/> + the dashboard's
/// <see cref="DashboardView.Epics"/> (an inline-SVG/board render driven by already-projected domain input, the
/// sanctioned chart-input carry); otherwise it renders the derived <see cref="Cards"/> (pure data). The whole
/// panel is OMITTED (this view is null on <see cref="DashboardView"/>) when there is no epics model, or the
/// derived view has nothing to show — a byte-load-bearing conditional. [Story 6.2]</summary>
/// <param name="SprintBoard">Non-null → render the sprint board from this + the dashboard epics model. Null →
///   render <see cref="Cards"/>.</param>
/// <param name="Cards">The derived now/next cards (empty when <see cref="SprintBoard"/> drives the panel).</param>
public sealed record DashboardNowNext(SprintStatus? SprintBoard, IReadOnlyList<NowNextCard> Cards);

/// <summary>The rendering variant of an <see cref="IndexCardView"/> — the home index grid mixes a few card
/// shapes (a plain doc card, the prominent primary-PRD card, an ADR card with a raw status pill, a retrospective
/// card with a date line, and a quick-dev work card with a status badge + type pill). The card carries its data;
/// this tells the adapter which markup to emit. [Story 6.2]</summary>
public enum IndexCardStyle
{
    /// <summary>An ordinary artifact card: title, optional status BADGE (via <see cref="StatusStyles.ForDoc"/>),
    /// optional date·author meta, source path.</summary>
    Doc,

    /// <summary>The prominent primary-PRD card: a kicker, a title LINK (so it can also carry a branch link),
    /// status badge, meta, source path, and an optional "Quality review →" branch link.</summary>
    PrimaryPrd,

    /// <summary>An ADR card: title, optional status as a raw <c>pill</c> (class derived from the status' first
    /// word — NOT a status-badge), source path.</summary>
    Adr,

    /// <summary>A retrospective card: title, an optional date line (as a <c>&lt;p&gt;</c>), source path. No status.</summary>
    Retro,
}

/// <summary>One home-index card as pure DATA. The common core is <c>{ Title, Href, Status?, Meta?, SourcePath }</c>
/// (as the story specifies); <see cref="Style"/> plus a few style-specific optionals cover the handful of card
/// variants the index actually renders. All values are already RESOLVED data (e.g. the SPEC-hub card rename, the
/// "date · author" meta join) — the adapter only maps them to markup and routes <see cref="Status"/> through the
/// per-style status rendering. No HTML in any field. [Story 6.2]</summary>
public sealed record IndexCardView
{
    /// <summary>Which card markup to emit.</summary>
    public required IndexCardStyle Style { get; init; }

    /// <summary>The card title (already resolved — e.g. the SPEC hub's disambiguated label).</summary>
    public required string Title { get; init; }

    /// <summary>The card's drill href (output-relative, already normalized).</summary>
    public required string Href { get; init; }

    /// <summary>The literal text of the <c>index-card-path</c> line — a source path for artifact/adr/retro
    /// cards, or the literal "Quick-dev · one-shot" for a quick-dev card.</summary>
    public required string SourcePath { get; init; }

    /// <summary>The RAW status word, or null for no status. Rendered per <see cref="Style"/>: a status-badge for
    /// <see cref="IndexCardStyle.Doc"/>/<see cref="IndexCardStyle.PrimaryPrd"/> (via <see cref="StatusStyles.ForDoc"/>),
    /// or a raw <c>pill</c> for <see cref="IndexCardStyle.Adr"/>.</summary>
    public string? Status { get; init; }

    /// <summary>The de-emphasized meta line. For a doc/PRD card this is the "date · author" join (rendered as a
    /// <c>&lt;p&gt;</c>); for a retro card it is the date text; for an ADR card it is the formatted decision date
    /// (same <c>&lt;p&gt;</c> shape). Null emits nothing.</summary>
    public string? Meta { get; init; }

    /// <summary>ADR only: a one-line summary of the decision (from the ADR body), rendered as a muted line under the
    /// meta. Null emits nothing. [Story 10.4]</summary>
    public string? Summary { get; init; }

    /// <summary>Primary-PRD only: the kicker text ("Primary document").</summary>
    public string? Kicker { get; init; }

    /// <summary>Primary-PRD only: the branch-link href (the quality-review rubric page), or null.</summary>
    public string? BranchHref { get; init; }

    /// <summary>Primary-PRD only: the branch-link label ("Quality review →" text), or null.</summary>
    public string? BranchLabel { get; init; }
}

/// <summary>The special-layout data for the "Planning Artifacts" home band, which is NOT a flat card grid: a
/// prominent primary-PRD card, a paired "UX" subgroup, and the remaining planning docs as ordinary cards. Each
/// slot is optional/possibly-empty so a missing PRD/UX simply omits that block (never an empty labeled group).
/// Present on the planning <see cref="IndexBand"/> only; every other band uses <see cref="IndexBand.Cards"/>. [Story 6.2]</summary>
/// <param name="Prd">The prominent primary-PRD card (Style = <see cref="IndexCardStyle.PrimaryPrd"/>), or null.</param>
/// <param name="UxCards">The UX Design + UX Experience cards under the "UX" sub-label (empty → no subgroup).</param>
/// <param name="OtherCards">The Product Brief + remaining planning cards (empty → no trailing grid).</param>
public sealed record PlanningLayout(
    IndexCardView? Prd,
    IReadOnlyList<IndexCardView> UxCards,
    IReadOnlyList<IndexCardView> OtherCards);

/// <summary>One home-index band (section) as DATA: its title + icon concept key, an optional right-aligned
/// "more" link (the ADR band's "All ADRs →"), whether the title uses the title-ROW layout (ADR) or a plain
/// title, whether the icon is suppressed (the Retrospectives band emits its title with no icon), and either a
/// flat list of <see cref="Cards"/> or the special <see cref="Planning"/> layout. The band ORDER and membership
/// (which doc lands where, PRD prominence, unrecognized-folder degradation) are computed by
/// <see cref="DashboardViewBuilder"/>, so the adapter renders bands top-to-bottom with no grouping logic. [Story 6.2]</summary>
public sealed record IndexBand
{
    /// <summary>The band's section title text.</summary>
    public required string Title { get; init; }

    /// <summary>The <see cref="Icons.ForConcept"/> key for the band's leading glyph (usually equal to
    /// <see cref="Title"/>). Ignored when <see cref="NoIcon"/> is set.</summary>
    public required string ConceptKey { get; init; }

    /// <summary>The band's cards, in render order. Empty when <see cref="Planning"/> drives the band.</summary>
    public required IReadOnlyList<IndexCardView> Cards { get; init; }

    /// <summary>True for the ADR band, which renders its title in the <c>index-section-title-row</c> layout with a
    /// trailing <see cref="MoreLinkHref"/> link rather than a plain title.</summary>
    public bool TitleRow { get; init; }

    /// <summary>The "more" link href (ADR band's "All ADRs →"), or null.</summary>
    public string? MoreLinkHref { get; init; }

    /// <summary>The "more" link label text, or null.</summary>
    public string? MoreLinkLabel { get; init; }

    /// <summary>True for the Retrospectives band, whose title renders with NO leading icon (matching today's
    /// bytes). Every other plain band prefixes the concept icon.</summary>
    public bool NoIcon { get; init; }

    /// <summary>The special planning-band layout, or null for an ordinary card-grid band.</summary>
    public PlanningLayout? Planning { get; init; }
}

/// <summary>The host-neutral SECTION view model for the dashboard (home) page body — the typed decomposition of
/// <c>HtmlTemplater.RenderIndex</c> + <c>AppendDashboard</c> that fills 6.1's opaque <see cref="PageView.BodyHtml"/>
/// seam for this one surface (memory: story-6-1-delivery-seam-live). It carries the data-shaped sections as pure
/// records (<see cref="StatTiles"/>, <see cref="ProgressBars"/>, <see cref="NowNext"/>, <see cref="QuickLinks"/>,
/// <see cref="IndexBands"/>) and the chart/rich panels as their already-projected DOMAIN INPUT
/// (<see cref="Epics"/>, <see cref="Progress"/>, <see cref="Requirements"/>, <see cref="Coverage"/>,
/// <see cref="Work"/>) so the adapter re-renders them by calling the existing <c>Charts.*</c> helpers with no
/// re-parsing (AD-2). Every optional section is nullable so an absent input renders byte-for-byte nothing (the
/// byte-load-bearing conditionals). This is DATA only — no HTML strings, no delegates — so the deferred JSON
/// view-model export (Story 6.4) is a trivial serialize. [Story 6.2]</summary>
public sealed record DashboardView
{
    /// <summary>The project name — the page's <c>&lt;h1&gt;</c>.</summary>
    public required string SiteTitle { get; init; }

    /// <summary>The headline stat-grid row (4 or 5 tiles; the 5th "Direct changes" tile is present only when
    /// there is quick-dev/deferred work).</summary>
    public required IReadOnlyList<StatTile> StatTiles { get; init; }

    /// <summary>The "Now &amp; Next" panel, or null when it is omitted (no epics model, or the derived view is
    /// empty). See <see cref="DashboardNowNext"/>.</summary>
    public DashboardNowNext? NowNext { get; init; }

    /// <summary>The epics model — the input the sunburst ("Project at a Glance"), the Epic-Status donut, the
    /// requirements-flow Sankey, and the sprint board render from. Null omits the sunburst panel and drives the
    /// donut's binary-fallback branch. Carried as already-projected domain input, not re-modeled geometry
    /// (memory: charting-is-pure-svg-no-js).</summary>
    public EpicsModel? Epics { get; init; }

    /// <summary>The command catalog the sunburst's guidance overlay consumes.</summary>
    public required CommandCatalog Commands { get; init; }

    /// <summary>The progress snapshot — the input for the Epic-Status donut's fraction, the "Overall Progress"
    /// bars, the Story Pipeline funnel, the Git Pulse panel (incl. its <see cref="ProgressModel.Git"/>/
    /// <see cref="ProgressModel.DeepGit"/> forks), and the "Progress by Epic" mosaic.</summary>
    public required ProgressModel Progress { get; init; }

    /// <summary>The pre-computed "Overall Progress" bars (Planning + Implementation), forks already resolved.</summary>
    public required IReadOnlyList<ProgressBarView> ProgressBars { get; init; }

    /// <summary>The requirements model — the Requirements panel's tile grid + flow input. Null omits the panel.</summary>
    public RequirementsModel? Requirements { get; init; }

    /// <summary>The artifact-coverage model — the Planning-Artifacts coverage panel's input. Null (or empty)
    /// omits the panel.</summary>
    public ArtifactCoverage? Coverage { get; init; }

    /// <summary>The dashboard quick-link pills (a superset of the top nav, each with a description).</summary>
    public required IReadOnlyList<NavQuickLink> QuickLinks { get; init; }

    /// <summary>The quick-dev / deferred-work inventory the "Direct &amp; Quick-Dev Work" band renders. Carried
    /// as already-projected domain input (its entries are data).</summary>
    public required WorkInventory Work { get; init; }

    /// <summary>The count of open retrospective action items — the "Retro Action Items" callout in the work band
    /// (from the portal-wide <see cref="Counts"/> ledger). 0 omits the callout. [Story 8.3]</summary>
    public required int OpenRetroActionItems { get; init; }

    /// <summary>The portal-wide count ledger — THE source for every summary count this view surfaces
    /// (stat tiles, progress bars, action-item callout, sprint wheel). [Story 8.3; FR21]</summary>
    public required ProjectCounts Counts { get; init; }

    /// <summary>The home index-grid bands (planning / implementation / spec-kernel / unrecognized-folder / ADR /
    /// retro), in render order, fully grouped and classified by the builder.</summary>
    public required IReadOnlyList<IndexBand> IndexBands { get; init; }

    /// <summary>True when the activity-timeline page (<c>timeline.html</c>) was generated, so the Git Pulse
    /// panel renders the guarded "View activity timeline →" link. Defaults false — the generator sets it from
    /// its cached <c>_timelinePath</c>, so a link is never emitted to a page that wasn't produced. [Story 7.3]</summary>
    public bool HasTimeline { get; init; }
}
