using System.Diagnostics;

namespace SpecScribe.Tests;

/// <summary>Hermetic coverage for Story 21.2's <see cref="GitMetrics.TryGetFirstCommitDate"/>: it returns the
/// EARLIEST commit date of a file (following renames) and degrades to null — never throws — for a nonexistent path
/// or a non-repo directory. Uses its OWN throwaway git repo with pinned author dates so the assertion is stable and
/// independent of this repo's live history.</summary>
public class GitMetricsFirstCommitDateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "specscribe-firstcommit-" + Guid.NewGuid().ToString("N"));

    public GitMetricsFirstCommitDateTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void TryGetFirstCommitDate_ReturnsEarliestDate_ForFileWithMultipleCommits()
    {
        Assert.True(TryCreateHistory(),
            "git CLI unavailable on this host — cannot exercise TryGetFirstCommitDate; install git rather than silently skipping this test");

        var first = GitMetrics.TryGetFirstCommitDate(_root, "story.md");

        // Two commits (add on the 5th, edit on the 9th) — the earliest (creation) date wins.
        Assert.Equal(new DateOnly(2026, 1, 5), first);
    }

    [Fact]
    public void TryGetFirstCommitDate_ReturnsNull_ForNonexistentPath()
    {
        Assert.True(TryCreateHistory(),
            "git CLI unavailable on this host — cannot exercise TryGetFirstCommitDate; install git rather than silently skipping this test");

        Assert.Null(GitMetrics.TryGetFirstCommitDate(_root, "does-not-exist.md"));
    }

    [Fact]
    public void TryGetFirstCommitDate_ReturnsNull_ForNonRepoDirectory_NeverThrows()
    {
        var nonRepo = Path.Combine(Path.GetTempPath(), "specscribe-nonrepo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(nonRepo);
        try
        {
            Assert.Null(GitMetrics.TryGetFirstCommitDate(nonRepo, "anything.md"));
        }
        finally
        {
            try { Directory.Delete(nonRepo, recursive: true); } catch { /* best-effort */ }
        }
    }

    private bool TryCreateHistory()
    {
        if (!RunGit("init")) return false;
        File.WriteAllText(Path.Combine(_root, "story.md"), "one\n");
        if (!RunGit("add .")) return false;
        if (!Commit("Seed story", "2026-01-05T12:00:00")) return false;
        File.WriteAllText(Path.Combine(_root, "story.md"), "one\ntwo\n");
        return RunGit("add .") && Commit("Edit story", "2026-01-09T12:00:00");
    }

    private bool Commit(string message, string authorDate) => RunGit(
        $"-c user.name=\"Cadence Tester\" -c user.email=cadence@example.com -c commit.gpgsign=false commit --date=\"{authorDate}\" -m \"{message}\"");

    private bool RunGit(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // Pin committer date too so nothing depends on the wall clock.
            psi.Environment["GIT_COMMITTER_DATE"] = "2026-01-09T12:00:00";
            using var process = Process.Start(psi);
            if (process is null) return false;
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
