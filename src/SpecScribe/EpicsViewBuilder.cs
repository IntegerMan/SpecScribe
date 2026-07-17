using System.Text;

namespace SpecScribe;

/// <summary>Builds the host-neutral epics-family section view models (<see cref="EpicsIndexView"/>,
/// <see cref="EpicPageView"/>, <see cref="StoryPageView"/>, <see cref="StoryPlaceholderView"/>) from the
/// already-projected domain models — the rendering-core half of Story 6.2's epics decomposition. Data-shaped
/// sections become records (chips, story cards, progress bars); the command-catalog-driven guidance panels (epic
/// up-next / next-steps, the retro affordance, the create-story notes) are PRE-RENDERED here as named opaque
/// fragments (the sanctioned "modeling the input is disproportionate" carry) so the adapter emits them verbatim.
/// The <see cref="HtmlRenderAdapter"/> maps the rest to bytes with the same <c>Charts.*</c>/<c>Mermaid.*</c>
/// helpers, so the produced bytes are unchanged. [Story 6.2]</summary>
public static class EpicsViewBuilder
{
    /// <summary>The relative prefix every epics-family page uses: they all live at depth 1 (<c>epics/…</c>).</summary>
    private static string Prefix(string outputPath) => PathUtil.RelativePrefix(outputPath);

    // ----- Epics index --------------------------------------------------------------------------------------

    public static EpicsIndexView BuildIndex(EpicsModel model, ProgressModel progress, SiteNav nav, CommandCatalog commands, ProjectCounts? counts = null, FollowUpGeometry? followUps = null)
    {
        // Production always passes the shared ledger. Null → build an equivalent ephemeral ledger from the
        // same inputs so tests/stubs that omit counts keep coherent subtitle + panel stats. [Story 8.3]
        var ledger = counts ?? ProjectCounts.Build(progress, null, WorkInventory.Empty, model);
        return new()
        {
            SiteTitle = nav.SiteTitle,
            EpicCount = ledger.EpicsDefined,
            DraftedCount = ledger.EpicsDrafted,
            Progress = progress,
            Counts = ledger,
            Epics = model,
            Commands = commands,
            VerticalSliceChips = model.Epics.Where(e => e.Section == EpicSection.VerticalSlice).Select(BuildChip).ToList(),
            FurtherDevelopmentChips = model.Epics.Where(e => e.Section == EpicSection.FurtherDevelopment).Select(BuildChip).ToList(),
            FollowUps = followUps ?? FollowUpGeometry.Empty,
        };
    }

    private static EpicChip BuildChip(EpicInfo epic) =>
        new(epic.Number, epic.Title, StatusStyles.ForEpicWithRetrospective(epic), $"epics/epic-{epic.Number}.html");

    // ----- Epic page ----------------------------------------------------------------------------------------

    public static EpicPageView BuildEpic(EpicInfo epic, EpicProgress progress, CommandCatalog commands, string? epicRetroPath, EntityPager? pager = null, FollowUpGeometry? followUps = null)
    {
        var outputPath = $"epics/epic-{epic.Number}.html";
        var prefix = Prefix(outputPath);
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);

        var bars = new List<ProgressBarView>
        {
            new("Stories detailed", progress.StoriesWithArtifact, progress.StoryCount),
        };
        if (progress.TasksTotal > 0)
        {
            bars.Add(new ProgressBarView("Tasks", progress.TasksDone, progress.TasksTotal));
        }

        // Consolidate repeated "no plan yet" CLI hints into one banner when 2+ stories lack an artifact —
        // a lone undrafted story keeps today's per-card create-story note (not clutter). [Story 8.6]
        var undrafted = epic.Stories.Where(s => s.ArtifactOutputPath is null).ToList();
        var consolidated = undrafted.Count >= 2;

