using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 12.2's <see cref="GsdCoreArtifactAdapter"/> — the first non-BMad framework
/// surface. Follows <see cref="BmadArtifactAdapterTests"/>' temp-dir fixture style
/// (<see cref="Directory.CreateTempSubdirectory"/> + <c>const string</c> bodies).
///
/// <para><b>The fixture is DERIVED from a real GSD Core repository, deliberately including its awkward shapes</b>,
/// because the documentation-derived plan for this adapter was wrong in six places and only the live repo showed
/// it. Encoded here: a decimal phase (<c>02.1</c>), a <c>999.x</c> backlog phase, a plan with no <c>## Tasks</c>
/// heading, a plan whose only checkboxes are UNCHECKED <c>## Verification</c> boxes on finished work, a <c>[x]</c>
/// plan with no sibling <c>-SUMMARY.md</c>, a <c>STATE.md</c> roll-up that disagrees with the roadmap, and both the
/// em-dash and hyphen plan-line separators that occur in one file.</para>
///
/// <para><b>The reference repository is never read by a test.</b> CI has no such path; it informed these strings
/// and nothing more.</para></summary>
public class GsdCoreArtifactAdapterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-gsd-").FullName;

    private string Planning => Path.Combine(_root, ".planning");
    private string Phases => Path.Combine(Planning, "phases");
    private string Site => Path.Combine(_root, "site");

    /// <summary>A roadmap in the real grammar: a milestone-grouped <c>## Phases</c> overview that establishes
    /// ROADMAP ORDER, per-milestone <c>— Phase Details</c> sections carrying the authoritative per-plan checkboxes,
    /// a <c>## Backlog</c>, and a <c>## Progress</c> roll-up. Phase 2.1 is third in overview order, so its plans
    /// take ordinal 3.</summary>
    private const string RoadmapMd = """
        # Roadmap: Fixture

        ## Overview

        A fixture roadmap in GSD Core's real grammar.

        ## Phases

        ### Milestone: v1.0 (completed 2026-05-27)

        - [x] **Phase 1: Identity and Scope** - Establish boundaries. (completed 2026-05-07)
        - [x] **Phase 2: Conversation Continuity** - Persist conversations. (completed 2026-05-08)
        - [x] **Phase 2.1: UI Foundation and Style System** - Establish the design system. (completed 2026-05-14)

        ### Milestone: v2.0

        - [ ] **Phase 7: External Document Ingestion** - Import documents.

        ## Milestone: v1.0 — Phase Details

        ### Phase 1: Identity and Scope
        **Goal**: Users operate inside enforced identity boundaries.
        **Requirements**: CTX-01, CTX-02
        **Plans**: 2 plans
        Plans:
        - [x] 01-00-PLAN.md - Establish auth pipeline.
        - [x] 01-01-PLAN.md - Implement allowlist and scope APIs.

        ### Phase 2: Conversation Continuity
        **Goal**: Continuity across sessions.
        **Requirements**: CONV-01
        Plans:
        - [x] 02-00-PLAN.md — Wave 0: test infrastructure

        ### Phase 2.1: UI Foundation and Style System (INSERTED)
        **Goal**: The client reflects the UX specification.
        **Requirements**: Supports all future UI requirements.
        Plans:
        - [x] 02.1-01-PLAN.md — Wave 0: Quasar install
        - [ ] 02.1-02-PLAN.md — Wave 1: token CSS

        ## Milestone: v2.0 — Phase Details

        ### Phase 7: External Document Ingestion
        **Goal**: Import external documents as RAG sources.
        **Plans**: TBD

        ## Backlog

        ### Phase 999.1: Sentiment Analysis (BACKLOG)

        **Goal:** Classify user message sentiment.
        **Plans:** 0 plans

        Plans:
        - [ ] TBD (promote with /gsd-review-backlog when ready)

        ## Progress

        ### Milestone: v1.0 (completed 2026-05-27)

        | Phase | Plans Complete | Status | Completed |
        |-------|----------------|--------|-----------|
        | 1. Identity and Scope | 2/2 | Complete | 2026-05-07 |
        """;

    /// <summary>A plan with <b>no <c>## Tasks</c> heading at all</b> — 33 of 58 plans in the reference repo are
    /// shaped this way. <see cref="TaskListParser"/> returns an empty list for it.</summary>
    private const string PlanNoTasksMd = """
        ---
        phase: "01"
        plan: 00
        type: execute
        ---

        # Plan 01-00

        ## Context

        Establish the auth pipeline.
        """;

    /// <summary>A plan that DOES have <c>## Tasks</c>, decomposed as <c>&lt;task&gt;</c> XML blocks, plus a
    /// <c>## Verification</c> section whose boxes stay UNCHECKED even though the work shipped. This is the exact
    /// shape that makes a naive checkbox tally report 0-done-of-N on finished work — and note there is no
    /// <c>Status:</c> line and no <c>status:</c> frontmatter key anywhere, which is why the adapter takes status
    /// from the roadmap instead.</summary>
    private const string PlanWithVerificationBoxesMd = """
        ---
        phase: 1
        plan: 01
        ---

        # Plan 01-01

        ## Tasks

        <task type="auto">
        Implement the allowlist.
        </task>

        ## Verification

        - [ ] Allowlist rejects an unknown subject
        - [ ] Scope API returns project-partitioned rows
        """;

    private const string SummaryMd = """
        # Summary 01-00

        Executed in one wave.
        """;

    /// <summary>Frontmatter whose roll-up DISAGREES with the roadmap (which marks 4 of 6 plans complete), and
    /// which counts a total the roadmap does not have. Both disagreements must be reported, not reconciled.</summary>
    private const string StateMd = """
        ---
        gsd_state_version: 1.0
        milestone: v1.0
        status: ready_to_execute
        last_updated: "2026-05-20T00:00:00.000Z"
        progress:
          total_phases: 3
          completed_phases: 2
          total_plans: 5
          completed_plans: 3
          percent: 60
        ---

        # Project State
        """;

    public GsdCoreArtifactAdapterTests()
    {
        Directory.CreateDirectory(Path.Combine(Phases, "01-identity-and-scope"));
        Directory.CreateDirectory(Path.Combine(Phases, "02-conversation-continuity"));
        Directory.CreateDirectory(Path.Combine(Phases, "02.1-ui-foundation-and-style-system"));

        File.WriteAllText(Path.Combine(Planning, "ROADMAP.md"), RoadmapMd);
        File.WriteAllText(Path.Combine(Planning, "STATE.md"), StateMd);
        File.WriteAllText(Path.Combine(Planning, "REQUIREMENTS.md"), "# Requirements\n\nCONV-01: Something.\n");
        File.WriteAllText(Path.Combine(Planning, "PROJECT.md"), "# Fixture\n\n## What This Is\n\nA fixture.\n");
        File.WriteAllText(Path.Combine(Planning, "config.json"), "{\"version\":1}");

        var p1 = Path.Combine(Phases, "01-identity-and-scope");
        File.WriteAllText(Path.Combine(p1, "01-00-PLAN.md"), PlanNoTasksMd);
        File.WriteAllText(Path.Combine(p1, "01-00-SUMMARY.md"), SummaryMd);
        // 01-01 is marked [x] in the roadmap but has NO sibling summary — one of the three disagreeing signals.
        File.WriteAllText(Path.Combine(p1, "01-01-PLAN.md"), PlanWithVerificationBoxesMd);

        File.WriteAllText(Path.Combine(Phases, "02-conversation-continuity", "02-00-PLAN.md"), PlanNoTasksMd);
        var p21 = Path.Combine(Phases, "02.1-ui-foundation-and-style-system");
        File.WriteAllText(Path.Combine(p21, "02.1-01-PLAN.md"), PlanNoTasksMd);
        File.WriteAllText(Path.Combine(p21, "02.1-02-PLAN.md"), PlanNoTasksMd);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Planning, adrs: Path.Combine(_root, "docs", "adrs"), output: Path.Combine(_root, "site"),
        projectName: "Fixture", includeReadme: false);

    private List<string> SourceFiles() =>
        Directory.EnumerateFiles(Planning, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ProgressModel Project(EpicsModel epics, IReadOnlyDictionary<string, string> artifacts) =>
        ProgressCalculator.Compute(epics, artifacts, git: null);

    private ArtifactBundle Ingest() => new GsdCoreArtifactAdapter().Ingest(Options(), SourceFiles(), Project);

    // ---- AppliesTo (Task 4) -------------------------------------------------------------------------------------

    [Fact]
    public void AppliesTo_PlanningMarkerAtRepoRoot_SelfSelects()
    {
        Assert.True(new GsdCoreArtifactAdapter().AppliesTo(Options(), SourceFiles()));
    }

    [Fact]
    public void AppliesTo_NoPlanningMarker_DoesNotSelfSelect()
    {
        var other = Directory.CreateTempSubdirectory("specscribe-gsd-none-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(other, "_bmad-output"));
            var options = ForgeOptions.Resolve(
                source: Path.Combine(other, "_bmad-output"), output: Path.Combine(other, "site"));
            Assert.False(new GsdCoreArtifactAdapter().AppliesTo(options, Array.Empty<string>()));
        }
        finally { try { Directory.Delete(other, recursive: true); } catch (IOException) { } }
    }

    [Fact]
    public void WorkflowCommands_DiscoverOnlyInstalledCommands_AndKeepNativePhaseArguments()
    {
        var commandsRoot = Path.Combine(_root, ".claude", "commands", "gsd");
        Directory.CreateDirectory(commandsRoot);
        File.WriteAllText(Path.Combine(commandsRoot, "plan-phase.md"), "# Plan phase");
        File.WriteAllText(Path.Combine(commandsRoot, "discuss-phase.md"), "# Discuss phase");
        File.WriteAllText(Path.Combine(commandsRoot, "ui-phase.md"), "# Specify phase UI");
        File.WriteAllText(Path.Combine(commandsRoot, "research-phase.md"), "# Research phase");
        File.WriteAllText(Path.Combine(commandsRoot, "execute-phase.md"), "# Execute phase");
        File.WriteAllText(Path.Combine(commandsRoot, "code-review.md"), "# Review phase");

        var bundle = Ingest();
        var commands = Assert.IsType<CommandCatalog>(bundle.WorkflowCommands);
        var phase = Assert.IsType<EpicsModel>(bundle.Epics).Epics[2];

        Assert.True(commands.UsesPhaseArguments);
    Assert.Equal("/gsd:discuss-phase", commands.Command("discuss-phase"));
    Assert.Equal("/gsd:ui-phase", commands.Command("ui-phase"));
    Assert.Equal("/gsd:research-phase", commands.Command("research-phase"));
        Assert.Equal("/gsd:plan-phase", commands.Command("create-story"));
        Assert.Equal("/gsd:execute-phase", commands.Command("dev-story"));
        Assert.Equal("/gsd:code-review", commands.Command("code-review"));
        Assert.Null(commands.Command("sprint-status"));
        Assert.Equal("2.1", phase.WorkflowCommandArgument);
        Assert.All(phase.Stories, story => Assert.Equal("2.1", story.WorkflowCommandArgument));
    }

    [Fact]
    public void WorkflowCommands_MissingPreparatoryDefinition_OmitsOnlyThatCommand()
    {
        var commandsRoot = Path.Combine(_root, ".claude", "commands", "gsd");
        Directory.CreateDirectory(commandsRoot);
        File.WriteAllText(Path.Combine(commandsRoot, "discuss-phase.md"), "# Discuss phase");
        File.WriteAllText(Path.Combine(commandsRoot, "ui-phase.md"), "# Specify phase UI");
        File.WriteAllText(Path.Combine(commandsRoot, "plan-phase.md"), "# Plan phase");

        var commands = Assert.IsType<CommandCatalog>(Ingest().WorkflowCommands);

        Assert.Equal("/gsd:discuss-phase", commands.Command("discuss-phase"));
        Assert.Equal("/gsd:ui-phase", commands.Command("ui-phase"));
        Assert.Null(commands.Command("research-phase"));
        Assert.Equal("/gsd:plan-phase", commands.Command("create-epics-and-stories"));
    }

    [Fact]
    public void WorkflowCommands_MissingCommandDirectory_IsAnExplicitEmptyGsdCatalog()
    {
        var commands = Assert.IsType<CommandCatalog>(Ingest().WorkflowCommands);

        Assert.Equal("GSD Core", commands.ModuleLabel);
        Assert.True(commands.UsesPhaseArguments);
        Assert.True(commands.IsEmpty);
    }

    // ---- The synthetic ordinal and the story-id form (AC #3, D2) ------------------------------------------------

    /// <summary>AC #3's pin: BOTH the ordinal assignment AND the <c>{ordinal}.{plan}</c> story-id form, including a
    /// decimal phase and a <c>999.x</c> backlog phase. Phase 2.1 is third in roadmap order, so its plans are 3.1
    /// and 3.2 — which is exactly what makes the <c>"N.M"</c> contract survive a phase number an <c>int</c> cannot
    /// hold.</summary>
    [Fact]
    public void Epics_PhaseOrdinalsFollowRoadmapOrder_AndStoryIdsAreOrdinalDotPlan()
    {
        var bundle = Ingest();
        var epics = Assert.IsType<EpicsModel>(bundle.Epics);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, epics.Epics.Select(e => e.Number).ToArray());
        Assert.Contains("Phase 1: Identity and Scope", epics.Epics[0].Title);
        Assert.All(epics.Epics, epic => Assert.False(epic.RequiresRetrospective));
        Assert.Equal("done", StatusStyles.ForEpicWithRetrospective(epics.Epics[0]));
        // The DECIMAL phase keeps its real label while taking a sequential ordinal.
        Assert.Equal(3, epics.Epics[2].Number);
        Assert.Contains("Phase 2.1: UI Foundation and Style System", epics.Epics[2].Title);
        Assert.Equal("Phase 2.1", epics.Epics[2].DisplayName);
        Assert.Equal("Plan 2.1.1", epics.Epics[2].Stories[0].DisplayName);
        // The 999.x backlog phase is represented, not dropped.
        Assert.Contains("Phase 999.1: Sentiment Analysis", epics.Epics[4].Title);

        Assert.Equal(new[] { "1.0", "1.1" }, epics.Epics[0].Stories.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { "3.1", "3.2" }, epics.Epics[2].Stories.Select(s => s.Id).ToArray());
    }

    /// <summary>The <c>(INSERTED)</c> / <c>(BACKLOG)</c> markers and the trailing <c>(completed …)</c> are grammar,
    /// not part of a phase's name — a title carrying them would read as one on every surface.</summary>
    [Fact]
    public void Epics_PhaseTitles_StripGrammarMarkers()
    {
        var epics = Assert.IsType<EpicsModel>(Ingest().Epics);
        Assert.DoesNotContain("INSERTED", epics.Epics[2].Title);
        Assert.DoesNotContain("BACKLOG", epics.Epics[4].Title);
        Assert.DoesNotContain("completed", epics.Epics[0].Title);
    }

    /// <summary>A phase with no plans listed is Pending, not Drafted — mirroring EpicsParser's own rule rather
    /// than inventing a GSD-specific one.</summary>
    [Fact]
    public void Epics_PhaseWithNoPlans_IsPending()
    {
        var epics = Assert.IsType<EpicsModel>(Ingest().Epics);
        Assert.Empty(epics.Epics[3].Stories);
        Assert.Equal(EpicStatus.Pending, epics.Epics[3].Status);
        Assert.Equal(EpicStatus.Drafted, epics.Epics[0].Status);
    }

    [Fact]
    public void Epics_PhaseCompanions_ProjectPlanningContext_AndRecordCompletedDiscussion()
    {
        var phaseDirectory = Path.Combine(Phases, "07-external-document-ingestion");
        Directory.CreateDirectory(phaseDirectory);
        File.WriteAllText(Path.Combine(phaseDirectory, "07-CONTEXT.md"), """
            # Phase 7 Context

            ## Phase Boundary

            Import text and markdown as RAG sources.

            ## Implementation Decisions

            ### Source storage

            Keep source content with its provenance.

            ## Canonical References

            This reference inventory belongs on the source document.

            ## Deferred Ideas

            This deferred idea belongs on the source document.
            """);
        File.WriteAllText(Path.Combine(phaseDirectory, "07-DISCUSSION-LOG.md"), "# Discussion Log\n");
        File.WriteAllText(Path.Combine(phaseDirectory, "07-UI-SPEC.md"), """
            # Phase 7 UI Design Contract

            ## 0. Scope and Inheritance

            Reuse the existing Knowledge navigation.

            ## 6. Routes

            Add an explorer route under knowledge.

            ## 7. Component Inventory

            ### New Components

            Add a source explorer.

            ## 8. Layout Specifications

            This detailed layout belongs on the source document.
            """);

        var phase = Assert.IsType<EpicsModel>(Ingest().Epics).Epics[3];

        Assert.True(phase.HasDiscussionLog);
        Assert.True(phase.HasUiPlan);
        Assert.Equal(EpicStatus.Drafted, phase.Status);
        Assert.Equal("drafted", StatusStyles.ForEpicWithRetrospective(phase));
        Assert.Contains("Phase Boundary", phase.PhaseContextHtml);
        Assert.Contains("Import text and markdown", phase.PhaseContextHtml);
        Assert.Contains("Implementation Decisions", phase.PhaseContextHtml);
        Assert.Contains("Keep source content", phase.PhaseContextHtml);
        Assert.DoesNotContain("reference inventory", phase.PhaseContextHtml);
        Assert.DoesNotContain("deferred idea", phase.PhaseContextHtml);
        Assert.Contains("Scope and Inheritance", phase.PhaseContextHtml);
        Assert.Contains("explorer route", phase.PhaseContextHtml);
        Assert.Contains("source explorer", phase.PhaseContextHtml);
        Assert.DoesNotContain("detailed layout", phase.PhaseContextHtml);
    }

    // ---- Status and task tally, honestly (Task 6, findings #2 and #8) -------------------------------------------

    /// <summary>THE defect this story exists to prevent. Every plan file here is statusless and either has no
    /// <c>## Tasks</c> heading or only unchecked verification boxes, so the artifact-derived answer for all of them
    /// is "no status, 0/0 tasks" — which renders a finished plan as a drafted story with no plan. The roadmap's
    /// checkbox is what makes it right, and it must survive the projection callback.</summary>
    [Fact]
    public void Stories_StatusComesFromRoadmapCheckbox_AndSurvivesProgressEnrichment()
    {
        var epics = Assert.IsType<EpicsModel>(Ingest().Epics);

        var phase1 = epics.Epics[0];
        Assert.All(phase1.Stories, s => Assert.Equal("done", s.Status));
        Assert.All(phase1.Stories, s => Assert.Equal("done", StatusStyles.ForStory(s)));

        // The unchecked plan in phase 2.1 is drafted, not done, and not "unrecognized".
        var drafted = epics.Epics[2].Stories[1];
        Assert.Equal("drafted", drafted.Status);
        Assert.Equal("drafted", StatusStyles.ForStory(drafted));
    }

    /// <summary>Finding #2, pinned from both ends: the plan bodies genuinely yield no usable tally, and the adapter
    /// leaves 0/0 rather than synthesizing one. 0/0 suppresses the badge, which is the honest outcome.</summary>
    [Fact]
    public void Stories_TaskTallyStaysZero_BecauseGsdPlansCarryNoUsableCheckboxes()
    {
        Assert.Empty(TaskListParser.Parse(PlanNoTasksMd));
        var verificationOnly = TaskListParser.Parse(PlanWithVerificationBoxesMd);
        Assert.DoesNotContain(verificationOnly, t => t.Done);
        Assert.Null(EpicsParser.ExtractStatus(PlanWithVerificationBoxesMd));

        var epics = Assert.IsType<EpicsModel>(Ingest().Epics);
        Assert.All(epics.Epics.SelectMany(e => e.Stories), s =>
        {
            Assert.Equal(0, s.TasksDone);
            Assert.Equal(0, s.TasksTotal);
        });
    }

    [Fact]
    public void PlanParser_XmlTasksAreUnmarkedAndDoNotReadVerificationCheckboxes()
    {
        var tasks = GsdCorePlanParser.ParseTasks(PlanWithVerificationBoxesMd);

        var task = Assert.Single(tasks);
        Assert.Equal("Implement the allowlist.", task.Text);
        Assert.Equal(TaskState.Unmarked, task.State);
        Assert.False(task.Done);
    }

    [Fact]
    public void PlanParser_UsesNamesOrDirectTaskText_AndIgnoresTasksOutsideTheTasksSection()
    {
        const string plan = """
            ## Tasks

            <task><name>Named task</name><action>Verbose action body</action></task>
            <task>
            Direct task label
            <action>
            Verbose action body
            </action>
            </task>
            <task><action>Action-only task</action></task>

            ## Notes

            <task><name>Not a plan task</name></task>
            """;

        var tasks = GsdCorePlanParser.ParseTasks(plan);

        Assert.Equal(["Named task", "Direct task label"], tasks.Select(task => task.Text));
        Assert.All(tasks, task => Assert.Equal(TaskState.Unmarked, task.State));
    }

    /// <summary>Plans are resolved to their files by FILENAME. The fixture's frontmatter deliberately encodes the
    /// same phase four different ways (<c>"01"</c>, <c>1</c>, and the 02.1 plans' copies), so a resolver that
    /// trusted it would mis-key.</summary>
    [Fact]
    public void StoryArtifacts_ResolveByFilename_NotFrontmatter()
    {
        var bundle = Ingest();
        Assert.Equal(
            Path.Combine(Phases, "01-identity-and-scope", "01-00-PLAN.md"),
            bundle.StoryArtifactsById["1.0"]);
        Assert.Equal(
            Path.Combine(Phases, "02.1-ui-foundation-and-style-system", "02.1-02-PLAN.md"),
            bundle.StoryArtifactsById["3.2"]);
    }

    /// <summary>Plans are consumed (they become story pages); summaries are NOT (they keep their own page, coverage
    /// tier Rendered) and instead get a Skipped notice recording that the plan won the story-artifact slot.</summary>
    [Fact]
    public void Consumed_CoversPlansOnly_AndTheSummaryIsReportedRatherThanSwallowed()
    {
        var bundle = Ingest();
        var consumed = bundle.ConsumedSourceRelatives.Select(PathUtil.NormalizeSlashes).ToList();

        Assert.Contains("phases/01-identity-and-scope/01-00-PLAN.md", consumed);
        Assert.DoesNotContain("phases/01-identity-and-scope/01-00-SUMMARY.md", consumed);
        Assert.Contains(bundle.Diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Skipped && d.Message.Contains("01-00-SUMMARY.md"));
    }

    [Fact]
    public void GenerateAll_RendersGsdTasksAndCompletionSummary_WithoutConsumingTheSummaryPage()
    {
        var planPath = Path.Combine(Phases, "01-identity-and-scope", "01-01-PLAN.md");
        File.WriteAllText(planPath, """
            # Plan 01-01

            <objective>Protect identity boundaries.</objective>

            ## Tasks

            <task type="auto"><name>Implement the allowlist</name><action>Verbose implementation detail</action></task>
            """);
        File.WriteAllText(Path.Combine(Phases, "01-identity-and-scope", "01-01-SUMMARY.md"), """
            # Summary 01-01

            ## Tasks Completed

            - Implemented the allowlist.

            ## What Was Built

            Identity boundary enforcement.

            ## Verification Results

            All boundary tests passed.

            ## Notes

            This section is intentionally not promoted.
            """);

        Assert.Equal("Protect identity boundaries.", GsdCorePlanParser.ReadDetail(planPath, File.ReadAllText(planPath))!.Objective);
        var generatedStory = Assert.IsType<EpicsModel>(Ingest().Epics).Epics[0].Stories[1];
        Assert.Equal("epics/story-1-1.html", generatedStory.ArtifactOutputPath);

        var generator = new SiteGenerator(Options());
        Assert.DoesNotContain(generator.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var storyHtml = SiteRegion.Read(Site, "epics/story-1-1.html");
        Assert.Contains("Protect identity boundaries.", storyHtml);
        Assert.Contains("Implement the allowlist", storyHtml);
        Assert.Contains("Planned step", storyHtml);
        Assert.Contains("1 task listed", storyHtml);
        Assert.Contains("id=\"sec-completion-summary-1\"", storyHtml);
        Assert.Contains("Tasks Completed", storyHtml);
        Assert.Contains("What Was Built", storyHtml);
        Assert.Contains("Verification Results", storyHtml);
        Assert.Contains("href=\"#sec-completion-summary-1\"", storyHtml);
        Assert.DoesNotContain("This section is intentionally not promoted", storyHtml);

        Assert.True(SiteRegion.Exists(Site, "phases/01-identity-and-scope/01-01-SUMMARY.html"));
    }

    [Fact]
    public void GenerateAll_UsesGsdPhaseAndPlanVocabularyAcrossPlanningSurfaces()
    {
        var bundle = Ingest();
        Assert.Equal(PlanningVocabulary.GsdCore, bundle.PlanningVocabulary);

        var generator = new SiteGenerator(Options());
        Assert.False(generator.GenerateAll().Any(e => e.Outcome == GenerationOutcome.Error));

        var dashboard = SiteRegion.Read(Site, "index.html");
        var index = SiteRegion.Read(Site, "epics.html");
        var phase = SiteRegion.Read(Site, "epics/epic-1.html");
        var plan = SiteRegion.Read(Site, "epics/story-1-0.html");

        Assert.Contains("Phases drafted", dashboard);
        Assert.Contains("Plans defined", dashboard);
        Assert.Contains("Phases &amp; Plans", index);
        Assert.DoesNotContain("Epics &amp; Stories", index);
        Assert.Contains("Phase 1", phase);
        Assert.Contains("Plan 1.0", plan);
    }

    // ---- Finding #7: three disagreeing completion signals -------------------------------------------------------

    /// <summary>The roadmap marks 4 of 6 plans complete; STATE.md claims 3 of 5; one summary file exists. Exactly
    /// ONE Informational notice names all three and says which is authoritative — nothing is averaged, and the
    /// summary set never overrides a declared <c>[x]</c>.</summary>
    [Fact]
    public void CompletionSignals_WhenTheyDisagree_OneInformationalNamesAllThree()
    {
        var bundle = Ingest();
        var notice = Assert.Single(bundle.Diagnostics.Where(d =>
            d.Category == AdapterDiagnosticCategory.Informational && d.Message.Contains("completion signals disagree")));

        Assert.Contains("4/5", notice.Message);
        Assert.Contains("STATE.md reports 3/5", notice.Message);
        Assert.Contains("1 '-SUMMARY.md'", notice.Message);
        Assert.Contains("authoritative", notice.Message);

        // And the model itself reports the ROADMAP's answer, not a reconciliation of the three.
        var epics = Assert.IsType<EpicsModel>(bundle.Epics);
        Assert.Equal(4, epics.Epics.SelectMany(e => e.Stories).Count(s => s.Status == "done"));
    }

    // ---- Milestones (Task 8 / AC #4) ----------------------------------------------------------------------------

    [Fact]
    public void Milestones_BandPhasesByRoadmapGroup_WithCanonicalStateWords()
    {
        var epics = Assert.IsType<EpicsModel>(Ingest().Epics);
        Assert.Equal(new[] { "v1.0", "v2.0", "Backlog" }, epics.Milestones.Select(m => m.Name).ToArray());

        var v1 = epics.Milestones[0];
        Assert.Equal(new[] { 1, 2, 3 }, v1.EpicNumbers.ToArray());
        Assert.Equal("done", v1.StatusWord);
        Assert.Equal("2026-05-27", v1.CompletedDate);

        // v2.0 declares a phase with no plans: not started, and NOT "unrecognized" — GSD's own "Not started" has
        // no StatusStyles arm, which is exactly why the adapter maps the word instead of passing it through.
        Assert.Equal("drafted", epics.Milestones[1].StatusWord);
        Assert.Null(epics.Milestones[1].CompletedDate);
        Assert.All(epics.Milestones, m => Assert.NotEqual("unrecognized", StatusStyles.ForStatus(m.StatusWord)));
    }

    // ---- Sprint (Task 7) ----------------------------------------------------------------------------------------

    [Fact]
    public void Sprint_ProjectsStateAndRoadmap_UsingTheSameOrdinalsAsTheEpics()
    {
        var bundle = Ingest();
        var sprint = Assert.IsType<SprintStatus>(bundle.Sprint);

        Assert.False(sprint.IsEmpty);
        Assert.Empty(sprint.ActionItems);
        Assert.Equal(5, sprint.Entries.Count(e => e.Kind == SprintEntryKind.Epic));
        // 5 plans: 2 in phase 1, 1 in phase 2, 2 in phase 2.1. Phase 7 lists none, and the backlog phase's
        // "- [ ] TBD (promote with …)" placeholder names no plan file, so it is correctly not a plan.
        Assert.Equal(5, sprint.Entries.Count(e => e.Kind == SprintEntryKind.Story));

        // Phase 1 is fully complete; phase 2.1 is partly complete; phase 7 has nothing.
        Assert.Equal("done", sprint.Entries.First(e => e.Kind == SprintEntryKind.Epic && e.EpicNumber == 1).Status);
        Assert.Equal("in-progress", sprint.Entries.First(e => e.Kind == SprintEntryKind.Epic && e.EpicNumber == 3).Status);
        Assert.Equal("backlog", sprint.Entries.First(e => e.Kind == SprintEntryKind.Epic && e.EpicNumber == 4).Status);

        // Every ledger value maps onto the canonical vocabulary — none renders as "unrecognized".
        Assert.All(sprint.Entries, e => Assert.NotEqual("unrecognized", StatusStyles.ForSprint(e.Status)));
    }

    [Fact]
    public void Sprint_WhenNoPhasesAreRecoverable_IsNullWithAnUnsupportedNotice()
    {
        File.WriteAllText(Path.Combine(Planning, "ROADMAP.md"), "# Roadmap\n\nNothing structured here.\n");
        var bundle = Ingest();

        Assert.Null(bundle.Sprint);
        Assert.Contains(bundle.Diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Unsupported && d.Message.Contains("no per-phase status"));
    }

    // ---- The rest of the bundle contract (D3, retros, config.json) ----------------------------------------------

    [Fact]
    public void Bundle_RequirementsStayNull_AndRetrosStayEmptyWithNoDiagnostic()
    {
        var bundle = Ingest();
        Assert.Null(bundle.Requirements);
        Assert.Empty(bundle.Retros);
        // Nothing in the notice stream claims a retrospective was found, skipped or malformed.
        Assert.DoesNotContain(bundle.Diagnostics, d => d.Message.Contains("retro", StringComparison.OrdinalIgnoreCase));
        // ModuleContext is BMad-typed: a GSD repo can only be None. Stated, not silently empty.
        Assert.Equal(BmadModule.Unknown, bundle.Module.Module);
    }

    [Fact]
    public void Bundle_ConfigJson_IsReportedAsAnUninterpretedBoundary()
    {
        Assert.Contains(Ingest().Diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Informational && d.Message.Contains("config.json"));
    }

    [Fact]
    public void Bundle_EpicsSourceIsTheRoadmap_SoTheGenericPassExcludesIt()
    {
        Assert.Equal(Path.Combine(Planning, "ROADMAP.md"), Ingest().EpicsSourceFullPath);
    }

    // ---- Malformed / absent handling (AC #2) --------------------------------------------------------------------

    [Fact]
    public void Roadmap_PresentButUnparseable_ReportsMalformed_AndNeverThrows()
    {
        File.WriteAllText(Path.Combine(Planning, "ROADMAP.md"), "not a roadmap at all");
        var bundle = Ingest();

        Assert.Null(bundle.Epics);
        Assert.Contains(bundle.Diagnostics, d => d.Category == AdapterDiagnosticCategory.Malformed);
    }

    [Fact]
    public void Roadmap_Absent_DegradesSilently_WithNoDiagnostic()
    {
        File.Delete(Path.Combine(Planning, "ROADMAP.md"));
        var bundle = Ingest();

        Assert.Null(bundle.Epics);
        Assert.Null(bundle.EpicsSourceFullPath);
        Assert.DoesNotContain(bundle.Diagnostics, d => d.Category is AdapterDiagnosticCategory.Malformed or AdapterDiagnosticCategory.Error);
    }

    /// <summary>The bounded answer to the single-valued-SourceRoot constraint: when <c>.planning/</c> is outside
    /// this run's source root its paths cannot be expressed relative to it, so the adapter contributes nothing but
    /// one notice saying so — rather than improvising a second path scheme (Story 4.9's question) or emitting
    /// paths that <see cref="PathUtil.EscapesRepoRoot"/> rejects.</summary>
    [Fact]
    public void PlanningOutsideTheSourceRoot_ContributesNothingButAStatedNotice()
    {
        var elsewhere = Path.Combine(_root, "_bmad-output");
        Directory.CreateDirectory(elsewhere);
        var options = ForgeOptions.Resolve(source: elsewhere, output: Path.Combine(_root, "site"));

        var bundle = new GsdCoreArtifactAdapter().Ingest(options, Array.Empty<string>(), Project);

        Assert.Null(bundle.Epics);
        Assert.Null(bundle.Sprint);
        Assert.Contains(bundle.Diagnostics, d =>
            d.Category == AdapterDiagnosticCategory.Informational && d.Message.Contains("outside this run's source root"));
    }

    /// <summary>The scoped watch-mode slice returns the same epics the full ingest does — the property AD-5 needs,
    /// stated as a test rather than assumed.</summary>
    [Fact]
    public void IngestEpics_MatchesTheFullIngestsEpicsFamily()
    {
        var scoped = new GsdCoreArtifactAdapter().IngestEpics(Options(), SourceFiles(), Project);
        var full = Ingest();

        Assert.Equal(full.EpicsSourceFullPath, scoped.SourceFullPath);
        Assert.Equal(full.Epics!.Epics.Count, scoped.Epics!.Epics.Count);
        Assert.Equal(full.StoryArtifactsById.Count, scoped.StoryArtifactsById.Count);
        Assert.Null(scoped.Requirements);
    }
}
