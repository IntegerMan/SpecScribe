using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Contract coverage for the directional coupling metric spine (Story 24.1): the pure cross-boundary
/// ("surprising coupling") classifier, and the shared minimum-support floor both the per-file list and the hub
/// directional view apply. Pure and repo-free (NFR8): paths in, booleans out, never a throw, no SpecScribe
/// path literals.</summary>
public class GitMetricsCouplingTests
{
    // ---- Story 24.1 Task 1: IsCrossBoundary ----

    [Fact]
    public void IsCrossBoundary_SameTopLevelDirectory_IsNotCrossBoundary()
    {
        Assert.False(GitMetrics.IsCrossBoundary("src/A.cs", "src/B.cs"));
        // Divergence BELOW the top-level segment is still the same boundary — the module is the unit, not the folder.
        Assert.False(GitMetrics.IsCrossBoundary("src/core/A.cs", "src/web/deep/B.cs"));
    }

    [Fact]
    public void IsCrossBoundary_DifferentTopLevelDirectories_IsCrossBoundary()
    {
        Assert.True(GitMetrics.IsCrossBoundary("src/A.cs", "tests/B.cs"));
        Assert.True(GitMetrics.IsCrossBoundary("apps/web/A.ts", "packages/ui/B.ts"));
    }

    [Fact]
    public void IsCrossBoundary_RootLevelFilesShareTheRootBoundary()
    {
        // Owner decision Q2: two root-level files are the same (root) boundary...
        Assert.False(GitMetrics.IsCrossBoundary("README.md", "LICENSE"));
        // ...and a root-level file is cross-boundary against anything nested.
        Assert.True(GitMetrics.IsCrossBoundary("README.md", "src/A.cs"));
        Assert.True(GitMetrics.IsCrossBoundary("src/A.cs", "README.md"));
    }

    [Fact]
    public void IsCrossBoundary_NormalizesBackslashesBeforeComparing()
    {
        // Windows-style separators must not make two same-module files look cross-boundary.
        Assert.False(GitMetrics.IsCrossBoundary(@"src\A.cs", "src/B.cs"));
        Assert.True(GitMetrics.IsCrossBoundary(@"src\A.cs", @"tests\B.cs"));
    }

    [Fact]
    public void IsCrossBoundary_IsSymmetricAndSelfIsNeverCrossBoundary()
    {
        Assert.Equal(
            GitMetrics.IsCrossBoundary("src/A.cs", "docs/B.md"),
            GitMetrics.IsCrossBoundary("docs/B.md", "src/A.cs"));
        Assert.False(GitMetrics.IsCrossBoundary("src/A.cs", "src/A.cs"));
    }

    [Fact]
    public void IsCrossBoundary_EmptyOrNullPaths_DegradeToNotCrossBoundaryNeverThrow()
    {
        // An unknowable boundary must not be asserted as an architectural smell — degrade to the quiet answer.
        Assert.False(GitMetrics.IsCrossBoundary("", "src/A.cs"));
        Assert.False(GitMetrics.IsCrossBoundary("src/A.cs", ""));
        Assert.False(GitMetrics.IsCrossBoundary(null!, null!));
        Assert.False(GitMetrics.IsCrossBoundary("/", "src/A.cs"));
    }

    [Fact]
    public void IsCrossBoundary_IsOrthogonalToTheProcessVsCodeLens()
    {
        // A pair can be BOTH cross-boundary AND process-coupling — the two lenses layer, they don't replace.
        Assert.True(GitMetrics.IsCrossBoundary("src/A.cs", "config/app.yaml"));
        Assert.Equal(GitMetrics.CouplingKind.Process, GitMetrics.ClassifyCoupling("src/A.cs", "config/app.yaml"));
    }

    [Fact]
    public void CouplingMinSupport_DefaultsToTwoSoOneOffCouplesAreCoincidenceNotSignal()
    {
        Assert.Equal(2, GitMetrics.CouplingMinSupport);
    }

    // ---- Story 24.1 Task 3: DeepGitPulse.DirectedCoupling ----
    //
    // Added by the Story 24.1 code review. Task 3 shipped with NO coverage of its own: every DirectedCoupling
    // reference in the suite was a hand-built templater fixture whose helper openly synthesized confidence, so
    // nothing asserted that ParseNumstatLog populates the view at all, that both directions are emitted, that the
    // floor reaches it, or — despite Task 7 naming a "confidence sort" — that anything is ordered.

    /// <summary>The record separator <c>ParseNumstatRecords</c> keys on — git's <c>%x01</c>, i.e. U+0001 — matching
    /// the fixture helper in <c>GitMetricsTests</c>.</summary>
    private const string Sentinel = "";

