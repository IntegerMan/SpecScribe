using System.Text;

namespace SpecScribe;

/// <summary>Builds the host-neutral <see cref="DashboardView"/> from the already-projected domain models — the
/// rendering-core half of Story 6.2's dashboard decomposition. The fork/derivation logic that used to sit inline
/// in <c>HtmlTemplater.RenderIndex</c> + <c>AppendDashboard</c> lives here (which stat tile the tasks/commits fork
/// resolves to, which now/next cards the derived view yields, the overall-progress bars). The
/// <see cref="HtmlRenderAdapter"/> then maps the resulting DATA to bytes with no branching of its own (memory:
/// story-6-1-delivery-seam-live — the re-home-don't-rewrite discipline, one level down from 6.1's chrome).
/// [Story 6.2; home-index bands removed in spec-declutter-home-dashboard]</summary>
public static class DashboardViewBuilder
{
    /// <summary>The ordered WELL-KNOWN <see cref="ForgeOptions.SourceRoot"/> top-level folders (friendly title +
    /// source-path prefix), fixed order Overview → Planning → Spec Kernel → Implementation. Relocated verbatim
    /// from <c>HtmlTemplater</c> (its former <c>KnownIndexGroups</c>). Titles are unused post-declutter (home-index
    /// bands removed); the prefixes alone gate <see cref="IsWellKnownTopLevelFolder"/>. This set is SourceRoot-only —
    /// ADR roots (<see cref="ForgeOptions.AdrSourceRoot"/>, typically <c>docs/adrs</c>) never appear as SourceRoot
    /// tops, and retros live under the already-listed <c>implementation-artifacts/</c> prefix (not a top-level
    /// <c>retros/</c> folder), so do NOT "fix" all-clear diagnostics by adding <c>adrs</c>/<c>retros</c> here
    /// (that was a misdiagnosed Epic 4 debt). [Story 4.2 Task 3; relocated Story 6.2; spec-close-known-index-groups-misdiagnosis]</summary>
    private static readonly (string Title, string Prefix)[] KnownIndexGroups =
    {
        ("Overview", ""),
        ("Planning Artifacts", "planning-artifacts"),
        ("Spec Kernel", "specs"),
        ("Implementation Artifacts", BmadArtifactAdapter.ImplementationArtifactsDirName),
        // Forged ideas. The do-not-extend warning above is about `adrs`/`retros`, and its stated rationale is that
        // those are NOT SourceRoot tops — an ADR root lives at docs/adrs and retros live under
        // implementation-artifacts/, so adding them would have papered over a misdiagnosis. `forge` is
        // categorically different: it IS a SourceRoot top, written there by a CORE BMad skill
        // (`bmad-forge-idea`'s forge_output_path is "{output_folder}/forge", and {output_folder} resolves to
        // _bmad-output = SourceRoot), and Story 18.4 gives it a first-class rendered surface. The warning's
        // rationale simply does not cover this case. [Story 18.4]
        ("Ideas", IdeaDiscovery.WorkspaceRootDirName),
        // Module test artifacts. Same categorical argument as `forge` directly above, and for the same reason: it
        // IS a SourceRoot top, written there by an installed BMad module (Test Architect's `module.yaml` declares
        // `test_artifacts` default "{output_folder}/test-artifacts", and {output_folder} resolves to
        // _bmad-output = SourceRoot), and Story 18.5 gives it a first-class rendered surface. Without this entry
        // `SiteGenerator.UnrecognizedTopLevelFolders` emits "unrecognized top-level folder" for a directory this
        // story now models — a regression signal, not cosmetic. [Story 18.5]
        ("Test Artifacts", TestArtifactDerivation.ArtifactsDirName),
    };

    /// <summary>Whether a top-level <see cref="ForgeOptions.SourceRoot"/> folder is one of the well-known groups —
    /// the signal the generator uses to emit an "unrecognized structure" notice for anything else. Relocated from
    /// <c>HtmlTemplater</c>; <see cref="HtmlTemplater.IsWellKnownTopLevelFolder"/> now delegates here. Do not extend
    /// this for ADR/<c>retros</c> roots — those are not SourceRoot tops in normal BMad layout (see
    /// <c>KnownIndexGroups</c>). [Story 4.2 Task 3/5; spec-close-known-index-groups-misdiagnosis]</summary>
    public static bool IsWellKnownTopLevelFolder(string folder) =>
        KnownIndexGroups.Any(g => g.Prefix.Length > 0 && string.Equals(g.Prefix, folder, StringComparison.OrdinalIgnoreCase));

