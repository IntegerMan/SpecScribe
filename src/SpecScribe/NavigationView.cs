namespace SpecScribe;

/// <summary>One top-nav destination as host-neutral DATA: the label a surface shows, the output-relative page
/// it targets, and the <see cref="Icons.ForConcept"/> concept key that picks its glyph. Carried separately from
/// the label (they happen to coincide today) so a non-HTML surface can pick an icon without re-deriving the
/// mapping the HTML nav bar hard-codes. This is the already host-neutral data that lived inline on
/// <see cref="SiteNav.Items"/> — lifted, not reinvented. [Story 6.1]</summary>
public sealed record NavItem(string Label, string OutputRelativePath, string ConceptKey);

/// <summary>One dashboard quick-link as host-neutral data — the superset of the nav bar that also carries a
/// short per-entry description. Lifted from <see cref="SiteNav.QuickLinks"/>. [Story 6.1]</summary>
public sealed record NavQuickLink(string Label, string OutputRelativePath, string Description);

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

    /// <summary>The ordered top-nav items (label + output-relative target + icon concept key). A surface renders
    /// these in order; the HTML adapter marks the one matching <see cref="ActiveOutputRelativePath"/> current.</summary>
    public required IReadOnlyList<NavItem> Items { get; init; }

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
}
