using System.Text.Json;

namespace SpecScribe;

/// <summary>One node of the Story 20.2 sunburst-explorer payload: the projection of a single drawn wedge onto the
/// data the client drill-in reads. <see cref="Id"/> is the canonical identity the wedge's <c>data-node-id</c> also
/// carries (so the client joins DOM ↔ payload with no ambiguity, and Story 20.3's edges join by the same grain);
/// <see cref="Weight"/> is the SAME number <see cref="Charts.Sunburst"/> used to size the wedge (never a re-count —
/// see <see cref="Charts.SunburstEpicWeight"/>/<see cref="Charts.SunburstStoryWeight"/>); <see cref="Kind"/> drives
/// the zoom-vs-open rule (a wedge with <c>story</c> children drills, a leaf opens its <see cref="Href"/> — the Story
/// 9.13 destination already on the wedge's <c>&lt;a&gt;</c>); <see cref="Ring"/> states which radial band the SVG
/// actually drew it on. <see cref="Kind"/> and <see cref="Ring"/> are deliberately SEPARATE facts: an epic's
/// open/done aggregates are drawn on the aggregate band, but the orphan and unplanned roots' aggregates are drawn on
/// the STORY band, so inferring the ring from the kind is wrong for those. [Story 20.2; Ring added by 20.2 review]</summary>
public sealed record SunburstExplorerNode(
    string Id,
    string? ParentId,
    int Weight,
    string Label,
    string StatusClass,
    string? Href,
    string Kind,
    string Ring);

/// <summary>The presentation geometry the client re-layout needs to land zoomed arcs on the SAME rings the static
/// chart drew — projected from the same factors <see cref="Charts.Sunburst"/> uses, NOT a second geometry of
/// weights/counts. All radii are absolute (× size already applied). [Story 20.2]</summary>
public sealed record SunburstExplorerMeta(
    int Size,
    double Cx,
    double Pad,
    double Start,
    double EpicInner,
    double EpicOuter,
    double StoryInner,
    double StoryOuter,
    double AggInner,
    double AggOuter);

/// <summary>The whole explorer payload island content: geometry meta + the node hierarchy + edges.
///
/// <para><b><see cref="Edges"/> stays empty, and that is the finished answer — not an unfinished one.</b> Story 20.2
/// reserved this slot for Story 20.3 to fill from <c>SiteGenerator._workGraph</c>. Story 20.1's code review then
/// established (§1a) that the two id spaces are DISJOINT and that most work-graph edge endpoints
/// (<c>d*</c>/<c>a*</c>/<c>src:</c>/<c>res:</c>/<c>retro:</c>) have no wedge at all. Translating the graph into this
/// namespace leaves exactly one joinable shape — <c>Contains</c>, story → epic — which <see cref="SunburstExplorerNode.ParentId"/>
/// already states. So an edge array here would carry no information the payload does not already have, while adding
/// bytes to an embedded payload that grows with project size (the one budget question 20.1 left open). Story 20.3
/// therefore delivers the relationship truth as server-rendered DOM (<see cref="RelatedWorkTemplater"/>) keyed by
/// this same id namespace, and the client joins DOM ↔ selection with no payload lookup at all. Kept in the shape
/// rather than removed: the field is part of the shipped island contract, and an empty array is a smaller,
/// clearer statement than a missing key. [Story 20.2; resolved by Story 20.3]</para></summary>
public sealed record SunburstExplorerModel(
    SunburstExplorerMeta Meta,
    IReadOnlyList<SunburstExplorerNode> Nodes,
    IReadOnlyList<object> Edges);

public static partial class Charts
{
    /// <summary>DOM id / island id of the sunburst-explorer payload island — the one place the class ↔ script
    /// contract is named. [Story 20.2]</summary>
    public const string SunburstExplorerDataId = "sunburst-explorer-data";

