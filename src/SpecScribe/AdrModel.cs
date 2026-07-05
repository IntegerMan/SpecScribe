namespace SpecScribe;

/// <summary>An Architecture Decision Record surfaced on the index. ADRs are hand-authored under
/// <c>docs/adrs/</c> (not <c>_bmad-output</c>), so they carry no BMad frontmatter — the number comes from the
/// filename prefix and the status from the record's "**Status:**" line.</summary>
public sealed record AdrEntry(
    string Title,
    string OutputRelativePath,
    string SourceRelativePath,
    string? Status,
    int? Number);
