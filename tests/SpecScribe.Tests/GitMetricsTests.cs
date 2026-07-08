using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Contract coverage for the `git log --pretty=format:%h%x09%ad%x09%an%x09%s --date=format:%Y-%m-%dT%H:%M`
/// parse: the pure helper feeds both the heatmap's daily series and the per-day pages (hash, subject, author,
/// time), and must skip malformed lines rather than fail the whole pulse (GitMetrics is never-throw).</summary>
public class GitMetricsTests
{
    [Fact]
    public void ParseLog_GroupsCommitsByDayInAscendingOrderWithAuthorAndTime()
    {
        // git log emits newest first; the series must still come out ascending.
        var log = "ccc3333\t2026-01-07T09:15\tCarol\tThird change\n" +
                  "bbb2222\t2026-01-05T14:32\tBob\tSecond change\n" +
                  "aaa1111\t2026-01-05T08:01\tAlice\tFirst change\n";

        var (series, commitsByDay) = GitMetrics.ParseLog(log);

        Assert.Equal(new[]
        {
            (new DateOnly(2026, 1, 5), 2),
            (new DateOnly(2026, 1, 7), 1),
        }, series);

        var jan5 = commitsByDay[new DateOnly(2026, 1, 5)];
        Assert.Equal(2, jan5.Count);
        // Within a day, git log order (newest first) is preserved; author + time land on each commit.
        Assert.Equal(new CommitInfo("bbb2222", "Second change", "Bob", "14:32"), jan5[0]);
        Assert.Equal(new CommitInfo("aaa1111", "First change", "Alice", "08:01"), jan5[1]);
        Assert.Equal(new CommitInfo("ccc3333", "Third change", "Carol", "09:15"),
            Assert.Single(commitsByDay[new DateOnly(2026, 1, 7)]));
    }

    [Fact]
    public void ParseLog_SkipsMalformedLinesWithoutThrowing()
    {
        var log = "aaa1111\t2026-01-05T08:01\tAlice\tGood commit\n" +
                  "not-a-real-line\n" +                              // no tabs at all
                  "bbb2222\tnot-a-date\tBob\tBad date\n" +           // unparseable date
                  "\t2026-01-06T10:00\tCarol\tMissing hash\n" +      // empty hash
                  "ddd4444\t2026-01-05T09:00\tDave\n";               // only 3 fields (no subject column)

        var (series, commitsByDay) = GitMetrics.ParseLog(log);

        var only = Assert.Single(series);
        Assert.Equal((new DateOnly(2026, 1, 5), 1), only);
        Assert.Equal("Good commit", Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]).Subject);
    }

    [Fact]
    public void ParseLog_IsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // th-TH defaults to the Buddhist calendar (2569 ≈ Gregorian 2026): a culture-sensitive parse
            // would shift every ISO date git emits by 543 years.
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");

            var (series, commitsByDay) = GitMetrics.ParseLog("aaa1111\t2026-01-05T14:32\tAlice\tChange\n");

            Assert.Equal(new DateOnly(2026, 1, 5), Assert.Single(series).Day);
            Assert.Equal("14:32", Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]).Time);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseLog_LabelsEmptySubjectsAndAuthors()
    {
        // git commit --allow-empty-message yields an empty %s; a missing author name yields an empty %an.
        var (_, commitsByDay) = GitMetrics.ParseLog("aaa1111\t2026-01-05T14:32\t\t\n");

        var commit = Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]);
        Assert.Equal("(no subject)", commit.Subject);
        Assert.Equal("Unknown", commit.Author);
    }

    [Fact]
    public void ParseLog_KeepsTabsInsideSubject()
    {
        // Split is capped at 4 parts, so a subject containing a tab survives intact.
        var log = "aaa1111\t2026-01-05T14:32\tAlice\tsubject\twith tab\n";

        var (_, commitsByDay) = GitMetrics.ParseLog(log);

        Assert.Equal("subject\twith tab", Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]).Subject);
    }

    [Fact]
    public void ParseChangedFiles_RanksByFrequencyDescendingAndTruncatesToTop()
    {
        // `git log --name-only --pretty=format:` emits one file path per line, with a blank line between
        // commits. Three commits touching these files: Program.cs×3, Charts.cs×2, and one each of the rest.
        var log = "Program.cs\nCharts.cs\nA.cs\n\n" +
                  "Program.cs\nCharts.cs\nB.cs\n\n" +
                  "Program.cs\nC.cs\nD.cs\n";

        var top = GitMetrics.ParseChangedFiles(log, top: 3);

        Assert.Equal(3, top.Count);
        Assert.Equal(("Program.cs", 3), top[0]);
        Assert.Equal(("Charts.cs", 2), top[1]);
        // A/B/C/D all tie at 1; the ordinal tie-break keeps the ranking deterministic (A.cs first).
        Assert.Equal(("A.cs", 1), top[2]);
    }

    [Fact]
    public void ParseChangedFiles_SkipsBlankLinesAndTrimsCarriageReturnsWithoutThrowing()
    {
        // Blank separator lines and a stray Windows \r must not become phantom "" / trailing-CR file names.
        var log = "src/Foo.cs\r\n\r\nsrc/Foo.cs\r\n   \n";

        var files = GitMetrics.ParseChangedFiles(log);

        var only = Assert.Single(files);
        Assert.Equal(("src/Foo.cs", 2), only);
    }

    [Fact]
    public void ParseChangedFiles_ReturnsEmptyForNoFileChanges()
    {
        // A window of merge-only / empty commits yields no name-only lines at all.
        Assert.Empty(GitMetrics.ParseChangedFiles("\n\n   \n"));
    }

    [Theory]
    [InlineData(30, 4)]  // exactly 30 days ago is inside the window
    [InlineData(31, 2)]  // 31 days ago falls outside; only the 15-days-ago and today commits remain
    public void CountCommitsInLastDays_IncludesBoundaryDayExcludesOlder(int oldestOffset, int expected)
    {
        var today = new DateOnly(2026, 2, 1);
        var series = new (DateOnly Day, int Count)[]
        {
            (today.AddDays(-oldestOffset), 2), // the boundary commit under test
            (today.AddDays(-15), 1),
            (today, 1),
        };

        Assert.Equal(expected, GitMetrics.CountCommitsInLastDays(series, today, 30));
    }

    [Fact]
    public void CountCommitsInLastDays_ExcludesFutureDatedCommits()
    {
        // Clock/timezone skew can produce a commit dated after "today"; it must not inflate the rolling count.
        var today = new DateOnly(2026, 2, 1);
        var series = new (DateOnly Day, int Count)[]
        {
            (today.AddDays(2), 5), // future-dated — ignored
            (today, 3),
        };

        Assert.Equal(3, GitMetrics.CountCommitsInLastDays(series, today, 30));
    }
}
