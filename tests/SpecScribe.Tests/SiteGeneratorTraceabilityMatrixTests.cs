using System.Text.RegularExpressions;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 21.1: with epics.md present, a dedicated <c>traceability.html</c>
/// page is written (the requirement x covering-epic matrix, framed with its Story 10.2 legend/why sentence and a
/// ledger-sourced ranking caption), the "Traceability" Delivery nav entry appears beside Requirements (shared
/// <c>hasEpics</c> gate), and compact coverage-strip teasers appear on the dashboard and requirements.html linking
/// back to it. Without epics.md, none of those exist — no dangling nav item, no orphaned page. Mirrors the
/// <see cref="SiteGeneratorCodeMapTests"/> fixture style.</summary>
public class SiteGeneratorTraceabilityMatrixTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-trace-matrix-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string TraceabilityPage => Path.Combine(Site, "traceability.html");
    private string RequirementsPage => Path.Combine(Site, "requirements.html");
    private string IndexPage => Path.Combine(Site, "index.html");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: Covered requirement

        ### NonFunctional Requirements

        NFR1: Deferred requirement

        ### FR Coverage Map

        FR1: Epic 1 - core
        NFR1: Deferred - shelved

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    private const string EpicsMdNoCoverage = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorTraceabilityMatrixTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Adrs);
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

    private SiteGenerator GenerateSite(string epicsMd)
    {
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), epicsMd);
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void GenerateAll_WithEpics_ProducesTraceabilityPageFramedWithLegendAndRankingCaption()
    {
        GenerateSite(EpicsMd);

        Assert.True(File.Exists(TraceabilityPage), "traceability.html should be generated when epics.md exists");
        var html = File.ReadAllText(TraceabilityPage);

        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"site-nav\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        Assert.Contains("class=\"trace-matrix\"", html);
        Assert.Contains("trace-cell covered", html);
        // The Story 10.2 frame: title, why sentence, and the ledger-sourced ranking caption.
        Assert.Contains("Requirement Coverage Matrix", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains("of 2 requirements have a delivering epic", html);
        // The chart-intrinsic 3-swatch legend.
        Assert.Contains("trace-legend", html);
    }

    [Fact]
    public void GenerateAll_WithEpics_AddsTraceabilityNavEntryBesideRequirements()
    {
        GenerateSite(EpicsMd);

        var index = File.ReadAllText(IndexPage);
        Assert.Contains("href=\"traceability.html\"", index);
        Assert.Contains(">Traceability</a>", index);
    }

    [Fact]
    public void GenerateAll_WithoutEpics_WritesNoTraceabilityPageAndNoNavEntry()
    {
        // No epics.md at all in the source tree — the shared hasEpics gate must keep both the page and its
        // nav item absent, never a dangling link.
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.False(File.Exists(TraceabilityPage), "no traceability.html without epics.md (shared hasEpics gate)");
        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("href=\"traceability.html\"", index);
        Assert.DoesNotContain(">Traceability<", index);
    }

    [Fact]
    public void GenerateAll_WithEpicsButNoRequirementCoverageData_DegradesHonestly()
    {
        // epics.md with no "## Requirements Inventory" section at all — RequirementsModel.Everything is empty.
        GenerateSite(EpicsMdNoCoverage);

        Assert.True(File.Exists(TraceabilityPage), "the page still writes (hasEpics), degrading honestly inside");
        var html = File.ReadAllText(TraceabilityPage);
        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public void GenerateAll_TraceabilityCellsDeepLinkToRealPages()
    {
        GenerateSite(EpicsMd);

        AssertNoBrokenLocalLinks(TraceabilityPage);
    }

    [Fact]
    public void GenerateAll_RequirementsPage_ShowsTraceabilityStripLinkingToFullMatrix()
    {
        GenerateSite(EpicsMd);

        var html = File.ReadAllText(RequirementsPage);
        Assert.Contains("trace-strip", html);
        Assert.Contains("href=\"traceability.html\"", html);
        Assert.Contains("View full traceability matrix", html);
    }

    [Fact]
    public void GenerateAll_Dashboard_ShowsTraceabilityStripLinkingToFullMatrix()
    {
        GenerateSite(EpicsMd);

        var html = File.ReadAllText(IndexPage);
        Assert.Contains("trace-panel", html);
        Assert.Contains("trace-strip", html);
        Assert.Contains("href=\"traceability.html\"", html);
    }

    [Fact]
    public void GenerateAll_RequirementsStripAndDashboardStrip_AgreeWithTheSatisfactionBand()
    {
        // The strip's numbers must never compete with requirements.html's own satisfaction band — same
        // ProjectCounts.RequirementsOverall source (AC #2).
        GenerateSite(EpicsMd);

        var html = File.ReadAllText(RequirementsPage);
        var bandCovered = Regex.Match(html, "satisfaction-chip-count\">(\\d+)</span>").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(bandCovered));
        // Both the satisfaction band's "Satisfied" chip and the trace-strip's "Covered" chip read count 1
        // (FR1 is Done, NFR1 is Deferred) — the strip must not diverge into a different number.
        var stripMatch = Regex.Match(html, "trace-strip-chips\">.*?satisfaction-chip-count\">(\\d+)</span>", RegexOptions.Singleline);
        Assert.True(stripMatch.Success);
        Assert.Equal("1", stripMatch.Groups[1].Value);
    }

    private void AssertNoBrokenLocalLinks(string pagePath)
    {
        var html = File.ReadAllText(pagePath);
        var pageDir = Path.GetDirectoryName(pagePath)!;
        foreach (Match m in Regex.Matches(html, "href=\"([^\"]+)\""))
        {
            var raw = m.Groups[1].Value;
            if (raw.StartsWith("#", StringComparison.Ordinal)
                || Regex.IsMatch(raw, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                continue;

            var target = raw.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            var resolved = Path.GetFullPath(Path.Combine(pageDir, target.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(resolved), $"broken link: {raw} -> {resolved}");
        }
    }
}
