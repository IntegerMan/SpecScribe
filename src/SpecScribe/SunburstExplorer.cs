using System.Text.Json;

namespace SpecScribe;

/// <summary>One node of the Story 20.2 sunburst-explorer payload: the projection of a single drawn wedge onto the
/// data the client drill-in reads. <see cref="Id"/> is the canonical identity the wedge's <c>data-node-id</c> also
/// carries (so the client joins DOM ↔ payload with no ambiguity, and Story 20.3's edges join by the same grain);
/// <see cref="Weight"/> is the SAME number <see cref="Charts.Sunburst"/> used to size the wedge (never a re-count —
/// see <see cref="Charts.SunburstEpicWeight"/>/<see cref="Charts.SunburstStoryWeight"/>); <see cref="Kind"/> drives
/// the ring the wedge lives on and the zoom-vs-open rule (a wedge with <c>story</c> children drills, a leaf opens
/// its <see cref="Href"/> — the Story 9.13 destination already on the wedge's <c>&lt;a&gt;</c>). [Story 20.2]</summary>
public sealed record SunburstExplorerNode(
    string Id,
    string? ParentId,
    int Weight,
    string Label,
    string StatusClass,
    string? Href,
    string Kind);

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

/// <summary>The whole explorer payload island content: geometry meta + the node hierarchy + edges. Story 20.2 ships
/// <see cref="Edges"/> empty; Story 20.3 fills it from <c>SiteGenerator._workGraph</c> (joined by <see cref="SunburstExplorerNode.Id"/>).
/// [Story 20.2]</summary>
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
    public static IReadOnlyList<SunburstExplorerNode> SunburstExplorerNodes(
        EpicsModel model,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var epics = model.Epics.OrderBy(e => e.Number).ToList();
        var nodes = new List<SunburstExplorerNode>();
        if (epics.Count == 0) return nodes;

        var geometry = followUps ?? FollowUpGeometry.Empty;
        var unplannedGeo = unplanned ?? UnplannedWorkGeometry.Empty;
        var knownEpics = epics.Select(e => e.Number).ToHashSet();

        foreach (var epic in epics)
        {
            var epicId = $"epic-{epic.Number}";
            var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
            var epicTitle = PathUtil.StripHtmlTags(epic.Title);
            var (openCount, doneCount) = CountEpicFollowUpAggregates(epic, geometry, unplannedGeo);

            nodes.Add(new SunburstExplorerNode(
                epicId, null, SunburstEpicWeight(geometry, unplannedGeo, epic),
                $"Epic {epic.Number}: {epicTitle}", epicClass, $"epics/epic-{epic.Number}.html", "epic"));

            var storyWeightSum = epic.Stories.Sum(s => SunburstStoryWeight(geometry, epic.Number, s));
            if (storyWeightSum > 0)
            {
                if (epic.Stories.Count >= StoryDensityCollapseThreshold)
                {
                    // Preserve the server's drawn collapse: a dense epic shows ONE summary wedge, so the payload
                    // carries one summary node (no per-story wedges the static chart never drew). The absence of any
                    // `story`-kind child is exactly what makes the epic non-drillable client-side (it opens instead).
                    nodes.Add(new SunburstExplorerNode(
                        $"{epicId}~summary", epicId, storyWeightSum,
                        $"Epic {epic.Number}: {epic.Stories.Count} {Plural(epic.Stories.Count, "story", "stories")}",
                        epicClass, $"epics/epic-{epic.Number}.html", "story-summary"));
                }
                else
                {
                    foreach (var story in epic.Stories)
                    {
                        var noPlan = story.TasksTotal == 0;
                        var storyClass = noPlan ? "noplan" : StatusStyles.ForStory(story);
                        var storyHref = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                        nodes.Add(new SunburstExplorerNode(
                            story.Id, epicId, SunburstStoryWeight(geometry, epic.Number, story),
                            $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}", storyClass, storyHref, "story"));
                    }
                }
            }

            var aggregateHref = geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number);
            if (openCount > 0)
                nodes.Add(new SunburstExplorerNode(
                    $"{epicId}~open", epicId, openCount,
                    $"Epic {epic.Number}: {openCount} open {Plural(openCount, "follow-up", "follow-ups")}",
                    "followup-open", aggregateHref, "aggregate"));
            if (doneCount > 0)
                nodes.Add(new SunburstExplorerNode(
                    $"{epicId}~done", epicId, doneCount,
                    $"Epic {epic.Number}: {doneCount} done {Plural(doneCount, "follow-up", "follow-ups")}",
                    "followup-done", aggregateHref, "aggregate"));
        }

        var unattributed = geometry.OrphanActionItems(knownEpics);
        if (unattributed.Count > 0)
        {
            var openOrphans = unattributed.Count(a => !FollowUpGeometry.IsDone(a));
            var doneOrphans = unattributed.Count - openOrphans;
            var orphanClass = openOrphans > 0 ? "followup-open" : "followup-done";
            var orphanHref = geometry.FollowUpsGroupHref;
            nodes.Add(new SunburstExplorerNode(
                "orphan", null, Math.Max(1, unattributed.Count),
                $"Follow-ups: {unattributed.Count} unattributed {Plural(unattributed.Count, "item", "items")}",
                orphanClass, orphanHref, "follow-up"));
            if (openOrphans > 0)
                nodes.Add(new SunburstExplorerNode("orphan~open", "orphan", openOrphans,
                    $"Follow-ups: {openOrphans} open unattributed {Plural(openOrphans, "item", "items")}",
                    "followup-open", orphanHref, "aggregate"));
            if (doneOrphans > 0)
                nodes.Add(new SunburstExplorerNode("orphan~done", "orphan", doneOrphans,
                    $"Follow-ups: {doneOrphans} done unattributed {Plural(doneOrphans, "item", "items")}",
                    "followup-done", orphanHref, "aggregate"));
        }

        var unplannedSlots = unplannedGeo.SunburstUnplannedWeight;
        if (unplannedSlots > 0)
        {
            var openUnplanned = unplannedGeo.UnplannedQuickDev.Count(q => UnplannedWorkGeometry.IsOpenQuickDev(q.Entry.Status))
                + unplannedGeo.UnattributableDeferred.Count(s => !s.Item.Resolved);
            var doneUnplanned = Math.Max(0, unplannedSlots - openUnplanned);
            var rootClass = openUnplanned > 0 ? "unplanned" : "followup-done";
            var rootHref = unplannedGeo.GroupRootHref ?? "#";
            nodes.Add(new SunburstExplorerNode(
                "unplanned", null, Math.Max(1, unplannedSlots),
                $"Unplanned: {unplannedSlots} direct / one-off {Plural(unplannedSlots, "item", "items")}",
                rootClass, rootHref, "unplanned"));
            if (openUnplanned > 0)
                nodes.Add(new SunburstExplorerNode("unplanned~open", "unplanned", openUnplanned,
                    $"Unplanned: {openUnplanned} open {Plural(openUnplanned, "item", "items")}",
                    "unplanned", rootHref, "aggregate"));
            if (doneUnplanned > 0)
                nodes.Add(new SunburstExplorerNode("unplanned~done", "unplanned", doneUnplanned,
                    $"Unplanned: {doneUnplanned} done {Plural(doneUnplanned, "item", "items")}",
                    "followup-done", rootHref, "aggregate"));
        }

        return nodes;
    }

    /// <summary>Builds the full explorer payload model (geometry meta + nodes + empty edges) for the given
    /// <paramref name="size"/> (the same size passed to <see cref="Sunburst"/>). [Story 20.2]</summary>
    public static SunburstExplorerModel SunburstExplorerData(
        EpicsModel model, int size = 380,
        FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null)
    {
        var meta = new SunburstExplorerMeta(
            size, size / 2.0, SbPad, SbStartAngle,
            size * SbEpicInnerF, size * SbEpicOuterF,
            size * SbStoryInnerF, size * SbStoryOuterF,
            size * SbAggInnerF, size * SbAggOuterF);
        return new SunburstExplorerModel(meta, SunburstExplorerNodes(model, followUps, unplanned), Array.Empty<object>());
    }

    /// <summary>The inline JSON island the dashboard mounts beside <see cref="Sunburst"/> — the client drill-in's
    /// only data source (no fetch, <c>file://</c>-safe). Returns "" when there is nothing to explore (no epics), so
    /// the empty-state chart ships no inert island. System.Text.Json's default encoder escapes <c>&lt; &gt; &amp;</c>,
    /// so the payload is safe to embed directly inside a <c>&lt;script&gt;</c>. [Story 20.2]</summary>
    public static string SunburstExplorerIsland(
        EpicsModel model, int size = 380,
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
            }),
            edges = data.Edges,
        };
        var json = JsonSerializer.Serialize(payload);
        return $"<script type=\"application/json\" id=\"{SunburstExplorerDataId}\">{json}</script>\n";
    }
}
