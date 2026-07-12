using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Renders one per-commit detail page — a static <c>commit/{shortHash}.html</c> showing a commit's
/// subject, full message body, author + date attribution, and the files it changed with per-file line churn
/// (FR-10, Story 7.5). A synthesized page (no markdown source), so it builds its own shell the way
/// <see cref="CommitDayTemplater"/> does rather than going through <see cref="HtmlTemplater.RenderPage"/>.
/// <para>Every git field (subject, body, author, path, hash) is free text and an injection surface, so every
/// segment is escaped via <see cref="PathUtil.Html"/> before it is wrapped. The author is shown as single-commit
/// attribution ("by {author} …"), never aggregated or ranked (PRD non-goal). File paths link to Story 7.1's
/// <c>code/…html</c> pages through the guarded <paramref name="fileHref"/> resolver — a resolved target renders a
/// real link, no target renders plain <c>&lt;code&gt;</c> (never a dead link). Everything degrades: an empty body
/// omits the prose block, a null timestamp shows the author without a date, a binary file row shows a marker
/// rather than <c>+0</c>/<c>&minus;0</c>. Pure HTML/CSS with neutral tokens (no <c>--status-*</c>, no JS).</para></summary>
public static class CommitDetailTemplater
{
    // Blank-line (optionally whitespace-only) paragraph boundary in a commit body.
    private static readonly Regex ParagraphBreak = new(@"\n[ \t]*\n", RegexOptions.Compiled);

    /// <summary>The default short-hash length git abbreviates <c>%h</c> to (git's floor). The generator derives the
    /// page filename from <c>Hash[..ShortHashLength]</c> and only widens it on the astronomically-unlikely event of a
    /// 7-char collision within the bounded window, so the templater and the generator agree on the display hash.</summary>
    public const int ShortHashLength = 7;

