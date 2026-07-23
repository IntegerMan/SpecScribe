using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The kind of a work-graph node — determines its drawn shape + label prefix (never a
/// lifecycle <c>--status-*</c> fill). Bounded to the derivable-today provenance vocabulary Story 19.1
/// locked; <c>covers</c>/requirement and <c>cites</c>/code nodes are deliberately out of the MVP draw
/// (see Story 19.2 Dev Notes). [Story 19.2; 19.1 §1]</summary>
public enum WorkNodeKind
{
    Epic,
    Story,
    Deferred,
    Action,
    /// <summary>A quick-dev / <c>spec-*</c> one-shot, or any non-story provenance source/resolver.</summary>
    Spec,
    /// <summary>An epic retrospective — the (soft) target of a <c>raised-in</c> cross-link.</summary>
    Retro,
}

/// <summary>A directed work-graph edge kind. Direction is always <em>carrier → target</em> (the node that
/// physically holds the reference points at the node referenced) — Story 19.1's single locked convention.
/// [Story 19.2; 19.1 §2]</summary>
public enum WorkEdgeKind
{
    /// <summary>Structural attribution/containment: story ∈ epic, or an otherwise-unrooted follow-up ∈ epic.</summary>
    Contains,
    /// <summary>A deferred item arose because of its source story/spec/quick-dev.</summary>
    StemmedFrom,
    /// <summary>A deferred item is closed by its resolving story/spec.</summary>
    Resolves,
    /// <summary>An action item's obligation also surfaced in another epic's retro (soft, heuristic).</summary>
    RaisedIn,
}

/// <summary>One node in an epic-scoped work graph. <see cref="Href"/> is null when no generated page exists
/// for the target (rendered as a non-link chip, mirroring Epic 7's guarded-href discipline). <see cref="Title"/>
/// carries the full hover/aria text when <see cref="Label"/> is truncated for the drawn geometry. [Story 19.2]</summary>
public sealed record WorkNode(WorkNodeKind Kind, string Id, string Label, string? Href, string? Title = null);

/// <summary>One directed edge (carrier <see cref="FromId"/> → target <see cref="ToId"/>). [Story 19.2; 19.1 §2]</summary>
public sealed record WorkEdge(string FromId, string ToId, WorkEdgeKind Kind);

/// <summary>The provenance subgraph for a single epic: its nodes + directed edges, plus any simple directed
/// cycles found over that edge set (empty when the scope is acyclic — the honest common case). <see cref="Overflow"/>
/// counts follow-ups elided by the per-epic draw cap; they still appear in the templater's sr-only list.
/// [Story 19.2]</summary>
public sealed record WorkGraphEpic(
    int EpicNumber,
    string EpicTitle,
    IReadOnlyList<WorkNode> Nodes,
    IReadOnlyList<WorkEdge> Edges,
    IReadOnlyList<IReadOnlyList<string>> Cycles,
    int Overflow = 0,
    IReadOnlyList<string>? OverflowLabels = null)
{
    /// <summary>Plain-text labels for the follow-ups <see cref="Overflow"/> elided by the per-epic draw cap
    /// (Story 19.2 review) — the sr-only enumeration lists these too, so the "not drawn (listed below)" overflow
    /// note is actually true for assistive tech, not just sighted users. Empty when nothing overflowed.</summary>
    public IReadOnlyList<string> OverflowLabelsOrEmpty => OverflowLabels ?? Array.Empty<string>();

    /// <summary>Non-null for the synthetic <em>Unattributed</em> bucket (Story 19.1 code-review D1) that hosts
    /// follow-ups belonging to no epic (<c>EpicNumber == null</c> or an unknown epic); its display name. Null for
    /// a real epic. [Story 19.2]</summary>
    public string? BucketLabel { get; init; }

    /// <summary>Display heading — "Epic N" for a real epic, the bucket label ("Unattributed") otherwise.</summary>
    public string DisplayName => BucketLabel ?? $"Epic {EpicNumber}";

    /// <summary>Stable in-page anchor / section id for the scope picker.</summary>
    public string Anchor => BucketLabel is null ? $"wg-epic-{EpicNumber}" : "wg-unattributed";

    /// <summary>A copy with every node href re-prefixed for a deeper page (e.g. <c>"../"</c> for the epic/story
    /// detail pages under <c>epics/</c>). The model is projected root-relative for <c>work-graph.html</c>; this
    /// re-roots those hrefs when the same subgraph is embedded elsewhere. No-op for the root page. [Story 19.2]</summary>
    public WorkGraphEpic Reprefixed(string linkPrefix)
    {
        if (string.IsNullOrEmpty(linkPrefix)) return this;
        var nodes = Nodes
            .Select(n => n.Href is { Length: > 0 } h
                ? n with { Href = FollowUpGeometry.ApplyLinkPrefix(linkPrefix, h) }
                : n)
            .ToList();
        return this with { Nodes = nodes };
    }
}

/// <summary>The whole-portal work graph: one <see cref="WorkGraphEpic"/> per epic that carries a graph signal
/// (≥1 attributed deferred item or open action item). An epic with only structural containment and no
/// follow-ups is omitted, so <see cref="IsEmpty"/> is the single NFR8 gate the page + nav share. [Story 19.2]</summary>
public sealed record WorkGraphModel(IReadOnlyList<WorkGraphEpic> Epics)
{
    public static WorkGraphModel Empty { get; } = new(Array.Empty<WorkGraphEpic>());

    /// <summary>True when no epic carries a graph signal — the page is not written and the nav entry is omitted
    /// (NFR8: absent artifacts → absent surfaces). [Story 19.2; 19.1 §5]</summary>
    public bool IsEmpty => Epics.Count == 0;
}

/// <summary>Pure projection from already-parsed models (<see cref="FollowUpGeometry"/> / <see cref="FollowUpDeferredSlot"/>,
/// <see cref="EpicsModel"/> structure, <see cref="ActionItemsTemplater.FindNearDuplicates"/>, <see cref="SiteGenerator"/>'s
/// epic→retro map) into an epic-scoped directed work graph. Reuses the seams Story 19.1 inventoried — it never
/// re-parses deferred markdown / sprint yaml and never re-counts open items against <see cref="ProjectCounts"/>.
/// Deterministic: identical input → identical node/edge order. [Story 19.2; 19.1 coverage map]</summary>
public static class WorkGraphBuilder
{
    /// <summary>Upper bound on drawn follow-up nodes (deferred + action) per epic. Beyond this the builder stops
    /// adding follow-ups and reports the remainder as <see cref="WorkGraphEpic.Overflow"/> — the templater still
    /// enumerates the full drawn set in its sr-only equivalent (nothing hidden from assistive tech). Comfortably
    /// above any real epic's follow-up fan-out this generator has run against; the default dogfood never trips it.</summary>
    public const int MaxFollowUpsPerEpic = 40;

    private static readonly Regex DottedStoryId = new(@"^\d+\.\d+$", RegexOptions.Compiled);

    /// <summary>Projects the whole work graph. <paramref name="followUps"/> must be the project-wide geometry
    /// (root-relative hrefs) — the page lives at the site root, so no re-prefixing is needed. Never throws:
    /// missing/degenerate inputs yield <see cref="WorkGraphModel.Empty"/> (AD-4). [Story 19.2]</summary>
    public static WorkGraphModel Build(
        EpicsModel? epics,
        FollowUpGeometry? followUps,
        IReadOnlyDictionary<int, string>? epicRetroMap = null)
    {
        if (epics is null || epics.Epics.Count == 0 || followUps is null) return WorkGraphModel.Empty;

        // Cross-epic near-duplicate action links (the soft `raised-in` source), computed once over the open set —
        // the SAME call ActionItemsTemplater makes, so the graph's cross-links can't drift from that page's.
        var nearDup = ActionItemsTemplater.FindNearDuplicates(followUps.OpenActionItems);

        var storyTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in epics.Epics)
        foreach (var s in e.Stories)
            storyTitles[s.Id] = PathUtil.StripHtmlTags(s.Title);
        var knownEpicNumbers = epics.Epics.Select(e => e.Number).ToHashSet();

        var built = new List<WorkGraphEpic>();
        foreach (var epic in epics.Epics)
        {
            var inEpicStoryIds = new HashSet<string>(epic.Stories.Select(s => s.Id), StringComparer.Ordinal);
            var deferred = followUps.DeferredForEpicNumber(epic.Number);
            var actions = followUps.ActionItems
                .Where(a => a.EpicNumber == epic.Number && !FollowUpGeometry.IsDone(a))
                .ToList();
            var e = BuildSubgraph(epic.Number, bucketLabel: null, $"Epic {epic.Number}",
                StoryEpicLinkifier.EpicPagePath(epic.Number), PathUtil.StripHtmlTags(epic.Title),
                inEpicStoryIds, deferred, actions, followUps, epicRetroMap, nearDup, storyTitles);
            if (e is not null) built.Add(e);
        }

        // D1 (Story 19.1 code review): follow-ups belonging to no epic (EpicNumber == null OR an unknown/ghost
        // epic) MUST NOT be silently dropped — orphaned provenance is a primary trace target. Render them in a
        // synthetic "Unattributed" pseudo-epic bucket, mirroring the action-items page's trailing Unattributed
        // group; the bucket omits cleanly when empty (same rule as an empty epic).
        var orphanDeferred = followUps.OrphanDeferredItems(knownEpicNumbers);
        var orphanActions = followUps.OrphanActionItems(knownEpicNumbers)
            .Where(a => !FollowUpGeometry.IsDone(a))
            .ToList();
        var bucket = BuildSubgraph(0, bucketLabel: "Unattributed", "Unattributed", rootHref: null, epicTitle: "",
            new HashSet<string>(StringComparer.Ordinal), orphanDeferred, orphanActions, followUps, epicRetroMap,
            nearDup, storyTitles);
        if (bucket is not null) built.Add(bucket);

        return built.Count == 0 ? WorkGraphModel.Empty : new WorkGraphModel(built);
    }

    /// <summary>Projects one scope's subgraph — a real epic (<paramref name="bucketLabel"/> null) or the synthetic
    /// Unattributed bucket. <paramref name="inEpicStoryIds"/> are the stories drawn in the root's own stories layer
    /// (empty for the bucket, so every source is an external origin leaf). Returns null when there is no graph
    /// signal (no deferred + no action) or nothing linked (NFR8).</summary>
    private static WorkGraphEpic? BuildSubgraph(
        int epicNumber,
        string? bucketLabel,
        string rootLabel,
        string? rootHref,
        string epicTitle,
        HashSet<string> inEpicStoryIds,
        IReadOnlyList<FollowUpDeferredSlot> deferred,
        IReadOnlyList<SprintActionItem> actions,
        FollowUpGeometry followUps,
        IReadOnlyDictionary<int, string>? epicRetroMap,
        IReadOnlyDictionary<SprintActionItem, IReadOnlyList<int>> nearDup,
        IReadOnlyDictionary<string, string> storyTitles)
    {
        // NFR8: a graph signal is at least one deferred item or open action item in this scope. Structural
        // containment alone (an epic's stories) is deliberately NOT a signal — otherwise the surface never omits.
        if (deferred.Count == 0 && actions.Count == 0) return null;

        var nodes = new List<WorkNode>();
        var byId = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<WorkEdge>();

        // Re-root an href for this page (work-graph.html lives at the output root). Deferred SourceHref/ResolvingHref
        // are prefixed for the deferred PAGE's depth (e.g. "../epics/story-1-1.html"); ApplyLinkPrefix("") strips the
        // leading ../ so the link resolves from the root. Idempotent on already-root-relative hrefs. [Story 19.2]
        static string? Root(string? href) =>
            string.IsNullOrEmpty(href) ? href : FollowUpGeometry.ApplyLinkPrefix("", href!);

        string Add(WorkNodeKind kind, string id, string label, string? href, string? title = null)
        {
            if (byId.Add(id)) nodes.Add(new WorkNode(kind, id, label, Root(href), title));
            return id;
        }
        void Link(string from, string to, WorkEdgeKind kind)
        {
            if (!string.Equals(from, to, StringComparison.Ordinal))
                edges.Add(new WorkEdge(from, to, kind));
        }

        var epicId = Add(WorkNodeKind.Epic, $"e{epicNumber}", rootLabel, rootHref,
            epicTitle.Length > 0 ? epicTitle : null);

        // In-scope story node (drawn in the stories layer) — created lazily the first time a follow-up names it.
        string EnsureEpicStory(string storyId)
        {
            var id = $"s{storyId}";
            if (byId.Contains(id)) return id;
            var title = storyTitles.TryGetValue(storyId, out var t) ? t : null;
            Add(WorkNodeKind.Story, id, $"Story {storyId}", StoryEpicLinkifier.StoryPagePath(storyId), title);
            Link(id, epicId, WorkEdgeKind.Contains);
            return id;
        }

        var drawn = 0;
        var overflow = 0;
        var overflowLabels = new List<string>();

        for (var i = 0; i < deferred.Count; i++)
        {
            if (drawn >= MaxFollowUpsPerEpic)
            {
                // Elided items still get their full plain-text label recorded (Story 19.2 review) — the templater's
                // sr-only enumeration lists them even though the SVG never draws them.
                for (var k = i; k < deferred.Count; k++)
                    overflowLabels.Add($"Deferred item: {Summarize(deferred[k].Item.BodyHtml)}");
                foreach (var a in actions)
                    overflowLabels.Add($"Action item: {FollowUpRow.SummarizePlainText(a.Action, 90)}");
                overflow = (deferred.Count - i) + Math.Max(0, actions.Count);
                goto done;
            }
            var slot = deferred[i];
            drawn++;
            var did = Add(WorkNodeKind.Deferred, $"d{epicNumber}-{i}",
                Summarize(slot.Item.BodyHtml), slot.DetailHref, slot.ProvenanceLabel);

            var rooted = false;

            // stemmed-from: carrier (deferred) → its source. In-scope story sources connect through the story
            // (which already links to the root); external story sources become their own origin leaf.
            if (!string.IsNullOrEmpty(slot.SourceStoryId) && inEpicStoryIds.Contains(slot.SourceStoryId!))
            {
                Link(did, EnsureEpicStory(slot.SourceStoryId!), WorkEdgeKind.StemmedFrom);
                rooted = true;
            }
            else if (!string.IsNullOrEmpty(slot.SourceStoryId))
            {
                var osid = Add(WorkNodeKind.Story, $"s{slot.SourceStoryId}", $"Story {slot.SourceStoryId}",
                    StoryEpicLinkifier.StoryPagePath(slot.SourceStoryId!));
                Link(did, osid, WorkEdgeKind.StemmedFrom);
            }
            else if (!string.IsNullOrWhiteSpace(slot.SourceKey) && !string.IsNullOrEmpty(slot.SourceHref))
            {
                // D4 (Story 19.1 code review): an unresolved SourceKey must NEVER mint a phantom node from the raw
                // string (cf. the a16ca0f phantom-item fix). Only project the source node when the key resolved to a
                // real page (SourceHref non-null — set by ResolveSourceHref/FindQuickDev); otherwise drop the edge
                // and let the item root to its epic below.
                var key = FollowUpGeometry.NormalizeSourceKey(slot.SourceKey);
                if (key.Length > 0)
                {
                    var spid = Add(WorkNodeKind.Spec, $"src:{key}", key, slot.SourceHref);
                    Link(did, spid, WorkEdgeKind.StemmedFrom);
                }
            }

            // resolves: carrier (deferred) → its resolving story/spec. A story resolver reuses the SAME story-node
            // id scheme as every other story reference in this graph (EnsureEpicStory / SourceStoryId linking
            // above), so a story that both sources AND resolves a deferred item — or resolves its own — draws as
            // ONE node, not two. [Story 19.2 review]
            if (slot.Item.Resolved && !string.IsNullOrWhiteSpace(slot.Item.ResolvingRef))
            {
                var reff = slot.Item.ResolvingRef!.Trim();
                var bare = System.IO.Path.GetFileName(reff.Replace('\\', '/'));
                if (DottedStoryId.IsMatch(bare))
                {
                    var rid = Add(WorkNodeKind.Story, $"s{bare}", $"Story {bare}", StoryEpicLinkifier.StoryPagePath(bare));
                    Link(did, rid, WorkEdgeKind.Resolves);
                }
                else if (slot.Item.ResolvingHref is { Length: > 0 })
                {
                    // D4 (Story 19.1 code review): never mint a node from raw resolver text without a real page to
                    // land on — mirrors the SourceKey guard above. [Story 19.2 review]
                    var rid = Add(WorkNodeKind.Spec, $"res:{FollowUpGeometry.NormalizeSourceKey(reff)}",
                        FollowUpRefs.ResolvingLabel(reff), slot.Item.ResolvingHref);
                    Link(did, rid, WorkEdgeKind.Resolves);
                }
            }

            // Attribute the deferred item to the root when nothing anchored it to an in-scope story.
            if (!rooted) Link(did, epicId, WorkEdgeKind.Contains);
        }

        for (var j = 0; j < actions.Count; j++)
        {
            if (drawn >= MaxFollowUpsPerEpic)
            {
                for (var k = j; k < actions.Count; k++)
                    overflowLabels.Add($"Action item: {FollowUpRow.SummarizePlainText(actions[k].Action, 90)}");
                overflow += actions.Count - j;
                break;
            }
            var action = actions[j];
            drawn++;
            var aid = Add(WorkNodeKind.Action, $"a{epicNumber}-{j}",
                FollowUpRow.SummarizePlainText(action.Action, 90), followUps.HrefFor(action));
            Link(aid, epicId, WorkEdgeKind.Contains);

            // raised-in (soft): carrier (action) → another epic's retro when the obligation near-duplicates there.
            if (nearDup.TryGetValue(action, out var otherEpics))
            {
                foreach (var other in otherEpics)
                {
                    var retroHref = epicRetroMap is not null && epicRetroMap.TryGetValue(other, out var rp) ? rp : null;
                    var rtid = Add(WorkNodeKind.Retro, $"retro:{other}", $"Epic {other} retro", retroHref);
                    Link(aid, rtid, WorkEdgeKind.RaisedIn);
                }
            }
        }

    done:
        // A signal existed but every follow-up was elided (cap of 0 is impossible, but stay defensive) → omit.
        if (nodes.Count <= 1 || edges.Count == 0) return null;

        var cycles = FindCycles(nodes.Select(n => n.Id).ToList(), edges);
        return new WorkGraphEpic(epicNumber, epicTitle, nodes, edges, cycles, overflow, overflowLabels) { BucketLabel = bucketLabel };
    }

    /// <summary>Projects a <em>story-scoped</em> subgraph for embedding on a story page (Story 19.2): the story
    /// and the deferred items that <em>stemmed from it</em> (the reverse "Deferred from this" set —
    /// <see cref="FollowUpGeometry.DeferredForSource"/>), plus each item's resolver, all under the story's epic
    /// root so the layered layout reads Epic → Story → Deferred → Resolvers. Root-relative hrefs (caller re-prefixes
    /// via <see cref="WorkGraphEpic.Reprefixed"/>). Returns null when nothing stemmed from the story — no tab.</summary>
    public static WorkGraphEpic? BuildStory(StoryInfo story, string epicTitle, FollowUpGeometry? followUps)
    {
        if (followUps is null) return null;
        var deferred = followUps.DeferredForSource(story.Id);
        if (deferred.Count == 0) return null;

        var nodes = new List<WorkNode>();
        var byId = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<WorkEdge>();

        static string? Root(string? href) =>
            string.IsNullOrEmpty(href) ? href : FollowUpGeometry.ApplyLinkPrefix("", href!);
        string Add(WorkNodeKind kind, string id, string label, string? href, string? title = null)
        {
            if (byId.Add(id)) nodes.Add(new WorkNode(kind, id, label, Root(href), title));
            return id;
        }
        void Link(string from, string to, WorkEdgeKind kind)
        {
            if (!string.Equals(from, to, StringComparison.Ordinal)) edges.Add(new WorkEdge(from, to, kind));
        }

        var epicId = Add(WorkNodeKind.Epic, $"e{story.EpicNumber}", $"Epic {story.EpicNumber}",
            StoryEpicLinkifier.EpicPagePath(story.EpicNumber), epicTitle.Length > 0 ? epicTitle : null);
        var sid = Add(WorkNodeKind.Story, $"s{story.Id}", $"Story {story.Id}",
            StoryEpicLinkifier.StoryPagePath(story.Id), PathUtil.StripHtmlTags(story.Title));
        Link(sid, epicId, WorkEdgeKind.Contains);

        var overflow = 0;
        var overflowLabels = new List<string>();
        for (var i = 0; i < deferred.Count; i++)
        {
            if (i >= MaxFollowUpsPerEpic)
            {
                for (var k = i; k < deferred.Count; k++)
                    overflowLabels.Add($"Deferred item: {Summarize(deferred[k].Item.BodyHtml)}");
                overflow = deferred.Count - i;
                break;
            }
            var slot = deferred[i];
            var did = Add(WorkNodeKind.Deferred, $"d-{i}", Summarize(slot.Item.BodyHtml),
                slot.DetailHref, slot.ProvenanceLabel);
            Link(did, sid, WorkEdgeKind.StemmedFrom); // stemmed from THIS story

            // Same story-node id reuse + D4 href guard as BuildSubgraph above. [Story 19.2 review]
            if (slot.Item.Resolved && !string.IsNullOrWhiteSpace(slot.Item.ResolvingRef))
            {
                var reff = slot.Item.ResolvingRef!.Trim();
                var bare = System.IO.Path.GetFileName(reff.Replace('\\', '/'));
                if (DottedStoryId.IsMatch(bare))
                {
                    var rid = Add(WorkNodeKind.Story, $"s{bare}", $"Story {bare}", StoryEpicLinkifier.StoryPagePath(bare));
                    Link(did, rid, WorkEdgeKind.Resolves);
                }
                else if (slot.Item.ResolvingHref is { Length: > 0 })
                {
                    var rid = Add(WorkNodeKind.Spec, $"res:{FollowUpGeometry.NormalizeSourceKey(reff)}",
                        FollowUpRefs.ResolvingLabel(reff), slot.Item.ResolvingHref);
                    Link(did, rid, WorkEdgeKind.Resolves);
                }
            }
        }

        if (edges.Count == 0) return null;
        var cycles = FindCycles(nodes.Select(n => n.Id).ToList(), edges);
        return new WorkGraphEpic(story.EpicNumber, epicTitle, nodes, edges, cycles, overflow, overflowLabels);
    }

    /// <summary>Short plain-text label for a deferred item's HTML body (tags stripped, whitespace collapsed,
    /// ellipsis-truncated) — the full text stays in the node tooltip / sr-only list.</summary>
    private static string Summarize(string bodyHtml) => FollowUpRow.SummarizeFromHtml(bodyHtml, 90);

    /// <summary>Finds simple directed cycles over <paramref name="edges"/> (a "cycle" = a directed loop among
    /// named node types — Story 19.1's MVP definition). Deterministic DFS from each node in
    /// <paramref name="nodeIds"/> order; each discovered back-edge reconstructs one elementary cycle, deduped by
    /// canonical rotation and capped at <paramref name="cap"/>. Returns an empty list for the acyclic common case
    /// (the honest "no cycles in this scope" signal). Self-loops are ignored at edge-build time (never linked).
    /// [Story 19.2 Task 2; 19.1 §3]</summary>
    internal static IReadOnlyList<IReadOnlyList<string>> FindCycles(
        IReadOnlyList<string> nodeIds, IReadOnlyList<WorkEdge> edges, int cap = 12)
    {
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in nodeIds) adj[id] = new List<string>();
        foreach (var e in edges)
            if (adj.TryGetValue(e.FromId, out var outs) && adj.ContainsKey(e.ToId))
                outs.Add(e.ToId);

        var found = new List<IReadOnlyList<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal); // canonical cycle keys, for dedup
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new List<string>();

        void Dfs(string node)
        {
            if (found.Count >= cap) return;
            onStack.Add(node);
            stack.Add(node);
            foreach (var next in adj[node])
            {
                if (found.Count >= cap) break;
                if (onStack.Contains(next))
                {
                    var start = stack.IndexOf(next);
                    if (start < 0) continue;
                    var cycle = stack.GetRange(start, stack.Count - start);
                    var key = CanonicalKey(cycle);
                    if (seen.Add(key)) found.Add(cycle);
                }
                else
                {
                    Dfs(next);
                }
            }
            onStack.Remove(node);
            stack.RemoveAt(stack.Count - 1);
        }

        foreach (var id in nodeIds)
        {
            if (found.Count >= cap) break;
            Dfs(id);
        }

        return found;
    }

    /// <summary>Rotation-invariant key for a cycle so the same loop discovered from different entry nodes
    /// dedupes to one report (e.g. [a,b,c] and [b,c,a] share a key).</summary>
    private static string CanonicalKey(IReadOnlyList<string> cycle)
    {
        var min = 0;
        for (var i = 1; i < cycle.Count; i++)
            if (string.CompareOrdinal(cycle[i], cycle[min]) < 0) min = i;
        var rotated = new List<string>(cycle.Count);
        for (var i = 0; i < cycle.Count; i++) rotated.Add(cycle[(min + i) % cycle.Count]);
        return string.Join("", rotated);
    }
}
