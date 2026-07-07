using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Render-level coverage for Story 2.3 Task 3: the sprint page emits a mapped status badge per epic
/// and story, groups stories under their epic, links stories that have a generated page (and renders plain
/// text — never a broken link — for those that don't), and shows the open action-items section only when
/// there are open items. Also holds the Story 1.4 a11y floor (skip link + single main).</summary>
public class SprintTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);

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

    [Fact]
    public void RenderIndex_EmitsMappedBadgesGroupsStoriesAndResolvesOrPlainTextsLinks()
    {
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: in-progress
              1-1-foundation: done
              1-2-traceability: in-progress
              2-1-undrafted: backlog
            """)!;

        // Epic 1 is in the model (so it links); story 1.1 has a generated page, 1.2 does not; epic 2 / story
        // 2.1 are tracked but absent from the model, so they must render as plain text (never a broken link).
        var epics = EpicsWith(Epic(1, "Foundation",
            Story("1.1", 1, "Foundation Story", "epics/story-1-1.html"),
            Story("1.2", 1, "Traceability Story", artifactOutputPath: null)));

        var html = SprintTemplater.RenderIndex(sprint, epics, Nav());

        // Lifecycle → shared color classes: in-progress→active, done→done, backlog→pending.
        Assert.Contains("<span class=\"status-badge active\">In progress</span>", html);
        Assert.Contains("<span class=\"status-badge done\">Done</span>", html);
        Assert.Contains("<span class=\"status-badge pending\">Backlog</span>", html);

        // Story 1.1 links to its generated page; the epic header links to the epic page.
        Assert.Contains("href=\"epics/story-1-1.html\"", html);
        Assert.Contains("href=\"epics/epic-1.html\"", html);
        // Story 1.2 has no generated page → plain text, never a broken link.
        Assert.DoesNotContain("href=\"epics/story-1-2.html\"", html);
        // Epic 2 / story 2.1 aren't in the model → plain "Epic 2" + prettified slug, no broken links.
        Assert.DoesNotContain("href=\"epics/epic-2.html\"", html);
        Assert.Contains("Epic 2", html);
        Assert.Contains("Undrafted", html);

        // Grouping: the story rows live inside sprint-epic sections.
        Assert.Contains("class=\"sprint-epic\"", html);
        Assert.Contains("class=\"sprint-story-row\"", html);
    }

    [Fact]
    public void RenderIndex_ShowsActionItemsSectionOnlyWhenOpenItemsExist()
    {
        var withOpen = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              epic-1-retrospective: done

            action_items:
              - epic: 1
                action: "Add error-handling review to the checklist"
                status: open
            """)!;

        var withOpenHtml = SprintTemplater.RenderIndex(withOpen, epics: null, Nav());
        Assert.Contains("Open retrospective action items", withOpenHtml);
        Assert.Contains("Add error-handling review to the checklist", withOpenHtml);

        // Only-done action items → nothing to surface → section absent (not an empty header).
        var onlyDone = SprintStatusParser.Parse("""
            development_status:
              epic-1: done

            action_items:
              - epic: 1
                action: "Already handled"
                status: done
            """)!;
        Assert.DoesNotContain("Open retrospective action items", SprintTemplater.RenderIndex(onlyDone, epics: null, Nav()));

        // No action_items block at all (the live repo file) → section absent, no broken layout.
        var none = SprintStatusParser.Parse("development_status:\n  epic-1: in-progress\n  1-1-x: done\n")!;
        Assert.DoesNotContain("Open retrospective action items", SprintTemplater.RenderIndex(none, epics: null, Nav()));
    }

    [Fact]
    public void RenderIndex_HoldsSkipLinkAndSingleMainLandmark()
    {
        var sprint = SprintStatusParser.Parse("development_status:\n  epic-1: in-progress\n  1-1-x: done\n")!;
        var html = SprintTemplater.RenderIndex(sprint, epics: null, Nav());

        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
        // The source is named so it reads as the tracking-file view (Story 1.5 truthfulness).
        Assert.Contains("from sprint-status.yaml", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
