using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Requirements + progress tests share one realistic epics.md and drive the real pipeline
/// (EpicsParser → ProgressCalculator → RequirementsParser) rather than hand-building models.</summary>
public class RequirementsParserTests
{
    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: The game runs a day cycle
        FR2: Patients arrive

        **Village**
        FR3: The village grows

        ### NonFunctional Requirements

        NFR1: Loads fast

        ### FR Coverage Map

        FR1: Epic 1 - core loop
        FR2: Epic 1 - arrivals
        FR3: Deferred - post slice
        NFR1: Epic 1 - startup

        ## Epic List

        ### Epic 1: Foundation

        Stand it up.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want a skeleton, so that work can begin.
        """;

    private static RequirementsModel Parse(IReadOnlyDictionary<string, string>? artifacts = null)
    {
        var epics = EpicsParser.Parse(EpicsMd);
        var progress = ProgressCalculator.Compute(epics, artifacts ?? new Dictionary<string, string>(), git: null);
        return RequirementsParser.Parse(EpicsMd, epics, progress);
    }

    [Fact]
    public void Parse_SplitsFunctionalAndNonFunctional()
    {
        var model = Parse();

        Assert.Equal(3, model.Functional.Count);
        var nfr = Assert.Single(model.NonFunctional);
        Assert.Equal("NFR1", nfr.Id);
    }

    [Fact]
    public void Parse_TracksCategoriesForFunctionalRequirementsOnly()
    {
        var model = Parse();

        Assert.Equal("Core Loop", model.Functional[0].Category);
        Assert.Equal("Village", model.Functional[2].Category);
        Assert.Null(model.NonFunctional[0].Category);
    }

    [Fact]
    public void Parse_ReadsCoverageMap()
    {
        var model = Parse();

        Assert.Equal(1, model.ById["FR1"].CoverageEpicNumber);
        Assert.Equal("core loop", model.ById["FR1"].CoverageNote);
        Assert.True(model.ById["FR3"].Deferred);
        Assert.Equal(RequirementStatus.Deferred, model.ById["FR3"].Status);
    }

    [Fact]
    public void Parse_UncoveredEpicWithoutTaskPlansIsPlanned()
        => Assert.Equal(RequirementStatus.Planned, Parse().ById["FR1"].Status);

    [Fact]
    public void ById_IsCaseInsensitive()
        => Assert.True(Parse().ById.ContainsKey("fr1"));

    // ---- Story 3.7: multi-epic coverage (the structured FR→story mapping the Sankey stands on) ----

    private const string MultiEpicEpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: Single-epic requirement
        FR2: Multi-epic requirement
        FR3: Deferred requirement
        FR4: Unmapped requirement

        ### FR Coverage Map

        FR1: Epic 1 - just the first
        FR2: Epics 1 & 2 - spans two epics
        FR3: Deferred - post slice
        FR4: covered somewhere but no epic number

        ## Epic List

        ### Epic 1: Foundation

        Stand it up.

        ### Epic 2: Expansion

        Grow it.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want a skeleton, so that work can begin.

        ## Epic 2: Expansion

        ### Story 2.1: Widen

        As a dev, I want more surface, so that features fit.
        """;

    private static (RequirementsModel Reqs, EpicsModel Epics) ParseMultiEpic()
    {
        var epics = EpicsParser.Parse(MultiEpicEpicsMd);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        return (RequirementsParser.Parse(MultiEpicEpicsMd, epics, progress), epics);
    }

    [Fact]
    public void Parse_CapturesAllCoveringEpics_NotJustTheFirst()
    {
        var fr2 = ParseMultiEpic().Reqs.ById["FR2"];

        // Both covering epics are recorded, in order, de-duplicated.
        Assert.Equal(new[] { 1, 2 }, fr2.CoverageEpicNumbers);
        // ...while the singular primary is preserved as the FIRST covering epic (load-bearing for existing consumers).
        Assert.Equal(1, fr2.CoverageEpicNumber);
        // ...and status still rolls up from the primary epic (semantics unchanged).
        Assert.Equal(RequirementStatus.Planned, fr2.Status);
    }

    [Fact]
    public void Parse_SingleEpicCoverageHasOneElementList()
    {
        var fr1 = ParseMultiEpic().Reqs.ById["FR1"];
        Assert.Equal(new[] { 1 }, fr1.CoverageEpicNumbers);
        Assert.Equal(1, fr1.CoverageEpicNumber);
    }

