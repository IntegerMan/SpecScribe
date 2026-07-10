namespace SpecScribe;

/// <summary>One breadcrumb entry: a label plus the output-relative page it links to, or a null path for the
/// current page (rendered as plain text, not a self-link). [Story 6.1]</summary>
public sealed record BreadcrumbCrumb(string Label, string? OutputRelativePath);

/// <summary>The drill trail as a host-neutral view model — the ordered "Home / Epics / Epic 1 / Story 1.1"
/// path, last entry = current page (null path). This is exactly the data
/// <see cref="SiteNav.RenderBreadcrumb"/> already took as a parameter, now a named type a non-HTML surface can
/// bind to. The trail also encodes the AD-8 drill-UP relationship: the parent is the last crumb with a real
/// target (see <see cref="ParentTarget"/>), so <see cref="InteractionState"/> sources its parent link from here
/// rather than re-deriving it. [Story 6.1]</summary>
public sealed record BreadcrumbTrail
{
    public required IReadOnlyList<BreadcrumbCrumb> Crumbs { get; init; }

    /// <summary>An empty trail — the home page has no breadcrumb (the HTML adapter renders nothing for it,
    /// exactly as today). [Story 6.1]</summary>
    public static BreadcrumbTrail Empty { get; } = new() { Crumbs = Array.Empty<BreadcrumbCrumb>() };

    /// <summary>Lifts the legacy <c>(Label, OutputRelativePath?)</c> tuple trail — the shape every templater
    /// still authors inline — into the typed view model, so callers migrate incrementally without rewriting
    /// their trail literals. [Story 6.1]</summary>
    public static BreadcrumbTrail From(IReadOnlyList<(string Label, string? OutputRelativePath)> trail) =>
        new() { Crumbs = trail.Select(t => new BreadcrumbCrumb(t.Label, t.OutputRelativePath)).ToList() };

    /// <summary>The drill-UP parent target: the last crumb carrying a real path (the current page is the final
    /// crumb with a null path). Null on a root page with no parent crumb (e.g. the home page's empty trail).
    /// This is the single source <see cref="InteractionState.ParentTarget"/> derives from, so the rendered
    /// breadcrumb and the interaction model can never disagree about where "up" goes. [Story 6.1]</summary>
    public string? ParentTarget => Crumbs
        .Reverse()
        .Select(c => c.OutputRelativePath)
        .FirstOrDefault(p => p is not null);
}
