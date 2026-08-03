using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.7 AC #4 coverage: the <see cref="JsonSpaRenderAdapter"/> (the third concrete
/// <see cref="IRenderAdapter"/>, surface id <c>spa</c>) runs against the SAME parity harness the HTML and webview
/// surfaces do — 6.1's chrome facts and 6.2's section facts. Because the SPA ships the SAME C#-rendered content,
/// section parity holds with ZERO exceptions, and — being a real browser that keeps specscribe.css/specscribe.js —
/// its asset carriers MATCH the html surface too (no asset.css/asset.js exception, unlike the webview). Its ONE
/// sanctioned divergence is Mermaid (the roadmap init can't survive an innerHTML swap). The chrome facts are
/// checked on the SPA's EFFECTIVE served page — the shared entry shell (which loads css/js) wrapping the page's
/// content region — because that is what a browser actually renders. [Story 6.7]</summary>
public class RenderSpaParityTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    /// <summary>The page as the SPA serves it: the shared entry shell (head with the real specscribe.css +
    /// specscribe.js) wrapping this page's content region — the string a browser paints. FindDivergences reads its
    /// asset carriers from the shell head and its nav/breadcrumb/drill/status from the region.</summary>
    private static string ServedPage(PageView page) =>
        SpaDelivery.BuildEntryShell("SpecScribe", JsonSpaRenderAdapter.Shared.RenderContent(page));

    /// <summary>A representative epic page: drill children + a status badge in the body (mermaid optional).</summary>
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
            (mermaidNeeded ? "<pre class=\"mermaid\">\ngraph TD; A--&gt;B;\n</pre>\n" : string.Empty) +
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
    public void ServedPage_HasFullChromeParity_WithNoExceptionNeeded()
    {
        // A non-mermaid epic page: the served page reproduces every chrome fact — nav graph, breadcrumb/drill,
        // status, children — AND its asset carriers (specscribe.css/js in the shell head) match the html surface,
        // so ZERO divergences with no exception filtering at all. This is the SPA's advantage over the webview.
        var page = EpicPage(Nav());
        var served = ServedPage(page);

        var divergences = RenderParity.FindDivergences(page, served, "spa", Array.Empty<HostRenderException>());
        Assert.True(divergences.Count == 0, "expected parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void ServedPage_AssetCarriers_MatchTheHtmlSurface()
    {
        // Explicit proof the SPA keeps the real stylesheet + enhancement script (unlike the webview, which inlines
        // CSS and drops the script): the served page references both by their canonical names.
        var served = ServedPage(EpicPage(Nav()));
        Assert.Contains("<link rel=\"stylesheet\" href=\"" + ForgeOptions.StylesheetName, served);
        Assert.Contains("<script src=\"" + ForgeOptions.ScriptName, served);
    }

    [Fact]
    public void MermaidPage_DegradesUnderTheOneRegisteredException()
    {
        // [Story 23.6 AC #1/#8] ⚠️ THIS NO LONGER PROVES AN EXCEPTION IS LOAD-BEARING, BECAUSE THERE IS NO
        // EXCEPTION LEFT — and the reason is worth keeping rather than deleting the test.
        //
        // It used to assert that the SPA's served page carried no `mermaid.initialize` where the static page did,
        // and that the registry silenced exactly that. No C# surface emits an init now (the deleted
        // `HtmlRenderAdapter.Render` was the only one; it is the renderer's, keyed on `chromeNeeds().needsMermaid`),
        // so the divergence cannot arise and the SPA's registry entry was retired with the webview's three.
        //
        // What survives, and is asserted here, is the invariant that ACTUALLY matters to a reader: the SPA serves
        // a page that carries the diagram SOURCE and no executable init, and it holds FULL parity with its page
        // view — no divergence at all, sanctioned or otherwise. That last line is the strongest form of this
        // test's original claim, since it now passes against an EMPTY registry rather than one tuned to excuse a
        // known gap.
        var page = EpicPage(Nav(), mermaidNeeded: true);
        var served = ServedPage(page);

        Assert.DoesNotContain("mermaid.initialize", served);
        Assert.Contains(Mermaid.BlockMarker, served);
        Assert.Empty(RenderParity.FindDivergences(page, served, "spa", Array.Empty<HostRenderException>()));
        Assert.Empty(RenderParity.FindDivergences(page, served, "spa"));
    }

    [Fact]
    public void FindDivergences_StillCatchesAnUnregisteredSpaDivergence()
    {
        // The registry must never blanket-silence the SPA: a dropped drill child (not a registered fact) surfaces
        // exactly as it would for the HTML surface.
        var real = EpicPage(Nav());
        var served = SpaDelivery.BuildEntryShell("SpecScribe", JsonSpaRenderAdapter.Shared.RenderContent(
            real with { BodyHtml = "<main id=\"main-content\">\n<a href=\"../epics/story-1-1.html\">1.1</a>\n</main>\n\n" }));

        var divergences = RenderParity.FindDivergences(real, served, "spa");
        Assert.Contains(divergences, d => d.StartsWith("drill.child", StringComparison.Ordinal));
    }

    // ----- Section parity (AC #4, 6.2 facts) — zero exceptions ------------------------------------------------

    [Fact]
    public void DashboardContent_HasFullSectionParity_UnderSpa()
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

        var content = JsonSpaRenderAdapter.Shared.RenderContent(page);
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(view), RenderParity.ExtractDashboardSection(content), "spa");
        Assert.True(divergences.Count == 0, "expected section parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void EpicsIndexAndEpicPageContent_HaveFullSectionParity_UnderSpa()
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
        var indexContent = JsonSpaRenderAdapter.Shared.RenderContent(EpicsTemplater.BuildIndexPage(model, ProgressModel.Empty, Nav(), CommandCatalog.Empty));
        Assert.Empty(RenderParity.FindSectionDivergences(
            RenderParity.FromEpicsIndexView(indexView), RenderParity.ExtractEpicsIndexSection(indexContent), "spa"));

        var progress = new EpicProgress
        {
            Number = 1, Title = "Foundation", StoryCount = 2, StoriesWithArtifact = 1,
            TasksDone = 1, TasksTotal = 2, Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int>(),
        };
        var epicView = EpicsViewBuilder.BuildEpic(epic, progress, CommandCatalog.Empty, epicRetroPath: null);
        var epicContent = JsonSpaRenderAdapter.Shared.RenderContent(EpicsTemplater.BuildEpicPage(epic, progress, Nav(), CommandCatalog.Empty, epicRetroPath: null));
        Assert.Empty(RenderParity.FindSectionDivergences(
            RenderParity.FromEpicPageView(epicView), RenderParity.ExtractEpicPageSection(epicContent), "spa"));
    }

    // ----- Registry hygiene (AC #4: the SPA adds exactly one justified chrome exception) ----------------------

    [Fact]
    public void Registry_CarriesNoSpaException_BecauseTheServedRegionMatchesItsPageView()
    {
        // [Story 23.6 AC #1] ⚠️ INVERTED. This pinned the SPA's single justified `mermaid` exception: the served
        // page carried no `mermaid.initialize` where the static page did, so the roadmap degraded to readable
        // preformatted text.
        //
        // No C# surface emits that init any more — it was `HtmlRenderAdapter.Render`'s, and it is the renderer's
        // now — so the divergence cannot arise and the entry was retired. The registry is empty, which is the
        // honest consequence of ADR 0024: every C# surface projects ONE composed region, so they cannot disagree
        // on a region fact, and every remaining difference lives in chrome, which C# no longer produces.
        //
        // Asserted as emptiness rather than deleted, so that re-adding an exception without a divergence to
        // justify it fails here — the direction the registry's own "a registered divergence that no longer
        // exists is a bug" contract can actually catch.
        Assert.Empty(HostRenderExceptions.Registry.Where(e => e.SurfaceId == "spa"));
    }
}
