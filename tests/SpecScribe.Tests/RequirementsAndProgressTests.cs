using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Requirements + progress tests share one realistic epics.md and drive the real pipeline
/// (EpicsParser → ProgressCalculator → RequirementsParser) rather than hand-building models.</summary>
public class RequirementsParserTests
{
    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: The game runs a day cycle
        FR2: Patients arrive

        **Village**
        FR3: The village grows

        ### NonFunctional Requirements

        NFR1: Loads fast

        ### FR Coverage Map

        FR1: Epic 1 - core loop
        FR2: Epic 1 - arrivals
        FR3: Deferred - post slice
        NFR1: Epic 1 - startup

        ## Epic List

        ### Epic 1: Foundation

        Stand it up.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want a skeleton, so that work can begin.
        """;

    private static RequirementsModel Parse(IReadOnlyDictionary<string, string>? artifacts = null)
    {
        var epics = EpicsParser.Parse(EpicsMd);
        var progress = ProgressCalculator.Compute(epics, artifacts ?? new Dictionary<string, string>(), git: null);
        return RequirementsParser.Parse(EpicsMd, epics, progress);
    }

    [Fact]
    public void Parse_SplitsFunctionalAndNonFunctional()
    {
        var model = Parse();

        Assert.Equal(3, model.Functional.Count);
        var nfr = Assert.Single(model.NonFunctional);
        Assert.Equal("NFR1", nfr.Id);
    }

    [Fact]
    public void Parse_TracksCategoriesForFunctionalRequirementsOnly()
    {
        var model = Parse();

        Assert.Equal("Core Loop", model.Functional[0].Category);
        Assert.Equal("Village", model.Functional[2].Category);
        Assert.Null(model.NonFunctional[0].Category);
    }

    [Fact]
    public void Parse_ReadsCoverageMap()
    {
        var model = Parse();

        Assert.Equal(1, model.ById["FR1"].CoverageEpicNumber);
        Assert.Equal("core loop", model.ById["FR1"].CoverageNote);
        Assert.True(model.ById["FR3"].Deferred);
        Assert.Equal(RequirementStatus.Deferred, model.ById["FR3"].Status);
    }

    [Fact]
    public void Parse_UncoveredEpicWithoutTaskPlansIsPlanned()
        => Assert.Equal(RequirementStatus.Planned, Parse().ById["FR1"].Status);

    [Fact]
    public void ById_IsCaseInsensitive()
        => Assert.True(Parse().ById.ContainsKey("fr1"));

    // ---- Story 3.7: multi-epic coverage (the structured FR→story mapping the Sankey stands on) ----

    private const string MultiEpicEpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        **Core Loop**
        FR1: Single-epic requirement
        FR2: Multi-epic requirement
        FR3: Deferred requirement
        FR4: Unmapped requirement

        ### NonFunctional Requirements

        NFR1: Covered via epic header
        NFR2: Unmapped non-functional

        ### UX Design Requirements

        UX-DR1: Covered design requirement
        UX-DR2: Unmapped design requirement

        ### FR Coverage Map

        FR1: Epic 1 - just the first
        FR2: Epics 1 & 2 - spans two epics
        FR3: Deferred - tech debt carried from Epic 1; revisit later
        FR4: covered somewhere but no epic number
        NFR1: Epic 1 - also on the map

        ## Epic List

        ### Epic 1: Foundation

        Stand it up.
        **FRs covered:** FR1 · **UX-DRs:** UX-DR1 · **NFRs:** NFR1

        ### Epic 2: Expansion

        Grow it.
        **FRs covered:** FR2

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want a skeleton, so that work can begin.

        ## Epic 2: Expansion

        ### Story 2.1: Widen

        As a dev, I want more surface, so that features fit.
        """;

    private static (RequirementsModel Reqs, EpicsModel Epics) ParseMultiEpic()
    {
        var epics = EpicsParser.Parse(MultiEpicEpicsMd);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        return (RequirementsParser.Parse(MultiEpicEpicsMd, epics, progress), epics);
    }

    [Fact]
    public void Parse_CapturesAllCoveringEpics_NotJustTheFirst()
    {
        var fr2 = ParseMultiEpic().Reqs.ById["FR2"];

        // Both covering epics are recorded, in order, de-duplicated.
        Assert.Equal(new[] { 1, 2 }, fr2.CoverageEpicNumbers);
        // ...while the singular primary is preserved as the FIRST covering epic (load-bearing for existing consumers).
        Assert.Equal(1, fr2.CoverageEpicNumber);
        // ...and status still rolls up from the primary epic (semantics unchanged).
        Assert.Equal(RequirementStatus.Planned, fr2.Status);
    }

