namespace SpecScribe;

/// <summary>The site-wide header nav (Home, module planning docs, ADRs, Epics) plus breadcrumb rendering.
/// Nav targets are discovered by well-known filename, so a missing doc is simply omitted rather than
/// producing a broken link.</summary>
public sealed class SiteNav
{
    public const string HomeOutputPath = "index.html";
    public const string EpicsOutputPath = "epics.html";
    public const string RequirementsOutputPath = "requirements.html";

    /// <summary>The dedicated requirement × covering-epic traceability matrix page. Requirements are parsed out
    /// of epics.md, so this shares the SAME <c>hasEpics</c> gate as <see cref="RequirementsOutputPath"/> — no
    /// dedicated flag — mirroring how <see cref="RiskQuadrantOutputPath"/> shares <see cref="CodeMapOutputPath"/>'s
    /// gate. Shared between the generator (writes the file) and the templater/nav (link to it) so the two can
    /// never disagree. [Story 21.1]</summary>
    public const string TraceabilityOutputPath = "traceability.html";
    public const string SprintOutputPath = "sprint.html";
    public const string RetrosOutputPath = "retros.html";
    public const string ActionItemsOutputPath = "action-items.html";
    public const string AdrsLandingOutputPath = "adrs/index.html";
    public const string ReadmeOutputPath = "readme.html";

    /// <summary>The opt-in deep-git analytics page (hotspots + change-coupling graph). Generated only when
    /// <c>--deep-git</c> is set; rides the Insights nav group when the deep-git data signal is present at
    /// nav-build time (ComputeProgress runs in the Ingest callback before <see cref="Build"/>). Shared between
    /// the generator (which writes the file) and the templater (which links to it) so the two can't disagree.
    /// [Story 3.2; 10.1]</summary>
    public const string DeepAnalyticsOutputPath = "deep-analytics.html";

    /// <summary>The opt-in aggregate Git Insights hub (file change frequency, activity over time, contributor
    /// attribution). Generated only when <c>--deep-git</c> produced deep data; rides the Insights nav group
    /// gated on <c>DeepGit.Insights</c> at nav-build time (same ingest-before-nav ordering as Deep Analytics).
    /// Shared between the generator (writes the file) and the templaters (link to it) so the two can't disagree.
    /// [Story 3.8; 10.1]</summary>
    public const string GitInsightsOutputPath = "git-insights.html";

    /// <summary>The chronological activity timeline page (heatmap + a newest-first list of active dates linking to
    /// their date pages). Written only when there is activity to show (git history OR artifact-change days); the
    /// dashboard's Git Pulse panel links here when the page exists. Deliberately NOT a top-nav item — nav is built
    /// before git is computed, so a nav entry could dangle — reached via the dashboard + breadcrumb instead. Shared
    /// between the generator (writes the file) and the templaters (link to it) so the two can't disagree. [Story 7.3]</summary>
    public const string TimelineOutputPath = "timeline.html";

    /// <summary>The source-code treemap page (files sized by lines of code, colorable by git-derived change
    /// signals). Written only when the source-code walk found readable files; the nav item and dashboard quick link
    /// gate on the same signal so a link is never emitted to a page that wasn't produced. Shared between the
    /// generator (writes the file) and the templater/nav (link to it) so the two can't disagree. Replaced the
    /// retired Story 3.4 artifact structure tree. [Story 7.6]</summary>
    public const string CodeMapOutputPath = "code-map.html";

    /// <summary>The refactor-target risk quadrant page (files plotted by size × churn frequency). Rides the SAME
    /// gating signal as <see cref="CodeMapOutputPath"/> (the cached source-code walk being non-empty) — it is a
    /// sibling insight surface over the identical data, not an independently-gated one, so a link is never
    /// emitted to a page that wasn't produced. Shared between the generator (writes the file) and the
    /// templater/nav (link to it) so the two can't disagree. [Story 7.10]</summary>
    public const string RiskQuadrantOutputPath = "risk-quadrant.html";

    /// <summary>The epic-scoped work-graph page (a directed provenance subgraph per epic + a circular-provenance
    /// query). Written only when at least one epic carries a graph signal — an attributed deferred item or an open
    /// action item; the nav entry gates on the SAME signal (surfaced by the caller, since it needs the parsed
    /// deferred/sprint data the *.md source list doesn't reveal) so the link can never dangle. Rides the Insights
    /// nav group. Shared between the generator (writes the file) and nav (links to it) so the two can't disagree.
    /// [Story 19.2]</summary>
    public const string WorkGraphOutputPath = "work-graph.html";

