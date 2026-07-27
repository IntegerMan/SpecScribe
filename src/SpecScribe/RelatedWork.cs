namespace SpecScribe;

/// <summary>Which end of a directed work edge the node being described sits on. Edges are always
/// <em>carrier → target</em> (ADR 0011 / Story 19.1 §2), so the same <see cref="WorkEdgeKind"/> reads differently
/// from each side: a deferred item <em>stemmed from</em> its source story (outgoing), while that story has
/// <em>work that stemmed from it</em> (incoming). Both directions are surfaced — a pane that showed only outgoing
/// edges would leave every story and epic looking unrelated to anything. [Story 20.3]</summary>
public enum RelatedWorkDirection
{
    /// <summary>The described node is the edge's <see cref="WorkEdge.FromId"/> (the carrier).</summary>
    Outgoing,

    /// <summary>The described node is the edge's <see cref="WorkEdge.ToId"/> (the target).</summary>
    Incoming,
}

/// <summary>One related node as the pane renders it: the OTHER endpoint of an edge touching the described node.
/// <see cref="Href"/> is null when the work graph had no page to land on — rendered as a non-link chip, mirroring
/// <see cref="WorkNode"/>'s guarded-href discipline (never a dead <c>&lt;a&gt;</c>, never a fabricated
/// destination). [Story 20.3]</summary>
public sealed record RelatedWorkEntry(WorkNodeKind Kind, string Label, string? Href, string? Title);

/// <summary>One (edge kind × direction) bucket of related nodes — the "groups related nodes by edge kind" unit of
/// AC #1. <see cref="Hidden"/> counts entries elided by <see cref="RelatedWork.MaxEntriesPerGroup"/> so the pane
/// can say how many it is not showing instead of truncating silently. [Story 20.3]</summary>
public sealed record RelatedWorkGroup(
    WorkEdgeKind Kind,
    RelatedWorkDirection Direction,
    string Heading,
    IReadOnlyList<RelatedWorkEntry> Entries,
    int Hidden = 0);

/// <summary>A work-graph node whose relationships are shown INSIDE another node's scope, under its own name.
/// Used for a story the sunburst never drew a wedge for: it can never be the selection, but dropping it would take
/// a whole edge kind off the surface with it (on the live portal every <c>Resolves</c> edge lands on a resolver
/// story, and most resolver stories sit in density-collapsed epics). Story 20.1 spike §1a rule 2 requires resolving
/// such a node to its nearest existing ancestor rather than dropping it silently — this is that, WITHOUT
/// re-attributing its relationships to the ancestor, which would make the group headings lie. [Story 20.3]</summary>
public sealed record RelatedWorkSubject(
    string Label,
    WorkNodeKind Kind,
    string? Href,
    IReadOnlyList<RelatedWorkGroup> Groups);

/// <summary>The related-work content for ONE selectable explorer node, keyed by the id the explorer payload island
/// speaks (<see cref="IslandId"/> — <c>epic-{N}</c> / <c>{StoryInfo.Id}</c> / <c>orphan</c>), not by the work
/// graph's internal id. The two id spaces are disjoint and a literal join returns nothing; the translation is
/// <see cref="RelatedWork.IslandIdFor"/>. <see cref="Groups"/> are this node's OWN relationships;
/// <see cref="Subjects"/> are the unwedged descendants folded into its scope, each under its own name.
/// [Story 20.3; Story 20.1 spike §1a]</summary>
public sealed record RelatedWorkNode(
    string IslandId,
    string Label,
    WorkNodeKind Kind,
    string? Href,
    string ScopeAnchor,
    IReadOnlyList<RelatedWorkGroup> Groups,
    IReadOnlyList<RelatedWorkSubject> Subjects)
{
    /// <summary>Total related entries actually carried (excludes <see cref="RelatedWorkGroup.Hidden"/>).</summary>
    public int EntryCount =>
        Groups.Sum(g => g.Entries.Count) + Subjects.Sum(s => s.Groups.Sum(g => g.Entries.Count));
}

