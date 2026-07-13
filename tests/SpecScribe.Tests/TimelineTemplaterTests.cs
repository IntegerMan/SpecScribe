using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the chronological activity timeline page: the a11y shell, per-day rows linking to their
/// date pages with the right newest-first summaries, the reused heatmap (present only with git), and graceful
/// degradation for git-absent and empty-union inputs.</summary>
public class TimelineTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> NoArtifacts
        = new Dictionary<DateOnly, IReadOnlyList<(string Label, string Href)>>();
    private static readonly IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> NoCommits
        = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>();

    private static GitPulse PulseFor(params (DateOnly Day, int Count)[] series)
    {
        var byDay = series.ToDictionary(
            s => s.Day,
            s => (IReadOnlyList<CommitInfo>)Enumerable.Range(0, s.Count)
                .Select(i => new CommitInfo($"h{i}", "subject", "Alice", "12:00")).ToList());
        var ordered = series.OrderBy(s => s.Day).ToList();
        return new GitPulse(
            TotalCommits: series.Sum(s => s.Count),
            ActiveDays: series.Length,
            FirstCommitDate: ordered[0].Day,
            LastCommitDate: ordered[^1].Day,
            DailySeries: ordered,
            CommitsByDay: byDay,
            LastCommitTimestamp: DateTime.Now,
            Last30DayCommitCount: series.Sum(s => s.Count),
            TopChangedFiles: Array.Empty<(string, int)>());
    }

    [Fact]
    public void RenderPage_HasA11yShellAndTimelineHeading()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var git = PulseFor((today, 2));

        var html = TimelineTemplater.RenderPage(git, new[] { today }, git.CommitsByDay, NoArtifacts, Nav());

        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Contains("<title>Activity Timeline — SpecScribe</title>", html);
        Assert.Contains("<h1>Activity Timeline</h1>", html);
        Assert.Contains("crumb-current", html);   // Home / Timeline breadcrumb
        Assert.Contains(">Timeline<", html);
    }

    [Fact]
    public void RenderPage_RowsLinkToDatePages_NewestFirst_WithSummaries()
    {
        var d1 = new DateOnly(2026, 7, 4);
        var d2 = new DateOnly(2026, 7, 6);
        var git = PulseFor((d1, 1), (d2, 3));
        var artifacts = ActivityModel.GroupArtifactsByDay(new[]
        {
            (d2, "Epics", "epics.html"),
            (d2, "PRD", "planning-artifacts/PRD.html"),
        });
        var daysNewestFirst = new[] { d2, d1 };

        var html = TimelineTemplater.RenderPage(git, daysNewestFirst, git.CommitsByDay, artifacts, Nav());

        // Root-level page → date links are relative to root (commits/…), not ../.
        Assert.Contains($"class=\"timeline-date\" href=\"commits/{Charts.D(d2)}.html\">{Charts.DReadable(d2)}</a>", html);
        Assert.Contains($"class=\"timeline-date\" href=\"commits/{Charts.D(d1)}.html\">{Charts.DReadable(d1)}</a>", html);
        // Summaries combine commits + artifacts; singular/plural correct.
        Assert.Contains("3 commits &middot; 2 artifacts updated", html);
        Assert.Contains("1 commit", html);
        // Newest-first: d2's list row appears before d1's (checked on the timeline-date links, not the
        // heatmap — which lists both dates ascending).
        var rowD2 = html.IndexOf($"timeline-date\" href=\"commits/{Charts.D(d2)}.html", StringComparison.Ordinal);
        var rowD1 = html.IndexOf($"timeline-date\" href=\"commits/{Charts.D(d1)}.html", StringComparison.Ordinal);
        Assert.True(rowD2 >= 0 && rowD1 >= 0 && rowD2 < rowD1, "newest date row should come first");
    }

    [Fact]
    public void RenderPage_GitPresent_RendersHeatmap()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var git = PulseFor((today.AddDays(-2), 1), (today, 2));

        var html = TimelineTemplater.RenderPage(git, new[] { today, today.AddDays(-2) }, git.CommitsByDay, NoArtifacts, Nav());

        Assert.Contains("class=\"heatmap\"", html);
        Assert.Contains("timeline-heatmap", html);
    }

    [Fact]
    public void RenderPage_GitAbsent_NoHeatmap_ButArtifactListStillRenders()
    {
        var day = new DateOnly(2026, 7, 6);
        var artifacts = ActivityModel.GroupArtifactsByDay(new[] { (day, "Architecture", "planning-artifacts/ARCHITECTURE.html") });

        var html = TimelineTemplater.RenderPage(git: null, new[] { day }, NoCommits, artifacts, Nav());

        Assert.DoesNotContain("class=\"heatmap\"", html);
        Assert.DoesNotContain("timeline-heatmap", html);
        // The artifact-driven list still renders the day row + summary.
        Assert.Contains($"href=\"commits/{Charts.D(day)}.html\"", html);
        Assert.Contains("1 artifact updated", html);
    }

    [Fact]
    public void RenderPage_EmptyUnion_RendersFriendlyNote_NoList()
    {
        var html = TimelineTemplater.RenderPage(git: null, Array.Empty<DateOnly>(), NoCommits, NoArtifacts, Nav());

        Assert.Contains("No activity to show yet.", html);
        Assert.DoesNotContain("timeline-list", html);
    }
}
