namespace SpecScribe;

/// <summary>A parsed BMad retrospective note (<c>epic-N-retro-DATE.md</c>): its epic number, title, header meta
/// (date + participants lifted out of the body), and the narrative body HTML (with the <c>## Action Items</c>
/// table's Status cells badged and the date/participant lines stripped). A first-class artifact class rendered
/// by <see cref="RetroTemplater"/>. [Story 2.3 retro pages]</summary>
public sealed class RetroModel
{
    public required int EpicNumber { get; init; }
    public required string Title { get; init; }
    public string? DateText { get; init; }
    public required IReadOnlyList<string> Participants { get; init; }

    /// <summary>The rendered narrative (date/participant lines removed, action-items table badged).</summary>
    public required string BodyHtml { get; init; }

    public required string SourceRelativePath { get; init; }
    public required string OutputRelativePath { get; init; }
    public bool HasMermaid { get; init; }
}
