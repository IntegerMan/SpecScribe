using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.3's activity timeline + date pages. Exercises: the timeline
/// page + dashboard link appearing when there is data; the union day set (an artifact-only day still gets a date
/// page + timeline row, a dead day gets neither); graceful degradation (no epics/artifacts + no git → nothing,
/// no error); determinism; and the commit-day + heatmap-link regression. Follows the temp-dir git-fixture style
/// of <see cref="SiteGeneratorCommitDetailsTests"/>.</summary>
public class SiteGeneratorTimelineTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-timeline-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string Timeline => Path.Combine(Site, "timeline.html");
    private string Index => Path.Combine(Site, "index.html");
    private string CommitsDayDir => Path.Combine(Site, "commits");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorTimelineTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(string? source = null, string? output = null) => ForgeOptions.Resolve(
        source: source ?? Source, output: output ?? Site, projectName: "SpecScribe", includeReadme: false);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_WithArtifactsButNoGit_EmitsArtifactDrivenTimelineAndDatePage()
    {
        // The fixture is not a git repository, so the pulse is null. AC #2: the artifact-mtime signal still
        // drives an activity timeline + date pages, and the dashboard links to it — no error.
        var events = new SiteGenerator(Options()).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(Timeline), "timeline.html should be generated from the artifact-mtime signal alone");

        var timeline = File.ReadAllText(Timeline);
        Assert.Contains("<h1>Activity Timeline</h1>", timeline);
        Assert.Contains("class=\"timeline-date\" href=\"commits/", timeline);
        Assert.DoesNotContain("class=\"heatmap\"", timeline); // no git → no heatmap

        // The dashboard's Git Pulse panel links to the timeline.
        Assert.Contains("View activity timeline", File.ReadAllText(Index));

        // An artifact-only day (today) gets a real date page — neutral "Activity on …" heading, no commit list.
        Assert.True(Directory.Exists(CommitsDayDir));
        var today = Charts.D(DateOnly.FromDateTime(DateTime.Now));
        var todayPage = Path.Combine(CommitsDayDir, $"{today}.html");
        Assert.True(File.Exists(todayPage), "an artifact-edited day should get a date page even with no commit");
        var page = File.ReadAllText(todayPage);
        Assert.Contains("Activity on", page);
        Assert.Contains("artifacts-updated", page);
        Assert.DoesNotContain("commit-day-list", page);
    }

    [Fact]
    public void GenerateAll_NoArtifactsNoGit_EmitsNoTimelineNoError()
    {
        // A source tree with no epics.md (so nothing populates the reference map) and no git → no artifact days,
        // no commit days: no timeline, no commits/ dir, no dashboard link, no error, rest of site still builds.
        var emptySource = Path.Combine(_root, "empty-src");
        Directory.CreateDirectory(Path.Combine(emptySource, "planning-artifacts"));
        File.WriteAllText(Path.Combine(emptySource, "planning-artifacts", "brief.md"), "# Brief\n\nJust a brief.\n");
        var emptySite = Path.Combine(_root, "empty-site");

        var events = new SiteGenerator(Options(source: emptySource, output: emptySite)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(File.Exists(Path.Combine(emptySite, "timeline.html")));
        Assert.False(Directory.Exists(Path.Combine(emptySite, "commits")));
        Assert.DoesNotContain("View activity timeline", File.ReadAllText(Path.Combine(emptySite, "index.html")));
        Assert.True(File.Exists(Path.Combine(emptySite, "index.html")), "baseline generation must still succeed");
    }

    [Fact]
    public void GenerateAll_WithGitHistory_TimelineHasHeatmapAndDateRows_DashboardLinks()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the git-driven timeline; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        Assert.True(File.Exists(Timeline));
        var timeline = File.ReadAllText(Timeline);
        Assert.Contains("class=\"heatmap\"", timeline);          // reused activity-over-time visual
        Assert.Contains("class=\"timeline-date\" href=\"commits/", timeline);

        // Every timeline date row links to a date page that actually exists (no dead links).
        foreach (Match m in Regex.Matches(timeline, "href=\"(commits/[0-9-]+\\.html)\""))
        {
            Assert.True(File.Exists(Path.Combine(Site, m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar))),
                $"timeline links a missing date page: {m.Groups[1].Value}");
        }

        Assert.Contains("View activity timeline", File.ReadAllText(Index));
    }

    [Fact]
    public void GenerateAll_HeatmapStillLinksToDatePages_Regression()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the heatmap regression; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        // The dashboard heatmap still links each active cell to its commits/{date}.html date page.
        Assert.Contains("href=\"commits/", File.ReadAllText(Index));

        // A commit-bearing day page keeps its commit rows + subject linkification (unchanged shape).
        var dayPages = Directory.GetFiles(CommitsDayDir, "*.html");
        Assert.NotEmpty(dayPages);
        Assert.Contains(dayPages, p => File.ReadAllText(p).Contains("commit-day-list"));
    }

    [Fact]
    public void GenerateAll_TwoRunsProduceIdenticalTimelineAndDatePages()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise determinism; install git rather than silently skipping this test");

        var site2 = Path.Combine(_root, "site2");
        var events1 = new SiteGenerator(Options()).GenerateAll();
        var events2 = new SiteGenerator(Options(output: site2)).GenerateAll();
        AssertNoErrors(events1);
        AssertNoErrors(events2);

        static string Stable(string html) =>
            Regex.Replace(html, @"on \w+ \d{1,2}, \d{4} at \d{1,2}:\d{2} [AP]M", "on <t>");

        Assert.Equal(Stable(File.ReadAllText(Timeline)), Stable(File.ReadAllText(Path.Combine(site2, "timeline.html"))));

        var pages1 = Directory.GetFiles(CommitsDayDir, "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        var pages2 = Directory.GetFiles(Path.Combine(site2, "commits"), "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(pages1.Select(Path.GetFileName), pages2.Select(Path.GetFileName));
        for (var i = 0; i < pages1.Count; i++)
        {
            Assert.Equal(Stable(File.ReadAllText(pages1[i])), Stable(File.ReadAllText(pages2[i])));
        }
    }

    /// <summary>Initializes a real git repo in the fixture root with two commits by a known author. Returns false
    /// (tests no-op) when the git CLI is unavailable or refuses; identity and signing are forced via -c overrides
    /// so a host's global config can't break the fixture.</summary>
    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\n");
        if (!RunGit("add .")) return false;
        if (!Commit("Implement Story 1.1 foundation")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\ntwo\n");
        return RunGit("add .") && Commit("Second commit");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"Timeline Tester\" -c user.email=timeline@example.com -c commit.gpgsign=false commit -m \"{message}\"");

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
