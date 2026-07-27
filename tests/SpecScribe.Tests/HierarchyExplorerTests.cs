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
        // work. For a model with no dense epic the only permitted difference is the synthesized root, which the SVG
        // draws as a decorative circle rather than a data node.
        var model = SampleModel();
        var svgIds = SvgNodeIds(model);

        var componentIds = Build(model).Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(HierarchyExplorer.ProjectRootId, componentIds);
        componentIds.Remove(HierarchyExplorer.ProjectRootId);
        Assert.Equal(svgIds, componentIds);
    }

    [Fact]
    public void AC1_DenseEpic_TheComponentExpandsWhatTheSvgHadToCollapse()
    {
        // The ONE sanctioned divergence, and it is a divergence in what can be DRAWN rather than in what is true.
        // A fixed 380 px static chart cannot fit eight legible story wedges inside one epic's sweep, so it draws a
        // single "8 stories" summary. The component drills — an epic's own view has the whole sweep — so collapsing
        // there would hide exactly the stories the reader drilled in to find, and make them unselectable, which is
        // what select mode exists for. [owner-directed 2026-07-25]
        var stories = Enumerable.Range(1, Charts.StoryDensityCollapseThreshold)
            .Select(i => Story($"1.{i}", $"Story {i}", "in progress", 1, 2))
            .ToArray();
        var model = Model(Epic(1, "Dense", stories));

        var svgIds = SvgNodeIds(model);
        var componentIds = Build(model).Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        // The SVG collapsed; the component did not.
        Assert.Contains("epic-1~summary", svgIds);
        Assert.DoesNotContain("epic-1~summary", componentIds);
        foreach (var story in stories) Assert.Contains(story.Id, componentIds);

        // And the divergence is EXACTLY that: swap the summary for its stories and the two sets agree again, so a
        // real drift still fails this test rather than hiding behind the exemption.
        var reconciled = componentIds
            .Where(id => id != HierarchyExplorer.ProjectRootId)
            .Where(id => !stories.Any(s => s.Id == id))
            .Append("epic-1~summary")
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(svgIds, reconciled);

        // Weights are untouched by the expansion: the summary wedge's weight is the sum of the stories that
        // replaced it, so a parent still equals the sum of its children (Finding C / D2 holds either way).
        var nodes = Build(model).Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var summaryWeight = Charts.SunburstExplorerNodes(model).Single(n => n.Id == "epic-1~summary").Weight;
        Assert.Equal(summaryWeight, stories.Sum(s => nodes[s.Id].Value));
    }

    /// <summary>The node set the retired SVG drew. It used to be parsed out of <c>Charts.Sunburst</c>'s
    /// <c>data-node-id</c> attributes; Story 20.7 deleted that chart, so it is taken from the SAME shared walk the
    /// SVG built itself from, with <c>expandDenseEpics: false</c> — which is precisely the collapse the SVG applied.
    ///
    /// <para>This is a RETARGET, not a weakening, and it is deliberate: the guard's job is that the component's
    /// payload never claims or omits a node the shared walk does not have, and that job outlives the SVG.
    /// Deleting it because its counterpart went away would remove the anti-drift net at the moment it matters
    /// most. The reader-visible half of the same question is now
    /// <see cref="Twin_EnumeratesExactlyThePayload_SoNeitherCanDriftFromTheOther"/>. [Story 20.7, Open Question 2]</para></summary>
    private static HashSet<string> SvgNodeIds(EpicsModel model) =>
        Charts.SunburstExplorerNodes(model, expandDenseEpics: false)
            .Select(n => n.Id)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Twin_EnumeratesExactlyThePayload_SoNeitherCanDriftFromTheOther()
    {
        // The invariant `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` used to hold, retargeted at the surface that
        // is now the reader-visible one (ADR 0013 §2: the twin is THE no-JS contract). It is not tautological —
        // TextTwinHtml walks by parentId under a depth cap and a cycle guard, either of which can silently drop a
        // node, and a dropped node is exactly the "the chart shows something the listing does not" failure the
        // twin exists to prevent.
        var model = Model(
            Epic(1, "Alpha", Story("1.1", "One", "done", 2, 2), Story("1.2", "Two", "in progress", 1, 3)),
            Epic(2, "Beta", Story("2.1", "Three", "drafted", 0, 0)));

        var built = Build(model);
        var payloadIds = built.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var twinLabels = Regex.Matches(HierarchyExplorer.TextTwinHtml(built), "<li>(?:<a [^>]*>)?(?<label>[^<]+)")
            .Select(m => m.Groups["label"].Value)
            .ToList();

        Assert.Equal(payloadIds.Count, twinLabels.Count);
        // Every payload node's LABEL appears once, so the listing is complete by count and by content.
        foreach (var node in built.Nodes)
            Assert.Contains(twinLabels, l => l == PathUtil.Html(node.Label));
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

    // ---- What a reader is shown instead of the layout number (owner verify round 2026-07-25) ----------------

    [Fact]
    public void Detail_ReplacesTheLayoutNumberWithSomethingAReaderCanUse()
    {
        // "Weight is a confusing value on the tooltip that is not helpful or intuitive for the reader." `Value`
        // stays because Plotly cannot size a sector without it; `Detail` is what a person is ever shown, phrased
        // the way the shipped SVG's own wedge <title> phrases it so the two charts cannot describe one story
        // differently.
        var model = Model(
            Epic(1, "Alpha", Story("1.1", "Planned", "in progress", 3, 8), Story("1.2", "NoPlan", null, 0, 0)),
            Epic(2, "Beta", Story("2.1", "Three", "done", 4, 4, epicNumber: 2)));

        var nodes = Build(model).Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        Assert.Equal("3 of 8 tasks done", nodes["1.1"].Detail);
        // An un-drafted story says so in words. "0 of 0 tasks done" would read as failure rather than as not-yet-planned.
        Assert.Equal("No task plan yet", nodes["1.2"].Detail);
        Assert.Equal("2 stories", nodes["epic-1"].Detail);
        Assert.Equal("1 story", nodes["epic-2"].Detail);
        Assert.Equal("2 epics", nodes[HierarchyExplorer.ProjectRootId].Detail);
    }

    [Fact]
    public void Detail_IsEmptyWhereTheLabelAlreadyCarriesTheCount()
    {
        // An aggregate's own label already reads "Epic 1: 2 open follow-ups", so a Detail would only repeat it.
        var model = SampleModel();
        var items = new[]
        {
            new SprintActionItem("Chase a thing", "open", EpicNumber: 1, Owner: null),
            new SprintActionItem("Another", "open", EpicNumber: 1, Owner: null),
        };
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 0),
        };
        // The ledger is the single source of the open tally and FollowUpGeometry.From asserts the two agree — the
        // chart layer must never recount. Declare the 2 open items rather than passing Empty.
        var counts = ProjectCounts.Empty with { OpenActionItems = 2 };
        var geometry = FollowUpGeometry.From(items, counts, work, epics: model);

        var built = HierarchyExplorer.ProjectDashboard(model, "SpecScribe", Config(), geometry);
        var aggregates = built.Nodes.Where(n => n.Kind == "aggregate").ToList();

        Assert.NotEmpty(aggregates);
        Assert.All(aggregates, n => Assert.Equal(string.Empty, n.Detail));
    }

    [Fact]
    public void RenderedSurfaces_NeverShowTheWordWeightToAReader()
    {
        // The regression guard for the whole point above: neither the twin nor any rendered attribute may put the
        // layout number in front of someone. (The framing sentence's ordinary English use of the word is prose in
        // Charts.WhyText, not a value readout, and is not part of this block.)
        var built = Build(SampleModel());

        Assert.DoesNotContain("weight ", HierarchyExplorer.TextTwinHtml(built));
        Assert.All(built.Nodes, n => Assert.DoesNotContain("weight", n.Detail, StringComparison.OrdinalIgnoreCase));
    }

    // ---- The component's own legend (AC #1) [Story 20.5 review] ---------------------------------------------

    [Fact]
    public void Legend_IsSuppliedByTheComponent_SoStory207sDeletionCannotTakeItAway()
    {
        // AC #1: "it supplies one selector idiom, one Story 10.2 framing block (legend + analysis window + framing
        // sentence) ... so no call site hand-writes any of them." Until the code review the component supplied no
        // legend at all: Charts.Framed has no legend slot, and the only legend on the dashboard came from
        // Charts.SunburstLegend INSIDE Charts.Sunburst — i.e. inside the D1 fallback Story 20.7 deletes. The bug was
        // invisible precisely because D1 kept that fallback on the page.
        //
        // Asserted over Render's OWN output, which after Story 20.7 is the ONLY output — there is no fallback.
        //
        // STORY 20.7 changed the MARKUP FAMILY, and that is load-bearing rather than cosmetic. The component's
        // first legend used its own `.ss-hierarchy-*` classes, which sat outside the pure-CSS drilled-legend
        // selectors — so the dashboard's drilled filtering was in fact still being done by the retained SVG's
        // legend, and would have died silently with it. It now renders through the SAME Charts.SunburstLegend, so
        // `[data-explorer][data-sb-scope] .sunburst-legend .sb-legend-item` keeps matching. [Task 2.2]
        var html = HierarchyExplorer.Render(Build(SampleModel()));

        Assert.Contains("<div class=\"sunburst-legend\">", html);
        Assert.Contains("sb-legend-item", html);
        Assert.DoesNotContain("ss-hierarchy-legend-item", html);
    }

    [Fact]
    public void Legend_ListsOnlyStatusesThePayloadActuallyCarries_AndNamesThemInProse()
    {
        // A legend row pointing at zero sectors is the phantom-entry class Stories 10.7 and 21.1 each closed. The
        // entries are derived from the nodes themselves, and the prose is each node's ALREADY-RESOLVED StatusLabel
        // rather than a second lookup — which is what makes chart, legend, tooltip, accessible name and twin
        // incapable of disagreeing.
        var built = Build(SampleModel());
        var legend = HierarchyExplorer.LegendHtml(built);

        var present = built.Nodes
            .Where(n => n.Id != HierarchyExplorer.ProjectRootId && n.StatusClass.Length > 0)
            .Select(n => n.StatusClass)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(present);
        foreach (var status in present)
        {
            Assert.Contains($"sb-{status}", legend);
            var label = built.Nodes.First(n => n.StatusClass == status && n.Id != HierarchyExplorer.ProjectRootId).StatusLabel;
            Assert.Contains(label, legend);
        }

        // The synthesized root is a SCOPE, not a lifecycle stage — "Whole project" must never appear as a legend
        // entry, and its neutral colour must not be presented as a status.
        Assert.DoesNotContain(HierarchyExplorer.ProjectRootStatusLabel, legend);
    }

    [Fact]
    public void Legend_IsAbsentFromAnEmptyModel()
    {
        // Same NFR8 rule the rest of the scaffold follows: no nodes, no chrome.
        Assert.Equal(string.Empty, HierarchyExplorer.LegendHtml(
            new HierarchyExplorerModel(Build(SampleModel()).Config, Array.Empty<HierarchyNode>())));
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
        // Story 20.2's island (`sunburst-explorer-data`) was retired by Story 20.7, but the id must stay distinct
        // for the reason that outlives it: this story puts up to five instances in one SPA session, and two on one
        // page must not collide on island id, host id, radio names or twin id.
        var a = HierarchyExplorer.IslandHtml(Build(SampleModel(), Config(domId: "first")));
        var b = HierarchyExplorer.IslandHtml(Build(SampleModel(), Config(domId: "second")));

        Assert.Contains("id=\"first-data\"", a);
        Assert.Contains("id=\"second-data\"", b);
        Assert.DoesNotContain("sunburst-explorer-data", a);
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
    public void Render_ShipsNoServerChart_AndTheTwinIsWhatStandsBehindAFailedMount()
    {
        // Story 20.7 retired the SVG that used to ride inside this panel as `fallbackHtml`, and the parameter went
        // with it. This is the replacement assertion, and it is the stronger one: what a JS-off (or failed-mount)
        // visitor gets is the TEXT TWIN, which is complete, navigable and needs no script — where the retained SVG
        // needed specscribe.js to be reachable in order to be drilled at all. Rewritten rather than deleted,
        // because "there is still something here when the chart does not arrive" is the fact worth pinning, and it
        // is the fact ADR 0013 §2 turns into a contract.
        var html = HierarchyExplorer.Render(Build(SampleModel()), "chart-panel sunburst-panel", " data-explorer");

        Assert.DoesNotContain("<svg class=\"sunburst\"", html);
        Assert.DoesNotContain("sb-explorer-", html);

        var hostAt = html.IndexOf("data-hierarchy>", StringComparison.Ordinal);
        var twinAt = html.IndexOf("ss-hierarchy-twin", StringComparison.Ordinal);
        var closeAt = html.LastIndexOf("</div>", StringComparison.Ordinal);

        Assert.True(hostAt > 0 && twinAt > hostAt, "the twin renders after the chart host");
        Assert.True(twinAt < closeAt, "the twin must stay inside the component's panel");
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
        // What a sector's SIZE conveys is stated in words a reader can use — "3 of 8 tasks done", never the raw
        // layout number. The owner's verify round: "weight is a confusing value ... not helpful or intuitive".
        Assert.DoesNotContain("weight ", twin);
        foreach (var node in built.Nodes.Where(n => n.Detail.Length > 0))
        {
            Assert.Contains(PathUtil.Html(node.Detail), twin);
        }
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
    public void Render_ShipsTheBootPlaceholder_ButNoScriptOfItsOwn()
    {
        // The owner's second point: with JS on you saw the server SVG paint and then get replaced by a
        // differently-organized chart. The placeholder is the visible half of the fix and lives in the body; the
        // MARKER that reveals it is emitted on the chrome seam (see BootScript), because the webview and SPA
        // surfaces consume this body directly and must carry no script at all.
        var html = HierarchyExplorer.Render(Build(SampleModel()));

        Assert.Contains("ss-hierarchy-booting", html);
        Assert.Contains("Initializing", html);
        Assert.DoesNotContain("<script>", html);

        // The placeholder must precede the chart host it stands in for.
        var placeholderAt = html.IndexOf("ss-hierarchy-booting", StringComparison.Ordinal);
        var hostAt = html.IndexOf(HierarchyExplorer.HostMarker + ">", StringComparison.Ordinal);
        Assert.True(placeholderAt < hostAt, "the placeholder must precede the chart host it stands in for");
    }

    [Fact]
    public void BootScript_SuppressesTheFlash_AndCannotStrandTheReaderIfTheEngineNeverArrives()
    {
        // ORDER IS THE WHOLE MECHANISM: this runs while the body is still parsing, which is the only moment the
        // server SVG can be suppressed without the reader watching it paint. Nothing deferred can do that.
        Assert.Contains("data-ss-hierarchy-boot", HierarchyExplorer.BootScript);
        // The expiry is what keeps owner decision D1 honest — a blocked bundle must degrade to the server chart,
        // never to a permanent "Initializing…" over a chart that works.
        Assert.Contains("removeAttribute", HierarchyExplorer.BootScript);
        Assert.Contains(HierarchyExplorer.BootTimeoutMs.ToString(), HierarchyExplorer.BootScript);
    }

    [Fact]
    public void ContainsHost_DrivesTheEngineFlagFromTheRenderedBody()
    {
        // The flag is computed from the page, so it can never claim a 1.2 MB engine a page does not host — the
        // same discipline MermaidNeeded follows.
        Assert.True(HierarchyExplorer.ContainsHost(HierarchyExplorer.Render(Build(SampleModel()))));
        Assert.False(HierarchyExplorer.ContainsHost("<div class=\"chart-panel\"><svg class=\"sunburst\"></svg></div>"));
    }

    // =============================================================================================================
    // Story 20.6 — the golden-fingerprint REPLACEMENT assertions (ADR 0013 §6).
    //
    // WHY THEY EXIST. GoldenContentFingerprint currently draws most of its dashboard signal from chart SVG (69.3%
    // of the body, measured by the Story 23.1 spike). When Story 20.7 retires that SVG, the coverage evaporates.
    // ADR 0013 §6 requires the replacement to land BEFORE the first retirement, over the three things that are
    // still server-rendered once the chart is client-drawn: the embedded PAYLOAD, the component CONFIGURATION, and
    // the TEXT TWIN. These run ALONGSIDE the existing fingerprint test through the transition (Task 4.4) — nothing
    // here weakens or replaces it, because SVG is still server-rendered on every surface at this point.
    //
    // ⚠️ HONEST SCOPE LIMIT — do not read these as portfolio-wide chart coverage, which the fingerprint has never
    // had. The golden fixture is NOT a git repo and cites no real repo files, so the git-derived surfaces never
    // render in it: `git-insights.html`, `impact-map.html` and `timeline.html`/`commits/` are all absent from
    // GoldenOutputInventory. (`code-map.html` and `risk-quadrant.html` DO render there — they ride the fixture's
    // repo-root markdown walk, not deep-git. Story 20.6's own task text asserted otherwise; verified against
    // SiteGeneratorAdapterTests' inventory on 2026-07-26 and corrected here.) The fingerprint is therefore the net
    // for the PLANNING hierarchy surfaces; the git surfaces are netted by their own templater tests, and the JS-off
    // behaviour of any surface has no test-suite equivalent at all — it is a live-browser activity, recorded in
    // `_bmad-output/implementation-artifacts/20-6-text-twin-audit.md`.
    //
    // Task 4.3 (folding vendored assets out of FingerprintTree) is NOT re-done here: it already landed with Story
    // 20.5 and was hardened by Story 25.1's review into the shared `KnownStaticAssets` map, which covers plotly AND
    // both prism assets — i.e. already the single predicate Task 4.3 asked for rather than a plotly special case.
    // =============================================================================================================

    [Fact]
    public void Replacement_TheEmbeddedPayload_CarriesEveryFieldTheClientReads()
    {
        // The island is the component's ONLY datasource. If a field silently stops being emitted the chart draws
        // wrong (or blank) with no error — so the shape is pinned, not assumed.
        var built = Build(SampleModel());
        var html = HierarchyExplorer.IslandHtml(built);

        Assert.Contains("<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"test-hierarchy-data\">", html);

        var nodes = IslandJson(built).GetProperty("nodes");
        Assert.Equal(built.Nodes.Count, nodes.GetArrayLength());

        foreach (var node in nodes.EnumerateArray())
        {
            foreach (var field in new[] { "id", "parentId", "label", "value", "statusClass", "statusLabel", "href", "kind" })
            {
                Assert.True(node.TryGetProperty(field, out _), $"payload node is missing '{field}'");
            }
            // `value` must be a NUMBER and never null: one null anywhere in Plotly's values array collapses
            // calcdata to a single point and renders nothing — no error, no console warning (20.4 Finding B).
            Assert.Equal(JsonValueKind.Number, node.GetProperty("value").ValueKind);
            // statusLabel is PROSE, never the CSS class — the twin's and the accessible name's non-color reading.
            Assert.NotEqual(node.GetProperty("statusClass").GetString(), node.GetProperty("statusLabel").GetString());
        }
    }

    [Fact]
    public void Replacement_TheComponentConfiguration_IsEmittedAndMatchesThePayloadsActualShape()
    {
        var built = Build(SampleModel());
        var config = IslandJson(built).GetProperty("config");

        foreach (var field in new[] { "domId", "shape", "mode", "hashKey", "size", "labels", "branchvalues" })
        {
            Assert.True(config.TryGetProperty(field, out _), $"component config is missing '{field}'");
        }
        Assert.Equal("test-hierarchy", config.GetProperty("domId").GetString());
        Assert.Equal("sunburst", config.GetProperty("shape").GetString());
        Assert.Equal("select", config.GetProperty("mode").GetString());
        Assert.Equal("sb", config.GetProperty("hashKey").GetString());
        Assert.Equal(560, config.GetProperty("size").GetInt32());
        Assert.True(config.GetProperty("labels").GetBoolean());

        // Assert against the CONSTANT, never a literal "total" — the constant is the contract both sides read.
        Assert.Equal(HierarchyExplorer.BranchValues, config.GetProperty("branchvalues").GetString());

        // …and the payload must genuinely BE what `branchvalues` claims. A payload/branchvalues mismatch renders a
        // blank or wrong chart with only a console warning, so "parent-inclusive" is verified rather than trusted:
        // under `total`, every parent's value is the exact sum of its emitted children.
        Assert.Equal("total", HierarchyExplorer.BranchValues);
        var byParent = built.Nodes
            .Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.Sum(n => n.Value));
        foreach (var parent in built.Nodes.Where(n => byParent.ContainsKey(n.Id)))
        {
            Assert.Equal(byParent[parent.Id], parent.Value);
        }
        Assert.NotEmpty(byParent);
    }

    [Fact]
    public void Replacement_TheTextTwin_StatesEveryPayloadNodeInProseWithAResolvingLink()
    {
        // The durable half of AC#1: this is what makes the JS-off audit REPEATABLE instead of a one-time
        // inspection. Every node the chart would draw is stated in the twin, as words, with a real link.
        var built = Build(SampleModel());
        var twin = HierarchyExplorer.TextTwinHtml(built);

        Assert.Equal(built.Nodes.Count, Regex.Matches(twin, "<li>").Count);

        foreach (var node in built.Nodes)
        {
            Assert.Contains($"<a href=\"{PathUtil.Html(node.Href!)}\">{PathUtil.Html(node.Label)}</a>", twin);
            // A WORD, not a CSS class. Guarding both directions is the point: `statusClass` leaking into the twin
            // is exactly the defect the 20.4 probe hit, where accessible names read "— done, weight 44".
            Assert.Contains(PathUtil.Html(node.StatusLabel), twin);
        }
        var statusClasses = built.Nodes.Select(n => n.StatusClass).Distinct();
        foreach (var cls in statusClasses)
        {
            Assert.DoesNotContain($"twin-meta\">{PathUtil.Html(cls)}<", twin);
        }
    }

    // ---- Story 20.6 Task 3: the twin-presentation knob (owner D3/D4) ----------------------------------------

    [Fact]
    public void TwinDisplay_DefaultsToDetails_SoAJsOffVisitorReachesItInOneClick()
    {
        // Owner D3. <details> needs no script, which is the whole reason it is the default.
        Assert.Equal(HierarchyTwinDisplay.Details, Config().TwinDisplay);

        var twin = HierarchyExplorer.TextTwinHtml(Build(SampleModel()));
        Assert.StartsWith("<details class=\"ss-hierarchy-twin\" id=\"test-hierarchy-twin\">", twin);
        Assert.Contains("<summary>Project at a Glance — full text listing</summary>", twin);
        Assert.DoesNotContain("sr-only", twin);
        // Closed: availability, not on-screen duplication (ADR 0013 §2).
        Assert.DoesNotContain("<details open", twin);
    }

    [Fact]
    public void TwinDisplay_ScreenReaderOnly_HidesThePresentationAndNothingElse()
    {
        // Owner D4, for surfaces that already carry a visible companion listing (the dashboard's tile grid + the
        // 20.3 rail). The CONTRACT does not vary by surface — only the wrapper does.
        var built = Build(SampleModel(), Config() with { TwinDisplay = HierarchyTwinDisplay.ScreenReaderOnly });
        var twin = HierarchyExplorer.TextTwinHtml(built);

        Assert.StartsWith("<section class=\"ss-hierarchy-twin sr-only\" id=\"test-hierarchy-twin\"", twin);
        // A landmark with an accessible NAME — how a screen-reader user finds the listing without tabbing to it.
        Assert.Contains("aria-labelledby=\"test-hierarchy-twin-title\"", twin);
        Assert.Contains("id=\"test-hierarchy-twin-title\">Project at a Glance — full text listing</h3>", twin);
        Assert.DoesNotContain("<details", twin);

        // `sr-only` is the clip-rect technique, NOT display:none — the links must stay real anchors so they remain
        // focusable and in the accessibility tree. Removing them would break the NAVIGATION half of NFR-5, which
        // ADR 0013 says may never be lost. (Story 20.2's review caught the mirror-image bug live: an SVG <a> at
        // display:none stays focusable. Only a browser shows which you got; this pins the markup half.)
        foreach (var node in built.Nodes)
        {
            Assert.Contains($"<a href=\"{PathUtil.Html(node.Href!)}\">{PathUtil.Html(node.Label)}</a>", twin);
        }
    }

    [Fact]
    public void TwinDisplay_ChangesPresentationOnly_TheListingIsByteIdenticalInBothModes()
    {
        // The load-bearing guarantee behind the whole D3/D4 split: a surface cannot quietly get a LESS complete
        // twin by choosing a different presentation. Strip the wrapper and the two must be identical.
        var details = HierarchyExplorer.TextTwinHtml(Build(SampleModel()));
        var srOnly = HierarchyExplorer.TextTwinHtml(
            Build(SampleModel(), Config() with { TwinDisplay = HierarchyTwinDisplay.ScreenReaderOnly }));

        static string Listing(string twin)
        {
            var start = twin.IndexOf("<ul class=\"ss-hierarchy-twin-list\">", StringComparison.Ordinal);
            var end = twin.LastIndexOf("</ul>", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "both modes must emit the nested listing");
            return twin[start..(end + "</ul>".Length)];
        }

        Assert.Equal(Listing(details), Listing(srOnly));
    }
}
