using SpecScribe;

namespace SpecScribe.Tests;

public class CommandCatalogTests
{
    [Fact]
    public void Command_ReturnsSlashCommandForKnownStep()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });

        Assert.Equal("/bmad-create-story", catalog.Command("create-story"));
        Assert.Equal("/bmad-create-story 1.2", catalog.Command("create-story", "1.2"));
    }

    [Fact]
    public void Command_ReturnsNullForUnknownStep()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>());

        Assert.Null(catalog.Command("dev-story"));
    }
}

public class ModuleContextTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("specscribe-module-").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void WriteModule(string moduleName, string csv, params string[] installedModules)
    {
        var configDir = Path.Combine(_repo, "_bmad", "_config");
        Directory.CreateDirectory(configDir);
        var manifest = "modules:\n" + string.Join("\n", installedModules.Select(m => $"  - name: {m}\n    version: 6.0.0"));
        File.WriteAllText(Path.Combine(configDir, "manifest.yaml"), manifest);

        var moduleDir = Path.Combine(_repo, "_bmad", moduleName);
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module-help.csv"), csv);
    }

    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,url,
        BMad Method,bmad-create-story,Create Story,CS,"Prepare the next story, with commas, quoted",create,,4-implementation,,,true,implementation_artifacts,story
        BMad Method,bmad-dev-story,Dev Story,DS,Execute the story,,,4-implementation,,,true,,
        BMad Method,bmad-code-review,Code Review,CR,Review the changes,,,4-implementation,,,false,,
        """;

    private const string GdsCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        Game Dev Studio,_meta,,,,,,,,,false,url,
        Game Dev Studio,gds-create-story,Create Story,CS,Prepare the next story,create,,4-implementation,,,true,implementation_artifacts,story
        Game Dev Studio,gds-dev-story,Dev Story,DS,Execute the story,,,4-implementation,,,true,,
        """;

    [Fact]
    public void Detect_ReadsBmadMethodModuleAndCommands()
    {
        WriteModule("bmm", BmmCsv, "core", "bmm");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("BMad Method", ctx.Commands.ModuleLabel);
        Assert.Equal("/bmad-create-story", ctx.Commands.Command("create-story"));
        Assert.Equal("/bmad-dev-story 1.2", ctx.Commands.Command("dev-story", "1.2"));
        Assert.Contains(ctx.Docs, d => d.FileName == "prd.md");
    }

    [Fact]
    public void Detect_ReadsGameDevStudioModuleAndCommands()
    {
        WriteModule("gds", GdsCsv, "core", "gds");

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.GameDevStudio, ctx.Module);
        Assert.Equal("/gds-dev-story", ctx.Commands.Command("dev-story"));
        Assert.Contains(ctx.Docs, d => d.FileName == "gdd.md");
    }

    [Fact]
    public void Detect_ReturnsNoneWhenNoBmadFolder()
    {
        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.Unknown, ctx.Module);
        Assert.True(ctx.Commands.IsEmpty);
        Assert.Empty(ctx.Docs);
    }

    [Fact]
    public void Detect_FallsBackToOnDiskCsvWhenManifestMissing()
    {
        // No manifest.yaml written — detection should still find the module-help.csv on disk.
        var moduleDir = Path.Combine(_repo, "_bmad", "bmm");
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, "module-help.csv"), BmmCsv);

        var ctx = ModuleContext.Detect(_repo, Array.Empty<string>());

        Assert.Equal(BmadModule.BmadMethod, ctx.Module);
        Assert.Equal("/bmad-code-review", ctx.Commands.Command("code-review"));
    }
}

