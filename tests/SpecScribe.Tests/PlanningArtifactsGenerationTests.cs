using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 2.4: a real generation pass over a planning-artifacts fixture
/// (brief <c>draft</c>, prd <c>final</c> + sibling <c>review-rubric.md</c> with no frontmatter, DESIGN/EXPERIENCE
/// <c>final</c>) must card the PRD prominently with a "Final" badge and a link to its quality review, keep the
/// UX docs paired, keep the Product Brief distinct with a "Draft" badge, and suppress the rubric as a standalone
/// card while STILL writing its page (link resolves, no 404). Also proves the degradation path: with the rubric
/// removed, the PRD card drops the quality-review link cleanly. [Story 2.4 Task 5/6]</summary>
public class PlanningArtifactsGenerationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-planning-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private string IndexPage => Path.Combine(Site, "index.html");
    private string RubricPage => Path.Combine(Site, "planning-artifacts", "prds", "prd-x", "review-rubric.html");
    private string PrdPage => Path.Combine(Site, "planning-artifacts", "prds", "prd-x", "prd.html");

    // Output-relative hrefs as they appear in the home index (index.html lives at the site root).
    private const string PrdHref = "planning-artifacts/prds/prd-x/prd.html";
    private const string RubricHref = "planning-artifacts/prds/prd-x/review-rubric.html";
    private const string BriefHref = "planning-artifacts/briefs/brief.html";

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.
        """;

    public PlanningArtifactsGenerationTests()
    {
        void Write(string rel, string body)
        {
            var full = Path.Combine(Source, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, body);
        }

        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");

        Write("planning-artifacts/epics.md", EpicsMd);
        Write("planning-artifacts/briefs/brief.md", "---\nstatus: draft\n---\n\n# Product Brief\n\nThe brief.\n");
        Write("planning-artifacts/prds/prd-x/prd.md", "---\nstatus: final\n---\n\n# Product Requirements\n\nThe PRD.\n");
        // review-rubric.md deliberately has NO frontmatter (title derives from its H1) — a PRD companion.
        Write("planning-artifacts/prds/prd-x/review-rubric.md", "# PRD Quality Review — SpecScribe\n\nRubric.\n");
        Write("planning-artifacts/ux-designs/ux-x/DESIGN.md", "---\nstatus: final\n---\n\n# UX Design\n\nDesign.\n");
        Write("planning-artifacts/ux-designs/ux-x/EXPERIENCE.md", "---\nstatus: final\n---\n\n# UX Experience\n\nFlows.\n");
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
    public void GenerateAll_PlanningPagesStillGenerated_ButNoHomeIndexBand()
    {
        // spec-declutter-home-dashboard removed the home planning index band (PRD primary card, UX subgroup,
        // rubric branch link). The planning PAGES are still generated on disk (reachable by direct URL / nav) —
        // only the duplicated home listing is gone.
        GenerateSite();
        var index = File.ReadAllText(IndexPage);

        // No home index-band card markup for the planning artifacts anymore.
        Assert.DoesNotContain("index-card--primary", index);
        Assert.DoesNotContain("index-card-branch", index);
        Assert.DoesNotContain("<div class=\"index-subgroup-label\">UX</div>", index);
        Assert.DoesNotContain($"<a class=\"index-card\" href=\"{RubricHref}\">", index);

        // Every planning page is still generated on disk (the removal never orphaned a generated page).
        Assert.True(File.Exists(RubricPage), "review-rubric.html must still be generated");
        Assert.True(File.Exists(PrdPage), "prd.html must be generated");
    }

    [Fact]
    public void GenerateAll_WithoutRubric_StillGeneratesNoRubricPageAndNoHomeBranchLink()
    {
        // The rename/remove scenario from Task 6: no rubric → no rubric page and no dangling reference on home.
        File.Delete(Path.Combine(Source, "planning-artifacts", "prds", "prd-x", "review-rubric.md"));
        GenerateSite();
        var index = File.ReadAllText(IndexPage);

        Assert.DoesNotContain("index-card-branch", index);        // no quality-review link on home
        Assert.DoesNotContain(RubricHref, index);                 // no reference to the (absent) rubric page
        Assert.False(File.Exists(RubricPage));
    }
}
