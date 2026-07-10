using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>AC #2 coverage: the semantic-parity harness detects whether a rendered surface dropped or
/// reinterpreted a semantic fact of its source <see cref="PageView"/>. Story 6.1's only surface is the
/// <see cref="HtmlRenderAdapter"/>, so its output must show FULL parity, and an injected divergence must be
/// caught — proving the harness genuinely detects regressions (not a byte-only check that proves nothing new).
/// This is the exact hook Story 6.2's webview adapter runs against. [Story 6.1]</summary>
public class RenderParityTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    /// <summary>An epic page carrying real drill-down child links + a status badge in its (opaque) body, so the
    /// harness's child/status presence checks exercise the real markup.</summary>
    private static PageView EpicPage(SiteNav nav)
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
                MermaidNeeded = false,
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

    [Fact]
    public void HtmlAdapterOutput_HasFullParityWithItsPageView()
    {
        var page = EpicPage(Nav());
        var html = HtmlRenderAdapter.Shared.Render(page).Content;

        // The HTML adapter reproduces every semantic fact — nav graph, breadcrumb/drill, assets, status, children.
        var divergences = RenderParity.FindDivergences(page, html, "html");
        Assert.True(divergences.Count == 0, "expected parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void Extract_RecoversTheDrillAndAssetFacts()
    {
        var page = EpicPage(Nav());
        var facts = RenderParity.Extract(HtmlRenderAdapter.Shared.Render(page).Content, page);

        Assert.Equal("SpecScribe", facts.SiteTitle);
        Assert.Equal(SiteNav.EpicsOutputPath, facts.ParentDrillTarget); // drill-up recovered from the breadcrumb
        Assert.Equal(new[] { "epics/story-1-1.html", "epics/story-1-2.html" }, facts.ChildDrillTargets);
        Assert.Equal("active", facts.StatusStage);
        Assert.Equal(ForgeOptions.StylesheetName, facts.Stylesheet); // "../" prefix + ?v= token folded away
        // The nav targets are recovered in order; an epic page is not itself a nav item, so nothing is active.
        Assert.Equal(new[] { "index.html", "readme.html", "adrs/index.html", "epics.html", "requirements.html" },
            facts.Nav.Select(n => n.Target).ToList());
        Assert.DoesNotContain(facts.Nav, n => n.Active);
    }

    [Fact]
    public void FindDivergences_CatchesADroppedChildLink()
    {
        // The rendered body links only story 1.1; the PageView (fake) claims a child the output doesn't contain.
        var real = EpicPage(Nav());
        var html = HtmlRenderAdapter.Shared.Render(
            real with { BodyHtml = "<main id=\"main-content\">\n<a href=\"../epics/story-1-1.html\">1.1</a>\n</main>\n\n" }).Content;

        var divergences = RenderParity.FindDivergences(real, html, "html");
        Assert.Contains(divergences, d => d.StartsWith("drill.child", StringComparison.Ordinal));
    }

    [Fact]
    public void FindDivergences_CatchesAMisreportedStatusStage()
    {
        var page = EpicPage(Nav());
        var html = HtmlRenderAdapter.Shared.Render(page).Content; // body renders a "active" badge

        // A PageView that claims the page is "done" when the rendered badge says "active" is a divergence.
        var lying = page with { Interaction = page.Interaction with { StatusStage = "done" } };
        var divergences = RenderParity.FindDivergences(lying, html, "html");
        Assert.Contains(divergences, d => d.StartsWith("status", StringComparison.Ordinal));
    }

    [Fact]
    public void FindDivergences_CatchesADroppedNavItem()
    {
        var page = EpicPage(Nav());
        var html = HtmlRenderAdapter.Shared.Render(page).Content;

        // Inject a nav item into the reference that the rendered bar never carried.
        var extraNav = page.Nav with { Items = page.Nav.Items.Append(new NavItem("Ghost", "ghost.html", "Ghost")).ToList() };
        var divergences = RenderParity.FindDivergences(page with { Nav = extraNav }, html, "html");
        Assert.Contains(divergences, d => d.StartsWith("nav", StringComparison.Ordinal));
    }

    [Fact]
    public void FindDivergences_HostRenderExceptionSilencesTheSanctionedDivergence()
    {
        var page = EpicPage(Nav());
        var html = HtmlRenderAdapter.Shared.Render(page).Content;
        var lying = page with { Interaction = page.Interaction with { StatusStage = "done" } };

        // Without an exception the status divergence surfaces…
        Assert.Contains(RenderParity.FindDivergences(lying, html, "webview"),
            d => d.StartsWith("status", StringComparison.Ordinal));

        // …but a registered host-specific exception for that surface + fact silences it (AC #2: differences are
        // documented exceptions only). A DIFFERENT surface's exception does not apply.
        var exceptions = new[] { new HostRenderException("webview", "status", "webview shows status in its own status bar") };
        Assert.DoesNotContain(RenderParity.FindDivergences(lying, html, "webview", exceptions),
            d => d.StartsWith("status", StringComparison.Ordinal));
        Assert.Contains(RenderParity.FindDivergences(lying, html, "html", exceptions),
            d => d.StartsWith("status", StringComparison.Ordinal));
    }

    [Fact]
    public void HostRenderExceptionRegistry_IsEmptyInStory61()
    {
        // The HTML adapter reproduces every fact, so there is nothing to except this story. A future surface
        // adds its host-specific exceptions here rather than drifting silently.
        Assert.Empty(HostRenderExceptions.Registry);
    }
}
