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

    /// <summary>The node set a READER can actually reach — parsed out of the text twin the Hierarchy Explorer
    /// renders from this walk's output.
    ///
    /// <para>It used to be the <c>data-node-id</c> values <c>Charts.Sunburst</c> stamped onto its wedges. Story
    /// 20.7 retired that chart, so the invariant below is retargeted rather than deleted: its job was always that
    /// the payload can neither CLAIM something the reader cannot see nor OMIT something the reader can, and under
    /// ADR 0013 §2 the twin is now that reader-visible surface. Deleting the guard because its counterpart went
    /// away would have removed the anti-drift net at exactly the moment it matters most. [Story 20.7, Open
    /// Question 2]</para></summary>
    private static HashSet<string> TwinNodeIds(
        EpicsModel model, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null)
    {
        var config = new HierarchyExplorerConfig(
            DomId: "t", Shape: "sunburst", Mode: HierarchyMode.Navigate, HashKey: "sb",
            Size: 380, Labels: true, Meta: new Charts.ChartMeta(Title: "Project at a Glance"));
        // `expandDenseEpics: false` — the collapse the SVG applied, so the comparison is like-for-like with what
        // these tests were originally written against.
        var nodes = HierarchyExplorer.Reparent(
            Charts.SunburstExplorerNodes(model, followUps, unplanned, expandDenseEpics: false),
            "Project", "index.html");
        var twin = HierarchyExplorer.TextTwinHtml(new HierarchyExplorerModel(config, nodes));
        var labels = Regex.Matches(twin, "<li>(?:<a [^>]*>)?(?<label>[^<]+)")
            .Select(m => m.Groups["label"].Value)
            .ToHashSet(StringComparer.Ordinal);
        // Map the twin's labels back to ids through the payload, so a node the twin silently dropped is missing here.
        return nodes
            .Where(n => n.Id != HierarchyExplorer.ProjectRootId && labels.Contains(PathUtil.Html(n.Label)))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Projector_NodeSet_EqualsTheWedgesTheSvgDrew()
    {
        // The anti-drift invariant (AC #1): the payload can neither claim a wedge the chart didn't draw nor omit one
        // it did. Both are projected from the SAME model, so their id sets must be identical.
        var model = Model(
            Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5), Story("1.2", "Two", "done", 3, 3)),
            Epic(2, "Beta", Story("2.1", "Three", null, 0, 0)));

        var reachableIds = TwinNodeIds(model);
        var payloadIds = Charts.SunburstExplorerNodes(model).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(reachableIds, payloadIds);
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

        Assert.Contains(nodes, n => n.Id == "epic-1~summary" && n.Kind == "story-summary" && n.ParentId == "epic-1");
        Assert.DoesNotContain(nodes, n => n.Kind == "story"); // no per-story wedges drawn → none in the payload
        Assert.Equal(TwinNodeIds(model), nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void Projector_NoPlanStory_IsStillADrillableStoryNode()
    {
        // A zero-task "no plan yet" story is still drawn as its own story wedge — so it stays a `story` node and keeps
        // its epic drillable — but it is now sized to the AVERAGE drafted-story weight instead of a 1-unit sliver, so
        // un-drafted work doesn't read as misleadingly trivial. Here the one drafted story has raw weight 4 (4 tasks),
        // so the no-plan story is bumped to 4 — the SAME number the SVG sizes its wedge by. [owner 2026-07-24]
        var model = Model(Epic(1, "Alpha", Story("1.1", "Planned", "in progress", 1, 4), Story("1.2", "NoPlan", null, 0, 0)));

        var nodes = Charts.SunburstExplorerNodes(model).ToDictionary(n => n.Id);

        Assert.Equal("story", nodes["1.2"].Kind);
        Assert.Equal("noplan", nodes["1.2"].StatusClass);
        Assert.Equal(4, nodes["1.2"].Weight); // bumped to the average drafted-story weight (4), not a 1-unit sliver
        Assert.Equal(Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty), nodes["1.2"].Weight);
    }

    [Fact]
    public void NoPlanStoryWeight_IsTheRoundedMeanOfDraftedStories_AndOnlyLiftsTheFloor()
    {
        // The bump is the rounded MEAN raw weight of the drafted stories, and it only LIFTS a no-plan wedge — it never
        // shrinks a real one. Drafted weights {2, 5} → mean 3.5 → 4; the no-plan story takes 4 while the drafted
        // stories keep their honest 2 and 5. [owner 2026-07-24 "bump to average"]
        var drafted2 = Story("1.1", "Two", "in progress", 0, 2);
        var drafted5 = Story("1.2", "Five", "in progress", 0, 5);
        var noPlan = Story("1.3", "NoPlan", null, 0, 0);
        var model = Model(Epic(1, "Alpha", drafted2, drafted5, noPlan));

        Assert.Equal(4, Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty)); // Round(3.5) away-from-zero
        Assert.Equal(2, Charts.SunburstStoryWeight(FollowUpGeometry.Empty, 1, drafted2, noPlanWeight: 4)); // honest, unlifted
        Assert.Equal(5, Charts.SunburstStoryWeight(FollowUpGeometry.Empty, 1, drafted5, noPlanWeight: 4)); // honest, unlifted
        Assert.Equal(4, Charts.SunburstStoryWeight(FollowUpGeometry.Empty, 1, noPlan, noPlanWeight: 4)); // lifted to the mean
    }

    [Fact]
    public void NoPlanStoryWeight_FallsBackToOne_WhenNothingIsDraftedYet()
    {
        // A brand-new project where nothing has a plan yet has no drafted weights to average — the bump falls back to
        // the historical 1-unit floor, so the glance is byte-identical to the pre-bump behavior in that case.
        var model = Model(Epic(1, "Alpha", Story("1.1", "A", null, 0, 0), Story("1.2", "B", null, 0, 0)));

        Assert.Equal(1, Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty));
        var nodes = Charts.SunburstExplorerNodes(model).ToDictionary(n => n.Id);
        Assert.Equal(1, nodes["1.1"].Weight);
        Assert.Equal(1, nodes["1.2"].Weight);
    }

    [Fact]
    public void WebviewAdapter_StripsTheIsland_ButKeepsTheTwinAndItsLinks()
    {
        // The webview ships no specscribe.js, so the island is unreadable weight there — dropped, and registered as
        // the `data-island` host exception. What must NOT be dropped is whatever carries the INFORMATION.
        //
        // Story 20.7 rewrote this test rather than deleting it, and the rewrite is the whole point. It used to
        // assert the webview keeps "the chart and its links", meaning the server-rendered SVG. That SVG is gone, so
        // the original assertion is no longer true — but the QUESTION it was asking is more important now, not
        // less: owner decision D3 accepts that this surface shows no chart picture, and the only thing that makes
        // that a documented DEGRADATION rather than a hole is the text twin surviving the strip with its links
        // intact. The strip is a regex over <script type="application/json">; the twin is <details>/<ul>/<a>
        // markup, so it should survive — this confirms it rather than assuming it. [Story 20.7 Task 9.2/9.5]
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
            // The real shape a converted surface ships: the component's island, and its text twin.
            BodyHtml = "<main id=\"main-content\"><div class=\"chart-panel sunburst-panel\" data-explorer>"
                + "<div class=\"ss-hierarchy\" id=\"dashboard-hierarchy\" data-hierarchy></div>\n"
                + "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"dashboard-hierarchy-data\">{\"nodes\":[]}</script>\n"
                + "<details class=\"ss-hierarchy-twin\" id=\"dashboard-hierarchy-twin\">\n<summary>Full text listing</summary>\n"
                + "<ul class=\"ss-hierarchy-twin-list\">\n<li><a href=\"epics/epic-1.html\">Epic 1: Alpha</a> "
                + "<span class=\"ss-hierarchy-twin-meta\">Stories drafted</span></li>\n</ul>\n</details>\n"
                + "</div></main>",
        };

        var rendered = new WebviewRenderAdapter().RenderContent(page);

        // The island goes.
        Assert.DoesNotContain("application/json", rendered);
        Assert.DoesNotContain("ss-hierarchy-data", rendered);

        // The twin stays, COMPLETE and NAVIGABLE — the two halves ADR 0013 §2 actually requires. A twin whose
        // links were rewritten away would be a hole dressed as a degradation.
        Assert.Contains("ss-hierarchy-twin", rendered);
        Assert.Contains("Epic 1: Alpha", rendered);
        Assert.Contains("epics/epic-1.html", rendered);
        Assert.Contains("Stories drafted", rendered);

        // And no chart picture is claimed here — that absence is itself registered, not silent.
        Assert.DoesNotContain("<svg class=\"sunburst\"", rendered);
        Assert.Contains(HostRenderExceptions.Registry, e => e.SurfaceId == "webview" && e.FactId == "data-island");
        Assert.Contains(HostRenderExceptions.Registry, e => e.SurfaceId == "webview" && e.FactId == "hierarchy-chart");
    }

    [Fact]
    public void Projector_EmptyModel_YieldsNoNodesAndNoIsland()
    {
        var empty = Model();
        Assert.Empty(Charts.SunburstExplorerNodes(empty));
        // The island half moved with the island: Story 20.7 deleted Charts.SunburstExplorerIsland, and the
        // component's own IslandHtml already returns "" for an empty model (HierarchyExplorerTests).
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

        var nodes = Charts.SunburstExplorerNodes(model, followUps, unplanned);

        Assert.Equal(TwinNodeIds(model, followUps, unplanned), nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
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

        var nodes = Charts.SunburstExplorerNodes(model);

        Assert.Equal(TwinNodeIds(model), nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
        var collapsed = storyCount >= Charts.StoryDensityCollapseThreshold;
        Assert.Equal(collapsed, nodes.Any(n => n.Kind == "story-summary"));
        Assert.Equal(!collapsed, nodes.Any(n => n.Kind == "story"));
    }

    [Fact]
    public void Projector_EpicWithNoStories_ClaimsNoWedgeTheChartDidNotDraw()
    {
        var model = Model(Epic(1, "Empty"));

        var nodes = Charts.SunburstExplorerNodes(model);

        Assert.Equal(TwinNodeIds(model), nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));
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

    // `Sunburst_OmitsNodeIdHooks_UnlessTheSurfaceOptsIn` was DELETED by Story 20.7, not rewritten. It asserted a
    // GEOMETRY/attribute fact about `Charts.Sunburst` — whether an SVG carried `data-node-id` join hooks — and both
    // the chart and the client block that joined against those hooks are gone. There is no payload or twin
    // statement it could be retargeted at, because the fact it pinned no longer exists. [Story 20.7 Task 10.1]

    // `Island_IsWellFormedJson_WithMetaNodesAndEmptyEdges` was DELETED by Story 20.7 along with
    // `Charts.SunburstExplorerIsland` / `SunburstExplorerData` / `SunburstExplorerMeta`. It asserted the SHAPE of
    // 20.2's island — the ring radii the client re-laid drilled arcs against, and the deliberately-empty edges
    // array. Plotly computes its own geometry, so there is no second geometry left to pin. The equivalent
    // assertions for the component's island (well-formed JSON, camelCase fields, the emitted branchvalues) live
    // in HierarchyExplorerTests and are not duplicated here. [Story 20.7 Task 10.1]
}
