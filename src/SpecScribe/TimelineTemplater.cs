using System.Text;

namespace SpecScribe;

/// <summary>Renders the chronological activity timeline (<c>timeline.html</c>, root-level): the reused
/// <see cref="Charts.CommitHeatmap"/> "activity over time" visual (when git history exists) over a newest-first
/// list of active dates, each linking to its generated date page (<c>commits/{date}.html</c>) with a compact
/// "N commits · M artifacts updated" summary. A synthesized page (no markdown source) that builds its own shell
/// like <see cref="CommitDayTemplater"/> — pure inline SVG + native links/lists, no JavaScript. The day set is
/// the union computed by <see cref="ActivityModel.UnionDays"/>, the same set the date pages are generated from,
/// so no row can ever link a day that has no page. Degrades gracefully: git absent → no heatmap but the
/// artifact-driven list still renders; empty union → a friendly note. [Story 7.3]</summary>
public static class TimelineTemplater
{
    public static string RenderPage(
        GitPulse? git,
        IReadOnlyList<DateOnly> daysNewestFirst,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> commitsByDay,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>> artifactsByDay,
        SiteNav nav)
    {
        var outputPath = SiteNav.TimelineOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Activity Timeline — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Activity timeline for {nav.SiteTitle}: commits and artifact changes over time, with a page for each active date."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Timeline", null),
        }));

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Activity</div>\n");
        sb.Append("  <h1>Activity Timeline</h1>\n");
        sb.Append("</header>\n\n");

        sb.Append("<article class=\"doc-body\">\n");

        // Activity-over-time heatmap — reused verbatim from the dashboard. Only when git history exists; the
        // timeline still renders its artifact-driven list without it (AC #2 graceful degradation).
        if (git is { DailySeries.Count: > 0 })
        {
            sb.Append("<div class=\"chart-panel timeline-heatmap\">\n");
            sb.Append(Charts.CommitHeatmap(git.DailySeries, git.CommitsByDay));
            sb.Append(Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.ActivityCadence)));
            sb.Append("</div>\n");
        }

        if (daysNewestFirst.Count == 0)
        {
            sb.Append("<p class=\"timeline-empty\">No activity to show yet.</p>\n");
        }
        else
        {
            sb.Append("<ol class=\"timeline-list\">\n");
            foreach (var day in daysNewestFirst)
            {
                var commitCount = commitsByDay.TryGetValue(day, out var c) ? c.Count : 0;
                var artifactCount = artifactsByDay.TryGetValue(day, out var a) ? a.Count : 0;

                var parts = new List<string>(2);
                if (commitCount > 0)
                {
                    parts.Add($"{commitCount} {Charts.Plural(commitCount, "commit", "commits")}");
                }
                if (artifactCount > 0)
                {
                    parts.Add($"{artifactCount} {Charts.Plural(artifactCount, "artifact updated", "artifacts updated")}");
                }
                // Each part is escaped, then joined with a literal &middot; (matching the heatmap headline's
                // separator convention) — never a raw non-ASCII char run through the HTML encoder.
                var summary = string.Join(" &middot; ", parts.Select(PathUtil.Html));

                // Story 10.8: shares the list-row visual grammar (background/spacing/meta cluster) with every
                // other index via the CSS the ListRow primitive defines, while keeping the exact `timeline-row`/
                // `timeline-date`/`timeline-summary` class names and structure the existing suite pins — commits
                // carry no lifecycle status, so this is the badge-less path (summary + metadata chip + link only).
                sb.Append("  <li class=\"timeline-row\">\n");
                sb.Append("    <div class=\"list-row-scan\">\n");
                sb.Append($"      <a class=\"timeline-date\" href=\"commits/{Charts.D(day)}.html\">{PathUtil.Html(Charts.DReadable(day))}</a>\n");
                sb.Append("      <div class=\"list-row-meta\">\n");
                sb.Append($"        <span class=\"timeline-summary\">{summary}</span>\n");
                sb.Append("      </div>\n");
                sb.Append("    </div>\n");
                sb.Append("  </li>\n");
            }
            sb.Append("</ol>\n");
        }

        sb.Append("</article>\n\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
