using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>One commit's headline identity: enough for a "what landed that day" list without a full log.
/// <paramref name="Time"/> is the author-local "HH:mm" of the commit.</summary>
public sealed record CommitInfo(string ShortHash, string Subject, string Author, string Time);

/// <summary>A lightweight snapshot of repo activity, for the dashboard's "project pulse".
/// <para><paramref name="LastCommitTimestamp"/> is the exact date+time of the most recent commit,
/// <paramref name="Last30DayCommitCount"/> the rolling count over the trailing 30 days, and
/// <paramref name="TopChangedFiles"/> the most-frequently-touched files over a bounded recent window —
/// the three baseline signals FR-9 requires on the dashboard. <paramref name="TopChangedFiles"/> degrades
/// to an empty list (never null) when the name-only git call fails even though the rest of the pulse
/// succeeded, so partial data still renders. [Story 3.1]</para></summary>
public sealed record GitPulse(
    int TotalCommits,
    int ActiveDays,
    DateOnly FirstCommitDate,
    DateOnly LastCommitDate,
    IReadOnlyList<(DateOnly Day, int Count)> DailySeries,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> CommitsByDay,
    DateTime LastCommitTimestamp,
    int Last30DayCommitCount,
    IReadOnlyList<(string Path, int ChangeCount)> TopChangedFiles);

/// <summary>The opt-in deep-git signals (FR-10): file-path <paramref name="Hotspots"/> (which files change
/// most often) and <paramref name="Coupling"/> (which file pairs change together). Both are purely file-path
/// signals — never author/productivity signals (PRD non-goal). Populated only when <c>--deep-git</c> is set;
/// a null <see cref="DeepGitPulse"/> means "not opted in, or deep analysis failed" and the panel is omitted
/// entirely rather than shown empty. <paramref name="AnalyzedCommits"/> is the honest window size (parsed
/// commit count from the bounded <c>-n 300</c> fetch — never a hard-coded "300"). [Story 3.2; Story 10.2]</summary>
public sealed record DeepGitPulse(
    IReadOnlyList<(string Path, int Changes)> Hotspots,
    IReadOnlyList<CoupledPair> Coupling,
    int AnalyzedCommits = 0)
{
    /// <summary>The Git Insights hub aggregates (file frequency + churn, contributor attribution, activity)
    /// computed from the SAME shared numstat parse — one fetch, one parse, several views. Settable (not
    /// <c>init</c>) for the same reason <see cref="ProgressModel.DeepGit"/> is: <see cref="SiteGenerator"/>
    /// clears it if writing <c>git-insights.html</c> fails, so the dashboard's "View all git insights" link is
    /// never left pointing at a page that doesn't exist. [Story 3.8]</summary>
    public GitInsightsData? Insights { get; set; }

    /// <summary>The per-commit records parsed from the SAME shared numstat fetch (one fetch, one parse, several
    /// views), surfaced so Story 7.5 can render a per-commit detail page (<c>commit/{shortHash}.html</c>) without
    /// re-fetching. Newest-first (git log order, as <see cref="GitMetrics.ParseNumstatRecords"/> emits). Empty
    /// (never null) when the log was empty or predates the enriched fetch — the per-commit phase then generates
    /// no pages and the hub/day-page hash links stay plain (guarded). [Story 7.5]</summary>
    public IReadOnlyList<DeepCommit> Commits { get; init; } = Array.Empty<DeepCommit>();

    /// <summary>Per-file deep-git signals keyed by repo-relative path (git's own forward-slash paths, the same
    /// strings the numstat rows carry — so it joins cleanly to the code-page path map), for the opt-in
    /// "Advanced coverage" section on Story 7.1's code pages (Story 7.4). Computed from the SAME shared numstat
    /// parse (one fetch, one parse, several views) — no extra git call. Empty (never null) when the log was empty
    /// or predates the enriched fetch, so a file with no insight renders no section and its baseline code page is
    /// untouched. [Story 7.4]</summary>
    public IReadOnlyDictionary<string, FileInsight> FileInsights { get; init; }
        = new Dictionary<string, FileInsight>(StringComparer.Ordinal);

    /// <summary>The untruncated per-file metric view for the source-code treemap (Story 7.6): ONE entry per file
    /// that appears anywhere in the analyzed window — deliberately NOT top-N truncated like <see cref="Insights"/>,
    /// so a whole-codebase treemap can colorize every file that has git history. Keyed by repo-relative path (git's
    /// own forward-slash paths, the same strings the numstat rows carry — so it joins cleanly to the treemap's
    /// source-file walk). Computed from the SAME shared numstat parse (one fetch, one parse, several views) — no
    /// extra git call. Settable (mirroring <see cref="Insights"/>) so <see cref="SiteGenerator"/> can clear/ignore
    /// it. Empty (never null) when the log was empty or predates the enriched fetch; a file with no entry simply
    /// gets a neutral fill (per-file graceful degradation, AC #2). [Story 7.6]</summary>
    public IReadOnlyDictionary<string, CodeFileMetrics> CodeMapMetrics { get; set; }
        = new Dictionary<string, CodeFileMetrics>(StringComparer.Ordinal);

    /// <summary>The full (uncapped) canonical unordered file-pair co-change count map, keyed the same way
    /// <see cref="GitMetrics.BuildFileInsights"/> and <see cref="GitMetrics.ParseNumstatLog"/>'s own internal
    /// coupling tally key their pairs (ordinal-ordered <c>(A,B)</c> with <c>A &lt;= B</c>) — this is the SAME
    /// dictionary already built once inside <see cref="GitMetrics.BuildFileInsights"/> for the per-file "coupled
    /// files" view, simply returned instead of discarded, so callers can ask "are these two arbitrary files
    /// co-changed?" without a second git call or a second commit scan. Look up via
    /// <see cref="GitMetrics.CoChangeCount"/> (it canonicalizes the pair order for you). Empty (never null) when
    /// deep-git found no non-bulk multi-file commits. [reference-graph epic grouping + relationships]</summary>
    public IReadOnlyDictionary<(string FileA, string FileB), int> CoChangePairs { get; init; }
        = new Dictionary<(string, string), int>();

    /// <summary>The whole-repo coupling view expressed DIRECTIONALLY (Story 24.1): the same top-N surface as
    /// <see cref="Coupling"/>, but each row is a <see cref="DirectedCouple"/> carrying confidence / support / lift /
    /// cross-boundary rather than an unordered pair and a raw shared-commit count — so the hub's ranked table can
    /// say "when A changes, B usually changes too" and rank by how strong that pull actually is, instead of letting
    /// an always-churning file top the list simply by appearing everywhere.
    /// <para>Computed once from the SAME single numstat parse that produces <see cref="Coupling"/> (the co-change
    /// pair tally, the per-file change counts, and <see cref="AnalyzedCommits"/> are all already in hand) — no
    /// second git call, no second commit scan — and surfaced here so every consuming view reads one shared
    /// computation (AC #2) instead of re-deriving the metric per surface. Ranked by confidence desc, then support
    /// desc, then ordinal path (owner decision Q4). Empty (never null) when deep-git found no qualifying couples.
    /// <see cref="Coupling"/> is retained alongside it: the node-link graph still encodes shared commits on
    /// undirected edges, and a directed list is the wrong input for that. [Story 24.1]</para></summary>
    public IReadOnlyList<DirectedCouple> DirectedCoupling { get; init; } = Array.Empty<DirectedCouple>();

    /// <summary>The minimum-support floor actually used to build this pulse's coupling views — recorded rather than
    /// assumed, so a consumer filtering <see cref="CoChangePairs"/> (which is deliberately stored UNFILTERED, being
    /// a general-purpose lookup map) applies the same threshold these views did instead of hardcoding one.
    /// <para>Story 24.1 introduced <see cref="GitMetrics.CouplingMinSupport"/> as "the shared source, not two
    /// literals in two methods", then left <c>SiteGenerator.BuildRelatedRelatedEdges</c> comparing against a bare
    /// <c>2</c> under a comment claiming it mirrored that threshold — true only by coincidence, and silently false
    /// the moment a caller passed a different floor. This property is what makes the claim structural.
    /// [Story 24.1 code review]</para></summary>
    public int MinSupport { get; init; } = GitMetrics.CouplingMinSupport;
}

/// <summary>The per-file git-derived signals a source-code treemap colorizes by (Story 7.6). <paramref name="Changes"/>
/// = commits touching this file (once per commit, mirroring <see cref="GitMetrics.BuildInsights"/>'s
/// once-per-commit-per-file counting); <paramref name="TotalChurn"/> = Σ (added + deleted) across every numstat row
/// (binary rows contribute 0); average change size is <c>TotalChurn / Changes</c> (computed at render, divide-by-zero
/// guarded). <paramref name="FirstDate"/>/<paramref name="LastDate"/> are the oldest/newest commit day within the
/// <b>analyzed window</b> touching this file — NOT true repository creation/modification: the shared fetch is bounded
/// (<c>-n 300</c>), so these are "recency within recent history", matching the AC's deliberate "<b>relative</b>
/// creation date" wording. Either date is null when no parsed record for the file carried a timestamp.
/// <paramref name="AvgCoChanged"/> = the average number of <b>other</b> files touched in the same commits as this file
/// (its typical "blast radius" per change), averaged over the non-bulk commits touching it — a commit whose distinct
/// file set exceeds <see cref="GitMetrics.CouplingFileSetCap"/> is excluded as sweeping noise (matching the coupling
/// view), while a solo commit contributes 0. Null when no non-bulk commit touched the file.
/// <paramref name="Contributors"/> is this file's per-author attribution (commits touching THIS file + their latest
/// such commit's day), ordered commits-desc/name-asc and capped at <see cref="GitMetrics.CodeMapFileContributorCap"/>
/// — the whole-tree analog of <see cref="FileChangeStat.Contributors"/>, computed by the SAME uncapped per-file walk
/// as every other field here (unlike <see cref="GitInsightsData.Files"/>, never top-N-file-truncated). Null (never an
/// empty non-null list) when the file has no git record. <paramref name="TotalContributors"/> is the file's full
/// distinct-author count before that cap, so a truncated list can be disclosed as truncated. [Story 7.6; co-change
/// dimension; Story 7.11 author attribution]</summary>
public sealed record CodeFileMetrics(
    int Changes,
    int TotalChurn,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    double? AvgCoChanged = null,
    IReadOnlyList<FileContributor>? Contributors = null,
    int TotalContributors = 0);

