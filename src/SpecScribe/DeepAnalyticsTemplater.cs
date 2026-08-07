using System.Globalization;
using System.Linq;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the opt-in <c>deep-analytics.html</c> page — the dedicated home for the deeper git signals
/// (FR-10) that used to be a cramped dashboard panel: a change-coupling node-link graph with room to breathe,
/// its precise text companion, and the file hotspots. A synthesized page (no markdown source), so it builds its
/// own shell the way <see cref="CommitDayTemplater"/> does rather than going through
/// <see cref="HtmlTemplater.RenderPage"/>. Generated only when <c>--deep-git</c> produced data; the dashboard's
/// Git Pulse panel links here. Pure HTML/CSS + inline SVG — no JS, matching the chart convention.
/// Panel chrome (title/window/ranking/why) comes from <see cref="Charts.Framed"/> by construction (Story 10.2).
/// [Story 3.2; Story 10.2]</summary>
public static class DeepAnalyticsTemplater
{
    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract. Story 23.4
    /// moved every standalone templater onto it so the IR's content region can be COMPOSED
    /// (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup + wayfinding + body) instead of sliced back
    /// out of a rendered full page. <see cref="RenderPage"/> is the unchanged HTML projection of this same model,
    /// so the bytes are identical.
    /// <para>⚠️ <b>This page's body deliberately extends PAST <c>&lt;/main&gt;</c>.</b> The <c>:target</c> zoom
    /// lightbox is a sibling of the landmark, not a child of it, and the old
    /// <c>SpaDelivery.ExtractContentRegion</c> slice truncated at <c>&lt;/main&gt;</c> — so the "Expand" link
    /// resolved to nothing on every non-HTML surface. Composing the region from this <see cref="PageView"/> FIXES
    /// that inherited capture defect; it is a documented, attributed non-zero parity delta, not a regression.
    /// [Story 23.4 AC #1, AC #3]</para></summary>
    public static PageView BuildPage(DeepGitPulse deep, SiteNav nav, Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.DeepAnalyticsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);
        var window = AnalyzedWindow(deep);

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"deep-page\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Deep Analytics &middot; opt-in</div>\n");
        sb.Append("  <h1>Deep Git Analytics</h1>\n");
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill\">{deep.Coupling.Count} coupled {Charts.Plural(deep.Coupling.Count, "pair", "pairs")}</span>\n");
        sb.Append($"    <span class=\"pill\">{deep.Hotspots.Count} {Charts.Plural(deep.Hotspots.Count, "hotspot", "hotspots")}</span>\n");
        if (deep.AnalyzedCommits > 0)
        {
            sb.Append($"    <span class=\"pill\">{N(deep.AnalyzedCommits)} {Charts.Plural(deep.AnalyzedCommits, "commit", "commits")} analyzed</span>\n");
        }
        sb.Append("  </div>\n</header>\n\n");

        // Change Coupling — graph is the centerpiece; chrome via Charts.Framed (Story 10.2). Chart-intrinsic
        // node/edge legend stays in the body; the old deep-page-lead framing moved into Charts.WhyText.
        var hasCoupling = deep.Coupling.Count > 0;
        // Process-vs-code coupling (Story 10.6, AC1): shown only when it applies, so a purely code-coupled
        // project never sees copy about a case that doesn't exist for it.
        // Read off the pair — classified once upstream, never re-derived per view (AC #2). [Story 24.1 code review]
        var hasProcessPairs = deep.Coupling.Any(p => p.Kind == GitMetrics.CouplingKind.Process);
        var couplingBody = new StringBuilder();
        if (hasCoupling)
        {
            couplingBody.Append("    <a class=\"coupling-expand\" href=\"#coupling-zoom\" aria-label=\"Expand the change-coupling graph\">&#10530; Expand</a>\n");
        }
        couplingBody.Append(Charts.CouplingGraph(deep.Coupling, fileHref: fileHref));
        if (hasCoupling)
        {
            var legend = "Node size = how often a file is coupled &middot; link thickness = how many commits changed the two files together.";
            if (hasProcessPairs)
            {
                legend = "Node size = how often a file is coupled &middot; link thickness = how many commits changed the two files together &middot; dashed lines mark process-coupling.";
            }
            couplingBody.Append($"    <p class=\"coupling-legend\">{legend}</p>\n");
        }
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Change Coupling",
                Window: window,
                Note: hasProcessPairs ? Charts.ProcessCouplingNote : null,
                Why: Charts.WhyText(Charts.ChartMetric.ChangeCoupling)),
            couplingBody.ToString(),
            panelClass: "chart-panel deep-page-graph-panel"));
        sb.Append("</section>\n\n");

        // Lower row — ranked pairs + hotspots, both framed.
        sb.Append("<section class=\"deep-page-section\">\n");
        sb.Append("  <div class=\"deep-page-lower\">\n");

        // Story 24.1: the ranked table reads the DIRECTED view (confidence-ranked) while the graph above stays on
        // the symmetric shared-commit pairs, so the caption must name which ranking THIS panel used — the two are
        // deliberately different populations, and a caption inherited from the graph's would misdescribe the rows.
        // The ranking caption lives here in ChartMeta.Ranking, never in the shared WhyText (Story 10.2).
        var directed = deep.DirectedCoupling;
        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Ranked Pairs",
                Window: window,
                Ranking: directed.Count > 0
                    ? $"Top {N(directed.Count)} directed {Charts.Plural(directed.Count, "couple", "couples")} by confidence — how often the first file's changes bring the second along"
                    : null,
                Why: Charts.WhyText(Charts.ChartMetric.ChangeCoupling)),
            Charts.CouplingTable(directed, fileHref),
            panelClass: "chart-panel deep-page-list-panel"));

        sb.Append(Charts.Framed(
            new Charts.ChartMeta(
                Title: "Git Hotspots",
                Window: window,
                Ranking: HotspotRanking(deep),
                Why: Charts.WhyText(Charts.ChartMetric.FileChurn)),
            Charts.HotspotBars(deep.Hotspots, fileHref),
            panelClass: "chart-panel deep-page-list-panel"));

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
            sb.Append(Charts.CouplingGraph(deep.Coupling, fileHref: fileHref));
            sb.Append("  </div>\n");
            sb.Append("</div>\n\n");
        }

        return new PageView
        {
            Kind = PageKind.DeepAnalytics,
            OutputRelativePath = outputPath,
            Title = $"Deep Git Analytics — {nav.SiteTitle}",
            MetaDescription = $"Deeper git insights for {nav.SiteTitle}: change coupling between files and the repository's change hotspots.",
            Nav = nav.ToNavigationView(outputPath, nav.BuildInsightsLocalContext(outputPath)),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                ("Deep Analytics", null),
            }),
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    private static string? AnalyzedWindow(DeepGitPulse deep) =>
        deep.AnalyzedCommits > 0
            ? $"Last {N(deep.AnalyzedCommits)} commits"
            : null;

    private static string? HotspotRanking(DeepGitPulse deep)
    {
        if (deep.Hotspots.Count == 0) return null;
        var n = deep.Hotspots.Count;
        var total = deep.Insights?.TotalFilesTouched ?? 0;
        return total > n
            ? $"Top {N(n)} of {N(total)} files by change count"
            : $"Top {N(n)} files by change count";
    }

    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
