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
    public void GenerateAll_CardsPrdProminentWithFinalBadgeAndLinksTheRubric()
    {
        GenerateSite();
        var index = File.ReadAllText(IndexPage);

        // PRD is the prominent primary card carrying its own "Final" badge.
        Assert.Contains("index-card--primary", index);
        Assert.Contains($"<h2><a href=\"{PrdHref}\">", index);
        Assert.Contains("class=\"status-badge done", index);
        Assert.Contains("class=\"status-badge drafted", index);
        Assert.Contains(">Final</span>", index);
        // Brief is a distinct card with a "Draft" badge; UX docs are paired.
        Assert.Contains(">Draft</span>", index);
        Assert.Contains("<div class=\"index-subgroup-label\">UX</div>", index);
        Assert.Contains($"href=\"{BriefHref}\"", index);

        // The rubric is linked from the PRD card, but is NOT a standalone top-level card...
        Assert.Contains($"<a class=\"index-card-branch\" href=\"{RubricHref}\">Quality review", index);
        Assert.DoesNotContain($"<a class=\"index-card\" href=\"{RubricHref}\">", index);
        // ...and its page is still generated on disk (the link resolves — no 404).
        Assert.True(File.Exists(RubricPage), "review-rubric.html must still be generated");
        Assert.True(File.Exists(PrdPage), "prd.html must be generated");
    }

    [Fact]
    public void GenerateAll_WithoutRubric_PrdCardDropsTheQualityReviewLinkCleanly()
    {
        // The rename/remove scenario from Task 6: no rubric → the PRD card has no dangling quality-review link,
        // and no broken reference to a page that wasn't generated.
        File.Delete(Path.Combine(Source, "planning-artifacts", "prds", "prd-x", "review-rubric.md"));
        GenerateSite();
        var index = File.ReadAllText(IndexPage);

        Assert.Contains("index-card--primary", index);            // PRD still prominent
        Assert.DoesNotContain("index-card-branch", index);        // but no quality-review link
        Assert.DoesNotContain(RubricHref, index);                 // no reference to the (absent) rubric page
        Assert.False(File.Exists(RubricPage));
    }
}
