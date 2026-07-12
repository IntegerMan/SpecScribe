using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders one in-portal code file page (Story 7.1) — a line-numbered, HTML-escaped, monospace view of a
/// referenced repository source file at <c>code/&lt;repo-relative-path&gt;.html</c>. A synthesized page (no markdown
/// source), so it builds its own shell via <see cref="PathUtil.RenderHeadOpen"/> the way
/// <see cref="CommitDayTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
///
/// Every line gets a stable <c>id="L{n}"</c> anchor (1-based, GitHub-compatible) so citations rewritten in Story 7.2
/// can deep-link to <c>code/&lt;path&gt;.html#L42</c>; that anchor scheme is a locked cross-story convention. No
/// syntax highlighter and no client JS: "syntax-readable" here means legible-as-code (monospace, line numbers,
/// preserved whitespace, horizontal scroll for long lines), not tokenized coloring.</summary>
public static class CodeFileTemplater
{
    /// <summary>Renders the full code page. <paramref name="lines"/> is rendered verbatim — one anchored
    /// <c>.code-line</c> per element, numbered from 1, including blank lines — so line numbers stay 1:1 with the
    /// source. The caller owns newline normalization and escaping is applied here.</summary>
    public static string RenderPage(
        string repoRelativePath,
        string outputRelativePath,
        IReadOnlyList<string> lines,
        SiteNav nav)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav);

        var count = lines.Count;
        sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "line" : "lines")}</span></div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<pre class=\"code-file\"><code>");
        for (var i = 0; i < count; i++)
        {
            var n = i + 1;
            sb.Append($"<span class=\"code-line\" id=\"L{n.ToString(CultureInfo.InvariantCulture)}\">")
              .Append($"<span class=\"code-ln\">{n.ToString(CultureInfo.InvariantCulture)}</span>")
              .Append($"<span class=\"code-src\">{PathUtil.Html(lines[i])}</span></span>\n");
        }
        sb.Append("</code></pre>\n\n");

        return EndShell(sb, prefix);
    }

    /// <summary>Renders a clearly-marked placeholder page for a referenced file that exists but can't be shown
    /// inline (binary, oversized, or unreadable). The page still carries the full nav/breadcrumb/a11y shell and a
    /// stable URL so navigation never breaks (AC #1) — only the line table is replaced by an explanatory note.</summary>
    public static string RenderPlaceholder(
        string repoRelativePath,
        string outputRelativePath,
        string reason,
        SiteNav nav)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav);

        sb.Append("  <div class=\"meta-pills\"><span class=\"pill\">Not rendered</span></div>\n");
        sb.Append("</header>\n\n");
        sb.Append($"<p class=\"code-placeholder\">{PathUtil.Html(reason)}</p>\n\n");

        return EndShell(sb, prefix);
    }

    /// <summary>Emits the head + nav + breadcrumb + open <c>&lt;main&gt;</c>/<c>&lt;header&gt;</c> shared by both the
    /// full page and the placeholder. Leaves the header open so each caller appends its own meta pill(s) and closes
    /// it — mirroring the synthesized-page shape of <see cref="CommitDayTemplater"/>.</summary>
    private static StringBuilder BeginShell(string repoRelativePath, string outputRelativePath, string prefix, SiteNav nav)
    {
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{repoRelativePath} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Source file {repoRelativePath} in {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputRelativePath));
        sb.Append(SiteNav.RenderBreadcrumb(outputRelativePath, new (string, string?)[]
        {
            ("Home", "index.html"),
            (repoRelativePath, null),
        }));

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Source File</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(repoRelativePath)}</h1>\n");
        return sb;
    }

    private static string EndShell(StringBuilder sb, string prefix)
    {
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
