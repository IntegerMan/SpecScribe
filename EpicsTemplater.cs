using System.Text;

namespace DocsForge;

/// <summary>Renders the three epics page types: the epics index, one page per epic, and one page per story
/// (for stories with a resolved implementation-artifacts detail file).</summary>
public static class EpicsTemplater
{
    public static string RenderIndex(EpicsModel model, ProgressModel progress, SiteNav nav)
    {
        const string outputPath = SiteNav.EpicsOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Epics & Stories — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + "docsforge.css"));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Epics", null) }));

        var drafted = model.Epics.Count(e => e.Status == EpicStatus.Drafted);
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Epics &amp; Stories</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {model.Epics.Count} epics &middot; {drafted} with stories drafted</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<section class=\"dashboard\">\n");
        AppendProgressPanel(sb, progress);
        sb.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Project at a Glance</h3>\n");
        sb.Append(Charts.Sunburst(model));
        sb.Append("</div>\n");
        sb.Append("</section>\n\n");

        if (model.OverviewHtml.Length > 0)
        {
            sb.Append($"<div class=\"banner\">{model.OverviewHtml}</div>\n\n");
        }

        AppendChipSection(sb, "Vertical Slice", model.Epics.Where(e => e.Section == EpicSection.VerticalSlice).ToList());
        AppendChipSection(sb, "Further Development", model.Epics.Where(e => e.Section == EpicSection.FurtherDevelopment).ToList());

        sb.Append("<div class=\"section-divider\">All Epics</div>\n\n");
        foreach (var epic in model.Epics)
        {
            AppendEpicCard(sb, epic);
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

        sb.Append(PathUtil.RenderFooter($"{PathUtil.Html(nav.SiteTitle)} &middot; Epics &amp; Stories &middot; last rebuilt {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append(Mermaid.InitScript());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderEpic(EpicInfo epic, EpicProgress progress, SiteNav nav)
    {
        var outputPath = $"epics/epic-{epic.Number}.html";
        var epicClass = StatusStyles.ForEpic(epic);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)} — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + "docsforge.css"));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), null),
        }));

        // "Epic N" rides a kicker line above the h1 (with the status badge alongside it), matching the
        // story page's header, so the h1 carries just the epic's title. No project-name subtitle here —
        // the nav brand already carries it, and repeating it on every epic page was pure noise.
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"kicker-row\">\n");
        sb.Append($"    <span class=\"story-kicker\">Epic {epic.Number}</span>\n");
        sb.Append($"    <span class=\"status-badge {epicClass}\">{PathUtil.Html(StatusStyles.EpicLabel(epicClass))}</span>\n");
        sb.Append("  </div>\n");
        sb.Append($"  <h1>{epic.Title}</h1>\n");
        sb.Append("</header>\n\n");

        // Goal + FR meta only — the title/status already live in the header above.
        if (epic.GoalHtml.Length > 0 || epic.FrMetaHtml is { Length: > 0 })
        {
            sb.Append("<div class=\"epic-card epic-intro\">\n");
            if (epic.GoalHtml.Length > 0) sb.Append($"  <p class=\"epic-goal\">{epic.GoalHtml}</p>\n");
            if (epic.FrMetaHtml is { Length: > 0 }) sb.Append($"  <div class=\"epic-meta\">{epic.FrMetaHtml}</div>\n");
            sb.Append("</div>\n\n");
        }

        var prefix = PathUtil.RelativePrefix(outputPath);
        var nextStepsHtml = BmadCommands.RenderEpicNextSteps(epic);

        if (progress.StoryCount > 0)
        {
            // Side by side instead of stacked — two short panels full-width, one above the other, was a
            // lot of scrolling for not much content. Epic Progress + the combined Up-Next/Next-Steps panel
            // share a column (stacked) next to the taller sunburst, which fills the empty space a lone
            // progress panel used to leave beside it.
            sb.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
            sb.Append("<div class=\"chart-col\">\n");
            sb.Append("<div class=\"chart-panel\">\n<h3>Epic Progress</h3>\n");
            sb.Append(Charts.ProgressBar("Stories detailed", progress.StoriesWithArtifact, progress.StoryCount));
            if (progress.TasksTotal > 0)
            {
                sb.Append(Charts.ProgressBar("Tasks", progress.TasksDone, progress.TasksTotal));
            }
            sb.Append("</div>\n\n");
            AppendNextActionsPanel(sb, epic, prefix);
            sb.Append("</div>\n\n");

            sb.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Story Breakdown</h3>\n");
            sb.Append(Charts.EpicSunburst(epic, story => story.ArtifactOutputPath is { } ap
                ? prefix + ap
                : $"#{StoryAnchorId(story.Id)}"));
            sb.Append("</div>\n");
            sb.Append("</div>\n</section>\n\n");
        }
        else if (nextStepsHtml.Length > 0)
        {
            sb.Append("<section class=\"dashboard-narrow\">\n");
            sb.Append(nextStepsHtml);
            sb.Append("</section>\n\n");
        }

        foreach (var story in epic.Stories)
        {
            AppendStoryCard(sb, story, prefix);
        }

        sb.Append(PathUtil.RenderFooter($"{PathUtil.Html(nav.SiteTitle)} &middot; Epic {epic.Number}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderStory(
        EpicInfo epic,
        StoryInfo story,
        string artifactSourceRelativePath,
        string blurbHtml,
        string remainderHtml,
        IReadOnlyList<(string Label, string ContentHtml)> devAgentRecord,
        IReadOnlyList<TaskItem> tasks,
        SiteNav nav)
    {
        var outputPath = story.ArtifactOutputPath
            ?? throw new InvalidOperationException($"RenderStory called for story {story.Id} with no resolved artifact.");
        var epicOutputPath = $"epics/epic-{epic.Number}.html";
        var storyClass = StatusStyles.ForStory(story);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen($"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)} — {nav.SiteTitle}", PathUtil.RelativePrefix(outputPath) + "docsforge.css"));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), epicOutputPath),
            ($"Story {story.Id}", null),
        }));

        // The id sits on a kicker line above the h1 (so the title wraps naturally instead of sharing a
        // line with the id and breaking mid-word), with the status badge alongside it on that same line.
        // No project-name subtitle here — the nav brand already carries it, and a story is two levels into
        // the project already.
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"kicker-row\">\n");
        sb.Append($"    <span class=\"story-kicker\">Story {PathUtil.Html(story.Id)}</span>\n");
        if (story.Status is { Length: > 0 } status)
        {
            sb.Append($"    <span class=\"status-badge {storyClass}\">{PathUtil.Html(status)}</span>\n");
        }
        sb.Append("  </div>\n");
        sb.Append($"  <h1>{story.Title}</h1>\n");
        sb.Append("</header>\n\n");

        // The narrative ("As a X, I want Y") leads the page — readers want the what/why before the charts.
        if (blurbHtml.Length > 0)
        {
            sb.Append($"<div class=\"story-lead user-story\">{blurbHtml}</div>\n\n");
        }

        sb.Append("<section class=\"dashboard-narrow\">\n<div class=\"chart-row\">\n");
        if (tasks.Count > 0)
        {
            sb.Append("<div class=\"chart-panel sunburst-panel\">\n<h3>Task Breakdown</h3>\n");
            sb.Append(Charts.TaskSunburst(tasks));
            sb.Append("</div>\n");
        }
        sb.Append(BmadCommands.RenderNextSteps(story, artifactSourceRelativePath));
        sb.Append("</div>\n");

        // Dev Agent Record used to be four mostly-empty headings buried at the very bottom — a compact
        // table near the top surfaces "has an agent actually worked this yet?" at a glance.
        if (devAgentRecord.Count > 0)
        {
            sb.Append("<div class=\"chart-panel\">\n<h3>Dev Agent Record</h3>\n<table class=\"dev-agent-table\">\n");
            foreach (var (label, contentHtml) in devAgentRecord)
            {
                sb.Append($"  <tr><th>{PathUtil.Html(label)}</th><td>{contentHtml}</td></tr>\n");
            }
            sb.Append("</table>\n</div>\n");
        }
        sb.Append("</section>\n\n");

        sb.Append("<article class=\"doc-body epic-card\">\n");
        sb.Append(remainderHtml);
        sb.Append("\n</article>\n\n");

        sb.Append(PathUtil.RenderFooter($"{PathUtil.Html(nav.SiteTitle)} &middot; Source: <code>{PathUtil.Html(PathUtil.NormalizeSlashes(artifactSourceRelativePath))}</code>"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendStoryCard(StringBuilder sb, StoryInfo story, string prefix)
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
            sb.Append($"    <span class=\"status-badge task-badge\">{Charts.MiniDonut(story.TasksDone, story.TasksTotal)} {story.TasksDone}/{story.TasksTotal} tasks</span>\n");
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

        if (story.TasksTotal > 0)
        {
            sb.Append("  <div class=\"per-story-progress\">\n");
            sb.Append(Charts.ProgressBar("Tasks", story.TasksDone, story.TasksTotal));
            sb.Append("  </div>\n");
        }

        if (story.ArtifactOutputPath is not null)
        {
            sb.Append($"  <a class=\"view-epic-link\" href=\"{PathUtil.Html(prefix + story.ArtifactOutputPath)}\">View full story plan &rarr;</a>\n");
        }
        else
        {
            sb.Append("  <p class=\"not-detailed-note\">No detailed story plan yet.</p>\n");
        }

        sb.Append("</div>\n\n");
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
        }));
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

    private static void AppendEpicCard(StringBuilder sb, EpicInfo epic)
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
            sb.Append("  <p class=\"pending-note\">Stories not yet drafted.</p>\n");
        }
        sb.Append($"  <a class=\"view-epic-link\" href=\"epics/epic-{epic.Number}.html\">View Epic {epic.Number} stories &rarr;</a>\n");
        sb.Append("</div>\n\n");
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
    private static void AppendNextActionsPanel(StringBuilder sb, EpicInfo epic, string prefix)
    {
        sb.Append("<div class=\"chart-panel\">\n<h3>Up Next</h3>\n");
        AppendUpNextCard(sb, epic, prefix);

        var nextStepsInner = BmadCommands.RenderEpicNextStepsInner(epic);
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
        var active = epic.Stories.FirstOrDefault(s => StatusStyles.ForStory(s) == "active");
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

        var (cssClass, kicker) = active is not null ? ("active", "In development")
            : ready is not null ? ("ready", "Ready for dev")
            : ("drafted", "Not yet drafted");
        var href = target.ArtifactOutputPath is { } ap ? prefix + ap : $"#{StoryAnchorId(target.Id)}";

        sb.Append($"  <a class=\"now-next-card {cssClass}\" href=\"{PathUtil.Html(href)}\">\n");
        sb.Append($"    <span class=\"now-next-kicker\">{PathUtil.Html(kicker)}</span>\n");
        sb.Append($"    <span class=\"now-next-title\">Story {PathUtil.Html(target.Id)} &middot; {target.Title}</span>\n");
        sb.Append("  </a>\n");
    }
}
