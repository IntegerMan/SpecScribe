using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the in-portal code file page (Story 7.1): the a11y shell, the locked <c>id="L{n}"</c>
/// line-anchor convention, 1:1 numbering (blank lines included), HTML escaping, and the placeholder page.</summary>
public class CodeFileTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private const string OutputPath = "code/src/SpecScribe/Sample.cs.html";
    private const string RepoRelative = "src/SpecScribe/Sample.cs";

    [Fact]
    public void RenderPage_RendersTitleBreadcrumbAndA11yShell()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());

        Assert.Contains($"<title>{RepoRelative} — SpecScribe</title>", html);
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<div class=\"story-kicker\">Source File</div>", html);
        // Site a11y contract: skip-link first, single main landmark.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        // Breadcrumb: Home / <file path>. The nested page's Home link carries the correct ../ depth prefix.
        Assert.Contains("Home", html);
        var skipIndex = html.IndexOf("skip-link", StringComparison.Ordinal);
        var mainIndex = html.IndexOf("id=\"main-content\"", StringComparison.Ordinal);
        Assert.True(skipIndex >= 0 && skipIndex < mainIndex, "skip-link must precede the main landmark");
        // Exactly one main landmark.
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderPage_EmitsOneAnchoredLinePerSourceLineNumberedFromOne()
    {
        var lines = new[] { "line one", "line two", "line three" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.DoesNotContain("id=\"L4\"", html);
        // Count matches the input line count exactly (1:1).
        Assert.Equal(lines.Length, CountOccurrences(html, "class=\"code-line\""));
        // The gutter carries the 1-based number and the source text sits in its own span.
        Assert.Contains("<span class=\"code-ln\">1</span>", html);
        Assert.Contains("<span class=\"code-src\">line one</span>", html);
        // Line-count meta pill.
        Assert.Contains("<span class=\"pill\">3 lines</span>", html);
    }

    [Fact]
    public void RenderPage_BlankLineStillEmitsAnchoredRowSoNumberingStays1To1()
    {
        var lines = new[] { "before", "", "after" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        // Three lines, three anchors — the blank middle line is not collapsed away.
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.Equal(3, CountOccurrences(html, "class=\"code-line\""));
        // The blank line renders an empty (but present) source span.
        Assert.Contains("<span class=\"code-ln\">2</span><span class=\"code-src\"></span>", html);
    }

    [Fact]
    public void RenderPage_EscapesHtmlMetacharactersInSource()
    {
        var lines = new[] { "if (a < b && c > d) return \"x\";" };

        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, lines, Nav());

        Assert.Contains("if (a &lt; b &amp;&amp; c &gt; d) return &quot;x&quot;;", html);
        // The raw, unescaped angle bracket form must never reach the output.
        Assert.DoesNotContain("a < b", html);
    }

    [Fact]
    public void RenderPage_SingleLineUsesSingularPill()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "only" }, Nav());

        Assert.Contains("<span class=\"pill\">1 line</span>", html);
    }

    [Fact]
    public void RenderPlaceholder_RendersShellAndReasonWithoutLineTable()
    {
        var html = CodeFileTemplater.RenderPlaceholder(RepoRelative, OutputPath, "This file is too large to render inline.", Nav());

        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<p class=\"code-placeholder\">This file is too large to render inline.</p>", html);
        // No line table on a placeholder page.
        Assert.DoesNotContain("class=\"code-line\"", html);
        Assert.Contains("<span class=\"pill\">Not rendered</span>", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
