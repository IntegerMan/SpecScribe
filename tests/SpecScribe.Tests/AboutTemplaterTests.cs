using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 4.8 About page: it surfaces SpecScribe's product metadata (read from
/// the assembly, not hardcoded) and links on to the diagnostics run log.</summary>
public class AboutTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    [Fact]
    public void FromAssembly_ReadsMetadata_AndTrimsBuildSuffixFromVersion()
    {
        var meta = ProductMetadata.FromAssembly();

        // Deterministic builds append "+<commit>" to the informational version — the About page shows it trimmed.
        Assert.DoesNotContain("+", meta.Version);
        Assert.False(string.IsNullOrWhiteSpace(meta.Version));
        Assert.False(string.IsNullOrWhiteSpace(meta.Description));
        Assert.False(string.IsNullOrWhiteSpace(meta.Author));
        Assert.Equal(PathUtil.RepositoryUrl, meta.RepositoryUrl);
    }

    [Theory]
    // Deterministic build with a full 40-char hex sha → version kept, hash truncated to the first 7.
    [InlineData("0.1.0-preview+9f8e7d6c5b4a3210fedcba98765432100abcdef1", "0.1.0-preview", "9f8e7d6")]
    // Short hex suffix (< 7 chars) is kept whole.
    [InlineData("1.0.0+abcd", "1.0.0", "abcd")]
    // Non-hex "+" suffix (branch/build metadata) is dropped — the Build row shows the date only, never a bogus hash.
    [InlineData("1.0.0+branch-x", "1.0.0", null)]
    // No "+" suffix at all → no commit hash.
    [InlineData("1.0.0", "1.0.0", null)]
    public void ParseInformationalVersion_SplitsVersionAndGuardsHash(
        string informational, string expectedVersion, string? expectedHash)
    {
        var (version, hash) = ProductMetadata.ParseInformationalVersion(informational);

        Assert.Equal(expectedVersion, version);
        Assert.Equal(expectedHash, hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseInformationalVersion_EmptyInput_SignalsFallback(string? informational)
    {
        var (version, hash) = ProductMetadata.ParseInformationalVersion(informational);

        // Empty version string is the caller's cue to fall back to AssemblyName.Version.
        Assert.Equal(string.Empty, version);
        Assert.Null(hash);
    }

    [Theory]
    [InlineData("0.1.0-preview", true)]  // real trailing pre-release label
    [InlineData("1.0.0", false)]         // stable
    [InlineData("1.0.0-", false)]        // trailing bare dash is not a label
    public void IsPrerelease_RequiresNonEmptyTrailingLabel(string version, bool expected)
    {
        var meta = new ProductMetadata(version, "d", "a", "https://repo", "https://author", null, null);

        Assert.Equal(expected, meta.IsPrerelease);
    }

    [Fact]
    public void RenderPage_ShowsAssemblyMetadataAndDiagnosticsLink()
    {
        var meta = ProductMetadata.FromAssembly();
        var html = AboutTemplater.RenderPage(Nav());

        Assert.Contains("About SpecScribe", html);
        // Asserted against the REFLECTED values, not hardcoded copies, so the page can't drift from the package.
        Assert.Contains(meta.Version, html);
        Assert.Contains(meta.Description, html);
        Assert.Contains(meta.Author, html);
        Assert.Contains(meta.RepositoryUrl, html);
        // The reachability path's final hop: About → diagnostics run log.
        Assert.Contains("href=\"diagnostics.html\"", html);
        // Full page shell (skip link + single main landmark), like every other synthesized page.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\"", html);
        // The centered content-column layout shared with the diagnostics page.
        Assert.Contains("<main id=\"main-content\" class=\"info-page\">", html);
    }
}
