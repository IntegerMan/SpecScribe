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

    private static readonly (string OutputUrl, string Title)[] Refs =
    {
        ("epics/story-7-1.html", "Story 7.1: In-Portal Code File Browsing"),
        ("epics/epic-8.html", "Epic 8: Dashboard Command Center"),
    };

    [Fact]
    public void RenderPage_WithReferences_LeadsWithRelationshipGraphThenSecondarySource()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);

        // The relationships block (graph + accessible list) is present and is the hero — it precedes the source.
        Assert.Contains("<section class=\"code-relationships\">", html);
        Assert.Contains("class=\"ref-graph\"", html);
        Assert.Contains("<section class=\"code-source-section\"", html);
        var relIndex = html.IndexOf("code-relationships", StringComparison.Ordinal);
        var srcIndex = html.IndexOf("code-source-section", StringComparison.Ordinal);
        Assert.True(relIndex >= 0 && relIndex < srcIndex, "relationships must lead the page, source is secondary");

        // Each citing artifact is a real graph node link carrying its full title, with a compact ring label.
        Assert.Contains("class=\"ref-node\" href=\"", html);
        Assert.Contains("epics/story-7-1.html", html);
        Assert.Contains("<title>Story 7.1: In-Portal Code File Browsing</title>", html);
        Assert.Contains(">Story 7.1</text>", html);   // compact ring label (identifier before the colon)

        // The always-present accessible list carries the FULL titles and meaningful link text — visually hidden
        // (sr-only) so the visible surface is just the graph, but present in the DOM for assistive tech.
        Assert.Contains("class=\"ref-list sr-only\"", html);
        Assert.Contains(">Story 7.1: In-Portal Code File Browsing</a>", html);
        Assert.Contains(">Epic 8: Dashboard Command Center</a>", html);

        // The locked line anchors survive the redesign (source is de-emphasized, never removed).
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("data-code-path=\"src/SpecScribe/Sample.cs\"", html);
    }

    [Fact]
    public void RenderPage_NoReferences_OmitsRelationshipsBlockButKeepsSource()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());

        Assert.DoesNotContain("code-relationships", html);
        Assert.DoesNotContain("ref-graph", html);
        // Source still renders with its anchors.
        Assert.Contains("<section class=\"code-source-section\"", html);
        Assert.Contains("id=\"L1\"", html);
    }

    [Fact]
    public void RenderPage_WithExternalUrl_AddsAdditiveViewSourceLink()
    {
        const string external = "https://github.com/owner/repo/blob/main/src/SpecScribe/Sample.cs";
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, external);

        Assert.Contains("class=\"code-external-link\"", html);
        Assert.Contains($"href=\"{external}\"", html);
        Assert.Contains("View on GitHub", html);
        // The in-portal source is still fully rendered — the external link is additive, not a replacement.
        Assert.Contains("class=\"code-file\"", html);
        Assert.Contains("<span class=\"code-src\">using System;</span>", html);
    }

    [Fact]
    public void RenderPage_NoExternalUrl_OmitsViewSourceLink()
    {
        var html = CodeFileTemplater.RenderPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);

        Assert.DoesNotContain("code-external-link", html);
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
