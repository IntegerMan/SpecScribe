using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 3.4: with source *.md artifacts present, a structure.html tree
/// page is produced (nested native &lt;details&gt; disclosure, linking to at least one generated page) and the
/// Structure nav item + quick link appear; with no source files, none of those exist and no broken links are
/// emitted. Follows the temp-dir fixture style of <see cref="SiteGeneratorSprintTests"/>. [Story 3.4]</summary>
public class SiteGeneratorStructureTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-structure-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string StructurePage => Path.Combine(Site, "structure.html");
    private string IndexPage => Path.Combine(Site, "index.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    private const string BriefMd = """
        # Product Brief

        A short brief so the tree has a linkable planning artifact under a nested folder.
        """;

    public SiteGeneratorStructureTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts", "briefs", "brief-x"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "briefs", "brief-x", "brief.md"), BriefMd);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private void GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void GenerateAll_WithSourceArtifacts_ProducesStructurePageWithTreeNavAndQuickLink()
    {
        GenerateSite();

        Assert.True(File.Exists(StructurePage), "structure.html should be generated when source *.md files exist");
        var html = File.ReadAllText(StructurePage);

        // The standard standalone-page shell: single main landmark, nav, breadcrumb.
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"site-nav\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        // The nested native <details> tree, and a link to at least one known generated page (epics.html).
        Assert.Contains("class=\"structure-tree\"", html);
        Assert.Contains("<details", html);
        Assert.Contains("href=\"epics.html\"", html);
        // The collapsed single-child chain reads as one branch label: briefs → brief-x is a pure single-child
        // chain, so it renders as one "briefs / brief-x" branch (planning-artifacts itself has two children —
        // epics.md and briefs — so it is not collapsed).
        Assert.Contains("briefs / brief-x", html);

        // The Structure nav item + dashboard quick link appear and point at the page.
        var index = File.ReadAllText(IndexPage);
        Assert.Contains("href=\"structure.html\"", index);
        Assert.Contains("Explore the project and artifact structure.", index);

        AssertNoBrokenLocalLinks(StructurePage);
        AssertNoBrokenLocalLinks(IndexPage);
    }

    [Fact]
    public void GenerateAll_WithNoSourceArtifacts_OmitsStructurePageAndNav()
    {
        // Remove every source *.md so the source-artifact file set is empty.
        foreach (var md in Directory.EnumerateFiles(Source, "*.md", SearchOption.AllDirectories))
        {
            File.Delete(md);
        }

        GenerateSite();

        Assert.False(File.Exists(StructurePage), "no structure.html without any source *.md files");
        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("href=\"structure.html\"", index);

        AssertNoBrokenLocalLinks(IndexPage);
    }

    /// <summary>Every local (non-anchor, non-scheme) href on the page resolves to a file that was actually
    /// generated — the "never a broken link" guarantee (AC #2, NFR2).</summary>
    private void AssertNoBrokenLocalLinks(string pagePath)
    {
        var html = File.ReadAllText(pagePath);
        var pageDir = Path.GetDirectoryName(pagePath)!;
        foreach (Match m in Regex.Matches(html, "href=\"([^\"]+)\""))
        {
            var raw = m.Groups[1].Value;
            if (raw.StartsWith("#", StringComparison.Ordinal)
                || Regex.IsMatch(raw, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                continue;

            var target = raw.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            var resolved = Path.GetFullPath(Path.Combine(pageDir, target.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(resolved), $"broken link: {raw} → {resolved}");
        }
    }
}
