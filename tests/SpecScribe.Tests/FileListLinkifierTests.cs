using SpecScribe;

namespace SpecScribe.Tests;

public class FileListLinkifierTests
{
    [Fact]
    public void LinkifyHtml_WrapsListItemsWhenHrefResolves()
    {
        var html = "<ul><li>src/SpecScribe/Foo.cs</li><li>missing/path.cs</li></ul>";
        var linked = FileListLinkifier.LinkifyHtml(html, p =>
            p == "src/SpecScribe/Foo.cs"
                ? new FileListLink("code/src/SpecScribe/Foo.cs.html", "touch-file touch-file-code")
                : null);

        Assert.Contains("class=\"touch-file touch-file-code\"", linked);
        Assert.Contains("<a href=\"code/src/SpecScribe/Foo.cs.html\">src/SpecScribe/Foo.cs</a>", linked);
        Assert.Contains("<li>missing/path.cs</li>", linked);
    }

    [Fact]
    public void LinkifyHtml_StripsNewAnnotationBeforeLookup()
    {
        var resolver = new ChangeSurfaceFileResolver(
            "../",
            new Dictionary<string, string>(),
            p => p == "src/SpecScribe/Foo.cs" ? "../code/src/SpecScribe/Foo.cs.html" : null);

        var html = "<ul><li>src/SpecScribe/Foo.cs (new)</li></ul>";
        var linked = FileListLinkifier.LinkifyHtml(html, resolver.ResolveForDevRecord);

        Assert.Contains("touch-file-new", linked);
        Assert.Contains("Foo.cs (new)", linked);
    }
}
