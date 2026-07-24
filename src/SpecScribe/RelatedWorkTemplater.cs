using System.Text;

namespace SpecScribe;

/// <summary>Renders the Story 20.3 <em>Related work</em> pane — the explorer's sibling region listing the
/// work-graph nodes related to the current selection, grouped by edge kind.
///
/// <para><b>The server ships the whole truth (AC #2 / NFR8).</b> Every selectable node's groups are rendered into
/// the DOM at generation time, and with JavaScript off ALL of them are visible — the pane reads as a compact
/// per-epic digest of <c>work-graph.html</c>, which is the documented no-JS default view. The client
/// (<c>specscribe.js</c>) only ever REVEALS a slice of this markup as the selection changes; it fetches nothing,
/// counts nothing, and invents no destination. This mirrors the <c>work-graph-scope-select</c> idiom the work-graph
/// page already uses ("with JS off every section shows").</para>
///
/// <para><b>Empty means absent, not blank.</b> A project with no work-graph signal renders NO pane at all
/// (<see cref="RelatedWorkModel.IsEmpty"/> — the same NFR8 gate <c>work-graph.html</c> uses), so the panel can
/// never ship as permanent dead chrome on a young project. The designed empty state is for the other case: a
/// selection that exists but has no edges. [Story 20.3]</summary>
public static class RelatedWorkTemplater
{
    /// <summary>DOM attribute marking the pane root — the one place the class ↔ script contract is named, mirroring
    /// <see cref="Charts.SunburstExplorerDataId"/>. [Story 20.3]</summary>
    public const string PaneAttribute = "data-related-pane";

    /// <summary>Renders the whole pane, or "" when there is nothing to relate (NFR8: absent data → absent surface).
    /// <paramref name="workGraphHref"/> is the host-relative path to <c>work-graph.html</c>, or null when that page
    /// was not generated — in which case the "see the full graph" affordances are omitted rather than emitted as
    /// links to a page that does not exist (Epic 7's guarded-href discipline).</summary>
    public static string RenderPane(RelatedWorkModel model, string? workGraphHref)
    {
        if (model.IsEmpty) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<aside class=\"chart-panel related-work-panel wm-panel wm-show-overview wm-show-track\" ");
        sb.Append(PaneAttribute);
        sb.Append(" aria-labelledby=\"related-work-h\">\n");
        sb.Append("<div class=\"chart-panel-header-row\"><h3 id=\"related-work-h\">Related work</h3>");
        if (workGraphHref is { Length: > 0 })
            sb.Append($"<a class=\"view-epic-link\" href=\"{PathUtil.Html(workGraphHref)}\">View the full work graph &rarr;</a>");
        sb.Append("</div>\n");
        sb.Append("<p class=\"related-work-intro\">Where each epic's follow-up work came from, and what closed it. Drill into the chart above to narrow this to one scope; with the whole project in view, every scope's connections are listed.</p>\n");
        // Selection changes are announced here rather than on the sunburst's own live region: two live regions
        // updating from one activation would talk over each other. [Story 20.3]
        sb.Append("<div class=\"related-work-live sr-only\" aria-live=\"polite\"></div>\n");

