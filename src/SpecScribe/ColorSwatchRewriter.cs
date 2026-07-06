using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Paints inline <c>&lt;code&gt;</c> spans whose entire content is a single recognized color value —
/// CSS hex (<c>#rgb</c>/<c>#rgba</c>/<c>#rrggbb</c>/<c>#rrggbbaa</c>), <c>rgb()</c>/<c>rgba()</c> functions, or a
/// standard CSS named color — by setting the span's background to that color and choosing a black or white
/// foreground that maximizes WCAG contrast. Runs as a whole-HTML post-process (like <see cref="SourceLinkifier"/>)
/// so a value is colorized wherever it appears. Block/fenced code (<c>&lt;pre&gt;&lt;code&gt;</c>) and spans with
/// extra text are left untouched.</summary>
public static class ColorSwatchRewriter
{
    // Inline code only: Markdig emits attribute-less <code> for inline spans and <code class="language-…"> /
    // <pre><code> for blocks. The negative lookbehind excludes a <code> that opens a block (indented code emits
    // <pre><code>); it tolerates attributes on <pre> and whitespace between the tags so the exclusion doesn't
    // hinge on Markdig's exact spacing. The fenced variant carries a class so it never matches attribute-less
    // <code>. Content is HTML-escaped text with no nested tags.
    private static readonly Regex InlineCodePattern = new(
        @"(?<!<pre[^>]*>\s*)<code>(?<value>[^<]*)</code>",
        RegexOptions.Compiled);