        // Epic pages live under epics/ — rewrite follow-up hrefs with the relative prefix. Filter to this
        // epic so zero open follow-ups here omits the ring even when the project has others. [Story 9.7]
        var projectFollowUps = followUps ?? FollowUpGeometry.Empty;
        var scopedActions = projectFollowUps.ActionItems
            .Where(a => a.EpicNumber == epic.Number)
            .ToList();
        var scopedDeferred = projectFollowUps.DeferredItems
            .Where(s => s.EpicNumber == epic.Number)
            .ToList();
        var epicFollowUps = scopedActions.Count > 0 || scopedDeferred.Count > 0
            ? new FollowUpGeometry(
                scopedActions,
                DeferredOpenCount: 0,
                DeferredHref: null,
                ActionItemsHref: prefix + SiteNav.ActionItemsOutputPath,
                ActionDetailSlugs: projectFollowUps.ActionDetailSlugs
                    ?? FollowUpSlug.AssignActionSlugs(projectFollowUps.ActionItems),
                DeferredSlots: scopedDeferred)
            : FollowUpGeometry.Empty;

        return new EpicPageView
        {
            Number = epic.Number,
            TitleHtml = epic.Title,
            StatusClass = epicClass,
            StatusLabel = StatusStyles.EpicLabel(epicClass),
            GoalHtml = epic.GoalHtml,
            FrMetaHtml = epic.FrMetaHtml,
            HasStories = progress.StoryCount > 0,
            ProgressBars = bars,
            NextActionsPanelHtml = RenderNextActionsPanel(epic, prefix, commands),
            NextStepsHtml = BmadCommands.RenderEpicNextSteps(epic, commands),
            RetroAffordanceHtml = RenderRetroAffordance(epic, epicClass, prefix, commands, epicRetroPath),
            UndraftedBannerHtml = consolidated ? RenderUndraftedBanner(epic, undrafted, commands) : string.Empty,
            Epic = epic,
            Commands = commands,
            Prefix = prefix,
            StoryCards = epic.Stories.Select(s => BuildStoryCard(s, prefix, commands, consolidated)).ToList(),
            Pager = pager ?? EntityPager.None,
            FollowUps = epicFollowUps,
        };
    }

    private static StoryCardView BuildStoryCard(StoryInfo story, string prefix, CommandCatalog commands, bool consolidated)
    {
        var hasArtifact = story.ArtifactOutputPath is not null;
        var titleTarget = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);

        string? noteHtml = null;
        if (!hasArtifact)
        {
            // Consolidated path: the banner carries the single create-story affordance; cards keep a plain label.
            noteHtml = consolidated
                ? "No detailed story plan yet."
                : BmadCommands.InlineGuidance(
                    commands.Command("create-story", story.Id),
                    "No detailed story plan yet — draft it with",
                    "No detailed story plan yet.");
        }

        return new StoryCardView
        {
            Id = story.Id,
            TitleHtml = story.Title,
            AnchorId = StoryAnchorId(story.Id),
            StatusStage = StatusStyles.ForStory(story),
            Status = story.Status is { Length: > 0 } s ? s : null,
            TasksDone = story.TasksDone,
            TasksTotal = story.TasksTotal,
            TitleHref = prefix + titleTarget,
            ViewPlanHref = story.ArtifactOutputPath is { } ap ? prefix + ap : null,
            UserStoryHtml = story.UserStoryHtml,
            UserStoryNoteHtml = story.UserStoryNoteHtml,
            AcBlocksHtml = story.AcBlocksHtml,
            NoteHtml = noteHtml,
            UpdatedDate = story.LastUpdatedDate,
        };
    }

    // ----- Story page ---------------------------------------------------------------------------------------

    public static StoryPageView BuildStory(
        EpicInfo epic,
        StoryInfo story,
        string blurbHtml,
        string remainderHtml,
        IReadOnlyList<AcceptanceCriterion> acceptanceCriteria,
        IReadOnlyList<(string Label, string ContentHtml)> devAgentRecord,
        IReadOnlyList<TaskItem> tasks,
        string reviewFindingsHtml,
        string changeLogHtml,
        StoryEvidence evidence,
        StoryChangeSurface changeSurface,
        CommandCatalog commands,
        string? epicRetroPath,
        EntityPager? pager = null)
    {
        var outputPath = story.ArtifactOutputPath
            ?? throw new InvalidOperationException($"BuildStory called for story {story.Id} with no resolved artifact.");
        var prefix = Prefix(outputPath);

        return new StoryPageView
        {
            Id = story.Id,
            TitleHtml = story.Title,
            StatusStage = StatusStyles.ForStory(story),
            Status = story.Status is { Length: > 0 } s ? s : null,
            Evidence = evidence,
            ChangeSurface = changeSurface,
            RetroLinkHtml = RenderStoryRetroLink(epic.Number, prefix, epicRetroPath),
            BlurbHtml = blurbHtml,
            Tasks = tasks,
            NextStepsHtml = BmadCommands.RenderNextSteps(story, commands),
            AcceptanceCriteria = acceptanceCriteria,
            DevAgentRecord = devAgentRecord.Select(d => new DevAgentEntry(d.Label, d.ContentHtml)).ToList(),
            ReviewFindingsHtml = reviewFindingsHtml,
            RemainderHtml = remainderHtml,
            ChangeLogHtml = changeLogHtml,
            Pager = pager ?? EntityPager.None,
        };
    }

    // ----- Story placeholder --------------------------------------------------------------------------------

    public static StoryPlaceholderView BuildStoryPlaceholder(EpicInfo epic, StoryInfo story, CommandCatalog commands, string? epicRetroPath, EntityPager? pager = null)
    {
        var outputPath = StoryEpicLinkifier.StoryPagePath(story.Id);
        var prefix = Prefix(outputPath);
        var epicOutputPath = $"epics/epic-{epic.Number}.html";

        var note = BmadCommands.InlineGuidance(
            commands.Command("create-story", story.Id),
            "This story hasn't been drafted in detail yet — create its plan with",
            "This story hasn't been drafted in detail yet.");

        return new StoryPlaceholderView
        {
            Id = story.Id,
            TitleHtml = story.Title,
            StatusStage = StatusStyles.ForStory(story),
            RetroLinkHtml = RenderStoryRetroLink(epic.Number, prefix, epicRetroPath),
            UserStoryHtml = story.UserStoryHtml,
            UserStoryNoteHtml = story.UserStoryNoteHtml,
            AcBlocksHtml = story.AcBlocksHtml,
            NoteHtml = note,
            EpicNumber = epic.Number,
            BackHref = prefix + epicOutputPath,
            Pager = pager ?? EntityPager.None,
        };
    }

    // ----- Pre-rendered command/prefix-driven opaque fragments ----------------------------------------------
    // These are relocated verbatim from EpicsTemplater; they build small command-catalog-driven guidance HTML
    // that is disproportionate to model as data, so they are carried as named opaque fragments (Story 6.2).

    /// <summary>Relocated from <c>EpicsTemplater.AppendStoryRetroLink</c>. Returns "" when no retro exists.</summary>
    private static string RenderStoryRetroLink(int epicNumber, string prefix, string? epicRetroPath)
    {
        if (epicRetroPath is not { Length: > 0 }) return string.Empty;
        return $"    <a class=\"pill pill-link\" href=\"{PathUtil.Html(prefix + epicRetroPath)}\">Epic {epicNumber} retro &rarr;</a>\n";
    }

    /// <summary>Relocated from <c>EpicsTemplater.AppendRetroAffordance</c>. Returns "" when nothing shows.</summary>
    private static string RenderRetroAffordance(EpicInfo epic, string epicClass, string prefix, CommandCatalog commands, string? epicRetroPath)
    {
        if (epicRetroPath is { Length: > 0 })
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"epic-card epic-retro-affordance\">\n");
            sb.Append($"  <a class=\"epic-retro-link\" href=\"{PathUtil.Html(prefix + epicRetroPath)}\">View Epic {epic.Number} Retrospective &rarr;</a>\n");
            sb.Append("</div>\n\n");
            return sb.ToString();
        }

        // With the retro-gated classifier (BuildEpic passes ForEpicWithRetrospective), an all-stories-done epic
        // with no retro reads as "review" — that is exactly the state that should be nudged to run a retro. Once
        // a retro exists the epic is "done" and this whole method returned above via the epicRetroPath link. [spec-sunburst-retro]
        if (epicClass != "review") return string.Empty;
        var guidance = BmadCommands.InlineGuidance(
            commands.Command("retrospective"),
            $"Epic {epic.Number} is complete — capture the lessons with",
            string.Empty);
        if (guidance.Length == 0) return string.Empty;
        return $"<div class=\"epic-card epic-retro-affordance\">\n  <div class=\"pending-note\">{guidance}</div>\n</div>\n\n";
    }

    /// <summary>One designed banner when an epic has 2+ undrafted stories: a count sentence plus a single
    /// create-story affordance for the next undrafted id (catalog-driven; NFR8 degrades to count-only).
    /// [Story 8.6]</summary>
    private static string RenderUndraftedBanner(EpicInfo epic, IReadOnlyList<StoryInfo> undrafted, CommandCatalog commands)
    {
        _ = epic; // signature kept parallel to other opaque-fragment builders for the epic page
        var n = undrafted.Count;
        var guidance = BmadCommands.InlineGuidance(
            commands.Command("create-story", undrafted[0].Id),
            $"{n} stories in this epic need task plans — draft the next with",
            $"{n} stories in this epic need task plans.");
        return $"<div class=\"epic-undrafted-banner\">{guidance}</div>\n";
    }

    /// <summary>Relocated from <c>EpicsTemplater.AppendNextActionsPanel</c> + <c>AppendUpNextCard</c> — one panel
    /// (Up Next spotlight card + the epic's next-step commands).</summary>
    private static string RenderNextActionsPanel(EpicInfo epic, string prefix, CommandCatalog commands)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel\">\n<h3>Up Next</h3>\n");
        AppendUpNextCard(sb, epic, prefix);

        var nextStepsInner = BmadCommands.RenderEpicNextStepsInner(epic, commands);
        if (nextStepsInner.Length > 0)
        {
            sb.Append(nextStepsInner);
        }
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    private static void AppendUpNextCard(StringBuilder sb, EpicInfo epic, string prefix)
    {
        var active = epic.Stories.FirstOrDefault(s => StatusStyles.ForStory(s) is "active" or "review");
        var ready = active is null ? epic.Stories.FirstOrDefault(s => StatusStyles.ForStory(s) == "ready") : null;
        var undetailed = active is null && ready is null
            ? epic.Stories.FirstOrDefault(s => s.ArtifactOutputPath is null)
            : null;
        var target = active ?? ready ?? undetailed;

        if (target is null)
        {
            sb.Append("  <p class=\"all-done-note\">Every story in this epic is done.</p>\n");
            return;
        }

        var (cssClass, kicker) = active is not null
                ? StatusStyles.ForStory(active) == "review" ? ("review", "In review") : ("active", "In development")
            : ready is not null ? ("ready", "Ready for dev")
            : ("drafted", "Not yet drafted");
        var href = target.ArtifactOutputPath is { } ap ? prefix + ap : $"#{StoryAnchorId(target.Id)}";

        sb.Append($"  <a class=\"now-next-card {cssClass}\" href=\"{PathUtil.Html(href)}\">\n");
        sb.Append($"    <span class=\"now-next-kicker\">{PathUtil.Html(kicker)}</span>\n");
        sb.Append($"    <span class=\"now-next-title\">Story {PathUtil.Html(target.Id)} &middot; {target.Title}</span>\n");
        sb.Append("  </a>\n");
    }

    /// <summary>The in-page anchor id for a story card. Relocated from <c>EpicsTemplater.StoryAnchorId</c>.</summary>
    internal static string StoryAnchorId(string storyId) => $"story-{storyId.Replace('.', '-')}";
}
