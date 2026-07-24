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

        var svg = Charts.Sunburst(model, nodeIds: true);
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
        var svgIds = SvgNodeIds(Charts.Sunburst(model, nodeIds: true));

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
    public void WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks()
    {
        // The webview ships no specscribe.js, so the island is unreadable weight there — dropped, and registered as
        // the `data-island` host exception. What must NOT be dropped is the chart itself. [Story 20.2 review]
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[] { ("Home", null) });
        var page = new PageView
        {
            Kind = PageKind.Home,
            Title = "Dashboard",
            OutputRelativePath = "index.html",
            Nav = nav.ToNavigationView("index.html"),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = ForgeOptions.StylesheetName,
                ScriptHref = ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = Array.Empty<string>(),
            },
            BodyHtml = "<main id=\"main-content\"><div class=\"chart-panel\" data-explorer>"
                + "<svg class=\"sunburst\"><a href=\"epics/epic-1.html\"><path class=\"sb-seg\" data-node-id=\"epic-1\"></path></a></svg>"
                + "<script type=\"application/json\" id=\"sunburst-explorer-data\">{\"nodes\":[]}</script>\n"
                + "</div></main>",
        };

        var rendered = new WebviewRenderAdapter().RenderContent(page);

        Assert.DoesNotContain("application/json", rendered);
        Assert.DoesNotContain("sunburst-explorer-data", rendered);
        Assert.Contains("<svg class=\"sunburst\"", rendered);
        Assert.Contains("epics/epic-1.html", rendered);
        Assert.Contains(HostRenderExceptions.Registry, e => e.SurfaceId == "webview" && e.FactId == "data-island");
    }

    [Fact]
    public void Projector_EmptyModel_YieldsNoNodesAndNoIsland()
    {
        var empty = Model();
        Assert.Empty(Charts.SunburstExplorerNodes(empty));
        Assert.Equal(string.Empty, Charts.SunburstExplorerIsland(empty));
    }

    /// <summary>A geometry pair with BOTH an unattributed ("orphan") follow-up root and per-epic follow-up
    /// aggregates — the branches the bare-model tests never reach. `EpicNumber: 99` is not in the model, so it lands
    /// in the orphan slice exactly as an unknown epic number does in production. [Story 20.2 review]</summary>
    private static (FollowUpGeometry FollowUps, UnplannedWorkGeometry Unplanned) GeometryWithOrphansAndAggregates(EpicsModel model)
    {
        var items = new[]
        {
            new SprintActionItem("Epic-attributed follow-up", "open", EpicNumber: 1, Owner: null),
            new SprintActionItem("Epic-attributed, done", "done", EpicNumber: 1, Owner: null),
            new SprintActionItem("Unknown epic debt", "open", EpicNumber: 99, Owner: null),
            new SprintActionItem("Unscoped cleanup", "done", EpicNumber: null, Owner: null),
        };
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 0),
        };
        // The counts ledger is the single source of the open tally (FollowUpGeometry.From asserts the two agree —
        // the chart layer must never recount), so declare the 2 open items above rather than passing Empty.
        var counts = ProjectCounts.Empty with { OpenActionItems = 2 };
        var followUps = FollowUpGeometry.From(items, counts, work, epics: model);
        return (followUps, UnplannedWorkGeometry.From(work, followUps, model));
    }

    [Fact]
    public void Projector_NodeSet_MatchesTheSvg_WithFollowUpsAndUnplannedPresent()
    {
        // The anti-drift invariant is only worth anything if it runs over the branches that can actually drift.
        // Every other test passes the model bare, so the epic open/done aggregate ring, the orphan root and its own
        // aggregate ring — the hand-written arithmetic — were entirely unexercised. [Story 20.2 review]
        var model = Model(
            Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5), Story("1.2", "Two", "done", 3, 3)),
            Epic(2, "Beta", Story("2.1", "Three", null, 0, 0, epicNumber: 2)));
        var (followUps, unplanned) = GeometryWithOrphansAndAggregates(model);

        var svgIds = SvgNodeIds(Charts.Sunburst(model, followUps: followUps, unplanned: unplanned, nodeIds: true));
        var nodes = Charts.SunburstExplorerNodes(model, followUps, unplanned);

        Assert.Equal(svgIds, nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
        Assert.Contains(nodes, n => n.Id == "orphan");           // the branch the bare-model tests never reached
        Assert.Contains(nodes, n => n.Id.EndsWith("~open", StringComparison.Ordinal));
    }

    [Fact]
    public void Projector_OrphanAndUnplannedAggregates_DeclareTheStoryRing_NotTheAggregateRing()
    {
        // Charts.Sunburst draws an EPIC's open/done aggregates on the aggregate band but the orphan/unplanned roots'
        // aggregates on the STORY band. `Kind` is identical for both, so the client cannot infer the ring from it —
        // `Ring` is the fact that keeps a drilled re-layout on the radii the server actually used.
        var model = Model(Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5)));
        var (followUps, unplanned) = GeometryWithOrphansAndAggregates(model);

        var nodes = Charts.SunburstExplorerNodes(model, followUps, unplanned).ToDictionary(n => n.Id);

        Assert.Equal("aggregate", nodes["orphan~open"].Kind);
        Assert.Equal("story", nodes["orphan~open"].Ring);
        Assert.Equal("epic", nodes["orphan"].Ring);
        if (nodes.TryGetValue("epic-1~open", out var epicOpen))
        {
            Assert.Equal("aggregate", epicOpen.Kind);
            Assert.Equal("aggregate", epicOpen.Ring); // an epic's own aggregates DO sit on the aggregate band
        }
    }

    [Theory]
    [InlineData(7)]   // one below the collapse threshold — per-story wedges, epic stays drillable
    [InlineData(8)]   // exactly at it — collapses
    [InlineData(9)]   // one above
    public void Projector_NodeSet_MatchesTheSvg_AcrossTheCollapseBoundary(int storyCount)
    {
        // The gate is `>= StoryDensityCollapseThreshold`; testing only the exact threshold leaves an off-by-one in
        // either direction free to ship. [Story 20.2 review]
        var stories = Enumerable.Range(1, storyCount)
            .Select(i => Story($"1.{i}", $"Story {i}", "in progress", 1, 2))
            .ToArray();
        var model = Model(Epic(1, "Boundary", stories));

        var svgIds = SvgNodeIds(Charts.Sunburst(model, nodeIds: true));
        var nodes = Charts.SunburstExplorerNodes(model);

        Assert.Equal(svgIds, nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
        var collapsed = storyCount >= Charts.StoryDensityCollapseThreshold;
        Assert.Equal(collapsed, nodes.Any(n => n.Kind == "story-summary"));
        Assert.Equal(!collapsed, nodes.Any(n => n.Kind == "story"));
    }

    [Fact]
    public void Projector_EpicWithNoStories_ClaimsNoWedgeTheChartDidNotDraw()
    {
        var model = Model(Epic(1, "Empty"));

        var svgIds = SvgNodeIds(Charts.Sunburst(model, nodeIds: true));
        var nodes = Charts.SunburstExplorerNodes(model);

        Assert.Equal(svgIds, nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
        Assert.DoesNotContain(nodes, n => n.Kind == "story" || n.Kind == "story-summary");
    }

    [Fact]
    public void Projector_DuplicateStoryIds_EmitOneNode()
    {
        // Story ids come from `### Story N.M:` headings and nothing dedupes them, so a repeated heading yields two
        // wedges sharing one data-node-id. The payload must still describe ONE logical node, or a drilled ring
        // double-counts its weight. [Story 20.2 review]
        var model = Model(Epic(1, "Dupes",
            Story("1.1", "First", "in progress", 1, 2),
            Story("1.1", "Same id again", "done", 2, 2)));

        var nodes = Charts.SunburstExplorerNodes(model);

        Assert.Single(nodes, n => n.Id == "1.1");
        Assert.Equal(nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count(), nodes.Count);
    }

    [Fact]
    public void Sunburst_OmitsNodeIdHooks_UnlessTheSurfaceOptsIn()
    {
        // Only the surface that also mounts the explorer island needs the join hooks; the epics index renders the
        // same chart with no explorer and should not carry ~2.5 KB of attributes nothing reads. [Story 20.2 review]
        var model = Model(Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5)));

        Assert.DoesNotContain("data-node-id", Charts.Sunburst(model));
        Assert.Contains("data-node-id", Charts.Sunburst(model, nodeIds: true));
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
        // `ring` is a separate fact from `kind` — the client reads it to place a drilled wedge on the right band.
        Assert.Equal("epic", first.GetProperty("ring").GetString());
        Assert.True(first.GetProperty("weight").GetInt32() >= 1);
    }
}
