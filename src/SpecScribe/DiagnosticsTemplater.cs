using System.Text;

namespace SpecScribe;

/// <summary>Severity axis for a diagnostics notice — deliberately distinct from the lifecycle
/// <c>--status-*</c> stage vocabulary (a run notice is not a delivery stage). Drives only the badge color:
/// <see cref="Error"/> borrows the site's established <c>--rust</c> attention accent, <see cref="Warning"/>
/// stays muted/neutral. The category word is always rendered as text beside the color (never color-only,
/// UX-DR17 / NFR6). [Story 4.8]</summary>
public enum DiagnosticSeverity { Error, Warning }

/// <summary>One row on the Story 4.8 diagnostics page: a single non-fatal notice from the run, projected off
/// the unified <see cref="GenerationEvent"/> channel. <see cref="Category"/> is the fine
/// <see cref="AdapterDiagnosticCategory"/> word for ingest notices (recovered from the message prefix
/// <see cref="SiteGenerator"/> adds) or the coarse <see cref="GenerationOutcome"/> word for render-time
/// notices. [Story 4.8 Task 2]</summary>
/// <param name="Category">The category word shown in the badge (e.g. <c>Unsupported</c>, <c>Malformed</c>,
/// <c>Skipped</c>, <c>Error</c>).</param>
/// <param name="SourcePath">The source- or output-relative path the run reported for this notice.</param>
/// <param name="Message">Human-readable detail; may be null for a bare skip (rendered as an em-dash).</param>
/// <param name="Severity">Error vs. warning — the badge color only.</param>
public sealed record DiagnosticNotice(string Category, string SourcePath, string? Message, DiagnosticSeverity Severity)
{
    /// <summary>Projects the run's non-fatal notices off the single accumulated <see cref="GenerationEvent"/>
    /// list — the "whole run's non-fatal notices" surface with ZERO double-counting (each adapter diagnostic is
    /// mapped into that list exactly once by <see cref="SiteGenerator"/>). Filters to the two non-fatal outcomes
    /// (<see cref="GenerationOutcome.Error"/> / <see cref="GenerationOutcome.Skipped"/>) and recovers the fine
    /// ingest category from the message prefix. Preserves the events' order. [Story 4.8 Task 2]</summary>
    public static IReadOnlyList<DiagnosticNotice> FromEvents(IReadOnlyList<GenerationEvent> events)
    {
        var notices = new List<DiagnosticNotice>();
        foreach (var e in events)
        {
            if (e.Outcome is not (GenerationOutcome.Error or GenerationOutcome.Skipped))
            {
                continue;
            }

            var (category, message) = SplitCategory(e);
            var severity = e.Outcome == GenerationOutcome.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
            notices.Add(new DiagnosticNotice(category, e.RelativePath, message, severity));
        }

        return notices;
    }

    /// <summary>Recovers the fine ingest category from the <c>[Category] …</c> prefix
    /// <see cref="SiteGenerator.MapDiagnostics"/> adds, so the four-category ingest vocabulary survives the
    /// coarse two-outcome collapse. Only attempted when <see cref="GenerationEvent.FromAdapterDiagnostic"/> is
    /// set — i.e. the event is known to have come from <c>MapDiagnostics</c> — so a render-time exception
    /// message that merely happens to start with a matching <c>[Word]</c> shape is never misread as a category
    /// tag. [Story 4.8 Task 2] [Review][Patch]</summary>
    private static (string Category, string? Message) SplitCategory(GenerationEvent e)
    {
        var msg = e.Message;
        if (e.FromAdapterDiagnostic && msg is { Length: > 2 } && msg[0] == '[')
        {
            var close = msg.IndexOf(']');
            if (close > 1)
            {
                var word = msg[1..close];
                if (Enum.TryParse<AdapterDiagnosticCategory>(word, ignoreCase: false, out _))
                {
                    var rest = msg[(close + 1)..].TrimStart();
                    return (word, rest.Length == 0 ? null : rest);
                }
            }
        }

        // No recognized category prefix (a render-time Error/Skipped) → the coarse outcome word is the category.
        return (e.Outcome.ToString(), msg);
    }
}

