namespace SpecScribe;

/// <summary>Inputs for sunburst follow-up geometry (Story 9.7 / FR30). Counts for <em>open</em> items must
/// agree with <see cref="ProjectCounts"/> — never re-parsed from yaml/markdown at the chart layer.
/// Detail/provenance pages remain Story 9.6's domain; per-item detail hrefs are Story 9.11.
/// Attributed items render as story-ring peers under their epic; unattributed items + deferred aggregate
/// share a synthetic epic-level "Follow-ups" slice.</summary>
public sealed record FollowUpGeometry(
    IReadOnlyList<SprintActionItem> ActionItems,
    int DeferredOpenCount,
    string? DeferredHref,
    string ActionItemsHref,
    IReadOnlyDictionary<SprintActionItem, string>? ActionDetailSlugs = null)
{
    public static FollowUpGeometry Empty { get; } = new(
        Array.Empty<SprintActionItem>(),
        0,
        null,
        SiteNav.ActionItemsOutputPath,
        new Dictionary<SprintActionItem, string>());

    public static bool IsDone(SprintActionItem item) =>
        item.Status.Equals("done", StringComparison.OrdinalIgnoreCase);

    /// <summary>Open / in-progress action items — the set whose count must match
    /// <see cref="ProjectCounts.OpenActionItems"/>.</summary>
    public IReadOnlyList<SprintActionItem> OpenActionItems =>
        ActionItems.Where(a => !IsDone(a)).ToList();

    /// <summary>True when any follow-up wedge should render (open or done action items, or deferred surface).</summary>
    public bool HasAny =>
        ActionItems.Count > 0
        || (DeferredOpenCount > 0 && DeferredHref is { Length: > 0 });

    /// <summary>Link prefix derived from <see cref="ActionItemsHref"/> (e.g. <c>../</c> on epic pages).</summary>
    public string LinkPrefix =>
        ActionItemsHref.EndsWith(SiteNav.ActionItemsOutputPath, StringComparison.Ordinal)
            ? ActionItemsHref[..^SiteNav.ActionItemsOutputPath.Length]
            : "";

    /// <summary>Per-item detail page href (Story 9.11). Falls back to the whole action-items page when
    /// the item has no assigned slug. Prefix matches <see cref="ActionItemsHref"/> depth.</summary>
    public string HrefFor(SprintActionItem item)
    {
        var slugs = ActionDetailSlugs ?? FollowUpSlug.AssignActionSlugs(ActionItems);
        if (slugs.TryGetValue(item, out var slug))
            return LinkPrefix + FollowUpSlug.OutputPath(slug);
        return ActionItemsHref;
    }

    /// <summary>Builds geometry from the portal ledger + already-projected inventory.
    /// <paramref name="actionItems"/> is the full sprint list (open + done) so completed follow-ups can
    /// render green; open tallies still come from <paramref name="counts"/>.</summary>
    public static FollowUpGeometry From(
        IReadOnlyList<SprintActionItem> actionItems,
        ProjectCounts counts,
        WorkInventory work,
        string linkPrefix = "")
    {
        var deferredHref = work.Deferred is { } d && counts.DeferredOpenItems > 0
            ? linkPrefix + d.OutputPath
            : null;

        return new FollowUpGeometry(
            actionItems,
            counts.DeferredOpenItems,
            deferredHref,
            linkPrefix + SiteNav.ActionItemsOutputPath,
            FollowUpSlug.AssignActionSlugs(actionItems));
    }

    /// <summary>Epic-scoped geometry: this epic's action items only. Deferred aggregate stays on the project
    /// sunburst's unattributed slice (no per-item epic attribution without re-parsing).
    /// Preserves the project-wide slug map so detail URLs stay stable under epic filtering.</summary>
    public FollowUpGeometry ForEpic(int epicNumber) =>
        new(
            ActionItems.Where(a => a.EpicNumber == epicNumber).ToList(),
            DeferredOpenCount: 0,
            DeferredHref: null,
            ActionItemsHref,
            ActionDetailSlugs ?? FollowUpSlug.AssignActionSlugs(ActionItems));

    public IReadOnlyList<SprintActionItem> ForEpicNumber(int epicNumber) =>
        ActionItems.Where(a => a.EpicNumber == epicNumber).ToList();

    public IReadOnlyList<SprintActionItem> UnattributedActionItems =>
        ActionItems.Where(a => a.EpicNumber is null).ToList();
}
