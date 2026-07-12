using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.7 integration coverage: with <c>--spa</c> on, <see cref="SiteGenerator"/> emits the JSON+SPA
/// delivery form — a manifest, a bounded set of content chunks, the entry shell, and the client script — ALONGSIDE
/// the untouched static site. The bundle covers EVERY page the static run emits (AC #7), a long-tail page's content
/// region is the SAME C#-rendered content as the static page's (no re-render — AC #1), the form is opt-in (AC #3),
/// the static site is byte-identical (AC #5, also pinned by <see cref="SiteGeneratorAdapterTests"/>), the emit is
/// read-only outside the output root (AC #6), and the entry shell carries the no-JS fallback (AC #2). Follows the
/// temp-dir fixture style of <see cref="SiteGeneratorWebviewTests"/>.</summary>
public class SiteGeneratorSpaTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-spa-").FullName;

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

    public SiteGeneratorSpaTests()
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

    private ForgeOptions Options(bool spa) => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false, emitSpa: spa);

    private SiteGenerator GeneratedSite(bool spa = true)
    {
        var gen = new SiteGenerator(Options(spa));
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    /// <summary>Every static <c>.html</c> page the site emitted (the SPA's own entry shell excluded).</summary>
    private IReadOnlyList<string> StaticHtmlPages() =>
        Directory.EnumerateFiles(Site, "*.html", SearchOption.AllDirectories)
            .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)))
            .Where(p => p != SpaDelivery.EntryFileName)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void RenderSpaBundle_CoversEveryPageTheStaticSiteEmits()
    {
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        // AC #7: the bundle's page set is EXACTLY the static site's page set — every page, one-to-one, no more.
        var bundlePaths = bundle.Pages.Select(p => p.OutputRelativePath).OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(StaticHtmlPages(), bundlePaths);

        // Sanity: the family + long-tail split is genuinely exercised (dashboard/epics AND a long-tail page present).
        Assert.Contains("index.html", bundlePaths);
        Assert.Contains("epics/epic-1.html", bundlePaths);
        Assert.Contains("about.html", bundlePaths);
        Assert.Contains("requirements/fr1.html", bundlePaths);
        Assert.All(bundle.Pages, p => Assert.False(string.IsNullOrWhiteSpace(p.ContentHtml)));
    }

    [Fact]
    public void GenerateWithSpa_EmitsABoundedFewFiles_FarFewerThanPages()
    {
        GeneratedSite();

        var spaFiles = Directory.EnumerateFiles(Path.Combine(Site, SpaDelivery.ChunkDir)).ToList();
        // A manifest + a handful of content chunks — bounded and small, never one-JSON-per-page (AC #7).
        Assert.Contains(spaFiles, f => Path.GetFileName(f) == "manifest.json");
        var chunks = spaFiles.Count(f => Path.GetFileName(f).StartsWith("pages-", StringComparison.Ordinal));
        Assert.InRange(chunks, 1, 12);

        // The whole SPA footprint (entry shell + client script + manifest + chunks) is far smaller than the page
        // count — the file-count win the story exists for.
        var spaFootprint = 2 + spaFiles.Count; // app.html + specscribe-spa.js + spa/*
        Assert.True(spaFootprint < StaticHtmlPages().Count,
            $"SPA footprint {spaFootprint} should be far below the {StaticHtmlPages().Count} static pages");
    }

    [Fact]
    public void Manifest_AndChunks_RoundTrip_EveryPageResolvesToItsRegion()
    {
        GeneratedSite();

        using var manifestDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar))));
        var root = manifestDoc.RootElement;
        Assert.Equal("SpecScribe", root.GetProperty("siteTitle").GetString());
        Assert.Equal("index.html", root.GetProperty("entry").GetString());

        var pages = root.GetProperty("pages");
        // The manifest lists exactly the static page set.
        var manifestPaths = pages.EnumerateObject().Select(p => p.Name).OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(StaticHtmlPages(), manifestPaths);

        var chunkCache = new Dictionary<string, JsonElement>();
        foreach (var page in pages.EnumerateObject())
        {
            var chunkRel = page.Value.GetProperty("chunk").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(page.Value.GetProperty("title").GetString()));

            if (!chunkCache.TryGetValue(chunkRel, out var chunk))
            {
                var text = File.ReadAllText(Path.Combine(Site, chunkRel.Replace('/', Path.DirectorySeparatorChar)));
                chunk = JsonDocument.Parse(text).RootElement.Clone();
                chunkCache[chunkRel] = chunk;
            }
            // The page's content region round-trips out of its chunk and is a real, non-empty region.
            var region = chunk.GetProperty(page.Name).GetString();
            Assert.False(string.IsNullOrWhiteSpace(region));
            Assert.Contains("<nav class=\"site-nav\"", region);
        }
    }

    [Fact]
    public void LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock()
    {
        // AC #1: no re-render. A long-tail page's SPA content region carries the EXACT <main> block the static page
        // wrote (sliced from the render pipeline's own output, not a re-parse) — byte-for-byte.
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        foreach (var rel in new[] { "about.html", "requirements/fr1.html", "diagnostics.html" })
        {
            var staticMain = MainBlock(File.ReadAllText(Path.Combine(Site, rel.Replace('/', Path.DirectorySeparatorChar))));
            var region = bundle.Pages.Single(p => p.OutputRelativePath == rel).ContentHtml;
            Assert.Contains(staticMain, region);
            // …and the region also carries the page's own nav + breadcrumb chrome (the swappable region shape).
            Assert.Contains("<nav class=\"site-nav\"", region);
        }
    }

    [Fact]
    public void WithoutSpa_EmitsNoSpaFilesAtAll()
    {
        GeneratedSite(spa: false);

        // AC #3: opt-in. With the flag off, not one SPA artifact is written — the default generation is untouched.
        Assert.False(File.Exists(Path.Combine(Site, SpaDelivery.EntryFileName)));
        Assert.False(File.Exists(Path.Combine(Site, SpaDelivery.ScriptName)));
        Assert.False(Directory.Exists(Path.Combine(Site, SpaDelivery.ChunkDir)));
    }

    [Fact]
    public void EntryShell_InlinesTheDashboard_AndCarriesTheNoScriptFallback()
    {
        GeneratedSite();
        var app = File.ReadAllText(Path.Combine(Site, SpaDelivery.EntryFileName));

        // AC #2 / NFR6: the dashboard region is inlined (readable with JS off), a noscript link reaches the static
        // site, and the client script is loaded. The inlined nav links are ordinary relative links to static pages.
        Assert.Contains("stat-card", app);                                   // the real dashboard body, inlined
        Assert.Contains("<div id=\"spa-content\" data-path=\"index.html\">", app);
        Assert.Contains("<noscript>", app);
        Assert.Contains("<a href=\"index.html\">open the full static site</a>", app);
        Assert.Contains("<script src=\"" + SpaDelivery.ScriptName + "\"", app);
        Assert.Contains("href=\"epics.html\"", app);                         // nav link works with JS disabled
    }

    [Fact]
    public void SpaEmit_IsReadOnly_LeavesSourceArtifactsUntouched()
    {
        // AC #6: the full generate + SPA emit writes ONLY under the output root — no source planning artifact or
        // ADR (_bmad-output/**, docs/**) is created, deleted, or modified.
        var docsRoot = Path.Combine(_root, "docs");
        string[] SourceFiles() =>
            Directory.EnumerateFiles(Source, "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(docsRoot, "*", SearchOption.AllDirectories))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var before = SourceFiles().ToDictionary(p => p, File.GetLastWriteTimeUtc);

        GeneratedSite();

        var after = SourceFiles().ToDictionary(p => p, File.GetLastWriteTimeUtc);
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        Assert.All(before, kv => Assert.Equal(kv.Value, after[kv.Key]));
    }

    [Fact]
    public void RenderSpaBundle_BeforeAnyGeneration_ThrowsInsteadOfGuessing()
    {
        var gen = new SiteGenerator(Options(spa: true));
        Assert.Throws<InvalidOperationException>(() => gen.RenderSpaBundle());
    }

    /// <summary>The <c>&lt;main id="main-content"&gt;…&lt;/main&gt;</c> block of a full page — the landmark the SPA
    /// slices, recovered here to prove the region carries it byte-for-byte.</summary>
    private static string MainBlock(string fullHtml)
    {
        var open = fullHtml.IndexOf("<main id=\"main-content\"", StringComparison.Ordinal);
        var close = fullHtml.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open, "fixture page has a single <main id=\"main-content\"> landmark");
        return fullHtml[open..(close + "</main>".Length)];
    }
}
