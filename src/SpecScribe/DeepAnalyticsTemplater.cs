using System.Text;

namespace SpecScribe;

/// <summary>Renders the opt-in <c>deep-analytics.html</c> page — the dedicated home for the deeper git signals
/// (FR-10) that used to be a cramped dashboard panel: a change-coupling node-link graph with room to breathe,
/// its precise text companion, and the file hotspots. A synthesized page (no markdown source), so it builds its
/// own shell the way <see cref="CommitDayTemplater"/> does rather than going through
/// <see cref="HtmlTemplater.RenderPage"/>. Generated only when <c>--deep-git</c> produced data; the dashboard's
/// Git Pulse panel links here. Pure HTML/CSS + inline SVG — no JS, matching the chart convention. [Story 3.2]</summary>
public static class DeepAnalyticsTemplater
{
    public static string RenderPage(DeepGitPulse deep, SiteNav nav)
    {
        var outputPath = SiteNav.DeepAnalyticsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Deep Git Analytics — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Deeper git insights for {nav.SiteTitle}: change coupling between files and the repository's change hotspots."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Deep Analytics", null),
        }));

        sb.Append("<main id=\"main-content\" class=\"deep-page\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Deep Analytics &middot; opt-in</div>\n");
        sb.Append("  <h1>Deep Git Analytics</h1>\n");
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill\">{deep.Coupling.Count} coupled {Charts.Plural(deep.Coupling.Count, "pair", "pairs")}</span>\n");
        sb.Append($"    <span class=\"pill\">{deep.Hotspots.Count} {Charts.Plural(deep.Hotspots.Count, "hotspot", "hotspots")}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        // Change Coupling — the graph is the centerpiece, given the full content width to breathe. Its exact,
        // screen-reader friendly companion (the ranked table) now sits in the lower row beside the hotspots, so
        // the visualization is never the sole information carrier.
        var hasCoupling = deep.Coupling.Count > 0;
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append("  <h2>Change Coupling</h2>\n");
        sb.Append("  <p class=\"deep-page-lead\">Files that tend to change in the same commits. Thicker links and larger nodes mean the files change together more often — a hint at hidden dependencies worth a second look.</p>\n");
        sb.Append("  <div class=\"chart-panel deep-page-graph-panel\">\n");
        // Expand affordance: a pure-CSS :target lightbox (same mechanism as the commit heatmap's drill-down) —
        // no JS. Only offered when there's actually a graph to enlarge.
        if (hasCoupling)
        {
            sb.Append("    <a class=\"coupling-expand\" href=\"#coupling-zoom\" aria-label=\"Expand the change-coupling graph\">&#10530; Expand</a>\n");
        }
        sb.Append(Charts.CouplingGraph(deep.Coupling));
        if (hasCoupling)
        {
            sb.Append("    <p class=\"coupling-legend\">Node size = how often a file is coupled &middot; link thickness = how many commits changed the two files together.</p>\n");
        }
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");

        // Lower row — the precise text companions side by side: the ranked coupling pairs and the change hotspots.
        // Two equal panels on wide screens, stacked on narrow.
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append("  <div class=\"deep-page-lower\">\n");

        sb.Append("    <div class=\"chart-panel deep-page-list-panel\">\n");
        sb.Append("      <div class=\"deep-page-panel-head\">\n");
        sb.Append("        <h3>Ranked Pairs</h3>\n");
        if (hasCoupling)
        {
            sb.Append($"        <span class=\"deep-page-panel-count\">{deep.Coupling.Count} {Charts.Plural(deep.Coupling.Count, "pair", "pairs")}</span>\n");
        }
        sb.Append("      </div>\n");
        sb.Append("      <p class=\"deep-page-note\">Files that changed together most often, with the number of shared commits.</p>\n");
        sb.Append(Charts.CouplingTable(deep.Coupling));
        sb.Append("    </div>\n");

        sb.Append("    <div class=\"chart-panel deep-page-list-panel\">\n");
        sb.Append("      <div class=\"deep-page-panel-head\">\n");
        sb.Append("        <h3>Git Hotspots</h3>\n");
        if (deep.Hotspots.Count > 0)
        {
            sb.Append($"        <span class=\"deep-page-panel-count\">{deep.Hotspots.Count} {Charts.Plural(deep.Hotspots.Count, "hotspot", "hotspots")}</span>\n");
        }
        sb.Append("      </div>\n");
        sb.Append("      <p class=\"deep-page-note\">The files changed most often across recent history — the parts of the codebase carrying the most churn.</p>\n");
        sb.Append(Charts.HotspotBars(deep.Hotspots));
        sb.Append("    </div>\n");

        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");

        sb.Append("</main>\n\n");

        // Pure-CSS :target lightbox holding an enlarged copy of the same graph — activated by the "Expand" link
        // above, dismissed by clicking the backdrop or the ✕ (both navigate to "#", clearing the :target). No JS,
        // mirroring the commit-heatmap drill-down convention. The graph is the same SVG; its user-unit labels
        // scale up with the larger display size, which is the whole point of the zoom.
        if (hasCoupling)
        {
            sb.Append("<div id=\"coupling-zoom\" class=\"coupling-lightbox\" role=\"dialog\" aria-label=\"Change coupling graph, enlarged\">\n");
            sb.Append("  <a class=\"coupling-lightbox-backdrop\" href=\"#\" aria-label=\"Close enlarged graph\"></a>\n");
            sb.Append("  <div class=\"coupling-lightbox-panel\">\n");
            sb.Append("    <a class=\"coupling-lightbox-close\" href=\"#\" aria-label=\"Close enlarged graph\">&times;</a>\n");
            sb.Append(Charts.CouplingGraph(deep.Coupling));
            sb.Append("  </div>\n");
            sb.Append("</div>\n\n");
        }

        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
