# ADR 0032: The CSP Posture After the Projection Layer — Amending ADR 0005 §4 Once

**Status:** Proposed (authored 2026-07-29 by Story 23.4; ratification is the owner's)
**Date:** 2026-07-29
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0005](0005-vs-code-webview-runtime-and-packaging.md) §4 (the webview CSP clauses)
**Relates to:** [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §Decision 5 (which owed this amendment jointly); [ADR 0013](0013-text-twin-is-the-no-js-contract.md) (the no-JS contract the tight policy protects); [ADR 0010](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) (the decision that introduced client JS at all); [ADR 0024](0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) (the region seam the webview still consumes); Story 23.1's spike report §Axis 3 (the CSP matrix), Story 23.3 (`noScripts: true`), Story 23.4 (this amendment's owner)

## Context

Two separate pieces of work each owed an amendment to ADR 0005 §4, and ADR 0012 §Decision 5 explicitly required them to **land once, not twice**:

1. **ADR 0010/0012's client-side charting.** ADR 0005 §4 asserts "**the body carries no scripts of its own** — the only `<script>` in each document is the shim's own nonce'd bridge," and treats that as what makes `script-src 'nonce-…'` (no `'unsafe-inline'`) sufficient. The portal has since vendored a ~1.2 MB `plotly-hierarchy.min.js` and ships `specscribe.js`, so the sentence as written no longer describes the artefact.
2. **Story 23.1's Nuxt finding.** The spike measured that Nuxt **hydration** does not survive the webview CSP, and that closing the gap needed **two knobs together** — `'strict-dynamic'` **plus** `experimental.payloadExtraction: false`. Half-applying it was catastrophic rather than merely incomplete: the page blanked, 148 SVGs → **0**.

The seeded expectation was therefore that this story would relax the policy string. **It does not**, and the reason is a change of fact rather than a change of mind.

## What was re-measured (2026-07-29, at the whole-site scale)

Every number below is from a full `--deep-git --spa` generate plus `npm run generate`, over **1,469 IR pages** — not a sample.

| claim | measured |
| --- | --- |
| Nuxt runtime `<script>` tags on IR routes | **0** (matched against real `<script>` TAGS, never a substring — several `code/**` pages render source that *mentions* `__NUXT__`) |
| `_payload.json` files alongside an IR route | **0** (5 exist site-wide, all on the non-IR `measure/*` + component-library demo routes Story 23.2 owns) |
| executable `<script>` inside an IR content region | **0** |
| inert `<script type="application/json">` data islands inside regions | **163** (this is what the Hierarchy Explorer reads) |

