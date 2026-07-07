using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Contract coverage for the `git log --pretty=format:%h%x09%ad%x09%s --date=short` parse:
/// the pure helper feeds both the heatmap's daily series and the per-day drill-down lists, and must
/// skip malformed lines rather than fail the whole pulse (GitMetrics is never-throw).</summary>
public class GitMetricsTests
{
    [Fact]
    public void ParseLog_GroupsCommitsByDayInAscendingOrder()
    {
        // git log emits newest first; the series must still come out ascending.
        var log = "ccc3333\t2026-01-07\tThird change\n" +
                  "bbb2222\t2026-01-05\tSecond change\n" +
                  "aaa1111\t2026-01-05\tFirst change\n";

        var (series, commitsByDay) = GitMetrics.ParseLog(log);

        Assert.Equal(new[]
        {
            (new DateOnly(2026, 1, 5), 2),
            (new DateOnly(2026, 1, 7), 1),
        }, series);

        var jan5 = commitsByDay[new DateOnly(2026, 1, 5)];
        Assert.Equal(2, jan5.Count);
        // Within a day, git log order (newest first) is preserved for the drill-down list.
        Assert.Equal(new CommitInfo("bbb2222", "Second change"), jan5[0]);
        Assert.Equal(new CommitInfo("aaa1111", "First change"), jan5[1]);
        Assert.Equal(new CommitInfo("ccc3333", "Third change"),
            Assert.Single(commitsByDay[new DateOnly(2026, 1, 7)]));
    }

    [Fact]
    public void ParseLog_SkipsMalformedLinesWithoutThrowing()
    {
        var log = "aaa1111\t2026-01-05\tGood commit\n" +
                  "not-a-real-line\n" +                       // no tabs at all
                  "bbb2222\tnot-a-date\tBad date\n" +         // unparseable date
                  "\t2026-01-06\tMissing hash\n";             // empty hash

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

            var (series, _) = GitMetrics.ParseLog("aaa1111\t2026-01-05\tChange\n");

            Assert.Equal(new DateOnly(2026, 1, 5), Assert.Single(series).Day);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseLog_LabelsEmptySubjects()
    {
        // git commit --allow-empty-message yields an empty %s; the panel row shouldn't render a bare hash.
        var (_, commitsByDay) = GitMetrics.ParseLog("aaa1111\t2026-01-05\t\n");

        Assert.Equal("(no subject)", Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]).Subject);
    }

    [Fact]
    public void ParseLog_KeepsTabsInsideSubject()
    {
        // Split is capped at 3 parts, so a subject containing a tab survives intact.
        var log = "aaa1111\t2026-01-05\tsubject\twith tab\n";

        var (_, commitsByDay) = GitMetrics.ParseLog(log);

        Assert.Equal("subject\twith tab", Assert.Single(commitsByDay[new DateOnly(2026, 1, 5)]).Subject);
    }
}