/// <summary>One file's numstat row within a commit. <paramref name="Added"/>/<paramref name="Deleted"/> are
/// null for binary files (git prints <c>-</c> for both counts) — the path still counts as a change. [Story 3.8]</summary>
public sealed record DeepFileChange(string Path, int? Added, int? Deleted);

/// <summary>One commit parsed from the shared deep-git numstat fetch: identity (<paramref name="Hash"/>,
/// <paramref name="Author"/>, <paramref name="Timestamp"/>), message (<paramref name="Subject"/> and the
/// free-text <paramref name="Body"/>, carried so the per-commit detail pages of Story 7.5 can reuse this one
/// fetch), and the commit's touched-file set. <paramref name="Timestamp"/> is null when the record predates
/// the enriched fetch format or the date failed to parse. [Story 3.8]</summary>
public sealed record DeepCommit(
    string Hash,
    string Author,
    DateTime? Timestamp,
    string Subject,
    string Body,
    IReadOnlyList<DeepFileChange> Files);

/// <summary>One person's attribution to a single file — how many commits by this author touched THIS file in
/// the window, and when they last did. Framed per-file to answer "who do I talk to about this file?" — the
/// author appears only in the context of files they worked on, never as a row in a global scoreboard. [Story 3.8]</summary>
public sealed record FileContributor(string Name, int Commits, DateOnly? LastCommitDate);

/// <summary>One file's aggregate change stats for the Git Insights hub: how many commits touched it in the
/// analyzed window, total line churn, its most recent commit (<paramref name="LatestHash"/> /
/// <paramref name="LastChangeDate"/>, for the guarded per-commit link + "latest change" line), and the
/// per-file <paramref name="Contributors"/> that power the file→people drill-down. <paramref name="LinesAdded"/>/
/// <paramref name="LinesDeleted"/> sum only text-file rows (binary rows carry no counts). [Story 3.8]</summary>
/// <paramref name="TotalContributors"/> is the file's full distinct-author count before the top-N take, so
/// the page can disclose when the shown list is truncated. [Review addition 2026-07-09]</summary>
public sealed record FileChangeStat(
    string Path,
    int Changes,
    int LinesAdded,
    int LinesDeleted,
    string LatestHash,
    DateOnly? LastChangeDate,
    IReadOnlyList<FileContributor> Contributors,
    int TotalContributors);

/// <summary>One commit that touched a file, for that file's bounded "change history" list (Story 7.4). The
/// honest, bounded reading of "history/blame-style annotations" — recent commits that changed the file (from the
/// shared numstat fetch), never a per-line <c>git blame</c> call. <paramref name="Date"/> is null when the source
/// commit's timestamp failed to parse (the row still renders, dateless). <paramref name="ShortHash"/> is the 7-char
/// abbreviation used to guard the link to the per-commit page (Story 7.5), matched by prefix. [Story 7.4]</summary>
public sealed record CommitTouch(string ShortHash, DateOnly? Date, string Author, string Subject);

/// <summary>The per-file deep-git signals surfaced on a code page's opt-in "Advanced coverage" section (Story 7.4,
/// FR-19): how often the file changed (<paramref name="ChangeCount"/>), file-scoped contributor attribution
/// (<paramref name="Contributors"/> — who has changed THIS file and how many times, never a cross-repo ranking),
/// the files it most often changes alongside (<paramref name="CoupledFiles"/>, from the same co-change pair data
/// the hub's coupling uses), and a bounded newest-first change history (<paramref name="History"/>). All lists are
/// capped; every field is derived from the ONE shared <c>--deep-git</c> numstat fetch — no extra git call.
/// <paramref name="TotalContributors"/> is the file's full distinct-author count before the top-N take (mirrors
/// <see cref="FileChangeStat.TotalContributors"/>), so the page can disclose when the shown list is truncated
/// instead of silently rendering a partial list as if it were complete. [Story 7.4; review addition 2026-07-13]</summary>
public sealed record FileInsight(
    int ChangeCount,
    IReadOnlyList<(string Author, int Commits)> Contributors,
    IReadOnlyList<CoupledFile> CoupledFiles,
    IReadOnlyList<CommitTouch> History,
    int TotalContributors);

/// <summary>One entry in a focal file's "changes with" list, expressed as DIRECTIONAL coupling strength rather than
/// an unnormalized symmetric tally (Story 24.1, AC #1). <paramref name="Path"/> is the OTHER file;
/// every metric here is read from the focal file's point of view, so the same pair yields different numbers in each
/// file's list.
/// <list type="bullet">
/// <item><description><paramref name="Support"/> — the shared-commit count (the old <c>CoChanges</c>): how many
/// commits changed both files. Filtered by <see cref="GitMetrics.CouplingMinSupport"/> upstream so a single
/// coincidental co-commit never reads as a relationship.</description></item>
/// <item><description><paramref name="Confidence"/> — <c>Support / ChangeCount[focal]</c>, in <c>[0,1]</c>:
/// roughly "when I touch this file, I touch <paramref name="Path"/> this often". ASYMMETRIC — a rarely-changed file
/// can be 100% confident about a churning one while the reverse is 5% — which is the whole point: a raw shared count
/// makes always-churning files look coupled to everything.
/// <para><b>Read the two halves honestly.</b> The numerator and denominator are drawn from DIFFERENT commit
/// populations, by design: <paramref name="Support"/> is tallied only over commits touching between 2 and
/// <see cref="GitMetrics.CouplingFileSetCap"/> files, while <c>ChangeCount</c> counts EVERY commit touching the focal
/// file. A file dragged through bulk/vendored/import sweeps therefore reads LESS confident than the plain-English
/// sentence above implies — a lockfile pulled through fifty 200-file vendor commits can report ~17% about its real
/// partner. That is deliberate: a 200-file sweep is not evidence that two of those files belong together, but it IS
/// evidence that the file churns, and the metric declines to reward churn. Owner decision, Story 24.1 code review:
/// keep the formula, state the skew.</para></description></item>
/// <item><description><paramref name="Lift"/> — <c>Confidence ÷ (ChangeCount[Path] ÷ analyzed commits)</c>: how much
/// the pairing beats <paramref name="Path"/>'s own base rate. Above 1 means genuinely more together than chance; a
/// file touched in every commit has a base rate near 1 and self-demotes. NULL (never <c>NaN</c>/<c>Infinity</c>)
/// when the denominator is 0 — render it as "—" or omit it, never as a number.</description></item>
/// <item><description><paramref name="CrossBoundary"/> — <see cref="GitMetrics.IsCrossBoundary"/>, computed once
/// here so every downstream surface reads the SAME flag (AC #2) instead of re-deriving it per view.</description></item>
/// <item><description><paramref name="Kind"/> — the preserved Code-vs-Process lens
/// (<see cref="GitMetrics.ClassifyCoupling"/>, Story 10.6). Orthogonal to
/// <paramref name="CrossBoundary"/>: a pair can be both.</description></item>
/// </list>
/// [Story 24.1; replaces Story 7.4's <c>(Path, CoChanges)</c> tuple]</summary>
public sealed record CoupledFile(
    string Path,
    int Support,
    double Confidence,
    double? Lift,
    bool CrossBoundary,
    GitMetrics.CouplingKind Kind);

/// <summary>One DIRECTED edge of the whole-repo coupling view behind the Git Insights hub (Story 24.1, AC #1/#3):
/// the same metric spine as <see cref="CoupledFile"/>, but carrying its own <paramref name="FromPath"/> so the hub's
/// ranked table can read as "when <paramref name="FromPath"/> changes, <paramref name="ToPath"/> usually changes
/// too" instead of an unordered pair. Both directions of a pair are emitted (they carry different confidence), then
/// ranked together — so a strongly one-way relationship surfaces on its strong side rather than being averaged away.
/// Computed ONCE in <see cref="GitMetrics.ParseNumstatLog"/> from the already-parsed co-change pairs, per-file change
/// counts, and analyzed-commit total — no second git call, no second commit scan — and surfaced on
/// <see cref="DeepGitPulse.DirectedCoupling"/> so every view reuses it. [Story 24.1]</summary>
public sealed record DirectedCouple(
    string FromPath,
    string ToPath,
    int Support,
    double Confidence,
    double? Lift,
    bool CrossBoundary,
    GitMetrics.CouplingKind Kind);

/// <summary>One UNORDERED co-changed pair of the whole-repo coupling view — the population behind
/// <see cref="DeepGitPulse.Coupling"/> and the hub's coupling graph, ranked by <paramref name="CoChanges"/> (shared
/// commits) rather than by confidence, which is what keeps the graph a stable "what moves together most" picture
/// while <see cref="DeepGitPulse.DirectedCoupling"/> answers the sharper directional question.
/// <para>This replaced a bare <c>(FileA, FileB, CoChanges)</c> tuple so the pair can CARRY its classifications
/// instead of every view re-deriving them. AC #2 requires cross-boundary to be "available to every downstream
/// surface (list and graphs) as a shared property, not recomputed per view" — with a 3-tuple the flag was not
/// merely unrendered on the graph, it was structurally unreachable there, and the hub was separately calling
/// <see cref="GitMetrics.ClassifyCoupling"/> again per render to find its process pairs. Both flags are now
/// computed once, beside the identical computation feeding <see cref="DirectedCouple"/>, so the two surfaces can
/// never disagree about whether a given pair crosses a boundary. [Story 24.1; widened by its code review]</para>
/// <para>Deconstructs to <c>(FileA, FileB, CoChanges)</c> so the pair still reads as a triple at call sites that
/// only want the counts.</para></summary>
public sealed record CoupledPair(
    string FileA,
    string FileB,
    int CoChanges,
    bool CrossBoundary,
    GitMetrics.CouplingKind Kind)
{
    public void Deconstruct(out string fileA, out string fileB, out int coChanges)
    {
        fileA = FileA;
        fileB = FileB;
        coChanges = CoChanges;
    }
}

/// <summary>The aggregate views behind the Git Insights hub page (FR-10), all derived from the one shared
/// bounded numstat fetch: per-file change frequency + churn + the file's contributors (the master-detail
/// "who works on this file" drill-down), and the per-day activity series for the analyzed window
/// (<paramref name="CommitCount"/> commits, <paramref name="ContributorCount"/> distinct authors — headline
/// context only, never a ranked people list). <paramref name="CommitCount"/> is always equal to
/// <paramref name="Activity"/>'s summed counts by construction (a commit whose date failed to parse is excluded
/// from both, rather than inflating one total without the other). <paramref name="TotalFilesTouched"/> is the
/// full distinct-file count before the top-N take, so the page can disclose when <see cref="Files"/> is
/// truncated. [Story 3.8]</summary>
public sealed record GitInsightsData(
    IReadOnlyList<FileChangeStat> Files,
    IReadOnlyList<(DateOnly Day, int Count)> Activity,
    int CommitCount,
    int ContributorCount,
    int TotalFilesTouched);

