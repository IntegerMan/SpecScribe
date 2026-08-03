using System.Text.RegularExpressions;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 21.2: with epics.md + done stories present, a dedicated
/// <c>cadence.html</c> page is written (the story-completion heatmap + cycle-time histogram, each framed with its
/// Story 10.2 metadata and the cycle-time honesty caveat), the "Cadence" Delivery nav entry appears beside
/// Traceability (shared <c>hasEpics</c> gate), and a compact cadence teaser appears on the dashboard linking back to
/// it. Without epics.md, none of those exist. With epics but zero done stories the page still writes with its honest
/// empty state. Mirrors the <see cref="SiteGeneratorTraceabilityMatrixTests"/> fixture style. Completion dates sit in
/// mid-July 2026 (the repo's operating timeframe) so they are safely on/before the generation "today".</summary>
public class SiteGeneratorCadenceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-cadence-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Impl => Path.Combine(Source, "implementation-artifacts");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string CadenceRoute => "cadence.html";
    private string IndexRoute => "index.html";

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: First Story

        As a maintainer, I want the first.

        ### Story 1.2: Second Story

        As a maintainer, I want the second.

        ### Story 1.3: Third Story

        As a maintainer, I want the third.
        """;

    // epics.md with a single, NOT-done story and no artifact — hasEpics is true, but zero stories complete.
    private const string EpicsMdNoDone = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: First Story

        As a maintainer, I want the first.
        """;

    private static string DoneArtifact(string id, string isoDate) => $"""
        # Story {id}
        Status: done

        ## Tasks / Subtasks

        - [x] Task 1

        ## Change Log

        - {isoDate}: Completed the story.
        """;

    public SiteGeneratorCadenceTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Impl);
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(bool spa = false) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, emitSpa: spa);

    private SiteGenerator GenerateWithDoneStories(bool spa = false)
    {
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        // 1.1 alone on Jul 14; 1.2 and 1.3 together on Jul 16 (a multi-completion day).
        File.WriteAllText(Path.Combine(Impl, "1-1-first.md"), DoneArtifact("1.1", "2026-07-14"));
        File.WriteAllText(Path.Combine(Impl, "1-2-second.md"), DoneArtifact("1.2", "2026-07-16"));
        File.WriteAllText(Path.Combine(Impl, "1-3-third.md"), DoneArtifact("1.3", "2026-07-16"));
        var gen = new SiteGenerator(Options(spa));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void GenerateAll_WithDoneStories_ProducesCadencePageFramedWithBothCharts()
    {
        GenerateWithDoneStories();

        Assert.True(SiteRegion.Exists(Site, CadenceRoute), "cadence.html should be generated when epics.md exists");
        var html = SiteRegion.Read(Site, CadenceRoute);

        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        // Both framed charts + the metric-generic why sentence.
        Assert.Contains("Story Completion Cadence", html);
        Assert.Contains("Story Cycle-Time", html);
        Assert.Contains("chart-frame-why", html);
        // The completion heatmap + its text-equivalent log rendered real done-story dates.
        Assert.Contains("class=\"heatmap\"", html);
        Assert.Contains("cadence-log", html);
        // A single-completion day links to that story; a multi-completion day lists both in the log.
        Assert.Contains("href=\"epics/story-1-1.html\"", html);
        Assert.Contains("href=\"epics/story-1-2.html\"", html);
        Assert.Contains("href=\"epics/story-1-3.html\"", html);
    }

    [Fact]
    public void GenerateAll_CadencePage_CarriesTheApproximateCycleTimeCaveat()
    {
        GenerateWithDoneStories();
        var html = SiteRegion.Read(Site, CadenceRoute);
        // The honesty caveat must be visible (Note slot), not buried (AC #2).
        Assert.Contains("chart-frame-note", html);
        Assert.Contains("Approximate", html);
    }

    [Fact]
    public void GenerateAll_AddsCadenceNavEntryBesideTraceability()
    {
        GenerateWithDoneStories();
        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("href=\"cadence.html\"", index);
        Assert.Contains(">Cadence</a>", index);
    }

    [Fact]
    public void GenerateAll_Dashboard_ShowsCadenceStripLinkingToCadencePage()
    {
        GenerateWithDoneStories();
        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("cadence-panel", index);
        Assert.Contains("cadence-strip", index);
        Assert.Contains("View delivery cadence", index);
    }

    [Fact]
    public void GenerateAll_CadencePage_NoBrokenLocalLinks()
    {
        GenerateWithDoneStories();
        SiteRegion.AssertNoBrokenLocalLinks(Site, CadenceRoute);
    }

    [Fact]
    public void GenerateAll_WithoutEpics_WritesNoCadencePageAndNoNavEntry()
    {
        // No epics.md at all — the shared hasEpics gate keeps both the page and its nav item absent.
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.False(SiteRegion.Exists(Site, CadenceRoute), "no cadence.html without epics.md (shared hasEpics gate)");
        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.DoesNotContain("href=\"cadence.html\"", index);
        Assert.DoesNotContain(">Cadence<", index);
    }

    [Fact]
    public void GenerateAll_WithEpicsButNoDoneStories_WritesPageWithHonestEmptyState()
    {
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMdNoDone);
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.True(SiteRegion.Exists(Site, CadenceRoute), "the page still writes (hasEpics), degrading honestly inside");
        var html = SiteRegion.Read(Site, CadenceRoute);
        Assert.Contains("No completed stories to chart yet", html);
        Assert.Contains("No story has a derivable cycle-time", html);
        // Nothing to show → no dashboard strip (omit, don't show an empty panel).
        Assert.DoesNotContain("cadence-strip", SiteRegion.Read(Site, IndexRoute));
    }

    [Fact]
    public void RenderSpaBundle_IncludesCadencePage_ForWebviewSpaParity()
    {
        var gen = GenerateWithDoneStories(spa: true);
        var bundle = gen.RenderSpaBundle();
        var page = Assert.Single(bundle.Pages, p => p.OutputRelativePath == SiteNav.CadenceOutputPath);
        Assert.False(string.IsNullOrWhiteSpace(page.ContentHtml));
    }

}