    /// <summary>The generation diagnostics (run-log) page: the run's non-fatal notices (unsupported/malformed/
    /// skipped artifacts + render-time errors) plus the effective configuration and detection results. Written on
    /// EVERY full run (the zero-notice case renders an all-clear state), so — unlike the git pages — its link can
    /// never dangle. Surfaced under the Help nav group as <c>Logs</c>, and still linked from About. Shared between
    /// the generator (writes the file) and the About templater (links to it) so the two can't disagree.
    /// [Story 4.8; Help nav]</summary>
    public const string DiagnosticsOutputPath = "diagnostics.html";

    /// <summary>The About page: SpecScribe's own product metadata (version/description/author/repository) plus the
    /// prominent link to the <see cref="DiagnosticsOutputPath"/> run log. Written on every full run; surfaced under
    /// the Help nav group and via the site-wide footer ("View generation details"). Shared between the generator
    /// (writes the file) and the footer (links to it) so the two can't disagree. [Story 4.8; Help nav]</summary>
    public const string AboutOutputPath = "about.html";

    /// <summary>The Spec-Driven Development / how-to-use orientation page (<c>how-to-read.html</c>). Written on
    /// EVERY full run (like About/Diagnostics) so its link can never dangle. Surfaced under Help as
    /// <c>How to use SpecScribe</c>. [Story 10.3; Help nav]</summary>
    public const string HowToReadOutputPath = "how-to-read.html";

    /// <summary>About Spec-Driven Development hub — support matrix + links to framework guides. [About SDD]</summary>
    public const string AboutSddOutputPath = "about-sdd.html";
    public const string AboutSddBmadOutputPath = "about-sdd-bmad.html";
    public const string AboutSddGdsOutputPath = "about-sdd-gds.html";
    public const string AboutSddSpecKitOutputPath = "about-sdd-speckit.html";
    public const string AboutSddGsdOutputPath = "about-sdd-gsd.html";
    public const string AboutSddGsdPiOutputPath = "about-sdd-gsd-pi.html";
    public const string AboutSddSuperpowersOutputPath = "about-sdd-superpowers.html";

    /// <summary>Flattened leaf list in render order — every child across <see cref="Groups"/> (including flat
    /// top-level links). Compatibility contract for RenderParity / SPA / Has* predicates. [Story 10.1]</summary>
    public required IReadOnlyList<(string Label, string OutputRelativePath)> Items { get; init; }

    /// <summary>Journey-organized top-nav groups. Empty-label groups are flat top-level links (Home, or a
    /// single-child collapse). [Story 10.1]</summary>
    public required IReadOnlyList<(string Label, IReadOnlyList<(string Label, string OutputRelativePath)> Children)> Groups { get; init; }

    /// <summary>Every discoverable key view for the dashboard's quick-link grid. A superset of the nav bar:
    /// it also carries module docs kept out of the top nav (brief, UX) plus a short description per entry.
    /// <c>Group</c> is the single source of truth for which white key-views band group
    /// (<see cref="HtmlRenderAdapter"/>'s Delivery/Insights/Follow-ups/Project bands) the entry belongs to —
    /// set once, here, at the same call site that also decides delivery/insights/followUps/project membership,
    /// so there is no second hand-maintained label→group classifier to drift out of sync. Quick-link-only
    /// entries with no dedicated Delivery/Insights/Follow-ups gate (How to read, Readme, module docs, ADRs,
    /// Spec kernels) all land in Project. [Story 10.1 deferred debt cleanup]</summary>
    public required IReadOnlyList<(string Label, string OutputRelativePath, string Description, string Group)> QuickLinks { get; init; }

    /// <summary>The project name (from _bmad/config.toml) — used for the nav brand and page-title suffixes.</summary>
    public required string SiteTitle { get; init; }

    public string Brand => SiteTitle;

    public bool HasEpics => Items.Any(i => i.Label == "Epics");

    public bool HasAdrs => Items.Any(i => i.Label == "ADRs");

    public bool HasReadme => Items.Any(i => i.Label == "Readme");

    public bool HasSprint => Items.Any(i => i.Label == "Sprint");

    public bool HasCodeMap => Items.Any(i => i.Label == "Code Map");

