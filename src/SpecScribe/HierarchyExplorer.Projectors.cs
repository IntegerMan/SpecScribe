namespace SpecScribe;

/// <summary>The per-surface projections onto <see cref="HierarchyExplorerModel"/> — Story 20.7 F2.
///
/// <para>Until this story <see cref="HierarchyExplorer.ProjectDashboard"/> was the ONLY projection that existed,
/// and it is dashboard-shaped. Three of the five surfaces 20.7 converts had no projector at all, so they are
/// added here rather than grown onto the dashboard's, which would have made one function answer to four
/// datasources.</para>
///
/// <para><b>Every one of these is a pure projection over the view model the call site already holds.</b> No
/// <see cref="ProjectCounts"/> re-count, no second walk of <see cref="EpicsModel"/>, no git call — that
/// discipline is the whole reason ADR 0012 exists, and the one rule that keeps a chart from disagreeing with the
/// page it sits on.</para>
///
/// <para><b>And every one satisfies the four Story 20.4 data-contract findings BY CONSTRUCTION</b>, because
/// each ends in <see cref="HierarchyExplorer.Reparent"/>-equivalent roll-up: exactly one root (Finding A —
/// Plotly refuses a forest outright), no <c>null</c> in <c>values</c> (Finding B — a single null collapses
/// calcdata to one point and renders nothing, silently), a parent that is the exact sum of its drawn children
/// (Finding C), and an emitted <c>branchvalues</c> that matches (<see cref="HierarchyExplorer.BranchValues"/>).
/// The parent-inclusive rule is the one that constrains the SHAPE of these projections, and it is why two of
/// them differ from the SVG they replace in a way worth stating out loud — see
/// <see cref="HierarchyExplorer.ProjectEpic"/>'s task-bulk node.</para></summary>
public static partial class HierarchyExplorer
{
    /// <summary>The structural (non-leaf) colour family for a surface whose leaves are not lifecycle-coloured.
    /// The Impact Map's own directory arcs already use it, so a converted chart's parents keep the exact fill the
    /// shipped one had.</summary>
    private const string ImpactStructuralColorClass = "impact-arc-dir";

    /// <summary>The Impact Map's five-level sequential commit ramp — the class list an impact leaf paints with.
    /// The number of levels is the shipped one; the COLOURS are never named here, only the class that resolves
    /// them, so a token change moves the chart (AD-7).</summary>
    private const int ImpactRampLevels = 5;

