using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Renders the open retrospective action-items page (<c>action-items.html</c>): items grouped by
/// source epic retrospective (age-ordered), with visible-text resolving links, conservative cross-retro
/// near-duplicate notes, and a "Resolve with AI" command. [Story 2.3; Story 9.6]</summary>
public static class ActionItemsTemplater
{
    public static string RenderPage(
        IReadOnlyList<SprintActionItem> openItems,
        IReadOnlyDictionary<int, string>? epicRetroMap,
        CommandCatalog commands,
        SiteNav nav,
        string? deferredWorkHref = null,
        ProjectCounts? counts = null,
        EpicsModel? epicsModel = null,
        IReadOnlyDictionary<string, string>? hrefMap = null)
    {
        var outputPath = SiteNav.ActionItemsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);
        var quickDev = commands.Command("quick-dev");
        counts ??= ProjectCounts.Build(
            ProgressModel.Empty,
            new SprintStatus { Entries = Array.Empty<SprintEntry>(), ActionItems = openItems },
            WorkInventory.Empty);
        var openCount = counts.OpenActionItems;

        var groups = GroupByEpic(openItems);
        var crossLinks = FindNearDuplicates(openItems);

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
        sb.Append("  <h1>Open Action Items");
        sb.Append(StatusStyles.LegendKey());
        sb.Append("</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {openCount} open {Charts.Plural(openCount, "item", "items")} &middot; from retrospectives</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\">\n<section class=\"action-items-wrap\">\n");

        foreach (var group in groups)
        {
            sb.Append(RenderGroupHeading(group.EpicNumber, epicRetroMap));
            sb.Append("<ul class=\"followup-rows-list action-items-list\">\n");
            foreach (var item in group.Items)
            {
                RenderCard(sb, item, epicRetroMap, deferredWorkHref, quickDev, epicsModel, hrefMap, prefix, crossLinks);
            }
            sb.Append("</ul>\n");
        }

