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

    // ---- Story 1.5 seams -------------------------------------------------------------------

    [Fact]
    public void Stylesheet_HasNoPreferenceEntranceBlock()
        // The complementary "no-preference" half of the motion contract sits beside the "reduce" block. [Story 1.5 D2]
        => Assert.Contains("@media (prefers-reduced-motion: no-preference)", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasPerStatusTokens()
    {
        // One CSS variable per lifecycle stage — the single stage→color source. [Story 1.5 B2/B3]
        var css = ReadStylesheet();
        Assert.Contains("--status-pending:", css);
        Assert.Contains("--status-drafted:", css);
        Assert.Contains("--status-ready:", css);
        Assert.Contains("--status-active:", css);
        Assert.Contains("--status-review:", css);
        Assert.Contains("--status-done:", css);
    }

    [Fact]
    public void Stylesheet_HasOnBrandTooltipStyles()
    {
        // CSS-only tooltip for HTML elements + the JS-positioned node for SVG segments. [Story 1.5 C1/C2]
        var css = ReadStylesheet();
        Assert.Contains("[data-tooltip]", css);
        Assert.Contains("::after", css);
        Assert.Contains(".ss-tooltip", css);
    }

    [Fact]
    public void Stylesheet_HasCopyButtonStyles()
        => Assert.Contains(".copy-btn", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasSendMenuStyles()
    {
        // The unified command badge and its send menu (native <details>) with shared menu rows.
        var css = ReadStylesheet();
        Assert.Contains(".cmd-badge", css);
        Assert.Contains(".send-menu", css);
        Assert.Contains(".send-toggle", css);
        Assert.Contains(".send-item", css);
    }

    [Fact]
    public void Script_IsEmbeddedAlongsideStylesheet()
    {
        // The one sanctioned progressive-enhancement script must ship embedded the way the CSS does, so the
        // global-tool package stays self-contained. [Story 1.5 Task 3]
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.js");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var js = reader.ReadToEnd();
        Assert.Contains("clipboard", js);
        Assert.Contains("ss-tooltip", js);
        // The send menu's click-away / Escape dismissal ships in the same sanctioned script.
        Assert.Contains("send-menu", js);
    }
}
