using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level regression coverage for Story 1.2 traceability: requirement-ID linkification,
/// source-citation reference-map routing (epics.md → epics.html, consumed artifacts → story pages, other
/// docs → mirrored html), and ADR regeneration stale-output safety on change/rename/delete.</summary>
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

    // Story 1.1 references FR6 (known) and FR99 (unknown), and cites three different targets across three
    // different rendered slices (blurb, acceptance criteria, change log) so reference-map routing coverage
    // for each slice can be asserted independently.
    private const string Story11Md = """
        # Story 1.1: Traceability Links

        Status: ready-for-dev

        ## Story

        As a contributor, I want FR6 linkified and unknown FR99 left as plain text.
        See _bmad-output/planning-artifacts/epics.md for the epic breakdown.

        ## Acceptance Criteria

        1. Given a reference, then it resolves. See _bmad-output/planning-artifacts/prd.md for detail.

        ## Tasks / Subtasks

        - [ ] Task 1: Wire up traceability

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
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nProduct requirements.\n");
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-traceability-links.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-2-second-story.md"), Story12Md);

        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords of decisions.\n");
        File.WriteAllText(Path.Combine(Adrs, "0001-first.md"),
            "# ADR 0001: First Decision\n\n**Status:** Accepted\n\nRelates to [ADR 0002](0002-second.md) and the [index](README.md).\n");
        File.WriteAllText(Path.Combine(Adrs, "0002-second.md"),
            "# ADR 0002: Second Decision\n\n**Status:** Proposed\n\nBody.\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source,
        adrs: Adrs,
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false);

    // ---- Requirement-ID linkification (AC #1) ----

    [Fact]
    public void GenerateAll_LinksKnownRequirementIdOnStoryPage()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(StoryPage);
        Assert.Contains("class=\"req-ref\"", html);
        Assert.Contains("requirements/fr6.html", html);
    }

    [Fact]
    public void GenerateAll_LeavesUnknownRequirementIdAsPlainText()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(StoryPage);
        Assert.Contains("FR99", html);
        Assert.DoesNotContain("requirements/fr99.html", html);
    }

    [Fact]
    public void GenerateAll_SkipsSelfLinkOnRequirementDetailPage()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(Path.Combine(Site, "requirements", "fr6.html"));
        // The FR6 detail page must never link FR6 to itself, but may still link other ids.
        Assert.DoesNotContain("href=\"../requirements/fr6.html\"", html);
    }

    // ---- Source-citation reference-map routing (AC #2) ----

    [Fact]
    public void GenerateAll_RoutesEpicsCitationToGeneratedEpicsHtml()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(StoryPage);
        // epics.md is special-cased to the generated epics.html, never its generic mirrored render.
        Assert.Contains("href=\"../epics.html\"", html);
        Assert.DoesNotContain("planning-artifacts/epics.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesConsumedArtifactCitationToStoryPage()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(StoryPage);
        // The change-log citation to the 1.2 artifact must resolve to its story detail page, proving both
        // consumed-artifact routing and change-log slice processing.
        Assert.Contains("epics/story-1-2.html", html);
    }

    [Fact]
    public void GenerateAll_RoutesPlainDocCitationToMirroredHtml()
    {
        new SiteGenerator(Options()).GenerateAll();

        var html = File.ReadAllText(StoryPage);
        // The acceptance-criteria citation to prd.md must resolve to its mirrored page, proving AC-slice
        // processing and generic mirrored routing.
        Assert.Contains("planning-artifacts/prd.html", html);
    }

    // ---- ADR cross-linking + regeneration coherence (AC #2) ----

    [Fact]
    public void GenerateAll_RewritesAdrCrossLinksAndSurfacesStatus()
    {
        new SiteGenerator(Options()).GenerateAll();

        var adrHtml = File.ReadAllText(Path.Combine(Site, "adrs", "0001-first.html"));
        Assert.Contains("href=\"0002-second.html\"", adrHtml);
        Assert.Contains("href=\"index.html\"", adrHtml);

        var index = File.ReadAllText(HomeIndex);
        Assert.Contains("Accepted", index);
    }

    [Fact]
    public void RegenerateAdrs_RemovesStalePageAndIndexCard_WhenAdrDeleted()
    {
        var gen = new SiteGenerator(Options());
        gen.GenerateAll();
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));

        File.Delete(Path.Combine(Adrs, "0002-second.md"));
        gen.RegenerateAdrs();

        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));
        Assert.DoesNotContain("0002-second.html", File.ReadAllText(HomeIndex));
    }

    [Fact]
    public void RegenerateAdrs_HandlesRename_WithoutLeavingStalePage()
    {
        var gen = new SiteGenerator(Options());
        gen.GenerateAll();
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));

        File.Move(Path.Combine(Adrs, "0002-second.md"), Path.Combine(Adrs, "0002-renamed.md"));
        gen.RegenerateAdrs();

        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0002-second.html")));
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-renamed.html")));
    }

    [Fact]
    public void RegenerateAdrs_ReflectsChangedStatus_OnIndexCard()
    {
        var gen = new SiteGenerator(Options());
        gen.GenerateAll();

        File.WriteAllText(Path.Combine(Adrs, "0002-second.md"),
            "# ADR 0002: Second Decision\n\n**Status:** Superseded by [0001](0001-first.md)\n\nBody.\n");
        gen.RegenerateAdrs();

        // Status extraction flattens the markdown link in the status line to plain text for the card.
        var index = File.ReadAllText(HomeIndex);
        Assert.Contains("Superseded by 0001", index);
    }
}
