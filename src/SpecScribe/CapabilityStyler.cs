using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns a spec kernel's <c>## Capabilities</c> section — authored as a nested bullet list of
/// <c>CAP-N</c> items with <c>intent:</c>/<c>success:</c> sub-bullets — into scannable definition-list cards.
/// A pure body-HTML post-processor (like <see cref="GherkinStyler"/>): it is anchored on the
/// <c>id="capabilities"</c> heading so it can never fire on another list, and returns the input unchanged
/// whenever the authored shape isn't found. The heading itself is left intact, so the "On this page" TOC
/// (which reads <see cref="DocModel.Headings"/>, not this HTML) is unaffected. [Story 2.2 polish]</summary>
public static class CapabilityStyler
{
    // The Capabilities section: its heading (Markdig auto-ids "## Capabilities" → id="capabilities") followed
    // by the single list Markdig emits for the CAP items. The list's own items nest their own <ul>, so a plain
    // lazy `.*?</ul>` would stop at the first (inner) close — instead we lazily extend to the </ul> that sits
    // just before the next heading (or end of doc), which is the outer list's close. Singleline so `.` spans
    // newlines.
    private static readonly Regex Section = new(
        "(?<head><h2[^>]*\\bid=\"capabilities\"[^>]*>.*?</h2>)\\s*<ul>(?<body>.*?)</ul>\\s*(?=<h[1-6]|\\z)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // One capability list item: a bold CAP-id (Markdig wraps it in <p> for a loose/blank-line-separated list,
    // so the <p>/</p> are optional), then the nested list of label/body rows.
    private static readonly Regex CapItem = new(
        "<li>\\s*(?:<p>)?\\s*<strong>(?<id>CAP-[^<]+)</strong>\\s*(?:</p>)?\\s*<ul>(?<rows>.*?)</ul>\\s*</li>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // One detail row inside a capability: a bold label (optionally colon-terminated) then its body text, again
    // tolerating an optional <p> wrapper for a loose sub-list.
    private static readonly Regex Row = new(
        "<li>\\s*(?:<p>)?\\s*<strong>(?<label>[^<]+?):?</strong>\\s*(?<body>.*?)\\s*(?:</p>)?\\s*</li>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Rewrites the Capabilities list into <c>.capabilities</c> cards. No section, no CAP items, or a
    /// list that doesn't fully match the authored convention → returns <paramref name="html"/> unchanged.</summary>
    public static string Style(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        return Section.Replace(html, m =>
        {
            var body = m.Groups["body"].Value;

            var cards = new StringBuilder();
            var matched = 0;
            // Replace each CAP item with a card, and track whether anything OTHER than the CAP items remains
            // in the list body. If a stray top-level <li> survives, the list isn't the pure CAP convention, so
            // we must not rewrap it (a bare <li> inside a <div> would be invalid) — bail to the original.
            var leftover = CapItem.Replace(body, item =>
            {
                matched++;
                cards.Append(BuildCard(item.Groups["id"].Value, item.Groups["rows"].Value));
                return string.Empty;
            });

            if (matched == 0 || leftover.Contains("<li", StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }

            return $"{m.Groups["head"].Value}\n<div class=\"capabilities\">\n{cards}</div>";
        });
    }

    private static string BuildCard(string id, string rowsHtml)
    {
        var rows = new StringBuilder();
        foreach (Match r in Row.Matches(rowsHtml))
        {
            var label = r.Groups["label"].Value.Trim();
            var rowBody = r.Groups["body"].Value.Trim();
            rows.Append($"    <div class=\"cap-row\"><dt>{PathUtil.Html(label)}</dt><dd>{rowBody}</dd></div>\n");
        }

        var sb = new StringBuilder();
        sb.Append("  <div class=\"capability\">\n");
        sb.Append($"    <div class=\"capability-id\">{PathUtil.Html(id.Trim())}</div>\n");
        if (rows.Length > 0)
        {
            sb.Append("    <dl class=\"capability-detail\">\n");
            sb.Append(rows);
            sb.Append("    </dl>\n");
        }
        sb.Append("  </div>\n");
        return sb.ToString();
    }
}
