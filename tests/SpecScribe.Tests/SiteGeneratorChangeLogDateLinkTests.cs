using System.Diagnostics;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for a story page's Change Log date links: an entry's leading date links to
/// its <c>commits/{date}.html</c> page exactly when <c>Charts.LinkedCommitDays</c> — the SAME function
/// <c>GenerateDatePagesInternal</c> uses to decide which days get pages — includes that date. Covers the
/// review-loop-2 fix directly: a future-dated commit (clock skew / backdated-forward) must NOT link, since
/// <c>LinkedCommitDays</c> excludes any day after "today". [date links]</summary>
public class SiteGeneratorChangeLogDateLinkTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-changelog-datelink-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string StoryPage => Path.Combine(Site, "epics", "story-1-1.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Drafted Story

        As a contributor, I want a drafted story, so that pages render.

        **Acceptance Criteria:**

        1.
        **Given** a plan
        **When** it renders
        **Then** links appear
        """;

    private static string Story11Md(string changeLogDate) => $$"""
        # Story 1.1: Drafted Story

        Status: review

        ## Story

        As a contributor, I want a drafted story.

        ## Acceptance Criteria

        1. **Given** a plan **When** it renders **Then** links appear

        ## Tasks / Subtasks

        - [x] Task 1: Ship it

        ## Change Log

        - {{changeLogDate}}: Verified the story.
        """;

    public SiteGeneratorChangeLogDateLinkTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void WriteStory(string changeLogDate) =>
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-drafted-story.md"), Story11Md(changeLogDate));

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, output: Site, projectName: "SpecScribe", includeReadme: false);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GitCommitOnTheChangeLogDate_DateLinksToDayPage()
    {
        WriteStory("2026-07-16");
        Assert.True(CommitOn("2026-07-16"), "git CLI unavailable on this host — cannot exercise the day-page link; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        Assert.True(File.Exists(Path.Combine(Site, "commits", "2026-07-16.html")), "expected a day page for the commit date");
        var html = File.ReadAllText(StoryPage);
        Assert.Contains("href=\"../commits/2026-07-16.html\"", html);
        Assert.Contains(">Jul 16, 2026</a>", html);
    }

    [Fact]
    public void NoGitAtAll_ChangeLogDateStaysPlainText()
    {
        WriteStory("2026-07-16");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        var html = File.ReadAllText(StoryPage);
        Assert.Contains("Jul 16, 2026", html);
        Assert.DoesNotContain("commits/2026-07-16.html", html);
    }

    [Fact]
    public void GitHistoryOnUnrelatedDay_ChangeLogDateStaysPlainText()
    {
        WriteStory("2026-07-16");
        Assert.True(CommitOn("2026-01-01"), "git CLI unavailable on this host — cannot exercise this case; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        Assert.False(File.Exists(Path.Combine(Site, "commits", "2026-07-16.html")));
        var html = File.ReadAllText(StoryPage);
        Assert.Contains("Jul 16, 2026", html);
        Assert.DoesNotContain("commits/2026-07-16.html", html);
    }

    [Fact]
    public void FutureDatedCommit_MatchingChangeLogDate_StaysPlainText()
    {
        // Review-loop-2 fix: LinkedCommitDays excludes any day after "today", so a clock-skewed or manually
        // backdated-forward commit must never produce a link even though CommitsByDay technically contains it.
        var future = DateOnly.FromDateTime(DateTime.Now).AddYears(1).ToString("yyyy-MM-dd");
        WriteStory(future);
        Assert.True(CommitOn(future), "git CLI unavailable on this host — cannot exercise this case; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        Assert.False(File.Exists(Path.Combine(Site, "commits", $"{future}.html")), "a future-dated commit must not get a day page");
        var html = File.ReadAllText(StoryPage);
        Assert.DoesNotContain($"commits/{future}.html", html);
    }

    /// <summary>Initializes a git repo and commits the whole fixture tree on the given day (author AND committer
    /// date pinned so day-grouping is deterministic). Returns false (test no-ops) when the git CLI is unavailable.</summary>
    private bool CommitOn(string day)
    {
        if (!RunGit("init")) return false;
        if (!RunGit("add .")) return false;
        var stamp = $"{day}T12:00:00";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c user.name=\"DateLink Tester\" -c user.email=datelink@example.com -c commit.gpgsign=false commit --date=\"{stamp}\" -m \"commit on {day}\"",
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.Environment["GIT_COMMITTER_DATE"] = stamp;
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
