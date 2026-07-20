using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 10.10's requirement-page local-context band
/// (<c>RequirementsTemplater.BuildRequirementLocalContext</c>/<c>RequirementGroupLabel</c>) — no prior test
/// exercised this builder directly, only the generic seam mechanics in <c>HtmlRenderAdapterTests</c>.</summary>
public class RequirementLocalContextTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-reqlocal-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string Adrs => Path.Combine(_root, "docs", "adrs");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: The game runs a day cycle
        FR2: Patients arrive

        ### FR Coverage Map

        FR1: Epic 1 - core loop
        FR2: Epic 1 - arrivals

        ## Epic List

        ### Epic 1: Foundation

        Stand it up.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want a skeleton.
        """;

    public RequirementLocalContextTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    [Fact]
    public void GenerateAll_RequirementPage_LocalContextBand_ListsCategorySiblingsWithCurrentMarkedActive()
    {
        var events = new SiteGenerator(Options()).GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(Path.Combine(Site, "requirements", "fr1.html"));
        Assert.Contains("site-nav-local-context", html);
        Assert.Contains("Core Loop", html);
        Assert.Contains("local-context-pill active", html);
        // FR2 (same category) is a real link; FR1 (current) never self-links.
        Assert.Matches(new System.Text.RegularExpressions.Regex("<a[^>]*class=\"local-context-pill\"[^>]*>FR2"), html);
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex("<a[^>]*class=\"local-context-pill\"[^>]*>FR1"), html);
    }
}
