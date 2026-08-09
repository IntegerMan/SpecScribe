using System.Text;

namespace SpecScribe;

/// <summary>Renders the About Spec-Driven Development hub and per-framework sub-pages. The hub carries a
/// brief checkbox support matrix; each framework page covers intro, SpecScribe support, detection, commands,
/// and a vertical methodology state diagram (supported frameworks only). Always written. [About SDD]</summary>
public static class AboutSddTemplater
{
    /// <summary>The framework roster. <c>Url</c> is the framework's CANONICAL documentation home and
    /// <c>Blurb</c> a one-paragraph statement of what the framework actually is — both null where SpecScribe has
    /// not pinned them yet, which is a visible choice rather than a silent omission.
    ///
    /// <para>⚠️ <c>Url</c>/<c>Blurb</c> exist because a roster of bare ids and labels proved genuinely ambiguous.
    /// Story 12.1's spike found that "GSD" had no canonical URL recorded here OR in <c>README.md</c>, and that the
    /// ambiguity had already produced a wrong answer: the story was drafted against <c>gsd-build/gsd-2</c> — the
    /// RETIRED predecessor, which "now continues as GSD Pi" — instead of GSD Core, the current-version product.
    /// The two GSD entries are DISTINCT products, not two versions of one: <b>GSD Core</b> is markdown-native
    /// under <c>.planning/</c> with no database, while <b>GSD Pi</b> is SQLite-authoritative under <c>.gsd/</c>
    /// with the markdown as rendered projections. Keep them pinned. [Story 12.1]</para>
    ///
    /// <para><c>Blurb</c> is a pre-composed HTML fragment (it carries <c>&lt;code&gt;</c> markers), not escaped
    /// text — matching how every other body string in this templater is built.</para>
    ///
    /// <para><c>Label</c> deliberately stays "GSD"/"GSD-Pi" rather than becoming "GSD Core"/"GSD Pi": the labels
    /// are load-bearing for nav pills and page titles, and the disambiguation the roster needed is carried by
    /// <c>Url</c> + <c>Blurb</c>. Renaming the labels is a separate, owner-facing display decision.</para></summary>
    public static readonly (string Id, string Label, string OutputPath, bool Supported, string? Url, string? Blurb)[] Frameworks =
    [
        ("bmad", "BMad", SiteNav.AboutSddBmadOutputPath, true,
            "https://github.com/bmad-code-org/BMAD-METHOD", null),
        ("gds", "BMad GDS", SiteNav.AboutSddGdsOutputPath, true,
            "https://github.com/bmad-code-org/bmad-module-game-dev-studio", null),
        ("speckit", "Spec Kit", SiteNav.AboutSddSpecKitOutputPath, false,
            "https://github.com/github/spec-kit",
            "<strong>GitHub Spec Kit</strong> drives development from a specification: a <code>.specify/</code> "
            + "install marker plus numbered <code>specs/&lt;NNN&gt;-slug/</code> folders holding a spec, plan, and "
            + "task breakdown, authored through <code>/speckit.*</code> commands."),
        // Supported since Story 12.2: GsdCoreArtifactAdapter projects .planning/ROADMAP.md into epics & stories and
        // STATE.md into the sprint ledger. Support is per-noun, not blanket — see FamilyMatrix.GsdCore.
        ("gsd", "GSD", SiteNav.AboutSddGsdOutputPath, true,
            "https://docs.opengsd.net/core",
            "<strong>GSD Core</strong> (Get Shit Done) is a spec-driven framework layered on your existing AI "
            + "coding runtime as <code>/gsd-*</code> slash commands. It keeps every artifact as plain Markdown and "
            + "JSON in a <code>.planning/</code> directory &mdash; project brief, requirements, roadmap, and live "
            + "state at the root, then one folder per phase &mdash; and decomposes work as Milestone &rarr; Phase "
            + "&rarr; Task. There is no database: what is on disk is the project."),
        ("gsd-pi", "GSD-Pi", SiteNav.AboutSddGsdPiOutputPath, false,
            "https://docs.opengsd.net/pi",
            "<strong>GSD Pi</strong> is Get Shit Done's autonomous agent CLI &mdash; the successor to GSD 2 &mdash; "
            + "and it stores state differently from GSD Core. A SQLite database at <code>.gsd/gsd.db</code> is the "
            + "single source of truth, and the Markdown beside it (<code>.gsd/</code> state, decisions, knowledge, "
            + "and <code>milestones/</code>) is <em>rendered from</em> that database. Work decomposes as Milestone "
            + "&rarr; Slice &rarr; Task. SpecScribe reads the Markdown projections, never the database."),
        ("superpowers", "Superpowers", SiteNav.AboutSddSuperpowersOutputPath, false, null, null),
    ];

