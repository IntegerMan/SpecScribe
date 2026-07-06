using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the shared table-of-contents seam (Story 1.3 AC #3): the sidebar renderer, the
/// two-column page-shell wrapper, and the rendered-order heading extractor used to build detail-page TOCs.</summary>
public class TocTests
{
    [Fact]
    public void RenderSidebar_EmptyEntriesYieldsEmptyString()
        => Assert.Equal(string.Empty, Toc.RenderSidebar(Array.Empty<Toc.Entry>()));

    [Fact]
    public void RenderSidebar_EmitsAccessibleNavWithLinks()
    {
        var html = Toc.RenderSidebar(new[]
        {
            new Toc.Entry(2, "First Section", "sec-one"),
            new Toc.Entry(3, "Nested", "sec-two"),
        });

        Assert.Contains("<nav class=\"toc-sidebar\" aria-label=\"On this page\">", html);
        Assert.Contains("<a class=\"toc-link\" href=\"#sec-one\">First Section</a>", html);
        // Level-3 entries render indented via the toc-h3 modifier.
        Assert.Contains("<a class=\"toc-link toc-h3\" href=\"#sec-two\">Nested</a>", html);
    }

    [Fact]
    public void RenderSidebar_EscapesEntryText()
    {
        var html = Toc.RenderSidebar(new[] { new Toc.Entry(2, "A & B <c>", "x") });
        Assert.Contains(">A &amp; B &lt;c&gt;</a>", html);
    }

    [Fact]
    public void WrapWithSidebar_NoEntriesReturnsContentUnwrapped()
    {
        const string main = "<article>content</article>";
        Assert.Equal(main, Toc.WrapWithSidebar(main, Array.Empty<Toc.Entry>()));
    }

    [Fact]
    public void WrapWithSidebar_WrapsContentAndSidebarInTwoColumnShell()
    {
        var html = Toc.WrapWithSidebar("<article>content</article>", new[] { new Toc.Entry(2, "Sec", "sec") });

        Assert.Contains("<div class=\"page-shell\">", html);
        Assert.Contains("<div class=\"page-main\">", html);
        Assert.Contains("<article>content</article>", html);
        Assert.Contains("class=\"toc-sidebar\"", html);
        // No trace of the retired top-strip presentation.
        Assert.DoesNotContain("toc-strip", html);
    }

    [Fact]
    public void ExtractHeadings_ReturnsLevel2And3HeadingsInDocumentOrder()
    {
        const string html = """
            <h2 id="a">Alpha</h2>
            <p>text</p>
            <h3 id="b">Beta</h3>
            <h2 id="c">Gamma</h2>
            """;

        var entries = Toc.ExtractHeadings(html);

        Assert.Equal(3, entries.Count);
        Assert.Equal(("a", 2, "Alpha"), (entries[0].AnchorId, entries[0].Level, entries[0].Text));
        Assert.Equal(("b", 3, "Beta"), (entries[1].AnchorId, entries[1].Level, entries[1].Text));
        Assert.Equal(("c", 2, "Gamma"), (entries[2].AnchorId, entries[2].Level, entries[2].Text));
    }

    [Fact]
    public void ExtractHeadings_SkipsHeadingsWithoutIdSoNoEntryIsADeadLink()
    {
        const string html = "<h2>No id here</h2>\n<h2 id=\"real\">Real</h2>";

        var entries = Toc.ExtractHeadings(html);

        var only = Assert.Single(entries);
        Assert.Equal("real", only.AnchorId);
    }

    [Fact]
    public void ExtractHeadings_StripsInlineMarkupFromEntryText()
    {
        var entries = Toc.ExtractHeadings("<h2 id=\"h\">Use <code>RenderBlock</code> here</h2>");

        var only = Assert.Single(entries);
        Assert.Equal("Use RenderBlock here", only.Text);
    }

    [Fact]
    public void ExtractHeadings_IgnoresH1AndH4()
    {
        var entries = Toc.ExtractHeadings("<h1 id=\"top\">Top</h1>\n<h4 id=\"deep\">Deep</h4>");
        Assert.Empty(entries);
    }

    [Fact]
    public void ExtractHeadings_StripsHeadingAnchorWhoseAttributeSpansNewlines()
    {
        // A remainder heading with an "(AC: #N)" reference gets linkified into an anchor whose title carries
        // the criterion's multi-line text; the extracted TOC text must still be clean plain text, not raw markup.
        const string html = "<h3 id=\"toc-ac\">Sidebar (AC <a class=\"ac-ref\" href=\"#ac-3\" title=\"Given a page\nWhen viewed\nThen it works\">#3</a>)</h3>";

        var only = Assert.Single(Toc.ExtractHeadings(html));
        Assert.Equal("Sidebar (AC #3)", only.Text);
        Assert.DoesNotContain("<a", only.Text);
        Assert.DoesNotContain("title=", only.Text);
    }
}
