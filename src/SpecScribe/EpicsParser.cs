using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Parses a BMad epics.md into a structured <see cref="EpicsModel"/>.
///
/// Source shape (see _bmad-output/planning-artifacts/gdds/*/epics.md):
/// - "## Epic List" holds one "### Epic N: Title" entry per epic (goal + **FRs/NFRs covered:** lines),
///   for ALL epics, split into Vertical Slice / Further Development by a "*Vertical slice complete...*" divider.
/// - Later, "## Epic N: Title" (H2, not H3) full sections exist only for epics with drafted stories,
///   each containing "### Story N.M: Title" subsections (user story + Given/When/Then/And acceptance criteria).
/// An epic with a full H2 section is "Drafted"; an Epic-List-only epic is "Pending".</summary>
public static class EpicsParser
{
    private static readonly Regex ListEpicHeading = new(@"^### Epic (\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex SectionEpicHeading = new(@"^## Epic (\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex StoryHeading = new(@"^### Story (\d+)\.(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex AcHeading = new(@"^\*\*Acceptance Criteria:?\*\*\s*$", RegexOptions.Compiled);
    private static readonly Regex AcKeywordLine = new(@"^\*\*(Given|When|Then|And|But)\*\*\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex AcBareNumberLine = new(@"^(\d+)\.$", RegexOptions.Compiled);
    private static readonly Regex MetaLine = new(@"^\*\*(FRs|NFRs) covered:\*\*\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex StatusLine = new(@"^Status:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Matches one or more HTML comments (each lazily closed at its first <c>--&gt;</c>) at the very
    /// start of a story's user-story region, plus any trailing blank space, so they can be peeled off and
    /// rendered as their own block. Singleline so a comment spans lines; anchored at start so only *leading*
    /// comments are lifted — narrative after the close marker, and an unterminated <c>&lt;!--</c>, are left
    /// alone.</summary>
    private static readonly Regex LeadingHtmlComments = new(@"\A\s*(?:<!--.*?-->\s*)+", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Tolerant retirement/superseded detector (Story 10.5 AC3) over a peeled leading-comment's raw
    /// text — word-boundaried, case-insensitive, no new authoring schema (recognizes the free-text seat-mapping
    /// comments authors already write, e.g. <c>&lt;!-- Story 3.4 retired 2026-07-08 ... --&gt;</c>). A matched
    /// comment is classified as a retired notice and diverted away from <see cref="StoryInfo.UserStoryNoteHtml"/>
    /// to the epic's <see cref="EpicInfo.RetiredNoticesHtml"/> collection instead of being attached to the next
    /// story card.</summary>
    private static readonly Regex RetirementKeyword = new(@"\b(retired|superseded|deprecated)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static EpicsModel Parse(string raw)
    {
        var body = MarkdownConverter.StripFrontmatter(raw);
        var lines = body.Replace("\r\n", "\n").Split('\n');

        var overviewHtml = ExtractSectionHtml(lines, "## Overview");
        var requirementsHtml = ExtractSectionHtml(lines, "## Requirements Inventory");

        var listEntries = ParseEpicList(lines);
        var sections = ParseEpicSections(lines).ToDictionary(s => s.Number);

        var epics = new List<EpicInfo>();
        foreach (var entry in listEntries.OrderBy(e => e.Number))
        {
            sections.TryGetValue(entry.Number, out var section);

            var title = (section?.Title.Length > 0 ? section.Title : null) ?? entry.Title;
            var goal = (section?.Goal.Length > 0 ? section.Goal : null) ?? entry.Goal;
            var metaRaw = section?.MetaRaw ?? entry.MetaRaw;
            var stories = section?.Stories ?? new List<StoryInfo>();

            epics.Add(new EpicInfo
            {
                Number = entry.Number,
                Title = MarkdownConverter.RenderInline(title),
                GoalHtml = MarkdownConverter.RenderInline(goal),
                FrMetaHtml = metaRaw is null ? null : RenderMeta(metaRaw),
                Status = stories.Count > 0 ? EpicStatus.Drafted : EpicStatus.Pending,
                Section = entry.IsFurtherDevelopment ? EpicSection.FurtherDevelopment : EpicSection.VerticalSlice,
                Stories = stories,
                RetiredNoticesHtml = section?.RetiredNoticesHtml ?? new List<string>(),
            });
        }

        return new EpicsModel
        {
            OverviewHtml = overviewHtml,
            RequirementsInventoryHtml = requirementsHtml,
            Epics = epics,
        };
    }

    /// <summary>Extracts the "Status: xyz" line implementation-artifact files carry near the top (not YAML
    /// frontmatter — just a plain line right after the H1).</summary>
    public static string? ExtractStatus(string rawArtifactMarkdown)
    {
        var m = StatusLine.Match(rawArtifactMarkdown);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    // Leading ISO date on a change-log row: list bullet ("- 2026-07-08 …") or table cell ("| 2026-07-08 | …").
    // Prefix required so prose lines that merely open with yyyy-MM-dd cannot poison the max. [Review][Patch]
    private static readonly Regex ChangeLogLeadingIsoDate =
        new(@"^\s*(?:[-*]\s+|\|\s*)(\d{4}-\d{2}-\d{2})\b", RegexOptions.Compiled);

    /// <summary>Returns the latest ISO <c>yyyy-MM-dd</c> date found in the artifact's <c>## Change Log</c>
    /// (or <c>### Change Log</c>) section (table or list form), or null when the section is absent / has no
    /// parseable date. Malformed rows are skipped — never throws. Pure + repo-free. [Story 8.8]</summary>
    public static DateOnly? ExtractLatestChangeLogDate(string? raw)
    {
        if (raw is null) return null;

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        // Prefer H2; fall back to H3 — several drafted artifacts in this repo use ### Change Log.
        var (start, end) = FindSection(lines, "## Change Log", 0, lines.Length);
        if (start < 0)
            (start, end) = FindSection(lines, "### Change Log", 0, lines.Length);
        if (start < 0) return null;

        DateOnly? max = null;
        for (var i = start + 1; i < end; i++)
        {
            var m = ChangeLogLeadingIsoDate.Match(lines[i]);
            if (!m.Success) continue;
            if (!DateOnly.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                continue;
            }
            if (max is null || date > max) max = date;
        }
        return max;
    }

    // "586 tests green" / "759 C# tests green" / "429 tests pass" / "440 tests passing" — free-text tallies
    // living in Dev Agent Record Completion Notes (and occasionally elsewhere). [Story 9.4]
    private static readonly Regex TestEvidencePhrase = new(
        @"\b(\d[\d,]*)\s+(?:C#\s+)?tests?\s+(green|pass|passing)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Newest-first Change Log row in list/table form with bold or plain action:
    // "- 2026-07-11 — **Code review passed…**", "- 2026-07-11: Code review passed…",
    // or "| 2026-07-11 | Code review passed… |". Distinct from Story 8.8's max-date scan — the evidence
    // strip wants the top dated-shape entry (abort if that date is unparseable) + whether its action
    // reads as verification. [Story 9.4]
    private static readonly Regex ChangeLogTopEntry = new(
        @"^\s*(?:[-*]\s+|\|\s*)(?<date>\d{4}-\d{2}-\d{2})\b(?:\s*\|\s*|\s+[—–-]\s+|\s*:\s*)(?:\*\*(?<bold>[^*]+)\*\*|(?<plain>.*?))(?:\s*\|)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ChangeLogVerificationAction = new(
        @"\b(verif(?:y|ied|ication|ications?)|reviewed|review\s+(?:passed|complete(?:d)?|done)|tests?\s+(?:green|pass(?:ed|ing)?)|Status\s*(?:→|->)\s*(?:done|review))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Best-effort free-text test tally from a story artifact. Prefers the first match inside
    /// <c>## Dev Agent Record</c>, then falls back to a whole-document scan. Returns a normalized
    /// <c>"{n} passing tests"</c> phrase (author "green"/"pass"/"passing" all collapse to that reading);
    /// null when absent. Deterministic first-match order; never throws. [Story 9.4]</summary>
    public static string? ExtractTestEvidence(string? raw)
    {
        if (raw is null) return null;

        var normalized = raw.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        return MatchTestEvidenceInSection(lines, "## Dev Agent Record")
               ?? MatchTestEvidenceInSection(lines, "### Dev Agent Record")
               ?? MatchTestEvidenceInSection(lines, "## Change Log")
               ?? MatchTestEvidenceInSection(lines, "### Change Log");
    }

    private static string? MatchTestEvidenceInSection(string[] lines, string exactHeading)
    {
        var (start, end) = FindSection(lines, exactHeading, 0, lines.Length);
        if (start < 0) return null;
        return MatchTestEvidence(string.Join("\n", lines[(start + 1)..end]));
    }

    private static string? MatchTestEvidence(string text)
    {
        var m = TestEvidencePhrase.Match(text);
        if (!m.Success) return null;
        return $"{m.Groups[1].Value} passing tests";
    }

    /// <summary>Top (newest-first) Change Log entry shaped <c>- YYYY-MM-dd — **action**</c>, with a flag for
    /// whether the action text reads as verification/review/done and the raw action for the visible
    /// "Latest change" cue. Returns null when the section or a matching dated entry is absent; the first
    /// dated-shape row with an unparseable calendar date also returns null (do not skip to a later row).
    /// Never throws. Distinct from <see cref="ExtractLatestChangeLogDate"/> (Story 8.8 max-date across
    /// table/list forms). [Story 9.4]</summary>
    public static (DateOnly Date, bool IsVerification, string Action)? ExtractChangeLogVerification(string? raw)
    {
        if (raw is null) return null;

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var (start, end) = FindSection(lines, "## Change Log", 0, lines.Length);
        if (start < 0)
            (start, end) = FindSection(lines, "### Change Log", 0, lines.Length);
        if (start < 0) return null;

        for (var i = start + 1; i < end; i++)
        {
            var m = ChangeLogTopEntry.Match(lines[i]);
            if (!m.Success) continue;
            if (!DateOnly.TryParseExact(m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                // First dated-shape match is the "top" entry — bad calendar → no verified/updated pill.
                return null;
            }
            var action = (m.Groups["bold"].Success ? m.Groups["bold"].Value : m.Groups["plain"].Value)
                .Trim()
                .Trim('|')
                .Trim();
            if (action.Length == 0) continue;
            var isVerification = ChangeLogVerificationAction.IsMatch(action);
            return (date, isVerification, action);
        }
        return null;
    }

    /// <summary>Splits a story implementation-artifact into its lead "## Story" blurb (the As-a/I-want/
    /// So-that narrative) and the rest of the plan, stopping before "## Dev Agent Record". Both the
    /// "## Acceptance Criteria" section (surfaced as its own anchored panel via
    /// <see cref="ExtractAcceptanceCriteria"/>) and "## Dev Agent Record" (a table via
    /// <see cref="ExtractDevAgentRecord"/>) are excised here so they aren't rendered twice. Lets the story
    /// page put the narrative and ACs ahead of the full plan without duplicating any section.</summary>
    public static (string BlurbHtml, string RemainderHtml) SplitStoryArtifact(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');

        var storyIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Story");
        var blurbHtml = ExtractSectionHtml(lines, "## Story");

        int remainderStart;
        if (storyIdx >= 0)
        {
            remainderStart = lines.Length;
            for (var i = storyIdx + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { remainderStart = i; break; }
            }
        }
        else
        {
            // Malformed/unexpected shape — fall back to the first H2 found, so at least the leading
            // H1/Status line never leaks into the rendered remainder.
            remainderStart = Array.FindIndex(lines, l => l.StartsWith("## ", StringComparison.Ordinal));
            if (remainderStart < 0) remainderStart = 0;
        }

        var remainderEnd = lines.Length;
        for (var i = remainderStart; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == "## Dev Agent Record") { remainderEnd = i; break; }
        }

        // Carve out the sections that render as their own panels elsewhere on the page, so they aren't
        // duplicated inside the remainder: Acceptance Criteria (leads the page), plus Review Findings and
        // Change Log (surfaced near the top / at the very bottom respectively). Change Log and Review
        // Findings normally sit after Dev Agent Record and are already past remainderEnd, but carving them
        // explicitly keeps the remainder clean regardless of section order.
        var carved = new[] { "## Acceptance Criteria", "## Review Findings", "## Change Log" }
            .Select(h => FindSection(lines, h, remainderStart, remainderEnd))
            .Where(s => s.Start >= 0)
            .OrderBy(s => s.Start)
            .ToList();

        var remainderLines = new List<string>();
        var cursor = remainderStart;
        foreach (var (secStart, secEnd) in carved)
        {
            if (secStart > cursor) remainderLines.AddRange(lines[cursor..secStart]);
            cursor = secEnd;
        }
        if (cursor < remainderEnd) remainderLines.AddRange(lines[cursor..remainderEnd]);

        var remainderMd = string.Join("\n", remainderLines);
        remainderMd = SourceCitationBrackets.Replace(remainderMd, "$1");
        return (blurbHtml, MarkdownConverter.RenderBlock(remainderMd));
    }

    /// <summary>Locates a "## Heading" section within [start, limit), returning its heading index and the
    /// index of the next H2 (or <paramref name="limit"/>). Returns (-1, -1) when the heading isn't found.</summary>
    private static (int Start, int End) FindSection(string[] lines, string exactHeading, int start, int limit)
    {
        var headingIdx = -1;
        for (var i = start; i < limit; i++)
        {
            if (lines[i].TrimEnd() == exactHeading) { headingIdx = i; break; }
        }
        if (headingIdx < 0) return (-1, -1);

        var end = limit;
        for (var i = headingIdx + 1; i < limit; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }
        }
        return (headingIdx, end);
    }

    // "[Source: _bmad-output/path.md — note]" is a plain bracketed citation, not markdown link syntax —
    // the brackets and "Source:" label are decorative noise once SourceLinkifier turns the path into a
    // real link, so they're stripped before rendering rather than left as literal punctuation around it.
    // The inner group is balance-aware over one level of "[...]" so a citation whose content is ITSELF a
    // markdown link (or several) — "[Source: [X.cs:1](../x.cs), [Y.cs:2-3](../y.cs)]" (Story 7.2) — has only
    // the OUTER wrapper stripped; a naive ".*?" would stop at the first inner label's "]" and corrupt the link
    // into visible "[X.cs:1(../x.cs)" markdown. "[^\[\]]" (a non-bracket run) and "\[[^\]]*\]" (a balanced pair)
    // are disjoint per position, so the match is linear — no catastrophic backtracking.
    private static readonly Regex SourceCitationBrackets = new(@"\[Source:\s*((?:[^\[\]]|\[[^\]]*\])*)\]", RegexOptions.Compiled);

    private static readonly Regex DevAgentSubHeading = new(@"^### (.+)$", RegexOptions.Compiled);

    /// <summary>Pulls the Dev Agent Record section's subsections (Agent Model Used, Debug Log
    /// References, Completion Notes List, File List) into label/content pairs for a compact table,
    /// instead of four mostly-empty headings buried at the bottom of the page. Accepts
    /// <c>## Dev Agent Record</c> or <c>### Dev Agent Record</c> (same heading order as
    /// <see cref="ExtractTestEvidence"/>).</summary>
    public static IReadOnlyList<(string Label, string ContentHtml)> ExtractDevAgentRecord(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var headingIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Dev Agent Record");
        if (headingIdx < 0)
            headingIdx = Array.FindIndex(lines, l => l.TrimEnd() == "### Dev Agent Record");
        if (headingIdx < 0) return Array.Empty<(string, string)>();

        var sectionEnd = lines.Length;
        for (var i = headingIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { sectionEnd = i; break; }
        }

        var subStarts = new List<(int Index, string Label)>();
        for (var i = headingIdx + 1; i < sectionEnd; i++)
        {
            var m = DevAgentSubHeading.Match(lines[i]);
            if (m.Success) subStarts.Add((i, m.Groups[1].Value.Trim()));
        }

        var result = new List<(string, string)>();
        for (var s = 0; s < subStarts.Count; s++)
        {
            var (idx, label) = subStarts[s];
            var end = s + 1 < subStarts.Count ? subStarts[s + 1].Index : sectionEnd;
            var contentMd = string.Join("\n", lines[(idx + 1)..end]).Trim();
            var contentHtml = contentMd.Length > 0
                ? MarkdownConverter.RenderBlock(contentMd)
                : "<span class=\"dev-agent-empty\">Not yet recorded</span>";
            result.Add((label, contentHtml));
        }

        return result;
    }

    private static readonly Regex AcNumberedItem = new(@"^(\d+)\.\s+(.*)$", RegexOptions.Compiled);

    /// <summary>Parses the "## Acceptance Criteria" numbered list into per-criterion (number, html, plain
    /// text) tuples, so the story page can render each one in its own anchored panel row and turn
    /// "(AC: #N)" task references into tooltip-bearing links back to it.</summary>
    public static IReadOnlyList<AcceptanceCriterion> ExtractAcceptanceCriteria(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var (start, end) = FindSection(lines, "## Acceptance Criteria", 0, lines.Length);
        if (start < 0) return Array.Empty<AcceptanceCriterion>();

        var itemStarts = new List<(int Index, int Number)>();
        for (var i = start + 1; i < end; i++)
        {
            var m = AcNumberedItem.Match(lines[i]);
            if (m.Success) itemStarts.Add((i, int.Parse(m.Groups[1].Value)));
        }

        var result = new List<AcceptanceCriterion>();
        for (var s = 0; s < itemStarts.Count; s++)
        {
            var (idx, number) = itemStarts[s];
            var itemEnd = s + 1 < itemStarts.Count ? itemStarts[s + 1].Index : end;

            // Strip the leading "N. " marker and keep any continuation/sub-bullet lines that follow.
            var bodyLines = new List<string> { AcNumberedItem.Match(lines[idx]).Groups[2].Value };
            for (var i = idx + 1; i < itemEnd; i++) bodyLines.Add(lines[i]);
            var bodyMd = SourceCitationBrackets.Replace(string.Join("\n", bodyLines).Trim(), "$1");

            // PlainText comes from the pre-styling render: StyleCriterion trims the whitespace between
            // clauses, which would glue words together ("…completion.When…") once tags are stripped.
            var html = MarkdownConverter.RenderInline(bodyMd);
            result.Add(new AcceptanceCriterion(number, GherkinStyler.StyleCriterion(html), PathUtil.StripHtmlTags(html)));
        }

        return result;
    }

    // "AC: #1" or "AC #1, #2" — the leading "AC" with an optional colon, then one or more "#N" numbers.
    private static readonly Regex AcReferenceGroup = new(@"\bAC:?\s*#\d+(?:\s*,\s*#\d+)*", RegexOptions.Compiled);
    private static readonly Regex AcReferenceNumber = new(@"#(\d+)", RegexOptions.Compiled);

    /// <summary>Rewrites every "(AC: #N)" reference in already-rendered story HTML into a link to the
    /// matching criterion's "#ac-N" anchor, with that criterion's full text as a hover tooltip. Numbers
    /// with no matching criterion are left as plain text.</summary>
    public static string LinkifyAcReferences(string html, IReadOnlyDictionary<int, string> criteriaByNumber)
    {
        if (html.Length == 0 || criteriaByNumber.Count == 0) return html;

        return AcReferenceGroup.Replace(html, group =>
            AcReferenceNumber.Replace(group.Value, num =>
            {
                var n = int.Parse(num.Groups[1].Value);
                return criteriaByNumber.TryGetValue(n, out var text)
                    ? $"<a class=\"ac-ref\" href=\"#ac-{n}\" title=\"{PathUtil.Html(text)}\">#{n}</a>"
                    : num.Value;
            }));
    }

    /// <summary>Renders a named "## Heading" section of a raw story artifact to block HTML (heading itself
    /// excluded, up to the next H2), with "[Source: ...]" citation brackets stripped as in the remainder.
    /// Returns "" when the section is absent. Used to surface Review Findings and Change Log as their own
    /// panels on the story page.</summary>
    public static string ExtractNamedSectionHtml(string raw, string exactHeading)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var (start, end) = FindSection(lines, exactHeading, 0, lines.Length);
        if (start < 0) return string.Empty;

        var slice = SourceCitationBrackets.Replace(string.Join("\n", lines[(start + 1)..end]).Trim(), "$1");
        if (slice.Length == 0) return string.Empty;
        // The Change Log gets a tolerant pass that reformats its dates through PortalDates and adds an ordinal cue
        // to same-day runs so events sharing a date read in order (Story 10.4 AC2). Any unrecognized shape (a
        // table, free prose) passes through untouched — never reordered, never dropped (NFR8).
        if (exactHeading == "## Change Log") slice = SequenceChangeLog(slice);
        return MarkdownConverter.RenderBlock(slice);
    }

    /// <summary>Extracts an H3 subsection anywhere in the artifact (e.g. <c>### Verify before marking review</c> under
    /// Dev Notes), ending at the next H3 or H2. Citation brackets stripped; returns "" when absent. [ADR 0007]</summary>
    public static string ExtractSubsectionHtml(string raw, string exactHeading)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var idx = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == exactHeading) { idx = i; break; }
        }
        if (idx < 0) return string.Empty;

        var end = lines.Length;
        for (var i = idx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("### ", StringComparison.Ordinal) ||
                lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        var slice = SourceCitationBrackets.Replace(string.Join("\n", lines[(idx + 1)..end]).Trim(), "$1");
        return slice.Length == 0 ? string.Empty : MarkdownConverter.RenderBlock(slice);
    }

    // A change-log list item that leads with an ISO date: "- 2026-07-06: <text>" (bullet may be - or *).
    private static readonly Regex ChangeLogDatedItem =
        new(@"^(?<bullet>\s*[-*]\s+)(?<date>\d{4}-\d{2}-\d{2})\s*:(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>Tolerant same-day sequencing for the Change Log (Story 10.4 AC2). Recognizes the shipped
    /// "- YYYY-MM-DD: text" list shape: reformats each visible date through <see cref="PortalDates.Day"/> and, for a
    /// run of consecutive items sharing one date, appends an ordinal "(k of N)" cue so a reader can order events that
    /// otherwise differ only in prose. Single-date items get no marker (unique days stay uncluttered). Continuation
    /// lines and any non-list shape (a table, free prose) are left exactly as authored — this annotates existing
    /// order, it never reorders or drops content (NFR8). Pure + repo-free so the rule is unit-testable. Degrades to
    /// the input unchanged when no dated list item is present.</summary>
    public static string SequenceChangeLog(string slice)
    {
        var lines = slice.Replace("\r\n", "\n").Split('\n');

        // Pass 1: find the item-start lines and their parsed dates, in order.
        var items = new List<(int LineIndex, DateOnly Date)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = ChangeLogDatedItem.Match(lines[i]);
            if (m.Success && DateOnly.TryParseExact(
                    m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                items.Add((i, date));
            }
        }
        if (items.Count == 0) return slice; // not a recognizable dated list — degrade to as-is

        // Group into runs of same-date items that are also ADJACENT in source order: two same-date items only join a
        // run when no OTHER list-item bullet sits between them (an intervening bullet with a different/unparseable date
        // is a distinct entry, so the two must not read as "1 of 2"/"2 of 2" across it). Continuation lines (indented,
        // non-bullet) don't break adjacency — they belong to the preceding item.
        var runs = new List<List<int>>();
        for (var i = 0; i < items.Count; i++)
        {
            var startsNewRun = i == 0
                || items[i].Date != items[i - 1].Date
                || HasInterveningBullet(lines, items[i - 1].LineIndex, items[i].LineIndex);
            if (startsNewRun) runs.Add(new List<int>());
            runs[^1].Add(i);
        }

        var marker = new string?[items.Count];
        foreach (var run in runs)
        {
            if (run.Count <= 1) continue; // unique day (or a broken run) stays uncluttered
            for (var k = 0; k < run.Count; k++)
            {
                marker[run[k]] = $" ({k + 1} of {run.Count})";
            }
        }

        // Pass 2: rewrite each item-start line — reformat the date, insert the marker before the colon.
        for (var idx = 0; idx < items.Count; idx++)
        {
            var (lineIndex, date) = items[idx];
            var m = ChangeLogDatedItem.Match(lines[lineIndex]);
            lines[lineIndex] = $"{m.Groups["bullet"].Value}{PortalDates.Day(date)}{marker[idx] ?? string.Empty}:{m.Groups["rest"].Value}";
        }

        return string.Join("\n", lines);
    }

    // Any markdown list-item bullet (dated or not), used to detect a distinct entry sitting between two dated items.
    // Captures its own leading indentation so a nested sub-bullet (more indented than the item bullets themselves)
    // can be told apart from a sibling top-level entry.
    private static readonly Regex ChangeLogBullet = new(@"^(?<indent>\s*)[-*]\s", RegexOptions.Compiled);

    /// <summary>True when a list-item bullet line sits strictly between <paramref name="fromLine"/> and
    /// <paramref name="toLine"/> AT THE SAME (OR SHALLOWER) INDENTATION as the item bullets themselves — i.e. a
    /// distinct change-log entry (with a different or unparseable date) separates two same-date items, so they must
    /// not be grouped into one "(k of N)" run. A more-indented bullet is a nested sub-point of the preceding entry,
    /// not a separate entry, and does not break adjacency.</summary>
    private static bool HasInterveningBullet(string[] lines, int fromLine, int toLine)
    {
        var itemMatch = ChangeLogBullet.Match(lines[fromLine]);
        var itemIndent = itemMatch.Success ? itemMatch.Groups["indent"].Length : 0;
        for (var i = fromLine + 1; i < toLine; i++)
        {
            var m = ChangeLogBullet.Match(lines[i]);
            if (m.Success && m.Groups["indent"].Length <= itemIndent) return true;
        }
        return false;
    }

    private static string ExtractSectionHtml(string[] lines, string exactHeading)
    {
        var start = Array.FindIndex(lines, l => l.TrimEnd() == exactHeading);
        if (start < 0) return string.Empty;

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }
        }

        var slice = string.Join("\n", lines[(start + 1)..end]);
        return MarkdownConverter.RenderBlock(slice);
    }

    private sealed record ListEntry(int Number, string Title, string Goal, string? MetaRaw, bool IsFurtherDevelopment);

    private static List<ListEntry> ParseEpicList(string[] lines)
    {
        var headingIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Epic List");
        if (headingIdx < 0) return new List<ListEntry>();

        var end = lines.Length;
        for (var i = headingIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }
        }

        var dividerIdx = -1;
        for (var i = headingIdx + 1; i < end; i++)
        {
            if (lines[i].Contains("Vertical slice complete", StringComparison.OrdinalIgnoreCase)) { dividerIdx = i; break; }
        }

        var entryStarts = new List<(int Index, int Number, string Title)>();
        for (var i = headingIdx + 1; i < end; i++)
        {
            var m = ListEpicHeading.Match(lines[i]);
            if (m.Success)
            {
                entryStarts.Add((i, int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim()));
            }
        }

        var results = new List<ListEntry>();
        for (var e = 0; e < entryStarts.Count; e++)
        {
            var (idx, number, title) = entryStarts[e];
            var bodyEnd = e + 1 < entryStarts.Count ? entryStarts[e + 1].Index : end;

            var goalLines = new List<string>();
            var metaLines = new List<string>();
            for (var i = idx + 1; i < bodyEnd; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line == "---") continue;
                if (line.StartsWith('*') && line.Contains("Vertical slice", StringComparison.OrdinalIgnoreCase)) continue;

                if (line.StartsWith("**FRs covered:**", StringComparison.Ordinal) || line.StartsWith("**NFRs covered:**", StringComparison.Ordinal))
                {
                    metaLines.Add(line);
                }
                else
                {
                    goalLines.Add(line);
                }
            }

            var isFurther = dividerIdx >= 0 && idx > dividerIdx;
            results.Add(new ListEntry(number, title, string.Join(" ", goalLines), metaLines.Count > 0 ? string.Join("\n", metaLines) : null, isFurther));
        }

        return results;
    }

    private sealed class SectionEntry
    {
        public int Number;
        public string Title = string.Empty;
        public string Goal = string.Empty;
        public string? MetaRaw;
        public List<StoryInfo> Stories = new();

        /// <summary>Rendered retirement/superseded notices found in this epic (Story 10.5, AC3) — either a
        /// story's own leading comment (see <see cref="ParseStory"/>) or one hoisted from between two stories
        /// by <see cref="HoistBetweenStoryRetiredComments"/> — collected in source order, empty when none
        /// matched.</summary>
        public List<string> RetiredNoticesHtml = new();
    }

    /// <summary>Story 10.5 AC3: finds and peels standalone retirement/superseded HTML comments that sit
    /// BETWEEN two stories in an epic's body — the real-world placement (the actual Story 3.4 notice sits
    /// after Story 3.3's last Acceptance-Criteria line and before the "### Story 3.5" heading, not inside
    /// either story's own region). A comment sitting immediately after a "### Story" heading is a DIFFERENT
    /// shape — a story marked retired via its own leading comment — and is left untouched here;
    /// <see cref="ParseStory"/>'s leading-comment peel already classifies that case. Blanks the matched
    /// comment's own lines in place (to empty strings) so they degrade to the blank lines the existing AC-block
    /// parser already skips, rather than being swept in as literal AC-block text (today's quirk). An
    /// unterminated <c>&lt;!--</c> or a comment that doesn't match a retirement keyword is left completely
    /// untouched (NFR8 — no behavior change for ordinary between-story content).</summary>
    private static List<string> HoistBetweenStoryRetiredComments(
        string[] lines, int scanStart, int scanEnd, IReadOnlyList<(int Index, int EpicNum, int StoryNum, string Title)> storyStarts)
    {
        var notices = new List<string>();
        var headingLines = new HashSet<int>(storyStarts.Select(s => s.Index));

        var i = scanStart;
        while (i < scanEnd)
        {
            if (!lines[i].TrimStart().StartsWith("<!--", StringComparison.Ordinal)) { i++; continue; }

            var startLine = i;
            var closeLine = -1;
            for (var j = i; j < scanEnd; j++)
            {
                if (lines[j].Contains("-->", StringComparison.Ordinal)) { closeLine = j; break; }
            }
            if (closeLine < 0) { i++; continue; } // unterminated — leave alone, mirrors LeadingHtmlComments' degrade.

            var precedingNonBlank = PrecedingNonBlankLineIndex(lines, startLine);
            var isLeadingCommentOfAStory = precedingNonBlank >= 0 && headingLines.Contains(precedingNonBlank);
            if (!isLeadingCommentOfAStory)
            {
                var commentText = string.Join("\n", lines[startLine..(closeLine + 1)]);
                if (RetirementKeyword.IsMatch(commentText))
                {
                    notices.Add(MarkdownConverter.RenderBlock(commentText));
                    for (var k = startLine; k <= closeLine; k++) lines[k] = string.Empty;
                }
            }

            i = closeLine + 1;
        }

        return notices;
    }

    /// <summary>The index of the nearest non-blank line before <paramref name="fromExclusive"/>, or -1 when
    /// every earlier line is blank (or <paramref name="fromExclusive"/> is 0).</summary>
    private static int PrecedingNonBlankLineIndex(string[] lines, int fromExclusive)
    {
        for (var k = fromExclusive - 1; k >= 0; k--)
        {
            if (lines[k].Trim().Length > 0) return k;
        }
        return -1;
    }

    private static List<SectionEntry> ParseEpicSections(string[] lines)
    {
        var epicStarts = new List<(int Index, int Number, string Title)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = SectionEpicHeading.Match(lines[i]);
            if (m.Success)
            {
                epicStarts.Add((i, int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim()));
            }
        }

        var results = new List<SectionEntry>();
        for (var e = 0; e < epicStarts.Count; e++)
        {
            var (idx, number, title) = epicStarts[e];
            var bodyEnd = e + 1 < epicStarts.Count ? epicStarts[e + 1].Index : lines.Length;

            var storyStarts = new List<(int Index, int EpicNum, int StoryNum, string Title)>();
            for (var i = idx + 1; i < bodyEnd; i++)
            {
                var sm = StoryHeading.Match(lines[i]);
                if (sm.Success)
                {
                    storyStarts.Add((i, int.Parse(sm.Groups[1].Value), int.Parse(sm.Groups[2].Value), sm.Groups[3].Value.Trim()));
                }
            }

            // Story 10.5 AC3: hoist retirement/superseded comments that sit BETWEEN two stories — the real
            // placement (e.g. the actual Story 3.4 notice sits after Story 3.3's last AC line and before
            // "### Story 3.5", not inside either story's own body) — before any story is parsed, so the
            // comment's lines never pollute the preceding story's trailing AC-block text (today's swallow-as-
            // AC-content quirk) and never become the following story's leading-comment note.
            var betweenStoryRetiredNotices = storyStarts.Count > 0
                ? HoistBetweenStoryRetiredComments(lines, storyStarts[0].Index, bodyEnd, storyStarts)
                : new List<string>();

            var preambleEnd = storyStarts.Count > 0 ? storyStarts[0].Index : bodyEnd;
            var goalLines = new List<string>();
            var metaLines = new List<string>();
            for (var i = idx + 1; i < preambleEnd; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("**FRs covered:**", StringComparison.Ordinal) || line.StartsWith("**NFRs covered:**", StringComparison.Ordinal))
                {
                    metaLines.Add(line);
                }
                else
                {
                    goalLines.Add(line);
                }
            }

            var section = new SectionEntry
            {
                Number = number,
                Title = title,
                Goal = string.Join(" ", goalLines),
                MetaRaw = metaLines.Count > 0 ? string.Join("\n", metaLines) : null,
            };
            section.RetiredNoticesHtml.AddRange(betweenStoryRetiredNotices);

            for (var s = 0; s < storyStarts.Count; s++)
            {
                var (sIdx, epicNum, storyNum, storyTitle) = storyStarts[s];
                var storyEnd = s + 1 < storyStarts.Count ? storyStarts[s + 1].Index : bodyEnd;
                var (story, retiredNoticeHtml) = ParseStory(lines, sIdx, storyEnd, epicNum, storyNum, storyTitle);
                section.Stories.Add(story);
                if (retiredNoticeHtml is { Length: > 0 })
                {
                    section.RetiredNoticesHtml.Add(retiredNoticeHtml);
                }
            }

            results.Add(section);
        }

