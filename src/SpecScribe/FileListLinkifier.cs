using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Turns Dev Agent Record File List rows into typed links to in-portal pages when available.
/// [ADR 0007; Story 9.4]</summary>
public static class FileListLinkifier
{
    private static readonly Regex ListItem = new(@"<li>([^<]*)</li>", RegexOptions.Compiled);

    /// <summary>Rewrites plain-text <c>&lt;li&gt;path&lt;/li&gt;</c> entries into links when
    /// <paramref name="resolveLink"/> returns a link.</summary>
    public static string LinkifyHtml(string html, Func<string, FileListLink?> resolveLink)
    {
        if (html.Length == 0) return html;
        return ListItem.Replace(html, m =>
        {
            var text = m.Groups[1].Value.Trim();
            if (text.Length == 0) return m.Value;
            var link = resolveLink(text);
            return link is { Href.Length: > 0 } l
                ? $"<li class=\"{PathUtil.Html(l.CssClass)}\"><a href=\"{PathUtil.Html(l.Href)}\">{PathUtil.Html(text)}</a></li>"
                : m.Value;
        });
    }
}
