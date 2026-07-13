using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders one in-portal code file page (Story 7.1) — a line-numbered, HTML-escaped, monospace view of a
/// referenced repository source file at <c>code/&lt;repo-relative-path&gt;.html</c>. A synthesized page (no markdown
/// source), so it builds its own shell via <see cref="PathUtil.RenderHeadOpen"/> the way
/// <see cref="CommitDayTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
///
/// Every line gets a stable <c>id="L{n}"</c> anchor (1-based, GitHub-compatible) so citations rewritten in Story 7.2
/// can deep-link to <c>code/&lt;path&gt;.html#L42</c>; that anchor scheme is a locked cross-story convention. The
/// source renders as one contiguous <c>&lt;code class="language-&#42;"&gt;</c> block (per-line <c>.code-line</c> spans
/// carry the anchors; line numbers come from a CSS <c>::before</c> counter on <c>data-ln</c>, never from tokenized
/// text) so the vendored Prism highlighter tokenizes multi-line constructs correctly while its <c>keep-markup</c>
/// plugin preserves the anchors. Highlighting is a pure progressive enhancement: with JS off the block is still
/// legible monospace with working line numbers and <c>#L{n}</c> anchors.</summary>
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
    /// source — it never replaces the in-portal page.
    ///
    /// <para><paramref name="insight"/> (Story 7.4), when non-null, appends an opt-in "Advanced coverage" section
    /// under the source: the file's contributors (attribution), change frequency, coupled files, and a bounded
    /// change history — all gated on <c>--deep-git</c> upstream. A null insight renders nothing extra, so the
    /// baseline page is byte-identical to a run without deep-git. <paramref name="coupledFileHref"/> resolves a
    /// coupled file's repo-relative path to its <c>code/…html</c> page (null → plain text), and
    /// <paramref name="commitHref"/> resolves a history entry's short hash to its <c>commit/…html</c> page (null →
    /// plain <c>&lt;code&gt;</c>); both return output-relative paths that this method prefixes.</para></summary>
    public static string RenderPage(
        string repoRelativePath,
        string outputRelativePath,
        IReadOnlyList<string> lines,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title)>? referencedBy = null,
        string? externalSourceUrl = null,
        FileInsight? insight = null,
        Func<string, string?>? coupledFileHref = null,
        Func<string, string?>? commitHref = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav, highlight: true);

        var count = lines.Count;
        sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "line" : "lines")}</span></div>\n");
        sb.Append("</header>\n\n");

        // The "view online" jump-off rides ALONGSIDE the source (next to the "Source" heading) since it points at the
        // code, not at the insights — so it survives even when there is no insights tab.
        var source = BuildSource(repoRelativePath, lines, externalSourceUrl);

        // The insights view collects everything ABOUT the file — its relationship graph and (Story 7.4) the opt-in
        // git-signal panels — and the code view holds the source. A null insight => empty coverage => the page is
        // byte-identical to a run without --deep-git for that half.
        var coverage = insight is null ? "" : BuildCoverageSection(prefix, insight, coupledFileHref, commitHref);
        var insights = BuildInsightsPanel(prefix, repoRelativePath, referencedBy, coverage);

        if (insights.Length == 0)
        {
            // Nothing to say about the file (uncited, no external link, no deep-git insight) — no point in tabs; the
            // source spans the full width exactly as the pre-tab layout did for an uncited file.
            sb.Append(source).Append('\n');
            return EndShell(sb, prefix);
        }

        // Two pure-CSS tabbed views: insights lead (the reader asked to be led by what the code MEANS, not the code),
        // code second. A deep link to code/<path>.html#L42 still lands: a :target on a source line forces the code
        // view forward in CSS (see .code-tabs :target rules), so the locked #L{n} convention survives the tabs.
        AppendTabs(sb, outputRelativePath, insights, source);
        return EndShell(sb, prefix);
    }

    /// <summary>Builds the secondary "Source" panel: the file's contents as one contiguous
    /// <c>&lt;code class="language-*"&gt;</c> block so Prism can tokenize multi-line constructs (block comments,
    /// verbatim strings) correctly; the language class routes it to the right grammar (absent =&gt; Prism leaves it
    /// plain, the graceful path for unknown file types). Every line — including blanks — is one anchored
    /// <c>.code-line</c> span carrying the locked <c>id="L{n}"</c> anchor and a <c>data-ln</c> for the CSS gutter
    /// counter (deliberately NOT a text child, so the tokenized <c>textContent</c> stays pure source). The
    /// <c>data-code-path</c> hook lets a host re-target the file (VS Code recommendation R4.2).</summary>
    private static string BuildSource(string repoRelativePath, IReadOnlyList<string> lines, string? externalSourceUrl)
    {
        var count = lines.Count;
        var source = new StringBuilder();
        source.Append($"<section class=\"code-source-section\" data-code-path=\"{PathUtil.Html(PathUtil.NormalizeSlashes(repoRelativePath))}\">\n");
        // The additive "view online" link (Story 7.7) sits to the right of the heading — an inline jump-off with its
        // host mark, never a replacement for the in-portal source.
        var external = externalSourceUrl is { Length: > 0 } u ? "\n    " + ExternalSourceAnchor(u) : "";
        source.Append($"  <div class=\"code-source-head\">\n    <h2>Source</h2>{external}\n  </div>\n");
        var langClass = LanguageClass(repoRelativePath);
        source.Append(langClass is null ? "<pre class=\"code-file\"><code>" : $"<pre class=\"code-file\"><code class=\"{langClass}\">");
        for (var i = 0; i < count; i++)
        {
            var n = i + 1;
            var ns = n.ToString(CultureInfo.InvariantCulture);
            source.Append($"<span class=\"code-line\" id=\"L{ns}\" data-ln=\"{ns}\">{PathUtil.Html(lines[i])}</span>\n");
        }
        source.Append("</code></pre>\n</section>\n");
        return source.ToString();
    }

    /// <summary>Wraps the two views in a pure-CSS, no-JS tab shell ([[charting-is-pure-svg-no-js]]): a
    /// <c>&lt;fieldset&gt;</c> of two radio "tabs" (a visually-hidden legend names the choice for assistive tech) plus
    /// two sibling panels. The insights tab is <c>checked</c> so the page LEADS with what the file means; CSS
    /// <c>:has(:checked)</c> toggles the panels and <c>:target</c> forces the code panel forward for <c>#L{n}</c> deep
    /// links. The radio group name is per-page unique so several code pages consolidated into one document
    /// (SPA/webview capture) don't cross-wire their tabs.</summary>
    private static void AppendTabs(StringBuilder sb, string outputRelativePath, string insightsPanel, string sourcePanel)
    {
        var group = PathUtil.Html(TabGroupName(outputRelativePath));
        sb.Append("<div class=\"code-tabs\">\n");
        sb.Append("  <fieldset class=\"code-tablist\">\n");
        sb.Append("    <legend class=\"sr-only\">Choose a view for this file</legend>\n");
        sb.Append($"    <label class=\"code-tab code-tab--insights\"><input type=\"radio\" class=\"code-tab-input\" name=\"{group}\" checked><span>Insights</span></label>\n");
        sb.Append($"    <label class=\"code-tab code-tab--source\"><input type=\"radio\" class=\"code-tab-input\" name=\"{group}\"><span>Code</span></label>\n");
        sb.Append("  </fieldset>\n");
        sb.Append("  <div class=\"code-tabpanel code-tabpanel--insights\">\n");
        sb.Append(insightsPanel);
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"code-tabpanel code-tabpanel--source\">\n");
        sb.Append(sourcePanel);
        sb.Append("  </div>\n");
        sb.Append("</div>\n\n");
    }

    /// <summary>Assembles the insights view: the "Referenced by" relationship graph as a card, followed by the opt-in
    /// advanced-coverage panels (<paramref name="coverage"/>). Returns empty when there is nothing to show (uncited
    /// and no insight) so the caller drops the tabs entirely and renders the source full-width.</summary>
    private static string BuildInsightsPanel(
        string prefix, string repoRelativePath, IReadOnlyList<(string OutputUrl, string Title)>? referencedBy,
        string coverage)
    {
        var hasRefs = referencedBy is { Count: > 0 };
        if (!hasRefs && coverage.Length == 0) return "";

        var sb = new StringBuilder();
        if (hasRefs)
        {
            sb.Append("<div class=\"insight-panels\">\n");
            sb.Append(BuildRelationshipsCard(prefix, repoRelativePath, referencedBy!));
            sb.Append("</div>\n");
        }
        sb.Append(coverage);
        return sb.ToString();
    }

    /// <summary>Renders the opt-in "Advanced coverage" section (Story 7.4): the file's change frequency, file-scoped
    /// contributor attribution ("N commits" — never a ranking), the files it most often changes alongside (guarded
    /// links to their code pages), and a bounded newest-first change history (each row's hash a guarded link to its
    /// per-commit page). Neutral chart tokens, no JS, everything escaped (author names / subjects / paths / hashes
    /// are free-text injection surfaces). Empty sub-parts are omitted (no empty heading); a fully-empty insight
    /// renders nothing so a later refactor can't leak a hollow section.</summary>
    private static string BuildCoverageSection(
        string prefix, FileInsight insight, Func<string, string?>? coupledFileHref, Func<string, string?>? commitHref)
    {
        var hasContributors = insight.Contributors.Count > 0;
        var hasCoupled = insight.CoupledFiles.Count > 0;
        var hasHistory = insight.History.Count > 0;
        // A file with an insight but no change count and no sub-parts has nothing to say — omit the whole section.
        if (insight.ChangeCount == 0 && !hasContributors && !hasCoupled && !hasHistory)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("<section class=\"code-insights\" aria-labelledby=\"advanced-coverage\">\n");
        sb.Append("  <h2 id=\"advanced-coverage\">Advanced coverage</h2>\n");

        // Each git signal is its own bordered child panel, laid out in a responsive grid — no wrapper card, no
        // explanatory preamble; the panel headings carry the meaning.
        sb.Append("  <div class=\"insight-panels\">\n");

        // Change frequency — always shown when the section renders (the anchoring "how often" signal).
        sb.Append("    <section class=\"insight-panel code-insight-block\">\n");
        sb.Append("      <h3>Change frequency</h3>\n");
        sb.Append($"      <p class=\"code-insight-frequency\">Changed in <strong>{insight.ChangeCount.ToString(CultureInfo.InvariantCulture)}</strong> {Charts.Plural(insight.ChangeCount, "commit", "commits")} in the analyzed history.</p>\n");
        sb.Append("    </section>\n");

        if (hasContributors)
        {
            sb.Append("    <section class=\"insight-panel code-insight-block\">\n");
            sb.Append("      <h3>Contributors to this file</h3>\n");
            sb.Append("      <ul class=\"code-insight-contributors\">\n");
            foreach (var (author, commits) in insight.Contributors)
            {
                sb.Append(
                    $"        <li><span class=\"contributor-name\">{PathUtil.Html(author)}</span> " +
                    $"<span class=\"contributor-count\">{commits.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(commits, "commit", "commits")}</span></li>\n");
            }
            sb.Append("      </ul>\n");
            sb.Append("    </section>\n");
        }

        if (hasCoupled)
        {
            sb.Append("    <section class=\"insight-panel code-insight-block\">\n");
            sb.Append("      <h3>Often changed with</h3>\n");
            sb.Append("      <ul class=\"code-insight-coupled\">\n");
            foreach (var (path, coChanges) in insight.CoupledFiles)
            {
                var pathHtml = PathUtil.Html(PathUtil.NormalizeSlashes(path));
                var target = coupledFileHref?.Invoke(path);
                var nameCell = target is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(prefix + PathUtil.NormalizeSlashes(target))}\">{pathHtml}</a>"
                    : $"<code>{pathHtml}</code>";
                sb.Append(
                    $"        <li>{nameCell} <span class=\"coupled-count\">{coChanges.ToString(CultureInfo.InvariantCulture)}&times;</span></li>\n");
            }
            sb.Append("      </ul>\n");
            sb.Append("    </section>\n");
        }

        sb.Append("  </div>\n");

        if (hasHistory)
        {
            sb.Append("  <section class=\"insight-panel code-insight-history\">\n");
            sb.Append("    <h3>Change history</h3>\n");
            sb.Append("    <div class=\"table-scroll\">\n");
            sb.Append("    <table class=\"code-history-table\">\n");
            sb.Append("      <caption>Recent commits that changed this file, newest first.</caption>\n");
            sb.Append("      <thead>\n        <tr>\n");
            sb.Append("          <th scope=\"col\">Date</th>\n");
            sb.Append("          <th scope=\"col\">Commit</th>\n");
            sb.Append("          <th scope=\"col\">Author</th>\n");
            sb.Append("          <th scope=\"col\">Summary</th>\n");
            sb.Append("        </tr>\n      </thead>\n      <tbody>\n");
            foreach (var touch in insight.History)
            {
                var hashHtml = PathUtil.Html(touch.ShortHash);
                var target = commitHref?.Invoke(touch.ShortHash);
                var hashCell = target is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(prefix + PathUtil.NormalizeSlashes(target))}\"><code>{hashHtml}</code></a>"
                    : $"<code>{hashHtml}</code>";
                var dateCell = touch.Date is { } d ? PathUtil.Html(Charts.D(d)) : "&mdash;";
                var subject = touch.Subject.Length == 0 ? "(no subject)" : touch.Subject;
                sb.Append("        <tr>\n");
                sb.Append($"          <td class=\"code-history-date\">{dateCell}</td>\n");
                sb.Append($"          <td class=\"code-history-hash\">{hashCell}</td>\n");
                sb.Append($"          <td class=\"code-history-author\">{PathUtil.Html(touch.Author)}</td>\n");
                sb.Append($"          <td class=\"code-history-subject\">{PathUtil.Html(subject)}</td>\n");
                sb.Append("        </tr>\n");
            }
            sb.Append("      </tbody>\n    </table>\n");
            sb.Append("    </div>\n");
            sb.Append("  </section>\n");
        }

        sb.Append("</section>\n");
        return sb.ToString();
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

    /// <summary>Builds the "Referenced by" relationship card for the insights tab: the pure-SVG node-link graph of the
    /// artifacts that cite this file (Story 7.1 rework), plus a visually-hidden but present <c>&lt;ul&gt;</c> that
    /// mirrors the graph's links for assistive tech (the <c>&lt;svg role="img"&gt;</c> exposes only its summary label,
    /// so this is the accessible, keyboard-reachable equivalent — meaningful link text, never "click here",
    /// NFR6/UX-DR16). Same markup the sidebar used, now a card among the insight panels.</summary>
    private static string BuildRelationshipsCard(
        string prefix, string repoRelativePath, IReadOnlyList<(string OutputUrl, string Title)> referencedBy)
    {
        // Resolve each citing artifact once to (href, full title, compact label) — shared by the graph and list.
        var nodes = new List<(string Href, string Title, string Short)>(referencedBy.Count);
        foreach (var (outputUrl, title) in referencedBy)
        {
            nodes.Add((prefix + PathUtil.NormalizeSlashes(outputUrl), title, ShortLabel(title)));
        }

        var sb = new StringBuilder();
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
        return sb.ToString();
    }

    /// <summary>A per-page-unique radio-group name for the view tabs, derived from the page's output-relative path
    /// (non-alphanumeric runs collapse to a single hyphen). Uniqueness matters when several code pages are captured
    /// into one document (SPA/webview consolidation): a shared name would make their radio groups mutually exclusive
    /// and cross-wire the tabs.</summary>
    private static string TabGroupName(string outputRelativePath)
    {
        var sb = new StringBuilder("code-view-");
        var prevHyphen = false;
        foreach (var c in outputRelativePath)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                prevHyphen = false;
            }
            else if (!prevHyphen)
            {
                sb.Append('-');
                prevHyphen = true;
            }
        }
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
    /// it — mirroring the synthesized-page shape of <see cref="CommitDayTemplater"/>. <paramref name="highlight"/>
    /// adds the vendored Prism stylesheet + highlighter to the head (only the full page, which actually renders a
    /// <c>&lt;code&gt;</c> block, asks for them).</summary>
    private static StringBuilder BeginShell(string repoRelativePath, string outputRelativePath, string prefix, SiteNav nav, bool highlight = false)
    {
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{repoRelativePath} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Source file {repoRelativePath} in {nav.SiteTitle}.",
            highlight ? HighlightHead(prefix) : null));
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

    /// <summary>The extra head tags a highlighted code page needs: the vendored Prism theme stylesheet and the
    /// highlighter script (both build-versioned like every other asset). The script auto-highlights every
    /// <c>&lt;code class="language-*"&gt;</c> on load and its bundled keep-markup plugin preserves our per-line
    /// anchors. <c>defer</c> keeps it off the critical path; with JS off the page is still legible monospace.</summary>
    private static string HighlightHead(string prefix)
    {
        var v = PathUtil.CurrentAssetVersion;
        return $"<link rel=\"stylesheet\" href=\"{PathUtil.Html(prefix + ForgeOptions.CodeHighlightStyleName)}?v={v}\">\n" +
               $"<script src=\"{PathUtil.Html(prefix + ForgeOptions.CodeHighlightScriptName)}?v={v}\" defer></script>\n";
    }

    /// <summary>Maps a repo-relative source path to its Prism grammar class (<c>language-&#42;</c>) by file
    /// extension (and a couple of well-known extensionless names). Returns <c>null</c> for anything not in the
    /// vendored bundle so the page renders as plain, un-tokenized monospace — the deliberate graceful fallback for
    /// unknown file types rather than a wrong-grammar mangling.</summary>
    private static string? LanguageClass(string repoRelativePath)
    {
        var norm = PathUtil.NormalizeSlashes(repoRelativePath);
        var name = norm[(norm.LastIndexOf('/') + 1)..];

        // A few source files are identified by name, not extension.
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase))
        {
            return "language-docker";
        }

        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return null;
        }

        var grammar = name[(dot + 1)..].ToLowerInvariant() switch
        {
            "cs" => "csharp",
            "ts" => "typescript",
            "tsx" => "tsx",
            "js" or "mjs" or "cjs" => "javascript",
            "jsx" => "jsx",
            "json" => "json",
            "json5" => "json5",
            "yml" or "yaml" => "yaml",
            "toml" => "toml",
            "ini" or "cfg" or "editorconfig" => "ini",
            "sh" or "bash" or "zsh" => "bash",
            "ps1" or "psm1" or "psd1" => "powershell",
            "py" or "pyi" => "python",
            "sql" => "sql",
            "md" or "markdown" => "markdown",
            "rs" => "rust",
            "go" => "go",
            "java" => "java",
            "kt" or "kts" => "kotlin",
            "swift" => "swift",
            "rb" => "ruby",
            "php" => "php",
            "c" or "h" => "c",
            "cpp" or "cc" or "cxx" or "hpp" or "hxx" => "cpp",
            "css" => "css",
            "graphql" or "gql" => "graphql",
            "diff" or "patch" => "diff",
            "html" or "htm" or "xml" or "svg" or "xaml" or "axaml" or "csproj" or "props"
                or "targets" or "slnx" or "vbproj" or "fsproj" or "plist" or "resx" => "markup",
            _ => null,
        };

        return grammar is null ? null : "language-" + grammar;
    }
}
