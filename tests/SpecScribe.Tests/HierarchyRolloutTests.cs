using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 20.7: the site-wide rollout. Three new projectors, two new component capabilities, and the
/// anti-drift invariant that keeps the rollout from being quietly re-widened.
///
/// <para>Deliberately NOT here: any test of the client behaviour Tasks 1.3, 7.2 and 9.3 add. This project is
/// SSR-first and ships no JS test harness, so the filter, the re-plot and the a11y layer's survival across a
/// filter change are verified in a live browser (Task 11) and their SHIPPED CONTENT is asserted as strings over
/// the built asset (the <c>StylesheetTests</c> pattern). Saying so plainly beats implying coverage that does not
/// exist. [Story 20.7 Task 10.8]</para></summary>
public class HierarchyRolloutTests
{
    // ---- Fixtures ------------------------------------------------------------------------------------------

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

    private static HierarchyExplorerConfig Config(string domId = "t", string shape = "sunburst") => new(
        DomId: domId, Shape: shape, Mode: HierarchyMode.Navigate, HashKey: "sb",
        Size: 380, Labels: true, Meta: new Charts.ChartMeta(Title: "Test chart"));

    private static EpicsModel SampleModel() => Model(
        Epic(1, "Alpha", Story("1.1", "One", "in progress", 2, 5), Story("1.2", "Two", "done", 3, 3)),
        Epic(2, "Beta", Story("2.1", "Three", null, 0, 0, epicNumber: 2)));

    /// <summary>The four Story 20.4 data-contract findings, asserted over any payload. Each one renders a BLANK or
    /// WRONG chart with at most a console warning, which is precisely why they are checked by construction rather
    /// than noticed by looking. [Story 20.7 Task 10.2]</summary>
    private static void AssertPlotlyDataContract(HierarchyExplorerModel model)
    {
        var nodes = model.Nodes;
        Assert.NotEmpty(nodes);

        // Finding A — EXACTLY ONE ROOT. Plotly refuses a forest outright ("Multiple implied roots, cannot build
        // sunburst hierarchy of trace 0") and draws nothing.
        Assert.Single(nodes, n => n.ParentId is null);
        Assert.Equal(HierarchyExplorer.ProjectRootId, nodes.Single(n => n.ParentId is null).Id);

        // Every non-root parent id resolves — a dangling parent is a silently dropped subtree.
        var ids = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var n in nodes.Where(n => n.ParentId is not null))
            Assert.Contains(n.ParentId!, ids);

        // Finding B — no null in `values`. The type makes it unrepresentable (`int`, not `int?`); this pins that
        // the type is still the one doing that work, and that nothing is negative either.
        Assert.All(nodes, n => Assert.True(n.Value >= 0, $"{n.Id} has a negative value"));