    [Fact]
    public void Parse_SingleEpicCoverageHasOneElementList()
    {
        var fr1 = ParseMultiEpic().Reqs.ById["FR1"];
        Assert.Equal(new[] { 1 }, fr1.CoverageEpicNumbers);
        Assert.Equal(1, fr1.CoverageEpicNumber);
    }

    [Fact]
    public void Parse_DeferredAndUnmappedHaveEmptyCoverageEpics()
    {
        var reqs = ParseMultiEpic().Reqs;

        var deferred = reqs.ById["FR3"];
        Assert.True(deferred.Deferred);
        Assert.Empty(deferred.CoverageEpicNumbers);
        Assert.Null(deferred.CoverageEpicNumber);

        var unmapped = reqs.ById["FR4"];
        Assert.False(unmapped.Deferred);
        Assert.Empty(unmapped.CoverageEpicNumbers);
        Assert.Null(unmapped.CoverageEpicNumber);
    }

    // ---- Story 9.3: deferred-on-purpose vs unmapped as distinct status tiers ----

    [Fact]
    public void DeriveStatus_NoCoveringEpicNotDeferred_IsUnmapped_DistinctFromPlannedAndDeferred()
    {
        var reqs = ParseMultiEpic().Reqs;

        // FR4 (and the uncovered NFR2 / UX-DR2) have no covering epic and are not deferred → the new Unmapped
        // tier, NOT the old overloaded "Planned". This is the false-oversight-vs-intentional-scope fix. [Story 9.3]
        Assert.Equal(RequirementStatus.Unmapped, reqs.ById["FR4"].Status);
        Assert.Equal(RequirementStatus.Unmapped, reqs.ById["NFR2"].Status);
        Assert.Equal(RequirementStatus.Unmapped, reqs.ById["UX-DR2"].Status);

        // Deferred-on-purpose stays its own distinct tier — never conflated with unmapped.
        Assert.Equal(RequirementStatus.Deferred, reqs.ById["FR3"].Status);

        // A requirement WITH a real covering epic that simply hasn't started stays Planned (both Epic 1 & 2 are
        // merely drafted here) — the legitimate reading of "Planned", now no longer sharing a bucket with a gap.
        Assert.Equal(RequirementStatus.Planned, reqs.ById["FR2"].Status);
        Assert.Equal(RequirementStatus.Planned, reqs.ById["FR1"].Status);
    }

    [Fact]
    public void DeriveStatus_NamedEpicThatDoesNotExist_StaysPlannedNotUnmappedNorDone()
    {
        // Edge shape the parser must not over-claim: a coverage line names an epic number with no matching epic
        // in the model. Author intent to map exists (not Unmapped), the epic can't roll up to done (not Done) →
        // it reads "covered but not started" = Planned. Guards the vacuous-All-on-empty Done trap. [Story 9.3]
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            **Core**
            FR1: Names a phantom epic

            ### FR Coverage Map

            FR1: Epic 99 - typo'd or since-removed epic number

            ## Epic List

            ### Epic 1: Real

            Goal.

            ## Epic 1: Real

            ### Story 1.1: A

            As a user, I want a, so that b.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);

