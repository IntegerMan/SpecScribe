namespace SpecScribe;

/// <summary>A single heading extracted from the rendered body, used for the in-page contents strip.</summary>
public sealed record Heading(int Level, string Text, string Id);

/// <summary>Everything needed to render one generated HTML page for one source markdown file.</summary>
public sealed class DocModel
{
    public required string SourceRelativePath { get; init; }
    public required string OutputRelativePath { get; init; }
    public required string Title { get; init; }
    public required Frontmatter Frontmatter { get; init; }
    public required string BodyHtml { get; init; }
    public required IReadOnlyList<Heading> Headings { get; init; }

    /// <summary>True when the rendered body contains a mermaid diagram, so the page template knows to inject the
    /// client-side mermaid init script (see <see cref="Mermaid.InitScript"/>).</summary>
    public bool HasMermaid { get; init; }

    /// <summary>Resolved cross-reference links to this doc's companion/source documents, attached by
    /// <see cref="SiteGenerator"/> after conversion (spec-kernel pages only, resolved by file existence so a
    /// missing target is never a broken link). Empty for every other doc, so the templater omits the block.
    /// Settable post-construction, mirroring how <see cref="StoryInfo.ArtifactOutputPath"/> is filled in the
    /// generation pass. [Story 2.2 Task 4]</summary>
    public IReadOnlyList<(string Label, string Href)> Companions { get; set; } = Array.Empty<(string, string)>();
}
