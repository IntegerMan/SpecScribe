using System.Globalization;
using System.Text;

namespace SpecScribe;

/// <summary>Renders the standalone <c>code-map.html</c> page — the source-code map surface (Story 7.6, FR14).
/// Reuses the same page shell every <c>Write*</c> page uses (<see cref="PathUtil.RenderHeadOpen"/> + nav +
/// breadcrumb + <c>&lt;main id="main-content"&gt;</c> + footer). Each of the four precomputed
/// <see cref="CodeMapVariant"/> panels renders through the ONE Hierarchy Explorer component
/// (<see cref="HierarchyExplorer.ProjectCodeMap"/>), which draws both shapes from one payload behind one selector.
/// <para><b>No-JS contract — ADR 0013, which supersedes the "a correct server-rendered chart IS the no-JS story"
/// reading this comment used to state.</b> Story 20.9 retired the server-rendered treemap and sunburst SVGs, so
/// JS-off loses the VISUALIZATION. It loses neither the information nor the navigation: each panel's complete
/// per-file table (<see cref="AppendFileTable"/>) ships as ordinary server-rendered markup with every file's path,
/// line count, type and six git metrics as text and every path linked, and Story 20.6 D1 audited that table as
/// this surface's text twin BECAUSE it is richer than the component's generic nested listing. The component is
/// therefore configured <see cref="HierarchyTwinDisplay.External"/> — one complete listing per panel, not two.</para>
/// <para>The two "exclude spec-driven development directories" / "exclude tests" checkboxes that pick which panel
/// shows stay PURE CSS and keep working with JavaScript off (owner decision D2 of Story 20.9) — they are the one
/// filter on this page that does. They additionally carry <c>data-hierarchy-reveal</c>, because three of the four
/// panels are <c>display:none</c> at load and Plotly cannot lay out in a zero-width container (F1). Every control
/// that DOES need script — the shape selector, the colorize picker — rides inside the component's hidden control
/// bar, so a no-JS visitor never sees a dead control. Replaced the retired Story 3.4 structure-tree page.
/// [Story 7.6; Story 20.9 conversion]</para></summary>
public static class CodeMapTemplater
{
    /// <summary>Renders the whole page from all four precomputed filter combinations (<see cref="CodeMap.BuildVariants"/>).
    /// Headline stats describe the unfiltered ("full") variant regardless of which panel the checkboxes currently show
    /// — the checkboxes are a view onto one codebase, not a different one. <paramref name="fileHref"/> is the guarded
    /// in-portal code-page resolver (Story 7.1): a non-null return routes a file to its code page, a null return (or a
    /// null resolver) leaves it a plain, focusable rect — never a broken link.</summary>
    public static string RenderPage(IReadOnlyList<CodeMapVariant> variants, SiteNav nav, Func<string, string?>? fileHref = null) =>
        HtmlRenderAdapter.Shared.Render(BuildPage(variants, nav, fileHref)).Content;

