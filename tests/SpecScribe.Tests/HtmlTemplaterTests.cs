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
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.GameDevStudio), hasAdrs: true);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("dashboard-quick-links", html);
        Assert.Contains("href=\"epics.html\"", html);
        Assert.Contains("href=\"requirements.html\"", html);
        Assert.Contains("href=\"adrs/index.html\"", html);
        Assert.Contains("href=\"game-architecture.html\"", html);
    }

    [Fact]
    public void RenderIndex_ShowsReadmeQuickLinkWhenReadmeAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: true);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("dashboard-quick-links", html);
        Assert.Contains("href=\"readme.html\"", html);
        Assert.Contains("Read the project overview.", html);
    }

    [Fact]
    public void RenderIndex_EmitsSkipLinkAndSingleMainLandmark()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // Skip link is first-focusable and targets the one main landmark. [Story 1.4 AC #1, UX-DR16]
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderIndex_ProgressBarsCarryProgressbarAria()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The dashboard's overall-progress bars must expose progressbar semantics with a current value. [Story 1.4 AC #1]
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=", html);
        Assert.Contains("aria-valuemin=\"0\"", html);
        Assert.Contains("aria-valuemax=\"100\"", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
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
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.DoesNotContain("dashboard-quick-links", html);
        Assert.DoesNotContain("href=\"epics.html\"", html);
        Assert.DoesNotContain("href=\"requirements.html\"", html);
        Assert.DoesNotContain("href=\"adrs/index.html\"", html);
    }
}
