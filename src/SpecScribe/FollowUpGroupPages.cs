namespace SpecScribe;

/// <summary>One row on a generated filtered follow-up group list page. [Story 9.13]</summary>
public sealed record FollowUpGroupMember(
    string Kind,
    string SummaryHtml,
    string DetailHref,
    string StatusToken,
    string StatusLabel,
    string SourceChipHtml,
    bool Resolved,
    string DetailBodyHtml = "");

/// <summary>One non-empty filtered group page to emit under <c>follow-ups/group-*.html</c>. [Story 9.13]</summary>
public sealed record FollowUpGroupSpec(
    string Slug,
    string Title,
    string Subtitle,
    IReadOnlyList<FollowUpGroupMember> Members,
    string? WholeSiteListHref = null,
    string? WholeSiteListLabel = null)
{
    public string OutputPath => FollowUpSlug.OutputPath(Slug);
    public int Count => Members.Count;
}

/// <summary>Stable group-page identity + membership projection for sunburst group destinations.
/// Paths use a <c>group-</c> prefix so they never collide with <c>action-</c>/<c>deferred-</c> detail slugs.
/// Membership mirrors sunburst sets (9.12) — no second deferred parse. [Story 9.13]</summary>
public static class FollowUpGroupPages
{
    public const string FollowUpsSlug = "group-follow-ups";
    public const string UnplannedSlug = "group-unplanned";

    public static string EpicSlug(int epicNumber) => $"group-epic-{epicNumber}";

    public static string FollowUpsPath => FollowUpSlug.OutputPath(FollowUpsSlug);
    public static string UnplannedPath => FollowUpSlug.OutputPath(UnplannedSlug);
    public static string EpicPath(int epicNumber) => FollowUpSlug.OutputPath(EpicSlug(epicNumber));

    /// <summary>True when <paramref name="slug"/> is a group-page slug (not an item detail).</summary>
    public static bool IsGroupSlug(string slug) =>
        slug.StartsWith("group-", StringComparison.Ordinal);

    /// <summary>Enumerates non-empty group pages only (NFR8). Order: Follow-ups, Unplanned, epic-N ascending.</summary>
    public static IReadOnlyList<FollowUpGroupSpec> Enumerate(
        FollowUpGeometry followUps,
        UnplannedWorkGeometry unplanned,
        EpicsModel? epics = null)
    {
        var groups = new List<FollowUpGroupSpec>();

        var unattributed = followUps.UnattributedActionItems;
        if (unattributed.Count > 0)
        {
            groups.Add(new FollowUpGroupSpec(
                FollowUpsSlug,
                "Follow-ups",
                "Unattributed action items",
                unattributed.Select(a => FromAction(a, followUps, "Unattributed")).ToList(),
                SiteNav.ActionItemsOutputPath,
                "All open action items"));
        }

        if (unplanned.HasUnplanned)
        {
            var members = new List<FollowUpGroupMember>();
            foreach (var qd in unplanned.UnplannedQuickDev)
                members.Add(FromQuickDev(qd));
            foreach (var slot in unplanned.UnattributableDeferred)
                members.Add(FromDeferred(slot, sourceChip: "Direct change"));

            groups.Add(new FollowUpGroupSpec(
                UnplannedSlug,
                "Unplanned",
                "Direct / one-off work",
                members,
                unplanned.DeferredListHref is { Length: > 0 } dh
                    ? StripLinkPrefix(dh)
                    : null,
                unplanned.DeferredListHref is { Length: > 0 } ? "All deferred work" : null));
        }

        var epicNumbers = new SortedSet<int>();
        foreach (var a in followUps.ActionItems)
            if (a.EpicNumber is { } en) epicNumbers.Add(en);
        foreach (var d in followUps.DeferredItems)
            if (d.EpicNumber is { } en) epicNumbers.Add(en);
        if (epics is not null)
            foreach (var e in epics.Epics)
                epicNumbers.Add(e.Number);

        foreach (var n in epicNumbers)
        {
            var actions = followUps.ForEpicNumber(n);
            var deferred = followUps.DeferredForEpicNumber(n);
            // Pin: epic group = attributed actions + deferred. Optional quick-dev for completeness.
            if (actions.Count == 0 && deferred.Count == 0) continue;

            var members = new List<FollowUpGroupMember>();
            foreach (var a in actions)
                members.Add(FromAction(a, followUps, $"Epic {n}"));
            foreach (var slot in deferred)
                members.Add(FromDeferred(slot, sourceChip: slot.ProvenanceLabel));
            foreach (var qd in unplanned.ForEpic(n))
                members.Add(FromQuickDev(qd));

            groups.Add(new FollowUpGroupSpec(
                EpicSlug(n),
                $"Epic {n} follow-ups",
                $"Attributed follow-ups for Epic {n}",
                members,
                SiteNav.ActionItemsOutputPath,
                "All open action items"));
        }

        return groups;
    }

    private static FollowUpGroupMember FromAction(
        SprintActionItem item, FollowUpGeometry geometry, string sourceChip)
    {
        var summary = FollowUpRow.SummarizePlainText(item.Action);
        return new FollowUpGroupMember(
            "action",
            PathUtil.Html(summary),
            geometry.HrefFor(item),
            StatusStyles.ForSprint(item.Status),
            StatusStyles.SprintLabel(item.Status),
            PathUtil.Html(sourceChip),
            FollowUpGeometry.IsDone(item));
    }

    private static FollowUpGroupMember FromDeferred(FollowUpDeferredSlot slot, string sourceChip)
    {
        var summary = FollowUpRow.SummarizeFromHtml(slot.Item.BodyHtml);
        var (token, label) = slot.Item.Resolved
            ? ("done", "Resolved")
            : (StatusStyles.ForSprint("open"), "Open");
        return new FollowUpGroupMember(
            "deferred",
            PathUtil.Html(summary),
            slot.DetailHref,
            token,
            label,
            PathUtil.Html(sourceChip),
            slot.Item.Resolved);
    }

    private static FollowUpGroupMember FromQuickDev(UnplannedQuickDevSlot slot)
    {
        var status = slot.Entry.Status;
        var open = UnplannedWorkGeometry.IsOpenQuickDev(status);
        var token = open
            ? (string.IsNullOrWhiteSpace(status) ? StatusStyles.ForSprint("open") : StatusStyles.ForSprint(status))
            : "done";
        var label = open
            ? (string.IsNullOrWhiteSpace(status) ? "Open" : StatusStyles.SprintLabel(status))
            : "Done";
        return new FollowUpGroupMember(
            "direct",
            PathUtil.Html(slot.Entry.Title),
            slot.Href,
            token,
            label,
            PathUtil.Html("Direct change"),
            !open);
    }

    /// <summary>Strip a depth prefix so whole-site list hrefs stay root-relative for the templater's
    /// <see cref="PathUtil.RelativePrefix"/> rewrite.</summary>
    private static string StripLinkPrefix(string href)
    {
        var root = PathUtil.NormalizeSlashes(href);
        while (root.StartsWith("../", StringComparison.Ordinal))
            root = root[3..];
        if (root.StartsWith("./", StringComparison.Ordinal))
            root = root[2..];
        return root;
    }
}
