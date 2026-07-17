using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Parses the "## Requirements Inventory" section of a BMad epics.md into a structured
/// <see cref="RequirementsModel"/>.
///
/// Source shape (see _bmad-output/planning-artifacts/gdds/*/epics.md):
/// - "### Functional Requirements" — bold category headers (**Core Loop &amp; Time**) followed by
///   "FR{n}: {definition}" lines.
/// - "### NonFunctional Requirements" — "NFR{n}: {definition}" lines, no categories.
/// - "### UX Design Requirements" — "UX-DR{n}: {definition}" lines, no categories. [Story 9.2]
/// - "### FR Coverage Map" — one "FR{n}: Epic {N} - {note}", "FR{n}: Epics {N} &amp; {M} - {note}", or
///   "FR{n}: Deferred - {note}" line per requirement (NFRs listed the same way). ALL named epics are captured
///   (<see cref="RequirementInfo.CoverageEpicNumbers"/>); the first is the primary covering epic.
/// - "## Epic List" header lines — a second coverage source for NFR/UX-DR via reverse index
///   (<c>**NFRs:**</c> / <c>**UX-DRs:**</c> / <c>**NFRs covered:**</c> tokens). FR coverage stays map-only.
/// Each requirement's <see cref="RequirementStatus"/> is rolled up from its covering epic(s)' progress.</summary>
public static class RequirementsParser
{
    private static readonly Regex DefLine = new(@"^(FR|NFR)(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex UxDrLine = new(@"^UX-DR(\d+):\s*(.+)$", RegexOptions.Compiled);
    // FR Coverage Map lines may name FR, NFR, or UX-DR (Task 2 header∪map union for Design). [Story 9.2 review]
    private static readonly Regex CoverageMapLine = new(@"^(FR|NFR|UX-DR)(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex CategoryLine = new(@"^\*\*(.+?)\*\*$", RegexOptions.Compiled);
    // Matches the leading "Epic 4" / "Epics 1 & 2" / "Epics 1, 2 and 3" coverage clause. Only confirms the
    // clause names epic(s) at all (plural "Epics" and multi-number lists are real in epics.md — FR2/FR5/FR7:
    // "Epics 1 & 2" — which the old singular "Epic\s+(\d+)" first-match pattern silently dropped entirely, no
    // whitespace after "Epic"s). The actual numbers are pulled from the WHOLE clause via NumberRef below, not
    // from this pattern's capture group, so any separator between numbers ("&", ",", "and", ", and") works —
    // a narrower digits/comma/ampersand-only capture group would silently drop numbers after the word "and".
    private static readonly Regex EpicClause = new(@"^Epics?\b", RegexOptions.Compiled);
    private static readonly Regex NumberRef = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex ListEpicHeading = new(@"^### Epic (\d+):", RegexOptions.Compiled);
    // FR\d+ / NFR\d+ / UX-DR\d+ tokens on epic-header coverage lines — ordered by appearance for deterministic
    // CoverageEpicNumbers. [Story 9.2 Task 2]
    private static readonly Regex ReqIdToken = new(@"\b(?:FR|NFR|UX-DR)(\d+)\b", RegexOptions.Compiled);

    private sealed record Coverage(IReadOnlyList<int> EpicNumbers, bool Deferred, string? Note);

    public static RequirementsModel Parse(string rawEpicsMd, EpicsModel epics, ProgressModel progress)
    {
        var body = MarkdownConverter.StripFrontmatter(rawEpicsMd);
        var lines = body.Replace("\r\n", "\n").Split('\n');

        var inventory = SliceSection(lines, "## Requirements Inventory", "## ");
        var frLines = SliceSection(inventory, "### Functional Requirements", "### ");
        var nfrLines = SliceSection(inventory, "### NonFunctional Requirements", "### ");
        var uxDrLines = SliceSection(inventory, "### UX Design Requirements", "### ");
        var mapLines = SliceSection(inventory, "### FR Coverage Map", "### ");

        var mapCoverage = ParseCoverage(mapLines);
        // Second source: epic-header reverse index. Scoped per kind in ResolveCoverage so FRs stay map-only.
        var headerCoverage = ParseEpicHeaderCoverage(lines);
        var epicsByNumber = epics.Epics.ToDictionary(e => e.Number);

        var functional = ParseDefs(frLines, RequirementKind.Functional, withCategories: true,
            mapCoverage, headerCoverage, epicsByNumber);
        var nonFunctional = ParseDefs(nfrLines, RequirementKind.NonFunctional, withCategories: false,
            mapCoverage, headerCoverage, epicsByNumber);
        var design = ParseUxDrs(uxDrLines, mapCoverage, headerCoverage, epicsByNumber);

        return new RequirementsModel
        {
            Functional = functional,
            NonFunctional = nonFunctional,
            Design = design,
        };
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
            var m = CoverageMapLine.Match(line);
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
            // Numbers come from the WHOLE clause (not just EpicClause's match), so "Epics 1, 2 and 3" captures
            // all three regardless of separator — EpicClause only gates whether the line names epics at all.
            var epicNumbers = !deferred && EpicClause.IsMatch(clause)
                ? NumberRef.Matches(clause)
                    .Select(match => int.Parse(match.Value))
                    .Distinct()
                    .ToArray()
                : Array.Empty<int>();

            map[id] = new Coverage(epicNumbers, deferred, note.Length > 0 ? note : null);
        }
        return map;
    }

    /// <summary>Builds requirement-id → covering epic numbers from "## Epic List" header lines. Captures every
    /// FR/NFR/UX-DR token on each epic's meta line(s); callers scope which kinds union with the FR Coverage Map.
    /// Deterministic: epics in source order, tokens in appearance order, de-duplicated. [Story 9.2 Task 2]</summary>
    private static Dictionary<string, List<int>> ParseEpicHeaderCoverage(string[] lines)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var headingIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Epic List");
        if (headingIdx < 0) return map;

        var end = lines.Length;
        for (var i = headingIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }
        }