        Assert.Equal(new[] { 99 }, reqs.ById["FR1"].CoverageEpicNumbers);
        Assert.Equal(RequirementStatus.Planned, reqs.ById["FR1"].Status);
    }

    [Fact]
    public void StoriesFor_ResolvesCoveringEpicsToTheirStories_InSourceOrder()
    {
        var (reqs, epics) = ParseMultiEpic();

        var fr2Stories = RequirementsParser.StoriesFor(reqs.ById["FR2"], epics).Select(s => s.Id).ToList();
        Assert.Equal(new[] { "1.1", "2.1" }, fr2Stories);

        // A deferred/unmapped requirement resolves to no stories.
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["FR3"], epics));
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["FR4"], epics));
    }

    [Fact]
    public void DeriveStatus_PartiallyImplemented_WhenACoveringEpicHasAnInProgressStory()
    {
        // Story 3.7 follow-up: requirements now surface a story-derived "partially implemented" (Active) tier
        // when a covering epic has work in flight — the earlier design refused this; now the FR→story mapping
        // backs it. A covering epic with an in-progress story rolls up to ForEpic == "active".
        var dir = Directory.CreateTempSubdirectory("ss-req-active-").FullName;
        try
        {
            var artifact = Path.Combine(dir, "1-1.md");
            File.WriteAllText(artifact, "# Story 1.1\nStatus: in progress\n\n## Tasks / Subtasks\n\n- [x] a\n- [ ] b\n");
            var epics = EpicsParser.Parse(MultiEpicEpicsMd);
            var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);
            var reqs = RequirementsParser.Parse(MultiEpicEpicsMd, epics, progress);

            // FR1 (Epic 1, in-progress story) and FR2 (Epics 1 & 2 — Epic 1 active) both read "partially implemented".
            Assert.Equal(RequirementStatus.Active, reqs.ById["FR1"].Status);
            Assert.Equal(RequirementStatus.Active, reqs.ById["FR2"].Status);
            // Deferred is unaffected; the genuinely-unmapped FR4 now reads Unmapped, not Planned. [Story 9.3]
            Assert.Equal(RequirementStatus.Deferred, reqs.ById["FR3"].Status);
            Assert.Equal(RequirementStatus.Unmapped, reqs.ById["FR4"].Status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void DeriveStatus_AllDoneCoveringEpicIsDone_EvenWithNoRetrospective()
    {
        // Guard: requirement roll-up must NOT be retro-gated. A covering epic whose every story is done rolls
        // its requirement up to Done regardless of whether a retrospective exists — a retro is a closure ritual,
        // not an implementation signal. Swapping DeriveStatus onto ForEpicWithRetrospective would wrongly drop a
        // fully-built requirement to Planned. This is exactly the all-done-no-retro state the sunburst renders as
        // "In review", so the divergence between the two classifiers is intentional and pinned here. [spec-sunburst-retro]
        var dir = Directory.CreateTempSubdirectory("ss-req-done-").FullName;
        try
        {
            var artifact = Path.Combine(dir, "1-1.md");
            File.WriteAllText(artifact, "# Story 1.1\nStatus: done\n\n## Tasks / Subtasks\n\n- [x] a\n- [x] b\n");
            var epics = EpicsParser.Parse(MultiEpicEpicsMd);
            var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);
            // The pipeline never sets HasRetrospective (SiteGenerator does, from the retro map) — so it defaults
            // false here: the all-done-no-retro state.
            Assert.All(epics.Epics, e => Assert.False(e.HasRetrospective));

            var reqs = RequirementsParser.Parse(MultiEpicEpicsMd, epics, progress);

            // FR1 is covered solely by Epic 1, now fully done → Done, not Planned.
            Assert.Equal(RequirementStatus.Done, reqs.ById["FR1"].Status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void RenderIndex_SatisfactionBand_ShowsFourReadingsOverEverything()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var counts = ProjectCounts.Build(progress, null, WorkInventory.Empty, epics, reqs);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav, counts);

        Assert.Contains("id=\"satisfaction\"", html);
        Assert.Contains("Satisfaction at a glance", html);
        Assert.Contains("satisfaction-band", html);
        Assert.Contains("satisfaction-bar", html);
        Assert.Contains("satisfaction-chips", html);
        Assert.Contains("Satisfied", html);
        Assert.Contains("In flight", html);
        Assert.Contains("Deferred on purpose", html);
        Assert.Contains("Not yet mapped", html);
        Assert.Contains(Icons.ForStatus("unmapped"), html);
        // In-flight tooltip enumerates the lifecycle (Active/Ready/Planned).
        Assert.Contains("partially implemented", html);
        Assert.Contains("ready for dev", html);
        // Design (UX-DR) is in the ledger totals — Everything has 8 reqs (4 planned + 3 unmapped + 1 deferred).
        Assert.Equal(8, counts.RequirementsOverall.Total);
        Assert.Equal(2, counts.RequirementsDesign.Total);
        Assert.Contains("seg pending", html); // Planned / Unmapped tiers
        // Existing FR+NFR surfaces still present (additive band, not a replacement).
        Assert.Contains("Requirements at a glance", html);
        Assert.Contains("id=\"at-a-glance\"", html);
        Assert.Contains("Requirements flow", html);
        Assert.Contains("req-status-grid", html);
        Assert.Contains("req-flow-svg", html);
    }

    [Fact]
    public void RenderIndex_EmptyEverything_OmitsSatisfactionBand()
    {
        var epics = EpicsParser.Parse("""
            # Epics
            ## Requirements Inventory
            ### Functional Requirements
            **Core**
            ## FR Coverage Map
            ## Epic List
            ### Epic 1: Alone
            Goal.
            ## Epic 1: Alone
            ### Story 1.1: A
            As a user, I want x, so that y.
            """);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse("""
            # Epics
            ## Requirements Inventory
            ### Functional Requirements
            **Core**
            ## FR Coverage Map
            ## Epic List
            ### Epic 1: Alone
            Goal.
            ## Epic 1: Alone
            ### Story 1.1: A
            As a user, I want x, so that y.
            """, epics, progress);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        Assert.DoesNotContain("Satisfaction at a glance", html);
        Assert.DoesNotContain("id=\"satisfaction\"", html);
        Assert.DoesNotContain("satisfaction-band", html);
    }

    [Fact]
    public void RenderIndex_PopulatedProject_ContainsStatusGridAndFlowPanel()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        // The status-block grid section (AC #1) with a labeled block per requirement...
        Assert.Contains("Requirements at a glance", html);
        Assert.Contains("req-status-grid", html);
        Assert.Contains("req-status-block", html);
        // ...and the requirements flow panel (AC #2), including the honest "No coverage" node.
        Assert.Contains("Requirements flow", html);
        Assert.Contains("req-flow-svg", html);
        Assert.Contains("No coverage", html);
    }

    // ---- Story 9.2: NFR / UX-DR first-class parsing, coverage, and rendering ----

    [Fact]
    public void Parse_UxDrs_IntoDesign_WithIdsAndSlugs_AllExcludesDesign()
    {
        var reqs = ParseMultiEpic().Reqs;

        Assert.Equal(2, reqs.Design.Count);
        Assert.Equal("UX-DR1", reqs.Design[0].Id);
        Assert.Equal("ux-dr1", reqs.Design[0].Slug);
        Assert.Equal("UX-DR2", reqs.Design[1].Id);
        Assert.Equal("ux-dr2", reqs.Design[1].Slug);
        Assert.Equal(RequirementKind.Design, reqs.Design[0].Kind);

        // ById resolves UX-DRs; All stays FR+NFR only (FR flow/grid scope).
        Assert.True(reqs.ById.ContainsKey("UX-DR1"));
        Assert.Equal(6, reqs.All.Count()); // FR1–4 + NFR1–2
        Assert.DoesNotContain(reqs.All, r => r.Kind == RequirementKind.Design);
        Assert.Equal(6 + 2, reqs.Everything.Count()); // All + Design
    }

    [Fact]
    public void Parse_NfrAndUxDr_CoverageFromEpicHeaderUnion_UnmappedStayEmpty()
    {
        var (reqs, epics) = ParseMultiEpic();

        // NFR1 is on BOTH the FR Coverage Map (Epic 1) and the epic header — union de-dups to [1].
        Assert.Equal(new[] { 1 }, reqs.ById["NFR1"].CoverageEpicNumbers);
        Assert.Equal(1, reqs.ById["NFR1"].CoverageEpicNumber);

        // UX-DR1 covered only via epic-header reverse index.
        Assert.Equal(new[] { 1 }, reqs.ById["UX-DR1"].CoverageEpicNumbers);
        Assert.Equal(new[] { "1.1" }, RequirementsParser.StoriesFor(reqs.ById["UX-DR1"], epics).Select(s => s.Id).ToList());

        // Unmapped NFR/UX-DR — empty coverage, StoriesFor empty.
        Assert.Empty(reqs.ById["NFR2"].CoverageEpicNumbers);
        Assert.Empty(reqs.ById["UX-DR2"].CoverageEpicNumbers);
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["UX-DR2"], epics));
    }

    [Fact]
    public void Parse_UxDrLineInFrCoverageMap_UnionsWithHeader()
    {
        // Task 2: UX-DR coverage = header ∪ map — map lines must be ingestible. [Story 9.2 review]
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            FR1: Only

            ### UX Design Requirements

            UX-DR1: Map-only coverage
            UX-DR2: Header-only coverage

            ### FR Coverage Map

            FR1: Epic 1 - only
            UX-DR1: Epic 2 - from the map

            ## Epic List

            ### Epic 1: Foundation

            Goal.
            **UX-DRs:** UX-DR2

            ### Epic 2: Expansion

            Goal.

            ## Epic 1: Foundation

            ### Story 1.1: A

            As a user, I want a, so that b.

            ## Epic 2: Expansion

            ### Story 2.1: B

            As a user, I want b, so that c.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);

        Assert.Equal(new[] { 2 }, reqs.ById["UX-DR1"].CoverageEpicNumbers);
        Assert.Equal(new[] { 1 }, reqs.ById["UX-DR2"].CoverageEpicNumbers);
    }

    [Fact]
    public void Parse_DeferredNfr_DoesNotUnionHeaderEpics()
    {
        // Deferred map entry must not pick up header covering epics. [Story 9.2 review]
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            FR1: Only

            ### NonFunctional Requirements

            NFR1: Deferred but casually tagged in a header

            ### FR Coverage Map

            FR1: Epic 1 - only
            NFR1: Deferred - shelved on purpose

            ## Epic List

            ### Epic 1: Foundation

            Goal.
            **NFRs:** NFR1

            ## Epic 1: Foundation

            ### Story 1.1: A

            As a user, I want a, so that b.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);

        Assert.True(reqs.ById["NFR1"].Deferred);
        Assert.Equal(RequirementStatus.Deferred, reqs.ById["NFR1"].Status);
        Assert.Empty(reqs.ById["NFR1"].CoverageEpicNumbers);
        Assert.Empty(RequirementsParser.StoriesFor(reqs.ById["NFR1"], epics));
    }

    [Fact]
    public void RenderIndex_OrphanCoveringEpic_ShowsHonestAbsenceNote()
    {
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            FR1: Only

            ### NonFunctional Requirements

            NFR1: Named epic that does not exist

            ### FR Coverage Map

            FR1: Epic 1 - only
            NFR1: Epic 99 - ghost

            ## Epic List

            ### Epic 1: Foundation

            Goal.

            ## Epic 1: Foundation

            ### Story 1.1: A

            As a user, I want a, so that b.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        Assert.Equal(new[] { 99 }, reqs.ById["NFR1"].CoverageEpicNumbers);
        Assert.Equal(RequirementStatus.Planned, reqs.ById["NFR1"].Status);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);
        Assert.Contains("Covering epic not found in the epic list.", html);
        Assert.DoesNotContain("nfr-uxdr-epic-list", html);
    }

    [Fact]
    public void Parse_FrCoverageStaysMapOnly_HeaderFrTokensDoNotUnion()
    {
        // Epic 1's header lists FR1; Epic 2's lists FR2. FR2's map already has Epics 1 & 2.
        // Header must NOT change FR coverage (FR output byte-identical guardrail).
        var fr2 = ParseMultiEpic().Reqs.ById["FR2"];
        Assert.Equal(new[] { 1, 2 }, fr2.CoverageEpicNumbers);
        Assert.Equal(1, fr2.CoverageEpicNumber);
    }

    [Fact]
    public void RenderIndex_NfrUxDrCoverageSection_ShowsMappedAndUnmappedTreatments()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        Assert.Contains("Non-functional &amp; design coverage", html);
        Assert.Contains("Non-functional requirements", html);
        Assert.Contains("UX design requirements", html);
        Assert.Contains("Design (", html); // Design donut
        Assert.Contains("design</div>", html); // subtitle count includes design

        // Covered UX-DR/NFR: "Delivered by" epic cards after the description (not header chips).
        Assert.Contains("nfr-uxdr-epics-label", html);
        Assert.Contains("Delivered by", html);
        Assert.Contains("nfr-uxdr-epic-card", html);
        Assert.Contains("href=\"epics/epic-1.html\"", html);
        Assert.Contains("nfr-uxdr-epic-num\">Epic 1<", html);

        // Unmapped: "Not yet mapped" badge (icon+word via StatusStyles.Badge) — never a bare "Planned".
        Assert.Contains(">Not yet mapped<", html);
        Assert.Contains("Not yet mapped to a delivering epic.", html);
        // The unmapped coverage row must not stamp Planned as its badge label.
        Assert.Contains("id=\"cov-ux-dr2\"", html);
        var covUxDr2 = html[html.IndexOf("id=\"cov-ux-dr2\"", StringComparison.Ordinal)..];
        var end = covUxDr2.IndexOf("</div>\n\n", StringComparison.Ordinal);
        var row = end >= 0 ? covUxDr2[..end] : covUxDr2;
        Assert.Contains("Not yet mapped", row);
        Assert.DoesNotContain(">Planned<", row);
    }

    [Fact]
    public void RenderIndex_NoNfrOrDesign_OmitsCoverageSectionAndDesignDonut()
    {
        // NFR8 degrade-gracefully: absent sub-groups/donut, not empty/broken.
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            **Core**
            FR1: Only functional

            ### FR Coverage Map

            FR1: Epic 1 - only

            ## Epic List

            ### Epic 1: Solo

            Goal.

            ## Epic 1: Solo

            ### Story 1.1: A

            As a user, I want a, so that b.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        Assert.DoesNotContain("Non-functional &amp; design coverage", html);
        Assert.DoesNotContain(">Design (", html);
        Assert.DoesNotContain(">Non-functional (", html);
        Assert.DoesNotContain(" non-functional", html);
        Assert.DoesNotContain("design</div>", html);
        Assert.Empty(reqs.Design);
        Assert.Empty(reqs.NonFunctional);
    }

    [Fact]
    public void RenderRequirement_UxDr_KickerAndBackLink()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var req = reqs.ById["UX-DR1"];

        var html = RequirementsTemplater.RenderRequirement(req, progress, nav, epics);

        Assert.Contains("UX Design Requirement", html);
        Assert.Contains("requirements.html", html);
        Assert.Contains("req-detail", html);
    }

    // ---- Story 9.1: requirement detail page lists its covering stories, grouped by epic ----

    private static string RenderDetail(string reqId)
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        return RequirementsTemplater.RenderRequirement(reqs.ById[reqId], progress, nav, epics);
    }

    [Fact]
    public void RenderRequirement_MultiEpicCovered_ListsStoriesFromEveryCoveringEpic_LinkedAndBadged()
    {
        // FR2 spans Epics 1 & 2 — the regression this story also fixes: the old single-card Coverage body
        // showed only the primary epic. Every covering epic's stories must appear, grouped, each linked to its
        // page and carrying a canonical status badge.
        var html = RenderDetail("FR2");

        // Both covering epic group headers.
        Assert.Contains("epics/epic-1.html", html);
        Assert.Contains("epics/epic-2.html", html);

        // Each covering epic's stories link to their (placeholder) story pages, from both epics.
        Assert.Contains($"href=\"../{StoryEpicLinkifier.StoryPagePath("1.1")}\"", html);
        Assert.Contains($"href=\"../{StoryEpicLinkifier.StoryPagePath("2.1")}\"", html);

        // Rendered as grouped compact story cards, with a canonical status badge (drafted → "Drafted").
        Assert.Contains("coverage-story-card", html);
        Assert.Contains("status-badge drafted", html);
        Assert.Contains(">Drafted<", html);

        // Honest, epic-level framing — never phrased as a per-story mapping.
        Assert.Contains("grouped by epic", html);
    }

    [Fact]
    public void RenderRequirement_SingleEpicCovered_DoesNotLeakOtherEpicsStories()
    {
        // FR1 is covered solely by Epic 1 → only Epic 1's story appears; Epic 2's story must not leak in.
        var html = RenderDetail("FR1");

        Assert.Contains($"href=\"../{StoryEpicLinkifier.StoryPagePath("1.1")}\"", html);
        Assert.DoesNotContain($"href=\"../{StoryEpicLinkifier.StoryPagePath("2.1")}\"", html);
    }

    [Fact]
    public void RenderRequirement_DeferredVsUnmapped_RenderDistinctEmptyStates()
    {
        // AC #2: an uncovered requirement states it explicitly, and deferred-on-purpose reads distinctly from
        // genuinely-unmapped (the copy-level distinction 9.1 makes; 9.3 adds the visual treatment).
        var deferred = RenderDetail("FR3");
        var unmapped = RenderDetail("FR4");

        Assert.Contains("Deferred — not yet assigned to an epic.", deferred);
        Assert.Contains("Not yet mapped to any epic or story.", unmapped);

        // The two empty states are genuinely different, not the same note reused.
        Assert.DoesNotContain("Not yet mapped to any epic or story.", deferred);
        Assert.DoesNotContain("Deferred — not yet assigned to an epic.", unmapped);

        // Neither uncovered requirement fabricates a story card.
        Assert.DoesNotContain("coverage-story-card", deferred);
        Assert.DoesNotContain("coverage-story-card", unmapped);
    }

    [Fact]
    public void RenderRequirement_NamedButMissingEpic_DoesNotClaimUnmapped()
    {
        // Coverage map names Epic 99 (absent from the model). DeriveStatus stays Planned; the Coverage body
        // must NOT reuse the Unmapped "Not yet mapped" copy — that contradicted the header badge. [9.1 review]
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            **Core**
            FR1: Names a phantom epic

            ### FR Coverage Map

            FR1: Epic 99 - typo'd or since-removed epic number

            ## Epic List

            ### Epic 1: Real

            Goal.

            ## Epic 1: Real

            ### Story 1.1: A

            As a user, I want a, so that b.
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderRequirement(reqs.ById["FR1"], progress, nav, epics);

        Assert.Contains("Covering epic(s) named in the map were not found in the epic list.", html);
        Assert.Contains("since-removed epic number", html);
        Assert.DoesNotContain("Not yet mapped to any epic or story.", html);
        Assert.DoesNotContain("coverage-story-card", html);
        Assert.Contains(">Planned<", html);
    }

    [Fact]
    public void RenderRequirement_CoveringEpicWithNoStories_StatesEmptyExplicitly()
    {
        // Task 3: a covering epic with zero drafted stories gets an explicit per-group note, not a blank group.
        var md = """
            # Epics

            ## Requirements Inventory

            ### Functional Requirements

            **Core**
            FR1: Covered by an empty epic

            ### FR Coverage Map

            FR1: Epic 1 - empty shell

            ## Epic List

            ### Epic 1: Empty Shell

            Listed but no stories drafted yet.

            ## Epic 1: Empty Shell
            """;
        var epics = EpicsParser.Parse(md);
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var reqs = RequirementsParser.Parse(md, epics, progress);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderRequirement(reqs.ById["FR1"], progress, nav, epics);

        Assert.Contains("No stories drafted in this epic yet.", html);
        Assert.DoesNotContain("coverage-story-card", html);
        Assert.Contains("epics/epic-1.html", html);
    }

    // ---- Story 9.3: distinct visual treatment + deferral-source linking ----

    [Fact]
    public void RenderRequirement_Unmapped_ShowsNotYetMappedBadge_NotPlanned_WithDistinctIcon()
    {
        // FR4 is unmapped: its header badge (and coverage-body note) must read "Not yet mapped", never the
        // misleading "Planned", in the tan pending family with the distinct unmapped glyph. [Story 9.3 Task 5]
        var html = RenderDetail("FR4");

        Assert.Contains(">Not yet mapped<", html);
        Assert.DoesNotContain(">Planned<", html);
        // Reuses the pending/tan color class (owner decision #1 — no 7th token)...
        Assert.Contains("status-badge pending", html);
        // ...but carries the unmapped icon glyph (the dashed-slot rect), so it never reads color-only vs Planned.
        Assert.Contains(Icons.ForStatus("unmapped"), html);
    }

    [Fact]
    public void RenderIndex_UnmappedRequirement_ShowsDistinctTreatmentAcrossGridCardsAndDonut()
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = RequirementsTemplater.RenderIndex(reqs, epics, progress, nav);

        // The donut legend gains a distinct "Not yet mapped" segment separate from "Planned".
        Assert.Contains("Not yet mapped", html);
        Assert.Contains(">Planned<", html); // FR1/FR2 are genuinely Planned — still present, not swallowed
        // The requirement card for the unmapped FR4 flags it explicitly (was a silent no-chip else before).
        Assert.Contains("req-epic unmapped", html);
        // The unmapped requirement never renders a misleading "Planned" badge on its own card.
        var fr4 = html[html.IndexOf("id=\"fr4\"", StringComparison.Ordinal)..];
        var fr4End = fr4.IndexOf("</div>\n\n", StringComparison.Ordinal);
        var fr4Card = fr4End >= 0 ? fr4[..fr4End] : fr4;
        Assert.Contains("Not yet mapped", fr4Card);
        Assert.DoesNotContain(">Planned<", fr4Card);
    }

    private static string RenderDetailWithSources(string reqId, IReadOnlyDictionary<int, string>? retroMap, string? deferredWorkHref)
    {
        var (reqs, epics) = ParseMultiEpic();
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        return RequirementsTemplater.RenderRequirement(reqs.ById[reqId], progress, nav, epics, retroMap, deferredWorkHref);
    }

    [Fact]
    public void RenderRequirement_DeferredWithResolvableSource_LinksToRetroAndDeferredWork()
    {
        // FR3's note is "tech debt carried from Epic 1; revisit later" — the heuristic resolves BOTH an Epic 1
        // retro link and (debt language) the deferred-work backlog link. [Story 9.3 Task 5 / AC #2]
        var retroMap = new Dictionary<int, string> { [1] = "retros/epic-1-retro.html" };
        var html = RenderDetailWithSources("FR3", retroMap, "deferred-work.html");

        Assert.Contains("deferral-sources", html);
        Assert.Contains("From Epic 1 retrospective", html);
        Assert.Contains("In deferred-work backlog", html);
        // Hrefs are prefixed to the detail page's requirements/ depth — never a broken root-relative link.
        Assert.Contains("href=\"../retros/epic-1-retro.html\"", html);
        Assert.Contains("href=\"../deferred-work.html\"", html);
    }

    [Fact]
    public void RenderRequirement_DeferredWithNoMatchingSource_RendersNoLink_Gracefully()
    {
        // A deferred requirement whose note names no epic and no debt language, OR whose targets don't exist,
        // renders plain text — never a fabricated or broken link (AC #2 "when one exists"; NFR2 degrade). [Story 9.3]

        // (a) note matches "Epic 1" + debt, but no maps supplied → no link surface at all.
        var noMaps = RenderDetailWithSources("FR3", retroMap: null, deferredWorkHref: null);
        Assert.DoesNotContain("deferral-sources", noMaps);

        // (b) maps supplied, but the note names an epic with no retro in the map and no debt page → still no link.
        var unrelated = RenderDetailWithSources("FR3", new Dictionary<int, string> { [7] = "retros/epic-7-retro.html" }, deferredWorkHref: null);
        Assert.DoesNotContain("deferral-sources", unrelated);
    }
}

