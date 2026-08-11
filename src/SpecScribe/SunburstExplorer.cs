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

// `SunburstExplorerMeta` (the ring radii the client re-laid drilled arcs against) and `SunburstExplorerModel`
// (meta + nodes + the deliberately-empty edges array) were DELETED by Story 20.7 with the client block that read
// them. Plotly computes its own geometry, so there is no second geometry to keep in step any more.

public static partial class Charts
{
    // `SunburstExplorerDataId` / `SunburstExplorerData` / `SunburstExplorerIsland` — Story 20.2's island and its
    // id — were DELETED by Story 20.7 along with the client block that read them. `SunburstExplorerNodes` below
    // SURVIVES: it is still the single walk of EpicsModel, and HierarchyExplorer.ProjectDashboard is a thin
    // adapter over its output. [Story 20.7 Task 8.3]

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
                $"{epic.DisplayName}: {epicTitle}", epicClass, $"epics/epic-{epic.Number}.html", "epic", "epic"));

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
                        $"{epic.DisplayName}: {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}",
                        epicClass, $"epics/epic-{epic.Number}.html", "story-summary", "story"));
                }
                else
                {
                    foreach (var story in epic.Stories)
                    {
                        var storyClass = StatusStyles.ForStoryDisplay(story);
                        var storyHref = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                        Add(new SunburstExplorerNode(
                            story.Id, epicId, SunburstStoryWeight(geometry, epic.Number, story, noPlanWeight),
                            $"{story.DisplayName}: {PathUtil.StripHtmlTags(story.Title)}", storyClass, storyHref, "story", "story"));
                    }
                }
            }

            // An EPIC's aggregates are drawn on the aggregate band (Charts.Sunburst passes aggregateInner/Outer).
            var aggregateHref = geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number);
            if (openCount > 0)
                Add(new SunburstExplorerNode(
                    $"{epicId}~open", epicId, openCount,
                    $"{epic.DisplayName}: {openCount} open {Plural(openCount, "follow-up", "follow-ups")}",
                    "followup-open", aggregateHref, "aggregate", "aggregate"));
            if (doneCount > 0)
                Add(new SunburstExplorerNode(
                    $"{epicId}~done", epicId, doneCount,
                    $"{epic.DisplayName}: {doneCount} done {Plural(doneCount, "follow-up", "follow-ups")}",
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

}