        var entryStarts = new List<(int Index, int Number)>();
        for (var i = headingIdx + 1; i < end; i++)
        {
            var m = ListEpicHeading.Match(lines[i].Trim());
            if (m.Success)
            {
                entryStarts.Add((i, int.Parse(m.Groups[1].Value)));
            }
        }

        for (var e = 0; e < entryStarts.Count; e++)
        {
            var (idx, epicNumber) = entryStarts[e];
            var bodyEnd = e + 1 < entryStarts.Count ? entryStarts[e + 1].Index : end;

            for (var i = idx + 1; i < bodyEnd; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                // Only scan coverage meta lines (bold-labelled). Goal prose can mention FR ids casually —
                // attributing those would over-claim. Labels vary: "FRs covered:", "NFRs:", "UX-DRs:",
                // "NFRs covered:" (Epics 16/17).
                if (!line.StartsWith("**", StringComparison.Ordinal)) continue;
                if (!line.Contains("FR", StringComparison.Ordinal) &&
                    !line.Contains("NFR", StringComparison.Ordinal) &&
                    !line.Contains("UX-DR", StringComparison.Ordinal)) continue;

                foreach (Match token in ReqIdToken.Matches(line))
                {
                    // Reconstruct the full id from the match (group 0 is FR25 / NFR8 / UX-DR21).
                    var id = token.Value;
                    if (!map.TryGetValue(id, out var list))
                    {
                        list = new List<int>();
                        map[id] = list;
                    }
                    if (!list.Contains(epicNumber))
                    {
                        list.Add(epicNumber);
                    }
                }
            }
        }

