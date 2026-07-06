using SpecScribe;

namespace SpecScribe.Tests;

public class ColorSwatchRewriterTests
{
    [Fact]
    public void Rewrite_DarkHex_PaintsBackgroundWithWhiteForeground()
    {
        var html = ColorSwatchRewriter.Rewrite("<code>#1a1208</code>");

        Assert.Equal("<code class=\"color-swatch\" style=\"background-color:#1a1208;color:#fff\">#1a1208</code>", html);
    }

    [Fact]
    public void Rewrite_NearWhiteHex_UsesBlackForeground()
    {
        var html = ColorSwatchRewriter.Rewrite("<code>#f5f0e8</code>");

        Assert.Contains("background-color:#f5f0e8", html);
        Assert.Contains("color:#000", html);
    }

    [Fact]
    public void Rewrite_ShorthandHex_IsRecognized()
    {
        var html = ColorSwatchRewriter.Rewrite("<code>#fff</code>");

        Assert.Contains("class=\"color-swatch\"", html);
        Assert.Contains("background-color:#fff", html);
        Assert.Contains("color:#000", html);
    }

    [Fact]
    public void Rewrite_RgbaWithAlpha_KeepsValueAndPicksWhiteForeground()
    {
        var html = ColorSwatchRewriter.Rewrite("<code>rgba(26, 18, 8, 0.94)</code>");

        Assert.Contains("background-color:rgba(26, 18, 8, 0.94)", html);
        Assert.Contains("color:#fff", html);
    }

    [Fact]
    public void Rewrite_CssNamedColor_IsColorized()
    {
        var html = ColorSwatchRewriter.Rewrite("<code>teal</code>");

        Assert.Contains("class=\"color-swatch\"", html);
        Assert.Contains("background-color:teal", html);
        Assert.Contains("color:#fff", html);
    }

    [Fact]
    public void Rewrite_NonCssName_IsLeftUntouched()
    {
        const string input = "<code>cream</code>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_InvalidHex_IsLeftUntouched()
    {
        const string input = "<code>#zzz</code>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_MixedContent_IsLeftUntouched()
    {
        const string input = "<code>background: #fff</code>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_BlockCode_IsLeftUntouched()
    {
        const string input = "<pre><code>#fff</code></pre>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_FencedCodeWithLanguageClass_IsLeftUntouched()
    {
        const string input = "<pre><code class=\"language-css\">#fff</code></pre>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_OutOfRangeRgb_IsLeftUntouched()
    {
        const string input = "<code>rgb(300, 0, 0)</code>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_OutOfRangeAlpha_IsLeftUntouched()
    {
        const string input = "<code>rgba(0, 0, 0, 5)</code>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_BlockCodeWithPreAttributes_IsLeftUntouched()
    {
        const string input = "<pre class=\"language-css\"><code>#fff</code></pre>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_BlockCodeWithWhitespaceBeforeCode_IsLeftUntouched()
    {
        const string input = "<pre>\n<code>#fff</code></pre>";

        Assert.Equal(input, ColorSwatchRewriter.Rewrite(input));
    }

    [Fact]
    public void Rewrite_PreservesSurroundingHtmlAndOnlyPaintsColors()
    {
        var html = ColorSwatchRewriter.Rewrite("<p>Use <code>#008080</code> not <code>hello</code>.</p>");

        Assert.Contains("<p>Use <code class=\"color-swatch\" style=\"background-color:#008080;color:#fff\">#008080</code> not <code>hello</code>.</p>", html);
    }
}
