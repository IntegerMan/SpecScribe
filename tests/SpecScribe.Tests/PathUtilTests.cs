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

    [Theory]
    [InlineData("C:/Dev/SpecScribe/file.md", true)] // rooted — the "no common root" GetRelativePath fallback
    [InlineData("/etc/passwd", true)] // rooted on POSIX
    [InlineData("..", true)]
    [InlineData("../sibling/file.md", true)]
    [InlineData("../../file.md", true)]
    [InlineData("foo/bar.md", false)]
    [InlineData("file.md", false)]
    // A real in-repo path merely NAMED like an up-level segment must not be misclassified as an escape — a bare
    // "..".StartsWith substring check would wrongly flag this. [Story 6.10 deferred-work fix]
    [InlineData("..cache/notes.md", false)]
    public void EscapesRepoRoot_ChecksTheLeadingSegment_NotABareSubstring(string relativePath, bool expected)
        => Assert.Equal(expected, PathUtil.EscapesRepoRoot(relativePath));

    [Fact]
    public void ResolveRealPath_NoSymlinksInvolved_ReturnsTheFullNormalizedPath()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-realpath-");
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(dir.FullName, "a", "b")).FullName;
            var file = Path.Combine(nested, "f.txt");
            File.WriteAllText(file, "x");

            Assert.Equal(Path.GetFullPath(file), PathUtil.ResolveRealPath(file));
            // A path that doesn't exist yet has nothing to resolve against — degrades to the plain lexical form.
            Assert.Equal(Path.GetFullPath(Path.Combine(nested, "missing.txt")), PathUtil.ResolveRealPath(Path.Combine(nested, "missing.txt")));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [SkippableFact]
    public void ResolveRealPath_ChasesASymlinkedAncestorDirectory_ToItsRealTarget()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-realpath-symlink-");
        try
        {
            var real = Directory.CreateDirectory(Path.Combine(dir.FullName, "real")).FullName;
            File.WriteAllText(Path.Combine(real, "f.txt"), "x");
            var link = Path.Combine(dir.FullName, "link");

            try
            {
                Directory.CreateSymbolicLink(link, real);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Creating a directory symlink needs elevation/Developer Mode on some Windows hosts — 6-10's own
                // deferred item hit the same "can't reproduce in every CI environment" wall for the sibling
                // different-drive branch. Skipped, not failed. [6-10-deferred-debt-cleanup]
                throw new SkipException("Creating a symbolic link isn't permitted on this host — skipped, not failed.");
            }

            var resolved = PathUtil.ResolveRealPath(Path.Combine(link, "f.txt"));

            Assert.Equal(Path.GetFullPath(Path.Combine(real, "f.txt")), resolved);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [SkippableFact]
    public void ResolveRealPath_ChasesASymlinkPartwayDownTheTree_NotJustAtTheLeadingSegment()
    {
        // Blind Hunter (spec-epic6 review): the sibling symlink test above only covers "the queried path's FIRST
        // segment is itself the link" — this one puts a real, non-symlinked ancestor ("outer") ahead of the link,
        // and a real nested subdirectory ("nested") after it, matching the doc comment's own "an artifact path
        // that traverses a symlink" claim more literally than a root-is-the-link shape does.
        var dir = Directory.CreateTempSubdirectory("specscribe-realpath-symlink-nested-");
        try
        {
            var outer = Directory.CreateDirectory(Path.Combine(dir.FullName, "outer")).FullName;
            var real = Directory.CreateDirectory(Path.Combine(dir.FullName, "real", "nested")).FullName;
            File.WriteAllText(Path.Combine(real, "f.txt"), "x");
            var link = Path.Combine(outer, "link");

            try
            {
                Directory.CreateSymbolicLink(link, real);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new SkipException("Creating a symbolic link isn't permitted on this host — skipped, not failed.");
            }

            var resolved = PathUtil.ResolveRealPath(Path.Combine(outer, "link", "f.txt"));

            Assert.Equal(Path.GetFullPath(Path.Combine(real, "f.txt")), resolved);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

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
        // Status legend is on-demand beside status surfaces — not footer chrome.
        Assert.Contains("class=\"doc-footer\"", footer);
        Assert.DoesNotContain("status-legend", footer);
        Assert.DoesNotContain("doc-footer-credit", footer);
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
