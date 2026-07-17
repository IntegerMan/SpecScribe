namespace SpecScribe;

/// <summary>Inputs for the sunburst follow-up outer band (Story 9.7 / FR30 geometry). Counts must agree with
/// <see cref="ProjectCounts"/> — never re-parsed from yaml/markdown at the chart layer. Detail/provenance
/// pages remain Story 9.6's domain; this type only carries what the remaining-work geometry needs to count
/// and link.</summary>
public sealed record FollowUpGeometry(
    IReadOnlyList<SprintActionItem> OpenActionItems,
    int DeferredOpenCount,
    string? DeferredHref,
    string ActionItemsHref)
{
    public static FollowUpGeometry Empty { get; } = new(
        Array.Empty<SprintActionItem>(),
        0,
        null,
        SiteNav.ActionItemsOutputPath);

    /// <summary>True when the 4th ring should render — open action items and/or a deferred surface with a
    /// positive ledger count (NFR8: omit entirely when neither is present).</summary>
    public bool HasRing =>
        OpenActionItems.Count > 0
        || (DeferredOpenCount > 0 && DeferredHref is { Length: > 0 });

    /// <summary>Wedge count on the follow-up ring (one per open action item + optional deferred aggregate).</summary>
    public int WedgeCount =>
        OpenActionItems.Count + (DeferredOpenCount > 0 && DeferredHref is { Length: > 0 } ? 1 : 0);

    /// <summary>Builds geometry from the portal ledger + already-projected inventory. Open items must be the
    /// same list that produced <see cref="ProjectCounts.OpenActionItems"/> (typically
    /// <see cref="SprintStatus.OpenActionItems"/>). Deferred wedges require both a positive ledger count and a
    /// deferred surface href — never invent a link when the note is absent.</summary>
    public static FollowUpGeometry From(
        IReadOnlyList<SprintActionItem> openActionItems,
        ProjectCounts counts,
        WorkInventory work,
        string linkPrefix = "")
    {
        var deferredHref = work.Deferred is { } d && counts.DeferredOpenItems > 0
            ? linkPrefix + d.OutputPath
            : null;

        return new FollowUpGeometry(
            openActionItems,
            counts.DeferredOpenItems,
            deferredHref,
            linkPrefix + SiteNav.ActionItemsOutputPath);
    }

    /// <summary>Epic-scoped geometry: only this epic's open action items. Deferred aggregate is omitted here
    /// (no per-item epic attribution without re-parsing — safe default per Story 9.7). Zero for the epic →
    /// <see cref="HasRing"/> is false even when the project has other follow-ups.</summary>
    public FollowUpGeometry ForEpic(int epicNumber) =>
        new(
            OpenActionItems.Where(a => a.EpicNumber == epicNumber).ToList(),
            DeferredOpenCount: 0,
            DeferredHref: null,
            ActionItemsHref);
}
