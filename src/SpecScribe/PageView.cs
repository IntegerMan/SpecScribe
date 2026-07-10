namespace SpecScribe;

/// <summary>The page families the shared renderer emits — the <c>kind</c> AD-2's page model carries so a surface
/// can treat a page by its role without string-matching paths. Covers the current surfaces; a new page type adds
/// a member here rather than a magic path check. [Story 6.1]</summary>
public enum PageKind
{
    Home,
    Epics,
    Epic,
    Story,
    Requirements,
    Sprint,
    Doc,
    Retro,
    Structure,
    Adr,
    Diagnostics,
    About,
    GitInsights,
    DeepAnalytics,
    CommitDay,
}

/// <summary>A single page's identity + chrome context as a host-neutral view model — the AD-2 "page model" that
/// is the DELIVERY contract between the shared renderer and every <see cref="IRenderAdapter"/>. It carries what a
/// page IS and how it relates to others (kind, path, title/meta, the <see cref="NavigationView"/>,
/// <see cref="BreadcrumbTrail"/>, <see cref="AssetManifest"/>, and <see cref="InteractionState"/>) — the shared
/// chrome + identity + interaction context every surface must reproduce — with the page BODY carried as an opaque
/// already-rendered payload (<see cref="BodyHtml"/>).
/// <para>The body is opaque BY DESIGN this story: decomposing page bodies into section view models is deferred
/// parity work (the dashboard + epics bodies land in Story 6.2's rendering core, no other body has a planned
/// consumer). Pulling a body through the contract now would be a byte-risky rewrite with no consumer. So
/// <see cref="PageView"/> models the shared shell + semantics; the <see cref="HtmlRenderAdapter"/> composes the
/// opaque body into that shell, and the parity harness proves the shell's semantics survived. This is the
/// render-side mirror of Story 4.1's <see cref="ArtifactBundle"/>: an ingestion bundle carries normalized source
/// models; a page view carries normalized delivery models. [Story 6.1]</para></summary>
public sealed record PageView
{
    /// <summary>Which page family this is — see <see cref="PageKind"/>.</summary>
    public required PageKind Kind { get; init; }

    /// <summary>The page's output-relative path (e.g. <c>epics/epic-1.html</c>) — its identity and the basis for
    /// every relative link prefix.</summary>
    public required string OutputRelativePath { get; init; }

    /// <summary>The document title (the full <c>&lt;title&gt;</c> text, already including any site suffix).</summary>
    public required string Title { get; init; }

    /// <summary>The meta/OG description, or null to fall back to <see cref="Title"/> (matching
    /// <see cref="PathUtil.RenderHeadOpen"/>'s default — pages that pass no description today set this null).</summary>
    public string? MetaDescription { get; init; }

    /// <summary>The site navigation graph, with this page marked active.</summary>
    public required NavigationView Nav { get; init; }

    /// <summary>The drill trail to this page (empty on the home page).</summary>
    public required BreadcrumbTrail Breadcrumb { get; init; }

    /// <summary>The shared assets this page needs.</summary>
    public required AssetManifest Assets { get; init; }

    /// <summary>The drill / status semantics of this page.</summary>
    public required InteractionState Interaction { get; init; }

    /// <summary>The already-rendered inner page content (the <c>&lt;main&gt;…&lt;/main&gt;</c> body from today's
    /// templaters), carried verbatim. This is the DEFERRED-decomposition seam: the adapter composes it into the
    /// shared chrome untouched, so no byte of body HTML changes and no body is pulled through the contract until
    /// a surface actually consumes it (Story 6.2). [Story 6.1]</summary>
    public required string BodyHtml { get; init; }
}
