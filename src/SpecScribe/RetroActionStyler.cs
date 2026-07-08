using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Post-processes a retrospective's <c>## Action Items</c> table: drops the <c>Owner</c> column (retro
/// "owners" are LLM-generated personas, not real assignees — noise once the doc exists) and badges the Status
/// cells (open / in-progress / done → the shared on-brand status badge). A pure body-HTML post-processor
/// anchored on the <c>id="action-items"</c> heading + the table that follows it (like
/// <see cref="CapabilityStyler"/>), so it can never fire on another table; no action-items table, or an
/// unrecognized status word, leaves the HTML unchanged. [Story 2.3 retro pages]</summary>
public static class RetroActionStyler
{
    private static readonly Regex Section = new(
        "(?<head><h2[^>]*\\bid=\"action-items\"[^>]*>.*?</h2>)\\s*(?<table><table[^>]*>.*?</table>)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // A bare Status cell — the only <td>s whose entire content is a lifecycle word are the table's Status column.
    private static readonly Regex StatusCell = new(
        "<td>\\s*(?<s>open|in-progress|in progress|done)\\s*</td>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Row = new("<tr[^>]*>.*?</tr>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Cell = new("<t(?<k>[hd])\\b[^>]*>.*?</t\\k<k>>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);

    public static string Style(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        return Section.Replace(html, m =>
        {
            var table = RemoveColumn(m.Groups["table"].Value, "Owner");
            table = StatusCell.Replace(table, cell =>
            {
                var status = cell.Groups["s"].Value;
                return $"<td>{StatusStyles.Badge(StatusStyles.ForSprint(status), StatusStyles.SprintLabel(status))}</td>";
            });
            return m.Groups["head"].Value + "\n" + table;
        });
    }

    /// <summary>Removes the whole column whose header cell text equals <paramref name="header"/> (case-insensitive)
    /// from a rendered markdown table — the header <c>&lt;th&gt;</c> and the aligned <c>&lt;td&gt;</c> in every
    /// row. A no-op when no such column exists. Relies on Markdig's one-cell-per-line output (each cell followed
    /// by a newline), which is stripped alongside the cell so no blank line is left behind.</summary>
    private static string RemoveColumn(string table, string header)
    {
        var index = -1;
        var headerCellCount = 0;
        foreach (Match r in Row.Matches(table))
        {
            if (!r.Value.Contains("<th", StringComparison.Ordinal)) continue; // the header row
            var cells = Cell.Matches(r.Value);
            headerCellCount = cells.Count;
            for (var i = 0; i < cells.Count; i++)
            {
                if (Tags.Replace(cells[i].Value, string.Empty).Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            break;
        }
        if (index < 0) return table;

        return Row.Replace(table, r =>
        {
            var cells = Cell.Matches(r.Value);
            // A ragged row (cell count doesn't match the header) can't be trusted to align by position —
            // removing "index" could strip the wrong cell instead of Owner. Leave it fully unchanged rather
            // than risk misaligning the table. [Story 2.3 review]
            if (cells.Count != headerCellCount || index >= cells.Count) return r.Value;
            var cell = cells[index];
            var end = cell.Index + cell.Length;
            var removeLen = cell.Length + (end < r.Value.Length && r.Value[end] == '\n' ? 1 : 0); // eat trailing newline
            return r.Value.Remove(cell.Index, removeLen);
        });
    }
}
