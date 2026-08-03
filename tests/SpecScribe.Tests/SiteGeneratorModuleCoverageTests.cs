using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 18.6 — the dashboard's Planning Artifacts panel resolves its
/// canonical family set from the DETECTED BMad module rather than from a hardcoded BMad Method list, so a
/// module SpecScribe publishes no family set for stops being told it is missing eight artifacts its
/// methodology never produces (ADR 0015 Decision 5a; NFR8 "absent, not misleadingly empty").
/// <para>Deliberately a SEPARATE class from <see cref="SiteGeneratorCoverageTests"/>: that fixture creates no
/// <c>_bmad/</c> at all and its five tests must stay that way, because <c>BmadModule.Unknown</c> is exactly the
/// state owner decision D1 preserves. Installing a module there would test the opposite decision.</para></summary>
public class SiteGeneratorModuleCoverageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-modcov-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string IndexRoute => "index.html";

    // Verbatim upstream rows, pinned exactly as in ModuleContextTests — see the provenance block there for
    // repositories and commit SHAs. Synthetic rows are what let the skill-prefix identity bug hide, so the
    // module label and skill ids here are what actually ships. [Story 18.2 Task 6; ADR 0015 Decision 7]
    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,https://docs.bmad-method.org/llms.txt,
        BMad Method,bmad-prd,Create Edit and Review PRD,PRD,Facilitated PRD workflow.,,,2-planning,bmad-product-brief,,true,planning_artifacts,prd
        BMad Method,bmad-create-story,Create Story,CS,Story cycle start.,create,,4-implementation,bmad-sprint-planning,,true,implementation_artifacts,story
        """;

    private const string GdsCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Game Dev Studio,_meta,,,,,,,,,false,https://game-dev-studio-docs.bmad-method.org/llms.txt,
        Game Dev Studio,gds-create-story,Create Story,CS,Create Story with comprehensive context for developer agent implementation.,,,4-production,gds-sprint-planning,,true,implementation_artifacts,story
        """;

    /// <summary>Creative Intelligence Suite — a real, first-party, DETECTED module that SpecScribe models no
    /// planning-artifact family set for. The unmodeled case this story closes. [Story 18.6]</summary>
    private const string CisCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Creative Intelligence Suite,_meta,,,,,,,,,false,https://cis-docs.bmad-method.org/llms.txt,
        Creative Intelligence Suite,bmad-cis-innovation-strategy,Innovation Strategy,IS,Identify disruption opportunities and architect business model innovation.,,,anytime,,,false,output_folder,innovation strategy
        Creative Intelligence Suite,bmad-cis-storytelling,Storytelling,ST,Craft compelling narratives using proven story frameworks and techniques.,,,anytime,,,false,output_folder,narrative/story
        """;

    public SiteGeneratorModuleCoverageTests()
    {
        Directory.CreateDirectory(Source);
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

    private void InstallModule(string code, string csv)
    {
        var moduleDir = Path.Combine(_root, "_bmad", code);
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module-help.csv"), csv);
    }

    /// <summary><c>bmad-spec</c> is a <b>Core</b> skill (this repo's <c>_bmad/core/module-help.csv</c> lists it;
    /// <c>_bmad/bmm/module-help.csv</c> does not), and Core ships with EVERY module install. So a cis/tea/bmb
    /// repo that ran <c>/bmad-spec</c> owns a genuine <c>specs/*/SPEC.md</c> — the Spec Kernel family reads
    /// Present, <c>IsEmpty</c> is false, and the existing omission gate does NOT fire. This single file is the
    /// whole reachability proof for the defect. [Story 18.6 premise]</summary>
    private void WriteCoreProducedSpecKernel()
    {
        var specDir = Path.Combine(Source, "specs", "spec-x");
        Directory.CreateDirectory(specDir);
        File.WriteAllText(Path.Combine(specDir, "SPEC.md"),
            "# SPEC\n\nThe canonical contract, written by the core `bmad-spec` skill.\n");
    }

    private void WriteBmadMethodArtifacts()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), """
            # Epics

            ## Epic List

            ### Epic 1: Foundation

            Stand up the portal.

            ## Epic 1: Foundation

            ### Story 1.1: Foundation Story

            As a maintainer, I want the foundation.
            """);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nWhat and why.\n");
    }

    private static IReadOnlyList<GenerationEvent> PanelOmissionDiagnostics(IEnumerable<GenerationEvent> events) =>
        events.Where(e => e.FromAdapterDiagnostic
                          && e.Message is { } m
                          && m.Contains("no modeled planning-artifact family set", StringComparison.Ordinal))
            .ToList();

    // ---- The defect, and its closure ------------------------------------------------------------------

    [Fact]
    public void GenerateAll_UnmodeledPrimaryWithCoreProducedSpec_OmitsThePlanningArtifactsPanel()
    {
        // A CIS-only repo that ran the CORE /bmad-spec skill. Before Story 18.6 this rendered the BMad Method
        // panel with 1 present (Spec Kernel) + 7 missing families — PRD, Product Brief, Architecture, UX,
        // Epics, Stories, Requirements — on a project whose methodology produces none of them.
        InstallModule("cis", CisCsv);
        WriteCoreProducedSpecKernel();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, IndexRoute);

        // The WHOLE panel goes (owner decision D2) — not just its missing cards, and not an empty shell.
        Assert.DoesNotContain("coverage-panel", html);
        Assert.DoesNotContain("Planning Artifacts", html);
        Assert.DoesNotContain("coverage-grid", html);
        Assert.DoesNotContain("coverage-meter", html);
    }

    [Fact]
    public void GenerateAll_UnmodeledPrimary_EmitsExactlyOneInformationalPanelOmissionDiagnostic()
    {
        // D3: silent on the page, but recorded — the panel must not vanish without trace. One row per generate
        // run, never one per family and never one per incremental (ADR 0015 Decision 2d).
        InstallModule("cis", CisCsv);
        WriteCoreProducedSpecKernel();

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll().ToList();

        var notices = PanelOmissionDiagnostics(events);
        var notice = Assert.Single(notices);
        Assert.Equal(GenerationOutcome.Skipped, notice.Outcome);
        Assert.StartsWith("[Informational]", notice.Message);
        Assert.Contains("'cis'", notice.Message);
        Assert.Contains("Creative Intelligence Suite", notice.Message);
        // Anchored at the repo root, reusing ModuleContext's centralized path — _bmad/ is a SIBLING of the
        // source tree, so a source-anchored subject would name a file that does not exist.
        Assert.Equal("_bmad/cis/module-help.csv", notice.RelativePath);
        Assert.Equal(DiagnosticAnchorRoot.Repo, notice.DiagnosticAnchor);
    }

    // ---- AC #2: the modeled modules are untouched -----------------------------------------------------

    [Fact]
    public void GenerateAll_BmadMethodPrimary_KeepsTheEightFamilyPanelAndEmitsNoOmissionDiagnostic()
    {
        InstallModule("bmm", BmmCsv);
        WriteBmadMethodArtifacts();
        WriteCoreProducedSpecKernel();

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll().ToList();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("coverage-panel", html);
        Assert.Contains("Planning Artifacts", html);
        foreach (var label in EightBmadMethodFamilies)
        {
            Assert.Contains($">{label}<", html);
        }

        Assert.Empty(PanelOmissionDiagnostics(events));
    }

    [Fact]
    public void GenerateAll_GameDevStudioPrimary_KeepsTheEightBmadMethodFamilyPanel()
    {
        // AC #2 locks GDS to the BMad Method family set. GDS actually produces gdd.md / narrative-design.md /
        // game-architecture.md, so today's panel is arguably wrong for GDS too — but modeling a GDS-specific
        // set here would violate AC #2 and move the golden fingerprint. Recorded as a follow-up candidate;
        // this test pins the deliberate behavior so a later reader cannot "fix" it by accident. [Story 18.6]
        InstallModule("gds", GdsCsv);
        WriteBmadMethodArtifacts();

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll().ToList();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("coverage-panel", html);
        foreach (var label in EightBmadMethodFamilies)
        {
            Assert.Contains($">{label}<", html);
        }

        Assert.Empty(PanelOmissionDiagnostics(events));
    }

    // ---- D3 cardinality across the watch-mode incremental paths ---------------------------------------

    [Fact]
    public void GenerateOne_AfterUnmodeledPrimary_DoesNotAccumulatePanelOmissionDiagnostics()
    {
        // RefreshCoverage() runs on EVERY watch incremental. Emitting the notice from BuildArtifactCoverage
        // would add a diagnostics row per keystroke — the exact failure ADR 0015 Decision 2d forbids.
        InstallModule("cis", CisCsv);
        WriteCoreProducedSpecKernel();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var notesDir = Path.Combine(Source, "notes");
        Directory.CreateDirectory(notesDir);
        var notePath = Path.Combine(notesDir, "session.md");
        File.WriteAllText(notePath, "# Session\n\nA creative-intelligence note.\n");

        var ev = gen.GenerateOne(notePath);
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);

        // The panel stays omitted after the incremental (the detected module persists across rebuilds), and
        // the incremental itself contributes no second notice.
        Assert.DoesNotContain("coverage-panel", SiteRegion.Read(Site, IndexRoute));
        Assert.Empty(PanelOmissionDiagnostics(new[] { ev }));
    }

    [Fact]
    public void RegenerateEpics_AfterUnmodeledPrimary_DoesNotReEmitThePanelOmissionDiagnostic()
    {
        // Unlike the count-divergence notice — whose ledger genuinely changes on a watch rebuild —
        // the detected module is immutable for the life of the run (detect-once-per-run, Story 18.2), so a
        // re-emission could only ever duplicate a row that is already correct. [Story 18.6 Task 5 choice]
        InstallModule("cis", CisCsv);
        WriteCoreProducedSpecKernel();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var ev = gen.RegenerateEpics();
        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);

        Assert.DoesNotContain("coverage-panel", SiteRegion.Read(Site, IndexRoute));
    }

    private static readonly string[] EightBmadMethodFamilies =
    [
        "PRD", "Product Brief", "Architecture", "UX", "Spec Kernel", "Epics", "Stories", "Requirements",
    ];
}
