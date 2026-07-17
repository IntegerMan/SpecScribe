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
        string title = "Deferred Work")
    {
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
                sb.Append("<section class=\"deferred-work-wrap\">\n");
                sb.Append("  <ul class=\"followup-rows-list deferred-items-list\">\n");
                foreach (var item in unstructured)
                    RenderItem(sb, item, "Deferred work", prefix, detailSlugs);
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

            sb.Append("<section class=\"deferred-work-wrap\">\n");
            if (model.PreambleHtml is { Length: > 0 } preamble)
            {
                sb.Append("<div class=\"deferred-work-preamble doc-body\">\n");
                sb.Append(preamble);
                sb.Append("</div>\n");
            }
            foreach (var group in model.Groups)
            {
                RenderGroup(sb, group, prefix, detailSlugs);
            }
            sb.Append("</section>\n");
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void RenderGroup(StringBuilder sb, DeferredWorkGroup group, string prefix, IReadOnlyDictionary<DeferredWorkItem, string> detailSlugs)
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

        sb.Append("  <ul class=\"followup-rows-list deferred-items-list\">\n");
        foreach (var item in ordered)
            RenderItem(sb, item, group.ProvenanceLabel, prefix, detailSlugs);
        sb.Append("  </ul>\n");
        sb.Append("</section>\n");
    }

    private static void RenderItem(
        StringBuilder sb,
        DeferredWorkItem item,
        string provenanceLabel,
        string prefix,
        IReadOnlyDictionary<DeferredWorkItem, string> detailSlugs)
    {
        var summaryPlain = FollowUpRow.SummarizeFromHtml(item.BodyHtml);
        var summaryHtml = PathUtil.Html(summaryPlain);
        var detailHref = detailSlugs.TryGetValue(item, out var slug)
            ? prefix + FollowUpSlug.OutputPath(slug)
            : null;

        // Teaser: resolving links and full body live on the detail page (Story 9.11).
        var detail = new StringBuilder();
        if (item.ResolvingRef is { Length: > 0 } rr)
        {
            var label = rr.Contains('.') && !rr.Contains('-') ? $"Story {rr}" : rr;
            detail.Append($"<span class=\"deferred-item-resolving\">Resolving: {PathUtil.Html(label)}</span>\n");
        }
        if (item.Resolved)
        {
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
            detailHref: detailHref);
    }
}
