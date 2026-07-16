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
        // The project identity now lives in the global dark nav bar (and the journey-grouped menu replaces the old
        // Explore Key Views strip), so the dashboard body carries only a visually-hidden H1 — a screen reader still
        // hears the page's name, without a redundant visible title band eating vertical space above the stats.
        sb.Append($"<h1 class=\"sr-only\">{PathUtil.Html(view.SiteTitle)} — project dashboard</h1>\n");
        AppendDashboardSection(sb, view, codeItemHref);

        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    private void AppendDashboardSection(StringBuilder sb, DashboardView view, Func<string, string?>? codeItemHref = null)
    {
        var p = view.Progress;

        sb.Append("<section class=\"dashboard\">\n");

        // One flex-wrap tile band: the five headline stats plus Epic Status / Overall Progress / Deferred /
        // Action Items — all siblings with shared sizing, with top padding clearing the white key-views bar.
        AppendTileBand(sb, view, p);

        // Sunburst — the glance-at-structure scan path — then Now & Next / sprint board.
        // Segments already navigate; no redundant header CTA. [spec-sprint-epic-filter-and-home-layout; home welcome]
        if (view.Epics is { } epicsForSunburst)
        {
            sb.Append("<div class=\"chart-panel sunburst-panel\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Project at a Glance</h3></div>\n");
            sb.Append(Charts.Sunburst(epicsForSunburst, commands: view.Commands));
            sb.Append("</div>\n\n");
        }

        // Now & Next: sprint board when tracked, else the derived cards; omitted entirely when the view is null.
        if (view.NowNext is { } nowNext)
        {
            AppendNowAndNext(sb, nowNext, view.Epics, view.Counts);
        }

        // Story Pipeline funnel — reads the portal-wide ledger (drafted total == StoriesDefined). [Story 8.3]
        sb.Append("<div class=\"chart-panel funnel-panel\">\n<h3>Story Pipeline</h3>\n");
        sb.Append(Charts.RefinementFunnel(view.Counts));
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

    /// <summary>The unified homepage tile band — headline stats + Epic Status + Overall Progress + Deferred +
    /// Action Items as one flex-wrap row set (typically two dense rows). Journey accents ride on each card;
    /// the first card of a group carries a floating group caption above it.</summary>
    private void AppendTileBand(StringBuilder sb, DashboardView view, ProgressModel p)
    {
        sb.Append("<div class=\"dashboard-tile-band\">\n");

        var reqTiles = view.StatTiles.Where(IsRequirementsStat).ToList();
        var epicStoryTiles = view.StatTiles.Where(t => t.Label is "Epics drafted" or "Stories defined").ToList();
        var executionTiles = view.StatTiles.Where(t => t.Label is "Planned tasks done" or "Direct changes").ToList();
        var insightTiles = view.StatTiles.Where(t => t.Label is "Commit" or "Commits").ToList();
        // One claimed set for leftover rendering — never maintain a second parallel label list.
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        void Claim(IEnumerable<StatTile> tiles)
        {
            foreach (var tile in tiles) claimed.Add(tile.Label);
        }
        Claim(reqTiles);
        Claim(epicStoryTiles);
        Claim(executionTiles);
        Claim(insightTiles);

        // Flat flex-wrap band (typically two dense rows). Journey identity = accent rail + floating caption
        // on the first card of each group — not cluster wrappers that wrap cards into extra rows.
        AppendStatJourney(sb, "requirements", "Requirements", reqTiles);
        AppendEpicsJourney(sb, p, view.Epics, epicStoryTiles);
        AppendExecutionJourney(sb, view.ProgressBars, executionTiles);
        AppendFollowUpJourney(sb, view.Work, view.OpenRetroActionItems, view.Counts);
        AppendStatJourney(sb, "insights", "Insights", insightTiles);

        // Unexpected remaining tiles keep their place at the end so counts never disappear.
        foreach (var tile in view.StatTiles)
        {
            if (claimed.Contains(tile.Label)) continue;
            sb.Append(Charts.StatCard(tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href));
        }

        sb.Append("</div>\n\n");
    }

    private static bool IsRequirementsStat(StatTile tile) =>
        tile.Label is "Functional reqs" or "Non-functional" or "Design reqs";

    private static void AppendStatJourney(StringBuilder sb, string key, string label, IReadOnlyList<StatTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: $"journey-card journey-{key}",
                journeyLabel: i == 0 ? label : null));
        }
    }

    private void AppendEpicsJourney(StringBuilder sb, ProgressModel p, EpicsModel? epicsModel, IReadOnlyList<StatTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: "journey-card journey-epics",
                journeyLabel: i == 0 ? "Epics & Stories" : null));
        }
        AppendEpicStatusTile(sb, p, epicsModel, lead: tiles.Count == 0);
    }

    private void AppendExecutionJourney(StringBuilder sb, IReadOnlyList<ProgressBarView> bars, IReadOnlyList<StatTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: "journey-card journey-execution",
                journeyLabel: i == 0 ? "Execution" : null));
        }
        AppendOverallProgressTile(sb, bars, lead: tiles.Count == 0);
    }

    private void AppendFollowUpJourney(StringBuilder sb, WorkInventory work, int openRetro, ProjectCounts counts)
        => AppendWorkSummaryCards(sb, work, openRetro, counts, lead: true);

    /// <summary>Compact Overall Progress tile — donut centered above a Stories-Defined-style label.
    /// Omitted entirely when there are no bars so an empty Execution accent never appears alone.</summary>
    private void AppendOverallProgressTile(StringBuilder sb, IReadOnlyList<ProgressBarView> bars, bool lead = false)
    {
        if (bars.Count == 0) return;

        var leadAttr = lead ? " journey-lead" : string.Empty;
        var leadHtml = lead ? "<span class=\"tile-journey-label\">Execution</span>" : string.Empty;
        sb.Append($"<div class=\"stat-card tile-card overall-progress-tile journey-card journey-execution{leadAttr}\">\n");
        sb.Append(leadHtml);
        var ring = bars.Count > 1 && bars[1].Max > 0 ? bars[1] : bars[0];
        var pct = ring.Max <= 0 ? 0 : (int)Math.Round(Math.Clamp((double)ring.Value / ring.Max * 100, 0, 100));
        var segments = new List<(string Label, int Value, string CssClass)>
        {
            ("Complete", ring.Value, "done"),
            ("Remaining", Math.Max(0, ring.Max - ring.Value), "pending"),
        };

        // Ring carries progressbar semantics so assistive tech still gets the single completion value. [Story 1.4 AC #1]
        sb.Append($"<div class=\"tile-card-visual\" role=\"progressbar\" aria-valuenow=\"{pct}\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"Overall progress: {pct}%\">\n");
        sb.Append(Charts.Donut(segments, size: 72, ariaLabel: $"Overall progress: {pct}%", centerText: $"{pct}%"));
        sb.Append("</div>\n");
        sb.Append("<div class=\"stat-label\">Overall progress</div>\n");
        sb.Append("</div>\n");
    }

    /// <summary>Compact Epic Status donut tile — donut centered above a Stories-Defined-style label.
    /// Segments come from the retro-gated <see cref="StatusStyles.ForEpicWithRetrospective"/> roll-up.</summary>
    private void AppendEpicStatusTile(StringBuilder sb, ProgressModel p, EpicsModel? epicsModel, bool lead = false)
    {
        var leadAttr = lead ? " journey-lead" : string.Empty;
        var leadHtml = lead ? "<span class=\"tile-journey-label\">Epics &amp; Stories</span>" : string.Empty;
        sb.Append($"<div class=\"stat-card tile-card epic-status-tile journey-card journey-epics{leadAttr}\">\n");
        sb.Append(leadHtml);

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

        sb.Append("<div class=\"tile-card-visual\">\n");
        sb.Append(Charts.Donut(segments, size: 72, ariaLabel: $"Epic status: {ariaParts}", centerText: centerText));
        sb.Append("</div>\n");
        sb.Append("<div class=\"stat-label\">Epic status</div>\n");
        sb.Append("</div>\n");
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
            // Title left; toggle + CTA grouped in a wrap-friendly aside (same pattern as Now & Next's
            // .sprint-board-header-aside) so the header stays two flex peers instead of three crowded ones.
            sb.Append("<div class=\"chart-panel-header-row req-panel-header\">\n");
            sb.Append("<h3>Requirements");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>\n");
            sb.Append("<div class=\"req-panel-header-aside\">");
            sb.Append(RenderRequirementsTabs());
            sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a>");
            sb.Append("</div>\n</div>\n");
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

    /// <summary>Display title for a nav/key-view label — "Epics" reads as "Epics &amp; Stories" in the menu
    /// and key-views chips. Consumed by <see cref="AppendNavMenu"/> and <see cref="AppendKeyViewsBand"/>.</summary>
    private static string QuickLinkTitle(string label) => label switch
    {
        "Epics" => "Epics & Stories",
        _ => label,
    };

    /// <summary>Artifact-family accent class for a key-view chip or group trigger
    /// (planning / architecture / epics / requirements).</summary>
    private static string QuickLinkFamily(string label)
    {
        if (label.Equals("Work", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Epics", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Sprint", StringComparison.OrdinalIgnoreCase))
            return "family-epics";
        if (label.Contains("Architecture", StringComparison.OrdinalIgnoreCase) || label.Contains("ADR", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Code Map", StringComparison.OrdinalIgnoreCase))
            return "family-architecture";
        if (label.Contains("Requirement", StringComparison.OrdinalIgnoreCase))
            return "family-requirements";
        return "family-planning";
    }

    /// <summary>Deferred Work / Action Items follow-up tiles — same StatCard language as the rest of the band
    /// (large number + uppercase label + sub-line). Independently gated. Counts from the portal-wide ledger.</summary>
    private void AppendWorkSummaryCards(StringBuilder sb, WorkInventory work, int openRetro, ProjectCounts counts, bool lead = false)
    {
        var leadUsed = false;
        if (work.Deferred is { } deferred)
        {
            var count = counts.DeferredOpenItems;
            sb.Append(Charts.StatCard(
                count.ToString(),
                "Deferred work",
                $"open {Charts.Plural(count, "item", "items")}",
                tooltip: $"{count} open deferred {Charts.Plural(count, "item", "items")} tracked outside the epic plan.",
                href: deferred.OutputPath,
                extraClass: "journey-card journey-followup work-summary-card deferred",
                journeyLabel: lead && !leadUsed ? "Follow up" : null));
            leadUsed = true;
        }

        if (openRetro > 0)
        {
            sb.Append(Charts.StatCard(
                openRetro.ToString(),
                "Action items",
                $"open {Charts.Plural(openRetro, "item", "items")}",
                tooltip: $"{openRetro} open retro action {Charts.Plural(openRetro, "item", "items")}.",
                href: SiteNav.ActionItemsOutputPath,
                extraClass: "journey-card journey-followup work-summary-card retro",
                journeyLabel: lead && !leadUsed ? "Follow up" : null));
        }
    }
}