        return results;
    }

    /// <summary>Returns the parsed story plus, when its leading comment is classified as a retirement/superseded
    /// notice (Story 10.5, AC3), that notice's rendered HTML (null otherwise) — the caller diverts it to the
    /// epic's Retired section instead of attaching it as this story's <see cref="StoryInfo.UserStoryNoteHtml"/>.</summary>
    private static (StoryInfo Story, string? RetiredNoticeHtml) ParseStory(string[] lines, int startIdx, int endIdx, int epicNum, int storyNum, string title)
    {
        var acIdx = -1;
        for (var i = startIdx + 1; i < endIdx; i++)
        {
            if (AcHeading.IsMatch(lines[i].Trim())) { acIdx = i; break; }
        }

        var userStoryEnd = acIdx >= 0 ? acIdx : endIdx;

        // Peel any leading HTML comment(s) off the front of the user-story region and render them as their own
        // block (RenderBlock → the block-comment renderer's marker-free .md-comment aside), leaving the
        // As-a/I-want narrative to keep its single-line join. Folding both together would collapse the block
        // comment to inline text — and inline HTML comments forbid the "--" these seat-mapping notes carry, so
        // Markdig would emit the literal <!-- --> markers into the italic blurb. Peeling only the *leading*
        // run (lazy match to the first "-->") means narrative that follows the close marker — even on the same
        // line — stays in the narrative, an unterminated "<!--" leaves the region untouched rather than eating
        // the story, and a comment authored below the narrative is left in place rather than hoisted up.
        var regionText = string.Join("\n", lines[(startIdx + 1)..userStoryEnd]);
        var leading = LeadingHtmlComments.Match(regionText);
        var commentMd = leading.Success ? leading.Value.Trim() : string.Empty;
        var narrativeText = leading.Success ? regionText[leading.Length..] : regionText;

        // A leading comment that reads as a retirement/superseded notice (Story 10.5, AC3) is classified —
        // not attached as this story's note — so it never renders inline above an active story card; an
        // ordinary seat-mapping note (no keyword match) stays exactly where it is (degrade, NFR8).
        var hasCommentText = HasCommentText(commentMd);
        var isRetiredNotice = hasCommentText && RetirementKeyword.IsMatch(commentMd);
        var userStoryNoteHtml = hasCommentText && !isRetiredNotice
            ? MarkdownConverter.RenderBlock(commentMd)
            : string.Empty;
        var retiredNoticeHtml = isRetiredNotice ? MarkdownConverter.RenderBlock(commentMd) : null;
        var narrativeLines = narrativeText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        var userStoryHtml = MarkdownConverter.RenderInline(JoinUserStoryLines(narrativeLines));

        var acBlocks = new List<string>();
        var trailingNotes = new List<string>();
        if (acIdx >= 0)
        {
            var currentBlockLines = new List<string>();
            int? pendingNumber = null;

            void FlushBlock()
            {
                // An empty flush keeps pendingNumber: a bare "1." line is followed by a **Given** line,
                // and both trigger a flush before the block's content has accumulated.
                if (currentBlockLines.Count == 0) return;

                // Same visual grammar as the story page's criterion rows: an optional gold "AC #N" label
                // column beside a body of block-level gherkin lines (see .ac-block / .gherkin-line CSS).
                var body = string.Concat(currentBlockLines.Select(l => $"<span class=\"gherkin-line\">{RenderAcLine(l)}</span>"));
                var label = pendingNumber is { } n ? $"<span class=\"ac-num\">AC #{n}</span>" : string.Empty;
                acBlocks.Add($"{label}<span class=\"ac-block-body\">{body}</span>");
                currentBlockLines.Clear();
                pendingNumber = null;
            }

            for (var i = acIdx + 1; i < endIdx; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;

                // A non-retirement HTML comment sitting in the AC range (e.g. a correct-course note trailing
                // after the last AC line, before the next story heading) would otherwise be swept in as
                // literal gherkin content. Retirement/superseded comments never reach here — they're already
                // blanked by HoistBetweenStoryRetiredComments before ParseStory runs. Lazy match to the next
                // "-->", mirroring that same hoist; an unterminated "<!--" degrades to ordinary AC content.
                if (line.StartsWith("<!--", StringComparison.Ordinal))
                {
                    var closeLine = -1;
                    for (var j = i; j < endIdx; j++)
                    {
                        if (lines[j].Contains("-->", StringComparison.Ordinal)) { closeLine = j; break; }
                    }
                    if (closeLine >= 0)
                    {
                        FlushBlock();
                        var commentText = string.Join("\n", lines[i..(closeLine + 1)].Select(l => l.Trim()));
                        trailingNotes.Add(MarkdownConverter.RenderBlock(commentText));
                        i = closeLine;
                        continue;
                    }
                }

                // A bare "1." line numbers the block that follows; as content it would render as an
                // empty markdown <ol>. Flush whatever came before and let the number ride the next block.
                if (AcBareNumberLine.Match(line) is { Success: true } num && int.TryParse(num.Groups[1].Value, out var number))
                {
                    FlushBlock();
                    pendingNumber = number;
                    continue;
                }

                if (line.StartsWith("**Given**", StringComparison.Ordinal))
                {
                    FlushBlock();
                }
                currentBlockLines.Add(line);
            }
            FlushBlock();
        }

        var story = new StoryInfo
        {
            Id = $"{epicNum}.{storyNum}",
            EpicNumber = epicNum,
            Title = MarkdownConverter.RenderInline(title),
            UserStoryHtml = userStoryHtml,
            UserStoryNoteHtml = userStoryNoteHtml,
            AcBlocksHtml = acBlocks,
            TrailingNotesHtml = trailingNotes,
        };
        return (story, retiredNoticeHtml);
    }

    private static string RenderAcLine(string line)
    {
        var m = AcKeywordLine.Match(line);
        if (m.Success)
        {
            var keyword = m.Groups[1].Value;
            var rest = m.Groups[2].Value;
            // No literal space after the chip — its margin-right supplies a deterministic gap so the
            // clause text and the .gherkin-line hanging indent land on the same column.
            return $"{GherkinStyler.KeywordSpan(keyword)}{MarkdownConverter.RenderInline(rest)}";
        }
        return MarkdownConverter.RenderInline(line);
    }

    /// <summary>True when a peeled leading-comment run has visible text between its markers, so an empty
    /// <c>&lt;!-- --&gt;</c> doesn't produce a hollow <c>.md-comment</c> aside above the story.</summary>
    private static bool HasCommentText(string commentMd)
        => commentMd.Length > 0 && commentMd.Replace("<!--", string.Empty).Replace("-->", string.Empty).Trim().Length > 0;

    /// <summary>Joins "As a X," / "I want Y," / "So that Z." lines into one sentence, lowercasing the
    /// leading word of continuation lines (except "I") to read naturally mid-sentence.</summary>
    private static string JoinUserStoryLines(List<string> lines)
    {
        var joined = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (i > 0 && line.Length > 0 && char.IsUpper(line[0]) && !line.StartsWith("I ", StringComparison.Ordinal))
            {
                line = char.ToLowerInvariant(line[0]) + line[1..];
            }
            joined.Add(line);
        }
        return string.Join(" ", joined);
    }

    private static string RenderMeta(string metaRaw)
    {
        var parts = metaRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            var m = MetaLine.Match(line.Trim());
            return m.Success
                ? $"{m.Groups[1].Value}: {MarkdownConverter.RenderInline(m.Groups[2].Value)}"
                : MarkdownConverter.RenderInline(line);
        });
        return string.Join(" &middot; ", parts);
    }
}
