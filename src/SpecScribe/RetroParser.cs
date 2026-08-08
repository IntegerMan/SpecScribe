using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Recognizes and parses BMad retrospective notes (<c>epic-N-retro-DATE.md</c>) into a
/// <see cref="RetroModel"/>. Reuses <see cref="MarkdownConverter"/> for the narrative render, then lifts the
/// <c>**Date:**</c>/<c>**Participants:**</c> lines into the header and badges the action-items table (via
/// <see cref="RetroActionStyler"/>). [Story 2.3 retro pages]</summary>
public static class RetroParser
{
    // A retro may cover SEVERAL epics (a joint retrospective), so the name carries a RUN of epic numbers, not
    // one: `epic-19-21-retro-…`, `epics-19-and-21-retro-…`, `epic-19+21-retro-…`. The run stays anchored by the
    // literal `-retro` on purpose — without that anchor `epic-1-retro-2026-07-07` would greedily read the DATE
    // as epic numbers (1, 2026, 7, 7). With it, the greedy attempt finds no following `-retro` and backtracks to
    // the single `1`. [spec-multi-epic-retro-attribution]
    // Each epic token is BOUNDED to 1-3 ASCII digits, which is load-bearing twice over:
    //   * `epic-1-2026-07-07-retro.md` (date before `-retro`) would otherwise be read as epics 1/2026/7/7 and
    //     mark the real Epic 7 retro'd. The `-retro` anchor alone does not stop this — only the bound does.
    //   * An out-of-`int` run like `epic-99999999999-retro-*` would otherwise match here and then parse to
    //     NOTHING, consuming the file and attributing it to no epic at all. Bounded, it fails to match and so
    //     falls through to the unrecognized-retro diagnostic instead of vanishing.
    // ASCII `[0-9]` rather than `\d` on purpose: `\d` also matches Unicode decimal digits, which int.TryParse
    // then rejects — the same silent-drop hole by another route.
    // CultureInvariant is required with IgnoreCase: under tr-TR/az the dotted/dotless `I` makes `EPIC-…` fail
    // to case-fold onto `epic`, which would make a whole retro invisible on a Turkish-locale machine or CI box.
    private static readonly Regex FileName = TimedRegex.New(
        @"^epics?-(?<nums>[0-9]{1,3}(?:(?:-and-|[-+&])[0-9]{1,3})*)-retro\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Number = TimedRegex.New(@"[0-9]+", RegexOptions.Compiled);

    // An `epic…retro…` name we could NOT parse — reported as an Unsupported diagnostic rather than silently
    // dropped, because a silent drop is exactly the bug this spec fixes (a whole retro vanished, and with it two
    // epics' "Done" status). Anchored at `epic`/`epics` so unrelated files that merely mention a retro (e.g.
    // `spec-sunburst-retro-review-….md`) never trip it.
    // Requires a DIGIT right after the epic prefix, so a deliberate non-retro like `epics-retro-process.md`
    // stays quiet while `epic-3-retrospective-notes.md` — which really does name an epic and claim a retro,
    // yet won't parse — is still reported.
    private static readonly Regex RetroLooking = TimedRegex.New(
        @"^epics?-[0-9].*retro", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DateLine = TimedRegex.New(@"(?m)^\*\*Date:\*\*\s*(?<v>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ParticipantsLine = TimedRegex.New(@"(?m)^\*\*Participants:\*\*\s*(?<v>.+?)\s*$", RegexOptions.Compiled);

    // The rendered date/participants paragraphs — removed from the narrative since they move to the header.
    private static readonly Regex RenderedMeta = TimedRegex.New(
        @"<p><strong>(?:Date|Participants):</strong>.*?</p>\s*",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // The leading <h1> (the file's title) — dropped from the body since the styled page header already carries
    // it; leaving it in duplicated the title as an oversized in-body heading. [Story 2.3 retro standardize]
    private static readonly Regex LeadingH1 = TimedRegex.New(
        @"\A\s*<h1[^>]*>.*?</h1>\s*",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>True for a retrospective notes file (matched by the well-known <c>epic-N-retro-*</c> name, or
    /// its multi-epic forms such as <c>epic-19-21-retro-*</c>).</summary>
    public static bool IsRetroFile(string path) => FileName.IsMatch(Path.GetFileNameWithoutExtension(path));

    /// <summary>True for a file whose name reads like an epic retrospective but which
    /// <see cref="IsRetroFile"/> does not recognize — the caller reports it so an unhandled naming spelling
    /// surfaces as a visible skip instead of disappearing.</summary>
    public static bool LooksLikeUnrecognizedRetro(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!RetroLooking.IsMatch(name)) return false;

        // Keyed on "produced no epic to attribute to", NOT merely on regex non-match: a name that matches
        // FileName but yields zero usable numbers would otherwise be consumed and attributed to nothing while
        // this safety net stayed silent — the exact silent-drop this whole change exists to end.
        return EpicNumbersOf(path).Count == 0;
    }

    /// <summary>Every epic number a retro file covers — one for the usual <c>epic-N-retro-*</c>, several for a
    /// joint retrospective — de-duplicated and ascending. Empty when the name carries none.</summary>
    public static IReadOnlyList<int> EpicNumbersOf(string path)
    {
        var m = FileName.Match(Path.GetFileNameWithoutExtension(path));
        if (!m.Success) return Array.Empty<int>();

        return Number.Matches(m.Groups["nums"].Value)
            .Select(x => int.TryParse(x.Value, out var n) ? n : (int?)null)
            .Where(n => n is not null)
            .Select(n => n!.Value)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    public static RetroModel Parse(string sourceFullPath, string sourceRelativePath, string outputRelativePath)
    {
        var raw = MarkdownConverter.ReadAllTextShared(sourceFullPath);
        var doc = MarkdownConverter.Convert(sourceFullPath, sourceRelativePath, outputRelativePath);

        var date = DateLine.Match(raw) is { Success: true } dm ? dm.Groups["v"].Value.Trim() : null;
        var participants = ParticipantsLine.Match(raw) is { Success: true } pm
            ? pm.Groups["v"].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
            : new List<string>();

        // Strip the leading title h1 + the date/participants paragraphs (all shown in the styled header now)
        // and badge the action items.
        var stripped = LeadingH1.Replace(doc.BodyHtml, string.Empty);
        var body = RetroActionStyler.Style(RenderedMeta.Replace(stripped, string.Empty));

        return new RetroModel
        {
            EpicNumbers = EpicNumbersOf(Path.GetFileName(sourceFullPath)),
            Title = doc.Title,
            DateText = string.IsNullOrEmpty(date) ? null : date,
            Participants = participants,
            BodyHtml = body,
            HasMermaid = doc.HasMermaid,
            SourceRelativePath = sourceRelativePath,
            OutputRelativePath = outputRelativePath,
        };
    }
}
