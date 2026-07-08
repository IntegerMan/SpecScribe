using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Recognizes and parses BMad retrospective notes (<c>epic-N-retro-DATE.md</c>) into a
/// <see cref="RetroModel"/>. Reuses <see cref="MarkdownConverter"/> for the narrative render, then lifts the
/// <c>**Date:**</c>/<c>**Participants:**</c> lines into the header and badges the action-items table (via
/// <see cref="RetroActionStyler"/>). [Story 2.3 retro pages]</summary>
public static class RetroParser
{
    private static readonly Regex FileName = new(@"^epic-(?<n>\d+)-retro\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DateLine = new(@"(?m)^\*\*Date:\*\*\s*(?<v>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ParticipantsLine = new(@"(?m)^\*\*Participants:\*\*\s*(?<v>.+?)\s*$", RegexOptions.Compiled);

    // The rendered date/participants paragraphs — removed from the narrative since they move to the header.
    private static readonly Regex RenderedMeta = new(
        @"<p><strong>(?:Date|Participants):</strong>.*?</p>\s*",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>True for a retrospective notes file (matched by the well-known <c>epic-N-retro-*</c> name).</summary>
    public static bool IsRetroFile(string path) => FileName.IsMatch(Path.GetFileNameWithoutExtension(path));

    /// <summary>The epic number a retro file belongs to, or null when the name doesn't carry one.</summary>
    public static int? EpicNumberOf(string path)
    {
        var m = FileName.Match(Path.GetFileNameWithoutExtension(path));
        return m.Success ? int.Parse(m.Groups["n"].Value) : null;
    }

    public static RetroModel Parse(string sourceFullPath, string sourceRelativePath, string outputRelativePath)
    {
        var raw = MarkdownConverter.ReadAllTextShared(sourceFullPath);
        var doc = MarkdownConverter.Convert(sourceFullPath, sourceRelativePath, outputRelativePath);

        var date = DateLine.Match(raw) is { Success: true } dm ? dm.Groups["v"].Value.Trim() : null;
        var participants = ParticipantsLine.Match(raw) is { Success: true } pm
            ? pm.Groups["v"].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
            : new List<string>();

        // Strip the date/participants paragraphs (now shown in the styled header) and badge the action items.
        var body = RetroActionStyler.Style(RenderedMeta.Replace(doc.BodyHtml, string.Empty));

        return new RetroModel
        {
            EpicNumber = EpicNumberOf(Path.GetFileName(sourceFullPath)) ?? 0,
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
