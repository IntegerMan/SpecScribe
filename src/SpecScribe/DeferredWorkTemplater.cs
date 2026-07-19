using System.Text;

namespace SpecScribe;

/// <summary>Renders the deferred-work page as structured provenance cards (sibling of action-items),
/// or falls back to the plain markdown body when the note isn't in the Deferred-from shape. [Story 9.6]</summary>
public static class DeferredWorkTemplater
{
    public static string RenderPage(
        DeferredWorkModel model,
        SiteNav nav,
        string outputRelativePath,
        string title = "Deferred Work",
        CommandCatalog? commands = null,
        EpicsModel? epicsModel = null,
        IReadOnlyDictionary<string, string>? hrefMap = null)
    {
        var catalog = commands ?? CommandCatalog.Empty;
        var prefix = PathUtil.RelativePrefix(outputRelativePath);
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{title} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Deferred work for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputRelativePath));
        sb.Append(SiteNav.RenderBreadcrumb(outputRelativePath, new (string, string?)[]
        {
            ("Home", prefix + "index.html"),
            (title, null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; real-but-not-now</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\">\n");

        if (!model.IsStructured)
        {
            var unstructured = FollowUpGeometry.UnstructuredItems(model.PlainBodyHtml);
            if (unstructured.Count == 0)
            {
                sb.Append("<article class=\"doc-body deferred-work-fallback\">\n");
                sb.Append(model.PlainBodyHtml ?? string.Empty);
                sb.Append("</article>\n");
            }
            else
            {
                // Per-item detail deep links even without ## Deferred from: headings (Story 9.11).
                var pairs = unstructured.Select(i => (Item: i, ProvenanceLabel: "Deferred work")).ToList();
                var detailSlugs = FollowUpSlug.AssignDeferredSlugs(pairs);
                var openEntries = unstructured
                    .Where(i => !i.Resolved)
                    .Select(i => ToListBatchEntry(i, "Deferred work", detailSlugs))
                    .ToList();
                sb.Append("<section class=\"deferred-work-wrap\">\n");
                sb.Append(BmadCommands.RenderListBatchPane(title, catalog, openDeferred: openEntries));
                sb.Append("  <ul class=\"followup-rows-list deferred-items-list js-listable\">\n");
                foreach (var item in unstructured)
                    RenderItem(sb, item, "Deferred work", prefix, detailSlugs, epicsModel, hrefMap);
                sb.Append("  </ul>\n");
                sb.Append("</section>\n");
            }
        }
        else
        {
            var pairs = model.Groups
                .SelectMany(g => g.Items.Select(i => (Item: i, ProvenanceLabel: g.ProvenanceLabel)))
                .ToList();
            var detailSlugs = FollowUpSlug.AssignDeferredSlugs(pairs);
            var openEntries = model.Groups
                .SelectMany(g => g.Items
                    .Where(i => !i.Resolved)
                    .Select(i => ToListBatchEntry(i, g.SourceKey ?? g.ProvenanceLabel, detailSlugs)))
                .ToList();

            sb.Append("<section class=\"deferred-work-wrap\">\n");
            sb.Append(BmadCommands.RenderListBatchPane(title, catalog, openDeferred: openEntries));
            if (model.PreambleHtml is { Length: > 0 } preamble)
            {
                sb.Append("<div class=\"deferred-work-preamble doc-body\">\n");
                sb.Append(preamble);
                sb.Append("</div>\n");
            }
            foreach (var group in model.Groups)
            {
                RenderGroup(sb, group, prefix, detailSlugs, epicsModel, hrefMap);
            }
            sb.Append("</section>\n");
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Projects one open deferred item for the list-batch Address/Close pane — raw summary text
    /// (never linkified) plus its detail deep link when one exists. <paramref name="provenanceKey"/> is the
    /// group's <see cref="DeferredWorkGroup.SourceKey"/> when known, else its heading label.
    /// [spec-follow-up-list-batch-actions]</summary>
    private static BmadCommands.ListBatchEntry ToListBatchEntry(
        DeferredWorkItem item, string provenanceKey,
        IReadOnlyDictionary<DeferredWorkItem, string> detailSlugs)
    {
        var summary = FollowUpRow.SummarizeFromHtml(item.BodyHtml, maxChars: 200);
        if (string.IsNullOrWhiteSpace(summary))
            summary = PathUtil.StripHtmlTags(item.BodyHtml).Trim();
        if (string.IsNullOrWhiteSpace(summary))
            summary = "(no deferred text)";
        // Site-root-relative detail cue (no page-depth prefix) so prompts match group/story Address deferred.
        var href = detailSlugs.TryGetValue(item, out var slug) ? FollowUpSlug.OutputPath(slug) : null;
        return new BmadCommands.ListBatchEntry(summary, provenanceKey, href);
    }

    private static void RenderGroup(
        StringBuilder sb, DeferredWorkGroup group, string prefix,
        IReadOnlyDictionary<DeferredWorkItem, string> detailSlugs,
        EpicsModel? epicsModel, IReadOnlyDictionary<string, string>? hrefMap)
    {
        sb.Append("<section class=\"deferred-group\">\n");
        sb.Append("  <h2 class=\"deferred-group-title\">");
        sb.Append("Deferred from: ");
        if (group.SourceStoryHref is { Length: > 0 } href)
            sb.Append($"<a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(group.ProvenanceLabel)}</a>");
        else
            sb.Append(PathUtil.Html(group.ProvenanceLabel));
        sb.Append("</h2>\n");

        // Open items first so outstanding promises lead; resolved trail with distinct treatment.
        var ordered = group.Items
            .Select((item, index) => (item, index))
            .OrderBy(t => t.item.Resolved ? 1 : 0)
            .ThenBy(t => t.index)
            .Select(t => t.item);

        sb.Append("  <ul class=\"followup-rows-list deferred-items-list js-listable\">\n");
        foreach (var item in ordered)
            RenderItem(sb, item, group.ProvenanceLabel, prefix, detailSlugs, epicsModel, hrefMap);
        sb.Append("  </ul>\n");
        sb.Append("</section>\n");
    }

    private static void RenderItem(
        StringBuilder sb,
        DeferredWorkItem item,
        string provenanceLabel,
        string prefix,
        IReadOnlyDictionary<DeferredWorkItem, string> detailSlugs,
        EpicsModel? epicsModel,
        IReadOnlyDictionary<string, string>? hrefMap)
    {
        var summaryPlain = FollowUpRow.SummarizeFromHtml(item.BodyHtml);
        // Visible text only — same selective linkify action-items uses; never whole-page ApplyReferenceLinks.
        var summaryHtml = FollowUpRefs.LinkifyVisibleText(summaryPlain, epicsModel, hrefMap, prefix);
        var detailHref = detailSlugs.TryGetValue(item, out var slug)
            ? prefix + FollowUpSlug.OutputPath(slug)
            : null;

        // With a detail URL: scan + View detail only (code review 9.10). Without: full disclosure (9.10 seam).
        var detail = new StringBuilder();
        if (detailHref is null)
        {
            // Keep parser BodyHtml as-is for structure; summaries above carry selective Story/Epic/FR links.
            detail.Append($"<div class=\"deferred-item-body\">{item.BodyHtml}</div>\n");
            if (item.ResolvingHref is { Length: > 0 } rh && item.ResolvingRef is { Length: > 0 } rr)
            {
                var label = FollowUpRefs.ResolvingLabel(rr);
                var href = FollowUpGeometry.ApplyLinkPrefix(prefix, rh);
                detail.Append($"<a class=\"deferred-item-resolving\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(href))}\">Resolving: {PathUtil.Html(label)} &rarr;</a>\n");
            }
            else if (item.ResolvingRef is { Length: > 0 } rr2)
            {
                var label = FollowUpRefs.ResolvingLabel(rr2);
                detail.Append($"<span class=\"deferred-item-resolving\">Resolving: {PathUtil.Html(label)}</span>\n");
            }
            if (item.Resolved)
                detail.Append("<span class=\"deferred-resolved-mark\" aria-hidden=\"true\">✓</span>\n");
        }

        var (statusToken, statusLabel) = item.Resolved
            ? ("done", "Resolved")
            : (StatusStyles.ForSprint("open"), "Open");

        FollowUpRow.Render(
            sb,
            summaryHtml,
            statusToken,
            statusLabel,
            PathUtil.Html(provenanceLabel),
            detail.ToString(),
            resolved: item.Resolved,
            detailHref: detailHref,
            sortName: summaryPlain,
            sortStatus: statusToken);
    }
}
