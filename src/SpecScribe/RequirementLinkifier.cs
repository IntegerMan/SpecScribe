using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns plain "FR25"/"NFR7" references — inert text Markdig has no reason to linkify — into
/// real links to that requirement's generated page, wherever they appear in a rendered body. Runs as a
/// whole-page post-process (like <see cref="SourceLinkifier"/>) so a reference is linked no matter which
/// template emitted it. Anchor-aware: it never rewrites a token already inside an &lt;a&gt;…&lt;/a&gt;
/// span, so re-linking the requirement pages' own links (or nav) is a no-op.</summary>
public static class RequirementLinkifier
{
    // Captures whole anchor spans so Regex.Split hands them back as delimiters we skip over.
    private static readonly Regex AnchorSplit = new(
        "(<a\\b[^>]*>.*?</a>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex RefPattern = new(@"\b(FR|NFR)(\d+)\b", RegexOptions.Compiled);

    /// <param name="html">Already-rendered HTML to scan.</param>
    /// <param name="requirements">The known requirement set — only ids present here are linked.</param>
    /// <param name="outputRelativePrefix">The "../" depth prefix from the current page to the output root.</param>
    /// <param name="skipId">A requirement id to leave as plain text (its own detail page).</param>
    public static string Linkify(string html, RequirementsModel requirements, string outputRelativePrefix, string? skipId = null)
    {
        if (string.IsNullOrEmpty(html) || requirements.ById.Count == 0)
        {
            return html;
        }

        var parts = AnchorSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            // Odd indices are the captured <a>…</a> spans — leave them untouched.
            if (i % 2 == 1) continue;
            parts[i] = ReplaceRefs(parts[i], requirements, outputRelativePrefix, skipId);
        }
        return string.Concat(parts);
    }

    private static string ReplaceRefs(string text, RequirementsModel requirements, string prefix, string? skipId)
    {
        return RefPattern.Replace(text, m =>
        {
            var id = m.Groups[1].Value + m.Groups[2].Value;
            if (skipId is not null && string.Equals(id, skipId, StringComparison.OrdinalIgnoreCase))
            {
                return m.Value;
            }
            if (!requirements.ById.TryGetValue(id, out var req))
            {
                return m.Value;
            }

            var href = prefix + "requirements/" + req.Slug + ".html";
            return $"<a class=\"req-ref\" href=\"{PathUtil.Html(href)}\">{m.Value}</a>";
        });
    }
}
