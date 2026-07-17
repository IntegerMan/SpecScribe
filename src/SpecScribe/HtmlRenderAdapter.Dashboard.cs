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

        // Tile band — journey groups tagged for work-stage visibility. [Story 9.8]
        AppendTileBand(sb, view, p);

        // Sunburst — Overview pulse.
        if (view.Epics is { } epicsForSunburst)
        {
            sb.Append("<div class=\"chart-panel sunburst-panel wm-panel wm-show-overview wm-show-track\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Project at a Glance</h3></div>\n");
            sb.Append(Charts.Sunburst(epicsForSunburst, commands: view.Commands, followUps: view.FollowUps));
            sb.Append("</div>\n\n");
        }

        // Project Next Steps — Overview + Review (body from DashboardView; wrap here so wm-* classes
        // are composed, not string-replaced onto RenderPanel markup). [Story 9.8]
        if (view.NextStepsHtml.Length > 0)
        {
            sb.Append("<div class=\"chart-panel next-steps wm-panel wm-show-overview wm-show-review\">\n");
            sb.Append(view.NextStepsHtml);
            sb.Append("</div>\n\n");
        }

        // Now & Next / sprint board — Develop.
        if (view.NowNext is { } nowNext)
        {
            AppendNowAndNext(sb, nowNext, view.Epics, view.Counts, view.Commands);
        }

        // Story Pipeline — Requirements.
        sb.Append("<div class=\"chart-panel funnel-panel wm-panel wm-show-requirements\">\n<h3>Story Pipeline</h3>\n");
        sb.Append(Charts.RefinementFunnel(view.Counts));
        sb.Append("</div>\n\n");

        // Git Pulse — Develop.
        sb.Append("<div class=\"chart-panel git-pulse-panel wm-panel wm-show-develop\">\n");
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

        // Planning documents — Requirements.
        if (view.Coverage is { IsEmpty: false } coverage)
        {
            var coverageToday = DateOnly.FromDateTime(DateTime.Now);
            sb.Append("<div class=\"chart-panel coverage-panel wm-panel wm-show-requirements\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Planning Artifacts</h3>");
            sb.Append(Charts.CoverageMeter(coverage, coverageToday));
            sb.Append("</div>\n");
            sb.Append(Charts.ArtifactCoveragePanel(coverage, coverageToday));
            sb.Append("</div>\n\n");
        }

        AppendRequirementsPanel(sb, view.Requirements, view.Epics, view.Counts);

        // Progress by Epic mosaic — Plan.
        if (p.PerEpic.Count > 0)
        {
            sb.Append("<div class=\"chart-panel wm-panel wm-show-plan wm-show-track\">\n<h3>Progress by Epic</h3>\n");
            sb.Append(Charts.EpicMosaic(p.PerEpic, e => $"epics/epic-{e.Number}.html"));
            sb.Append("</div>\n\n");
        }

        sb.Append("</section>\n\n");
    }

    /// <summary>The unified homepage tile band — a 5-column grid (2×5 when Overview/Track show their
    /// curated 10 tiles). Journey accents and work-stage visibility classes ride on each card; captions
    /// sit in a reserved padding slot so they never stagger rows. [Story 9.8]</summary>
    private void AppendTileBand(StringBuilder sb, DashboardView view, ProgressModel p)
    {
        sb.Append("<div class=\"dashboard-tile-band\">\n");

        var reqTiles = view.StatTiles.Where(IsRequirementsStat).ToList();
        var epicStoryTiles = view.StatTiles.Where(t => t.Label is "Epics drafted" or "Stories defined").ToList();
        var plannedTiles = view.StatTiles.Where(t => t.Label is "Planned tasks done").ToList();
        var directTiles = view.StatTiles.Where(t => t.Label is "Direct changes").ToList();
        var insightTiles = view.StatTiles.Where(t => t.Label is "Commit" or "Commits").ToList();
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        void Claim(IEnumerable<StatTile> tiles)
        {
            foreach (var tile in tiles) claimed.Add(tile.Label);
        }
        Claim(reqTiles);
        Claim(epicStoryTiles);
        Claim(plannedTiles);
        Claim(directTiles);
        Claim(insightTiles);

        // Overview/Track → 10 tiles (2×5). Review → 8 tiles (2×4 via CSS). Develop → commits only.
        // Direct changes skips Overview (redundant with Deferred); Commits skips Overview (Develop home).
        AppendStatJourney(sb, "requirements", "Requirements", reqTiles,
            "wm-panel wm-show-overview wm-show-requirements wm-show-track");
        AppendEpicsJourney(sb, p, view.Epics, epicStoryTiles,
            "wm-panel wm-show-overview wm-show-plan wm-show-review wm-show-track");
        AppendStatJourney(sb, "execution", "Execution", plannedTiles,
            "wm-panel wm-show-overview wm-show-review wm-show-track");
        AppendStatJourney(sb, "execution", "Execution", directTiles,
            "wm-panel wm-show-review",
            showLead: plannedTiles.Count == 0);
        AppendOverallProgressTile(sb, view.ProgressBars,
            "wm-panel wm-show-overview wm-show-review wm-show-track",
            lead: plannedTiles.Count == 0 && directTiles.Count == 0);
        AppendFollowUpJourney(sb, view.Work, view.OpenRetroActionItems, view.Counts,
            "wm-panel wm-show-overview wm-show-review wm-show-track");
        AppendStatJourney(sb, "insights", "Insights", insightTiles,
            "wm-panel wm-show-develop");

        foreach (var tile in view.StatTiles)
        {
            if (claimed.Contains(tile.Label)) continue;
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: "wm-panel wm-show-overview"));
        }

        sb.Append("</div>\n\n");
    }

    private static bool IsRequirementsStat(StatTile tile) =>
        tile.Label is "Functional reqs" or "Non-functional" or "Design reqs";

    private static void AppendStatJourney(
        StringBuilder sb, string key, string label, IReadOnlyList<StatTile> tiles, string showClass,
        bool showLead = true)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: $"journey-card journey-{key} {showClass}",
                journeyLabel: showLead && i == 0 ? label : null));
        }
    }

    private void AppendEpicsJourney(
        StringBuilder sb, ProgressModel p, EpicsModel? epicsModel, IReadOnlyList<StatTile> tiles, string showClass)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            sb.Append(Charts.StatCard(
                tile.Number, tile.Label, tile.Sub, tile.Tooltip, tile.Href,
                extraClass: $"journey-card journey-epics {showClass}",
                journeyLabel: i == 0 ? "Epics & Stories" : null));
        }
        AppendEpicStatusTile(sb, p, epicsModel, showClass, lead: tiles.Count == 0);
    }

    private void AppendFollowUpJourney(
        StringBuilder sb, WorkInventory work, int openRetro, ProjectCounts counts, string showClass)
    {
        if (!HasFollowUpTiles(work, openRetro)) return;
        AppendWorkSummaryCards(sb, work, openRetro, counts, showClass, lead: true);
    }

    private static bool HasFollowUpTiles(WorkInventory work, int openRetro) =>
        work.Deferred is not null || openRetro > 0;

    /// <summary>Compact Overall Progress tile — donut centered above a Stories-Defined-style label.
    /// Omitted entirely when there are no bars so an empty Execution accent never appears alone.</summary>
    private void AppendOverallProgressTile(
        StringBuilder sb, IReadOnlyList<ProgressBarView> bars, string showClass, bool lead = false)
    {
        if (bars.Count == 0) return;

        var leadAttr = lead ? " journey-lead" : string.Empty;
        var leadHtml = lead ? "<span class=\"tile-journey-label\">Execution</span>" : string.Empty;
        sb.Append($"<div class=\"stat-card tile-card overall-progress-tile journey-card journey-execution{leadAttr} {showClass}\">\n");
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
        sb.Append(Charts.Donut(segments, size: 52, ariaLabel: $"Overall progress: {pct}%", centerText: $"{pct}%"));
        sb.Append("</div>\n");
        sb.Append("<div class=\"stat-label\">Overall progress</div>\n");
        sb.Append("</div>\n");
    }

    /// <summary>Compact Epic Status donut tile — donut centered above a Stories-Defined-style label.
    /// Segments come from the retro-gated <see cref="StatusStyles.ForEpicWithRetrospective"/> roll-up.</summary>
    private void AppendEpicStatusTile(
        StringBuilder sb, ProgressModel p, EpicsModel? epicsModel, string showClass, bool lead = false)
    {
        var leadAttr = lead ? " journey-lead" : string.Empty;
        var leadHtml = lead ? "<span class=\"tile-journey-label\">Epics &amp; Stories</span>" : string.Empty;
        sb.Append($"<div class=\"stat-card tile-card epic-status-tile journey-card journey-epics{leadAttr} {showClass}\">\n");
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
        sb.Append(Charts.Donut(segments, size: 52, ariaLabel: $"Epic status: {ariaParts}", centerText: centerText));
        sb.Append("</div>\n");
        sb.Append("<div class=\"stat-label\">Epic status</div>\n");
        sb.Append("</div>\n");
    }
    /// <summary>The "Now &amp; Next" panel — Develop work-stage. Re-homed from <c>HtmlTemplater.AppendNowAndNext</c>.</summary>
    private void AppendNowAndNext(StringBuilder sb, DashboardNowNext nowNext, EpicsModel? epicsModel, ProjectCounts counts, CommandCatalog commands)
    {
        if (nowNext.SprintBoard is { } sprint && epicsModel is not null)
        {
            sb.Append("<div class=\"chart-panel sprint-board-panel wm-panel wm-show-develop\">\n");
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
            sb.Append(SprintTemplater.RenderBoard(sprint, epicsModel, capPerColumn: 3, moreHref: SiteNav.SprintOutputPath, wrapWithEpicFilter: false, commands: commands));
            sb.Append(SprintTemplater.CloseEpicFilterable());
            sb.Append("</div>\n\n");
            return;
        }

        sb.Append("<div class=\"chart-panel wm-panel wm-show-develop\">\n<h3>Now &amp; Next");
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

    /// <summary>The dashboard requirements panel — Requirements work-stage.</summary>
    private void AppendRequirementsPanel(StringBuilder sb, RequirementsModel? requirements, EpicsModel? epicsModel,
        ProjectCounts counts)
    {
        // Gate on Everything so Design-only inventories still get the satisfaction rollup (ledger is over
        // Everything). FR+NFR grid/flow stay gated on All. [Story 9.9 review]
        if (requirements is null || !requirements.Everything.Any()) return;

        var hasFrNfr = requirements.All.Any();

        sb.Append("<div class=\"chart-panel req-panel wm-panel wm-show-requirements wm-show-track\">\n");
        if (epicsModel is not null)
        {
            sb.Append("<div class=\"chart-panel-header-row req-panel-header\">\n");
            sb.Append("<h3>Requirements");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>\n");
            sb.Append("<div class=\"req-panel-header-aside\">");
            if (hasFrNfr) sb.Append(RenderRequirementsTabs());
            sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a>");
            sb.Append("</div>\n</div>\n");
            AppendSatisfactionRollup(sb, counts.RequirementsOverall);
            if (hasFrNfr)
            {
                sb.Append("<div class=\"req-view req-view-flow\">");
                sb.Append(Charts.RequirementFlow(requirements, epicsModel));
                sb.Append("</div>\n");
                sb.Append("<div class=\"req-view req-view-grid\">");
                sb.Append(Charts.RequirementStatusGrid(requirements.All.ToList(), prefix: string.Empty));
                sb.Append("</div>\n");
            }
        }
        else
        {
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Requirements");
            sb.Append(StatusStyles.LegendKey());
            sb.Append("</h3>");
            sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a></div>\n");
            AppendSatisfactionRollup(sb, counts.RequirementsOverall);
            if (hasFrNfr)
            {
                sb.Append(Charts.RequirementStatusGrid(requirements.All.ToList(), prefix: string.Empty));
            }
        }

        sb.Append("</div>\n\n");
    }

    /// <summary>Compact Home satisfaction rollup — four chips linking to <c>requirements.html#satisfaction</c>.
    /// Omitted when the ledger has no requirements (NFR8). [Story 9.9]</summary>
    private static void AppendSatisfactionRollup(StringBuilder sb, ProjectCounts.RequirementSatisfaction sat)
    {
        if (sat.Total <= 0) return;

        sb.Append("<div class=\"satisfaction-rollup\">\n");
        sb.Append("<div class=\"satisfaction-rollup-label\">Satisfaction</div>\n");
        // The bracketed bar bridges the four chips to the six-tier Sankey directly below on Home — its In-flight
        // bracket carries the same Partially/Ready/Planned colors the Sankey uses, so the readings no longer read
        // as a colour-mismatched header over the flow. [Story 9.9 coherence]
        sb.Append(Charts.RequirementSatisfactionBar(sat));
        sb.Append(Charts.RequirementSatisfactionChips(
            sat,
            satisfiedHref: "requirements.html#satisfaction",
            inFlightHref: "requirements.html#satisfaction",
            deferredHref: "requirements.html#satisfaction",
            unmappedHref: "requirements.html#satisfaction"));
        sb.Append("</div>\n");
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
    private void AppendWorkSummaryCards(
        StringBuilder sb, WorkInventory work, int openRetro, ProjectCounts counts, string showClass, bool lead = false)
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
                extraClass: $"journey-card journey-followup work-summary-card deferred {showClass}",
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
                extraClass: $"journey-card journey-followup work-summary-card retro {showClass}",
                journeyLabel: lead && !leadUsed ? "Follow up" : null));
        }
    }
}
