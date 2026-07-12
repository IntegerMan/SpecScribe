using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pure coverage for Story 7.2's citation resolver (<see cref="CodeReferenceLinkifier"/>): the two citation
/// shapes (Markdig view-source hrefs + inert code-span/comment citations) resolve to in-portal code pages (or, in
/// external mode, to <c>{base}/&lt;path&gt;#L{n}</c>), unresolved references degrade to plain text without broken
/// links, and the pass is anchor-aware + idempotent. In-portal cases are disk-free (they gate on the forward map);
/// external cases use a temp repo since that mode gates on a real file existing.</summary>
public class CodeReferenceLinkifierTests
{
    private static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["src/SpecScribe/Foo.cs"] = "code/src/SpecScribe/Foo.cs.html",
    };

    private static string InPortal(string html, string prefix = "") =>
        CodeReferenceLinkifier.Linkify(html, Map, codeSourceBaseUrl: null, prefix, repoRoot: "/repo", sourceRoot: "/repo/_bmad-output");

    [Fact]
    public void HrefWithLineInHref_ResolvesToPageWithLineAnchor()
    {
        var html = "<a href=\"../../src/SpecScribe/Foo.cs:42\">Foo.cs:42</a>";

        var result = InPortal(html);

        Assert.Equal("<a href=\"code/src/SpecScribe/Foo.cs.html#L42\">Foo.cs:42</a>", result);
    }

    [Fact]
    public void HrefWithoutLine_ResolvesToPageWithNoFragment()
    {
        var html = "<a href=\"../../src/SpecScribe/Foo.cs\">Foo.cs</a>";

        var result = InPortal(html);

        Assert.Equal("<a href=\"code/src/SpecScribe/Foo.cs.html\">Foo.cs</a>", result);
        Assert.DoesNotContain("#L", result);
    }

    [Fact]
    public void HrefLineRange_UsesFirstLineOnly()
    {
        var html = "<a href=\"../../src/SpecScribe/Foo.cs:42-60\">Foo.cs</a>";

        Assert.Contains("Foo.cs.html#L42", InPortal(html));
        Assert.DoesNotContain("#L42-", InPortal(html));
    }

    [Fact]
    public void HrefResolvesWithPagePrefix()
    {
        var html = "<a href=\"../../src/SpecScribe/Foo.cs:42\">Foo.cs</a>";

        Assert.Contains("href=\"../code/src/SpecScribe/Foo.cs.html#L42\"", InPortal(html, prefix: "../"));
    }

    [Fact]
    public void InlineCodeSpanCitation_IsLinkedWithLineAnchor()
    {
        var html = "As done [Source: <code>src/SpecScribe/Foo.cs:15</code>].";

        var result = InPortal(html);

        Assert.Contains("<a href=\"code/src/SpecScribe/Foo.cs.html#L15\">", result);
        // The visible citation text (with its code span) is preserved.
        Assert.Contains("<code>src/SpecScribe/Foo.cs:15</code>", result);
    }

    [Fact]
    public void InlinePlainTextCitation_IsLinked()
    {
        var html = "[Source: src/SpecScribe/Foo.cs:15]";

        Assert.Contains("<a href=\"code/src/SpecScribe/Foo.cs.html#L15\">src/SpecScribe/Foo.cs:15</a>", InPortal(html));
    }

    [Fact]
    public void CitationInsideCommentAside_IsLinked()
    {
        // Story 2.6 renders a comment as an escaped <aside> with literal backticks and no anchor; 7.2 makes the
        // citation inside it clickable — the "comment linking" half of the story.
        var html = "<aside class=\"md-comment\">[Source: `src/SpecScribe/Foo.cs:9`]</aside>";

        var result = InPortal(html);

        Assert.Contains("<a href=\"code/src/SpecScribe/Foo.cs.html#L9\">", result);
        Assert.Contains("class=\"md-comment\"", result);
    }

    [Fact]
    public void UnresolvedHref_DegradesToPlainText()
    {
        var html = "<a href=\"../../src/SpecScribe/DoesNotExist.cs\">DoesNotExist.cs</a>";

        var result = InPortal(html);

        Assert.Equal("DoesNotExist.cs", result);
        Assert.DoesNotContain("<a", result);
    }

    [Fact]
    public void UnresolvedInlineCitation_StaysPlainText()
    {
        var html = "[Source: src/SpecScribe/DoesNotExist.cs:3]";

        Assert.Equal(html, InPortal(html));
    }

    [Fact]
    public void Idempotent_SecondPassIsANoOp()
    {
        var html = "Body [Source: <code>src/SpecScribe/Foo.cs:15</code>] and <a href=\"../../src/SpecScribe/Foo.cs:42\">Foo.cs:42</a>.";

        var once = InPortal(html);
        var twice = InPortal(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void LeavesRequirementAndNavLinksUntouched()
    {
        var html = "<a class=\"req-ref\" href=\"requirements/fr15.html\">FR15</a> <a href=\"index.html\">Home</a>";

        Assert.Equal(html, InPortal(html));
    }

    [Fact]
    public void LeavesMarkdownDocCitationsUntouched()
    {
        // The .md citation path is SourceLinkifier's (Story 1.2); this code resolver must not disturb it.
        var html = "<a href=\"../game-architecture.md\">arch</a> and [Source: _bmad-output/game-architecture.md#Overview]";

        Assert.Equal(html, InPortal(html));
    }

    [Fact]
    public void EscapesEmittedHref()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["src/A&B.cs"] = "code/src/A&B.cs.html",
        };
        var html = "<a href=\"src/A&B.cs:1\">A&B.cs</a>";

        var result = CodeReferenceLinkifier.Linkify(html, map, null, "", "/repo", "/repo/_bmad-output");

        Assert.Contains("code/src/A&amp;B.cs.html#L1", result);
    }

    [Fact]
    public void NoCodePagesAndNoBaseUrl_IsANoOp()
    {
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var html = "<a href=\"../../src/SpecScribe/Foo.cs:42\">Foo.cs</a>";

        Assert.Equal(html, CodeReferenceLinkifier.Linkify(html, empty, null, "", "/repo", "/repo/_bmad-output"));
    }

    // ---- External mode (base URL set; gates on a real repo file) ----

    private sealed class ExternalRepo : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("specscribe-cref-ext-").FullName;
        public string Source => Path.Combine(Root, "_bmad-output");

        public ExternalRepo()
        {
            Directory.CreateDirectory(Path.Combine(Root, "src", "SpecScribe"));
            Directory.CreateDirectory(Source);
            File.WriteAllText(Path.Combine(Root, "src", "SpecScribe", "Foo.cs"), "namespace X;\npublic class Foo { }\n");
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void ExternalMode_ResolvesToBaseUrlWithLineAnchor_TrailingSlashNormalized()
    {
        using var repo = new ExternalRepo();
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var html = "<a href=\"../../src/SpecScribe/Foo.cs:42\">Foo.cs:42</a>";

        var result = CodeReferenceLinkifier.Linkify(
            html, empty, "https://github.com/IntegerMan/SpecScribe/blob/main/", "", repo.Root, repo.Source);

        Assert.Equal(
            "<a href=\"https://github.com/IntegerMan/SpecScribe/blob/main/src/SpecScribe/Foo.cs#L42\">Foo.cs:42</a>",
            result);
    }

    [Fact]
    public void ExternalMode_NonRepoPath_DegradesToPlainText()
    {
        using var repo = new ExternalRepo();
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Resolves to a file that is not in the repo → no external link is emitted.
        var html = "<a href=\"../../src/SpecScribe/Missing.cs\">Missing.cs</a>";

        var result = CodeReferenceLinkifier.Linkify(
            html, empty, "https://github.com/IntegerMan/SpecScribe/blob/main", "", repo.Root, repo.Source);

        Assert.Equal("Missing.cs", result);
    }
}