        // `data-related-node` carries the island id verbatim (that is the join key); the derived DOM id is reduced
        // to a safe alphabet, which can collide (`20.2` and `20-2` both reduce to `20-2`), so disambiguate here —
        // a duplicate `id` would break the `aria-labelledby` pairing for one of the two sections.
        var usedDomIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in model.Nodes)
        {
            var domId = "rw-" + DomSafe(node.IslandId);
            var unique = domId;
            for (var n = 2; !usedDomIds.Add(unique); n++) unique = $"{domId}-{n}";
            sb.Append(RenderNode(node, unique, workGraphHref));
        }

        // Designed empty state (AC #2) for a selection that exists but has no edges. Server-rendered and hidden by
        // default: with JS off there is no selection, so every scope above is showing and this would be a lie.
        sb.Append("<p class=\"related-work-empty\" data-related-empty hidden>No related work items for this selection.</p>\n");

        if (model.Overflow > 0)
        {
            var more = workGraphHref is { Length: > 0 }
                ? $" They are listed on the <a href=\"{PathUtil.Html(workGraphHref)}\">work graph</a>."
                : string.Empty;
            sb.Append($"<p class=\"related-work-overflow\">{model.Overflow} further follow-up {Charts.Plural(model.Overflow, "item", "items")} sit beyond the work graph's per-epic draw limit and are not shown here.{more}</p>\n");
        }

        sb.Append("</aside>\n\n");
        return sb.ToString();
    }

    private static string RenderNode(RelatedWorkNode node, string domId, string? workGraphHref)
    {
        var sb = new StringBuilder();
        sb.Append($"<section class=\"related-node\" data-related-node=\"{PathUtil.Html(node.IslandId)}\" aria-labelledby=\"{domId}-h\">\n");
        sb.Append($"  <h4 class=\"related-node-title\" id=\"{domId}-h\">");
        // Guarded href: a node the generator produced no page for renders as plain text, never a dead link.
        if (node.Href is { Length: > 0 })
            sb.Append($"<a href=\"{PathUtil.Html(node.Href)}\">{PathUtil.Html(node.Label)}</a>");
        else
            sb.Append(PathUtil.Html(node.Label));
        sb.Append("</h4>\n");

        AppendGroups(sb, node.Groups, node.ScopeAnchor, workGraphHref, indent: "  ");

        // Nodes the chart drew no wedge for but whose relationships belong in this scope — each under its OWN name,
        // so a story's "Resolved by this" is never mis-attributed to the epic hosting it. Without this fold, every
        // Resolves edge on the live portal would be invisible: they all land on resolver stories, and most of those
        // sit in density-collapsed epics with no wedge. [Story 20.3; Story 20.1 spike §1a rule 2]
        foreach (var subject in node.Subjects)
        {
            sb.Append("  <div class=\"related-subject\">\n");
            sb.Append("    <h5 class=\"related-subject-title\">");
            if (subject.Href is { Length: > 0 })
                sb.Append($"<a href=\"{PathUtil.Html(subject.Href)}\">{PathUtil.Html(subject.Label)}</a>");
            else
                sb.Append(PathUtil.Html(subject.Label));
            sb.Append("</h5>\n");
            AppendGroups(sb, subject.Groups, node.ScopeAnchor, workGraphHref, indent: "    ");
            sb.Append("  </div>\n");
        }

        sb.Append("</section>\n");
        return sb.ToString();
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
                // No kind chip, and no colour signal to reinforce: RelatedWork.NodeText already names the kind IN
                // THE LABEL for exactly the kinds that need it ("Deferred item: …", "Action item: …", "Source: …")
                // while an epic/story/retro label is self-describing. A separate chip repeated the word — a screen
                // reader read "Story Story 19.1" — so it is the label, not decoration, that carries the type here.
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
                // Truncation is reported, never silent: the pane caps rows so the dashboard stays readable, and
                // says exactly how many it withheld. [Story 20.3]
                var link = workGraphHref is { Length: > 0 }
                    ? $"<a href=\"{PathUtil.Html(workGraphHref)}#{PathUtil.Html(scopeAnchor)}\">See all on the work graph &rarr;</a>"
                    : string.Empty;
                sb.Append($"{indent}  <p class=\"related-more\">+{group.Hidden} more not shown. {link}</p>\n");
            }
            sb.Append($"{indent}</div>\n");
        }
    }

    /// <summary>Island ids come from author-controlled markdown (a story id is a <c>### Story N.M:</c> heading), so
    /// they can carry dots, spaces or worse. The id-namespace VALUE stays verbatim in <c>data-related-node</c> —
    /// that is what the client joins on — and only the derived DOM <c>id</c> is reduced to a safe alphabet, so a
    /// heading can never produce a malformed <c>aria-labelledby</c> pair. [Story 20.3]</summary>
    private static string DomSafe(string islandId)
    {
        var sb = new StringBuilder(islandId.Length);
        foreach (var ch in islandId)
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
        return sb.Length > 0 ? sb.ToString() : "node";
    }
}
