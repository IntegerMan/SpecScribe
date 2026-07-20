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
    /// plain <c>&lt;code&gt;</c>), and <paramref name="dayHref"/> resolves a history entry's date to its
    /// <c>commits/{date}.html</c> page (null → plain text); all three return output-relative paths that this method
    /// prefixes.</para></summary>
    public static string RenderPage(
        string repoRelativePath,
        string outputRelativePath,
        IReadOnlyList<string> lines,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy = null,
        string? externalSourceUrl = null,
        FileInsight? insight = null,
        Func<string, string?>? coupledFileHref = null,
        Func<string, string?>? commitHref = null,
        Func<DateOnly, string?>? dayHref = null,
        EntityPager? pager = null,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges = null,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges = null,
        NavLocalContext? localContext = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav, highlight: true, pager: pager, localContext: localContext);

        var count = lines.Count;
        sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "line" : "lines")}</span></div>\n");
        sb.Append("</header>\n\n");

        // The "view online" jump-off rides ALONGSIDE the source (next to the "Source" heading) since it points at the
        // code, not at the insights — so it survives even when there is no insights tab.
        var source = BuildSource(repoRelativePath, lines, externalSourceUrl);

        // Four independent views, each holding one facet of the file. A null insight => empty insights/history and
        // (when uncited) empty relationships => the page is byte-identical to a run without --deep-git for that facet.
        //   Insights      — the git-signal coverage: change frequency and contributors.
        //   Relationships — the reference graph: what cites the file (solid) plus the files it co-changes with (dashed, Story 7.8).
        //   History       — the bounded change-history table.
        //   Code          — the source itself (always present).
        var insightsPanel = BuildInsightsPanel(insight);
        var relationshipsPanel = BuildRelationshipsPanel(
            prefix, repoRelativePath, outputRelativePath, referencedBy, insight, coupledFileHref, storyRelatedEdges, relatedRelatedEdges);
        var historyPanel = BuildHistoryPanel(prefix, insight, commitHref, dayHref);

        // Assemble in a fixed order (Insights → Relationships → History → Code); empty panels drop out so a file only
        // ever shows tabs it can back with content. The first surviving tab is the default-checked one.
        var tabs = new List<CodeTab>(4);
        if (insightsPanel.Length > 0) tabs.Add(new CodeTab("insights", "Insights", insightsPanel));
        if (relationshipsPanel.Length > 0) tabs.Add(new CodeTab("relationships", "Relationships", relationshipsPanel));
        if (historyPanel.Length > 0) tabs.Add(new CodeTab("history", "History", historyPanel));
        tabs.Add(new CodeTab("source", "Code", source));

        if (tabs.Count == 1)
        {
            // Nothing to say about the file (uncited, no external link, no deep-git insight) — no point in tabs; the
            // source spans the full width exactly as the pre-tab layout did for an uncited file.
            sb.Append(source).Append('\n');
            return EndShell(sb, prefix);
        }

        // A deep link to code/<path>.html#L42 still lands: a :target on a source line forces the code view forward in
        // CSS (see .code-tabs :target rules), so the locked #L{n} convention survives regardless of the default tab.
        AppendTabs(sb, outputRelativePath, tabs);
        return EndShell(sb, prefix);
    }

    /// <summary>One tab in the code page's pure-CSS tab strip: a css modifier (<c>insights</c>/<c>relationships</c>/
    /// <c>history</c>/<c>source</c>) shared between its <c>.code-tab--{Mod}</c> radio label and its
    /// <c>.code-tabpanel--{Mod}</c> panel, a visible <c>Label</c> (also the <see cref="Icons.ForCodeTab"/> key), and
    /// the pre-rendered panel HTML. <c>source</c> is kept as the Code tab's modifier so the locked <c>#L{n}</c>
    /// deep-link CSS keys (<c>.code-tabpanel--source</c>) still resolve.</summary>
    private readonly record struct CodeTab(string Mod, string Label, string Panel);

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

    /// <summary>Wraps the surviving views in a pure-CSS, no-JS tab shell ([[charting-is-pure-svg-no-js]]): a
    /// <c>&lt;fieldset&gt;</c> of radio "tabs" (a visually-hidden legend names the choice for assistive tech) plus one
    /// sibling panel each. Every tab carries a decorative <see cref="Icons.ForCodeTab"/> glyph before its text label.
    /// The first tab is <c>checked</c> so the page LEADS with the first surviving view (Insights when present); CSS
    /// <c>:has(:checked)</c> toggles the panels and <c>:target</c> forces the Code panel forward for <c>#L{n}</c> deep
    /// links. The radio group name is per-page unique so several code pages consolidated into one document
    /// (SPA/webview capture) don't cross-wire their tabs.</summary>
    private static void AppendTabs(StringBuilder sb, string outputRelativePath, IReadOnlyList<CodeTab> tabs)
    {
        var group = PathUtil.Html(TabGroupName(outputRelativePath));
        sb.Append("<div class=\"code-tabs\">\n");
        sb.Append("  <fieldset class=\"code-tablist\">\n");
        sb.Append("    <legend class=\"sr-only\">Choose a view for this file</legend>\n");
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var check = i == 0 ? " checked" : "";
            sb.Append(
                $"    <label class=\"code-tab code-tab--{tab.Mod}\"><input type=\"radio\" class=\"code-tab-input\" name=\"{group}\"{check}>" +
                $"{Icons.ForCodeTab(tab.Label)}<span>{tab.Label}</span></label>\n");
        }
        sb.Append("  </fieldset>\n");
        foreach (var tab in tabs)
        {
            sb.Append($"  <div class=\"code-tabpanel code-tabpanel--{tab.Mod}\">\n");
            sb.Append(tab.Panel);
            sb.Append("  </div>\n");
        }
        sb.Append("</div>\n\n");
    }

    /// <summary>Builds the <em>Insights</em> panel: the opt-in "Advanced coverage" section (Story 7.4) — the file's
    /// change frequency and file-scoped contributor attribution ("N commits" — never a ranking). The files it most
    /// often changes alongside are NOT listed here: as of Story 7.8 (AC #2) they live as related-file nodes on the
    /// reference graph (the Relationships tab, the single relationship surface), so a visible list here would duplicate
    /// them. The reference graph and the change-history table own the Relationships and History tabs. Returns empty
    /// when the insight is null or carries no frequency/contributor data, so the caller drops the tab entirely; a null
    /// insight leaves the page byte-identical to a run without --deep-git.</summary>
    private static string BuildInsightsPanel(FileInsight? insight)
    {
        if (insight is null) return "";

        var hasContributors = insight.Contributors.Count > 0;
        // Coupling now lives on the relationship graph (Story 7.8), not here, so it no longer keeps this panel alive:
        // a file with an insight but no change count and no contributors has nothing to say.
        if (insight.ChangeCount == 0 && !hasContributors)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("<section class=\"code-insights\" aria-labelledby=\"advanced-coverage\">\n");
        sb.Append("  <h2 id=\"advanced-coverage\">Advanced coverage</h2>\n");

        // Each git signal is its own bordered child panel, laid out in a responsive grid — no explanatory preamble;
        // the panel headings carry the meaning.
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
            // Disclose truncation rather than let a capped top-N list read as the complete contributor set.
            var moreContributors = insight.TotalContributors - insight.Contributors.Count;
            if (moreContributors > 0)
            {
                sb.Append($"      <p class=\"code-insight-more\">+{moreContributors.ToString(CultureInfo.InvariantCulture)} more {Charts.Plural(moreContributors, "contributor", "contributors")}</p>\n");
            }
            sb.Append("    </section>\n");
        }

        sb.Append("  </div>\n");
        sb.Append("</section>\n");
        return sb.ToString();
    }

    /// <summary>Builds the <em>Relationships</em> panel: the "Referenced by" reference graph as its single card. The
    /// graph carries two node populations (Story 7.8): the citing artifacts (always, as solid-spoke gold circles) and
    /// — when the <paramref name="insight"/> carries coupled files — the files this file most often changes alongside
    /// (dashed-spoke neutral diamonds, resolved to their code pages via <paramref name="coupledFileHref"/>). The graph
    /// is now the single relationship surface (AC #2), so the old visible "Often changed with" list is gone; its text
    /// equivalent lives in the card's sr-only list. Returns empty when there is neither a citation nor a related file,
    /// so the caller drops the tab.</summary>
    private static string BuildRelationshipsPanel(
        string prefix, string repoRelativePath, string outputRelativePath,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy,
        FileInsight? insight, Func<string, string?>? coupledFileHref,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges)
    {
        var hasRefs = referencedBy is { Count: > 0 };
        var related = BuildRelatedNodes(prefix, insight, coupledFileHref);
        if (!hasRefs && related.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append("<div class=\"insight-panels\">\n");
        sb.Append(BuildRelationshipsCard(
            prefix, repoRelativePath, outputRelativePath,
            referencedBy ?? Array.Empty<(string, string, (int, string)?)>(), related,
            storyRelatedEdges, relatedRelatedEdges));
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>Maps the file's coupled-file list (Story 7.4's <see cref="FileInsight.CoupledFiles"/> — already
    /// sorted, capped, and <c>--deep-git</c>-gated upstream) to related-file graph/list nodes (Story 7.8). Each entry
    /// becomes <c>(Href?, fullPath, basename, coChanges)</c>: <c>Href</c> is the coupled file's <c>code/…html</c> page
    /// resolved via <paramref name="coupledFileHref"/> and prefixed for this page — non-null only when that file has an
    /// in-portal page (it too is cited), so an uncited coupled file becomes a non-link chip, never a dead link. Full
    /// path rides the tooltip/list text; the basename is the on-graph label. A null insight or empty coupling yields an
    /// empty list, so the graph stays citations-only (byte-identical to a run without deep-git).</summary>
    private static IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)> BuildRelatedNodes(
        string prefix, FileInsight? insight, Func<string, string?>? coupledFileHref)
    {
        if (insight is null || insight.CoupledFiles.Count == 0)
        {
            return Array.Empty<(string?, string, string, int)>();
        }

        var list = new List<(string? Href, string Title, string Short, int CoChanges)>(insight.CoupledFiles.Count);
        foreach (var (path, coChanges) in insight.CoupledFiles)
        {
            var norm = PathUtil.NormalizeSlashes(path);
            var target = coupledFileHref?.Invoke(path);
            var href = target is { Length: > 0 } ? prefix + PathUtil.NormalizeSlashes(target) : null;
            list.Add((href, norm, BaseName(path), coChanges));
        }
        return list;
    }

    /// <summary>Builds the <em>History</em> panel: the bounded, newest-first change-history table (Story 7.4) — each
    /// row's hash a guarded link to its per-commit page (null → plain <c>&lt;code&gt;</c>), and its date a guarded
    /// link to that day's <c>commits/{date}.html</c> page (null → plain text). Everything escaped (author names /
    /// subjects / hashes are free-text injection surfaces). Returns empty when the insight is null or carries no
    /// history, so the caller drops the tab.
    /// <para><b>Story 10.8 scope:</b> like <see cref="CodeMapTemplater"/>'s file table, stays a genuine
    /// <c>&lt;table&gt;</c> (Design Direction #5) — its Date/Commit/Author/Summary header row is load-bearing, and
    /// commits carry no lifecycle status, so there is no badge to route through the shared row primitive.</para></summary>
    private static string BuildHistoryPanel(
        string prefix, FileInsight? insight, Func<string, string?>? commitHref, Func<DateOnly, string?>? dayHref)
    {
        if (insight is null || insight.History.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append("<section class=\"insight-panel code-insight-history\">\n");
        sb.Append("  <h2>Change history</h2>\n");
        sb.Append("  <div class=\"table-scroll\">\n");
        sb.Append("  <table class=\"code-history-table\">\n");
        sb.Append("    <caption>Recent commits that changed this file, newest first.</caption>\n");
        sb.Append("    <thead>\n      <tr>\n");
        sb.Append("        <th scope=\"col\">Date</th>\n");
        sb.Append("        <th scope=\"col\">Commit</th>\n");
        sb.Append("        <th scope=\"col\">Author</th>\n");
        sb.Append("        <th scope=\"col\">Summary</th>\n");
        sb.Append("      </tr>\n    </thead>\n    <tbody>\n");
        foreach (var touch in insight.History)
        {
            var hashHtml = PathUtil.Html(touch.ShortHash);
            var target = commitHref?.Invoke(touch.ShortHash);
            var hashCell = target is { Length: > 0 }
                ? $"<a href=\"{PathUtil.Html(prefix + PathUtil.NormalizeSlashes(target))}\"><code>{hashHtml}</code></a>"
                : $"<code>{hashHtml}</code>";
            var dateCell = "&mdash;";
            if (touch.Date is { } d)
            {
                var dateText = PathUtil.Html(Charts.D(d));
                var dateTarget = dayHref?.Invoke(d);
                dateCell = dateTarget is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(prefix + PathUtil.NormalizeSlashes(dateTarget))}\">{dateText}</a>"
                    : dateText;
            }
            var subject = touch.Subject.Length == 0 ? "(no subject)" : touch.Subject;
            sb.Append("      <tr>\n");
            sb.Append($"        <td class=\"code-history-date\">{dateCell}</td>\n");
            sb.Append($"        <td class=\"code-history-hash\">{hashCell}</td>\n");
            sb.Append($"        <td class=\"code-history-author\">{PathUtil.Html(touch.Author)}</td>\n");
            sb.Append($"        <td class=\"code-history-subject\">{PathUtil.Html(subject)}</td>\n");
            sb.Append("      </tr>\n");
        }
        sb.Append("    </tbody>\n  </table>\n");
        sb.Append("  </div>\n");
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
        string prefix, string repoRelativePath,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy, string? externalSourceUrl)
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
            foreach (var (outputUrl, title, _) in referencedBy)
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

    /// <summary>The four precomputed reference-graph toggle combinations (mirrors <see cref="CodeMap.BuildVariants"/>
    /// / <see cref="CodeMapTemplater"/>'s pure-CSS multi-checkbox idiom), keyed exactly like
    /// <c>data-view="flat-flat|epic-flat|flat-rel|epic-rel"</c> per the Design Notes: first flag = "Group by epic",
    /// second = "Show relationships".</summary>
    private static readonly (string Key, bool Epic, bool Rel)[] RefGraphVariants =
    {
        ("flat-flat", false, false),
        ("epic-flat", true, false),
        ("flat-rel", false, true),
        ("epic-rel", true, true),
    };

    /// <summary>Builds the <em>Referenced by</em> relationship card: the pure-SVG reference graph plus two
    /// independent, pure-CSS opt-in toggles — <b>"Group by epic"</b> (nests citing-story nodes under a parent epic
    /// hub instead of a flat ring; non-story citers stay at the top level, unaffected) and <b>"Show relationships"</b>
    /// (draws extra neutral edges: story&#8596;related-file when that story also cites the related file, and
    /// related-file&#8596;related-file when that pair is itself frequently co-changed). All four combinations are
    /// pre-rendered server-side (mirroring <see cref="CodeMap.BuildVariants"/>) into sibling <c>.ref-graph-view</c>
    /// panels switched by two checkboxes via CSS <c>~</c> sibling-combinator selectors keyed on CLASS (not id — a
    /// page-unique id still backs each checkbox's <c>for</c>/<c>id</c> pair for correct label semantics when several
    /// code pages are consolidated into one document, but the show/hide selectors themselves only need to see the
    /// SAME two checkbox classes repeat per <c>&lt;section&gt;</c>, so they need no per-page duplication in
    /// specscribe.css). When both toggles are off (the default, unchecked state) the visible "flat-flat" panel is
    /// produced by calling <see cref="Charts.ReferenceGraph"/> with no epic/edge data at all — BYTE-IDENTICAL to the
    /// pre-existing Story 7.8 call. <paramref name="storyRelatedEdges"/>/<paramref name="relatedRelatedEdges"/> being
    /// null/empty (no <c>--deep-git</c>, or nothing to relate) means all four variants render identically — the
    /// checkboxes still appear (so the control surface never disappears) but toggling them is a visual no-op, the
    /// graceful-degradation path. The sr-only list is toggle-agnostic: it always enumerates epic membership and
    /// cross-edges (when present) regardless of which panel is currently visible, so assistive tech never has less
    /// information than the richest sighted view.</summary>
    private static string BuildRelationshipsCard(
        string prefix, string repoRelativePath, string outputRelativePath,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)> referencedBy,
        IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)> related,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges)
    {
        // Resolve each citing artifact once to (href, full title, compact label, epic) — shared by the graph and list.
        var nodes = new List<(string Href, string Title, string Short)>(referencedBy.Count);
        var refEpics = new List<(int EpicNumber, string EpicTitle)?>(referencedBy.Count);
        foreach (var (outputUrl, title, epic) in referencedBy)
        {
            nodes.Add((prefix + PathUtil.NormalizeSlashes(outputUrl), title, ShortLabel(title)));
            refEpics.Add(epic is { } e ? (e.Number, e.Title) : null);
        }

        var hasRelated = related.Count > 0;
        // The note gains the co-change population's framing ONLY when related files are present, so a baseline
        // (no-deep-git) card stays byte-identical to Story 7.1 and never promises a dashed edge the graph won't draw.
        var note = hasRelated
            ? "The artifacts that cite this file (solid) and the files it most often changes alongside (dashed). These are citations and co-changes over time &#8212; not code call/dependency edges."
            : "The artifacts that cite this file. References run artifact&#8594;file, so this shows what refers to the file — not code dependencies.";

        var sb = new StringBuilder();
        sb.Append("<section class=\"code-relationships\">\n");
        sb.Append("  <h2>Referenced by</h2>\n");
        sb.Append($"  <p class=\"code-relationships-note\">{note}</p>\n");

        // Two independent pure-CSS toggles, always rendered once the card itself renders (even with no epic/edge
        // data — AC "both checkboxes present ... no exception"). Page-unique ids only for the <label for>/<input id>
        // pair's correctness under document consolidation (mirrors TabGroupName); the CSS toggle logic itself keys
        // off the checkbox CLASSES, which are the same on every code page.
        var group = RefGraphGroupSlug(outputRelativePath);
        sb.Append($"  <input type=\"checkbox\" id=\"refgraph-epic-{group}\" class=\"refgraph-toggle refgraph-toggle-epic\">");
        sb.Append($"<label for=\"refgraph-epic-{group}\" class=\"refgraph-toggle-label\">Group by epic</label>\n");
        sb.Append($"  <input type=\"checkbox\" id=\"refgraph-rel-{group}\" class=\"refgraph-toggle refgraph-toggle-rel\">");
        sb.Append($"<label for=\"refgraph-rel-{group}\" class=\"refgraph-toggle-label\">Show relationships</label>\n");

        foreach (var (key, epicOn, relOn) in RefGraphVariants)
        {
            sb.Append($"  <div class=\"ref-graph-wrap ref-graph-view\" data-view=\"{key}\">\n");
            sb.Append(Charts.ReferenceGraph(
                BaseName(repoRelativePath), nodes, 0, related,
                refEpics: epicOn ? refEpics : null,
                groupByEpic: epicOn,
                crossEdges: relOn ? storyRelatedEdges : null,
                relatedEdges: relOn ? relatedRelatedEdges : null));
            sb.Append("  </div>\n");
        }

        sb.Append("  <ul class=\"ref-list sr-only\">\n");
        for (var i = 0; i < nodes.Count; i++)
        {
            var (href, title, _) = nodes[i];
            var epicSuffix = refEpics[i] is { } epic ? $" (Epic {epic.EpicNumber}: {PathUtil.Html(epic.EpicTitle)})" : "";
            var crossSuffix = BuildStoryCrossSuffix(i, storyRelatedEdges, related);
            sb.Append($"    <li><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(title)}</a>{epicSuffix}{crossSuffix}</li>\n");
        }
        if (hasRelated)
        {
            // The accessible text equivalent of the related-file nodes (AC #2's second half): a labelled sub-list of
            // path + co-change strength, linked to the coupled file's code page when it has one, plain text otherwise.
            // Also enumerates any "Show relationships" cross edges touching each related file, so the sr-only text
            // stays complete regardless of which toggle combination happens to be visible.
            sb.Append("    <li class=\"ref-list-related\">Files changed alongside this one:\n");
            sb.Append("      <ul>\n");
            for (var j = 0; j < related.Count; j++)
            {
                var (href, title, _, coChanges) = related[j];
                var pathHtml = PathUtil.Html(title);
                var nameCell = href is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(href)}\">{pathHtml}</a>"
                    : pathHtml;
                var relatedCrossSuffix = BuildRelatedCrossSuffix(j, storyRelatedEdges, relatedRelatedEdges, nodes, related);
                sb.Append($"        <li>{nameCell} &#8212; changed together {coChanges.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(coChanges, "time", "times")}{relatedCrossSuffix}</li>\n");
            }
            sb.Append("      </ul>\n");
            sb.Append("    </li>\n");
        }
        sb.Append("  </ul>\n");
        sb.Append("</section>\n");
        return sb.ToString();
    }

    /// <summary>The sr-only suffix on a citing-artifact's list item naming any related file it ALSO cites (the
    /// "Show relationships" story&#8596;related-file edge's text equivalent).</summary>
    private static string BuildStoryCrossSuffix(
        int refIndex, IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)> related)
    {
        if (storyRelatedEdges is not { Count: > 0 }) return "";
        var names = storyRelatedEdges
            .Where(e => e.RefIndex == refIndex && e.RelatedIndex >= 0 && e.RelatedIndex < related.Count)
            .Select(e => related[e.RelatedIndex].Title)
            .ToList();
        if (names.Count == 0) return "";
        return $" &#8212; also cites {string.Join("; ", names.Select(PathUtil.Html))}";
    }

    /// <summary>The sr-only suffix on a related-file's list item naming any citing story that also cites it, and
    /// any OTHER related file it is itself frequently co-changed with (the "Show relationships" edges' text
    /// equivalent for the related-file population).</summary>
    private static string BuildRelatedCrossSuffix(
        int relatedIndex,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges,
        IReadOnlyList<(string Href, string Title, string Short)> nodes,
        IReadOnlyList<(string? Href, string Title, string Short, int CoChanges)> related)
    {
        var parts = new List<string>();
        if (storyRelatedEdges is { Count: > 0 })
        {
            var citerNames = storyRelatedEdges
                .Where(e => e.RelatedIndex == relatedIndex && e.RefIndex >= 0 && e.RefIndex < nodes.Count)
                .Select(e => nodes[e.RefIndex].Title)
                .ToList();
            if (citerNames.Count > 0) parts.Add($"also cited by {string.Join("; ", citerNames.Select(PathUtil.Html))}");
        }
        if (relatedRelatedEdges is { Count: > 0 })
        {
            var otherNames = relatedRelatedEdges
                .Where(e => e.RelatedIndexA != e.RelatedIndexB && (e.RelatedIndexA == relatedIndex || e.RelatedIndexB == relatedIndex))
                .Select(e => e.RelatedIndexA == relatedIndex ? e.RelatedIndexB : e.RelatedIndexA)
                .Where(idx => idx >= 0 && idx < related.Count)
                .Select(idx => related[idx].Title)
                .ToList();
            if (otherNames.Count > 0) parts.Add($"also co-changed with {string.Join("; ", otherNames.Select(PathUtil.Html))}");
        }
        return parts.Count == 0 ? "" : $" &#8212; {string.Join("; ", parts)}";
    }

    /// <summary>A per-page-unique slug for the reference graph's two toggle-checkbox ids — so several code pages
    /// consolidated into one document (SPA/webview capture) never cross-wire their <c>label for</c>/<c>input id</c>
    /// pairs. Built from the same <see cref="Slugify"/> helper <see cref="TabGroupName"/> uses (independently, not by
    /// slicing its output), so the two stay correct even if one's prefix ever changes. The show/hide CSS itself does
    /// not depend on this slug (it matches the shared checkbox classes instead), so this exists purely for correct
    /// label semantics, not for the toggle mechanism.</summary>
    private static string RefGraphGroupSlug(string outputRelativePath) => Slugify(outputRelativePath);

    /// <summary>A per-page-unique radio-group name for the view tabs, derived from the page's output-relative path.
    /// Uniqueness matters when several code pages are captured into one document (SPA/webview consolidation): a
    /// shared name would make their radio groups mutually exclusive and cross-wire the tabs.</summary>
    private static string TabGroupName(string outputRelativePath) => "code-view-" + Slugify(outputRelativePath);

    /// <summary>Collapses non-alphanumeric runs in a path to a single hyphen and lowercases it — the shared slug
    /// primitive behind both <see cref="TabGroupName"/> and <see cref="RefGraphGroupSlug"/>.</summary>
    private static string Slugify(string outputRelativePath)
    {
        var sb = new StringBuilder();
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
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy = null,
        string? externalSourceUrl = null,
        EntityPager? pager = null,
        NavLocalContext? localContext = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = BeginShell(repoRelativePath, outputRelativePath, prefix, nav, pager: pager, localContext: localContext);

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
    private static StringBuilder BeginShell(string repoRelativePath, string outputRelativePath, string prefix, SiteNav nav, bool highlight = false, EntityPager? pager = null, NavLocalContext? localContext = null)
    {
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{repoRelativePath} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Source file {repoRelativePath} in {nav.SiteTitle}.",
            highlight ? HighlightHead(prefix) : null));
        sb.Append(nav.RenderNavBar(outputRelativePath, localContext));
        // Sibling pager (prev/next across sibling files, alphabetical) rides the coherent wayfinding strip
        // alongside the breadcrumb now, not the body's own header. [Story 10.11]
        sb.Append(SiteNav.RenderWayfinding(outputRelativePath, new (string, string?)[]
        {
            ("Home", "index.html"),
            (repoRelativePath, null),
        }, pager));

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
            // "swift" intentionally NOT mapped here even though the vendored bundle now carries the Swift grammar
            // (tools/prism-vendor/build.js's WANT list requests it) — wiring it up is a separate decision; for now
            // ".swift" falls through to plain monospace via the null return below.
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
