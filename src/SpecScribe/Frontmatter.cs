namespace SpecScribe;

/// <summary>Parsed YAML frontmatter from a BMad markdown doc. Fields are optional — BMad docs vary widely in what they set.</summary>
public sealed class Frontmatter
{
    public string? Title { get; init; }
    public string? Project { get; init; }
    public string? Date { get; init; }
    public string? Author { get; init; }
    public string? Version { get; init; }
    public string? Status { get; init; }

    /// <summary>The <c>route</c> field a <c>bmad-quick-dev</c> spec artifact carries (e.g. <c>one-shot</c>),
    /// used to classify a doc as quick-dev/direct work. Null for the vast majority of BMad docs that don't
    /// set it.</summary>
    public string? Route { get; init; }

    /// <summary>The <c>type</c> field a quick-dev spec artifact carries (e.g. <c>chore</c>, <c>feature</c>) —
    /// optional context alongside <see cref="Route"/>. Null when unset.</summary>
    public string? Type { get; init; }

    public static readonly Frontmatter Empty = new();
}
