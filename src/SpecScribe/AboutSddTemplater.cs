using System.Text;

namespace SpecScribe;

/// <summary>Renders the About Spec-Driven Development hub and per-framework sub-pages. The hub carries a
/// brief checkbox support matrix; each framework page covers intro, SpecScribe support, detection, commands,
/// and a vertical methodology state diagram (supported frameworks only). Always written. [About SDD]</summary>
public static class AboutSddTemplater
{
    public static readonly (string Id, string Label, string OutputPath, bool Supported)[] Frameworks =
    [
        ("bmad", "BMad", SiteNav.AboutSddBmadOutputPath, true),
        ("gds", "BMad GDS", SiteNav.AboutSddGdsOutputPath, true),
        ("speckit", "Spec Kit", SiteNav.AboutSddSpecKitOutputPath, false),
        ("gsd", "GSD", SiteNav.AboutSddGsdOutputPath, false),
        ("gsd-pi", "GSD-Pi", SiteNav.AboutSddGsdPiOutputPath, false),
        ("superpowers", "Superpowers", SiteNav.AboutSddSuperpowersOutputPath, false),
    ];

    public static string RenderHub(SiteNav nav, bool methodPresent, bool gdsPresent)
    {
        var outputPath = SiteNav.AboutSddOutputPath;
        var sb = Begin(nav, outputPath, "About Spec-Driven Development",
            "Which Spec-Driven Development frameworks SpecScribe understands, and how to get started.");

        sb.Append("  <p>Spec-Driven Development (SDD) means planning and shipping with AI-assisted methodologies ");
        sb.Append("that keep briefs, requirements, stories, and decisions as first-class artifacts. SpecScribe ");
        sb.Append("renders those artifacts into this portal.</p>\n");
        sb.Append("  <p>Use the matrix below for a quick support snapshot, then open a framework page for ");
        sb.Append("orientation, install steps, and what SpecScribe can show today.</p>\n");

        AppendSupportMatrix(sb);

        sb.Append("  <h2 id=\"frameworks\">Framework guides</h2>\n");
        sb.Append("  <ul class=\"sdd-framework-links\">\n");
        foreach (var fw in Frameworks)
        {
            var detected = fw.Id switch
            {
                "bmad" => methodPresent,
                "gds" => gdsPresent,
                _ => false,
            };
            sb.Append("    <li>");
            sb.Append($"<a href=\"{PathUtil.Html(fw.OutputPath)}\">{PathUtil.Html(fw.Label)}</a>");
            if (fw.Supported)
            {
                sb.Append(" <span class=\"sdd-support-yes\">Supported</span>");
                if (detected)
                    sb.Append(" <span class=\"sdd-detected\">Detected</span>");
            }
            else
            {
                sb.Append(" <span class=\"sdd-support-soon\">Coming soon</span>");
            }
            sb.Append("</li>\n");
        }
        sb.Append("  </ul>\n");

        return End(sb, hasMermaid: false);
    }

    public static string RenderFrameworkPage(SiteNav nav, string frameworkId, bool methodPresent, bool gdsPresent)
    {
        var fw = Frameworks.First(f => f.Id == frameworkId);
        var detected = frameworkId switch
        {
            "bmad" => methodPresent,
            "gds" => gdsPresent,
            _ => false,
        };

        var sb = Begin(nav, fw.OutputPath, fw.Label,
            $"About {fw.Label} for Spec-Driven Development — orientation, SpecScribe support, and getting started.");

        if (detected)
            sb.Append("  <p class=\"sdd-detected-banner\" role=\"status\"><span class=\"sdd-detected\">Detected</span> in this project</p>\n");

        switch (frameworkId)
        {
            case "bmad":
                AppendBmadBody(sb, detected);
                return End(sb, hasMermaid: true);
            case "gds":
                AppendGdsBody(sb, detected);
                return End(sb, hasMermaid: true);
            default:
                AppendComingSoonBody(sb, fw.Label);
                return End(sb, hasMermaid: false);
        }
    }

