using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>One commit's headline identity: enough for a "what landed that day" list without a full log.</summary>
public sealed record CommitInfo(string ShortHash, string Subject);

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
            // the parse never has to guess where a free-text subject begins.
            var logText = RunGit(repoRoot, "log --pretty=format:%h%x09%ad%x09%s --date=short");
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

    /// <summary>Parses `git log --pretty=format:%h%x09%ad%x09%s --date=short` output into the ascending
    /// daily commit series plus per-day commit details. Pure so the format contract is unit-testable
    /// without a repo; malformed lines are skipped rather than failing the whole pulse.</summary>
    public static (IReadOnlyList<(DateOnly Day, int Count)> Series,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> CommitsByDay) ParseLog(string logText)
    {
        var byDay = new Dictionary<DateOnly, List<CommitInfo>>();
        foreach (var line in logText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length < 3) continue;
            var hash = parts[0].Trim();
            // Exact invariant parse: git emits ISO yyyy-MM-dd, and a culture-sensitive parse would
            // reinterpret it under non-Gregorian default calendars (th-TH, fa-IR), corrupting every date.
            if (hash.Length == 0 || !DateOnly.TryParseExact(
                    parts[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }

            if (!byDay.TryGetValue(day, out var commits))
            {
                byDay[day] = commits = new List<CommitInfo>();
            }
            var subject = parts[2].Trim();
            commits.Add(new CommitInfo(hash, subject.Length == 0 ? "(no subject)" : subject));
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
