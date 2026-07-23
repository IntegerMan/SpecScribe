namespace SpecScribe;

/// <summary>The delivery-cadence dataset (Story 21.2): how story <em>completions</em> have flowed over time, and,
/// where derivable, how long each story took from first-touch to done. Distinct from
/// <see cref="Charts.ChartMetric.ActivityCadence"/> (raw commit activity) — this is keyed on when <em>stories</em>
/// reached done, a sparser signal.
/// <para><paramref name="CompletionSeries"/> is the per-day done-story count (ascending, same shape
/// <see cref="Charts.CommitHeatmap"/> consumes); <paramref name="CompletionsByDay"/> maps each of those days to the
/// stories completed that day (for cell links / same-day tooltips); <paramref name="CycleTimes"/> is one
/// (story, day-delta) entry per done story whose first-touch AND done-date both resolved and produced a
/// non-negative span. All three degrade honestly to empty rather than fabricating (NFR8/AC #2).</para></summary>
public sealed record DeliveryCadenceData(
    IReadOnlyList<(DateOnly Day, int Count)> CompletionSeries,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<StoryInfo>> CompletionsByDay,
    IReadOnlyList<(string StoryId, int Days)> CycleTimes)
{
    public static readonly DeliveryCadenceData Empty = new(
        Array.Empty<(DateOnly, int)>(),
        new Dictionary<DateOnly, IReadOnlyList<StoryInfo>>(),
        Array.Empty<(string, int)>());

    /// <summary>True when there is nothing to chart on either axis — the honest whole-surface empty state.
    /// The dedicated page still renders (each chart shows its own empty note); the dashboard strip omits.</summary>
    public bool IsEmpty => CompletionSeries.Count == 0 && CycleTimes.Count == 0;

    /// <summary>Total done stories placed on the completion cadence (the sum of the per-day counts). A done
    /// story with a null <see cref="StoryInfo.LastUpdatedDate"/> can't be placed, so it isn't counted here.</summary>
    public int TotalCompletions => CompletionSeries.Sum(s => s.Count);
}

/// <summary>Builds the <see cref="DeliveryCadenceData"/> from the resolved epics roster — a plain, pure static
/// builder (the <see cref="ArtifactCoverage"/>/<see cref="WorkInventory"/> shape: static class + records, no DI),
/// unit-testable without a repo or disk. The only data that isn't already in-model is a story's <em>first-touch</em>
/// date, supplied by an <em>injectable</em> resolver so the whole thing tests over constructed fixtures the way
/// <see cref="GitMetrics.ParseNumstatLog"/> does; production wires in <see cref="GitFirstTouchResolver"/>.
/// Never throws — any per-story resolution failure just excludes that one story; a total absence of done stories
/// is a normal, expected empty result (AD-4 / NFR2). [Story 21.2]</summary>
public static class DeliveryCadence
{
    /// <summary>Assembles the dataset. Completion cadence needs zero git work — it reuses each done story's
    /// already-computed <see cref="StoryInfo.LastUpdatedDate"/> (Story 8.8's git-date-else-Change-Log-else-null
    /// resolver). Cycle-time is computed only when <paramref name="firstTouch"/> is supplied; a null resolver
    /// yields no cycle-times (cadence-only), which is exactly what the dashboard strip needs.
    /// <list type="bullet">
    /// <item>Done = <see cref="StatusStyles.ForStory"/> == "done" — the single classifier, never a raw compare.</item>
    /// <item>A done story with a null <see cref="StoryInfo.LastUpdatedDate"/> can't be placed on the cadence and
    /// is excluded (never fabricated).</item>
    /// <item>Cycle-time = done-date − first-touch, kept only when both resolve and the span is non-negative; a
    /// negative span (a clearly bad first-touch match) is skipped, never clamped to zero.</item>
    /// </list></summary>
    public static DeliveryCadenceData Build(EpicsModel? epics, Func<StoryInfo, DateOnly?>? firstTouch = null)
    {
        if (epics is null) return DeliveryCadenceData.Empty;

        var doneStories = epics.Epics
            .SelectMany(e => e.Stories)
            .Where(s => StatusStyles.ForStory(s) == "done")
            .ToList();

        // Completion cadence — bucket done stories with a resolvable done-date by day (SortedDictionary keeps the
        // series ascending and the whole result deterministic regardless of roster iteration order).
        var byDay = new SortedDictionary<DateOnly, List<StoryInfo>>();
        foreach (var story in doneStories)
        {
            if (story.LastUpdatedDate is not { } day) continue; // null → cannot place; exclude, don't guess.
            if (!byDay.TryGetValue(day, out var list))
            {
                list = new List<StoryInfo>();
                byDay[day] = list;
            }
            list.Add(story);
        }

        var completionSeries = byDay
            .Select(kv => (Day: kv.Key, Count: kv.Value.Count))
            .ToList();
        var completionsByDay = byDay.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<StoryInfo>)kv.Value
                .OrderBy(s => s.EpicNumber)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList());

        // Cycle-time — done-date − first-touch, per done story where both resolve and the span is non-negative.
        var cycleTimes = new List<(string StoryId, int Days)>();
        if (firstTouch is not null)
        {
            foreach (var story in doneStories)
            {
                if (story.LastUpdatedDate is not { } done) continue;
                if (firstTouch(story) is not { } first) continue;   // unresolved first-touch → skip.
                var days = done.DayNumber - first.DayNumber;
                if (days < 0) continue;                             // clearly bad match → skip, never clamp to 0.
                cycleTimes.Add((story.Id, days));
            }
        }

        return new DeliveryCadenceData(completionSeries, completionsByDay, cycleTimes);
    }

    /// <summary>The production first-touch resolver: maps a story to its artifact file's true first-commit date via
    /// the bounded, never-throwing <see cref="GitMetrics.TryGetFirstCommitDate"/>. The lookup key mirrors
    /// <see cref="ProgressCalculator.ResolveLastUpdated"/> exactly (<c>{SourceDirName}/{ArtifactSourcePath}</c>), so
    /// the cycle-time's two endpoints are measured against the SAME file. A story with no
    /// <see cref="StoryInfo.ArtifactSourcePath"/> (an undrafted story) resolves to null and drops out of cycle-time.
    /// Runs at most once per done story with a resolvable done-date — bounded by story count, not commits. [Story 21.2]</summary>
    public static Func<StoryInfo, DateOnly?> GitFirstTouchResolver(string repoRoot) => story =>
    {
        if (story.ArtifactSourcePath is not { Length: > 0 } sourceRel) return null;
        var repoRel = PathUtil.NormalizeSlashes($"{ForgeOptions.SourceDirName}/{sourceRel}");
        return GitMetrics.TryGetFirstCommitDate(repoRoot, repoRel);
    };
}
