using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 3.8's Git Insights hub. The load-bearing AC #2 pin: with
/// <c>DeepGitAnalytics == false</c> no <c>git-insights.html</c> is produced, no error is reported, and the
/// default dashboard carries no hub link — the gate lives at the option/render boundary, never a wall-clock
/// timing test. The enabled path (real git history) and the determinism check shell out to the git CLI and
/// no-op gracefully when git is unavailable on the host. Follows the temp-dir fixture style of
/// <see cref="SiteGeneratorSprintTests"/>.</summary>
public class SiteGeneratorGitInsightsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-gitinsights-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string HubPage => Path.Combine(Site, "git-insights.html");
    private string IndexPage => Path.Combine(Site, "index.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorGitInsightsTests()
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
    public void GenerateAll_FlagOff_EmitsNoHubAndNoErrorAndNoDashboardLink()
    {
        // AC #2's performance guarantee is this gate: flag off -> the deep path (and the hub behind it)
        // never runs, so no page, no link, no error — the default output is untouched.
        var events = new SiteGenerator(Options(deepGit: false)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(File.Exists(HubPage), "git-insights.html must not exist when --deep-git is off");
        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("git-insights.html", index);
        Assert.DoesNotContain("View all git insights", index);
    }

    [Fact]
    public void GenerateAll_FlagOnWithoutGitHistory_DegradesToNoHubWithoutError()
    {
        // The temp fixture is not a git repository, so the deep pass yields null. NFR-2: no hub, no error,
        // and the rest of the site still generates.
        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.False(File.Exists(HubPage));
        Assert.True(File.Exists(Path.Combine(Site, "epics.html")), "baseline generation must still succeed");
    }

    [Fact]
    public void GenerateAll_FlagOnWithHistory_EmitsHubAndDashboardLink()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise gated hub generation; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(HubPage), "git-insights.html must be generated when --deep-git has data");

        var hub = File.ReadAllText(HubPage);
        Assert.Contains(">Files &amp; Contributors</h2>", hub);
        Assert.Contains(">Activity Over Time</h2>", hub);
        Assert.Contains("tracked.txt", hub);          // a known committed file appears in the frequency table
        Assert.Contains("id=\"gi-file-0\"", hub);      // its contributor drill-down panel is present
        Assert.Contains("Insight Tester", hub);        // the committing author appears as a file contributor

        var index = File.ReadAllText(IndexPage);
        Assert.Contains("href=\"git-insights.html\"", index);
        Assert.Contains("View all git insights", index);
    }

    [Fact]
    public void GenerateAll_TwoRunsProduceIdenticalHubMarkup()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise determinism; install git rather than silently skipping this test");

        var site2 = Path.Combine(_root, "site2");
        var events1 = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        var events2 = new SiteGenerator(Options(deepGit: true, output: site2)).GenerateAll();
        AssertNoErrors(events1);
        AssertNoErrors(events2);

        // The footer carries the generation timestamp — strip it, then the hub must be byte-identical.
        static string Stable(string html) => Regex.Replace(html, @"on \d{4}-\d{2}-\d{2} \d{2}:\d{2}", "on <t>");
        var first = Stable(File.ReadAllText(HubPage));
        var second = Stable(File.ReadAllText(Path.Combine(site2, "git-insights.html")));
        Assert.Equal(first, second);
    }

    /// <summary>Initializes a real git repo in the fixture root with two commits by a known author. Returns
    /// false (tests no-op) when the git CLI is unavailable or refuses — identity and signing are forced via
    /// -c overrides so a host's global config can't break the fixture.</summary>
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
        $"-c user.name=\"Insight Tester\" -c user.email=insights@example.com -c commit.gpgsign=false commit -m \"{message}\"");

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
