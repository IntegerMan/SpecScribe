using System.Security.Cryptography;
using System.Text.Json;
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
        // The ADR root's README becomes the adrs/index.html landing the nav links to. Without it the landing is
        // never generated (a pre-existing gap — the nav still links it), so the capture fixture mirrors the
        // real-world layout every documented repo uses. [spec-webview-doc-page-surfaces]
        File.WriteAllText(Path.Combine(Adrs, "README.md"),
            "# Decisions\n\n- [ADR 0001: A Decision](0001-a-decision.md)\n");
        // A generic planning doc (story artifacts render only as epics-family pages, not doc pages) — the
        // captured-surface tests need one real long-tail doc page. [spec-webview-doc-page-surfaces]
        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "prd.md"), "# PRD\n\nA requirement.\n");
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
    public void RenderWebviewSurfaces_StoryArtifactDeletedMidRender_DegradesThatStoryOnly()
    {
        // Simulates the sub-second window between GenerateAll() and RenderWebviewSurfaces() where a drafted
        // story's .md vanishes (e.g. a rename/delete mid-save). The bundle must still complete, with ONLY that
        // one story degraded to a placeholder — not the entire webview bundle aborting. [Deferred item, Story 6.4
        // review]
        var gen = GeneratedSite();
        File.Delete(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"));

        var bundle = gen.RenderWebviewSurfaces();

        var degraded = bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html");
        Assert.Contains("Story 1.1", degraded.Title);

        // The rest of the bundle is unaffected — Story 2.1's artifact still exists and still renders in full.
        var untouched = bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-2-1.html");
        Assert.Contains("It ships", untouched.ContentHtml);
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
    public void EscapedRepoRoot_DegradesSourcePathToNull_InsteadOfShippingAnAbsolutePath()
    {
        // Deferred-work regression (6-10-editor-artifact-bridges-reveal-source.md): a misconfigured RepoRoot
        // that doesn't actually contain the source artifacts must never ship an absolute (or ".."-escaping)
        // sourcePath — Path.GetRelativePath silently returns such input unchanged, which would otherwise
        // violate the "always repo-relative" contract the TS-side resolveWorkspacePath containment guard
        // depends on. The honest degrade is the same "button hidden" null every aggregate page already uses.
        var options = ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);
        var escaped = new ForgeOptions
        {
            RepoRoot = Path.Combine(_root, "not-the-real-root"),
            SourceRoot = options.SourceRoot,
            AdrSourceRoot = options.AdrSourceRoot,
            AdrSourceExplicit = options.AdrSourceExplicit,
            OutputRoot = options.OutputRoot,
            SiteTitle = options.SiteTitle,
            IncludeReadme = options.IncludeReadme,
            DeepGitAnalytics = options.DeepGitAnalytics,
        };
        Directory.CreateDirectory(escaped.RepoRoot);

        // CapturePages=true also exercises BuildCapturedSourceMap's Add — the third RepoRelative call site
        // (alongside _epicsSourcePath/storySourcePath below) — so all three degrade identically, not just the
        // two reached by the plain epics/story family.
        var gen = new SiteGenerator(escaped) { CapturePages = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var bundle = gen.RenderWebviewSurfaces();

        Assert.Null(bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html").SourcePath);
        Assert.Null(bundle.Surfaces.Single(s => s.OutputRelativePath == "epics.html").SourcePath);
        var docPage = bundle.Surfaces.Single(s => s.OutputRelativePath == "planning-artifacts/prd.html");
        Assert.Null(docPage.SourcePath);

        // The escape must degrade all the way through JSON serialization too, not just the in-memory property.
        var json = WebviewCommand.SerializePayload(bundle, "SpecScribeOutput");
        using var doc = JsonDocument.Parse(json);
        var storySurface = doc.RootElement.GetProperty("surfaces").GetProperty("epics/story-1-1.html");
        Assert.True(storySurface.TryGetProperty("sourcePath", out var src));
        Assert.Equal(JsonValueKind.Null, src.ValueKind);
    }

    [SkippableFact]
    public void RepoRelative_SymlinkedRepoRootAliasingTheArtifactsRealPath_StillComputesTheTrueRelativePath()
    {
        // 6-10-deferred-debt-cleanup: RepoRelative used to compare RepoRoot and each artifact's absolute path
        // purely lexically. Here RepoRoot is configured via a symlink ALIAS of the real fixture root while the
        // discovered story artifact sits at its real (non-aliased) absolute path — exactly the "RepoRoot
        // symlinked, or an artifact path traversing a symlink" mismatch the deferred item warned about. Before
        // the fix, GetRelativePath's lexical comparison between two differently-named sibling directories finds
        // no common literal prefix and climbs out with a "../"-prefixed string, which EscapesRepoRoot degrades
        // to null — an honest-but-wrong "reveal button hidden" false negative on a perfectly valid in-repo
        // artifact, not the true relative path the TS-side real-path-based containment guard would recognize.
        var options = Options();
        var link = Path.Combine(Path.GetDirectoryName(_root)!, "link-" + Path.GetFileName(_root));
        try
        {
            Directory.CreateSymbolicLink(link, _root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SkipException("Creating a symbolic link isn't permitted on this host (e.g. non-elevated Windows without Developer Mode) — skipped, not failed.");
        }

        try
        {
            var aliased = new ForgeOptions
            {
                RepoRoot = link,
                SourceRoot = options.SourceRoot,
                AdrSourceRoot = options.AdrSourceRoot,
                AdrSourceExplicit = options.AdrSourceExplicit,
                OutputRoot = options.OutputRoot,
                SiteTitle = options.SiteTitle,
                IncludeReadme = options.IncludeReadme,
                DeepGitAnalytics = options.DeepGitAnalytics,
            };

            var gen = new SiteGenerator(aliased);
            Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
            var bundle = gen.RenderWebviewSurfaces();

            Assert.Equal(
                "_bmad-output/implementation-artifacts/1-1-foundation.md",
                bundle.Surfaces.Single(s => s.OutputRelativePath == "epics/story-1-1.html").SourcePath);
        }
        finally
        {
            Directory.Delete(link);
        }
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
    public void SerializePayload_EmitsResolvedWatchRoots_CamelCase()
    {
        // Story 6.11: the payload carries the resolved watch roots the shim builds its file watchers from — all
        // camelCase, forward-slashed, additive (no HTML change). The values here are the pure resolvers' output.
        var bundle = GeneratedSite().RenderWebviewSurfaces();
        var json = WebviewCommand.SerializePayload(
            bundle, "SpecScribeOutput", "_bmad-output", "docs/adrs", "../..");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("_bmad-output", root.GetProperty("sourceRoot").GetString());
        Assert.Equal("docs/adrs", root.GetProperty("adrRoot").GetString());
        // The repo-root offset rides verbatim so the shim resolves the absolute repo root on a subdir-open.
        Assert.Equal("../..", root.GetProperty("repoRoot").GetString());
    }

    [Fact]
    public void EntryDocument_CarriesTheHiddenRevealButton_AndEmptyDataSource()
    {
        // The entry is the dashboard (no source): the "Open source" control is present as webview-only toolbar
        // chrome but paints hidden, and #specscribe-surface's data-source is empty until an update swaps in a
        // sourced surface. Toolbar chrome, never in the shared body (parity/golden unaffected — fact #6).
        var bundle = GeneratedSite().RenderWebviewSurfaces();

        Assert.Contains("ss-reveal-src-btn", bundle.EntryDocument);
        // The actual "paints hidden" claim in the test name — a class-name substring match alone would not catch
        // a regression that drops the `hidden` attribute from the button markup.
        Assert.Contains("class=\"ss-reveal-src-btn\" title=\"Open the markdown file this view was rendered from (read-only)\" hidden>", bundle.EntryDocument);
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
        // Content hash, not just mtime: a write that preserves LastWriteTimeUtc (e.g. a same-tick rewrite on a
        // coarse-resolution filesystem) would pass an mtime-only guard undetected. Snapshot BOTH mtime and hash
        // from the SAME single file listing per phase (not two separate SourceFiles() calls) so the two
        // dictionaries can never disagree on which files exist. [deferred-work, review patch]
        static (DateTime Time, string Hash) Snapshot(string path) =>
            (File.GetLastWriteTimeUtc(path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var before = SourceFiles().ToDictionary(p => p, Snapshot);

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        gen.RenderWebviewSurfaces();

        var after = SourceFiles().ToDictionary(p => p, Snapshot);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));
    }

    // ===== spec-webview-doc-page-surfaces: long-tail captured surfaces =======================================

    private SiteGenerator GeneratedSiteWithCapture()
    {
        var gen = new SiteGenerator(Options()) { CapturePages = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    [Fact]
    public void CapturePages_AddsLongTailPages_AsNavigableSurfaces()
    {
        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();
        // ORDINAL (case-sensitive) deliberately: the shim's lookup is a case-sensitive JS object keyed by the
        // serialized surface paths, so an assertion with a looser comparer would pass cases the runtime rejects.
        var keys = bundle.Surfaces.Select(s => s.OutputRelativePath).ToHashSet(StringComparer.Ordinal);

        // The owner's dead-end set becomes live in-panel targets: the ADR landing AND an ADR detail page,
        // requirements, about, and the story artifact's generic doc rendering. Family surfaces still present.
        Assert.Contains(SiteNav.AdrsLandingOutputPath, keys);
        Assert.Contains(keys, k => k.StartsWith("adrs/", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(k, SiteNav.AdrsLandingOutputPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(SiteNav.RequirementsOutputPath, keys);
        Assert.Contains(SiteNav.AboutOutputPath, keys);
        Assert.Contains("planning-artifacts/prd.html", keys);
        Assert.Contains("index.html", keys);
        Assert.Contains("epics.html", keys);
    }

    [Fact]
    public void CapturePages_IncludesCodeMapAsACapturedSurface()
    {
        // Story 7.9, AC #3 "HTML + webview + SPA stay coherent": code-map.html is a WriteOutput-synthesized page
        // (like the other long-tail pages), NOT one of the deliberate exclusions (code PAGES / commit-day pages /
        // commit/ detail pages — those scale with the target repo's history, unlike the treemap itself), so it
        // IS captured for the webview/SPA surface set and must render the new file-type dimension coherently there.
        Directory.CreateDirectory(Path.Combine(_root, "src", "Lib"));
        File.WriteAllText(Path.Combine(_root, "src", "Lib", "Widget.cs"), "namespace Lib;\npublic class Widget { }\n");

        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();
        var codeMap = bundle.Surfaces.SingleOrDefault(s => s.OutputRelativePath == "code-map.html");

        Assert.NotNull(codeMap);
        Assert.Contains("codemap-cell type-csharp", codeMap!.ContentHtml);
        Assert.Contains("codemap-legend-discrete", codeMap.ContentHtml);
        Assert.Contains("<nav class=\"site-nav\"", codeMap.ContentHtml); // carries the shared chrome like every surface
        Assert.DoesNotContain("<script", codeMap.ContentHtml);          // captured regions never carry the scoped enhancement
    }

    [Fact]
    public void CapturePages_IncludesTraceabilityAsACapturedSurface()
    {
        // Story 21.1, AC #1 "SPA/webview coherence": traceability.html is a WriteOutput-synthesized page like
        // every other long-tail insight page, so it IS captured for the webview/SPA surface set. Mirrors Story
        // 7.9's code-map.html coherence assertion.
        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();
        var traceability = bundle.Surfaces.SingleOrDefault(s => s.OutputRelativePath == "traceability.html");

        Assert.NotNull(traceability);
        Assert.Contains("class=\"trace-matrix\"", traceability!.ContentHtml);
        Assert.Contains("<nav class=\"site-nav\"", traceability.ContentHtml); // carries the shared chrome like every surface
        Assert.DoesNotContain("<script", traceability.ContentHtml);          // captured regions never carry the scoped enhancement
    }

    [Fact]
    public void CapturePages_EveryEntryNavLink_ResolvesToABundledSurface()
    {
        // The user-facing acceptance: no header nav link may dead-end. Every .html href in the ENTRY surface's
        // nav region (output-relative from index.html, so no prefix resolution needed) must be a bundled key.
        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();
        var keys = bundle.Surfaces.Select(s => s.OutputRelativePath).ToHashSet(StringComparer.Ordinal);
        var entry = bundle.Surfaces.Single(s => s.OutputRelativePath == "index.html");
        var navEnd = entry.ContentHtml.IndexOf("</nav>", StringComparison.Ordinal);
        Assert.True(navEnd > 0, "entry surface carries the nav region");
        var nav = entry.ContentHtml[..navEnd];

        var hrefs = System.Text.RegularExpressions.Regex.Matches(nav, "href=\"(?<h>[^\"#]+\\.html)")
            .Select(m => m.Groups["h"].Value).Distinct().ToList();
        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, h => Assert.Contains(h, keys));
    }

    [Fact]
    public void CapturePages_SubdirectorySurfaceLinks_ResolveToBundledKeys()
    {
        // The drill scenario that motivated the feature: a SUBDIRECTORY surface (adrs/index.html) emits
        // ../-prefixed nav hrefs and bare record hrefs; resolved against its own base — the same dot-segment
        // collapse the bridge script performs — every one must land on a bundled key (case-SENSITIVE, like the
        // shim's JS-object lookup).
        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();
        var keys = bundle.Surfaces.Select(s => s.OutputRelativePath).ToHashSet(StringComparer.Ordinal);
        var adrIndex = bundle.Surfaces.Single(s => s.OutputRelativePath == SiteNav.AdrsLandingOutputPath);

        var hrefs = System.Text.RegularExpressions.Regex.Matches(adrIndex.ContentHtml, "href=\"(?<h>[^\"#]+\\.html)")
            .Select(m => m.Groups["h"].Value).Distinct().ToList();
        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, h => Assert.Contains(ResolveLikeBridge(h, SiteNav.AdrsLandingOutputPath), keys));
        // And at least one resolved href is the ADR record itself — the drill the owner clicked for.
        Assert.Contains(hrefs, h =>
            ResolveLikeBridge(h, SiteNav.AdrsLandingOutputPath).StartsWith("adrs/", StringComparison.Ordinal)
            && ResolveLikeBridge(h, SiteNav.AdrsLandingOutputPath) != SiteNav.AdrsLandingOutputPath);
    }

    /// <summary>The bridge script's relative-href resolution, mirrored: join to the base surface's directory and
    /// collapse <c>.</c>/<c>..</c> segments (see the <c>resolve()</c> function in WebviewRenderAdapter's
    /// DocumentTemplate).</summary>
    private static string ResolveLikeBridge(string href, string basePath)
    {
        var baseDir = basePath.Contains('/') ? basePath[..(basePath.LastIndexOf('/') + 1)] : string.Empty;
        var parts = new List<string>();
        foreach (var segment in (baseDir + href).Split('/'))
        {
            if (segment == "." || segment.Length == 0) continue;
            if (segment == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(segment);
        }
        return string.Join('/', parts);
    }

    [Fact]
    public void AdrLandingIsSynthesized_WhenTheAdrRootHasNoReadme()
    {
        // Owner decision (2026-07-12): nav links adrs/index.html whenever records exist, so a README-less ADR
        // root must synthesize a landing (previously a 404 in the static site AND a webview dead-end). The
        // landing lists each record as a drill link. Repos WITH a README are byte-identical (golden unaffected).
        File.Delete(Path.Combine(Adrs, "README.md"));

        var gen = GeneratedSiteWithCapture();
        var bundle = gen.RenderWebviewSurfaces();

        Assert.True(File.Exists(Path.Combine(Site, "adrs", "index.html")), "synthesized landing written to the site");
        var landing = bundle.Surfaces.Single(s => s.OutputRelativePath == SiteNav.AdrsLandingOutputPath);
        Assert.Contains("0001-a-decision.html", landing.ContentHtml);
        Assert.Contains("ADR 0001: A Decision", landing.ContentHtml);
    }

    [Fact]
    public void AdrLandingIsSynthesized_WhenTheRootReadmeExistsButFailsToRender()
    {
        // [Review][Patch] A README that EXISTS but can't be read/parsed (locked file, transient I/O error) must
        // not suppress the fallback synthesis the way filename-match-alone previously did — otherwise the nav's
        // adrs/index.html link stays unwritten and 404s despite the README being present on disk.
        var readmePath = Path.Combine(Adrs, "README.md");
        File.WriteAllText(readmePath, "# Decisions\n\n- [ADR 0001: A Decision](0001-a-decision.md)\n");

        using (new FileStream(readmePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var gen = new SiteGenerator(Options());
            var events = gen.GenerateAll();

            Assert.Contains(events, e => e.Outcome == GenerationOutcome.Error
                && e.RelativePath.Contains("README", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(Site, "adrs", "index.html")),
                "synthesized landing written despite the README failing to render");
            var landingHtml = File.ReadAllText(Path.Combine(Site, "adrs", "index.html"));
            Assert.Contains("0001-a-decision.html", landingHtml);
        }
    }

    [Fact]
    public void AdrLandingSynthesis_DoesNotClobberAnAdrFileThatAlreadyOccupiesTheLandingSlot()
    {
        // [Review][Patch] A non-record ADR file literally named "index.md" (no root README present) renders to
        // the SAME output slot ("adrs/index.html") the synthesized landing would use. The real page must win —
        // synthesis must not silently overwrite it.
        File.Delete(Path.Combine(Adrs, "README.md"));
        File.WriteAllText(Path.Combine(Adrs, "index.md"), "# Decisions Overview\n\nHand-authored index.\n");

        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

        var landingHtml = File.ReadAllText(Path.Combine(Site, "adrs", "index.html"));
        Assert.Contains("Decisions Overview", landingHtml);
        Assert.Contains("Hand-authored index.", landingHtml);
    }

    [Fact]
    public void RepoReadmeSurface_CarriesItsSourcePath()
    {
        // readme.html renders straight from the repo README (never via _docs) — the spec's first-named page
        // must still carry its reveal-source mapping.
        File.WriteAllText(Path.Combine(_root, "README.md"), "# The Repo\n\nHello.\n");
        var options = ForgeOptions.Resolve(
            source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: true);
        var gen = new SiteGenerator(options) { CapturePages = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var bundle = gen.RenderWebviewSurfaces();

        var readme = bundle.Surfaces.Single(s => s.OutputRelativePath == SiteNav.ReadmeOutputPath);
        Assert.Equal("README.md", readme.SourcePath);
    }

    [Fact]
    public void CapturePages_ExcludesCodePages_FromTheBundle()
    {
        // A cited real source file produces a code page in the SITE, but the webview bundle deliberately omits
        // code/** — the tree scales with the TARGET repo (unbounded payload), and in-editor the 7.2 citations
        // open the real file via revealSource instead. The shim's toast stays the honest fallback.
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "Widget.cs"), "public class Widget { }\n");
        File.AppendAllText(Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            "\n[Source: `src/Widget.cs`]\n");

        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();

        Assert.True(File.Exists(Path.Combine(Site, "code", "src", "Widget.cs.html")),
            "the code page itself is still generated into the site");
        Assert.DoesNotContain(bundle.Surfaces,
            s => s.OutputRelativePath.StartsWith("code/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CapturedSurface_CarriesChromeRegion_AndRepoRelativeSourcePath()
    {
        var bundle = GeneratedSiteWithCapture().RenderWebviewSurfaces();

        // Region shape = the family shape: fresh per-page nav leads, the page's own <main> follows — so a swap
        // refreshes active-nav/breadcrumb exactly like a family surface (the 6.7 extraction guarantees this).
        var adrIndex = bundle.Surfaces.Single(s => s.OutputRelativePath == SiteNav.AdrsLandingOutputPath);
        Assert.StartsWith("<nav class=\"site-nav\"", adrIndex.ContentHtml);
        Assert.Contains("<main id=\"main-content\"", adrIndex.ContentHtml);
        Assert.False(string.IsNullOrWhiteSpace(adrIndex.Title));

        // Reveal-source: an ADR detail page maps to its repo-relative .md (docs/adrs/...); a generic doc page
        // maps to its _bmad-output source — same one convention as story surfaces (Story 6.10). Aggregates
        // (requirements/about) carry none → button hidden, like the dashboard.
        var adrDetail = bundle.Surfaces.Single(s =>
            s.OutputRelativePath.StartsWith("adrs/", StringComparison.OrdinalIgnoreCase)
            && s.OutputRelativePath != SiteNav.AdrsLandingOutputPath);
        Assert.Equal("docs/adrs/0001-a-decision.md", adrDetail.SourcePath);
        var docPage = bundle.Surfaces.Single(s => s.OutputRelativePath == "planning-artifacts/prd.html");
        Assert.Equal("_bmad-output/planning-artifacts/prd.md", docPage.SourcePath);
        var about = bundle.Surfaces.Single(s => s.OutputRelativePath == SiteNav.AboutOutputPath);
        Assert.Null(about.SourcePath);
    }

    [Fact]
    public void Webview_DegradesToAValidBundleForANonBmadWorkspace()
    {
        // Goal A: the extension spawns `webview` in ANY folder. A plain workspace — no `_bmad-output`, just a README
        // and a source file — must still produce a valid, error-free bundle (README + Code Map) with an EMPTY outline,
        // never crash. Uses tolerant resolution (the webview command's path) anchored on the non-bmad dir, and mirrors
        // the command's CapturePages=true. [spec-vscode-any-workspace-and-processing-indicators]
        var plain = Directory.CreateTempSubdirectory("specscribe-nonbmad-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(plain, "README.md"), "# Plain Repo\n\nJust code, no BMad.\n");
            File.WriteAllText(Path.Combine(plain, "Program.cs"), "public static class P { public static void M() { } }\n");

            var options = ForgeOptions.Resolve(
                startDirectory: plain, output: Path.Combine(plain, "site"), includeReadme: true, requireSource: false);
            Assert.False(Directory.Exists(options.SourceRoot)); // no _bmad-output — the source root is absent

            var gen = new SiteGenerator(options) { CapturePages = true };
            Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

            var bundle = gen.RenderWebviewSurfaces();
            Assert.Equal("index.html", bundle.EntryPath);
            Assert.NotEmpty(bundle.Surfaces);                 // at minimum the dashboard renders
            Assert.Empty(bundle.Outline.Epics);               // no epics — the "no epics" outline state, not an error
            Assert.Equal(0, bundle.Outline.Summary.Total);
            // The non-bmad value is real: the README page and the code map both come through as navigable surfaces.
            Assert.Contains(bundle.Surfaces, s => s.OutputRelativePath == SiteNav.ReadmeOutputPath);
            Assert.Contains(bundle.Surfaces, s => s.OutputRelativePath == SiteNav.CodeMapOutputPath);
        }
        finally
        {
            try { Directory.Delete(plain, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Webview_DegradesToAValidBundleForATrulyEmptyWorkspace()
    {
        // The I/O matrix's most degenerate row: an empty folder — no README, no code, no git, no `_bmad-output`.
        // The webview path must still produce a valid, error-free bundle (the dashboard always renders) with an
        // empty outline, never a crash or a blank entry. [spec-vscode-any-workspace-and-processing-indicators]
        var empty = Directory.CreateTempSubdirectory("specscribe-empty-").FullName;
        try
        {
            var options = ForgeOptions.Resolve(
                startDirectory: empty, output: Path.Combine(empty, "site"), includeReadme: true, requireSource: false);

            var gen = new SiteGenerator(options) { CapturePages = true };
            Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);

            var bundle = gen.RenderWebviewSurfaces();
            Assert.Equal("index.html", bundle.EntryPath);
            Assert.NotEmpty(bundle.Surfaces);                 // the dashboard entry surface always renders
            Assert.False(string.IsNullOrWhiteSpace(bundle.EntryDocument));
            Assert.Empty(bundle.Outline.Epics);
            Assert.Equal(0, bundle.Outline.Summary.Total);
        }
        finally
        {
            try { Directory.Delete(empty, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // ===== Deferred item, Story 6.4 review: scoped re-render payload-shape parity ==================================

    [Fact]
    public void SerializePayload_AfterIncrementalRegen_HasTheSameShapeAsTheInitialRender()
    {
        // The persistent `--serve` mode streams a payload after an INCREMENTAL RegenerateEpics() (not a fresh
        // GenerateAll()) on every debounced live edit — both go through the exact same SerializePayload call as
        // the one-shot path, so the extension's parser needs no branch on which mode produced a given line. This
        // pins that the two payloads share the same top-level JSON shape, and that the incremental path actually
        // carries the live edit forward (not a stale snapshot). [Deferred item, Story 6.4 review]
        var gen = GeneratedSite();
        var initialPayload = WebviewCommand.SerializePayload(gen.RenderWebviewSurfaces(), "SpecScribeOutput");

        File.WriteAllText(
            Path.Combine(Source, "implementation-artifacts", "1-1-foundation.md"),
            Story11Md.Replace("I want the foundation.", "I want the foundation, freshly edited."));
        Assert.Equal(GenerationOutcome.Updated, gen.RegenerateEpics().Outcome);
        var pushedPayload = WebviewCommand.SerializePayload(gen.RenderWebviewSurfaces(), "SpecScribeOutput");

        using var initialDoc = JsonDocument.Parse(initialPayload);
        using var pushedDoc = JsonDocument.Parse(pushedPayload);
        var initialKeys = initialDoc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var pushedKeys = pushedDoc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(initialKeys, pushedKeys);

        // The incremental regen actually reached the pushed payload — not a stale copy of the initial one.
        Assert.Contains("freshly edited", pushedPayload);
        Assert.DoesNotContain("freshly edited", initialPayload);
    }
}
