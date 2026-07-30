using System.Globalization;

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
                    $"{story.Id}~tasks", story.Id, $"Story {story.Id} tasks: {storyDetail}", storyDetail,
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
                $"{files.Count} {Charts.Plural(files.Count, "file", "files")} · {epicChurn.ToString("N0", CultureInfo.InvariantCulture)} lines changed",
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
                    $"{file.Churn.ToString("N0", CultureInfo.InvariantCulture)} {Charts.Plural(file.Churn, "line", "lines")} changed across {file.Commits.ToString("N0", CultureInfo.InvariantCulture)} {Charts.Plural(file.Commits, "commit", "commits")}",
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

    // -----------------------------------------------------------------------------------------------------------
    // Code Map and Git Insights ownership — the two COLORIZE-DRIVEN surfaces [Story 20.9]
    //
    // These two are the ones Story 20.7's owner decision D1 split out, and the reason is worth stating where the
    // code is: their colour is not a property of a node at all. It is a property of the node CROSSED WITH a
    // dimension the reader is choosing right now — seven of them on one page, four modes plus two live inputs on
    // the other. So the projections below carry each node's RAW metric bag (owner decision D1) and the config
    // carries the per-dimension rules; the component resolves a class list per node per dimension through the
    // shipped cascade and never learns which surface it is drawing.
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>The structural class every DIRECTORY node paints with on both converted surfaces. Directories are
    /// real drawn sectors here (the SVG drew them as boundary rects), and they do NOT participate in any
    /// dimension — a directory has no change frequency and no dominant author, and the shipped SVG never
    /// recoloured one either.
    ///
    /// <para>The <c>-sunburst</c> variant is chosen deliberately over <c>.codemap-dir</c>: the treemap's rule is
    /// <c>fill: none</c>, which was right for a boundary rect drawn OVER its children and is wrong for a Plotly
    /// sector, which is a filled shape. One instance now draws both shapes, so the class with a real fill is the
    /// one that can serve both. Its ownership counterpart <c>.ownership-wedge-dir</c> already has the identical
    /// declaration.</para></summary>
    private const string CodeMapDirColorClass = "codemap-dir-sunburst";

    private const string OwnershipDirColorClass = "ownership-wedge-dir";

    /// <summary>The Code Map's leaf colour FAMILY. The dimension rule appends the state token
    /// (<c>level-3</c>, <c>type-csharp</c>); this is the half that says which stylesheet family resolves it.</summary>
    private const string CodeMapLeafColorClass = "codemap-cell";

    private const string OwnershipLeafColorClass = "ownership-wedge";

    /// <summary>Panel-wide constant keys (Task 1.2) — named once so the emitter and the dimension declarations
    /// cannot drift on a string.</summary>
    public const string ConstantTopAuthors = "topAuthors";

    /// <summary>The tree's most-recent commit day, as a day-number. The staleness and spotlight rules measure
    /// against THIS, never wall-clock <c>now</c> (FR31) — a regenerated portal must colour a file the same way
    /// tomorrow as it did today.</summary>
    public const string ConstantAsOf = "asof";

    /// <summary>Projects one precomputed Code Map filter variant onto the component: directory → file, sized by
    /// lines of code, with each file's seven colorize dimensions carried as raw generation-time values.
    ///
    /// <para><b>The metric bag is LIFTED, not re-derived.</b> Its keys are exactly the <c>data-*</c> the retired
    /// <c>Charts.CodeTreemap</c> wrote on every rect — <c>path</c>, <c>lines</c>, <c>filetype</c>,
    /// <c>filetype-label</c>, <c>changes</c>, <c>churn</c>, <c>first</c>, <c>last</c>, <c>cochanged</c> — in the
    /// same units (day-NUMBERS for the two dates, the same <c>0.###</c> format for co-change). A dimension whose
    /// input changed units would recolour the chart silently, so they did not.</para>
    ///
    /// <para><b>The link guard is Story 7.1's, unchanged.</b> A <paramref name="fileHref"/> returning null leaves
    /// a plain, focusable node — never a broken link — and a non-null return is prefixed exactly as the file
    /// table prefixes it, so the chart and the table cannot route one file two ways.</para>
    ///
    /// <para>Returns an empty model for an empty variant, so the call site renders its own honest "No files match
    /// this filter." rather than an empty chart frame (NFR8).</para></summary>
    public static HierarchyExplorerModel ProjectCodeMap(
        CodeMapVariant variant,
        HierarchyExplorerConfig config,
        Func<string, string?>? fileHref = null,
        string prefix = "")
    {
        if (variant.Map.IsEmpty) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());

        // The detail cap is the SAME one the file table applies, from the same file list — so a file with a table
        // row has a hover card and vice versa, and a large repo cannot reintroduce the per-node HTML bloat
        // `MaxDetailedCodeMapFiles` exists to prevent (Task 4.4 keeps that discipline alive).
        var files = variant.Map.Files();
        var detailed = Charts.SelectDetailedCodeMapFiles(files, variant.Map.FileCount);

        var nodes = new List<HierarchyNode>
        {
            new(ProjectRootId, null, "All files", "All files", 0,
                $"{variant.Map.FileCount:N0} {Charts.Plural(variant.Map.FileCount, "file", "files")}",
                string.Empty, "All files", null, ProjectRootKind, CodeMapDirColorClass),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal) { ProjectRootId };

        WalkCodeMap(variant.Map.Roots, ProjectRootId, node =>
        {
            if (!seen.Add(node.Id)) return;
            nodes.Add(node);
        }, (node, parentId) => CodeMapDirNode(node, parentId), (node, parentId) =>
            CodeMapFileNode(node, parentId, fileHref, prefix, detailed));

        if (nodes.Count == 1) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());
        return new HierarchyExplorerModel(config, RollUp(nodes));
    }

    private static HierarchyNode CodeMapDirNode(CodeMapNode node, string parentId) =>
        new(node.RepoRelativePath, parentId, node.RepoRelativePath, node.Label, 0,
            string.Empty, string.Empty, "Directory", null, "directory", CodeMapDirColorClass);

    /// <summary>Story 20.10's shared-payload projector: builds ONE <see cref="HierarchyExplorerModel"/> across ALL
    /// four Code Map filter variants instead of one per variant (<see cref="ProjectCodeMap"/>, kept for any other
    /// single-variant caller). Every distinct file's metric bag, hover card, label, detail and href is built
    /// exactly once — from the <c>full</c> variant, which is the superset every other variant filters from — and
    /// each variant becomes a <see cref="HierarchyView"/> naming its own directory scaffold and which of those
    /// shared files it contains (owner decision D1; F2's directory-collapse divergence is why the scaffold cannot
    /// be shared too). The detail cap (<see cref="Charts.SelectDetailedCodeMapFiles"/>) is applied ONCE, against
    /// the distinct file set and its true count (F7) — so the chart and every view's table agree on which files
    /// are "detailed" no matter how many views a file appears in.</summary>
    public static HierarchyExplorerModel ProjectCodeMapViews(
        IReadOnlyList<CodeMapVariant> variants,
        HierarchyExplorerConfig config,
        Func<string, string?>? fileHref = null,
        string prefix = "")
    {
        // [Review][Patch] An empty variant list returned an ArgumentOutOfRangeException from `variants[0]` one line
        // before the code written to produce an empty model. This is a public entry point; answer it the same way
        // the empty-map case immediately below does.
        if (variants.Count == 0) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());
        var full = variants.FirstOrDefault(v => v.Key == "full") ?? variants[0];
        if (full.Map.IsEmpty) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());

        // The shared, deduplicated file bag — built ONCE, from the superset variant. Order is the walk's own
        // parent-before-child, directories-then-files order (Charts.OrderBySignificance re-orders PER VIEW below;
        // this index order only has to be stable, not significant, since every view addresses it by index).
        var allFiles = full.Map.Files();
        var detailed = Charts.SelectDetailedCodeMapFiles(allFiles, full.Map.FileCount);
        var sharedNodes = new List<HierarchyNode>(allFiles.Count);
        var fileIndexByPath = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in allFiles)
        {
            // [Review][Patch] `ProjectCodeMap` guards its walk with `seen.Add(node.Id)`; this loop dropped that
            // guard, and the asymmetry was silent: the dictionary assignment below OVERWRITES while `sharedNodes.Add`
            // appends, so a colliding path would leave an orphaned duplicate in the payload that no view indexes.
            if (fileIndexByPath.ContainsKey(f.RepoRelativePath)) continue;
            fileIndexByPath[f.RepoRelativePath] = sharedNodes.Count;
            // ParentId is meaningless on the shared copy (parent is a property of (file, view) — F2); left null
            // rather than any one view's answer, so nothing accidentally reads it as authoritative.
            sharedNodes.Add(CodeMapFileNode(f, parentId: string.Empty, fileHref, prefix, detailed) with { ParentId = null });
        }

        var views = new List<HierarchyView>(variants.Count);
        foreach (var variant in variants)
        {
            views.Add(BuildCodeMapView(variant, fileIndexByPath));
        }

        return new HierarchyExplorerModel(config, sharedNodes, views);
    }

    /// <summary>Builds one variant's <see cref="HierarchyView"/>: its own directory scaffold (never shared, F2)
    /// plus which shared file indices it contains and where each hangs in THIS view. Ordered by
    /// <see cref="Charts.OrderBySignificance"/> over this view's own file subset, matching
    /// <see cref="CodeMapTemplater"/>'s table ordering exactly (Task 4.1 — a subset of one ordering is the same
    /// relative order, so a view's chart and its table never disagree on reading order).
    ///
    /// <para>Walks <see cref="CodeMap.BuildDir"/>'s already-collapsed tree directly rather than through
    /// <see cref="WalkCodeMap"/>'s <c>file</c> builder — the whole point of sharing is to build each file's
    /// (expensive, hover-card-bearing) <see cref="HierarchyNode"/> exactly once, so a per-view walk must record
    /// only an INDEX for a file, never rebuild it.</para></summary>
    private static HierarchyView BuildCodeMapView(CodeMapVariant variant, IReadOnlyDictionary<string, int> fileIndexByPath)
    {
        var title = CodeMapViewTitle(variant);
        var window = $"{variant.Map.FileCount:N0} {Charts.Plural(variant.Map.FileCount, "file", "files")} · {variant.Map.TotalLines:N0} {Charts.Plural((int)Math.Min(variant.Map.TotalLines, int.MaxValue), "line", "lines")}";
        var when = $"cm-exclude-spec={(variant.ExcludesSpecDev ? "1" : "0")};cm-exclude-tests={(variant.ExcludesTests ? "1" : "0")}";

        if (variant.Map.IsEmpty)
            return new HierarchyView(variant.Key, title, window, Array.Empty<HierarchyNode>(), Array.Empty<int>(), Array.Empty<int>(), when);

        var scaffold = new List<HierarchyNode>
        {
            new(ProjectRootId, null, "All files", "All files", 0,
                $"{variant.Map.FileCount:N0} {Charts.Plural(variant.Map.FileCount, "file", "files")}",
                string.Empty, "All files", null, ProjectRootKind, CodeMapDirColorClass),
        };
        var scaffoldIndexById = new Dictionary<string, int>(StringComparer.Ordinal) { [ProjectRootId] = 0 };

        // Ordered file list for THIS view — the same significance order the file table renders (Task 4.1) — with
        // its (path -> scaffold parent id) worked out from the SAME collapsed tree the table's own files() flatten
        // ignores, since the scaffold parent depends on tree position, not flattening order.
        var parentPathOf = new Dictionary<string, string>(StringComparer.Ordinal);
        WalkForScaffold(variant.Map.Roots, ProjectRootId, scaffold, scaffoldIndexById, parentPathOf);

        var orderedFiles = Charts.OrderBySignificance(variant.Map.Files()).ToList();
        var files = new List<int>(orderedFiles.Count);
        var parentIdx = new List<int>(orderedFiles.Count);
        foreach (var f in orderedFiles)
        {
            if (!fileIndexByPath.TryGetValue(f.RepoRelativePath, out var idx)) continue; // defensive; cannot occur (full is the superset)
            var parentPath = parentPathOf.TryGetValue(f.RepoRelativePath, out var pp) ? pp : ProjectRootId;
            if (!scaffoldIndexById.TryGetValue(parentPath, out var sIdx)) sIdx = 0;
            files.Add(idx);
            parentIdx.Add(sIdx);
        }

        return new HierarchyView(variant.Key, title, window, scaffold, files, parentIdx, when);
    }

    /// <summary>Each view's own framed title (F4) — the SAME vocabulary <c>CodeMapTemplater.VariantTitle</c> used
    /// per-panel before Story 20.10 collapsed four panels to one instance. Lives here (not in the templater)
    /// because it is now payload DATA the client swaps on a view change, not a one-time server string.</summary>
    internal static string CodeMapViewTitle(CodeMapVariant variant) =>
        (variant.ExcludesSpecDev, variant.ExcludesTests) switch
        {
            (true, true) => "Source Code Map — excluding spec-driven development directories and tests",
            (true, false) => "Source Code Map — excluding spec-driven development directories",
            (false, true) => "Source Code Map — excluding tests",
            _ => "Source Code Map — every file",
        };

    /// <summary>Walks a variant's already-collapsed directory tree, emitting ONLY directory nodes into
    /// <paramref name="scaffold"/> (never rebuilding a file's expensive <see cref="HierarchyNode"/> — that is the
    /// whole point of the shared-payload split) and recording each file's enclosing directory path in
    /// <paramref name="parentPathOf"/> so <see cref="BuildCodeMapView"/> can resolve it to a scaffold index after
    /// re-ordering the files by significance. Mirrors <see cref="WalkCodeMap"/>'s own traversal order
    /// (directories before files, depth-first) without needing its two-builder-function shape.</summary>
    private static void WalkForScaffold(
        IReadOnlyList<CodeMapNode> level, string parentId,
        List<HierarchyNode> scaffold, Dictionary<string, int> scaffoldIndexById,
        Dictionary<string, string> parentPathOf)
    {
        foreach (var node in level)
        {
            if (node.IsDirectory)
            {
                // [Review][Patch] The same duplicate-id guard `ProjectCodeMap`'s walk carries (`seen.Add`). Without
                // it, a directory whose path collides with an id already in the scaffold — including the
                // synthesized `ProjectRootId` — emitted a second node AND repointed `scaffoldIndexById` away from
                // the first, producing a self-parented root Plotly rejects outright.
                if (scaffoldIndexById.ContainsKey(node.RepoRelativePath))
                {
                    WalkForScaffold(node.Children, node.RepoRelativePath, scaffold, scaffoldIndexById, parentPathOf);
                    continue;
                }
                scaffoldIndexById[node.RepoRelativePath] = scaffold.Count;
                scaffold.Add(CodeMapDirNode(node, parentId));
                WalkForScaffold(node.Children, node.RepoRelativePath, scaffold, scaffoldIndexById, parentPathOf);
            }
            else
            {
                parentPathOf[node.RepoRelativePath] = parentId;
            }
        }
    }

    private static HierarchyNode CodeMapFileNode(
        CodeMapNode node, string parentId, Func<string, string?>? fileHref, string prefix, HashSet<string>? detailed)
    {
        var category = node.Category ?? CodeFileType.Other;
        var metrics = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = node.RepoRelativePath,
            ["lines"] = Inv(node.Lines),
            ["filetype"] = category.Key,
            ["filetype-label"] = category.Label,
        };
        if (node.Metrics is { } m)
        {
            metrics["changes"] = Inv(m.Changes);
            metrics["churn"] = Inv(m.TotalChurn);
            if (m.FirstDate is { } fd) metrics["first"] = Inv(fd.DayNumber);
            if (m.LastDate is { } ld) metrics["last"] = Inv(ld.DayNumber);
            // The SAME "0.###" the SVG's data-cochanged used — a different rounding here would re-bucket files.
            if (m.AvgCoChanged is { } co) metrics["cochanged"] = co.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        var href = fileHref?.Invoke(node.RepoRelativePath);
        var lineWord = Charts.Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines");
        var detail = node.Metrics is { } dm
            ? $"{node.Lines:N0} {lineWord} · {dm.Changes:N0} {Charts.Plural(dm.Changes, "change", "changes")}"
            : $"{node.Lines:N0} {lineWord}";

        return new HierarchyNode(
            node.RepoRelativePath, parentId, node.RepoRelativePath, node.Label,
            // A zero-line file still gets a visible, clickable sector rather than a zero-width one nobody can
            // reach — the same floor the Impact Map's pure-rename tiles take.
            (int)Math.Max(1, Math.Min(node.Lines, int.MaxValue)),
            detail, string.Empty, category.Label,
            href is { Length: > 0 } target ? prefix + target : null,
            "file", CodeMapLeafColorClass, metrics,
            detailed is null || detailed.Contains(node.RepoRelativePath) ? Charts.BuildTreemapCard(node) : null);
    }

    /// <summary>Projects the whole-tree code-ownership hierarchy: directory → file, sized by lines of code, each
    /// file carrying the four modes' raw inputs. Replaces <c>Charts.CodeOwnershipSunburst</c> AND
    /// <c>Charts.CodeOwnershipTreemap</c> — ONE instance where there were two charts, because the component's own
    /// selector re-types the trace in place.
    ///
    /// <para><b>Every file's prose lands in the payload, because this surface's twin is the component's.</b>
    /// Story 7.11 deleted both prior ownership tables on owner feedback, which is why Story 20.6's audit recorded
    /// this page as having no text twin AT ALL. <see cref="HierarchyNode.StatusLabel"/> and
    /// <see cref="HierarchyNode.Detail"/> therefore carry the dominant author, share %, contributor count and
    /// last-active date as words — the twin, the accessible name and the tooltip all read them, so there is one
    /// vocabulary rather than three.</para>
    ///
    /// <para><b>FR-10 / ADR 0010 §4 hold in every mode, and rendering technology does not change that.</b> The
    /// top-contributor roster is a bounded COLOUR PALETTE, not a leaderboard; the spotlight picker is built by the
    /// component from the alphabetical union of every node's own roster, never a top-N ranking. Nothing here
    /// sorts a contributor by volume into reader-facing output.</para></summary>
    public static HierarchyExplorerModel ProjectOwnership(
        IReadOnlyList<CodeMapNode> roots,
        IReadOnlyList<string> topAuthors,
        HierarchyExplorerConfig config,
        Func<string, string?>? fileHref = null,
        HashSet<string>? detailedFiles = null)
    {
        var nodes = new List<HierarchyNode>
        {
            new(ProjectRootId, null, "All files", "All files", 0, string.Empty,
                string.Empty, "Whole tree", null, ProjectRootKind, OwnershipDirColorClass),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal) { ProjectRootId };

        WalkCodeMap(roots, ProjectRootId, node =>
        {
            if (!seen.Add(node.Id)) return;
            nodes.Add(node);
        },
        (node, parentId) => new HierarchyNode(
            node.RepoRelativePath, parentId, node.RepoRelativePath, node.Label, 0,
            string.Empty, string.Empty, "Directory", null, "directory", OwnershipDirColorClass),
        (node, parentId) => OwnershipFileNode(node, parentId, fileHref, detailedFiles));

        if (nodes.Count == 1) return new HierarchyExplorerModel(config, Array.Empty<HierarchyNode>());
        return new HierarchyExplorerModel(config, RollUp(nodes));
    }

    private static HierarchyNode OwnershipFileNode(
        CodeMapNode node, string parentId, Func<string, string?>? fileHref, HashSet<string>? detailedFiles)
    {
        var info = Charts.DescribeOwnershipFile(node, fileHref);
        var contributors = node.Metrics?.Contributors ?? Array.Empty<FileContributor>();

        // The SAME values BuildOwnershipDataAttrs wrote as data-share / data-dominant / data-contributors /
        // data-last / data-owner, in the same units. `owner` stays the compact [name, commits, lastDay] triple
        // array the spotlight rule and the roster union both read.
        var metrics = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = node.RepoRelativePath,
            ["lines"] = Inv(node.Lines),
        };
        if (contributors.Count > 0 && info.SharePct is { } share)
        {
            metrics["share"] = Inv(share);
            metrics["dominant"] = info.DominantName ?? string.Empty;
            metrics["contributors"] = Inv(info.TotalContributors);
            if (info.LastDate is { } d) metrics["last"] = Inv(d.DayNumber);
            metrics["owner"] = Charts.BuildOwnerJson(contributors);
        }

        // The twin's prose (owner decision D3): what the chart conveys, in words. A file with no contributor
        // record says so honestly rather than reading as an unowned file.
        var statusLabel = contributors.Count == 0
            ? "No git history"
            : $"{info.DominantName} · {info.SharePct}% share";
        var detail = contributors.Count == 0
            ? $"{node.Lines:N0} {Charts.Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines")}"
            : $"{info.TotalContributors} {Charts.Plural(info.TotalContributors, "contributor", "contributors")}"
              + (info.LastDate is { } ld ? $" · last active {PortalDates.Day(ld)}" : string.Empty);

        return new HierarchyNode(
            node.RepoRelativePath, parentId, node.RepoRelativePath, node.Label,
            (int)Math.Max(1, Math.Min(node.Lines, int.MaxValue)),
            detail, string.Empty, statusLabel, info.Href, "file", OwnershipLeafColorClass, metrics,
            detailedFiles is null || detailedFiles.Contains(node.RepoRelativePath)
                ? Charts.BuildOwnershipCard(node, info)
                : null);
    }

    /// <summary>The ONE depth-first walk both converted surfaces project through — directories before their
    /// contents, so the emitted order is parent-before-child at every level, which the roll-up, the client filter
    /// and the twin all rely on. A second walk here would be exactly the drift ADR 0012 exists to end.
    ///
    /// <para>A directory with no files anywhere beneath it is emitted anyway: the roll-up will give it value 0,
    /// which Plotly draws as nothing, and dropping it would need a second tree pass to know. An empty directory
    /// cannot occur in a <see cref="CodeMap"/> today (the builder prunes them), so this is a guard rather than a
    /// case.</para></summary>
    private static void WalkCodeMap(
        IReadOnlyList<CodeMapNode> level,
        string parentId,
        Action<HierarchyNode> emit,
        Func<CodeMapNode, string, HierarchyNode> dir,
        Func<CodeMapNode, string, HierarchyNode> file)
    {
        foreach (var node in level)
        {
            if (node.IsDirectory)
            {
                emit(dir(node, parentId));
                WalkCodeMap(node.Children, node.RepoRelativePath, emit, dir, file);
            }
            else
            {
                emit(file(node, parentId));
            }
        }
    }

    private static string Inv(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Inv(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // -----------------------------------------------------------------------------------------------------------
    // The eleven dimension declarations [Story 20.9 Task 1.3]
    //
    // These live BESIDE their projectors and not inside the component, because the vocabulary is the surface's:
    // "dominant-author share" and "file type" are facts about these two pages, and a `switch (surface)` inside the
    // shared component is the drift this epic exists to end (Task 1.8). Every rule below is a VERBATIM port of the
    // one `initCodeMapPanel` / `initOwnershipSunburst` shipped — the fills must be UNCHANGED by the conversion,
    // not merely plausible, so the bucketing, the cut points and the wording all had to travel exactly.
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>Which legend block the Code Map's numeric dimensions share. Six of the seven are ramps over the
    /// same 0–4 scale, so they share one ramp legend whose caption tracks the active dimension; "File type" owns
    /// the discrete one.</summary>
    public const string CodeMapRampLegend = "ramp";

    public const string CodeMapDiscreteLegend = "discrete";

    /// <summary>The Code Map's seven colorize dimensions, in the shipped dropdown's own order — change frequency
    /// first, because it is the default the SVG baked. When <paramref name="hasMetrics"/> is false, file type is
    /// the ONLY dimension (there is nothing for the six git-derived ramps to quantize), matching the shipped
    /// dropdown exactly. [Story 7.9 / 7.12 preserved]</summary>
    public static IReadOnlyList<HierarchyDimension> CodeMapDimensions(bool hasMetrics)
    {
        var fileType = new HierarchyDimension(
            "filetype", "file type", HierarchyDimensionKind.Categorical, "filetype",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "{label}: {value}" },
            ClassPrefix: "type-", NoneClass: "level-none",
            LegendKey: CodeMapDiscreteLegend, LabelMetric: "filetype-label");

        if (!hasMetrics) return new[] { fileType };

        return new[]
        {
            Ramp("changes", "change frequency", "changes"),
            // Absolute day-numbers are ~739,000 and differ by hundreds, so a from-zero ramp would put every file
            // in the top bucket. The date dimensions scale against the file set's own [min,max] window instead —
            // the shipped rule's `isDate` branch, preserved.
            RampWindow("last", "recency of last change", "last"),
            RampWindow("created", "recency of first change", "first"),
            // Churn ÷ changes, with the shipped `!ch` guard: a file with zero changes has no average, and saying
            // "no data" is honest where dividing by zero is not.
            Ramp("avgchange", "average change size", "churn", divisor: "changes"),
            Ramp("churn", "churn", "churn"),
            Ramp("cochange", "files changed together", "cochanged"),
            fileType,
        };
    }

    private static HierarchyDimension Ramp(string key, string label, string metric, string divisor = "") =>
        new(key, label, HierarchyDimensionKind.Ramp, metric, RampText, LegendKey: CodeMapRampLegend, Divisor: divisor);

    private static HierarchyDimension RampWindow(string key, string label, string metric) =>
        new(key, label, HierarchyDimensionKind.RampWindow, metric, RampText, LegendKey: CodeMapRampLegend);

    /// <summary>The ramp dimensions' accessible-name phrasing, shared by all six because the shipped renderer
    /// phrased all six identically. <c>{level}</c> renders as "lowest" / "level N of 4" / "highest" — the BUCKET,
    /// which is exactly what the colour encodes, and never the raw value the colour does not literally
    /// represent.</summary>
    private static readonly IReadOnlyDictionary<string, string> RampText =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["value"] = "{label}: {level}",
            ["none"] = "no data for {label}",
        };

    /// <summary>Git Insights ownership's four live modes, in the shipped selector's own order. Share is the
    /// default (it was the server-baked one).</summary>
    public static IReadOnlyList<HierarchyDimension> OwnershipDimensions() => new[]
    {
        // Fixed cut points, not a data-relative quartile split: a share percentage is meaningful on its own
        // scale, so "76–100%" means the same thing on every repo's chart (Charts.OwnershipShareLevel's reasoning).
        new HierarchyDimension(
            "share", "dominant-author share", HierarchyDimensionKind.Cutoff, "share",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["value"] = "{value}% dominant-author share",
                ["none"] = "no git history",
            },
            LegendKey: "share", Cutoffs: new[] { 25, 50, 75 }),

        // A bounded COLOUR PALETTE, not a leaderboard (FR-10). Anything past the roster falls to the shared
        // overflow class, exactly as a file type past the classified set does.
        new HierarchyDimension(
            "top", "dominant contributor", HierarchyDimensionKind.Roster, "dominant",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["value"] = "dominant contributor: {name}",
                ["none"] = "no git history",
            },
            ClassPrefix: "owner-author-", LegendKey: "top", RosterConstant: ConstantTopAuthors),

        // One of the two dimensions owner decision D1 says cannot be precomputed: the contributor is chosen at
        // runtime from an unbounded roster. Cutoffs are the shipped fixed day-boundaries, and they run the other
        // way from `share`'s — MORE recent is a HIGHER level.
        new HierarchyDimension(
            "spotlight", "contributor spotlight", HierarchyDimensionKind.Spotlight, "owner",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hit"] = "{name} worked on this file ({days} ago)",
                // Touched, but their own last-touch date was not embedded — an honest "unknown", never coerced
                // into a recency bucket the data does not support. [Review 2026-07-22, preserved]
                ["unknown"] = "{name} worked on this file (date unknown)",
                // NEVER the stronger, and sometimes false, "has not worked on this file": a file with more
                // contributors than the per-file cap can have a real contributor who simply ranks below it here.
                ["off"] = "{name} is not among this file's most-active tracked contributors",
                ["none"] = "no git history",
            },
            LegendKey: "spotlight", Cutoffs: new[] { 30, 90, 180 },
            ExtraClass: "spotlight-touched", OffClass: "owner-spotlight-off",
            Arg: HierarchyDimensionArg.Roster),

        // The other one: a free 1–60 month threshold typed into a number input. Measures the FILE's own
        // last-touch date, not anything contributor-specific — `last` carries no author.
        new HierarchyDimension(
            "staleness", "staleness", HierarchyDimensionKind.Threshold, "last",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stale"] = "not touched in {monthsAgo}+ months",
                ["fresh"] = "touched within the last {months} months",
                ["none"] = "no git history",
            },
            ClassPrefix: "owner-", LegendKey: "staleness", Arg: HierarchyDimensionArg.Threshold),
    };
}
