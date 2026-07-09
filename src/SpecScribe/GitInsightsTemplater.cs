using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the opt-in aggregate <c>git-insights.html</c> hub (FR-10) — the "click in to see more"
/// destination behind the dashboard's Git Pulse panel: file change frequency + churn, activity over time
/// (the reused commit heatmap, whose active days already link to their per-day pages), and contributor
/// attribution. A synthesized page (no markdown source), so it builds its own shell the way
/// <see cref="CommitDayTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
/// <para>Progressive enhancement contract (NFR-5): every table is complete, escaped, and server-sorted at
/// generation time — the no-JS reading is the primary artifact. The one sanctioned script upgrades tables
/// marked <c>js-sortable</c> with client-side sort/filter over the already-present rows; nothing here depends
/// on it. Outgoing detail links are guarded on target existence via the <c>fileHref</c>/<c>commitHref</c>
/// resolvers (Stories 7.1/7.4/7.5 seams): no resolver or no target → plain escaped text, never a dead link.
/// Contributors are attribution ("who has been active where"), never a ranked leaderboard (PRD boundary).
/// [Story 3.8]</para></summary>
public static class GitInsightsTemplater
{
    public static string RenderPage(
        GitInsightsData insights,
        GitPulse? git,
        SiteNav nav,
        Func<string, string?>? fileHref = null,
        Func<string, string?>? commitHref = null)
    {
        var outputPath = SiteNav.GitInsightsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — git-insights.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Git Insights — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Aggregate git insights for {nav.SiteTitle}: file change frequency, activity over time, and contributor attribution."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Git Insights", null),
        }));

        sb.Append("<main id=\"main-content\" class=\"deep-page git-insights\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Git Insights &middot; opt-in</div>\n");
        sb.Append("  <h1>Git Insights</h1>\n");
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill\">{N(insights.CommitCount)} {Charts.Plural(insights.CommitCount, "commit", "commits")} analyzed</span>\n");
        sb.Append($"    <span class=\"pill\">{N(insights.Files.Count)} {Charts.Plural(insights.Files.Count, "file", "files")}</span>\n");
        sb.Append($"    <span class=\"pill\">{N(insights.Contributors.Count)} {Charts.Plural(insights.Contributors.Count, "contributor", "contributors")}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        AppendFileFrequencySection(sb, insights.Files, fileHref);
        AppendActivitySection(sb, insights, git);
        AppendContributorSection(sb, insights.Contributors, commitHref);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>File change frequency + churn, server-sorted (change count desc, ordinal path tie-break —
    /// the order <see cref="GitMetrics.BuildInsights"/> already emitted). File cells link to their in-portal
    /// detail page only when the resolver produces one.</summary>
    private static void AppendFileFrequencySection(
        StringBuilder sb, IReadOnlyList<FileChangeStat> files, Func<string, string?>? fileHref)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <h2>File Change Frequency</h2>\n");
        sb.Append("  <p class=\"deep-page-lead\">The files that changed most often in the analyzed window, with their total line churn — the parts of the codebase carrying the most recent activity.</p>\n");
        sb.Append("  <div class=\"chart-panel\">\n");

        if (files.Count == 0)
        {
            sb.Append("    <div class=\"chart-empty\">No file change data available.</div>\n");
        }
        else
        {
            sb.Append("    <div class=\"table-scroll\">\n");
            sb.Append("    <table class=\"gi-table js-sortable\" data-filter-label=\"Filter files\">\n");
            sb.Append("      <caption>Files by change frequency — sorted by number of commits touching each file, most-changed first.</caption>\n");
            sb.Append("      <thead>\n        <tr>\n");
            sb.Append("          <th scope=\"col\">File</th>\n");
            sb.Append("          <th scope=\"col\" class=\"gi-num\" data-sort=\"num\" aria-sort=\"descending\">Changes</th>\n");
            sb.Append("          <th scope=\"col\" class=\"gi-num\" data-sort=\"num\">Lines added</th>\n");
            sb.Append("          <th scope=\"col\" class=\"gi-num\" data-sort=\"num\">Lines deleted</th>\n");
            sb.Append("        </tr>\n      </thead>\n      <tbody>\n");
            foreach (var file in files)
            {
                sb.Append("        <tr>\n");
                sb.Append($"          <td class=\"gi-file\">{GuardedLink(file.Path, fileHref?.Invoke(file.Path), code: true)}</td>\n");
                sb.Append($"          <td class=\"gi-num\">{N(file.Changes)}</td>\n");
                sb.Append($"          <td class=\"gi-num\">{N(file.LinesAdded)}</td>\n");
                sb.Append($"          <td class=\"gi-num\">{N(file.LinesDeleted)}</td>\n");
                sb.Append("        </tr>\n");
            }
            sb.Append("      </tbody>\n    </table>\n");
            sb.Append("    </div>\n");
        }

        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>Activity over time — the existing accessible commit heatmap, reused rather than a parallel
    /// time chart. Its active-day cells already link to the generated <c>commits/{date}.html</c> pages (and
    /// this page sits at the output root, the same place the heatmap's root-relative hrefs assume), so the
    /// "select an entry → navigate to detail" contract holds with zero new link plumbing. The headline
    /// figures come from the deep window's activity series so the text matches the analyzed commits.</summary>
    private static void AppendActivitySection(StringBuilder sb, GitInsightsData insights, GitPulse? git)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <h2>Activity Over Time</h2>\n");
        var windowDays = insights.Activity.Count;
        var windowCommits = insights.Activity.Sum(a => a.Count);
        sb.Append($"  <p class=\"deep-page-lead\">{N(windowCommits)} {Charts.Plural(windowCommits, "commit", "commits")} across {N(windowDays)} active {Charts.Plural(windowDays, "day", "days")} in the analyzed window. Each active day in the heatmap links to that day's commit log.</p>\n");
        sb.Append("  <div class=\"chart-panel\">\n");
        if (git is not null && git.DailySeries.Count > 0)
        {
            sb.Append(Charts.CommitHeatmap(git.DailySeries, git.CommitsByDay));
        }
        else
        {
            sb.Append("    <div class=\"chart-empty\">No activity data available.</div>\n");
        }
        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>Contributor attribution — who has been active where, as collaboration context. Deliberately
    /// framed as attribution counts and NOT a leaderboard/productivity score (the amended PRD boundary):
    /// no rank column, no "top performer" copy. The latest-commit cell links to its per-commit detail page
    /// only when the resolver produces one (Story 7.5 seam).</summary>
    private static void AppendContributorSection(
        StringBuilder sb, IReadOnlyList<ContributorStat> contributors, Func<string, string?>? commitHref)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <h2>Contributor Attribution</h2>\n");
        sb.Append("  <p class=\"deep-page-lead\">Who has been active where in the analyzed window — collaboration context for \"who knows this area,\" not a scoreboard.</p>\n");
        sb.Append("  <div class=\"chart-panel\">\n");

        if (contributors.Count == 0)
        {
            sb.Append("    <div class=\"chart-empty\">No contributor data available.</div>\n");
        }
        else
        {
            sb.Append("    <div class=\"table-scroll\">\n");
            sb.Append("    <table class=\"gi-table js-sortable\" data-filter-label=\"Filter contributors\">\n");
            sb.Append("      <caption>Contributor attribution — commit and file counts per author in the analyzed window.</caption>\n");
            sb.Append("      <thead>\n        <tr>\n");
            sb.Append("          <th scope=\"col\">Contributor</th>\n");
            sb.Append("          <th scope=\"col\" class=\"gi-num\" data-sort=\"num\" aria-sort=\"descending\">Commits</th>\n");
            sb.Append("          <th scope=\"col\" class=\"gi-num\" data-sort=\"num\">Files touched</th>\n");
            sb.Append("          <th scope=\"col\">Last active</th>\n");
            sb.Append("          <th scope=\"col\">Latest commit</th>\n");
            sb.Append("        </tr>\n      </thead>\n      <tbody>\n");
            foreach (var contributor in contributors)
            {
                var lastActive = contributor.LastCommitDate is { } d ? Charts.DReadable(d) : "—";
                var lastActiveSort = contributor.LastCommitDate is { } iso ? Charts.D(iso) : string.Empty;
                var shortHash = contributor.LatestHash.Length > 7 ? contributor.LatestHash[..7] : contributor.LatestHash;
                sb.Append("        <tr>\n");
                sb.Append($"          <th scope=\"row\" class=\"gi-contributor\">{PathUtil.Html(contributor.Name)}</th>\n");
                sb.Append($"          <td class=\"gi-num\">{N(contributor.Commits)}</td>\n");
                sb.Append($"          <td class=\"gi-num\">{N(contributor.FilesTouched)}</td>\n");
                sb.Append($"          <td data-sort-value=\"{PathUtil.Html(lastActiveSort)}\">{PathUtil.Html(lastActive)}</td>\n");
                sb.Append($"          <td>{GuardedLink(shortHash, commitHref?.Invoke(contributor.LatestHash), code: true)}</td>\n");
                sb.Append("        </tr>\n");
            }
            sb.Append("      </tbody>\n    </table>\n");
            sb.Append("    </div>\n");
        }

        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>The guard-all-links-on-target-availability discipline: a resolved href renders a real link,
    /// no href renders the same text plain (escaped either way) — never a dead link while the detail-page
    /// stories (7.1/7.4/7.5) are unmerged.</summary>
    private static string GuardedLink(string text, string? href, bool code)
    {
        var escaped = PathUtil.Html(text);
        var inner = code ? $"<code>{escaped}</code>" : escaped;
        return href is { Length: > 0 } ? $"<a href=\"{PathUtil.Html(href)}\">{inner}</a>" : inner;
    }

    /// <summary>Invariant integer formatting — derived numbers must read identically regardless of host
    /// culture (the same invariant-formatting discipline the date helpers in <see cref="Charts"/> follow).</summary>
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
