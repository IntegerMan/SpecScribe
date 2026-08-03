using SpecScribe;

namespace SpecScribe.Tests;

public class SiteGeneratorReadmeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-readme-").FullName;

    private string Site => Path.Combine(_root, "site");

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

        Assert.True(SiteRegion.Exists(Site, "readme.html"));
        Assert.Contains("Welcome to the project overview.", SiteRegion.Read(Site, "readme.html"));

        var index = SiteRegion.Read(Site, "index.html");
        Assert.Contains("href=\"readme.html\"", index);
    }

    [Fact]
    public void GenerateAll_OmitsReadme_WhenDisabled()
    {
        new SiteGenerator(Options(includeReadme: false)).GenerateAll();

        Assert.False(SiteRegion.Exists(Site, "readme.html"));

        var index = SiteRegion.Read(Site, "index.html");
        Assert.DoesNotContain("href=\"readme.html\"", index);
    }

    [Fact]
    public void GenerateAll_OmitsReadme_WhenFileMissing()
    {
        File.Delete(Path.Combine(_root, "README.md"));

        new SiteGenerator(Options(includeReadme: true)).GenerateAll();

        Assert.False(SiteRegion.Exists(Site, "readme.html"));
    }

    [Fact]
    public void GenerateAll_EmitsSelfContainedScriptAsset()
    {
        new SiteGenerator(Options(includeReadme: true)).GenerateAll();

        // The tooltip/copy script is copied to the output root the same way the stylesheet is, and pages link
        // it — so the site stays self-contained on a static host. [Story 1.5 Task 3]
        Assert.True(File.Exists(Path.Combine(Site, ForgeOptions.ScriptName)));

        // [Story 23.6 AC #8] The <script src> TAG is chrome and no C# code path emits one. The two halves of
        // this assertion split accordingly and both survive: C# still writes the asset (above) and still owns
        // the cache-bust token the renderer stamps onto it (below, from the IR's chrome block —
        // `web/components/surfaces/IrSurface.vue` builds the tag). The tag itself is covered end-to-end by
        // `check:parity`'s `pageSha`, which hashes the whole rendered page.
        Assert.False(string.IsNullOrEmpty(SiteRegion.Chrome(Site).AssetVersion),
            "the IR must carry the asset cache-bust — it is the renderer's only source for it, and an empty "
            + "value silently drops the ?v= query from every asset link");
    }
}
