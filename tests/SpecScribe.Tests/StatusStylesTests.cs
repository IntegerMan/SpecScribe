using SpecScribe;

namespace SpecScribe.Tests;

public class StatusStylesTests
{
    private static StoryInfo Story(string? status) => new()
    {
        Id = "1.1",
        EpicNumber = 1,
        Title = "A story",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
    };

    private static EpicInfo Epic(EpicStatus status, params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "An epic",
        GoalHtml = string.Empty,
        Status = status,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    [Theory]
    [InlineData("done", "done")]
    [InlineData("Complete", "done")]
    [InlineData("ready-for-review", "review")]
    [InlineData("in progress", "active")]
    [InlineData("in-dev", "active")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("something else", "drafted")]
    [InlineData(null, "drafted")]
    public void ForStory_MapsStatusKeywords(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForStory(Story(status)));

    [Fact]
    public void ForEpic_PendingOrStorylessEpicsArePending()
    {
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Pending, Story("done"))));
        Assert.Equal("pending", StatusStyles.ForEpic(Epic(EpicStatus.Drafted)));
    }

    [Fact]
    public void ForEpic_DoneOnlyWhenEveryStoryIsDone()
    {
        Assert.Equal("done", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"), Story("complete"))));
        Assert.Equal("active", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"), Story("ready-for-dev"))));
    }

    [Fact]
    public void ForEpic_ReadyWhenAnyStoryIsReadyAndNoneFurther()
    {
        // Any ready-for-dev story (with none in dev/review/done) lifts the epic to the ready tier, mirroring
        // the "any active → active" rule. [spec-sunburst-epic-focus-and-ready-rollup]
        Assert.Equal("ready", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null), Story("ready-for-dev"))));
        Assert.Equal("ready", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("ready-for-dev"), Story("ready-for-dev"))));
    }

    [Fact]
    public void ForEpic_DraftedOnlyWhenNoStoryIsReadyOrFurther()
        => Assert.Equal("drafted", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null), Story("something else"))));

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("active", "In development")]
    [InlineData("ready", "Ready for dev")]
    [InlineData("drafted", "Stories drafted")]
    [InlineData("pending", "Pending")]
    public void EpicLabel_MapsEachTier(string cssClass, string expected)
        => Assert.Equal(expected, StatusStyles.EpicLabel(cssClass));

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("review", "In review")]
    [InlineData("active", "In development")]
    [InlineData("ready", "Ready for dev")]
    [InlineData("drafted", "Drafted")]
    [InlineData("pending", "Pending")]
    public void StoryLabel_MapsEachStage(string cssClass, string expected)
        => Assert.Equal(expected, StatusStyles.StoryLabel(cssClass));

    [Theory]
    [InlineData("done", "done")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData(null, "drafted")]
    public void ForStatus_MapsRawStatusText(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForStatus(status));
}
