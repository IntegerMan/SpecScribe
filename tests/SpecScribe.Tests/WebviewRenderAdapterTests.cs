using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.4 AC #4 coverage: the <see cref="WebviewRenderAdapter"/> (the second concrete
/// <see cref="IRenderAdapter"/>, surface id <c>webview</c>) runs against the SAME parity harness the HTML surface
/// does — 6.1's chrome facts and 6.2's section facts — and diverges ONLY on the three facts registered in
/// <see cref="HostRenderExceptions.Registry"/> (inlined CSS, absent enhancement script, CSP-blocked Mermaid).
/// Also pins the webview document contract ADR 0005 ratified: strict CSP, exactly one nonce'd bridge script, the
/// two host-runtime placeholders, and a script-free swappable content region. [Story 6.4]</summary>
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
        // …and no external script is referenced: the enhancement script is deliberately absent (the body must
        // reach the same information without it — the feature-parity rule; the inlined CSS may still MENTION it
        // in a comment, which is why this pins the tag, not the name) and the ?v= cache-bust scheme is
        // meaningless here.
        Assert.DoesNotContain("<script src=", doc);
        Assert.DoesNotContain("?v=", doc);
    }

    [Fact]
    public void Render_CarriesExactlyOneScript_TheNoncedBridge()
    {
        var doc = WebviewRenderAdapter.Shared.Render(EpicPage(Nav())).Content;

        // One <script> total — the nonce'd bridge. In particular the HTML surface's inline nav-toggle script
        // (which the CSP would silently block) must NOT be emitted; the bridge owns the toggle instead.
        Assert.Equal(1, Count(doc, "<script"));
        Assert.Contains("acquireVsCodeApi", doc);
        Assert.Contains("postMessage", doc);
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
        // …and is script-free: innerHTML swaps never execute scripts, so anything script-shaped in here would be
        // dead weight at best and a parity lie at worst.
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

    // ----- Registry hygiene (AC #4: every entry justified, none blanket) --------------------------------------

    [Fact]
    public void Registry_CarriesExactlyTheThreeJustifiedWebviewChromeExceptions()
    {
        // Exactly the three ADR 0005 measured — all webview-scoped, all chrome/asset facts, each with a real
        // reason. No html-surface entry (the HTML adapter still diverges on nothing) and no section.* entry
        // (the body facts hold FULL parity — the content is byte-identical by construction). Story 6.7's SPA
        // surface adds its own single (mermaid) entry, asserted separately in RenderSpaParityTests.
        var webview = HostRenderExceptions.Registry.Where(e => e.SurfaceId == "webview").ToList();
        Assert.Equal(3, webview.Count);
        Assert.All(webview, e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        Assert.Equal(
            new[] { "asset.css", "asset.js", "mermaid" },
            webview.Select(e => e.FactId).OrderBy(f => f, StringComparer.Ordinal).ToList());
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
