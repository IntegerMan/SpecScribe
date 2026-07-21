using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 7.6: with source code present under the repo root, a
/// <c>code-map.html</c> treemap page is produced (server-rendered SVG + a text-equivalent table, inside the
/// standard page shell) and the "Code Map" nav item + dashboard quick link appear; with no readable source files,
/// none of those exist and no broken links are emitted. In a non-git temp repo the deep-git metrics are absent, so
/// the page renders sized-by-LOC with the "git data unavailable" notice. Follows the temp-dir fixture style of the
/// sprint/structure generation tests. Replaced the retired Story 3.4 SiteGeneratorStructureTests. [Story 7.6]</summary>
public class SiteGeneratorCodeMapTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-codemap-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string CodeMapPage => Path.Combine(Site, "code-map.html");
    private string RiskQuadrantPage => Path.Combine(Site, "risk-quadrant.html");
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

    private const string WidgetCs = """
        namespace Sample;

        public sealed class Widget
        {
            public int Value { get; set; }
            public string Render() => $"<b>{Value}</b>";
        }
        """;

    public SiteGeneratorCodeMapTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "Sample"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(_root, "src", "Sample", "Widget.cs"), WidgetCs);
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
    public void GenerateAll_WithSourceCode_ProducesCodeMapPageWithTreemapNavAndQuickLink()
    {
        GenerateSite();

        Assert.True(File.Exists(CodeMapPage), "code-map.html should be generated when readable source files exist");
        var html = File.ReadAllText(CodeMapPage);

        // The standard standalone-page shell: single main landmark, nav, breadcrumb.
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"site-nav\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);

        // The server-rendered SVG treemap + the no-JS text-equivalent table listing the walked source file.
        Assert.Contains("class=\"codemap\"", html);
        Assert.Contains("codemap-cell", html);
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/Sample/Widget.cs", html);
        // Non-git temp repo → no deep-git metrics → sized-by-LOC with the graceful-degradation notice.
        Assert.Contains("codemap-notice", html);
        // Round 2: the two pure-CSS exclude-filter checkboxes and their four precomputed panels are always present.
        Assert.Contains("id=\"cm-exclude-spec\"", html);
        Assert.Contains("id=\"cm-exclude-tests\"", html);
        Assert.Contains("data-view=\"full\"", html);

        // Code Map is reachable from the global journey nav menu (the Codebase group), pointing at the page.
        var index = File.ReadAllText(IndexPage);
        Assert.Contains("href=\"code-map.html\"", index);
        Assert.Contains(">Code Map</a>", index);

        AssertNoBrokenLocalLinks(CodeMapPage);
        AssertNoBrokenLocalLinks(IndexPage);
    }

    [Fact]
    public void GenerateAll_WithCodeSourceBaseUrlConfigured_LinksTreemapCellsAndTableRowsToSource()
    {
        // Story 7.6 review: fileHref is now wired via the same guarded CodeItemHref resolver every other
        // git-analytics surface uses. --code-url mode resolves for ANY walked file (no citation needed), so it's
        // the simplest fixture to prove the seam is live (AC #3: "routes to its code page... when available").
        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false,
            codeSourceBaseUrl: "https://github.com/example/repo/blob/main"));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(CodeMapPage);
        var expectedHref = "https://github.com/example/repo/blob/main/src/Sample/Widget.cs";

        // Both the SVG rect's <a> and the text-equivalent table's row link to the SAME resolved target.
        Assert.Contains($"href=\"{expectedHref}\"", html);
        Assert.Contains($"<a href=\"{expectedHref}\">src/Sample/Widget.cs</a>", html);
    }

    [Fact]
    public void GenerateAll_WithNoReadableSourceFiles_OmitsCodeMapPageAndNav()
    {
        // Remove every file under the repo root so the source-code walk finds nothing.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        GenerateSite();

        Assert.False(File.Exists(CodeMapPage), "no code-map.html without any readable source files");
        var index = File.ReadAllText(IndexPage);
        Assert.DoesNotContain("href=\"code-map.html\"", index);

        AssertNoBrokenLocalLinks(IndexPage);
    }

    [Fact]
    public void GenerateAll_WithoutDeepGit_FileTypeIsTheDefaultColorizeDimensionWithADiscreteLegend()
    {
        // Story 7.9: this fixture is a non-git temp repo (no --deep-git), so hasMetrics is false; file type needs
        // no git data, so it becomes the baked-in default colorize dimension instead of a flat neutral fill.
        GenerateSite();

        var html = File.ReadAllText(CodeMapPage);
        Assert.Contains("value=\"filetype\" selected", html);
        Assert.Contains("codemap-legend-discrete", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\">", html); // visible (not hidden)
        Assert.DoesNotContain("codemap-cell level-none", html); // no flat-neutral fallback in this state anymore
        Assert.Contains("codemap-cell type-", html);
        Assert.Contains(">Type</th>", html); // always-present text-table column
        Assert.Contains(">C#</td>", html);   // src/Sample/Widget.cs classifies as C#

        // The secondary (demoted) notice explains only the six git-derived dimensions are unavailable — the
        // controls are no longer a fully-hidden block.
        Assert.Contains("codemap-notice-secondary", html);
    }

    [Fact]
    public void GenerateAll_DeterministicAcrossTwoRuns()
    {
        GenerateSite();
        var first = File.ReadAllText(CodeMapPage);

        Directory.Delete(Site, recursive: true);
        GenerateSite();
        var second = File.ReadAllText(CodeMapPage);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateAll_OversizedTextFile_StillAppearsOnCodeMap()
    {
        // >1MB text must still contribute LOC to the treemap (streamed count); the 1MB cap is render-only.
        var oversized = Path.Combine(_root, "src", "Sample", "Huge.cs");
        var body = new string('x', 1_100_000);
        File.WriteAllText(oversized, "namespace Sample;\n// " + body + "\n");

        GenerateSite();

        Assert.True(File.Exists(CodeMapPage));
        var html = File.ReadAllText(CodeMapPage);
        Assert.Contains("src/Sample/Huge.cs", html);
        Assert.Contains("src/Sample/Widget.cs", html);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_DefaultDimensionIsUnchangedAndFileTypeIsASelectable7thOption()
    {
        // AC #3 regression guard: when real git metrics ARE available, the baked-in default colorize dimension
        // stays change frequency exactly as pre-7.9 — file type is added as a 7th dropdown option, not a
        // replacement default.
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise --deep-git generation; install git rather than silently skipping this test");

        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, deepGitAnalytics: true));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = File.ReadAllText(CodeMapPage);
        Assert.Contains("value=\"changes\" selected", html);   // unchanged sequential default (AC #3)
        Assert.Contains("value=\"filetype\">File type</option>", html); // 7th option, not selected
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\">", html); // ramp legend visible by default
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" hidden>", html); // discrete legend pre-rendered, hidden
        Assert.Contains(">Type</th>", html); // Type column always present regardless of hasMetrics
    }

    /// <summary>Initializes a real git repo in the fixture root with one commit, so <c>hasMetrics</c> is true —
    /// mirrors <see cref="SiteGeneratorCodeInsightsTests"/>'s fixture. Returns false (test no-ops) when the git
    /// CLI is unavailable.</summary>
    private bool TryCreateGitHistory()
    {
        if (!RunGit("init")) return false;
        if (!RunGit("add .")) return false;
        return Commit("Seed the repo");
    }

    private bool Commit(string message) => RunGit(
        $"-c user.name=\"CodeMap Tester\" -c user.email=codemap@example.com -c commit.gpgsign=false commit -m \"{message}\"");

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

    /// <summary>Every local (non-anchor, non-scheme) href on the page resolves to a file that was actually
    /// generated — the "never a broken link" guarantee (AC #3, NFR2).</summary>
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
}