    // -----------------------------------------------------------------------------------------------------------
    // Epic detail
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>One epic's own hierarchy: the epic as root, its stories beneath it, each story's stemmed deferred
    /// items beneath that, and the epic's open/done follow-up aggregates as story-band peers. Replaces
    /// <c>Charts.EpicSunburst</c>.
    ///
    /// <para><b>The destination rule is LIFTED, not re-derived</b> (anti-pattern 6). <paramref name="hrefBuilder"/>
    /// is the same closure the call site passes today — <c>view.Prefix + (story.ArtifactOutputPath ??
    /// StoryEpicLinkifier.StoryPagePath(story.Id))</c> — and it becomes a per-node <see cref="HierarchyNode.Href"/>
    /// in the payload. Story 9.13's contract is the one that holds; there is no second one here.</para>
    ///
    /// <para><b>The one shape change from the SVG, stated plainly.</b> <c>EpicSunburst</c> sized a story wedge at
    /// <c>max(1, tasks + stemmedDeferred)</c> and then drew the deferred items across that same sweep — its rings
    /// are NOT parent-inclusive, which a hand-rolled SVG can get away with and Plotly cannot. Projecting the
    /// deferred as children and stopping there would have let the roll-up rewrite a story's value to its deferred
    /// COUNT, so a story with ten tasks and two deferred items would draw smaller than a sibling with ten tasks and
    /// none — more work, less ink. So a story that has both emits an explicit task-bulk child carrying
    /// <c>TasksTotal</c>, and the sum is then exactly the weight the SVG used. A story with no deferred stays a
    /// leaf and is untouched.</para>
    ///
    /// <para><b>No-plan weighting is the SVG's, deliberately.</b> Story 20.5 AC#4's average bump comes from
    /// <c>Charts.SunburstNoPlanStoryWeight</c>, which needs the whole <see cref="EpicsModel"/> to compute an
    /// average; an epic page holds one <see cref="EpicInfo"/>. Rather than invent an epic-local average — a second
    /// weighting rule for the same concept — this keeps <c>EpicSunburst</c>'s own <c>max(1, …)</c> floor, so the
    /// conversion changes no angle on this surface. Recorded in the story rather than left to be discovered.</para></summary>
    public static HierarchyExplorerModel ProjectEpic(
        EpicInfo epic,
        Func<StoryInfo, string> hrefBuilder,
        HierarchyExplorerConfig config,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null)
    {
        var geometry = (followUps ?? FollowUpGeometry.Empty).ForEpic(epic.Number);
        var epicFollowUps = geometry.ActionItems;
        var storyIds = epic.Stories.Select(s => s.Id).ToList();
        var epicLevelDeferred = geometry.EpicLevelDeferred(epic.Number, storyIds);
        var epicQuickDev = (unplanned ?? UnplannedWorkGeometry.Empty).ForEpic(epic.Number);
        var peerCount = epicFollowUps.Count + epicLevelDeferred.Count + epicQuickDev.Count;

        var epicTitle = PathUtil.StripHtmlTags(epic.Title);
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
        var nodes = new List<HierarchyNode>
        {
            new(ProjectRootId, null, $"Epic {epic.Number}: {epicTitle}", $"Epic {epic.Number}", 0,
                $"{epic.Stories.Count} {Charts.Plural(epic.Stories.Count, "story", "stories")}",
                epicClass, StatusStyles.EpicLabel(epicClass), null, "epic",
                PlanningColorClass(epicClass)),
        };

        // First-wins on a duplicate id, matching SunburstExplorerNodes' own dedupe rule: story ids come from
        // `### Story N.M:` headings and nothing dedupes them.
        var seen = new HashSet<string>(StringComparer.Ordinal) { ProjectRootId };
        void Add(HierarchyNode n) { if (seen.Add(n.Id)) nodes.Add(n); }

        foreach (var story in epic.Stories)
        {
            var noPlan = story.TasksTotal == 0;
            var storyClass = noPlan ? "noplan" : StatusStyles.ForStory(story);
            var children = geometry.StoryChildDeferred(epic.Number, story.Id);
            var storyWeight = Math.Max(1, story.TasksTotal + children.Count);
            var storyDetail = noPlan ? "No task plan yet" : $"{story.TasksDone} of {story.TasksTotal} tasks done";

            Add(new HierarchyNode(
                story.Id, ProjectRootId,
                $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}", $"Story {story.Id}",
                storyWeight, storyDetail, storyClass,
                StatusLabelFor(storyClass, "story"), hrefBuilder(story), "story",
                PlanningColorClass(storyClass)));

            if (children.Count == 0) continue;

            // The task-bulk child — see the summary. Emitted only when there are tasks to account for, so the
            // children sum is exactly `tasks + deferred`, i.e. the weight the SVG used.
            if (story.TasksTotal > 0)
            {
                Add(new HierarchyNode(
                    $"{story.Id}~tasks", story.Id, $"Story {story.Id}: {storyDetail}", storyDetail,
                    story.TasksTotal, string.Empty, storyClass, StatusLabelFor(storyClass, "story"),
                    hrefBuilder(story), "story-summary", PlanningColorClass(storyClass)));
            }

            for (var i = 0; i < children.Count; i++)
            {
                var slot = children[i];
                var cls = slot.Item.Resolved ? "followup-done" : "followup-open";
                Add(new HierarchyNode(
                    $"{story.Id}~deferred-{i}", story.Id, Charts.DeferredSlotLabel(slot),
                    slot.Item.Resolved ? "Resolved" : "Open", 1, string.Empty, cls,
                    StatusLabelFor(cls, "follow-up"), slot.DetailHref, "follow-up",
                    PlanningColorClass(cls)));
            }
        }

        if (peerCount > 0)
        {
            var openPeer = epicFollowUps.Count(a => !FollowUpGeometry.IsDone(a))
                + epicLevelDeferred.Count(d => !d.Item.Resolved)
                + epicQuickDev.Count(q => UnplannedWorkGeometry.IsOpenQuickDev(q.Entry.Status));
            var donePeer = peerCount - openPeer;
            var aggregateHref = geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number);

