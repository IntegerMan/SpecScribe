using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>One commit's headline identity: enough for a "what landed that day" list without a full log.
/// <paramref name="Time"/> is the author-local "HH:mm" of the commit.</summary>
public sealed record CommitInfo(string ShortHash, string Subject, string Author, string Time);

/// <summary>A lightweight snapshot of repo activity, for the dashboard's "project pulse".</summary>
public sealed record GitPulse(
    int TotalCommits,
    int ActiveDays,
    DateOnly FirstCommitDate,
    DateOnly LastCommitDate,
    IReadOnlyList<(DateOnly Day, int Count)> DailySeries,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> CommitsByDay);

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

            return new GitPulse(
                TotalCommits: totalCommits,
                ActiveDays: series.Count,
                FirstCommitDate: series[0].Day,
                LastCommitDate: series[^1].Day,
                DailySeries: series,
                CommitsByDay: commitsByDay);
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
