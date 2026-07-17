using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Pure string→string post-processor that wraps selected H2 sections of already-rendered
/// remainder HTML in a collapsed-by-default native <c>&lt;details&gt;</c> disclosure. Used on drafted
/// story pages so Dev Notes / References stay out of the reviewer's first glance while Context &amp; Scope
/// and Tasks stay expanded. Deterministic, framework-agnostic (no-match → passthrough). [Story 9.5]</summary>
public static partial class CollapsibleSections
{
    /// <summary>Default slug set for story-page remainder collapse: Markdig auto-ids for
    /// <c>## Dev Notes</c> and a top-level <c>## References</c> (defensive — usually an H3 under Dev Notes).</summary>
    public static readonly IReadOnlySet<string> StoryRemainderSlugs =
        new HashSet<string>(StringComparer.Ordinal) { "dev-notes", "references" };

    // Match an H2 that already carries an id attribute (Markdig AutoIdentifier). Capture the full open tag,
    // the slug, the inner HTML, and the close tag so we can re-emit the heading inside <summary> unchanged
    // except for relocating it. Id match is case-sensitive — Markdig lowercases.
    [GeneratedRegex(
        @"<h2\b(?<open>[^>]*\bid=""(?<id>[^""]+)""[^>]*)>(?<inner>.*?)</h2>",
        RegexOptions.Singleline)]
    private static partial Regex H2WithId();

    // Strip id="…" from buried headings (h3+) so Toc.ExtractHeadings (id-gated) drops them from the sidebar.
    // Documented trade-off: those subsections lose their deep-anchor even when the details is expanded —
    // nothing deep-links to remainder H3 ids today (AC refs → #ac-N; Source citations → external pages).
    [GeneratedRegex(
        @"<(?<tag>h[3-6])\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOpenTag();

    [GeneratedRegex(@"\s*\bid=""[^""]*""", RegexOptions.None)]
    private static partial Regex IdAttribute();

    /// <summary>Wraps every H2 whose Markdig auto-id is in <paramref name="headingSlugs"/> in a
    /// collapsed <c>&lt;details class="collapsible-section"&gt;</c>, keeping the H2 (with its id) inside
    /// the always-visible <c>&lt;summary&gt;</c> and stripping ids from buried h3+ headings. Returns
    /// <paramref name="remainderHtml"/> unchanged when nothing matches (NFR8 degrade-to-absent).</summary>
    public static string WrapSections(string remainderHtml, IReadOnlySet<string> headingSlugs)
    {
        if (string.IsNullOrEmpty(remainderHtml) || headingSlugs.Count == 0)
            return remainderHtml;

        var matches = H2WithId().Matches(remainderHtml);
        if (matches.Count == 0) return remainderHtml;

        // Collect (matchIndex → sectionEnd) for matching slugs only, then wrap last→first so earlier
        // offsets stay valid. Section body runs from the H2 through the character before the next H2
        // (any H2 — matching or not) or end of fragment.
        var allH2 = matches.Cast<Match>().ToList();
        var sections = new List<(int Start, int End, string Slug, string HeadingHtml)>();
        for (var i = 0; i < allH2.Count; i++)
        {
            var m = allH2[i];
            var slug = m.Groups["id"].Value;
            if (!headingSlugs.Contains(slug)) continue;

            var sectionEnd = i + 1 < allH2.Count ? allH2[i + 1].Index : remainderHtml.Length;
            sections.Add((m.Index, sectionEnd, slug, m.Value));
        }

        if (sections.Count == 0) return remainderHtml;

        var sb = new StringBuilder(remainderHtml);
        for (var i = sections.Count - 1; i >= 0; i--)
        {
            var (start, end, slug, headingHtml) = sections[i];
            var section = sb.ToString(start, end - start);
            // Body = everything after the heading tag inside this section.
            var body = section.Length > headingHtml.Length
                ? section[headingHtml.Length..]
                : string.Empty;
            body = StripBuriedHeadingIds(body);

            var wrapped = new StringBuilder(section.Length + 96);
            wrapped.Append("<details class=\"collapsible-section\" id=\"")
                .Append(PathUtil.Html(slug))
                .Append("-section\">\n")
                .Append("<summary>")
                .Append(headingHtml)
                .Append("</summary>\n")
                .Append(body)
                .Append("</details>\n");

            sb.Remove(start, end - start);
            sb.Insert(start, wrapped.ToString());
        }

        return sb.ToString();
    }

    /// <summary>Convenience overload using <see cref="StoryRemainderSlugs"/>. After wrapping, moves every
    /// collapsible block to the end of the remainder so Dev Notes / References sit just above Change Log
    /// on the story page (Context &amp; Scope and Tasks stay above).</summary>
    public static string WrapStoryRemainder(string remainderHtml)
        => MoveCollapsibleSectionsToEnd(WrapSections(remainderHtml, StoryRemainderSlugs));

    /// <summary>Pulls every <c>collapsible-section</c> details block out of document order and appends them
    /// at the end — so collapsed working notes trail the expanded claim sections.</summary>
    public static string MoveCollapsibleSectionsToEnd(string html)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains("collapsible-section", StringComparison.Ordinal))
            return html;

        var pulled = new List<string>();
        var remainder = CollapsibleDetails().Replace(html, m =>
        {
            pulled.Add(m.Value.TrimEnd() + "\n");
            return string.Empty;
        });
        if (pulled.Count == 0) return html;

        var sb = new StringBuilder(html.Length + 8);
        sb.Append(remainder.TrimEnd());
        if (sb.Length > 0) sb.Append('\n');
        foreach (var block in pulled) sb.Append(block);
        return sb.ToString();
    }

    [GeneratedRegex(
        @"<details\s+class=""collapsible-section""[^>]*>.*?</details>\s*",
        RegexOptions.Singleline)]
    private static partial Regex CollapsibleDetails();

    private static string StripBuriedHeadingIds(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        return HeadingOpenTag().Replace(body, static m =>
        {
            var attrs = IdAttribute().Replace(m.Groups["attrs"].Value, string.Empty);
            return $"<{m.Groups["tag"].Value}{attrs}>";
        });
    }
}
