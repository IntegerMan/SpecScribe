using System.Text.RegularExpressions;

namespace DocsForge;

/// <summary>Parses the "## Requirements Inventory" section of a BMad epics.md into a structured
/// <see cref="RequirementsModel"/>.
///
/// Source shape (see _bmad-output/planning-artifacts/gdds/*/epics.md):
/// - "### Functional Requirements" — bold category headers (**Core Loop &amp; Time**) followed by
///   "FR{n}: {definition}" lines.
/// - "### NonFunctional Requirements" — "NFR{n}: {definition}" lines, no categories.
/// - "### FR Coverage Map" — one "FR{n}: Epic {N} - {note}" or "FR{n}: Deferred - {note}" line per
///   requirement (NFRs listed the same way). The first "Epic (\d+)" match is the primary covering epic.
/// Each requirement's <see cref="RequirementStatus"/> is rolled up from its covering epic's progress.</summary>
public static class RequirementsParser
{
    private static readonly Regex DefLine = new(@"^(FR|NFR)(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex CategoryLine = new(@"^\*\*(.+?)\*\*$", RegexOptions.Compiled);
    private static readonly Regex EpicRef = new(@"Epic\s+(\d+)", RegexOptions.Compiled);

    private sealed record Coverage(int? EpicNumber, bool Deferred, string? Note);

    public static RequirementsModel Parse(string rawEpicsMd, EpicsModel epics, ProgressModel progress)
    {
        var body = MarkdownConverter.StripFrontmatter(rawEpicsMd);
        var lines = body.Replace("\r\n", "\n").Split('\n');

        var inventory = SliceSection(lines, "## Requirements Inventory", "## ");
        var frLines = SliceSection(inventory, "### Functional Requirements", "### ");
        var nfrLines = SliceSection(inventory, "### NonFunctional Requirements", "### ");
        var mapLines = SliceSection(inventory, "### FR Coverage Map", "### ");

        var coverage = ParseCoverage(mapLines);
        var epicsByNumber = epics.Epics.ToDictionary(e => e.Number);
        var progressByEpic = progress.PerEpic.ToDictionary(p => p.Number);

        var functional = ParseDefs(frLines, RequirementKind.Functional, withCategories: true, coverage, epicsByNumber, progressByEpic);
        var nonFunctional = ParseDefs(nfrLines, RequirementKind.NonFunctional, withCategories: false, coverage, epicsByNumber, progressByEpic);

        return new RequirementsModel { Functional = functional, NonFunctional = nonFunctional };
    }

    /// <summary>Returns the lines under an exact heading, up to the next heading at the given level (or end).</summary>
    private static string[] SliceSection(string[] lines, string exactHeading, string stopPrefix)
    {
        var start = Array.FindIndex(lines, l => l.TrimEnd() == exactHeading);
        if (start < 0) return Array.Empty<string>();

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(stopPrefix, StringComparison.Ordinal)) { end = i; break; }
        }

        return lines[(start + 1)..end];
    }

    private static Dictionary<string, Coverage> ParseCoverage(string[] lines)
    {
        var map = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var m = DefLine.Match(line);
            if (!m.Success) continue;

            var id = m.Groups[1].Value + m.Groups[2].Value;
            var rest = m.Groups[3].Value.Trim();
            var deferred = rest.StartsWith("Deferred", StringComparison.OrdinalIgnoreCase);
            var epicMatch = EpicRef.Match(rest);
            int? epicNumber = !deferred && epicMatch.Success ? int.Parse(epicMatch.Groups[1].Value) : null;

            // Note is whatever follows the first " - " separator, else the whole remainder.
            var dash = rest.IndexOf(" - ", StringComparison.Ordinal);
            var note = dash >= 0 ? rest[(dash + 3)..].Trim() : rest;

            map[id] = new Coverage(epicNumber, deferred, note.Length > 0 ? note : null);
        }
        return map;
    }

    private static List<RequirementInfo> ParseDefs(
        string[] lines,
        RequirementKind kind,
        bool withCategories,
        IReadOnlyDictionary<string, Coverage> coverage,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        IReadOnlyDictionary<int, EpicProgress> progressByEpic)
    {
        var result = new List<RequirementInfo>();
        string? category = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var def = DefLine.Match(line);
            if (!def.Success)
            {
                // A bold-only line inside the Functional section is a category header, not a requirement.
                if (withCategories && CategoryLine.IsMatch(line))
                {
                    category = CategoryLine.Match(line).Groups[1].Value.Trim();
                }
                continue;
            }

            var lineKind = def.Groups[1].Value == "FR" ? RequirementKind.Functional : RequirementKind.NonFunctional;
            if (lineKind != kind) continue; // guards against a stray cross-kind line in the wrong section

            var number = int.Parse(def.Groups[2].Value);
            var id = def.Groups[1].Value + number;
            coverage.TryGetValue(id, out var cov);

            int? epicNumber = cov?.EpicNumber;
            var deferred = cov?.Deferred ?? false;
            string? epicTitle = epicNumber is { } en && epicsByNumber.TryGetValue(en, out var e) ? e.Title : null;

            result.Add(new RequirementInfo
            {
                Kind = kind,
                Number = number,
                TextHtml = MarkdownConverter.RenderInline(def.Groups[3].Value),
                Category = withCategories ? category : null,
                CoverageEpicNumber = epicNumber,
                CoverageEpicTitleHtml = epicTitle,
                CoverageNote = cov?.Note,
                Deferred = deferred,
                Status = DeriveStatus(deferred, epicNumber, epicsByNumber, progressByEpic),
            });
        }

        return result;
    }

    /// <summary>Rolls a requirement's status up from its covering epic. Because the FR→Epic map is
    /// epic-level, one in-flight story can't be pinned to a specific requirement — so we never claim
    /// "in development". We only assert Done when the <em>entire</em> covering epic is done
    /// (<see cref="StatusStyles.ForEpic"/> == "done"); otherwise Ready once the epic's stories have task
    /// plans, else Planned. This stops a lone scaffolding story from painting every requirement its epic
    /// covers as actively in development.</summary>
    private static RequirementStatus DeriveStatus(
        bool deferred,
        int? epicNumber,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        IReadOnlyDictionary<int, EpicProgress> progressByEpic)
    {
        if (deferred) return RequirementStatus.Deferred;
        if (epicNumber is not { } n || !epicsByNumber.TryGetValue(n, out var epic)) return RequirementStatus.Planned;

        if (StatusStyles.ForEpic(epic) == "done") return RequirementStatus.Done;

        // Not fully done: "Ready for dev" once task plans exist for the covering epic, else still Planned.
        return progressByEpic.TryGetValue(n, out var ep) && ep.TasksTotal > 0
            ? RequirementStatus.Ready
            : RequirementStatus.Planned;
    }
}
