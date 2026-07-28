using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the <c>test-artifacts.html</c> list page and the dashboard's Module Coverage panel — owner
/// decision D1's two surfaces, built from one model so the panel and the page can never state different figures.
///
/// <para>Row anatomy comes from Story 10.8's shared <see cref="ListRow"/> grammar; the page SHELL is the
/// standalone-templater shell (<see cref="IdeasTemplater"/> / <see cref="TraceabilityTemplater"/> are the freshest
/// precedents).</para>
///
/// <para><b>No JS and no chart.</b> Everything here is a server-rendered table, list or badge, so ADR 0013's
/// text-twin gate is never entered at all — which is why owner decision D1's panel is counts-and-badges rather
/// than a visualization. Every tier and every gate verdict carries its WORD; colour is only ever a supporting
/// accent (UX-DR17, "no state signalled by colour alone").</para>
///
/// <para><b>The tier legend is the deliverable, not decoration.</b> The PRD and <c>SPEC.md</c> both carried
/// <em>"How should coverage tiers be communicated so users understand interpretation boundaries?"</em> as an open
/// question. The answer this story ships is: state the boundary in words, on the surface, next to the artifacts it
/// applies to.</para> [Story 18.5]</summary>
public static class TestArtifactsTemplater
{
    public static string RenderListPage(TestArtifactsModel model, SiteNav nav)
    {
        var outputPath = SiteNav.TestArtifactsOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — test-artifacts.html is at the output root.
        var moduleName = ModuleName(model);

        var sb = new StringBuilder();
        sb.Append(PathUtil.RenderHeadOpen(
            $"Test Artifacts — {nav.SiteTitle}",
            prefix + ForgeOptions.StylesheetName,
            prefix + ForgeOptions.ScriptName,
            $"Test artifacts for {nav.SiteTitle} — the quality gate verdict, coverage figures, and how deeply SpecScribe interprets each document."));
        sb.Append(nav.RenderNavBar(outputPath));
        sb.Append(SiteNav.RenderBreadcrumb(outputPath, new (string, string?)[]
        {
            ("Home", SiteNav.HomeOutputPath),
            ("Test Artifacts", null),
        }));

        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Test Artifacts</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; quality evidence produced by {PathUtil.Html(moduleName)}</p>\n\n");
        sb.Append("<p class=\"ta-intro\">These documents are written by a separate methodology module, not by SpecScribe. Each one is labelled with how far SpecScribe&rsquo;s interpretation of it goes &mdash; so a figure shown here is one that was actually read, and an artifact SpecScribe does not model says so rather than quietly looking supported.</p>\n\n");

        sb.Append(RenderGateSection(model));
        sb.Append(RenderCoverageSection(model));
        sb.Append(RenderTraceabilityJoinSection(model, prefix));
        sb.Append(RenderTierLegend());
        sb.Append(RenderArtifactList(model, prefix));

        sb.Append("</main>\n\n");
        sb.Append(PathUtil.RenderFooter(prefix));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    /// <summary>The dashboard's Module Coverage panel BODY — module label, gate badge, tier counts, and one link
    /// through to the page. Returns "" when there is nothing to show, so the caller omits the whole panel rather
    /// than render an empty one (NFR8).
    ///
    /// <para>Shape is deliberately MODULE-AGNOSTIC: it reads a module code, a label, a verdict word and a tier
    /// count map, none of which name Test Architect. A second covered module drops in by producing the same
    /// model. No second module is implemented here.</para></summary>
    public static string RenderModuleCoveragePanelBody(TestArtifactsModel model, string linkPrefix = "")
    {
        if (model.IsEmpty) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"module-coverage\">\n");

        sb.Append("  <p class=\"module-coverage-lead\">");
        sb.Append($"Artifacts from <strong>{PathUtil.Html(ModuleName(model))}</strong> are interpreted alongside this project&rsquo;s own planning documents.");
        sb.Append("</p>\n");

        if (model.GateWord is { Length: > 0 } gate)
        {
            sb.Append("  <div class=\"module-coverage-gate\">\n");
            sb.Append($"    <span class=\"module-coverage-gate-label\">Quality gate</span>\n    {GateBadge(gate)}\n");
            if (model.Gate?.CriticalOpen is { } critical)
            {
                sb.Append($"    <span class=\"pill\">{critical} critical {PathUtil.Html(Charts.Plural(critical, "gap", "gaps"))} open</span>\n");
            }
            sb.Append("  </div>\n");
            if (model.Gate?.Rationale is { Length: > 0 } rationale)
            {
                sb.Append($"  <p class=\"module-coverage-rationale\">{PathUtil.Html(SiteGenerator.CollapseSummary(rationale))}</p>\n");
            }
        }

        sb.Append("  <ul class=\"module-coverage-tiers\">\n");
        foreach (var tier in CoverageTiers.Order)
        {
            var count = model.CountIn(tier);
            if (count == 0) continue; // NFR8: never a "0 unsupported" row.
            sb.Append("    <li class=\"module-coverage-tier\">");
            sb.Append($"<span class=\"module-coverage-count\">{count}</span> ");
            sb.Append($"<span class=\"module-coverage-word\">{PathUtil.Html(CoverageTiers.Word(tier).ToLowerInvariant())}</span>");
            sb.Append($"<span class=\"module-coverage-tier-note\"> &mdash; {PathUtil.Html(TierCountNote(tier))}</span>");
            sb.Append("</li>\n");
        }
        sb.Append("  </ul>\n");

        sb.Append($"  <p class=\"module-coverage-link\"><a class=\"view-epic-link\" href=\"{PathUtil.Html(linkPrefix + SiteNav.TestArtifactsOutputPath)}\">View all test artifacts &rarr;</a></p>\n");
        sb.Append("</div>\n");
        return sb.ToString();
    }

