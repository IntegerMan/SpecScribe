using SpecScribe;

namespace SpecScribe.Tests;

public class SiteGeneratorReadmeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-readme-").FullName;

    public SiteGeneratorReadmeTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "_bmad-output"));
        File.WriteAllText(Path.Combine(_root, "README.md"), "# Sample Project\n\nWelcome to the project overview.\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ForgeOptions Options(bool includeReadme) => ForgeOptions.Resolve(
        source: Path.Combine(_root, "_bmad-output"),
        output: Path.Combine(_root, "site"),
        projectName: "SpecScribe",
        includeReadme: includeReadme);

    [Fact]
    public void GenerateAll_RendersReadmePageAndLinksItFromIndex_WhenEnabled()
    {
        new SiteGenerator(Options(includeReadme: true)).GenerateAll();

        var readmePath = Path.Combine(_root, "site", "readme.html");
        Assert.True(File.Exists(readmePath));
        Assert.Contains("Welcome to the project overview.", File.ReadAllText(readmePath));

        var index = File.ReadAllText(Path.Combine(_root, "site", "index.html"));
        Assert.Contains("href=\"readme.html\"", index);
    }

    [Fact]
    public void GenerateAll_OmitsReadme_WhenDisabled()
    {
        new SiteGenerator(Options(includeReadme: false)).GenerateAll();

        Assert.False(File.Exists(Path.Combine(_root, "site", "readme.html")));

        var index = File.ReadAllText(Path.Combine(_root, "site", "index.html"));
        Assert.DoesNotContain("href=\"readme.html\"", index);
    }

    [Fact]
    public void GenerateAll_OmitsReadme_WhenFileMissing()
    {
        File.Delete(Path.Combine(_root, "README.md"));

        new SiteGenerator(Options(includeReadme: true)).GenerateAll();

        Assert.False(File.Exists(Path.Combine(_root, "site", "readme.html")));
    }
}
