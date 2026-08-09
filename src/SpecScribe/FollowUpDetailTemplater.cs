using System.Text;

namespace SpecScribe;

/// <summary>Shared follow-up detail page for action items and deferred-work items.
/// One template; the only per-kind branch is provenance framing + status vocabulary. [Story 9.11]</summary>
public static class FollowUpDetailTemplater
{
    /// <summary>Builds an action-item page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract, so
    /// the IR's content region can be COMPOSED (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup +
    /// wayfinding + body) instead of sliced back out of a rendered full page. <see cref="RenderActionPage"/> is
    /// the unchanged HTML projection of this same model, so the bytes are identical. [Story 23.4 AC #3]</summary>
    public static PageView BuildActionPage(
        SprintActionItem item,
        string slug,
        SiteNav nav,
        CommandCatalog commands,
        IReadOnlyDictionary<int, string>? epicRetroMap = null,
        string? deferredWorkHref = null,
        EpicsModel? epicsModel = null,
        IReadOnlyDictionary<string, string>? hrefMap = null,
        IReadOnlyDictionary<SprintActionItem, IReadOnlyList<int>>? crossLinks = null,
        NavLocalContext? localContext = null)
    {
        var outputPath = FollowUpSlug.OutputPath(slug);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var title = FollowUpRow.SummarizePlainText(item.Action, maxChars: 80);
        var statusToken = StatusStyles.ForSprint(item.Status);
        var statusLabel = StatusStyles.SprintLabel(item.Status);

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"followup-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Action item</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusToken, statusLabel)}");
        if (item.EpicNumber is { } epicNum)
            sb.Append(EpicPillLink(prefix, epicNum));
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</div>\n</header>\n\n");

        sb.Append("<section class=\"followup-detail-body\">\n");
        sb.Append($"  <div class=\"followup-detail-fulltext\">{FollowUpRefs.LinkifyVisibleText(item.Action, epicsModel, hrefMap, prefix)}</div>\n");

