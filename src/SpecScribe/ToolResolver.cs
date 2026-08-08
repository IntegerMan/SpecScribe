using System.Diagnostics;

namespace SpecScribe;

/// <summary>Resolves an external tool name (<c>git</c>, <c>node</c>) to an ABSOLUTE path, searching <c>PATH</c>
/// only — never the current directory. [Story 17.2 Task 2, Sonar <c>csharpsquid:S4036</c>]
///
/// <para><b>The measurement this exists for.</b> On Windows, <see cref="Process.Start(ProcessStartInfo)"/> with
/// <c>UseShellExecute = false</c> and a BARE file name reaches <c>CreateProcessW</c>, whose documented search
/// order includes <b>the current directory of the CALLING process</b> ahead of <c>PATH</c>. It is NOT the
/// child's <c>WorkingDirectory</c> that is searched — so <see cref="GitMetrics"/> setting
/// <c>WorkingDirectory</c> to the analyzed repo was neither the risk nor a mitigation. The risk is that
/// SpecScribe's OWN cwd is normally inside the repository being analyzed, because that is the documented
/// invocation: <c>cd some-cloned-repo &amp;&amp; specscribe generate</c>.</para>
///
/// <para><b>Reproduced 2026-08-08 on Windows 11, baseline <c>e8a689d</c>, both arms:</b> with a harmless marker
/// binary planted as <c>git.exe</c> at a scratch repo root and cwd set to that root,
/// <c>Process.Start("git", "--version")</c> executed <b>the planted binary</b> when
/// <c>NoDefaultCurrentDirectoryInExePath</c> was unset — proven by the child's own stderr naming its sidecar
/// <c>marker.dll</c> in the hostile directory — and executed the REAL git when that variable was set to
/// <c>1</c>. Same result for <c>node</c>. The variable is NOT set in a default end-user shell (it happened to
/// be set inside this project's Git Bash session, which is exactly the confounder that made measuring
/// necessary rather than reasoning).</para>
///
/// <para><b>Why a PATH walk rather than a 3-tier resolver.</b> <c>extension/src/extension.ts</c>'s
/// <c>resolveTool()</c> is a setting → bundled → PATH cascade, and it is the right shape for locating
/// SpecScribe ITSELF (the user may have it anywhere). <c>git</c> and <c>node</c> are different: there is no
/// setting to honor, nothing bundled to prefer, and the only question is "which PATH entry". Copying the
/// three-tier shape here would add two tiers that could never fire. What IS reused is the principle — resolve
/// to an absolute path before spawning, never hand a bare name to the OS loader.</para></summary>
public static class ToolResolver
{
    // Resolution is stable for a process lifetime and a generate spawns git many hundreds of times, so the
    // answer is cached. A null entry caches "not found" too — a missing tool must not re-walk PATH per call.
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>The absolute path to <paramref name="toolName"/>, or null when it is not on <c>PATH</c>.
    /// The current directory is never consulted.</summary>
    public static string? Find(string toolName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(toolName, out var cached)) return cached;
            var resolved = Search(toolName);
            Cache[toolName] = resolved;
            return resolved;
        }
    }

    /// <summary>The absolute path to <paramref name="toolName"/>, falling back to the bare name when it cannot
    /// be found. The fallback is deliberate: a machine with no <c>git</c> on <c>PATH</c> should fail with the
    /// OS's own "not found" through the caller's existing error handling, exactly as before this story — NOT
    /// with a new exception type from the resolver. Callers that reach the fallback are no worse off than they
    /// were, and every machine that HAS the tool is now hardened.</summary>
    public static string Resolve(string toolName) => Find(toolName) ?? toolName;

    private static string? Search(string toolName)
    {
        // An absolute or relative path was supplied rather than a bare name — the caller already decided.
        if (toolName.Contains(Path.DirectorySeparatorChar) || toolName.Contains(Path.AltDirectorySeparatorChar))
            return File.Exists(toolName) ? Path.GetFullPath(toolName) : null;

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        var extensions = ExecutableExtensions();

        foreach (var rawDir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = rawDir.Trim().Trim('"');
            if (dir.Length == 0) continue;

            // A RELATIVE PATH entry resolves against the current directory, which is the very thing this class
            // exists to keep out of the search. `.` on PATH is the classic form. Skipped, not resolved.
            if (!Path.IsPathRooted(dir)) continue;

            foreach (var ext in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, toolName + ext);
                }
                catch (ArgumentException)
                {
                    // A PATH entry containing invalid path characters — skip it rather than fail the run.
                    break;
                }

                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    /// <summary>The extensions to try, in order. On Windows this is <c>PATHEXT</c> (with an empty entry first so
    /// an extensionless file still resolves); elsewhere just the bare name.</summary>
    private static string[] ExecutableExtensions()
    {
        if (!OperatingSystem.IsWindows()) return [string.Empty];

        var pathext = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathext)) return [".EXE", ".CMD", ".BAT", ".COM", string.Empty];

        var parts = pathext
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .ToList();
        parts.Add(string.Empty);
        return [.. parts];
    }
}
