using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The single shared table-of-contents seam: one sidebar renderer, one two-column page shell, and a
/// rendered-order heading extractor, reused by every TOC-bearing page type (generic docs, ADRs, README via
/// <see cref="HtmlTemplater"/>; epic/story detail via <see cref="EpicsTemplater"/>). The TOC is never forked
/// per page — callers just build an ordered <see cref="Entry"/> list in the page's actual rendered order.</summary>
public static class Toc
{
    /// <summary>One contents entry: a heading/section <paramref name="Level"/> (2 or 3 — 3 renders indented),
    /// its visible <paramref name="Text"/>, and the in-page <paramref name="AnchorId"/> it links to.</summary>
    public sealed record Entry(int Level, string Text, string AnchorId);

    /// <summary>Renders the accessible "On this page" sidebar nav from an ordered entry list. Returns "" when
    /// there are no entries so the caller can fall back to a single-column layout.</summary>
    public static string RenderSidebar(IReadOnlyList<Entry> entries)
    {
        if (entries.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<nav class=\"toc-sidebar\" aria-label=\"On this page\">\n");
        sb.Append("  <span class=\"toc-label\">On this page</span>\n");
        foreach (var e in entries)
        {
            var cls = e.Level >= 3 ? "toc-link toc-h3" : "toc-link";
            sb.Append($"  <a class=\"{cls}\" href=\"#{PathUtil.Html(e.AnchorId)}\">{PathUtil.Html(e.Text)}</a>\n");
        }
        sb.Append("</nav>\n");
        return sb.ToString();
    }

    /// <summary>Wraps main-content HTML and its TOC sidebar into the shared two-column page shell. With no
    /// entries the content is returned unwrapped (single column) so pages with nothing to index keep their
    /// existing full-width layout.</summary>
    public static string WrapWithSidebar(string mainHtml, IReadOnlyList<Entry> entries)
    {
        if (entries.Count == 0) return mainHtml;

        var sb = new StringBuilder();
        sb.Append("<div class=\"page-shell\">\n");
        sb.Append("<div class=\"page-main\">\n");
        sb.Append(mainHtml);
        sb.Append("</div>\n");
        sb.Append(RenderSidebar(entries));
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    private static readonly Regex HeadingTag = new(
        @"<h(?<lvl>[23])\b[^>]*\bid=""(?<id>[^""]+)""[^>]*>(?<text>.*?)</h\k<lvl>>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Extracts level-2/3 headings that carry an id from already-rendered fragment HTML, in document
    /// order, as TOC entries — used to slot a detail page's remainder-fragment headings into its rendered-order
    /// TOC. Headings without an id are skipped (there is no anchor to link to), so no entry is ever a dead link.</summary>
    public static IReadOnlyList<Entry> ExtractHeadings(string html)
    {
        var entries = new List<Entry>();
        if (string.IsNullOrEmpty(html)) return entries;

        foreach (Match m in HeadingTag.Matches(html))
        {
            var level = m.Groups["lvl"].Value == "3" ? 3 : 2;
            var text = PathUtil.StripHtmlTags(m.Groups["text"].Value).Trim();
            if (text.Length == 0) continue;
            entries.Add(new Entry(level, text, m.Groups["id"].Value));
        }
        return entries;
    }
}
