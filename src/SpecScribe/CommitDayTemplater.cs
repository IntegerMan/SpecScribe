using System.Text;

namespace SpecScribe;

/// <summary>Renders one "day overview" page — a static <c>commits/{yyyy-MM-dd}.html</c> listing every commit
/// that landed on that day (short hash, author-local time, author, subject), with previous/next links to the
/// adjacent active days. A synthesized page (no markdown source), so it builds its own shell the way
/// <see cref="RequirementsTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
/// The heatmap cell links here; this is the durable route future epics can enrich.</summary>
public static class CommitDayTemplater
{
    public static string RenderPage(
        DateOnly day,
        IReadOnlyList<CommitInfo> commits,
        DateOnly? prevDay,
        DateOnly? nextDay,
        SiteNav nav)
    {
        var readable = Charts.DReadable(day);
        var outputPath = $"commits/{Charts.D(day)}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Commits on {readable} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"The {commits.Count} commit{(commits.Count == 1 ? string.Empty : "s")} that landed on {readable} in {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ($"Commits on {readable}", null),
        }));

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Commit Activity</div>\n");
        sb.Append($"  <h1>Commits on {PathUtil.Html(readable)}</h1>\n");
        sb.Append($"  <div class=\"meta-pills\"><span class=\"pill\">{commits.Count} {Charts.Plural(commits.Count, "commit", "commits")}</span></div>\n");
        sb.Append("</header>\n\n");

        // Commits list newest-first (git log order). Each row: short hash, author-local time, author, subject.
        sb.Append("<article class=\"doc-body\">\n");
        sb.Append("<ul class=\"commit-day-list\">\n");
        foreach (var commit in commits)
        {
            sb.Append("  <li class=\"commit-day-item\">\n");
            sb.Append($"    <code class=\"commit-hash\">{PathUtil.Html(commit.ShortHash)}</code>\n");
            sb.Append($"    <span class=\"commit-time\">{PathUtil.Html(commit.Time)}</span>\n");
            sb.Append($"    <span class=\"commit-author\">{PathUtil.Html(commit.Author)}</span>\n");
            sb.Append($"    <span class=\"commit-subject\">{PathUtil.Html(commit.Subject)}</span>\n");
            sb.Append("  </li>\n");
        }
        sb.Append("</ul>\n");
        sb.Append("</article>\n\n");

        // Prev/next hop across active days only (sibling pages in this same commits/ dir); omitted at the ends.
        if (prevDay is not null || nextDay is not null)
        {
            sb.Append("<nav class=\"commit-day-nav\" aria-label=\"Adjacent active days\">\n");
            if (prevDay is { } p)
            {
                sb.Append($"  <a class=\"commit-day-prev\" href=\"{Charts.D(p)}.html\">&laquo; {PathUtil.Html(Charts.DReadable(p))}</a>\n");
            }
            if (nextDay is { } n)
            {
                sb.Append($"  <a class=\"commit-day-next\" href=\"{Charts.D(n)}.html\">{PathUtil.Html(Charts.DReadable(n))} &raquo;</a>\n");
            }
            sb.Append("</nav>\n\n");
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}", prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