    public bool HasWorkGraph => Items.Any(i => i.Label == "Work Graph");

    /// <summary>Assembles the journey-organized top nav (Home · Delivery · Insights · Follow-ups · Project).
    /// Every child is added only when its availability signal is true; an empty group is omitted; a group with
    /// exactly one available child collapses to a flat top-level link. [Story 10.1]</summary>
    /// <remarks>
    /// Accepted tradeoff (same contract Structure/Sprint/ADRs already accept): git pages are gated on the
    /// deep-git <em>data</em> signal available at nav-build time, not on successful later render. If a git page
    /// fails to render after nav was already embedded in earlier pages, the Insights child would point at a
    /// page that wasn't written — an NFR2-exceptional degradation. Do not attempt a post-render nav rebuild.
    /// The identical tradeoff applies to Follow-ups: <c>hasActionItems</c>/<c>hasDeferredWork</c> are also
    /// data signals read before their pages render, so an Action Items or Deferred Work render failure after
    /// nav embedding carries the same dangling-link risk — gate on the signal, never attempt a rebuild.
    /// </remarks>
    public static SiteNav Build(
        IReadOnlyList<string> sourceRelativePaths,
        string siteTitle,
        IReadOnlyList<ModuleDoc>? moduleDocs = null,
        bool hasAdrs = false,
        bool hasReadme = false,
        bool hasSprint = false,
        bool hasCodeMap = false,
        bool hasGitInsights = false,
        bool hasDeepAnalytics = false,
        bool hasActionItems = false,
        bool hasDeferredWork = false,
        bool hasWorkGraph = false,
        string? deferredWorkOutputPath = null,
        List<AdapterDiagnostic>? diagnostics = null)
    {
        var delivery = new List<(string Label, string Path)>();
        var insights = new List<(string Label, string Path)>();
        var followUps = new List<(string Label, string Path)>();
        var project = new List<(string Label, string Path)>();
        var help = new List<(string Label, string Path)>();
        var quickLinks = new List<(string, string, string, string)>();

        // Help: always-written orientation + product pages. Order: How to use SpecScribe → About SDD →
        // About → Logs. Framework sub-pages ride the white local-context bar, not this dropdown. [Help nav]
        help.Add(("How to use SpecScribe", HowToReadOutputPath));
        help.Add(("About Spec-Driven Development", AboutSddOutputPath));
        help.Add(("About", AboutOutputPath));
        help.Add(("Logs", DiagnosticsOutputPath));
        quickLinks.Add(("How to use SpecScribe", HowToReadOutputPath, "Reading order and glossary for this portal.", "Help"));
        quickLinks.Add(("About Spec-Driven Development", AboutSddOutputPath, "Frameworks, support matrix, and getting started with SDD.", "Help"));
        quickLinks.Add(("About", AboutOutputPath, "SpecScribe version, build, and product details.", "Help"));
        quickLinks.Add(("Logs", DiagnosticsOutputPath, "Generation diagnostics and the run log.", "Help"));

        // The README is the project's front-door narrative — Project group, first among module docs.
        if (hasReadme)
        {
            project.Add(("Readme", ReadmeOutputPath));
            quickLinks.Add(("Readme", ReadmeOutputPath, "Read the project overview.", "Project"));
        }

        // Module docs (PRD/Architecture, or GDD/Narrative/etc.) are matched by filename anywhere in the
        // source tree, so a missing doc is simply skipped rather than producing a broken link. In-nav docs
        // ride the Project group; all discovered docs appear in the dashboard quick links. When more than one
        // file shares a well-known filename, alphabetical OrdinalIgnoreCase first-wins for the link (unchanged
        // selection rule) but the pick is no longer silent — the skipped sibling(s) surface as one Skipped
        // diagnostic so a duplicate doesn't just vanish. [spec-epic2-deferred-debt-cleanup]
        foreach (var doc in moduleDocs ?? Array.Empty<ModuleDoc>())
        {
            var matches = sourceRelativePaths
                .Where(p => string.Equals(Path.GetFileName(p), doc.FileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            var match = matches[0];
            if (matches.Count > 1)
            {
                diagnostics?.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped,
                    match,
                    $"{matches.Count - 1} duplicate '{doc.FileName}' file(s) skipped in favor of this one"));
            }

            var outputPath = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(match));
            if (doc.InNav)
            {
                project.Add((doc.Label, outputPath));
            }

            quickLinks.Add((doc.Label, outputPath, doc.Description, "Project"));
        }

