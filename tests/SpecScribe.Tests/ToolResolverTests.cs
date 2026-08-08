using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Pins the tool-resolution surface Story 17.2 Task 2 measured. [Sonar csharpsquid:S4036]
///
/// <para><b>The measurement, for the record.</b> On Windows 11 at baseline <c>e8a689d</c>, with a harmless
/// marker binary planted as <c>git.exe</c> at a scratch repo root and the CALLING process's cwd set to that
/// root, <c>Process.Start("git", "--version")</c> executed <b>the planted binary</b> — proven by the child's
/// own stderr naming its sidecar <c>marker.dll</c> in the hostile directory. Setting
/// <c>NoDefaultCurrentDirectoryInExePath=1</c> made the real git run instead, which is the control arm. The
/// variable is not set in a default end-user shell. Same result for <c>node</c>.</para>
///
/// <para><b>What these tests can and cannot assert.</b> Planting an executable and spawning it is a slow,
/// environment-dependent integration test that would also have to mutate <c>Environment.CurrentDirectory</c>
/// — a PROCESS-WIDE mutation that races every other test in a parallel xUnit run. So the regression is pinned
/// at the resolver instead, on the property that actually closes the hole: <b>the answer is an absolute path
/// drawn from PATH, and the current directory is never consulted.</b> A future change that reverted
/// <c>GitMetrics</c> to a bare <c>"git"</c> would not fail these tests, so the call sites are additionally
/// pinned by <see cref="SpawnSitesResolveAbsolutePaths"/> reading the shipped source.</para></summary>
public class ToolResolverTests
{
    [Fact]
    public void Resolve_ReturnsAbsolutePath_ForATooOnPath()
    {
        // `dotnet` is on PATH wherever this suite can run at all.
        var resolved = ToolResolver.Find("dotnet");

        Assert.NotNull(resolved);
        Assert.True(Path.IsPathRooted(resolved), $"expected an absolute path, got '{resolved}'");
        Assert.True(File.Exists(resolved), $"resolved path does not exist: '{resolved}'");
    }

    [Fact]
    public void Resolve_DoesNotPreferAnExecutableInTheCurrentDirectory()
    {
        // THE REGRESSION. A file planted in the current directory must not be resolved, because PATH is the
        // only thing searched. Named with a token no real tool uses so a PATH hit is impossible.
        var name = "specscribe-hijack-probe";
        var planted = Path.Combine(Environment.CurrentDirectory, name + ".exe");

        var createdByThisTest = false;
        try
        {
            if (!File.Exists(planted))
            {
                File.WriteAllBytes(planted, [0x4D, 0x5A]); // "MZ" — enough to look like a PE to a naive check
                createdByThisTest = true;
            }

            Assert.Null(ToolResolver.Find(name));
        }
        finally
        {
            if (createdByThisTest && File.Exists(planted)) File.Delete(planted);
        }
    }

    [Fact]
    public void Resolve_IgnoresRelativePathEntries()
    {
        // `.` on PATH is the classic form of the same defect: a relative entry resolves against the current
        // directory, which is precisely what this class exists to keep out of the search. It must be SKIPPED,
        // not resolved. Asserted through the public surface: a name that exists ONLY in the current directory
        // stays unresolvable even though `.` is a PATH entry in this process.
        var original = Environment.GetEnvironmentVariable("PATH");
        var name = "specscribe-relative-probe";
        var planted = Path.Combine(Environment.CurrentDirectory, name + ".exe");
        try
        {
            File.WriteAllBytes(planted, [0x4D, 0x5A]);
            Environment.SetEnvironmentVariable("PATH", "." + Path.PathSeparator + original);

            Assert.Null(ToolResolver.Find(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
            if (File.Exists(planted)) File.Delete(planted);
        }
    }

    [Fact]
    public void Resolve_FallsBackToTheBareNameWhenNotFound()
    {
        // Deliberate: a machine with no `git` should fail through the caller's EXISTING error handling with
        // the OS's own "not found", exactly as before this story — not with a new exception from the resolver.
        Assert.Equal("definitely-not-a-real-tool-xyz", ToolResolver.Resolve("definitely-not-a-real-tool-xyz"));
    }

    [Fact]
    public void Resolve_HonoursAnExplicitPath()
    {
        var self = typeof(ToolResolverTests).Assembly.Location;

        Assert.Equal(Path.GetFullPath(self), ToolResolver.Find(self));
    }

    [Fact]
    public void SpawnSitesResolveAbsolutePaths()
    {
        // Guards the CALL SITES, not the resolver: the hole reopens if someone writes a bare name again, and
        // no unit test over ToolResolver could see that. Reads the shipped source rather than trusting a
        // comment. Sonar flags exactly these two C# sites as csharpsquid:S4036.
        var root = RepoRoot();
        var git = File.ReadAllText(Path.Combine(root, "src", "SpecScribe", "GitMetrics.cs"));
        var nuxt = File.ReadAllText(Path.Combine(root, "src", "SpecScribe", "NuxtPrerender.cs"));

        Assert.DoesNotContain("FileName = \"git\"", git, StringComparison.Ordinal);
        Assert.Contains("ToolResolver.Resolve(\"git\")", git, StringComparison.Ordinal);

        Assert.DoesNotContain("new ProcessStartInfo(\"node\"", nuxt, StringComparison.Ordinal);
        Assert.Contains("ToolResolver.Resolve(\"node\")", nuxt, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SpecScribe")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
