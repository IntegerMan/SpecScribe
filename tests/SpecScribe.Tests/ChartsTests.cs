using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Accessibility-name coverage for the SVG charts (Story 1.4 AC #1): every drillable segment link
/// carries an aria-label so its hover-only &lt;title&gt; is reachable without a pointer, and the whole-chart
/// donut/heatmap SVGs expose a role="img" name. Colour/legend text redundancy (status is never colour-only)
/// is guarded too so it can't silently regress.</summary>
public class ChartsTests
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
    public void Sunburst_SegmentLinksCarryAriaLabelsAndKeepTitles()
    {
        var story = Story("1.1", "Do the thing", "in progress", done: 2, total: 5);
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(story) },
        };

        var svg = Charts.Sunburst(model);

        // Epic + story segment <a>s carry a descriptive aria-label (keyboard/SR name)...
        Assert.Contains("aria-label=\"Epic 1: First Epic — In development, 1 story\"", svg);
        Assert.Contains("aria-label=\"Story 1.1: Do the thing — in progress\"", svg);
        // ...the task ring links are named too...
        Assert.Contains("aria-label=\"Story 1.1: 2 of 5 tasks done\"", svg);
        Assert.Contains("aria-label=\"Story 1.1: 3 tasks remaining\"", svg);
        // ...and the pointer-only <title> tooltips are still present (both paths retained).
        Assert.Contains("<title>Epic 1: First Epic", svg);
        Assert.Contains("<title>Story 1.1: Do the thing", svg);
        // Legend text keeps status shape+label, not colour alone (UX-DR17).
        Assert.Contains("Pending</span>", svg);
        Assert.Contains("Done</span>", svg);
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

        var noRetro = Charts.Sunburst(Model(hasRetro: false));
        // The epic (inner-ring) segment carries the review class + label. (The task ring has its own sb-done arc
        // for the finished tasks, so the epic segment's aria-label is the unambiguous signal to assert on.)
        Assert.Contains("class=\"sb-seg sb-review\"", noRetro);
        Assert.Contains("aria-label=\"Epic 1: First Epic — In review, 1 story\"", noRetro);

        var withRetro = Charts.Sunburst(Model(hasRetro: true));
        // Once a retro exists the epic segment is green "done" again. (Assert on the epic aria-label, not a bare
        // "In review" — the legend always lists an "In review" swatch regardless of the data.)
        Assert.Contains("aria-label=\"Epic 1: First Epic — Done, 1 story\"", withRetro);
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

        var svg = Charts.Sunburst(model);

        Assert.Contains("<span class=\"sb-legend-item sb-review-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"sb-legend-item sb-done-item\" tabindex=\"0\">", svg);
        // The always-visible swatch + label remain (status is never emphasis-only / colour-only).
        Assert.Contains("<span class=\"swatch sb-review-sw\"></span>In review</span>", svg);
    }

    [Fact]
    public void EpicSunburst_LegendEntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // Review follow-up: the epic-level Sunburst test above only covers ONE of the three SunburstLegend
        // call sites. This pins the story-level overload (EpicSunburst), which uses the same 6-tuple set.
        var epic = Epic(Story("1.1", "A story", "in progress", done: 2, total: 5));

        var svg = Charts.EpicSunburst(epic, _ => "epics/epic-1.html");

        Assert.Contains("<span class=\"sb-legend-item sb-review-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"sb-legend-item sb-done-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"swatch sb-review-sw\"></span>In review</span>", svg);
    }

    [Fact]
    public void TaskSunburst_LegendEntriesAreKeyboardReachableAndStatusKeyedForEmphasis()
    {
        // Review follow-up: pins the third SunburstLegend call site (TaskSunburst), which uses the distinct
        // 2-item "Not done"/"Done" set rather than the six lifecycle statuses.
        var tasks = new List<TaskItem> { new("Do the thing", Done: true, Subtasks: Array.Empty<TaskItem>()) };

        var svg = Charts.TaskSunburst(tasks);

        Assert.Contains("<span class=\"sb-legend-item sb-pending-item\" tabindex=\"0\">", svg);
        Assert.Contains("<span class=\"sb-legend-item sb-done-item\" tabindex=\"0\">", svg);
        // This legend's pending label reads "Not done", not the shared "Pending" text (status is never
        // colour-only, and the wording matches this chart's own not-done/done framing).
        Assert.Contains("<span class=\"swatch sb-pending-sw\"></span>Not done</span>", svg);
        Assert.Contains("<span class=\"swatch sb-done-sw\"></span>Done</span>", svg);
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

        var svg = Charts.Sunburst(model);

        Assert.Contains($"href=\"{StoryEpicLinkifier.StoryPagePath("1.2")}\"", svg);
        Assert.DoesNotContain("href=\"epics/epic-1.html\" aria-label=\"Story 1.2", svg);
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
        var multiSvg = Charts.Sunburst(multi);
        Assert.Contains(">2</text>", multiSvg);
        Assert.Contains(">epics</text>", multiSvg);
        Assert.DoesNotContain(">stories</text>", multiSvg);

        var single = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(S("1.1")) },
        };
        var singleSvg = Charts.Sunburst(single);
        Assert.Contains(">1</text>", singleSvg);
        Assert.Contains(">epic</text>", singleSvg);
    }

    [Fact]
    public void EpicSunburst_SegmentLinksCarryAriaLabels()
    {
        var epic = Epic(Story("1.1", "A story", "done", done: 3, total: 3));

        var svg = Charts.EpicSunburst(epic, _ => "epics/epic-1.html");

        Assert.Contains("aria-label=\"Story 1.1: A story — done\"", svg);
        Assert.Contains("aria-label=\"Story 1.1: 3 of 3 tasks done\"", svg);
        Assert.Contains("role=\"img\"", svg); // whole-chart name retained
        // Center headlines the epic's story count (singular here), never a task fraction, even when tasks
        // exist — the outer ring already conveys task completion. [epic-sunburst story-count]
        Assert.Contains(">1</text>", svg);
        Assert.Contains(">story</text>", svg);
        Assert.DoesNotContain(">tasks</text>", svg);
    }

    private static CommandCatalog Catalog() => new("BMad", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
    });

    [Fact]
    public void Sunburst_UnplannedStoryGetsDashedPlaceholderArcWithCreateStoryCta()
    {
        // A story with a plan and one without: only the unplanned one gets the dashed placeholder arc, and
        // its tooltip names the module's create-story command for that id. [Story 2.1 UXO E4]
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Planned", "in progress", 2, 5), Story("1.2", "Unplanned", "ready", 0, 0)) },
        };

        var svg = Charts.Sunburst(model, commands: Catalog());

        // The placeholder arc exists, is a real link, and carries the call-to-action tooltip + aria fallback.
        Assert.Contains("class=\"sb-seg sb-noplan\"", svg);
        Assert.Contains("<title>Story 1.2: no task plan yet — run /bmad-create-story 1.2</title>", svg);
        Assert.Contains("aria-label=\"Story 1.2: no task plan yet\"", svg);
        // The planned story keeps its real task ring, not a placeholder.
        Assert.Contains("aria-label=\"Story 1.1: 2 of 5 tasks done\"", svg);
    }

    [Fact]
    public void Sunburst_PlaceholderOmitsCommandWhenModuleLacksIt()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { Epic(Story("1.1", "Unplanned", "ready", 0, 0)) },
        };

        // No catalog → no command is invented; the placeholder still reads as a next action, just unnamed.
        var svg = Charts.Sunburst(model);

        Assert.Contains("class=\"sb-seg sb-noplan\"", svg);
        Assert.Contains("<title>Story 1.1: no task plan yet</title>", svg);
        Assert.DoesNotContain("run /", svg);
    }

    [Fact]
    public void EpicSunburst_UnplannedStoryGetsDashedPlaceholderArc()
    {
        var epic = Epic(Story("1.1", "Unplanned", "ready", 0, 0));

        var svg = Charts.EpicSunburst(epic, _ => "epics/epic-1.html", commands: Catalog());

        Assert.Contains("class=\"sb-seg sb-noplan\"", svg);
        Assert.Contains("<title>Story 1.1: no task plan yet — run /bmad-create-story 1.1</title>", svg);
        // The story segment itself stays a real link (the placeholder doesn't replace navigation).
        Assert.Contains("aria-label=\"Story 1.1: Unplanned — ready\"", svg);
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
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", OpenItemCount: 3),
        };
        var counts = ProjectCounts.Empty with { DeferredOpenItems = 3, OpenActionItems = 2 };
        var geometry = FollowUpGeometry.From(items, counts, work);

        var svg = Charts.Sunburst(model, followUps: geometry);

        Assert.Equal(2, geometry.OpenActionItems.Count);
        Assert.Equal(counts.DeferredOpenItems, geometry.DeferredOpenCount);
        // Open follow-ups are orange; completed action items reuse done green — never a 4th outer ring.
        Assert.Contains("class=\"sb-seg sb-followup-open\"", svg);
        Assert.Contains("aria-label=\"Action item: Fix the heatmap debt\"", svg);
        Assert.Contains("aria-label=\"Action item (done): Ship delivery follow-up\"", svg);
        Assert.Contains("aria-label=\"Action item: Unscoped cleanup\"", svg);
        Assert.Contains("aria-label=\"Deferred work: 3 open items\"", svg);
        Assert.Contains("aria-label=\"Follow-ups:", svg);
        Assert.DoesNotContain("outermost: open follow-ups", svg);
        Assert.Contains("Open follow-up</span>", svg);
        Assert.Contains("stories &amp; follow-ups", svg);
        // Per-item detail hrefs (Story 9.11) on action wedges.
        Assert.Contains("href=\"follow-ups/action-", svg);
        Assert.Contains("href=\"follow-ups/action-fix-the-heatmap-debt", svg);
        // Epic 1 aria mentions its follow-up so aggregation is visible on the epic wedge.
        Assert.Contains("1 follow-up", svg);
        foreach (var label in ExtractFollowUpAriaLabels(svg).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(label.StartsWith("Story", StringComparison.Ordinal), label);
        }
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

        var without = Charts.Sunburst(model);
        var withEmpty = Charts.Sunburst(model, followUps: FollowUpGeometry.Empty);

        Assert.DoesNotContain("sb-followup", without);
        Assert.DoesNotContain("sb-followup", withEmpty);
        Assert.DoesNotContain("Open follow-up</span>", without);
        Assert.Equal(without, withEmpty);
    }

    [Fact]
    public void EpicSunburst_FollowUps_AreStoryRingPeers_FilteredToEpic()
    {
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
            DeferredOpenCount: 2,
            DeferredHref: "deferred-work.html",
            ActionItemsHref: SiteNav.ActionItemsOutputPath);

        var svg1 = Charts.EpicSunburst(epic1, _ => "epics/epic-1.html", followUps: geometry);
        var svg2 = Charts.EpicSunburst(epic2, _ => "epics/epic-2.html", followUps: geometry);

        Assert.Contains("aria-label=\"Action item: Epic 1 only\"", svg1);
        Assert.Contains("class=\"sb-seg sb-followup-open\"", svg1);
        Assert.Contains("href=\"follow-ups/action-", svg1);
        Assert.DoesNotContain("Epic 2 only", svg1);
        Assert.DoesNotContain("Deferred work", svg1);
        Assert.Contains("aria-label=\"Action item: Epic 2 only\"", svg2);
        Assert.DoesNotContain("Epic 1 only", svg2);
        Assert.DoesNotContain("outermost: open follow-ups", svg1);
    }

    private static string ExtractFollowUpAriaLabels(string svg)
    {
        var labels = new List<string>();
        var needle = "aria-label=\"";
        for (var i = 0; (i = svg.IndexOf(needle, i, StringComparison.Ordinal)) >= 0;)
        {
            i += needle.Length;
            var end = svg.IndexOf('"', i);
            if (end < 0) break;
            var label = svg[i..end];
            if (label.StartsWith("Action item", StringComparison.Ordinal)
                || label.StartsWith("Deferred work:", StringComparison.Ordinal)
                || label.StartsWith("Follow-ups:", StringComparison.Ordinal))
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

        Assert.Contains("No file changes in recent history.", html);
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

    [Fact]
    public void CodeTreemap_RendersOneFocusableRectPerFileWithMetricDataAttributes()
    {
        var map = TreemapWithMetrics();
        var svg = Charts.CodeTreemap(map.Layout(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: true, fileHref: null);

        // Server-rendered SVG (no client layout math), one file rect carrying every metric as data-*. No id on the
        // <svg> itself — the page can render up to four of these (round 2), and a duplicate id is invalid HTML.
        Assert.Contains("<svg class=\"codemap\"", svg);
        Assert.Contains("class=\"codemap-cell", svg);
        Assert.Contains("data-path=\"src/A.cs\"", svg);
        Assert.Contains("data-lines=\"100\"", svg);
        Assert.Contains("data-changes=\"5\"", svg);
        Assert.Contains("data-churn=\"120\"", svg);
        // Focusable, with a rich body-level tooltip + accessible name (color never the sole signal, AC #4).
        Assert.Contains("tabindex=\"0\"", svg);
        Assert.Contains("js-tip", svg);
        Assert.Contains("data-tip-html=", svg);
        Assert.Contains("aria-label=", svg);
        // The default (change-frequency) fill is baked in server-side — A.cs is the busiest, so level-4.
        Assert.Contains("codemap-cell level-4 js-tip", svg);
    }

    [Fact]
    public void CodeTreemap_LinksFileOnlyWhenResolverReturnsATarget()
    {
        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>());
        var layout = map.Layout();

        // A resolver that yields a page → the rect is wrapped in an <a>; role="link" is omitted on the rect
        // itself since nesting an interactive role inside the already-interactive <a> is invalid ARIA.
        var linked = Charts.CodeTreemap(layout, CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false,
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        Assert.Contains("<a href=\"code/src/A.cs.html\">", linked);
        Assert.DoesNotContain("role=\"link\"", linked);
        // The whole-SVG root still carries role="img" (a separate, valid usage); only the per-rect role is omitted
        // when the rect is wrapped in a real <a> — assert the rect markup itself has no role attribute.
        Assert.Contains("<rect class=\"codemap-cell level-none js-tip\" tabindex=\"0\"", linked);
        Assert.DoesNotContain("role=\"img\" aria-label=\"A.cs", linked);

        // No resolver → a plain, focusable rect, never a broken link (the 7.1-dormant seam).
        var plain = Charts.CodeTreemap(layout, CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false, fileHref: null);
        Assert.DoesNotContain("<a href", plain);
        Assert.Contains("role=\"img\"", plain);
    }

    [Fact]
    public void CodeTreemap_AppliesThePrefixToLinkedFileHrefs()
    {
        // Mirrors CodeMapTemplater.AppendFileTable's `prefix + target` discipline so the SVG rect and the
        // text-equivalent table always agree on the same resolved link, even if code-map.html ever moves off
        // the output root.
        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>());
        var layout = map.Layout();

        var linked = Charts.CodeTreemap(layout, CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false,
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null, prefix: "../");
        Assert.Contains("<a href=\"../code/src/A.cs.html\">", linked);
    }

    [Fact]
    public void CodeTreemap_MetriclessFilesAreNeutralWithNoGitDataAttributes()
    {
        // Git data unavailable (empty metric dict) → every file is sized-by-LOC with a neutral fill and no
        // per-file git data-* (per-file graceful degradation, AC #2).
        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>());
        var svg = Charts.CodeTreemap(map.Layout(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false, fileHref: null);

        Assert.Contains("level-none", svg);
        Assert.DoesNotContain("data-changes", svg);
        Assert.DoesNotContain("data-churn", svg);
    }

    [Fact]
    public void CodeTreemap_EscapesLabelsAndPaths()
    {
        var map = CodeMap.Build(new[] { ("a&b/c<d>.cs", 5L) }, new Dictionary<string, CodeFileMetrics>());
        var svg = Charts.CodeTreemap(map.Layout(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false, fileHref: null);

        Assert.Contains("a&amp;b", svg);          // directory label + data-path escaped
        Assert.Contains("c&lt;d&gt;.cs", svg);    // file label/path escaped
        Assert.DoesNotContain("<d>", svg);        // no raw unescaped angle brackets leak through
    }

    [Fact]
    public void CodeTreemap_DirectoriesCarryNoTextLabelAtAnyDepth()
    {
        // A top-level dir and two nested sibling directories. No directory — at any depth — emits a <text> label;
        // the treemap is pure boxes + color, and every name lives in the tooltip card + text table instead.
        var map = CodeMap.Build(
            new[] { ("alpha/beta/A.cs", 100L), ("alpha/gamma/B.cs", 100L) },
            new Dictionary<string, CodeFileMetrics>());
        var svg = Charts.CodeTreemap(map.Layout(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false, fileHref: null);

        Assert.DoesNotContain("<text", svg);
        Assert.DoesNotContain(">alpha</text>", svg);
        Assert.DoesNotContain(">beta</text>", svg);
        Assert.DoesNotContain(">gamma</text>", svg);
        // Boundary rects are still drawn at every depth (AC #1 "clear boundaries") — just unlabeled.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(svg, "class=\"codemap-dir\"").Count);
    }

    [Fact]
    public void CodeTreemap_RendersRichHtmlTooltipCardWithCoChangeMetric()
    {
        var map = CodeMap.Build(
            new[] { ("src/A.cs", 100L) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 120, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1), AvgCoChanged: 3.4),
            });
        var svg = Charts.CodeTreemap(map.Layout(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: true, fileHref: null);

        // The stylized card is served via data-tip-html (double-escaped for the attribute → innerHTML round-trip);
        // the old plain-text data-tip attribute is gone.
        Assert.Contains("data-tip-html=", svg);
        Assert.DoesNotContain("data-tip=\"", svg);
        // Co-change is both a data-* (for the JS dimension switch) and a labeled row inside the card.
        Assert.Contains("data-cochanged=\"3.4\"", svg);
        Assert.Contains("Files changed together", svg);
        // Dates render via the portal's human-readable token (Charts.DReadable), not the raw ISO machine token —
        // the card is user-facing text, matching the rest of the app's tooltip date convention.
        Assert.Contains("Jun 1, 2026", svg);
        Assert.Contains("Jul 1, 2026", svg);
        Assert.DoesNotContain("2026-06-01", svg);
        Assert.DoesNotContain("2026-07-01", svg);
    }

    [Fact]
    public void CodeTreemap_EmptyLayoutRendersAnEmptyChartNotice()
    {
        var svg = Charts.CodeTreemap(Array.Empty<TreemapRect>(), CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics: false, fileHref: null);
        Assert.Contains("chart-empty", svg);
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
}
