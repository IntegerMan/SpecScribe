using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 2.3: with a sprint-status.yaml present, a sprint.html page is
/// produced and the home index carries the Sprint widget + Sprint nav; with no yaml, none of those appear and
/// no sprint.html is written. Both paths are asserted to be free of broken local links. Follows the temp-dir
/// fixture style of <see cref="SiteGeneratorFidelityTests"/>.</summary>
public class SiteGeneratorSprintTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-sprint-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string SprintYaml => Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");
    private string SprintPage => Path.Combine(Site, "sprint.html");
    private string IndexPage => Path.Combine(Site, "index.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    // Tracks epic-1 and its story (in-progress) plus a retrospective entry — exercises epic/story/retro
    // classification and the in-progress→active badge on a real generated page.
    private const string SprintYamlContent = """
        last_updated: 2026-07-06T22:00:00-04:00
        development_status:
          epic-1: in-progress
          1-1-foundation: in-progress
          1-2-undrafted: backlog
          epic-1-retrospective: optional
        """;

    public SiteGeneratorSprintTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
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

    private void GenerateSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
    }

    [Fact]
    public void GenerateAll_WithSprintYaml_ProducesSprintPageWidgetAndNav()
    {
        File.WriteAllText(SprintYaml, SprintYamlContent);
        GenerateSite();

        Assert.True(File.Exists(SprintPage), "sprint.html should be generated when the tracking file exists");
        var sprintHtml = File.ReadAllText(SprintPage);
        Assert.Contains("Sprint Status", sprintHtml);
        Assert.Contains("class=\"sprint-board\"", sprintHtml);       // Kanban board
        Assert.Contains("sprint-lane active", sprintHtml);           // in-progress lane
        Assert.Contains("status-badge active", sprintHtml);          // epic badge (in-progress → active)
        Assert.Contains("href=\"epics/epic-1.html\"", sprintHtml);   // epic links resolve (epic view)
        Assert.Contains("href=\"epics/story-1-1.html\"", sprintHtml);// drafted story card links
        // The undrafted story 1.2 still links — to its generated placeholder page (never dead text).
        Assert.Contains("href=\"epics/story-1-2.html\"", sprintHtml);
        Assert.True(File.Exists(Path.Combine(Site, "epics", "story-1-2.html")), "undrafted story gets a placeholder page");

        var index = File.ReadAllText(IndexPage);
        // AC #2: with sprint data, the home Now & Next panel becomes the sprint board (tracked view), labeled
        // with its source and linking to the full sprint page. [Story 2.3]
        Assert.Contains("chart-panel sprint-board-panel", index);
        Assert.Contains("from sprint-status.yaml", index);
        Assert.Contains("href=\"sprint.html\"", index);              // CTA + nav item link to the board

        AssertNoBrokenLocalLinks(SprintPage);
        AssertNoBrokenLocalLinks(IndexPage);
        // The epic page also links the undrafted story to its placeholder (the "epic links you there too").
        AssertNoBrokenLocalLinks(Path.Combine(Site, "epics", "epic-1.html"));
        Assert.Contains("href=\"../epics/story-1-2.html\"", File.ReadAllText(Path.Combine(Site, "epics", "epic-1.html")));
    }

    [Fact]
    public void GenerateAll_WithoutSprintYaml_OmitsPageWidgetAndNav()
    {
        // No sprint-status.yaml written.
        GenerateSite();

        Assert.False(File.Exists(SprintPage), "no sprint.html without a tracking file");
        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("from sprint-status.yaml", index);
        Assert.DoesNotContain("href=\"sprint.html\"", index);

        AssertNoBrokenLocalLinks(IndexPage);
    }

    /// <summary>Every local (non-anchor, non-http) href on the page resolves to a file that was actually
    /// generated — the "never a broken link" guarantee (AC#1, NFR2).</summary>
    private void AssertNoBrokenLocalLinks(string pagePath)
    {
        var html = File.ReadAllText(pagePath);
        var pageDir = Path.GetDirectoryName(pagePath)!;
        foreach (Match m in Regex.Matches(html, "href=\"([^\"]+)\""))
        {
            var raw = m.Groups[1].Value;
            // Only local page links are checkable: skip anchors and anything with a URI scheme
            // (http:, data: favicon, mailto:, cursor:// deep links, etc.).
            if (raw.StartsWith("#", StringComparison.Ordinal)
                || Regex.IsMatch(raw, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                continue;

            var target = raw.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            var resolved = Path.GetFullPath(Path.Combine(pageDir, target.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(resolved), $"broken link: {raw} → {resolved}");
        }
    }
}
