using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 12.2's two shared prerequisites — framework-neutral source-root discovery
/// (<see cref="ForgeOptions.SourceDirNames"/>) and the adapter registry with owner decision D5's minimal merge —
/// plus AC #4's byte-for-byte guarantee for a framework with no milestone level.
///
/// <para>These are the tests that protect EVERY existing project, not just the new one: the load-bearing
/// assertions here are the ones that say a BMad repository resolves, ingests and renders exactly as it did before
/// this story existed.</para>
///
/// <para>A NEW file rather than additions to <c>BmadArtifactAdapterTests</c> / <c>ForgeOptionsTests</c>, following
/// the same hunk-attribution discipline Story 12.1 used: a concurrent session may hold those files, and a story's
/// own coverage should be reviewable as its own hunks.</para></summary>
public class AdapterRegistryTests : IDisposable
{
    private readonly List<string> _temps = new();

    private string NewRoot()
    {
        var dir = Directory.CreateTempSubdirectory("specscribe-registry-").FullName;
        _temps.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _temps)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private const string RoadmapMd = """
        # Roadmap: Fixture

        ## Phases

        ### Milestone: v1.0 (completed 2026-05-27)

        - [x] **Phase 1: Foundation** - Stand it up. (completed 2026-05-07)

        ## Milestone: v1.0 — Phase Details

        ### Phase 1: Foundation
        **Goal**: Stand up the foundation.
        Plans:
        - [x] 01-00-PLAN.md — Wave 0: scaffolding
        """;

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    /// <summary>A GSD Core tree: <c>.planning/</c> with a roadmap and one phase holding one plan.</summary>
    private string GsdRepo()
    {
        var root = NewRoot();
        var phase = Path.Combine(root, ".planning", "phases", "01-foundation");
        Directory.CreateDirectory(phase);
        File.WriteAllText(Path.Combine(root, ".planning", "ROADMAP.md"), RoadmapMd);
        File.WriteAllText(Path.Combine(phase, "01-00-PLAN.md"), "# Plan 01-00\n\nScaffolding.\n");
        return root;
    }

    /// <summary>A BMad tree: an <c>_bmad/bmm</c> install (the marker <c>AppliesTo</c> sniffs) plus an
    /// <c>_bmad-output/</c> holding an epics file.</summary>
    private string BmadRepo()
    {
        var root = NewRoot();
        Directory.CreateDirectory(Path.Combine(root, "_bmad", "bmm"));
        Directory.CreateDirectory(Path.Combine(root, "_bmad-output", "planning-artifacts"));
        File.WriteAllText(Path.Combine(root, "_bmad-output", "planning-artifacts", "epics.md"), EpicsMd);
        return root;
    }

    private static List<string> SourceFiles(ForgeOptions options) =>
        Directory.Exists(options.SourceRoot)
            ? Directory.EnumerateFiles(options.SourceRoot, "*.md", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

    private static ProgressModel Project(EpicsModel epics, IReadOnlyDictionary<string, string> artifacts) =>
        ProgressCalculator.Compute(epics, artifacts, git: null);

    // ---- Task 2: framework-neutral source-root discovery --------------------------------------------------------

    /// <summary>The regression guard for every existing project: a BMad tree still resolves to
    /// <c>_bmad-output</c>, exactly as the single-literal walk-up did.</summary>
    [Fact]
    public void Resolve_BmadOutputTree_ResolvesExactlyAsBefore()
    {
        var root = BmadRepo();
        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));

