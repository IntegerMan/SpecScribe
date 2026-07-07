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

    [Fact]
    public void RenderNextSteps_UsesDetectedModuleCommands()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "ready-for-dev"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
        Assert.Contains("Next Steps", html);
        Assert.DoesNotContain("(BMad Method)", html);
        Assert.DoesNotContain("/gds-", html);
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
        });

        var html = BmadCommands.RenderNextSteps(Story("2.1", "review"), catalog);

        Assert.Contains("/bmad-code-review 2.1", html);
        Assert.DoesNotContain("/bmad-create-story", html);
        Assert.DoesNotContain("/bmad-retrospective", html);
    }

    [Fact]
    public void RenderNextSteps_InProgressStory_CarriesStoryIdOnCodeReview()
    {
        var html = BmadCommands.RenderNextSteps(Story("1.2", "in-progress"), BmmCatalog);

        Assert.Contains("/bmad-dev-story 1.2", html);
        Assert.Contains("/bmad-code-review 1.2", html);
    }

    [Fact]
    public void RenderNextSteps_UnplannedStory_StillSuggestsDraftingItsOwnPlan()
    {
        // The one create-story a story page keeps: drafting the story being viewed, with its own id.
        var html = BmadCommands.RenderNextSteps(Story("3.2", null), BmmCatalog);

        Assert.Contains("/bmad-create-story 3.2", html);
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
        // The done story is not the front line and produces no dev/review prompt of its own here.
        Assert.DoesNotContain("Story 1.3", html);
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
