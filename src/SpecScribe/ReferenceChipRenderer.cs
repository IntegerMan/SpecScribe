using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Renders three inline authoring shapes Markdig passes through as literal text — <c>[[wiki-link]]</c>
/// names, <c>[ASSUMPTION: …]</c> tags, and bare extension-bearing <c>file:line</c> citations Story 7.2's
/// <see cref="CodeReferenceLinkifier"/> left unresolved — into designed elements instead of raw syntax (Story
/// 10.5, AC1). Runs as a whole-page post-process in <see cref="SiteGenerator.ApplyReferenceLinks"/>, AFTER
/// <see cref="CodeReferenceLinkifier"/>: a citation 7.2 already resolved into a real <c>&lt;a&gt;</c> is left
/// completely untouched (Decision #1 — the simplest boundary; a resolved link is no longer raw syntax, so AC1's
/// requirement is already satisfied without re-touching 7.2's output). Mirrors <see cref="CodeReferenceLinkifier"/>'s
/// anchor-split + <c>&lt;code&gt;</c>/<c>&lt;pre&gt;</c>-skipping shape so nothing inside a link, a code sample,
/// or another tag's attributes is ever rewritten — the AC1 correctness core. All three shapes are matched in one
/// regex so a single <c>Replace</c> pass can never re-scan (and double-wrap) another shape's own output.</summary>
public static class ReferenceChipRenderer
{
    // Splits already-rendered HTML into alternating [plain, protected, plain, protected, ...] segments. Only
    // even indices (plain prose) are matched below; a whole <a>/<code>/<pre>/<svg>/<script>/<style>/<head>
    // span, or any other standalone tag, is carried through untouched — same discipline as
    // AbbreviationExpander.ProtectedSplit (including <script>/<style>/<head>, since ApplyReferenceLinks runs
    // over the full page HTML). <svg> matters specifically: chart tooltips (e.g. the sunburst's <title> text)
    // carry raw, unrendered task text inside an SVG subtree, where injected <span> markup would show as
    // literal visible text (SVG <title> has no HTML sub-parsing) rather than a styled chip — so the whole
    // subtree is skipped, not just individual tags.
    private static readonly Regex ProtectedSplit = new(
        "(<a\\b[^>]*>.*?</a>|<code\\b[^>]*>.*?</code>|<pre\\b[^>]*>.*?</pre>|<svg\\b[^>]*>.*?</svg>"
        + "|<head\\b[^>]*>.*?</head>|<script\\b[^>]*>.*?</script>|<style\\b[^>]*>.*?</style>|<[^>]*>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Three shapes in one alternation (leftmost-match-wins, tried in listed order at each position):
    //   [[name]]              — a memory/cross-doc reference name with no portal resolver (non-link chip only).
    //   [ASSUMPTION: text]    — case-insensitive keyword, reusing the Story 2.6 annotation vocabulary.
    //   path.ext:line         — bare file:line citation; requires an extension on the final path segment
    //                           (mirrors CodeReferenceLinkifier.IsRelativeCodeHref) so an extension-less false
    //                           positive ("note: 3") never matches, and is bounded on both sides so it never
    //                           grabs a fragment of a longer token.
    private static readonly Regex Combined = new(
        @"\[\[(?<wiki>[^\[\]\r\n]+)\]\]" +
        @"|\[ASSUMPTION\s*:\s*(?<assump>[^\[\]\r\n]+)\]" +
        @"|(?<![\w/\\.-])(?<file>[A-Za-z0-9_][\w\-./\\]*\.[A-Za-z][A-Za-z0-9]{0,9}):(?<line>\d{1,6})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Rewrites all three shapes on one rendered page. Degrades any unrecognized shape to as-is (NFR8).</summary>
    public static string Render(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        var parts = ProtectedSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1) continue; // odd indices are the captured protected spans — leave them untouched.
            parts[i] = Combined.Replace(parts[i], Evaluate);
        }
        return string.Concat(parts);
    }

    private static string Evaluate(Match m)
    {
        if (m.Groups["wiki"].Success)
        {
            return $"<span class=\"ref-chip\">{m.Groups["wiki"].Value}</span>";
        }
        if (m.Groups["assump"].Success)
        {
            return $"<span class=\"md-comment-inline assumption-tag\"><strong>ASSUMPTION:</strong> {m.Groups["assump"].Value.Trim()}</span>";
        }
        // Bare file:line — never touched when it already sits inside an <a>/<code>/<pre> span (protected above).
        return $"<span class=\"ref-chip\">{m.Value}</span>";
    }
}
