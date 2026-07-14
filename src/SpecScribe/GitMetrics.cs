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
/// entirely rather than shown empty. [Story 3.2]</summary>
public sealed record DeepGitPulse(
    IReadOnlyList<(string Path, int Changes)> Hotspots,
    IReadOnlyList<(string FileA, string FileB, int CoChanges)> Coupling)
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
/// view), while a solo commit contributes 0. Null when no non-bulk commit touched the file. [Story 7.6; co-change dimension]</summary>
public sealed record CodeFileMetrics(int Changes, int TotalChurn, DateOnly? FirstDate, DateOnly? LastDate, double? AvgCoChanged = null);

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
    IReadOnlyList<(string Path, int CoChanges)> CoupledFiles,
    IReadOnlyList<CommitTouch> History,
    int TotalContributors = 0);

/// <summary>The aggregate views behind the Git Insights hub page (FR-10), all derived from the one shared
/// bounded numstat fetch: per-file change frequency + churn + the file's contributors (the master-detail
/// "who works on this file" drill-down), and the per-day activity series for the analyzed window
/// (<paramref name="CommitCount"/> commits, <paramref name="ContributorCount"/> distinct authors — headline
/// context only, never a ranked people list). <paramref name="TotalFilesTouched"/> is the full distinct-file
/// count before the top-N take, so the page can disclose when <see cref="Files"/> is truncated. [Story 3.8]</summary>
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
            // paths (and blank inter-commit separators) come back. -n 200 caps the window so this never
            // repeats the uncapped-history timeout risk deferred-work.md flagged for the heatmap log. If it
            // fails, degrade this one signal to an empty list rather than nulling the whole pulse (AD-4).
            var nameOnlyText = RunGit(repoRoot, "log --name-only --pretty=format: -n 200");
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
    /// already produced — no extra git call. The last day in the ascending series is the most recent, and its
    /// per-day list is newest-first (see <c>ParseLog</c>'s preserved git order), so its first entry's HH:mm is
    /// the latest commit time. Falls back to midnight on that day if the time can't be recovered. Invariant
    /// time parse for the same non-Gregorian-calendar reasons ParseLog is invariant.</summary>
    private static DateTime LastCommitTimestamp(
        IReadOnlyList<(DateOnly Day, int Count)> series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> commitsByDay)
    {
        var lastDay = series[^1].Day;
        if (commitsByDay.TryGetValue(lastDay, out var commits) && commits.Count > 0 &&
            TimeOnly.TryParseExact(commits[0].Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return lastDay.ToDateTime(time);
        }
        return lastDay.ToDateTime(TimeOnly.MinValue);
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

    /// <summary>Parses `git log --name-only --pretty=format:` output — one changed-file path per line, blank
    /// lines separating commits — into the most-changed files, sorted by change count descending (ordinal
    /// path as a stable tie-break) and truncated to <paramref name="top"/>. Blank/whitespace lines and stray
    /// carriage returns are skipped, so the parse never throws and never emits phantom entries. Pure, mirroring
    /// <see cref="ParseLog"/>, so the format contract is unit-testable without a repo.</summary>
    public static IReadOnlyList<(string Path, int ChangeCount)> ParseChangedFiles(string log, int top = 5)
    {
        var counts = new Dictionary<string, int>();
        foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = line.Trim();
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
    /// (partial data beats none; AD-4). [Story 3.2]</summary>
    public static DeepGitPulse? TryComputeDeep(string repoRoot)
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

            return ParseNumstatLog(logText);
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
    /// set, every unordered co-changed pair, kept only at <c>CoChanges &gt;= 2</c>, sorted desc, top
    /// <paramref name="topCoupling"/>. Commits touching more than <see cref="CouplingFileSetCap"/> files are
    /// skipped for coupling (bulk imports). The returned pulse also carries the Git Insights hub aggregates
    /// (<see cref="DeepGitPulse.Insights"/>) computed from the same parsed records. [Story 3.8]</para>
    /// Pure and repo-free (mirrors <see cref="ParseLog"/>) so the format contract is unit-testable; malformed
    /// lines are skipped rather than throwing.</summary>
    public static DeepGitPulse ParseNumstatLog(string logText, int topHotspots = 10, int topCoupling = 10)
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
            .Where(kv => kv.Value >= 2)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Item1, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Item2, StringComparer.Ordinal)
            .Take(topCoupling)
            .Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value))
            .ToList();

        var fileInsights = BuildFileInsights(commits, out var coChangePairs);
        return new DeepGitPulse(hotspots, coupling)
        {
            Insights = BuildInsights(commits),
            Commits = commits,
            FileInsights = fileInsights,
            CodeMapMetrics = BuildCodeMapMetrics(commits),
            CoChangePairs = coChangePairs,
        };
    }

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
                body = fields[4].Trim();
                numstatBlock = fields[5];
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

        return new GitInsightsData(files, activity, commits.Count, allAuthors.Count, fileStats.Count);
    }

    /// <summary>History rows kept per file — recent commits that touched it, newest-first. Bounded so a
    /// long-lived file's "change history" list stays a scannable summary, not the whole log. [Story 7.4]</summary>
    private const int FileInsightHistoryCap = 15;

    /// <summary>Contributors shown per file (the file-scoped "who has changed this?" attribution). Bounded so a
    /// widely-touched file lists its principal authors, not an unbounded roster. [Story 7.4]</summary>
    private const int FileInsightContributorCap = 8;

    /// <summary>Coupled files shown per file (the files it most often changes alongside). Bounded for the same
    /// reason the hub's coupling is capped — a scannable "changes with" list, not every co-change ever. [Story 7.4]</summary>
    private const int FileInsightCoupledCap = 8;

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
        int coupledCap = FileInsightCoupledCap)
        => BuildFileInsights(commits, out _, historyCap, contributorCap, coupledCap);

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
        int coupledCap = FileInsightCoupledCap)
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
        var coupledByFile = new Dictionary<string, List<(string Path, int CoChanges)>>(StringComparer.Ordinal);
        foreach (var (key, count) in pairCounts)
        {
            var (a, b) = key;
            if (!coupledByFile.TryGetValue(a, out var listA)) coupledByFile[a] = listA = new();
            listA.Add((b, count));
            if (!coupledByFile.TryGetValue(b, out var listB)) coupledByFile[b] = listB = new();
            listB.Add((a, count));
        }

        var result = new Dictionary<string, FileInsight>(StringComparer.Ordinal);
        foreach (var (path, a) in accum)
        {
            var contributors = a.Contributors
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(contributorCap)
                .Select(kv => (Author: kv.Key, Commits: kv.Value))
                .ToList();

            var coupled = coupledByFile.TryGetValue(path, out var pairs)
                ? pairs
                    .OrderByDescending(p => p.CoChanges)
                    .ThenBy(p => p.Path, StringComparer.Ordinal)
                    .Take(coupledCap)
                    .ToList()
                : new List<(string Path, int CoChanges)>();

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
    }

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
            result[path] = new CodeFileMetrics(a.Changes, a.TotalChurn, a.FirstDate, a.LastDate, avgCoChanged);
        }

        return result;
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
    public static string? TryGetDefaultBranch(string repoRoot)
    {
        var symref = RunGit(repoRoot, "symbolic-ref refs/remotes/origin/HEAD");
        if (string.IsNullOrWhiteSpace(symref)) return null;
        symref = symref.Trim();
        var slash = symref.LastIndexOf('/');
        return slash >= 0 && slash < symref.Length - 1 ? symref[(slash + 1)..] : null;
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
