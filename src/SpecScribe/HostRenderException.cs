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
        new HostRenderException("spa", "mermaid",
            "The SPA swaps content regions via innerHTML, where an injected Mermaid init script never executes and "
            + "is not re-run across swaps, so the epics roadmap's <pre class=\"mermaid\"> degrades to readable "
            + "preformatted text — the same accepted fallback as the webview. Unlike the webview, the SPA keeps "
            + "specscribe.css/specscribe.js (real browser), so it registers no asset.css/asset.js exception. Full "
            + "Mermaid-in-SPA re-init is a deferred enhancement (Story 6.7 Completion Notes)."),
    };
}
