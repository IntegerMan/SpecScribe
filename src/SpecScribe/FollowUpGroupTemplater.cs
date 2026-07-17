using System.Text;

namespace SpecScribe;

/// <summary>Standalone templater for generated filtered follow-up group list pages.
/// Reuses <see cref="FollowUpRow"/> scan grammar; no Resolve <c>data-copy</c> on group pages. [Story 9.13]</summary>
public static class FollowUpGroupTemplater
{
    public static string RenderPage(FollowUpGroupSpec group, SiteNav nav)
    {
        if (group.Count == 0)
            throw new ArgumentException("NFR8: never render an empty follow-up group page.", nameof(group));

        var outputPath = group.OutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{group.Title} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"{group.Title}: {group.Count} {Charts.Plural(group.Count, "item", "items")} for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Sprint Status", "sprint.html"),
            (group.Title, null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <div class=\"story-kicker\">Follow-up group</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(group.Title)}</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(group.Subtitle)} &middot; {group.Count} {Charts.Plural(group.Count, "item", "items")}");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</div>\n</header>\n\n");

        sb.Append("<main id=\"main-content\">\n<section class=\"followup-group-wrap\">\n");
        sb.Append("<ul class=\"followup-rows-list\">\n");
        foreach (var member in group.Members)
        {
            // Detail hrefs from geometry may already carry a depth prefix; normalize for this page.
            var detailHref = FollowUpGeometry.ApplyLinkPrefix(prefix, member.DetailHref);
            FollowUpRow.Render(
                sb,
                member.SummaryHtml,
                member.StatusToken,
                member.StatusLabel,
                member.SourceChipHtml,
                member.DetailBodyHtml,
                resolved: member.Resolved,
                detailHref: detailHref);
        }
        sb.Append("</ul>\n");

        if (group.WholeSiteListHref is { Length: > 0 } listHref
            && group.WholeSiteListLabel is { Length: > 0 } listLabel)
        {
            var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(listHref));
            sb.Append($"  <p class=\"followup-group-all\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(listLabel)} &rarr;</a></p>\n");
        }

        sb.Append("</section>\n</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
