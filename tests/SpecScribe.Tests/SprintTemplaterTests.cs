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

        // Six lifecycle lanes (incl. unrecognized for present-but-unmapped values), each with the mapped status class.
        foreach (var cls in new[] { "pending", "ready", "active", "review", "done", "unrecognized" })
            Assert.Contains($"<section class=\"sprint-lane {cls}\"", html);

        // Cards carry the story's stage color + the js-tip hook; done story 1.1 and in-progress 1.2 link out.
        Assert.Contains("class=\"sprint-card js-tip done\"", html);
        Assert.Contains("class=\"sprint-card js-tip active\"", html);
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
        Assert.Contains("class=\"sprint-card js-tip active\"", html);
        Assert.Contains("data-tip=\"Epic 2: Rendering\nStory 2.3: Sprint Widget\n3 of 8 tasks done\"", html);
        Assert.Contains("data-tip=\"Epic 2: Rendering\nStory 2.6: Later Story\nNo task plan yet\"", html);
        // Progress bar only for the story WITH a task plan: 3/8 = 38%, partial fill; no per-bar tooltip now.
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=\"38\"", html);
        Assert.Contains("class=\"sprint-card-progress-fill partial\" style=\"width:38%\"", html);
        Assert.DoesNotContain("data-tooltip=", html);
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
        // Six done stories, capped at 2 → two cards + a "+4 more" link to the target.
        var sprint = SprintStatusParser.Parse("""
            development_status:
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
        // Only the first 2 of 6 done cards are shown before the "more" link.
        Assert.Equal(2, CountOccurrences(board, "sprint-card js-tip done"));

        // Uncapped render shows all six.
        var full = SprintTemplater.RenderBoard(sprint, epics);
        Assert.Equal(6, CountOccurrences(full, "sprint-card js-tip done"));
        Assert.DoesNotContain("sprint-lane-more", full);
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

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
