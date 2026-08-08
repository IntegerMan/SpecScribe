namespace SpecScribe;

/// <summary>One open deferred / action child of a story, as the details rail lists it: the child's own name and a
/// link to its follow-up page when it has one. A child with no generated page renders as plain text rather than a
/// dead <c>&lt;a&gt;</c> (Epic 7's guarded-href discipline). [Story 20.8 D2]</summary>
public sealed record RelatedCardChild(string Label, string? Href);

/// <summary>One selection's card in the Story 20.3 details rail — the "fancy card" that augments a chart selection:
/// the node's name, a one-line summary of what it holds, a single most-relevant AI action, and a link to its full
/// detail page. <see cref="Relationships"/> carries the work-graph groups that render as the JS-off fallback (the
/// AC #2 / NFR8 server-rendered relationship block); with JS on the card leads with the summary and that block is
/// hidden. [Story 20.3]
///
/// <para><b>Story 20.8 D1/D2/D3 added three fields and emptied one.</b> A STORY card's
/// <see cref="Relationships"/> is now deliberately empty — its groups are folded into its epic's card as a
/// <see cref="RelatedWorkSubject"/>, once (D1) — while <see cref="MoreCommands"/> and <see cref="Children"/> make
/// that card richer in the two ways that cost bytes linearly rather than quadratically (D2).
/// <see cref="Aliases"/> is the "selecting X shows Y's card" redirect (D3): additional payload ids this card
/// answers for, decided here in C# so the script never string-munges an id into another id.</para></summary>
/// <param name="MoreCommands">The status-gated command set MINUS the visible primary — rendered behind a collapsed
/// native <c>&lt;details&gt;</c>. Never repeats <paramref name="PrimaryCommand"/> (the "EpicEpic 19" duplication
/// class Story 20.3's live round caught), and empty means no disclosure at all rather than a dead control.</param>
/// <param name="Children">The story's OPEN deferred children, by name. Capped; see
/// <paramref name="HiddenChildren"/>.</param>
/// <param name="HiddenChildren">How many children the cap withheld — stated as "+N more" rather than truncated
/// silently (NFR8).</param>
/// <param name="Aliases">Payload ids that resolve to THIS card instead of one of their own — today only
/// <c>epic-N~summary</c> → <c>epic-N</c> (D3). Emitted as a DOM attribute so the client matches on data the server
/// decided, never on a second string rule.</param>
public sealed record RelatedCard(
    string IslandId,
    string Title,
    string KindWord,
    string Summary,
    string? PrimaryCommand,
    string? DetailHref,
    RelatedWorkNode Relationships,
    IReadOnlyList<OutlineStoryCommand>? MoreCommands = null,
    IReadOnlyList<RelatedCardChild>? Children = null,
    int HiddenChildren = 0,
    IReadOnlyList<string>? Aliases = null);

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
    /// <summary>How many open deferred children a story card lists before it says "+N more". A story with a long
    /// tail of follow-ups is exactly the story whose card must stay scannable; the remainder is STATED, and the
    /// card's "View details" link reaches the full set. [Story 20.8 D2]</summary>
    public const int MaxChildrenPerCard = 5;

    public static RelatedWorkPaneModel Build(
        RelatedWorkModel relationships,
        EpicsModel? epics,
        CommandCatalog commands,
        FollowUpGeometry geometry,
        ProjectCounts counts,
        string projectTitle,
        string? workGraphHref,
        IReadOnlyList<SunburstExplorerNode>? selectableNodes = null)
    {
        var project = new RelatedProjectCard(
            projectTitle,
            ProjectSummary(counts),
            epics is not null ? BmadCommands.PrimaryProjectCommand(epics, commands) : null,
            "Select a node in the chart for its related work, or open the full work graph.");

        if (relationships.IsEmpty || epics is null)
            return new RelatedWorkPaneModel(project, Array.Empty<RelatedCard>(), workGraphHref, relationships.Overflow);

        var epicsByNumber = epics.Epics.ByFirst(e => e.Number);
        var storiesById = epics.Epics.SelectMany(e => e.Stories).GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var byIslandId = relationships.Nodes.ToDictionary(n => n.IslandId, StringComparer.Ordinal);

        // ---- D1: the story -> epic relationship fold, restored --------------------------------------------
        //
        // Story 20.3 folded a story's relationship groups into its epic's card. Story 20.5's owner-verify round
        // removed the fold, because `select` mode had just made a story leaf selectable and a story card that
        // showed nothing to act on was the defect being fixed. That reversal was right on the information then
        // available and wrong on the measurement that arrived afterwards: with the fold gone the rail rendered
        // EVERY relationship twice — once under the story, once under its epic — and grew to 443,137 B of an
        // 878,971 B dashboard (50.4%), 187 cards, 160 of them stories, 372 relationship rows.
        //
        // So the fold comes back and the story card goes minimal (its groups move here; its title, summary,
        // command affordance and "View details" link stay). Removing the DUPLICATION is the honest fix — the
        // alternative on the table, dropping `RelatedWork.MaxEntriesPerGroup` from 12, would have traded away
        // JS-off completeness for every node including epics to pay for a duplication that should not exist.
        // A reader who selects a story reaches its relationships through its epic's card, its own page, or
        // work-graph.html — all one click, all stated. [Story 20.8 D1, owner-locked 2026-07-25]
        var subjectsByEpic = new Dictionary<string, List<RelatedWorkSubject>>(StringComparer.Ordinal);
        // Explicit first-seen host order. Dictionary enumeration order is not a contract and the golden
        // fingerprint depends on this (FR31), so the order is carried by a list, exactly as RelatedWork.Build
        // carries its own `nodeOrder`.
        var epicFoldOrder = new List<string>();
        foreach (var node in relationships.Nodes)
        {
            var host = FoldHostFor(node.IslandId);
            if (host is null) continue;
            // A story sitting inside its epic's card restates "Part of -> Epic N" as the heading above it. Drop
            // it with the SHARED test rather than a second rule. [Story 20.3]
            var meaningful = node.Groups.Where(g => !RelatedWork.IsRestatedContainsGroup(g)).ToList();
            // A wedged story can itself already carry upstream-folded Subjects (RelatedWork.Build's own separate
            // ancestor fold, when this story was the nearest wedged ancestor of an unwedged deferred/action/spec
            // item). Those must ride along when the story's own section collapses into its epic here — dropping
            // them would silently discard real relationship data that would otherwise have nowhere left to live.
            if (meaningful.Count == 0 && node.Subjects.Count == 0) continue;
            if (!subjectsByEpic.TryGetValue(host, out var list))
            {
                subjectsByEpic[host] = list = new List<RelatedWorkSubject>();
                epicFoldOrder.Add(host);
            }
            if (meaningful.Count > 0)
                list.Add(new RelatedWorkSubject(
                    StoryTitle(node.IslandId, storiesById) ?? node.Label, node.Kind, node.Href, meaningful));
            list.AddRange(node.Subjects);
        }

        // ---- D3: canonicalize the selectable set before any card is built ---------------------------------
        //
        // `epic-N~summary` is the epic restated — same href, same content — so it gets no card of its own and
        // resolves to its parent epic's instead. The synthesized project root is the whole view, which the rail
        // already represents as its no-selection project card. Both redirects are decided HERE, in C#, and
        // published as data (`data-related-alias`), so the client and the server can never disagree about them.
        var orderedIds = new List<string>();
        var payloadById = new Dictionary<string, SunburstExplorerNode>(StringComparer.Ordinal);
        var aliasesByCanonical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var payload in selectableNodes ?? Array.Empty<SunburstExplorerNode>())
        {
            if (payload.Id == HierarchyExplorer.ProjectRootId) continue; // -> the project card, by absence
            var canonical = CanonicalIslandId(payload.Id);
            if (!string.Equals(canonical, payload.Id, StringComparison.Ordinal))
            {
                if (!aliasesByCanonical.TryGetValue(canonical, out var aliases))
                    aliasesByCanonical[canonical] = aliases = new List<string>();
                aliases.Add(payload.Id);
                continue;
            }
            if (payloadById.ContainsKey(payload.Id)) continue;
            payloadById[payload.Id] = payload;
            orderedIds.Add(payload.Id);
        }

        var cards = new List<RelatedCard>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        // Draw order first, so the rail's card order matches the chart's — and so a selectable node with NO
        // work-graph relationships still gets a card. That last part is the actual fix Story 20.5 landed: story
        // 23.2 had no edges, so under the old projection it had no node, no card, and the rail's designed empty
        // state. A story with no relationships still has a name, a status, a page and a next command; "no related
        // items" is a fact about its edges, never a reason to show the reader nothing.
        foreach (var islandId in orderedIds)
        {
            if (!placed.Add(islandId)) continue;
            var card = BuildCard(
                islandId, payloadById[islandId], byIslandId, subjectsByEpic,
                epicsByNumber, storiesById, commands, geometry);
            if (card is null) continue;
            cards.Add(aliasesByCanonical.TryGetValue(islandId, out var aliases)
                ? card with { Aliases = aliases }
                : card);
        }

        // Anything the graph knows about that the chart did not draw in this pass — kept so the JS-off stacked
        // view never silently loses a relationship set that used to be reachable. STORY nodes are skipped
        // outright: after D1 a story's groups live on its epic's card and nowhere else, so re-adding an undrawn
        // story here would reintroduce exactly the duplication the fold exists to remove. (Note this touches only
        // WEDGED stories — `RelatedWork.Build` already folds UNWEDGED ones into their ancestor as subjects before
        // the projection reaches this layer, which is a different fold in a different file.)
        foreach (var node in relationships.Nodes)
        {
            if (FoldHostFor(node.IslandId) is not null) continue;
            if (!placed.Add(node.IslandId)) continue;
            var card = BuildCard(
                node.IslandId, payload: null, byIslandId, subjectsByEpic,
                epicsByNumber, storiesById, commands, geometry);
            if (card is null) continue;
            cards.Add(aliasesByCanonical.TryGetValue(node.IslandId, out var aliases)
                ? card with { Aliases = aliases }
                : card);
        }

        // The fallback pass D1 requires: an epic that hosts folded story subjects but has no card of its own —
        // because the chart did not draw its wedge in this pass AND the work graph carries no node for the epic
        // itself (every edge rooted through a story). Without this the whole fold would be dropped on the floor,
        // which is the duplication fix quietly turning into a data-loss bug. Iterated over `epicFoldOrder`, not
        // over the dictionary, for FR31.
        foreach (var host in epicFoldOrder)
        {
            if (!placed.Add(host)) continue;
            var card = BuildCard(
                host, payload: null, byIslandId, subjectsByEpic,
                epicsByNumber, storiesById, commands, geometry);
            if (card is null) continue;
            cards.Add(aliasesByCanonical.TryGetValue(host, out var aliases)
                ? card with { Aliases = aliases }
                : card);
        }

        return new RelatedWorkPaneModel(project, cards, workGraphHref, relationships.Overflow);
    }

    /// <summary>The card id a payload id resolves to. Identity for everything except <c>epic-N~summary</c>, the
    /// dense-epic roll-up wedge whose href IS its epic's page and whose content would be its epic's card restated —
    /// so selecting it shows the epic's card rather than a near-duplicate or the empty state.
    ///
    /// <para>Exposed <c>internal</c> so the completeness test (Story 20.8 Task 3.4) asserts the redirect against
    /// this one rule rather than re-implementing it. [Story 20.8 D3]</para></summary>
    internal static string CanonicalIslandId(string islandId)
    {
        if (!islandId.StartsWith("epic-", StringComparison.Ordinal)) return islandId;
        var tilde = islandId.IndexOf('~');
        return tilde > 0 && islandId.AsSpan(tilde + 1).SequenceEqual("summary")
            ? islandId[..tilde]
            : islandId;
    }

    /// <summary>The epic card a story's relationship groups fold into (<c>20.5</c> → <c>epic-20</c>), or null when
    /// the id is not a story id. The derivation is <c>IslandId.IndexOf('.')</c> — the SAME one
    /// <see cref="RelatedWork.AncestorIslandIdFor"/> and <see cref="RelatedWorkTemplater"/> already use; a second
    /// derivation here would be a second thing to keep in step. [Story 20.8 D1]</summary>
    private static string? FoldHostFor(string islandId)
    {
        var dot = islandId.IndexOf('.');
        return dot > 0 ? $"epic-{islandId[..dot]}" : null;
    }

    /// <summary>A readable label for a fold host id that resolved to no <see cref="EpicInfo"/> — the same shape
    /// <c>RelatedWork.HostLabel</c> uses for its own orphaned-host fallback.</summary>
    private static string HostLabelFor(string islandId) =>
        islandId.StartsWith("epic-", StringComparison.Ordinal) ? "Epic " + islandId[5..] : islandId;

    private static string? StoryTitle(string islandId, IReadOnlyDictionary<string, StoryInfo> storiesById) =>
        storiesById.TryGetValue(islandId, out var story)
            ? $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}"
            : null;

    /// <summary>One card, for either a drawn selectable node (<paramref name="payload"/> non-null) or a graph node
    /// the chart did not draw. Returns null when the id resolves to no card at all — which, after D3, means only an
    /// id with neither a domain object, a work-graph node, nor a payload entry.</summary>
    private static RelatedCard? BuildCard(
        string islandId,
        SunburstExplorerNode? payload,
        IReadOnlyDictionary<string, RelatedWorkNode> byIslandId,
        IReadOnlyDictionary<string, List<RelatedWorkSubject>> subjectsByEpic,
        IReadOnlyDictionary<int, EpicInfo> epicsByNumber,
        IReadOnlyDictionary<string, StoryInfo> storiesById,
        CommandCatalog commands,
        FollowUpGeometry geometry)
    {
        byIslandId.TryGetValue(islandId, out var known);

        // ---- A STORY leaf. D1: no relationship block at all; D2: the full command set + its open children. ----
        if (storiesById.TryGetValue(islandId, out var story))
        {
            var stage = StatusStyles.ForStory(story);
            var progress = story.TasksTotal == 0
                ? "no task plan yet"
                : $"{story.TasksDone} of {story.TasksTotal} tasks done";
            // ONE list, computed once, feeding BOTH the command gating and the children list — the round-2 code
            // already computed exactly this to feed PrimaryStoryCommand.
            var openDeferred = geometry.DeferredForSource(story.Id)?.Where(IsOpen).ToList();
            var primary = BmadCommands.PrimaryStoryCommand(story, commands, openDeferred);
            var (children, hidden) = ChildrenOf(openDeferred);

            return new RelatedCard(
                islandId,
                $"Story {story.Id}: {PathUtil.StripHtmlTags(story.Title)}",
                "Story",
                // The SAME sentence HierarchyExplorer.WithDetails puts in HierarchyNode.Detail and the chart's own
                // tooltip. A third phrasing of a story's progress is a defect, not a nuance.
                $"{StatusStyles.StoryLabel(stage)} · {progress}",
                primary,
                known?.Href ?? story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id),
                // D1: EMPTY. RelatedWorkTemplater.RenderCard already guards on
                // `Groups.Count > 0 || Subjects.Count > 0`, so the <details class="related-card-full"> block
                // simply does not render — no templater branch, which would be a second rule to keep in sync.
                EmptyRelationships(islandId, $"Story {story.Id}", WorkNodeKind.Story),
                MoreCommands: MoreCommandsFor(story, commands, openDeferred, primary),
                Children: children,
                HiddenChildren: hidden);
        }

        // ---- An EPIC scope: its own groups plus the story subjects folded into it (D1). ----
        if (islandId.StartsWith("epic-", StringComparison.Ordinal)
            && int.TryParse(islandId.AsSpan("epic-".Length), out var epicNumber)
            && epicsByNumber.TryGetValue(epicNumber, out var epic))
        {
            var rel = known ?? EmptyRelationships(islandId, $"Epic {epic.Number}", WorkNodeKind.Epic);
            if (subjectsByEpic.TryGetValue(islandId, out var folded))
                rel = rel with { Subjects = rel.Subjects.Concat(folded).ToList() };

            var stories = epic.Stories.Count;
            var open = OpenFollowUpCount(epic.Number, geometry);
            var openDeferred = geometry.DeferredForEpicNumber(epic.Number).Where(IsOpen).ToList();
            return new RelatedCard(
                islandId,
                $"Epic {epic.Number}: {PathUtil.StripHtmlTags(epic.Title)}",
                "Epic",
                $"{stories} {Charts.Plural(stories, "story", "stories")}"
                    + (open > 0 ? $" · {open} open follow-{(open == 1 ? "up" : "ups")}" : string.Empty),
                BmadCommands.PrimaryEpicCommand(epic, commands, openDeferred),
                rel.Href ?? $"epics/epic-{epic.Number}.html",
                rel);
        }

        // ---- A scope the work graph knows but no domain object does: the Unattributed root. Unchanged. ----
        if (known is not null)
        {
            var rel = known;
            if (subjectsByEpic.TryGetValue(islandId, out var folded))
                rel = rel with { Subjects = rel.Subjects.Concat(folded).ToList() };
            // Count AFTER folding — a phantom "epic-N" whose number matches no current EpicInfo but still carries
            // a work-graph node lands here too, and its folded story subjects must be reflected in the count the
            // reader sees, not just in the rendered block underneath it.
            var items = rel.EntryCount;
            return new RelatedCard(
                islandId, known.Label, "Follow-ups",
                $"{items} related {Charts.Plural(items, "item", "items")} with no epic",
                null, known.Href, rel);
        }

        // ---- A fold host with no EpicInfo AND no work-graph node of its own: an epic number that no longer
        // matches any current epic, but still has story subjects folded onto it (D1). Materialize a minimal card
        // — the same "don't drop content with nowhere else to live" reasoning RelatedWork.Build already applies
        // via its own HostLabel fallback — rather than falling through to `payload is null` and discarding the
        // whole fold silently. ----
        if (subjectsByEpic.TryGetValue(islandId, out var orphanFolded))
        {
            var rel = EmptyRelationships(islandId, HostLabelFor(islandId), WorkNodeKind.Epic)
                with { Subjects = orphanFolded };
            var items = rel.EntryCount;
            return new RelatedCard(
                islandId, HostLabelFor(islandId), "Follow-ups",
                $"{items} related {Charts.Plural(items, "item", "items")} with no matching epic",
                null, null, rel);
        }

        // ---- D3: the follow-up AGGREGATES and the `unplanned` root. ----
        //
        // These are the wedges that had no card at all: `epic-N~open` / `~done`, `orphan~open` / `~done`,
        // `unplanned~open` / `~done`, and — the gap neither Story 20.3 nor 20.5 noticed, live on reviewed code —
        // the `unplanned` ROOT itself, because `RelatedWork.IslandIdFor` maps only Epic and Story kinds. Measured
        // on this portal before the fix: 25 of 212 payload nodes selected to the rail's empty state.
        //
        // The label is the PAYLOAD's, taken verbatim and never re-composed: the explorer breadcrumb and the chart's
        // own accessible name use that exact wording, and a drift here reads as two names for one wedge.
        if (payload is null) return null;
        return new RelatedCard(
            islandId,
            payload.Label,
            islandId.StartsWith("unplanned", StringComparison.Ordinal) ? "Unplanned" : "Follow-ups",
            // The label already carries the count ("Epic 7: 3 open follow-ups") — which is exactly why
            // HierarchyExplorer.WithDetails leaves `Detail` empty for these kinds. So the summary states the
            // STATUS in prose instead of restating the number. UX-DR17/19: a word, never colour alone.
            Charts.SunburstLocalStatusLabel(payload.StatusClass) ?? "Follow-up group",
            // No single artifact to act on, so no command — the same rule the Unattributed root already follows.
            PrimaryCommand: null,
            DetailHref: payload.Href is { Length: > 0 } href && href != "#" ? href : null,
            EmptyRelationships(islandId, payload.Label, WorkNodeKind.Epic));
    }

    /// <summary>A relationship node carrying no groups and no subjects — the honest shape for a card whose
    /// relationships live somewhere else (a story, after D1) or do not exist (an aggregate).</summary>
    private static RelatedWorkNode EmptyRelationships(string islandId, string label, WorkNodeKind kind) =>
        new(islandId, label, kind, null, RelatedWork.AnchorForIslandId(islandId),
            Array.Empty<RelatedWorkGroup>(), Array.Empty<RelatedWorkSubject>());

    /// <summary>The story's command set MINUS the one already shown as the visible primary badge — D2's
    /// "more command, not more relationships".
    ///
    /// <para>Filtered by VALUE rather than by <c>Skip(1)</c>, and that matters at the edges: for a done story
    /// <see cref="BmadCommands.PrimaryStoryCommand"/> returns null while
    /// <see cref="BmadCommands.StoryCommands"/> still returns a muted <c>correct-course</c> escape hatch, so
    /// <c>Skip(1)</c> would silently swallow the only entry there is. Exactly one occurrence is removed, so a
    /// catalog that legitimately repeated a command would not lose both. [Story 20.8 D2]</para></summary>
    private static IReadOnlyList<OutlineStoryCommand> MoreCommandsFor(
        StoryInfo story,
        CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred,
        string? primary)
    {
        var all = BmadCommands.StoryCommands(story, commands, openDeferred);
        if (all.Count == 0) return Array.Empty<OutlineStoryCommand>();

        var more = new List<OutlineStoryCommand>(all.Count);
        var removedPrimary = false;
        foreach (var entry in all)
        {
            if (!removedPrimary && primary is not null
                && string.Equals(entry.Command, primary, StringComparison.Ordinal))
            {
                removedPrimary = true;
                continue;
            }
            more.Add(entry);
        }
        return more;
    }

    /// <summary>A story's open deferred children by name, capped, with the remainder counted. The label is
    /// <see cref="FollowUpRow.SummarizeFromHtml"/> — the SAME helper the work graph names a deferred node with, so
    /// the rail and the graph cannot show one item under two names. [Story 20.8 D2]</summary>
    private static (IReadOnlyList<RelatedCardChild> Children, int Hidden) ChildrenOf(
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred)
    {
        if (openDeferred is null || openDeferred.Count == 0)
            return (Array.Empty<RelatedCardChild>(), 0);

        var children = openDeferred
            .Take(MaxChildrenPerCard)
            .Select(s => new RelatedCardChild(
                FollowUpRow.SummarizeFromHtml(s.Item.BodyHtml, 90),
                s.DetailHref is { Length: > 0 } ? s.DetailHref : null))
            .ToList();
        return (children, Math.Max(0, openDeferred.Count - MaxChildrenPerCard));
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
