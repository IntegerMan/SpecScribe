using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 4.1: with ingestion routed through
/// <see cref="BmadArtifactAdapter"/>, the generated site is exactly what the inline parse chain produced —
/// pinned here as a golden inventory of every output file a representative BMad fixture yields — and adapter
/// diagnostics surface on the existing event channel without failing the run or suppressing sibling pages
/// (AC #2). The full byte-for-byte before/after diff was performed against a frozen copy of this repo's own
/// artifacts at implementation time (zero diffs, modulo the wall-clock footer and the build-derived asset
/// cache-bust token); this fixture keeps the shape of that guarantee alive in the suite. Follows the temp-dir
/// fixture style of <see cref="SiteGeneratorSprintTests"/>.</summary>
public class SiteGeneratorAdapterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-adaptergen-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string SprintYaml => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string RetroMd = """
        # Epic 1 Retrospective

        **Date:** 2026-07-06
        **Participants:** Team

        Went well.
        """;

    private const string SprintYamlContent = """
        last_updated: 2026-07-06T22:00:00-04:00
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
          1-2-undrafted: backlog
        """;

    public SiteGeneratorAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "epic-1-retro-2026-07-06.md"), RetroMd);
        File.WriteAllText(SprintYaml, SprintYamlContent);
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
    public void GenerateAll_GoldenOutputInventory_IsExactlyThePreAdapterPageSet()
    {
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var actual = Directory.EnumerateFiles(Site, "*", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // The exact page set the pre-adapter pipeline produced for this fixture — a new, missing, or
        // relocated output file is a rendering-behavior change and must be a deliberate decision, never a
        // side effect of adapter work (AC #1: rendering stays framework-agnostic and unchanged).
        var expected = new[]
        {
            "adrs/index.html",
            "epics.html",
            "epics/epic-1.html",
            "epics/story-1-1.html",
            "epics/story-1-2.html",
            "implementation-artifacts/epic-1-retro-2026-07-06.html",
            "index.html",
            "requirements.html",
            "requirements/fr1.html",
            "requirements/nfr1.html",
            "retros.html",
            "specscribe.css",
            "specscribe.js",
            "sprint.html",
            "structure.html",
        }.OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GenerateAll_UnusableSprintYaml_ReportsSkippedDiagnosticAndSiblingsStillRender()
    {
        File.WriteAllText(SprintYaml, "just: some\nunrelated: keys\n");

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();

        // AC #2: the unsupported shape is categorized and reported as non-fatal on the existing event
        // channel — never an Error, never an abort…
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);
        var diag = Assert.Single(events, e => e.Outcome == GenerationOutcome.Skipped && e.RelativePath.EndsWith("sprint-status.yaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("development_status", diag.Message);

        // …and every successful artifact still renders, while the sprint surfaces omit cleanly.
        Assert.False(File.Exists(Path.Combine(Site, "sprint.html")));
        Assert.True(File.Exists(Path.Combine(Site, "index.html")));
        Assert.True(File.Exists(Path.Combine(Site, "epics.html")));
        Assert.True(File.Exists(Path.Combine(Site, "epics", "story-1-1.html")));
        Assert.DoesNotContain("href=\"sprint.html\"", File.ReadAllText(Path.Combine(Site, "index.html")));
    }

    [Fact]
    public void GenerateAll_ThenRegenerateEpics_KeepsWatchParity()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        // A watch-mode epics edit: retitle the story, then run the incremental path the watcher uses.
        File.WriteAllText(
            Path.Combine(Source, "planning-artifacts", "epics.md"),
            EpicsMd.Replace("Foundation Story", "Renamed Story"));
        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        Assert.Contains("Renamed Story", File.ReadAllText(Path.Combine(Site, "epics", "story-1-1.html")));
        Assert.Contains("Renamed Story", File.ReadAllText(Path.Combine(Site, "epics.html")));
    }
}
