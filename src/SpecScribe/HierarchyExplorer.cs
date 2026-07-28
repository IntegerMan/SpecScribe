using System.Text;
using System.Text.Json;

namespace SpecScribe;

/// <summary>How activating a node behaves — ADR 0012 §3's explicit mode contract. There is no third mode and no
/// "default": every call site states which one it wants, because the two are not interchangeable and a surface
/// that silently navigates when the owner wanted a selection is a bug you only find by clicking. [Story 20.5]</summary>
public enum HierarchyMode
{
    /// <summary>Activating a leaf follows its <see cref="HierarchyNode.Href"/> — the Story 9.13 destination
    /// contract (leaf → detail page, group → generated filtered list page).</summary>
    Navigate,

    /// <summary>Activating a leaf raises <c>specscribe:explorer-select</c> and does NOT navigate. The node's own
    /// destination stays reachable (breadcrumb + text twin), and the selection is announced to assistive tech.</summary>
    Select,
}

/// <summary>One node of the Hierarchy Explorer payload — the host-neutral projection (AD-2) both shapes read.
/// <para><see cref="Value"/> is an <c>int</c> rather than a nullable, and that is load-bearing: a single
/// <c>null</c> anywhere in Plotly's <c>values</c> array collapses calcdata to one point and renders <b>nothing</b>
/// — no error, no console warning (Story 20.4 spike, Finding B; measured calcdata 1 → 119 on changing null to 0).
/// The type makes that unrepresentable.</para>
/// <para><see cref="StatusLabel"/> is the PROSE status ("In review"), never the CSS class ("review"). UX-DR17/19
/// want status as words, and the 20.4 probe's accessible names read "— done, weight 44" precisely because it used
/// the class. It is resolved from <see cref="StatusStyles"/> / <see cref="Charts.SunburstLocalStatusLabel"/>, the
/// same source the legend and <c>SunburstCompanionList</c> use, so chart, twin and tile grid cannot disagree.</para></summary>
/// <param name="ShortLabel">The IDENTIFIER-only form drawn INSIDE a sector ("Epic 7", "Story 20.5"), as distinct
/// from <paramref name="Label"/>'s full title, which stays the hover card's heading, the text twin's link text and
/// the accessible name. This is not cosmetic: Plotly's <c>uniformtext</c> draws every label at ONE size — the
/// smallest that fits any sector — and hides the rest, so a single long title suppresses labels chart-wide.
/// Measured on the real dashboard: drilled into an epic, full titles left <b>2 of 7</b> sectors labelled. Owner
/// decision D3 asks for a labelled explorer, and a chart that hides five labels in seven is not one.</param>
/// <param name="Detail">The human-meaningful size sentence — "3 of 8 tasks done", "12 stories", "No task plan
/// yet". <paramref name="Value"/> is a LAYOUT number: it is what Plotly needs to size a sector, and the owner's
/// 2026-07-25 verify round named it exactly right — "weight is a confusing value on the tooltip that is not
/// helpful or intuitive for the reader". So `Value` stays in the payload because the chart cannot draw without
/// it, and `Detail` is what a person is ever shown, in the tooltip and in the accessible name. Empty when the
/// node's own label already carries its count ("Epic 7: 3 open follow-ups").</param>
/// <param name="ColorClass">The node's RESOLVABLE CSS CLASS LIST — the string the client puts on a throwaway
/// element to read its fill and stroke back out of the shipped cascade. It is a class list rather than a token
/// name because the component now serves more than one colour family: the planning surfaces emit
/// <c>"sb-seg sb-done"</c> and the Impact Map emits <c>"impact-tm-tile impact-level-3"</c>, and a resolver that
/// composed <c>"sb-seg " + map[statusClass]</c> could only ever speak the first.
/// <para><b>Why the server decides and the client only applies.</b> AD-7: a colour VALUE must never be typed in
/// JS, because a token change has to move the chart with it. Emitting the class list keeps that true while
/// removing the last piece of family knowledge from the script — the old <c>STATUS_CLASS</c> map WAS a second
/// place the vocabulary lived. Story 20.9 adds its eleven colorize dimensions on this same seam.</para>
/// <para><see cref="StatusClass"/> stays and keeps its three jobs: the text twin, the accessible name, and the
/// <c>data-tok-*</c> publication the pure-CSS drilled legend consumes. It is the node's IDENTITY; this is only
/// how it paints. [Story 20.7 F3(a)]</param>
public sealed record HierarchyNode(
    string Id,
    string? ParentId,
    string Label,
    string ShortLabel,
    int Value,
    string Detail,
    string StatusClass,
    string StatusLabel,
    string? Href,
    string Kind,
    string ColorClass = "");

/// <summary>How the text twin PRESENTS. It never changes what the twin contains — ADR 0013 §2's completeness
/// contract is identical in both modes — only whether a sighted reader sees a disclosure control for it.
/// [Story 20.6, owner decisions D3/D4]</summary>
public enum HierarchyTwinDisplay
{
    /// <summary>The default (owner D3): a closed <c>&lt;details&gt;</c>. <c>&lt;details&gt;</c> works with no
    /// script, so a JS-off visitor reaches the full listing in one click.</summary>
    Details,

    /// <summary>Visually hidden, still in the accessibility tree (owner D4). For surfaces that ALREADY carry a
    /// visible companion panel — the dashboard's <c>SunburstCompanionList</c> tile grid and the Story 20.3 rail —
    /// where a second visible listing would be on-screen duplication. The twin still discharges the completeness
    /// contract; the tile grid keeps its product value as a navigation aid.</summary>
    ScreenReaderOnly,
}

