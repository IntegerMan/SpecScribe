using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 20.3 — the related-work projection (grouping, the island id bridge, dedup, guarded hrefs,
/// determinism, the NFR8 omit gate) and the pane it renders.</summary>
public class RelatedWorkTests
{
    // ---- Grouping by edge kind + direction ---------------------------------------------------------------

    [Fact]
    public void Build_GroupsRelatedNodesByEdgeKindInBothDirections()
    {
        // Epic 1 with a deferred item that stemmed from Story 1.1 → the epic sees the story it contains, and the
        // story (which has a wedge) sees the work that stemmed from it.
        var model = Project(Geometry(deferred: new[]
        {
            Deferred("Fix the flaky retry loop", epic: 1, sourceStoryId: "1.1"),
        }));

        var epic = Node(model, "epic-1");
        Assert.Equal("Contains", Assert.Single(epic.Groups).Heading);
        Assert.Contains(epic.Groups.Single().Entries, e => e.Label == "Story 1.1");

        var story = Node(model, "1.1");
        // Outgoing Contains ("Part of" the epic) AND incoming StemmedFrom ("Work that stemmed from this").
        Assert.Contains(story.Groups, g => g.Heading == "Part of" && g.Entries.Any(e => e.Label == "Epic 1"));
        var stemmed = Assert.Single(story.Groups.Where(g => g.Kind == WorkEdgeKind.StemmedFrom));
        Assert.Equal(RelatedWorkDirection.Incoming, stemmed.Direction);
        Assert.Equal("Work that stemmed from this", stemmed.Heading);
        Assert.Contains(stemmed.Entries, e => e.Label == "Deferred item: Fix the flaky retry loop");
    }

    [Fact]
    public void Build_ResolvedDeferred_SurfacesResolvesGroupOnTheResolvingStory()
    {
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Colour contrast debt</p>", Resolved: true, ResolvingRef: "1.2",
                ResolvingHref: "epics/story-1-2.html"),
            "Deferred work", EpicNumber: 1, "follow-ups/x.html");

        var resolver = Node(Project(Geometry(deferred: new[] { slot })), "1.2");

        var group = Assert.Single(resolver.Groups.Where(g => g.Kind == WorkEdgeKind.Resolves));
        Assert.Equal(RelatedWorkDirection.Incoming, group.Direction);
        Assert.Equal("Resolved by this", group.Heading);
        Assert.Contains(group.Entries, e => e.Label == "Deferred item: Colour contrast debt");
    }

    [Fact]
    public void Build_GroupsOnlyTheFourShippedEdgeKinds_NeverAPhantomCoversOrCitesSection()
    {
        // epics.md's AC #1 prose names five kinds (…covers, cites…) but WorkEdgeKind ships four: covers/cites are
        // deliberately out of the Story 19.2 MVP draw. A section the graph cannot populate is a phantom, so the
        // projection must never manufacture one — and, being driven off the enum, it gains a fifth kind for free.
        var model = Project(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        var kinds = model.Nodes.SelectMany(n => n.Groups).Select(g => g.Kind).Distinct().ToList();
        Assert.All(kinds, k => Assert.Contains(k, Enum.GetValues<WorkEdgeKind>()));
        Assert.All(model.Nodes.SelectMany(n => n.Groups), g => Assert.NotEmpty(g.Entries));
    }

    [Fact]
    public void Heading_FallsBackForAnEdgeKindTheTableHasNotBeenTaught()
    {
        // Forward-compatibility (Dev Notes): a future covers/cites kind must render, not blank or throw.
        var future = (WorkEdgeKind)999;
        Assert.False(string.IsNullOrWhiteSpace(RelatedWork.Heading(future, RelatedWorkDirection.Outgoing)));
        Assert.False(string.IsNullOrWhiteSpace(RelatedWork.Heading(future, RelatedWorkDirection.Incoming)));
    }

    // ---- The island id bridge (Story 20.1 spike §1a) ------------------------------------------------------

    [Fact]
    public void Build_TranslatesWorkGraphIdsIntoTheIslandNamespace()
    {
        // The two id spaces are DISJOINT: the graph mints e1/s1.1, the explorer payload mints epic-1/1.1. A literal
        // join returns zero matches, so the pane must be keyed on the translated ids.
        var model = Project(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        var ids = model.Nodes.Select(n => n.IslandId).ToList();
        Assert.Contains("epic-1", ids);
        Assert.Contains("1.1", ids);
        Assert.DoesNotContain("e1", ids);
        Assert.DoesNotContain("s1.1", ids);
    }

    [Fact]
    public void Build_StoryWithoutAWedge_GetsNoSection()
    {
        // A dense or fully-done epic emits no per-story payload nodes, so that story can never BE the selection.
        // It must be dropped from the keyed sections rather than claiming a wedge the chart never drew.
        var graph = WorkGraphBuilder.Build(TwoEpicModel(),
            Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        var model = RelatedWork.Build(graph, new[] { "epic-1", "epic-2" }); // no story ids in the island

        Assert.Contains(model.Nodes, n => n.IslandId == "epic-1");
        Assert.DoesNotContain(model.Nodes, n => n.IslandId == "1.1");
    }

    [Fact]
    public void Build_UnattributedBucket_MapsToTheSunburstOrphanRoot_NotToEpicZero()
    {
        // The bucket is built with EpicNumber 0 but it is NOT epic 0 — it is the sunburst's `orphan` root, and it is
        // identified by BucketLabel so a real Epic 0 could never be mistaken for it.
        var orphanAction = new SprintActionItem("An unattributed obligation", "open", null, null);
        var graph = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(
            actions: new[] { orphanAction },
            deferred: new[] { Deferred("Orphan debt", epic: 99) }));

        var model = RelatedWork.Build(graph, new[] { "epic-1", "epic-2", RelatedWork.OrphanIslandId });

        Assert.Contains(model.Nodes, n => n.IslandId == RelatedWork.OrphanIslandId);
        Assert.DoesNotContain(model.Nodes, n => n.IslandId == "epic-0");
    }

    [Fact]
    public void Build_NodesWithNoWedgeStillAppearAsEntries()
    {
        // Every StemmedFrom/Resolves/RaisedIn edge terminates on a d*/a*/src:/res:/retro: node the sunburst never
        // drew. Those are related-work ROWS — dropping them would empty the pane of most of its content.
        var slot = Deferred("Revisit the parser", epic: 2, sourceKey: "spec-parser-hardening",
            sourceHref: "spec-parser-hardening.html");
        var model = Project(Geometry(deferred: new[] { slot }));

        var epic = Node(model, "epic-2");
        Assert.Contains(epic.Groups.SelectMany(g => g.Entries),
            e => e.Kind == WorkNodeKind.Deferred && e.Label == "Deferred item: Revisit the parser");
    }

    // ---- Dedup, guarded hrefs, caps, determinism ----------------------------------------------------------

    [Fact]
    public void Build_DedupesNodesAndEdgesAcrossPerEpicSubgraphs()
    {
        // `_workGraph` is a LIST of per-epic subgraphs, not one graph: Story 1.1 legitimately appears in Epic 1's
        // own subgraph and again in Epic 2's (as an external source). Without dedup the pane double-lists it.
        var model = Project(Geometry(deferred: new[]
        {
            Deferred("Debt in epic one", epic: 1, sourceStoryId: "1.1"),
            Deferred("Debt in epic two", epic: 2, sourceStoryId: "1.1"),
        }));

        Assert.Single(model.Nodes.Where(n => n.IslandId == "1.1"));
        var story = Node(model, "1.1");
        foreach (var group in story.Groups)
            Assert.Equal(group.Entries.Count, group.Entries.Select(e => e.Label).Distinct().Count());
    }

    [Fact]
    public void Build_NodeWithoutAPage_KeepsANullHref()
    {
        // WorkNode.Href is nullable by design; the pane renders those as non-link chips (Epic 7's guarded-href
        // discipline), so the projection must carry the null through rather than inventing a destination.
        var orphanAction = new SprintActionItem("An unattributed obligation", "open", null, null);
        var graph = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: new[] { orphanAction }));
        var model = RelatedWork.Build(graph, new[] { RelatedWork.OrphanIslandId });

        var root = Node(model, RelatedWork.OrphanIslandId);
        Assert.Null(root.Href); // the Unattributed bucket has no page of its own
        Assert.Contains(root.Groups.SelectMany(g => g.Entries), e => e.Kind == WorkNodeKind.Action);
    }

    [Fact]
    public void Build_CapsGroupEntries_AndReportsWhatItWithheld()
    {
        var many = Enumerable.Range(0, RelatedWork.MaxEntriesPerGroup + 5)
            .Select(i => Deferred($"Debt number {i}", epic: 1))
            .ToArray();

        var epic = Node(Project(Geometry(deferred: many)), "epic-1");

        var group = Assert.Single(epic.Groups.Where(g => g.Kind == WorkEdgeKind.Contains));
        Assert.Equal(RelatedWork.MaxEntriesPerGroup, group.Entries.Count);
        Assert.Equal(5, group.Hidden); // truncation is reported, never silent
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        // FR31: identical input → identical projection, including section and entry ORDER (the golden fingerprint
        // depends on it, and Dictionary enumeration order is not a contract).
        var geo = Geometry(deferred: new[]
        {
            Deferred("Debt one", epic: 1, sourceStoryId: "1.1"),
            Deferred("Debt two", epic: 2, sourceStoryId: "2.1"),
        });

        var a = Flatten(Project(geo));
        var b = Flatten(Project(geo));

        Assert.Equal(a, b);
    }

    // ---- The NFR8 omit gate -------------------------------------------------------------------------------

    [Fact]
    public void Build_EmptyOrNullGraph_YieldsAnEmptyModel_AndRendersNoPane()
    {
        // AD-4: a missing work graph must never fail generation, and the pane must be ABSENT rather than an empty
        // region — otherwise a young project ships permanent dead chrome on its home page.
        Assert.True(RelatedWork.Build(null).IsEmpty);
        Assert.True(RelatedWork.Build(WorkGraphModel.Empty).IsEmpty);
        Assert.Equal(string.Empty, RelatedWorkTemplater.RenderPane(RelatedWorkModel.Empty, "work-graph.html"));
    }

    // ---- The pane markup ----------------------------------------------------------------------------------

    [Fact]
    public void RenderPane_ShipsEveryScopeServerRendered_WithTheDesignedEmptyState()
    {
        var html = RelatedWorkTemplater.RenderPane(
            Project(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") })),
            SiteNav.WorkGraphOutputPath);

        // AC #2 / NFR8: the relationship data is in the DOM, not behind a script.
        Assert.Contains(RelatedWorkTemplater.PaneAttribute, html);
        Assert.Contains("data-related-node=\"epic-1\"", html);
        Assert.Contains("data-related-node=\"1.1\"", html);
        Assert.Contains("epics/story-1-1.html", html);
        // The designed empty state ships hidden — with JS off there is no selection, so every scope is showing and
        // "no related work" would be a lie.
        Assert.Contains("data-related-empty hidden", html);
        Assert.Contains("No related work items for this selection.", html);
        // Nothing is signalled by colour: the node kind is in the LABEL (the shared RelatedWork.NodeText
        // vocabulary), so there is no badge or tint carrying meaning that words do not.
        Assert.Contains("Deferred item: A debt", html);
        Assert.DoesNotContain("related-kind", html);
    }

    [Fact]
    public void RenderPane_WithoutAWorkGraphPage_OmitsTheLinkRatherThanEmittingADeadOne()
    {
        var html = RelatedWorkTemplater.RenderPane(
            Project(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") })),
            workGraphHref: null);

        Assert.DoesNotContain(SiteNav.WorkGraphOutputPath, html);
        Assert.Contains("data-related-node=\"epic-1\"", html);
    }

    [Fact]
    public void RenderPane_NeverEmitsRawMarkupFromAuthorControlledArtifacts()
    {
        // Two different defences, and the pane needs both. The LABEL arrives already tag-stripped (the work graph
        // summarizes a deferred item's body HTML), so no markup reaches the pane through it; the TITLE is raw
        // provenance text straight out of the artifact and is escaped on the way out. A deferred item quoting HTML
        // in its provenance line is entirely ordinary in this repo's own artifacts.
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Handle <script>alert(1)</script> in titles</p>", Resolved: false, null, null),
            ProvenanceLabel: "code review of <b>7.1</b> & friends",
            EpicNumber: 1, "follow-ups/x.html");

        var html = RelatedWorkTemplater.RenderPane(Project(Geometry(deferred: new[] { slot })), null);

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>7.1</b>", html);
        Assert.Contains("&lt;b&gt;7.1&lt;/b&gt; &amp; friends", html);
        Assert.Contains("Handle alert(1) in titles", html); // the label survives, minus its markup
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    /// <summary>Projects with the island id set the real dashboard would produce for <see cref="TwoEpicModel"/> —
    /// derived from <see cref="Charts.SunburstExplorerNodes"/> itself, so the test can never key on a wedge the
    /// chart would not draw. Note the projection's ONLY inputs are the work-graph model and this id list: there is
    /// no <see cref="ProjectCounts"/> seam to reach, which is how the "never re-counts" invariant is enforced —
    /// by construction, not by a mock.</summary>
    private static RelatedWorkModel Project(FollowUpGeometry geometry)
    {
        var epics = TwoEpicModel();
        var graph = WorkGraphBuilder.Build(epics, geometry);
        var islandIds = Charts.SunburstExplorerNodes(epics, geometry).Select(n => n.Id).ToList();
        return RelatedWork.Build(graph, islandIds);
    }

    private static RelatedWorkNode Node(RelatedWorkModel model, string islandId) =>
        Assert.Single(model.Nodes.Where(n => n.IslandId == islandId));

    private static IReadOnlyList<string> Flatten(RelatedWorkModel model) => model.Nodes
        .SelectMany(n => n.Groups.SelectMany(g => g.Entries.Select(e => $"{n.IslandId}|{g.Heading}|{e.Label}|{e.Href}")))
        .ToList();

    private static FollowUpDeferredSlot Deferred(
        string body, int epic, string? sourceStoryId = null, string? sourceHref = null, string? sourceKey = null) =>
        new(new DeferredWorkItem($"<p>{body}</p>", Resolved: false, null, null),
            "Deferred work", EpicNumber: epic, "follow-ups/x.html", sourceKey, sourceHref, sourceStoryId);

    private static FollowUpGeometry Geometry(
        IReadOnlyList<SprintActionItem>? actions = null, IReadOnlyList<FollowUpDeferredSlot>? deferred = null) =>
        new(actions ?? Array.Empty<SprintActionItem>(),
            deferred?.Count ?? 0, "deferred-work.html", SiteNav.ActionItemsOutputPath,
            new Dictionary<SprintActionItem, string>(), deferred ?? Array.Empty<FollowUpDeferredSlot>());

    private static EpicsModel TwoEpicModel() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[] { Epic(1, "Foundation", "1.1", "1.2"), Epic(2, "Second", "2.1") },
    };

    private static EpicInfo Epic(int number, string title, params string[] storyIds) => new()
    {
        Number = number,
        Title = title,
        GoalHtml = string.Empty,
        Status = EpicStatus.Drafted,
        Section = EpicSection.VerticalSlice,
        Stories = storyIds.Select(id => new StoryInfo
        {
            Id = id,
            EpicNumber = number,
            Title = $"Story {id}",
            UserStoryHtml = string.Empty,
            AcBlocksHtml = Array.Empty<string>(),
        }).ToList(),
    };
}
