namespace SpecScribe;

/// <summary>The site-wide header nav (Home, module planning docs, ADRs, Epics) plus breadcrumb rendering.
/// Nav targets are discovered by well-known filename, so a missing doc is simply omitted rather than
/// producing a broken link.</summary>
public sealed class SiteNav
{
    public const string HomeOutputPath = "index.html";
    public const string EpicsOutputPath = "epics.html";
    public const string RequirementsOutputPath = "requirements.html";
    public const string SprintOutputPath = "sprint.html";
    public const string RetrosOutputPath = "retros.html";
    public const string ActionItemsOutputPath = "action-items.html";
    public const string AdrsLandingOutputPath = "adrs/index.html";
    public const string ReadmeOutputPath = "readme.html";

    /// <summary>The opt-in deep-git analytics page (hotspots + change-coupling graph). Generated only when
    /// <c>--deep-git</c> is set; the dashboard's Git Pulse panel links here when the data exists. Shared between
    /// the generator (which writes the file) and the templater (which links to it) so the two can't disagree. [Story 3.2]</summary>
    public const string DeepAnalyticsOutputPath = "deep-analytics.html";

    /// <summary>The opt-in aggregate Git Insights hub (file change frequency, activity over time, contributor
    /// attribution). Generated only when <c>--deep-git</c> produced deep data; reached from the dashboard's Git
    /// Pulse panel (not the top nav — nav is built before git is computed, so a nav entry could dangle). Shared
    /// between the generator (writes the file) and the templaters (link to it) so the two can't disagree. [Story 3.8]</summary>
    public const string GitInsightsOutputPath = "git-insights.html";

    /// <summary>The chronological activity timeline page (heatmap + a newest-first list of active dates linking to
    /// their date pages). Written only when there is activity to show (git history OR artifact-change days); the
    /// dashboard's Git Pulse panel links here when the page exists. Deliberately NOT a top-nav item — nav is built
    /// before git is computed, so a nav entry could dangle — reached via the dashboard + breadcrumb instead. Shared
    /// between the generator (writes the file) and the templaters (link to it) so the two can't disagree. [Story 7.3]</summary>
    public const string TimelineOutputPath = "timeline.html";

    /// <summary>The interactive project/artifact structure tree page. Written only when the source-artifact file
    /// set is non-empty; the nav item and dashboard quick link gate on the same signal so a link is never emitted
    /// to a page that wasn't produced. Shared between the generator (writes the file) and the templater/nav
    /// (link to it) so the two can't disagree. [Story 3.4]</summary>
    public const string StructureOutputPath = "structure.html";

    /// <summary>The generation diagnostics (run-log) page: the run's non-fatal notices (unsupported/malformed/
    /// skipped artifacts + render-time errors) plus the effective configuration and detection results. Written on
    /// EVERY full run (the zero-notice case renders an all-clear state), so — unlike the git pages — its link can
    /// never dangle. Deliberately NOT a top-nav item: it is reached via the footer → About → Diagnostics path, so
    /// it stays out of <see cref="Build"/>'s <c>Items</c>/<c>QuickLinks</c>. Shared between the generator (writes
    /// the file) and the About templater (links to it) so the two can't disagree. [Story 4.8]</summary>
    public const string DiagnosticsOutputPath = "diagnostics.html";

    /// <summary>The About page: SpecScribe's own product metadata (version/description/author/repository) plus the
    /// prominent link to the <see cref="DiagnosticsOutputPath"/> run log. It is the owner-chosen reachability path
    /// for the diagnostics page — linked from the site-wide footer, written on every full run. Like
    /// <see cref="DiagnosticsOutputPath"/> it is deliberately NOT a top-nav item (reached via the footer), so it
    /// stays out of <see cref="Build"/>'s <c>Items</c>/<c>QuickLinks</c>. Shared between the generator (writes the
    /// file) and the footer (links to it) so the two can't disagree. [Story 4.8]</summary>
    public const string AboutOutputPath = "about.html";

