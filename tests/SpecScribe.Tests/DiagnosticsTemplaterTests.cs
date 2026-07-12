using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 4.8 diagnostics run-log page: the notice list projected off the run's
/// event channel (filter + fine-category recovery, no double-count), the notices table, the all-clear state,
/// the effective-configuration disclosure, and the local-first config model.</summary>
public class DiagnosticsTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static DiagnosticsConfig Config(bool deepGit = false, bool adrExplicit = false) => new()
    {
        SiteTitle = "SpecScribe",
        RepoRoot = "C:/Dev/SpecScribe",
        SourceRootDisplay = "_bmad-output",
        AdrSourceDisplay = "docs/adrs",
        AdrSourceExplicit = adrExplicit,
        OutputRootDisplay = "SpecScribeOutput",
        DeepGitAnalytics = deepGit,
        IncludeReadme = true,
        CodeSourceDisplay = "in-portal only",
        ModuleDisplay = "BMad Method",
    };

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    [Fact]
    public void RenderPage_OneNoticePerCategory_RendersARowCarryingCategoryWordSourceAndMessage()
    {
        var notices = new[]
        {
            new DiagnosticNotice("Unsupported", "impl/sprint-status.yaml", "no development_status map", DiagnosticSeverity.Warning),
            new DiagnosticNotice("Malformed", "adrs/0001.md", "parse failed", DiagnosticSeverity.Error),
            new DiagnosticNotice("Skipped", "impl/foo.md", null, DiagnosticSeverity.Warning),
        };

        var html = DiagnosticsTemplater.RenderPage(notices, Config(), Nav());

        Assert.Contains("diagnostics-table", html);
        // The fine category word survives as text on every row (never color-only, UX-DR17/NFR6).
        Assert.Contains(">Unsupported</span>", html);
        Assert.Contains(">Malformed</span>", html);
        Assert.Contains(">Skipped</span>", html);
        // Source paths + messages render.
        Assert.Contains("impl/sprint-status.yaml", html);
        Assert.Contains("no development_status map", html);
        // Severity → color class, not lifecycle stage tokens.
        Assert.Contains("status-badge diag-error", html);   // Malformed
        Assert.Contains("status-badge diag-warn", html);    // Unsupported / Skipped
        // A null message renders as an em-dash cell, never crashes.
        Assert.Contains("&mdash;", html);
        // Three notices → three source cells (one row each).
        Assert.Equal(3, Count(html, "diagnostics-source"));
        Assert.Contains("&middot; 3 notices &middot;", html);
    }

    [Fact]
    public void RenderPage_EmptyNoticeList_RendersAllClearStateNotAnEmptyTable()
    {
        var html = DiagnosticsTemplater.RenderPage(Array.Empty<DiagnosticNotice>(), Config(), Nav());

        Assert.Contains("diagnostics-clear", html);
        Assert.Contains("No notices", html);
        Assert.DoesNotContain("diagnostics-table", html);
        Assert.Contains("&middot; 0 notices &middot;", html);
        // AC #2: the config disclosure still renders in the all-clear case.
        Assert.Contains("Effective configuration", html);
    }

    [Fact]
    public void RenderPage_ConfigDetails_CarryEveryEffectiveConfigField()
    {
        var html = DiagnosticsTemplater.RenderPage(Array.Empty<DiagnosticNotice>(), Config(deepGit: true), Nav());

        Assert.Contains("<dt>Detected framework</dt><dd>BMad Method</dd>", html);
        Assert.Contains("<dt>Source root</dt><dd>_bmad-output</dd>", html);
        Assert.Contains("<dt>ADR location</dt><dd>docs/adrs (default)</dd>", html);
        Assert.Contains("<dt>Output directory</dt><dd>SpecScribeOutput</dd>", html);
        Assert.Contains("<dt>Deep-git analytics</dt><dd>on (--deep-git)</dd>", html);
    }

    [Fact]
    public void RenderPage_ExplicitAdrSource_IsMarkedExplicit()
    {
        var html = DiagnosticsTemplater.RenderPage(Array.Empty<DiagnosticNotice>(), Config(adrExplicit: true), Nav());
        Assert.Contains("<dt>ADR location</dt><dd>docs/adrs (explicit (--adrs))</dd>", html);
    }

    // ---- DiagnosticNotice.FromEvents: filter + fine-category recovery (Task 2) ----

    [Fact]
    public void FromEvents_FiltersToNonFatalOutcomes_AndRecoversFineCategory()
    {
        var events = new[]
        {
            new GenerationEvent(GenerationOutcome.Generated, "index.html", TimeSpan.Zero),                             // filtered out
            new GenerationEvent(GenerationOutcome.Skipped, "impl/sprint-status.yaml", TimeSpan.Zero, "[Unsupported] no development_status map", FromAdapterDiagnostic: true),
            new GenerationEvent(GenerationOutcome.Error, "adrs/0001.md", TimeSpan.Zero, "[Malformed] boom", FromAdapterDiagnostic: true),
            // Same bracket shape as an adapter diagnostic, but NOT flagged as one — a render-time exception
            // that happens to look like a category tag must never be misread as one. [Review][Patch]
            new GenerationEvent(GenerationOutcome.Error, "x.md", TimeSpan.Zero, "[Error] raw exception text"),
            new GenerationEvent(GenerationOutcome.Skipped, "y.md", TimeSpan.Zero, null),                               // null msg → coarse "Skipped"
        };

        var notices = DiagnosticNotice.FromEvents(events);

        Assert.Equal(4, notices.Count); // the Generated event is not a notice
        Assert.Equal("Unsupported", notices[0].Category);
        Assert.Equal("no development_status map", notices[0].Message); // the [Category] prefix is stripped
        Assert.Equal(DiagnosticSeverity.Warning, notices[0].Severity);
        Assert.Equal("Malformed", notices[1].Category);
        Assert.Equal(DiagnosticSeverity.Error, notices[1].Severity);
        // A render-time error message is never bracket-parsed (not FromAdapterDiagnostic), even when it
        // coincidentally starts with a matching [Category] shape — the whole message is left intact under
        // the coarse outcome word. [Review][Patch]
        Assert.Equal("Error", notices[2].Category);
        Assert.Equal("[Error] raw exception text", notices[2].Message);
        Assert.Equal("Skipped", notices[3].Category);
        Assert.Null(notices[3].Message);
    }

    // ---- DiagnosticsConfig.FromRun: local-first, repo-relative formatting (Task 3) ----

    [Fact]
    public void FromRun_FormatsPathsRepoRelative_AndDetectsModule()
    {
        var repo = Path.Combine(Path.GetTempPath(), "diagcfg-repo");
        var options = new ForgeOptions
        {
            RepoRoot = repo,
            SourceRoot = Path.Combine(repo, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repo, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repo, "SpecScribeOutput"),
            SiteTitle = "SpecScribe",
            IncludeReadme = true,
            DeepGitAnalytics = true,
        };

        var config = DiagnosticsConfig.FromRun(options, ModuleContext.None);

        Assert.Equal("_bmad-output", config.SourceRootDisplay);
        Assert.Equal(PathUtil.NormalizeSlashes(Path.Combine("docs", "adrs")), config.AdrSourceDisplay);
        Assert.Equal("SpecScribeOutput", config.OutputRootDisplay);
        Assert.True(config.DeepGitAnalytics);
        // ModuleContext.None → Unknown module surfaces as an explicit "not detected" line, not a blank.
        Assert.Equal("Unknown (not detected)", config.ModuleDisplay);
    }

    [Fact]
    public void FromRun_OutputOutsideRepo_FallsBackToAbsolutePath()
    {
        var repo = Path.Combine(Path.GetTempPath(), "diagcfg-repo");
        var elsewhere = Path.Combine(Path.GetTempPath(), "elsewhere", "out");
        var options = new ForgeOptions
        {
            RepoRoot = repo,
            SourceRoot = Path.Combine(repo, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repo, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = elsewhere,
            SiteTitle = "SpecScribe",
            IncludeReadme = true,
            DeepGitAnalytics = false,
        };

        var config = DiagnosticsConfig.FromRun(options, ModuleContext.None);

        // Not under the repo root → the absolute path reads clearer than a "../"-led relative path.
        Assert.Equal(PathUtil.NormalizeSlashes(elsewhere), config.OutputRootDisplay);
    }
}