        var hasEpics = sourceRelativePaths.Any(BmadArtifactAdapter.IsEpicsFile);
        if (hasEpics)
        {
            delivery.Add(("Epics", EpicsOutputPath));
            // Requirements are parsed out of epics.md, so they share its availability guard.
            delivery.Add(("Requirements", RequirementsOutputPath));
            // The traceability matrix is a sibling view over the same requirements + epics — shares this gate
            // rather than a dedicated flag so the page and its nav item can never dangle independently. [Story 21.1]
            delivery.Add(("Traceability", TraceabilityOutputPath));
            quickLinks.Add(("Epics", EpicsOutputPath, "Track epic and story delivery progress.", "Delivery"));
            quickLinks.Add(("Requirements", RequirementsOutputPath, "Review FR/NFR coverage and status.", "Delivery"));
            quickLinks.Add(("Traceability", TraceabilityOutputPath, "See the full requirement-to-epic coverage matrix.", "Delivery"));
        }

        // The sprint tracking file (sprint-status.yaml) is its own first-class delivery view, gated on the
        // file's presence exactly like ADRs/Readme — signalled by the caller since the yaml isn't in the
        // *.md source list. [Story 2.3 Task 5]
        if (hasSprint)
        {
            delivery.Add(("Sprint", SprintOutputPath));
            quickLinks.Add(("Sprint", SprintOutputPath, "See where every epic and story sits.", "Delivery"));
        }

        // Insights: deep-git pages (data signal at nav-build time) + Code Map (source-code walk).
        // Gate on the data signal — not on successful later render (accepted tradeoff; see method remarks).
        if (hasGitInsights)
        {
            insights.Add(("Git Insights", GitInsightsOutputPath));
        }

        if (hasDeepAnalytics)
        {
            insights.Add(("Deep Analytics", DeepAnalyticsOutputPath));
        }

        if (hasCodeMap)
        {
            insights.Add(("Code Map", CodeMapOutputPath));
            quickLinks.Add(("Code Map", CodeMapOutputPath, "Explore the codebase by size and change activity.", "Insights"));
            // Risk Quadrant is a sibling surface over the same source-code walk (Story 7.10) — reuses the Code
            // Map's own gating signal rather than a second flag, so the two can never dangle independently.
            insights.Add(("Risk Quadrant", RiskQuadrantOutputPath));
            quickLinks.Add(("Risk Quadrant", RiskQuadrantOutputPath, "Spot high-churn, high-size refactor targets.", "Insights"));
        }

        // The epic-scoped work graph (Story 19.2) — its own signal (a graph-bearing epic), independent of the
        // source-code walk, so it gates separately from Code Map. Ordered last in Insights.
        if (hasWorkGraph)
        {
            insights.Add(("Work Graph", WorkGraphOutputPath));
            quickLinks.Add(("Work Graph", WorkGraphOutputPath, "Trace where each epic's follow-up work came from.", "Insights"));
        }

        // Follow-ups: open retro action items + deferred-work note (NFR8 — omit when absent). Also added to
        // quickLinks (mirroring every other nav child) so the Follow-ups group in KeyViewGroupOrder can
        // actually surface them on the white key-views band, not just the dark-bar dropdown. [Story 10.1]
        if (hasActionItems)
        {
            followUps.Add(("Action Items", ActionItemsOutputPath));
            quickLinks.Add(("Action Items", ActionItemsOutputPath, "Review open retrospective action items.", "Follow-ups"));
        }

        if (hasDeferredWork && !string.IsNullOrEmpty(deferredWorkOutputPath))
        {
            var normalizedDeferredPath = PathUtil.NormalizeSlashes(deferredWorkOutputPath);
            followUps.Add(("Deferred Work", normalizedDeferredPath));
            quickLinks.Add(("Deferred Work", normalizedDeferredPath, "See work explicitly deferred for later.", "Follow-ups"));
        }

        if (hasAdrs)
        {
            quickLinks.Add(("ADRs", AdrsLandingOutputPath, "Browse architecture decisions.", "Project"));
        }

