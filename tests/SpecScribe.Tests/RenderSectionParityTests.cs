using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.2 AC #3 coverage: the semantic-parity harness, extended from 6.1's shared CHROME facts down
/// into the two decomposed BODIES (dashboard + epics), detects whether a rendered surface dropped or reinterpreted
/// a SECTION fact (stat tiles, epic chips, story rows). The HTML adapter renders the section view models to their
/// exact facts (full parity), and an injected section divergence must be caught — proving the harness genuinely
/// detects section regressions, not just re-checks chrome. This is the hook Story 6.4's webview adapter runs
/// against. [Story 6.2]</summary>
public class RenderSectionParityTests
{
    // ----- Dashboard stat tiles -----------------------------------------------------------------------------

    private static DashboardView Dashboard(IReadOnlyList<StatTile> tiles) => new()
    {
        SiteTitle = "SpecScribe",
        StatTiles = tiles,
        Commands = CommandCatalog.Empty,
        Progress = ProgressModel.Empty,
        ProgressBars = new[]
        {
            new ProgressBarView("Planning", 0, 0, "0 / 0 epics"),
            new ProgressBarView("Implementation", 0, 0, "not started"),
        },
        QuickLinks = Array.Empty<NavQuickLink>(),
        Work = WorkInventory.Empty,
        OpenRetroActionItems = 0,
        Counts = ProjectCounts.Empty,
    };