/// <summary>The whole related-work projection for one host page: one <see cref="RelatedWorkNode"/> per selectable
/// explorer node that has at least one related item, plus the work graph's own honestly-reported draw overflow.
/// <see cref="IsEmpty"/> is the pane's NFR8 gate — an empty projection renders NO pane at all rather than
/// permanent dead chrome on a young project (deferred-work.md:960). [Story 20.3]</summary>
public sealed record RelatedWorkModel(
    IReadOnlyList<RelatedWorkNode> Nodes,
    int Overflow,
    IReadOnlyList<string> OverflowLabels)
{
    public static RelatedWorkModel Empty { get; } = new(Array.Empty<RelatedWorkNode>(), 0, Array.Empty<string>());

    public bool IsEmpty => Nodes.Count == 0;
}

/// <summary>The single relationship vocabulary + the pure projection from Epic 19's already-computed
/// <see cref="WorkGraphModel"/> onto the per-node adjacency the Story 20.3 related-work pane renders.
///
/// <para><b>Pure read, no second ledger.</b> Every input is an already-projected model: this type never touches
/// <see cref="ProjectCounts"/>, never re-runs an Epic 9 parser, and never calls
/// <see cref="WorkGraphBuilder.Build"/> — the generator's cached <c>_workGraph</c> instance is passed in verbatim.
/// The signature is the proof: it takes a <see cref="WorkGraphModel"/> and a set of island ids, and there is no
/// counting seam to reach.</para>
///
/// <para><b>Data-driven over the enum, not hard-coded to four kinds.</b> Grouping iterates
/// <see cref="WorkEdgeKind"/>'s declared values, so if Epic 19 (or Epic 24) later adds <c>covers</c>/<c>cites</c>
/// edges the pane renders them with no rewrite — and a kind with no entries renders nothing, so the pane never
/// shows a phantom section for a relationship the graph cannot populate.</para>
///
/// <para><b>The id bridge.</b> The explorer payload island and the work graph mint ids independently and the two
/// spaces are DISJOINT (<c>epic-20</c>/<c>20.2</c> vs <c>e20</c>/<c>s20.2</c>); most edge endpoints
/// (<c>d*</c>/<c>a*</c>/<c>src:</c>/<c>res:</c>/<c>retro:</c>) have no wedge at all. Translation happens here, once,
/// server-side — never in JS and never by string-munging at two call sites.
/// [Story 20.1 spike §1a, corrected at its 2026-07-24 code review]</para>
/// [Story 20.3]</summary>
public static class RelatedWork
{
    /// <summary>Island id of the sunburst's unattributed-follow-ups root. The work graph models the same work as a
    /// synthetic <em>Unattributed</em> bucket carrying <c>EpicNumber == 0</c>; these are the same concept under two
    /// names, and the bucket is identified by <see cref="WorkGraphEpic.BucketLabel"/> — NEVER by its epic number,
    /// which a real Epic 0 would collide with. [Story 20.1 spike §1a rule 4]</summary>
    public const string OrphanIslandId = "orphan";

    /// <summary>Per-group entry cap. A single epic can carry up to <see cref="WorkGraphBuilder.MaxFollowUpsPerEpic"/>
    /// follow-ups, and the pane ships EVERY selectable node's groups into the dashboard DOM (that is what makes it
    /// work with JS off), so an uncapped projection would put hundreds of rows on the home page. Elided entries are
    /// counted in <see cref="RelatedWorkGroup.Hidden"/> and the pane links out to <c>work-graph.html</c> for the
    /// full set — truncation is reported, never silent. [Story 20.3]</summary>
    public const int MaxEntriesPerGroup = 12;

