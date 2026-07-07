using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the generated per-day commit page: readable heading, per-commit rows
/// (hash/time/author/subject, escaped), prev/next active-day links, and the site a11y contract.</summary>
public class CommitDayTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static CommitInfo C(string hash, string subject, string author = "Alice", string time = "14:32") =>
        new(hash, subject, author, time);

    [Fact]
    public void RenderPage_ShowsReadableTitleAndCommitRows()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[]
        {
            C("abc1234", "Fix the thing", "Matt Eland", "09:15"),
            C("def5678", "Add the other thing"),
        };

        var html = CommitDayTemplater.RenderPage(day, commits, null, null, Nav());

        Assert.Contains($"<title>Commits on {Charts.DReadable(day)} — SpecScribe</title>", html);
        Assert.Contains($"<h1>Commits on {Charts.DReadable(day)}</h1>", html);
        Assert.Contains("<code class=\"commit-hash\">abc1234</code>", html);
        Assert.Contains("<span class=\"commit-time\">09:15</span>", html);
        Assert.Contains("<span class=\"commit-author\">Matt Eland</span>", html);
        Assert.Contains("<span class=\"commit-subject\">Fix the thing</span>", html);
        // Site a11y contract: skip-link first, single main landmark.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
    }

    [Fact]
    public void RenderPage_EscapesCommitFields()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[] { C("abc1234", "fix <div> & \"q\"", author: "<b>Bob</b>") };

        var html = CommitDayTemplater.RenderPage(day, commits, null, null, Nav());

        Assert.Contains("fix &lt;div&gt; &amp; &quot;q&quot;", html);
        Assert.Contains("&lt;b&gt;Bob&lt;/b&gt;", html);
        Assert.DoesNotContain("fix <div>", html);
    }

    [Fact]
    public void RenderPage_PrevNextLinkAdjacentDaysWithIsoHrefAndReadableText()
    {
        var day = new DateOnly(2026, 7, 6);
        var prev = new DateOnly(2026, 7, 4);
        var next = new DateOnly(2026, 7, 9);

        var html = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, prev, next, Nav());

        // Sibling hrefs stay ISO (bare filename in the same commits/ dir); link text is readable.
        Assert.Contains("class=\"commit-day-prev\" href=\"2026-07-04.html\"", html);
        Assert.Contains(Charts.DReadable(prev), html);
        Assert.Contains("class=\"commit-day-next\" href=\"2026-07-09.html\"", html);
        Assert.Contains(Charts.DReadable(next), html);
    }

    [Fact]
    public void RenderPage_OmitsPrevAtEarliestAndNextAtLatest()
    {
        var day = new DateOnly(2026, 7, 6);

        var earliest = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, null, new DateOnly(2026, 7, 9), Nav());
        Assert.DoesNotContain("commit-day-prev", earliest);
        Assert.Contains("commit-day-next", earliest);

        var latest = CommitDayTemplater.RenderPage(day, new[] { C("a", "x") }, new DateOnly(2026, 7, 4), null, Nav());
        Assert.Contains("commit-day-prev", latest);
        Assert.DoesNotContain("commit-day-next", latest);
    }
}
