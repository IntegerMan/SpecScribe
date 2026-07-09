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
    IReadOnlyList<(string FileA, string FileB, int CoChanges)> Coupling);

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
            // line per file per commit; the \x01 sentinel prefixes each commit's header line so the parser can
            // find commit boundaries unambiguously. --numstat (not bare --name-only) is the shared foundation
            // the Epic-3 git re-plan designates for the downstream hub/detail stories; this story ignores the
            // added/deleted columns and uses only the file-set data. [Story 3.2 re-plan]
            var logText = RunGit(repoRoot, "log --numstat --pretty=format:%x01%H -n 300");
            if (logText is null) return null;

            return ParseNumstatLog(logText);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses `git log --numstat --pretty=format:%x01%H` output into deep-git signals. Each commit is
    /// introduced by a line beginning with the <c>\x01</c> sentinel; the following <c>added\tdeleted\tpath</c>
    /// lines are that commit's touched files (binary files show <c>-\t-\tpath</c> — the path is still taken).
    /// <para><b>Hotspots</b> = per-path change frequency (commits touching the file), sorted desc with an
    /// ordinal path tie-break, top <paramref name="topHotspots"/>. <b>Coupling</b> = for each commit's file
    /// set, every unordered co-changed pair, kept only at <c>CoChanges &gt;= 2</c>, sorted desc, top
    /// <paramref name="topCoupling"/>. Commits touching more than <see cref="CouplingFileSetCap"/> files are
    /// skipped for coupling (bulk imports).</para>
    /// Pure and repo-free (mirrors <see cref="ParseLog"/>) so the format contract is unit-testable; malformed
    /// lines are skipped rather than throwing.</summary>
    public static DeepGitPulse ParseNumstatLog(string logText, int topHotspots = 10, int topCoupling = 10)
    {
        var changeCounts = new Dictionary<string, int>();
        // Canonicalized (ordinal-ordered) file pair -> number of commits changing both together.
        var pairCounts = new Dictionary<(string, string), int>();
        var current = new HashSet<string>(StringComparer.Ordinal);

        void Flush()
        {
            if (current.Count == 0) return;

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

            current.Clear();
        }

        foreach (var line in logText.Split('\n'))
        {
            if (line.Length > 0 && line[0] == '\u0001')
            {
                // New commit boundary — bank the previous commit's file set before starting the next.
                Flush();
                continue;
            }

            // A numstat data line: added \t deleted \t path. Cap the split at 3 so a path containing a tab
            // survives intact; skip anything that doesn't have the two leading count columns.
            var parts = line.Split('\t', 3);
            if (parts.Length < 3) continue;
            var filePath = ResolveRenamedPath(parts[2].Trim());
            if (filePath.Length == 0) continue;
            current.Add(filePath);
        }
        Flush(); // the final commit has no trailing sentinel to trigger its flush.

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

        return new DeepGitPulse(hotspots, coupling);
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
