using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Wraps the first in-page occurrence of each acronym-shaped <see cref="GlossaryTerm"/> in a
/// native <c>&lt;abbr title="..."&gt;</c> span, so a first-time visitor sees "FR" expand to "Functional
/// Requirement" the first time it appears on a page — later occurrences are left as plain text. Runs as a
/// whole-page post-process (like <see cref="RequirementLinkifier"/>), guarded against a null vocabulary so
/// an undetected framework's pages render byte-unchanged (NFR8). Never rewrites inside an
/// &lt;a&gt;/&lt;code&gt;/&lt;pre&gt;/&lt;abbr&gt;/&lt;svg&gt;/&lt;head&gt;/&lt;script&gt;/&lt;style&gt;
/// span or a standalone tag, so nav labels, breadcrumb links, command badges, and pre-existing
/// abbreviations are never corrupted.</summary>
public static class AbbreviationExpander
{
    // Same protected-split grammar as RequirementLinkifier, extended with <abbr>…</abbr> so an
    // already-expanded (or hand-authored) abbreviation is never re-wrapped or matched inside its own title.
    private static readonly Regex ProtectedSplit = new(
        "(<a\\b[^>]*>.*?</a>|<code\\b[^>]*>.*?</code>|<pre\\b[^>]*>.*?</pre>|<abbr\\b[^>]*>.*?</abbr>|<svg\\b[^>]*>.*?</svg>"
        + "|<head\\b[^>]*>.*?</head>|<script\\b[^>]*>.*?</script>|<style\\b[^>]*>.*?</style>|<[^>]*>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <param name="html">Already-rendered HTML to scan.</param>
    /// <param name="glossary">The module's vocabulary. Empty (undetected framework) is a byte-identical no-op.</param>
    public static string Expand(string html, IReadOnlyList<GlossaryTerm> glossary)
    {
        if (string.IsNullOrEmpty(html) || glossary.Count == 0)
        {
            return html;
        }

        var acronyms = glossary.Where(g => g.IsAcronym).ToList();
        if (acronyms.Count == 0)
        {
            return html;
        }

        // Longest-term-first alternation so "NFR" wins over "FR" when both would match at the same spot.
        var pattern = new Regex(
            @"\b(" + string.Join("|", acronyms.OrderByDescending(a => a.Term.Length).Select(a => Regex.Escape(a.Term))) + @")\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var byTerm = acronyms.ToDictionary(a => a.Term, StringComparer.Ordinal);

        // First-use-per-page state: lives for the duration of this single call, so each page independently
        // expands its own first occurrences.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var parts = ProtectedSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            // Odd indices are the captured protected spans — leave them untouched.
            if (i % 2 == 1) continue;
            parts[i] = ReplaceFirstUse(parts[i], pattern, byTerm, seen);
        }
        return string.Concat(parts);
    }

    private static string ReplaceFirstUse(string text, Regex pattern, IReadOnlyDictionary<string, GlossaryTerm> byTerm, HashSet<string> seen)
    {
        return pattern.Replace(text, m =>
        {
            var term = byTerm[m.Value];
            if (!seen.Add(term.Term))
            {
                return m.Value;
            }

            return $"<abbr title=\"{PathUtil.Html(term.Expansion)}\">{m.Value}</abbr>";
        });
    }
}
