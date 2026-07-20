using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for the Story 3.3 review fixes: a present family's card links only to a
/// page that was actually produced, and watch-mode incremental regeneration (<see cref="SiteGenerator.GenerateOne"/>,
/// <see cref="SiteGenerator.RegenerateEpics"/>) refreshes the cached <c>ArtifactCoverage</c> rather than leaving
/// the Planning Artifacts panel stale until the next full <c>generate</c>. Follows the temp-dir fixture style of
/// <see cref="SiteGeneratorStructureTests"/>.</summary>
public class SiteGeneratorCoverageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-coverage-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
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

        A short brief so the Product Brief family has a linkable present card.
        """;

    public SiteGeneratorCoverageTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
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

    [Fact]
    public void GenerateAll_PresentFamilyCardLinksToTheActualGeneratedPage()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(IndexPage);

        // Epics is present (epics.md parsed successfully) and links to the real epics.html — the href is
        // resolved only after _epicsModel/_docs are populated, so this proves the coverage build now runs
        // after page generation rather than before it. [Story 3.3 review]
        Assert.Contains("coverage-card js-tip present family-epics\" href=\"epics.html\"", html);
    }

    [Fact]
    public void GenerateAll_MalformedMemlogUpdatedDate_ContributesNoEnrichmentAndDoesNotThrow()
    {
        // A .memlog.md with no parseable "updated:" date must degrade to "no enrichment for this file" rather
        // than throw or corrupt the primary coverage picture (BuildMemlogMap's `catch { continue; }` /
        // unparseable-date `continue` path). [Story 3.3 deferred-debt cleanup]
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", ".memlog.md"),
            "# Decision Journal\n\nupdated: not-a-date\n\nSome notes.\n");

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("Decision journal (.memlog) updated", html);
    }

    [Fact]
    public void GenerateAll_MalformedMemlogAlongsideValidOne_ValidOneStillEnrichesItsFamily()
    {
        // A malformed .memlog.md elsewhere in the tree must not prevent a separate, well-formed, correctly
        // ancestor-scoped .memlog.md from enriching its family — the malformed one is filtered out before
        // ancestor-selection runs, not merged with or shadowing a good candidate. [Story 3.3 deferred-debt cleanup]
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", ".memlog.md"),
            "# Decision Journal\n\nupdated: not-a-date\n\nSome notes.\n");
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", ".memlog.md"),
            "# Decision Journal\n\nupdated: 2026-07-01\n\nEpics scoped journal.\n");

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(IndexPage);
        Assert.Contains($"Decision journal (.memlog) updated {Charts.DReadable(new DateOnly(2026, 7, 1))}", html);
    }

    [Fact]
    public void GenerateOne_RefreshesCoveragePanelWithoutAFullRegenerate()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // Product Brief did not exist at full-generate time, so its card should read Missing.
        Assert.Contains(">Missing<", File.ReadAllText(IndexPage));

        // Add it after the fact and push it through the single-file watch-mode path (no GenerateAll re-run).
        var briefDir = Path.Combine(Source, "planning-artifacts", "briefs", "brief-x");
        Directory.CreateDirectory(briefDir);
        var briefPath = Path.Combine(briefDir, "brief.md");
        File.WriteAllText(briefPath, BriefMd);

        var ev = gen.GenerateOne(briefPath);
        Assert.Equal(GenerationOutcome.Generated, ev.Outcome);

        // The Planning Artifacts panel reflects the new file immediately — the cached ArtifactCoverage was
        // recomputed as part of GenerateOne, not left stale until the next full generate. [Story 3.3 review]
        var html = File.ReadAllText(IndexPage);
        Assert.Contains("coverage-card js-tip present family-planning\"", html);
        Assert.Contains(">Product Brief<", html);
    }

    [Fact]
    public void RegenerateEpics_RefreshesCoveragePanelForNewlyAddedStoryArtifacts()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // No implementation-artifacts/<n>-<n>-*.md yet, so Stories should read Missing.
        Assert.Contains(">Missing<", File.ReadAllText(IndexPage));

        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "1-1-foundation-story.md"),
            "# Story 1.1\n\nDetail.\n");

        var ev = gen.RegenerateEpics();
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);

        var html = File.ReadAllText(IndexPage);
        Assert.Contains(">Stories<", html);
        // Stories' card is present now — it no longer shows a Missing chip next to that family name.
        Assert.DoesNotContain("Stories</span><span class=\"coverage-chip missing\">", html);
    }
}
