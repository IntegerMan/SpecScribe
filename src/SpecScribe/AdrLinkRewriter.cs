using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Rewrites the Markdown-authored <c>.md</c> links inside a rendered ADR body so they point at the
/// generated pages instead of the raw source. ADRs cross-link heavily — to sibling records
/// (<c>0004-title.md</c>) and to the architecture doc (<c>../../_bmad-output/game-architecture.md</c>) — and
/// those hrefs would 404 in <c>SpecScribeOutput/</c> if left untouched.</summary>
public static class AdrLinkRewriter
{
    // href to any *.md (optionally with a #fragment). Absolute URLs never end in ".md" here, so this
    // only ever catches the repo-relative links markdig emitted from the ADR source.
    private static readonly Regex MdHrefPattern = new(
        "href=\"(?<path>[^\":#]+\\.md)(?<frag>#[^\"]*)?\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Rewrites ADR body links. <paramref name="rootPrefix"/> is the page's climb back to the OUTPUT
    /// root (<see cref="PathUtil.RelativePrefix"/> of its output path) — <c>"../"</c> for a top-level record in
    /// <c>SpecScribeOutput/adrs/</c>, one segment more per nesting level (records may sit one directory deeper
    /// since Story 4.2) — so a link into <c>_bmad-output</c> resolves to the mirrored page at the right depth.
    /// A sibling record stays in the same folder, and a reference to the ADR root's <c>README.md</c> maps to
    /// the ADR landing (<c>index.html</c>).</summary>
    public static string Rewrite(string bodyHtml, string rootPrefix = "../") =>
        MdHrefPattern.Replace(bodyHtml, m =>
        {
            var path = PathUtil.NormalizeSlashes(m.Groups["path"].Value);
            var frag = m.Groups["frag"].Value;
            return $"href=\"{PathUtil.Html(MapTarget(path, rootPrefix) + frag)}\"";
        });

    private static string MapTarget(string mdPath, string rootPrefix)
    {
        var bmadIndex = mdPath.IndexOf("_bmad-output/", StringComparison.OrdinalIgnoreCase);
        if (bmadIndex >= 0)
        {
            // e.g. "../../_bmad-output/game-architecture.md" -> "../game-architecture.html" (from adrs/)
            var tail = mdPath[(bmadIndex + "_bmad-output/".Length)..];
            return rootPrefix + PathUtil.ToOutputRelative(tail);
        }

        // Trim a leading "./" so bare sibling references normalize cleanly.
        if (mdPath.StartsWith("./", StringComparison.Ordinal))
        {
            mdPath = mdPath[2..];
        }

        // The ADR root's README is rendered as the landing page. The root sits one level below the output
        // root (adrs/), so a reference climbs to it with exactly one "../" fewer than rootPrefix carries; a
        // nested README keeps its own name and needs no mapping.
        //
        // This subtraction is correct ONLY because SiteGenerator.EnumerateAdrFiles recurses exactly one level
        // below AdrSourceRoot (Task 1's explicit bound), so rootPrefix here is always "../" (top-level record)
        // or "../../" (one nested level) — never deeper. Nothing ties that recursion bound to this arithmetic
        // today; the assert below is the guard, so a future deepening of EnumerateAdrFiles fails loudly here
        // instead of silently mis-resolving README links for records nested more than one level down.
        // [deferred-adrlinkrewriter-climb-arithmetic]
        System.Diagnostics.Debug.Assert(
            rootPrefix.Length <= "../../".Length,
            "AdrLinkRewriter's climb arithmetic assumes ADR records nest at most one level below AdrSourceRoot " +
            "(see SiteGenerator.EnumerateAdrFiles); a deeper rootPrefix means that bound moved and this formula " +
            "needs revisiting.");
        var climbToAdrRoot = rootPrefix.Length >= "../".Length ? rootPrefix[..^"../".Length] : string.Empty;
        if (string.Equals(mdPath, climbToAdrRoot + "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return climbToAdrRoot + "index.html";
        }

        // Sibling record (or any other same-tree doc): a straight extension swap keeps it in place.
        return PathUtil.ToOutputRelative(mdPath);
    }
}
