using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 10.1's journey-organized nav: Insights / Follow-ups groups
/// are data-gated (NFR8), Structure stays retired, and local links from Home + an interior page resolve.
/// Follows the temp-dir fixture style of <see cref="SiteGeneratorSprintTests"/> /
/// <see cref="SiteGeneratorGitInsightsTests"/>.</summary>
public class SiteGeneratorGroupedNavTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-grouped-nav-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string IndexPage => Path.Combine(Site, "index.html");
    private string EpicsPage => Path.Combine(Site, "epics.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: done

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string SprintWithOpenAction = """
        last_updated: 2026-07-16T12:00:00-04:00
        development_status:
          epic-1: done
          1-1-foundation: done
          epic-1-retrospective: done
        action_items:
          - epic: 1
            action: "Follow up on the foundation handoff"
            owner: Dana
            status: open
        """;

    private const string DeferredMd = """
        # Deferred Work

        ## Deferred from: review of foundation (2026-07-16)

        - [ ] Polish the nav disclosure styling later
        """;

    public SiteGeneratorGroupedNavTests()
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

    private ForgeOptions Options(bool deepGit = false) => ForgeOptions.Resolve(
        source: Source, output: Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0,
            "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_WithoutDeepGit_OmitsInsightsGroupAndStructure()
    {
        var events = new SiteGenerator(Options(deepGit: false)).GenerateAll();
        AssertNoErrors(events);

        var index = File.ReadAllText(IndexPage);
        var epics = File.ReadAllText(EpicsPage);

        Assert.DoesNotContain("site-nav-group-summary\">Insights", index);
        Assert.DoesNotContain("git-insights.html", index);
        Assert.DoesNotContain("deep-analytics.html", index);
        Assert.DoesNotContain(">Structure<", index);
        Assert.False(File.Exists(Path.Combine(Site, "structure.html")));

        Assert.Contains("Delivery", index);
        Assert.Contains("site-nav-group", index);
        Assert.Contains("<summary class=\"site-nav-group-summary\"", index);

        AssertNoBrokenLocalLinks(IndexPage);
        AssertNoBrokenLocalLinks(EpicsPage);
        Assert.DoesNotContain("site-nav-group-summary\">Insights", epics);
    }

    [Fact]
    public void GenerateAll_WithFollowUps_SurfacesGroupOnHomeAndInteriorPage()
    {
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), SprintWithOpenAction);
        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), DeferredMd);

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        Assert.True(File.Exists(Path.Combine(Site, "action-items.html")));
        Assert.True(File.Exists(Path.Combine(Site, "implementation-artifacts", "deferred-work.html")));

        foreach (var page in new[] { IndexPage, EpicsPage })
        {
            var html = File.ReadAllText(page);
            Assert.Contains("Follow-ups", html);
            Assert.Contains("href=\"action-items.html\"", html);
            Assert.Contains("href=\"implementation-artifacts/deferred-work.html\"", html);
            AssertNoBrokenLocalLinks(page);
        }
    }

    [Fact]
    public void GenerateAll_WithoutFollowUpData_OmitsFollowUpsGroup()
    {
        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("Follow-ups", index);
        Assert.DoesNotContain("action-items.html", index);
        Assert.False(File.Exists(Path.Combine(Site, "action-items.html")));
    }

    [Fact]
    public void GenerateAll_WithDeepGitHistory_SurfacesInsightsGroupFromInteriorPage()
    {
        Assert.True(TryCreateGitHistory(),
            "git CLI unavailable on this host — cannot exercise Insights nav generation; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        Assert.True(File.Exists(Path.Combine(Site, "git-insights.html")));
        Assert.True(File.Exists(Path.Combine(Site, "deep-analytics.html")));

        var epics = File.ReadAllText(EpicsPage);
        Assert.Contains("Insights", epics);
        Assert.Contains("href=\"git-insights.html\"", epics);
        Assert.Contains("href=\"deep-analytics.html\"", epics);
        AssertNoBrokenLocalLinks(EpicsPage);
        AssertNoBrokenLocalLinks(IndexPage);
    }

    private void AssertNoBrokenLocalLinks(string pagePath)
    {
        var html = File.ReadAllText(pagePath);
        var pageDir = Path.GetDirectoryName(pagePath)!;
        foreach (Match m in Regex.Matches(html, "href=\"([^\"]+)\""))
        {
            var raw = m.Groups[1].Value;
            if (raw.StartsWith("#", StringComparison.Ordinal)
                || Regex.IsMatch(raw, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
                continue;

            var target = raw.Split('#')[0].Split('?')[0];
            if (target.Length == 0) continue;

            var resolved = Path.GetFullPath(Path.Combine(pageDir, target.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(resolved), $"broken link: {raw} → {resolved}");
        }
    }

    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\n");
        if (!RunGit("add .")) return false;
        if (!Commit("First commit")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\ntwo\n");
        return RunGit("add .") && Commit("Second commit");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"Nav Tester\" -c user.email=nav@example.com -c commit.gpgsign=false commit -m \"{message}\"");

    private bool RunGit(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
