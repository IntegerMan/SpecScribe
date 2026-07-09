using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns plain "Story 1.5" / "Epic 2" mentions — inert text Markdig has no reason to linkify —
/// into real links to that story's or epic's generated page, wherever they appear in a rendered body.
/// Runs as a whole-page post-process (like <see cref="RequirementLinkifier"/>) so a mention is linked no
/// matter which template emitted it. Anchor-aware: it never rewrites a mention already inside an
/// &lt;a&gt;…&lt;/a&gt; span (TOC entries, breadcrumbs, cards), and it also skips &lt;code&gt;/&lt;pre&gt;
/// spans so BMad command snippets like <c>create-story 2.6</c> stay copyable plain text. Only ids present
/// in the parsed plan are linked; every story links to <c>epics/story-N-M.html</c>, which exists for all
/// stories now that undrafted ones get a placeholder page there.</summary>
public static class StoryEpicLinkifier
{
    // Captures whole spans that must never receive an injected <a> so Regex.Split hands them back as
    // delimiters we skip over:
    //   a    — don't double-link an existing anchor (TOC, breadcrumb, card).
    //   code — BMad command snippets like `create-story 2.6` must stay copyable plain text.
    //   pre  — Mermaid sources (roadmap nodes say "Epic 1").
    //   svg  — chart <title>/aria text ("Epic 2: …") not wrapped in its own anchor.
    //   head — <title>/<meta content="…"> carry the page title; a doc titled "Epic 1 Retrospective" would
    //          otherwise get an <a href="…"> injected inside <title> and inside a content="…" attribute,
    //          whose own double quotes terminate the attribute and corrupt the document head.
    //   script/style — raw JS/CSS text (Markdig passes embedded HTML through) must not be rewritten.
    //   any other single tag — a mention can also live inside an attribute value (data-tip, aria-label,
    //   title, alt, …) on a plain element that isn't itself an <a> (e.g. a fallback sprint board card with
    //   no href renders as a <div data-tip="Epic 3: …">, not an <a>). Rewriting text INSIDE that tag would
    //   inject a raw <a>…</a> into the attribute value and corrupt the tag. Matching every standalone tag
    //   here — after the element-pair alternatives above have first claim — restricts replacement to real
    //   text nodes only, never tag markup. [bugfix: sprint board card tooltip HTML corruption]
    private static readonly Regex ProtectedSplit = new(
        "(<a\\b[^>]*>.*?</a>|<code\\b[^>]*>.*?</code>|<pre\\b[^>]*>.*?</pre>|<svg\\b[^>]*>.*?</svg>"
        + "|<head\\b[^>]*>.*?</head>|<script\\b[^>]*>.*?</script>|<style\\b[^>]*>.*?</style>|<[^>]*>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Capitalized mention forms only — "Story 1.5" / "Epic 2" — matching how the artifacts are authored.
    // \s+ (not a literal space) so a mention hard-wrapped across a source line ("Story\n1.5") still links.
    // The (?!\.\d) tail stops a three-part id ("Story 1.5.2") from partially matching as "Story 1.5".
    private static readonly Regex StoryPattern = new(@"\bStory\s+(\d+)\.(\d+)\b(?!\.\d)", RegexOptions.Compiled);
    private static readonly Regex EpicPattern = new(@"\bEpic\s+(\d+)\b(?!\.\d)", RegexOptions.Compiled);

    /// <summary>The generated page path for an epic, relative to the output root.</summary>
    public static string EpicPagePath(int epicNumber) => $"epics/epic-{epicNumber}.html";

    /// <summary>The generated page path for a story (real or placeholder), relative to the output root.</summary>
    public static string StoryPagePath(string storyId) => $"epics/story-{storyId.Replace('.', '-')}.html";

    /// <param name="html">Already-rendered HTML to scan.</param>
    /// <param name="model">The parsed plan — only stories/epics present here are linked.</param>
    /// <param name="outputRelativePrefix">The "../" depth prefix from the current page to the output root.</param>
    /// <param name="skipStoryId">A story id ("N.M") to leave as plain text (its own page).</param>
    /// <param name="skipEpicNumber">An epic number to leave as plain text (its own page).</param>
    public static string Linkify(string html, EpicsModel model, string outputRelativePrefix, string? skipStoryId = null, int? skipEpicNumber = null)
    {
        if (string.IsNullOrEmpty(html) || model.Epics.Count == 0)
        {
            return html;
        }

        var epicNumbers = new HashSet<int>(model.Epics.Select(e => e.Number));
        var storyIds = new HashSet<string>(model.Epics.SelectMany(e => e.Stories).Select(s => s.Id), StringComparer.Ordinal);

        var parts = ProtectedSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            // Odd indices are the captured protected spans — leave them untouched.
            if (i % 2 == 1) continue;
            parts[i] = ReplaceMentions(parts[i], epicNumbers, storyIds, outputRelativePrefix, skipStoryId, skipEpicNumber);
        }
        return string.Concat(parts);
    }

    private static string ReplaceMentions(string text, HashSet<int> epicNumbers, HashSet<string> storyIds, string prefix, string? skipStoryId, int? skipEpicNumber)
    {
        text = StoryPattern.Replace(text, m =>
        {
            // Reject leading-zero digit runs ("Story 1.05") rather than silently normalizing them to a
            // different, confidently-wrong target ("1.5"). TryParse also keeps an absurd digit run
            // ("Story 99999999999.1") from throwing OverflowException and failing the whole epics pass.
            if (HasLeadingZero(m.Groups[1].Value) || HasLeadingZero(m.Groups[2].Value)
                || !int.TryParse(m.Groups[1].Value, out var epic) || !int.TryParse(m.Groups[2].Value, out var story))
            {
                return m.Value;
            }
            var id = $"{epic}.{story}";
            if (!storyIds.Contains(id) || string.Equals(id, skipStoryId, StringComparison.Ordinal))
            {
                return m.Value;
            }
            return $"<a class=\"story-ref\" href=\"{PathUtil.Html(prefix + StoryPagePath(id))}\">{m.Value}</a>";
        });

        return EpicPattern.Replace(text, m =>
        {
            if (HasLeadingZero(m.Groups[1].Value) || !int.TryParse(m.Groups[1].Value, out var number))
            {
                return m.Value;
            }
            if (!epicNumbers.Contains(number) || number == skipEpicNumber)
            {
                return m.Value;
            }
            return $"<a class=\"epic-ref\" href=\"{PathUtil.Html(prefix + EpicPagePath(number))}\">{m.Value}</a>";
        });
    }

    private static bool HasLeadingZero(string digits) => digits.Length > 1 && digits[0] == '0';
}