    /// <summary>Builds this page's host-neutral <see cref="PageView"/> — see
    /// <see cref="RiskQuadrantTemplater.BuildPage"/> for why every standalone templater grew one. This page emits
    /// NO boot marker at all (neither head nor inline) and only pulls the engine, which is the third of the three
    /// hierarchy shapes <see cref="AssetManifest.HierarchyBootInline"/> documents. [Story 23.4 AC #3]</summary>
    public static PageView BuildPage(IReadOnlyList<CodeMapVariant> variants, SiteNav nav, Func<string, string?>? fileHref = null)
    {
        var outputPath = SiteNav.CodeMapOutputPath;
        var prefix = PathUtil.RelativePrefix(outputPath); // "" — code-map.html is at the output root.

        var full = variants.FirstOrDefault(v => v.Key == "full") ?? variants[0];
        var map = full.Map;

        var fileWord = Charts.Plural(map.FileCount, "file", "files");
        var dirWord = Charts.Plural(map.DirectoryCount, "directory", "directories");
        var lineWord = Charts.Plural((int)Math.Min(map.TotalLines, int.MaxValue), "line", "lines");
        var headline = $"{map.FileCount:N0} {fileWord} across {map.DirectoryCount:N0} {dirWord} · {map.TotalLines:N0} {lineWord} of code";

        var sb = new StringBuilder();
        sb.Append("<main id=\"main-content\" class=\"dashboard\">\n\n");
        sb.Append("<h1>Code Map</h1>\n");
        sb.Append($"<p class=\"doc-subtitle\">{PathUtil.Html(nav.SiteTitle)} &middot; {PathUtil.Html(headline)}</p>\n\n");

        // Shown once, shared across all four filter combinations (not one of the four panels themselves) —
        // deliberately NOT wrapped in a .chart-panel card: the two filter checkboxes right below need to be plain,
        // unwrapped siblings of the four .codemap-view panels (the CSS sibling-combinator toggle needs them at the
        // same nesting level as their targets), so nothing here can be a common ancestor of both.
        sb.Append("<h3>Source Code Map</h3>\n");
        sb.Append("<p class=\"chart-lead\">Every file, sized by its lines of code and nested inside its directory. ");
        sb.Append("Use \"Sunburst / Treemap\" to switch shape, and \"Colorize by\" to switch what the color encodes — a git-derived change signal when available, or file type. Select a directory to zoom in. Filter what's shown with the checkboxes below; the full text listing under each map works with JavaScript off.</p>\n\n");

        // Pure CSS: no JavaScript is needed for filtering to work (round 2).
        AppendFilterCheckbox(sb, "cm-exclude-spec", "Exclude spec-driven development directories");
        AppendFilterCheckbox(sb, "cm-exclude-tests", "Exclude tests");

        foreach (var variant in variants)
        {
            AppendVariantPanel(sb, variant, fileHref, prefix);
        }

        sb.Append("</main>\n\n");

        return new PageView
        {
            Kind = PageKind.Structure,
            OutputRelativePath = outputPath,
            Title = $"Code Map — {nav.SiteTitle}",
            MetaDescription = $"Source-code treemap for {nav.SiteTitle} — every file sized by its lines of code and colorable by git-derived change activity, with a full text-equivalent listing.",
            Nav = nav.ToNavigationView(outputPath, nav.BuildInsightsLocalContext(outputPath)),
            Breadcrumb = BreadcrumbTrail.From(new (string, string?)[] { ("Home", "index.html"), ("Code Map", null) }),
            Assets = new AssetManifest
            {
                StylesheetHref = prefix + ForgeOptions.StylesheetName,
                ScriptHref = prefix + ForgeOptions.ScriptName,
                MermaidNeeded = false,
                // The vendored plotly.js hierarchy engine: AFTER the body, a local file reference, never a CDN
                // (NFR-3). Conditional on a host actually being present, so an empty repo ships no 1.2 MB bundle
                // it cannot use — the exact miss Story 20.7 made on four surfaces at once, every layer below the
                // browser green while nothing mounted, which is why `SiteGeneratorSpaTests` asserts the "no
                // fewer" half. This page emits NO boot marker, so HierarchyBootInline stays false. [Story 23.4]
                HierarchyEngineNeeded = HierarchyExplorer.ContainsHost(sb.ToString()),
            },
            Interaction = InteractionState.None,
            BodyHtml = sb.ToString(),
        };
    }

    /// <summary>The pure-CSS panel toggle — the ONE filter on this page that works with JavaScript off, and owner
    /// decision D2 of Story 20.9 keeps it that way deliberately: trading it for a byte win would take information
    /// away from a no-JS visitor to make a chart cheaper.
    ///
    /// <para><c>data-hierarchy-reveal</c> is the only thing this story adds. Exactly one <c>.codemap-view</c> panel
    /// is visible at a time and the other three are <c>display:none</c>, i.e. ZERO-WIDTH — and Plotly cannot lay
    /// out in a zero-width container while <c>responsive: true</c>'s resize listener never fires on a CSS-only
    /// reveal. The marker tells the component "a mount may become possible when this changes"; it does not tell it
    /// anything about this page. These are real <c>&lt;input&gt;</c> elements — the toggle is pure CSS for
    /// STYLING, but the elements still fire <c>change</c>. [Story 20.9 F1]</para></summary>
    private static void AppendFilterCheckbox(StringBuilder sb, string id, string label)
    {
        sb.Append($"  <input type=\"checkbox\" id=\"{id}\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal>");
        sb.Append($"<label for=\"{id}\" class=\"codemap-filter-label\">{PathUtil.Html(label)}</label>\n");
    }

