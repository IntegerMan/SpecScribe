using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level regression coverage for Story 1.3 markdown fidelity: AC-anchor ↔ AC-reference
/// deep-linking on a real rendered story page, task-checklist completion state, mermaid client-render blocks
/// carrying the init script (on both full doc pages and inside story-artifact bodies), and the negative
/// control that a page with no diagram never gets the init script.</summary>
public class SiteGeneratorFidelityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-fidelity-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string StoryRoute => "epics/story-1-1.html";

    // Init-script fingerprint — the pinned mermaid CDN module import that HtmlTemplater/EpicsTemplater inject.
    private const string InitScriptMarker = "cdn.jsdelivr.net/npm/mermaid@11";

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the fidelity portal.

        ## Epic 1: Foundation

        ### Story 1.1: Markdown Fidelity

        As a reviewer, I want faithful rendering.
        """;

    // Story 1.1 exercises every AC-fidelity seam: two numbered acceptance criteria (→ id="ac-1"/id="ac-2"
    // anchors), tasks that reference them (→ href="#ac-1" ac-ref links) with one done + one pending checkbox,
    // and a mermaid fence in a NON-carved remainder section (Design Notes) so the story page must inject the
    // client-side init script even though the fence lives in an artifact body, not a full page.
    private const string Story11Md = """
        # Story 1.1: Markdown Fidelity

        Status: ready-for-dev

        ## Story

        As a reviewer, I want faithful rendering.

        ## Acceptance Criteria

        1. Diagrams render client-side and checklists show completion states.
        2. AC references deep-link to criteria anchors.

        ## Tasks / Subtasks

        - [x] Task 1: Render mermaid client-side (AC: #1)
        - [ ] Task 2: Deep-link AC references (AC: #2)

        ## Design Notes

        ```mermaid
        graph TD
          A --> B
        ```

        ## Dev Agent Record

        ### Agent Model Used

        test-model
        """;

    public SiteGeneratorFidelityTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-markdown-fidelity.md"), Story11Md);

        // A full-page doc with a mermaid fence (routed through Convert) and a second with only ordinary code.
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "diagram-doc.md"),
            "# Diagram Doc\n\n```mermaid\nflowchart LR\n  X --> Y\n```\n");
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "plain-doc.md"),
            "# Plain Doc\n\n```csharp\nvar x = 1;\n```\n");

        // A multi-heading doc whose rendered order equals source order — its TOC sidebar entries must follow
        // DocModel.Headings order exactly.
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "guide.md"),
            "# Guide\n\n## First Topic\n\ntext\n\n### A Detail\n\ntext\n\n## Second Topic\n\ntext\n\n## Third Topic\n\ntext\n");

        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
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

    private SiteGenerator GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    // ---- AC deep-linking (AC #2) ----

    [Fact]
    public void GenerateAll_StoryPageHasAcAnchorsMatchingItsReferences()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        // Every criterion gets a stable anchor...
        Assert.Contains("id=\"ac-1\"", html);
        Assert.Contains("id=\"ac-2\"", html);
        // ...and the task references deep-link to those exact anchors as .ac-ref links.
        Assert.Contains("class=\"ac-ref\" href=\"#ac-1\"", html);
        Assert.Contains("class=\"ac-ref\" href=\"#ac-2\"", html);
    }

    [Fact]
    public void GenerateAll_EveryAcReferenceAnchorResolvesToAnExistingCriterion()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        // No .ac-ref href may point at an id that isn't actually on the page (no dead deep-links).
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(html, "class=\"ac-ref\" href=\"#(ac-\\d+)\""))
        {
            var id = m.Groups[1].Value;
            Assert.Contains($"id=\"{id}\"", html);
        }
    }

    // ---- Task checklist completion state (AC #1) ----

    [Fact]
    public void GenerateAll_StoryPageRendersTaskCheckboxesWithCompletionState()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        Assert.Contains("type=\"checkbox\"", html);
        // Task 1 is done → a checked checkbox is present on the page.
        Assert.Contains("checked", html);
    }

    // ---- Mermaid fidelity (AC #1) ----

    [Fact]
    public void GenerateAll_StoryPageWithMermaidInBodyCarriesClientRenderBlockAndInitScript()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        // The fragment-rendered fence became a client-render block, which is what makes the renderer emit the
        // init module for this page.
        Assert.Contains(Mermaid.BlockMarker, html);
        Assert.DoesNotContain("<code class=\"language-mermaid\">", html);
        // [Story 23.6 AC #8] The init MODULE is chrome, and no C# code path emits a page's chrome any more. The
        // question splits in two and both halves are still asserted: the site ships the script (here, off the
        // IR's chrome block — the renderer's only source for it), and THIS page gets it because its region
        // carries the block marker above. The per-page half is `web/test/chrome-needs.test.ts`, which pins
        // `chromeNeeds().needsMermaid` against exactly this marker.
        Assert.Contains(InitScriptMarker, SiteRegion.Chrome(Site).MermaidInitScript);
    }

    [Fact]
    public void GenerateAll_FullDocPageWithMermaidCarriesInitScript()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, "planning-artifacts/diagram-doc.html");

        Assert.Contains(Mermaid.BlockMarker, html);
        // [Story 23.6 AC #8] Chrome — see the note on the story-page test above for where each half now lives.
        Assert.Contains(InitScriptMarker, SiteRegion.Chrome(Site).MermaidInitScript);
    }

    [Fact]
    public void GenerateAll_PageWithoutMermaidOmitsInitScript()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, "planning-artifacts/plain-doc.html");

        // Negative control. [Story 23.6 AC #8] The site-level script always exists now, so the honest negative
        // is "this page carries no diagram, therefore the renderer emits no module for it" — the marker the
        // renderer keys on, not the module body.
        Assert.DoesNotContain(Mermaid.BlockMarker, html);
        Assert.Contains("language-csharp", html);
    }

    // ---- TOC sidebar (AC #3) ----

    /// <summary>Pulls the ordered list of anchor ids out of the page's single <c>toc-sidebar</c> nav.</summary>
    private static IReadOnlyList<string> TocAnchorOrder(string html)
    {
        var start = html.IndexOf("<nav class=\"toc-sidebar\"", StringComparison.Ordinal);
        if (start < 0) return Array.Empty<string>();
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        var block = html[start..end];

        return System.Text.RegularExpressions.Regex.Matches(block, "href=\"#([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    [Fact]
    public void GenerateAll_GenericDocRendersSidebarNotStrip_InSourceHeadingOrder()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, "planning-artifacts/guide.html");

        // The shared seam renders the accessible sidebar, and the retired top-strip is gone everywhere.
        Assert.Contains("<nav class=\"toc-sidebar\" aria-label=\"On this page\">", html);
        Assert.Contains("<div class=\"page-shell\">", html);
        Assert.DoesNotContain("toc-strip", html);

        // Full-page render order == DocModel.Headings order.
        var order = TocAnchorOrder(html);
        Assert.Equal(new[] { "first-topic", "a-detail", "second-topic", "third-topic" }, order);
    }

    [Fact]
    public void GenerateAll_StoryDetailPageBuildsSidebarInEmissionOrder()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        Assert.Contains("<nav class=\"toc-sidebar\" aria-label=\"On this page\">", html);
        var order = TocAnchorOrder(html);

        // Detail pages emit sections out of source order; the TOC must follow the templater's emission order:
        // User Story → Task Breakdown → Acceptance Criteria → Dev Agent Record → remainder headings.
        int UserStory() => order.ToList().IndexOf("sec-user-story");
        int TaskBreakdown() => order.ToList().IndexOf("sec-task-breakdown");
        int AcceptanceCriteria() => order.ToList().IndexOf("sec-acceptance-criteria");
        int DevAgentRecord() => order.ToList().IndexOf("sec-dev-agent-record");

        Assert.True(UserStory() >= 0 && TaskBreakdown() >= 0 && AcceptanceCriteria() >= 0 && DevAgentRecord() >= 0);
        Assert.True(UserStory() < TaskBreakdown());
        Assert.True(TaskBreakdown() < AcceptanceCriteria());
        Assert.True(AcceptanceCriteria() < DevAgentRecord());
        // The remainder "Design Notes" heading slots in after the panels, in rendered order.
        Assert.True(DevAgentRecord() < order.ToList().IndexOf("design-notes"));
    }

    [Fact]
    public void GenerateAll_EveryStoryTocEntryPointsAtAnIdThatExistsOnThePage()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, StoryRoute);

        // No dead links: every sidebar entry's target id must actually be present on the page.
        foreach (var anchor in TocAnchorOrder(html))
        {
            Assert.Contains($"id=\"{anchor}\"", html);
        }
    }

    [Fact]
    public void GenerateAll_EpicDetailPageSidebarJumpsToEachStoryCard()
    {
        GenerateSite();
        var html = SiteRegion.Read(Site, "epics/epic-1.html");

        Assert.Contains("<nav class=\"toc-sidebar\" aria-label=\"On this page\">", html);
        // The epic page's story jump-list must point at real story-card ids (no dead links).
        foreach (var anchor in TocAnchorOrder(html))
        {
            Assert.Contains($"id=\"{anchor}\"", html);
        }
        Assert.Contains("id=\"story-1-1\"", html);
    }
}