            // The SAME labels the SVG's aggregate wedges carried — this text is user-visible in the breadcrumb and
            // the twin, so a second phrasing here would read as two names for one wedge (the drift Story 20.3's
            // live round caught).
            if (openPeer > 0)
                Add(new HierarchyNode(
                    "epic~open", ProjectRootId,
                    $"Epic {epic.Number}: {openPeer} open {Charts.Plural(openPeer, "follow-up", "follow-ups")}",
                    $"{openPeer} open", openPeer, string.Empty, "followup-open",
                    StatusLabelFor("followup-open", "aggregate"), aggregateHref, "aggregate",
                    PlanningColorClass("followup-open")));
            if (donePeer > 0)
                Add(new HierarchyNode(
                    "epic~done", ProjectRootId,
                    $"Epic {epic.Number}: {donePeer} done {Charts.Plural(donePeer, "follow-up", "follow-ups")}",
                    $"{donePeer} done", donePeer, string.Empty, "followup-done",
                    StatusLabelFor("followup-done", "aggregate"), aggregateHref, "aggregate",
                    PlanningColorClass("followup-done")));
        }

        // Nothing to chart: no stories AND no peers. Return an empty model so the call site can render its own
        // honest empty note rather than an empty chart frame (NFR8).
        if (nodes.Count == 1) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());

        return new HierarchyExplorerModel(config, RollUp(nodes));
    }

    // -----------------------------------------------------------------------------------------------------------
    // Story detail
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>One story's task hierarchy: task → subtask, plus the Deferred parent and its items. Replaces
    /// <c>Charts.TaskSunburst</c>.
    ///
    /// <para><b>This datasource does not speak the story lifecycle</b>, and that is the trap Task 6.2 names. A
    /// task is done or it is not; it is never "In review" or "Ready for dev". <c>StatusStyles.StoryLabel</c> would
    /// answer "Pending" for an undone task, which is a lifecycle STAGE word applied to something that has no
    /// lifecycle — and the shipped SVG's own legend says "Not done". So the prose comes from
    /// <see cref="Charts.TaskStatusLabel"/>, added beside <c>SunburstLocalStatusLabel</c> as the ONE place the task
    /// vocabulary lives. It deliberately does NOT go into <c>SunburstLocalStatusLabel</c> itself: that map is
    /// consulted for every surface, so teaching it "pending means Not done" would have renamed every pending story
    /// on the dashboard.</para>
    ///
    /// <para>Weights mirror the SVG exactly — a task is <c>max(1, subtasks)</c> and its subtasks are 1 each, so the
    /// children already sum to the parent with no adjustment; the Deferred parent is its item count for the same
    /// reason.</para></summary>
    public static HierarchyExplorerModel ProjectStoryTasks(
        string storyId,
        string storyTitle,
        IReadOnlyList<TaskItem> tasks,
        HierarchyExplorerConfig config,
        IReadOnlyList<FollowUpDeferredSlot>? deferred = null,
        string? storyStatusClass = null)
    {
        var deferredItems = deferred ?? Array.Empty<FollowUpDeferredSlot>();
        if (tasks.Count == 0 && deferredItems.Count == 0)
            return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());

        var tasksDone = tasks.Count(t => t.Done);
        var rootDetail = tasks.Count > 0
            ? $"{tasksDone} of {tasks.Count} {Charts.Plural(tasks.Count, "task", "tasks")} done"
            : $"{deferredItems.Count(s => !s.Item.Resolved)} of {deferredItems.Count} open";
        var rootLabel = storyTitle.Length > 0 ? $"Story {storyId}: {storyTitle}" : $"Story {storyId}";

        // The root is THE STORY, so it takes the story's own lifecycle status. It must NOT take
        // `ProjectRootStatusLabel` ("Whole project"): that prose exists because the dashboard's synthesized root is
        // a scope rather than a stage, and borrowing it here made a story's centre sector read
        // "Story 20.5: … — Whole project, 80 of 80 tasks done". Caught in the live round, which is the only place
        // a wrong-but-well-formed accessible name shows up. [Story 20.7 Task 6.2]
        var rootStatus = storyStatusClass is { Length: > 0 } ? storyStatusClass : "unrecognized";
        var nodes = new List<HierarchyNode>
        {
            new(ProjectRootId, null, rootLabel, $"Story {storyId}", 0, rootDetail,
                rootStatus, StatusStyles.StoryLabel(rootStatus), null, "story",
                PlanningColorClass(rootStatus)),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal) { ProjectRootId };
        void Add(HierarchyNode n) { if (seen.Add(n.Id)) nodes.Add(n); }

        for (var t = 0; t < tasks.Count; t++)
        {
            var task = tasks[t];
            var cls = task.Done ? "done" : "pending";
            var text = PathUtil.StripHtmlTags(task.Text);
            var id = $"task-{t}";
            Add(new HierarchyNode(
                id, ProjectRootId, text, ShortTaskLabel(text), Math.Max(1, task.Subtasks.Count),
                string.Empty, cls, Charts.TaskStatusLabel(task.Done), null, "task",
                PlanningColorClass(cls)));

            for (var s = 0; s < task.Subtasks.Count; s++)
            {
                var sub = task.Subtasks[s];
                var subCls = sub.Done ? "done" : "pending";
                var subText = PathUtil.StripHtmlTags(sub.Text);
                Add(new HierarchyNode(
                    $"{id}-{s}", id, subText, ShortTaskLabel(subText), 1, string.Empty, subCls,
                    Charts.TaskStatusLabel(sub.Done), null, "subtask", PlanningColorClass(subCls)));
            }
        }

        if (deferredItems.Count > 0)
        {
            var openDeferred = deferredItems.Count(s => !s.Item.Resolved);
            var doneDeferred = deferredItems.Count - openDeferred;
            var parentClass = openDeferred > 0 ? "followup-open" : "followup-done";
            var parentLabel = openDeferred > 0
                ? $"Deferred: {openDeferred} open / {doneDeferred} done"
                : $"Deferred: {doneDeferred} done";
            Add(new HierarchyNode(
                "deferred", ProjectRootId, parentLabel, "Deferred", deferredItems.Count, string.Empty,
                parentClass, StatusLabelFor(parentClass, "follow-up"),
                // The story page's own deferred panel — the parent is a group, not one item (the SVG's own target).
                "#sec-deferred-from-artifact", "follow-up", PlanningColorClass(parentClass)));

            for (var i = 0; i < deferredItems.Count; i++)
            {
                var slot = deferredItems[i];
                var cls = slot.Item.Resolved ? "followup-done" : "followup-open";
                Add(new HierarchyNode(
                    $"deferred-{i}", "deferred", Charts.DeferredSlotLabel(slot),
                    slot.Item.Resolved ? "Resolved" : "Open", 1, string.Empty, cls,
                    StatusLabelFor(cls, "follow-up"), slot.DetailHref, "follow-up",
                    PlanningColorClass(cls)));
            }
        }

        return new HierarchyExplorerModel(config, RollUp(nodes));
    }

    /// <summary>A task's in-sector label. Plotly's <c>uniformtext</c> draws every label at ONE size — the smallest
    /// that fits any sector — so a single long task sentence silences the labels chart-wide (measured on the
    /// dashboard: 2 of 7 sectors labelled). Task text is a sentence, not an identifier, so there is no natural
    /// short form to take; it is clipped instead, and the full text stays the hover card, the accessible name and
    /// the twin's link text.</summary>
    private static string ShortTaskLabel(string text)
    {
        const int max = 28;
        if (text.Length <= max) return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }

    // -----------------------------------------------------------------------------------------------------------
    // Impact Map
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>The Planning ↔ Code Impact Map as epic → directory → file (owner decision D4). Replaces the
    /// client-side <c>renderTreemap</c>/<c>renderSunburst</c> pair in <c>specscribe.js</c>.
    ///
    /// <para><b>D4's visible consequence, and it must be said where a reader sees it.</b> The shipped chart merged
    /// the selected epics into ONE directory tree, so a file touched by three epics drew once. With the epic as a
    /// real level it draws three times, once under each epic, and the root total therefore reads as TOTAL
    /// ATTRIBUTED CHURN rather than distinct-file churn. What that buys: the epic multi-select stops being a
    /// bespoke merge-and-relayout and becomes the component's generic subtree filter, Plotly's own drill-in scopes
    /// to one epic for free, and the chart's shape finally matches its epic-grouped text twin
    /// (<c>Charts.ImpactMapBody</c>), which was already grouped this way. The framing sentence and the legend both
    /// state the counting basis so the chart and the list below it cannot appear to disagree.</para>
    ///
    /// <para><b>Colour is the shipped ramp, resolved not re-typed.</b> Leaves carry
    /// <c>impact-tm-tile impact-level-N</c> and structural nodes carry <c>impact-arc-dir</c> — the exact classes
    /// the hand-rolled renderers used, applied verbatim by the client's probe. The level function is the shipped
    /// one, moved to generation time (FR31) so the same input always produces the same payload.</para>
    ///
    /// <para><b>The non-colour channel is the number itself.</b> A sequential ramp signals by colour, so each
    /// leaf's <see cref="HierarchyNode.Detail"/> carries the churn and commit counts in words — which is what the
    /// tooltip, the accessible name and the twin all read (UX-DR17).</para></summary>
    public static HierarchyExplorerModel ProjectImpactMap(
        EpicsModel epics,
        PlanningCodeImpactData data,
        string prefix,
        HierarchyExplorerConfig config)
    {
        var attributed = epics.Epics
            .Where(e => data.FilesByEpic.ContainsKey(e.Number))
            .OrderBy(e => e.Number)
            .ToList();
        if (attributed.Count == 0) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());

        // The ramp denominator is the busiest file across every attributed epic, so a level means the same thing
        // in every subtree and filtering cannot silently re-scale the colours out from under the legend.
        var maxCommits = 0;
        foreach (var epic in attributed)
            foreach (var file in data.FilesByEpic[epic.Number])
                if (file.Commits > maxCommits) maxCommits = file.Commits;

        var nodes = new List<HierarchyNode>
        {
            new(ProjectRootId, null, "All attributed code areas", "All areas", 0, string.Empty,
                "unrecognized", "Whole project", null, ProjectRootKind, ImpactStructuralColorClass),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal) { ProjectRootId };
        void Add(HierarchyNode n) { if (seen.Add(n.Id)) nodes.Add(n); }

        foreach (var epic in attributed)
        {
            var files = data.FilesByEpic[epic.Number];
            if (files.Count == 0) continue;

            var epicId = $"epic-{epic.Number}";
            var epicChurn = files.Sum(f => f.Churn);
            Add(new HierarchyNode(
                epicId, ProjectRootId,
                $"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)}", $"Epic {epic.Number}",
                Math.Max(1, epicChurn),
                $"{files.Count} {Charts.Plural(files.Count, "file", "files")} · {epicChurn:N0} lines changed",
                "unrecognized", $"Epic {epic.Number}", $"{prefix}epics/epic-{epic.Number}.html", "epic",
                ImpactStructuralColorClass));

            foreach (var file in files)
            {
                var dir = DirectoryOf(file.Path);
                var dirId = $"{epicId}/{dir}";
                Add(new HierarchyNode(
                    dirId, epicId, dir, dir, 0, string.Empty, "unrecognized", "Directory",
                    null, "directory", ImpactStructuralColorClass));

                Add(new HierarchyNode(
                    $"{dirId}/{file.Path}", dirId, file.Path, FileNameOf(file.Path),
                    // A file that is genuinely attributed but shows zero churn (a pure rename) still gets a
                    // visible tile rather than a zero-width one it is impossible to click or read.
                    Math.Max(1, file.Churn),
                    $"{file.Churn:N0} {Charts.Plural(file.Churn, "line", "lines")} changed across {file.Commits:N0} {Charts.Plural(file.Commits, "commit", "commits")}",
                    "unrecognized", "Touched file",
                    file.CodePageHref is { Length: > 0 } href ? prefix + href : null, "file",
                    $"impact-tm-tile impact-level-{ImpactLevel(file.Commits, maxCommits)}"));
            }
        }

        if (nodes.Count == 1) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());
        return new HierarchyExplorerModel(config, RollUp(nodes));
    }

    /// <summary>The shipped five-level ramp bucket, moved from <c>specscribe.js</c>'s <c>levelOf</c> to generation
    /// time. Same arithmetic, same clamp — a level a reader saw yesterday is the level they see today (FR31).</summary>
    public static int ImpactLevel(int commits, int maxCommits)
    {
        if (maxCommits <= 0) return 1;
        var level = (int)Math.Ceiling(ImpactRampLevels * (double)commits / maxCommits);
        return level < 1 ? 1 : level > ImpactRampLevels ? ImpactRampLevels : level;
    }

    /// <summary>The directory a repo-relative path sits in, or <c>"(root)"</c> for a top-level file — the same
    /// grouping key the shipped renderers used, so the converted chart groups identically.</summary>
    private static string DirectoryOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "(root)" : path[..slash];
    }

    private static string FileNameOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path[(slash + 1)..] : path;
    }
}