    /// <summary>The Code Map explorer's configured size, applied by the component as a HEIGHT capped to its own
    /// width (never a width — <c>HierarchyExplorerConfig.Size</c> is one int and the container supplies the
    /// width). The retired SVG's 1000×640 was a fixed viewBox for a chart that neither labelled nor drilled;
    /// only the 640 carries over, and only because it is the height. Verified live rather than ported on faith
    /// (Story 20.9 F5).</summary>
    private const int CodeMapExplorerSize = 640;

    /// <summary>Renders one precomputed filter combination as a self-contained panel: a "View as" shape toggle
    /// (Treemap/Sunburst) crossed with a shared "Colorize by" dimension dropdown, the drill breadcrumb (Treemap
    /// zoom only), the shared legend/notice, both chart shapes, and the text-equivalent table. Owner feedback,
    /// Story 7.12 review round 2: this used to be TWO separate panels — a general multi-dimension Treemap and a
    /// recency-only Sunburst — which felt like "what to view" and "how to view it" were artificially split across
    /// different surfaces. They're now ONE panel: <see cref="AppendColorizeControls"/>/<see cref="AppendLegend"/>/
    /// <see cref="AppendDiscreteLegend"/> govern BOTH shapes (the client-side <c>recolor()</c> enhancement in
    /// <c>specscribe.js</c> now queries <c>.codemap-cell</c> across the whole panel, not just the treemap's own
    /// <c>&lt;svg&gt;</c>, so a dimension switch recolors whichever shape is showing — and the other, off-screen
    /// one, so neither can drift stale). The treemap card and the table card are SIBLING <c>.chart-panel</c>s
    /// inside the (unstyled) <c>.codemap-view</c> wrapper — never one nested inside the other, matching how every
    /// other chart on this site pairs a visual + its text-equivalent table as two top-level cards. Nothing here
    /// carries an <c>id</c> except the per-shape toggle radios (suffixed with <paramref name="variant"/>'s key so
    /// all four filter-combination panels' toggles can coexist without collision): every other lookup is scoped
    /// with class selectors (not <c>getElementById</c>). Exactly one panel is visible at a time via the pure-CSS
    /// checkbox toggle; the others are <c>display:none</c> (and therefore out of the accessibility tree) until
    /// selected.</summary>
    private static void AppendVariantPanel(StringBuilder sb, CodeMapVariant variant, Func<string, string?>? fileHref, string prefix)
    {
        // [Review][Patch] Declares which of the two pure-CSS filter checkboxes need which checked state to reveal
        // THIS panel — read by specscribe.js's boot-time hash reveal so a deep link into a non-default panel (e.g.
        // "no-tests") auto-checks the right box instead of silently doing nothing, since only the default-visible
        // panel's own init runs `scopeFromHash()` at load. Declarative, not a surface name in the shared component.
        var revealWhen = $"cm-exclude-spec={(variant.ExcludesSpecDev ? "1" : "0")};cm-exclude-tests={(variant.ExcludesTests ? "1" : "0")}";
        sb.Append($"<div class=\"codemap-view\" data-view=\"{PathUtil.Html(variant.Key)}\" data-hierarchy-reveal-when=\"{PathUtil.Html(revealWhen)}\">\n");

        if (variant.Map.IsEmpty)
        {
            sb.Append("  <p class=\"codemap-notice\" role=\"note\">No files match this filter.</p>\n");
            sb.Append("</div>\n\n");
            return;
        }

        var files = variant.Map.Files();
        var hasMetrics = files.Any(f => f.Metrics is not null);
        var maxChanges = Charts.ComputeMaxChanges(variant.Map.Roots);

        // "What to view" — the colorize dimension picker, kept exactly as Story 7.12's owner-directed merge left
        // it and now wired to the component's dimension contract instead of a per-panel recolour loop. It rides
        // inside the component's own hidden control bar, so it inherits the reveal handshake rather than
        // re-inventing one, and the "how to view it" axis is the component's shape selector.
        var controls = new StringBuilder();
        AppendColorizeControls(controls, hasMetrics);

        var legend = new StringBuilder();
        if (!hasMetrics)
        {
            // OUTSIDE the legend bar deliberately: this is a fact about the DATA, not chrome for a chart, so it
            // stands whether or not the chart ever mounts.
            legend.Append("    <p class=\"codemap-notice codemap-notice-secondary\" role=\"note\">Git change data is unavailable (run with <code>--deep-git</code> in a git repository to colorize by the six git-derived dimensions). The map is colorized by file type instead.</p>\n");
        }
        // Both legend shapes ship pre-rendered; the component shows exactly the one the active dimension owns, so
        // the visible legend can never disagree with what is coloured. The bar itself is hidden until a successful
        // mount — a legend for a chart that never renders is chrome for nothing.
        legend.Append("    <div class=\"ss-hierarchy-legends\" hidden>\n");
        AppendLegend(legend, hasMetrics, maxChanges);
        AppendDiscreteLegend(legend, files, hasMetrics);
        legend.Append("    </div>\n");

        // Four instances, four DomIds, four HashKeys — keyed off the variant so their deep links cannot collide
        // (Story 20.9 F4). `#dir=` is deliberately NOT preserved: it was never a documented stable scheme and
        // contorting `hashKey` to keep it would fork the deep-link vocabulary across surfaces.
        var config = new HierarchyExplorerConfig(
            DomId: $"codemap-{variant.Key}",
            // Story 20.7 D2: selector ordering is fixed site-wide, the DEFAULT shape stays per-instance. This
            // surface's shipped default was the treemap.
            Shape: "treemap",
            Mode: HierarchyMode.Navigate,
            HashKey: $"cm-{variant.Key}",
            Size: CodeMapExplorerSize,
            Labels: true,
            Meta: new Charts.ChartMeta(
                VariantTitle(variant),
                Window: $"{variant.Map.FileCount:N0} {Charts.Plural(variant.Map.FileCount, "file", "files")} · {variant.Map.TotalLines:N0} {Charts.Plural((int)Math.Min(variant.Map.TotalLines, int.MaxValue), "line", "lines")}"),
            // Story 20.6 D1: the per-variant file table below IS this surface's twin, and it is richer than the
            // generic nested listing — it carries every file's six git metrics as real table cells.
            TwinDisplay: HierarchyTwinDisplay.External,
            Dimensions: HierarchyExplorer.CodeMapDimensions(hasMetrics));

        var model = HierarchyExplorer.ProjectCodeMap(variant, config, fileHref, prefix);
        sb.Append(HierarchyExplorer.Render(
            model, "chart-panel codemap-panel", " data-explorer", controls.ToString(), legend.ToString()));

        AppendFileTable(sb, files, hasMetrics, fileHref, prefix);

        sb.Append("</div>\n\n");
    }

