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
    public void ForEpic_DraftedWhenNoStoryHasStartedDev()
        => Assert.Equal("drafted", StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null), Story("ready-for-dev"))));
}
