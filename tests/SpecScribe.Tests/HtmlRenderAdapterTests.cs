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

    [Fact]
    public void StoryPlaceholder_RendersCreateStoryNoteAboveAcceptanceCriteria()
    {
        var view = new StoryPlaceholderView
        {
            Id = "9.7",
            TitleHtml = "Open Follow-Ups",
            StatusStage = "drafted",
            RetroLinkHtml = string.Empty,
            UserStoryHtml = "<p>As a user…</p>",
            AcBlocksHtml = new[] { "<div>AC 1</div>" },
            NoteHtml = "<span>create its plan with</span>",
            EpicNumber = 9,
            BackHref = "../epics/epic-9.html",
        };

        var html = HtmlRenderAdapter.Shared.RenderStoryPlaceholderBody(view);

        var noteIdx = html.IndexOf("pending-note", System.StringComparison.Ordinal);
        var acIdx = html.IndexOf("ac-panel", System.StringComparison.Ordinal);
        Assert.True(noteIdx >= 0 && acIdx > noteIdx, "Create-story note must render above the AC panel");
        // Note must be a sibling ahead of the AC section — not nested inside it.
        Assert.DoesNotContain("ac-list\">\n  <div class=\"epic-card\">", html);
        Assert.Contains("<div class=\"epic-card\">", html[..acIdx]);
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
    public void BuildEpic_SunburstStoryWedges_LinkToStoryPagesNotInPageAnchors()
    {
        // Epic sunburst used to fall back to #story-N-M (scroll to the card on this page) when
        // ArtifactOutputPath was null. Placeholder pages exist at StoryPagePath for every undrafted
        // story — clicks must leave the epic page for the story surface, matching story-card TitleHref.
        var epic = EpicWith(Drafted("1.1", "A"), Undrafted("1.2", "B"));
        var view = EpicsViewBuilder.BuildEpic(epic, ProgressFor(epic), CreateStoryCatalog(), epicRetroPath: null);
        var html = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        var sunburstStart = html.IndexOf("aria-label=\"Epic story breakdown\"", StringComparison.Ordinal);
        Assert.True(sunburstStart >= 0);
        var sunburstEnd = html.IndexOf("</svg>", sunburstStart, StringComparison.Ordinal);
        Assert.True(sunburstEnd > sunburstStart);
        var sunburst = html[sunburstStart..sunburstEnd];

        Assert.Contains("href=\"../epics/story-1-1.html\"", sunburst);
        Assert.Contains("href=\"../epics/story-1-2.html\"", sunburst);
        Assert.DoesNotContain("href=\"#story-", sunburst);
    }

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

    private static RequirementInfo Req(RequirementKind kind, int number, RequirementStatus status) => new()
    {
        Kind = kind,
        Number = number,
        TextHtml = kind + " " + number,
        CoverageEpicNumbers = status is RequirementStatus.Unmapped or RequirementStatus.Deferred
            ? Array.Empty<int>()
            : new[] { 1 },
        CoverageEpicNumber = status is RequirementStatus.Unmapped or RequirementStatus.Deferred ? null : 1,
        Deferred = status == RequirementStatus.Deferred,
        Status = status,
    };

    [Fact]
    public void Build_RequirementStatTiles_SubLineDistinguishesUnmappedAndDeferred()
    {
        var requirements = new RequirementsModel
        {
            Functional = new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Done) },
            NonFunctional = new[]
            {
                Req(RequirementKind.NonFunctional, 1, RequirementStatus.Unmapped),
                Req(RequirementKind.NonFunctional, 2, RequirementStatus.Deferred),
            },
            Design = new[] { Req(RequirementKind.Design, 1, RequirementStatus.Unmapped) },
        };
        var view = DashboardViewBuilder.Build(
            Nav(),
            ProgressModel.Empty,
            epicsModel: null,
            requirements,
            CommandCatalog.Empty,
            WorkInventory.Empty,
            sprint: null,
            coverage: null);

        var nfr = Assert.Single(view.StatTiles, t => t.Label == "Non-functional");
        Assert.Equal("1 not yet mapped · 1 deferred", nfr.Sub);
        Assert.DoesNotContain("planned", nfr.Sub, StringComparison.Ordinal);

        var design = Assert.Single(view.StatTiles, t => t.Label == "Design reqs");
        Assert.Equal("1 not yet mapped", design.Sub);
    }

    [Fact]
    public void RenderDashboardBody_SatisfactionRollup_LinksToHubAnchor()
    {
        var requirements = new RequirementsModel
        {
            Functional = new[] { Req(RequirementKind.Functional, 1, RequirementStatus.Done) },
            NonFunctional = new[] { Req(RequirementKind.NonFunctional, 1, RequirementStatus.Unmapped) },
            Design = new[] { Req(RequirementKind.Design, 1, RequirementStatus.Planned) },
        };
        var view = DashboardViewBuilder.Build(
            Nav(),
            ProgressModel.Empty,
            RequirementsEpics(),
            requirements,
            CommandCatalog.Empty,
            WorkInventory.Empty,
            sprint: null,
            coverage: null);

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        Assert.Contains("satisfaction-rollup", body);
        Assert.Contains("requirements.html#satisfaction", body);
        Assert.Contains("Satisfied", body);
        Assert.Contains("In flight", body);
        Assert.Contains("Not yet mapped", body);
        Assert.Contains(Icons.ForStatus("unmapped"), body);
    }

    [Fact]
    public void RenderDashboardBody_DesignOnly_ShowsSatisfactionRollup()
    {
        var requirements = new RequirementsModel
        {
            Functional = Array.Empty<RequirementInfo>(),
            NonFunctional = Array.Empty<RequirementInfo>(),
            Design = new[] { Req(RequirementKind.Design, 1, RequirementStatus.Planned) },
        };
        var view = DashboardViewBuilder.Build(
            Nav(),
            ProgressModel.Empty,
            RequirementsEpics(),
            requirements,
            CommandCatalog.Empty,
            WorkInventory.Empty,
            sprint: null,
            coverage: null);

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        Assert.Contains("satisfaction-rollup", body);
        Assert.Contains("requirements.html#satisfaction", body);
        Assert.DoesNotContain("req-status-grid", body);
        Assert.DoesNotContain("req-flow-svg", body);
    }

    [Fact]
    public void RenderDashboardBody_NoRequirements_OmitsSatisfactionRollup()
    {
        var view = DashboardViewBuilder.Build(
            Nav(),
            ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            CommandCatalog.Empty,
            WorkInventory.Empty,
            sprint: null,
            coverage: null);

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);

        Assert.DoesNotContain("satisfaction-rollup", body);
        Assert.DoesNotContain("requirements.html#satisfaction", body);
    }

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

    // ---- Story 9.8: Home Next Steps + work-stage focus strip ----------------------------------------------

    private static CommandCatalog ProjectWorkflowCatalog() => new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["sprint-status"] = "/bmad-sprint-status",
        ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
    });

    private static DashboardView WorkflowDashboard(EpicsModel? epics, CommandCatalog? commands = null)
    {
        var catalog = commands ?? ProjectWorkflowCatalog();
        return DashboardViewBuilder.Build(
            Nav(),
            ProgressModel.Empty,
            epics,
            requirements: null,
            catalog,
            WorkInventory.Empty,
            sprint: null,
            coverage: null);
    }

    [Fact]
    public void RenderDashboardBody_WiresProjectNextSteps_BeforeNowAndNext()
    {
        var ready = Drafted("1.1", "A");
        ready.Status = "ready-for-dev";
        var undrafted = Undrafted("1.2", "B");
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { EpicWith(ready, undrafted) },
        };

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkflowDashboard(epics));

        Assert.Contains("class=\"chart-panel next-steps", body);
        Assert.Contains("/bmad-dev-story 1.1", body);
        Assert.Contains("/bmad-create-story 1.2", body);
        var nextSteps = body.IndexOf("class=\"chart-panel next-steps", StringComparison.Ordinal);
        var nowNext = body.IndexOf("Now &amp; Next", StringComparison.Ordinal);
        Assert.True(nextSteps >= 0 && nowNext >= 0 && nextSteps < nowNext,
            "Project Next Steps must render before Now & Next");
    }

    [Fact]
    public void RenderDashboardBody_OmitsProjectNextSteps_WhenCatalogEmpty()
    {
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { EpicWith(Drafted("1.1", "A")) },
        };

        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkflowDashboard(epics, CommandCatalog.Empty));

        Assert.DoesNotContain("class=\"chart-panel next-steps", body);
    }

    [Fact]
    public void RenderDashboardBody_OmitsInlineWorkModeStrip_PanelsTaggedForVisibility()
    {
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkflowDashboard(epics: null));
        Assert.DoesNotContain("work-mode-strip", body);
        Assert.DoesNotContain("name=\"work-mode\"", body);
        Assert.Contains("wm-panel wm-show-overview", body);
        Assert.Contains("wm-show-track", body);
        // Tiles carry visibility classes themselves — no journey wrappers that break flex-wrap.
        Assert.DoesNotContain("<div class=\"wm-panel wm-show-overview wm-show-requirements", body);
    }

    [Fact]
    public void RenderDashboardBody_WorkModePanels_TaggedWithoutInlineRadios()
    {
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[] { EpicWith(Drafted("1.1", "A"), Undrafted("1.2", "B")) },
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkflowDashboard(epics));

        Assert.Contains("wm-show-develop", body);
        Assert.Contains("wm-show-requirements", body);
        Assert.Contains("wm-show-plan", body);
        Assert.Contains("wm-show-review", body);
        Assert.Contains("wm-show-track", body);
        Assert.DoesNotContain("work-mode-strip", body);
        Assert.DoesNotContain("name=\"work-mode\"", body);
        Assert.Contains("Now &amp; Next", body);
        Assert.Contains("Story Pipeline", body);
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

        Assert.Contains("work-summary-card deferred", body);
        Assert.Contains("stat-label\">Deferred work</div>", body);
        Assert.DoesNotContain("work-summary-card retro", body);
        Assert.DoesNotContain("Retro Action Items", body);
        Assert.DoesNotContain("quick-dev-card", body);
    }

    [Fact]
    public void RenderDashboardBody_RetroOnly_RendersRetroCardOnly()
    {
        // I/O matrix: open retro action items, empty work inventory → the Retro summary card only.
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(WorkDashboard(WorkInventory.Empty, openRetro: 4));

        Assert.Contains("work-summary-card retro", body);
        Assert.Contains("stat-number\">4</div>", body);
        Assert.Contains("stat-label\">Action items</div>", body);
        Assert.Contains("open items", body);
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
    public void RenderNav_OnHome_ShowsWorkModeToggleStrip_InsteadOfKeyViewPills()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false, hasReadme: true);

        var html = HtmlRenderAdapter.Shared.RenderNav(nav.ToNavigationView(SiteNav.HomeOutputPath));
        var keyViews = html[html.IndexOf("site-nav-key-views", StringComparison.Ordinal)..];

        Assert.Contains("work-mode-jumps", keyViews);
        Assert.Contains("id=\"wm-overview\"", keyViews);
        Assert.Contains("id=\"wm-requirements\"", keyViews);
        Assert.Contains("id=\"wm-plan\"", keyViews);
        Assert.Contains("id=\"wm-develop\"", keyViews);
        Assert.Contains("id=\"wm-review\"", keyViews);
        Assert.Contains("id=\"wm-track\"", keyViews);
        Assert.Contains("work-mode-pill", keyViews);
        Assert.DoesNotContain("key-view-group", keyViews);
        Assert.DoesNotContain("Docs<span", keyViews);
        // Dark bar still carries the journey menus (with family color classes).
        Assert.Contains("site-menu-group family-planning", html);
        Assert.Contains("site-menu-group family-epics", html);
    }

    [Fact]
    public void RenderNav_OffHome_GroupsPlanningDocsUnderDocsOnKeyViewsBand()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/prds/prd-x/prd.md",
            "planning-artifacts/briefs/brief-x/brief.md",
            "planning-artifacts/ux-designs/ux-x/DESIGN.md",
            "planning-artifacts/ux-designs/ux-x/EXPERIENCE.md",
            "planning-artifacts/epics.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.BmadMethod), hasAdrs: false, hasReadme: true);

        var html = HtmlRenderAdapter.Shared.RenderNav(nav.ToNavigationView(SiteNav.EpicsOutputPath));
        var keyViews = html[html.IndexOf("site-nav-key-views", StringComparison.Ordinal)..];

        Assert.Contains("key-view-group", keyViews);
        Assert.Contains("key-view-trigger", keyViews);
        Assert.Contains("Docs<span", keyViews);
        Assert.Contains("aria-expanded=\"false\"", keyViews);
        Assert.Contains("aria-controls=\"key-view-panel-docs\"", keyViews);
        Assert.Contains("id=\"key-view-panel-docs\"", keyViews);
        // Related planning docs live under the Docs dropdown — not as five peer pills.
        Assert.DoesNotContain("class=\"quick-link-pill family-planning\" href=\"readme.html\"", keyViews);
        Assert.Contains("key-view-item", keyViews);
        Assert.Contains("href=\"readme.html\"", keyViews);
        Assert.Contains("PRD</a>", keyViews);
        Assert.Contains("Product Brief</a>", keyViews);
        Assert.Contains("href=\"epics.html\"", keyViews);
        Assert.DoesNotContain("work-mode-jumps", keyViews);
    }

    [Fact]
    public void RenderDashboardBody_JourneySegments_DoNotDuplicateStatTiles()
    {
        var view = DashboardWithRequirements(withEpics: true) with
        {
            StatTiles = new[]
            {
                new StatTile("1/2", "Functional reqs", null, "tip", "requirements.html"),
                new StatTile("1/1", "Epics drafted", null, "tip", "epics.html"),
                new StatTile("3", "Stories defined", null, "tip", "requirements.html"),
                new StatTile("1/1", "Planned tasks done", null, "tip", "sprint.html"),
                new StatTile("2", "Direct changes", null, "tip", "deferred.html"),
                new StatTile("9", "Commits", null, "tip", "timeline.html"),
            },
            ProgressBars = new[] { new ProgressBarView("Implementation", 1, 2) },
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);
        foreach (var label in view.StatTiles.Select(t => t.Label))
        {
            var needle = $"stat-label\">{label}</div>";
            var first = body.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(first >= 0, $"missing tile {label}");
            Assert.Equal(-1, body.IndexOf(needle, first + needle.Length, StringComparison.Ordinal));
        }
        Assert.Contains("tile-journey-label\">Requirements</span>", body);
        Assert.Contains("tile-journey-label\">Execution</span>", body);
        Assert.Contains("journey-execution", body);
        Assert.Contains("journey-lead", body);
        Assert.Contains("overall-progress-tile", body);
        Assert.Contains("stat-label\">Overall progress</div>", body);
        Assert.Contains("stat-label\">Epic status</div>", body);
        // Direct changes rides with Execution in DOM order (after Planned, before Overall) but is
        // Review-only — Overview/Track omit it so those stages stay a clean 2×5.
        var plannedAt = body.IndexOf("stat-label\">Planned tasks done</div>", StringComparison.Ordinal);
        var directAt = body.IndexOf("stat-label\">Direct changes</div>", StringComparison.Ordinal);
        var progressAt = body.IndexOf("overall-progress-tile", StringComparison.Ordinal);
        Assert.True(plannedAt >= 0 && directAt > plannedAt && progressAt > directAt);
        var directCls = CardClassBefore(body, directAt);
        Assert.Contains("wm-show-review", directCls);
        Assert.DoesNotContain("wm-show-overview", directCls);
        // Commits are Develop-only (not Overview) so Overview stays at 10 tiles.
        var commitsAt = body.IndexOf("stat-label\">Commits</div>", StringComparison.Ordinal);
        Assert.True(commitsAt >= 0);
        var commitsCls = CardClassBefore(body, commitsAt);
        Assert.Contains("wm-show-develop", commitsCls);
        Assert.DoesNotContain("wm-show-overview", commitsCls);
    }

    private static string CardClassBefore(string body, int labelAt)
    {
        var classAt = body.LastIndexOf("class=\"stat-card", labelAt, StringComparison.Ordinal);
        Assert.True(classAt >= 0 && classAt < labelAt);
        var end = body.IndexOf('"', classAt + 7);
        Assert.True(end > classAt);
        return body[(classAt + 7)..end];
    }

    [Fact]
    public void RenderDashboardBody_OmitsOverallProgressWhenNoBars()
    {
        var view = DashboardWithRequirements(withEpics: true) with
        {
            ProgressBars = Array.Empty<ProgressBarView>(),
            StatTiles = Array.Empty<StatTile>(),
        };
        var body = HtmlRenderAdapter.Shared.RenderDashboardBody(view);
        Assert.DoesNotContain("overall-progress-tile", body);
        Assert.DoesNotContain("journey-execution", body);
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
        Assert.Contains("journey-epics", body);
        Assert.Contains("journey-execution", body);
        Assert.Contains("href=\"epics.html\"", body); // Epics drafted tile / sunburst still navigate
        Assert.Contains("href=\"sprint.html\"", body); // board cards / moreHref still navigate
    }

    // ---- Story 9.4 verification evidence strip + ADR 0007 change surface ------------------------------------

    private static StoryChangeSurface SampleSurface(
        IReadOnlyList<string>? classifications = null,
        IReadOnlyList<(int Number, string PlainText)>? checklist = null,
        IReadOnlyList<ChangeSurfaceFile>? files = null,
        string? verifyBeforeReviewHtml = null) =>
        new(
            classifications ?? new[] { "visual", "rendered UI" },
            checklist ?? new[] { (1, "a compact evidence strip appears near the status badge") },
            files ?? new[]
            {
                new ChangeSurfaceFile("src/SpecScribe/EpicsParser.cs", "EpicsParser.cs", "code/src/SpecScribe/EpicsParser.cs.html", ChangeSurfaceFileKind.Code),
                new ChangeSurfaceFile("src/SpecScribe/assets/specscribe.css", "specscribe.css", "code/src/SpecScribe/assets/specscribe.css.html", ChangeSurfaceFileKind.Code),
            },
            verifyBeforeReviewHtml);

    private static StoryPageView StoryBodyView(
        StoryEvidence evidence,
        StoryChangeSurface? changeSurface = null,
        string? status = "done",
        IReadOnlyList<DevAgentEntry>? devRecord = null,
        string changeLogHtml = "") => new()
    {
        Id = "1.1",
        TitleHtml = "Evidence Strip",
        StatusStage = StatusStyles.ForStory(new StoryInfo
        {
            Id = "1.1", EpicNumber = 1, Title = "Evidence", UserStoryHtml = string.Empty,
            AcBlocksHtml = Array.Empty<string>(), Status = status,
        }),
        Status = status,
        Evidence = evidence,
        ChangeSurface = changeSurface ?? SampleSurface(),
        RetroLinkHtml = string.Empty,
        BlurbHtml = string.Empty,
        Tasks = Array.Empty<TaskItem>(),
        NextStepsHtml = string.Empty,
        AcceptanceCriteria = new[]
        {
            new AcceptanceCriterion(1, "<p>criterion</p>", "a compact evidence strip appears near the status badge"),
        },
        DevAgentRecord = devRecord ?? new[] { new DevAgentEntry("Completion Notes", "<p>notes</p>") },
        ReviewFindingsHtml = string.Empty,
        RemainderHtml = string.Empty,
        ChangeLogHtml = changeLogHtml,
    };

    [Fact]
    public void RenderStoryBody_DeferredFromThis_RendersPanelWithLinks_OmitsWhenEmpty()
    {
        var evidence = new StoryEvidence(1, 1, null, null, false);
        var empty = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence));
        Assert.DoesNotContain("deferred-from-artifact", empty);
        Assert.DoesNotContain("sec-deferred-from-artifact", empty);

        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Park the exposure.</p>", false, null, null),
            "code review of 1-1-evidence.md (2026-07-09)",
            EpicNumber: 1,
            DetailHref: "../follow-ups/deferred-abc.html",
            SourceKey: "1-1-evidence");
        var with = StoryBodyView(evidence) with { DeferredFromThis = new[] { slot } };
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(with);
        Assert.Contains("id=\"sec-deferred-from-artifact\"", html);
        Assert.Contains("Deferred from this artifact", html);
        Assert.Contains("Park the exposure", html);
        Assert.Contains("href=\"../follow-ups/deferred-abc.html\"", html);
        Assert.Contains("followup-row", html);
        // Story-level task sunburst nests deferred under an inner Deferred parent (no full-circle fringe).
        Assert.Contains("id=\"sec-task-breakdown\"", html);
        Assert.Contains("Deferred item: Park the exposure.", html);
        Assert.Contains("href=\"#sec-deferred-from-artifact\"", html);
    }

    [Fact]
    public void RenderStoryBody_EvidenceStrip_PopulatedPillsAndChangeSurfacePanel()
    {
        var evidence = new StoryEvidence(5, 5, "586 passing tests", new DateOnly(2026, 7, 9), VerifiedIsReview: true);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence));

        Assert.Contains("class=\"evidence-strip\"", html);
        Assert.Contains("&#10003; 5 tasks", html);
        Assert.Contains("586 passing tests", html);
        Assert.Contains("evidence-pill tests-pass", html);
        Assert.Contains("verified 2026-07-09", html);
        Assert.Contains("class=\"evidence-dev-record-link\"", html);
        Assert.Contains("href=\"#sec-dev-agent-record\"", html);
        Assert.Contains("aria-label=\"Jump to Dev Agent Record for full verification evidence\"", html);
        Assert.Contains("class=\"change-surface\"", html);
        Assert.Contains("change-surface-panel", html);
        Assert.Contains("change-surface-title", html);
        Assert.Contains("open>", html);
        Assert.Contains("id=\"sec-change-surface\"", html);
        Assert.Contains("visual + rendered UI", html);
        Assert.Contains("href=\"#ac-1\"", html);
        Assert.Contains("AC #1", html);
        Assert.Contains("href=\"code/src/SpecScribe/EpicsParser.cs.html\"", html);
        Assert.Contains("EpicsParser.cs", html);
        Assert.Contains("specscribe.css", html);
        Assert.Contains("class=\"change-surface-files\"", html);
        Assert.Contains("touch-file-code", html);
        Assert.DoesNotContain("change-surface-ship", html);
        Assert.DoesNotContain("Latest:", html);
        Assert.DoesNotContain("class=\"evidence-link\"", html);
        // Panel sits below charts, not in the header evidence block.
        var headerEnd = html.IndexOf("</header>", StringComparison.Ordinal);
        var panelAt = html.IndexOf("id=\"sec-change-surface\"", StringComparison.Ordinal);
        Assert.True(panelAt > headerEnd);
    }

    [Fact]
    public void RenderStoryBody_EvidenceStrip_MissingTestsShowsEmptyStatePill()
    {
        var evidence = new StoryEvidence(5, 5, null, new DateOnly(2026, 7, 9), VerifiedIsReview: true);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence));

        Assert.Contains("evidence-pill empty", html);
        Assert.Contains("no test evidence recorded", html);
        Assert.Contains("class=\"evidence-strip\"", html);
        Assert.DoesNotContain("tests-pass", html);
    }

    [Fact]
    public void RenderStoryBody_EvidenceStrip_NonVerificationDateReadsUpdated()
    {
        var evidence = new StoryEvidence(2, 4, "12 passing tests", new DateOnly(2026, 7, 8), VerifiedIsReview: false);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence));

        Assert.Contains("updated 2026-07-08", html);
        Assert.DoesNotContain("verified 2026-07-08", html);
    }

    [Fact]
    public void RenderStoryBody_ChangeSurface_HonestAbsenceWhenThin()
    {
        var evidence = new StoryEvidence(0, 0, null, null, false);
        var surface = new StoryChangeSurface(
            Array.Empty<string>(), Array.Empty<(int, string)>(), Array.Empty<ChangeSurfaceFile>(), null);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(
            StoryBodyView(evidence, surface, status: "ready-for-dev", devRecord: Array.Empty<DevAgentEntry>()));

        Assert.Contains("class=\"evidence-strip\"", html);
        Assert.Contains("no tasks recorded", html);
        Assert.Contains(Icons.ForConcept("Tasks"), html);
        Assert.Contains("no test evidence recorded", html);
        Assert.Contains("no verification recorded", html);
        Assert.Contains("class=\"change-surface\"", html);
        Assert.Contains("no file list recorded", html);
        Assert.Contains("no verification guidance recorded", html);
        Assert.DoesNotContain("href=\"#sec-dev-agent-record\"", html);
        Assert.DoesNotContain("evidence-dev-record-link", html);
    }

    [Fact]
    public void RenderStoryBody_ChangeSurface_ShowsVerifyBeforeReviewAndLinkedFiles()
    {
        var verifyHtml = "<p>Open <code>action-items.html</code> and confirm grouping.</p>";
        var files = new[]
        {
            new ChangeSurfaceFile("src/SpecScribe/Foo.cs", "Foo.cs (new)", "code/src/SpecScribe/Foo.cs.html", ChangeSurfaceFileKind.CodeNew),
        };
        var surface = SampleSurface(files: files, verifyBeforeReviewHtml: verifyHtml);
        var evidence = new StoryEvidence(1, 1, "10 passing tests", null, false);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence, surface));

        Assert.Contains("change-surface-verify-manual", html);
        Assert.Contains("action-items.html", html);
        Assert.Contains("Foo.cs (new)", html);
        Assert.Contains("touch-file-new", html);
        Assert.Contains("href=\"code/src/SpecScribe/Foo.cs.html\"", html);
    }

    [Fact]
    public void RenderStoryBody_ChangeSurface_UpdatedChipsForSprintAndStory()
    {
        var files = new[]
        {
            new ChangeSurfaceFile("src/SpecScribe/Foo.cs", "Foo.cs", "code/src/SpecScribe/Foo.cs.html", ChangeSurfaceFileKind.Code),
            new ChangeSurfaceFile(
                "_bmad-output/implementation-artifacts/sprint-status.yaml",
                "Sprint Status",
                "../sprint.html",
                ChangeSurfaceFileKind.Sprint),
            new ChangeSurfaceFile(
                "9-2-nfr-and-ux-dr-coverage-maps.md",
                "NFR and UX DR Coverage Maps",
                "../epics/story-9-2.html",
                ChangeSurfaceFileKind.StoryArtifact),
        };
        var surface = SampleSurface(files: files);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(
            StoryBodyView(new StoryEvidence(1, 1, null, null, false), surface));

        Assert.Contains("change-surface-section-label\">Updated</div>", html);
        Assert.Contains("change-surface-chip-sprint", html);
        Assert.Contains("change-surface-chip-story", html);
        Assert.Contains(">Sprint Status</span>", html);
        Assert.Contains(">NFR and UX DR Coverage Maps</span>", html);
        Assert.Contains("href=\"../sprint.html\"", html);
        Assert.Contains("href=\"../epics/story-9-2.html\"", html);
        // Sprint/story must not remain in the Touched path grid.
        Assert.DoesNotContain("touch-file-sprint", html);
        Assert.DoesNotContain("touch-file-story", html);
        Assert.Contains("Foo.cs", html);
        Assert.Contains(Icons.ForConcept("Sprint Status"), html);
        Assert.Contains(Icons.ForConcept("Story"), html);
    }

    [Fact]
    public void RenderStoryBody_EvidenceStrip_OmittedWhenNoStatusBadge()
    {
        var evidence = new StoryEvidence(5, 5, "10 passing tests", new DateOnly(2026, 7, 1), true);
        var html = HtmlRenderAdapter.Shared.RenderStoryBody(StoryBodyView(evidence, status: null));

        Assert.DoesNotContain("evidence-strip", html);
        Assert.DoesNotContain("change-surface", html);
    }
}
