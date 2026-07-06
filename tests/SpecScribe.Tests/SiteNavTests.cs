using SpecScribe;

namespace SpecScribe.Tests;

public class SiteNavTests
{
    [Fact]
    public void Build_OmitsMissingArtifactClasses()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        Assert.Equal(new[] { "Home", "Epics", "Requirements" }, nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void Build_IncludesSourceDerivedAndAdrLinksWhenAvailable()
    {
        var nav = SiteNav.Build(new[]
        {
            "gdd.md",
            "narrative-design.md",
            "game-architecture.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", hasAdrs: true);

        Assert.Equal(
            new[] { "Home", "GDD", "Narrative", "Game Architecture", "ADRs", "Epics", "Requirements" },
            nav.Items.Select(i => i.Label).ToArray());
    }

    [Fact]
    public void RenderNavBar_AddsMobileToggleAndActivePageSemantics()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true);

        var html = nav.RenderNavBar(SiteNav.RequirementsOutputPath);

        Assert.Contains("class=\"site-nav-toggle\"", html);
        Assert.Contains("aria-controls=\"site-nav-links\"", html);
        Assert.Contains("aria-current=\"page\">Requirements</a>", html);
    }

    [Fact]
    public void RenderBreadcrumb_UsesRelativePathsAndMarksCurrentPage()
    {
        var html = SiteNav.RenderBreadcrumb("epics/stories/1-1.html", new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Epics", SiteNav.EpicsOutputPath),
            ("Story 1.1", null),
        });

        Assert.Contains("href=\"../../index.html\"", html);
        Assert.Contains("href=\"../../epics.html\"", html);
        Assert.Contains("class=\"crumb-current\" aria-current=\"page\">Story 1.1</span>", html);
    }
}