    /// <summary>Checkbox matrix columns mirror <see cref="ArtifactBundle"/> projection families
    /// (Epics/Stories, Requirements, Sprint, Retros, Module planning docs) plus next-step Commands from
    /// <see cref="ModuleContext"/> — the nouns the adapter contract already uses. [About SDD]</summary>
    private static void AppendSupportMatrix(StringBuilder sb)
    {
        sb.Append("  <h2 id=\"support-matrix\">SpecScribe support</h2>\n");
        sb.Append("  <p>Checkbox view of which artifact families SpecScribe can project today ");
        sb.Append("(the same nouns as the shared adapter contract). Empty cells are placeholders for ");
        sb.Append("future framework adapters.</p>\n");
        sb.Append("  <table class=\"sdd-support-matrix\">\n");
        sb.Append("    <thead><tr>");
        sb.Append("<th>Framework</th>");
        sb.Append("<th>Epics &amp; Stories</th>");
        sb.Append("<th>Requirements</th>");
        sb.Append("<th>Sprint</th>");
        sb.Append("<th>Retros</th>");
        sb.Append("<th>Planning docs</th>");
        sb.Append("<th>Commands</th>");
        sb.Append("</tr></thead>\n");
        sb.Append("    <tbody>\n");
        // BMad / BMad GDS both ride BmadArtifactAdapter → full ArtifactBundle + CommandCatalog.
        AppendMatrixRow(sb, "BMad", SiteNav.AboutSddBmadOutputPath, true);
        AppendMatrixRow(sb, "BMad GDS", SiteNav.AboutSddGdsOutputPath, true);
        AppendMatrixRow(sb, "Spec Kit", SiteNav.AboutSddSpecKitOutputPath, false);
        AppendMatrixRow(sb, "GSD", SiteNav.AboutSddGsdOutputPath, false);
        AppendMatrixRow(sb, "GSD-Pi", SiteNav.AboutSddGsdPiOutputPath, false);
        AppendMatrixRow(sb, "Superpowers", SiteNav.AboutSddSuperpowersOutputPath, false);
        sb.Append("    </tbody>\n  </table>\n");
    }

    private static void AppendMatrixRow(StringBuilder sb, string label, string href, bool supported)
    {
        sb.Append("      <tr>");
        sb.Append($"<th scope=\"row\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(label)}</a></th>");
        for (var i = 0; i < 6; i++)
            sb.Append($"<td>{Check(supported)}</td>");
        sb.Append("</tr>\n");
    }

    private static string Check(bool yes) => yes
        ? "<span class=\"sdd-check\" aria-label=\"Yes\">✓</span>"
        : "<span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span>";

