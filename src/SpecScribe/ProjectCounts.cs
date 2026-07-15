using System.Diagnostics;

namespace SpecScribe;

/// <summary>One portal-wide count ledger — THE single generator-side authority for the story/epic/task/
/// deferred/action-item families every summary widget and detail view reads. Built once per generation from
/// <see cref="ProgressModel"/> (epics.md plan of record), <see cref="SprintStatus"/> (yaml tracking ledger),
/// and <see cref="WorkInventory"/>; never re-counted at a render site. Named fields keep the plan-of-record
/// (<see cref="StoriesDefined"/>/<see cref="EpicsDefined"/>) distinct from the tracking ledger
/// (<see cref="StoriesTracked"/>/<see cref="EpicsTracked"/>) so a legitimate Defined≠Tracked difference is
/// signal, not a silent clash. Pure data — no I/O, no rendering. [Story 8.3; FR21]</summary>
public sealed record ProjectCounts
{
    /// <summary>Tracked-stage row shape shared with the sprint wheel / page summary
    /// (<see cref="SprintTemplater.StoryStageCounts"/>). Label + count + status css class.</summary>
    public readonly record struct StageCount(string Label, int Count, string CssClass);

    public required int EpicsDefined { get; init; }
    public required int EpicsDrafted { get; init; }
    public required int EpicsPending { get; init; }
    public required int StoriesDefined { get; init; }
    public required int StoriesWithArtifact { get; init; }
    public required int TasksDone { get; init; }
    public required int TasksTotal { get; init; }

    public required int EpicsTracked { get; init; }
    public required int StoriesTracked { get; init; }

    /// <summary>Per-stage tallies over yaml-tracked stories only, in sprint StageOrder
    /// (done → … → unrecognized). Sums to <see cref="StoriesTracked"/>.</summary>
    public required IReadOnlyList<StageCount> TrackedStoryStages { get; init; }

    /// <summary>Per-stage tallies over epics.md-defined stories (StatusStyles.StoryStages partition).
    /// Sums to <see cref="StoriesDefined"/>.</summary>
    public required IReadOnlyList<StageCount> DefinedStoryStages { get; init; }

    public required int DeferredOpenItems { get; init; }
    public required int DirectChanges { get; init; }
    public required int OpenActionItems { get; init; }

    /// <summary>Story ids present in epics.md but absent from sprint-status.yaml (sorted).</summary>
    public required IReadOnlyList<string> UntrackedDefinedStories { get; init; }

    /// <summary>Story ids (or raw keys) present in sprint-status.yaml with no matching epics.md story (sorted).</summary>
    public required IReadOnlyList<string> OrphanTrackedRows { get; init; }

    /// <summary>True when either reconciliation list is non-empty — emit exactly one non-fatal notice.</summary>
    public bool HasDivergence => UntrackedDefinedStories.Count > 0 || OrphanTrackedRows.Count > 0;

    public static readonly ProjectCounts Empty = new()
    {
        EpicsDefined = 0,
        EpicsDrafted = 0,
        EpicsPending = 0,
        StoriesDefined = 0,
        StoriesWithArtifact = 0,
        TasksDone = 0,
        TasksTotal = 0,
        EpicsTracked = 0,
        StoriesTracked = 0,
        TrackedStoryStages = Array.Empty<StageCount>(),
        DefinedStoryStages = Array.Empty<StageCount>(),
        DeferredOpenItems = 0,
        DirectChanges = 0,
        OpenActionItems = 0,
        UntrackedDefinedStories = Array.Empty<string>(),
        OrphanTrackedRows = Array.Empty<string>(),
    };

    /// <summary>Lifecycle display order for tracked (sprint yaml) tallies — mirrors
    /// <see cref="SprintTemplater"/> StageOrder so the wheel and page summary stay aligned. [Story 8.3]</summary>
    private static readonly (string CssClass, string Label)[] TrackedStageOrder =
    {
        ("done", "Done"),
        ("review", "In review"),
        ("active", "In progress"),
        ("ready", "Ready for dev"),
        ("pending", "Backlog"),
        ("retired", "Retired"),
        ("unrecognized", "Unrecognized"),
    };

