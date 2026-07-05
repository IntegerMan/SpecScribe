using System.Text.RegularExpressions;

namespace DocsForge;

/// <summary>Rewrites the Markdown-authored <c>.md</c> links inside a rendered ADR body so they point at the
/// generated pages instead of the raw source. ADRs cross-link heavily — to sibling records
/// (<c>0004-title.md</c>) and to the architecture doc (<c>../../_bmad-output/game-architecture.md</c>) — and
/// those hrefs would 404 in <c>docs/live/</c> if left untouched.</summary>
public static class AdrLinkRewriter
{
    // href to any *.md (optionally with a #fragment). Absolute URLs never end in ".md" here, so this
    // only ever catches the repo-relative links markdig emitted from the ADR source.
    private static readonly Regex MdHrefPattern = new(
        "href=\"(?<path>[^\":#]+\\.md)(?<frag>#[^\"]*)?\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Rewrites ADR body links. Every ADR page lives one directory deep (<c>docs/live/adrs/</c>), so a
    /// link into <c>_bmad-output</c> resolves to <c>../&lt;mirrored path&gt;.html</c>, a sibling record stays in
    /// the same folder, and <c>README.md</c> maps to the ADR landing (<c>index.html</c>).</summary>
    public static string Rewrite(string bodyHtml) =>
        MdHrefPattern.Replace(bodyHtml, m =>
        {
            var path = PathUtil.NormalizeSlashes(m.Groups["path"].Value);
            var frag = m.Groups["frag"].Value;
            return $"href=\"{PathUtil.Html(MapTarget(path) + frag)}\"";
        });

    private static string MapTarget(string mdPath)
    {
        var bmadIndex = mdPath.IndexOf("_bmad-output/", StringComparison.OrdinalIgnoreCase);
        if (bmadIndex >= 0)
        {
            // e.g. "../../_bmad-output/game-architecture.md" -> "../game-architecture.html"
            var tail = mdPath[(bmadIndex + "_bmad-output/".Length)..];
            return "../" + PathUtil.ToOutputRelative(tail);
        }

        // Trim a leading "./" so bare sibling references normalize cleanly.
        if (mdPath.StartsWith("./", StringComparison.Ordinal))
        {
            mdPath = mdPath[2..];
        }

        // The README is rendered as the ADR landing page.
        if (string.Equals(mdPath, "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return "index.html";
        }

        // Sibling record (or any other same-tree doc): a straight extension swap keeps it in place.
        return PathUtil.ToOutputRelative(mdPath);
    }
}
