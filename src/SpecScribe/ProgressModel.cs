namespace SpecScribe;

public sealed class EpicProgress
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required int StoryCount { get; init; }
    public required int StoriesWithArtifact { get; init; }
    public required int TasksDone { get; init; }
    public required int TasksTotal { get; init; }
    public required EpicStatus Status { get; init; }

    /// <summary>Per-story delivery-status tally for this epic, keyed by <see cref="StatusStyles"/> css class
    /// (done/review/active/ready/drafted/pending). Feeds the "Progress by Epic" delivery mosaic so a mid-dev
    /// epic renders its real mix rather than a full "detailed" ring. Empty for an epic with no stories.</summary>
    public required IReadOnlyDictionary<string, int> StoryStatusCounts { get; init; }
}

/// <summary>A single computed snapshot of project progress — epics/stories/tasks tallied from the parsed
/// epics.md and its resolved implementation artifacts, plus an optional git activity pulse. Recomputed
/// whenever epics.md or an implementation-artifacts file changes; reused as-is for unrelated doc saves.</summary>
public sealed class ProgressModel
{
    public required int EpicsTotal { get; init; }
    public required int EpicsDrafted { get; init; }
    public required int EpicsPending { get; init; }
    public required int StoriesTotal { get; init; }
    public required int StoriesWithArtifact { get; init; }
    public required int TasksDone { get; init; }
    public required int TasksTotal { get; init; }
    public required IReadOnlyList<EpicProgress> PerEpic { get; init; }
    public GitPulse? Git { get; init; }

    public static readonly ProgressModel Empty = new()
    {
        EpicsTotal = 0,
        EpicsDrafted = 0,
        EpicsPending = 0,
        StoriesTotal = 0,
        StoriesWithArtifact = 0,
        TasksDone = 0,
        TasksTotal = 0,
        PerEpic = Array.Empty<EpicProgress>(),
        Git = null,
    };
}
