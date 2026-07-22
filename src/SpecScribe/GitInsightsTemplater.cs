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
/// <para>Progressive enhancement contract (NFR-5, reinterpreted by ADR 0010 for this opt-in surface): a real,
/// useful default-mode chart (dominant-author share %) plus its full text-equivalent tree render and work with
/// JS off; JS only adds the live mode selector (top contributors / individual-author spotlight / staleness
/// threshold) on top — see <c>specscribe.js</c>'s <c>initOwnershipSunburst</c>. Author information stays
/// descriptive attribution in every mode, never a cross-repo ranked scoreboard (FR-10). Outgoing file links are
/// guarded on target existence via the <c>fileHref</c> resolver (Stories 7.1/7.4 seam): no resolver or no
/// target → plain escaped text, never a dead link. [Story 3.8; Story 7.11 rewrite]</para></summary>
public static class GitInsightsTemplater
{
    public static string RenderPage(
        GitInsightsData insights,
        GitPulse? git,
        SiteNav nav,
        CodeMap codeMap,
        IReadOnlyList<string> topAuthors,
        Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.GitInsightsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — git-insights.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Git Insights — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Aggregate git insights for {nav.SiteTitle}: code ownership concentration and activity over time."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildInsightsLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Git Insights", null),
        }));

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

        AppendActivitySection(sb, insights, git);
        AppendOwnershipSection(sb, insights, codeMap, topAuthors, fileHref);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Code ownership &amp; bus-factor (Story 7.11 rewrite — replaces the earlier files-and-contributors
    /// master-detail table AND the earlier plain ranked ownership table; see this file's Change Log): a
    /// whole-tree, interactive sunburst/treemap TOGGLE (owner feedback — mirrors Story 7.12's own Sunburst/
    /// Treemap pure-CSS radio toggle rather than showing the sunburst with a second view stacked below it) over
    /// EVERY source file (not a top-N subset — <paramref name="codeMap"/> comes from the same uncapped
    /// <see cref="CodeMap.Build"/> walk the Code Map and Risk Quadrant pages use), pre-colored server-side by
    /// dominant-author commit share % (the required no-JS default, ADR 0010) with a client-side mode selector for
    /// top contributors / an individual-author spotlight / a configurable staleness threshold that recolors
    /// WHICHEVER view is toggled visible (<c>specscribe.js</c>'s <c>initOwnershipSunburst</c> queries both
    /// <c>.ownership-wedge</c> and <c>.ownership-cell</c>). The SAME per-file data is also available as a
    /// collapsed, no-JS text-equivalent tree (<see cref="Charts.CodeOwnershipTree"/> — AC #3's accessible
    /// fallback, a secondary disclosure below the toggle rather than a third permanently-visible view). In a
    /// solo-maintainer repo (<c>insights.ContributorCount == 1</c>) every mode would trivially read "one person,
    /// everywhere", so the section reframes honestly instead (AC #4, NFR8).</summary>
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

        if (insights.ContributorCount == 1)
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

        sb.Append("  <div class=\"chart-panel ownership-panel\">\n");

        // Mode selector + contextual controls: hidden in the server HTML so no-JS never ships an inert control
        // (mirrors the Code Map colorize dropdown's own reveal-on-enhance pattern), populated/wired by
        // specscribe.js's initOwnershipSunburst (ADR 0010, Story 7.11 Task 4).
        sb.Append("    <div class=\"ownership-controls\" hidden>\n");
        sb.Append("      <label class=\"ownership-controls-label\">Color by\n");
        sb.Append("        <select class=\"ownership-mode-select\" aria-label=\"Color the ownership sunburst by\">\n");
        sb.Append("          <option value=\"share\" selected>Dominant-author share</option>\n");
        sb.Append("          <option value=\"top\">Top contributors</option>\n");
        sb.Append("          <option value=\"spotlight\">One contributor's work</option>\n");
        sb.Append("          <option value=\"staleness\">Staleness (no current contributor)</option>\n");
        sb.Append("        </select>\n      </label>\n");
        sb.Append("      <label class=\"ownership-author-wrap\" hidden>Contributor\n");
        sb.Append("        <select class=\"ownership-author-select\" aria-label=\"Spotlight a contributor\"></select>\n");
        sb.Append("      </label>\n");
        sb.Append("      <label class=\"ownership-threshold-wrap\" hidden>Stale after (months)\n");
        sb.Append("        <input type=\"number\" class=\"ownership-threshold-input\" min=\"1\" max=\"60\" value=\"6\">\n");
        sb.Append("      </label>\n");
        sb.Append("    </div>\n");

        // Sunburst/Treemap view toggle: a pure-CSS radio pair mirroring the sprint board's .board-tabs component
        // and Story 7.12's own Code Freshness toggle exactly — both views render from the SAME codeMap/topAuthors
        // so either is instantly available with no re-fetch, and the live mode switcher recolors both together.
        sb.Append("    <div class=\"board-tabs\">\n");
        sb.Append("      <input type=\"radio\" id=\"ownership-view-sunburst\" name=\"ownership-view\" class=\"board-tab-radio\" checked>\n");
        sb.Append("      <input type=\"radio\" id=\"ownership-view-treemap\" name=\"ownership-view\" class=\"board-tab-radio ownership-treemap-radio\">\n");
        sb.Append("      <div class=\"board-tabbar\">\n");
        sb.Append("        <label for=\"ownership-view-sunburst\" class=\"board-tab\">Sunburst</label>\n");
        sb.Append("        <label for=\"ownership-view-treemap\" class=\"board-tab\">Treemap</label>\n");
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");

        sb.Append("    <div class=\"ownership-view ownership-view-sunburst\">\n");
        sb.Append("      <div class=\"ownership-sunburst-wrap\">\n");
        sb.Append("        ").Append(Charts.CodeOwnershipSunburst(codeMap.Roots, topAuthors, fileHref: fileHref));
        sb.Append(Charts.OwnershipLegend(codeMap.Files()));
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");
        sb.Append("    <div class=\"ownership-view ownership-view-treemap\">\n");
        sb.Append("      <div class=\"ownership-treemap-wrap\">\n");
        sb.Append("        ").Append(Charts.CodeOwnershipTreemap(codeMap.Layout(), topAuthors, fileHref: fileHref));
        sb.Append(Charts.OwnershipLegend(codeMap.Files()));
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");

        // Accessible text-equivalent (AC #3): the SAME per-file data the sunburst/treemap's wedges/cells carry,
        // as a collapsed directory-tree disclosure below the toggle — a fallback a reader can open, not a third
        // permanently-visible view competing with the two chart forms (owner feedback).
        sb.Append("    <details class=\"ownership-tree-details\">\n");
        sb.Append("      <summary>Full file list (every file's dominant author, share, contributors, last active)</summary>\n");
        sb.Append(Charts.CodeOwnershipTree(codeMap.Roots, fileHref));
        sb.Append("    </details>\n");

        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>Activity over time — the existing accessible commit heatmap, reused rather than a parallel
    /// time chart. Its active-day cells already link to the generated <c>commits/{date}.html</c> pages (and
    /// this page sits at the output root, the same place the heatmap's root-relative hrefs assume), so the
    /// "select an entry → navigate to detail" contract holds with zero new link plumbing. The headline figures
    /// are derived from the SAME series the heatmap renders (<paramref name="git"/>'s <c>DailySeries</c>/
    /// <c>CommitsByDay</c>) — never from <c>insights.Activity</c>'s separately-bounded deep-git window — so the
    /// sentence can never disagree with the chart directly below it. Falls back to the deep window's activity
    /// series only when no baseline pulse is available at all. [Review fix 2026-07-09]</summary>
    private static void AppendActivitySection(StringBuilder sb, GitInsightsData insights, GitPulse? git)
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
            sb.Append(Charts.CommitHeatmap(git.DailySeries, git.CommitsByDay));
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