    /// <summary>Each panel's own framed title. The page heading above the checkboxes describes the surface; a
    /// panel's title has to say WHICH filter combination it is, or four identically-titled panels sit in one
    /// document with nothing but a checkbox state telling them apart. This absorbs the old
    /// <c>.codemap-view-note</c> — same fact, in the frame's own slot rather than a bespoke paragraph.</summary>
    private static string VariantTitle(CodeMapVariant variant) =>
        (variant.ExcludesSpecDev, variant.ExcludesTests) switch
        {
            (true, true) => "Source Code Map — excluding spec-driven development directories and tests",
            (true, false) => "Source Code Map — excluding spec-driven development directories",
            (false, true) => "Source Code Map — excluding tests",
            _ => "Source Code Map — every file",
        };

    /// <summary>The dimension-switch control — a dropdown, keyboard-operable, present whenever the variant has
    /// files (Story 7.9 loosened this from "only when git metrics exist" — file type needs no git data). Emitted
    /// <c>hidden</c>; the enhancement script reveals it (scoped to this panel) and re-fills the rects on change.
    /// With JS off the treemap keeps its server-baked default, so an inert control never shows. When
    /// <paramref name="hasMetrics"/> is true, "File type" is a 7th option appended after the six unchanged
    /// git-derived ones (unchanged baked default: change frequency); when false, it's the ONLY option and the
    /// baked default. A <c>&lt;select&gt;</c> rather than a radio group (round 2) — reads better as one compact
    /// dropdown than a many-item radio list. [Subtask 5.2; Story 7.9]</summary>
    private static void AppendColorizeControls(StringBuilder sb, bool hasMetrics)
    {
        // No `hidden` here any more: this rides INSIDE the component's own hidden control bar, which is revealed
        // by the same successful mount. Two nested `hidden` layers would leave the select invisible after mount.
        sb.Append("    <div class=\"codemap-controls\">\n");
        sb.Append("      <label class=\"codemap-controls-label\">Colorize by\n");
        sb.Append("        <select class=\"codemap-dim-select\" data-hierarchy-dimension aria-label=\"Colorize the treemap and sunburst by\">\n");
        if (hasMetrics)
        {
            AppendOption(sb, "changes", "Change frequency", true);
            AppendOption(sb, "last", "Recently changed", false);
            AppendOption(sb, "created", "First changed", false);
            AppendOption(sb, "avgchange", "Avg change size", false);
            AppendOption(sb, "churn", "Churn", false);
            AppendOption(sb, "cochange", "Files changed together", false);
            AppendOption(sb, "filetype", "File type", false);
        }
        else
        {
            AppendOption(sb, "filetype", "File type", true);
        }
        sb.Append("        </select>\n");
        sb.Append("      </label>\n");
        sb.Append("    </div>\n");
    }

