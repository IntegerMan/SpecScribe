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
    private string ImpactMapRoute => "impact-map.html";
    private string EpicsRoute => "epics.html";
    private string Epic1Route => "epics/epic-1.html";
    private string Story11Route => "epics/story-1-1.html";
    private string Story13Route => "epics/story-1-3.html";

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

        ### Story 1.3: Untouched Story

        As a maintainer, I want a story the fixture commit never mentions.
        """;

    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: review

        ## Story

        As a maintainer, I want the foundation.

        ## Tasks / Subtasks

        - [x] Task 1: Build the widget.
        """;

    // Has its own artifact (so its page is generated) but the fixture commit's subject never names it — the
    // negative case Task 5 requires: an attributable story with zero attribution shows no widget at all
    // (absent, not an empty panel). [Review][Patch]
    private const string Story13Md = """
        # Story 1.3: Untouched Story

        Status: ready-for-dev

        ## Story

        As a maintainer, I want a story the fixture commit never mentions.
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
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-3-untouched-story.md"), Story13Md);
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

        Assert.True(SiteRegion.Exists(Site, ImpactMapRoute));

        // The Delivery nav entry appears (root-relative on the epics page).
        Assert.Contains("href=\"impact-map.html\"", SiteRegion.Read(Site, EpicsRoute));

        var impact = SiteRegion.Read(Site, ImpactMapRoute);
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

        var impact = SiteRegion.Read(Site, ImpactMapRoute);
        // STORY 20.7 converted this surface to the Hierarchy Explorer. The scaffold is the component's — ONE
        // island, ONE host, one selector — replacing the bespoke `impact-map-data` island and the two hand-rolled
        // mounts that `renderTreemap`/`renderSunburst` filled. Two islands on one page was the drift, not the fix.
        Assert.DoesNotContain("id=\"impact-map-data\"", impact);
        Assert.DoesNotContain("id=\"impact-treemap\"", impact);
        Assert.DoesNotContain("id=\"impact-sunburst\"", impact);
        Assert.Contains("id=\"impact-hierarchy-data\"", impact);
        Assert.Contains("application/json", impact);
        Assert.Contains("impact-epic-toggle", impact);

        // The payload carries the same facts the shapes draw with: the file's path, its churn as the layout value,
        // and its commit count in the reader-facing Detail sentence — which is the ramp's non-colour channel, so
        // the level is never signalled by fill alone (UX-DR17).
        Assert.Contains("src/Sample/Widget.cs", impact);
        Assert.Matches(new Regex("\"detail\":\"[0-9,]+ lines? changed across [0-9,]+ commits?\""), impact);
        // Owner D4: epic -> directory -> file, with the SHIPPED commit ramp as the leaf colour family.
        Assert.Matches(new Regex("\"colorClass\":\"impact-tm-tile impact-level-[1-5]\""), impact);
        Assert.Contains("attributed churn", impact);   // D4's counting basis, stated where a reader sees it

        // The epic selector keeps the sprint board's multi-select dropdown markup — this story changed what drives
        // the chart, not the control vocabulary — and now carries the component's generic filter hook.
        Assert.Contains("sprint-epic-filter impact-epic-filter", impact);
        Assert.Contains("sprint-epic-filter-count", impact);
        Assert.Contains("data-hierarchy-filter", impact);
        // The shape selector is the component's, ordered Sunburst-then-Treemap site-wide (owner D2) while this
        // instance still DEFAULTS to treemap — a deep file tree reads better as rectangles, and demoting that to
        // match the planning surfaces would be a regression dressed as consistency.
        Assert.Contains("id=\"impact-hierarchy-shape-sunburst\"", impact);
        Assert.Contains("id=\"impact-hierarchy-shape-treemap\"", impact);
        Assert.Matches(new Regex("value=\"treemap\" checked"), impact);

        // The no-JS / accessible text-equivalent fallback list is present, and every control still starts hidden —
        // now inside the component's own control bar, so a surface's controls inherit the mount handshake rather
        // than re-inventing it.
        Assert.Contains("impact-fallback", impact);
        Assert.Contains("class=\"ss-hierarchy-controls\" hidden", impact);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_ImpactMapNavEntryCarriesAnIcon()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: true);

        // The Delivery nav entry for the impact map renders with its concept glyph, like every other nav item —
        // an <svg class="ss-icon"> immediately precedes the "Impact Map" link label on the epics page.
        var epicsHtml = SiteRegion.Read(Site, EpicsRoute);
        Assert.Contains(Icons.ForConcept("Impact Map"), epicsHtml);
    }

    [Fact]
    public void GenerateAll_WithDeepGit_EpicAndStoryPagesShowCodeAreasWidget()
    {
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: true);

        var epicHtml = SiteRegion.Read(Site, Epic1Route);
        Assert.Contains("Code Areas Touched", epicHtml);
        Assert.Contains("code/src/Sample/Widget.cs.html", epicHtml);
        Assert.Contains("See the full impact map", epicHtml);

        var storyHtml = SiteRegion.Read(Site, Story11Route);
        Assert.Contains("Code Areas Touched", storyHtml);
        Assert.Contains("code/src/Sample/Widget.cs.html", storyHtml);

        // Negative case (Task 5): a story with its own artifact/page but zero commit attribution shows no
        // widget at all — absent, not an empty panel (NFR8). [Review][Patch]
        Assert.True(SiteRegion.Exists(Site, Story13Route), "Story 1.3 has its own artifact and should still get a page");
        Assert.DoesNotContain("Code Areas Touched", SiteRegion.Read(Site, Story13Route));
    }

    // ---- Without --deep-git: combined gate holds (hasEpics alone is NOT sufficient) ----

    [Fact]
    public void GenerateAll_WithoutDeepGit_OmitsImpactMapPageNavAndWidget()
    {
        // A real git repo exists, but --deep-git is OFF → no DeepGit.Commits → the whole surface is absent even
        // though hasEpics is true. This is the distinguishing assertion vs 21.1/21.2's bare hasEpics gate.
        Assert.True(TryCreateGitHistory("Story 1.1 foundation work"), "git CLI unavailable");
        GenerateSite(deepGit: false);

        Assert.False(SiteRegion.Exists(Site, ImpactMapRoute));

        // Positive control: the epics page (and its Delivery nav) still exist — proving hasEpics IS true here, so
        // the omission below is specifically the missing hasDeepAnalytics half, not a missing roster.
        var epicsHtml = SiteRegion.Read(Site, EpicsRoute);
        Assert.Contains("href=\"traceability.html\"", epicsHtml); // a bare-hasEpics Delivery sibling is present
        Assert.DoesNotContain("href=\"impact-map.html\"", epicsHtml);

        Assert.DoesNotContain("Code Areas Touched", SiteRegion.Read(Site, Epic1Route));
        Assert.DoesNotContain("Code Areas Touched", SiteRegion.Read(Site, Story11Route));
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
