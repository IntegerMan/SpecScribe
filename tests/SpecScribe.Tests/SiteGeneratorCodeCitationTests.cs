using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.2: source citations on rendered pages resolve to Story 7.1's
/// in-portal code pages with the correct <c>#L{n}</c> anchor (no residual dead <c>../../src/…</c> link), the cited
/// code page carries a "Referenced by" block back to every citing artifact (AC #2), the resolution runs on doc pages
/// (proving it lives in the whole-page pass, not the story-only path), and output is deterministic. External-link
/// mode turns the same citations into <c>{base}/&lt;path&gt;#L{n}</c> GitHub links with no <c>code/</c> pages.</summary>
public class SiteGeneratorCodeCitationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-cite-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string ArtifactsDir => Path.Combine(Source, "implementation-artifacts");
    private string SrcDir => Path.Combine(_root, "src", "Lib");
    private string CodePage => Path.Combine(Site, "code", "src", "Lib", "Foo.cs.html");
    private string NotesPage => Path.Combine(Site, "implementation-artifacts", "notes.html");
    private string OtherPage => Path.Combine(Site, "implementation-artifacts", "other.html");

    public SiteGeneratorCodeCitationTests()
    {
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(ArtifactsDir);
        Directory.CreateDirectory(SrcDir);

        // A code file with enough lines that the cited line 42 is real.
        var lines = Enumerable.Range(1, 60).Select(i => $"var line{i} = {i};");
        File.WriteAllText(Path.Combine(SrcDir, "Foo.cs"), "namespace Lib;\n" + string.Join("\n", lines) + "\n");

        // A doc (two levels deep, like a real implementation artifact) that cites the code file via BOTH shapes:
        // a markdown-link view-source citation (line in href) and an inline code-span citation.
        File.WriteAllText(Path.Combine(ArtifactsDir, "notes.md"),
            """
            # Engineering Notes

            The core lives at [Source: [Foo.cs:42](../../src/Lib/Foo.cs:42)].
            Also see the guard [Source: `src/Lib/Foo.cs:15`].
            """);

        // A SECOND doc citing the same file — proves cross-page resolution + a multi-entry "Referenced by".
        File.WriteAllText(Path.Combine(ArtifactsDir, "other.md"),
            """
            # Other Doc

            Related work in [Source: [Foo.cs](../../src/Lib/Foo.cs)].
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string? codeSourceBaseUrl = null) => ForgeOptions.Resolve(
        source: Source,
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false,
        codeSourceBaseUrl: codeSourceBaseUrl);

    private void Generate(string? codeSourceBaseUrl = null)
    {
        var gen = new SiteGenerator(Options(codeSourceBaseUrl));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void Citation_ResolvesToCodePageWithLineAnchor_AndNoDeadLink()
    {
        Generate();

        var html = File.ReadAllText(NotesPage);
        Assert.Contains("code/src/Lib/Foo.cs.html#L42", html);
        Assert.Contains("code/src/Lib/Foo.cs.html#L15", html);
        // No residual dead view-source link into the raw source tree.
        Assert.DoesNotContain("href=\"../../src/Lib/Foo.cs", html);
    }

    [Fact]
    public void CodePage_HasReferencedByBackToCitingArtifacts()
    {
        Generate();

        var html = File.ReadAllText(CodePage);
        Assert.Contains("code-referenced-by", html);
        // Both citing docs are listed with meaningful (title) link text back to their pages.
        Assert.Contains("Engineering Notes", html);
        Assert.Contains("Other Doc", html);
        Assert.Contains("notes.html", html);
        Assert.Contains("other.html", html);
    }

    [Fact]
    public void Citation_ResolvesOnASecondDocPageToo()
    {
        Generate();

        // Proves the resolver runs in the whole-page pass, not a story-only block.
        Assert.Contains("code/src/Lib/Foo.cs.html", File.ReadAllText(OtherPage));
    }

    [Fact]
    public void Output_IsDeterministicAcrossRuns()
    {
        Generate();
        var first = File.ReadAllText(NotesPage);
        var firstCode = File.ReadAllText(CodePage);

        Generate();
        Assert.Equal(first, File.ReadAllText(NotesPage));
        Assert.Equal(firstCode, File.ReadAllText(CodePage));
    }

    [Fact]
    public void ExternalMode_CitationsBecomeGitHubLinks_AndNoCodePages()
    {
        Generate(codeSourceBaseUrl: "https://github.com/IntegerMan/SpecScribe/blob/main");

        var html = File.ReadAllText(NotesPage);
        Assert.Contains("https://github.com/IntegerMan/SpecScribe/blob/main/src/Lib/Foo.cs#L42", html);
        Assert.Contains("https://github.com/IntegerMan/SpecScribe/blob/main/src/Lib/Foo.cs#L15", html);
        // No in-portal code page is generated in external mode.
        Assert.False(File.Exists(CodePage));
        Assert.False(Directory.Exists(Path.Combine(Site, "code")));
    }
}
