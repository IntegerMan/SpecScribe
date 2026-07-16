using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.9 coverage for the host-neutral <c>ProjectOutline</c> the <c>specscribe webview</c> payload
/// carries for the VS Code native surfaces (activity-bar tree + status bar): epic/story records mapped 1:1 from
/// the ingested <see cref="EpicsModel"/>, stages from <see cref="StatusStyles"/>, every <c>SurfacePath</c> equal
/// to a real webview surface key, core-computed summary counts, placeholder-story mapping, the retro-gated epic
/// stage, and the camelCase serialized shape the TS shim parses. Data only — no HTML, so the golden fingerprint
/// is unaffected by construction. Follows the temp-dir fixture style of <see cref="SiteGeneratorWebviewTests"/>,
/// seeding a bmm module so per-story helper commands resolve.</summary>
public class SiteGeneratorOutlineTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-outline-").FullName;

    private string Source => Path.Combine(_root, "_bmad-output");
    private string Adrs => Path.Combine(_root, "docs", "adrs");
    private string Site => Path.Combine(_root, "site");

    private const string EpicsMd = """
        # Epics

        ## Requirements Inventory

        ### Functional Requirements

        FR1: The portal renders artifacts

        ### FR Coverage Map

        FR1: Epic 1 - rendering

        ## Epic List

        ### Epic 1: Foundation

        Stand up the portal.

        ### Epic 2: Delivery

        Ship the portal.

        ### Epic 3: Hardening

        Harden the portal.

        ## Epic 1: Foundation

        ### Story 1.1: Foundation Story

        As a maintainer, I want the foundation.

        ### Story 1.2: Undrafted Story

        As a maintainer, I want the follow-up (no artifact yet).

        ### Story 1.3: Review Story

        As a maintainer, I want the review pass.

        ## Epic 2: Delivery

        ### Story 2.1: Delivery Story

        As a maintainer, I want delivery.

        ## Epic 3: Hardening

        ### Story 3.1: Hardening Story One

        As a maintainer, I want hardening one.

        ### Story 3.2: Hardening Story Two

        As a maintainer, I want hardening two.
        """;

    // 1.1 is in-progress → stage "active"; two tasks, one done → 1/2.
    private const string Story11Md = """
        # Story 1.1: Foundation Story

        Status: in-progress

        ## Story

        As a maintainer, I want the foundation.

        ## Acceptance Criteria

        1. It works.

        ## Tasks / Subtasks

        - [x] Task 1: Do it (AC: #1)
        - [ ] Task 2: Polish it
        """;

    private const string Story13Md = """
        # Story 1.3: Review Story

        Status: review

        ## Story

        As a maintainer, I want the review pass.

        ## Acceptance Criteria

        1. It reviews.

        ## Tasks / Subtasks

        - [x] Task 1: Review it (AC: #1)
        """;

    private static string DoneStory(string id, string title) => $"""
        # Story {id}: {title}

        Status: done

        ## Story

        As a maintainer, I want {title}.

        ## Acceptance Criteria

        1. It ships.

        ## Tasks / Subtasks

        - [x] Task 1: Ship it (AC: #1)
        """;

    // Only "core" and "bmm" modules; the csv gives create-story/dev-story/code-review/correct-course so
    // per-story helper commands and the done escape hatch resolve to /bmad-* strings. [Story 8.5]
    private const string BmmCsv = """
        module,skill,display-name,menu-code,description,action,args,phase,preceded-by,followed-by,required,output-location,outputs
        BMad Method,_meta,,,,,,,,,false,url,
        BMad Method,bmad-create-story,Create Story,CS,Prepare the next story,create,,4-implementation,,,true,implementation_artifacts,story
        BMad Method,bmad-dev-story,Dev Story,DS,Execute the story,,,4-implementation,,,true,,
        BMad Method,bmad-code-review,Code Review,CR,Review the changes,,,4-implementation,,,false,,
        BMad Method,bmad-correct-course,Correct Course,CC,Navigate significant changes,,,anytime,,,false,planning_artifacts,change proposal
        """;

    public SiteGeneratorOutlineTests()
    {
        Directory.CreateDirectory(Path.Combine(Source, "planning-artifacts"));
        var impl = Path.Combine(Source, "implementation-artifacts");
        Directory.CreateDirectory(impl);
        Directory.CreateDirectory(Adrs);

        File.WriteAllText(Path.Combine(Source, "planning-artifacts", "epics.md"), EpicsMd);
        File.WriteAllText(Path.Combine(impl, "1-1-foundation.md"), Story11Md);
        // 1.2 deliberately has NO artifact → placeholder story.
        File.WriteAllText(Path.Combine(impl, "1-3-review.md"), Story13Md);
        File.WriteAllText(Path.Combine(impl, "2-1-delivery.md"), DoneStory("2.1", "Delivery Story"));
        File.WriteAllText(Path.Combine(impl, "3-1-hardening-one.md"), DoneStory("3.1", "Hardening Story One"));
        File.WriteAllText(Path.Combine(impl, "3-2-hardening-two.md"), DoneStory("3.2", "Hardening Story Two"));
        // Epic 2's stories are all done AND it has a retrospective → retro-gated stage "done".
        // Epic 3's stories are all done but there is NO retro → retro-gated stage "review".
        File.WriteAllText(Path.Combine(impl, "epic-2-retro-2026-01-01.md"),
            "# Epic 2 Retrospective\n\n**Date:** 2026-01-01\n\nWent well.\n");

        // Seed a bmm module so BmadCommands.PrimaryStoryCommand resolves to /bmad-* strings.
        var configDir = Path.Combine(_root, "_bmad", "_config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "manifest.yaml"),
            "modules:\n  - name: core\n    version: 6.0.0\n  - name: bmm\n    version: 6.0.0");
        var bmmDir = Path.Combine(_root, "_bmad", "bmm");
        Directory.CreateDirectory(bmmDir);
        File.WriteAllText(Path.Combine(bmmDir, "module-help.csv"), BmmCsv);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ForgeOptions Options() => ForgeOptions.Resolve(
        source: Source, adrs: Adrs, output: Site, projectName: "SpecScribe", includeReadme: false);

    private ProjectOutline Outline()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        return gen.RenderWebviewSurfaces().Outline;
    }

    private OutlineStory Story(string id) =>
        Outline().Epics.SelectMany(e => e.Stories).Single(s => s.Id == id);

    [Fact]
    public void Outline_MapsEveryEpicAndStory_OneToOne_InCoreOrder()
    {
        var outline = Outline();

        Assert.Equal(new[] { 1, 2, 3 }, outline.Epics.Select(e => e.Number).ToArray());
        Assert.Equal(new[] { "1.1", "1.2", "1.3" }, outline.Epics[0].Stories.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { "2.1" }, outline.Epics[1].Stories.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { "3.1", "3.2" }, outline.Epics[2].Stories.Select(s => s.Id).ToArray());
        Assert.Equal("Foundation", outline.Epics[0].Title);
    }

    [Fact]
    public void StoryStages_MatchStatusStyles()
    {
        Assert.Equal("active", Story("1.1").Stage);   // in-progress
        Assert.Equal("drafted", Story("1.2").Stage);  // placeholder (no status)
        Assert.Equal("review", Story("1.3").Stage);
        Assert.Equal("done", Story("2.1").Stage);
    }

    [Fact]
    public void EpicStages_UseRetroGatedClassifier()
    {
        var outline = Outline();

        Assert.Equal("active", outline.Epics[0].Stage);          // has an active story
        Assert.Equal("done", outline.Epics[1].Stage);            // all done AND a retro exists
        Assert.Equal("review", outline.Epics[2].Stage);          // all done but NO retro → retro-gated review
    }

    [Fact]
    public void EverySurfacePath_MatchesARealSurfaceKey()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var bundle = gen.RenderWebviewSurfaces();
        var keys = bundle.Surfaces.Select(s => (string?)s.OutputRelativePath).ToHashSet();

        foreach (var epic in bundle.Outline.Epics)
        {
            Assert.Contains(epic.SurfacePath, keys);
            foreach (var story in epic.Stories)
            {
                // Placeholder stories are surfaces too — every story node is clickable.
                Assert.Contains(story.SurfacePath, keys);
            }
        }
    }

    [Fact]
    public void PlaceholderStory_IsClickable_ButHasNoSourceOrCounts()
    {
        var s12 = Story("1.2");

        Assert.Equal("epics/story-1-2.html", s12.SurfacePath);   // still reveals a surface
        Assert.Null(s12.SourcePath);                             // no artifact → "Open Source" omitted host-side
        Assert.Equal(0, s12.TasksTotal);
        Assert.Equal(0, s12.TasksDone);
    }

    [Fact]
    public void StoryCounts_AndSourcePath_ComeFromTheArtifact()
    {
        var s11 = Story("1.1");

        Assert.Equal(2, s11.TasksTotal);
        Assert.Equal(1, s11.TasksDone);
        // Repo-relative (Story 6.10 harmonization): the tree "Open Source" joins this to the workspace folder with
        // the SAME one convention the webview reveal + configuredOutputRoot use — no `_bmad-output` literal host-side.
        Assert.Equal("_bmad-output/implementation-artifacts/1-1-foundation.md", s11.SourcePath);
    }

    [Fact]
    public void EpicStoryCounts_ReflectDoneRollup()
    {
        var outline = Outline();

        Assert.Equal(3, outline.Epics[0].StoriesTotal);
        Assert.Equal(0, outline.Epics[0].StoriesDone);           // 1.1 active, 1.2 drafted, 1.3 review
        Assert.Equal(1, outline.Epics[1].StoriesDone);           // 2.1 done
        Assert.Equal(2, outline.Epics[2].StoriesDone);           // 3.1, 3.2 done
    }

    [Fact]
    public void Summary_CountsStoriesByStage_CoreSide()
    {
        var s = Outline().Summary;

        Assert.Equal(1, s.Active);   // 1.1
        Assert.Equal(1, s.Review);   // 1.3
        Assert.Equal(3, s.Done);     // 2.1, 3.1, 3.2
        Assert.Equal(6, s.Total);    // 1.1, 1.2, 1.3, 2.1, 3.1, 3.2
    }

    [Fact]
    public void HelperCommand_IsTheMostActionablePerStatus_OrNullWhenDone()
    {
        Assert.Equal("/bmad-dev-story 1.1", Story("1.1").HelperCommand);      // active → resume dev
        Assert.Equal("/bmad-create-story 1.2", Story("1.2").HelperCommand);   // undrafted → draft it
        Assert.Equal("/bmad-code-review 1.3", Story("1.3").HelperCommand);    // review → review it
        Assert.Null(Story("2.1").HelperCommand);                              // done → no next action
    }

    [Fact]
    public void HelperCommand_ReadyStage_ResolvesToDevStory()
    {
        // Isolated from the shared fixture (which has no "ready" story) so it doesn't disturb the epic/story
        // indices every other test in this file asserts against. Mirrors BmadCommands.PrimaryStoryCommand's own
        // doc comment ("dev-story when ready/active") — the "ready" branch was previously untested here.
        var story = new StoryInfo
        {
            Id = "9.9",
            EpicNumber = 9,
            Title = "Ready Story",
            UserStoryHtml = "",
            AcBlocksHtml = Array.Empty<string>(),
            Status = "ready-for-dev",
        };
        var commands = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
        });

        Assert.Equal("ready", StatusStyles.ForStory(story));
        Assert.Equal("/bmad-dev-story 9.9", BmadCommands.PrimaryStoryCommand(story, commands));
    }

    [Fact]
    public void EpicStage_Pending_WhenEpicHasNoStories()
    {
        // Isolated for the same reason as HelperCommand_ReadyStage_ResolvesToDevStory above — the shared fixture's
        // three epics never hit "pending" (StatusStyles.EpicStages' 6th, epic-only tier), which ForEpicWithRetrospective
        // and the tree's stage icon/color map both need to handle.
        var epic = new EpicInfo
        {
            Number = 9,
            Title = "Empty Epic",
            GoalHtml = "",
            Status = EpicStatus.Drafted,
            Section = EpicSection.FurtherDevelopment,
            Stories = Array.Empty<StoryInfo>(),
        };

        Assert.Equal("pending", StatusStyles.ForEpicWithRetrospective(epic));
    }

    [Fact]
    public void SerializePayload_EmitsOutline_AllCamelCase()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var bundle = gen.RenderWebviewSurfaces();

        var json = WebviewCommand.SerializePayload(bundle, "SpecScribeOutput");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("outline", out var outline), "payload carries an `outline` field");
        Assert.True(outline.TryGetProperty("epics", out var epics));
        Assert.True(outline.TryGetProperty("summary", out var summary));
        Assert.True(summary.TryGetProperty("active", out _));

        var story = epics[0].GetProperty("stories")[0];
        // camelCase keys the TS WebviewPayload.outline interface depends on.
        foreach (var key in new[] { "id", "title", "stage", "surfacePath", "sourcePath", "tasksDone", "tasksTotal", "helperCommand" })
        {
            Assert.True(story.TryGetProperty(key, out _), $"story node carries camelCase `{key}`");
        }

        // The surfaces dictionary keys stay verbatim output-relative paths (NOT camelCased by the naming policy).
        Assert.True(root.GetProperty("surfaces").TryGetProperty("index.html", out _), "surface keys are untouched paths");
    }

    // ===== spec-vscode-sidebar-shortcuts-and-story-command-quickpick: the full status-gated command list =====

    [Fact]
    public void StoryCommands_MirrorThePageNextStepsSet_StatusGated()
    {
        // ONE generation for all four assertions (Story(id) would re-run GenerateAll per call). The tree's
        // Quick Pick option set IS the story page's Next Steps set (one BmadCommands source): in-progress →
        // dev-story + code-review + correct-course; review → code-review + correct-course; done → muted
        // correct-course hatch only (HelperCommand stays null); undrafted → create-story. [Story 8.5]
        var stories = Outline().Epics.SelectMany(e => e.Stories).ToDictionary(s => s.Id);

        Assert.Equal(new[] { "/bmad-dev-story 1.1", "/bmad-code-review 1.1", "/bmad-correct-course" },
            stories["1.1"].Commands.Select(c => c.Command).ToArray());
        Assert.Equal(new[] { "/bmad-code-review 1.3", "/bmad-correct-course" },
            stories["1.3"].Commands.Select(c => c.Command).ToArray());
        Assert.Equal(new[] { "/bmad-correct-course" },
            stories["2.1"].Commands.Select(c => c.Command).ToArray());
        Assert.Null(stories["2.1"].HelperCommand); // hatch is not a primary
        Assert.Equal(new[] { "/bmad-create-story 1.2" },
            stories["1.2"].Commands.Select(c => c.Command).ToArray());

        // Every entry carries the page's description (the Quick Pick's detail line), and the legacy single
        // helperCommand is exactly the first entry — one source, no drift.
        var s11 = stories["1.1"];
        Assert.All(s11.Commands, c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
        Assert.Equal(s11.Commands[0].Command, s11.HelperCommand);
    }

    [Fact]
    public void StoryCommands_ParityWithRenderedNextStepsPanel_AcrossStatuses()
    {
        // The load-bearing "mirrors the page" claim, asserted against the ACTUAL RenderNextSteps output rather
        // than by construction: for every status the panel offers exactly the commands StoryCommands emits
        // (badge count == list count, each command present). Done stories keep PrimaryStoryCommand null while
        // the list may carry the muted correct-course hatch. Undrafted X.1: create-story primary + readiness
        // alternate. [Story 8.5]
        var commands = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
            ["code-review"] = "/bmad-code-review",
            ["create-story"] = "/bmad-create-story",
            ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
            ["correct-course"] = "/bmad-correct-course",
        });

        foreach (var status in new[] { "ready-for-dev", "in-progress", "in-review", "done", "" })
        {
            var story = new StoryInfo
            {
                Id = "9.1", // X.1 so the blank-status branch exercises create-story primary + readiness alternate
                EpicNumber = 9,
                Title = "Parity Story",
                UserStoryHtml = "",
                AcBlocksHtml = Array.Empty<string>(),
                Status = status,
            };
            var list = BmadCommands.StoryCommands(story, commands);
            var html = BmadCommands.RenderNextSteps(story, commands);
            var primary = BmadCommands.PrimaryStoryCommand(story, commands);

            if (StatusStyles.ForStory(story) == "done")
            {
                Assert.Null(primary);
                Assert.Equal(new[] { "/bmad-correct-course" }, list.Select(c => c.Command).ToArray());
                Assert.Contains("all-done", html);
                Assert.Equal(list.Count, html.Split("cmd-copy").Length - 1);
                foreach (var c in list)
                {
                    Assert.Contains(PathUtil.Html(c.Command), html);
                }
                continue;
            }

            Assert.Equal(list.FirstOrDefault()?.Command, primary);
            Assert.NotEmpty(list);
            Assert.Equal(list.Count, html.Split("cmd-copy").Length - 1); // one copy badge per emitted command
            foreach (var c in list)
            {
                Assert.Contains(PathUtil.Html(c.Command), html);
            }
        }
    }

    [Fact]
    public void StoryCommands_ReadyStage_GetsNoCodeReview_EvenWhenTheModuleExposesIt()
    {
        // A ready story has no changes yet — the page never offers code-review, so neither may the tree.
        // The catalog deliberately exposes code-review to prove the gate is status-driven, not catalog-driven.
        var story = new StoryInfo
        {
            Id = "9.9",
            EpicNumber = 9,
            Title = "Ready Story",
            UserStoryHtml = "",
            AcBlocksHtml = Array.Empty<string>(),
            Status = "ready-for-dev",
        };
        var commands = new CommandCatalog("BMad Method", new Dictionary<string, string>
        {
            ["dev-story"] = "/bmad-dev-story",
            ["code-review"] = "/bmad-code-review",
        });

        Assert.Equal(new[] { "/bmad-dev-story 9.9" },
            BmadCommands.StoryCommands(story, commands).Select(c => c.Command).ToArray());
    }

    [Fact]
    public void SerializePayload_EmitsStoryCommands_CamelCase()
    {
        var gen = new SiteGenerator(Options());
        Assert.DoesNotContain(gen.GenerateAll(), e => e.Outcome == GenerationOutcome.Error);
        var json = WebviewCommand.SerializePayload(gen.RenderWebviewSurfaces(), "SpecScribeOutput");
        using var doc = JsonDocument.Parse(json);

        // Story 1.1 (in-progress) — the wire shape the TS OutlineStoryCommand interface depends on:
        // `commands` is an array of { command, description } and its first command IS helperCommand.
        var story = doc.RootElement.GetProperty("outline").GetProperty("epics")[0].GetProperty("stories")[0];
        Assert.True(story.TryGetProperty("commands", out var cmds), "story node carries camelCase `commands`");
        Assert.Equal(JsonValueKind.Array, cmds.ValueKind);
        Assert.True(cmds.GetArrayLength() >= 1, "an in-progress story has at least one command");
        Assert.True(cmds[0].TryGetProperty("command", out var first), "entry carries camelCase `command`");
        Assert.True(cmds[0].TryGetProperty("description", out _), "entry carries camelCase `description`");
        Assert.Equal(story.GetProperty("helperCommand").GetString(), first.GetString());
    }
}
