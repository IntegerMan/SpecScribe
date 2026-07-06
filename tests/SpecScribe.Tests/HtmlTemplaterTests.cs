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

    private static StoryInfo Story(string id, string? status) => new()
    {
        Id = id,
        EpicNumber = 1,
        Title = "A story",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
        TasksDone = 0,
        TasksTotal = 0,
    };

    private static EpicsModel ModelWith(EpicStatus epicStatus, params StoryInfo[] stories) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "First Epic",
                GoalHtml = string.Empty,
                Status = epicStatus,
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            },
        },
    };

    private static ProgressModel ProgressWithCommits(int totalCommits)
    {
        var day = new DateOnly(2026, 1, 5);
        return new ProgressModel
        {
            EpicsTotal = 1,
            EpicsDrafted = 1,
            EpicsPending = 0,
            StoriesTotal = 1,
            StoriesWithArtifact = 1,
            TasksDone = 0,
            TasksTotal = 0,
            PerEpic = Array.Empty<EpicProgress>(),
            Git = new GitPulse(totalCommits, 1, day, day, new (DateOnly, int)[] { (day, totalCommits) }),
        };
    }

    [Theory]
    [InlineData(1, "Commit")]
    [InlineData(7, "Commits")]
    public void RenderIndex_PluralizesCommitStatLabel(int totalCommits, string expectedLabel)
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithCommits(totalCommits),
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // "1 Commits" was the reported defect — the count-bearing label must agree in number. [Story 1.5 A2]
        Assert.Contains($"class=\"stat-label\">{expectedLabel}</div>", html);
    }

    [Fact]
    public void RenderIndex_EmitsFaviconDescriptionAndDashboardTitle()
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

        // Home <title> is descriptive; favicon + description/OG land so shared links aren't bare. [Story 1.5 G1/G2]
        Assert.Contains("<title>SpecScribe — Project Dashboard</title>", html);
        Assert.Contains("<link rel=\"icon\"", html);
        Assert.Contains("<meta name=\"description\"", html);
    }

    [Fact]
    public void RenderIndex_SurfacesNowAndNextAheadOfExploreKeyViews()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "ready for dev")),
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The most valuable panel leads; the slimmed link grid trails it. [Story 1.5 F1]
        var nowNext = html.IndexOf("Now &amp; Next", StringComparison.Ordinal);
        var explore = html.IndexOf("Explore Key Views", StringComparison.Ordinal);
        Assert.True(nowNext >= 0 && explore >= 0, "both panels should render");
        Assert.True(nowNext < explore, "Now & Next must appear before Explore Key Views");
    }

    [Fact]
    public void RenderIndex_QuickLinkPillsCarryFamilyAccents()
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

        // Slimmed to pills, accented by artifact family. [Story 1.5 F1/B5]
        Assert.Contains("quick-link-pill family-epics", html);
        Assert.Contains("quick-link-pill family-requirements", html);
    }

    [Fact]
    public void RenderIndex_EpicStatusDonutReflectsStoryRollup()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        // An epic with an in-development story rolls up to "active" — the donut must say "In development",
        // not the old binary drafted/pending split that contradicted the sunburst. [Story 1.5 A3]
        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "in progress")),
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("Epic Status", html);
        Assert.Contains("In development (1)", html);
        // Zero-count rows are suppressed (B4): no "Done (0)" noise.
        Assert.DoesNotContain("Done (0)", html);
    }

    [Fact]
    public void RenderEpic_EmitsSkipLinkAndSingleMainLandmark()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var epic = new EpicInfo
        {
            Number = 1,
            Title = "First Epic",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = Array.Empty<StoryInfo>(),
        };
        var progress = new EpicProgress
        {
            Number = 1,
            Title = "First Epic",
            StoryCount = 0,
            StoriesWithArtifact = 0,
            TasksDone = 0,
            TasksTotal = 0,
            Status = EpicStatus.Drafted,
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, CommandCatalog.Empty);

        // The heavily-refactored detail templater must carry the same skip link + exactly one main landmark
        // as the dashboard — guards against a duplicate/zero-landmark regression in RenderEpic. [Story 1.4 AC #1, UX-DR16]
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }
}