    private static void AppendOption(StringBuilder sb, string value, string label, bool selectedOption)
    {
        var sel = selectedOption ? " selected" : string.Empty;
        sb.Append($"          <option value=\"{value}\"{sel}>{PathUtil.Html(label)}</option>\n");
    }

    /// <summary>The sequential-ramp legend for the change-frequency dimension — reuses the commit-heatmap ramp
    /// levels (a non-<c>--status-*</c> scale). Server-baked visible only when it explains the baked-in default
    /// (git metrics present); otherwise pre-rendered <c>hidden</c> so the client-side dimension switch can reveal
    /// it without a DOM rewrite when the user picks a numeric dimension from the dropdown. Each swatch carries a
    /// real change-count range from <see cref="Charts.CodeMapChangeLevelRange"/> — never the literal "Less … More"
    /// placeholder, per AC #1 of Story 7.12 (which this ramp's sunburst sibling also shares). [Subtask 4.3;
    /// Story 7.9; Review 2026-07-22]</summary>
    private static void AppendLegend(StringBuilder sb, bool hasMetrics, double maxChanges)
    {
        // `data-hierarchy-legend` names which dimensions own this block (the six numeric ramps share it); the
        // caption is a TEMPLATE the component substitutes the active dimension's own label into, so the words
        // stay this surface's and the component never learns them. Initial `hidden` is only the pre-mount state —
        // the component sets it explicitly for every block on the first dimension apply.
        sb.Append("    <div class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"")
          .Append(HierarchyExplorer.CodeMapRampLegend).Append('"').Append(hasMetrics ? "" : " hidden").Append(">");
        sb.Append("<span class=\"codemap-legend-dim\" data-hierarchy-legend-caption=\"Colorized by {label}\">Colorized by change frequency</span> ");
        for (var l = 0; l <= 4; l++)
        {
            if (l > 0 && Charts.IsCodeMapChangeLevelUnreachable(l, maxChanges)) continue;
            var label = l == 0 ? "0 changes" : Charts.CodeMapChangeLevelRange(l, maxChanges);
            sb.Append($"<span class=\"codemap-legend-swatch level-{l}\"></span>");
            sb.Append($"<span class=\"codemap-legend-label\">{PathUtil.Html(label)}</span> ");
        }
        sb.Append("</div>\n");
    }

    /// <summary>The discrete (categorical) legend for the "File type" dimension — a swatch + human label per
    /// category actually present in this variant's file set (never every possible category, so a repo with no
    /// config files doesn't show an unused "Config &amp; Data" swatch). Pre-rendered alongside
    /// <see cref="AppendLegend"/>: whichever legend explains the currently-baked default ships visible, the other
    /// <c>hidden</c>, and the client-side dimension switch simply toggles which one is shown rather than rewriting
    /// either one's content (both are static once rendered — this variant's category set never changes at
    /// runtime). [Story 7.9]</summary>
    private static void AppendDiscreteLegend(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics)
    {
        var present = CodeFileType.AllCategories.Where(cat => files.Any(f => f.Category == cat)).ToList();

        sb.Append("    <div class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"")
          .Append(HierarchyExplorer.CodeMapDiscreteLegend).Append('"').Append(hasMetrics ? " hidden" : "").Append(">");
        sb.Append("<span class=\"codemap-legend-dim\" data-hierarchy-legend-caption=\"Colorized by {label}\">Colorized by file type</span> ");
        foreach (var cat in present)
        {
            sb.Append($"<span class=\"codemap-legend-swatch type-{cat.Key}\"></span>");
            sb.Append($"<span class=\"codemap-legend-label\">{PathUtil.Html(cat.Label)}</span> ");
        }
        sb.Append("</div>\n");
    }

    /// <summary>The text-equivalent table — the no-JS truth of the visualization and the screen-reader listing:
    /// every file with its path, line count, and (when present) git metrics as TEXT, so color is never the sole
    /// signal (AC #4). Ordered by the default dimension (change frequency) descending, then lines, so the reading
    /// order is meaningful. Each path links to its in-portal code page when the guarded resolver supplies one.
    /// [Subtask 4.6]
    /// <para><b>Story 10.8 scope:</b> stays a genuine <c>&lt;table&gt;</c> (Design Direction #5) — its multi-column
    /// numeric header row is load-bearing for the accessible/no-JS reading of the treemap, and files carry no
    /// lifecycle status, so there is no badge to route through the shared row primitive. Only a badge-bearing
    /// row family gets rewired onto <see cref="ListRow"/>.</para></summary>
    private static void AppendFileTable(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics, Func<string, string?>? fileHref, string prefix)
    {
        // Ordering is the SAME Charts.OrderBySignificance the treemap's detail cap uses — shared, not a second
        // hand-rolled copy, so the two text-equivalents of the visualization can never silently disagree on which
        // files count as "most significant." [Review][Patch: DRY]
        var ordered = Charts.OrderBySignificance(files).ToList();

        // Deferred item (at-scale SPA perf pass): capped at very large scale, the SAME cap+ordering the treemap's
        // rich tooltips use, so a file with a table row also has a tooltip and vice versa. Below the cap (every
        // real project so far — Epic-7 scale is ~1,060 files) this is a no-op: `shown` == `ordered`,
        // byte-identical to before.
        var cap = Charts.MaxDetailedCodeMapFiles;
        var shown = ordered.Count > cap ? ordered.Take(cap).ToList() : ordered;
        var omittedCount = ordered.Count - shown.Count;

        sb.Append("    <section class=\"chart-panel\">\n");
        sb.Append("      <h3>All files</h3>\n");
        var leadScope = omittedCount > 0
            ? $"The {cap.ToString("N0", CultureInfo.InvariantCulture)} most significant files in the treemap"
            : "Every file in the treemap";
        sb.Append($"      <p class=\"chart-lead\">{leadScope}, listed as text{(hasMetrics ? ", ordered by change frequency" : ", ordered by size")}.</p>\n");
        sb.Append($"      <table class=\"codemap-table\" data-page-size=\"{CodeMapTablePageSize.ToString(CultureInfo.InvariantCulture)}\">\n");
        sb.Append("        <thead><tr><th scope=\"col\">File</th><th scope=\"col\" class=\"num\">Lines</th><th scope=\"col\">Type</th>");
        if (hasMetrics)
        {
            sb.Append("<th scope=\"col\" class=\"num\">Changes</th><th scope=\"col\" class=\"num\">Churn</th><th scope=\"col\" class=\"num\">Avg</th><th scope=\"col\" class=\"num\">Together</th><th scope=\"col\">First</th><th scope=\"col\">Last</th>");
        }
        sb.Append("</tr></thead>\n        <tbody>\n");

        foreach (var file in shown)
        {
            var href = fileHref?.Invoke(file.RepoRelativePath);
            var pathCell = href is { Length: > 0 } target
                ? $"<a href=\"{PathUtil.Html(prefix + target)}\">{PathUtil.Html(file.RepoRelativePath)}</a>"
                : PathUtil.Html(file.RepoRelativePath);

            sb.Append("          <tr class=\"codemap-table-row\"><th scope=\"row\">").Append(pathCell).Append("</th>");
            sb.Append($"<td class=\"num\">{file.Lines.ToString("N0", CultureInfo.InvariantCulture)}</td>");
            // Always present, independent of hasMetrics — the categorical dimension's text equivalent. [Story 7.9]
            sb.Append($"<td>{PathUtil.Html((file.Category ?? CodeFileType.Other).Label)}</td>");
            if (hasMetrics)
            {
                if (file.Metrics is { } m)
                {
                    var avg = m.Changes > 0 ? ((double)m.TotalChurn / m.Changes).ToString("N0", CultureInfo.InvariantCulture) : "—";
                    var together = m.AvgCoChanged is { } co ? co.ToString("N1", CultureInfo.InvariantCulture) : "—";
                    var first = m.FirstDate is { } fd ? PortalDates.Day(fd) : "—";
                    var last = m.LastDate is { } ld ? PortalDates.Day(ld) : "—";
                    sb.Append($"<td class=\"num\">{m.Changes.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{m.TotalChurn.ToString("N0", CultureInfo.InvariantCulture)}</td>");
                    sb.Append($"<td class=\"num\">{avg}</td>");
                    sb.Append($"<td class=\"num\">{together}</td>");
                    sb.Append($"<td>{first}</td><td>{last}</td>");
                }
                else
                {
                    sb.Append("<td class=\"num\">—</td><td class=\"num\">—</td><td class=\"num\">—</td><td class=\"num\">—</td><td>—</td><td>—</td>");
                }
            }
            sb.Append("</tr>\n");
        }

        if (omittedCount > 0)
        {
            var colspan = hasMetrics ? 9 : 3;
            sb.Append($"          <tr class=\"codemap-table-truncated\"><td colspan=\"{colspan}\">+{omittedCount.ToString("N0", CultureInfo.InvariantCulture)} more ")
              .Append(Charts.Plural(omittedCount, "file", "files"))
              .Append(" not shown in this table — each still has its own colored, focusable rectangle in the treemap above.</td></tr>\n");
        }

        sb.Append("        </tbody>\n      </table>\n");
        AppendCodeMapTablePager(sb);
        sb.Append("    </section>\n\n");
    }

    /// <summary>The number of rows the file table shows per page once client-side pagination kicks in — mirrors
    /// <see cref="RiskQuadrantTemplater"/>'s elevated-risk grid pager, sized larger since a table row is far
    /// denser than a card. Owner feedback (Story 7.12 review): the "All files" table could run to hundreds/
    /// thousands of rows on a real repo with no way to page through it.</summary>
    private const int CodeMapTablePageSize = 30;

    /// <summary>The client-side pager control for the file table (progressive enhancement ONLY — every row
    /// always renders in the markup, in order, as the complete no-JS truth; specscribe.js's
    /// <c>initCodemapTablePager</c> only reveals this control and hides off-page rows once there's more than one
    /// page's worth). Mirrors <see cref="RiskQuadrantTemplater"/>'s <c>.risk-pager</c> exactly — same shape, new
    /// class family so the two pagers can never cross-wire. Emitted <c>hidden</c>; sits immediately after the
    /// table (not before it) so a no-JS visitor never sees inert controls, matching the risk grid's own
    /// "controls belong at the bottom of the list they page" precedent.</summary>
    private static void AppendCodeMapTablePager(StringBuilder sb)
    {
        sb.Append("      <div class=\"codemap-table-pager\" hidden>\n");
        sb.Append("        <button type=\"button\" class=\"codemap-table-pager-prev\" aria-label=\"Previous page of files\">&lsaquo; Prev</button>\n");
        sb.Append("        <span class=\"codemap-table-pager-status\" aria-live=\"polite\"></span>\n");
        sb.Append("        <button type=\"button\" class=\"codemap-table-pager-next\" aria-label=\"Next page of files\">Next &rsaquo;</button>\n");
        sb.Append("      </div>\n");
    }
}
