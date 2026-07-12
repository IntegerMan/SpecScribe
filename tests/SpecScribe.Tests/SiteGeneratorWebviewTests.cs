using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.4 integration coverage: <see cref="SiteGenerator.RenderWebviewSurfaces"/> renders the FULL
/// webview-navigable surface set (dashboard, epics index, every epic page, every story page/placeholder) from the
/// same cached models the HTML site was generated from — reference-linkified, script-free, watch-parity-preserving,
/// and write-free (AC #6). Follows the temp-dir fixture style of <see cref="SiteGeneratorAdapterTests"/>.</summary>
public class SiteGeneratorWebviewTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-webview-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### NonFunctional Requirements

        NFR1: Generation degrades gracefully

        ### FR Coverage Map

        FR1: Epic 1 - rendering
        NFR1: Epic 1 - degradation

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ### Epic 2: Delivery

        Ship the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.
        """;

    // The blurb mentions Story 2.1 so the webview path's reference-linkify pass is observable (the mention must
    // become a link, exactly as the generated static page's does).
    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation. Builds toward Story 2.1.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        """;

    private const string Story21Md = """
        # Story 2.1: Delivery Story

        Status: done

        ## Story

        As a maintainer, I want delivery.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [x] Task 1: Ship it (AC: #1)
        """;

    public SiteGeneratorWebviewTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        Directory.CreateDirectory(Path.Combine(Source, "implementation-artifacts"));
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"), Story11Md);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "2-1-delivery.md"), Story21Md);
        File.WriteAllText(Path.Combine(Adrs, "0001-a-decision.md"), "# ADR 0001: A Decision\n\n**Status:** Accepted\n\nBody.\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private SiteGenerator GeneratedSite()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void RenderWebviewSurfaces_CoversAllFiveSurfaceFamilies()
    {
        var bundle = GeneratedSite().RenderWebviewSurfaces();

        // Dashboard first (the entry surface), then the whole epics family: index, per-epic pages, drafted story
        // pages AND the undrafted story's placeholder — the webview's complete navigable set.
        Assert.Equal("index.html", bundle.EntryPath);
        Assert.Equal("SpecScribe", bundle.SiteTitle);
        Assert.Equal(
            new[]
            {
                "epics.html",
                "epics/epic-1.html",
                "epics/epic-2.html",
                "epics/story-1-1.html",
                "epics/story-1-2.html",
                "epics/story-2-1.html",
                "index.html",
            },
            bundle.Surfaces.Select(s => s.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void EverySurface_CarriesTheChromeAndNoScript()
    {
        var bundle = GeneratedSite().RenderWebviewSurfaces();

        Assert.All(bundle.Surfaces, s =>
        {
            // Nav + (except home) breadcrumb travel with every content region, so a swap always refreshes the
            // active-nav highlight and drill trail; and no region carries a script (innerHTML swaps would never
            // execute one — anything script-shaped here would be dead code).
            Assert.Contains("<nav class=\"site-nav\"", s.ContentHtml);
            Assert.DoesNotContain("<script", s.ContentHtml);
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
        });

        var epicPage = bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/epic-1.html");
        Assert.Contains("<div class=\"breadcrumb\"", epicPage.ContentHtml);
        // Drill-down semantics: the epic page links both its drafted story and the placeholder.
        Assert.Contains("story-1-1.html", epicPage.ContentHtml);
        Assert.Contains("story-1-2.html", epicPage.ContentHtml);
    }

    [Fact]
    public void EntryDocument_IsTheDashboardWrappedInTheCspShell()
    {
        var bundle = GeneratedSite().RenderWebviewSurfaces();
        var dashboard = bundle.Surfaces.Single(s => s.OutputRelativePath == "index.html");

        Assert.Contains("Content-Security-Policy", bundle.EntryDocument);
        Assert.Contains("__CSP_SOURCE__", bundle.EntryDocument);
        Assert.Contains("__NONCE__", bundle.EntryDocument);
        // The embedded content is the linkified dashboard region itself — shell wrapping happens AFTER
        // linkification, so the linkifier never touched the shell's CSS/script text.
        Assert.Contains(dashboard.ContentHtml, bundle.EntryDocument);
        Assert.Contains("stat-card", bundle.EntryDocument);
    }

    [Fact]
    public void StoryContent_IsReferenceLinkified_LikeTheStaticPage()
    {
        var bundle = GeneratedSite().RenderWebviewSurfaces();
        var story11 = bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html");

        // The blurb's "Story 2.1" mention became a real link (the ApplyReferenceLinks pass the static site gets),
        // with the page's own relative prefix.
        Assert.Contains("story-2-1.html", story11.ContentHtml);
        // And the placeholder page renders for the undrafted story rather than a dead link target.
        var placeholder = bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-2.html");
        Assert.Contains("Story 1.2", placeholder.Title);
    }

    [Fact]
    public void RenderWebviewSurfaces_TracksWatchModeRegeneration()
    {
        var gen = GeneratedSite();
        Assert.Contains("Foundation Story", gen.RenderWebviewSurfaces()
            .Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html").Title);

        // A watch-mode epics edit followed by the incremental path the watcher uses: the webview bundle must
        // reflect the SAME refreshed models (the artifact/reference caches refresh through RenderEpicsPages).
        File.WriteAllText(
            Path.Combine(Source, "planning-artifacts", "epics.md"),
            EpicsMd.Replace("Foundation Story", "Renamed Story"));
        var ev = gen.RegenerateEpics();
        Assert.Equal(GenerationOutcome.Updated, ev.Outcome);

        var bundle = gen.RenderWebviewSurfaces();
        Assert.Contains("Renamed Story", bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html").Title);
    }

    [Fact]
    public void RenderWebviewSurfaces_WritesNothing()
    {
        var gen = GeneratedSite();
        var before = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, File.GetLastWriteTimeUtc);

        gen.RenderWebviewSurfaces();

        // AC #6 read-only: the webview render is a pure projection — no file appears, vanishes, or changes
        // anywhere under the fixture root (sources, ADRs, or the generated site alike).
        var after = Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, File.GetLastWriteTimeUtc);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));
    }

    [Fact]
    public void RenderWebviewSurfaces_BeforeAnyGeneration_ThrowsInsteadOfGuessing()
    {
        var gen = new SiteGenerator(Options());
        Assert.Throws<InvalidOperationException>(() => gen.RenderWebviewSurfaces());
    }

    [Fact]
    public void EverySurface_CarriesRepoRelativeSourcePath_PerFamily()
    {
        // Story 6.10 AC #1: each surface knows the repo-relative artifact it was rendered from. A story → its own
        // `.md`; an epic/index/placeholder → the epics file; the dashboard → null (it aggregates many).
        var bundle = GeneratedSite().RenderWebviewSurfaces();
        WebviewSurface S(string key) => bundle.Surfaces.Single(s => s.OutputRelativePath == key);

        Assert.Equal("_bmad-output/implementation-artifacts/1-1-foundation.md", S("epics/story-1-1.html").SourcePath);
        Assert.Equal("_bmad-output/implementation-artifacts/2-1-delivery.md", S("epics/story-2-1.html").SourcePath);
        // The undrafted story's placeholder reveals the epic that declares it (its source IS the epics file).
        Assert.Equal("_bmad-output/planning-artifacts/epics.md", S("epics/story-1-2.html").SourcePath);
        Assert.Equal("_bmad-output/planning-artifacts/epics.md", S("epics/epic-1.html").SourcePath);
        Assert.Equal("_bmad-output/planning-artifacts/epics.md", S("epics.html").SourcePath);
        // The dashboard has no single source artifact → null, so the reveal button hides on it.
        Assert.Null(S("index.html").SourcePath);

        // Forward-slashed on every platform (the host joins it to the workspace folder, like configuredOutputRoot).
        Assert.All(bundle.Surfaces.Where(s => s.SourcePath is not null), s => Assert.DoesNotContain('\\', s.SourcePath!));
    }

    [Fact]
    public void SerializePayload_EmitsSourcePathPerSurface_CamelCase_NullForDashboard()
    {
        var bundle = GeneratedSite().RenderWebviewSurfaces();
        var json = WebviewCommand.SerializePayload(bundle, "SpecScribeOutput");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var surfaces = doc.RootElement.GetProperty("surfaces");

        var story = surfaces.GetProperty("epics/story-1-1.html");
        Assert.True(story.TryGetProperty("sourcePath", out var src), "surface object carries camelCase `sourcePath`");
        Assert.Equal("_bmad-output/implementation-artifacts/1-1-foundation.md", src.GetString());

        // The dashboard's null source serializes as JSON null so the shim distinguishes "no source" from a value
        // (both hide the button) — the property is present and null, not absent-as-a-computed-value.
        var dashboard = surfaces.GetProperty("index.html");
        Assert.True(dashboard.TryGetProperty("sourcePath", out var dashSrc));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, dashSrc.ValueKind);
    }

    [Fact]
    public void EntryDocument_CarriesTheHiddenRevealButton_AndEmptyDataSource()
    {
        // The entry is the dashboard (no source): the "Open source" control is present as webview-only toolbar
        // chrome but paints hidden, and #specscribe-surface's data-source is empty until an update swaps in a
        // sourced surface. Toolbar chrome, never in the shared body (parity/golden unaffected — fact #6).
        var bundle = GeneratedSite().RenderWebviewSurfaces();

        Assert.Contains("ss-reveal-src-btn", bundle.EntryDocument);
        Assert.Contains("data-source=\"\"", bundle.EntryDocument);
        Assert.Contains("revealSource", bundle.EntryDocument);          // the bridge posts it
        Assert.Contains("data-code-path", bundle.EntryDocument);        // the AC #2 seam is present (inert)
    }

    [Fact]
    public void FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched()
    {
        // AC #6 at the seam that actually writes: the `specscribe webview` command runs a full GenerateAll()
        // (which DOES write the site) before RenderWebviewSurfaces(). Pin that this write pass never creates,
        // deletes, or modifies any source planning artifact (_bmad-output/**, docs/**) — the read-only guarantee
        // the panel makes about the user's project. (The generated site output is separate — the real command
        // redirects it to a temp scratch dir, so it never lands in the project.)
        var docsRoot = Path.Combine(_root, "docs");
        string[] SourceFiles() =>
            Directory.EnumerateFiles(Source, "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(docsRoot, "*", SearchOption.AllDirectories))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var before = SourceFiles().ToDictionary(p => p, File.GetLastWriteTimeUtc);

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        gen.RenderWebviewSurfaces();

        var after = SourceFiles().ToDictionary(p => p, File.GetLastWriteTimeUtc);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));
    }
}
