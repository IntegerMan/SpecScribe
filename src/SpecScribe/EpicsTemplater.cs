namespace SpecScribe;

/// <summary>Renders the three epics page types: the epics index, one page per epic, and one page per story
/// (for stories with a resolved implementation-artifacts detail file).
/// <para>Story 6.2 decomposed each page BODY into a host-neutral section view model (built by
/// <see cref="EpicsViewBuilder"/>) that <see cref="HtmlRenderAdapter"/> renders to today's exact bytes; these
/// entry points are now thin — build the domain models → build the section view model → ask the adapter to render
/// the body → flow it through the shared chrome as 6.1's <see cref="PageView.BodyHtml"/> seam. Bytes unchanged
/// (the golden regression is the gate). [Story 6.1; Story 6.2]</para></summary>
public static class EpicsTemplater
{
    public static string RenderIndex(EpicsModel model, ProgressModel progress, SiteNav nav, CommandCatalog commands, ProjectCounts? counts = null, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null) =>
        HtmlRenderAdapter.Shared.Render(BuildIndexPage(model, progress, nav, commands, counts, followUps, unplanned)).Content;

    /// <summary>Builds the epics-index <see cref="PageView"/> without committing to a surface — the mechanical
    /// split of <see cref="RenderIndex"/> (bytes unchanged: it now just feeds this through the HTML adapter) that
    /// lets the webview surface render the SAME page model through <see cref="WebviewRenderAdapter"/> instead of
    /// duplicating the view/PageView assembly. Same split as the other <c>Build*Page</c> methods here. [Story 6.4]</summary>
    public static PageView BuildIndexPage(EpicsModel model, ProgressModel progress, SiteNav nav, CommandCatalog commands, ProjectCounts? counts = null, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null)
    {
        const string outputPath = SiteNav.EpicsOutputPath;
        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[] { ("Home", "index.html"), ("Epics", null) });

        var view = EpicsViewBuilder.BuildIndex(model, progress, nav, commands, counts, followUps, unplanned);
        var body = HtmlRenderAdapter.Shared.RenderEpicsIndexBody(view);

