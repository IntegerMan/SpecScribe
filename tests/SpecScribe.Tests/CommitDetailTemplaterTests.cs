using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the generated per-commit detail page (Story 7.5): the four AC-#1 signals (subject,
/// author+date attribution, body prose, files-changed table with churn), the site a11y contract, escaping of
/// every free-text git field, partial/binary degradation, and the guarded file link.</summary>
public class CommitDetailTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static DeepCommit Commit(
        string hash = "abc1234def567",
        string author = "Matt Eland",
        DateTime? timestamp = null,
        string subject = "Fix the thing",
        string body = "A longer explanation.",
        params DeepFileChange[] files)
        => new(hash, author, timestamp ?? new DateTime(2026, 7, 6, 9, 15, 0), subject, body,
               files.Length > 0 ? files : new[] { new DeepFileChange("src/A.cs", 3, 1) });

    [Fact]
    public void RenderPage_RendersTheFourSignals()
    {
        var commit = Commit(
            subject: "Fix the thing",
            body: "First paragraph.\n\nSecond paragraph.",
            files: new[] { new DeepFileChange("src/A.cs", 3, 1), new DeepFileChange("src/B.cs", 10, 0) });

        var html = CommitDetailTemplater.RenderPage(commit, Nav());

        // Subject as <h1>.
        Assert.Contains("<h1>Fix the thing</h1>", html);
        // Author + date attribution pill, framed as attribution (never a rank).
        Assert.Contains("class=\"pill commit-attribution\">by Matt Eland", html);
        Assert.Contains(Charts.DReadable(new DateOnly(2026, 7, 6)), html);
        Assert.Contains("at 09:15", html);
        // Body prose with paragraph breaks preserved → multiple <p>.
        Assert.Contains("<p>First paragraph.</p>", html);
        Assert.Contains("<p>Second paragraph.</p>", html);
        // Files table with per-file +added / −deleted.
        Assert.Contains("<code>src/A.cs</code>", html);
        Assert.Contains("+3", html);
        Assert.Contains("&minus;1", html);
        Assert.Contains("<code>src/B.cs</code>", html);
        Assert.Contains("+10", html);
        Assert.Contains("&minus;0", html);
    }

    [Fact]
    public void RenderPage_PreservesSingleNewlinesAsLineBreaksWithinAParagraph()
    {
        var html = CommitDetailTemplater.RenderPage(Commit(body: "Line A\nLine B"), Nav());
        Assert.Contains("Line A<br>", html);
        Assert.Contains("Line B", html);
    }

    [Fact]
    public void RenderPage_HonorsTheSiteA11yContract()
    {
        var html = CommitDetailTemplater.RenderPage(Commit(), Nav());

        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\" class=\"commit-detail\">", html);
        // Exactly one main landmark.
        Assert.Equal(1, CountOccurrences(html, "<main id=\"main-content\""));
        // Breadcrumb to Home, then the short hash.
        Assert.Contains("Commit abc1234", html);
        // The files table carries a caption (a11y).
        Assert.Contains("<caption>Files changed in this commit", html);
    }

    [Fact]
    public void RenderPage_EscapesEveryFreeTextGitField()
    {
        var commit = Commit(
            author: "<b>Bob</b>",
            subject: "fix <div> & \"q\"",
            body: "body <script>alert(1)</script> & more",
            files: new[] { new DeepFileChange("src/<evil>.cs", 1, 0) });

        var html = CommitDetailTemplater.RenderPage(commit, Nav());

        Assert.Contains("fix &lt;div&gt; &amp; &quot;q&quot;", html);
        Assert.Contains("&lt;b&gt;Bob&lt;/b&gt;", html);
        Assert.Contains("body &lt;script&gt;alert(1)&lt;/script&gt; &amp; more", html);
        Assert.Contains("&lt;evil&gt;", html);
        // No raw markup from any field survived.
        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("fix <div>", html);
        Assert.DoesNotContain("<b>Bob</b>", html);
    }

    [Fact]
    public void RenderPage_BinaryRowShowsMarkerNotZeroChurn()
    {
        var commit = Commit(files: new[] { new DeepFileChange("assets/logo.png", null, null) });

        var html = CommitDetailTemplater.RenderPage(commit, Nav());

        Assert.Contains("assets/logo.png", html);
        Assert.Contains("commit-file-binary", html);
        // A binary row never prints +0 / −0.
        Assert.DoesNotContain("+0", html);
        Assert.DoesNotContain("&minus;0", html);
    }

    [Fact]
    public void RenderPage_EmptyBodyOmitsTheProseBlock()
    {
        var html = CommitDetailTemplater.RenderPage(Commit(body: "   "), Nav());
        Assert.DoesNotContain("commit-message", html);
        // No empty paragraph left behind.
        Assert.DoesNotContain("<p></p>", html);
    }

    [Fact]
    public void RenderPage_NullTimestampShowsAuthorWithoutDate()
    {
        var commit = new DeepCommit("abc1234def567", "Solo Author", null, "Subject", "",
            new[] { new DeepFileChange("src/A.cs", 1, 0) });

        var html = CommitDetailTemplater.RenderPage(commit, Nav());

        // Attribution is author-only when the timestamp didn't parse — no date/time, no crash.
        Assert.Contains("<span class=\"pill commit-attribution\">by Solo Author</span>", html);
    }

    [Fact]
    public void RenderPage_MissingSubjectDegradesWithoutEmptyHeading()
    {
        var html = CommitDetailTemplater.RenderPage(Commit(subject: ""), Nav());
        Assert.Contains("<h1>(no subject)</h1>", html);
        Assert.DoesNotContain("<h1></h1>", html);
    }

    [Fact]
    public void RenderPage_FileLinkIsGuardedOnResolverAvailability()
    {
        var commit = Commit(files: new[]
        {
            new DeepFileChange("src/A.cs", 1, 0),
            new DeepFileChange("src/B.cs", 2, 0),
        });

        // Resolver returns a root-relative code page only for A.cs; the templater prepends its own "../" prefix.
        string? Resolver(string path) => path == "src/A.cs" ? "code/src/A.cs.html" : null;

        var html = CommitDetailTemplater.RenderPage(commit, Nav(), Resolver);

        Assert.Contains("href=\"../code/src/A.cs.html\"><code>src/A.cs</code></a>", html);
        // B.cs has no page → plain <code>, never a dead link.
        Assert.Contains("<code>src/B.cs</code>", html);
        Assert.DoesNotContain("href=\"../code/src/B.cs.html\"", html);
    }

    [Fact]
    public void RenderPage_NullResolverRendersAllPathsPlain()
    {
        var html = CommitDetailTemplater.RenderPage(Commit(), Nav());
        Assert.Contains("<code>src/A.cs</code>", html);
        Assert.DoesNotContain("<a href=\"../code/", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
