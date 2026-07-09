using SpecScribe;

namespace SpecScribe.Tests;

public class IconsTests
{
    [Theory]
    [InlineData("done")]
    [InlineData("active")]
    [InlineData("review")]
    [InlineData("ready")]
    [InlineData("drafted")]
    [InlineData("pending")]
    [InlineData("deferred")]
    public void ForStatus_EveryKnownCssClassReturnsAGlyph(string cssClass)
        => AssertWellFormedIcon(Icons.ForStatus(cssClass));

    [Fact]
    public void ForStatus_UnknownClassReturnsEmpty()
        => Assert.Equal(string.Empty, Icons.ForStatus("not-a-real-status"));

    [Theory]
    [InlineData("Home")]
    [InlineData("Readme")]
    [InlineData("PRD")]
    [InlineData("Product Brief")]
    [InlineData("UX Design")]
    [InlineData("UX Experience")]
    [InlineData("Architecture")]
    [InlineData("Epics")]
    [InlineData("Requirements")]
    [InlineData("ADRs")]
    [InlineData("Spec")]
    [InlineData("Sprint")]
    [InlineData("Overview")]
    [InlineData("Planning Artifacts")]
    [InlineData("Spec Kernel")]
    [InlineData("Implementation Artifacts")]
    [InlineData("Direct & Quick-Dev Work")]
    [InlineData("Deferred")]
    [InlineData("Structure")]
    public void ForConcept_EveryKnownLabelReturnsAGlyph(string label)
        => AssertWellFormedIcon(Icons.ForConcept(label));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some Unrecognized Concept")]
    public void ForConcept_UnknownOrEmptyLabelReturnsEmpty(string? label)
        => Assert.Equal(string.Empty, Icons.ForConcept(label));

    private static void AssertWellFormedIcon(string svg)
    {
        Assert.False(string.IsNullOrEmpty(svg));
        Assert.Contains("aria-hidden=\"true\"", svg);
        Assert.Contains("focusable=\"false\"", svg);
        Assert.Contains("currentColor", svg);
        Assert.DoesNotContain("#", svg); // no hard-coded hex color anywhere in the glyph
    }
}
