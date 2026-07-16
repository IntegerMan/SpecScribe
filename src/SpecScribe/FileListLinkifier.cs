using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns Dev Agent Record File List rows and change-surface file entries into links to in-portal
/// <c>code/…html</c> pages when those pages exist. [ADR 0007; Story 9.4]</summary>
public static class FileListLinkifier
{
    private static readonly Regex ListItem = new(@"<li>([^<]*)</li>", RegexOptions.Compiled);

    /// <summary>Rewrites plain-text <c>&lt;li&gt;path&lt;/li&gt;</c> entries into links when
    /// <paramref name="resolveHref"/> returns a page-relative href.</summary>
    public static string LinkifyHtml(string html, Func<string, string?> resolveHref)
    {
        if (html.Length == 0) return html;
        return ListItem.Replace(html, m =>
        {
            var text = m.Groups[1].Value.Trim();
            if (text.Length == 0) return m.Value;
            var path = ChangeSurface.NormalizeFileListPath(text);
            var href = resolveHref(path);
            return href is { Length: > 0 }
                ? $"<li><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(text)}</a></li>"
                : m.Value;
        });
    }
}
