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
        // (A,B) co-change twice, (A,C) once. At the default support floor of 2 (Story 24.1) the (A,C) couple is
        // coincidence and drops out entirely, leaving B as A's only qualifying couple.
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "A.cs", "C.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        var b = Assert.Single(map["A.cs"].CoupledFiles);
        Assert.Equal("B.cs", b.Path);
        Assert.Equal(2, b.Support);
        Assert.DoesNotContain(map["A.cs"].CoupledFiles, c => c.Path == "C.cs");
        // The PAIR is symmetric (same support both ways) even though its confidence is not.
        Assert.Contains(map["B.cs"].CoupledFiles, c => c.Path == "A.cs" && c.Support == 2);
    }

    [Fact]
    public void BuildFileInsights_SupportFloorIsConfigurableAndAdmitsOneOffCouplesWhenLowered()
    {
        // Same fixture, floor lowered to 1: the coincidental (A,C) couple is now admitted and ranks BELOW B —
        // A changed 3 times, so B rides 2/3 (67%) and C rides 1/3 (33%). Confidence-desc ordering (Q4).
        var commits = new[]
        {
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "A.cs", "C.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "A.cs", "B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "A.cs", "B.cs"),
        };

        var coupled = GitMetrics.BuildFileInsights(commits, minSupport: 1)["A.cs"].CoupledFiles;

        Assert.Equal(new[] { "B.cs", "C.cs" }, coupled.Select(c => c.Path));
        Assert.Equal(2.0 / 3, coupled[0].Confidence, 6);
        Assert.Equal(1.0 / 3, coupled[1].Confidence, 6);
    }

    // ---- Story 24.1: directional confidence / lift / cross-boundary on the per-file list ----

    [Fact]
    public void BuildFileInsights_ConfidenceIsAsymmetricUsingEachFocalFilesOwnChangeCount()
    {
        // A.cs changes 4 times; B.cs changes twice, always alongside A. So B is 100% confident about A
        // ("whenever I change, A changes too") while A is only 50% confident about B. A raw shared-commit count
        // reports 2 in both directions and loses exactly this finding.
        var commits = new[]
        {
            Commit("h4", "Alice", "2026-07-04T10:00", "s4", "src/A.cs"),
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "src/A.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "src/A.cs", "src/B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "src/A.cs", "src/B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        var aToB = Assert.Single(map["src/A.cs"].CoupledFiles);
        var bToA = Assert.Single(map["src/B.cs"].CoupledFiles);
        Assert.Equal(0.5, aToB.Confidence, 6);   // 2 shared / 4 of A's own changes
        Assert.Equal(1.0, bToA.Confidence, 6);   // 2 shared / 2 of B's own changes
        // Support is the same shared count in both directions — it is confidence that is directional.
        Assert.Equal(2, aToB.Support);
        Assert.Equal(2, bToA.Support);
    }

    [Fact]
    public void BuildFileInsights_LiftMeasuresConfidenceAgainstTheTargetsOwnBaseRate()
    {
        // 4 analyzed commits. A changes 4×, B changes 2× (both alongside A).
        // A→B: confidence 0.5, B's base rate 2/4 = 0.5 → lift 1.0 (B shows up exactly as often as chance predicts).
        // B→A: confidence 1.0, A's base rate 4/4 = 1.0 → lift 1.0 — the always-churning file self-demotes rather
        // than looking like a strong dependency, which is the whole reason lift is carried alongside confidence.
        var commits = new[]
        {
            Commit("h4", "Alice", "2026-07-04T10:00", "s4", "src/A.cs"),
            Commit("h3", "Alice", "2026-07-03T10:00", "s3", "src/A.cs"),
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "src/A.cs", "src/B.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "src/A.cs", "src/B.cs"),
        };

        var map = GitMetrics.BuildFileInsights(commits);

        Assert.Equal(1.0, Assert.Single(map["src/A.cs"].CoupledFiles).Lift!.Value, 6);
        Assert.Equal(1.0, Assert.Single(map["src/B.cs"].CoupledFiles).Lift!.Value, 6);
    }

    [Fact]
    public void Lift_UndefinedDenominator_IsNullNeverNaNOrInfinity()
    {
        // The guard exists because NaN/Infinity render as literal "NaN"/"∞" in markup — a null is renderable as "—".
        Assert.Null(GitMetrics.Lift(0.5, targetChangeCount: 0, analyzedCommits: 10));
        Assert.Null(GitMetrics.Lift(0.5, targetChangeCount: 10, analyzedCommits: 0));
        Assert.Equal(2.0, GitMetrics.Lift(0.5, targetChangeCount: 5, analyzedCommits: 20)!.Value, 6);
    }

    [Fact]
    public void BuildFileInsights_FlagsCrossBoundaryCouplesAndPreservesTheProcessKind()
    {
        // A.cs (src/) couples with same-module B.cs, with cross-module tests/T.cs, and with a root-level config
        // that is ALSO process signal — so one fixture proves the two lenses are carried independently.
        var commits = new[]
        {
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "src/A.cs", "src/B.cs", "tests/T.cs", "app.yaml"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "src/A.cs", "src/B.cs", "tests/T.cs", "app.yaml"),
        };

        var coupled = GitMetrics.BuildFileInsights(commits)["src/A.cs"].CoupledFiles
            .ToDictionary(c => c.Path, StringComparer.Ordinal);

        Assert.False(coupled["src/B.cs"].CrossBoundary);
        Assert.True(coupled["tests/T.cs"].CrossBoundary);
        Assert.True(coupled["app.yaml"].CrossBoundary);
        // ClassifyCoupling is preserved verbatim and is orthogonal to the boundary flag.
        Assert.Equal(GitMetrics.CouplingKind.Code, coupled["src/B.cs"].Kind);
        Assert.Equal(GitMetrics.CouplingKind.Code, coupled["tests/T.cs"].Kind);
        Assert.Equal(GitMetrics.CouplingKind.Process, coupled["app.yaml"].Kind);
    }

    [Fact]
    public void BuildFileInsights_SupportFloorAppliesBeforeTheCapSoNoiseCannotCrowdOutRealCouples()
    {
        // A.cs is coupled once each with 12 one-off partners (noise) and twice with one real partner. With the
        // floor applied AFTER a cap of 4, the real couple could be pushed out entirely; applied BEFORE, it is the
        // only survivor. Ordering the noise ahead of it is deliberate — one-off couples score 100% confidence.
        var oneOffs = Enumerable.Range(0, 12)
            .Select(i => Commit($"n{i:00}0000", "Alice", $"2026-06-{i + 1:00}T10:00", $"noise {i}",
                "src/A.cs", $"src/Noise{i:00}.cs"));
        var real = new[]
        {
            Commit("r2000000", "Alice", "2026-07-02T10:00", "real 2", "src/A.cs", "src/Real.cs"),
            Commit("r1000000", "Alice", "2026-07-01T10:00", "real 1", "src/A.cs", "src/Real.cs"),
        };

        var coupled = GitMetrics.BuildFileInsights(real.Concat(oneOffs).ToArray(), coupledCap: 4)["src/A.cs"].CoupledFiles;

        Assert.Equal("src/Real.cs", Assert.Single(coupled).Path);
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
        var coupled = Assert.Single(map["A.cs"].CoupledFiles);
        Assert.Equal("B.cs", coupled.Path);
        Assert.Equal(2, coupled.Support);
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

        // minSupport: 1 because every partner here co-changes exactly once — this test is about the CAPS, and the
        // Story 24.1 support floor would otherwise empty the coupled list before the cap could be observed.
        var insight = GitMetrics.BuildFileInsights(
            commits, historyCap: 5, contributorCap: 3, coupledCap: 4, minSupport: 1)["A.cs"];

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
        // A single commit gives the (A,B) couple support 1 — below the default floor, so no couple survives. The
        // rest of the per-file insight (change count, contributors, history) is unaffected by the coupling floor.
        Assert.Empty(a.CoupledFiles);
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

    // ---- Story 24.2: the graph-scoped coupled cap ----

    /// <summary>One commit touching the focal file and <paramref name="others"/> siblings, repeated twice so every
    /// pair clears the <see cref="GitMetrics.CouplingMinSupport"/> floor and reaches the capped list at all.</summary>
    private static IReadOnlyList<DeepCommit> WideCoupling(int others)
    {
        var paths = new List<string> { "src/Focal.cs" };
        for (var i = 0; i < others; i++) paths.Add($"src/Other{i.ToString("00", CultureInfo.InvariantCulture)}.cs");
        return new[]
        {
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", paths.ToArray()),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", paths.ToArray()),
        };
    }

    [Fact]
    public void BuildFileInsights_CoupledCap_DefaultsToEightAndHonoursTheGraphScopedCapWhenAsked()
    {
        // Owner decision D2: FileInsightCoupledCap stays 8 as the const default for any caller that does not ask
        // for more; the code page's relationship surface asks for RelationshipGraphCoupledCap. Both bounds are a
        // Take on the SAME already-computed, already-floored, already-confidence-sorted list — never a second git
        // call or a second commit scan.
        var commits = WideCoupling(30);

        Assert.Equal(8, GitMetrics.BuildFileInsights(commits)["src/Focal.cs"].CoupledFiles.Count);
        Assert.Equal(
            GitMetrics.RelationshipGraphCoupledCap,
            GitMetrics.BuildFileInsights(commits, coupledCap: GitMetrics.RelationshipGraphCoupledCap)["src/Focal.cs"].CoupledFiles.Count);
    }

    [Fact]
    public void BuildFileInsights_SupportFloorAndConfidenceSortAreAppliedBEFORETheCap()
    {
        // The ordering matters and is easy to break silently: if the cap ran first, a below-floor couple could
        // occupy a capped slot and evict a real one. Verified rather than assumed after the cap change (Task 1).
        var commits = new[]
        {
            // Focal + Strong in BOTH commits (support 2, clears the floor); Weak in only ONE (support 1, below it).
            Commit("h2", "Alice", "2026-07-02T10:00", "s2", "src/Focal.cs", "src/Strong.cs"),
            Commit("h1", "Alice", "2026-07-01T10:00", "s1", "src/Focal.cs", "src/Strong.cs", "src/Weak.cs"),
        };

        // A cap of ONE: if the floor were applied after the cap, "src/Weak.cs" could take the only slot.
        var coupled = GitMetrics.BuildFileInsights(commits, coupledCap: 1)["src/Focal.cs"].CoupledFiles;

        Assert.Single(coupled);
        Assert.Equal("src/Strong.cs", coupled[0].Path);
    }

    [Fact]
    public void ParseNumstatLog_ThreadsTheCoupledCapThroughToTheFileInsights()
    {
        // The whole point of threading it here rather than re-capping downstream: ONE parse feeds the per-file
        // insights, so the wider cap must arrive at BuildFileInsights through this single path.
        var fs = ((char)0x1f).ToString();
        var sentinel = ((char)0x01).ToString();
        string Rec(string hash, string date, params string[] rows)
            => sentinel + hash + fs + "Alice" + fs + date + fs + "s" + fs + "" + fs + "\n" +
               string.Concat(rows.Select(r => r + "\n"));

        var rows = new List<string> { "1\t0\tsrc/Focal.cs" };
        for (var i = 0; i < 30; i++) rows.Add($"1\t0\tsrc/Other{i.ToString("00", CultureInfo.InvariantCulture)}.cs");
        var log = Rec("h1", "2026-07-01T09:00", rows.ToArray()) + Rec("h2", "2026-07-02T09:00", rows.ToArray());

        Assert.Equal(8, GitMetrics.ParseNumstatLog(log).FileInsights["src/Focal.cs"].CoupledFiles.Count);
        Assert.Equal(
            GitMetrics.RelationshipGraphCoupledCap,
            GitMetrics.ParseNumstatLog(log, coupledCap: GitMetrics.RelationshipGraphCoupledCap)
                .FileInsights["src/Focal.cs"].CoupledFiles.Count);
    }
}
