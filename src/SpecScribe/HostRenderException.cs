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
    /// began supplying the chart engine and mount code. Story 23.6 then removed the webview's remaining three
    /// (<c>asset.css</c>, <c>asset.js</c>, <c>mermaid</c>): each registered a difference against the C#-rendered
    /// PAGE, and no C# code path renders one any more — see the note in <see cref="Registry"/>. What survives is
    /// the SPA's single <c>mermaid</c> entry.</para></summary>
    public static readonly IReadOnlyList<HostRenderException> Registry = new HostRenderException[]
    {
        // ── RETIRED BY STORY 23.6 (AC #1/#2): the webview's `asset.css`, `asset.js` and `mermaid` entries ──
        //
        // This registry's own contract is that "an unregistered divergence is a bug, and so is a REGISTERED one
        // that no longer exists". All three of these registered a difference against the C#-rendered PAGE, and
        // C# no longer renders one.
        //
        //   · `asset.css` / `asset.js` described a CARRIER difference: the golden page emitted
        //     `<link rel="stylesheet">` and `<script src=… defer>`, the webview inlined both under its CSP. The
        //     golden side of that comparison is gone — the renderer builds the head from the IR — and the parity
        //     facts those entries excepted (`SemanticFacts.Stylesheet` / `.Script`) were removed with it, because
        //     no C# surface evidences a head tag any more.
        //   · `mermaid` described the webview shipping no `mermaid.initialize`. Neither does anything else in C#:
        //     `Mermaid.InitScript()` was emitted only by the deleted `HtmlRenderAdapter.Render`, and the init is
        //     the renderer's now (`chromeNeeds().needsMermaid` → `IrSurface.vue`). `MermaidPresent` SURVIVES as a
        //     region-level fact — "this page carries a diagram", so a surface that DROPS the block is still
        //     caught — but every C# surface emits the same region, so it cannot diverge and the exception has
        //     nothing left to except.
        //
        // ⚠️ NONE OF THIS MEANS THE WEBVIEW GAINED MERMAID. It still cannot run it under its CSP. What changed is
        // that the difference is no longer expressible as a C#-side PARITY fact: ADR 0024 makes every surface a
        // filtered projection of one region, so the surfaces agree by construction and the only remaining
        // differences live in chrome — which belongs to the renderer, not to this harness. The capability gap is
        // recorded in ADR 0005 and ADR 0032, which is where it always belonged.
        //
        // Deleted rather than reworded, following the precedent set two entries down by ADR 0036: a registry that
        // keeps retired entries stops being readable as "what differs today".

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
        // The SPA's `mermaid` entry is retired for the SAME reason as the webview's three above, and its
        // absence completes the pattern rather than being a separate judgement. It registered that an injected
        // init script never executes across an innerHTML swap, so the SERVED PAGE carried no `mermaid.initialize`
        // where the static page did. There is no static page, and no C# surface emits an init at all.
        //
        // ⚠️ THE REGISTRY IS NOW EMPTY, AND THAT IS THE HONEST STATE — not an oversight to be filled back in.
        // ADR 0024 makes every C# surface a filtered projection of ONE composed region, so the surfaces cannot
        // disagree on a region fact by construction, and every difference that remains between them lives in
        // chrome — which Story 23.6 moved to the renderer. An empty registry says exactly that: nothing C#
        // produces diverges today.
        //
        // The type and this list stay. `FindSectionDivergences` shares the mechanism, and the next real
        // divergence should be registered here rather than rediscovering the need for a registry.
    };
}
