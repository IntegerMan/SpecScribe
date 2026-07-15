using System.Text;

namespace SpecScribe;

/// <summary>The dashboard (home) page BODY rendering, re-homed from <c>HtmlTemplater.RenderIndex</c> +
/// <c>AppendDashboard</c> into the delivery adapter and driven by the host-neutral <see cref="DashboardView"/>
/// (Story 6.2's decomposition of 6.1's opaque <see cref="PageView.BodyHtml"/> for this surface). This is a
/// mechanical RE-HOMING, not a rewrite (same discipline as 6.1's chrome move): the data-shaped sections render
/// from the view's records, the chart/rich panels call the SAME <c>Charts.*</c>/<c>SprintTemplater.*</c> helpers
/// with the view's already-projected domain inputs, and every byte-load-bearing conditional maps to a
/// nullable/optional section — so the produced bytes are unchanged (the golden regression is the gate). [Story 6.2]</summary>
public sealed partial class HtmlRenderAdapter
{
    /// <summary>Renders the full <c>&lt;main&gt;…&lt;/main&gt;</c> dashboard body from its section view model —
    /// the string that becomes <see cref="PageView.BodyHtml"/>. [Story 6.2]</summary>
    public string RenderDashboardBody(DashboardView view, Func<string, string?>? codeItemHref = null)
    {
        var sb = new StringBuilder();

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(view.SiteTitle)}</h1>\n");
        sb.Append("</header>\n\n");