    public static string RenderPage(
        DeepCommit commit,
        SiteNav nav,
        Func<string, string?>? fileHref = null)
    {
        var shortHash = ShortHash(commit.Hash);
        var outputPath = $"commit/{shortHash}.html";
        var prefix = PathUtil.RelativePrefix(outputPath); // "../" — commit/ is one level below the output root.
        var subject = commit.Subject.Length == 0 ? "(no subject)" : commit.Subject;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Commit {shortHash} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Commit {shortHash} in {nav.SiteTitle}: {subject}"));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ($"Commit {shortHash}", null),
        }));

        // Single <main id="main-content"> landmark / skip-link target. [Story 1.4 AC #1]
        sb.Append("<main id=\"main-content\" class=\"commit-detail\">\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <div class=\"story-kicker\">Commit Detail</div>\n");
        sb.Append($"  <h1>{PathUtil.Html(subject)}</h1>\n");

        // Attribution pill(s): the short hash, then "by {author}" and — when the timestamp parsed — the date/time.
        // Attribution framing ("who made this change"), never a count or ranking. [PRD FR-10 non-goal]
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    <span class=\"pill commit-hash-pill\"><code>{PathUtil.Html(shortHash)}</code></span>\n");
        sb.Append($"    <span class=\"pill commit-attribution\">{Attribution(commit)}</span>\n");
        sb.Append("  </div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<article class=\"doc-body\">\n");

        // Full commit body as readable, escaped, paragraph-preserving prose. Omitted entirely when empty (no
        // empty <p>). The body is the largest free-text injection surface on the site — every segment escaped.
        var bodyHtml = RenderBody(commit.Body);
        if (bodyHtml.Length > 0)
        {
            sb.Append("  <div class=\"commit-message\">\n");
            sb.Append(bodyHtml);
            sb.Append("  </div>\n");
        }

        AppendFilesTable(sb, commit.Files, prefix, fileHref);

        sb.Append("</article>\n\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The commit's display short hash: the first <see cref="ShortHashLength"/> chars of the full
    /// <c>%H</c> hash (git's default abbreviation floor), or the whole hash if it is somehow shorter.</summary>
    public static string ShortHash(string fullHash) =>
        fullHash.Length > ShortHashLength ? fullHash[..ShortHashLength] : fullHash;

    /// <summary>"by {author}" with the commit date+time appended when the timestamp parsed. Invariant date/time
    /// formatting so the attribution reads identically on every host. Escaped (author is free text).</summary>
    private static string Attribution(DeepCommit commit)
    {
        var author = commit.Author.Length == 0 ? "Unknown" : commit.Author;
        var sb = new StringBuilder($"by {PathUtil.Html(author)}");
        if (commit.Timestamp is { } ts)
        {
            var day = DateOnly.FromDateTime(ts);
            var time = ts.ToString("HH:mm", CultureInfo.InvariantCulture);
            sb.Append($" &middot; {PathUtil.Html(Charts.DReadable(day))} at {PathUtil.Html(time)}");
        }
        return sb.ToString();
    }

    /// <summary>Renders the commit body as escaped, paragraph-preserving prose: blank lines start a new
    /// <c>&lt;p&gt;</c>, single newlines within a paragraph become <c>&lt;br&gt;</c>. Every line is escaped
    /// before wrapping. Returns the empty string when the body has no visible content (so the caller omits the
    /// block rather than emitting an empty paragraph).</summary>
    private static string RenderBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder();
        foreach (var paragraph in ParagraphBreak.Split(normalized))
        {
            var lines = paragraph.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(PathUtil.Html)
                .ToList();
            if (lines.Count == 0) continue;
            sb.Append("    <p>").Append(string.Join("<br>\n", lines)).Append("</p>\n");
        }
        return sb.ToString();
    }

    /// <summary>The files-changed table: one row per touched file (path, lines added, lines deleted). The path is a
    /// guarded link to its <c>code/…html</c> page (plain <c>&lt;code&gt;</c> when unresolved). Text-file churn shows
    /// <c>+N</c>/<c>&minus;N</c>; a binary row (both counts null) shows a "binary" marker and em dashes rather than
    /// <c>+0</c>/<c>&minus;0</c>. Scrolls inside its own container so the page body never scrolls horizontally.</summary>
    private static void AppendFilesTable(
        StringBuilder sb,
        IReadOnlyList<DeepFileChange> files,
        string prefix,
        Func<string, string?>? fileHref)
    {
        sb.Append("  <section class=\"commit-files\">\n");
        var count = files.Count;
        sb.Append($"    <h2>{N(count)} {Charts.Plural(count, "file", "files")} changed</h2>\n");

        if (count == 0)
        {
            sb.Append("    <p class=\"commit-files-empty\">No file changes recorded for this commit.</p>\n");
            sb.Append("  </section>\n");
            return;
        }

        sb.Append("    <div class=\"table-scroll\">\n");
        sb.Append("    <table class=\"commit-files-table\">\n");
        sb.Append("      <caption>Files changed in this commit, with per-file line churn.</caption>\n");
        sb.Append("      <thead>\n        <tr>\n");
        sb.Append("          <th scope=\"col\">File</th>\n");
        sb.Append("          <th scope=\"col\" class=\"commit-num\">Lines added</th>\n");
        sb.Append("          <th scope=\"col\" class=\"commit-num\">Lines deleted</th>\n");
        sb.Append("        </tr>\n      </thead>\n      <tbody>\n");
        foreach (var file in files)
        {
            var pathHtml = PathUtil.Html(file.Path);
            var isBinary = file.Added is null && file.Deleted is null;
            var target = fileHref?.Invoke(file.Path);
            var pathCell = target is { Length: > 0 }
                ? $"<a href=\"{PathUtil.Html(prefix + target)}\"><code>{pathHtml}</code></a>"
                : $"<code>{pathHtml}</code>";
            if (isBinary)
            {
                pathCell += " <span class=\"commit-file-binary\">binary</span>";
            }

            sb.Append("        <tr>\n");
            sb.Append($"          <td class=\"commit-file\">{pathCell}</td>\n");
            sb.Append($"          <td class=\"commit-num commit-added\">{Added(file.Added)}</td>\n");
            sb.Append($"          <td class=\"commit-num commit-deleted\">{Deleted(file.Deleted)}</td>\n");
            sb.Append("        </tr>\n");
        }
        sb.Append("      </tbody>\n    </table>\n");
        sb.Append("    </div>\n");
        sb.Append("  </section>\n");
    }

    private static string Added(int? added) => added is { } a ? $"+{N(a)}" : "&mdash;";

    private static string Deleted(int? deleted) => deleted is { } d ? $"&minus;{N(d)}" : "&mdash;";

    /// <summary>Invariant integer formatting — derived numbers read identically regardless of host culture
    /// (the same discipline the git/chart helpers follow).</summary>
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
