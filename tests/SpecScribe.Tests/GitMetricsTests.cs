using System.Diagnostics;
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

    [Fact]
    public void ParseChangedFiles_CollapsesFullPathRenameToTheNewPath()
    {
        // Defensive: name-status/numstat-shaped "old => new" lines (not emitted by --name-only) still
        // collapse to the new path so frequency is not split. Production relies on -M so --name-only
        // already prints the destination path only for rename commits.
        var log = "src/Old.cs => src/New.cs\n\n" +
                  "src/New.cs\n";

        var files = GitMetrics.ParseChangedFiles(log);

        var only = Assert.Single(files);
        Assert.Equal(("src/New.cs", 2), only);
    }

    [Fact]
    public void ParseChangedFiles_CollapsesBraceAbbreviatedRenameToTheNewPath()
    {
        // Same defensive collapse for brace-abbreviated rename forms.
        var log = "src/{Old.cs => New.cs}\n\n" +
                  "src/New.cs\n";

        var files = GitMetrics.ParseChangedFiles(log);

        var only = Assert.Single(files);
        Assert.Equal(("src/New.cs", 2), only);
    }

    [Fact]
    public void LastCommitTimestamp_UsesMaximumTimeOnLastDayRegardlessOfListOrder()
    {
        var lastDay = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (lastDay, 2) };
        // Deliberately oldest-first within the day — max time must still win.
        var commitsByDay = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
        {
            [lastDay] = new[]
            {
                new CommitInfo("aaa1111", "Earlier", "Alice", "10:00"),
                new CommitInfo("bbb2222", "Later", "Bob", "14:00"),
            },
        };

        var stamp = GitMetrics.LastCommitTimestamp(series, commitsByDay);

        Assert.Equal(new DateTime(2026, 1, 7, 14, 0, 0), stamp);
    }

    [Fact]
    public void LastCommitTimestamp_SkipsUnparseableTimesWhenPickingMaximum()
    {
        var lastDay = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (lastDay, 2) };
        var commitsByDay = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
        {
            [lastDay] = new[]
            {
                new CommitInfo("aaa1111", "Bad", "Alice", "not-a-time"),
                new CommitInfo("bbb2222", "Good", "Bob", "14:00"),
            },
        };

        Assert.Equal(new DateTime(2026, 1, 7, 14, 0, 0), GitMetrics.LastCommitTimestamp(series, commitsByDay));
    }

    [Fact]
    public void LastCommitTimestamp_FallsBackToMidnightWhenNoTimeParses()
    {
        var lastDay = new DateOnly(2026, 1, 7);
        var series = new (DateOnly Day, int Count)[] { (lastDay, 1) };
        var commitsByDay = new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
        {
            [lastDay] = new[] { new CommitInfo("aaa1111", "Bad time", "Alice", "not-a-time") },
        };

        Assert.Equal(lastDay.ToDateTime(TimeOnly.MinValue), GitMetrics.LastCommitTimestamp(series, commitsByDay));
    }

    [Fact]
    public void LastCommitTimestamp_EmptySeriesReturnsMinValue()
    {
        Assert.Equal(DateTime.MinValue, GitMetrics.LastCommitTimestamp(
            Array.Empty<(DateOnly, int)>(),
            new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>()));
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
        Assert.Equal(0, empty.AnalyzedCommits);
    }

    [Fact]
    public void ParseNumstatLog_AnalyzedCommitsReflectsParsedCommitCount()
    {
        // Honest window for deep pages: the parsed commit count (bounded by -n 300), never a hard-coded 300.
        var log = Numstat(
            new[] { "A.cs" },
            new[] { "B.cs" },
            new[] { "C.cs" });

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.Equal(3, deep.AnalyzedCommits);
        Assert.Equal(3, deep.Commits.Count);
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
    public void ParseNumstatRecords_BodyContainingRawFieldSentinelDoesNotTruncateNumstatRows()
    {
        // A body embedding the raw 0x1F field-sentinel byte (pathological, but not impossible in arbitrary
        // commit text) used to shift the numstat block off a fixed fields[5] index and silently drop rows
        // after it. It must not truncate — every numstat row still lands in Files. [Deferred, Story 3.8]
        var body = "before" + FS + "after";
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Subject", body,
            "3\t1\tsrc/A.cs",
            "10\t0\tsrc/B.cs");

        var commit = Assert.Single(GitMetrics.ParseNumstatRecords(log));

        Assert.Equal(body, commit.Body);
        Assert.Equal(2, commit.Files.Count);
        Assert.Equal(new DeepFileChange("src/A.cs", 3, 1), commit.Files[0]);
        Assert.Equal(new DeepFileChange("src/B.cs", 10, 0), commit.Files[1]);
    }

    [Fact]
    public void BuildInsights_CommitCountNeverDivergesFromSummedActivity()
    {
        // A commit with no parseable timestamp is still counted per-file, but must not inflate CommitCount past
        // what Activity sums — the two totals can never disagree by construction. [Deferred, Story 3.8]
        var commits = new[]
        {
            Commit("h2", "Alice", "2026-07-02T10:00", ("A.cs", 1, 0)),
            new DeepCommit("h1", "Bob", null, "s", "", new[] { new DeepFileChange("A.cs", 1, 0) }),
        };

        var insights = GitMetrics.BuildInsights(commits);

        Assert.Equal(2, Assert.Single(insights.Files).Changes); // both commits still counted per-file
        Assert.Equal(insights.Activity.Sum(a => a.Count), insights.CommitCount);
        Assert.Equal(1, insights.CommitCount); // the undated commit is excluded from this dated aggregate
    }

    [Fact]
    public void BuildInsights_ChurnSumsEveryNumstatRowWhileChangesCountsOncePerCommit()
    {
        // A rename+modify pair resolving to the same path emits two numstat rows within one commit. Changes
        // counts the commit once (dedup via seenInCommit); churn sums both rows regardless — an intentional,
        // documented tradeoff (deferred-work.md, story-3-8), pinned here so it can't silently change either
        // way without a test failing.
        var commit = new DeepCommit("h1", "Alice", new DateTime(2026, 7, 1, 10, 0, 0), "s", "", new[]
        {
            new DeepFileChange("A.cs", 3, 1),
            new DeepFileChange("A.cs", 2, 0),
        });

        var file = Assert.Single(GitMetrics.BuildInsights(new[] { commit }).Files);

        Assert.Equal(1, file.Changes);      // deduped: one commit, one change
        Assert.Equal(5, file.LinesAdded);   // churn sums both rows: 3 + 2
        Assert.Equal(1, file.LinesDeleted); // churn sums both rows: 1 + 0
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
        // A.cs: 3 changes, churn summed across all three commits; h3 is its newest (latest change).
        var a = insights.Files[0];
        Assert.Equal("A.cs", a.Path);
        Assert.Equal(3, a.Changes);
        Assert.Equal(7, a.LinesAdded);
        Assert.Equal(3, a.LinesDeleted);
        Assert.Equal("h3", a.LatestHash);
        Assert.Equal(new DateOnly(2026, 7, 3), a.LastChangeDate);
        // B and C tie at 1; ordinal path tie-break keeps B (C is truncated by topFiles: 2).
        Assert.Equal("B.cs", insights.Files[1].Path);
        Assert.Equal(3, insights.CommitCount);
        // TotalFilesTouched (3: A/B/C) exceeds the truncated Files.Count (2) so the page can disclose the cap.
        Assert.Equal(3, insights.TotalFilesTouched);
    }

    [Fact]
    public void BuildInsights_TotalContributorsDiscloseCapWhenAuthorListIsTruncated()
    {
        var commits = new[]
        {
            Commit("h1", "Alice", "2026-07-03T10:00", ("A.cs", 1, 0)),
            Commit("h2", "Bob", "2026-07-02T10:00", ("A.cs", 1, 0)),
        };

        var file = Assert.Single(GitMetrics.BuildInsights(commits, topContributorsPerFile: 1).Files);

        Assert.Single(file.Contributors);
        // TotalContributors (2: Alice + Bob) exceeds the truncated Contributors.Count (1).
        Assert.Equal(2, file.TotalContributors);
    }

    [Fact]
    public void BuildInsights_BackfillsLastChangeDateWhenNewestCommitDateIsUnparseable()
    {
        // The newest commit touching A.cs has no parseable timestamp (Timestamp: null); an older commit does.
        // LastChangeDate must fall back to that older, valid date rather than staying stuck null.
        var commits = new[]
        {
            new DeepCommit("h2", "Alice", null, "s", "", new[] { new DeepFileChange("A.cs", 1, 0) }),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 1, 0)),
        };

        var file = Assert.Single(GitMetrics.BuildInsights(commits).Files);

        Assert.Equal("h2", file.LatestHash); // identity still comes from the newest commit
        Assert.Equal(new DateOnly(2026, 7, 1), file.LastChangeDate); // date backfilled from the older commit
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

        var file = Assert.Single(GitMetrics.BuildInsights(commits).Files);

        Assert.Equal("logo.png", file.Path);
        Assert.Equal(1, file.Changes);
        Assert.Equal(0, file.LinesAdded);
        Assert.Equal(0, file.LinesDeleted);
    }

    [Fact]
    public void BuildInsights_ScopesContributorsPerFileNotGlobally()
    {
        // Records arrive in git log order (newest first). The "who works on this file?" drill-down attributes
        // each author to the files THEY touched — never a global scoreboard.
        var commits = new[]
        {
            Commit("hb2", "Bob", "2026-07-05T10:00", ("B.cs", 1, 0)),
            Commit("ha1", "Alice", "2026-07-04T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0)),
            Commit("hb1", "Bob", "2026-07-01T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0)),
        };

        var insights = GitMetrics.BuildInsights(commits);

        // Global figure is only a distinct-author count for headline context — no ranked people list.
        Assert.Equal(2, insights.ContributorCount);

        // B.cs was touched by Bob (twice) and Alice (once): Bob sorts first by commit count, and each
        // contributor's date is their newest commit touching THIS file.
        var b = insights.Files.Single(f => f.Path == "B.cs");
        Assert.Equal(new[] { "Bob", "Alice" }, b.Contributors.Select(c => c.Name));
        Assert.Equal(new FileContributor("Bob", 2, new DateOnly(2026, 7, 5)), b.Contributors[0]);
        Assert.Equal(new FileContributor("Alice", 1, new DateOnly(2026, 7, 4)), b.Contributors[1]);

        // A.cs was touched by Bob (hb1) and Alice (ha1), once each — Bob's date is his A.cs commit (Jul 1),
        // NOT his newer B.cs commit, proving the date is file-scoped.
        var aFile = insights.Files.Single(f => f.Path == "A.cs");
        Assert.Equal(new FileContributor("Bob", 1, new DateOnly(2026, 7, 1)), aFile.Contributors.Single(c => c.Name == "Bob"));
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
        Assert.Empty(insights.Activity);
        Assert.Equal(0, insights.CommitCount);
        Assert.Equal(0, insights.ContributorCount);
    }

    [Fact]
    public void ParseNumstatLog_CarriesTheHubInsightsFromTheSameParse()
    {
        // One parse, several views: the hotspot/coupling pulse also carries the hub aggregates.
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Subject", "", "1\t0\tA.cs");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.NotNull(deep.Insights);
        Assert.Equal(1, deep.Insights!.CommitCount);
        Assert.Equal(1, deep.Insights.ContributorCount);
        var file = Assert.Single(deep.Insights.Files);
        Assert.Equal("A.cs", file.Path);
        Assert.Equal("Alice", Assert.Single(file.Contributors).Name);
    }

    [Fact]
    public void ParseNumstatLog_ExposesTheParsedCommitsForPerCommitPages()
    {
        // Story 7.5: the pulse carries the per-commit records (subject/body/author/timestamp/files) so the
        // per-commit detail pages ride the same one parse — newest-first (git log order), a binary row kept
        // with null churn. Same records BuildInsights consumes, exposed rather than re-parsed.
        var log = Rec("full1234abcd", "Alice", "2026-07-02T11:00", "Newer subject", "Body text.",
                      "3\t1\tsrc/A.cs")
                + Rec("full5678wxyz", "Bob", "2026-07-01T09:15", "Older subject", "",
                      "-\t-\tassets/logo.png");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.Equal(2, deep.Commits.Count);
        // Newest-first: git log order is preserved through the parse.
        Assert.Equal("full1234abcd", deep.Commits[0].Hash);
        Assert.Equal("Alice", deep.Commits[0].Author);
        Assert.Equal(new DateTime(2026, 7, 2, 11, 0, 0), deep.Commits[0].Timestamp);
        Assert.Equal("Newer subject", deep.Commits[0].Subject);
        Assert.Equal("Body text.", deep.Commits[0].Body);
        Assert.Equal(new DeepFileChange("src/A.cs", 3, 1), Assert.Single(deep.Commits[0].Files));
        // Binary row on the older commit keeps its path with null counts.
        var binary = Assert.Single(deep.Commits[1].Files);
        Assert.Equal("assets/logo.png", binary.Path);
        Assert.Null(binary.Added);
        Assert.Null(binary.Deleted);
        // Back-compat: the hotspot/coupling/insights views still populate from the same parse.
        Assert.NotNull(deep.Insights);
        Assert.Equal(2, deep.Insights!.CommitCount);
        Assert.Equal(2, deep.Hotspots.Count);
    }

    [Fact]
    public void ParseNumstatLog_EmptyLog_ExposesEmptyCommitsNeverNull()
    {
        var deep = GitMetrics.ParseNumstatLog(string.Empty);
        Assert.NotNull(deep.Commits);
        Assert.Empty(deep.Commits);
    }

    // ---- BuildCodeMapMetrics (untruncated per-file treemap metrics) [Story 7.6] ----

    [Fact]
    public void BuildCodeMapMetrics_CountsChangesOncePerCommitPerFileAndSumsChurn()
    {
        // A.cs is listed TWICE in one commit (still one change, both rows' churn summed) and once more in an
        // older commit. B.cs appears once. Binary rows contribute 0 churn but still count as a change.
        var commits = new[]
        {
            new DeepCommit("h2", "Alice", new DateTime(2026, 7, 2, 10, 0, 0), "s", "", new[]
            {
                new DeepFileChange("A.cs", 3, 1),
                new DeepFileChange("A.cs", 10, 0), // same file twice in one commit -> one change, churn still summed
                new DeepFileChange("bin/Logo.png", null, null), // binary -> 0 churn, still a change
            }),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 2, 2), ("B.cs", 5, 0)),
        };

        var map = GitMetrics.BuildCodeMapMetrics(commits);

        var a = map["A.cs"];
        Assert.Equal(2, a.Changes);                 // once per commit despite the duplicate row
        Assert.Equal(3 + 1 + 10 + 0 + 2 + 2, a.TotalChurn); // every row summed across both commits
        var bin = map["bin/Logo.png"];
        Assert.Equal(1, bin.Changes);
        Assert.Equal(0, bin.TotalChurn);            // binary rows never throw, add no churn
        Assert.Equal(5, map["B.cs"].TotalChurn);
    }

    [Fact]
    public void BuildCodeMapMetrics_ResolvesFirstAndLastDateFromNewestFirstStream()
    {
        // Records arrive newest-first. LastDate = newest day, FirstDate = oldest day in the window.
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-10T10:00", ("A.cs", 1, 0)),
            Commit("h2", "Alice", "2026-07-05T10:00", ("A.cs", 1, 0)),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 1, 0)),
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(new DateOnly(2026, 7, 10), a.LastDate);
        Assert.Equal(new DateOnly(2026, 7, 1), a.FirstDate);
    }

    [Fact]
    public void BuildCodeMapMetrics_BackfillsDatesWhenNewestCommitHasNoTimestamp()
    {
        // The newest commit touching A.cs has an unparseable timestamp (null); an older one carries the date.
        // Both FirstDate and LastDate must fall back to that older valid date rather than staying stuck null.
        var commits = new[]
        {
            new DeepCommit("h2", "Alice", null, "s", "", new[] { new DeepFileChange("A.cs", 1, 0) }),
            Commit("h1", "Alice", "2026-07-05T10:00", ("A.cs", 1, 0)),
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(new DateOnly(2026, 7, 5), a.LastDate);
        Assert.Equal(new DateOnly(2026, 7, 5), a.FirstDate);
    }

    [Fact]
    public void BuildCodeMapMetrics_IsUntruncatedUnlikeBuildInsights()
    {
        // A 60-file window: BuildInsights caps at top-50, but the treemap needs EVERY file, so this map holds 60.
        var files = Enumerable.Range(0, 60).Select(i => ($"src/File{i:00}.cs", 1, 0)).ToArray();
        var commit = Commit("h1", "Alice", "2026-07-01T10:00", files);

        var map = GitMetrics.BuildCodeMapMetrics(new[] { commit });

        Assert.Equal(60, map.Count);
        Assert.True(GitMetrics.BuildInsights(new[] { commit }).Files.Count <= 50);
    }

    [Fact]
    public void BuildCodeMapMetrics_EmptyInputYieldsEmptyMap()
    {
        Assert.Empty(GitMetrics.BuildCodeMapMetrics(Array.Empty<DeepCommit>()));
    }

    [Fact]
    public void BuildCodeMapMetrics_AveragesCoChangedFilesAndExcludesBulkCommits()
    {
        // A.cs is touched by: {A,B} (1 other), {A,B,C} (2 others), {A} alone (0 others), and a 51-file bulk commit
        // (distinct > CouplingFileSetCap=50 -> excluded from the co-change average, though it STILL counts as a change).
        var bulk = Enumerable.Range(0, 50).Select(i => ($"src/Bulk{i:00}.cs", 1, 0)).Prepend(("A.cs", 1, 0)).ToArray();
        var commits = new[]
        {
            Commit("h4", "Alice", "2026-07-04T10:00", bulk),                     // 51 files -> excluded
            Commit("h3", "Alice", "2026-07-03T10:00", ("A.cs", 1, 0)),           // alone -> 0 others
            Commit("h2", "Alice", "2026-07-02T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0), ("C.cs", 1, 0)), // 2 others
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0)),                 // 1 other
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(4, a.Changes);                       // bulk commit still counts toward change frequency
        Assert.NotNull(a.AvgCoChanged);
        Assert.Equal(1.0, a.AvgCoChanged!.Value, 3);      // (1 + 2 + 0) / 3 qualifying commits — bulk excluded
    }

    [Fact]
    public void BuildCodeMapMetrics_FileTouchedOnlyAloneHasZeroCoChangeNotNull()
    {
        var commits = new[]
        {
            Commit("h2", "Alice", "2026-07-02T10:00", ("A.cs", 1, 0)),
            Commit("h1", "Alice", "2026-07-01T10:00", ("A.cs", 1, 0)),
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(0.0, a.AvgCoChanged);                // solo commits count in the denominator, contributing 0
    }

    [Fact]
    public void BuildCodeMapMetrics_FileOnlyInBulkCommitsHasNullCoChange()
    {
        // X.cs appears only in a sweeping 51-file commit -> no qualifying (non-bulk) commit -> null co-change,
        // but it is still a real change.
        var bulk = Enumerable.Range(0, 50).Select(i => ($"src/Bulk{i:00}.cs", 1, 0)).Prepend(("X.cs", 1, 0)).ToArray();
        var map = GitMetrics.BuildCodeMapMetrics(new[] { Commit("h1", "Alice", "2026-07-01T10:00", bulk) });

        Assert.Equal(1, map["X.cs"].Changes);
        Assert.Null(map["X.cs"].AvgCoChanged);
    }

    // ---- BuildCodeMapMetrics per-file author attribution [Story 7.11] ----

    [Fact]
    public void BuildCodeMapMetrics_AccumulatesPerFileAuthorsOncePerCommitPerFile()
    {
        // A.cs listed twice in one commit by Alice must count as ONE commit for Alice on A.cs (mirrors the
        // Changes counting discipline), plus a second commit by Bob.
        var commits = new[]
        {
            new DeepCommit("h2", "Alice", new DateTime(2026, 7, 5, 10, 0, 0), "s", "", new[]
            {
                new DeepFileChange("A.cs", 3, 1),
                new DeepFileChange("A.cs", 1, 0), // same file twice in one commit -> one commit credited
            }),
            Commit("h1", "Bob", "2026-07-01T10:00", ("A.cs", 2, 0)),
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(2, a.TotalContributors);
        Assert.NotNull(a.Contributors);
        var alice = a.Contributors!.Single(c => c.Name == "Alice");
        Assert.Equal(1, alice.Commits);
        Assert.Equal(new DateOnly(2026, 7, 5), alice.LastCommitDate);
        var bob = a.Contributors!.Single(c => c.Name == "Bob");
        Assert.Equal(1, bob.Commits);
    }

    [Fact]
    public void BuildCodeMapMetrics_OrdersContributorsByCommitsDescThenNameAsc()
    {
        var commits = new[]
        {
            Commit("h3", "Carol", "2026-07-03T10:00", ("A.cs", 1, 0)),
            Commit("h2", "Alice", "2026-07-02T10:00", ("A.cs", 1, 0)),
            Commit("h1b", "Alice", "2026-07-01T09:00", ("A.cs", 1, 0)),
            Commit("h1", "Bob", "2026-07-01T10:00", ("A.cs", 1, 0)),
        };

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(new[] { "Alice", "Bob", "Carol" }, a.Contributors!.Select(c => c.Name)); // Alice=2 commits first, Bob/Carol tie at 1 -> name-asc
    }

    [Fact]
    public void BuildCodeMapMetrics_ContributorsAreCappedAtCodeMapFileContributorCap()
    {
        var commits = Enumerable.Range(0, GitMetrics.CodeMapFileContributorCap + 5)
            .Select(i => Commit($"h{i}", $"Author{i:00}", "2026-07-01T10:00", ("A.cs", 1, 0)))
            .ToArray();

        var a = GitMetrics.BuildCodeMapMetrics(commits)["A.cs"];

        Assert.Equal(GitMetrics.CodeMapFileContributorCap, a.Contributors!.Count);
        Assert.Equal(GitMetrics.CodeMapFileContributorCap + 5, a.TotalContributors); // the true count survives the cap
    }

    [Fact]
    public void BuildCodeMapMetrics_FileWithNoCommitsHasNoEntry()
    {
        // BuildCodeMapMetrics only produces entries for files that appear in the window at all; there is no
        // separate "zero contributors" state to assert on a file that was never touched.
        var map = GitMetrics.BuildCodeMapMetrics(Array.Empty<DeepCommit>());
        Assert.Empty(map);
    }

    // ---- BuildTopAuthors [Story 7.11] ----

    [Fact]
    public void BuildTopAuthors_RanksByTotalCommitsAcrossAllFilesOncePerCommit()
    {
        // A single sweeping commit touching many files must count once for its author, not once per file.
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", ("A.cs", 1, 0), ("B.cs", 1, 0), ("C.cs", 1, 0)),
            Commit("h2", "Bob", "2026-07-02T10:00", ("A.cs", 1, 0)),
            Commit("h1", "Bob", "2026-07-01T10:00", ("B.cs", 1, 0)),
        };

        var top = GitMetrics.BuildTopAuthors(commits);

        Assert.Equal(new[] { "Bob", "Alice" }, top); // Bob: 2 commits, Alice: 1 commit (not 3, despite 3 files)
    }

    [Fact]
    public void BuildTopAuthors_TieBreaksByNameAscAndCapsAtCapN()
    {
        var names = new[] { "AuthorE", "AuthorD", "AuthorC", "AuthorB", "AuthorA" };
        var commits = names.Select((name, i) => Commit($"h{i}", name, "2026-07-01T10:00", ("A.cs", 1, 0))).ToArray();

        var top = GitMetrics.BuildTopAuthors(commits, capN: 3);

        Assert.Equal(3, top.Count);
        Assert.Equal(new[] { "AuthorA", "AuthorB", "AuthorC" }, top); // all tied at 1 commit -> name-ascending
    }

    [Fact]
    public void BuildTopAuthors_EmptyInputYieldsEmptyList()
    {
        Assert.Empty(GitMetrics.BuildTopAuthors(Array.Empty<DeepCommit>()));
    }

    [Fact]
    public void ParseNumstatLog_CarriesTheCodeMapMetricsFromTheSameParse()
    {
        // One parse, several views: the pulse also carries the untruncated treemap metric map.
        var log = Rec("abc123", "Alice", "2026-07-01T09:15", "Subject", "", "3\t1\tsrc/A.cs");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.NotNull(deep.CodeMapMetrics);
        var a = deep.CodeMapMetrics["src/A.cs"];
        Assert.Equal(1, a.Changes);
        Assert.Equal(4, a.TotalChurn);
        Assert.Equal(new DateOnly(2026, 7, 1), a.LastDate);
    }

    private static DeepCommit Commit(string hash, string author, string date, params (string Path, int Added, int Deleted)[] files)
        => new(hash, author,
            DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            "subject", "",
            files.Select(f => new DeepFileChange(f.Path, f.Added, f.Deleted)).ToList());

    // ---- Story 10.6 AC1: process-vs-code coupling classifier (framework-neutral, never a SpecScribe literal) ----

    [Theory]
    [InlineData("src/App.cs")]
    [InlineData("lib/util.py")]
    [InlineData("app/main.go")]
    [InlineData("Component.tsx")]
    [InlineData("README.md")]
    [InlineData("Makefile")]
    public void IsProcessPath_SourceAndAmbiguousPathsAreNotProcess(string path) =>
        Assert.False(GitMetrics.IsProcessPath(path));

    [Theory]
    [InlineData("config/settings.yaml")]
    [InlineData("config/settings.yml")]
    [InlineData("package.json")]
    [InlineData("pyproject.toml")]
    [InlineData("package-lock.json")]
    [InlineData("Gemfile.lock")]
    [InlineData("go.sum")]
    [InlineData("src/styles/theme.css")]
    [InlineData("src/styles/theme.scss")]
    [InlineData("src/styles/theme.less")]
    [InlineData("bin/App.dll")]
    [InlineData("obj/Debug/App.dll")]
    [InlineData("packages/app/dist/bundle.js")]
    [InlineData("frontend/node_modules/lib/index.js")]
    public void IsProcessPath_ConfigStatusLockfileBuildOutputAndStylesheetsAreProcess(string path) =>
        Assert.True(GitMetrics.IsProcessPath(path));

    [Fact]
    public void ClassifyCoupling_SourceToSourceIsCode()
    {
        Assert.Equal(GitMetrics.CouplingKind.Code, GitMetrics.ClassifyCoupling("src/A.cs", "src/B.cs"));
    }

    [Fact]
    public void ClassifyCoupling_YamlToCssIsProcess()
    {
        Assert.Equal(GitMetrics.CouplingKind.Process, GitMetrics.ClassifyCoupling("status.yaml", "theme.css"));
    }

    [Fact]
    public void ClassifyCoupling_SourceToLockfileIsProcess()
    {
        Assert.Equal(GitMetrics.CouplingKind.Process, GitMetrics.ClassifyCoupling("src/A.cs", "package-lock.json"));
    }

    [Fact]
    public void ClassifyCoupling_AmbiguousPathsDefaultToCode()
    {
        // Neither side matches a known process pattern — false negative (code) is cheaper than hiding a
        // real dependency behind an over-eager process classification (owner-locked design rule).
        Assert.Equal(GitMetrics.CouplingKind.Code, GitMetrics.ClassifyCoupling("README.md", "Makefile"));
    }

    [Fact]
    public void ClassifyCoupling_EitherSideProcessMakesTheWholePairProcess()
    {
        // Only ONE side needs to be process — a code file co-committed with a process file is still
        // routine-upkeep coupling, not a code dependency.
        Assert.Equal(GitMetrics.CouplingKind.Process, GitMetrics.ClassifyCoupling("src/A.cs", "config/app.json"));
        Assert.Equal(GitMetrics.CouplingKind.Process, GitMetrics.ClassifyCoupling("config/app.json", "src/A.cs"));
    }

    [Fact]
    public void BranchNameFromOriginHeadSymref_PreservesSlashyBranchNames()
    {
        // Taking the segment after the last '/' would collapse these to "foo" / "1.0". [10.4 deferred-debt]
        Assert.Equal("feature/foo", GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/origin/feature/foo"));
        Assert.Equal("main", GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/origin/main"));
        Assert.Equal("release/1.0", GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/origin/release/1.0\n"));
    }

    [Fact]
    public void BranchNameFromOriginHeadSymref_ReturnsNullWhenMissingOrNotOrigin()
    {
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref(null));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref(""));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref("   "));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref("refs/heads/main"));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/upstream/main"));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/origin/"));
        Assert.Null(GitMetrics.BranchNameFromOriginHeadSymref("refs/remotes/origin/   "));
    }
}