    /// <summary>Builds the hub page's host-neutral <see cref="PageView"/> — the AD-2 delivery contract, so the
    /// IR's content region can be COMPOSED (<see cref="JsonSpaRenderAdapter.RenderContent"/>) instead of sliced
    /// back out of a rendered full page. [Story 23.4 AC #3]</summary>
    public static PageView BuildHubPage(SiteNav nav, bool methodPresent, bool gdsPresent, bool gsdPresent = false)
    {
        var outputPath = SiteNav.AboutSddOutputPath;
        var page = Begin(nav, outputPath, "About Spec-Driven Development",
            "Which Spec-Driven Development frameworks SpecScribe understands, and how to get started.");
        var sb = page.Body;

        sb.Append("  <p>Spec-Driven Development (SDD) means planning and shipping with AI-assisted methodologies ");
        sb.Append("that keep briefs, requirements, stories, and decisions as first-class artifacts. SpecScribe ");
        sb.Append("renders those artifacts into this portal.</p>\n");
        sb.Append("  <p>Use the matrix below for a quick support snapshot, then open a framework page for ");
        sb.Append("orientation, install steps, and what SpecScribe can show today. BMad itself installs as ");
        sb.Append("several modules — support varies by module, and ");
        sb.Append($"<a href=\"{PathUtil.Html(SiteNav.AboutSddBmadOutputPath)}#modules\">BMad &rsaquo; Modules</a> ");
        sb.Append("says which does what.</p>\n");

        AppendSupportMatrix(sb);

        sb.Append("  <h2 id=\"frameworks\">Framework guides</h2>\n");
        sb.Append("  <ul class=\"sdd-framework-links\">\n");
        foreach (var fw in Frameworks)
        {
            var detected = fw.Id switch
            {
                "bmad" => methodPresent,
                "gds" => gdsPresent,
                "gsd" => gsdPresent,
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

        return End(page, hasMermaid: false);
    }

    /// <summary>Builds a framework page's host-neutral <see cref="PageView"/> — see <see cref="BuildHubPage"/>.
    /// [Story 23.4 AC #3]</summary>
    public static PageView BuildFrameworkPage(
        SiteNav nav, string frameworkId, bool methodPresent, bool gdsPresent, bool gsdPresent = false)
    {
        var fw = Frameworks.First(f => f.Id == frameworkId);
        var detected = frameworkId switch
        {
            "bmad" => methodPresent,
            "gds" => gdsPresent,
            "gsd" => gsdPresent,
            _ => false,
        };

        var page = Begin(nav, fw.OutputPath, fw.Label,
            $"About {fw.Label} for Spec-Driven Development — orientation, SpecScribe support, and getting started.");
        var sb = page.Body;

        if (detected)
            sb.Append("  <p class=\"sdd-detected-banner\" role=\"status\"><span class=\"sdd-detected\">Detected</span> in this project</p>\n");

        switch (frameworkId)
        {
            case "bmad":
                AppendBmadBody(sb, detected, fw.Url!);
                return End(page, hasMermaid: true);
            case "gds":
                AppendGdsBody(sb, detected, fw.Url!);
                return End(page, hasMermaid: true);
            case "gsd":
                AppendGsdCoreBody(sb, detected, fw.Url!);
                return End(page, hasMermaid: true);
            default:
                AppendComingSoonBody(sb, fw.Label, fw.Url, fw.Blurb);
                return End(page, hasMermaid: false);
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
        AppendMatrixRow(sb, "BMad", SiteNav.AboutSddBmadOutputPath, FamilyMatrix.All);
        AppendMatrixRow(sb, "BMad GDS", SiteNav.AboutSddGdsOutputPath, FamilyMatrix.All);
        AppendMatrixRow(sb, "Spec Kit", SiteNav.AboutSddSpecKitOutputPath, FamilyMatrix.None);
        AppendMatrixRow(sb, "GSD", SiteNav.AboutSddGsdOutputPath, FamilyMatrix.GsdCore);
        AppendMatrixRow(sb, "GSD-Pi", SiteNav.AboutSddGsdPiOutputPath, FamilyMatrix.None);
        AppendMatrixRow(sb, "Superpowers", SiteNav.AboutSddSuperpowersOutputPath, FamilyMatrix.None);
        sb.Append("    </tbody>\n  </table>\n");
    }

    /// <summary>What a matrix cell can say. The third value is the point: before Story 12.2 both helpers took a
    /// single <c>bool supported</c>, which forced a framework to be all-✓ or all-✗ — and GSD Core is genuinely
    /// neither. Worse, a bare ✗ conflates two different facts. <see cref="NotProjected"/> means the framework HAS
    /// the artifact and SpecScribe does not project it (a SpecScribe boundary); <see cref="NoAnalog"/> means the
    /// framework has no such artifact at all (nothing to project, and nothing owed). Rendering both as "—" would
    /// have made GSD's empty Retros row read as unfinished work when it is a structural absence. [Story 12.2 Task 9]</summary>
    private enum FamilySupport
    {
        Supported,
        NotProjected,
        NoAnalog,
    }

    /// <summary>Per-noun support for one framework, in the matrix's column order. The nouns mirror
    /// <see cref="ArtifactBundle"/>'s projection families plus <see cref="ModuleContext"/>'s commands — the same
    /// nouns the shared adapter contract already uses.</summary>
    private sealed record FamilyMatrix(
        FamilySupport EpicsAndStories,
        FamilySupport Requirements,
        FamilySupport Sprint,
        FamilySupport Retros,
        FamilySupport PlanningDocs,
        FamilySupport Commands)
    {
        /// <summary>Every family projected — BMad and BMad GDS, both riding <see cref="BmadArtifactAdapter"/>.</summary>
        public static FamilyMatrix All { get; } = Uniform(FamilySupport.Supported);

        /// <summary>No adapter yet. Deliberately <see cref="FamilySupport.NotProjected"/> rather than
        /// <see cref="FamilySupport.NoAnalog"/>: these frameworks have the artifacts, SpecScribe just does not read
        /// them yet.</summary>
        public static FamilyMatrix None { get; } = Uniform(FamilySupport.NotProjected);

        /// <summary>GSD Core, per owner decisions D1/D3 and the module ceiling (Gap 3). The remaining unticked rows are
        /// each unticked for a DIFFERENT stated reason, spelled out in words on the framework page.</summary>
        public static FamilyMatrix GsdCore { get; } = new(
            EpicsAndStories: FamilySupport.Supported,  // ROADMAP.md → phases + plans
            Requirements: FamilySupport.NotProjected,  // D3 — open-ended id prefixes; REQUIREMENTS.md renders as a document
            Sprint: FamilySupport.Supported,           // STATE.md + the roadmap's per-plan checkbox
            Retros: FamilySupport.NoAnalog,            // GSD Core has no retrospective artifact at all
            PlanningDocs: FamilySupport.NotProjected,  // Gap 3 — ModuleContext is BMad-typed
            Commands: FamilySupport.Supported);        // Installed .claude/commands/gsd definitions drive next steps

        private static FamilyMatrix Uniform(FamilySupport v) => new(v, v, v, v, v, v);

        public IReadOnlyList<FamilySupport> InColumnOrder =>
            new[] { EpicsAndStories, Requirements, Sprint, Retros, PlanningDocs, Commands };
    }

    private static void AppendMatrixRow(StringBuilder sb, string label, string href, FamilyMatrix families)
    {
        sb.Append("      <tr>");
        sb.Append($"<th scope=\"row\"><a href=\"{PathUtil.Html(href)}\">{PathUtil.Html(label)}</a></th>");
        foreach (var family in families.InColumnOrder)
            sb.Append($"<td>{Check(family)}</td>");
        sb.Append("</tr>\n");
    }

    /// <summary>The cell glyph. Every state carries its own <c>aria-label</c> word, so the distinction survives for
    /// a screen-reader user exactly as it does visually — the table's meaning is never carried by glyph shape
    /// alone. The ✓ and — markup is byte-identical to what the two-state helper emitted, so every all-✓ and all-✗
    /// row in the matrix is unchanged.</summary>
    private static string Check(FamilySupport support) => support switch
    {
        FamilySupport.Supported => "<span class=\"sdd-check\" aria-label=\"Yes\">✓</span>",
        FamilySupport.NoAnalog => "<span class=\"sdd-check sdd-check--na\" aria-label=\"Not applicable — this framework has no such artifact\">n/a</span>",
        _ => "<span class=\"sdd-check sdd-check--no\" aria-label=\"No\">—</span>",
    };

    private static void AppendFamilySupportTable(StringBuilder sb, FamilyMatrix families)
    {
        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--compact\">\n");
        sb.Append("    <tbody>\n");
        AppendCompactRow(sb, "Epics &amp; Stories", families.EpicsAndStories);
        AppendCompactRow(sb, "Requirements", families.Requirements);
        AppendCompactRow(sb, "Sprint", families.Sprint);
        AppendCompactRow(sb, "Retros", families.Retros);
        AppendCompactRow(sb, "Planning docs", families.PlanningDocs);
        AppendCompactRow(sb, "Commands", families.Commands);
        sb.Append("    </tbody>\n  </table>\n");
    }

    private static void AppendCompactRow(StringBuilder sb, string label, FamilySupport support) =>
        sb.Append($"      <tr><th scope=\"row\">{label}</th><td>{Check(support)}</td></tr>\n");

    private static void AppendBmadBody(StringBuilder sb, bool detected, string url)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>BMad</strong> (BMad Method) is an AI-assisted methodology for product briefs, ");
        sb.Append("PRDs, epics, stories, and retrospectives. Choose it when you want a full planning → delivery ");
        sb.Append("spine with slash-command workflows in your editor.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Install into a repo, then run the help skill to pick your next step:</p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install</code></pre>\n");
        sb.Append($"  <p>See <a href=\"{PathUtil.Html(url)}\">the official documentation</a> ");
        sb.Append("for more information and installation options.</p>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad is not detected in this repository yet (_bmad/bmm).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe projects BMad through the shared adapter contract: epics &amp; stories, ");
        sb.Append("requirements, sprint, retros, planning docs, and next-step commands when those artifacts exist.</p>\n");
        AppendFamilySupportTable(sb, FamilyMatrix.All);

        AppendBmadModulesSection(sb);

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

    /// <summary>How SpecScribe treats BMad's MODULE ecosystem — the Epic 18 answer. BMad installs as a set of
    /// modules under <c>_bmad/{code}/</c> and BMad Builder can mint new ones, so this section states the
    /// open-world posture (<see cref="BmadModule.Unmodeled"/>), names the coverage-tier vocabulary
    /// (<see cref="CoverageTiers"/>), and points at the two core-skill surfaces (Ideas, Test Artifacts).
    /// <para>Deliberately framework-agnostic about WHICH module is installed here: this page is written on every
    /// run and takes only the two presence booleans, so naming a detected module would require threading
    /// <see cref="ModuleContext"/> through. Diagnostics already reports the detected module by label and code,
    /// and this section links there rather than restating it — one source of truth (NFR8).</para>
    /// [Epic 18; ADR 0015; ADR 0020; ADR 0021]</summary>
    private static void AppendBmadModulesSection(StringBuilder sb)
    {
        sb.Append("  <h2 id=\"modules\">Modules</h2>\n");
        sb.Append("  <p>BMad is not one thing: it installs as a set of modules under <code>_bmad/{code}/</code>, ");
        sb.Append("and BMad Builder can mint new ones with codes nobody has seen before. SpecScribe identifies ");
        sb.Append("each installed module from that directory name and states how deeply it understands it. ");
        sb.Append("Installing a second module never degrades the one you already had.</p>\n");

        sb.Append("  <table class=\"sdd-support-matrix sdd-support-matrix--modules\">\n");
        sb.Append("    <thead><tr><th>Module</th><th>Code</th><th>What SpecScribe does</th></tr></thead>\n");
        sb.Append("    <tbody>\n");
        AppendModuleRow(sb, "BMad Method", "bmm",
            "<strong>Full projection</strong> — every family above, plus this module's glossary and next-step commands.");
        AppendModuleRow(sb, "Game Dev Studio", "gds",
            "<strong>Full projection</strong> — the same families, with GDD / narrative planning docs and GDS commands.");
        AppendModuleRow(sb, "Test Architect", "tea",
            "<strong>Named, with its test artifacts interpreted</strong> — they get their own page and a Module Coverage panel on the home page, carrying the quality-gate verdict and coverage figures.");
        AppendModuleRow(sb, "Any other module", "cis, bmb, …",
            "<strong>Named</strong> — its real label and its own commands, and its documents render as pages.");
        sb.Append("    </tbody>\n  </table>\n");

        // The boundary paragraph is load-bearing, not padding: Test Architect is `BmadModule.Unmodeled` like any
        // other non-BMM/GDS module (`ModuleContext.ModuleForCode`), so the no-glossary / no-coverage-panel rule
        // applies to it TOO. A table row alone would have implied a middle tier that the identity layer does not
        // have, which is exactly the confidently-wrong failure NFR8 forbids. [ADR 0015 Decision 2; Story 18.6]
        sb.Append("  <p>&ldquo;Named&rdquo; is a stated boundary, not a gap. For every module below the top two — ");
        sb.Append("<strong>Test Architect included</strong> — SpecScribe publishes no glossary and no planning-doc ");
        sb.Append("set, and the artifact-coverage panel on the home page is omitted rather than reporting BMad ");
        sb.Append("Method artifact families the module never produces. The run records that omission on ");
        sb.Append($"<a href=\"{PathUtil.Html(SiteNav.DiagnosticsOutputPath)}\">Diagnostics</a>, which also names ");
        sb.Append("the module actually detected in this project.</p>\n");

        sb.Append("  <h3 id=\"coverage-tiers\">Coverage tiers</h3>\n");
        sb.Append("  <p>Where SpecScribe interprets a module's artifacts, each one carries a tier saying how far ");
        sb.Append("that interpretation goes:</p>\n");
        sb.Append("  <dl class=\"sdd-tier-list\">\n");
        foreach (var tier in CoverageTiers.Order)
        {
            sb.Append($"    <div class=\"cap-row\"><dt>{PathUtil.Html(CoverageTiers.Word(tier))}</dt>");
            sb.Append($"<dd>{PathUtil.Html(CoverageTiers.Description(tier))}</dd></div>\n");
        }
        sb.Append("  </dl>\n");

        sb.Append("  <h3 id=\"core-surfaces\">Surfaces from BMad core</h3>\n");
        sb.Append("  <p>Two skills ship in BMad <em>core</em>, so they are present whichever module you run. Each ");
        sb.Append("gets its own surface, and each is omitted entirely when the underlying artifacts don't exist:</p>\n");
        sb.Append("  <ul class=\"sdd-commands\">\n");
        sb.Append("    <li><code>/bmad-forge-idea</code> &mdash; an <strong>Ideas</strong> page grouping every forged ");
        sb.Append("idea by how it turned out (hardened, in progress, killed), each with a detail page, the forge's ");
        sb.Append("own report carried through unchanged, and a forward link to the brief, PRD, or epic the idea ");
        sb.Append("became where that link is evidenced on disk.</li>\n");
        sb.Append("    <li><code>/bmad-testarch-*</code> &mdash; a <strong>Test Artifacts</strong> page. This is also ");
        sb.Append("the one place SpecScribe reads something other than markdown: the Test Architect gate decision ");
        sb.Append("and end-to-end trace summary are read by exact filename, so the gate verdict is visible rather ");
        sb.Append("than invisible to a markdown-only scan.</li>\n");
        sb.Append("  </ul>\n");
    }

    private static void AppendModuleRow(StringBuilder sb, string label, string code, string treatment) =>
        sb.Append($"      <tr><th scope=\"row\">{PathUtil.Html(label)}</th><td><code>{PathUtil.Html(code)}</code></td><td>{treatment}</td></tr>\n");

    private static void AppendGdsBody(StringBuilder sb, bool detected, string url)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>BMad GDS</strong> (Game Dev Studio) adapts BMad for game development — GDD, ");
        sb.Append("narrative, and quick-flow prototyping across Unity, Unreal, and Godot. Choose it when the ");
        sb.Append("primary artifacts are game design docs rather than a software PRD spine.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Add the GDS module during BMad install (or to an existing install):</p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx bmad-method install --modules gds</code></pre>\n");
        sb.Append($"  <p>See <a href=\"{PathUtil.Html(url)}\">the official documentation</a> ");
        sb.Append("for more information and installation options.</p>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">BMad GDS is not detected in this repository yet (_bmad/gds).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe projects BMad GDS through the same adapter families as BMad — including ");
        sb.Append("GDD / narrative / architecture planning docs and GDS-oriented commands when installed.</p>\n");
        AppendFamilySupportTable(sb, FamilyMatrix.All);
        sb.Append("  <p>GDS is one of BMad's modules, and a repo may install several at once. For how SpecScribe ");
        sb.Append($"treats the rest of them, see <a href=\"{PathUtil.Html(SiteNav.AboutSddBmadOutputPath)}#modules\">");
        sb.Append("BMad &rsaquo; Modules</a>.</p>\n");

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

    /// <summary>GSD Core's framework page — the same five-beat shape as <see cref="AppendBmadBody"/> (what it is →
    /// get started → SpecScribe support → commands → methodology), replacing the "coming soon" placeholder now that
    /// <see cref="GsdCoreArtifactAdapter"/> exists.
    ///
    /// <para><b>The three unticked rows are explained IN WORDS, not left as bare dashes.</b> This is the whole
    /// posture of Story 12.2: an absent tick must read as a stated boundary, never as unfinished work (NFR8). Each
    /// of the three is unticked for a different reason — a deliberate modelling decision (Requirements), and a
    /// structural ceiling in a shared type that this story is explicitly out of scope to widen (Planning docs,
    /// Commands) — and a reader who cannot tell those apart has been told nothing useful.</para>
    /// [Story 12.2 Task 9]</summary>
    private static void AppendGsdCoreBody(StringBuilder sb, bool detected, string url)
    {
        sb.Append("  <h2 id=\"overview\">What it is</h2>\n");
        sb.Append("  <p><strong>GSD Core</strong> (Get Shit Done) is a spec-driven framework layered on your ");
        sb.Append("existing AI coding runtime as <code>/gsd-*</code> slash commands. It keeps every artifact as ");
        sb.Append("plain Markdown and JSON in a <code>.planning/</code> directory &mdash; project brief, ");
        sb.Append("requirements, roadmap, and live state at the root, then one folder per phase &mdash; and ");
        sb.Append("decomposes work as Milestone &rarr; Phase &rarr; Plan. There is no database: what is on disk is ");
        sb.Append("the project. Choose it when you want the whole plan reviewable in your editor and your diff.</p>\n");
        sb.Append("  <p class=\"sdd-absent-info\">Not to be confused with <strong>BMad GDS</strong> (Game Dev ");
        sb.Append($"Studio), a different framework with a near-anagram name &mdash; see ");
        sb.Append($"<a href=\"{PathUtil.Html(SiteNav.AboutSddGdsOutputPath)}\">BMad GDS</a>. GSD Core is keyed on ");
        sb.Append("<code>.planning/</code>; BMad GDS installs under <code>_bmad/gds</code>.</p>\n");

        sb.Append("  <h2 id=\"get-started\">Get started</h2>\n");
        sb.Append("  <p>Install GSD Core into a repo, then let it scaffold the planning directory:</p>\n");
        sb.Append("  <pre class=\"sdd-install\"><code>npx @opengsd/gsd-core install</code></pre>\n");
        sb.Append($"  <p>See <a href=\"{PathUtil.Html(url)}\">the official documentation</a> ");
        sb.Append("for more information and installation options.</p>\n");
        if (!detected)
            sb.Append("  <p class=\"sdd-absent-info\">GSD Core is not detected in this repository yet (.planning).</p>\n");

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        sb.Append("  <p>SpecScribe reads <code>.planning/ROADMAP.md</code> as the epics source: each <em>phase</em> ");
        sb.Append("becomes an epic and each <em>plan</em> (<code>NN-YY-PLAN.md</code>) becomes a story, with the ");
        sb.Append("roadmap's per-plan checkbox as the completion signal. Milestones render as banded groups on the ");
        sb.Append($"<a href=\"{PathUtil.Html(SiteNav.EpicsOutputPath)}\">Epics &amp; Stories</a> index, and ");
        sb.Append("<code>.planning/STATE.md</code> supplies the sprint view. Next-step prompts discover installed ");
        sb.Append("definitions from <code>.claude/commands/gsd/</code>, so unavailable commands are omitted. Support is per-family, not blanket:</p>\n");
        AppendFamilySupportTable(sb, FamilyMatrix.GsdCore);

        sb.Append("  <h3 id=\"boundaries\">Why three rows are not ticked</h3>\n");
        sb.Append("  <p>Each is a stated boundary with its own reason, not a queue of unfinished work:</p>\n");
        sb.Append("  <dl class=\"sdd-tier-list\">\n");
        sb.Append("    <div class=\"cap-row\"><dt>Requirements</dt><dd>GSD Core does ship a ");
        sb.Append("<code>REQUIREMENTS.md</code> with a requirement&rarr;phase traceability table, and it renders in ");
        sb.Append("full as its own page. What SpecScribe does <em>not</em> do is model its ids. GSD projects define ");
        sb.Append("their own id prefixes &mdash; one real project uses twelve (<code>CONV-01</code>, ");
        sb.Append("<code>CAP-01</code>, <code>GADM-01</code>, &hellip;) &mdash; so the set is open-ended, while ");
        sb.Append("SpecScribe's requirement model is a closed vocabulary. Projecting them would mean renaming each ");
        sb.Append("one to an id GSD never wrote, which is worse than not projecting them.</dd></div>\n");
        sb.Append("    <div class=\"cap-row\"><dt>Retros</dt><dd>Marked <em>n/a</em> rather than unsupported: GSD ");
        sb.Append("Core has no retrospective artifact at all. Its per-plan <code>NN-YY-SUMMARY.md</code> files are ");
        sb.Append("execution logs, and treating one as a retrospective would mark its phase closed out on every ");
        sb.Append("status surface on the strength of a build log. They render as their own pages instead.</dd></div>\n");
        sb.Append("    <div class=\"cap-row\"><dt>Planning docs</dt><dd>The module-planning-doc column remains a ");
        sb.Append("ceiling, not a GSD gap. It is driven by SpecScribe's BMad-specific module-identity model and its ");
        sb.Append("installed-module layout (<code>_bmad/{code}/</code>). GSD workflow commands do not require that ");
        sb.Append("identity: each installed definition under <code>.claude/commands/gsd/</code> is discovered directly ");
        sb.Append("for the matching next-step prompt.</dd></div>\n");
        sb.Append("  </dl>\n");
        sb.Append("  <p>Everything else under <code>.planning/</code> &mdash; the project brief, the codebase map, ");
        sb.Append("research notes, todos, and each phase's context and verification companions &mdash; renders as ");
        sb.Append("its own page through the standard Markdown pass. The one file SpecScribe does not read is ");
        sb.Append($"<code>config.json</code>, and the run records that on ");
        sb.Append($"<a href=\"{PathUtil.Html(SiteNav.DiagnosticsOutputPath)}\">Diagnostics</a>.</p>\n");

        sb.Append("  <h2 id=\"commands\">Common commands</h2>\n");
        sb.Append("  <ul class=\"sdd-commands\">\n");
        sb.Append("    <li><code>/gsd-new</code> — define the project brief and core value</li>\n");
        sb.Append("    <li><code>/gsd-requirements</code> — capture requirements</li>\n");
        sb.Append("    <li><code>/gsd-roadmap</code> — plan milestones and phases</li>\n");
        sb.Append("    <li><code>/gsd-plan-phase</code> — decompose a phase into numbered plans</li>\n");
        sb.Append("    <li><code>/gsd-execute-phase</code> — execute every plan in a phase</li>\n");
        sb.Append("    <li><code>/gsd-code-review</code> — review a completed phase</li>\n");
        sb.Append("    <li><code>/gsd-review-backlog</code> — promote backlog phases into the roadmap</li>\n");
        sb.Append("  </ul>\n");

        sb.Append("  <h2 id=\"methodology\">Methodology</h2>\n");
        sb.Append("  <p>Typical progression: define the project once, then loop plan &rarr; execute &rarr; ");
        sb.Append("summarize for each plan inside a phase, and close each milestone when its phases are done.</p>\n");
        sb.Append(Mermaid.Block(Mermaid.SddGsdCoreDiagram()));
    }

    /// <summary>The placeholder body for a framework SpecScribe does not yet project. Carries the framework's
    /// IDENTITY (<paramref name="blurb"/>) and its canonical documentation home (<paramref name="url"/>) when the
    /// roster pins them, so "coming soon" still says what the thing IS rather than only that it is absent — the
    /// same honest-absence posture NFR8 asks of every other surface. <paramref name="blurb"/> is pre-composed
    /// HTML; <paramref name="label"/> and <paramref name="url"/> are escaped. [Story 12.1]</summary>
    private static void AppendComingSoonBody(StringBuilder sb, string label, string? url, string? blurb)
    {
        sb.Append("  <h2 id=\"overview\">Coming soon</h2>\n");
        if (!string.IsNullOrEmpty(blurb))
        {
            sb.Append($"  <p>{blurb}</p>\n");
        }

        sb.Append($"  <p>{PathUtil.Html(label)} support in SpecScribe is planned. This page is a placeholder ");
        sb.Append("so the framework roster stays honest while adapters land in later epics.</p>\n");
        if (!string.IsNullOrEmpty(url))
        {
            sb.Append($"  <p>See <a href=\"{PathUtil.Html(url)}\">the official documentation</a> ");
            sb.Append("for more information and installation options.</p>\n");
        }

        sb.Append("  <h2 id=\"specscribe-support\">SpecScribe support</h2>\n");
        AppendFamilySupportTable(sb, FamilyMatrix.None);
    }

    /// <summary>The identity every SDD page shares, carried from <see cref="Begin"/> to <see cref="End"/> so the
    /// page's chrome facts reach its <see cref="PageView"/> instead of being string-built and discarded. Story 23.4
    /// moved this templater onto the delivery contract; the two-phase Begin/End shape is unchanged.</summary>
    private sealed record SddPage(SiteNav Nav, string OutputPath, string Title, string Description, IReadOnlyList<(string, string?)> Trail, StringBuilder Body);

    private static SddPage Begin(SiteNav nav, string outputPath, string title, string description)
    {
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

        // ⚠️ The body starts at the doc-header, NOT at <main> — these pages emit their title block BEFORE the
        // landmark, and the old region slice began at the breadcrumb, so the header was inside the captured
        // region. Starting at <main> would keep the golden gate green while silently dropping the title block
        // from the IR. [Story 23.4 AC #3, finding 1]
        var sb = new StringBuilder();
        sb.Append("<header class=\"doc-header\">\n");
        sb.Append($"  <h1>{PathUtil.Html(title)}</h1>\n");
        sb.Append($"  <div class=\"doc-subtitle\">{PathUtil.Html(description)}</div>\n");
        sb.Append("</header>\n\n");
        sb.Append("<main id=\"main-content\" class=\"info-page\">\n");
        sb.Append("<section class=\"chart-panel about-sdd-panel\">\n");
        return new SddPage(nav, outputPath, title, description, trail, sb);
    }

    private static PageView End(SddPage page, bool hasMermaid)
    {
        var sb = page.Body;
        sb.Append("</section>\n");
        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.About,
            OutputRelativePath = page.OutputPath,
            Title = $"{page.Title} — {page.Nav.SiteTitle}",
            MetaDescription = page.Description,
            Nav = page.Nav.ToNavigationView(page.OutputPath, page.Nav.BuildSddLocalContext(page.OutputPath)),
            Breadcrumb = BreadcrumbTrail.From(page.Trail),
            Assets = new AssetManifest
            {
                StylesheetHref = ForgeOptions.StylesheetName,
                ScriptHref = ForgeOptions.ScriptName,
                MermaidNeeded = hasMermaid,
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }
}
