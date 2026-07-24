namespace SpecScribe;

/// <summary>A parsed BMad retrospective note (<c>epic-N-retro-DATE.md</c>): the epics it covers, title, header
/// meta (date + participants lifted out of the body), and the narrative body HTML (with the
/// <c>## Action Items</c> table's Status cells badged and the date/participant lines stripped). A first-class
/// artifact class rendered by <see cref="RetroTemplater"/>. [Story 2.3 retro pages]</summary>
public sealed class RetroModel
{
    /// <summary>Every epic this retro covers, de-duplicated and ascending — usually one, but a JOINT
    /// retrospective (<c>epic-19-21-retro-*.md</c>) covers several and must reach all of them. Holding the whole
    /// set here is what lets <c>SiteGenerator.SetRetros</c> fan one retro out to every epic that should show it.
    /// [spec-multi-epic-retro-attribution]</summary>
    /// NORMALIZED on construction rather than merely documented: <see cref="PrimaryEpicNumber"/>, the adapter
    /// sort and the retro pager all read "lowest covered epic" off index 0, so a caller passing `[21, 19]`
    /// would silently corrupt all three. The parser already guarantees this; enforcing it here makes the
    /// invariant total for every construction path, including tests.
    public required IReadOnlyList<int> EpicNumbers
    {
        get => _epicNumbers;
        init => _epicNumbers = value.Distinct().OrderBy(n => n).ToList();
    }

    private IReadOnlyList<int> _epicNumbers = Array.Empty<int>();

    /// <summary>The lowest epic covered — the retro's sort key and the epic its page leads with. Null only when
    /// the filename carried no usable number.</summary>
    public int? PrimaryEpicNumber => EpicNumbers.Count > 0 ? EpicNumbers[0] : null;

    public required string Title { get; init; }
    public string? DateText { get; init; }
    public required IReadOnlyList<string> Participants { get; init; }

    /// <summary>The rendered narrative (date/participant lines removed, action-items table badged).</summary>
    public required string BodyHtml { get; init; }

    public required string SourceRelativePath { get; init; }
    public required string OutputRelativePath { get; init; }
    public bool HasMermaid { get; init; }
}
