using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One code file attributed to an epic/story by the impact map: its repo-relative path plus the
/// output-relative in-portal code-page href when one exists (<c>null</c> when the file has no code page — only
/// possible in the resolver-less unit-test mode; production always filters those out). Rendering surfaces apply
/// their own page prefix to <see cref="CodePageHref"/>. [Story 21.3]</summary>
public sealed record ImpactFile(string Path, string? CodePageHref);

/// <summary>The best-effort correlation between planning items (epics/stories) and the code files their commits
/// touched, mined from commit-message + merge-branch naming (Story 21.3). <see cref="FilesByEpic"/> is keyed by
/// epic number, <see cref="FilesByStory"/> by story id ("N.M"); both hold ONLY files that survived the code-page
/// link gate (never a dead link) and are ordinal-sorted for determinism. <see cref="AttributedCommitCount"/> /
/// <see cref="TotalAnalyzedCommits"/> back the honest "N of M analyzed commits correlated" caveat — a commit is
/// "attributed" when it resolved to at least one real epic/story (by direct match or merge-branch backfill),
/// regardless of whether any of its files were linkable. Empty (never null) dictionaries when nothing correlated.
/// This is a heuristic, not a tracked mapping — its provenance caveat must render wherever it surfaces.</summary>
public sealed record PlanningCodeImpactData(
    IReadOnlyDictionary<int, IReadOnlyList<ImpactFile>> FilesByEpic,
    IReadOnlyDictionary<string, IReadOnlyList<ImpactFile>> FilesByStory,
    int AttributedCommitCount,
    int TotalAnalyzedCommits)
{
    /// <summary>No commit data (or a failure): an honest empty result, never an exception. [AD-4 / NFR2]</summary>
    public static PlanningCodeImpactData Empty { get; } = new(
        new Dictionary<int, IReadOnlyList<ImpactFile>>(),
        new Dictionary<string, IReadOnlyList<ImpactFile>>(),
        0,
        0);

    /// <summary>True when no epic or story ended up with any linkable touched file — the dedicated page then
    /// renders its honest empty note rather than a misleading empty grid, and every epic/story widget is
    /// absent (AC #2, NFR8).</summary>
    public bool HasAnyFiles => FilesByEpic.Count > 0 || FilesByStory.Count > 0;
}

/// <summary>Correlates planning items (epics/stories) with the code files their commits actually touched, mined
/// purely from commit-message and merge-branch naming that already exists in any repo's history — no new
/// authoring convention, no <c>[Source:]</c> citation, no second git call (reuses the bounded <c>--deep-git</c>
/// numstat fetch). A two-tier, single-pass, best-effort heuristic:
/// <list type="number">
/// <item>Tier 1 — direct match: a commit subject/body that names a real epic/story (validated against the roster).</item>
/// <item>Tier 2 — merge/branch backfill: a merge commit whose branch name names a real epic/story attributes it to
/// the run of immediately-following, still-unattributed commits up to the next merge boundary (a linear-window
/// approximation of "which commits this branch merged", deliberately NOT a parent-hash DAG walk).</item>
/// </list>
/// Every candidate number is validated against the actual <see cref="EpicsModel"/> roster before it is trusted, so
/// a coincidental or malformed number ("Code review on 7.x", an ISO date) is discarded, never guessed into
/// existence. Pure over its inputs; never throws (a malformed anything degrades to an honest empty result).
/// [Story 21.3]</summary>
public static class PlanningCodeImpact
{
    /// <summary>One raw work-item reference extracted from free text: an epic number and, when the text named a
    /// story, its story number (else null for an epic-level mention). Raw — roster validation happens in the
    /// caller so this stays a pure, fixture-free text function.</summary>
    public readonly record struct WorkItemRef(int Epic, int? Story);

