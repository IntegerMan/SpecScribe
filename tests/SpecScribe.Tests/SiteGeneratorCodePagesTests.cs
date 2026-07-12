using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.1 in-portal code file pages: referenced source files render as
/// line-numbered pages with stable <c>L{n}</c> anchors, non-referenced files are omitted, binary/oversized/traversal
/// candidates degrade non-fatally, a removed citation self-heals on the next full pass, external-link mode skips the
/// pages entirely, and output is deterministic.</summary>
public class SiteGeneratorCodePagesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-code-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string CodeDir => Path.Combine(Site, "code");
    private string ArtifactsDir => Path.Combine(Source, "implementation-artifacts");
    private string SrcDir => Path.Combine(_root, "src", "Lib");

    // Referenced by an inline citation; the page must be generated.
    private string ReferencedPage => Path.Combine(CodeDir, "src", "Lib", "Referenced.cs.html");
    // Referenced by a markdown-link citation (relative to the artifact dir).
    private string LinkedPage => Path.Combine(CodeDir, "src", "Lib", "Linked.cs.html");
    // Exists on disk but is never cited; must NOT get a page.
    private string UnreferencedPage => Path.Combine(CodeDir, "src", "Lib", "Unreferenced.cs.html");

    public SiteGeneratorCodePagesTests()
    {
        Directory.CreateDirectory(ArtifactsDir);
        Directory.CreateDirectory(SrcDir);

        File.WriteAllText(Path.Combine(SrcDir, "Referenced.cs"),
            "namespace Lib;\npublic class Referenced { } // marker-token\n");
        File.WriteAllText(Path.Combine(SrcDir, "Linked.cs"), "namespace Lib;\npublic class Linked { }\n");
        File.WriteAllText(Path.Combine(SrcDir, "Unreferenced.cs"), "namespace Lib;\npublic class Unreferenced { }\n");

        WriteArtifact(Referenced: true);
    }

    /// <summary>Writes the citing artifact. When <paramref name="Referenced"/> is false the citations are dropped so
    /// the stale-output regression can prove the pages disappear on the next pass.</summary>
    private void WriteArtifact(bool Referenced)
    {
        var body = Referenced
            ? """
              # Notes

              Inline citation [Source: `src/Lib/Referenced.cs:2`].
              Markdown-link citation [Source: [Linked.cs](../../src/Lib/Linked.cs)].
              """
            : "# Notes\n\nNo citations here.\n";
        File.WriteAllText(Path.Combine(ArtifactsDir, "1-1-notes.md"), body);
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

    private SiteGenerator Generate(string? codeSourceBaseUrl = null)
    {
        var gen = new SiteGenerator(Options(codeSourceBaseUrl));
        AssertNoErrors(gen.GenerateAll());
        return gen;
    }

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events) =>
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

    [Fact]
    public void GenerateAll_RendersReferencedFileWithLineAnchors()
    {
        Generate();

        Assert.True(File.Exists(ReferencedPage));
        var html = File.ReadAllText(ReferencedPage);
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("public class Referenced", html);
        // The page carries the code-file surface class.
        Assert.Contains("class=\"code-file\"", html);
    }

    [Fact]
    public void GenerateAll_ResolvesMarkdownLinkCitationRelativeToArtifactDir()
    {
        Generate();

        Assert.True(File.Exists(LinkedPage));
        Assert.Contains("public class Linked", File.ReadAllText(LinkedPage));
    }

    [Fact]
    public void GenerateAll_OmitsNonReferencedFile()
    {
        Generate();

        Assert.False(File.Exists(UnreferencedPage));
    }

    [Fact]
    public void GenerateAll_DegradesBinaryFileNonFatally()
    {
        // A referenced binary file (embedded NUL) must not throw; a placeholder page is emitted instead.
        File.WriteAllBytes(Path.Combine(SrcDir, "Blob.bin"), new byte[] { 0x1, 0x2, 0x0, 0x3, 0x4 });
        File.AppendAllText(Path.Combine(ArtifactsDir, "1-1-notes.md"), "\n[Source: `src/Lib/Blob.bin`]\n");

        Generate();

        var page = Path.Combine(CodeDir, "src", "Lib", "Blob.bin.html");
        Assert.True(File.Exists(page));
        var html = File.ReadAllText(page);
        Assert.Contains("code-placeholder", html);
        Assert.DoesNotContain("class=\"code-line\"", html);
    }

    [Fact]
    public void GenerateAll_DegradesOversizedFileNonFatally()
    {
        // A referenced file above the size cap (~1 MB) degrades to a placeholder rather than being rendered.
        File.WriteAllText(Path.Combine(SrcDir, "Big.cs"), new string('a', 1_100_000));
        File.AppendAllText(Path.Combine(ArtifactsDir, "1-1-notes.md"), "\n[Source: `src/Lib/Big.cs`]\n");

        Generate();

        var page = Path.Combine(CodeDir, "src", "Lib", "Big.cs.html");
        Assert.True(File.Exists(page));
        var html = File.ReadAllText(page);
        Assert.Contains("code-placeholder", html);
        Assert.DoesNotContain("class=\"code-line\"", html);
    }

    [Fact]
    public void GenerateAll_RejectsPathTraversalCitationWithoutPageOrLeak()
    {
        // A citation resolving OUTSIDE the repo root (parent of _root) must produce no page, no error, and write
        // nothing outside the output root.
        var secretName = $"specscribe-secret-{Guid.NewGuid():N}.txt";
        var secretFull = Path.Combine(Directory.GetParent(_root)!.FullName, secretName);
        File.WriteAllText(secretFull, "TOP SECRET");
        try
        {
            File.AppendAllText(Path.Combine(ArtifactsDir, "1-1-notes.md"),
                $"\n[Source: [secret](../../../{secretName})]\n");

            Generate();

            // No page anywhere under code/ for the secret file.
            if (Directory.Exists(CodeDir))
            {
                Assert.DoesNotContain(
                    Directory.GetFiles(CodeDir, "*", SearchOption.AllDirectories),
                    f => f.Contains(secretName, StringComparison.OrdinalIgnoreCase));
            }
            // The secret file itself is untouched and was never copied into the site.
            Assert.False(File.Exists(Path.Combine(Site, secretName)));
            Assert.Equal("TOP SECRET", File.ReadAllText(secretFull));
        }
        finally
        {
            File.Delete(secretFull);
        }
    }

    [Fact]
    public void GenerateAll_RemovesStaleCodePage_WhenCitationRemoved()
    {
        var gen = new SiteGenerator(Options());
        AssertNoErrors(gen.GenerateAll());
        Assert.True(File.Exists(ReferencedPage));

        // Drop the citations and regenerate — the full rebuild wipes the code/ tree, so no stale page survives.
        WriteArtifact(Referenced: false);
        AssertNoErrors(gen.GenerateAll());

        Assert.False(File.Exists(ReferencedPage));
        Assert.False(File.Exists(LinkedPage));
    }

    [Fact]
    public void GenerateAll_ExternalBase_StillGeneratesPages_AndAddsViewSourceLink()
    {
        Generate(codeSourceBaseUrl: "https://github.com/owner/repo/blob/main");

        // The external base is additive (Story 7.7): in-portal code pages are still generated…
        Assert.True(File.Exists(ReferencedPage));
        Assert.True(File.Exists(LinkedPage));
        // …and each carries an additive "view source online" link to the hosted file (whole-file, no #L anchor).
        var html = File.ReadAllText(ReferencedPage);
        Assert.Contains("code-external-link", html);
        Assert.Contains("https://github.com/owner/repo/blob/main/src/Lib/Referenced.cs", html);
        // The rest of the site still generates.
        Assert.True(File.Exists(Path.Combine(Site, "index.html")));
    }

    [Fact]
    public void GenerateAll_IsDeterministicForCodePageBody()
    {
        var first = new SiteGenerator(Options());
        AssertNoErrors(first.GenerateAll());
        var firstBody = CodeBody(File.ReadAllText(ReferencedPage));

        // A fresh generator over the same input must produce the identical code region (the footer timestamp aside).
        var secondSite = Path.Combine(_root, "site2");
        var second = new SiteGenerator(ForgeOptions.Resolve(source: Source, output: secondSite, projectName: "SpecScribe", includeReadme: false));
        AssertNoErrors(second.GenerateAll());
        var secondBody = CodeBody(File.ReadAllText(Path.Combine(secondSite, "code", "src", "Lib", "Referenced.cs.html")));

        Assert.Equal(firstBody, secondBody);
    }

    private static string CodeBody(string html)
    {
        const string open = "<pre class=\"code-file\">";
        const string close = "</pre>";
        var start = html.IndexOf(open, StringComparison.Ordinal);
        var end = html.IndexOf(close, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "code-file block must be present");
        return html.Substring(start, end - start);
    }
}
