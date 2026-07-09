using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Parses the "## Requirements Inventory" section of a BMad epics.md into a structured
/// <see cref="RequirementsModel"/>.
///
/// Source shape (see _bmad-output/planning-artifacts/gdds/*/epics.md):
/// - "### Functional Requirements" — bold category headers (**Core Loop &amp; Time**) followed by
///   "FR{n}: {definition}" lines.
/// - "### NonFunctional Requirements" — "NFR{n}: {definition}" lines, no categories.
/// - "### FR Coverage Map" — one "FR{n}: Epic {N} - {note}", "FR{n}: Epics {N} &amp; {M} - {note}", or
///   "FR{n}: Deferred - {note}" line per requirement (NFRs listed the same way). ALL named epics are captured
///   (<see cref="RequirementInfo.CoverageEpicNumbers"/>); the first is the primary covering epic.
/// Each requirement's <see cref="RequirementStatus"/> is rolled up from its primary covering epic's progress.</summary>
public static class RequirementsParser
{
    private static readonly Regex DefLine = new(@"^(FR|NFR)(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex CategoryLine = new(@"^\*\*(.+?)\*\*$", RegexOptions.Compiled);
    // Matches the leading "Epic 4" / "Epics 1 & 2" / "Epics 1, 2 and 3" coverage clause and its numbers.
    // Plural "Epics" and multi-number lists are real in epics.md (FR2/FR5/FR7: "Epics 1 & 2"), which the old
    // singular "Epic\s+(\d+)" first-match pattern silently dropped entirely (no whitespace after "Epic"s).
    private static readonly Regex EpicClause = new(@"^Epics?\s+([\d,&\s]*\d)", RegexOptions.Compiled);
    private static readonly Regex NumberRef = new(@"\d+", RegexOptions.Compiled);

    private sealed record Coverage(IReadOnlyList<int> EpicNumbers, bool Deferred, string? Note);

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

            // The coverage clause is everything before the " - " note separator ("Epics 1 & 2"); the note is
            // whatever follows (or the whole remainder when there's no separator).
            var dash = rest.IndexOf(" - ", StringComparison.Ordinal);
            var clause = (dash >= 0 ? rest[..dash] : rest).Trim();
            var note = dash >= 0 ? rest[(dash + 3)..].Trim() : rest;

            // Capture EVERY covering epic, not just the first — "Epics 1 & 2" genuinely covers both, and the
            // requirements flow (Story 3.7) needs the full set. Ordered by appearance, de-duplicated; empty for
            // a deferred line or a clause that doesn't name epics (unmapped). The singular primary is this
            // list's first element (resolved in ParseDefs). [Story 3.7 Task 1.1]
            var clauseMatch = deferred ? Match.Empty : EpicClause.Match(clause);
            var epicNumbers = clauseMatch.Success
                ? NumberRef.Matches(clauseMatch.Groups[1].Value)
                    .Select(match => int.Parse(match.Value))
                    .Distinct()
                    .ToArray()
                : Array.Empty<int>();

            map[id] = new Coverage(epicNumbers, deferred, note.Length > 0 ? note : null);
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

            var epicNumbers = cov?.EpicNumbers ?? Array.Empty<int>();
            // Primary = the first covering epic; keeps every existing consumer byte-for-byte unchanged.
            int? epicNumber = epicNumbers.Count > 0 ? epicNumbers[0] : null;
            var deferred = cov?.Deferred ?? false;
            string? epicTitle = epicNumber is { } en && epicsByNumber.TryGetValue(en, out var e) ? e.Title : null;

            result.Add(new RequirementInfo
            {
                Kind = kind,
                Number = number,
                TextHtml = MarkdownConverter.RenderInline(def.Groups[3].Value),
                Category = withCategories ? category : null,
                CoverageEpicNumber = epicNumber,
                CoverageEpicNumbers = epicNumbers,
                CoverageEpicTitleHtml = epicTitle,
                CoverageNote = cov?.Note,
                Deferred = deferred,
                Status = DeriveStatus(deferred, epicNumber, epicsByNumber, progressByEpic),
            });
        }

        return result;
    }

    /// <summary>Resolves a requirement's covering epics (<see cref="RequirementInfo.CoverageEpicNumbers"/>)
    /// to their stories, in source order — the story-backed half of the FR→story mapping AC #2 asks for.
    /// A deferred/unmapped requirement (no covering epics) yields no stories. Deterministic: epics in the
    /// requirement's coverage order, stories in each epic's declared order. Missing epic numbers are skipped
    /// (routing is best-effort, never throws). [Story 3.7 Task 1.3]</summary>
    public static IReadOnlyList<StoryInfo> StoriesFor(RequirementInfo req, EpicsModel epics)
    {
        var byNumber = epics.Epics.ToDictionary(e => e.Number);
        return req.CoverageEpicNumbers
            .Where(byNumber.ContainsKey)
            .SelectMany(n => byNumber[n].Stories)
            .ToList();
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
