namespace SpecScribe;

/// <summary>One sanctioned cross-surface divergence: a named semantic fact (<paramref name="FactId"/>) that a
/// specific surface (<paramref name="SurfaceId"/>) is ALLOWED to render differently from the shared view model,
/// with a documented <paramref name="Reason"/>. This is the ONLY legitimate way a surface may diverge (AC #2:
/// "differences are documented as host-specific exceptions only"). A divergence the parity harness finds that is
/// NOT registered here is a BUG, not an exception. Story 6.1 registers none — the HTML adapter reproduces every
/// fact; Story 6.2's webview registers here rather than drifting silently. [Story 6.1]</summary>
/// <param name="SurfaceId">The <see cref="IRenderAdapter.Id"/> the exception applies to (e.g. <c>webview</c>).</param>
/// <param name="FactId">The semantic-fact id (see <see cref="RenderParity"/>'s fact ids, e.g. <c>asset.css</c>).</param>
/// <param name="Reason">Why this surface legitimately diverges on this fact.</param>
public sealed record HostRenderException(string SurfaceId, string FactId, string Reason);

/// <summary>The single documented home for sanctioned cross-surface divergence — the registry AC #2's parity
/// checks consult. Empty through Stories 6.1/6.2 (the HTML adapter drops/reinterprets nothing); Story 6.4's
/// webview surface registers its three host-specific exceptions here — all CHROME/ASSET facts forced by the
/// webview platform's Content-Security-Policy, never a section/content fact (the body facts hold full parity).
/// [Story 6.1; entries Story 6.4]</summary>
public static class HostRenderExceptions
{
    /// <summary>The sanctioned divergences. Exactly the three ADR 0005 measured for the webview surface; every
    /// entry names its surface and carries a reviewable reason. An unregistered divergence is a bug.</summary>
    public static readonly IReadOnlyList<HostRenderException> Registry = new[]
    {
        new HostRenderException("webview", "asset.css",
            "The webview inlines the production stylesheet into its <style> block (no <link rel=\"stylesheet\"> "
            + "is emitted): under the webview CSP local resources only load via asWebviewUri, and ADR 0005 "
            + "ratified inlining so the shim ships no loose asset files. Same bytes of CSS, different carrier."),
        new HostRenderException("webview", "asset.js",
            "The specscribe.js enhancement script is deliberately absent: it is convenience-only by the "
            + "progressive-enhancement policy (rendering-architecture.md), and ADR 0005 measured that the body "
            + "reaches the same information without it. The webview's only script is its own nonce'd bridge."),
        new HostRenderException("webview", "mermaid",
            "No Mermaid script can load under the webview CSP (script-src is nonce-locked, remote loads are "
            + "blocked), so any <pre class=\"mermaid\"> — the epics roadmap AND, since the whole-site captured "
            + "surfaces (spec-webview-doc-page-surfaces), any doc/ADR page carrying a diagram — degrades to "
            + "readable preformatted text — ADR 0005's accepted fallback. The captured surfaces also drop any "
            + "in-page script the same way (innerHTML swaps never execute scripts; the sliced region is "
            + "script-free by the same policy as asset.js). Bundling Mermaid with a nonce remains an option."),
        // The SPA surface (Story 6.7) is a REAL browser, so — unlike the webview — it keeps the production
        // specscribe.css and specscribe.js: those chrome/asset facts MATCH the html surface (the shared entry shell
        // loads them), which is why the SPA registers NO asset.css / asset.js exception. Its ONE sanctioned
        // divergence is Mermaid: the epics roadmap's <pre class="mermaid"> is initialized by an inline
        // `mermaid.initialize` the static page carries after its footer, but the SPA swaps content regions via
        // innerHTML (an injected <script> never executes) and does not re-run a Mermaid pass across swaps, so the
        // served page string carries no `mermaid.initialize` and the roadmap degrades to readable preformatted text
        // — the same accepted fallback as the webview. Full Mermaid-in-SPA (re-init across swaps) is a deferred
        // enhancement; the diagram source is present and readable meanwhile (progressive enhancement / NFR6).
        new HostRenderException("webview", "data-island",
            "Inline <script type=\"application/json\"> data islands (the Hierarchy Explorer payload, and any later "
            + "one) are stripped from the webview content region. NOTE the reason is NOT CSP: a JSON data block is "
            + "never executed, so script-src does not apply to it and the CSP would not have blocked it. The island "
            + "is dropped because it is DEAD WEIGHT here — the webview deliberately ships no specscribe.js (see the "
            + "asset.js exception above), so nothing on this surface can ever read it, and it is pure payload in a "
            + "document the reader inlines. AMENDED BY STORY 20.7: this reason used to end \"the static SVG chart "
            + "and its Story 9.13 links are untouched, so no information is lost\". That is now FALSE — 20.7 retired "
            + "the SVG. What carries the information on this surface is the text twin, which the strip leaves in "
            + "place (it is <details>/<div> markup, not a <script>). Still an asset-weight divergence, not a "
            + "content one — see the hierarchy-chart exception below for the picture."),
        new HostRenderException("webview", "hierarchy-chart",
            "The webview presents the Hierarchy Explorer's TEXT TWIN and no chart picture. It ships no "
            + "specscribe.js at all (see asset.js) and therefore no Plotly, so nothing can mount a chart here; the "
            + "server-rendered SVG that used to stand in its place was retired by Story 20.7. This is the fallback "
            + "ADR 0012 §5 and ADR 0013 §7 BOTH pre-authorize, not an unplanned gap: the twin is complete, "
            + "navigable and non-colour, so NFR-5 as amended by ADR 0013 holds — JS-off may lose the "
            + "visualization, never the information or the navigation. It is a SEQUENCING choice rather than a "
            + "technical limit: the Story 20.4 spike proved Plotly renders under the byte-verbatim shipped policy, "
            + "and the ADR 0005 CSP amendment that would let it load here lands ONCE, with Story 23.4 (ADR 0012 §5) "
            + "— deliberately not twice. Owner decision D3, Story 20.7. "
            + "EXTENDED BY STORY 20.9 to the two colorize-driven surfaces it converted, and one of them needs its "
            + "own sentence: on git-insights.html the fallback is exactly as described above, the component's own "
            + "text twin. On code-map.html the component emits NO twin (HierarchyTwinDisplay.External) because the "
            + "surface already ships a richer one — its per-variant file table, which Story 20.6 D1 audited and "
            + "kept, carrying every file's path, line count, type and six git metrics as real table cells with "
            + "every path linked. So the picture is absent on both, the INFORMATION is present on both, and the "
            + "thing carrying it differs by surface. Saying \"the Hierarchy Explorer's text twin\" alone would "
            + "have been wrong for the Code Map."),
        new HostRenderException("spa", "mermaid",
            "The SPA swaps content regions via innerHTML, where an injected Mermaid init script never executes and "
            + "is not re-run across swaps, so the epics roadmap's <pre class=\"mermaid\"> degrades to readable "
            + "preformatted text — the same accepted fallback as the webview. Unlike the webview, the SPA keeps "
            + "specscribe.css/specscribe.js (real browser), so it registers no asset.css/asset.js exception. Full "
            + "Mermaid-in-SPA re-init is a deferred enhancement (Story 6.7 Completion Notes)."),
    };
}
