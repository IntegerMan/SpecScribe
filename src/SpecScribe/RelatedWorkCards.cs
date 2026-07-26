namespace SpecScribe;

/// <summary>One selection's card in the Story 20.3 details rail — the "fancy card" that augments a chart selection:
/// the node's name, a one-line summary of what it holds, a single most-relevant AI action, and a link to its full
/// detail page. <see cref="Relationships"/> carries the work-graph groups that render as the JS-off fallback (the
/// AC #2 / NFR8 server-rendered relationship block); with JS on the card leads with the summary and that block is
/// hidden. [Story 20.3]</summary>
public sealed record RelatedCard(
    string IslandId,
    string Title,
    string KindWord,
    string Summary,
    string? PrimaryCommand,
    string? DetailHref,
    RelatedWorkNode Relationships);

/// <summary>The rail's default card, shown when nothing is selected: top-level project details plus a prompt to
/// pick a node. [Story 20.3]</summary>
public sealed record RelatedProjectCard(
    string Title,
    string Summary,
    string? PrimaryCommand,
    string Hint);

/// <summary>The whole details rail: the default project card + one <see cref="RelatedCard"/> per selectable scope
/// that has related work, plus the work graph's own honestly-reported draw overflow
/// (<see cref="RelatedWorkModel.Overflow"/>) carried through so the rail can disclose it rather than silently
/// dropping it (Story 20.1 spike §1a rule 5). <see cref="IsEmpty"/> omits the rail entirely (NFR8) — the same gate
/// the pane used before the card redesign. [Story 20.3]</summary>
public sealed record RelatedWorkPaneModel(
    RelatedProjectCard Project,
    IReadOnlyList<RelatedCard> Cards,
    string? WorkGraphHref,
    int Overflow = 0)
{
    // Overflow alone (no cards) is still real information to disclose — the omit gate must not silently swallow
    // it the way it swallows a genuinely empty projection. [Story 20.3 review]
    public bool IsEmpty => Cards.Count == 0 && Overflow == 0;
}

/// <summary>Joins the pure <see cref="RelatedWork"/> relationship projection to the domain models
/// (<see cref="EpicsModel"/>, <see cref="CommandCatalog"/>, <see cref="FollowUpGeometry"/>, <see cref="ProjectCounts"/>)
/// to produce the details-rail cards: each scope's name, summary, and single most-relevant BMad command. The
/// relationship half stays a pure read of the cached work graph; this layer adds only the per-node title/status and
/// the primary command (both already computed elsewhere — reused, never re-derived). [Story 20.3]</summary>
public static class RelatedWorkCards
{
    public static RelatedWorkPaneModel Build(
        RelatedWorkModel relationships,
        EpicsModel? epics,
        CommandCatalog commands,
        FollowUpGeometry geometry,
        ProjectCounts counts,
        string projectTitle,
        string? workGraphHref,
        IReadOnlyList<string>? selectableIslandIds = null)
    {
        var project = new RelatedProjectCard(
            projectTitle,
            ProjectSummary(counts),
            epics is not null ? BmadCommands.PrimaryProjectCommand(epics, commands) : null,
            "Select a node in the chart for its related work, or open the full work graph.");

        if (relationships.IsEmpty || epics is null)
            return new RelatedWorkPaneModel(project, Array.Empty<RelatedCard>(), workGraphHref, relationships.Overflow);

        var epicsByNumber = epics.Epics.ToDictionary(e => e.Number);
        var storiesById = epics.Epics.SelectMany(e => e.Stories).GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // A card exists for everything the explorer can SELECT — and after Story 20.5 that includes story leaves.
        //
        // Story 20.3 deliberately gave stories no card of their own, and stated the precondition for that in the
        // same breath: "a story wedge is a leaf that NAVIGATES to its own page on click (Story 20.2's model), so a
        // standalone story card would never be reachable via selection." Story 20.5's `select` mode is exactly what
        // makes it reachable — activating a story leaf now raises `specscribe:explorer-select` carrying its id and
        // navigates nowhere. The owner's 2026-07-25 verify round confirmed the consequence from the other side:
        // selecting a story showed nothing of value, which broke the whole point of select mode (find the work you
        // want, copy its command, go).
        //
        // So the fold is gone rather than kept alongside a story card: a story's relationships now live on the
        // story's own card, once. Keeping both would put every story under its epic AND under itself, which is
        // precisely the duplication 20.3's fold existed to avoid — the JS-off stacked view shows every card.
        // [Story 20.5, owner-directed 2026-07-25; supersedes the Story 20.3 owner redesign 2026-07-24]
        var cards = new List<RelatedCard>();
        var byIslandId = relationships.Nodes.ToDictionary(n => n.IslandId, StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);

        // Draw order first, so the rail's card order matches the chart's — and so a selectable node with NO
        // work-graph relationships still gets a card. That last part is the actual fix: story 23.2 had no edges, so
        // under the old projection it had no node, no card, and the rail's designed empty state. A story with no
        // relationships still has a name, a status, a page and a next command; "no related items" is a fact about
        // its edges, never a reason to show the reader nothing.
        foreach (var islandId in selectableIslandIds ?? Array.Empty<string>())
        {
            if (!placed.Add(islandId)) continue;
            var node = byIslandId.TryGetValue(islandId, out var known)
                ? known
                : SynthesizeNode(islandId, epicsByNumber, storiesById);
            if (node is null) continue;
            var (title, kindWord, summary, command) = Resolve(node, epicsByNumber, storiesById, commands, geometry);
            cards.Add(new RelatedCard(islandId, title, kindWord, summary, command, node.Href, node));
        }

        // Anything the graph knows about that the chart did not draw in this pass — kept so the JS-off stacked view
        // never silently loses a relationship set that used to be reachable.
        foreach (var node in relationships.Nodes)
        {
            if (!placed.Add(node.IslandId)) continue;
            var (title, kindWord, summary, command) = Resolve(node, epicsByNumber, storiesById, commands, geometry);
            cards.Add(new RelatedCard(node.IslandId, title, kindWord, summary, command, node.Href, node));
        }

        return new RelatedWorkPaneModel(project, cards, workGraphHref, relationships.Overflow);
    }

