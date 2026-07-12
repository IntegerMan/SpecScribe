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

        AppendRelationships(sb, prefix, repoRelativePath, referencedBy);

        // Source is deliberately secondary — the relationships above are the point of a code page here. The block
        // still carries the locked per-line id="L{n}" anchors (kept visible, never collapsed, so a "#L42" deep link
        // lands on every browser) and a data-code-path hook for host re-targeting (VS Code recommendation R4.2).
        sb.Append($"<section class=\"code-source-section\" data-code-path=\"{PathUtil.Html(PathUtil.NormalizeSlashes(repoRelativePath))}\">\n");
        sb.Append("  <div class=\"code-source-head\">\n");
        sb.Append("    <h2>Source</h2>\n");
        if (externalSourceUrl is { Length: > 0 })
        {
            sb.Append("    ").Append(ExternalSourceAnchor(externalSourceUrl)).Append('\n');
        }
        sb.Append("  </div>\n");

        sb.Append("<pre class=\"code-file\"><code>");
        for (var i = 0; i < count; i++)
        {
            var n = i + 1;
            sb.Append($"<span class=\"code-line\" id=\"L{n.ToString(CultureInfo.InvariantCulture)}\">")
              .Append($"<span class=\"code-ln\">{n.ToString(CultureInfo.InvariantCulture)}</span>")
              .Append($"<span class=\"code-src\">{PathUtil.Html(lines[i])}</span></span>\n");
        }
        sb.Append("</code></pre>\n");
        sb.Append("</section>\n\n");

        return EndShell(sb, prefix);
    }

    /// <summary>Emits the relationships block (Story 7.1 rework) — the hero of a code page: a pure-SVG node-link
    /// graph (<see cref="Charts.ReferenceGraph"/>) of the artifacts that cite this file, plus an always-present
    /// semantic <c>&lt;ul&gt;</c> equivalent for screen readers and no-image contexts. Both share one resolved node
    /// list so the graph and the list never disagree; link text is the citing artifact's title (never "click here" —
    /// NFR6/UX-DR16). The note states the honest scope: references run artifact&#8594;file, so this is "what refers to
    /// the file", not a code call/dependency graph. An empty list writes nothing (no bare heading).</summary>
    private static void AppendRelationships(
        StringBuilder sb, string prefix, string repoRelativePath, IReadOnlyList<(string OutputUrl, string Title)>? referencedBy)
    {
        if (referencedBy is null || referencedBy.Count == 0) return;

        // Resolve each citing artifact once to (href, full title, compact label) — shared by the graph and the list.
        var nodes = new List<(string Href, string Title, string Short)>(referencedBy.Count);
        foreach (var (outputUrl, title) in referencedBy)
        {
            var href = prefix + PathUtil.NormalizeSlashes(outputUrl);
            nodes.Add((href, title, ShortLabel(title)));
        }

        sb.Append("<section class=\"code-relationships\">\n");
        sb.Append("  <h2>Referenced by</h2>\n");
        sb.Append("  <p class=\"code-relationships-note\">The artifacts that cite this file. References run artifact&#8594;file, so this shows what refers to the file — not code dependencies.</p>\n");
        sb.Append("  <div class=\"ref-graph-wrap\">\n");
        sb.Append(Charts.ReferenceGraph(BaseName(repoRelativePath), nodes));
        sb.Append("  </div>\n");
        sb.Append("  <ul class=\"ref-list\">\n");
        foreach (var (href, title, _) in nodes)
        {
            sb.Append($"    <li><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(title)}</a></li>\n");
        }
        sb.Append("  </ul>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>The <c>&lt;a&gt;</c> to the same file on its hosting platform (Story 7.7), an <em>additive</em> link
    /// out that never replaces the in-portal page. The label names the host when recognizable (GitHub/GitLab/
    /// Bitbucket) and otherwise stays generic, so the external destination is truthful. <c>rel="noopener"</c> since
    /// this leaves the portal.</summary>
    private static string ExternalSourceAnchor(string url) =>
        $"<a class=\"code-external-link\" href=\"{PathUtil.Html(url)}\" rel=\"noopener noreferrer\">{PathUtil.Html(ExternalLinkLabel(url))} ↗</a>";

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
        // viewable on its hosting platform — so both survive the degraded page.
        AppendRelationships(sb, prefix, repoRelativePath, referencedBy);
        sb.Append($"<p class=\"code-placeholder\">{PathUtil.Html(reason)}</p>\n\n");
        if (externalSourceUrl is { Length: > 0 })
        {
            sb.Append($"<p class=\"code-external-standalone\">{ExternalSourceAnchor(externalSourceUrl)}</p>\n\n");
        }

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
