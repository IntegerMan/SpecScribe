using SpecScribe;

namespace SpecScribe.Tests;

public class HtmlTemplaterTests
{
    [Fact]
    public void RenderIndex_ShowsDashboardQuickLinksForAvailableSections()
    {
        var nav = SiteNav.Build(new[]
        {
            "planning-artifacts/epics.md",
            "game-architecture.md",
        }, "SpecScribe", ModuleContext.DocsFor(BmadModule.GameDevStudio), hasAdrs: true);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("dashboard-quick-links", html);
        Assert.Contains("href=\"epics.html\"", html);
        Assert.Contains("href=\"requirements.html\"", html);
        Assert.Contains("href=\"adrs/index.html\"", html);
        Assert.Contains("href=\"game-architecture.html\"", html);
    }

    [Fact]
    public void RenderIndex_ShowsReadmeQuickLinkWhenReadmeAvailable()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasReadme: true);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("dashboard-quick-links", html);
        Assert.Contains("href=\"readme.html\"", html);
        Assert.Contains("Read the project overview.", html);
    }

    [Fact]
    public void RenderIndex_EmitsSkipLinkAndSingleMainLandmark()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // Skip link is first-focusable and targets the one main landmark. [Story 1.4 AC #1, UX-DR16]
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderIndex_ProgressBarsCarryProgressbarAria()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The dashboard's overall-progress bars must expose progressbar semantics with a current value. [Story 1.4 AC #1]
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=", html);
        Assert.Contains("aria-valuemin=\"0\"", html);
        Assert.Contains("aria-valuemax=\"100\"", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    [Fact]
    public void RenderIndex_OmitsQuickLinksPanelWhenOnlyHomeExists()
    {
        var nav = SiteNav.Build(Array.Empty<string>(), "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.DoesNotContain("dashboard-quick-links", html);
        Assert.DoesNotContain("href=\"epics.html\"", html);
        Assert.DoesNotContain("href=\"requirements.html\"", html);
        Assert.DoesNotContain("href=\"adrs/index.html\"", html);
    }

    private static StoryInfo Story(string id, string? status) => new()
    {
        Id = id,
        EpicNumber = 1,
        Title = "A story",
        UserStoryHtml = string.Empty,
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
        TasksDone = 0,
        TasksTotal = 0,
    };

    private static EpicsModel ModelWith(EpicStatus epicStatus, params StoryInfo[] stories) => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            new EpicInfo
            {
                Number = 1,
                Title = "First Epic",
                GoalHtml = string.Empty,
                Status = epicStatus,
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            },
        },
    };

    private static ProgressModel ProgressWithCommits(int totalCommits)
    {
        var day = new DateOnly(2026, 1, 5);
        return new ProgressModel
        {
            EpicsTotal = 1,
            EpicsDrafted = 1,
            EpicsPending = 0,
            StoriesTotal = 1,
            StoriesWithArtifact = 1,
            TasksDone = 0,
            TasksTotal = 0,
            PerEpic = Array.Empty<EpicProgress>(),
            // CommitsByDay stays consistent with the series (production can never emit a mismatch), so
            // templater tests exercise the real linked-cell path.
            Git = new GitPulse(totalCommits, 1, day, day, new (DateOnly, int)[] { (day, totalCommits) },
                new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
                {
                    [day] = Enumerable.Range(1, totalCommits)
                        .Select(i => new CommitInfo($"c{i:000}", $"Change {i}", "Alice", "12:00"))
                        .ToList(),
                },
                LastCommitTimestamp: day.ToDateTime(new TimeOnly(12, 0)),
                Last30DayCommitCount: totalCommits,
                TopChangedFiles: new (string, int)[] { ("src/Program.cs", 3), ("README.md", 1) }),
        };
    }

    [Theory]
    [InlineData(1, "Commit")]
    [InlineData(7, "Commits")]
    public void RenderIndex_PluralizesCommitStatLabel(int totalCommits, string expectedLabel)
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithCommits(totalCommits),
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // "1 Commits" was the reported defect — the count-bearing label must agree in number. [Story 1.5 A2]
        Assert.Contains($"class=\"stat-label\">{expectedLabel}</div>", html);
    }

    [Fact]
    public void RenderIndex_WiresHeatmapDayLinksThroughDashboard()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithCommits(3),
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The call-site wiring (CommitsByDay → CommitHeatmap) surfaces the per-day page link on the dashboard.
        // The commit content itself now lives on commits/2026-01-05.html, not inline.
        Assert.Contains("<a href=\"commits/2026-01-05.html\"", html);
        Assert.DoesNotContain("#heat-day-", html);
        Assert.DoesNotContain("Change 1", html);
    }

    [Fact]
    public void RenderIndex_RendersGitPulsePanelWithBaselineSignals()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithCommits(7),
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The three FR-9 baseline signals all surface in the new panel. [Story 3.1 AC #1]
        Assert.Contains("<h3>Git Pulse</h3>", html);
        Assert.Contains("in the last 30 days", html);                 // 30-day rolling count
        Assert.Contains("Mon, Jan 5, 2026 at 12:00", html);           // exact last-commit timestamp
        Assert.Contains("src/Program.cs", html);                      // top changed file
        Assert.Contains("Top changed files", html);

        // Consolidated + graphical presentation. [Story 3.1 AC #3 — owner review follow-up]
        Assert.Contains("git-pulse-body", html);                      // the merged two-part body
        Assert.Contains("git-pulse-bar-fill", html);                  // top files as proportional bars
        Assert.Contains("class=\"heatmap\"", html);                   // activity heatmap embedded in the same panel
        Assert.DoesNotContain("<h3>Commit Activity</h3>", html);      // the separate heatmap panel is gone
    }

    [Fact]
    public void RenderIndex_RendersGitPulseFallbackWhenNoGitHistory()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty, // Git is null
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The exact UX-spec empty-state copy: an em-dash with the specified tooltip. [Story 3.1 AC #2; EXPERIENCE.md:169]
        Assert.Contains("<h3>Git Pulse</h3>", html);
        Assert.Contains("data-tooltip=\"Run in a git repository to enable commit stats\"", html);
    }

    // A ProgressModel with both baseline git and the opt-in deep-git payload populated, for the --deep-git panel.
    private static ProgressModel ProgressWithDeepGit()
    {
        var day = new DateOnly(2026, 1, 5);
        return new ProgressModel
        {
            EpicsTotal = 1,
            EpicsDrafted = 1,
            EpicsPending = 0,
            StoriesTotal = 1,
            StoriesWithArtifact = 1,
            TasksDone = 0,
            TasksTotal = 0,
            PerEpic = Array.Empty<EpicProgress>(),
            Git = new GitPulse(3, 1, day, day, new (DateOnly, int)[] { (day, 3) },
                new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
                {
                    [day] = new[] { new CommitInfo("c001", "Change", "Alice", "12:00") },
                },
                LastCommitTimestamp: day.ToDateTime(new TimeOnly(12, 0)),
                Last30DayCommitCount: 3,
                TopChangedFiles: new (string, int)[] { ("src/Program.cs", 3) }),
            DeepGit = new DeepGitPulse(
                Hotspots: new (string, int)[] { ("src/HtmlTemplater.cs", 9), ("src/Charts.cs", 4) },
                Coupling: new (string, string, int)[] { ("src/Charts.cs", "src/HtmlTemplater.cs", 5) }),
        };
    }

    [Fact]
    public void RenderIndex_RendersDeepAnalyticsPanelDistinctlyWhenDeepGitPopulated()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithDeepGit(),
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // AC #2: the deep insights get their own labeled panel, distinct from the baseline Git Pulse surface.
        Assert.Contains("deep-git-panel", html);
        Assert.Contains("Deep Analytics", html);
        Assert.Contains("Git Hotspots", html);
        Assert.Contains("Change Coupling", html);
        // The actual signals render: a hotspot path and a coupling pair with its co-change count.
        Assert.Contains("src/HtmlTemplater.cs", html);
        Assert.Contains("src/Charts.cs", html);
        Assert.Contains("5&times; together", html);
    }

    [Fact]
    public void RenderIndex_OmitsDeepAnalyticsPanelEntirelyWhenDeepGitNull()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressWithCommits(3), // Git present, DeepGit null (the default, no --deep-git)
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // Subtask 3.4: unlike the baseline "—" placeholders, the deep panel simply does not exist when not
        // opted into, so the default dashboard is unchanged for users who never pass --deep-git.
        Assert.DoesNotContain("deep-git-panel", html);
        Assert.DoesNotContain("Deep Analytics", html);
        // ...while the baseline Git Pulse panel is still there, proving only the deep surface was omitted.
        Assert.Contains("<h3>Git Pulse</h3>", html);
    }

    [Fact]
    public void RenderIndex_EmitsFaviconDescriptionAndDashboardTitle()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // Home <title> is descriptive; favicon + description/OG land so shared links aren't bare. [Story 1.5 G1/G2]
        Assert.Contains("<title>SpecScribe — Project Dashboard</title>", html);
        Assert.Contains("<link rel=\"icon\"", html);
        Assert.Contains("<meta name=\"description\"", html);
    }

    [Fact]
    public void RenderIndex_SurfacesProjectAtAGlanceAheadOfExploreKeyViews()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "ready for dev")),
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // The most valuable panel leads; the slimmed link grid trails it. [Story 1.5 F1]
        var glance = html.IndexOf("Project at a Glance", StringComparison.Ordinal);
        var explore = html.IndexOf("Explore Key Views", StringComparison.Ordinal);
        Assert.True(glance >= 0 && explore >= 0, "both panels should render");
        Assert.True(glance < explore, "Project at a Glance must appear before Explore Key Views");
    }

    [Fact]
    public void RenderIndex_QuickLinkPillsCarryFamilyAccents()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: null,
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        // Slimmed to pills, accented by artifact family. [Story 1.5 F1/B5]
        Assert.Contains("quick-link-pill family-epics", html);
        Assert.Contains("quick-link-pill family-requirements", html);
    }

    [Fact]
    public void RenderIndex_EpicStatusDonutReflectsStoryRollup()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        // An epic with an in-development story rolls up to "active" — the donut must say "In development",
        // not the old binary drafted/pending split that contradicted the sunburst. [Story 1.5 A3]
        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(),
            nav: nav,
            progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "in progress")),
            requirements: null,
            adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty);

        Assert.Contains("Epic Status", html);
        Assert.Contains("In development (1)", html);
        // Zero-count rows are suppressed (B4): no "Done (0)" noise.
        Assert.DoesNotContain("Done (0)", html);
    }

    [Fact]
    public void RenderIndex_NowAndNextBecomesSprintBoardAndSurfacesOpenRetroItemsWithDeferredWork()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var sprint = SprintStatusParser.Parse("""
            development_status:
              epic-1: done
              1-1-first-story: done

            action_items:
              - epic: 1
                action: "Route deferred tech debt"
                status: open
            """);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(), nav: nav, progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "done")), requirements: null, adrs: Array.Empty<AdrEntry>(),
            commands: CommandCatalog.Empty, work: null, sprint: sprint);

        // AC #2: with sprint data, Now & Next BECOMES the sprint board (the tracked view), labeled with its
        // source and carrying a CTA to the full sprint page — no separate "Sprint Status" panel. [Story 2.3]
        Assert.Contains("chart-panel sprint-board-panel", html);
        Assert.Contains("Now &amp; Next <span class=\"panel-source-inline\">from sprint-status.yaml</span>", html);
        Assert.Contains("class=\"sprint-board\"", html);
        Assert.Contains("href=\"sprint.html\"", html);
        // Open retro action items are surfaced as a callout (beside deferred work) linking to the sprint page.
        Assert.Contains("work-callout retro-callout", html);
        Assert.Contains("Retro Action Items", html);
        Assert.Contains("1 open item", html);
    }

    [Fact]
    public void RenderIndex_OmitsSprintWidgetWhenNoSprintDataAndLeavesDashboardIntact()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

        var html = HtmlTemplater.RenderIndex(
            docs: Array.Empty<DocModel>(), nav: nav, progress: ProgressModel.Empty,
            epicsModel: ModelWith(EpicStatus.Drafted, Story("1.1", "ready for dev")),
            requirements: null, adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty,
            sprint: null);

        // No sprint widget markers at all (clean omission, not an empty panel)...
        Assert.DoesNotContain("from sprint-status.yaml", html);
        Assert.DoesNotContain("View Sprint", html);
        // ...and the sunburst panel + a11y floor are unaffected.
        Assert.Contains("Project at a Glance", html);
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderStoryPlaceholder_WrapsAcPanelInDashboardNarrowForAlignment()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var epic = new EpicInfo
        {
            Number = 7, Title = "Code and Git", GoalHtml = string.Empty,
            Status = EpicStatus.Pending, Section = EpicSection.FurtherDevelopment, Stories = Array.Empty<StoryInfo>(),
        };
        var story = new StoryInfo
        {
            Id = "7.2", EpicNumber = 7, Title = "Source Citation",
            UserStoryHtml = "<p>As a contributor…</p>", AcBlocksHtml = new[] { "<div>AC 1</div>" },
        };

        var html = EpicsTemplater.RenderStoryPlaceholder(epic, story, nav, CommandCatalog.Empty);

        // The AC panel is wrapped in dashboard-narrow (860 centered) so it aligns with the header/lead/note
        // instead of spilling edge-to-edge like a bare .chart-panel. [Story 2.3 redesign]
        var sec = html.IndexOf("<section class=\"dashboard-narrow\">", StringComparison.Ordinal);
        var ac = html.IndexOf("class=\"chart-panel ac-panel\"", StringComparison.Ordinal);
        Assert.True(sec >= 0 && ac > sec, "AC panel must be wrapped by dashboard-narrow");
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));

        // The "Back to Epic" link has no width/margin of its own — it must sit inside its own
        // dashboard-narrow section too, or it renders flush against <main>'s full-width left edge instead
        // of lining up under the header/note/AC panel above it.
        var lastSec = html.LastIndexOf("<section class=\"dashboard-narrow\">", StringComparison.Ordinal);
        var backLink = html.IndexOf("view-epic-link", StringComparison.Ordinal);
        Assert.True(lastSec >= 0 && lastSec != sec && backLink > lastSec, "Back-to-epic link must be wrapped by its own dashboard-narrow section");
    }

    private static DocModel Doc(string sourceRel, string outputRel, string title, Frontmatter fm, string bodyHtml = "") => new()
    {
        SourceRelativePath = sourceRel,
        OutputRelativePath = outputRel,
        Title = title,
        Frontmatter = fm,
        BodyHtml = bodyHtml,
        Headings = Array.Empty<Heading>(),
    };

    private static ProgressModel ProgressWith(int epicsDrafted, int epicsTotal, int storiesTotal, int storiesWithArtifact, int tasksDone, int tasksTotal) => new()
    {
        EpicsTotal = epicsTotal,
        EpicsDrafted = epicsDrafted,
        EpicsPending = epicsTotal - epicsDrafted,
        StoriesTotal = storiesTotal,
        StoriesWithArtifact = storiesWithArtifact,
        TasksDone = tasksDone,
        TasksTotal = tasksTotal,
        PerEpic = Array.Empty<EpicProgress>(),
    };

    private static CommandCatalog BmadCatalog() => new("BMad", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
    });

    [Fact]
    public void RenderIndex_SurfacesQuickDevAndDeferredAsFirstClassWorkWithStatusBadge()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var quickDev = Doc("implementation-artifacts/spec-foo.md", "implementation-artifacts/spec-foo.html", "A quick fix",
            new Frontmatter { Status = "done", Route = "one-shot", Type = "chore" });
        var deferred = Doc("implementation-artifacts/deferred-work.md", "implementation-artifacts/deferred-work.html", "Deferred Work",
            Frontmatter.Empty, "<ul><li>one</li><li>two</li></ul>");
        var plain = Doc("implementation-artifacts/some-note.md", "implementation-artifacts/some-note.html", "Some Note", Frontmatter.Empty);
        var docs = new[] { quickDev, deferred, plain };

        var html = HtmlTemplater.RenderIndex(docs, nav, ProgressModel.Empty, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Build(docs));

        // Dedicated first-class section with a status badge (not flat text) for the quick-dev entry. The badge
        // carries its icon ahead of the still-present status word (Story 2.5: never icon-only).
        Assert.Contains("Direct &amp; Quick-Dev Work", html);
        Assert.Contains("class=\"status-badge done\"", html);
        Assert.Contains(">done</span>", html);
        // Deferred-work callout with its open-item count.
        Assert.Contains("work-callout", html);
        Assert.Contains("2 open items", html);
        // Not double-listed: the quick-dev + deferred docs are promoted out of the generic grid (their page
        // link appears exactly once), while the plain artifact stays in the grid. [Story 2.1 Task 2]
        Assert.Equal(1, CountOccurrences(html, "href=\"implementation-artifacts/spec-foo.html\""));
        Assert.Equal(1, CountOccurrences(html, "href=\"implementation-artifacts/deferred-work.html\""));
        Assert.Contains("href=\"implementation-artifacts/some-note.html\"", html);
    }

    [Fact]
    public void RenderIndex_ShowsSeparateDirectChangesStatWithoutAlteringEpicStoryTaskTallies()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var progress = ProgressWith(epicsDrafted: 1, epicsTotal: 2, storiesTotal: 5, storiesWithArtifact: 3, tasksDone: 4, tasksTotal: 10);
        var quickDev = Doc("implementation-artifacts/spec-foo.md", "implementation-artifacts/spec-foo.html", "A quick fix",
            new Frontmatter { Status = "done", Route = "one-shot" });
        var docs = new[] { quickDev };

        var withWork = HtmlTemplater.RenderIndex(docs, nav, progress, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Build(docs));
        var withoutWork = HtmlTemplater.RenderIndex(Array.Empty<DocModel>(), nav, progress, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Empty);

        // The separate "Direct changes" signal is present, counting the quick-dev work.
        Assert.Contains("Direct changes", withWork);
        Assert.Contains(">1</div><div class=\"stat-label\">Direct changes</div>", withWork);
        // ...but the epic/story/task tallies are byte-for-byte unchanged whether or not the quick-dev doc is
        // present — quick-dev work must never inflate epic/story completion. [Story 2.1 Task 3]
        foreach (var tally in new[]
        {
            ">1/2</div><div class=\"stat-label\">Epics drafted</div>",
            ">5</div><div class=\"stat-label\">Stories defined</div>",
            ">4/10</div><div class=\"stat-label\">Planned tasks done</div>",
        })
        {
            Assert.Contains(tally, withWork);
            Assert.Contains(tally, withoutWork);
        }
        // And no "Direct changes" card at all when there's no such work (four-card row preserved).
        Assert.DoesNotContain("Direct changes", withoutWork);
    }

    [Fact]
    public void RenderIndex_EmitsSpecKernelSectionWithClearTitleAndKeepsItOutOfOther()
    {
        // Nav built without the spec source so this test isolates the index grouping — the kernel quick-link
        // pill (which also targets SPEC.html) is exercised separately in SiteNavTests.
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        // The SPEC hub's own H1 is the generic project name "SpecScribe"; it carries id: SPEC-* frontmatter so
        // its index card must read as a clear, disambiguating label instead. Companions title clearly from H1.
        var spec = Doc("specs/spec-x/SPEC.md", "specs/spec-x/SPEC.html", "SpecScribe", new Frontmatter { Id = "SPEC-x" });
        var companion = Doc("specs/spec-x/requirements-catalog.md", "specs/spec-x/requirements-catalog.html", "Requirements Catalog", Frontmatter.Empty);
        var docs = new[] { spec, companion };

        var html = HtmlTemplater.RenderIndex(docs, nav, ProgressModel.Empty, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Build(docs));

        // Labeled "Spec Kernel" band (AC #1), not the generic "Other" bucket. The band's icon (Story 2.5)
        // rides ahead of the still-present text.
        Assert.Contains("class=\"index-section-title\">", html);
        Assert.Contains(">Spec Kernel</div>", html);
        Assert.DoesNotContain("<div class=\"index-section-title\">Other</div>", html);
        // Both kernel docs are carded under it; the SPEC hub carries the clear title, not a bare "SpecScribe".
        Assert.Contains(">SPEC — Canonical Contract</h2>", html);
        Assert.DoesNotContain(">SpecScribe</h2>", html);
        Assert.Contains(">Requirements Catalog</h2>", html);
        // Each kernel doc is listed exactly once (claimed by the Spec Kernel group, not double-listed).
        Assert.Equal(1, CountOccurrences(html, "href=\"specs/spec-x/SPEC.html\""));
        Assert.Equal(1, CountOccurrences(html, "href=\"specs/spec-x/requirements-catalog.html\""));
    }

    [Fact]
    public void RenderIndex_OmitsSpecKernelSectionWhenNoSpecDocs()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var docs = new[] { Doc("implementation-artifacts/x.md", "implementation-artifacts/x.html", "X", Frontmatter.Empty) };

        var html = HtmlTemplater.RenderIndex(docs, nav, ProgressModel.Empty, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Build(docs));

        // No specs/ content → the empty-group guard omits the section cleanly (AC #2 graceful degradation).
        Assert.DoesNotContain("Spec Kernel", html);
    }

    [Fact]
    public void RenderPage_RendersCompanionDocsBlockOnlyWhenResolvedCompanionsPresent()
    {
        var nav = SiteNav.Build(new[] { "specs/spec-x/SPEC.md" }, "SpecScribe", hasAdrs: false);

        var withCompanions = Doc("specs/spec-x/SPEC.md", "specs/spec-x/SPEC.html", "SpecScribe", new Frontmatter { Id = "SPEC-x" });
        withCompanions.Companions = new[] { ("Requirements Catalog", "requirements-catalog.html") };
        var withHtml = HtmlTemplater.RenderPage(withCompanions, nav);
        Assert.Contains("class=\"companion-docs\"", withHtml);
        Assert.Contains("Companion documents", withHtml);
        Assert.Contains("<a href=\"requirements-catalog.html\">Requirements Catalog</a>", withHtml);

        // A doc with no resolved companions renders no block (every non-spec page is unaffected).
        var plain = Doc("planning-artifacts/prd.md", "planning-artifacts/prd.html", "PRD", Frontmatter.Empty);
        var plainHtml = HtmlTemplater.RenderPage(plain, nav);
        Assert.DoesNotContain("companion-docs", plainHtml);
    }

    [Fact]
    public void RenderEpicsIndex_EmptyModelEmitsCreateEpicsGuidanceWhenModuleExposesIt()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var empty = new EpicsModel { OverviewHtml = string.Empty, RequirementsInventoryHtml = string.Empty, Epics = Array.Empty<EpicInfo>() };

        var withCmd = EpicsTemplater.RenderIndex(empty, ProgressModel.Empty, nav, BmadCatalog());
        Assert.Contains("/bmad-create-epics-and-stories", withCmd);
        Assert.Contains("data-copy=\"/bmad-create-epics-and-stories\"", withCmd);

        // A module that exposes no such command prints guidance WITHOUT inventing a command.
        var noCmd = EpicsTemplater.RenderIndex(empty, ProgressModel.Empty, nav, CommandCatalog.Empty);
        Assert.DoesNotContain("/bmad-create-epics-and-stories", noCmd);
        Assert.DoesNotContain("data-copy", noCmd);
        Assert.Contains("No epics yet", noCmd);
    }

    [Fact]
    public void RenderNextSteps_EmitsSplitButtonWithUrlEncodedCursorDeeplink()
    {
        var story = Story("1.1", status: "ready-for-dev"); // ready => dev-story <id> suggestion
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
            ["code-review"] = "/bmad-code-review",
        });

        var html = BmadCommands.RenderNextSteps(story, commands);

        // Unified badge: the command text lives inside the badge, and the primary Copy button (now an
        // icon carrying its data-copy payload) is preserved.
        Assert.Contains("class=\"cmd-badge\"", html);
        Assert.Contains("<code class=\"cmd-text\">/bmad-dev-story 1.1</code>", html);
        Assert.Contains("class=\"cmd-copy\" data-copy=\"/bmad-dev-story 1.1\"", html); // command+icon = one click-to-copy button
        Assert.Contains("<svg class=\"icon\"", html); // the copy icon sits beside the command
        Assert.Contains("data-tooltip=\"Copy command\"", html); // rich on-brand tooltip on the icon button
        // The send menu leads with a plain "Copy" row (a second copy trigger), then the deep links.
        Assert.Contains("<details class=\"send-menu\">", html);
        Assert.Contains("<summary class=\"send-toggle\"", html);
        Assert.Contains("class=\"send-item\" data-copy=\"/bmad-dev-story 1.1\" aria-label=\"Copy command\">Copy</button>", html);
        // The Cursor deep link carries the URL-encoded command (slash -> %2F, space -> %20).
        Assert.Contains(
            "<a class=\"send-item\" href=\"cursor://anysphere.cursor-deeplink/prompt?text=%2Fbmad-dev-story%201.1\">Open in Cursor</a>", html);
    }

    [Fact]
    public void RenderEpicsIndex_PendingEpicCardPairsGuidanceWithCreateEpicsCommand()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var model = ModelWith(EpicStatus.Pending); // pending epic, no stories

        var html = EpicsTemplater.RenderIndex(model, ProgressModel.Empty, nav, BmadCatalog());

        Assert.Contains("Stories not yet drafted — draft them with", html);
        Assert.Contains("/bmad-create-epics-and-stories", html);
    }

    [Fact]
    public void RenderEpic_UndraftedStoryCardPairsGuidanceWithCreateStoryCommand()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var epic = new EpicInfo
        {
            Number = 1,
            Title = "First Epic",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("1.1", status: null) }, // listed, no artifact/plan yet
        };
        var progress = new EpicProgress
        {
            Number = 1, Title = "First Epic", StoryCount = 1, StoriesWithArtifact = 0,
            TasksDone = 0, TasksTotal = 0, Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int>(),
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, BmadCatalog());

        Assert.Contains("No detailed story plan yet — draft it with", html);
        Assert.Contains("/bmad-create-story 1.1", html);
    }

    [Fact]
    public void RenderEpic_LinksToExistingRetroWhenPresent()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var epic = new EpicInfo
        {
            Number = 1, Title = "First Epic", GoalHtml = string.Empty,
            Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("1.1", status: "In progress") },
        };
        var progress = new EpicProgress
        {
            Number = 1, Title = "First Epic", StoryCount = 1, StoriesWithArtifact = 1,
            TasksDone = 0, TasksTotal = 0, Status = EpicStatus.Drafted, StoryStatusCounts = new Dictionary<string, int>(),
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, CommandCatalog.Empty,
            epicRetroPath: "implementation-artifacts/epic-1-retro-2026-07-07.html");

        // Retro link resolves at the epic page's depth-1 prefix.
        Assert.Contains("class=\"epic-retro-link\" href=\"../implementation-artifacts/epic-1-retro-2026-07-07.html\">View Epic 1 Retrospective &rarr;</a>", html);
    }

    [Fact]
    public void RenderEpic_CompleteEpicWithoutRetroSuggestsRetrospectiveCommand()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var retroCat = new CommandCatalog("BMad", new Dictionary<string, string> { ["retrospective"] = "/bmad-retrospective" });

        EpicInfo Epic(string storyStatus) => new()
        {
            Number = 2, Title = "Done Epic", GoalHtml = string.Empty,
            Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice,
            Stories = new[] { Story("2.1", status: storyStatus) },
        };
        var progress = new EpicProgress
        {
            Number = 2, Title = "Done Epic", StoryCount = 1, StoriesWithArtifact = 1,
            TasksDone = 0, TasksTotal = 0, Status = EpicStatus.Drafted, StoryStatusCounts = new Dictionary<string, int>(),
        };

        // Complete epic (story done), no retro, command exposed → suggestion.
        var done = EpicsTemplater.RenderEpic(Epic("Done"), progress, nav, retroCat);
        Assert.Contains("Epic 2 is complete — capture the lessons with", done);
        Assert.Contains("/bmad-retrospective", done);

        // Incomplete epic (story in review) → no suggestion.
        var incomplete = EpicsTemplater.RenderEpic(Epic("In review"), progress, nav, retroCat);
        Assert.DoesNotContain("capture the lessons", incomplete);

        // Complete epic but the module doesn't expose the command → nothing (graceful).
        var noCmd = EpicsTemplater.RenderEpic(Epic("Done"), progress, nav, CommandCatalog.Empty);
        Assert.DoesNotContain("capture the lessons", noCmd);
    }

    [Fact]
    public void RenderStoryAndPlaceholder_LinkBackToEpicRetro()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false, hasSprint: true);
        var epic = new EpicInfo
        {
            Number = 1, Title = "First Epic", GoalHtml = string.Empty,
            Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
        };
        var retroPath = "implementation-artifacts/epic-1-retro-2026-07-07.html";

        var drafted = new StoryInfo { Id = "1.1", EpicNumber = 1, Title = "Drafted Story", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = "epics/story-1-1.html", Status = "Done" };
        var storyHtml = EpicsTemplater.RenderStory(epic, drafted, "implementation-artifacts/1-1.md",
            string.Empty, string.Empty, Array.Empty<AcceptanceCriterion>(),
            Array.Empty<(string, string)>(), Array.Empty<TaskItem>(), string.Empty, string.Empty,
            nav, CommandCatalog.Empty, epicRetroPath: retroPath);
        Assert.Contains($"class=\"pill pill-link\" href=\"../{retroPath}\">Epic 1 retro &rarr;</a>", storyHtml);

        var undrafted = new StoryInfo { Id = "1.2", EpicNumber = 1, Title = "Undrafted", UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), ArtifactOutputPath = null };
        var placeholder = EpicsTemplater.RenderStoryPlaceholder(epic, undrafted, nav, CommandCatalog.Empty, epicRetroPath: retroPath);
        Assert.Contains($"class=\"pill pill-link\" href=\"../{retroPath}\">Epic 1 retro &rarr;</a>", placeholder);

        // No retro path → no back-link.
        var noLink = EpicsTemplater.RenderStoryPlaceholder(epic, undrafted, nav, CommandCatalog.Empty);
        Assert.DoesNotContain("Epic 1 retro", noLink);
    }

    [Fact]
    public void RenderEpic_EmitsSkipLinkAndSingleMainLandmark()
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        var epic = new EpicInfo
        {
            Number = 1,
            Title = "First Epic",
            GoalHtml = string.Empty,
            Status = EpicStatus.Drafted,
            Section = EpicSection.VerticalSlice,
            Stories = Array.Empty<StoryInfo>(),
        };
        var progress = new EpicProgress
        {
            Number = 1,
            Title = "First Epic",
            StoryCount = 0,
            StoriesWithArtifact = 0,
            TasksDone = 0,
            TasksTotal = 0,
            Status = EpicStatus.Drafted,
            StoryStatusCounts = new Dictionary<string, int>(),
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, CommandCatalog.Empty);

        // The heavily-refactored detail templater must carry the same skip link + exactly one main landmark
        // as the dashboard — guards against a duplicate/zero-landmark regression in RenderEpic. [Story 1.4 AC #1, UX-DR16]
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    // ---- Story 2.4: planning-artifacts grouping, badges, PRD prominence, rubric fold ----

    private const string PrdOut = "planning-artifacts/prds/prd-x/prd.html";
    private const string RubricOut = "planning-artifacts/prds/prd-x/review-rubric.html";
    private const string BriefOut = "planning-artifacts/briefs/brief.html";

    private static DocModel PrdDoc() => Doc("planning-artifacts/prds/prd-x/prd.md", PrdOut, "SpecScribe PRD",
        new Frontmatter { Status = "final", Date = "2026-07-05", Author = "John" });
    private static DocModel RubricDoc() => Doc("planning-artifacts/prds/prd-x/review-rubric.md", RubricOut,
        "PRD Quality Review — SpecScribe", Frontmatter.Empty);
    private static DocModel BriefDoc() => Doc("planning-artifacts/briefs/brief.md", BriefOut, "Product Brief",
        new Frontmatter { Status = "draft" });
    private static DocModel DesignDoc() => Doc("planning-artifacts/ux-designs/ux-x/DESIGN.md",
        "planning-artifacts/ux-designs/ux-x/DESIGN.html", "UX Design", new Frontmatter { Status = "final" });
    private static DocModel ExperienceDoc() => Doc("planning-artifacts/ux-designs/ux-x/EXPERIENCE.md",
        "planning-artifacts/ux-designs/ux-x/EXPERIENCE.html", "UX Experience", new Frontmatter { Status = "final" });

    private static string RenderPlanning(params DocModel[] docs)
    {
        var nav = SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);
        return HtmlTemplater.RenderIndex(docs, nav, ProgressModel.Empty, epicsModel: null, requirements: null,
            adrs: Array.Empty<AdrEntry>(), commands: CommandCatalog.Empty, work: WorkInventory.Build(docs));
    }

    [Fact]
    public void RenderIndex_PlanningSection_BadgesPrdProminentUxPairedRubricFoldedUnderPrd()
    {
        var html = RenderPlanning(BriefDoc(), ExperienceDoc(), RubricDoc(), PrdDoc(), DesignDoc());

        // (a) status is an on-brand badge with the mapped class — not the old " · "-joined plain text. The
        // badge's decorative icon rides ahead of the still-present status word (Story 2.5).
        Assert.Contains("class=\"status-badge done\"", html);   // PRD: final → done/"Final"
        Assert.Contains(">Final</span>", html);
        Assert.Contains("class=\"status-badge drafted\"", html); // brief: draft → drafted/"Draft"
        Assert.Contains(">Draft</span>", html);
        Assert.DoesNotContain("final · 2026-07-05", html);                          // no middot status text run

        // (b) the PRD is a prominent primary card, ahead of the brief and the UX pair.
        Assert.Contains("class=\"index-card index-card--primary\"", html);
        Assert.Contains($"<h2><a href=\"{PrdOut}\">SpecScribe PRD</a></h2>", html);
        var prdPos = html.IndexOf("index-card--primary", StringComparison.Ordinal);
        Assert.True(prdPos >= 0 && prdPos < html.IndexOf(BriefOut, StringComparison.Ordinal), "PRD leads the band");
        Assert.True(prdPos < html.IndexOf("index-subgroup-label", StringComparison.Ordinal), "PRD precedes the UX pair");

        // (c) UX Design + UX Experience are grouped under one shared "UX" sub-label, adjacent.
        Assert.Contains("<div class=\"index-subgroup-label\">UX</div>", html);
        var design = html.IndexOf("ux-x/DESIGN.html", StringComparison.Ordinal);
        var experience = html.IndexOf("ux-x/EXPERIENCE.html", StringComparison.Ordinal);
        Assert.True(design >= 0 && experience >= 0);
        Assert.True(design < experience, "Design precedes Experience in the pair");
        Assert.DoesNotContain(BriefOut, html[design..experience]); // nothing else wedged between the UX pair

        // (d) the rubric is NOT a standalone card...
        Assert.DoesNotContain($"<a class=\"index-card\" href=\"{RubricOut}\">", html);
        Assert.DoesNotContain("PRD Quality Review", html); // its title never appears as a peer card
        // (e) ...it is reachable as a branch link from the PRD card.
        Assert.Contains($"<a class=\"index-card-branch\" href=\"{RubricOut}\">Quality review", html);
        Assert.Equal(1, CountOccurrences(html, RubricOut)); // exactly one reference: the branch link

        // Story 1.4 a11y floor preserved by reusing the RenderIndex shell.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }

    [Fact]
    public void RenderIndex_PlanningSection_NoPrd_OmitsPrimaryCardAndRubricLinkButStillRenders()
    {
        // Rubric present but no PRD to fold it into → it degrades to an ordinary card (never orphan-linked/dropped),
        // and the section still renders whatever exists with no empty "PRD" slot. [Story 2.4 Task 3/4 graceful]
        var html = RenderPlanning(BriefDoc(), DesignDoc(), ExperienceDoc(), RubricDoc());

        Assert.Contains(">Planning Artifacts</div>", html);
        Assert.DoesNotContain("index-card--primary", html);
        Assert.DoesNotContain("index-card-branch", html);
        // The unfolded rubric is a normal card, not a broken link.
        Assert.Contains($"<a class=\"index-card\" href=\"{RubricOut}\">", html);
        // The UX pair and brief still render.
        Assert.Contains("<div class=\"index-subgroup-label\">UX</div>", html);
        Assert.Contains($"href=\"{BriefOut}\"", html);
    }

    [Fact]
    public void RenderIndex_PlanningSection_PrdWithoutRubric_ShowsNoQualityReviewLink()
    {
        var html = RenderPlanning(PrdDoc(), BriefDoc());

        // PRD is still the prominent primary card, but with no rubric there is no dangling quality-review link.
        Assert.Contains("index-card--primary", html);
        Assert.DoesNotContain("index-card-branch", html);
        Assert.DoesNotContain("Quality review", html);
        // No UX docs → no empty "UX" labeled group.
        Assert.DoesNotContain("index-subgroup-label", html);
    }

    [Fact]
    public void RenderIndex_PlanningSection_UnrecognizedDocsOnly_RenderAsOrdinaryCardsWithoutEmptyGroups()
    {
        var note = Doc("planning-artifacts/misc/note.md", "planning-artifacts/misc/note.html", "A Note", Frontmatter.Empty);
        var html = RenderPlanning(note);

        Assert.Contains(">Planning Artifacts</div>", html);
        Assert.Contains("<a class=\"index-card\" href=\"planning-artifacts/misc/note.html\">", html);
        Assert.DoesNotContain("index-card--primary", html);
        Assert.DoesNotContain("index-subgroup-label", html);
    }

    [Fact]
    public void RenderIndex_PlanningSection_OmittedEntirelyWhenNoPlanningDocs()
    {
        var html = RenderPlanning(Doc("implementation-artifacts/x.md", "implementation-artifacts/x.html", "X", Frontmatter.Empty));

        Assert.DoesNotContain("Planning Artifacts", html);
    }
}