    /// <summary>Projects the project-glance sunburst into the Story 20.2 explorer payload: one node per drawn wedge,
    /// each carrying the SAME weight/hierarchy/status/destination the SVG already used — a pure projection over
    /// <see cref="EpicsModel"/> + <see cref="FollowUpGeometry"/> + <see cref="UnplannedWorkGeometry"/> (no
    /// <see cref="ProjectCounts"/> re-count, no second geometry). Ordering mirrors <see cref="Sunburst"/>'s draw
    /// order exactly so the payload can never claim a wedge the chart didn't draw (or omit one it did). Returns an
    /// empty list when there are no epics (the chart shows its empty state). [Story 20.2]</summary>
    /// <param name="expandDenseEpics">Emit a node per STORY even for an epic the static SVG collapses to one
    /// summary wedge (<see cref="StoryDensityCollapseThreshold"/>+ stories).
    /// <para>Default <c>false</c> preserves the Story 20.2 contract exactly: the payload claims precisely the
    /// wedges <see cref="Sunburst"/> drew, which is what
    /// <c>SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew</c> pins.</para>
    /// <para><c>true</c> is for the Story 20.5 Hierarchy Explorer, and the reason it is allowed to differ is that
    /// the collapse is a DRAWING constraint, not a fact about the work: a fixed 380 px static chart cannot fit
    /// eight legible story wedges in one epic's sweep, so it draws "8 stories" instead. The component is larger and
    /// — decisively — it DRILLS, so an epic's own view has the whole sweep to itself. Collapsing there cost the
    /// owner the thing select mode exists for: "when I drill into an epic I can't see the component stories, which
    /// makes it hard to understand what's going on in this epic or select individual stories." Weights are
    /// unaffected either way: the summary wedge's weight is exactly the sum of the per-story weights this emits,
    /// so a parent still equals the sum of its children. [Story 20.5, owner-directed 2026-07-25]</para></param>
    public static IReadOnlyList<SunburstExplorerNode> SunburstExplorerNodes(
        EpicsModel model,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null,
        bool expandDenseEpics = false)
    {
        var epics = model.Epics.OrderBy(e => e.Number).ToList();
        var nodes = new List<SunburstExplorerNode>();
        if (epics.Count == 0) return nodes;

        var geometry = followUps ?? FollowUpGeometry.Empty;
        var unplannedGeo = unplanned ?? UnplannedWorkGeometry.Empty;
        var knownEpics = epics.Select(e => e.Number).ToHashSet();

        // The no-plan average bump the SVG applies (Charts.Sunburst) must be the SAME number here, or the payload
        // weight would disagree with the drawn wedge for every un-drafted story. [owner 2026-07-24]
        var noPlanWeight = SunburstNoPlanStoryWeight(model, geometry);

        // Node ids come from author-controlled markdown (story ids are `### Story N.M:` headings, which nothing
        // dedupes), so a repeated heading would otherwise emit the same id twice — giving the client two payload
        // entries for one logical wedge and double-counting its weight when a ring is re-laid. Keep the FIRST, which
        // matches the draw order the SVG itself used. [Story 20.2 review]
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(SunburstExplorerNode node)
        {
            if (seen.Add(node.Id)) nodes.Add(node);
        }

        foreach (var epic in epics)
        {
            var epicId = $"epic-{epic.Number}";
            var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
            var epicTitle = PathUtil.StripHtmlTags(epic.Title);
            var (openCount, doneCount) = SunburstEpicAggregates(epic, geometry, unplannedGeo);

            Add(new SunburstExplorerNode(
                epicId, null, SunburstEpicWeight(geometry, unplannedGeo, epic, noPlanWeight),
                $"Epic {epic.Number}: {epicTitle}", epicClass, $"epics/epic-{epic.Number}.html", "epic", "epic"));

            var storyWeightSum = epic.Stories.Sum(s => SunburstStoryWeight(geometry, epic.Number, s, noPlanWeight));
            if (storyWeightSum > 0)
            {
                if (epic.Stories.Count >= StoryDensityCollapseThreshold && !expandDenseEpics)
                {
                    // Preserve the server's drawn collapse: a dense epic shows ONE summary wedge, so the payload
                    // carries one summary node (no per-story wedges the static chart never drew). The absence of any
                    // `story`-kind child is exactly what makes the epic non-drillable client-side (it opens instead).
                    Add(new SunburstExplorerNode(
                        $"{epicId}~summary", epicId, storyWeightSum,
                        $"Epic {epic.Number}: {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}",
                        epicClass, $"epics/epic-{epic.Number}.html", "story-summary", "story"));
                }
                else
                {
                    foreach (var story in epic.Stories)
                    {
                        var noPlan = story.TasksTotal == 0;
                        var storyClass = noPlan ? "noplan" : StatusStyles.ForStory(story);
                        var storyHref = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                        Add(new SunburstExplorerNode(
                            story.Id, epicId, SunburstStoryWeight(geometry, epic.Number, story, noPlanWeight),
                            $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}", storyClass, storyHref, "story", "story"));
                    }
                }
            }

            // An EPIC's aggregates are drawn on the aggregate band (Charts.Sunburst passes aggregateInner/Outer).
            var aggregateHref = geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number);
            if (openCount > 0)
                Add(new SunburstExplorerNode(
                    $"{epicId}~open", epicId, openCount,
                    $"Epic {epic.Number}: {openCount} open {Plural(openCount, "follow-up", "follow-ups")}",
                    "followup-open", aggregateHref, "aggregate", "aggregate"));
            if (doneCount > 0)
                Add(new SunburstExplorerNode(
                    $"{epicId}~done", epicId, doneCount,
                    $"Epic {epic.Number}: {doneCount} done {Plural(doneCount, "follow-up", "follow-ups")}",
                    "followup-done", aggregateHref, "aggregate", "aggregate"));
        }

        var unattributed = geometry.OrphanActionItems(knownEpics);
        if (unattributed.Count > 0)
        {
            var (openOrphans, doneOrphans) = SunburstOrphanAggregates(unattributed);
            var orphanClass = openOrphans > 0 ? "followup-open" : "followup-done";
            var orphanHref = geometry.FollowUpsGroupHref;
            // Mirror the SVG's own all-done phrasing (Charts.Sunburst's orphanAria) — this label is user-visible in
            // the explorer breadcrumb, so a drift here reads as two different names for one wedge.
            var orphanLabel = openOrphans > 0
                ? $"Follow-ups: {unattributed.Count} unattributed {Plural(unattributed.Count, "item", "items")}"
                : $"Follow-ups: {unattributed.Count} completed unattributed {Plural(unattributed.Count, "item", "items")}";
            Add(new SunburstExplorerNode(
                "orphan", null, Math.Max(1, unattributed.Count), orphanLabel,
                orphanClass, orphanHref, "follow-up", "epic"));
            // NB the orphan/unplanned aggregates are drawn on the STORY band, not the aggregate band — Charts.Sunburst
            // passes storyInner/storyOuter for these two roots. Hence the explicit Ring. [Story 20.2 review]
            if (openOrphans > 0)
                Add(new SunburstExplorerNode("orphan~open", "orphan", openOrphans,
                    $"Follow-ups: {openOrphans} open unattributed {Plural(openOrphans, "item", "items")}",
                    "followup-open", orphanHref, "aggregate", "story"));
            if (doneOrphans > 0)
                Add(new SunburstExplorerNode("orphan~done", "orphan", doneOrphans,
                    $"Follow-ups: {doneOrphans} done unattributed {Plural(doneOrphans, "item", "items")}",
                    "followup-done", orphanHref, "aggregate", "story"));
        }

        var unplannedSlots = unplannedGeo.SunburstUnplannedWeight;
        if (unplannedSlots > 0)
        {
            var (openUnplanned, doneUnplanned) = SunburstUnplannedAggregates(unplannedGeo);
            var rootClass = openUnplanned > 0 ? "unplanned" : "followup-done";
            var rootHref = unplannedGeo.GroupRootHref ?? "#";
            var rootLabel = openUnplanned > 0
                ? $"Unplanned: {unplannedSlots} direct / one-off {Plural(unplannedSlots, "item", "items")}"
                : $"Unplanned: {unplannedSlots} completed direct / one-off {Plural(unplannedSlots, "item", "items")}";
            Add(new SunburstExplorerNode(
                "unplanned", null, Math.Max(1, unplannedSlots), rootLabel,
                rootClass, rootHref, "unplanned", "epic"));
            if (openUnplanned > 0)
                Add(new SunburstExplorerNode("unplanned~open", "unplanned", openUnplanned,
                    $"Unplanned: {openUnplanned} open {Plural(openUnplanned, "item", "items")}",
                    "unplanned", rootHref, "aggregate", "story"));
            if (doneUnplanned > 0)
                Add(new SunburstExplorerNode("unplanned~done", "unplanned", doneUnplanned,
                    $"Unplanned: {doneUnplanned} done {Plural(doneUnplanned, "item", "items")}",
                    "followup-done", rootHref, "aggregate", "story"));
        }

        return nodes;
    }

