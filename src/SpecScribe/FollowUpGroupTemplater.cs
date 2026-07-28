using System.Linq;
using System.Text;

namespace SpecScribe;

/// <summary>Standalone templater for generated filtered follow-up group list pages.
/// Reuses <see cref="FollowUpRow"/> scan grammar. Batch Address/Close <c>data-copy</c> lives only in the
/// list-batch pane above the rows — individual rows still carry no per-item command chrome.
/// [Story 9.13; spec-follow-up-list-batch-actions]</summary>
public static class FollowUpGroupTemplater
{
    public static string RenderPage(FollowUpGroupSpec group, SiteNav nav, CommandCatalog? commands = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(group, nav, commands)).Content;

    /// <summary>Builds a group page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract, so the
    /// IR's content region can be COMPOSED (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup +
    /// wayfinding + body) instead of sliced back out of a rendered full page. <see cref="RenderPage"/> is the
    /// unchanged HTML projection of this same model, so the bytes are identical.
    /// ⚠️ The body starts at <c>&lt;header class="doc-header"&gt;</c>, not at <c>&lt;main&gt;</c> — this page emits
    /// its title block BEFORE the landmark, and the old region slice began at the breadcrumb, so the header was
    /// inside the captured region. [Story 23.4 AC #3, finding 1]</summary>
    public static PageView BuildPage(FollowUpGroupSpec group, SiteNav nav, CommandCatalog? commands = null)
    {
        if (group.Count == 0)
            throw new ArgumentException("NFR8: never render an empty follow-up group page.", nameof(group));

        var outputPath = group.OutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <div class=\"story-kicker\">Follow-up group</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(group.Title)}</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(group.Subtitle)} &middot; {group.Count} {Charts.Plural(group.Count, "item", "items")}");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</div>\n</header>\n\n");

        sb.Append("<main id=\"main-content\">\n<section class=\"followup-group-wrap\">\n");
        // Pane lives inside the wrap so it shares the 1040px list column. [spec-follow-up-list-batch-actions]
        sb.Append(RenderListBatchPane(group, commands ?? CommandCatalog.Empty));
        sb.Append("<ul class=\"followup-rows-list js-listable\">\n");
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
                detailHref: detailHref,
                sortName: member.RawSummary ?? PathUtil.StripHtmlTags(member.SummaryHtml),
                sortStatus: member.StatusToken);
        }
        sb.Append("</ul>\n");

        if (group.WholeSiteListHref is { Length: > 0 } listHref
            && group.WholeSiteListLabel is { Length: > 0 } listLabel)
        {
            var href = PathUtil.NormalizeSlashes(prefix + PathUtil.NormalizeSlashes(listHref));
            sb.Append($"  <p class=\"followup-group-all\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(listLabel)} &rarr;</a></p>\n");
        }

        sb.Append("</section>\n</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Doc,
            OutputRelativePath = outputPath,
            Title = $"{group.Title} — {nav.SiteTitle}",
            MetaDescription = $"{group.Title}: {group.Count} {Charts.Plural(group.Count, "item", "items")} for {nav.SiteTitle}.",
            Nav = nav.ToNavigationView(outputPath),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                ("Sprint Status", "sprint.html"),
                (group.Title, null),
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

    /// <summary>Open deferred/action members (never <c>Kind == "direct"</c>) projected as list-batch
    /// prompt entries, in the page's existing display order. [spec-follow-up-list-batch-actions]</summary>
    private static string RenderListBatchPane(FollowUpGroupSpec group, CommandCatalog commands)
    {
        var openDeferred = group.Members
            .Where(m => m.Kind == "deferred" && !m.Resolved)
            .Select(ToListBatchEntry)
            .ToList();
        var openActions = group.Members
            .Where(m => m.Kind == "action" && !m.Resolved)
            .Select(ToListBatchEntry)
            .ToList();

        return BmadCommands.RenderListBatchPane(group.Title, commands, openDeferred, openActions);
    }

    private static BmadCommands.ListBatchEntry ToListBatchEntry(FollowUpGroupMember member)
    {
        var href = member.DetailHref;
        if (href is { Length: > 0 })
        {
            // Site-root-relative cue for prompts (strip page-depth prefixes geometry may carry).
            while (href.StartsWith("../", StringComparison.Ordinal))
                href = href[3..];
            if (href.StartsWith("./", StringComparison.Ordinal))
                href = href[2..];
        }
        return new(
            member.RawSummary ?? PathUtil.StripHtmlTags(member.SummaryHtml),
            member.RawProvenance ?? PathUtil.StripHtmlTags(member.SourceChipHtml),
            href);
    }
}
