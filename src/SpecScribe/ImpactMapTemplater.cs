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
    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — see
    /// <see cref="RiskQuadrantTemplater.BuildPage"/> for why every standalone templater grew one. This page is why
    /// <see cref="AssetManifest.ExtraHead"/> and <see cref="AssetManifest.HierarchyBootInline"/> are separate
    /// flags: its boot marker rides the HEAD, not the pre-body slot. [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(EpicsModel epics, PlanningCodeImpactData data, SiteNav nav)
    {
        var outputPath = SiteNav.ImpactMapOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — impact-map.html is at the output root.

        var ranking = data.TotalAnalyzedCommits > 0
            ? $"{data.AttributedCommitCount.ToString("N0", CultureInfo.InvariantCulture)} of {data.TotalAnalyzedCommits.ToString("N0", CultureInfo.InvariantCulture)} analyzed commits correlated to a story or epic"
            : null;

        var meta = new Charts.ChartMeta(
            Title: "Code Areas Touched",
            Ranking: ranking,
            Why: Charts.WhyText(Charts.ChartMetric.PlanningCodeImpact),
            // Owner decision D4's counting basis, stated in the framing block a reader actually reads. The chart
            // is now grouped BY EPIC, so a file touched by three epics contributes to three subtrees and the
            // totals are attributed churn rather than distinct-file churn. Without this sentence the chart and the
            // epic-grouped list below it appear to disagree about how much changed. [Story 20.7 Task 7.3]
            Note: $"{ImpactAttributionNote} {Charts.PlanningCodeImpactNote}");

        var config = new HierarchyExplorerConfig(
            DomId: HierarchyDomId,
            // Owner decision D2: the SELECTOR ORDERING standardizes site-wide (Sunburst | Treemap), the DEFAULT
            // SHAPE stays per-instance. A deep file tree reads better as rectangles, and demoting that to match
            // the planning surfaces would be a regression dressed as consistency.
            Shape: "treemap",
            Mode: HierarchyMode.Navigate,
            HashKey: "impact",
            Size: HierarchySize,
            Labels: true,
            Meta: meta,
            // The epic-grouped <details> list below IS this surface's twin — Story 20.6 audited it at 993/993 and
            // called it the reference implementation, and 20.6 D1 keeps it rather than replacing it with the
            // component's generic nested list. So the component's own twin is the accessibility-tree copy, not a
            // third visible listing.
            TwinDisplay: HierarchyTwinDisplay.ScreenReaderOnly,
            Filterable: true);

        var model = HierarchyExplorer.ProjectImpactMap(epics, data, prefix, config);
        var explorerHtml = HierarchyExplorer.Render(
            model,
            panelClass: "chart-panel impact-panel",
            panelAttributes: " data-explorer",
            controlsHtml: BuildEpicFilterControls(epics, data),
            // This surface does not speak `--status-*`, so it keeps its own size/colour legend rather than a
            // lifecycle legend that would describe nothing on screen.
            legendHtml: BuildImpactLegend());

        // The document is assembled only now, because the anti-flash boot marker has to be in <head> — it must run
        // while the body is still parsing, which is the only moment it can suppress the swap the reader would
        // otherwise watch. This page builds its own head rather than going through PageView, so the marker rides
        // `extraHead`; the adapter emits the same script on the same terms for every other converted surface.
        var hasChart = HierarchyExplorer.ContainsHost(explorerHtml);

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Planning &#8596; Code Impact Map</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; the code areas each epic's work actually touched</p>\n\n");

        sb.Append(hasChart
            ? explorerHtml
            : Charts.Framed(meta, "<div class=\"chart-empty\">No commits could be correlated to a story or epic yet.</div>\n"));

        // The epic-grouped text list is the accessible text-equivalent + no-JS fallback (it IS the whole content
        // with the script off). Open by default so a no-JS visitor sees it; the script collapses it once the
        // treemap is live (still one click away). Omitted entirely when there is nothing to list — the Framed
        // body above already renders the honest empty note, and a second copy of the identical message in this
        // wrapper was pure duplication. [Story 21.3; a11y text-twin discipline] [Review][Patch]
        if (data.HasAnyFiles)
        {
            // `data-hierarchy-collapse-on-mount`: the component closes this once its chart is live, which is
            // exactly what the retired `initImpactMap` did. `open` in the served HTML is the load-bearing half —
            // a JS-off visitor gets the full listing with no interaction at all (ADR 0013 §2), and Story 20.6
            // audited THIS list at 993/993 as the reference twin, so it is kept rather than replaced.
            sb.Append("<details class=\"chart-panel impact-fallback\" id=\"impact-fallback\" data-hierarchy-collapse-on-mount open>\n");
            sb.Append("  <summary>All touched files, grouped by epic</summary>\n");
            sb.Append(Charts.ImpactMapBody(epics, data, prefix));
            sb.Append("</details>\n\n");
        }

        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Structure,
            OutputRelativePath = outputPath,
            Title = $"Impact Map — {nav.SiteTitle}",
            MetaDescription = $"Planning-to-code impact map for {nav.SiteTitle} — an interactive treemap of which code areas each epic's commits touched, sized by churn and colored by commit activity.",
            Nav = nav.ToNavigationView(outputPath, nav.BuildDeliveryLocalContext(outputPath)),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[] { ("Home", "index.html"), ("Impact Map", null) }),
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
                // The boot marker rides the HEAD here (Story 21.3's convention), never the pre-body slot — hence
                // ExtraHead rather than HierarchyBootInline. The engine <script src> still lands AFTER the body,
                // a local file reference, never a CDN (NFR-3). Both conditional on the block actually carrying a
                // host, so a deep-git-less run that renders the honest empty note ships no engine it cannot use.
                // [Story 20.7 Task 7; Story 23.4 AC #3]
                ExtraHead = hasChart ? HierarchyExplorer.BootScript : null,
                HierarchyEngineNeeded = hasChart,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    /// <summary>DOM id of this page's Hierarchy Explorer instance.</summary>
    internal const string HierarchyDomId = "impact-hierarchy";

    /// <summary>Chart size. Larger than the planning surfaces': this is a file tree, and its treemap default has
    /// far more leaves to label.</summary>
    internal const int HierarchySize = 620;

    /// <summary>Owner decision D4's counting basis, in the reader's own words. See the call site.</summary>
    internal const string ImpactAttributionNote =
        "Grouped by epic, so a file touched by several epics appears under each — totals are attributed churn, not distinct-file churn.";

    /// <summary>The epic multi-select, unchanged in markup from Story 21.3 — the SAME sprint-board dropdown
    /// component, the same classes, the same wording. What changed is only what it drives: it used to feed a
    /// bespoke merge-and-relayout in <c>initImpactMap</c>, and now it feeds the component's generic root-subtree
    /// filter.
    ///
    /// <para>Each checkbox gains <c>data-hierarchy-filter</c> and a <c>value</c> that IS the node id of the epic it
    /// controls. That pairing is the whole contract: the component knows nothing about epics, only that a checked
    /// filter control names a root child to keep. <see cref="HierarchyExplorerConfig.Filterable"/> gates it.</para>
    ///
    /// <para>It rides inside the component's own <c>hidden</c> control bar, so it is revealed by the same
    /// successful mount as the shape selector — a JS-off visitor still never sees a dead control.
    /// [Story 20.7 Task 7.2]</para></summary>
    private static string BuildEpicFilterControls(EpicsModel epics, PlanningCodeImpactData data)
    {
        var attributedEpics = epics.Epics
            .Where(e => data.FilesByEpic.ContainsKey(e.Number))
            .OrderBy(e => e.Number)
            .ToList();
        if (attributedEpics.Count == 0) return string.Empty;

        var sb = new StringBuilder();
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
            sb.Append($"      <label class=\"sprint-epic-filter-opt\"><input type=\"checkbox\" class=\"impact-epic-toggle\" data-hierarchy-filter value=\"epic-{epic.Number}\" checked> Epic {epic.Number} &middot; {title}</label>\n");
        }
        sb.Append("    </div>\n");
        sb.Append("  </details>\n");
        return sb.ToString();
    }

    /// <summary>This surface's own legend — size and the five-level commit ramp — kept verbatim from Story 21.3.
    /// The component's status legend would have described a `--status-*` vocabulary no sector on this page carries.
    /// [Story 20.7 Task 7.3]</summary>
    private static string BuildImpactLegend()
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"impact-legend\">\n");
        sb.Append("  <span class=\"impact-legend-item\"><span class=\"impact-legend-size\"></span> Size = lines changed (churn)</span>\n");
        sb.Append("  <span class=\"impact-legend-item impact-legend-color\">Color = commits touching the area <span class=\"impact-legend-ramp\"><i class=\"impact-level-1\"></i><i class=\"impact-level-2\"></i><i class=\"impact-level-3\"></i><i class=\"impact-level-4\"></i><i class=\"impact-level-5\"></i></span> few &rarr; many</span>\n");
        sb.Append($"  <span class=\"impact-legend-item impact-legend-basis\">{ImpactAttributionNote}</span>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }
}
