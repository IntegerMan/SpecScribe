using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Small stateless helpers shared by every page-rendering class (relative-link math, HTML escaping, the common page shell).</summary>
public static class PathUtil
{
    public static string NormalizeSlashes(string path) => path.Replace('\\', '/');

    /// <summary>Number of "../" segments needed to reach the output root from this file's directory.
    /// Deliberately avoids Path.GetDirectoryName, which renormalizes separators to the OS style on
    /// Windows (back to '\') and silently breaks a naive forward-slash split.</summary>
    public static string RelativePrefix(string outputRelativePath)
    {
        var segments = NormalizeSlashes(outputRelativePath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var depth = segments.Length - 1;
        return depth <= 0 ? string.Empty : string.Concat(Enumerable.Repeat("../", depth));
    }

    public static string ToOutputRelative(string sourceRelativePath) => Path.ChangeExtension(sourceRelativePath, ".html");

    public static string Html(string s) => WebUtility.HtmlEncode(s);

    public static string RenderHeadOpen(string title, string cssHref)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"UTF-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n");
        sb.Append($"<title>{Html(title)}</title>\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{Html(cssHref)}\">\n");
        sb.Append("</head>\n<body>\n");
        // Skip link is the first focusable element on every page — a keyboard user can jump straight past
        // the nav to the page's single <main id="main-content"> landmark. [Story 1.4 AC #1, UX-DR16]
        sb.Append("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>\n");
        return sb.ToString();
    }

    public static string RenderFooter(string trailingHtml)
        => $"<footer class=\"doc-footer\">\n  Generated using <a href=\"https://github.com/IntegerMan/SpecScribe\">SpecScribe</a> {trailingHtml}\n</footer>\n\n";

    // Singleline so a tag whose attributes contain newlines still strips cleanly — e.g. an "(AC: #N)"
    // reference linkified inside a heading carries a multi-line `title` (the criterion's Given/When/Then
    // text), and without Singleline the `.` would stop at the first newline and leave the raw tag behind.
    private static readonly Regex TagStripRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Strips HTML tags AND decodes entities, leaving true plain text — used where
    /// already-rendered inline HTML (e.g. an epic title with an embedded &lt;code&gt; span) needs to appear
    /// in a context that will be re-encoded (page titles, breadcrumbs, SVG tooltips). Without the decode,
    /// "&amp;amp;" in the source would double-encode to a literal "&amp;amp;amp;" on screen.</summary>
    public static string StripHtmlTags(string html) => WebUtility.HtmlDecode(TagStripRegex.Replace(html, string.Empty));
}
