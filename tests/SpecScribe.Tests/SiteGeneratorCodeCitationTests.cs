using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.2: source citations on rendered pages resolve to Story 7.1's
/// in-portal code pages with the correct <c>#L{n}</c> anchor (no residual dead <c>../../src/…</c> link), the cited
/// code page carries a relationships block back to every citing artifact (AC #2), the resolution runs on doc pages
/// (proving it lives in the whole-page pass, not the story-only path), and output is deterministic. With an external
/// source base set (Story 7.7) the in-portal pages and citations are UNCHANGED; each page merely gains an additive
/// "view source online" link — the base is additive, never a replacement.</summary>
public class SiteGeneratorCodeCitationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-cite-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string ArtifactsDir => Path.Combine(Source, "implementation-artifacts");
    private string SrcDir => Path.Combine(_root, "src", "Lib");
    private string CodeRoute => "code/src/Lib/Foo.cs.html";
    private string NotesRoute => "implementation-artifacts/notes.html";
    private string OtherRoute => "implementation-artifacts/other.html";

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

        var html = SiteRegion.Read(Site, NotesRoute);
        Assert.Contains("code/src/Lib/Foo.cs.html#L42", html);
        Assert.Contains("code/src/Lib/Foo.cs.html#L15", html);
        // No residual dead view-source link into the raw source tree.
        Assert.DoesNotContain("href=\"../../src/Lib/Foo.cs", html);
    }

    [Fact]
    public void CodePage_HasReferencedByBackToCitingArtifacts()
    {
        Generate();

        var html = SiteRegion.Read(Site, CodeRoute);
        // The relationships block (graph component + accessible list) is the hero of the code page.
        Assert.Contains("code-relationships", html);
        Assert.Contains("data-relgraph", html);
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
        Assert.Contains("code/src/Lib/Foo.cs.html", SiteRegion.Read(Site, OtherRoute));
    }

    [Fact]
    public void Output_IsDeterministicAcrossRuns()
    {
        Generate();
        var first = SiteRegion.Read(Site, NotesRoute);
        var firstCode = SiteRegion.Read(Site, CodeRoute);

        Generate();
        Assert.Equal(first, SiteRegion.Read(Site, NotesRoute));
        Assert.Equal(firstCode, SiteRegion.Read(Site, CodeRoute));
    }

    [Fact]
    public void ExternalBase_IsAdditive_KeepsInPortalCitationsAndAddsViewSourceLink()
    {
        Generate(codeSourceBaseUrl: "https://github.com/IntegerMan/SpecScribe/blob/main");

        // Citations still resolve to the in-portal code pages — the external base never diverts them.
        var notes = SiteRegion.Read(Site, NotesRoute);
        Assert.Contains("code/src/Lib/Foo.cs.html#L42", notes);
        Assert.Contains("code/src/Lib/Foo.cs.html#L15", notes);
        Assert.DoesNotContain("github.com/IntegerMan/SpecScribe/blob/main/src/Lib/Foo.cs#L42", notes);

        // The in-portal page IS generated and carries an additive "view source online" link to the hosted file.
        Assert.True(SiteRegion.Exists(Site, CodeRoute));
        var code = SiteRegion.Read(Site, CodeRoute);
        Assert.Contains("code-external-link", code);
        Assert.Contains("https://github.com/IntegerMan/SpecScribe/blob/main/src/Lib/Foo.cs", code);
        Assert.Contains("View on GitHub", code);
    }
}