**Story 23.3's `routeRules: { '/**': { noScripts: true } }` removed Story 23.1's premise entirely.** There is no hydration on an IR route, so there is nothing for `'strict-dynamic'` to permit. Finding (2) is not "fixed"; it is **inapplicable**.

**And the webview never sees Nuxt output anyway.** Story 23.4 AC #3 and ADR 0024 keep the webview and the SPA on the C# region seam. A Nuxt-specific CSP relaxation would have been a policy loosening for a consumer that does not exist.

## Decision

**1. No relaxation of the policy string. `script-src` stays nonce-locked with no `'unsafe-inline'` and no `'strict-dynamic'`; `experimental.payloadExtraction` stays untouched.** This is the *stronger* outcome, not a missing one — and it is now measured at whole-site scale rather than inferred from a spike shell.

**2. ADR 0005 §4's "the body carries no scripts of its own" clause is restated as a claim about the REGION, and strengthened from an observation into an enforced invariant.** The original wording was a statement about what the renderer happened to emit. The accurate and more useful claim is:

> The IR **content region** carries no executable script. It may carry inert `<script type="application/json">` data islands, which are DOM data rather than code. Every script the portal needs — the nav toggle, `specscribe.js`, `plotly-hierarchy.min.js`, the Mermaid init, the anti-flash boot marker — is **chrome-level**, emitted outside the region by `HtmlRenderAdapter.Render`, and therefore replaced by whichever shell consumes the region.

This is enforced in three places rather than trusted: `HierarchyExplorer`'s boot marker is deliberately emitted outside the captured region; `WebviewRenderAdapter` strips every JSON island; and `IrSurface.vue` **throws at build time** on a region carrying an executable island rather than shipping a page that would be silently inert (`v-html` never executes injected `<script>`).

**3. The vendored charting bundle does not contradict the tight policy, because it is chrome, not body.** ADR 0012's own addendum measured this and found "no relaxation of the policy string is required"; this amendment records the same result at the projection layer. A nonce'd or shell-supplied `<script src>` in the head is exactly what `script-src 'nonce-…'` is for. What *would* contradict ADR 0005 is a script inside the region — which is why (2) is written as an invariant with a build-time gate.

**4. The spike's stated boundary is carried forward, not quietly widened.** Story 23.1's CSP verdict was for the **policy string** under **header** delivery over an **HTTP-served** asset graph. It is *not* a claim about `<meta>` delivery, `vscode-resource:` URIs, or an Electron paint (23-1-spike-report.md:239–245, :482). "Two lines wide" was a **lower bound**. Nothing here extends it.

## Consequences

**Good.**

- The security-critical lock stays strict, and the claim behind it is now provable by a harness instead of by reading the renderer.
- The "land it once" requirement in ADR 0012 §Decision 5 is discharged by this single ADR.
- The invariant in Decision 2 is the one a future story can actually violate, and it now fails a build rather than shipping a dead page.

**Bad, and accepted.**

- **Mermaid remains the one CSP casualty**, unchanged since ADR 0005: `<pre class="mermaid">` degrades to readable preformatted text under a nonce-locked policy. Still the accepted fallback; still not solved here.
- **The "one renderer" claim is true of the SITE, not yet of the PRODUCT.** After Story 23.4, Nuxt writes every `.html` the site ships, but the webview and SPA still consume the C# region seam (ADR 0024). Saying this out loud is deliberate: leaving it implied is how a reader concludes the webview is a Nuxt consumer and reasons about its CSP from the wrong premise. **Open question (Story 23.4 Q2, unresolved):** does the webview eventually consume the Nuxt output, or stay on the region path permanently? If it ever moves, Story 23.1's `'strict-dynamic'` finding becomes live again and this ADR must be revisited — the finding is dormant, not dead.

**Neutral.**

- No code change ships with this ADR. That is the finding, and a documentation-only amendment for a security posture is a legitimate outcome — the alternative would have been loosening a policy to satisfy a consumer that does not exist.

## Alternatives considered

**Apply `'strict-dynamic'` + `payloadExtraction: false` as seeded.** Rejected on measurement: there is no hydration on any IR route to enable, and the webview is not a Nuxt consumer. It would loosen `script-src` for no beneficiary — and Story 23.1 showed the half-applied form blanks the page, so carrying an unnecessary two-knob edit is pure downside risk.

**Fold this into [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md).** Rejected: Story 23.5 was explicit that ADR 0022 deliberately does not touch CSP, and ADR 0012 §Decision 5 asked for one CSP amendment — not for CSP to be a footnote in a packaging decision.

**Leave ADR 0005 §4 as-is, since no policy string changes.** Rejected: the "body carries no scripts of its own" sentence is now literally false of the shipped page, and a reader checking the CSP against the artefact would find a contradiction and have to guess which side is stale. An amendment that only corrects a claim is still worth landing.

**Write the amendment as two ADRs (one for charting, one for the projection).** Rejected by ADR 0012 §Decision 5's "landed once, not twice" — two records of one posture is how they drift.

## Re-verification, and a ratification request (Story 17.2, 2026-08-08, baseline `e8a689d`)

Story 17.2 AC #1 requires the webview CSP/nonce posture to be **verified**. It was re-measured at this
baseline rather than taken on this record's word, on both sides of the seam, matching real `<script>` **tags**
and never a substring (this portal renders its own source, so `application/json` and `__NUXT__` appear as
prose on real pages):

| | IR side | rendered site |
| --- | --- | --- |
| units scanned | 1,268 region strings | 1,262 pages |
| **executable `<script>` in-region** | **0** | **0** (inside `<main id="main-content">`) |
| inert `type="application/json"` islands | 348 | 343 |
| pages flagged `hasExecutableIsland` | **0** | — |

The island COUNT has moved since this ADR was written (163 over 1,469 pages on 2026-07-29 → 348 over 1,268
today); the site changed underneath it. **The invariant this ADR actually asserts — zero executable script in
the region — is unchanged.** The policy string at `WebviewRenderAdapter.cs` is byte-identical to the one
recorded above, and `WebviewRenderAdapterTests` already asserts its three security-critical clauses
(`script-src 'nonce-…'`, `style-src 'unsafe-inline'`, and the absence of `script-src 'unsafe-inline'`).

**Ratification is requested.** AC #1 asks for a *verified* posture, and verifying against a record that is
itself unratified is half a job. Nothing in this re-measurement contradicts the ADR; the status is left
`Proposed` because flipping it is the owner's call, not this story's.

**One thing this ADR did NOT cover, now recorded elsewhere.** Its scope is the webview. The generated static
site has no CSP at all, which Story 17.2 measured as a live stored-XSS vector — see
[ADR 0042](0042-raw-html-in-the-repositorys-own-markdown-is-neutralized.md) (the source-level fix, shipped)
and [ADR 0043](0043-the-generated-static-site-carries-no-csp.md) (the CSP question, deliberately referred to
the owner). The script-island invariant above is exactly the thing that did **not** catch it: it keys on
script islands only and never looks at event-handler attributes.