    [Fact]
    public void Parse_DeferredAndUnmappedHaveEmptyCoverageEpics()
    {
        var reqs = ParseMultiEpic().Reqs;

        var deferred = reqs.ById["FR3"];
        Assert.True(deferred.Deferred);
        Assert.Empty(deferred.CoverageEpicNumbers);
        Assert.Null(deferred.CoverageEpicNumber);

        var unmapped = reqs.ById["FR4"];
        Assert.False(unmapped.Deferred);
        Assert.Empty(unmapped.CoverageEpicNumbers);
        Assert.Null(unmapped.CoverageEpicNumber);
    }

    [Fact]
    public void StoriesFor_ResolvesCoveringEpicsToTheirStories_InSourceOrder()
    {
        var (reqs, epics) = ParseMultiEpic();

        var fr2Stories = RequirementsParser.StoriesFor(reqs.ById["FR2"], epics).Select(s => s.Id).ToList();
        Assert.Equal(new[] { "1.1", "2.1" }, fr2Stories);

        // A deferred/unmapped requirement resolves to no stories.
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["FR3"], epics));
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["FR4"], epics));
    }

    [Fact]
    public void DeriveStatus_PartiallyImplemented_WhenACoveringEpicHasAnInProgressStory()
    {
        // Story 3.7 follow-up: requirements now surface a story-derived "partially implemented" (Active) tier
        // when a covering epic has work in flight — the earlier design refused this; now the FR→story mapping
        // backs it. A covering epic with an in-progress story rolls up to ForEpic == "active".
        var dir = Directory.CreateTempSubdirectory("ss-req-active-").FullName;
        try
        {
            var artifact = Path.Combine(dir, "1-1.md");
            File.WriteAllText(artifact, "# Story 1.1\nStatus: in progress\n\n## Tasks / Subtasks\n\n- [x] a\n- [ ] b\n");
            var epics = EpicsParser.Parse(MultiEpicEpicsMd);
            var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);
            var reqs = RequirementsParser.Parse(MultiEpicEpicsMd, epics, progress);

            // FR1 (Epic 1, in-progress story) and FR2 (Epics 1 & 2 — Epic 1 active) both read "partially implemented".
            Assert.Equal(RequirementStatus.Active, reqs.ById["FR1"].Status);
            Assert.Equal(RequirementStatus.Active, reqs.ById["FR2"].Status);
            // The uncovered/deferred ones are unaffected.
            Assert.Equal(RequirementStatus.Deferred, reqs.ById["FR3"].Status);
            Assert.Equal(RequirementStatus.Planned, reqs.ById["FR4"].Status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RenderIndex_PopulatedProject_ContainsStatusGridAndFlowPanel()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        // The status-block grid section (AC #1) with a labeled block per requirement...
        Assert.Contains("Requirements at a glance", html);
        Assert.Contains("req-status-grid", html);
        Assert.Contains("req-status-block", html);
        // ...and the requirements flow panel (AC #2), including the honest "No coverage" node.
        Assert.Contains("Requirements flow", html);
        Assert.Contains("req-flow-svg", html);
        Assert.Contains("No coverage", html);
    }
}

public class ProgressCalculatorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteArtifact(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static EpicsModel Epics => EpicsParser.Parse("""
        ## Epic List

        ### Epic 1: Foundation

        Goal.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want scaffolding.

        ### Story 1.2: Second story

        As a dev, I want more.
        """);

    [Fact]
    public void Compute_TalliesTasksFromArtifacts()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: in progress

            ## Tasks / Subtasks

            - [x] Done task
            - [ ] Open task
            - [X] Also done
            """);

        var epics = Epics;
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);

        Assert.Equal(2, progress.TasksDone);
        Assert.Equal(3, progress.TasksTotal);
        Assert.Equal(1, progress.StoriesWithArtifact);
        Assert.Equal(2, progress.StoriesTotal);

        // Side effect: the story itself is annotated for downstream rendering.
        var story = epics.Epics[0].Stories[0];
        Assert.Equal(2, story.TasksDone);
        Assert.Equal("in progress", story.Status);
    }

    [Fact]
    public void Compute_ZeroStateWhenNoArtifactsExist()
    {
        var progress = ProgressCalculator.Compute(Epics, new Dictionary<string, string>(), git: null);

        Assert.Equal(0, progress.TasksTotal);
        Assert.Equal(0, progress.StoriesWithArtifact);
        Assert.Equal(1, progress.EpicsDrafted);
        Assert.Equal(0, progress.EpicsPending);
    }

    [Fact]
    public void Compute_MissingArtifactFileCountsAsZeroInsteadOfThrowing()
    {
        var map = new Dictionary<string, string> { ["1.1"] = Path.Combine(_dir, "does-not-exist.md") };
        var progress = ProgressCalculator.Compute(Epics, map, git: null);

        Assert.Equal(0, progress.TasksTotal);
        Assert.Equal(1, progress.StoriesWithArtifact);
    }
}