        // Finding C / owner D2 — a parent's value is the EXACT SUM of its drawn children (children win).
        var childrenOf = nodes.Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var (parentId, kids) in childrenOf)
        {
            var parent = nodes.First(n => n.Id == parentId);
            Assert.Equal(kids.Sum(k => k.Value), parent.Value);
        }

        // And the emitted branchvalues MATCHES that shape — asserted against the CONSTANT, never a literal
        // "total", because a payload/branchvalues mismatch draws wrong with only a console warning.
        using var doc = JsonDocument.Parse(IslandJson(model));
        Assert.Equal(HierarchyExplorer.BranchValues,
            doc.RootElement.GetProperty("config").GetProperty("branchvalues").GetString());
    }

    private static string IslandJson(HierarchyExplorerModel model)
    {
        var island = HierarchyExplorer.IslandHtml(model);
        return island[(island.IndexOf('>') + 1)..island.LastIndexOf("</script>", StringComparison.Ordinal)];
    }

    // ---- Task 10.2: the four 20.4 invariants, per new projector --------------------------------------------

    [Fact]
    public void ProjectDashboard_SatisfiesThePlotlyDataContract()
    {
        AssertPlotlyDataContract(
            HierarchyExplorer.ProjectDashboard(SampleModel(), "Project", Config()));
    }

    [Fact]
    public void ProjectEpic_SatisfiesThePlotlyDataContract()
    {
        var epic = Epic(1, "Alpha",
            Story("1.1", "One", "in progress", 2, 5),
            Story("1.2", "Two", "done", 3, 3),
            Story("1.3", "NoPlan", null, 0, 0));

        AssertPlotlyDataContract(
            HierarchyExplorer.ProjectEpic(epic, s => $"stories/{s.Id}.html", Config()));
    }

    [Fact]
    public void ProjectStoryTasks_SatisfiesThePlotlyDataContract()
    {
        var tasks = new[]
        {
            new TaskItem("Task one", true, new[] { new TaskItem("Sub A", true, Array.Empty<TaskItem>()), new TaskItem("Sub B", false, Array.Empty<TaskItem>()) }),
            new TaskItem("Task two", false, Array.Empty<TaskItem>()),
        };

        AssertPlotlyDataContract(
            HierarchyExplorer.ProjectStoryTasks("1.1", "Sample", tasks, Config()));
    }

    [Fact]
    public void ProjectImpactMap_SatisfiesThePlotlyDataContract()
    {
        AssertPlotlyDataContract(
            HierarchyExplorer.ProjectImpactMap(ImpactEpics(), ImpactData(), string.Empty, Config(shape: "treemap")));
    }

    // ---- Task 10.3: AC #4 of Story 20.5 survives the conversion --------------------------------------------

    [Fact]
    public void NoPlanStoryWeight_SurvivesTheConversion_OnTheDashboardAndTheEpicsIndex()
    {
        // Epic 20's own AC text calls this out: "Story 20.7's conversion must carry the average-bump forward."
        // The bump lives inside Charts.SunburstNoPlanStoryWeight, which is an INPUT to the projector — this pins
        // that the conversion neither re-floors it nor recomputes it, on BOTH surfaces that draw this datasource.
        // Verified visually as a real sector sweep in Task 11.4; asserted numerically here.
        var model = Model(Epic(1, "Alpha",
            Story("1.1", "Planned", "in progress", 0, 2),
            Story("1.2", "AlsoPlanned", "in progress", 0, 5),
            Story("1.3", "NoPlan", null, 0, 0)));

        var expected = Charts.SunburstNoPlanStoryWeight(model, FollowUpGeometry.Empty);
        Assert.Equal(4, expected); // Round(3.5) away-from-zero — the owner's 2026-07-24 bump, not a 1-unit sliver

        // The dashboard and the epics index share ONE projector (they always shared one datasource), so the same
        // call with a different config is the whole of the difference — which is itself the assertion.
        var dashboard = HierarchyExplorer.ProjectDashboard(model, "Project", Config("dashboard-hierarchy"));
        var epicsIndex = HierarchyExplorer.ProjectDashboard(model, "Project", Config("epics-index-hierarchy"));

        Assert.Equal(expected, dashboard.Nodes.Single(n => n.Id == "1.3").Value);
        Assert.Equal(expected, epicsIndex.Nodes.Single(n => n.Id == "1.3").Value);
        // The drafted stories keep their honest weights — the bump only ever LIFTS a no-plan wedge.
        Assert.Equal(2, dashboard.Nodes.Single(n => n.Id == "1.1").Value);
        Assert.Equal(5, dashboard.Nodes.Single(n => n.Id == "1.2").Value);
    }

    // ---- Task 10.5: the colour generalization changed no resolved colour ------------------------------------

    [Fact]
    public void ColorClass_ForPlanningSurfaces_IsByteIdenticalToWhatTheSvgResolvedAgainst()
    {
        // Task 1.1 replaced the client's `"sb-seg " + STATUS_CLASS[statusClass]` composition with a server-emitted
        // class list. That is only safe if the string it emits is the SAME one the probe used to build, because the
        // probe resolves colours through the live cascade — a different class list is a different colour, silently.
        // ASSERTED, not assumed.
        foreach (var token in new[]
                 {
                     "done", "active", "review", "ready", "drafted", "pending",
                     "noplan", "followup-open", "followup-done", "unplanned", "unrecognized",
                 })
        {
            Assert.Equal($"sb-seg sb-{token}", HierarchyExplorer.PlanningColorClass(token));
        }

        // A token nobody has styled falls back exactly where the client's `|| "sb-unrecognized"` used to.
        Assert.Equal("sb-seg sb-unrecognized", HierarchyExplorer.PlanningColorClass("no-such-status"));

        // And every node in a real payload carries one, so no sector can reach the client with nothing to resolve.
        var model = HierarchyExplorer.ProjectDashboard(SampleModel(), "Project", Config());
        Assert.All(model.Nodes, n => Assert.StartsWith("sb-seg ", n.ColorClass));
    }

    [Fact]
    public void ImpactMap_UsesItsOwnColourFamily_AndTheShippedRampBuckets()
    {
        // The second colour family, which is the whole reason ColorClass exists. Leaves paint through the shipped
        // 5-level commit ramp; structural nodes through the shipped directory fill. No colour VALUE appears here
        // or in the emitter — only the class that resolves one (AD-7).
        var model = HierarchyExplorer.ProjectImpactMap(
            ImpactEpics(), ImpactData(), string.Empty, Config(shape: "treemap"));

        var files = model.Nodes.Where(n => n.Kind == "file").ToList();
        Assert.NotEmpty(files);
        Assert.All(files, n => Assert.Matches(@"^impact-tm-tile impact-level-[1-5]$", n.ColorClass));
        Assert.All(model.Nodes.Where(n => n.Kind is "epic" or "directory" or "project"),
            n => Assert.Equal("impact-arc-dir", n.ColorClass));

        // No `sb-*` class leaks into this family — the two must not be able to blend.
        Assert.DoesNotContain(model.Nodes, n => n.ColorClass.Contains("sb-", StringComparison.Ordinal));

        // The ramp is the SHIPPED arithmetic, moved to generation time: ceil(5k/maxK), clamped 1..5.
        Assert.Equal(1, HierarchyExplorer.ImpactLevel(1, 10));
        Assert.Equal(3, HierarchyExplorer.ImpactLevel(5, 10));
        Assert.Equal(5, HierarchyExplorer.ImpactLevel(10, 10));
        Assert.Equal(5, HierarchyExplorer.ImpactLevel(99, 10));  // clamped, never a 6th level with no colour
        Assert.Equal(1, HierarchyExplorer.ImpactLevel(0, 0));    // no data — never a divide-by-zero
    }

    // ---- Task 10.4: the rollout-completeness invariant ------------------------------------------------------

    [Fact]
    public void NoSourceFileOutsideTheComponent_ConstructsAPlanningHierarchyChart()
    {
        // The anti-drift invariant this epic keeps producing, applied to the rollout itself. ADR 0010 §6 required
        // one shared charting engine as a CONVENTION and it did not hold — three concurrent sessions produced
        // three arc renderers. A convention is easy to defeat; an assertion is not.
        //
        // The ALLOWLIST is the point of the test, not an exemption from it, and Story 20.9 EMPTIED IT. Story 20.7
        // seeded it with the four entry points Code Map and Git Insights ownership still needed; converting those
        // two surfaces deleted all four, and an empty allowlist is now the assertion rather than a placeholder.
        //
        // This is the moment Epic 20 AC#2 — "exactly one implementation of a hierarchy chart exists in the
        // codebase" — stops being aspirational. Three stories in a row had to say "not yet". A future story that
        // WIDENS this list instead of leaving it empty is doing the wrong thing; the component is the route.
        var allowlist = Array.Empty<string>();

        var retired = new[]
        {
            // Story 20.7's three planning entry points…
            "Sunburst", "EpicSunburst", "TaskSunburst",
            // …and Story 20.9's four colorize-driven ones.
            "CodeTreemap", "CodeMapSunburst", "CodeOwnershipSunburst", "CodeOwnershipTreemap",
        };
        var chartsSource = StripComments(File.ReadAllText(SourcePath("Charts.cs")));

        Assert.Empty(allowlist);
        foreach (var name in retired)
        {
            Assert.DoesNotMatch(
                new Regex($@"public static string {name}\s*\("),
                chartsSource);
        }

        // The last hand-rolled ARC GEOMETRY in C# went with them, and that is the concrete form of "exactly one
        // implementation": `BuildSunburstSvg` had exactly two callers, both Story 20.9's, so Charts.cs now sheds
        // its polar geometry completely. Asserted rather than left to a reader to confirm by eye. [Story 20.9 F8]
        Assert.DoesNotContain("BuildSunburstSvg", chartsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AnnularSector", chartsSource, StringComparison.Ordinal);

        // And no source file CALLS a retired entry point by any route. Comments are stripped first: the deletions
        // are explained in prose at the sites they left behind, and a guard that a comment can trip is a guard
        // that gets weakened the first time it fires for the wrong reason.
        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var code = StripComments(File.ReadAllText(file));
            foreach (var name in retired)
                Assert.DoesNotContain($"Charts.{name}(", code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheClientCarriesNoStatusVocabulary_AndNoRetiredRenderer()
    {
        // Task 1.1's other half, asserted over the SHIPPED asset (the StylesheetTests pattern). The `STATUS_CLASS`
        // map was a second copy of the status vocabulary living in JS; removing it is what lets a second colour
        // family exist at all, so its absence is the fact worth pinning rather than the addition.
        var js = File.ReadAllText(Path.Combine(SourceRoot(), "assets", "specscribe.js"));

        Assert.DoesNotContain("var STATUS_CLASS", js);
        Assert.Contains("colorClass", js);

        // Every retired client renderer, gone by name — Story 20.7's three, plus Story 20.9's two, plus the Code
        // Map file-table pager (owner feedback 2026-08-01).
        //
        // The pager was previously pinned as KEPT here, on the reasoning that removing it would strip a control
        // from the one listing a JS-off visitor reads. That reasoning is what INVERTED, not what was overruled:
        // "All files" is now a tree of native <details>, so the listing's navigation control works with JavaScript
        // OFF — strictly better than a pager that only ever worked with it on. The twin decision is untouched (the
        // listing is still HierarchyTwinDisplay.External); only its shape moved.
        foreach (var fn in new[]
                 {
                     "function initSunburstExplorer", "function renderSunburst", "function arcPath",
                     "function initImpactMap", "function initCodeMapPanel", "function initOwnershipSunburst",
                     "function initCodemapTablePager",
                 })
        {
            Assert.DoesNotContain(fn, js);
        }

        // KEPT, and deliberately: the risk quadrant's elevated-risk grid is still a flat list with no structure to
        // disclose, so its pager is still the right control. Separate class family, separate surface.
        Assert.Contains("function initRiskGridPager", js);
    }

    [Fact]
    public void ShippedPrivacyGuards_HoldNowThatFourMoreInstancesBuildAPlotlyConfig()
    {
        // plotly.js 3.7.0's modebar carries a cloud button that UPLOADS the chart to Plotly Cloud, so
        // `displayModeBar:false` is a privacy requirement rather than a cosmetic default — and this story adds four
        // more instances constructing that config. Re-asserted here at rollout scope. [Task 10.7]
        var js = File.ReadAllText(Path.Combine(SourceRoot(), "assets", "specscribe.js"));

        Assert.Contains("displayModeBar: false", js);
        Assert.DoesNotContain("sendDataToCloud", js);
        Assert.DoesNotContain("cdn.plot.ly", js);
        Assert.DoesNotContain("plotly.com", js);
        Assert.Contains("plotlyServerURL: \"\"", js);
        Assert.Contains("topojsonURL: \"\"", js);
    }

    // ---- Task 10.7: the honest empty state, per converted surface -------------------------------------------

    [Fact]
    public void EpicsIndex_WithNoEpics_StillSaysSomething_RatherThanRenderingABareHeading()
    {
        // F1's second casualty, and the one that could ship silently. `Charts.Sunburst` returned an honest
        // "Nothing to chart yet." note for an empty model; the component returns "" (no island, no host, no inert
        // selector — NFR8). On the dashboard and the story page the panel is CONDITIONAL, so "" is right. On the
        // epics index it is NOT conditional, so "" would have turned an honest note into a bare heading.
        var view = EpicsViewBuilder.BuildIndex(
            Model(), ProgressModel.Empty,
            SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: false),
            CommandCatalog.Empty);

        Assert.Equal(string.Empty, view.HierarchyExplorerHtml);

        var body = new HtmlRenderAdapter().RenderEpicsIndexBody(view);
        Assert.Contains("Project at a Glance", body);
        Assert.Contains("Nothing to chart yet.", body);
    }

    [Fact]
    public void StoryDetail_WithNoTasksAndNoDeferred_RendersNoPanelAtAll()
    {
        // The other side of the same rule: this panel IS conditional, so the honest answer is no panel — not an
        // empty frame with a heading over nothing.
        var model = HierarchyExplorer.ProjectStoryTasks(
            "1.1", "Sample", Array.Empty<TaskItem>(), Config());

        Assert.Empty(model.Nodes);
        Assert.Equal(string.Empty, HierarchyExplorer.Render(model));
    }

    [Fact]
    public void EpicDetail_WithNoStoriesAndNoFollowUps_RendersNoPanelAtAll()
    {
        var model = HierarchyExplorer.ProjectEpic(
            Epic(1, "Empty"), s => $"stories/{s.Id}.html", Config());

        Assert.Empty(model.Nodes);
        Assert.Equal(string.Empty, HierarchyExplorer.Render(model));
    }

    // ---- The projectors' own facts ---------------------------------------------------------------------------

    [Fact]
    public void ProjectEpic_CoversWhatTheEpicSvgDrew_StoriesDeferredAndTheFollowUpAggregates()
    {
        // Task 5.2: the node set must cover what EpicSunburst actually drew, not just the stories — the epic-level
        // follow-up aggregates and the story-child deferred items were both real wedges with real destinations.
        var story = Story("1.1", "One", "in progress", 2, 4);
        var epic = Epic(1, "Alpha", story);
        var deferred = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Park the thing</p>", Resolved: false, ResolvingRef: null, ResolvingHref: null),
            "Story 1.1", EpicNumber: 1, DetailHref: "follow-ups/d1.html", SourceStoryId: "1.1");
        var geometry = new FollowUpGeometry(
            new[] { new SprintActionItem("Chase the vendor", "open", EpicNumber: 1, Owner: null) },
            DeferredOpenCount: 0, DeferredHref: null,
            ActionItemsHref: SiteNav.ActionItemsOutputPath,
            DeferredSlots: new[] { deferred });

        var model = HierarchyExplorer.ProjectEpic(epic, s => $"stories/{s.Id}.html", Config(), geometry);
        var byId = model.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        Assert.Contains("1.1", byId.Keys);
        Assert.Contains("epic~open", byId.Keys);                       // the aggregate ring the SVG drew
        Assert.Contains(byId.Keys, k => k.StartsWith("1.1~deferred", StringComparison.Ordinal));
        Assert.Equal("stories/1.1.html", byId["1.1"].Href);            // the lifted Story 9.13 destination
        Assert.Equal("follow-ups/d1.html", byId.Values.First(n => n.Kind == "follow-up" && n.ParentId == "1.1").Href);

        // The task-bulk child exists precisely so the story's value stays `tasks + deferred` under a parent-
        // inclusive roll-up rather than collapsing to its deferred COUNT — more work must not draw less ink.
        Assert.Equal(4, byId["1.1~tasks"].Value);
        Assert.Equal(5, byId["1.1"].Value);
        AssertPlotlyDataContract(model);
    }

    [Fact]
    public void ProjectStoryTasks_UsesTheTaskVocabulary_NotTheStoryLifecycleOne()
    {
        // Task 6.2. A task is done or it is not; it is never "Ready for dev", and an undone one is not "Pending" —
        // the shipped SVG's own legend said "Not done". StatusStyles.StoryLabel would have answered "Pending",
        // which is a lifecycle STAGE word applied to something with no lifecycle.
        var tasks = new[]
        {
            new TaskItem("Wire the thing", false, Array.Empty<TaskItem>()),
            new TaskItem("Ship the thing", true, Array.Empty<TaskItem>()),
        };
        var model = HierarchyExplorer.ProjectStoryTasks("1.1", "Sample", tasks, Config());
        var labels = model.Nodes.Where(n => n.Kind == "task").Select(n => n.StatusLabel).ToList();

        Assert.Contains("Not done", labels);
        Assert.Contains("Done", labels);
        Assert.DoesNotContain("Pending", labels);

        // And the shared lifecycle vocabulary is UNCHANGED by that — the fix deliberately did not go into
        // SunburstLocalStatusLabel, which every other surface consults.
        Assert.Equal("Pending", StatusStyles.StoryLabel("pending"));
        Assert.Null(Charts.SunburstLocalStatusLabel("pending"));
    }

    [Fact]
    public void ProjectImpactMap_GroupsByEpic_AndSaysSoWhereAReaderSees()
    {
        // Owner decision D4's visible product change: a file touched by several epics now draws under each, so the
        // root total is ATTRIBUTED churn rather than distinct-file churn. The number changing silently is the
        // failure mode; the framing note is the fix, so the note is asserted with the shape.
        var model = HierarchyExplorer.ProjectImpactMap(
            ImpactEpics(), ImpactData(), string.Empty, Config(shape: "treemap"));

        // `src/Shared.cs` is attributed to BOTH epics and therefore appears twice, once under each.
        var shared = model.Nodes.Where(n => n.Kind == "file" && n.Label == "src/Shared.cs").ToList();
        Assert.Equal(2, shared.Count);
        Assert.NotEqual(shared[0].ParentId, shared[1].ParentId);

        // Epic → directory → file, three real levels under the synthesized root.
        Assert.Contains(model.Nodes, n => n.Kind == "epic");
        Assert.Contains(model.Nodes, n => n.Kind == "directory");

        // The counting basis is stated in prose the reader meets, not only in a story file.
        Assert.Contains("attributed churn", ImpactMapTemplater.ImpactAttributionNote);
        Assert.Contains("appears under each", ImpactMapTemplater.ImpactAttributionNote);
    }

    [Fact]
    public void EveryConvertedInstance_HasADistinctDomIdAndHashKey()
    {
        // Up to five instances now exist and the SPA can hold two pages in one session, so a collision on host id,
        // island id, radio name, twin id or hash key is a real failure mode rather than a theoretical one.
        var domIds = new[]
        {
            DashboardViewBuilder.DashboardHierarchyDomId,
            EpicsViewBuilder.EpicsIndexHierarchyDomId,
            ImpactMapTemplater.HierarchyDomId,
            "epic-1-hierarchy",
            "story-1-1-hierarchy",
        };
        Assert.Equal(domIds.Length, domIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheLegend_RendersThroughTheSharedRenderer_SoTheDrilledLegendCssStillMatches()
    {
        // Task 2.2, and it is the half of the legend's behaviour that SURVIVES the SVG's retirement. The pure-CSS
        // drilled filtering is `[data-explorer][data-sb-scope] .sunburst-legend .sb-legend-item { display:none }`
        // plus one `data-tok-*` re-show per status, and it only works if this legend IS that markup.
        var html = HierarchyExplorer.Render(
            HierarchyExplorer.ProjectDashboard(SampleModel(), "Project", Config()),
            "chart-panel sunburst-panel", " data-explorer");

        Assert.Contains("<div class=\"sunburst-legend\">", html);
        Assert.Contains("class=\"sb-legend-item sb-done-item\"", html);
        Assert.Contains("class=\"swatch sb-done-sw\"", html);
        Assert.Contains("tabindex=\"0\"", html);

        // Membership is the PAYLOAD's, so a legend row can never point at zero sectors — the phantom-entry defect
        // Stories 10.7 and 21.1 each had to close. Nothing here is `unplanned`, so no such swatch is drawn.
        Assert.DoesNotContain("sb-unplanned-item", html);
    }

    // ---- Impact-map fixtures --------------------------------------------------------------------------------

    private static EpicsModel ImpactEpics() => Model(
        Epic(1, "Alpha", Story("1.1", "One", "done", 1, 1)),
        Epic(2, "Beta", Story("2.1", "Two", "done", 1, 1, epicNumber: 2)));

    private static PlanningCodeImpactData ImpactData() => new(
        new Dictionary<int, IReadOnlyList<ImpactFile>>
        {
            [1] = new[]
            {
                new ImpactFile("src/Shared.cs", "code/src-shared-cs.html", 120, 8),
                new ImpactFile("src/OnlyAlpha.cs", "code/src-onlyalpha-cs.html", 30, 2),
                new ImpactFile("README.md", null, 5, 1),
            },
            [2] = new[]
            {
                new ImpactFile("src/Shared.cs", "code/src-shared-cs.html", 60, 4),
            },
        },
        new Dictionary<string, IReadOnlyList<ImpactFile>>(),
        AttributedCommitCount: 12,
        TotalAnalyzedCommits: 20);

    /// <summary>Line comments and block comments removed, so a rollout guard tests CODE. Crude on purpose — it
    /// does not need to understand string literals containing "//", because no source file in this project puts a
    /// retired entry point's call syntax inside one.</summary>
    private static string StripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join("\n", source
            .Split('\n')
            .Select(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : l));
    }

    private static string SourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !Directory.Exists(Path.Combine(dir, "src", "SpecScribe")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(string.IsNullOrEmpty(dir), "could not locate the repository root from the test bin directory");
        return Path.Combine(dir!, "src", "SpecScribe");
    }

    private static string SourcePath(string fileName) => Path.Combine(SourceRoot(), fileName);
}
