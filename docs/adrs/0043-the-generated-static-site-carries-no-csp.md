# ADR 0043: The Generated Static Site's CSP — Measured, Costed, and Deliberately Not Shipped Unilaterally

**Status:** Proposed (authored 2026-08-08 by Story 17.2; **the decision itself is the owner's** — see §Decision)
**Date:** 2026-08-08
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0042](0042-raw-html-in-the-repositorys-own-markdown-is-neutralized.md) (the primary control, shipped by the same story); [ADR 0032](0032-csp-posture-after-the-projection-layer.md) (the webview policy, re-verified unchanged — and the precedent that a half-applied CSP is catastrophic rather than merely incomplete); [ADR 0033](0033-content-drift-gates-are-targeted-and-regenerable.md) (governs any new gate, which a hash-based policy would require); [ADR 0013](0013-text-twin-is-the-no-js-contract.md); [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)

## Context

Story 17.2 AC #1 asks that "the webview CSP/nonce posture is verified." It is (ADR 0032, re-verified at `e8a689d` — the policy string in `WebviewRenderAdapter.cs:63-64` is unchanged). The gap that verification exposes is the **other** surface: **the generated static site has no CSP at all.** `PathUtil.RenderHeadOpen` emits `charset`, `viewport`, `description` and `og:*` and nothing else; post-Epic 23 the head is emitted by Nuxt's `useHead` in `IrSurface.vue`, which adds no policy either. A grep for `Content-Security-Policy` across `web/` returns only `node_modules` noise.

This matters concretely because this repository publishes its own portal to GitHub Pages (`publish-docs-live-pages.yml`), so the origin is real and shared.

## What was measured (2026-08-08, baseline `e8a689d`, full generate: 1,262 HTML pages)

A CSP is only as good as its fit with what the site actually emits. Measured across every generated page, not sampled:

| fact | count | consequence for a policy |
| --- | --- | --- |
| pages | 1,262 | — |
| **inline `<script>` blocks** | **1,054**, on 531 pages | `script-src` cannot be `'self'` alone |
| of those, `type="module"` | 14 | the Mermaid init |
| external scripts | `specscribe.js` ×1262, `plotly-hierarchy.min.js` ×342, `prism.js` ×285, `specscribe-spa.js` ×1 | all same-origin — `'self'` covers them |
| **inline `style=""` attributes** | **2,105** | `style-src` needs `'unsafe-inline'` — attribute styles cannot be hashed |
| `<style>` blocks | 0 | — |

### Three findings that decide the shape

**1. A nonce is worthless here, and this is not a matter of effort.** Nonces work because the value is unpredictable *per response*. These are static files served from disk or GitHub Pages with no server in the request path, so any nonce is a constant baked into the published HTML — and an attacker who can inject markup into the page can read that constant out of the very file they are attacking and put it on their own `<script>` tag. A static nonce is decoration. **Hashes, or `'unsafe-inline'`, are the only real options.**

**2. `'unsafe-inline'` would make the policy nearly pointless for the threat it is meant to cover.** ADR 0042's measured vector was `<img onerror>` — an inline handler. `script-src 'unsafe-inline'` permits inline event handlers. A CSP that allows them does not defend against the thing this story found.

**3. Hashes are tractable — 1,054 inline scripts, but only a handful of distinct ones.** They come from a small fixed set of templates (the hierarchy anti-flash boot marker, the graph boot marker, the TOC active-section tracker, the Mermaid init). So the policy needs roughly 4–6 `'sha256-…'` values, not a thousand. **But it needs them to be exactly right, and to be regenerated whenever any boot script changes** — which is a new content-drift gate, and ADR 0033 requires any new gate to localize failure to a named artifact, be scoped so a sibling story cannot turn it red, and be proven deterministic across machines and CI operating systems before pinning.

**4. The Mermaid init imports from a CDN.** Those 14 `type="module"` blocks `import` mermaid from an external origin, so a real policy must name that origin in `script-src` — and a `default-src 'none'` policy would silently kill every Mermaid diagram on the site. This is also recorded against NFR3 by Story 17.2 Task 5: it is a **viewer-side** outbound request from the rendered page, not an outbound call by the tool, but the distinction should be stated rather than discovered.

**5. `<meta http-equiv>` cannot carry the whole policy.** `frame-ancestors` and `report-uri` are ignored in a meta-delivered CSP. Since a static site has no response headers under SpecScribe's control, a meta-delivered policy is structurally partial — the clickjacking clause in particular cannot be delivered at all by the generator, only by whatever host serves the output.

## Decision

**The static site ships NO CSP in this story, and the decision to add one is referred to the owner with a concrete recommendation.** Three reasons, in order of weight:

1. **ADR 0042 already closes the measured hole at the source.** Handlers, `javascript:` URLs and embedding elements are removed before they reach the IR, so a CSP here is defense-in-depth over a channel that is now shut — not the primary control. Shipping it under time pressure buys less than it appears to.
2. **ADR 0032's precedent is explicit that a half-applied CSP is worse than none.** Story 23.1 measured a partial policy blanking the page: 148 SVGs → **0**. A wrong hash set on 531 pages breaks every chart, every diagram, and the TOC tracker — silently, in the viewer's browser, on a surface the test suite structurally cannot see (CLAUDE.md § Verification).
3. **It requires a new drift gate, and ADR 0033 governs those.** A hash-pinning gate must be designed to ADR 0033's four constraints, not bolted on at the end of a hardening story.

### Recommended policy, for whenever this is taken up

Recorded now so the next attempt starts from measurement rather than re-deriving it:

```
default-src 'self';
base-uri 'none';
object-src 'none';
script-src 'self' 'sha256-…'×N <mermaid-cdn-origin>;
style-src 'self' 'unsafe-inline';
img-src 'self' data:;
font-src 'self' data:;
form-action 'none';
frame-src 'none';
```

`style-src 'unsafe-inline'` is unavoidable (2,105 attribute styles) and is **the same concession ADR 0032 already accepts for the webview**, so it is a parity choice rather than a new weakening. `frame-ancestors` is deliberately absent — it cannot be delivered by meta and belongs to the host.

## Consequences

**The honest statement of posture, which should not be softened:** the generated static site has no CSP today, and after this story it still has none. What changed is that the injection channel a CSP would have mitigated is closed at its source, and the residual risk is now second-order (a bug in `HtmlSafety`, or a future emitter that introduces a new raw-HTML path) rather than the direct, reproduced, stored-XSS vector Story 17.2 measured.

**A future emitter change can silently reopen the second-order risk.** Nothing enforces that new raw-HTML paths route through `HtmlSafety`. That is a real gap and it is named here rather than left implicit; it is the strongest argument for eventually shipping the CSP.

**If the owner rejects this and wants the CSP now**, the work is: derive the N hashes at build time, add them to the Nuxt head, add an ADR 0033-compliant gate that fails when a boot script changes without its hash, and verify in a live browser that charts, Mermaid, Prism and the TOC tracker all still work — the suite cannot see any of those.
