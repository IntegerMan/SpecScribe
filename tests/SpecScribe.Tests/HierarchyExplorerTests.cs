using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 20.5: the Hierarchy Explorer component's emitter — payload, scaffold and text twin.
///
/// <para>The four assertions that matter most are the four blocking data-contract defects the Story 20.4 spike
/// found between the shipped Story 20.2 island and Plotly's hierarchy model. Each of them renders a blank or wrong
/// chart with <b>no error and no console warning</b>, which is precisely why they are pinned here rather than left
/// to a live-browser look: exactly one root, no <c>null</c> in any value, parent value == Σ children, and an
/// emitted <c>branchvalues</c> that matches the shape the payload actually has.</para>
///
/// <para>JS is NOT unit-tested — this codebase is SSR-first and has no JS harness. Everything in the client
/// component is verified in a live browser (Task 8) plus the string guards in <see cref="StylesheetTests"/>.</para></summary>
public class HierarchyExplorerTests
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

    private static HierarchyExplorerConfig Config(
        string domId = "test-hierarchy",
        string shape = "sunburst",
        HierarchyMode mode = HierarchyMode.Select) =>
        new(domId, shape, mode, "sb", 560, true,
            new Charts.ChartMeta("Project at a Glance", Why: Charts.WhyText(Charts.ChartMetric.WorkHierarchy)));

    private static EpicsModel SampleModel() => Model(
        Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5), Story("1.2", "Two", "done", 3, 3)),
        Epic(2, "Beta", Story("2.1", "Three", "done", 4, 4, epicNumber: 2)));

    private static HierarchyExplorerModel Build(EpicsModel model, HierarchyExplorerConfig? config = null) =>
        HierarchyExplorer.ProjectDashboard(model, "SpecScribe", config ?? Config());

    private static JsonElement IslandJson(HierarchyExplorerModel model)
    {
        var html = HierarchyExplorer.IslandHtml(model);
        var m = Regex.Match(html, @"^<script type=""application/json"" class=""ss-hierarchy-data"" id=""(?<id>[^""]+)"">(?<json>.*)</script>\n$", RegexOptions.Singleline);
        Assert.True(m.Success, "The island must be a single application/json script tag carrying the payload.");
        return JsonDocument.Parse(m.Groups["json"].Value).RootElement;
    }

    // ---- Finding A: exactly one root ---------------------------------------------------------------------

    [Fact]
    public void Payload_HasExactlyOneRoot_AndItIsTheSynthesizedProjectNode()
    {
        // Plotly's hierarchy traces refuse a forest outright ("Multiple implied roots, cannot build sunburst
        // hierarchy of trace 0"), and the Story 20.2 payload IS a forest — one root per epic, plus the orphan and
        // unplanned roots. The hand-rolled SVG never noticed because its centre is a decorative circle rather than
        // a data node. The emitter synthesizes the missing root so the payload is valid on its own.
        var built = Build(SampleModel());

        var roots = built.Nodes.Where(n => n.ParentId is null).ToList();

        Assert.Single(roots);
        Assert.Equal(HierarchyExplorer.ProjectRootId, roots[0].Id);
        Assert.Equal(HierarchyExplorer.ProjectRootKind, roots[0].Kind);
        Assert.Equal("SpecScribe", roots[0].Label);
        Assert.Equal(SiteNav.HomeOutputPath, roots[0].Href);
        // Every former root is re-parented onto it — nothing is left dangling.
        Assert.All(built.Nodes.Where(n => n.Id != HierarchyExplorer.ProjectRootId),
            n => Assert.NotNull(n.ParentId));
        Assert.Contains(built.Nodes, n => n.Id == "epic-1" && n.ParentId == HierarchyExplorer.ProjectRootId);
    }

    [Fact]
    public void Payload_EveryParentIdResolvesToANodeInTheSamePayload()
    {
        // A parent id with no matching node is the other way to produce an implied second root.
        var built = Build(SampleModel());
        var ids = built.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(built.Nodes.Where(n => n.ParentId is not null), n => Assert.Contains(n.ParentId!, ids));
    }

    // ---- Finding B: no null in values --------------------------------------------------------------------

    [Fact]
    public void Payload_ContainsNoNullValue_BecauseOneNullRendersNothingSilently()
    {
        // A single null anywhere in Plotly's `values` collapses calcdata to ONE point and renders nothing — no
        // error, no console warning (measured: calcdata 1 -> 119 on changing null to 0). Asserted over the
        // SERIALIZED payload, because that is what the client actually parses.
        var json = IslandJson(Build(SampleModel()));

        foreach (var node in json.GetProperty("nodes").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Number, node.GetProperty("value").ValueKind);
        }
        Assert.DoesNotContain("\"value\":null", json.GetRawText(), StringComparison.Ordinal);
    }

    // ---- Finding C / owner decision D2: children win -----------------------------------------------------

    [Fact]
    public void Payload_EveryParentValueIsExactlyTheSumOfItsChildren()
    {
        // Owner decision D2: a parent's value is the exact sum of its DRAWN children, so the rings can never
        // disagree and a child's angle is comparable across the whole chart. The alternative — the shipped
        // SunburstEpicWeight, which also counts epic-level follow-up PEERS that are not drawn as children — makes
        // `branchvalues: 'total'` invalid and warns per offending parent.
        var built = Build(SampleModel());
        var byParent = built.Nodes
            .Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(n => n.Value), StringComparer.Ordinal);

        foreach (var node in built.Nodes)
        {
            if (byParent.TryGetValue(node.Id, out var childSum))
            {
                Assert.Equal(childSum, node.Value);
            }
        }
        // And the root therefore totals the whole tree rather than sitting at zero.
        Assert.Equal(byParent[HierarchyExplorer.ProjectRootId],
            built.Nodes.Single(n => n.Id == HierarchyExplorer.ProjectRootId).Value);
    }

    [Fact]
    public void Payload_LeafWeightsAreUntouchedByTheParentRollUp()
    {
        // The roll-up may only ever change a PARENT. If it could touch a leaf it would silently rewrite the honest
        // weights — including AC #4's no-plan average bump — that the whole projection exists to carry through.
        var model = SampleModel();
        var source = Charts.SunburstExplorerNodes(model).ToDictionary(n => n.Id, StringComparer.Ordinal);
        var built = Build(model);
        var childIds = built.Nodes.Where(n => n.ParentId is not null).Select(n => n.ParentId!).ToHashSet(StringComparer.Ordinal);

        foreach (var node in built.Nodes.Where(n => !childIds.Contains(n.Id) && n.Id != HierarchyExplorer.ProjectRootId))
        {
            Assert.Equal(source[node.Id].Weight, node.Value);
        }
    }

    [Fact]
    public void Payload_EmittedBranchValues_MatchesTheShapeThePayloadActuallyHas()
    {
        // A payload/branchvalues mismatch is the failure mode that draws a blank or wrong chart with only a console
        // warning, so the two are decided together and travel together. `total` means "a parent's value already
        // INCLUDES its children" — which is exactly what the assertion above proves the payload is.
        var json = IslandJson(Build(SampleModel()));

        Assert.Equal("total", json.GetProperty("config").GetProperty("branchvalues").GetString());
        Assert.Equal("total", HierarchyExplorer.BranchValues);
    }

    // ---- AC #4: the no-plan average bump is PRESERVED, never re-derived ----------------------------------

    [Fact]
    public void AC4_NoPlanStory_KeepsTheAverageBump_AndDraftedStoriesKeepTheirHonestWeight()
    {
        // The component must PRESERVE the owner's 2026-07-24 "bump to average" policy rather than re-derive it:
        // an un-drafted story renders at a typical, clickable size instead of a 1-unit hairline that reads as
        // misleadingly trivial, while every drafted node keeps its honest weight (the floor only lifts).
        // Drafted raw weights {2, 5} -> mean 3.5 -> 4.
        var model = Model(Epic(1, "Alpha",
            Story("1.1", "Two", "in progress", 0, 2),
            Story("1.2", "Five", "in progress", 0, 5),
            Story("1.3", "NoPlan", null, 0, 0)));

        var nodes = Build(model).Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var expected = Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty);

        Assert.Equal(4, expected);
        Assert.Equal(expected, nodes["1.3"].Value);
        Assert.Equal("noplan", nodes["1.3"].StatusClass);
        Assert.Equal(2, nodes["1.1"].Value);   // untouched
        Assert.Equal(5, nodes["1.2"].Value);   // untouched — the bump never shrinks a real wedge
    }

    [Fact]
    public void AC4_NothingDraftedYet_FallsBackToTheHistoricalOneUnitFloor()
    {
        // With no drafted story there is nothing to average against, so the bump degrades to the historical
        // Math.Max(1, …) floor and a brand-new project's chart is unchanged.
        var model = Model(Epic(1, "Alpha", Story("1.1", "NoPlan", null, 0, 0), Story("1.2", "AlsoNoPlan", null, 0, 0)));

        var nodes = Build(model).Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        Assert.Equal(1, Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty));
        Assert.Equal(1, nodes["1.1"].Value);
        Assert.Equal(1, nodes["1.2"].Value);
    }

    // ---- AC #1 anti-drift: the component claims exactly the wedges the SVG drew ---------------------------

    [Fact]
    public void AC1_ComponentNodeIdSet_EqualsTheSvgNodeIdSet_PlusOnlyTheSynthesizedRoot()
    {
        // While the server SVG and the component are BOTH live (owner decision D1), the two must describe the same
        // work. The only permitted difference is the synthesized root, which the SVG draws as a decorative circle.
        // This extends SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew rather than replacing it.
        var model = SampleModel();
        var svgIds = Regex.Matches(Charts.Sunburst(model, nodeIds: true), "data-node-id=\"(?<id>[^\"]+)\"")
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var componentIds = Build(model).Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(HierarchyExplorer.ProjectRootId, componentIds);
        componentIds.Remove(HierarchyExplorer.ProjectRootId);
        Assert.Equal(svgIds, componentIds);
    }

    // ---- Status prose, not CSS classes --------------------------------------------------------------------

    [Fact]
    public void StatusLabel_IsProse_FromTheSameSourceTheLegendAndTileGridUse()
    {
        // The 20.4 probe put the CSS CLASS into accessible names ("— done, weight 44"); UX-DR17/19 want words.
        // Lifecycle stages come from StatusStyles (an epic reads "Stories drafted", a story reads "Drafted"), and
        // the four chart-local classes come from Charts.SunburstLocalStatusLabel — the SAME map the chart's own
        // swatch strip reads, so the chart, the twin and the tile grid cannot disagree.
        Assert.Equal(StatusStyles.EpicLabel("drafted"), HierarchyExplorer.StatusLabelFor("drafted", "epic"));
        Assert.Equal(StatusStyles.StoryLabel("drafted"), HierarchyExplorer.StatusLabelFor("drafted", "story"));
        Assert.NotEqual(HierarchyExplorer.StatusLabelFor("drafted", "epic"), HierarchyExplorer.StatusLabelFor("drafted", "story"));

        Assert.Equal("No task plan", HierarchyExplorer.StatusLabelFor("noplan", "story"));
        Assert.Equal("Open follow-up", HierarchyExplorer.StatusLabelFor("followup-open", "aggregate"));
        Assert.Equal("Done follow-up", HierarchyExplorer.StatusLabelFor("followup-done", "aggregate"));
        Assert.Equal("Direct change", HierarchyExplorer.StatusLabelFor("unplanned", "unplanned"));

        // No node may carry a CSS class where prose belongs.
        var built = Build(SampleModel());
        Assert.All(built.Nodes, n => Assert.NotEqual(n.StatusClass, n.StatusLabel));
        Assert.Equal(HierarchyExplorer.ProjectRootStatusLabel,
            built.Nodes.Single(n => n.Id == HierarchyExplorer.ProjectRootId).StatusLabel);
    }

    [Fact]
    public void ShortLabel_IsTheIdentifierOnly_AndTheFullTitleSurvivesBesideIt()
    {
        // Plotly's uniformtext draws every label at ONE size and hides what will not fit, so a long title
        // suppresses labels chart-wide (measured: 2 of 7 sectors labelled when drilled). The short form is what
        // gets drawn; the full title stays the hover heading, the twin's link text, and the accessible name.
        var built = Build(SampleModel());
        var nodes = built.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        Assert.Equal("Epic 1", nodes["epic-1"].ShortLabel);
        Assert.Equal("Story 1.1", nodes["1.1"].ShortLabel);
        Assert.StartsWith("Epic 1: ", nodes["epic-1"].Label);
        Assert.All(built.Nodes, n => Assert.False(string.IsNullOrWhiteSpace(n.ShortLabel)));
        Assert.All(built.Nodes, n => Assert.True(n.ShortLabel.Length <= n.Label.Length));
    }

    // ---- Island shape --------------------------------------------------------------------------------------

    [Fact]
    public void Island_IsValidJson_CarryingBothConfigAndNodes()
    {
        // ADR 0013 §5: the IR carries chart DATA plus COMPONENT CONFIGURATION. Story 20.6's fingerprint-replacement
        // assertions build on this shape.
        var json = IslandJson(Build(SampleModel(), Config(mode: HierarchyMode.Navigate, shape: "treemap")));
        var cfg = json.GetProperty("config");

        Assert.Equal("test-hierarchy", cfg.GetProperty("domId").GetString());
        Assert.Equal("Project at a Glance", cfg.GetProperty("title").GetString());
        Assert.Equal("treemap", cfg.GetProperty("shape").GetString());
        Assert.Equal("navigate", cfg.GetProperty("mode").GetString());
        Assert.Equal("sb", cfg.GetProperty("hashKey").GetString());
        Assert.Equal(560, cfg.GetProperty("size").GetInt32());
        Assert.True(cfg.GetProperty("labels").GetBoolean());

        var first = json.GetProperty("nodes").EnumerateArray().First();
        foreach (var key in new[] { "id", "parentId", "label", "shortLabel", "value", "statusClass", "statusLabel", "href", "kind" })
        {
            Assert.True(first.TryGetProperty(key, out _), $"payload node is missing '{key}'");
        }
    }

    [Fact]
    public void Island_DoesNotReuseStory202sIslandId_AndTwoInstancesGetDistinctIds()
    {
        // Story 20.2's island is still live and still read by its own JS block until Story 20.7 retires it, so this
        // one must not collide with it — nor with a second instance of this component on the same page.
        var a = HierarchyExplorer.IslandHtml(Build(SampleModel(), Config(domId: "first")));
        var b = HierarchyExplorer.IslandHtml(Build(SampleModel(), Config(domId: "second")));

        Assert.Contains("id=\"first-data\"", a);
        Assert.Contains("id=\"second-data\"", b);
        Assert.DoesNotContain(Charts.SunburstExplorerDataId, a);
        Assert.NotEqual(a, b);
    }

    // ---- The scaffold --------------------------------------------------------------------------------------

    [Fact]
    public void Render_EmitsTheWholeFramedBlock_SoNoCallSiteHandWritesAnyPartOfIt()
    {
        var html = HierarchyExplorer.Render(Build(SampleModel()), "chart-panel sunburst-panel", " data-explorer");

        // Story 10.2 framing: title + framing sentence, from the shared source.
        Assert.Contains("<h3>Project at a Glance</h3>", html);
        Assert.Contains(PathUtil.Html(Charts.WhyText(Charts.ChartMetric.WorkHierarchy)), html);
        // The panel keeps the class the Story 3.5 swatch-hover CSS keys on, and 20.2's opt-in hook.
        Assert.Contains("<div class=\"chart-panel sunburst-panel\" data-explorer>", html);
        // Selector, ordered Sunburst-then-Treemap — the ONE ordering Story 20.7 AC#1 exists to standardize — and
        // shipped [hidden] because switching a trace type requires script.
        var sunburstAt = html.IndexOf("test-hierarchy-shape-sunburst", StringComparison.Ordinal);
        var treemapAt = html.IndexOf("test-hierarchy-shape-treemap", StringComparison.Ordinal);
        Assert.True(sunburstAt > 0 && treemapAt > sunburstAt, "the selector must offer Sunburst before Treemap");
        Assert.Contains("<div class=\"ss-hierarchy-controls\" hidden>", html);
        Assert.Contains("<div class=\"ss-hierarchy-drill\" hidden>", html);
        // Host, live region, island, twin.
        Assert.Contains("<div class=\"ss-hierarchy\" id=\"test-hierarchy\" data-hierarchy></div>", html);
        Assert.Contains("<div class=\"ss-hierarchy-live sr-only\" aria-live=\"polite\"></div>", html);
        Assert.Contains("class=\"ss-hierarchy-data\"", html);
        Assert.Contains("<details class=\"ss-hierarchy-twin\"", html);
        // The component's own class family — 20.7 must be able to delete 20.2's markup and CSS cleanly.
        Assert.DoesNotContain("sb-explorer-", html);
    }

    [Fact]
    public void Render_KeepsTheRetainedServerChartInsideTheSamePanel()
    {
        // Owner decision D1: the server SVG is the LIVE fallback, kept beneath the (hidden) host and hidden only on
        // a successful mount. It must be inside the component's panel, not orphaned outside it.
        var html = HierarchyExplorer.Render(Build(SampleModel()), fallbackHtml: "<svg class=\"sunburst\"></svg>\n");

        var hostAt = html.IndexOf("data-hierarchy>", StringComparison.Ordinal);
        var svgAt = html.IndexOf("<svg class=\"sunburst\">", StringComparison.Ordinal);
        var closeAt = html.LastIndexOf("</div>", StringComparison.Ordinal);

        Assert.True(hostAt > 0 && svgAt > hostAt, "the retained SVG renders after the chart host");
        Assert.True(svgAt < closeAt, "the retained SVG must stay inside the component's panel");
    }

    [Fact]
    public void Render_EmptyModel_ShipsNoIslandNoHostAndNoInertSelector()
    {
        // NFR8: an empty project gets nothing rather than an inert control over an empty chart.
        var built = Build(Model());

        Assert.Empty(built.Nodes);
        Assert.Equal(string.Empty, HierarchyExplorer.Render(built));
        Assert.Equal(string.Empty, HierarchyExplorer.IslandHtml(built));
        Assert.Equal(string.Empty, HierarchyExplorer.TextTwinHtml(built));
    }

    // ---- The text twin (ADR 0013 §2) -----------------------------------------------------------------------

    [Fact]
    public void TextTwin_IsComplete_Navigable_NonColor_AndNestedByParent()
    {
        // ADR 0013 §2 — and the assertion Story 20.6's per-surface audit builds on. Every node in the payload
        // appears in the twin, with a prose status word and a real resolving link.
        var built = Build(SampleModel());
        var twin = HierarchyExplorer.TextTwinHtml(built);

        foreach (var node in built.Nodes)
        {
            Assert.Contains(PathUtil.Html(node.Label), twin);
            Assert.Contains(PathUtil.Html(node.StatusLabel), twin);
            Assert.Contains($"href=\"{PathUtil.Html(node.Href!)}\"", twin);
        }
        Assert.Equal(built.Nodes.Count, Regex.Matches(twin, "<li>").Count);
        // Nested by parentId, so the hierarchy itself is legible without the picture.
        Assert.Contains("<ul class=\"ss-hierarchy-twin-list\">\n<li>", twin);
        Assert.True(Regex.Matches(twin, "<ul class=\"ss-hierarchy-twin-list\">").Count > 1,
            "child levels must nest in their own list, not flatten into one");
        // Weight is stated as a number so the twin conveys what sector SIZE conveys.
        Assert.Contains("weight ", twin);
    }

    [Fact]
    public void TextTwin_SurvivesADuplicateStoryId_WithoutRecursingForever()
    {
        // Story ids come from `### Story N.M:` headings and nothing dedupes them, so a repeated heading is
        // reachable from ordinary authoring input. The projector keeps the first; the twin must simply render.
        var model = Model(Epic(1, "Alpha",
            Story("1.1", "One", "in progress", 1, 2),
            Story("1.1", "One again", "done", 2, 2)));

        var built = Build(model);
        var twin = HierarchyExplorer.TextTwinHtml(built);

        Assert.Equal(built.Nodes.Count, Regex.Matches(twin, "<li>").Count);
    }

    // ---- Asset flag ----------------------------------------------------------------------------------------

    [Fact]
    public void ContainsHost_DrivesTheEngineFlagFromTheRenderedBody()
    {
        // The flag is computed from the page, so it can never claim a 1.2 MB engine a page does not host — the
        // same discipline MermaidNeeded follows.
        Assert.True(HierarchyExplorer.ContainsHost(HierarchyExplorer.Render(Build(SampleModel()))));
        Assert.False(HierarchyExplorer.ContainsHost("<div class=\"chart-panel\"><svg class=\"sunburst\"></svg></div>"));
    }
}
