using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Help orientation pages: <c>how-to-read.html</c> (How to use SpecScribe —
/// reading order + glossary only) and the About Spec-Driven Development hub + framework sub-pages. Follows the
/// temp-dir fixture style of <see cref="SiteGeneratorOutlineTests"/>.</summary>
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
        Assert.Contains("href=\"how-to-read.html\"", index);
        Assert.Contains("Help", index);
        Assert.Contains("href=\"about-sdd.html\"", index);
        Assert.Contains("href=\"about.html\"", index);
        Assert.Contains("href=\"diagnostics.html\"", index);
    }

    [Fact]
    public void HowToRead_NavAndH1LabeledHowToUseSpecScribe()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        Assert.Contains("<h1>How to use SpecScribe</h1>", html);
        Assert.DoesNotContain("sdd-tab", html);
        Assert.DoesNotContain("class=\"mermaid\"", html);

        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("How to use SpecScribe", index);
        Assert.Contains("About Spec-Driven Development", index);
        Assert.DoesNotContain("How to read this portal", index);
    }

    [Fact]
    public void GenerateAll_WritesAboutSddHubAndFrameworkPages()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();

        Assert.True(File.Exists(Path.Combine(Site, "about-sdd.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-bmad.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-gds.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-speckit.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-gsd.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-gsd-pi.html")));
        Assert.True(File.Exists(Path.Combine(Site, "about-sdd-superpowers.html")));
    }

    [Fact]
    public void AboutSdd_Hub_ShowsSupportMatrix()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "about-sdd.html"));

        Assert.Contains("<h1>About Spec-Driven Development</h1>", html);
        Assert.Contains("sdd-support-matrix", html);
        Assert.Contains("id=\"support-matrix\"", html);
        Assert.Contains("href=\"about-sdd-bmad.html\"", html);
        Assert.Contains("href=\"about-sdd-gds.html\"", html);
        Assert.Contains("href=\"about-sdd-speckit.html\"", html);
    }

    [Fact]
    public void AboutSdd_BmadPresent_ShowsDetectedChip()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var hub = File.ReadAllText(Path.Combine(Site, "about-sdd.html"));
        var bmad = File.ReadAllText(Path.Combine(Site, "about-sdd-bmad.html"));

        Assert.Contains("sdd-detected", hub);
        Assert.Contains(">Detected<", hub);
        Assert.Contains("sdd-support-yes", hub);
        Assert.Contains(">Supported<", hub);
        Assert.Contains(">Detected<", bmad);
        Assert.DoesNotContain("In this project</th>", hub);
        Assert.Contains("Epics &amp; Stories", hub);
        Assert.Contains("Requirements", hub);
        Assert.Contains(">Sprint<", hub);
        Assert.Contains(">Retros<", hub);
        Assert.Contains("Planning docs", hub);
        Assert.Contains(">Commands<", hub);
        Assert.Contains("/bmad-help", bmad);
        Assert.Contains("/bmad-product-brief", bmad);
        Assert.Contains("/bmad-prd", bmad);
        Assert.Contains("/bmad-create-epics-and-stories", bmad);
        Assert.Contains("/bmad-create-story", bmad);
        Assert.Contains("/bmad-dev-story", bmad);
        Assert.Contains("/bmad-code-review", bmad);
        Assert.Contains("/bmad-correct-course", bmad);
        Assert.Contains("/bmad-retrospective", bmad);
        Assert.Contains("class=\"mermaid\"", bmad);
        Assert.Contains("stateDiagram-v2", bmad);
        Assert.Contains("Product Brief Created", bmad);
        Assert.Contains("In a Sprint", bmad);
        Assert.Contains("the official documentation", bmad);
        Assert.Contains("mermaid.esm.min.mjs", bmad);
        Assert.DoesNotContain("BMad is not detected", bmad);
    }

    [Fact]
    public void AboutSdd_GdsAbsent_ShowsInstallCta()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "about-sdd-gds.html"));

        Assert.Contains("npx bmad-method install --modules gds", html);
        Assert.Contains("https://github.com/bmad-code-org/bmad-module-game-dev-studio", html);
        Assert.Contains("the official documentation", html);
        Assert.Contains("BMad GDS is not detected", html);
        Assert.Contains("class=\"mermaid\"", html);
        Assert.Contains("stateDiagram-v2", html);
    }

    [Fact]
    public void AboutSdd_SpecKit_ShowsComingSoon()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var hub = File.ReadAllText(Path.Combine(Site, "about-sdd.html"));
        var speckit = File.ReadAllText(Path.Combine(Site, "about-sdd-speckit.html"));

        Assert.Contains("Coming soon", hub);
        Assert.Contains("Coming soon", speckit);
        Assert.Contains("Spec Kit", speckit);
        Assert.DoesNotContain("class=\"mermaid\"", speckit);
        Assert.DoesNotContain("mermaid.esm.min.mjs", speckit);
    }

    [Fact]
    public void AboutSdd_LocalContextWhiteBar_LinksOverviewAndFrameworks()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var hub = File.ReadAllText(Path.Combine(Site, "about-sdd.html"));
        var bmad = File.ReadAllText(Path.Combine(Site, "about-sdd-bmad.html"));

        // Hub: Overview is the active pill (span, not a self-link); frameworks are links. Overview carries the
        // same Icons.ForConcept glyph the dark-bar Insights dropdown shows for this label (Story 10.10; icon:
        // Story 7.12 review); the framework labels are uncurated so they render no icon.
        Assert.Contains("site-nav-local-context", hub);
        Assert.Contains($"local-context-pill active\" aria-current=\"page\">{Icons.ForConcept("Overview")}Overview</span>", hub);
        Assert.Contains("href=\"about-sdd-bmad.html\" class=\"local-context-pill\">BMad</a>", hub);
        Assert.Contains("href=\"about-sdd-gds.html\" class=\"local-context-pill\">BMad GDS</a>", hub);
        Assert.Contains("href=\"about-sdd-speckit.html\" class=\"local-context-pill\">Spec Kit</a>", hub);
        Assert.Contains("href=\"about-sdd-gsd.html\" class=\"local-context-pill\">GSD</a>", hub);
        Assert.Contains("href=\"about-sdd-gsd-pi.html\" class=\"local-context-pill\">GSD-Pi</a>", hub);
        Assert.Contains("href=\"about-sdd-superpowers.html\" class=\"local-context-pill\">Superpowers</a>", hub);

        // Framework page: Overview is a link back to the hub; BMad is the active pill.
        Assert.Contains($"href=\"about-sdd.html\" class=\"local-context-pill\">{Icons.ForConcept("Overview")}Overview</a>", bmad);
        Assert.Contains("local-context-pill active\" aria-current=\"page\">BMad</span>", bmad);
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
    public void AboutSdd_NoBmadFolder_StillWritesHub_FrameworksAbsent()
    {
        var undetectedRoot = Directory.CreateTempSubdirectory("specscribe-howtoread-nomod-").FullName;
        try
        {
            var source = Path.Combine(undetectedRoot, "_bmad-output");
            Directory.CreateDirectory(Path.Combine(source, "planning-artifacts"));
            File.WriteAllText(Path.Combine(source, "planning-artifacts", "epics.md"), EpicsMd);
            var output = Path.Combine(undetectedRoot, "site");

            new SiteGenerator(ForgeOptions.Resolve(source: source, output: output, projectName: "SpecScribe", includeReadme: false)).GenerateAll();

            Assert.True(File.Exists(Path.Combine(output, "how-to-read.html")));
            Assert.True(File.Exists(Path.Combine(output, "about-sdd.html")));
            Assert.True(File.Exists(Path.Combine(output, "about-sdd-bmad.html")));

            var howToRead = File.ReadAllText(Path.Combine(output, "how-to-read.html"));
            Assert.DoesNotContain("sdd-tab", howToRead);
            Assert.Contains("<h1>How to use SpecScribe</h1>", howToRead);

            var bmad = File.ReadAllText(Path.Combine(output, "about-sdd-bmad.html"));
            Assert.Contains("npx bmad-method install</code>", bmad);
            Assert.Contains("BMad is not detected", bmad);

            var gds = File.ReadAllText(Path.Combine(output, "about-sdd-gds.html"));
            Assert.Contains("npx bmad-method install --modules gds", gds);

            var hub = File.ReadAllText(Path.Combine(output, "about-sdd.html"));
            Assert.Contains("Coming soon", hub);
            Assert.DoesNotContain("sdd-detected", hub);
        }
        finally
        {
            Directory.Delete(undetectedRoot, recursive: true);
        }
    }

    [Fact]
    public void AboutSdd_DualInstall_BothPresent()
    {
        // Install GDS alongside BMM.
        var gdsDir = Path.Combine(_root, "_bmad", "gds");
        Directory.CreateDirectory(gdsDir);
        File.WriteAllText(Path.Combine(gdsDir, "module-help.csv"), GdsCsv);
        File.WriteAllText(Path.Combine(_root, "_bmad", "_config", "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: bmm\n    version: 6.0.0\n  - name: gds\n    version: 6.0.0");

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var hub = File.ReadAllText(Path.Combine(Site, "about-sdd.html"));
        var bmad = File.ReadAllText(Path.Combine(Site, "about-sdd-bmad.html"));
        var gds = File.ReadAllText(Path.Combine(Site, "about-sdd-gds.html"));

        Assert.Contains("sdd-detected", hub);
        Assert.Contains(">Detected<", hub);
        Assert.Contains("/bmad-help", bmad);
        Assert.Contains("/bmgd-gdd", gds);
        Assert.Contains("GDD", gds);
        Assert.Contains("Narrative Design", gds);
        Assert.DoesNotContain("BMad is not detected", bmad);
        Assert.DoesNotContain("BMad GDS is not detected", gds);
    }

    [Fact]
    public void HowToRead_BypassesApplyReferenceLinks()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = File.ReadAllText(Path.Combine(Site, "how-to-read.html"));

        // Must not contain <abbr> tags (page defines the glossary, mustn't self-expand).
        Assert.DoesNotContain("<abbr", html);
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
