using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Badges the Status cells of a retrospective's <c>## Action Items</c> table (open / in-progress /
/// done → the shared on-brand status badge). A pure body-HTML post-processor anchored on the
/// <c>id="action-items"</c> heading + the table that follows it (like <see cref="CapabilityStyler"/>), so it
/// can never fire on another table; no action-items table, or an unrecognized status word, leaves the HTML
/// unchanged. [Story 2.3 retro pages]</summary>
public static class RetroActionStyler
{
    private static readonly Regex Section = new(
        "(?<head><h2[^>]*\\bid=\"action-items\"[^>]*>.*?</h2>)\\s*(?<table><table[^>]*>.*?</table>)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // A bare Status cell — the only <td>s whose entire content is a lifecycle word are the table's Status column.
    private static readonly Regex StatusCell = new(
        "<td>\\s*(?<s>open|in-progress|in progress|done)\\s*</td>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Style(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        return Section.Replace(html, m =>
        {
            var table = StatusCell.Replace(m.Groups["table"].Value, cell =>
            {
                var status = cell.Groups["s"].Value;
                return $"<td>{StatusStyles.Badge(StatusStyles.ForSprint(status), StatusStyles.SprintLabel(status))}</td>";
            });
            return m.Groups["head"].Value + "\n" + table;
        });
    }
}
