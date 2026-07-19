using System.Diagnostics;

namespace SpecScribe;

/// <summary>One portal-wide count ledger — THE single generator-side authority for the story/epic/task/
/// deferred/action-item families every summary widget and detail view reads. Built once per generation from
/// <see cref="ProgressModel"/> (epics.md plan of record), <see cref="SprintStatus"/> (yaml tracking ledger),
/// and <see cref="WorkInventory"/>; never re-counted at a render site. Named fields keep the plan-of-record
/// (<see cref="StoriesDefined"/>/<see cref="EpicsDefined"/>) distinct from the tracking ledger
/// (<see cref="StoriesTracked"/>/<see cref="EpicsTracked"/>) so a legitimate Defined≠Tracked difference is
/// signal, not a silent clash. Pure data — no I/O, no rendering. [Story 8.3; FR21]
/// <para>Requirement-satisfaction buckets (Story 9.9) are computed over <see cref="RequirementsModel.Everything"/>
/// so Home and the requirements hub share one source for Satisfied / In flight / Deferred / Unmapped.</para></summary>
public sealed record ProjectCounts
{
    /// <summary>Tracked-stage row shape shared with the sprint wheel / page summary
    /// (<see cref="SprintTemplater.StoryStageCounts"/>). Label + count + status css class.</summary>
    public readonly record struct StageCount(string Label, int Count, string CssClass);

    /// <summary>Requirement-satisfaction tallies over one requirement set — six canonical
    /// <see cref="RequirementStatus"/> tiers plus the four-reading rollups (Satisfied / In flight /
    /// Deferred on purpose / Unmapped). Colors/words route through <see cref="StatusStyles"/>; the four
    /// readings are labels on brackets, not a parallel vocabulary. [Story 9.9]</summary>
    public sealed record RequirementSatisfaction
    {
        public required int Done { get; init; }
        public required int Active { get; init; }
        public required int Ready { get; init; }
        public required int Planned { get; init; }
        public required int Unmapped { get; init; }
        public required int Deferred { get; init; }

        public int Total => Done + Active + Ready + Planned + Unmapped + Deferred;
        public int Satisfied => Done;
        public int InFlight => Active + Ready + Planned;

