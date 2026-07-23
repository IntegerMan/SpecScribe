using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>traceability.html</c> page — the full requirement × covering-epic
/// traceability matrix (<see cref="Charts.TraceabilityMatrix"/>), framed with the mandatory Story 10.2 legend/
/// why sentence and a covered-of-total ranking caption sourced from the single-source
/// <see cref="ProjectCounts.RequirementSatisfaction"/> ledger (Story 8.3 — never a local recount). Mirrors the
/// same synthesized-page shell every standalone insight page uses (<see cref="RiskQuadrantTemplater"/> is the
/// freshest precedent) rather than <see cref="HtmlTemplater.RenderPage"/>. [Story 21.1]</summary>
public static class TraceabilityTemplater
{
    public static string RenderPage(RequirementsModel requirements, EpicsModel epics, SiteNav nav, ProjectCounts counts)
    {
        var outputPath = SiteNav.TraceabilityOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — traceability.html is at the output root.
        var sat = counts.RequirementsOverall;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Traceability — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Requirement-to-epic traceability matrix for {nav.SiteTitle} — every FR, NFR, and UX design requirement plotted against the epics that cover it."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildDeliveryLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Traceability", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Requirement Traceability</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; every requirement plotted against its covering epics</p>\n\n");

        var body = Charts.TraceabilityLegend(sat) + Charts.TraceabilityMatrix(requirements, epics, prefix);

        var covered = sat.Satisfied + sat.InFlight;
        var ranking = sat.Total > 0
            ? $"{covered.ToString("N0", CultureInfo.InvariantCulture)} of {sat.Total.ToString("N0", CultureInfo.InvariantCulture)} requirements have a delivering epic &middot; {sat.Deferred.ToString("N0", CultureInfo.InvariantCulture)} deferred &middot; {sat.Unmapped.ToString("N0", CultureInfo.InvariantCulture)} unmapped"
            : null;

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Requirement Coverage Matrix",
                Ranking: ranking,
                Why: Charts.WhyText(Charts.ChartMetric.RequirementTraceability)),
            body));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
