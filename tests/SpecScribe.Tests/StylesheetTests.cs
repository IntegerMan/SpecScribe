using System.Reflection;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Cheap guards over the embedded stylesheet so the Story 1.4 accessibility floor (a shared
/// focus-visible ring, a reduced-motion block, and the skip link) can't be silently deleted in a later
/// refactor without a test going red.</summary>
public class StylesheetTests
{
    private static string ReadStylesheet()
    {
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.css")
            ?? throw new InvalidOperationException("Embedded stylesheet 'SpecScribe.assets.specscribe.css' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Stylesheet_HasReducedMotionBlock()
        => Assert.Contains("@media (prefers-reduced-motion: reduce)", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasFocusVisibleRing()
        => Assert.Contains(":focus-visible", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasSkipLinkRule()
        => Assert.Contains(".skip-link", ReadStylesheet());
}
