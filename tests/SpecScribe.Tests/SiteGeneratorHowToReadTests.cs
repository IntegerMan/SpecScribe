using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Help orientation pages: <c>how-to-read.html</c> (How to use SpecScribe —
/// reading order, CLI generate/watch guidance, glossary) and the About Spec-Driven Development hub + framework
/// sub-pages. Follows the temp-dir fixture style of <see cref="SiteGeneratorOutlineTests"/>.</summary>
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

    // Verbatim upstream rows, pinned exactly as in ModuleContextTests — see the provenance block there for
    // repositories and commit SHAs. This fixture was the last synthetic one; AC #3 requires BMad Method's
    // surfaces to be verified against REAL catalog content, not invented rows. [Review][Patch P4]
    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,https://docs.bmad-method.org/llms.txt,
        BMad Method,bmad-create-story,Create Story,CS,Story cycle start: Prepare first found story in the sprint plan that is next or a specific epic/story designation.,create,,4-implementation,bmad-sprint-planning,bmad-create-story:validate,true,implementation_artifacts,story
        BMad Method,bmad-dev-story,Dev Story,DS,Story cycle: Execute story implementation tasks and tests then CR then back to DS if fixes needed.,,,4-implementation,bmad-create-story:validate,,true,,
        """;

    // Verbatim upstream rows, pinned exactly as in ModuleContextTests — see the provenance block there for
    // repositories and commit SHAs. A synthetic prefix here would let a prefix-keyed identity regression pass
    // unnoticed. [Story 18.2 Task 6; ADR 0015 Decision 7]
    private const string GdsCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Game Dev Studio,_meta,,,,,,,,,false,https://game-dev-studio-docs.bmad-method.org/llms.txt,
        Game Dev Studio,gds-create-story,Create Story,CS,Create Story with comprehensive context for developer agent implementation.,,,4-production,gds-sprint-planning,,true,implementation_artifacts,story
        """;

    /// <summary>Test Architect's real <c>module-help.csv</c>: every skill is <c>bmad-*</c> prefixed, so this is
    /// the fixture that used to be misidentified as BMad Method and served BMM's whole glossary. [Story 18.2]</summary>
    private const string TeaCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Test Architecture Enterprise,_meta,,,,,,,,,false,https://bmad-code-org.github.io/bmad-method-test-architecture-enterprise/llms.txt,
        Test Architecture Enterprise,bmad-testarch-trace,Traceability,TR,Coverage traceability and gate,,,4-implementation,bmad-testarch-test-review,,false,test_artifacts,traceability matrix|gate decision
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

        var howToReadRoute = "how-to-read.html";
        Assert.True(SiteRegion.Exists(Site, howToReadRoute));

        var index = SiteRegion.Read(Site, "index.html");
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
        var html = SiteRegion.Read(Site, "how-to-read.html");

        Assert.Contains("<h1>How to use SpecScribe</h1>", html);
        Assert.DoesNotContain("sdd-tab", html);
        Assert.DoesNotContain("class=\"mermaid\"", html);

        var index = SiteRegion.Read(Site, "index.html");
        Assert.Contains("How to use SpecScribe", index);
        Assert.Contains("About Spec-Driven Development", index);
        Assert.DoesNotContain("How to read this portal", index);
    }

    [Fact]
    public void GenerateAll_WritesAboutSddHubAndFrameworkPages()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();

        Assert.True(SiteRegion.Exists(Site, "about-sdd.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-bmad.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-gds.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-speckit.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-gsd.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-gsd-pi.html"));
        Assert.True(SiteRegion.Exists(Site, "about-sdd-superpowers.html"));
    }

    [Fact]
    public void AboutSdd_Hub_ShowsSupportMatrix()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "about-sdd.html");

        Assert.Contains("<h1>About Spec-Driven Development</h1>", html);
        Assert.Contains("sdd-support-matrix", html);
        Assert.Contains("id=\"support-matrix\"", html);
        Assert.Contains(">Version<", html);
        Assert.Contains(">1.42.3<", html);
        Assert.Contains("href=\"about-sdd-bmad.html\"", html);
        Assert.Contains("href=\"about-sdd-gds.html\"", html);
        Assert.Contains("href=\"about-sdd-speckit.html\"", html);
    }

    [Fact]
    public void AboutSdd_BmadPresent_ShowsDetectedChip()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var hub = SiteRegion.Read(Site, "about-sdd.html");
        var bmad = SiteRegion.Read(Site, "about-sdd-bmad.html");

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
        // [Story 23.6 AC #8] The init module is site-level chrome; this page gets it because its section carries
        // the mermaid block asserted just above. See `web/test/chrome-needs.test.ts` for the per-page half.
        Assert.Contains("mermaid.esm.min.mjs", SiteRegion.Chrome(Site).MermaidInitScript);
        Assert.DoesNotContain("BMad is not detected", bmad);
    }

    [Fact]
    public void AboutSdd_GdsAbsent_ShowsInstallCta()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "about-sdd-gds.html");

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
        var hub = SiteRegion.Read(Site, "about-sdd.html");
        var speckit = SiteRegion.Read(Site, "about-sdd-speckit.html");

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
        var hub = SiteRegion.Read(Site, "about-sdd.html");
        var bmad = SiteRegion.Read(Site, "about-sdd-bmad.html");

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
        var full = SiteRegion.Read(Site, "how-to-read.html");

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
            var html = SiteRegion.Read(output, "how-to-read.html");

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
        var html = SiteRegion.Read(Site, "how-to-read.html");

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
        var html = SiteRegion.Read(Site, "epics.html");

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

            Assert.True(SiteRegion.Exists(output, "how-to-read.html"));
            Assert.True(SiteRegion.Exists(output, "about-sdd.html"));
            Assert.True(SiteRegion.Exists(output, "about-sdd-bmad.html"));

            var howToRead = SiteRegion.Read(output, "how-to-read.html");
            Assert.DoesNotContain("sdd-tab", howToRead);
            Assert.Contains("<h1>How to use SpecScribe</h1>", howToRead);

            var bmad = SiteRegion.Read(output, "about-sdd-bmad.html");
            Assert.Contains("npx bmad-method install</code>", bmad);
            Assert.Contains("BMad is not detected", bmad);

            var gds = SiteRegion.Read(output, "about-sdd-gds.html");
            Assert.Contains("npx bmad-method install --modules gds", gds);

            var hub = SiteRegion.Read(output, "about-sdd.html");
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
        var hub = SiteRegion.Read(Site, "about-sdd.html");
        var bmad = SiteRegion.Read(Site, "about-sdd-bmad.html");
        var gds = SiteRegion.Read(Site, "about-sdd-gds.html");

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
    public void HowToRead_GenerateSection_CoversGenerateAndWatch_BetweenReadingOrderAndGlossary()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        Assert.Contains("<h2 id=\"generate\">Generate with SpecScribe</h2>", html);
        Assert.Contains("<code>specscribe generate</code>", html);
        Assert.Contains("<code>specscribe watch</code>", html);

        // Onboarding flows first (read the portal, then produce it); the glossary/commands are reference
        // material consulted later.
        var readingOrder = html.IndexOf("<h2 id=\"reading-order\">", StringComparison.Ordinal);
        var generate = html.IndexOf("<h2 id=\"generate\">", StringComparison.Ordinal);
        var glossary = html.IndexOf("<h2 id=\"glossary\">", StringComparison.Ordinal);
        Assert.True(readingOrder >= 0 && generate > readingOrder && glossary > generate,
            "Generate section must sit between Reading order and Glossary");
    }

    [Fact]
    public void HowToRead_GenerateSection_NamesPathOverrides_AndDefersToHelpForTheRest()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = GenerateSectionOf(SiteRegion.Read(Site, "how-to-read.html"));

        Assert.Contains("<code>--source</code>", html);
        Assert.Contains("<code>--adrs</code>", html);
        Assert.Contains("<code>--output</code>", html);
        Assert.Contains("--help", html);

        // The flag table lives in --help, not here — a second copy drifts the moment Epic 5 adds an option.
        Assert.DoesNotContain("--project-name", html);
        Assert.DoesNotContain("--no-readme", html);
        Assert.DoesNotContain("--spa", html);
        Assert.DoesNotContain("--code-url", html);
        Assert.DoesNotContain("--today-policy", html);
    }

    [Fact]
    public void HowToRead_GenerateSection_NamesDiagnosticsFieldLabels_AndLinksThere()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var full = SiteRegion.Read(Site, "how-to-read.html");
        var html = GenerateSectionOf(full);

        // The exact labels DiagnosticsTemplater.RenderConfig renders, so prose here maps onto what a real run
        // shows — not a renamed parallel vocabulary.
        var diagnostics = SiteRegion.Read(Site, "diagnostics.html");
        foreach (var label in new[] { "Source root", "ADR location", "Output directory", "README included", "Deep-git analytics", "External source base" })
        {
            Assert.Contains(label, html);
            Assert.Contains($"<dt>{label}</dt>", diagnostics);
        }

        Assert.Contains("href=\"diagnostics.html\"", html);
        Assert.Contains(".specscribe", html);
        Assert.Contains("Configure paths", html);
        Assert.Contains("--show-config", html);
    }

    [Fact]
    public void HowToRead_GenerateSection_IsFrameworkAgnostic_AndRendersWithoutADetectedModule()
    {
        // Same NFR8 discipline the reading order already follows: this section names slots and SpecScribe's own
        // CLI, never a methodology's folder names — and unlike every other section it has no availability gate,
        // so an undetected repo (no glossary, no commands, no reading-order pages) still gets it.
        var bareRoot = Directory.CreateTempSubdirectory("specscribe-howtoread-bare-").FullName;
        try
        {
            var source = Path.Combine(bareRoot, "_bmad-output");
            Directory.CreateDirectory(source);
            var output = Path.Combine(bareRoot, "site");

            new SiteGenerator(ForgeOptions.Resolve(source: source, output: output, projectName: "SpecScribe", includeReadme: false)).GenerateAll();
            var full = SiteRegion.Read(output, "how-to-read.html");

            Assert.DoesNotContain("<h2 id=\"reading-order\">", full);
            Assert.DoesNotContain("<h2 id=\"glossary\">", full);
            Assert.Contains("<h2 id=\"generate\">Generate with SpecScribe</h2>", full);
            // The subtitle/intro must not promise reading order or glossary content that isn't there.
            Assert.DoesNotContain("Start with the reading order", full);

            var section = GenerateSectionOf(full);
            Assert.DoesNotContain("_bmad-output", section);
            Assert.DoesNotContain("BMad", section);
            Assert.DoesNotContain("bmad", section);
        }
        finally
        {
            Directory.Delete(bareRoot, recursive: true);
        }
    }

    [Fact]
    public void HowToRead_SubtitleAndIntro_MentionGeneratingNotJustReading()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        Assert.Contains("then generate the site yourself", html);
        Assert.Contains("how to rebuild", html);
        // [Story 23.6 AC #8] The meta description is CHROME — asserted against the IR's head projection, which
        // is the value the renderer emits, rather than as a substring of a document C# no longer writes.
        Assert.Contains("how to generate and refresh the site from the command line",
            SiteRegion.Head(Site, "how-to-read.html").Description);
    }

    /// <summary>The Generate section's own markup, from its <c>h2</c> to the next one (or the end of the panel) —
    /// so a DoesNotContain assertion can't be satisfied or defeated by the surrounding page (the nav bar and the
    /// glossary both mention BMad on a detected repo).</summary>
    private static string GenerateSectionOf(string html)
    {
        var start = html.IndexOf("<h2 id=\"generate\">", StringComparison.Ordinal);
        Assert.True(start >= 0, "Generate section should be present");
        var next = html.IndexOf("<h2 ", start + 1, StringComparison.Ordinal);
        var end = next >= 0 ? next : html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "Generate section should have an end boundary");
        return html[start..end];
    }

    // ---- Story 18.2: a detected-but-unmodeled module is NAMED, never silently given BMM's vocabulary ----

    /// <summary>Swaps this fixture's BMM install for one whose code SpecScribe doesn't model, keeping every
    /// other source artifact identical so the only variable is module identity.</summary>
    private void InstallOnly(string code, string csv)
    {
        Directory.Delete(Path.Combine(_root, "_bmad", "bmm"), recursive: true);
        File.WriteAllText(Path.Combine(_root, "_bmad", "_config", "manifest.yaml"),
            $"modules:\n  - name: core\n    version: 6.0.0\n  - name: {code}\n    version: 6.0.0");
        var dir = Path.Combine(_root, "_bmad", code);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "module-help.csv"), csv);
    }

    [Fact]
    public void HowToRead_UnmodeledModule_NamesTheModuleWhereTheGlossaryWouldBe()
    {
        InstallOnly("tea", TeaCsv);

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        // The heading and its anchor survive so in-page links to #glossary still resolve...
        Assert.Contains("<h2 id=\"glossary\">Glossary</h2>", html);
        // ...but the body is the owner's named acknowledgement, not a definition list.
        Assert.Contains("Test Architecture Enterprise", html);
        Assert.Contains("SpecScribe doesn't publish a glossary for it yet.", html);
        Assert.DoesNotContain("howtoread-glossary", html);

        // And emphatically NOT BMad Method's vocabulary.
        Assert.DoesNotContain("<dt>FR</dt>", html);
        Assert.DoesNotContain("<dt>PRD</dt>", html);
        Assert.DoesNotContain("spec kernel", html);
    }

    [Fact]
    public void ContentPages_UnmodeledModule_NoBmadMethodAbbreviationExpansion()
    {
        InstallOnly("tea", TeaCsv);

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var epics = SiteRegion.Read(Site, "epics.html");

        // epics.md deliberately uses bare FR/NFR/AC/ADR/PRD tokens — with no glossary there is nothing to
        // expand, so the site-wide AbbreviationExpander must leave every one of them plain.
        Assert.DoesNotContain("<abbr", epics);
        Assert.Contains("FR and NFR items", epics);
    }

    [Fact]
    public void HowToRead_UnmodeledModule_OmitsCommandLegend_AndSkipsModuleDocReadingOrder()
    {
        InstallOnly("tea", TeaCsv);

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        // The catalog parses fine, but the legend promises commands "captioned on story and epic pages" —
        // surfaces that only exist for a modeled module. It renders only for a MODELED primary.
        Assert.DoesNotContain("<h2 id=\"commands\">Commands you'll see</h2>", html);

        // No module docs are published for it, so the reading order carries none — even though this fixture
        // has prd.md/ARCHITECTURE-SPINE.md on disk (they render, they're just not module docs here).
        var start = html.IndexOf("<h2 id=\"reading-order\">", StringComparison.Ordinal);
        var end = html.IndexOf("</ol>", start, StringComparison.Ordinal);
        var order = html[start..end];
        Assert.DoesNotContain("href=\"prd.html\"", order);
        Assert.DoesNotContain("href=\"ARCHITECTURE-SPINE.html\"", order);
        Assert.Contains("href=\"epics.html\"", order);
    }

    [Fact]
    public void HowToRead_UnmodeledModule_DoesNotPromiseAGlossaryTheAcknowledgementDenies()
    {
        // The acknowledgement ALWAYS renders for an unmodeled module, so counting it as "module content" made
        // the page's own subtitle and intro promise "the reading order and glossary below" and "what the
        // recurring terms mean" — on a page whose glossary section says SpecScribe publishes no glossary for
        // this module. That is Story 5.6's rule: a section that always renders for a given state cannot be the
        // signal for "is there content". Only the header copy changes; the acknowledgement itself stays.
        // [Review][Patch P2]
        InstallOnly("tea", TeaCsv);

        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        // The acknowledgement still renders, anchor and all — this patch must not have silenced it.
        Assert.Contains("<h2 id=\"glossary\">Glossary</h2>", html);
        Assert.Contains("SpecScribe doesn't publish a glossary for it yet.", html);

        // ...but nothing on the page promises a glossary that does not exist.
        Assert.DoesNotContain("Start with the reading order and glossary below", html);
        Assert.DoesNotContain("what the recurring terms mean", html);
        // [Story 23.6 AC #8] Chrome: on the UNMODELED path the phrase lives only in the meta description (the
        // doc-subtitle that also carries it is emitted on the modeled path), so the head projection is where it
        // is now asked for.
        Assert.Contains("Orientation for a first visit", SiteRegion.Head(Site, "how-to-read.html").Description);
    }

    [Fact]
    public void HowToRead_ModeledModule_StillPromisesItsReadingOrderAndGlossary()
    {
        // The other side of Patch P2: a real glossary is real content, so the modeled path's header copy is
        // unchanged. AC #3 — BMad Method's surfaces must not move. [Review][Patch P2]
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

        Assert.Contains("Start with the reading order and glossary below", html);
        Assert.Contains("what the recurring terms mean", html);
        Assert.Contains("<dl class=\"howtoread-glossary\">", html);
    }

    [Fact]
    public void Diagnostics_UnmodeledModule_ReportsOneInformationalNoticeNamingCodeAndLabel()
    {
        InstallOnly("tea", TeaCsv);

        var events = new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();

        var notices = events
            .Where(e => e.Message is not null && e.Message.Contains("Detected BMad module", StringComparison.Ordinal))
            .ToList();

        var notice = Assert.Single(notices);
        Assert.Equal(GenerationOutcome.Skipped, notice.Outcome); // non-fatal
        Assert.Contains("[Informational]", notice.Message!);
        Assert.Contains("'tea'", notice.Message!);
        Assert.Contains("Test Architecture Enterprise", notice.Message!);
    }

    [Fact]
    public void Diagnostics_ModeledModule_EmitsNoUnmodeledNotice()
    {
        var events = new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();

        Assert.DoesNotContain(events,
            e => e.Message is not null && e.Message.Contains("Detected BMad module", StringComparison.Ordinal));
    }

    [Fact]
    public void HowToRead_BypassesApplyReferenceLinks()
    {
        new SiteGenerator(Options(Source, Adrs, Site)).GenerateAll();
        var html = SiteRegion.Read(Site, "how-to-read.html");

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
