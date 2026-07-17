using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 8.3 portal-wide count ledger — mapping, Defined-vs-Tracked,
/// reconciliation, Σ stages == total, Empty, and determinism. Pure (no IO).</summary>
public class ProjectCountsTests
{
    private static StoryInfo Story(string id, int epic, string title, string? status = null) => new()
    {
        Id = id,
        EpicNumber = epic,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
    };

    private static EpicInfo Epic(int number, string title, EpicStatus status, params StoryInfo[] stories) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = status,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    private static EpicsModel EpicsWith(params EpicInfo[] epics) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = epics,
    };

    private static ProgressModel ProgressFor(EpicsModel epics) =>
        ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);

    private static WorkInventory WorkWith(int quickDev, int? deferredOpen = null) => new()
    {
        QuickDev = Enumerable.Range(1, quickDev)
            .Select(i => new QuickDevEntry($"QD {i}", $"qd-{i}.html", null, null))
            .ToList(),
        Deferred = deferredOpen is { } n
            ? new DeferredWorkEntry("Deferred", "deferred-work.html", n)
            : null,
    };

    [Fact]
    public void Build_MapsEachFamilyFromProgressSprintAndWork()
    {
        var epics = EpicsWith(
            Epic(1, "Foundation", EpicStatus.Drafted, Story("1.1", 1, "A"), Story("1.2", 1, "B")),
            Epic(2, "Later", EpicStatus.Pending));
        var progress = ProgressFor(epics);
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              1-1-a: done
              1-2-b: in-progress
              epic-2: backlog
            action_items:
              - epic: 1
                action: Do the thing
                owner: Alice
                status: open
              - epic: 1
                action: Done already
                owner: Bob
                status: done
            """)!;
        var work = WorkWith(quickDev: 2, deferredOpen: 3);

        var counts = ProjectCounts.Build(progress, sprint, work, epics);

        Assert.Equal(2, counts.EpicsDefined);
        Assert.Equal(1, counts.EpicsDrafted);
        Assert.Equal(1, counts.EpicsPending);
        Assert.Equal(2, counts.StoriesDefined);
        Assert.Equal(2, counts.EpicsTracked);
        Assert.Equal(2, counts.StoriesTracked);
        Assert.Equal(2, counts.DirectChanges);
        Assert.Equal(3, counts.DeferredOpenItems);
        Assert.Equal(1, counts.OpenActionItems);
        Assert.False(counts.HasDivergence);
    }

    [Fact]
    public void Build_StoriesDefinedAndTrackedAreIndependent()
    {
        var epics = EpicsWith(Epic(1, "Foundation", EpicStatus.Drafted,
            Story("1.1", 1, "A"), Story("1.2", 1, "B"), Story("1.3", 1, "C")));
        var progress = ProgressFor(epics);
        // Yaml tracks only 1.1 + an orphan 9.9 — Defined=3, Tracked=2.
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              1-1-a: done
              9-9-orphan: backlog
            """)!;

        var counts = ProjectCounts.Build(progress, sprint, WorkInventory.Empty, epics);

        Assert.Equal(3, counts.StoriesDefined);
        Assert.Equal(2, counts.StoriesTracked);
        Assert.Equal(new[] { "1.2", "1.3" }, counts.UntrackedDefinedStories);
        Assert.Equal(new[] { "9.9" }, counts.OrphanTrackedRows);
        Assert.True(counts.HasDivergence);
    }

    [Fact]
    public void Build_StageSumsEqualStoryTotals()
    {
        var epics = EpicsWith(Epic(1, "Foundation", EpicStatus.Drafted,
            Story("1.1", 1, "A", "done"),
            Story("1.2", 1, "B", "in-progress"),
            Story("1.3", 1, "C")));
        var dir = Directory.CreateTempSubdirectory("pc-stages-").FullName;
        try
        {
            var a1 = Path.Combine(dir, "1-1.md");
            var a2 = Path.Combine(dir, "1-2.md");
            File.WriteAllText(a1, "# Story 1.1\nStatus: done\n");
            File.WriteAllText(a2, "# Story 1.2\nStatus: in-progress\n");
            var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>
            {
                ["1.1"] = a1,
                ["1.2"] = a2,
            }, git: null);
            var sprint = SprintStatusParser.Parse("""
                development_status:
                  1-1-a: done
                  1-2-b: in-progress
                  1-3-c: backlog
                """)!;

            var counts = ProjectCounts.Build(progress, sprint, WorkInventory.Empty, epics);

            Assert.Equal(counts.StoriesDefined, counts.DefinedStoryStages.Sum(s => s.Count));
            Assert.Equal(counts.StoriesTracked, counts.TrackedStoryStages.Sum(s => s.Count));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Build_TrackedStagePartitionIncludesRetired()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: done
              1-2-b: in-progress
              3-4-interactive-tree: retired
            """)!;

        var counts = ProjectCounts.Build(ProgressModel.Empty, sprint, WorkInventory.Empty);

        Assert.Equal(3, counts.StoriesTracked);
        Assert.Equal(counts.StoriesTracked, counts.TrackedStoryStages.Sum(s => s.Count));
        var retired = Assert.Single(counts.TrackedStoryStages, s => s.CssClass == "retired");
        Assert.Equal(1, retired.Count);
        Assert.Equal("Retired", retired.Label);
        Assert.Equal(0, counts.TrackedStoryStages.Single(s => s.CssClass == "unrecognized").Count);
    }

    [Fact]
    public void Build_OpenActionItemsWhenDevelopmentStatusEmpty()
    {
        var sprint = new SprintStatus
        {
            Entries = Array.Empty<SprintEntry>(),
            ActionItems = new[] { new SprintActionItem("Follow up", "open", 1, "Owner") },
        };

        var counts = ProjectCounts.Build(ProgressModel.Empty, sprint, WorkInventory.Empty);

        Assert.Equal(1, counts.OpenActionItems);
    }

    [Fact]
    public void Empty_IsAllZero()
    {
        var e = ProjectCounts.Empty;
        Assert.Equal(0, e.EpicsDefined);
        Assert.Equal(0, e.StoriesDefined);
        Assert.Equal(0, e.EpicsTracked);
        Assert.Equal(0, e.StoriesTracked);
        Assert.Equal(0, e.OpenActionItems);
        Assert.Equal(0, e.DeferredOpenItems);
        Assert.Equal(0, e.DirectChanges);
        Assert.Empty(e.UntrackedDefinedStories);
        Assert.Empty(e.OrphanTrackedRows);
        Assert.False(e.HasDivergence);
        Assert.Equal(0, e.RequirementsOverall.Total);
        Assert.Equal(0, e.RequirementsFunctional.Total);
    }

    [Fact]
    public void Build_NullRequirements_YieldsEmptySatisfaction_NoThrow()
    {
        var counts = ProjectCounts.Build(ProgressModel.Empty, sprint: null, WorkInventory.Empty);

        Assert.Equal(0, counts.RequirementsOverall.Total);
        Assert.Equal(0, counts.RequirementsOverall.Satisfied);
        Assert.Equal(0, counts.RequirementsOverall.InFlight);
        Assert.Equal(0, counts.RequirementsOverall.Tiers.Count(t => t.Count > 0));
    }

    [Fact]
    public void Build_AbsentSprint_TrackedCountsZero_NoDivergenceNotice()
    {
        var epics = EpicsWith(Epic(1, "Foundation", EpicStatus.Drafted, Story("1.1", 1, "A")));
        var progress = ProgressFor(epics);

        var counts = ProjectCounts.Build(progress, sprint: null, WorkInventory.Empty, epics);

        Assert.Equal(1, counts.StoriesDefined);
        Assert.Equal(0, counts.StoriesTracked);
        Assert.Equal(0, counts.EpicsTracked);
        Assert.Equal(0, counts.OpenActionItems);
        Assert.False(counts.HasDivergence);
        Assert.Empty(counts.UntrackedDefinedStories);
        Assert.Empty(counts.OrphanTrackedRows);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var epics = EpicsWith(Epic(1, "Foundation", EpicStatus.Drafted,
            Story("1.1", 1, "A"), Story("1.2", 1, "B")));
        var progress = ProgressFor(epics);
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: done
              9-9-orphan: backlog
            """)!;
        var work = WorkWith(1, 2);

        var a = ProjectCounts.Build(progress, sprint, work, epics);
        var b = ProjectCounts.Build(progress, sprint, work, epics);

        Assert.Equal(a.StoriesDefined, b.StoriesDefined);
        Assert.Equal(a.StoriesTracked, b.StoriesTracked);
        Assert.Equal(a.UntrackedDefinedStories, b.UntrackedDefinedStories);
        Assert.Equal(a.OrphanTrackedRows, b.OrphanTrackedRows);
        Assert.Equal(a.TrackedStoryStages, b.TrackedStoryStages);
        Assert.Equal(a.DivergenceMessage(), b.DivergenceMessage());
    }

    [Fact]
    public void DivergenceMessage_ListsBothSides_Deterministically()
    {
        var epics = EpicsWith(Epic(1, "Foundation", EpicStatus.Drafted,
            Story("1.1", 1, "A"), Story("1.2", 1, "B")));
        var progress = ProgressFor(epics);
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: done
              9-9-orphan: backlog
            """)!;

        var msg = ProjectCounts.Build(progress, sprint, WorkInventory.Empty, epics).DivergenceMessage();

        Assert.Contains("1.2", msg);
        Assert.Contains("9.9", msg);
        Assert.StartsWith("Count divergence between epics.md and sprint-status.yaml:", msg);
    }

    [Fact]
    public void Build_RequirementSatisfaction_SumsTiersAndFourReadings_AcrossEverything()
    {
        var requirements = new RequirementsModel
        {
            Functional = new[]
            {
                Req(RequirementKind.Functional, 1, RequirementStatus.Done),
                Req(RequirementKind.Functional, 2, RequirementStatus.Active),
                Req(RequirementKind.Functional, 3, RequirementStatus.Ready),
                Req(RequirementKind.Functional, 4, RequirementStatus.Planned),
                Req(RequirementKind.Functional, 5, RequirementStatus.Unmapped),
                Req(RequirementKind.Functional, 6, RequirementStatus.Deferred),
            },
            NonFunctional = new[]
            {
                Req(RequirementKind.NonFunctional, 1, RequirementStatus.Done),
                Req(RequirementKind.NonFunctional, 2, RequirementStatus.Unmapped),
            },
            Design = new[]
            {
                Req(RequirementKind.Design, 1, RequirementStatus.Active),
                Req(RequirementKind.Design, 2, RequirementStatus.Planned),
            },
        };

        var counts = ProjectCounts.Build(
            ProgressModel.Empty, sprint: null, WorkInventory.Empty, epics: null, requirements);

        var o = counts.RequirementsOverall;
        Assert.Equal(10, o.Total);
        Assert.Equal(2, o.Done);
        Assert.Equal(2, o.Active);
        Assert.Equal(1, o.Ready);
        Assert.Equal(2, o.Planned);
        Assert.Equal(2, o.Unmapped);
        Assert.Equal(1, o.Deferred);
        Assert.Equal(2, o.Satisfied);
        Assert.Equal(5, o.InFlight); // Active+Ready+Planned
        Assert.Equal(o.Done + o.Active + o.Ready + o.Planned + o.Unmapped + o.Deferred, o.Total);
        Assert.Equal(6, counts.RequirementsFunctional.Total);
        Assert.Equal(2, counts.RequirementsNonFunctional.Total);
        Assert.Equal(2, counts.RequirementsDesign.Total);
        Assert.Equal(
            counts.RequirementsFunctional.Total + counts.RequirementsNonFunctional.Total + counts.RequirementsDesign.Total,
            o.Total);

        // Tier CssClass routes through ForRequirement vocabulary (Unmapped → pending).
        Assert.Equal("pending", o.Tiers.Single(t => t.Label == "Not yet mapped").CssClass);
        Assert.Equal("pending", o.Tiers.Single(t => t.Label == "Planned").CssClass);
        Assert.Equal("done", o.Tiers.Single(t => t.Label == "Done").CssClass);
    }

    [Fact]
    public void Build_RequirementSatisfaction_IsDeterministic()
    {
        var requirements = new RequirementsModel
        {
            Functional = new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Done) },
            NonFunctional = Array.Empty<RequirementInfo>(),
            Design = new[] { Req(RequirementKind.Design, 1, RequirementStatus.Unmapped) },
        };

        var a = ProjectCounts.Build(ProgressModel.Empty, null, WorkInventory.Empty, null, requirements);
        var b = ProjectCounts.Build(ProgressModel.Empty, null, WorkInventory.Empty, null, requirements);

        Assert.Equal(a.RequirementsOverall.Total, b.RequirementsOverall.Total);
        Assert.Equal(a.RequirementsOverall.InFlight, b.RequirementsOverall.InFlight);
        Assert.Equal(a.RequirementsOverall.Tiers, b.RequirementsOverall.Tiers);
        Assert.Equal(a.RequirementsOverall.Readings, b.RequirementsOverall.Readings);
    }

    private static RequirementInfo Req(RequirementKind kind, int number, RequirementStatus status) => new()
    {
        Kind = kind,
        Number = number,
        TextHtml = kind + " " + number,
        CoverageEpicNumbers = status is RequirementStatus.Unmapped or RequirementStatus.Deferred
            ? Array.Empty<int>()
            : new[] { 1 },
        CoverageEpicNumber = status is RequirementStatus.Unmapped or RequirementStatus.Deferred ? null : 1,
        Deferred = status == RequirementStatus.Deferred,
        Status = status,
    };
}
