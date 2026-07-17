using SpecScribe;

namespace SpecScribe.Tests;

public class ChangeSurfaceFileResolverTests
{
    [Fact]
    public void Resolve_SprintStatusLinksToSprintBoard()
    {
        var resolver = new ChangeSurfaceFileResolver(
            "../",
            new Dictionary<string, string>(),
            _ => null);

        var file = resolver.Resolve(new ChangeSurface.FileListEntry(
            "_bmad-output/implementation-artifacts/sprint-status.yaml",
            "sprint-status.yaml"));

        Assert.Equal(ChangeSurfaceFileKind.Sprint, file.Kind);
        Assert.Equal("../sprint.html", file.Href);
        Assert.Equal("Sprint Status", file.Label);
    }

    [Fact]
    public void Resolve_StoryArtifactLinksToStoryPage()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["implementation-artifacts/9-4-verification-evidence-strip-on-story-pages.md"] =
                "epics/story-9-4.html",
        };
        var resolver = new ChangeSurfaceFileResolver("../", map, _ => null);

        var file = resolver.Resolve(new ChangeSurface.FileListEntry(
            "9-4-verification-evidence-strip-on-story-pages.md",
            "9-4-verification-evidence-strip-on-story-pages.md"));

        Assert.Equal(ChangeSurfaceFileKind.StoryArtifact, file.Kind);
        Assert.Equal("../epics/story-9-4.html", file.Href);
        Assert.Equal("Verification Evidence Strip on Story Pages", file.Label);
    }

    [Fact]
    public void PrettyStorySlug_PreservesAcronymsAndSmallWords()
    {
        Assert.Equal(
            "NFR and UX DR Coverage Maps",
            ChangeSurfaceFileResolver.PrettyStorySlug("nfr-and-ux-dr-coverage-maps"));
    }

    [Fact]
    public void Resolve_NewCodeFileUsesCodeNewKind()
    {
        var resolver = new ChangeSurfaceFileResolver(
            "../",
            new Dictionary<string, string>(),
            p => p == "src/SpecScribe/Foo.cs" ? "../code/src/SpecScribe/Foo.cs.html" : null);

        var file = resolver.Resolve(new ChangeSurface.FileListEntry(
            "src/SpecScribe/Foo.cs",
            "src/SpecScribe/Foo.cs (new)"));

        Assert.Equal(ChangeSurfaceFileKind.CodeNew, file.Kind);
        Assert.Equal("../code/src/SpecScribe/Foo.cs.html", file.Href);
        Assert.Equal("Foo.cs (new)", file.Label);
    }
}
