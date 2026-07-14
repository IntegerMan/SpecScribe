using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for <see cref="HtmlRenderAdapter"/> — the first concrete <see cref="IRenderAdapter"/>. Pins
/// that it assembles the shared chrome around the opaque body in the exact order (and bytes) the templaters
/// produced inline, and that <see cref="SiteNav"/>'s delegating chrome helpers stayed byte-identical after the
/// re-homing. [Story 6.1]</summary>
public class HtmlRenderAdapterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);

    private static PageView StoryPage(SiteNav nav) => new()
    {
        Kind = PageKind.Story,
        OutputRelativePath = "epics/story-1-1.html",
        Title = "Story 1.1: Foundation — SpecScribe",
        Nav = nav.ToNavigationView("epics/story-1-1.html"),
        Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            ("1 · Foundation", "epics/epic-1.html"),
            ("Story 1.1", null),
        }),
        Assets = new AssetManifest
        {
            StylesheetHref = "../" + ForgeOptions.StylesheetName,
            ScriptHref = "../" + ForgeOptions.ScriptName,
            MermaidNeeded = false,
        },
        Interaction = new InteractionState { ParentTarget = "epics/epic-1.html", StatusStage = "active" },
        BodyHtml = "<main id=\"main-content\">\n<h1>Foundation</h1>\n</main>\n\n",
    };

    [Fact]
    public void Render_AssemblesChromeAroundBodyInOrder()
    {
        var html = HtmlRenderAdapter.Shared.Render(StoryPage(Nav())).Content;

        // Head → nav → breadcrumb → body → footer → close, in order.
        var doctype = html.IndexOf("<!DOCTYPE html>", StringComparison.Ordinal);
        var navIdx = html.IndexOf("<nav class=\"site-nav\"", StringComparison.Ordinal);
        var crumb = html.IndexOf("<div class=\"breadcrumb\"", StringComparison.Ordinal);
        var body = html.IndexOf("<main id=\"main-content\">", StringComparison.Ordinal);
        var footer = html.IndexOf("<footer class=\"doc-footer\">", StringComparison.Ordinal);
        var close = html.IndexOf("</body>\n</html>\n", StringComparison.Ordinal);
        Assert.True(doctype >= 0 && doctype < navIdx && navIdx < crumb && crumb < body && body < footer && footer < close,
            $"chrome order wrong: {doctype}/{navIdx}/{crumb}/{body}/{footer}/{close}");

        // The title + prefixed, cache-busted assets land in the head; the breadcrumb resolves the "../" prefix.
        Assert.Contains("<title>Story 1.1: Foundation — SpecScribe</title>", html);
        Assert.Contains($"href=\"../{ForgeOptions.StylesheetName}?v=", html);
        Assert.Contains($"src=\"../{ForgeOptions.ScriptName}?v=", html);
        Assert.Contains("href=\"../index.html\"", html); // Home crumb: epics/story-1-1.html is depth 1 → "../"
    }

    [Fact]
    public void Render_InjectsMermaidInitOnlyWhenManifestSaysSo()
    {
        var nav = Nav();
        var without = HtmlRenderAdapter.Shared.Render(StoryPage(nav)).Content;
        Assert.DoesNotContain("mermaid.initialize", without);

        var withMermaid = StoryPage(nav) with { Assets = StoryPage(nav).Assets with { MermaidNeeded = true } };
        var html = HtmlRenderAdapter.Shared.Render(withMermaid).Content;
        Assert.Contains("mermaid.initialize", html);
        // The init script sits after the footer, before the closing body tag (matching the templaters).
        Assert.True(html.IndexOf("<footer", StringComparison.Ordinal) < html.IndexOf("mermaid.initialize", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_EmptyBreadcrumb_EmitsNoBreadcrumbBlock()
    {
        var nav = Nav();
        var home = StoryPage(nav) with
        {
            Kind = PageKind.Home,
            OutputRelativePath = SiteNav.HomeOutputPath,
            Breadcrumb = BreadcrumbTrail.Empty,
            Nav = nav.ToNavigationView(SiteNav.HomeOutputPath),
        };
        var html = HtmlRenderAdapter.Shared.Render(home).Content;
        Assert.DoesNotContain("<div class=\"breadcrumb\"", html);
    }

    [Fact]
    public void RenderNav_MatchesSiteNavRenderNavBarByteForByte()
    {
        // The re-homed nav rendering and SiteNav's delegating wrapper must be byte-identical — the whole point
        // of "re-home, don't rewrite".
        var nav = Nav();
        var viaAdapter = HtmlRenderAdapter.Shared.RenderNav(nav.ToNavigationView(SiteNav.RequirementsOutputPath));
        var viaSiteNav = nav.RenderNavBar(SiteNav.RequirementsOutputPath);
        Assert.Equal(viaSiteNav, viaAdapter);
    }

    [Fact]
    public void RenderBreadcrumb_MatchesSiteNavRenderBreadcrumbByteForByte()
    {
        var trail = new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Epics", SiteNav.EpicsOutputPath),
            ("Story 1.1", null),
        };
        var viaAdapter = HtmlRenderAdapter.Shared.RenderBreadcrumb("epics/stories/1-1.html", BreadcrumbTrail.From(trail));
        var viaSiteNav = SiteNav.RenderBreadcrumb("epics/stories/1-1.html", trail);
        Assert.Equal(viaSiteNav, viaAdapter);
    }

    [Fact]
    public void Adapter_IdentifiesAsTheHtmlSurface()
    {
        Assert.Equal("html", HtmlRenderAdapter.Shared.Id);
    }

    [Fact]
    public void StoryPlaceholder_RendersUserStoryNoteAsOwnBlockAboveTheBlurb()
    {
        var view = new StoryPlaceholderView
        {
            Id = "1.1",
            TitleHtml = "Edge story",
            StatusStage = "drafted",
            RetroLinkHtml = string.Empty,
            UserStoryNoteHtml = "<aside class=\"md-comment\">seat note</aside>\n",
            UserStoryHtml = "<p>As a user…</p>",
            AcBlocksHtml = System.Array.Empty<string>(),
            NoteHtml = "<span>draft it</span>",
            EpicNumber = 1,
            BackHref = "../epics/epic-1.html",
        };

        var html = HtmlRenderAdapter.Shared.RenderStoryPlaceholderBody(view);

        // The note is its own block, ahead of the user-story blurb — not folded inside it.
        var noteIdx = html.IndexOf("md-comment", System.StringComparison.Ordinal);
        var blurbIdx = html.IndexOf("user-story", System.StringComparison.Ordinal);
        Assert.True(noteIdx >= 0 && blurbIdx >= 0 && noteIdx < blurbIdx);
        Assert.DoesNotContain("user-story\"><aside class=\"md-comment\"", html);
    }

    [Fact]
    public void StoryPlaceholder_OmitsNoteBlockWhenNoteIsEmpty()
    {
        var view = new StoryPlaceholderView
        {
            Id = "1.1",
            TitleHtml = "No-note story",
            StatusStage = "drafted",
            RetroLinkHtml = string.Empty,
            UserStoryHtml = "<p>As a user…</p>",
            AcBlocksHtml = System.Array.Empty<string>(),
            NoteHtml = "<span>draft it</span>",
            EpicNumber = 1,
            BackHref = "../epics/epic-1.html",
        };

        var html = HtmlRenderAdapter.Shared.RenderStoryPlaceholderBody(view);

        Assert.DoesNotContain("md-comment", html);
    }

    private static StoryCardView Card(
        string id = "1.1",
        string? status = "review",
        string statusStage = "review",
        int tasksDone = 5,
        int tasksTotal = 5) => new()
    {
        Id = id,
        TitleHtml = "Paired Progress Story",
        AnchorId = $"story-{id.Replace('.', '-')}",
        StatusStage = statusStage,
        Status = status,
        TasksDone = tasksDone,
        TasksTotal = tasksTotal,
        TitleHref = "epics/story-1-1.html",
        ViewPlanHref = "epics/story-1-1.html",
        UserStoryHtml = "<p>As a maintainer…</p>",
        AcBlocksHtml = Array.Empty<string>(),
    };

    private static EpicPageView EpicPage(params StoryCardView[] cards) => new()
    {
        Number = 1,
        TitleHtml = "Paired Progress",
        StatusClass = "active",
        StatusLabel = "In development",
        GoalHtml = string.Empty,
        HasStories = true,
        ProgressBars = Array.Empty<ProgressBarView>(),
        NextActionsPanelHtml = string.Empty,
        NextStepsHtml = string.Empty,
        RetroAffordanceHtml = string.Empty,
        UndraftedBannerHtml = string.Empty,
        Epic = new EpicInfo
        {
            Number = 1,
            Title = "Paired Progress",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = Array.Empty<StoryInfo>(),
        },
        Commands = CommandCatalog.Empty,
        Prefix = string.Empty,
        StoryCards = cards,
    };

    [Fact]
    public void RenderEpicBody_PairsStatusAndTaskBadgesWhenBothPresent()
    {
        var statusBadge = StatusStyles.Badge("review", "review");
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(EpicPage(Card()));

        Assert.Contains("class=\"story-status-pair\"", html);
        Assert.Contains("story-status-pair-sep", html);
        Assert.Contains(" · ", html);
        // Inner badge bytes unchanged — grouped, not rewritten.
        Assert.Contains(statusBadge, html);
        Assert.Contains("status-badge task-badge complete", html);
        Assert.Contains("&#10003; 5 tasks", html);
    }

    [Fact]
    public void RenderEpicBody_StatusOnly_EmitsBadgeWithoutPairOrOrphanSeparator()
    {
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(EpicPage(Card(tasksTotal: 0)));

        Assert.DoesNotContain("story-status-pair", html);
        Assert.DoesNotContain("story-status-pair-sep", html);
        Assert.Contains(StatusStyles.Badge("review", "review"), html);
        Assert.DoesNotContain("task-badge", html);
    }

    [Fact]
    public void RenderEpicBody_TasksOnly_EmitsTaskBadgeWithoutPairOrOrphanSeparator()
    {
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(EpicPage(Card(status: null, tasksDone: 2, tasksTotal: 4)));

        Assert.DoesNotContain("story-status-pair", html);
        Assert.DoesNotContain("story-status-pair-sep", html);
        Assert.Contains("2/4 tasks", html);
        // No lifecycle status badge — only the task badge.
        Assert.DoesNotContain("status-badge review", html);
    }

    // ---- Story 8.6 undrafted-banner consolidation ---------------------------------------------------------

    private static StoryInfo Undrafted(string id, string title) => new()
    {
        Id = id,
        EpicNumber = 1,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        ArtifactOutputPath = null,
    };

    private static StoryInfo Drafted(string id, string title) => new()
    {
        Id = id,
        EpicNumber = 1,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        ArtifactOutputPath = $"epics/story-{id.Replace('.', '-')}.html",
        Status = "ready-for-dev",
    };

    private static EpicInfo EpicWith(params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "Foundation",
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    private static EpicProgress ProgressFor(EpicInfo epic) => new()
    {
        Number = epic.Number,
        Title = epic.Title,
        StoryCount = epic.Stories.Count,
        StoriesWithArtifact = epic.Stories.Count(s => s.ArtifactOutputPath is not null),
        TasksDone = 0,
        TasksTotal = 0,
        Status = epic.Status,
        StoryStatusCounts = new Dictionary<string, int>(),
    };

    private static CommandCatalog CreateStoryCatalog() => new("BMad", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
    });

    [Fact]
    public void BuildEpic_TwoPlusUndrafted_EmitsOneBannerWithNextCreateStoryCommand()
    {
        var epic = EpicWith(Drafted("1.1", "A"), Undrafted("1.2", "B"), Undrafted("1.3", "C"));
        var view = EpicsViewBuilder.BuildEpic(epic, ProgressFor(epic), CreateStoryCatalog(), epicRetroPath: null);
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        Assert.Contains("class=\"epic-undrafted-banner\"", html);
        Assert.Equal(1, html.Split("class=\"epic-undrafted-banner\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("2 stories in this epic need task plans", html);
        Assert.Contains("data-copy=\"/bmad-create-story 1.2\"", html);
        Assert.DoesNotContain("data-copy=\"/bmad-create-story 1.3\"", html);
        Assert.Contains("class=\"not-detailed-note\">No detailed story plan yet.</div>", html);
        Assert.DoesNotContain("No detailed story plan yet — draft it with", html);
    }

    [Fact]
    public void BuildEpic_SingleUndrafted_KeepsInlineCommandAndNoBanner()
    {
        var epic = EpicWith(Drafted("1.1", "A"), Undrafted("1.2", "B"));
        var view = EpicsViewBuilder.BuildEpic(epic, ProgressFor(epic), CreateStoryCatalog(), epicRetroPath: null);
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        Assert.DoesNotContain("epic-undrafted-banner", html);
        Assert.Contains("No detailed story plan yet — draft it with", html);
        Assert.Contains("data-copy=\"/bmad-create-story 1.2\"", html);
    }

    [Fact]
    public void BuildEpic_TwoPlusUndrafted_WithoutCreateStory_DegradesToCountOnlyBanner()
    {
        // NFR8: catalog without create-story → count sentence, no hard-coded slash command. [Story 8.6]
        var epic = EpicWith(Undrafted("1.1", "A"), Undrafted("1.2", "B"));
        var view = EpicsViewBuilder.BuildEpic(epic, ProgressFor(epic), CommandCatalog.Empty, epicRetroPath: null);
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        Assert.Contains("class=\"epic-undrafted-banner\">2 stories in this epic need task plans.</div>", html);
        Assert.DoesNotContain("create-story", html);
        Assert.DoesNotContain("cmd-badge", html);
        Assert.Contains("class=\"not-detailed-note\">No detailed story plan yet.</div>", html);
    }

    // ---- Story 8.7: one primary view per dashboard dataset ------------------------------------------------

    private static RequirementInfo Fr(int number, int epic) => new()
    {
        Kind = RequirementKind.Functional,
        Number = number,
        TextHtml = "Requirement " + number,
        CoverageEpicNumber = epic,
        CoverageEpicNumbers = new[] { epic },
        Status = RequirementStatus.Active,
    };

    private static EpicsModel RequirementsEpics() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[] { EpicWith(Drafted("1.1", "A")) },
    };

    private static DashboardView DashboardWithRequirements(bool withEpics) => new()
    {
        SiteTitle = "SpecScribe",
        StatTiles = Array.Empty<StatTile>(),
        Commands = CommandCatalog.Empty,
        Progress = ProgressModel.Empty,
        ProgressBars = Array.Empty<ProgressBarView>(),
        QuickLinks = Array.Empty<NavQuickLink>(),
        Work = WorkInventory.Empty,
        OpenRetroActionItems = 0,
        Counts = ProjectCounts.Empty,
        IndexBands = Array.Empty<IndexBand>(),
        Requirements = new RequirementsModel { Functional = new[] { Fr(1, 1) }, NonFunctional = Array.Empty<RequirementInfo>() },
        Epics = withEpics ? RequirementsEpics() : null,
    };

    [Fact]
    public void RenderDashboardBody_RequirementsWithEpics_ConsolidatesBehindFlowFirstToggle()
    {
        // AC #1: the coverage flow is the single default-visible primary (checked), the status-block grid is the
        // demoted alternate, and the reused sprint radio-toggle chrome drives the switch. [Story 8.7]
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: true));

        // The panel-scoped toggle uses panel-unique ids/name (never the sprint radios), flow checked by default.
        Assert.Contains("<input type=\"radio\" id=\"rv-flow\" name=\"req-view\" class=\"board-tab-radio\" checked>", body);
        Assert.Contains("<input type=\"radio\" id=\"rv-grid\" name=\"req-view\" class=\"board-tab-radio\">", body);
        Assert.Contains("<label for=\"rv-flow\" class=\"board-tab\">Flow</label>", body);
        Assert.Contains("<label for=\"rv-grid\" class=\"board-tab\">Status grid</label>", body);

        // Both views live in the DOM, each in its own wrapper; the flow (role="img" SVG) renders BEFORE the grid.
        var flowWrap = body.IndexOf("<div class=\"req-view req-view-flow\">", StringComparison.Ordinal);
        var gridWrap = body.IndexOf("<div class=\"req-view req-view-grid\">", StringComparison.Ordinal);
        Assert.True(flowWrap >= 0 && gridWrap >= 0 && flowWrap < gridWrap, $"flow must render before grid: {flowWrap}/{gridWrap}");
        Assert.Contains("class=\"req-flow-svg\"", body);
        Assert.Contains("class=\"req-status-grid\"", body);
        Assert.True(body.IndexOf("class=\"req-flow-svg\"", StringComparison.Ordinal)
            < body.IndexOf("class=\"req-status-grid\"", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderDashboardBody_RequirementsWithoutEpics_RendersGridAloneWithNoToggle()
    {
        // AC #1 single-view branch: no flow → the status grid renders alone, no toggle, no .req-view wrappers.
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: false));

        Assert.Contains("class=\"req-status-grid\"", body); // text-twin still present (AC #2 guardrail)
        Assert.DoesNotContain("id=\"rv-flow\"", body);
        Assert.DoesNotContain("req-view-flow", body);
        Assert.DoesNotContain("req-view-grid", body);
        Assert.DoesNotContain("class=\"req-flow-svg\"", body);
    }

    [Fact]
    public void RenderDashboardBody_KeepsTheStatusBlockTextTwinInTheDom()
    {
        // AC #2 text-twin guardrail: the status-block grid is never removed whenever requirements exist,
        // whether or not the flow (and thus the toggle) is present.
        Assert.Contains("class=\"req-status-grid\"", HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: true)));
        Assert.Contains("class=\"req-status-grid\"", HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: false)));
    }

    [Fact]
    public void RenderDashboardBody_IsDeterministicForTheRequirementsPanel()
    {
        var a = HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: true));
        var b = HtmlRenderAdapter.Shared.RenderDashboardBody(DashboardWithRequirements(withEpics: true));
        Assert.Equal(a, b);
    }

    [Fact]
    public void RenderEpicsIndexBody_SubtitleNoLongerRestatesTheStatGridCounts()
    {
        // AC #2 dedup: the header subtitle drops the epic/drafted count restatement; the stat grid below stays
        // the single authoritative count display ("Epics drafted" + "Stories defined" tiles). [Story 8.7]
        var model = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                EpicWith(Drafted("1.1", "A")),
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: true, hasReadme: true);
        var view = EpicsViewBuilder.BuildIndex(model, ProgressModel.Empty, nav, CommandCatalog.Empty);
        var body = HtmlRenderAdapter.Shared.RenderEpicsIndexBody(view);

        // The subtitle keeps the site title only — the duplicated counts are gone.
        Assert.Contains("<div class=\"doc-subtitle\">SpecScribe</div>", body);
        // The old count restatement (a middot-joined "N epics · M with stories drafted") no longer ships.
        Assert.DoesNotContain("with stories drafted", body);

        // The stat grid remains the single count home.
        Assert.Contains("Epics drafted", body);
        Assert.Contains("Stories defined", body);
    }
}
