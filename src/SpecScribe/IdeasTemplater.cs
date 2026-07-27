using System.Text;

namespace SpecScribe;

/// <summary>Renders the two Ideas surfaces: the grouped <c>ideas.html</c> list and each idea's
/// <c>ideas/{slug}.html</c> detail page.
/// <para>Row anatomy comes from Story 10.8's shared <see cref="ListRow"/> grammar and the row model mirrors
/// <c>SiteGenerator.RegenerateAdrs</c>' synthesized ADR landing (title + summary + badge + date chip + one primary
/// link). The page SHELL is the standalone-templater shell (<see cref="WorkGraphTemplater"/> /
/// <see cref="TraceabilityTemplater"/>) rather than the ADR landing's <see cref="HtmlTemplater.RenderPage"/>,
/// for one reason: Ideas is a TWO-level surface, and only the standalone shell can render the
/// Home → Ideas → {idea} breadcrumb a detail page needs — <c>RenderPage</c>'s crumbs are fixed at Home → title.</para>
/// <para><b>No JS anywhere.</b> Grouping is server-rendered sections, the chronology is an ordered list, and
/// <c>js-listable</c> is the inert Story 10.9 sort/filter opt-in seam. With <c>script-src 'none'</c> the pages are
/// fully readable (ADR 0013 / NFR-5). Every verdict carries its WORD, never colour alone (UX-DR17).</para>
/// [Story 18.4]</summary>
public static class IdeasTemplater
{
    public static string RenderListPage(IdeasModel model, SiteNav nav)
    {
        var outputPath = SiteNav.IdeasOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — ideas.html is at the output root.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Ideas — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Forged ideas for {nav.SiteTitle} — what was pressure-tested, how each one turned out, and where it went next."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Ideas", null),
        }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Ideas</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; ideas pressure-tested before they became work</p>\n\n");
        sb.Append("<p class=\"ideas-intro\">Each entry is a forge session &mdash; an idea taken apart by opposing personas until it hardened, died, or simply got clearer. The session&rsquo;s own chronology of decisions, assumptions, cracks and locks is on its page, alongside the original report the forge rendered at the time.</p>\n\n");

        foreach (var verdict in IdeasModel.SectionOrder)
        {
            var ideas = model.InVerdict(verdict);
            // NFR8: a verdict with nothing in it emits NO section — never an empty heading, never "0 ideas".
            if (ideas.Count == 0) continue;
            sb.Append(RenderSection(verdict, ideas));
        }

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string RenderSection(IdeaVerdict verdict, IReadOnlyList<IdeaEntry> ideas)
    {
        var sb = new StringBuilder();
        var heading = IdeaDerivation.SectionHeading(verdict);
        var anchor = "ideas-" + IdeaDerivation.Slugify(heading);
        var countWord = ideas.Count == 1 ? "1 idea" : $"{ideas.Count} ideas";

        sb.Append($"<section class=\"ideas-section\" id=\"{anchor}\">\n");
        sb.Append($"  <h2 class=\"ideas-section-heading\">{PathUtil.Html(heading)} <span class=\"ideas-section-count pill\">{PathUtil.Html(countWord)}</span></h2>\n");
        sb.Append("  <ul class=\"ideas-list list-rows-list js-listable\">\n");

        foreach (var idea in ideas)
        {
            var summaryHtml = idea.Summary is { Length: > 0 } summary
                ? $"<strong>{PathUtil.Html(idea.Title)}</strong> &mdash; {PathUtil.Html(summary)}"
                : $"<strong>{PathUtil.Html(idea.Title)}</strong>";

            // The badge carries the TRUE exit word, not the bucket's word: inside "In progress" that is what tells
            // a finished-but-clarified session apart from a genuinely unfinished one (the D2 mitigation, on the
            // list as well as the detail page). FreeTextBadge degrades an unknown word to a slugged pill that
            // still shows the word — never colour-only.
            var badgeHtml = StatusStyles.FreeTextBadge(idea.ExitWord);
            var chips = idea.Date is { } date
                ? new[] { ListRow.Chip(PathUtil.Html(PortalDates.Day(date))) }
                : Array.Empty<string>();
            var primaryLink = ListRow.PrimaryLink(PathUtil.Html(idea.DetailOutputPath), "View idea");
            var accentToken = StatusStyles.IdeaAccentToken(idea.ExitWord);

            ListRow.Render(
                sb, summaryHtml, badgeHtml, chips, primaryLink,
                extraRowClass: accentToken is null ? null : $"list-row-accent-{accentToken}",
                sortName: idea.Title,
                sortDate: idea.Date is { } sortDate ? PortalDates.IsoDay(sortDate) : null,
                // The canonical stage token (done/pending/deferred), so the client sort/filter ranks ideas through
                // StatusStyles.CanonicalRank rather than a second status vocabulary in JS (Story 10.9 guardrail).
                sortStatus: accentToken);
        }

        sb.Append("  </ul>\n");
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    /// <summary>One idea's synthesized detail page: the session's chronology, the distilled hand-off when the idea
    /// hardened, a link out to the forge's own report when it was safe to carry, and any forward link to what the
    /// idea went on to become. Synthesized because <c>forged-idea.md</c> exists ONLY on a hardened exit — linking
    /// straight to it would leave every killed, clarified and in-progress idea with no destination at all
    /// (owner decision D1).</summary>
    public static string RenderDetailPage(IdeaEntry idea, SiteNav nav)
    {
        var outputPath = idea.DetailOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "../" — detail pages live under ideas/.

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{idea.Title} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"{idea.Title} — a forged idea for {nav.SiteTitle}: how the session went and how it ended."));
        sb.Append(nav.RenderNavBar(outputPath));
        // Crumb paths are ROOT-relative: RenderBreadcrumb applies the page's own relative prefix itself.
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Ideas", SiteNav.IdeasOutputPath),
            (idea.Title, null),
        }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(idea.Title)}</h1>\n");
        if (idea.Summary is { Length: > 0 } summary)
        {
            sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(summary)}</div>\n");
        }
        sb.Append("  <div class=\"meta-pills\">\n");
        sb.Append($"    {StatusStyles.FreeTextBadge(idea.ExitWord)}\n");
        if (idea.Date is { } date)
        {
            sb.Append($"    <span class=\"pill\">{PathUtil.Html(PortalDates.Day(date))}</span>\n");
        }
        sb.Append($"    <span class=\"pill\">{PathUtil.Html(idea.WorkspaceSourceRelative)}</span>\n");
        sb.Append("  </div>\n</header>\n\n");

        sb.Append("<article class=\"doc-body idea-body\">\n");
        sb.Append(RenderOutcomeStatement(idea));

        if (idea.ForwardLinks.Count > 0)
        {
            sb.Append(RenderForwardLinks(idea, prefix));
        }

        if (idea.ForgedIdeaHtml is { Length: > 0 } handoff)
        {
            sb.Append("<h2 id=\"idea-handoff\">What was handed off</h2>\n");
            sb.Append($"<div class=\"idea-handoff\">\n{handoff}\n</div>\n\n");
        }

        sb.Append(RenderChronology(idea));

        if (idea.ReportOutputPath is { Length: > 0 } reportPath)
        {
            sb.Append(RenderReportLink(reportPath));
        }

        sb.Append("</article>\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>States how the session ACTUALLY ended, in words, including the exit the list's three buckets cannot
    /// express. epics.md fixes the list vocabulary at hardened / killed / in-progress, and the owner chose to fold
    /// the forge's third terminal exit (<em>Clarified</em>) into in-progress (D2) — so this sentence is the record
    /// that a clarified session was COMPLETE, not unfinished. Without it the bucketing would be the only statement
    /// the portal makes, and for clarified sessions it would be wrong.</summary>
    private static string RenderOutcomeStatement(IdeaEntry idea)
    {
        var text = idea.ExitWord switch
        {
            "Hardened" =>
                "This session <strong>hardened</strong>: the idea held up and was distilled into a hand-off the planning workflows can pick up.",
            "Killed" =>
                "This session <strong>killed</strong> the idea: it did not hold up under pressure. Finding that out early is a valid outcome, and the reasoning is kept here rather than discarded.",
            "Clarified" =>
                "This session ended <strong>clarified</strong>: it is <em>complete</em>, but produced understanding rather than a hand-off, so there is no distilled artifact. It is grouped under &ldquo;In progress&rdquo; on the Ideas list, which tracks epics.md&rsquo;s three-verdict vocabulary &mdash; the exit word here is the accurate record.",
            _ =>
                "This session is <strong>still open</strong>: its memory log has no completion marker, so the forge can still resume it.",
        };
        return $"<p class=\"idea-outcome\">{text}</p>\n\n";
    }

    private static string RenderForwardLinks(IdeaEntry idea, string prefix)
    {
        var sb = new StringBuilder();
        sb.Append("<h2 id=\"idea-downstream\">What it became</h2>\n");
        sb.Append("<ul class=\"idea-downstream-list\">\n");
        foreach (var link in idea.ForwardLinks)
        {
            sb.Append(
                $"  <li><a href=\"{PathUtil.Html(prefix + link.OutputRelativePath)}\" title=\"{PathUtil.Html(link.Evidence)}\">{PathUtil.Html(link.Label)}</a></li>\n");
        }
        sb.Append("</ul>\n\n");
        return sb.ToString();
    }

    private static string RenderChronology(IdeaEntry idea)
    {
        var sb = new StringBuilder();
        sb.Append("<h2 id=\"idea-chronology\">How the session went</h2>\n");
        if (idea.Entries.Count == 0)
        {
            // NFR8: an honest empty state rather than an empty list. The memlog exists (that is how the workspace
            // was discovered) but carries no entries yet — a session interrupted right after init.
            sb.Append("<p class=\"idea-chronology-empty\">This session&rsquo;s memory log has no entries yet.</p>\n\n");
            return sb.ToString();
        }

        sb.Append("<ol class=\"idea-chronology\">\n");
        foreach (var entry in idea.Entries)
        {
            var typeClass = entry.Type is { Length: > 0 } t ? " idea-entry-" + IdeaDerivation.Slugify(t) : string.Empty;
            sb.Append($"  <li class=\"idea-entry{typeClass}\">");
            if (entry.Type is { Length: > 0 } type)
            {
                // The kind is a WORD, not a colour — the type class only tints an already-labelled tag (UX-DR17).
                sb.Append($"<span class=\"idea-entry-type\">{PathUtil.Html(type)}</span> ");
            }
            sb.Append($"<span class=\"idea-entry-text\">{PathUtil.Html(entry.Text)}</span></li>\n");
        }
        sb.Append("</ol>\n\n");
        return sb.ToString();
    }

    /// <summary>Links the carried-over <c>forge-report.html</c>, labelled for what it is: the forge's OWN page,
    /// carried verbatim, with its own styling and no portal chrome. It is a dead end by design — wrapping it in
    /// the portal template would nest one complete <c>&lt;html&gt;</c> document inside another (the defect class
    /// Story 23.3 hit, where every harness passed while 187 pages were structurally corrupt).</summary>
    private static string RenderReportLink(string reportOutputPath)
    {
        // The report is a SIBLING of the detail page — both live under ideas/ — so the href is the bare filename,
        // NOT the page's "../" prefix plus the root-relative path (which would climb out of the directory and back
        // into a path that doesn't exist).
        var slash = reportOutputPath.LastIndexOf('/');
        var href = PathUtil.Html(slash < 0 ? reportOutputPath : reportOutputPath[(slash + 1)..]);
        return "<h2 id=\"idea-report\">The original report</h2>\n"
            + "<p class=\"idea-report-note\">The forge rendered its own self-contained report at the end of this session, crediting the personas that pressure-tested the idea and naming what was rejected and why. It is carried into this portal exactly as written &mdash; it keeps its own styling and has no site navigation, so use your browser&rsquo;s back button to return.</p>\n"
            + $"<p class=\"idea-report-link\"><a class=\"list-row-primary\" href=\"{href}\">Open the original forge report &rarr;</a></p>\n\n";
    }
}
