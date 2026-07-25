namespace SpecScribe;

/// <summary>The shared assets a page needs, as host-neutral data — the AD-2 "asset manifest" a delivery adapter
/// wires into its host's head. It NAMES the stylesheet and script hrefs (output-relative, already carrying the
/// caller's <c>../</c> prefix, WITHOUT the build cache-bust token — <see cref="PathUtil.RenderHeadOpen"/> still
/// owns appending <c>?v=</c>) and flags whether the page carries a mermaid diagram (so the client init module is
/// injected only when one landed). It deliberately models WHICH assets, not HOW a host themes them — host-aware
/// theming / VS Code chrome variables are Story 6.5 (AD-7), delivered webview-side as a separate inline theme
/// layer (see <see cref="WebviewRenderAdapter"/>), not through this manifest. [Story 6.1]</summary>
public sealed record AssetManifest
{
    /// <summary>The output-relative stylesheet href (prefix applied, no <c>?v=</c>).</summary>
    public required string StylesheetHref { get; init; }

    /// <summary>The output-relative enhancement-script href (prefix applied, no <c>?v=</c>).</summary>
    public required string ScriptHref { get; init; }

    /// <summary>Whether this page carries at least one mermaid diagram block and therefore needs the client-side
    /// init module. The producer computes it from the rendered body (<see cref="Mermaid.ContainsBlock"/>), so
    /// the flag can never disagree with what the page actually contains.</summary>
    public required bool MermaidNeeded { get; init; }

    /// <summary>Whether this page carries at least one Hierarchy Explorer (Story 20.5) and therefore needs the
    /// vendored plotly.js bundle. Computed by the producer from the RENDERED BODY
    /// (<see cref="HierarchyExplorer.ContainsHost"/>), exactly like <see cref="MermaidNeeded"/> — so the flag can
    /// never disagree with what the page actually contains, which is the failure mode a hand-set boolean invites.
    /// <para>Optional with a <c>false</c> default rather than <c>required</c>: every page that does not host a
    /// hierarchy chart must stay byte-identical, and 1.2 MB is not a rounding error.</para></summary>
    public bool HierarchyEngineNeeded { get; init; }
}