/// <summary>Real-git wiring for <see cref="GitMetrics.TryCompute"/> Story 3.1 fields
/// (<c>spec-3-1-deferred-debt-cleanup</c>). Asserts hard if git is unavailable — no silent skip.</summary>
public class GitMetricsTryComputeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "specscribe-trycompute-" + Guid.NewGuid().ToString("N"));

    public GitMetricsTryComputeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void TryCompute_WiresLastCommitTimestampLast30DayCountAndTopChangedFiles()
    {
        Assert.True(TryCreateGitHistory(),
            "git CLI unavailable on this host — cannot exercise TryCompute field wiring; install git rather than silently skipping this test");

        var pulse = GitMetrics.TryCompute(_root);

        Assert.NotNull(pulse);
        Assert.Equal(2, pulse!.TotalCommits);
        Assert.Equal(2, pulse.Last30DayCommitCount);
        Assert.Equal(pulse.LastCommitDate, DateOnly.FromDateTime(pulse.LastCommitTimestamp));
        // Two commits both touch tracked.txt → name-only ranking must count both, not merely "some" file.
        var tracked = Assert.Single(pulse.TopChangedFiles, f => f.Path == "tracked.txt");
        Assert.Equal(2, tracked.ChangeCount);
    }

    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\n");
        if (!RunGit("add .")) return false;
        if (!Commit("First commit")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\ntwo\n");
        return RunGit("add .") && Commit("Second commit");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"Pulse Tester\" -c user.email=pulse@example.com -c commit.gpgsign=false commit -m \"{message}\"");

    private bool RunGit(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
