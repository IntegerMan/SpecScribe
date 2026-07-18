using SpecScribe;

namespace SpecScribe.Tests;

public class MarkdownConverterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private DocModel Convert(string content, string name = "doc.md")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return MarkdownConverter.Convert(path, name, Path.ChangeExtension(name, ".html"));
    }

    [Fact]
    public void Convert_ParsesFrontmatterAndBody()
    {
        var doc = Convert("""
            ---
            title: My Spec
            status: ready-for-dev
            author: Matt
            ---
            # Heading

            Body text.
            """);

        Assert.Equal("My Spec", doc.Title);
        Assert.Equal("ready-for-dev", doc.Frontmatter.Status);
        Assert.Equal("Matt", doc.Frontmatter.Author);
        Assert.Contains("<p>Body text.</p>", doc.BodyHtml);
        Assert.DoesNotContain("title: My Spec", doc.BodyHtml); // frontmatter never leaks into the body
    }

    [Fact]
    public void Convert_ParsesRouteAndTypeWhenPresentElseNull()
    {
        // A bmad-quick-dev artifact carries route/type; they classify it as direct/one-shot work. [Story 2.1 Task 1]
        var quickDev = Convert("""
            ---
            title: A quick fix
            status: done
            route: one-shot
            type: chore
            ---
            # Body
            """);
        Assert.Equal("one-shot", quickDev.Frontmatter.Route);
        Assert.Equal("chore", quickDev.Frontmatter.Type);

        // A normal doc without those fields leaves them null (they're optional; most BMad docs don't set them).
        var plain = Convert("---\ntitle: Normal\n---\n# Body\n");
        Assert.Null(plain.Frontmatter.Route);
        Assert.Null(plain.Frontmatter.Type);
    }

    [Fact]
    public void Convert_ParsesIdAndCompanionSourceListsWhenPresent()
    {
        // A spec-kernel doc declares its cross-references as YAML lists (companions:/sources:) plus an id. [Story 2.2 Task 4]
        var spec = Convert("""
            ---
            id: SPEC-specscribe
            companions:
              - requirements-catalog.md
              - settings-and-signals.md
            sources:
              - ../../planning-artifacts/prd.md
            ---
            # SpecScribe
            """);

        Assert.Equal("SPEC-specscribe", spec.Frontmatter.Id);
        Assert.Equal(new[] { "requirements-catalog.md", "settings-and-signals.md" }, spec.Frontmatter.Companions);
        Assert.Equal(new[] { "../../planning-artifacts/prd.md" }, spec.Frontmatter.Sources);
    }

    [Fact]
    public void Convert_CompanionAndSourceListsDefaultEmptyWhenAbsentOrScalar()
    {
        // Absent → empty (every non-spec doc is unaffected).
        var plain = Convert("---\ntitle: Normal\n---\n# Body\n");
        Assert.Empty(plain.Frontmatter.Companions);
        Assert.Empty(plain.Frontmatter.Sources);
        Assert.Null(plain.Frontmatter.Id);

        // A scalar (not a YAML sequence) degrades to empty rather than throwing.
        var scalar = Convert("---\ncompanions: just-a-string\nsources: also-scalar\n---\n# Body\n");
        Assert.Empty(scalar.Frontmatter.Companions);
        Assert.Empty(scalar.Frontmatter.Sources);
    }

    [Fact]
    public void Convert_FallsBackToFirstH1ThenFilenameForTitle()
    {
        Assert.Equal("From Heading", Convert("# From Heading\n\ntext").Title);
        Assert.Equal("My Neat Doc", Convert("no headings at all", "my-neat-doc.md").Title);
    }

    [Fact]
    public void Convert_RendersMermaidFencesAsClientRenderBlocks()
    {
        var doc = Convert("""
            # Diagram

            ```mermaid
            graph TD
              A --> B
            ```
            """);

        Assert.True(doc.HasMermaid);
        Assert.Contains("<pre class=\"mermaid\">", doc.BodyHtml);
        Assert.DoesNotContain("<code class=\"language-mermaid\">", doc.BodyHtml);
    }

    [Fact]
    public void Convert_OrdinaryCodeFencesAreUntouched()
    {
        var doc = Convert("```csharp\nvar x = 1;\n```");

        Assert.False(doc.HasMermaid);
        Assert.Contains("language-csharp", doc.BodyHtml);
    }

    [Fact]
    public void Convert_CollectsHeadingsWithIdsForToc()
    {
        var doc = Convert("# Top\n\n## Section One\n\n### Deep\n\n#### Too Deep\n");

        Assert.Contains(doc.Headings, h => h.Level == 2 && h.Text == "Section One");
        Assert.DoesNotContain(doc.Headings, h => h.Level == 4);
    }

    [Fact]
    public void StripFrontmatter_HandlesPresenceAbsenceAndUnterminatedBlocks()
    {
        Assert.Equal("body\n", MarkdownConverter.StripFrontmatter("---\ntitle: x\n---\nbody\n"));
        Assert.Equal("just body", MarkdownConverter.StripFrontmatter("just body"));

        // An unterminated frontmatter fence is not frontmatter — the text passes through whole.
        const string unterminated = "---\ntitle: x\nbody with no closing fence";
        Assert.Equal(unterminated, MarkdownConverter.StripFrontmatter(unterminated));
    }

    [Fact]
    public void Convert_MalformedYamlFallsBackToBodyContent()
    {
        var doc = Convert("---\n- [broken: yaml\n:::\n---\n# Real Title\n");

        Assert.Equal("Real Title", doc.Title);
        Assert.Null(doc.Frontmatter.Status);
    }

    [Fact]
    public void RenderInline_UnwrapsTheParagraphElement()
        => Assert.Equal("some <strong>bold</strong> text", MarkdownConverter.RenderInline("some **bold** text"));

    [Fact]
    public void RenderBlock_TagsMarkdownTables()
        => Assert.Contains("<table class=\"md-table\">", MarkdownConverter.RenderBlock("| a | b |\n|---|---|\n| 1 | 2 |"));

    [Fact]
    public void RenderBlock_RendersMermaidFencesAsClientRenderBlocks()
    {
        // Fragments (story remainder, dev-agent record, etc.) must carry the same mermaid fidelity as a full
        // page — a fence authored inside an artifact body used to render as inert <code class="language-mermaid">.
        var html = MarkdownConverter.RenderBlock("""
            Some intro text.

            ```mermaid
            graph TD
              A --> B
            ```
            """);

        Assert.Contains("<pre class=\"mermaid\">", html);
        Assert.DoesNotContain("<code class=\"language-mermaid\">", html);
        Assert.True(Mermaid.ContainsBlock(html));
    }

    [Fact]
    public void RenderBlock_OrdinaryCodeFenceInFragmentIsUntouched()
    {
        var html = MarkdownConverter.RenderBlock("```csharp\nvar x = 1;\n```");

        Assert.Contains("language-csharp", html);
        Assert.False(Mermaid.ContainsBlock(html));
    }

    [Fact]
    public void RenderDocumentHtml_MermaidAndCommentFidelityIsStableAcrossRepeatedCallsOnTheSharedPipeline()
    {
        // The mermaid/comment wrappers are now installed once per pipeline-Setup (a static, shared
        // MarkdownPipeline) rather than re-applied per call — repeated Convert/RenderBlock/RenderInline calls
        // must still produce byte-identical fragment HTML every time, with no accumulation/leakage of wrapper
        // layers across calls. [spec-epic2-deferred-debt-cleanup]
        const string markdown = """
            Some intro text.

            ```mermaid
            graph TD
              A --> B
            ```

            <!-- a note -->
            """;

        var firstBlock = MarkdownConverter.RenderBlock(markdown);
        var secondBlock = MarkdownConverter.RenderBlock(markdown);
        Assert.Equal(firstBlock, secondBlock);
        Assert.Contains("<pre class=\"mermaid\">", firstBlock);
        Assert.Contains("md-comment", firstBlock);

        var firstDoc = Convert(markdown);
        var secondDoc = Convert(markdown);
        Assert.Equal(firstDoc.BodyHtml, secondDoc.BodyHtml);
        Assert.True(firstDoc.HasMermaid);
    }

    [Theory]
    [InlineData("- [x] Done task", "checked")]
    [InlineData("- [X] Done task uppercase", "checked")]
    [InlineData("- [ ] Pending task", "unchecked")]
    public void RenderBlock_RendersTaskListCheckboxesWithCompletionState(string markdown, string expectation)
    {
        // GitHub-style task lists render as disabled <input type="checkbox">; a completed item carries the
        // `checked` attribute (styled into a green checkmark by specscribe.css), an incomplete one does not.
        var html = MarkdownConverter.RenderBlock(markdown);

        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("disabled", html);
        if (expectation == "checked")
        {
            Assert.Contains("checked", html);
        }
        else
        {
            Assert.DoesNotContain("checked", html);
        }
    }

    [Fact]
    public void RenderBlock_LeavesNonCheckboxBulletsUnaffected()
    {
        var html = MarkdownConverter.RenderBlock("- plain bullet\n- another bullet");

        Assert.DoesNotContain("type=\"checkbox\"", html);
        Assert.Contains("<li>plain bullet</li>", html);
    }

    [Fact]
    public void Convert_RendersBlockCommentAsVisibleAnnotation()
    {
        var doc = Convert("""
            # Doc

            <!--
            note
            -->

            Body text.
            """);

        Assert.Contains("class=\"md-comment\"", doc.BodyHtml);
        Assert.DoesNotContain("<!--", doc.BodyHtml);
        Assert.DoesNotContain("-->", doc.BodyHtml);
    }

    [Fact]
    public void Convert_RendersInlineCommentWithSurroundingProseIntact()
    {
        var doc = Convert("text <!-- aside --> more text");

        Assert.Contains("class=\"md-comment-inline\"", doc.BodyHtml);
        Assert.Contains("text", doc.BodyHtml);
        Assert.Contains("more text", doc.BodyHtml);
    }

    [Fact]
    public void RenderInline_ConvertsComment()
    {
        var html = MarkdownConverter.RenderInline("Some **bold** <!-- todo --> text");

        Assert.Contains("md-comment-inline", html);
        Assert.DoesNotContain("<!--", html);
    }

    [Fact]
    public void RenderBlock_ConvertsComment()
    {
        var html = MarkdownConverter.RenderBlock("<!--\nnote\n-->\n\nBody.");

        Assert.Contains("md-comment", html);
        Assert.DoesNotContain("<!--", html);
    }

    [Fact]
    public void Convert_UnterminatedCommentDoesNotThrowAndSurroundingTextRenders()
    {
        var doc = Convert("<!-- never closed\n\nMore text.");

        Assert.Contains("More text", doc.BodyHtml);
    }

    [Fact]
    public void Convert_CommentTextIsHtmlEncoded()
    {
        var doc = Convert("<!-- see <Foo> & \"bar\" -->");

        Assert.Contains("&lt;Foo&gt;", doc.BodyHtml);
        Assert.Contains("&amp;", doc.BodyHtml);
        Assert.DoesNotContain("<Foo>", doc.BodyHtml);
    }

    [Fact]
    public void Convert_PlainContentWithNoCommentsHasNoMdComment()
    {
        var doc = Convert("# Heading\n\nJust ordinary prose, no comments here.");

        Assert.DoesNotContain("md-comment", doc.BodyHtml);
    }

    [Fact]
    public void Convert_ShortOverlappingInlineCommentDoesNotThrow()
    {
        var doc = Convert("text <!--> more text");

        Assert.Contains("md-comment-inline", doc.BodyHtml);
        Assert.Contains("more text", doc.BodyHtml);
    }

    [Fact]
    public void Convert_ShortOverlappingBlockCommentDoesNotLeaveStrayMarkerCharacters()
    {
        var doc = Convert("<!-->\n\nBody text.");

        Assert.Contains("<aside class=\"md-comment\"></aside>", doc.BodyHtml);
    }

    [Fact]
    public void RenderInline_CommentOutsideDocBodyStillCarriesMdCommentClass()
    {
        var html = MarkdownConverter.RenderInline("Goal text <!-- sync back later --> continued");

        Assert.Contains("md-comment-inline", html);
    }
}
