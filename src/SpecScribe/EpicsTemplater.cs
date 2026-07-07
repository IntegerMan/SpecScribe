using System.Text;

namespace SpecScribe;

/// <summary>Renders the three epics page types: the epics index, one page per epic, and one page per story
/// (for stories with a resolved implementation-artifacts detail file).</summary>
public static class EpicsTemplater
{
    public static string RenderIndex(EpicsModel model, ProgressModel progress, SiteNav nav, CommandCatalog commands)
    {
        const string outputPath = SiteNav.EpicsOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Epics & Stories — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName, PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Epics", null) }));

        var drafted = model.Epics.Count(e => e.Status == EpicStatus.Drafted);
        // Single <main id="main-content"> landmark / skip-link target for the epics index. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Epics &amp; Stories</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {model.Epics.Count} epics &middot; {drafted} with stories drafted</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<section class=\"dashboard\">\n");
        AppendProgressPanel(sb, progress);
        sb.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Project at a Glance</h3>\n");
        sb.Append(Charts.Sunburst(model, commands));
        sb.Append("</div>\n");
        sb.Append("</section>\n\n");

        if (model.OverviewHtml.Length > 0)
        {
            sb.Append($"<div class=\"banner\">{model.OverviewHtml}</div>\n\n");
        }

        // Empty state: epics.md exists (so this page is even linked) but lists no epics. Rather than headers
        // over an empty sunburst, signpost the one command that seeds the plan. [Story 2.1 Task 6]
        if (model.Epics.Count == 0)
        {
            AppendEmptyEpicsGuidance(sb, commands);
        }

        AppendChipSection(sb, "Vertical Slice", model.Epics.Where(e => e.Section == EpicSection.VerticalSlice).ToList());
        AppendChipSection(sb, "Further Development", model.Epics.Where(e => e.Section == EpicSection.FurtherDevelopment).ToList());

        if (model.Epics.Count > 0)
        {
            sb.Append("<div class=\"section-divider\">All Epics</div>\n\n");
            foreach (var epic in model.Epics)
            {
                AppendEpicCard(sb, epic, commands);
            }
        }

        // The build-order flowchart, demoted to the bottom — the sunburst above is the primary viz.
        sb.Append("<div class=\"section-divider\">Suggested Build Order</div>\n\n");
        sb.Append(Mermaid.Block(Mermaid.RoadmapDiagram(model)));

        if (model.RequirementsInventoryHtml.Length > 0)
        {
            sb.Append("<details class=\"epic-card requirements-inventory\">\n");
            sb.Append("  <summary>Requirements Inventory</summary>\n");
            sb.Append($"  <div class=\"story-body\">{model.RequirementsInventoryHtml}</div>\n");
            sb.Append("</details>\n\n");
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append(Mermaid.InitScript());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderEpic(EpicInfo epic, EpicProgress progress, SiteNav nav, CommandCatalog commands)
    {
        var outputPath = $"epics/epic-{epic.Number}.html";
        var epicClass = StatusStyles.ForEpic(epic);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)} — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName, PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), null),
        }));

        // Main content is composed into its own builder so it can be wrapped in the two-column page shell
        // beside the TOC sidebar. An epic page is a card list rather than a prose document, so its TOC is a
        // jump-list built in emission order: an Overview entry for the intro, then one entry per story card
        // (each card already carries a stable id), so a reader can leap straight to any story.
        var main = new StringBuilder();
        var toc = new List<Toc.Entry>();

        // "Epic N" rides a kicker line above the h1 (with the status badge alongside it), matching the
        // story page's header, so the h1 carries just the epic's title. No project-name subtitle here —
        // the nav brand already carries it, and repeating it on every epic page was pure noise.
        main.Append("<header class=\"doc-header\">\n");
        main.Append("  <div class=\"kicker-row\">\n");
        main.Append($"    <span class=\"story-kicker\">Epic {epic.Number}</span>\n");
        main.Append($"    <span class=\"status-badge {epicClass}\">{PathUtil.Html(StatusStyles.EpicLabel(epicClass))}</span>\n");
        main.Append("  </div>\n");
        main.Append($"  <h1>{epic.Title}</h1>\n");
        main.Append("</header>\n\n");

        // Goal + FR meta only — the title/status already live in the header above.
        if (epic.GoalHtml.Length > 0 || epic.FrMetaHtml is { Length: > 0 })
        {
            main.Append("<div class=\"epic-card epic-intro\" id=\"sec-overview\">\n");
            if (epic.GoalHtml.Length > 0) main.Append($"  <p class=\"epic-goal\">{epic.GoalHtml}</p>\n");
            if (epic.FrMetaHtml is { Length: > 0 }) main.Append($"  <div class=\"epic-meta\">{epic.FrMetaHtml}</div>\n");
            main.Append("</div>\n\n");
            toc.Add(new Toc.Entry(2, "Overview", "sec-overview"));
        }

        var prefix = PathUtil.RelativePrefix(outputPath);
        var nextStepsHtml = BmadCommands.RenderEpicNextSteps(epic, commands);

        if (progress.StoryCount > 0)
        {
            // Side by side instead of stacked — two short panels full-width, one above the other, was a
            // lot of scrolling for not much content. Epic Progress + the combined Up-Next/Next-Steps panel
            // share a column (stacked) next to the taller sunburst, which fills the empty space a lone
            // progress panel used to leave beside it.
            main.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
            main.Append("<div class=\"chart-col\">\n");
            main.Append("<div class=\"chart-panel\">\n<h3>Epic Progress</h3>\n");
            main.Append(Charts.ProgressBar("Stories detailed", progress.StoriesWithArtifact, progress.StoryCount));
            if (progress.TasksTotal > 0)
            {
                main.Append(Charts.ProgressBar("Tasks", progress.TasksDone, progress.TasksTotal));
            }
            main.Append("</div>\n\n");
            AppendNextActionsPanel(main, epic, prefix, commands);
            main.Append("</div>\n\n");

            main.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Story Breakdown</h3>\n");
            main.Append(Charts.EpicSunburst(epic, story => story.ArtifactOutputPath is { } ap
                ? prefix + ap
                : $"#{StoryAnchorId(story.Id)}", commands));
            main.Append("</div>\n");
            main.Append("</div>\n</section>\n\n");
        }
        else if (nextStepsHtml.Length > 0)
        {
            main.Append("<section class=\"dashboard-narrow\">\n");
            main.Append(nextStepsHtml);
            main.Append("</section>\n\n");
        }

        foreach (var story in epic.Stories)
        {
            AppendStoryCard(main, story, prefix, commands);
            toc.Add(new Toc.Entry(2, $"Story {story.Id}", StoryAnchorId(story.Id)));
        }

        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(main.ToString(), toc));
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        // A ```mermaid fence authored inside an artifact body (story remainder, dev-agent record, review
        // findings, change log) renders as a <pre class="mermaid"> block via RenderBlock. Inject the
        // client-side init script only when one actually landed on this page, mirroring the HasMermaid gate
        // that guards full pages — detail pages otherwise never carry it.
        if (Mermaid.ContainsBlock(sb.ToString()))
        {
            sb.Append(Mermaid.InitScript());
        }
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderStory(
        EpicInfo epic,
        StoryInfo story,
        string artifactSourceRelativePath,
        string blurbHtml,
        string remainderHtml,
        IReadOnlyList<AcceptanceCriterion> acceptanceCriteria,
        IReadOnlyList<(string Label, string ContentHtml)> devAgentRecord,
        IReadOnlyList<TaskItem> tasks,
        string reviewFindingsHtml,
        string changeLogHtml,
        SiteNav nav,
        CommandCatalog commands)
    {
        var outputPath = story.ArtifactOutputPath
            ?? throw new InvalidOperationException($"RenderStory called for story {story.Id} with no resolved artifact.");
        var epicOutputPath = $"epics/epic-{epic.Number}.html";
        var storyClass = StatusStyles.ForStory(story);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)} — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName, PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), epicOutputPath),
            ($"Story {story.Id}", null),
        }));

        // Main content is composed into its own builder (header + panels + article) so it can be wrapped in
        // the two-column page shell beside the TOC sidebar. The detail page emits sections OUT OF source
        // order, so the TOC is built from the templater's ACTUAL emission order, not any source-heading list:
        // User Story → Task Breakdown → Acceptance Criteria → Dev Agent Record → Review Findings →
        // remainder-fragment headings (in their rendered order) → Change Log. Each emitted panel gets a stable
        // id + human label so its TOC link lands on a real anchor.
        var main = new StringBuilder();
        var toc = new List<Toc.Entry>();

        // The id sits on a kicker line above the h1 (so the title wraps naturally instead of sharing a
        // line with the id and breaking mid-word), with the status badge alongside it on that same line.
        // No project-name subtitle here — the nav brand already carries it, and a story is two levels into
        // the project already.
        main.Append("<header class=\"doc-header\">\n");
        main.Append("  <div class=\"kicker-row\">\n");
        main.Append($"    <span class=\"story-kicker\">Story {PathUtil.Html(story.Id)}</span>\n");
        if (story.Status is { Length: > 0 } status)
        {
            main.Append($"    <span class=\"status-badge {storyClass}\">{PathUtil.Html(status)}</span>\n");
        }
        main.Append("  </div>\n");
        main.Append($"  <h1>{story.Title}</h1>\n");
        main.Append("</header>\n\n");

        // The narrative ("As a X, I want Y") leads the page — readers want the what/why before the charts.
        if (blurbHtml.Length > 0)
        {
            main.Append($"<div class=\"story-lead user-story\" id=\"sec-user-story\">{blurbHtml}</div>\n\n");
            toc.Add(new Toc.Entry(2, "User Story", "sec-user-story"));
        }

        main.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
        if (tasks.Count > 0)
        {
            main.Append("<div class=\"chart-panel sunburst-panel\" id=\"sec-task-breakdown\">\n<h3>Task Breakdown</h3>\n");
            main.Append(Charts.TaskSunburst(tasks));
            main.Append("</div>\n");
            toc.Add(new Toc.Entry(2, "Task Breakdown", "sec-task-breakdown"));
        }
        main.Append(BmadCommands.RenderNextSteps(story, commands));
        main.Append("</div>\n");

        // Acceptance Criteria leads the plan as its own panel (ahead of the Dev Agent Record): each
        // criterion is independently anchored (id="ac-N") so the "(AC: #N)" references peppered through
        // the tasks below can deep-link straight to it.
        if (acceptanceCriteria.Count > 0)
        {
            main.Append("<div class=\"chart-panel ac-panel\" id=\"sec-acceptance-criteria\">\n<h3>Acceptance Criteria</h3>\n<div class=\"ac-criteria\">\n");
            foreach (var ac in acceptanceCriteria)
            {
                main.Append($"  <div class=\"ac-criterion\" id=\"ac-{ac.Number}\">\n");
                main.Append($"    <a class=\"ac-anchor\" href=\"#ac-{ac.Number}\">AC #{ac.Number}</a>\n");
                main.Append($"    <div class=\"ac-criterion-body\">{ac.Html}</div>\n");
                main.Append("  </div>\n");
            }
            main.Append("</div>\n</div>\n");
            toc.Add(new Toc.Entry(2, "Acceptance Criteria", "sec-acceptance-criteria"));
        }

        // Dev Agent Record used to be four mostly-empty headings buried at the very bottom. It's now a
        // compact table, collapsed by default (agent bookkeeping, not the first thing a reader wants) but
        // one click away when you need to check whether an agent has actually worked this story.
        if (devAgentRecord.Count > 0)
        {
            main.Append("<details class=\"chart-panel dev-agent-details\" id=\"sec-dev-agent-record\">\n<summary>Dev Agent Record</summary>\n<table class=\"dev-agent-table\">\n");
            foreach (var (label, contentHtml) in devAgentRecord)
            {
                main.Append($"  <tr><th>{PathUtil.Html(label)}</th><td>{contentHtml}</td></tr>\n");
            }
            main.Append("</table>\n</details>\n");
            toc.Add(new Toc.Entry(2, "Dev Agent Record", "sec-dev-agent-record"));
        }
        main.Append("</section>\n\n");

        // Review Findings ride high on the page — a code-review outcome is one of the first things a reader
        // wants, ahead of the full plan. Rendered outside the plan article so it reads as its own callout.
        if (reviewFindingsHtml.Length > 0)
        {
            main.Append("<section class=\"chart-panel review-findings\" id=\"sec-review-findings\">\n<h3>Review Findings</h3>\n");
            main.Append($"<div class=\"doc-body\">{reviewFindingsHtml}</div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, "Review Findings", "sec-review-findings"));
        }

        main.Append("<article class=\"doc-body epic-card\">\n");
        main.Append(remainderHtml);
        main.Append("\n</article>\n\n");
        // The remainder renders in the middle in its own source order; its headings receive ids from Markdig's
        // auto-identifier extension, so gather them here (in rendered order) to slot into the TOC between the
        // Review-Findings panel above and the Change-Log panel below.
        toc.AddRange(Toc.ExtractHeadings(remainderHtml));

        // Change Log (revision history) anchors the very bottom of the page as its own section.
        if (changeLogHtml.Length > 0)
        {
            main.Append("<section class=\"chart-panel change-log\" id=\"sec-change-log\">\n<h3>Change Log</h3>\n");
            main.Append($"<div class=\"doc-body\">{changeLogHtml}</div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, "Change Log", "sec-change-log"));
        }

        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(main.ToString(), toc));
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        // A ```mermaid fence authored inside an artifact body (story remainder, dev-agent record, review
        // findings, change log) renders as a <pre class="mermaid"> block via RenderBlock. Inject the
        // client-side init script only when one actually landed on this page, mirroring the HasMermaid gate
        // that guards full pages — detail pages otherwise never carry it.
        if (Mermaid.ContainsBlock(sb.ToString()))
        {
            sb.Append(Mermaid.InitScript());
        }
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Renders the placeholder page for a story that exists in epics.md but has no implementation
    /// artifact yet. Lives at the exact path the real story page will use (see
    /// <see cref="StoryEpicLinkifier.StoryPagePath"/>), so inline "Story N.M" mentions always resolve and a
    /// later-drafted artifact overwrites it in place. Shows what the plan already knows (narrative + epics.md
    /// acceptance criteria) and signposts the create-story command instead of dead-ending.</summary>
    public static string RenderStoryPlaceholder(EpicInfo epic, StoryInfo story, SiteNav nav, CommandCatalog commands)
    {
        var outputPath = StoryEpicLinkifier.StoryPagePath(story.Id);
        var epicOutputPath = $"epics/epic-{epic.Number}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)} — {nav.SiteTitle}", prefix + ForgeOptions.StylesheetName, prefix + ForgeOptions.ScriptName));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), epicOutputPath),
            ($"Story {story.Id}", null),
        }));

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"kicker-row\">\n");
        sb.Append($"    <span class=\"story-kicker\">Story {PathUtil.Html(story.Id)}</span>\n");
        sb.Append($"    <span class=\"status-badge {StatusStyles.ForStory(story)}\">Not yet drafted</span>\n");
        sb.Append("  </div>\n");
        sb.Append($"  <h1>{story.Title}</h1>\n");
        sb.Append("</header>\n\n");

        if (story.UserStoryHtml.Length > 0)
        {
            sb.Append($"<div class=\"story-lead user-story\">{story.UserStoryHtml}</div>\n\n");
        }

        if (story.AcBlocksHtml.Count > 0)
        {
            sb.Append("<div class=\"chart-panel ac-panel\">\n<h3>Acceptance Criteria</h3>\n<div class=\"ac-list\">\n");
            foreach (var block in story.AcBlocksHtml)
            {
                sb.Append($"  <div class=\"ac-block\">{block}</div>\n");
            }
            sb.Append("</div>\n</div>\n\n");
        }

        // The dead-end ("no plan yet") becomes a next action when the module exposes create-story,
        // mirroring the epic page's undrafted-story note. [Story 2.1 Task 6]
        var note = BmadCommands.InlineGuidance(
            commands.Command("create-story", story.Id),
            "This story hasn't been drafted in detail yet — create its plan with",
            "This story hasn't been drafted in detail yet.");
        sb.Append($"<div class=\"epic-card\">\n  <p class=\"pending-note\">{note}</p>\n</div>\n\n");

        sb.Append($"<a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + epicOutputPath)}\">&larr; Back to Epic {epic.Number}</a>\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendStoryCard(StringBuilder sb, StoryInfo story, string prefix, CommandCatalog commands)
    {
        var storyClass = StatusStyles.ForStory(story);

        sb.Append($"<div class=\"story-card\" id=\"{StoryAnchorId(story.Id)}\">\n");
        sb.Append("  <div class=\"story-card-header\">\n");
        sb.Append($"    <span class=\"story-id\">{PathUtil.Html(story.Id)}</span>\n");

        if (story.ArtifactOutputPath is { } artifactPath)
        {
            sb.Append($"    <a class=\"story-title story-title-link\" href=\"{PathUtil.Html(prefix + artifactPath)}\">{story.Title}</a>\n");
        }
        else
        {
            sb.Append($"    <span class=\"story-title\">{story.Title}</span>\n");
        }

        if (story.Status is { Length: > 0 } status)
        {
            sb.Append($"    <span class=\"status-badge {storyClass}\">{PathUtil.Html(status)}</span>\n");
        }
        if (story.TasksTotal > 0)
        {
            sb.Append($"    {TaskBadge(story)}\n");
        }
        sb.Append("  </div>\n");

        sb.Append($"  <div class=\"user-story\">{story.UserStoryHtml}</div>\n");

        if (story.AcBlocksHtml.Count > 0)
        {
            sb.Append("  <div class=\"ac-label\">Acceptance Criteria</div>\n  <div class=\"ac-list\">\n");
            foreach (var block in story.AcBlocksHtml)
            {
                sb.Append($"    <div class=\"ac-block\">{block}</div>\n");
            }
            sb.Append("  </div>\n");
        }

        if (story.ArtifactOutputPath is not null)
        {
            sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + story.ArtifactOutputPath)}\">View full story plan &rarr;</a>\n");
        }
        else
        {
            // An undrafted story's dead-end note becomes a next action: draft it with create-story N.N when
            // the module exposes that command. [Story 2.1 Task 6]
            var note = BmadCommands.InlineGuidance(
                commands.Command("create-story", story.Id),
                "No detailed story plan yet — draft it with",
                "No detailed story plan yet.");
            sb.Append($"  <p class=\"not-detailed-note\">{note}</p>\n");
        }

        sb.Append("</div>\n\n");
    }

    /// <summary>The story card's single task indicator (the bottom per-story progress bar was dropped as
    /// redundant). Task counts are usually all-or-nothing, so the display adapts: a checkmark when every
    /// task is done, a muted count when none are, and the mini donut only in the one state where a
    /// fraction visual actually informs — partial progress. Neutral tones throughout; green stays
    /// reserved for lifecycle done status (the story badge beside it).</summary>
    private static string TaskBadge(StoryInfo story)
    {
        if (story.TasksDone >= story.TasksTotal)
        {
            return $"<span class=\"status-badge task-badge complete\">&#10003; {story.TasksTotal} tasks</span>";
        }
        if (story.TasksDone == 0)
        {
            return $"<span class=\"status-badge task-badge none-done\">0/{story.TasksTotal} tasks</span>";
        }
        return $"<span class=\"status-badge task-badge\">{Charts.MiniDonut(story.TasksDone, story.TasksTotal)} {story.TasksDone}/{story.TasksTotal} tasks</span>";
    }

    private static void AppendProgressPanel(StringBuilder sb, ProgressModel progress)
    {
        sb.Append("<div class=\"stat-grid\">\n");
        sb.Append(Charts.StatCard($"{progress.EpicsDrafted}/{progress.EpicsTotal}", "Epics drafted"));
        sb.Append(Charts.StatCard(progress.StoriesTotal.ToString(), "Stories defined", $"{progress.StoriesWithArtifact} with a task plan"));
        sb.Append(progress.TasksTotal > 0
            ? Charts.StatCard($"{progress.TasksDone}/{progress.TasksTotal}", "Tasks done", $"across {progress.StoriesWithArtifact} planned stor{(progress.StoriesWithArtifact == 1 ? "y" : "ies")}")
            : Charts.StatCard("—", "Tasks done", "none tracked yet"));
        sb.Append("</div>\n\n");

        sb.Append("<div class=\"chart-panel\">\n<h3>Epic Status</h3>\n<div class=\"donut-and-legend\">\n");
        sb.Append(Charts.Donut(new (string, int, string)[]
        {
            ("Drafted", progress.EpicsDrafted, "drafted"),
            ("Pending", progress.EpicsPending, "pending"),
        }, ariaLabel: $"Epic status: {progress.EpicsDrafted} drafted, {progress.EpicsPending} pending"));
        sb.Append("<div class=\"donut-legend\">\n");
        sb.Append($"  <span><span class=\"swatch drafted\"></span>Drafted ({progress.EpicsDrafted})</span>\n");
        sb.Append($"  <span><span class=\"swatch pending\"></span>Pending ({progress.EpicsPending})</span>\n");
        sb.Append("</div>\n</div>\n</div>\n\n");

        // Full width, not paired with the compact donut above — a 22-card mosaic stretched to match a
        // small donut's height (or vice versa) just leaves one of them mostly empty.
        sb.Append("<div class=\"chart-panel\">\n<h3>Progress by Epic</h3>\n");
        sb.Append(Charts.EpicMosaic(progress.PerEpic, e => $"epics/epic-{e.Number}.html"));
        sb.Append("</div>\n\n");
    }

    private static void AppendChipSection(StringBuilder sb, string title, List<EpicInfo> epics)
    {
        if (epics.Count == 0) return;

        sb.Append($"<div class=\"section-divider\">{PathUtil.Html(title)}</div>\n<div class=\"epic-overview\">\n");
        foreach (var epic in epics)
        {
            var cls = StatusStyles.ForEpic(epic);
            sb.Append($"  <a class=\"epic-chip {cls}\" href=\"epics/epic-{epic.Number}.html\"><span class=\"num\">{epic.Number:00}</span>{epic.Title}</a>\n");
        }
        sb.Append("</div>\n\n");
    }

    private static void AppendEpicCard(StringBuilder sb, EpicInfo epic, CommandCatalog commands)
    {
        var statusCls = StatusStyles.ForEpic(epic);
        sb.Append($"<div class=\"epic-card\" id=\"epic-{epic.Number}\">\n");
        sb.Append($"  <h2><span class=\"epic-num\">Epic {epic.Number}</span> {epic.Title} <span class=\"epic-status {statusCls}\">{PathUtil.Html(StatusStyles.EpicLabel(statusCls))}</span></h2>\n");

        if (epic.GoalHtml.Length > 0)
        {
            sb.Append($"  <p class=\"epic-goal\">{epic.GoalHtml}</p>\n");
        }
        if (epic.FrMetaHtml is { Length: > 0 })
        {
            sb.Append($"  <div class=\"epic-meta\">{epic.FrMetaHtml}</div>\n");
        }
        if (epic.Status == EpicStatus.Pending)
        {
            // A pending epic's dead-end note becomes a next action when the module exposes the command. [Story 2.1 Task 6]
            var note = BmadCommands.InlineGuidance(
                commands.Command("create-epics-and-stories"),
                "Stories not yet drafted — draft them with",
                "Stories not yet drafted.");
            sb.Append($"  <p class=\"pending-note\">{note}</p>\n");
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"epics/epic-{epic.Number}.html\">View Epic {epic.Number} stories &rarr;</a>\n");
        sb.Append("</div>\n\n");
    }

    /// <summary>The zero-epic empty state for the epics index: epics.md exists but breaks down into no epics,
    /// so point at the command that seeds the plan. Omits the command cleanly if the module doesn't expose
    /// it. [Story 2.1 Task 6]</summary>
    private static void AppendEmptyEpicsGuidance(StringBuilder sb, CommandCatalog commands)
    {
        var note = BmadCommands.InlineGuidance(
            commands.Command("create-epics-and-stories"),
            "No epics yet. Break your plan into epics and stories with",
            "No epics yet — add them to your plan to see them here.");
        sb.Append($"<div class=\"epic-card empty-state\">\n  <p class=\"pending-note\">{note}</p>\n</div>\n\n");
    }

    /// <summary>Breadcrumb label like "1 · World Rendering & Interac…" — the number alone told you nothing.</summary>
    private static string EpicCrumbLabel(EpicInfo epic)
    {
        var title = PathUtil.StripHtmlTags(epic.Title);
        const int maxLength = 26;
        if (title.Length > maxLength)
        {
            title = title[..maxLength].TrimEnd() + "…";
        }
        return $"{epic.Number} · {title}";
    }

    /// <summary>The in-page anchor id for a story card, used by the epic sunburst to jump to a story
    /// that has no detailed artifact (and therefore no separate page to link to).</summary>
    private static string StoryAnchorId(string storyId) => $"story-{storyId.Replace('.', '-')}";

    /// <summary>One panel that answers "what next?" end to end: the spotlighted story card on top (what to
    /// look at), then the BMad commands that act on it (how to move it forward). These used to be two
    /// separate stacked panels — folding them together keeps the target and its workflow prompt in one
    /// place. Fills the vertical space the progress-bar column otherwise left empty beside the taller
    /// sunburst.</summary>
    private static void AppendNextActionsPanel(StringBuilder sb, EpicInfo epic, string prefix, CommandCatalog commands)
    {
        sb.Append("<div class=\"chart-panel\">\n<h3>Up Next</h3>\n");
        AppendUpNextCard(sb, epic, prefix);

        var nextStepsInner = BmadCommands.RenderEpicNextStepsInner(epic, commands);
        if (nextStepsInner.Length > 0)
        {
            sb.Append(nextStepsInner);
        }
        sb.Append("</div>\n\n");
    }

    /// <summary>Spotlights the single story that best represents "what to look at next" in this epic —
    /// a story in development, else one ready for dev, else the next undetailed one — or says the epic is
    /// fully done.</summary>
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
}
