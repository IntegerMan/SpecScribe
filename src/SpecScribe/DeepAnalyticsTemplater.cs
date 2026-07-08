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

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Deep Analytics &middot; opt-in</div>\n");
        sb.Append("  <h1>Deep Git Analytics</h1>\n");
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill\">{deep.Coupling.Count} coupled {Charts.Plural(deep.Coupling.Count, "pair", "pairs")}</span>\n");
        sb.Append($"    <span class=\"pill\">{deep.Hotspots.Count} {Charts.Plural(deep.Hotspots.Count, "hotspot", "hotspots")}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        // Change Coupling — the graph is the centerpiece; the ranked list beside it is the exact, screen-reader
        // friendly companion so the visualization is never the sole information carrier.
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append("  <h2>Change Coupling</h2>\n");
        sb.Append("  <p class=\"deep-page-lead\">Files that tend to change in the same commits. Thicker links and larger nodes mean the files change together more often — a hint at hidden dependencies worth a second look.</p>\n");
        sb.Append("  <div class=\"deep-page-coupling\">\n");
        sb.Append("    <div class=\"chart-panel deep-page-graph-panel\">\n");
        sb.Append(Charts.CouplingGraph(deep.Coupling));
        sb.Append("    </div>\n");
        sb.Append("    <div class=\"chart-panel deep-page-list-panel\">\n");
        sb.Append("      <div class=\"deep-git-title\">Ranked pairs</div>\n");
        sb.Append(Charts.CouplingList(deep.Coupling));
        sb.Append("    </div>\n");
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");

        // Git Hotspots — full width, with room for more entries than the old panel showed.
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append("  <h2>Git Hotspots</h2>\n");
        sb.Append("  <p class=\"deep-page-lead\">The files changed most often across recent history — the parts of the codebase carrying the most churn.</p>\n");
        sb.Append("  <div class=\"chart-panel\">\n");
        sb.Append(Charts.HotspotBars(deep.Hotspots));
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
