using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>impact-map.html</c> page — the planning ↔ code impact map (Story 21.3):
/// for each epic, the set of code files its commits actually touched, correlated best-effort from commit-message
/// and merge-branch naming (<see cref="Charts.ImpactMapBody"/>). Framed with the mandatory Story 10.2 why sentence
/// and an honest "N of M analyzed commits correlated" ranking caption, plus the <see cref="Charts.PlanningCodeImpactNote"/>
/// provenance caveat in the frame's Note slot. Mirrors the same synthesized-page shell every standalone insight/
/// delivery page uses (<see cref="TraceabilityTemplater"/> is the freshest sibling precedent) rather than
/// <see cref="HtmlTemplater.RenderPage"/>. Rides the Delivery nav group's local-context band. [Story 21.3]</summary>
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
            $"Planning-to-code impact map for {nav.SiteTitle} — which code areas each epic's commits actually touched, correlated best-effort from commit and branch naming."));
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
                Title: "Code Areas Touched by Epic",
                Ranking: ranking,
                Why: Charts.WhyText(Charts.ChartMetric.PlanningCodeImpact),
                Note: Charts.PlanningCodeImpactNote),
            Charts.ImpactMapBody(epics, data, prefix)));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