/// <summary>The effective configuration + detection results for a run — AC #2's "what this run actually did"
/// surface. Every field is a display-ready string/bool derived ENTIRELY from already-resolved values on
/// <see cref="ForgeOptions"/> and <see cref="ModuleContext"/> plus pure <see cref="Path.GetRelativePath"/>
/// string math: no file I/O, no remote calls, consistent with local-first operation (AC #2 / NFR3). [Story 4.8 Task 3]</summary>
public sealed record DiagnosticsConfig
{
    public required string SiteTitle { get; init; }
    public required string RepoRoot { get; init; }
    public required string SourceRootDisplay { get; init; }
    public required string AdrSourceDisplay { get; init; }
    public required bool AdrSourceExplicit { get; init; }
    public required string OutputRootDisplay { get; init; }
    public required bool DeepGitAnalytics { get; init; }
    public required bool IncludeReadme { get; init; }

    /// <summary>The detected framework/module label (e.g. "BMad Method"), or "Unknown (not detected)" when no
    /// methodology module resolved — the AC #2 "detected framework/module" line.</summary>
    public required string ModuleDisplay { get; init; }

    /// <summary>Builds the model from a run's resolved options and detected module. Local-first by construction:
    /// only field reads and <see cref="Path.GetRelativePath"/> (a pure string transform — it never touches the
    /// filesystem or network). [Story 4.8 Task 3, AC #2 / NFR3]</summary>
    public static DiagnosticsConfig FromRun(ForgeOptions options, ModuleContext module) => new()
    {
        SiteTitle = options.SiteTitle,
        RepoRoot = PathUtil.NormalizeSlashes(options.RepoRoot),
        SourceRootDisplay = RelToRepo(options.RepoRoot, options.SourceRoot),
        AdrSourceDisplay = RelToRepo(options.RepoRoot, options.AdrSourceRoot),
        AdrSourceExplicit = options.AdrSourceExplicit,
        OutputRootDisplay = RelToRepo(options.RepoRoot, options.OutputRoot),
        DeepGitAnalytics = options.DeepGitAnalytics,
        IncludeReadme = options.IncludeReadme,
        ModuleDisplay = module.Module == BmadModule.Unknown
            ? "Unknown (not detected)"
            : module.Commands.ModuleLabel,
    };

    /// <summary>Renders <paramref name="path"/> relative to the repo root for readability, falling back to the
    /// absolute path when it isn't under the repo root (e.g. an <c>--output</c> pointed elsewhere). Pure string
    /// math via <see cref="Path.GetRelativePath"/> — no I/O. [Story 4.8 Task 3]</summary>
    private static string RelToRepo(string repoRoot, string path)
    {
        var rel = Path.GetRelativePath(repoRoot, path);
        // GetRelativePath returns the input verbatim (absolute) when there's no shared root, and a "../"-led
        // path when the target sits above/aside the repo — in either case the absolute path reads clearer.
        // Checked as a full leading path segment (not a bare string prefix) so a real child folder whose name
        // happens to start with ".." (e.g. "..cache") isn't mistaken for an escape above the repo root.
        // [Review][Patch]
        if (Path.IsPathRooted(rel) || rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar)
            || rel.StartsWith(".." + Path.AltDirectorySeparatorChar))
        {
            return PathUtil.NormalizeSlashes(path);
        }

        return rel == "." ? "." : PathUtil.NormalizeSlashes(rel);
    }
}

