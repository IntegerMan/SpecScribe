using System.Text;

namespace SpecScribe;

/// <summary>Shared list-row anatomy — primary label, optional status badge, metadata chips, one primary
/// link, and a designed empty state — extracted from the Story 9.10 <see cref="FollowUpRow"/> grammar so
/// every index page (requirements, epics/stories, code, ADRs, timeline) can read as one system. Status
/// badges still come from <see cref="StatusStyles"/> (Story 8.2) and counts from <see cref="ProjectCounts"/>
/// (Story 8.3) — this primitive only standardizes the row shell around them. <see cref="FollowUpRow"/>
/// already expresses this exact anatomy (scan line + badge + source chip + one primary affordance), so it is
/// left as-is rather than rewired onto this type — the two are siblings, not parent/child. [Story 10.8]</summary>
public static class ListRow
{
    /// <summary>Renders one <c>&lt;li&gt;</c> list row: summary text, an optional status badge, any number of
    /// metadata chips (already-rendered HTML, e.g. from <see cref="Chip"/>), and one optional primary link
    /// (already-rendered HTML, e.g. from <see cref="PrimaryLink"/>). Used by list-shaped indexes that don't
    /// already have their own established card grammar (ADR landing list, activity timeline). [Story 10.8]</summary>
    public static void Render(
        StringBuilder sb,
        string summaryHtml,
        string? badgeHtml,
        IReadOnlyList<string> chipsHtml,
        string? primaryLinkHtml,
        bool resolved = false,
        string? extraRowClass = null,
        string? sortName = null,
        string? sortDate = null,
        string? sortStatus = null)
    {
        var rowClass = "list-row";
        if (resolved) rowClass += " resolved";
        if (extraRowClass is { Length: > 0 }) rowClass += " " + extraRowClass;

        sb.Append($"  <li class=\"{rowClass}\"");
        if (sortName is { Length: > 0 }) sb.Append($" data-sort-name=\"{PathUtil.Html(sortName)}\"");
        if (sortDate is { Length: > 0 }) sb.Append($" data-sort-date=\"{PathUtil.Html(sortDate)}\"");
        if (sortStatus is { Length: > 0 }) sb.Append($" data-sort-status=\"{PathUtil.Html(sortStatus)}\"");
        sb.Append(">\n");
        sb.Append("    <div class=\"list-row-scan\">\n");
        sb.Append($"      <span class=\"list-row-summary\">{summaryHtml}</span>\n");
        sb.Append("      <div class=\"list-row-meta\">\n");
        if (badgeHtml is { Length: > 0 })
        {
            sb.Append($"        {badgeHtml}\n");
        }
        foreach (var chip in chipsHtml)
        {
            sb.Append($"        {chip}\n");
        }
        if (primaryLinkHtml is { Length: > 0 })
        {
            sb.Append($"        {primaryLinkHtml}\n");
        }
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");
        sb.Append("  </li>\n");
    }

    /// <summary>One metadata chip — a small pill for a secondary fact (a date, a count, a source) that sits
    /// beside the status badge and primary link. <paramref name="contentHtml"/> is already escaped/rendered.</summary>
    public static string Chip(string contentHtml) =>
        $"<span class=\"list-row-chip pill\">{contentHtml}</span>";

    /// <summary>The row's one primary affordance — a "go there" link, always arrow-suffixed so it reads the
    /// same everywhere. <paramref name="href"/> and <paramref name="label"/> must already be HTML-escaped.</summary>
    public static string PrimaryLink(string href, string label, string? extraClass = null)
    {
        var cls = extraClass is { Length: > 0 } ? $"list-row-primary {extraClass}" : "list-row-primary";
        return $"<a class=\"{cls}\" href=\"{href}\">{label} &rarr;</a>";
    }

    /// <summary>The promoted Story 8.6 empty-state convention — <c>.empty-state</c> + <c>.pending-note</c> — as
    /// one call site instead of every surface hand-rolling the same two-class wrapper. <paramref name="cardClass"/>
    /// lets each surface keep its own card class (e.g. <c>epic-card</c>) so existing per-surface empty-state CSS
    /// (border, spacing) still applies unchanged. <paramref name="bodyHtml"/> is already-rendered guidance HTML.</summary>
    public static string EmptyState(string bodyHtml, string cardClass) =>
        $"<div class=\"{cardClass} empty-state\">\n  <div class=\"pending-note\">{bodyHtml}</div>\n</div>\n\n";
}
