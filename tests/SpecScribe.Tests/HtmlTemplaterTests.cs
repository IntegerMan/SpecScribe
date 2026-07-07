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
            // templater tests exercise the real linked-cells + panels path.
            Git = new GitPulse(totalCommits, 1, day, day, new (DateOnly, int)[] { (day, totalCommits) },
                new Dictionary<DateOnly, IReadOnlyList<CommitInfo>>
                {
                    [day] = Enumerable.Range(1, totalCommits)
                        .Select(i => new CommitInfo($"c{i:000}", $"Change {i}"))
                        .ToList(),
                }),
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
    public void RenderIndex_WiresHeatmapDrilldownThroughDashboard()
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

        // The call-site wiring (CommitsByDay → CommitHeatmap) surfaces linked cells and panels on the page.
        Assert.Contains("<a href=\"#heat-day-2026-01-05\"", html);
        Assert.Contains("id=\"heat-day-2026-01-05\"", html);
        Assert.Contains("<code>c001</code> Change 1", html);
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
    public void RenderIndex_SurfacesNowAndNextAheadOfExploreKeyViews()
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
        var nowNext = html.IndexOf("Now &amp; Next", StringComparison.Ordinal);
        var explore = html.IndexOf("Explore Key Views", StringComparison.Ordinal);
        Assert.True(nowNext >= 0 && explore >= 0, "both panels should render");
        Assert.True(nowNext < explore, "Now & Next must appear before Explore Key Views");
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

        // Dedicated first-class section with a status badge (not flat text) for the quick-dev entry.
        Assert.Contains("Direct &amp; Quick-Dev Work", html);
        Assert.Contains("<span class=\"status-badge done\">done</span>", html);
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

        // Labeled "Spec Kernel" band (AC #1), not the generic "Other" bucket.
        Assert.Contains("<div class=\"index-section-title\">Spec Kernel</div>", html);
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
        Assert.Contains("class=\"copy-btn\" data-copy=\"/bmad-dev-story 1.1\"", html);
        Assert.Contains("<svg class=\"icon\"", html); // the badge Copy button is an icon, not the word "Copy"
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
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, BmadCatalog());

        Assert.Contains("No detailed story plan yet — draft it with", html);
        Assert.Contains("/bmad-create-story 1.1", html);
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
        };

        var html = EpicsTemplater.RenderEpic(epic, progress, nav, CommandCatalog.Empty);

        // The heavily-refactored detail templater must carry the same skip link + exactly one main landmark
        // as the dashboard — guards against a duplicate/zero-landmark regression in RenderEpic. [Story 1.4 AC #1, UX-DR16]
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Equal(1, CountOccurrences(html, "id=\"main-content\""));
    }
}