    private static void AppendFamilySupportTable(StringBuilder sb, bool supported)
    {
        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--compact\">\n");
        sb.Append("    <tbody>\n");
        AppendCompactRow(sb, "Epics &amp; Stories", supported);
        AppendCompactRow(sb, "Requirements", supported);
        AppendCompactRow(sb, "Sprint", supported);
        AppendCompactRow(sb, "Retros", supported);
        AppendCompactRow(sb, "Planning docs", supported);
        AppendCompactRow(sb, "Commands", supported);
        sb.Append("    </tbody>\n  </table>\n");
    }

    private static void AppendCompactRow(StringBuilder sb, string label, bool supported) =>
        sb.Append($"      <tr><th scope=\"row\">{label}</th><td>{Check(supported)}</td></tr>\n");

    private static void AppendBmadBody(StringBuilder sb, bool detected)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>BMad</strong> (BMad Method) is an AI-assisted methodology for product briefs, ");
        sb.Append("PRDs, epics, stories, and retrospectives. Choose it when you want a full planning → delivery ");
        sb.Append("spine with slash-command workflows in your editor.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Install into a repo, then run the help skill to pick your next step:</p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install</code></pre>\n");
        sb.Append("  <p>See <a href=\"https://github.com/bmad-code-org/BMAD-METHOD\">the official documentation</a> ");
        sb.Append("for more information and installation options.</p>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad is not detected in this repository yet (_bmad/bmm).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe projects BMad through the shared adapter contract: epics &amp; stories, ");
        sb.Append("requirements, sprint, retros, planning docs, and next-step commands when those artifacts exist.</p>\n");
        AppendFamilySupportTable(sb, supported: true);

        sb.Append("  <h2 id=\"commands\">Common commands</h2>\n");
        sb.Append("  <ul class=\"sdd-commands\">\n");
        sb.Append("    <li><code>/bmad-help</code> — guided help</li>\n");
        sb.Append("    <li><code>/bmad-product-brief</code> — product brief</li>\n");
        sb.Append("    <li><code>/bmad-prd</code> — PRD</li>\n");
        sb.Append("    <li><code>/bmad-create-epics-and-stories</code> — epics &amp; stories</li>\n");
        sb.Append("    <li><code>/bmad-create-story</code> — story ready for dev</li>\n");
        sb.Append("    <li><code>/bmad-dev-story</code> / <code>/bmad-quick-dev</code> — implement</li>\n");
        sb.Append("    <li><code>/bmad-code-review</code> — review</li>\n");
        sb.Append("    <li><code>/bmad-correct-course</code> — adjust mid-sprint when scope shifts</li>\n");
        sb.Append("    <li><code>/bmad-retrospective</code> — epic retrospective</li>\n");
        sb.Append("  </ul>\n");

        sb.Append("  <h2 id=\"methodology\">Methodology</h2>\n");
        sb.Append("  <p>Typical progression: plan once, then loop create → develop → review for each story ");
        sb.Append("inside a sprint (with optional course correction), and close with a retrospective.</p>\n");
        sb.Append(Mermaid.Block(Mermaid.SddMethodDiagram()));
    }

    private static void AppendGdsBody(StringBuilder sb, bool detected)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>BMad GDS</strong> (Game Dev Studio) adapts BMad for game development — GDD, ");
        sb.Append("narrative, and quick-flow prototyping across Unity, Unreal, and Godot. Choose it when the ");
        sb.Append("primary artifacts are game design docs rather than a software PRD spine.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Add the GDS module during BMad install (or to an existing install):</p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install --modules gds</code></pre>\n");
        sb.Append("  <p>See <a href=\"https://github.com/bmad-code-org/bmad-module-game-dev-studio\">the official documentation</a> ");
        sb.Append("for more information and installation options.</p>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad GDS is not detected in this repository yet (_bmad/gds).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe projects BMad GDS through the same adapter families as BMad — including ");
        sb.Append("GDD / narrative / architecture planning docs and GDS-oriented commands when installed.</p>\n");
        AppendFamilySupportTable(sb, supported: true);

        sb.Append("  <h2 id=\"commands\">Common commands</h2>\n");
        sb.Append("  <ul class=\"sdd-commands\">\n");
        sb.Append("    <li><code>/bmad-help</code> — guided help</li>\n");
        sb.Append("    <li><code>/bmgd-gdd</code> — game design document</li>\n");
        sb.Append("    <li><code>/bmgd-narrative</code> — narrative design</li>\n");
        sb.Append("    <li><code>/bmgd-quick-dev</code> — prototype / quick flow</li>\n");
        sb.Append("  </ul>\n");

        sb.Append("  <h2 id=\"methodology\">Methodology</h2>\n");
        sb.Append("  <p>Typical progression through GDS workflows:</p>\n");
        sb.Append(Mermaid.Block(Mermaid.SddGdsDiagram()));
    }

    private static void AppendComingSoonBody(StringBuilder sb, string label)
    {
        sb.Append("  <h2 id=\"overview\">Coming soon</h2>\n");
        sb.Append($"  <p>{PathUtil.Html(label)} support in SpecScribe is planned. This page is a placeholder ");
        sb.Append("so the framework roster stays honest while adapters land in later epics.</p>\n");
        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        AppendFamilySupportTable(sb, supported: false);
    }

    private static StringBuilder Begin(SiteNav nav, string outputPath, string title, string description)
    {
        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"{title} — {nav.SiteTitle}",
            ForgeOptions.StylesheetName, ForgeOptions.ScriptName,
            description));
        sb.Append(nav.RenderNavBar(outputPath, nav.BuildSddLocalContext(outputPath)));

        var trail = new List<(string, string?)>
        {
            ("Home", SiteNav.HomeOutputPath),
        };
        if (!string.Equals(outputPath, SiteNav.AboutSddOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            trail.Add(("About Spec-Driven Development", SiteNav.AboutSddOutputPath));
            trail.Add((title, null));
        }
        else
        {
            trail.Add(("About Spec-Driven Development", null));
        }
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, trail));

        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(description)}</div>\n");
        sb.Append("</header>\n\n");
        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel about-sdd-panel\">\n");
        return sb;
    }

    private static string End(StringBuilder sb, bool hasMermaid)
    {
        sb.Append("</section>\n");
        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter());
        if (hasMermaid)
            sb.Append(Mermaid.InitScript());
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }
}
