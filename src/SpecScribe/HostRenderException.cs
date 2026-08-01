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
    /// <summary>The sanctioned divergences — every entry names its surface and carries a reviewable reason. An
    /// unregistered divergence is a bug, and so is a registered one that no longer exists.
    /// <para>ADR 0036 removed two webview entries (<c>data-island</c>, <c>hierarchy-chart</c>) when the shell
    /// began supplying the chart engine and mount code, and NARROWED <c>asset.js</c> from "absent" to "inlined
    /// rather than linked". The three that remain — <c>asset.css</c>, <c>asset.js</c>, <c>mermaid</c> — are now
    /// two carrier differences and one CSP casualty; none is a missing capability.</para></summary>
    public static readonly IReadOnlyList<HostRenderException> Registry = new[]
    {
        new HostRenderException("webview", "asset.css",
            "The webview inlines the production stylesheet into its <style> block (no <link rel=\"stylesheet\"> "
            + "is emitted): under the webview CSP local resources only load via asWebviewUri, and ADR 0005 "
            + "ratified inlining so the shim ships no loose asset files. Same bytes of CSS, different carrier."),
        new HostRenderException("webview", "asset.js",
            "NARROWED BY ADR 0036, and the narrowing matters: this used to read \"the specscribe.js enhancement "
            + "script is deliberately absent\". It is no longer absent. WrapDocument inlines the production "
            + "specscribe.js — the SAME file, whole and unforked (ADR 0036 §2) — together with the vendored chart "
            + "engine, both under the document nonce, which is how charts mount here at all. What survives as a "
            + "divergence is purely the CARRIER, exactly like asset.css above: the parity fact is a "
            + "<script src=\"...\" defer> tag, and this surface has no such tag because `localResourceRoots` is "
            + "empty and nothing may load from disk. Same bytes of JavaScript, different delivery. The webview is "
            + "NOT missing behaviour here any more, and reading this entry as though it were would be the mistake "
            + "it previously described."),
        new HostRenderException("webview", "mermaid",
            "No Mermaid script can load under the webview CSP (script-src is nonce-locked, remote loads are "
            + "blocked), so any <pre class=\"mermaid\"> — the epics roadmap AND, since the whole-site captured "
            + "surfaces (spec-webview-doc-page-surfaces), any doc/ADR page carrying a diagram — degrades to "
            + "readable preformatted text — ADR 0005's accepted fallback. Unchanged by ADR 0036: that decision put "
            + "the CHART engine and specscribe.js on the shell, and neither carries Mermaid, so this stays the one "
            + "CSP casualty ADR 0032 also names. The content region remains free of EXECUTABLE script either way "
            + "(an innerHTML swap would never run one). Bundling Mermaid with a nonce remains an option, and is now "
            + "a smaller step than it was — the shell already proves a nonce'd vendored bundle works here."),
        // The SPA surface (Story 6.7) is a REAL browser, so — unlike the webview — it keeps the production
        // specscribe.css and specscribe.js: those chrome/asset facts MATCH the html surface (the shared entry shell
        // loads them), which is why the SPA registers NO asset.css / asset.js exception. Its ONE sanctioned
        // divergence is Mermaid: the epics roadmap's <pre class="mermaid"> is initialized by an inline
        // `mermaid.initialize` the static page carries after its footer, but the SPA swaps content regions via
        // innerHTML (an injected <script> never executes) and does not re-run a Mermaid pass across swaps, so the
        // served page string carries no `mermaid.initialize` and the roadmap degrades to readable preformatted text
        // — the same accepted fallback as the webview. Full Mermaid-in-SPA (re-init across swaps) is a deferred
        // enhancement; the diagram source is present and readable meanwhile (progressive enhancement / NFR6).
        // RETIRED BY ADR 0036: the `data-island` and `hierarchy-chart` webview exceptions.
        //
        // `data-island` registered that inline <script type="application/json"> payloads were stripped from the
        // webview region. They are not any more — the region ships verbatim. The strip's stated reason was never
        // CSP (a JSON block is inert, and ADR 0032 §2 explicitly PERMITS islands in a region); it was DEAD WEIGHT,
        // because the surface shipped nothing that could read one. Now that the shell supplies the engine and the
        // mount code, the island is live data and the divergence is gone.
        //
        // `hierarchy-chart` registered that the webview presented the text twin and no chart picture. It described
        // its own cause honestly as "a SEQUENCING choice rather than a technical limit", noting the Story 20.4
        // spike had already proved Plotly renders under the byte-verbatim shipped policy. ADR 0036 discharges that
        // sequencing: charts now mount here, so there is no missing picture to register. ADR 0013 §7's twin
        // fallback is not deleted — it reverts to being the FAILURE path (the twin is revealed on
        // [data-hierarchy-failed]) rather than the normal presentation.
        //
        // Both entries are DELETED rather than reworded: there is no residual divergence left to describe, and a
        // registry that keeps retired entries for history stops being readable as "what differs today".
        new HostRenderException("spa", "mermaid",
            "The SPA swaps content regions via innerHTML, where an injected Mermaid init script never executes and "
            + "is not re-run across swaps, so the epics roadmap's <pre class=\"mermaid\"> degrades to readable "
            + "preformatted text — the same accepted fallback as the webview. Unlike the webview, the SPA keeps "
            + "specscribe.css/specscribe.js (real browser), so it registers no asset.css/asset.js exception. Full "
            + "Mermaid-in-SPA re-init is a deferred enhancement (Story 6.7 Completion Notes)."),
    };
}
