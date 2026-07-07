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

        var svg = Charts.Sunburst(model, Catalog());

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

        var svg = Charts.EpicSunburst(epic, _ => "epics/epic-1.html", Catalog());

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
    public void CommitHeatmap_WithoutDetailsCarriesRoleImgAndName()
    {
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 3),
            (new DateOnly(2026, 1, 7), 1),
        };

        var svg = Charts.CommitHeatmap(series);

        // A link-free render keeps role="img": one named graphic, children hidden from AT.
        Assert.Contains("role=\"img\"", svg);
        Assert.Contains("aria-label=\"Commit activity: 4 commits across 2 active days, 2026-01-05–2026-01-07\"", svg);
        // Per-cell tooltips remain for pointer users.
        Assert.Contains("<title>2026-01-05: 3 commits</title>", svg);
    }

    private static IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> Commits(
        params (DateOnly Day, CommitInfo[] Items)[] days) =>
        days.ToDictionary(d => d.Day, d => (IReadOnlyList<CommitInfo>)d.Items);

    [Fact]
    public void CommitHeatmap_LinksActiveDaysAndEmitsPanels()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var d2 = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (d1, 2), (d2, 1) };
        var commits = Commits(
            (d1, new[] { new CommitInfo("abc1234", "First change"), new CommitInfo("def5678", "Second change") }),
            (d2, new[] { new CommitInfo("aaa1111", "Third change") }));

        var svg = Charts.CommitHeatmap(series, commits);

        // With drill-down links present, the SVG is role="group" so AT can reach them.
        Assert.Contains("role=\"group\"", svg);
        Assert.DoesNotContain("role=\"img\"", svg);
        // Active-day cells are wrapped in drill-down anchors with accessible names...
        Assert.Contains("<a href=\"#heat-day-2026-01-05\" aria-label=\"2026-01-05: 2 commits — view details\">", svg);
        Assert.Contains("<a href=\"#heat-day-2026-01-07\" aria-label=\"2026-01-07: 1 commit — view details\">", svg);
        // ...and each active day gets a panel listing hash + subject, plus a Close link back to the chart.
        Assert.Contains("id=\"heat-day-2026-01-05\"", svg);
        Assert.Contains("<code>abc1234</code> First change", svg);
        Assert.Contains("<code>aaa1111</code> Third change", svg);
        Assert.Contains("class=\"heatmap-day-close\" href=\"#commit-heatmap\"", svg);
    }

    [Fact]
    public void CommitHeatmap_ZeroCommitDaysAreNotLinks()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var series = new (DateOnly Day, int Count)[] { (d1, 2) };
        var commits = Commits((d1, new[] { new CommitInfo("abc1234", "Only change") }));

        var svg = Charts.CommitHeatmap(series, commits);

        // Exactly ONE anchor inside the SVG: the single active day. Zero-commit cells stay unwrapped
        // (no ~100-stop keyboard trap), so the grid contains no other hrefs.
        var svgOnly = svg[..svg.IndexOf("</svg>", StringComparison.Ordinal)];
        Assert.Equal(1, CountOf(svgOnly, "<a href=\"#heat-day-"));
        // The zero-day tooltip is still present for pointer users.
        Assert.Contains(": 0 commits</title>", svgOnly);
    }

    [Fact]
    public void CommitHeatmap_EscapesCommitSubjects()
    {
        var d1 = new DateOnly(2026, 1, 5);
        var series = new (DateOnly Day, int Count)[] { (d1, 1) };
        var commits = Commits((d1, new[] { new CommitInfo("abc1234", "fix <div> & \"quotes\"") }));

        var svg = Charts.CommitHeatmap(series, commits);

        Assert.Contains("fix &lt;div&gt; &amp; &quot;quotes&quot;", svg);
        Assert.DoesNotContain("fix <div>", svg);
    }

    [Fact]
    public void CommitHeatmap_PanelNavSkipsEmptyDaysAndOmitsAtEnds()
    {
        // Three active days with gaps between them — prev/next must hop across the gaps.
        var d1 = new DateOnly(2026, 1, 5);
        var d2 = new DateOnly(2026, 1, 9);
        var d3 = new DateOnly(2026, 1, 14);
        var series = new (DateOnly Day, int Count)[] { (d1, 1), (d2, 1), (d3, 1) };
        var commits = Commits(
            (d1, new[] { new CommitInfo("a1", "one") }),
            (d2, new[] { new CommitInfo("b2", "two") }),
            (d3, new[] { new CommitInfo("c3", "three") }));

        var svg = Charts.CommitHeatmap(series, commits);

        var p1 = PanelOf(svg, "2026-01-05");
        var p2 = PanelOf(svg, "2026-01-09");
        var p3 = PanelOf(svg, "2026-01-14");

        // Earliest: next only, pointing at the adjacent ACTIVE day (not 01-06), naming its date.
        Assert.DoesNotContain("heatmap-day-prev", p1);
        Assert.Contains("href=\"#heat-day-2026-01-09\">Next active day (2026-01-09) &raquo;</a>", p1);
        // Middle: both neighbors.
        Assert.Contains("href=\"#heat-day-2026-01-05\"", p2);
        Assert.Contains("href=\"#heat-day-2026-01-14\"", p2);
        // Latest: previous only.
        Assert.DoesNotContain("heatmap-day-next", p3);
        Assert.Contains("href=\"#heat-day-2026-01-09\"", p3);
        // Every panel carries a Close link even at the ends.
        Assert.Contains("heatmap-day-close", p1);
        Assert.Contains("heatmap-day-close", p3);
    }

    [Fact]
    public void CommitHeatmap_WithoutDetailsRendersNoLinksOrPanels()
    {
        var series = new (DateOnly Day, int Count)[] { (new DateOnly(2026, 1, 5), 3) };

        var svg = Charts.CommitHeatmap(series);

        Assert.DoesNotContain("<a href=\"#heat-day-", svg);
        Assert.DoesNotContain("class=\"heatmap-days\"", svg);
        Assert.DoesNotContain("class=\"heatmap-day\"", svg);
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

    private static string PanelOf(string html, string date)
    {
        var start = html.IndexOf($"id=\"heat-day-{date}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"panel for {date} not found");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
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

        // A real past day is rendered with its tooltip...
        Assert.Contains($"<title>{today.AddDays(-3):yyyy-MM-dd}: 2 commits</title>", svg);
        // ...but tomorrow is never rendered — future days aren't zero-commit days. [Story 1.5 A4]
        Assert.DoesNotContain($"{today.AddDays(1):yyyy-MM-dd}:", svg);
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
        Assert.Contains("last commit 2026-01-07", svg);
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
}