    // A story/epic pair in either dotted ("6.7") or hyphenated ("6-4", from a branch name like
    // worktree-bmad-dev-story-6-4) form. Digit-boundary lookarounds keep it from biting into a longer number and
    // stop an ISO date's leading 4-digit year from ever validating (epic 2026 is never on any roster). A bare
    // pair is deliberately caught (branch names carry no "Story " keyword) — the roster validation in the caller
    // is what keeps a stray "2.5" or "4-8" from misattributing.
    private static readonly Regex WorkPair = new(
        @"(?<!\d)(\d+)[.-](\d+)(?!\d)", RegexOptions.Compiled);

    // An epic-only mention ("Epic 19", "epic-24") — one number, no story part. The pair regex above needs two
    // numbers, so epic-level references need their own pattern.
    private static readonly Regex EpicOnly = new(
        @"\bEpic[\s-]+(\d+)(?![.\-]?\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // A merge commit — git's default merge messages ("Merge branch 'x'", "Merge pull request #n from o/x",
    // "Merge remote-tracking branch 'origin/x'"). Its branch-name text is a zero-cost Tier-2 signal even though
    // its own numstat file set is always empty (git doesn't diff merges under the shared --numstat fetch).
    private static readonly Regex MergeSubject = new(
        @"^Merge\b", RegexOptions.Compiled);

    /// <summary>Extracts every work-item reference candidate from one piece of free text (a commit subject+body,
    /// or a merge commit's branch-name subject). Pure and fixture-free: it returns RAW candidates in first-seen
    /// order (deduped); validation against the real <see cref="EpicsModel"/> roster is the caller's job, so this
    /// function is unit-testable with literal strings and no model fixture. Never throws; empty/blank → empty.</summary>
    public static IReadOnlyList<WorkItemRef> TryExtractWorkItemRefs(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<WorkItemRef>();

        var seen = new HashSet<WorkItemRef>();
        var results = new List<WorkItemRef>();

        void Add(WorkItemRef reference)
        {
            if (seen.Add(reference)) results.Add(reference);
        }

        foreach (Match m in WorkPair.Matches(text))
        {
            if (int.TryParse(m.Groups[1].Value, out var epic) && int.TryParse(m.Groups[2].Value, out var story))
            {
                Add(new WorkItemRef(epic, story));
            }
        }

        foreach (Match m in EpicOnly.Matches(text))
        {
            if (int.TryParse(m.Groups[1].Value, out var epic))
            {
                Add(new WorkItemRef(epic, null));
            }
        }

        return results;
    }

    /// <summary>Builds the correlation from the epics roster and the shared deep-git commit list. The optional
    /// <paramref name="codePageResolver"/> maps a repo-relative file path to its output-relative in-portal
    /// code-page href (<c>null</c> when the file has no page); when supplied it doubles as the "is this a real,
    /// linkable file" gate — a file it can't resolve is dropped so the impact map never emits a dead link. When
    /// it is <c>null</c> (attribution-only unit tests) no files are dropped and each carries a null href. Never
    /// throws: zero commits, zero epics, or zero matches all return an honest empty result. [AC #1, #2]</summary>
    public static PlanningCodeImpactData Build(
        EpicsModel epics,
        IReadOnlyList<DeepCommit> commits,
        Func<string, string?>? codePageResolver = null)
    {
        if (epics is null || epics.Epics.Count == 0 || commits is null || commits.Count == 0)
            return PlanningCodeImpactData.Empty;

        var validEpics = new HashSet<int>();
        var validStories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var epic in epics.Epics)
        {
            validEpics.Add(epic.Number);
            foreach (var story in epic.Stories)
            {
                validStories.Add(story.Id);
            }
        }

        // Raw path accumulators (dedup within each set); resolved/sorted/filtered at the end.
        var epicFiles = new Dictionary<int, HashSet<string>>();
        var storyFiles = new Dictionary<string, HashSet<string>>();

        HashSet<string> EpicSet(int epic) =>
            epicFiles.TryGetValue(epic, out var set) ? set : epicFiles[epic] = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> StorySet(string id) =>
            storyFiles.TryGetValue(id, out var set) ? set : storyFiles[id] = new HashSet<string>(StringComparer.Ordinal);

        // Validate a raw candidate list against the roster: a story ref survives only when the exact "N.M" is a
        // real story; an epic-only ref only when the epic number is real. Nothing is guessed into existence.
        List<WorkItemRef> Validate(IEnumerable<WorkItemRef> raw)
        {
            var kept = new List<WorkItemRef>();
            foreach (var reference in raw)
            {
                if (reference.Story is int s)
                {
                    if (validStories.Contains($"{reference.Epic}.{s}")) kept.Add(reference);
                }
                else if (validEpics.Contains(reference.Epic))
                {
                    kept.Add(reference);
                }
            }
            return kept;
        }

        void Attribute(IReadOnlyList<WorkItemRef> refs, IReadOnlyList<DeepFileChange> files)
        {
            if (refs.Count == 0 || files.Count == 0) return;
            foreach (var reference in refs)
            {
                if (reference.Story is int s)
                {
                    var id = $"{reference.Epic}.{s}";
                    var storySet = StorySet(id);
                    var epicSet = EpicSet(reference.Epic); // story files roll up into the parent epic's set
                    foreach (var f in files)
                    {
                        storySet.Add(f.Path);
                        epicSet.Add(f.Path);
                    }
                }
                else
                {
                    var epicSet = EpicSet(reference.Epic);
                    foreach (var f in files) epicSet.Add(f.Path);
                }
            }
        }

        var attributedCommits = 0;
        // The active merge/branch reference set backfilled onto still-unattributed commits until the next merge
        // boundary. Reset (to the new merge's refs, possibly empty) each time a merge is encountered — a linear,
        // single-pass approximation of branch membership over the already-ordered (newest-first) commit list.
        IReadOnlyList<WorkItemRef> activeMergeRefs = Array.Empty<WorkItemRef>();

        foreach (var commit in commits)
        {
            var tier1 = Validate(TryExtractWorkItemRefs(commit.Subject + "\n" + commit.Body));

            if (MergeSubject.IsMatch(commit.Subject))
            {
                // A merge is a window boundary: its branch-name refs govern the following unattributed commits.
                activeMergeRefs = tier1;
                if (tier1.Count > 0)
                {
                    // The merge self-identifies; count it as attributed even though its own file set is empty.
                    attributedCommits++;
                    Attribute(tier1, commit.Files);
                }
                continue;
            }

            if (tier1.Count > 0)
            {
                attributedCommits++;
                Attribute(tier1, commit.Files);
            }
            else if (activeMergeRefs.Count > 0)
            {
                attributedCommits++;
                Attribute(activeMergeRefs, commit.Files);
            }
        }

        var filesByEpic = Resolve(epicFiles, codePageResolver);
        var filesByStory = Resolve(storyFiles, codePageResolver);

        return new PlanningCodeImpactData(filesByEpic, filesByStory, attributedCommits, commits.Count);
    }

    /// <summary>Turns a raw path-set map into the final ordinal-sorted, link-gated <see cref="ImpactFile"/> lists.
    /// When a resolver is supplied a file with no resolvable code page is dropped (never a dead link); empty
    /// entries are omitted entirely so a key never survives with zero files.</summary>
    private static IReadOnlyDictionary<TKey, IReadOnlyList<ImpactFile>> Resolve<TKey>(
        Dictionary<TKey, HashSet<string>> raw,
        Func<string, string?>? codePageResolver)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, IReadOnlyList<ImpactFile>>();
        foreach (var (key, paths) in raw)
        {
            var files = new List<ImpactFile>();
            foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal))
            {
                if (codePageResolver is null)
                {
                    files.Add(new ImpactFile(path, null));
                }
                else
                {
                    var href = codePageResolver(path);
                    if (href is { Length: > 0 }) files.Add(new ImpactFile(path, href));
                }
            }
            if (files.Count > 0) result[key] = files;
        }
        return result;
    }
}
