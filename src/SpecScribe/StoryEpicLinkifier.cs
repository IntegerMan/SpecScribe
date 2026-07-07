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
    // Captures whole anchor/code/pre/svg spans so Regex.Split hands them back as delimiters we skip over.
    // pre protects Mermaid sources (roadmap nodes say "Epic 1"); svg protects chart <title>/aria text that
    // isn't wrapped in its own anchor — injecting an <a> inside either corrupts the rendered artifact.
    private static readonly Regex ProtectedSplit = new(
        "(<a\\b[^>]*>.*?</a>|<code\\b[^>]*>.*?</code>|<pre\\b[^>]*>.*?</pre>|<svg\\b[^>]*>.*?</svg>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Capitalized mention forms only — "Story 1.5" / "Epic 2" — matching how the artifacts are authored.
    private static readonly Regex StoryPattern = new(@"\bStory (\d+)\.(\d+)\b", RegexOptions.Compiled);
    private static readonly Regex EpicPattern = new(@"\bEpic (\d+)\b", RegexOptions.Compiled);

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
            var id = $"{int.Parse(m.Groups[1].Value)}.{int.Parse(m.Groups[2].Value)}";
            if (!storyIds.Contains(id) || string.Equals(id, skipStoryId, StringComparison.Ordinal))
            {
                return m.Value;
            }
            return $"<a class=\"story-ref\" href=\"{PathUtil.Html(prefix + StoryPagePath(id))}\">{m.Value}</a>";
        });

        return EpicPattern.Replace(text, m =>
        {
            var number = int.Parse(m.Groups[1].Value);
            if (!epicNumbers.Contains(number) || number == skipEpicNumber)
            {
                return m.Value;
            }
            return $"<a class=\"epic-ref\" href=\"{PathUtil.Html(prefix + EpicPagePath(number))}\">{m.Value}</a>";
        });
    }
}
