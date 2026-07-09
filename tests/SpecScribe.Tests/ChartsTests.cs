using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Accessibility-name coverage for the SVG charts (Story 1.4 AC #1): every drillable segment link
/// carries an aria-label so its hover-only &lt;title&gt; is reachable without a pointer, and the whole-chart
/// donut/heatmap SVGs expose a role="img" name. Colour/legend text redundancy (status is never colour-only)
/// is guarded too so it can't silently regress.</summary>
public class ChartsTests
{
    private static StoryInfo Story(string id, string title, string? status, int done, int total) => new()
    {
        Id = id,
        EpicNumber = 1,
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

    // ---- Project structure tree (Story 3.4) ------------------------------------------------

    [Fact]
    public void ProjectStructureTree_RendersDirectoriesAsNativeDetailsWithGlyphAndLabel()
    {
        var tree = ProjectTree.Build(new[] { "planning-artifacts/epics.md" }, new Dictionary<string, string>());

        var html = Charts.ProjectStructureTree(tree);

        // A directory branch is a native <details>/<summary> disclosure (zero-JS expand/collapse + announced
        // state), the top-level one open, carrying the decorative Structure glyph beside its label.
        Assert.Contains("<details open>", html);
        Assert.Contains("<summary>", html);
        Assert.Contains("aria-hidden=\"true\"", html); // the Structure glyph is decorative
        Assert.Contains("<span class=\"tree-label\">planning-artifacts</span>", html);
        // The whole surface is JS-free — lock the no-script contract.
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void ProjectStructureTree_LinksMappedLeavesAndLeavesOthersAsPlainText()
    {
        var tree = ProjectTree.Build(
            new[] { "planning-artifacts/epics.md", "planning-artifacts/orphan.md" },
            new Dictionary<string, string> { ["planning-artifacts/epics.md"] = "epics.html" });

        var html = Charts.ProjectStructureTree(tree);

        // Mapped file → an <a> to its generated page (AC #2 routing). structure.html is at the root, so the
        // output-relative href passes through unprefixed.
        Assert.Contains("<a class=\"tree-file\" href=\"epics.html\">epics.md</a>", html);
        // Unmapped file → plain, non-link text — never a broken link.
        Assert.Contains("<span class=\"tree-file\">orphan.md</span>", html);
    }

    [Fact]
    public void ProjectStructureTree_EscapesLabelsAndHrefs()
    {
        var tree = ProjectTree.Build(
            new[] { "a&b/c<d>.md" },
            new Dictionary<string, string> { ["a&b/c<d>.md"] = "pages/a&b.html" });

        var html = Charts.ProjectStructureTree(tree);

        Assert.Contains("a&amp;b</span>", html);        // directory label escaped
        Assert.Contains(">c&lt;d&gt;.md</a>", html);     // file label escaped
        Assert.Contains("href=\"pages/a&amp;b.html\"", html); // href escaped
        Assert.DoesNotContain("<d>", html);              // no raw unescaped angle brackets leak through
    }
}
