using SpecScribe;

namespace SpecScribe.Tests;

public class PathUtilTests
{
    [Theory]
    [InlineData("index.html", "")]
    [InlineData("adrs/0001-thing.html", "../")]
    [InlineData("planning-artifacts/gdds/Game/epics.html", "../../../")]
    [InlineData(@"planning-artifacts\gdds\Game\epics.html", "../../../")]
    public void RelativePrefix_ReturnsOneSegmentPerDirectory(string outputRelativePath, string expected)
        => Assert.Equal(expected, PathUtil.RelativePrefix(outputRelativePath));

    [Theory]
    [InlineData(@"a\b\c.md", "a/b/c.md")]
    [InlineData("already/forward.md", "already/forward.md")]
    public void NormalizeSlashes_ConvertsBackslashes(string input, string expected)
        => Assert.Equal(expected, PathUtil.NormalizeSlashes(input));

    [Fact]
    public void ToOutputRelative_SwapsExtensionToHtml()
        => Assert.Equal("docs/spec.html", PathUtil.ToOutputRelative("docs/spec.md"));

    [Fact]
    public void Html_EncodesMarkupCharacters()
        => Assert.Equal("&lt;b&gt;bold &amp; brash&lt;/b&gt;", PathUtil.Html("<b>bold & brash</b>"));

    [Fact]
    public void StripHtmlTags_RemovesTagsAndDecodesEntities()
        => Assert.Equal("Epic & <code>", PathUtil.StripHtmlTags("Epic &amp; <code>&lt;code&gt;</code>"));

    [Fact]
    public void RenderHeadOpen_EncodesTitleAndStylesheetHref()
    {
        var html = PathUtil.RenderHeadOpen("Cats & Dogs", "../style.css");
        Assert.Contains("<title>Cats &amp; Dogs</title>", html);
        Assert.Contains("href=\"../style.css\"", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
    }
}