    /// <summary>Builds the portal-wide count ledger. <paramref name="epics"/> supplies story ids for
    /// epics.md↔yaml reconciliation (when null or sprint empty, reconciliation lists stay empty and no
    /// divergence is reported — graceful degradation). Pure and deterministic. [Story 8.3]</summary>
    public static ProjectCounts Build(
        ProgressModel progress,
        SprintStatus? sprint,
        WorkInventory work,
        EpicsModel? epics = null)
    {
        progress ??= ProgressModel.Empty;
        work ??= WorkInventory.Empty;

        var definedStages = BuildDefinedStoryStages(progress);
        var definedStageSum = definedStages.Sum(s => s.Count);
        // Partition invariant: every ProgressCalculator-built story lands in exactly one stage.
        // Hand-built ProgressModel stubs (StoriesTotal set, PerEpic empty) skip the check.
        if (progress.PerEpic.Count > 0)
        {
            Debug.Assert(definedStageSum == progress.StoriesTotal,
                $"Defined story stage partition must equal StoriesDefined; got Σ={definedStageSum}, total={progress.StoriesTotal}");
        }

        var hasSprint = sprint is { IsEmpty: false };
        var storyEntries = hasSprint
            ? sprint!.Entries.Where(e => e.Kind == SprintEntryKind.Story).ToList()
            : new List<SprintEntry>();
        var epicTracked = hasSprint
            ? sprint!.Entries.Count(e => e.Kind == SprintEntryKind.Epic)
            : 0;

        var trackedStages = BuildTrackedStoryStages(storyEntries);
        var storiesTracked = trackedStages.Sum(s => s.Count);
        Debug.Assert(storiesTracked == storyEntries.Count,
            $"Tracked story stage partition must equal StoriesTracked; got Σ={storiesTracked}, total={storyEntries.Count}");

        var (untracked, orphans) = hasSprint && epics is not null
            ? Reconcile(epics, storyEntries)
            : (Array.Empty<string>() as IReadOnlyList<string>, Array.Empty<string>() as IReadOnlyList<string>);

        return new ProjectCounts
        {
            EpicsDefined = progress.EpicsTotal,
            EpicsDrafted = progress.EpicsDrafted,
            EpicsPending = progress.EpicsPending,
            StoriesDefined = progress.StoriesTotal,
            StoriesWithArtifact = progress.StoriesWithArtifact,
            TasksDone = progress.TasksDone,
            TasksTotal = progress.TasksTotal,
            EpicsTracked = epicTracked,
            StoriesTracked = storiesTracked,
            TrackedStoryStages = trackedStages,
            DefinedStoryStages = definedStages,
            DeferredOpenItems = work.Deferred?.OpenItemCount ?? 0,
            DirectChanges = work.QuickDev.Count,
            OpenActionItems = sprint?.OpenActionItems.Count ?? 0,
            UntrackedDefinedStories = untracked,
            OrphanTrackedRows = orphans,
        };
    }

    /// <summary>Deterministic one-line notice for a divergent ledger — input-only, for the
    /// <see cref="AdapterDiagnostic"/> channel. [Story 8.3]</summary>
    public string DivergenceMessage()
    {
        var parts = new List<string>();
        if (UntrackedDefinedStories.Count > 0)
        {
            parts.Add($"{UntrackedDefinedStories.Count} defined {Plural(UntrackedDefinedStories.Count, "story", "stories")} missing from sprint-status.yaml ({string.Join(", ", UntrackedDefinedStories)})");
        }
        if (OrphanTrackedRows.Count > 0)
        {
            parts.Add($"{OrphanTrackedRows.Count} tracked {Plural(OrphanTrackedRows.Count, "row", "rows")} with no matching defined story ({string.Join(", ", OrphanTrackedRows)})");
        }
        return "Count divergence between epics.md and sprint-status.yaml: " + string.Join("; ", parts);
    }

    private static IReadOnlyList<StageCount> BuildDefinedStoryStages(ProgressModel progress)
    {
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var epic in progress.PerEpic)
        {
            foreach (var (status, n) in epic.StoryStatusCounts)
            {
                byStatus[status] = byStatus.GetValueOrDefault(status) + n;
            }
        }

        return StatusStyles.StoryStages
            .Select(css => new StageCount(LabelForDefined(css), byStatus.GetValueOrDefault(css), css))
            .ToList();
    }

    private static string LabelForDefined(string css) => css switch
    {
        "done" => "Done",
        "review" => "In review",
        "active" => "In development",
        "ready" => "Ready for dev",
        "drafted" => "Drafted",
        "unrecognized" => "Unrecognized",
        _ => css,
    };

    private static IReadOnlyList<StageCount> BuildTrackedStoryStages(IReadOnlyList<SprintEntry> stories) =>
        TrackedStageOrder
            .Select(s => new StageCount(
                s.Label,
                stories.Count(st => StatusStyles.ForSprint(st.Status) == s.CssClass),
                s.CssClass))
            .ToList();

    private static (IReadOnlyList<string> Untracked, IReadOnlyList<string> Orphans) Reconcile(
        EpicsModel epics, IReadOnlyList<SprintEntry> storyEntries)
    {
        var definedIds = epics.Epics
            .SelectMany(e => e.Stories)
            .Select(s => s.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var trackedById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in storyEntries)
        {
            var id = StoryIdOf(entry);
            // First raw key wins for orphan reporting; deterministic via later sort of values.
            trackedById.TryAdd(id, entry.EpicNumber is { } e && entry.StoryMinor is { } m
                ? $"{e}.{m}"
                : entry.RawKey);
        }

        var trackedIds = trackedById.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var untracked = definedIds
            .Where(id => !trackedIds.Contains(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orphans = trackedById
            .Where(kv => !definedIds.Contains(kv.Key))
            .Select(kv => kv.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (untracked, orphans);
    }

    private static string StoryIdOf(SprintEntry entry) =>
        entry.EpicNumber is { } e && entry.StoryMinor is { } m
            ? $"{e}.{m}"
            : entry.RawKey;

    private static string Plural(int n, string one, string many) => n == 1 ? one : many;
}