    public required IReadOnlyList<(string Label, string OutputRelativePath)> Items { get; init; }

    /// <summary>Every discoverable key view for the dashboard's quick-link grid. A superset of the nav bar:
    /// it also carries module docs kept out of the top nav (brief, UX) plus a short description per entry.</summary>
    public required IReadOnlyList<(string Label, string OutputRelativePath, string Description)> QuickLinks { get; init; }

    /// <summary>The project name (from _bmad/config.toml) — used for the nav brand and page-title suffixes.</summary>
    public required string SiteTitle { get; init; }

    public string Brand => SiteTitle;

    public bool HasEpics => Items.Any(i => i.Label == "Epics");

    public bool HasAdrs => Items.Any(i => i.Label == "ADRs");

    public bool HasReadme => Items.Any(i => i.Label == "Readme");

    public bool HasSprint => Items.Any(i => i.Label == "Sprint");

    public bool HasStructure => Items.Any(i => i.Label == "Structure");

    public static SiteNav Build(
        IReadOnlyList<string> sourceRelativePaths,
        string siteTitle,
        IReadOnlyList<ModuleDoc>? moduleDocs = null,
        bool hasAdrs = false,
        bool hasReadme = false,
        bool hasSprint = false,
        bool hasStructure = false)
    {
        var items = new List<(string, string)> { ("Home", HomeOutputPath) };
        var quickLinks = new List<(string, string, string)>();

        // The README is the project's front-door narrative, so it sits first after Home.
        if (hasReadme)
        {
            items.Add(("Readme", ReadmeOutputPath));
            quickLinks.Add(("Readme", ReadmeOutputPath, "Read the project overview."));
        }

        // Module docs (PRD/Architecture, or GDD/Narrative/etc.) are matched by filename anywhere in the
        // source tree, so a missing doc is simply skipped rather than producing a broken link. In-nav docs
        // ride the top nav; all discovered docs appear in the dashboard quick links.
        foreach (var doc in moduleDocs ?? Array.Empty<ModuleDoc>())
        {
            var match = sourceRelativePaths.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), doc.FileName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                continue;
            }