        /// <summary>Six canonical tiers in Done→…→Deferred order — stacked-bar segments. CssClass from
        /// <see cref="StatusStyles.ForRequirement"/> (Unmapped→pending); Label from
        /// <see cref="StatusStyles.RequirementLabel"/>.</summary>
        public IReadOnlyList<StageCount> Tiers => new[]
        {
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Done), Done, "done"),
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Active), Active, "active"),
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Ready), Ready, "ready"),
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Planned), Planned, "pending"),
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Unmapped), Unmapped, "pending"),
            new StageCount(StatusStyles.RequirementLabel(RequirementStatus.Deferred), Deferred, "deferred"),
        };

        /// <summary>Four-reading chip rollups. In-flight CssClass is <c>active</c> (honest lifecycle expands
        /// in the chip tooltip); Unmapped keeps pending color + distinct word. [Story 9.9]</summary>
        public IReadOnlyList<StageCount> Readings => new[]
        {
            new StageCount("Satisfied", Satisfied, "done"),
            new StageCount("In flight", InFlight, "active"),
            new StageCount("Deferred on purpose", Deferred, "deferred"),
            new StageCount("Unmapped", Unmapped, "pending"),
        };

        public static readonly RequirementSatisfaction Empty = new()
        {
            Done = 0, Active = 0, Ready = 0, Planned = 0, Unmapped = 0, Deferred = 0,
        };

        public static RequirementSatisfaction From(IEnumerable<RequirementInfo>? reqs)
        {
            if (reqs is null) return Empty;
            var list = reqs as IReadOnlyList<RequirementInfo> ?? reqs.ToList();
            if (list.Count == 0) return Empty;
            return new RequirementSatisfaction
            {
                Done = list.Count(r => r.Status == RequirementStatus.Done),
                Active = list.Count(r => r.Status == RequirementStatus.Active),
                Ready = list.Count(r => r.Status == RequirementStatus.Ready),
                Planned = list.Count(r => r.Status == RequirementStatus.Planned),
                Unmapped = list.Count(r => r.Status == RequirementStatus.Unmapped),
                Deferred = list.Count(r => r.Status == RequirementStatus.Deferred),
            };
        }
    }

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

    /// <summary>Story ids that appear more than once in epics.md (sorted). First-wins membership still
    /// applies for untracked/orphan reconcile; these ids are named on the Unsupported channel so duplicates
    /// are not silent. [spec-epic8-deferred-debt-cleanup]</summary>
    public required IReadOnlyList<string> DuplicateDefinedStoryIds { get; init; }

    /// <summary>Satisfaction over <see cref="RequirementsModel.Everything"/> (FR+NFR+UX-DR). Empty when
    /// requirements were not supplied to <see cref="Build"/>. [Story 9.9]</summary>
    public required RequirementSatisfaction RequirementsOverall { get; init; }

    /// <summary>Per-kind satisfaction for Home requirement tiles — same six-tier shape. [Story 9.9]</summary>
    public required RequirementSatisfaction RequirementsFunctional { get; init; }
    public required RequirementSatisfaction RequirementsNonFunctional { get; init; }
    public required RequirementSatisfaction RequirementsDesign { get; init; }

    /// <summary>True when reconcile lists or duplicate defined ids are non-empty — emit exactly one non-fatal notice.</summary>
    public bool HasDivergence =>
        UntrackedDefinedStories.Count > 0
        || OrphanTrackedRows.Count > 0
        || DuplicateDefinedStoryIds.Count > 0;

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
        DuplicateDefinedStoryIds = Array.Empty<string>(),
        RequirementsOverall = RequirementSatisfaction.Empty,
        RequirementsFunctional = RequirementSatisfaction.Empty,
        RequirementsNonFunctional = RequirementSatisfaction.Empty,
        RequirementsDesign = RequirementSatisfaction.Empty,
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
    /// divergence is reported — graceful degradation). <paramref name="requirements"/> supplies
    /// satisfaction buckets over Everything (null/empty → empty buckets, no throw — NFR8). Pure and
    /// deterministic. [Story 8.3; Story 9.9]</summary>
    public static ProjectCounts Build(
        ProgressModel progress,
        SprintStatus? sprint,
        WorkInventory work,
        EpicsModel? epics = null,
        RequirementsModel? requirements = null)
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

        IReadOnlyList<string> untracked = Array.Empty<string>();
        IReadOnlyList<string> orphans = Array.Empty<string>();
        IReadOnlyList<string> duplicates = Array.Empty<string>();
        if (epics is not null)
        {
            (untracked, orphans, duplicates) = hasSprint
                ? Reconcile(epics, storyEntries)
                : (Array.Empty<string>(), Array.Empty<string>(), FindDuplicateDefinedIds(epics));
        }

        var overall = RequirementSatisfaction.From(requirements?.Everything);
        var functional = RequirementSatisfaction.From(requirements?.Functional);
        var nonFunctional = RequirementSatisfaction.From(requirements?.NonFunctional);
        var design = RequirementSatisfaction.From(requirements?.Design);
        if (overall.Total > 0)
        {
            Debug.Assert(overall.Total == functional.Total + nonFunctional.Total + design.Total,
                "Overall satisfaction must equal the sum of per-kind tallies");
        }

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
            DuplicateDefinedStoryIds = duplicates,
            RequirementsOverall = overall,
            RequirementsFunctional = functional,
            RequirementsNonFunctional = nonFunctional,
            RequirementsDesign = design,
        };
    }

    /// <summary>Max ids listed per side of <see cref="DivergenceMessage"/> before <c>+N more</c>.
    /// Totals in the prose stay accurate. [spec-epic8-deferred-debt-cleanup]</summary>
    internal const int DivergenceIdListCap = 10;

    /// <summary>Deterministic one-line notice for a divergent ledger — input-only, for the
    /// <see cref="AdapterDiagnostic"/> channel. [Story 8.3]</summary>
    public string DivergenceMessage()
    {
        var parts = new List<string>();
        if (DuplicateDefinedStoryIds.Count > 0)
        {
            parts.Add($"{DuplicateDefinedStoryIds.Count} duplicated story {Plural(DuplicateDefinedStoryIds.Count, "id", "ids")} in epics.md ({FormatIdList(DuplicateDefinedStoryIds)})");
        }
        if (UntrackedDefinedStories.Count > 0)
        {
            parts.Add($"{UntrackedDefinedStories.Count} defined {Plural(UntrackedDefinedStories.Count, "story", "stories")} missing from sprint-status.yaml ({FormatIdList(UntrackedDefinedStories)})");
        }
        if (OrphanTrackedRows.Count > 0)
        {
            parts.Add($"{OrphanTrackedRows.Count} tracked {Plural(OrphanTrackedRows.Count, "row", "rows")} with no matching defined story ({FormatIdList(OrphanTrackedRows)})");
        }

        // Duplicate-only (no untracked/orphan reconcile) is an epics.md integrity issue — don't blame sprint.
        if (DuplicateDefinedStoryIds.Count > 0
            && UntrackedDefinedStories.Count == 0
            && OrphanTrackedRows.Count == 0)
        {
            return "Count divergence in epics.md: " + string.Join("; ", parts);
        }

        return "Count divergence between epics.md and sprint-status.yaml: " + string.Join("; ", parts);
    }

    private static string FormatIdList(IReadOnlyList<string> ids)
    {
        if (ids.Count <= DivergenceIdListCap)
            return string.Join(", ", ids);
        var shown = string.Join(", ", ids.Take(DivergenceIdListCap));
        return $"{shown}, +{ids.Count - DivergenceIdListCap} more";
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

    private static string LabelForDefined(string css) => StatusStyles.StoryLabel(css);

    private static IReadOnlyList<StageCount> BuildTrackedStoryStages(IReadOnlyList<SprintEntry> stories) =>
        TrackedStageOrder
            .Select(s => new StageCount(
                s.Label,
                stories.Count(st => StatusStyles.ForSprint(st.Status) == s.CssClass),
                s.CssClass))
            .ToList();

    private static (IReadOnlyList<string> Untracked, IReadOnlyList<string> Orphans, IReadOnlyList<string> Duplicates) Reconcile(
        EpicsModel epics, IReadOnlyList<SprintEntry> storyEntries)
    {
        var duplicates = FindDuplicateDefinedIds(epics);
        // First-wins membership for reconcile — duplicates are reported separately, not collapsed silently.
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

        return (untracked, orphans, duplicates);
    }

    private static IReadOnlyList<string> FindDuplicateDefinedIds(EpicsModel epics) =>
        epics.Epics
            .SelectMany(e => e.Stories)
            .Select(s => s.Id)
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.First())
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string StoryIdOf(SprintEntry entry) =>
        entry.EpicNumber is { } e && entry.StoryMinor is { } m
            ? $"{e}.{m}"
            : entry.RawKey;

    private static string Plural(int n, string one, string many) => n == 1 ? one : many;
}
