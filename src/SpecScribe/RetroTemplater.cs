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
    public static string RenderIndex(IReadOnlyList<RetroModel> retros, SiteNav nav)
    {
        var outputPath = SiteNav.RetrosOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Retrospectives — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"Epic retrospectives for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[] { ("Home", "index.html"), ("Retrospectives", null) }));

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
            var meta = new List<string> { $"Epic {r.EpicNumber}" };
            if (r.DateText is { Length: > 0 } d) meta.Add(d);
            sb.Append($"    <p>{PathUtil.Html(string.Join(" · ", meta))}</p>\n");
            sb.Append($"    <span class=\"index-card-path\">{PathUtil.Html(PathUtil.NormalizeSlashes(r.SourceRelativePath))}</span>\n");
            sb.Append("  </a>\n");
        }
        sb.Append("</div>\n\n");
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public static string RenderPage(RetroModel retro, EpicsModel? epics, SiteNav nav)
    {
        var outputPath = retro.OutputRelativePath;
        var prefix = PathUtil.RelativePrefix(outputPath);
        var epicExists = epics?.Epics.Any(e => e.Number == retro.EpicNumber) ?? false;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{retro.Title} — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"{retro.Title} — a retrospective for {nav.SiteTitle}."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", "index.html"),
            ("Sprint Status", "sprint.html"),
            (retro.Title, null),
        }));

        var main = new StringBuilder();
        main.Append("<header class=\"doc-header retro-header\">\n");
        main.Append($"  <div class=\"story-kicker\">Epic {retro.EpicNumber} Retrospective</div>\n");
        main.Append($"  <h1>{PathUtil.Html(retro.Title)}</h1>\n");

        main.Append("  <div class=\"meta-pills\">\n");
        if (retro.DateText is { Length: > 0 } date)
        {
            main.Append($"    <span class=\"pill\">{PathUtil.Html(date)}</span>\n");
        }
        if (epicExists)
        {
            main.Append($"    <a class=\"pill pill-link\" href=\"{PathUtil.Html($"{prefix}epics/epic-{retro.EpicNumber}.html")}\">Epic {retro.EpicNumber} &rarr;</a>\n");
        }
        main.Append("  </div>\n");

        if (retro.Participants.Count > 0)
        {
            main.Append("  <section class=\"retro-personas\" aria-label=\"Personas\">\n");
            main.Append("    <div class=\"retro-personas-label\">Personas</div>\n");
            main.Append("    <div class=\"persona-grid\">\n");
            foreach (var raw in retro.Participants)
            {
                var p = Personas.Parse(raw);
                main.Append($"      <span class=\"persona-pill {p.RoleClass}\">{Icons.ForPersona(p.RoleClass)}");
                main.Append($"<span class=\"persona-name\">{PathUtil.Html(p.Name)}</span>");
                if (p.Role is { Length: > 0 } role)
                {
                    main.Append($"<span class=\"persona-role\">{PathUtil.Html(role)}</span>");
                }
                main.Append("</span>\n");
            }
            main.Append("    </div>\n");
            main.Append("  </section>\n");
        }
        main.Append("</header>\n\n");

        // The retro is epic-scoped, so surface that epic's stories (each linked to its story/placeholder page)
        // right under the header — the sprint's stories, reachable from the retro that reviewed them.
        var toc = new List<Toc.Entry>();
        var epic = epics?.Epics.FirstOrDefault(e => e.Number == retro.EpicNumber);
        if (epic is { Stories.Count: > 0 })
        {
            main.Append("<section class=\"retro-stories\" id=\"retro-stories\">\n  <h2>Stories in this Epic</h2>\n  <div class=\"retro-story-list\">\n");
            foreach (var story in epic.Stories)
            {
                var storyClass = StatusStyles.ForStory(story);
                var href = prefix + (story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id));
                main.Append($"    <a class=\"retro-story-row {storyClass}\" href=\"{PathUtil.Html(href)}\">\n");
                main.Append($"      <span class=\"retro-story-id\">Story {PathUtil.Html(story.Id)}</span>\n");
                main.Append($"      <span class=\"retro-story-title\">{story.Title}</span>\n");
                main.Append($"      {StatusStyles.Badge(storyClass, StatusStyles.StoryLabel(storyClass))}\n");
                main.Append("    </a>\n");
            }
            main.Append("  </div>\n</section>\n\n");
            toc.Add(new Toc.Entry(2, "Stories in this Epic", "retro-stories"));
        }

        main.Append("<article class=\"doc-body\">\n");
        main.Append(retro.BodyHtml);
        main.Append("\n</article>\n");

        toc.AddRange(Toc.ExtractHeadings(retro.BodyHtml));
        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(main.ToString(), toc));
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter($"on {DateTime.Now:yyyy-MM-dd HH:mm}"));
        if (retro.HasMermaid)
        {
            sb.Append(Mermaid.InitScript());
        }
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