/// <summary>The component's own configuration, embedded in the island beside the nodes. ADR 0013 §5: the IR
/// carries chart <b>data + component configuration</b> — this is that shape, arriving early. Story 20.6's
/// fingerprint-replacement assertions assert on it. [Story 20.5]</summary>
/// <param name="DomId">Unique per instance. Drives the host id, the island id, the selector radio ids and the
/// twin — two instances on one page must not collide.</param>
/// <param name="Shape">The shape shown first: <c>sunburst</c> or <c>treemap</c>.</param>
/// <param name="Mode">See <see cref="HierarchyMode"/>.</param>
/// <param name="HashKey">The URL-fragment key for deep-linking the drilled scope (UX-DR6). The dashboard keeps
/// <c>sb</c> so links already shared keep working.</param>
/// <param name="Size">Chart size in px. Config-driven so no literal ever lands in the JS.</param>
/// <param name="Labels">Whether to draw in-sector labels (owner decision D3, "Labelled explorer").</param>
/// <param name="Meta">The Story 10.2 framing block — title + analysis window + framing sentence.</param>
/// <param name="TwinDisplay">How the text twin presents (owner D3/D4). Config-driven, never a call-site literal
/// and never a second twin builder. Unlike <paramref name="Size"/>, it is server-only: it never reaches
/// <see cref="IslandHtml"/>'s emitted client configuration, because the client has no reason to know how the twin
/// it never renders is presented. Trailing and defaulted so every existing call site keeps compiling and keeps the
/// D3 default.</param>
/// <param name="Filterable">Whether this instance honours root-subtree filter controls (Story 20.7 Task 1.3).
/// When set, the client watches for <c>[data-hierarchy-filter]</c> checkboxes inside the panel — each carrying the
/// id of a ROOT CHILD as its value — projects the payload to the checked roots plus their descendants, re-runs the
/// parent roll-up client-side, and re-plots. It is config-gated and generic on purpose: the Impact Map's epic
/// multi-select is the first consumer and Story 20.9's is the second, and an Impact-Map-shaped branch inside the
/// shared component is precisely the drift ADR 0012 exists to end.</param>
public sealed record HierarchyExplorerConfig(
    string DomId,
    string Shape,
    HierarchyMode Mode,
    string HashKey,
    int Size,
    bool Labels,
    Charts.ChartMeta Meta,
    HierarchyTwinDisplay TwinDisplay = HierarchyTwinDisplay.Details,
    bool Filterable = false);

/// <summary>The whole payload: component configuration + the node hierarchy. One datasource, both shapes — the
/// selector re-types the trace, it never re-derives geometry, re-counts against <see cref="ProjectCounts"/>, or
/// fetches (AC #1; <c>file://</c>-safe). [Story 20.5]</summary>
public sealed record HierarchyExplorerModel(
    HierarchyExplorerConfig Config,
    IReadOnlyList<HierarchyNode> Nodes);

/// <summary>The ONE standardized hierarchy surface (ADR 0012 §2): a sunburst and a treemap over the same
/// datasource, behind one selector, with an explicit activation mode. Every hierarchy call site routes through
/// here so a site-wide chart change lands in one place.
///
/// <para>Why a component and not a convention: ADR 0010 §6 already required one shared charting engine and it did
/// not hold — three concurrent sessions produced three independent arc renderers, three <c>Treemap | Sunburst</c>
/// toggles that disagree on ordering, and seven hierarchy entry points in <see cref="Charts"/>. A shared
/// convention is easy to defeat; a shared component is much harder to accidentally reinvent.</para>
///
/// <para><b>Host-neutral by construction (AD-2 / ADR 0002).</b> Everything here is a pure projection over an
/// already-built view model plus string building. No <see cref="ProjectCounts"/> re-count, no second geometry, no
/// git call, no adapter knowledge. <see cref="HtmlRenderAdapter"/> renders the string this produces; it does not
/// build one.</para></summary>
public static partial class HierarchyExplorer
{
    /// <summary>Id of the synthesized single root (Story 20.4 spike, Finding A). Plotly's hierarchy traces require
    /// <b>exactly one</b> root and refuse a forest outright — <i>"Multiple implied roots, cannot build sunburst
    /// hierarchy of trace 0"</i> — while the Story 20.2 payload is a 25-root forest (24 epics + <c>unplanned</c> +
    /// <c>orphan</c>). The hand-rolled SVG never noticed because its centre is a decorative circle, not a data
    /// node. Synthesized in the EMITTER rather than client-side so the payload is valid on its own and the text
    /// twin describes the same tree. It is also where Escape-to-top and the breadcrumb land.</summary>
    public const string ProjectRootId = "__project__";

    /// <summary>The <c>Kind</c> carried by <see cref="ProjectRootId"/>.</summary>
    public const string ProjectRootKind = "project";

    /// <summary>Prose "status" for the synthesized root. It is a scope, not a lifecycle stage, so it must not
    /// borrow a stage word — "Unrecognized" on the project node would read as a claim about the project.</summary>
    public const string ProjectRootStatusLabel = "Whole project";

    /// <summary>The Plotly <c>branchvalues</c> mode this payload is built for — and it is a contract, not a
    /// preference. <c>total</c> means "a parent's value already INCLUDES its children", which is exactly what
    /// <see cref="Reparent"/> produces under owner decision D2 (children win: a parent's value is the exact sum of
    /// its drawn children). A payload/<c>branchvalues</c> mismatch is the failure mode that renders a blank or
    /// wrong chart with only a console warning, so it is emitted in the island and asserted in a test rather than
    /// left as a shared assumption between C# and JS. [Story 20.4 Finding C; owner D2]</summary>
    public const string BranchValues = "total";

    /// <summary>The CSS class every sunburst wedge carries, and the one the client's colour probe needs in order
    /// to match the shipped <c>.sunburst .sb-seg.sb-&lt;token&gt;</c> rules. Named once here rather than typed into
    /// <see cref="PlanningColorClass"/> twice.</summary>
    public const string PlanningSegClass = "sb-seg";