        // Spec kernels: Project-group nav children + dashboard quick-links (same presence gate).
        // [Story 2.2 Task 3; Story 10.1 Project group]
        var specKernels = sourceRelativePaths
            .Where(p => IsUnderSpecs(p) && string.Equals(Path.GetFileName(p), "SPEC.md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var multiKernel = specKernels.Count > 1;
        var folderLabels = multiKernel
            ? SpecKernelDisambiguatedLabels(specKernels)
            : null;
        for (var i = 0; i < specKernels.Count; i++)
        {
            var specKernel = specKernels[i];
            var specOutputPath = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(specKernel));
            var label = multiKernel ? $"Spec — {folderLabels![i]}" : "Spec";
            var description = multiKernel
                ? "Read this SPEC kernel and its companions."
                : "Read the canonical SPEC kernel and its companions.";
            project.Add((label, specOutputPath));
            quickLinks.Add((label, specOutputPath, description, "Project"));
        }

        // ADRs sit next to the architecture doc conceptually; they live in docs/adrs, not _bmad-output,
        // so their availability is signalled by the caller rather than the source-file list. Ordered after
        // Spec kernels to match the Design Direction taxonomy table (Readme, PRD, Architecture, Spec, ADRs).
        // [Story 10.1]
        if (hasAdrs)
        {
            project.Add(("ADRs", AdrsLandingOutputPath));
        }

        // Assemble groups: Home always flat; named groups omit when empty; single child collapses flat.
        var groups = new List<(string Label, IReadOnlyList<(string Label, string OutputRelativePath)> Children)>
        {
            ("", new List<(string, string)> { ("Home", HomeOutputPath) }),
        };
        AppendGroup(groups, "Delivery", delivery);
        AppendGroup(groups, "Insights", insights);
        AppendGroup(groups, "Follow-ups", followUps);
        AppendGroup(groups, "Project", project);
        AppendGroup(groups, "Help", help);

        var items = groups.SelectMany(g => g.Children).ToList();
        return new SiteNav { Items = items, Groups = groups, QuickLinks = quickLinks, SiteTitle = siteTitle };
    }

    /// <summary>Adds a named group when it has children; a single child collapses to a flat top-level link
    /// (empty group label) so shallow repos don't get one-item dropdowns. [Story 10.1]</summary>
    private static void AppendGroup(
        List<(string Label, IReadOnlyList<(string Label, string OutputRelativePath)> Children)> groups,
        string label,
        List<(string Label, string Path)> children)
    {
        if (children.Count == 0) return;
        if (children.Count == 1)
        {
            groups.Add(("", children.Select(c => (c.Label, c.Path)).ToList()));
            return;
        }

        groups.Add((label, children.Select(c => (c.Label, c.Path)).ToList()));
    }

    /// <summary>True when a source path lives under the <c>specs/</c> directory (the spec-kernel folder
    /// convention), keyed by directory prefix rather than the <c>spec</c> filename substring so it stays
    /// disjoint from Story 2.1's <c>implementation-artifacts/spec-*.md</c> quick-dev files. [Story 2.2 Task 1]</summary>
    private static bool IsUnderSpecs(string sourceRelativePath) =>
        PathUtil.NormalizeSlashes(sourceRelativePath).StartsWith("specs/", StringComparison.OrdinalIgnoreCase);

