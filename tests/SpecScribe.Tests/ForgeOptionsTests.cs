using SpecScribe;

namespace SpecScribe.Tests;

public class ForgeOptionsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-tests-").FullName;

    public ForgeOptionsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "repo", "_bmad-output"));
        Directory.CreateDirectory(Path.Combine(_root, "repo", "nested", "deeper"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Repo => Path.Combine(_root, "repo");

    [Fact]
    public void Resolve_WalksUpFromStartDirectoryToFindBmadOutput()
    {
        var options = ForgeOptions.Resolve(startDirectory: Path.Combine(Repo, "nested", "deeper"));

        Assert.Equal(Repo, options.RepoRoot);
        Assert.Equal(Path.Combine(Repo, "_bmad-output"), options.SourceRoot);
        Assert.Equal(Path.Combine(Repo, "docs", "adrs"), options.AdrSourceRoot);
        Assert.Equal(Path.Combine(Repo, "SpecScribeOutput"), options.OutputRoot);
    }

    [Fact]
    public void Resolve_ThrowsActionableErrorWhenNothingIsFound()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;

        var ex = Assert.Throws<DirectoryNotFoundException>(() => ForgeOptions.Resolve(startDirectory: outside));
        Assert.Contains("--source", ex.Message);
    }

    [Fact]
    public void Resolve_TolerantModeDoesNotThrowWhenNoBmadOutputAndAnchorsOnStartDirectory()
    {
        // A plain, non-bmad workspace: the tolerant (webview/extension) path must fall back to the start directory
        // as the repo root with a (nonexistent) conventional source root, never throw — the extension is usable in
        // ANY workspace. [spec-vscode-any-workspace-and-processing-indicators]
        var plain = Directory.CreateDirectory(Path.Combine(_root, "plain-repo")).FullName;

        var options = ForgeOptions.Resolve(startDirectory: plain, requireSource: false);

        Assert.Equal(plain, options.RepoRoot);
        Assert.Equal(Path.Combine(plain, "_bmad-output"), options.SourceRoot);
        Assert.False(Directory.Exists(options.SourceRoot)); // the source root need not exist — generation degrades
        Assert.Equal(Path.Combine(plain, "SpecScribeOutput"), options.OutputRoot);
    }

    [Fact]
    public void Resolve_TolerantModeStillWalksUpToARealBmadProject()
    {
        // When a marker DOES exist up-tree, tolerant mode resolves it exactly like the default — the fallback only
        // engages when nothing is found, so real bmad projects are unaffected.
        var options = ForgeOptions.Resolve(startDirectory: Path.Combine(Repo, "nested", "deeper"), requireSource: false);

        Assert.Equal(Repo, options.RepoRoot);
        Assert.Equal(Path.Combine(Repo, "_bmad-output"), options.SourceRoot);
    }

    [Fact]
    public void Resolve_ExplicitSourceWinsAndAnchorsTheOtherDefaults()
    {
        var source = Path.Combine(Repo, "_bmad-output");
        var options = ForgeOptions.Resolve(source: source, startDirectory: _root);

        Assert.Equal(source, options.SourceRoot);
        Assert.Equal(Repo, options.RepoRoot); // derived from the source's parent, not discovery
        Assert.Equal(Path.Combine(Repo, "SpecScribeOutput"), options.OutputRoot);
    }

    [Fact]
    public void Resolve_ExplicitOverridesBeatDerivedDefaults()
    {
        var options = ForgeOptions.Resolve(
            source: Path.Combine(Repo, "_bmad-output"),
            adrs: Path.Combine(_root, "my-adrs"),
            output: Path.Combine(_root, "site"),
            projectName: "My Game");

        Assert.Equal(Path.Combine(_root, "my-adrs"), options.AdrSourceRoot);
        Assert.Equal(Path.Combine(_root, "site"), options.OutputRoot);
        Assert.Equal("My Game", options.SiteTitle);
    }

    [Fact]
    public void Resolve_AdrProbe_FindsConventionalHomeWhenDefaultAbsent()
    {
        // No docs/adrs: the ordered probe resolves the first conventional home with markdown content. [Story 4.2 Task 1]
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "decisions"));
        File.WriteAllText(Path.Combine(Repo, "docs", "decisions", "0001-first.md"), "# First\n");

        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal(Path.Combine(Repo, "docs", "decisions"), options.AdrSourceRoot);
        Assert.False(options.AdrSourceExplicit);
    }

    [Fact]
    public void Resolve_AdrProbe_CanonicalDefaultWinsWhenPresent()
    {
        // docs/adrs exists (even alongside another convention): today's default branch, byte-identical resolution.
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "adrs"));
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "decisions"));
        File.WriteAllText(Path.Combine(Repo, "docs", "decisions", "0001-first.md"), "# First\n");

        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal(Path.Combine(Repo, "docs", "adrs"), options.AdrSourceRoot);
    }

    [Fact]
    public void Resolve_AdrProbe_ExplicitAdrsAlwaysWinsAndNeverProbes()
    {
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "decisions"));
        File.WriteAllText(Path.Combine(Repo, "docs", "decisions", "0001-first.md"), "# First\n");

        var explicitDir = Path.Combine(_root, "my-adrs");
        var options = ForgeOptions.Resolve(source: Path.Combine(Repo, "_bmad-output"), adrs: explicitDir);
        Assert.Equal(explicitDir, options.AdrSourceRoot);
        Assert.True(options.AdrSourceExplicit);
    }

    [Fact]
    public void Resolve_AdrProbe_SkipsEmptyCandidatesAndSeesNestedRecordsOneLevelDeep()
    {
        // docs/adr exists but is empty; docs/decisions holds only a nested (one-level) record — the probe
        // must skip the empty candidate and count the nested one as content. [Story 4.2 Task 1]
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "adr"));
        Directory.CreateDirectory(Path.Combine(Repo, "docs", "decisions", "2024"));
        File.WriteAllText(Path.Combine(Repo, "docs", "decisions", "2024", "0007-nested.md"), "# Nested\n");

        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal(Path.Combine(Repo, "docs", "decisions"), options.AdrSourceRoot);
    }

    [Fact]
    public void Resolve_AdrProbe_NothingFoundKeepsCanonicalDefaultSilently()
    {
        // No conventional home anywhere: the canonical (absent) default is kept — ADRs are optional and a
        // fruitless probe stays silent, unlike an explicit-but-missing --adrs. [Story 4.2 Task 1]
        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal(Path.Combine(Repo, "docs", "adrs"), options.AdrSourceRoot);
        Assert.False(options.AdrSourceExplicit);
    }

    [Fact]
    public void Resolve_ReadsProjectNameFromBmadConfig()
    {
        Directory.CreateDirectory(Path.Combine(Repo, "_bmad"));
        File.WriteAllText(Path.Combine(Repo, "_bmad", "config.toml"), "project_name = \"Cozy Game\"\n");

        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal("Cozy Game", options.SiteTitle);
    }

    [Fact]
    public void Resolve_FallsBackToDefaultSiteTitle()
    {
        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.Equal(ForgeOptions.DefaultSiteTitle, options.SiteTitle);
    }

    [Fact]
    public void Resolve_DeepGitAnalyticsDefaultsToFalse()
    {
        // AC #1: deep analysis never runs implicitly — the resolved flag is off unless explicitly requested.
        var options = ForgeOptions.Resolve(startDirectory: Repo);
        Assert.False(options.DeepGitAnalytics);
    }

    [Fact]
    public void SiteSettings_DeepGitFlagFlowsIntoResolvedOptions()
    {
        // The --deep-git bool on SiteSettings must reach ForgeOptions.DeepGitAnalytics, mirroring the
        // --no-readme/IncludeReadme flow. Source is passed explicitly so resolution doesn't depend on the cwd.
        var settings = new SiteSettings { Source = Path.Combine(Repo, "_bmad-output"), DeepGit = true };

        Assert.True(settings.Resolve().DeepGitAnalytics);
    }

    [Fact]
    public void CodeSourceBaseUrl_DefaultsToNull()
    {
        // In-portal code pages are the default: an unset --code-url resolves to a null base URL. [Story 7.1]
        Assert.Null(ForgeOptions.Resolve(startDirectory: Repo).CodeSourceBaseUrl);
    }

    [Fact]
    public void SiteSettings_CodeUrlFlowsIntoResolvedOptions()
    {
        // The --code-url option must reach ForgeOptions.CodeSourceBaseUrl. Source is explicit so resolution
        // doesn't depend on the cwd. [Story 7.1]
        var settings = new SiteSettings
        {
            Source = Path.Combine(Repo, "_bmad-output"),
            CodeUrl = "https://github.com/owner/repo/blob/main",
        };

        Assert.Equal("https://github.com/owner/repo/blob/main", settings.Resolve().CodeSourceBaseUrl);
    }
}
