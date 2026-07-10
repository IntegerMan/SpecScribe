namespace SpecScribe;

/// <summary>An Architecture Decision Record surfaced on the index. ADRs are hand-authored outside
/// <c>_bmad-output</c> — in <c>docs/adrs/</c> by default, or whichever conventional home the ForgeOptions
/// probe (or <c>--adrs</c>) resolved. Format and organization vary across projects, so only the title and
/// link are guaranteed: <see cref="Number"/> derives from any of several filename schemes (0001-x, ADR-0001-x,
/// adr_1_x, …) and <see cref="Status"/> from a "**Status:**" line, a "## Status" section, or frontmatter —
/// each null when not derivable, and the record still renders (unnumbered records sort last, status-less
/// cards carry no badge). [Story 4.2 Task 2]</summary>
public sealed record AdrEntry(
    string Title,
    string OutputRelativePath,
    string SourceRelativePath,
    string? Status,
    int? Number);
