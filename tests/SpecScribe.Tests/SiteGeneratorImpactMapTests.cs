using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Generation-level coverage for Story 21.3: with an epics roster AND <c>--deep-git</c> commit/file data,
/// a <c>impact-map.html</c> page is produced (per-epic touched-file lists correlated from commit naming), the
/// "Impact Map" Delivery nav entry appears, and each attributed epic/story page carries a "Code Areas Touched"
/// widget. WITHOUT <c>--deep-git</c> — even though the epics roster is present — none of those exist (the combined
/// <c>hasEpics &amp;&amp; hasDeepAnalytics</c> gate, distinguishing this from 21.1/21.2's bare <c>hasEpics</c> gate).
/// Uses a real git fixture (mirrors <see cref="SiteGeneratorCodeMapTests"/>'s <c>--deep-git</c> pattern) since the
/// correlation genuinely needs commit history.</summary>
public class SiteGeneratorImpactMapTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-impact-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");
    private string ImpactMapPage => Path.Combine(Site, "impact-map.html");
    private string EpicsPage => Path.Combine(Site, "epics.html");
    private string Epic1Page => Path.Combine(Site, "epics", "epic-1.html");
    private string Story11Page => Path.Combine(Site, "epics", "story-1-1.html");

    private const string EpicsMd = """
        # Epics

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Second Story

        As a maintainer, I want more.
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: review

        ## Story

        As a maintainer, I want the foundation.

        ## Tasks / Subtasks

        - [x] Task 1: Build the widget.
        """;

    private const string WidgetCs = """
        namespace Sample;

        public sealed class Widget
        {
            public int Value { get; set; }
            public string Render() => $"<b>{Value}</b>";
        }
        """;

    public SiteGeneratorImpactMapTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "Sample"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation-story.md"), Story11Md);
        File.WriteAllText(Path.Combine(_root, "src", "Sample", "Widget.cs"), WidgetCs);
        File.WriteAllText(Path.Combine(Adrs, "README.md"), "# ADR Index\n\nRecords.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options(bool deepGit) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false,
        deepGitAnalytics: deepGit);

    private SiteGenerator GenerateSite(bool deepGit)
    {
        var gen = new SiteGenerator(Options(deepGit));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    // ---- With --deep-git: page + nav + widgets ----

    [Fact]
    public void GenerateAll_WithDeepGit_ProducesImpactMapPageWithNavAndAttribution()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"),
            "git CLI unavailable on this host — cannot exercise --deep-git generation; install git rather than silently skipping this test");
        GenerateSite(deepGit: true);

        Assert.True(File.Exists(ImpactMapPage));

        // The Delivery nav entry appears (root-relative on the epics page).
        Assert.Contains("href=\"impact-map.html\"", File.ReadAllText(EpicsPage));

        var impact = File.ReadAllText(ImpactMapPage);
        Assert.Contains("Epic 1", impact);
        // The commit touched Widget.cs, which got an in-portal code page → a real, non-dead link.
        Assert.Contains("code/src/Sample/Widget.cs.html", impact);
        // The honest best-effort caveat + a real correlated-commit count both render.
        Assert.Contains("analyzed commits correlated", impact);
        Assert.Contains("best-effort", impact, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_ImpactMapCarriesInteractiveTreemapAndNoScriptFallback()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: true);

        var impact = File.ReadAllText(ImpactMapPage);
        // The interactive scaffold: the JSON data island the script reads, the epic multi-select, and the mounts.
        Assert.Contains("id=\"impact-map-data\"", impact);
        Assert.Contains("application/json", impact);
        Assert.Contains("impact-epic-toggle", impact);
        Assert.Contains("id=\"impact-treemap\"", impact);
        // The payload carries the weight fields the shapes draw with (churn 'c' + commits 'k').
        Assert.Matches(new Regex("\"p\":\"src/Sample/Widget\\.cs\".*\"c\":\\d+.*\"k\":\\d+"), impact);
        // The epic selector reuses the sprint board's multi-select dropdown component.
        Assert.Contains("sprint-epic-filter impact-epic-filter", impact);
        Assert.Contains("sprint-epic-filter-count", impact);
        // The Treemap | Sunburst view toggle (board-tabs) + the sunburst mount both exist; the toggle radios sit
        // inside .impact-shapes so the pure-CSS :has() visibility swap can reach them.
        Assert.Contains("id=\"impact-view-treemap\"", impact);
        Assert.Contains("id=\"impact-view-sunburst\"", impact);
        Assert.Contains("id=\"impact-sunburst\"", impact);
        Assert.Matches(new Regex("<div class=\"impact-shapes\">\\s*<div class=\"board-tabs impact-shape-tabs\">"), impact);
        // The no-JS / accessible text-equivalent fallback list is present (and the controls start hidden).
        Assert.Contains("impact-fallback", impact);
        Assert.Contains("class=\"impact-controls\" hidden", impact);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_ImpactMapNavEntryCarriesAnIcon()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: true);

        // The Delivery nav entry for the impact map renders with its concept glyph, like every other nav item —
        // an <svg class="ss-icon"> immediately precedes the "Impact Map" link label on the epics page.
        var epicsHtml = File.ReadAllText(EpicsPage);
        Assert.Contains(Icons.ForConcept("Impact Map"), epicsHtml);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_EpicAndStoryPagesShowCodeAreasWidget()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: true);

        var epicHtml = File.ReadAllText(Epic1Page);
        Assert.Contains("Code Areas Touched", epicHtml);
        Assert.Contains("code/src/Sample/Widget.cs.html", epicHtml);
        Assert.Contains("See the full impact map", epicHtml);

        var storyHtml = File.ReadAllText(Story11Page);
        Assert.Contains("Code Areas Touched", storyHtml);
        Assert.Contains("code/src/Sample/Widget.cs.html", storyHtml);
    }

    // ---- Without --deep-git: combined gate holds (hasEpics alone is NOT sufficient) ----

    [Fact]
    public void GenerateAll_WithoutDeepGit_OmitsImpactMapPageNavAndWidget()
    {
        // A real git repo exists, but --deep-git is OFF → no DeepGit.Commits → the whole surface is absent even
        // though hasEpics is true. This is the distinguishing assertion vs 21.1/21.2's bare hasEpics gate.
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: false);

        Assert.False(File.Exists(ImpactMapPage));

        // Positive control: the epics page (and its Delivery nav) still exist — proving hasEpics IS true here, so
        // the omission below is specifically the missing hasDeepAnalytics half, not a missing roster.
        var epicsHtml = File.ReadAllText(EpicsPage);
        Assert.Contains("href=\"traceability.html\"", epicsHtml); // a bare-hasEpics Delivery sibling is present
        Assert.DoesNotContain("href=\"impact-map.html\"", epicsHtml);

        Assert.DoesNotContain("Code Areas Touched", File.ReadAllText(Epic1Page));
        Assert.DoesNotContain("Code Areas Touched", File.ReadAllText(Story11Page));
    }

    // ---- SPA / webview coherence ----

    [Fact]
    public void GenerateAll_WithDeepGit_ImpactMapCapturedForWebviewCoherence()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        var gen = new SiteGenerator(Options(deepGit: true)) { CapturePages = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var bundle = gen.RenderWebviewSurfaces();
        Assert.Contains(bundle.Surfaces, s => s.OutputRelativePath == "impact-map.html");
    }

    // ---- git fixture helpers (mirror SiteGeneratorCodeMapTests) ----

    private bool TryCreateGitHistory(string subject)
    {
        if (!RunGit("init")) return false;
        if (!RunGit("add .")) return false;
        return RunGit($"-c user.name=\"Impact Tester\" -c user.email=impact@example.com -c commit.gpgsign=false commit -m \"{subject}\"");
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
