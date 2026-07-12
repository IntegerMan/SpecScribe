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
        // A page that NEEDS Mermaid renders no inline `mermaid.initialize` in the SPA (innerHTML swaps can't run an
        // injected init) — a divergence without the registry, sanctioned with it (the accepted text fallback).
        var page = EpicPage(Nav(), mermaidNeeded: true);
        var served = ServedPage(page);

        Assert.DoesNotContain("mermaid.initialize", served);
        Assert.Contains(
            RenderParity.FindDivergences(page, served, "spa", Array.Empty<HostRenderException>()),
            d => d.StartsWith("mermaid", StringComparison.Ordinal));
        // With the registry, mermaid is the ONLY candidate divergence and it is silenced — full parity.
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
            IndexBands = Array.Empty<IndexBand>(),
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
    public void Registry_CarriesExactlyOneJustifiedSpaException_Mermaid()
    {
        // The SPA is a real browser, so it keeps css/js — no asset.css/asset.js exception (its advantage over the
        // webview's three). Its ONE sanctioned divergence is mermaid, with a real reason and never a section fact.
        var spa = HostRenderExceptions.Registry.Where(e => e.SurfaceId == "spa").ToList();
        var single = Assert.Single(spa);
        Assert.Equal("mermaid", single.FactId);
        Assert.False(string.IsNullOrWhiteSpace(single.Reason));
        Assert.DoesNotContain(spa, e => e.FactId is "asset.css" or "asset.js");
        Assert.DoesNotContain(spa, e => e.FactId.StartsWith("section.", StringComparison.Ordinal));
    }
}