    /// <summary>Projects the pane's per-node adjacency.
    /// <paramref name="graph"/> is the generator's cached model (reused verbatim; never rebuilt).
    /// <paramref name="islandNodeIds"/> is the id set the explorer payload actually drew — from
    /// <see cref="Charts.SunburstExplorerNodes"/> — which decides which nodes are SELECTABLE; a node the chart never
    /// drew gets no section (it can never be the selection) but still appears as an entry wherever it is related.
    /// <paramref name="linkPrefix"/> re-roots node hrefs for a host page below the site root
    /// (<see cref="WorkGraphEpic.Reprefixed"/>); the dashboard is at the root, so it is a no-op there — but the rule
    /// is applied, not assumed away. Never throws: a null/empty graph yields
    /// <see cref="RelatedWorkModel.Empty"/> (AD-4). [Story 20.3]</summary>
    public static RelatedWorkModel Build(
        WorkGraphModel? graph,
        IReadOnlyCollection<string>? islandNodeIds = null,
        string linkPrefix = "")
    {
        if (graph is null || graph.IsEmpty) return RelatedWorkModel.Empty;

        var islandIds = islandNodeIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(islandNodeIds, StringComparer.Ordinal);

        // `_workGraph` is NOT one graph — it is a list of per-epic subgraphs, each with its own id set, so the same
        // story legitimately appears in several of them. Flatten with dedup by node id and by (from,to,kind), or the
        // pane double-lists every cross-subgraph reference. [Story 20.1 spike §1a rule 4]
        var byId = new Dictionary<string, WorkNode>(StringComparer.Ordinal);
        var scopeOfNode = new Dictionary<string, WorkGraphEpic>(StringComparer.Ordinal);
        // Dictionary enumeration order is an implementation detail, so the section order is carried by this list —
        // the model's own epic-then-node order. Determinism (FR31) is a golden-fingerprint invariant here.
        var nodeOrder = new List<string>();
        var edgeSeen = new HashSet<(string, string, WorkEdgeKind)>();
        var edges = new List<WorkEdge>();
        var overflow = 0;
        var overflowLabels = new List<string>();

        foreach (var raw in graph.Epics)
        {
            var scope = raw.Reprefixed(linkPrefix);
            foreach (var n in scope.Nodes)
            {
                if (!byId.ContainsKey(n.Id))
                {
                    byId[n.Id] = n;
                    scopeOfNode[n.Id] = scope;
                    nodeOrder.Add(n.Id);
                }
            }
            foreach (var e in scope.Edges)
            {
                if (edgeSeen.Add((e.FromId, e.ToId, e.Kind))) edges.Add(e);
            }
            // Respect the graph's own bounds rather than under-reporting them. [Story 20.1 spike §1a rule 5]
            overflow += scope.Overflow;
            overflowLabels.AddRange(scope.OverflowLabelsOrEmpty);
        }

        // Adjacency, in first-seen edge order — deterministic (FR31): the flatten above walks the model's own epic
        // and node order, and nothing here sorts or hashes into the output.
        var outgoing = new Dictionary<string, List<WorkEdge>>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, List<WorkEdge>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!byId.ContainsKey(e.FromId) || !byId.ContainsKey(e.ToId)) continue;
            (outgoing.TryGetValue(e.FromId, out var o) ? o : outgoing[e.FromId] = new List<WorkEdge>()).Add(e);
            (incoming.TryGetValue(e.ToId, out var i) ? i : incoming[e.ToId] = new List<WorkEdge>()).Add(e);
        }

        // Place every node into the scope that OWNS its section: itself when the chart drew a wedge for it,
        // otherwise its nearest existing ancestor (§1a rule 2). Nodes with neither — deferred/action/source/retro —
        // own no section and appear only as entries.
        var scopes = new List<RelatedWorkNode>();
        var scopeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var foldedInto = new List<(string Host, RelatedWorkSubject Subject, string Anchor)>();

        foreach (var id in nodeOrder)
        {
            var node = byId[id];
            var own = IslandIdFor(scopeOfNode[id], node, islandIds);
            var host = own ?? AncestorIslandIdFor(scopeOfNode[id], node, islandIds);
            if (host is null) continue;

            var groups = BuildGroups(id, outgoing, incoming, byId);
            if (groups.Count == 0) continue;

            if (own is not null)
            {
                if (scopeIndex.ContainsKey(own)) continue; // one section per island id
                scopeIndex[own] = scopes.Count;
                scopes.Add(new RelatedWorkNode(
                    own, node.Label, node.Kind, node.Href, AnchorForIslandId(own),
                    groups, Array.Empty<RelatedWorkSubject>()));
            }
            else
            {
                // A folded subject sits INSIDE its container's section, so its own outgoing "Part of → Epic N"
                // group restates the heading above it. Drop it: on the live portal that was the single most
                // repeated group in the pane and it told the reader nothing.
                var meaningful = groups.Where(g => !IsRestatedContainsGroup(g)).ToList();
                if (meaningful.Count == 0) continue;
                // Anchor from `host` (the correctly id-derived ancestor), NOT from `scopeOfNode[id]` — that
                // scope is first-seen-wins across the whole flatten (rule 4), so a story cross-referenced from
                // a foreign epic's follow-up before its own epic's subgraph is walked would otherwise carry
                // that foreign epic's anchor here, sending its "+N more" link to the wrong section. [review]
                foldedInto.Add((host, new RelatedWorkSubject(node.Label, node.Kind, node.Href, meaningful),
                    AnchorForIslandId(host)));
            }
        }

        // Attach folded subjects in first-seen order. A host scope can be reached ONLY this way — an epic whose own
        // node carries no edges because every follow-up rooted through a story — so materialize it rather than
        // losing the content it hosts.
        foreach (var (host, subject, anchor) in foldedInto)
        {
            if (!scopeIndex.TryGetValue(host, out var at))
            {
                at = scopeIndex[host] = scopes.Count;
                scopes.Add(new RelatedWorkNode(
                    host, HostLabel(host), WorkNodeKind.Epic, null, anchor,
                    Array.Empty<RelatedWorkGroup>(), new List<RelatedWorkSubject>()));
            }
            scopes[at] = scopes[at] with { Subjects = scopes[at].Subjects.Append(subject).ToList() };
        }

        return scopes.Count == 0 && overflow == 0
            ? RelatedWorkModel.Empty
            : new RelatedWorkModel(scopes, overflow, overflowLabels);
    }

    private static string HostLabel(string islandId) =>
        islandId == OrphanIslandId
            ? "Unattributed"
            : islandId.StartsWith("epic-", StringComparison.Ordinal) ? "Epic " + islandId[5..] : islandId;

    /// <summary>The <see cref="WorkGraphEpic.Anchor"/> a given island id maps to, computed from the id itself
    /// rather than from whatever <see cref="WorkGraphEpic"/> instance happened to be walked first — the id is
    /// always correct (it is what <see cref="IslandIdFor"/>/<see cref="AncestorIslandIdFor"/> already resolved),
    /// while a scope reference can be the wrong epic's for a node reached first as a foreign cross-reference.
    /// Mirrors <see cref="WorkGraphEpic.Anchor"/>'s own two-branch shape exactly.</summary>
    internal static string AnchorForIslandId(string islandId) =>
        islandId == OrphanIslandId ? "wg-unattributed" : $"wg-{islandId}";

    /// <summary>True for a node's own outgoing "Part of → Epic N" group — the shared test for "drop this group
    /// because the section/subject it would sit under already states the relationship as its container."
    ///
    /// <para><b>One caller remains:</b> the folded-subject branch of <see cref="Build"/> below, where a node sits
    /// inside its container's section and its own "Part of" group would restate the heading above it. The second
    /// caller this doc used to name — <c>RelatedWorkCards</c> folding a story's groups into its epic card — was
    /// removed when Story 20.5's <c>select</c> mode gave story leaves cards of their own, and a standalone story
    /// card SHOULD state which epic it belongs to. Corrected rather than left describing a design that no longer
    /// exists, which is the failure the Story 7.11/7.12 joint review recorded. [Story 20.5 review]</para></summary>
    internal static bool IsRestatedContainsGroup(RelatedWorkGroup g) =>
        g.Kind == WorkEdgeKind.Contains && g.Direction == RelatedWorkDirection.Outgoing;

    /// <summary>The (edge kind × direction) buckets for one node, iterated over <see cref="WorkEdgeKind"/>'s
    /// declared values so a future kind renders without a rewrite. Empty buckets are omitted — a section the graph
    /// cannot populate is a phantom, not a placeholder.</summary>
    private static IReadOnlyList<RelatedWorkGroup> BuildGroups(
        string nodeId,
        IReadOnlyDictionary<string, List<WorkEdge>> outgoing,
        IReadOnlyDictionary<string, List<WorkEdge>> incoming,
        IReadOnlyDictionary<string, WorkNode> byId)
    {
        var groups = new List<RelatedWorkGroup>();
        foreach (var kind in Enum.GetValues<WorkEdgeKind>())
        {
            Take(outgoing, nodeId, kind, RelatedWorkDirection.Outgoing, e => e.ToId);
            Take(incoming, nodeId, kind, RelatedWorkDirection.Incoming, e => e.FromId);
        }
        return groups;

        void Take(
            IReadOnlyDictionary<string, List<WorkEdge>> side,
            string id,
            WorkEdgeKind kind,
            RelatedWorkDirection direction,
            Func<WorkEdge, string> otherEnd)
        {
            if (!side.TryGetValue(id, out var list)) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<RelatedWorkEntry>();
            var hidden = 0;
            foreach (var e in list)
            {
                if (e.Kind != kind) continue;
                var other = otherEnd(e);
                // Two edges of the same kind between the same pair collapse to one row (the reader learns nothing
                // from a duplicate), and a self-edge can never appear — Link() refuses those at build time.
                if (!seen.Add(other) || !byId.TryGetValue(other, out var n)) continue;
                if (entries.Count >= MaxEntriesPerGroup) { hidden++; continue; }
                entries.Add(new RelatedWorkEntry(n.Kind, NodeText(n), n.Href, n.Title));
            }
            if (entries.Count > 0) groups.Add(new RelatedWorkGroup(kind, direction, Heading(kind, direction), entries, hidden));
        }
    }

    /// <summary>Translates a work-graph node id into the id the explorer payload island speaks, or null when the
    /// chart never drew a wedge for it — which is the common case and is CORRECT: every <c>StemmedFrom</c>,
    /// <c>Resolves</c> and <c>RaisedIn</c> edge terminates on a deferred/action/source/retro node the sunburst has
    /// never drawn. Those are related-work rows, not wedges; the pane is a related-work list, not a second
    /// projection of the chart. [Story 20.1 spike §1a rules 1–4]</summary>
    internal static string? IslandIdFor(WorkGraphEpic scope, WorkNode node, ISet<string> islandIds)
    {
        switch (node.Kind)
        {
            case WorkNodeKind.Epic:
                // The synthetic Unattributed bucket is `e0` but is NOT epic 0 — it is the sunburst's `orphan` root.
                var epicIslandId = scope.BucketLabel is not null ? OrphanIslandId : $"epic-{scope.EpicNumber}";
                return islandIds.Contains(epicIslandId) ? epicIslandId : null;

            case WorkNodeKind.Story:
                // `s20.2` → `20.2`, but ONLY if that story actually has a wedge: SunburstExplorerNodes emits
                // per-story nodes only for an epic under the density-collapse threshold whose stories carry weight,
                // so a dense or fully-done epic contributes no story ids at all. An unwedged story can never BE the
                // selection, so it gets no section — its relationships stay visible as entries under whatever is
                // related to it, and in full on work-graph.html.
                var storyId = node.Id.Length > 1 ? node.Id[1..] : string.Empty;
                return storyId.Length > 0 && islandIds.Contains(storyId) ? storyId : null;

            default:
                return null;
        }
    }

    /// <summary>The nearest island-addressable ancestor for a node the chart drew no wedge for — §1a rule 2's
    /// "resolve to the nearest existing ancestor rather than dropping the edge silently".
    ///
    /// <para>Only STORY nodes fold. A deferred/action/source/retro node is a related-work ROW by nature: it has no
    /// place of its own in the hierarchy the chart draws, and hoisting it into an epic scope would list an epic's
    /// every follow-up twice (once under the epic's own <c>Contains</c>, once as a fabricated subject).</para>
    ///
    /// <para>The epic is derived from the STORY ID (<c>7.11</c> → <c>epic-7</c>), not from whichever subgraph the
    /// node happened to be seen in first: a story can appear in another epic's subgraph as an external source, and
    /// filing it under that epic would be wrong. The containing scope is the fallback. [Story 20.3]</para></summary>
    internal static string? AncestorIslandIdFor(WorkGraphEpic scope, WorkNode node, ISet<string> islandIds)
    {
        if (node.Kind != WorkNodeKind.Story) return null;

        var storyId = node.Id.Length > 1 ? node.Id[1..] : string.Empty;
        var dot = storyId.IndexOf('.');
        if (dot > 0)
        {
            var fromId = $"epic-{storyId[..dot]}";
            if (islandIds.Contains(fromId)) return fromId;
        }
        if (scope.BucketLabel is not null)
            return islandIds.Contains(OrphanIslandId) ? OrphanIslandId : null;
        var fromScope = $"epic-{scope.EpicNumber}";
        return islandIds.Contains(fromScope) ? fromScope : null;
    }

    /// <summary>The pane's group heading for one (kind, direction) pair. Falls back to a derived heading for a kind
    /// this table has not been taught yet, so adding a <see cref="WorkEdgeKind"/> value can never produce a blank
    /// or crashing section.</summary>
    internal static string Heading(WorkEdgeKind kind, RelatedWorkDirection direction) => (kind, direction) switch
    {
        (WorkEdgeKind.Contains, RelatedWorkDirection.Outgoing) => "Part of",
        (WorkEdgeKind.Contains, RelatedWorkDirection.Incoming) => "Contains",
        (WorkEdgeKind.StemmedFrom, RelatedWorkDirection.Outgoing) => "Stemmed from",
        (WorkEdgeKind.StemmedFrom, RelatedWorkDirection.Incoming) => "Work that stemmed from this",
        (WorkEdgeKind.Resolves, RelatedWorkDirection.Outgoing) => "Resolved by",
        (WorkEdgeKind.Resolves, RelatedWorkDirection.Incoming) => "Resolved by this",
        (WorkEdgeKind.RaisedIn, RelatedWorkDirection.Outgoing) => "Also raised in",
        (WorkEdgeKind.RaisedIn, RelatedWorkDirection.Incoming) => "Also raised here",
        _ => direction == RelatedWorkDirection.Outgoing ? Sentence(EdgeVerb(kind)) : $"Referenced by ({kind})",
    };

    private static string Sentence(string verb) =>
        verb.Length > 0 ? char.ToUpperInvariant(verb[0]) + verb[1..] : verb;

    /// <summary>Plain-text name of a work-graph node, kind-prefixed so a deferred item's summary is never mistaken
    /// for a story title. THE single node vocabulary — <see cref="WorkGraphTemplater"/> renders its sr-only
    /// enumeration through this same helper so the graph page and the pane can never drift into two names for one
    /// node. [Story 19.2; relocated here Story 20.3]</summary>
    public static string NodeText(WorkNode n) => n.Kind switch
    {
        WorkNodeKind.Deferred => $"Deferred item: {n.Label}",
        WorkNodeKind.Action => $"Action item: {n.Label}",
        WorkNodeKind.Spec => $"Source: {n.Label}",
        _ => n.Label,
    };

    /// <summary>Plain-text verb for a carrier → target edge, reading in the edge's own direction. THE single edge
    /// vocabulary, shared with <see cref="WorkGraphTemplater"/>'s sr-only link enumeration (Story 20.8 reuses it
    /// too — one relationship vocabulary, never a second). [Story 19.2; relocated here Story 20.3]</summary>
    public static string EdgeVerb(WorkEdgeKind kind) => kind switch
    {
        WorkEdgeKind.Contains => "is part of",
        WorkEdgeKind.StemmedFrom => "stemmed from",
        WorkEdgeKind.Resolves => "was resolved by",
        WorkEdgeKind.RaisedIn => "was also raised in",
        _ => "links to",
    };

}
