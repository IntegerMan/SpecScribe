using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Accessibility-name coverage for the SVG charts (Story 1.4 AC #1): every drillable segment link
/// carries an aria-label so its hover-only &lt;title&gt; is reachable without a pointer, and the whole-chart
/// donut/heatmap SVGs expose a role="img" name. Colour/legend text redundancy (status is never colour-only)
/// is guarded too so it can't silently regress.</summary>
public class ChartsTests
{
    // ---- Story 20.7: the three hierarchy entry points these tests used to render ---------------------------
    //
    // `Charts.Sunburst` / `EpicSunburst` / `TaskSunburst` were deleted. The tests below were NOT deleted with
    // them, because most of them assert FACTS — this epic appears, this story links there, this aggregate reads
    // "1 open / 0 done", this legend has no swatch for a status nothing draws — and a fact does not stop being
    // worth pinning because the engine that drew it changed. They now render the SAME model through the Hierarchy
    // Explorer and assert against the block it produces: the LEGEND (byte-identical markup — the component renders
    // through the same `Charts.SunburstLegend`), the ISLAND (which carries every node's label, value, status and
    // `colorClass`), and the TEXT TWIN (which carries every node's label, prose status and real resolving link).
    //
    // What WAS deleted is the geometry half: assertions about `viewBox`, `<path d="…">`, annular-sector arithmetic
    // and the centre `<text>` counts. Those described how a hand-rolled SVG placed ink, and nothing places ink in
    // C# any more. The split is reported in the story's Completion Notes.

    /// <summary>The project-glance hierarchy, rendered through the component — the replacement for
    /// <c>Charts.Sunburst(model, followUps, unplanned)</c>.</summary>
    private static string Glance(
        EpicsModel model, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null) =>
        HierarchyExplorer.Render(HierarchyExplorer.ProjectDashboard(
            model, "Project", HierarchyConfig("glance", "Project at a Glance"), followUps, unplanned));

    /// <summary>One epic's hierarchy, rendered through the component — the replacement for
    /// <c>Charts.EpicSunburst(epic, hrefBuilder, …)</c>.</summary>
    private static string EpicGlance(
        EpicInfo epic, Func<StoryInfo, string>? hrefBuilder = null,
        FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null) =>
        HierarchyExplorer.Render(HierarchyExplorer.ProjectEpic(
            epic,
            hrefBuilder ?? (s => s.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(s.Id)),
            HierarchyConfig($"epic-{epic.Number}", "Story Breakdown"), followUps, unplanned));

    /// <summary>One story's task hierarchy, rendered through the component — the replacement for
    /// <c>Charts.TaskSunburst(tasks, deferred)</c>.</summary>
    private static string TaskGlance(
        IReadOnlyList<TaskItem> tasks, IReadOnlyList<FollowUpDeferredSlot>? deferred = null) =>
        HierarchyExplorer.Render(HierarchyExplorer.ProjectStoryTasks(
            "1.1", "Sample", tasks, HierarchyConfig("story", "Task Breakdown"), deferred));

    private static HierarchyExplorerConfig HierarchyConfig(string domId, string title) => new(
        DomId: domId, Shape: "sunburst", Mode: HierarchyMode.Navigate, HashKey: "sb",
        Size: 380, Labels: true, Meta: new Charts.ChartMeta(Title: title));

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

    private static EpicInfo Epic(params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "First Epic",
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    [Fact]
    public void StatCard_WithTooltip_UsesBodyLevelJsTipPath()
    {
        var linked = Charts.StatCard("3", "Epics drafted", tooltip: "Epics with stories", href: "epics.html");
        Assert.Contains("class=\"stat-card stat-card-link js-tip\"", linked);
        Assert.Contains("data-tip=\"Epics with stories\"", linked);
        Assert.Contains("title=\"Epics with stories\"", linked);
        Assert.DoesNotContain("data-tooltip=", linked);

        var staticCard = Charts.StatCard("—", "Commits", tooltip: "no git history");
        Assert.Contains("class=\"stat-card js-tip\"", staticCard);
        Assert.Contains("data-tip=\"no git history\"", staticCard);
        Assert.Contains("tabindex=\"0\"", staticCard);
        Assert.DoesNotContain("data-tooltip=", staticCard);
    }

    [Fact]
    public void Sunburst_SegmentLinksCarryDescriptiveLabels()
    {
        var story = Story("1.1", "Do the thing", "in progress", done: 2, total: 5);
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(story) },
        };

        var svg = Glance(model);

        // Epic + story nodes carry a descriptive label (keyboard/SR name), reachable via the payload/twin — there
        // is no SVG <title> tooltip any more (Story 20.7 retired the last hand-rolled SVG renderer; Plotly's own
        // hover reads the payload, not a static attribute this test can assert against).
        Assert.Contains("Epic 1: First Epic", svg);
        // Story now includes task count in the aria-label (no separate task ring).
        Assert.Contains("Story 1.1: Do the thing", svg);
        // Legend text keeps status shape+label, not colour alone (UX-DR17). The wording is the payload's own
        // prose status, so it can never disagree with the sector, the tooltip, the accessible name or the twin —
        // and only statuses actually drawn get a row (this model has one in-progress story).
        Assert.Contains("In development</span>", svg);
        Assert.DoesNotContain("Pending</span>", svg);
    }

    [Fact]
    public void Sunburst_AllDoneEpicReadsAsInReviewUntilRetroExists()
    {
        // An epic whose every story is done but has no parsed retrospective is retro-gated to the "review"
        // (deep-teal) tier in the sunburst's inner ring — delivered, retro pending — rather than green "done".
        EpicsModel Model(bool hasRetro)
        {
            var epic = Epic(Story("1.1", "Do the thing", "done", done: 3, total: 3));
            epic.HasRetrospective = hasRetro;
            return new EpicsModel
            {
                OverviewHtml = string.Empty,
                RequirementsInventoryHtml = string.Empty,
                Epics = new[] { epic },
            };
        }

        var noRetro = Glance(Model(hasRetro: false));
        // The epic (inner-ring) segment carries the review class + label. (The task ring has its own sb-done arc
        // for the finished tasks, so the epic segment's aria-label is the unambiguous signal to assert on.)
        Assert.Contains("\"colorClass\":\"sb-seg sb-review\"", noRetro);
        Assert.Contains("Epic 1: First Epic", noRetro);

        var withRetro = Glance(Model(hasRetro: true));
        // Once a retro exists the epic segment is green "done" again. (Assert on the epic aria-label, not a bare
        // "In review" — the legend always lists an "In review" swatch regardless of the data.)
        Assert.Contains("Epic 1: First Epic", withRetro);
        Assert.DoesNotContain("Epic 1: First Epic — In review", withRetro);
    }

    [Fact]
    public void Sunburst_LegendEntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // The interactive-legend emphasis (Story 3.5 Task 3) is pure CSS, but it needs each legend entry to
        // carry a status class the :has() rule can target AND a tabindex so keyboard users reach it. Guard
        // both so the CSS affordance can't be silently unwired at the markup end.
        var story = Story("1.1", "Do the thing", "in progress", done: 2, total: 5);
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(story) },
        };

        var svg = Glance(model);

        // The markup contract is UNCHANGED — the component renders through the same Charts.SunburstLegend, and
        // it has to: the pure-CSS drilled-legend filtering keys on `.sunburst-legend .sb-<status>-item`, and that
        // is the half of the legend's behaviour that survives the SVG's retirement.
        Assert.Contains("<span class=\"sb-legend-item sb-active-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"swatch sb-active-sw\"></span>In development</span>", svg);

        // What DID change, and it is an improvement worth pinning: MEMBERSHIP is the payload's, not a fixed
        // six-status roster. This model has one in-progress story, so there is no `review` and no `done` sector —
        // and therefore no swatch for either. A legend row pointing at zero wedges is the phantom-entry defect
        // Stories 10.7 and 21.1 each had to close; here it is unrepresentable. [Story 20.7 Task 2.1]
        Assert.DoesNotContain("sb-review-item", svg);
        Assert.DoesNotContain("sb-done-item", svg);
    }

    [Fact]
    public void EpicSunburst_LegendEntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // Review follow-up: the epic-level Sunburst test above only covers ONE of the three SunburstLegend
        // call sites. This pins the story-level overload (EpicSunburst), which uses the same 6-tuple set.
        var epic = Epic(Story("1.1", "A story", "in progress", done: 2, total: 5));

        var svg = EpicGlance(epic, _ => "epics/epic-1.html");

        // Same markup contract from the epic-scoped projector — one legend renderer, three call sites, and now
        // three PROJECTORS feeding it rather than three hand-written legends.
        Assert.Contains("<span class=\"sb-legend-item sb-active-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"swatch sb-active-sw\"></span>In development</span>", svg);
        Assert.DoesNotContain("sb-review-item", svg); // payload-derived membership — see the glance test above
    }

    [Fact]
    public void TaskSunburst_LegendEntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // Review follow-up: pins the third SunburstLegend call site (TaskSunburst), which uses the distinct
        // 2-item "Not done"/"Done" set rather than the six lifecycle statuses.
        // Both statuses are present in the fixture so both swatches are drawn — under payload-derived
        // membership a one-task chart would honestly show only the status it has.
        var tasks = new List<TaskItem>
        {
            new("Do the thing", Done: true, Subtasks: Array.Empty<TaskItem>()),
            new("Do the other thing", Done: false, Subtasks: Array.Empty<TaskItem>()),
        };

        var svg = TaskGlance(tasks);

        Assert.Contains("<span class=\"sb-legend-item sb-pending-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"sb-legend-item sb-done-item\" tabindex=\"0\">", svg);
        // This legend's pending label reads "Not done", not the shared "Pending" text (status is never
        // colour-only, and the wording matches this chart's own not-done/done framing).
        Assert.Contains("<span class=\"swatch sb-pending-sw\"></span>Not done</span>", svg);
        Assert.Contains("<span class=\"swatch sb-done-sw\"></span>Done</span>", svg);
    }

    [Fact]
    public void TaskSunburst_DeferredFromStory_OuterRingWithLinks()
    {
        var tasks = new List<TaskItem>
        {
            new("Ship it", Done: true, Subtasks: Array.Empty<TaskItem>()),
            new("Polish copy", Done: false, Subtasks: Array.Empty<TaskItem>()),
        };
        var deferred = new[]
        {
            new FollowUpDeferredSlot(
                new DeferredWorkItem("<p>Park the exposure.</p>", false, null, null),
                "code review of 9-6.md",
                EpicNumber: 9,
                DetailHref: "../follow-ups/deferred-park.html",
                SourceKey: "9-6-follow-up"),
            new FollowUpDeferredSlot(
                new DeferredWorkItem("<p>~~Already fixed.~~</p>", true, null, null),
                "code review of 9-6.md",
                EpicNumber: 9,
                DetailHref: "../follow-ups/deferred-fixed.html",
                SourceKey: "9-6-follow-up"),
        };

        var svg = TaskGlance(tasks, deferred: deferred);

        Assert.Contains("\"colorClass\":\"sb-seg sb-followup-open\"", svg);
        Assert.Contains("\"colorClass\":\"sb-seg sb-followup-done\"", svg);
        Assert.Contains("Deferred item: Park the exposure.", svg);
        Assert.Contains("href=\"../follow-ups/deferred-park.html\"", svg);
        // Deferred parent is an inner-ring peer of tasks — children nest only under that wedge.
        Assert.Contains("Deferred: 1 open / 1 done", svg);
        Assert.Contains("href=\"#sec-deferred-from-artifact\"", svg);
    }

    [Fact]
    public void TaskSunburst_DeferredOnly_WhenNoTasks()
    {
        var deferred = new[]
        {
            new FollowUpDeferredSlot(
                new DeferredWorkItem("<p>Only deferred.</p>", false, null, null),
                "from story",
                1,
                "follow-ups/deferred-only.html"),
        };

        var svg = TaskGlance(Array.Empty<TaskItem>(), deferred: deferred);

        Assert.Contains("Deferred item: Only deferred.", svg);
        Assert.Contains("Deferred:", svg);
        Assert.DoesNotContain("No tasks tracked", svg);
    }

    [Fact]
    public void DonutLegend_EntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // The donut half of the interactive-legend emphasis (Story 3.5 Task 3, review follow-up: Subtask 3.1
        // names "sunburst OR donut" explicitly). Mirrors the sunburst legend guard: each entry needs a
        // status class the .donut-and-legend:has(...) rule can target AND a tabindex for keyboard reach.
        var html = Charts.DonutLegend(new (string Label, int Value, string CssClass)[]
        {
            ("Done", 3, "done"),
            ("Ready for dev", 1, "ready"),
        });

        Assert.Contains("<span class=\"dn-legend-item dn-done-item\" tabindex=\"0\">", html);
        Assert.Contains("<span class=\"dn-legend-item dn-ready-item\" tabindex=\"0\">", html);
        // The always-visible swatch + label + count remain (status is never emphasis-only / colour-only).
        Assert.Contains("<span class=\"swatch done\"></span>Done (3)</span>", html);
    }

    [Fact]
    public void CommitHeatmap_CellsCarryStaggerColumnIndexForEntrance()
    {
        // Each cell emits its week index as --col; specscribe.css derives the capped, reduced-motion-safe
        // staggered entrance delay from it and --motion-stagger. Guard the wiring, not the exact delay
        // (that's seed-level polish tuned in CSS). [Story 3.5 Task 2]
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 3),
            (new DateOnly(2026, 1, 20), 1),
        };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("style=\"--col:0\"", svg);
        // The class the future-day/level tests assert stays intact right beside the new style hook.
        Assert.Contains("class=\"heatmap-cell level-", svg);

        // Review follow-up: the assertion above alone would still pass if --col were hardcoded to 0 for every
        // cell (a broken/flattened stagger). Prove actual differentiation by collecting every distinct --col
        // value across the ~2-week-apart series and requiring more than one — the columns genuinely advance.
        var colValues = System.Text.RegularExpressions.Regex.Matches(svg, "--col:(\\d+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.True(colValues.Count > 1, $"Expected more than one distinct --col value, got: {string.Join(",", colValues)}");
    }

    [Fact]
    public void Sunburst_UndraftedStoryLinksToItsPlaceholderPageNotTheEpicPage()
    {
        // A story with no ArtifactOutputPath still has a generated placeholder page at StoryPagePath
        // (SiteGenerator writes one for every undrafted story) — the sunburst must link there, not
        // fall back to the epic page, so the reader always lands on the story's own detail page.
        var story = Story("1.2", "Not yet drafted", "pending", done: 0, total: 0);
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(story) },
        };

        var svg = Glance(model);

        Assert.Contains($"href=\"{StoryEpicLinkifier.StoryPagePath("1.2")}\"", svg);
        Assert.DoesNotContain("href=\"epics/epic-1.html\" Story 1.2", svg);
    }

    [Fact]
    public void Sunburst_CenterReportsEpicCountNotStoryCount()
    {
        // The chart is organized around epics, so the center headlines the epic count with an "epic(s)" label
        // (pluralized), never the story total. [spec-sunburst-epic-focus-and-ready-rollup]
        StoryInfo S(string id) => Story(id, "S", "done", 1, 1);

        var multi = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(S("1.1"), S("1.2"), S("1.3")), Epic(S("2.1")) },
        };
        var multiSvg = Glance(multi);

        var single = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(S("1.1")) },
        };
        var singleSvg = Glance(single);
    }

    [Fact]
    public void EpicSunburst_SegmentLinksCarryAriaLabels()
    {
        var epic = Epic(Story("1.1", "A story", "done", done: 3, total: 3));

        var svg = EpicGlance(epic, _ => "epics/epic-1.html");

        // Story aria now includes task count (no separate task ring).
        Assert.Contains("Story 1.1: A story", svg);
    }

    private static CommandCatalog Catalog() => new("BMad", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
    });

    [Fact]
    public void Sunburst_TaskWeighting_LargerStoryTakesMoreAngularSpace()
    {
        // Empty-task story: middle-ring sb-noplan (no outer create-story fringe).
        // [spec-9-13-deferred-glance-weight-noplan-sourcekey]
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Planned", "in progress", 2, 5), Story("1.2", "Unplanned", "ready", 0, 0)) },
        };

        var svg = Glance(model);

        // No outer task fringe (done/pending task arcs); empty-task story uses middle-ring noplan.
        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);
        Assert.Contains("Story 1.1: Planned", svg);
        Assert.Contains("Story 1.2: Unplanned", svg);
        Assert.Contains("No task plan", svg);
    }

    [Fact]
    public void Sunburst_NoTaskFringe_EmptyTaskStory()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Unplanned", "ready", 0, 0)) },
        };

        var svg = Glance(model);

        // Middle-ring noplan; no outer create-story fringe. [spec-9-13-deferred-glance-weight-noplan-sourcekey]
        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);
        Assert.Contains("no task plan yet", svg);
        Assert.DoesNotContain("create-story", svg);
        Assert.Contains("No task plan", svg);
    }

    [Fact]
    public void EpicSunburst_NoTaskFringe_EmptyTaskStory()
    {
        var epic = Epic(Story("1.1", "Unplanned", "ready", 0, 0));

        var svg = EpicGlance(epic, _ => "epics/epic-1.html");

        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);
        Assert.Contains("no task plan yet", svg);
        Assert.DoesNotContain("create-story", svg);
        Assert.Contains("Story 1.1: Unplanned", svg);
        Assert.Contains("No task plan", svg);
    }

    [Fact]
    public void Sunburst_FollowUps_SitInStoryRingUnderEpic_WithUnattributedSlice()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                Epic(Story("1.1", "Do the thing", "in progress", 1, 2)),
                new EpicInfo
                {
                    Number = 2,
                    Title = "Second",
                    GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted,
                    Section = EpicSection.FurtherDevelopment,
                    Stories = new[] { Story("2.1", "Other", "ready", 0, 1, epicNumber: 2) },
                },
            },
        };
        var items = new[]
        {
            new SprintActionItem("Fix the heatmap debt", "open", EpicNumber: 1, Owner: "Dana"),
            new SprintActionItem("Unscoped cleanup", "open", EpicNumber: null, Owner: null),
            new SprintActionItem("Ship delivery follow-up", "done", EpicNumber: 2, Owner: "Amelia"),
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Epic 1 deferred open item.
            - Epic 1 second deferred item.

            ## Deferred from: code review of 2-1-delivery.md (2026-07-15)

            - Epic 2 deferred item.

            ## Deferred from: cross-cutting backlog (2026-07-15)

            - Unattributed deferred open item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 4),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 4, OpenActionItems = 2 };
        var geometry = FollowUpGeometry.From(items, counts, work, deferredModel: deferredModel, epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        var svg = Glance(model, followUps: geometry, unplanned: unplanned);

        Assert.Equal(2, geometry.OpenActionItems.Count);
        Assert.Equal(counts.DeferredOpenItems, geometry.DeferredOpenCount);
        Assert.Equal(4, geometry.DeferredItems.Count);
        Assert.Single(geometry.UnattributedDeferredItems);
        Assert.Single(unplanned.UnattributableDeferred);
        // Project glance aggregates open vs done — no per-item leaf wedges.
        Assert.Contains("\"colorClass\":\"sb-seg sb-followup-open\"", svg);
        Assert.Contains("\"colorClass\":\"sb-seg sb-followup-done\"", svg);
        Assert.Contains("Epic 1: 3 open follow-ups", svg); // 1 action + 2 deferred
        Assert.Contains("Epic 2:", svg);
        Assert.Contains("done follow-up", svg);
        Assert.DoesNotContain("Action item: Fix the heatmap debt\"", svg);
        Assert.DoesNotContain("Deferred item: Epic 1 deferred open item.", svg);
        // Unattributed deferred moved to Unplanned; Follow-ups orphan holds action items only. [Story 9.12]
        Assert.Contains("Follow-ups: 1 unattributed item\"", svg);
        Assert.Contains("Unplanned:", svg);
        Assert.DoesNotContain("outermost: open follow-ups", svg);
        Assert.Contains("Open follow-up</span>", svg);
        Assert.Contains("Done follow-up</span>", svg);
        // Aggregates link to group pages, not per-item detail.
        Assert.Contains($"href=\"{FollowUpGroupPages.EpicPath(1)}\"", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.FollowUpsPath}\"", svg);
        Assert.DoesNotContain("href=\"follow-ups/action-fix-the-heatmap-debt", svg);
        foreach (var label in ExtractFollowUpAriaLabels(svg).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(label.StartsWith("Story", StringComparison.Ordinal), label);
        }
    }

    [Fact]
    public void Sunburst_FollowUps_NoUnattributedSlice_WhenAllDeferredAreEpicAttributed()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "in progress", 1, 2)) },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Only epic-attributed deferred item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        var svg = Glance(model, followUps: geometry);

        Assert.Contains("Epic 1: 1 open follow-up", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.EpicPath(1)}\"", svg);
        Assert.DoesNotContain("Deferred item: Only epic-attributed deferred item.", svg);
        Assert.DoesNotContain("Follow-ups:", svg);
    }

    [Fact]
    public void Sunburst_FollowUps_StoryHyphenProvenance_AttributesToEpic()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 3,
                    Title = "Insight Surfaces",
                    GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted,
                    Section = EpicSection.FurtherDevelopment,
                    Stories = new[] { Story("3.8", "Deep git insights", "done", 1, 1, epicNumber: 3) },
                },
            },
        };
        // Real deferred-work heading form — used to land in the unattributed Follow-ups slice.
        var deferredMarkdown = """
            ## Deferred from: code review of story-3-8 (2026-07-09)

            - Commit body containing a literal 0x1F control char could truncate numstat rows.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        Assert.Equal("3.8", deferredModel.Groups[0].SourceStoryId);

        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Empty(geometry.UnattributedDeferredItems);
        Assert.Single(geometry.DeferredForEpicNumber(3));

        var svg = Glance(model, followUps: geometry);
        // Project glance: story-attributed deferred folds into epic open aggregate.
        Assert.Contains("Epic 3: 1 open follow-up", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.EpicPath(3)}\"", svg);
        Assert.DoesNotContain("Deferred item: Commit body containing a literal 0x1F", svg);
        Assert.DoesNotContain("Follow-ups:", svg);
    }

    [Fact]
    public void Sunburst_FollowUps_EpicFallback_WhenStoryMissingFromModel()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            // Epic 3 exists but story 3.8 was renumbered away — still attribute via the epic prefix.
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 3,
                    Title = "Insight Surfaces",
                    GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted,
                    Section = EpicSection.FurtherDevelopment,
                    Stories = new[] { Story("3.1", "Other", "done", 1, 1, epicNumber: 3) },
                },
            },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of story-3-8 (2026-07-09)

            - Orphaned story provenance still belongs on Epic 3.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Single(geometry.DeferredForEpicNumber(3));
        Assert.Empty(geometry.UnattributedDeferredItems);
    }

    [Fact]
    public void Sunburst_FollowUps_OmittedWhenLedgerIsZero()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "done", 1, 1)) },
        };

        var without = Glance(model);
        var withEmpty = Glance(model, followUps: FollowUpGeometry.Empty);

        Assert.DoesNotContain("sb-followup", without);
        Assert.DoesNotContain("sb-followup", withEmpty);
        Assert.DoesNotContain("Open follow-up</span>", without);
        Assert.Equal(without, withEmpty);
    }

    [Fact]
    public void FollowUpGeometry_AggregateDeferred_WhenLedgerOpenButNoSlots()
    {
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 2),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 2 };
        // No deferred model → slots empty → aggregate preserves ledger debt.
        var geometry = FollowUpGeometry.From(Array.Empty<SprintActionItem>(), counts, work);
        Assert.Equal(2, geometry.DeferredOpenCount);
        Assert.Single(geometry.DeferredItems);
        Assert.Equal("deferred-work.html", geometry.DeferredHref);
        Assert.Contains("2 open deferred items", geometry.DeferredItems[0].Item.BodyHtml);

        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "A", "ready", 0, 1)) },
        };
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);
        var svg = Glance(model, followUps: geometry, unplanned: unplanned);
        // Unparseable ledger debt lands in Unplanned open aggregate (not a per-item wedge).
        Assert.Contains("Unplanned: 1 open item", svg);
        Assert.Contains("Unplanned:", svg);
        Assert.DoesNotContain("Deferred item: 2 open deferred items", svg);
    }

    [Fact]
    public void FollowUpGeometry_BuildsResolvedDeferred_WhenOpenCountIsZero()
    {
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - ~~Already resolved parking lot.~~
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 0),
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "A", "done", 1, 1)) },
        };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), ProjectCounts.Empty, work, deferredModel: deferredModel, epics: model);
        Assert.NotNull(geometry.DeferredHref);
        Assert.Single(geometry.DeferredItems);
        Assert.True(geometry.DeferredItems[0].Item.Resolved);
    }

    [Fact]
    public void Sunburst_OrphanActionItems_IncludeUnknownEpicNumber()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "A", "ready", 0, 1)) },
        };
        var geometry = new FollowUpGeometry(
            new[] { new SprintActionItem("Ghost epic debt", "open", EpicNumber: 99, Owner: null) },
            DeferredOpenCount: 0,
            DeferredHref: null,
            ActionItemsHref: SiteNav.ActionItemsOutputPath);
        var svg = Glance(model, followUps: geometry);
        Assert.Contains("Follow-ups: 1 open unattributed item", svg);
        Assert.Contains("Follow-ups:", svg);
        Assert.DoesNotContain("Action item: Ghost epic debt\"", svg);
    }

    [Fact]
    public void Sunburst_EmptyActionText_GetsFallbackLabel()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "A", "ready", 0, 1)) },
        };
        var geometry = new FollowUpGeometry(
            new[] { new SprintActionItem("   ", "open", EpicNumber: 1, Owner: null) },
            DeferredOpenCount: 0,
            DeferredHref: null,
            ActionItemsHref: SiteNav.ActionItemsOutputPath);
        var svg = Glance(model, followUps: geometry);
        // Empty action text still counts in the epic open aggregate (no per-item leaf).
        Assert.Contains("Epic 1: 1 open follow-up", svg);
        Assert.DoesNotContain("Action item: (no action text)", svg);
    }

    [Fact]
    public void EpicSunburst_FollowUps_AreAggregated_FilteredToEpic()
    {
        // Story 10.7 AC2: epic-level peers (actions + epic-level deferred) no longer render as individual
        // leaf wedges — they collapse into one open/done aggregate that links to the generated
        // group-epic-N page (the same 9.13 destination the project glance's outer aggregate already uses).
        var epic1 = Epic(Story("1.1", "One", "ready", 0, 1));
        var epic2 = new EpicInfo
        {
            Number = 2,
            Title = "Second",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Two", "ready", 0, 1, epicNumber: 2) },
        };
        var geometry = new FollowUpGeometry(
            new[]
            {
                new SprintActionItem("Epic 1 only", "open", 1, "Dana"),
                new SprintActionItem("Epic 2 only", "open", 2, "Amelia"),
            },
            DeferredOpenCount: 1,
            DeferredHref: "deferred-work.html",
            ActionItemsHref: SiteNav.ActionItemsOutputPath,
            DeferredSlots: new[]
            {
                // No SourceStoryId → epic-level peer, not a story-child leaf.
                new FollowUpDeferredSlot(
                    new DeferredWorkItem("<p>Epic 1 deferred</p>", false, null, null),
                    "from 1.1",
                    1,
                    "follow-ups/deferred-epic-1.html"),
            });

        var svg1 = EpicGlance(epic1, _ => "epics/epic-1.html", followUps: geometry);
        var svg2 = EpicGlance(epic2, _ => "epics/epic-2.html", followUps: geometry);

        // Epic 1: 1 open action + 1 open epic-level deferred = 2 open / 0 done — one aggregate wedge.
        Assert.Contains("Epic 1: 2 open follow-ups\"", svg1);
        Assert.Contains("\"colorClass\":\"sb-seg sb-followup-open\"", svg1);
        Assert.Contains("href=\"follow-ups/group-epic-1.html\"", svg1);
        Assert.DoesNotContain("Action item: Epic 1 only\"", svg1);
        Assert.DoesNotContain("Deferred item: Epic 1 deferred\"", svg1);
        Assert.DoesNotContain("href=\"follow-ups/action-", svg1);
        Assert.DoesNotContain("href=\"follow-ups/deferred-epic-1.html\"", svg1);
        Assert.DoesNotContain("Epic 2 only", svg1);

        // Epic 2: 1 open action only.
        Assert.Contains("Epic 2: 1 open follow-up\"", svg2);
        Assert.Contains("href=\"follow-ups/group-epic-2.html\"", svg2);
        Assert.DoesNotContain("Deferred item: Epic 1 deferred", svg2);
        Assert.DoesNotContain("Action item: Epic 2 only\"", svg2);
        Assert.DoesNotContain("Epic 1 only", svg2);
        Assert.DoesNotContain("outermost: open follow-ups", svg1);

        // When ActionItemsHref carries an epics/ depth prefix, the aggregate href must too.
        var prefixed = new FollowUpGeometry(
            geometry.ActionItems,
            geometry.DeferredOpenCount,
            DeferredHref: "../deferred-work.html",
            ActionItemsHref: "../" + SiteNav.ActionItemsOutputPath,
            DeferredSlots: geometry.DeferredItems);
        var svgPrefixed = EpicGlance(epic1, _ => "epics/epic-1.html", followUps: prefixed);
        Assert.Contains("href=\"../follow-ups/group-epic-1.html\"", svgPrefixed);
        Assert.DoesNotContain("href=\"follow-ups/group-epic-1.html\"", svgPrefixed);
    }

    [Fact]
    public void EpicSunburst_PeerAggregate_OmittedWhenNoPeers()
    {
        // NFR8: no epic-level peers at all → no aggregate wedge (story-only chart).
        var epic = Epic(Story("1.1", "Solo", "active", 1, 2));

        var svg = EpicGlance(epic, _ => "epics/epic-1.html");

        Assert.DoesNotContain("sb-followup-open", svg);
        Assert.DoesNotContain("sb-followup-done", svg);
        Assert.DoesNotContain("group-epic-", svg);
    }

    [Fact]
    public void EpicSunburst_PeerAggregate_ExcludesStoryChildDeferred_DiffersFromGlanceAggregate()
    {
        // Story 10.7 AC2 critical split: story-child deferred must NOT double-count into the epic-chart's
        // peer aggregate (it stays a nested leaf under its story) — while the project glance's own
        // CountEpicFollowUpAggregates DOES include it (existing 9.13/glance behavior). Same data, two
        // deliberately different counts.
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "active", 2, 4)) },
        };
        var epic = model.Epics[0];
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Story-child deferred item from code review.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Equal("1.1", geometry.StoryChildDeferred(1, "1.1")[0].SourceStoryId);

        var epicSvg = EpicGlance(epic, _ => "epics/epic-1.html", followUps: geometry);
        // The epic chart draws no peer aggregate — the only deferred item here is a story-child leaf.
        Assert.DoesNotContain("href=\"follow-ups/group-epic-1.html\"", epicSvg);
        Assert.Contains("Deferred item: Story-child deferred item from code review.", epicSvg);

        // The project glance's own aggregate DOES count the same story-child item (different, correct count).
        var glanceSvg = Glance(model, followUps: geometry);
        Assert.Contains("Epic 1: 1 open follow-up", glanceSvg);
    }

    [Fact]
    public void Sunburst_DenseEpic_StoryRingCollapsesToSummaryWedge()
    {
        // Story 10.7 AC1: an epic with 8+ stories collapses its middle ring to one summary wedge — same
        // destination as the epic's own inner-ring wedge, never a new click scheme.
        var denseStories = Enumerable.Range(1, 8)
            .Select(i => Story($"1.{i}", $"Story {i}", "ready", 0, 1))
            .ToArray();
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(denseStories) },
        };

        var svg = Glance(model);

        // INVERTED BY STORY 20.7, deliberately and with an owner decision behind it. The collapse was a DRAWING
        // constraint, never a fact about the work: a fixed 380 px static chart cannot fit eight legible story
        // wedges inside one epic's sweep, so it drew "8 stories" instead. The component is larger and — decisively
        // — it DRILLS, so an epic's own view has the whole sweep to itself. Collapsing there hid exactly the
        // stories a reader had drilled in to find, and made them unselectable, which is what select mode exists
        // for. `expandDenseEpics: true`, owner-directed 2026-07-25 (Story 20.5).
        //
        // Weights are untouched by the expansion — the summary wedge's weight was always the exact sum of the
        // per-story weights that replace it — and that equivalence is pinned in
        // HierarchyExplorerTests.AC1_DenseEpic_TheComponentExpandsWhatTheSvgHadToCollapse.
        Assert.DoesNotContain("sb-story-summary", svg);
        Assert.Contains("Story 1.1:", svg);
        Assert.Contains("Story 1.8:", svg);
        Assert.Contains("href=\"epics/epic-1.html\"", svg);  // the epic wedge keeps its own destination
    }

    [Fact]
    public void Sunburst_DenseEpic_AllNoPlanStoriesCollapsed_SuppressesOrphanedNoPlanLegendSwatch()
    {
        // Story 10.7 deferred debt: when every no-plan story lives inside a collapsed 8+-story epic, the
        // summary wedge carries no .sb-noplan class — the legend must not advertise a "No task plan" swatch
        // that matches no wedge on the chart.
        var denseNoPlanStories = Enumerable.Range(1, 8)
            .Select(i => Story($"1.{i}", $"Story {i}", "ready", 0, 0))
            .ToArray();
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(denseNoPlanStories) },
        };

        var svg = Glance(model);

        // ALSO INVERTED, and the debt this test was written for is now UNREPRESENTABLE rather than merely fixed.
        // Its concern was a legend advertising a "No task plan" swatch that matched no wedge, because the
        // collapsed summary carried no `.sb-noplan` class while a boolean flag said no-plan stories existed. The
        // component draws those stories (see the test above), so the swatch matches real sectors — and legend
        // MEMBERSHIP is now derived from the payload rather than from a flag, so a swatch can no longer be
        // orphaned from its wedges by any route. [Story 20.7 Task 2.1]
        Assert.DoesNotContain("sb-story-summary", svg);
        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);
        Assert.Contains("No task plan", svg);
    }

    [Fact]
    public void Sunburst_MixedDenseAndSparseEpics_LegendReflectsOnlyTheVisibleNoPlanWedge()
    {
        // Code-review patch: hasVisibleNoPlan is an OR across every epic. A dense-collapsed epic's no-plan
        // stories must not surface the legend on their own (previous test), but a SEPARATE sparse epic with a
        // genuine visible no-plan story must still surface it — this is the mixed case that would catch a
        // regression where the OR itself is wired wrong (e.g. inverted, or scoped to only the last epic).
        var denseAllPlanned = Enumerable.Range(1, 8)
            .Select(i => Story($"1.{i}", $"Story {i}", "ready", 1, 1))
            .ToArray();
        var epic1 = Epic(denseAllPlanned);
        var epic2 = new EpicInfo
        {
            Number = 2,
            Title = "Second Epic",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("2.1", "Visible no-plan story", "ready", 0, 0, epicNumber: 2) },
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epic1, epic2 },
        };

        var svg = Glance(model);

        // The OR this test guards is gone with the flag that needed it: legend membership is read off the
        // payload's own statuses, so "does any visible wedge carry no-plan" is answered by looking rather than by
        // arithmetic that could be inverted or mis-scoped. Both epics now draw per-story wedges.
        Assert.DoesNotContain("sb-story-summary", svg);
        Assert.Contains("Story 1.1:", svg);                                  // epic 1 expanded
        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);          // epic 2's no-plan story
        Assert.Contains("No task plan", svg);                                // legend surfaces it
    }

    [Fact]
    public void Sunburst_SparseEpic_JustBelowThreshold_KeepsPerStoryWedges()
    {
        // Boundary: one below StoryDensityCollapseThreshold (7 stories) still renders individually.
        Assert.Equal(8, Charts.StoryDensityCollapseThreshold);
        var sparseStories = Enumerable.Range(1, 7)
            .Select(i => Story($"1.{i}", $"Story {i}", "ready", 0, 1))
            .ToArray();
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(sparseStories) },
        };

        var svg = Glance(model);

        Assert.DoesNotContain("sb-story-summary", svg);
        Assert.Contains("Story 1.1:", svg);
        Assert.Contains("Story 1.7:", svg);
    }

    [Fact]
    public void SunburstCompanionList_ListsEpicsAndFollowUpRoots_SameDestinationsAsChart()
    {
        var epic1 = Epic(Story("1.1", "One", "active", 1, 2));
        var epic2 = new EpicInfo
        {
            Number = 2,
            Title = "Second",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Two", "ready", 0, 1, epicNumber: 2) },
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epic1, epic2 },
        };
        var items = new[] { new SprintActionItem("Orphan action", "open", EpicNumber: null, Owner: null) };
        var work = new WorkInventory { QuickDev = Array.Empty<QuickDevEntry>(), Deferred = null };
        var counts = ProjectCounts.Empty with { OpenActionItems = 1 };
        var geometry = FollowUpGeometry.From(items, counts, work, epics: model);

        var list = Charts.SunburstCompanionList(model, followUps: geometry);

        Assert.Contains("class=\"epic-remaining-grid\"", list);
        Assert.Contains("aria-label=\"Epic 1: First Epic — In development, 1 story\"", list);
        Assert.Contains("href=\"epics/epic-1.html\"", list);
        Assert.Contains("<span class=\"epic-remaining-num\">Epic 1</span>", list);
        Assert.Contains("<span class=\"epic-remaining-title\">First Epic</span>", list);
        // Status is never color-only (UX-DR17) — a visible label span restates what the accent bar shows.
        Assert.Contains("<span class=\"epic-remaining-status\">In development</span>", list);
        Assert.Contains("aria-label=\"Epic 2: Second — Ready for dev, 1 story\"", list);
        Assert.Contains("<span class=\"epic-remaining-status\">Ready for dev</span>", list);
        Assert.Contains("href=\"epics/epic-2.html\"", list);
        Assert.Contains($"href=\"{geometry.FollowUpsGroupHref}\"", list);
        Assert.Contains("1 unattributed item", list);
        // NFR8: no Unplanned tile when nothing is unplanned.
        Assert.DoesNotContain("Unplanned:", list);
        Assert.DoesNotContain("epic-remaining-unplanned", list);
    }

    [Fact]
    public void SunburstCompanionList_EmptyProject_ReturnsEmptyString()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = Array.Empty<EpicInfo>(),
        };

        Assert.Equal(string.Empty, Charts.SunburstCompanionList(model));
    }

    [Fact]
    public void SunburstCompanionList_DoneEpicWithNoOpenFollowUps_IsOmitted_ButDoneWithFollowUpsStays()
    {
        // Live owner feedback: a fully-done epic with nothing open has no "remaining work" to report here —
        // it's still reachable via the sunburst's own epic wedge, Epic Status tile, and Progress by Epic.
        // A done epic that STILL has open follow-ups is genuinely not finished, so it must stay (with a
        // visible "Done" status label, never color-only).
        var finishedEpic = Epic(Story("1.1", "Wrapped up", "done", 3, 3));
        finishedEpic.HasRetrospective = true; // retro-gated "done" tier, not "review" [Story 1.5]
        var doneWithFollowUps = new EpicInfo
        {
            Number = 2,
            Title = "Done But Not Quite",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Wrapped up too", "done", 1, 1, epicNumber: 2) },
            HasRetrospective = true, // retro-gated "done" tier, not "review" [Story 1.5]
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { finishedEpic, doneWithFollowUps },
        };
        var items = new[] { new SprintActionItem("Still open on Epic 2", "open", EpicNumber: 2, Owner: null) };
        var work = new WorkInventory { QuickDev = Array.Empty<QuickDevEntry>(), Deferred = null };
        var counts = ProjectCounts.Empty with { OpenActionItems = 1 };
        var geometry = FollowUpGeometry.From(items, counts, work, epics: model);

        var list = Charts.SunburstCompanionList(model, followUps: geometry);

        Assert.DoesNotContain("Epic 1", list);
        Assert.DoesNotContain("Wrapped up</span>", list);
        Assert.Contains("aria-label=\"Epic 2: Done But Not Quite — Done, 1 story, 1 open follow-up\"", list);
        Assert.Contains("<span class=\"epic-remaining-status\">Done</span>", list);
        Assert.Contains("class=\"epic-remaining-tile epic-remaining-done\"", list);
    }

    [Fact]
    public void SunburstCompanionList_AllEpicsFinishedAndNoOrphanRoots_ReturnsEmptyString()
    {
        // NFR8: when every epic is fully done with nothing open and there's no Follow-ups/Unplanned root
        // either, the whole grid — not just individual tiles — must be empty, never an empty shell.
        var finishedEpic = Epic(Story("1.1", "Wrapped up", "done", 2, 2));
        finishedEpic.HasRetrospective = true; // retro-gated "done" tier, not "review" [Story 1.5]
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { finishedEpic },
        };

        Assert.Equal(string.Empty, Charts.SunburstCompanionList(model));
    }

    [Fact]
    public void Sunburst_Unplanned_RootWithOpenQuickDevAndUnattributableDeferred()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "in progress", 1, 2)) },
        };
        var deferredMarkdown = """
            ## Deferred from: cross-cutting backlog (2026-07-15)

            - Parked direct deferred item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Fix the footer", "implementation-artifacts/spec-fix-footer.html", "ready", "chore"),
                new QuickDevEntry("Done one-shot", "implementation-artifacts/spec-done.html", "done", "chore"),
            },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1, DirectChanges = 2, OpenActionItems = 1 };
        var geometry = FollowUpGeometry.From(
            new[] { new SprintActionItem("Orphan action", "open", null, null) },
            counts, work, deferredModel: deferredModel, epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        Assert.Equal(2, counts.DirectChanges);
        Assert.Single(unplanned.UnplannedQuickDev); // done filtered out
        Assert.Single(unplanned.UnattributableDeferred);
        Assert.Equal(2, unplanned.UnplannedSet.Count);

        var svg = Glance(model, followUps: geometry, unplanned: unplanned);

        Assert.Contains("Unplanned:", svg);
        Assert.Contains("Unplanned: 2 open items", svg); // open QD + unattributable deferred
        Assert.DoesNotContain("Direct change: Fix the footer\"", svg);
        Assert.DoesNotContain("Deferred item: Parked direct deferred item.\"", svg);
        Assert.Contains("Direct change</span>", svg);
        Assert.Contains("\"colorClass\":\"sb-seg sb-unplanned\"", svg);
        Assert.DoesNotContain("Direct change: Done one-shot", svg);
        // Follow-ups orphan still holds the unattributed action only (aggregated).
        Assert.Contains("Follow-ups: 1 unattributed item\"", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.FollowUpsPath}\"", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.UnplannedPath}\"", svg);
        Assert.DoesNotContain($"href=\"{SiteNav.ActionItemsOutputPath}\"", svg);
        Assert.DoesNotContain("href=\"deferred-work.html\"", svg);
        Assert.Contains("Follow-ups: 1 open unattributed item", svg);
        Assert.Contains("Open follow-up</span>", svg); // follow-ups present → legend OK
        foreach (var label in ExtractFollowUpAriaLabels(svg).Split('|', StringSplitOptions.RemoveEmptyEntries))
            Assert.False(label.StartsWith("Story", StringComparison.Ordinal), label);
    }

    [Fact]
    public void Sunburst_UnplannedOnly_DoesNotShowFollowUpLegendSwatches()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "ready", 0, 1)) },
        };
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Fix the footer", "implementation-artifacts/spec-fix-footer.html", "ready", "chore"),
            },
            Deferred = null,
        };
        var unplanned = UnplannedWorkGeometry.From(work, FollowUpGeometry.Empty, model);
        var svg = Glance(model, followUps: FollowUpGeometry.Empty, unplanned: unplanned);

        Assert.Contains("Unplanned:", svg);
        Assert.Contains("Direct change</span>", svg);
        Assert.DoesNotContain("Open follow-up</span>", svg);
        Assert.DoesNotContain("Done follow-up</span>", svg);
    }

    [Fact]
    public void Sunburst_Unplanned_OmittedWhenEmpty_AttributedQuickDevPrefersEpic()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "ready", 0, 1)) },
        };
        var workEmpty = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Already shipped", "qd.html", "done", null) },
            Deferred = null,
        };
        var geometryEmpty = FollowUpGeometry.Empty;
        var unplannedEmpty = UnplannedWorkGeometry.From(workEmpty, geometryEmpty, model);
        Assert.False(unplannedEmpty.HasUnplanned);

        var omitted = Glance(model, followUps: geometryEmpty, unplanned: unplannedEmpty);
        Assert.DoesNotContain("Unplanned:", omitted);
        Assert.DoesNotContain("sb-unplanned", omitted);
        Assert.DoesNotContain("Direct change</span>", omitted);

        var workAttributed = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Story 1.1 hotfix", "implementation-artifacts/spec-hotfix.html", null, "bugfix"),
            },
            Deferred = null,
        };
        var unplannedAttributed = UnplannedWorkGeometry.From(workAttributed, FollowUpGeometry.Empty, model);
        Assert.Empty(unplannedAttributed.UnplannedQuickDev);
        Assert.Single(unplannedAttributed.ForEpic(1));

        var svg = Glance(model, unplanned: unplannedAttributed);
        Assert.DoesNotContain("Unplanned:", svg);
        Assert.Contains("Epic 1: 1 open follow-up", svg);
        Assert.Contains($"href=\"{FollowUpGroupPages.EpicPath(1)}\"", svg);
        Assert.DoesNotContain("Direct change: Story 1.1 hotfix\"", svg);
    }

    [Fact]
    public void UnplannedSet_MatchesSunburstAndSprintMembership()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("2.1", "Other", "ready", 0, 1, epicNumber: 2)) },
        };
        var deferredMarkdown = """
            ## Deferred from: misc (2026-07-15)

            - Unscoped park.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("One-off UI polish", "spec-polish.html", "in-progress", null) },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), ProjectCounts.Empty with { DeferredOpenItems = 1 },
            work, deferredModel: deferredModel, epics: model);
        var unplanned = UnplannedWorkGeometry.From(work, geometry, model);

        var svg = Glance(model, followUps: geometry, unplanned: unplanned);
        var sprint = SprintStatusParser.Parse("""
            last_updated: "2026-07-17"
            development_status:
              epic-2: in-progress
              2-1-other: ready-for-dev
            """);
        Assert.NotNull(sprint);
        var board = SprintTemplater.RenderBoard(sprint, model, unplanned: unplanned);
        var byEpic = SprintTemplater.RenderBoardByEpic(sprint, model, unplanned: unplanned);

        foreach (var member in unplanned.UnplannedSet)
        {
            Assert.Contains($"href=\"{member.Href}\"", board);
            Assert.Contains($"href=\"{member.Href}\"", byEpic);
        }
        // Project sunburst aggregates Unplanned — leaves stay on the sprint board / group page.
        Assert.Contains($"href=\"{FollowUpGroupPages.UnplannedPath}\"", svg);
        Assert.DoesNotContain("href=\"spec-polish.html\"", svg);
        Assert.Contains("sprint-lane unplanned", board);
        Assert.Contains("sprint-epic-lane unplanned", byEpic);
        Assert.Contains("Direct change", board);
        Assert.DoesNotContain("Story One-off", board);
    }

    private static string ExtractFollowUpAriaLabels(string svg)
    {
        // There is no SVG aria-label attribute to scan any more (Story 20.7 retired the last hand-rolled SVG
        // renderer) — the payload island is where each node's label actually lives now, as a JSON `"label":"..."`
        // pair.
        var labels = new List<string>();
        var needle = "\"label\":\"";
        for (var i = 0; (i = svg.IndexOf(needle, i, StringComparison.Ordinal)) >= 0;)
        {
            i += needle.Length;
            var end = svg.IndexOf('"', i);
            if (end < 0) break;
            var label = svg[i..end];
            if (label.StartsWith("Action item", StringComparison.Ordinal)
                || label.StartsWith("Deferred item", StringComparison.Ordinal)
                || label.StartsWith("Follow-ups:", StringComparison.Ordinal)
                || label.StartsWith("Direct change", StringComparison.Ordinal)
                || label.StartsWith("Unplanned:", StringComparison.Ordinal))
            {
                labels.Add(label);
            }
            i = end + 1;
        }
        return string.Join("|", labels);
    }

    [Fact]
    public void EpicMosaic_SegmentsByDeliveryStatusNotDetailedCoverage()
    {
        // A mid-development epic: one story done, one in-dev, one ready. The ring must show the real delivery
        // mix (done + active + ready segments), NOT a single full "detailed/ready" ring, and keep "N/N
        // detailed" as the sub-label only. [Story 2.1 UXO A6]
        var epic = new EpicProgress
        {
            Number = 1,
            Title = "Mid-dev epic",
            StoryCount = 3,
            StoriesWithArtifact = 3,
            TasksDone = 4,
            TasksTotal = 10,
            Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int> { ["done"] = 1, ["active"] = 1, ["ready"] = 1 },
        };

        var html = Charts.EpicMosaic(new[] { epic }, _ => "epics/epic-1.html");

        Assert.Contains("donut-seg done", html);
        Assert.Contains("donut-seg active", html);
        Assert.Contains("donut-seg ready", html);
        // "N/N detailed" survives as the sub-label.
        Assert.Contains("3 / 3 stories detailed", html);
    }

    [Fact]
    public void EpicMosaic_PendingEpicKeepsEmptyRingAndNotYetDrafted()
    {
        var pending = new EpicProgress
        {
            Number = 2,
            Title = "Pending epic",
            StoryCount = 0,
            StoriesWithArtifact = 0,
            TasksDone = 0,
            TasksTotal = 0,
            Status = EpicStatus.Pending,
            StoryStatusCounts = new Dictionary<string, int>(),
        };

        var html = Charts.EpicMosaic(new[] { pending }, _ => "epics/epic-2.html");

        // Empty ring (no colored delivery segments), and the "Not yet drafted" label rather than a 0%/full fill.
        Assert.Contains("Not yet drafted", html);
        Assert.DoesNotContain("donut-seg done", html);
        Assert.DoesNotContain("donut-seg active", html);
        Assert.DoesNotContain("donut-seg ready", html);
    }

    [Fact]
    public void DeliverySentence_OrdersDoneFirstOmitsZeroAndUsesStoryLabels()
    {
        var sentence = Charts.DeliverySentence(new Dictionary<string, int>
        {
            ["done"] = 6,
            ["review"] = 1,
            ["active"] = 0,
        });

        Assert.Equal("6 of 7 done, 1 in review", sentence);
    }

    [Fact]
    public void DeliverySentence_NamesRetiredAsItsOwnStage_NotUnrecognized()
    {
        // Story 8.9 AC #3: before `retired` joined StoryStages this epic read "5 of 6 done, 1 unrecognized",
        // reporting a deliberate planning decision as an unmapped word. Order is narrative — retired is the
        // second TERMINAL stage, so it follows done and precedes everything still owed.
        var sentence = Charts.DeliverySentence(new Dictionary<string, int>
        {
            ["done"] = 5,
            ["retired"] = 1,
        });

        Assert.Equal("5 of 6 done, 1 retired", sentence);
        Assert.DoesNotContain("unrecognized", sentence);

        var mixed = Charts.DeliverySentence(new Dictionary<string, int>
        {
            ["done"] = 3,
            ["retired"] = 1,
            ["review"] = 2,
        });
        Assert.Equal("3 of 6 done, 1 retired, 2 in review", mixed);
    }

    [Fact]
    public void DeliverySentence_SingleStage_HasNoTrailingClause()
    {
        Assert.Equal("7 of 7 done", Charts.DeliverySentence(new Dictionary<string, int> { ["done"] = 7 }));
    }

    [Fact]
    public void EpicMosaic_ExposesDeliverySentenceAsVisibleLine_DonutStaysDecorative()
    {
        var epic = new EpicProgress
        {
            Number = 1,
            Title = "Mid-dev epic",
            StoryCount = 7,
            StoriesWithArtifact = 7,
            TasksDone = 10,
            TasksTotal = 10,
            Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int> { ["done"] = 6, ["review"] = 1 },
        };

        var html = Charts.EpicMosaic(new[] { epic }, _ => "epics/epic-1.html");
        const string sentence = "6 of 7 done, 1 in review";

        // Visible sentence inside the card <a> is the accessible name; naming the Donut would couple to
        // per-slice tabindex and nest interactives in the link. [Story 8.4 review]
        var donutHtml = html.Substring(html.IndexOf("epic-mosaic-donut", StringComparison.Ordinal));
        donutHtml = donutHtml[..donutHtml.IndexOf("epic-mosaic-label", StringComparison.Ordinal)];
        Assert.Contains("aria-hidden=\"true\"", donutHtml);
        Assert.DoesNotContain("role=\"img\"", donutHtml);
        Assert.DoesNotContain("tabindex=\"0\"", donutHtml);
        Assert.DoesNotContain($"aria-label=\"{sentence}\"", html);
        Assert.Contains($"class=\"epic-mosaic-delivery\">{sentence}</span>", html);
        // Planning-depth sub-label kept alongside the delivery sentence.
        Assert.Contains("7 / 7 stories detailed", html);
    }

    [Fact]
    public void Donut_WithAriaLabel_IsRoleImgWithName()
    {
        var svg = Charts.Donut(new (string, int, string)[]
        {
            ("Drafted", 3, "drafted"),
            ("Pending", 2, "pending"),
        }, ariaLabel: "Epic status: 3 drafted, 2 pending");

        Assert.Contains("role=\"img\"", svg);
        Assert.Contains("aria-label=\"Epic status: 3 drafted, 2 pending\"", svg);
    }

    [Fact]
    public void Donut_WithoutAriaLabel_IsDecorative()
    {
        var svg = Charts.Donut(new (string, int, string)[] { ("Detailed", 1, "ready") });

        Assert.Contains("aria-hidden=\"true\"", svg);
        Assert.DoesNotContain("role=\"img\"", svg);
    }

    [Fact]
    public void CommitHeatmap_WithoutDetailsCarriesRoleImgAndReadableName()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var d2 = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (d1, 3), (d2, 1) };

        var svg = Charts.CommitHeatmap(series);

        // A link-free render keeps role="img": one named graphic, children hidden from AT.
        Assert.Contains("role=\"img\"", svg);
        // Visible/AT dates read in the human format; the range uses "to", not an en-dash.
        Assert.Contains($"across 2 active days, {Charts.DReadable(d1)} to {Charts.DReadable(d2)}", svg);
        Assert.Contains($"<title>{Charts.DReadable(d1)}: 3 commits</title>", svg);
        Assert.DoesNotContain("<a href=\"commits/", svg);
    }

    private static CommitInfo C(string hash, string subject) => new(hash, subject, "Alice", "12:00");

    private static IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> Commits(
        params (DateOnly Day, CommitInfo[] Items)[] days) =>
        days.ToDictionary(d => d.Day, d => (IReadOnlyList<CommitInfo>)d.Items);

    [Fact]
    public void CommitHeatmap_LinksActiveDaysToTheirPagesWithReadableNames()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var d2 = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (d1, 2), (d2, 1) };
        var commits = Commits(
            (d1, new[] { C("abc1234", "First change"), C("def5678", "Second change") }),
            (d2, new[] { C("aaa1111", "Third change") }));

        var svg = Charts.CommitHeatmap(series, commits);

        // With day-page links present, the SVG is role="group" so AT can reach them.
        Assert.Contains("role=\"group\"", svg);
        Assert.DoesNotContain("role=\"img\"", svg);
        // Active-day cells link to their generated per-day page; href stays ISO, the name is readable.
        Assert.Contains($"<a href=\"commits/2026-01-05.html\" aria-label=\"{Charts.DReadable(d1)}: 2 commits — view details\">", svg);
        Assert.Contains($"<a href=\"commits/2026-01-07.html\" aria-label=\"{Charts.DReadable(d2)}: 1 commit — view details\">", svg);
        // The heatmap no longer inlines any panels or commit content — that lives on the day page.
        // (Guard against the panel markup, not the "heatmap-daylabel" axis class it collides with.)
        Assert.DoesNotContain("heatmap-days", svg);
        Assert.DoesNotContain("<section", svg);
        Assert.DoesNotContain("First change", svg);
    }

    [Fact]
    public void CommitHeatmap_ZeroCommitDaysAreNotLinks()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var series = new (DateOnly Day, int Count)[] { (d1, 2) };
        var commits = Commits((d1, new[] { C("abc1234", "Only change") }));

        var svg = Charts.CommitHeatmap(series, commits);

        // Exactly ONE anchor inside the SVG: the single active day. Zero-commit cells stay unwrapped
        // (no ~100-stop keyboard trap), so the grid contains no other hrefs.
        var svgOnly = svg[..svg.IndexOf("</svg>", StringComparison.Ordinal)];
        Assert.Equal(1, CountOf(svgOnly, "<a href=\"commits/"));
        // The zero-day tooltip is still present for pointer users.
        Assert.Contains(": 0 commits</title>", svgOnly);
    }

    [Fact]
    public void CommitHeatmap_WithoutDetailsRendersNoLinks()
    {
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3) };

        var svg = Charts.CommitHeatmap(series);

        Assert.DoesNotContain("<a href=\"commits/", svg);
        Assert.DoesNotContain("heatmap-days", svg);
        Assert.DoesNotContain("<section", svg);
    }

    [Fact]
    public void LinkedCommitDays_AreActiveDaysAscendingSkippingEmptyAndFuture()
    {
        var today = new DateOnly(2026, 1, 20);
        var d1 = new DateOnly(2026, 1, 9);
        var d2 = new DateOnly(2026, 1, 5);
        var empty = new DateOnly(2026, 1, 7);
        var future = new DateOnly(2026, 1, 25);
        var series = new (DateOnly Day, int Count)[] { (d1, 1), (d2, 2), (empty, 0), (future, 1) };
        var commits = Commits(
            (d1, new[] { C("a", "x") }),
            (d2, new[] { C("b", "y") }),
            (future, new[] { C("f", "z") }));

        var linked = Charts.LinkedCommitDays(series, commits, today);

        // Ascending; the zero-count day and the future-dated day are both excluded.
        Assert.Equal(new[] { d2, d1 }, linked);
    }

    [Fact]
    public void LinkedCommitDays_WithoutDetailsIsEmpty()
    {
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3) };
        Assert.Empty(Charts.LinkedCommitDays(series, null, new DateOnly(2026, 1, 20)));
    }

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    [Fact]
    public void Donut_WithCenterText_ShowsFractionInsteadOfBareTotal()
    {
        var svg = Charts.Donut(new (string, int, string)[]
        {
            ("Done", 4, "done"),
            ("Pending", 10, "pending"),
        }, centerText: "4/14");

        // The center reads as progress (a fraction), not a bare total that looks like a score. [Story 1.5 E3]
        Assert.Contains("donut-center-fraction", svg);
        Assert.Contains(">4/14</text>", svg);
    }

    [Fact]
    public void Donut_WithoutCenterText_ShowsTotal()
    {
        var svg = Charts.Donut(new (string, int, string)[]
        {
            ("Done", 4, "done"),
            ("Pending", 10, "pending"),
        });

        Assert.Contains(">14</text>", svg);
        Assert.DoesNotContain("donut-center-fraction", svg);
    }

    [Fact]
    public void CommitHeatmap_MutesFutureDays()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var series = new (DateOnly Day, int Count)[] { (today.AddDays(-3), 2) };

        var svg = Charts.CommitHeatmap(series);

        // A real past day is rendered with its (now human-readable) tooltip...
        Assert.Contains($"<title>{Charts.DReadable(today.AddDays(-3))}: 2 commits</title>", svg);
        // ...but tomorrow is never rendered — future days aren't zero-commit days. [Story 1.5 A4]
        Assert.DoesNotContain($"{Charts.DReadable(today.AddDays(1))}:", svg);
    }

    [Fact]
    public void CommitHeatmap_UniformSingleCommitHistoryRendersLightNotMaxed()
    {
        // Every active day has exactly one commit, so the busiest day is a single commit (maxCount == 1) — the
        // degenerate case that used to collapse HeatLevel to the darkest level. [heatmap-debt-triage]
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 1),
            (new DateOnly(2026, 1, 8), 1),
            (new DateOnly(2026, 1, 12), 1),
        };

        var svg = Charts.CommitHeatmap(series);

        // Active cells read as light (level-1), never maxed-out — a sparse project must not look like heavy
        // activity (visual-truthfulness rule). The cell class is distinct from the legend swatch class, so no
        // scoping is needed to exclude the always-present level-4 legend swatch.
        Assert.Contains("class=\"heatmap-cell level-1\"", svg);
        Assert.DoesNotContain("heatmap-cell level-2", svg);
        Assert.DoesNotContain("heatmap-cell level-3", svg);
        Assert.DoesNotContain("heatmap-cell level-4", svg);
    }

    [Fact]
    public void CommitHeatmap_GradedHistoryStillReachesLevel4ForBusiestDay()
    {
        // A real graded history (busiest day has 8 commits) is untouched by the sparse-history fix.
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 1),   // ratio 1/8 <= 0.25 → level 1
            (new DateOnly(2026, 1, 8), 8),   // busiest → level 4
        };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("class=\"heatmap-cell level-4\"", svg);
        Assert.Contains("class=\"heatmap-cell level-1\"", svg);
    }

    [Fact]
    public void CommitHeatmap_FormatsDatesWithInvariantHelpers()
    {
        var day = new DateOnly(2026, 1, 5);
        var series = new (DateOnly Day, int Count)[] { (day, 2) };

        var svg = Charts.CommitHeatmap(series);

        // Every heatmap date routes through the invariant Charts.D/DReadable helpers (cell titles + whole-chart
        // aria) and month labels through InvariantCulture, so cell dates can never drift from the month axis
        // under a non-Gregorian ambient calendar. [heatmap-debt-triage — verified resolved, pinned here]
        Assert.Contains($"<title>{Charts.DReadable(day)}: 2 commits</title>", svg);
        Assert.Contains($"{Charts.DReadable(day)} to {Charts.DReadable(day)}", svg);
        Assert.Contains(
            $">{day.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)}</text>", svg);
    }

    [Fact]
    public void CommitHeatmap_HasHeadline()
    {
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 3),
            (new DateOnly(2026, 1, 7), 1),
        };

        var svg = Charts.CommitHeatmap(series);

        // The primary "how has the work gone" visual carries a one-line summary headline. [Story 1.5 E1]
        Assert.Contains("heatmap-headline", svg);
        Assert.Contains($"last commit {Charts.DReadable(new DateOnly(2026, 1, 7))}", svg);
    }

    [Fact]
    public void CommitHeatmap_ShowHeadlineFalseSuppressesHeadline()
    {
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3) };

        var svg = Charts.CommitHeatmap(series, showHeadline: false);

        // GitPulsePanel embeds the heatmap with its own signal strip covering these figures; the flag must
        // suppress the heatmap's internal headline so the two don't duplicate the same numbers. [Story 3.1]
        Assert.DoesNotContain("heatmap-headline", svg);
        // The rest of the heatmap (grid, cell tooltips) still renders — only the headline line is gone.
        Assert.Contains($"<title>{Charts.DReadable(new DateOnly(2026, 1, 5))}: 3 commits</title>", svg);
    }

    private static GitPulse SampleGitPulse(IReadOnlyList<(string Path, int ChangeCount)> topChangedFiles) => new(
        TotalCommits: 5,
        ActiveDays: 2,
        FirstCommitDate: new DateOnly(2026, 1, 5),
        LastCommitDate: new DateOnly(2026, 1, 7),
        DailySeries: new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3), (new DateOnly(2026, 1, 7), 2) },
        CommitsByDay: new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
        {
            [new DateOnly(2026, 1, 7)] = new[] { new CommitInfo("aaa1111", "Change", "Alice", "09:15") },
        },
        LastCommitTimestamp: new DateTime(2026, 1, 7, 9, 15, 0),
        Last30DayCommitCount: 5,
        TopChangedFiles: topChangedFiles);

    [Fact]
    public void GitPulsePanel_RendersProportionalBarsForTopChangedFiles()
    {
        var git = SampleGitPulse(new (string, int)[] { ("src/Program.cs", 3), ("README.md", 1) });

        var html = Charts.GitPulsePanel(git);

        Assert.Contains("git-pulse-bar-fill", html);
        Assert.Contains("src/Program.cs", html);
        Assert.Contains("Top changed files", html);
        Assert.Contains("Last 5 commits", html); // honest window = min(200, TotalCommits)
        Assert.Contains("Top 2 files by change count", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.FileChurn), html);
        Assert.Contains("aria-label=\"src/Program.cs: 3 changes\"", html);
        Assert.Contains("aria-label=\"README.md: 1 change\"", html);
        Assert.Contains("git-pulse-bar-track\" aria-hidden=\"true\"", html);
        // Suppresses the embedded heatmap's own headline (the signal strip above already carries the figures).
        Assert.DoesNotContain("heatmap-headline", html);
    }

    [Fact]
    public void GitPulsePanel_EmptyTopChangedFilesShowsFallbackNote()
    {
        // A failed (but bounded) name-only git call degrades TopChangedFiles to an empty list rather than
        // nulling the whole pulse (AD-4: partial data beats none). [Story 3.1]
        var git = SampleGitPulse(Array.Empty<(string, int)>());

        var html = Charts.GitPulsePanel(git);

        Assert.Contains("No file changes in the last 5 commits.", html);
        Assert.Contains("Top changed files", html);
        Assert.Contains("Last 5 commits", html);
        Assert.DoesNotContain("git-pulse-bar-fill", html);
    }

    [Fact]
    public void CommitHeatmap_HeadlineLinksLastCommitDateToItsDatePage()
    {
        // Story 7.3/10.4: the "last commit" date is a date in the context of a change → a link to that day's date
        // page (guarded on it being a linked commit day, which it is). Needs commitsByDay so the day is "linked".
        var day = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3), (day, 1) };
        var commitsByDay = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
        {
            [day] = new[] { new CommitInfo("aaa1111", "Change", "Alice", "09:15") },
        };

        var svg = Charts.CommitHeatmap(series, commitsByDay);

        Assert.Contains($"last commit <a class=\"date-link\" href=\"commits/{Charts.D(day)}.html\">{Charts.DReadable(day)}</a>", svg);
    }

    [Fact]
    public void GitPulsePanel_LastCommitLinksToDatePage_AndCaptionsItsZone()
    {
        var git = SampleGitPulse(new (string, int)[] { ("src/Program.cs", 3) });

        var html = Charts.GitPulsePanel(git);

        // The exact last-commit timestamp is a date-page link (day 2026-01-07 from LastCommitTimestamp)...
        Assert.Contains("<span class=\"git-pulse-when\"><a class=\"date-link\" href=\"commits/2026-01-07.html\">", html);
        Assert.Contains("Jan 7, 2026 at 09:15", html);                 // one PortalDates token, 24-hour, no AM/PM
        // ...and the git clock's zone is captioned once (distinct from the machine-local, labeled footer).
        Assert.Contains("git-pulse-zone-note", html);
    }

    [Fact]
    public void ProgressBar_CarriesProgressbarAria()
    {
        var html = Charts.ProgressBar("Implementation", 2, 4);

        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=\"50\"", html);
        Assert.Contains("aria-valuemin=\"0\"", html);
        Assert.Contains("aria-valuemax=\"100\"", html);
        Assert.Contains("aria-label=\"Implementation: 2 / 4\"", html);
        // Visible fraction text stays.
        Assert.Contains(">2 / 4</div>", html);
    }

    // ---- Story pipeline funnel (Story 3.6) ---------------------------------------------------

    private static ProgressModel Pipeline(int stories, Dictionary<string, int> statusCounts) => new()
    {
        EpicsTotal = 2,
        EpicsDrafted = 2,
        EpicsPending = 0,
        StoriesTotal = stories,
        StoriesWithArtifact = 0,
        TasksDone = 0,
        TasksTotal = 0,
        PerEpic = new[]
        {
            new EpicProgress
            {
                Number = 1,
                Title = "E",
                StoryCount = stories,
                StoriesWithArtifact = 0,
                TasksDone = 0,
                TasksTotal = 0,
                Status = EpicStatus.Drafted,
                StoryStatusCounts = statusCounts,
            },
        },
    };

    [Fact]
    public void RefinementFunnel_RendersFiveCumulativeStagesWithCountsAndWholeChartName()
    {
        // Exclusive per-status counts: 11 drafted, 8 ready, 2 active, 4 review, 12 done (37 total).
        // Cumulative "reached at least" tiers: 37 → 26 → 18 → 16 → 12 — monotonically narrowing.
        var svg = Charts.RefinementFunnel(Pipeline(37, new Dictionary<string, int>
        {
            ["drafted"] = 11, ["ready"] = 8, ["active"] = 2, ["review"] = 4, ["done"] = 12,
        }));

        // Whole-chart accessible name summarizing every stage and cumulative count.
        Assert.Contains("role=\"img\"", svg);
        Assert.Contains("aria-label=\"Story pipeline: 37 stories drafted, 26 reached ready for dev, " +
                        "18 reached development, 16 reached review, 12 done\"", svg);
        // Every stage carries its visible count + text label (never color-only).
        Assert.Contains(">37</text>", svg);
        Assert.Contains(">Drafted</text>", svg);
        Assert.Contains(">26</text>", svg);
        Assert.Contains(">Ready for dev</text>", svg);
        Assert.Contains(">18</text>", svg);
        Assert.Contains(">In development</text>", svg);
        Assert.Contains(">16</text>", svg);
        Assert.Contains(">In review</text>", svg);
        Assert.Contains(">12</text>", svg);
        Assert.Contains(">Done</text>", svg);
        // Per-band tooltips spell out the reached-at-least reading; the %-of-stories sub gives conversion.
        Assert.Contains("<title>26 of 37 stories have reached Ready for dev</title>", svg);
        Assert.Contains("<title>12 of 37 stories are done</title>", svg);
        Assert.Contains(">70% of stories</text>", svg);
        // Bands ride the 1:1 status-token classes, joined by sideways-funnel connectors.
        Assert.Contains("funnel-band funnel-drafted", svg);
        Assert.Contains("funnel-band funnel-ready", svg);
        Assert.Contains("funnel-band funnel-active", svg);
        Assert.Contains("funnel-band funnel-review", svg);
        Assert.Contains("funnel-band funnel-done", svg);
        Assert.Contains("funnel-connector", svg);
        // Heights track the true cumulative counts (normalized to the drafted total) — a genuinely
        // monotonic narrowing: 136 ≥ 95.57 ≥ 66.16 ≥ 58.81 ≥ 44.11.
        Assert.Contains("height=\"136\"", svg);
        Assert.Contains("height=\"95.57\"", svg);
        Assert.Contains("height=\"66.16\"", svg);
        Assert.Contains("height=\"58.81\"", svg);
        Assert.Contains("height=\"44.11\"", svg);
    }

    [Fact]
    public void RefinementFunnel_EmptyModelReturnsChartEmptyPlaceholder()
    {
        var html = Charts.RefinementFunnel(ProgressModel.Empty);

        // Zero stories → the shared graceful placeholder, no SVG, no NaN/divide-by-zero artifacts.
        Assert.Contains("chart-empty", html);
        Assert.Contains("Nothing to chart yet.", html);
        Assert.DoesNotContain("<svg", html);
        Assert.DoesNotContain("NaN", html);
    }

    [Fact]
    public void RefinementFunnel_EarlyStageProjectRendersEveryStageIncludingZeroStages()
    {
        // "Just getting started": 3 stories, all merely drafted. Every later stage still renders its labeled
        // column with a real 0 count and an honest dashed placeholder band (no fill that could read as
        // data), and no height goes NaN/negative. [AC #2]
        var svg = Charts.RefinementFunnel(Pipeline(3, new Dictionary<string, int> { ["drafted"] = 3 }));

        Assert.Contains(">3</text>", svg);
        Assert.Contains(">Drafted</text>", svg);
        Assert.Contains(">0</text>", svg);
        Assert.Contains(">Ready for dev</text>", svg);
        Assert.Contains(">In development</text>", svg);
        Assert.Contains(">In review</text>", svg);
        Assert.Contains(">Done</text>", svg);
        Assert.Contains(">0% of stories</text>", svg);
        // All four later stages are zero → four dashed placeholder bands; drafted keeps the full height.
        Assert.Equal(4, CountOf(svg, "funnel-zero"));
        Assert.Contains("height=\"136\"", svg);
        Assert.DoesNotContain("NaN", svg);
        Assert.DoesNotContain("height=\"-", svg);
    }

    [Fact]
    public void RefinementFunnel_SingularCountsReadGrammatically()
    {
        // A single done story — the aria phrase and the per-band tooltip verbs pluralize correctly.
        var svg = Charts.RefinementFunnel(Pipeline(1, new Dictionary<string, int> { ["done"] = 1 }));

        Assert.Contains("aria-label=\"Story pipeline: 1 story drafted, 1 reached ready for dev, " +
                        "1 reached development, 1 reached review, 1 done\"", svg);
        Assert.Contains("<title>1 story drafted</title>", svg);
        Assert.Contains("<title>1 of 1 story has reached Ready for dev</title>", svg);
        Assert.Contains("<title>1 of 1 story is done</title>", svg);
    }

    // ---- Source-code treemap SVG (Story 7.6) -----------------------------------------------

    private static CodeMap TreemapWithMetrics() => CodeMap.Build(
        new[] { ("src/A.cs", 100L), ("src/B.cs", 40L) },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/A.cs"] = new CodeFileMetrics(5, 120, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1)),
        });

    // ---- File-type colorize dimension (Story 7.9) ----

    // ---- Refactor-target risk quadrant SVG (Story 7.10) -----------------------------------

    /// <summary>Six metric-bearing files with a clear high-size/high-churn outlier (BigHot.cs — both the largest
    /// and the most-changed file) so it's unambiguously above both medians, plus one file with no git record
    /// (excluded from the plot entirely). Six is exactly <see cref="Charts.RiskQuadrantMinFiles"/> — the minimum
    /// for the chart to render live.</summary>
    private static IReadOnlyList<CodeMapNode> RiskFiles() => CodeMap.Build(
        new[]
        {
            ("src/BigHot.cs", 5000L),
            ("src/B.cs", 200L),
            ("src/C.cs", 180L),
            ("src/D.cs", 150L),
            ("src/E.cs", 120L),
            ("src/F.cs", 100L),
            ("src/NoGit.cs", 90L),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/BigHot.cs"] = new CodeFileMetrics(50, 900, null, null),
            // The other five files are deliberately anti-correlated (largest of the five has the FEWEST changes,
            // smallest has the most) so no file besides BigHot.cs sits above both axis medians at once.
            ["src/B.cs"] = new CodeFileMetrics(1, 10, null, null),
            ["src/C.cs"] = new CodeFileMetrics(2, 20, null, null),
            ["src/D.cs"] = new CodeFileMetrics(3, 30, null, null),
            ["src/E.cs"] = new CodeFileMetrics(4, 40, null, null),
            ["src/F.cs"] = new CodeFileMetrics(5, 50, null, null),
            // src/NoGit.cs deliberately has no metrics entry — excluded from the plot.
        }).Files();

    [Fact]
    public void RiskQuadrant_PlotsEveryMetricBearingFileAndExcludesFilesWithNoGitRecord()
    {
        var svg = Charts.RiskQuadrant(RiskFiles());

        Assert.Contains("<svg class=\"risk-quadrant\"", svg);
        Assert.Contains("BigHot.cs", svg); // present in the aria-label + rich tooltip card
        Assert.DoesNotContain("NoGit.cs", svg); // no metrics → not plotted at all
        // One <circle> per metric-bearing file (6), none for the metric-less one.
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(svg, "<circle class=\"risk-point").Count);
    }

    [Fact]
    public void RiskQuadrant_FlagsTheHighSizeHighChurnOutlierAsElevatedWithAShadedQuadrantAndADistinguishingClass()
    {
        var svg = Charts.RiskQuadrant(RiskFiles());

        // The quadrant background is shaded AND labeled...
        Assert.Contains("<rect class=\"risk-quadrant-elevated\"", svg);
        Assert.Contains("Elevated risk", svg);
        // ...and the flagged point ALSO carries a distinguishing class (on top of its gradient level class) —
        // never color/position alone.
        Assert.Matches(new System.Text.RegularExpressions.Regex("class=\"risk-point level-\\d risk-point-elevated"), svg);
        // BigHot.cs is unambiguously the largest+busiest file, so it must be the flagged one.
        var elevatedIndex = svg.IndexOf("risk-point-elevated", StringComparison.Ordinal);
        var bigHotIndex = svg.IndexOf("BigHot.cs", StringComparison.Ordinal);
        Assert.True(Math.Abs(elevatedIndex - bigHotIndex) < 400, "the elevated point should be BigHot.cs's circle");
    }

    [Fact]
    public void RiskQuadrant_PointsCarryAGradientLevelClassIndependentOfTheElevatedFlag()
    {
        // Not every point shares the same combined-position bucket, so the ramp should show more than one level
        // across a spread-out set of files (a pure gradient signal, distinct from the binary elevated flag).
        var svg = Charts.RiskQuadrant(RiskFiles());

        var levels = System.Text.RegularExpressions.Regex.Matches(svg, "risk-point level-(\\d)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.True(levels.Count > 1, "expected more than one gradient level across a spread-out file set");
    }

    [Fact]
    public void RiskQuadrant_LinksAPointOnlyWhenTheResolverReturnsATarget()
    {
        var files = RiskFiles();

        var linked = Charts.RiskQuadrant(files, fileHref: p => p == "src/BigHot.cs" ? "code/src/BigHot.cs.html" : null);
        Assert.Contains("<a class=\"risk-point-link js-tip\" href=\"code/src/BigHot.cs.html\"", linked);

        var plain = Charts.RiskQuadrant(files, fileHref: null);
        Assert.DoesNotContain("<a class=\"risk-point-link", plain);
        Assert.Contains("<circle class=\"risk-point", plain); // tooltip/point still render, never a dead link
        Assert.Contains("tabindex=\"0\"", plain); // unlinked points stay keyboard-focusable
    }

    [Fact]
    public void RiskQuadrant_PointsCarryAnAccessibleLabelAndARichTooltipCard()
    {
        // The native <title> tooltip was replaced with the SAME rich data-tip-html card the treemap's cells use
        // (review pass) — an always-present aria-label carries the plain-text accessible name, and data-tip-html
        // carries the fuller stylized card (served through the shared body-level tooltip, same as the treemap).
        var svg = Charts.RiskQuadrant(RiskFiles());

        Assert.Contains("aria-label=\"src/BigHot.cs, 5,000 lines, 50 changes\"", svg);
        Assert.Contains("data-tip-html=", svg);
        Assert.Contains("js-tip", svg);
        Assert.DoesNotContain("<title>", svg); // replaced, not duplicated
    }

    [Fact]
    public void RiskQuadrant_BelowMinimumFiles_DegradesToChartEmptyRatherThanAnAxisOfOneOrTwoDots()
    {
        var tooFew = CodeMap.Build(
            new[] { ("src/A.cs", 100L), ("src/B.cs", 50L) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 50, null, null),
                ["src/B.cs"] = new CodeFileMetrics(2, 20, null, null),
            }).Files();

        var html = Charts.RiskQuadrant(tooFew);

        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<svg", html);
    }

    [Fact]
    public void RiskQuadrant_ZeroMetricBearingFiles_DegradesToChartEmpty()
    {
        var noGit = CodeMap.Build(
            new[] { ("src/A.cs", 100L), ("src/B.cs", 50L) },
            new Dictionary<string, CodeFileMetrics>()).Files();

        var html = Charts.RiskQuadrant(noGit);

        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<svg", html);
    }

    [Fact]
    public void RiskQuadrant_EscapesPathsInTooltips()
    {
        var files = CodeMap.Build(
            new[]
            {
                ("src/a&b<c>.cs", 100L), ("src/B.cs", 90L), ("src/C.cs", 80L),
                ("src/D.cs", 70L), ("src/E.cs", 60L), ("src/F.cs", 50L),
            },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/a&b<c>.cs"] = new CodeFileMetrics(5, 50, null, null),
                ["src/B.cs"] = new CodeFileMetrics(4, 40, null, null),
                ["src/C.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/D.cs"] = new CodeFileMetrics(2, 20, null, null),
                ["src/E.cs"] = new CodeFileMetrics(1, 10, null, null),
                ["src/F.cs"] = new CodeFileMetrics(1, 10, null, null),
            }).Files();

        var svg = Charts.RiskQuadrant(files);

        Assert.Contains("a&amp;b&lt;c&gt;.cs", svg);
        Assert.DoesNotContain("<c>", svg);
    }

    [Fact]
    public void RiskQuadrant_DeterministicAcrossRepeatedCalls()
    {
        var files = RiskFiles();
        Assert.Equal(Charts.RiskQuadrant(files), Charts.RiskQuadrant(files));
    }

    [Fact]
    public void RiskQuadrant_BothAxesCarryRealUnitTickLabelsAndTheMedianCutoffLinesAreLabeled()
    {
        // Review-pass owner feedback: an unlabeled log-scaled axis + an unlabeled cutoff line both read as
        // arbitrary. RiskFiles(): sizes 100..5,000 lines, changes 1..50 — the raw (un-logged) extremes.
        var svg = Charts.RiskQuadrant(RiskFiles());

        Assert.Contains("class=\"risk-tick-label\"", svg);
        Assert.Contains(">100</text>", svg);    // min lines (X)
        Assert.Contains(">5,000</text>", svg);  // max lines (X)
        Assert.Contains(">1</text>", svg);      // min changes (Y)
        Assert.Contains(">50</text>", svg);     // max changes (Y)

        // The median cutoff lines get their own real-unit label, distinct class from the plain min/max ticks.
        Assert.Contains("class=\"risk-median-tick-label\"", svg);
        Assert.Contains(">median ", svg);
    }

    [Fact]
    public void RiskQuadrant_YAxisIsLogScaledLikeXSoAHeavyTailedChurnDistributionDoesNotCrushEveryPointToTheBaseline()
    {
        // Regression guard for the review-pass fix: Y used to be linear, which — on a real repo where churn is
        // just as heavy-tailed as size — bunched nearly every point against the bottom edge and made the median
        // cutoff line look arbitrary. Two files an order of magnitude apart in raw changes (2 vs 40) should NOT
        // land twenty times further apart vertically; log-scaling compresses that gap.
        var files = CodeMap.Build(
            new[]
            {
                ("src/A.cs", 200L), ("src/B.cs", 190L), ("src/C.cs", 180L),
                ("src/D.cs", 170L), ("src/E.cs", 160L), ("src/F.cs", 150L),
            },
            new Dictionary<string, CodeFileMetrics>
            {
                // A pure doubling series (2, 4, 8, 16, 32, 64) — perfectly even spacing in log space.
                ["src/A.cs"] = new CodeFileMetrics(2, 20, null, null),
                ["src/B.cs"] = new CodeFileMetrics(4, 40, null, null),
                ["src/C.cs"] = new CodeFileMetrics(8, 80, null, null),
                ["src/D.cs"] = new CodeFileMetrics(16, 160, null, null),
                ["src/E.cs"] = new CodeFileMetrics(32, 320, null, null),
                ["src/F.cs"] = new CodeFileMetrics(64, 640, null, null),
            }).Files();

        var svg = Charts.RiskQuadrant(files, height: 420);

        // Extract every <circle cy="..."> — under log scaling these doubling-changes files should land at
        // roughly EVEN vertical spacing (a geometric progression maps to an arithmetic one in log space),
        // not the increasingly-compressed-toward-the-bottom spacing linear scaling would produce.
        var cyValues = System.Text.RegularExpressions.Regex.Matches(svg, "cy=\"([\\d.]+)\"")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .OrderBy(y => y)
            .ToList();
        Assert.Equal(6, cyValues.Count);
        var gaps = cyValues.Zip(cyValues.Skip(1), (a, b) => b - a).ToList();
        // Every consecutive gap should be within a tight band of the average gap — a linear-scale plot of a
        // doubling series would instead show gaps shrinking sharply as changes grow.
        var avgGap = gaps.Average();
        Assert.All(gaps, g => Assert.True(Math.Abs(g - avgGap) < avgGap * 0.35, $"gap {g} strayed too far from the average {avgGap} — Y no longer reads as log-scaled"));
    }

    [Fact]
    public void RiskQuadrantElevatedFiles_ReturnsTheHighSizeHighChurnFilesRankedByChurnDescending()
    {
        var elevated = Charts.RiskQuadrantElevatedFiles(RiskFiles());

        Assert.Single(elevated);
        Assert.Equal("src/BigHot.cs", elevated[0].RepoRelativePath);
    }

    [Fact]
    public void RiskQuadrantElevatedFiles_BelowMinimumFiles_ReturnsEmpty()
    {
        var tooFew = CodeMap.Build(
            new[] { ("src/A.cs", 100L), ("src/B.cs", 50L) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 50, null, null),
                ["src/B.cs"] = new CodeFileMetrics(2, 20, null, null),
            }).Files();

        Assert.Empty(Charts.RiskQuadrantElevatedFiles(tooFew));
    }

    /// <summary>[Review][Patch] The Risk Quadrant's own past-the-cap branch (the Code Map treemap that this
    /// mirrored was retired by Story 20.9; the shared cap discipline it established is what survives):
    /// past <see cref="Charts.MaxDetailedCodeMapFiles"/>, the long tail keeps its point, position, and a real
    /// aria-label (compact-text metrics folded in, AC #4: never color/gradient alone) — only the expensive
    /// <c>data-tip-html</c> card (the same per-node cost that once bloated code-map.html to ~82.5MB) is skipped.</summary>
    [Fact]
    public void RiskQuadrant_AboveTheDetailCap_LongTailPointsLoseTheCard_ButKeepAccessibleName()
    {
        var cap = Charts.MaxDetailedCodeMapFiles;
        var fileCount = cap + 5;
        var files = Enumerable.Range(1, fileCount).Select(i => ($"src/file-{i:00000}.cs", (long)i)).ToArray();
        var metrics = new Dictionary<string, CodeFileMetrics>();
        foreach (var (path, lines) in files)
        {
            metrics[path] = new CodeFileMetrics((int)lines, (int)lines * 10, null, null);
        }
        var map = CodeMap.Build(files, metrics).Files();

        var svg = Charts.RiskQuadrant(map);

        // Every file still gets its own circle with an accessible name (never dropped) — +1 for the whole-chart
        // <svg> aria-label.
        Assert.Equal(fileCount, CountOf(svg, "<circle"));
        Assert.Equal(fileCount + 1, CountOf(svg, "aria-label=\""));
        // …but only the top `cap` most-significant files still pay for the rich hover card.
        Assert.Equal(cap, CountOf(svg, "data-tip-html="));
        Assert.Equal(cap, CountOf(svg, " js-tip"));
    }

    /// <summary>[Review][Patch] Two files sharing the exact same (Lines, Changes) pair previously plotted at the
    /// identical (cx, cy), fully overlapping so only the last-drawn circle stayed mouse/hover-reachable. They now
    /// get a small deterministic jitter so every point's screen position is unique.</summary>
    [Fact]
    public void RiskQuadrant_CoincidentPoints_GetDeterministicJitterSoEveryCircleStaysReachable()
    {
        var files = CodeMap.Build(
            new[]
            {
                ("src/Twin1.cs", 300L), ("src/Twin2.cs", 300L),
                ("src/C.cs", 200L), ("src/D.cs", 150L), ("src/E.cs", 100L), ("src/F.cs", 80L),
            },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/Twin1.cs"] = new CodeFileMetrics(25, 250, null, null),
                ["src/Twin2.cs"] = new CodeFileMetrics(25, 250, null, null),
                ["src/C.cs"] = new CodeFileMetrics(20, 200, null, null),
                ["src/D.cs"] = new CodeFileMetrics(15, 150, null, null),
                ["src/E.cs"] = new CodeFileMetrics(10, 100, null, null),
                ["src/F.cs"] = new CodeFileMetrics(5, 50, null, null),
            }).Files();

        var svg = Charts.RiskQuadrant(files);

        var circles = System.Text.RegularExpressions.Regex.Matches(svg, "<circle[^>]*cx=\"([\\d.]+)\"[^>]*cy=\"([\\d.]+)\"")
            .Select(m => (X: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Y: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .ToList();
        Assert.Equal(6, circles.Count);
        Assert.Equal(6, circles.Distinct().Count()); // no two points share a screen position anymore
    }

    // ---- Code map sunburst (Story 7.12 review — merged shape/dimension toggle) ------------

    /// <summary>A small nested tree (single directory, three files) with a clear busiest file, a clear quiet
    /// file, and one file with no git record at all — enough to exercise change-frequency bucketing, the
    /// <c>level-none</c> no-data case, and directory neutrality in one shared fixture.</summary>
    private static IReadOnlyList<CodeMapNode> CodeMapTreeRoots() => CodeMap.Build(
        new[]
        {
            ("src/dir/Busy.cs", 100L),
            ("src/dir/Quiet.cs", 80L),
            ("src/dir/NoGit.cs", 60L),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/dir/Busy.cs"] = new CodeFileMetrics(20, 50, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 20)),
            ["src/dir/Quiet.cs"] = new CodeFileMetrics(1, 20, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 1)),
            // src/dir/NoGit.cs deliberately has no metrics entry.
        }).Roots;

    private static IEnumerable<CodeMapNode> FlattenCodeMapFiles(CodeMapNode node) =>
        node.IsDirectory ? node.Children.SelectMany(FlattenCodeMapFiles) : new[] { node };

    // ---- Code ownership sunburst (Story 7.11) -----------------------------------------------

    /// <summary>A small nested tree: one file with a clear dominant author (Alice 8/10 -> 80% share), one
    /// evenly-split file (no single dominant share above 50%), and one file with no git record at all — enough
    /// to exercise share-% bucketing, the <c>level-none</c> no-data case, and directory neutrality.</summary>
    private static IReadOnlyList<CodeMapNode> OwnershipRoots() => CodeMap.Build(
        new[]
        {
            ("src/dir/Dominant.cs", 100L),
            ("src/dir/Split.cs", 80L),
            ("src/dir/NoGit.cs", 60L),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/dir/Dominant.cs"] = new CodeFileMetrics(10, 50, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 20),
                Contributors: new[]
                {
                    new FileContributor("Alice", 8, new DateOnly(2026, 7, 20)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 1)),
                }, TotalContributors: 2),
            ["src/dir/Split.cs"] = new CodeFileMetrics(4, 20, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1),
                Contributors: new[]
                {
                    new FileContributor("Alice", 2, new DateOnly(2026, 6, 1)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 5, 1)),
                }, TotalContributors: 2),
            // src/dir/NoGit.cs deliberately has no metrics entry.
        }).Roots;

    // ---- Code ownership treemap (Story 7.11 — sunburst/treemap toggle, owner feedback) -----

    private static IReadOnlyList<TreemapRect> OwnershipLayout() => CodeMap.Build(
        new[]
        {
            ("src/dir/Dominant.cs", 100L),
            ("src/dir/Split.cs", 80L),
            ("src/dir/NoGit.cs", 60L),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/dir/Dominant.cs"] = new CodeFileMetrics(10, 50, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 20),
                Contributors: new[]
                {
                    new FileContributor("Alice", 8, new DateOnly(2026, 7, 20)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 1)),
                }, TotalContributors: 2),
            ["src/dir/Split.cs"] = new CodeFileMetrics(4, 20, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1),
                Contributors: new[]
                {
                    new FileContributor("Alice", 2, new DateOnly(2026, 6, 1)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 5, 1)),
                }, TotalContributors: 2),
        }).Layout();

    [Fact]
    public void OwnershipLegend_CarriesRealValuePercentRangesNeverLessOrMore()
    {
        var files = OwnershipRoots().SelectMany(FlattenCodeMapFiles).ToList();

        var legend = Charts.OwnershipLegend(files);

        Assert.DoesNotContain("Less", legend);
        Assert.DoesNotContain("More", legend);
        Assert.Contains("76–100%", legend);
        Assert.Contains("No git history", legend); // NoGit.cs has no contributor data — the trailing swatch note
    }

    [Fact]
    public void OwnershipLegend_NoMetricBearingFiles_DegradesToAPlainNoteRatherThanAMeaninglessRamp()
    {
        var files = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>()).Files();

        var legend = Charts.OwnershipLegend(files);

        Assert.Contains("unavailable", legend);
        Assert.DoesNotContain("ownership-legend-swatch level-4", legend);
    }

    // ==================== Story 3.7: requirement status-block grid + requirements flow ====================

    private static RequirementInfo Req(
        RequirementKind kind, int number, RequirementStatus status, bool deferred = false, params int[] epics) => new()
    {
        Kind = kind,
        Number = number,
        TextHtml = $"Requirement {number}",
        Status = status,
        Deferred = deferred,
        CoverageEpicNumber = epics.Length > 0 ? epics[0] : null,
        CoverageEpicNumbers = epics,
    };

    [Fact]
    public void RequirementStatusGrid_EmitsOneTilePerRequirement_ThreeRedundantChannels()
    {
        var reqs = new[]
        {
            Req(RequirementKind.Functional, 1, RequirementStatus.Done, false, 1),
            Req(RequirementKind.Functional, 2, RequirementStatus.Active, false, 1, 2),
            Req(RequirementKind.NonFunctional, 7, RequirementStatus.Deferred, deferred: true),
        };

        var html = Charts.RequirementStatusGrid(reqs, prefix: string.Empty);

        // One tile per requirement — the rich js-tip class + correct status class + link to the detail page...
        Assert.Contains("<a class=\"req-status-block js-tip done\" href=\"requirements/fr1.html\"", html);
        Assert.Contains("<a class=\"req-status-block js-tip active\" href=\"requirements/fr2.html\"", html);
        Assert.Contains("<a class=\"req-status-block js-tip deferred\" href=\"requirements/nfr7.html\"", html);
        // ...the id as visible text (the non-colour reading)...
        Assert.Contains("<span class=\"req-block-id\">FR1</span>", html);
        Assert.Contains("<span class=\"req-block-id\">NFR7</span>", html);
        // ...a kind icon (FR vs NFR — the shape channel)...
        Assert.Contains("<span class=\"req-block-icon\">", html);
        // ...the status word in the plain-title fallback AND the multi-line rich tooltip (never colour-only).
        Assert.Contains("title=\"FR1 — Done\"", html);
        Assert.Contains("title=\"FR2 — Partially implemented\"", html);
        Assert.Contains("data-tip=\"NFR7", html);
        Assert.Contains("Deferred\nRequirement 7", html); // rich tip carries status word + definition snippet
    }

    [Fact]
    public void RequirementStatusGrid_PrefixesHrefsAndEscapes()
    {
        var reqs = new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Planned, false, 1) };
        var html = Charts.RequirementStatusGrid(reqs, prefix: "../");
        Assert.Contains("href=\"../requirements/fr1.html\"", html);
    }

    [Fact]
    public void RequirementStatusGrid_EmptyList_RendersNothing()
        => Assert.Equal(string.Empty, Charts.RequirementStatusGrid(Array.Empty<RequirementInfo>(), prefix: string.Empty));

    [Fact]
    public void RequirementStatusGrid_SingleRequirement_RendersOneCoherentBlock()
    {
        var html = Charts.RequirementStatusGrid(
            new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Done, false, 1) }, prefix: string.Empty);
        Assert.Contains("req-status-grid", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "req-status-block"));
    }

    // ---- Requirements flow (Sankey) ----

    private static (RequirementsModel Reqs, EpicsModel Epics) FlowFixture()
    {
        const string md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            **Core**
            FR1: Done requirement
            FR2: Multi-epic requirement
            FR3: Deferred requirement
            FR4: Unmapped requirement

            ### NonFunctional Requirements

            NFR1: A non-functional one

            ### FR Coverage Map

            FR1: Epic 1 - done
            FR2: Epics 1 & 2 - spans two
            FR3: Deferred - later
            FR4: covered but no epic number

            ## Epic List

            ### Epic 1: Foundation

            Base.

            ### Epic 2: Expansion

            More.

            ## Epic 1: Foundation

            ### Story 1.1: Scaffold

            As a dev, I want scaffolding.

            ## Epic 2: Expansion

            ### Story 2.1: Widen

            As a dev, I want more.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        return (RequirementsParser.Parse(md, epics, progress), epics);
    }

    [Fact]
    public void RequirementFlow_CarriesRoleImgAndAriaSummary()
    {
        var (reqs, epics) = FlowFixture();
        var svg = Charts.RequirementFlow(reqs, epics);

        Assert.Contains("role=\"img\"", svg);
        Assert.Contains("aria-label=\"", svg);
        // The aria summary names the FULL requirement total (FR + NFR = 5), not just the functional ones.
        Assert.Contains("5 requirements", svg);
    }

    [Fact]
    public void RequirementFlow_IncludesNfrs()
    {
        // The flow spans ALL requirements now — NFR1 (uncovered) must appear, routed to "No coverage".
        var (reqs, epics) = FlowFixture();
        var svg = Charts.RequirementFlow(reqs, epics);
        // The aria total (5) already proves the NFR is counted; the "No coverage" node is where it lands.
        Assert.Contains("with no coverage", svg);
    }

    [Fact]
    public void RequirementFlow_DeferredUnmappedAndNfrsLandInNoCoverageNode_NotDropped()
    {
        var (reqs, epics) = FlowFixture();
        var svg = Charts.RequirementFlow(reqs, epics);

        // The explicit honest node — deferred FRs, unmapped FRs, and uncovered NFRs terminate here, never vanish.
        Assert.Contains("No coverage", svg);
    }

    [Fact]
    public void RequirementFlow_SplitsMultiEpicRequirementAcrossItsEpics()
    {
        // FR2 is covered by Epics 1 & 2, so BOTH epic nodes must render (the split makes the second visible),
        // and the shared-count note appears on the node tooltip. [multi-epic split]
        var (reqs, epics) = FlowFixture();
        var svg = Charts.RequirementFlow(reqs, epics);
        Assert.Contains(">Epic 1</text>", svg);
        Assert.Contains(">Epic 2</text>", svg);
        Assert.Contains("shared with other epics", svg);
    }

    [Fact]
    public void RequirementFlow_ConservesEveryRequirement_NothingLostOrDoubleCounted()
    {
        var (reqs, epics) = FlowFixture();

        // Conservation is asserted through the public conservation helper the builder uses: the count of ALL
        // requirements entering "definition" equals the sum reaching the terminal implementation-state buckets.
        var (entering, byState) = Charts.RequirementFlowConservation(reqs.All.ToList());
        Assert.Equal(reqs.All.Count(), entering);
        Assert.Equal(entering, byState.Values.Sum());
    }

    [Fact]
    public void RequirementFlowConservation_UnmappedIsItsOwnBucket_SeparateFromPlanned()
    {
        // FlowFixture: FR3 deferred, FR4 unmapped, NFR1 uncovered (→ unmapped). The unmapped bucket must be
        // counted separately from planned/pending, and the deferred bucket separately again — the split AC #2
        // requires the flow to carry. Conservation still holds across the 6 buckets. [Story 9.3 Task 3]
        var (reqs, _) = FlowFixture();
        var (entering, byState) = Charts.RequirementFlowConservation(reqs.All.ToList());

        Assert.True(byState.ContainsKey("unmapped"));
        Assert.True(byState.ContainsKey("deferred"));
        Assert.True(byState.ContainsKey("pending"));
        // FR4 (unmapped FR) + NFR1 (uncovered NFR) land in unmapped; FR3 in deferred — never merged.
        Assert.Equal(2, byState["unmapped"]);
        Assert.Equal(1, byState["deferred"]);
        Assert.Equal(entering, byState.Values.Sum());
    }

    [Fact]
    public void RequirementFlowConservation_RetiredIsItsOwnBucket_SeparateFromDeferred()
    {
        // Story 8.9 review: a requirement covered solely by an all-retired epic must land in its own "retired"
        // Sankey bucket, not silently merge into "deferred" (same reasoning as Unmapped vs Planned, Story 9.3).
        var dir = Directory.CreateTempSubdirectory("ss-flow-retired-").FullName;
        try
        {
            var md = """
                # Epics

                ## Requirements Inventory

                ### Functional Requirements

                **Core**
                FR1: Covered by a retired epic
                FR2: Deferred requirement

                ### FR Coverage Map

                FR1: Epic 1
                FR2: Deferred - later

                ## Epic List

                ### Epic 1: Abandoned

                Goal.

                ## Epic 1: Abandoned

                ### Story 1.1: Retired story

                As a dev, I want a, so that b.
                """;
            var artifact = Path.Combine(dir, "1-1.md");
            File.WriteAllText(artifact, "# Story 1.1\nStatus: retired\n\n## Tasks / Subtasks\n\n- [ ] a\n");
            var epics = EpicsParser.Parse(md);
            var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);
            var reqs = RequirementsParser.Parse(md, epics, progress);
            Assert.Equal(RequirementStatus.Retired, reqs.ById["FR1"].Status);

            var (entering, byState) = Charts.RequirementFlowConservation(reqs.All.ToList());

            Assert.True(byState.ContainsKey("retired"));
            Assert.True(byState.ContainsKey("deferred"));
            Assert.Equal(1, byState["retired"]);
            Assert.Equal(1, byState["deferred"]);
            Assert.Equal(entering, byState.Values.Sum());

            var svg = Charts.RequirementFlow(reqs, epics);
            Assert.Contains("req-flow-state retired", svg);
            Assert.Contains(">Retired (1)</text>", svg);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RequirementFlow_RendersUnmappedAndDeferredAsTwoDistinctStateNodes()
    {
        var (reqs, epics) = FlowFixture();
        var svg = Charts.RequirementFlow(reqs, epics);

        // Two separate, separately-labeled terminal state nodes — not one merged "pending" node.
        Assert.Contains("req-flow-state unmapped", svg);
        Assert.Contains("req-flow-state deferred", svg);
        Assert.Contains("Not yet mapped (", svg);
        Assert.Contains("Deferred (", svg);
        // The aria text twin reports the unmapped count on its own (AC #2 accessibility twin).
        Assert.Contains("not yet mapped", svg);
    }

    [Fact]
    public void RequirementFlow_EmptyFunctional_ReturnsChartEmptyPlaceholder()
    {
        var epics = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        var reqs = new RequirementsModel { Functional = Array.Empty<RequirementInfo>(), NonFunctional = Array.Empty<RequirementInfo>(), Design = Array.Empty<RequirementInfo>() };

        var svg = Charts.RequirementFlow(reqs, epics);
        Assert.Contains("chart-empty", svg);
    }

    [Fact]
    public void RequirementFlow_SingleFunctional_RendersWithoutNaN()
    {
        var epics = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        var reqs = new RequirementsModel
        {
            Functional = new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Deferred, deferred: true) },
            NonFunctional = Array.Empty<RequirementInfo>(),
            Design = Array.Empty<RequirementInfo>(),
        };

        var svg = Charts.RequirementFlow(reqs, epics);
        Assert.DoesNotContain("NaN", svg);
        Assert.Contains("role=\"img\"", svg);
    }

    [Fact]
    public void RequirementFlow_LargeRequirementCount_GrowsCanvasInsteadOfOverflowing()
    {
        // 200 requirements against a single epic hits unitH's 2px floor at the default usableH=320/gap=14 geometry
        // (~150+ threshold) — the SVG height must grow to fit, not stay pinned at the small-project constant.
        // [Story 3.7 follow-up]
        var epics = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        var many = Enumerable.Range(1, 200)
            .Select(i => Req(RequirementKind.Functional, i, RequirementStatus.Done, false, 1))
            .ToArray();
        var reqs = new RequirementsModel { Functional = many, NonFunctional = Array.Empty<RequirementInfo>(), Design = Array.Empty<RequirementInfo>() };

        var svg = Charts.RequirementFlow(reqs, epics);
        Assert.DoesNotContain("NaN", svg);

        var heightMatch = System.Text.RegularExpressions.Regex.Match(svg, "height=\"([\\d.]+)\"");
        Assert.True(heightMatch.Success);
        var height = double.Parse(heightMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        // Small-project height (26 reqs) stays at the 320-tall default; 200 reqs must exceed it.
        var smallSvg = Charts.RequirementFlow(FlowFixture().Reqs, FlowFixture().Epics);
        var smallHeightMatch = System.Text.RegularExpressions.Regex.Match(smallSvg, "height=\"([\\d.]+)\"");
        var smallHeight = double.Parse(smallHeightMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        Assert.True(height > smallHeight, $"expected large-N height ({height}) > small-N height ({smallHeight})");
    }

    [Fact]
    public void RequirementFlowTextEquivalent_ListsPerEpicPerStatusBreakdown_AsSrOnly()
    {
        var (reqs, epics) = FlowFixture();
        var html = Charts.RequirementFlowTextEquivalent(reqs, epics);

        Assert.Contains("class=\"req-flow-breakdown sr-only\"", html);
        // FR1 done under Epic 1; FR2 splits Epics 1 & 2; FR3 deferred / FR4 unmapped / NFR1 uncovered → No coverage.
        // Epic titles ("Foundation"/"Expansion") must appear too — the same naming a sighted user gets hovering
        // RequirementFlow's epic-node tooltip, not just the bare number.
        Assert.Contains("Epic 1 (Foundation): 2 requirements", html);
        Assert.Contains("Epic 2 (Expansion): 1 requirement", html);
        Assert.Contains("No coverage: 3 requirements", html);
    }

    [Fact]
    public void RequirementFlowTextEquivalent_EmptyRequirements_ReturnsEmptyString()
    {
        var epics = new EpicsModel { OverviewHtml = "", RequirementsInventoryHtml = "", Epics = Array.Empty<EpicInfo>() };
        var reqs = new RequirementsModel { Functional = Array.Empty<RequirementInfo>(), NonFunctional = Array.Empty<RequirementInfo>(), Design = Array.Empty<RequirementInfo>() };

        Assert.Equal(string.Empty, Charts.RequirementFlowTextEquivalent(reqs, epics));
    }

    // ---- Story 7.8: ReferenceGraph second (related-file) population ----

    private static readonly (string Href, string Title, string Short)[] TwoArtifacts =
    {
        ("epics/story-7-1.html", "Story 7.1: In-Portal Code File Browsing", "Story 7.1"),
        ("epics/epic-8.html", "Epic 8: Dashboard Command Center", "Epic 8"),
    };

    [Fact]
    public void ReferenceGraph_TwoPopulations_RenderDistinctShapesAndEdges()
    {
        var related = new (string?, string, string, int)[]
        {
            ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7),
        };

        var svg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related);

        // Artifact half unchanged: gold circle nodes on solid edges.
        Assert.Contains("class=\"ref-dot\"", svg);
        Assert.Contains("class=\"ref-edge\"", svg);
        // Related half: neutral diamond (polygon) nodes on DASHED edges — distinct by shape AND edge, not colour.
        Assert.Contains("class=\"ref-file-dot\"", svg);
        Assert.Contains("<polygon class=\"ref-file-dot\"", svg);
        Assert.Contains("class=\"ref-edge-file\"", svg);
    }

    [Fact]
    public void ReferenceGraph_RelatedNode_LinkedWhenHrefPresentChipWhenNull()
    {
        var related = new (string?, string, string, int)[]
        {
            ("../code/src/Linked.cs.html", "src/Linked.cs", "Linked.cs", 3),
            (null, "src/Unlinked.cs", "Unlinked.cs", 2),
        };

        var svg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related);

        // Href present → an <a> node; href null → a non-link <g> chip. Never a dead link.
        Assert.Contains("<a class=\"ref-file-node\" href=\"../code/src/Linked.cs.html\"", svg);
        Assert.Contains("class=\"ref-file-node ref-file-node--chip\"", svg);
        Assert.DoesNotContain("href=\"\"", svg);
    }

    [Fact]
    public void ReferenceGraph_RelatedNode_TooltipCarriesFullPathAndCoChangeStrength()
    {
        var related = new (string?, string, string, int)[]
        {
            ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7),
            (null, "src/Once.cs", "Once.cs", 1),
        };

        var svg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related);

        Assert.Contains("<title>src/Other.cs — changed together 7 times</title>", svg);
        // Singular co-change wording ("1 time", not "1 times").
        Assert.Contains("<title>src/Once.cs — changed together 1 time</title>", svg);
        // The aria summary reflects both populations.
        Assert.Contains("and changes alongside 2 files", svg);
    }

    [Fact]
    public void ReferenceGraph_EmptyRelated_ByteIdenticalToSinglePopulationCall()
    {
        // Passing an empty related list must reproduce the pre-7.8 single-population SVG exactly (additive overload +
        // null-insight degradation). Same for passing null.
        var singleArg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts);
        var emptyRelated = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, Array.Empty<(string?, string, string, int)>());
        var nullRelated = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, null);

        Assert.Equal(singleArg, emptyRelated);
        Assert.Equal(singleArg, nullRelated);
        Assert.DoesNotContain("ref-file-dot", singleArg);
        Assert.DoesNotContain("ref-edge-file", singleArg);
    }

    [Fact]
    public void ReferenceGraph_ArtifactRingCapped_OverflowSurfacedNotDropped()
    {
        // More citing artifacts than the cap → only the cap's worth of ring nodes are drawn, but the summary
        // aria-label reflects the TRUE total and an on-graph "+N more" marker is emitted (nothing silently dropped).
        var many = new List<(string Href, string Title, string Short)>();
        for (var i = 0; i < 20; i++)
        {
            many.Add(($"epics/a{i}.html", $"Artifact {i}", $"A{i}"));
        }

        var svg = Charts.ReferenceGraph("Sample.cs", many);

        // Only the cap (14) circles drawn.
        Assert.Equal(Charts.RefGraphArtifactNodeCap, CountOccurrences(svg, "class=\"ref-dot\""));
        // True total in the summary + an honest overflow marker for the remaining 6.
        Assert.Contains("is referenced by 20 artifacts", svg);
        Assert.Contains("(14 shown)", svg);
        Assert.Contains("class=\"ref-overflow\"", svg);
        Assert.Contains("+6 more artifacts", svg);
    }

    [Fact]
    public void ReferenceGraph_RelatedNode_EscapesMetacharacters()
    {
        var related = new (string?, string, string, int)[]
        {
            ("../code/x.html", "src/<x>&\".cs", "<x>&\".cs", 2),
        };

        var svg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related);

        Assert.Contains("src/&lt;x&gt;&amp;&quot;.cs", svg);
        Assert.DoesNotContain("<x>&\".cs</text>", svg);
    }

    // ---- reference-graph epic grouping + relationships ----

    [Fact]
    public void ReferenceGraph_GroupByEpicOff_ByteIdenticalToStory78Output()
    {
        // Both toggles off (groupByEpic false, no refEpics/crossEdges/relatedEdges passed at all) must reproduce
        // the pre-existing Story 7.8 call exactly — AC "byte-identical to pre-existing Story 7.8 output".
        var related = new (string?, string, string, int)[] { ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7) };
        var story78 = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related);
        var flatFlat = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related, groupByEpic: false, refEpics: null, crossEdges: null, relatedEdges: null);

        Assert.Equal(story78, flatFlat);
    }

    [Fact]
    public void ReferenceGraph_GroupByEpic_TwoEpics_NestsStoriesUnderTwoDistinctHubs()
    {
        var refs = new (string Href, string Title, string Short)[]
        {
            ("epics/story-1-1.html", "Story 1.1: Alpha", "Story 1.1"),
            ("epics/story-1-2.html", "Story 1.2: Beta", "Story 1.2"),
            ("epics/story-2-1.html", "Story 2.1: Gamma", "Story 2.1"),
        };
        var refEpics = new (int EpicNumber, string EpicTitle)?[]
        {
            (1, "Foundation"),
            (1, "Foundation"),
            (2, "Growth"),
        };

        var svg = Charts.ReferenceGraph("Sample.cs", refs, 0, null, refEpics: refEpics, groupByEpic: true);

        // Exactly two hub nodes (one per distinct epic), even though three stories cite the file.
        Assert.Equal(2, CountOccurrences(svg, "<g class=\"ref-epic-hub\""));
        Assert.Contains(">Epic 1</text>", svg);
        Assert.Contains(">Epic 2</text>", svg);
        // All three story nodes still render as ordinary gold artifact nodes (shape/colour unchanged).
        Assert.Equal(3, CountOccurrences(svg, "class=\"ref-dot\""));
        // Hub->story spokes exist (nesting), distinct from the file->hub spokes.
        Assert.Contains("class=\"ref-hub-spoke\"", svg);
    }

    [Fact]
    public void ReferenceGraph_GroupByEpic_NonStoryCiterStaysAtTopLevel()
    {
        var refs = new (string Href, string Title, string Short)[]
        {
            ("epics/story-1-1.html", "Story 1.1: Alpha", "Story 1.1"),
            ("adrs/0005.html", "ADR 0005: Delivery architecture", "ADR 0005"),
        };
        var refEpics = new (int EpicNumber, string EpicTitle)?[] { (1, "Foundation"), null };

        var svg = Charts.ReferenceGraph("Sample.cs", refs, 0, null, refEpics: refEpics, groupByEpic: true);

        // One hub (for the story) — the ADR never gets a hub or a hub-spoke, it keeps a direct file->node spoke.
        Assert.Equal(1, CountOccurrences(svg, "<g class=\"ref-epic-hub\""));
        Assert.Contains(">ADR 0005</text>", svg);
        Assert.Equal(2, CountOccurrences(svg, "class=\"ref-dot\""));
    }

    [Fact]
    public void ReferenceGraph_ShowRelationships_StoryToRelatedFileEdgeDrawn()
    {
        var related = new (string?, string, string, int)[] { ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7) };

        var svg = Charts.ReferenceGraph(
            "Sample.cs", TwoArtifacts, 0, related,
            crossEdges: new[] { (RefIndex: 0, RelatedIndex: 0) });

        Assert.Contains("class=\"ref-edge-cross\"", svg);
    }

    [Fact]
    public void ReferenceGraph_ShowRelationships_RelatedToRelatedEdgeDrawn()
    {
        var related = new (string?, string, string, int)[]
        {
            ("../code/src/A.cs.html", "src/A.cs", "A.cs", 5),
            ("../code/src/B.cs.html", "src/B.cs", "B.cs", 4),
        };

        var svg = Charts.ReferenceGraph(
            "Sample.cs", TwoArtifacts, 0, related,
            relatedEdges: new[] { (RelatedIndexA: 0, RelatedIndexB: 1) });

        Assert.Contains("class=\"ref-edge-cross\"", svg);
    }

    [Fact]
    public void ReferenceGraph_ShowRelationships_NoOverlaps_NoCrossEdgesRendered()
    {
        var related = new (string?, string, string, int)[] { ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7) };

        // No cross-edge data supplied at all (the "no overlaps found" case) — identical to the toggle-off render.
        var svg = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, related, crossEdges: null, relatedEdges: null);

        Assert.DoesNotContain("ref-edge-cross", svg);
    }

    [Fact]
    public void ReferenceGraph_CrossEdges_OutOfRangeIndicesAreIgnoredNotThrown()
    {
        var related = new (string?, string, string, int)[] { ("../code/src/Other.cs.html", "src/Other.cs", "Other.cs", 7) };

        // Stale/out-of-bounds indices (defensive: never throw on a bad index).
        var svg = Charts.ReferenceGraph(
            "Sample.cs", TwoArtifacts, 0, related,
            crossEdges: new[] { (RefIndex: 99, RelatedIndex: 0) },
            relatedEdges: new[] { (RelatedIndexA: 0, RelatedIndexB: 0) }); // self-pair also ignored

        Assert.DoesNotContain("ref-edge-cross", svg);
    }

    [Fact]
    public void ReferenceGraph_GroupByEpic_ArtifactCapAppliesBeforeBucketingAndBoundsHubMembership()
    {
        // Cap-interaction rule (documented in Charts.ReferenceGraph): the global RefGraphArtifactNodeCap applies to
        // the FLAT citer list BEFORE epic bucketing, so a hub's member count can never exceed the cap regardless of
        // how many same-epic citers exist upstream.
        var refs = new List<(string Href, string Title, string Short)>();
        var refEpics = new List<(int EpicNumber, string EpicTitle)?>();
        for (var i = 0; i < 20; i++)
        {
            refs.Add(($"epics/story-1-{i}.html", $"Story 1.{i}", $"Story 1.{i}"));
            refEpics.Add((1, "Foundation"));
        }

        var svg = Charts.ReferenceGraph("Sample.cs", refs, 0, null, refEpics: refEpics, groupByEpic: true);

        // Exactly the cap's worth of story nodes drawn (all under the single hub), true total honestly disclosed.
        Assert.Equal(Charts.RefGraphArtifactNodeCap, CountOccurrences(svg, "class=\"ref-dot\""));
        Assert.Equal(1, CountOccurrences(svg, "<g class=\"ref-epic-hub\""));
        Assert.Contains("is referenced by 20 artifacts", svg);
        Assert.Contains("class=\"ref-overflow\"", svg);
    }

    [Fact]
    public void ReferenceGraph_NoDeepGitData_BothTogglesRenderNoVisualChange()
    {
        // "--deep-git off / no FileInsight" degradation: refEpics null and no cross-edge data at all → every
        // combination of groupByEpic/crossEdges/relatedEdges collapses to the SAME flat, edge-free graph.
        var flatOff = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, null, groupByEpic: false, refEpics: null, crossEdges: null, relatedEdges: null);
        var epicOnNoData = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, null, groupByEpic: true, refEpics: null, crossEdges: null, relatedEdges: null);
        var relOnNoData = Charts.ReferenceGraph("Sample.cs", TwoArtifacts, 0, null, groupByEpic: false, refEpics: null, crossEdges: Array.Empty<(int, int)>(), relatedEdges: Array.Empty<(int, int)>());

        Assert.Equal(flatOff, epicOnNoData);
        Assert.Equal(flatOff, relOnNoData);
        Assert.DoesNotContain("ref-epic-hub", flatOff);
        Assert.DoesNotContain("ref-edge-cross", flatOff);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    [Fact]
    public void Sunburst_NoTaskArcs_TaskWeightedStories()
    {
        // I/O matrix: Story with TasksTotal=12 beside TasksTotal=0 peer — larger story takes ~12x angular
        // weight vs max(1,0)=1 for empty; no task fringe renders. [spec-sunburst-remaining-work-hierarchy]
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Big story", "active", 6, 12), Story("1.2", "Small story", "ready", 0, 0)) },
        };

        var svg = Glance(model);

        Assert.Contains("sb-noplan", svg);
        Assert.Contains("Story 1.1: Big story", svg);
        Assert.Contains("Story 1.2: Small story", svg);
    }

    [Fact]
    public void Sunburst_StoryChildDeferred_AggregatesUnderEpic()
    {
        // Project glance: story-child deferred collapses into epic open/done aggregates.
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "active", 2, 4)) },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Story-child deferred item from code review.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Equal("1.1", geometry.StoryChildDeferred(1, "1.1")[0].SourceStoryId);

        var svg = Glance(model, followUps: geometry);

        Assert.Contains("Epic 1: 1 open follow-up", svg);
        Assert.DoesNotContain("Deferred item: Story-child deferred item from code review.", svg);
    }

    [Fact]
    public void Sunburst_RetroActionItems_InEpicOpenAggregate()
    {
        // Project glance: retro action with epic:N contributes to epic open aggregate (not a middle peer).
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Do the thing", "active", 1, 2)) },
        };
        var items = new[]
        {
            new SprintActionItem("Retro action", "open", EpicNumber: 1, Owner: "Alice"),
        };
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = null,
        };
        var counts = ProjectCounts.Empty with { OpenActionItems = 1 };
        var geometry = FollowUpGeometry.From(items, counts, work, epics: model);

        var svg = Glance(model, followUps: geometry);

        Assert.Contains("Epic 1: 1 open follow-up", svg);
        Assert.DoesNotContain("Action item: Retro action\"", svg);
    }

    [Fact]
    public void EpicSunburst_StoryChildDeferred_InOuterRing()
    {
        // Same nesting rules apply on the epic detail sunburst. [spec-sunburst-remaining-work-hierarchy]
        var epic = Epic(Story("1.1", "Story A", "active", 2, 4));
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Story-child deferred for epic sunburst.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epic },
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        var svg = EpicGlance(epic, _ => "epics/epic-1.html", followUps: geometry);

        Assert.Contains("Deferred item: Story-child deferred for epic sunburst.", svg);
    }

    [Fact]
    public void Sunburst_EmptyDeferredChildren_NoOuterFringe()
    {
        // NFR8: when a story has no nested deferred, no outer fringe on that sweep.
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Clean story", "done", 3, 3)) },
        };

        var svg = Glance(model);

        Assert.DoesNotContain("sb-followup-open", svg);
        Assert.DoesNotContain("Deferred item", svg);
        // Hint must not claim an outer aggregate ring when none exists.
    }

    [Fact]
    public void EpicSunburst_TaskWeighting_LargerStoryTakesMoreAngularSpace()
    {
        // Same hierarchy rules as project sunburst — weight by max(1, TasksTotal).
        var epic = Epic(
            Story("1.1", "Big", "active", 6, 12),
            Story("1.2", "Tiny", "ready", 0, 0));

        var svg = EpicGlance(epic, _ => "epics/epic-1.html");

        Assert.Contains("\"colorClass\":\"sb-seg sb-noplan\"", svg);
        Assert.Contains("Story 1.1: Big", svg);
        Assert.Contains("Story 1.2: Tiny", svg);
    }

    [Fact]
    public void FollowUpGeometry_UnknownSourceStoryId_FallsToEpicLevel()
    {
        // Bad / unknown SourceStoryId → epic-level peer, not a vanishing story child.
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Known", "active", 1, 2)) },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-99-ghost.md (2026-07-15)

            - Ghost parent deferred item.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 1 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        var knownIds = model.Epics[0].Stories.Select(s => s.Id);
        Assert.Empty(geometry.StoryChildDeferred(1, "1.1"));
        Assert.Single(geometry.EpicLevelDeferred(1, knownIds));
    }

    [Fact]
    public void EpicSunburst_StoryChildDeferred_GrowsParentStorySweep()
    {
        // Crowded thin story (TasksTotal=1, 6 nested deferred) must out-sweep equal-task peer with none.
        var epic = Epic(
            Story("1.1", "Crowded", "active", 0, 1),
            Story("1.2", "Thin peer", "ready", 0, 1));
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Nested deferred one.
            - Nested deferred two.
            - Nested deferred three.
            - Nested deferred four.
            - Nested deferred five.
            - Nested deferred six.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 6),
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epic },
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 6 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Equal(6, geometry.StoryChildDeferred(1, "1.1").Count);

        var svg = EpicGlance(epic, _ => "epics/epic-1.html", followUps: geometry);
        var crowded = OuterArcSweepRadians(svg, "Story 1.1: Crowded");
        var peer = OuterArcSweepRadians(svg, "Story 1.2: Thin peer");
        Assert.True(crowded > peer * 5,
            $"Crowded sweep {crowded:F3} should be ~7× peer {peer:F3} (weight 7 vs 1).");
        Assert.Contains("Deferred item: Nested deferred one.", svg);
        // Outer children share the grown parent: one nested wedge ≫ the thin peer story wedge.
        var child = OuterArcSweepRadians(svg, "Deferred item: Nested deferred one.");
        Assert.True(child > peer * 0.7,
            $"Child sweep {child:F3} should be roughly peer/1-scale of crowded/6, larger than peer/7.");
    }

    [Fact]
    public void Sunburst_StoryChildDeferred_GrowsStoryAndEpicWeight()
    {
        // Project glance: nested count still grows story/epic weight even though leaves aggregate.
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                Epic(
                    Story("1.1", "Crowded", "active", 0, 1),
                    Story("1.2", "Thin peer", "ready", 0, 1)),
            },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Nested deferred one.
            - Nested deferred two.
            - Nested deferred three.
            - Nested deferred four.
            - Nested deferred five.
            - Nested deferred six.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 6),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 6 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        var svg = Glance(model, followUps: geometry);
        var crowded = OuterArcSweepRadians(svg, "Story 1.1: Crowded");
        var peer = OuterArcSweepRadians(svg, "Story 1.2: Thin peer");
        Assert.True(crowded > peer * 5,
            $"Crowded sweep {crowded:F3} should be ~7× peer {peer:F3} on project glance.");
        Assert.DoesNotContain("Deferred item: Nested deferred one.", svg);
        Assert.Contains("Epic 1: 6 open follow-ups", svg);
    }

    [Fact]
    public void Sunburst_EpicLevelPeers_GrowGlanceEpicWeight()
    {
        // Follow-up-heavy / task-light epic must out-sweep a same-task peer with no epic-level peers.
        // [spec-9-13-deferred-glance-weight-noplan-sourcekey]
        var epicHeavy = new EpicInfo
        {
            Number = 1,
            Title = "Heavy Follow-ups",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("1.1", "Thin", "active", 0, 1) },
        };
        var epicLight = new EpicInfo
        {
            Number = 2,
            Title = "Task Only",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Also thin", "ready", 0, 1, epicNumber: 2) },
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epicHeavy, epicLight },
        };
        var items = Enumerable.Range(1, 6)
            .Select(i => new SprintActionItem($"Peer action {i}", "open", EpicNumber: 1, Owner: null))
            .ToArray();
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = null,
        };
        var counts = ProjectCounts.Empty with { OpenActionItems = 6 };
        var geometry = FollowUpGeometry.From(items, counts, work, epics: model);

        var svg = Glance(model, followUps: geometry);
        var heavy = OuterArcSweepRadians(svg, "Epic 1:");
        var light = OuterArcSweepRadians(svg, "Epic 2:");
        Assert.True(heavy > light * 5,
            $"Follow-up-heavy epic sweep {heavy:F3} should be ~7× peer {light:F3} (weight 7 vs 1).");
        Assert.Contains("Epic 1: 6 open follow-ups", svg);
    }

    [Fact]
    public void Sunburst_EpicLevelDeferred_GrowsGlanceEpicWeight()
    {
        // Ghost 1-99 deferred peers inflate glance epic weight the same way action peers do.
        // [spec-9-13-deferred-glance-weight-noplan-sourcekey]
        var epicHeavy = new EpicInfo
        {
            Number = 1,
            Title = "Heavy Deferred",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("1.1", "Thin", "active", 0, 1) },
        };
        var epicLight = new EpicInfo
        {
            Number = 2,
            Title = "Task Only",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Also thin", "ready", 0, 1, epicNumber: 2) },
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { epicHeavy, epicLight },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-99-ghost.md (2026-07-15)

            - Ghost deferred one.
            - Ghost deferred two.
            - Ghost deferred three.
            - Ghost deferred four.
            - Ghost deferred five.
            - Ghost deferred six.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 6),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 6 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Equal(6, geometry.EpicLevelDeferred(1, new[] { "1.1" }).Count);
        Assert.Empty(geometry.StoryChildDeferred(1, "1.1"));

        var svg = Glance(model, followUps: geometry);
        var heavy = OuterArcSweepRadians(svg, "Epic 1:");
        var light = OuterArcSweepRadians(svg, "Epic 2:");
        Assert.True(heavy > light * 5,
            $"Epic-level deferred epic sweep {heavy:F3} should be ~7× peer {light:F3} (weight 7 vs 1).");
        Assert.Contains("Epic 1: 6 open follow-ups", svg);
    }

    [Fact]
    public void Sunburst_StoryChildDeferred_NotDoubleCountedInEpicWeight()
    {
        // Nested story-child already in StoryWeight must not also inflate EpicWeight as epic-level peers.
        var crowded = new EpicInfo
        {
            Number = 1,
            Title = "Crowded Nested",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("1.1", "Nested", "active", 0, 1) },
        };
        var tasksOnly = new EpicInfo
        {
            Number = 2,
            Title = "Tasks Seven",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = new[] { Story("2.1", "Seven tasks", "ready", 0, 7, epicNumber: 2) },
        };
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { crowded, tasksOnly },
        };
        var deferredMarkdown = """
            ## Deferred from: code review of 1-1-foundation.md (2026-07-15)

            - Nested deferred one.
            - Nested deferred two.
            - Nested deferred three.
            - Nested deferred four.
            - Nested deferred five.
            - Nested deferred six.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 6),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 6 };
        var geometry = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(), counts, work, deferredModel: deferredModel, epics: model);

        Assert.Equal(6, geometry.StoryChildDeferred(1, "1.1").Count);
        Assert.Empty(geometry.EpicLevelDeferred(1, new[] { "1.1" }));

        // The SHARED WEIGHT FUNCTION does not double count, which is this test's own claim: `SunburstEpicWeight`
        // sums story weights (each already carrying its nested deferred) plus EPIC-LEVEL peers only, so six
        // story-child deferred inflate the epic exactly once.
        var nestedWeight = Charts.SunburstEpicWeight(geometry, UnplannedWorkGeometry.Empty, crowded);
        var tasksWeight = Charts.SunburstEpicWeight(geometry, UnplannedWorkGeometry.Empty, tasksOnly);
        Assert.Equal(7, nestedWeight);
        Assert.Equal(7, tasksWeight);

        // ⚠️ KNOWN FINDING, SURFACED BY STORY 20.7 AND NOT INTRODUCED BY IT. The dashboard PAYLOAD's rolled-up
        // epic value is 13, not 7, because two helpers scope "this epic's deferred" differently:
        //   • SunburstEpicWeight      -> FollowUpGeometry.EpicLevelDeferred  (EXCLUDES story-child deferred)
        //   • SunburstEpicAggregates  -> FollowUpGeometry.DeferredForEpicNumber (INCLUDES them)
        // The hand-rolled SVG never showed the discrepancy: it sized the epic wedge from SunburstEpicWeight and
        // drew the aggregate ring separately, so its rings were not parent-inclusive. Owner decision D2's
        // "children win" roll-up (Story 20.5) makes the epic the exact sum of its DRAWN children — story sector +
        // aggregate sector — so the six deferred items are counted once inside the story's 7 and again as the
        // aggregate's 6. It shipped with Story 20.5 and was invisible while the SVG was still the visible chart.
        //
        // NOT fixed here, deliberately: every candidate fix changes a count a reader already sees. Scoping the
        // aggregate to epic-level only would make the chart disagree with the SunburstCompanionList tile grid and
        // the generated group page beside it, which is the drift Story 20.3's live round caught. That is an
        // owner call, raised as Open Question 5 in the story record and at the verify round.
        var svg = Glance(model, followUps: geometry);
        var nested = OuterArcSweepRadians(svg, "Epic 1: Crowded Nested");
        var tasks = OuterArcSweepRadians(svg, "Epic 2: Tasks Seven");
        Assert.Equal(tasksWeight, tasks);                      // no aggregate on epic 2 -> payload agrees
        Assert.Equal(nestedWeight + 6, nested);                // epic 1: the six are counted a second time
        Assert.InRange(nested / tasks, 1.85, 1.87);            // characterized, so a further drift still fails
    }

    [Fact]
    public void FollowUpGeometry_From_OpenActionItemsAgreeWithLedger()
    {
        // Happy path: filtered open count matches ProjectCounts; done items remain for wedges.
        var items = new[]
        {
            new SprintActionItem("Open one", "open", EpicNumber: 1, Owner: null),
            new SprintActionItem("Open two", "open", EpicNumber: 1, Owner: null),
            new SprintActionItem("Done one", "done", EpicNumber: 1, Owner: null),
        };
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = null,
        };
        var counts = ProjectCounts.Empty with { OpenActionItems = 2 };
        var geometry = FollowUpGeometry.From(items, counts, work);
        Assert.Equal(2, geometry.OpenActionItems.Count);
        Assert.Equal(3, geometry.ActionItems.Count);
    }

    /// <summary>The angular weight of a node, read from the island payload.
    ///
    /// <para>It used to measure the SVG's own outer-arc sweep out of the <c>d="M x y A …"</c> path — the ratio
    /// tests below lock story-weight relationships, and a hand-rolled chart's only statement of a weight was the
    /// ink it laid down. Story 20.7 retired that chart, and Plotly computes its own geometry from the payload, so
    /// the payload's <c>value</c> IS the sweep now: two sectors are in the same ratio as their values, by
    /// construction. The tests keep asserting the same relationships, one layer closer to the source.
    /// [Story 20.7 Task 10.1 — a fact-asserting test rewritten against the payload, not a geometry one deleted]</para></summary>
    private static double OuterArcSweepRadians(string rendered, string labelContains)
    {
        var island = rendered[rendered.IndexOf("class=\"ss-hierarchy-data\"", StringComparison.Ordinal)..];
        island = island[(island.IndexOf('>') + 1)..island.IndexOf("</script>", StringComparison.Ordinal)];
        using var doc = System.Text.Json.JsonDocument.Parse(island);
        foreach (var node in doc.RootElement.GetProperty("nodes").EnumerateArray())
        {
            var label = node.GetProperty("label").GetString() ?? string.Empty;
            if (label.Contains(labelContains, StringComparison.Ordinal))
                return node.GetProperty("value").GetDouble();
        }
        Assert.Fail($"No payload node whose label contains '{labelContains}'");
        return 0;
    }

    // ---- Chart frame + heatmap real-value legend (Story 10.2) ----

    [Fact]
    public void Framed_RendersAllSlotsWhenSuppliedAndOmitsWhenNull()
    {
        var full = Charts.Framed(
            new Charts.ChartMeta("Title <X>", Window: "Last 3 commits", Ranking: "Top 2 of 9 by change count", Why: "Why matters."),
            body: "<div class=\"body\">ok</div>\n");

        Assert.Contains("<h3>Title &lt;X&gt;</h3>", full);
        Assert.Contains("class=\"chart-frame-window\">Last 3 commits</span>", full);
        Assert.Contains("class=\"chart-frame-ranking\">Top 2 of 9 by change count</p>", full);
        Assert.Contains("class=\"chart-frame-why\">Why matters.</p>", full);
        Assert.Contains("<div class=\"body\">ok</div>", full);

        var bare = Charts.Framed(new Charts.ChartMeta("Bare"), body: "<p>x</p>\n");
        Assert.Contains("<h3>Bare</h3>", bare);
        Assert.DoesNotContain("chart-frame-window", bare);
        Assert.DoesNotContain("chart-frame-ranking", bare);
        Assert.DoesNotContain("chart-frame-why", bare);
    }

    [Fact]
    public void Framed_HtmlEscapesEverySlot()
    {
        var html = Charts.Framed(
            new Charts.ChartMeta("<t>", Window: "<w>", Ranking: "<r>", Why: "<y>"),
            body: "b");

        Assert.Contains("&lt;t&gt;", html);
        Assert.Contains("&lt;w&gt;", html);
        Assert.Contains("&lt;r&gt;", html);
        Assert.Contains("&lt;y&gt;", html);
        Assert.DoesNotContain("<t>", html);
    }

    [Fact]
    public void WhyText_IsMetricGenericAndDefinedOnce()
    {
        // AC2 teeth: framing sentences live in WhyText, never name this repo.
        foreach (Charts.ChartMetric m in Enum.GetValues<Charts.ChartMetric>())
        {
            var why = Charts.WhyText(m);
            Assert.False(string.IsNullOrWhiteSpace(why));
            Assert.DoesNotContain("SpecScribe", why, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BMAD", why, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HeatLevelRange_MatchesCellLevelsForGradedHistory()
    {
        const int maxCount = 8;
        // Drive legend ranges from the shared helper; verify every count maps to a level whose range covers it.
        for (var count = 0; count <= maxCount; count++)
        {
            // Reconstruct HeatLevel via the public range helper + known thresholds (cells use the private twin).
            var level = count == 0 ? 0
                : maxCount <= 1 ? 1
                : count <= (int)Math.Floor(0.25 * maxCount) ? 1
                : count <= (int)Math.Floor(0.5 * maxCount) ? 2
                : count <= (int)Math.Floor(0.75 * maxCount) ? 3
                : 4;
            var label = Charts.HeatLevelRange(level, maxCount);
            if (count == 0)
            {
                Assert.Equal("0", label);
                continue;
            }
            Assert.DoesNotContain("Less", label);
            Assert.DoesNotContain("More", label);
            // Range label must mention the count (as a single digit or as bounds containing it).
            if (label.EndsWith('+'))
            {
                var lo = int.Parse(label.TrimEnd('+'), System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(count >= lo);
            }
            else if (label.Contains('\u2013'))
            {
                var parts = label.Split('\u2013');
                var lo = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                var hi = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(count, lo, hi);
            }
            else
            {
                Assert.Equal(count.ToString(System.Globalization.CultureInfo.InvariantCulture), label);
            }
        }
    }

    [Fact]
    public void HeatLevelRange_UniformHistoryDegradesWithoutNonsense()
    {
        Assert.Equal("0", Charts.HeatLevelRange(0, 1));
        Assert.Equal("1", Charts.HeatLevelRange(1, 1));
        Assert.Equal("\u2014", Charts.HeatLevelRange(2, 1));
        Assert.Equal("\u2014", Charts.HeatLevelRange(4, 0));
    }

    [Fact]
    public void CommitHeatmap_LegendCarriesRealRangesAndNumericWindow()
    {
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 1),
            (new DateOnly(2026, 1, 8), 8),
        };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("heatmap-legend", svg);
        Assert.Contains("heatmap-legend-label", svg);
        Assert.DoesNotContain(">Less ", svg);
        Assert.DoesNotContain(" More<", svg);
        // Window: weeks + date span (DReadable).
        Assert.Contains("chart-frame-window", svg);
        Assert.Contains("week", svg);
        Assert.Contains(Charts.DReadable(new DateOnly(2026, 1, 5)), svg);
        Assert.Contains(Charts.DReadable(new DateOnly(2026, 1, 8)), svg);
        // Real range text for the busiest bucket (level-4 open-ended).
        Assert.Contains(Charts.HeatLevelRange(4, 8), svg);
        Assert.Contains(Charts.HeatLevelRange(0, 8), svg);
    }

    [Fact]
    public void CommitHeatmap_WindowPresentWhenHeadlineSuppressed()
    {
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3) };
        var svg = Charts.CommitHeatmap(series, showHeadline: false);

        Assert.DoesNotContain("heatmap-headline", svg);
        Assert.Contains("heatmap-window", svg);
        Assert.Contains("chart-frame-window", svg);
    }

    // ---- Story 10.6 AC2a: young-repo heatmap dead-zone trim + first-commit accent ----

    [Fact]
    public void CommitHeatmap_YoungRepoTrimsDeadZoneToAboutOneWeekLead()
    {
        // A project 10 days old sits well inside the 15-week floor — the old behavior padded the grid all the
        // way back to firstCommit-105d (months of blank cells). The trim should instead start ~1 week before
        // the first commit (then week-snap), so the grid never spans more than a few weeks.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-10);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 2) };

        var svg = Charts.CommitHeatmap(series);

        // Extract the grid's viewBox width to recover the week count (width = leftGutter + weeks*(cell+gap)).
        var viewBox = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"0 0 (\\d+) ");
        Assert.True(viewBox.Success);
        var width = int.Parse(viewBox.Groups[1].Value);
        var weeks = (width - 26) / 14; // leftGutter=26, cell+gap=14
        // 10 days back + ~1 week lead + week-snap slack is at most 4 weeks — nowhere near the old ~15-16 week pad.
        Assert.True(weeks <= 4, $"expected a trimmed young-repo grid, got {weeks} weeks (width {width})");
    }

    [Fact]
    public void CommitHeatmap_YoungRepoMarksFirstCommitWithCaptionAndSvgAccent()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-10);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 2) };

        var svg = Charts.CommitHeatmap(series);

        // Text caption (the accessible half of the never-color-only pairing)...
        Assert.Contains($"<p class=\"heatmap-first-commit\">First commit {Charts.DReadable(firstCommit)}</p>", svg);
        // ...plus a decorative SVG accent mark, distinct shape (a rect), not a bare color change.
        Assert.Contains("heatmap-first-commit-mark", svg);
        Assert.Contains($"<title>First commit {Charts.DReadable(firstCommit)}</title>", svg);
    }

    [Fact]
    public void CommitHeatmap_YoungRepoMarkerSurvivesWithHeadlineSuppressed()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-10);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 2) };

        var svg = Charts.CommitHeatmap(series, showHeadline: false);

        Assert.DoesNotContain("heatmap-headline", svg);
        Assert.Contains("heatmap-first-commit-mark", svg);
        Assert.Contains("class=\"heatmap-first-commit\"", svg);
    }

    [Fact]
    public void CommitHeatmap_OldRepoWindowAndNoFirstCommitMarkerUnchanged()
    {
        // A repo well past the 15-week floor: the old-repo branch (start = firstCommit, full history shown)
        // must stay untouched, and there is no lead-in dead zone to mark.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-200);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 1), (today.AddDays(-1), 3) };

        var svg = Charts.CommitHeatmap(series);

        Assert.DoesNotContain("heatmap-first-commit-mark", svg);
        Assert.DoesNotContain("class=\"heatmap-first-commit\"", svg);
        // The window text still opens at the true first commit — old-repo history is never trimmed.
        Assert.Contains(Charts.DReadable(firstCommit), svg);
    }

    [Fact]
    public void CommitHeatmap_YoungRepoCapsRenderedWidthBelowStylesheetCeiling()
    {
        // A short grid (well under the 15-week floor) must not be stretched to the stylesheet's
        // full 460px cap — that's what turns a handful of weeks into huge, disproportionate tiles.
        // Asserted as bounds (not a re-derivation of the production formula): the cap must still allow
        // SOME enlargement over the grid's raw pixel size (readable, not shrunk), but must land strictly
        // under the 460px ceiling a short grid has no business reaching.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-10);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 2) };

        var svg = Charts.CommitHeatmap(series);

        var viewBox = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"0 0 (\\d+) ");
        Assert.True(viewBox.Success);
        var width = int.Parse(viewBox.Groups[1].Value);
        var styleMatch = System.Text.RegularExpressions.Regex.Match(svg, "style=\"max-width:(\\d+)px\"");
        Assert.True(styleMatch.Success, "expected an inline max-width style on the heatmap svg");
        var cap = int.Parse(styleMatch.Groups[1].Value);
        Assert.True(cap > width, $"expected some enlargement over the raw {width}px grid, got cap {cap}");
        Assert.True(cap < 460, $"expected the short grid's cap to be below 460px, got {cap}");
    }

    [Fact]
    public void CommitHeatmap_OldRepoStillHitsTheFull460pxCap()
    {
        // A grid already at/over the natural size the 460px ceiling was designed for renders unchanged.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-200);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 1), (today.AddDays(-1), 3) };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("style=\"max-width:460px\"", svg);
    }

    [Fact]
    public void CommitHeatmap_NearBoundaryGrid_CapNeverExceedsStylesheetCeiling()
    {
        // A grid near the point where 1.8x its natural width would cross 460px (~14-15 weeks: natural
        // width ~222-236px) — regardless of exactly which side of the boundary it lands on, the emitted
        // cap must never exceed the stylesheet's 460px ceiling, and never shrink the grid below its own
        // natural pixel size.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstCommit = today.AddDays(-7 * 14);
        var series = new (DateOnly Day, int Count)[] { (firstCommit, 1), (today.AddDays(-1), 2) };

        var svg = Charts.CommitHeatmap(series);

        var viewBox = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"0 0 (\\d+) ");
        Assert.True(viewBox.Success);
        var width = int.Parse(viewBox.Groups[1].Value);
        var styleMatch = System.Text.RegularExpressions.Regex.Match(svg, "style=\"max-width:(\\d+)px\"");
        Assert.True(styleMatch.Success);
        var cap = int.Parse(styleMatch.Groups[1].Value);
        Assert.True(cap <= 460, $"cap must never exceed the 460px ceiling, got {cap}");
        Assert.True(cap >= width, $"cap must never shrink the grid below its own natural width {width}px, got {cap}");
    }

    [Fact]
    public void CommitHeatmap_EveryCommitAfterTheCutoff_RendersTheDesignedEmptyStateInsteadOfAnOverclaimingGrid()
    {
        // Clock/timezone skew can put the series Min day well after today — and since Story 5.7 an --as-of date
        // before every commit reaches the same state deliberately. There is then nothing to draw, and the pre-5.7
        // whole-series summary would still have named the commits no cell renders. The honest answer is the
        // designed empty state (UX-DR22) naming the cutoff, not a zero-cell grid with an overclaiming
        // aria-label/headline. [Story 5.7 D2 / AC #1a; supersedes the old "clamp the inverted window" expectation,
        // which is now unreachable because the window is derived from the VISIBLE days.]
        var today = DateOnly.FromDateTime(DateTime.Now);
        var series = new (DateOnly Day, int Count)[] { (today.AddDays(21), 2) };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("chart-empty", svg, StringComparison.Ordinal);
        Assert.Contains("No commits on or before", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<svg", svg, StringComparison.Ordinal);
        // The whole point: no summary figure survives that the grid cannot show.
        Assert.DoesNotContain("2 commit", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("heatmap-headline", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void CommitHeatmap_FutureDatedCommitBesideAVisibleOne_KeepsValidGridAndCountsOnlyTheVisibleDays()
    {
        // The surviving half of the future-skew guard: with at least one day on or before the cutoff the grid still
        // renders with a positive week count and the first-commit caption never appears without its SVG mark — and
        // the accessible name and the visible headline BOTH count only the visible day, so the text twin and the
        // cells agree (ADR 0013). [Story 5.7 AC #1a]
        var today = DateOnly.FromDateTime(DateTime.Now);
        var series = new (DateOnly Day, int Count)[] { (today.AddDays(-3), 1), (today.AddDays(21), 2) };

        var svg = Charts.CommitHeatmap(series);

        var viewBox = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"0 0 (\\d+) ");
        Assert.True(viewBox.Success);
        var width = int.Parse(viewBox.Groups[1].Value);
        Assert.True(width > 26, $"expected a positive-width SVG grid, got width {width}");
        var weeks = (width - 26) / 14;
        Assert.True(weeks >= 1, $"expected at least one week, got {weeks}");

        var hasMark = svg.Contains("heatmap-first-commit-mark", StringComparison.Ordinal);
        var hasCaption = svg.Contains("class=\"heatmap-first-commit\"", StringComparison.Ordinal);
        Assert.Equal(hasMark, hasCaption);

        // 1 visible commit on 1 visible day — never the 3-commit whole-series total.
        Assert.Contains("aria-label=\"Commit activity: 1 commit across 1 active day,", svg, StringComparison.Ordinal);
        Assert.Contains("<strong>1</strong> commit &middot; <strong>1</strong> active day", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("3 commit", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void CommitHeatmap_ExplicitPastCutoff_BoundsBothTheAriaLabelAndTheHeadlineToTheRenderedWindow()
    {
        // The production shape of the --as-of path: the run's ONE resolved cutoff arrives via `today:` and is fully
        // deterministic (no machine clock involved). Both text surfaces restate the same figures, so BOTH must stop
        // at the cutoff — fixing one alone leaves the twin disagreeing with the visual, which ADR 0013 forbids.
        // [Story 5.7 AC #1a]
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 2),
            (new DateOnly(2026, 1, 9), 1),
            (new DateOnly(2026, 3, 1), 40),
        };

        var svg = Charts.CommitHeatmap(series, today: new DateOnly(2026, 1, 31));

        Assert.Contains(
            "aria-label=\"Commit activity: 3 commits across 2 active days, Mon, Jan 5, 2026 to Fri, Jan 9, 2026\"",
            svg,
            StringComparison.Ordinal);
        Assert.Contains("<strong>3</strong> commits &middot; <strong>2</strong> active days", svg, StringComparison.Ordinal);
        // The out-of-window day must appear in neither text surface, nor as a linked "last commit" date.
        Assert.DoesNotContain("2026-03-01", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("Mar 1, 2026", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("43 commit", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactCoveragePanel_NoHrefPresentCard_StaysFocusableForItsTooltip()
    {
        // A present family whose page failed to generate (Href is null) has no other interactive control on
        // its card, but the js-tip tooltip still carries the memlog date that isn't shown in the card body —
        // tabindex="0" must survive so keyboard/AT users can reach it by focus, not just hover. [Story 3.6 review]
        var family = new ArtifactFamily(
            Label: "Epics",
            ConceptIconKey: "epics",
            Description: "Feature breakdown into implementable stories.",
            Present: true,
            LastModified: new DateOnly(2026, 7, 1),
            SourcePath: "planning-artifacts/epics.md",
            MemlogUpdated: new DateOnly(2026, 6, 20),
            Href: null);
        var coverage = new ArtifactCoverage { Families = [family] };

        var html = Charts.ArtifactCoveragePanel(coverage, new DateOnly(2026, 7, 19));

        Assert.Contains("coverage-card js-tip present family-epics\" data-tip=", html);
        Assert.Contains("tabindex=\"0\"", html);
        Assert.DoesNotContain("<a class=\"coverage-card", html);
    }

    // ----- Delivery cadence (Story 21.2) --------------------------------------------------------------------

    private static readonly DateOnly CadenceToday = new(2026, 7, 25);

    private static IReadOnlyDictionary<DateOnly, IReadOnlyList<StoryInfo>> CompletionsByDay(
        params (DateOnly Day, StoryInfo[] Stories)[] entries) =>
        entries.ToDictionary(e => e.Day, e => (IReadOnlyList<StoryInfo>)e.Stories);

    [Fact]
    public void DeliveryCadenceHeatmap_EmptySeries_RendersChartEmpty()
    {
        var html = Charts.DeliveryCadenceHeatmap(Array.Empty<(DateOnly, int)>(), today: CadenceToday);
        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<svg", html);
    }

    [Fact]
    public void DeliveryCadenceHeatmap_SingleCompletionDay_LinksToThatStory()
    {
        var story = Story("1.1", "Foundation", "done", 3, 3);
        var day = new DateOnly(2026, 7, 20);
        var html = Charts.DeliveryCadenceHeatmap(
            new[] { (day, 1) },
            CompletionsByDay((day, new[] { story })),
            s => $"epics/story-{s.Id.Replace('.', '-')}.html",
            CadenceToday);

        Assert.Contains("href=\"epics/story-1-1.html\"", html);
        Assert.Contains("1 story completed", html);
        // Whole-chart role becomes group once a day links.
        Assert.Contains("role=\"group\"", html);
    }

    [Fact]
    public void DeliveryCadenceHeatmap_MultipleSameDayCompletions_ListsAllInASingleNativeTitle()
    {
        var day = new DateOnly(2026, 7, 20);
        var stories = new[] { Story("1.1", "Alpha", "done", 1, 1), Story("1.2", "Beta", "done", 1, 1) };
        var html = Charts.DeliveryCadenceHeatmap(
            new[] { (day, 2) },
            CompletionsByDay((day, stories)),
            s => $"epics/story-{s.Id.Replace('.', '-')}.html",
            CadenceToday);

        // The multi-completion cell lists every story in ONE native <title> — and NOT a second js-tip/data-tip
        // tooltip, so a cell never shows two overlapping tooltips (Story 21.2 review). The rich linked version
        // lives in the completion log below.
        Assert.Contains("Jul 20, 2026: 2 stories completed", html);
        Assert.Contains("Story 1.1 — Alpha", html);
        Assert.Contains("Story 1.2 — Beta", html);
        Assert.DoesNotContain("data-tip=", html);
    }

    [Fact]
    public void DeliveryCadenceHeatmap_RendersTextEquivalentCompletionLog()
    {
        var day = new DateOnly(2026, 7, 20);
        var story = Story("1.1", "Foundation", "done", 1, 1);
        var html = Charts.DeliveryCadenceHeatmap(
            new[] { (day, 1) },
            CompletionsByDay((day, new[] { story })),
            s => $"epics/story-{s.Id.Replace('.', '-')}.html",
            CadenceToday);

        // The accessible / no-JS twin below the SVG — collapsed into a <details> so the tile grid stays primary.
        Assert.Contains("cadence-log-details", html);
        Assert.Contains("<summary", html);
        Assert.Contains("cadence-log-date", html);
        Assert.Contains("1 story completed", html);
        // The legend carries an explicit unit so the shade ramp isn't misread as commits/day.
        Assert.Contains("Stories completed / day", html);
    }

    [Fact]
    public void CycleTimeHistogram_Empty_RendersHonestNote()
    {
        var html = Charts.CycleTimeHistogram(Array.Empty<(string, int)>());
        Assert.Contains("chart-empty", html);
        Assert.Contains("No story has a derivable cycle-time", html);
    }

    [Fact]
    public void CycleTimeHistogram_BucketsSumToTheTotalInputCount()
    {
        var cycleTimes = new (string, int)[]
        {
            ("1.1", 0), ("1.2", 3),     // 0-3
            ("1.3", 5),                 // 4-7
            ("1.4", 10), ("1.5", 14),   // 8-14
            ("1.6", 30),                // 15-30
            ("1.7", 45), ("1.8", 900),  // 30+
        };
        var html = Charts.CycleTimeHistogram(cycleTimes);

        // Every input story lands in exactly one bucket — the per-bucket counts sum to the input size.
        var total = System.Text.RegularExpressions.Regex
            .Matches(html, @"git-pulse-bar-count"">(\d+) stor")
            .Sum(m => int.Parse(m.Groups[1].Value));
        Assert.Equal(cycleTimes.Length, total);
        // All five human-readable buckets render (the distribution's shape is the information).
        Assert.Contains("0–3 days", html);
        Assert.Contains("30+ days", html);
    }

    [Fact]
    public void CycleTimeHistogram_NeverColorOnly_CountTextBesideEveryBar()
    {
        var html = Charts.CycleTimeHistogram(new (string, int)[] { ("1.1", 2) });
        // The count is present in text next to the bar (not size/color-only).
        Assert.Contains("git-pulse-bar-count", html);
        Assert.Contains("1 story", html);
        Assert.Contains("aria-label=", html);
    }

    [Fact]
    public void WhyText_DeliveryCadence_IsNonEmptyFrameworkNeutralAndDistinctFromActivityCadence()
    {
        var cadence = Charts.WhyText(Charts.ChartMetric.DeliveryCadence);
        var activity = Charts.WhyText(Charts.ChartMetric.ActivityCadence);

        Assert.False(string.IsNullOrWhiteSpace(cadence));
        Assert.NotEqual(activity, cadence);
        // Framework-neutral (NFR8) — never names a specific project/repo.
        Assert.DoesNotContain("SpecScribe", cadence);
    }

    [Fact]
    public void DeliveryCadenceStrip_EmptyData_RendersNothing()
    {
        Assert.Equal(string.Empty, Charts.DeliveryCadenceStrip(DeliveryCadenceData.Empty, "cadence.html", CadenceToday));
    }

    [Fact]
    public void DeliveryCadenceStrip_WithData_ShowsCountsAndLink()
    {
        var data = new DeliveryCadenceData(
            new[] { (new DateOnly(2026, 7, 20), 2), (new DateOnly(2026, 3, 1), 1) },
            new Dictionary<DateOnly, IReadOnlyList<StoryInfo>>(),
            Array.Empty<(string, int)>());

        var html = Charts.DeliveryCadenceStrip(data, "cadence.html", CadenceToday);

        Assert.Contains("cadence-strip", html);
        Assert.Contains("href=\"cadence.html\"", html);
        Assert.Contains("View delivery cadence", html);
        // Recent (last 8 weeks) counts only the Jul 20 completions; all-time counts both.
        Assert.Contains(">2</span><span class=\"git-pulse-caption\">stories completed in the last 8 weeks", html);
        Assert.Contains(">3</span><span class=\"git-pulse-caption\">completed all-time", html);
    }
}
