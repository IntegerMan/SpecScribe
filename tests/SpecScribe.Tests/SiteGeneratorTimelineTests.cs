using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.3's activity timeline + date pages, updated for the git-derived
/// artifact-day bug fix. Exercises: the timeline page + dashboard link appearing when git history exists; artifacts
/// attributed to the day a commit actually changed them (--deep-git) rather than the checkout-day mtime collapse;
/// honest degradation (artifacts on disk but no git → NO timeline, since git can't verify the change dates; no
/// epics/artifacts + no git → nothing, no error); determinism; and the commit-day + heatmap-link regression.
/// Follows the temp-dir git-fixture style of <see cref="SiteGeneratorCommitDetailsTests"/>.</summary>
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

    private ForgeOptions Options(string? source = null, string? output = null, bool deepGit = false) => ForgeOptions.Resolve(
        source: source ?? Source, output: output ?? Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_WithArtifactsButNoGit_EmitsNoTimeline()
    {
        // The fixture has recognized artifacts on disk but is NOT a git repository, so git can't tell us when any
        // of them actually changed. Story 7.3 bug fix: we DROP the artifact-updated claim rather than fabricate it
        // from filesystem mtime (which collapsed every file onto the checkout day). No timeline, no date pages, no
        // dashboard link — the honest degradation — and the rest of the site still generates with no error.
        var events = new SiteGenerator(Options()).GenerateAll();

        AssertNoErrors(events);
        Assert.False(File.Exists(Timeline), "no git → no verifiable activity dates → no timeline (no mtime fallback)");
        Assert.False(Directory.Exists(CommitsDayDir), "no git → no date pages");
        Assert.DoesNotContain("View activity timeline", File.ReadAllText(Index));
        Assert.True(File.Exists(Path.Combine(Site, "epics.html")), "baseline generation must still succeed");
    }

    [Fact]
    public void GenerateAll_DeepGitAttributesArtifactsToTheirRealCommitDay()
    {
        // The bug fix's core: with --deep-git, an artifact is listed as "updated" ONLY on the day a commit actually
        // touched it — not bunched onto today. Here epics.md (a recognized artifact → epics.html) is committed, so
        // its commit day's date page lists it under "Artifacts updated" and the timeline row counts it.
        Assert.True(TryCommitArtifact(), "git CLI unavailable on this host — cannot exercise git-derived artifact days; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        Assert.True(File.Exists(Timeline));
        var timeline = File.ReadAllText(Timeline);
        Assert.Contains("artifact updated", timeline); // the summary counts the real artifact change ("1 artifact updated")

        // Exactly one date page (the single commit day) lists epics.md under "Artifacts updated" — linking to its
        // generated page — and no page attributes an artifact to a day git didn't record a change on.
        var dayPages = Directory.GetFiles(CommitsDayDir, "*.html");
        Assert.NotEmpty(dayPages);
        var withArtifacts = dayPages.Where(p => File.ReadAllText(p).Contains("artifacts-updated")).ToList();
        Assert.Single(withArtifacts);
        var page = File.ReadAllText(withArtifacts[0]);
        Assert.Contains("class=\"artifact-update-list\"", page);
        Assert.Contains("../epics.html", page); // the recognized artifact links to its page

        // No stray attribution: tracked.txt is not a recognized artifact, so it never appears in the section.
        Assert.DoesNotContain("tracked.txt", page);
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
            Regex.Replace(html, @"on \w+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", "on <t>");

        Assert.Equal(Stable(File.ReadAllText(Timeline)), Stable(File.ReadAllText(Path.Combine(site2, "timeline.html"))));

        var pages1 = Directory.GetFiles(CommitsDayDir, "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        var pages2 = Directory.GetFiles(Path.Combine(site2, "commits"), "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(pages1.Select(Path.GetFileName), pages2.Select(Path.GetFileName));
        for (var i = 0; i < pages1.Count; i++)
        {
            Assert.Equal(Stable(File.ReadAllText(pages1[i])), Stable(File.ReadAllText(pages2[i])));
        }
    }

    [Fact]
    public void GenerateAll_DatePagePager_IsNewestFirst_PrevIsNewerDay_NextIsOlder()
    {
        // Three commits on three distinct days so the MIDDLE date page has a sibling on each side. [Prev/next navigation]
        Assert.True(TryCreateBackdatedHistory(), "git CLI unavailable on this host — cannot exercise the date pager; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options()).GenerateAll();
        AssertNoErrors(events);

        var middle = Path.Combine(CommitsDayDir, "2026-03-02.html");
        Assert.True(File.Exists(middle), "expected a date page for the middle backdated day");
        var html = File.ReadAllText(middle);

        // Newest-first: Prev links to the NEWER adjacent day, Next to the OLDER — the user-chosen direction.
        var prev = Regex.Match(html, "entity-pager-prev\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        var next = Regex.Match(html, "entity-pager-next\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.Contains("2026-03-03.html", prev);
        Assert.Contains("2026-03-01.html", next);
        // The pager rides the header (before the article); the retired bottom nav is gone.
        Assert.Contains("<nav class=\"entity-pager\"", html);
        Assert.DoesNotContain("commit-day-nav", html);
    }

    /// <summary>Initializes a git repo and commits on three distinct, backdated days (author AND committer date
    /// pinned so day-grouping is deterministic regardless of which git date drives it). Returns false (test no-ops)
    /// when the git CLI is unavailable.</summary>
    private bool TryCreateBackdatedHistory()
    {
        if (!RunGit("init")) return false;
        return CommitOn("2026-03-01") && CommitOn("2026-03-02") && CommitOn("2026-03-03");
    }

    private bool CommitOn(string day)
    {
        File.WriteAllText(Path.Combine(_root, $"file-{day}.txt"), day);
        if (!RunGit("add .")) return false;
        var stamp = $"{day}T12:00:00";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c user.name=\"Timeline Tester\" -c user.email=timeline@example.com -c commit.gpgsign=false commit --date=\"{stamp}\" -m \"commit on {day}\"",
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

    /// <summary>Initializes a git repo and commits the whole fixture tree — including the recognized artifact
    /// <c>_bmad-output/planning-artifacts/epics.md</c> — so the --deep-git per-file history attributes that artifact
    /// to its real commit day. Returns false (test no-ops) when the git CLI is unavailable.</summary>
    private bool TryCommitArtifact()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "one\n");
        return RunGit("add .") && Commit("Implement Story 1.1 foundation and add epics");
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