    /// <summary>The status tokens the shipped <c>.sb-*</c> cascade actually paints. This list used to live in
    /// <c>specscribe.js</c> as <c>STATUS_CLASS</c>, where it was a second copy of the status vocabulary the client
    /// had to keep in step by hand; moving it here makes the emitter the only thing that knows the family, which is
    /// what lets a second family (the Impact Map's ramp, and Story 20.9's eleven dimensions) exist at all.</summary>
    private static readonly HashSet<string> PaintedStatusTokens = new(StringComparer.Ordinal)
    {
        "done", "active", "review", "ready", "drafted", "pending", "noplan",
        "followup-open", "followup-done", "unplanned", "unrecognized",
    };

    /// <summary>The planning family's <see cref="HierarchyNode.ColorClass"/>: the wedge class plus the node's own
    /// status token. An unpainted token falls back to <c>sb-unrecognized</c> — the same last-resort the client's
    /// old <c>STATUS_CLASS[…] || "sb-unrecognized"</c> took, preserved so the resolved colours are byte-identical
    /// to what the SVG drew rather than merely similar. [Story 20.7 Task 1.1]</summary>
    public static string PlanningColorClass(string statusClass) =>
        PaintedStatusTokens.Contains(statusClass)
            ? $"{PlanningSegClass} sb-{statusClass}"
            : $"{PlanningSegClass} sb-unrecognized";

    /// <summary>Builds the dashboard's Hierarchy Explorer model over the project-glance datasource.
    ///
    /// <para><b>It does not re-walk <see cref="EpicsModel"/>.</b> <see cref="Charts.SunburstExplorerNodes"/> stays
    /// the single walk and this is a thin adapter over its output — a second traversal is exactly the drift ADR
    /// 0012 exists to end, and <c>SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew</c> is the
    /// invariant that keeps payload and SVG honest while both are live.</para>
    ///
    /// <para><b>AC #4 is preserved by construction, not re-derived.</b> Leaf weights arrive already carrying the
    /// owner's 2026-07-24 no-plan average bump (<see cref="Charts.SunburstNoPlanStoryWeight"/> threaded through
    /// <c>SunburstStoryWeight</c>/<c>SunburstEpicWeight</c>), so an un-drafted story renders at a typical,
    /// clickable size rather than a hairline. Nothing below re-floors or recomputes them.</para></summary>
    public static HierarchyExplorerModel ProjectDashboard(
        EpicsModel model,
        string siteTitle,
        HierarchyExplorerConfig config,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        // `expandDenseEpics: true` — the component drills, so an epic's own view has the whole sweep to itself and
        // the static chart's "8 stories" collapse would only hide the stories the reader drilled in to find.
        var source = Charts.SunburstExplorerNodes(model, followUps, unplanned, expandDenseEpics: true);
        var nodes = Reparent(source, siteTitle, SiteNav.HomeOutputPath);
        return new HierarchyExplorerModel(config, WithDetails(nodes, model));
    }

    /// <summary>Fills each node's <see cref="HierarchyNode.Detail"/> — the sentence a reader actually sees in place
    /// of the layout number. A lookup against the SAME <see cref="EpicsModel"/> the projection came from, never a
    /// second walk of it, and phrased the way the shipped SVG's own <c>&lt;title&gt;</c> phrases it so the two
    /// charts cannot describe one story differently.</summary>
    private static IReadOnlyList<HierarchyNode> WithDetails(IReadOnlyList<HierarchyNode> nodes, EpicsModel model)
    {
        if (nodes.Count == 0) return nodes;

        var storiesById = new Dictionary<string, StoryInfo>(StringComparer.Ordinal);
        var epicsByIsland = new Dictionary<string, EpicInfo>(StringComparer.Ordinal);
        foreach (var epic in model.Epics)
        {
            epicsByIsland.TryAdd($"epic-{epic.Number}", epic);
            foreach (var story in epic.Stories) storiesById.TryAdd(story.Id, story);
        }

        var epicCount = model.Epics.Count;
        var result = new List<HierarchyNode>(nodes.Count);
        foreach (var n in nodes)
        {
            var detail = n.Kind switch
            {
                ProjectRootKind => $"{epicCount} {Charts.Plural(epicCount, "epic", "epics")}",
                "epic" when epicsByIsland.TryGetValue(n.Id, out var e) =>
                    $"{e.Stories.Count} {Charts.Plural(e.Stories.Count, "story", "stories")}",
                "story" when storiesById.TryGetValue(n.Id, out var s) =>
                    // Matches Charts.Sunburst's own wedge <title>: an un-drafted story says so in words rather than
                    // reporting "0 of 0 tasks done", which reads as failure instead of as not-yet-planned.
                    s.TasksTotal == 0 ? "No task plan yet" : $"{s.TasksDone} of {s.TasksTotal} tasks done",
                // story-summary, aggregate, follow-up and unplanned nodes already state their count in their own
                // label ("Epic 7: 3 open follow-ups"), so a Detail here would only repeat it.
                _ => string.Empty,
            };
            result.Add(detail.Length == 0 ? n : n with { Detail = detail });
        }
        return result;
    }

