using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One open quick-dev / one-shot entry projected for sunburst + sprint Unplanned surfaces.
/// Epic-attributed entries render under that epic's story ring; unattributed ones join the synthetic
/// Unplanned root / Unplanned lane. Never labeled as a story. [Story 9.12]</summary>
public sealed record UnplannedQuickDevSlot(
    QuickDevEntry Entry,
    int? EpicNumber,
    string Href);

/// <summary>One member of the shared Unplanned membership set (sunburst root ↔ sprint lane equality).
/// <see cref="Kind"/> is <c>direct</c> (quick-dev) or <c>deferred</c>. [Story 9.12]</summary>
public sealed record UnplannedMember(
    string Kind,
    string Title,
    string Href,
    string? Status,
    bool IsDone,
    string? SourceKey = null,
    string? SourceHref = null);

/// <summary>Projection of unplanned / one-off work for the project sunburst and sprint board.
/// Membership = open unattributable quick-dev + unattributable deferred slots (from
/// <see cref="FollowUpGeometry"/>). Unattributed action items stay on the Follow-ups orphan (9.13).
/// Counts: open quick-dev is a subset of <see cref="ProjectCounts.DirectChanges"/> (all statuses);
/// unattributable deferred are the unattributed open subset of ledger-backed deferred slots —
/// never a second parse of deferred markdown. [Story 9.12]</summary>
public sealed record UnplannedWorkGeometry(
    IReadOnlyList<UnplannedQuickDevSlot> QuickDevSlots,
    IReadOnlyList<FollowUpDeferredSlot> UnattributableDeferred,
    string? DeferredListHref = null,
    string LinkPrefix = "")
{
    private static readonly Regex StoryMention = new(
        @"\bStory\s+(\d+)\.(\d+)\b(?!\.\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EpicMention = new(
        @"\bEpic\s+(\d+)\b(?!\.\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static UnplannedWorkGeometry Empty { get; } = new(
        Array.Empty<UnplannedQuickDevSlot>(),
        Array.Empty<FollowUpDeferredSlot>());

    /// <summary>Open quick-dev with no resolvable epic — Unplanned root / lane members.</summary>
    public IReadOnlyList<UnplannedQuickDevSlot> UnplannedQuickDev =>
        QuickDevSlots.Where(s => s.EpicNumber is null).ToList();

    /// <summary>Open quick-dev attributed to a specific epic (story-ring peers).</summary>
    public IReadOnlyList<UnplannedQuickDevSlot> ForEpic(int epicNumber) =>
        QuickDevSlots.Where(s => s.EpicNumber == epicNumber).ToList();

    public bool HasUnplanned => UnplannedMemberCount > 0;

    public int UnplannedMemberCount => UnplannedQuickDev.Count + UnattributableDeferred.Count;

    /// <summary>Shared membership set for sunburst Unplanned root and sprint Unplanned lane —
    /// one source of truth; tests pin equality across surfaces. [Story 9.12]</summary>
    public IReadOnlyList<UnplannedMember> UnplannedSet =>
        UnplannedQuickDev
            .Select(s => new UnplannedMember(
                "direct",
                s.Entry.Title,
                s.Href,
                s.Entry.Status,
                !IsOpenQuickDev(s.Entry.Status)))
            .Concat(UnattributableDeferred.Select(d => new UnplannedMember(
                "deferred",
                PathUtil.StripHtmlTags(FollowUpRow.SummarizeFromHtml(d.Item.BodyHtml)),
                d.DetailHref,
                d.Item.Resolved ? "done" : "open",
                d.Item.Resolved,
                SourceKey: d.SourceKey,
                SourceHref: d.SourceHref)))
            .ToList();

    /// <summary>Stable filtered Unplanned group page (<c>follow-ups/group-unplanned.html</c>).
    /// Prefixed for epic-depth pages via <see cref="LinkPrefix"/>. Null when the set is empty (NFR8). [Story 9.13]</summary>
    public string? GroupRootHref =>
        HasUnplanned
            ? FollowUpGeometry.ApplyLinkPrefix(LinkPrefix, FollowUpGroupPages.UnplannedPath)
            : null;

    /// <summary>Null/empty status counts as open (remaining work). Done/resolved (case-insensitive) are closed.</summary>
    public static bool IsOpenQuickDev(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return true;
        var s = status.Trim();
        return !s.Equals("done", StringComparison.OrdinalIgnoreCase)
            && !s.Equals("resolved", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds unplanned geometry from inventory + already-projected follow-up slots.
    /// Reuses <see cref="FollowUpGeometry.UnattributedDeferredItems"/> — never re-parses deferred markdown.
    /// Done quick-dev that still have open deferred from their code review are re-surfaced as parents so
    /// residual work stays attached to the item it stemmed from. [Story 9.12]</summary>
    public static UnplannedWorkGeometry From(
        WorkInventory work,
        FollowUpGeometry followUps,
        EpicsModel? epics = null,
        string linkPrefix = "")
    {
        var unattributedDeferred = followUps.UnattributedDeferredItems
            .Select(s => s with { DetailHref = FollowUpGeometry.ApplyLinkPrefix(linkPrefix, s.DetailHref) })
            .ToList();

        var residualSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in unattributedDeferred)
        {
            if (slot.Item.Resolved) continue;
            if (slot.SourceKey is { Length: > 0 } key)
                residualSourceKeys.Add(FollowUpGeometry.NormalizeSourceKey(key));
        }

        var quickDev = work.QuickDev
            .Where(q => IsOpenQuickDev(q.Status) || residualSourceKeys.Contains(FollowUpGeometry.NormalizeSourceKey(Path.GetFileNameWithoutExtension(q.OutputPath))))
            .Select(q =>
            {
                var epic = ResolveQuickDevEpic(q, epics, followUps);
                var href = FollowUpGeometry.ApplyLinkPrefix(linkPrefix, q.OutputPath);
                return new UnplannedQuickDevSlot(q, epic, href);
            })
            .ToList();

        // Deferred that inherit an epic via parent quick-dev are already attributed in FollowUpGeometry.
        // Remaining unattributable deferred stay here — including children of unplanned quick-dev parents.
        var deferred = unattributedDeferred
            .Where(s => s.EpicNumber is null)
            .ToList();

        return new UnplannedWorkGeometry(quickDev, deferred, DeferredListHref: null, linkPrefix);
    }

    private static readonly Regex ProvenanceParen = new(
        @"\(([^)]+)\)", RegexOptions.Compiled);

    /// <summary>Best-effort epic attribution: text heuristics first (title, filename, deferred cue that
    /// names this <c>spec-*</c>); then unique timing from authored frontmatter date vs deferred
    /// story-review dates under one epic. Multi-epic ties → null (leave Unplanned). No new schema.</summary>
    public static int? ResolveQuickDevEpic(
        QuickDevEntry entry,
        EpicsModel? epics,
        FollowUpGeometry? followUps = null)
    {
        if (epics is null) return null;

        var epicNumbers = new HashSet<int>(epics.Epics.Select(e => e.Number));

        // Filename stem: "spec-…" won't match StoryIdFromKey; also try title for "N-M-slug" / "Story N.M".
        foreach (var token in CandidateTokens(entry))
        {
            var storyId = FollowUpRefs.StoryIdFromKey(token);
            if (storyId is not null)
            {
                var epic = EpicFromStoryId(epics, storyId);
                if (epic is not null) return epic;
            }
        }

        var haystack = $"{entry.Title} {Path.GetFileNameWithoutExtension(entry.OutputPath)}";
        var storyMention = StoryMention.Match(haystack);
        if (storyMention.Success
            && int.TryParse(storyMention.Groups[1].Value, out var se)
            && epicNumbers.Contains(se))
            return se;

        var epicMention = EpicMention.Match(haystack);
        if (epicMention.Success
            && int.TryParse(epicMention.Groups[1].Value, out var en)
            && epicNumbers.Contains(en))
            return en;

        // Optional: deferred items that name this spec and already carry an epic — unique only (ties → null).
        if (followUps is not null)
        {
            var stem = Path.GetFileNameWithoutExtension(entry.OutputPath);
            var cueHits = new HashSet<int>();
            foreach (var slot in followUps.DeferredItems)
            {
                if (slot.EpicNumber is not { } epic) continue;
                var body = PathUtil.StripHtmlTags(slot.Item.BodyHtml);
                var provenance = slot.ProvenanceLabel;
                if (ContainsSpecName(body, stem) || ContainsSpecName(provenance, stem))
                    cueHits.Add(epic);
            }
            if (cueHits.Count == 1) return cueHits.First();
            if (cueHits.Count > 1) return null;
        }

        // Last: unique same-day timing vs story-keyed deferred reviews — only when this quick-dev
        // already has residual deferred naming it (avoids same-day coincidence false parents).
        if (entry.AuthoredDate is { } authored && followUps is not null)
        {
            var stem = Path.GetFileNameWithoutExtension(entry.OutputPath);
            var named = followUps.DeferredItems.Any(s =>
                !string.IsNullOrWhiteSpace(s.SourceKey)
                && string.Equals(
                    FollowUpGeometry.NormalizeSourceKey(s.SourceKey),
                    FollowUpGeometry.NormalizeSourceKey(stem),
                    StringComparison.OrdinalIgnoreCase));
            if (named)
                return ResolveEpicByTiming(authored, followUps, epicNumbers);
        }

        return null;
    }

    /// <summary>When the quick-dev's authored day uniquely matches deferred story-review dates under
    /// exactly one epic, attribute there. Two+ epics on that day → null (do not guess).</summary>
    private static int? ResolveEpicByTiming(
        DateOnly authored,
        FollowUpGeometry followUps,
        HashSet<int> epicNumbers)
    {
        var hits = new HashSet<int>();
        foreach (var slot in followUps.DeferredItems)
        {
            if (slot.EpicNumber is not { } epic || !epicNumbers.Contains(epic)) continue;
            // Story-keyed reviews only — timing cue is "reviews of stories under one epic".
            if (slot.SourceKey is null || FollowUpRefs.StoryIdFromKey(slot.SourceKey) is null) continue;
            if (!TryExtractProvenanceDate(slot.ProvenanceLabel, out var day)) continue;
            if (day == authored) hits.Add(epic);
        }

        return hits.Count == 1 ? hits.First() : null;
    }

    private static bool TryExtractProvenanceDate(string provenanceLabel, out DateOnly day)
    {
        day = default;
        var m = ProvenanceParen.Match(provenanceLabel);
        return m.Success && PortalDates.TryParseDay(m.Groups[1].Value.Trim(), out day);
    }

    private static IEnumerable<string> CandidateTokens(QuickDevEntry entry)
    {
        yield return entry.Title;
        var file = Path.GetFileName(entry.OutputPath);
        if (!string.IsNullOrEmpty(file)) yield return file;
        var stem = Path.GetFileNameWithoutExtension(entry.OutputPath);
        if (!string.IsNullOrEmpty(stem)) yield return stem;
    }

    private static int? EpicFromStoryId(EpicsModel epics, string storyId)
    {
        foreach (var epic in epics.Epics)
        foreach (var story in epic.Stories)
            if (string.Equals(story.Id, storyId, StringComparison.Ordinal))
                return epic.Number;

        var dot = storyId.IndexOf('.');
        if (dot > 0
            && int.TryParse(storyId.AsSpan(0, dot), out var epicNum)
            && epics.Epics.Any(e => e.Number == epicNum))
            return epicNum;
        return null;
    }

    private static bool ContainsSpecName(string text, string stem)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(stem)) return false;
        return text.Contains(stem, StringComparison.OrdinalIgnoreCase)
            || text.Contains(stem + ".md", StringComparison.OrdinalIgnoreCase)
            || text.Contains(stem + ".html", StringComparison.OrdinalIgnoreCase);
    }
}
