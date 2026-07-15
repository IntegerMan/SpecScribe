namespace SpecScribe;

/// <summary>One headline stat-tile as pure DATA — the four inputs <see cref="Charts.StatCard"/> takes. The
/// dashboard's stat-grid row is a list of these; the tasks/commits FORKS (e.g. "—/none tracked" when no tasks
/// exist) are RESOLVED into the tile's field values at build time, so the adapter just renders each tile with no
/// branching. Mirrors the <c>StatCard</c> argument tuple exactly so re-homing is byte-identical. [Story 6.2]</summary>
/// <param name="Number">The big headline value (already formatted, e.g. "12/18" or "—").</param>
/// <param name="Label">The tile label (already pluralized).</param>
/// <param name="Sub">The optional sub-line, or null for none.</param>
/// <param name="Tooltip">The optional on-brand tooltip, or null for none.</param>
/// <param name="Href">The optional drill target (output-relative). When set, the tile renders as a link to the
/// most relevant standalone view; null keeps it a static tile. Only set when the target page exists.</param>
public sealed record StatTile(string Number, string Label, string? Sub = null, string? Tooltip = null, string? Href = null);

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

/// <summary>The host-neutral SECTION view model for the dashboard (home) page body — the typed decomposition of
/// <c>HtmlTemplater.RenderIndex</c> + <c>AppendDashboard</c> that fills 6.1's opaque <see cref="PageView.BodyHtml"/>
/// seam for this one surface (memory: story-6-1-delivery-seam-live). It carries the data-shaped sections as pure
/// records (<see cref="StatTiles"/>, <see cref="ProgressBars"/>, <see cref="NowNext"/>, <see cref="QuickLinks"/>)
/// and the chart/rich panels as their already-projected DOMAIN INPUT
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

    /// <summary>True when the activity-timeline page (<c>timeline.html</c>) was generated, so the Git Pulse
    /// panel renders the guarded "View activity timeline →" link. Defaults false — the generator sets it from
    /// its cached <c>_timelinePath</c>, so a link is never emitted to a page that wasn't produced. [Story 7.3]</summary>
    public bool HasTimeline { get; init; }
}
