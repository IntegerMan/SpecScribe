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
/// Board membership = open unattributable/orphan quick-dev + open orphan deferred + done quick-dev
/// parents resurfaced when open deferred still names them. Sunburst weight counts open members only
/// (hybrid residual). Unattributed action items stay on the Follow-ups orphan (9.13).
/// Counts: open quick-dev is a subset of <see cref="ProjectCounts.DirectChanges"/> (all statuses);
/// orphan deferred are the unattributed open subset of ledger-backed deferred slots —
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

    /// <summary>Quick-dev with no resolvable epic — Unplanned root / lane members (may include resurfaced done parents).</summary>
    public IReadOnlyList<UnplannedQuickDevSlot> UnplannedQuickDev =>
        QuickDevSlots.Where(s => s.EpicNumber is null).ToList();

    /// <summary>Open quick-dev attributed to a specific epic (story-ring peers).</summary>
    public IReadOnlyList<UnplannedQuickDevSlot> ForEpic(int epicNumber) =>
        QuickDevSlots.Where(s => s.EpicNumber == epicNumber).ToList();

    /// <summary>Board / group-page presence — includes resurfaced done residual parents.</summary>
    public bool HasUnplanned => UnplannedMemberCount > 0;

    /// <summary>Full board membership count (open + resurfaced done residual parents + open deferred).</summary>
    public int UnplannedMemberCount => UnplannedQuickDev.Count + UnattributableDeferred.Count;

    /// <summary>Sunburst Unplanned root weight — open members only (excludes resurfaced done residual parents). [Story 9.12 hybrid]</summary>
    public int SunburstUnplannedWeight =>
        UnplannedQuickDev.Count(q => IsOpenQuickDev(q.Entry.Status))
        + UnattributableDeferred.Count(s => !s.Item.Resolved);

    /// <summary>Shared membership set for sprint Unplanned lane + group page —
    /// one source of truth for board cards. Sunburst weight uses <see cref="SunburstUnplannedWeight"/>. [Story 9.12]</summary>
    public IReadOnlyList<UnplannedMember> UnplannedSet =>
        UnplannedQuickDev
            .Select(s => new UnplannedMember(
                "direct",
                DisplayTitle(s.Entry.Title),
                s.Href,
                s.Entry.Status,
                !IsOpenQuickDev(s.Entry.Status)))
            .Concat(UnattributableDeferred.Select(d => new UnplannedMember(
                "deferred",
                DisplayTitle(PathUtil.StripHtmlTags(FollowUpRow.SummarizeFromHtml(d.Item.BodyHtml))),
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

    /// <summary>Non-empty title for wedges/cards; blank/whitespace → <c>(no title)</c>.</summary>
    public static string DisplayTitle(string? title)
    {
        var t = PathUtil.StripHtmlTags(title ?? string.Empty).Trim();
        return t.Length > 0 ? t : "(no title)";
    }

    /// <summary>Builds unplanned geometry from inventory + already-projected follow-up slots.
    /// Reuses orphan deferred (null epic or unknown epic number) — never re-parses deferred markdown.
    /// Done quick-dev that still have open deferred from their code review are re-surfaced as parents for
    /// the board; sunburst weight excludes them. [Story 9.12]</summary>
    public static UnplannedWorkGeometry From(
        WorkInventory work,
        FollowUpGeometry followUps,
        EpicsModel? epics = null,
        string linkPrefix = "",
        IReadOnlyList<RetroModel>? retros = null)
    {
        IReadOnlyList<FollowUpDeferredSlot> orphanDeferred;
        if (epics is not null)
        {
            var known = epics.Epics.Select(e => e.Number).ToHashSet();
            orphanDeferred = followUps.OrphanDeferredItems(known);
        }
        else
            orphanDeferred = followUps.UnattributedDeferredItems;

        var unattributedDeferred = orphanDeferred
            .Where(s => !s.Item.Resolved)
            .Select(s => s with { DetailHref = FollowUpGeometry.ApplyLinkPrefix(linkPrefix, s.DetailHref) })
            .ToList();

        var residualSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in unattributedDeferred)
        {
            if (slot.SourceKey is { Length: > 0 } key)
                residualSourceKeys.Add(FollowUpGeometry.NormalizeSourceKey(key));
        }

        var quickDev = work.QuickDev
            .Where(q => IsOpenQuickDev(q.Status) || residualSourceKeys.Contains(FollowUpGeometry.NormalizeSourceKey(Path.GetFileNameWithoutExtension(q.OutputPath))))
            .Select(q =>
            {
                var epic = ResolveQuickDevEpic(q, epics, followUps, retros);
                var href = FollowUpGeometry.ApplyLinkPrefix(linkPrefix, q.OutputPath);
                return new UnplannedQuickDevSlot(q, epic, href);
            })
            .ToList();

        // Deferred that inherit an epic via parent quick-dev are already attributed in FollowUpGeometry.
        // Remaining orphan deferred stay here — including children of unplanned quick-dev parents.
        return new UnplannedWorkGeometry(quickDev, unattributedDeferred, DeferredListHref: followUps.DeferredHref, linkPrefix);
    }

    private static readonly Regex ProvenanceParen = new(
        @"\(([^)]+)\)", RegexOptions.Compiled);

    /// <summary>Best-effort epic attribution: text heuristics first (title, filename, deferred cue that
    /// names this <c>spec-*</c>); then unique timing from authored frontmatter date vs retro
    /// <see cref="RetroModel.DateText"/> and/or story <see cref="StoryInfo.LastUpdatedDate"/> under one
    /// epic; then fallback to deferred review dates. Multi-epic ties → null (leave Unplanned). No new
    /// schema. [spec-sunburst-remaining-work-hierarchy]</summary>
    public static int? ResolveQuickDevEpic(
        QuickDevEntry entry,
        EpicsModel? epics,
        FollowUpGeometry? followUps = null,
        IReadOnlyList<RetroModel>? retros = null)
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

        // Date-based attribution: unique-day match AuthoredDate to retro DateText or story LastUpdatedDate
        // under exactly one epic. [spec-sunburst-remaining-work-hierarchy]
        if (entry.AuthoredDate is { } authored)
        {
            var dateHit = ResolveEpicByDateMatch(authored, epics, retros);
            if (dateHit is not null) return dateHit;
        }

        // Last: unique same-day timing vs story-keyed deferred reviews — only when this quick-dev
        // already has residual deferred naming it (avoids same-day coincidence false parents).
        if (entry.AuthoredDate is { } authored2 && followUps is not null)
        {
            var stem = Path.GetFileNameWithoutExtension(entry.OutputPath);
            var named = followUps.DeferredItems.Any(s =>
                !string.IsNullOrWhiteSpace(s.SourceKey)
                && string.Equals(
                    FollowUpGeometry.NormalizeSourceKey(s.SourceKey),
                    FollowUpGeometry.NormalizeSourceKey(stem),
                    StringComparison.OrdinalIgnoreCase));
            if (named)
                return ResolveEpicByTiming(authored2, followUps, epicNumbers);
        }

        return null;
    }

    /// <summary>Cascaded unique-day match (Design Notes resolve order):
    /// 1) unique <paramref name="authored"/> vs epic <see cref="RetroModel.DateText"/>;
    /// 2) else unique vs story <see cref="StoryInfo.LastUpdatedDate"/> owned by one epic;
    /// else null. Retro ties abort without falling through. Unknown retro epic numbers ignored.
    /// [spec-sunburst-remaining-work-hierarchy]</summary>
    private static int? ResolveEpicByDateMatch(
        DateOnly authored,
        EpicsModel epics,
        IReadOnlyList<RetroModel>? retros)
    {
        var epicNumbers = new HashSet<int>(epics.Epics.Select(e => e.Number));

        // Tier 1 — retro DateText (unique only; ties → null, no fallthrough).
        if (retros is { Count: > 0 })
        {
            var retroHits = new HashSet<int>();
            foreach (var retro in retros)
            {
                if (!epicNumbers.Contains(retro.EpicNumber)) continue;
                if (retro.DateText is not { Length: > 0 } dt) continue;
                if (!PortalDates.TryParseDay(dt, out var retroDay)) continue;
                if (retroDay == authored)
                    retroHits.Add(retro.EpicNumber);
            }
            if (retroHits.Count == 1) return retroHits.First();
            if (retroHits.Count > 1) return null;
        }

        // Tier 2 — story LastUpdatedDate under exactly one epic.
        var storyHits = new HashSet<int>();
        foreach (var epic in epics.Epics)
        {
            foreach (var story in epic.Stories)
            {
                if (story.LastUpdatedDate == authored)
                {
                    storyHits.Add(epic.Number);
                    break;
                }
            }
        }

        return storyHits.Count == 1 ? storyHits.First() : null;
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

    /// <summary>True when <paramref name="text"/> names <paramref name="stem"/> as a hyphen-aware token
    /// (optional <c>.md</c>/<c>.html</c>). Plain <c>Contains</c> would let <c>spec-a</c> hit <c>spec-ab</c>.
    /// Guards match <see cref="RenderParity"/> status-class boundaries — <c>\b</c> treats <c>-</c> as a break.</summary>
    private static bool ContainsSpecName(string text, string stem)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(stem)) return false;
        // (?<![\w-])stem(?:\.(?:md|html))?(?![\w-]) — case-insensitive.
        var pattern = $@"(?<![\w-]){Regex.Escape(stem)}(?:\.(?:md|html))?(?![\w-])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
