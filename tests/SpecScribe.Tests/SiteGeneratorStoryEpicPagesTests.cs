using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for the story/epic mention links and undrafted-story placeholder
/// pages: every story defined in epics.md gets a page at epics/story-N-M.html (placeholder when no
/// implementation artifact exists), inline "Story N.M"/"Epic N" mentions link to those pages, pages never
/// self-link, protected regions (Mermaid sources, SVG charts) are never rewritten, and a later-drafted
/// artifact overwrites its placeholder in place. Also covers the Gherkin keyword styling on both AC
/// surfaces (story-page criterion panels and epic-card AC blocks).</summary>
public class SiteGeneratorStoryEpicPagesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-storypages-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string DraftedStoryPage => Path.Combine(Site, "epics", "story-1-1.html");
    private string PlaceholderPage => Path.Combine(Site, "epics", "story-1-2.html");
    private string EpicPage => Path.Combine(Site, "epics", "epic-1.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Drafted Story

        As a contributor,
        I want a drafted story,
        So that pages render.

        **Acceptance Criteria:**

        1.
        **Given** a plan
        **When** it renders
        **Then** links appear

        ### Story 1.2: Future Story

        As a contributor,
        I want a future story,
        So that placeholders exist.

        **Acceptance Criteria:**

        1.
        **Given** an undrafted story
        **When** the site generates
        **Then** a placeholder page exists
        """;

    private const string Story11Md = """
        # Story 1.1: Drafted Story

        Status: ready-for-dev

        ## Story

        As a contributor, I want a drafted story. Sequence this after Story 1.2 (part of Epic 1).

        ## Acceptance Criteria

        1. **Given** deferred notes exist **When** the site is generated **Then** they render **And** Story 1.2 stays reachable.

           **Origin & scope:** trailing note paragraph mentioning Epic 1.

        ## Tasks / Subtasks

        - [ ] Task 1: Mention Story 9.9, which is not planned anywhere.
        """;

    private const string Story12Md = """
        # Story 1.2: Future Story

        Status: ready-for-dev

        ## Story

        As a contributor, I want the placeholder replaced.

        ## Tasks / Subtasks

        - [ ] Task 1: Stub
        """;

    public SiteGeneratorStoryEpicPagesTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-drafted-story.md"), Story11Md);
        // A plain doc whose title/body mention an epic — its <head> title/meta must not be corrupted by the
        // linkifier, while the same mention in the body should still link.
        File.WriteAllText(Path.Combine(Source, "epic-1-retrospective.md"), "# Epic 1 Retrospective\n\nReviewing Epic 1 and Story 1.1.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source,
        adrs: Path.Combine(_root, "docs", "adrs"),
        output: Site,
        projectName: "SpecScribe",
        includeReadme: false);

    private SiteGenerator GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    // ---- Placeholder pages for undrafted stories ----

    [Fact]
    public void GenerateAll_EmitsPlaceholderPageForUndraftedStory()
    {
        GenerateSite();

        Assert.True(File.Exists(PlaceholderPage));
        var html = File.ReadAllText(PlaceholderPage);
        Assert.Contains("Not yet drafted", html);
        Assert.Contains("I want a future story", html);
        // The dead-end reads as a next action (command form or plain fallback, module-dependent).
        Assert.Contains("hasn't been drafted in detail yet", html);
    }

    [Fact]
    public void GenerateAll_PlaceholderCarriesGherkinStyledAcBlocks()
    {
        GenerateSite();

        var html = File.ReadAllText(PlaceholderPage);
        Assert.Contains("class=\"gherkin-kw kw-given\"", html);
        Assert.Contains("class=\"gherkin-kw kw-then\"", html);
    }

    [Fact]
    public void GenerateAll_PlaceholderDoesNotChangeDetailedStoryAccounting()
    {
        GenerateSite();

        // The epic page must still treat 1.2 as undetailed: guidance note present, no full-plan link.
        // Lone undrafted story → no consolidation banner; per-card note kept (fixture has no create-story
        // catalog → plain fallback; command path covered by EpicsViewBuilder unit tests). [Story 8.6]
        var html = File.ReadAllText(EpicPage);
        Assert.Contains("class=\"not-detailed-note\"", html);
        Assert.DoesNotContain("epic-undrafted-banner", html);
        Assert.Contains("No detailed story plan yet.", html);
    }

    [Fact]
    public void GenerateAll_MultipleUndraftedStories_ConsolidatesHintsIntoOneBanner()
    {
        // 2+ undrafted → one banner + plain card notes (count-only without a module catalog). [Story 8.6]
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), """
            # Epics

            ## Epic List

            ### Epic 1: Foundation

            Stand up the portal.

            ## Epic 1: Foundation

            ### Story 1.1: Drafted Story

            As a contributor,
            I want a drafted story,
            So that pages render.

            **Acceptance Criteria:**

            1.
            **Given** a plan
            **When** it renders
            **Then** links appear

            ### Story 1.2: Future Story

            As a contributor,
            I want a future story,
            So that placeholders exist.

            **Acceptance Criteria:**

            1.
            **Given** an undrafted story
            **When** the site generates
            **Then** a placeholder page exists

            ### Story 1.3: Another Future

            As a contributor,
            I want another future story,
            So that consolidation applies.

            **Acceptance Criteria:**

            1.
            **Given** two undrafted stories
            **When** the epic page renders
            **Then** hints consolidate
            """);

        GenerateSite();

        var html = File.ReadAllText(EpicPage);
        Assert.Equal(1, CountOccurrences(html, "class=\"epic-undrafted-banner\""));
        Assert.Contains("2 stories in this epic need task plans.", html);
        Assert.Contains("class=\"not-detailed-note\">No detailed story plan yet.</div>", html);
        Assert.DoesNotContain("No detailed story plan yet — draft it with", html);
        Assert.Equal(2, CountOccurrences(html, "class=\"not-detailed-note\""));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    [Fact]
    public void RegenerateEpics_OverwritesPlaceholderOnceArtifactAppears()
    {
        var gen = GenerateSite();
        Assert.Contains("Not yet drafted", File.ReadAllText(PlaceholderPage));

        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-2-future-story.md"), Story12Md);
        var ev = gen.RegenerateEpics();

        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);
        var html = File.ReadAllText(PlaceholderPage);
        Assert.DoesNotContain("Not yet drafted", html);
        Assert.Contains("I want the placeholder replaced", html);
    }

    [Fact]
    public void RegenerateEpics_KeepsRetroGatedEpicDone_DoesNotFlipToReviewInWatchMode()
    {
        // Watch-mode regression: an all-stories-done epic WITH a retrospective must keep rendering "Done" after an
        // incremental RegenerateEpics. The retro flag is re-stamped from the preserved retro map, not reset to
        // false — which (before the fix) reset HasRetrospective to false on the rebuilt model and wrongly
        // downgraded the epic to the retro-gated "In review" on the sunburst/donut/badge. [spec-sunburst-retro]
        var impl = Path.Combine(Source, "implementation-artifacts");
        const string done = "Status: done\n\n## Tasks / Subtasks\n\n- [x] Task 1\n";
        File.WriteAllText(Path.Combine(impl, "1-1-drafted-story.md"), "# Story 1.1\n" + done);
        File.WriteAllText(Path.Combine(impl, "1-2-future-story.md"), "# Story 1.2\n" + done);
        File.WriteAllText(Path.Combine(impl, "epic-1-retro-2026-07-10.md"),
            "# Epic 1 Retrospective\n\n**Date:** 2026-07-10\n**Participants:** Team\n\nWent well.\n");

        var gen = GenerateSite();
        // Sanity: all done + retro present ⇒ the epic header badge is "Done", never the retro-gated "In review".
        Assert.Contains("status-badge done", File.ReadAllText(EpicPage));
        Assert.DoesNotContain("status-badge review", File.ReadAllText(EpicPage));

        // A watch edit to a story file triggers an incremental epics regen (retros are NOT re-parsed on this path).
        File.WriteAllText(Path.Combine(impl, "1-1-drafted-story.md"), "# Story 1.1\n" + done + "- [x] Task 2\n");
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateEpics().Outcome);

        // Still "Done" — before the fix the epic flipped to "In review" on the incremental rebuild.
        var epicHtml = File.ReadAllText(EpicPage);
        Assert.Contains("status-badge done", epicHtml);
        Assert.DoesNotContain("status-badge review", epicHtml);
    }

    // ---- Inline Story/Epic mention links ----

    [Fact]
    public void GenerateAll_LinksStoryAndEpicMentionsOnStoryPage()
    {
        GenerateSite();

        var html = File.ReadAllText(DraftedStoryPage);
        Assert.Contains("<a class=\"story-ref\" href=\"../epics/story-1-2.html\">Story 1.2</a>", html);
        Assert.Contains("<a class=\"epic-ref\" href=\"../epics/epic-1.html\">Epic 1</a>", html);
    }

    [Fact]
    public void GenerateAll_LeavesUnknownStoryMentionsPlain()
    {
        GenerateSite();

        var html = File.ReadAllText(DraftedStoryPage);
        Assert.Contains("Story 9.9", html);
        Assert.DoesNotContain("story-9-9.html", html);
    }

    [Fact]
    public void GenerateAll_NeverSelfLinksAStoryOrEpicPage()
    {
        GenerateSite();

        // The story page's own kicker says "Story 1.1"; the epic page's kicker says "Epic 1".
        Assert.DoesNotContain("class=\"story-ref\" href=\"../epics/story-1-1.html\"", File.ReadAllText(DraftedStoryPage));
        Assert.DoesNotContain("class=\"epic-ref\" href=\"../epics/epic-1.html\"", File.ReadAllText(EpicPage));
    }

    [Fact]
    public void GenerateAll_NeverRewritesMermaidSourcesOrSvgCharts()
    {
        GenerateSite();

        // The epics index carries the roadmap Mermaid source (node text "Epic 1") and the sunburst SVG
        // (<title>Epic 1: …</title>) — injecting anchors into either corrupts the rendered artifact.
        var html = File.ReadAllText(Path.Combine(Site, "epics.html"));
        var mermaidStart = html.IndexOf("<pre class=\"mermaid\"", StringComparison.Ordinal);
        Assert.True(mermaidStart >= 0);
        var mermaidEnd = html.IndexOf("</pre>", mermaidStart, StringComparison.Ordinal);
        var mermaid = html[mermaidStart..mermaidEnd];
        Assert.DoesNotContain("epic-ref", mermaid);
        Assert.DoesNotContain("<title><a", html);
    }

    [Fact]
    public void GenerateAll_NeverCorruptsTheHeadOfADocTitledWithAnEpic()
    {
        GenerateSite();

        var html = File.ReadAllText(Path.Combine(Site, "epic-1-retrospective.html"));
        var headEnd = html.IndexOf("</head>", StringComparison.Ordinal);
        Assert.True(headEnd > 0);
        var head = html[..headEnd];

        // The <title> and <meta content="…"> mention "Epic 1"/"Story 1.1" — no anchor may be injected there
        // (its quotes would terminate the content attribute and break the markup).
        Assert.DoesNotContain("epic-ref", head);
        Assert.DoesNotContain("story-ref", head);
        Assert.Contains("<title>Epic 1 Retrospective", head);

        // Positive control: the same mention in the body IS linked, so the head-skip isn't masking a linkifier
        // that simply never ran on this page.
        var body = html[headEnd..];
        Assert.Contains("class=\"epic-ref\"", body);
    }

    [Fact]
    public void RegenerateEpics_PrunesPlaceholderWhenStoryLeavesThePlan()
    {
        var gen = GenerateSite();
        Assert.True(File.Exists(PlaceholderPage));

        // Drop Story 1.2 from epics.md, then regenerate as watch mode would.
        var trimmed = EpicsMd[..EpicsMd.IndexOf("### Story 1.2:", StringComparison.Ordinal)].TrimEnd() + "\n";
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), trimmed);
        var ev = gen.RegenerateEpics();

        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);
        Assert.False(File.Exists(PlaceholderPage));
        Assert.True(File.Exists(DraftedStoryPage));
    }

    // ---- Gherkin styling on the story page's AC panel ----

    [Fact]
    public void GenerateAll_StylesGherkinKeywordsInStoryAcPanel()
    {
        GenerateSite();

        var html = File.ReadAllText(DraftedStoryPage);
        Assert.Contains("class=\"gherkin-line\"", html);
        Assert.Contains("class=\"gherkin-kw kw-when\"", html);
        Assert.Contains("class=\"gherkin-kw kw-and\"", html);
        Assert.DoesNotContain("<strong>Given</strong>", html);
    }

    [Fact]
    public void GenerateAll_MultiParagraphCriterionKeepsPerLineChipsAndSeparateNote()
    {
        GenerateSite();

        // The fixture criterion carries a trailing "Origin & scope" paragraph, so its body keeps <p>
        // wrappers. Chips must still land per-line inside the clause paragraph, and the note must render
        // as its own untouched paragraph — the exact case that used to degrade to inline chips.
        var html = File.ReadAllText(DraftedStoryPage);
        Assert.Contains("<p><span class=\"gherkin-line\">", html);
        Assert.Contains("<p><strong>Origin &amp; scope:</strong>", html);
    }

    // ---- Epic-card AC layout and the single task indicator ----

    [Fact]
    public void GenerateAll_EpicCardAcBlocksCarryLabelsAndNoEmptyLists()
    {
        GenerateSite();

        var html = File.ReadAllText(EpicPage);
        Assert.Contains("<span class=\"ac-num\">AC #1</span>", html);
        // The bare "1." lines authored in epics.md used to render as stray empty <ol> fragments.
        Assert.DoesNotContain("<ol>\n<li></li>\n</ol>", html);
        Assert.Contains("class=\"ac-block-body\"", html);
    }

    [Fact]
    public void GenerateAll_StoryCardHasExactlyOneTaskIndicator()
    {
        GenerateSite();

        var html = File.ReadAllText(EpicPage);
        // The bottom per-story progress bar is gone; the header badge is the single indicator.
        Assert.DoesNotContain("per-story-progress", html);
        // Fixture story 1.1 has 0/1 tasks done → muted count badge, no donut inside it.
        Assert.Contains("task-badge none-done\">0/1 tasks", html);
    }
}
