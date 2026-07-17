using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pure-helper coverage for Story 9.12 Unplanned membership (open quick-dev + unattributable deferred).</summary>
public class UnplannedWorkGeometryTests
{
    private static EpicsModel OneEpic() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "Foundation",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = new[]
                {
                    new StoryInfo
                    {
                        Id = "1.2",
                        EpicNumber = 1,
                        Title = "Auth",
                        UserStoryHtml = string.Empty,
                        AcBlocksHtml = Array.Empty<string>(),
                    },
                },
            },
        },
    };

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("ready", true)]
    [InlineData("in-progress", true)]
    [InlineData("done", false)]
    [InlineData("DONE", false)]
    [InlineData("resolved", false)]
    [InlineData("Resolved", false)]
    public void IsOpenQuickDev_FiltersDoneAndResolved(string? status, bool expected) =>
        Assert.Equal(expected, UnplannedWorkGeometry.IsOpenQuickDev(status));

    [Fact]
    public void From_SplitsAttributedQuickDevFromUnplannedSet()
    {
        var epics = OneEpic();
        var deferredMarkdown = """
            ## Deferred from: cross-cutting (2026-07-15)

            - Unattributed park.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry("Story 1.2 polish", "spec-polish.html", null, null),
                new QuickDevEntry("Random one-shot", "spec-random.html", "ready", null),
                new QuickDevEntry("Finished", "spec-done.html", "done", null),
            },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var followUps = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1, DirectChanges = 3 },
            work,
            deferredModel: deferredModel,
            epics: epics);

        var unplanned = UnplannedWorkGeometry.From(work, followUps, epics);

        Assert.Single(unplanned.ForEpic(1));
        Assert.Equal("Story 1.2 polish", unplanned.ForEpic(1)[0].Entry.Title);
        Assert.Single(unplanned.UnplannedQuickDev);
        Assert.Equal("Random one-shot", unplanned.UnplannedQuickDev[0].Entry.Title);
        Assert.Single(unplanned.UnattributableDeferred);
        Assert.Equal(2, unplanned.UnplannedSet.Count);
        Assert.Contains(unplanned.UnplannedSet, m => m.Kind == "direct" && m.Title == "Random one-shot");
        Assert.Contains(unplanned.UnplannedSet, m => m.Kind == "deferred");
        // Ledger DirectChanges = all quick-dev statuses; geometry open subset is smaller.
        Assert.Equal(3, work.QuickDev.Count);
        Assert.Equal(2, work.QuickDev.Count(q => UnplannedWorkGeometry.IsOpenQuickDev(q.Status)));
        Assert.Equal(1, followUps.DeferredOpenCount);
    }

    [Fact]
    public void From_EmptyWhenNoOpenMembers()
    {
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Done", "x.html", "resolved", null) },
            Deferred = null,
        };
        var unplanned = UnplannedWorkGeometry.From(work, FollowUpGeometry.Empty, OneEpic());
        Assert.False(unplanned.HasUnplanned);
        Assert.Null(unplanned.GroupRootHref);
        Assert.Empty(unplanned.UnplannedSet);
    }

    [Fact]
    public void GroupRootHref_PointsAtFilteredUnplannedGroupPage()
    {
        var deferredMarkdown = """
            ## Deferred from: misc (2026-07-15)

            - Only deferred.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var followUps = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1 },
            work,
            deferredModel: deferredModel);
        var unplanned = UnplannedWorkGeometry.From(work, followUps, null);

        Assert.Equal(FollowUpGroupPages.UnplannedPath, unplanned.GroupRootHref);
        Assert.NotEqual("deferred-work.html", unplanned.GroupRootHref);
        Assert.NotEqual(SiteNav.ActionItemsOutputPath, unplanned.GroupRootHref);
    }

    [Fact]
    public void GroupRootHref_AppliesLinkPrefix()
    {
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("One-shot", "spec.html", "ready", null) },
            Deferred = null,
        };
        var unplanned = UnplannedWorkGeometry.From(work, FollowUpGeometry.Empty, null, linkPrefix: "../");
        Assert.Equal("../" + FollowUpGroupPages.UnplannedPath, unplanned.GroupRootHref);
    }

    [Fact]
    public void ResolveQuickDevEpic_FromEpicMentionInTitle()
    {
        var entry = new QuickDevEntry("Epic 1 cleanup", "spec-cleanup.html", null, null);
        Assert.Equal(1, UnplannedWorkGeometry.ResolveQuickDevEpic(entry, OneEpic()));
    }

    [Fact]
    public void From_CodeReviewOfSpec_AttributesDeferredToParentQuickDevEpic_WhenResolvable()
    {
        var epics = OneEpic();
        var deferredMarkdown = """
            ## Deferred from: code review of spec-auth-polish (2026-07-06)

            - Residual review item after the one-shot.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        Assert.Equal("spec-auth-polish", deferredModel.Groups[0].SourceKey);
        Assert.Null(deferredModel.Groups[0].SourceStoryId);

        var work = new WorkInventory
        {
            // Title mentions Story 1.2 so the quick-dev (and its deferred children) land under Epic 1.
            QuickDev = new[]
            {
                new QuickDevEntry("Story 1.2 auth polish", "implementation-artifacts/spec-auth-polish.html", "done", "chore"),
            },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var followUps = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1, DirectChanges = 1 },
            work,
            deferredModel: deferredModel,
            epics: epics);

        Assert.Empty(followUps.UnattributedDeferredItems);
        Assert.Single(followUps.DeferredForEpicNumber(1));
        Assert.Equal("spec-auth-polish", followUps.DeferredForEpicNumber(1)[0].SourceKey);

        var unplanned = UnplannedWorkGeometry.From(work, followUps, epics);
        Assert.False(unplanned.HasUnplanned);
        Assert.Empty(unplanned.UnplannedSet);
    }

    [Fact]
    public void From_CodeReviewOfSpec_KeepsParentAndChildTogetherUnderUnplanned_WhenNoEpic()
    {
        var deferredMarkdown = """
            ## Deferred from: code review of spec-home-next-steps-label-and-code-review (2026-07-06)

            - Retrospective fallback can mislabel a project that has review work.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        var work = new WorkInventory
        {
            QuickDev = new[]
            {
                new QuickDevEntry(
                    "Home next steps label",
                    "implementation-artifacts/spec-home-next-steps-label-and-code-review.html",
                    "done",
                    "chore"),
            },
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var followUps = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1, DirectChanges = 1 },
            work,
            deferredModel: deferredModel,
            epics: OneEpic());

        Assert.Single(followUps.UnattributedDeferredItems);
        Assert.Equal(
            "spec-home-next-steps-label-and-code-review",
            followUps.UnattributedDeferredItems[0].SourceKey);

        var unplanned = UnplannedWorkGeometry.From(work, followUps, OneEpic());
        // Done parent resurfaces because open deferred still stems from its review.
        Assert.Single(unplanned.UnplannedQuickDev);
        Assert.Equal("done", unplanned.UnplannedQuickDev[0].Entry.Status);
        Assert.Single(unplanned.UnattributableDeferred);
        Assert.Contains(unplanned.UnplannedSet, m => m.Kind == "direct");
        Assert.Contains(unplanned.UnplannedSet, m =>
            m.Kind == "deferred" && m.SourceKey == "spec-home-next-steps-label-and-code-review");

        var svg = Charts.Sunburst(
            OneEpic(),
            followUps: followUps,
            unplanned: unplanned);
        Assert.Contains("from Direct change: spec-home-next-steps-label-and-code-review", svg);
        Assert.Contains("aria-label=\"Direct change (done): Home next steps label\"", svg);
    }

    [Fact]
    public void From_CodeReviewOfStoryKey_StaysUnderEpic_NotUnplanned()
    {
        var epics = OneEpic();
        var deferredMarkdown = """
            ## Deferred from: code review of 1-2-auth.md (2026-07-06)

            - Story-keyed residual should not land in Unplanned.
            """;
        var deferredModel = DeferredWorkParser.Parse(deferredMarkdown);
        Assert.Equal("1.2", deferredModel.Groups[0].SourceStoryId);

        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred work", "deferred-work.html", 1),
        };
        var followUps = FollowUpGeometry.From(
            Array.Empty<SprintActionItem>(),
            ProjectCounts.Empty with { DeferredOpenItems = 1 },
            work,
            deferredModel: deferredModel,
            epics: epics);

        Assert.Empty(followUps.UnattributedDeferredItems);
        Assert.Single(followUps.DeferredForEpicNumber(1));

        var unplanned = UnplannedWorkGeometry.From(work, followUps, epics);
        Assert.False(unplanned.HasUnplanned);
    }
}