    /// <summary>Builds the full explorer payload model (geometry meta + nodes + empty edges) for the given
    /// <paramref name="size"/> (the same size passed to <see cref="Sunburst"/>). [Story 20.2]</summary>
    public static SunburstExplorerModel SunburstExplorerData(
        EpicsModel model, int size = SunburstGlanceSize,
        FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null,
        bool expandDenseEpics = false)
    {
        var meta = new SunburstExplorerMeta(
            size, size / 2.0, SbPad, SbStartAngle,
            size * SbEpicInnerF, size * SbEpicOuterF,
            size * SbStoryInnerF, size * SbStoryOuterF,
            size * SbAggInnerF, size * SbAggOuterF);
        return new SunburstExplorerModel(
            meta, SunburstExplorerNodes(model, followUps, unplanned, expandDenseEpics), Array.Empty<object>());
    }

    /// <summary>The inline JSON island the dashboard mounts beside <see cref="Sunburst"/> — the client drill-in's
    /// only data source (no fetch, <c>file://</c>-safe). Returns "" when there is nothing to explore (no epics), so
    /// the empty-state chart ships no inert island. System.Text.Json's default encoder escapes <c>&lt; &gt; &amp;</c>,
    /// so the payload is safe to embed directly inside a <c>&lt;script&gt;</c>. [Story 20.2]</summary>
    public static string SunburstExplorerIsland(
        EpicsModel model, int size = SunburstGlanceSize,
        FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null)
    {
        if (model.Epics.Count == 0) return string.Empty;
        var data = SunburstExplorerData(model, size, followUps, unplanned);
        var payload = new
        {
            meta = new
            {
                size = data.Meta.Size,
                cx = data.Meta.Cx,
                pad = data.Meta.Pad,
                start = data.Meta.Start,
                epicInner = data.Meta.EpicInner,
                epicOuter = data.Meta.EpicOuter,
                storyInner = data.Meta.StoryInner,
                storyOuter = data.Meta.StoryOuter,
                aggInner = data.Meta.AggInner,
                aggOuter = data.Meta.AggOuter,
            },
            nodes = data.Nodes.Select(n => new
            {
                id = n.Id,
                parentId = n.ParentId,
                weight = n.Weight,
                label = n.Label,
                statusClass = n.StatusClass,
                href = n.Href,
                kind = n.Kind,
                ring = n.Ring,
            }),
            edges = data.Edges,
        };
        var json = JsonSerializer.Serialize(payload);
        return $"<script type=\"application/json\" id=\"{SunburstExplorerDataId}\">{json}</script>\n";
    }
}
