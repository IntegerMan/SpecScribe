using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Shared scan-first row grammar for action-items and deferred-work list pages.
/// Heavy per-item detail lives in a collapsed <c>&lt;details&gt;</c> until Story 9.11 detail URLs land. [Story 9.10]</summary>
public static class FollowUpRow
{
    private static readonly Regex SummaryField = new(
        @"^\s*summary:\s*(?<text>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex MetadataLine = new(
        @"^\s*(?:source_spec|evidence)\s*:.*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Derives a short plain-text lead from existing authored text — prefers an authored
    /// <c>summary:</c> field when present, otherwise the first non-metadata sentence. No new authoring
    /// schema. [Story 9.10]</summary>
    public static string SummarizePlainText(string plainText, int maxChars = 120)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return string.Empty;

        var summary = SummaryField.Match(plainText);
        if (summary.Success)
            return TruncatePlainText(summary.Groups["text"].Value.Trim(), maxChars);

        var withoutMeta = MetadataLine.Replace(plainText, "");
        var trimmed = withoutMeta.Trim();
        if (trimmed.Length == 0) trimmed = plainText.Trim();

        var end = FindFirstSentenceEnd(trimmed);
        var lead = end > 0 ? trimmed[..end].Trim() : trimmed;
        return TruncatePlainText(lead, maxChars);
    }

    /// <summary>Strips HTML then summarizes. Used for deferred-work item bodies. [Story 9.10]</summary>
    public static string SummarizeFromHtml(string html, int maxChars = 120) =>
        SummarizePlainText(PathUtil.StripHtmlTags(html), maxChars);

    /// <summary>Renders one scan-first follow-up row: summary + status + source chip + primary affordance,
    /// with heavy detail collapsed in <c>&lt;details&gt;</c>. [Story 9.10]</summary>
    public static void Render(
        StringBuilder sb,
        string summaryHtml,
        string statusToken,
        string statusLabel,
        string sourceChipHtml,
        string detailBodyHtml,
        bool resolved = false,
        string? detailHref = null)
    {
        var rowClass = resolved ? "followup-row resolved" : "followup-row";
        sb.Append($"  <li class=\"{rowClass}\">\n");
        sb.Append("    <div class=\"followup-row-scan\">\n");
        sb.Append($"      <span class=\"followup-row-summary\">{summaryHtml}</span>\n");
        // Meta cluster stays visually adjacent (status + source + primary) so wide viewports
        // don't leave a scan gap between the title and its actions.
        sb.Append("      <div class=\"followup-row-meta\">\n");
        sb.Append($"        {StatusStyles.Badge(statusToken, statusLabel)}\n");
        sb.Append($"        <span class=\"followup-row-source pill\">{sourceChipHtml}</span>\n");

        if (detailHref is { Length: > 0 })
        {
            sb.Append($"        <a class=\"followup-row-primary\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(detailHref))}\">View detail &rarr;</a>\n");
            sb.Append("      </div>\n");
            sb.Append("    </div>\n");
            if (detailBodyHtml.Length > 0)
            {
                sb.Append("    <details class=\"followup-row-detail\">\n");
                sb.Append("      <summary class=\"followup-row-detail-toggle\">More</summary>\n");
                sb.Append($"      <div class=\"followup-row-detail-body\">{detailBodyHtml}</div>\n");
                sb.Append("    </details>\n");
            }
        }
        else if (detailBodyHtml.Length > 0)
        {
            sb.Append("        <details class=\"followup-row-detail followup-row-detail--primary\">\n");
            sb.Append("          <summary class=\"followup-row-primary\">View detail</summary>\n");
            sb.Append($"          <div class=\"followup-row-detail-body\">{detailBodyHtml}</div>\n");
            sb.Append("        </details>\n");
            sb.Append("      </div>\n");
            sb.Append("    </div>\n");
        }
        else
        {
            // No detail href and no body — omit empty primary (I/O: list page / omit link).
            sb.Append("      </div>\n");
            sb.Append("    </div>\n");
        }

        sb.Append("  </li>\n");
    }

    /// <summary>Reverse-index panel: deferred items that name this story / quick-dev via existing
    /// provenance. Empty input → empty string (NFR8). Reuses the scan-first row grammar.
    /// When a slot has no detail href, falls back to <paramref name="deferredListHref"/> when provided;
    /// otherwise omits the primary link (never an empty disclosure). [artifact-review-nav]</summary>
    public static string RenderDeferredFromArtifactPanel(
        IReadOnlyList<FollowUpDeferredSlot> slots,
        string heading = "Deferred from this artifact",
        string? deferredListHref = null)
    {
        if (slots is null || slots.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel deferred-from-artifact\" id=\"sec-deferred-from-artifact\">\n");
        sb.Append($"<h3>{PathUtil.Html(heading)}</h3>\n");
        sb.Append("<ul class=\"followup-rows-list\">\n");

        var ordered = slots
            .Select((slot, index) => (slot, index))
            .OrderBy(t => t.slot.Item.Resolved ? 1 : 0)
            .ThenBy(t => t.index)
            .Select(t => t.slot);

        foreach (var slot in ordered)
        {
            var summaryPlain = SummarizeFromHtml(slot.Item.BodyHtml);
            var summaryHtml = PathUtil.Html(summaryPlain);
            var (statusToken, statusLabel) = slot.Item.Resolved
                ? ("done", "Resolved")
                : (StatusStyles.ForSprint("open"), "Open");
            var detailHref = !string.IsNullOrWhiteSpace(slot.DetailHref)
                ? slot.DetailHref
                : (!string.IsNullOrWhiteSpace(deferredListHref) ? deferredListHref : null);

            Render(
                sb,
                summaryHtml,
                statusToken,
                statusLabel,
                PathUtil.Html(slot.ProvenanceLabel),
                detailBodyHtml: string.Empty,
                resolved: slot.Item.Resolved,
                detailHref: detailHref);
        }

        sb.Append("</ul>\n</div>\n");
        return sb.ToString();
    }

    private static string TruncatePlainText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        var cut = text[..maxChars].TrimEnd();
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxChars / 2) cut = cut[..lastSpace];
        return cut + "…";
    }

    private static int FindFirstSentenceEnd(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('.' or '!' or '?')) continue;
            if (c == '.' && i > 0 && char.IsDigit(text[i - 1]))
            {
                if (i + 1 < text.Length && char.IsDigit(text[i + 1])) continue;
            }
            var next = i + 1 < text.Length ? text[i + 1] : ' ';
            if (next is ' ' or '\n' or '\r' || i == text.Length - 1)
                return i + 1;
        }
        return -1;
    }
}