    /// <summary>A card for a selectable node the work graph has no entry for — a story or epic the chart drew but
    /// that has no edges. It carries no relationships (there are none), which is the honest state; everything else
    /// a reader needs is on the domain object. Returns null for an id that maps to no domain object at all.
    /// [Story 20.5]</summary>
    private static RelatedWorkNode? SynthesizeNode(
        string islandId,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        IReadOnlyDictionary<string, StoryInfo> storiesById)
    {
        if (storiesById.TryGetValue(islandId, out var story))
        {
            var href = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
            return new RelatedWorkNode(
                islandId, $"Story {story.Id}", WorkNodeKind.Story, href,
                RelatedWork.AnchorForIslandId(islandId),
                Array.Empty<RelatedWorkGroup>(), Array.Empty<RelatedWorkSubject>());
        }
        if (islandId.StartsWith("epic-", StringComparison.Ordinal)
            && int.TryParse(islandId.AsSpan("epic-".Length), out var n)
            && epicsByNumber.TryGetValue(n, out var epic))
        {
            return new RelatedWorkNode(
                islandId, $"Epic {epic.Number}", WorkNodeKind.Epic, $"epics/epic-{epic.Number}.html",
                RelatedWork.AnchorForIslandId(islandId),
                Array.Empty<RelatedWorkGroup>(), Array.Empty<RelatedWorkSubject>());
        }
        // An aggregate/summary wedge (`epic-7~open`, `epic-7~summary`) is a roll-up of work the rail already shows
        // under its parent — no card of its own, so selecting one falls through to the designed empty state.
        return null;
    }

    /// <summary>Resolves a card's title/kind/summary/command for the three selectable shapes: an epic scope, a
    /// STORY leaf (Story 20.5's select mode made these reachable), or a relationship-only scope such as the
    /// Unattributed root.</summary>
    private static (string Title, string KindWord, string Summary, string? Command) Resolve(
        RelatedWorkNode node,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        IReadOnlyDictionary<string, StoryInfo> storiesById,
        CommandCatalog commands,
        FollowUpGeometry geometry)
    {
        // Story leaf: full title, its real status and task progress, and the ONE command that actually moves it
        // forward — the same BmadCommands.PrimaryStoryCommand the story page and the VS Code outline already use,
        // so the rail can never suggest a different next step than the rest of the portal. That command badge is
        // the copy-to-clipboard button this whole surface exists for.
        if (storiesById.TryGetValue(node.IslandId, out var story))
        {
            var title = $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}";
            var stage = StatusStyles.ForStory(story);
            var progress = story.TasksTotal == 0
                ? "no task plan yet"
                : $"{story.TasksDone} of {story.TasksTotal} tasks done";
            var openDeferred = geometry.DeferredForSource(story.Id)?.Where(IsOpen).ToList();
            var summary = $"{StatusStyles.StoryLabel(stage)} · {progress}";
            return (title, "Story", summary, BmadCommands.PrimaryStoryCommand(story, commands, openDeferred));
        }

        // Epic scope: full title, story count + open follow-up count, the epic's primary next-step command.
        if (node.IslandId.StartsWith("epic-", StringComparison.Ordinal)
            && int.TryParse(node.IslandId.AsSpan("epic-".Length), out var epicNumber)
            && epicsByNumber.TryGetValue(epicNumber, out var epic))
        {
            var title = $"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)}";
            var stories = epic.Stories.Count;
            var open = OpenFollowUpCount(epic.Number, geometry);
            var summary = $"{stories} {Charts.Plural(stories, "story", "stories")}"
                + (open > 0 ? $" · {open} open follow-{(open == 1 ? "up" : "ups")}" : string.Empty);
            var openDeferred = geometry.DeferredForEpicNumber(epic.Number).Where(IsOpen).ToList();
            return (title, "Epic", summary, BmadCommands.PrimaryEpicCommand(epic, commands, openDeferred));
        }

        // The Unattributed root, or any scope with no domain object: summarize from the relationship groups. No AI
        // action — there is no single artifact to act on.
        var items = node.EntryCount;
        return (node.Label, "Follow-ups",
            $"{items} related {Charts.Plural(items, "item", "items")} with no epic", null);
    }

    private static string ProjectSummary(ProjectCounts c)
    {
        var open = c.DeferredOpenItems + c.OpenActionItems;
        var parts = new List<string>
        {
            $"{c.EpicsDefined} {Charts.Plural(c.EpicsDefined, "epic", "epics")}",
            $"{c.StoriesDefined} {Charts.Plural(c.StoriesDefined, "story", "stories")}",
        };
        if (open > 0) parts.Add($"{open} open follow-{(open == 1 ? "up" : "ups")}");
        return string.Join(" · ", parts);
    }

    private static int OpenFollowUpCount(int epicNumber, FollowUpGeometry geometry) =>
        geometry.DeferredForEpicNumber(epicNumber).Count(IsOpen)
        + geometry.ActionItems.Count(a => a.EpicNumber == epicNumber && !FollowUpGeometry.IsDone(a));

    private static bool IsOpen(FollowUpDeferredSlot slot) => !slot.Item.Resolved;
}