        sb.Append("</section>\n</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Groups open items by epic number ascending; null-epic items trail as "Unattributed".
    /// Preserves file order within each group. Pure and order-stable. [Story 9.6]</summary>
    public static IReadOnlyList<ActionItemGroup> GroupByEpic(IReadOnlyList<SprintActionItem> openItems)
    {
        var attributed = openItems
            .Select((item, index) => (item, index))
            .Where(t => t.item.EpicNumber is not null)
            .GroupBy(t => t.item.EpicNumber!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new ActionItemGroup(
                g.Key,
                g.OrderBy(t => t.index).Select(t => t.item).ToList()))
            .ToList();

        var unattributed = openItems.Where(i => i.EpicNumber is null).ToList();
        if (unattributed.Count > 0)
            attributed.Add(new ActionItemGroup(null, unattributed));

        return attributed;
    }

    /// <summary>Conservative near-duplicate detector: pairs from different epics whose normalized token
    /// overlap is high. Prefer false negatives. Pure and deterministic. [Story 9.6]</summary>
    public static IReadOnlyDictionary<SprintActionItem, int> FindNearDuplicates(IReadOnlyList<SprintActionItem> openItems)
    {
        // Maps each item to the counterpart epic number it cross-links to (first match wins for stability).
        var result = new Dictionary<SprintActionItem, int>();
        for (var i = 0; i < openItems.Count; i++)
        {
            var a = openItems[i];
            if (a.EpicNumber is null) continue;
            for (var j = i + 1; j < openItems.Count; j++)
            {
                var b = openItems[j];
                if (b.EpicNumber is null || b.EpicNumber == a.EpicNumber) continue;
                if (!AreNearDuplicates(a.Action, b.Action)) continue;
                result.TryAdd(a, b.EpicNumber.Value);
                result.TryAdd(b, a.EpicNumber.Value);
            }
        }
        return result;
    }

    /// <summary>True when two action texts are highly similar (Jaccard ≥ 0.45 on significant tokens AND
    /// ≥ 6 shared tokens). Tuned to the Epic 1↔Epic 2 heatmap-debt pair.</summary>
    public static bool AreNearDuplicates(string a, string b)
    {
        var ta = SignificantTokens(a);
        var tb = SignificantTokens(b);
        if (ta.Count == 0 || tb.Count == 0) return false;

        var shared = ta.Intersect(tb, StringComparer.Ordinal).ToList();
        if (shared.Count < 6) return false;

        var union = ta.Union(tb, StringComparer.Ordinal).Count();
        if (union == 0) return false;
        var jaccard = (double)shared.Count / union;
        return jaccard >= 0.45;
    }

    private static void RenderCard(
        StringBuilder sb,
        SprintActionItem item,
        IReadOnlyDictionary<int, string>? epicRetroMap,
        string? deferredWorkHref,
        string? quickDev,
        EpicsModel? epicsModel,
        IReadOnlyDictionary<string, string>? hrefMap,
        string prefix,
        IReadOnlyDictionary<SprintActionItem, int> crossLinks)
    {
        var summaryPlain = FollowUpRow.SummarizePlainText(item.Action);
        var summaryHtml = FollowUpRefs.LinkifyVisibleText(summaryPlain, epicsModel, hrefMap, prefix);
        var sourceChip = item.EpicNumber is { } en ? $"Epic {en}" : "Unattributed";

        var detail = new StringBuilder();
        detail.Append($"<div class=\"followup-detail-fulltext\">{FollowUpRefs.LinkifyVisibleText(item.Action, epicsModel, hrefMap, prefix)}</div>\n");

        if (crossLinks.TryGetValue(item, out var otherEpic))
        {
            if (epicRetroMap is not null && epicRetroMap.TryGetValue(otherEpic, out var otherRetro))
            {
                detail.Append($"<a class=\"action-item-cross\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(otherRetro))}\">also raised in Epic {otherEpic} retrospective &rarr;</a>\n");
            }
            else
            {
                detail.Append($"<span class=\"action-item-cross\">also raised in Epic {otherEpic} retrospective</span>\n");
            }
        }

        if (deferredWorkHref is { Length: > 0 } dw && DeferralHeuristics.IsDebtRelated(item.Action))
        {
            detail.Append($"<a class=\"action-item-deferred\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(dw))}\">In deferred-work backlog &rarr;</a>\n");
        }

        if (quickDev is { Length: > 0 })
        {
            var epicNote = item.EpicNumber is { } e3 ? $" (Epic {e3})" : string.Empty;
            // Payload uses RAW action text — never the linkified fragment (copy-payload corruption trap).
            var prompt = $"{quickDev} Resolve this retrospective action item{epicNote}: {item.Action}";
            detail.Append("<div class=\"action-item-resolve\">\n");
            detail.Append($"{BmadCommands.RenderLabeledCommand("Resolve with AI", prompt)}\n");
            detail.Append("</div>\n");
        }

        FollowUpRow.Render(
            sb,
            summaryHtml,
            StatusStyles.ForSprint(item.Status),
            StatusStyles.SprintLabel(item.Status),
            PathUtil.Html(sourceChip),
            detail.ToString());
    }

    private static string RenderGroupHeading(int? epicNumber, IReadOnlyDictionary<int, string>? epicRetroMap)
    {
        if (epicNumber is null)
            return "<h2 class=\"action-items-group\">Unattributed</h2>\n";

        var label = $"From the Epic {epicNumber.Value} retrospective";
        if (epicRetroMap is not null && epicRetroMap.TryGetValue(epicNumber.Value, out var retroHref))
        {
            return $"<h2 class=\"action-items-group\"><a href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(retroHref))}\">{PathUtil.Html(label)}</a></h2>\n";
        }
        return $"<h2 class=\"action-items-group\">{PathUtil.Html(label)}</h2>\n";
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "of", "to", "into", "for", "before", "after", "on", "in", "at",
        "with", "from", "by", "as", "is", "are", "was", "were", "be", "been", "this", "that", "these",
        "those", "it", "its", "now", "rather", "than", "across", "two", "same",
    };

    private static readonly Regex TokenSplit = new(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HashSet<string> SignificantTokens(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in TokenSplit.Split(text.ToLowerInvariant()))
        {
            if (raw.Length < 2) continue;
            if (StopWords.Contains(raw)) continue;
            set.Add(raw);
        }
        return set;
    }
}

/// <summary>One epic-retro group on the action-items page. <see cref="EpicNumber"/> is null for the trailing
/// Unattributed bucket. [Story 9.6]</summary>
public sealed record ActionItemGroup(int? EpicNumber, IReadOnlyList<SprintActionItem> Items);
