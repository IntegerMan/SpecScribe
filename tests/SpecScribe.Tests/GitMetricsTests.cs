using System.Globalization;
using System.Linq;
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

    // ---- ParseNumstatLog (deep git analytics: hotspots + coupling) [Story 3.2] ----

    private const string Sentinel = "\u0001";

    // Builds `git log --numstat --pretty=format:%x01%H` shaped text: each commit is a \x01 sentinel header line
    // followed by "added\tdeleted\tpath" numstat lines. The counts are irrelevant to this parser (it uses the
    // file-set only), so a fixed "1\t0" prefix stands in for them.
    private static string Numstat(params string[][] commits)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < commits.Length; i++)
        {
            sb.Append(Sentinel).Append("hash").Append(i).Append('\n');
            foreach (var path in commits[i])
            {
                sb.Append("1\t0\t").Append(path).Append('\n');
            }
        }
        return sb.ToString();
    }

    [Fact]
    public void ParseNumstatLog_RanksHotspotsByChangeFrequencyDescendingAndTruncatesToTopN()
    {
        // A.cs in all three commits, B.cs and C.cs in one each.
        var log = Numstat(
            new[] { "A.cs", "B.cs" },
            new[] { "A.cs", "C.cs" },
            new[] { "A.cs" });

        var deep = GitMetrics.ParseNumstatLog(log, topHotspots: 2);

        Assert.Equal(2, deep.Hotspots.Count);
        Assert.Equal(("A.cs", 3), deep.Hotspots[0]);
        // B and C tie at 1; the ordinal path tie-break keeps it deterministic (B before C).
        Assert.Equal(("B.cs", 1), deep.Hotspots[1]);
    }

    [Fact]
    public void ParseNumstatLog_CountsCoupledPairsKeepsOnlyThoseAtOrAboveTwoAndTruncates()
    {
        // (A,B) co-change in commits 1+2, (A,C) in commits 1+3, (B,C) only in commit 1.
        var log = Numstat(
            new[] { "A.cs", "B.cs", "C.cs" },
            new[] { "A.cs", "B.cs" },
            new[] { "A.cs", "C.cs" });

        var deep = GitMetrics.ParseNumstatLog(log);

        // (B,C) sits at 1 and is dropped by the >= 2 threshold; (A,B) and (A,C) both survive at 2.
        Assert.Equal(2, deep.Coupling.Count);
        Assert.Equal(("A.cs", "B.cs", 2), deep.Coupling[0]);
        Assert.Equal(("A.cs", "C.cs", 2), deep.Coupling[1]);
    }

    [Fact]
    public void ParseNumstatLog_PairsAreUnorderedRegardlessOfWithinCommitFileOrder()
    {
        // Same pair, opposite file order across two commits — must be counted as one canonical pair, not two.
        var log = Numstat(
            new[] { "Z.cs", "A.cs" },
            new[] { "A.cs", "Z.cs" });

        var deep = GitMetrics.ParseNumstatLog(log);

        var pair = Assert.Single(deep.Coupling);
        Assert.Equal(("A.cs", "Z.cs", 2), pair); // canonicalized to ordinal order
    }

    [Fact]
    public void ParseNumstatLog_SkipsOversizedCommitsForCouplingButStillCountsThemAsHotspots()
    {
        // One bulk commit touching 60 files (> the 50-file cap) plus two small commits that form a real pair.
        var bulk = Enumerable.Range(0, 60).Select(i => $"bulk/File{i:00}.cs").ToArray();
        var log = Numstat(
            bulk,
            new[] { "A.cs", "B.cs" },
            new[] { "A.cs", "B.cs" });

        var deep = GitMetrics.ParseNumstatLog(log, topHotspots: 100);

        // The 60-file commit would generate 1770 pairs if not skipped; the only surviving coupling is the real
        // (A,B) pair from the two small commits — proving the O(n²) guard fired.
        var pair = Assert.Single(deep.Coupling);
        Assert.Equal(("A.cs", "B.cs", 2), pair);
        // ...yet the bulk commit's files still count toward hotspot frequency (the cap is coupling-only).
        Assert.Contains(("bulk/File00.cs", 1), deep.Hotspots);
    }

    [Fact]
    public void ParseNumstatLog_SkipsMalformedAndBlankLinesWithoutThrowing()
    {
        // A well-formed commit interleaved with junk: a line with no tabs, an empty-path numstat line, a blank
        // line, and a binary-file marker ("-\t-\tpath"), which is a legitimate numstat row and must be kept.
        var log =
            Sentinel + "hash0\n" +
            "1\t0\tA.cs\n" +
            "garbage-with-no-tabs\n" +
            "\t\t\n" +               // empty path -> skipped
            "\n" +                    // blank -> skipped
            "-\t-\tBin.dll\n";        // binary file -> path still taken

        var deep = GitMetrics.ParseNumstatLog(log, topHotspots: 10);

        Assert.Contains(("A.cs", 1), deep.Hotspots);
        Assert.Contains(("Bin.dll", 1), deep.Hotspots);
        Assert.Equal(2, deep.Hotspots.Count); // the junk lines produced no phantom entries
    }

    [Fact]
    public void ParseNumstatLog_ReturnsEmptyListsWhenNoFilesChanged()
    {
        // Merge-only / empty commits: sentinels with no numstat rows, and the fully empty case.
        var log = Sentinel + "hash0\n" + Sentinel + "hash1\n";

        var deep = GitMetrics.ParseNumstatLog(log);
        Assert.Empty(deep.Hotspots);
        Assert.Empty(deep.Coupling);

        var empty = GitMetrics.ParseNumstatLog(string.Empty);
        Assert.Empty(empty.Hotspots);
        Assert.Empty(empty.Coupling);
    }

    [Fact]
    public void ParseNumstatLog_CollapsesFullPathRenameToTheNewPath()
    {
        // git's numstat rename form with no shared prefix/suffix: "old => new" as one tab-delimited field.
        var log = Sentinel + "hash0\n" + "0\t0\told/Name.cs => new/Renamed.cs\n";

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.Contains(("new/Renamed.cs", 1), deep.Hotspots);
        Assert.DoesNotContain(deep.Hotspots, h => h.Path.Contains("=>"));
    }

    [Fact]
    public void ParseNumstatLog_CollapsesBraceAbbreviatedRenameToTheNewPath()
    {
        // git's common-prefix/suffix abbreviated rename form: "src/{old.css => new.css}".
        var log = Sentinel + "hash0\n" + "0\t0\tsrc/{old.css => new.css}\n";

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.Contains(("src/new.css", 1), deep.Hotspots);
        Assert.DoesNotContain(deep.Hotspots, h => h.Path.Contains("{") || h.Path.Contains("=>"));
    }

    [Fact]
    public void ParseNumstatLog_RenamedPathsCoupleCorrectlyWithSiblingChanges()
    {
        // A rename alongside a normal edit in the same commit must pair on the *resolved* new path, not the
        // raw arrow text, so coupling reflects real files.
        var log = Sentinel + "hash0\n" +
            "0\t0\told/Name.cs => new/Renamed.cs\n" +
            "1\t1\tOther.cs\n" +
            Sentinel + "hash1\n" +
            "0\t0\tnew/Renamed.cs\n" +
            "1\t1\tOther.cs\n";

        var deep = GitMetrics.ParseNumstatLog(log);

        // Canonical unordered key is ordinal-sorted; "Other.cs" < "new/Renamed.cs" ordinally ('O' < 'n').
        Assert.Contains(("Other.cs", "new/Renamed.cs", 2), deep.Coupling);
    }

    // ---- ParseNumstatRecords + BuildInsights (Git Insights hub aggregates) [Story 3.8] ----

    /// <summary>The \x1f header-field separator of the enriched deep fetch, built from its code point so no
    /// invisible control character has to live in this source file.</summary>
    private static readonly string FS = ((char)0x1f).ToString();

    /// <summary>Builds one enriched-format record: sentinel + hash/author/date/subject/body header (\x1f
    /// separated, trailing \x1f closing the body) followed by numstat rows — the exact shape
    /// `--pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f` emits.</summary>
    private static string Rec(string hash, string author, string date, string subject, string body, params string[] numstatLines)
        => Sentinel + hash + FS + author + FS + date + FS + subject + FS + body + FS + "\n" +
           string.Concat(numstatLines.Select(l => l + "\n"));

    [Fact]
    public void ParseNumstatRecords_ParsesEnrichedHeaderFieldsAndNumstatRows()
    {
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Fix the thing", "Longer explanation.",
            "3\t1\tsrc/A.cs",
            "10\t0\tsrc/B.cs");

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        Assert.Equal("abc123", commit.Hash);
        Assert.Equal("Alice", commit.Author);
        Assert.Equal(new DateTime(2026, 7, 1, 9, 15, 0), commit.Timestamp);
        Assert.Equal("Fix the thing", commit.Subject);
        Assert.Equal("Longer explanation.", commit.Body);
        Assert.Equal(2, commit.Files.Count);
        Assert.Equal(new DeepFileChange("src/A.cs", 3, 1), commit.Files[0]);
        Assert.Equal(new DeepFileChange("src/B.cs", 10, 0), commit.Files[1]);
    }

    [Fact]
    public void ParseNumstatRecords_MultiLineBodyNeverBleedsIntoTheFileSet()
    {
        // The body deliberately contains a line that LOOKS like a numstat row. The trailing \x1f body
        // sentinel keeps it inside the message; only the real numstat row lands in Files.
        var body = "First body line.\n9\t9\tFakeFromBody.cs\nlast line";
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Subject", body, "1\t0\tReal.cs");

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        Assert.Equal(body, commit.Body);
        var file = Assert.Single(commit.Files);
        Assert.Equal("Real.cs", file.Path);
    }

    [Fact]
    public void ParseNumstatRecords_BinaryRowsKeepThePathWithNullCounts()
    {
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Add image", "", "-\t-\tassets/logo.png");

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        var file = Assert.Single(commit.Files);
        Assert.Equal("assets/logo.png", file.Path);
        Assert.Null(file.Added);
        Assert.Null(file.Deleted);
    }

    [Fact]
    public void ParseNumstatRecords_AcceptsTheMinimalLegacyHashOnlyShape()
    {
        // The pre-3.8 fetch format (%x01%H only): hash line + numstat rows, no \x1f fields.
        var log = Sentinel + "hash0\n1\t0\tA.cs\n";

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        Assert.Equal("hash0", commit.Hash);
        Assert.Equal("Unknown", commit.Author); // no author field -> attribution-safe placeholder
        Assert.Null(commit.Timestamp);
        Assert.Equal("A.cs", Assert.Single(commit.Files).Path);
    }

    [Fact]
    public void ParseNumstatRecords_SkipsJunkAndHandlesEmptyInput()
    {
        Assert.Empty(GitMetrics.ParseNumstatRecords(string.Empty));

        var log = Rec("abc123", "", "not-a-date", "Subject", "",
            "garbage-with-no-tabs",
            "\t\t",          // empty path -> skipped
            "",               // blank -> skipped
            "2\t2\tKept.cs");

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        Assert.Equal("Unknown", commit.Author);  // empty author -> placeholder
        Assert.Null(commit.Timestamp);            // unparseable date -> null, never a throw
        Assert.Equal("Kept.cs", Assert.Single(commit.Files).Path);
    }

    [Fact]
    public void BuildInsights_OrdersFilesByChangeCountThenOrdinalPathAndTruncates()
    {
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", ("A.cs", 1, 1), ("C.cs", 1, 0)),
            Commit("h2", "Alice", "2026-07-02T10:00", ("A.cs", 2, 0), ("B.cs", 1, 0)),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 4, 2)),
        };

        var insights = GitMetrics.BuildInsights(commits, topFiles: 2);

        Assert.Equal(2, insights.Files.Count);
        // A.cs: 3 changes, churn summed across all three commits.
        Assert.Equal(new FileChangeStat("A.cs", 3, 7, 3), insights.Files[0]);
        // B and C tie at 1; ordinal path tie-break keeps B (C is truncated by topFiles: 2).
        Assert.Equal("B.cs", insights.Files[1].Path);
        Assert.Equal(3, insights.CommitCount);
    }

    [Fact]
    public void BuildInsights_BinaryRowsCountAsChangesButAddNoChurn()
    {
        var commits = new[]
        {
            new DeepCommit("h1", "Alice", new DateTime(2026, 7, 1, 10, 0, 0), "s", "", new[]
            {
                new DeepFileChange("logo.png", null, null),
            }),
        };

        var insights = GitMetrics.BuildInsights(commits);

        Assert.Equal(new FileChangeStat("logo.png", 1, 0, 0), Assert.Single(insights.Files));
    }

    [Fact]
    public void BuildInsights_AggregatesContributorAttributionNewestCommitFirst()
    {
        // Records arrive in git log order (newest first): Bob's newest commit is hb2.
        var commits = new[]
        {
            Commit("hb2", "Bob", "2026-07-05T10:00", ("B.cs", 1, 0)),
            Commit("ha1", "Alice", "2026-07-04T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0)),
            Commit("hb1", "Bob", "2026-07-01T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0)),
        };

        var insights = GitMetrics.BuildInsights(commits);

        Assert.Equal(2, insights.Contributors.Count);
        // Bob has more commits so he sorts first — attribution counts, not a "rank" field.
        Assert.Equal(new ContributorStat("Bob", 2, 2, new DateOnly(2026, 7, 5), "hb2"), insights.Contributors[0]);
        Assert.Equal(new ContributorStat("Alice", 1, 2, new DateOnly(2026, 7, 4), "ha1"), insights.Contributors[1]);
    }

    [Fact]
    public void BuildInsights_BucketsActivityPerDayAscending()
    {
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T18:00", ("A.cs", 1, 0)),
            Commit("h2", "Alice", "2026-07-03T09:00", ("A.cs", 1, 0)),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 1, 0)),
        };

        var insights = GitMetrics.BuildInsights(commits);

        Assert.Equal(new[]
        {
            (new DateOnly(2026, 7, 1), 1),
            (new DateOnly(2026, 7, 3), 2),
        }, insights.Activity);
    }

    [Fact]
    public void BuildInsights_EmptyInputYieldsEmptyViews()
    {
        var insights = GitMetrics.BuildInsights(Array.Empty<DeepCommit>());

        Assert.Empty(insights.Files);
        Assert.Empty(insights.Contributors);
        Assert.Empty(insights.Activity);
        Assert.Equal(0, insights.CommitCount);
    }

    [Fact]
    public void ParseNumstatLog_CarriesTheHubInsightsFromTheSameParse()
    {
        // One parse, several views: the hotspot/coupling pulse also carries the hub aggregates.
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Subject", "", "1\t0\tA.cs");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.NotNull(deep.Insights);
        Assert.Equal(1, deep.Insights!.CommitCount);
        Assert.Equal("A.cs", Assert.Single(deep.Insights.Files).Path);
        Assert.Equal("Alice", Assert.Single(deep.Insights.Contributors).Name);
    }

    private static DeepCommit Commit(string hash, string author, string date, params (string Path, int Added, int Deleted)[] files)
        => new(hash, author,
            DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            "subject", "",
            files.Select(f => new DeepFileChange(f.Path, f.Added, f.Deleted)).ToList());
}
