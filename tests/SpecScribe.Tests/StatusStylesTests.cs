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

    [Fact]
    public void EpicStages_CoversEveryForEpicOutputAndEachHasALabel()
    {
        // Representative epics exercising each reachable ForEpic branch. EpicStages is the single list the Epic
        // Status donut iterates, so binding ForEpic's real outputs to it (both directions) guarantees a class
        // can never silently drop from the donut, nor an EpicStages member be dead. [heatmap-debt-triage]
        var outputs = new[]
        {
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("done"))),          // done
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("in progress"))),   // active
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story("ready-for-dev"))), // ready
            StatusStyles.ForEpic(Epic(EpicStatus.Drafted, Story(null))),            // drafted
            StatusStyles.ForEpic(Epic(EpicStatus.Pending, Story("done"))),          // pending
        };

        Assert.All(outputs, o => Assert.Contains(o, StatusStyles.EpicStages));
        Assert.Equal(StatusStyles.EpicStages.OrderBy(s => s), outputs.Distinct().OrderBy(s => s));
        // Each stage maps to its OWN non-empty label. Distinctness is the real guard: a stage added to
        // EpicStages but missing from EpicLabel's switch would fall through to the `_ => "Pending"` default
        // and collide with the genuine "pending" label — a plain non-empty check could never catch that.
        var labels = StatusStyles.EpicStages.Select(StatusStyles.EpicLabel).ToList();
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

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

    [Theory]
    // development_status lifecycle onto the shared six-stage vocabulary. [Story 2.3 Task 2]
    [InlineData("done", "done")]
    [InlineData("review", "review")]
    [InlineData("in-progress", "active")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("backlog", "pending")]
    // retrospective + action-item statuses ride the same colors.
    [InlineData("optional", "pending")]
    [InlineData("open", "ready")]
    // unknown/forward-compat + empty → pending, never an invented color.
    [InlineData("blocked", "pending")]
    [InlineData("", "pending")]
    [InlineData(null, "pending")]
    public void ForSprint_MapsLifecycleOntoSharedColors(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForSprint(status));

    [Theory]
    [InlineData("done", "Done")]
    [InlineData("review", "In review")]
    [InlineData("in-progress", "In progress")]
    [InlineData("ready-for-dev", "Ready for dev")]
    [InlineData("backlog", "Backlog")]
    [InlineData("optional", "Optional")]
    [InlineData("open", "Open")]
    // forward-compat value still reads as a real word (title-cased), never a raw token.
    [InlineData("blocked", "Blocked")]
    public void SprintLabel_MapsEachLifecycleValueToAWord(string status, string expected)
        => Assert.Equal(expected, StatusStyles.SprintLabel(status));

    [Theory]
    // A planning doc's own free-text frontmatter status onto the shared six-stage colors. [Story 2.4 Task 1]
    [InlineData("final", "done")]
    [InlineData("approved", "done")]
    [InlineData("published", "done")]
    [InlineData("review", "review")]
    [InlineData("in-progress", "active")]
    [InlineData("wip", "active")]
    [InlineData("ready", "ready")]
    [InlineData("ready-for-dev", "ready")]
    [InlineData("draft", "drafted")]
    [InlineData("proposed", "drafted")]
    // empty/null/unknown → parchment pending, never an invented color.
    [InlineData("something-new", "pending")]
    [InlineData("", "pending")]
    [InlineData(null, "pending")]
    public void ForDoc_MapsDocStatusOntoSharedColors(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.ForDoc(status));

    [Theory]
    // The label is the doc's own word, title-cased — truthful to what the document declares, not remapped to a
    // lifecycle noun (Story 1.5). [Story 2.4 Task 1]
    [InlineData("final", "Final")]
    [InlineData("draft", "Draft")]
    [InlineData("ready", "Ready")]
    [InlineData("ready-for-dev", "Ready For Dev")]
    [InlineData("", "Pending")]
    [InlineData(null, "Pending")]
    public void DocLabel_IsTheHumanCasedSourceWord(string? status, string expected)
        => Assert.Equal(expected, StatusStyles.DocLabel(status));

    // ---- Story 2.5: status icon anchored to this one seam --------------------------------------

    [Theory]
    [InlineData("done")]
    [InlineData("active")]
    [InlineData("review")]
    [InlineData("ready")]
    [InlineData("drafted")]
    [InlineData("pending")]
    [InlineData("deferred")]
    public void Icon_ReturnsAGlyphForEveryKnownCssClass(string cssClass)
        => Assert.False(string.IsNullOrEmpty(StatusStyles.Icon(cssClass)));

    [Fact]
    public void Icon_UnknownCssClassReturnsEmpty()
        => Assert.Equal(string.Empty, StatusStyles.Icon("not-a-real-status"));

    [Fact]
    public void Badge_RendersIconAndTextInsideTheStatusBadgeSpan()
    {
        var badge = StatusStyles.Badge("done", "Done");
        Assert.Contains("class=\"status-badge done\"", badge);
        Assert.Contains("aria-hidden=\"true\"", badge); // the icon is decorative
        Assert.Contains("Done", badge); // the word always stays (UX-DR17: never icon-only)
    }
}
