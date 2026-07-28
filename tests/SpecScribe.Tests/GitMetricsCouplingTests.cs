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
}
