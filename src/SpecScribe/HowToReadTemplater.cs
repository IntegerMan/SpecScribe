using System.Text;

namespace SpecScribe;

/// <summary>Renders the "How to read this portal" orientation page (<c>how-to-read.html</c>): a suggested
/// reading order through the pages that actually exist, plus a glossary of the detected module's
/// vocabulary. The adoption gate for a first-time visitor who doesn't know the methodology (Journey 5).
/// Mirrors <see cref="AboutTemplater"/>'s chromeless synthesized-page shell exactly. Written on every full
/// run so its Home quick-link and footer reach can never 404, and — like About/Diagnostics — it is written
/// directly rather than through <c>ApplyReferenceLinks</c>, so it never self-expands the very glossary
/// terms it defines. [Story 10.3]</summary>
public static class HowToReadTemplater
{
    public static string RenderPage(SiteNav nav, IReadOnlyList<ModuleDoc> moduleDocs, IReadOnlyList<GlossaryTerm> glossary, CommandCatalog commands)
    {
        var outputPath = SiteNav.HowToReadOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"How to Read This Portal — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"Orientation for a first visit to {nav.SiteTitle}'s documentation portal: a suggested reading order and a glossary of the terms used throughout."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("How to Read This Portal", null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>How to Read This Portal</h1>\n");

        // Build sections first so the header subtitle + intro can tell the truth when every append is a
        // no-op (undetected module + no reading-order pages) — never promise content that doesn't exist.
        var sections = new StringBuilder();
        AppendReadingOrder(sections, nav, moduleDocs);
        AppendGlossary(sections, glossary);
        AppendCommandLegend(sections, commands);

        if (sections.Length > 0)
        {
            sb.Append("  <div class=\"doc-subtitle\">New here? Start with the reading order and glossary below.</div>\n");
        }
        else
        {
            sb.Append("  <div class=\"doc-subtitle\">Orientation for a first visit — content appears as the project grows.</div>\n");
        }
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel howtoread-panel\">\n");

        if (sections.Length > 0)
        {
            sb.Append("  <p>This portal documents a project built with an AI-assisted development methodology. ");
            sb.Append("If you're new to it, the sections below walk you through what to read first and what the ");
            sb.Append("recurring terms mean.</p>\n");
            sb.Append(sections);
        }
        else
        {
            sb.Append("  <p>This portal documents a project built with an AI-assisted development methodology. ");
            sb.Append("No reading-order pages or glossary terms are available for this project yet.</p>\n");
        }

        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Journey 5's canonical path (Readme → module docs in their own declared order → ADRs →
    /// Epics → Sprint), gated on the same availability signal the nav bar already used — a step whose page
    /// wasn't produced is simply omitted (NFR8), so a shallow repo gets a shorter, honest list. The module
    /// docs step reuses whatever order <see cref="ModuleContext.DocsFor"/> declared, so the sequence reads
    /// "Readme → PRD → Architecture → ADRs → Epics → Sprint" for BMad Method without this templater naming
    /// those labels itself — a Game Dev Studio repo gets its own doc labels in the same slot.</summary>
    private static void AppendReadingOrder(StringBuilder sb, SiteNav nav, IReadOnlyList<ModuleDoc> moduleDocs)
    {
        var steps = new List<(string Label, string OutputRelativePath)>();
        if (nav.HasReadme)
        {
            steps.Add(("Readme", SiteNav.ReadmeOutputPath));
        }

        foreach (var doc in moduleDocs.Where(d => d.InNav))
        {
            var match = nav.Items.FirstOrDefault(i => i.Label == doc.Label);
            if (match.OutputRelativePath is { Length: > 0 })
            {
                steps.Add(match);
            }
        }

        if (nav.HasAdrs)
        {
            steps.Add(("ADRs", SiteNav.AdrsLandingOutputPath));
        }

        if (nav.HasEpics)
        {
            steps.Add(("Epics", SiteNav.EpicsOutputPath));
        }

        if (nav.HasSprint)
        {
            steps.Add(("Sprint", SiteNav.SprintOutputPath));
        }

        if (steps.Count == 0)
        {
            return;
        }

        sb.Append("  <h2 id=\"reading-order\">Reading order</h2>\n");
        sb.Append("  <ol class=\"howtoread-order\">\n");
        foreach (var step in steps)
        {
            sb.Append($"    <li><a href=\"{PathUtil.Html(step.OutputRelativePath)}\">{PathUtil.Html(step.Label)}</a></li>\n");
        }
        sb.Append("  </ol>\n");
    }

    /// <summary>The module's vocabulary as a definition list, acronyms first (stable sort preserves each
    /// group's declared order). Omitted entirely — not rendered empty — when the module publishes no
    /// glossary (an undetected framework), so AC2/NFR8 never renders an empty-but-present section.</summary>
    private static void AppendGlossary(StringBuilder sb, IReadOnlyList<GlossaryTerm> glossary)
    {
        if (glossary.Count == 0)
        {
            return;
        }

        sb.Append("  <h2 id=\"glossary\">Glossary</h2>\n");
        sb.Append("  <dl class=\"howtoread-glossary\">\n");
        foreach (var term in glossary.OrderByDescending(g => g.IsAcronym))
        {
            sb.Append($"    <div class=\"cap-row\"><dt>{PathUtil.Html(term.Term)}</dt><dd>{PathUtil.Html(term.Definition)}</dd></div>\n");
        }
        sb.Append("  </dl>\n");
    }

    /// <summary>A light-touch note that the slash commands seen on story/epic pages come from the detected
    /// methodology — not a full command enumeration (the story pages already caption each one). Omitted
    /// when no module was detected, so an undetected framework never claims a methodology it doesn't have.</summary>
    private static void AppendCommandLegend(StringBuilder sb, CommandCatalog commands)
    {
        if (commands.IsEmpty)
        {
            return;
        }

        sb.Append("  <h2 id=\"commands\">Commands you'll see</h2>\n");
        sb.Append($"  <p>Slash commands like the ones captioned on story and epic pages come from your detected methodology, {PathUtil.Html(commands.ModuleLabel)}.</p>\n");
    }
}
