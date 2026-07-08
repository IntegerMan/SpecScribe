using System.Text;

namespace SpecScribe;

/// <summary>Renders the open retrospective action-items page (<c>action-items.html</c>): each open item as a
/// row with its text, epic + owner pills, status badge, a link to the retrospective it came from, and a
/// "Resolve with AI" command (a <c>/bmad-quick-dev</c> prompt composed with the action text). Reached from the
/// sprint page's flag button and the home "Retro Action Items" callout. [Story 2.3 retro action items]</summary>
public static class ActionItemsTemplater
{
    public static string RenderPage(IReadOnlyList<SprintActionItem> openItems, IReadOnlyDictionary<int, string>? epicRetroMap, CommandCatalog commands, SiteNav nav, string? deferredWorkHref = null)
    {
        var outputPath = SiteNav.ActionItemsOutputPath;
        var quickDev = commands.Command("quick-dev");

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Open Action Items — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"Open retrospective action items for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Sprint Status", "sprint.html"),
            ("Open Action Items", null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Open Action Items</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {openItems.Count} open {Charts.Plural(openItems.Count, "item", "items")} &middot; from retrospectives</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\">\n<section class=\"action-items-wrap\">\n");
        sb.Append("<ul class=\"action-items-list\">\n");
        foreach (var item in openItems)
        {
            sb.Append("  <li class=\"action-item-card\">\n");
            sb.Append($"    <div class=\"action-item-text\">{PathUtil.Html(item.Action)}</div>\n");

            sb.Append("    <div class=\"action-item-meta\">\n");
            if (item.EpicNumber is { } en)
            {
                sb.Append($"      <span class=\"pill\">Epic {en}</span>\n");
            }
            // Owner intentionally omitted: retro "owners" are LLM-generated personas for the retrospective
            // exercise, not real assignees, so they're noise once the retro doc exists. [Story 2.3 polish #7]
            sb.Append($"      {StatusStyles.Badge(StatusStyles.ForSprint(item.Status), StatusStyles.SprintLabel(item.Status))}\n");

            // Link back to the retrospective this item came from (matched by epic).
            if (item.EpicNumber is { } e2 && epicRetroMap is not null && epicRetroMap.TryGetValue(e2, out var retroHref))
            {
                sb.Append($"      <a class=\"action-item-retro\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(retroHref))}\">From Epic {e2} retrospective &rarr;</a>\n");
            }

            // Debt-related items also point at the deferred-work backlog (where the item asks the work be
            // routed) — only when the text is actually about deferred work / tech debt and a page exists.
            if (deferredWorkHref is { Length: > 0 } dw && IsDebtRelated(item.Action))
            {
                sb.Append($"      <a class=\"action-item-deferred\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(dw))}\">In deferred-work backlog &rarr;</a>\n");
            }
            sb.Append("    </div>\n");

            // "Resolve with AI": a copyable /bmad-quick-dev prompt composed with the action text (when the
            // active module exposes quick-dev). The visible label stays short; the payload carries the intent.
            if (quickDev is { Length: > 0 })
            {
                var epicNote = item.EpicNumber is { } e3 ? $" (Epic {e3})" : string.Empty;
                var prompt = $"{quickDev} Resolve this retrospective action item{epicNote}: {item.Action}";
                sb.Append("    <div class=\"action-item-resolve\">\n");
                sb.Append($"      {BmadCommands.RenderLabeledCommand("Resolve with AI", prompt)}\n");
                sb.Append("    </div>\n");
            }

            sb.Append("  </li>\n");
        }
        sb.Append("</ul>\n</section>\n</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>True when an action item's text is about deferred work / tech debt — the signal for surfacing a
    /// link to the deferred-work backlog page beside it.</summary>
    private static bool IsDebtRelated(string action) =>
        action.Contains("deferred", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("tech debt", StringComparison.OrdinalIgnoreCase) ||
        action.Contains("technical debt", StringComparison.OrdinalIgnoreCase);
}