    /// <summary>Maps the Story 20.2 explorer nodes onto the Plotly hierarchy contract: one synthesized root
    /// (Finding A), prose status labels, and parent values that are the exact sum of their emitted children
    /// (Finding C, resolved by owner decision D2 — children win, so the rings can never disagree and a child's
    /// angle is comparable across the whole chart).
    ///
    /// <para>Returns an empty list for an empty source, so a project with no epics ships no island, no host and no
    /// inert selector rather than an empty chart frame (NFR8).</para></summary>
    public static IReadOnlyList<HierarchyNode> Reparent(
        IReadOnlyList<SunburstExplorerNode> source, string rootLabel, string rootHref)
    {
        if (source.Count == 0) return Array.Empty<HierarchyNode>();

        // Finding A: every parentless node becomes a child of the one synthesized root. Draw order is preserved
        // (the root leads), so the payload still mirrors the order the SVG drew.
        // The root's "status" is not a lifecycle stage, so it gets prose of its own rather than a stage word that
        // would read as a claim about the project. Its statusClass is the neutral `unrecognized` token purely so
        // the colour cascade resolves it to plain ink like every other unclassified mark.
        var nodes = new List<HierarchyNode>(source.Count + 1)
        {
            new(ProjectRootId, null, rootLabel, rootLabel, 0, string.Empty, "unrecognized",
                ProjectRootStatusLabel, rootHref, ProjectRootKind, PlanningColorClass("unrecognized")),
        };
        foreach (var n in source)
        {
            nodes.Add(new HierarchyNode(
                n.Id, n.ParentId ?? ProjectRootId, n.Label, ShortLabelFor(n), n.Weight, string.Empty,
                n.StatusClass, StatusLabelFor(n.StatusClass, n.Kind), n.Href, n.Kind,
                PlanningColorClass(n.StatusClass)));
        }

        return RollUpParentValues(nodes);
    }

    /// <summary>Finding C / owner D2: rewrites every node that HAS children to the sum of those children's
    /// (already rolled-up) values, bottom-up. Leaves are untouched, so every honest weight — including the AC #4
    /// no-plan average bump — survives verbatim; the roll-up only ever changes a parent.
    ///
    /// <para>Iterative rather than recursive, and tolerant of a cycle: node ids come from author-controlled
    /// markdown (<c>### Story N.M:</c> headings, which nothing dedupes), so a hostile or merely careless authoring
    /// input must not be able to stack-overflow generation.</para></summary>
    /// <summary>The roll-up seam every projector ends on. Named separately from <see cref="Reparent"/> because the
    /// three Story 20.7 projectors build their own node list (they are not projecting a
    /// <see cref="SunburstExplorerNode"/> forest) but must still land on the SAME Finding-C rule — a second
    /// implementation of "children win" is exactly how two charts start disagreeing.</summary>
    internal static IReadOnlyList<HierarchyNode> RollUp(List<HierarchyNode> nodes) => RollUpParentValues(nodes);

    private static IReadOnlyList<HierarchyNode> RollUpParentValues(List<HierarchyNode> nodes)
    {
        var childrenOf = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            // First-wins on a duplicate id, matching the projector's own dedupe rule.
            indexOf.TryAdd(nodes[i].Id, i);
        }
        for (var i = 0; i < nodes.Count; i++)
        {
            var parent = nodes[i].ParentId;
            if (parent is null || !indexOf.ContainsKey(parent)) continue;
            if (!childrenOf.TryGetValue(parent, out var list)) childrenOf[parent] = list = new List<int>();
            list.Add(i);
        }

        var values = nodes.Select(n => n.Value).ToArray();
        var done = new bool[nodes.Count];

        // Post-order over an explicit stack: visit children first, then sum. `onStack` guards a cycle — a node
        // reached twice on the same descent keeps its own value rather than recursing forever.
        var onStack = new bool[nodes.Count];
        for (var start = 0; start < nodes.Count; start++)
        {
            if (done[start]) continue;
            var stack = new Stack<(int Index, bool Expanded)>();
            stack.Push((start, false));
            while (stack.Count > 0)
            {
                var (i, expanded) = stack.Pop();
                if (done[i]) continue;
                if (!expanded)
                {
                    if (onStack[i]) { done[i] = true; continue; }
                    onStack[i] = true;
                    stack.Push((i, true));
                    if (childrenOf.TryGetValue(nodes[i].Id, out var kids))
                    {
                        foreach (var k in kids) if (!done[k]) stack.Push((k, false));
                    }
                    continue;
                }
                onStack[i] = false;
                if (childrenOf.TryGetValue(nodes[i].Id, out var children))
                {
                    var sum = 0;
                    foreach (var k in children) sum += values[k];
                    values[i] = sum;
                }
                done[i] = true;
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (values[i] != nodes[i].Value) nodes[i] = nodes[i] with { Value = values[i] };
        }
        return nodes;
    }

