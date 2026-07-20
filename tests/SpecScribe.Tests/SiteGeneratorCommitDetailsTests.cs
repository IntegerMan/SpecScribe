using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.5's per-commit detail pages. The load-bearing AC #2 pin: with
/// <c>DeepGitAnalytics == false</c> no <c>commit/</c> directory is produced, no error is reported, and the
/// per-day pages render plain <c>&lt;code&gt;</c> hashes — the gate lives at the option/render boundary, never a
/// wall-clock timing test. The enabled path (real git history) exercises page emission, the day-page + hub hash
/// links lighting up, reference linkification, and determinism; it no-ops gracefully when git is unavailable on
/// the host. Follows the temp-dir fixture style of <see cref="SiteGeneratorGitInsightsTests"/>.</summary>
public class SiteGeneratorCommitDetailsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-commitdetail-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string CommitDir => Path.Combine(Site, "commit");
    private string CommitsDayDir => Path.Combine(Site, "commits");
    private string HubPage => Path.Combine(Site, "git-insights.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorCommitDetailsTests()
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

    private ForgeOptions Options(bool deepGit, string? output = null) => ForgeOptions.Resolve(
        source: Source, output: output ?? Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_FlagOff_EmitsNoCommitDirAndPlainDayHashes()
    {
        // AC #2's performance guarantee is this gate: flag off -> the deep path (and per-commit pages behind it)
        // never runs, so no commit/ dir, no error, and the day-page hashes stay plain <code> (no commit/ links).
        var events = new SiteGenerator(Options(deepGit: false)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(Directory.Exists(CommitDir), "commit/ must not exist when --deep-git is off");

        // This fixture is not a git repo, so there are no date pages at all now (Story 7.3's date pages are
        // git-derived — no mtime fallback). If any did render, none links into commit/ when --deep-git is off
        // (the resolver has no pages) and any commit hashes stay plain <code>.
        if (Directory.Exists(CommitsDayDir))
        {
            foreach (var page in Directory.GetFiles(CommitsDayDir, "*.html"))
            {
                var day = File.ReadAllText(page);
                Assert.DoesNotContain("../commit/", day);
                if (day.Contains("commit-day-list"))
                {
                    Assert.Contains("<code class=\"commit-hash\">", day);
                }
            }
        }
    }

    [Fact]
    public void GenerateAll_FlagOnWithoutGitHistory_DegradesToNoCommitPagesWithoutError()
    {
        // The temp fixture is not a git repository, so the deep pass yields null. NFR-2: no commit/ dir, no error,
        // and the rest of the site still generates.
        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(Directory.Exists(CommitDir));
        Assert.True(File.Exists(Path.Combine(Site, "epics.html")), "baseline generation must still succeed");
    }

    [Fact]
    public void GenerateAll_FlagOnWithHistory_EmitsBoundedCommitPagesAndSubjectAndAuthor()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise gated commit-page generation; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(Directory.Exists(CommitDir), "commit/ must be generated when --deep-git has data");

        var pages = Directory.GetFiles(CommitDir, "*.html");
        Assert.NotEmpty(pages);
        Assert.True(pages.Length <= 300, "per-commit pages are bounded by the -n 300 deep window");

        var allPages = string.Concat(pages.Select(File.ReadAllText));
        Assert.Contains("Implement Story 1.1 foundation", allPages);   // a known commit subject
        Assert.Contains("by Detail Tester", allPages);                  // author shown as attribution, not a rank
    }

    [Fact]
    public void GenerateAll_CommitDetailPager_IsChronological_PrevIsEarlierCommit_NextIsLater()
    {
        // Two commits, oldest first: "Implement Story 1.1 foundation" then "Second commit". [Prev/next navigation]
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the commit pager; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The subject "Story 1.1" gets linkified inside <h1> (see the reference-linkification test below), so
        // match on the h1's inner text loosely rather than the exact escaped subject string.
        var pages = Directory.GetFiles(CommitDir, "*.html");
        var olderPage = pages.Single(p => Regex.Match(File.ReadAllText(p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("foundation"));
        var olderHtml = File.ReadAllText(olderPage);
        var newerPage = pages.Single(p => Regex.Match(File.ReadAllText(p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("Second commit"));
        var newerHtml = File.ReadAllText(newerPage);

        // The oldest commit's page has no earlier sibling (Prev disabled) and Next points at the newer commit.
        Assert.Contains("entity-pager-prev is-disabled", olderHtml);
        var olderNext = Regex.Match(olderHtml, "entity-pager-next\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.EndsWith(Path.GetFileName(newerPage), olderNext);

        // The newest commit's page has no later sibling (Next disabled) and Prev points back at the older commit.
        Assert.Contains("entity-pager-next is-disabled", newerHtml);
        var newerPrev = Regex.Match(newerHtml, "entity-pager-prev\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.EndsWith(Path.GetFileName(olderPage), newerPrev);
    }

    [Fact]
    public void GenerateAll_CommitDetailPage_LocalContextBand_ListsSiblingCommitsWithCurrentMarkedActive()
    {
        // [Story 10.10 review — patch] no direct test previously exercised the commit-page NavLocalContext
        // builder; only the generic seam mechanics were covered.
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the commit local-context band; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        var pages = Directory.GetFiles(CommitDir, "*.html");
        var olderPage = pages.Single(p => Regex.Match(File.ReadAllText(p), "<h1>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value.Contains("foundation"));
        var olderHtml = File.ReadAllText(olderPage);

        Assert.Contains("site-nav-local-context", olderHtml);
        Assert.Contains("Recent commits", olderHtml);
        // The current commit renders as an inactive-safe <span>, never a self-link, while the sibling commit
        // is a real link — same "current page never self-links" rule the pager and breadcrumb already follow.
        Assert.Contains("local-context-pill active", olderHtml);
        Assert.Matches(new Regex("<a[^>]*class=\"local-context-pill\"[^>]*>[^<]*Second commit"), olderHtml);
    }

    [Fact]
    public void GenerateAll_FlagOnWithHistory_LightsUpDayPageAndHubHashLinks()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise gated hash-link wiring; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The per-day page's hash is now a link into commit/ (from commits/ depth → ../commit/…).
        var dayPages = Directory.GetFiles(CommitsDayDir, "*.html");
        Assert.NotEmpty(dayPages);
        var anyDayLinks = dayPages.Any(p => File.ReadAllText(p).Contains("class=\"commit-hash-link\" href=\"../commit/"));
        Assert.True(anyDayLinks, "a day page's hash should link into commit/ when a per-commit page exists");

        // The Git Insights hub's "latest {hash}" link resolves to a commit/ page (hub is at root → commit/…).
        Assert.True(File.Exists(HubPage));
        var hub = File.ReadAllText(HubPage);
        Assert.Contains("href=\"commit/", hub);
    }

    [Fact]
    public void GenerateAll_FlagOnWithHistory_LinkifiesReferencesInCommitMessages()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise reference linkification; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        AssertNoErrors(events);

        // The commit subject "Implement Story 1.1 foundation" becomes a guarded story link via ApplyReferenceLinks.
        var allPages = string.Concat(Directory.GetFiles(CommitDir, "*.html").Select(File.ReadAllText));
        Assert.Contains("class=\"story-ref\" href=\"../epics/story-1-1.html\"", allPages);
    }

    [Fact]
    public void GenerateAll_TwoRunsProduceIdenticalCommitMarkup()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise determinism; install git rather than silently skipping this test");

        var site2 = Path.Combine(_root, "site2");
        var events1 = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        var events2 = new SiteGenerator(Options(deepGit: true, output: site2)).GenerateAll();
        AssertNoErrors(events1);
        AssertNoErrors(events2);

        // Strip the human-friendly footer timestamp (24h + zone, Story 10.4), then every commit page must be
        // byte-identical run to run.
        static string Stable(string html) =>
            Regex.Replace(html, @"on \w+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", "on <t>");

        var pages1 = Directory.GetFiles(CommitDir, "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        var pages2 = Directory.GetFiles(Path.Combine(site2, "commit"), "*.html").OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(pages1.Select(Path.GetFileName), pages2.Select(Path.GetFileName));
        for (var i = 0; i < pages1.Count; i++)
        {
            Assert.Equal(Stable(File.ReadAllText(pages1[i])), Stable(File.ReadAllText(pages2[i])));
        }
    }

    /// <summary>Initializes a real git repo in the fixture root with two commits by a known author — the first
    /// referencing Story 1.1 (for the linkification check). Returns false (tests no-op) when the git CLI is
    /// unavailable or refuses; identity and signing are forced via -c overrides so a host's global config can't
    /// break the fixture.</summary>
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
        $"-c user.name=\"Detail Tester\" -c user.email=detail@example.com -c commit.gpgsign=false commit -m \"{message}\"");

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