public class BmadCommandsTests
{
    private static readonly CommandCatalog BmmCatalog = new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["correct-course"] = "/bmad-correct-course",
        ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
    });

    private static readonly CommandCatalog BmmWithoutCorrectCourse = new("BMad Method", new Dictionary<string, string>
    {
        ["create-story"] = "/bmad-create-story",
        ["dev-story"] = "/bmad-dev-story",
        ["code-review"] = "/bmad-code-review",
        ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
    });

    private static StoryInfo Story(string id, string? status) => new()
    {
        Id = id,
        EpicNumber = int.Parse(id.Split('.')[0]),
        Title = "A story",
        UserStoryHtml = "",
        AcBlocksHtml = Array.Empty<string>(),
        Status = status,
    };

    private static int CountClass(string html, string cssClass) =>
        html.Split($"class=\"{cssClass}\"", StringSplitOptions.None).Length - 1;

    [Fact]
    public void RenderNextSteps_UsesDetectedModuleCommands()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "ready-for-dev"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        // Nothing has been implemented yet for a ready-for-dev story, so code review isn't a valid next step.
        Assert.DoesNotContain("/bmad-code-review 1.2", html);
        Assert.Contains("Next Steps", html);
        Assert.DoesNotContain("(BMad Method)", html);
        Assert.DoesNotContain("/gds-", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.DoesNotContain("Other actions", html);
    }

    [Fact]
    public void RenderNextSteps_OmitsPanelWhenModuleUndetected()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "ready-for-dev"), CommandCatalog.Empty);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void RenderNextSteps_ReviewStory_SuggestsOnlyCodeReviewWithStoryId()
    {
        // Story pages never suggest drafting other stories or retrospectives — those are epic/project
        // moves. The catalog includes both commands to prove they're withheld, not merely uninstalled.
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["code-review"] = "/bmad-code-review",
            ["retrospective"] = "/bmad-retrospective",
            ["correct-course"] = "/bmad-correct-course",
        });

        var html = BmadCommands.RenderNextSteps(Story("2.1", "review"), catalog);

        Assert.Contains("/bmad-code-review 2.1", html);
        Assert.DoesNotContain("/bmad-create-story", html);
        Assert.DoesNotContain("/bmad-retrospective", html);
        Assert.Contains("/bmad-correct-course", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.Contains("Other actions", html);
        Assert.True(html.IndexOf("next-steps-primary", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 2.1", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Other actions", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-correct-course", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderNextSteps_InProgressStory_CarriesStoryIdOnCodeReview()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.Contains("/bmad-correct-course", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.Contains("next-steps-alt", html);
        Assert.True(html.IndexOf("/bmad-dev-story 1.2", StringComparison.Ordinal)
                    < html.IndexOf("Other actions", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Other actions", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal));
        Assert.True(html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-correct-course", StringComparison.Ordinal));
        Assert.Contains("next-steps-desc", html);
    }

    [Fact]
    public void RenderNextSteps_InProgress_PromotesAlternateWhenPrimaryMissing()
    {
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["code-review"] = "/bmad-code-review",
            ["correct-course"] = "/bmad-correct-course",
        });

        var html = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), catalog);

        Assert.DoesNotContain("/bmad-dev-story", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.True(html.IndexOf("next-steps-primary", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-code-review 1.2", StringComparison.Ordinal));
        Assert.Contains("Other actions", html);
        Assert.Contains("/bmad-correct-course", html);
    }

    [Fact]
    public void RenderNextSteps_UnplannedStory_StillSuggestsDraftingItsOwnPlan()
    {
        // The one create-story a story page keeps: drafting the story being viewed, with its own id.
        var html = BmadCommands.RenderNextSteps(Story("3.2", null), BmmCatalog);

        Assert.Contains("/bmad-create-story 3.2", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.DoesNotContain("check-implementation-readiness", html);
    }

    [Fact]
    public void RenderNextSteps_UnplannedFirstStory_CreateStoryIsPrimary_ReadinessIsAlternate()
    {
        var html = BmadCommands.RenderNextSteps(Story("3.1", null), BmmCatalog);

        Assert.Contains("/bmad-create-story 3.1", html);
        Assert.Contains("/bmad-check-implementation-readiness", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.True(html.IndexOf("/bmad-create-story 3.1", StringComparison.Ordinal)
                    < html.IndexOf("Other actions", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Other actions", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-check-implementation-readiness", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderNextSteps_DoneStory_ShowsCelebratoryAllDonePanelNotCodeReview()
    {
        // Pure celebration when correct-course is absent — byte-identical to the pre-8.5 celebratory panel.
        var without = BmadCommands.RenderNextSteps(Story("2.1", "done"), BmmWithoutCorrectCourse);

        Assert.Contains("next-steps all-done", without);
        Assert.Contains("All done", without);
        Assert.Contains("ss-icon", without);
        Assert.DoesNotContain("/bmad-code-review", without);
        Assert.DoesNotContain("Other actions", without);
        Assert.DoesNotContain("next-steps-primary", without);

        // With correct-course: celebration + one muted escape hatch, never a primary / never code-review.
        var with = BmadCommands.RenderNextSteps(Story("2.1", "done"), BmmCatalog);

        Assert.Contains("next-steps all-done", with);
        Assert.Contains("All done", with);
        Assert.Contains("Other actions", with);
        Assert.Contains("/bmad-correct-course", with);
        Assert.Contains("Re-open this story if it needs rework.", with);
        Assert.DoesNotContain("next-steps-primary", with);
        Assert.DoesNotContain("/bmad-code-review", with);
        Assert.Contains("next-steps-desc", with);
    }

    [Fact]
    public void RenderNextSteps_CorrectCourseDropsWhenModuleLacksIt()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmWithoutCorrectCourse);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.DoesNotContain("correct-course", html);
        Assert.Contains("Other actions", html); // code-review still demoted
    }

    [Fact]
    public void RenderNextSteps_IsDeterministic()
    {
        var a = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);
        var b = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);
        Assert.Equal(a, b);
    }

    private static EpicInfo Epic(bool hasRetro, params StoryInfo[] stories) => new()
    {
        Number = 1,
        Title = "First Epic",
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = stories,
        HasRetrospective = hasRetro,
    };

    [Fact]
    public void RenderEpicNextSteps_AllStoriesDoneNoRetro_SuggestsRetrospective()
    {
        // The retro-gated "review" state (every story done, no retro yet) is exactly when to nudge a retro.
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
        });

        var html = BmadCommands.RenderEpicNextSteps(Epic(hasRetro: false, Story("1.1", "done"), Story("1.2", "done")), catalog);

        Assert.Contains("/bmad-retrospective 1", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.DoesNotContain("Other actions", html);
    }

    [Fact]
    public void RenderEpicNextSteps_AllStoriesDoneWithRetro_SuggestsNothing()
    {
        // Once the retro exists the epic is "done" — nothing more to suggest, so the panel is omitted entirely
        // (no re-nagging to run a retrospective it already has). [spec-sunburst-retro]
        var catalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
        });

        var html = BmadCommands.RenderEpicNextSteps(Epic(hasRetro: true, Story("1.1", "done"), Story("1.2", "done")), catalog);

        Assert.Equal(string.Empty, html);
    }

    private static EpicsModel Project(params StoryInfo[] stories) => new()
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
                Status = EpicStatus.Drafted,
                Section = EpicSection.VerticalSlice,
                Stories = stories,
            },
        },
    };

    [Fact]
    public void RenderProjectNextSteps_ListsCodeReviewForStoryAwaitingReview()
    {
        var html = BmadCommands.RenderProjectNextSteps(
            Project(Story("1.3", "done"), Story("1.4", "review")), BmmCatalog);

        // A lone review story passes its id straight to the command.
        Assert.Contains("/bmad-code-review 1.4", html);
        Assert.Contains("Story 1.4 is awaiting code review", html);
        // The done story is not the front line — it gets no dev-story or code-review prompt of its own here.
        // (It may still be named as the next story to draft, since this fixture leaves its plan path unset.)
        Assert.DoesNotContain("dev-story 1.3", html);
        Assert.DoesNotContain("code-review 1.3", html);
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
    }

    [Fact]
    public void RenderProjectNextSteps_GroupsReviewStoriesIntoOneNamedPrompt_BeforeTheFrontLine()
    {
        // Two stories awaiting review plus a ready front-line story: a single code-review prompt lists both
        // ids (grouped by action, not one row per story), and it precedes the dev-story front line.
        var html = BmadCommands.RenderProjectNextSteps(
            Project(Story("1.4", "review"), Story("2.1", "review"), Story("1.5", "ready-for-dev")), BmmCatalog);

        Assert.Contains("Stories 1.4, 2.1 are awaiting code review", html);
        Assert.Contains("/bmad-dev-story 1.5", html);
        // Exactly one code-review row, not one per review story. Count the rendered command (each row's
        // badge carries the command in a <code class="cmd-text"> and a data-copy for its copy button). [Story 1.5 F2]
        Assert.Equal(1, html.Split("<code class=\"cmd-text\">/bmad-code-review").Length - 1);
        // Multiple review stories keep the bare command — no single id is appended.
        Assert.DoesNotContain("/bmad-code-review 1.4", html);
        Assert.True(html.IndexOf("awaiting code review", StringComparison.Ordinal)
                    < html.IndexOf("/bmad-dev-story", StringComparison.Ordinal),
            "review prompt should render before the front-line dev-story prompt");
        Assert.Equal(1, CountClass(html, "next-steps-primary"));
        Assert.Contains("Other actions", html);
    }

    [Fact]
    public void RenderProjectNextSteps_OmitsCodeReviewWhenNoStoryInReview()
    {
        var html = BmadCommands.RenderProjectNextSteps(
            Project(Story("1.4", "ready-for-dev")), BmmCatalog);

        Assert.DoesNotContain("code-review", html);
        Assert.DoesNotContain("awaiting code review", html);
    }

    [Fact]
    public void RenderProjectNextSteps_OmitsCodeReviewWhenModuleLacksCommand()
    {
        var noReviewCatalog = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
        });

        var html = BmadCommands.RenderProjectNextSteps(Project(Story("1.4", "review")), noReviewCatalog);

        Assert.DoesNotContain("code-review", html);
        Assert.DoesNotContain("awaiting code review", html);
    }
}