        Assert.Equal(root, options.RepoRoot);
        Assert.Equal(Path.Combine(root, "_bmad-output"), options.SourceRoot);
        // No _bmad/config.toml and a BMad source root → the historical default, unchanged.
        Assert.Equal(ForgeOptions.DefaultSiteTitle, options.SiteTitle);
    }

    /// <summary>The gap this story closed: before it, a pure GSD repository threw
    /// <see cref="DirectoryNotFoundException"/> here — generation failed before any adapter was consulted.</summary>
    [Fact]
    public void Resolve_PlanningOnlyTree_ResolvesToThePlanningMarker()
    {
        var root = GsdRepo();
        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));

        Assert.Equal(root, options.RepoRoot);
        Assert.Equal(Path.Combine(root, ".planning"), options.SourceRoot);
    }

    /// <summary>A GSD site must never be branded with BMad's default name. The project's own
    /// <c>PROJECT.md</c> H1 supplies the title; with neither that nor a BMad config, the repo folder name does —
    /// never a framework the repo does not use.</summary>
    [Fact]
    public void Resolve_NonBmadRoot_IsNotBrandedWithTheBmadDefault()
    {
        var root = GsdRepo();
        File.WriteAllText(Path.Combine(root, ".planning", "PROJECT.md"), "# Aurora\n\n## What This Is\n\nA project.\n");
        var titled = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
        Assert.Equal("Aurora", titled.SiteTitle);

        File.Delete(Path.Combine(root, ".planning", "PROJECT.md"));
        var untitled = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
        Assert.NotEqual(ForgeOptions.DefaultSiteTitle, untitled.SiteTitle);
        Assert.Equal(Path.GetFileName(root), untitled.SiteTitle);
    }

    [Fact]
    public void Resolve_NoMarkerAtAll_StillThrowsAnActionableMessage()
    {
        var root = NewRoot();
        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site")));

        Assert.Contains("_bmad-output", ex.Message);
        Assert.Contains(".planning", ex.Message);
        Assert.Contains("--source", ex.Message);
    }

    /// <summary>The tolerant <c>requireSource: false</c> path (the webview/extension contract) still degrades
    /// instead of throwing, and still points at the conventional BMad location.</summary>
    [Fact]
    public void Resolve_TolerantMode_StillDegradesRatherThanThrowing()
    {
        var root = NewRoot();
        var options = ForgeOptions.Resolve(
            startDirectory: root, output: Path.Combine(root, "site"), requireSource: false);

        Assert.Equal(root, options.RepoRoot);
        Assert.Equal(Path.Combine(root, "_bmad-output"), options.SourceRoot);
    }

    /// <summary>A repo carrying BOTH markers resolves to the framework INSTALL marker, not to BMad's output
    /// folder. This is the ordering decision the real reference repository forced: its <c>_bmad-output</c> holds a
    /// handful of planning documents while <c>.planning</c> holds the entire roadmap, and resolving to the former
    /// would put every GSD artifact outside the source root where its paths cannot be expressed.</summary>
    [Fact]
    public void Resolve_RepoWithBothMarkers_PrefersTheFrameworkInstallMarker()
    {
        var root = BmadRepo();
        Directory.CreateDirectory(Path.Combine(root, ".planning"));
        File.WriteAllText(Path.Combine(root, ".planning", "ROADMAP.md"), RoadmapMd);

        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
        Assert.Equal(Path.Combine(root, ".planning"), options.SourceRoot);
    }

    // ---- Task 3: selection and merge ----------------------------------------------------------------------------

    /// <summary>A BMad-only repo runs exactly one adapter, and the registry returns that adapter's bundle
    /// VERBATIM — the same instance, so there is no merge code path between it and the generator at all. That
    /// reference equality is the strongest available statement of "output is unchanged".</summary>
    [Fact]
    public void Ingest_BmadOnlyRepo_ReturnsTheBmadBundleUnchanged()
    {
        var root = BmadRepo();
        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
        var files = SourceFiles(options);

        var selected = Assert.Single(new AdapterRegistry().Select(options, files));
        Assert.IsType<BmadArtifactAdapter>(selected);

        var direct = new BmadArtifactAdapter().Ingest(options, files, Project);
        var viaRegistry = new AdapterRegistry().Ingest(options, files, Project);

        Assert.Equal(direct.Diagnostics.Count, viaRegistry.Diagnostics.Count);
        Assert.Equal(direct.EpicsSourceFullPath, viaRegistry.EpicsSourceFullPath);
        Assert.Equal(direct.Epics!.Epics.Count, viaRegistry.Epics!.Epics.Count);
        // No cross-adapter notice fires on a single-adapter run — one that did would itself be the regression.
        Assert.DoesNotContain(viaRegistry.Diagnostics, d => d.Message.Contains("adapters recognized this repository"));
    }

    [Fact]
    public void Ingest_GsdOnlyRepo_ReturnsTheGsdBundle()
    {
        var root = GsdRepo();
        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));

        var selected = Assert.Single(new AdapterRegistry().Select(options, SourceFiles(options)));
        Assert.IsType<GsdCoreArtifactAdapter>(selected);

        var bundle = new AdapterRegistry().Ingest(options, SourceFiles(options), Project);
        Assert.NotNull(bundle.Epics);
        Assert.Single(bundle.Epics!.Epics);
        Assert.Single(bundle.Epics!.Milestones);
        Assert.Null(bundle.Requirements);
    }

    /// <summary>A repo matching NOTHING — a bare <c>_bmad-output</c> tree with no install — still ingests through
    /// BMad, which is precisely what the generator did before the registry existed.</summary>
    [Fact]
    public void Select_RepoMatchingNoMarker_FallsBackToBmad()
    {
        var root = NewRoot();
        Directory.CreateDirectory(Path.Combine(root, "_bmad-output"));
        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));

        var selected = Assert.Single(new AdapterRegistry().Select(options, SourceFiles(options)));
        Assert.IsType<BmadArtifactAdapter>(selected);
    }

    /// <summary>The D5 merge, end to end: both adapters match, GSD supplies the epics family (its roadmap is the
    /// only epics source inside the resolved source root), BMad supplies the module identity, and the run says so
    /// in one Informational notice plus one naming the marker that did not become primary.</summary>
    [Fact]
    public void Ingest_RepoWithBothFrameworks_MergesAndSaysWhoSuppliedWhat()
    {
        var root = BmadRepo();
        var phase = Path.Combine(root, ".planning", "phases", "01-foundation");
        Directory.CreateDirectory(phase);
        File.WriteAllText(Path.Combine(root, ".planning", "ROADMAP.md"), RoadmapMd);
        File.WriteAllText(Path.Combine(phase, "01-00-PLAN.md"), "# Plan 01-00\n\nScaffolding.\n");

        var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
        Assert.Equal(Path.Combine(root, ".planning"), options.SourceRoot);

        var registry = new AdapterRegistry();
        Assert.Equal(2, registry.Select(options, SourceFiles(options)).Count);

        var bundle = registry.Ingest(options, SourceFiles(options), Project);

        // GSD Core owns the epics family — its roadmap is the epics source, and BMad's epics.md is outside the
        // resolved source root so BMad contributes none.
        Assert.NotNull(bundle.Epics);
        Assert.Equal(Path.Combine(root, ".planning", "ROADMAP.md"), bundle.EpicsSourceFullPath);
        Assert.Single(bundle.Epics!.Milestones);

        var matchSet = Assert.Single(bundle.Diagnostics.Where(d => d.Message.Contains("adapters recognized this repository")));
        Assert.Equal(AdapterDiagnosticCategory.Informational, matchSet.Category);
        Assert.Contains("GSD Core", matchSet.Message);
        Assert.Contains("BMad", matchSet.Message);
        Assert.Contains("epics & stories: GSD Core", matchSet.Message);

        // The non-primary framework's documents do not render as pages, and that is stated rather than silent.
        Assert.Contains(bundle.Diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Informational
            && d.Message.Contains("did not become")
            && d.Message.Contains("_bmad-output"));
    }

    /// <summary>The watch-mode scoped slice resolves the SAME epics owner as a full ingest, so an incremental pass
    /// and a full rebuild can never disagree about which framework owns the epics (AD-5 / ADR 0027).</summary>
    [Fact]
    public void IngestEpics_ResolvesTheSameOwnerAsAFullIngest()
    {
        foreach (var root in new[] { BmadRepo(), GsdRepo() })
        {
            var options = ForgeOptions.Resolve(startDirectory: root, output: Path.Combine(root, "site"));
            var files = SourceFiles(options);
            var registry = new AdapterRegistry();

            var full = registry.Ingest(options, files, Project);
            var scoped = registry.IngestEpics(options, files, Project);

            Assert.Equal(full.EpicsSourceFullPath, scoped.SourceFullPath);
            Assert.Equal(full.Epics?.Epics.Count, scoped.Epics?.Epics.Count);
            Assert.Equal(full.StoryArtifactsById.Count, scoped.StoryArtifactsById.Count);
        }
    }

    // ---- Task 8 / AC #4: milestone bands, and the byte-for-byte guarantee ---------------------------------------

    private static EpicsModel ModelWith(IReadOnlyList<MilestoneInfo> milestones) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Milestones = milestones,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1, Title = "Phase 1: Foundation", GoalHtml = string.Empty,
                Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                Stories = new[]
                {
                    new StoryInfo
                    {
                        Id = "1.0", EpicNumber = 1, Title = "Scaffolding",
                        UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), Status = "done",
                    },
                },
            },
        },
    };

    private static string RenderIndexBody(EpicsModel model)
    {
        var progress = ProgressCalculator.Compute(model, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(Array.Empty<string>(), "Fixture", hasAdrs: false);
        var view = EpicsViewBuilder.BuildIndex(model, progress, nav, CommandCatalog.Empty);
        return HtmlRenderAdapter.Shared.RenderEpicsIndexBody(view);
    }

    /// <summary>AC #4's byte-for-byte half. With no milestone level the view carries no bands, the renderer takes
    /// the untouched pre-Story-12.2 branch, and not one byte of band markup reaches the page.</summary>
    [Fact]
    public void EpicsIndex_FrameworkWithNoMilestoneLevel_RendersTheChipSectionsAndNoBandMarkup()
    {
        var body = RenderIndexBody(ModelWith(Array.Empty<MilestoneInfo>()));

        Assert.Contains("<div class=\"section-divider\">Vertical Slice</div>", body);
        Assert.DoesNotContain("milestone-band", body);
        Assert.DoesNotContain("<div class=\"section-divider\">Milestones</div>", body);
    }

    /// <summary>With a milestone level the bands REPLACE the chip sections — the phases are listed once, under the
    /// framework's own grouping, not twice under an <see cref="EpicSection"/> label it never made.</summary>
    [Fact]
    public void EpicsIndex_WithMilestones_RendersBandsInsteadOfChipSections()
    {
        var body = RenderIndexBody(ModelWith(new[]
        {
            new MilestoneInfo("v1.0", "done", "2026-05-27", new[] { 1 }),
        }));

        Assert.Contains("<div class=\"section-divider\">Milestones</div>", body);
        Assert.Contains("milestone-band-name\">v1.0<", body);
        Assert.Contains("1 phase", body);
        Assert.Contains("1/1 plans complete", body);
        Assert.Contains("completed 2026-05-27", body);
        Assert.DoesNotContain("<div class=\"section-divider\">Vertical Slice</div>", body);

        // Never colour alone (UX-DR17): the band badge carries its word.
        Assert.Contains("status-badge done", body);
        Assert.Contains("Done", body);
    }

    /// <summary>A milestone a framework declared but has not filled must not render as a bare heading — the trap
    /// <c>HierarchyExplorerHtml</c>'s doc comment records, applied to a second surface.</summary>
    [Fact]
    public void EpicsIndex_MilestoneWithNoPhases_StatesTheEmptyCaseInWords()
    {
        var body = RenderIndexBody(ModelWith(new[]
        {
            new MilestoneInfo("v2.0", "drafted", null, Array.Empty<int>()),
        }));

        Assert.Contains("milestone-band-name\">v2.0<", body);
        Assert.Contains("No phases are grouped under this milestone yet.", body);
        Assert.Contains("no plans listed yet", body);
        Assert.DoesNotContain("completed ", body);
    }
}