public class ProgressCalculatorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteArtifact(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static EpicsModel Epics => EpicsParser.Parse("""
        ## Epic List

        ### Epic 1: Foundation

        Goal.

        ## Epic 1: Foundation

        ### Story 1.1: Scaffold

        As a dev, I want scaffolding.

        ### Story 1.2: Second story

        As a dev, I want more.
        """);

    [Fact]
    public void Compute_TalliesTasksFromArtifacts()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: in progress

            ## Tasks / Subtasks

            - [x] Done task
            - [ ] Open task
            - [X] Also done
            """);

        var epics = Epics;
        var progress = ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);

        Assert.Equal(2, progress.TasksDone);
        Assert.Equal(3, progress.TasksTotal);
        Assert.Equal(1, progress.StoriesWithArtifact);
        Assert.Equal(2, progress.StoriesTotal);

        // Side effect: the story itself is annotated for downstream rendering.
        var story = epics.Epics[0].Stories[0];
        Assert.Equal(2, story.TasksDone);
        Assert.Equal("in progress", story.Status);
    }

    [Fact]
    public void Compute_ZeroStateWhenNoArtifactsExist()
    {
        var progress = ProgressCalculator.Compute(Epics, new Dictionary<string, string>(), git: null);

        Assert.Equal(0, progress.TasksTotal);
        Assert.Equal(0, progress.StoriesWithArtifact);
        Assert.Equal(1, progress.EpicsDrafted);
        Assert.Equal(0, progress.EpicsPending);
    }

    [Fact]
    public void Compute_MissingArtifactFileCountsAsZeroInsteadOfThrowing()
    {
        var map = new Dictionary<string, string> { ["1.1"] = Path.Combine(_dir, "does-not-exist.md") };
        var progress = ProgressCalculator.Compute(Epics, map, git: null);

        Assert.Equal(0, progress.TasksTotal);
        Assert.Equal(1, progress.StoriesWithArtifact);
    }

    // ---- Story 8.8 LastUpdatedDate resolution ------------------------------------------------------------

    private static DeepGitPulse DeepWithFileDate(string repoRelativePath, DateOnly lastDate) =>
        new(Array.Empty<(string, int)>(), Array.Empty<(string, string, int)>())
        {
            CodeMapMetrics = new Dictionary<string, CodeFileMetrics>(StringComparer.Ordinal)
            {
                [repoRelativePath] = new CodeFileMetrics(1, 1, lastDate, lastDate),
            },
        };

    [Fact]
    public void Compute_LastUpdatedDate_PrefersGitFileDateOverChangeLog()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task

            ## Change Log
            - 2026-07-01: Change-log date (should lose)
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/1-1-scaffold.md";

        var deep = DeepWithFileDate(
            "_bmad-output/implementation-artifacts/1-1-scaffold.md",
            new DateOnly(2026, 7, 14));

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null, deep);

        Assert.Equal(new DateOnly(2026, 7, 14), epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_PrefersGitEvenWhenOlderThanChangeLog()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task

            ## Change Log
            - 2026-07-20: Authored later than last git touch
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/1-1-scaffold.md";

        var deep = DeepWithFileDate(
            "_bmad-output/implementation-artifacts/1-1-scaffold.md",
            new DateOnly(2026, 7, 1));

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null, deep);

        Assert.Equal(new DateOnly(2026, 7, 1), epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_FallsBackToChangeLogWhenDeepNull()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task

            ## Change Log
            - 2026-07-08: Only source
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/1-1-scaffold.md";

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null, deep: null);

        Assert.Equal(new DateOnly(2026, 7, 8), epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_FallsBackWhenGitPathUnmatched()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task

            ## Change Log
            - 2026-07-10: Fallback after unmatched path
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/1-1-scaffold.md";

        var deep = DeepWithFileDate("src/other.cs", new DateOnly(2026, 7, 14));

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null, deep);

        Assert.Equal(new DateOnly(2026, 7, 10), epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_NullWhenNeitherGitNorChangeLog()
    {
        var artifact = WriteArtifact("1-1-scaffold.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/1-1-scaffold.md";

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null);

        Assert.Null(epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_ClearsWhenArtifactMissingOnRecompute()
    {
        var epics = Epics;
        epics.Epics[0].Stories[0].LastUpdatedDate = new DateOnly(2026, 7, 1);

        ProgressCalculator.Compute(epics, new Dictionary<string, string>(), git: null);

        Assert.Null(epics.Epics[0].Stories[0].LastUpdatedDate);
    }

    [Fact]
    public void Compute_LastUpdatedDate_PathKeyUsesSourceDirNamePlusArtifactSourcePath()
    {
        // Guards the path-reconciliation sharp edge: ArtifactSourcePath is relative to _bmad-output/,
        // while git paths are repo-root-relative.
        var artifact = WriteArtifact("renamed.md", """
            # Story 1.1
            Status: ready-for-dev

            ## Tasks / Subtasks
            - [ ] Task

            ## Change Log
            - 2026-01-01: Should lose to git
            """);

        var epics = Epics;
        epics.Epics[0].Stories[0].ArtifactSourcePath = "implementation-artifacts/renamed.md";

        var expectedKey = PathUtil.NormalizeSlashes($"{ForgeOptions.SourceDirName}/implementation-artifacts/renamed.md");
        Assert.Equal("_bmad-output/implementation-artifacts/renamed.md", expectedKey);

        var deep = DeepWithFileDate(expectedKey, new DateOnly(2026, 6, 15));

        ProgressCalculator.Compute(epics, new Dictionary<string, string> { ["1.1"] = artifact }, git: null, deep);

        Assert.Equal(new DateOnly(2026, 6, 15), epics.Epics[0].Stories[0].LastUpdatedDate);
    }
}
