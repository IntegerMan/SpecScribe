using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 21.3's presentation layer: the <see cref="Charts.ChartMetric.PlanningCodeImpact"/>
/// framing sentence + note constant, and <see cref="Charts.ImpactMapBody"/> (the epic-grouped linked-file list on
/// the dedicated page). The data is constructed directly (the correlation logic itself is exercised in
/// <see cref="PlanningCodeImpactTests"/>), so these assert markup, honesty framing, and the empty-state degrade.</summary>
public class ChartsPlanningImpactTests
{
    private static EpicsModel Model(params EpicInfo[] epics) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = epics,
    };

    private static EpicInfo Epic(int number, string title) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = Array.Empty<StoryInfo>(),
    };

    [Fact]
    public void WhyText_PlanningCodeImpact_IsNonEmptyAndDistinct()
    {
        var why = Charts.WhyText(Charts.ChartMetric.PlanningCodeImpact);
        Assert.False(string.IsNullOrWhiteSpace(why));

        // Distinct from every other metric's sentence (Story 10.2 — one framing per metric, no copy reuse).
        foreach (Charts.ChartMetric other in Enum.GetValues<Charts.ChartMetric>())
        {
            if (other == Charts.ChartMetric.PlanningCodeImpact) continue;
            Assert.NotEqual(Charts.WhyText(other), why);
        }
    }

    [Fact]
    public void PlanningCodeImpactNote_CarriesBestEffortCaveat()
    {
        Assert.Contains("best-effort", Charts.PlanningCodeImpactNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approximate", Charts.PlanningCodeImpactNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImpactMapBody_EmptyData_ShowsHonestEmptyStateNotAGrid()
    {
        var html = Charts.ImpactMapBody(Model(Epic(1, "Foundation")), PlanningCodeImpactData.Empty, prefix: "");
        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<ul", html);
    }

    [Fact]
    public void ImpactMapBody_RendersEpicHeadingLinkAndFileLinks()
    {
        var data = new PlanningCodeImpactData(
            new Dictionary<int, IReadOnlyList<ImpactFile>>
            {
                [7] = new[] { new ImpactFile("src/GitMetrics.cs", "code/src/GitMetrics.cs.html", Churn: 42, Commits: 3) },
            },
            new Dictionary<string, IReadOnlyList<ImpactFile>>(),
            AttributedCommitCount: 3,
            TotalAnalyzedCommits: 10);

        var html = Charts.ImpactMapBody(Model(Epic(7, "Insights")), data, prefix: "");

        Assert.Contains("href=\"epics/epic-7.html\"", html);
        Assert.Contains("Epic 7", html);
        Assert.Contains("href=\"code/src/GitMetrics.cs.html\"", html);
        Assert.Contains("src/GitMetrics.cs", html);
    }

    [Fact]
    public void ImpactMapBody_AppliesPagePrefixToHrefs()
    {
        var data = new PlanningCodeImpactData(
            new Dictionary<int, IReadOnlyList<ImpactFile>>
            {
                [1] = new[] { new ImpactFile("src/A.cs", "code/src/A.cs.html", Churn: 10, Commits: 1) },
            },
            new Dictionary<string, IReadOnlyList<ImpactFile>>(),
            1, 1);

        var html = Charts.ImpactMapBody(Model(Epic(1, "Foundation")), data, prefix: "../");
        Assert.Contains("href=\"../code/src/A.cs.html\"", html);
        Assert.Contains("href=\"../epics/epic-1.html\"", html);
    }

    [Fact]
    public void ImpactMapBody_OmitsEpicWithNoFiles()
    {
        var data = new PlanningCodeImpactData(
            new Dictionary<int, IReadOnlyList<ImpactFile>>
            {
                [1] = new[] { new ImpactFile("src/A.cs", "code/src/A.cs.html", Churn: 10, Commits: 1) },
            },
            new Dictionary<string, IReadOnlyList<ImpactFile>>(),
            1, 1);

        // Epic 2 has no entry in FilesByEpic → it must not appear as a section.
        var html = Charts.ImpactMapBody(Model(Epic(1, "One"), Epic(2, "Two")), data, prefix: "");
        Assert.Contains("epics/epic-1.html", html);
        Assert.DoesNotContain("epics/epic-2.html", html);
    }
}
