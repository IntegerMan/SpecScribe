using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Render-level coverage for the Story 2.3 Jira/Kanban redesign: the sprint page renders a five-column
/// status board (mapped lanes + linked cards), a pure-CSS status↔epic toggle, standard sprint command buttons,
/// links every model story (drafted or not) to its generated page, and shows open action items only when
/// present. RenderBoard caps columns with a "+N more" link. Holds the Story 1.4 a11y floor.</summary>
public class SprintTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

    private static CommandCatalog SprintCommands() => new("BMad", new Dictionary<string, string>
    {
        ["sprint-planning"] = "/bmad-sprint-planning",
        ["sprint-status"] = "/bmad-sprint-status",
        ["correct-course"] = "/bmad-correct-course",
        ["retrospective"] = "/bmad-retrospective",
    });

    private static StoryInfo Story(string id, int epic, string title, string? artifactOutputPath, int tasksDone = 0, int tasksTotal = 0) => new()
    {
        Id = id,
        EpicNumber = epic,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        ArtifactOutputPath = artifactOutputPath,
        TasksDone = tasksDone,
        TasksTotal = tasksTotal,
    };

    private static EpicsModel EpicsWith(params EpicInfo[] epics) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = epics,
    };

    private static EpicInfo Epic(int number, string title, params StoryInfo[] stories) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
    };

    private static SprintStatus Sample() => SprintStatusParser.Parse("""
        development_status:
          epic-1: in-progress
          1-1-foundation: done
          1-2-traceability: in-progress
          1-3-backlogged: backlog
          epic-1-retrospective: optional
        """)!;

    private static EpicsModel SampleEpics() => EpicsWith(Epic(1, "Foundation",
        Story("1.1", 1, "Foundation Story", "epics/story-1-1.html"),
        Story("1.2", 1, "Traceability Story", artifactOutputPath: null),   // undrafted, but in the model
        Story("1.3", 1, "Backlogged Story", "epics/story-1-3.html")));

    [Fact]
    public void RenderIndex_RendersSixColumnBoardWithMappedLanesLinkedCardsToggleAndCommands()
    {
        var html = SprintTemplater.RenderIndex(Sample(), SampleEpics(), Nav(), SprintCommands());

        // Five core lifecycle lanes always; retired/unrecognized only when the sample has such stories (it doesn't).
        foreach (var cls in new[] { "pending", "ready", "active", "review", "done" })
            Assert.Contains($"<section class=\"sprint-lane {cls}\"", html);
        Assert.DoesNotContain("sprint-lane retired", html);
        Assert.DoesNotContain("sprint-lane unrecognized", html);
        Assert.Contains("style=\"--lane-count: 5\"", html);

        // Cards carry the story's stage color + the js-tip hook; done story 1.1 and in-progress 1.2 link out.
        // Sample stories have no task plan → Story 8.4 also stamps .no-plan on the card class list.
        Assert.Contains("sprint-card js-tip done no-plan", html);
        Assert.Contains("sprint-card js-tip active no-plan", html);
        Assert.Contains("href=\"epics/story-1-1.html\"", html);

        // Pure-CSS toggle scaffolding (no JS) and both views present.
        Assert.Contains("id=\"sv-status\"", html);
        Assert.Contains("id=\"sv-epic\"", html);
        Assert.Contains("class=\"board-view board-view-status\"", html);
        Assert.Contains("class=\"board-view board-view-epic\"", html);
        Assert.Contains("class=\"sprint-epic-lane\"", html);

        // Sprint commands live in a header popout (<details>) with a description per command.
        Assert.Contains("class=\"cmd-menu\"", html);
        Assert.Contains("data-copy=\"/bmad-sprint-planning\"", html);
        Assert.Contains("data-copy=\"/bmad-retrospective\"", html);
        Assert.Contains("Plan the sprint from epics", html);
        Assert.Contains("class=\"cmd-menu-desc\"", html);
    }

    [Fact]
    public void RenderIndex_CardShowsStoryIdRichTooltipAndGatedTaskProgressBar()
    {
        var sprint = SprintStatusParser.Parse("development_status:\n  epic-2: in-progress\n  2-3-widget: in-progress\n  2-6-later: backlog\n")!;
        var epics = EpicsWith(Epic(2, "Rendering",
            Story("2.3", 2, "Sprint Widget", "epics/story-2-3.html", tasksDone: 3, tasksTotal: 8),   // has a plan
            Story("2.6", 2, "Later Story", artifactOutputPath: null)));                              // no plan

        var html = SprintTemplater.RenderIndex(sprint, epics, Nav(), CommandCatalog.Empty);

        // Id is "Story N.M" (no separate epic badge); the rich tooltip lives on the card's data-tip.
        Assert.Contains("<span class=\"sprint-card-id\">Story 2.3</span>", html);
        Assert.DoesNotContain("sprint-card-epic", html);
        // data-tip carries epic name + story name + task info (\n-separated); each card is a js-tip.
        Assert.Contains("sprint-card js-tip active\"", html); // planned — no no-plan modifier
        Assert.Contains("data-tip=\"Epic 2: Rendering\nStory 2.3: Sprint Widget\n3 of 8 tasks done\"", html);
        Assert.Contains("data-tip=\"Epic 2: Rendering\nStory 2.6: Later Story\nNo task plan yet\"", html);
        Assert.Contains("sprint-card js-tip pending no-plan", html); // 2.6 has no plan
        // Progress bar only for the story WITH a task plan: 3/8 = 38%, partial fill; no per-bar tooltip now.
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=\"38\"", html);
        Assert.Contains("class=\"sprint-card-progress-fill partial\" style=\"width:38%\"", html);
        // No per-bar tooltip on the sprint board body (the global nav's attribution badge carries a data-tooltip,
        // so scope this to the page body after the chrome).
        var body = html[(html.IndexOf("</nav>", StringComparison.Ordinal) + "</nav>".Length)..];
        Assert.DoesNotContain("data-tooltip=", body);
    }

    [Fact]
    public void RenderIndex_LinksEveryModelStoryIncludingUndrafted()
    {
        // Story 1.2 has no ArtifactOutputPath but IS in the model → it must link to its placeholder page
        // (StoryPagePath), never dead-end as plain text. [Story 2.3 redesign]
        var html = SprintTemplater.RenderIndex(Sample(), SampleEpics(), Nav(), CommandCatalog.Empty);

        Assert.Contains("href=\"epics/story-1-2.html\"", html);
        Assert.Contains("href=\"epics/story-1-3.html\"", html);
    }

    [Fact]
    public void RenderIndex_PlainTextForYamlStoryWithNoModelMatch()
    {
        // A tracked story absent from the epics model has no page to point at → plain text, no broken link.
        var sprint = SprintStatusParser.Parse("development_status:\n  epic-9: backlog\n  9-9-orphan: backlog\n")!;
        var html = SprintTemplater.RenderIndex(sprint, SampleEpics(), Nav(), CommandCatalog.Empty);

        Assert.DoesNotContain("href=\"epics/story-9-9.html\"", html);
        Assert.Contains("Orphan", html); // prettified slug shown as text
    }

    [Fact]
    public void RenderBoard_CapsEachColumnAndLinksToMore()
    {
        // Six done stories, capped at 2 → two visible cards + four hidden overflow + a "+4 more" link.
        // Cap applies after the default epic filter (here: epic 1 is active via done stories + open retro).
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              epic-1-retrospective: optional
              1-1-a: done
              1-2-b: done
              1-3-c: done
              1-4-d: done
              1-5-e: done
              1-6-f: done
            """)!;
        var epics = EpicsWith(Epic(1, "E",
            Story("1.1", 1, "A", "epics/story-1-1.html"), Story("1.2", 1, "B", "epics/story-1-2.html"),
            Story("1.3", 1, "C", "epics/story-1-3.html"), Story("1.4", 1, "D", "epics/story-1-4.html"),
            Story("1.5", 1, "E", "epics/story-1-5.html"), Story("1.6", 1, "F", "epics/story-1-6.html")));

        var board = SprintTemplater.RenderBoard(sprint, epics, capPerColumn: 2, moreHref: "sprint.html");

        Assert.Contains("class=\"sprint-lane-more\" href=\"sprint.html\">+4 more", board);
        Assert.Equal(6, CountOccurrences(board, "sprint-card js-tip done"));
        Assert.Equal(2, CountVisibleSprintCards(board));
        Assert.Contains("data-cap-overflow=\"1\"", board);

        // Uncapped render shows all six visible.
        var full = SprintTemplater.RenderBoard(sprint, epics);
        Assert.Equal(6, CountVisibleSprintCards(full));
        Assert.DoesNotContain("sprint-lane-more", full);
    }

    [Fact]
    public void RenderBoard_EmptyColumnShowsDashedPlaceholderWithColumnCopy()
    {
        // Active lane empty (AC #2 example); ready + backlog populated; review/done empty too. [Story 8.6]
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              1-1-a: ready-for-dev
              1-2-b: backlog
            """)!;
        var epics = EpicsWith(Epic(1, "Foundation",
            Story("1.1", 1, "A", "epics/story-1-1.html"),
            Story("1.2", 1, "B", "epics/story-1-2.html")));
        var board = SprintTemplater.RenderBoard(sprint, epics);

        Assert.Contains("class=\"sprint-lane-empty\">Nothing in progress — pick from Ready.", board);
        Assert.Contains("class=\"sprint-lane-empty\">Nothing awaiting review.", board);
        Assert.Contains("class=\"sprint-lane-empty\">Nothing finished yet.", board);
        Assert.DoesNotContain("sprint-lane retired", board);
        Assert.DoesNotContain("sprint-lane unrecognized", board);
        Assert.Contains("aria-label=\"In progress: 0 stories\"", board);

        // Populated columns never get an empty placeholder.
        Assert.DoesNotContain("sprint-lane-empty\">Nothing ready to pick up", board);
        Assert.DoesNotContain("sprint-lane-empty\">No cards in backlog", board);

        var noReady = SprintStatusParser.Parse("""
            development_status:
              1-1-a: in-progress
            """)!;
        Assert.Contains(
            "class=\"sprint-lane-empty\">Nothing ready to pick up — draft or refine the next story.",
            SprintTemplater.RenderBoard(noReady, epics));
        Assert.Contains(
            "class=\"sprint-lane-empty\">No cards in backlog",
            SprintTemplater.RenderBoard(noReady, epics));
    }

    [Fact]
    public void RenderBoard_EmptyLanePlaceholderMatchesOnCappedHomeBoard()
    {
        // Shared renderer: page (uncapped) and home (capped) empty lanes must match. [Story 8.6]
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: ready-for-dev
            """)!;
        var epics = EpicsWith(Epic(1, "E", Story("1.1", 1, "A", "epics/story-1-1.html")));

        var page = SprintTemplater.RenderBoard(sprint, epics);
        var home = SprintTemplater.RenderBoard(sprint, epics, capPerColumn: 3, moreHref: "sprint.html");

        Assert.Contains("class=\"sprint-lane-empty\">Nothing in progress — pick from Ready.", page);
        Assert.Contains("class=\"sprint-lane-empty\">Nothing in progress — pick from Ready.", home);
        Assert.Equal(
            CountOccurrences(page, "sprint-lane-empty"),
            CountOccurrences(home, "sprint-lane-empty"));
    }

    [Fact]
    public void RenderBoard_EmptyReadyLane_CarriesCreateStoryBadge_WhenUndraftedAndCatalogAllow()
    {
        // Story 9.8: Ready empty copy that implies drafting gets InlineGuidance when target + catalog exist.
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: in-progress
            """)!;
        var epics = EpicsWith(Epic(1, "E",
            Story("1.1", 1, "A", "epics/story-1-1.html"),
            Story("1.2", 1, "B", artifactOutputPath: null)));
        var catalog = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });

        var board = SprintTemplater.RenderBoard(sprint, epics, commands: catalog);

        Assert.Contains("sprint-lane-empty", board);
        Assert.Contains("/bmad-create-story 1.2", board);
        Assert.Contains("draft the next story with", board);
        Assert.Contains("cmd-badge", board);
    }

    [Fact]
    public void RenderBoard_EmptyReadyLane_KeepsDesignedCopy_WhenNoUndraftedOrNoCommand()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: in-progress
            """)!;
        var planned = EpicsWith(Epic(1, "E", Story("1.1", 1, "A", "epics/story-1-1.html")));
        var catalog = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });

        // All stories drafted → designed 8.6 copy, no badge.
        Assert.Contains(
            "class=\"sprint-lane-empty\">Nothing ready to pick up — draft or refine the next story.",
            SprintTemplater.RenderBoard(sprint, planned, commands: catalog));

        // Undrafted exists but catalog lacks create-story → degrade to designed copy (NFR8).
        var undrafted = EpicsWith(Epic(1, "E",
            Story("1.1", 1, "A", "epics/story-1-1.html"),
            Story("1.2", 1, "B", artifactOutputPath: null)));
        Assert.Contains(
            "class=\"sprint-lane-empty\">Nothing ready to pick up — draft or refine the next story.",
            SprintTemplater.RenderBoard(sprint, undrafted, commands: CommandCatalog.Empty));
        Assert.DoesNotContain("cmd-badge", SprintTemplater.RenderBoard(sprint, undrafted, commands: CommandCatalog.Empty));
    }

    [Fact]
    public void RenderIndex_HasRetrosLinkAndOpenItemsFlagButtonNotAModal()
    {
        var withOpen = SprintStatusParser.Parse("""
            development_status:
              epic-1: done

            action_items:
              - epic: 1
                action: "Add error-handling review to the checklist"
                status: open
            """)!;
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

        var html = SprintTemplater.RenderIndex(withOpen, null, Nav(), SprintCommands(), retros);

        // No modal — the buttons are plain links to real pages.
        Assert.DoesNotContain("retro-menu", html);
        // "Retros" link → retros.html (with a rich js-tip tooltip).
        Assert.Contains("<a class=\"cmd-menu-toggle js-tip\" href=\"retros.html\"", html);
        Assert.Contains("data-tip=\"1 retrospective\nLatest: Epic 1 Retrospective (2026-07-07)\"", html);
        // "⚑ 1" flag → action-items.html (only when open items exist).
        Assert.Contains("<a class=\"sprint-flag js-tip\" href=\"action-items.html\"", html);
        Assert.Contains("⚑ 1", html);
        // The command popout is relabelled "Commands".
        Assert.Contains(">Commands ▾</summary>", html);

        // No retros and no open items → neither button appears.
        var none = SprintStatusParser.Parse("development_status:\n  epic-1: in-progress\n  1-1-x: done\n")!;
        var bare = SprintTemplater.RenderIndex(none, null, Nav(), CommandCatalog.Empty);
        Assert.DoesNotContain("href=\"retros.html\"", bare);
        Assert.DoesNotContain("sprint-flag", bare);
    }

    [Fact]
    public void RenderIndex_HoldsSkipLinkAndSingleMainLandmark()
    {
        var html = SprintTemplater.RenderIndex(Sample(), SampleEpics(), Nav(), CommandCatalog.Empty);

        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
        Assert.Contains("from sprint-status.yaml", html);
    }

    [Fact]
    public void RenderBoard_LaneHeadsCarryStageMeaningTipsAndFocus()
    {
        // Core lanes always present; optional lanes asserted via the retired/unrecognized fixture below.
        var board = SprintTemplater.RenderBoard(Sample(), SampleEpics());

        foreach (var (cls, label) in new[]
                 {
                     ("pending", "Backlog"),
                     ("ready", "Ready for dev"),
                     ("active", "In progress"),
                     ("review", "In review"),
                     ("done", "Done"),
                 })
        {
            var tip = $"{label} = {StatusStyles.StageMeaning(cls)}";
            Assert.Contains($"class=\"sprint-lane {cls}\"", board);
            Assert.Contains($"data-tip=\"{tip}\"", board);
            Assert.Contains($"title=\"{tip}\"", board);
        }

        Assert.DoesNotContain("sprint-lane retired", board);
        Assert.DoesNotContain("sprint-lane unrecognized", board);
        Assert.Contains("sprint-lane-head js-tip", board);
        Assert.Contains("tabindex=\"0\"", board);
        // AC distinguishing phrases (StageMeaning seam, enriched for readiness).
        Assert.Contains("not yet ready to pick up", board, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task plan exists and dependencies met", board, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderBoard_OmitsEmptyRetiredAndUnrecognizedLanes()
    {
        var emptyOptional = SprintTemplater.RenderBoard(Sample(), SampleEpics());
        Assert.DoesNotContain("sprint-lane retired", emptyOptional);
        Assert.DoesNotContain("sprint-lane unrecognized", emptyOptional);
        Assert.Contains("--lane-count: 5", emptyOptional);

        var withOptional = SprintTemplater.RenderBoard(SprintStatusParser.Parse("""
            development_status:
              1-1-a: done
              3-4-retired: retired
              9-9-blocked: blocked
            """)!, null);
        Assert.Contains("sprint-lane retired", withOptional);
        Assert.Contains("sprint-lane unrecognized", withOptional);
        Assert.Contains("--lane-count: 7", withOptional);
        Assert.Contains($"data-tip=\"Retired = {StatusStyles.StageMeaning("retired")}\"", withOptional);
        Assert.Contains($"data-tip=\"Unrecognized = {StatusStyles.StageMeaning("unrecognized")}\"", withOptional);
    }

    [Fact]
    public void RenderBoard_NoPlanCardsGetDashedModifier_PlannedCardsDoNot()
    {
        var sprint = SprintStatusParser.Parse("development_status:\n  epic-2: in-progress\n  2-3-widget: in-progress\n  2-6-later: backlog\n  2-9-orphan: backlog\n")!;
        var epics = EpicsWith(Epic(2, "Rendering",
            Story("2.3", 2, "Sprint Widget", "epics/story-2-3.html", tasksDone: 3, tasksTotal: 8),
            Story("2.6", 2, "Later Story", artifactOutputPath: null))); // TasksTotal == 0

        var board = SprintTemplater.RenderBoard(sprint, epics);

        Assert.Contains("class=\"sprint-card js-tip active\"", board); // planned — no no-plan
        Assert.DoesNotContain("class=\"sprint-card js-tip active no-plan\"", board);
        Assert.Contains("class=\"sprint-card js-tip pending no-plan\"", board); // 2.6 + orphan
        Assert.Contains("role=\"progressbar\"", board); // planned story still gets the bar
    }

    [Fact]
    public void RenderBoard_RetiredStoryLandsInRetiredLaneNotUnrecognized()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              3-4-interactive-tree: retired
              1-1-a: done
              9-9-blocked: blocked
            """)!;
        var board = SprintTemplater.RenderBoard(sprint, null);

        Assert.Contains("sprint-lane retired", board);
        Assert.Contains("sprint-card js-tip retired", board);
        Assert.Contains("Interactive Tree", board); // prettified slug
        // Retired lane owns the retired card; unrecognized still owns truly unmapped words.
        var retiredLaneIdx = board.IndexOf("sprint-lane retired", StringComparison.Ordinal);
        var unrecognizedLaneIdx = board.IndexOf("sprint-lane unrecognized", StringComparison.Ordinal);
        Assert.True(retiredLaneIdx >= 0 && unrecognizedLaneIdx > retiredLaneIdx);
        var retiredLane = board[retiredLaneIdx..unrecognizedLaneIdx];
        Assert.Contains("sprint-card js-tip retired", retiredLane);
        Assert.DoesNotContain("sprint-card js-tip unrecognized", retiredLane);
        Assert.Contains("sprint-card js-tip unrecognized", board[unrecognizedLaneIdx..]);
    }

    [Fact]
    public void RenderProgressWheel_DenominatorExcludesRetired()
    {
        // Spec matrix: 41 done → use small numbers mirroring M = tracked − retired.
        var sprint = SprintStatusParser.Parse("""
            development_status:
              1-1-a: done
              1-2-b: done
              1-3-c: in-progress
              3-4-retired: retired
            """)!;
        var wheel = SprintTemplater.RenderProgressWheel(sprint);

        // 2 done / 3 non-retired tracked (done+done+active); retired excluded from M.
        Assert.Contains("2 / 3 done", wheel);
        Assert.Contains("Retired: 1", wheel);
        Assert.DoesNotContain("donut-seg retired", wheel);
    }

    [Fact]
    public void RenderProgressWheel_AllRetired_ReturnsEmpty()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              3-4-a: retired
              3-4-b: retired
            """)!;
        Assert.Equal(string.Empty, SprintTemplater.RenderProgressWheel(sprint));
    }

    [Fact]
    public void ActiveEpicNumbers_PrefersEpicsWithInFlightOrDoneAndOpenRetro()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-old: done
              epic-1-retrospective: done
              epic-3: in-progress
              3-1-live: in-progress
              epic-3-retrospective: optional
              epic-4: backlog
              4-1-later: backlog
              epic-4-retrospective: optional
            """)!;

        Assert.Equal(new[] { 3 }, SprintTemplater.ActiveEpicNumbers(sprint));
    }

    [Fact]
    public void ActiveEpicNumbers_FallsBackToFirstNonDoneEpic()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-old: done
              epic-1-retrospective: done
              epic-2: in-progress
              2-1-ready: ready-for-dev
              epic-2-retrospective: optional
              epic-3: backlog
              3-1-later: backlog
            """)!;

        // No in-progress/review/done with open retro → first epic-N ≠ done is epic 2.
        Assert.Equal(new[] { 2 }, SprintTemplater.ActiveEpicNumbers(sprint));
    }

    [Fact]
    public void RenderBoard_DefaultsToActiveEpicsAndKeepsOthersHidden()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-old: done
              epic-1-retrospective: done
              epic-3: in-progress
              3-1-live: in-progress
              epic-3-retrospective: optional
            """)!;
        var epics = EpicsWith(
            Epic(1, "Done Epic", Story("1.1", 1, "Old", "epics/story-1-1.html")),
            Epic(3, "Live", Story("3.1", 3, "Live Story", "epics/story-3-1.html")));

        var board = SprintTemplater.RenderBoard(sprint, epics);

        Assert.Contains("class=\"sprint-filterable\"", board);
        Assert.Contains("data-default-epics=\"3\"", board);
        Assert.Contains("data-epics=", board);
        Assert.DoesNotContain("sprint-epic-filter", board); // injected by JS — absent in SSR
        Assert.Contains("data-epic=\"3\"", board);
        Assert.Contains("data-epic=\"1\" hidden", board);
        Assert.Equal(1, CountVisibleSprintCards(board));
        Assert.Contains("Story 3.1", board);
    }

    [Fact]
    public void RenderBoard_CapAppliesAfterActiveEpicFilter()
    {
        // Mixed epics in the active lane: without filter-before-cap the first 3 would be epic 1+2;
        // with default=epic 2 only, the three visible cards must all be epic 2.
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-a: in-progress
              1-2-b: in-progress
              1-3-c: in-progress
              epic-1-retrospective: done
              epic-2: in-progress
              2-1-a: in-progress
              2-2-b: in-progress
              2-3-c: in-progress
              2-4-d: in-progress
              2-5-e: in-progress
              epic-2-retrospective: optional
            """)!;
        var epics = EpicsWith(
            Epic(1, "Old",
                Story("1.1", 1, "A", "epics/story-1-1.html"), Story("1.2", 1, "B", "epics/story-1-2.html"),
                Story("1.3", 1, "C", "epics/story-1-3.html")),
            Epic(2, "Live",
                Story("2.1", 2, "A", "epics/story-2-1.html"), Story("2.2", 2, "B", "epics/story-2-2.html"),
                Story("2.3", 2, "C", "epics/story-2-3.html"), Story("2.4", 2, "D", "epics/story-2-4.html"),
                Story("2.5", 2, "E", "epics/story-2-5.html")));

        var board = SprintTemplater.RenderBoard(sprint, epics, capPerColumn: 3, moreHref: "sprint.html");

        Assert.Contains("data-default-epics=\"2\"", board);
        Assert.Equal(3, CountVisibleSprintCards(board));
        Assert.Equal(3, CountOccurrences(board, "data-epic=\"2\" data-tip="));
        Assert.Contains("+2 more", board);
    }

    [Fact]
    public void RenderIndex_SharesOneEpicFilterAcrossBoardViews()
    {
        var html = SprintTemplater.RenderIndex(Sample(), SampleEpics(), Nav(), SprintCommands());
        Assert.Equal(1, CountOccurrences(html, "class=\"sprint-filterable\""));
        Assert.Contains("data-default-epics=\"1\"", html);
        Assert.Contains("class=\"board-view board-view-status\"", html);
        Assert.Contains("class=\"board-view board-view-epic\"", html);
        Assert.Contains("sprint-epic-lane\" data-epic=\"1\"", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    /// <summary>Counts sprint cards whose opening tag does not include the <c>hidden</c> attribute.</summary>
    private static int CountVisibleSprintCards(string html)
    {
        int count = 0, i = 0;
        const string needle = "sprint-card js-tip";
        while ((i = html.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            var tagEnd = html.IndexOf('>', i);
            if (tagEnd < 0) break;
            var open = html[i..tagEnd];
            if (!open.Contains(" hidden", StringComparison.Ordinal)) count++;
            i = tagEnd + 1;
        }
        return count;
    }
}
