using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the aggregate Git Insights hub page (Story 3.8): the site a11y contract, accessible
/// server-sorted tables (caption + th scope), escaping of repo-derived text, the guarded detail links
/// (link when a resolver produces a target, plain text when not — never a dead link), the
/// attribution-not-ranking framing, and friendly empty-section notes.</summary>
public class GitInsightsTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static GitInsightsData SampleInsights() => new(
        Files: new[]
        {
            new FileChangeStat("src/SpecScribe/Charts.cs", 9, 120, 40),
            new FileChangeStat("src/SpecScribe/HtmlTemplater.cs", 4, 33, 12),
        },
        Contributors: new[]
        {
            new ContributorStat("Alice", 7, 5, new DateOnly(2026, 7, 6), "abc1234def"),
            new ContributorStat("Bob", 2, 3, new DateOnly(2026, 7, 2), "fff9999aaa"),
        },
        Activity: new[]
        {
            (new DateOnly(2026, 7, 2), 3),
            (new DateOnly(2026, 7, 6), 6),
        },
        CommitCount: 9);

    private static GitPulse SamplePulse()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[] { new CommitInfo("abc1234", "Fix", "Alice", "10:00") };
        return new GitPulse(
            TotalCommits: 1,
            ActiveDays: 1,
            FirstCommitDate: day,
            LastCommitDate: day,
            DailySeries: new[] { (day, 1) },
            CommitsByDay: new Dictionary<DateOnly, IReadOnlyList<CommitInfo>> { [day] = commits },
            LastCommitTimestamp: new DateTime(2026, 7, 6, 10, 0, 0),
            Last30DayCommitCount: 1,
            TopChangedFiles: Array.Empty<(string, int)>());
    }

    [Fact]
    public void RenderPage_HasSiteChromeAndAllThreeSections()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\" class=\"deep-page git-insights\">", html);
        Assert.Contains("Git Insights</h1>", html);
        Assert.Contains(">File Change Frequency</h2>", html);
        Assert.Contains(">Activity Over Time</h2>", html);
        Assert.Contains(">Contributor Attribution</h2>", html);
        // Breadcrumb trail back home.
        Assert.Contains("Home", html);
        Assert.Contains("crumb-current", html);
    }

    [Fact]
    public void RenderPage_TablesAreAccessibleAndServerSorted()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Accessible-table contract: a <caption> and <th scope="col"> per table (EXPERIENCE.md:234).
        Assert.Contains("<caption>Files by change frequency", html);
        Assert.Contains("<caption>Contributor attribution", html);
        Assert.Contains("<th scope=\"col\">File</th>", html);
        Assert.Contains("<th scope=\"col\">Contributor</th>", html);
        // Contributor names are row headers.
        Assert.Contains("<th scope=\"row\" class=\"gi-contributor\">Alice</th>", html);
        // The generation-time sort is announced (and is the no-JS reading order): most-changed file first.
        Assert.Contains("aria-sort=\"descending\"", html);
        var charts = html.IndexOf("src/SpecScribe/Charts.cs", StringComparison.Ordinal);
        var templater = html.IndexOf("src/SpecScribe/HtmlTemplater.cs", StringComparison.Ordinal);
        Assert.True(charts >= 0 && templater >= 0 && charts < templater, "files must render change-count desc");
        // Tables opt into the client-side enhancement and live in their own scroll container.
        Assert.Contains("js-sortable", html);
        Assert.Contains("table-scroll", html);
    }

    [Fact]
    public void RenderPage_ReusesTheCommitHeatmapForActivity()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Activity over time = the existing accessible heatmap (whose active days link to per-day pages),
        // not a new parallel time chart.
        Assert.Contains("class=\"heatmap\"", html);
        Assert.Contains("commits/2026-07-06.html", html);
        // The window summary is derived from the deep activity series.
        Assert.Contains("9 commits across 2 active days", html);
    }

    [Fact]
    public void RenderPage_EscapesRepoDerivedText()
    {
        var insights = new GitInsightsData(
            Files: new[] { new FileChangeStat("src/<weird> & \"odd\".cs", 1, 1, 0) },
            Contributors: new[] { new ContributorStat("<b>Eve</b> & Co", 1, 1, new DateOnly(2026, 7, 1), "beef123") },
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 1);

        var html = GitInsightsTemplater.RenderPage(insights, null, Nav());

        Assert.Contains("src/&lt;weird&gt; &amp; &quot;odd&quot;.cs", html);
        Assert.Contains("&lt;b&gt;Eve&lt;/b&gt; &amp; Co", html);
        Assert.DoesNotContain("<weird>", html);
        Assert.DoesNotContain("<b>Eve</b>", html);
    }

    [Fact]
    public void RenderPage_GuardsDetailLinksOnTargetExistence()
    {
        var insights = SampleInsights();

        // No resolvers (7.1/7.4/7.5 unmerged): every file/commit renders as plain escaped text — no dead links.
        var unresolved = GitInsightsTemplater.RenderPage(insights, null, Nav());
        Assert.DoesNotContain("href=\"code/", unresolved);
        Assert.DoesNotContain("href=\"commit/", unresolved);
        Assert.Contains("<code>src/SpecScribe/Charts.cs</code>", unresolved);
        Assert.Contains("<code>abc1234</code>", unresolved); // short-hash display, plain

        // With resolvers, the same cells become links to the resolved targets.
        var resolved = GitInsightsTemplater.RenderPage(
            insights, null, Nav(),
            fileHref: path => path == "src/SpecScribe/Charts.cs" ? "code/src/SpecScribe/Charts.cs.html" : null,
            commitHref: hash => hash == "abc1234def" ? "commit/abc1234.html" : null);
        Assert.Contains("<a href=\"code/src/SpecScribe/Charts.cs.html\"><code>src/SpecScribe/Charts.cs</code></a>", resolved);
        Assert.Contains("<a href=\"commit/abc1234.html\"><code>abc1234</code></a>", resolved);
        // The unresolved file in the same table stays plain text (per-entry guarding, not all-or-nothing).
        Assert.Contains("<code>src/SpecScribe/HtmlTemplater.cs</code>", resolved);
        Assert.DoesNotContain("href=\"code/src/SpecScribe/HtmlTemplater.cs.html\"", resolved);
    }

    [Fact]
    public void RenderPage_FramesContributorsAsAttributionNotRanking()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // The PRD boundary: attribution/collaboration context in, leaderboards/scores out.
        Assert.Contains("Contributor Attribution", html);
        Assert.Contains("not a scoreboard", html);
        Assert.DoesNotContain("leaderboard", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top performer", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("productivity", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Rank<", html);
    }

    [Fact]
    public void RenderPage_EmptySectionsDegradeToFriendlyNotes()
    {
        var empty = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Contributors: Array.Empty<ContributorStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 0);

        var html = GitInsightsTemplater.RenderPage(empty, null, Nav());

        Assert.Contains("No file change data available.", html);
        Assert.Contains("No contributor data available.", html);
        Assert.Contains("No activity data available.", html);
        Assert.DoesNotContain("<tbody>", html); // no broken/empty tables
    }
}
