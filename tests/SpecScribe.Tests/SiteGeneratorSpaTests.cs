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
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "deferred-work.md"), DeferredWorkMd);
        File.WriteAllText(Path.Combine(Source, "implementation-artifacts", "spec-a-followup.md"), SpecFollowUpMd);
    }

    /// <summary>Deferred work that STEMS FROM Story 1.1 and is RESOLVED by a spec doc rather than by a dotted
    /// story id — the exact shape <c>WorkGraph.BuildStory</c> routes through <c>ResolvingHref</c>, which is the
    /// field Story 22.4's AC #5 defect nulled on the <c>RenderEpicsPages</c> route. Without this the fixture
    /// renders no work graph at all and the parity assertion is vacuous. [Story 22.4 code review]</summary>
    private const string DeferredWorkMd = """
        # Deferred Work

        ## Deferred from: Story 1.1 Foundation Story

        - source_spec: `1-1-foundation.md`
          summary: **[RESOLVED]** ~~Harden the foundation's error path.~~ Picked up by `spec-a-followup.md`.
          evidence: Fixture item exercising the resolver-node/edge pair.

        - source_spec: `1-1-foundation.md`
          summary: An open follow-up from the foundation story, deliberately unresolved.
          evidence: Fixture item exercising the unresolved branch.
        """;

    private const string SpecFollowUpMd = """
        # Spec: A Follow-up

        route: one-shot

        Resolves the deferred foundation item.
        """;

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

    /// <summary>Every page the site emitted (the SPA's own entry shell excluded).
    /// <para>[Story 23.6 AC #8] Was a <c>*.html</c> walk of the output root. The route set is the same inventory
    /// now that the IR is what a generate produces — and, unlike the walk, it cannot silently shrink to zero and
    /// leave the callers' "covers every page" comparisons passing over two empty lists.</para></summary>
    private IReadOnlyList<string> StaticHtmlPages() =>
        SiteRegion.Routes(Site)
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

        var staticIndex = SiteRegion.Read(Site, "index.html");
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

        // STORY 20.9 took the instance count from five site-wide to TEN (four of them on code-map.html alone).
        // Story 20.10 collapsed those four Code Map panels into ONE shared-payload instance — back down to one
        // island on this page, now carrying all four views' data instead of four independent islands. Extended
        // here rather than in a parallel test, per Task 7.3.
        var spaCodeMap = gen.RenderSpaBundle().Pages.SingleOrDefault(p => p.OutputRelativePath == "code-map.html");
        if (spaCodeMap is not null)
        {
            Assert.Contains("id=\"codemap-data\"", spaCodeMap.ContentHtml);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(spaCodeMap.ContentHtml, "ss-hierarchy-data"));
            // Every view's own scaffold + membership rides the ONE island, so the capture carries all four.
            foreach (var key in new[] { "full", "no-spec", "no-tests", "no-spec-no-tests" })
            {
                Assert.Contains($"\"key\":\"{key}\"", spaCodeMap.ContentHtml);
            }
            // The (now shared, deduplicated) file table is this surface's twin (Story 20.6 D1), so IT is what has
            // to ride the capture - the component emits no generic twin here at all.
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
        var index = SiteRegion.Read(Site, "index.html");
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
        // [Story 23.6 AC #8] ⚠️ THE SUBJECT OF THE SECOND HALF MOVED, and the split is the substance.
        //
        // The old assertion paired `<script src="plotly-hierarchy.min.js">` — CHROME, emitted by the deleted
        // `HtmlRenderAdapter.Render` from `page.Assets.HierarchyEngineNeeded` — against the chart host in the body.
        // No C# code path emits that tag any more. The renderer derives the need STRUCTURALLY from the region it
        // is handed (`web/ir/adapter.ts`: `needsHierarchyEngine` is a `data-hierarchy` probe over
        // `region.mainInnerHtml`), so the tag and the host cannot disagree by construction, and
        // `web/test/contracts.test.ts` pins that derivation. Re-asserting it here would assert nothing.
        //
        // What is STILL a C# decision, and still exactly Story 20.7's bug, is the ASSET COPY: `EnsureHierarchyEngine`
        // ships the 1.2 MB bundle only when a page sets the flag. A family that hosts a chart but never sets
        // `HierarchyEngineNeeded` still yields a site whose chart cannot mount — the engine simply is not there.
        // That is what this now asserts, per-region rather than per-tag, so the "no fewer" half survives the
        // deletion.
        var routesHostingChart = SiteRegion.Routes(Site)
            .Where(r => HierarchyExplorer.ContainsHost(SiteRegion.Read(Site, r)))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(routesHostingChart.Count > 0, File.Exists(bundle));

        if (hostsChart)
        {
            // Guard against a vacuous green: this fixture HAS epics, so the converted family really is exercised.
            Assert.Contains("index.html", routesHostingChart);
            Assert.Contains("epics.html", routesHostingChart);
            Assert.Contains(routesHostingChart, p => p.StartsWith("epics/epic-", StringComparison.Ordinal));
            Assert.Contains(routesHostingChart, p => p.StartsWith("epics/story-", StringComparison.Ordinal));
            // And a page with no chart hosts none — the "no more" half, now read off the region.
            Assert.DoesNotContain("about.html", routesHostingChart);
        }
        else
        {
            Assert.Empty(routesHostingChart);
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

        var staticIndex = SiteRegion.Read(Site, "index.html");
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
        var staticIndex = SiteRegion.Read(Site, "index.html");

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
        var staticAdr = SiteRegion.Read(Site, "adrs/0001-a-decision.html");
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

            // Delta addressing describes the region that shipped. `bytes` is the region's JSON-ENCODED size
            // (code review fix) — the same exact measurement the chunk ceiling budgets against, not raw UTF-8
            // content bytes, which under-report escape-heavy regions by up to 6x.
            Assert.Equal(SpaDelivery.ContentHash(region), page.Value.GetProperty("contentHash").GetString());
            Assert.Equal(
                System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(region)),
                page.Value.GetProperty("bytes").GetInt32());

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

    /// <summary>Code review finding: the non-git fallback code walk (<c>FallbackCodeWalk</c>, the branch this
    /// non-git fixture exercises per the comment above) fed <c>Directory.GetFileSystemEntries</c>'s result
    /// straight into a stack-based walk with no sort. <c>GetFileSystemEntries</c> makes NO ordering guarantee,
    /// and NTFS vs. ext4/APFS enumerate the same directory differently in practice — a genuine PORTABILITY bug
    /// (stable on one OS, different between Windows and Linux for byte-identical source), which is exactly what
    /// <c>portability-probe</c> exists to catch, and which moved <c>GenerateAll_GoldenIrFingerprint...</c>
    /// between CI's Windows and Ubuntu jobs. Files are created here in DELIBERATELY descending name order so an
    /// unsorted walk would surface them in creation order; the fix must show them in ascending order in
    /// <c>code-map.html</c> regardless.</summary>
    [Fact]
    public void CodeMapFallbackWalk_ListsFiles_InDeterministicSortedOrder_NotFilesystemEnumerationOrder()
    {
        var codeDir = Path.Combine(_root, "tools-probe");
        Directory.CreateDirectory(codeDir);
        // Created descending: zebra first, apple last — the OPPOSITE of the order the assertion requires.
        File.WriteAllText(Path.Combine(codeDir, "zebra.py"), "print('z')\n");
        File.WriteAllText(Path.Combine(codeDir, "mango.py"), "print('m')\n");
        File.WriteAllText(Path.Combine(codeDir, "apple.py"), "print('a')\n");

        var gen = GeneratedSite(spa: false);
        var codeMapRoute = "code-map.html";
        Assert.True(SiteRegion.Exists(Site, codeMapRoute), "code-map.html did not render — the fallback walk found nothing");
        var html = SiteRegion.Read(Site, codeMapRoute);

        var apple = html.IndexOf("apple.py", StringComparison.Ordinal);
        var mango = html.IndexOf("mango.py", StringComparison.Ordinal);
        var zebra = html.IndexOf("zebra.py", StringComparison.Ordinal);
        Assert.True(apple >= 0 && mango >= 0 && zebra >= 0, "not all three probe files appear in code-map.html");
        Assert.True(apple < mango, "apple.py (alphabetically first) must appear before mango.py, not in creation order");
        Assert.True(mango < zebra, "mango.py must appear before zebra.py (alphabetically last, created first)");
    }

    [Fact]
    public void LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock()
    {
        // AC #1: no re-render. [Story 23.6 AC #8] ⚠️ THE COMPARISON MOVED, because one of its two sides was
        // the written page and that no longer exists.
        //
        // It used to slice `<main>` out of the static document and assert the SPA bundle's region contained it
        // byte-for-byte. Both sides are the composed region now, so re-pointing it at the page would be a
        // tautology — and the byte-equality it was standing in for is exactly what `RegionCompositionParityTests`
        // proved across 1,469 pages before being retired with its subject.
        //
        // What is still TWO code paths, and therefore still worth pinning, is the in-memory bundle
        // (`RenderSpaBundle`) against the EMITTED IR (`EmitSpaSite` → chunk files → read back). They share a
        // producer but not a serialization path, so a chunk-assignment or escaping bug shows up here.
        //
        // SCOPE, unchanged and still worth stating: this compares the region body and the presence of a nav
        // element. It is blind to the page-local nav-context divergence Story 23.1 found — that lives in the
        // nav's contents, which this never inspects. `CapturedPage_KeepsItsOwnLocalContextNavBand` pins that.
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        foreach (var rel in new[] { "about.html", "requirements/fr1.html", "diagnostics.html" })
        {
            var region = bundle.Pages.Single(p => p.OutputRelativePath == rel).ContentHtml;
            Assert.Equal(SiteRegion.Read(Site, rel).TrimEnd(), region.TrimEnd());
            // …and the region carries the page's own nav + breadcrumb chrome (the swappable region shape).
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
        Assert.True(SiteRegion.Exists(Site, "adrs/0002-second-decision.html"));

        // Simulate a watch-mode delete (or an equivalent rename, which the same wipe+rebuild path handles
        // identically): the source record vanishes, then RegenerateAdrs runs — the real dispatch route ANY
        // ADR-directory change takes, per IsAdr/RegenerateAdrs, never RemoveFor.
        File.Delete(Path.Combine(Adrs, "0002-second-decision.md"));
        gen.RegenerateAdrs();

        Assert.False(SiteRegion.Exists(Site, "adrs/0002-second-decision.html"));
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

    /// <summary>⚠️ INVERTED by Story 23.6 AC #6, and the inversion is the point.
    /// <para>This test used to be <c>WithoutSpa_EmitsNoSpaFilesAtAll</c> and asserted the opposite: with
    /// <c>--spa</c> off, not one IR artifact was written. That was correct while C# also wrote a <c>.html</c>
    /// per page. It no longer does — the IR is the canonical output (ADR 0016) and the static pages are
    /// rendered FROM it (ADR 0022 §Decision 3) — so a run that emitted no IR would emit nothing at all.</para>
    /// <para>Kept rather than deleted, and kept as an ASSERTION rather than a comment, because "the IR is
    /// always there" is now the load-bearing guarantee between the user and an empty output root. The
    /// <c>spa: false</c> argument is passed deliberately: it proves the retired flag cannot suppress the IR
    /// even when a caller still sets it.</para></summary>
    [Fact]
    public void TheIrIsEmittedUnconditionally_EvenWhenTheRetiredSpaFlagIsOff()
    {
        GeneratedSite(spa: false);

        Assert.True(File.Exists(Path.Combine(Site, SpaDelivery.EntryFileName)));
        Assert.True(File.Exists(Path.Combine(Site, SpaDelivery.ScriptName)));
        Assert.True(Directory.Exists(Path.Combine(Site, SpaDelivery.ChunkDir)));
        Assert.True(File.Exists(Path.Combine(Site, SpaDelivery.ManifestPath)));
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

    // ===== Story 22.4: ONE region shape across the whole IR ==================================================

    /// <summary>Story 22.4 AC #4. Before this story the IR carried TWO region shapes: a re-rendered family page
    /// carried <c>HtmlRenderAdapter.RenderWayfinding</c>'s <c>&lt;div class="page-wayfinding"&gt;</c> wrapper, while a
    /// CAPTURED page whose pager rendered non-empty was sliced from the inner <c>&lt;div class="breadcrumb"&gt;</c> —
    /// carrying the wrapper's closing <c>&lt;/div&gt;</c> without its opener, unbalanced by exactly one element. On
    /// the real repo that was 594 of 1,400 pages. The TS adapter detected the shape and prepended the missing
    /// opener; that repair (and the throw behind it) is deleted by this story, so the invariant has to hold HERE,
    /// at the emitter.
    /// <para>Asserted over EVERY page in the bundle, not a sample, because the defect class this replaces —
    /// Story 23.3's double-opened wrapper — nested <c>&lt;main&gt;</c> and <c>&lt;footer&gt;</c> inside the
    /// wayfinding band on 187 pages while <c>&lt;main&gt;</c> stayed byte-identical, so parity, link resolution and
    /// every a11y assertion passed green. A sampled assertion is exactly what that defect walks through.</para></summary>
    [Fact]
    public void EveryIrRegion_HasOneBalancedWayfindingBand_AndExactlyOneMainLandmark()
    {
        // A second ADR gives the ADR pages a non-empty prev/next pager, which is the ONLY thing that makes
        // RenderWayfinding emit its wrapper — so this fixture genuinely exercises a CAPTURED wrapped page and not
        // just the re-rendered family shape. Without it the test would be vacuously green on the old emitter.
        File.WriteAllText(Path.Combine(Adrs, "0002-another-decision.md"),
            "# ADR 0002: Another Decision\n\n**Status:** Accepted\n\nBody.\n");
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        const string mainMarker = "<main id=\"main-content\"";
        const string wrapMarker = "<div class=\"page-wayfinding\"";
        const string crumbMarker = "<div class=\"breadcrumb\"";

        var wrapped = 0;
        var bare = 0;
        foreach (var page in bundle.Pages)
        {
            var html = page.ContentHtml;
            var mainOpen = html.IndexOf(mainMarker, StringComparison.Ordinal);
            if (mainOpen < 0)
            {
                // The documented degrade: a page carrying no landmark slices to nav-only. It has no band to
                // balance, and the webview drops it outright (ReferenceEquals check). Nothing to assert.
                continue;
            }

            // Exactly ONE landmark — a second <main> is the shape 23.3's defect produced.
            Assert.Equal(1, CountOccurrences(html, mainMarker));
            Assert.Equal(1, CountOccurrences(html, "<main"));

            var wrapOpen = html.IndexOf(wrapMarker, StringComparison.Ordinal);
            var crumbOpen = html.IndexOf(crumbMarker, StringComparison.Ordinal);
            var bodyStart = mainOpen;
            if (wrapOpen >= 0 && wrapOpen < bodyStart) bodyStart = wrapOpen;
            if (crumbOpen >= 0 && crumbOpen < bodyStart) bodyStart = crumbOpen;
            if (bodyStart == mainOpen) continue; // no wayfinding band at all — legitimate on some surfaces

            var band = html[bodyStart..mainOpen];
            // Element-balanced: the band opens and closes every <div> it contains, so injecting it can never
            // swallow the <main> that follows it.
            Assert.Equal(CountOccurrences(band, "<div"), CountOccurrences(band, "</div>"));

            // The band opens and closes on the SAME side of <main> — i.e. entirely before it. A wrapper opener
            // that survives past the landmark is the nesting defect restated.
            if (wrapOpen >= 0 && wrapOpen < mainOpen)
            {
                wrapped++;
                Assert.StartsWith(wrapMarker, band, StringComparison.Ordinal);
                Assert.EndsWith("</div>\n\n", band, StringComparison.Ordinal);
            }
            else
            {
                bare++;
            }
        }

        // Guard against a vacuous green: BOTH shapes must actually be present in what was measured, and the
        // wrapped set must include a CAPTURED page (an ADR record), not only the re-rendered epics family.
        Assert.True(wrapped > 0, "fixture emitted no wrapped wayfinding band — the invariant was not exercised");
        Assert.True(bare > 0, "fixture emitted no bare breadcrumb band — the invariant was not exercised");
        var adr = bundle.Pages.Single(p => p.OutputRelativePath == "adrs/0001-a-decision.html").ContentHtml;
        Assert.Contains(wrapMarker, adr, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var n = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
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

    /// <summary>The work-graph accessible summary <c>Charts</c> renders for an epic subgraph — the ONE string
    /// that carries both counts the 46-delta moved. Two shapes exist (with and without circular provenance), so
    /// this matches the stable prefix and returns the whole sentence for comparison.</summary>
    private static IReadOnlyList<string> WorkGraphSummaries(string html)
    {
        var found = new List<string>();
        const string marker = "Work graph for ";
        for (var i = html.IndexOf(marker, StringComparison.Ordinal); i >= 0;
             i = html.IndexOf(marker, i + marker.Length, StringComparison.Ordinal))
        {
            var end = html.IndexOf("The list below enumerates", i, StringComparison.Ordinal);
            if (end < 0) { found.Add(html[i..]); break; }
            found.Add(html[i..end].Trim());
        }
        return found;
    }

    /// <summary>
    /// AC #5's regression guard: the static page and the IR must report the SAME work-graph node and edge
    /// counts for every surface that carries one.
    ///
    /// <para>This is the test AC #5 required ("asserted by a test that would have caught the 46-delta") and that
    /// Story 22.4 shipped without — its Task 3 subtask was marked complete while no such test existed, found by
    /// the story's code review. The defect it guards: <c>ResolveDeferredModel</c> handed an EMPTY <c>_docs</c> to
    /// <c>FollowUpRefs.BuildHrefMap</c> on the <c>RenderEpicsPages</c> route, so every spec resolver's
    /// <c>ResolvingHref</c> came back null and <c>WorkGraph.BuildStory</c> dropped the resolver node AND its
    /// edge — the static page drew "4 work items and 3 provenance links" where the IR drew "5 and 5", across 46
    /// story surfaces plus 9 epic pages and <c>work-graph.html</c>.</para>
    ///
    /// <para>⚠️ The assertion is guarded against vacuity on purpose. Story 22.4's own parity measurement first
    /// reported a meaningless "822/822 identical" because it compared <c>undefined</c> to <c>undefined</c> on a
    /// misspelled field. A comparison that finds nothing to compare must FAIL, not pass.</para>
    /// </summary>
    [Fact]
    public void EverySurface_ReportsTheSameWorkGraphCounts_InTheStaticPageAndTheIr()
    {
        var gen = GeneratedSite();
        var bundle = gen.RenderSpaBundle();

        var compared = 0;
        foreach (var page in bundle.Pages)
        {
            // [Story 23.6 AC #8] The "static page" side of this comparison is gone with the writer. The two
            // sides that remain are the in-memory bundle and the EMITTED IR — same producer, different
            // serialization path — so a route that survives the bundle but is dropped or garbled on the way to a
            // chunk file is still caught. The vacuity guard below is what keeps the narrowed comparison honest.
            if (!SiteRegion.Exists(Site, page.OutputRelativePath)) continue;

            var fromStatic = WorkGraphSummaries(SiteRegion.Read(Site, page.OutputRelativePath));
            var fromIr = WorkGraphSummaries(page.ContentHtml);

            // Same NUMBER of work graphs on the page, and the same counts in each — compared as the rendered
            // sentence, so a node/edge divergence and a label divergence are both caught by one assertion.
            Assert.Equal(fromStatic, fromIr);
            compared += fromStatic.Count;
        }

        Assert.True(
            compared > 0,
            "VACUOUS: no page in this fixture rendered a work graph, so this test proved nothing. The fixture " +
            "must keep at least one epic whose stories carry provenance (Story 1.1 'Builds toward Story 2.1'). " +
            "Fix the fixture rather than deleting this guard.");
    }

    // ===== Story 22.6: the watch-mode delta sidecar =========================================================

    private string DeltaFile => Path.Combine(Site, SpaDelivery.DeltaPath.Replace('/', Path.DirectorySeparatorChar));

    private JsonElement ReadDelta() => JsonDocument.Parse(File.ReadAllText(DeltaFile)).RootElement;

    private static string[] DeltaArr(JsonElement delta, string name) =>
        delta.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();

    /// <summary>A generator wired the way <see cref="WatchCommand.RunWatchLoop"/> wires one: <c>--spa</c> on and
    /// the sidecar enabled BEFORE the first pass, so the session's basis and sequence start where the watch loop's
    /// would.</summary>
    private SiteGenerator WatchingSite()
    {
        var gen = new SiteGenerator(Options(spa: true)) { EmitDeltaSidecar = true };
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen;
    }

    /// <summary>AC #2 + AC #4, and the NFR9 hazard the story's Trap 7 names: a cold one-shot <c>generate --spa</c>
    /// must emit NO delta at all. The document carries a wall-clock <c>generatedAt</c> by nature, so emitting one
    /// "helpfully" on a cold build is exactly how a timestamp reaches a CI artifact and a byte-reproducible build
    /// stops being byte-reproducible.</summary>
    [Fact]
    public void OneShotGenerateWithSpa_EmitsNoDeltaSidecar_SoAColdBuildStaysReproducible()
    {
        GeneratedSite(spa: true);

        Assert.False(File.Exists(DeltaFile));
        Assert.DoesNotContain(
            SpaDelivery.DeltaPath,
            Directory.EnumerateFiles(Site, "*.json", SearchOption.AllDirectories)
                .Select(p => PathUtil.NormalizeSlashes(Path.GetRelativePath(Site, p))));
    }

    /// <summary>The gate is the SIDECAR SWITCH and nothing else — re-pointed by Story 23.6 AC #6.
    /// <para>This asserted that a generator with the switch ON but <c>--spa</c> off wrote no delta, on the
    /// grounds that it "emits no IR at all, so there is nothing to diff". That reasoning is void: the IR is now
    /// emitted unconditionally, so there IS something to diff on every run. The assertion that still matters —
    /// and the one NFR9 depends on — is the converse: <c>--spa</c> being off must not be what keeps the delta
    /// out, because a one-shot <c>generate</c> has to stay byte-reproducible and the delta carries a wall
    /// clock. So this now pins the switch as the ONLY gate, in both directions.</para></summary>
    [Fact]
    public void DeltaSidecar_IsGatedOnTheSwitchAlone_NotOnTheRetiredSpaFlag()
    {
        // Switch OFF, retired flag off: no delta, because the SWITCH is off.
        var oneShot = new SiteGenerator(Options(spa: false));
        Assert.DoesNotContain(oneShot.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        Assert.False(File.Exists(DeltaFile), "a one-shot generate must write no delta — NFR9 byte-reproducibility.");

        // Switch ON, retired flag still off: the delta IS written. Before Story 23.6 this produced nothing, and
        // the reason was the IR being absent rather than the switch — a gate that agreed with the intended
        // behaviour for the wrong reason.
        var watching = new SiteGenerator(Options(spa: false)) { EmitDeltaSidecar = true };
        Assert.DoesNotContain(watching.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        Assert.True(File.Exists(DeltaFile));
    }

    /// <summary>AC #7's first degrade condition, made real rather than dead code: the first emit of a watch
    /// session has no basis to diff against, so it is a <c>full</c> marker with EMPTY lists. A polling consumer
    /// attaching mid-session reads it and knows to refetch.</summary>
    [Fact]
    public void FirstEmitOfAWatchSession_IsAFullMarker_WithEmptyLists()
    {
        WatchingSite();

        var delta = ReadDelta();
        Assert.True(delta.GetProperty("full").GetBoolean());
        Assert.Empty(DeltaArr(delta, "changed"));
        Assert.Empty(DeltaArr(delta, "added"));
        Assert.Empty(DeltaArr(delta, "removed"));
        Assert.Empty(DeltaArr(delta, "chunks"));
        Assert.Equal(1, delta.GetProperty("sequence").GetInt64());
        Assert.Equal(SpaDelivery.DeltaSchemaVersion, delta.GetProperty("deltaSchemaVersion").GetInt32());
        Assert.Equal(SpaDelivery.SchemaVersion, delta.GetProperty("schemaVersion").GetInt32());
    }

    /// <summary>Task 3's core claim: two consecutive watch regens produce a sidecar whose contents are EXACTLY
    /// what <see cref="SpaDelivery.BuildDelta"/> computes from the two manifests. Asserted by recomputing the
    /// delta from the emitted manifests rather than by re-stating the expected page list — a hand-written
    /// expectation would drift from the emitter, and this is the seam where the two must agree.</summary>
    [Fact]
    public void ASecondWatchRegen_WritesADeltaMatchingBuildDelta_OverTheTwoEmittedManifests()
    {
        var gen = WatchingSite();
        var manifestPath = Path.Combine(Site, SpaDelivery.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        var manifestAfterFirst = File.ReadAllText(manifestPath);

        var doc = Path.Combine(Source, "planning-artifacts", "a-doc.md");
        File.WriteAllText(doc, "# A Doc\n\nOriginal body.\n");
        gen.SetWatchTrigger("_bmad-output/planning-artifacts/a-doc.md");
        Assert.NotEqual(GenerationOutcome.Error, gen.GenerateOne(doc).Outcome);

        var manifestAfterSecond = File.ReadAllText(manifestPath);
        var expected = SpaDelivery.BuildDelta(
            manifestAfterFirst, manifestAfterSecond, 2,
            "_bmad-output/planning-artifacts/a-doc.md", DateTimeOffset.UnixEpoch);

        var actual = ReadDelta();
        var expectedDoc = JsonDocument.Parse(expected).RootElement;

        Assert.False(actual.GetProperty("full").GetBoolean());
        Assert.Equal(DeltaArr(expectedDoc, "changed"), DeltaArr(actual, "changed"));
        Assert.Equal(DeltaArr(expectedDoc, "added"), DeltaArr(actual, "added"));
        Assert.Equal(DeltaArr(expectedDoc, "removed"), DeltaArr(actual, "removed"));
        Assert.Equal(DeltaArr(expectedDoc, "chunks"), DeltaArr(actual, "chunks"));

        // The new page is genuinely in it — otherwise the four equalities above could all hold vacuously.
        Assert.Contains("planning-artifacts/a-doc.html", DeltaArr(actual, "added"));
        Assert.Equal(2, actual.GetProperty("sequence").GetInt64());
        Assert.Equal("_bmad-output/planning-artifacts/a-doc.md", actual.GetProperty("trigger").GetString());
    }

    /// <summary>The sequence is monotonic within a session so a polling consumer can detect a MISSED delta (a gap
    /// ⇒ refetch) without comparing clocks. Three emits ⇒ 1, 2, 3 — never reset, never repeated.</summary>
    [Fact]
    public void DeltaSequence_IsMonotonicAcrossAWatchSession()
    {
        var gen = WatchingSite();
        Assert.Equal(1, ReadDelta().GetProperty("sequence").GetInt64());

        var doc = Path.Combine(Source, "planning-artifacts", "seq.md");
        File.WriteAllText(doc, "# Seq\n\nOne.\n");
        gen.GenerateOne(doc);
        Assert.Equal(2, ReadDelta().GetProperty("sequence").GetInt64());

        File.WriteAllText(doc, "# Seq\n\nTwo.\n");
        gen.GenerateOne(doc);
        Assert.Equal(3, ReadDelta().GetProperty("sequence").GetInt64());
    }

    /// <summary>AC #7 + Trap 5: a topology escalation is a whole-site rebuild, and a literal page diff there would
    /// produce a thousand-entry <c>changed</c> list — larger and slower than the full payload it was meant to
    /// replace. It must reach the degrade-to-full branch, and it must be labelled with the SHARED constant rather
    /// than a third spelling of "directory change".</summary>
    [Fact]
    public void ATopologyEscalation_DegradesToFull_AndCarriesTheSharedLabel()
    {
        var gen = WatchingSite();

        gen.SetWatchTrigger(FileWatcherService.TopologyEventLabel);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);

        var delta = ReadDelta();
        Assert.True(delta.GetProperty("full").GetBoolean());
        Assert.Empty(DeltaArr(delta, "changed"));
        Assert.Equal(FileWatcherService.TopologyEventLabel, delta.GetProperty("trigger").GetString());
    }

    /// <summary>⚠ REGRESSION GUARD for a defect Story 22.6's LIVE verification caught and no unit test had.
    ///
    /// <para>The first implementation derived the sidecar's degrade-to-full from the trigger LABEL
    /// (<c>trigger == "&lt;directory change&gt;"</c>). During Task 8 a concurrent session's save re-set that label
    /// between <c>RegenerateTopology</c> setting it and the emit reading it — the watch log printed
    /// <c>&lt;directory change&gt; full rebuild</c> while the sidecar written in the same second read
    /// <c>"full": false</c> with the sibling's path as its trigger. The label is racy BY CONSTRUCTION (one
    /// debounce Timer per changed path, each on its own thread-pool thread), so any correctness decision derived
    /// from it is defeatable exactly that way.</para>
    ///
    /// <para>This reproduces the race deterministically — overwrite the label to a plausible sibling path AFTER
    /// the topology route has been entered is not reachable from a test, so it does the strictly harder thing:
    /// sets the label to an ordinary file path and calls <c>RegenerateTopology</c> anyway. Under the old
    /// label-derived logic that produces <c>full: false</c>; under the flag the route sets on itself it stays
    /// <c>full: true</c>.</para></summary>
    [Fact]
    public void ATopologyRebuild_StillDegradesToFull_WhenAConcurrentSaveOverwroteTheTriggerLabel()
    {
        var gen = WatchingSite();

        // The label says "an ordinary file changed" — exactly what a concurrent debounce pass would have left
        // behind. The route must not believe it.
        gen.SetWatchTrigger("_bmad-output/planning-artifacts/some-sibling.md");
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);

        var delta = ReadDelta();
        Assert.True(
            delta.GetProperty("full").GetBoolean(),
            "a topology rebuild emitted a NON-full delta because the trigger label had been overwritten — the "
            + "degrade must come from the route's own flag, never from the racy label");
        Assert.Empty(DeltaArr(delta, "changed"));
    }

    /// <summary>The other half of the same fix: the full-delta flag is consumed EXACTLY once. A topology rebuild
    /// must not leave it armed so that some later, unrelated incremental emit degrades to full for no reason —
    /// which would quietly undo the whole point of the story on every regen after a directory change.</summary>
    [Fact]
    public void TheFullDeltaFlag_IsConsumedOnce_SoTheNextIncrementalEmitIsStillADelta()
    {
        var gen = WatchingSite();
        gen.SetWatchTrigger(FileWatcherService.TopologyEventLabel);
        Assert.NotEqual(GenerationOutcome.Error, gen.RegenerateTopology().Outcome);
        Assert.True(ReadDelta().GetProperty("full").GetBoolean());

        var doc = Path.Combine(Source, "planning-artifacts", "after-topology.md");
        File.WriteAllText(doc, "# After Topology\n\nAn ordinary content change.\n");
        gen.SetWatchTrigger("_bmad-output/planning-artifacts/after-topology.md");
        gen.GenerateOne(doc);

        var delta = ReadDelta();
        Assert.False(
            delta.GetProperty("full").GetBoolean(),
            "the full-delta flag stayed armed past its own emit, so an ordinary edit degraded to a full refetch");
        Assert.Contains("planning-artifacts/after-topology.html", DeltaArr(delta, "added"));
    }

    /// <summary>AC #2's atomicity requirement, verified by what it leaves behind rather than by racing a reader:
    /// the write goes to a temp file and is MOVED over the target, so no <c>.tmp</c> survives a successful emit
    /// and the file that exists is always a complete, parseable document.</summary>
    [Fact]
    public void DeltaSidecar_IsWrittenAtomically_LeavingNoTempFileBehind()
    {
        var gen = WatchingSite();
        var doc = Path.Combine(Source, "planning-artifacts", "atomic.md");
        File.WriteAllText(doc, "# Atomic\n\nBody.\n");
        gen.GenerateOne(doc);

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(Site, SpaDelivery.ChunkDir), "*.tmp"));
        // Parseable, i.e. never observed torn.
        Assert.False(ReadDelta().GetProperty("full").GetBoolean());
    }

    /// <summary>Code review finding (Story 22.6): the delta sidecar is an OPTIONAL, additive, watch-only file
    /// (AC #2/#4) — a failure writing it must never turn an otherwise-successful IR/site emit into a reported
    /// <see cref="GenerationOutcome.Error"/>. Forces a real write failure (a directory sitting where the sidecar
    /// file needs to land, so <c>File.Move</c> cannot replace it) rather than asserting on the try/catch's
    /// existence.</summary>
    [Fact]
    public void ADeltaSidecarWriteFailure_DoesNotFailTheOtherwiseSuccessfulRoute()
    {
        var gen = WatchingSite();

        // A directory at the sidecar's own path makes File.Move(temp, full, overwrite: true) throw — a real,
        // reproducible I/O failure rather than a mocked one. WatchingSite()'s own first pass already wrote a
        // real delta.json file there, so it has to come out before a directory can take its place.
        File.Delete(DeltaFile);
        Directory.CreateDirectory(DeltaFile);

        var doc = Path.Combine(Source, "planning-artifacts", "sidecar-fail.md");
        File.WriteAllText(doc, "# Sidecar Fail\n\nBody.\n");
        var ev = gen.GenerateOne(doc);

        Assert.NotEqual(GenerationOutcome.Error, ev.Outcome);
        // The page itself still rendered — the failure was isolated to the sidecar, not the site.
        Assert.True(SiteRegion.Exists(Site, "planning-artifacts/sidecar-fail.html"));
    }

    /// <summary>⚠ THE Task 1 FINDING, pinned so it cannot silently regress. The story's Trap 2 said not to advance
    /// the delta basis on a <see cref="GenerationOutcome.Skipped"/> outcome "because the generator's in-memory
    /// state is unchanged". That premise is FALSE for <see cref="SiteGenerator.RegenerateFromDataSource"/>, which
    /// calls <see cref="SiteGenerator.GenerateAll"/> on its first line and only afterwards inspects the events to
    /// decide what to report — so an unparseable data source returns <c>Skipped</c> having already rewritten the
    /// entire IR. Capturing the basis at the EMIT seam (rather than gating it on the reported outcome) is what
    /// makes this correct: the emit happened, so the basis advanced, so the NEXT delta is computed against what
    /// is actually on disk. A basis gated on the outcome would emit a false "unchanged" here — the failure AC #7
    /// names as worse than a false "changed".</summary>
    [Fact]
    public void ASkippedOutcomeThatStillReEmittedTheIr_StillAdvancesTheDeltaBasis()
    {
        var gen = WatchingSite();

        // A data source that does not parse: the route rebuilds everything, then reports Skipped.
        var dataSource = Path.Combine(Source, "implementation-artifacts", "sprint-status.yaml");
        File.WriteAllText(dataSource, ":\n  this is not valid yaml at all\n    - [\n");
        var ev = gen.RegenerateFromDataSource(dataSource);

        // The premise under test — if this route ever stops reporting Skipped here, the trap is gone and this
        // test is measuring nothing, so fail loudly rather than passing vacuously.
        Assert.Equal(GenerationOutcome.Skipped, ev.Outcome);

        // The basis advanced anyway (sequence moved), which is the whole point.
        Assert.Equal(2, ReadDelta().GetProperty("sequence").GetInt64());

        // And the NEXT real edit diffs against THAT emit, naming only the newly added page — not a stale basis's
        // worth of spurious changes, and not a false "nothing changed".
        var doc = Path.Combine(Source, "planning-artifacts", "after-skip.md");
        File.WriteAllText(doc, "# After Skip\n\nBody.\n");
        gen.SetWatchTrigger("_bmad-output/planning-artifacts/after-skip.md");
        gen.GenerateOne(doc);

        var delta = ReadDelta();
        Assert.False(delta.GetProperty("full").GetBoolean());
        Assert.Contains("planning-artifacts/after-skip.html", DeltaArr(delta, "added"));
    }

    // ===== Story 22.6 AC #5: the Quiet Stamp ================================================================

    /// <summary>AC #5: the stamp is in the INITIAL server-rendered markup, so it is not a JS-only artifact — with
    /// JS off it still reads, and it reads the honest state ("unavailable", because nothing is updating it).</summary>
    [Fact]
    public void TheEntryShell_CarriesTheQuietStamp_InServerRenderedMarkup()
    {
        GeneratedSite(spa: true);

        var shell = File.ReadAllText(Path.Combine(Site, SpaDelivery.EntryFileName));

        Assert.Contains($"id=\"{SpaDelivery.LiveStampId}\"", shell);
        Assert.Contains("Live updates: unavailable", shell);
    }

    /// <summary>AC #5's exclusion, and AC #4's byte-identity guard in one. A static page has NO live channel, so
    /// claiming one would be a lie — and putting the stamp in the shared <c>PathUtil.RenderHeadOpen</c> (the
    /// obvious place) would both do that AND move every page's bytes, breaking the
    /// <c>GoldenContentFingerprint</c> gate.</summary>
    [Fact]
    public void NoStaticPage_CarriesTheQuietStamp()
    {
        GeneratedSite(spa: true);

        // [Story 23.6 AC #8] Over the IR's regions. `StaticHtmlPages()` is the route set now, so the guard
        // below matters: an empty route set would sweep nothing and pass.
        Assert.NotEmpty(StaticHtmlPages());
        foreach (var page in StaticHtmlPages())
        {
            var region = SiteRegion.Read(Site, page);
            Assert.DoesNotContain(SpaDelivery.LiveStampId, region);
            Assert.DoesNotContain("Live updates:", region);
        }
    }

    /// <summary>CLAUDE.md § Verification — no state may be signalled by color alone. The stamp's two states differ
    /// in their WORDS, so they read identically to a screen reader, a monochrome display and a colorblind reader.
    /// Pinned structurally: the markup carries no inline color, no status token, and no motion.</summary>
    [Fact]
    public void TheQuietStamp_ConveysStateAsText_NeverByColorOrMotion()
    {
        // The at-rest state names itself in words rather than relying on a swatch.
        Assert.Contains("Live updates: unavailable", SpaDelivery.LiveStampMarkup);

        // No color-only signalling: no inline style, no --status-* token, no state-carrying class suffix.
        Assert.DoesNotContain("style=", SpaDelivery.LiveStampMarkup);
        Assert.DoesNotContain("--status-", SpaDelivery.LiveStampMarkup);
        // No motion: the direction is deliberately motionless, so no --motion-* token rides along.
        Assert.DoesNotContain("--motion-", SpaDelivery.LiveStampMarkup);

        // Announced to assistive tech without stealing focus.
        Assert.Contains("role=\"status\"", SpaDelivery.LiveStampMarkup);
        Assert.Contains("aria-live=\"polite\"", SpaDelivery.LiveStampMarkup);
    }
}