        return map;
    }

    /// <summary>Resolves covering epics for one requirement, scoped by kind so FR output stays byte-identical:
    /// FR = map only; NFR = map ∪ header; UX-DR = header ∪ map (map has none today). [Story 9.2 Task 2]</summary>
    private static Coverage ResolveCoverage(
        string id,
        RequirementKind kind,
        IReadOnlyDictionary<string, Coverage> mapCoverage,
        IReadOnlyDictionary<string, List<int>> headerCoverage)
    {
        mapCoverage.TryGetValue(id, out var map);
        headerCoverage.TryGetValue(id, out var headerEpics);

        var deferred = map?.Deferred ?? false;
        var note = map?.Note;

        // Deferred is a deliberate shelve — never union header epics onto it (would make StoriesFor/detail
        // list delivery while the badge says Deferred). [Story 9.2 review]
        IReadOnlyList<int> epicNumbers;
        if (deferred)
        {
            epicNumbers = Array.Empty<int>();
        }
        else if (kind == RequirementKind.Functional)
        {
            // FR coverage source = FR Coverage Map ONLY — never union header FR tokens.
            epicNumbers = map?.EpicNumbers ?? Array.Empty<int>();
        }
        else
        {
            // NFR/UX-DR: union map + header, map first (appearance order), then header, de-duplicated.
            var merged = new List<int>();
            if (map?.EpicNumbers is { Count: > 0 } fromMap)
            {
                foreach (var n in fromMap)
                {
                    if (!merged.Contains(n)) merged.Add(n);
                }
            }
            if (headerEpics is { Count: > 0 })
            {
                foreach (var n in headerEpics)
                {
                    if (!merged.Contains(n)) merged.Add(n);
                }
            }
            epicNumbers = merged;
        }

        return new Coverage(epicNumbers, deferred, note);
    }

    private static List<RequirementInfo> ParseDefs(
        string[] lines,
        RequirementKind kind,
        bool withCategories,
        IReadOnlyDictionary<string, Coverage> mapCoverage,
        IReadOnlyDictionary<string, List<int>> headerCoverage,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber)
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
            var cov = ResolveCoverage(id, kind, mapCoverage, headerCoverage);

            var epicNumbers = cov.EpicNumbers;
            // Primary = the first covering epic; keeps every existing consumer byte-for-byte unchanged.
            int? epicNumber = epicNumbers.Count > 0 ? epicNumbers[0] : null;
            var deferred = cov.Deferred;
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
                CoverageNote = cov.Note,
                Deferred = deferred,
                Status = DeriveStatus(deferred, epicNumbers, epicsByNumber),
            });
        }

        return result;
    }

    /// <summary>Parses "UX-DR{n}: …" definition lines (no categories). [Story 9.2 Task 1]</summary>
    private static List<RequirementInfo> ParseUxDrs(
        string[] lines,
        IReadOnlyDictionary<string, Coverage> mapCoverage,
        IReadOnlyDictionary<string, List<int>> headerCoverage,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber)
    {
        var result = new List<RequirementInfo>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var def = UxDrLine.Match(line);
            if (!def.Success) continue;

            var number = int.Parse(def.Groups[1].Value);
            var id = "UX-DR" + number;
            var cov = ResolveCoverage(id, RequirementKind.Design, mapCoverage, headerCoverage);

            var epicNumbers = cov.EpicNumbers;
            int? epicNumber = epicNumbers.Count > 0 ? epicNumbers[0] : null;
            string? epicTitle = epicNumber is { } en && epicsByNumber.TryGetValue(en, out var e) ? e.Title : null;

            result.Add(new RequirementInfo
            {
                Kind = RequirementKind.Design,
                Number = number,
                TextHtml = MarkdownConverter.RenderInline(def.Groups[2].Value),
                Category = null,
                CoverageEpicNumber = epicNumber,
                CoverageEpicNumbers = epicNumbers,
                CoverageEpicTitleHtml = epicTitle,
                CoverageNote = cov.Note,
                Deferred = cov.Deferred,
                Status = DeriveStatus(cov.Deferred, epicNumbers, epicsByNumber),
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

    /// <summary>Rolls a requirement's status up from ALL of its covering epics via
    /// <see cref="StatusStyles.ForEpic"/> (the single epic→status classifier, which already means "a story has
    /// entered dev/review/done" for its "active" tier). Least→most complete: <c>Deferred</c>; <c>Unmapped</c>
    /// (no covering epic named at all — a genuine coverage gap, distinct from Planned); <c>Planned</c>
    /// (covering epic(s) exist but none have started); <c>Ready</c> (a covering epic has task-planned stories
    /// but none started); <c>Active</c> = partially implemented (a covering epic has a story in flight, but
    /// not every covering epic is done); <c>Done</c> (every covering epic is fully done).
    /// <para>Because the map is epic-level, "Active" is an epic-level approximation of the covering epics'
    /// aggregate progress — it does NOT use <see cref="StoriesFor"/> to check whether the specific stories
    /// this requirement maps to are the ones in flight; any story anywhere in a covering epic being active is
    /// enough. This supersedes the earlier refusal to surface any mid-development state, but it is the same
    /// coarse epic-level signal as every other tier here, not a finer per-requirement claim.</para>
    /// <para>The Unmapped/Planned split (Story 9.3) kills the false-oversight-vs-intentional-scope confusion
    /// that previously let a requirement with no covering epic silently read as "Planned."</para></summary>
    private static RequirementStatus DeriveStatus(
        bool deferred,
        IReadOnlyList<int> epicNumbers,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber)
    {
        if (deferred) return RequirementStatus.Deferred;

        // No covering epic named at all — a genuinely UNMAPPED requirement (an unmapped FR, or an uncovered
        // NFR/UX-DR): no plan exists yet. Distinct from "Planned" (a real covering epic that simply hasn't
        // started); collapsing the two into one "Planned" bucket is exactly the false-oversight-vs-intentional-
        // scope confusion Story 9.3 removes. [Story 9.3 Task 1]
        if (epicNumbers.Count == 0) return RequirementStatus.Unmapped;

        var classes = epicNumbers
            .Where(epicsByNumber.ContainsKey)
            .Select(n => StatusStyles.ForEpic(epicsByNumber[n]))
            .ToList();
        // The coverage line named epic(s) but none resolve to a known epic (e.g. a typo'd or since-removed epic
        // number). Author intent to map exists, so this reads "covered but not started" (Planned), not Unmapped.
        // Kept deliberately, NOT dead code for this input shape: without it, `classes.All(… == "done")` below is
        // vacuously true on an empty list and would over-claim Done — the "parser silently over-claims a real
        // input shape" class of bug the Epic 3 retro flagged. [Story 9.3]
        if (classes.Count == 0) return RequirementStatus.Planned;

        if (classes.All(c => c == "done")) return RequirementStatus.Done;
        // A covering epic that is itself fully done, or has any story in dev/review/done, rolls up to
        // "active" — surfaced as partially implemented since the whole set of covering epics isn't done yet.
        // "done" must count here too: a requirement covered by one finished epic and one merely-ready epic
        // has real progress and must not be reported as just "Ready for dev".
        if (classes.Any(c => c is "active" or "done")) return RequirementStatus.Active;
        if (classes.Any(c => c == "ready")) return RequirementStatus.Ready;
        return RequirementStatus.Planned;
    }
}
