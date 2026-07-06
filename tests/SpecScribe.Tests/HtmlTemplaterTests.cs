using SpecScribe;

namespace SpecScribe.Tests;

public class HtmlTemplaterTests
{
    [Fact]
    public void RenderIndex_ShowsDashboardQuickLinksForAvailableSections()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/epics.md",
            "game-architecture.md",
        }, "SpecScribe", hasAdrs: true);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>());

        Assert.Contains("dashboard-quick-links", html);
        Assert.Contains("href=\"epics.html\"", html);
        Assert.Contains("href=\"requirements.html\"", html);
        Assert.Contains("href=\"adrs/index.html\"", html);
        Assert.Contains("href=\"game-architecture.html\"", html);
    }

    [Fact]
    public void RenderIndex_OmitsQuickLinksPanelWhenOnlyHomeExists()
    {
        var nav = SiteNav.Build(Array.Empty<string>(), "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>());

        Assert.DoesNotContain("dashboard-quick-links", html);
        Assert.DoesNotContain("href=\"epics.html\"", html);
        Assert.DoesNotContain("href=\"requirements.html\"", html);
        Assert.DoesNotContain("href=\"adrs/index.html\"", html);
    }
}
