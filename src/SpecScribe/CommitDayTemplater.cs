using System.Text;

namespace SpecScribe;

/// <summary>Renders one "date page" — a static <c>commits/{yyyy-MM-dd}.html</c> summarizing what happened on
/// that day: the commits that landed (short hash, author-local time, author, subject) and/or the artifacts
/// changed (git-derived from that day's commits, each linking to its generated page), with previous/next links to the
/// adjacent active days. A synthesized page (no markdown source), so it builds its own shell the way
/// <see cref="RequirementsTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
/// The heatmap cell and the activity timeline both link here. Generalized from the original commit-only page in
/// Story 7.3: a day may carry commits, artifacts, or both — the heading, pills, and sections all adapt, and a
/// commit-bearing day with no artifact changes renders exactly as it did before. [Story 7.3]</summary>
public static class CommitDayTemplater
{
    public static string RenderPage(
        DateOnly day,
        IReadOnlyList<CommitInfo> commits,
        IReadOnlyList<(string Label, string Href)> artifacts,
        EntityPager? pager,
        SiteNav nav,
        Func<string, string?>? commitHref = null)
    {
        var readable = Charts.DReadable(day);
        var outputPath = $"commits/{Charts.D(day)}.html";
        var prefix = PathUtil.RelativePrefix(outputPath);

        var hasCommits = commits.Count > 0;
        var hasArtifacts = artifacts.Count > 0;
        // A day reads as "Commits on …" when it carries commits; an artifact-only day (a doc edited with no
        // commit) reads as the neutral "Activity on …" so the heading never claims commits it doesn't have.
        var pageLabel = hasCommits ? $"Commits on {readable}" : $"Activity on {readable}";
        var kicker = hasCommits ? "Commit Activity" : "Activity";

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{pageLabel} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            BuildMetaDescription(readable, nav.SiteTitle, commits.Count, artifacts.Count)));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            (pageLabel, null),
        }));

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append(pager?.Render()); // Prev = newer day, Next = older (newest-first order). [Prev/next navigation]
        sb.Append($"  <div class=\"story-kicker\">{PathUtil.Html(kicker)}</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(pageLabel)}</h1>\n");
        sb.Append("  <div class=\"meta-pills\">");
        if (hasCommits)
        {
            sb.Append($"<span class=\"pill\">{commits.Count} {Charts.Plural(commits.Count, "commit", "commits")}</span>");
        }
        if (hasArtifacts)
        {
            sb.Append($"<span class=\"pill\">{artifacts.Count} {Charts.Plural(artifacts.Count, "artifact updated", "artifacts updated")}</span>");
        }
        sb.Append("</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<article class=\"doc-body\">\n");

        // Commits list newest-first (git log order) — only when the day actually has commits. Each row: short
        // hash, author-local time, author, subject.
        if (hasCommits)
        {
            sb.Append("<ul class=\"commit-day-list\">\n");
            foreach (var commit in commits)
            {
                sb.Append("  <li class=\"commit-day-item\">\n");
                // The hash links to its per-commit detail page (Story 7.5) when the resolver has one, plain otherwise
                // (guard-all-links-on-target-availability). The resolver returns a path relative to the output root,
                // so this nested page prepends its own "../" prefix. The <code class="commit-hash"> stays intact for
                // styling either way.
                var hashHtml = PathUtil.Html(commit.ShortHash);
                var commitTarget = commitHref?.Invoke(commit.ShortHash);
                sb.Append(commitTarget is { Length: > 0 }
                    ? $"    <a class=\"commit-hash-link\" href=\"{PathUtil.Html(prefix + commitTarget)}\"><code class=\"commit-hash\">{hashHtml}</code></a>\n"
                    : $"    <code class=\"commit-hash\">{hashHtml}</code>\n");
                sb.Append($"    <span class=\"commit-time\">{PathUtil.Html(commit.Time)}</span>\n");
                sb.Append($"    <span class=\"commit-author\">{PathUtil.Html(commit.Author)}</span>\n");
                sb.Append($"    <span class=\"commit-subject\">{PathUtil.Html(commit.Subject)}</span>\n");
                sb.Append("  </li>\n");
            }
            sb.Append("</ul>\n");
            // One caption for the git clock's zone (Story 10.4 "captioned git"): commit times stay in each commit's
            // authored offset — distinct from the machine-local, zone-labeled generation footer below.
            sb.Append("<p class=\"git-pulse-zone-note\">Commit times shown in each commit&rsquo;s local time zone.</p>\n");
        }

        // "Artifacts updated" — the git-derived artifact-change signal: each recognized artifact that a commit on
        // this day actually touched, linking to its generated page (Story 7.3 bug fix — replaces the old, misleading
        // filesystem-mtime signal that collapsed every file onto the checkout day). Omitted (no empty heading) when
        // no tracked artifact changed that day, and absent entirely without --deep-git. Hrefs are
        // output-root-relative, so this nested page prepends its own "../" prefix; labels/hrefs are escaped.
        if (hasArtifacts)
        {
            sb.Append("<section class=\"artifacts-updated\">\n");
            sb.Append("<h2>Artifacts updated</h2>\n");
            sb.Append("<ul class=\"artifact-update-list\">\n");
            foreach (var (label, href) in artifacts)
            {
                sb.Append($"  <li><a href=\"{PathUtil.Html(prefix + href)}\">{PathUtil.Html(label)}</a></li>\n");
            }
            sb.Append("</ul>\n");
            sb.Append("</section>\n");
        }

        sb.Append("</article>\n\n");

        // Prev/next hop across active days now rides the inline header pager (see above), replacing the old
        // bottom-of-page nav so every entity family navigates identically. [Prev/next navigation]

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The page's meta/OG description, phrased to match what the day actually carries: commits only
    /// (the original wording, unchanged for byte-parity on existing commit pages), commits + artifacts, or an
    /// artifact-only day. Counts drive the singular/plural so the summary never reads "1 commits".</summary>
    private static string BuildMetaDescription(string readable, string siteTitle, int commitCount, int artifactCount)
    {
        if (commitCount > 0 && artifactCount == 0)
        {
            return $"The {commitCount} commit{(commitCount == 1 ? string.Empty : "s")} that landed on {readable} in {siteTitle}.";
        }
        if (commitCount > 0)
        {
            return $"The {commitCount} commit{(commitCount == 1 ? string.Empty : "s")} and {artifactCount} artifact update{(artifactCount == 1 ? string.Empty : "s")} on {readable} in {siteTitle}.";
        }
        return $"{artifactCount} artifact{(artifactCount == 1 ? string.Empty : "s")} updated on {readable} in {siteTitle}.";
    }
}
