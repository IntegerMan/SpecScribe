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

    public static readonly Frontmatter Empty = new();
}
