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
    public void FromAssembly_OnThisBuild_StillCarriesACommitHashAndAPreReleaseLabel()
    {
        // ⚠️ TWO SILENT REGRESSIONS THIS PINS, both of which leave the whole suite green. [Story 16.3 AC #1]
        //
        // (1) THE COMMIT HASH. Story 16.3 moved version derivation to MinVer, which sets InformationalVersion
        //     itself. The "+<sha>" suffix comes from the SDK's SourceLink path (SourceRevisionId); if MinVer's
        //     assignment were to land AFTER the SDK's append, the suffix would simply be gone — and it would
        //     not error, because ParseInformationalVersion DROPS an implausible suffix rather than showing a
        //     bogus hash (IsShaLike). The About page's Build row would quietly lose the commit and nothing
        //     above would notice: FromAssembly_ReadsMetadata asserts only that the TRIMMED version has no "+".
        //
        // (2) THE PRE-RELEASE LABEL. MinVer's undirected default on a tagless repository is 0.0.0-alpha.0, but
        //     a mis-set MinVerDefaultPreReleaseIdentifiers (or a future stable tag) yields a bare 0.1.0 — which
        //     removes the About page's "Preview" badge silently, since IsPrerelease is what drives it.
        //     ADR 0040 §5: "the first release without the label is by definition no longer a preview."
        //
        // Asserted on SHAPE, never on a literal: the version carries a height that moves with every commit, and
        // the sha changes on every build. A literal here would be a gate that fails for the wrong reason.
        var meta = ProductMetadata.FromAssembly();

        Assert.NotNull(meta.CommitHash);
        Assert.Equal(7, meta.CommitHash!.Length);
        Assert.True(meta.IsPrerelease, $"version '{meta.Version}' carries no pre-release label, so the About "
            + "page's Preview badge has silently disappeared (ADR 0040 §5).");
        Assert.StartsWith("0.", meta.Version); // ADR 0040 §5: the whole preview stays in 0.x
    }

    [Theory]
    // Deterministic build with a full 40-char hex sha → version kept, hash truncated to the first 7.
    [InlineData("0.1.0-preview+9f8e7d6c5b4a3210fedcba98765432100abcdef1", "0.1.0-preview", "9f8e7d6")]
    // Exactly 7 hex chars (IsShaLike's minimum plausible sha length) is kept whole.
    [InlineData("1.0.0+abcdef0", "1.0.0", "abcdef0")]
    // Below the 7-char minimum: too short to plausibly be a git sha, dropped.
    [InlineData("1.0.0+abcd", "1.0.0", null)]
    // Non-hex "+" suffix (branch/build metadata) is dropped — the Build row shows the date only, never a bogus hash.
    [InlineData("1.0.0+branch-x", "1.0.0", null)]
    // No "+" suffix at all → no commit hash.
    [InlineData("1.0.0", "1.0.0", null)]
    // Documents the accepted gap (Story 6.1 review): IsShaLike is a shape check, not proof of origin — a
    // hex-valid-length ALL-DIGIT suffix (e.g. a date-like build number) still passes, since digits are valid hex
    // characters too and rejecting them risks false negatives on genuine shas that happen to be all-digits.
    [InlineData("1.0.0+12345678", "1.0.0", "1234567")]
    // Empty pre-"+" version segment still preserves a real hash — ParseInformationalVersion never silently drops
    // a plausible commit hash just because the version half is empty (the caller decides how to handle that).
    [InlineData("+abcdef0", "", "abcdef0")]
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
    public void ParseInformationalVersion_EmptyInput_ReturnsEmptyVersionAndNullHash(string? informational)
    {
        var (version, hash) = ProductMetadata.ParseInformationalVersion(informational);

        // FromAssembly keys its own AssemblyName.Version fallback off the PRESENCE of the informational attribute,
        // not off this empty-string result (an empty result here can also arise from an empty pre-"+" segment
        // on a non-null informational string — see the "+abcdef0" case above).
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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(AboutTemplater.BuildPage(Nav()));

        Assert.Contains("About SpecScribe", html);
        // Asserted against the REFLECTED values, not hardcoded copies, so the page can't drift from the package.
        Assert.Contains(meta.Version, html);
        Assert.Contains(meta.Description, html);
        Assert.Contains(meta.Author, html);
        Assert.Contains(meta.RepositoryUrl, html);
        // The reachability path's final hop: About → diagnostics run log.
        Assert.Contains("href=\"diagnostics.html\"", html);
        // Full page shell (skip link + single main landmark), like every other synthesized page.
        // [Story 23.6 AC #8] The skip-link assertion lived here and is NOT lost — it is head-emitted chrome,
        // and the region carries no head. `npm run check:a11y` owns `skip-link` over every EMITTED page,
        // which is the only place it can be asserted honestly now that no C# path composes a whole page.
        Assert.Contains("<main id=\"main-content\"", html);
        // The centered content-column layout shared with the diagnostics page.
        Assert.Contains("<main id=\"main-content\" class=\"info-page\">", html);
    }
}
