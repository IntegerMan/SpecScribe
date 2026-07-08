using System.Text;

namespace SpecScribe;

/// <summary>Renders a retrospective note (<see cref="RetroModel"/>) as a dedicated, stylized page: a header
/// with the epic-retro kicker, date, a link to the epic, and participant pills, then the narrative (its section
/// headings feeding the shared "On this page" TOC via <see cref="Toc"/>). Mirrors the shared page shell used by
/// <see cref="SprintTemplater"/>/<see cref="RequirementsTemplater"/> — one <c>&lt;main id="main-content"&gt;</c>,
/// shared nav/breadcrumb/footer. [Story 2.3 retro pages]</summary>
public static class RetroTemplater
{
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
            main.Append("  <div class=\"retro-participants\" aria-label=\"Participants\">\n");
            foreach (var p in retro.Participants)
            {
                main.Append($"    <span class=\"participant-pill\">{PathUtil.Html(p)}</span>\n");
            }
            main.Append("  </div>\n");
        }
        main.Append("</header>\n\n");

        main.Append("<article class=\"doc-body\">\n");
        main.Append(retro.BodyHtml);
        main.Append("\n</article>\n");

        sb.Append("<main id=\"main-content\">\n");
        sb.Append(Toc.WrapWithSidebar(main.ToString(), Toc.ExtractHeadings(retro.BodyHtml)));
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