    /// <summary>One display suffix per multi-kernel path: prefer the immediate parent folder name, but when
    /// that name collides across kernels (or is empty), use the specs-relative directory (e.g.
    /// <c>pkg-a/core</c>) so quick-link labels stay distinguishable. [spec-epic2-deferred-debt-cleanup]</summary>
    private static IReadOnlyList<string> SpecKernelDisambiguatedLabels(IReadOnlyList<string> specKernels)
    {
        var preferred = specKernels.Select(SpecKernelFolderName).ToList();
        var collision = preferred
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1 || string.IsNullOrEmpty(g.Key));
        if (!collision) return preferred;
        return specKernels.Select(SpecKernelSpecsRelativeDir).ToList();
    }

    /// <summary>The folder directly containing a <c>SPEC.md</c> kernel (e.g. <c>specs/foo/SPEC.md</c> →
    /// <c>foo</c>). Empty when the path has no directory segment. [spec-epic2-deferred-debt-cleanup]</summary>
    private static string SpecKernelFolderName(string specSourceRelativePath)
    {
        var normalized = PathUtil.NormalizeSlashes(specSourceRelativePath);
        var dirEnd = normalized.LastIndexOf('/');
        if (dirEnd < 0) return string.Empty;
        var dir = normalized[..dirEnd];
        var parentEnd = dir.LastIndexOf('/');
        return parentEnd < 0 ? dir : dir[(parentEnd + 1)..];
    }

    /// <summary>Specs-relative directory of a kernel (e.g. <c>specs/pkg-a/core/SPEC.md</c> →
    /// <c>pkg-a/core</c>; <c>specs/SPEC.md</c> → <c>specs</c>). [spec-epic2-deferred-debt-cleanup]</summary>
    private static string SpecKernelSpecsRelativeDir(string specSourceRelativePath)
    {
        var normalized = PathUtil.NormalizeSlashes(specSourceRelativePath);
        var dirEnd = normalized.LastIndexOf('/');
        if (dirEnd < 0) return "specs";
        var dir = normalized[..dirEnd];
        if (dir.StartsWith("specs/", StringComparison.OrdinalIgnoreCase))
            return dir["specs/".Length..];
        return string.Equals(dir, "specs", StringComparison.OrdinalIgnoreCase) ? "specs" : dir;
    }

    /// <summary>True when <paramref name="sourceRelativePaths"/> contains a <c>deferred-work.md</c> (any folder).
    /// Returns the output-relative HTML path for the first match (alphabetical OrdinalIgnoreCase). [Story 10.1]</summary>
    public static string? FindDeferredWorkOutputPath(
        IReadOnlyList<string> sourceRelativePaths, List<AdapterDiagnostic>? diagnostics = null)
    {
        var matches = sourceRelativePaths
            .Where(p => string.Equals(Path.GetFileName(p), "deferred-work.md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matches.Count == 0) return null;

        if (matches.Count > 1)
        {
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Skipped,
                matches[0],
                $"{matches.Count - 1} duplicate 'deferred-work.md' file(s) skipped in favor of this one"));
        }

        return PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(matches[0]));
    }

    /// <summary>Projects this nav's already host-neutral data into the typed <see cref="NavigationView"/> the
    /// render adapters consume, with <paramref name="activeOutputRelativePath"/> marking the current page. The
    /// icon concept key is the item's label (the mapping <see cref="RenderNavBar"/> always used); a non-HTML
    /// surface reads it without re-deriving it. This is the "named typed view of SiteNav's data" the delivery
    /// contract (AD-2) needs — <see cref="Build"/> stays the producer. [Story 6.1; 10.1]</summary>
    public NavigationView ToNavigationView(string activeOutputRelativePath, NavLocalContext? localContext = null) => new()
    {
        SiteTitle = SiteTitle,
        Groups = Groups.Select(g => new NavGroup(
            g.Label,
            string.IsNullOrEmpty(g.Label)
                ? (g.Children.Count > 0 ? g.Children[0].Label : "")
                : g.Label,
            g.Children.Select(c => new NavItem(c.Label, c.OutputRelativePath, c.Label)).ToList())).ToList(),
        Items = Items.Select(i => new NavItem(i.Label, i.OutputRelativePath, i.Label)).ToList(),
        QuickLinks = QuickLinks.Select(q => new NavQuickLink(q.Label, q.OutputRelativePath, q.Description, q.Group)).ToList(),
        ActiveOutputRelativePath = activeOutputRelativePath,
        LocalContext = localContext,
    };

    /// <summary>Builds the white sub-header band's local context for an Insights page (<c>git-insights.html</c>,
    /// <c>deep-analytics.html</c>, <c>code-map.html</c>) from THIS SAME Insights nav group's membership — the
    /// exact list <see cref="Build"/> already computed for the dark bar, not a parallel query. Null when the
    /// Insights group doesn't exist or collapsed to a single flat link (nothing to navigate between — NFR8: a
    /// degenerate one-item band is not "meaningful local context"). [Story 10.10]</summary>
    public NavLocalContext? BuildInsightsLocalContext(string activeOutputRelativePath)
    {
        var group = Groups.FirstOrDefault(g => string.Equals(g.Label, "Insights", StringComparison.Ordinal));
        if (group.Children is null || group.Children.Count < 2) return null;

        var prefix = PathUtil.RelativePrefix(activeOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(activeOutputRelativePath);
        var items = group.Children
            .Select(c => new NavLocalItem(
                c.Label,
                prefix + c.OutputRelativePath,
                string.Equals(PathUtil.NormalizeSlashes(c.OutputRelativePath), current, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new NavLocalContext("Insights", items);
    }

    /// <summary>Builds the white sub-header band's local context for a Delivery-group index page (<c>epics.html</c>,
    /// <c>requirements.html</c>, <c>traceability.html</c>, <c>sprint.html</c>) from THIS SAME Delivery nav group's
    /// membership — mirrors <see cref="BuildInsightsLocalContext"/> exactly. Without this, these pages fell back to
    /// the generic quick-links band, which just re-lists the same Delivery/Insights/Follow-ups/Project/Help groups
    /// already in the dark bar above — redundant chrome rather than useful page-type wayfinding. Null when the
    /// Delivery group doesn't exist or collapsed to a single flat link (NFR8). [Story 21.1 follow-up]</summary>
    public NavLocalContext? BuildDeliveryLocalContext(string activeOutputRelativePath)
    {
        var group = Groups.FirstOrDefault(g => string.Equals(g.Label, "Delivery", StringComparison.Ordinal));
        if (group.Children is null || group.Children.Count < 2) return null;

        var prefix = PathUtil.RelativePrefix(activeOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(activeOutputRelativePath);
        var items = group.Children
            .Select(c => new NavLocalItem(
                c.Label,
                prefix + c.OutputRelativePath,
                string.Equals(PathUtil.NormalizeSlashes(c.OutputRelativePath), current, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return new NavLocalContext("Delivery", items);
    }

    /// <summary>White-bar local context for About Spec-Driven Development hub + framework sub-pages —
    /// Overview plus every framework guide. Always meaningful (7+ items). [About SDD]</summary>
    public NavLocalContext BuildSddLocalContext(string activeOutputRelativePath)
    {
        var prefix = PathUtil.RelativePrefix(activeOutputRelativePath);
        var current = PathUtil.NormalizeSlashes(activeOutputRelativePath);
        var items = new List<NavLocalItem>
        {
            new("Overview", prefix + AboutSddOutputPath,
                string.Equals(current, AboutSddOutputPath, StringComparison.OrdinalIgnoreCase)),
        };
        foreach (var fw in AboutSddTemplater.Frameworks)
        {
            items.Add(new NavLocalItem(
                fw.Label,
                prefix + fw.OutputPath,
                string.Equals(current, PathUtil.NormalizeSlashes(fw.OutputPath), StringComparison.OrdinalIgnoreCase)));
        }
        return new NavLocalContext("Spec-Driven Development", items);
    }

    /// <summary>Renders the site nav bar. The string-building was re-homed behind
    /// <see cref="HtmlRenderAdapter"/> in Story 6.1 (the DELIVERY seam); this now projects to a
    /// <see cref="NavigationView"/> and delegates, so the bytes are unchanged and un-migrated pages keep calling
    /// this exactly as before. The optional <paramref name="localContext"/> lets standalone-templater call sites
    /// (code files, requirements, follow-ups, ADRs, commits, insight pages) supply the white band's page-type
    /// context without hand-building a <see cref="NavigationView"/> themselves. [Story 10.10]</summary>
    public string RenderNavBar(string currentOutputRelativePath, NavLocalContext? localContext = null) =>
        HtmlRenderAdapter.Shared.RenderNav(ToNavigationView(currentOutputRelativePath, localContext));

    /// <summary>Renders a "Home / Epics / Epic 1 / Story 1.1" trail. The last entry (current page) should
    /// have a null path so it renders as plain text rather than a self-link. Re-homed behind
    /// <see cref="HtmlRenderAdapter"/> in Story 6.1; delegates so the bytes are unchanged.</summary>
    public static string RenderBreadcrumb(string currentOutputRelativePath, IReadOnlyList<(string Label, string? OutputRelativePath)> trail) =>
        HtmlRenderAdapter.Shared.RenderBreadcrumb(currentOutputRelativePath, BreadcrumbTrail.From(trail));

    /// <summary>Renders the breadcrumb + sibling pager as one coherent wayfinding strip — the standalone-templater
    /// counterpart of <see cref="HtmlRenderAdapter.RenderWayfinding"/> for the pages that build their own shell
    /// rather than going through a <see cref="PageView"/> (code files, commits, retros, generic docs). Delegates
    /// so the bytes match the <see cref="PageView"/> family exactly. [Story 10.11]</summary>
    public static string RenderWayfinding(string currentOutputRelativePath, IReadOnlyList<(string Label, string? OutputRelativePath)> trail, EntityPager? pager) =>
        HtmlRenderAdapter.Shared.RenderWayfinding(currentOutputRelativePath, BreadcrumbTrail.From(trail), pager);
}