/// <summary>Shells out to git for a handful of read-only stats. Never throws and never blocks a save —
/// any failure (git missing, not a repo, slow process) simply yields a null pulse, which callers treat
/// as "no git data available" rather than an error.</summary>
public static class GitMetrics
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>Per-commit file-set cap for coupling: coupling is O(files²) per commit, so one bulk-import,
    /// merge, or vendored-drop commit touching thousands of files would explode the pair count. Commits whose
    /// file set exceeds this are skipped when building coupling pairs (they are almost never meaningful
    /// co-change signal) — they still count toward hotspot frequency. [Story 3.2 Subtask 2.5]</summary>
    private const int CouplingFileSetCap = 50;

    /// <summary>Minimum shared-commit count (support) for a co-change pair to count as coupling at all (Story 24.1,
    /// AC #1). Two files that happened to land in ONE commit together are coincidence, not a relationship — and
    /// confidence alone can't tell the difference (a file changed once, alongside one other file, scores a
    /// meaningless 100%). The floor is what makes confidence trustworthy.
    /// <para>This is the SHARED source both the hub's directed view and each file's "changes with" list apply, for
    /// the same reason <see cref="CouplingFileSetCap"/> is shared: the per-file list and the hub must agree about
    /// what counts as a couple, and two literals in two methods is how they silently stop agreeing. Public so
    /// callers/tests can state the threshold instead of restating the number. "Configurable" here means a named
    /// parameter with a sensible default (owner decision Q3) — <see cref="BuildFileInsights"/> and
    /// <see cref="ParseNumstatLog"/> both take it as an argument — not a new user-facing CLI flag.</para></summary>
    public const int CouplingMinSupport = 2;

    /// <summary>Extensions that are process signal for <b>coupling</b> classification only (Story 10.6, AC1):
    /// config/status/lockfile formats plus stylesheet extensions — the live symptom class ("sprint-status.yaml
    /// changes together with specscribe.css" reads as a code dependency when it is really committing-habit).
    /// Marking a path process here never demotes it elsewhere (code map, code pages, language treatment) — this
    /// is a coupling-only lens. Pattern/extension-only, per repo, never a SpecScribe path literal (NFR8).</summary>
    private static readonly HashSet<string> ProcessExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".yml", ".yaml", ".json", ".toml", ".lock", ".css", ".scss", ".less",
    };

    /// <summary>Directory segments that mark everything beneath them as process/build-output for coupling
    /// classification (Story 10.6, AC1) — matched anywhere in the path, not just at the root, so a nested
    /// <c>packages/app/dist/bundle.js</c> still classifies as process.</summary>
    private static readonly HashSet<string> ProcessDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "dist", "node_modules",
    };

    /// <summary>Lockfile basenames whose extension alone wouldn't mark them process (Story 10.6, AC1) — e.g.
    /// <c>go.sum</c> carries no recognized process extension. Most language lockfiles (<c>package-lock.json</c>,
    /// <c>yarn.lock</c>, <c>Cargo.lock</c>, …) already match via <see cref="ProcessExtensions"/>; this list only
    /// covers the exceptions. Framework-neutral (NFR8): common cross-ecosystem names, never a SpecScribe literal.</summary>
    private static readonly HashSet<string> ProcessBasenames = new(StringComparer.OrdinalIgnoreCase)
    {
        "go.sum",
    };

    /// <summary>Whether coupled-with kind (Story 10.6, AC1): the file's own git history/dependency role, not
    /// whether the pair it's part of hides a real code dependency.</summary>
    public enum CouplingKind
    {
        /// <summary>Application/library source — the default when a path matches no process pattern (ambiguous
        /// paths classify as code: a false negative here is cheaper than hiding a real dependency).</summary>
        Code,

        /// <summary>Config, status, lockfile, build-output, or stylesheet — the routine-upkeep class that tends
        /// to get co-committed with unrelated source changes rather than reflecting a code dependency.</summary>
        Process,
    }

    /// <summary>True when <paramref name="path"/> is process signal for <b>coupling</b> classification (Story
    /// 10.6, AC1): a directory segment in <see cref="ProcessDirNames"/>, a basename in
    /// <see cref="ProcessBasenames"/>, or an extension in <see cref="ProcessExtensions"/> (stylesheets included —
    /// process for coupling purposes only, never elsewhere in the portal). Pattern/extension-only, so it
    /// generalizes across repositories without any SpecScribe-specific literal (NFR8). Pure and repo-free.</summary>
    public static bool IsProcessPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;

        if (segments.Take(segments.Length - 1).Any(ProcessDirNames.Contains)) return true;

        var fileName = segments[^1];
        if (ProcessBasenames.Contains(fileName)) return true;

        var ext = Path.GetExtension(fileName);
        return ext.Length > 0 && ProcessExtensions.Contains(ext);
    }

    /// <summary>Classifies a coupled file pair for the Deep Analytics graph/table (Story 10.6, AC1): a pair is
    /// <see cref="CouplingKind.Process"/> when EITHER path is process (<see cref="IsProcessPath"/>) — one process
    /// file co-committed with a code file is still routine-upkeep coupling, not a code dependency. Two code files
    /// stay <see cref="CouplingKind.Code"/>. Pure and repo-free.</summary>
    public static CouplingKind ClassifyCoupling(string pathA, string pathB) =>
        IsProcessPath(pathA) || IsProcessPath(pathB) ? CouplingKind.Process : CouplingKind.Code;

    /// <summary>The module/boundary a path belongs to for cross-boundary coupling: its FIRST path segment (the
    /// top-level directory), or the empty string for a root-level file. Divergence below that segment is still the
    /// same boundary — the top-level directory is the coarsest structural unit every repository has, and the one a
    /// "these two modules move together" signal is about. Null/empty/separator-only paths yield null, which
    /// <see cref="IsCrossBoundary"/> reads as "unknowable" rather than as a boundary of its own. [Story 24.1]</summary>
    private static string? BoundaryOf(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // 0 segments = nothing but separators — unknowable. 1 segment = a root-level file, whose boundary is the
        // repository root itself (shared with every other root-level file, distinct from any nested module).
        return segments.Length == 0 ? null : segments.Length == 1 ? string.Empty : segments[0];
    }

    /// <summary>True when a coupled pair crosses a module boundary — "surprising" coupling (Story 24.1, AC #2): the
    /// two files live under DIFFERENT top-level directories, so their co-change is an architectural signal (a
    /// dependency that spans modules) rather than the unremarkable case of two files in the same area moving
    /// together. Root-level files share the repository-root boundary with each other and are cross-boundary against
    /// anything nested (owner decision Q2).
    /// <para>Computed ONCE and carried as a shared property on <see cref="CoupledFile.CrossBoundary"/> /
    /// <see cref="DirectedCouple.CrossBoundary"/> so no downstream view re-derives it divergently. Orthogonal to and
    /// layered on top of <see cref="ClassifyCoupling"/>'s Code-vs-Process lens — a pair can be both. Pure, repo-free
    /// (no SpecScribe path literals, NFR8), symmetric, deterministic, and never throws: an empty or unknowable path
    /// degrades to <c>false</c>, because asserting an architectural smell on a path we cannot read is worse than
    /// staying quiet.</para></summary>
    public static bool IsCrossBoundary(string pathA, string pathB)
    {
        var a = BoundaryOf(pathA);
        var b = BoundaryOf(pathB);
        if (a is null || b is null) return false;
        return !string.Equals(a, b, StringComparison.Ordinal);
    }

    public static GitPulse? TryCompute(string repoRoot)
    {
        try
        {
            var countText = RunGit(repoRoot, "rev-list --count HEAD");
            if (countText is null || !int.TryParse(countText.Trim(), out var totalCommits) || totalCommits <= 0)
            {
                return null;
            }

            // One log call feeds both the daily counts and the per-day commit lists, tab-separated so
            // the parse never has to guess where a free-text subject begins. %ad carries date + time
            // (author-local) so the per-day pages can show when each commit landed. The date format uses a
            // 'T' separator, not a space: RunGit passes a single argument string that git tokenizes on
            // whitespace, so a space inside --date=format:… would split it into two broken arguments.
            var logText = RunGit(repoRoot, "log --pretty=format:%h%x09%ad%x09%an%x09%s --date=format:%Y-%m-%dT%H:%M");
            if (logText is null) return null;

            var (series, commitsByDay) = ParseLog(logText);
            if (series.Count == 0) return null;

            var today = DateOnly.FromDateTime(DateTime.Now);

            // A second, bounded git call for the "top changed files" signal. --name-only prints one path per
            // commit's touched file; the empty --pretty=format: suppresses the commit header lines so only
            // paths (and blank inter-commit separators) come back. -M collapses renames/moves onto the new
            // path (same ResolveRenamedPath treatment as the deep numstat path). -n 200 caps the window so
            // this never repeats the uncapped-history timeout risk deferred-work.md flagged for the heatmap
            // log. If it fails, degrade this one signal to an empty list rather than nulling the whole pulse
            // (AD-4).
            var nameOnlyText = RunGit(repoRoot, "log -M --name-only --pretty=format: -n 200");
            var topChangedFiles = nameOnlyText is null
                ? Array.Empty<(string, int)>()
                : ParseChangedFiles(nameOnlyText);

            return new GitPulse(
                TotalCommits: totalCommits,
                ActiveDays: series.Count,
                FirstCommitDate: series[0].Day,
                LastCommitDate: series[^1].Day,
                DailySeries: series,
                CommitsByDay: commitsByDay,
                LastCommitTimestamp: LastCommitTimestamp(series, commitsByDay),
                Last30DayCommitCount: CountCommitsInLastDays(series, today, 30),
                TopChangedFiles: topChangedFiles);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses `git log --pretty=format:%h%x09%ad%x09%an%x09%s --date=format:%Y-%m-%dT%H:%M`
    /// output into the ascending daily commit series plus per-day commit details (hash, subject, author,
    /// time). Pure so the format contract is unit-testable without a repo; malformed lines are skipped
    /// rather than failing the whole pulse.</summary>
    public static (IReadOnlyList<(DateOnly Day, int Count)> Series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> CommitsByDay) ParseLog(string logText)
    {
        var byDay = new Dictionary<DateOnly, List<CommitInfo>>();
        foreach (var line in logText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // hash \t "yyyy-MM-dd HH:mm" \t author \t subject — cap at 4 so a tab inside the subject survives.
            var parts = line.Split('\t', 4);
            if (parts.Length < 4) continue;
            var hash = parts[0].Trim();
            // Exact invariant parse: git emits an ISO date, and a culture-sensitive parse would reinterpret
            // it under non-Gregorian default calendars (th-TH, fa-IR), corrupting every date.
            if (hash.Length == 0 || !DateTime.TryParseExact(
                    parts[1].Trim(), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
            {
                continue;
            }
            var day = DateOnly.FromDateTime(stamp);

            if (!byDay.TryGetValue(day, out var commits))
            {
                byDay[day] = commits = new List<CommitInfo>();
            }
            var author = parts[2].Trim();
            var subject = parts[3].Trim();
            commits.Add(new CommitInfo(
                hash,
                subject.Length == 0 ? "(no subject)" : subject,
                author.Length == 0 ? "Unknown" : author,
                // 24-hour time via the single PortalDates token (Story 10.4); same "HH:mm" shape LastCommitTimestamp
                // parses back. Author-local (git's authored offset) — never converted.
                PortalDates.TimeOfDay(stamp)));
        }

        var series = byDay
            .OrderBy(kv => kv.Key)
            .Select(kv => (Day: kv.Key, Count: kv.Value.Count))
            .ToList();
        var commitsByDay = byDay.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<CommitInfo>)kv.Value);
        return (series, commitsByDay);
    }

    /// <summary>The exact timestamp of the most recent commit, reconstructed from data <see cref="ParseLog"/>
    /// already produced — no extra git call. The last day in the ascending series is the most recent; among
    /// that day's commits the latest parseable HH:mm wins (order-independent, so clock-skew / merge list
    /// order cannot pick a stale first entry). Falls back to midnight on that day if no time can be
    /// recovered. Invariant time parse for the same non-Gregorian-calendar reasons ParseLog is invariant.
    /// Public so the max-time contract is unit-testable without a repo.</summary>
    public static DateTime LastCommitTimestamp(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> commitsByDay)
    {
        if (series.Count == 0)
        {
            return DateTime.MinValue;
        }

        var lastDay = series[^1].Day;
        if (!commitsByDay.TryGetValue(lastDay, out var commits) || commits.Count == 0)
        {
            return lastDay.ToDateTime(TimeOnly.MinValue);
        }

        TimeOnly? latest = null;
        foreach (var commit in commits)
        {
            if (!TimeOnly.TryParseExact(commit.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                continue;
            }
            if (latest is null || time > latest.Value)
            {
                latest = time;
            }
        }

        return lastDay.ToDateTime(latest ?? TimeOnly.MinValue);
    }

    /// <summary>Sums commits in <paramref name="series"/> whose day is within the trailing
    /// <paramref name="days"/> window ending at <paramref name="today"/> (inclusive on both ends): a day
    /// exactly <paramref name="days"/> ago still counts, one older does not. Future-dated commits (clock/
    /// timezone skew) are excluded so they can't inflate the rolling count. Pure so the boundary is
    /// unit-testable without a repo.</summary>
    public static int CountCommitsInLastDays(IReadOnlyList<(DateOnly Day, int Count)> series, DateOnly today, int days)
    {
        var cutoff = today.AddDays(-days);
        return series.Where(s => s.Day >= cutoff && s.Day <= today).Sum(s => s.Count);
    }

    /// <summary>Parses `git log -M --name-only --pretty=format:` output — one changed-file path per line, blank
    /// lines separating commits — into the most-changed files, sorted by change count descending (ordinal
    /// path as a stable tie-break) and truncated to <paramref name="top"/>. Production relies on <c>-M</c> so
    /// rename commits already emit only the destination path; arrow/brace forms (name-status/numstat shaped)
    /// are still collapsed via <see cref="ResolveRenamedPath"/> if present. Path keys stay Ordinal (same as
    /// the rest of the git layer). Blank/whitespace lines and stray carriage returns are skipped, so the
    /// parse never throws and never emits phantom entries. Pure, mirroring <see cref="ParseLog"/>, so the
    /// format contract is unit-testable without a repo.</summary>
    public static IReadOnlyList<(string Path, int ChangeCount)> ParseChangedFiles(string log, int top = 5)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var raw = line.Trim();
            if (raw.Length == 0) continue;
            var path = ResolveRenamedPath(raw).Trim();
            if (path.Length == 0) continue;
            counts[path] = counts.GetValueOrDefault(path) + 1;
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(top)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>The opt-in deep-git pass (FR-10). A single bounded <c>git log --numstat</c> call — one shared
    /// git code path reused across the deep-git surfaces — feeds the pure <see cref="ParseNumstatLog"/> parser.
    /// Obeys the same never-throw contract as <see cref="TryCompute"/>: any failure yields <c>null</c>, which
    /// the dashboard treats as "no deep data" and simply omits the panel, never an error. This is a separate
    /// call from <see cref="TryCompute"/>, so a deep failure leaves the baseline <see cref="GitPulse"/> intact
    /// (partial data beats none; AD-4). The "single shared git code path" is scoped to the deep-git family
    /// (this story, 3.8, 7.4, 7.5) — it deliberately does not absorb <see cref="TryCompute"/>'s separate,
    /// always-on, lighter <c>--name-only</c> call (bounded at <c>-n 200</c>, vs this call's <c>-n 300</c> —
    /// the two bounds are independent and may drift; that's expected, not a bug), which stays on its own
    /// bounded window regardless of <c>--deep-git</c> so the FR-10 performance gate never depends on this
    /// heavier fetch. [Story 3.2]</summary>
    /// <param name="coupledCap">Forwarded to <see cref="ParseNumstatLog"/> — see its remarks. The site generator
    /// passes <see cref="RelationshipGraphCoupledCap"/> because the code page's relationship surface is this
    /// list's richest consumer; the default keeps every other caller unchanged. [Story 24.2]</param>
    public static DeepGitPulse? TryComputeDeep(string repoRoot, int coupledCap = FileInsightCoupledCap)
    {
        try
        {
            // Bounded with -n so an uncapped log can't blow the 3s RunGit budget on a mature repo
            // (deferred-work.md flagged this exact scaling trap). --numstat emits one "added\tdeleted\tpath"
            // line per file per commit. The \x01 sentinel marks each commit record's start and \x1f separates
            // its header fields (hash, author, date, subject, body) — free-text-safe delimiters, since bodies
            // can contain blank lines and tabs. A trailing \x1f closes the body so the numstat rows that follow
            // can never be mistaken for message text. The date format uses a 'T' separator (not a space) for
            // the same argument-tokenizing reason TryCompute's does. --numstat (not bare --name-only) plus the
            // author/date/subject/body fields make this THE one shared fetch feeding the deep panel (3.2), the
            // Git Insights hub (3.8), and the per-file/per-commit detail pages (7.4/7.5). [Story 3.2 re-plan; Story 3.8]
            var logText = RunGit(repoRoot,
                "log --numstat --date=format:%Y-%m-%dT%H:%M --pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f -n 300");
            if (logText is null) return null;

            return ParseNumstatLog(logText, coupledCap: coupledCap);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses the shared deep-git numstat log (see <see cref="ParseNumstatRecords"/> for the record
    /// format — the enriched sentinel shape and the minimal <c>%x01%H</c> shape are both accepted) into the
    /// deep-git signals.
    /// <para><b>Hotspots</b> = per-path change frequency (commits touching the file), sorted desc with an
    /// ordinal path tie-break, top <paramref name="topHotspots"/>. <b>Coupling</b> = for each commit's file
    /// set, every unordered co-changed pair, kept only at <c>CoChanges &gt;= <paramref name="minSupport"/></c>
    /// (default <see cref="CouplingMinSupport"/>), sorted desc, top
    /// <paramref name="topCoupling"/>. Commits touching more than <see cref="CouplingFileSetCap"/> files are
    /// skipped for coupling (bulk imports). The returned pulse also carries the Git Insights hub aggregates
    /// (<see cref="DeepGitPulse.Insights"/>) computed from the same parsed records. [Story 3.8]</para>
    /// Pure and repo-free (mirrors <see cref="ParseLog"/>) so the format contract is unit-testable; malformed
    /// lines are skipped rather than throwing.</summary>
    /// <param name="coupledCap">How many coupled files each <see cref="FileInsight.CoupledFiles"/> list keeps.
    /// Threaded here exactly as Story 24.1 threaded <paramref name="minSupport"/> — one optional parameter with a
    /// default, no new CLI flag — so the code page's relationship surface can ask for
    /// <see cref="RelationshipGraphCoupledCap"/> without a second git call or a second commit scan. [Story 24.2]</param>
    public static DeepGitPulse ParseNumstatLog(
        string logText, int topHotspots = 10, int topCoupling = 10, int minSupport = CouplingMinSupport,
        int coupledCap = FileInsightCoupledCap)
    {
        var changeCounts = new Dictionary<string, int>();
        // Canonicalized (ordinal-ordered) file pair -> number of commits changing both together.
        var pairCounts = new Dictionary<(string, string), int>();
        // Refactored (Story 3.8) to ride the shared record parse: the raw text is parsed ONCE into
        // per-commit records, and hotspots/coupling are computed as one view over them — the Git Insights
        // hub aggregates are a second view over the same records (one fetch, one parse, several views).
        var commits = ParseNumstatRecords(logText);

        foreach (var commit in commits)
        {
            // A commit's file SET: the same resolved path listed twice within one commit counts once.
            var current = new HashSet<string>(commit.Files.Select(f => f.Path), StringComparer.Ordinal);
            if (current.Count == 0) continue;

            foreach (var path in current)
            {
                changeCounts[path] = changeCounts.GetValueOrDefault(path) + 1;
            }

            // Guard the O(n²) pair cost: a bulk/merge/vendored commit is not a meaningful co-change signal.
            if (current.Count >= 2 && current.Count <= CouplingFileSetCap)
            {
                var files = current.ToArray();
                for (var i = 0; i < files.Length; i++)
                {
                    for (var j = i + 1; j < files.Length; j++)
                    {
                        var a = files[i];
                        var b = files[j];
                        // Canonical unordered key: (A,B) and (B,A) map to the same pair.
                        var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
                        pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
                    }
                }
            }
        }

        var hotspots = changeCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topHotspots)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        var coupling = pairCounts
            .Where(kv => kv.Value >= minSupport)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Item1, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Item2, StringComparer.Ordinal)
            .Take(topCoupling)
            // Classify ONCE, here, beside the identical computation feeding DirectedCoupling — never per view.
            // AC #2 requires cross-boundary to reach every downstream surface as a shared property. [Story 24.1]
            .Select(kv => new CoupledPair(
                kv.Key.Item1,
                kv.Key.Item2,
                kv.Value,
                IsCrossBoundary(kv.Key.Item1, kv.Key.Item2),
                ClassifyCoupling(kv.Key.Item1, kv.Key.Item2)))
            .ToList();

        // Lift's base-rate denominator is NOT the same number as AnalyzedCommits. A record that contributed no
        // numstat rows — every merge commit, since `log --numstat` does not diff merges by default and the fetch
        // carries no --no-merges — is skipped by the accumulation loop above, so it can never raise any file's
        // ChangeCount. Counting it in the denominator understates every base rate and therefore OVERSTATES every
        // lift, whose entire interpretive value is its anchor at 1.0 ("shows up that often anyway"). On this
        // repository 25 of 299 windowed commits are merges, so lift read ~9% high across the board.
        // AnalyzedCommits stays commits.Count: it is the honest size of the window we looked at, which is a
        // different question from "how many commits could have moved this file". [Story 24.1 code review]
        var couplingWindow = commits.Count(c => c.Files.Count > 0);

        var fileInsights = BuildFileInsights(commits, out var coChangePairs, coupledCap: coupledCap, minSupport: minSupport);
        return new DeepGitPulse(hotspots, coupling, AnalyzedCommits: commits.Count)
        {
            Insights = BuildInsights(commits),
            Commits = commits,
            FileInsights = fileInsights,
            CodeMapMetrics = BuildCodeMapMetrics(commits),
            CoChangePairs = coChangePairs,
            DirectedCoupling = BuildDirectedCoupling(pairCounts, changeCounts, couplingWindow, topCoupling, minSupport),
            MinSupport = minSupport,
        };
    }

    /// <summary>Projects the already-tallied co-change pairs into the hub's ranked DIRECTED view (Story 24.1, AC #1/#3)
    /// — the whole-repo analog of <see cref="FileInsight.CoupledFiles"/>, sharing its metric definitions, its
    /// <paramref name="minSupport"/> floor, and its confidence-first ordering so the two surfaces can never disagree
    /// about what counts as a couple or which couples matter most.
    /// <para>Both directions of each qualifying pair are emitted and then ranked together, because they usually carry
    /// different confidence: a helper file dragged along by a hub file is a real finding on one side and noise on the
    /// other, and collapsing THAT to one row would average the finding away. The exception is an exact echo — equal
    /// support and equal confidence, hence equal lift — where the two rows differ in nothing the table renders; see
    /// <see cref="IsEcho"/>. The top-N take therefore selects the strongest DIRECTED relationships, which is the
    /// ranking AC #3 asks for — it is not the same population as <see cref="DeepGitPulse.Coupling"/>'s top-N by
    /// shared commits, and the hub's ranking caption says so.</para>
    /// <para><paramref name="minSupport"/> does double duty: it is the floor for ADMITTING a pair at all, and
    /// <c>support &gt; minSupport</c> is the preference for RANKING one into the visible top-N, so bare-floor pairs
    /// cannot crowd out well-evidenced ones. The preference degrades to the full admitted set when nothing clears
    /// it, so a young repository gets a weak panel rather than an empty one.</para>
    /// <para><paramref name="analyzedCommits"/> is the count of commits that contributed files — NOT
    /// <see cref="DeepGitPulse.AnalyzedCommits"/>. See the call site for why the two differ.</para>
    /// <para>Derived entirely from maps the single numstat parse already built (no extra git call, no second commit
    /// scan). Pure and repo-free; empty in, empty out; never throws.</para></summary>
    private static IReadOnlyList<DirectedCouple> BuildDirectedCoupling(
        IReadOnlyDictionary<(string, string), int> pairCounts,
        IReadOnlyDictionary<string, int> changeCounts,
        int analyzedCommits,
        int topCoupling,
        int minSupport)
    {
        var directed = new List<DirectedCouple>();
        foreach (var (key, support) in pairCounts)
        {
            if (support < minSupport) continue;
            var (a, b) = key;
            var crossBoundary = IsCrossBoundary(a, b);
            var kind = ClassifyCoupling(a, b);

            // pairCounts keys are canonicalized ordinal-first, so `a`->`b` is the deterministic survivor when the
            // two directions turn out to be echoes of each other.
            var forward = Make(a, b);
            var reverse = Make(b, a);
            if (forward is not null) directed.Add(forward);
            if (reverse is not null && !(forward is not null && IsEcho(forward, reverse))) directed.Add(reverse);

            DirectedCouple? Make(string from, string to)
            {
                // A file present in a pair was touched, so its change count is >= the pair's support; the guard
                // keeps a malformed/partial map from dividing by zero rather than expressing a real case.
                var fromChanges = changeCounts.GetValueOrDefault(from);
                if (fromChanges <= 0) return null;
                var confidence = (double)support / fromChanges;
                return new DirectedCouple(
                    from, to, support, confidence,
                    Lift(confidence, changeCounts.GetValueOrDefault(to), analyzedCommits),
                    crossBoundary, kind);
            }
        }

        // Ranking (Story 24.1 code review, owner decision). Two problems fixed here, both invisible in a unit
        // fixture and both obvious on a real repository:
        //
        // 1. A pair sitting at EXACTLY the support floor carries the weakest evidence the floor admits — two
        //    commits — yet scores confidence 1.0 whenever neither file has ever changed apart, which is the norm
        //    for files introduced together. Real repositories hold far more such pairs than topCoupling, so
        //    ranking on confidence alone let the ORDINAL PATH tie-break pick the visible window: the panel
        //    degenerated into an alphabetical list of support-floor trivia while genuinely well-evidenced couples
        //    could never appear. Prefer pairs carrying more evidence than the floor demands.
        // 2. The fallback matters as much as the rule. On a young repository, or a narrow window, EVERY pair may
        //    sit at the floor; dropping them all would trade a weak panel for an empty one. So the strict set is
        //    a preference, not a gate.
        var ranked = directed.Where(d => d.Support > minSupport).ToList();
        if (ranked.Count == 0) ranked = directed;

        return ranked
            .OrderByDescending(d => d.Confidence)
            .ThenByDescending(d => d.Support)
            .ThenBy(d => d.FromPath, StringComparer.Ordinal)
            .ThenBy(d => d.ToPath, StringComparer.Ordinal)
            .Take(topCoupling)
            .ToList();
    }

    /// <summary>True when two directions of the same pair would render as byte-identical rows. Lift is
    /// mathematically symmetric (<c>support·N ÷ (changeCount[a]·changeCount[b])</c> is invariant under swapping the
    /// endpoints), and support is shared by construction, so equal confidence — which happens exactly when both
    /// files changed the same number of times, the everyday "these two only ever move together" case — leaves the
    /// two rows differing in nothing the table shows but the order of the path cells. Emitting both was defended as
    /// "the asymmetry is the finding, not a duplicate"; that defence holds only where an asymmetry actually exists,
    /// and not in the population confidence-ranking floats to the top. [Story 24.1 code review]</summary>
    private static bool IsEcho(DirectedCouple x, DirectedCouple y) =>
        x.Support == y.Support && Math.Abs(x.Confidence - y.Confidence) < 1e-9;

    /// <summary>Commit-record boundary sentinel in the shared deep-git fetch (<c>%x01</c>): marks where each
    /// commit's header begins. Field/record sentinels are used (not blank lines or tabs) because subjects and
    /// bodies are free text. [Story 3.8]</summary>
    private const char RecordSentinel = (char)0x01;

    /// <summary>Header-field separator sentinel in the shared deep-git fetch (<c>%x1f</c>): splits hash /
    /// author / date / subject / body; a trailing one closes the body so numstat rows can't be mistaken for
    /// message text. [Story 3.8]</summary>
    private const char FieldSentinel = (char)0x1F;

    /// <summary>Parses the shared deep-git fetch
    /// (<c>log --numstat --pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f</c>) into one
    /// <see cref="DeepCommit"/> per commit. Records are split on the <see cref="RecordSentinel"/>; within a
    /// record the <see cref="FieldSentinel"/>s separate hash / author / date / subject / body, and everything
    /// after the closing body sentinel is that commit's numstat rows — so multi-line bodies (even ones that
    /// look like numstat rows) can never bleed into the file set. Also accepts the older minimal
    /// <c>%x01%H</c>-only shape (first line = hash, rest = numstat rows). Pure and repo-free (mirrors
    /// <see cref="ParseLog"/>): malformed lines are skipped, dates parse invariantly (culture-sensitive parses
    /// corrupt dates under non-Gregorian default calendars), and it never throws. [Story 3.8]</summary>
    public static IReadOnlyList<DeepCommit> ParseNumstatRecords(string logText)
    {
        var commits = new List<DeepCommit>();

        foreach (var record in logText.Split(RecordSentinel))
        {
            if (record.Length == 0) continue; // the empty slice before the first sentinel

            string hash;
            var author = string.Empty;
            var subject = string.Empty;
            var body = string.Empty;
            DateTime? stamp = null;
            string numstatBlock;

            var fields = record.Split(FieldSentinel);
            if (fields.Length >= 6)
            {
                // Enriched shape: hash / author / date / subject / body, then the numstat rows.
                hash = fields[0].Trim();
                author = fields[1].Trim();
                if (DateTime.TryParseExact(
                        fields[2].Trim(), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    stamp = parsed;
                }
                subject = fields[3].Trim();
                // A free-text body containing a raw 0x1F byte splits into extra fields here, which would shift
                // the numstat block off a fixed fields[5] and silently drop everything after it. The format
                // string's trailing %x1f always closes the body, so the numstat rows are always the LAST field
                // regardless of how many sentinel-shaped pieces the body itself was split into; rejoin those
                // middle pieces (re-inserting the sentinel) to recover the original body text.
                body = string.Join(FieldSentinel, fields[4..^1]).Trim();
                numstatBlock = fields[^1];
            }
            else
            {
                // Minimal legacy shape (%x01%H only): first line is the hash, the rest are numstat rows.
                var newline = record.IndexOf('\n');
                hash = (newline >= 0 ? record[..newline] : record).Trim();
                numstatBlock = newline >= 0 ? record[(newline + 1)..] : string.Empty;
            }

            if (hash.Length == 0) continue;

            var files = new List<DeepFileChange>();
            foreach (var line in numstatBlock.Split('\n'))
            {
                // A numstat data line: added \t deleted \t path. Cap the split at 3 so a path containing a
                // tab survives intact; skip anything that doesn't have the two leading count columns. Binary
                // files print "-" for both counts — the path is still a change, the counts stay null.
                var parts = line.Split('\t', 3);
                if (parts.Length < 3) continue;
                var filePath = ResolveRenamedPath(parts[2].Trim());
                if (filePath.Length == 0) continue;
                int? added = int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var a) ? a : null;
                int? deleted = int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : null;
                files.Add(new DeepFileChange(filePath, added, deleted));
            }

            commits.Add(new DeepCommit(
                hash,
                author.Length == 0 ? "Unknown" : author,
                stamp,
                subject,
                body,
                files));
        }

        return commits;
    }

    /// <summary>Per-file accumulator: change frequency + churn, the file's newest commit (records arrive in
    /// git log order — newest first — so the first commit seen touching a file is its latest), and per-author
    /// attribution scoped to THIS file. A small mutable class keeps the multi-field read-modify-write in the
    /// hot loop readable. [Story 3.8]</summary>
    private sealed class FileAccum
    {
        public int Changes;
        public int Added;
        public int Deleted;
        public string LatestHash = string.Empty;
        public DateOnly? LastChangeDate;
        // Author -> (commits by that author touching this file, their latest such commit's day).
        public readonly Dictionary<string, (int Commits, DateOnly? LastDate)> Authors = new(StringComparer.Ordinal);
    }

    /// <summary>Aggregates the parsed deep-git records into the Git Insights hub's views (FR-10): per-file
    /// change frequency + line churn + each file's contributor breakdown (top <paramref name="topFiles"/>
    /// files, change-count desc with an ordinal path tie-break — the generation-time ordering IS the no-JS
    /// reading order), and the ascending per-day activity series for the analyzed window. Contributors are
    /// scoped PER FILE (the "who works on this file?" drill-down), never a global ranked people list — the
    /// only global people figure is a distinct-author count for headline context. Pure and repo-free so every
    /// ordering/counting rule is unit-testable; empty input yields empty views, never null. [Story 3.8]</summary>
    public static GitInsightsData BuildInsights(IReadOnlyList<DeepCommit> commits, int topFiles = 50, int topContributorsPerFile = 12)
    {
        var fileStats = new Dictionary<string, FileAccum>(StringComparer.Ordinal);
        var allAuthors = new HashSet<string>(StringComparer.Ordinal);
        var byDay = new Dictionary<DateOnly, int>();

        foreach (var commit in commits)
        {
            allAuthors.Add(commit.Author);
            var day = commit.Timestamp is { } when ? DateOnly.FromDateTime(when) : (DateOnly?)null;
            if (day is { } d) byDay[d] = byDay.GetValueOrDefault(d) + 1;

            var seenInCommit = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in commit.Files)
            {
                if (!fileStats.TryGetValue(file.Path, out var accum))
                {
                    fileStats[file.Path] = accum = new FileAccum();
                }

                // Churn sums every numstat row; change frequency + per-author attribution count once per
                // commit (a file listed twice in one commit is still one change by one author).
                accum.Added += file.Added ?? 0;
                accum.Deleted += file.Deleted ?? 0;
                if (seenInCommit.Add(file.Path))
                {
                    accum.Changes++;
                    if (accum.LatestHash.Length == 0)
                    {
                        // First (newest) commit touching this file — its identity is the file's "latest change".
                        accum.LatestHash = commit.Hash;
                        accum.LastChangeDate = day;
                    }
                    else
                    {
                        // The newest commit's own date failed to parse (day is null) — backfill from the next
                        // (older) commit that does have one, rather than leaving LastChangeDate stuck null.
                        accum.LastChangeDate ??= day;
                    }
                    var author = accum.Authors.GetValueOrDefault(commit.Author);
                    author.Commits++;
                    author.LastDate ??= day; // newest-first: the first date seen for this author+file is latest
                    accum.Authors[commit.Author] = author;
                }
            }
        }

        var files = fileStats
            .OrderByDescending(kv => kv.Value.Changes)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topFiles)
            .Select(kv => new FileChangeStat(
                kv.Key,
                kv.Value.Changes,
                kv.Value.Added,
                kv.Value.Deleted,
                kv.Value.LatestHash,
                kv.Value.LastChangeDate,
                kv.Value.Authors
                    .OrderByDescending(a => a.Value.Commits)
                    .ThenBy(a => a.Key, StringComparer.Ordinal)
                    .Take(topContributorsPerFile)
                    .Select(a => new FileContributor(a.Key, a.Value.Commits, a.Value.LastDate))
                    .ToList(),
                kv.Value.Authors.Count))
            .ToList();

        var activity = byDay
            .OrderBy(kv => kv.Key)
            .Select(kv => (Day: kv.Key, Count: kv.Value))
            .ToList();

        // CommitCount is derived from the same dated commits Activity sums (never commits.Count, which would
        // include any commit whose %ad timestamp failed to parse and was therefore excluded from byDay) — the
        // headline "N commits analyzed" figure can then never disagree with the activity series below it.
        var commitCount = activity.Sum(a => a.Count);

        return new GitInsightsData(files, activity, commitCount, allAuthors.Count, fileStats.Count);
    }

    /// <summary>History rows kept per file — recent commits that touched it, newest-first. Bounded so a
    /// long-lived file's "change history" list stays a scannable summary, not the whole log. [Story 7.4]</summary>
    private const int FileInsightHistoryCap = 15;

    /// <summary>Contributors shown per file (the file-scoped "who has changed this?" attribution). Bounded so a
    /// widely-touched file lists its principal authors, not an unbounded roster. [Story 7.4]</summary>
    private const int FileInsightContributorCap = 8;

    /// <summary>Coupled files shown per file (the files it most often changes alongside). Bounded for the same
    /// reason the hub's coupling is capped — a scannable "changes with" list, not every co-change ever. [Story 7.4]
    /// <para>Left at 8 by Story 24.2 (owner decision D2): it stays the default for any caller that does not ask for
    /// more. The code page's relationship surface asks, via <see cref="RelationshipGraphCoupledCap"/>.</para></summary>
    private const int FileInsightCoupledCap = 8;

    /// <summary>Coupled files carried to the code page's <em>relationship surface</em> — the Story 24.2 ego graph,
    /// its sr-only text twin, and the two index-aligned cross-edge builders, which are three consumers of ONE
    /// population and must move together (ADR 0013 §2: no fact may exist only inside the chart).
    ///
    /// <para><b>Why 20 and not "all".</b> The uncapped one-hop ego neighbourhood on this repository measures 360
    /// nodes / 4,782 edges / 449,346 B — never shippable per code page. Story 24.6 measured top-20 at 21 nodes /
    /// 210 edges / <b>20,253 B</b>, which is what Story 23.1's already-accepted sunburst island costs (20,915 B).
    /// Owner decision D2 took that number.</para>
    ///
    /// <para>This is a <c>Take</c> bound on the already-computed, already-support-floored, already-confidence-sorted
    /// list <see cref="BuildFileInsights"/> produces — never a second git call or a second commit scan.</para>
    /// [Story 24.2 D2]</summary>
    public const int RelationshipGraphCoupledCap = 20;

    /// <summary>Display abbreviation length for a commit's <c>%H</c> hash in a file's change history (git's default
    /// floor). The per-commit page guard (Story 7.5) matches this prefix against the full-hash page map. [Story 7.4]</summary>
    private const int FileInsightShortHashLength = 7;

    /// <summary>Per-file accumulator for <see cref="BuildFileInsights"/>: change frequency, the file's author
    /// attribution (author → commits touching THIS file), and its bounded newest-first history. Mirrors
    /// <see cref="FileAccum"/>'s small-mutable-class shape for a readable hot loop. [Story 7.4]</summary>
    private sealed class FileInsightAccum
    {
        public int ChangeCount;
        public readonly Dictionary<string, int> Contributors = new(StringComparer.Ordinal);
        public readonly List<CommitTouch> History = new();
    }

    /// <summary>Builds the per-file deep-git insight map (Story 7.4) from the SAME parsed records the hotspot/
    /// coupling/hub views consume — one fetch, one parse, several views; no extra git call. Per file:
    /// <b>change count</b> (commits touching it, once per commit), <b>contributors</b> (author → per-file commit
    /// count, file-scoped attribution — never a global ranking), <b>coupled files</b> (derived from the same
    /// unordered co-change pairs the coupling view uses, respecting the <see cref="CouplingFileSetCap"/> bulk-commit
    /// skip so per-file coupling matches the hub), and a <b>bounded newest-first history</b> of the commits that
    /// touched it. Every list is capped (<see cref="FileInsightContributorCap"/>/<see cref="FileInsightCoupledCap"/>/
    /// <see cref="FileInsightHistoryCap"/>). Pure and repo-free (mirrors <see cref="BuildInsights"/>): records arrive
    /// newest-first, malformed input is already dropped upstream, empty input yields an empty map, and it never
    /// throws. [Story 7.4]</summary>
    public static IReadOnlyDictionary<string, FileInsight> BuildFileInsights(
        IReadOnlyList<DeepCommit> commits,
        int historyCap = FileInsightHistoryCap,
        int contributorCap = FileInsightContributorCap,
        int coupledCap = FileInsightCoupledCap,
        int minSupport = CouplingMinSupport)
        => BuildFileInsights(commits, out _, historyCap, contributorCap, coupledCap, minSupport);

    /// <summary>Same as the four-argument overload, but also surfaces the full (uncapped) canonical file-pair
    /// co-change map it computes internally via <paramref name="coChangePairs"/> — the ONE dictionary this method
    /// already builds to derive each file's capped <see cref="FileInsight.CoupledFiles"/> list, just also handed
    /// back instead of discarded. This is how <see cref="DeepGitPulse.CoChangePairs"/> gets populated without a
    /// second git call or a second commit scan. [reference-graph epic grouping + relationships]</summary>
    public static IReadOnlyDictionary<string, FileInsight> BuildFileInsights(
        IReadOnlyList<DeepCommit> commits,
        out IReadOnlyDictionary<(string FileA, string FileB), int> coChangePairs,
        int historyCap = FileInsightHistoryCap,
        int contributorCap = FileInsightContributorCap,
        int coupledCap = FileInsightCoupledCap,
        int minSupport = CouplingMinSupport)
    {
        var accum = new Dictionary<string, FileInsightAccum>(StringComparer.Ordinal);
        // Canonical unordered file pair -> co-change count. Same rule as ParseNumstatLog's coupling so the per-file
        // "changes with" list agrees with the hub, including the bulk-commit skip.
        var pairCounts = new Dictionary<(string, string), int>();

        foreach (var commit in commits)
        {
            // A commit's file SET: the same resolved path listed twice within one commit counts once.
            var fileSet = new HashSet<string>(commit.Files.Select(f => f.Path), StringComparer.Ordinal);
            if (fileSet.Count == 0) continue;

            var day = commit.Timestamp is { } ts ? DateOnly.FromDateTime(ts) : (DateOnly?)null;
            var shortHash = commit.Hash.Length > FileInsightShortHashLength
                ? commit.Hash[..FileInsightShortHashLength]
                : commit.Hash;

            foreach (var path in fileSet)
            {
                if (!accum.TryGetValue(path, out var a))
                {
                    accum[path] = a = new FileInsightAccum();
                }
                a.ChangeCount++;
                a.Contributors[commit.Author] = a.Contributors.GetValueOrDefault(commit.Author) + 1;
                if (a.History.Count < historyCap)
                {
                    // Records are newest-first, so append preserves newest-first up to the cap.
                    a.History.Add(new CommitTouch(shortHash, day, commit.Author, commit.Subject));
                }
            }

            // Guard the O(n²) pair cost exactly as ParseNumstatLog does: a bulk/merge/vendored commit is not a
            // meaningful co-change signal (it still counts toward change frequency above).
            if (fileSet.Count >= 2 && fileSet.Count <= CouplingFileSetCap)
            {
                var files = fileSet.ToArray();
                for (var i = 0; i < files.Length; i++)
                {
                    for (var j = i + 1; j < files.Length; j++)
                    {
                        var x = files[i];
                        var y = files[j];
                        var key = string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
                        pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
                    }
                }
            }
        }

        // Fan each unordered pair out to both members' "changes with" lists (the other file + shared-commit count).
        // The DIRECTIONAL metrics (Story 24.1) are deliberately NOT computed here: a pair's confidence depends on
        // whose list it lands in, and the per-file loop below is where the focal file's own ChangeCount is in hand.
        // Support is filtered here rather than downstream so a below-floor couple never occupies a capped slot.
        var coupledByFile = new Dictionary<string, List<(string Path, int Support)>>(StringComparer.Ordinal);
        foreach (var (key, count) in pairCounts)
        {
            if (count < minSupport) continue;
            var (a, b) = key;
            if (!coupledByFile.TryGetValue(a, out var listA)) coupledByFile[a] = listA = new();
            listA.Add((b, count));
            if (!coupledByFile.TryGetValue(b, out var listB)) coupledByFile[b] = listB = new();
            listB.Add((a, count));
        }

        // Lift's denominator: the other file's base rate = ChangeCount[other] / the commits that could have moved
        // it. Records with no files were skipped by the loop above and can never raise anyone's ChangeCount, so
        // they must be excluded HERE too — counting them understates every base rate and overstates every lift.
        // (This is deliberately NOT DeepGitPulse.AnalyzedCommits, which answers the different question "how big
        // was the window we looked at". The previous comment here claimed the skip was the reason commits.Count
        // was correct; the skip is precisely why it was wrong.) [Story 24.1 code review]
        var analyzedCommits = commits.Count(c => c.Files.Count > 0);

        // Both members of every surviving pair were touched, so both are in accum by construction; the guard is
        // defensive only (0 makes Lift return null rather than dividing by zero).
        int OtherChangeCount(string other) => accum.TryGetValue(other, out var o) ? o.ChangeCount : 0;

        var result = new Dictionary<string, FileInsight>(StringComparer.Ordinal);
        foreach (var (path, a) in accum)
        {
            var contributors = a.Contributors
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(contributorCap)
                .Select(kv => (Author: kv.Key, Commits: kv.Value))
                .ToList();

            // Directional coupling (Story 24.1): confidence is computed from THIS file's change count, so the same
            // pair reads differently in each member's list. a.ChangeCount is >= 1 for every file in accum (it is
            // only created on a touch) and can never be less than a pair's support, so confidence lands in [0,1].
            var coupled = coupledByFile.TryGetValue(path, out var pairs)
                ? pairs
                    .Select(p => new CoupledFile(
                        p.Path,
                        p.Support,
                        Confidence: (double)p.Support / a.ChangeCount,
                        Lift: Lift((double)p.Support / a.ChangeCount, OtherChangeCount(p.Path), analyzedCommits),
                        CrossBoundary: IsCrossBoundary(path, p.Path),
                        Kind: ClassifyCoupling(path, p.Path)))
                    .OrderByDescending(p => p.Confidence)
                    .ThenByDescending(p => p.Support)
                    .ThenBy(p => p.Path, StringComparer.Ordinal)
                    .Take(coupledCap)
                    .ToList()
                : new List<CoupledFile>();

            result[path] = new FileInsight(a.ChangeCount, contributors, coupled, a.History, TotalContributors: a.Contributors.Count);
        }

        coChangePairs = pairCounts;
        return result;
    }

    /// <summary>Looks up an arbitrary file pair's co-change count in a <see cref="DeepGitPulse.CoChangePairs"/> map,
    /// canonicalizing the pair order the same way <see cref="BuildFileInsights"/>/<see cref="ParseNumstatLog"/>
    /// key their own internal tally (ordinal-ordered <c>(A,B)</c> with <c>A &lt;= B</c>) — so callers never need to
    /// know or guess the canonical order themselves. 0 (never throws) when the pair never co-occurred, when either
    /// path is empty, or when the map itself is empty (e.g. no deep-git data). [reference-graph epic grouping +
    /// relationships]</summary>
    public static int CoChangeCount(IReadOnlyDictionary<(string FileA, string FileB), int> pairs, string a, string b)
    {
        if (pairs is null || pairs.Count == 0 || a.Length == 0 || b.Length == 0) return 0;
        var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        return pairs.GetValueOrDefault(key);
    }

    /// <summary>Lift for a directed couple (Story 24.1): <c>confidence ÷ (targetChangeCount ÷ analyzedCommits)</c> —
    /// how much the pairing beats the target file's own base rate of appearing in any commit. Above 1 the two really
    /// do travel together; at 1 the target simply shows up that often anyway, which is exactly how an
    /// always-churning file self-demotes instead of looking coupled to everything.
    /// <para>The ONE place this division happens, so no surface can compute it differently or forget the guard.
    /// Returns null — never <c>NaN</c> or <c>Infinity</c>, which would leak into rendered markup as literal
    /// "NaN"/"∞" — when the base rate is undefined (no analyzed commits, or a target with no recorded changes).
    /// Pure, repo-free, never throws.</para></summary>
    public static double? Lift(double confidence, int targetChangeCount, int analyzedCommits)
    {
        if (targetChangeCount <= 0 || analyzedCommits <= 0) return null;
        var baseRate = (double)targetChangeCount / analyzedCommits;
        return baseRate <= 0 ? null : confidence / baseRate;
    }

    /// <summary>Per-file accumulator for <see cref="BuildCodeMapMetrics"/>: change frequency, total churn, and the
    /// oldest/newest commit day seen for the file. Mirrors <see cref="FileAccum"/>'s small-mutable-class shape for a
    /// readable hot loop. [Story 7.6]</summary>
    private sealed class CodeMapAccum
    {
        public int Changes;
        public int TotalChurn;
        public DateOnly? FirstDate; // oldest day seen (records are newest-first, so the LAST assignment wins — keep overwriting)
        public DateOnly? LastDate;  // newest day seen (records are newest-first, so the FIRST non-null day is latest)
        public long CoChangedTotal;  // Σ over non-bulk commits touching this file of (other files in that commit)
        public int CoChangeCommits;  // count of those non-bulk commits (the co-change average's denominator)
        // Author -> (commits by that author touching this file, their latest such commit's day) — the SAME shape
        // FileAccum.Authors uses, so BuildInsights and this whole-tree walk can never disagree on what "a commit
        // by this author touching this file" means. [Story 7.11]
        public readonly Dictionary<string, (int Commits, DateOnly? LastDate)> Authors = new(StringComparer.Ordinal);
    }

    /// <summary>Per-file contributors kept in <see cref="CodeFileMetrics.Contributors"/> — the whole-tree analog of
    /// <see cref="BuildInsights"/>'s <c>topContributorsPerFile</c> default (12). A file with more distinct authors
    /// than this is vanishingly rare in practice; when it happens, an author ranked below the cap on EVERY file
    /// they've ever touched cannot be found by the individual-author spotlight (Story 7.11 AC #2c) — an accepted,
    /// documented bound rather than an unbounded per-file list. [Story 7.11]</summary>
    public const int CodeMapFileContributorCap = 12;

    /// <summary>Builds the untruncated per-file treemap metric map (Story 7.6) from the SAME parsed records the
    /// hotspot/coupling/hub/per-file views consume — one fetch, one parse, several views; no extra git call. Unlike
    /// <see cref="BuildInsights"/> this is NOT top-N truncated: EVERY file appearing anywhere in the window gets an
    /// entry, so a whole-codebase treemap can colorize each file with history. Per file: <b>Changes</b> (commits
    /// touching it, once per commit — a file listed twice in one commit is one change, mirroring
    /// <see cref="BuildInsights"/>'s <c>seenInCommit</c> guard), <b>TotalChurn</b> (Σ added + deleted across every
    /// numstat row; binary rows contribute 0), the <b>oldest/newest</b> commit day within the window, and the
    /// <b>average co-changed file count</b> (mean number of other files touched in the same non-bulk commits — the
    /// file's typical blast radius; bulk commits above <see cref="CouplingFileSetCap"/> excluded). Records
    /// arrive newest-first, so <c>LastDate</c> is the first non-null day seen and <c>FirstDate</c> is the last
    /// (oldest) day seen (kept overwriting). Pure and repo-free (mirrors <see cref="BuildInsights"/>): empty input
    /// yields an empty map and it never throws. [Story 7.6]</summary>
    public static IReadOnlyDictionary<string, CodeFileMetrics> BuildCodeMapMetrics(IReadOnlyList<DeepCommit> commits)
    {
        var accum = new Dictionary<string, CodeMapAccum>(StringComparer.Ordinal);

        foreach (var commit in commits)
        {
            var day = commit.Timestamp is { } ts ? DateOnly.FromDateTime(ts) : (DateOnly?)null;
            // Once-per-commit-per-file change counting: a file listed twice in one commit is still one change.
            var seenInCommit = new HashSet<string>(StringComparer.Ordinal);

            // Co-change blast radius: distinct files in THIS commit, minus self, credited to each member — but only
            // for non-bulk commits (a sweeping commit above the cap is excluded from BOTH numerator and denominator,
            // matching the coupling view's CouplingFileSetCap discipline). Solo commits (distinct==1) contribute 0.
            var distinctCount = commit.Files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count();
            var coChangeQualifies = distinctCount is > 0 and <= CouplingFileSetCap;

            foreach (var file in commit.Files)
            {
                if (!accum.TryGetValue(file.Path, out var a))
                {
                    accum[file.Path] = a = new CodeMapAccum();
                }

                // Churn sums every numstat row (binary rows contribute 0); change frequency counts once per commit.
                a.TotalChurn += (file.Added ?? 0) + (file.Deleted ?? 0);
                if (seenInCommit.Add(file.Path))
                {
                    a.Changes++;
                    if (coChangeQualifies)
                    {
                        a.CoChangedTotal += distinctCount - 1;
                        a.CoChangeCommits++;
                    }
                    // Author attribution, gated the SAME once-per-commit-per-file way as Changes above (mirrors
                    // FileAccum's Authors update in BuildInsights). [Story 7.11]
                    var author = a.Authors.GetValueOrDefault(commit.Author);
                    author.Commits++;
                    author.LastDate ??= day; // newest-first: the first date seen for this author+file is latest
                    a.Authors[commit.Author] = author;
                }

                // Dates: records are newest-first. The first non-null day seen is the file's latest; keep
                // overwriting FirstDate with each newer-to-older day so it settles on the oldest day in the window.
                if (day is { } d)
                {
                    a.LastDate ??= d;
                    a.FirstDate = d;
                }
            }
        }

        var result = new Dictionary<string, CodeFileMetrics>(StringComparer.Ordinal);
        foreach (var (path, a) in accum)
        {
            double? avgCoChanged = a.CoChangeCommits > 0 ? (double)a.CoChangedTotal / a.CoChangeCommits : null;
            var contributors = a.Authors
                .OrderByDescending(kv => kv.Value.Commits)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(CodeMapFileContributorCap)
                .Select(kv => new FileContributor(kv.Key, kv.Value.Commits, kv.Value.LastDate))
                .ToList();
            result[path] = new CodeFileMetrics(a.Changes, a.TotalChurn, a.FirstDate, a.LastDate, avgCoChanged, contributors, a.Authors.Count);
        }

        return result;
    }

    /// <summary>The bounded, deterministic top-author roster used for the whole-tree ownership sunburst's discrete
    /// top-N palette mode (Story 7.11 AC #2b) — ranked by TOTAL commits across the analyzed window (once per
    /// commit, not per file-touch, so a single sweeping commit doesn't inflate an author's rank), tie-broken by
    /// name for a stable order. Deliberately NOT the same thing as the individual-author spotlight roster (AC #2c),
    /// which is unbounded and comes from the per-file <see cref="CodeFileMetrics.Contributors"/> union instead —
    /// this is only the fixed palette-assignment list, never rendered as a ranked "top contributors" leaderboard
    /// (FR-10; the sunburst spotlight framing stays "where has this person worked", not "who did the most").
    /// Pure and repo-free; empty input yields an empty list. [Story 7.11]</summary>
    public static IReadOnlyList<string> BuildTopAuthors(IReadOnlyList<DeepCommit> commits, int capN = CodeMapFileContributorCap)
    {
        if (commits.Count == 0) return Array.Empty<string>();
        return commits
            .GroupBy(c => c.Author, StringComparer.Ordinal)
            .Select(g => (Author: g.Key, Commits: g.Count()))
            .OrderByDescending(a => a.Commits)
            .ThenBy(a => a.Author, StringComparer.Ordinal)
            .Take(Math.Max(capN, 0))
            .Select(a => a.Author)
            .ToList();
    }

    /// <summary>Resolves a `--numstat` path field to the file's current path, collapsing git's rename/move
    /// display syntax rather than treating it as one literal path. Git renders a rename either as a full
    /// <c>old/path.cs =&gt; new/path.cs</c> swap, or — when old and new share a prefix/suffix — abbreviated as
    /// <c>common/{old.cs =&gt; new.cs}/tail</c>. Both forms are collapsed to the new (post-rename) path so
    /// hotspot/coupling counts track the file's current name instead of embedding the raw arrow text as a
    /// bogus combined "path". Non-rename lines pass through unchanged.</summary>
    private static string ResolveRenamedPath(string rawPath)
    {
        var braceStart = rawPath.IndexOf('{');
        var braceEnd = braceStart >= 0 ? rawPath.IndexOf('}', braceStart) : -1;
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            var inner = rawPath[(braceStart + 1)..braceEnd];
            var braceArrow = inner.IndexOf(" => ", StringComparison.Ordinal);
            if (braceArrow >= 0)
            {
                var prefix = rawPath[..braceStart];
                var suffix = rawPath[(braceEnd + 1)..];
                var newInner = inner[(braceArrow + 4)..];
                return prefix + newInner + suffix;
            }
        }

        var arrow = rawPath.IndexOf(" => ", StringComparison.Ordinal);
        return arrow >= 0 ? rawPath[(arrow + 4)..] : rawPath;
    }

    /// <summary>The <c>origin</c> remote URL, or null when there is no remote / no git (Story 7.7). Uses the same
    /// timeout-guarded, failure-tolerant <see cref="RunGit"/> seam as history reads — a repo without a remote simply
    /// yields no external-source base.</summary>
    public static string? TryGetRemoteUrl(string repoRoot)
    {
        var url = RunGit(repoRoot, "remote get-url origin");
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    /// <summary>The date a file was FIRST committed (its true creation, following renames), or null when the path
    /// has no history / isn't tracked / there's no git — a bounded, per-file, never-throwing git shell-out mirroring
    /// <see cref="TryGetCurrentBranch"/>/<see cref="TryGetRemoteUrl"/>'s single-purpose pattern. Used as the
    /// "first-touch" start of a story's cycle-time (Story 21.2); called at most once per done story with a
    /// resolvable done-date, never in a hot loop. <c>--follow</c> traces the file across renames so a moved story
    /// file still reports its original creation; <c>--diff-filter=A</c> keeps only the add(s), and we take the
    /// EARLIEST (min) parsed date so a delete-then-re-add can't understate the age. Deliberately NOT derived from
    /// <see cref="FileInsight.History"/>, whose newest-first list is capped and would misreport the true first
    /// commit for any file with more touches than the cap. Invariant date parse for the same non-Gregorian-calendar
    /// reasons the rest of this class parses invariantly. [Story 21.2]</summary>
    public static DateOnly? TryGetFirstCommitDate(string repoRoot, string repoRelativePath)
    {
        if (string.IsNullOrWhiteSpace(repoRelativePath)) return null;

        // Quote the pathspec so a path with spaces stays a single argument; --follow requires exactly one path.
        var output = RunGit(repoRoot, $"log --follow --diff-filter=A --format=%ad --date=short -- \"{repoRelativePath}\"");
        if (output is null) return null;

        DateOnly? earliest = null;
        foreach (var line in output.Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0) continue;
            if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (earliest is null || date < earliest.Value) earliest = date;
        }
        return earliest;
    }

    /// <summary>The current branch name, or null in detached-HEAD state (or no git) so the caller can fall back to a
    /// default branch for the external-source base (Story 7.7).</summary>
    public static string? TryGetCurrentBranch(string repoRoot)
    {
        var branch = RunGit(repoRoot, "rev-parse --abbrev-ref HEAD");
        if (string.IsNullOrWhiteSpace(branch)) return null;
        branch = branch.Trim();
        return branch is "HEAD" or "" ? null : branch;
    }

    /// <summary>The remote's default branch (e.g. "main" or "master"), read from the local
    /// <c>refs/remotes/origin/HEAD</c> symref, or null when it isn't set (common on a shallow/single-branch clone)
    /// or there is no git (Story 7.7). A fallback for <see cref="TryGetCurrentBranch"/> in detached-HEAD states, so
    /// the external-source URL doesn't have to guess a hardcoded branch name.</summary>
    public static string? TryGetDefaultBranch(string repoRoot) =>
        BranchNameFromOriginHeadSymref(RunGit(repoRoot, "symbolic-ref refs/remotes/origin/HEAD"));

    /// <summary>Extracts the branch name from a <c>refs/remotes/origin/HEAD</c> symref target, preserving
    /// slashy names like <c>feature/foo</c>. Returns null when empty or not under that origin prefix —
    /// never take the segment after the last <c>/</c> (that collapses slashy branches). [Story 10.4 deferred-debt]</summary>
    public static string? BranchNameFromOriginHeadSymref(string? symref)
    {
        if (string.IsNullOrWhiteSpace(symref)) return null;
        symref = symref.Trim();
        const string prefix = "refs/remotes/origin/";
        if (!symref.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var name = symref[prefix.Length..];
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>Lists the repo's git-TRACKED files (repo-relative, forward-slash), or null when the directory is not
    /// a git repo / git is unavailable — the source-file set the code-map treemap walks (Story 7.6). Reuses the same
    /// timeout-guarded, failure-tolerant <see cref="RunGit"/> seam as the history reads; <c>ls-files</c> already
    /// excludes <c>bin/</c>, <c>obj/</c>, <c>.git/</c>, <c>node_modules/</c>, and everything <c>.gitignore</c> covers
    /// — defining "the codebase" exactly the way git does. <c>core.quotepath=off</c> keeps non-ASCII paths literal
    /// (never octal-escaped). Never throws (RunGit swallows failures → null). [Story 7.6]</summary>
    public static IReadOnlyList<string>? TryListFiles(string repoRoot)
    {
        var output = RunGit(repoRoot, "-c core.quotepath=off ls-files");
        if (output is null) return null;

        var files = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            var path = line.Trim();
            if (path.Length == 0) continue;
            files.Add(PathUtil.NormalizeSlashes(path));
        }
        return files;
    }

    private static string? RunGit(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Commit subjects are free text; without this Windows decodes stdout with the OEM
                // codepage and non-ASCII subjects (accents, CJK, emoji) turn to mojibake.
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return null;
            }

            var output = stdoutTask.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
