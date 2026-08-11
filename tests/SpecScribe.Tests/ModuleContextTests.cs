using SpecScribe;

namespace SpecScribe.Tests;

public class CommandCatalogTests
{
    [Fact]
    public void Command_ReturnsSlashCommandForKnownStep()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });

        Assert.Equal("/bmad-create-story", catalog.Command("create-story"));
        Assert.Equal("/bmad-create-story 1.2", catalog.Command("create-story", "1.2"));
    }

    [Fact]
    public void Command_ReturnsNullForUnknownStep()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>());

        Assert.Null(catalog.Command("dev-story"));
    }
}

public class ModuleContextTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("specscribe-module-").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void WriteModule(string moduleName, string csv, params string[] installedModules)
    {
        WriteManifest(installedModules);
        WriteModuleDir(moduleName, csv);
    }

    /// <summary>Writes the installed-module manifest in the order given — the order IS the fixture for the
    /// primary-selection tests, since manifest order is what used to decide the winner. [Story 18.2]</summary>
    private void WriteManifest(params string[] installedModules)
    {
        var configDir = Path.Combine(_repo, "_bmad", "_config");
        Directory.CreateDirectory(configDir);
        var manifest = "modules:\n" + string.Join("\n", installedModules.Select(m => $"  - name: {m}\n    version: 6.0.0"));
        File.WriteAllText(Path.Combine(configDir, "manifest.yaml"), manifest);
    }

    private void WriteModuleDir(string moduleName, string csv)
    {
        var moduleDir = Path.Combine(_repo, "_bmad", moduleName);
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module-help.csv"), csv);
    }

    // VERBATIM upstream content, like every other fixture in this file. It was the last synthetic one, which
    // left AC #3's "verified against REAL module-help.csv content rather than synthetic fixtures" half-met:
    // Task 6 re-pinned GDS/TEA/CIS/BMB but not BMad Method — the one module whose behaviour AC #3 most needs
    // held still. Rows chosen to preserve every property the old synthetic fixture exercised, now with real
    // bytes: the two `bmad-create-story` rows are genuinely how upstream ships it, so `first row wins for a
    // given step` (create beating validate) is now pinned against reality rather than an invention, and
    // `bmad-prd`'s real description supplies the quoted-field-with-embedded-commas case.
    //   BMad Method  bmad-code-org/BMAD-METHOD  src/bmm-skills/module-help.csv
    //                fetched 2026-07-27, pinned at bb45db4aa4496c69239f9c0629c290fd1b072fc9
    // [Review][Patch P4; ADR 0015 Decision 7]
    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,https://docs.bmad-method.org/llms.txt,
        BMad Method,bmad-prd,Create Edit and Review PRD,PRD,"Facilitated PRD workflow — create a new PRD via coached discovery, update an existing one against a change signal, or validate a finished PRD against a checklist with an HTML findings report.",,,2-planning,bmad-product-brief,,true,planning_artifacts,prd
        BMad Method,bmad-create-story,Create Story,CS,Story cycle start: Prepare first found story in the sprint plan that is next or a specific epic/story designation.,create,,4-implementation,bmad-sprint-planning,bmad-create-story:validate,true,implementation_artifacts,story
        BMad Method,bmad-create-story,Validate Story,VS,Validates story readiness and completeness before development work begins.,validate,,4-implementation,bmad-create-story:create,bmad-dev-story,false,implementation_artifacts,story validation report
        BMad Method,bmad-dev-story,Dev Story,DS,Story cycle: Execute story implementation tasks and tests then CR then back to DS if fixes needed.,,,4-implementation,bmad-create-story:validate,,true,,
        BMad Method,bmad-code-review,Code Review,CR,Story cycle: If issues back to DS if approved then next CS or ER if epic complete.,,,4-implementation,bmad-dev-story,,false,,
        """;

    // ---- Upstream-pinned module fixtures [Story 18.2 Task 6; ADR 0015 Decision 7] --------------------
    //
    // Every constant below is VERBATIM content (header, `_meta` row, and a representative subset of the
    // data rows) from the module's own `src/module-help.csv` in the `bmad-code-org` GitHub organization,
    // re-fetched 2026-07-26 and pinned to the commit that last touched each file:
    //
    //   Game Dev Studio  bmad-module-game-dev-studio             9f947c9611cedf01f220796f65bf41a96100be0a
    //   Test Architect   bmad-method-test-architecture-enterprise 4a7522664ad4bf1c5338a1819144de458eaebecd
    //   Creative Intel.  bmad-module-creative-intelligence-suite  0a3413af3a4dc3ef9c06da79c671958b59b3b46c
    //   BMad Builder     bmad-builder (skills/module-help.csv)    a4a8483defb54ca3f42c76b6e80eed05279ed3a2
    //   BMad Method      BMAD-METHOD (src/bmm-skills/module-help.csv)
    //                    bb45db4aa4496c69239f9c0629c290fd1b072fc9  — added 2026-07-27 by code review [Patch P4].
    //                    BMM was the ONE module Task 6 left synthetic, and it is the module AC #3 most needs
    //                    held still. Note the real path is `src/bmm-skills/`, not the `src/` the ADR's
    //                    evidence table implies. Its constant sits with the tests below, not in this block,
    //                    because several fixtures are derived from it by string replacement.
    //
    // Rows are subset, never edited: the module label, the skill ids and the `_meta` row's docs-URL
    // output-location are exactly what ships. This matters — synthetic `gds-*` rows are precisely why the
    // skill-prefix identity bug went unnoticed, since BMad's docs advertise GDS commands as `/bmgd-*` and
    // the suite would have passed had that been the on-disk reality. It is not: GDS really does use `gds-*`
    // (BMGD is branding), and every OTHER first-party module really is `bmad-*` prefixed.

    private const string GdsCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Game Dev Studio,_meta,,,,,,,,,false,https://game-dev-studio-docs.bmad-method.org/llms.txt,
        Game Dev Studio,gds-gdd,Game Design Document,GDD,"Create, update, or validate a game's GDD — the primary design artifact covering pillars, mechanics, progression, levels, art, audio, and development epics.",,,2-design,,,false,planning_artifacts,gdd
        Game Dev Studio,gds-create-story,Create Story,CS,Create Story with comprehensive context for developer agent implementation.,,,4-production,gds-sprint-planning,,true,implementation_artifacts,story
        Game Dev Studio,gds-dev-story,Dev Story,DS,Execute Dev Story workflow implementing tasks and tests.,,,4-production,gds-create-story,,true,,
        """;

    private const string TeaCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Test Architecture Enterprise,_meta,,,,,,,,,false,https://bmad-code-org.github.io/bmad-method-test-architecture-enterprise/llms.txt,
        Test Architecture Enterprise,bmad-testarch-nfr,NFR Evidence Audit,NR,Audit non-functional requirement evidence,,,4-implementation,bmad-testarch-automate,,false,test_artifacts,nfr report
        Test Architecture Enterprise,bmad-testarch-trace,Traceability,TR,Coverage traceability and gate,,,4-implementation,bmad-testarch-test-review,,false,test_artifacts,traceability matrix|gate decision
        """;

    private const string CisCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Creative Intelligence Suite,_meta,,,,,,,,,false,https://cis-docs.bmad-method.org/llms.txt,
        Creative Intelligence Suite,bmad-cis-innovation-strategy,Innovation Strategy,IS,Identify disruption opportunities and architect business model innovation.,,,anytime,,,false,output_folder,innovation strategy
        Creative Intelligence Suite,bmad-brainstorming,Brainstorming,BS,Facilitate brainstorming sessions using one or more techniques.,,,anytime,,,false,output_folder,brainstorming session results
        Creative Intelligence Suite,bmad-cis-storytelling,Storytelling,ST,Craft compelling narratives using proven story frameworks and techniques.,,,anytime,,,false,output_folder,narrative/story
        """;

    private const string BmbCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Builder,_meta,,,,,,,,,false,https://bmad-builder-docs.bmad-method.org/llms.txt,
        BMad Builder,bmad-bmb-setup,Setup Builder Module,SB,"Install or update BMad Builder module config and help entries.",configure,"{-H: headless mode}|{inline values: skip prompts with provided values}",anytime,,,false,{project-root}/_bmad,config.yaml and config.user.yaml
        BMad Builder,bmad-module-builder,Ideate Module,IM,"Brainstorm and plan a BMad module — explore ideas, decide architecture, and produce a build plan.",ideate-module,"{description: initial module idea}",anytime,,bmad-module-builder:create-module,false,bmad_builder_reports,module plan
        BMad Builder,bmad-module-builder,Create Module,CM,"Scaffold module infrastructure into built skills, making them an installable BMad module.",create-module,"{-H: headless mode}|{path: skills folder or single SKILL.md}",anytime,bmad-module-builder:ideate-module,,false,bmad_builder_output_folder,setup skill
        """;

    [Fact]
    public void Detect_ReadsBmadMethodModuleAndCommands()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("BMad Method", ctx.Commands.ModuleLabel);
        Assert.Equal("/bmad-create-story", ctx.Commands.Command("create-story"));
        Assert.Equal("/bmad-dev-story 1.2", ctx.Commands.Command("dev-story", "1.2"));
        Assert.Contains(ctx.Docs, d => d.FileName == "prd.md");
    }

    [Fact]
    public void Detect_ReadsGameDevStudioModuleAndCommands()
    {
        WriteModule("gds", GdsCsv, "core", "gds");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.GameDevStudio, ctx.Module);
        Assert.Equal("/gds-dev-story", ctx.Commands.Command("dev-story"));
        Assert.Contains(ctx.Docs, d => d.FileName == "gdd.md");
    }

    [Fact]
    public void Detect_ReturnsNoneWhenNoBmadFolder()
    {
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.Unknown, ctx.Module);
        Assert.True(ctx.Commands.IsEmpty);
        Assert.Empty(ctx.Docs);
        Assert.Empty(ctx.Glossary);
    }

    // ---- Story 10.3: the adapter-supplied glossary seam (AC2) ----------------------------------------

    [Fact]
    public void GlossaryFor_BmadMethod_ReturnsFrNfrAcAdrPrdAcronymSet()
    {
        var glossary = ModuleContext.GlossaryFor(BmadModule.BmadMethod);

        foreach (var term in new[] { "FR", "NFR", "AC", "ADR", "PRD" })
        {
            Assert.Contains(glossary, g => g.Term == term && g.IsAcronym);
        }
        // Longer terms are glossary-only, not acronym-shaped (no <abbr> expansion for these).
        Assert.Contains(glossary, g => g.Term == "spec kernel" && !g.IsAcronym);
        Assert.Contains(glossary, g => g.Term == "quick-dev" && !g.IsAcronym);
    }

    [Fact]
    public void GlossaryFor_GameDevStudio_ReturnsItsOwnVocabulary_NotBmadTerms()
    {
        var glossary = ModuleContext.GlossaryFor(BmadModule.GameDevStudio);

        Assert.Contains(glossary, g => g.Term == "GDD" && g.IsAcronym);
        Assert.DoesNotContain(glossary, g => g.Term is "FR" or "NFR" or "AC" or "ADR" or "PRD");
    }

    [Fact]
    public void GlossaryFor_Unknown_ReturnsEmpty()
    {
        Assert.Empty(ModuleContext.GlossaryFor(BmadModule.Unknown));
    }

    [Fact]
    public void Detect_ReadsBmadMethodModule_PopulatesGlossary()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
    }

    [Fact]
    public void Detect_FallsBackToOnDiskCsvWhenManifestMissing()
    {
        // No manifest.yaml written — detection should still find the module-help.csv on disk.
        var moduleDir = Path.Combine(_repo, "_bmad", "bmm");
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module-help.csv"), BmmCsv);

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("/bmad-code-review", ctx.Commands.Command("code-review"));
    }

    // ---- SDD help page: independent presence helpers ------------------------------------------------

    [Fact]
    public void IsMethodPresent_TrueWhenBmmInstalled()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");
        Assert.True(ModuleContext.IsMethodPresent(_repo));
    }

    [Fact]
    public void IsMethodPresent_FalseWhenNoBmadFolder()
    {
        Assert.False(ModuleContext.IsMethodPresent(_repo));
    }

    [Fact]
    public void IsGdsPresent_TrueWhenGdsInstalled()
    {
        WriteModule("gds", GdsCsv, "core", "gds");
        Assert.True(ModuleContext.IsGdsPresent(_repo));
    }

    [Fact]
    public void IsGdsPresent_FalseWhenNoBmadFolder()
    {
        Assert.False(ModuleContext.IsGdsPresent(_repo));
    }

    [Fact]
    public void DualInstall_BothPresent()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm", "gds");
        var gdsDir = Path.Combine(_repo, "_bmad", "gds");
        Directory.CreateDirectory(gdsDir);
        File.WriteAllText(Path.Combine(gdsDir, "module-help.csv"), GdsCsv);

        Assert.True(ModuleContext.IsMethodPresent(_repo));
        Assert.True(ModuleContext.IsGdsPresent(_repo));
    }

    [Fact]
    public void MethodPresent_GdsAbsent_OnlyMethodTrue()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");
        Assert.True(ModuleContext.IsMethodPresent(_repo));
        Assert.False(ModuleContext.IsGdsPresent(_repo));
    }

    // ---- Story 18.2 / ADR 0015: identity comes from the module CODE, never the skill prefix -----------
    // Defect A (false presence): every first-party BMad module except GDS prefixes its skills `bmad-`, so
    // CIS/TEA/BMB used to be identified as BmadMethod and served BMM's entire glossary. These fixtures use
    // the modules' REAL skill ids on purpose — inventing `cis-*`/`tea-*` ids would make them pass for the
    // wrong reason and hide the bug.

    [Theory]
    [InlineData("cis", "Creative Intelligence Suite")]
    [InlineData("tea", "Test Architecture Enterprise")]
    [InlineData("bmb", "BMad Builder")]
    public void Detect_ModuleWithBmadSkillPrefix_IsUnmodeled_NotBmadMethod(string code, string label)
    {
        WriteModule(code, CsvFor(code), "core", code);

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        // The identity itself: unmodeled, and emphatically NOT a BmadMethod fallback. Unmodeled is its own
        // case, NOT a reuse of Unknown — Unknown means detection FAILED and prints "Unknown (not detected)"
        // on the diagnostics page, which for this repo would replace a true label with a false one.
        Assert.NotEqual(BmadModule.BmadMethod, ctx.Module);
        Assert.NotEqual(BmadModule.Unknown, ctx.Module);
        Assert.Equal(BmadModule.Unmodeled, ctx.Module);
        Assert.False(ctx.IsModeled);
        Assert.Equal(code, ctx.Code);

        // Populated-but-unmodeled: the real label and the parsed catalog survive; docs and glossary do not.
        Assert.Equal(label, ctx.Commands.ModuleLabel);
        Assert.False(ctx.Commands.IsEmpty);
        Assert.Empty(ctx.Docs);
        Assert.Empty(ctx.Glossary);
        Assert.True(ctx.IsUnmodeled);
    }

    /// <summary>The regression pin for ADR 0015 Decision 1: TEA's skills are ALL <c>bmad-*</c>, so this fails
    /// the moment identity keys off the skill prefix again.</summary>
    [Fact]
    public void Detect_TeaSkillsAreAllBmadPrefixed_StillDoesNotInheritBmadMethodVocabulary()
    {
        WriteModule("tea", TeaCsv, "core", "tea");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.StartsWith("/bmad-", ctx.Commands.Command("testarch-trace")!);
        Assert.DoesNotContain(ctx.Glossary, g => g.Term is "FR" or "NFR" or "AC" or "ADR" or "PRD");
        Assert.DoesNotContain(ctx.Docs, d => d.FileName == "prd.md");
    }

    [Fact]
    public void Detect_KnownModules_CarryTheirCode()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal("bmm", ctx.Code);
        // The former second assertion here — a SECOND Detect call asserting !IsUnmodeled — was implied by
        // Code == "bmm" and added no coverage. Replaced with the label, which is independent state and is what
        // the Decision-1c cross-check now reads. [Review][Patch P7]
        Assert.Equal("BMad Method", ctx.Commands.ModuleLabel);
    }

    [Fact]
    public void None_HasNoCode_AndIsNotUnmodeled()
    {
        // No module at all is a DIFFERENT state from "a module we don't model" — how-to-read renders a named
        // acknowledgement for the latter and omits the section entirely for the former.
        Assert.Null(ModuleContext.None.Code);
        Assert.False(ModuleContext.None.IsUnmodeled);
    }

    // Defect B (live regression to shipped BMM support): with an auxiliary module ahead of bmm in the
    // installed manifest, ChoosePrimary used to return the manifest-first candidate, demoting BMM and
    // stripping EVERY BMM command suggestion portal-wide — while IsMethodPresent still reported True.

    [Theory]
    [InlineData("cis")]
    [InlineData("tea")]
    [InlineData("bmb")]
    public void Detect_AuxiliaryModuleAheadOfBmmInManifest_KeepsBmadMethodPrimary(string aux)
    {
        WriteManifest("core", aux, "bmm");
        WriteModuleDir(aux, CsvFor(aux));
        WriteModuleDir("bmm", BmmCsv);

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("/bmad-create-story", ctx.Commands.Command("create-story"));
        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
        Assert.Contains(ctx.Docs, d => d.FileName == "prd.md");

        // AC #2's consistency clause: "Detected" and the selected primary must agree.
        Assert.True(ModuleContext.IsMethodPresent(_repo));
    }

    [Fact]
    public void Detect_AuxiliaryModuleAheadOfGds_KeepsGameDevStudioPrimary()
    {
        WriteManifest("core", "tea", "gds");
        WriteModuleDir("tea", TeaCsv);
        WriteModuleDir("gds", GdsCsv);

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.GameDevStudio, ctx.Module);
        Assert.Equal("/gds-dev-story", ctx.Commands.Command("dev-story"));
        Assert.True(ModuleContext.IsGdsPresent(_repo));
    }

    [Fact]
    public void Detect_BmmAndGds_SourceShapeStillBreaksTheTie_NotManifestOrder()
    {
        // The looksLikeGame tie-break BETWEEN the two modeled modules is separate and correct — it must
        // survive the ranking change, in both directions.
        WriteManifest("core", "bmm", "gds");
        WriteModuleDir("bmm", BmmCsv);
        WriteModuleDir("gds", GdsCsv);

        var gameish = ModuleContext.Detect(_repo, new[] { Path.Combine("planning-artifacts", "gdd.md") });
        Assert.Equal(BmadModule.GameDevStudio, gameish.Module);

        var planningish = ModuleContext.Detect(_repo, new[] { Path.Combine("planning-artifacts", "prd.md") });
        Assert.Equal(BmadModule.BmadMethod, planningish.Module);
    }

    [Fact]
    public void Detect_TeaBeforeBmmAndGds_NeitherModeledModuleIsDemoted()
    {
        WriteManifest("core", "tea", "gds", "bmm");
        WriteModuleDir("tea", TeaCsv);
        WriteModuleDir("gds", GdsCsv);
        WriteModuleDir("bmm", BmmCsv);

        var ctx = ModuleContext.Detect(_repo, new[] { Path.Combine("planning-artifacts", "gdd.md") });

        Assert.Equal(BmadModule.GameDevStudio, ctx.Module);
        Assert.True(ModuleContext.IsMethodPresent(_repo));
        Assert.True(ModuleContext.IsGdsPresent(_repo));
    }

    /// <summary>Install dirs are matched the way the rest of the file matches module codes — case-insensitively.</summary>
    [Fact]
    public void Detect_ModuleDirectoryCasing_DoesNotChangeIdentity()
    {
        WriteModule("BMM", BmmCsv, "core", "BMM");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
        // Code is stored lower-invariant so comparison is case-insensitive everywhere. Pinned because the
        // lower-casing was previously unasserted, and a diagnostic PATH built from it named a file that does
        // not exist on a case-sensitive filesystem — see the anchor test below. [Review][Patch P6]
        Assert.Equal("bmm", ctx.Code);
    }

    [Fact]
    public void Detect_DiagnosticAnchor_UsesTheRealDirectoryCasing_NotTheLowercasedCode()
    {
        // RepoRelativeCsv used to be built from the lower-invariant code, so a repo whose install directory is
        // `_bmad/TEA/` produced the anchor `_bmad/tea/module-help.csv` — a path that resolves to nothing on
        // Linux/macOS, which is the same wrong-root failure DiagnosticAnchorRoot.Repo was added to prevent.
        // [Review][Patch P6]
        WriteModule("TEA", TeaCsv, "core", "TEA");

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.True(ctx.IsUnmodeled);
        var notice = Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Informational);
        Assert.Equal("_bmad/TEA/module-help.csv", notice.RelativePath);
        Assert.Equal(DiagnosticAnchorRoot.Repo, notice.Anchor);
    }

    // ---- Review patches P1 / P9 / P10: the label is evidence, not an identity oracle -------------------

    [Fact]
    public void BuildContext_CsvWithNoModuleColumn_YieldsNoLabel_NotTheLiteralBMad()
    {
        // The label used to default to the literal "BMad" at the parse site, so a catalog with no `module`
        // header column (only `skill` is required to parse) made CommandCatalog.HasLabel true for every
        // context — rendering "This project uses the BMad module", the exact false claim ADR 0015 Decision 2b
        // exists to prevent, and leaving all three HasLabel guards unreachable. [Review][Patch P1]
        const string noModuleColumn = """
            skill,display-name,menu-code,description
            acme-create-story,Create Story,CS,Prepare the next story
            acme-dev-story,Dev Story,DS,Execute the story
            """;
        WriteModule("acme", noModuleColumn, "core", "acme");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.True(ctx.IsUnmodeled);
        Assert.Equal(string.Empty, ctx.Commands.ModuleLabel);
        Assert.False(ctx.Commands.HasLabel);
        // The catalog itself still parses — an absent label costs the module its NAME, not its commands.
        Assert.Equal("/acme-create-story", ctx.Commands.Command("create-story"));
    }

    [Fact]
    public void BuildContext_ModeledCodeWithNoLabelAtAll_IsNotDemoted()
    {
        // An ABSENT label is not evidence of squatting. Demoting on it would strip a genuine BMM install of
        // its docs, glossary and commands because of a missing CSV column. [Review][Patch P1 + P9]
        const string bmmNoLabel = """
            skill,display-name,menu-code,description
            bmad-create-story,Create Story,CS,Prepare the next story
            """;
        WriteModule("bmm", bmmNoLabel, "core", "bmm");

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
        Assert.DoesNotContain(diagnostics, d => d.Category == AdapterDiagnosticCategory.Unsupported);
    }

    [Theory]
    [InlineData("BMad Method")]           // exact — the shipped case
    [InlineData("BMad Method v6")]        // a plausible upstream version suffix
    [InlineData("BMad  Method")]          // interior whitespace drift
    [InlineData("BMM: BMad Method")]      // the "BMGD: …" branding shape BMad's own module.yaml files use
    public void BuildContext_ModeledCodeWithADriftedLabel_IsNotDemoted(string label)
    {
        // Exact matching made the shipped happy path depend on a third-party display string, and ADR 0015
        // itself documents that BMad's labels drift. A cosmetic upstream rename must not strip a real install
        // of its docs, glossary, site-wide abbreviation expansion and command legend. [Review][Patch P9]
        WriteModule("bmm", BmmCsv.Replace("BMad Method", label), "core", "bmm");

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
        Assert.DoesNotContain(diagnostics, d => d.Category == AdapterDiagnosticCategory.Unsupported);
    }

    [Fact]
    public void Detect_SquatterAtAModeledCode_NeverDemotesAGenuineModeledModuleBelowIt()
    {
        // AC #2's first clause. Ranking is computed from CODES before any label is parsed, so a minted module
        // squatting `_bmad/gds/` outranks a genuine `_bmad/bmm/` the moment the source tree carries a game
        // hint. Detect used to break on the first non-null BuildContext — and a demoted context is non-null —
        // so the squatter took the primary slot and BMM, a MODELED module, was demoted below an auxiliary one:
        // Defect B's exact symptom through a different door. [Review][Patch P10]
        const string squatter = """
            module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
            Totally Not GDS,_meta,,,,,,,,,false,url,
            Totally Not GDS,gds-something,Something,SO,Do a thing,,,,,,false,output_folder,
            """;
        WriteManifest("core", "gds", "bmm");
        WriteModuleDir("gds", squatter);
        WriteModuleDir("bmm", BmmCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        // The game hint ranks gds ABOVE bmm — this is the ordering that used to lose BMM.
        var ctx = ModuleContext.Detect(_repo, new[] { "gdds/gdd.md" }, diagnostics);

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("bmm", ctx.Code);
        Assert.Equal("/bmad-create-story", ctx.Commands.Command("create-story"));
        Assert.Contains(ctx.Glossary, g => g.Term == "FR");
        Assert.Contains(ctx.Docs, d => d.FileName == "prd.md");
        // The squatter is still reported — skipped, not silently ignored.
        Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Unsupported);
    }

    [Fact]
    public void Detect_SquatterIsTheOnlyInstall_StillYieldsItsContext_NotNone()
    {
        // The demoted candidate is a LAST-RESORT fallback: skipping it entirely would leave a repo whose only
        // install is a squatter with no context at all, losing its real label and parsed catalog. [Patch P10]
        const string squatter = """
            module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
            Totally Not GDS,_meta,,,,,,,,,false,url,
            Totally Not GDS,gds-something,Something,SO,Do a thing,,,,,,false,output_folder,
            """;
        WriteModule("gds", squatter, "core", "gds");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.Unmodeled, ctx.Module);
        Assert.Equal("Totally Not GDS", ctx.Commands.ModuleLabel);
        Assert.Empty(ctx.Glossary);
    }

    // ---- Story 18.2 / ADR 0015: the open-world guards ------------------------------------------------

    [Theory]
    [InlineData("scripts")]
    [InlineData("custom")]
    [InlineData("_config")]
    public void Detect_ReservedBmadChild_IsNeverAModule_EvenCarryingAModuleHelpCsv(string reserved)
    {
        // "The directory name IS the module code" would otherwise GUARANTEE accepting _bmad/scripts/ as a
        // module the instant anything dropped a module-help.csv there. Skipped silently — not an error.
        WriteManifest("core", "bmm");
        WriteModuleDir("bmm", BmmCsv);
        WriteModuleDir(reserved, TeaCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("bmm", ctx.Code);
        Assert.Empty(diagnostics); // silent: a reserved name is not a diagnosable condition
    }

    [Fact]
    public void Detect_MintedModuleSquattingAModeledCode_IsDemotedToUnmodeled_AndReported()
    {
        // Nothing stops a BMad Builder-minted module installing at _bmad/gds/. Without the label cross-check
        // it would silently inherit Game Dev Studio's docs and glossary.
        const string squatter = """
            module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
            Totally Not GDS,_meta,,,,,,,,,false,url,
            Totally Not GDS,gds-something,Something,SO,Do a thing,,,,,,false,output_folder,
            """;
        WriteModule("gds", squatter, "core", "gds");

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal(BmadModule.Unmodeled, ctx.Module);
        Assert.Empty(ctx.Docs);
        Assert.Empty(ctx.Glossary);
        Assert.DoesNotContain(ctx.Glossary, g => g.Term == "GDD");

        var mismatch = Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Unsupported);
        Assert.Contains("Totally Not GDS", mismatch.Message);
        Assert.Contains("Game Dev Studio", mismatch.Message);
        Assert.Equal(DiagnosticAnchorRoot.Repo, mismatch.Anchor);
    }

    [Fact]
    public void Detect_ManifestAndDiskDisagree_TheSetIsTheirUnion()
    {
        // The disk scan used to fire ONLY when the manifest produced zero candidates, so a manifest naming
        // bmm beside an installed _bmad/tea/ never saw TEA — while IsModulePresent (OR semantics) said it
        // was present. Two answers to one question; now the disk scan is not a fallback.
        WriteManifest("core", "bmm");
        WriteModuleDir("bmm", BmmCsv);
        WriteModuleDir("tea", TeaCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        // BMM still wins the primary slot (rank, not discovery order)...
        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        // ...but TEA is now visible, and is reported rather than invisible.
        //
        // Informational, NOT Skipped: this notice fires at ONE non-primary module, i.e. the ordinary healthy
        // BMM+TEA install, and Skipped renders at Warning severity — a correctly configured repo must not show
        // a warning. The threshold stays at one because the explanation is what a BMM+TEA user needs.
        // [Review][Patch P13; owner call D5]
        var secondary = Assert.Single(
            diagnostics, d => d.Category == AdapterDiagnosticCategory.Informational);
        Assert.Contains("tea", secondary.Message);
        Assert.Equal(DiagnosticAnchorRoot.Repo, secondary.Anchor);
        Assert.DoesNotContain(diagnostics, d => d.Category == AdapterDiagnosticCategory.Skipped);
        // A MODELED primary does publish planning docs and a glossary, so the provenance clause says so.
        Assert.Contains("planning docs, glossary and workflow commands come from 'bmm'", secondary.Message);
    }

    [Fact]
    public void Detect_UnparseableHigherRanked_IsNotAlsoReportedAsMerelyNotPrimary()
    {
        // The `others` set used to be "every index except the winner", which swept in the higher-ranked
        // candidates that had just FAILED TO PARSE. So an unreadable bmm beside a valid tea produced two
        // notices about bmm: one saying its catalog could not be parsed, and one saying it merely lost a
        // ranking. The second was false, and it told the reader the ranking worked as designed.
        // [Review][Patch P3]
        WriteManifest("core", "bmm", "tea");
        WriteModuleDir("bmm", "not,a,valid\ncatalog");
        WriteModuleDir("tea", TeaCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal("tea", ctx.Code);
        var malformed = Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Malformed);
        Assert.Contains("bmm", malformed.Message);
        // The whole point: no second notice re-describing bmm as a ranking loser.
        Assert.DoesNotContain(
            diagnostics,
            d => d.Category == AdapterDiagnosticCategory.Informational && d.Message.Contains("are not the primary"));
    }

    [Fact]
    public void Detect_UnmodeledPrimary_DoesNotClaimItPublishesDocsOrGlossary()
    {
        // Two unmodeled modules: the secondary notice and the unmodeled notice render on the same diagnostics
        // page, so the first must not assert that "planning docs, glossary and workflow commands come from"
        // a module the second says publishes neither. [Review][Patch P3]
        WriteManifest("core", "tea", "cis");
        WriteModuleDir("tea", TeaCsv);
        WriteModuleDir("cis", CisCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.True(ctx.IsUnmodeled);
        var secondary = Assert.Single(diagnostics, d => d.Message.Contains("are not the primary"));
        Assert.DoesNotContain("planning docs, glossary", secondary.Message);
        Assert.Contains("publishes no planning docs or glossary", secondary.Message);
    }

    [Fact]
    public void Detect_AuxiliaryOnlyRepo_PicksTheSameWinnerRegardlessOfManifestOrder()
    {
        // No modeled module installed: the winner must still be reproducible, so it is ordinal by CODE.
        // Discovery order (manifest order, or the platform-dependent Directory.EnumerateDirectories order on
        // the disk path) is never a tiebreak.
        WriteManifest("core", "tea", "cis");
        WriteModuleDir("tea", TeaCsv);
        WriteModuleDir("cis", CisCsv);
        var first = ModuleContext.Detect(_repo, Array.Empty<string>());

        WriteManifest("core", "cis", "tea");
        var second = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal("cis", first.Code); // ordinal: "cis" < "tea"
        Assert.Equal(first.Code, second.Code);
        Assert.Equal(BmadModule.Unmodeled, first.Module);
    }

    [Fact]
    public void Detect_UnparseableHigherRankedModule_IsReported_AndNeverPromotesALowerRankedOne()
    {
        // BMM outranks TEA. If BMM's CSV won't parse, TEA inherits the slot only because BMM is GONE, not
        // because the rank changed — and the unreadable module is reported rather than silently dropped.
        WriteManifest("core", "bmm", "tea");
        WriteModuleDir("bmm", "not,a,valid\ncatalog");
        WriteModuleDir("tea", TeaCsv);

        var diagnostics = new List<AdapterDiagnostic>();
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.Equal("tea", ctx.Code);
        var malformed = Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Malformed);
        Assert.Contains("bmm", malformed.RelativePath);
        Assert.Equal(DiagnosticAnchorRoot.Repo, malformed.Anchor);
    }

    [Fact]
    public void Detect_UnmodeledPrimary_EmitsExactlyOneInformationalNamingCodeAndLabel()
    {
        WriteModule("tea", TeaCsv, "core", "tea");

        var diagnostics = new List<AdapterDiagnostic>();
        ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        var info = Assert.Single(diagnostics, d => d.Category == AdapterDiagnosticCategory.Informational);
        Assert.Contains("'tea'", info.Message);
        Assert.Contains("Test Architecture Enterprise", info.Message);
        Assert.Equal("_bmad/tea/module-help.csv", info.RelativePath);
        Assert.Equal(DiagnosticAnchorRoot.Repo, info.Anchor);
    }

    [Fact]
    public void Detect_ModeledPrimary_EmitsNoInformationalNotice()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");

        var diagnostics = new List<AdapterDiagnostic>();
        ModuleContext.Detect(_repo, Array.Empty<string>(), diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Category == AdapterDiagnosticCategory.Informational);
    }

    [Fact]
    public void CommandCatalogEmpty_CarriesNoLabel_SoNoSurfaceCanNameAModuleThatIsNotThere()
    {
        // It used to carry the placeholder "BMad". ModuleContext.None IS this instance, so any surface that
        // names the module would have announced "This project uses the BMad module" on a repo with no
        // _bmad/ install at all — a worse false claim than saying nothing.
        Assert.Equal(string.Empty, CommandCatalog.Empty.ModuleLabel);
        Assert.False(CommandCatalog.Empty.HasLabel);
        Assert.False(ModuleContext.None.Commands.HasLabel);
    }

    private static string CsvFor(string code) => code switch
    {
        "cis" => CisCsv,
        "tea" => TeaCsv,
        "bmb" => BmbCsv,
        "gds" => GdsCsv,
        "_config" or "custom" or "scripts" => TeaCsv,
        _ => BmmCsv,
    };

    [Fact]
    public void IsMethodPresent_TrueWhenManifestListsBmmWithoutCsv()
    {
        // Manifest-only OR signal: module listed but module-help.csv missing.
        var bmadRoot = Path.Combine(_repo, "_bmad");
        Directory.CreateDirectory(Path.Combine(bmadRoot, "_config"));
        Directory.CreateDirectory(Path.Combine(bmadRoot, "bmm"));
        File.WriteAllText(Path.Combine(bmadRoot, "_config", "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: bmm\n    version: 6.0.0");

        Assert.True(ModuleContext.IsMethodPresent(_repo));
        Assert.False(ModuleContext.IsGdsPresent(_repo));
    }
}

public class BmadCommandsTests
{
    private static readonly CommandCatalog BmmCatalog = new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["correct-course"] = "/bmad-correct-course",
        ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
    });

    private static readonly CommandCatalog BmmWithoutCorrectCourse = new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
    });

    private static StoryInfo Story(string id, string? status, string? workflowCommandArgument = null) => new()
    {
        Id = id,
        EpicNumber = int.Parse(id.Split('.')[0]),
        Title = "A story",
        UserStoryHtml = "",
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
        WorkflowCommandArgument = workflowCommandArgument,
    };

    private static int CountClass(string html, string cssClass) =>
        html.Split($"class=\"{cssClass}\"", StringSplitOptions.None).Length - 1;

    private static int CountNextStepCards(string html) =>
        html.Split("class=\"next-step-card ", StringSplitOptions.None).Length - 1;

    private static int CountNextStepPrimary(string html) =>
        html.Split("next-step-card-primary", StringSplitOptions.None).Length - 1;

    [Fact]
    public void RenderNextSteps_UsesDetectedModuleCommands()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("1.2", "ready-for-dev"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        // Nothing has been implemented yet for a ready-for-dev story, so code review isn't a valid next step.
        Assert.DoesNotContain("/bmad-code-review 1.2", html);
        Assert.Contains("Next Steps", html);
        Assert.DoesNotContain("(BMad Method)", html);
        Assert.DoesNotContain("/gds-", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.Contains("next-steps-cards", html);
        Assert.DoesNotContain("Other actions", html);
    }

    [Fact]
    public void RenderNextSteps_GsdCatalog_UsesNativePhaseArgument()
    {
        var gsd = new CommandCatalog("GSD Core", new Dictionary<string, string>
        {
            ["dev-story"] = "/gsd-execute-phase",
            ["code-review"] = "/gsd-code-review",
        }, usesPhaseArguments: true);
        var story = Story("3.1", "ready-for-dev", workflowCommandArgument: "2.1");

        var html = WorkflowCommands.RenderNextSteps(story, gsd);

        Assert.Contains("/gsd-execute-phase 2.1", html);
        Assert.DoesNotContain("/gsd-execute-phase 3.1", html);
        Assert.DoesNotContain("/bmad-", html);
    }

    [Fact]
    public void RenderNextSteps_PlannedGsdPlanExplainsPhaseScopedExecution()
    {
        var gsd = new CommandCatalog("GSD Core", new Dictionary<string, string>
        {
            ["dev-story"] = "/gsd:execute-phase",
        }, usesPhaseArguments: true);
        var plan = new StoryInfo
        {
            Id = "3.1",
            EpicNumber = 3,
            NativeDisplayName = "Plan 2.1",
            Title = "A story",
            UserStoryHtml = string.Empty,
            AcBlocksHtml = Array.Empty<string>(),
            Status = "drafted",
            WorkflowCommandArgument = "2.1",
            ArtifactOutputPath = "epics/story-3-1.html",
        };

        var html = WorkflowCommands.RenderNextSteps(plan, gsd);

        Assert.Contains("Execution scope", html);
        Assert.Contains("Plan 2.1 is executed as part of Phase 2.1", html);
        Assert.Contains("does not provide a plan-level execution command", html);
        Assert.DoesNotContain("/gsd:execute-phase", html);
    }

    [Fact]
    public void RenderNextSteps_OmitsPanelWhenModuleUndetected()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("1.2", "ready-for-dev"), CommandCatalog.Empty);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void RenderNextSteps_ReviewStory_SuggestsOnlyCodeReviewWithStoryId()
    {
        // Story pages never suggest drafting other stories or retrospectives — those are epic/project
        // moves. The catalog includes both commands to prove they're withheld, not merely uninstalled.
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["code-review"] = "/bmad-code-review",
            ["retrospective"] = "/bmad-retrospective",
            ["correct-course"] = "/bmad-correct-course",
        });

        var html = WorkflowCommands.RenderNextSteps(Story("2.1", "review"), catalog);

        Assert.Contains("/bmad-code-review 2.1", html);
        Assert.DoesNotContain("/bmad-create-story", html);
        Assert.DoesNotContain("/bmad-retrospective", html);
        Assert.Contains("/bmad-correct-course", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.DoesNotContain("Other actions", html);
        Assert.True(html.IndexOf("next-step-card-primary", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 2.1", StringComparison.Ordinal));
        Assert.True(html.IndexOf("/bmad-code-review 2.1", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-correct-course", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderNextSteps_InProgressStory_CarriesStoryIdOnCodeReview()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.Contains("/bmad-correct-course", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.Equal(3, CountNextStepCards(html));
        Assert.DoesNotContain("Other actions", html);
        Assert.True(html.IndexOf("/bmad-dev-story 1.2", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal));
        Assert.True(html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-correct-course", StringComparison.Ordinal));
        Assert.Contains("next-step-desc", html);
    }

    [Fact]
    public void RenderNextSteps_InProgress_PromotesAlternateWhenPrimaryMissing()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["code-review"] = "/bmad-code-review",
            ["correct-course"] = "/bmad-correct-course",
        });

        var html = WorkflowCommands.RenderNextSteps(Story("1.2", "in-progress"), catalog);

        Assert.DoesNotContain("/bmad-dev-story", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.True(html.IndexOf("next-step-card-primary", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal));
        Assert.DoesNotContain("Other actions", html);
        Assert.Contains("/bmad-correct-course", html);
    }

    [Fact]
    public void RenderNextSteps_UnplannedStory_StillSuggestsDraftingItsOwnPlan()
    {
        // The one create-story a story page keeps: drafting the story being viewed, with its own id.
        var html = WorkflowCommands.RenderNextSteps(Story("3.2", null), BmmCatalog);

        Assert.Contains("/bmad-create-story 3.2", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.DoesNotContain("check-implementation-readiness", html);
    }

    [Fact]
    public void RenderNextSteps_UnplannedFirstStory_CreateStoryIsPrimary_ReadinessIsAlternate()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("3.1", null), BmmCatalog);

        Assert.Contains("/bmad-create-story 3.1", html);
        Assert.Contains("/bmad-check-implementation-readiness", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.True(html.IndexOf("/bmad-create-story 3.1", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-check-implementation-readiness", StringComparison.Ordinal));
        Assert.DoesNotContain("Other actions", html);
    }

    [Fact]
    public void RenderNextSteps_DoneStory_ShowsCelebratoryAllDonePanelNotCodeReview()
    {
        // Pure celebration when correct-course is absent — byte-identical to the pre-8.5 celebratory panel.
        var without = WorkflowCommands.RenderNextSteps(Story("2.1", "done"), BmmWithoutCorrectCourse);

        Assert.Contains("next-steps all-done", without);
        Assert.Contains("All done", without);
        Assert.Contains("ss-icon", without);
        Assert.DoesNotContain("/bmad-code-review", without);
        Assert.DoesNotContain("Other actions", without);
        Assert.DoesNotContain("next-step-card-primary", without);

        // With correct-course: celebration + one muted escape hatch, never a primary / never code-review.
        var with = WorkflowCommands.RenderNextSteps(Story("2.1", "done"), BmmCatalog);

        Assert.Contains("next-steps all-done", with);
        Assert.Contains("All done", with);
        Assert.Contains("Other actions", with);
        Assert.Contains("/bmad-correct-course", with);
        Assert.Contains("Re-open this story if it needs rework.", with);
        Assert.DoesNotContain("next-step-card-primary", with);
        Assert.DoesNotContain("/bmad-code-review", with);
        Assert.Contains("next-steps-desc", with);
    }

    [Fact]
    public void RenderNextSteps_CorrectCourseDropsWhenModuleLacksIt()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmWithoutCorrectCourse);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.DoesNotContain("correct-course", html);
        Assert.DoesNotContain("Other actions", html);
        Assert.Equal(2, CountNextStepCards(html));
    }

    [Fact]
    public void RenderNextSteps_IsDeterministic()
    {
        var a = WorkflowCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);
        var b = WorkflowCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);
        Assert.Equal(a, b);
    }

    private static EpicInfo Epic(bool hasRetro, params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "First Epic",
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
        HasRetrospective = hasRetro,
    };

    [Fact]
    public void RenderEpicNextSteps_AllStoriesDoneNoRetro_SuggestsRetrospective()
    {
        // The retro-gated "review" state (every story done, no retro yet) is exactly when to nudge a retro.
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
        });

        var html = WorkflowCommands.RenderEpicNextSteps(Epic(hasRetro: false, Story("1.1", "done"), Story("1.2", "done")), catalog);

        Assert.Contains("/bmad-retrospective 1", html);
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.Contains("next-steps-cards", html);
        Assert.DoesNotContain("Other actions", html);
    }

    [Fact]
    public void RenderEpicNextSteps_AllStoriesDoneWithRetro_SuggestsNothing()
    {
        // Once the retro exists the epic is "done" — nothing more to suggest, so the panel is omitted entirely
        // (no re-nagging to run a retrospective it already has). [spec-sunburst-retro]
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
        });

        var html = WorkflowCommands.RenderEpicNextSteps(Epic(hasRetro: true, Story("1.1", "done"), Story("1.2", "done")), catalog);

        Assert.Equal(string.Empty, html);
    }

    private static EpicsModel Project(params StoryInfo[] stories) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "First Epic",
                GoalHtml = string.Empty,
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            },
        },
    };

    [Fact]
    public void RenderProjectNextSteps_ListsCodeReviewForStoryAwaitingReview()
    {
        var html = WorkflowCommands.RenderProjectNextSteps(
            Project(Story("1.3", "done"), Story("1.4", "review")), BmmCatalog);

        // A lone review story passes its id straight to the command.
        Assert.Contains("/bmad-code-review 1.4", html);
        Assert.Contains("Story 1.4 is awaiting code review", html);
        // The done story is not the front line — it gets no dev-story or code-review prompt of its own here.
        // (It may still be named as the next story to draft, since this fixture leaves its plan path unset.)
        Assert.DoesNotContain("dev-story 1.3", html);
        Assert.DoesNotContain("code-review 1.3", html);
        Assert.Equal(1, CountNextStepPrimary(html));
    }

    [Fact]
    public void RenderProjectNextSteps_GroupsReviewStoriesIntoOneNamedPrompt_BeforeTheFrontLine()
    {
        // Two stories awaiting review plus a ready front-line story: a single code-review prompt lists both
        // ids (grouped by action, not one row per story), and it precedes the dev-story front line.
        var html = WorkflowCommands.RenderProjectNextSteps(
            Project(Story("1.4", "review"), Story("2.1", "review"), Story("1.5", "ready-for-dev")), BmmCatalog);

        Assert.Contains("Stories 1.4, 2.1 are awaiting code review", html);
        Assert.Contains("/bmad-dev-story 1.5", html);
        // Exactly one code-review row, not one per review story. Count the rendered command (each row's
        // badge carries the command in a <code class="cmd-text"> and a data-copy for its copy button). [Story 1.5 F2]
        Assert.Equal(1, html.Split("<code class=\"cmd-text\">/bmad-code-review").Length - 1);
        // Multiple review stories keep the bare command — no single id is appended.
        Assert.DoesNotContain("/bmad-code-review 1.4", html);
        Assert.True(html.IndexOf("awaiting code review", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-dev-story", StringComparison.Ordinal),
            "review prompt should render before the front-line dev-story prompt");
        Assert.Equal(1, CountNextStepPrimary(html));
        Assert.DoesNotContain("Other actions", html);
        Assert.True(html.IndexOf("awaiting code review", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-dev-story", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderProjectNextSteps_OmitsCodeReviewWhenNoStoryInReview()
    {
        var html = WorkflowCommands.RenderProjectNextSteps(
            Project(Story("1.4", "ready-for-dev")), BmmCatalog);

        Assert.DoesNotContain("code-review", html);
        Assert.DoesNotContain("awaiting code review", html);
    }

    [Fact]
    public void RenderProjectNextSteps_OmitsCodeReviewWhenModuleLacksCommand()
    {
        var noReviewCatalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
        });

        var html = WorkflowCommands.RenderProjectNextSteps(Project(Story("1.4", "review")), noReviewCatalog);

        Assert.DoesNotContain("code-review", html);
        Assert.DoesNotContain("awaiting code review", html);
    }

    // ---- Story 9.8 coherence: Home × epic × empty Ready primary affordance matrix -------------------
    // Surface × lifecycle × primary × destination (regression pin for Driver Journey 2):
    // | Home Next Steps | mid-epic undrafted (done + undrafted) | create-story {id} | story draft |
    // | Epic Up Next + Next Steps | same mid-epic | create-story {id} primary (not buried) | story draft |
    // | Epic active with front line | in-progress + undrafted | sprint-status primary; create-story alt | status / draft |
    // | Empty Ready lane | undrafted + catalog | InlineGuidance create-story {same id as Home} | draft |
    // | Empty Ready lane | pending-epic undrafted only | designed copy (no create-story badge) | — |
    // | Empty Ready lane | no undrafted / no catalog | designed copy only (8.6) | — |

    [Fact]
    public void RenderProjectNextSteps_MidEpicUndrafted_SuggestsCreateStory_AlignedWithForEpic()
    {
        // Epic is visually "active" (a done story) with a later undrafted story — Home must recommend
        // create-story for that id the same way ForEpic(active) does. [Story 9.8]
        var done = Story("1.1", "done");
        done.ArtifactOutputPath = "epics/story-1-1.html";
        var undrafted = Story("1.2", null);

        var projectHtml = WorkflowCommands.RenderProjectNextSteps(Project(done, undrafted), BmmCatalog);
        Assert.Contains("/bmad-create-story 1.2", projectHtml);

        var epicCatalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["sprint-status"] = "/bmad-sprint-status",
            ["dev-story"] = "/bmad-dev-story",
        });
        var epicHtml = WorkflowCommands.RenderEpicNextSteps(Epic(hasRetro: false, done, undrafted), epicCatalog);
        Assert.Contains("/bmad-create-story 1.2", epicHtml);
        // Up Next would spotlight the undrafted story — create-story is the primary, not buried under sprint-status.
        Assert.True(
            epicHtml.IndexOf("/bmad-create-story 1.2", StringComparison.Ordinal)
            < epicHtml.IndexOf("/bmad-sprint-status", StringComparison.Ordinal),
            "create-story must be primary when Up Next target is undrafted");
        Assert.Equal(1, CountNextStepPrimary(epicHtml));
    }

    [Fact]
    public void RenderEpicNextSteps_ActiveWithFrontLine_KeepsSprintStatusPrimary_CreateStoryAlternate()
    {
        var active = Story("1.1", "in-progress");
        active.ArtifactOutputPath = "epics/story-1-1.html";
        var undrafted = Story("1.2", null);
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["sprint-status"] = "/bmad-sprint-status",
        });

        var html = WorkflowCommands.RenderEpicNextSteps(Epic(hasRetro: false, active, undrafted), catalog);

        Assert.Contains("/bmad-sprint-status", html);
        Assert.Contains("/bmad-create-story 1.2", html);
        Assert.True(
            html.IndexOf("/bmad-sprint-status", StringComparison.Ordinal)
            < html.IndexOf("/bmad-create-story 1.2", StringComparison.Ordinal),
            "with an in-progress front line, sprint-status stays primary");
        Assert.DoesNotContain("Other actions", html);
        Assert.Equal(2, CountNextStepCards(html));
    }

    /// <summary>Known slug families BmadCommands suggests must map explicitly (fail closed for unknowns).</summary>
    public static TheoryData<string, string, string> KnownCommandAccentKicker => new()
    {
        { "/bmad-dev-story 1.2", "active", "Develop" },
        { "/bmad-sprint-status", "active", "Plan" },
        { "/bmad-correct-course", "active", "Recover" },
        { "/bmad-quick-dev", "active", "Implement" },
        { "/bmad-code-review 1.2", "review", "Review" },
        { "/bmad-retrospective 3", "review", "Review" },
        { "/bmad-create-story 1.2", "drafted", "Draft" },
        { "/bmad-create-epics-and-stories", "ready", "Break down" },
        { "/bmad-sprint-planning", "ready", "Plan" },
        { "/bmad-check-implementation-readiness", "pending", "Validate" },
    };

    [Theory]
    [MemberData(nameof(KnownCommandAccentKicker))]
    public void AccentAndKicker_KnownSlugFamilies_AreExplicit(string command, string accent, string kicker)
    {
        Assert.Equal(accent, WorkflowCommands.AccentForCommand(command));
        Assert.Equal(kicker, WorkflowCommands.KickerForCommand(command, isPrimary: false));
    }

    [Fact]
    public void AccentAndKicker_UnknownSlug_FailsClosedToPendingAlsoConsider()
    {
        const string unknown = "/bmad-totally-new-skill";
        Assert.Equal("pending", WorkflowCommands.AccentForCommand(unknown));
        Assert.Equal("Also consider", WorkflowCommands.KickerForCommand(unknown, isPrimary: false));
        Assert.Equal("Recommended", WorkflowCommands.KickerForCommand(unknown, isPrimary: true));
    }

    [Fact]
    public void AccentAndKicker_SprintStatus_StaysActiveWithPlanKicker()
    {
        Assert.Equal("active", WorkflowCommands.AccentForCommand("/bmad-sprint-status"));
        Assert.Equal("Plan", WorkflowCommands.KickerForCommand("/bmad-sprint-status", isPrimary: false));
    }

    // ---- Address deferred I/O matrix ----------------------------------------------------------------

    private static readonly CommandCatalog BmmWithQuickDev = new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["correct-course"] = "/bmad-correct-course",
        ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
        ["quick-dev"] = "/bmad-quick-dev",
    });

    private static FollowUpDeferredSlot OpenSlot(string body, string? sourceKey = null, string? detailHref = null) =>
        new(new DeferredWorkItem($"<p>{body}</p>", Resolved: false, null, null),
            "code review of 6-5-test", EpicNumber: 6,
            DetailHref: detailHref ?? "follow-ups/deferred-test.html",
            SourceKey: sourceKey ?? "6-5-test");

    private static FollowUpDeferredSlot ResolvedSlot(string body) =>
        new(new DeferredWorkItem($"<p>{body}</p>", Resolved: true, null, null),
            "code review of 6-5-test", EpicNumber: 6,
            DetailHref: "follow-ups/deferred-resolved.html",
            SourceKey: "6-5-test");

    [Fact]
    public void RenderNextSteps_DoneStoryWithOpenDeferred_AddressDeferredPrimary_NoCelebration()
    {
        var deferred = new[] { OpenSlot("Extract helper method"), OpenSlot("Add logging") };
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "done"), BmmWithQuickDev, deferred);

        Assert.Contains("Address deferred", html);
        Assert.Contains("next-step-card-primary", html);
        Assert.Contains("done-deferred-status", html);
        Assert.Contains("2 open deferred items remain", html);
        Assert.DoesNotContain("all-done", html);
        Assert.DoesNotContain("All done", html);
        Assert.Contains("Extract helper method", html);
        Assert.Contains("Add logging", html);
        Assert.Contains("6-5-test", html);
    }

    [Fact]
    public void RenderNextSteps_DoneStoryWithNoOpenDeferred_CelebratoryAllDone()
    {
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "done"), BmmWithQuickDev);
        Assert.Contains("all-done", html);
        Assert.Contains("All done", html);
        Assert.DoesNotContain("Address deferred", html);
        Assert.DoesNotContain("done-deferred-status", html);
    }

    [Fact]
    public void RenderNextSteps_DoneStoryWithOnlyResolvedDeferred_CelebratoryAllDone()
    {
        var deferred = new[] { ResolvedSlot("Already fixed") };
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "done"), BmmWithQuickDev, deferred);
        Assert.Contains("all-done", html);
        Assert.Contains("All done", html);
        Assert.DoesNotContain("Address deferred", html);
    }

    [Fact]
    public void RenderNextSteps_InProgressWithOpenDeferred_AddressDeferredNotPrimary()
    {
        var deferred = new[] { OpenSlot("Fix edge case") };
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "in-progress"), BmmWithQuickDev, deferred);

        Assert.Contains("/bmad-dev-story 6.5", html);
        Assert.Contains("Address deferred", html);
        var primaryIdx = html.IndexOf("next-step-card-primary", StringComparison.Ordinal);
        var devIdx = html.IndexOf("/bmad-dev-story 6.5", StringComparison.Ordinal);
        var addrIdx = html.IndexOf("Address deferred", StringComparison.Ordinal);
        Assert.True(primaryIdx < devIdx, "dev-story should be under the primary card");
        Assert.True(devIdx < addrIdx, "Address deferred should come after dev-story");
    }

    [Fact]
    public void RenderNextSteps_ReviewWithOpenDeferred_AddressDeferredNotPrimary()
    {
        var deferred = new[] { OpenSlot("Fix edge case") };
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "review"), BmmWithQuickDev, deferred);

        Assert.Contains("/bmad-code-review 6.5", html);
        Assert.Contains("Address deferred", html);
        var reviewIdx = html.IndexOf("/bmad-code-review 6.5", StringComparison.Ordinal);
        var addrIdx = html.IndexOf("Address deferred", StringComparison.Ordinal);
        Assert.True(reviewIdx < addrIdx, "code-review should precede Address deferred");
    }

    [Fact]
    public void RenderEpicNextSteps_EpicWithOpenDeferred_AddressDeferredCoversAll()
    {
        var deferred = new[]
        {
            OpenSlot("Story-child item", "6-5-slug", "follow-ups/deferred-1.html"),
            OpenSlot("Epic-level item", "spec-infra", "follow-ups/deferred-2.html"),
        };
        var epic = Epic(hasRetro: false, Story("1.1", "in-progress"));
        var html = WorkflowCommands.RenderEpicNextSteps(epic, BmmWithQuickDev, deferred);

        Assert.Contains("Address deferred", html);
        Assert.Contains("Story-child item", html);
        Assert.Contains("Epic-level item", html);
        Assert.Contains("2 item", html);
    }

    [Fact]
    public void RenderEpicNextSteps_DoneEpicWithOpenDeferred_AddressDeferredPrimary()
    {
        var deferred = new[] { OpenSlot("Left over item") };
        var doneEpic = Epic(hasRetro: true, Story("1.1", "done"), Story("1.2", "done"));
        var html = WorkflowCommands.RenderEpicNextSteps(doneEpic, BmmWithQuickDev, deferred);

        Assert.Contains("Address deferred", html);
        Assert.Contains("next-step-card-primary", html);
        Assert.Contains("done-deferred-status", html);
        Assert.DoesNotContain("all-done", html);
    }

    [Fact]
    public void RenderEpicNextStepsInner_DoneEpicWithOpenDeferred_AddressDeferredInUpNextFoldIn()
    {
        // Epic pages with stories only render Up Next (RenderEpicNextStepsInner), not the standalone
        // RenderEpicNextSteps panel — done+deferred must surface there. [spec-address-deferred-next-steps]
        var deferred = new[] { OpenSlot("Left over item") };
        var doneEpic = Epic(hasRetro: true, Story("1.1", "done"), Story("1.2", "done"));
        var inner = WorkflowCommands.RenderEpicNextStepsInner(doneEpic, BmmWithQuickDev, deferred);

        Assert.Contains("Address deferred", inner);
        Assert.Contains("done-deferred-status", inner);
        Assert.Contains("next-step-card-primary", inner);
        Assert.DoesNotContain("chart-panel", inner); // fold-in — no nested panel wrapper
    }

    [Fact]
    public void RenderEpicNextSteps_ReviewEpicWithDeferred_AddressDeferredDemoted()
    {
        var deferred = new[] { OpenSlot("Needs fixing") };
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
            ["quick-dev"] = "/bmad-quick-dev",
        });
        var reviewEpic = Epic(hasRetro: false, Story("1.1", "done"), Story("1.2", "done"));
        var html = WorkflowCommands.RenderEpicNextSteps(reviewEpic, catalog, deferred);

        Assert.Contains("/bmad-retrospective 1", html);
        Assert.Contains("Address deferred", html);
        var retroIdx = html.IndexOf("/bmad-retrospective", StringComparison.Ordinal);
        var addrIdx = html.IndexOf("Address deferred", StringComparison.Ordinal);
        Assert.True(retroIdx < addrIdx, "retro should be primary; address deferred demoted");
    }

    [Fact]
    public void RenderNextSteps_OpenDeferredButNoQuickDev_OmitsAddressDeferred()
    {
        var catalogNoQd = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
            ["code-review"] = "/bmad-code-review",
        });
        var deferred = new[] { OpenSlot("Should not show") };

        var htmlDone = WorkflowCommands.RenderNextSteps(Story("6.5", "done"), catalogNoQd, deferred);
        Assert.DoesNotContain("Address deferred", htmlDone);
        Assert.Contains("all-done", htmlDone);

        var htmlActive = WorkflowCommands.RenderNextSteps(Story("6.5", "in-progress"), catalogNoQd, deferred);
        Assert.DoesNotContain("Address deferred", htmlActive);
    }

    [Fact]
    public void StoryCommands_DoneWithOpenDeferred_AddressDeferredPresent()
    {
        var deferred = new[] { OpenSlot("Deferred task") };
        var cmds = WorkflowCommands.StoryCommands(Story("6.5", "done"), BmmWithQuickDev, deferred);

        Assert.True(cmds.Count >= 1);
        Assert.Contains(cmds, c => c.Command.Contains("Address open deferred"));
        Assert.Equal(cmds[0].Command, WorkflowCommands.PrimaryStoryCommand(Story("6.5", "done"), BmmWithQuickDev, deferred));
    }

    [Fact]
    public void PrimaryStoryCommand_DoneWithOpenDeferredButNoQuickDev_HatchIsNotPrimary()
    {
        var catalogNoQd = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["correct-course"] = "/bmad-correct-course",
        });
        var deferred = new[] { OpenSlot("Parked work") };

        Assert.Null(WorkflowCommands.PrimaryStoryCommand(Story("6.5", "done"), catalogNoQd, deferred));
        Assert.DoesNotContain("Address deferred",
            WorkflowCommands.RenderNextSteps(Story("6.5", "done"), catalogNoQd, deferred));
        Assert.Contains("all-done", WorkflowCommands.RenderNextSteps(Story("6.5", "done"), catalogNoQd, deferred));
    }

    [Fact]
    public void RenderEpicNextSteps_PendingEpicWithOpenDeferred_AddressDeferredDemoted()
    {
        var epic = new EpicInfo
        {
            Number = 1,
            Title = "Pending",
            GoalHtml = string.Empty,
            Status = EpicStatus.Pending,
            Section = EpicSection.VerticalSlice,
            Stories = Array.Empty<StoryInfo>(),
        };
        var deferred = new[] { OpenSlot("From planning") };
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
            ["quick-dev"] = "/bmad-quick-dev",
        });
        var html = WorkflowCommands.RenderEpicNextSteps(epic, catalog, deferred);

        Assert.Contains("Address deferred", html);
        var createIdx = html.IndexOf("create-epics", StringComparison.Ordinal);
        var addrIdx = html.IndexOf("Address deferred", StringComparison.Ordinal);
        Assert.True(createIdx >= 0 && createIdx < addrIdx);
    }

    [Fact]
    public void StoryCommands_DoneWithNoDeferred_EmptyOrHatchOnly()
    {
        var cmds = WorkflowCommands.StoryCommands(Story("6.5", "done"), BmmWithQuickDev);
        var primary = WorkflowCommands.PrimaryStoryCommand(Story("6.5", "done"), BmmWithQuickDev);

        Assert.Null(primary);
        if (cmds.Count > 0)
            Assert.Contains("correct-course", cmds[0].Command);
    }

    [Fact]
    public void RenderNextSteps_DoneWithDeferredPanel_SingleItemGrammar()
    {
        var deferred = new[] { OpenSlot("Single item") };
        var html = WorkflowCommands.RenderNextSteps(Story("6.5", "done"), BmmWithQuickDev, deferred);

        Assert.Contains("1 open deferred item remains", html);
        Assert.Contains("(1 item)", html);
    }

    [Fact]
    public void RenderNextSteps_CheckImplementationAlternate_UsesPendingAccentAndValidateKicker()
    {
        // Undrafted first story: create-story primary, check-implementation-readiness as non-primary.
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
        });
        var html = WorkflowCommands.RenderNextSteps(Story("1.1", null), catalog);

        Assert.Contains("next-step-card next-step-card-primary drafted", html);
        Assert.Contains(">Recommended</span>", html);
        Assert.Contains("next-step-card pending", html);
        Assert.Contains(">Validate</span>", html);
        Assert.DoesNotContain(">Also consider</span>", html);
        // Primary create-story before Validate alternate.
        Assert.True(
            html.IndexOf("next-step-card-primary", StringComparison.Ordinal)
            < html.IndexOf(">Validate</span>", StringComparison.Ordinal));
        Assert.True(
            html.IndexOf("next-step-card pending", StringComparison.Ordinal)
            < html.IndexOf(">Validate</span>", StringComparison.Ordinal)
            && html.IndexOf("next-step-card pending", StringComparison.Ordinal)
               > html.IndexOf("next-step-card-primary", StringComparison.Ordinal));
    }
}
