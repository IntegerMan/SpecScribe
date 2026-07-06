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
    public void EpicSunburst_SegmentLinksCarryAriaLabels()
    {
        var epic = Epic(Story("1.1", "A story", "done", done: 3, total: 3));

        var svg = Charts.EpicSunburst(epic, _ => "epics/epic-1.html");

        Assert.Contains("aria-label=\"Story 1.1: A story — done\"", svg);
        Assert.Contains("aria-label=\"Story 1.1: 3 of 3 tasks done\"", svg);
        Assert.Contains("role=\"img\"", svg); // whole-chart name retained
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
    public void CommitHeatmap_CarriesRoleImgAndName()
    {
        var series = new (DateOnly Day, int Count)[]
        {
            (new DateOnly(2026, 1, 5), 3),
            (new DateOnly(2026, 1, 7), 1),
        };

        var svg = Charts.CommitHeatmap(series);

        Assert.Contains("role=\"img\"", svg);
        Assert.Contains("aria-label=\"Commit activity: 4 commits across 2 active days, 2026-01-05–2026-01-07\"", svg);
        // Per-cell tooltips remain for pointer users.
        Assert.Contains("<title>2026-01-05: 3 commits</title>", svg);
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
