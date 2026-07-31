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
        NavLocalContext? localContext = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(
            repoRelativePath, outputRelativePath, lines, nav, referencedBy, externalSourceUrl, insight,
            coupledFileHref, commitHref, dayHref, pager, storyRelatedEdges, relatedRelatedEdges, localContext)).Content;

    /// <summary>Builds a code page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract, so the IR's
    /// content region can be COMPOSED (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup + wayfinding +
    /// body) instead of sliced back out of a rendered full page. <see cref="RenderPage"/> is the unchanged HTML
    /// projection of this same model, so the bytes are identical. [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(
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
        var shell = BeginShell(repoRelativePath, outputRelativePath, prefix, nav, highlight: true, pager: pager, localContext: localContext);
        var sb = shell.Body;

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
            return EndShell(shell);
        }

        // A deep link to code/<path>.html#L42 still lands: a :target on a source line forces the code view forward in
        // CSS (see .code-tabs :target rules), so the locked #L{n} convention survives regardless of the default tab.
        AppendTabs(sb, outputRelativePath, tabs);
        return EndShell(shell);
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
        // ⚠ THE ZERO-WIDTH MOUNT TRAP (Story 24.2). These tabs are pure-CSS radios, so whenever an Insights panel
        // exists it is the default-checked tab and the Relationships panel is `display:none` — zero width — at the
        // moment the client first reaches the graph host. Plotly CANNOT lay out in a zero-width container and it
        // does NOT complain: it draws a chart of the wrong size. Marking the radios opts them into the same
        // deferred-mount/flush handshake `data-hierarchy-reveal` already implements, so the first mount happens on
        // the reveal instead of never. Emitted only when this page actually hosts a graph, so a page without one
        // keeps byte-identical tab markup.
        var reveal = tabs.Any(t => RelationshipGraph.ContainsHost(t.Panel))
            ? " " + RelationshipGraph.RevealMarker
            : "";
        sb.Append("<div class=\"code-tabs\">\n");
        sb.Append("  <fieldset class=\"code-tablist\">\n");
        sb.Append("    <legend class=\"sr-only\">Choose a view for this file</legend>\n");
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var check = i == 0 ? " checked" : "";
            sb.Append(
                // `checked` stays LAST so the structural "exactly one radio is checked" assertions can keep keying
                // on ` checked>` rather than on a looser match that a file path could satisfy by accident.
                $"    <label class=\"code-tab code-tab--{tab.Mod}\"><input type=\"radio\" class=\"code-tab-input\" name=\"{group}\"{reveal}{check}>" +
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
    /// confidence-sorted, support-floored, capped, and <c>--deep-git</c>-gated upstream) to related-file graph/list
    /// nodes (Story 7.8). Each entry becomes a <see cref="RelatedNode"/> carrying the link/label triple plus Story
    /// 24.1's directional metrics: <c>Href</c> is the coupled file's <c>code/…html</c> page
    /// resolved via <paramref name="coupledFileHref"/> and prefixed for this page — non-null only when that file has an
    /// in-portal page (it too is cited), so an uncited coupled file becomes a non-link chip, never a dead link. Full
    /// path rides the tooltip/list text; the basename is the on-graph label. A null insight or empty coupling yields an
    /// empty list, so the graph stays citations-only (byte-identical to a run without deep-git).</summary>
    /// <summary>One resolved related-file node: the link/label triple the graph draws, plus the Story 24.1
    /// directional metrics the sr-only text twin reports.
    ///
    /// <para><b>Story 24.2 owns this record and the former <c>ToGraphNodes</c>, by explicit handoff.</b> Both sat in
    /// Story 24.1's File List, but 24.1's own doc comment attributed them forward to the graph story; the handoff is
    /// recorded here and in 24.2's story record so the two reviews cannot each treat them as the other's
    /// (CLAUDE.md § Scoping a code review). <c>ToGraphNodes</c> existed only to project this record down to the
    /// 4-tuple the retired <c>Charts.ReferenceGraph</c> consumed, so it went with the SVG — which also resolved the
    /// <c>external_roslyn:CA1859</c> Sonar finding against it.</para>
    ///
    /// <para><paramref name="ProcessCoupling"/> is new in 24.2 and is a TWIN-COMPLETENESS fix, not a graph feature:
    /// the graph draws process coupling as a dotted spoke, and ADR 0013 §2 forbids a fact existing only inside the
    /// chart — so the twin has to be able to say it in words. Surfaced by this story's Task 6 audit.</para></summary>
    private sealed record RelatedNode(
        string? Href, string Title, string Short, int Support, double Confidence, double? Lift, bool CrossBoundary,
        bool ProcessCoupling);

    private static IReadOnlyList<RelatedNode> BuildRelatedNodes(
        string prefix, FileInsight? insight, Func<string, string?>? coupledFileHref)
    {
        if (insight is null || insight.CoupledFiles.Count == 0)
        {
            return Array.Empty<RelatedNode>();
        }

        var list = new List<RelatedNode>(insight.CoupledFiles.Count);
        foreach (var coupled in insight.CoupledFiles)
        {
            var norm = PathUtil.NormalizeSlashes(coupled.Path);
            var target = coupledFileHref?.Invoke(coupled.Path);
            var href = target is { Length: > 0 } ? prefix + PathUtil.NormalizeSlashes(target) : null;
            list.Add(new RelatedNode(
                href, norm, BaseName(coupled.Path),
                coupled.Support, coupled.Confidence, coupled.Lift, coupled.CrossBoundary,
                coupled.Kind == GitMetrics.CouplingKind.Process));
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

    /// <summary>Builds the placeholder page's left sidebar: the additive "view source online" action (Story 7.7).
    /// Returns empty when there is none, so <see cref="AppendBody"/> renders the body full-width.
    ///
    /// <para><b>Story 24.2 removed this method's citing-artifact graph, and the reason is worth recording because
    /// the story expected a decision here.</b> <c>Charts.ReferenceGraph</c> had two call sites, and this was the
    /// second — but it was UNREACHABLE. Its only caller is <see cref="BuildPlaceholderPage"/>, and only on the
    /// <c>!hasExtraTabs</c> branch; <c>hasExtraTabs</c> is false only when the relationships panel is empty, and
    /// that panel is empty only when there are NO citing artifacts and no coupled files. So this method could never
    /// be reached with a non-empty <paramref name="referencedBy"/>, and the graph it held could never draw. Pinned
    /// by <c>CodeFileTemplaterTests.PlaceholderPage_WithCiters_RendersTabsNotAnAsideGraph</c> rather than left as a
    /// reasoning claim.</para>
    ///
    /// <para>So the answer to the story's open question — keep the SVG for this path, or give it the component with
    /// an empty coupled population? — is neither: there was no live second renderer to decide about, and after this
    /// story exactly ONE relationship renderer exists.</para></summary>
    private static string BuildAside(string? externalSourceUrl)
    {
        var external = externalSourceUrl is { Length: > 0 } u ? ExternalSourceAnchor(u) : "";
        if (external.Length == 0) return "";

        var sb = new StringBuilder();
        sb.Append("<aside class=\"code-aside\">\n");
        sb.Append($"<div class=\"code-actions\">{external}</div>\n");
        sb.Append("</aside>\n");
        return sb.ToString();
    }

    /// <summary>Builds the relationship card: the Story 24.2 interactive ego graph (<see cref="RelationshipGraph"/>)
    /// plus the canonical sr-only text twin it carries.
    ///
    /// <para><b>What changed in Story 24.2.</b> The four pre-rendered <c>.ref-graph-view</c> SVG panels and their
    /// pure-CSS <c>~</c>-sibling show/hide are gone, together with <c>Charts.ReferenceGraph</c> itself: ADR 0013 §1/§4
    /// make the text twin the no-JS contract, so retaining an SVG <em>and</em> adding the interactive graph is the
    /// dual-renderer option ADR 0013's options table explicitly rejected. The two toggles survive (owner decision D3)
    /// as CLIENT-side edge-visibility filters over ONE solved layout — they hide edges, they never re-lay-out
    /// (ADR 0030 §4) — and they ride inside the component's <c>hidden</c> control bar so a JS-off reader never sees
    /// an inert checkbox. That is why they are emitted only when their edge population is non-empty: the retired
    /// card shipped both unconditionally, which meant a checkbox that toggled nothing.</para>
    ///
    /// <para><b>The sr-only list is the twin, and it is complete for BOTH populations</b> (ADR 0013 §2, audited by
    /// this story's Task 6): every citing artifact — ALL of them, including any beyond the graph's drawn
    /// <see cref="RelationshipGraph.ArtifactNodeCap"/> — with its epic membership and its cross-edges, and every
    /// coupled file with support, directional confidence, cross-boundary and process-coupling as WORDS and lift on
    /// the row title. Assistive technology therefore never has less information than the richest sighted view.</para></summary>
    private static string BuildRelationshipsCard(
        string prefix, string repoRelativePath, string outputRelativePath,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)> referencedBy,
        IReadOnlyList<RelatedNode> related,
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

        var twin = BuildRelationshipsTwin(nodes, refEpics, related, storyRelatedEdges, relatedRelatedEdges);
        var model = RelationshipGraphModel(
            repoRelativePath, outputRelativePath, nodes, refEpics, related,
            storyRelatedEdges, relatedRelatedEdges, twin);

        return RelationshipGraph.Render(
            model,
            panelClass: "chart-panel code-relationships",
            panelAttributes: " data-relgraph-panel",
            showEpicFilter: model.Edges.Any(e => e.Kind == RelationshipGraph.EdgeKind.EpicMembership),
            showCrossFilter: model.Edges.Any(e =>
                e.Kind is RelationshipGraph.EdgeKind.CrossCitation or RelationshipGraph.EdgeKind.CrossCoupling));
    }

    /// <summary>Projects the resolved citing-artifact / coupled-file populations into the
    /// <see cref="RelationshipGraph"/> model: the pinned focal node, the two ring populations, the epic hubs, and
    /// the four edge kinds. Extracted from <see cref="BuildRelationshipsCard"/> rather than inlined — this file
    /// already carries two <c>S3776</c> cognitive-complexity errors and several <c>S107</c> parameter-count
    /// warnings, so it is at its complexity ceiling and new work extracts.
    ///
    /// <para>Node ORDER is load-bearing twice over: it is the ring's angular sweep (so the populations stay in
    /// contiguous arcs and the hub-and-spoke read survives), and it is the reading order the client's roving
    /// tabindex follows. Focal first, then citing artifacts, then epic hubs, then coupled files — the last already
    /// confidence-desc from Story 24.1, so the ring sweeps the ranked order and the twin below it agrees.</para></summary>
    private static RelationshipGraph.RelationshipGraphModel RelationshipGraphModel(
        string repoRelativePath, string outputRelativePath,
        IReadOnlyList<(string Href, string Title, string Short)> nodes,
        IReadOnlyList<(int EpicNumber, string EpicTitle)?> refEpics,
        IReadOnlyList<RelatedNode> related,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges,
        string twinHtml)
    {
        // The artifact ring stays bounded exactly as the retired SVG's did: a heavily-cited hub file would otherwise
        // crowd the ring into illegibility. The overflow is disclosed in the ranking caption and the twin still
        // enumerates every citer, so the honesty of "+N more" survives the renderer change.
        var shownRefs = Math.Min(nodes.Count, RelationshipGraph.ArtifactNodeCap);
        var overflow = nodes.Count - shownRefs;

        var graphNodes = new List<RelationshipGraph.GraphNode>(1 + shownRefs + related.Count);
        var edges = new List<RelationshipGraph.GraphEdge>();

        var focalLabel = BaseName(repoRelativePath);
        var focalPath = PathUtil.NormalizeSlashes(repoRelativePath);
        graphNodes.Add(new RelationshipGraph.GraphNode(
            "focal", focalLabel, focalPath, RelationshipGraph.NodeKind.Focal, null,
            Weight: Math.Max(1, shownRefs + related.Count), Strength: 0,
            Detail: $"{focalPath} — this file. {nodes.Count.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(nodes.Count, "citing artifact", "citing artifacts")}, {related.Count.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(related.Count, "co-changed file", "co-changed files")}."));

        // --- Citing artifacts. Index i in `nodes` maps to graph ordinal 1 + i for i < shownRefs.
        for (var i = 0; i < shownRefs; i++)
        {
            var (href, title, shortLabel) = nodes[i];
            var epicSuffix = refEpics[i] is { } e ? $" (Epic {e.EpicNumber}: {e.EpicTitle})" : "";
            graphNodes.Add(new RelationshipGraph.GraphNode(
                $"ref{i.ToString(CultureInfo.InvariantCulture)}", shortLabel, title,
                RelationshipGraph.NodeKind.Artifact, href,
                // Citing artifacts carry no change-frequency signal of their own (they are documents, not tracked
                // code files), so they all render at the base marker size. Said here rather than faked with a
                // derived number that would read as data.
                Weight: 1, Strength: 0,
                Detail: $"{title}{epicSuffix} — cites this file."));
            // Detail is null: a citation's description is derivable from its two endpoints, so the component's
            // per-kind phrase describes it once instead of every edge re-spelling both titles.
            edges.Add(new RelationshipGraph.GraphEdge(
                0, 1 + i, RelationshipGraph.EdgeKind.Citation, Support: 0,
                CrossBoundary: false, ProcessCoupling: false, Detail: null));
        }

        // --- Epic hubs, one per distinct epic among the DRAWN citers, in first-appearance order. Their edges are
        // what the "Group by epic" filter shows and hides; the hubs themselves are hidden alongside their edges so
        // the filter never leaves a disconnected chip floating (surviving nodes still do not MOVE — ADR 0030 §4).
        var epicOrdinal = new Dictionary<int, int>();
        for (var i = 0; i < shownRefs; i++)
        {
            if (refEpics[i] is not { } epic) continue;
            if (!epicOrdinal.TryGetValue(epic.EpicNumber, out _))
            {
                epicOrdinal[epic.EpicNumber] = graphNodes.Count;
                var members = 0;
                for (var j = 0; j < shownRefs; j++)
                {
                    if (refEpics[j] is { } other && other.EpicNumber == epic.EpicNumber) members++;
                }
                var epicTitle = $"Epic {epic.EpicNumber.ToString(CultureInfo.InvariantCulture)}: {epic.EpicTitle}";
                graphNodes.Add(new RelationshipGraph.GraphNode(
                    $"epic{epic.EpicNumber.ToString(CultureInfo.InvariantCulture)}",
                    $"Epic {epic.EpicNumber.ToString(CultureInfo.InvariantCulture)}", epicTitle,
                    RelationshipGraph.NodeKind.EpicHub, null,
                    Weight: members, Strength: 0,
                    Detail: $"{epicTitle} — {members.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(members, "citing story", "citing stories")}."));
            }
            edges.Add(new RelationshipGraph.GraphEdge(
                1 + i, epicOrdinal[epic.EpicNumber], RelationshipGraph.EdgeKind.EpicMembership,
                Support: 0, CrossBoundary: false, ProcessCoupling: false, Detail: null));
        }

        // --- Coupled files. Ordinal = coupledBase + j, so the index-aligned cross-edge builders below can be
        // translated without a lookup.
        var coupledBase = graphNodes.Count;
        for (var j = 0; j < related.Count; j++)
        {
            var r = related[j];
            graphNodes.Add(new RelationshipGraph.GraphNode(
                $"rel{j.ToString(CultureInfo.InvariantCulture)}", r.Short, r.Title,
                RelationshipGraph.NodeKind.Coupled, r.Href,
                Weight: r.Support,
                // Confidence is the pull toward the hub, so a stronger couple is DRAWN NEARER — the graph's one
                // continuous channel for it. Deliberately not stroke width: Plotly's line style is trace-level, so
                // width can only be banded (ADR 0030 §5).
                Strength: r.Confidence,
                Detail: RelatedDetail(r)));
            // A coupling spoke DOES carry its own sentence: support, confidence and lift are facts about the PAIR
            // and cannot be recovered from either endpoint, so no template can express them.
            edges.Add(new RelationshipGraph.GraphEdge(
                0, coupledBase + j, RelationshipGraph.EdgeKind.Coupling,
                Support: r.Support, CrossBoundary: r.CrossBoundary, ProcessCoupling: r.ProcessCoupling,
                Detail: $"{focalPath} and {RelatedDetail(r)}"));
        }

        // --- Cross edges (owner decision D3's second filter). The two builders are INDEX-ALIGNED with the citer
        // list and the coupled list respectively (SiteGenerator.BuildStoryRelatedEdges / BuildRelatedRelatedEdges).
        // Widening the coupled cap to RelationshipGraphCoupledCap changes the coupled list's LENGTH, so both bounds
        // are re-checked here rather than assumed — an out-of-range index is dropped, never drawn against the wrong
        // node.
        if (storyRelatedEdges is { Count: > 0 })
        {
            foreach (var (refIndex, relatedIndex) in storyRelatedEdges)
            {
                if (refIndex < 0 || refIndex >= shownRefs) continue;
                if (relatedIndex < 0 || relatedIndex >= related.Count) continue;
                edges.Add(new RelationshipGraph.GraphEdge(
                    1 + refIndex, coupledBase + relatedIndex, RelationshipGraph.EdgeKind.CrossCitation,
                    Support: 0, CrossBoundary: false, ProcessCoupling: false, Detail: null));
            }
        }
        if (relatedRelatedEdges is { Count: > 0 })
        {
            foreach (var (a, b) in relatedRelatedEdges)
            {
                if (a == b) continue;
                if (a < 0 || a >= related.Count || b < 0 || b >= related.Count) continue;
                edges.Add(new RelationshipGraph.GraphEdge(
                    coupledBase + a, coupledBase + b, RelationshipGraph.EdgeKind.CrossCoupling,
                    Support: 0, CrossBoundary: false, ProcessCoupling: false, Detail: null));
            }
        }

        var ranking = BuildRankingCaption(related.Count, overflow);
        var meta = new Charts.ChartMeta(
            Title: "Relationships",
            Window: null,
            Ranking: ranking,
            // The change-coupling framing is emitted ONLY when there is coupling to frame. A citations-only card
            // (no --deep-git, or a file with no qualifying couples) would otherwise carry a sentence about a
            // metric it does not draw — the misdescribing-frame class Story 10.2 exists to prevent.
            Why: related.Count > 0 ? Charts.WhyText(Charts.ChartMetric.ChangeCoupling) : null,
            Note: related.Any(r => r.ProcessCoupling) ? Charts.ProcessCouplingNote : null);

        return new RelationshipGraph.RelationshipGraphModel(
            meta, "relgraph-" + RelGraphDomSlug(outputRelativePath), graphNodes, edges, twinHtml);
    }

    /// <summary>The ranking caption (Story 10.2's <see cref="Charts.ChartMeta.Ranking"/> slot) — and the home of the
    /// "+N more" honesty disclosure now that there is no on-graph overflow chip. Server-rendered, so a JS-off reader
    /// sees it too.</summary>
    private static string BuildRankingCaption(int relatedCount, int overflow)
    {
        var parts = new List<string>(2);
        if (relatedCount > 0)
        {
            // Describes the ORDER of the listing, which is true whether or not a chart ever draws. The earlier
            // wording ended "— the strongest are drawn nearest the centre", which the JS-off audit caught
            // misdescribing a page where nothing is drawn; that reading now lives in the legend entry it belongs
            // to, which is itself revealed only on a successful mount.
            parts.Add("Co-changed files are ranked by how often a change to this file came with a change to them.");
        }
        if (overflow > 0)
        {
            parts.Add($"{overflow.ToString(CultureInfo.InvariantCulture)} further citing {Charts.Plural(overflow, "artifact is", "artifacts are")} listed in full below but not drawn, to keep the graph legible.");
        }
        return parts.Count == 0 ? "" : string.Join(" ", parts);
    }

    /// <summary>The ONE composed sentence describing a coupled file — used by the graph node's tooltip, its
    /// accessible name, and its coupling spoke. Shares its numbers with the twin's row by construction (same
    /// <see cref="RelatedNode"/>, same <see cref="Charts.Percent"/>), so chart and text cannot disagree. Words, not
    /// colour, for both the cross-boundary and process-coupling facts (UX-DR17/NFR8).</summary>
    private static string RelatedDetail(RelatedNode r)
    {
        var sb = new StringBuilder();
        sb.Append(r.Title)
          .Append(" — changed together ")
          .Append(r.Support.ToString(CultureInfo.InvariantCulture))
          .Append(' ')
          .Append(Charts.Plural(r.Support, "time", "times"))
          .Append(", confidence ")
          .Append(Charts.Percent(r.Confidence));
        if (r.Lift is { } lift) sb.Append(", lift ").Append(lift.ToString("0.0", CultureInfo.InvariantCulture)).Append('×');
        if (r.CrossBoundary) sb.Append(", cross-boundary");
        if (r.ProcessCoupling) sb.Append(", process coupling");
        sb.Append('.');
        return sb.ToString();
    }

    /// <summary>The canonical sr-only text twin (ADR 0013 §2; Story 24.1 AC #3) — server-rendered, complete for
    /// BOTH node populations, navigable, and carrying every metric as non-colour text. Passed to
    /// <see cref="RelationshipGraph.Render"/>, which refuses to emit a chart without it.</summary>
    private static string BuildRelationshipsTwin(
        IReadOnlyList<(string Href, string Title, string Short)> nodes,
        IReadOnlyList<(int EpicNumber, string EpicTitle)?> refEpics,
        IReadOnlyList<RelatedNode> related,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges)
    {
        var hasRelated = related.Count > 0;
        var sb = new StringBuilder();
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
            // Story 24.1 (AC #3): this list is the CANONICAL text twin the Epic 24 graph stories reuse rather than
            // replace, so it carries the full directional metric — directional confidence read from this file's
            // side, and a cross-boundary marker as real WORDS (never colour or a glyph alone, UX-DR19/NFR8). Lift
            // rides the row's title attribute: it is the specialist's number, and spending sr-only reading time on
            // it would bury the confidence the row is actually about.
            sb.Append("    <li class=\"ref-list-related\">Files changed alongside this one:\n");
            sb.Append("      <ul>\n");
            for (var j = 0; j < related.Count; j++)
            {
                var r = related[j];
                var pathHtml = PathUtil.Html(r.Title);
                var nameCell = r.Href is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(r.Href)}\">{pathHtml}</a>"
                    : pathHtml;
                var relatedCrossSuffix = BuildRelatedCrossSuffix(j, storyRelatedEdges, relatedRelatedEdges, nodes, related);
                var boundarySuffix = r.CrossBoundary ? " &#183; cross-boundary" : "";
                // Story 24.2 Task 6 (the ADR 0013 §3 audit): the graph draws process coupling as a DOTTED spoke,
                // and §2 forbids a fact existing only inside the chart — so the twin has to be able to say it. It
                // could not before this story; the audit is what found that, and this is the fix rather than a note.
                var processSuffix = r.ProcessCoupling ? " &#183; process coupling" : "";
                var liftAttr = r.Lift is { } lift
                    ? $" title=\"Lift {lift.ToString("0.0", CultureInfo.InvariantCulture)}&#215; this file's usual rate\""
                    : "";
                sb.Append($"        <li{liftAttr}>{nameCell} &#8212; changed together {r.Support.ToString(CultureInfo.InvariantCulture)} {Charts.Plural(r.Support, "time", "times")} &#183; confidence {Charts.Percent(r.Confidence)}{boundarySuffix}{processSuffix}{relatedCrossSuffix}</li>\n");
            }
            sb.Append("      </ul>\n");
            sb.Append("    </li>\n");
        }
        sb.Append("  </ul>\n");
        return sb.ToString();
    }

    /// <summary>The sr-only suffix on a citing-artifact's list item naming any related file it ALSO cites (the
    /// "Show relationships" story&#8596;related-file edge's text equivalent).</summary>
    private static string BuildStoryCrossSuffix(
        int refIndex, IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges,
        IReadOnlyList<RelatedNode> related)
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
        IReadOnlyList<RelatedNode> related)
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

    /// <summary>A per-page-unique slug for the relationship graph's DOM id — which now addresses the chart host, its
    /// payload island (<c>{id}-data</c>) and its two filter checkboxes, so several code pages consolidated into one
    /// document (SPA/webview capture) never cross-wire an island with the wrong host or a <c>label for</c> with the
    /// wrong <c>input id</c>. Built from the same <see cref="Slugify"/> helper <see cref="TabGroupName"/> uses
    /// (independently, not by slicing its output), so the two stay correct even if one's prefix ever changes.
    ///
    /// <para>Story 24.2 renamed this from <c>RefGraphGroupSlug</c> and it now carries MORE weight than it did: the
    /// retired pure-CSS toggles keyed their show/hide off shared checkbox CLASSES and needed the slug only for label
    /// semantics, whereas the component resolves its island BY id. A collision is now a mis-rendered chart, not a
    /// mis-labelled checkbox.</para></summary>
    private static string RelGraphDomSlug(string outputRelativePath) => Slugify(outputRelativePath);

    /// <summary>A per-page-unique radio-group name for the view tabs, derived from the page's output-relative path.
    /// Uniqueness matters when several code pages are captured into one document (SPA/webview consolidation): a
    /// shared name would make their radio groups mutually exclusive and cross-wire the tabs.</summary>
    private static string TabGroupName(string outputRelativePath) => "code-view-" + Slugify(outputRelativePath);

    /// <summary>Collapses non-alphanumeric runs in a path to a single hyphen and lowercases it — the shared slug
    /// primitive behind both <see cref="TabGroupName"/> and <see cref="RelGraphDomSlug"/>. Path separators are
    /// encoded as the alphanumeric token <c>x2f</c> (after escaping any pre-existing <c>x2f</c> run as
    /// <c>x2fx2f</c>) so <c>a/b</c>, <c>a-b</c>, and a literal <c>ax2fb</c> segment never collide under SPA/webview
    /// document consolidation. [spec-7-1-deferred-debt-cleanup]</summary>
    internal static string SoftSlugify(string outputRelativePath)
    {
        // Escape existing x2f first so a literal "x2f" in a filename can't collide with an encoded slash.
        var encoded = outputRelativePath
            .Replace("x2f", "x2fx2f", StringComparison.OrdinalIgnoreCase)
            .Replace("/", "x2f", StringComparison.Ordinal);
        var sb = new StringBuilder();
        var prevHyphen = false;
        foreach (var c in encoded)
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

    private static string Slugify(string outputRelativePath) => SoftSlugify(outputRelativePath);

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
    /// stable URL so navigation never breaks (AC #1) — only the line table is replaced by an explanatory note.
    /// When deep-git <paramref name="insight"/> (or relationships) is available, Insights/History/Relationships tabs
    /// still render — the Code panel holds the placeholder reason. [spec-7-1-deferred-debt-cleanup]</summary>
    public static string RenderPlaceholder(
        string repoRelativePath,
        string outputRelativePath,
        string reason,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy = null,
        string? externalSourceUrl = null,
        EntityPager? pager = null,
        NavLocalContext? localContext = null,
        FileInsight? insight = null,
        Func<string, string?>? coupledFileHref = null,
        Func<string, string?>? commitHref = null,
        Func<DateOnly, string?>? dayHref = null,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges = null,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPlaceholderPage(
            repoRelativePath, outputRelativePath, reason, nav, referencedBy, externalSourceUrl, pager, localContext,
            insight, coupledFileHref, commitHref, dayHref, storyRelatedEdges, relatedRelatedEdges)).Content;

    /// <summary>Builds a not-rendered code page's host-neutral <see cref="PageView"/> — see
    /// <see cref="BuildPage"/>. No Prism head here: a placeholder renders no <c>&lt;code&gt;</c> block.
    /// [Story 23.4 AC #3]</summary>
    public static PageView BuildPlaceholderPage(
        string repoRelativePath,
        string outputRelativePath,
        string reason,
        SiteNav nav,
        IReadOnlyList<(string OutputUrl, string Title, (int Number, string Title)? Epic)>? referencedBy = null,
        string? externalSourceUrl = null,
        EntityPager? pager = null,
        NavLocalContext? localContext = null,
        FileInsight? insight = null,
        Func<string, string?>? coupledFileHref = null,
        Func<string, string?>? commitHref = null,
        Func<DateOnly, string?>? dayHref = null,
        IReadOnlyList<(int RefIndex, int RelatedIndex)>? storyRelatedEdges = null,
        IReadOnlyList<(int RelatedIndexA, int RelatedIndexB)>? relatedRelatedEdges = null)
    {
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var shell = BeginShell(repoRelativePath, outputRelativePath, prefix, nav, pager: pager, localContext: localContext);
        var sb = shell.Body;

        sb.Append("  <div class=\"meta-pills\"><span class=\"pill\">Not rendered</span></div>\n");
        sb.Append("</header>\n\n");

        var insightsPanel = BuildInsightsPanel(insight);
        var relationshipsPanel = BuildRelationshipsPanel(
            prefix, repoRelativePath, outputRelativePath, referencedBy, insight, coupledFileHref, storyRelatedEdges, relatedRelatedEdges);
        var historyPanel = BuildHistoryPanel(prefix, insight, commitHref, dayHref);
        var hasExtraTabs = insightsPanel.Length > 0 || relationshipsPanel.Length > 0 || historyPanel.Length > 0;

        // With no insight/relationships tabs, the page keeps the pre-tab two-column layout (aside + placeholder
        // body), and any external link rides in the aside instead — so the Source panel only carries the
        // external-link anchor itself when tabs are actually rendered.
        var sourceHead = hasExtraTabs && externalSourceUrl is { Length: > 0 }
            ? $"    <h2>Source</h2>\n    {ExternalSourceAnchor(externalSourceUrl)}\n"
            : "    <h2>Source</h2>\n";
        var source =
            $"<section class=\"code-source-section\">\n  <div class=\"code-source-head\">\n{sourceHead}  </div>\n" +
            $"<p class=\"code-placeholder\">{PathUtil.Html(reason)}</p>\n</section>\n";

        if (!hasExtraTabs)
        {
            AppendBody(sb, BuildAside(externalSourceUrl), source);
            return EndShell(shell);
        }

        // Deep-git / relationships present: same tab shell as RenderPage; Code panel is the placeholder reason.
        var tabs = new List<CodeTab>(4);
        if (insightsPanel.Length > 0) tabs.Add(new CodeTab("insights", "Insights", insightsPanel));
        if (relationshipsPanel.Length > 0) tabs.Add(new CodeTab("relationships", "Relationships", relationshipsPanel));
        if (historyPanel.Length > 0) tabs.Add(new CodeTab("history", "History", historyPanel));
        tabs.Add(new CodeTab("source", "Code", source));

        AppendTabs(sb, outputRelativePath, tabs);
        return EndShell(shell);
    }

    /// <summary>Emits the head + nav + breadcrumb + open <c>&lt;main&gt;</c>/<c>&lt;header&gt;</c> shared by both the
    /// full page and the placeholder. Leaves the header open so each caller appends its own meta pill(s) and closes
    /// it — mirroring the synthesized-page shape of <see cref="CommitDayTemplater"/>. <paramref name="highlight"/>
    /// adds the vendored Prism stylesheet + highlighter to the head (only the full page, which actually renders a
    /// <c>&lt;code&gt;</c> block, asks for them).</summary>
    /// <summary>The identity every code page shares, carried from <see cref="BeginShell"/> to
    /// <see cref="EndShell"/> so the page's chrome facts reach its <see cref="PageView"/> instead of being
    /// string-built into a full document and discarded. Story 23.4 moved this templater onto the delivery
    /// contract; the two-phase Begin/End shape is unchanged.</summary>
    private sealed record CodeShell(
        SiteNav Nav, string RepoRelativePath, string OutputRelativePath, string Prefix,
        bool Highlight, EntityPager? Pager, NavLocalContext? LocalContext, StringBuilder Body);

    private static CodeShell BeginShell(string repoRelativePath, string outputRelativePath, string prefix, SiteNav nav, bool highlight = false, EntityPager? pager = null, NavLocalContext? localContext = null)
    {
        var sb = new StringBuilder();
        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1] The .code-page wrapper
        // gives the header + two-column body a centered max-width with side gutters (this synthesized page has no
        // markdown .doc-body of its own to supply them, so content otherwise ran to the window edge).
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<div class=\"code-page\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Source File</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(repoRelativePath)}</h1>\n");
        return new CodeShell(nav, repoRelativePath, outputRelativePath, prefix, highlight, pager, localContext, sb);
    }

    /// <summary>Closes the shared shell and returns the page as a <see cref="PageView"/>. The Prism theme +
    /// highlighter ride <see cref="AssetManifest.ExtraHead"/> — the second real user of that field, alongside the
    /// Impact Map's head-placed hierarchy boot marker. Only the full page (which actually renders a
    /// <c>&lt;code&gt;</c> block) asks for them. [Story 23.4 AC #3]</summary>
    private static PageView EndShell(CodeShell shell)
    {
        var sb = shell.Body;
        sb.Append("</div>\n</main>\n\n");

        // Story 24.2: derived from the FINISHED body, never hand-set — a flag computed from the page cannot
        // disagree with the page. False-defaulted, so a code page with no graph (no citers, no coupling) keeps
        // byte-identical output and never pulls the 1.2 MB bundle.
        var body = sb.ToString();
        var graph = RelationshipGraph.ContainsHost(body);

        return new PageView
        {
            Kind = PageKind.Doc,
            OutputRelativePath = shell.OutputRelativePath,
            Title = $"{shell.RepoRelativePath} — {shell.Nav.SiteTitle}",
            MetaDescription = $"Source file {shell.RepoRelativePath} in {shell.Nav.SiteTitle}.",
            Nav = shell.Nav.ToNavigationView(shell.OutputRelativePath, shell.LocalContext),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                (shell.RepoRelativePath, null),
            }),
            // Sibling pager (prev/next across sibling files, alphabetical) rides the coherent wayfinding strip
            // alongside the breadcrumb now, not the body's own header. [Story 10.11]
            Pager = shell.Pager,
            Assets = new AssetManifest
            {
                StylesheetHref = shell.Prefix + ForgeOptions.StylesheetName,
                ScriptHref = shell.Prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
                GraphEngineNeeded = graph,
                GraphBootInline = graph,
                // The code page ALREADY uses ExtraHead for Prism — the graph's boot marker therefore rides the
                // inline seam rather than clobbering it.
                ExtraHead = shell.Highlight ? HighlightHead(shell.Prefix) : null,
            },
            Interaction = InteractionState.None,
            BodyHtml = body,
        };
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
