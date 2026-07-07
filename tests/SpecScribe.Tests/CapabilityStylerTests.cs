using SpecScribe;

namespace SpecScribe.Tests;

public class CapabilityStylerTests
{
    // Render authored markdown through the real pipeline so the styler is tested against the exact HTML
    // Markdig emits (heading auto-ids, nested <ul> shape) rather than a hand-mocked approximation.
    private static string Render(string markdown) => MarkdownConverter.RenderBlock(markdown);

    [Fact]
    public void Style_TurnsCapabilityListIntoDefinitionListCards()
    {
        // Blank lines between CAP items → a "loose" list, so Markdig wraps each CAP-id in <p> and the outer
        // <ul> nests per-item <ul>s — the exact shape the real SPEC.md produces.
        var html = Render("""
            ## Capabilities

            - **CAP-1**
              - **intent:** Ingest artifacts from many frameworks.
              - **success:** Repositories render without fatal failures.

            - **CAP-2**
              - **intent:** Generate a readable portal.
              - **success:** The index links major artifact classes.

            ## Constraints

            Some text.
            """);

        var styled = CapabilityStyler.Style(html);

        Assert.Contains("<div class=\"capabilities\">", styled);
        Assert.Contains("<div class=\"capability-id\">CAP-1</div>", styled);
        Assert.Contains("<div class=\"capability-id\">CAP-2</div>", styled);
        Assert.Contains("<dt>intent</dt>", styled);
        Assert.Contains("<dt>success</dt>", styled);
        Assert.Contains("Ingest artifacts from many frameworks.", styled);
        // The raw bold-CAP bullet is consumed into the card header; the heading (and its TOC anchor) survives.
        Assert.DoesNotContain("<strong>CAP-1</strong>", styled);
        Assert.Contains("id=\"capabilities\"", styled);
    }

    [Fact]
    public void Style_ReturnsUnchangedWhenNoCapabilitiesSection()
    {
        var html = Render("""
            ## Why

            - **Point one:** first.
            - **Point two:** second.
            """);

        Assert.Equal(html, CapabilityStyler.Style(html));
    }

    [Fact]
    public void Style_FallsBackUnchangedWhenListHasAStrayNonCapabilityItem()
    {
        // A top-level bullet that isn't a CAP item means the list isn't the pure authored convention — the
        // styler must not rewrap it (a bare <li> inside a <div> would be invalid), so it returns the original.
        var html = Render("""
            ## Capabilities

            - **CAP-1**
              - **intent:** do a thing.
            - just a plain bullet, not a capability
            """);

        var styled = CapabilityStyler.Style(html);

        Assert.Equal(html, styled);
        Assert.DoesNotContain("class=\"capabilities\"", styled);
    }

    [Fact]
    public void Style_HandlesEmptyInput() => Assert.Equal(string.Empty, CapabilityStyler.Style(string.Empty));
}
