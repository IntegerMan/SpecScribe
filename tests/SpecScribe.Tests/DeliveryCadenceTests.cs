using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 21.2 <see cref="DeliveryCadence"/> data builder — pure over a constructed
/// <see cref="EpicsModel"/> plus an injectable first-touch resolver, so it needs no repo or disk (the
/// <c>GitMetrics.ParseNumstatLog</c> testing style). Guards the honesty rules: only done stories with a resolvable
/// done-date land on the cadence; cycle-time is skipped (never fabricated / never negative-clamped) when
/// first-touch fails or the span goes negative; a project with no done stories is a normal empty result.</summary>
public class DeliveryCadenceTests
{
    private static StoryInfo Story(string id, string? status, DateOnly? lastUpdated, int epicNumber = 1) => new()
    {
        Id = id,
        EpicNumber = epicNumber,
        Title = $"Story {id}",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
        LastUpdatedDate = lastUpdated,
        ArtifactSourcePath = $"implementation-artifacts/{id.Replace('.', '-')}.md",
    };

    private static EpicsModel Model(params StoryInfo[] stories) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "Epic 1",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            },
        },
    };

    [Fact]
    public void Build_FiltersToDoneStoriesWithAResolvableDate()
    {
        var model = Model(
            Story("1.1", "done", new DateOnly(2026, 7, 10)),
            Story("1.2", "review", new DateOnly(2026, 7, 11)),   // not done → excluded
            Story("1.3", "ready-for-dev", new DateOnly(2026, 7, 12))); // not done → excluded

        var data = DeliveryCadence.Build(model);

        var day = Assert.Single(data.CompletionSeries);
        Assert.Equal(new DateOnly(2026, 7, 10), day.Day);
        Assert.Equal(1, day.Count);
        Assert.Equal(1, data.TotalCompletions);
    }

    [Fact]
    public void Build_ExcludesDoneStoryWithNullDate_NeverFabricates()
    {
        var model = Model(
            Story("1.1", "done", new DateOnly(2026, 7, 10)),
            Story("1.2", "done", lastUpdated: null)); // done but no derivable date → cannot place, excluded

        var data = DeliveryCadence.Build(model);

        Assert.Single(data.CompletionSeries);
        Assert.Equal(1, data.TotalCompletions);
    }

    [Fact]
    public void Build_GroupsMultipleSameDayCompletions_AscendingSeries()
    {
        var model = Model(
            Story("1.2", "done", new DateOnly(2026, 7, 12)),
            Story("1.1", "done", new DateOnly(2026, 7, 10)),
            Story("1.3", "completed", new DateOnly(2026, 7, 10))); // synonym for done

        var data = DeliveryCadence.Build(model);

        Assert.Equal(2, data.CompletionSeries.Count);
        // Ascending by day.
        Assert.Equal(new DateOnly(2026, 7, 10), data.CompletionSeries[0].Day);
        Assert.Equal(2, data.CompletionSeries[0].Count);
        Assert.Equal(new DateOnly(2026, 7, 12), data.CompletionSeries[1].Day);
        // Same-day list carries both, deterministically ordered.
        var sameDay = data.CompletionsByDay[new DateOnly(2026, 7, 10)];
        Assert.Equal(new[] { "1.1", "1.3" }, sameDay.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void Build_CycleTime_ComputesDoneMinusFirstTouch_ForResolvableStories()
    {
        var model = Model(Story("1.1", "done", new DateOnly(2026, 7, 21)));
        var data = DeliveryCadence.Build(model, _ => new DateOnly(2026, 7, 19)); // 2-day span

        var entry = Assert.Single(data.CycleTimes);
        Assert.Equal("1.1", entry.StoryId);
        Assert.Equal(2, entry.Days);
    }

    [Fact]
    public void Build_CycleTime_SkipsStoryWhereFirstTouchUnresolved()
    {
        var model = Model(
            Story("1.1", "done", new DateOnly(2026, 7, 21)),
            Story("1.2", "done", new DateOnly(2026, 7, 21)));

        // First-touch resolves only for 1.1.
        var data = DeliveryCadence.Build(model, s => s.Id == "1.1" ? new DateOnly(2026, 7, 20) : (DateOnly?)null);

        var entry = Assert.Single(data.CycleTimes);
        Assert.Equal("1.1", entry.StoryId);
    }

    [Fact]
    public void Build_CycleTime_SkipsNegativeSpan_NeverClampsToZero()
    {
        // A clearly-bad first-touch AFTER the done-date → negative span → skipped entirely, not clamped to 0.
        var model = Model(Story("1.1", "done", new DateOnly(2026, 7, 10)));
        var data = DeliveryCadence.Build(model, _ => new DateOnly(2026, 7, 20));

        Assert.Empty(data.CycleTimes);
    }

    [Fact]
    public void Build_CycleTime_KeepsZeroDaySameDaySpan()
    {
        var model = Model(Story("1.1", "done", new DateOnly(2026, 7, 10)));
        var data = DeliveryCadence.Build(model, _ => new DateOnly(2026, 7, 10));

        var entry = Assert.Single(data.CycleTimes);
        Assert.Equal(0, entry.Days);
    }

    [Fact]
    public void Build_WithoutFirstTouchResolver_YieldsCadenceButNoCycleTimes()
    {
        var model = Model(Story("1.1", "done", new DateOnly(2026, 7, 10)));
        var data = DeliveryCadence.Build(model);

        Assert.Single(data.CompletionSeries);
        Assert.Empty(data.CycleTimes);
    }

    [Fact]
    public void Build_NoDoneStories_ReturnsHonestEmptyResult_NeverThrows()
    {
        var model = Model(Story("1.1", "ready-for-dev", new DateOnly(2026, 7, 10)));
        var data = DeliveryCadence.Build(model, _ => new DateOnly(2026, 7, 1));

        Assert.True(data.IsEmpty);
        Assert.Empty(data.CompletionSeries);
        Assert.Empty(data.CycleTimes);
        Assert.Equal(0, data.TotalCompletions);
    }

    [Fact]
    public void Build_NullEpics_ReturnsEmpty()
    {
        var data = DeliveryCadence.Build(null, _ => new DateOnly(2026, 7, 1));
        Assert.True(data.IsEmpty);
    }
}
