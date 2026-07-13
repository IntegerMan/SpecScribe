namespace SpecScribe;

/// <summary>Pure, IO-free helpers behind Story 7.3's activity timeline and generalized date pages: grouping the
/// read-only artifact-last-modified signal by day, and computing the union "date-page day set". Repo-free and
/// deterministic so every ordering/grouping rule is unit-testable without a generation pass, mirroring
/// <see cref="GitMetrics"/>' pure parsers. The date pages, the timeline list, and the heatmap all derive their
/// day set from <see cref="UnionDays"/>, so no surface can ever link a day that has no page. [Story 7.3]</summary>
public static class ActivityModel
{
    /// <summary>Groups <c>(day, label, href)</c> artifact-change tuples into a per-day map. Within a day the
    /// entries are deterministically ordered (label ordinal, then href) so the no-JS reading order is stable
    /// across runs; a duplicate <c>(label, href)</c> on the same day collapses to one entry. Callers clamp any
    /// future-skewed day to <c>today</c> before handing tuples in, so every key is a real, on-or-before-today
    /// day. Empty input yields an empty map (never null).</summary>
    public static IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> GroupArtifactsByDay(
        IEnumerable<(DateOnly Day, string Label, string Href)> artifacts)
    {
        var byDay = new Dictionary<DateOnly, List<(string Label, string Href)>>();
        foreach (var (day, label, href) in artifacts)
        {
            if (!byDay.TryGetValue(day, out var list))
            {
                byDay[day] = list = new List<(string Label, string Href)>();
            }

            if (!list.Any(e => string.Equals(e.Href, href, StringComparison.Ordinal)
                            && string.Equals(e.Label, label, StringComparison.Ordinal)))
            {
                list.Add((label, href));
            }
        }

        return byDay.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<(string Label, string Href)>)kv.Value
                .OrderBy(e => e.Label, StringComparer.Ordinal)
                .ThenBy(e => e.Href, StringComparer.Ordinal)
                .ToList());
    }

    /// <summary>The date-page day set: the union of the git commit days and the artifact-change days, distinct
    /// and ascending. Both the generated date pages and the timeline list (and its prev/next walk) derive from
    /// this one set, so a linked day can never point at a page that wasn't generated, and vice versa. Both
    /// inputs are expected to already be on-or-before-today (the callers filter/clamp); this only merges,
    /// de-duplicates, and sorts. Empty inputs yield an empty list.</summary>
    public static IReadOnlyList<DateOnly> UnionDays(IEnumerable<DateOnly> commitDays, IEnumerable<DateOnly> artifactDays)
    {
        var set = new SortedSet<DateOnly>();
        foreach (var day in commitDays) set.Add(day);
        foreach (var day in artifactDays) set.Add(day);
        return set.ToList();
    }
}
