using System.Reflection;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Cheap guards over the embedded stylesheet so the Story 1.4 accessibility floor (a shared
/// focus-visible ring, a reduced-motion block, and the skip link) can't be silently deleted in a later
/// refactor without a test going red.</summary>
public class StylesheetTests
{
    private static string ReadStylesheet()
    {
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.css")
            ?? throw new InvalidOperationException("Embedded stylesheet 'SpecScribe.assets.specscribe.css' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Stylesheet_HasReducedMotionBlock()
        => Assert.Contains("@media (prefers-reduced-motion: reduce)", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasFocusVisibleRing()
        => Assert.Contains(":focus-visible", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasSkipLinkRule()
        => Assert.Contains(".skip-link", ReadStylesheet());

    // ---- Story 1.5 seams -------------------------------------------------------------------

    [Fact]
    public void Stylesheet_HasNoPreferenceEntranceBlock()
        // The complementary "no-preference" half of the motion contract sits beside the "reduce" block. [Story 1.5 D2]
        => Assert.Contains("@media (prefers-reduced-motion: no-preference)", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasPerStatusTokens()
    {
        // One CSS variable per lifecycle stage — the single stage→color source. [Story 1.5 B2/B3]
        var css = ReadStylesheet();
        Assert.Contains("--status-pending:", css);
        Assert.Contains("--status-drafted:", css);
        Assert.Contains("--status-ready:", css);
        Assert.Contains("--status-active:", css);
        Assert.Contains("--status-review:", css);
        Assert.Contains("--status-done:", css);
        Assert.Contains("--status-deferred:", css);
        Assert.Contains("--status-unrecognized:", css); // Story 8.2
        Assert.Contains("--status-unrecognized-hatch:", css);
        Assert.Contains("var(--status-unrecognized-hatch)", css);
        Assert.Contains(".status-legend", css);
        Assert.Contains(".status-legend-toggle", css);
        Assert.Contains(".status-legend-panel", css);
        Assert.Contains(".status-legend-key-swatch.unrecognized", css);
        Assert.Contains(".status-badge.unrecognized", css);
        Assert.DoesNotContain(".doc-footer-credit", css);
        Assert.DoesNotContain("grid-template-columns: repeat(3, minmax(0, 1fr))", css); // no triple-wide footer legend
        Assert.DoesNotContain(".status-legend-key-text", css);
        Assert.Contains(".status-badge.retired", css);
        Assert.Contains(".status-legend-key-swatch.retired", css);
        Assert.Contains(".sprint-lane.retired .sprint-lane-head", css);
        Assert.Contains(".sprint-card.retired", css);
        Assert.Contains("grid-template-columns: repeat(var(--lane-count, 5), minmax(11.5rem, 1fr))", css);
        Assert.Contains(".sprint-lane-label", css);
        Assert.Contains("white-space: nowrap", css); // lane labels stay single-line with the count badge
        // Story 8.4 paired progress + readiness surfaces
        Assert.Contains(".story-status-pair", css);
        Assert.Contains(".sprint-card.no-plan", css);
        Assert.Contains(".sprint-lane-head.js-tip", css);
        Assert.Contains(".epic-mosaic-delivery", css);
        // Story 8.5 primary / demoted-alternates next-steps hierarchy
        Assert.Contains(".next-steps-primary", css);
        Assert.Contains(".next-steps-alternates", css);
        Assert.Contains(".next-steps-alt", css);
        // Story 8.6 designed empty states
        Assert.Contains(".epic-undrafted-banner", css);
        Assert.Contains(".sprint-lane-empty", css);
        // Story 8.8 generation-time recency marker on story cards
        Assert.Contains(".story-card-updated", css);
    }

    [Fact]
    public void Stylesheet_HasOnBrandTooltipStyles()
    {
        // CSS-only tooltip for compact chrome + the JS-positioned body-level node for stats/SVG. [Story 1.5 C1/C2]
        var css = ReadStylesheet();
        Assert.Contains("[data-tooltip]", css);
        Assert.Contains("::after", css);
        Assert.Contains(".ss-tooltip", css);
        Assert.Contains(".stat-card.js-tip:focus-visible", css);
    }

    [Fact]
    public void Stylesheet_HasKeyViewGroupingAndJourneySegmentHooks()
    {
        var css = ReadStylesheet();
        Assert.Contains(".key-view-group", css);
        Assert.Contains(".key-view-panel", css);
        Assert.Contains(".key-view-group.is-open", css);
        Assert.Contains(".journey-card", css);
        Assert.Contains(".tile-journey", css);
        Assert.Contains(".tile-journey-cards", css);
        Assert.Contains(".journey-requirements", css);
        Assert.Contains(".journey-followup", css);
        Assert.Contains(".tile-card-visual", css);
    }

    [Fact]
    public void Stylesheet_HasCopyButtonStyles()
        => Assert.Contains(".cmd-copy", ReadStylesheet());

    [Fact]
    public void Stylesheet_HasCodeFilePageStyles()
    {
        // Story 7.1 in-portal code pages: the code surface, the anchored line rows/gutter, and the :target
        // line highlight the "#L{n}" deep-links rely on must survive later refactors.
        var css = ReadStylesheet();
        Assert.Contains(".code-file", css);
        Assert.Contains(".code-line", css);
        // Line numbers are a CSS gutter counter on data-ln (kept out of tokenized text so Prism never colors them).
        Assert.Contains(".code-line::before", css);
        Assert.Contains(".code-line:target", css);
        // Prism token colors are scoped to the code surface so no other <code> on the site is recolored.
        Assert.Contains(".code-file .token.comment", css);
    }

    [Fact]
    public void Stylesheet_HasAdvancedCoverageStyles()
    {
        // Story 7.4 opt-in "Advanced coverage" section: the surface, its contributor list, and the change-history
        // table must survive later refactors so a deep-git run stays styled. (Story 7.8 retired the visible coupled
        // list here — coupling now renders on the reference graph.)
        var css = ReadStylesheet();
        Assert.Contains(".code-insights", css);
        Assert.Contains(".code-insight-contributors", css);
        Assert.Contains(".code-history-table", css);
        Assert.Contains(".code-insight-more", css);
        // The retired coupled-list styles are gone (its relationship moved to the graph).
        Assert.DoesNotContain(".code-insight-coupled", css);
    }

    [Fact]
    public void Stylesheet_HasReferencedByStyles()
    {
        // Story 7.1 (rework) relationships block + reference graph on code pages — neutral tokens only, no --status-*.
        var css = ReadStylesheet();
        Assert.Contains(".code-relationships", css);
        Assert.Contains(".ref-graph", css);
        // Story 7.8 — the related-file node population is distinguished by shape (diamond) AND edge (dashed), never
        // colour alone: the dedicated classes must be present and the dashed edge must carry a dash pattern.
        Assert.Contains(".ref-edge-file", css);
        Assert.Contains(".ref-file-dot", css);
        // [Review][Patch] scoped to the .ref-edge-file rule block itself — stroke-dasharray also appears in
        // unrelated pre-existing rules, so a bare Assert.Contains would pass even if this rule lost its dash pattern.
        Assert.Matches(new Regex(@"\.ref-edge-file\s*\{[^}]*stroke-dasharray"), css);
    }

    [Fact]
    public void Stylesheet_HasTimelineAndArtifactsUpdatedStyles()
    {
        // Story 7.3 — the timeline.html surface and the date pages' "Artifacts updated" section. Neutral tokens
        // only (never --status-*, activity is not a lifecycle state). [Review][Patch] no companion test existed.
        var css = ReadStylesheet();
        Assert.Contains(".timeline-list", css);
        Assert.Contains(".timeline-row", css);
        Assert.Contains(".timeline-heatmap", css);
        Assert.Contains(".artifacts-updated", css);
        Assert.Contains(".artifact-update-list", css);
    }

    [Fact]
    public void Stylesheet_HasSendMenuStyles()
    {
        // The unified command badge and its send menu (native <details>) with shared menu rows.
        var css = ReadStylesheet();
        Assert.Contains(".cmd-badge", css);
        Assert.Contains(".send-menu", css);
        Assert.Contains(".send-toggle", css);
        Assert.Contains(".send-item", css);
    }

    [Fact]
    public void Stylesheet_HasIconSizingRule()
    {
        // .ss-icon handles em-relative sizing/alignment only; color comes from currentColor inheritance, so no
        // per-status hex belongs in this rule (that stays in .status-badge.* above it). [Story 2.5 Task 5]
        var css = ReadStylesheet();
        Assert.Contains(".ss-icon", css);
        var ruleStart = css.IndexOf(".ss-icon {", StringComparison.Ordinal);
        var ruleEnd = css.IndexOf('}', ruleStart);
        var rule = css[ruleStart..ruleEnd];
        Assert.DoesNotContain("#", rule);
    }

    [Fact]
    public void Stylesheet_HasCoveragePanelStyles()
    {
        // Cheap guard so the Story 3.3 coverage-panel seam can't be silently deleted in a later refactor.
        var css = ReadStylesheet();
        Assert.Contains(".coverage-card", css);
        Assert.Contains(".coverage-chip", css);
    }

    [Fact]
    public void Stylesheet_HasCodeMapStyles()
    {
        // Cheap guard so the Story 7.6 code-map treemap seam can't be silently deleted in a later refactor.
        var css = ReadStylesheet();
        Assert.Contains(".codemap-cell", css);
        Assert.Contains(".codemap-legend", css);
    }

    [Fact]
    public void Stylesheet_FunnelStagesRouteThroughStatusTokens()
    {
        // Every pipeline stage color resolves 1:1 to its own --status-* token (single stage→color source) —
        // the token routing can't silently regress to hardcoded hex. [Story 3.6, Story 1.5 B2/B3]
        var css = ReadStylesheet();
        Assert.Contains(".funnel-band.funnel-drafted { fill: var(--status-drafted); }", css);
        Assert.Contains(".funnel-band.funnel-ready { fill: var(--status-ready); }", css);
        Assert.Contains(".funnel-band.funnel-active { fill: var(--status-active); }", css);
        Assert.Contains(".funnel-band.funnel-review { fill: var(--status-review); }", css);
        Assert.Contains(".funnel-band.funnel-done { fill: var(--status-done); }", css);
    }

    // ---- Story 3.7 seams (requirement status blocks + requirements flow) ----------

    [Fact]
    public void Stylesheet_RequirementStatusBlocksRouteThroughStatusTokens()
    {
        // Every status-bearing block fill resolves 1:1 to its --status-* token (single stage→color source),
        // so the token routing can't silently regress to hardcoded hex. [Story 3.7 Task 4, Story 1.5 B2/B3]
        var css = ReadStylesheet();
        Assert.Contains(".req-status-block.done { background: var(--status-done); }", css);
        Assert.Contains(".req-status-block.active { background: var(--status-active); color: var(--warm-white); }", css);
        Assert.Contains(".req-status-block.ready { background: var(--status-ready); }", css);
        Assert.Contains(".req-status-block.pending { background: var(--status-pending); }", css);
        Assert.Contains(".req-status-block.deferred { background: var(--status-deferred); color: var(--warm-white); }", css);
    }

    [Fact]
    public void Stylesheet_RequirementFlowStatesRouteThroughStatusTokens()
    {
        // The Sankey's terminal state nodes carry the SAME status tokens as every other chart; the structural
        // definition/epic chrome uses neutral base-palette tones, never a status token. [Story 3.7 Task 4]
        var css = ReadStylesheet();
        Assert.Contains(".req-flow-state.done { fill: var(--status-done); }", css);
        Assert.Contains(".req-flow-state.active { fill: var(--status-active); }", css);
        Assert.Contains(".req-flow-state.ready { fill: var(--status-ready); }", css);
        Assert.Contains(".req-flow-state.pending { fill: var(--status-pending); }", css);
        Assert.Contains(".req-flow-state.deferred { fill: var(--status-deferred); }", css);
        // Structural nodes are NOT status tokens — they use the neutral parchment chrome.
        Assert.Contains(".req-flow-epic { fill: var(--parchment-dark)", css);
    }

    [Fact]
    public void Stylesheet_HasRequirementsViewToggleRules()
    {
        // Story 8.7: the home requirements panel consolidates its two renderings behind a panel-scoped
        // pure-CSS toggle (flow default, status-block grid demoted). The view-switch rules and the active-tab
        // styling for the panel-unique rv-flow/rv-grid radios must ship so the toggle can't silently regress.
        var css = ReadStylesheet();
        Assert.Contains(".req-view-grid { display: none; }", css);
        Assert.Contains(".req-panel:has(#rv-grid:checked) .req-view-flow { display: none; }", css);
        Assert.Contains(".req-panel:has(#rv-grid:checked) .req-view-grid { display: block; }", css);
        Assert.Contains("#rv-flow:checked ~ .board-tabbar label[for=\"rv-flow\"]", css);
        Assert.Contains("#rv-grid:checked ~ .board-tabbar label[for=\"rv-grid\"]", css);
        Assert.Contains("#rv-flow:focus-visible ~ .board-tabbar label[for=\"rv-flow\"]", css);
        Assert.Contains("#rv-grid:focus-visible ~ .board-tabbar label[for=\"rv-grid\"]", css);
        Assert.Contains(".req-panel-header-aside", css);
    }

    [Fact]
    public void Stylesheet_RequirementsPanelsShareTheStretchedColumn()
    {
        // Requirements index matches the home dashboard column (1100px + gutters); chart-panels stretch
        // with every other section rather than locking to a narrower flush column. [Story 9.2 UX]
        var css = ReadStylesheet();
        Assert.Contains(".req-index", css);
        Assert.Contains(".req-index .chart-panel", css);
        Assert.Contains("main.req-detail", css);
        Assert.Contains(".nfr-uxdr-epic-list", css);
        Assert.Contains(".nfr-uxdr-epic-card", css);
    }

    [Fact]
    public void Stylesheet_RequirementFlowJoinsBothReducedMotionSeams()
    {
        // The flow's entrance lives ONLY in the no-preference half and is explicitly cancelled in the reduce
        // half — the hard accessibility invariant (AC #3): reduced-motion users get the fully-formed diagram
        // at rest with zero information loss. [Story 3.7 Task 4.3]
        var css = ReadStylesheet();
        Assert.Contains(".req-flow", NoPreferenceBlock(css));
        Assert.Contains(".req-flow", ReduceBlock(css));
    }

    // ---- Story 3.5 seams (one tokenized, reduced-motion-safe insight motion vocabulary) ----------

    [Fact]
    public void Stylesheet_HasMotionTokens()
    {
        // One named motion family — the single-source discipline of --status-* applied to timing, so every
        // insight surface animates with the same feel instead of scattered literal seconds. [Story 3.5 Task 1]
        var css = ReadStylesheet();
        Assert.Contains("--motion-fast:", css);
        Assert.Contains("--motion-entrance:", css);
        Assert.Contains("--motion-entrance-long:", css);
        Assert.Contains("--motion-ease:", css);
        Assert.Contains("--motion-stagger:", css);
    }

    [Fact]
    public void Stylesheet_EntranceAnimationsRideMotionTokens()
    {
        // The existing 1.5 entrances were re-routed through the tokens (timings-only refactor), and the new
        // insight-surface entrances use the same family — no bare-second literals sneaking back in. [Story 3.5 Task 1]
        var css = ReadStylesheet();
        Assert.Contains("animation: progress-grow var(--motion-entrance-long)", css);
        Assert.Contains("animation: chart-enter var(--motion-entrance)", css);
    }

    [Fact]
    public void Stylesheet_NoPreferenceCoversInsightSurfaces()
    {
        // The heatmap cell reveal (staggered via --col/--motion-stagger) and the shared sibling-panel reveal
        // live in the no-preference half of the motion contract. [Story 3.5 Task 2]
        var css = ReadStylesheet();
        var block = NoPreferenceBlock(css);
        Assert.Contains(".heatmap .heatmap-cell", block);
        Assert.Contains("var(--motion-stagger)", block);
        Assert.Contains("cell-fade", block);
        Assert.Contains("panel-reveal", block);
        // Keyed to already-present sibling roots so an unbuilt surface (the funnel until 3.6) is a no-op.
        Assert.Contains(".git-pulse", block);
        Assert.Contains(".coverage", block);
        Assert.Contains(".codemap", block);
        Assert.Contains(".refinement-funnel", block);
    }

    [Fact]
    public void Stylesheet_ReduceBlockNeutralizesNewInsightMotion()
    {
        // The reduce half is a hard accessibility invariant (AC #2): every new entrance is explicitly cancelled
        // so reduced-motion users get the fully-formed surfaces at rest with zero information loss. [Story 3.5 Task 4]
        var css = ReadStylesheet();
        var block = ReduceBlock(css);
        Assert.Contains(".heatmap .heatmap-cell", block);
        Assert.Contains(".refinement-funnel", block);
        Assert.Contains("animation: none", block);
    }

    [Fact]
    public void Stylesheet_HasInteractiveLegendEmphasisRule()
    {
        // Pure-CSS interactive-legend emphasis (UXO C3, deferred here by Story 1.5): a legend hover/focus dims
        // the non-matching sunburst segments via :has(). Class toggle stayed out of JS. [Story 3.5 Task 3]
        // Keyed off :focus-visible, not bare :focus (review fix): a mouse click on the tabindex="0" span must
        // not leave the dim stuck with no visible indicator once the pointer moves away.
        var css = ReadStylesheet();
        Assert.Contains(".sunburst-panel:has(.sb-review-item:hover) .sb-seg:not(.sb-review)", css);
        Assert.Contains(".sunburst-panel:has(.sb-done-item:focus-visible) .sb-seg:not(.sb-done)", css);
        // The focusable legend entries keep the shared on-brand focus ring.
        Assert.Contains(".sunburst-legend .sb-legend-item:focus-visible", css);
    }

    [Fact]
    public void Stylesheet_HasDonutInteractiveLegendEmphasisRule()
    {
        // The donut half of the same affordance (review follow-up: Subtask 3.1 names "sunburst OR donut"
        // explicitly). Same :has()/:focus-visible grammar, scoped to .donut-and-legend/.donut-seg.
        var css = ReadStylesheet();
        Assert.Contains(".donut-and-legend:has(.dn-done-item:hover) .donut-seg:not(.done)", css);
        Assert.Contains(".donut-and-legend:has(.dn-deferred-item:focus-visible) .donut-seg:not(.deferred)", css);
        Assert.Contains(".donut-legend .dn-legend-item:focus-visible", css);
    }

    [Fact]
    public void Script_DoesNotImplementLegendEmphasis()
    {
        // The no-new-JS contract for insight surfaces: legend emphasis is pure CSS, so the one sanctioned
        // script must carry none of it — no legend/emphasis handler smuggled in. [Story 3.5 Task 5]
        var js = ReadScript();
        Assert.DoesNotContain("emphasize", js);
        Assert.DoesNotContain("sunburst-legend", js);
        Assert.DoesNotContain("sb-legend-item", js);
    }

    // ---- Story 3.8 seams (Git Insights hub tables + the sanctioned sort/filter enhancer) ----------

    [Fact]
    public void Stylesheet_HasGitInsightsTableStyles()
    {
        // Cheap guard so the hub's accessible-table + scroll-container styling can't be silently deleted:
        // wide tables scroll inside their own container (the body never scrolls horizontally), and the
        // sort/filter controls the enhancer creates have on-brand, focus-visible styling.
        var css = ReadStylesheet();
        Assert.Contains(".table-scroll { overflow-x: auto; contain: inline-size; }", css);
        Assert.Contains(".gi-table", css);
        Assert.Contains(".gi-sort-btn:focus-visible", css);
        Assert.Contains(".gi-filter", css);
        Assert.Contains(".gi-row-hidden { display: none; }", css);
    }

    [Fact]
    public void Stylesheet_HasGitInsightsMasterDetailStyles()
    {
        // The file→contributors drill-down is pure-CSS :target (no JS): guard the master-detail grid, the
        // whole-row stretched select link, and the :target reveal so the no-JS interaction can't regress.
        var css = ReadStylesheet();
        Assert.Contains(".gi-master-detail", css);
        Assert.Contains(".gi-row-link::after", css);
        Assert.Contains(".gi-contributors-panel:target", css);
        Assert.Contains(".gi-contributor-list", css);
    }

    [Fact]
    public void Script_HasTableSortFilterEnhancer()
    {
        // The Git Insights sort/filter enhancement lives in the ONE sanctioned script (no second file, no
        // CDN), announces sort state via aria-sort, and only ever targets opt-in js-sortable tables.
        var js = ReadScript();
        Assert.Contains("js-sortable", js);
        Assert.Contains("aria-sort", js);
        Assert.Contains("data-filter-label", js);
    }

    /// <summary>The body of the single <c>@media (prefers-reduced-motion: no-preference)</c> block.</summary>
    private static string NoPreferenceBlock(string css) => MediaBlock(css, "@media (prefers-reduced-motion: no-preference)");

    /// <summary>The body of the single <c>@media (prefers-reduced-motion: reduce)</c> block.</summary>
    private static string ReduceBlock(string css) => MediaBlock(css, "@media (prefers-reduced-motion: reduce)");

    /// <summary>Extracts a top-level @media block's body by brace-matching from its opening brace, so an
    /// assertion targets that block rather than incidentally matching a selector elsewhere in the file.</summary>
    private static string MediaBlock(string css, string header)
    {
        var start = css.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing block: {header}");
        var open = css.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}' && --depth == 0) return css[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces after {header}");
    }

    private static string ReadScript()
    {
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.js")
            ?? throw new InvalidOperationException("Embedded script 'SpecScribe.assets.specscribe.js' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Script_IsEmbeddedAlongsideStylesheet()
    {
        // The one sanctioned progressive-enhancement script must ship embedded the way the CSS does, so the
        // global-tool package stays self-contained. [Story 1.5 Task 3]
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe.js");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var js = reader.ReadToEnd();
        Assert.Contains("clipboard", js);
        Assert.Contains("ss-tooltip", js);
        // The send menu's click-away / Escape dismissal ships in the same sanctioned script.
        Assert.Contains("send-menu", js);
    }

    // ---- spec-scribes-nib-branding: brand mark + contrast pass ---------------------------------------------

    private static string ReadThemeBridge()
    {
        var asm = typeof(Charts).Assembly;
        using var stream = asm.GetManifestResourceStream("SpecScribe.assets.specscribe-webview-theme.css")
            ?? throw new InvalidOperationException("Embedded 'SpecScribe.assets.specscribe-webview-theme.css' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>WCAG 2.x relative-luminance contrast ratio for two <c>#rrggbb</c> values — computed, not
    /// eyeballed, so a future palette tweak that regresses the contrast pass goes red here rather than in a
    /// review screenshot.</summary>
    private static double ContrastRatio(string hexA, string hexB)
    {
        static double Channel(string hex, int index)
        {
            var c = Convert.ToInt32(hex.Substring(index, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        static double Lum(string hex)
        {
            var h = hex.TrimStart('#');
            return 0.2126 * Channel(h, 0) + 0.7152 * Channel(h, 2) + 0.0722 * Channel(h, 4);
        }
        var (l1, l2) = (Lum(hexA), Lum(hexB));
        if (l1 < l2) (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }

    private static string TokenValue(string css, string token)
    {
        // Comments are stripped first: this stylesheet's prose routinely quotes token/value pairs (including
        // this pass's own before/after notes), and Regex.Match would happily validate a dead value quoted in a
        // comment while the live definition regressed.
        var code = System.Text.RegularExpressions.Regex.Replace(
            css, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        var m = System.Text.RegularExpressions.Regex.Match(
            code, $@"{System.Text.RegularExpressions.Regex.Escape(token)}\s*:\s*(#[0-9a-fA-F]{{6}})\s*;");
        Assert.True(m.Success, $"{token} must be defined as a 6-digit hex literal");
        return m.Groups[1].Value;
    }

    [Fact]
    public void MutedInk_ClearsWcagAA_OnBothParchmentSurfaces()
    {
        // The owner's F5 finding ("light text on light backgrounds"): --ink-light measured 3.95:1 on
        // --parchment-dark. The deepened value must clear 4.5:1 (AA, normal text) on BOTH parchment surfaces.
        var css = ReadStylesheet();
        var inkLight = TokenValue(css, "--ink-light");
        Assert.True(ContrastRatio(inkLight, TokenValue(css, "--parchment")) >= 4.5,
            $"--ink-light {inkLight} vs --parchment must be >= 4.5:1");
        Assert.True(ContrastRatio(inkLight, TokenValue(css, "--parchment-dark")) >= 4.5,
            $"--ink-light {inkLight} vs --parchment-dark must be >= 4.5:1");
    }

    [Fact]
    public void FunnelConnector_IsItsOwnVisibleToken_OnSiteAndInTheBridge()
    {
        // The owner's F5 finding: the funnel's stage-linking band filled with --parchment-dark, which the
        // webview bridge remaps to the widget BACKGROUND — literally invisible on dark hosts. It must be its
        // own token, visibly distinct from the page background on the site (the old pairing was ~1.1:1), and
        // re-valued in the shared .vscode-* bridge block riding the host separator palette.
        var css = ReadStylesheet();
        // The RAW token is what ships: the rule must not reintroduce an opacity that re-blends the band toward
        // the surface below the floor asserted here.
        Assert.Contains(".funnel-connector { fill: var(--funnel-connector); }", css);
        var connector = TokenValue(css, "--funnel-connector");
        // Asserted against the surfaces the funnel ACTUALLY sits on: the chart panel (--warm-white) and the
        // page body (--cream) — not the parchment tokens, which host no funnel.
        Assert.True(ContrastRatio(connector, TokenValue(css, "--warm-white")) >= 1.8,
            $"--funnel-connector {connector} must be distinguishable (>=1.8:1) from the chart panel surface");
        Assert.True(ContrastRatio(connector, TokenValue(css, "--cream")) >= 1.8,
            $"--funnel-connector {connector} must be distinguishable (>=1.8:1) from the page body surface");

        // The bridge derives the band from the host FOREGROUND (never the ~1.5:1 border hairlines, which some
        // themes even define as transparent) so it is visible and hue-neutral in every theme family.
        var bridge = ReadThemeBridge();
        Assert.Contains("--funnel-connector: color-mix(in srgb, var(--vscode-foreground)", bridge);
    }

    [Fact]
    public void SiteNavMark_HasSizingRule_AndInheritsCurrentColor()
    {
        // The Scribe's Nib header mark: sized relative to the wordmark and colored ONLY via currentColor
        // (token-system rule; the brand span's color drives it on every surface, including whatever the
        // webview bridge re-values that color to). Anchored on the exact rule opener — MediaBlock is for
        // unique @media headers and would mis-anchor on a comment mention of the selector.
        var css = ReadStylesheet();
        var ruleStart = css.IndexOf(".site-nav-mark {", StringComparison.Ordinal);
        Assert.True(ruleStart >= 0, "stylesheet carries the .site-nav-mark rule");
        var rule = css[ruleStart..css.IndexOf('}', ruleStart)];
        Assert.Contains("fill: currentColor", rule);
        Assert.DoesNotContain("#", rule);
    }

    [Fact]
    public void StatusDeferred_IsFrozenLiteral_DecoupledFromMutedInk()
    {
        // Review finding: --status-deferred used to ALIAS --ink-light, so deepening the muted-text token would
        // silently re-tune the deferred ACCENT — exactly the owner's no-accent-retune constraint. It must stay
        // a frozen literal at the pre-pass value.
        Assert.Equal("#7a6250", TokenValue(ReadStylesheet(), "--status-deferred"));
    }
}
