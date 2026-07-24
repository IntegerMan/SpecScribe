using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.8 coverage for the one new datum the <c>specscribe webview</c> payload carries: the
/// project's configured output root, expressed workspace-relative with forward slashes so the VS Code shim's
/// "Open Generated Site" command can join it to the workspace folder and find an already-generated
/// <c>index.html</c>. Pure string resolution — no spawn, no generation — so it is unit-testable in isolation.</summary>
public class WebviewCommandTests
{
    [Fact]
    public void ResolveConfiguredOutputRoot_DefaultsToSpecScribeOutput_RelativeToRepoRoot()
    {
        // The shim spawns `webview` without --output and never consults .specscribe (R5.3), so the resolved
        // output is always the default SpecScribeOutput under the repo root — expressed relative, forward-slashed.
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-cor-default");
        var options = new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

        Assert.Equal("SpecScribeOutput", WebviewCommand.ResolveConfiguredOutputRoot(options));
    }

    [Fact]
    public void ResolveConfiguredOutputRoot_NestedOutput_UsesForwardSlashes()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-cor-nested");
        var options = new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repoRoot, "build", "site"),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

        // Never emit a backslash even on Windows: the shim treats the value as a POSIX-joinable relative path.
        Assert.Equal("build/site", WebviewCommand.ResolveConfiguredOutputRoot(options));
    }

    // ===== Story 6.11: the resolved watch roots the shim builds its file watchers from ============================

    private static ForgeOptions RootedOptions(string repoRoot, string? source = null, string? adrs = null) =>
        new()
        {
            RepoRoot = repoRoot,
            SourceRoot = source ?? Path.Combine(repoRoot, ForgeOptions.SourceDirName),
            AdrSourceRoot = adrs ?? Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = adrs is not null,
            OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };

    [Fact]
    public void ResolveSourceRoot_And_AdrRoot_DefaultLayout_RepoRelative_ForwardSlashed()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-roots-default");
        var options = RootedOptions(repoRoot);

        Assert.Equal("_bmad-output", WebviewCommand.ResolveSourceRoot(options));
        Assert.Equal("docs/adrs", WebviewCommand.ResolveAdrRoot(options));
    }

    [Fact]
    public void ResolveSourceRoot_And_AdrRoot_CustomRoots_AreProjected()
    {
        // A repo with non-default --source/--adrs (Story 5.1/5.2) must watch the CUSTOM trees, not the literals.
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-roots-custom");
        var options = RootedOptions(
            repoRoot,
            source: Path.Combine(repoRoot, "spec", "artifacts"),
            adrs: Path.Combine(repoRoot, "decisions"));

        Assert.Equal("spec/artifacts", WebviewCommand.ResolveSourceRoot(options));
        Assert.Equal("decisions", WebviewCommand.ResolveAdrRoot(options));
    }

    [Fact]
    public void ResolveRepoRootOffset_AtRepoRoot_IsDot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-offset-root");
        // The shim spawns `webview` with cwd == the workspace folder; opened at the repo root they coincide → ".".
        Assert.Equal(".", WebviewCommand.ResolveRepoRootOffset(RootedOptions(repoRoot), workingDirectory: repoRoot));
    }

    [Fact]
    public void ResolveRepoRootOffset_OpenedOnSubdir_IsTheUpwardOffset()
    {
        // Opened two levels deep, the workspace folder is a descendant and the repo root is two up → "../..", so the
        // shim resolves the real repo root and anchors both watchers and reveal-source to it (the subdir-open fix).
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-offset-subdir");
        var workingDirectory = Path.Combine(repoRoot, "packages", "app");

        Assert.Equal("../..", WebviewCommand.ResolveRepoRootOffset(RootedOptions(repoRoot), workingDirectory));
    }

    // ===== Deferred item, Story 6.4 review: the scratch-dir key folds case only where the OS filesystem does ======

    [Fact]
    public void ScratchKey_IsStableForTheSameRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-scratch-stable");
        Assert.Equal(WebviewCommand.ScratchKey(repoRoot), WebviewCommand.ScratchKey(repoRoot));
    }

    [Fact]
    public void ScratchKey_CaseDifferingRepoRoots_MatchTheOsFilesystemsOwnCaseSensitivity()
    {
        // On Windows (case-INSENSITIVE filesystem, this project's primary target OS) two path casings of the
        // SAME physical repo (a workspace-folder URI vs. a manually-typed cwd, or drive-letter casing) must fold
        // to the SAME stable scratch dir — a blanket no-fold would silently reintroduce the "successive spawns
        // accumulate instead of overwrite" bug this key exists to prevent. On a case-sensitive filesystem
        // (Linux) two such paths ARE distinct repos and must not collide. [Review][Patch]
        var lower = "/home/dev/myrepo";
        var upper = "/home/dev/MYREPO";

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(WebviewCommand.ScratchKey(lower), WebviewCommand.ScratchKey(upper));
        }
        else
        {
            Assert.NotEqual(WebviewCommand.ScratchKey(lower), WebviewCommand.ScratchKey(upper));
        }
    }

    // ===== Regression: the scratch `.lock` must live OUTSIDE the wiped output root =================================

    [Fact]
    public void RedirectOutputToScratch_HeldLock_DoesNotBlockRecursiveWipeOfTheOutputRoot()
    {
        // The webview generation holds the scratch `.lock` open for the process's life (FileShare.None +
        // DeleteOnClose), then SiteGenerator.GenerateAll wipes the output root recursively before every full
        // rebuild. If the lock lived INSIDE the output root (the original bug) that wipe tried to delete a file the
        // SAME process holds exclusively locked and threw a sharing-violation IOException on every single run — a
        // deterministic self-deadlock the VS Code extension surfaced as "renderer exited 1: (no stderr)". This test
        // reproduces the exact mechanism (hold the lock, then wipe the output root) and asserts it no longer throws.
        var repoRoot = Path.Combine(Path.GetTempPath(), $"specscribe-scratch-wipe-{Guid.NewGuid():N}");
        try
        {
            var options = new ForgeOptions
            {
                RepoRoot = repoRoot,
                SourceRoot = Path.Combine(repoRoot, ForgeOptions.SourceDirName),
                AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
                AdrSourceExplicit = false,
                OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
                SiteTitle = "SpecScribe",
                IncludeReadme = false,
                DeepGitAnalytics = false,
            };

            var redirected = WebviewCommand.RedirectOutputToScratch(options);

            // Simulate a rebuild's clean step against a populated output tree while the lock is still held.
            Directory.CreateDirectory(redirected.OutputRoot);
            File.WriteAllText(Path.Combine(redirected.OutputRoot, "index.html"), "<html></html>");

            var ex = Record.Exception(() => Directory.Delete(redirected.OutputRoot, recursive: true));

            Assert.Null(ex); // with the fix the lock is a sibling of OutputRoot, so the wipe never touches it
        }
        finally
        {
            WebviewCommand.ReleaseScratchLockForTests();
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), "specscribe-webview", WebviewCommand.ScratchKey(repoRoot)), recursive: true); }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }
}
