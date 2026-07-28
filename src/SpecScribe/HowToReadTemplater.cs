using System.Text;

namespace SpecScribe;

/// <summary>Renders the "How to use SpecScribe" orientation page (<c>how-to-read.html</c>): a suggested
/// reading order through the pages that actually exist, how to generate and refresh the site from the
/// command line, plus a glossary of the detected module's vocabulary.
/// Written on every full run so its Help-nav link never 404s, and — like About/Diagnostics — it is written
/// directly rather than through <c>ApplyReferenceLinks</c>, so it never self-expands the glossary terms it
/// defines. [Story 10.3; Story 5.6; Help nav; How to use SpecScribe]</summary>
public static class HowToReadTemplater
{
    /// <param name="module">The detected module context, passed WHOLE rather than as its three projections:
    /// since Story 18.2 the glossary and command-legend sections need the module's identity state and label,
    /// not just its term list, and threading two more positional parameters through would have made the
    /// signature a list of facts that must be kept mutually consistent by every caller. [Story 18.2]</param>
    public static string RenderPage(SiteNav nav, ModuleContext module) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(nav, module)).Content;

    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — see
    /// <see cref="RiskQuadrantTemplater.BuildPage"/> for why every standalone templater grew one.
    /// <para>⚠️ <b>The body starts at <c>&lt;header class="doc-header"&gt;</c>, not at <c>&lt;main&gt;</c>.</b>
    /// This page emits a doc-header BEFORE the landmark, and the slice this composition replaces began at the
    /// breadcrumb — so a body that started at <c>&lt;main&gt;</c> would silently drop the page's own title block
    /// from the IR while the static page still showed it. <see cref="PageView.BodyHtml"/> is everything between
    /// the wayfinding band and the footer.</para> [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(SiteNav nav, ModuleContext module)
    {
        var moduleDocs = module.Docs;
        var outputPath = SiteNav.HowToReadOutputPath;

        var sb = new StringBuilder();
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append("  <h1>How to use SpecScribe</h1>\n");

        // Build sections first so the header subtitle + intro can tell the truth when every MODULE-DERIVED
        // append is a no-op (undetected module + no reading-order pages) — never promise content that doesn't
        // exist. The Generate section is deliberately outside that signal: the CLI exists no matter what was
        // detected, so it renders in both branches and the page is never actually empty. Split into two
        // builders only so the unconditional section can sit between them in reading position. [Story 5.6]
        var readingOrder = new StringBuilder();
        AppendReadingOrder(readingOrder, nav, moduleDocs);

        var reference = new StringBuilder();
        var glossary = AppendGlossary(reference, module);

        // Tracked BEFORE the legend is appended, so "did the glossary contribute real TERMS" survives the next
        // append rather than being re-derived from a buffer length. [Review][Patch P2]
        var hasGlossaryTerms = glossary == GlossarySection.Terms;
        AppendCommandLegend(reference, module);

        // The unmodeled ACKNOWLEDGEMENT must not be evidence that a glossary exists. It ALWAYS renders for an
        // unmodeled module, so treating it as content made the subtitle and intro promise "the reading order
        // and glossary below" and "what the recurring terms mean" on a page whose glossary section says
        // SpecScribe publishes no glossary for this module. Story 5.6's rule: a section that always renders for
        // a given state cannot be the signal for whether there is content.
        //
        // Two signals, not one, because they fail independently — an unmodeled repo routinely HAS a reading
        // order (epics, ADRs, README) while never having a glossary, so a single boolean cannot keep both
        // halves of that sentence honest. The None path is unchanged. [Review][Patch P2]
        var hasModuleContent = readingOrder.Length > 0 || hasGlossaryTerms;

        var sections = new StringBuilder();
        sections.Append(readingOrder);
        AppendGenerateSection(sections);
        sections.Append(reference);

        if (hasModuleContent && hasGlossaryTerms)
        {
            sb.Append("  <div class=\"doc-subtitle\">New here? Start with the reading order and glossary below, then generate the site yourself.</div>\n");
        }
        else if (hasModuleContent)
        {
            sb.Append("  <div class=\"doc-subtitle\">New here? Start with the reading order below, then generate the site yourself.</div>\n");
        }
        else
        {
            sb.Append("  <div class=\"doc-subtitle\">Orientation for a first visit — including how to generate this site yourself.</div>\n");
        }
        sb.Append("</header>\n\n");

        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel howtoread-panel\">\n");

        if (hasModuleContent)
        {
            sb.Append("  <p>This portal documents a project built with an AI-assisted development methodology. ");
            sb.Append("If you're new to it, the sections below walk you through what to read first, how to rebuild ");
            sb.Append(hasGlossaryTerms
                ? "this site yourself, and what the recurring terms mean. "
                : "this site yourself. ");
            sb.Append("For framework overviews and SpecScribe ");
            sb.Append($"support, see <a href=\"{SiteNav.AboutSddOutputPath}\">About Spec-Driven Development</a>.</p>\n");
        }
        else
        {
            sb.Append("  <p>Orientation content appears as the project grows. The section below covers generating ");
            sb.Append("and refreshing this site from the command line; for frameworks SpecScribe can work with, see ");
            sb.Append($"<a href=\"{SiteNav.AboutSddOutputPath}\">About Spec-Driven Development</a>.</p>\n");
        }

        sb.Append(sections);

        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.About,
            OutputRelativePath = outputPath,
            Title = $"How to use SpecScribe — {nav.SiteTitle}",
            MetaDescription = $"Orientation for a first visit to {nav.SiteTitle}'s documentation portal: a suggested reading order, how to generate and refresh the site from the command line, and a glossary of the terms used throughout.",
            Nav = nav.ToNavigationView(outputPath),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[]
            {
                ("Home", SiteNav.HomeOutputPath),
                ("How to use SpecScribe", null),
            }),
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

    /// <summary>How to produce and refresh the site the reader is standing in: the two commands, the
    /// no-flags-needed default, the three path overrides, and where the effective settings live. Unconditional —
    /// unlike every other section here it has no availability gate, because the CLI it documents is the same
    /// regardless of which methodology (if any) was detected. Framework-agnostic by the same discipline
    /// <see cref="AppendReadingOrder"/> follows (NFR8): it names the <see cref="DiagnosticsConfig"/> field labels
    /// a reader will actually see on <c>diagnostics.html</c>, never a specific framework's folder names. Deliberately
    /// does NOT reproduce the flag table — <c>--help</c> is the one surface that can't drift. [Story 5.6]</summary>
    private static void AppendGenerateSection(StringBuilder sb)
    {
        sb.Append("  <h2 id=\"generate\">Generate with SpecScribe</h2>\n");

        sb.Append("  <p><code>specscribe generate</code> builds this site once. <code>specscribe watch</code> keeps ");
        sb.Append("going: it rebuilds after every source change, so a browser tab left open stays current while ");
        sb.Append("you edit.</p>\n");

        sb.Append("  <p>In a conventional repository layout neither command needs a flag — SpecScribe walks up from ");
        sb.Append("the current directory to find the source root and writes the site beside it. For a different ");
        sb.Append("layout, <code>--source</code> sets the source root, <code>--adrs</code> the decision-record ");
        sb.Append("directory, and <code>--output</code> the directory the HTML is written to. Run ");
        sb.Append("<code>specscribe generate --help</code> for the full option list.</p>\n");

        sb.Append("  <p>The values a run actually uses are the ones reported on ");
        sb.Append($"<a href=\"{SiteNav.DiagnosticsOutputPath}\">Diagnostics</a>: Source root, ADR location, Output ");
        sb.Append("directory, README included, Deep-git analytics, and External source base. Save them per ");
        sb.Append("repository from the interactive menu's \"Configure paths\", which writes a <code>.specscribe</code> ");
        sb.Append("folder beside your project — one per checkout, so ignore it in version control if you'd rather not ");
        sb.Append("share your local paths. A flag on the command line always wins over a saved value for that one ");
        sb.Append("run, and <code>specscribe generate --show-config</code> prints where each value came from without ");
        sb.Append("generating anything.</p>\n");
    }

    /// <summary>The module's vocabulary as a definition list, acronyms first (stable sort preserves each
    /// group's declared order). Omitted entirely — not rendered empty — when the module publishes no
    /// glossary (an undetected framework), so AC2/NFR8 never renders an empty-but-present section.
    /// <para>Between those two states sits a third: a module SpecScribe recognized but does not model. That
    /// gets a NAMED acknowledgement rather than silent omission (owner design call) — an invisible section
    /// leaves a Test Architect user unable to tell whether the portal is honest or broken. The heading and its
    /// <c>#glossary</c> anchor still render so in-page links don't break; only the <c>&lt;dl&gt;</c> is
    /// replaced. Gated on BOTH the unmodeled state and a real label: <see cref="CommandCatalog.Empty"/> is
    /// <see cref="ModuleContext.None"/>'s catalog, so a state-only gate would have announced a module on a
    /// repo with no BMad install at all. [Story 18.2; ADR 0015 Decision 2c]</para></summary>
    /// <summary>Which of the three glossary states was rendered, so <see cref="RenderPage"/>'s
    /// "is there content" signal can tell a real term list from the named acknowledgement — the latter always
    /// renders for an unmodeled module and therefore cannot serve as evidence that content exists.
    /// [Review][Patch P2]</summary>
    private enum GlossarySection { None, Acknowledgement, Terms }

    private static GlossarySection AppendGlossary(StringBuilder sb, ModuleContext module)
    {
        if (module.IsUnmodeled && module.Commands.HasLabel)
        {
            sb.Append("  <h2 id=\"glossary\">Glossary</h2>\n");
            sb.Append($"  <p>This project uses the <strong>{PathUtil.Html(module.Commands.ModuleLabel)}</strong> ");
            sb.Append("module. SpecScribe doesn't publish a glossary for it yet.</p>\n");
            return GlossarySection.Acknowledgement;
        }

        var glossary = module.Glossary;
        if (glossary.Count == 0)
        {
            return GlossarySection.None;
        }

        sb.Append("  <h2 id=\"glossary\">Glossary</h2>\n");
        sb.Append("  <dl class=\"howtoread-glossary\">\n");
        foreach (var term in glossary.OrderByDescending(g => g.IsAcronym))
        {
            sb.Append($"    <div class=\"cap-row\"><dt>{PathUtil.Html(term.Term)}</dt><dd>{PathUtil.Html(term.Definition)}</dd></div>\n");
        }
        sb.Append("  </dl>\n");
        return GlossarySection.Terms;
    }

    /// <summary>A light-touch note that the slash commands seen on story/epic pages come from the detected
    /// methodology — not a full command enumeration (the story pages already caption each one). Omitted
    /// when no module was detected, so an undetected framework never claims a methodology it doesn't have.
    /// <para>Gated on a MODELED primary, not merely a non-empty catalog. The sentence promises commands "like
    /// the ones captioned on story and epic pages", and those captions come from surfaces (story pages, epic
    /// pages, the sprint board) that only exist when epics and stories do — which only the two modeled modules
    /// produce. An unmodeled module parses a perfectly real catalog whose commands are captioned nowhere, so
    /// the legend would point at pages the reader will never see. [Story 18.2; ADR 0015 Decision 2c]</para></summary>
    private static void AppendCommandLegend(StringBuilder sb, ModuleContext module)
    {
        if (!module.IsModeled || module.Commands.IsEmpty || !module.Commands.HasLabel)
        {
            return;
        }

        sb.Append("  <h2 id=\"commands\">Commands you'll see</h2>\n");
        sb.Append($"  <p>Slash commands like the ones captioned on story and epic pages come from your detected methodology, {PathUtil.Html(module.Commands.ModuleLabel)}.</p>\n");
    }
}
