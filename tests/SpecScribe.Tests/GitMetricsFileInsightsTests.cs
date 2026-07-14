using System.Globalization;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Contract coverage for the per-file deep-git insight map (Story 7.4): <see cref="GitMetrics.BuildFileInsights"/>
/// derives, from the SAME parsed records the hub/hotspot/coupling views consume, each file's change count,
/// file-scoped contributor attribution (never a ranking), the files it most often changes alongside (respecting the
/// bulk-commit coupling cap), and a bounded newest-first change history. Pure and repo-free: newest-first records in,
/// bounded maps out, empty in → empty out, never a throw.</summary>
public class GitMetricsFileInsightsTests
{
    private static DeepCommit Commit(string hash, string author, string? date, string subject, params string[] paths)
        => new(hash, author,
            date is null ? null : DateTime.ParseExact(date, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            subject, "",
            paths.Select(p => new DeepFileChange(p, 1, 0)).ToList());

    [Fact]
    public void BuildFileInsights_CountsChangesPerFile()
    {
        // A.cs in three commits, B.cs in one — change count is per-file, once per commit.
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "A.cs", "B.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        Assert.Equal(3, map["A.cs"].ChangeCount);
        Assert.Equal(1, map["B.cs"].ChangeCount);
    }

    [Fact]
    public void BuildFileInsights_TalliesContributorsPerFileDescendingWithOrdinalTieBreak()
    {
        // A.cs touched by Bob twice and Alice once → Bob first by count; B.cs touched by Alice once.
        var commits = new[]
        {
            Commit("h3", "Bob", "2026-07-03T10:00", "s3", "A.cs"),
            Commit("h2", "Bob", "2026-07-02T10:00", "s2", "A.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        Assert.Equal(new[] { ("Bob", 2), ("Alice", 1) }, map["A.cs"].Contributors);
        // Attribution is file-scoped: B.cs lists only Alice (Bob never touched it), not a global roster.
        Assert.Equal(new[] { ("Alice", 1) }, map["B.cs"].Contributors);
    }

    [Fact]
    public void BuildFileInsights_ContributorsAreFileScopedNotAGlobalRanking()
    {
        // Bob has more total commits, but on C.cs only Alice appears — no cross-file aggregation leaks in.
        var commits = new[]
        {
            Commit("h4", "Bob", "2026-07-04T10:00", "s4", "A.cs"),
            Commit("h3", "Bob", "2026-07-03T10:00", "s3", "A.cs"),
            Commit("h2", "Bob", "2026-07-02T10:00", "s2", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "C.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        Assert.Equal(new[] { ("Alice", 1) }, map["C.cs"].Contributors);
        Assert.DoesNotContain(map["C.cs"].Contributors, c => c.Author == "Bob");
    }

    [Fact]
    public void BuildFileInsights_CoupledFilesAreTheOtherMemberOfEachPairDescending()
    {
        // (A,B) co-change twice, (A,C) once. A's coupled list: B (2) before C (1).
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "A.cs", "C.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        Assert.Equal(new[] { ("B.cs", 2), ("C.cs", 1) }, map["A.cs"].CoupledFiles);
        // Symmetric: B's list contains A with the same count.
        Assert.Contains(("A.cs", 2), map["B.cs"].CoupledFiles);
    }

    [Fact]
    public void BuildFileInsights_SkipsOversizedCommitsForCouplingButStillCountsChanges()
    {
        // One bulk commit of 60 files (> the 50-file coupling cap) plus two small (A,B) commits.
        var bulk = Enumerable.Range(0, 60).Select(i => $"bulk/File{i:00}.cs").ToArray();
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "bulk drop", bulk),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        // The bulk commit generated no coupling, so a bulk file has no "changes with" list...
        Assert.Empty(map["bulk/File00.cs"].CoupledFiles);
        // ...but it still counts as a change for that file (the cap is coupling-only).
        Assert.Equal(1, map["bulk/File00.cs"].ChangeCount);
        // The real (A,B) coupling survives untouched.
        Assert.Equal(("B.cs", 2), Assert.Single(map["A.cs"].CoupledFiles));
    }

    [Fact]
    public void BuildFileInsights_BuildsNewestFirstHistoryWithHashDateAuthorSubject()
    {
        var commits = new[]
        {
            Commit("ffffffff1111", "Bob", "2026-07-03T10:00", "Third change", "A.cs"),
            Commit("eeeeeeee2222", "Alice", "2026-07-01T10:00", "First change", "A.cs"),
        };

        var history = GitMetrics.BuildFileInsights(commits)["A.cs"].History;

        Assert.Equal(2, history.Count);
        // Newest-first, 7-char short hash, per-commit date/author/subject.
        Assert.Equal(new CommitTouch("fffffff", new DateOnly(2026, 7, 3), "Bob", "Third change"), history[0]);
        Assert.Equal(new CommitTouch("eeeeeee", new DateOnly(2026, 7, 1), "Alice", "First change"), history[1]);
    }

    [Fact]
    public void BuildFileInsights_HistoryRowKeepsNullDateWhenCommitTimestampIsUnparseable()
    {
        var commits = new[]
        {
            Commit("abcdef123456", "Alice", null, "Dateless", "A.cs"),
        };

        var touch = Assert.Single(GitMetrics.BuildFileInsights(commits)["A.cs"].History);

        Assert.Null(touch.Date);      // degraded, not thrown
        Assert.Equal("abcdef1", touch.ShortHash);
        Assert.Equal("Alice", touch.Author);
    }

    [Fact]
    public void BuildFileInsights_BinaryOnlyRowStillCountsAndAttributes()
    {
        var commits = new[]
        {
            new DeepCommit("h1", "Alice", new DateTime(2026, 7, 1, 10, 0, 0), "Add image", "",
                new[] { new DeepFileChange("assets/logo.png", null, null) }),
        };

        var insight = GitMetrics.BuildFileInsights(commits)["assets/logo.png"];

        Assert.Equal(1, insight.ChangeCount);
        Assert.Equal(("Alice", 1), Assert.Single(insight.Contributors));
    }

    [Fact]
    public void BuildFileInsights_EmptyInputYieldsEmptyMap()
    {
        Assert.Empty(GitMetrics.BuildFileInsights(Array.Empty<DeepCommit>()));
    }

    [Fact]
    public void BuildFileInsights_CommitWithNoFilesIsSkipped()
    {
        // A merge/empty commit (no numstat rows) contributes nothing.
        var commits = new[]
        {
            new DeepCommit("h1", "Alice", new DateTime(2026, 7, 1, 10, 0, 0), "empty", "", Array.Empty<DeepFileChange>()),
        };

        Assert.Empty(GitMetrics.BuildFileInsights(commits));
    }

    [Fact]
    public void BuildFileInsights_BoundsContributorsCoupledAndHistoryToTheirCaps()
    {
        // A.cs: 20 distinct authors, coupled with 20 distinct files, across 20 commits — every list must be capped.
        var commits = Enumerable.Range(0, 20)
            .Select(i => Commit($"hash{i:00}aaaa", $"Author{i:00}", $"2026-07-{(i % 27) + 1:00}T10:00", $"change {i}",
                "A.cs", $"partner/File{i:00}.cs"))
            .ToArray();

        var insight = GitMetrics.BuildFileInsights(commits, historyCap: 5, contributorCap: 3, coupledCap: 4)["A.cs"];

        Assert.Equal(3, insight.Contributors.Count);
        Assert.Equal(4, insight.CoupledFiles.Count);
        Assert.Equal(5, insight.History.Count);
        // TotalContributors is the full distinct-author count BEFORE the top-N take, so a page can disclose
        // truncation (review addition) instead of the capped list silently reading as complete.
        Assert.Equal(20, insight.TotalContributors);
    }

    [Fact]
    public void BuildFileInsights_HistoryRespectsCapNewestFirst()
    {
        var commits = Enumerable.Range(0, 10)
            .Select(i => Commit($"h{i:00}00000", "Alice", $"2026-07-{i + 1:00}T10:00", $"change {i}", "A.cs"))
            .ToArray(); // index 0 is newest (git log order)

        var history = GitMetrics.BuildFileInsights(commits, historyCap: 3)["A.cs"].History;

        Assert.Equal(3, history.Count);
        // The three newest are kept in newest-first order.
        Assert.Equal("change 0", history[0].Subject);
        Assert.Equal("change 1", history[1].Subject);
        Assert.Equal("change 2", history[2].Subject);
    }

    [Fact]
    public void ParseNumstatLog_CarriesTheFileInsightsFromTheSameParse()
    {
        // One parse, several views: the hotspot/coupling pulse also carries the per-file insight map.
        var fs = ((char)0x1f).ToString();
        var sentinel = "\u0001";
        string Rec(string hash, string author, string date, string subject, params string[] rows)
            => sentinel + hash + fs + author + fs + date + fs + subject + fs + "" + fs + "\n" +
               string.Concat(rows.Select(r => r + "\n"));

        var log = Rec("abcdef123456", "Alice", "2026-07-01T09:15", "Fix", "1\t0\tsrc/A.cs", "2\t0\tsrc/B.cs");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.NotNull(deep.FileInsights);
        var a = deep.FileInsights["src/A.cs"];
        Assert.Equal(1, a.ChangeCount);
        Assert.Equal(("Alice", 1), Assert.Single(a.Contributors));
        Assert.Equal(("src/B.cs", 1), Assert.Single(a.CoupledFiles));
        Assert.Equal("abcdef1", Assert.Single(a.History).ShortHash);
    }

    [Fact]
    public void ParseNumstatLog_EmptyLog_ExposesEmptyFileInsightsNeverNull()
    {
        var deep = GitMetrics.ParseNumstatLog(string.Empty);
        Assert.NotNull(deep.FileInsights);
        Assert.Empty(deep.FileInsights);
    }

    // ---- reference-graph epic grouping + relationships: exposing the pair-count map for arbitrary lookups ----

    [Fact]
    public void BuildFileInsights_OutOverload_ExposesTheSamePairCountsBuildFileInsightsAlreadyComputed()
    {
        // (A,B) co-change twice, (A,C) once — the SAME pairCounts BuildFileInsights already builds internally to
        // derive A.cs's CoupledFiles list, now also handed back via the out overload (no second scan/git call).
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "A.cs", "C.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        GitMetrics.BuildFileInsights(commits, out var pairs);

        Assert.Equal(2, GitMetrics.CoChangeCount(pairs, "A.cs", "B.cs"));
        Assert.Equal(1, GitMetrics.CoChangeCount(pairs, "A.cs", "C.cs"));
        // Canonicalized order doesn't matter to the caller.
        Assert.Equal(2, GitMetrics.CoChangeCount(pairs, "B.cs", "A.cs"));
        // A pair that never co-occurred is 0, never a throw/missing-key exception.
        Assert.Equal(0, GitMetrics.CoChangeCount(pairs, "B.cs", "C.cs"));
    }

    [Fact]
    public void CoChangeCount_EmptyMapOrEmptyPath_ReturnsZeroNeverThrows()
    {
        var empty = new Dictionary<(string, string), int>();
        Assert.Equal(0, GitMetrics.CoChangeCount(empty, "A.cs", "B.cs"));
        Assert.Equal(0, GitMetrics.CoChangeCount(empty, "", "B.cs"));
    }

    [Fact]
    public void ParseNumstatLog_CoChangePairs_MirrorsTheHubCouplingViewForArbitraryPairLookup()
    {
        var fs = ((char)0x1f).ToString();
        var sentinel = ((char)0x01).ToString();
        string Rec(string hash, string author, string date, string subject, params string[] rows)
            => sentinel + hash + fs + author + fs + date + fs + subject + fs + "" + fs + "\n" +
               string.Concat(rows.Select(r => r + "\n"));

        var log =
            Rec("h1", "Alice", "2026-07-01T09:00", "s1", "1\t0\tsrc/A.cs", "1\t0\tsrc/B.cs") +
            Rec("h2", "Alice", "2026-07-02T09:00", "s2", "1\t0\tsrc/A.cs", "1\t0\tsrc/B.cs");

        var deep = GitMetrics.ParseNumstatLog(log);

        Assert.NotNull(deep.CoChangePairs);
        Assert.Equal(2, GitMetrics.CoChangeCount(deep.CoChangePairs, "src/A.cs", "src/B.cs"));
        Assert.Contains((("src/A.cs", "src/B.cs"), 2), deep.CoChangePairs.Select(kv => (kv.Key, kv.Value)));
    }

    [Fact]
    public void ParseNumstatLog_EmptyLog_ExposesEmptyCoChangePairsNeverNull()
    {
        var deep = GitMetrics.ParseNumstatLog(string.Empty);
        Assert.NotNull(deep.CoChangePairs);
        Assert.Empty(deep.CoChangePairs);
    }
}
