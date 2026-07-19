using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pure coverage for Story 10.5 AC1's <see cref="ReferenceChipRenderer"/>: <c>[[wiki-link]]</c> chips,
/// <c>[ASSUMPTION: …]</c> annotation styling, and bare <c>file:line</c> chips — plus the negative cases that
/// prove the anchor/code-span safety (never touching text inside an existing link, a code span, or a code
/// block) and the extension-bearing false-positive guard.</summary>
public class ReferenceChipRendererTests
{
    [Fact]
    public void WikiLink_RendersAsNonLinkChip()
    {
        var result = ReferenceChipRenderer.Render("See [[epic-2-retrospective]] for context.");

        Assert.Equal("See <span class=\"ref-chip\">epic-2-retrospective</span> for context.", result);
    }

    [Fact]
    public void AssumptionTag_RendersWithAnnotationVocabulary()
    {
        var result = ReferenceChipRenderer.Render("[ASSUMPTION: the API stays backward compatible]");

        Assert.Equal(
            "<span class=\"md-comment-inline assumption-tag\"><strong>ASSUMPTION:</strong> the API stays backward compatible</span>",
            result);
    }

    [Fact]
    public void AssumptionTag_IsCaseInsensitiveOnKeyword()
    {
        var result = ReferenceChipRenderer.Render("[assumption: lowercase keyword]");

        Assert.Contains("<strong>ASSUMPTION:</strong> lowercase keyword", result);
    }

    [Fact]
    public void BareFileLine_RendersAsChip()
    {
        var result = ReferenceChipRenderer.Render("See src/SpecScribe/Foo.cs:42 for the implementation.");

        Assert.Equal(
            "See <span class=\"ref-chip\">src/SpecScribe/Foo.cs:42</span> for the implementation.",
            result);
    }

    [Fact]
    public void BareFileLine_InsideCodeSpan_IsUntouched()
    {
        var html = "<code>src/SpecScribe/Foo.cs:42</code>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void BareFileLine_InsidePreBlock_IsUntouched()
    {
        var html = "<pre>src/SpecScribe/Foo.cs:42</pre>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void BareFileLine_InsideSvgTitle_IsUntouched()
    {
        // Chart tooltips (e.g. the sunburst's <title>) carry raw, unrendered text inside an SVG subtree — SVG
        // <title> has no HTML sub-parsing, so injected <span> markup there would show as literal visible text
        // instead of a styled chip. The whole <svg>...</svg> subtree must be skipped.
        var html = "<svg><path d=\"M0 0\"><title>See src/SpecScribe/Foo.cs:42 for details</title></path></svg>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void BareFileLine_InsideExistingAnchor_IsNotDoubleWrapped()
    {
        // Models Story 7.2's resolved output: CodeReferenceLinkifier already turned this into a real link.
        var html = "<a href=\"code/src/SpecScribe/Foo.cs.html#L42\">src/SpecScribe/Foo.cs:42</a>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void WikiLink_InsideExistingAnchorText_IsUntouched()
    {
        var html = "<a href=\"epics/epic-2.html\">[[epic-2-retrospective]]</a>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void AmbiguousColonWithoutExtension_IsLeftRaw()
    {
        var html = "note: 3 items remain";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }

    [Fact]
    public void EmptyOrNullInput_DegradesToAsIs()
    {
        Assert.Equal(string.Empty, ReferenceChipRenderer.Render(string.Empty));
        Assert.Null(ReferenceChipRenderer.Render(null!));
    }

    [Fact]
    public void UnrecognizedShape_DegradesToAsIs()
    {
        var html = "<p>Nothing special here — just plain prose.</p>";

        Assert.Equal(html, ReferenceChipRenderer.Render(html));
    }
}
