using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pure, disk-free coverage for the citation-target extraction and locator stripping that drive the
/// referenced code-file set (Story 7.1). Disk-backed resolution/filtering is covered at generation level in
/// <see cref="SiteGeneratorCodePagesTests"/>.</summary>
public class CodeReferenceScannerTests
{
    [Fact]
    public void ExtractTargets_PullsMarkdownLinkHrefRelativeToArtifact()
    {
        var md = "See [Source: [X.cs:76-99](../../src/SpecScribe/X.cs)] for detail.";

        var targets = CodeReferenceScanner.ExtractTargets(md);

        var hit = Assert.Single(targets);
        Assert.Equal("../../src/SpecScribe/X.cs", hit.Target);
        Assert.True(hit.RelativeToArtifact);
    }

    [Fact]
    public void ExtractTargets_PullsInlineCodeSpanTargetAsRepoRelative()
    {
        var md = "As implemented [Source: `src/SpecScribe/X.cs:15-17`].";

        var targets = CodeReferenceScanner.ExtractTargets(md);

        var hit = Assert.Single(targets);
        Assert.Equal("src/SpecScribe/X.cs:15-17", hit.Target);
        Assert.False(hit.RelativeToArtifact);
    }

    [Fact]
    public void ExtractTargets_PullsBareInlineTargetWithoutBackticks()
    {
        var md = "[Source: src/SpecScribe/X.cs:15]";

        var targets = CodeReferenceScanner.ExtractTargets(md);

        var hit = Assert.Single(targets);
        Assert.Equal("src/SpecScribe/X.cs:15", hit.Target);
        Assert.False(hit.RelativeToArtifact);
    }

    [Fact]
    public void ExtractTargets_ReturnsNothingForProseWithoutCitations()
    {
        Assert.Empty(CodeReferenceScanner.ExtractTargets("A plain [link](../../src/Foo.cs) not a source citation."));
        Assert.Empty(CodeReferenceScanner.ExtractTargets(string.Empty));
    }

    [Theory]
    [InlineData("src/SpecScribe/X.cs:15-17", "src/SpecScribe/X.cs")]
    [InlineData("src/SpecScribe/X.cs:42", "src/SpecScribe/X.cs")]
    [InlineData("_bmad-output/specs/architecture.md#Overview", "_bmad-output/specs/architecture.md")]
    [InlineData("src/SpecScribe/X.cs", "src/SpecScribe/X.cs")]
    public void StripLocator_RemovesLineAndFragmentSuffixes(string input, string expected)
    {
        Assert.Equal(expected, CodeReferenceScanner.StripLocator(input));
    }
}
