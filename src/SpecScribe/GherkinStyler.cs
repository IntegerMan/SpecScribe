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
        "<strong>(Given|When|Then|And)</strong>",
        RegexOptions.Compiled);

    /// <summary>Restructures a rendered criterion (one inline-HTML flow with embedded
    /// <c>&lt;strong&gt;</c> keywords) so each keyword opens a block-level "gherkin-line" span. Prose
    /// before the first keyword stays outside the line structure; prose after the last keyword's clause
    /// (e.g. an "Origin &amp; scope" note) stays inside that final line. Criteria containing no bold
    /// keywords come back unchanged.</summary>
    public static string StyleCriterion(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        var matches = KeywordStrong.Matches(html);
        if (matches.Count == 0) return html;

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
            sb.Append($"<span class=\"gherkin-line\">{KeywordSpan(m.Groups[1].Value)}{clause.TrimEnd()}</span>");
        }

        return sb.ToString();
    }

    /// <summary>The styled marker for a single keyword, e.g. <c>&lt;span class="gherkin-kw kw-given"&gt;
    /// Given&lt;/span&gt;</c>. Shared by <see cref="StyleCriterion"/> and the epic-card AC path
    /// (<see cref="EpicsParser"/>), which already breaks lines itself and only needs the marker.</summary>
    public static string KeywordSpan(string keyword) =>
        $"<span class=\"gherkin-kw kw-{keyword.ToLowerInvariant()}\">{keyword}</span>";
}
