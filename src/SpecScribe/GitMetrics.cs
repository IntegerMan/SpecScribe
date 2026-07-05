using System.Diagnostics;

namespace SpecScribe;

/// <summary>A lightweight snapshot of repo activity, for the dashboard's "project pulse".</summary>
public sealed record GitPulse(
    int TotalCommits,
    int ActiveDays,
    DateOnly FirstCommitDate,
    DateOnly LastCommitDate,
    IReadOnlyList<(DateOnly Day, int Count)> DailySeries);

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

            var logText = RunGit(repoRoot, "log --pretty=format:%ad --date=short");
            if (logText is null) return null;

            var days = logText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => DateOnly.TryParse(l.Trim(), out var d) ? d : (DateOnly?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            if (days.Count == 0) return null;

            var series = days
                .GroupBy(d => d)
                .OrderBy(g => g.Key)
                .Select(g => (Day: g.Key, Count: g.Count()))
                .ToList();

            return new GitPulse(
                TotalCommits: totalCommits,
                ActiveDays: series.Count,
                FirstCommitDate: series[0].Day,
                LastCommitDate: series[^1].Day,
                DailySeries: series);
        }
        catch
        {
            return null;
        }
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
