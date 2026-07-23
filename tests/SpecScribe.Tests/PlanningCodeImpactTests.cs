using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 21.3 <see cref="PlanningCodeImpact"/> correlation builder — pure over a
/// constructed <see cref="EpicsModel"/> roster plus synthetic <see cref="DeepCommit"/> lists (no repo, no disk,
/// the <c>GitMetrics.ParseNumstatLog</c> testing style). Fixtures model the REAL shapes observed in this repo's
/// own history (dotted "Story 6.7" subjects, hyphenated branch names like <c>worktree-bmad-dev-story-6-4</c>, merge
/// commits with a branch name but no files, and unattributable subjects like "Overnight code review"). Guards the
/// honesty rules: roster validation (never guess an id into existence), story→epic rollup, the Tier-2 merge-window
/// approximation and its boundaries, the code-page link gate, and the honest empty result.</summary>
public class PlanningCodeImpactTests
{
    private static StoryInfo Story(string id, int epicNumber) => new()
    {
        Id = id,
        EpicNumber = epicNumber,
        Title = $"Story {id}",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
    };

    private static EpicInfo Epic(int number, params StoryInfo[] stories) => new()
    {
        Number = number,
        Title = $"Epic {number}",
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    // A roster mirroring the real ids the story's design notes reference.
    private static EpicsModel Roster() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            Epic(3, Story("3.8", 3)),
            Epic(6, Story("6.4", 6), Story("6.7", 6)),
            Epic(7, Story("7.8", 7), Story("7.11", 7)),
            Epic(19, Story("19.2", 19)),
        },
    };

    private static DeepCommit Commit(string subject, string body = "", params string[] files) =>
        new("abc1234", "Author", null, subject, body,
            files.Select(f => new DeepFileChange(f, 1, 0)).ToArray());

    private static DeepCommit Merge(string subject) =>
        new("merge123", "Author", null, subject, string.Empty, Array.Empty<DeepFileChange>());

    // ----- TryExtractWorkItemRefs (pure text) ---------------------------------------------------------------

    [Theory]
    [InlineData("Story 6.7 deferred-work cleanup", 6, 7)]
    [InlineData("7.11", 7, 11)]
    [InlineData("fix: 7.10 dev revisions", 7, 10)]
    [InlineData("Merge branch 'worktree-bmad-dev-story-6-4'", 6, 4)]
    [InlineData("Merge branch 'worktree-story-7-8-related-files-graph'", 7, 8)]
    [InlineData("Merge branch 'spike/delivery-arch-6-6'", 6, 6)]
    [InlineData("Merge branch 'worktree-story-19-2-work-graph'", 19, 2)]
    public void TryExtractWorkItemRefs_FindsStoryPair(string text, int epic, int story)
    {
        var refs = PlanningCodeImpact.TryExtractWorkItemRefs(text);
        Assert.Contains(new PlanningCodeImpact.WorkItemRef(epic, story), refs);
    }

    [Theory]
    [InlineData("Epic 19 spike", 19)]
    [InlineData("epic-24 change coupling", 24)]
    public void TryExtractWorkItemRefs_FindsEpicOnly(string text, int epic)
    {
        var refs = PlanningCodeImpact.TryExtractWorkItemRefs(text);
        Assert.Contains(new PlanningCodeImpact.WorkItemRef(epic, null), refs);
    }

    [Theory]
    [InlineData("Overnight code review")]
    [InlineData("Adjustments")]
    [InlineData("Misc changes")]
    [InlineData("")]
    [InlineData(null)]
    public void TryExtractWorkItemRefs_EmptyWhenNoNumber(string? text)
    {
        Assert.Empty(PlanningCodeImpact.TryExtractWorkItemRefs(text));
    }

    [Fact]
    public void TryExtractWorkItemRefs_DoesNotExplodeArangeLikeSevenDotX()
    {
        // "7.x" carries no clean numeric pair — must not become story 7.anything.
        var refs = PlanningCodeImpact.TryExtractWorkItemRefs("Code review on 7.x");
        Assert.Empty(refs);
    }

    // ----- Tier 1: direct match + roster validation -------------------------------------------------------

    [Fact]
    public void Build_Tier1_AttributesValidatedStory()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("7.11 ownership insights", "", "src/GitMetrics.cs") });

        Assert.True(data.FilesByStory.ContainsKey("7.11"));
        Assert.Contains(data.FilesByStory["7.11"], f => f.Path == "src/GitMetrics.cs");
        Assert.Equal(1, data.AttributedCommitCount);
        Assert.Equal(1, data.TotalAnalyzedCommits);
    }

    [Fact]
    public void Build_Tier1_DiscardsUnvalidatedNumber()
    {
        // 99.1 is not on the roster — a commit naming it attributes nothing (never guessed into existence).
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("Story 99.1 imaginary", "", "src/Ghost.cs") });

        Assert.Empty(data.FilesByStory);
        Assert.Empty(data.FilesByEpic);
        Assert.Equal(0, data.AttributedCommitCount);
        Assert.Equal(1, data.TotalAnalyzedCommits);
    }

    [Fact]
    public void Build_Tier1_AttributesTwoDistinctStoriesFromOneCommit()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("Joint work on 7.11 and 7.8", "", "src/Shared.cs") });

        Assert.Contains(data.FilesByStory["7.11"], f => f.Path == "src/Shared.cs");
        Assert.Contains(data.FilesByStory["7.8"], f => f.Path == "src/Shared.cs");
        Assert.Equal(1, data.AttributedCommitCount);
    }

    [Fact]
    public void Build_StoryFilesRollUpIntoParentEpic()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("Story 6.7 fix", "", "src/A.cs") });

        Assert.Contains(data.FilesByStory["6.7"], f => f.Path == "src/A.cs");
        Assert.True(data.FilesByEpic.ContainsKey(6));
        Assert.Contains(data.FilesByEpic[6], f => f.Path == "src/A.cs");
    }

    [Fact]
    public void Build_EpicOnlyMention_AttributesEpicNotStory()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("Epic 19 groundwork", "", "src/Graph.cs") });

        Assert.Contains(data.FilesByEpic[19], f => f.Path == "src/Graph.cs");
        Assert.Empty(data.FilesByStory);
    }

    // ----- Tier 2: merge/branch linear-window backfill ----------------------------------------------------

    [Fact]
    public void Build_Tier2_MergeBranchBackfillsFollowingUnattributedCommits()
    {
        // A merge naming story 7.8, followed by two of its own commits that don't self-identify.
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[]
            {
                Merge("Merge branch 'worktree-story-7-8-related-files-graph'"),
                Commit("Fix tooltip clipping", "", "src/Charts.cs"),   // backfilled to 7.8
                Commit("Review notes", "", "src/EpicsView.cs"),        // backfilled to 7.8
            });

        Assert.Contains(data.FilesByStory["7.8"], f => f.Path == "src/Charts.cs");
        Assert.Contains(data.FilesByStory["7.8"], f => f.Path == "src/EpicsView.cs");
    }

    [Fact]
    public void Build_Tier2_DoesNotReachPastNextMergeBoundary()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[]
            {
                Merge("Merge branch 'worktree-story-7-8-related-files-graph'"),
                Commit("cleanup", "", "src/Inside.cs"),                 // within 7.8's window
                Merge("Merge branch 'worktree-bmad-dev-story-3-8'"),    // boundary → new window (3.8)
                Commit("more cleanup", "", "src/Outside.cs"),           // belongs to 3.8, NOT 7.8
            });

        Assert.Contains(data.FilesByStory["7.8"], f => f.Path == "src/Inside.cs");
        Assert.DoesNotContain(data.FilesByStory["7.8"], f => f.Path == "src/Outside.cs");
        Assert.Contains(data.FilesByStory["3.8"], f => f.Path == "src/Outside.cs");
    }

    [Fact]
    public void Build_Tier2_DoesNotOverrideACommitsOwnTier1Match()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[]
            {
                Merge("Merge branch 'worktree-story-7-8-related-files-graph'"),
                Commit("Story 6.7 landed on this branch", "", "src/Own.cs"),  // keeps its OWN 6.7 match
            });

        Assert.Contains(data.FilesByStory["6.7"], f => f.Path == "src/Own.cs");
        Assert.False(data.FilesByStory.ContainsKey("7.8"),
            "a commit with its own Tier-1 match must not be backfilled to the merge branch");
    }

    [Fact]
    public void Build_Tier2_UnvalidatedMergeBranchBackfillsNothing()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[]
            {
                Merge("Merge branch 'feat/code-review-single-story-id'"), // no valid number
                Commit("something", "", "src/Orphan.cs"),                 // no active ref → unattributed
            });

        Assert.Empty(data.FilesByEpic);
        Assert.Empty(data.FilesByStory);
        Assert.Equal(0, data.AttributedCommitCount);
    }

    // ----- Link gate ---------------------------------------------------------------------------------------

    [Fact]
    public void Build_DropsFilesWithNoCodePage_NeverADeadLink()
    {
        // Resolver knows only about A.cs; B.cs has no code page and must be dropped from the linked set.
        string? Resolver(string path) => path == "src/A.cs" ? "code/src/A.cs.html" : null;

        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("7.11 work", "", "src/A.cs", "src/B.cs") },
            Resolver);

        var files = data.FilesByStory["7.11"];
        Assert.Contains(files, f => f.Path == "src/A.cs" && f.CodePageHref == "code/src/A.cs.html");
        Assert.DoesNotContain(files, f => f.Path == "src/B.cs");
    }

    [Fact]
    public void Build_AttributedCountIndependentOfFileLinkability()
    {
        // The commit self-identifies (attributed) but none of its files are linkable → still counts, no files.
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("7.11 work", "", "src/B.cs") },
            _ => null);

        Assert.Equal(1, data.AttributedCommitCount);
        Assert.False(data.HasAnyFiles);
        Assert.False(data.FilesByStory.ContainsKey("7.11"));
    }

    // ----- Determinism + empty results ---------------------------------------------------------------------

    [Fact]
    public void Build_FilesAreOrdinalSorted()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("7.11 work", "", "src/Zebra.cs", "src/Apple.cs", "src/Mango.cs") });

        var paths = data.FilesByStory["7.11"].Select(f => f.Path).ToList();
        Assert.Equal(new[] { "src/Apple.cs", "src/Mango.cs", "src/Zebra.cs" }, paths);
    }

    [Fact]
    public void Build_EmptyCommits_ReturnsHonestEmpty()
    {
        var data = PlanningCodeImpact.Build(Roster(), Array.Empty<DeepCommit>());
        Assert.Same(PlanningCodeImpactData.Empty, data);
        Assert.False(data.HasAnyFiles);
    }

    [Fact]
    public void Build_NoEpics_ReturnsHonestEmpty()
    {
        var emptyModel = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = Array.Empty<EpicInfo>(),
        };
        var data = PlanningCodeImpact.Build(emptyModel, new[] { Commit("7.11", "", "src/A.cs") });
        Assert.False(data.HasAnyFiles);
    }

    [Fact]
    public void Build_NoMatches_ReturnsEmptyButCountsAnalyzed()
    {
        var data = PlanningCodeImpact.Build(
            Roster(),
            new[] { Commit("Overnight code review", "", "src/A.cs"), Commit("Adjustments", "", "src/B.cs") });

        Assert.False(data.HasAnyFiles);
        Assert.Equal(0, data.AttributedCommitCount);
        Assert.Equal(2, data.TotalAnalyzedCommits);
    }
}
