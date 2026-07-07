using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 2.2: the spec kernel (specs/**) renders as a labeled
/// first-class band with a kernel quick-link, and frontmatter companions:/sources: resolve to real generated
/// pages (resolve-or-omit — a listed-but-absent target never emits a broken link).</summary>
public class SiteGeneratorSpecKernelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-spec-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string HomeIndex => Path.Combine(Site, "index.html");
    private string SpecPage => Path.Combine(Site, "specs", "spec-x", "SPEC.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: First

        As a contributor, I want a page.
        """;

    // SPEC.md declares two resolvable companions (requirements-catalog.md, settings-and-signals.md), one
    // MISSING companion (does-not-exist.md), and a resolvable source (../../planning-artifacts/prd.md).
    private const string SpecMd = """
        ---
        id: SPEC-x
        companions:
          - requirements-catalog.md
          - settings-and-signals.md
          - does-not-exist.md
        sources:
          - ../../planning-artifacts/prd.md
        ---

        # SpecScribe

        ## Why

        Canonical contract body.
        """;

    public SiteGeneratorSpecKernelTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "specs", "spec-x"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nProduct requirements.\n");
        File.WriteAllText(Path.Combine(Source, "specs", "spec-x", "SPEC.md"), SpecMd);
        File.WriteAllText(Path.Combine(Source, "specs", "spec-x", "requirements-catalog.md"), "# Requirements Catalog\n\nDetail.\n");
        File.WriteAllText(Path.Combine(Source, "specs", "spec-x", "settings-and-signals.md"), "# Settings and Signals\n\nDetail.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source,
        adrs: Adrs,
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false);

    private SiteGenerator GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void GenerateAll_SurfacesSpecKernelSectionAndQuickLinkOnHome()
    {
        GenerateSite();

        var index = File.ReadAllText(HomeIndex);
        // Labeled band + clear SPEC card title, out of "Other".
        Assert.Contains("<div class=\"index-section-title\">Spec Kernel</div>", index);
        Assert.Contains(">SPEC — Canonical Contract</h2>", index);
        Assert.Contains("href=\"specs/spec-x/SPEC.html\"", index);
        // The kernel quick-link is present in the dashboard's Explore Key Views pills.
        Assert.Contains(">Spec Kernel</a>", index);
    }

    [Fact]
    public void GenerateAll_ResolvesCompanionCrossLinks_AndOmitsAbsentTargets()
    {
        GenerateSite();

        var spec = File.ReadAllText(SpecPage);
        Assert.Contains("class=\"companion-docs\"", spec);
        // Present companions resolve to their generated pages (SPEC.html sits two dirs deep → ../../).
        Assert.Contains("href=\"../../specs/spec-x/requirements-catalog.html\"", spec);
        Assert.Contains("href=\"../../specs/spec-x/settings-and-signals.html\"", spec);
        // The resolvable source (prd.md) resolves too.
        Assert.Contains("href=\"../../planning-artifacts/prd.html\"", spec);
        // The listed-but-absent companion emits NO link (resolve-or-omit; never a broken link). [AC #2 / NFR2]
        Assert.DoesNotContain("does-not-exist", spec);
    }

    [Fact]
    public void GenerateAll_MissingSpecFolderDegradesGracefully()
    {
        // No specs/ content at all → no section, no quick-link, no broken nav.
        Directory.Delete(Path.Combine(Source, "specs"), recursive: true);
        GenerateSite();

        var index = File.ReadAllText(HomeIndex);
        Assert.DoesNotContain("Spec Kernel", index);
    }

    [Fact]
    public void GenerateAll_SpecPageStillRendersTocForItsHeadings()
    {
        GenerateSite();

        // Standalone spec pages keep the shared "On this page" TOC sidebar (verify, don't rebuild — Task 5).
        var spec = File.ReadAllText(SpecPage);
        Assert.Contains("toc-sidebar", spec);
        Assert.Contains(">Why</a>", spec);
    }
}
