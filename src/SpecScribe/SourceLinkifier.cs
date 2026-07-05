using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns "[Source: _bmad-output/path/to/doc.md#Some Heading]" citations — plain bracketed text
/// Markdig has no reason to linkify — into real links to the corresponding generated page, wherever they
/// appear in a rendered body (References list, inline Dev Notes citations, etc.). Only the file path is
/// linked; a trailing "#Fragment" note is left as plain text since it names prose sections, not real
/// heading ids.</summary>
public static class SourceLinkifier
{
    private static readonly Regex SourcePathPattern = new(
        @"_bmad-output/(?<path>[^\]\n]+?\.md)",
        RegexOptions.Compiled);

    /// <param name="html">Already-rendered HTML to scan.</param>
    /// <param name="referenceMap">Source-relative path (forward slashes, no "_bmad-output/" prefix) to
    /// output-relative URL, e.g. "game-architecture.md" -> "game-architecture.html".</param>
    /// <param name="outputRelativePrefix">The "../" depth prefix from the current page to the output root.</param>
    public static string Linkify(string html, IReadOnlyDictionary<string, string> referenceMap, string outputRelativePrefix)
    {
        return SourcePathPattern.Replace(html, m =>
        {
            var path = PathUtil.NormalizeSlashes(m.Groups["path"].Value);
            if (!referenceMap.TryGetValue(path, out var url))
            {
                return m.Value;
            }

            var href = outputRelativePrefix + url;
            return $"<a href=\"{PathUtil.Html(href)}\">{m.Value}</a>";
        });
    }
}
