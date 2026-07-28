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
    public void HierarchyExplorerIsland_SurvivesSpaContentRegionCapture()
    {
        // Parity: the dashboard's explorer root marker + inline JSON island are mounted INSIDE
        // <main id="main-content">, so the SPA content-region slice must carry them byte-for-byte.
        // SCOPE OF THIS TEST: it pins the MARKUP surviving the capture — nothing more. Whether the client actually
        // re-enhances that markup after an innerHTML swap is a runtime concern this SSR test cannot observe; the
        // swap fires `specscribe:content-swapped` and specscribe.js re-runs `initHierarchyExplorers` against the
        // fresh region. Do not read a green here as proof of live SPA parity.
        // [comment corrected by the Story 20.2 review — it previously claimed the behavior this cannot test]
        //
        // STORY 20.7: retargeted from Story 20.2's `sunburst-explorer-data` island and its `data-node-id` join
        // hooks — both retired with the SVG — onto the component's island. The question is unchanged and now
        // matters MORE: with the SVG gone there is no second copy of this information on the page, so an island or
        // twin lost at the capture boundary is information lost outright. Up to five distinct instances exist
        // across the site and two pages can be in one SPA session, so the ids are checked as distinct too.
        var gen = GeneratedSite();

        var staticIndex = File.ReadAllText(Path.Combine(Site, "index.html"));
        Assert.Contains("data-explorer", staticIndex);
        Assert.Contains("id=\"dashboard-hierarchy-data\"", staticIndex);
        // 20.2's island and its join hooks are gone from the shipped page, not merely unread.
        Assert.DoesNotContain("id=\"sunburst-explorer-data\"", staticIndex);
        Assert.DoesNotContain("data-node-id=", staticIndex);

        var spaIndex = gen.RenderSpaBundle().Pages.Single(p => p.OutputRelativePath == "index.html").ContentHtml;
        Assert.Contains("data-explorer", spaIndex);
        Assert.Contains("id=\"dashboard-hierarchy-data\"", spaIndex);
        // The island is INSIDE the captured <main> region (not stranded before it).
        var mainStart = spaIndex.IndexOf("<main id=\"main-content\"", StringComparison.Ordinal);
        Assert.True(mainStart >= 0 && spaIndex.IndexOf("id=\"dashboard-hierarchy-data\"", StringComparison.Ordinal) > mainStart);

        // The epics index carries its OWN instance, with its own ids — a collision would make one of the two
        // unmountable in an SPA session that visited both.
        var spaEpics = gen.RenderSpaBundle().Pages.SingleOrDefault(p => p.OutputRelativePath == "epics/index.html");
        if (spaEpics is not null)
        {
            Assert.Contains("id=\"epics-index-hierarchy-data\"", spaEpics.ContentHtml);
            Assert.DoesNotContain("id=\"dashboard-hierarchy-data\"", spaEpics.ContentHtml);
        }

        // Story 20.5: the Hierarchy Explorer's own island and its TEXT TWIN ride the same capture. The twin
        // matters more than the island here — under ADR 0013 it is the no-JS contract, and an SPA visitor whose
        // scripting is blocked mid-session must get the same server-rendered truth as a static-site one.
        // SAME SCOPE CAVEAT as above: this pins markup surviving the capture, never live SPA re-enhancement.
        // Story 20.6: keyed on the twin's stable IDENTITY (`id="{domId}-twin"`), not on its wrapper element. The
        // dashboard now presents its twin as `<section class="ss-hierarchy-twin sr-only">` rather than `<details>`
        // (owner D4 — the page already carries the SunburstCompanionList tile grid and the 20.3 rail, so a third
        // VISIBLE listing would be on-screen duplication), and this assertion previously pinned the `<details>`
        // literal. What this test is actually about is that the twin SURVIVES the capture and lands inside <main>;
        // which wrapper carries it is a per-surface presentation choice the component owns. The completeness
        // contract itself does not vary by presentation — HierarchyExplorerTests
        // .TwinDisplay_ChangesPresentationOnly_TheListingIsByteIdenticalInBothModes pins that separately.
        const string twinId = "id=\"dashboard-hierarchy-twin\"";
        foreach (var html in new[] { staticIndex, spaIndex })
        {
            Assert.Contains("class=\"ss-hierarchy-data\"", html);
            Assert.Contains("class=\"ss-hierarchy-twin", html);
            Assert.Contains(twinId, html);
            // The twin is the no-JS contract, so its LISTING — not just its wrapper — must ride the capture.
            Assert.Contains("<ul class=\"ss-hierarchy-twin-list\">", html);
            Assert.Contains("data-hierarchy", html);
        }
        Assert.True(spaIndex.IndexOf("class=\"ss-hierarchy-data\"", StringComparison.Ordinal) > mainStart);
        Assert.True(spaIndex.IndexOf(twinId, StringComparison.Ordinal) > mainStart);

        // STORY 20.9: the count went from five instances site-wide to TEN, and four of them are on ONE page. A
        // collision there would not be subtle-in-theory - `code-map.html`'s four panels differ only by filter, so
        // two sharing a DomId would leave one permanently unmountable and two sharing a HashKey would have them
        // fighting over the fragment. Extended here rather than in a parallel test, per Task 5.3.
        var spaCodeMap = gen.RenderSpaBundle().Pages.SingleOrDefault(p => p.OutputRelativePath == "code-map.html");
        if (spaCodeMap is not null)
        {
            foreach (var key in new[] { "full", "no-spec", "no-tests", "no-spec-no-tests" })
            {
                Assert.Contains($"id=\"codemap-{key}-data\"", spaCodeMap.ContentHtml);
            }
            // The per-variant file table is this surface's twin (Story 20.6 D1), so IT is what has to ride the
            // capture - the component emits no generic twin here at all.
            Assert.Contains("class=\"codemap-table\"", spaCodeMap.ContentHtml);
            Assert.DoesNotContain("ss-hierarchy-twin", spaCodeMap.ContentHtml);
        }

        // Every DomId across the WHOLE bundle is distinct: an SPA session can visit any two of these pages, and
        // the component keys its host, island, selector radios and twin off that one id.
        var allDomIds = gen.RenderSpaBundle().Pages
            .SelectMany(p => System.Text.RegularExpressions.Regex.Matches(p.ContentHtml, "id=\"(?<d>[a-z0-9-]+)-data\" ?>")
                .Select(m => m.Groups["d"].Value))
            .ToList();
        Assert.Equal(allDomIds.Count, allDomIds.Distinct(StringComparer.Ordinal).Count());

        // The re-init seam every one of them depends on after an innerHTML swap. SAME SCOPE CAVEAT as above: this
        // pins that the seam is WIRED in the shipped asset, never that a live swap re-enhanced anything.
        var js = File.ReadAllText(Path.Combine(RepoSourceRoot(), "assets", "specscribe.js"));
        Assert.Contains("specscribe:content-swapped", js);
        Assert.Contains("initHierarchyExplorers(e && e.detail ? e.detail.root : document)", js);
    }

    [Fact]
    public void HierarchyEngineBundle_ShipsOnlyWhereAHierarchyChartWasRendered()
    {
        // The vendored plotly bundle is 1.2 MB. It must land in the output ONLY because a page actually hosts a
        // hierarchy chart — the same discipline that keeps prism.js off a site with no code pages — and the
        // <script> tag must appear on exactly that page and nowhere else. Unconditional emission would put it into
        // every fixture and every code-free site.
        GeneratedSite();

        var bundle = Path.Combine(Site, ForgeOptions.HierarchyEngineScriptName);
        var index = File.ReadAllText(Path.Combine(Site, "index.html"));
        var hostsChart = index.Contains("data-hierarchy", StringComparison.Ordinal);

        Assert.Equal(hostsChart, File.Exists(bundle));

        // STORY 20.7 generalized this from a hard-coded page list to the INVARIANT that list was standing in for,
        // which is what the previous comment here anticipated ("Story 20.7 converts the other six call sites").
        // The engine tag must appear on exactly the pages that host a chart — no more, and crucially NO FEWER.
        //
        // The "no fewer" half is not hypothetical. Story 20.7's four new instances all shipped their island and
        // their twin but mounted NOTHING, because `EpicsTemplater` builds its own AssetManifests and none of them
        // set `HierarchyEngineNeeded`. Every layer below the browser was green: the page rendered, the payload was
        // correct, the twin stood in, and the chart simply never arrived. A hard-coded `["index.html"]` could not
        // have caught it — it would have gone on passing while four surfaces were silently chartless.
        var tag = $"{ForgeOptions.HierarchyEngineScriptName}\"></script>";
        var pages = Directory.EnumerateFiles(Site, "*.html", SearchOption.AllDirectories)
            .Select(p => (Path: PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p)), Html: File.ReadAllText(p)))
            .Where(p => p.Path != SpaDelivery.EntryFileName)
            .ToList();

        var pagesWithTag = pages.Where(p => p.Html.Contains(tag, StringComparison.Ordinal))
            .Select(p => p.Path).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var pagesHostingChart = pages.Where(p => HierarchyExplorer.ContainsHost(p.Html))
            .Select(p => p.Path).OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Equal(pagesHostingChart, pagesWithTag);

        if (hostsChart)
        {
            // Guard against a vacuous green: this fixture HAS epics, so the converted family really is exercised.
            Assert.Contains("index.html", pagesWithTag);
            Assert.Contains("epics.html", pagesWithTag);
            Assert.Contains(pagesWithTag, p => p.StartsWith("epics/epic-", StringComparison.Ordinal));
            Assert.Contains(pagesWithTag, p => p.StartsWith("epics/story-", StringComparison.Ordinal));
            // And a page with no chart still ships no 1.2 MB bundle reference.
            Assert.DoesNotContain("about.html", pagesWithTag);
        }
        else
        {
            Assert.Empty(pagesWithTag);
        }
    }

    [Fact]
    public void RelatedWorkPane_SurvivesSpaContentRegionCapture()
    {
        // Story 20.3 AC #2 / parity: the pane is the NO-JS delivery of the relationship data, so it must reach the
        // SPA body byte-for-byte — an SPA visitor with scripting blocked mid-session gets the same server-rendered
        // truth as a static-site one.
        // SCOPE OF THIS TEST, stated plainly (the Story 20.2 review's lesson): it pins MARKUP surviving the capture.
        // Whether the client re-reveals the selected scope after an innerHTML swap is a runtime concern SSR cannot
        // observe — the swap fires `specscribe:content-swapped` and specscribe.js re-syncs the fresh pane against
        // the explorer's published `data-sb-scope`. Do not read a green here as proof of live SPA parity.
        var gen = GeneratedSite();

        var staticIndex = File.ReadAllText(Path.Combine(Site, "index.html"));
        var spaIndex = gen.RenderSpaBundle().Pages.Single(p => p.OutputRelativePath == "index.html").ContentHtml;

        // The fixture is only guaranteed to carry a pane when it has work-graph signal; when it does, both forms
        // must agree, and when it does not, BOTH must omit it (NFR8 — absent data, absent surface, on every host).
        var inStatic = staticIndex.Contains(RelatedWorkTemplater.PaneAttribute, StringComparison.Ordinal);
        var inSpa = spaIndex.Contains(RelatedWorkTemplater.PaneAttribute, StringComparison.Ordinal);
        Assert.Equal(inStatic, inSpa);

        if (!inSpa) return;
        Assert.Contains("data-related-node=", spaIndex);
        Assert.Contains("No related work items for this selection.", spaIndex);
        // Story 20.8 extended what the pane CARRIES (a per-story command disclosure, a deferred-children list, and
        // the `~summary` redirect published as `data-related-alias`), so the parity claim has to move with it —
        // extended here rather than given a parallel case, which would let the two drift. Each is asserted as
        // "present in the SPA body iff present in the static one", so this stays fixture-independent: a fixture
        // with no story commands proves nothing, but it also cannot pass by accident.
        foreach (var marker in new[] { "related-card-commands", "related-card-children", "data-related-alias=" })
            Assert.Equal(
                staticIndex.Contains(marker, StringComparison.Ordinal),
                spaIndex.Contains(marker, StringComparison.Ordinal));
        // Inside the captured <main> region, not stranded before it.
        var mainStart = spaIndex.IndexOf("<main id=\"main-content\"", StringComparison.Ordinal);
        Assert.True(mainStart >= 0
            && spaIndex.IndexOf(RelatedWorkTemplater.PaneAttribute, StringComparison.Ordinal) > mainStart);
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
    public void Manifest_CarriesTheNavGraphAndPerPageBreadcrumbDrillData()
    {
        // Story 6.7 review: the manifest previously carried only path->title/chunk. It must ALSO carry the top nav
        // graph plus, per page, the breadcrumb trail and drill parent/children — for BOTH the view-model-rendered
        // families (dashboard/epics/stories) and every long-tail page whose breadcrumb is recovered from its own
        // captured HTML (never re-read from disk).
        GeneratedSite();
        using var manifestDoc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar))));
        var root = manifestDoc.RootElement;

        var nav = root.GetProperty("nav").EnumerateArray().ToList();
        Assert.NotEmpty(nav);
        Assert.Contains(nav, n => n.GetProperty("label").GetString() == "Home" && n.GetProperty("outputRelativePath").GetString() == "index.html");
        Assert.Contains(nav, n => n.GetProperty("label").GetString() == "Epics");

        var pages = root.GetProperty("pages");

        // The dashboard (family-rendered) has no parent — its own breadcrumb trail is empty, just like the static page.
        var home = pages.GetProperty("index.html");
        Assert.Empty(home.GetProperty("breadcrumb").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, home.GetProperty("parent").ValueKind);
        Assert.Contains("epics.html", home.GetProperty("children").EnumerateArray().Select(c => c.GetString()));

        // "About" is a long-tail (non-family) page: its breadcrumb/parent must be recovered from its OWN captured
        // HTML, not just available on the family pages.
        var about = pages.GetProperty("about.html");
        var aboutCrumbs = about.GetProperty("breadcrumb").EnumerateArray().ToList();
        Assert.Equal(2, aboutCrumbs.Count);
        Assert.Equal("Home", aboutCrumbs[0].GetProperty("label").GetString());
        Assert.Equal("index.html", aboutCrumbs[0].GetProperty("outputRelativePath").GetString());
        Assert.Equal("About", aboutCrumbs[1].GetProperty("label").GetString());
        Assert.Equal(JsonValueKind.Null, aboutCrumbs[1].GetProperty("outputRelativePath").ValueKind);
        Assert.Equal("index.html", about.GetProperty("parent").GetString());

        // Epic 1 (family-rendered) drills down to its own stories, recovered structurally from the model.
        var epic1 = pages.GetProperty("epics/epic-1.html");
        Assert.Equal("epics.html", epic1.GetProperty("parent").GetString());
        var epic1Children = epic1.GetProperty("children").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains("epics/story-1-1.html", epic1Children);
    }

    // ===== Story 22.2: the canonical IR's added guarantees ===================================================

    /// <summary>Story 22.2 AC #4. The IR's dashboard region must be the SAME bytes the static page wrote — the
    /// claim Story 23.1 found violated by exactly 277 bytes / 5 anchors, because <c>BuildSpaBundle</c>'s
    /// <c>BuildIndexPage</c> call used named arguments starting at <c>counts:</c> and so silently skipped the
    /// positional <c>codeItemHref</c>, degrading the Git Pulse top-changed-file labels from links to plain text.
    /// Asserting the whole <c>&lt;main&gt;</c> block rather than an anchor count means ANY future argument
    /// divergence at that call site fails here, not just this one.</summary>
    [Fact]
    public void DashboardIrRegion_CarriesTheSameMainBlock_AsTheStaticPage()
    {
        var gen = GeneratedSite();
        var region = gen.RenderSpaBundle().Pages.Single(p => p.OutputRelativePath == "index.html").ContentHtml;
        var staticIndex = File.ReadAllText(Path.Combine(Site, "index.html"));

        Assert.Equal(MainBlock(staticIndex), MainBlock(region));
    }

    /// <summary>Story 22.2 AC #5: a captured page's IR region keeps the page-local context band the static
    /// renderer computed for it, instead of the generic key-views nav the re-render path produced (Story 23.1's
    /// enumerated difference #2). A page that genuinely HAS no local context is unchanged.</summary>
    [Fact]
    public void CapturedPage_KeepsItsOwnLocalContextNavBand_AndLeavesOthersUnchanged()
    {
        // A second ADR gives the band a navigable (non-active) sibling; with one ADR it is a degenerate self-link
        // and AppendKeyViewsBand correctly falls back to the generic chips.
        File.WriteAllText(Path.Combine(Adrs, "0002-another-decision.md"),
            "# ADR 0002: Another Decision\n\n**Status:** Accepted\n\nBody.\n");
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        string NavOf(string s) => s[..(s.IndexOf("</nav>", StringComparison.Ordinal) + "</nav>".Length)];

        var adrRegion = bundle.Pages.Single(p => p.OutputRelativePath == "adrs/0001-a-decision.html").ContentHtml;
        var adrNav = NavOf(adrRegion);
        Assert.Contains("site-nav-local-context", adrNav);
        Assert.Contains("aria-label=\"ADRs\"", adrNav);
        // It IS the static page's own nav, byte-for-byte — not a re-render that happens to look similar.
        var staticAdr = File.ReadAllText(Path.Combine(Site, "adrs", "0001-a-decision.html"));
        Assert.Equal(NavOf(staticAdr[staticAdr.IndexOf("<nav class=\"site-nav\"", StringComparison.Ordinal)..]), adrNav);
        // The inline nav-toggle script that follows the nav on the HTML surface is excluded (the client owns the
        // toggle through delegation, and an injected script never executes after an innerHTML swap).
        Assert.DoesNotContain("<script", adrNav);

        // A page with no local context keeps the generic band, unchanged.
        var aboutNav = NavOf(bundle.Pages.Single(p => p.OutputRelativePath == "about.html").ContentHtml);
        Assert.DoesNotContain("site-nav-local-context", aboutNav);
        Assert.Contains("<nav class=\"site-nav\"", aboutNav);
    }

    /// <summary>Story 22.2 AC #1/#5/#6: the manifest carries the schema version and, per page, the head
    /// projection, the script-island declaration, and the delta-addressing pair (content hash + byte size). Each
    /// is checked against the region that actually shipped, so none can become a parallel, drifting truth.</summary>
    [Fact]
    public void Manifest_CarriesSchemaVersion_HeadProjection_ScriptIslands_AndPerPageHashAndBytes()
    {
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();
        using var manifestDoc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar))));
        var root = manifestDoc.RootElement;

        Assert.Equal(SpaDelivery.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());

        var regions = bundle.Pages.ToDictionary(p => p.OutputRelativePath, p => p.ContentHtml, StringComparer.Ordinal);
        foreach (var page in root.GetProperty("pages").EnumerateObject())
        {
            var region = regions[page.Name];
            var head = page.Value.GetProperty("head");
            // Head projection: title + description, description resolved with the SAME fallback-to-title rule
            // PathUtil.RenderHeadOpen applies, so a consumer never has to reproduce the fallback itself.
            Assert.Equal(page.Value.GetProperty("title").GetString(), head.GetProperty("title").GetString());
            Assert.False(string.IsNullOrWhiteSpace(head.GetProperty("description").GetString()));

            // Delta addressing describes the region that shipped.
            Assert.Equal(SpaDelivery.ContentHash(region), page.Value.GetProperty("contentHash").GetString());
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(region), page.Value.GetProperty("bytes").GetInt32());

            // Every embedded <script> in the region is declared, with the strip-or-nonce kind a consumer needs.
            var declared = page.Value.GetProperty("scriptIslands").EnumerateArray().ToList();
            Assert.Equal(SpaDelivery.ExtractScriptIslands(region).Count, declared.Count);
            Assert.All(declared, i => Assert.Contains(i.GetProperty("kind").GetString(),
                new[] { SpaDelivery.DataIslandKind, SpaDelivery.ExecutableScriptKind }));
        }

        // Guard against a vacuous green: the dashboard really does carry islands to declare.
        var home = root.GetProperty("pages").GetProperty("index.html").GetProperty("scriptIslands").EnumerateArray().ToList();
        Assert.NotEmpty(home);
        Assert.Contains(home, i => i.GetProperty("id").GetString() == "dashboard-hierarchy-data"
            && i.GetProperty("kind").GetString() == SpaDelivery.DataIslandKind);
    }

    /// <summary>Story 22.2 AC #6, proved by REPEATED RUNS rather than asserted once: two consecutive generations
    /// of unchanged input must emit a byte-identical manifest — otherwise the per-page hash reports a false change
    /// on every build and is worthless to 22.5/22.6. NFR9 (reproducible CI) says the same thing about the chunk
    /// files, so those are compared too.
    /// <para>Both runs write to the SAME directory, because the diagnostics page echoes the configured output root
    /// inside its own region — two different directories would (correctly) yield two different hashes for it.</para>
    /// <para>And that directory sits OUTSIDE the repo root, which this class's default <c>Site</c> does not. On a
    /// non-git fixture the code map falls back to <c>FallbackCodeWalk</c>, which walks the repo root and excludes
    /// dot-dirs/<c>bin</c>/<c>obj</c>/<c>node_modules</c> but NOT the output directory — so a nested output feeds
    /// run 1's generated <c>.html</c> files into run 2's code map and <c>code-map.html</c> legitimately changes.
    /// That is a pre-existing property of the fallback walk (the real repo is a git checkout whose output dir is
    /// gitignored, so it never hits it), not IR volatility, and putting the output outside the walked tree is what
    /// makes this test measure the thing it claims to.</para></summary>
    [Fact]
    public void ManifestAndChunks_AreByteIdentical_AcrossTwoConsecutiveRunsOfUnchangedInput()
    {
        var isolatedOutput = Directory.CreateTempSubdirectory("specscribe-spa-det-").FullName;
        try
        {
            AssertTwoRunsAgree(isolatedOutput);
        }
        finally
        {
            try { Directory.Delete(isolatedOutput, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void AssertTwoRunsAgree(string outputRoot)
    {
        void Generate()
        {
            var gen = new SiteGenerator(ForgeOptions.Resolve(
                source: Source, adrs: Adrs, output: outputRoot, projectName: "SpecScribe",
                includeReadme: false, emitSpa: true));
            Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        }

        string ManifestPathOnDisk() => Path.Combine(outputRoot, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        Dictionary<string, string> ChunksOnDisk() => Directory
            .EnumerateFiles(Path.Combine(outputRoot, SpaDelivery.ChunkDir), "pages-*.json")
            .ToDictionary(Path.GetFileName!, File.ReadAllText, StringComparer.Ordinal);

        Generate();
        var firstManifest = File.ReadAllText(ManifestPathOnDisk());
        var firstChunks = ChunksOnDisk();

        Generate();
        var secondManifest = File.ReadAllText(ManifestPathOnDisk());
        if (firstManifest != secondManifest)
        {
            // Name the culprit rather than making the next reader diff two 100 KB strings by eye: a hash that
            // moves on unchanged input means some volatile token lives INSIDE that page's content region.
            using var a = JsonDocument.Parse(firstManifest);
            using var b = JsonDocument.Parse(secondManifest);
            var bPages = b.RootElement.GetProperty("pages");
            var moved = a.RootElement.GetProperty("pages").EnumerateObject()
                .Where(p => p.Value.GetProperty("contentHash").GetString()
                    != bPages.GetProperty(p.Name).GetProperty("contentHash").GetString())
                .Select(p => p.Name)
                .ToList();
            Assert.Fail($"content hash moved across two runs of unchanged input for: {string.Join(", ", moved)}");
        }

        var secondChunks = ChunksOnDisk();
        Assert.Equal(firstChunks.Keys.OrderBy(k => k, StringComparer.Ordinal), secondChunks.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(firstChunks, kv => Assert.Equal(kv.Value, secondChunks[kv.Key]));
    }

    [Fact]
    public void LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock()
    {
        // AC #1: no re-render. A long-tail page's SPA content region carries the EXACT <main> block the static page
        // wrote (sliced from the render pipeline's own output, not a re-parse) — byte-for-byte.
        // SCOPE, stated plainly (Story 22.2): this compares ONLY the <main> block and the presence of a nav
        // element. It was therefore blind to the page-local nav-context divergence Story 23.1 found — that lived
        // in the nav, which this never inspected. CapturedPage_KeepsItsOwnLocalContextNavBand above is what pins
        // the nav; do not read this test as covering it.
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
    public void EmitSpaSite_ThrowsALoudDiagnostic_WhenADocsOutputPathCollidesWithAReservedSpaPath()
    {
        // Story 6.7 review: EmitSpaSite writes app.html/specscribe-spa.js/spa/*.json LAST, with no guard against a
        // real doc's own output path claiming one of those reserved names — which would otherwise silently
        // overwrite either the legitimate static page or the SPA's own delivery file. A doc source file named
        // "app.md" at the source root maps straight to the output root's "app.html" (Path.ChangeExtension), landing
        // squarely on SpaDelivery.EntryFileName.
        File.WriteAllText(Path.Combine(Source, "app.md"), "# App\n\nSome doc that happens to be named app.\n");

        var gen = new SiteGenerator(Options(spa: true));
        var ex = Assert.Throws<InvalidOperationException>(() => gen.GenerateAll());
        Assert.Contains("app.html", ex.Message);
    }

    [Fact]
    public void RegenerateEpics_ReEmitsTheSpaSite_EvenWhenEpicsSourceIsMissing()
    {
        // Story 6.7 review: RegenerateEpics' early-return path (epics.md not found) rewrote the nav via WriteIndex
        // but skipped EmitSpaSite — unlike every other watch call site — so the SPA form could go stale relative
        // to the freshly-rewritten index.html. Deleting epics.md flips the top nav graph (the "Epics" item drops);
        // the SPA manifest must reflect that on the very next incremental pass, not lag behind.
        var gen = GeneratedSite();
        Assert.Contains(NavLabels(), l => l == "Epics");

        File.Delete(Path.Combine(Source, "planning-artifacts", "epics.md"));
        var ev = gen.RegenerateEpics();

        // Story 5.3 changed this outcome from Skipped to Removed: this exact scenario — epics.md deleted AFTER a
        // full generation — now also tears down the stale epics output family it left behind, which is a real
        // destructive change to the output tree rather than the no-op Skipped claimed. A project that never had an
        // epics.md still reports Skipped. The subject of THIS test is unchanged (the SPA manifest's nav must not
        // lag the rewritten index); only the pinned outcome moved. [Story 5.3 AC #3]
        Assert.Equal(GenerationOutcome.Removed, ev.Outcome);
        Assert.DoesNotContain(NavLabels(), l => l == "Epics");

        List<string> NavLabels()
        {
            using var doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar))));
            return doc.RootElement.GetProperty("nav").EnumerateArray()
                .Select(n => n.GetProperty("label").GetString()!).ToList();
        }
    }

    [Fact]
    public void RegenerateAdrs_PrunesTheRenamedOrDeletedRecordFromTheSpaCapture()
    {
        // Deferred item (Story 6.7 review): _spaCapture entries were only explicitly removed on a doc DELETE
        // going through the generic RemoveFor path — an ADR rename/delete goes through RegenerateAdrs instead
        // (IsAdr routes there, never through RemoveFor), which wipes+rebuilds the physical adrs/ directory but
        // left the in-memory _spaCapture key for the vanished record dangling, so the NEXT SPA bundle carried
        // an orphaned page no longer part of the static site. This pins the fix: RegenerateAdrs must prune it.
        File.WriteAllText(Path.Combine(Adrs, "0002-second-decision.md"), "# ADR 0002: Second Decision\n\n**Status:** Accepted\n\nBody.\n");

        var gen = GeneratedSite();
        Assert.Contains(gen.RenderSpaBundle().Pages, p => p.OutputRelativePath == "adrs/0002-second-decision.html");
        Assert.True(File.Exists(Path.Combine(Site, "adrs", "0002-second-decision.html")));

        // Simulate a watch-mode delete (or an equivalent rename, which the same wipe+rebuild path handles
        // identically): the source record vanishes, then RegenerateAdrs runs — the real dispatch route ANY
        // ADR-directory change takes, per IsAdr/RegenerateAdrs, never RemoveFor.
        File.Delete(Path.Combine(Adrs, "0002-second-decision.md"));
        gen.RegenerateAdrs();

        Assert.False(File.Exists(Path.Combine(Site, "adrs", "0002-second-decision.html")));
        var bundle = gen.RenderSpaBundle();
        Assert.DoesNotContain(bundle.Pages, p => p.OutputRelativePath == "adrs/0002-second-decision.html");
        // The surviving ADR (from the constructor fixture) is untouched.
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == "adrs/0001-a-decision.html");
    }

    [Fact]
    public void RegenerateAdrs_PrunesTheOldKey_OnAnActualRename_NotJustADelete()
    {
        // Review follow-up: the test above only exercised File.Delete; a real watch-mode RENAME (File.Move) also
        // leaves the OLD source file gone and a NEW one present in the SAME RegenerateAdrs pass — confirming the
        // old output-path key is pruned AND the new one appears, not just that delete-then-nothing is handled.
        File.WriteAllText(Path.Combine(Adrs, "0002-old-name.md"), "# ADR 0002: Old Name\n\n**Status:** Accepted\n\nBody.\n");
        var gen = GeneratedSite();
        Assert.Contains(gen.RenderSpaBundle().Pages, p => p.OutputRelativePath == "adrs/0002-old-name.html");

        File.Move(Path.Combine(Adrs, "0002-old-name.md"), Path.Combine(Adrs, "0002-new-name.md"));
        gen.RegenerateAdrs();

        var bundle = gen.RenderSpaBundle();
        Assert.DoesNotContain(bundle.Pages, p => p.OutputRelativePath == "adrs/0002-old-name.html");
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == "adrs/0002-new-name.html");
    }

    [Fact]
    public void RegenerateAdrs_NeverPrunesANonRecordPage_ThatStillRendersEveryPass()
    {
        // Review follow-up: the fix's live-path set previously came from _adrs alone, which ONLY ever holds
        // record entries (IsAdrRecordFile) — a template scaffold file and a nested (non-root) README both render
        // real pages via the plain WriteOutput branch but are deliberately never records, so they'd be pruned as
        // "stale" the SAME pass that just (re)wrote them. Pins that a template + nested README both survive
        // across a second RegenerateAdrs pass (where the pruning actually runs and could evict them).
        File.WriteAllText(Path.Combine(Adrs, "0000-template.md"), "# ADR Template\n\nFill this in.\n");
        Directory.CreateDirectory(Path.Combine(Adrs, "notes"));
        File.WriteAllText(Path.Combine(Adrs, "notes", "README.md"), "# Notes\n\nContext for this subfolder.\n");

        var gen = GeneratedSite();
        var firstBundle = gen.RenderSpaBundle();
        Assert.Contains(firstBundle.Pages, p => p.OutputRelativePath == "adrs/0000-template.html");
        Assert.Contains(firstBundle.Pages, p => p.OutputRelativePath == "adrs/notes/README.html");

        // A second pass is where the prune actually executes against a NON-EMPTY prior capture.
        gen.RegenerateAdrs();

        var bundle = gen.RenderSpaBundle();
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == "adrs/0000-template.html");
        Assert.Contains(bundle.Pages, p => p.OutputRelativePath == "adrs/notes/README.html");
    }

    [Fact]
    public void RegenerateAdrs_PrunesTheLandingPage_WhenTheLastAdrIsRemoved()
    {
        // Review follow-up: unconditionally protecting the landing path from pruning was itself a staleness bug —
        // when the LAST record (and the root README) are both gone, nothing writes adrs/index.html this pass
        // (the synthesized-landing fallback is gated on _adrs.Count > 0), so a stale capture entry for it must
        // be pruned too, not kept alive just because it's "the landing page."
        File.WriteAllText(Path.Combine(Adrs, "0002-only-other.md"), "# ADR 0002: Only Other\n\n**Status:** Accepted\n\nBody.\n");
        var gen = GeneratedSite();
        Assert.Contains(gen.RenderSpaBundle().Pages, p => p.OutputRelativePath == "adrs/index.html");

        File.Delete(Path.Combine(Adrs, "0001-a-decision.md"));
        File.Delete(Path.Combine(Adrs, "0002-only-other.md"));
        gen.RegenerateAdrs();

        Assert.DoesNotContain(gen.RenderSpaBundle().Pages, p => p.OutputRelativePath == "adrs/index.html");
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
        Assert.Contains("<div id=\"spa-content\" data-path=\"index.html\" data-asset-version=\"", app);
        Assert.Contains("<noscript>", app);
        Assert.Contains("<a href=\"index.html\">open the full static site</a>", app);
        Assert.Contains("<script src=\"" + SpaDelivery.ScriptName + "?v=", app); // cache-busted like specscribe.js
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

    /// <summary>The shipped-asset directory, located from the test bin folder — the established pattern for
    /// asserting a fact about specscribe.js content (StylesheetTests / HierarchyRolloutTests).</summary>
    private static string RepoSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !Directory.Exists(Path.Combine(dir, "src", "SpecScribe")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(string.IsNullOrEmpty(dir), "could not locate the repository root from the test bin directory");
        return Path.Combine(dir!, "src", "SpecScribe");
    }

}
