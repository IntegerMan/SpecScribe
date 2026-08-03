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
    private string CodeMapRoute => "code-map.html";
    private string RiskQuadrantRoute => "risk-quadrant.html";
    private string IndexRoute => "index.html";

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

    /// <summary>The ONE shared code-map island JSON (Story 20.10 D1/D2 — one payload, four server-declared views,
    /// replacing the four independent per-panel islands Story 20.9 shipped).</summary>
    private static string FullIsland(string html)
    {
        var m = Regex.Match(
            html,
            "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"codemap-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(m.Success, "expected the shared Code Map island");
        return m.Groups["j"].Value;
    }

    [Fact]
    public void GenerateAll_WithSourceCode_ProducesCodeMapPageWithTreemapNavAndQuickLink()
    {
        GenerateSite();

        Assert.True(SiteRegion.Exists(Site, CodeMapRoute), "code-map.html should be generated when readable source files exist");
        var html = SiteRegion.Read(Site, CodeMapRoute);

        // The standard standalone-page shell: single main landmark, nav, breadcrumb.
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"site-nav\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);

        // Story 20.9: the server-rendered SVG treemap is gone and the chart is the ONE Hierarchy Explorer over a
        // ProjectCodeMap payload. What a JS-off visitor gets is the per-variant text-equivalent table, which
        // Story 20.6 D1 audited and KEPT as this surface's twin because it is richer than the generic listing.
        Assert.Contains(HierarchyExplorer.HostMarker, html);
        Assert.Contains("ss-hierarchy-data", html);
        Assert.DoesNotContain("class=\"codemap\"", html);
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/Sample/Widget.cs", html);
        // The vendored engine reaches THIS page. [Story 23.6 AC #8] The `<script src>` tag is chrome and no C#
        // code path emits it now, but the invariant it stood for survives intact and is asserted in the two
        // places it actually lives: the region carries the host marker (above), which is precisely what makes
        // `chromeNeeds().needsHierarchyEngine` true on the renderer side, and the engine file is on disk
        // (below). Story 20.7's miss — a correct payload mounting nothing because the flag never reached the
        // page — is caught by the host-marker assertion, since the renderer derives from that marker rather
        // than from any flag a templater might forget to set.
        Assert.True(HierarchyExplorer.ContainsHost(html),
            "the code map must host a hierarchy chart — that host is what makes the renderer ship the engine");
        Assert.True(File.Exists(Path.Combine(Site, ForgeOptions.HierarchyEngineScriptName)),
            "the hierarchy engine must be on disk for a source-only repo, not only when a dashboard chart copied it");
        // Non-git temp repo → no deep-git metrics → sized-by-LOC with the graceful-degradation notice.
        Assert.Contains("codemap-notice", html);
        // Round 2: the two pure-CSS exclude-filter checkboxes are always present. Story 20.10 D2 collapsed the
        // four precomputed panels into ONE instance whose views (still keyed "full"/"no-spec"/"no-tests"/
        // "no-spec-no-tests") live in the shared island's own `views` array.
        Assert.Contains("id=\"cm-exclude-spec\"", html);
        Assert.Contains("id=\"cm-exclude-tests\"", html);
        Assert.Contains("\"key\":\"full\"", FullIsland(html));

        // Code Map is reachable from the global journey nav menu (the Codebase group), pointing at the page.
        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("href=\"code-map.html\"", index);
        Assert.Contains(">Code Map</a>", index);

        SiteRegion.AssertNoBrokenLocalLinks(Site, CodeMapRoute);
        SiteRegion.AssertNoBrokenLocalLinks(Site, IndexRoute);
    }

    [Fact]
    public void GenerateAll_WithSourceCode_ProducesRiskQuadrantPageWithNavItemAndChartEmptyState()
    {
        // Story 7.10 review: the risk quadrant moved off code-map.html onto its own Insights page — it still
        // writes (source files exist) even in this non-git fixture, but below Charts.RiskQuadrantMinFiles the
        // chart itself degrades to its empty state (only 1 file here).
        GenerateSite();

        Assert.True(SiteRegion.Exists(Site, RiskQuadrantRoute), "risk-quadrant.html should be generated when readable source files exist");
        var html = SiteRegion.Read(Site, RiskQuadrantRoute);

        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        Assert.Contains("Refactor-Target Risk Quadrant", html);
        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<svg class=\"risk-quadrant\"", html);

        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.Contains("href=\"risk-quadrant.html\"", index);
        Assert.Contains(">Risk Quadrant</a>", index);

        SiteRegion.AssertNoBrokenLocalLinks(Site, RiskQuadrantRoute);
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

        var html = SiteRegion.Read(Site, CodeMapRoute);
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

        Assert.False(SiteRegion.Exists(Site, CodeMapRoute), "no code-map.html without any readable source files");
        Assert.False(SiteRegion.Exists(Site, RiskQuadrantRoute), "no risk-quadrant.html without any readable source files (shared gating signal)");
        var index = SiteRegion.Read(Site, IndexRoute);
        Assert.DoesNotContain("href=\"code-map.html\"", index);
        Assert.DoesNotContain("href=\"risk-quadrant.html\"", index);

        SiteRegion.AssertNoBrokenLocalLinks(Site, IndexRoute);
    }

    [Fact]
    public void GenerateAll_WithoutDeepGit_FileTypeIsTheDefaultColorizeDimensionWithADiscreteLegend()
    {
        // Story 7.9: this fixture is a non-git temp repo (no --deep-git), so hasMetrics is false; file type needs
        // no git data, so it becomes the baked-in default colorize dimension instead of a flat neutral fill.
        GenerateSite();

        var html = SiteRegion.Read(Site, CodeMapRoute);
        Assert.Contains("value=\"filetype\" selected", html);
        Assert.Contains("codemap-legend-discrete", html);
        // The default ("full") view's discrete legend ships visible; Story 20.10 adds data-hierarchy-legend-view.
        Assert.Contains("data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\" data-hierarchy-legend-view=\"full\">", html);
        // File type is the ONLY dimension declared - there is nothing for the six git-derived ramps to quantize,
        // which is the same rule the dropdown has always followed, now stated once in the contract.
        var island = FullIsland(html);
        Assert.Contains("\"key\":\"filetype\"", island);
        Assert.DoesNotContain("\"key\":\"changes\"", island);
        Assert.Contains("\"filetype\":\"csharp\"", island);
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
        var first = SiteRegion.Read(Site, CodeMapRoute);

        Directory.Delete(Site, recursive: true);
        GenerateSite();
        var second = SiteRegion.Read(Site, CodeMapRoute);

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

        Assert.True(SiteRegion.Exists(Site, CodeMapRoute));
        var html = SiteRegion.Read(Site, CodeMapRoute);
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

        var html = SiteRegion.Read(Site, CodeMapRoute);
        Assert.Contains("value=\"changes\" selected", html);   // unchanged sequential default (AC #3)
        Assert.Contains("value=\"filetype\">File type</option>", html); // 7th option, not selected
        // The default ("full") view's ramp legend ships visible; its discrete legend ships hidden.
        Assert.Contains("data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapRampLegend + "\" data-hierarchy-legend-view=\"full\">", html);
        Assert.Contains("data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\" data-hierarchy-legend-view=\"full\" hidden>", html);

        // Story 20.9: the dropdown's seven options are now backed by seven DECLARED dimensions, in the same
        // order, with change frequency first - so the control and the contract cannot drift apart.
        var island = FullIsland(html);
        foreach (var key in new[] { "changes", "last", "created", "avgchange", "churn", "cochange", "filetype" })
        {
            Assert.Contains("\"key\":\"" + key + "\"", island);
        }
        Assert.True(island.IndexOf("\"key\":\"changes\"", StringComparison.Ordinal) < island.IndexOf("\"key\":\"filetype\"", StringComparison.Ordinal),
            "change frequency is the default and must be declared first");
        Assert.Contains(">Type</th>", html); // Type column always present regardless of hasMetrics
    }

    [Fact]
    public void GenerateAll_WithDeepGitAndEnoughFiles_ProducesALiveRiskQuadrantWithAPointLinkingToItsCodePage()
    {
        // Needs at least Charts.RiskQuadrantMinFiles (6) distinct files with git history.
        for (var i = 0; i < 6; i++)
        {
            File.WriteAllText(Path.Combine(_root, "src", "Sample", $"File{i}.cs"),
                $"namespace Sample;\npublic sealed class File{i} {{ public int Value => {i}; }}\n");
        }
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise --deep-git generation; install git rather than silently skipping this test");
        // A second commit touching one file so at least one file has more than one change (varied churn signal).
        File.AppendAllText(Path.Combine(_root, "src", "Sample", "File0.cs"), "// touched again\n");
        Assert.True(RunGit("add ."));
        Assert.True(Commit("Touch File0 again"));

        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, deepGitAnalytics: true));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, RiskQuadrantRoute);
        Assert.Contains("<svg class=\"risk-quadrant\"", html);
        Assert.Contains("risk-point", html);
        SiteRegion.AssertNoBrokenLocalLinks(Site, RiskQuadrantRoute);
    }

    [Fact]
    public void GenerateAll_WithDeepGitAndEnoughFiles_RoutesSprintStatusAndEpicsLinksToTheirOwnPagesNotACodePage()
    {
        // Story 7.10 review: sprint-status.yaml and epics.md are BOTH walked as ordinary source-code-walk files
        // (this fixture's own _bmad-output is under the temp repo root) AND already have a dedicated rendered
        // page (sprint.html / epics.html) — CodeItemHref must prefer that page over the generic code/…html view.
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), """
            last_updated: 2026-07-16T12:00:00-04:00
            development_status:
              epic-1: done
              1-1-foundation-story: done
            """);

        for (var i = 0; i < 6; i++)
        {
            File.WriteAllText(Path.Combine(_root, "src", "Sample", $"File{i}.cs"),
                $"namespace Sample;\npublic sealed class File{i} {{ public int Value => {i}; }}\n");
        }
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise --deep-git generation; install git rather than silently skipping this test");
        // Touch sprint-status.yaml + epics.md across a few more commits so both accumulate real churn and rank
        // on the risk quadrant / code-map table alongside the plain source files.
        for (var touch = 0; touch < 3; touch++)
        {
            File.AppendAllText(Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml"), $"# touch {touch}\n");
            File.AppendAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), $"\n<!-- touch {touch} -->\n");
            Assert.True(RunGit("add ."));
            Assert.True(Commit($"Touch sprint + epics {touch}"));
        }

        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, deepGitAnalytics: true));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var codeMapHtml = SiteRegion.Read(Site, CodeMapRoute);
        // The text-equivalent table links each file's row header — sprint-status.yaml and epics.md must route to
        // their real rendered pages, never a code/…html raw view.
        Assert.Contains("<a href=\"sprint.html\">", codeMapHtml);
        Assert.Contains("<a href=\"epics.html\">", codeMapHtml);
        Assert.DoesNotContain("code/_bmad-output/implementation-artifacts/sprint-status.yaml.html", codeMapHtml);
        Assert.DoesNotContain("code/_bmad-output/planning-artifacts/epics.md.html", codeMapHtml);

        SiteRegion.AssertNoBrokenLocalLinks(Site, CodeMapRoute);
    }

    [Fact]
    public void GenerateAll_WithoutDeepGit_SunburstStillRendersColorizedByFileType()
    {
        // Story 7.12 review: the sunburst is now the treemap's "how to view it" shape sibling, sharing the SAME
        // colorize dimension — with no --deep-git data, that baked-in default is file type (AC #2 parity with
        // the treemap's own non-git degrade), never omitted.
        GenerateSite();

        var html = SiteRegion.Read(Site, CodeMapRoute);
        // One payload now serves BOTH shapes, so parity between them is structural rather than something two
        // renderers have to agree on: there is only one set of nodes and one dimension rule.
        Assert.Contains(HierarchyExplorer.HostMarker, html);
        Assert.DoesNotContain("class=\"codemap-sunburst\"", html);
        Assert.Contains("\"colorClass\":\"codemap-cell\"", FullIsland(html));
        Assert.Contains("\"filetype\":\"csharp\"", FullIsland(html));
        SiteRegion.AssertNoBrokenLocalLinks(Site, CodeMapRoute);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_SunburstRendersARealColoredWedgeLinkingToItsOwnCodePage()
    {
        Assert.True(TryCreateGitHistory(), "git CLI unavailable on this host — cannot exercise --deep-git generation; install git rather than silently skipping this test");

        var gen = new SiteGenerator(ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, deepGitAnalytics: true));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var html = SiteRegion.Read(Site, CodeMapRoute);
        // Real git history means the ramp has something to quantize. The LEVEL is resolved client-side per
        // dimension now (that is what makes a free staleness threshold and an arbitrary contributor possible at
        // all), so what the server can honestly assert is that the raw metric the ramp reads actually arrived -
        // and that the node still routes to its own code page through the guarded Story 7.1 resolver.
        var island = FullIsland(html);
        Assert.Contains(HierarchyExplorer.HostMarker, html);
        Assert.Matches(new Regex("\"changes\":\"[1-9][0-9]*\""), island);
        Assert.Matches(new Regex("\"href\":\"code/[^\"]+\\.html\""), island);
        SiteRegion.AssertNoBrokenLocalLinks(Site, CodeMapRoute);
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
}
