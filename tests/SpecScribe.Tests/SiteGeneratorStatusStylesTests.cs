using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 8.2: unrecognized status badges, non-fatal notices, and the
/// portal-wide status legend key. Temp-dir fixture style matching other SiteGenerator* tests.</summary>
public class SiteGeneratorStatusStylesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-status-8-2-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");

    public SiteGeneratorStatusStylesTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        // AppliesTo needs _bmad/ for the adapter path that emits AdapterDiagnostic.
        Directory.CreateDirectory(Path.Combine(_root, "_bmad"));

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), """
            # Epics

            ## Epic List

            ### Epic 1: Foundation

            Stand up the portal.

            ## Epic 1: Foundation

            ### Story 1.1: Odd Status

            As a contributor, I want an unrecognized status surfaced.

            ### Story 1.2: No Status Yet

            As a contributor, I want an absent status to stay drafted.
            """);

        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-odd-status.md"), """
            # Story 1.1: Odd Status

            Status: frobnicated

            ## Story

            As a contributor, I want an unrecognized status surfaced.

            ## Acceptance Criteria

            1. It shows as unrecognized.

            ## Tasks / Subtasks

            - [x] Task 1: Do it (AC: #1)
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source,
        adrs: Path.Combine(_root, "docs", "adrs"),
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false);

    [Fact]
    public void GenerateAll_UnrecognizedStatus_RendersBadgeAndNonFatalNotice_AbsentStaysDrafted()
    {
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll().ToList();

        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var notice = Assert.Single(events, e =>
            e.FromAdapterDiagnostic
            && e.Message is { } m
            && m.Contains("frobnicated", StringComparison.Ordinal));
        Assert.Equal(GenerationOutcome.Skipped, notice.Outcome); // Unsupported → Skipped, not Error
        Assert.Contains("[Unsupported]", notice.Message);

        var oddPage = File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html"));
        Assert.Contains("status-badge unrecognized", oddPage);
        Assert.Contains("frobnicated", oddPage);
        Assert.Contains("class=\"status-legend\"", oddPage);
        Assert.Contains("Show status legend", oddPage);

        var placeholder = File.ReadAllText(Path.Combine(Site, "epics", "story-1-2.html"));
        Assert.DoesNotContain("status-badge unrecognized", placeholder);
        // Placeholder uses "Not yet drafted" with drafted stage — no unmapped notice for absent status.
        Assert.Contains("status-badge drafted", placeholder);
    }

    [Fact]
    public void GenerateAll_LegendKey_IsDeterministicAcrossTwoRuns()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var first = File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html"));

        // Wipe and regenerate from the same inputs — badges, legend, and notice text must be byte-stable
        // aside from the volatile footer clock (already covered by golden normalization elsewhere).
        Directory.Delete(Site, recursive: true);
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var second = File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html"));

        static string StripFooterClock(string html) =>
            System.Text.RegularExpressions.Regex.Replace(
                html,
                @"on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}",
                "on DATE");

        Assert.Equal(StripFooterClock(first), StripFooterClock(second));
    }
}
