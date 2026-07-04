using System.Text.RegularExpressions;

namespace DocsForge;

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
    private static readonly Regex AcKeywordLine = new(@"^\*\*(Given|When|Then|And)\*\*\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex MetaLine = new(@"^\*\*(FRs|NFRs) covered:\*\*\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex StatusLine = new(@"^Status:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

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

    /// <summary>Splits a story implementation-artifact into its lead "## Story" blurb (the As-a/I-want/
    /// So-that narrative) and the rest of the plan (Acceptance Criteria onward), stopping before
    /// "## Dev Agent Record" — that section is pulled out separately as a table via
    /// <see cref="ExtractDevAgentRecord"/>. Lets the story page put the narrative ahead of its charts
    /// without duplicating either section.</summary>
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

        var remainderMd = string.Join("\n", lines[remainderStart..remainderEnd]);
        remainderMd = SourceCitationBrackets.Replace(remainderMd, "$1");
        return (blurbHtml, MarkdownConverter.RenderBlock(remainderMd));
    }

    // "[Source: _bmad-output/path.md — note]" is a plain bracketed citation, not markdown link syntax —
    // the brackets and "Source:" label are decorative noise once SourceLinkifier turns the path into a
    // real link, so they're stripped before rendering rather than left as literal punctuation around it.
    private static readonly Regex SourceCitationBrackets = new(@"\[Source:\s*(.*?)\]", RegexOptions.Compiled);

    private static readonly Regex DevAgentSubHeading = new(@"^### (.+)$", RegexOptions.Compiled);

    /// <summary>Pulls the "## Dev Agent Record" section's subsections (Agent Model Used, Debug Log
    /// References, Completion Notes List, File List) into label/content pairs for a compact table,
    /// instead of four mostly-empty headings buried at the bottom of the page.</summary>
    public static IReadOnlyList<(string Label, string ContentHtml)> ExtractDevAgentRecord(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var headingIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Dev Agent Record");
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

            for (var s = 0; s < storyStarts.Count; s++)
            {
                var (sIdx, epicNum, storyNum, storyTitle) = storyStarts[s];
                var storyEnd = s + 1 < storyStarts.Count ? storyStarts[s + 1].Index : bodyEnd;
                section.Stories.Add(ParseStory(lines, sIdx, storyEnd, epicNum, storyNum, storyTitle));
            }

            results.Add(section);
        }

        return results;
    }

    private static StoryInfo ParseStory(string[] lines, int startIdx, int endIdx, int epicNum, int storyNum, string title)
    {
        var acIdx = -1;
        for (var i = startIdx + 1; i < endIdx; i++)
        {
            if (AcHeading.IsMatch(lines[i].Trim())) { acIdx = i; break; }
        }

        var userStoryEnd = acIdx >= 0 ? acIdx : endIdx;
        var userStoryLines = new List<string>();
        for (var i = startIdx + 1; i < userStoryEnd; i++)
        {
            var line = lines[i].Trim();
            if (line.Length > 0) userStoryLines.Add(line);
        }
        var userStoryHtml = MarkdownConverter.RenderInline(JoinUserStoryLines(userStoryLines));

        var acBlocks = new List<string>();
        if (acIdx >= 0)
        {
            var currentBlockLines = new List<string>();

            void FlushBlock()
            {
                if (currentBlockLines.Count > 0)
                {
                    acBlocks.Add(string.Join("<br>", currentBlockLines.Select(RenderAcLine)));
                    currentBlockLines.Clear();
                }
            }

            for (var i = acIdx + 1; i < endIdx; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("**Given**", StringComparison.Ordinal))
                {
                    FlushBlock();
                }
                currentBlockLines.Add(line);
            }
            FlushBlock();
        }

        return new StoryInfo
        {
            Id = $"{epicNum}.{storyNum}",
            EpicNumber = epicNum,
            Title = MarkdownConverter.RenderInline(title),
            UserStoryHtml = userStoryHtml,
            AcBlocksHtml = acBlocks,
        };
    }

    private static string RenderAcLine(string line)
    {
        var m = AcKeywordLine.Match(line);
        if (m.Success)
        {
            var keyword = m.Groups[1].Value;
            var rest = m.Groups[2].Value;
            return $"<span class=\"kw\">{keyword}</span> {MarkdownConverter.RenderInline(rest)}";
        }
        return MarkdownConverter.RenderInline(line);
    }

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
