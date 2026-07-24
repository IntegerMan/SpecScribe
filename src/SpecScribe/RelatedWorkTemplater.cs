using System.Text;

namespace SpecScribe;

/// <summary>Renders the Story 20.3 details rail — a compact card beside the explorer sunburst that augments the
/// current selection: the node's name, a one-line summary, a single most-relevant AI action (a read-only BMad
/// command badge, AD-6), and a link to its full detail page. When nothing is selected it shows a project-level
/// default card and a prompt to pick a node.
///
/// <para><b>Progressive enhancement (AC #2 / NFR8).</b> Every card is server-rendered. With JavaScript OFF the rail
/// shows the project card plus every scope's card stacked, and each card's relationship groups are expanded in a
/// native <c>&lt;details&gt;</c> — the complete work-graph relationship data, never JS-gated. With JavaScript ON
/// (<c>data-related-ready</c> on the pane) the CSS collapses that to the fancy single-card behaviour: the project
/// card by default, one scope card on selection, its <c>&lt;details&gt;</c> hidden in favour of the "View details"
/// link. The client only ever toggles which card is current; it fetches nothing and computes no count.</para>
///
/// <para><b>Empty means absent.</b> A project with no work-graph signal renders NO rail
/// (<see cref="RelatedWorkPaneModel.IsEmpty"/>) — never dead chrome. [Story 20.3]</summary>
public static class RelatedWorkTemplater
{
    /// <summary>DOM attribute marking the rail root — the one place the class ↔ script contract is named. [Story 20.3]</summary>
    public const string PaneAttribute = "data-related-pane";

    public static string RenderPane(RelatedWorkPaneModel model)
    {
        if (model.IsEmpty) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<aside class=\"chart-panel related-work-panel wm-panel wm-show-overview wm-show-track\" ");
        sb.Append(PaneAttribute);
        sb.Append(" aria-labelledby=\"related-work-h\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3 id=\"related-work-h\">Related work</h3>");
        if (model.WorkGraphHref is { Length: > 0 } wg)
            sb.Append($"<a class=\"view-epic-link\" href=\"{PathUtil.Html(wg)}\">View the full work graph &rarr;</a>");
        sb.Append("</div>\n");
        // Selection changes announce here, not on the sunburst's own live region — two live regions updating from
        // one activation would talk over each other. [Story 20.3]
        sb.Append("<div class=\"related-work-live sr-only\" aria-live=\"polite\"></div>\n");

        RenderProjectCard(sb, model.Project);

        foreach (var card in model.Cards)
            RenderCard(sb, card, model.WorkGraphHref);

        // Designed empty state (AC #2) for a selection whose node has no card — revealed by JS only. Server-rendered
        // hidden: with JS off there is no selection, so every scope's card is already showing.
        sb.Append("<p class=\"related-work-empty\" data-related-empty hidden>No related work items for this selection.</p>\n");

