using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 10.3: the "How to read this portal" orientation page is
/// written on every full run, its reading order only lists pages that actually exist, its glossary comes
/// from the detected module (AC2 — never hard-coded), Home's Explore Key Views leads with it, and the
/// first-use &lt;abbr&gt; expander wraps bare acronyms on content pages. Follows the temp-dir fixture style
/// of <see cref="SiteGeneratorOutlineTests"/>.</summary>
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

        // Story 10.1 (shipped ahead of this story) replaced the old flat "Explore Key Views" quick-link grid
        // with a journey-organized top nav shared by every page, Home included — so reachability from Home
        // now runs through the shared nav bar's Project group rather than a dashboard card. It leads that
        // group, ahead of Readme.
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        var howToReadIdx = index.IndexOf("href=\"how-to-read.html\"", StringComparison.Ordinal);
        var readmeIdx = index.IndexOf("href=\"readme.html\"", StringComparison.Ordinal);
        Assert.True(howToReadIdx >= 0, "Home's nav bar should link to how-to-read.html");
        Assert.True(readmeIdx < 0 || howToReadIdx < readmeIdx, "how-to-read should lead the Project nav group");
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
    public void UndetectedModule_GlossarySectionOmitted_AndAbbreviationExpanderIsNoOp()
    {
        // No _bmad folder at all — ModuleContext.Detect degrades to None, so the glossary section is
        // omitted (not empty-but-present) and content pages render byte-unchanged with respect to FR/AC/ADR.
        var undetectedRoot = Directory.CreateTempSubdirectory("specscribe-howtoread-nomod-").FullName;
        try
        {
            var source = Path.Combine(undetectedRoot, "_bmad-output");
            Directory.CreateDirectory(Path.Combine(source, "planning-artifacts"));
            File.WriteAllText(Path.Combine(source, "planning-artifacts", "epics.md"), EpicsMd);
            var output = Path.Combine(undetectedRoot, "site");

            new SiteGenerator(ForgeOptions.Resolve(source: source, output: output, projectName: "SpecScribe", includeReadme: false)).GenerateAll();

            var howToRead = File.ReadAllText(Path.Combine(output, "how-to-read.html"));
            Assert.DoesNotContain("Glossary", howToRead);
            Assert.DoesNotContain("<dl", howToRead);
            Assert.True(File.Exists(Path.Combine(output, "how-to-read.html")));

            var epics = File.ReadAllText(Path.Combine(output, "epics.html"));
            Assert.DoesNotContain("<abbr", epics);
        }
        finally
        {
            Directory.Delete(undetectedRoot, recursive: true);
        }
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