        // Provenance: retro-epic framing (action-item branch).
        sb.Append("  <div class=\"followup-detail-provenance epic-card\">\n");
        sb.Append("    <h3>Where it came from</h3>\n");
        if (item.EpicNumber is { } en)
        {
            var label = $"From the Epic {en} retrospective";
            if (epicRetroMap is not null && epicRetroMap.TryGetValue(en, out var retroHref))
            {
                var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(retroHref));
                sb.Append($"    <p><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(label)}</a></p>\n");
            }
            else
            {
                sb.Append($"    <p>{PathUtil.Html(label)}</p>\n");
            }
        }
        else
        {
            sb.Append("    <p>Unattributed — no epic retrospective recorded.</p>\n");
        }
        sb.Append("  </div>\n");

        if (crossLinks is not null && crossLinks.TryGetValue(item, out var otherEpics) && otherEpics is { Count: > 0 })
        {
            sb.Append("  <div class=\"followup-detail-cross\">\n");
            ActionItemsTemplater.AppendCrossLinks(sb, item, crossLinks, epicRetroMap, prefix);
            sb.Append("  </div>\n");
        }

        if (deferredWorkHref is { Length: > 0 } dw && DeferralHeuristics.IsDebtRelated(item.Action))
        {
            var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(dw));
            sb.Append($"  <a class=\"action-item-deferred\" href=\"{PathUtil.Html(href)}\">In deferred-work backlog &rarr;</a>\n");
        }

        sb.Append("</section>\n");

        // Same Next Steps card panel as story pages — labeled resolve prompt + room for alternates.
        var nextSteps = WorkflowCommands.RenderActionItemNextSteps(item, commands);
        if (nextSteps.Length > 0)
            sb.Append(nextSteps);

        AppendBackLink(sb, prefix + SiteNav.ActionItemsOutputPath, "All open action items");
        sb.Append("</main>\n\n");

        return ComposePage(sb, nav, outputPath, prefix, title, "Action item",
            ("Open Action Items", SiteNav.ActionItemsOutputPath), localContext);
    }

    /// <summary>Builds a deferred-work page's host-neutral <see cref="PageView"/> — see
    /// <see cref="BuildActionPage"/>. [Story 23.4 AC #3]</summary>
    public static PageView BuildDeferredPage(
        DeferredWorkItem item,
        string provenanceLabel,
        string? sourceStoryHref,
        string slug,
        SiteNav nav,
        string listOutputPath,
        CommandCatalog? commands = null,
        int? epicNumber = null,
        NavLocalContext? localContext = null,
        string? displayBodyHtml = null)
    {
        var outputPath = FollowUpSlug.OutputPath(slug);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var title = FollowUpRow.SummarizeFromHtml(item.BodyHtml, maxChars: 80);
        var (statusToken, statusLabel) = item.Resolved
            ? ("done", "Resolved")
            : (StatusStyles.ForSprint("open"), "Open");

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"followup-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Deferred work</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusToken, statusLabel)}");
        if (epicNumber is { } en)
            sb.Append(EpicPillLink(prefix, en));
        sb.Append(StatusStyles.LegendKey());
        if (item.Resolved)
            sb.Append("<span class=\"deferred-resolved-mark\" aria-hidden=\"true\">✓</span>");
        sb.Append("</div>\n</header>\n\n");

        sb.Append("<section class=\"followup-detail-body\">\n");
        sb.Append($"  <div class=\"deferred-item-body followup-detail-fulltext\">{displayBodyHtml ?? item.BodyHtml}</div>\n");

        // Provenance: ## Deferred from: source framing (deferred-item branch).
        sb.Append("  <div class=\"followup-detail-provenance epic-card\">\n");
        sb.Append("    <h3>Where it came from</h3>\n");
        sb.Append("    <p>Deferred from: ");
        if (sourceStoryHref is { Length: > 0 })
        {
            var href = FollowUpGeometry.ApplyLinkPrefix(prefix, sourceStoryHref);
            sb.Append($"<a href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">{PathUtil.Html(provenanceLabel)}</a>");
        }
        else
        {
            sb.Append(PathUtil.Html(provenanceLabel));
        }
        sb.Append("</p>\n  </div>\n");

        if (item.ResolvingHref is { Length: > 0 } rh && item.ResolvingRef is { Length: > 0 } rr)
        {
            var label = FollowUpRefs.ResolvingLabel(rr);
            var href = FollowUpGeometry.ApplyLinkPrefix(prefix, rh);
            sb.Append($"  <a class=\"deferred-item-resolving\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">Resolving: {PathUtil.Html(label)} &rarr;</a>\n");
        }
        else if (item.ResolvingRef is { Length: > 0 } rr2)
        {
            sb.Append($"  <span class=\"deferred-item-resolving\">Resolving: {PathUtil.Html(FollowUpRefs.ResolvingLabel(rr2))}</span>\n");
        }

        sb.Append("</section>\n");

        var nextSteps = WorkflowCommands.RenderDeferredItemNextSteps(item, commands ?? CommandCatalog.Empty);
        if (nextSteps.Length > 0)
            sb.Append(nextSteps);

        var listHref = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(listOutputPath));
        AppendBackLink(sb, listHref, "All deferred work");
        sb.Append("</main>\n\n");

        return ComposePage(sb, nav, outputPath, prefix, title, "Deferred work",
            ("Deferred Work", PathUtil.NormalizeSlashes(listOutputPath)), localContext);
    }

    /// <summary>The one shell both follow-up kinds share, as a <see cref="PageView"/>. Replaces the old
    /// AppendShellOpen/AppendShellClose string pair: the same identity facts now REACH the delivery contract
    /// instead of being string-built into a full page and discarded. The caller's
    /// <paramref name="localContext"/> is threaded straight to <see cref="SiteNav.ToNavigationView"/>, which has
    /// always accepted one — so the page-local nav band survives region composition by construction.
    /// [Story 23.4 AC #3]</summary>
    private static PageView ComposePage(
        StringBuilder body, SiteNav nav, string outputPath, string prefix, string title, string kindLabel,
        (string Label, string Href) listCrumb, NavLocalContext? localContext) =>
        new()
        {
            Kind = PageKind.Doc,
            OutputRelativePath = outputPath,
            Title = $"{title} — {nav.SiteTitle}",
            MetaDescription = $"{kindLabel} follow-up for {nav.SiteTitle}.",
            Nav = nav.ToNavigationView(outputPath, localContext),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                (listCrumb.Label, listCrumb.Href),
                (title, null),
            }),
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = InteractionState.None,
            BodyHtml = body.ToString(),
        };

    private static void AppendBackLink(StringBuilder sb, string href, string label)
    {
        sb.Append($"<p class=\"followup-detail-back\"><a href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">&larr; {PathUtil.Html(label)}</a></p>\n");
    }

    /// <summary>Stylized epic page link in the meta-pills row — same <c>pill pill-link</c> treatment
    /// as retrospective detail pages. Omit when unattributed (NFR8).</summary>
    private static string EpicPillLink(string prefix, int epicNumber)
    {
        var href = PathUtil.NormalizeSlashes(prefix + $"epics/epic-{epicNumber}.html");
        return $"<a class=\"pill pill-link\" href=\"{PathUtil.Html(href)}\">Epic {epicNumber} &rarr;</a>";
    }
}
