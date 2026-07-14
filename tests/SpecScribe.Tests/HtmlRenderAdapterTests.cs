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
}
