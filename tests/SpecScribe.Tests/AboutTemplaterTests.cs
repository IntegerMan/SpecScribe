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
    }
}
