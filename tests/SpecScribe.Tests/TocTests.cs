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
    public void RenderSidebar_DeduplicatesEntriesSharingAnAnchorIdKeepingTheFirst()
    {
        // Detail pages merge hardcoded panel ids with remainder-heading auto-ids; a collision must not emit two
        // links to the same anchor (a browser only jumps to the first, so the later one is a dead link).
        var html = Toc.RenderSidebar(new[]
        {
            new Toc.Entry(2, "Acceptance Criteria", "sec-acceptance-criteria"),
            new Toc.Entry(2, "Acceptance Criteria (dup)", "sec-acceptance-criteria"),
        });

        Assert.Contains("<a class=\"toc-link\" href=\"#sec-acceptance-criteria\">Acceptance Criteria</a>", html);
        Assert.DoesNotContain("Acceptance Criteria (dup)", html);
        // Exactly one link to the shared anchor.
        var occurrences = html.Split("href=\"#sec-acceptance-criteria\"").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void RenderSidebar_GroupsLevel3ChildrenUnderCollapsibleLevel2Parent()
    {
        // Story 10.5 AC2: h2 -> h3 -> h3 renders one <details class="toc-group"> with two children.
        var html = Toc.RenderSidebar(new[]
        {
            new Toc.Entry(2, "Parent", "sec-parent"),
            new Toc.Entry(3, "Child One", "sec-child-1"),
            new Toc.Entry(3, "Child Two", "sec-child-2"),
        });

        Assert.Equal(1, html.Split("<details class=\"toc-group\"").Length - 1);
        Assert.Equal(1, html.Split("</details>").Length - 1);
        // The summary keeps a real jump link to the parent's own section (invariant + a11y).
        Assert.Contains("<summary><a class=\"toc-link\" href=\"#sec-parent\">Parent</a></summary>", html);
        Assert.Contains("<a class=\"toc-link toc-h3\" href=\"#sec-child-1\">Child One</a>", html);
        Assert.Contains("<a class=\"toc-link toc-h3\" href=\"#sec-child-2\">Child Two</a>", html);
    }

    [Fact]
    public void RenderSidebar_ChildlessLevel2StaysAPlainLinkNotWrapped()
    {
        var html = Toc.RenderSidebar(new[]
        {
            new Toc.Entry(2, "Alone", "sec-alone"),
            new Toc.Entry(2, "Also Alone", "sec-also-alone"),
        });

        Assert.DoesNotContain("toc-group", html);
        Assert.Contains("<a class=\"toc-link\" href=\"#sec-alone\">Alone</a>", html);
        Assert.Contains("<a class=\"toc-link\" href=\"#sec-also-alone\">Also Alone</a>", html);
    }

    [Fact]
    public void RenderSidebar_StrayLeadingLevel3DegradesToPlainLink()
    {
        // No preceding level-2 to attach to — shouldn't occur in practice, but must degrade, never drop (NFR8).
        var html = Toc.RenderSidebar(new[] { new Toc.Entry(3, "Orphan", "sec-orphan") });

        Assert.DoesNotContain("toc-group", html);
        Assert.Contains("<a class=\"toc-link toc-h3\" href=\"#sec-orphan\">Orphan</a>", html);
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