        AppendDashboardSection(sb, view, codeItemHref);

        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    private void AppendDashboardSection(StringBuilder sb, DashboardView view, Func<string, string?>? codeItemHref = null)
    {
        var p = view.Progress;

        sb.Append("<section class=\"dashboard\">\n");

        // Stat headline row — the tiles carry the already-resolved forks.
        sb.Append("<div class=\"stat-grid\">\n");
        foreach (var tile in view.StatTiles)
        {
            sb.Append(Charts.StatCard(tile.Number, tile.Label, tile.Sub, tile.Tooltip));
        }
        sb.Append("</div>\n\n");

        // Summary band directly below the headline stats: the Epic-Status donut + Overall-Progress bars pulled
        // up alongside the Deferred/Retro work cards and the Explore Key Views links, so the at-a-glance status,
        // outstanding follow-up work, and key navigation all sit in one row near the top of the page.
        AppendSummaryBand(sb, view, p);

        // Sunburst — the glance-at-structure scan path — then Now & Next / sprint board.
        // [spec-sprint-epic-filter-and-home-layout]
        if (view.Epics is { } epicsForSunburst)
        {
            sb.Append("<div class=\"chart-panel sunburst-panel\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Project at a Glance</h3>");
            sb.Append("<a class=\"view-epic-link\" href=\"epics.html\">View Epics &amp; Stories &rarr;</a></div>\n");
            sb.Append(Charts.Sunburst(epicsForSunburst, commands: view.Commands));
            sb.Append("</div>\n\n");
        }

        // Now & Next: sprint board when tracked, else the derived cards; omitted entirely when the view is null.
        if (view.NowNext is { } nowNext)
        {
            AppendNowAndNext(sb, nowNext, view.Epics, view.Counts);
        }

        // Story Pipeline funnel — unconditional (the builder owns its empty-state via ProgressModel.Empty).
        sb.Append("<div class=\"chart-panel funnel-panel\">\n<h3>Story Pipeline</h3>\n");
        sb.Append(Charts.RefinementFunnel(p));
        sb.Append("</div>\n\n");

        // Consolidated Git Pulse panel — header links fork on the timeline (baseline, always-available) and
        // deep-git (opt-in); body forks on git presence. Each link is guarded on its target page existing.
        sb.Append("<div class=\"chart-panel git-pulse-panel\">\n");
        if (view.HasTimeline || p.DeepGit is not null)
        {
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Git Pulse</h3><span class=\"git-pulse-header-links\">");
            if (view.HasTimeline)
            {
                sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.TimelineOutputPath}\">View activity timeline &rarr;</a>");
            }
            if (p.DeepGit is not null)
            {
                if (p.DeepGit.Insights is not null)
                {
                    sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.GitInsightsOutputPath}\">View all git insights &rarr;</a>");
                }
                sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.DeepAnalyticsOutputPath}\">View Deep Analytics &rarr;</a>");
            }
            sb.Append("</span></div>\n");
        }
        else
        {
            sb.Append("<h3>Git Pulse</h3>\n");
        }
        sb.Append(p.Git is { } pulse
            ? Charts.GitPulsePanel(pulse, codeItemHref)
            : "<div class=\"chart-empty git-pulse-empty\" data-tooltip=\"Run in a git repository to enable commit stats\" tabindex=\"0\">—</div>\n");
        sb.Append("</div>\n\n");

        // Planning Coverage — only when at least one canonical family was recognized.
        if (view.Coverage is { IsEmpty: false } coverage)
        {
            var coverageToday = DateOnly.FromDateTime(DateTime.Now);
            sb.Append("<div class=\"chart-panel coverage-panel\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Planning Artifacts</h3>");
            sb.Append(Charts.CoverageMeter(coverage, coverageToday));
            sb.Append("</div>\n");
            sb.Append(Charts.ArtifactCoveragePanel(coverage, coverageToday));
            sb.Append("</div>\n\n");
        }

        AppendRequirementsPanel(sb, view.Requirements, view.Epics);

        if (p.PerEpic.Count > 0)
        {
            sb.Append("<div class=\"chart-panel\">\n<h3>Progress by Epic</h3>\n");
            sb.Append(Charts.EpicMosaic(p.PerEpic, e => $"epics/epic-{e.Number}.html"));
            sb.Append("</div>\n\n");
        }

        sb.Append("</section>\n\n");
    }

    /// <summary>The dashboard summary band — a single responsive row placed directly under the headline
    /// stat-grid. It gathers the Epic-Status donut and Overall-Progress bars (previously a mid-page chart-row)
    /// together with the Deferred/Retro follow-up cards and the Explore Key Views quick links, so a reader sees
    /// project status, outstanding work, and primary navigation without scrolling to the bottom of the page.</summary>
    private void AppendSummaryBand(StringBuilder sb, DashboardView view, ProgressModel p)
    {
        sb.Append("<div class=\"dashboard-summary-band\">\n");

        AppendEpicStatusPanel(sb, p, view.Epics);

        sb.Append("<div class=\"chart-panel\">\n<h3>Overall Progress</h3>\n");
        foreach (var bar in view.ProgressBars)
        {
            sb.Append(Charts.ProgressBar(bar.Label, bar.Value, bar.Max, bar.RightLabel));
        }
        sb.Append("</div>\n");

        AppendWorkSummaryCards(sb, view.Work, view.OpenRetroActionItems, view.Counts);
        AppendDashboardQuickLinks(sb, view.QuickLinks);

        sb.Append("</div>\n\n");
    }

    /// <summary>The Epic Status donut. Re-homed from <c>HtmlTemplater.AppendEpicStatusPanel</c> — the donut derives
    /// its segments from the retro-gated <see cref="StatusStyles.ForEpicWithRetrospective"/> roll-up (an all-done
    /// epic with no retrospective reads as "In review", harmonizing with the sunburst/chips/badge surfaces that
    /// already used it), so nothing is re-modelled. [Story 6.2 review: harmonized the epic-status surfaces.]</summary>
    private void AppendEpicStatusPanel(StringBuilder sb, ProgressModel p, EpicsModel? epicsModel)
    {
        sb.Append("<div class=\"chart-panel\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3>Epic Status");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h3></div>\n<div class=\"donut-and-legend\">\n");

        List<(string Label, int Value, string CssClass)> segments;
        string? centerText;
        if (epicsModel is not null && epicsModel.Epics.Count > 0)
        {
            var classes = epicsModel.Epics.Select(StatusStyles.ForEpicWithRetrospective).ToList();
            int Count(string c) => classes.Count(x => x == c);
            segments = StatusStyles.EpicStages
                .Select(stage => (StatusStyles.EpicLabel(stage), Count(stage), stage))
                .ToList();
            centerText = $"{Count("done")}/{classes.Count}";
        }
        else
        {
            segments = new List<(string, int, string)>
            {
                ("Stories drafted", p.EpicsDrafted, "drafted"),
                ("Pending", p.EpicsPending, "pending"),
            };
            centerText = null;
        }

        var nonZero = segments.Where(s => s.Value > 0).ToList();
        var ariaParts = nonZero.Count > 0
            ? string.Join(", ", nonZero.Select(s => $"{s.Value} {s.Label.ToLowerInvariant()}"))
            : "no epics yet";

        sb.Append(Charts.Donut(segments, ariaLabel: $"Epic status: {ariaParts}", centerText: centerText));
        sb.Append(Charts.DonutLegend(nonZero));
        sb.Append("</div>\n</div>\n\n");
    }

    /// <summary>The "Now &amp; Next" panel. Re-homed from <c>HtmlTemplater.AppendNowAndNext</c>: the sprint-board
    /// branch renders from the tracked <see cref="SprintStatus"/> + the dashboard's epics model; the derived
    /// branch renders the pre-computed cards.</summary>
    private void AppendNowAndNext(StringBuilder sb, DashboardNowNext nowNext, EpicsModel? epicsModel, ProjectCounts counts)
    {
        if (nowNext.SprintBoard is { } sprint && epicsModel is not null)
        {
            // One filterable root wraps header + board so the epic dropdown can sit in the header aside
            // (less vertical chrome than a row above the lanes). [spec-sprint-epic-filter-and-home-layout]
            sb.Append("<div class=\"chart-panel sprint-board-panel\">\n");
            sb.Append(SprintTemplater.OpenEpicFilterable(sprint, epicsModel, capPerColumn: 3));
            sb.Append("<div class=\"chart-panel-header-row sprint-board-header\">\n");
            sb.Append("  <h3>Now &amp; Next <span class=\"panel-source-inline\">from sprint-status.yaml</span>");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>\n");
            sb.Append("  <div class=\"sprint-board-header-aside\">");
            sb.Append(SprintTemplater.EpicFilterHostMarkup);
            sb.Append(SprintTemplater.RenderProgressWheel(counts));
            sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.SprintOutputPath}\">View sprint board &rarr;</a>");
            sb.Append("</div>\n</div>\n");
            sb.Append(SprintTemplater.EpicFilterEmptyHintMarkup);
            sb.Append(SprintTemplater.RenderBoard(sprint, epicsModel, capPerColumn: 3, moreHref: SiteNav.SprintOutputPath, wrapWithEpicFilter: false));
            sb.Append(SprintTemplater.CloseEpicFilterable());
            sb.Append("</div>\n\n");
            return;
        }

        sb.Append("<div class=\"chart-panel\">\n<h3>Now &amp; Next");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h3>\n<div class=\"now-next\">\n");
        foreach (var card in nowNext.Cards)
        {
            sb.Append($"  <a class=\"now-next-card {card.CssClass}\" href=\"{PathUtil.Html(card.Href)}\">\n");
            sb.Append($"    <span class=\"now-next-kicker\">{PathUtil.Html(card.Kicker)}</span>\n");
            sb.Append($"    <span class=\"now-next-title\">{PathUtil.Html(card.Title)}</span>\n");
            sb.Append("  </a>\n");
        }
        sb.Append("</div>\n</div>\n\n");
    }

    /// <summary>The dashboard requirements panel. Re-homed from <c>HtmlTemplater.AppendRequirementsPanel</c>.</summary>
    private void AppendRequirementsPanel(StringBuilder sb, RequirementsModel? requirements, EpicsModel? epicsModel)
    {
        if (requirements is null || !requirements.All.Any()) return;

        var grid = Charts.RequirementStatusGrid(requirements.All.ToList(), prefix: string.Empty);

        sb.Append("<div class=\"chart-panel req-panel\">\n");
        if (epicsModel is not null)
        {
            // Two renderings of one dataset are consolidated behind a panel-scoped clone of the sprint
            // board's pure-CSS radio toggle: the coverage flow is the default-visible primary and the
            // status-block grid is the demoted alternate. Both stay in the DOM (the grid is the flow's
            // Story-3.7 accessibility text-twin, never removed). [Story 8.7]
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Requirements");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>");
            sb.Append(RenderRequirementsTabs());
            sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a></div>\n");
            sb.Append("<div class=\"req-view req-view-flow\">");
            sb.Append(Charts.RequirementFlow(requirements, epicsModel));
            sb.Append("</div>\n");
            sb.Append("<div class=\"req-view req-view-grid\">");
            sb.Append(grid);
            sb.Append("</div>\n");
        }
        else
        {
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Requirements");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>");
            sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a></div>\n");
            sb.Append(grid);
        }
        sb.Append("</div>\n\n");
    }

    /// <summary>The pure-CSS view toggle for the requirements panel: a panel-scoped clone of
    /// <c>SprintTemplater.RenderBoardTabs</c> with panel-unique ids/name (<c>rv-flow</c>/<c>rv-grid</c>,
    /// <c>req-view</c>) so it can never collide with the sprint radios. Reuses the generic
    /// <c>.board-tabs/.board-tabbar/.board-tab/.board-tab-radio</c> chrome. [Story 8.7]</summary>
    private static string RenderRequirementsTabs()
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"board-tabs\">");
        sb.Append("<input type=\"radio\" id=\"rv-flow\" name=\"req-view\" class=\"board-tab-radio\" checked>");
        sb.Append("<input type=\"radio\" id=\"rv-grid\" name=\"req-view\" class=\"board-tab-radio\">");
        sb.Append("<div class=\"board-tabbar\">");
        sb.Append("<label for=\"rv-flow\" class=\"board-tab\">Flow</label>");
        sb.Append("<label for=\"rv-grid\" class=\"board-tab\">Status grid</label>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    /// <summary>The "Explore Key Views" quick-link pill row. Re-homed from
    /// <c>HtmlTemplater.AppendDashboardQuickLinks</c>; the family/title helpers are pure label mappings.</summary>
    private void AppendDashboardQuickLinks(StringBuilder sb, IReadOnlyList<NavQuickLink> quickLinks)
    {
        if (quickLinks.Count == 0) return;

        sb.Append("<div class=\"chart-panel dashboard-quick-links\">\n<h3>Explore Key Views</h3>\n");
        sb.Append("<div class=\"quick-link-pills\">\n");
        foreach (var (label, outputPath, description) in quickLinks)
        {
            sb.Append($"  <a class=\"quick-link-pill {QuickLinkFamily(label)}\" href=\"{PathUtil.Html(outputPath)}\" data-tooltip=\"{PathUtil.Html(description)}\">{Icons.ForConcept(label)}{PathUtil.Html(QuickLinkTitle(label))}</a>\n");
        }
        sb.Append("</div>\n</div>\n\n");
    }

    private static string QuickLinkTitle(string label) => label switch
    {
        "Epics" => "Epics & Stories",
        _ => label,
    };

    private static string QuickLinkFamily(string label)
    {
        if (label.Contains("Architecture", StringComparison.OrdinalIgnoreCase) || label.Contains("ADR", StringComparison.OrdinalIgnoreCase))
            return "family-architecture";
        if (label.Equals("Epics", StringComparison.OrdinalIgnoreCase))
            return "family-epics";
        if (label.Contains("Requirement", StringComparison.OrdinalIgnoreCase))
            return "family-requirements";
        return "family-planning";
    }

    /// <summary>The Deferred / Retro follow-up cards for the summary band. Each is a compact "traditional" card
    /// (label + open-count) rather than the old full-width callout bar, so the two sit as tiles alongside the
    /// status/progress panels and the Explore Key Views links. Each card is independently gated: the Deferred card
    /// when there is deferred work, the Retro card when there are open action items; the "Direct changes" stat
    /// tile still carries the quick-dev count. Counts come from the portal-wide ledger. [Story 8.3]</summary>
    private void AppendWorkSummaryCards(StringBuilder sb, WorkInventory work, int openRetro, ProjectCounts counts)
    {
        if (work.Deferred is { } deferred)
        {
            var count = counts.DeferredOpenItems;
            sb.Append($"<a class=\"summary-card work-summary-card deferred\" href=\"{PathUtil.Html(deferred.OutputPath)}\">\n");
            sb.Append($"  <span class=\"summary-card-label\">{Icons.ForConcept("Deferred")}Deferred Work</span>\n");
            sb.Append($"  <span class=\"summary-card-count\">{count} open {Charts.Plural(count, "item", "items")}</span>\n");
            sb.Append("</a>\n");
        }

        if (openRetro > 0)
        {
            sb.Append($"<a class=\"summary-card work-summary-card retro\" href=\"{PathUtil.Html(SiteNav.ActionItemsOutputPath)}\">\n");
            sb.Append($"  <span class=\"summary-card-label\">{Icons.ForConcept("Retrospective")}Retro Action Items</span>\n");
            sb.Append($"  <span class=\"summary-card-count\">{openRetro} open {Charts.Plural(openRetro, "item", "items")}</span>\n");
            sb.Append("</a>\n");
        }
    }
}
