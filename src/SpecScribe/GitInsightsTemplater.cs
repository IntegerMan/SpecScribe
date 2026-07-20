using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the opt-in aggregate <c>git-insights.html</c> hub (FR-10) — the "click in to see more"
/// destination behind the dashboard's Git Pulse panel. Two sections: a files-and-contributors master-detail
/// (a file change-frequency table on the left; selecting a file reveals who has been working on it on the
/// right — answering "who do I talk to about this file?"), and activity over time (the reused commit heatmap,
/// whose active days already link to their per-day pages). A synthesized page (no markdown source), so it
/// builds its own shell the way <see cref="CommitDayTemplater"/> does rather than going through
/// <see cref="HtmlTemplater.RenderPage"/>.
/// <para>Progressive enhancement contract (NFR-5): everything reads and works with JS off. The file table is
/// complete, escaped, and server-sorted at generation time; the file→contributors drill-down is pure-CSS
/// <c>:target</c> (the same mechanism the commit heatmap and coupling graph use — no JS needed). The one
/// sanctioned script only upgrades the <c>js-sortable</c> table with client-side sort/filter over the
/// already-present rows. Contributors are scoped per file (collaboration context), never a global ranked
/// scoreboard (the PRD boundary). Outgoing detail links are guarded on target existence via the
/// <c>fileHref</c>/<c>commitHref</c> resolvers (Stories 7.1/7.4/7.5 seams): no resolver or no target → plain
/// escaped text, never a dead link. [Story 3.8]</para></summary>
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
            $"Aggregate git insights for {nav.SiteTitle}: file change frequency, who works on each file, and activity over time."));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildInsightsLocalContext(outputPath)));
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
        var filesLabel = insights.TotalFilesTouched > insights.Files.Count
            ? TruncatedFilesRankingFact(insights.Files.Count, insights.TotalFilesTouched, capitalized: false)
            : $"{N(insights.Files.Count)} {Charts.Plural(insights.Files.Count, "file", "files")}";
        sb.Append($"    <span class=\"pill\">{filesLabel}</span>\n");
        sb.Append($"    <span class=\"pill\">{N(insights.ContributorCount)} {Charts.Plural(insights.ContributorCount, "contributor", "contributors")}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        AppendFilesAndContributorsSection(sb, insights, fileHref, commitHref);
        AppendActivitySection(sb, insights, git);

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The files-and-contributors master-detail. Left: the change-frequency table (server-sorted,
    /// change-count desc with an ordinal path tie-break — the order <see cref="GitMetrics.BuildInsights"/>
    /// already emitted, and the no-JS reading order). Each file name is a pure-CSS <c>:target</c> link to its
    /// contributor panel on the right, so "select a file → see who works on it" needs no JS. Right: one
    /// contributor panel per file (all present in the HTML; CSS reveals the targeted one) plus a default
    /// prompt when nothing is selected. Answers "who do I talk to about this file?" rather than presenting a
    /// global people ranking. Window/ranking/why chrome comes from the shared <see cref="Charts"/> frame slots
    /// (Story 10.2).</summary>
    private static void AppendFilesAndContributorsSection(
        StringBuilder sb,
        GitInsightsData insights,
        Func<string, string?>? fileHref,
        Func<string, string?>? commitHref)
    {
        var files = insights.Files;
        var window = insights.CommitCount > 0
            ? $"Last {N(insights.CommitCount)} commits"
            : null;
        var ranking = files.Count == 0
            ? null
            : insights.TotalFilesTouched > files.Count
                ? TruncatedFilesRankingFact(files.Count, insights.TotalFilesTouched, capitalized: true)
                : $"Top {N(files.Count)} files by commit count";

        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <div class=\"chart-frame-head\"><h2>Files &amp; Contributors</h2>");
        if (!string.IsNullOrEmpty(window)) sb.Append(Charts.FrameWindowSlot(window));
        sb.Append("</div>\n");
        sb.Append(Charts.FrameRankingSlot(ranking));
        sb.Append(Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.FileChurn)));

        if (files.Count == 0)
        {
            sb.Append("  <div class=\"chart-panel\"><div class=\"chart-empty\">No file change data available.</div></div>\n");
            sb.Append("</section>\n\n");
            return;
        }

        sb.Append("  <div class=\"gi-master-detail\">\n");

        // Master: the file change-frequency table.
        sb.Append("    <div class=\"gi-master chart-panel\">\n");
        sb.Append("      <div class=\"table-scroll\">\n");
        sb.Append("      <table class=\"gi-table js-sortable\" data-filter-label=\"Filter files\">\n");
        sb.Append("        <caption>Files by change frequency — sorted by number of commits touching each file, most-changed first. Select a file for its contributors.</caption>\n");
        sb.Append("        <thead>\n          <tr>\n");
        sb.Append("            <th scope=\"col\">File</th>\n");
        sb.Append("            <th scope=\"col\" class=\"gi-num\" data-sort=\"num\" aria-sort=\"descending\">Changes</th>\n");
        sb.Append("            <th scope=\"col\" class=\"gi-num\" data-sort=\"num\">Lines added</th>\n");
        sb.Append("            <th scope=\"col\" class=\"gi-num\" data-sort=\"num\">Lines deleted</th>\n");
        sb.Append("          </tr>\n        </thead>\n        <tbody>\n");
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var pathHtml = PathUtil.Html(file.Path);
            sb.Append("          <tr>\n");
            // The whole row is the select target (stretched-link pattern): this invisible anchor's ::after
            // covers the row, so a click anywhere on it reveals this file's contributors via :target — no JS.
            // The file NAME is kept separate so it can be reserved for navigating to the file's own page
            // (Story 7.1/7.4): when a resolver gives it a target it renders as a real link that sits above the
            // row overlay (navigates); until then it is plain text and the row overlay's click selects.
            sb.Append("            <td class=\"gi-file\">");
            sb.Append($"<a class=\"gi-row-link\" href=\"#gi-file-{i}\" aria-label=\"Show contributors for {pathHtml}\"></a>");
            var nameHref = fileHref?.Invoke(file.Path);
            sb.Append(nameHref is { Length: > 0 }
                ? $"<a class=\"gi-file-name\" href=\"{PathUtil.Html(nameHref)}\"><code>{pathHtml}</code></a>"
                : $"<span class=\"gi-file-name\"><code>{pathHtml}</code></span>");
            sb.Append("</td>\n");
            sb.Append($"            <td class=\"gi-num\">{N(file.Changes)}</td>\n");
            sb.Append($"            <td class=\"gi-num\">{N(file.LinesAdded)}</td>\n");
            sb.Append($"            <td class=\"gi-num\">{N(file.LinesDeleted)}</td>\n");
            sb.Append("          </tr>\n");
        }
        sb.Append("        </tbody>\n      </table>\n");
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");

        // Detail: one contributor panel per file (revealed by :target) + a default prompt.
        sb.Append("    <div class=\"gi-detail\">\n");
        for (var i = 0; i < files.Count; i++)
        {
            AppendContributorPanel(sb, files[i], i, fileHref, commitHref);
        }
        // Hub-wide softening (Story 10.6, AC2b): "the people to talk to" reads oddly in a solo repo, where every
        // file's contributor panel already says "Sole contributor:" — soften the shared unselected-state prompt
        // the same way.
        var prompt = insights.ContributorCount == 1
            ? "Select a file to see who has been working on it — the person to talk to about that area."
            : "Select a file to see who has been working on it — the people to talk to about that area.";
        sb.Append("      <div class=\"gi-detail-default chart-panel\">\n");
        sb.Append($"        <p class=\"gi-detail-prompt\">{prompt}</p>\n");
        sb.Append("      </div>\n");
        sb.Append("    </div>\n");

        sb.Append("  </div>\n");
        sb.Append("</section>\n\n");
    }

    /// <summary>One file's contributor panel — the detail side of the master-detail, revealed when its file is
    /// selected (pure-CSS <c>:target</c>). Lists the people who touched this file (commits + when they last
    /// did), framed as "who to talk to", never a rank. Carries the file's latest-change commit link and an
    /// optional "view file page" link, both guarded on resolver availability (7.1/7.4/7.5 seams).</summary>
    private static void AppendContributorPanel(
        StringBuilder sb,
        FileChangeStat file,
        int index,
        Func<string, string?>? fileHref,
        Func<string, string?>? commitHref)
    {
        // tabindex="-1": not in the tab order, but focusable via script — lets the progressive-enhancement
        // script (specscribe.js) move focus here when :target reveals this panel, without adding an extra
        // no-JS tab stop. [Deferred, Story 3.8]
        sb.Append($"      <div class=\"gi-contributors-panel chart-panel\" id=\"gi-file-{index}\" role=\"region\" tabindex=\"-1\" aria-label=\"Contributors to {PathUtil.Html(file.Path)}\">\n");
        sb.Append("        <div class=\"gi-detail-head\">\n");
        sb.Append($"          <h3 class=\"gi-detail-title\"><code>{PathUtil.Html(file.Path)}</code></h3>\n");

        // Sub-line: change count + the file's latest change (guarded commit link + date).
        sb.Append("          <p class=\"gi-detail-sub\">");
        sb.Append($"{N(file.Changes)} {Charts.Plural(file.Changes, "change", "changes")}");
        if (file.LatestHash.Length > 0)
        {
            var shortHash = file.LatestHash.Length > 7 ? file.LatestHash[..7] : file.LatestHash;
            sb.Append($" &middot; latest {GuardedLink(shortHash, commitHref?.Invoke(file.LatestHash), code: true)}");
            if (file.LastChangeDate is { } changed)
            {
                sb.Append($" on {PathUtil.Html(Charts.DReadable(changed))}");
            }
        }
        sb.Append("</p>\n");
        sb.Append("        </div>\n");

        if (file.Contributors.Count == 0)
        {
            sb.Append("        <p class=\"gi-detail-prompt\">No contributor data available for this file.</p>\n");
        }
        else
        {
            // Sole-contributor reword (Story 10.6, AC2b): "People to talk to" reads as comic when there is
            // exactly one person to talk to. TotalContributors (the file's full distinct-author count), not
            // the capped Contributors.Count, so a truncated multi-contributor list never mis-reads as solo.
            var lead = file.TotalContributors <= 1 ? "Sole contributor:" : "People to talk to about this file:";
            sb.Append($"        <p class=\"gi-detail-lead\">{lead}</p>\n");
            sb.Append("        <ul class=\"gi-contributor-list\">\n");
            foreach (var contributor in file.Contributors)
            {
                var meta = $"{N(contributor.Commits)} {Charts.Plural(contributor.Commits, "commit", "commits")}";
                if (contributor.LastCommitDate is { } last)
                {
                    meta += $" &middot; last {PathUtil.Html(Charts.DReadable(last))}";
                }
                sb.Append("          <li>");
                sb.Append($"<span class=\"gi-contributor-name\">{PathUtil.Html(contributor.Name)}</span>");
                sb.Append($"<span class=\"gi-contributor-meta\">{meta}</span>");
                sb.Append("</li>\n");
            }
            if (file.TotalContributors > file.Contributors.Count)
            {
                var more = file.TotalContributors - file.Contributors.Count;
                sb.Append($"          <li class=\"gi-more\">and {N(more)} more {Charts.Plural(more, "contributor", "contributors")}&hellip;</li>\n");
            }
            sb.Append("        </ul>\n");
        }

        // Optional deep link to the file's own detail page (7.1/7.4), only when a target exists.
        var href = fileHref?.Invoke(file.Path);
        if (href is { Length: > 0 })
        {
            sb.Append($"        <a class=\"view-epic-link gi-detail-filelink\" href=\"{PathUtil.Html(href)}\">View file page &rarr;</a>\n");
        }

        sb.Append("      </div>\n");
    }

    /// <summary>Activity over time — the existing accessible commit heatmap, reused rather than a parallel
    /// time chart. Its active-day cells already link to the generated <c>commits/{date}.html</c> pages (and
    /// this page sits at the output root, the same place the heatmap's root-relative hrefs assume), so the
    /// "select an entry → navigate to detail" contract holds with zero new link plumbing. The headline figures
    /// are derived from the SAME series the heatmap renders (<paramref name="git"/>'s <c>DailySeries</c>/
    /// <c>CommitsByDay</c>) — never from <c>insights.Activity</c>'s separately-bounded deep-git window — so the
    /// sentence can never disagree with the chart directly below it. Falls back to the deep window's activity
    /// series only when no baseline pulse is available at all. [Review fix 2026-07-09]</summary>
    private static void AppendActivitySection(StringBuilder sb, GitInsightsData insights, GitPulse? git)
    {
        sb.Append("<section class=\"deep-page-section git-insights-section\">\n");
        sb.Append("  <div class=\"chart-frame-head\"><h2>Activity Over Time</h2></div>\n");
        var windowDays = git?.DailySeries.Count ?? insights.Activity.Count;
        var windowCommits = git is not null ? git.DailySeries.Sum(d => d.Count) : insights.Activity.Sum(a => a.Count);
        // Numeric window + framing via shared slots (Story 10.2); heatmap builder carries its own grid-span window.
        sb.Append(Charts.FrameWindowSlot($"{N(windowCommits)} {Charts.Plural(windowCommits, "commit", "commits")} across {N(windowDays)} active {Charts.Plural(windowDays, "day", "days")}"));
        sb.Append("\n");
        sb.Append(Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.ActivityCadence)));
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

    /// <summary>The "top N of M files by commit count" truncated-ranking fact, shared by the header meta-pill
    /// and the Files &amp; Contributors ranking caption so the two can never independently drift (Story 10.2 AC2
    /// review — they previously duplicated this computation with inconsistent capitalization). Only the leading
    /// case differs: capitalized for the standalone frame caption, lowercase for the mid-sentence pill.</summary>
    private static string TruncatedFilesRankingFact(int shown, int total, bool capitalized) =>
        $"{(capitalized ? "Top" : "top")} {N(shown)} of {N(total)} files by commit count";
}
