namespace SpecScribe.Tests;

/// <summary>TEMPORARY — Story 16.2 AC #5. Proves empirically that a red `build-test-analyze` blocks a pull
/// request from being merged once the `main` ruleset is active. It fails in the Test step (not the Build step)
/// deliberately: the point is to prove the GATING CHECK's conclusion is what blocks the merge, not that a
/// broken compile stops CI. This branch and this file are deleted as soon as the proof is recorded.</summary>
public class CiRedProof
{
    [Xunit.Fact]
    public void DeliberatelyRed_ProvesTheRequiredCheckBlocksMerge() =>
        Xunit.Assert.Fail("deliberate CI-red proof for Story 16.2 AC #5 — this branch is throwaway");
}
