namespace SpecScribe;

/// <summary>One top-nav destination as host-neutral DATA: the label a surface shows, the output-relative page
/// it targets, and the <see cref="Icons.ForConcept"/> concept key that picks its glyph. Carried separately from
/// the label (they happen to coincide today) so a non-HTML surface can pick an icon without re-deriving the
/// mapping the HTML nav bar hard-codes. This is the already host-neutral data that lived inline on
/// <see cref="SiteNav.Items"/> — lifted, not reinvented. [Story 6.1]</summary>
public sealed record NavItem(string Label, string OutputRelativePath, string ConceptKey);

/// <summary>One journey-organized top-nav group: a disclosure header label + icon concept key, and the leaf
/// <see cref="NavItem"/> children it discloses. An empty <see cref="Label"/> means the children render as flat
/// top-level links (Home, or a single-child group collapsed out of a pointless one-item disclosure). [Story 10.1]</summary>
public sealed record NavGroup(string Label, string ConceptKey, IReadOnlyList<NavItem> Children);

/// <summary>One dashboard quick-link as host-neutral data — the superset of the nav bar that also carries a
/// short per-entry description. Lifted from <see cref="SiteNav.QuickLinks"/>. <c>Group</c> is the single-sourced
/// white key-views band classification (Delivery/Insights/Follow-ups/Project) set on <see cref="SiteNav.Build"/>;
/// it defaults to "Project" so pre-existing 3-arg construction sites (which don't exercise band grouping) keep
/// compiling unchanged. [Story 6.1; Story 10.1 deferred debt cleanup]</summary>
public sealed record NavQuickLink(string Label, string OutputRelativePath, string Description, string Group = "Project");

/// <summary>One entry in a page's local-context list — the white sub-header band's page-type-specific content
/// (a sibling story, a sibling code file, an ADR, ...). <see cref="Href"/> is already relative to the current
/// page (the same convention <c>PagerLink.Href</c> uses), so the renderer never recomputes a prefix per item.
/// [Story 10.10]</summary>
public sealed record NavLocalItem(string Label, string Href, bool IsActive);

/// <summary>The white sub-header band's page-type-appropriate local context (e.g. "Stories in this epic"), built
/// entirely from data the rendering call site already computed for some other purpose (an <c>EntityPager</c>
/// family, <c>epic.Stories</c>, <c>_adrs</c>, <c>nav.Groups</c>) — no new authoring schema. Null or an empty
/// <see cref="Items"/> list, or a list containing only the current (active) item — nothing else to navigate
/// to — all mean "no rich context for this page"; the renderer falls back to the existing generic quick-links
/// band (NFR8: never a degenerate one-item-looks-broken band). [Story 10.10]</summary>
public sealed record NavLocalContext(string Title, IReadOnlyList<NavLocalItem> Items);

/// <summary>The site navigation graph as a host-neutral view model — the AD-2 "navigation graph" the shared
/// renderer emits and every delivery adapter consumes WITHOUT reinterpreting source artifacts
/// ([ARCHITECTURE-SPINE.md AD-2]). It is the DELIVERY-seam mirror of Story 4.1's ingestion-seam
/// <see cref="ArtifactBundle"/>: this carries "what a page links to", not "what a source file said". The data
/// here already lived on <see cref="SiteNav"/> (<c>Items</c>/<c>QuickLinks</c>/<c>SiteTitle</c>); the contract is
/// that data cleanly separated from the string-building an <see cref="IRenderAdapter"/> owns, so a future VS Code
/// webview (Story 6.2) binds to the SAME nav graph the <see cref="HtmlRenderAdapter"/> renders — no duplicated
/// discovery, no HTML in the model. [Story 6.1]</summary>
public sealed record NavigationView
{
    /// <summary>The project name — the nav brand and page-title suffix. Was <see cref="SiteNav.SiteTitle"/>.</summary>
    public required string SiteTitle { get; init; }

    /// <summary>The ordered top-nav leaf items (label + output-relative target + icon concept key) — a FLATTENED,
    /// in-render-order projection of every leaf in <see cref="Groups"/> (plus flat top-level links like Home).
    /// Kept for RenderParity, the SPA manifest, and dashboard active-item logic. [Story 6.1; 10.1]</summary>
    public required IReadOnlyList<NavItem> Items { get; init; }

    /// <summary>The hierarchical top-nav structure the renderer walks (journey groups → children). Flat top-level
    /// links use an empty <see cref="NavGroup.Label"/>. Always agrees with <see cref="Items"/>. Required (not
    /// defaulted) so a construction site that forgets it fails to compile instead of silently rendering an
    /// empty dark-bar menu while <see cref="Items"/>-driven RenderParity still reports a passing nav. [Story 10.1]</summary>
    public required IReadOnlyList<NavGroup> Groups { get; init; }

    /// <summary>The dashboard quick-link grid — a superset of <see cref="Items"/> carrying a description each.</summary>
    public required IReadOnlyList<NavQuickLink> QuickLinks { get; init; }

    /// <summary>The output-relative path of the page currently being rendered — the single input the adapter
    /// needs to compute relative link prefixes and mark the active nav item. Not "what a page IS" (that is
    /// <see cref="PageView.OutputRelativePath"/>); it is "which nav entry is current", kept on the nav view so a
    /// surface can render the bar from this one value.</summary>
    public required string ActiveOutputRelativePath { get; init; }

    /// <summary>When true (Home with an epics model), the work-mode strip offers the full stage set
    /// (Overview · Requirements · Plan · Develop · Review · Track). When false on Home (no epics model),
    /// only Overview is emitted — NFR8 degrade for stages with nothing to emphasize. Ignored off Home.
    /// Defaults true so existing <see cref="SiteNav.ToNavigationView"/> call sites stay unchanged. [Story 9.8]</summary>
    public bool FullHomeWorkModeStrip { get; init; } = true;

    /// <summary>The current page's local-context list for the white sub-header band (e.g. an epic's stories, a
    /// code file's directory siblings). Null (the default) means "no rich context computed for this page" —
    /// <see cref="HtmlRenderAdapter"/> falls back to the generic quick-links band, unchanged from before this
    /// field existed. [Story 10.10]</summary>
    public NavLocalContext? LocalContext { get; init; }
}
