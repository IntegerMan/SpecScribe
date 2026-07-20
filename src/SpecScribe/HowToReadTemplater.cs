using System.Text;

namespace SpecScribe;

/// <summary>Renders the "Spec-Driven Development" orientation page (<c>how-to-read.html</c>): framework tabs
/// (always present, colored by detection), a static curated command list, methodology flowchart per supported
/// framework, muted stubs for planned frameworks, and install docs when absent. Preserves the reading-order
/// and glossary sections. Written on every full run so its Home quick-link and footer reach can never 404, and
/// — like About/Diagnostics — it is written directly rather than through <c>ApplyReferenceLinks</c>, so it
/// never self-expands the very glossary terms it defines. [Story 10.3; SDD help page]</summary>
public static class HowToReadTemplater
{
    public static string RenderPage(SiteNav nav, IReadOnlyList<ModuleDoc> moduleDocs, IReadOnlyList<GlossaryTerm> glossary, CommandCatalog commands, bool methodPresent = false, bool gdsPresent = false)
    {
        var outputPath = SiteNav.HowToReadOutputPath;

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Spec-Driven Development — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            $"Orientation for spec-driven development with {nav.SiteTitle}: frameworks, commands, and methodology."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Spec-Driven Development", null),
        }));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>Spec-Driven Development</h1>\n");

        // Build sections first so the header subtitle + intro can tell the truth when every append is a
        // no-op (undetected module + no reading-order pages) — never promise content that doesn't exist.
        var sections = new StringBuilder();
        AppendFrameworkTabs(sections, methodPresent, gdsPresent);
        AppendReadingOrder(sections, nav, moduleDocs);
        AppendGlossary(sections, glossary);
        AppendCommandLegend(sections, commands);

        sb.Append("  <div class=\"doc-subtitle\">Frameworks, commands, and methodology for spec-driven development.</div>\n");
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel howtoread-panel\">\n");

        sb.Append("  <p>This portal documents a project built with an AI-assisted spec-driven development methodology. ");
        sb.Append("The framework tabs below show which modules are available, their key commands, and how to install any that are missing.</p>\n");
        sb.Append(sections);

        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        sb.Append(PathUtil.RenderFooter());

        var html = sb.ToString();
        // Append Mermaid init script only when page contains a Mermaid block.
        if (Mermaid.ContainsBlock(html))
        {
            sb.Append(Mermaid.InitScript());
            sb.Append("</body>\n</html>\n");
            return sb.ToString();
        }

        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>Default tab logic: Method if Present, else GDS if Present, else Method.</summary>
    private static string DefaultTab(bool methodPresent, bool gdsPresent)
        => !methodPresent && gdsPresent ? "gds" : "method";

    private static void AppendFrameworkTabs(StringBuilder sb, bool methodPresent, bool gdsPresent)
    {
        var defaultTab = DefaultTab(methodPresent, gdsPresent);

        var tabs = new (string Id, string Label, string State)[]
        {
            ("method", "BMad Method", methodPresent ? "present" : "absent"),
            ("gds", "BMad GDS", gdsPresent ? "present" : "absent"),
            ("speckit", "Spec Kit", "coming-soon"),
            ("gsd", "GSD", "coming-soon"),
            ("gsd-pi", "GSD-Pi", "coming-soon"),
            ("superpowers", "Superpowers", "coming-soon"),
        };

        sb.Append("  <div class=\"sdd-tabs\">\n");
        sb.Append("    <fieldset class=\"sdd-tablist\">\n");
        sb.Append("      <legend class=\"sr-only\">Framework tabs</legend>\n");

        foreach (var (id, label, state) in tabs)
        {
            var isChecked = id == defaultTab ? " checked" : "";
            var badgeText = state switch
            {
                "present" => "Present",
                "absent" => "Absent",
                _ => "Coming Soon",
            };
            sb.Append($"      <label class=\"sdd-tab sdd-tab--{id} sdd-tab-state--{state}\">");
            sb.Append($"<input type=\"radio\" name=\"sdd-framework\" value=\"{id}\" class=\"sdd-tab-input\"{isChecked}>");
            sb.Append($"<span class=\"sdd-tab-label\">{PathUtil.Html(label)}</span>");
            sb.Append($"<span class=\"sdd-tab-badge sdd-badge--{state}\">{badgeText}</span>");
            sb.Append("</label>\n");
        }

        sb.Append("    </fieldset>\n");

        // Panels
        AppendMethodPanel(sb, methodPresent);
        AppendGdsPanel(sb, gdsPresent);
        AppendComingSoonPanel(sb, "speckit", "Spec Kit");
        AppendComingSoonPanel(sb, "gsd", "GSD");
        AppendComingSoonPanel(sb, "gsd-pi", "GSD-Pi");
        AppendComingSoonPanel(sb, "superpowers", "Superpowers");

        sb.Append("  </div>\n");
    }

    private static void AppendMethodPanel(StringBuilder sb, bool present)
    {
        sb.Append("    <div class=\"sdd-tabpanel sdd-tabpanel--method\">\n");
        if (present)
        {
            sb.Append("      <h3>BMad Method — Commands</h3>\n");
            sb.Append("      <ul class=\"sdd-commands\">\n");
            sb.Append("        <li><code>/bmad-help</code> — guided help</li>\n");
            sb.Append("        <li><code>/bmad-product-brief</code> — product brief</li>\n");
            sb.Append("        <li><code>/bmad-prd</code> — PRD</li>\n");
            sb.Append("        <li><code>/bmad-create-epics-and-stories</code> — epics &amp; stories</li>\n");
            sb.Append("        <li><code>/bmad-create-story</code> — story ready for dev</li>\n");
            sb.Append("        <li><code>/bmad-dev-story</code> / <code>/bmad-quick-dev</code> — implement</li>\n");
            sb.Append("        <li><code>/bmad-code-review</code> — review</li>\n");
            sb.Append("        <li><code>/bmad-retrospective</code> — epic retrospective</li>\n");
            sb.Append("      </ul>\n");
            sb.Append("      <h3>Methodology</h3>\n");
            sb.Append(Mermaid.Block(Mermaid.SddMethodDiagram()));
        }
        else
        {
            sb.Append("      <p class=\"sdd-absent-info\">BMad Method is not installed in this repository.</p>\n");
            sb.Append("      <p><a href=\"https://github.com/bmad-code-org/BMAD-METHOD\">BMad Method documentation</a></p>\n");
            sb.Append("      <pre class=\"sdd-install\"><code>npx bmad-method install</code></pre>\n");
        }
        sb.Append("    </div>\n");
    }

    private static void AppendGdsPanel(StringBuilder sb, bool present)
    {
        sb.Append("    <div class=\"sdd-tabpanel sdd-tabpanel--gds\">\n");
        if (present)
        {
            sb.Append("      <h3>BMad GDS — Commands</h3>\n");
            sb.Append("      <ul class=\"sdd-commands\">\n");
            sb.Append("        <li><code>/bmad-help</code> — guided help</li>\n");
            sb.Append("        <li><code>/bmgd-gdd</code> — game design document</li>\n");
            sb.Append("        <li><code>/bmgd-narrative</code> — narrative design</li>\n");
            sb.Append("        <li><code>/bmgd-quick-dev</code> — prototype / quick flow</li>\n");
            sb.Append("      </ul>\n");
            sb.Append("      <h3>Methodology</h3>\n");
            sb.Append(Mermaid.Block(Mermaid.SddGdsDiagram()));
        }
        else
        {
            sb.Append("      <p class=\"sdd-absent-info\">BMad Game Dev Studio is not installed in this repository.</p>\n");
            sb.Append("      <p><a href=\"https://github.com/bmad-code-org/bmad-module-game-dev-studio\">BMad Game Dev Studio documentation</a></p>\n");
            sb.Append("      <pre class=\"sdd-install\"><code>npx bmad-method install --modules gds</code></pre>\n");
        }
        sb.Append("    </div>\n");
    }

    private static void AppendComingSoonPanel(StringBuilder sb, string id, string label)
    {
        sb.Append($"    <div class=\"sdd-tabpanel sdd-tabpanel--{id}\">\n");
        sb.Append($"      <p class=\"sdd-coming-soon\">{PathUtil.Html(label)} — Coming Soon</p>\n");
        sb.Append("    </div>\n");
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
