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
    /// <summary>Renders the full code page. In this tool a code page leads with its <em>relationships</em> — the
    /// graph of artifacts that reference the file — and treats the source itself as secondary supporting detail;
    /// <see cref="AppendRelationships"/> is emitted first and the source table drops into a clearly-secondary
    /// <c>&lt;section class="code-source-section"&gt;</c> below it. <paramref name="lines"/> is still rendered verbatim
    /// — one anchored <c>.code-line</c> per element, numbered from 1, including blank lines — so line numbers stay
    /// 1:1 and every locked <c>id="L{n}"</c> anchor still resolves for Story 7.2's deep links. The caller owns
    /// newline normalization; escaping is applied here. <paramref name="referencedBy"/> (Story 7.2, AC #2) is the set
    /// of citing artifacts (output-relative URL + display title); an empty list omits the whole relationships block.
    /// <paramref name="externalSourceUrl"/> (Story 7.7), when set, adds an additive "view online" link to the hosted
    /// source — it never replaces the in-portal page.</summary>
    public static string RenderPage(
        string repoRelativePath,
        string outputRelativePath,
        IReadOnlyList<string> lines,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title)>? referencedBy = null,
        string? externalSourceUrl = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav);

        var count = lines.Count;
        sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "line" : "lines")}</span></div>\n");
        sb.Append("</header>\n\n");

        // Source is deliberately secondary — but it sits BESIDE the relationships (two columns), so a tall file no
        // longer pushes the graph out of view. The block still carries the locked per-line id="L{n}" anchors (kept
        // visible, never collapsed, so a "#L42" deep link lands on every browser) and a data-code-path hook for host
        // re-targeting (VS Code recommendation R4.2).
        var source = new StringBuilder();
        source.Append($"<section class=\"code-source-section\" data-code-path=\"{PathUtil.Html(PathUtil.NormalizeSlashes(repoRelativePath))}\">\n");
        source.Append("  <div class=\"code-source-head\">\n    <h2>Source</h2>\n  </div>\n");
        source.Append("<pre class=\"code-file\"><code>");
        for (var i = 0; i < count; i++)
        {
            var n = i + 1;
            source.Append($"<span class=\"code-line\" id=\"L{n.ToString(CultureInfo.InvariantCulture)}\">")
                  .Append($"<span class=\"code-ln\">{n.ToString(CultureInfo.InvariantCulture)}</span>")
                  .Append($"<span class=\"code-src\">{PathUtil.Html(lines[i])}</span></span>\n");
        }
        source.Append("</code></pre>\n</section>\n");

        AppendBody(sb, BuildAside(prefix, repoRelativePath, referencedBy, externalSourceUrl), source.ToString());

        return EndShell(sb, prefix);
    }

    /// <summary>Lays out the page body: the relationships aside beside the source in a two-column grid (the aside is
    /// a sticky sidebar; the source scrolls next to it), collapsing to a single column when there is no aside (an
    /// uncited file with no external link) so the source spans the full width.</summary>
    private static void AppendBody(StringBuilder sb, string aside, string body)
    {
        if (aside.Length == 0)
        {
            sb.Append(body).Append('\n');
            return;
        }
        sb.Append("<div class=\"code-layout\">\n").Append(aside).Append(body).Append("</div>\n\n");
    }

    /// <summary>Builds the left sidebar: the reference graph (Story 7.1 rework) — a pure-SVG node-link graph of the
    /// artifacts that cite this file, the hero of a code page — followed by the additive "view source online" action
    /// (Story 7.7). Returns empty when there is neither, so <see cref="AppendBody"/> renders the source full-width.
    /// A visually-hidden but present <c>&lt;ul&gt;</c> mirrors the graph's links for assistive tech (the <c>&lt;svg
    /// role="img"&gt;</c> exposes only its summary label, so this is the accessible, keyboard-reachable equivalent —
    /// meaningful link text, never "click here", NFR6/UX-DR16), while the visible surface stays just the graph.</summary>
    private static string BuildAside(
        string prefix, string repoRelativePath, IReadOnlyList<(string OutputUrl, string Title)>? referencedBy, string? externalSourceUrl)
    {
        var hasRefs = referencedBy is { Count: > 0 };
        var external = externalSourceUrl is { Length: > 0 } u ? ExternalSourceAnchor(u) : "";
        if (!hasRefs && external.Length == 0) return "";

        var sb = new StringBuilder();
        sb.Append("<aside class=\"code-aside\">\n");

        if (hasRefs)
        {
            // Resolve each citing artifact once to (href, full title, compact label) — shared by the graph and list.
            var nodes = new List<(string Href, string Title, string Short)>(referencedBy!.Count);
            foreach (var (outputUrl, title) in referencedBy)
            {
                nodes.Add((prefix + PathUtil.NormalizeSlashes(outputUrl), title, ShortLabel(title)));
            }

            sb.Append("<section class=\"code-relationships\">\n");
            sb.Append("  <h2>Referenced by</h2>\n");
            sb.Append("  <p class=\"code-relationships-note\">The artifacts that cite this file. References run artifact&#8594;file, so this shows what refers to the file — not code dependencies.</p>\n");
            sb.Append("  <div class=\"ref-graph-wrap\">\n");
            sb.Append(Charts.ReferenceGraph(BaseName(repoRelativePath), nodes));
            sb.Append("  </div>\n");
            sb.Append("  <ul class=\"ref-list sr-only\">\n");
            foreach (var (href, title, _) in nodes)
            {
                sb.Append($"    <li><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(title)}</a></li>\n");
            }
            sb.Append("  </ul>\n");
            sb.Append("</section>\n");
        }

        if (external.Length > 0)
        {
            sb.Append($"<div class=\"code-actions\">{external}</div>\n");
        }

        sb.Append("</aside>\n");
        return sb.ToString();
    }

    /// <summary>The <c>&lt;a&gt;</c> to the same file on its hosting platform (Story 7.7), an <em>additive</em> link
    /// out that never replaces the in-portal page. Leads with the host's mark (a GitHub logo when recognizable, else a
    /// generic external-link glyph) and a host-named label (GitHub/GitLab/Bitbucket), so the external destination is
    /// truthful. <c>rel="noopener"</c> since this leaves the portal.</summary>
    private static string ExternalSourceAnchor(string url) =>
        $"<a class=\"code-external-link\" href=\"{PathUtil.Html(url)}\" rel=\"noopener noreferrer\">{ExternalIcon(url)}<span>{PathUtil.Html(ExternalLinkLabel(url))}</span></a>";

    // Inline, self-contained marks (no external assets — the CSP forbids them). GitHub's mark for GitHub hosts; a
    // neutral "external link" glyph otherwise. Both aria-hidden — the anchor's text is the accessible name.
    private const string GitHubIcon =
        "<svg class=\"host-icon\" viewBox=\"0 0 16 16\" width=\"1.05em\" height=\"1.05em\" aria-hidden=\"true\" focusable=\"false\">" +
        "<path fill=\"currentColor\" d=\"M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 " +
        "0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 " +
        "1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 " +
        "0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 " +
        "2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 " +
        "3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z\"/></svg>";
    private const string ExternalGlyph =
        "<svg class=\"host-icon\" viewBox=\"0 0 24 24\" width=\"1.05em\" height=\"1.05em\" fill=\"none\" stroke=\"currentColor\" " +
        "stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">" +
        "<path d=\"M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6\"/><path d=\"M15 3h6v6\"/><path d=\"M10 14 21 3\"/></svg>";

    private static string ExternalIcon(string url) =>
        ExtractHost(url).Contains("github", StringComparison.OrdinalIgnoreCase) ? GitHubIcon : ExternalGlyph;

    private static string ExternalLinkLabel(string url)
    {
        var host = ExtractHost(url);
        if (host.Contains("github", StringComparison.OrdinalIgnoreCase)) return "View on GitHub";
        if (host.Contains("gitlab", StringComparison.OrdinalIgnoreCase)) return "View on GitLab";
        if (host.Contains("bitbucket", StringComparison.OrdinalIgnoreCase)) return "View on Bitbucket";
        return "View source online";
    }

    private static string ExtractHost(string url)
    {
        var scheme = url.IndexOf("://", StringComparison.Ordinal);
        var start = scheme >= 0 ? scheme + 3 : 0;
        var end = url.IndexOf('/', start);
        return end >= 0 ? url[start..end] : url[start..];
    }

    /// <summary>A compact ring label for the reference graph: the identifier before an early colon
    /// ("Story 7.1: …" &#8594; "Story 7.1", "ADR 0005: …" &#8594; "ADR 0005"); otherwise the full title, which the
    /// graph then ellipsis-truncates. The full title always stays on the node tooltip and in the list.</summary>
    private static string ShortLabel(string title)
    {
        var colon = title.IndexOf(':');
        return colon > 0 && colon <= 18 ? title[..colon].Trim() : title;
    }

    /// <summary>Filename (last forward-slash segment) of a repo-relative path — the center-node label for the graph
    /// while the page <c>&lt;h1&gt;</c> keeps the full path.</summary>
    private static string BaseName(string repoRelativePath)
    {
        var norm = PathUtil.NormalizeSlashes(repoRelativePath);
        var i = norm.LastIndexOf('/');
        return i >= 0 && i < norm.Length - 1 ? norm[(i + 1)..] : norm;
    }


    /// <summary>Renders a clearly-marked placeholder page for a referenced file that exists but can't be shown
    /// inline (binary, oversized, or unreadable). The page still carries the full nav/breadcrumb/a11y shell and a
    /// stable URL so navigation never breaks (AC #1) — only the line table is replaced by an explanatory note.</summary>
    public static string RenderPlaceholder(
        string repoRelativePath,
        string outputRelativePath,
        string reason,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title)>? referencedBy = null,
        string? externalSourceUrl = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav);

        sb.Append("  <div class=\"meta-pills\"><span class=\"pill\">Not rendered</span></div>\n");
        sb.Append("</header>\n\n");
        // A file that can't render inline still has relationships worth showing, and (Story 7.7) may still be
        // viewable on its hosting platform — so both survive the degraded page via the same two-column layout.
        var body = $"<section class=\"code-source-section\">\n  <div class=\"code-source-head\">\n    <h2>Source</h2>\n  </div>\n" +
                   $"<p class=\"code-placeholder\">{PathUtil.Html(reason)}</p>\n</section>\n";
        AppendBody(sb, BuildAside(prefix, repoRelativePath, referencedBy, externalSourceUrl), body);

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

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1] The .code-page wrapper
        // gives the header + two-column body a centered max-width with side gutters (this synthesized page has no
        // markdown .doc-body of its own to supply them, so content otherwise ran to the window edge).
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<div class=\"code-page\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Source File</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(repoRelativePath)}</h1>\n");
        return sb;
    }

    private static string EndShell(StringBuilder sb, string prefix)
    {
        sb.Append("</div>\n</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
