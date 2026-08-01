using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.4 AC #4 coverage: the <see cref="WebviewRenderAdapter"/> (the second concrete
/// <see cref="IRenderAdapter"/>, surface id <c>webview</c>) runs against the SAME parity harness the HTML surface
/// does — 6.1's chrome facts and 6.2's section facts — and diverges ONLY on the three facts registered in
/// <see cref="HostRenderExceptions.Registry"/> — since ADR 0036 just two: inlined CSS and CSP-blocked Mermaid.
/// Also pins the webview document contract ADR 0005 ratified and ADR 0032 restated: strict CSP (unchanged), the
/// two host-runtime placeholders, and a content region free of EXECUTABLE script — which since ADR 0036 means the
/// three nonce'd chrome scripts live in the shell while inert data islands ride in the region.
/// [Story 6.4; amended ADR 0036]</summary>
public class WebviewRenderAdapterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    /// <summary>The same representative epic page RenderParityTests uses: drill children + a status badge in the
    /// body, so the chrome checks exercise real markup.</summary>
    private static PageView EpicPage(SiteNav nav, bool mermaidNeeded = false)
    {
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", null),
        });
        var body =
            "<main id=\"main-content\">\n" +
            StatusStyles.Badge("active", "In development") + "\n" +
            "<a href=\"../epics/story-1-1.html\">Story 1.1</a>\n" +
            "<a href=\"../epics/story-1-2.html\">Story 1.2</a>\n" +
            "</main>\n\n";

        return new PageView
        {
            Kind = PageKind.Epic,
            OutputRelativePath = "epics/epic-1.html",
            Title = "Epic 1: Foundation — SpecScribe",
            Nav = nav.ToNavigationView("epics/epic-1.html"),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = "../" + ForgeOptions.StylesheetName,
                ScriptHref = "../" + ForgeOptions.ScriptName,
                MermaidNeeded = mermaidNeeded,
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = new[] { "epics/story-1-1.html", "epics/story-1-2.html" },
                StatusStage = "active",
            },
            BodyHtml = body,
        };
    }

    // ----- Chrome parity (AC #4, 6.1 facts) -------------------------------------------------------------------

    [Fact]
    public void Render_HasFullChromeParity_UnderTheRegisteredExceptions()
    {
        var page = EpicPage(Nav());
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        // The webview reproduces every chrome fact — nav graph, breadcrumb/drill, status, children — with only
        // the registered asset divergences (inlined CSS, no enhancement script) filtered as sanctioned.
        var divergences = RenderParity.FindDivergences(page, doc, WebviewRenderAdapter.Shared.Id);
        Assert.True(divergences.Count == 0, "expected parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void Render_WithoutTheRegistry_TheAssetDivergencesSurface()
    {
        // Proves the registered exceptions are load-bearing, not vacuous: unfiltered, the webview's inlined-CSS
        // and absent-script deltas ARE divergences the harness catches.
        var page = EpicPage(Nav());
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        var unfiltered = RenderParity.FindDivergences(page, doc, "webview", Array.Empty<HostRenderException>());
        Assert.Contains(unfiltered, d => d.StartsWith("asset.css", StringComparison.Ordinal));
        Assert.Contains(unfiltered, d => d.StartsWith("asset.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MermaidPage_DegradesUnderTheRegisteredException()
    {
        // A page that NEEDS Mermaid (the epics index always does) renders no init script under the webview CSP —
        // a divergence without the registry, sanctioned with it (ADR 0005's accepted text fallback).
        var page = EpicPage(Nav(), mermaidNeeded: true);
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        Assert.DoesNotContain("mermaid.initialize", doc);
        Assert.Contains(
            RenderParity.FindDivergences(page, doc, "webview", Array.Empty<HostRenderException>()),
            d => d.StartsWith("mermaid", StringComparison.Ordinal));
        Assert.DoesNotContain(
            RenderParity.FindDivergences(page, doc, "webview"),
            d => d.StartsWith("mermaid", StringComparison.Ordinal));
    }

    [Fact]
    public void FindDivergences_StillCatchesAnUnregisteredWebviewDivergence()
    {
        // The registry must never blanket-silence the webview: a dropped drill child (not a registered fact)
        // surfaces exactly as it would for the HTML surface.
        var real = EpicPage(Nav());
        var doc = WebviewRenderAdapter.Shared.Render(
            real with { BodyHtml = "<main id=\"main-content\">\n<a href=\"../epics/story-1-1.html\">1.1</a>\n</main>\n\n" }).Content;

        var divergences = RenderParity.FindDivergences(real, doc, "webview");
        Assert.Contains(divergences, d => d.StartsWith("drill.child", StringComparison.Ordinal));
    }

    /// <summary>A drafted story page (surface family #4): breadcrumb up to its epic + a status badge, no drill
    /// children — the webview must reproduce this chrome exactly like the HTML surface.</summary>
    private static PageView StoryPage(SiteNav nav)
    {
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", "epics/epic-1.html"),
            ("Story 1.1", null),
        });
        var body =
            "<main id=\"main-content\">\n" +
            StatusStyles.Badge("done", "Done") + "\n" +
            "</main>\n\n";

        return new PageView
        {
            Kind = PageKind.Story,
            OutputRelativePath = "epics/story-1-1.html",
            Title = "Story 1.1: Foundation — SpecScribe",
            Nav = nav.ToNavigationView("epics/story-1-1.html"),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = "../" + ForgeOptions.StylesheetName,
                ScriptHref = "../" + ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = Array.Empty<string>(),
                StatusStage = "done",
            },
            BodyHtml = body,
        };
    }

    /// <summary>An undrafted story's placeholder page (surface family #5): same chrome, but no status stage and no
    /// drill children — it must still reach FULL parity (the body facts are trivially satisfied).</summary>
    private static PageView StoryPlaceholderPage(SiteNav nav)
    {
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", "epics/epic-1.html"),
            ("Story 1.2", null),
        });

        return new PageView
        {
            Kind = PageKind.Story,
            OutputRelativePath = "epics/story-1-2.html",
            Title = "Story 1.2: Undrafted — SpecScribe",
            Nav = nav.ToNavigationView("epics/story-1-2.html"),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = "../" + ForgeOptions.StylesheetName,
                ScriptHref = "../" + ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = Array.Empty<string>(),
                StatusStage = null,
            },
            BodyHtml = "<main id=\"main-content\">\n<p>Not yet drafted.</p>\n</main>\n\n",
        };
    }

    [Fact]
    public void Render_StoryPage_HasFullChromeParity_UnderTheRegisteredExceptions()
    {
        // Closes the AC #4 gap the review flagged: story pages (surface family #4) now run through FindDivergences,
        // not just the dashboard/epics-index/epic-page trio.
        var page = StoryPage(Nav());
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        var divergences = RenderParity.FindDivergences(page, doc, WebviewRenderAdapter.Shared.Id);
        Assert.True(divergences.Count == 0, "expected story-page parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void Render_StoryPlaceholder_HasFullChromeParity_UnderTheRegisteredExceptions()
    {
        // …and the placeholder page (surface family #5): a no-status, no-children page must still reach parity.
        var page = StoryPlaceholderPage(Nav());
        var doc = WebviewRenderAdapter.Shared.Render(page).Content;

        var divergences = RenderParity.FindDivergences(page, doc, WebviewRenderAdapter.Shared.Id);
        Assert.True(divergences.Count == 0, "expected placeholder parity, got: " + string.Join(" | ", divergences));
    }

    // ----- Section parity (AC #4, 6.2 facts) ------------------------------------------------------------------

    [Fact]
    public void DashboardContent_HasFullSectionParity()
    {
        var view = new DashboardView
        {
            SiteTitle = "SpecScribe",
            StatTiles = new[] { new StatTile("3/5", "Epics drafted"), new StatTile("12", "Stories defined") },
            Commands = CommandCatalog.Empty,
            Progress = ProgressModel.Empty,
            ProgressBars = new[] { new ProgressBarView("Planning", 3, 5, "3 / 5 epics") },
            QuickLinks = new[] { new NavQuickLink("Epics", "epics.html", "All epics & stories") },
            Work = WorkInventory.Empty,
            OpenRetroActionItems = 0,
            Counts = ProjectCounts.Empty,
        };
        var page = new PageView
        {
            Kind = PageKind.Home,
            OutputRelativePath = SiteNav.HomeOutputPath,
            Title = "SpecScribe — Project Dashboard",
            Nav = Nav().ToNavigationView(SiteNav.HomeOutputPath),
            Breadcrumb = BreadcrumbTrail.Empty,
            Assets = new AssetManifest { StylesheetHref = ForgeOptions.StylesheetName, ScriptHref = ForgeOptions.ScriptName, MermaidNeeded = false },
            Interaction = new InteractionState { ChildTargets = new[] { SiteNav.EpicsOutputPath } },
            BodyHtml = HtmlRenderAdapter.Shared.RenderDashboardBody(view),
        };

        // The webview content region carries the byte-identical body, so the section facts it evidences equal
        // what the view model declares — under the webview surface id, with NO section exception needed.
        var content = WebviewRenderAdapter.Shared.RenderContent(page);
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(view), RenderParity.ExtractDashboardSection(content), "webview");
        Assert.True(divergences.Count == 0, "expected section parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void EpicsIndexAndEpicPageContent_HaveFullSectionParity()
    {
        var stories = new[]
        {
            new StoryInfo
            {
                Id = "1.1", EpicNumber = 1, Title = "Story 1.1", UserStoryHtml = "<p>As a user…</p>",
                AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-1-1.html",
                Status = "in-progress", TasksDone = 1, TasksTotal = 2,
            },
            new StoryInfo
            {
                Id = "1.2", EpicNumber = 1, Title = "Story 1.2", UserStoryHtml = "<p>As a user…</p>",
                AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = null, Status = null,
            },
        };
        var epic = new EpicInfo
        {
            Number = 1, Title = "Foundation", GoalHtml = string.Empty,
            Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice, Stories = stories,
        };
        var model = new EpicsModel { OverviewHtml = string.Empty, RequirementsInventoryHtml = string.Empty, Epics = new[] { epic } };

        var indexView = EpicsViewBuilder.BuildIndex(model, ProgressModel.Empty, Nav(), CommandCatalog.Empty);
        var indexPage = EpicsTemplater.BuildIndexPage(model, ProgressModel.Empty, Nav(), CommandCatalog.Empty);
        var indexContent = WebviewRenderAdapter.Shared.RenderContent(indexPage);
        Assert.Empty(RenderParity.FindSectionDivergences(
            RenderParity.FromEpicsIndexView(indexView), RenderParity.ExtractEpicsIndexSection(indexContent), "webview"));

        var progress = new EpicProgress
        {
            Number = 1, Title = "Foundation", StoryCount = 2, StoriesWithArtifact = 1,
            TasksDone = 1, TasksTotal = 2, Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int>(),
        };
        var epicView = EpicsViewBuilder.BuildEpic(epic, progress, CommandCatalog.Empty, epicRetroPath: null);
        var epicPage = EpicsTemplater.BuildEpicPage(epic, progress, Nav(), CommandCatalog.Empty, epicRetroPath: null);
        var epicContent = WebviewRenderAdapter.Shared.RenderContent(epicPage);
        Assert.Empty(RenderParity.FindSectionDivergences(
            RenderParity.FromEpicPageView(epicView), RenderParity.ExtractEpicPageSection(epicContent), "webview"));
    }

    // ----- The webview document contract (ADR 0005) ----------------------------------------------------------

    [Fact]
    public void Render_EmitsTheCspLockedShellWithTheTwoHostPlaceholders()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        Assert.StartsWith("<!DOCTYPE html>", doc);
        // The security-critical lock: default-deny, script nonce-locked (never 'unsafe-inline' for scripts),
        // styles 'unsafe-inline' for the render's inline style attributes — ADR 0005's measured posture.
        Assert.Contains("Content-Security-Policy", doc);
        Assert.Contains("default-src 'none'", doc);
        Assert.Contains("script-src 'nonce-__NONCE__'", doc);
        Assert.Contains("style-src 'unsafe-inline' __CSP_SOURCE__", doc);
        Assert.DoesNotContain("script-src 'unsafe-inline'", doc);
        // Exactly the two host-runtime placeholders the thin shim substitutes — the two-value seam that keeps
        // the shim dumb.
        Assert.Contains("__CSP_SOURCE__", doc);
        Assert.Contains("<script nonce=\"__NONCE__\">", doc);
    }

    [Fact]
    public void Render_InlinesTheStylesheet_AndShipsNoExternalAssetTags()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // The production CSS travels inline (no <link> can load under the CSP without asWebviewUri plumbing)…
        Assert.DoesNotContain("<link rel=\"stylesheet\"", doc);
        Assert.Contains("<style>", doc);
        Assert.Contains("--status-", doc); // a token only the real specscribe.css carries
        // …and no EXTERNAL script is referenced. Since ADR 0036 the webview does ship specscribe.js and the chart
        // engine, but both are INLINED under the nonce for the same reason the CSS is: `localResourceRoots` is
        // empty, so nothing can load from disk and a <script src> would simply fail. The ?v= cache-bust scheme is
        // meaningless here either way.
        Assert.DoesNotContain("<script src=", doc);
        Assert.DoesNotContain("?v=", doc);
    }

    [Fact]
    public void Render_CarriesExactlyThreeNoncedScripts_BridgeEngineAndApp()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // THREE <script> tags since ADR 0036, and every one of them nonce'd: the bridge, the vendored chart
        // engine, and the production specscribe.js. This used to assert exactly one.
        //
        // What has NOT changed, and is the point of counting rather than merely checking presence: the HTML
        // surface's inline nav-toggle script must still never be emitted (the CSP would silently block it, and the
        // bridge owns the toggle instead), and nothing may sneak in un-nonced. So the count is pinned AND every
        // occurrence is required to carry the nonce — a bare `<script>` would satisfy a presence check and be
        // dead on arrival in the panel.
        Assert.Equal(3, Count(doc, "<script"));
        Assert.Equal(3, Count(doc, "<script nonce=\"__NONCE__\">"));

        Assert.Contains("acquireVsCodeApi", doc);
        Assert.Contains("postMessage", doc);
    }

    [Fact]
    public void Render_InlinesTheChartEngineAndTheUnforkedAppScript()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // ADR 0036 §1: the engine ships INLINE, not as a <script src> — `localResourceRoots` is empty by design,
        // so a src would simply fail to load. Pinned by a marker only the real vendored bundle carries.
        Assert.Contains("Plotly", doc, StringComparison.Ordinal);
        // ADR 0036 §2 (do not fork the mount logic): the real specscribe.js is what ships, so the Explorer and the
        // Story 24.2 graph mount through the identical code path a browser uses. `initHierarchyExplorers` is
        // defined only in that file, so its presence proves the production script travelled — not a webview copy.
        Assert.Contains("initHierarchyExplorers", doc, StringComparison.Ordinal);
        Assert.Contains("specscribe:content-swapped", doc, StringComparison.Ordinal);
    }

    /// <summary>The shim substitutes <c>__NONCE__</c>/<c>__CSP_SOURCE__</c> across the SHELL (it lifts the content
    /// region out first, so a region can never forge a nonce). ADR 0036 put ~1.4 MB of vendored JavaScript INTO
    /// that shell — so if either asset happened to contain a placeholder token, the shim would rewrite bytes
    /// inside a minified bundle. Cheap to assert, silent and baffling if it ever came true.</summary>
    [Fact]
    public void InlinedAssets_ContainNeitherHostPlaceholderToken()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // The shell legitimately contains each token; what must not happen is the ASSETS contributing extra
        // occurrences. __CSP_SOURCE__ appears 3× (img-src, style-src, font-src) and __NONCE__ 4× (script-src plus
        // the three script tags) — all of them in the CSP meta and the tags, none from the ~1.4 MB of inlined JS.
        Assert.Equal(3, Count(doc, "__CSP_SOURCE__"));
        Assert.Equal(4, Count(doc, "__NONCE__"));

        // ALL FIVE placeholders, not just the two the shim substitutes. `__CONTENT__` is the dangerous one and was
        // originally missed: `WrapDocument` replaces it AFTER ~1.4 MB of vendored JavaScript is already in the
        // string, so an engine bundle that happened to contain that literal would have the page's content region
        // spliced into the middle of a minified script — silent, and baffling to diagnose. All five must be fully
        // consumed by the time the document is built.
        foreach (var token in new[] { "__TITLE__", "__PATH__", "__SOURCE__", "__ENGINE_JS__", "__APP_JS__", "__CONTENT__", "__CSS__", "__THEME_CSS__", "__HELPER_PROMPT__" })
        {
            Assert.False(doc.Contains(token, StringComparison.Ordinal),
                $"{token} survived into the rendered document — an inlined asset almost certainly reintroduced it.");
        }

        // And the closing tag that would end either inline block early. Clean in both vendored assets today;
        // nothing but this assertion stops a future re-vendor from breaking it.
        Assert.Equal(3, Count(doc, "</script>"));
    }

    [Fact]
    public void RenderContent_CarriesNavBreadcrumbAndBody_WithNoScriptAtAll()
    {
        var page = EpicPage(Nav());
        var content = WebviewRenderAdapter.Shared.RenderContent(page);

        // The swappable region carries the interaction chrome (nav + breadcrumb travel WITH the content so each
        // surface swap updates active-nav and the drill trail) and the body verbatim…
        Assert.Contains("<nav class=\"site-nav\"", content);
        Assert.Contains("<div class=\"breadcrumb\"", content);
        Assert.Contains(page.BodyHtml, content);
        // …and carries no EXECUTABLE script: innerHTML swaps never run one, so anything executable in here would
        // be dead weight at best and a parity lie at worst. (Inert application/json islands ARE permitted since
        // ADR 0036 — this fixture's body simply has none, which is why the blanket check still reads correctly
        // here. The island case is pinned by RenderContent_KeepsInertDataIslands below.)
        Assert.DoesNotContain("<script", content);
    }

    /// <summary>ADR 0036: the region ships data islands VERBATIM. This is the single most load-bearing assertion
    /// of that decision — the island is the chart's only data source, and stripping it is exactly what made the
    /// sunburst render as a legend with no chart. A regression here is silent: the page still renders, the panel
    /// still navigates, and only the chart quietly disappears.</summary>
    [Fact]
    public void RenderContent_KeepsInertDataIslands()
    {
        var page = EpicPage(Nav()) with
        {
            BodyHtml =
                "<main id=\"main-content\">\n" +
                "<div class=\"ss-hierarchy\" id=\"h1\" data-hierarchy></div>\n" +
                "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"h1-data\">{\"nodes\":[]}</script>\n" +
                "</main>\n",
        };

        var content = WebviewRenderAdapter.Shared.RenderContent(page);

        Assert.Contains("<script type=\"application/json\"", content);
        Assert.Contains("{\"nodes\":[]}", content);
        // The host the engine mounts into must survive too — an island with no host is as useless as a host with
        // no island.
        Assert.Contains("data-hierarchy", content);
        // Still no EXECUTABLE script: the island is the only <script> in the region.
        Assert.Equal(Count(content, "<script"), Count(content, "<script type=\"application/json\""));
    }

    [Fact]
    public void RenderContent_WithPager_CarriesTheCoherentWayfindingStrip_StillNoScript()
    {
        // Story 10.11: the pager rides the SAME chrome-level wayfinding strip as the breadcrumb in webview too —
        // just never the active-section tracking script, which only ever lands at the HtmlRenderAdapter.Render
        // chrome level (never inside PageView.BodyHtml), so it can't reach this swappable region at all.
        var page = EpicPage(Nav()) with
        {
            Pager = new EntityPager(new PagerLink("../epic-1.html", "Epic 1"), null),
        };

        var content = WebviewRenderAdapter.Shared.RenderContent(page);

        Assert.Contains("page-wayfinding", content);
        Assert.Contains("entity-pager", content);
        Assert.DoesNotContain("<script", content);
    }

    [Fact]
    public void Render_StampsTheSurfacePathTheBridgeResolvesLinksAgainst()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;
        // The bridge resolves relative hrefs (e.g. "story-1-1.html" from an epics/ page) against data-path;
        // data-source is empty here (Render wraps with no source — the Story 6.10 reveal button stays hidden).
        Assert.Contains("<div id=\"specscribe-surface\" data-path=\"epics/epic-1.html\" data-source=\"\">", doc);
    }

    // ----- Deferred item, Story 6.4 review: nav-toggle keyboard/focus parity -----------------------------------

    [Fact]
    public void Render_NavToggleBridge_HasKeyboardAndFocusParityWithTheHtmlScript()
    {
        // The HTML surface's inline NavToggleScript is CSP-blocked here, so the bridge script must reimplement
        // its keyboard/focus affordances rather than just class-toggle + aria-expanded (the gap the 6.4 parallel
        // adversarial review flagged and routed to a follow-up).
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // Opening focuses the first nav link.
        Assert.Contains("firstLink.focus()", doc);
        // Escape closes the open nav and returns focus to its toggle button.
        Assert.Contains("e.key !== 'Escape'", doc);
        Assert.Contains("toggleBtn.focus()", doc);
    }

    // ----- Registry hygiene (AC #4: every entry justified, none blanket) --------------------------------------

    [Fact]
    public void Registry_CarriesExactlyTheJustifiedWebviewChromeExceptions()
    {
        // The three ADR 0005 measured, plus `data-island`, plus Story 20.7's `hierarchy-chart` — all
        // webview-scoped, all chrome/asset facts, each with a real reason. No html-surface entry (the HTML adapter
        // still diverges on nothing) and no section.* entry (the body facts hold FULL parity).
        // Story 6.7's SPA surface adds its own single (mermaid) entry, asserted separately in RenderSpaParityTests.
        //
        // `data-island`: the webview strips inline JSON data islands — an ASSET-WEIGHT divergence, because the
        // island is unreadable here (this surface ships no specscribe.js; see asset.js).
        //
        // `hierarchy-chart` [Story 20.7, owner decision D3]: with no script there is no Plotly, and Story 20.7
        // retired the server-rendered SVG that used to stand in its place — so this surface shows the TEXT TWIN and
        // no chart picture. It is the fallback ADR 0012 §5 and ADR 0013 §7 both pre-authorize, and it is REGISTERED
        // rather than left silent precisely because the thing that makes it a documented degradation instead of a
        // hole is that the twin survives with its links. The ADR 0005 CSP amendment that would let Plotly load here
        // lands once, with Story 23.4.
        // ADR 0036 retired TWO of the five — `data-island` (regions ship verbatim; the island is live chart data
        // now, not dead weight) and `hierarchy-chart` (charts mount here, so there is no missing picture) — and
        // NARROWED `asset.js`. What remains is two CARRIER differences plus one CSP casualty.
        //
        // `asset.js` stays because the parity fact is the `<script src="..." defer>` TAG, not the behaviour: the
        // webview inlines specscribe.js instead of referencing it, so the fact genuinely still differs even though
        // nothing is missing any more. Exactly the shape asset.css has always had.
        //
        // Pinned as an exact set, not a count: a registry that keeps entries for divergences that no longer exist
        // is as misleading as one missing an entry that does, and only an exact-set assertion catches both.
        var webview = HostRenderExceptions.Registry.Where(e => e.SurfaceId == "webview").ToList();
        Assert.Equal(3, webview.Count);
        Assert.All(webview, e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        Assert.Equal(
            new[] { "asset.css", "asset.js", "mermaid" },
            webview.Select(e => e.FactId).OrderBy(f => f, StringComparer.Ordinal).ToList());
        // The narrowed asset.js reason must state that the script is INLINED. Asserted positively rather than as
        // "does not say absent": the reason deliberately quotes its own superseded wording to explain the change,
        // so a negative check on that phrase would fail on the very text that documents it correctly.
        var assetJs = webview.Single(e => e.FactId == "asset.js");
        Assert.Contains("inlines", assetJs.Reason, StringComparison.OrdinalIgnoreCase);
        // Global hygiene across every surface: a section.* fact may never be excepted (a body divergence is
        // always a bug).
        Assert.DoesNotContain(HostRenderExceptions.Registry, e => e.FactId.StartsWith("section.", StringComparison.Ordinal));
    }

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
