namespace SpecScribe;

/// <summary>A generated per-commit detail page, recorded after emission (mirrors <see cref="CommitDayEntry"/>).
/// <paramref name="Hash"/> is the commit's full <c>%H</c> hash (the key the <c>_commitPages</c> resolver matches,
/// exactly or by prefix, so both the full-hash hub link and the abbreviated <c>%h</c> day-page link resolve).
/// [Story 7.5]</summary>
public sealed record CommitDetailEntry(string Hash, string OutputRelativePath);
