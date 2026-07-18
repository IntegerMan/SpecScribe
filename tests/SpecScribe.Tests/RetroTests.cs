using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the retrospective-notes artifact class (Story 2.3 retro pages): filename discovery,
/// meta extraction + action-items badging in <see cref="RetroParser"/>, and the dedicated
/// <see cref="RetroTemplater"/> page (styled header, epic link, participant pills, single main).</summary>
public class RetroTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("specscribe-retro-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private const string RetroMd = """
        # Epic 1 Retrospective: Foundation

        **Date:** 2026-07-07
        **Participants:** Matt (Lead), Amelia (Dev), Alice (PO)

        ## What Went Well

        - Seams held across the epic.

        ## Action Items

        | # | Action | Owner | Status |
        |---|--------|-------|--------|
        | 1 | Route deferred tech debt | Dana | open |
        | 2 | Schedule retros promptly | Amelia | done |
        """;

    private RetroModel Parse()
    {
        var path = Path.Combine(_dir, "epic-1-retro-2026-07-07.md");
        File.WriteAllText(path, RetroMd);
        return RetroParser.Parse(path,
            "implementation-artifacts/epic-1-retro-2026-07-07.md",
            "implementation-artifacts/epic-1-retro-2026-07-07.html");
    }

    [Fact]
    public void IsRetroFile_MatchesEpicRetroNamesOnly()
    {
        Assert.True(RetroParser.IsRetroFile("epic-1-retro-2026-07-07.md"));
        Assert.Equal(1, RetroParser.EpicNumberOf("epic-1-retro-2026-07-07.md"));
        Assert.False(RetroParser.IsRetroFile("1-1-some-story.md"));
        Assert.False(RetroParser.IsRetroFile("epics.md"));
    }

    [Fact]
    public void Parse_ExtractsMetaBadgesActionItemsAndStripsMetaLines()
    {
        var retro = Parse();

        Assert.Equal(1, retro.EpicNumber);
        Assert.Equal("Epic 1 Retrospective: Foundation", retro.Title);
        Assert.Equal("2026-07-07", retro.DateText);
        Assert.Equal(new[] { "Matt (Lead)", "Amelia (Dev)", "Alice (PO)" }, retro.Participants.ToArray());

        // The leading title h1 is stripped from the body (the styled header already carries the title).
        Assert.DoesNotContain("<h1", retro.BodyHtml);
        // The date/participants lines are lifted out of the narrative (they move to the styled header).
        Assert.DoesNotContain("<strong>Date:</strong>", retro.BodyHtml);
        Assert.DoesNotContain("<strong>Participants:</strong>", retro.BodyHtml);
        // The Action Items table's Status cells are badged (open → ready, done → done); no bare status cells.
        Assert.Contains("status-badge ready js-tip", retro.BodyHtml);
        Assert.Contains("status-badge done js-tip", retro.BodyHtml);
        Assert.DoesNotContain("<td>open</td>", retro.BodyHtml);
        // The Owner column is dropped entirely — header + every owner cell (LLM personas, not real assignees).
        Assert.DoesNotContain("Owner", retro.BodyHtml);
        Assert.DoesNotContain("Dana", retro.BodyHtml);
        Assert.DoesNotContain("Amelia", retro.BodyHtml);
        // The Action text and remaining columns survive.
        Assert.Contains("Route deferred tech debt", retro.BodyHtml);
    }

    [Fact]
    public void RenderPage_StyledHeaderEpicLinkNoPersonasAndSingleMain()
    {
        var retro = Parse();
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 1, Title = "Foundation", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        Assert.Contains("class=\"story-kicker\">Epic 1 Retrospective</div>", html);
        // The h1 drops the redundant "Epic 1 Retrospective:" prefix (the kicker above already carries it).
        Assert.Contains("<h1>Foundation</h1>", html);
        Assert.DoesNotContain("<h1>Epic 1 Retrospective", html);
        // The retro date now routes through the single PortalDates token (Story 10.4): bare ISO → "Jul 7, 2026".
        Assert.Contains("<span class=\"pill\">Jul 7, 2026</span>", html);
        // Personas (LLM-generated retro participants) are NOT rendered — noise once the doc exists. [polish #7]
        Assert.DoesNotContain("retro-personas", html);
        Assert.DoesNotContain("persona-pill", html);
        Assert.DoesNotContain(">Personas<", html);
        // Epic link resolves at the retro page's depth-1 prefix.
        Assert.Contains("href=\"../epics/epic-1.html\">Epic 1 &rarr;</a>", html);
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderPage_ListsEpicStoriesAsSprintCards()
    {
        var retro = Parse();
        var epics = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                new EpicInfo
                {
                    Number = 1, Title = "Foundation", GoalHtml = string.Empty,
                    Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
                    Stories = new[]
                    {
                        new StoryInfo { Id = "1.1", EpicNumber = 1, Title = "Nav Foundation", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-1-1.html", Status = "Done" },
                        new StoryInfo { Id = "1.2", EpicNumber = 1, Title = "Traceability", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = null }, // undrafted → placeholder
                    },
                },
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderPage(retro, epics, nav);

        Assert.Contains("<section class=\"retro-stories\" id=\"retro-stories\">", html);
        Assert.Contains("Stories in this Epic", html);
        Assert.Contains("class=\"retro-story-grid\">", html);
        // Stories use the shared sprint-card markup (same style as the sprint board), status color on the card.
        Assert.Contains("<a class=\"sprint-card done\" href=\"../epics/story-1-1.html\">", html);
        Assert.Contains("<span class=\"sprint-card-id\">Story 1.1</span>", html);
        Assert.Contains("<span class=\"sprint-card-title\">Nav Foundation</span>", html);
        // Undrafted story links to its placeholder path.
        Assert.Contains("href=\"../epics/story-1-2.html\"", html);
        // No longer a row layout.
        Assert.DoesNotContain("retro-story-row", html);
    }

    [Fact]
    public void RenderIndex_ListsRetrosLinkingToTheirPages()
    {
        var retros = new[]
        {
            new RetroModel
            {
                EpicNumber = 1, Title = "Epic 1 Retrospective", DateText = "2026-07-07",
                Participants = Array.Empty<string>(), BodyHtml = string.Empty,
                SourceRelativePath = "implementation-artifacts/epic-1-retro-2026-07-07.md",
                OutputRelativePath = "implementation-artifacts/epic-1-retro-2026-07-07.html",
            },
        };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = RetroTemplater.RenderIndex(retros, nav);

        Assert.Contains("<h1>Retrospectives</h1>", html);
        Assert.Contains("href=\"implementation-artifacts/epic-1-retro-2026-07-07.html\"", html);
        Assert.Contains("Epic 1", html);
        Assert.Contains("2026-07-07", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void ActionItems_RenderPage_ShowsItemsRetroLinkAndDetailHref()
    {
        var open = new[]
        {
            new SprintActionItem("Route deferred tech debt", "open", 1, "Dana"),
            new SprintActionItem("Schedule retros promptly", "in-progress", 1, "Amelia"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro-2026-07-07.html" };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, map, commands, nav);

        Assert.Contains("<h1>Open Action Items", html);
        Assert.Contains("class=\"status-legend\"", html);
        Assert.Contains("Route deferred tech debt", html);
        // Owners are NOT shown — they're LLM-generated retro personas, not real assignees. [polish #7]
        Assert.DoesNotContain(">Dana</span>", html);
        Assert.DoesNotContain(">Amelia</span>", html);
        // Provenance lives on the group heading (Story 9.6) — linked to the epic's retro page.
        Assert.Contains("class=\"action-items-group\"", html);
        Assert.Contains("href=\"implementation-artifacts/epic-1-retro-2026-07-07.html\">From the Epic 1 retrospective", html);
        // Story 9.11 + code review 9.10: Resolve-with-AI on detail page; list is scan + View detail only.
        Assert.Contains("href=\"follow-ups/action-", html);
        Assert.Contains("class=\"followup-row-primary\"", html);
        Assert.DoesNotContain("Resolve with AI on the detail page", html);
        Assert.DoesNotContain("followup-row-detail", html);
        Assert.DoesNotContain("<span class=\"cmd-text\">Resolve with AI</span>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));

        // No quick-dev command exposed → still no resolve chrome on the list.
        var noCmd = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav);
        Assert.DoesNotContain("Resolve with AI", noCmd);
    }

    [Fact]
    public void ActionItems_RenderPage_WideWrapperAndDeferredTeaserOnlyForDebtItems()
    {
        var open = new[]
        {
            new SprintActionItem("Route deferred tech debt into the backlog", "open", 1, "Dana"),
            new SprintActionItem("Schedule retros promptly", "open", 1, "Amelia"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro-2026-07-07.html" };
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

        var html = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav, deferredWorkHref: "deferred-work.html");

        // Wider layout wrapper (not the 860 doc column).
        Assert.Contains("class=\"action-items-wrap\"", html);
        // Code review 9.10: list omits deferred teaser when detail URL exists; link lives on detail page.
        Assert.DoesNotContain("action-item-deferred", html);
        Assert.Contains("href=\"follow-ups/action-", html);
        Assert.DoesNotContain("followup-row-detail", html);

        // No deferred href → still no deferred chrome on the list (detail path owns it).
        var noHref = ActionItemsTemplater.RenderPage(open, map, CommandCatalog.Empty, nav);
        Assert.DoesNotContain("action-item-deferred", noHref);
    }

    [Fact]
    public void ActionItems_RenderPage_SummaryLinkifies_ResolveLivesOnDetail()
    {
        var open = new[]
        {
            new SprintActionItem("Fix Story 1.1 heatmap debt before Epic 2", "open", 1, "Dana"),
        };
        var map = new Dictionary<int, string> { [1] = "implementation-artifacts/epic-1-retro.html" };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string> { ["quick-dev"] = "/bmad-quick-dev" });
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var epics = new EpicsModel
        {
            OverviewHtml = "",
            RequirementsInventoryHtml = "",
            Epics =
            [
                new EpicInfo
                {
                    Number = 1,
                    Title = "Foundation",
                    GoalHtml = "",
                    Status = EpicStatus.Drafted,
                    Section = EpicSection.VerticalSlice,
                    Stories =
                    [
                        new StoryInfo
                        {
                            Id = "1.1",
                            EpicNumber = 1,
                            Title = "Foundation",
                            UserStoryHtml = "",
                            AcBlocksHtml = Array.Empty<string>(),
                        },
                    ],
                },
            ],
        };

        var html = ActionItemsTemplater.RenderPage(open, map, commands, nav, epicsModel: epics);

        // Summary line still linkifies Story N.M mentions.
        Assert.Contains("class=\"story-ref\"", html);
        Assert.Contains(">Story 1.1</a>", html);
        Assert.Contains("href=\"follow-ups/action-", html);
        // Resolve payload is on the detail page, not the list (Story 9.11).
        Assert.DoesNotContain("data-copy=", html);

        var detail = FollowUpDetailTemplater.RenderActionPage(
            open[0], FollowUpSlug.AssignActionSlugs(open)[open[0]], nav, commands, map, epicsModel: epics);
        Assert.Contains("class=\"chart-panel next-steps\"", detail);
        Assert.Contains("data-copy=\"/bmad-quick-dev Resolve this retrospective action item (Epic 1): Fix Story 1.1 heatmap debt before Epic 2\"", detail);
        Assert.DoesNotContain("data-copy=\"/bmad-quick-dev Resolve this retrospective action item (Epic 1): Fix <a", detail);
        Assert.Contains("Copies a quick-dev prompt", detail);
        Assert.Contains("Close with AI", detail);
        Assert.Contains("data-copy=\"/bmad-quick-dev Close this retrospective action item (Epic 1) in sprint-status.yaml", detail);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
