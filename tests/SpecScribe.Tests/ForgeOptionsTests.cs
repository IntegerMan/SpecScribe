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
        Assert.Equal(Path.Combine(Repo, "docs", "live"), options.OutputRoot);
    }

    [Fact]
    public void Resolve_ThrowsActionableErrorWhenNothingIsFound()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;

        var ex = Assert.Throws<DirectoryNotFoundException>(() => ForgeOptions.Resolve(startDirectory: outside));
        Assert.Contains("--source", ex.Message);
    }

    [Fact]
    public void Resolve_ExplicitSourceWinsAndAnchorsTheOtherDefaults()
    {
        var source = Path.Combine(Repo, "_bmad-output");
        var options = ForgeOptions.Resolve(source: source, startDirectory: _root);

        Assert.Equal(source, options.SourceRoot);
        Assert.Equal(Repo, options.RepoRoot); // derived from the source's parent, not discovery
        Assert.Equal(Path.Combine(Repo, "docs", "live"), options.OutputRoot);
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
}
