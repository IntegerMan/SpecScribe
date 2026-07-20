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

        AppendSupportMatrix(sb, methodPresent, gdsPresent);

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
                sb.Append(detected ? " <span class=\"sdd-detected\">Detected in this project</span>" : " <span class=\"sdd-support-yes\">Supported</span>");
            else
                sb.Append(" <span class=\"sdd-support-soon\">Coming soon</span>");
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
            sb.Append("  <p class=\"sdd-detected-banner\" role=\"status\"><span class=\"sdd-detected\">Detected in this project</span></p>\n");

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

    private static void AppendSupportMatrix(StringBuilder sb, bool methodPresent, bool gdsPresent)
    {
        sb.Append("  <h2 id=\"support-matrix\">SpecScribe support</h2>\n");
        sb.Append("  <p>A brief checkbox view of what SpecScribe covers today. Empty cells mean not yet ");
        sb.Append("supported — placeholders for future framework epics.</p>\n");
        sb.Append("  <table class=\"sdd-support-matrix\">\n");
        sb.Append("    <thead><tr><th>Framework</th><th>Portal</th><th>Artifacts</th><th>Commands</th><th>In this project</th></tr></thead>\n");
        sb.Append("    <tbody>\n");
        AppendMatrixRow(sb, "BMad", true, true, true, methodPresent, SiteNav.AboutSddBmadOutputPath);
        AppendMatrixRow(sb, "BMad GDS", true, true, true, gdsPresent, SiteNav.AboutSddGdsOutputPath);
        AppendMatrixRow(sb, "Spec Kit", false, false, false, false, SiteNav.AboutSddSpecKitOutputPath);
        AppendMatrixRow(sb, "GSD", false, false, false, false, SiteNav.AboutSddGsdOutputPath);
        AppendMatrixRow(sb, "GSD-Pi", false, false, false, false, SiteNav.AboutSddGsdPiOutputPath);
        AppendMatrixRow(sb, "Superpowers", false, false, false, false, SiteNav.AboutSddSuperpowersOutputPath);
        sb.Append("    </tbody>\n  </table>\n");
    }

    private static void AppendMatrixRow(StringBuilder sb, string label, bool portal, bool artifacts, bool commands, bool detected, string href)
    {
        sb.Append("      <tr>");
        sb.Append($"<th scope=\"row\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(label)}</a></th>");
        sb.Append($"<td>{Check(portal)}</td><td>{Check(artifacts)}</td><td>{Check(commands)}</td><td>{Check(detected)}</td>");
        sb.Append("</tr>\n");
    }

    private static string Check(bool yes) => yes
        ? "<span class=\"sdd-check\" aria-label=\"Yes\">✓</span>"
        : "<span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span>";

    private static void AppendBmadBody(StringBuilder sb, bool detected)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>BMad</strong> (BMad Method) is an AI-assisted methodology for product briefs, ");
        sb.Append("PRDs, epics, stories, and retrospectives. Choose it when you want a full planning → delivery ");
        sb.Append("spine with slash-command workflows in your editor.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Install into a repo, then run the help skill to pick your next step:</p>\n");
        sb.Append("  <p><a href=\"https://github.com/bmad-code-org/BMAD-METHOD\">BMad Method documentation</a></p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install</code></pre>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad is not detected in this repository yet (_bmad/bmm).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe fully supports BMad today: planning docs, epics/stories, sprint status, ");
        sb.Append("ADRs, next-step commands, glossary, and dashboard insights when those artifacts exist.</p>\n");
        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--compact\">\n");
        sb.Append("    <tbody>\n");
        sb.Append("      <tr><th scope=\"row\">Portal pages</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Core artifacts</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Next-step commands</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("    </tbody>\n  </table>\n");

        sb.Append("  <h2 id=\"commands\">Common commands</h2>\n");
        sb.Append("  <ul class=\"sdd-commands\">\n");
        sb.Append("    <li><code>/bmad-help</code> — guided help</li>\n");
        sb.Append("    <li><code>/bmad-product-brief</code> — product brief</li>\n");
        sb.Append("    <li><code>/bmad-prd</code> — PRD</li>\n");
        sb.Append("    <li><code>/bmad-create-epics-and-stories</code> — epics &amp; stories</li>\n");
        sb.Append("    <li><code>/bmad-create-story</code> — story ready for dev</li>\n");
        sb.Append("    <li><code>/bmad-dev-story</code> / <code>/bmad-quick-dev</code> — implement</li>\n");
        sb.Append("    <li><code>/bmad-code-review</code> — review</li>\n");
        sb.Append("    <li><code>/bmad-retrospective</code> — epic retrospective</li>\n");
        sb.Append("  </ul>\n");

        sb.Append("  <h2 id=\"methodology\">Methodology</h2>\n");
        sb.Append("  <p>Typical progression through BMad workflows:</p>\n");
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
        sb.Append("  <p><a href=\"https://github.com/bmad-code-org/bmad-module-game-dev-studio\">BMad Game Dev Studio documentation</a></p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install --modules gds</code></pre>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad GDS is not detected in this repository yet (_bmad/gds).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe supports BMad GDS today: GDD / narrative / architecture docs, module ");
        sb.Append("vocabulary, and GDS-oriented next-step commands when the module is installed.</p>\n");
        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--compact\">\n");
        sb.Append("    <tbody>\n");
        sb.Append("      <tr><th scope=\"row\">Portal pages</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Core artifacts</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Next-step commands</th><td><span class=\"sdd-check\" aria-label=\"Yes\">✓</span></td></tr>\n");
        sb.Append("    </tbody>\n  </table>\n");

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
        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--compact\">\n");
        sb.Append("    <tbody>\n");
        sb.Append("      <tr><th scope=\"row\">Portal pages</th><td><span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Core artifacts</th><td><span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span></td></tr>\n");
        sb.Append("      <tr><th scope=\"row\">Next-step commands</th><td><span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span></td></tr>\n");
        sb.Append("    </tbody>\n  </table>\n");
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
