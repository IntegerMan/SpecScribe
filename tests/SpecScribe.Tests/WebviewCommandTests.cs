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
}