    /// <summary>Assembles the full dashboard section view model. Same inputs (and defaults) as the former
    /// <c>HtmlTemplater.RenderIndex</c> so the templater becomes a thin builder → adapter call. Summary counts
    /// come exclusively from <paramref name="counts"/> (the portal-wide ledger). [Story 6.2; Story 8.3]</summary>
    public static DashboardView Build(
        SiteNav nav,
        ProgressModel progress,
        EpicsModel? epicsModel,
        RequirementsModel? requirements,
        CommandCatalog commands,
        WorkInventory work,
        SprintStatus? sprint,
        ArtifactCoverage? coverage,
        bool hasTimeline = false,
        ProjectCounts? counts = null,
        FollowUpGeometry? followUps = null,
        UnplannedWorkGeometry? unplanned = null,
        DeliveryCadenceData? cadence = null,
        WorkGraphModel? workGraph = null,
        TestArtifactsModel? testArtifacts = null)
    {
        // Production always passes the shared SiteGenerator ledger. Null → build an equivalent ephemeral
        // ledger from the same inputs so tests/stubs that omit counts keep correct Defined/Tracked numbers.
        var ledger = counts ?? ProjectCounts.Build(progress, sprint, work, epicsModel, requirements);
        var geometry = followUps ?? FollowUpGeometry.From(
            sprint?.ActionItems ?? Array.Empty<SprintActionItem>(),
            ledger,
            work,
            epics: epicsModel);
        var unplannedGeometry = unplanned ?? UnplannedWorkGeometry.From(work, geometry, epicsModel);
        return new DashboardView
        {
            SiteTitle = nav.SiteTitle,
            StatTiles = BuildStatTiles(ledger, progress, work, epicsModel, sprint, hasTimeline, requirements),
            NowNext = BuildNowNext(epicsModel, sprint),
            Epics = epicsModel,
            Commands = commands,
            Progress = progress,
            ProgressBars = BuildProgressBars(ledger),
            Requirements = requirements,
            Coverage = coverage,
            QuickLinks = nav.QuickLinks.Select(q => new NavQuickLink(q.Label, q.OutputRelativePath, q.Description, q.Group)).ToList(),
            Work = work,
            OpenRetroActionItems = ledger.OpenActionItems,
            Counts = ledger,
            HasTimeline = hasTimeline,
            FollowUps = geometry,
            UnplannedWork = unplannedGeometry,
            // Body fragment only — HtmlRenderAdapter wraps with work-mode panel classes. [Story 9.8]
            NextStepsHtml = epicsModel is { } epics
                ? BmadCommands.RenderProjectNextStepsBody(epics, commands)
                : string.Empty,
            // Compact delivery-cadence teaser → cadence.html. Empty (omitted) when there's nothing to show. [Story 21.2]
            CadenceStripHtml = cadence is { IsEmpty: false } c
                ? Charts.DeliveryCadenceStrip(c, SiteNav.CadenceOutputPath)
                : string.Empty,
            // Compact traceability teaser → traceability.html. Empty (omitted) when there are no requirements.
            // Routed through the builder so every surface renders identical bytes (Story 6.2). [Story 21.1 review]
            TraceabilityStripHtml = requirements is { } tr && tr.Everything.Any()
                ? Charts.TraceabilityStrip(ledger.RequirementsOverall, SiteNav.TraceabilityOutputPath)
                : string.Empty,
            // Story 20.3's Related-work rail. `workGraph` is the generator's ALREADY-COMPUTED `_workGraph` handed
            // in verbatim — never rebuilt here, and no ProjectCounts/Epic 9 parser is touched for the relationship
            // half. The island id set comes from the SAME Charts.SunburstExplorerNodes projection the explorer
            // payload uses, so the rail can never key on a wedge the chart didn't draw. [Story 20.3; Story 20.1 §1a]
            RelatedWorkHtml = BuildRelatedWorkHtml(
                workGraph, epicsModel, commands, geometry, unplannedGeometry, ledger, nav.SiteTitle),
            // Story 20.5's Hierarchy Explorer — the ONE standardized sunburst/treemap component (ADR 0012). Built
            // here, not in the adapter, for the AD-2 reason its siblings above are. Story 20.7 retired the
            // server-rendered Charts.Sunburst SVG it used to sit on top of; the text twin inside this block is now
            // what a JS-off (or failed-mount) visitor reads. [Story 20.5; Story 20.7]
            HierarchyExplorerHtml = BuildHierarchyExplorerHtml(
                epicsModel, geometry, unplannedGeometry, nav.SiteTitle),
            // Story 18.5's Module Coverage panel. `testArtifacts` is the generator's ALREADY-DISCOVERED model
            // handed in verbatim — never re-discovered here, and ModuleContext.Detect is never called again
            // (Story 18.2 made detection once-per-run on purpose). Empty model ⇒ empty fragment ⇒ no panel.
            ModuleCoverageHtml = testArtifacts is { IsEmpty: false } moduleCoverage
                ? TestArtifactsTemplater.RenderModuleCoveragePanelBody(moduleCoverage)
                : string.Empty,
            // The insight surfaces as cards on Home, because Home is the ONE page with no quick-link band. See
            // DashboardView.ExploreHtml for why that mattered enough to add a panel. [field feedback 2026-08-01]
            ExploreHtml = BuildExploreHtml(nav),
            // Only when there is no epics model AT ALL — an epics.md with zero epics is a different state, already
            // answered by the explorer's "Nothing to chart yet." empty panel, and telling that project to create
            // epics would be wrong. [field feedback 2026-08-01]
            NoEpicsGuidanceHtml = epicsModel is null ? BuildNoEpicsGuidanceHtml(commands) : string.Empty,
        };
    }

