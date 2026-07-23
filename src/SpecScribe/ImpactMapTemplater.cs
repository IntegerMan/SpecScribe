using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SpecScribe;

/// <summary>Renders the standalone <c>impact-map.html</c> page — the planning ↔ code impact map (Story 21.3):
/// an INTERACTIVE treemap of the code files each epic's commits touched, correlated best-effort from commit-message
/// and merge-branch naming. The visitor multi-selects epics; the client script (<c>initImpactMap</c>) merges the
/// selected epics into one shared directory hierarchy and lays out a squarified treemap — tiles SIZED by churn
/// (Σ lines added+deleted) and COLORED by how many attributed commits touched the area. Owner-directed redesign
/// (2026-07-22) of the original static link-list into a weighted, filterable treemap; this is a deliberate,
/// owner-authorized crossing of the project's "pure-SVG, no info-bearing JS" rule (front-running Epic 20's
/// interactive-explorer budget). Fully degrades with JS OFF: the epic-grouped text list below (the accessible
/// text-equivalent + noscript fallback) IS the content, and the interactive controls stay <c>hidden</c> until the
/// script reveals them. Framed with the mandatory Story 10.2 why sentence, an "N of M analyzed commits correlated"
/// ranking, and the <see cref="Charts.PlanningCodeImpactNote"/> caveat. Rides the Delivery nav local-context band.
/// [Story 21.3]</summary>
public static class ImpactMapTemplater
{
    public static string RenderPage(EpicsModel epics, PlanningCodeImpactData data, SiteNav nav)
    {
        var outputPath = SiteNav.ImpactMapOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — impact-map.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Impact Map — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Planning-to-code impact map for {nav.SiteTitle} — an interactive treemap of which code areas each epic's commits touched, sized by churn and colored by commit activity."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildDeliveryLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Impact Map", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Planning &#8596; Code Impact Map</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; the code areas each epic's work actually touched</p>\n\n");

        var ranking = data.TotalAnalyzedCommits > 0
            ? $"{data.AttributedCommitCount.ToString("N0", CultureInfo.InvariantCulture)} of {data.TotalAnalyzedCommits.ToString("N0", CultureInfo.InvariantCulture)} analyzed commits correlated to a story or epic"
            : null;

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Code Areas Touched",
                Ranking: ranking,
                Why: Charts.WhyText(Charts.ChartMetric.PlanningCodeImpact),
                Note: Charts.PlanningCodeImpactNote),
            BuildInteractiveBody(epics, data, prefix)));

        // The epic-grouped text list is the accessible text-equivalent + no-JS fallback (it IS the whole content
        // with the script off). Open by default so a no-JS visitor sees it; the script collapses it once the
        // treemap is live (still one click away). Omitted entirely when there is nothing to list — the Framed
        // body above already renders the honest empty note, and a second copy of the identical message in this
        // wrapper was pure duplication. [Story 21.3; a11y text-twin discipline] [Review][Patch]
        if (data.HasAnyFiles)
        {
            sb.Append("<details class=\"chart-panel impact-fallback\" id=\"impact-fallback\" open>\n");
            sb.Append("  <summary>All touched files, grouped by epic</summary>\n");
            sb.Append(Charts.ImpactMapBody(epics, data, prefix));
            sb.Append("</details>\n\n");
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The framed body: the interactive controls (epic multi-select + legend) the script reveals, the
    /// treemap mount point the script fills, and the embedded JSON payload it reads. All progressive-enhancement —
    /// emitted <c>hidden</c> / empty so a no-JS visitor sees nothing broken here and falls through to the text list.</summary>
    private static string BuildInteractiveBody(EpicsModel epics, PlanningCodeImpactData data, string prefix)
    {
        var attributedEpics = epics.Epics
            .Where(e => data.FilesByEpic.ContainsKey(e.Number))
            .OrderBy(e => e.Number)
            .ToList();

        if (attributedEpics.Count == 0)
        {
            // Deep-git ran but nothing correlated → honest empty note (mirrors ImpactMapBody's own degrade).
            return "<div class=\"chart-empty\">No commits could be correlated to a story or epic yet.</div>\n";
        }

        var sb = new StringBuilder();

        // Controls: an epic multi-select dropdown (the SAME sprint-epic-filter <details> component the sprint board
        // uses), a Treemap|Sunburst view toggle (the board-tabs pure-CSS radio toggle used on the Code Map/Ownership
        // pages), and a size/color legend. Hidden until the script confirms it can drive the shapes (a no-JS visitor
        // never sees dead controls — the risk-quadrant pager's reveal pattern; the text list below is the fallback).
        sb.Append("<div class=\"impact-controls\" hidden>\n");

        // Epic multi-select — reuse the sprint board's dropdown markup + styles so it reads identically. Server-
        // rendered (we know the epic roster at build time) and wired to the treemap/sunburst by the script.
        sb.Append("  <details class=\"sprint-epic-filter impact-epic-filter\">\n");
        sb.Append("    <summary class=\"sprint-epic-filter-summary\" aria-label=\"Choose which epics to include\">\n");
        sb.Append("      <span class=\"sprint-epic-filter-label\">Epics</span>\n");
        sb.Append("      <span class=\"sprint-epic-filter-count\"></span>\n");
        sb.Append("    </summary>\n");
        sb.Append("    <div class=\"sprint-epic-filter-panel\" role=\"group\" aria-label=\"Epics\">\n");
        sb.Append("      <div class=\"impact-select-actions\"><button type=\"button\" class=\"sprint-epic-filter-all impact-select-all\">All</button> <button type=\"button\" class=\"sprint-epic-filter-all impact-select-none\">None</button></div>\n");
        foreach (var epic in attributedEpics)
        {
            var title = PathUtil.Html(PathUtil.StripHtmlTags(epic.Title));
            sb.Append($"      <label class=\"sprint-epic-filter-opt\"><input type=\"checkbox\" class=\"impact-epic-toggle\" value=\"{epic.Number}\" checked> Epic {epic.Number} &middot; {title}</label>\n");
        }
        sb.Append("    </div>\n");
        sb.Append("  </details>\n");

        sb.Append("  <div class=\"impact-legend\">\n");
        sb.Append("    <span class=\"impact-legend-item\"><span class=\"impact-legend-size\"></span> Size = lines changed (churn)</span>\n");
        sb.Append("    <span class=\"impact-legend-item impact-legend-color\">Color = commits touching the area <span class=\"impact-legend-ramp\"><i class=\"impact-level-1\"></i><i class=\"impact-level-2\"></i><i class=\"impact-level-3\"></i><i class=\"impact-level-4\"></i><i class=\"impact-level-5\"></i></span> few &rarr; many</span>\n");
        sb.Append("  </div>\n");
        sb.Append("</div>\n");

        // The two shape mount points, preceded by the Treemap|Sunburst view toggle. The toggle's radios live INSIDE
        // this wrapper so the pure-CSS `:has(:checked)` visibility swap can see them (the Code Map's exact pattern —
        // the radios must be a descendant of the element whose children they show/hide). The script renders an SVG
        // into each mount; empty (with role/aria) without JS. [Story 21.3]
        sb.Append("<div class=\"impact-shapes\">\n");
        sb.Append("  <div class=\"board-tabs impact-shape-tabs\">\n");
        sb.Append("    <input type=\"radio\" id=\"impact-view-treemap\" name=\"impact-view\" class=\"board-tab-radio\" checked>\n");
        sb.Append("    <input type=\"radio\" id=\"impact-view-sunburst\" name=\"impact-view\" class=\"board-tab-radio impact-sunburst-radio\">\n");
        sb.Append("    <div class=\"board-tabbar\">\n");
        sb.Append("      <label for=\"impact-view-treemap\" class=\"board-tab\">Treemap</label>\n");
        sb.Append("      <label for=\"impact-view-sunburst\" class=\"board-tab\">Sunburst</label>\n");
        sb.Append("    </div>\n");
        sb.Append("  </div>\n");
        sb.Append("  <div class=\"impact-shape impact-shape-treemap\" id=\"impact-treemap\" role=\"img\" aria-label=\"Interactive treemap of code files touched, sized by lines changed and colored by commit count. The full list of files by epic is below.\"></div>\n");
        sb.Append("  <div class=\"impact-shape impact-shape-sunburst\" id=\"impact-sunburst\" role=\"img\" aria-label=\"Interactive sunburst of code files touched, arcs sized by lines changed and colored by commit count. The full list of files by epic is below.\"></div>\n");
        sb.Append("</div>\n");

        // The data payload the script reads. System.Text.Json's default encoder escapes <, >, & to \u00xx, so this
        // is safe to embed inside a <script> without breaking on a stray tag-like path. [Story 21.3]
        var payload = new
        {
            epics = attributedEpics.Select(e => new
            {
                n = e.Number,
                f = data.FilesByEpic[e.Number].Select(file => new
                {
                    p = file.Path,
                    h = file.CodePageHref is { Length: > 0 } href ? prefix + href : file.CodePageHref,
                    c = file.Churn,
                    k = file.Commits,
                }),
            }),
        };
        var json = JsonSerializer.Serialize(payload);
        sb.Append($"<script type=\"application/json\" id=\"impact-map-data\">{json}</script>\n");

        return sb.ToString();
    }
}
