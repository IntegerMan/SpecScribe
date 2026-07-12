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
}

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
                stamp.ToString("HH:mm", CultureInfo.InvariantCulture)));
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

        return new DeepGitPulse(hotspots, coupling) { Insights = BuildInsights(commits), Commits = commits };
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
