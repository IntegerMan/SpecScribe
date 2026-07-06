using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level regression coverage for Story 1.2 traceability: requirement-ID linkification,
/// source-citation reference-map routing (epics.md → epics.html, consumed artifacts → story pages, other
/// docs → mirrored html) across every story slice, fragment preservation, and ADR regeneration stale-output
/// safety on change/rename/delete.</summary>
public class SiteGeneratorTraceabilityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-trace-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string StoryPage => Path.Combine(Site, "epics", "story-1-1.html");
    private string HomeIndex => Path.Combine(Site, "index.html");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR6: Cross-link requirements, stories, and ADR references when IDs are detectable.

        ### NonFunctional Requirements

        NFR2: Generation degrades gracefully on malformed artifacts.

        ### FR Coverage Map

        FR6: Epic 1 - traceability
        NFR2: Epic 1 - resilience

        ## Epic List

        ### Epic 1: Foundation

        Stand up the traceability portal.

        ## Epic 1: Foundation

        ### Story 1.1: Traceability Links

        As a contributor, I want requirement links.

        ### Story 1.2: Second Story

        As a contributor, I want a second page.
        """;

    // Story 1.1 references FR6 (known) and FR99 (unknown), and cites a DISTINCT target from every one of the
    // six rendered slices production linkifies (SiteGenerator.cs:361-369) so each slice's source-link
    // processing can be asserted independently: blurb→epics.html, acceptance-criteria→prd.html,
    // remainder(Tasks)→rendering.html, dev-agent-record→architecture.html (with a #fragment),
    // review-findings→brief.html, change-log→story-1-2.html.
    private const string Story11Md = """
        # Story 1.1: Traceability Links

        Status: ready-for-dev

        ## Story

        As a contributor, I want FR6 linkified and unknown FR99 left as plain text.
        See _bmad-output/planning-artifacts/epics.md for the epic breakdown.

        ## Acceptance Criteria

        1. Given a reference, then it resolves. See _bmad-output/planning-artifacts/prd.md for detail.

        ## Tasks / Subtasks

        - [ ] Task 1: Wire up traceability per _bmad-output/planning-artifacts/rendering.md.

        ## Dev Agent Record

        ### Completion Notes List

        - Verified against _bmad-output/specs/architecture.md#Overview during implementation.

        ## Review Findings

        - Prior review referenced _bmad-output/planning-artifacts/brief.md for context.

        ## Change Log

        - 2026-01-01: Linked from _bmad-output/implementation-artifacts/1-2-second-story.md
        """;

    private const string Story12Md = """
        # Story 1.2: Second Story

        Status: backlog

        ## Story

        Second story body.

        ## Tasks / Subtasks

        - [ ] Task 1: Stub
        """;

    public SiteGeneratorTraceabilityTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "specs"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nProduct requirements.\n");
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "rendering.md"), "# Rendering\n\nRendering plan.\n");
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "brief.md"), "# Brief\n\nProject brief.\n");
        File.WriteAllText(Path.Combine(Source, "specs", "architecture.md"), "# Architecture\n\n## Overview\n\nArchitecture overview.\n");
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-traceability-links.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-2-second-story.md"), Story12Md);

        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords of decisions.\n");
        File.WriteAllText(Path.Combine(Adrs, "0001-first.md"),
            "# ADR 0001: First Decision\n\n**Status:** Accepted\n\nRelates to [ADR 0002](0002-second.md) and the [index](README.md). See the [context](0002-second.md#Context).\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-second.md"),
            "# ADR 0002: Second Decision\n\n**Status:** Proposed\n\nBody.\n");
    }

    public void Dispose()
    {
        // Best-effort cleanup: a transient Windows file lock (AV/handle) must not turn into a spurious test
        // failure or leak the temp tree. The OS reclaims %TEMP% regardless.
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source,
        adrs: Adrs,
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false);

    /// <summary>Runs a full generation and fails loudly if any page errored, so downstream absence-assertions
    /// can never pass simply because generation silently produced a <see cref="GenerationOutcome.Error"/>.</summary>
    private SiteGenerator GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        AssertNoErrors(gen.GenerateAll());
        return gen;
    }

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events) =>
        Assert.DoesNotContain(events, e => e.Outcome == GenerationOutcome.Error);

    // ---- Requirement-ID linkification (AC #1) ----

    [Fact]
    public void GenerateAll_LinksKnownRequirementIdOnStoryPage()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        Assert.Contains("class=\"req-ref\"", html);
        Assert.Contains("requirements/fr6.html", html);
    }

    [Fact]
    public void GenerateAll_LeavesUnknownRequirementIdAsPlainText()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        Assert.Contains("FR99", html);
        Assert.DoesNotContain("requirements/fr99.html", html);
        // Positive control: FR99 must remain plain text, not become the visible label of ANY anchor (a
        // regression pointing it at a default/other href would still satisfy the negative above).
        Assert.DoesNotContain(">FR99</a>", html);
    }

    [Fact]
    public void GenerateAll_SkipsSelfLinkOnRequirementDetailPage()
    {
        GenerateSite();

        var html = File.ReadAllText(Path.Combine(Site, "requirements", "fr6.html"));
        // Positive control first: FR6 must actually appear on its own detail page, otherwise the self-link
        // negative below passes vacuously (page could simply never mention FR6).
        Assert.Contains("FR6", html);
        // The FR6 detail page must never link FR6 to itself, but may still link other ids.
        Assert.DoesNotContain("href=\"../requirements/fr6.html\"", html);
    }

    // ---- Source-citation reference-map routing across every slice (AC #2) ----

    [Fact]
    public void GenerateAll_RoutesEpicsCitationToGeneratedEpicsHtml()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Blurb slice. epics.md is special-cased to the generated epics.html, never its mirrored render.
        Assert.Contains("href=\"../epics.html\"", html);
        Assert.DoesNotContain("planning-artifacts/epics.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesAcceptanceCriteriaCitationToMirroredHtml()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Acceptance-criteria slice → generic mirrored render.
        Assert.Contains("planning-artifacts/prd.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesRemainderCitationToMirroredHtml()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Remainder slice (the Tasks/Subtasks body) must receive source-link processing too.
        Assert.Contains("planning-artifacts/rendering.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesDevAgentRecordCitationToMirroredHtml()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Dev-agent-record slice.
        Assert.Contains("specs/architecture.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesReviewFindingsCitationToMirroredHtml()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Review-findings slice.
        Assert.Contains("planning-artifacts/brief.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesConsumedArtifactCitationToStoryPage()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // The change-log citation to the 1.2 artifact must resolve to its story detail page, proving both
        // consumed-artifact routing and change-log slice processing.
        Assert.Contains("epics/story-1-2.html", html);
    }

    [Fact]
    public void GenerateAll_PreservesSourceCitationFragmentOutsideTheLink()
    {
        GenerateSite();

        var html = File.ReadAllText(StoryPage);
        // Only the .md path is linked; a trailing "#Fragment" names prose, not a real id, so it must stay as
        // plain text OUTSIDE the anchor's href.
        Assert.Contains("specs/architecture.html", html);
        Assert.Contains("#Overview", html);
        Assert.DoesNotContain("architecture.html#Overview", html);
    }

    // ---- ADR cross-linking + regeneration coherence (AC #2) ----

    [Fact]
    public void GenerateAll_RewritesAdrCrossLinksAndSurfacesStatus()
    {
        GenerateSite();

        var adrHtml = File.ReadAllText(Path.Combine(Site, "adrs", "0001-first.html"));
        Assert.Contains("href=\"0002-second.html\"", adrHtml);
        Assert.Contains("href=\"index.html\"", adrHtml);

        // Scope the status assertion to ADR 0001's actual status pill so an unrelated occurrence of the word
        // "Accepted" (legend, CSS, another card) can't satisfy it.
        var index = File.ReadAllText(HomeIndex);
        Assert.Contains("status-accepted\">Accepted</span>", index);
    }

    [Fact]
    public void GenerateAll_PreservesAdrLinkFragment()
    {
        GenerateSite();

        var adrHtml = File.ReadAllText(Path.Combine(Site, "adrs", "0001-first.html"));
        // Markdown ADR links carry their fragment through the .md → .html rewrite unchanged.
        Assert.Contains("href=\"0002-second.html#Context\"", adrHtml);
    }

    [Fact]
    public void RegenerateAdrs_RemovesStalePageAndIndexCard_WhenAdrDeleted()
    {
        var gen = GenerateSite();
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));

        File.Delete(Path.Combine(Adrs, "0002-second.md"));
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateAdrs().Outcome);

        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));
        var index = File.ReadAllText(HomeIndex);
        Assert.DoesNotContain("0002-second.html", index);
        // Positive control: the untouched ADR 0001 must survive the regeneration intact, so "card removed"
        // can't be confused with "the whole ADR section failed to render".
        Assert.Contains("0001-first.html", index);
        Assert.Contains("status-accepted\">Accepted</span>", index);
    }

    [Fact]
    public void RegenerateAdrs_HandlesRename_WithoutLeavingStalePage()
    {
        var gen = GenerateSite();
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));

        File.Move(Path.Combine(Adrs, "0002-second.md"), Path.Combine(Adrs, "0002-renamed.md"));
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateAdrs().Outcome);

        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-renamed.html")));
        // The untouched ADR 0001 page must still exist after a sibling rename.
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0001-first.html")));
    }

    [Fact]
    public void RegenerateAdrs_ReflectsChangedStatus_OnIndexCard()
    {
        var gen = GenerateSite();

        File.WriteAllText(Path.Combine(Adrs, "0002-second.md"),
            "# ADR 0002: Second Decision\n\n**Status:** Superseded by [0001](0001-first.md)\n\nBody.\n");
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateAdrs().Outcome);

        // Status extraction flattens the markdown link in the status line to plain text for the card.
        var index = File.ReadAllText(HomeIndex);
        Assert.Contains("Superseded by 0001", index);
        // Prove the card was REPLACED, not appended to: the old "Proposed" status must be gone.
        Assert.DoesNotContain("Proposed", index);
    }
}
