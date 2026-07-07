namespace SpecScribe;

/// <summary>A generated per-day commit page, recorded after emission (mirrors <see cref="AdrEntry"/>). Kept so
/// the generator can expose the set of commit-day pages for future dashboard/index use and for tests.</summary>
public sealed record CommitDayEntry(DateOnly Date, string OutputRelativePath);