    // -----------------------------------------------------------------------------------------------------------
    // The server-rendered scaffold. Render() returns the WHOLE framed block so no call site hand-writes any part
    // of it — that single-source discipline is the point of the component (ADR 0012 §2).
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>Renders the complete Hierarchy Explorer block: the Story 10.2 framing panel (title + analysis
    /// window + framing sentence, via <see cref="Charts.Framed"/>), the shape selector, the breadcrumb bar, the
    /// chart host, the live region, the data island, and the text twin. A call site appends this one string.
    ///
    /// <para>Returns "" for an empty model — no island, no host, no inert selector (NFR8).</para>
    ///
    /// <para><paramref name="panelClass"/> is threaded rather than fixed because the dashboard's panel MUST keep
    /// <c>sunburst-panel</c>: the Story 3.5 legend-emphasis CSS is <c>.sunburst-panel:has(.sb-&lt;status&gt;-item:hover) …</c>,
    /// and dropping the class silently kills it (and three <c>StylesheetTests</c> assertions).</para>
    ///
    /// <para><b>The <c>fallbackHtml</c> slot is gone.</b> It was owner decision D1 of Story 20.5 made concrete —
    /// the server-rendered SVG this component took over from, kept inside the same panel so a failed mount left the
    /// reader with a chart. Story 20.7 retires those SVGs, so the argument goes away with the thing it carried.
    /// What stands behind a failed mount now is the TEXT TWIN, which is ADR 0013 §2's contract and is present on
    /// every instance regardless.</para>
    ///
    /// <para><paramref name="controlsHtml"/> and <paramref name="legendHtml"/> are the two slots that let a surface
    /// with its own vocabulary through the one component (Story 20.7 Task 7.2/7.3). Controls are appended INSIDE
    /// the same <c>hidden</c> control bar as the shape selector, so a surface's own controls inherit the reveal
    /// handshake rather than re-inventing it — a JS-off visitor never sees an inert control. A non-null
    /// <paramref name="legendHtml"/> replaces the status legend for a family that does not speak
    /// <c>--status-*</c>; passing "" is a deliberate "this surface has no legend", which is why it is nullable
    /// rather than empty-checked.</para></summary>
    public static string Render(
        HierarchyExplorerModel model,
        string panelClass = "chart-panel",
        string panelAttributes = "",
        string controlsHtml = "",
        string? legendHtml = null)
    {
        if (model.Nodes.Count == 0) return string.Empty;

        var cfg = model.Config;
        var id = cfg.DomId;
        var body = new StringBuilder();

        // --- Shape selector. The existing `.board-tabs` radio idiom (CodeMapTemplater / GitInsightsTemplater),
        // ordered Sunburst-then-Treemap. That single ordering is the divergence Story 20.7 AC#1 exists to end —
        // three shipped toggles disagree today — so it is fixed HERE, once, for 20.7 to copy.
        // Emitted `hidden`: switching a Plotly trace type requires script, so with JS off this would be an inert
        // control. Same convention `codemap-controls` / `sb-explorer-drill` already follow; the component reveals
        // it on a successful mount.
        body.Append($"<div class=\"ss-hierarchy-controls\" hidden>\n");
        body.Append("  <div class=\"board-tabs\">");
        body.Append($"<input type=\"radio\" id=\"{PathUtil.Html(id)}-shape-sunburst\" name=\"{PathUtil.Html(id)}-shape\" class=\"board-tab-radio ss-hierarchy-shape\" value=\"sunburst\"{Checked(cfg.Shape, "sunburst")}>");
        body.Append($"<input type=\"radio\" id=\"{PathUtil.Html(id)}-shape-treemap\" name=\"{PathUtil.Html(id)}-shape\" class=\"board-tab-radio ss-hierarchy-shape\" value=\"treemap\"{Checked(cfg.Shape, "treemap")}>");
        body.Append("<div class=\"board-tabbar\">");
        body.Append($"<label for=\"{PathUtil.Html(id)}-shape-sunburst\" class=\"board-tab\">Sunburst</label>");
        body.Append($"<label for=\"{PathUtil.Html(id)}-shape-treemap\" class=\"board-tab\">Treemap</label>");
        body.Append("</div></div>\n");
        // A surface's own controls ride inside the SAME hidden bar, so they are revealed by the same successful
        // mount and hidden by the same JS-off page. Nothing here knows what they are.
        body.Append(controlsHtml);
        body.Append("</div>\n");

        // --- Breadcrumb (drill scope) + the polite live region the a11y layer announces through.
        body.Append("<div class=\"ss-hierarchy-drill\" hidden><ol class=\"ss-hierarchy-breadcrumb\" aria-label=\"Zoom scope\"></ol></div>\n");

        // --- The boot placeholder — the anti-flash half of the JS handshake.
        //
        // Without it a scripted visitor sees the server SVG paint, then a differently-organized Plotly chart
        // replace it a moment later — the owner's words: "a jarring experience ... I'd rather see an
        // 'Initializing...' if JS is present." The marker that reveals this is set by
        // <see cref="HtmlRenderAdapter"/> at the CHROME level, before the body is parsed: nothing deferred could
        // prevent the flash, because `specscribe.js` runs after the document is parsed and the SVG has painted.
        // It lives there rather than here precisely so the webview and SPA surfaces — which consume this body
        // directly and must carry no script — never receive it.
        // The placeholder itself. Sized to the chart it is standing in for, so the swap costs no reflow.
        body.Append($"<div class=\"ss-hierarchy-booting\" role=\"status\" style=\"min-height:{cfg.Size}px\">")
            .Append("<span>Initializing chart&hellip;</span></div>\n");

        // --- Chart host. EMPTY at render time and `display:none` until the component reveals it (the CSS default
        // for `.ss-hierarchy`). Reserving its height server-side would leave a JS-off visitor staring at a blank
        // box the size of a chart that is never coming; the component sets the height from `config.size` at the
        // moment it mounts, so a JS-on page still does not reflow. Height is never a literal in the JS.
        body.Append($"<div class=\"ss-hierarchy\" id=\"{PathUtil.Html(id)}\" {HostMarker}></div>\n");
        body.Append($"<div class=\"ss-hierarchy-live sr-only\" aria-live=\"polite\"></div>\n");

        body.Append(legendHtml ?? LegendHtml(model));
        body.Append(IslandHtml(model));
        body.Append(TextTwinHtml(model));

        return Charts.Framed(cfg.Meta, body.ToString(), panelClass, panelAttributes);
    }

    private static string Checked(string shape, string value) =>
        string.Equals(shape, value, StringComparison.Ordinal) ? " checked" : string.Empty;

    /// <summary>The component's OWN legend — AC#1's "one framing block (legend + analysis window + framing
    /// sentence), so no call site hand-writes any of them."
    ///
    /// <para>It did not exist until the Story 20.5 code review. <see cref="Charts.Framed"/> has no legend slot, and
    /// the only legend on the dashboard came from <see cref="Charts.SunburstLegend"/> — emitted INSIDE
    /// <c>Charts.Sunburst</c>, i.e. inside the D1 fallback that Story 20.7 deletes. Owner decision 2026-07-26: the
    /// component owns its legend, so 20.7's deletion cannot take the dashboard's legend with it.</para>
    ///
    /// <para><b>It describes the channel actually on screen.</b> The retained SVG's legend encodes fill +
    /// <em>stroke-dash</em>; Plotly's <c>marker.line</c> has no dash, so the component signals those same four
    /// statuses with <c>marker.pattern.shape</c> HATCHING instead. A legend showing dashes beside a chart drawing
    /// hatches is the "phantom / misdescribing entry" class Stories 10.7 and 21.1 each closed. The swatch classes
    /// here carry the hatch, and the note names it in prose so the channel is never colour-only (UX-DR17).</para>
    ///
    /// <para>Entries are the statuses the payload ACTUALLY carries — never a fixed list, so a legend row can never
    /// point at zero sectors. The prose comes from each node's own already-resolved <see cref="HierarchyNode.StatusLabel"/>
    /// rather than a second lookup, which is what makes chart, legend, tooltip, accessible name and text twin
    /// incapable of disagreeing. The synthesized root is excluded: it is the whole project, not a lifecycle stage,
    /// and it is described by the breadcrumb and the twin instead.</para>
    ///
    /// <para><b>It renders through <see cref="Charts.SunburstLegend"/>, and the markup family is load-bearing</b>
    /// (Story 20.7 Task 2.2). The pure-CSS DRILLED-LEGEND FILTERING — <c>[data-explorer][data-sb-scope]
    /// .sunburst-legend .sb-legend-item { display: none }</c> plus one <c>data-tok-*</c> re-show per status — acts
    /// on legend items, and it is the half of the legend's behaviour that survives the SVG's retirement. It only
    /// keeps working if this legend IS a <c>.sunburst-legend</c> with <c>.sb-&lt;status&gt;-item</c> children. The
    /// component's earlier <c>.ss-hierarchy-legend</c> family silently sat outside those selectors, so the
    /// dashboard's drilled filtering was in fact still being done by the retained SVG's legend.</para>
    ///
    /// <para><b>What does NOT survive:</b> the Story 3.5 hover-emphasis (<c>.sunburst-panel:has(.sb-review-item:hover)
    /// .sb-seg:not(.sb-review)</c>) dims <c>.sb-seg</c> wedges, and Plotly draws <c>path.surface</c>. Those rules
    /// match nothing once the SVG is gone. Re-creating the behaviour would require the script to reach Plotly's
    /// sectors from a legend handler, which <c>StylesheetTests.Script_DoesNotImplementLegendEmphasis</c> exists to
    /// forbid. Recorded as a loss rather than routed around. [Story 20.7 F1 / Open Question 1]</para></summary>
    internal static string LegendHtml(HierarchyExplorerModel model)
    {
        // First label wins per status class, in canonical stage order then first-drawn order — deterministic for
        // FR31, and identical to what the chart drew because it IS what the chart drew.
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var n in model.Nodes)
        {
            if (n.Id == ProjectRootId) continue;
            if (string.IsNullOrEmpty(n.StatusClass) || string.IsNullOrEmpty(n.StatusLabel)) continue;
            if (seen.ContainsKey(n.StatusClass)) continue;
            seen[n.StatusClass] = n.StatusLabel;
            order.Add(n.StatusClass);
        }
        if (order.Count == 0) return string.Empty;

        var items = Charts.SunburstLegendItemsPresent(order.Select(s => (s, seen[s])).ToList());
        if (items.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append(Charts.SunburstLegend(items));
        if (items.Any(i => Charts.SunburstLocalStatusLabel(i.Status) is not null))
            sb.Append("<p class=\"ss-hierarchy-legend-note\">Hatched sectors are work outside the normal story lifecycle &mdash; follow-ups, direct changes, and stories with no task plan yet.</p>\n");
        return sb.ToString();
    }

    /// <summary>The inline JSON island — the component's ONLY data source. No fetch, so it is <c>file://</c>-safe
    /// and survives the webview's CSP and the SPA's content capture. Carries the component CONFIG alongside the
    /// nodes (ADR 0013 §5: the IR carries chart data + component configuration).
    ///
    /// <para>The id is per-<see cref="HierarchyExplorerConfig.DomId"/> and deliberately NOT
    /// <c>sunburst-explorer-data</c>: Story 20.2's island is still live and still read by 20.2's JS block until
    /// Story 20.7 retires it, and two instances on one page must not collide either.</para>
    ///
    /// <para><see cref="JsonSerializer"/>'s default encoder escapes <c>&lt; &gt; &amp;</c>, so the payload is safe
    /// to embed directly inside a <c>&lt;script&gt;</c> (the same reasoning
    /// <see cref="Charts.SunburstExplorerIsland"/> relies on).</para></summary>
    public static string IslandHtml(HierarchyExplorerModel model)
    {
        if (model.Nodes.Count == 0) return string.Empty;

        var cfg = model.Config;
        var payload = new
        {
            config = new
            {
                domId = cfg.DomId,
                // The framed title travels with the payload so the chart's own accessible name is the SAME string
                // the visible heading uses — and regenerates on a shape switch ("… — sunburst" / "… — treemap")
                // without the client inventing a second name for the panel.
                title = cfg.Meta.Title,
                shape = cfg.Shape,
                mode = cfg.Mode == HierarchyMode.Select ? "select" : "navigate",
                hashKey = cfg.HashKey,
                size = cfg.Size,
                labels = cfg.Labels,
                // Emitted, not assumed: the payload is parent-inclusive by construction (owner D2), and a
                // payload/branchvalues mismatch renders wrong with only a console warning.
                branchvalues = BranchValues,
                // Task 1.3. Off unless a surface asked for it, so no instance grows a control-scanning path it
                // has no controls for.
                filterable = cfg.Filterable,
            },
            nodes = model.Nodes.Select(n => new
            {
                id = n.Id,
                parentId = n.ParentId,
                label = n.Label,
                shortLabel = n.ShortLabel,
                value = n.Value,
                detail = n.Detail,
                statusClass = n.StatusClass,
                statusLabel = n.StatusLabel,
                // The resolvable class list (Task 1.1). Applied VERBATIM by the client's probe — it composes
                // nothing, so a family it has never heard of resolves exactly as its stylesheet says.
                colorClass = n.ColorClass,
                href = n.Href,
                kind = n.Kind,
            }),
        };
        var json = JsonSerializer.Serialize(payload);
        return $"<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"{PathUtil.Html(model.Config.DomId)}-data\">{json}</script>\n";
    }

    /// <summary>The text twin — mandatory, and under ADR 0013 §2 it is <b>the</b> no-JS contract rather than a
    /// courtesy: server-rendered, COMPLETE (every node's label, prose status and value), NAVIGABLE (every node's
    /// href is a real resolving link), non-color, and nested by <c>parentId</c> so the hierarchy itself is legible
    /// without the picture.
    ///
    /// <para>Presentation is <see cref="HierarchyExplorerConfig.TwinDisplay"/>'s call, and it changes ONLY the
    /// wrapper — the listing inside is byte-identical in both modes, because ADR 0013 §2's completeness contract
    /// does not vary by surface. <see cref="HierarchyTwinDisplay.Details"/> (owner D3, the default) ships a closed
    /// <c>&lt;details&gt;</c>: visually collapsed is explicitly acceptable (ADR 0013 §2 — availability, not
    /// on-screen duplication) and it opens with no script. <see cref="HierarchyTwinDisplay.ScreenReaderOnly"/>
    /// (owner D4) is for surfaces that already carry a visible companion listing.</para>
    ///
    /// <para><b>Why the sr-only variant is a <c>&lt;section&gt;</c> with an accessible name, and why the CSS
    /// reveals it on focus.</b> <c>.sr-only</c> is the clip-rect technique, so it deliberately stays in the
    /// accessibility tree — which is the whole point, and also a hazard: the dashboard's listing carries 200+
    /// links, and a clipped-but-focusable run of that size is an invisible tab tunnel for a SIGHTED keyboard user.
    /// Story 20.2's review caught the mirror-image bug live (an SVG <c>&lt;a&gt;</c> at <c>display:none</c> stays
    /// focusable) and the suite could not see it. So the twin keeps its links reachable — removing them from the
    /// tab order would break the navigation half of NFR-5 — and <c>.ss-hierarchy-twin.sr-only:focus-within</c>
    /// un-clips the container the moment focus enters it, the same pattern skip links use. Nothing is hidden from
    /// anyone; it simply stops being invisible once you are in it.</para>
    ///
    /// <para>Class family is the component's own <c>.ss-hierarchy-*</c>, never 20.2's <c>.sb-explorer-*</c>, so
    /// Story 20.7 can delete 20.2's markup and CSS cleanly.</para></summary>
    public static string TextTwinHtml(HierarchyExplorerModel model)
    {
        if (model.Nodes.Count == 0) return string.Empty;

        var childrenOf = new Dictionary<string, List<HierarchyNode>>(StringComparer.Ordinal);
        var roots = new List<HierarchyNode>();
        foreach (var n in model.Nodes)
        {
            if (n.ParentId is null) { roots.Add(n); continue; }
            if (!childrenOf.TryGetValue(n.ParentId, out var list)) childrenOf[n.ParentId] = list = new List<HierarchyNode>();
            list.Add(n);
        }

        var id = PathUtil.Html(model.Config.DomId);
        var heading = $"{PathUtil.Html(model.Config.Meta.Title)} — full text listing";
        var srOnly = model.Config.TwinDisplay switch
        {
            HierarchyTwinDisplay.Details => false,
            HierarchyTwinDisplay.ScreenReaderOnly => true,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.Config.TwinDisplay, "Unrecognized HierarchyTwinDisplay value."),
        };

        var sb = new StringBuilder();
        if (srOnly)
        {
            // A <section> with aria-labelledby rather than a bare <div>: a landmark with an accessible name is how
            // a screen-reader user FINDS this listing without tabbing to it, which matters more here than in the
            // <details> mode where a visible summary already advertises it. <h4>, not <h3>: this section nests
            // INSIDE the chart panel Charts.Framed already headed with its own <h3>{Title}</h3>, so a heading-level
            // reader would otherwise hit two same-level, near-identical headings for one panel.
            sb.Append($"<section class=\"ss-hierarchy-twin sr-only\" id=\"{id}-twin\" aria-labelledby=\"{id}-twin-title\">\n");
            sb.Append($"<h4 class=\"ss-hierarchy-twin-title\" id=\"{id}-twin-title\">{heading}</h4>\n");
            AppendTwinLevel(sb, roots, childrenOf, new HashSet<string>(StringComparer.Ordinal), 1);
            sb.Append("</section>\n");
        }
        else
        {
            sb.Append($"<details class=\"ss-hierarchy-twin\" id=\"{id}-twin\">\n");
            sb.Append($"<summary>{heading}</summary>\n");
            AppendTwinLevel(sb, roots, childrenOf, new HashSet<string>(StringComparer.Ordinal), 1);
            sb.Append("</details>\n");
        }
        return sb.ToString();
    }

