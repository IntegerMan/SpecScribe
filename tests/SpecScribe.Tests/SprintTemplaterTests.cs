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

    private static StoryInfo Story(string id, int epic, string title, string? artifactOutputPath) => new()
    {
        Id = id,
        EpicNumber = epic,
        Title = title,
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        ArtifactOutputPath = artifactOutputPath,
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
    public void RenderIndex_RendersFiveColumnBoardWithMappedLanesLinkedCardsToggleAndCommands()
    {
        var html = SprintTemplater.RenderIndex(Sample(), SampleEpics(), Nav(), SprintCommands());

        // Five lifecycle lanes, each with the mapped status class.
        foreach (var cls in new[] { "pending", "ready", "active", "review", "done" })
            Assert.Contains($"<section class=\"sprint-lane {cls}\"", html);

        // Cards reuse now-next-card with the story's stage color; done story 1.1 and in-progress 1.2 link out.
        Assert.Contains("class=\"now-next-card done\"", html);
        Assert.Contains("class=\"now-next-card active\"", html);
        Assert.Contains("href=\"epics/story-1-1.html\"", html);

        // Pure-CSS toggle scaffolding (no JS) and both views present.
        Assert.Contains("id=\"sv-status\"", html);
        Assert.Contains("id=\"sv-epic\"", html);
        Assert.Contains("class=\"board-view board-view-status\"", html);
        Assert.Contains("class=\"board-view board-view-epic\"", html);
        Assert.Contains("class=\"sprint-epic-lane\"", html);

        // Standard command buttons for the sprint lifecycle (copy payloads).
        Assert.Contains("data-copy=\"/bmad-sprint-planning\"", html);
        Assert.Contains("data-copy=\"/bmad-retrospective\"", html);
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
        Assert.Equal(2, CountOccurrences(board, "now-next-card done"));

        // Uncapped render shows all six.
        var full = SprintTemplater.RenderBoard(sprint, epics);
        Assert.Equal(6, CountOccurrences(full, "now-next-card done"));
        Assert.DoesNotContain("sprint-lane-more", full);
    }

    [Fact]
    public void RenderIndex_ShowsActionItemsSectionOnlyWhenOpenItemsExist()
    {
        var withOpen = SprintStatusParser.Parse("""
            development_status:
              epic-1: done

            action_items:
              - epic: 1
                action: "Add error-handling review to the checklist"
                status: open
            """)!;
        Assert.Contains("Open retrospective action items", SprintTemplater.RenderIndex(withOpen, null, Nav(), CommandCatalog.Empty));

        var none = SprintStatusParser.Parse("development_status:\n  epic-1: in-progress\n  1-1-x: done\n")!;
        Assert.DoesNotContain("Open retrospective action items", SprintTemplater.RenderIndex(none, null, Nav(), CommandCatalog.Empty));
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
