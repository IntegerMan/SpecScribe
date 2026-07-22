using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>risk-quadrant.html</c> page — the refactor-target risk quadrant (Story
/// 7.10, review pass): files plotted by size x churn frequency (<see cref="Charts.RiskQuadrant"/>), with a
/// paginated grid of the text-equivalent elevated-risk ranked list below it. Split out of the Code Map page onto
/// its own Insights nav entry (owner feedback: the chart was getting buried at the bottom of a long page).
/// Reuses the same synthesized-page shell every standalone insight page uses (<see cref="DeepAnalyticsTemplater"/>,
/// <see cref="CodeMapTemplater"/>) rather than <see cref="HtmlTemplater.RenderPage"/>. Fully correct with
/// JavaScript OFF: every elevated-risk file renders as a plain, ranked, guarded-linked list item; only the
/// Prev/Next PAGINATION of that already-complete list is a scoped JS enhancement (emitted <c>hidden</c>, revealed
/// by the script only once there's more than one page's worth). [Story 7.10]</summary>
public static class RiskQuadrantTemplater
{
    /// <summary>Renders the whole page from the unfiltered <see cref="CodeMap"/> (the same "full" set the Code
    /// Map page's headline stats + this story's chart already use — no per-filter-variant duplication).
    /// <paramref name="fileHref"/> is the guarded in-portal code-page resolver (Story 7.2 seam): a non-null
    /// return routes a file/point to its code page, a null return (or a null resolver) leaves it plain — never a
    /// broken link.</summary>
    public static string RenderPage(CodeMap map, SiteNav nav, Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.RiskQuadrantOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — risk-quadrant.html is at the output root.
        var files = map.Files();

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Risk Quadrant — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Refactor-target risk quadrant for {nav.SiteTitle} — files plotted by size and change frequency, with the high-churn, high-size quadrant flagged and ranked as a text-equivalent list."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildInsightsLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Risk Quadrant", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Refactor-Target Risk Quadrant</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; files plotted by size and change frequency</p>\n\n");

        var body = new StringBuilder();
        body.Append(Charts.RiskQuadrant(files, fileHref: fileHref));

        // Always framed — the title/why sentence render whether the chart is a live scatter or its below-
        // threshold empty state, mirroring DeepAnalyticsTemplater's framed CouplingGraph.
        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Size vs. Change Frequency",
                Why: Charts.WhyText(Charts.ChartMetric.RefactorRisk)),
            body.ToString()));

        AppendElevatedGrid(sb, Charts.RiskQuadrantElevatedFiles(files), fileHref);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The mandatory text equivalent of the chart's shaded quadrant (this project pairs every chart with
    /// a text/table equivalent, never an SVG-only signal): every elevated-risk file, ranked by change frequency,
    /// as a paginated grid (owner feedback: a plain <c>&lt;ol&gt;</c> read as a long, undifferentiated list). The
    /// FULL ranked set always renders in the markup — pagination is a client-side reveal/hide over an already-
    /// complete list (see <c>initRiskGridPager</c> in specscribe.js), never a truncation, so a no-JS visitor (or
    /// search engine, or screen reader ignoring the hidden pager) still sees every file in order. An empty
    /// quadrant says so plainly rather than omitting the section silently.</summary>
    private static void AppendElevatedGrid(StringBuilder sb, IReadOnlyList<CodeMapNode> elevated, Func<string, string?>? fileHref)
    {
        sb.Append("<section class=\"chart-panel\">\n");
        sb.Append("  <h3>Elevated-Risk Files</h3>\n");

        if (elevated.Count == 0)
        {
            sb.Append("  <p class=\"risk-quadrant-empty\">No files currently fall in the high-churn, high-size quadrant.</p>\n");
            sb.Append("</section>\n\n");
            return;
        }

        sb.Append($"  <p class=\"chart-lead\">{elevated.Count.ToString("N0", CultureInfo.InvariantCulture)} {Charts.Plural(elevated.Count, "file is", "files are")} both large and frequently changed, ranked by change frequency.</p>\n");

        sb.Append("  <ol class=\"risk-grid\" data-page-size=\"12\">\n");
        foreach (var file in elevated)
        {
            var href = fileHref?.Invoke(file.RepoRelativePath);
            var pathCell = href is { Length: > 0 } target
                ? $"<a href=\"{PathUtil.Html(target)}\">{PathUtil.Html(file.RepoRelativePath)}</a>"
                : PathUtil.Html(file.RepoRelativePath);
            var lines = file.Lines.ToString("N0", CultureInfo.InvariantCulture);
            var changes = (file.Metrics?.Changes ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            var churn = file.Metrics is { } m ? m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture) : "—";

            sb.Append("    <li class=\"risk-grid-item\">\n");
            sb.Append($"      <span class=\"risk-grid-path\">{pathCell}</span>\n");
            sb.Append($"      <span class=\"risk-grid-meta\">{lines} lines &middot; {changes} changes &middot; {churn} churn</span>\n");
            sb.Append("    </li>\n");
        }
        sb.Append("  </ol>\n");

        // The pager sits AFTER the grid (review-pass owner feedback: controls belong at the bottom of the list
        // they page, not floating above it) and is emitted hidden — with JS off it never appears and the grid
        // above stands as the complete, correctly-ordered no-JS truth. [Subtask: progressive-enhancement pagination]
        sb.Append("  <div class=\"risk-pager\" hidden>\n");
        sb.Append("    <button type=\"button\" class=\"risk-pager-prev\" aria-label=\"Previous page of elevated-risk files\">&lsaquo; Prev</button>\n");
        sb.Append("    <span class=\"risk-pager-status\" aria-live=\"polite\"></span>\n");
        sb.Append("    <button type=\"button\" class=\"risk-pager-next\" aria-label=\"Next page of elevated-risk files\">Next &rsaquo;</button>\n");
        sb.Append("  </div>\n");

        sb.Append("</section>\n\n");
    }
}
