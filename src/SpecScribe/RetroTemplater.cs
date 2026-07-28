using System.Text;

namespace SpecScribe;

/// <summary>Renders a retrospective note (<see cref="RetroModel"/>) as a dedicated, stylized page: a header
/// with the epic-retro kicker, date, a link to the epic, and participant pills, then the narrative (its section
/// headings feeding the shared "On this page" TOC via <see cref="Toc"/>). Mirrors the shared page shell used by
/// <see cref="SprintTemplater"/>/<see cref="RequirementsTemplater"/> — one <c>&lt;main id="main-content"&gt;</c>,
/// shared nav/breadcrumb/footer. [Story 2.3 retro pages]</summary>
public static class RetroTemplater
{
    /// <summary>The retrospectives index page (<c>retros.html</c>): one card per retro (title, date, epic),
    /// each linking to its dedicated page. Mirrors the shared index-page shell. [Story 2.3 retro pages]</summary>
    public static string RenderIndex(IReadOnlyList<RetroModel> retros, SiteNav nav) =>
        HtmlRenderAdapter.Shared.Render(BuildIndexPage(retros, nav)).Content;

    /// <summary>Builds the index page's host-neutral <see cref="PageView"/> — see
    /// <see cref="BuildPage"/> for why Story 23.4 moved every templater onto this contract.
    /// ⚠️ <b>The body starts at <c>&lt;header class="doc-header"&gt;</c>, not at <c>&lt;main&gt;</c></b>: this page
    /// emits its title block BEFORE the landmark, and the old region SLICE started at the breadcrumb, so that
    /// header was inside the captured region. A body that began at <c>&lt;main&gt;</c> would still render the
    /// static page correctly — the golden gate would stay green — while silently dropping the page's own title
    /// block from the IR. [Story 23.4 AC #3, finding 1]</summary>
    public static PageView BuildIndexPage(IReadOnlyList<RetroModel> retros, SiteNav nav)
    {
        var outputPath = SiteNav.RetrosOutputPath;

        var sb = new StringBuilder();
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Retrospectives</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {retros.Count} {Charts.Plural(retros.Count, "retrospective", "retrospectives")}</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\">\n");
        sb.Append("<div class=\"index-grid\">\n");
        foreach (var r in retros)
        {
            sb.Append($"  <a class=\"index-card\" href=\"{PathUtil.Html(PathUtil.NormalizeSlashes(r.OutputRelativePath))}\">\n");
            sb.Append($"    <h2>{PathUtil.Html(r.Title)}</h2>\n");
            var meta = new List<string> { EpicsLabel(r.EpicNumbers) };
            if (r.DateText is { Length: > 0 } d) meta.Add(PortalDates.ReformatAuthored(d));
            sb.Append($"    <p>{PathUtil.Html(string.Join(" · ", meta))}</p>\n");
            sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(PathUtil.NormalizeSlashes(r.SourceRelativePath))}</span>\n");
            sb.Append("  </a>\n");
        }
        sb.Append("</div>\n\n");
        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Retro,
            OutputRelativePath = outputPath,
            Title = $"Retrospectives — {nav.SiteTitle}",
            MetaDescription = $"Epic retrospectives for {nav.SiteTitle}.",
            Nav = nav.ToNavigationView(outputPath),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[] { ("Home", "index.html"), ("Retrospectives", null) }),
            Assets = new AssetManifest
            {
                StylesheetHref = ForgeOptions.StylesheetName,
                ScriptHref = ForgeOptions.ScriptName,
                MermaidNeeded = false,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    public static string RenderPage(RetroModel retro, EpicsModel? epics, SiteNav nav, EntityPager? pager = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(retro, epics, nav, pager)).Content;

    /// <summary>Builds a retro page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract. Story 23.4
    /// moved every standalone templater onto it so the IR's content region can be COMPOSED
    /// (<see cref="JsonSpaRenderAdapter.RenderContent"/>: nav markup + wayfinding + body) instead of sliced back
    /// out of a rendered full page. <see cref="RenderPage"/> is the unchanged HTML projection of this same model,
    /// so the bytes are identical. The TOC active-section script stays chrome-level:
    /// <see cref="HtmlRenderAdapter.Render"/> emits it when the body carries a <c>toc-sidebar</c>, which is
    /// exactly the old <c>toc.Count &gt; 0</c> condition. [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(RetroModel retro, EpicsModel? epics, SiteNav nav, EntityPager? pager = null)
    {
        var outputPath = retro.OutputRelativePath;
        var prefix = PathUtil.RelativePrefix(outputPath);
        // Every epic this retro covers that actually exists in the model, in ascending order. A joint retro
        // names several; one naming an epic the model doesn't have simply contributes no link and no stories.
        var coveredEpics = retro.EpicNumbers
            .Select(n => epics?.Epics.FirstOrDefault(e => e.Number == n))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        var sb = new StringBuilder();

        var main = new StringBuilder();
        main.Append("<header class=\"doc-header retro-header\">\n");
        main.Append($"  <div class=\"story-kicker\">{PathUtil.Html(KickerText(retro))}</div>\n");
        main.Append($"  <h1>{PathUtil.Html(HeadingTitle(retro))}</h1>\n");

        main.Append("  <div class=\"meta-pills\">\n");
        if (retro.DateText is { Length: > 0 } date)
        {
            main.Append($"    <span class=\"pill\">{PathUtil.Html(PortalDates.ReformatAuthored(date))}</span>\n");
        }
        // One pill per covered epic — a joint retro links back to every epic it reviewed, not just the first.
        foreach (var covered in coveredEpics)
        {
            main.Append($"    <a class=\"pill pill-link\" href=\"{PathUtil.Html($"{prefix}epics/epic-{covered.Number}.html")}\">Epic {covered.Number} &rarr;</a>\n");
        }
        main.Append("  </div>\n");
        main.Append("</header>\n\n");

        // The retro is epic-scoped, so surface that epic's stories (each linked to its story/placeholder page)
        // right under the header — the sprint's stories, reachable from the retro that reviewed them. Rendered
        // as the shared Kanban `.sprint-card` (id + title, status color on the left border) so they read exactly
        // like the sprint board's cards, in a responsive grid.
        var toc = new List<Toc.Entry>();
        var coveredStories = coveredEpics.SelectMany(e => e.Stories).ToList();
        if (coveredStories.Count > 0)
        {
            // Plural keys off epics that actually CONTRIBUTED stories — a joint retro whose second epic has none
            // would otherwise say "these Epics" over a grid holding one epic's work.
            var storiesHeading = coveredEpics.Count(e => e.Stories.Count > 0) > 1
                ? "Stories in these Epics"
                : "Stories in this Epic";
            main.Append($"<section class=\"retro-stories\" id=\"retro-stories\">\n  <h2>{PathUtil.Html(storiesHeading)}</h2>\n  <div class=\"retro-story-grid\">\n");
            foreach (var story in coveredStories)
            {
                var storyClass = StatusStyles.ForStory(story);
                var href = prefix + (story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id));
                main.Append($"    <a class=\"sprint-card {storyClass}\" href=\"{PathUtil.Html(href)}\">\n");
                main.Append($"      <div class=\"sprint-card-head\"><span class=\"sprint-card-id\">Story {PathUtil.Html(story.Id)}</span></div>\n");
                main.Append($"      <span class=\"sprint-card-title\">{story.Title}</span>\n");
                main.Append("    </a>\n");
            }
            main.Append("  </div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, storiesHeading, "retro-stories"));
        }

        main.Append("<article class=\"doc-body\">\n");
        main.Append(retro.BodyHtml);
        main.Append("\n</article>\n");

        toc.AddRange(Toc.ExtractHeadings(retro.BodyHtml));
        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(main.ToString(), toc));
        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Retro,
            OutputRelativePath = outputPath,
            Title = $"{retro.Title} — {nav.SiteTitle}",
            MetaDescription = $"{retro.Title} — a retrospective for {nav.SiteTitle}.",
            Nav = nav.ToNavigationView(outputPath),
            // Sibling pager (Prev/next across retros in ascending epic order) rides the coherent wayfinding strip
            // alongside the breadcrumb now, not the body's own header. [Story 10.11]
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", "index.html"),
                ("Sprint Status", "sprint.html"),
                (retro.Title, null),
            }),
            Pager = pager,
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = retro.HasMermaid,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    /// <summary>Plain-text label for the epics a retro covers: "Epic 1", "Epics 19 &amp; 21",
    /// "Epics 19, 20 &amp; 21". Returned UNESCAPED so each call site can escape once — the ampersand would
    /// otherwise be double-encoded by the callers that already run the value through
    /// <see cref="PathUtil.Html"/>. Single-epic output is character-for-character what this templater emitted
    /// before joint retros were supported, which is what keeps the golden fingerprint still.
    /// [spec-multi-epic-retro-attribution]</summary>
    private static string EpicsLabel(IReadOnlyList<int> epicNumbers)
    {
        // "Unattributed" rather than a bare "Epic": a card reading "Epic · Jul 7, 2026" looks like data, when in
        // fact no epic could be determined. Reuses the vocabulary the work-graph already uses for this state.
        if (epicNumbers.Count == 0) return "Unattributed";
        if (epicNumbers.Count == 1) return $"Epic {epicNumbers[0]}";

        var lead = string.Join(", ", epicNumbers.Take(epicNumbers.Count - 1));
        return $"Epics {lead} & {epicNumbers[^1]}";
    }

    /// <summary>The kicker above the h1 — "Epic 1 Retrospective" / "Epics 19 &amp; 21 Retrospective".</summary>
    private static string KickerText(RetroModel retro) => $"{EpicsLabel(retro.EpicNumbers)} Retrospective";

    /// <summary>The h1 title with the redundant "Epic N Retrospective" prefix stripped — the kicker line above
    /// already carries it, so a title like "Epic 1 Retrospective: High-Clarity …" shows as just "High-Clarity …".
    /// Falls back to "Retrospective" when nothing follows the prefix, and leaves any other title untouched.</summary>
    private static string HeadingTitle(RetroModel retro)
    {
        var kicker = KickerText(retro);
        var title = retro.Title.TrimStart();
        if (!title.StartsWith(kicker, StringComparison.OrdinalIgnoreCase)) return retro.Title;

        var rest = title[kicker.Length..].TrimStart(' ', '\t', ':', '-', '–', '—');
        return rest.Length > 0 ? rest : "Retrospective";
    }
}
