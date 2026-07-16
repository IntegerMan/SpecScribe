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
        int tasksTotal = 5,
        DateOnly? updatedDate = null) => new()
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
        UpdatedDate = updatedDate,
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

    // ---- Story 8.8 story-card recency marker ---------------------------------------------------------------

    [Fact]
    public void RenderEpicBody_EmitsStoryCardUpdatedWhenUpdatedDateSet()
    {
        var day = new DateOnly(2026, 7, 9);
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(EpicPage(Card(updatedDate: day)));

        Assert.Contains("class=\"story-card-updated\"", html);
        Assert.Contains($"Updated {PortalDates.Day(day)}", html);
    }

    [Fact]
    public void RenderEpicBody_OmitsStoryCardUpdatedWhenUpdatedDateNull()
    {
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(EpicPage(Card(updatedDate: null)));

        Assert.DoesNotContain("story-card-updated", html);
        Assert.DoesNotContain("Updated ", html);
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
        Requirements = new RequirementsModel { Functional = new[] { Fr(1, 1) }, NonFunctional = Array.Empty<RequirementInfo>(), Design = Array.Empty<RequirementInfo>() },
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
        // Toggle + CTA share a wrap-friendly header aside (title | controls), mirroring Now & Next.
        Assert.Contains("class=\"req-panel-header-aside\"", body);

        // Both views live in the DOM, each in its own wrapper; the flow (role="img" SVG) renders BEFORE the grid.
        var flowWrap = body.IndexOf("<div class=\"req-view req-view-flow\">", StringComparison.Ordinal);
        var gridWrap = body.IndexOf("<div class=\"req-view req-view-grid\">", StringComparison.Ordinal);
        Assert.True(flowWrap >= 0 && gridWrap >= 0 && flowWrap < gridWrap, $"flow must render before grid: {flowWrap}/{gridWrap}");
        Assert.Contains("class=\"req-flow-svg\"", body);
        // Status grid must be nested inside the demoted wrapper (not a sibling outside it).
        Assert.Contains("<div class=\"req-view req-view-grid\"><div class=\"req-status-grid\">", body);
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
        // Pin the subtitle region itself so a partial middot count restatement cannot sneak back in.
        var subOpen = body.IndexOf("<div class=\"doc-subtitle\">", StringComparison.Ordinal);
        Assert.True(subOpen >= 0);
        var subClose = body.IndexOf("</div>", subOpen, StringComparison.Ordinal);
        var subtitle = body.Substring(subOpen, subClose - subOpen);
        Assert.DoesNotContain("&middot;", subtitle, StringComparison.Ordinal);
        Assert.DoesNotContain("epics", subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("with stories drafted", subtitle, StringComparison.Ordinal);

        // The stat grid remains the single count home.
        Assert.Contains("Epics drafted", body);
        Assert.Contains("Stories defined", body);
    }

    // ----- Work-section gate + declutter guarantees (spec-declutter-home-dashboard) --------------------------

    /// <summary>A minimal dashboard view with a configurable work inventory + open-retro count, everything else
    /// empty — the fixture the four work-section gate scenarios drive.</summary>
    private static DashboardView WorkDashboard(WorkInventory work, int openRetro) => new()
    {
        SiteTitle = "SpecScribe",
        StatTiles = Array.Empty<StatTile>(),
        Commands = CommandCatalog.Empty,
        Progress = ProgressModel.Empty,
        ProgressBars = Array.Empty<ProgressBarView>(),
        QuickLinks = Array.Empty<NavQuickLink>(),
        Work = work,
        OpenRetroActionItems = openRetro,
        Counts = ProjectCounts.Empty,
    };

    [Fact]
    public void RenderDashboardBody_QuickDevOnly_RendersNoWorkCards()
    {
        // I/O matrix: quick-dev work but no deferred / no open retro → neither work summary card renders.
        // The quick-dev card grid no longer renders at all; the "Direct changes" stat tile carries that count.
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Fix the footer", "quick/fix-footer.html", "done", "chore") },
            Deferred = null,
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkDashboard(work, openRetro: 0));

        Assert.DoesNotContain("work-summary-card", body);
        Assert.DoesNotContain("Deferred Work", body);
        Assert.DoesNotContain("Retro Action Items", body);
        Assert.DoesNotContain("quick-dev-card", body);
    }

    [Fact]
    public void RenderDashboardBody_DeferredOnly_RendersDeferredCardOnly()
    {
        // I/O matrix: deferred work, no quick-dev, no open retro → the Deferred summary card only.
        var work = new WorkInventory
        {
            QuickDev = Array.Empty<QuickDevEntry>(),
            Deferred = new DeferredWorkEntry("Deferred Work", "deferred-work.html", 3),
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkDashboard(work, openRetro: 0));

        Assert.Contains("summary-card work-summary-card deferred", body);
        Assert.Contains("Deferred Work", body);
        Assert.DoesNotContain("work-summary-card retro", body);
        Assert.DoesNotContain("Retro Action Items", body);
        Assert.DoesNotContain("quick-dev-card", body);
    }

    [Fact]
    public void RenderDashboardBody_RetroOnly_RendersRetroCardOnly()
    {
        // I/O matrix: open retro action items, empty work inventory → the Retro summary card only.
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkDashboard(WorkInventory.Empty, openRetro: 4));

        Assert.Contains("summary-card work-summary-card retro", body);
        Assert.Contains("4 open items", body);
        Assert.DoesNotContain("work-summary-card deferred", body);
        Assert.DoesNotContain("quick-dev-card", body);
    }

    [Fact]
    public void RenderDashboardBody_FullWork_RendersBothCardsInBandButNoIndexCardMarkup()
    {
        // I/O matrix (full project): both Deferred + Retro summary cards present inside the summary band, but
        // NO quick-dev card grid and NO index-card / index-grid / index-section-title-row markup.
        var work = new WorkInventory
        {
            QuickDev = new[] { new QuickDevEntry("Fix the footer", "quick/fix-footer.html", "done", "chore") },
            Deferred = new DeferredWorkEntry("Deferred Work", "deferred-work.html", 2),
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkDashboard(work, openRetro: 1));

        Assert.Contains("dashboard-tile-band", body);
        Assert.Contains("work-summary-card deferred", body);
        Assert.Contains("work-summary-card retro", body);
        Assert.DoesNotContain("quick-dev-card", body);
        Assert.DoesNotContain("class=\"index-card\"", body);
        Assert.DoesNotContain("index-grid", body);
        Assert.DoesNotContain("index-section-title-row", body);
    }

    // ----- Home welcome: Work label, Docs grouping, removed CTAs, journey segments --------------------------

    [Fact]
    public void RenderNav_ShowsWorkGroupInsteadOfDelivery()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var html = HtmlRenderAdapter.Shared.RenderNav(nav.ToNavigationView(SiteNav.HomeOutputPath));

        Assert.Contains("site-menu-trigger", html);
        Assert.Contains("Work<span", html);
        Assert.DoesNotContain("Delivery<span", html);
        Assert.Contains("href=\"epics.html\"", html);
        Assert.Contains("href=\"sprint.html\"", html);
        Assert.Contains("href=\"requirements.html\"", html);
    }

    [Fact]
    public void RenderNav_GroupsPlanningDocsUnderDocsOnKeyViewsBand()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/briefs/brief-x/brief.md",
            "planning-artifacts/ux-designs/ux-x/DESIGN.md",
            "planning-artifacts/ux-designs/ux-x/EXPERIENCE.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false, hasReadme: true);

        var html = HtmlRenderAdapter.Shared.RenderNav(nav.ToNavigationView(SiteNav.HomeOutputPath));
        var keyViews = html[html.IndexOf("site-nav-key-views", StringComparison.Ordinal)..];

        Assert.Contains("key-view-group", keyViews);
        Assert.Contains("key-view-trigger", keyViews);
        Assert.Contains("Docs<span", keyViews);
        // Related planning docs live under the Docs dropdown — not as five peer pills.
        Assert.DoesNotContain("class=\"quick-link-pill family-planning\" href=\"readme.html\"", keyViews);
        Assert.Contains("key-view-item", keyViews);
        Assert.Contains("href=\"readme.html\"", keyViews);
        Assert.Contains("PRD</a>", keyViews);
        Assert.Contains("Product Brief</a>", keyViews);
        Assert.Contains("href=\"epics.html\"", keyViews);
    }

    [Fact]
    public void RenderDashboardBody_OmitsRedundantViewEpicsAndSprintCtas()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-first-story: done
            """);
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 1, Title = "One", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = new[]
                    {
                        new StoryInfo
                        {
                            Id = "1.1", EpicNumber = 1, Title = "First",
                            UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(),
                            Status = "done",
                        },
                    },
                },
            },
        };
        var view = DashboardViewBuilder.Build(
            nav,
            ProgressModel.Empty,
            epics,
            requirements: null,
            CommandCatalog.Empty,
            WorkInventory.Empty,
            sprint,
            coverage: null);

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        Assert.DoesNotContain("View epics", body);
        Assert.DoesNotContain("View sprint", body);
        Assert.DoesNotContain("View sprint board", body);
        Assert.DoesNotContain("View Epics", body);
        Assert.Contains("tile-journey-epics", body);
        Assert.Contains("tile-journey-execution", body);
        Assert.Contains("href=\"epics.html\"", body); // Epics drafted tile / sunburst still navigate
        Assert.Contains("href=\"sprint.html\"", body); // board cards / moreHref still navigate
    }
}
