using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns plain "FR25"/"NFR7" references — inert text Markdig has no reason to linkify — into
/// real links to that requirement's generated page, wherever they appear in a rendered body. Runs as a
/// whole-page post-process (like <see cref="SourceLinkifier"/>) so a reference is linked no matter which
/// template emitted it. Anchor-aware: it never rewrites a token already inside an &lt;a&gt;…&lt;/a&gt;
/// span, so re-linking the requirement pages' own links (or nav) is a no-op. Also skips &lt;code&gt;/&lt;pre&gt;/
/// &lt;svg&gt;/&lt;head&gt;/&lt;script&gt;/&lt;style&gt; and every standalone tag so attribute values
/// (<c>data-copy</c>, <c>data-tip</c>, <c>aria-label</c>, …) never receive an injected &lt;a&gt; — the same
/// corruption trap <see cref="StoryEpicLinkifier"/> already guards against.</summary>
public static class RequirementLinkifier
{
    // Same protected-split grammar as StoryEpicLinkifier: element pairs first, then any standalone tag so
    // replacement stays in text nodes only. Without the standalone-tag arm, a mention inside data-copy /
    // data-tip would get an <a href="…"> injected into the attribute and shatter the tag.
    // [spec-address-deferred-next-steps UX]
    private static readonly Regex ProtectedSplit = new(
        "(<a\\b[^>]*>.*?</a>|<code\\b[^>]*>.*?</code>|<pre\\b[^>]*>.*?</pre>|<svg\\b[^>]*>.*?</svg>"
        + "|<head\\b[^>]*>.*?</head>|<script\\b[^>]*>.*?</script>|<style\\b[^>]*>.*?</style>|<[^>]*>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Whole-token match for FR / NFR / UX-DR ids (word-boundary anchored). UX-DR is a single alternation
    // arm, so it cannot be partially matched as a bare "DR{n}". [Story 9.2 Task 5]
    private static readonly Regex RefPattern = new(@"\b(FR|NFR|UX-DR)(\d+)\b", RegexOptions.Compiled);

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

        var parts = ProtectedSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            // Odd indices are the captured protected spans — leave them untouched.
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