    /// <summary>Builds a numstat log where each commit is a set of paths. A commit with NO paths is a real and
    /// important case: `log --numstat` emits exactly that for a merge, and the fetch carries no --no-merges.</summary>
    private static string Numstat(params string[][] commits)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < commits.Length; i++)
        {
            sb.Append(Sentinel).Append("hash").Append(i).Append('\n');
            foreach (var path in commits[i]) sb.Append("1\t0\t").Append(path).Append('\n');
        }
        return sb.ToString();
    }

    private static DirectedCouple? Edge(DeepGitPulse deep, string from, string to) =>
        deep.DirectedCoupling.FirstOrDefault(d => d.FromPath == from && d.ToPath == to);

    [Fact]
    public void DirectedCoupling_IsPopulatedFromTheSameParseAndCarriesBothDirectionsWhenTheyDiffer()
    {
        // hub.cs changes 4x; helper.cs changes 2x, always alongside hub.cs. The relationship is strongly one-way:
        // "when helper changes, hub always changes" (100%) but "when hub changes, helper changes" only half the
        // time (50%). A symmetric shared-commit count cannot express that; this is the whole point of the story.
        var log = Numstat(
            new[] { "src/hub.cs", "src/helper.cs" },
            new[] { "src/hub.cs", "src/helper.cs" },
            new[] { "src/hub.cs" },
            new[] { "src/hub.cs" });

        var deep = GitMetrics.ParseNumstatLog(log);

        var hubToHelper = Edge(deep, "src/hub.cs", "src/helper.cs");
        var helperToHub = Edge(deep, "src/helper.cs", "src/hub.cs");
        Assert.NotNull(hubToHelper);
        Assert.NotNull(helperToHub);

        Assert.Equal(2, hubToHelper!.Support);
        Assert.Equal(2, helperToHub!.Support);       // support is a property of the PAIR, so it is shared...
        Assert.Equal(0.5, hubToHelper.Confidence, 9); // ...while confidence is directional.
        Assert.Equal(1.0, helperToHub.Confidence, 9);

        // The stronger direction ranks first — that is what "ranked by confidence" has to mean.
        Assert.Equal("src/helper.cs", deep.DirectedCoupling[0].FromPath);
    }

    [Fact]
    public void DirectedCoupling_CollapsesPureEchoRowsButKeepsGenuineAsymmetry()
    {
        // A and B change together and never apart, so BOTH directions are 100% with equal support and equal
        // lift: two rows identical in every value the table renders. Emitting both was pure echo.
        var symmetric = GitMetrics.ParseNumstatLog(Numstat(
            new[] { "src/A.cs", "src/B.cs" },
            new[] { "src/A.cs", "src/B.cs" },
            new[] { "src/A.cs", "src/B.cs" }));

        var pairRows = symmetric.DirectedCoupling
            .Where(d => (d.FromPath, d.ToPath) is ("src/A.cs", "src/B.cs") or ("src/B.cs", "src/A.cs"))
            .ToList();
        var only = Assert.Single(pairRows);
        Assert.Equal("src/A.cs", only.FromPath); // canonical ordinal-first direction survives, deterministically

        // Where the two directions genuinely differ, BOTH survive — the collapse must not average a finding away.
        var asymmetric = GitMetrics.ParseNumstatLog(Numstat(
            new[] { "src/hub.cs", "src/helper.cs" },
            new[] { "src/hub.cs", "src/helper.cs" },
            new[] { "src/hub.cs" },
            new[] { "src/hub.cs" }));
        Assert.Equal(2, asymmetric.DirectedCoupling.Count(d =>
            (d.FromPath, d.ToPath) is ("src/hub.cs", "src/helper.cs") or ("src/helper.cs", "src/hub.cs")));
    }

    [Fact]
    public void DirectedCoupling_PrefersPairsWithMoreEvidenceThanTheFloorForTheVisibleTopN()
    {
        // The defect this pins: a bare-floor pair (support 2, never apart) scores confidence 1.0, and ranking on
        // confidence alone let the ORDINAL PATH tie-break choose the visible window — so an alphabetically early
        // support-2 pair outranked a well-evidenced one. "aaa" sorts before "zzz" precisely to catch that.
        var log = Numstat(
            new[] { "aaa/one.cs", "aaa/two.cs" },   // support 2, 100% both ways -> bare floor
            new[] { "aaa/one.cs", "aaa/two.cs" },
            new[] { "zzz/x.cs", "zzz/y.cs" },       // support 3, 100% both ways -> more evidence
            new[] { "zzz/x.cs", "zzz/y.cs" },
            new[] { "zzz/x.cs", "zzz/y.cs" });

        var deep = GitMetrics.ParseNumstatLog(log, topCoupling: 1);

        var top = Assert.Single(deep.DirectedCoupling);
        Assert.Equal("zzz/x.cs", top.FromPath);
        Assert.Equal(3, top.Support);
    }

    [Fact]
    public void DirectedCoupling_FallsBackToFloorPairsRatherThanRenderingAnEmptyPanel()
    {
        // The preference above is a preference, not a gate: on a young repository EVERY pair sits at the floor,
        // and dropping them all would trade a weak panel for an empty one.
        var deep = GitMetrics.ParseNumstatLog(Numstat(
            new[] { "src/A.cs", "src/B.cs" },
            new[] { "src/A.cs", "src/B.cs" }));

        Assert.NotEmpty(deep.DirectedCoupling);
        Assert.All(deep.DirectedCoupling, d => Assert.Equal(2, d.Support));
    }

    [Fact]
    public void DirectedCoupling_AppliesTheSameSupportFloorTheRestOfTheCouplingViewsDo()
    {
        // A single shared commit is coincidence, not a relationship — and the floor must reach the hub's view,
        // not only the per-file list, which is why it is one shared const rather than a literal per surface.
        var log = Numstat(
            new[] { "src/A.cs", "src/B.cs" },
            new[] { "src/A.cs" },
            new[] { "src/B.cs" });

        Assert.Empty(GitMetrics.ParseNumstatLog(log).DirectedCoupling);
        // ...and the floor is honoured as a parameter, not just as its default.
        Assert.NotEmpty(GitMetrics.ParseNumstatLog(log, minSupport: 1).DirectedCoupling);
    }

    [Fact]
    public void DirectedCoupling_RecordsTheFloorItUsedSoConsumersNeedNotHardcodeOne()
    {
        // SiteGenerator filters the deliberately-unfiltered CoChangePairs map and must use the REAL floor; before
        // this it compared against a bare literal 2 under a comment claiming it mirrored the shared const.
        Assert.Equal(GitMetrics.CouplingMinSupport, GitMetrics.ParseNumstatLog(Numstat(new[] { "a.cs" })).MinSupport);
        Assert.Equal(5, GitMetrics.ParseNumstatLog(Numstat(new[] { "a.cs" }), minSupport: 5).MinSupport);
    }

    [Fact]
    public void DirectedCoupling_LiftBaseRateExcludesCommitsThatCouldNotHaveTouchedAnything()
    {
        // `log --numstat` emits a file-less record for every merge commit, and the fetch has no --no-merges. Such
        // a record can never raise any file's ChangeCount, so counting it in lift's denominator understates every
        // base rate and OVERSTATES every lift — whose entire interpretive value is its anchor at 1.0.
        //
        // Two real commits, both touching A and B, plus two file-less merge records. B's base rate is 2/2 = 1.0
        // (it changed in every commit that changed anything), so lift is exactly 1.0. Counting the merges would
        // give a base rate of 2/4 = 0.5 and report lift 2.0 — "twice its usual rate" for a file that is in fact
        // present every single time.
        var log = Numstat(
            new[] { "src/A.cs", "src/B.cs" },
            Array.Empty<string>(),
            new[] { "src/A.cs", "src/B.cs" },
            Array.Empty<string>());

        var deep = GitMetrics.ParseNumstatLog(log);

        var edge = Edge(deep, "src/A.cs", "src/B.cs") ?? Edge(deep, "src/B.cs", "src/A.cs");
        Assert.NotNull(edge);
        Assert.Equal(1.0, edge!.Lift!.Value, 9);

        // AnalyzedCommits is deliberately NOT the same number: it answers "how big was the window we looked at".
        Assert.Equal(4, deep.AnalyzedCommits);
    }

    [Fact]
    public void DirectedCoupling_CarriesTheSharedCrossBoundaryAndKindFlagsSoNoViewRederivesThem()
    {
        var deep = GitMetrics.ParseNumstatLog(Numstat(
            new[] { "src/A.cs", "config/app.yaml" },
            new[] { "src/A.cs", "config/app.yaml" }));

        // Both files changed twice and only ever together, so the two directions are exact echoes and collapse to
        // the canonical ordinal-first one ("config/..." precedes "src/...").
        var edge = Assert.Single(deep.DirectedCoupling);
        Assert.Equal(("config/app.yaml", "src/A.cs"), (edge.FromPath, edge.ToPath));
        Assert.True(edge.CrossBoundary);                                  // src/ vs config/
        Assert.Equal(GitMetrics.CouplingKind.Process, edge.Kind);         // orthogonal lens, both preserved

        // The symmetric population the hub GRAPH draws carries the same flags, from the same computation — AC #2
        // requires the property be shared, not re-derived per view. With a bare 3-tuple it was unreachable there.
        var pair = Assert.Single(deep.Coupling);
        Assert.True(pair.CrossBoundary);
        Assert.Equal(GitMetrics.CouplingKind.Process, pair.Kind);
    }
}