/// <summary>Renders the generation diagnostics (run-log) page (<c>diagnostics.html</c>): the run's non-fatal
/// notices in a table (category badge · source path · message), with the effective configuration + detection
/// results folded into a collapsible <c>&lt;details&gt;</c> below it (Owner-decided diagnostics-first
/// silhouette). A zero-notice run renders a clean all-clear state instead of an empty table. Reached via the
/// site-wide footer → About → Diagnostics path (not the top nav). [Story 4.8 Task 4]</summary>
public static class DiagnosticsTemplater
{
    public static string RenderPage(IReadOnlyList<DiagnosticNotice> notices, DiagnosticsConfig config, SiteNav nav)
    {
        var outputPath = SiteNav.DiagnosticsOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Generation Diagnostics — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"Run log and effective configuration for {nav.SiteTitle}'s last full generation."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("About", SiteNav.AboutOutputPath),
            ("Diagnostics", null),
        }));

        var noticeWord = Charts.Plural(notices.Count, "notice", "notices");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Generation Diagnostics</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(config.SiteTitle)} &middot; {notices.Count} {noticeWord} &middot; from the last full generation</div>\n");
        sb.Append("</header>\n\n");

        // info-page: the shared centered content column (side gutters + max-width) — same as the About page. [About polish]
        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append(RenderNotices(notices));
        sb.Append(RenderConfig(config));
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The notices table — one row per notice — or, when there are none, the all-clear panel (so a
    /// clean run reads as intentionally clean, never as an empty/broken table). AC #1. [Story 4.8 Task 4]</summary>
    private static string RenderNotices(IReadOnlyList<DiagnosticNotice> notices)
    {
        var sb = new StringBuilder();
        sb.Append("<section class=\"chart-panel diagnostics-panel\">\n");

        if (notices.Count == 0)
        {
            // All-clear state: a clean run has no notices, so say so plainly rather than render an empty table.
            sb.Append("  <div class=\"diagnostics-clear\">\n");
            sb.Append($"    {StatusStyles.Badge("done", "All clear")}\n");
            sb.Append("    <p>No notices &mdash; this was a clean run. Every discovered artifact rendered.</p>\n");
            sb.Append("  </div>\n");
            sb.Append("</section>\n\n");
            return sb.ToString();
        }

        sb.Append("  <table class=\"md-table diagnostics-table\">\n");
        sb.Append("    <thead><tr><th>Category</th><th>Source</th><th>Message</th></tr></thead>\n");
        sb.Append("    <tbody>\n");
        foreach (var notice in notices)
        {
            var cssClass = notice.Severity == DiagnosticSeverity.Error ? "diag-error" : "diag-warn";
            sb.Append("      <tr>\n");
            sb.Append($"        <td>{Badge(cssClass, notice.Category)}</td>\n");
            sb.Append($"        <td class=\"diagnostics-source\">{PathUtil.Html(notice.SourcePath)}</td>\n");
            // A bare skip can carry no message — render an em-dash cell rather than an empty one, never crash.
            var message = notice.Message is { Length: > 0 } m ? PathUtil.Html(m) : "&mdash;";
            sb.Append($"        <td>{message}</td>\n");
            sb.Append("      </tr>\n");
        }
        sb.Append("    </tbody>\n");
        sb.Append("  </table>\n");
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    /// <summary>The effective-configuration disclosure — a native <c>&lt;details&gt;</c> (no JS, so
    /// reduced-motion is trivially satisfied) holding the config/detection results as a definition list. Always
    /// rendered, including in the all-clear case. AC #2. [Story 4.8 Task 4]</summary>
    private static string RenderConfig(DiagnosticsConfig config)
    {
        var adrNote = config.AdrSourceExplicit ? "explicit (--adrs)" : "default";

        var sb = new StringBuilder();
        sb.Append("<details class=\"chart-panel diagnostics-config\">\n");
        sb.Append("  <summary>Effective configuration &amp; detection</summary>\n");
        sb.Append("  <dl class=\"diag-config\">\n");
        AppendRow(sb, "Site title", config.SiteTitle);
        AppendRow(sb, "Detected framework", config.ModuleDisplay);
        AppendRow(sb, "Repo root", config.RepoRoot);
        AppendRow(sb, "Source root", config.SourceRootDisplay);
        AppendRow(sb, "ADR location", $"{config.AdrSourceDisplay} ({adrNote})");
        AppendRow(sb, "Output directory", config.OutputRootDisplay);
        AppendRow(sb, "Deep-git analytics", config.DeepGitAnalytics ? "on (--deep-git)" : "off");
        AppendRow(sb, "README included", config.IncludeReadme ? "yes" : "no");
        sb.Append("  </dl>\n");
        sb.Append("</details>\n\n");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value) =>
        sb.Append($"    <div class=\"cap-row\"><dt>{PathUtil.Html(label)}</dt><dd>{PathUtil.Html(value)}</dd></div>\n");

    /// <summary>A diagnostics severity badge — the shared <c>.status-badge</c> chrome with a severity color
    /// class (routed through the two Story 4.8 <c>diag-*</c> rules, not the lifecycle stage tokens, since a
    /// notice isn't a delivery stage). The category word is always present as text, so the badge is never
    /// color-only (UX-DR17 / NFR6). [Story 4.8 Task 4]</summary>
    private static string Badge(string cssClass, string category) =>
        $"<span class=\"status-badge {cssClass}\">{PathUtil.Html(category)}</span>";
}
