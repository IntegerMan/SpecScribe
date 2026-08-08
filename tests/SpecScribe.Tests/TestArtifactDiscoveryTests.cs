using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 18.5 — the IO half of Test Architect (TEA) coverage, plus the generation-level surfaces.
///
/// <para><b>Why these are constructed fixtures.</b> Story 18.1 searched and found NO repository anywhere that has
/// actually run TEA, and this repo has no <c>_bmad/tea/</c>. The ACs' "representative repository" is therefore a
/// fixture, built here in three shapes:</para>
/// <list type="number">
/// <item><b>BMM + TEA</b> — the realistic case, and the only one the D2 join design targets. TEA's own
/// <c>module-help.csv</c> declares <c>bmad-testarch-atdd</c> as <c>preceded-by: bmad-create-story:create</c>, i.e.
/// TEA is designed to COMPOSE with BMM's story workflow rather than replace it.</item>
/// <item><b>TEA-only</b> — the honest-degradation case: quality evidence, no <c>epics.md</c>, no requirements, so
/// no join is even attempted.</item>
/// <item><b>BMM-only control</b> — AC #1's "BMM support fully intact" clause. Asserts the byte-for-byte ABSENCE of
/// every Story 18.5 surface, which is the cheapest regression net available.</item>
/// </list>
///
/// <para>Artifact CONTENTS are authored from <c>trace-template.md</c>'s real grammar and
/// <c>step-05-gate-decision.md</c>'s real JSON literals, not from imagination — see
/// <see cref="TestArtifactDerivationTests"/> for the per-file upstream commit SHAs.</para></summary>
public class TestArtifactDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-tea-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string ArtifactsDir => Path.Combine(Source, "test-artifacts");
    private string IndexRoute => "index.html";
    private string TestArtifactsRoute => "test-artifacts.html";

    public TestArtifactDiscoveryTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    // ---- Fixture parts -------------------------------------------------------------------------------------

    /// <summary>VERBATIM upstream <c>src/module-help.csv</c> — all ten rows, pinned at
    /// <c>4a7522664ad4bf1c5338a1819144de458eaebecd</c>. Note the <c>module</c> column reads "Test Architecture
    /// Enterprise" while <c>module.yaml</c> says <c>name: "Test Architect"</c>; the CSV is the only on-disk label,
    /// which is precisely why neither string may be hard-coded in <c>src/</c>.</summary>
    private const string TeaCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Test Architecture Enterprise,_meta,,,,,,,,,false,https://bmad-code-org.github.io/bmad-method-test-architecture-enterprise/llms.txt,
        Test Architecture Enterprise,bmad-teach-me-testing,Teach Me Testing,TMT,Teach testing fundamentals through 7 sessions (TEA Academy).,,,0-learning,,,false,test_artifacts,progress file|session notes|certificate
        Test Architecture Enterprise,bmad-testarch-test-design,Test Design,TD,Risk-based test planning.,,,3-solutioning,,bmad-testarch-framework,false,test_artifacts,test design document
        Test Architecture Enterprise,bmad-testarch-framework,Test Framework,TF,Initialize production-ready test framework.,,,3-solutioning,bmad-testarch-test-design,bmad-testarch-ci,false,test_artifacts,framework scaffold
        Test Architecture Enterprise,bmad-testarch-ci,CI Setup,CI,Configure CI/CD quality pipeline.,,,3-solutioning,bmad-testarch-framework,,false,test_artifacts,ci config
        Test Architecture Enterprise,bmad-testarch-atdd,ATDD,AT,Generate red-phase acceptance test scaffolds before implementation.,,,4-implementation,bmad-create-story:create,bmad-dev-story,false,test_artifacts,atdd-checklist|red-phase acceptance tests
        Test Architecture Enterprise,bmad-testarch-automate,Test Automation,TA,Expand test coverage.,,,4-implementation,bmad-testarch-atdd,,false,test_artifacts,test suite
        Test Architecture Enterprise,bmad-testarch-test-review,Test Review,RV,Quality audit (0-100 scoring).,,,4-implementation,bmad-testarch-automate,,false,test_artifacts,review report
        Test Architecture Enterprise,bmad-testarch-nfr,NFR Evidence Audit,NR,Audit non-functional requirement evidence.,,,4-implementation,bmad-testarch-automate,,false,test_artifacts,nfr report
        Test Architecture Enterprise,bmad-testarch-trace,Traceability,TR,Coverage traceability and gate.,,,4-implementation,bmad-testarch-test-review,,false,test_artifacts,traceability matrix|gate decision
        """;

    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,bmad-create-story,Create Story,CS,Prepare the next story,create,,4-implementation,,,true,implementation_artifacts,story
        BMad Method,bmad-dev-story,Dev Story,DS,Execute the story,,,4-implementation,,,true,,
        """;

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders module artifacts.
        FR2: The portal reports its own interpretation boundary.

        ## Epic List

        ### Epic 18: Module coverage

        Cover a second BMad module.

        ## Epic 18: Module coverage

        ### Story 18.4: Forged ideas list page

        As a user, I want forged ideas listed.

        ### Story 18.5: Priority module baseline coverage

        As a user, I want my module's artifacts interpreted.
        """;

    /// <summary>An <c>acceptance_criteria</c> / non-synthetic / high-confidence matrix whose criterion ids resolve
    /// against the fixture's own <c>epics.md</c> — the JOINABLE case AC #1 asks for. One row deliberately does not
    /// resolve, so the "some rows do not join" path is exercised too.</summary>
    private const string JoinableMatrix = """
        ---
        stepsCompleted: ['step-03-map-criteria', 'step-05-gate-decision']
        lastStep: 'step-05-gate-decision'
        lastSaved: '2026-07-20'
        workflowType: 'testarch-trace'
        coverageBasis: 'acceptance_criteria'
        oracleConfidence: 'high'
        oracleResolutionMode: 'formal_requirements'
        ---

        # Traceability Matrix & Gate Decision - Epic 18

        ## PHASE 1: REQUIREMENTS TRACEABILITY

        ### Coverage Summary

        | Priority  | Total Criteria | FULL Coverage | Coverage % | Status   |
        | --------- | -------------- | ------------- | ---------- | -------- |
        | P0        | 2              | 2             | 100%       | PASS     |
        | P1        | 1              | 0             | 0%         | FAIL     |

        ### Detailed Mapping

        #### FR1: The portal renders module artifacts (P0)

        - **Coverage:** FULL
        - **Tests:**
          - `18.5-E2E-001` - tests/e2e/portal.spec.ts:12

        #### 18.4-AC-1: Ideas page omits when empty (P0)

        - **Coverage:** FULL
        - **Tests:**
          - `18.4-E2E-001` - tests/e2e/ideas.spec.ts:9

        #### JOURNEY-3: A reviewer opens the gate badge (P1)

        - **Coverage:** NONE

        ## PHASE 2: QUALITY GATE DECISION

        ### GATE DECISION: CONCERNS
        """;

    /// <summary>A <c>user_journeys</c> / synthetic matrix whose criterion ids LOOK joinable. Nothing may join.</summary>
    private const string SyntheticMatrix = """
        ---
        coverageBasis: 'user_journeys'
        oracleConfidence: 'medium'
        oracleResolutionMode: 'synthetic_source'
        ---

        # Traceability Matrix & Gate Decision - Release

        ### Detailed Mapping

        #### FR1: Inferred from source, not read from a requirement (P0)

        - **Coverage:** FULL
        - **Tests:**
          - `SYN-E2E-001` - tests/e2e/smoke.spec.ts:3

        ### GATE DECISION: PASS
        """;

    private const string GateDecisionJson = """
        {
          "schema_version": "0.1.0",
          "evaluated_at": "2026-07-20T14:02:11.000Z",
          "repo": "SpecScribe",
          "target": { "type": "epic", "id": null, "label": null },
          "collection_status": "COLLECTED",
          "gate_basis": "priority_thresholds",
          "gate_status": "CONCERNS",
          "rationale": "All P0 criteria met; one P1 journey has no coverage.",
          "p0_status": "MET",
          "p1_status": "NOT_MET",
          "overall_status": "MET",
          "critical_open": 0,
          "links": { "trace_report_path": "test-artifacts/traceability-matrix.md", "trace_report_url": "", "artifact_url": "", "journey_evidence_url": "" }
        }
        """;

    private const string TraceSummaryJson = """
        {
          "schema_version": "0.1.0",
          "snapshot_at": "2026-07-20T14:02:11.000Z",
          "repo": "SpecScribe",
          "collection_mode": "contract_static",
          "collection_status": "COLLECTED",
          "inventory_basis": "acceptance_criteria",
          "gate_basis": "priority_thresholds",
          "source_sha": "",
          "target": { "type": "epic", "id": null, "label": null },
          "decision_mode": "deterministic",
          "evaluator": "TEA Agent",
          "confidence": "high",
          "oracle": { "resolution_mode": "formal_requirements", "confidence": "high", "sources": ["epics.md"], "external_pointer_status": "not_used", "synthetic": false },
          "coverage": {
            "inventory": { "covered": 2, "total": 3, "pct": 66.7 },
            "priority_breakdown": {
              "P0": { "total": 2, "covered": 2, "pct": 100 },
              "P1": { "total": 1, "covered": 0, "pct": 0 },
              "P2": { "total": 0, "covered": 0, "pct": 100 },
              "P3": { "total": 0, "covered": 0, "pct": 100 }
            },
            "by_level": { "e2e": 2, "api": 0, "component": 0, "unit": 4 }
          },
          "tests": { "files": 2, "cases": 6, "skipped_cases": 0, "fixme_cases": 0, "pending_cases": 0 },
          "risk_summary": { "critical_open": 0, "high_open": 1, "medium_open": 0, "low_open": 0 },
          "heuristics": { "endpoint_gaps": 0, "auth_negative_path_status": "present", "error_path_status": "present" },
          "blockers": [],
          "recommendations": [],
          "links": { "trace_report_path": "test-artifacts/traceability-matrix.md", "trace_report_url": "", "artifact_url": "", "journey_evidence_url": "" },
          "gate_status": "CONCERNS"
        }
        """;

    // ---- Fixture builders ----------------------------------------------------------------------------------

    private void InstallModule(string code, string csv)
    {
        var dir = Path.Combine(_root, "_bmad", code);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "module-help.csv"), csv);
    }

    private void WriteEpics() =>
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);

    /// <summary>The full <c>{output_folder}/test-artifacts/</c> tree with the PINNED filenames.</summary>
    private void WriteTeaArtifacts(string matrix = JoinableMatrix, bool withJson = true)
    {
        Directory.CreateDirectory(ArtifactsDir);
        File.WriteAllText(Path.Combine(ArtifactsDir, "traceability-matrix.md"), matrix);
        File.WriteAllText(Path.Combine(ArtifactsDir, "nfr-assessment.md"), "# NFR Evidence Audit\n\nSecurity: pass.\n");
        File.WriteAllText(Path.Combine(ArtifactsDir, "test-review.md"), "# Test Quality Review\n\nScore 82/100.\n");
        if (!withJson) return;
        File.WriteAllText(Path.Combine(ArtifactsDir, "gate-decision.json"), GateDecisionJson);
        File.WriteAllText(Path.Combine(ArtifactsDir, "e2e-trace-summary.json"), TraceSummaryJson);
    }

    private TestArtifactsModel Discover(List<AdapterDiagnostic>? diagnostics = null) =>
        TestArtifactDiscovery.Discover(_root, Source, diagnostics);

    // ---- Discovery: the module-presence gate ---------------------------------------------------------------

    [Fact]
    public void Discover_ArtifactsPresentButModuleNotInstalled_FindsNothing()
    {
        // The gate is the MODULE, not the filenames: a project that happens to keep a test-review.md in a
        // test-artifacts folder, with no _bmad/tea/ anywhere, must produce no surface at all.
        WriteTeaArtifacts();

        var diagnostics = new List<AdapterDiagnostic>();
        var model = Discover(diagnostics);

        Assert.True(model.IsEmpty);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Discover_ModuleInstalledButNoArtifactsDirectory_IsOneInformationalNotice()
    {
        InstallModule("tea", TeaCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var model = Discover(diagnostics);

        Assert.True(model.IsEmpty);
        var notice = Assert.Single(diagnostics);
        Assert.Equal(AdapterDiagnosticCategory.Informational, notice.Category);
        // Story 18.5 non-goal: reading _bmad/tea/config.yaml to tell "overridden path" from "never run" apart.
        // Review patch: the message no longer claims to know WHERE the path resolves (a nested-but-inside-tree
        // directory is indistinguishable from a genuinely overridden one to this non-recursive, name-only scan).
        Assert.Contains("this scan does not look", notice.Message);
        Assert.Equal(DiagnosticAnchorRoot.Source, notice.Anchor);
    }

    [Fact]
    public void Discover_ModuleInstalledWithArtifacts_TiersEveryFile()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var model = Discover();

        Assert.False(model.IsEmpty);
        Assert.Equal("tea", model.ModuleCode);
        Assert.Equal(5, model.Artifacts.Count);
        // matrix + both JSON files
        Assert.Equal(3, model.CountIn(CoverageTier.Summarized));
        // nfr-assessment.md + test-review.md
        Assert.Equal(2, model.CountIn(CoverageTier.Rendered));
        Assert.Equal(0, model.CountIn(CoverageTier.Unsupported));
    }

    [Fact]
    public void Discover_ReadsTheGateVerdictFromAFileTheMarkdownScanCannotSee()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var model = Discover();

        // This is ADR 0020's whole point: gate-decision.json is not `*.md`, so before this story its verdict was
        // never discovered, never rendered and never diagnosed.
        Assert.NotNull(model.Gate);
        Assert.Equal("CONCERNS", model.Gate!.Status);
        Assert.Equal("CONCERNS", model.GateWord);
        Assert.Equal("NOT_MET", model.Gate.P1Status);
        Assert.NotNull(model.Trace);
        Assert.Equal(6, model.Trace!.TestCases);
    }

    [Fact]
    public void Discover_UnknownArtifactFamily_IsUnsupportedWithANotice()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();
        // `bmad-teach-me-testing` writes "progress file | session notes | certificate" into test_artifacts; the
        // filenames are unpinned upstream, so the family is discovered, named, and explicitly not interpreted.
        File.WriteAllText(Path.Combine(ArtifactsDir, "tea-academy-progress.md"), "# Session 1\n\nNotes.\n");

        var diagnostics = new List<AdapterDiagnostic>();
        var model = Discover(diagnostics);

        Assert.Equal(1, model.CountIn(CoverageTier.Unsupported));
        Assert.Contains(diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Unsupported && d.Message.Contains("not one SpecScribe models"));
    }

    [Fact]
    public void Discover_UnknownJsonSchemaMajor_IsSkipped_AndTheFileIsStillListed()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();
        File.WriteAllText(Path.Combine(ArtifactsDir, "gate-decision.json"),
            GateDecisionJson.Replace("\"0.1.0\"", "\"9.0.0\""));

        var diagnostics = new List<AdapterDiagnostic>();
        var model = Discover(diagnostics);

        Assert.Null(model.Gate);
        // The rich summary still carries a gate word, so the surface degrades rather than going blank.
        Assert.Equal("CONCERNS", model.GateWord);
        Assert.Contains(diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Skipped && d.Message.Contains("schema version this build does not understand"));
        // Discovered-but-uninterpreted, never silently dropped.
        Assert.Contains(model.Artifacts, a => a.SourceRelative.EndsWith("gate-decision.json") && a.Tier == CoverageTier.Unsupported);
    }

    [Fact]
    public void Discover_MalformedJson_IsMalformed_AndGenerationStillSucceeds()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();
        File.WriteAllText(Path.Combine(ArtifactsDir, "e2e-trace-summary.json"), "{ truncated");

        var diagnostics = new List<AdapterDiagnostic>();
        var model = Discover(diagnostics);

        Assert.Null(model.Trace);
        Assert.Contains(diagnostics, d => d.Category == AdapterDiagnosticCategory.Malformed);

        // A Malformed diagnostic DOES surface as a GenerationOutcome.Error event — that is the existing
        // MapDiagnostics contract, unchanged here — but it is non-fatal: every other page still writes, and the
        // Test Artifacts page itself still renders without the summary it could not read.
        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.True(SiteRegion.Exists(Site, TestArtifactsRoute));
        Assert.Contains(events, e => e.Outcome == GenerationOutcome.Generated && e.RelativePath == "test-artifacts.html");
        Assert.DoesNotContain(events, e =>
            e.Outcome == GenerationOutcome.Error && !e.FromAdapterDiagnostic);
    }

    [Fact]
    public void Discover_OnlyTheTwoDeclaredJsonFilenamesAreRead()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();
        // ADR 0020's non-goal, pinned: this is NOT a general "ingest any JSON" seam. An unrelated JSON sitting in
        // the same directory is neither read nor listed.
        File.WriteAllText(Path.Combine(ArtifactsDir, "playwright-report.json"), "{ \"totally\": \"unrelated\" }");

        var model = Discover();

        Assert.DoesNotContain(model.Artifacts, a => a.SourceRelative.EndsWith("playwright-report.json"));
    }

    // ---- The D2 join ---------------------------------------------------------------------------------------

    [Fact]
    public void WithJoin_BmmPlusTea_JoinsOnlyTheIdsThisProjectDefines()
    {
        InstallModule("bmm", BmmCsv);
        InstallModule("tea", TeaCsv);
        WriteEpics();
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, TestArtifactsRoute);
        // FR1 resolves against RequirementsModel.ById; 18.4-AC-1 resolves via its story-id prefix.
        Assert.Contains("Module test coverage by requirement", html);
        Assert.Contains(">FR1<", html);
        Assert.Contains(">18.4<", html);
        // JOURNEY-3 resolves to nothing and is stated as unresolved rather than shown as covering something.
        Assert.DoesNotContain(">JOURNEY-3<", html);
        Assert.Contains("could not be resolved and is left out rather than guessed at", html);
    }

    [Fact]
    public void WithJoin_SyntheticOracle_RefusesTheJoinAndSaysWhy()
    {
        InstallModule("bmm", BmmCsv);
        InstallModule("tea", TeaCsv);
        WriteEpics();
        WriteTeaArtifacts(matrix: SyntheticMatrix, withJson: false);

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, TestArtifactsRoute);
        // The matrix's own FR1 row LOOKS joinable. It must not be joined, and the page must say why in words.
        Assert.DoesNotContain("Module test coverage by requirement", html);
        Assert.Contains("is <strong>not</strong> mapped onto this project", html);
        Assert.Contains("user_journeys", html);
        Assert.Contains("An honest gap is preferable", html);
    }

    [Fact]
    public void GenerateAll_TeaOnlyRepo_StillProducesThePage_WithNoJoinAttempted()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.True(SiteRegion.Exists(Site, TestArtifactsRoute));
        var html = SiteRegion.Read(Site, TestArtifactsRoute);
        Assert.Contains("Quality gate", html);
        // No epics.md ⇒ no requirements and no stories ⇒ nothing resolves, stated rather than blank.
        Assert.DoesNotContain("Module test coverage by requirement", html);
        Assert.False(SiteRegion.Exists(Site, "traceability.html"));
    }

    // ---- Generation-level surfaces --------------------------------------------------------------------------

    [Fact]
    public void GenerateAll_WithArtifacts_WritesThePage_NavEntry_AndDashboardPanel()
    {
        InstallModule("bmm", BmmCsv);
        InstallModule("tea", TeaCsv);
        WriteEpics();
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        Assert.True(SiteRegion.Exists(Site, TestArtifactsRoute));

        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("test-artifacts.html", index);          // nav entry + quick link
        Assert.Contains("module-coverage-panel", index);        // the dashboard panel
        Assert.Contains("Module Coverage", index);
        // The panel names the module by its OWN parsed label — never a hard-coded one.
        Assert.Contains("Test Architecture Enterprise", index);
    }

    [Fact]
    public void GenerateAll_TierBadgesCarryTheirWord_NeverColourAlone()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        gen.GenerateAll();

        var html = SiteRegion.Read(Site, TestArtifactsRoute);
        foreach (var tier in Enum.GetValues<CoverageTier>())
        {
            if (tier == CoverageTier.Unsupported) continue; // no unsupported artifact in this fixture
            Assert.Contains(CoverageTiers.Word(tier), html);
            Assert.Contains(CoverageTiers.Description(tier), html);
        }
        // The gate verdict word, not just its colour class.
        Assert.Contains("CONCERNS", html);
    }

    [Fact]
    public void GenerateAll_EachMarkdownArtifactIsRenderedExactlyOnce()
    {
        // The double-render hazard Task 4 guards against. The Test Artifacts page is an INDEX: it links the page
        // the generic *.md pass already writes rather than re-rendering the document, so nothing is consumed and
        // nothing is emitted twice. Proven against the real generated site, not the model.
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        gen.GenerateAll();

        // [Story 23.6 AC #8] Asked of the route set, not of a *.html walk: the double-render this guards against
        // would now show up as two ROUTES for one document, and a walk that finds no files at all would report
        // "rendered once" as readily as "not rendered".
        var renderedNfr = SiteRegion.Routes(Site)
            .Where(r => r.EndsWith("/nfr-assessment.html", StringComparison.Ordinal) || r == "nfr-assessment.html")
            .ToList();
        Assert.Single(renderedNfr);
        Assert.Equal("test-artifacts/nfr-assessment.html", renderedNfr[0]);

        // And the list page links exactly that page.
        Assert.Contains("test-artifacts/nfr-assessment.html", SiteRegion.Read(Site, TestArtifactsRoute));
    }

    [Fact]
    public void GenerateAll_TestArtifactsFolder_IsNoLongerAnUnrecognizedTopLevelFolder()
    {
        InstallModule("tea", TeaCsv);
        WriteTeaArtifacts();

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();

        // A notice firing here is a regression signal, not cosmetic: it would claim a folder this story models
        // renders "in its own home-index section".
        Assert.DoesNotContain(events, e =>
            e.Message is { } m && m.Contains("unrecognized top-level folder") && m.Contains("test-artifacts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(events, e =>
            e.RelativePath.StartsWith("test-artifacts/", StringComparison.OrdinalIgnoreCase)
            && e.Message is { } msg && msg.Contains("unrecognized top-level folder"));
        Assert.True(DashboardViewBuilder.IsWellKnownTopLevelFolder("test-artifacts"));
    }

    // ---- The BMM-only control (AC #1: "BMM support fully intact") -------------------------------------------

    [Fact]
    public void GenerateAll_BmmOnly_ShowsNoTestArtifactSurfaceAtAll()
    {
        InstallModule("bmm", BmmCsv);
        WriteEpics();

        var gen = new SiteGenerator(Options());
        var events = gen.GenerateAll();
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

        // No page.
        Assert.False(SiteRegion.Exists(Site, TestArtifactsRoute));

        var index = SiteRegion.Read(Site, IndexRoute);
        // No nav entry, no quick link, no panel — and no notice either. Absent, never "0 artifacts". [NFR8]
        Assert.DoesNotContain("test-artifacts.html", index);
        Assert.DoesNotContain("Module Coverage", index);
        Assert.DoesNotContain("module-coverage", index);
        Assert.DoesNotContain(events, e => e.Message is { } m && m.Contains("test artifact", StringComparison.OrdinalIgnoreCase));

        // BMM's own surfaces are untouched.
        Assert.True(SiteRegion.Exists(Site, "epics.html"));
        Assert.True(SiteRegion.Exists(Site, "requirements.html"));
        Assert.True(SiteRegion.Exists(Site, "traceability.html"));
    }

    [Fact]
    public void GenerateAll_BmmOnly_ProducesByteIdenticalOutputToARunOfTheSameFixture()
    {
        // The cheapest regression net for "BMM support fully intact": the whole site, byte-compared against a
        // second from-scratch generation of the identical inputs. Any nondeterminism this story introduced into a
        // BMM-only run — an ordering change, a stray notice, a panel that leaks in — fails here.
        InstallModule("bmm", BmmCsv);
        WriteEpics();

        new SiteGenerator(Options()).GenerateAll();
        var first = SnapshotSite();

        Directory.Delete(Site, recursive: true);
        new SiteGenerator(Options()).GenerateAll();
        var second = SnapshotSite();

        Assert.Equal(first.Keys.OrderBy(k => k, StringComparer.Ordinal), second.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var (path, content) in first)
        {
            Assert.True(second[path] == content, $"{path} differs between two identical runs");
        }
    }

    /// <summary>The site's every file, with the ONE genuinely per-run token folded out: the footer's generation
    /// clock, which carries minutes and therefore differs on every page whenever two runs straddle a minute
    /// boundary. That is not determinism — it is a timestamp doing its job — and
    /// <see cref="SiteGeneratorAdapterTests"/>'s golden fingerprint folds the identical token for the identical
    /// reason (its <c>FooterClock</c> regex). Nothing else is normalized: the point of this snapshot is that any
    /// REAL byte change in a BMM-only run fails it.
    /// <para>Caught by this test flaking exactly once in a full-suite run, then passing three consecutive full
    /// runs — the "confirm across repeated runs" discipline, applied to a test rather than to a hash.</para></summary>
    private Dictionary<string, string> SnapshotSite() => Directory
        .EnumerateFiles(Site, "*", SearchOption.AllDirectories)
        .ToDictionary(
            p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)),
            p => GoldenNormalization.StripFooterClock(File.ReadAllText(p), "on <generated>"),
            StringComparer.Ordinal);
}
