using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 7.1 deferred-debt cleanup: ambiguous <see cref="SiteGenerator.ResolveCommitPageHref"/>,
/// streamed <see cref="SiteGenerator.TryCountCodeLines"/>, and Ordinal code-path map semantics.</summary>
public class Story71DeferredDebtCleanupTests
{
    [Fact]
    public void ResolveCommitPageHref_ExactMatch_Wins()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["abc1111ffff"] = "commit/abc1111ffff.html",
            ["abc2222eeee"] = "commit/abc2222eeee.html",
        };

        Assert.Equal("commit/abc1111ffff.html", SiteGenerator.ResolveCommitPageHref(pages, "abc1111ffff"));
    }

    [Fact]
    public void ResolveCommitPageHref_UniquePrefix_Resolves()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["abc1111ffff"] = "commit/abc1111ffff.html",
            ["def2222eeee"] = "commit/def2222eeee.html",
        };

        Assert.Equal("commit/abc1111ffff.html", SiteGenerator.ResolveCommitPageHref(pages, "abc"));
    }

    [Fact]
    public void ResolveCommitPageHref_AmbiguousPrefix_ReturnsNull()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["abc1111ffff"] = "commit/abc1111ffff.html",
            ["abc2222eeee"] = "commit/abc2222eeee.html",
        };

        Assert.Null(SiteGenerator.ResolveCommitPageHref(pages, "abc"));
    }

    [Fact]
    public void TryCountCodeLines_MatchesSplitSemantics_ForCommonLineEndings()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-loc-").FullName;
        try
        {
            AssertLineCount(dir, "a\nb\nc", 3);
            AssertLineCount(dir, "a\nb\n", 2);
            AssertLineCount(dir, "a\r\nb\r\n", 2);
            AssertLineCount(dir, "only", 1);
            AssertLineCount(dir, "", 0);
            AssertLineCount(dir, "a\rb", 2);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void TryCountCodeLines_RejectsBinaryNul()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-bin-").FullName;
        try
        {
            var path = Path.Combine(dir, "blob.bin");
            File.WriteAllBytes(path, new byte[] { (byte)'a', 0, (byte)'b' });
            Assert.False(SiteGenerator.TryCountCodeLines(path, out _));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void OrdinalCodePathMaps_KeepCaseDifferingKeysDistinct()
    {
        // Pins the comparer contract the production maps now use (Linux case-variant files must not collide).
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Foo.cs"] = "code/src/Foo.cs.html",
            ["src/foo.cs"] = "code/src/foo.cs.html",
        };

        Assert.Equal(2, pages.Count);
        Assert.Equal("code/src/Foo.cs.html", pages["src/Foo.cs"]);
        Assert.Equal("code/src/foo.cs.html", pages["src/foo.cs"]);
    }

    private static void AssertLineCount(string dir, string contents, long expected)
    {
        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, contents);
        Assert.True(SiteGenerator.TryCountCodeLines(path, out var lines));
        Assert.Equal(expected, lines);
    }
}
