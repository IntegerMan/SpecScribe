using System;
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
    public void RenderFooter_CarriesRepoAndDetailsLinks_FromRoot()
    {
        var footer = PathUtil.RenderFooter();
        Assert.Contains(PathUtil.RepositoryUrl, footer);
        // Root page: the details link is a bare href (no relative prefix), relabelled from "About".
        Assert.Contains("href=\"about.html\"", footer);
        Assert.Contains(">View generation details</a>", footer);
        // Story 10.4: the generation clock now routes through PortalDates — one date token + 24-hour time + an
        // explicit machine-local zone label ("Jul 10, 2026 at 17:14 UTC-04:00"). Asserted by shape, not against
        // DateTime.Now, so the test doesn't depend on the moment it runs.
        Assert.Matches(@" on [A-Z][a-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2} &middot;", footer);
        // Story 8.2: the shared status legend key rides before the footer credit on every HTML page.
        Assert.Contains("class=\"status-legend-key\"", footer);
        Assert.Contains("status-legend-key-swatch unrecognized", footer);
    }

    [Fact]
    public void RenderFooter_ResolvesDetailsHrefFromNestedDepth()
    {
        var prefix = PathUtil.RelativePrefix("adrs/0001-thing.html"); // "../"
        var footer = PathUtil.RenderFooter(prefix);
        Assert.Contains("href=\"../about.html\"", footer);
        Assert.Contains(">View generation details</a>", footer);
    }

    [Fact]
    public void RenderHeadOpen_EncodesTitleAndStylesheetHref()
    {
        var html = PathUtil.RenderHeadOpen("Cats & Dogs", "../style.css", "../specscribe.js");
        Assert.Contains("<title>Cats &amp; Dogs</title>", html);
        // The css/js hrefs carry a build-versioned cache-busting query so a cached copy can't mask a redeploy.
        Assert.Contains("href=\"../style.css?v=", html);
        Assert.Contains("<script src=\"../specscribe.js?v=", html);
        Assert.Contains("\" defer></script>", html);
        // Favicon + description/OG land in <head> so tabs get an icon and shared links aren't bare. [Story 1.5 G1/G2]
        Assert.Contains("<link rel=\"icon\"", html);
        Assert.Contains("<meta name=\"description\"", html);
        Assert.Contains("property=\"og:title\"", html);
        Assert.StartsWith("<!DOCTYPE html>", html);
    }

    [Fact]
    public void RenderHeadOpen_FaviconIsTheNibMarkOnATealTile()
    {
        var html = PathUtil.RenderHeadOpen("Home", "specscribe.css", "specscribe.js");

        // Pull the favicon href and decode the percent-encoded data URI back to the raw SVG.
        const string marker = "href=\"data:image/svg+xml,";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "head is missing the data-URI favicon");
        start += marker.Length;
        var end = html.IndexOf('"', start);
        var svg = Uri.UnescapeDataString(html[start..end]);

        // The favicon reuses the ONE shared nib geometry — no fourth hand-copied rendition to drift.
        Assert.Contains(HtmlRenderAdapter.NibPathData, svg);
        // Rendered as the VS Code panel icon's self-contained palette: teal tile + gold vent.
        Assert.Contains("#2e6b7a", svg);
        Assert.Contains("#d4a017", svg);
        // And NOT the retired gold-quill-spark star (a distinctive point of its old path).
        Assert.DoesNotContain("18.4 13.6", svg);
    }
}