        // The epics index drills down to each epic page; its parent is Home (from the breadcrumb). The roadmap
        // diagram is always emitted, so MermaidNeeded is always true here (ContainsBlock confirms it). [Story 6.1]
        var prefix = PathUtil.RelativePrefix(outputPath);
        var page = new PageView
        {
            Kind = PageKind.Epics,
            OutputRelativePath = outputPath,
            Title = $"Epics & Stories — {nav.SiteTitle}",
            Nav = nav.ToNavigationView(outputPath),
            Breadcrumb = breadcrumb,
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = Mermaid.ContainsBlock(body),
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = model.Epics.Select(e => $"epics/epic-{e.Number}.html").ToList(),
            },
            BodyHtml = body,
        };
        return page;
    }

    public static string RenderEpic(EpicInfo epic, EpicProgress progress, SiteNav nav, CommandCatalog commands, string? epicRetroPath = null, EntityPager? pager = null, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null) =>
        HtmlRenderAdapter.Shared.Render(BuildEpicPage(epic, progress, nav, commands, epicRetroPath, pager, followUps, unplanned)).Content;

    /// <summary>Builds an epic page's <see cref="PageView"/> — see <see cref="BuildIndexPage"/> for why the
    /// build/render split exists. [Story 6.4]</summary>
    public static PageView BuildEpicPage(EpicInfo epic, EpicProgress progress, SiteNav nav, CommandCatalog commands, string? epicRetroPath = null, EntityPager? pager = null, FollowUpGeometry? followUps = null, UnplannedWorkGeometry? unplanned = null)
    {
        var outputPath = $"epics/epic-{epic.Number}.html";
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);

        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), null),
        });

        var view = EpicsViewBuilder.BuildEpic(epic, progress, commands, epicRetroPath, followUps, unplanned);
        var body = HtmlRenderAdapter.Shared.RenderEpicBody(view);

        // An epic drills up to the epics index and down to each of its story pages (drafted → the story's
        // artifact page, undrafted → its placeholder page); its status stage is the epic roll-up. MermaidNeeded
        // is computed from the body — a ```mermaid fence authored inside an artifact section renders as a
        // <pre class="mermaid"> block, injecting the client init only when one actually landed (as before). [Story 6.1]
        var prefix = PathUtil.RelativePrefix(outputPath);
        var page = new PageView
        {
            Kind = PageKind.Epic,
            OutputRelativePath = outputPath,
            Title = $"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)} — {nav.SiteTitle}",
            Nav = nav.ToNavigationView(outputPath, BuildStoriesLocalContext(epic, outputPath, "Stories in this epic", activeStoryId: null)),
            Breadcrumb = breadcrumb,
            Pager = pager,
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = Mermaid.ContainsBlock(body),
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                ChildTargets = epic.Stories.Select(s => s.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(s.Id)).ToList(),
                StatusStage = epicClass,
            },
            BodyHtml = body,
        };
        return page;
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
        StoryEvidence evidence,
        StoryChangeSurface changeSurface,
        SiteNav nav,
        CommandCatalog commands,
        string? epicRetroPath = null,
        EntityPager? pager = null,
        FollowUpGeometry? followUps = null) =>
        HtmlRenderAdapter.Shared.Render(BuildStoryPage(
            epic, story, artifactSourceRelativePath, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord,
            tasks, reviewFindingsHtml, changeLogHtml, evidence, changeSurface, nav, commands, epicRetroPath, pager, followUps)).Content;

    /// <summary>Builds a drafted story page's <see cref="PageView"/> — see <see cref="BuildIndexPage"/> for why
    /// the build/render split exists. [Story 6.4]</summary>
    public static PageView BuildStoryPage(
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
        StoryEvidence evidence,
        StoryChangeSurface changeSurface,
        SiteNav nav,
        CommandCatalog commands,
        string? epicRetroPath = null,
        EntityPager? pager = null,
        FollowUpGeometry? followUps = null)
    {
        var outputPath = story.ArtifactOutputPath
            ?? throw new InvalidOperationException($"RenderStory called for story {story.Id} with no resolved artifact.");
        var epicOutputPath = $"epics/epic-{epic.Number}.html";
        var storyClass = StatusStyles.ForStory(story);

        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), epicOutputPath),
            ($"Story {story.Id}", null),
        });

        var view = EpicsViewBuilder.BuildStory(
            epic, story, blurbHtml, remainderHtml, acceptanceCriteria, devAgentRecord, tasks,
            reviewFindingsHtml, changeLogHtml, evidence, changeSurface, commands, epicRetroPath, followUps);
        var body = HtmlRenderAdapter.Shared.RenderStoryBody(view);

        // A story is a drill leaf (no children); it drills up to its epic page. Its status stage is the story
        // roll-up, but only when the page actually renders a status badge (matching the header's own guard), so
        // the interaction model never asserts a status the page doesn't show. MermaidNeeded from the body. [Story 6.1]
        var page = new PageView
        {
            Kind = PageKind.Story,
            OutputRelativePath = outputPath,
            Title = $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)} — {nav.SiteTitle}",
            Nav = nav.ToNavigationView(outputPath, BuildStoriesLocalContext(epic, outputPath, $"Stories in Epic {epic.Number}", story.Id)),
            Breadcrumb = breadcrumb,
            Pager = pager,
            Assets = new AssetManifest
            {
                StylesheetHref = PathUtil.RelativePrefix(outputPath) + ForgeOptions.StylesheetName,
                ScriptHref = PathUtil.RelativePrefix(outputPath) + ForgeOptions.ScriptName,
                MermaidNeeded = Mermaid.ContainsBlock(body),
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                StatusStage = story.Status is { Length: > 0 } ? storyClass : null,
            },
            BodyHtml = body,
        };
        return page;
    }

    /// <summary>Renders the placeholder page for a story that exists in epics.md but has no implementation
    /// artifact yet. Lives at the exact path the real story page will use (see
    /// <see cref="StoryEpicLinkifier.StoryPagePath"/>), so inline "Story N.M" mentions always resolve and a
    /// later-drafted artifact overwrites it in place. Shows what the plan already knows (narrative + epics.md
    /// acceptance criteria) and signposts the create-story command instead of dead-ending.</summary>
    public static string RenderStoryPlaceholder(EpicInfo epic, StoryInfo story, SiteNav nav, CommandCatalog commands, string? epicRetroPath = null, EntityPager? pager = null) =>
        HtmlRenderAdapter.Shared.Render(BuildStoryPlaceholderPage(epic, story, nav, commands, epicRetroPath, pager)).Content;

    /// <summary>Builds an undrafted story's placeholder <see cref="PageView"/> — see <see cref="BuildIndexPage"/>
    /// for why the build/render split exists. [Story 6.4]</summary>
    public static PageView BuildStoryPlaceholderPage(EpicInfo epic, StoryInfo story, SiteNav nav, CommandCatalog commands, string? epicRetroPath = null, EntityPager? pager = null)
    {
        var outputPath = StoryEpicLinkifier.StoryPagePath(story.Id);
        var epicOutputPath = $"epics/epic-{epic.Number}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);

        var breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Epics", SiteNav.EpicsOutputPath),
            (EpicCrumbLabel(epic), epicOutputPath),
            ($"Story {story.Id}", null),
        });

        var view = EpicsViewBuilder.BuildStoryPlaceholder(epic, story, commands, epicRetroPath);
        var body = HtmlRenderAdapter.Shared.RenderStoryPlaceholderBody(view);

        // A placeholder story page always renders a "Not yet drafted" status badge, so its stage is the story
        // roll-up; it drills up to its epic and has no children (no drafted plan to link into). [Story 6.1]
        var page = new PageView
        {
            Kind = PageKind.Story,
            OutputRelativePath = outputPath,
            Title = $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)} — {nav.SiteTitle}",
            Nav = nav.ToNavigationView(outputPath, BuildStoriesLocalContext(epic, outputPath, $"Stories in Epic {epic.Number}", story.Id)),
            Breadcrumb = breadcrumb,
            Pager = pager,
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = Mermaid.ContainsBlock(body),
            },
            Interaction = new InteractionState
            {
                ParentTarget = breadcrumb.ParentTarget,
                StatusStage = StatusStyles.ForStory(story),
            },
            BodyHtml = body,
        };
        return page;
    }

    /// <summary>Builds the white sub-header band's local context for epic/story pages: the epic's own story list
    /// (already resolved on <c>epic.Stories</c>, the exact href expression <see cref="BuildEpicPage"/> already
    /// uses for <c>ChildTargets</c>), no recomputation. On the epic page no story is "current" (all inactive); on
    /// a story/placeholder page the current story is marked active. [Story 10.10]</summary>
    private static NavLocalContext BuildStoriesLocalContext(EpicInfo epic, string outputPath, string title, string? activeStoryId)
    {
        var prefix = PathUtil.RelativePrefix(outputPath);
        var items = epic.Stories
            .Select(s => new NavLocalItem(
                $"Story {s.Id}",
                prefix + (s.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(s.Id)),
                activeStoryId is not null && string.Equals(s.Id, activeStoryId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new NavLocalContext(title, items);
    }

    /// <summary>Breadcrumb label like "1 · World Rendering & Interac…" — the number alone told you nothing.</summary>
    public static string EpicCrumbLabel(EpicInfo epic)
    {
        var title = PathUtil.StripHtmlTags(epic.Title);
        const int maxLength = 26;
        if (title.Length > maxLength)
        {
            title = title[..maxLength].TrimEnd() + "…";
        }
        return $"{epic.Number} · {title}";
    }
}