    [Fact]
    public void Dashboard_StatTiles_HaveFullSectionParity()
    {
        var view = Dashboard(new[]
        {
            new StatTile("3/5", "Epics drafted", Tooltip: "tip"),
            new StatTile("12", "Stories defined", "8 with a task plan"),
            new StatTile("—", "Commits", "no git history"),
        });
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(view), RenderParity.ExtractDashboardSection(body), "html");
        Assert.True(divergences.Count == 0, "expected section parity, got: " + string.Join(" | ", divergences));
    }

    [Fact]
    public void Dashboard_ExtractRecoversTileNumbersAndLabels()
    {
        var view = Dashboard(new[] { new StatTile("3/5", "Epics drafted"), new StatTile("12", "Stories defined") });
        var facts = RenderParity.ExtractDashboardSection(HtmlRenderAdapter.Shared.RenderDashboardBody(view));

        Assert.Equal(new[] { "3/5|Epics drafted", "12|Stories defined" }, facts.StatTiles);
    }

    [Fact]
    public void Dashboard_FindSectionDivergences_CatchesAMisreportedStatTile()
    {
        var view = Dashboard(new[] { new StatTile("3/5", "Epics drafted") });
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        // A reference that claims a tile value the rendered body never carried is a section divergence.
        var lying = view with { StatTiles = new[] { new StatTile("9/9", "Epics drafted") } };
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(lying), RenderParity.ExtractDashboardSection(body), "html");
        Assert.Contains(divergences, d => d.StartsWith("section.statTiles", StringComparison.Ordinal));
    }

    // ----- Dashboard cards / panels / drill targets (AC #3 broadening) --------------------------------------

    /// <summary>A dashboard carrying every broadened section fact still on the home body: derived Now &amp; Next
    /// cards, Overall-Progress bars, and quick-link drill targets. The home index bands and quick-dev card grid
    /// were removed by spec-declutter-home-dashboard, so those facts are no longer projected. [spec-declutter-home-dashboard]</summary>
    private static DashboardView RichDashboard() => new()
    {
        SiteTitle = "SpecScribe",
        StatTiles = new[] { new StatTile("3/5", "Epics drafted") },
        Commands = CommandCatalog.Empty,
        Progress = ProgressModel.Empty,
        NowNext = new DashboardNowNext(SprintBoard: null, Cards: new[]
        {
            new NowNextCard("active", "In dev", "Story 1.1", "epics/story-1-1.html"),
            new NowNextCard("ready", "Up next", "Story 1.2", "epics/story-1-2.html"),
        }),
        ProgressBars = new[]
        {
            new ProgressBarView("Planning", 3, 5, "3 / 5 epics"),
            new ProgressBarView("Implementation", 1, 2),
        },
        QuickLinks = new[]
        {
            new NavQuickLink("Epics", "epics.html", "All epics & stories"),
            new NavQuickLink("Requirements", "requirements.html", "Requirement coverage"),
        },
        Work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Fix the footer", "quick/fix-footer.html", "done", "bugfix") },
            Deferred = null,
        },
        OpenRetroActionItems = 0,
        Counts = ProjectCounts.Empty,
    };

    [Fact]
    public void Dashboard_BroadenedSectionFacts_HaveFullParity()
    {
        var view = RichDashboard();
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        var expected = RenderParity.FromDashboardView(view);
        var actual = RenderParity.ExtractDashboardSection(body);

        Assert.Empty(RenderParity.FindSectionDivergences(expected, actual, "html"));

        // Each broadened fact is genuinely recovered from the rendered body — not merely both-empty.
        Assert.Equal(new[] { "active|In dev|Story 1.1|epics/story-1-1.html", "ready|Up next|Story 1.2|epics/story-1-2.html" }, actual.NowNextCards);
        Assert.Equal(new[] { "Planning|3 / 5 epics", "Implementation|1 / 2" }, actual.ProgressBars);
        Assert.Equal(new[] { "epics.html", "requirements.html" }, actual.QuickLinks);
    }

    [Fact]
    public void Dashboard_FindSectionDivergences_CatchesADroppedNowNextCard()
    {
        var view = RichDashboard();
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        var lying = view with { NowNext = new DashboardNowNext(null, view.NowNext!.Cards.Take(1).ToList()) };
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(lying), RenderParity.ExtractDashboardSection(body), "html");
        Assert.Contains(divergences, d => d.StartsWith("section.nowNextCards", StringComparison.Ordinal));
    }

    [Fact]
    public void Dashboard_FindSectionDivergences_CatchesADroppedQuickLink()
    {
        var view = RichDashboard();
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        var lying = view with { QuickLinks = view.QuickLinks.Take(1).ToList() };
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromDashboardView(lying), RenderParity.ExtractDashboardSection(body), "html");
        Assert.Contains(divergences, d => d.StartsWith("section.quickLinks", StringComparison.Ordinal));
    }

    [Fact]
    public void Dashboard_NoDerivedCards_EmitsNoNowNextCardFacts()
    {
        // When the derived Now & Next panel has no cards (the sprint-board mode renders no now-next-card either),
        // both the declared and evidenced now-next facts are empty — no manufactured divergence.
        var view = RichDashboard() with { NowNext = new DashboardNowNext(SprintBoard: null, Cards: Array.Empty<NowNextCard>()) };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);
        var actual = RenderParity.ExtractDashboardSection(body);
        Assert.Empty(actual.NowNextCards);
    }

    // ----- Epics-index chips --------------------------------------------------------------------------------

    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    private static EpicInfo Epic(int number, string title, EpicStatus status, EpicSection section, IReadOnlyList<StoryInfo> stories) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = status,
        Section = section,
        Stories = stories,
    };

    [Fact]
    public void EpicsIndex_Chips_HaveFullSectionParity()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                Epic(1, "Foundation", EpicStatus.Drafted, EpicSection.VerticalSlice, Array.Empty<StoryInfo>()),
                Epic(2, "Growth", EpicStatus.Pending, EpicSection.FurtherDevelopment, Array.Empty<StoryInfo>()),
            },
        };
        var view = EpicsViewBuilder.BuildIndex(model, ProgressModel.Empty, Nav(), CommandCatalog.Empty);
        var body = HtmlRenderAdapter.Shared.RenderEpicsIndexBody(view);

        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromEpicsIndexView(view), RenderParity.ExtractEpicsIndexSection(body), "html");
        Assert.True(divergences.Count == 0, "expected chip parity, got: " + string.Join(" | ", divergences));

        // The chip rows are actually recovered (number|stage|href) and equal what the view declared — not
        // merely both-empty. (Both epics roll up via StatusStyles.ForEpic, so the exact stage word is whatever
        // that seam returns; the point is the rendered body evidences the same rows the view model declares.)
        var facts = RenderParity.ExtractEpicsIndexSection(body);
        Assert.Equal(2, facts.EpicChips.Count);
        Assert.Equal(RenderParity.FromEpicsIndexView(view).EpicChips, facts.EpicChips);
        Assert.All(facts.EpicChips, row => Assert.Matches(@"^\d+\|[a-z]+\|epics/epic-\d+\.html$", row));
    }

    [Fact]
    public void EpicsIndex_FindSectionDivergences_CatchesADroppedChip()
    {
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                Epic(1, "Foundation", EpicStatus.Drafted, EpicSection.VerticalSlice, Array.Empty<StoryInfo>()),
                Epic(2, "Growth", EpicStatus.Drafted, EpicSection.VerticalSlice, Array.Empty<StoryInfo>()),
            },
        };
        var view = EpicsViewBuilder.BuildIndex(model, ProgressModel.Empty, Nav(), CommandCatalog.Empty);
        var body = HtmlRenderAdapter.Shared.RenderEpicsIndexBody(view);

        // A reference declaring only one chip while the body rendered two → divergence.
        var lying = view with { VerticalSliceChips = view.VerticalSliceChips.Take(1).ToList() };
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromEpicsIndexView(lying), RenderParity.ExtractEpicsIndexSection(body), "html");
        Assert.Contains(divergences, d => d.StartsWith("section.epicChips", StringComparison.Ordinal));
    }

    // ----- Epic-page story rows -----------------------------------------------------------------------------

    private static StoryInfo Story(string id, string? status, string? artifact, int done = 0, int total = 0) => new()
    {
        Id = id,
        EpicNumber = 1,
        Title = "Story " + id,
        UserStoryHtml = "<p>As a user…</p>",
        AcBlocksHtml = Array.Empty<string>(),
        ArtifactOutputPath = artifact,
        Status = status,
        TasksDone = done,
        TasksTotal = total,
    };

    private static (EpicInfo Epic, EpicProgress Progress) EpicWithStories()
    {
        var stories = new[]
        {
            Story("1.1", status: "in-progress", artifact: "epics/story-1-1.html", done: 1, total: 2),
            Story("1.2", status: null, artifact: null),
        };
        var epic = Epic(1, "Foundation", EpicStatus.Drafted, EpicSection.VerticalSlice, stories);
        var progress = new EpicProgress
        {
            Number = 1, Title = "Foundation", StoryCount = 2, StoriesWithArtifact = 1,
            TasksDone = 1, TasksTotal = 2, Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int>(),
        };
        return (epic, progress);
    }

    [Fact]
    public void EpicPage_StoryRows_HaveFullSectionParity()
    {
        var (epic, progress) = EpicWithStories();
        var view = EpicsViewBuilder.BuildEpic(epic, progress, CommandCatalog.Empty, epicRetroPath: null);
        var body = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromEpicPageView(view), RenderParity.ExtractEpicPageSection(body), "html");
        Assert.True(divergences.Count == 0, "expected story-row parity, got: " + string.Join(" | ", divergences));

        // id | stage | drill href — the drafted story carries its stage, the undrafted one carries none.
        var facts = RenderParity.ExtractEpicPageSection(body);
        Assert.Equal(
            new[] { "story-1-1|active|epics/story-1-1.html", "story-1-2||epics/story-1-2.html" },
            facts.StoryRows);
    }

    [Fact]
    public void EpicPage_FindSectionDivergences_CatchesAMisreportedStoryStage()
    {
        var (epic, progress) = EpicWithStories();
        var view = EpicsViewBuilder.BuildEpic(epic, progress, CommandCatalog.Empty, epicRetroPath: null);
        var body = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        // A reference that flips the drafted story's stage to "done" while the body renders an "active" badge.
        var lyingCards = view.StoryCards.Select(c => c.Id == "1.1" ? c with { StatusStage = "done" } : c).ToList();
        var lying = view with { StoryCards = lyingCards };
        var divergences = RenderParity.FindSectionDivergences(
            RenderParity.FromEpicPageView(lying), RenderParity.ExtractEpicPageSection(body), "html");
        Assert.Contains(divergences, d => d.StartsWith("section.storyRows", StringComparison.Ordinal));
    }

    // ----- Sanctioned exceptions + registry -----------------------------------------------------------------

    [Fact]
    public void FindSectionDivergences_HostRenderExceptionSilencesTheSanctionedDivergence()
    {
        var view = Dashboard(new[] { new StatTile("3/5", "Epics drafted") });
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);
        var lying = view with { StatTiles = new[] { new StatTile("9/9", "Epics drafted") } };

        var expected = RenderParity.FromDashboardView(lying);
        var actual = RenderParity.ExtractDashboardSection(body);

        // Without an exception the stat-tile divergence surfaces for the webview surface…
        Assert.Contains(RenderParity.FindSectionDivergences(expected, actual, "webview"),
            d => d.StartsWith("section.statTiles", StringComparison.Ordinal));

        // …but a registered host-specific exception for that surface + fact silences it; a different surface's
        // exception does not apply.
        var exceptions = new[] { new HostRenderException("webview", "section.statTiles", "webview shows tiles in its own header") };
        Assert.DoesNotContain(RenderParity.FindSectionDivergences(expected, actual, "webview", exceptions),
            d => d.StartsWith("section.statTiles", StringComparison.Ordinal));
        Assert.Contains(RenderParity.FindSectionDivergences(expected, actual, "html", exceptions),
            d => d.StartsWith("section.statTiles", StringComparison.Ordinal));
    }

    [Fact]
    public void HostRenderExceptionRegistry_CarriesNoSectionFactEntry()
    {
        // Every surface — the webview included (its entries arrived with Story 6.4, exactly as this test
        // anticipated in 6.2) — renders the decomposed section facts to full parity: the registry may only carry
        // chrome/asset exceptions, never a section.* fact. A section divergence is always a bug.
        Assert.DoesNotContain(HostRenderExceptions.Registry, e => e.FactId.StartsWith("section.", StringComparison.Ordinal));
    }
}
