using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the pure remainder-HTML collapse wrapper (Story 9.5 AC #2): section
/// boundaries, summary-hosted H2 id retention, buried-H3 id stripping, and NFR8 no-match passthrough.</summary>
public class CollapsibleSectionsTests
{
    [Fact]
    public void WrapSections_WrapsMatchingH2CollapsedByDefaultKeepingIdInSummary()
    {
        const string html = """
            <h2 id="context-scope">Context &amp; Scope</h2>
            <p>Keep me open.</p>
            <h2 id="dev-notes">Dev Notes</h2>
            <h3 id="reuse-map">Reuse map</h3>
            <p>Buried.</p>
            <h2 id="tasks-subtasks">Tasks / Subtasks</h2>
            <p>Also open.</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);

        Assert.Contains("<details class=\"collapsible-section\" id=\"dev-notes-section\">", wrapped);
        Assert.DoesNotContain("<details open", wrapped);
        Assert.Contains("<summary><h2 id=\"dev-notes\">Dev Notes</h2></summary>", wrapped);
        // Context & Tasks stay expanded — not wrapped.
        Assert.Contains("<h2 id=\"context-scope\">Context &amp; Scope</h2>", wrapped);
        Assert.Contains("<h2 id=\"tasks-subtasks\">Tasks / Subtasks</h2>", wrapped);
        Assert.DoesNotContain("id=\"context-scope-section\"", wrapped);
        Assert.DoesNotContain("id=\"tasks-subtasks-section\"", wrapped);
    }

    [Fact]
    public void WrapSections_StripsIdsFromBuriedH3sSoTocOmitsThem()
    {
        const string html = """
            <h2 id="dev-notes">Dev Notes</h2>
            <h3 id="project-structure-notes">Project Structure Notes</h3>
            <p>x</p>
            <h3 id="references">References</h3>
            <p>y</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);
        var headings = Toc.ExtractHeadings(wrapped);

        Assert.Contains(headings, e => e.AnchorId == "dev-notes" && e.Text == "Dev Notes");
        Assert.DoesNotContain(headings, e => e.AnchorId == "project-structure-notes");
        Assert.DoesNotContain(headings, e => e.AnchorId == "references");
        // Id attributes themselves are gone from the buried openings.
        Assert.DoesNotContain("id=\"project-structure-notes\"", wrapped);
        Assert.Contains("<h3>Project Structure Notes</h3>", wrapped);
        Assert.Contains("<h3>References</h3>", wrapped);
    }

    [Fact]
    public void WrapSections_WrapsTopLevelReferencesH2Separately()
    {
        const string html = """
            <h2 id="dev-notes">Dev Notes</h2>
            <p>notes</p>
            <h2 id="references">References</h2>
            <p>refs</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);

        Assert.Contains("id=\"dev-notes-section\"", wrapped);
        Assert.Contains("id=\"references-section\"", wrapped);
        Assert.Contains("<summary><h2 id=\"references\">References</h2></summary>", wrapped);
    }

    [Fact]
    public void WrapSections_WrapsMarkdigCollisionSlugForms()
    {
        const string html = """
            <h2 id="context-scope">Context &amp; Scope</h2>
            <p>Keep me open.</p>
            <h2 id="dev-notes">Dev Notes</h2>
            <p>first notes</p>
            <h2 id="dev-notes-1">Dev Notes</h2>
            <p>second notes</p>
            <h2 id="references">References</h2>
            <p>first</p>
            <h2 id="references-1">References</h2>
            <p>second</p>
            <h2 id="tasks-subtasks">Tasks / Subtasks</h2>
            <p>Also open.</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);

        Assert.Contains("id=\"dev-notes-section\"", wrapped);
        Assert.Contains("id=\"dev-notes-1-section\"", wrapped);
        Assert.Contains("id=\"references-section\"", wrapped);
        Assert.Contains("id=\"references-1-section\"", wrapped);
        Assert.Contains("<summary><h2 id=\"references-1\">References</h2></summary>", wrapped);
        Assert.DoesNotContain("id=\"context-scope-section\"", wrapped);
        Assert.DoesNotContain("id=\"tasks-subtasks-section\"", wrapped);
    }

    [Fact]
    public void WrapSections_NoMatchingHeading_ReturnsInputUnchanged()
    {
        const string html = """
            <h2 id="context-scope">Context &amp; Scope</h2>
            <p>Only this.</p>
            <h2 id="tasks-subtasks">Tasks / Subtasks</h2>
            <p>And tasks.</p>
            """;

        Assert.Equal(html, CollapsibleSections.WrapStoryRemainder(html));
        Assert.DoesNotContain("collapsible-section", CollapsibleSections.WrapStoryRemainder(html));
    }

    [Fact]
    public void WrapStoryRemainder_MovesDevNotesAfterExpandedSections()
    {
        const string html = """
            <h2 id="context-scope">Context &amp; Scope</h2>
            <p>Keep me open.</p>
            <h2 id="dev-notes">Dev Notes</h2>
            <h3 id="reuse-map">Reuse map</h3>
            <p>Buried.</p>
            <h2 id="tasks-subtasks">Tasks / Subtasks</h2>
            <p>Also open.</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);
        var contextAt = wrapped.IndexOf("id=\"context-scope\"", StringComparison.Ordinal);
        var tasksAt = wrapped.IndexOf("id=\"tasks-subtasks\"", StringComparison.Ordinal);
        var notesAt = wrapped.IndexOf("id=\"dev-notes-section\"", StringComparison.Ordinal);
        Assert.True(contextAt >= 0 && tasksAt > contextAt && notesAt > tasksAt,
            "Dev Notes collapsible should trail Context and Tasks so it sits above Change Log.");
    }

    [Fact]
    public void WrapSections_EmptyOrNullish_Passthrough()
    {
        Assert.Equal(string.Empty, CollapsibleSections.WrapStoryRemainder(string.Empty));
        Assert.Equal("plain", CollapsibleSections.WrapSections("plain", CollapsibleSections.StoryRemainderSlugs));
    }

    [Fact]
    public void WrapSections_MatchesOnIdSlugNotRawHeadingText()
    {
        // Inline markup inside the heading must not break the id-slug match.
        const string html = """
            <h2 id="dev-notes"><em>Dev</em> Notes</h2>
            <h3 id="reuse-map">Reuse</h3>
            <p>body</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);
        Assert.Contains("id=\"dev-notes-section\"", wrapped);
        Assert.Contains("<summary><h2 id=\"dev-notes\"><em>Dev</em> Notes</h2></summary>", wrapped);
        Assert.DoesNotContain("id=\"reuse-map\"", wrapped);
    }

    [Fact]
    public void WrapSections_IdLessFollowingH2_EndsSectionWithoutSwallowingIt()
    {
        const string html = """
            <h2 id="dev-notes">Dev Notes</h2>
            <p>notes body</p>
            <h2>Undrafted aside</h2>
            <p>must stay expanded</p>
            <h2 id="tasks-subtasks">Tasks / Subtasks</h2>
            <p>tasks</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);

        Assert.Contains("id=\"dev-notes-section\"", wrapped);
        Assert.Contains("<h2>Undrafted aside</h2>", wrapped);
        Assert.DoesNotContain("Undrafted aside</h2></summary>", wrapped);
        // Id-less H2 must remain outside the collapsed block (before the moved details at end).
        var asideAt = wrapped.IndexOf("<h2>Undrafted aside</h2>", StringComparison.Ordinal);
        var detailsAt = wrapped.IndexOf("id=\"dev-notes-section\"", StringComparison.Ordinal);
        Assert.True(asideAt >= 0 && detailsAt > asideAt);
        Assert.Contains("must stay expanded", wrapped[..detailsAt]);
    }

    [Fact]
    public void MoveCollapsibleSectionsToEnd_PreservesNestedDetailsInsideBody()
    {
        const string html = """
            <h2 id="context-scope">Context</h2>
            <p>open</p>
            <h2 id="dev-notes">Dev Notes</h2>
            <p>before</p>
            <details class="author-note"><summary>Inner</summary><p>nested body</p></details>
            <p>after</p>
            <h2 id="tasks-subtasks">Tasks</h2>
            <p>tasks</p>
            """;

        var wrapped = CollapsibleSections.WrapStoryRemainder(html);

        Assert.Contains("id=\"dev-notes-section\"", wrapped);
        Assert.Contains("<details class=\"author-note\"><summary>Inner</summary><p>nested body</p></details>", wrapped);
        // Outer collapsible must enclose the nested block intact (balanced close after nested close).
        var outerOpen = wrapped.IndexOf("<details class=\"collapsible-section\" id=\"dev-notes-section\">", StringComparison.Ordinal);
        Assert.True(outerOpen >= 0);
        var nestedOpen = wrapped.IndexOf("<details class=\"author-note\">", outerOpen, StringComparison.Ordinal);
        Assert.True(nestedOpen > outerOpen);
        var nestedClose = wrapped.IndexOf("</details>", nestedOpen, StringComparison.Ordinal);
        var outerClose = wrapped.IndexOf("</details>", nestedClose + "</details>".Length, StringComparison.Ordinal);
        Assert.True(nestedClose >= 0 && outerClose > nestedClose);
        Assert.Contains("<p>after</p>", wrapped[outerOpen..outerClose]);
        // No stray dangling close left in the expanded Tasks region.
        Assert.Contains("<h2 id=\"tasks-subtasks\">Tasks</h2>", wrapped[..outerOpen]);
    }
}