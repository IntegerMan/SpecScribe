using System.Reflection;
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
    }

    [Fact]
    public void Stylesheet_HasOnBrandTooltipStyles()
    {
        // CSS-only tooltip for HTML elements + the JS-positioned node for SVG segments. [Story 1.5 C1/C2]
        var css = ReadStylesheet();
        Assert.Contains("[data-tooltip]", css);
        Assert.Contains("::after", css);
        Assert.Contains(".ss-tooltip", css);
    }

    [Fact]
    public void Stylesheet_HasCopyButtonStyles()
        => Assert.Contains(".cmd-copy", ReadStylesheet());

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
    public void Stylesheet_HasStructureTreeStyles()
    {
        // Cheap guard so the Story 3.4 structure-tree seam can't be silently deleted in a later refactor.
        var css = ReadStylesheet();
        Assert.Contains(".structure-tree", css);
        Assert.Contains(".tree-branch", css);
    }

    [Fact]
    public void Stylesheet_FunnelStagesRouteThroughStatusTokens()
    {
        // Every funnel stage color resolves to a --status-* token (single stage→color source) — the token
        // routing can't silently regress to hardcoded hex. [Story 3.6, Story 1.5 B2/B3]
        var css = ReadStylesheet();
        Assert.Contains(".funnel-band.funnel-epics { fill: var(--status-drafted); }", css);
        Assert.Contains(".funnel-band.funnel-stories { fill: var(--status-ready); }", css);
        Assert.Contains(".funnel-band.funnel-planned { fill: var(--status-active); }", css);
        Assert.Contains(".funnel-band.funnel-tasks { fill: var(--status-done); }", css);
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
        Assert.Contains(".structure-tree", block);
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
        var css = ReadStylesheet();
        Assert.Contains(".sunburst-panel:has(.sb-review-item:hover) .sb-seg:not(.sb-review)", css);
        Assert.Contains(".sunburst-panel:has(.sb-done-item:focus) .sb-seg:not(.sb-done)", css);
        // The focusable legend entries keep the shared on-brand focus ring.
        Assert.Contains(".sunburst-legend .sb-legend-item:focus-visible", css);
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
}
