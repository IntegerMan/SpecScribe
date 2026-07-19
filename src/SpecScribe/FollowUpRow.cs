using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Shared scan-first row grammar for action-items and deferred-work list pages.
/// When a Story 9.11 detail URL is present, the row is scan line + primary link only (no list
/// <c>&lt;details&gt;</c>). Without a detail URL, heavy content stays in a collapsed native
/// disclosure. [Story 9.10]</summary>
public static class FollowUpRow
{
    private static readonly Regex SummaryField = new(
        @"^\s*summary:\s*(?<text>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Whole-line evidence fields (values often contain spaces).</summary>
    private static readonly Regex EvidenceLine = new(
        @"^[ \t]*evidence\s*:.*?\r?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary><c>source_spec:</c> path (optionally backtick-wrapped); trailing same-line prose is kept.</summary>
    private static readonly Regex SourceSpecLine = new(
        @"^[ \t]*source_spec\s*:\s*(?:`[^`\r\n]+`|\S+)(?<trail>[ \t]+\S.*?)?\r?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly HashSet<string> TitleAbbrevs = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "mr", "mrs", "ms", "vs", "etc", "approx", "cf", "al",
    };

    /// <summary>Derives a short plain-text lead from existing authored text — prefers an authored
    /// <c>summary:</c> field when present, otherwise the first non-metadata sentence. No new authoring
    /// schema. [Story 9.10]</summary>
    public static string SummarizePlainText(string plainText, int maxChars = 120)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return string.Empty;

        var summary = SummaryField.Match(plainText);
        if (summary.Success)
            return TruncatePlainText(summary.Groups["text"].Value.Trim(), maxChars);

        var withoutMeta = EvidenceLine.Replace(plainText, "");
        withoutMeta = SourceSpecLine.Replace(withoutMeta, m =>
            m.Groups["trail"].Success ? m.Groups["trail"].Value.TrimStart() : "");
        var trimmed = withoutMeta.Trim();
        // Metadata-only bullets must not fall back to restoring source_spec/evidence as the lead.
        if (trimmed.Length == 0) return string.Empty;

        var end = FindFirstSentenceEnd(trimmed);
        var lead = end > 0 ? trimmed[..end].Trim() : trimmed;
        return TruncatePlainText(lead, maxChars);
    }

    /// <summary>Strips HTML then summarizes. Used for deferred-work item bodies. [Story 9.10]</summary>
    public static string SummarizeFromHtml(string html, int maxChars = 120) =>
        SummarizePlainText(PathUtil.StripHtmlTags(html), maxChars);

    /// <summary>Renders one scan-first follow-up row: summary + status + source chip + primary affordance.
    /// With <paramref name="detailHref"/>, omits list disclosure (detail page owns the body).
    /// Without it, heavy detail collapses into native <c>&lt;details&gt;</c>. [Story 9.10]</summary>
    public static void Render(
        StringBuilder sb,
        string summaryHtml,
        string statusToken,
        string statusLabel,
        string sourceChipHtml,
        string detailBodyHtml,
        bool resolved = false,
        string? detailHref = null,
        string? sortName = null,
        string? sortDate = null,
        string? sortStatus = null)
    {
        var rowClass = resolved ? "followup-row resolved" : "followup-row";
        sb.Append($"  <li class=\"{rowClass}\"");
        if (sortName is { Length: > 0 }) sb.Append($" data-sort-name=\"{PathUtil.Html(sortName)}\"");
        if (sortDate is { Length: > 0 }) sb.Append($" data-sort-date=\"{PathUtil.Html(sortDate)}\"");
        if (sortStatus is { Length: > 0 }) sb.Append($" data-sort-status=\"{PathUtil.Html(sortStatus)}\"");
        sb.Append(">\n");
        sb.Append("    <div class=\"followup-row-scan\">\n");
        sb.Append($"      <span class=\"followup-row-summary\">{summaryHtml}</span>\n");
        // Meta cluster stays visually adjacent (status + source + primary) so wide viewports
        // don't leave a scan gap between the title and its actions.
        sb.Append("      <div class=\"followup-row-meta\">\n");
        sb.Append($"        {StatusStyles.Badge(statusToken, statusLabel)}\n");
        sb.Append($"        <span class=\"followup-row-source pill\">{sourceChipHtml}</span>\n");

        if (detailHref is { Length: > 0 })
        {
            // Owner (code review 9.10): scan + View detail only — no list-page More disclosure.
            sb.Append($"        <a class=\"followup-row-primary\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(detailHref))}\">View detail &rarr;</a>\n");
            sb.Append("      </div>\n");
            sb.Append("    </div>\n");
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
            if (c == '.' && IsAbbreviationPeriod(text, i)) continue;

            var next = i + 1 < text.Length ? text[i + 1] : ' ';
            if (next is ' ' or '\n' or '\r' || i == text.Length - 1)
                return i + 1;
        }
        return -1;
    }

    /// <summary>True when <paramref name="periodIndex"/> is an abbreviation/initial period, not a sentence end.</summary>
    private static bool IsAbbreviationPeriod(string text, int periodIndex)
    {
        // e.g. / i.e. / U.S. — letter '.' letter '.' (this index is the second or first of a pair)
        if (periodIndex >= 2
            && char.IsLetter(text[periodIndex - 1])
            && text[periodIndex - 2] == '.'
            && periodIndex >= 3
            && char.IsLetter(text[periodIndex - 3]))
            return true;

        // First period of e.g. / i.e. — next char is a letter (not whitespace), so the main loop
        // already skips via the whitespace check; still guard title abbrevs below.

        var start = periodIndex;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        if (start == periodIndex) return false;
        var word = text[start..periodIndex];
        return TitleAbbrevs.Contains(word);
    }
}