    /// <summary>The <c>create-epics-and-stories</c> command slug, named once. The two surfaces that ask a user to
    /// run it (this builder's no-epics call to action and <c>HtmlRenderAdapter.AppendEmptyEpicsGuidance</c>) keep
    /// their own SENTENCES deliberately — one explains an empty list, the other an empty project — but a drifting
    /// slug would send one of them to a command that does not exist.</summary>
    internal const string CreateEpicsCommandSlug = "create-epics-and-stories";

    /// <summary>The "Explore this codebase" card grid: every <c>Insights</c> quick link as a titled card carrying
    /// its own description, so the surfaces are discoverable from Home instead of only from the nav dropdown.
    /// <para>Projected from <see cref="SiteNav.QuickLinks"/> rather than from a hand-written list, so a surface that
    /// the run did not produce can never appear here — <c>SiteNav.Build</c> is already the single place that decides
    /// which insight pages exist, and duplicating that decision is how a dashboard grows a link to a 404.</para>
    /// <para>Empty (⇒ panel omitted, NFR8) when no insight surface was generated at all. Paths are emitted
    /// verbatim; the dashboard is always at the output root, so no relative prefix applies.</para></summary>
    private static string BuildExploreHtml(SiteNav nav)
    {
        var insights = nav.QuickLinks
            .Where(q => string.Equals(q.Group, "Insights", StringComparison.Ordinal))
            .ToList();
        if (insights.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"explore-cards\">\n");
        foreach (var link in insights)
        {
            sb.Append($"  <a class=\"explore-card\" href=\"{PathUtil.Html(link.OutputRelativePath)}\">\n");
            sb.Append($"    <span class=\"explore-card-title\">{Icons.ForConcept(link.Label)}{PathUtil.Html(link.Label)}</span>\n");
            sb.Append($"    <span class=\"explore-card-desc\">{PathUtil.Html(link.Description)}</span>\n");
            sb.Append("  </a>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>The no-epics call to action: what this project is missing, and the command that produces it.
    /// <para>Falls back to a command-free sentence when the detected module does not expose the command
    /// (<see cref="BmadCommands.InlineGuidance"/>'s contract) — a portal must never instruct a user to run something
    /// that is not installed.</para></summary>
    private static string BuildNoEpicsGuidanceHtml(CommandCatalog commands)
    {
        var note = BmadCommands.InlineGuidance(
            commands.Command(CreateEpicsCommandSlug),
            "This project has no epics or stories yet. Break the plan into them with",
            "This project has no epics or stories yet — add them to your plan to track delivery here.");
        // ⚠️ A <div>, NOT a <p>, and this is a correctness requirement rather than a preference.
        //
        // `InlineGuidance` renders a command badge that contains a `<details class="send-menu">` (the "other ways
        // to send this" disclosure). The HTML parser CLOSES AN OPEN <p> when it meets a `<details>` start tag, so
        // `<p>…<details>…</details></p>` re-parses as `<p>…</p><details>…</details>` — the disclosure escapes the
        // paragraph and renders as a stray ▾ marker on its own line under the sentence. That is what it did:
        // correct in the emitted string, wrong in the DOM, and invisible to any test asserting on the markup.
        // Caught by looking at the rendered page.
        //
        // `ListRow.EmptyState` already wraps guidance in a `<div class="pending-note">` for the same reason, which
        // is why the epics-page empty state never had this bug. Same idiom here.
        return $"<div class=\"no-epics-lead\">{note}</div>\n";
    }

    /// <summary>The dashboard instance's Hierarchy Explorer configuration + payload, or "" when there are no epics.
    ///
    /// <para><b>Mode is <see cref="HierarchyMode.Select"/></b> (ADR 0012 §3: the dashboard drives a details pane).
    /// Every id this payload can select now resolves to a card or to a stated redirect: epics and the roots from
    /// Story 20.3, story leaves from Story 20.5, and the follow-up aggregates plus the <c>unplanned</c> root from
    /// Story 20.8 D3 (<c>epic-N~summary</c> redirects to its parent epic; the synthesized project root to the
    /// no-selection project card). <c>RelatedWorkTests</c>' completeness test pins that invariant, so a future
    /// payload change cannot silently reintroduce a wedge that selects to the empty state. [Story 20.8]</para>
    ///
    /// <para>The hash key stays <c>sb</c> so deep links already shared keep resolving; it is config-driven so
    /// Story 20.7's other instances can differ. The size is generous
    /// (380) because owner decision D3 chose the "Labelled explorer" direction — in-sector labels need radius —
    /// and it is config-driven so no literal lands in the JS.</para></summary>
    private static string BuildHierarchyExplorerHtml(
        EpicsModel? epicsModel,
        FollowUpGeometry geometry,
        UnplannedWorkGeometry unplannedGeometry,
        string siteTitle)
    {
        if (epicsModel is null || epicsModel.Epics.Count == 0) return string.Empty;

        var config = new HierarchyExplorerConfig(
            DomId: DashboardHierarchyDomId,
            Shape: "sunburst",
            Mode: HierarchyMode.Select,
            HashKey: "sb",
            Size: DashboardHierarchySize,
            Labels: true,
            Meta: new Charts.ChartMeta(
                Title: "Project at a Glance",
                Why: Charts.WhyText(Charts.ChartMetric.WorkHierarchy)),
            // Owner decision D4 (Story 20.6): the dashboard ALREADY carries two visible listings — the
            // `SunburstCompanionList` tile grid ("Remaining Work by Epic") and the Story 20.3 rail — so a third
            // visible listing would be on-screen duplication. The twin still discharges ADR 0013 §2's completeness
            // contract, which neither of those two can: the tile grid is epic-level only and deliberately omits
            // done epics with no open follow-ups (`Charts.SunburstCompanionList`), and the rail is a selection
            // detail pane.
            // Audited 2026-07-26 (20-6-text-twin-audit.md, surface 1): the twin's node set matches the payload
            // 212/212 and is a strict superset of the 138 real nodes the SVG draws.
            TwinDisplay: HierarchyTwinDisplay.ScreenReaderOnly);

        var model = HierarchyExplorer.ProjectDashboard(
            epicsModel, siteTitle, config, geometry, unplannedGeometry);
        // `sunburst-panel` MUST survive, and after Story 20.7 for a DIFFERENT reason than before. It no longer
        // carries the Story 3.5 hover-emphasis (those rules key on `.sb-seg`, which nothing emits once the SVG is
        // retired — see HierarchyExplorer.LegendHtml); it carries the DRILLED-LEGEND scope rules
        // `[data-explorer][data-sb-scope] .sunburst-legend .sb-legend-item`, which are the half of the legend
        // behaviour that survives, plus three StylesheetTests assertions. `data-explorer` is the root the
        // component resolves its panel from and the root the 20.3 rail re-syncs off `data-sb-scope`.
        return HierarchyExplorer.Render(
            model,
            panelClass: "chart-panel sunburst-panel wm-panel wm-show-overview wm-show-track",
            panelAttributes: " data-explorer");
    }

    /// <summary>DOM id of the dashboard's Hierarchy Explorer instance. Deliberately NOT
    /// <c>sunburst-explorer-data</c>'s id — Story 20.2's island is still live and still read by 20.2's JS block
    /// until Story 20.7 retires it. [Story 20.5]</summary>
    internal const string DashboardHierarchyDomId = "dashboard-hierarchy";

    /// <summary>Chart size for the dashboard instance. Owner decision D3 ("Labelled explorer"): a larger radius so
    /// Plotly's in-sector labels have room. Its accepted cost is competition with Story 20.3's card rail inside
    /// <c>.explorer-layout</c> — the stacking breakpoint is raised for that panel rather than the labels shrunk.</summary>
    internal const int DashboardHierarchySize = 560;

    /// <summary>Renders the Related-work details rail when the dashboard has selectable work. Kept here (not in the
    /// adapter) so every surface renders identical bytes from one path. A missing relationship graph still leaves
    /// story selection useful: cards retain title, lifecycle, task summary, command, and detail link. [Story 20.3]</summary>
    private static string BuildRelatedWorkHtml(
        WorkGraphModel? workGraph,
        EpicsModel? epicsModel,
        CommandCatalog commands,
        FollowUpGeometry geometry,
        UnplannedWorkGeometry unplannedGeometry,
        ProjectCounts counts,
        string projectTitle)
    {
        if (epicsModel is null || epicsModel.Epics.Count == 0) return string.Empty;
        // Same `expandDenseEpics: true` the Hierarchy Explorer uses, so the rail's selectable set is exactly what
        // the component can select — a story inside a dense epic included. Passing the collapsed set here would
        // give those stories a wedge to click and no card to show for it.
        var selectable = Charts.SunburstExplorerNodes(
            epicsModel, geometry, unplannedGeometry, expandDenseEpics: true);
        // linkPrefix "" — the dashboard is at the site root, so WorkGraphEpic.Reprefixed is a no-op there. The rule
        // is applied rather than assumed away, so a nested host page stays correct. [Story 20.1 spike §1a rule 6]
        var relationships = RelatedWork.Build(
            workGraph, selectable.Select(n => n.Id).ToList(), linkPrefix: string.Empty);
        // The payload NODES go on to the card builder, in draw order — not just their ids. After Story 20.5 the
        // rail owes a card to everything the chart can SELECT, and Story 20.8 D3 extended that to the follow-up
        // aggregates and the `unplanned` root, whose LABEL and HREF exist only on the payload. Taking them from
        // there rather than re-composing them is what stops the rail and the explorer breadcrumb drifting into two
        // names for one wedge. [Story 20.5; Story 20.8 D3]
        var pane = RelatedWorkCards.Build(
            relationships, epicsModel, commands, geometry, counts, projectTitle,
            workGraph is { IsEmpty: false } ? SiteNav.WorkGraphOutputPath : null,
            selectableNodes: selectable,
            includeSelectableWithoutRelationships: true);
        return RelatedWorkTemplater.RenderPane(pane);
    }

    // ----- Stat tiles ---------------------------------------------------------------------------------------

    /// <summary>The headline stat-grid row, forks resolved. Count values come from the portal-wide ledger;
    /// the fifth "Direct changes" tile still gates on <paramref name="work"/>.IsEmpty (byte-load-bearing).
    /// Git/commit tile stays on <paramref name="progress"/> (out of Story 8.3 scope). Requirement tiles lead
    /// the band when a requirements model exists so a Stakeholder entering to check FR/NFR/UX-DR progress
    /// lands on a click-through to requirements.html first. Each other tile drills to the most relevant
    /// standalone view when that page exists. [Story 6.2; Story 8.3; Story 9.2 UX]</summary>
    private static IReadOnlyList<StatTile> BuildStatTiles(
        ProjectCounts c, ProgressModel p, WorkInventory work, EpicsModel? epicsModel, SprintStatus? sprint,
        bool hasTimeline, RequirementsModel? requirements)
    {
        var epicsHref = epicsModel is { Epics.Count: > 0 } ? SiteNav.EpicsOutputPath : null;
        // Stories defined → Requirements (traceability journey); tasks still prefer the sprint board when tracked.
        var storiesHref = epicsModel is { Epics.Count: > 0 } ? SiteNav.RequirementsOutputPath : null;
        var tasksHref = sprint is { IsEmpty: false } ? SiteNav.SprintOutputPath : epicsHref;
        var commitsHref = hasTimeline ? SiteNav.TimelineOutputPath : null;
        var reqHref = SiteNav.RequirementsOutputPath;

        var tiles = new List<StatTile>();

        // Requirements lead the band — clickable entry points into the requirements journey. [Story 9.2 UX]
        if (requirements is not null)
        {
            if (requirements.Functional.Count > 0)
            {
                tiles.Add(RequirementStatTile("Functional reqs", c.RequirementsFunctional, reqHref));
            }
            if (requirements.NonFunctional.Count > 0)
            {
                tiles.Add(RequirementStatTile("Non-functional", c.RequirementsNonFunctional, reqHref));
            }
            if (requirements.Design.Count > 0)
            {
                tiles.Add(RequirementStatTile("Design reqs", c.RequirementsDesign, reqHref));
            }
        }

        tiles.Add(new($"{c.EpicsDrafted}/{c.EpicsDefined}", "Epics drafted",
            Tooltip: "Epics with at least one story drafted, out of all epics.", Href: epicsHref));
        tiles.Add(new(c.StoriesDefined.ToString(), "Stories defined", $"{c.StoriesWithArtifact} with a task plan",
            "Stories listed across every epic; the sub-line counts those with a BMad task checklist.", storiesHref));
        tiles.Add(c.TasksTotal > 0
            ? new($"{c.TasksDone}/{c.TasksTotal}", "Planned tasks done",
                Tooltip: $"Checklist tasks done across the {c.StoriesWithArtifact} stories that have a task plan — not the whole project.",
                Href: tasksHref)
            : new("—", "Planned tasks done", "none tracked yet", Href: tasksHref));
        tiles.Add(p.Git is { } git
            ? new(git.TotalCommits.ToString(), Charts.Plural(git.TotalCommits, "Commit", "Commits"), CommitStatSub(git),
                "Total commits in the repository; the sub-line shows how recently work landed.", commitsHref)
            : new("—", "Commits", "no git history"));

        if (!work.IsEmpty)
        {
            var deferredCount = c.DeferredOpenItems;
            var sub = work.Deferred is not null
                ? $"{deferredCount} deferred {Charts.Plural(deferredCount, "item", "items")}"
                : "outside the epic plan";
            tiles.Add(new(c.DirectChanges.ToString(), "Direct changes", sub,
                "Quick-dev / one-shot changes and deferred-work notes — tracked separately from the epic/story plan, never counted as epic or story completion.",
                work.Deferred?.OutputPath));
        }

        return tiles;
    }

    /// <summary>One clickable requirements-kind tile: done/total with an in-progress sub-line, drilling to
    /// requirements.html. Counts come from the portal-wide ledger (no local recount). Sub-line prefers Active,
    /// then enumerates non-zero Ready/Planned/Unmapped/Deferred/Retired so unmapped coverage is never mislabelled
    /// as "planned". [Story 9.2 UX; Story 9.9; Retired added Story 8.9 review]</summary>
    private static StatTile RequirementStatTile(string label, ProjectCounts.RequirementSatisfaction sat, string href)
    {
        var sub = sat.Active > 0
            ? $"{sat.Active} partially implemented"
            : RequirementStatSubLine(sat);
        return new($"{sat.Done}/{sat.Total}", label, sub,
            $"{label}: {sat.Done} done of {sat.Total}. Open the requirements view to refine coverage and follow the epic → story chain.",
            href);
    }

    private static string RequirementStatSubLine(ProjectCounts.RequirementSatisfaction sat)
    {
        var parts = new List<string>();
        if (sat.Ready > 0) parts.Add($"{sat.Ready} ready");
        if (sat.Planned > 0) parts.Add($"{sat.Planned} planned");
        if (sat.Unmapped > 0) parts.Add($"{sat.Unmapped} not yet mapped");
        if (sat.Deferred > 0) parts.Add($"{sat.Deferred} deferred");
        if (sat.Retired > 0) parts.Add($"{sat.Retired} retired");
        if (parts.Count > 0) return string.Join(" · ", parts);
        return sat.Done == sat.Total && sat.Total > 0 ? "all done" : "0 ready · 0 planned";
    }

    /// <summary>The commit stat's sub-line: a deterministic absolute-date recency signal (active-day count
    /// lives on the timeline/git pulse surfaces instead). Uses <see cref="PortalDates.Day"/> (never
    /// <c>DateTime.Now</c>) so a from-scratch regen of the same inputs is byte-identical. [Story 1.5 F3; Story 8.8]</summary>
    private static string CommitStatSub(GitPulse git)
        => $"last commit {PortalDates.Day(git.LastCommitDate)}";

    // ----- Overall Progress bars ----------------------------------------------------------------------------

    /// <summary>The two "Overall Progress" bars, the tasks fork resolved — values from the ledger. [Story 6.2; Story 8.3]</summary>
    private static IReadOnlyList<ProgressBarView> BuildProgressBars(ProjectCounts c) => new[]
    {
        new ProgressBarView("Planning", c.EpicsDrafted, c.EpicsDefined, $"{c.EpicsDrafted} / {c.EpicsDefined} epics"),
        c.TasksTotal > 0
            ? new ProgressBarView("Implementation", c.TasksDone, c.TasksTotal, $"{c.TasksDone} / {c.TasksTotal} tasks ({c.StoriesWithArtifact} of {c.StoriesDefined} stories planned)")
            : new ProgressBarView("Implementation", 0, 0, "not started"),
    };

    // ----- Now & Next ---------------------------------------------------------------------------------------

    /// <summary>The "Now &amp; Next" panel view, or null when it is omitted. Reproduces <c>AppendNowAndNext</c>'s
    /// gating: nothing without an epics model; the sprint board when a sprint is tracked; otherwise the derived
    /// in-dev/review/up-next/next-to-draft cards (and null when even those are empty). [Story 6.2]</summary>
    private static DashboardNowNext? BuildNowNext(EpicsModel? epicsModel, SprintStatus? sprint)
    {
        if (epicsModel is null) return null;

        if (sprint is { IsEmpty: false })
        {
            return new DashboardNowNext(sprint, Array.Empty<NowNextCard>());
        }

        var allStories = epicsModel.Epics.SelectMany(e => e.Stories.Select(s => (Epic: e, Story: s))).ToList();
        var inDev = allStories.Where(x => StatusStyles.ForStory(x.Story) == "active").ToList();
        var inReview = allStories.Where(x => StatusStyles.ForStory(x.Story) == "review").ToList();
        var upNext = allStories.Where(x => StatusStyles.ForStory(x.Story) == "ready").ToList();

        var nextStoryToDraft = allStories
            .Where(x => x.Epic.Status == EpicStatus.Drafted && StatusStyles.ForStory(x.Story) == "drafted")
            .OrderBy(x => x.Epic.Number)
            .ThenBy(x => StoryMinor(x.Story.Id))
            .Select(x => (x.Epic, x.Story))
            .FirstOrDefault();

        var nextEpicToDraft = epicsModel.Epics.OrderBy(e => e.Number).FirstOrDefault(e => e.Status == EpicStatus.Pending);

        if (inDev.Count == 0 && inReview.Count == 0 && upNext.Count == 0
            && nextStoryToDraft.Story is null && nextEpicToDraft is null) return null;

        var cards = new List<NowNextCard>();

        foreach (var (epic, story) in inDev)
        {
            cards.Add(new NowNextCard("active", "In development",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        foreach (var (epic, story) in inReview)
        {
            cards.Add(new NowNextCard("review", "In review",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        foreach (var (epic, story) in upNext)
        {
            cards.Add(new NowNextCard("ready", "Up next",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        if (nextStoryToDraft.Story is not null)
        {
            var (epic, story) = nextStoryToDraft;
            cards.Add(new NowNextCard("drafted", "Next story to draft",
                $"Story {story.Id} · {PathUtil.StripHtmlTags(story.Title)}",
                story.ArtifactOutputPath ?? $"epics/epic-{epic.Number}.html"));
        }

        if (nextEpicToDraft is not null)
        {
            cards.Add(new NowNextCard("pending", "Next epic to draft",
                $"Epic {nextEpicToDraft.Number} · {PathUtil.StripHtmlTags(nextEpicToDraft.Title)}",
                $"epics/epic-{nextEpicToDraft.Number}.html"));
        }

        return new DashboardNowNext(null, cards);
    }

    /// <summary>The "M" from a story id "N.M"; <see cref="int.MaxValue"/> for ids that don't parse (sort last).
    /// Relocated from <c>HtmlTemplater.StoryMinor</c>.</summary>
    private static int StoryMinor(string storyId)
    {
        var dot = storyId.LastIndexOf('.');
        return dot >= 0 && int.TryParse(storyId.AsSpan(dot + 1), out var minor) ? minor : int.MaxValue;
    }

}