    private static void AppendTwinLevel(
        StringBuilder sb, List<HierarchyNode> level,
        Dictionary<string, List<HierarchyNode>> childrenOf, HashSet<string> visiting, int depth)
    {
        if (level.Count == 0) return;
        sb.Append("<ul class=\"ss-hierarchy-twin-list\">\n");
        foreach (var n in level)
        {
            sb.Append("<li>");
            // Navigable: a real resolving <a>, not a label. A node with no destination is rendered as plain text
            // rather than a dead link — an honest gap beats a link that goes nowhere.
            sb.Append(n.Href is { Length: > 0 } href
                ? $"<a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(n.Label)}</a>"
                : PathUtil.Html(n.Label));
            // Status as a WORD and weight as a number — the whole non-color reading of the chart (UX-DR17).
            var meta = n.Detail.Length > 0
                ? $"{PathUtil.Html(n.StatusLabel)} &middot; {PathUtil.Html(n.Detail)}"
                : PathUtil.Html(n.StatusLabel);
            sb.Append($" <span class=\"ss-hierarchy-twin-meta\">{meta}</span>");
            // Cycle guard: ids come from author-controlled markdown, and a self-parenting node must not hang
            // generation. `visiting` is added LAST so a failed descent never removes an id an ancestor owns.
            // Depth cap for the same reason.
            if (childrenOf.TryGetValue(n.Id, out var kids) && depth < 12 && visiting.Add(n.Id))
            {
                sb.Append('\n');
                AppendTwinLevel(sb, kids, childrenOf, visiting, depth + 1);
                visiting.Remove(n.Id);
            }
            sb.Append("</li>\n");
        }
        sb.Append("</ul>\n");
    }

