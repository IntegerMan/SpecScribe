using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the aggregate Git Insights hub page (Story 3.8): the site a11y contract, the
/// accessible server-sorted file table, the file→contributors master-detail (each file links to its
/// contributor panel; the panel answers "who do I talk to about this file?" rather than presenting a global
/// ranking), escaping of repo-derived text, the guarded detail links (link when a resolver produces a target,
/// plain text when not — never a dead link), and friendly empty states.</summary>
public class GitInsightsTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static GitInsightsData SampleInsights() => new(
        Files: new[]
        {
            new FileChangeStat("src/SpecScribe/Charts.cs", 9, 120, 40, "abc1234def", new DateOnly(2026, 7, 6),
                new[]
                {
                    new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 2)),
                }, TotalContributors: 2),
            new FileChangeStat("src/SpecScribe/HtmlTemplater.cs", 4, 33, 12, "fff9999aaa", new DateOnly(2026, 7, 3),
                new[] { new FileContributor("Bob", 4, new DateOnly(2026, 7, 3)) }, TotalContributors: 1),
        },
        Activity: new[]
        {
            (new DateOnly(2026, 7, 2), 3),
            (new DateOnly(2026, 7, 6), 6),
        },
        CommitCount: 9,
        ContributorCount: 2,
        TotalFilesTouched: 2);

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
    public void RenderPage_HasSiteChromeAndBothSections()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\" class=\"deep-page git-insights\">", html);
        Assert.Contains("Git Insights</h1>", html);
        Assert.Contains(">Files &amp; Contributors</h2>", html);
        Assert.Contains(">Activity Over Time</h2>", html);
        Assert.Contains("crumb-current", html); // breadcrumb trail back home
    }

    [Fact]
    public void RenderPage_DisclosesWhenFilesAndContributorsAreTruncated()
    {
        // TotalFilesTouched/TotalContributors exceed what's actually shown, so the page must say so rather
        // than presenting the capped counts as if they were the full totals. [Review fix 2026-07-09]
        var insights = new GitInsightsData(
            Files: new[]
            {
                new FileChangeStat("src/SpecScribe/Charts.cs", 9, 120, 40, "abc1234def", new DateOnly(2026, 7, 6),
                    new[] { new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)) }, TotalContributors: 5),
            },
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 9,
            ContributorCount: 5,
            TotalFilesTouched: 60);

        var html = GitInsightsTemplater.RenderPage(insights, null, Nav());

        Assert.Contains("top 1 of 60 files", html);
        Assert.Contains("and 4 more contributors", html);
    }

    [Fact]
    public void RenderPage_FileTableIsAccessibleAndServerSorted()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Accessible-table contract: a <caption> and <th scope="col"> (EXPERIENCE.md:234).
        Assert.Contains("<caption>Files by change frequency", html);
        Assert.Contains("<th scope=\"col\">File</th>", html);
        // The generation-time sort is announced and is the no-JS reading order: most-changed file first.
        Assert.Contains("aria-sort=\"descending\"", html);
        var charts = html.IndexOf("src/SpecScribe/Charts.cs", StringComparison.Ordinal);
        var templater = html.IndexOf("src/SpecScribe/HtmlTemplater.cs", StringComparison.Ordinal);
        Assert.True(charts >= 0 && templater >= 0 && charts < templater, "files must render change-count desc");
        // The table opts into the client-side enhancement and lives in its own scroll container.
        Assert.Contains("js-sortable", html);
        Assert.Contains("table-scroll", html);
    }

    [Fact]
    public void RenderPage_WholeRowSelectsThePerFileContributorPanel()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // The whole row is the :target select trigger (stretched-link pattern) — not the file name, which is
        // reserved for eventual file-page navigation. The invisible row link covers the row.
        Assert.Contains("<a class=\"gi-row-link\" href=\"#gi-file-0\" aria-label=\"Show contributors for src/SpecScribe/Charts.cs\"></a>", html);
        Assert.Contains("<a class=\"gi-row-link\" href=\"#gi-file-1\"", html);
        // With no file-page resolver, the file name is plain text (the row overlay's click selects it).
        Assert.Contains("<span class=\"gi-file-name\"><code>src/SpecScribe/Charts.cs</code></span>", html);
        Assert.DoesNotContain("gi-file-link", html); // the old file-name-as-drill-link is gone

        Assert.Contains("<div class=\"gi-contributors-panel chart-panel\" id=\"gi-file-0\"", html);
        Assert.Contains("<div class=\"gi-contributors-panel chart-panel\" id=\"gi-file-1\"", html);
        // The panel names the people to talk to about that file, with per-file counts.
        Assert.Contains("People to talk to about this file:", html);
        Assert.Contains("<span class=\"gi-contributor-name\">Alice</span>", html);
        Assert.Contains("<span class=\"gi-contributor-name\">Bob</span>", html);
        // A default prompt covers the "nothing selected yet" state.
        Assert.Contains("gi-detail-default", html);
        Assert.Contains("Select a file to see who has been working on it", html);
    }

    [Fact]
    public void RenderPage_IsNotFramedAsARankingOrScoreboard()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // The redesign is explicitly file-scoped attribution, not a global people ranking.
        Assert.DoesNotContain("leaderboard", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top performer", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("productivity", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Rank<", html);
        // No standalone "Contributor Attribution" section heading anymore — contributors live per file.
        Assert.DoesNotContain(">Contributor Attribution</h2>", html);
    }

    [Fact]
    public void RenderPage_ReusesTheCommitHeatmapForActivity()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav());

        // Activity over time = the existing accessible heatmap (whose active days link to per-day pages).
        // The headline is derived from the SAME pulse data as the heatmap (not insights.Activity), so the
        // two can never disagree — SamplePulse has 1 commit across 1 active day. [Review fix 2026-07-09]
        Assert.Contains("class=\"heatmap\"", html);
        Assert.Contains("commits/2026-07-06.html", html);
        Assert.Contains("1 commit across 1 active day", html);
    }

    [Fact]
    public void RenderPage_EscapesRepoDerivedText()
    {
        var insights = new GitInsightsData(
            Files: new[]
            {
                new FileChangeStat("src/<weird> & \"odd\".cs", 1, 1, 0, "beef123", new DateOnly(2026, 7, 1),
                    new[] { new FileContributor("<b>Eve</b> & Co", 1, new DateOnly(2026, 7, 1)) }, TotalContributors: 1),
            },
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 1,
            ContributorCount: 1,
            TotalFilesTouched: 1);

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

        // No resolvers (7.1/7.4/7.5 unmerged): the file's latest-change hash renders as plain text and no
        // "view file page" link appears — no dead links.
        var unresolved = GitInsightsTemplater.RenderPage(insights, null, Nav());
        Assert.DoesNotContain("href=\"code/", unresolved);
        Assert.DoesNotContain("href=\"commit/", unresolved);
        Assert.DoesNotContain("View file page", unresolved);
        Assert.Contains("<code>abc1234</code>", unresolved); // latest-change short hash, plain

        // The file name is plain text (a span) until a file-page resolver gives it a target.
        Assert.Contains("<span class=\"gi-file-name\">", unresolved);
        Assert.DoesNotContain("<a class=\"gi-file-name\"", unresolved);

        // With resolvers, the file name becomes a navigation link (above the row overlay), the latest-change
        // hash links to its commit page, and the panel gains its own file-page link.
        var resolved = GitInsightsTemplater.RenderPage(
            insights, null, Nav(),
            fileHref: path => path == "src/SpecScribe/Charts.cs" ? "code/src/SpecScribe/Charts.cs.html" : null,
            commitHref: hash => hash == "abc1234def" ? "commit/abc1234.html" : null);
        Assert.Contains("<a class=\"gi-file-name\" href=\"code/src/SpecScribe/Charts.cs.html\"><code>src/SpecScribe/Charts.cs</code></a>", resolved);
        Assert.Contains("<a href=\"commit/abc1234.html\"><code>abc1234</code></a>", resolved);
        Assert.Contains("<a class=\"view-epic-link gi-detail-filelink\" href=\"code/src/SpecScribe/Charts.cs.html\">", resolved);
        // The unresolved second file's name stays a plain span — per-entry guarding, not all-or-nothing.
        Assert.Contains("<span class=\"gi-file-name\"><code>src/SpecScribe/HtmlTemplater.cs</code></span>", resolved);
        Assert.DoesNotContain("href=\"commit/fff9999", resolved);
    }

    [Fact]
    public void RenderPage_EmptyFilesDegradesToFriendlyNoteWithNoMasterDetail()
    {
        var empty = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 0,
            ContributorCount: 0,
            TotalFilesTouched: 0);

        var html = GitInsightsTemplater.RenderPage(empty, null, Nav());

        Assert.Contains("No file change data available.", html);
        Assert.Contains("No activity data available.", html);
        Assert.DoesNotContain("gi-master-detail", html); // no broken/empty master-detail
        Assert.DoesNotContain("<tbody>", html);
    }
}
