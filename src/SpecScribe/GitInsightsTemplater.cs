using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the opt-in aggregate <c>git-insights.html</c> hub (FR-10) — the "click in to see more"
/// destination behind the dashboard's Git Pulse panel. Two sections, activity first: activity over time (the
/// reused commit heatmap, whose active days already link to their per-day pages — owner feedback: this is the
/// page's most immediately orienting chart, so it leads), then a whole-tree, interactive code-ownership
/// sunburst/treemap toggle (Story 7.11 — replaces the earlier files-and-contributors master-detail table AND the
/// earlier plain ranked ownership table; see this file's Change Log for that history). A synthesized page (no
/// markdown source), so it builds its own shell the way <see cref="CommitDayTemplater"/> does rather than
/// going through <see cref="HtmlTemplater.RenderPage"/>.
/// <para><b>No-JS contract — ADR 0013 §4, which SUPERSEDES the ADR 0010 §2 reading this comment used to state.</b>
/// The superseded wording claimed the default-mode chart "renders and works with JS off" and treated that as the
/// surface's whole no-JS story. Under ADR 0013 the contract is a server-rendered <b>text twin</b>: JS-off may lose
/// the visualization, but it must never lose the <b>information</b> or the <b>navigation</b>.
/// <para>Story 20.6's audit (surface 6, in <c>20-6-text-twin-audit.md</c>) recorded this page as the one surface
/// with <b>no twin at all</b>: 1,115 file nodes and 224 directory nodes, only 6 of those files linked anywhere
/// outside an <c>&lt;svg&gt;</c>, zero tables or lists enumerating them (Story 7.11 deleted both prior ownership
/// tables), no per-node <c>&lt;title&gt;</c> on the treemap, and both charts <c>role="img"</c> — which prunes
/// their in-chart links from the accessibility tree.</para>
/// <b>Story 20.9 satisfies the contract and only then retired the SVG</b>, in that order, because AC#3 states it:
/// the ownership chart now renders through the ONE Hierarchy Explorer component, whose server-rendered
/// <see cref="HierarchyExplorer.TextTwinHtml"/> enumerates EVERY node the chart draws — nested by directory, each
/// file carrying its dominant author, share %, contributor count and last-active date as prose, and every file a
/// real resolving link. It ships as a collapsed <c>&lt;details&gt;</c> (Story 20.6 D3's default), which needs no
/// script to open. A per-directory rollup would have been shorter and more readable and was rejected: it would not
/// enumerate every node, failing ADR 0013 §2's completeness predicate.</para>
/// <para>JS adds the live dimension selector (dominant-author share / top contributors / individual-author
/// spotlight / staleness threshold) through the component's dimension contract. Author information stays
/// descriptive attribution in every mode, never a cross-repo ranked scoreboard (FR-10, ADR 0010 §4 — unaffected by
/// rendering technology): the top-contributor roster is a bounded colour palette and the spotlight picker is the
/// alphabetical union of every file's own contributors. Outgoing file links are guarded on target existence via
/// the <c>fileHref</c> resolver (Stories 7.1/7.4 seam): no resolver or no target → a plain, focusable node, never a
/// dead link. [Story 3.8; Story 7.11 rewrite; Story 20.6 contract correction; Story 20.9 conversion]</para></summary>
public static class GitInsightsTemplater
{
    public static string RenderPage(
        GitInsightsData insights,
        GitPulse? git,
        SiteNav nav,
        CodeMap codeMap,
        IReadOnlyList<string> topAuthors,
        Func<string, string?>? fileHref = null,
        DateOnly? today = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(insights, git, nav, codeMap, topAuthors, fileHref, today)).Content;

    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract. Story 23.4
    /// moved every standalone templater onto it so the IR's content region can be COMPOSED
    /// (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup + wayfinding + body) instead of sliced back
    /// out of a rendered full page. <see cref="RenderPage"/> is the unchanged HTML projection of this same model,
    /// so the bytes are identical. The hierarchy engine now rides
    /// <see cref="AssetManifest.HierarchyEngineNeeded"/> — computed from the RENDERED BODY exactly as this page
    /// computed it inline, so the flag still cannot disagree with what the page contains, and with NO inline boot
    /// marker (the Code Map's shape, not the dashboard's). [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(
        GitInsightsData insights,
        GitPulse? git,
        SiteNav nav,
        CodeMap codeMap,
        IReadOnlyList<string> topAuthors,
        Func<string, string?>? fileHref = null,
        DateOnly? today = null)
    {
        var outputPath = SiteNav.GitInsightsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — git-insights.html is at the output root.

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"deep-page git-insights\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Git Insights &middot; opt-in</div>\n");
        sb.Append("  <h1>Git Insights</h1>\n");
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill\">{N(insights.CommitCount)} {Charts.Plural(insights.CommitCount, "commit", "commits")} analyzed</span>\n");
        var filesLabel = insights.TotalFilesTouched > insights.Files.Count
            ? TruncatedFilesRankingFact(insights.Files.Count, insights.TotalFilesTouched, capitalized: false)
            : $"{N(insights.Files.Count)} {Charts.Plural(insights.Files.Count, "file", "files")}";
        sb.Append($"    <span class=\"pill\">{filesLabel}</span>\n");
        sb.Append($"    <span class=\"pill\">{N(insights.ContributorCount)} {Charts.Plural(insights.ContributorCount, "contributor", "contributors")}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        AppendActivitySection(sb, insights, git, today);
        AppendOwnershipSection(sb, insights, codeMap, topAuthors, fileHref);

        sb.Append("</main>\n\n");

        var body = sb.ToString();
        return new PageView
        {
            Kind = PageKind.GitInsights,
            OutputRelativePath = outputPath,
            Title = $"Git Insights — {nav.SiteTitle}",
            MetaDescription = $"Aggregate git insights for {nav.SiteTitle}: code ownership concentration and activity over time.",
            Nav = nav.ToNavigationView(outputPath, nav.BuildInsightsLocalContext(outputPath)),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                ("Git Insights", null),
            }),
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
                // The vendored plotly.js hierarchy engine — same seam and same terms as the Impact Map and the
                // Code Map. Conditional on a host actually being present, so the solo-maintainer reframe and the
                // no-file-data empty state both ship no engine they cannot use. No inline boot marker: this page
                // never emitted one. [Story 20.9 Task 5; Story 23.4]
                HierarchyEngineNeeded = HierarchyExplorer.ContainsHost(body),
            },
            Interaction = InteractionState.None,
            BodyHtml = body,
        };
    }

    /// <summary>Code ownership &amp; bus-factor: ONE Hierarchy Explorer instance over EVERY source file (not a
    /// top-N subset — <paramref name="codeMap"/> comes from the same uncapped <see cref="CodeMap.Build"/> walk the
    /// Code Map and Risk Quadrant pages use), drawing both shapes from one
    /// <see cref="HierarchyExplorer.ProjectOwnership"/> payload behind the component's standard selector.
    ///
    /// <para><b>Story 20.9 replaced two server-rendered SVGs and a pure-CSS view toggle with that one instance</b>
    /// (Story 7.11's <c>CodeOwnershipSunburst</c> + <c>CodeOwnershipTreemap</c>, and 20.6's finding that this page
    /// had no text twin at all). The four live modes — dominant-author share, top contributors, an
    /// individual-author spotlight, and a configurable staleness threshold — are now DECLARED as dimensions
    /// (<see cref="HierarchyExplorer.OwnershipDimensions"/>) and resolved by the shared component, rather than
    /// implemented as four bespoke recolour functions that each knew this page's vocabulary. Two of them cannot be
    /// precomputed at all (owner decision D1): the spotlight takes an arbitrary contributor and staleness a free
    /// 1–60 month threshold, which is exactly why the payload carries each file's RAW generation-time values.</para>
    ///
    /// <para>Every file node carries the rich <c>.codemap-card</c> hover card
    /// (<see cref="Charts.BuildOwnershipCard"/>) the shipped wedges did — Story 20.5 made <c>.ss-tooltip</c> +
    /// <c>data-tip-html</c> the one tooltip system site-wide precisely so swapping the drawing engine never swaps
    /// the tooltip's look. ONE shared legend area carries all four mode-specific blocks, routed through the
    /// component's framing block; it shows exactly one at a time so the visible legend can never disagree with
    /// what is coloured (owner feedback), and it is hidden until a successful mount because a colour key for a
    /// chart that never renders is chrome for nothing.</para>
    ///
    /// <para>In a solo-maintainer repo every mode would trivially read "one person, everywhere", so the section
    /// reframes honestly instead of flagging every file as a bus-factor risk (AC #4, NFR8). That gate reads the
    /// <paramref name="codeMap"/>'s OWN contributor population rather than <c>insights.ContributorCount</c>, which
    /// counts authors across all commits repo-wide — the two can legitimately diverge.</para></summary>
    private static void AppendOwnershipSection(
        StringBuilder sb,
        GitInsightsData insights,
        CodeMap codeMap,
        IReadOnlyList<string> topAuthors,
        Func<string, string?>? fileHref)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <div class=\"chart-frame-head\"><h2>Code Ownership &amp; Bus-Factor</h2></div>\n");
        sb.Append(Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.CodeOwnership)));

        if (codeMap.IsEmpty)
        {
            sb.Append("  <div class=\"chart-panel\"><div class=\"chart-empty\">No file change data available.</div></div>\n");
            sb.Append("</section>\n\n");
            return;
        }

        var files = codeMap.Files();

        // The solo-repo reframe gate must read the SAME contributor population the chart itself colors from
        // (codeMap's own per-file Contributors) — NOT insights.ContributorCount, which counts every author across
        // ALL commits repo-wide, including commits that only touch files outside codeMap's current source-file
        // walk (deleted files, excluded paths). Those two counts can diverge: a second author whose only commits
        // touch such files would make insights.ContributorCount == 2 while the chart still renders 100%-dominant-
        // by-one-author coloring for every wedge — exactly the "flags everything at-risk, noise not signal" state
        // this reframe exists to prevent (AC #4). [Review 2026-07-22]
        var codeMapContributorCount = files
            .SelectMany(f => f.Metrics?.Contributors ?? Array.Empty<FileContributor>())
            .Select(c => c.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (codeMapContributorCount == 1)
        {
            // AC #4: the common single-maintainer OSS case. Every mode would trivially read "one person,
            // everywhere" here, so a sunburst flagging every wedge at-risk is noise rather than signal — say so
            // plainly instead.
            sb.Append("  <div class=\"chart-panel\">\n");
            sb.Append("    <p class=\"gi-solo-repo-note\">Single-maintainer project — one person has authored everything analyzed here, so a per-file ownership breakdown would flag every file as a bus-factor risk without adding any new information.</p>\n");
            sb.Append("  </div>\n");
            sb.Append("</section>\n\n");
            return;
        }

        // Mode selector + contextual controls. They ride INSIDE the component's own hidden control bar, so they
        // inherit its reveal-on-successful-mount handshake rather than carrying a second one — a no-JS visitor
        // never sees an inert control either way. `data-hierarchy-dimension` publishes which dimension is active;
        // the two `data-hierarchy-arg` inputs are the runtime arguments for the two dimensions owner decision D1
        // says cannot be precomputed. The contributor list is populated by the component from the ALPHABETICAL
        // UNION of every node's own roster — never a top-N ranking (FR-10).
        var controls = new StringBuilder();
        controls.Append("    <div class=\"ownership-controls\">\n");
        controls.Append("      <label class=\"ownership-controls-label\">Color by\n");
        controls.Append("        <select class=\"ownership-mode-select\" data-hierarchy-dimension aria-label=\"Color the ownership chart by\">\n");
        controls.Append("          <option value=\"share\" selected>Dominant-author share</option>\n");
        controls.Append("          <option value=\"top\">Top contributors</option>\n");
        controls.Append("          <option value=\"spotlight\">One contributor's work</option>\n");
        controls.Append("          <option value=\"staleness\">Staleness (no current contributor)</option>\n");
        controls.Append("        </select>\n      </label>\n");
        controls.Append($"      <label class=\"ownership-author-wrap\" data-hierarchy-arg-wrap=\"{HierarchyDimensionArg.Roster}\" hidden>Contributor\n");
        controls.Append($"        <select class=\"ownership-author-select\" data-hierarchy-arg=\"{HierarchyDimensionArg.Roster}\" aria-label=\"Spotlight a contributor\"></select>\n");
        controls.Append("      </label>\n");
        controls.Append($"      <label class=\"ownership-threshold-wrap\" data-hierarchy-arg-wrap=\"{HierarchyDimensionArg.Threshold}\" hidden>Stale after (months)\n");
        controls.Append($"        <input type=\"number\" class=\"ownership-threshold-input\" data-hierarchy-arg=\"{HierarchyDimensionArg.Threshold}\" min=\"1\" max=\"60\" value=\"6\">\n");
        controls.Append("      </label>\n");
        controls.Append("    </div>\n");

        // ONE shared legend area, four mode-specific blocks — ROUTED through the component's framing block rather
        // than rewritten (Story 20.9 Task 1.6). The component shows exactly the one the active dimension owns, so
        // the visible legend can never disagree with what is coloured. The bar is hidden until a successful mount:
        // with JS off this page now has no chart at all, and a legend for a chart nobody can see is chrome for
        // nothing — the twin below is what carries the information.
        var legend = new StringBuilder();
        legend.Append("    <div class=\"ss-hierarchy-legends\" hidden>\n");
        legend.Append(Charts.OwnershipLegend(files, " data-hierarchy-legend=\"share\""));
        legend.Append(Charts.OwnershipTopAuthorsLegend(topAuthors, " data-hierarchy-legend=\"top\""));
        legend.Append(Charts.OwnershipSpotlightLegend(" data-hierarchy-legend=\"spotlight\""));
        legend.Append(Charts.OwnershipStalenessLegend(" data-hierarchy-legend=\"staleness\""));
        legend.Append("    </div>\n");

        // Detail cap (the same MaxDetailedCodeMapFiles discipline the Code Map applies, [Review][Patch]
        // 2026-07-22): computed once here from the SAME file list the chart renders from, so a large repo cannot
        // reintroduce the per-node HTML bloat that cap exists to prevent.
        var detailedFiles = Charts.SelectDetailedCodeMapFiles(files, codeMap.FileCount);

        // The panel-wide constants (Task 1.2). `asof` is the tree's most-recent commit day — the staleness and
        // spotlight rules' fixed "now", generation-time computed, NEVER wall-clock (FR31).
        var constants = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HierarchyExplorer.ConstantTopAuthors] = Charts.BuildTopAuthorsJson(topAuthors),
        };
        if (MostRecentCommitDay(files) is { } asOf)
        {
            constants[HierarchyExplorer.ConstantAsOf] = asOf.DayNumber.ToString(CultureInfo.InvariantCulture);
        }

        var config = new HierarchyExplorerConfig(
            DomId: "ownership-explorer",
            // Story 20.7 D2: selector ordering is fixed site-wide, the DEFAULT shape stays per-instance. This
            // surface's shipped default was the sunburst.
            Shape: "sunburst",
            Mode: HierarchyMode.Navigate,
            HashKey: "own",
            Size: OwnershipExplorerSize,
            Labels: true,
            Meta: new Charts.ChartMeta(
                "Ownership by file",
                Window: $"{codeMap.FileCount:N0} {Charts.Plural(codeMap.FileCount, "file", "files")} · {codeMapContributorCount:N0} {Charts.Plural(codeMapContributorCount, "contributor", "contributors")}"),
            // Owner decision D3: the component's own generic nested twin. NOT a per-directory rollup — that would
            // be shorter and more readable but would not enumerate every node the chart draws, failing ADR 0013
            // §2's completeness predicate, which is the entire reason this twin exists. And NOT a restored ranked
            // table: Story 7.11 deleted both prior ownership tables on owner feedback, and this does not
            // re-litigate that.
            TwinDisplay: HierarchyTwinDisplay.Details,
            Dimensions: HierarchyExplorer.OwnershipDimensions(),
            Constants: constants);

        var model = HierarchyExplorer.ProjectOwnership(
            codeMap.Roots, topAuthors, config, fileHref, detailedFiles);

        sb.Append(HierarchyExplorer.Render(
            model, "chart-panel ownership-panel", " data-explorer", controls.ToString(), legend.ToString()));

        sb.Append("</section>\n\n");
    }

    /// <summary>The explorer's configured size — applied by the component as a HEIGHT capped to its own width.
    /// The retired SVG's 480 was a genuine square for a chart that neither labelled nor drilled; raised modestly
    /// because in-sector labels need room, and verified live rather than ported on faith (Story 20.9 F5,
    /// Open Question #3).</summary>
    private const int OwnershipExplorerSize = 560;

    /// <summary>The whole-tree "as of" day: the most recent commit date across every file in the map. This is the
    /// staleness and spotlight rules' fixed "now", exactly as the retired SVG root's <c>data-asof</c> was — a
    /// generation-time value, never wall-clock, so a regenerated portal colours a file the same way tomorrow as it
    /// did today (FR31).</summary>
    private static DateOnly? MostRecentCommitDay(IReadOnlyList<CodeMapNode> files)
    {
        DateOnly? most = null;
        foreach (var f in files)
        {
            if (f.Metrics?.LastDate is not { } d) continue;
            if (most is null || d > most) most = d;
        }
        return most;
    }

    /// <summary>Activity over time — the existing accessible commit heatmap, reused rather than a parallel
    /// time chart. Its active-day cells already link to the generated <c>commits/{date}.html</c> pages (and
    /// this page sits at the output root, the same place the heatmap's root-relative hrefs assume), so the
    /// "select an entry → navigate to detail" contract holds with zero new link plumbing. The headline figures
    /// are derived from the SAME series the heatmap renders (<paramref name="git"/>'s <c>DailySeries</c>/
    /// <c>CommitsByDay</c>) — never from <c>insights.Activity</c>'s separately-bounded deep-git window — so the
    /// sentence can never disagree with the chart directly below it. Falls back to the deep window's activity
    /// series only when no baseline pulse is available at all. [Review fix 2026-07-09]</summary>
    private static void AppendActivitySection(StringBuilder sb, GitInsightsData insights, GitPulse? git, DateOnly? today)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <div class=\"chart-frame-head\"><h2>Activity Over Time</h2></div>\n");
        var windowDays = git?.DailySeries.Count ?? insights.Activity.Count;
        var windowCommits = git is not null ? git.DailySeries.Sum(d => d.Count) : insights.Activity.Sum(a => a.Count);
        // Numeric window + framing via shared slots (Story 10.2); heatmap builder carries its own grid-span window.
        sb.Append(Charts.FrameWindowSlot($"{N(windowCommits)} {Charts.Plural(windowCommits, "commit", "commits")} across {N(windowDays)} active {Charts.Plural(windowDays, "day", "days")}"));
        sb.Append("\n");
        sb.Append(Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.ActivityCadence)));
        sb.Append("  <div class=\"chart-panel\">\n");
        if (git is not null && git.DailySeries.Count > 0)
        {
            sb.Append(Charts.CommitHeatmap(git.DailySeries, git.CommitsByDay, today: today));
        }
        else
        {
            sb.Append("    <div class=\"chart-empty\">No activity data available.</div>\n");
        }
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>Invariant integer formatting — derived numbers must read identically regardless of host
    /// culture (the same invariant-formatting discipline the date helpers in <see cref="Charts"/> follow).</summary>
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The "top N of M files by commit count" truncated-ranking fact, shared by the header meta-pill
    /// and the Files &amp; Contributors ranking caption so the two can never independently drift (Story 10.2 AC2
    /// review — they previously duplicated this computation with inconsistent capitalization). Only the leading
    /// case differs: capitalized for the standalone frame caption, lowercase for the mid-sentence pill.</summary>
    private static string TruncatedFilesRankingFact(int shown, int total, bool capitalized) =>
        $"{(capitalized ? "Top" : "top")} {N(shown)} of {N(total)} files by commit count";
}
