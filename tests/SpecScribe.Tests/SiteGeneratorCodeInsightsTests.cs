using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.4's opt-in "Advanced coverage" section on code pages. The
/// load-bearing AC #1 pin: with <c>DeepGitAnalytics == false</c> a referenced file's code page renders NO
/// advanced-coverage section (baseline untouched, the deep pass never runs); the enabled path (real git history)
/// exercises the section's contributors/frequency/coupled/history render and determinism; and both the no-git and
/// external-link paths degrade to no section with no error (AC #2). Follows the temp-git fixture style of
/// <see cref="SiteGeneratorCommitDetailsTests"/>.</summary>
public class SiteGeneratorCodeInsightsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-codeinsight-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Site => Path.Combine(_root, "site");
    private string ArtifactsDir => Path.Combine(Source, "implementation-artifacts");
    private string SrcDir => Path.Combine(_root, "src", "Lib");
    private string ReferencedPage => Path.Combine(Site, "code", "src", "Lib", "Referenced.cs.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.
        """;

    public SiteGeneratorCodeInsightsTests()
    {
        Directory.CreateDirectory(ArtifactsDir);
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(SrcDir);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(SrcDir, "Referenced.cs"), "namespace Lib;\npublic class Referenced { }\n");
        File.WriteAllText(Path.Combine(SrcDir, "Sibling.cs"), "namespace Lib;\npublic class Sibling { }\n");
        // Both files are cited, so both get code pages — the sibling proves coupled-file links can resolve.
        File.WriteAllText(Path.Combine(ArtifactsDir, "1-1-notes.md"),
            "# Notes\n\n[Source: `src/Lib/Referenced.cs:2`] and [Source: `src/Lib/Sibling.cs:2`].\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(bool deepGit, string? output = null, string? codeSourceBaseUrl = null) => ForgeOptions.Resolve(
        source: Source, output: output ?? Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit, codeSourceBaseUrl: codeSourceBaseUrl);

    private static void AssertNoErrors(IReadOnlyList<GenerationEvent> events)
    {
        var errors = events.Where(e => e.Outcome == GenerationOutcome.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => $"{e.RelativePath}: {e.Message}")));
    }

    [Fact]
    public void GenerateAll_FlagOff_RendersNoAdvancedCoverageSection()
    {
        // AC #1 baseline guarantee: with --deep-git off the code page carries no advanced-coverage section and no
        // deep pass (and thus no per-file insight) ever runs.
        var events = new SiteGenerator(Options(deepGit: false)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(ReferencedPage));
        Assert.DoesNotContain("code-insights", File.ReadAllText(ReferencedPage));
        // The deep pass never ran → no deep-analytics page.
        Assert.False(File.Exists(Path.Combine(Site, "deep-analytics.html")));
    }

    [Fact]
    public void GenerateAll_FlagOnWithoutGitHistory_DegradesToNoSectionWithoutError()
    {
        // The temp fixture is not a git repo → the deep pass yields null. AC #2: no section, no error, page renders.
        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(ReferencedPage));
        Assert.DoesNotContain("code-insights", File.ReadAllText(ReferencedPage));
    }

    [Fact]
    public void GenerateAll_FlagOnWithHistory_RendersAdvancedCoverageSection()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise gated advanced-coverage generation; install git rather than silently skipping this test");

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(ReferencedPage));
        var html = File.ReadAllText(ReferencedPage);

        Assert.Contains("class=\"code-insights\"", html);
        Assert.Contains("Advanced coverage", html);
        // Contributor attribution (the known fixture author), framed as commits — not a ranking.
        Assert.Contains("Insight Tester", html);
        Assert.Contains("Change frequency", html);
        // Story 7.8 (AC #2): the visible "Often changed with" list is GONE — the coupling now renders as a
        // related-file node on the reference graph (neutral diamond + dashed edge) linking to the sibling's code page.
        Assert.DoesNotContain("Often changed with", html);
        Assert.Contains("class=\"ref-edge-file\"", html);
        Assert.Contains("class=\"ref-file-dot\"", html);
        Assert.Contains("code/src/Lib/Sibling.cs.html", html);
        // The accessible text equivalent of the related node is present with its co-change strength.
        Assert.Contains("Files changed alongside this one:", html);
        Assert.Contains("changed together", html);
        // Change history table with a real date.
        Assert.Contains("code-history-table", html);
    }

    [Fact]
    public void GenerateAll_DeletedCoupledFile_RendersNonLinkChip()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the non-link chip path; install git rather than silently skipping this test");

        // A third file that co-changes with Referenced.cs but is cited by NO artifact AND is then deleted from disk.
        // Analytics-discovered code pages still require a real on-disk repo file (TryResolveRepoFile), so this coupled
        // file gets no page — on Referenced.cs's graph it must be a non-link chip (still shown + tooltipped from the
        // co-change history), never a dead link. (A coupled file that still exists now DOES get a page; see
        // GenerateAll_CoupledUncitedFile_NowGetsInPortalCodePage.)
        File.WriteAllText(Path.Combine(SrcDir, "Uncited.cs"), "namespace Lib;\npublic class Uncited { }\n");
        File.WriteAllText(Path.Combine(SrcDir, "Referenced.cs"), "namespace Lib;\npublic class Referenced { /* v3 */ }\n");
        Assert.True(RunGit("add .") && Commit("Change Referenced alongside an uncited helper"));
        File.Delete(Path.Combine(SrcDir, "Uncited.cs"));
        Assert.True(RunGit("add -A") && Commit("Remove the uncited helper (co-change history remains)"));

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        var html = File.ReadAllText(ReferencedPage);

        Assert.Contains("ref-file-node--chip", html);          // the uncited coupled file is a non-link chip
        Assert.Contains("src/Lib/Uncited.cs", html);           // still surfaced (tooltip + sr-only text)
        Assert.False(File.Exists(Path.Combine(Site, "code", "src", "Lib", "Uncited.cs.html")));
        Assert.DoesNotContain("code/src/Lib/Uncited.cs.html", html);   // never a link to a page that does not exist
    }

    [Fact]
    public void GenerateAll_CoupledUncitedFile_NowGetsInPortalCodePage()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise analytics-discovered code pages; install git rather than silently skipping this test");

        // A helper that co-changes with Referenced.cs but is cited by NO artifact. It IS a real on-disk source file,
        // so the git-analytics discovery pass now mints an in-portal code page for it (its related-files/insights are
        // the point of the source view) and Referenced.cs's related-file node links to that page instead of a chip.
        File.WriteAllText(Path.Combine(SrcDir, "Helper.cs"), "namespace Lib;\npublic class Helper { }\n");
        File.WriteAllText(Path.Combine(SrcDir, "Referenced.cs"), "namespace Lib;\npublic class Referenced { /* v4 */ }\n");
        Assert.True(RunGit("add .") && Commit("Change Referenced alongside an uncited on-disk helper"));

        var events = new SiteGenerator(Options(deepGit: true)).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(Path.Combine(Site, "code", "src", "Lib", "Helper.cs.html")),
            "an uncited but on-disk coupled file should now get an in-portal code page");
        var html = File.ReadAllText(ReferencedPage);
        Assert.Contains("code/src/Lib/Helper.cs.html", html);   // linked on the reference graph, not a chip
    }

    [Fact]
    public void GenerateAll_DeletedHotFile_ExternalMode_DegradesToPlainTextNotDeadLink()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise the deleted-file external-link guard; install git rather than silently skipping this test");

        // A file changed several times (so it ranks as a hot/top-changed file) then DELETED. With an external base
        // configured, its analytics link must NOT become an external blob/<branch>/<deleted-path> URL that would
        // 404 — a vanished file has no in-portal page and no valid external target, so it degrades to plain text.
        var gone = Path.Combine(SrcDir, "Gone.cs");
        File.WriteAllText(gone, "namespace Lib;\npublic class Gone { }\n");
        Assert.True(RunGit("add .") && Commit("Add Gone"));
        File.WriteAllText(gone, "namespace Lib;\npublic class Gone { /* churn */ }\n");
        Assert.True(RunGit("add .") && Commit("Churn Gone"));
        File.Delete(gone);
        Assert.True(RunGit("add -A") && Commit("Delete Gone"));

        var events = new SiteGenerator(Options(deepGit: true, codeSourceBaseUrl: "https://example.com/blob/main")).GenerateAll();

        AssertNoErrors(events);
        Assert.False(File.Exists(Path.Combine(Site, "code", "src", "Lib", "Gone.cs.html")));   // deleted → no page
        // No surface (dashboard Git Pulse, deep-analytics, git-insights, code map) may link the vanished file out.
        foreach (var page in Directory.EnumerateFiles(Site, "*.html", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("example.com/blob/main/src/Lib/Gone.cs", File.ReadAllText(page));
        }
    }

    [Fact]
    public void GenerateAll_ExternalMode_StillGeneratesCodePagesWithAdditiveSection()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise external-mode behavior; install git rather than silently skipping this test");

        // Story 7.7 made --code-url ADDITIVE: in-portal code pages always generate (each gaining a "view online"
        // link), so the advanced-coverage section still renders alongside the external link — no error.
        var events = new SiteGenerator(Options(deepGit: true, codeSourceBaseUrl: "https://example.com/blob/main")).GenerateAll();

        AssertNoErrors(events);
        Assert.True(File.Exists(ReferencedPage));
        var html = File.ReadAllText(ReferencedPage);
        Assert.Contains("code-external-link", html);
        Assert.Contains("class=\"code-insights\"", html);
    }

    [Fact]
    public void GenerateAll_TwoRunsProduceIdenticalCodePageMarkup()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise determinism; install git rather than silently skipping this test");

        var site2 = Path.Combine(_root, "site2");
        var events1 = new SiteGenerator(Options(deepGit: true)).GenerateAll();
        var events2 = new SiteGenerator(Options(deepGit: true, output: site2)).GenerateAll();
        AssertNoErrors(events1);
        AssertNoErrors(events2);

        static string Stable(string html) =>
            Regex.Replace(html, @"on \w+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", "on <t>");

        var page2 = Path.Combine(site2, "code", "src", "Lib", "Referenced.cs.html");
        Assert.Equal(Stable(File.ReadAllText(ReferencedPage)), Stable(File.ReadAllText(page2)));
    }

    /// <summary>Initializes a real git repo in the fixture root with two commits by a known author, the second
    /// changing both cited files together (so Referenced.cs gains a contributor, a change history, and a coupling
    /// with Sibling.cs). Returns false (test no-ops) when the git CLI is unavailable.</summary>
    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        if (!RunGit("add .")) return false;
        if (!Commit("Seed the library")) return false;
        File.WriteAllText(Path.Combine(SrcDir, "Referenced.cs"), "namespace Lib;\npublic class Referenced { /* v2 */ }\n");
        File.WriteAllText(Path.Combine(SrcDir, "Sibling.cs"), "namespace Lib;\npublic class Sibling { /* v2 */ }\n");
        return RunGit("add .") && Commit("Evolve Referenced and Sibling together");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"Insight Tester\" -c user.email=insight@example.com -c commit.gpgsign=false commit -m \"{message}\"");

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
