using SpecScribe;

namespace SpecScribe.Tests;

public class TaskListParserTests
{
    private const string Sample = """
        # Story 1.1

        ## Tasks / Subtasks

        - [x] **Set up the project** (AC: #1)
          - [x] Create the solution
          - [ ] Add CI
        - [ ] Write the docs
          - not a checkbox, just a note
        - [X] Uppercase done marker

        ## Dev Notes

        - [ ] This checkbox is outside the Tasks section and must be ignored
        """;

    [Fact]
    public void Parse_BuildsTwoLevelTree()
    {
        var tasks = TaskListParser.Parse(Sample);

        Assert.Equal(3, tasks.Count);
        Assert.Equal("Set up the project", tasks[0].Text); // bold markers and (AC: ...) stripped
        Assert.True(tasks[0].Done);
        Assert.Equal(2, tasks[0].Subtasks.Count);
        Assert.True(tasks[0].Subtasks[0].Done);
        Assert.False(tasks[0].Subtasks[1].Done);
    }

    [Fact]
    public void Parse_SkipsPlainBulletsAndStopsAtNextHeading()
    {
        var tasks = TaskListParser.Parse(Sample);

        Assert.Empty(tasks[1].Subtasks); // the plain "just a note" bullet doesn't count
        Assert.DoesNotContain(tasks, t => t.Text.Contains("outside the Tasks section"));
    }

    [Fact]
    public void Parse_TreatsUppercaseXAsDone()
        => Assert.True(TaskListParser.Parse(Sample)[2].Done);

    [Fact]
    public void Parse_ReturnsEmptyWithoutTasksHeading()
        => Assert.Empty(TaskListParser.Parse("# Story\n\n- [x] a checkbox with no Tasks section"));

    [Fact]
    public void Parse_ReturnsEmptyForEmptyInput()
        => Assert.Empty(TaskListParser.Parse(string.Empty));
}
