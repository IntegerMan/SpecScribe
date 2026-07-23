using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 20.2: the sunburst-explorer payload projector. The load-bearing guarantee is that the payload
/// claims EXACTLY the wedges the static <see cref="Charts.Sunburst"/> SVG drew — no invented nodes, no dropped
/// ones, and every weight is the SAME number the SVG sized its wedge by (never a re-count). These tests pin that
/// projection across the dense-epic-collapse / no-plan / multi-epic branches, plus the JSON island shape.</summary>
public class SunburstExplorerTests
{
    private static StoryInfo Story(string id, string title, string? status, int done, int total, int epicNumber = 1) => new()
    {
        Id = id,
        EpicNumber = epicNumber,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
        TasksDone = done,
        TasksTotal = total,
    };

    private static EpicInfo Epic(int number, string title, params StoryInfo[] stories) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    private static EpicsModel Model(params EpicInfo[] epics) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = epics,
    };

    // The set of data-node-id values the SVG actually stamped onto its wedges.
    private static HashSet<string> SvgNodeIds(string svg) =>
        Regex.Matches(svg, "data-node-id=\"(?<id>[^\"]+)\"")
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Projector_NodeSet_EqualsTheWedgesTheSvgDrew()
    {
        // The anti-drift invariant (AC #1): the payload can neither claim a wedge the chart didn't draw nor omit one
        // it did. Both are projected from the SAME model, so their id sets must be identical.
        var model = Model(
            Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5), Story("1.2", "Two", "done", 3, 3)),
            Epic(2, "Beta", Story("2.1", "Three", null, 0, 0)));

        var svg = Charts.Sunburst(model);
        var svgIds = SvgNodeIds(svg);
        var payloadIds = Charts.SunburstExplorerNodes(model).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(svgIds, payloadIds);
    }

    [Fact]
    public void Projector_EpicAndStoryWeights_MatchTheSharedWeightFunctions()
    {
        // Weights are the SAME numbers the SVG sizes its wedges by — the shared Charts.SunburstEpicWeight /
        // SunburstStoryWeight (extracted per Story 20.1's contract), never a parallel re-count.
        var s1 = Story("1.1", "One", "in progress", 2, 5);
        var s2 = Story("1.2", "Two", "done", 3, 3);
        var model = Model(Epic(1, "Alpha", s1, s2));

        var nodes = Charts.SunburstExplorerNodes(model).ToDictionary(n => n.Id);

        Assert.Equal(Charts.SunburstEpicWeight(FollowUpGeometry.Empty, UnplannedWorkGeometry.Empty, model.Epics[0]), nodes["epic-1"].Weight);
        Assert.Equal(Charts.SunburstStoryWeight(FollowUpGeometry.Empty, 1, s1), nodes["1.1"].Weight);
        Assert.Equal(Charts.SunburstStoryWeight(FollowUpGeometry.Empty, 1, s2), nodes["1.2"].Weight);
        // Parent/kind wiring the client drill reads.
        Assert.Null(nodes["epic-1"].ParentId);
        Assert.Equal("epic", nodes["epic-1"].Kind);
        Assert.Equal("epic-1", nodes["1.1"].ParentId);
        Assert.Equal("story", nodes["1.1"].Kind);
        Assert.Equal("epics/epic-1.html", nodes["epic-1"].Href);
    }

    [Fact]
    public void Projector_DenseEpic_CollapsesToOneSummaryNodeAndIsNotDrillable()
    {
        // A dense epic (>= StoryDensityCollapseThreshold stories) draws ONE summary wedge, so the payload carries a
        // single story-summary node and NO per-story `story` nodes — which is exactly what leaves it non-drillable
        // client-side (preserving the server's collapse rather than inventing wedges the static chart hid).
        var stories = Enumerable.Range(1, Charts.StoryDensityCollapseThreshold)
            .Select(i => Story($"1.{i}", $"Story {i}", "in progress", 1, 2))
            .ToArray();
        var model = Model(Epic(1, "Dense", stories));

        var nodes = Charts.SunburstExplorerNodes(model);
        var svgIds = SvgNodeIds(Charts.Sunburst(model));

        Assert.Contains(nodes, n => n.Id == "epic-1~summary" && n.Kind == "story-summary" && n.ParentId == "epic-1");
        Assert.DoesNotContain(nodes, n => n.Kind == "story"); // no per-story wedges drawn → none in the payload
        Assert.Equal(svgIds, nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void Projector_NoPlanStory_IsStillADrillableStoryNode()
    {
        // A zero-task "no plan yet" story is still drawn as its own (min-weight) story wedge — so it stays a `story`
        // node and keeps its epic drillable.
        var model = Model(Epic(1, "Alpha", Story("1.1", "Planned", "in progress", 1, 4), Story("1.2", "NoPlan", null, 0, 0)));

        var nodes = Charts.SunburstExplorerNodes(model).ToDictionary(n => n.Id);

        Assert.Equal("story", nodes["1.2"].Kind);
        Assert.Equal("noplan", nodes["1.2"].StatusClass);
        Assert.Equal(1, nodes["1.2"].Weight); // Math.Max(1, 0 tasks) — same floor the SVG uses
    }

    [Fact]
    public void Projector_EmptyModel_YieldsNoNodesAndNoIsland()
    {
        var empty = Model();
        Assert.Empty(Charts.SunburstExplorerNodes(empty));
        Assert.Equal(string.Empty, Charts.SunburstExplorerIsland(empty));
    }

    [Fact]
    public void Island_IsWellFormedJson_WithMetaNodesAndEmptyEdges()
    {
        var model = Model(Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5)));

        var island = Charts.SunburstExplorerIsland(model, size: 380);
        Assert.StartsWith("<script type=\"application/json\" id=\"sunburst-explorer-data\">", island);
        Assert.EndsWith("</script>\n", island);

        var json = island[(island.IndexOf('>') + 1)..island.LastIndexOf("</script>", StringComparison.Ordinal)];
        using var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement;

        // Geometry meta drives the client re-layout onto the same rings.
        Assert.Equal(380, rootEl.GetProperty("meta").GetProperty("size").GetInt32());
        Assert.True(rootEl.GetProperty("meta").TryGetProperty("epicInner", out _));
        // Story 20.2 ships edges empty (Story 20.3 fills them).
        Assert.Equal(0, rootEl.GetProperty("edges").GetArrayLength());
        // The first node is the epic, carrying the canonical id + camelCase fields the client reads.
        var first = rootEl.GetProperty("nodes")[0];
        Assert.Equal("epic-1", first.GetProperty("id").GetString());
        Assert.Equal("epic", first.GetProperty("kind").GetString());
        Assert.True(first.GetProperty("weight").GetInt32() >= 1);
    }
}
