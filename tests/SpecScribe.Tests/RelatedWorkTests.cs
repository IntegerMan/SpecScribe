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
        // A dense or fully-done epic emits no per-story payload nodes, so that story can never BE the selection. It
        // must be dropped from the keyed sections rather than claiming a wedge the chart never drew. Deliberately an
        // island set narrower than what the real chart would draw for this geometry (a direct unit test of
        // RelatedWork.Build's own id-set handling) — the orphan-root test below covers the real end-to-end
        // derivation via Charts.SunburstExplorerNodes instead.
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
        // identified by BucketLabel so a real Epic 0 could never be mistaken for it. Island ids are derived from the
        // SAME geometry through the real Charts.SunburstExplorerNodes call (not hand-picked), so this also proves
        // the chart actually draws the `orphan` wedge for an orphan action item.
        var epics = TwoEpicModel();
        var orphanAction = new SprintActionItem("An unattributed obligation", "open", null, null);
        var geometry = Geometry(actions: new[] { orphanAction }, deferred: new[] { Deferred("Orphan debt", epic: 99) });
        var graph = WorkGraphBuilder.Build(epics, geometry);
        var islandIds = IslandIds(epics, geometry);
        Assert.Contains(RelatedWork.OrphanIslandId, islandIds); // the chart really did draw this wedge

        var model = RelatedWork.Build(graph, islandIds);

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
        // The rail's own omit gate is covered by RenderPane_EmptyModel_RendersNothing.
    }

    // ---- The card rail (owner redesign 2026-07-24) --------------------------------------------------------

    [Fact]
    public void RenderPane_ShipsAProjectCardPlusOneCardPerScope_ServerRendered()
    {
        var html = RenderRail(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        Assert.Contains(RelatedWorkTemplater.PaneAttribute, html);
        // The default (no-selection) card carries project-level details + a prompt to pick a node.
        Assert.Contains("related-card-project", html);
        Assert.Contains("data-related-default", html);
        Assert.Contains("Select a node", html);
        // One card per SELECTABLE scope (epics + roots), full title — not the terse work-graph label.
        Assert.Contains("data-related-node=\"epic-1\"", html);
        Assert.Contains("Epic 1: Foundation", html);
        // A STORY LEAF has a card of its own. Story 20.3 folded stories into their epic because a story wedge
        // navigated on click and "a standalone story card would never be reachable via selection" — Story 20.5's
        // `select` mode is exactly what made it reachable, and the owner's 2026-07-25 verify round found the
        // consequence: selecting a story showed nothing to act on.
        //
        // Story 20.8 D1 kept the CARD and restored the FOLD: a story's relationships live once, on its epic's card,
        // while the story's own card carries its title, summary, command affordance and View-details link. The two
        // are separable and 20.5 conflated them — dropping the fold to give the story a card cost 372 duplicated
        // relationship rows and took the rail to 50.4% of the dashboard.
        Assert.Contains("data-related-node=\"1.1\"", html);
        Assert.Contains("epics/story-1-1.html", html);
        // The details link is a distinct button to the node's own page.
        Assert.Contains("related-card-more", html);
        Assert.Contains("epics/epic-1.html", html);
    }

    [Fact]
    public void RenderPane_CardCarriesTheNodesSingleAiAction_AsAReadOnlyCommandBadge()
    {
        // AC #1 / owner: one most-relevant AI action per card. Epic 1 is drafted with an unplanned story, so its
        // primary next step is create-story for that story, and the badge COPIES the command (AD-6 — read-only,
        // never mutates a planning artifact).
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });
        var html = RenderRail(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }), commands);

        Assert.Contains("related-card-action", html);
        Assert.Contains("data-copy=\"/bmad-create-story 1.1\"", html); // the epic's next-step command, copy-only
    }

    [Fact]
    public void RenderPane_KeepsTheRelationshipsAsAJsOffDetailsBlock()
    {
        // AC #2 / NFR8: with JS off the full relationships must still be on-page. They ride a native <details> the
        // CSS hides only once JS sets [data-related-ready]. The empty state ships hidden for the same reason as
        // before — with JS off there is no selection.
        var html = RenderRail(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        Assert.Contains("related-card-full", html);
        Assert.Contains("epics/story-1-1.html", html);
        Assert.Contains("data-related-empty hidden", html);
        // No colour-only signal: the node kind is in the label vocabulary, not a tinted badge.
        Assert.DoesNotContain("related-kind ", html);
    }

    [Fact]
    public void RenderPane_WithoutAWorkGraphPage_OmitsTheLinkRatherThanEmittingADeadOne()
    {
        var html = RenderRail(
            Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }),
            workGraphHref: null);

        Assert.DoesNotContain(SiteNav.WorkGraphOutputPath, html);
        Assert.Contains("data-related-node=\"epic-1\"", html);
    }

    [Fact]
    public void RenderPane_NeverEmitsRawMarkupFromAuthorControlledArtifacts()
    {
        // The LABEL arrives already tag-stripped (the work graph summarizes a deferred item's body HTML); the TITLE
        // is raw provenance text escaped on the way out. A deferred item quoting HTML in its provenance line is
        // entirely ordinary in this repo's own artifacts.
        var slot = new FollowUpDeferredSlot(
            new DeferredWorkItem("<p>Handle <script>alert(1)</script> in titles</p>", Resolved: false, null, null),
            ProvenanceLabel: "code review of <b>7.1</b> & friends",
            EpicNumber: 1, "follow-ups/x.html");

        var html = RenderRail(Geometry(deferred: new[] { slot }));

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>7.1</b>", html);
        Assert.Contains("&lt;b&gt;7.1&lt;/b&gt; &amp; friends", html);
        Assert.Contains("Handle alert(1) in titles", html);
    }

    [Fact]
    public void RenderPane_EmptyModel_RendersNothing()
    {
        var pane = RelatedWorkCards.Build(
            RelatedWorkModel.Empty, TwoEpicModel(), CommandCatalog.Empty, FollowUpGeometry.Empty,
            ProjectCounts.Empty, "SpecScribe", SiteNav.WorkGraphOutputPath);
        Assert.True(pane.IsEmpty);
        Assert.Equal(string.Empty, RelatedWorkTemplater.RenderPane(pane));
    }

    // ---- The Overflow honesty gate (Story 20.1 spike §1a rule 5) -----------------------------------------

    [Fact]
    public void RenderPane_SurfacesTheGraphsOwnOverflow_RatherThanUnderReporting()
    {
        var pane = RelatedWorkCards.Build(
            new RelatedWorkModel(Array.Empty<RelatedWorkNode>(), Overflow: 3, OverflowLabels: Array.Empty<string>()),
            TwoEpicModel(), CommandCatalog.Empty, FollowUpGeometry.Empty, ProjectCounts.Empty, "SpecScribe",
            SiteNav.WorkGraphOutputPath);

        var html = RelatedWorkTemplater.RenderPane(pane);

        Assert.Contains("related-work-overflow", html);
        Assert.Contains("3 more related items not drawn", html);
        Assert.Contains(SiteNav.WorkGraphOutputPath, html);
    }

    // ---- BmadCommands AI-action helpers (Story 20.3) ------------------------------------------------------

    [Fact]
    public void PrimaryEpicCommand_ReturnsTheFirstSuggestedCommand_ForAPendingEpic()
    {
        var epic = new EpicInfo
        {
            Number = 9, Title = "Undrafted", GoalHtml = string.Empty,
            Status = EpicStatus.Pending, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
        };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-epics-and-stories"] = "/bmad-create-epics-and-stories",
        });

        Assert.Equal("/bmad-create-epics-and-stories", WorkflowCommands.PrimaryEpicCommand(epic, commands));
    }

    [Fact]
    public void PrimaryEpicCommand_PendingGsdPhase_PrefersDiscussionWithTheNativePhaseArgument()
    {
        var epic = new EpicInfo
        {
            Number = 3, WorkflowCommandArgument = "02.1", Title = "Phase 02.1", GoalHtml = string.Empty,
            Status = EpicStatus.Pending, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
        };
        var commands = new CommandCatalog("GSD Core", new Dictionary<string, string>
        {
            ["discuss-phase"] = "/gsd:discuss-phase",
            ["create-epics-and-stories"] = "/gsd:plan-phase",
        }, usesPhaseArguments: true);

        Assert.Equal("/gsd:discuss-phase 02.1", WorkflowCommands.PrimaryEpicCommand(epic, commands));
    }

    [Fact]
    public void PrimaryEpicCommand_PendingGsdPhaseWithBlankNativeArgument_OmitsPhaseCommands()
    {
        var epic = new EpicInfo
        {
            Number = 3, WorkflowCommandArgument = "   ", Title = "Phase", GoalHtml = string.Empty,
            Status = EpicStatus.Pending, Section = EpicSection.VerticalSlice, Stories = Array.Empty<StoryInfo>(),
        };
        var commands = new CommandCatalog("GSD Core", new Dictionary<string, string>
        {
            ["discuss-phase"] = "/gsd:discuss-phase",
            ["create-epics-and-stories"] = "/gsd:plan-phase",
        }, usesPhaseArguments: true);

        Assert.Null(WorkflowCommands.PrimaryEpicCommand(epic, commands));
    }

    [Fact]
    public void PrimaryStoryCommand_PlannedGsdPlanExecutesItsPhaseInsteadOfPlanningAgain()
    {
        var plan = new StoryInfo
        {
            Id = "7.1", EpicNumber = 7, WorkflowCommandArgument = "7", Title = "Planned work",
            UserStoryHtml = string.Empty, AcBlocksHtml = Array.Empty<string>(), Status = "drafted",
            ArtifactOutputPath = "epics/story-7-1.html",
        };
        var commands = new CommandCatalog("GSD Core", new Dictionary<string, string>
        {
            ["create-story"] = "/gsd:plan-phase",
            ["dev-story"] = "/gsd:execute-phase",
        }, usesPhaseArguments: true);

        Assert.Equal("/gsd:execute-phase 7", WorkflowCommands.PrimaryStoryCommand(plan, commands));
    }

    [Fact]
    public void PrimaryEpicCommand_ReturnsNull_ForADoneEpicWithNoNextAction()
    {
        var epic = new EpicInfo
        {
            Number = 1, Title = "Foundation", GoalHtml = string.Empty,
            Status = EpicStatus.Drafted, Section = EpicSection.VerticalSlice, HasRetrospective = true,
            Stories = new[]
            {
                new StoryInfo
                {
                    Id = "1.1", EpicNumber = 1, Title = "Done story", UserStoryHtml = string.Empty,
                    AcBlocksHtml = Array.Empty<string>(), Status = "done",
                },
            },
        };
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["retrospective"] = "/bmad-retrospective",
        });

        Assert.Null(WorkflowCommands.PrimaryEpicCommand(epic, commands));
    }

    [Fact]
    public void PrimaryProjectCommand_ReturnsTheFirstSuggestedCommand_ForTheProject()
    {
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
        });

        // TwoEpicModel's Epic 1 is drafted with an undrafted Story 1.1 — the next-story-to-draft suggestion.
        Assert.Equal("/bmad-create-story 1.1", WorkflowCommands.PrimaryProjectCommand(TwoEpicModel(), commands));
    }

    [Fact]
    public void PrimaryProjectCommand_ReturnsNull_WhenTheModuleExposesNoMatchingCommand()
    {
        Assert.Null(WorkflowCommands.PrimaryProjectCommand(TwoEpicModel(), CommandCatalog.Empty));
    }

    [Fact]
    public void RenderPrimaryActionBadge_RendersACopyBadge_ForARealCommand()
    {
        var html = WorkflowCommands.RenderPrimaryActionBadge("/bmad-create-story 1.1");

        Assert.Contains("data-copy=\"/bmad-create-story 1.1\"", html);
    }

    [Fact]
    public void RenderPrimaryActionBadge_RendersNothing_ForANullOrBlankCommand()
    {
        Assert.Equal(string.Empty, WorkflowCommands.RenderPrimaryActionBadge(null));
        Assert.Equal(string.Empty, WorkflowCommands.RenderPrimaryActionBadge("  "));
    }

    // ---- Story 20.8: the completeness invariant (Task 3.4 / 5.1) -----------------------------------------

    [Fact]
    public void BuildPane_EverySelectableIdResolvesToACardOrANamedRedirect()
    {
        // THE headline invariant of Story 20.8, and the rail's analogue of
        // SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew: every id the explorer payload can
        // SELECT either has a card of its own or has a redirect that is named here, in a test, rather than
        // silently falling through to the designed empty state.
        //
        // Measured on the real portal BEFORE this story: 25 of 212 dashboard payload nodes (23 `epic-N~open`/
        // `~done` aggregates, the `unplanned` root and `unplanned~open`) selected to "No related work items" —
        // on shipped, reviewed code. This test is what stops the next payload change reintroducing that.
        var geometry = Geometry(
            deferred: new[]
            {
                Deferred("A debt", epic: 1, sourceStoryId: "1.1"),
                new FollowUpDeferredSlot(
                    new DeferredWorkItem("<p>Resolved epic-2 debt</p>", Resolved: true, null, null),
                    "Deferred work", EpicNumber: 2, "follow-ups/y.html"),
                // No epic number and no source: unattributable, which is what raises the `unplanned` ROOT — the
                // wedge RelatedWork.IslandIdFor has never mapped (it knows only Epic and Story kinds).
                new FollowUpDeferredSlot(
                    new DeferredWorkItem("<p>A one-off change</p>", Resolved: false, null, null),
                    "Deferred work", EpicNumber: null, "follow-ups/z.html"),
            },
            actions: new[] { new SprintActionItem("An unattributed obligation", "open", null, null) });

        var epics = TwoEpicModel();
        var pane = BuildPane(geometry);
        var selectable = SelectableNodes(epics, geometry);
        var byId = pane.Cards.ToDictionary(c => c.IslandId, StringComparer.Ordinal);

        // The fixture must actually EXERCISE the shapes, or this test passes by drawing nothing.
        Assert.Contains(selectable, n => n.Id.EndsWith("~open", StringComparison.Ordinal));
        Assert.Contains(selectable, n => n.Id.EndsWith("~done", StringComparison.Ordinal));
        Assert.Contains(selectable, n => n.Id == "unplanned");
        Assert.Contains(selectable, n => n.Id == RelatedWork.OrphanIslandId);

        var unresolved = new List<string>();
        foreach (var node in selectable)
        {
            // Redirect 1: the synthesized project root IS the whole view, which the rail already represents as its
            // no-selection project card — `drillTo` normalizes the id to no scope at all.
            if (node.Id == HierarchyExplorer.ProjectRootId) continue;
            // Redirect 2: `epic-N~summary` resolves to its parent epic's card (D3). Asserted EXPLICITLY below
            // rather than allowed to pass as "covered" here.
            var canonical = RelatedWorkCards.CanonicalIslandId(node.Id);
            if (!byId.ContainsKey(canonical)) unresolved.Add(node.Id);
        }

        Assert.Empty(unresolved);
    }

    [Fact]
    public void CanonicalIslandId_RedirectsOnlyTheDenseEpicSummaryWedge_ToItsParentEpic()
    {
        // The redirect asserted on its own, and its BOUNDS asserted with it: `~summary` is the only id that
        // resolves to something other than itself, so a future aggregate cannot be silently swept into an epic.
        Assert.Equal("epic-7", RelatedWorkCards.CanonicalIslandId("epic-7~summary"));
        Assert.Equal("epic-7~open", RelatedWorkCards.CanonicalIslandId("epic-7~open"));
        Assert.Equal("epic-7~done", RelatedWorkCards.CanonicalIslandId("epic-7~done"));
        Assert.Equal("epic-7", RelatedWorkCards.CanonicalIslandId("epic-7"));
        Assert.Equal("20.8", RelatedWorkCards.CanonicalIslandId("20.8"));
        Assert.Equal("unplanned~open", RelatedWorkCards.CanonicalIslandId("unplanned~open"));
        Assert.Equal("orphan~done", RelatedWorkCards.CanonicalIslandId("orphan~done"));
    }

    [Fact]
    public void RenderPane_DenseEpicSummaryWedge_GetsNoCardAndIsPublishedAsAnAliasOnItsEpic()
    {
        // The dashboard passes `expandDenseEpics: true`, so `epic-N~summary` is NOT drawn there — this exercises
        // the COLLAPSED payload every other Story 20.7 surface could hand in, and pins that the redirect travels
        // as data the client reads (`data-related-alias`) rather than as a second string rule in JS.
        var dense = new EpicsModel
        {
            OverviewHtml = string.Empty,
            RequirementsInventoryHtml = string.Empty,
            Epics = new[]
            {
                Epic(1, "Foundation",
                    Enumerable.Range(1, Charts.StoryDensityCollapseThreshold).Select(i => $"1.{i}").ToArray()),
            },
        };
        var geometry = Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") });
        var collapsed = Charts.SunburstExplorerNodes(
            dense, geometry, UnplannedWorkGeometry.From(WorkInventory.Empty, geometry, dense));

        Assert.Contains(collapsed, n => n.Id == "epic-1~summary");

        var pane = RelatedWorkCards.Build(
            RelatedWork.Build(WorkGraphBuilder.Build(dense, geometry), collapsed.Select(n => n.Id).ToList()),
            dense, CommandCatalog.Empty, geometry, ProjectCounts.Empty, "SpecScribe", "work-graph.html",
            selectableNodes: collapsed);

        Assert.DoesNotContain(pane.Cards, c => c.IslandId == "epic-1~summary");
        var epic = Assert.Single(pane.Cards, c => c.IslandId == "epic-1");
        Assert.Contains("epic-1~summary", epic.Aliases ?? Array.Empty<string>());
        Assert.Contains("data-related-alias=\"epic-1~summary\"", RelatedWorkTemplater.RenderPane(pane));
    }

    // ---- Story 20.8 D1: the story -> epic fold, restored --------------------------------------------------

    [Fact]
    public void RenderPane_StoryCard_CarriesNoRelationshipBlock_AndItsEpicCarriesItAsASubject()
    {
        // D1. Story 20.5's owner round removed the fold so a selected story had something to show; the measurement
        // that followed (443,137 B rail of an 878,971 B dashboard, 50.4%, every relationship rendered twice) is why
        // it comes back. The story card keeps its title, summary, command affordance and View-details link.
        var pane = BuildPane(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");
        Assert.Empty(story.Relationships.Groups);
        Assert.Empty(story.Relationships.Subjects);
        Assert.Equal("epics/story-1-1.html", story.DetailHref);

        var epic = Assert.Single(pane.Cards, c => c.IslandId == "epic-1");
        var subject = Assert.Single(epic.Relationships.Subjects, s => s.Label.StartsWith("Story 1.1", StringComparison.Ordinal));
        Assert.Contains(subject.Groups, g => g.Entries.Any(e => e.Label == "Deferred item: A debt"));
        // The restated "Part of -> Epic 1" group is dropped: the card it now sits inside IS Epic 1.
        Assert.DoesNotContain(subject.Groups, RelatedWork.IsRestatedContainsGroup);

        // The templater's existing `Groups.Count > 0 || Subjects.Count > 0` guard is what makes this free — no
        // "story cards have no relationships" branch was added, which would be a second rule to keep in sync.
        // Exactly one relationship block on the whole rail: Epic 1's, hosting the folded story.
        Assert.Equal(1, CountOccurrences(RelatedWorkTemplater.RenderPane(pane), "related-card-full"));
    }

    [Fact]
    public void RenderPane_NoRelationshipEntryAppearsTwiceInTheRenderedRail()
    {
        // THE assertion that pins the reversal. A future well-meaning change that "restores" story relationships
        // alongside the fold puts every entry on the page twice again — and breaks this loudly rather than only
        // showing up as a byte number nobody re-measures.
        //
        // SCOPE, stated so a green here is not over-read (measured on the real portal, 2026-07-27): the invariant
        // that actually holds site-wide is "no (card, subject, group, entry) TRIPLE repeats" — 363 triples, 362
        // distinct. Eight entry TEXTS legitimately appear under two different headings, because a deferred item
        // that stemmed from Story 6.3 and was resolved by Story 6.4 is two facts about two stories, not one fact
        // twice. The single true repeat found is a pre-existing label collision in `RelatedWork.BuildGroups`,
        // which dedupes by node id: two distinct deferred items whose first 90 summarized characters are
        // identical render as two identical rows. That is upstream of this fold and is recorded, not papered over.
        // This fixture gives each item one relationship, so here the two forms coincide.
        var html = RenderRail(Geometry(deferred: new[]
        {
            Deferred("Debt one", epic: 1, sourceStoryId: "1.1"),
            Deferred("Debt two", epic: 1, sourceStoryId: "1.2"),
            Deferred("Debt three", epic: 2, sourceStoryId: "2.1"),
        }));

        foreach (var label in new[] { "Deferred item: Debt one", "Deferred item: Debt two", "Deferred item: Debt three" })
            Assert.Equal(1, CountOccurrences(html, label));
    }

    // ---- Story 20.8 D2: more command, more children — not more relationships ------------------------------

    [Fact]
    public void RenderPane_StoryCard_KeepsThePrimaryVisible_AndPutsTheRestBehindANativeDisclosure()
    {
        var commands = new CommandCatalog("BMad", new Dictionary<string, string>
        {
            ["create-story"] = "/bmad-create-story",
            ["check-implementation-readiness"] = "/bmad-check-implementation-readiness",
        });
        var geometry = Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") });
        var pane = BuildPane(geometry, commands);

        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");
        var expectedPrimary = WorkflowCommands.PrimaryStoryCommand(
            TwoEpicModel().Epics[0].Stories[0], commands,
            geometry.DeferredForSource("1.1").Where(s => !s.Item.Resolved).ToList());
        Assert.Equal(expectedPrimary, story.PrimaryCommand);
        Assert.NotNull(expectedPrimary);

        // Entries 2..n only — never the primary again. "EpicEpic 19" / "Story Story 19.1" are the duplication
        // class Story 20.3's live round caught, and both were found by looking, not by a test.
        Assert.NotEmpty(story.MoreCommands!);
        Assert.DoesNotContain(story.MoreCommands!, c => c.Command == expectedPrimary);

        var html = RelatedWorkTemplater.RenderPane(pane);
        // A NATIVE <details>, so a script-blocked reader can still open it (AC #2). Not a .cmd-menu popout: that
        // is position:absolute with min-width:22rem, wider than the rail's own column.
        Assert.Contains("<details class=\"related-card-commands\">", html);
        Assert.DoesNotContain("cmd-menu-pop", html);
        Assert.Contains($"data-copy=\"{expectedPrimary}\"", html);
    }

    [Fact]
    public void RenderPane_StoryWithNoCommandsAtAll_EmitsNoDisclosure_NeverADeadControl()
    {
        // The module exposes nothing → BmadCommands.StoryCommands is empty → no primary badge AND no disclosure.
        var pane = BuildPane(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));

        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");
        Assert.Null(story.PrimaryCommand);
        Assert.Empty(story.MoreCommands!);
        Assert.DoesNotContain("related-card-commands", RelatedWorkTemplater.RenderPane(pane));
    }

    [Fact]
    public void RenderPane_StoryCard_ListsItsOpenDeferredChildrenByName_AndStatesTheRemainderWhenCapped()
    {
        // NB `FollowUpGeometry.DeferredForSource` matches on SOURCE KEY (`1-1-…`), not on SourceStoryId — the same
        // call EpicsViewBuilder makes for a story's deferred children. A fixture that set only SourceStoryId would
        // silently return nothing and this test would pass by drawing zero children.
        const string sourceKey = "1-1-first-story";
        var many = Enumerable.Range(1, RelatedWorkCards.MaxChildrenPerCard + 3)
            .Select(i => Deferred($"Child debt {i}", epic: 1, sourceStoryId: "1.1", sourceKey: sourceKey))
            .Concat(new[]
            {
                // Resolved children are not "open" and must not be listed.
                new FollowUpDeferredSlot(
                    new DeferredWorkItem("<p>Already handled</p>", Resolved: true, null, null),
                    "Deferred work", EpicNumber: 1, "follow-ups/x.html", sourceKey, null, "1.1"),
            })
            .ToArray();

        var pane = BuildPane(Geometry(deferred: many));
        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");

        Assert.Equal(RelatedWorkCards.MaxChildrenPerCard, story.Children!.Count);
        Assert.Equal(3, story.HiddenChildren);
        Assert.All(story.Children!, c => Assert.StartsWith("Child debt", c.Label));

        var html = RelatedWorkTemplater.RenderPane(pane);
        Assert.Contains("related-card-children", html);
        Assert.Contains("Child debt 1", html);
        Assert.Contains("+3 more not shown.", html); // stated, never truncated silently (NFR8)
        Assert.Contains("follow-ups/x.html", html);  // a real resolving link, not a bare chip

        // Scoped to the children block, not to the whole rail: the resolved item legitimately still appears as a
        // relationship ENTRY on Epic 1's card (the work graph knows about it), and asserting its absence rail-wide
        // would be asserting the wrong thing. What must be true is that the story's OPEN-follow-ups list excludes it.
        var childrenBlock = Between(html, "related-card-children", "</div>");
        Assert.DoesNotContain("Already handled", childrenBlock);
    }

    [Fact]
    public void RenderPane_StorySummaryLine_UsesTheSamePhrasingAsTheChartsOwnDetail()
    {
        // Anti-pattern 4: the chart tooltip, the text twin and the card summary must agree. HierarchyExplorer's
        // WithDetails says "No task plan yet" for an un-drafted story; the card's summary carries the same words
        // after its prose status, and neither may grow a third phrasing.
        var pane = BuildPane(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }));
        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");

        Assert.EndsWith("no task plan yet", story.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            StatusStyles.StoryLabel(StatusStyles.ForStory(TwoEpicModel().Epics[0].Stories[0])), story.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPane_ExplicitLifecycleWithoutChecklist_UsesChecklistCopy()
    {
        var epics = TwoEpicModel();
        epics.Epics[0].Stories[0].Status = "done";
        var pane = BuildPane(Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") }), epicsModel: epics);
        var story = Assert.Single(pane.Cards, c => c.IslandId == "1.1");

        Assert.StartsWith("Done", story.Summary, StringComparison.Ordinal);
        Assert.EndsWith("no task checklist available", story.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Story 20.8 D3: the aggregates and the `unplanned` root -------------------------------------------

    [Fact]
    public void RenderPane_FollowUpAggregatesAndTheUnplannedRoot_GetCards_WithAGroupPageAndNoCommand()
    {
        // REGRESSION NOTE — this is a FIX, not new behaviour. `RelatedWork.IslandIdFor` maps only
        // WorkNodeKind.Epic → `epic-{N}`/`orphan` and WorkNodeKind.Story → the bare story id, so the `unplanned`
        // ROOT matched neither and `SynthesizeNode` returned null for it: drilling into Unplanned on the live
        // portal showed the rail's designed empty state. Neither Story 20.3 nor Story 20.5 noticed, and both
        // shipped reviewed. The aggregates (`epic-N~open`/`~done`, `orphan~*`, `unplanned~*`) had the same gap.
        var geometry = Geometry(
            deferred: new[]
            {
                Deferred("A debt", epic: 1, sourceStoryId: "1.1"),
                new FollowUpDeferredSlot(
                    new DeferredWorkItem("<p>A one-off change</p>", Resolved: false, null, null),
                    "Deferred work", EpicNumber: null, "follow-ups/z.html"),
            },
            actions: new[] { new SprintActionItem("An unattributed obligation", "open", null, null) });

        var pane = BuildPane(geometry);
        var payload = SelectableNodes(TwoEpicModel(), geometry).ToDictionary(n => n.Id, StringComparer.Ordinal);

        foreach (var id in new[] { "epic-1~open", "orphan~open", "unplanned", "unplanned~open" })
        {
            Assert.True(payload.ContainsKey(id), $"fixture did not draw {id}");
            var card = Assert.Single(pane.Cards, c => c.IslandId == id);
            // The label is the PAYLOAD's, verbatim — the explorer breadcrumb and the chart's accessible name use
            // that exact wording, and a re-composition here reads as two names for one wedge.
            Assert.Equal(payload[id].Label, card.Title);
            // No single artifact to act on, so no command — the rule the Unattributed root already followed.
            Assert.Null(card.PrimaryCommand);
            Assert.Equal(payload[id].Href, card.DetailHref);
            // Status as a WORD (UX-DR17/19); the count is already in the label, so the summary never restates it.
            Assert.False(string.IsNullOrWhiteSpace(card.Summary));
        }

        Assert.Equal("Unplanned", pane.Cards.Single(c => c.IslandId == "unplanned").KindWord);
        Assert.Equal("Follow-ups", pane.Cards.Single(c => c.IslandId == "epic-1~open").KindWord);
    }

    [Fact]
    public void RenderPane_AggregateWithAnUnresolvableRoot_OmitsTheLinkRatherThanEmittingADeadOne()
    {
        // `Charts.SunburstExplorerNodes` writes `unplannedGeo.GroupRootHref ?? "#"` onto the unplanned root, so a
        // geometry with no group page hands the card layer a literal "#". A card must then render WITHOUT a
        // View-details link rather than with a link to nowhere (Epic 7's guarded-href discipline).
        //
        // Driven by handing the card layer that payload node directly rather than by contriving a geometry: on the
        // shipped path `GroupRootHref` is non-null whenever the root is drawn at all, so a geometry-level fixture
        // would exercise the happy path and quietly claim to have tested this branch.
        var geometry = Geometry(deferred: new[] { Deferred("A debt", epic: 1, sourceStoryId: "1.1") });
        var epics = TwoEpicModel();
        var selectable = SelectableNodes(epics, geometry)
            .Append(new SunburstExplorerNode(
                "unplanned", null, 1, "Unplanned: 1 direct / one-off item", "unplanned", "#", "unplanned", "epic"))
            .ToList();

        var pane = RelatedWorkCards.Build(
            Project(geometry, epics), epics, CommandCatalog.Empty, geometry, ProjectCounts.Empty, "SpecScribe",
            "work-graph.html", selectableNodes: selectable);
        var root = Assert.Single(pane.Cards, c => c.IslandId == "unplanned");

        Assert.Equal("Unplanned: 1 direct / one-off item", root.Title); // the card still renders, fully named
        Assert.Null(root.DetailHref);
        Assert.DoesNotContain("href=\"#\"", RelatedWorkTemplater.RenderPane(pane));
    }

    [Fact]
    public void RenderPane_ProjectWithNothingRelatable_StillRendersNoRail_NotARailOfEmptyAggregateCards()
    {
        // NFR8, and D3 must not weaken it: giving aggregates cards could easily have turned "no rail" into "a rail
        // of content-free cards" on a young project. The omit gate is upstream of the aggregates by construction —
        // an empty relationship projection returns before any selectable node is walked.
        var pane = RelatedWorkCards.Build(
            RelatedWorkModel.Empty, TwoEpicModel(), CommandCatalog.Empty, FollowUpGeometry.Empty,
            ProjectCounts.Empty, "SpecScribe", SiteNav.WorkGraphOutputPath,
            selectableNodes: SelectableNodes(TwoEpicModel(), FollowUpGeometry.Empty));

        Assert.True(pane.IsEmpty);
        Assert.Equal(string.Empty, RelatedWorkTemplater.RenderPane(pane));
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    /// <summary>The slice of <paramref name="haystack"/> from the first <paramref name="from"/> to the first
    /// <paramref name="to"/> after it — for scoping an assertion to one block of the rendered rail.</summary>
    private static string Between(string haystack, string from, string to)
    {
        var start = haystack.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' not found");
        var end = haystack.IndexOf(to, start, StringComparison.Ordinal);
        return end < 0 ? haystack[start..] : haystack[start..end];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }


    /// <summary>Builds the whole details rail exactly as the dashboard does — the relationship projection joined to
    /// the domain models for the card titles + primary commands.</summary>
    private static string RenderRail(
        FollowUpGeometry geometry, CommandCatalog? commands = null, string? workGraphHref = "work-graph.html",
        EpicsModel? epicsModel = null) =>
        RelatedWorkTemplater.RenderPane(BuildPane(geometry, commands, workGraphHref, epicsModel));

    /// <summary>The pane MODEL the dashboard would build, for assertions that are about the projection rather than
    /// about bytes. Same call shape as <see cref="DashboardViewBuilder"/>: the selectable payload NODES in draw
    /// order, not just their ids (Story 20.8 D3 needs each aggregate's own label and href).</summary>
    private static RelatedWorkPaneModel BuildPane(
        FollowUpGeometry geometry, CommandCatalog? commands = null, string? workGraphHref = "work-graph.html",
        EpicsModel? epicsModel = null)
    {
        var epics = epicsModel ?? TwoEpicModel();
        var rel = Project(geometry, epics);
        return RelatedWorkCards.Build(
            rel, epics, commands ?? CommandCatalog.Empty, geometry, ProjectCounts.Empty, "SpecScribe", workGraphHref,
            selectableNodes: SelectableNodes(epics, geometry));
    }

    /// <summary>The selectable payload the real dashboard draws — the same
    /// <see cref="Charts.SunburstExplorerNodes"/> call <c>DashboardViewBuilder.BuildRelatedWorkHtml</c> makes,
    /// including <see cref="UnplannedWorkGeometry"/>, so a test can never key on a wedge the chart would not
    /// draw.</summary>
    private static IReadOnlyList<SunburstExplorerNode> SelectableNodes(EpicsModel epics, FollowUpGeometry geometry)
    {
        var unplanned = UnplannedWorkGeometry.From(WorkInventory.Empty, geometry, epics);
        return Charts.SunburstExplorerNodes(epics, geometry, unplanned, expandDenseEpics: true);
    }

    /// <summary>Projects with the island id set the real dashboard would produce for <see cref="TwoEpicModel"/> —
    /// derived from the SAME 3-arg <see cref="Charts.SunburstExplorerNodes"/> call
    /// <see cref="DashboardViewBuilder.BuildRelatedWorkHtml"/> makes in production (including
    /// <see cref="UnplannedWorkGeometry"/>, which is what actually surfaces the orphan/unplanned root's island id) —
    /// so a test can never key on a wedge the chart would not draw, and a regression in how the orphan root reaches
    /// the payload would be caught here rather than only in a hand-constructed id array. Note the projection's ONLY
    /// inputs are the work-graph model and this id list: there is no <see cref="ProjectCounts"/> seam to reach,
    /// which is how the "never re-counts" invariant is enforced — by construction, not by a mock.</summary>
    private static RelatedWorkModel Project(FollowUpGeometry geometry, EpicsModel? epicsModel = null)
    {
        var epics = epicsModel ?? TwoEpicModel();
        var graph = WorkGraphBuilder.Build(epics, geometry);
        return RelatedWork.Build(graph, IslandIds(epics, geometry));
    }

    /// <summary>The island id set the real dashboard would produce for a given epics model + geometry — the same
    /// 3-arg <see cref="Charts.SunburstExplorerNodes"/> call <see cref="DashboardViewBuilder.BuildRelatedWorkHtml"/>
    /// makes in production. [Story 20.3 review]</summary>
    private static IReadOnlyList<string> IslandIds(EpicsModel epics, FollowUpGeometry geometry) =>
        SelectableNodes(epics, geometry).Select(n => n.Id).ToList();

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