            var outputPath = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(match));
            if (doc.InNav)
            {
                items.Add((doc.Label, outputPath));
            }

            quickLinks.Add((doc.Label, outputPath, doc.Description));
        }

        // ADRs sit next to the architecture doc conceptually; they live in docs/adrs, not _bmad-output,
        // so their availability is signalled by the caller rather than the source-file list.
        if (hasAdrs)
        {
            items.Add(("ADRs", AdrsLandingOutputPath));
        }

        var hasEpics = sourceRelativePaths.Any(BmadArtifactAdapter.IsEpicsFile);
        if (hasEpics)
        {
            items.Add(("Epics", EpicsOutputPath));
            // Requirements are parsed out of epics.md, so they share its availability guard.
            items.Add(("Requirements", RequirementsOutputPath));
            quickLinks.Add(("Epics", EpicsOutputPath, "Track epic and story delivery progress."));
            quickLinks.Add(("Requirements", RequirementsOutputPath, "Review FR/NFR coverage and status."));
        }

        // The sprint tracking file (sprint-status.yaml) is its own first-class delivery view, gated on the
        // file's presence exactly like ADRs/Readme — signalled by the caller since the yaml isn't in the
        // *.md source list. Sits in the Epics/Requirements delivery-tracking neighborhood. [Story 2.3 Task 5]
        if (hasSprint)
        {
            items.Add(("Sprint", SprintOutputPath));
            quickLinks.Add(("Sprint", SprintOutputPath, "See where every epic and story sits."));
        }

        // The interactive structure tree is its own first-class insight surface, gated on the source-artifact
        // file set the same way Sprint gates on the yaml. Sits in the Epics/Sprint insight-tracking
        // neighborhood. Its nav label routes through Icons.ForConcept("Structure"). [Story 3.4 Task 3.2]
        if (hasStructure)
        {
            items.Add(("Structure", StructureOutputPath));
            quickLinks.Add(("Structure", StructureOutputPath, "Explore the project and artifact structure."));
        }

        if (hasAdrs)
        {
            quickLinks.Add(("ADRs", AdrsLandingOutputPath, "Browse architecture decisions."));
        }

        // The spec kernel (SPEC.md + companions under specs/) is a first-class artifact class: surface a
        // dashboard quick-link to its canonical SPEC hub — the natural entry point — gated on the specs/
        // directory the same way epics/ADRs are matched by well-known presence, so an absent kernel simply
        // omits the link (no broken nav). The existing "Architecture" (ARCHITECTURE-SPINE) module-doc nav
        // entry is a separate concern and is left untouched — not duplicated here. [Story 2.2 Task 3]
        var specKernelHub = sourceRelativePaths.FirstOrDefault(p =>
            IsUnderSpecs(p) && string.Equals(Path.GetFileName(p), "SPEC.md", StringComparison.OrdinalIgnoreCase));
        if (specKernelHub is not null)
        {
            var specOutputPath = PathUtil.NormalizeSlashes(PathUtil.ToOutputRelative(specKernelHub));
            quickLinks.Add(("Spec", specOutputPath, "Read the canonical SPEC kernel and its companions."));
        }

        return new SiteNav { Items = items, QuickLinks = quickLinks, SiteTitle = siteTitle };
    }

    /// <summary>True when a source path lives under the <c>specs/</c> directory (the spec-kernel folder
    /// convention), keyed by directory prefix rather than the <c>spec</c> filename substring so it stays
    /// disjoint from Story 2.1's <c>implementation-artifacts/spec-*.md</c> quick-dev files. [Story 2.2 Task 1]</summary>
    private static bool IsUnderSpecs(string sourceRelativePath) =>
        PathUtil.NormalizeSlashes(sourceRelativePath).StartsWith("specs/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Projects this nav's already host-neutral data into the typed <see cref="NavigationView"/> the
    /// render adapters consume, with <paramref name="activeOutputRelativePath"/> marking the current page. The
    /// icon concept key is the item's label (the mapping <see cref="RenderNavBar"/> always used); a non-HTML
    /// surface reads it without re-deriving it. This is the "named typed view of SiteNav's data" the delivery
    /// contract (AD-2) needs — <see cref="Build"/> stays the producer. [Story 6.1]</summary>
    public NavigationView ToNavigationView(string activeOutputRelativePath) => new()
    {
        SiteTitle = SiteTitle,
        Items = Items.Select(i => new NavItem(i.Label, i.OutputRelativePath, i.Label)).ToList(),
        QuickLinks = QuickLinks.Select(q => new NavQuickLink(q.Label, q.OutputRelativePath, q.Description)).ToList(),
        ActiveOutputRelativePath = activeOutputRelativePath,
    };

    /// <summary>Renders the site nav bar. The string-building was re-homed behind
    /// <see cref="HtmlRenderAdapter"/> in Story 6.1 (the DELIVERY seam); this now projects to a
    /// <see cref="NavigationView"/> and delegates, so the bytes are unchanged and un-migrated pages keep calling
    /// this exactly as before.</summary>
    public string RenderNavBar(string currentOutputRelativePath) =>
        HtmlRenderAdapter.Shared.RenderNav(ToNavigationView(currentOutputRelativePath));

    /// <summary>Renders a "Home / Epics / Epic 1 / Story 1.1" trail. The last entry (current page) should
    /// have a null path so it renders as plain text rather than a self-link. Re-homed behind
    /// <see cref="HtmlRenderAdapter"/> in Story 6.1; delegates so the bytes are unchanged.</summary>
    public static string RenderBreadcrumb(string currentOutputRelativePath, IReadOnlyList<(string Label, string? OutputRelativePath)> trail) =>
        HtmlRenderAdapter.Shared.RenderBreadcrumb(currentOutputRelativePath, BreadcrumbTrail.From(trail));
}