    /// <summary>The chart host's opt-in marker — the ONE string that names the class ↔ script ↔ asset-flag
    /// contract, so no consumer re-types it.</summary>
    public const string HostMarker = "data-hierarchy";

    /// <summary>Set on the PANEL by the client the moment a mount succeeds. It ends the boot placeholder and
    /// disarms the inline script's expiry timer, so the two never fight over whether to show the server SVG.</summary>
    public const string MountedMarker = "data-hierarchy-mounted";

    /// <summary>Set on the panel by the client when a mount DECLINES or throws, so the server SVG comes straight
    /// back instead of the reader watching a placeholder until <see cref="BootTimeoutMs"/> expires.</summary>
    public const string FailedMarker = "data-hierarchy-failed";

    /// <summary>How long the boot placeholder may stand before it gives up and hands the page back to the server
    /// SVG. Long enough for a large bundle to parse and plot on a slow machine, short enough that a blocked script
    /// is not mistaken for a slow one.</summary>
    public const int BootTimeoutMs = 5000;

    /// <summary>The anti-flash handshake, injected by <see cref="HtmlRenderAdapter"/> BEFORE the page body so it
    /// runs while the body is still being parsed — the only moment at which the server SVG can be suppressed
    /// without the reader seeing it paint first. <c>specscribe.js</c> is <c>defer</c>, so it cannot do this.
    ///
    /// <para>It lives on the CHROME seam rather than inside the component's markup for a hard reason: the webview
    /// and SPA surfaces consume <see cref="PageView.BodyHtml"/> directly and must carry <b>no</b> script
    /// (<c>SiteGeneratorWebviewTests.EverySurface_CarriesTheChromeAndNoScript</c> pins that, and owner decision D4
    /// forbids touching <see cref="WebviewRenderAdapter"/>). Emitting it here keeps both true.</para>
    ///
    /// <para>The expiry is what keeps owner decision D1 honest. If the bundle is blocked or missing,
    /// <c>specscribe.js</c> may never mount anything — and a hide-first with no timeout would leave a permanent
    /// "Initializing…" over a chart that exists and works. So the marker is removed from any panel that has neither
    /// mounted nor reported failure, and the server SVG is simply the page.</para></summary>
    public static readonly string BootScript =
        "<script>(function(){var r=document.documentElement;r.setAttribute('data-ss-hierarchy-boot','1');"
        + $"setTimeout(function(){{r.removeAttribute('data-ss-hierarchy-boot');}},{BootTimeoutMs});}})();</script>\n";

