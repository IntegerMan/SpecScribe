using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>code-map.html</c> page — the source-code treemap surface (Story 7.6, FR14).
/// Reuses the same page shell every <c>Write*</c> page uses (<see cref="PathUtil.RenderHeadOpen"/> + nav +
/// breadcrumb + <c>&lt;main id="main-content"&gt;</c> + footer); the treemap SVG itself comes from the pure, server-
/// computed <see cref="Charts.CodeTreemap"/>. The page is fully correct with JavaScript OFF: each of the four
/// precomputed <see cref="CodeMapVariant"/> panels ships a server-rendered treemap sized-by-LOC with the default
/// colorize dimension baked in, a legend, the "git data unavailable" notice when applicable, and a complete
/// text-equivalent table — and the two "exclude spec-driven development directories" / "exclude tests" checkboxes
/// that pick which panel shows are PURE CSS (no script needed at all — round 2). Only the colorize dropdown and
/// directory zoom remain a scoped JS enhancement per panel: emitted <c>hidden</c>/inert and revealed by that script,
/// so a no-JS visitor never sees a dead control. Replaced the retired Story 3.4 structure-tree page. [Story 7.6]</summary>
public static class CodeMapTemplater
{
    /// <summary>Renders the whole page from all four precomputed filter combinations (<see cref="CodeMap.BuildVariants"/>).
    /// Headline stats describe the unfiltered ("full") variant regardless of which panel the checkboxes currently show
    /// — the checkboxes are a view onto one codebase, not a different one. <paramref name="fileHref"/> is the guarded
    /// in-portal code-page resolver (Story 7.1): a non-null return routes a file to its code page, a null return (or a
    /// null resolver) leaves it a plain, focusable rect — never a broken link.</summary>
    public static string RenderPage(IReadOnlyList<CodeMapVariant> variants, SiteNav nav, Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.CodeMapOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — code-map.html is at the output root.

        var full = variants.FirstOrDefault(v => v.Key == "full") ?? variants[0];
        var map = full.Map;

        var fileWord = Charts.Plural(map.FileCount, "file", "files");
        var dirWord = Charts.Plural(map.DirectoryCount, "directory", "directories");
        var lineWord = Charts.Plural((int)Math.Min(map.TotalLines, int.MaxValue), "line", "lines");
        var headline = $"{map.FileCount:N0} {fileWord} across {map.DirectoryCount:N0} {dirWord} · {map.TotalLines:N0} {lineWord} of code";

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Code Map — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Source-code treemap for {nav.SiteTitle} — every file sized by its lines of code and colorable by git-derived change activity, with a full text-equivalent listing."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildInsightsLocalContext(outputPath)));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Code Map", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Code Map</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {PathUtil.Html(headline)}</p>\n\n");

        // Shown once, shared across all four filter combinations (not one of the four panels themselves) —
        // deliberately NOT wrapped in a .chart-panel card: the two filter checkboxes right below need to be plain,
        // unwrapped siblings of the four .codemap-view panels (the CSS sibling-combinator toggle needs them at the
        // same nesting level as their targets), so nothing here can be a common ancestor of both.
        sb.Append("<h3>Source Code Treemap</h3>\n");
        sb.Append("<p class=\"chart-lead\">Each rectangle is a file, sized by its lines of code and nested inside its directory. ");
        sb.Append("Color shows a git-derived change signal when available — use the dropdown to recolor by another dimension, and select a directory to zoom in. Filter what's shown with the checkboxes below.</p>\n\n");

        // Pure CSS: no JavaScript is needed for filtering to work (round 2).
        AppendFilterCheckbox(sb, "cm-exclude-spec", "Exclude spec-driven development directories");
        AppendFilterCheckbox(sb, "cm-exclude-tests", "Exclude tests");

        foreach (var variant in variants)
        {
            AppendVariantPanel(sb, variant, fileHref, prefix);
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendFilterCheckbox(StringBuilder sb, string id, string label)
    {
        sb.Append($"  <input type=\"checkbox\" id=\"{id}\" class=\"codemap-filter-checkbox\">");
        sb.Append($"<label for=\"{id}\" class=\"codemap-filter-label\">{PathUtil.Html(label)}</label>\n");
    }

    /// <summary>Renders one precomputed filter combination as a self-contained panel: its own drill breadcrumb,
    /// colorize dropdown, legend/notice, SVG treemap, and text-equivalent table. The treemap card and the table card
    /// are SIBLING <c>.chart-panel</c>s inside the (unstyled) <c>.codemap-view</c> wrapper — never one nested inside
    /// the other, matching how every other chart on this site pairs a visual + its text-equivalent table as two
    /// top-level cards. Nothing here carries an <c>id</c>: all four panels share the same markup shape and the JS
    /// enhancement scopes every lookup to whichever panel it is currently wiring (class selectors, not
    /// <c>getElementById</c>). Exactly one panel is visible at a time via the pure-CSS checkbox toggle; the others
    /// are <c>display:none</c> (and therefore out of the accessibility tree) until selected.</summary>
    private static void AppendVariantPanel(StringBuilder sb, CodeMapVariant variant, Func<string, string?>? fileHref, string prefix)
    {
        sb.Append($"<div class=\"codemap-view\" data-view=\"{PathUtil.Html(variant.Key)}\">\n");

        if (variant.Map.IsEmpty)
        {
            sb.Append("  <p class=\"codemap-notice\" role=\"note\">No files match this filter.</p>\n");
            sb.Append("</div>\n\n");
            return;
        }

        var files = variant.Map.Files();
        var hasMetrics = files.Any(f => f.Metrics is not null);

        sb.Append("  <section class=\"chart-panel codemap-panel\">\n");

        if (variant.ExcludesSpecDev || variant.ExcludesTests)
        {
            var excluded = variant.ExcludesSpecDev && variant.ExcludesTests
                ? "spec-driven development directories and tests excluded"
                : variant.ExcludesSpecDev
                    ? "spec-driven development directories excluded"
                    : "tests excluded";
            sb.Append($"    <p class=\"codemap-view-note\">{Charts.Plural(variant.Map.FileCount, "file", "files")} shown — {excluded}.</p>\n");
        }

        // The drill breadcrumb is JS-driven (zoom requires script); emit it hidden so a no-JS visitor never sees an
        // inert control. The enhancement script reveals it on init, scoped to this panel.
        sb.Append("    <nav class=\"codemap-drill\" aria-label=\"Treemap zoom\" hidden>\n");
        sb.Append("      <ol class=\"codemap-breadcrumb\">\n");
        sb.Append("        <li><button type=\"button\" class=\"codemap-crumb\" data-path=\"\" aria-current=\"true\">All files</button></li>\n");
        sb.Append("      </ol>\n");
        sb.Append("    </nav>\n");

        // File type is the one colorize dimension that needs no git data, so the dropdown + a legend always
        // render once the variant has files — the "git data unavailable" state is no longer a fully-inert
        // controls block, just a smaller supplementary note below a WORKING (file-type) colorize dimension.
        // [Story 7.9 owner-directed design decision]
        AppendColorizeControls(sb, hasMetrics);
        AppendLegend(sb, hasMetrics);
        AppendDiscreteLegend(sb, files, hasMetrics);
        if (!hasMetrics)
        {
            sb.Append("    <p class=\"codemap-notice codemap-notice-secondary\" role=\"note\">Git change data is unavailable (run with <code>--deep-git</code> in a git repository to colorize by the six git-derived dimensions). The treemap is colorized by file type instead.</p>\n");
        }

        sb.Append("    <div class=\"codemap-viewport\">\n");
        sb.Append(Charts.CodeTreemap(variant.Layout, CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics, fileHref, prefix));
        sb.Append("    </div>\n");
        sb.Append("  </section>\n\n");

        AppendFileTable(sb, files, hasMetrics, fileHref, prefix);

        sb.Append("</div>\n\n");
    }

    /// <summary>The dimension-switch control — a dropdown, keyboard-operable, present whenever the variant has
    /// files (Story 7.9 loosened this from "only when git metrics exist" — file type needs no git data). Emitted
    /// <c>hidden</c>; the enhancement script reveals it (scoped to this panel) and re-fills the rects on change.
    /// With JS off the treemap keeps its server-baked default, so an inert control never shows. When
    /// <paramref name="hasMetrics"/> is true, "File type" is a 7th option appended after the six unchanged
    /// git-derived ones (unchanged baked default: change frequency); when false, it's the ONLY option and the
    /// baked default. A <c>&lt;select&gt;</c> rather than a radio group (round 2) — reads better as one compact
    /// dropdown than a many-item radio list. [Subtask 5.2; Story 7.9]</summary>
    private static void AppendColorizeControls(StringBuilder sb, bool hasMetrics)
    {
        sb.Append("    <div class=\"codemap-controls\" hidden>\n");
        sb.Append("      <label class=\"codemap-controls-label\">Colorize by\n");
        sb.Append("        <select class=\"codemap-dim-select\" aria-label=\"Colorize the treemap by\">\n");
        if (hasMetrics)
        {
            AppendOption(sb, "changes", "Change frequency", true);
            AppendOption(sb, "last", "Recently changed", false);
            AppendOption(sb, "created", "First changed", false);
            AppendOption(sb, "avgchange", "Avg change size", false);
            AppendOption(sb, "churn", "Churn", false);
            AppendOption(sb, "cochange", "Files changed together", false);
            AppendOption(sb, "filetype", "File type", false);
        }
        else
        {
            AppendOption(sb, "filetype", "File type", true);
        }
        sb.Append("        </select>\n");
        sb.Append("      </label>\n");
        sb.Append("    </div>\n");
    }

    private static void AppendOption(StringBuilder sb, string value, string label, bool selectedOption)
    {
        var sel = selectedOption ? " selected" : string.Empty;
        sb.Append($"          <option value=\"{value}\"{sel}>{PathUtil.Html(label)}</option>\n");
    }

    /// <summary>The sequential-ramp legend ("Less … More") — reuses the commit-heatmap ramp levels (a non-
    /// <c>--status-*</c> scale). Server-baked visible only when it explains the baked-in default (git metrics
    /// present); otherwise pre-rendered <c>hidden</c> so the client-side dimension switch can reveal it without a
    /// DOM rewrite when the user picks a numeric dimension from the dropdown. [Subtask 4.3; Story 7.9]</summary>
    private static void AppendLegend(StringBuilder sb, bool hasMetrics)
    {
        sb.Append("    <div class=\"codemap-legend codemap-legend-ramp\"").Append(hasMetrics ? "" : " hidden").Append(">");
        sb.Append("<span class=\"codemap-legend-dim\">Colorized by change frequency</span> ");
        sb.Append("<span aria-hidden=\"true\">Less ");
        for (var l = 0; l <= 4; l++)
        {
            sb.Append($"<span class=\"codemap-legend-swatch level-{l}\"></span>");
        }
        sb.Append(" More</span></div>\n");
    }

    /// <summary>The discrete (categorical) legend for the "File type" dimension — a swatch + human label per
    /// category actually present in this variant's file set (never every possible category, so a repo with no
    /// config files doesn't show an unused "Config &amp; Data" swatch). Pre-rendered alongside
    /// <see cref="AppendLegend"/>: whichever legend explains the currently-baked default ships visible, the other
    /// <c>hidden</c>, and the client-side dimension switch simply toggles which one is shown rather than rewriting
    /// either one's content (both are static once rendered — this variant's category set never changes at
    /// runtime). [Story 7.9]</summary>
    private static void AppendDiscreteLegend(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics)
    {
        var present = CodeFileType.AllCategories.Where(cat => files.Any(f => f.Category == cat)).ToList();

        sb.Append("    <div class=\"codemap-legend codemap-legend-discrete\"").Append(hasMetrics ? " hidden" : "").Append(">");
        sb.Append("<span class=\"codemap-legend-dim\">Colorized by file type</span> ");
        foreach (var cat in present)
        {
            sb.Append($"<span class=\"codemap-legend-swatch type-{cat.Key}\"></span>");
            sb.Append($"<span class=\"codemap-legend-label\">{PathUtil.Html(cat.Label)}</span> ");
        }
        sb.Append("</div>\n");
    }

    /// <summary>The text-equivalent table — the no-JS truth of the visualization and the screen-reader listing:
    /// every file with its path, line count, and (when present) git metrics as TEXT, so color is never the sole
    /// signal (AC #4). Ordered by the default dimension (change frequency) descending, then lines, so the reading
    /// order is meaningful. Each path links to its in-portal code page when the guarded resolver supplies one.
    /// [Subtask 4.6]
    /// <para><b>Story 10.8 scope:</b> stays a genuine <c>&lt;table&gt;</c> (Design Direction #5) — its multi-column
    /// numeric header row is load-bearing for the accessible/no-JS reading of the treemap, and files carry no
    /// lifecycle status, so there is no badge to route through the shared row primitive. Only a badge-bearing
    /// row family gets rewired onto <see cref="ListRow"/>.</para></summary>
    private static void AppendFileTable(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics, Func<string, string?>? fileHref, string prefix)
    {
        var ordered = files
            .OrderByDescending(f => f.Metrics?.Changes ?? -1)
            .ThenByDescending(f => f.Lines)
            .ThenBy(f => f.RepoRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        sb.Append("    <section class=\"chart-panel\">\n");
        sb.Append("      <h3>All files</h3>\n");
        sb.Append($"      <p class=\"chart-lead\">Every file in the treemap, listed as text{(hasMetrics ? ", ordered by change frequency" : ", ordered by size")}.</p>\n");
        sb.Append("      <table class=\"codemap-table\">\n");
        sb.Append("        <thead><tr><th scope=\"col\">File</th><th scope=\"col\" class=\"num\">Lines</th><th scope=\"col\">Type</th>");
        if (hasMetrics)
        {
            sb.Append("<th scope=\"col\" class=\"num\">Changes</th><th scope=\"col\" class=\"num\">Churn</th><th scope=\"col\" class=\"num\">Avg</th><th scope=\"col\" class=\"num\">Together</th><th scope=\"col\">First</th><th scope=\"col\">Last</th>");
        }
        sb.Append("</tr></thead>\n        <tbody>\n");

        foreach (var file in ordered)
        {
            var href = fileHref?.Invoke(file.RepoRelativePath);
            var pathCell = href is { Length: > 0 } target
                ? $"<a href=\"{PathUtil.Html(prefix + target)}\">{PathUtil.Html(file.RepoRelativePath)}</a>"
                : PathUtil.Html(file.RepoRelativePath);

            sb.Append("          <tr><th scope=\"row\">").Append(pathCell).Append("</th>");
            sb.Append($"<td class=\"num\">{file.Lines.ToString("N0", CultureInfo.InvariantCulture)}</td>");
            // Always present, independent of hasMetrics — the categorical dimension's text equivalent. [Story 7.9]
            sb.Append($"<td>{PathUtil.Html((file.Category ?? CodeFileType.Other).Label)}</td>");
            if (hasMetrics)
            {
                if (file.Metrics is { } m)
                {
                    var avg = m.Changes > 0 ? ((double)m.TotalChurn / m.Changes).ToString("N0", CultureInfo.InvariantCulture) : "—";
                    var together = m.AvgCoChanged is { } co ? co.ToString("N1", CultureInfo.InvariantCulture) : "—";
                    var first = m.FirstDate is { } fd ? PortalDates.Day(fd) : "—";
                    var last = m.LastDate is { } ld ? PortalDates.Day(ld) : "—";
                    sb.Append($"<td class=\"num\">{m.Changes.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{avg}</td>");
                    sb.Append($"<td class=\"num\">{together}</td>");
                    sb.Append($"<td>{first}</td><td>{last}</td>");
                }
                else
                {
                    sb.Append("<td class=\"num\">—</td><td class=\"num\">—</td><td class=\"num\">—</td><td class=\"num\">—</td><td>—</td><td>—</td>");
                }
            }
            sb.Append("</tr>\n");
        }

        sb.Append("        </tbody>\n      </table>\n");
        sb.Append("    </section>\n\n");
    }
}
