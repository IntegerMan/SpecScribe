namespace SpecScribe;

/// <summary>One deferred-work item projected for sunburst geometry. Epic-attributed items render under
/// their epic; items that still have no epic (and no parent quick-dev to inherit from) join the Unplanned
/// root (Story 9.12). Unattributed action items stay on the Follow-ups orphan. [Story 9.7; 9.12]</summary>
public sealed record FollowUpDeferredSlot(
    DeferredWorkItem Item,
    string ProvenanceLabel,
    int? EpicNumber,
    string DetailHref,
    /// <summary>Provenance key (<c>spec-*</c> or <c>N-M-slug</c>) when known — used to attach deferred
    /// to the parent quick-dev / story it stemmed from. [Story 9.12]</summary>
    string? SourceKey = null,
    /// <summary>Href to the provenance source page when resolvable (story page or quick-dev spec).</summary>
    string? SourceHref = null,
    /// <summary>Story id (<c>N.M</c>) when this deferred item stems from a story code review — used to
    /// nest the item under its parent story in the sunburst outer ring. [spec-sunburst-remaining-work-hierarchy]</summary>
    string? SourceStoryId = null);

/// <summary>Inputs for sunburst follow-up geometry (Story 9.7 / FR30). Counts for <em>open</em> items must
/// agree with <see cref="ProjectCounts"/> — never re-parsed from yaml/markdown at the chart layer.
/// Attributed items render as story-ring peers under their epic; unattributed action items share a synthetic
/// epic-level "Follow-ups" slice. Unattributable deferred items move to <see cref="UnplannedWorkGeometry"/>
/// (Story 9.12).</summary>
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

    /// <summary>Filtered Follow-ups orphan group page (<c>follow-ups/group-follow-ups.html</c>).
    /// Prefixed for epic-depth pages. [Story 9.13]</summary>
    public string FollowUpsGroupHref =>
        LinkPrefix + FollowUpGroupPages.FollowUpsPath;

    /// <summary>Builds geometry from the portal ledger + already-projected inventory.
    /// <paramref name="actionItems"/> is the full sprint list (open + done) so completed follow-ups can
    /// render as follow-up-done; open tallies still come from <paramref name="counts"/>.
    /// When <paramref name="deferredModel"/> is supplied, deferred items are attributed to epics via
    /// <c>SourceStoryId</c> and rendered as individual story-ring wedges. When the ledger reports open
    /// deferred but no slots can be built, a single aggregate unattributed slot preserves navigability.</summary>
    public static FollowUpGeometry From(
        IReadOnlyList<SprintActionItem> actionItems,
        ProjectCounts counts,
        WorkInventory work,
        string linkPrefix = "",
        DeferredWorkModel? deferredModel = null,
        EpicsModel? epics = null,
        IReadOnlyList<RetroModel>? retros = null)
    {
        // List href whenever the deferred surface exists — resolved items still need reverse-links / green wedges.
        var deferredHref = work.Deferred is { } d
            ? linkPrefix + d.OutputPath
            : null;

        var deferredSlots = BuildDeferredSlots(deferredModel, epics, work, linkPrefix, deferredHref);
        // Second pass: quick-dev-sourced slots inherit epic from ResolveQuickDevEpic with the
        // completed slot set (timing + deferred cues + retros need DeferredItems). Keeps chrome ↔ sunburst coherent.
        deferredSlots = EnrichQuickDevDeferredEpics(deferredSlots, epics, work, retros);

        // Ledger open deferred with nothing parseable → one aggregate wedge (Unplanned / list href). Never drop debt.
        if (deferredSlots.Count == 0 && counts.DeferredOpenItems > 0 && deferredHref is { Length: > 0 })
        {
            var n = counts.DeferredOpenItems;
            var body = $"<p>{n} open deferred {Plural(n, "item", "items")}</p>";
            deferredSlots = new[]
            {
                new FollowUpDeferredSlot(
                    new DeferredWorkItem(body, Resolved: false, null, null),
                    "Deferred work",
                    EpicNumber: null,
                    deferredHref),
            };
        }

        return new FollowUpGeometry(
            actionItems,
            counts.DeferredOpenItems,
            deferredHref,
            linkPrefix + SiteNav.ActionItemsOutputPath,
            FollowUpSlug.AssignActionSlugs(actionItems),
            deferredSlots);
    }

    private static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

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

    /// <summary>Story-child deferred: items whose <see cref="FollowUpDeferredSlot.SourceStoryId"/> resolves to
    /// a known story in the given epic. These render in the sunburst outer ring under their parent story's
    /// sweep. [spec-sunburst-remaining-work-hierarchy]</summary>
    public IReadOnlyList<FollowUpDeferredSlot> StoryChildDeferred(int epicNumber, string storyId) =>
        DeferredItems.Where(s => s.EpicNumber == epicNumber
            && !string.IsNullOrEmpty(s.SourceStoryId)
            && string.Equals(s.SourceStoryId, storyId, StringComparison.Ordinal)).ToList();

    /// <summary>Epic-level deferred: items attributed to the epic but NOT parented to any known story —
    /// these remain middle-ring peers of stories. [spec-sunburst-remaining-work-hierarchy]</summary>
    public IReadOnlyList<FollowUpDeferredSlot> EpicLevelDeferred(int epicNumber, IEnumerable<string> knownStoryIds)
    {
        var storySet = new HashSet<string>(knownStoryIds, StringComparer.Ordinal);
        return DeferredItems.Where(s => s.EpicNumber == epicNumber
            && (string.IsNullOrEmpty(s.SourceStoryId) || !storySet.Contains(s.SourceStoryId))).ToList();
    }

    /// <summary>Action items with no epic (<c>EpicNumber == null</c>). Prefer
    /// <see cref="OrphanActionItems"/> when an epic model is available so unknown epic numbers do not vanish.</summary>
    public IReadOnlyList<SprintActionItem> UnattributedActionItems =>
        ActionItems.Where(a => a.EpicNumber is null).ToList();

    /// <summary>Follow-ups orphan membership: null epic <em>or</em> <see cref="SprintActionItem.EpicNumber"/>
    /// not present in <paramref name="knownEpicNumbers"/>.</summary>
    public IReadOnlyList<SprintActionItem> OrphanActionItems(IReadOnlySet<int> knownEpicNumbers) =>
        ActionItems.Where(a => a.EpicNumber is null || !knownEpicNumbers.Contains(a.EpicNumber.Value)).ToList();

    public IReadOnlyList<FollowUpDeferredSlot> UnattributedDeferredItems =>
        DeferredItems.Where(s => s.EpicNumber is null).ToList();

    /// <summary>Unplanned deferred membership: null epic <em>or</em> <see cref="FollowUpDeferredSlot.EpicNumber"/>
    /// not present in <paramref name="knownEpicNumbers"/> — parallel to <see cref="OrphanActionItems"/>.</summary>
    public IReadOnlyList<FollowUpDeferredSlot> OrphanDeferredItems(IReadOnlySet<int> knownEpicNumbers) =>
        DeferredItems.Where(s => s.EpicNumber is null || !knownEpicNumbers.Contains(s.EpicNumber.Value)).ToList();

    /// <summary>Normalizes a provenance / filename key the same way deferred <c>SourceKey</c> and
    /// Unplanned membership do — strip <c>.md</c>/<c>.html</c>, trim backticks, ordinal-ignore-case stems.
    /// Null/whitespace → empty string (never throws).</summary>
    public static string NormalizeSourceKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var bare = key.Trim().Trim('`');
        if (bare.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) bare = bare[..^3];
        if (bare.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) bare = bare[..^5];
        return bare;
    }

    /// <summary>Reverse index: deferred slots whose <see cref="FollowUpDeferredSlot.SourceKey"/> names
    /// <paramref name="sourceKeyOrStoryId"/> (spec stem, <c>N-M-slug</c>, or story id <c>N.M</c>).
    /// Re-prefixes detail hrefs for the calling page depth. Empty when unstructured or no match (NFR8).
    /// Never throws. [artifact-review-nav-and-deferred]</summary>
    public IReadOnlyList<FollowUpDeferredSlot> DeferredForSource(string? sourceKeyOrStoryId, string linkPrefix = "")
    {
        if (string.IsNullOrWhiteSpace(sourceKeyOrStoryId)) return Array.Empty<FollowUpDeferredSlot>();
        var needle = NormalizeSourceKey(sourceKeyOrStoryId);
        if (needle.Length == 0) return Array.Empty<FollowUpDeferredSlot>();

        var needleStoryId = FollowUpRefs.StoryIdFromKey(needle);

        var matches = DeferredItems
            .Where(s => SourceKeyMatches(s.SourceKey, needle, needleStoryId))
            .Select(s => s with { DetailHref = ApplyLinkPrefix(linkPrefix, s.DetailHref) })
            .ToList();
        return matches;
    }

    private static bool SourceKeyMatches(string? sourceKey, string needle, string? needleStoryId)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return false;
        var key = NormalizeSourceKey(sourceKey);
        if (key.Length == 0) return false;
        if (string.Equals(key, needle, StringComparison.OrdinalIgnoreCase)) return true;

        if (needleStoryId is { Length: > 0 })
        {
            var fromKey = FollowUpRefs.StoryIdFromKey(key);
            if (string.Equals(fromKey, needleStoryId, StringComparison.Ordinal)) return true;
        }

        return false;
    }

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
                var sourceKey = p.Group.SourceKey;
                var sourceStoryId = p.Group.SourceStoryId
                    ?? (sourceKey is not null ? FollowUpRefs.StoryIdFromKey(sourceKey) : null);
                var epic = ResolveEpicNumber(epics, sourceStoryId)
                    ?? ResolveEpicFromSourceKey(epics, work, sourceKey);
                var href = slugs.TryGetValue(p.Item, out var slug)
                    ? linkPrefix + FollowUpSlug.OutputPath(slug)
                    : ApplyLinkPrefix(linkPrefix, listHref);
                var sourceHref = p.Group.SourceStoryHref is { Length: > 0 } sh
                    ? FollowUpGeometry.ApplyLinkPrefix(linkPrefix, sh)
                    : ResolveSourceHref(work, sourceKey, linkPrefix);
                return new FollowUpDeferredSlot(p.Item, p.ProvenanceLabel, epic, href, sourceKey, sourceHref, sourceStoryId);
            }).ToList();
        }

        var unstructured = UnstructuredItems(model?.PlainBodyHtml);
        if (unstructured.Count == 0) return Array.Empty<FollowUpDeferredSlot>();

        var unstructuredSlugs = FollowUpSlug.AssignDeferredSlugs(
            unstructured.Select(i => (i, "Deferred work")).ToList());
        return unstructured.Select(item =>
        {
            var href = unstructuredSlugs.TryGetValue(item, out var slug)
                ? linkPrefix + FollowUpSlug.OutputPath(slug)
                : ApplyLinkPrefix(linkPrefix, listHref);
            return new FollowUpDeferredSlot(item, "Deferred work", null, href);
        }).ToList();
    }

    /// <summary>Top-level list items from an unstructured deferred-work HTML body.
    /// Empty when the body has no parseable <c>&lt;li&gt;</c>s. [Story 9.11]</summary>
    public static IReadOnlyList<DeferredWorkItem> UnstructuredItems(string? bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(bodyHtml)) return Array.Empty<DeferredWorkItem>();
        return ExtractTopLevelListItems(bodyHtml)
            .Select(t => new DeferredWorkItem(t.BodyHtml, t.Resolved, null, null))
            .ToList();
    }

    /// <summary>When the deferred heading names a story key or a quick-dev <c>spec-*</c>, attribute to that
    /// epic (story → epic directly; quick-dev → text-only inherit on first pass). Timing/cue enrichment
    /// runs in <see cref="EnrichQuickDevDeferredEpics"/> once DeferredItems exist. [Story 9.12]</summary>
    private static int? ResolveEpicFromSourceKey(EpicsModel? epics, WorkInventory work, string? sourceKey)
    {
        if (epics is null || string.IsNullOrWhiteSpace(sourceKey)) return null;

        var storyId = FollowUpRefs.StoryIdFromKey(sourceKey);
        var fromStory = ResolveEpicNumber(epics, storyId);
        if (fromStory is not null) return fromStory;

        var quickDev = FindQuickDev(work, sourceKey);
        if (quickDev is null) return null;
        // Text heuristics only here — followUps not available until slots are built.
        return UnplannedWorkGeometry.ResolveQuickDevEpic(quickDev, epics, followUps: null);
    }

    /// <summary>Fills null <see cref="FollowUpDeferredSlot.EpicNumber"/> for slots whose SourceKey is a
    /// quick-dev, using the completed slot set + retros so date attribution matches page chrome.</summary>
    private static IReadOnlyList<FollowUpDeferredSlot> EnrichQuickDevDeferredEpics(
        IReadOnlyList<FollowUpDeferredSlot> slots,
        EpicsModel? epics,
        WorkInventory work,
        IReadOnlyList<RetroModel>? retros = null)
    {
        if (epics is null || slots.Count == 0) return slots;

        var temp = new FollowUpGeometry(
            Array.Empty<SprintActionItem>(),
            0,
            null,
            SiteNav.ActionItemsOutputPath,
            new Dictionary<SprintActionItem, string>(),
            slots);

        var changed = false;
        var enriched = new List<FollowUpDeferredSlot>(slots.Count);
        foreach (var slot in slots)
        {
            if (slot.EpicNumber is not null || string.IsNullOrWhiteSpace(slot.SourceKey))
            {
                enriched.Add(slot);
                continue;
            }

            var quickDev = FindQuickDev(work, slot.SourceKey);
            if (quickDev is null)
            {
                enriched.Add(slot);
                continue;
            }

            var epic = UnplannedWorkGeometry.ResolveQuickDevEpic(quickDev, epics, temp, retros);
            if (epic is null)
            {
                enriched.Add(slot);
                continue;
            }

            changed = true;
            enriched.Add(slot with { EpicNumber = epic });
        }

        return changed ? enriched : slots;
    }

    /// <summary>Matches a provenance key to a <see cref="QuickDevEntry"/> by output stem or filename.</summary>
    public static QuickDevEntry? FindQuickDev(WorkInventory work, string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return null;
        var bare = sourceKey.Trim().Trim('`');
        if (bare.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) bare = bare[..^3];
        if (bare.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) bare = bare[..^5];

        foreach (var q in work.QuickDev)
        {
            var stem = Path.GetFileNameWithoutExtension(q.OutputPath);
            var file = Path.GetFileName(q.OutputPath);
            if (string.Equals(stem, bare, StringComparison.OrdinalIgnoreCase)) return q;
            if (string.Equals(file, bare, StringComparison.OrdinalIgnoreCase)) return q;
            if (string.Equals(file, bare + ".html", StringComparison.OrdinalIgnoreCase)) return q;
            if (string.Equals(stem + ".md", bare + ".md", StringComparison.OrdinalIgnoreCase)
                && string.Equals(stem, bare, StringComparison.OrdinalIgnoreCase))
                return q;
        }
        return null;
    }

    private static string? ResolveSourceHref(WorkInventory work, string? sourceKey, string linkPrefix)
    {
        var qd = FindQuickDev(work, sourceKey);
        return qd is null ? null : FollowUpGeometry.ApplyLinkPrefix(linkPrefix, qd.OutputPath);
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