    /// <summary>Whether a rendered body carries a Hierarchy Explorer host. The producer of an
    /// <see cref="AssetManifest"/> calls this over the FINISHED body, mirroring
    /// <c>Mermaid.ContainsBlock</c> — a flag derived from the page cannot disagree with the page.</summary>
    public static bool ContainsHost(string bodyHtml) =>
        bodyHtml.Contains(HostMarker, StringComparison.Ordinal);

    /// <summary>The short, identifier-only label drawn inside a sector — see
    /// <see cref="HierarchyNode.ShortLabel"/> for why it exists. Derived from the node's own <c>Kind</c> and the
    /// label the projector already composed, never re-derived from <see cref="EpicsModel"/>:
    /// <list type="bullet">
    /// <item>epic / story → the part before the colon ("Epic 7", "Story 20.5")</item>
    /// <item>a dense epic's collapsed summary → the part after it ("14 stories")</item>
    /// <item>a follow-up aggregate → "<c>N open</c>" / "<c>N done</c>", from its own id suffix — never the epic's
    /// name, which would put two sectors reading "Epic 7" in one chart</item>
    /// <item>the orphan / unplanned roots → their family name alone</item>
    /// </list>
    /// Falls back to the full label when nothing matches, so an unrecognized shape is merely long, never blank.</summary>
    public static string ShortLabelFor(SunburstExplorerNode n)
    {
        var colon = n.Label.IndexOf(':');
        var head = colon > 0 ? n.Label[..colon].Trim() : n.Label;
        var tail = colon > 0 && colon + 1 < n.Label.Length ? n.Label[(colon + 1)..].Trim() : string.Empty;

        return n.Kind switch
        {
            "epic" or "story" => head,
            "story-summary" => tail.Length > 0 ? tail : head,
            "aggregate" => n.Id.EndsWith("~open", StringComparison.Ordinal) ? $"{n.Weight} open"
                : n.Id.EndsWith("~done", StringComparison.Ordinal) ? $"{n.Weight} done"
                : head,
            "follow-up" or "unplanned" => head,
            _ => n.Label,
        };
    }

    /// <summary>The prose status word for a node — <see cref="Charts.SunburstLocalStatusLabel"/> for the four
    /// chart-local classes that have no lifecycle token, otherwise the <see cref="StatusStyles"/> vocabulary from
    /// the node's own point of view (an epic reads "Stories drafted", a story reads "Drafted"). Never invents a
    /// second phrasing for a status the legend already names.</summary>
    public static string StatusLabelFor(string statusClass, string kind) =>
        Charts.SunburstLocalStatusLabel(statusClass)
        ?? (kind is "epic" or ProjectRootKind
            ? StatusStyles.EpicLabel(statusClass)
            : StatusStyles.StoryLabel(statusClass));
}
