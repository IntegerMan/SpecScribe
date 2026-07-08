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

/// <summary>Shells out to git for a handful of read-only stats. Never throws and never blocks a save —
/// any failure (git missing, not a repo, slow process) simply yields a null pulse, which callers treat
/// as "no git data available" rather than an error.</summary>
public static class GitMetrics
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

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