        sb.Append("</aside>\n\n");
        return sb.ToString();
    }

    private static void RenderProjectCard(StringBuilder sb, RelatedProjectCard card)
    {
        sb.Append("<div class=\"related-card related-card-project\" data-related-default>\n");
        sb.Append($"  <h4 class=\"related-card-title\">{PathUtil.Html(card.Title)}</h4>\n");
        sb.Append($"  <p class=\"related-card-summary\">{PathUtil.Html(card.Summary)}</p>\n");
        AppendAction(sb, card.PrimaryCommand);
        sb.Append($"  <p class=\"related-card-hint\">{PathUtil.Html(card.Hint)}</p>\n");
        sb.Append("</div>\n");
    }

    private static void RenderCard(StringBuilder sb, RelatedCard card, string? workGraphHref)
    {
        sb.Append($"<article class=\"related-card\" data-related-node=\"{PathUtil.Html(card.IslandId)}\">\n");
        // Name. The KindWord leads as a muted eyebrow (stated in words, never colour) so an epic vs story reads at a
        // glance without a coloured badge.
        sb.Append($"  <p class=\"related-card-kind\">{PathUtil.Html(card.KindWord)}</p>\n");
        sb.Append($"  <h4 class=\"related-card-title\">{PathUtil.Html(card.Title)}</h4>\n");
        sb.Append($"  <p class=\"related-card-summary\">{PathUtil.Html(card.Summary)}</p>\n");
        AppendAction(sb, card.PrimaryCommand);
        // The "more details" link — a distinct button to the node's own page, where its full work-graph tab lives.
        if (card.DetailHref is { Length: > 0 } href)
            sb.Append($"  <a class=\"related-card-more\" href=\"{PathUtil.Html(href)}\">View details &rarr;</a>\n");

        // The relationship groups — the JS-off relationship block (AC #2). A native <details>, expanded with JS off
        // (CSS hides it entirely once JS takes over, since the "View details" link then carries the reader onward).
        var rel = card.Relationships;
        if (rel.Groups.Count > 0 || rel.Subjects.Count > 0)
        {
            sb.Append("  <details class=\"related-card-full\">\n");
            sb.Append($"    <summary>Related items ({rel.EntryCount})</summary>\n");
            AppendGroups(sb, rel.Groups, rel.ScopeAnchor, workGraphHref, indent: "    ");
            foreach (var subject in rel.Subjects)
            {
                sb.Append("    <div class=\"related-subject\">\n");
                sb.Append("      <p class=\"related-subject-title\">");
                if (subject.Href is { Length: > 0 } sh)
                    sb.Append($"<a href=\"{PathUtil.Html(sh)}\">{PathUtil.Html(subject.Label)}</a>");
                else
                    sb.Append(PathUtil.Html(subject.Label));
                sb.Append("</p>\n");
                AppendGroups(sb, subject.Groups, rel.ScopeAnchor, workGraphHref, indent: "      ");
                sb.Append("    </div>\n");
            }
            sb.Append("  </details>\n");
        }

        sb.Append("</article>\n");
    }

    private static void AppendAction(StringBuilder sb, string? command)
    {
        var badge = BmadCommands.RenderPrimaryActionBadge(command);
        if (badge.Length == 0) return;
        sb.Append("  <div class=\"related-card-action\">");
        sb.Append(badge);
        sb.Append("</div>\n");
    }

    private static void AppendGroups(
        StringBuilder sb,
        IReadOnlyList<RelatedWorkGroup> groups,
        string scopeAnchor,
        string? workGraphHref,
        string indent)
    {
        foreach (var group in groups)
        {
            sb.Append($"{indent}<div class=\"related-group\">\n");
            sb.Append($"{indent}  <p class=\"related-group-title\">{PathUtil.Html(group.Heading)}</p>\n");
            sb.Append($"{indent}  <ul class=\"related-list\">\n");
            foreach (var entry in group.Entries)
            {
                // The node kind is already named in RelatedWork.NodeText where it matters ("Deferred item: …"),
                // so no colour-only signal here.
                sb.Append($"{indent}    <li class=\"related-row\">");
                var title = entry.Title is { Length: > 0 } ? $" title=\"{PathUtil.Html(entry.Title)}\"" : string.Empty;
                if (entry.Href is { Length: > 0 })
                    sb.Append($"<a href=\"{PathUtil.Html(entry.Href)}\"{title}>{PathUtil.Html(entry.Label)}</a>");
                else
                    sb.Append($"<span class=\"related-chip\"{title}>{PathUtil.Html(entry.Label)}</span>");
                sb.Append("</li>\n");
            }
            sb.Append($"{indent}  </ul>\n");
            if (group.Hidden > 0)
            {
                var link = workGraphHref is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(workGraphHref)}#{PathUtil.Html(scopeAnchor)}\">See all on the work graph &rarr;</a>"
                    : string.Empty;
                sb.Append($"{indent}  <p class=\"related-more\">+{group.Hidden} more not shown. {link}</p>\n");
            }
            sb.Append($"{indent}</div>\n");
        }
    }
}
