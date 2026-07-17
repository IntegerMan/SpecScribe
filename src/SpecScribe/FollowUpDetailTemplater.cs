using System.Text;

namespace SpecScribe;

/// <summary>Shared follow-up detail page for action items and deferred-work items.
/// One template; the only per-kind branch is provenance framing + status vocabulary. [Story 9.11]</summary>
public static class FollowUpDetailTemplater
{
    /// <summary>Renders an action-item detail page. Visible text is linkified inside the templater;
    /// Resolve-with-AI <c>data-copy</c> keeps raw action text. Do not run the result through
    /// site-level <c>ApplyReferenceLinks</c>. [Story 9.11]</summary>
    public static string RenderActionPage(
        SprintActionItem item,
        string slug,
        SiteNav nav,
        CommandCatalog commands,
        IReadOnlyDictionary<int, string>? epicRetroMap = null,
        string? deferredWorkHref = null,
        EpicsModel? epicsModel = null,
        IReadOnlyDictionary<string, string>? hrefMap = null,
        IReadOnlyDictionary<SprintActionItem, int>? crossLinks = null)
    {
        var outputPath = FollowUpSlug.OutputPath(slug);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var title = FollowUpRow.SummarizePlainText(item.Action, maxChars: 80);
        var statusToken = StatusStyles.ForSprint(item.Status);
        var statusLabel = StatusStyles.SprintLabel(item.Status);

        var sb = new StringBuilder();
        AppendShellOpen(sb, nav, outputPath, prefix, title, "Action item",
            ("Open Action Items", SiteNav.ActionItemsOutputPath));

        sb.Append("<main id=\"main-content\" class=\"followup-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Action item</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusToken, statusLabel)}");
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

        if (crossLinks is not null && crossLinks.TryGetValue(item, out var otherEpic))
        {
            sb.Append("  <div class=\"followup-detail-cross\">\n");
            if (epicRetroMap is not null && epicRetroMap.TryGetValue(otherEpic, out var otherRetro))
            {
                var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(otherRetro));
                sb.Append($"    <a class=\"action-item-cross\" href=\"{PathUtil.Html(href)}\">also raised in Epic {otherEpic} retrospective &rarr;</a>\n");
            }
            else
            {
                sb.Append($"    <span class=\"action-item-cross\">also raised in Epic {otherEpic} retrospective</span>\n");
            }
            sb.Append("  </div>\n");
        }

        if (deferredWorkHref is { Length: > 0 } dw && DeferralHeuristics.IsDebtRelated(item.Action))
        {
            var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(dw));
            sb.Append($"  <a class=\"action-item-deferred\" href=\"{PathUtil.Html(href)}\">In deferred-work backlog &rarr;</a>\n");
        }

        sb.Append("</section>\n");

        // Same Next Steps card panel as story pages — labeled resolve prompt + room for alternates.
        var nextSteps = BmadCommands.RenderActionItemNextSteps(item, commands);
        if (nextSteps.Length > 0)
            sb.Append(nextSteps);

        AppendBackLink(sb, prefix + SiteNav.ActionItemsOutputPath, "All open action items");
        sb.Append("</main>\n\n");
        AppendShellClose(sb, prefix);
        return sb.ToString();
    }

    /// <summary>Renders a deferred-work item detail page. Address/Close Next Steps embed
    /// <c>data-copy</c> — do <em>not</em> run through site-level <c>ApplyReferenceLinks</c>
    /// (RequirementLinkifier would corrupt FR mentions inside the attribute). Parser hrefs are
    /// output-root-relative (or deferred-work-page depth); re-prefix for <c>follow-ups/</c>.
    /// [Story 9.11]</summary>
    public static string RenderDeferredPage(
        DeferredWorkItem item,
        string provenanceLabel,
        string? sourceStoryHref,
        string slug,
        SiteNav nav,
        string listOutputPath,
        CommandCatalog? commands = null)
    {
        var outputPath = FollowUpSlug.OutputPath(slug);
        var prefix = PathUtil.RelativePrefix(outputPath);
        var title = FollowUpRow.SummarizeFromHtml(item.BodyHtml, maxChars: 80);
        var (statusToken, statusLabel) = item.Resolved
            ? ("done", "Resolved")
            : (StatusStyles.ForSprint("open"), "Open");

        var sb = new StringBuilder();
        AppendShellOpen(sb, nav, outputPath, prefix, title, "Deferred work",
            ("Deferred Work", PathUtil.NormalizeSlashes(listOutputPath)));

        sb.Append("<main id=\"main-content\" class=\"followup-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Deferred work</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\">{StatusStyles.Badge(statusToken, statusLabel)}");
        sb.Append(StatusStyles.LegendKey());
        if (item.Resolved)
            sb.Append("<span class=\"deferred-resolved-mark\" aria-hidden=\"true\">✓</span>");
        sb.Append("</div>\n</header>\n\n");

        sb.Append("<section class=\"followup-detail-body\">\n");
        sb.Append($"  <div class=\"deferred-item-body followup-detail-fulltext\">{item.BodyHtml}</div>\n");

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
            var label = rr.Contains('.') && !rr.Contains('-') ? $"Story {rr}" : rr;
            var href = FollowUpGeometry.ApplyLinkPrefix(prefix, rh);
            sb.Append($"  <a class=\"deferred-item-resolving\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">Resolving: {PathUtil.Html(label)} &rarr;</a>\n");
        }
        else if (item.ResolvingRef is { Length: > 0 } rr2)
        {
            sb.Append($"  <span class=\"deferred-item-resolving\">Resolving: {PathUtil.Html(rr2)}</span>\n");
        }

        sb.Append("</section>\n");

        var nextSteps = BmadCommands.RenderDeferredItemNextSteps(item, commands ?? CommandCatalog.Empty);
        if (nextSteps.Length > 0)
            sb.Append(nextSteps);

        var listHref = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(listOutputPath));
        AppendBackLink(sb, listHref, "All deferred work");
        sb.Append("</main>\n\n");
        AppendShellClose(sb, prefix);
        return sb.ToString();
    }

    private static void AppendShellOpen(
        StringBuilder sb, SiteNav nav, string outputPath, string prefix, string title, string kindLabel,
        (string Label, string Href) listCrumb)
    {
        sb.Append(PathUtil.RenderHeadOpen(
            $"{title} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"{kindLabel} follow-up for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            (listCrumb.Label, listCrumb.Href),
            (title, null),
        }));
    }

    private static void AppendShellClose(StringBuilder sb, string prefix)
    {
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
    }

    private static void AppendBackLink(StringBuilder sb, string href, string label)
    {
        sb.Append($"<p class=\"followup-detail-back\"><a href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">&larr; {PathUtil.Html(label)}</a></p>\n");
    }
}
