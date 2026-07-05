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