    private static string TierCountNote(CoverageTier tier) => tier switch
    {
        CoverageTier.Summarized => "read for their figures",
        CoverageTier.Rendered => "rendered as documents",
        _ => "listed but not interpreted",
    };

    // ---- Page sections -------------------------------------------------------------------------------------

    private static string RenderGateSection(TestArtifactsModel model)
    {
        if (model.GateWord is not { Length: > 0 } gate) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<section class=\"ta-section ta-gate\" id=\"ta-gate\">\n");
        sb.Append("  <h2>Quality gate</h2>\n");
        sb.Append("  <div class=\"ta-gate-row\">\n");
        sb.Append($"    {GateBadge(gate)}\n");
        sb.Append($"    <span class=\"ta-gate-meaning\">{PathUtil.Html(GateMeaning(gate))}</span>\n");
        sb.Append("  </div>\n");

        if (model.Gate is { } decision)
        {
            if (decision.Rationale is { Length: > 0 } rationale)
            {
                sb.Append($"  <p class=\"ta-gate-rationale\">{PathUtil.Html(rationale)}</p>\n");
            }

            var criteria = new List<(string Label, string? Value)>
            {
                ("P0 coverage", decision.P0Status),
                ("P1 coverage", decision.P1Status),
                ("Overall coverage", decision.OverallStatus),
            }.Where(c => c.Value is { Length: > 0 }).ToList();

            if (criteria.Count > 0)
            {
                sb.Append("  <ul class=\"ta-gate-criteria\">\n");
                foreach (var (label, value) in criteria)
                {
                    sb.Append($"    <li><span class=\"ta-criterion-label\">{PathUtil.Html(label)}</span> <span class=\"pill\">{PathUtil.Html(Humanize(value!))}</span></li>\n");
                }
                sb.Append("  </ul>\n");
            }

            if (decision.CriticalOpen is { } critical)
            {
                sb.Append($"  <p class=\"ta-gate-risk\">{critical} critical {PathUtil.Html(Charts.Plural(critical, "gap", "gaps"))} open at the time of the run.</p>\n");
            }
        }

        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    private static string RenderCoverageSection(TestArtifactsModel model)
    {
        var priorities = model.Trace?.PriorityBreakdown.Count > 0
            ? model.Trace.PriorityBreakdown
            : model.Matrix.PriorityBreakdown;
        var levels = model.Trace?.ByLevel ?? Array.Empty<TeaLevelCoverage>();
        if (priorities.Count == 0 && levels.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<section class=\"ta-section ta-coverage\" id=\"ta-coverage\">\n");
        sb.Append("  <h2>Test coverage</h2>\n");
        sb.Append("  <p class=\"ta-note\">Figures below are read from the module&rsquo;s own machine-readable summary. They describe coverage of that module&rsquo;s <em>coverage oracle</em> &mdash; which is not necessarily this project&rsquo;s requirement list; see below.</p>\n");

        if (priorities.Count > 0)
        {
            sb.Append("  <div class=\"table-scroll\">\n  <table class=\"ta-table\">\n");
            sb.Append("    <caption>Coverage by priority</caption>\n");
            sb.Append("    <thead><tr><th scope=\"col\">Priority</th><th scope=\"col\">Items</th><th scope=\"col\">Fully covered</th><th scope=\"col\">Coverage</th></tr></thead>\n");
            sb.Append("    <tbody>\n");
            foreach (var p in priorities)
            {
                sb.Append($"      <tr><th scope=\"row\">{PathUtil.Html(p.Priority)}</th><td>{p.Total}</td><td>{p.Covered}</td><td>{PathUtil.Html(Percent(p.Percent))}</td></tr>\n");
            }
            sb.Append("    </tbody>\n  </table>\n  </div>\n");
        }

        if (levels.Count > 0)
        {
            sb.Append("  <div class=\"table-scroll\">\n  <table class=\"ta-table\">\n");
            sb.Append("    <caption>Tests by level</caption>\n");
            sb.Append("    <thead><tr><th scope=\"col\">Level</th><th scope=\"col\">Tests</th></tr></thead>\n");
            sb.Append("    <tbody>\n");
            foreach (var l in levels)
            {
                sb.Append($"      <tr><th scope=\"row\">{PathUtil.Html(LevelLabel(l.Level))}</th><td>{l.Tests}</td></tr>\n");
            }
            sb.Append("    </tbody>\n  </table>\n  </div>\n");
        }

        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    /// <summary>The D2 join, stated honestly in both directions. When the join is admissible the resolved rows are
    /// listed and visibly attributed to the module; when it is not, the section says WHY in one sentence and shows
    /// nothing else.
    ///
    /// <para>This is where a confident-looking wrong answer would do the most damage — Story 21.1's own review
    /// caught a phantom-covered requirement that counted as covered and drew blank. The
    /// <see cref="TeaJoin.UnresolvedCount"/> is always stated, so an incomplete join reads as incomplete rather
    /// than as the whole picture.</para></summary>
    private static string RenderTraceabilityJoinSection(TestArtifactsModel model, string prefix)
    {
        if (model.Matrix.Criteria.Count == 0) return string.Empty;

        var moduleName = ModuleName(model);
        var sb = new StringBuilder();
        sb.Append("<section class=\"ta-section ta-join\" id=\"ta-traceability\">\n");
        sb.Append("  <h2>Against this project&rsquo;s requirements</h2>\n");

        // Two distinct no-join outcomes, and they must NOT read the same. The oracle can be inadmissible in
        // principle, or admissible but resolve to nothing this project actually defines (the TEA-only case: real
        // quality evidence, no epics.md, so no requirement or story id exists to resolve against). Either way the
        // joined table is omitted entirely rather than rendered empty — an empty matrix reads as "covered by
        // nothing", which is a claim, not an absence (NFR8).
        if (!model.Join.Admissible || model.Join.Rows.Count == 0)
        {
            var why = model.Join.Admissible
                ? "none of its coverage items resolve to a requirement or story this project defines"
                : model.Join.Reason;
            sb.Append($"  <p class=\"ta-join-absent\">{PathUtil.Html(moduleName)}&rsquo;s coverage is <strong>not</strong> mapped onto this project&rsquo;s requirement traceability, because {PathUtil.Html(why)}. ");
            sb.Append($"Its {model.Matrix.Criteria.Count} mapped {PathUtil.Html(Charts.Plural(model.Matrix.Criteria.Count, "item", "items"))} are shown on this page as their own dimension instead. An honest gap is preferable to a coverage claim this project&rsquo;s own artifacts cannot support.</p>\n");
            sb.Append(RenderCriteriaTable(model.Matrix.Criteria));
            sb.Append("</section>\n\n");
            return sb.ToString();
        }

        sb.Append($"  <p class=\"ta-note\">{model.Join.Rows.Count} of {PathUtil.Html(moduleName)}&rsquo;s {model.Matrix.Criteria.Count} mapped items resolve to a requirement or story this project actually defines");
        sb.Append(model.Join.UnresolvedCount switch
        {
            0 => ".",
            1 => "; the remaining one could not be resolved and is left out rather than guessed at.",
            var n => $"; the remaining {n} could not be resolved and are left out rather than guessed at.",
        });
        sb.Append("</p>\n");
        sb.Append($"  <p class=\"ta-note ta-attribution\">This is <em>test</em> coverage reported by {PathUtil.Html(moduleName)}, layered on top of &mdash; never replacing &mdash; SpecScribe&rsquo;s own requirement-to-epic coverage on the <a href=\"{PathUtil.Html(prefix + SiteNav.TraceabilityOutputPath)}\">traceability page</a>.</p>\n");

        sb.Append("  <div class=\"table-scroll\">\n  <table class=\"ta-table\">\n");
        sb.Append("    <caption>Module test coverage by requirement</caption>\n");
        sb.Append("    <thead><tr><th scope=\"col\">Requirement or story</th><th scope=\"col\">Reported as</th><th scope=\"col\">Tests</th><th scope=\"col\">Priority</th></tr></thead>\n");
        sb.Append("    <tbody>\n");
        foreach (var row in model.Join.Rows)
        {
            sb.Append($"      <tr><th scope=\"row\">{PathUtil.Html(row.TargetId)}</th>");
            sb.Append($"<td>{CoverageBadge(row.Criterion.CoverageStatus)}</td>");
            sb.Append($"<td>{row.Criterion.TestCount}</td>");
            sb.Append($"<td>{PathUtil.Html(row.Criterion.Priority ?? "&mdash;")}</td></tr>\n");
        }
        sb.Append("    </tbody>\n  </table>\n  </div>\n");
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    private static string RenderCriteriaTable(IReadOnlyList<TeaCriterionCoverage> criteria)
    {
        var sb = new StringBuilder();
        sb.Append("  <div class=\"table-scroll\">\n  <table class=\"ta-table\">\n");
        sb.Append("    <caption>Coverage oracle items, as the module reported them</caption>\n");
        sb.Append("    <thead><tr><th scope=\"col\">Item</th><th scope=\"col\">Reported as</th><th scope=\"col\">Tests</th><th scope=\"col\">Priority</th></tr></thead>\n");
        sb.Append("    <tbody>\n");
        foreach (var c in criteria)
        {
            var label = c.Description is { Length: > 0 }
                ? $"{PathUtil.Html(c.CriterionId)} &mdash; {PathUtil.Html(c.Description)}"
                : PathUtil.Html(c.CriterionId);
            sb.Append($"      <tr><th scope=\"row\">{label}</th>");
            sb.Append($"<td>{CoverageBadge(c.CoverageStatus)}</td>");
            sb.Append($"<td>{c.TestCount}</td>");
            sb.Append($"<td>{PathUtil.Html(c.Priority ?? "&mdash;")}</td></tr>\n");
        }
        sb.Append("    </tbody>\n  </table>\n  </div>\n");
        return sb.ToString();
    }

    /// <summary>The coverage-tier legend — this story's answer to the PRD/SPEC open question, rendered as words
    /// rather than left implicit in a badge colour.</summary>
    private static string RenderTierLegend()
    {
        var sb = new StringBuilder();
        sb.Append("<section class=\"ta-section ta-tiers\" id=\"ta-tiers\">\n");
        sb.Append("  <h2>How far interpretation goes</h2>\n");
        sb.Append("  <dl class=\"ta-tier-legend\">\n");
        foreach (var tier in CoverageTiers.Order)
        {
            sb.Append($"    <dt>{TierBadge(tier)}</dt>\n");
            sb.Append($"    <dd>{PathUtil.Html(CoverageTiers.Description(tier))}</dd>\n");
        }
        sb.Append("  </dl>\n");
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    private static string RenderArtifactList(TestArtifactsModel model, string prefix)
    {
        var sb = new StringBuilder();
        sb.Append("<section class=\"ta-section ta-artifacts\" id=\"ta-artifacts\">\n");
        sb.Append("  <h2>Discovered artifacts</h2>\n");
        sb.Append("  <ul class=\"ta-list list-rows-list js-listable\">\n");

        foreach (var artifact in model.Ordered)
        {
            var summaryHtml = new StringBuilder($"<strong>{PathUtil.Html(artifact.Title)}</strong>");
            if (artifact.Headline is { Length: > 0 } headline)
            {
                summaryHtml.Append($" &mdash; {PathUtil.Html(headline)}");
            }

            var chips = new List<string> { ListRow.Chip(PathUtil.Html(LeafName(artifact.SourceRelative))) };
            if (artifact.ProducingSkill is { Length: > 0 } skill)
            {
                chips.Add(ListRow.Chip(PathUtil.Html(skill)));
            }

            var primaryLink = artifact.OutputRelativePath is { Length: > 0 } page
                ? ListRow.PrimaryLink(PathUtil.Html(prefix + page), "Open document")
                : null;

            ListRow.Render(
                sb,
                summaryHtml.ToString(),
                TierBadge(artifact.Tier),
                chips,
                primaryLink,
                extraRowClass: $"list-row-accent-{CoverageTiers.AccentToken(artifact.Tier)}",
                sortName: artifact.Title,
                sortStatus: CoverageTiers.AccentToken(artifact.Tier));
        }

        sb.Append("  </ul>\n");
        sb.Append("</section>\n\n");
        return sb.ToString();
    }

    // ---- Badges: the WORD is always the signal --------------------------------------------------------------

    /// <summary>The tier badge. Always carries <see cref="CoverageTiers.Word"/>; the class only tints an
    /// already-labelled pill, and reuses an existing <c>--status-*</c> token rather than minting a new one.</summary>
    private static string TierBadge(CoverageTier tier) =>
        StatusStyles.Badge(CoverageTiers.AccentToken(tier), CoverageTiers.Word(tier));

    /// <summary>The gate badge. Always carries the verdict WORD.
    /// <para>Mapping onto the six existing stage tokens, with no new <c>--status-*</c> family added: <c>PASS</c>
    /// reads <c>done</c> (settled), <c>CONCERNS</c> reads <c>review</c> (look at this). <c>FAIL</c> and
    /// <c>WAIVED</c> BOTH read <c>deferred</c> — neither is a pass, and the six-token vocabulary has no distinct
    /// "blocked" tone to give them. That is deliberate rather than a fudge: the two are distinguished by their
    /// word, which is the contract (UX-DR17), and inventing a seventh token to separate them would break the
    /// single stage→colour source ([[specscribe-status-token-system]]).</para>
    /// <para>An unrecognized verdict keeps its word on the neutral <c>unrecognized</c> token — never dropped,
    /// never restyled as one of the four.</para></summary>
    private static string GateBadge(string gate)
    {
        var word = gate.Trim().ToUpperInvariant();
        var token = word switch
        {
            "PASS" => "done",
            "CONCERNS" => "review",
            "FAIL" or "WAIVED" => "deferred",
            _ => "unrecognized",
        };
        return StatusStyles.Badge(token, word);
    }

    private static string GateMeaning(string gate) => gate.Trim().ToUpperInvariant() switch
    {
        "PASS" => "Every gate criterion was met at the time of the run.",
        "CONCERNS" => "The critical criteria were met, but one or more secondary thresholds were not.",
        "FAIL" => "At least one critical criterion was not met.",
        "WAIVED" => "The gate did not pass, and the failure was accepted as a deliberate decision.",
        _ => "The module reported a verdict SpecScribe does not recognize; its word is shown unchanged.",
    };

    /// <summary>The per-item coverage badge. The module's own five-word vocabulary
    /// (<c>FULL|PARTIAL|NONE|UNIT-ONLY|INTEGRATION-ONLY</c>) is shown verbatim — it is that module's word, not
    /// SpecScribe's lifecycle vocabulary, so it is never translated into one.</summary>
    private static string CoverageBadge(string status)
    {
        var word = status.Trim().ToUpperInvariant();
        var token = word switch
        {
            "FULL" => "done",
            "PARTIAL" or "UNIT-ONLY" or "INTEGRATION-ONLY" => "active",
            "NONE" => "deferred",
            _ => "unrecognized",
        };
        return StatusStyles.Badge(token, word);
    }

    // ---- Small helpers -------------------------------------------------------------------------------------

    /// <summary>What to CALL the module on screen: its own declared label when one was parsed, else a neutral
    /// phrase. Never a hard-coded "Test Architect" or "Test Architecture Enterprise" — <c>module.yaml</c> and the
    /// installed <c>module-help.csv</c> disagree about which of those two it is, and only the CSV is on disk.</summary>
    private static string ModuleName(TestArtifactsModel model) =>
        model.ModuleLabel is { Length: > 0 } label ? label : "an installed methodology module";

    private static string Percent(double? value) =>
        value is { } v ? v.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";

    private static string LevelLabel(string level) => level switch
    {
        "e2e" => "End-to-end",
        "api" => "API",
        "component" => "Component",
        "unit" => "Unit",
        _ => IdeaDerivation.DeKebab(level),
    };

    /// <summary><c>NOT_MET</c> → <c>Not met</c>. Turns the module's SCREAMING_SNAKE status words into prose
    /// without changing which word they are.</summary>
    private static string Humanize(string value) =>
        IdeaDerivation.DeKebab(value.Replace('_', '-').ToLowerInvariant());

    private static string LeafName(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
