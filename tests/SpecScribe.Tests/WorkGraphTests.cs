using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 19.2 — the epic-scoped work-graph projection, cycle query, SVG builder, page templater, and
/// the NFR8 nav/omit gate.</summary>
public class WorkGraphTests
{
    // ---- Builder: node/edge projection -------------------------------------------------------------------

    [Fact]
    public void Build_DeferredFromInEpicStory_ProjectsStemmedFromAndContains()
    {
        var slot = Deferred("Fix the flaky retry loop", epic: 1, sourceStoryId: "1.1",
            sourceHref: "epics/story-1-1.html", sourceKey: "1-1-foundation");
        var geo = Geometry(deferred: new[] { slot });

        var model = WorkGraphBuilder.Build(TwoEpicModel(), geo);

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        // Nodes: Epic 1, Story 1.1, the deferred item.
        Assert.Contains(epic.Nodes, n => n.Kind == WorkNodeKind.Epic && n.Id == "e1");
        Assert.Contains(epic.Nodes, n => n.Kind == WorkNodeKind.Story && n.Id == "s1.1");
        Assert.Contains(epic.Nodes, n => n.Kind == WorkNodeKind.Deferred);
        // Story 1.1 → Epic 1 (contains); deferred → Story 1.1 (stemmed-from).
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.Contains && e.FromId == "s1.1" && e.ToId == "e1");
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.StemmedFrom && e.ToId == "s1.1");
        // Anchored via the story → no redundant deferred→epic containment edge.
        Assert.DoesNotContain(epic.Edges, e => e.Kind == WorkEdgeKind.Contains && e.FromId.StartsWith("d"));
    }

    [Fact]
    public void Build_DeferredFromSpec_ProjectsSpecSourceAndRootsToEpic()
    {
        var slot = Deferred("Revisit the parser", epic: 2, sourceKey: "spec-parser-hardening",
            sourceHref: "spec-parser-hardening.html");
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 2));
        var spec = Assert.Single(epic.Nodes.Where(n => n.Kind == WorkNodeKind.Spec));
        Assert.Equal("spec-parser-hardening", spec.Label);
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.StemmedFrom && e.ToId == spec.Id);
        // No in-epic story parent → the deferred item is rooted to its epic by a Contains edge.
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.Contains && e.ToId == "e2");
    }

    [Fact]
    public void Build_ResolvedDeferred_ProjectsResolvesEdge()
    {
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Colour contrast debt</p>", Resolved: true, ResolvingRef: "1.2",
                ResolvingHref: "epics/story-1-2.html"),
            "Deferred work", EpicNumber: 1, "follow-ups/x.html");
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.Resolves);
        Assert.Contains(epic.Nodes, n => n.Kind == WorkNodeKind.Story && n.Label == "Story 1.2");
    }

    [Fact]
    public void Build_OpenActionItem_ProjectsActionContainsEdge_DoneIsExcluded()
    {
        var actions = new[]
        {
            new SprintActionItem("Open follow-up in epic one", "open", 1, null),
            new SprintActionItem("Already closed", "done", 1, null),
        };
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: actions));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        var action = Assert.Single(epic.Nodes.Where(n => n.Kind == WorkNodeKind.Action));
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.Contains && e.FromId == action.Id && e.ToId == "e1");
        // The done item is not a node.
        Assert.DoesNotContain(epic.Nodes, n => n.Label.Contains("closed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_NearDuplicateActionsAcrossEpics_ProjectRaisedInSoftEdges()
    {
        const string shared = "Address the heatmap colour contrast accessibility debt carried across retrospectives";
        var actions = new[]
        {
            new SprintActionItem(shared, "open", 1, null),
            new SprintActionItem(shared, "open", 2, null),
        };
        var retroMap = new Dictionary<int, string> { [1] = "retros/epic-1.html", [2] = "retros/epic-2.html" };

        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: actions), retroMap);

        var epic1 = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        Assert.Contains(epic1.Edges, e => e.Kind == WorkEdgeKind.RaisedIn);
        var retro = Assert.Single(epic1.Nodes.Where(n => n.Kind == WorkNodeKind.Retro));
        Assert.Equal("retros/epic-2.html", retro.Href);
    }

    // ---- Builder: NFR8 gate -----------------------------------------------------------------------------

    [Fact]
    public void Build_EpicWithNoFollowUps_IsOmitted_StructuralContainmentIsNotSignal()
    {
        // Deferred/action only in epic 1; epic 2 has stories but no follow-ups → epic 2 is not in the model.
        var slot = Deferred("Only epic one has debt", epic: 1);
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        Assert.DoesNotContain(model.Epics, e => e.EpicNumber == 2);
        Assert.Contains(model.Epics, e => e.EpicNumber == 1);
    }

    [Fact]
    public void Build_UnattributedFollowUps_RenderInSyntheticBucket_NotDropped()
    {
        // D1 (19.1 review): null-epic + ghost-epic follow-ups must surface in an "Unattributed" pseudo-epic bucket.
        var orphanDeferred = Deferred("Orphaned debt with no epic", epic: 0) with { EpicNumber = null };
        var actions = new[]
        {
            new SprintActionItem("Attributed", "open", 1, null),
            new SprintActionItem("Orphan action, no epic", "open", null, null),
            new SprintActionItem("Ghost epic action", "open", 99, null), // epic 99 doesn't exist → unattributed
        };
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: actions, deferred: new[] { orphanDeferred }));

        var bucket = Assert.Single(model.Epics.Where(e => e.BucketLabel is not null));
        Assert.Equal("Unattributed", bucket.DisplayName);
        Assert.Equal("wg-unattributed", bucket.Anchor);
        Assert.Contains(bucket.Nodes, n => n.Kind == WorkNodeKind.Deferred);
        // Both the null-epic and the ghost-epic(99) action land in the bucket.
        Assert.Equal(2, bucket.Nodes.Count(n => n.Kind == WorkNodeKind.Action));
    }

    [Fact]
    public void Build_NoUnattributedWork_OmitsBucket()
    {
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: new[]
        {
            new SprintActionItem("Only attributed", "open", 1, null),
        }));
        Assert.DoesNotContain(model.Epics, e => e.BucketLabel is not null);
    }

    [Fact]
    public void Build_UnresolvedSourceKey_DoesNotMintPhantomNode()
    {
        // D4 (19.1 review): a SourceKey that resolves to no page (SourceHref null) must not create a node from the
        // raw string — the edge is dropped and the item roots to its epic instead.
        var slot = Deferred("Debt from an ungenerated spec", epic: 1, sourceKey: "spec-does-not-exist",
            sourceHref: null);
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        Assert.DoesNotContain(epic.Nodes, n => n.Kind == WorkNodeKind.Spec);
        Assert.DoesNotContain(epic.Edges, e => e.Kind == WorkEdgeKind.StemmedFrom);
        // The unrooted deferred still attaches to its epic (not lost).
        Assert.Contains(epic.Edges, e => e.Kind == WorkEdgeKind.Contains && e.ToId == "e1");
    }

    [Fact]
    public void Build_ResolvedSourceKey_MintsSourceNode()
    {
        var slot = Deferred("Debt from a real spec", epic: 1, sourceKey: "spec-real",
            sourceHref: "spec-real.html");
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        Assert.Contains(epic.Nodes, n => n.Kind == WorkNodeKind.Spec && n.Label == "spec-real");
    }

    [Fact]
    public void Build_NoSignalAnywhere_IsEmpty()
    {
        Assert.True(WorkGraphBuilder.Build(TwoEpicModel(), Geometry()).IsEmpty);
    }

    [Fact]
    public void Build_NullOrEmptyInputs_AreEmpty()
    {
        Assert.True(WorkGraphBuilder.Build(null, Geometry()).IsEmpty);
        Assert.True(WorkGraphBuilder.Build(TwoEpicModel(), null).IsEmpty);
    }

    [Fact]
    public void Build_IsDeterministic_SameInputSameOrder()
    {
        var slot = Deferred("Deterministic", epic: 1, sourceStoryId: "1.1");
        var a = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));
        var b = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));
        Assert.Equal(
            a.Epics.SelectMany(e => e.Nodes.Select(n => n.Id)),
            b.Epics.SelectMany(e => e.Nodes.Select(n => n.Id)));
    }

    // ---- Cycle query ------------------------------------------------------------------------------------

    [Fact]
    public void FindCycles_DetectsSimpleDirectedCycle()
    {
        var edges = new[]
        {
            new WorkEdge("a", "b", WorkEdgeKind.StemmedFrom),
            new WorkEdge("b", "c", WorkEdgeKind.StemmedFrom),
            new WorkEdge("c", "a", WorkEdgeKind.Resolves),
        };
        var cycles = WorkGraphBuilder.FindCycles(new[] { "a", "b", "c" }, edges);
        var cycle = Assert.Single(cycles);
        Assert.Equal(3, cycle.Count);
        Assert.Contains("a", cycle);
        Assert.Contains("b", cycle);
        Assert.Contains("c", cycle);
    }

    [Fact]
    public void FindCycles_AcyclicGraph_IsEmpty()
    {
        var edges = new[]
        {
            new WorkEdge("a", "b", WorkEdgeKind.StemmedFrom),
            new WorkEdge("b", "c", WorkEdgeKind.StemmedFrom),
        };
        Assert.Empty(WorkGraphBuilder.FindCycles(new[] { "a", "b", "c" }, edges));
    }

    [Fact]
    public void FindCycles_DeduplicatesRotations()
    {
        var edges = new[]
        {
            new WorkEdge("a", "b", WorkEdgeKind.StemmedFrom),
            new WorkEdge("b", "a", WorkEdgeKind.StemmedFrom),
        };
        // The 2-cycle a↔b is reachable from both a and b but reported once.
        Assert.Single(WorkGraphBuilder.FindCycles(new[] { "a", "b" }, edges));
    }

    [Fact]
    public void Build_ProjectedGraph_IsAcyclicByConstruction()
    {
        // The carrier→target projection always flows from follow-up items to the artifacts they reference; artifacts
        // never point back, so a projected epic subgraph is a DAG even when a deferred item both stems from and is
        // resolved by the same story. The cycle finder is still wired (correct + future-proof) but reports none here.
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Self-referential debt</p>", Resolved: true, ResolvingRef: "1.1",
                ResolvingHref: "epics/story-1-1.html"),
            "Deferred from Story 1.1", EpicNumber: 1, "follow-ups/x.html",
            SourceStoryId: "1.1", SourceHref: "epics/story-1-1.html", SourceKey: "1-1-foundation");
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { slot }));

        var epic = Assert.Single(model.Epics.Where(e => e.EpicNumber == 1));
        Assert.Empty(epic.Cycles);
    }

    [Fact]
    public void Templater_AmbiguousObligationAcrossThreeEpics_SurfacesInQueryPanel()
    {
        // An obligation raised in three epics leaves epic 1's action with two raised-in targets (epics 2 and 3) —
        // the "which retro owns this?" ambiguity (Story 19.1 query #2), the query that fires on real data.
        const string shared = "Address the heatmap colour contrast accessibility debt carried across retrospectives";
        var actions = new[]
        {
            new SprintActionItem(shared, "open", 1, null),
            new SprintActionItem(shared, "open", 2, null),
            new SprintActionItem(shared, "open", 3, null),
        };
        var retroMap = new Dictionary<int, string> { [2] = "retros/epic-2.html", [3] = "retros/epic-3.html" };
        var model = WorkGraphBuilder.Build(ThreeEpicModel(), Geometry(actions: actions), retroMap);
        var nav = SiteNav.Build(new[] { "epics.md" }, "TestProj", hasWorkGraph: true);

        var html = WorkGraphTemplater.RenderPage(model, nav);
        Assert.Contains("Ambiguous ownership", html);
    }

    // ---- Charts.WorkGraph SVG ---------------------------------------------------------------------------

    [Fact]
    public void WorkGraph_Svg_IsDeterministicAndAccessible()
    {
        var epic = OneDeferredEpic();
        var svg1 = Charts.WorkGraph(epic);
        var svg2 = Charts.WorkGraph(epic);
        Assert.Equal(svg1, svg2);
        Assert.Contains("role=\"img\"", svg1);
        Assert.Contains("marker-end=\"url(#work-arrow)\"", svg1);
        Assert.Contains("work-node-epic", svg1);
        Assert.Contains("aria-hidden=\"true\"", svg1); // decorative marker defs
    }

    [Fact]
    public void WorkGraph_EmptyEpic_RendersNothing()
    {
        var empty = new WorkGraphEpic(9, "Empty", Array.Empty<WorkNode>(), Array.Empty<WorkEdge>(),
            Array.Empty<IReadOnlyList<string>>());
        Assert.Equal(string.Empty, Charts.WorkGraph(empty));
    }

    [Fact]
    public void WorkGraph_SoftEdge_UsesDashedClass()
    {
        const string shared = "Address the heatmap colour contrast accessibility debt carried across retrospectives";
        var actions = new[]
        {
            new SprintActionItem(shared, "open", 1, null),
            new SprintActionItem(shared, "open", 2, null),
        };
        var retroMap = new Dictionary<int, string> { [2] = "retros/epic-2.html" };
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(actions: actions), retroMap);
        var epic1 = model.Epics.Single(e => e.EpicNumber == 1);
        Assert.Contains("work-edge-soft", Charts.WorkGraph(epic1));
    }

    // ---- Templater page ---------------------------------------------------------------------------------

    [Fact]
    public void Templater_RendersScopePicker_SrEnumeration_AndCyclePanel()
    {
        var model = WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { Deferred("A debt", epic: 1) }));
        var nav = SiteNav.Build(new[] { "epics.md" }, "TestProj", hasWorkGraph: true);

        var html = WorkGraphTemplater.RenderPage(model, nav);

        Assert.Contains("<main id=\"main-content\"", html);          // SPA capture seam / parity
        Assert.Contains("work-graph-scope", html);                    // scope picker
        Assert.Contains("href=\"#wg-epic-1\"", html);
        Assert.Contains("work-graph nodes", html);                    // sr-only enumeration heading
        Assert.Contains("No circular or ambiguous provenance in this scope.", html); // honest empty query
    }

    // ---- Nav gate ---------------------------------------------------------------------------------------

    [Fact]
    public void Nav_WorkGraphEntry_GatedOnSignal()
    {
        Assert.True(SiteNav.Build(new[] { "epics.md" }, "T", hasWorkGraph: true).HasWorkGraph);
        Assert.False(SiteNav.Build(new[] { "epics.md" }, "T", hasWorkGraph: false).HasWorkGraph);
    }

    // ---- Fixtures ---------------------------------------------------------------------------------------

    private static FollowUpDeferredSlot Deferred(
        string body, int epic, string? sourceStoryId = null, string? sourceHref = null, string? sourceKey = null) =>
        new(new DeferredWorkItem($"<p>{body}</p>", Resolved: false, null, null),
            "Deferred work", EpicNumber: epic, "follow-ups/x.html", sourceKey, sourceHref, sourceStoryId);

    private static FollowUpGeometry Geometry(
        IReadOnlyList<SprintActionItem>? actions = null, IReadOnlyList<FollowUpDeferredSlot>? deferred = null) =>
        new(actions ?? Array.Empty<SprintActionItem>(),
            deferred?.Count ?? 0, "deferred-work.html", SiteNav.ActionItemsOutputPath,
            new Dictionary<SprintActionItem, string>(), deferred ?? Array.Empty<FollowUpDeferredSlot>());

    private static WorkGraphEpic OneDeferredEpic() =>
        WorkGraphBuilder.Build(TwoEpicModel(), Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }))
            .Epics.Single(e => e.EpicNumber == 1);

    private static EpicsModel TwoEpicModel() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[]
        {
            Epic(1, "Foundation", "1.1", "1.2"),
            Epic(2, "Second", "2.1"),
        },
    };

    private static EpicsModel ThreeEpicModel() => new()
    {
        OverviewHtml = string.Empty,
        RequirementsInventoryHtml = string.Empty,
        Epics = new[] { Epic(1, "Foundation", "1.1"), Epic(2, "Second", "2.1"), Epic(3, "Third", "3.1") },
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
