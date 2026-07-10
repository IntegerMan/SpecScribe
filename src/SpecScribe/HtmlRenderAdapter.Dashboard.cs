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
    public string RenderDashboardBody(DashboardView view)
    {
        var sb = new StringBuilder();

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(view.SiteTitle)}</h1>\n");
        sb.Append("</header>\n\n");

        AppendDashboardSection(sb, view);
        AppendWorkTypesSection(sb, view.Work, view.OpenRetroActionItems);
        foreach (var band in view.IndexBands)
        {
            AppendIndexBand(sb, band);
        }

        sb.Append("</main>\n\n");
        return sb.ToString();
    }

    private void AppendDashboardSection(StringBuilder sb, DashboardView view)
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

        // Now & Next: sprint board when tracked, else the derived cards; omitted entirely when the view is null.
        if (view.NowNext is { } nowNext)
        {
            AppendNowAndNext(sb, nowNext, view.Epics);
        }

        // The sunburst headline panel — present only when there is an epics model.
        if (view.Epics is { } epicsForSunburst)
        {
            sb.Append("<div class=\"chart-panel sunburst-panel\">\n");
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Project at a Glance</h3>");
            sb.Append("<a class=\"view-epic-link\" href=\"epics.html\">View Epics &amp; Stories &rarr;</a></div>\n");
            sb.Append(Charts.Sunburst(epicsForSunburst, commands: view.Commands));
            sb.Append("</div>\n\n");
        }

        // Epic-status donut + overall-progress bars share a row.
        sb.Append("<div class=\"chart-row\">\n");
        AppendEpicStatusPanel(sb, p, view.Epics);

        sb.Append("<div class=\"chart-panel\">\n<h3>Overall Progress</h3>\n");
        foreach (var bar in view.ProgressBars)
        {
            sb.Append(Charts.ProgressBar(bar.Label, bar.Value, bar.Max, bar.RightLabel));
        }
        sb.Append("</div>\n");
        sb.Append("</div>\n\n");

        // Story Pipeline funnel — unconditional (the builder owns its empty-state via ProgressModel.Empty).
        sb.Append("<div class=\"chart-panel funnel-panel\">\n<h3>Story Pipeline</h3>\n");
        sb.Append(Charts.RefinementFunnel(p));
        sb.Append("</div>\n\n");

        // Consolidated Git Pulse panel — header links fork on deep-git, body forks on git presence.
        sb.Append("<div class=\"chart-panel git-pulse-panel\">\n");
        if (p.DeepGit is not null)
        {
            sb.Append("<div class=\"chart-panel-header-row\"><h3>Git Pulse</h3><span class=\"git-pulse-header-links\">");
            if (p.DeepGit.Insights is not null)
            {
                sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.GitInsightsOutputPath}\">View all git insights &rarr;</a>");
            }
            sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.DeepAnalyticsOutputPath}\">View Deep Analytics &rarr;</a></span></div>\n");
        }
        else
        {
            sb.Append("<h3>Git Pulse</h3>\n");
        }
        sb.Append(p.Git is { } pulse
            ? Charts.GitPulsePanel(pulse)
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

        AppendDashboardQuickLinks(sb, view.QuickLinks);

        sb.Append("</section>\n\n");
    }

    /// <summary>The Epic Status donut. Re-homed from <c>HtmlTemplater.AppendEpicStatusPanel</c> — the donut still
    /// derives its segments from the same <see cref="StatusStyles.ForEpic"/> roll-up, so nothing is re-modelled.</summary>
    private void AppendEpicStatusPanel(StringBuilder sb, ProgressModel p, EpicsModel? epicsModel)
    {
        sb.Append("<div class=\"chart-panel\">\n<h3>Epic Status</h3>\n<div class=\"donut-and-legend\">\n");

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
    private void AppendNowAndNext(StringBuilder sb, DashboardNowNext nowNext, EpicsModel? epicsModel)
    {
        if (nowNext.SprintBoard is { } sprint && epicsModel is not null)
        {
            sb.Append("<div class=\"chart-panel sprint-board-panel\">\n");
            sb.Append("<div class=\"chart-panel-header-row sprint-board-header\">\n");
            sb.Append("  <h3>Now &amp; Next <span class=\"panel-source-inline\">from sprint-status.yaml</span></h3>\n");
            sb.Append("  <div class=\"sprint-board-header-aside\">");
            sb.Append(SprintTemplater.RenderProgressWheel(sprint));
            sb.Append($"<a class=\"view-epic-link\" href=\"{SiteNav.SprintOutputPath}\">View sprint board &rarr;</a>");
            sb.Append("</div>\n</div>\n");
            sb.Append(SprintTemplater.RenderBoard(sprint, epicsModel, capPerColumn: 3, moreHref: SiteNav.SprintOutputPath));
            sb.Append("</div>\n\n");
            return;
        }

        sb.Append("<div class=\"chart-panel\">\n<h3>Now &amp; Next</h3>\n<div class=\"now-next\">\n");
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

        sb.Append("<div class=\"chart-panel req-panel\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3>Requirements</h3>");
        sb.Append("<a class=\"view-epic-link\" href=\"requirements.html\">View Requirements &rarr;</a></div>\n");
        sb.Append(Charts.RequirementStatusGrid(requirements.All.ToList(), prefix: string.Empty));
        if (epicsModel is not null)
        {
            sb.Append(Charts.RequirementFlow(requirements, epicsModel));
        }
        sb.Append("</div>\n\n");
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

    /// <summary>The "Direct &amp; Quick-Dev Work" band. Re-homed from <c>HtmlTemplater.AppendWorkTypesSection</c>,
    /// driven by the <see cref="WorkInventory"/> domain input + the open-retro count.</summary>
    private void AppendWorkTypesSection(StringBuilder sb, WorkInventory work, int openRetro)
    {
        if (work.IsEmpty && openRetro == 0) return;

        sb.Append($"<div class=\"index-section-title\">{Icons.ForConcept("Direct & Quick-Dev Work")}Direct &amp; Quick-Dev Work</div>\n");

        if (work.QuickDev.Count > 0)
        {
            sb.Append("<div class=\"index-grid\">\n");
            foreach (var entry in work.QuickDev)
            {
                sb.Append($"  <a class=\"index-card quick-dev-card\" href=\"{PathUtil.Html(entry.OutputPath)}\">\n");
                sb.Append($"    <h2>{PathUtil.Html(entry.Title)}</h2>\n");

                var badges = new StringBuilder();
                if (entry.Status is { Length: > 0 } status)
                {
                    badges.Append(StatusStyles.Badge(StatusStyles.ForStatus(status), status));
                }
                if (entry.Type is { Length: > 0 } type)
                {
                    badges.Append($"<span class=\"pill\">{PathUtil.Html(type)}</span>");
                }
                if (badges.Length > 0)
                {
                    sb.Append($"    <p class=\"work-card-badges\">{badges}</p>\n");
                }

                sb.Append("    <span class=\"index-card-path\">Quick-dev · one-shot</span>\n");
                sb.Append("  </a>\n");
            }
            sb.Append("</div>\n\n");
        }

        if (work.Deferred is { } deferred)
        {
            var count = deferred.OpenItemCount;
            sb.Append($"<a class=\"work-callout\" href=\"{PathUtil.Html(deferred.OutputPath)}\">\n");
            sb.Append($"  <span class=\"work-callout-label\">{Icons.ForConcept("Deferred")}Deferred Work</span>\n");
            sb.Append($"  <span class=\"work-callout-count\">{count} open {Charts.Plural(count, "item", "items")}</span>\n");
            sb.Append("</a>\n\n");
        }

        if (openRetro > 0)
        {
            sb.Append($"<a class=\"work-callout retro-callout\" href=\"{PathUtil.Html(SiteNav.ActionItemsOutputPath)}\">\n");
            sb.Append($"  <span class=\"work-callout-label\">{Icons.ForConcept("Retrospective")}Retro Action Items</span>\n");
            sb.Append($"  <span class=\"work-callout-count\">{openRetro} open {Charts.Plural(openRetro, "item", "items")}</span>\n");
            sb.Append("</a>\n\n");
        }
    }

    /// <summary>Renders one home-index band. Re-homed from the band loop + <c>AppendPlanningSection</c> /
    /// <c>AppendAdrSection</c> / <c>AppendRetrosSection</c> in <c>HtmlTemplater</c>, now driven by <see cref="IndexBand"/>.</summary>
    private void AppendIndexBand(StringBuilder sb, IndexBand band)
    {
        if (band.TitleRow)
        {
            // ADR band: the title-row layout with a trailing "more" link.
            sb.Append("<div class=\"index-section-title-row\">\n");
            sb.Append($"  <span class=\"index-section-title\">{Icons.ForConcept(band.ConceptKey)}{PathUtil.Html(band.Title)}</span>\n");
            sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(band.MoreLinkHref!)}\">{PathUtil.Html(band.MoreLinkLabel!)} &rarr;</a>\n");
            sb.Append("</div>\n");
            sb.Append("<div class=\"index-grid\">\n");
            foreach (var card in band.Cards) AppendIndexCard(sb, card);
            sb.Append("</div>\n\n");
            return;
        }

        if (band.Planning is { } planning)
        {
            AppendPlanningBand(sb, band, planning);
            return;
        }

        var titleGlyph = band.NoIcon ? string.Empty : Icons.ForConcept(band.ConceptKey);
        sb.Append($"<div class=\"index-section-title\">{titleGlyph}{PathUtil.Html(band.Title)}</div>\n");
        sb.Append("<div class=\"index-grid\">\n");
        foreach (var card in band.Cards) AppendIndexCard(sb, card);
        sb.Append("</div>\n\n");
    }

    private void AppendPlanningBand(StringBuilder sb, IndexBand band, PlanningLayout planning)
    {
        sb.Append($"<div class=\"index-section-title\">{Icons.ForConcept(band.ConceptKey)}{PathUtil.Html(band.Title)}</div>\n");

        if (planning.Prd is { } prd)
        {
            sb.Append("<div class=\"index-grid\">\n");
            AppendIndexCard(sb, prd);
            sb.Append("</div>\n");
        }

        if (planning.UxCards.Count > 0)
        {
            sb.Append("<div class=\"index-subgroup-label\">UX</div>\n");
            sb.Append("<div class=\"index-grid\">\n");
            foreach (var card in planning.UxCards) AppendIndexCard(sb, card);
            sb.Append("</div>\n");
        }

        if (planning.OtherCards.Count > 0)
        {
            sb.Append("<div class=\"index-grid\">\n");
            foreach (var card in planning.OtherCards) AppendIndexCard(sb, card);
            sb.Append("</div>\n");
        }

        sb.Append("\n");
    }

    /// <summary>Renders one index card per its <see cref="IndexCardStyle"/>. Re-homed from
    /// <c>HtmlTemplater.AppendIndexCard</c> / <c>AppendAdrCard</c> / <c>AppendPrimaryPrdCard</c> /
    /// <c>AppendRetrosSection</c>'s card loop.</summary>
    private void AppendIndexCard(StringBuilder sb, IndexCardView card)
    {
        switch (card.Style)
        {
            case IndexCardStyle.PrimaryPrd:
                sb.Append("  <div class=\"index-card index-card--primary\">\n");
                sb.Append($"    <span class=\"index-card-kicker\">{PathUtil.Html(card.Kicker!)}</span>\n");
                sb.Append($"    <h2><a href=\"{PathUtil.Html(card.Href)}\">{PathUtil.Html(card.Title)}</a></h2>\n");
                AppendCardStatusBadge(sb, card.Status);
                AppendCardMeta(sb, card.Meta);
                sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(card.SourcePath)}</span>\n");
                if (card.BranchHref is { } branchHref)
                {
                    sb.Append($"    <a class=\"index-card-branch\" href=\"{PathUtil.Html(branchHref)}\">{PathUtil.Html(card.BranchLabel!)} &rarr;</a>\n");
                }
                sb.Append("  </div>\n");
                break;

            case IndexCardStyle.Adr:
                sb.Append($"  <a class=\"index-card\" href=\"{PathUtil.Html(card.Href)}\">\n");
                sb.Append($"    <h2>{PathUtil.Html(card.Title)}</h2>\n");
                if (card.Status is { Length: > 0 } adrStatus)
                {
                    var cls = "status-" + adrStatus.Split(' ')[0].ToLowerInvariant().Replace(' ', '-');
                    sb.Append($"    <p><span class=\"pill {PathUtil.Html(cls)}\">{PathUtil.Html(adrStatus)}</span></p>\n");
                }
                sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(card.SourcePath)}</span>\n");
                sb.Append("  </a>\n");
                break;

            case IndexCardStyle.Retro:
                sb.Append($"  <a class=\"index-card\" href=\"{PathUtil.Html(card.Href)}\">\n");
                sb.Append($"    <h2>{PathUtil.Html(card.Title)}</h2>\n");
                if (card.Meta is { Length: > 0 } date)
                {
                    sb.Append($"    <p>{PathUtil.Html(date)}</p>\n");
                }
                sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(card.SourcePath)}</span>\n");
                sb.Append("  </a>\n");
                break;

            default: // Doc
                sb.Append($"  <a class=\"index-card\" href=\"{PathUtil.Html(card.Href)}\">\n");
                sb.Append($"    <h2>{PathUtil.Html(card.Title)}</h2>\n");
                AppendCardStatusBadge(sb, card.Status);
                AppendCardMeta(sb, card.Meta);
                sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(card.SourcePath)}</span>\n");
                sb.Append("  </a>\n");
                break;
        }
    }

    /// <summary>A doc/PRD card's status BADGE via <see cref="StatusStyles.ForDoc"/>/<see cref="StatusStyles.DocLabel"/>
    /// — the single status-color seam. Emits nothing for a blank status. Re-homed from
    /// <c>HtmlTemplater.AppendCardStatusBadge</c>.</summary>
    private static void AppendCardStatusBadge(StringBuilder sb, string? status)
    {
        if (status?.Trim() is not { Length: > 0 } trimmed) return;
        sb.Append($"    {StatusStyles.Badge(StatusStyles.ForDoc(trimmed), StatusStyles.DocLabel(trimmed))}\n");
    }

    /// <summary>The de-emphasized meta line (<c>&lt;p&gt;</c>). Emits nothing when null. Re-homed from
    /// <c>HtmlTemplater.AppendCardMeta</c>.</summary>
    private static void AppendCardMeta(StringBuilder sb, string? meta)
    {
        if (meta is not { Length: > 0 }) return;
        sb.Append($"    <p>{PathUtil.Html(meta)}</p>\n");
    }
}
