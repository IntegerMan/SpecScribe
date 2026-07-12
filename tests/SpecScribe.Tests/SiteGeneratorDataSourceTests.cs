using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.11 coverage for the non-markdown DATA SOURCE watch route: <c>sprint-status.yaml</c> (and
/// <c>_bmad/config.toml</c>) must refresh the live view, which the <c>.md</c> dispatch routes never did — worse,
/// <c>sprint-status.yaml</c> mis-routes to <see cref="SiteGenerator.RegenerateEpics"/> (which by design skips
/// sprint state). Verifies the classifier (<see cref="SiteGenerator.IsDataSource"/>) and the new route
/// (<see cref="SiteGenerator.RegenerateFromDataSource"/>) at the generator level; the extension-side glob widening
/// rides the F5 smoke. Temp-dir fixture in the style of <see cref="SiteGeneratorWebviewTests"/>.</summary>
public class SiteGeneratorDataSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-datasource-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string SprintPath => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");
    private string SprintHtml => Path.Combine(Site, "sprint.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    // The sprint tracking ledger — a non-.md data source under implementation-artifacts/. `last_updated` renders on
    // the sprint page verbatim, so it doubles as a distinctive marker for "was _sprint re-parsed?".
    private const string SprintYaml = """
        last_updated: MARKER-V1
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
        """;

    public SiteGeneratorDataSourceTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            "# Story 1.1: Foundation Story\n\nStatus: in-progress\n\n## Story\n\nAs a maintainer, I want it.\n");
        File.WriteAllText(SprintPath, SprintYaml);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private SiteGenerator GeneratedSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void IsDataSource_ClassifiesSprintAndConfig_ButNotOrdinaryFiles()
    {
        // Pure path classification (no disk read) — filename + segment convention, shared with the adapter/options.
        var gen = new SiteGenerator(Options());

        Assert.True(gen.IsDataSource(SprintPath));
        Assert.True(gen.IsDataSource(Path.Combine(_root, "_bmad", "config.toml")));

        // Negatives: a markdown artifact, a stray yaml that isn't the sprint file, and a config.toml NOT under _bmad.
        Assert.False(gen.IsDataSource(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md")));
        Assert.False(gen.IsDataSource(Path.Combine(Source, "planning-artifacts", "other.yaml")));
        Assert.False(gen.IsDataSource(Path.Combine(_root, "elsewhere", "config.toml")));
    }

    [Fact]
    public void SprintStatusYaml_IsClassifiedBothDataSourceAndEpicsRelated_SoOrderingMatters()
    {
        // The whole reason the dispatch checks IsDataSource FIRST: sprint-status.yaml lives under
        // implementation-artifacts/, so IsEpicsRelated also claims it — and RegenerateEpics never re-parses sprint.
        var gen = new SiteGenerator(Options());
        Assert.True(gen.IsEpicsRelated(SprintPath));
        Assert.True(gen.IsDataSource(SprintPath));
    }

    [Fact]
    public void RegenerateFromDataSource_ReParsesSprint_AndRewritesTheSprintSurface()
    {
        // The defect fix: a sprint-status.yaml edit refreshes the sprint board. Baseline renders MARKER-V1.
        var gen = GeneratedSite();
        Assert.Contains("MARKER-V1", File.ReadAllText(SprintHtml));

        File.WriteAllText(SprintPath, SprintYaml.Replace("MARKER-V1", "MARKER-V2"));
        var ev = gen.RegenerateFromDataSource(SprintPath);

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        // _sprint was re-parsed (a full GenerateAll) and the sprint surface rewritten — the board is no longer stale.
        Assert.Contains("MARKER-V2", File.ReadAllText(SprintHtml));
    }

    [Fact]
    public void RegenerateEpics_LeavesSprintStateStale_WhichIsWhyTheDataRouteExists()
    {
        // Documents the mis-route the data route avoids: routing a sprint-status.yaml change to RegenerateEpics
        // (what would happen if only the .md filter were widened) never re-parses _sprint, so the board strands the
        // change. This is the "the core side is more than the Filter property" case from the AC.
        var gen = GeneratedSite();
        Assert.Contains("MARKER-V1", File.ReadAllText(SprintHtml));

        File.WriteAllText(SprintPath, SprintYaml.Replace("MARKER-V1", "MARKER-V3"));
        var ev = gen.RegenerateEpics();

        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);
        var html = File.ReadAllText(SprintHtml);
        Assert.Contains("MARKER-V1", html);       // still the old value — RegenerateEpics skipped sprint state (AD-5)
        Assert.DoesNotContain("MARKER-V3", html); // the edit was stranded, exactly the bug 6.11 fixes
    }

    [Fact]
    public void FileWatcherService_WithConfigDirPresent_ConstructsAndWatches_WithoutThrowing()
    {
        // Exercises the new _bmad/config.toml watch branch (only added when the dir exists) and the widened
        // CreateWatcher filters — a deterministic smoke, no reliance on real FS-event timing.
        Directory.CreateDirectory(Path.Combine(_root, "_bmad"));
        File.WriteAllText(Path.Combine(_root, "_bmad", "config.toml"), "project_name = \"SpecScribe\"\n");
        var gen = GeneratedSite();

        using var watcher = new FileWatcherService(Options(), gen, _ => { });
        watcher.Start();
        watcher.Stop();
    }
}
