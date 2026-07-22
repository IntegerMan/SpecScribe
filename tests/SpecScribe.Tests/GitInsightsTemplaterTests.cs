using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the aggregate Git Insights hub page: the site a11y contract, the whole-tree code-ownership
/// sunburst + its accessible text-equivalent tree (Story 7.11 rewrite — replaces the earlier files-and-contributors
/// master-detail table AND the earlier plain ranked ownership table), escaping of repo-derived text, the guarded
/// file links (link when a resolver produces a target, plain text when not — never a dead link), the solo-repo
/// reframe, and friendly empty states.</summary>
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

    /// <summary>The whole-tree CodeMap the ownership sunburst/tree render from — mirrors what
    /// <c>CodeMap.Build(_codeFiles, DeepGitPulse.CodeMapMetrics)</c> produces in the generator. Charts.cs: 9
    /// changes, Alice 7 -> 78% dominant share, 2 contributors (multi-author). HtmlTemplater.cs: 4 changes, Bob
    /// 4 -> 100% dominant share, 1 contributor (sole).</summary>
    private static CodeMap SampleCodeMap() => CodeMap.Build(
        new (string RepoRelativePath, long Lines)[]
        {
            ("src/SpecScribe/Charts.cs", 100),
            ("src/SpecScribe/HtmlTemplater.cs", 50),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/SpecScribe/Charts.cs"] = new CodeFileMetrics(9, 160, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 6),
                Contributors: new[]
                {
                    new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 2)),
                }, TotalContributors: 2),
            ["src/SpecScribe/HtmlTemplater.cs"] = new CodeFileMetrics(4, 45, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                Contributors: new[] { new FileContributor("Bob", 4, new DateOnly(2026, 7, 3)) }, TotalContributors: 1),
        });

    private static IReadOnlyList<string> SampleTopAuthors() => new[] { "Alice", "Bob" };

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
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\" class=\"deep-page git-insights\">", html);
        Assert.Contains("Git Insights</h1>", html);
        Assert.Contains(">Code Ownership &amp; Bus-Factor</h2>", html);
        Assert.Contains(">Activity Over Time</h2>", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.CodeOwnership), html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.ActivityCadence), html);
        Assert.DoesNotContain("deep-page-lead", html);
        Assert.Contains("crumb-current", html); // breadcrumb trail back home
    }

    [Fact]
    public void RenderPage_DisclosesWhenTheFileCountPillIsTruncated()
    {
        // Insights.TotalFilesTouched exceeds Files.Count, so the header pill must say so rather than presenting
        // the capped count as the full total. [Review fix 2026-07-09, still load-bearing after the 7.11 rewrite]
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

        var html = GitInsightsTemplater.RenderPage(insights, null, Nav(), SampleCodeMap(), SampleTopAuthors());

        Assert.Contains("top 1 of 60 files by commit count", html);
    }

    [Fact]
    public void RenderPage_RendersTheWholeTreeSunburstAndItsRealValueLegend()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        Assert.Contains("<svg class=\"ownership-sunburst\"", html);
        Assert.Contains("ownership-wedge", html);
        // Real-value legend (Story 10.2) — never the literal "Less … More" placeholder.
        Assert.Contains("ownership-legend", html);
        Assert.Contains("76–100%", html);
        Assert.DoesNotContain("Less", html);
        Assert.DoesNotContain("…More", html);
    }

    [Fact]
    public void RenderPage_SunburstEmbedsGenerationTimeDataForTheLiveModeSwitcher()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        // ADR 0010 Task 4: every mode's data is embedded once at generation time — share/dominant/contributors/
        // last/owner per wedge, plus the bounded top-author roster and the whole-tree "as of" day on the SVG root.
        Assert.Contains("data-share=\"78\"", html); // Charts.cs: Alice 7/9 -> 78%
        Assert.Contains("data-share=\"100\"", html); // HtmlTemplater.cs: Bob 4/4 -> 100%
        Assert.Contains("data-dominant=\"Alice\"", html);
        Assert.Contains("data-dominant=\"Bob\"", html);
        Assert.Contains("data-contributors=\"2\"", html);
        Assert.Contains("data-owner=", html);
        Assert.Contains("data-top-authors=", html);
        Assert.Contains("data-asof=", html);
    }

    [Fact]
    public void RenderPage_ModeSelectorControlsShipHiddenForTheNoJsBaseline()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        // NFR-5/ADR 0010: no inert control ships in the no-JS page — specscribe.js reveals it.
        Assert.Contains("<div class=\"ownership-controls\" hidden>", html);
        Assert.Contains("ownership-mode-select", html);
        Assert.Contains("<label class=\"ownership-author-wrap\" hidden>", html);
        Assert.Contains("<label class=\"ownership-threshold-wrap\" hidden>", html);
    }

    [Fact]
    public void RenderPage_TextEquivalentTreeCarriesEveryFilesDominantAuthorShareContributorsAndLastActive()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        Assert.Contains("<ul class=\"ownership-tree\">", html);
        Assert.Contains("ownership-tree-file", html);
        Assert.Contains("Alice 78%", html);
        Assert.Contains("Bob 100%", html);
        Assert.Contains("2 contributors", html);
        Assert.Contains("1 contributor", html);
        Assert.Contains("last active", html);
    }

    [Fact]
    public void RenderPage_IsNotFramedAsARankingOrScoreboard()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

        // FR-10: descriptive attribution only, never a cross-repo people ranking — in every mode, including
        // the spotlight (recolorSpotlight answers "where has this person worked", never "who did the most").
        Assert.DoesNotContain("leaderboard", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top performer", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("productivity", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Rank<", html);
    }

    [Fact]
    public void RenderPage_ReusesTheCommitHeatmapForActivity()
    {
        var html = GitInsightsTemplater.RenderPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());

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
        var codeMap = CodeMap.Build(
            new (string, long)[] { ("src/<weird> & \"odd\".cs", 10) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/<weird> & \"odd\".cs"] = new CodeFileMetrics(1, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
                    Contributors: new[] { new FileContributor("<b>Eve</b> & Co", 1, new DateOnly(2026, 7, 1)) }, TotalContributors: 1),
            });
        var insights = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 1,
            ContributorCount: 2, // >1 so the solo-repo reframe doesn't short-circuit the section under test
            TotalFilesTouched: 1);

        var html = GitInsightsTemplater.RenderPage(insights, null, Nav(), codeMap, Array.Empty<string>());

        Assert.Contains("src/&lt;weird&gt; &amp; &quot;odd&quot;.cs", html);
        Assert.Contains("&lt;b&gt;Eve&lt;/b&gt; &amp; Co", html);
        Assert.DoesNotContain("<weird>", html);
        Assert.DoesNotContain("<b>Eve</b>", html);
    }

    [Fact]
    public void RenderPage_GuardsFileLinksOnTargetExistence()
    {
        var insights = SampleInsights();
        var codeMap = SampleCodeMap();

        // No resolver: every file link stays plain text/no href — no dead links.
        var unresolved = GitInsightsTemplater.RenderPage(insights, null, Nav(), codeMap, SampleTopAuthors());
        Assert.DoesNotContain("href=\"code/", unresolved);

        // With a resolver, the resolved file's wedge/tree entry becomes a real link; the unresolved file stays
        // plain text — per-entry guarding, not all-or-nothing.
        var resolved = GitInsightsTemplater.RenderPage(
            insights, null, Nav(), codeMap, SampleTopAuthors(),
            fileHref: path => path == "src/SpecScribe/Charts.cs" ? "code/src/SpecScribe/Charts.cs.html" : null);
        Assert.Contains("href=\"code/src/SpecScribe/Charts.cs.html\"", resolved);
        Assert.Contains("src/SpecScribe/HtmlTemplater.cs", resolved);
        Assert.DoesNotContain("href=\"code/src/SpecScribe/HtmlTemplater.cs", resolved);
    }

    // ---- Story 7.11: solo-repo reframe (AC #4) ----

    [Fact]
    public void RenderPage_SoloRepoOwnershipReframesInsteadOfAnAllFlaggedSunburst()
    {
        var codeMap = CodeMap.Build(
            new (string, long)[] { ("src/A.cs", 10), ("src/B.cs", 5) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
                    Contributors: new[] { new FileContributor("Alice", 5, new DateOnly(2026, 7, 1)) }, TotalContributors: 1),
                ["src/B.cs"] = new CodeFileMetrics(3, 4, new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 2),
                    Contributors: new[] { new FileContributor("Alice", 3, new DateOnly(2026, 7, 2)) }, TotalContributors: 1),
            });
        var insights = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 8,
            ContributorCount: 1,
            TotalFilesTouched: 2);

        var html = GitInsightsTemplater.RenderPage(insights, null, Nav(), codeMap, new[] { "Alice" });

        Assert.Contains("Single-maintainer project", html);
        Assert.Contains("gi-solo-repo-note", html);
        // No sunburst/mode-selector in the solo case — that would flag every wedge at-risk, noise not signal.
        Assert.DoesNotContain("ownership-sunburst", html);
        Assert.DoesNotContain("ownership-controls", html);
    }

    [Fact]
    public void RenderPage_OwnershipSectionDegradesToFriendlyNoteWhenCodeMapIsEmpty()
    {
        var empty = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 0,
            ContributorCount: 0,
            TotalFilesTouched: 0);

        var html = GitInsightsTemplater.RenderPage(empty, null, Nav(), CodeMap.Empty, Array.Empty<string>());

        Assert.Contains("No file change data available.", html);
        Assert.Contains("No activity data available.", html);
        Assert.DoesNotContain("ownership-sunburst", html);
        Assert.DoesNotContain("<tbody>", html);
    }
}
