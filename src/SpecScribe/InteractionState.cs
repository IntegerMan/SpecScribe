namespace SpecScribe;

/// <summary>The drill / traceability semantics of a page as host-neutral DATA — the AD-8 "interaction-state
/// shape" that is SHARED across surfaces (only the update TRANSPORT is adapter-specific). In this static site
/// there is no client-side drill state machine: "drill" is hyperlink navigation between generated pages
/// (Home → Epics → Epic N → Story N.M), so this models WHERE drilling goes — the parent/child page relationship —
/// plus the page's own status stage. It does NOT model hover/JS behavior (the client script is progressive
/// enhancement only) and it does NOT re-model status: the stage is CONSUMED from <see cref="StatusStyles"/>, the
/// documented status→stage seam Story 8.1 hardens, never forked here. A future webview (6.2) reads the same
/// relationships without depending on the HTML surface's enhancement scripts — the parity harness enforces
/// exactly that. [Story 6.1]</summary>
public sealed record InteractionState
{
    /// <summary>The drill-UP target (the parent page in the Home → Epics → Epic → Story hierarchy), sourced from
    /// the breadcrumb trail (<see cref="BreadcrumbTrail.ParentTarget"/>). Null on a root page.</summary>
    public string? ParentTarget { get; init; }

    /// <summary>The ordered drill-DOWN targets (the child pages this page links into: Home → the epics index,
    /// the epics index → each epic page, an epic → each of its story pages). Empty for a leaf (a story) or a
    /// page outside the drill hierarchy.</summary>
    public IReadOnlyList<string> ChildTargets { get; init; } = Array.Empty<string>();

    /// <summary>The page's own canonical status STAGE (e.g. <c>active</c>, <c>done</c>) as resolved by
    /// <see cref="StatusStyles"/> — the shared status-semantics fact the parity harness checks a surface
    /// reproduces. Null when the page has no single status (Home, the Epics index) or renders no status badge.
    /// This REFERENCES <see cref="StatusStyles"/>; it never relabels or remaps status. [Story 8.1 boundary]</summary>
    public string? StatusStage { get; init; }

    /// <summary>The no-interaction default — a page with no parent, no children, and no status stage (e.g. a
    /// standalone doc page). [Story 6.1]</summary>
    public static InteractionState None { get; } = new();
}
