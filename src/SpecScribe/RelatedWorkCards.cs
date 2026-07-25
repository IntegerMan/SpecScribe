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
        string? workGraphHref)
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

        // A card exists only for what the explorer can actually SELECT — the zoom scopes (epics + the orphan/unplanned
        // roots). A story wedge is a leaf that NAVIGATES to its own page on click (Story 20.2's model), so a standalone
        // story card would never be reachable via selection. Each story's relationships are instead folded into its
        // epic's card as a labelled subject, so the epic you drill into carries the full "what stemmed from what" —
        // and the JS-off stacked view stays complete (AC #2) without duplicating a story under both its epic and
        // itself. [Story 20.3 — owner redesign 2026-07-24]
        var storySubjectsByEpic = new Dictionary<string, List<RelatedWorkSubject>>(StringComparer.Ordinal);
        // Dictionary enumeration order is an implementation detail (same rule RelatedWork.Build follows for
        // nodeOrder/FR31), so first-seen epic order is carried explicitly rather than trusted to the dictionary.
        var storySubjectEpicOrder = new List<string>();
        foreach (var node in relationships.Nodes)
        {
            if (!storiesById.ContainsKey(node.IslandId)) continue; // not a bare story id → handled as a scope below
            var dot = node.IslandId.IndexOf('.');
            if (dot <= 0) continue;
            var epicIslandId = $"epic-{node.IslandId[..dot]}";
            // Drop the story's own outgoing "Part of → Epic N": it restates the card it now lives inside.
            var groups = node.Groups.Where(g => !RelatedWork.IsRestatedContainsGroup(g)).ToList();
            if (groups.Count == 0) continue;
            if (!storySubjectsByEpic.TryGetValue(epicIslandId, out var list))
            {
                list = storySubjectsByEpic[epicIslandId] = new List<RelatedWorkSubject>();
                storySubjectEpicOrder.Add(epicIslandId);
            }
            list.Add(new RelatedWorkSubject(node.Label, node.Kind, node.Href, groups));
        }

        var cards = new List<RelatedCard>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in relationships.Nodes)
        {
            if (storiesById.ContainsKey(node.IslandId)) continue; // stories fold into their epic, no card of their own
            var extra = storySubjectsByEpic.TryGetValue(node.IslandId, out var s) ? s : null;
            var enriched = extra is null ? node : node with { Subjects = node.Subjects.Concat(extra).ToList() };
            var (title, kindWord, summary, command) = Resolve(enriched, epicsByNumber, commands, geometry);
            cards.Add(new RelatedCard(node.IslandId, title, kindWord, summary, command, node.Href, enriched));
            placed.Add(node.IslandId);
        }

        // An epic that has story relationships but no scope node of its own (its own Contains group was empty) still
        // needs a card to host those stories, or the JS-off view would drop them (AC #2). Rare, but real.
        foreach (var epicIslandId in storySubjectEpicOrder)
        {
            if (placed.Contains(epicIslandId)) continue;
            var subjects = storySubjectsByEpic[epicIslandId];
            if (!int.TryParse(epicIslandId.AsSpan("epic-".Length), out var n) || !epicsByNumber.TryGetValue(n, out var epic))
                continue;
            var host = new RelatedWorkNode(
                epicIslandId, $"Epic {epic.Number}", WorkNodeKind.Epic,
                $"epics/epic-{epic.Number}.html", RelatedWork.AnchorForIslandId(epicIslandId),
                Array.Empty<RelatedWorkGroup>(), subjects);
            var (title, kindWord, summary, command) = Resolve(host, epicsByNumber, commands, geometry);
            cards.Add(new RelatedCard(epicIslandId, title, kindWord, summary, command, host.Href, host));
        }

        return new RelatedWorkPaneModel(project, cards, workGraphHref, relationships.Overflow);
    }

    /// <summary>Resolves an epic-scope card's title/kind/summary/command. The card set is keyed to what the
    /// explorer can actually SELECT (epics + the orphan/unplanned roots) — a bare story id never reaches here,
    /// since both call sites skip/never construct one (stories fold into their epic's card instead). [Story 20.3
    /// owner redesign]</summary>
    private static (string Title, string KindWord, string Summary, string? Command) Resolve(
        RelatedWorkNode node,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        CommandCatalog commands,
        FollowUpGeometry geometry)
    {
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