    private static readonly Regex HexPattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled);

    private static readonly Regex RgbFunctionPattern = new(
        @"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})\s*(?:,\s*(?<a>\d{1,3}(?:\.\d{1,6})?|\.\d{1,6})\s*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <param name="html">Already-rendered HTML to scan.</param>
    public static string Rewrite(string html)
    {
        if (string.IsNullOrEmpty(html) || html.IndexOf("<code>", StringComparison.Ordinal) < 0)
        {
            return html;
        }

        return InlineCodePattern.Replace(html, match =>
        {
            var value = match.Groups["value"].Value;
            var trimmed = value.Trim();

            if (!TryParseColor(trimmed, out var rgba))
            {
                return match.Value;
            }

            var foreground = ContrastForeground(rgba);
            return $"<code class=\"color-swatch\" style=\"background-color:{PathUtil.Html(trimmed)};color:{foreground}\">{value}</code>";
        });
    }

    /// <summary>Parses a hex, rgb()/rgba(), or CSS named color into 8-bit RGBA (alpha 0-1). Returns false for
    /// anything unrecognized so the caller leaves the span untouched.</summary>
    private static bool TryParseColor(string text, out (int R, int G, int B, double A) rgba)
    {
        rgba = default;

        if (HexPattern.IsMatch(text))
        {
            return TryParseHex(text, out rgba);
        }

        var fn = RgbFunctionPattern.Match(text);
        if (fn.Success)
        {
            var r = int.Parse(fn.Groups["r"].Value, CultureInfo.InvariantCulture);
            var g = int.Parse(fn.Groups["g"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(fn.Groups["b"].Value, CultureInfo.InvariantCulture);
            if (r > 255 || g > 255 || b > 255)
            {
                return false;
            }

            var a = 1.0;
            if (fn.Groups["a"].Success
                && (!double.TryParse(fn.Groups["a"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out a) || a > 1.0))
            {
                // Reject out-of-range alpha, mirroring the RGB channel bounds check above.
                return false;
            }

            rgba = (r, g, b, a);
            return true;
        }

        if (NamedColors.TryGetValue(text, out var hex))
        {
            return TryParseHex(hex, out rgba);
        }

        return false;
    }

    private static bool TryParseHex(string hex, out (int R, int G, int B, double A) rgba)
    {
        rgba = default;
        var body = hex[1..];

        // Expand shorthand #rgb / #rgba to full length.
        if (body.Length is 3 or 4)
        {
            body = string.Concat(body.Select(c => new string(c, 2)));
        }

        int Component(int index) => int.Parse(body.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var r = Component(0);
        var g = Component(2);
        var b = Component(4);
        var a = body.Length == 8 ? Component(6) / 255.0 : 1.0;

        rgba = (r, g, b, a);
        return true;
    }

    /// <summary>Returns "#000" or "#fff" — whichever yields the higher WCAG contrast ratio against the swatch's
    /// effective color (alpha composited over white, matching how the page renders a translucent swatch).</summary>
    private static string ContrastForeground((int R, int G, int B, double A) rgba)
    {
        var r = Composite(rgba.R, rgba.A);
        var g = Composite(rgba.G, rgba.A);
        var b = Composite(rgba.B, rgba.A);

        var luminance = RelativeLuminance(r, g, b);
        var contrastWithBlack = (luminance + 0.05) / 0.05;
        var contrastWithWhite = 1.05 / (luminance + 0.05);

        return contrastWithBlack >= contrastWithWhite ? "#000" : "#fff";
    }

    private static double Composite(int channel, double alpha) => channel * alpha + 255 * (1 - alpha);

    private static double RelativeLuminance(double r, double g, double b)
    {
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    private static double Linearize(double channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>The standard CSS named colors (CSS Color Module Level 4), keyed case-insensitively. Excludes
    /// <c>transparent</c>, which has no meaningful swatch.</summary>
    private static readonly IReadOnlyDictionary<string, string> NamedColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = "#f0f8ff", ["antiquewhite"] = "#faebd7", ["aqua"] = "#00ffff", ["aquamarine"] = "#7fffd4",
        ["azure"] = "#f0ffff", ["beige"] = "#f5f5dc", ["bisque"] = "#ffe4c4", ["black"] = "#000000",
        ["blanchedalmond"] = "#ffebcd", ["blue"] = "#0000ff", ["blueviolet"] = "#8a2be2", ["brown"] = "#a52a2a",
        ["burlywood"] = "#deb887", ["cadetblue"] = "#5f9ea0", ["chartreuse"] = "#7fff00", ["chocolate"] = "#d2691e",
        ["coral"] = "#ff7f50", ["cornflowerblue"] = "#6495ed", ["cornsilk"] = "#fff8dc", ["crimson"] = "#dc143c",
        ["cyan"] = "#00ffff", ["darkblue"] = "#00008b", ["darkcyan"] = "#008b8b", ["darkgoldenrod"] = "#b8860b",
        ["darkgray"] = "#a9a9a9", ["darkgreen"] = "#006400", ["darkgrey"] = "#a9a9a9", ["darkkhaki"] = "#bdb76b",
        ["darkmagenta"] = "#8b008b", ["darkolivegreen"] = "#556b2f", ["darkorange"] = "#ff8c00", ["darkorchid"] = "#9932cc",
        ["darkred"] = "#8b0000", ["darksalmon"] = "#e9967a", ["darkseagreen"] = "#8fbc8f", ["darkslateblue"] = "#483d8b",
        ["darkslategray"] = "#2f4f4f", ["darkslategrey"] = "#2f4f4f", ["darkturquoise"] = "#00ced1", ["darkviolet"] = "#9400d3",
        ["deeppink"] = "#ff1493", ["deepskyblue"] = "#00bfff", ["dimgray"] = "#696969", ["dimgrey"] = "#696969",
        ["dodgerblue"] = "#1e90ff", ["firebrick"] = "#b22222", ["floralwhite"] = "#fffaf0", ["forestgreen"] = "#228b22",
        ["fuchsia"] = "#ff00ff", ["gainsboro"] = "#dcdcdc", ["ghostwhite"] = "#f8f8ff", ["gold"] = "#ffd700",
        ["goldenrod"] = "#daa520", ["gray"] = "#808080", ["green"] = "#008000", ["greenyellow"] = "#adff2f",
        ["grey"] = "#808080", ["honeydew"] = "#f0fff0", ["hotpink"] = "#ff69b4", ["indianred"] = "#cd5c5c",
        ["indigo"] = "#4b0082", ["ivory"] = "#fffff0", ["khaki"] = "#f0e68c", ["lavender"] = "#e6e6fa",
        ["lavenderblush"] = "#fff0f5", ["lawngreen"] = "#7cfc00", ["lemonchiffon"] = "#fffacd", ["lightblue"] = "#add8e6",
        ["lightcoral"] = "#f08080", ["lightcyan"] = "#e0ffff", ["lightgoldenrodyellow"] = "#fafad2", ["lightgray"] = "#d3d3d3",
        ["lightgreen"] = "#90ee90", ["lightgrey"] = "#d3d3d3", ["lightpink"] = "#ffb6c1", ["lightsalmon"] = "#ffa07a",
        ["lightseagreen"] = "#20b2aa", ["lightskyblue"] = "#87cefa", ["lightslategray"] = "#778899", ["lightslategrey"] = "#778899",
        ["lightsteelblue"] = "#b0c4de", ["lightyellow"] = "#ffffe0", ["lime"] = "#00ff00", ["limegreen"] = "#32cd32",
        ["linen"] = "#faf0e6", ["magenta"] = "#ff00ff", ["maroon"] = "#800000", ["mediumaquamarine"] = "#66cdaa",
        ["mediumblue"] = "#0000cd", ["mediumorchid"] = "#ba55d3", ["mediumpurple"] = "#9370db", ["mediumseagreen"] = "#3cb371",
        ["mediumslateblue"] = "#7b68ee", ["mediumspringgreen"] = "#00fa9a", ["mediumturquoise"] = "#48d1cc", ["mediumvioletred"] = "#c71585",
        ["midnightblue"] = "#191970", ["mintcream"] = "#f5fffa", ["mistyrose"] = "#ffe4e1", ["moccasin"] = "#ffe4b5",
        ["navajowhite"] = "#ffdead", ["navy"] = "#000080", ["oldlace"] = "#fdf5e6", ["olive"] = "#808000",
        ["olivedrab"] = "#6b8e23", ["orange"] = "#ffa500", ["orangered"] = "#ff4500", ["orchid"] = "#da70d6",
        ["palegoldenrod"] = "#eee8aa", ["palegreen"] = "#98fb98", ["paleturquoise"] = "#afeeee", ["palevioletred"] = "#db7093",
        ["papayawhip"] = "#ffefd5", ["peachpuff"] = "#ffdab9", ["peru"] = "#cd853f", ["pink"] = "#ffc0cb",
        ["plum"] = "#dda0dd", ["powderblue"] = "#b0e0e6", ["purple"] = "#800080", ["rebeccapurple"] = "#663399",
        ["red"] = "#ff0000", ["rosybrown"] = "#bc8f8f", ["royalblue"] = "#4169e1", ["saddlebrown"] = "#8b4513",
        ["salmon"] = "#fa8072", ["sandybrown"] = "#f4a460", ["seagreen"] = "#2e8b57", ["seashell"] = "#fff5ee",
        ["sienna"] = "#a0522d", ["silver"] = "#c0c0c0", ["skyblue"] = "#87ceeb", ["slateblue"] = "#6a5acd",
        ["slategray"] = "#708090", ["slategrey"] = "#708090", ["snow"] = "#fffafa", ["springgreen"] = "#00ff7f",
        ["steelblue"] = "#4682b4", ["tan"] = "#d2b48c", ["teal"] = "#008080", ["thistle"] = "#d8bfd8",
        ["tomato"] = "#ff6347", ["turquoise"] = "#40e0d0", ["violet"] = "#ee82ee", ["wheat"] = "#f5deb3",
        ["white"] = "#ffffff", ["whitesmoke"] = "#f5f5f5", ["yellow"] = "#ffff00", ["yellowgreen"] = "#9acd32",
    };
}
