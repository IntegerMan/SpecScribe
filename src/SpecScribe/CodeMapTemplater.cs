using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>code-map.html</c> page — the source-code treemap surface (Story 7.6, FR14).
/// Reuses the same page shell every <c>Write*</c> page uses (<see cref="PathUtil.RenderHeadOpen"/> + nav +
/// breadcrumb + <c>&lt;main id="main-content"&gt;</c> + footer); the treemap SVG itself comes from the pure, server-
/// computed <see cref="Charts.CodeTreemap"/>. The page is fully correct with JavaScript OFF: a server-rendered
/// treemap sized-by-LOC with the default colorize dimension baked in, a legend, the "git data unavailable" notice
/// when applicable, and a complete text-equivalent table of every file and its metrics. The scoped JS enhancement
/// (dimension-switch + directory zoom) only adds interactivity — the colorize controls and the drill breadcrumb are
/// emitted <c>hidden</c> and revealed by that script, so a no-JS visitor never sees an inert control. Replaced the
/// retired Story 3.4 structure-tree page. [Story 7.6]</summary>
public static class CodeMapTemplater
{
    /// <summary>Renders the whole page. <paramref name="fileHref"/> is the guarded in-portal code-page resolver
    /// (Story 7.1): a non-null return routes a file to its code page, a null return (or a null resolver) leaves it a
    /// plain, focusable rect — never a broken link. It is <c>null</c> today (7.1 not yet on <c>main</c>); the seam is
    /// wired but dormant.</summary>
    public static string RenderPage(CodeMap map, IReadOnlyList<TreemapRect> layout, SiteNav nav, Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.CodeMapOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — code-map.html is at the output root.

        var files = map.Files();
        var hasMetrics = files.Any(f => f.Metrics is not null);

        var fileWord = Charts.Plural(map.FileCount, "file", "files");
        var dirWord = Charts.Plural(map.DirectoryCount, "directory", "directories");
        var lineWord = Charts.Plural((int)Math.Min(map.TotalLines, int.MaxValue), "line", "lines");
        var headline = $"{map.FileCount:N0} {fileWord} across {map.DirectoryCount:N0} {dirWord} \u00b7 {map.TotalLines:N0} {lineWord} of code";

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Code Map — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Source-code treemap for {nav.SiteTitle} — every file sized by its lines of code and colorable by git-derived change activity, with a full text-equivalent listing."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Code Map", null) }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Code Map</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {PathUtil.Html(headline)}</p>\n\n");

        sb.Append("<section class=\"chart-panel codemap-panel\">\n");
        sb.Append("  <h3>Source Code Treemap</h3>\n");
        sb.Append("  <p class=\"chart-lead\">Each rectangle is a file, sized by its lines of code and nested inside its directory. ");
        if (hasMetrics)
        {
            sb.Append("Color shows change activity across recent history — darker means more. Use the controls to recolor by another dimension, and select a directory to zoom in.</p>\n");
        }
        else
        {
            sb.Append("Files are shown at a neutral fill because git change data isn't available for this run.</p>\n");
        }

        // The drill breadcrumb is JS-driven (zoom requires script); emit it hidden so a no-JS visitor never sees an
        // inert control. The enhancement script reveals it on init.
        sb.Append("  <nav class=\"codemap-drill\" aria-label=\"Treemap zoom\" hidden>\n");
        sb.Append("    <ol class=\"codemap-breadcrumb\" id=\"codemap-breadcrumb\">\n");
        sb.Append("      <li><button type=\"button\" class=\"codemap-crumb\" data-path=\"\" aria-current=\"true\">All files</button></li>\n");
        sb.Append("    </ol>\n");
        sb.Append("  </nav>\n");

        if (hasMetrics)
        {
            AppendColorizeControls(sb);
            AppendLegend(sb);
        }
        else
        {
            sb.Append("  <p class=\"codemap-notice\" role=\"note\">Git change data is unavailable (run with <code>--deep-git</code> in a git repository to colorize by change activity). The treemap is still sized by lines of code.</p>\n");
        }

        sb.Append("  <div class=\"codemap-viewport\">\n");
        sb.Append(Charts.CodeTreemap(layout, CodeMap.DefaultWidth, CodeMap.DefaultHeight, hasMetrics, fileHref));
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");

        AppendFileTable(sb, files, hasMetrics, fileHref, prefix);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The dimension-switch control (radio group), keyboard-operable and present only when git metrics
    /// exist. Emitted <c>hidden</c>; the enhancement script reveals it and re-fills the rects on change. With JS off
    /// the treemap keeps its baked-in default (change frequency), so an inert control never shows. [Subtask 5.2]</summary>
    private static void AppendColorizeControls(StringBuilder sb)
    {
        sb.Append("  <form class=\"codemap-controls\" id=\"codemap-controls\" aria-label=\"Colorize the treemap by\" hidden>\n");
        sb.Append("    <span class=\"codemap-controls-label\">Colorize by</span>\n");
        AppendRadio(sb, "changes", "Change frequency", true);
        AppendRadio(sb, "last", "Recently changed", false);
        AppendRadio(sb, "created", "First changed", false);
        AppendRadio(sb, "avgchange", "Avg change size", false);
        sb.Append("  </form>\n");
    }

    private static void AppendRadio(StringBuilder sb, string value, string label, bool checkedRadio)
    {
        var id = $"codemap-dim-{value}";
        var check = checkedRadio ? " checked" : string.Empty;
        sb.Append($"    <label class=\"codemap-radio\"><input type=\"radio\" name=\"codemap-dim\" id=\"{id}\" value=\"{value}\"{check}> {PathUtil.Html(label)}</label>\n");
    }

    /// <summary>The sequential-ramp legend ("Less … More") — always visible (it explains the baked-in default
    /// colors), reusing the commit-heatmap ramp levels (a non-<c>--status-*</c> scale). [Subtask 4.3]</summary>
    private static void AppendLegend(StringBuilder sb)
    {
        sb.Append("  <div class=\"codemap-legend\" aria-hidden=\"true\">Less ");
        for (var l = 0; l <= 4; l++)
        {
            sb.Append($"<span class=\"codemap-legend-swatch level-{l}\"></span>");
        }
        sb.Append(" More</div>\n");
    }

    /// <summary>The text-equivalent table — the no-JS truth of the visualization and the screen-reader listing:
    /// every file with its path, line count, and (when present) git metrics as TEXT, so color is never the sole
    /// signal (AC #4). Ordered by the default dimension (change frequency) descending, then lines, so the reading
    /// order is meaningful. Each path links to its in-portal code page when the guarded resolver supplies one.
    /// [Subtask 4.6]</summary>
    private static void AppendFileTable(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics, Func<string, string?>? fileHref, string prefix)
    {
        var ordered = files
            .OrderByDescending(f => f.Metrics?.Changes ?? -1)
            .ThenByDescending(f => f.Lines)
            .ThenBy(f => f.RepoRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        sb.Append("<section class=\"chart-panel\">\n");
        sb.Append("  <h3>All files</h3>\n");
        sb.Append($"  <p class=\"chart-lead\">Every file in the treemap, listed as text{(hasMetrics ? ", ordered by change frequency" : ", ordered by size")}.</p>\n");
        sb.Append("  <table class=\"codemap-table\">\n");
        sb.Append("    <thead><tr><th scope=\"col\">File</th><th scope=\"col\" class=\"num\">Lines</th>");
        if (hasMetrics)
        {
            sb.Append("<th scope=\"col\" class=\"num\">Changes</th><th scope=\"col\" class=\"num\">Churn</th><th scope=\"col\" class=\"num\">Avg</th><th scope=\"col\">First</th><th scope=\"col\">Last</th>");
        }
        sb.Append("</tr></thead>\n    <tbody>\n");

        foreach (var file in ordered)
        {
            var href = fileHref?.Invoke(file.RepoRelativePath);
            var pathCell = href is { Length: > 0 } target
                ? $"<a href=\"{PathUtil.Html(prefix + target)}\">{PathUtil.Html(file.RepoRelativePath)}</a>"
                : PathUtil.Html(file.RepoRelativePath);

            sb.Append("      <tr><th scope=\"row\">").Append(pathCell).Append("</th>");
            sb.Append($"<td class=\"num\">{file.Lines.ToString("N0", CultureInfo.InvariantCulture)}</td>");
            if (hasMetrics)
            {
                if (file.Metrics is { } m)
                {
                    var avg = m.Changes > 0 ? ((double)m.TotalChurn / m.Changes).ToString("N0", CultureInfo.InvariantCulture) : "\u2014";
                    var first = m.FirstDate is { } fd ? Charts.D(fd) : "\u2014";
                    var last = m.LastDate is { } ld ? Charts.D(ld) : "\u2014";
                    sb.Append($"<td class=\"num\">{m.Changes.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{avg}</td>");
                    sb.Append($"<td>{first}</td><td>{last}</td>");
                }
                else
                {
                    sb.Append("<td class=\"num\">\u2014</td><td class=\"num\">\u2014</td><td class=\"num\">\u2014</td><td>\u2014</td><td>\u2014</td>");
                }
            }
            sb.Append("</tr>\n");
        }

        sb.Append("    </tbody>\n  </table>\n");
        sb.Append("</section>\n\n");
    }
}
