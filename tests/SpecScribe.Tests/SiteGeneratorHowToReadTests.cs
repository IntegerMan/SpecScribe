using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for the Spec-Driven Development orientation page (how-to-read.html):
/// framework tabs (always rendered, colored by presence), static command lists, Mermaid methodology diagrams,
/// install CTAs for absent frameworks, Coming Soon stubs for planned frameworks, plus the preserved reading
/// order and glossary sections. Follows the temp-dir fixture style of <see cref="SiteGeneratorOutlineTests"/>.</summary>
public class SiteGeneratorHowToReadTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-howtoread-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    // The Requirements Inventory intro sentence is the page's FIRST bare-acronym occurrence (the section
    // renders last in the actual HTML body, but nothing earlier on the page mentions these bare tokens) —
    // it exercises all five BMad Method acronyms in one shot. "### FR Coverage Map" a few lines later gives
    // a natural second bare "FR" to prove later occurrences stay plain.
    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        This project tracks FR and NFR items, each with an AC block, informed by the ADR log and the PRD.

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### FR Coverage Map

        FR1: Epic 1 - rendering

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,url,
        BMad Method,bmad-create-story,Create Story,CS,Prepare the next story,create,,4-implementation,,,true,implementation_artifacts,story
        """;

    private const string GdsCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Game Dev Studio,_meta,,,,,,,,,false,url,
        Game Dev Studio,gds-create-story,Create Story,CS,Prepare the next story,create,,4-implementation,,,true,implementation_artifacts,story
        """;

    public SiteGeneratorHowToReadTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(_root, "README.md"), "# Sample Project\n\nWelcome.\n");
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        // prd.md / ARCHITECTURE-SPINE.md sit at the source root (not nested) so their output paths stay
        // flat ("prd.html"/"ARCHITECTURE-SPINE.html") — module docs are matched by filename anywhere in the
        // source tree, so location doesn't affect detection, only the output path shape asserted below.
        File.WriteAllText(Path.Combine(Source, "prd.md"), "# PRD\n\nProduct requirements.\n");
        File.WriteAllText(Path.Combine(Source, "ARCHITECTURE-SPINE.md"), "# Architecture\n\nThe spine.\n");
        File.WriteAllText(Path.Combine(Adrs, "0001-use-something.md"), "# ADR 0001: Use Something\n\n**Status:** Accepted\n");
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            development_status:
              epic-1: in-progress
              1-1-foundation: in-progress
            """);

        var configDir = Path.Combine(_root, "_bmad", "_config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: bmm\n    version: 6.0.0");
        var bmmDir = Path.Combine(_root, "_bmad", "bmm");
        Directory.CreateDirectory(bmmDir);
        File.WriteAllText(Path.Combine(bmmDir, "module-help.csv"), BmmCsv);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string source, string adrs, string output) => ForgeOptions.Resolve(
        source: source, adrs: adrs, output: output, projectName: "SpecScribe", includeReadme: true);

    [Fact]
    public void GenerateAll_WritesHowToReadOnEveryRun_ReachableFromHomesNav()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();

        var howToReadPath = Path.Combine(Site, "how-to-read.html");
        Assert.True(File.Exists(howToReadPath));

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        var howToReadIdx = index.IndexOf("href=\"how-to-read.html\"", StringComparison.Ordinal);
        var readmeIdx = index.IndexOf("href=\"readme.html\"", StringComparison.Ordinal);
        Assert.True(howToReadIdx >= 0, "Home's nav bar should link to how-to-read.html");
        Assert.True(readmeIdx < 0 || howToReadIdx < readmeIdx, "how-to-read should lead the Project nav group");
    }

    [Fact]
    public void HowToRead_NavAndQuickLinksLabeledSpecDrivenDevelopment()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("<h1>Spec-Driven Development</h1>", html);
        Assert.Contains("Spec-Driven Development", html);
        // The nav bar should carry the new label (not the old one)
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("Spec-Driven Development", index);
        Assert.DoesNotContain("How to read this portal", index);
    }

    [Fact]
    public void HowToRead_MethodPresent_ShowsCommandsAndMermaidDiagram()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Method tab should be present-styled
        Assert.Contains("sdd-badge--present", html);
        Assert.Contains("/bmad-help", html);
        Assert.Contains("/bmad-product-brief", html);
        Assert.Contains("/bmad-prd", html);
        Assert.Contains("/bmad-create-epics-and-stories", html);
        Assert.Contains("/bmad-create-story", html);
        Assert.Contains("/bmad-dev-story", html);
        Assert.Contains("/bmad-code-review", html);
        Assert.Contains("/bmad-retrospective", html);
        // Mermaid diagram block
        Assert.Contains("class=\"mermaid\"", html);
        Assert.Contains("Brief", html);
        Assert.Contains("Retrospective", html);
        // Mermaid init script present because page has diagram
        Assert.Contains("mermaid.esm.min.mjs", html);
    }

    [Fact]
    public void HowToRead_GdsAbsent_ShowsInstallCta()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("npx bmad-method install --modules gds", html);
        Assert.Contains("https://github.com/bmad-code-org/bmad-module-game-dev-studio", html);
        Assert.Contains("BMad Game Dev Studio is not installed", html);
    }

    [Fact]
    public void HowToRead_PlannedFrameworks_ShowComingSoonOnly()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("Spec Kit — Coming Soon", html);
        Assert.Contains("GSD — Coming Soon", html);
        Assert.Contains("GSD-Pi — Coming Soon", html);
        Assert.Contains("Superpowers — Coming Soon", html);
        Assert.Contains("sdd-badge--coming-soon", html);
    }

    [Fact]
    public void HowToRead_AllTabsAlwaysRendered()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("sdd-tab--method", html);
        Assert.Contains("sdd-tab--gds", html);
        Assert.Contains("sdd-tab--speckit", html);
        Assert.Contains("sdd-tab--gsd", html);
        Assert.Contains("sdd-tab--gsd-pi", html);
        Assert.Contains("sdd-tab--superpowers", html);
    }

    [Fact]
    public void HowToRead_ReadingOrder_ListsAvailablePagesInJourney5Sequence()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var full = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Scope to the Reading order <ol> only — the page's own nav bar also links every one of these pages
        // (in nav-group order, not journey order), so searching the whole page would assert the wrong thing.
        var start = full.IndexOf("<h2 id=\"reading-order\">", StringComparison.Ordinal);
        var end = full.IndexOf("</ol>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "reading order section should be present");
        var html = full[start..end];

        var readme = html.IndexOf("href=\"readme.html\"", StringComparison.Ordinal);
        var prd = html.IndexOf("href=\"prd.html\"", StringComparison.Ordinal);
        var arch = html.IndexOf("href=\"ARCHITECTURE-SPINE.html\"", StringComparison.Ordinal);
        var adrs = html.IndexOf("href=\"adrs/index.html\"", StringComparison.Ordinal);
        var epics = html.IndexOf("href=\"epics.html\"", StringComparison.Ordinal);
        var sprint = html.IndexOf("href=\"sprint.html\"", StringComparison.Ordinal);

        Assert.True(readme >= 0 && prd >= 0 && arch >= 0 && adrs >= 0 && epics >= 0 && sprint >= 0,
            "every step of the reading order should be present when all its pages exist");
        Assert.True(readme < prd && prd < arch && arch < adrs && adrs < epics && epics < sprint,
            "reading order must be Readme -> PRD -> Architecture -> ADRs -> Epics -> Sprint");
    }

    [Fact]
    public void HowToRead_ReadingOrder_OmitsStepsForAbsentPages()
    {
        // No sprint-status.yaml, no ADRs, no README this time — a shallow repo gets a shorter, honest list.
        var shallowRoot = Directory.CreateTempSubdirectory("specscribe-howtoread-shallow-").FullName;
        try
        {
            var source = Path.Combine(shallowRoot, "_bmad-output");
            Directory.CreateDirectory(Path.Combine(source, "planning-artifacts"));
            File.WriteAllText(Path.Combine(source, "planning-artifacts", "epics.md"), "# Epics\n\n## Epic List\n\n### Epic 1: Foundation\n\nStand it up.\n");
            var output = Path.Combine(shallowRoot, "site");

            new SiteGenerator(ForgeOptions.Resolve(source: source, output: output, projectName: "SpecScribe", includeReadme: false)).GenerateAll();
            var html = File.ReadAllText(Path.Combine(output, "how-to-read.html"));

            Assert.DoesNotContain("href=\"readme.html\"", html);
            Assert.DoesNotContain("href=\"prd.html\"", html);
            Assert.DoesNotContain("href=\"adrs/index.html\"", html);
            Assert.DoesNotContain("href=\"sprint.html\"", html);
            Assert.Contains("href=\"epics.html\"", html);
        }
        finally
        {
            Directory.Delete(shallowRoot, recursive: true);
        }
    }

    [Fact]
    public void HowToRead_Glossary_ListsBmadMethodTerms_AndOmitsAcronymTitlesFromSharedRendering()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("<h2 id=\"glossary\">Glossary</h2>", html);
        Assert.Contains("<dt>FR</dt><dd>A specific capability the system must provide.</dd>", html);
        Assert.Contains("<dt>NFR</dt>", html);
        Assert.Contains("<dt>AC</dt>", html);
        Assert.Contains("<dt>ADR</dt>", html);
        Assert.Contains("<dt>PRD</dt>", html);
        Assert.Contains("<dt>spec kernel</dt>", html);
        // The page defines the terms — it must not self-expand them into nested <abbr> (would corrupt the dl).
        Assert.DoesNotContain("<abbr", html);
    }

    [Fact]
    public void ContentPage_FirstUseOfEachAcronym_ExpandsToAbbr_LaterUsesStayPlain()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "epics.html"));

        Assert.Contains("<abbr title=\"Functional Requirement\">FR</abbr>", html);
        Assert.Contains("<abbr title=\"Non-Functional Requirement\">NFR</abbr>", html);
        Assert.Contains("<abbr title=\"Acceptance Criterion\">AC</abbr>", html);
        Assert.Contains("<abbr title=\"Architecture Decision Record\">ADR</abbr>", html);
        Assert.Contains("<abbr title=\"Product Requirements Document\">PRD</abbr>", html);

        // Exactly one <abbr> wrap per acronym on the page — the "FR Coverage Map" heading's later bare "FR"
        // stays plain text, not a second wrap.
        Assert.Equal(1, CountOccurrences(html, "<abbr title=\"Functional Requirement\">FR</abbr>"));
        Assert.Contains("FR Coverage Map</h3>", html);
        Assert.DoesNotContain("<abbr title=\"Functional Requirement\">FR</abbr> Coverage Map", html);

        // The numbered FR1 reference is still linked by RequirementLinkifier and never gets a nested <abbr>.
        Assert.Contains("<a class=\"req-ref\" href=\"requirements/fr1.html\">FR1</a>", html);
        Assert.DoesNotContain("<abbr", html.Substring(html.IndexOf("req-ref", StringComparison.Ordinal), 40));
    }

    [Fact]
    public void HowToRead_NoBmadFolder_TabsStillRendered_MethodAndGdsAbsent()
    {
        var undetectedRoot = Directory.CreateTempSubdirectory("specscribe-howtoread-nomod-").FullName;
        try
        {
            var source = Path.Combine(undetectedRoot, "_bmad-output");
            Directory.CreateDirectory(Path.Combine(source, "planning-artifacts"));
            File.WriteAllText(Path.Combine(source, "planning-artifacts", "epics.md"), EpicsMd);
            var output = Path.Combine(undetectedRoot, "site");

            new SiteGenerator(ForgeOptions.Resolve(source: source, output: output, projectName: "SpecScribe", includeReadme: false)).GenerateAll();

            var howToRead = File.ReadAllText(Path.Combine(output, "how-to-read.html"));
            // Tabs always rendered — never omitted.
            Assert.Contains("sdd-tab--method", howToRead);
            Assert.Contains("sdd-tab--gds", howToRead);
            // Both are absent
            Assert.Contains("npx bmad-method install</code>", howToRead);
            Assert.Contains("npx bmad-method install --modules gds", howToRead);
            // Page still exists
            Assert.True(File.Exists(Path.Combine(output, "how-to-read.html")));
            // Planned tabs are Coming Soon
            Assert.Contains("Spec Kit — Coming Soon", howToRead);
        }
        finally
        {
            Directory.Delete(undetectedRoot, recursive: true);
        }
    }

    [Fact]
    public void HowToRead_DualInstall_BothPresent()
    {
        // Install GDS alongside BMM.
        var gdsDir = Path.Combine(_root, "_bmad", "gds");
        Directory.CreateDirectory(gdsDir);
        File.WriteAllText(Path.Combine(gdsDir, "module-help.csv"), GdsCsv);
        // Update manifest.
        File.WriteAllText(Path.Combine(_root, "_bmad", "_config", "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: bmm\n    version: 6.0.0\n  - name: gds\n    version: 6.0.0");

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Both tabs should be present
        Assert.Contains("sdd-tab-state--present", html);
        // Method commands
        Assert.Contains("/bmad-help", html);
        // GDS commands
        Assert.Contains("/bmgd-gdd", html);
        // GDS methodology diagram nodes
        Assert.Contains("GDD", html);
        Assert.Contains("Narrative Design", html);
        Assert.Contains("Prototype", html);
        // No install CTAs for either
        Assert.DoesNotContain("BMad Method is not installed", html);
        Assert.DoesNotContain("BMad Game Dev Studio is not installed", html);
    }

    [Fact]
    public void HowToRead_DefaultTabIsMethod_WhenMethodPresent()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Method radio should be checked
        var methodTabSection = html.Substring(
            html.IndexOf("sdd-tab--method", StringComparison.Ordinal),
            200);
        Assert.Contains("checked", methodTabSection);
    }

    [Fact]
    public void HowToRead_DefaultTabIsGds_WhenOnlyGdsPresent()
    {
        // Remove BMM, install only GDS.
        Directory.Delete(Path.Combine(_root, "_bmad", "bmm"), recursive: true);
        var gdsDir = Path.Combine(_root, "_bmad", "gds");
        Directory.CreateDirectory(gdsDir);
        File.WriteAllText(Path.Combine(gdsDir, "module-help.csv"), GdsCsv);
        File.WriteAllText(Path.Combine(_root, "_bmad", "_config", "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: gds\n    version: 6.0.0");

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // GDS radio should be checked as default (Method absent, GDS present)
        var gdsTabSection = html.Substring(
            html.IndexOf("sdd-tab--gds", StringComparison.Ordinal),
            200);
        Assert.Contains("checked", gdsTabSection);
    }

    [Fact]
    public void HowToRead_MermaidInitScriptOmittedWhenNoDiagram()
    {
        // Render with both absent — no Mermaid diagram rendered.
        var nav = SiteNav.Build(Array.Empty<string>(), "Empty Project");
        var html = HowToReadTemplater.RenderPage(
            nav, Array.Empty<ModuleDoc>(), Array.Empty<GlossaryTerm>(), CommandCatalog.Empty,
            methodPresent: false, gdsPresent: false);

        Assert.DoesNotContain("mermaid.esm.min.mjs", html);
        Assert.DoesNotContain("class=\"mermaid\"", html);
    }

    [Fact]
    public void HowToRead_BypassesApplyReferenceLinks()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Must not contain <abbr> tags (page defines the glossary, mustn't self-expand).
        Assert.DoesNotContain("<abbr", html);
    }

    [Fact]
    public void HowToRead_KeyboardAccessibleTabs()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Radio inputs are keyboard-focusable (native radio group navigation).
        Assert.Contains("type=\"radio\"", html);
        Assert.Contains("name=\"sdd-framework\"", html);
        // Presence not conveyed by color alone — badge text states Present/Absent/Coming Soon.
        Assert.Contains(">Present</span>", html);
        Assert.Contains(">Absent</span>", html);
        Assert.Contains(">Coming Soon</span>", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
