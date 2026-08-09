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
        // unwrapped siblings of the ONE chart panel and the file table section (the CSS sibling-combinator toggle
        // needs them at the same nesting level as their targets), so nothing here can be a common ancestor of both.
        sb.Append("<h3>Source Code Map</h3>\n");
        sb.Append("<p class=\"chart-lead\">Every file, sized by its lines of code and nested inside its directory. ");
        sb.Append("Use \"Sunburst / Treemap\" to switch shape, and \"Colorize by\" to switch what the color encodes — a git-derived change signal when available, or file type. Select a directory to zoom in. Filter what's shown with the checkboxes below; the full text listing under the map works with JavaScript off.</p>\n\n");

        // Pure CSS: no JavaScript is needed for the file table's filtering to work (round 2). `data-hierarchy-view-toggle`
        // additionally lets the ONE chart instance below pick a matching view (Story 20.10 Task 2.3) — declarative,
        // like `data-hierarchy-reveal-when` before it, so the shared component never learns these ids mean anything.
        AppendFilterCheckbox(sb, "cm-exclude-spec", "Exclude spec-driven development directories");
        AppendFilterCheckbox(sb, "cm-exclude-tests", "Exclude tests");

        if (!full.Map.IsEmpty)
        {
            AppendCodeMapPanel(sb, variants, fileHref, prefix);
        }

        sb.Append("</main>\n\n");

        // Materialised ONCE. [Story 23.4 code review, finding F-22] This is the largest body the generator
        // produces — `code-map.html` measured 8,012,656 B — and it was being built twice, once for the
        // `ContainsHost` scan and once for `BodyHtml`. Every sibling templater needing the same flag hoists it
        // first (`GitInsightsTemplater`, `HtmlTemplater`); this one did not.
        var body = sb.ToString();

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
                HierarchyEngineNeeded = HierarchyExplorer.ContainsHost(body),
            },
            Interaction = InteractionState.None,
            BodyHtml = body,
        };
    }

    /// <summary>The pure-CSS filter checkboxes (Story 20.9 owner decision D2, kept unchanged by Story 20.10 D3):
    /// the ONE filter on this page that works with JavaScript off. Trading it for a byte win would take
    /// information away from a no-JS visitor to make a chart cheaper.
    ///
    /// <para><c>data-hierarchy-view-toggle</c> is Story 20.10's addition, replacing the retired
    /// <c>data-hierarchy-reveal-when</c> (F5 — its only consumer, the four-panel reveal-by-hash path, is gone now
    /// that there is one always-visible chart instance). It declares these checkboxes as inputs to the component's
    /// VIEW switch (Task 2.3): the component reads each toggle's own id and checked state and matches the result
    /// against a view's own <c>when</c> string (e.g. <c>"cm-exclude-spec=1;cm-exclude-tests=0"</c>) — the same
    /// declarative idiom <c>data-hierarchy-reveal-when</c> used, so the shared component still never learns what
    /// these two checkboxes mean. <c>data-hierarchy-reveal</c> stays too: it is the general zero-width deferred-
    /// mount guard's trigger (Story 20.9 F1), a different and still-live capability.</para></summary>
    private static void AppendFilterCheckbox(StringBuilder sb, string id, string label)
    {
        sb.Append($"  <input type=\"checkbox\" id=\"{id}\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>");
        sb.Append($"<label for=\"{id}\" class=\"codemap-filter-label\">{PathUtil.Html(label)}</label>\n");
    }

    /// <summary>The Code Map explorer's configured size, applied by the component as a HEIGHT capped to its own
    /// width (never a width — <c>HierarchyExplorerConfig.Size</c> is one int and the container supplies the
    /// width). The retired SVG's 1000×640 was a fixed viewBox for a chart that neither labelled nor drilled;
    /// only the 640 carries over, and only because it is the height. Verified live rather than ported on faith
    /// (Story 20.9 F5).</summary>
    private const int CodeMapExplorerSize = 640;

    /// <summary>Renders the ONE Code Map chart instance (Story 20.10 owner decision D2), replacing the four
    /// independently-serialized <c>.codemap-view</c> panels: a "View as" shape toggle (Treemap/Sunburst) crossed
    /// with a shared "Colorize by" dimension dropdown, the drill breadcrumb (Treemap zoom only), all four variants'
    /// legend pairs (pre-rendered, the active view × active dimension pair shown), and one shape. The framed title
    /// and analysis window are the DEFAULT ("full") view's own strings server-side (F4) — the checkboxes drive a
    /// client-side swap once mounted (D2's accepted consequence: the chart is JavaScript-driven regardless of how
    /// many payloads back it). <paramref name="variants"/> must be non-empty and its "full" entry non-empty; the
    /// call site (<see cref="BuildPage"/>) already gates on that.</summary>
    private static void AppendCodeMapPanel(StringBuilder sb, IReadOnlyList<CodeMapVariant> variants, Func<string, string?>? fileHref, string prefix)
    {
        var full = variants.FirstOrDefault(v => v.Key == "full") ?? variants[0];

        // hasMetrics is now a WHOLE-PAGE property (Task 3.5): computed once over the DISTINCT file set (the
        // superset "full" variant already carries every file), not per-variant. A file's metrics either exist
        // (git ran) or they don't — that fact cannot disagree across views of the same repository.
        var distinctFiles = full.Map.Files();
        var hasMetrics = distinctFiles.Any(f => f.Metrics is not null);

        // "What to view" — the colorize dimension picker, unchanged in shape from Story 7.9/7.12; now global
        // rather than per-panel because there is only one panel and hasMetrics no longer varies by view.
        var controls = new StringBuilder();
        AppendColorizeControls(controls, hasMetrics);

        var legend = new StringBuilder();
        if (!hasMetrics)
        {
            // OUTSIDE the legend bar deliberately: this is a fact about the DATA, not chrome for a chart, so it
            // stands whether or not the chart ever mounts.
            legend.Append("    <p class=\"codemap-notice codemap-notice-secondary\" role=\"note\">Git change data is unavailable (run with <code>--deep-git</code> in a git repository to colorize by the six git-derived dimensions). The map is colorized by file type instead.</p>\n");
        }
        // Both legend shapes ship pre-rendered PER VIEW now (F4/D4): a view's ramp normalizes against its own file
        // subset, so its legend's real change-count ranges are that view's own. The component shows exactly the
        // pair belonging to the active view × active dimension, so the visible legend can never disagree with
        // what is coloured. The bar itself is hidden until a successful mount.
        legend.Append("    <div class=\"ss-hierarchy-legends\" hidden>\n");
        foreach (var variant in variants)
        {
            if (variant.Map.IsEmpty) continue;
            var vFiles = variant.Map.Files();
            var vMaxChanges = Charts.ComputeMaxChanges(variant.Map.Roots);
            AppendLegend(legend, hasMetrics, vMaxChanges, variant.Key);
            AppendDiscreteLegend(legend, vFiles, hasMetrics, variant.Key);
        }
        legend.Append("    </div>\n");

        // ONE instance, one DomId, one HashKey (Task 2.7 — the four `#cm-{key}=` deep links retire, same call
        // Story 20.9 made for `#dir=`; a shared link now encodes the view alongside the drilled scope).
        var config = new HierarchyExplorerConfig(
            DomId: "codemap",
            Shape: "treemap",
            Mode: HierarchyMode.Navigate,
            HashKey: "cm",
            Size: CodeMapExplorerSize,
            Labels: true,
            Meta: new Charts.ChartMeta(
                HierarchyExplorer.CodeMapViewTitle(full),
                Window: $"{full.Map.FileCount:N0} {Charts.Plural(full.Map.FileCount, "file", "files")} · {full.Map.TotalLines:N0} {Charts.Plural((int)Math.Min(full.Map.TotalLines, int.MaxValue), "line", "lines")}"),
            // Story 20.6 D1: the (now deduplicated) file table below IS this surface's twin.
            TwinDisplay: HierarchyTwinDisplay.External,
            ExternalTwinClass: "codemap-table-section",
            Dimensions: HierarchyExplorer.CodeMapDimensions(hasMetrics));

        var model = HierarchyExplorer.ProjectCodeMapViews(variants, config, fileHref, prefix);
        sb.Append(HierarchyExplorer.Render(
            model, "chart-panel codemap-panel", " data-explorer", controls.ToString(), legend.ToString()));

        AppendFileTree(sb, variants, full.Map, distinctFiles, hasMetrics, fileHref, prefix);
    }

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
    /// levels (a non-<c>--status-*</c> scale). Server-baked visible only for the DEFAULT ("full") view when it
    /// explains the baked-in default (git metrics present); otherwise pre-rendered <c>hidden</c> so the
    /// client-side dimension AND view switches can reveal it without a DOM rewrite. Each swatch carries a real
    /// change-count range from <see cref="Charts.CodeMapChangeLevelRange"/> — never the literal "Less … More"
    /// placeholder, per AC #1 of Story 7.12. Story 20.10 D4: <paramref name="maxChanges"/> is THIS VIEW's own
    /// scale (<see cref="Charts.ComputeMaxChanges"/> over the variant's own file subset), so the swatch ranges
    /// never silently disagree with what the chart actually paints once this view is active.
    /// [Subtask 4.3; Story 7.9; Review 2026-07-22]</summary>
    private static void AppendLegend(StringBuilder sb, bool hasMetrics, double maxChanges, string viewKey)
    {
        // `data-hierarchy-legend` names which DIMENSION owns this block; `data-hierarchy-legend-view` names which
        // VIEW it belongs to (Story 20.10) — the component shows the pair matching BOTH. The caption is a TEMPLATE
        // the component substitutes the active dimension's own label into, so the words stay this surface's.
        var visible = hasMetrics && viewKey == "full";
        sb.Append("    <div class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"")
          .Append(HierarchyExplorer.CodeMapRampLegend).Append("\" data-hierarchy-legend-view=\"").Append(PathUtil.Html(viewKey))
          .Append('"').Append(visible ? "" : " hidden").Append(">");
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
    /// category actually present in THIS VIEW's own file set (never every possible category, so a repo with no
    /// config files doesn't show an unused "Config &amp; Data" swatch, and a filtered view can legitimately show
    /// FEWER swatches than "full"). Pre-rendered alongside <see cref="AppendLegend"/> for the same view: whichever
    /// legend explains the currently-baked default ships visible, the rest <c>hidden</c>. [Story 7.9; Story 20.10]</summary>
    private static void AppendDiscreteLegend(StringBuilder sb, IReadOnlyList<CodeMapNode> files, bool hasMetrics, string viewKey)
    {
        var present = CodeFileType.AllCategories.Where(cat => files.Any(f => f.Category == cat)).ToList();
        var visible = !hasMetrics && viewKey == "full";

        sb.Append("    <div class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"")
          .Append(HierarchyExplorer.CodeMapDiscreteLegend).Append("\" data-hierarchy-legend-view=\"").Append(PathUtil.Html(viewKey))
          .Append('"').Append(visible ? "" : " hidden").Append(">");
        sb.Append("<span class=\"codemap-legend-dim\" data-hierarchy-legend-caption=\"Colorized by {label}\">Colorized by file type</span> ");
        foreach (var cat in present)
        {
            sb.Append($"<span class=\"codemap-legend-swatch type-{cat.Key}\"></span>");
            sb.Append($"<span class=\"codemap-legend-label\">{PathUtil.Html(cat.Label)}</span> ");
        }
        sb.Append("</div>\n");
    }

    /// <summary>The text-equivalent table — the no-JS truth of the visualization and the screen-reader listing:
    /// every DISTINCT file with its path, line count, and (when present) git metrics as TEXT, so color is never
    /// the sole signal (AC #4). Story 20.10 D3: ONE table now, over the shared distinct-file set, instead of one
    /// per variant — each row carries <c>is-spec</c>/<c>is-test</c> marker classes from the SAME
    /// <see cref="CodeMap.IsSpecDevPath"/>/<see cref="CodeMap.IsTestPath"/> predicates <see cref="CodeMap.BuildVariants"/>
    /// itself filters by, and the stylesheet hides a row under exactly the checkbox combination that would have
    /// excluded it — the no-JS filter guarantee (owner decision D2 of Story 20.9) preserved BY CONSTRUCTION,
    /// because it is still pure CSS and still zero script. Ordered by <see cref="Charts.OrderBySignificance"/>, the
    /// SAME ordering the treemap's detail cap uses, so the two text-equivalents of the visualization can never
    /// silently disagree on which files count as "most significant." Each path links to its in-portal code page
    /// when the guarded resolver supplies one. [Subtask 4.6]
    /// <para>The per-view LEAD sentence and the truncation row's applicability differ by view (a view with fewer
    /// files may not hit the cap even when "full" does), so <paramref name="variants"/> drives one <c>&lt;p&gt;</c>
    /// per view, toggled by the SAME 4-combination checkbox selector the panel switch used to use (Task 4.4) — a
    /// no-JS visitor still reads the correct sentence for whichever combination is checked.</para>
    /// <para><b>Story 10.8's Design Direction #5 — "stays a genuine <c>&lt;table&gt;</c>" — is AMENDED, not
    /// abandoned.</b> Owner feedback 2026-08-01: "Code Map All Files should be a hierarchy with expand / collapse."
    /// A flat listing of every path in the repository is not readable at real scale, and the directory structure
    /// this section flattens was already built and thrown away (<see cref="CodeMap.Roots"/>). What #5 was actually
    /// protecting — the multi-column numeric header row, load-bearing for the accessible/no-JS reading of the
    /// treemap — is preserved exactly: there is still a real <c>&lt;table&gt;</c> with a real
    /// <c>&lt;th scope="col"&gt;</c> header at every level. There are simply N of them, one per directory, nested
    /// inside <c>&lt;details&gt;</c>.</para>
    ///
    /// <para><b>Why this markup and not a nested list.</b> <c>&lt;details&gt;</c> cannot wrap <c>&lt;tr&gt;</c> —
    /// <c>&lt;tbody&gt;</c>'s content model is <c>&lt;tr&gt;</c> alone — but <c>&lt;details&gt;</c>' own content
    /// model is FLOW content, and a <c>&lt;table&gt;</c> is flow content. So each directory's
    /// <c>&lt;details&gt;</c> holds its child directories' <c>&lt;details&gt;</c> followed by exactly one
    /// <c>&lt;table&gt;</c> of that directory's OWN files. No table nests inside a cell; every table is a sibling
    /// of the child disclosures. Valid HTML, and <c>&lt;details&gt;</c> is natively keyboard-operable with zero
    /// script — the disclosure works with JavaScript off, which the retired pager did not.</para></summary>
    private static void AppendFileTree(
        StringBuilder sb, IReadOnlyList<CodeMapVariant> variants, CodeMap full,
        IReadOnlyList<CodeMapNode> distinctFiles,
        bool hasMetrics, Func<string, string?>? fileHref, string prefix)
    {
        var ordered = Charts.OrderBySignificance(distinctFiles).ToList();

        // The cap applies ONCE now, against the distinct set (F7) — so the chart and the table agree on which
        // files are "detailed" no matter how many views a file appears in.
        var cap = Charts.MaxDetailedCodeMapFiles;
        var shown = ordered.Count > cap ? ordered.Take(cap).ToList() : ordered;

        // [Review][Patch] How many of a VIEW's OWN files have no row is `|view| - |view ∩ shown|`, which is NOT
        // `view.FileCount - cap`. Rows are capped against the DISTINCT set, so a view smaller than the cap can
        // still lose files — its members simply rank below the global top-`cap` — and the old arithmetic reported
        // zero omissions for exactly that case, printing "Every file in the treemap" over a table missing rows.
        // That breaks the ADR 0013 §2 twin-completeness claim AC#3 makes for EVERY variant, silently, on any repo
        // past `MaxDetailedCodeMapFiles` files. Invisible at this repo's scale (F7's own caveat), which is why it
        // needs deriving rather than assuming.
        var shownPaths = new HashSet<string>(shown.Select(f => f.RepoRelativePath), StringComparer.Ordinal);
        var omittedByView = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var variant in variants)
        {
            omittedByView[variant.Key] = variant.Map.IsEmpty
                ? 0
                : variant.Map.Files().Count(f => !shownPaths.Contains(f.RepoRelativePath));
        }

        sb.Append("    <section class=\"chart-panel codemap-table-section\">\n");
        sb.Append("      <h3>All files</h3>\n");

        foreach (var variant in variants)
        {
            string leadText;
            if (variant.Map.IsEmpty)
            {
                leadText = "No files match this filter.";
            }
            else
            {
                var vOmitted = omittedByView[variant.Key];
                var vShown = variant.Map.FileCount - vOmitted;
                var leadScope = vOmitted > 0
                    ? $"The {vShown.ToString("N0", CultureInfo.InvariantCulture)} most significant files in the treemap"
                    : "Every file in the treemap";
                leadText = $"{leadScope}, listed as text{(hasMetrics ? ", ordered by change frequency" : ", ordered by size")}.";
            }
            sb.Append($"      <p class=\"chart-lead\" data-codemap-view=\"{PathUtil.Html(variant.Key)}\">{PathUtil.Html(leadText)}</p>\n");
        }

        // The ancestor directories of the most significant files open on load. Everything else is one click away.
        // All-collapsed would open on a page showing nothing (a regression from the flat table's 18 visible rows);
        // first-level-only opens `src/SpecScribe` and its ~300 files as a wall. This opens roughly a screenful
        // centred on the busiest code, which is what the lead sentence promises the listing is ordered by.
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in shown.Take(TreeAutoExpandFiles))
        {
            var path = file.RepoRelativePath;
            for (var slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
            {
                expanded.Add(path[..slash]);
            }
        }

        sb.Append("      <div class=\"codemap-tree\">\n");
        foreach (var node in Charts.OrderCodeMapLevel(full.Roots))
        {
            AppendTreeNode(sb, node, shownPaths, expanded, hasMetrics, fileHref, prefix, depth: 0);
        }
        sb.Append("      </div>\n");

        // ONE truncation notice per view, each carrying THAT view's own omission count and tagged with the same
        // `data-codemap-view` marker every other per-view fact in this section uses. Views that omit nothing emit
        // nothing at all.
        //
        // It is a <p> now rather than a <tr colspan>: with N tables there is no single table for it to be the last
        // row of, and a notice about the whole listing does not belong inside one directory's table.
        foreach (var variant in variants)
        {
            var vOmitted = omittedByView[variant.Key];
            if (vOmitted == 0) continue;
            sb.Append($"      <p class=\"chart-lead codemap-table-truncated\" data-codemap-view=\"{PathUtil.Html(variant.Key)}\">+{vOmitted.ToString("N0", CultureInfo.InvariantCulture)} more ")
              .Append(Charts.Plural(vOmitted, "file", "files"))
              .Append(" not shown in this listing — each still has its own colored, focusable rectangle in the treemap above.</p>\n");
        }

        sb.Append("    </section>\n\n");
    }

    /// <summary>Emits one node of the tree: a directory as a <c>&lt;details&gt;</c> containing its child
    /// directories then one <c>&lt;table&gt;</c> of its own files, or nothing at all for a file (files are emitted
    /// by their parent, so this is only ever called with a directory at the top level and recurses on directories).
    /// <para>A directory whose entire subtree fell outside the <see cref="Charts.MaxDetailedCodeMapFiles"/> cap
    /// emits NOTHING — an empty disclosure a reader can open to find nothing inside is worse than an absent one,
    /// and the truncation notice already accounts for those files.</para>
    /// <para><paramref name="depth"/> guards against a pathological path producing unbounded nesting, mirroring
    /// <see cref="HierarchyExplorer"/>'s twin emitter. Real repositories do not approach it.</para></summary>
    private static void AppendTreeNode(
        StringBuilder sb, CodeMapNode node, HashSet<string> shownPaths, HashSet<string> expanded,
        bool hasMetrics, Func<string, string?>? fileHref, string prefix, int depth)
    {
        if (!node.IsDirectory || depth > MaxTreeDepth) return;

        var ownFiles = node.Children
            .Where(c => !c.IsDirectory && shownPaths.Contains(c.RepoRelativePath))
            .ToList();
        var childDirs = node.Children.Where(c => c.IsDirectory).ToList();
        // Nothing under here survived the cap → emit nothing rather than an empty disclosure.
        if (ownFiles.Count == 0 && !childDirs.Any(d => SubtreeHasShownFile(d, shownPaths))) return;

        var descendants = DescendantFilePaths(node).ToList();
        var open = expanded.Contains(node.RepoRelativePath) ? " open" : string.Empty;

        sb.Append($"        <details class=\"codemap-tree-dir{DirectoryFilterMarkers(descendants)}\"{open}>\n");
        sb.Append("          <summary>");
        sb.Append($"<span class=\"codemap-tree-path\">{PathUtil.Html(node.Label)}</span>");
        sb.Append($"<span class=\"codemap-tree-meta\">{descendants.Count.ToString("N0", CultureInfo.InvariantCulture)} {Charts.Plural(descendants.Count, "file", "files")} · {node.Lines.ToString("N0", CultureInfo.InvariantCulture)} {Charts.Plural((int)Math.Min(node.Lines, int.MaxValue), "line", "lines")}</span>");
        sb.Append("</summary>\n");

        foreach (var child in Charts.OrderCodeMapLevel(childDirs))
        {
            AppendTreeNode(sb, child, shownPaths, expanded, hasMetrics, fileHref, prefix, depth + 1);
        }

        if (ownFiles.Count > 0)
        {
            AppendFileTable(sb, Charts.OrderCodeMapLevel(ownFiles), hasMetrics, fileHref, prefix);
        }

        sb.Append("        </details>\n");
    }

    /// <summary>One directory's own files as a real <c>&lt;table&gt;</c> — the part of Design Direction #5 that is
    /// preserved verbatim. Every level emits a full <c>&lt;thead&gt;</c> with <c>&lt;th scope="col"&gt;</c>, so the
    /// column semantics survive at every depth unconditionally; whether a nested header is VISUALLY repeated is a
    /// pure stylesheet decision, which is where it belongs rather than baked into the markup.
    /// <para>The row shape — <c>&lt;tr class="codemap-table-row is-spec|is-test"&gt;&lt;th scope="row"&gt;</c> with
    /// the FULL repo-relative path as the link text — is byte-identical to the flat table's. That is deliberate on
    /// two counts: it is what keeps the pure-CSS spec/test filter working with no rule change (the selector is a
    /// DESCENDANT of the section, so deeper nesting is a no-op), and the full path is what discharges ADR 0013 §2's
    /// completeness contract. Indentation carries the hierarchy; it must not also have to carry the identity.</para></summary>
    private static void AppendFileTable(
        StringBuilder sb, IEnumerable<CodeMapNode> files, bool hasMetrics,
        Func<string, string?>? fileHref, string prefix)
    {
        sb.Append("          <table class=\"codemap-table\">\n");
        sb.Append("            <thead><tr><th scope=\"col\">File</th><th scope=\"col\" class=\"num\">Lines</th><th scope=\"col\">Type</th>");
        if (hasMetrics)
        {
            sb.Append("<th scope=\"col\" class=\"num\">Changes</th><th scope=\"col\" class=\"num\">Churn</th><th scope=\"col\" class=\"num\">Avg</th><th scope=\"col\" class=\"num\">Together</th><th scope=\"col\">First</th><th scope=\"col\">Last</th>");
        }
        sb.Append("</tr></thead>\n            <tbody>\n");

        foreach (var file in files)
        {
            var href = fileHref?.Invoke(file.RepoRelativePath);
            var pathCell = href is { Length: > 0 } target
                ? $"<a href=\"{PathUtil.Html(prefix + target)}\">{PathUtil.Html(file.RepoRelativePath)}</a>"
                : PathUtil.Html(file.RepoRelativePath);

            var rowClass = "codemap-table-row";
            if (CodeMap.IsSpecDevPath(file.RepoRelativePath)) rowClass += " is-spec";
            if (CodeMap.IsTestPath(file.RepoRelativePath)) rowClass += " is-test";

            sb.Append("              <tr class=\"").Append(rowClass).Append("\"><th scope=\"row\">").Append(pathCell).Append("</th>");
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

        sb.Append("            </tbody>\n          </table>\n");
    }

    /// <summary>The directory-level half of the pure-CSS spec/test filter, precomputed here so the stylesheet needs
    /// three flat rules instead of counting.
    ///
    /// <para>Rows hide themselves — the existing <c>.codemap-table-row.is-spec</c> rules are descendant selectors
    /// and are unaffected by nesting. A DIRECTORY must also disappear when every file beneath it would be filtered
    /// out, or the listing shows empty disclosures. Emitting the predicate as a class is what keeps that expressible
    /// without JavaScript and without <c>:has()</c>: it is a pure function over the subtree, unit-testable on its
    /// own, and it cannot disagree with the server-side truth the way a CSS-evaluated approximation could.</para>
    ///
    /// <para><b>Computed over DESCENDANT FILE paths, never the directory's own path.</b>
    /// <see cref="CodeMap.IsSpecDevPath"/> matches on <c>prefix + "/"</c>, so it returns <c>false</c> for the
    /// directory <c>.claude</c> itself while returning <c>true</c> for everything inside it — a marker derived from
    /// the directory path would be wrong for exactly the directories the filter exists to hide. Deriving both
    /// markers the same way also means a non-test-NAMED directory holding only test files is correctly hidden.</para></summary>
    private static string DirectoryFilterMarkers(IReadOnlyList<string> descendantFiles)
    {
        if (descendantFiles.Count == 0) return string.Empty;

        var allSpec = true;
        var allTest = true;
        var allExcluded = true;
        foreach (var path in descendantFiles)
        {
            var spec = CodeMap.IsSpecDevPath(path);
            var test = CodeMap.IsTestPath(path);
            if (!spec) allSpec = false;
            if (!test) allTest = false;
            if (!spec && !test) allExcluded = false;
            if (!allSpec && !allTest && !allExcluded) break;
        }

        var markers = string.Empty;
        if (allSpec) markers += " dir-all-spec";
        if (allTest) markers += " dir-all-test";
        if (allExcluded) markers += " dir-all-excluded";
        return markers;
    }

    private static IEnumerable<string> DescendantFilePaths(CodeMapNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
            {
                foreach (var path in DescendantFilePaths(child)) yield return path;
            }
            else
            {
                yield return child.RepoRelativePath;
            }
        }
    }

    private static bool SubtreeHasShownFile(CodeMapNode node, HashSet<string> shownPaths)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
            {
                if (SubtreeHasShownFile(child, shownPaths)) return true;
            }
            else if (shownPaths.Contains(child.RepoRelativePath))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>How many of the most significant files get their ancestor directories opened on load. Sized so the
    /// listing opens on roughly a screenful of the busiest code — the same job the retired 18-row pager page did,
    /// achieved by disclosure rather than by pagination.
    ///
    /// <para><b>The pager is gone, deliberately.</b> It existed because of owner feedback at the Story 7.12 review
    /// ("hundreds/thousands of rows with no way to page through it"), and collapsed directories answer that
    /// complaint strictly better: they are structural rather than arbitrary, and <c>&lt;details&gt;</c> works with
    /// JavaScript off, which the pager did not. The two cannot honestly coexist — a pager over a partially expanded
    /// tree reports a page count that changes on every disclosure click, and <c>initCodemapTablePager</c> already
    /// had to reconcile two hiding mechanisms (<c>row.hidden</c> and the CSS filter); <c>&lt;details&gt;</c> would
    /// have been a third. <see cref="RiskQuadrantTemplater"/>'s own <c>.risk-pager</c> is a separate class family
    /// and is untouched.</para></summary>
    private const int TreeAutoExpandFiles = 25;

    /// <summary>Depth guard for the recursive emit — a defence against a pathological path, matching the cap
    /// <see cref="HierarchyExplorer"/>'s twin emitter uses. No real repository approaches it.</summary>
    private const int MaxTreeDepth = 12;
}
