namespace SpecScribe;

/// <summary>One deferred-work item projected for sunburst geometry. Epic-attributed items render under
/// their epic; unattributed items share the synthetic Follow-ups slice. [Story 9.7]</summary>
public sealed record FollowUpDeferredSlot(
    DeferredWorkItem Item,
    string ProvenanceLabel,
    int? EpicNumber,
    string DetailHref);

/// <summary>Inputs for sunburst follow-up geometry (Story 9.7 / FR30). Counts for <em>open</em> items must
/// agree with <see cref="ProjectCounts"/> — never re-parsed from yaml/markdown at the chart layer.
/// Attributed items render as story-ring peers under their epic; unattributed action items and unattributed
/// deferred items share a synthetic epic-level "Follow-ups" slice.</summary>
public sealed record FollowUpGeometry(
    IReadOnlyList<SprintActionItem> ActionItems,
    int DeferredOpenCount,
    string? DeferredHref,
    string ActionItemsHref,
    IReadOnlyDictionary<SprintActionItem, string>? ActionDetailSlugs = null,
    IReadOnlyList<FollowUpDeferredSlot>? DeferredSlots = null)
{
    public static FollowUpGeometry Empty { get; } = new(
        Array.Empty<SprintActionItem>(),
        0,
        null,
        SiteNav.ActionItemsOutputPath,
        new Dictionary<SprintActionItem, string>(),
        Array.Empty<FollowUpDeferredSlot>());

    public IReadOnlyList<FollowUpDeferredSlot> DeferredItems => DeferredSlots ?? Array.Empty<FollowUpDeferredSlot>();

    public static bool IsDone(SprintActionItem item) =>
        item.Status.Equals("done", StringComparison.OrdinalIgnoreCase);

    /// <summary>Open / in-progress action items — the set whose count must match
    /// <see cref="ProjectCounts.OpenActionItems"/>.</summary>
    public IReadOnlyList<SprintActionItem> OpenActionItems =>
        ActionItems.Where(a => !IsDone(a)).ToList();

    /// <summary>True when any follow-up wedge should render (action items or deferred items).</summary>
    public bool HasAny =>
        ActionItems.Count > 0 || DeferredItems.Count > 0;

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
    /// render green; open tallies still come from <paramref name="counts"/>.
    /// When <paramref name="deferredModel"/> is supplied, deferred items are attributed to epics via
    /// <c>SourceStoryId</c> and rendered as individual story-ring wedges (not one aggregate).</summary>
    public static FollowUpGeometry From(
        IReadOnlyList<SprintActionItem> actionItems,
        ProjectCounts counts,
        WorkInventory work,
        string linkPrefix = "",
        DeferredWorkModel? deferredModel = null,
        EpicsModel? epics = null)
    {
        var deferredHref = work.Deferred is { } d && counts.DeferredOpenItems > 0
            ? linkPrefix + d.OutputPath
            : null;

        var deferredSlots = BuildDeferredSlots(deferredModel, epics, work, linkPrefix, deferredHref);

        return new FollowUpGeometry(
            actionItems,
            counts.DeferredOpenItems,
            deferredHref,
            linkPrefix + SiteNav.ActionItemsOutputPath,
            FollowUpSlug.AssignActionSlugs(actionItems),
            deferredSlots);
    }

    /// <summary>Epic-scoped geometry: this epic's action items and epic-attributed deferred items only.
    /// Preserves the project-wide slug map so detail URLs stay stable under epic filtering.
    /// Re-prefixes deferred <see cref="FollowUpDeferredSlot.DetailHref"/> values to match
    /// <see cref="LinkPrefix"/> (epic pages live under <c>epics/</c> — without this, wedges 404 at
    /// <c>epics/follow-ups/…</c>). [Story 9.11]</summary>
    public FollowUpGeometry ForEpic(int epicNumber)
    {
        var prefix = LinkPrefix;
        return new(
            ActionItems.Where(a => a.EpicNumber == epicNumber).ToList(),
            DeferredOpenCount: 0,
            DeferredHref: null,
            ActionItemsHref,
            ActionDetailSlugs ?? FollowUpSlug.AssignActionSlugs(ActionItems),
            DeferredItems
                .Where(s => s.EpicNumber == epicNumber)
                .Select(s => s with { DetailHref = ApplyLinkPrefix(prefix, s.DetailHref) })
                .ToList());
    }

    /// <summary>Rewrites an output-root-relative (or already-prefixed) href for the current page depth.
    /// Strips leading <c>../</c> segments first so re-scoping is idempotent.</summary>
    public static string ApplyLinkPrefix(string linkPrefix, string href)
    {
        if (string.IsNullOrEmpty(href)) return href;
        var root = PathUtil.NormalizeSlashes(href);
        while (root.StartsWith("../", StringComparison.Ordinal))
            root = root[3..];
        if (root.StartsWith("./", StringComparison.Ordinal))
            root = root[2..];
        return linkPrefix + root;
    }
    public IReadOnlyList<SprintActionItem> ForEpicNumber(int epicNumber) =>
        ActionItems.Where(a => a.EpicNumber == epicNumber).ToList();

    public IReadOnlyList<FollowUpDeferredSlot> DeferredForEpicNumber(int epicNumber) =>
        DeferredItems.Where(s => s.EpicNumber == epicNumber).ToList();

    public IReadOnlyList<SprintActionItem> UnattributedActionItems =>
        ActionItems.Where(a => a.EpicNumber is null).ToList();

    public IReadOnlyList<FollowUpDeferredSlot> UnattributedDeferredItems =>
        DeferredItems.Where(s => s.EpicNumber is null).ToList();

    private static IReadOnlyList<FollowUpDeferredSlot> BuildDeferredSlots(
        DeferredWorkModel? model,
        EpicsModel? epics,
        WorkInventory work,
        string linkPrefix,
        string? listHref)
    {
        if (work.Deferred is null || listHref is null) return Array.Empty<FollowUpDeferredSlot>();

        if (model is { IsStructured: true })
        {
            var pairs = model.Groups
                .SelectMany(g => g.Items.Select(i => (Item: i, ProvenanceLabel: g.ProvenanceLabel, Group: g)))
                .ToList();
            if (pairs.Count == 0) return Array.Empty<FollowUpDeferredSlot>();

            var slugs = FollowUpSlug.AssignDeferredSlugs(
                pairs.Select(p => (p.Item, p.ProvenanceLabel)).ToList());

            return pairs.Select(p =>
            {
                var epic = ResolveEpicNumber(epics, p.Group.SourceStoryId);
                var href = slugs.TryGetValue(p.Item, out var slug)
                    ? linkPrefix + FollowUpSlug.OutputPath(slug)
                    : linkPrefix + listHref;
                return new FollowUpDeferredSlot(p.Item, p.ProvenanceLabel, epic, href);
            }).ToList();
        }

        var body = model?.PlainBodyHtml;
        if (string.IsNullOrWhiteSpace(body)) return Array.Empty<FollowUpDeferredSlot>();

        return ExtractTopLevelListItems(body)
            .Select(t =>
            {
                var item = new DeferredWorkItem(t.BodyHtml, t.Resolved, null, null);
                return new FollowUpDeferredSlot(item, "Deferred work", null, linkPrefix + listHref);
            })
            .ToList();
    }

    private static int? ResolveEpicNumber(EpicsModel? epics, string? storyId)
    {
        if (epics is null || string.IsNullOrWhiteSpace(storyId)) return null;
        foreach (var epic in epics.Epics)
        foreach (var story in epic.Stories)
            if (string.Equals(story.Id, storyId, StringComparison.Ordinal))
                return epic.Number;

        // Story id known but not in the model (renumbered/removed) — still attribute to the
        // epic encoded in the id when that epic exists, so deferred work doesn't fall into the
        // unattributed Follow-ups slice. [Story 9.7 attribution follow-up]
        var dot = storyId.IndexOf('.');
        if (dot > 0
            && int.TryParse(storyId.AsSpan(0, dot), out var epicNum)
            && epics.Epics.Any(e => e.Number == epicNum))
            return epicNum;
        return null;
    }

    /// <summary>Top-level rendered list items from an unstructured deferred-work body.</summary>
    private static IEnumerable<(string BodyHtml, bool Resolved)> ExtractTopLevelListItems(string bodyHtml)
    {
        var depth = 0;
        var i = 0;
        while (i < bodyHtml.Length)
        {
            if (StartsAt(bodyHtml, i, "<ul") || StartsAt(bodyHtml, i, "<ol")) { depth++; i += 3; continue; }
            if (StartsAt(bodyHtml, i, "</ul") || StartsAt(bodyHtml, i, "</ol")) { depth = Math.Max(0, depth - 1); i += 4; continue; }
            if (depth == 1 && StartsAt(bodyHtml, i, "<li"))
            {
                var close = bodyHtml.IndexOf("</li>", i, StringComparison.OrdinalIgnoreCase);
                if (close < 0) yield break;
                var innerStart = bodyHtml.IndexOf('>', i) + 1;
                if (innerStart <= i) { i++; continue; }
                var inner = bodyHtml[innerStart..close];
                var resolved = inner.Contains("<del", StringComparison.OrdinalIgnoreCase);
                yield return (inner.Trim(), resolved);
                i = close + 5;
                continue;
            }
            i++;
        }

        static bool StartsAt(string haystack, int index, string needle) =>
            index + needle.Length <= haystack.Length
            && string.CompareOrdinal(haystack, index, needle, 0, needle.Length) == 0;
    }
}
