using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns the bold Gherkin keywords BMad authors into acceptance criteria (**Given**/**When**/
/// **Then**/**And**) into styled keyword markers, each starting its own visual line. Only bold keywords
/// are treated as Gherkin — plain prose "given"/"and" and other bold runs (e.g. "**Origin &amp; scope:**")
/// pass through untouched, so the styling can never fire outside the authored convention.</summary>
public static class GherkinStyler
{
    private static readonly Regex KeywordStrong = new(
        "<strong>(Given|When|Then|And|But)</strong>",
        RegexOptions.Compiled);

    // A multi-paragraph criterion body (clauses + a trailing "Origin & scope"-style note) keeps its
    // Markdig <p> wrappers because RenderInline only strips a single enclosing pair. Chips-per-line must
    // then be applied within each paragraph's own inline flow — slicing across a <p> boundary would emit
    // overlapping tags. <p> can't nest in HTML, so the lazy pair-match is unambiguous.
    private static readonly Regex Paragraph = new(
        "<p>(.*?)</p>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Restructures a rendered criterion so each keyword opens a block-level "gherkin-line"
    /// span. A bare inline flow (the common case) is processed directly; a multi-paragraph body is
    /// processed per-paragraph, so the clause paragraph gets per-line chips while a trailing note
    /// paragraph renders untouched below it. Prose before the first keyword in a flow stays outside the
    /// line structure; prose after the last keyword's clause stays inside that final line. Criteria
    /// containing no bold keywords come back unchanged.</summary>
    public static string StyleCriterion(string html)
    {
        if (string.IsNullOrEmpty(html) || !KeywordStrong.IsMatch(html)) return html;

        return html.Contains("<p>", StringComparison.Ordinal)
            ? Paragraph.Replace(html, m => $"<p>{StyleFlow(m.Groups[1].Value)}</p>")
            : StyleFlow(html);
    }

    /// <summary>The per-flow pass: wraps each keyword clause of one inline flow (a whole single-paragraph
    /// criterion, or one paragraph's inner content) in a "gherkin-line" span.
    ///
    /// The block-per-line wrapping only holds when every keyword sits at the top level of the flow (the
    /// authored convention: a flat "**Given** … **When** …" sequence). If a keyword is nested inside
    /// another inline element (e.g. an emphasized or linked run), slicing between keyword positions would
    /// cut across that element and emit overlapping tags — so we degrade to styling the keyword markers in
    /// place, never producing invalid HTML.</summary>
    private static string StyleFlow(string html)
    {
        var matches = KeywordStrong.Matches(html);
        if (matches.Count == 0) return html;

        if (!AllTopLevel(html, matches))
        {
            return KeywordStrong.Replace(html, m => KeywordSpan(m.Groups[1].Value));
        }

        var sb = new StringBuilder();
        if (matches[0].Index > 0)
        {
            sb.Append(html[..matches[0].Index]);
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var clauseEnd = i + 1 < matches.Count ? matches[i + 1].Index : html.Length;
            var clause = html[(m.Index + m.Length)..clauseEnd];
            sb.Append($"<span class=\"gherkin-line\">{KeywordSpan(m.Groups[1].Value)}{clause.Trim()}</span>");
        }

        return sb.ToString();
    }

    /// <summary>True when every keyword match starts at markup depth 0 (not inside another open tag).
    /// Depth is tracked by scanning tags: an opening <c>&lt;tag&gt;</c> increments, a closing
    /// <c>&lt;/tag&gt;</c> decrements, and self-closing/void tags (e.g. <c>&lt;br&gt;</c>) are neutral.</summary>
    private static bool AllTopLevel(string html, MatchCollection matches)
    {
        foreach (Match m in matches)
        {
            if (DepthAt(html, m.Index) != 0) return false;
        }
        return true;
    }

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "img", "hr", "wbr", "input", "meta", "link", "area", "base", "col", "embed", "source", "track",
    };

    private static int DepthAt(string html, int index)
    {
        var depth = 0;
        for (var i = 0; i < index; i++)
        {
            if (html[i] != '<') continue;
            var close = html.IndexOf('>', i);
            if (close < 0) break;

            var isEnd = i + 1 < html.Length && html[i + 1] == '/';
            var selfClose = close > 0 && html[close - 1] == '/';
            if (isEnd)
            {
                depth--;
            }
            else if (!selfClose && !VoidTags.Contains(TagName(html, i)))
            {
                depth++;
            }
            i = close;
        }
        return depth;
    }

    /// <summary>The element name of the tag opening at <paramref name="lt"/> (the '&lt;' index),
    /// e.g. "strong" for <c>&lt;strong&gt;</c>.</summary>
    private static string TagName(string html, int lt)
    {
        var start = lt + 1;
        var end = start;
        while (end < html.Length && (char.IsLetterOrDigit(html[end]) || html[end] == '-')) end++;
        return html[start..end];
    }

    /// <summary>The styled marker for a single keyword, e.g. <c>&lt;span class="gherkin-kw kw-given"&gt;
    /// Given&lt;/span&gt;</c>. Shared by <see cref="StyleCriterion"/> and the epic-card AC path
    /// (<see cref="EpicsParser"/>), which already breaks lines itself and only needs the marker.</summary>
    public static string KeywordSpan(string keyword) =>
        $"<span class=\"gherkin-kw kw-{keyword.ToLowerInvariant()}\">{keyword}</span>";
}
