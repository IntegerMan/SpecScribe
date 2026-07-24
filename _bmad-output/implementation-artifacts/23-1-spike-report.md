# Story 23.1 Spike Report — Nuxt-over-IR Feasibility

**Status:** Complete · **Date:** 2026-07-23 · **Branch:** `spike/nuxt-ir-23-1` · **Probe:** [`spike/nuxt-ir/`](../../spike/nuxt-ir/README.md)

This is the durable deliverable of Story 23.1 (the throwaway probe code is deletable with the branch). It mirrors
Story 22.1's report structure. **It does NOT author a new ADR:**
[ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) already decided the direction (Vue +
Nuxt 3, universal/SSR, charts stay C#-SVG in the IR); this spike de-risks the *build* of that decision with real
numbers, and **gates** whether Stories 23.2–23.5 proceed as scoped or are re-scoped.

**Nothing found here contradicts ADR 0009.** No amendment is proposed. Two findings change the *shape* of the
implementation stories, and one is a defect in already-shipped code.

---

## Context

ADR 0009 (Accepted, 2026-07-20) ratified the direction but explicitly **did not schedule the build** — it seated
Epic 23 as backlog and ruled the epic must "open with a spike" proving the four integration risks it could not
settle by argument [§Disposition #2, §"Remaining spike-owned unknowns"]:

1. **NFR6 survives** — does `nuxt generate` actually produce a JS-optional-navigable static baseline?
2. **Parity is achievable** — can one representative surface match the golden output?
3. **Webview CSP survives** — can Nuxt's hydration run under `script-src 'nonce-…'` with no `unsafe-inline`?
4. **Node-in-pipeline cost** — what does a Node build step cost against the self-contained-binary model?

### Method

The probe is a real Nuxt 3 app rendering **four real SpecScribe surfaces** from a **proxy IR**. Epic 22's canonical
IR (Story 22.2) is still `backlog`, so — exactly as Story 22.1 did — the spike consumes the already-shipped
`SpaDelivery` output (`spa/manifest.json` + `spa/pages-*.json`, 82.3 MB / 35 files / 914 pages) generated from this
repo with `specscribe generate --spa`. All SpaDelivery-specific knowledge is confined to one file
([`ir/adapter.mjs`](../../spike/nuxt-ir/ir/adapter.mjs)) so the finding survives 22.2's schema change.

Surfaces were chosen so that between them they exercise all three AC #2 sub-risks — no single existing surface
does (the dashboard is chart/token-heavy but thin on prose; prose pages are the reverse):

| route | IR page | probes |
| --- | --- | --- |
| `/dashboard` | `index.html` | chart-SVG injection (148 inline SVGs), `--status-*`/`--motion-*` tokens |
| `/adr-0006` | `adrs/0006-delivery-architecture-and-distribution.html` | Mermaid ×5, gherkin, capability, tables |
| `/story-22-1` | `implementation-artifacts/22-1-spike-…html` | reference chips ×34, comment annotations |
| `/ux-design` | `planning-artifacts/ux-designs/…/DESIGN.html` | colour swatches ×48, tables |

Charts and analysis stayed in C# throughout (ADR 0009 non-goal; ADR 0008 axis C not reopened). Every number below
is reproducible with `npm run ir && npm run generate && npm run measure`. Machine: Windows 11, Node v24.11.1,
npm 11.6.2, Nuxt 3.

---

## Measured Evidence

### Axis 1 — NFR6 under `nuxt generate`

Per surface, from the emitted static files. **"js-off" is the same document with every `<script>…</script>`
removed** — the question NFR6 actually asks:

| route | prerendered HTML | with all `<script>` removed | inline `<svg>` | `<a href>` |
| --- | --- | --- | --- | --- |
| `/dashboard` | 366,910 B | **345,264 B** | 148 | 570 |
| `/adr-0006` | 59,966 B | **59,307 B** | 54 | 98 |
| `/story-22-1` | 73,291 B | **72,626 B** | 54 | 121 |
| `/ux-design` | 53,360 B | **52,698 B** | 54 | 67 |

No blank shell, no `<div id="app">`-only output. Navigation is real files, not a client router: the spike index
emits literal `<a href="/dashboard">` etc. in the prerendered HTML, and **every target exists as its own
prerendered `index.html` on disk** (verified for all four).

**This was also confirmed live in a real browser, not just by inspecting files.** Serving the output under the
shipped webview CSP (which blocks every Nuxt module — see Axis 3) the page rendered **148 SVGs with
`__vue_app__ === false`**: a genuine zero-JavaScript render of the real dashboard.

> **Verdict: NFR6 holds.** Prerender produces a fully-rendered, navigable-without-JS baseline for real surfaces.

### Axis 2 — Parity against the golden output

The parity oracle is the shipped static page. Measured on the `<main>` region — the substantive content — with
the surrounding chrome reported separately, because the two differ for a reason that has nothing to do with Nuxt:

| route | golden `<main>` | IR `<main>` | Nuxt `<main>` | golden ≡ IR | Nuxt ≡ IR |
| --- | --- | --- | --- | --- | --- |
| `/dashboard` | 349,439 B | 349,162 B | 349,162 B | ✗ (277 B) | **✓ byte-identical** |
| `/adr-0006` | 31,163 B | 31,163 B | 31,163 B | **✓** | **✓ byte-identical** |
| `/story-22-1` | 45,103 B | 45,103 B | 45,103 B | **✓** | **✓ byte-identical** |
| `/ux-design` | 25,143 B | 25,143 B | 25,143 B | **✓** | **✓ byte-identical** |

**Vue's `v-html` round-trips the IR content string verbatim** — the exact IR byte sequence appears unmodified in
the emitted HTML for all four surfaces (`emitted.includes(irContentHtml)` = true). No re-serialization, no
attribute reordering, no self-closing-tag rewriting. This was the single largest parity risk and it is simply
absent.

Custom Markdig renderer output survives with **identical counts to the golden page** on every surface:

| surface | Mermaid | ref chips | comment annot. | swatches | gherkin | capability | tables | inline SVG |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `/dashboard` | — | — | — | 35 = 35 | 9 = 9 | — | — | 115 = 115 |
| `/adr-0006` | 5 = 5 | — | — | — | 1 = 1 | 1 = 1 | 3 = 3 | — |
| `/story-22-1` | — | 34 = 34 | 1 = 1 | — | — | — | — | — |
| `/ux-design` | — | — | — | 48 = 48 | — | — | 5 = 5 | — |

**Enumerated differences (all three are IR-capture artefacts, not Nuxt artefacts):**

1. **Dashboard `<main>`, 277 B — the IR drops 5 anchors, 3 of them `code/*.html` links.** The shipped static
   dashboard hyperlinks the git-pulse bar labels
   (`<a href="code/tests/…/SiteGeneratorAdapterTests.cs.html">`); the `SpaDelivery` capture of the same panel
   emits them as plain text. Golden `<main>` has 553 anchors, the IR has 548. This is a **lossy capture in
   already-shipped code**, reproducible without Nuxt, and it is a defect in its own right (see Follow-ups).
2. **Nav chrome outside `<main>`.** The static ADR page renders a page-local context band
   (`site-nav-local-context`, `aria-label="ADRs"`); the SPA capture carries the generic key-views nav instead.
   Known static-vs-SPA divergence in the shipped delivery form.
3. **Document `<head>`.** Nuxt owns the head; it emits `<title>` + the stylesheet link but not the golden's
   og/description meta, favicon data-URI, or `?v=<ModuleVersionId>` cache-bust. Trivially closable in 23.3 (a head
   projection from the IR) — not attempted here, since the head is 23.3's job, not the spike's.

Scoped CSS behaved as ADR 0009 assumed, with **one ergonomic surprise worth scoping into 23.2**: `<style scoped>`
compiles to `[data-v-*]` attribute selectors that Vue stamps only on **template-authored** elements. It does
**not** reach `v-html`-injected markup — styling the IR's own content from an SFC requires `:deep()` or a global
sheet. Since the migration's entire premise is injecting host-rendered HTML, this affects every component 23.2
writes.

Token binding is drift-free by construction: the probe imports the **shipped `specscribe.css` verbatim** rather
than re-typing values, and the scoped component's `var(--status-done)` resolved live to `rgb(107, 143, 98)` in
the browser.

> **Verdict: parity is achievable.** Content parity is byte-identical through the whole chain; the only content
> delta is caused by a pre-existing SpaDelivery capture defect, and the head delta is ordinary 23.3 scope.

### Axis 3 — Webview CSP

The webview ships (`WebviewRenderAdapter.cs:102`):

```
default-src 'none'; base-uri 'none'; form-action 'none'; img-src __CSP_SOURCE__ data: https:;
style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;
```

**Can Nuxt emit a nonce? Yes.** Nuxt 3 core has no nonce option, but Nitro's `render:html` hook exposes the
emitted markup before serialization, and a ~10-line plugin stamps `nonce="__NONCE__"` onto **all three** scripts
Nuxt emits (entry module, the `window.__NUXT__` config inline script, and the `__NUXT_DATA__` block). Because a
prerendered build is static, the nonce cannot be per-render by construction — but placeholder-plus-substitution
is *exactly* the mechanism `WebviewRenderAdapter` already uses, so this composes with the existing host.

**But a nonce is not sufficient.** Replaying the shipped policy verbatim over the real output in a real browser
(`scripts/csp-probe.mjs`, which substitutes `__NONCE__` per request the way the host does):

| policy | `_payload.json` | module chunks | hydrated | rendered content |
| --- | --- | --- | --- | --- |
| **shipped webview policy** | blocked | **blocked** | no | **intact** — 148 SVGs |
| + `'strict-dynamic'` | blocked | ok | yes | **BLANKED — 0 SVGs** |
| + `'strict-dynamic'` + `connect-src` | ok | ok | yes | intact — 148 SVGs |
| payload inlined + `'strict-dynamic'` | n/a | ok | yes | intact — 148 SVGs |

Three things this measures that argument would have missed:

- **A nonce does not propagate to a module's static imports.** The nonced entry module loads; the three chunks it
  imports are blocked. `'strict-dynamic'` is the only thing that fixes it.
- **`connect-src` is absent, so `default-src 'none'` blocks the payload fetch.** Nuxt preloads
  `/dashboard/_payload.json` via `<link rel="preload" as="fetch">` and fetches it on hydration.
- **The partial relaxation is catastrophically worse than no relaxation.** With `'strict-dynamic'` but no
  `connect-src`, hydration *runs*, `useAsyncData` finds no payload, `v-if="data"` goes false, and Vue **deletes
  the prerendered content** — 148 SVGs to zero, on a page that rendered perfectly with JS fully blocked. Whoever
  touches this policy must change both knobs or neither.

`experimental.payloadExtraction: false` removes the fetch entirely (payload inlined into the HTML), leaving
`'strict-dynamic'` as the **single** required CSP change. Verified working.

One un-nonced inline script remains, and it is **not Nuxt's**: the IR content itself carries SpecScribe's own
`<script type="application/json" id="sunburst-explorer-data">` island (20,915 B, from Story 20.2). The webview
already strips that island today — confirming that whatever consumes the IR, not the framework, owns this
problem.

> **Verdict: hydration does NOT survive the webview CSP as shipped — but the gap is precisely two lines wide and
> fully characterised.** Adding `'strict-dynamic'` to `script-src` plus disabling payload extraction is sufficient
> and was verified end-to-end. Degradation under the unchanged policy is *graceful*: content renders, only
> interactivity is lost.

**Honesty boundary (mirroring ADR 0005 / Story 6.3):** this is a real Chromium engine enforcing the byte-identical
policy string over the real emitted output — but it is not VS Code's Electron webview with `vscode-resource:`
URIs. Everything up to an actual VS Code paint is evidence-backed; the paint itself needs one manual F5 and is
**not** claimed here.

### Axis 4 — Node in the pipeline

**Today (state a):** Node is **build-time-only and developer-side**. `extension/` (4 devDeps) and
`tools/prism-vendor/` (1 devDep) both carry zero runtime deps, and nothing in `src/SpecScribe/*.csproj` references
node or npm. **`specscribe generate` does not need Node to run.**

**Measured cost of adding it:**

| | measured |
| --- | --- |
| `npm ci`, cold cache | **18.4 s** |
| `node_modules` | **183.9 MB** (build-time only) |
| `nuxt generate`, first ever run | 112.5 s |
| `nuxt generate`, warm, 4 routes | 22.8 – 26.7 s |
| `nuxt generate`, 204 routes | 30.6 s |
| **`nuxt generate`, all 918 routes** | **37.1 s** (prerender phase alone: 15.5 s) |
| marginal cost per route | ≈ 17 – 24 ms |
| fixed Vite/Nitro build floor | ≈ 22 s |
| `_nuxt/` client assets | 239 KB |

Scaling is benign — the cost is dominated by a ~22 s fixed build, and 914 additional pages add only ~15 s. For
reference, the full `specscribe generate --spa` pipeline (parse, git analysis, charts, 914 pages, IR emission)
takes **64.5 s** on the same machine, so Nuxt adds roughly **+57 % to generation wall-clock**.

**Output weight at full site scale:**

| | bytes |
| --- | --- |
| C# static site (915 HTML files) | **57.0 MB** |
| Nuxt prerendered HTML (921 files) | **56.5 MB** — within 1 % |
| Nuxt hydration payloads (919 files) | **+67.4 MB** |
| **Nuxt total** | **129 MB (2.26×)** |

The prerendered HTML is **not** the cost — it is essentially identical to what C# emits. The entire 2.26× is
hydration payload: every page's content is shipped twice, once as HTML and once as devalue-serialized JSON.
Inlining the payload does not reduce the total (dashboard HTML goes 366,910 B → 827,584 B), it only moves it.
**Any Epic 23 implementation story that does not address payload duplication doubles the site's weight.** The
lever exists — Nuxt server components / `<NuxtIsland>` render server-side without a hydration payload, which is
the right shape for content that is static by construction — but it was **not** measured here and should be
23.2's first experiment.

**Distribution impact (input to 23.5, not a solution):**

- **(b) Nuxt at package/CI time, pre-built assets shipped in the binary** — compatible with the self-contained
  model. The 239 KB of `_nuxt/` assets embed like any other asset; `npm ci` + `nuxt build` join CI, matching how
  `extension/` and `tools/prism-vendor/` already work. End users still need no Node. **This is the only variant
  that preserves ADR 0005/0006.**
- **(c) Node in the `specscribe generate` critical path or end-user runtime** — breaks it. Prerendering is
  per-project (routes come from *the user's* IR), so shipping pre-built HTML is impossible; the user's machine
  would need Node + `node_modules`. That contradicts the self-contained-binary premise, and neither the npx
  channel (16.8) nor the VS Code Marketplace channel (16.5) could stay Node-runtime-free.

**The unresolved tension 23.5 must answer:** ADR 0009 wants Nuxt to render *the user's* project, but (b) only
ships pre-built assets, not pre-rendered pages. Either the shipped artefact becomes a client-rendered SPA over the
IR (which forfeits NFR6 — the thing Axis 1 just proved), or `specscribe generate` invokes Node at run time
(which forfeits self-containment). **This spike did not solve it, and it is the single biggest open question in
Epic 23.**

---

## Findings

1. **NFR6 holds under Nuxt prerender.** Fully-rendered, JS-optional, file-per-route. Confirmed in a real browser
   with all scripts blocked. *(AC #1 — satisfied.)*
2. **Content parity is byte-identical.** `v-html` round-trips the IR verbatim; `<main>` is byte-for-byte equal
   golden → IR → Nuxt on 3 of 4 surfaces, and every custom Markdig renderer survives at identical counts. The
   ~889 LOC of custom renderers ADR 0009 named as the fidelity risk are **not** a risk when their output travels
   as data. *(AC #2 — satisfied.)*
3. **The dashboard's 277-byte delta is a pre-existing SpaDelivery defect**, not a migration cost: the SPA capture
   drops 5 anchors (3 `code/*.html` links) from the git-pulse panel.
4. **`<style scoped>` does not reach `v-html` content.** Needs `:deep()`. Affects every component 23.2 writes.
5. **The webview CSP blocks hydration**, a nonce alone is insufficient (module imports need `'strict-dynamic'`),
   and **the half-fix blanks the page**. Fully characterised; two-knob fix verified.
6. **Nuxt emits nonces fine** via a Nitro `render:html` hook, composing with the host's existing `__NONCE__`
   substitution. The one un-nonced script is SpecScribe's own IR-carried island.
7. **Node costs +37 s and 2.26× output weight** at full scale — the weight being *entirely* hydration payload,
   with the prerendered HTML within 1 % of C#'s.
8. **`crawlLinks: true` is unusable.** Nitro's crawler follows the injected IR content's own links and aborts the
   build on the first 404. The route table must come from the IR manifest — which is the correct design anyway.
9. **Packaging is unresolved and is the epic's real risk.** Prerendering is inherently per-project; "ship
   pre-built assets" does not cover it.

## Gate — do Stories 23.2–23.5 proceed as scoped?

| Story | Gate | Reason |
| --- | --- | --- |
| **23.2** Component library & design-token bridge | **PROCEED, re-scoped** | Direction confirmed (tokens bind cleanly, scoped CSS compiles as expected). Add two items the spike surfaced: (a) a `:deep()`/global-sheet convention for styling `v-html`'d IR content — finding 4; (b) **measure server-components/`<NuxtIsland>` against payload duplication first**, since finding 7 says the naive shape doubles the site. |
| **23.3** Migrate baseline surfaces (dashboard, epics) | **PROCEED as scoped** | Parity is byte-identical on the surface it targets. Add the `<head>` projection (difference 3) to its scope; it is genuinely 23.3's job. |
| **23.4** Migrate remaining surfaces, retire `HtmlRenderAdapter` | **PROCEED, gated** | Must not start until 23.5 answers packaging — retiring the C# renderer is irreversible, and finding 9 means the delivery model for a Nuxt-rendered site is not yet settled. Also owns the webview CSP change (finding 5) as a **two-knob atomic edit**, with a regression test asserting content survives, since the half-applied fix is worse than none. |
| **23.5** Packaging reconciliation (Node build step) | **RE-SCOPE — promote and resequence ahead of 23.4** | It is no longer a tidy-up at the end of the epic; finding 9 makes it the epic's load-bearing unknown. It must decide between per-project prerender-at-generate-time (Node in the critical path) and a client-rendered shipped SPA (forfeits NFR6) **before** 23.4 retires the C# renderer that is currently the only thing satisfying both. |

**ADR 0009 stands unamended.** Nothing measured contradicts it: NFR6 survives prerender, parity is achievable,
charts-as-data works, and the CSP gap is closable. The re-scoping above is about *sequence and emphasis*, not
direction.

## Follow-ups outside this story

- **`SpaDelivery` drops `code/*.html` links from the dashboard's git-pulse panel** (finding 3). A live fidelity
  defect in shipped code today — the SPA and webview surfaces silently lose those links. Fix standalone, or fold
  into Story 22.2 when it generalises the capture.
- **What the front end wished the IR carried** (input to Story 22.2): a structured `head`/meta projection (title,
  description, og tags) so the consumer need not re-derive it; the page-local nav context that the static
  renderer computes but the capture discards; and a declaration of which embedded `<script>` islands a consumer
  must strip or nonce.

---

## What was NOT done (scope guard held)

No component library or token-convention work (23.2). No production dashboard/epics migration (23.3). The C#
`HtmlRenderAdapter` is fully intact (23.4). No packaging solution (23.5) — only its cost, as AC #3 requires. No
chart port and no analysis moved out of C#. **No production code shipped:** `src/SpecScribe/**` and `tests/**` are
git-confirmed untouched; everything the spike produced lives under `spike/nuxt-ir/`. Nothing joins
`SpecScribe.slnx` (still exactly two projects), `dotnet build src/SpecScribe`, `dotnet pack`, or the `extension/`
bundle, and `node_modules/` is excluded by the generator's own directory filter
([`SiteGenerator.cs:4110`](../../src/SpecScribe/SiteGenerator.cs)).

**One correction to the story's stated invariant, recorded rather than glossed:** the task text asked that "the
generated site stay byte-identical with or without the spike folder." **It does not, and it could not have for
any spike in this repo** — SpecScribe documents its own repository, so adding files to it changes its
self-documentation. Measured: the spike folder adds **13 code pages** (`code/spike/nuxt-ir/**`), which is the
same thing `code/spike/vscode/**` and `code/spike/delivery/**` already do for the Story 6.3 and 6.6 spikes. What
*is* true, and is the invariant that actually matters, is that **the generator's rendering behaviour is
unchanged**: no `src/` or `tests/` file was modified, and the `GoldenContentFingerprint` gate produces the exact
same hash with and without this branch.

### Test suite

`dotnet test SpecScribe.slnx -c Release` → **2,169 passed, 3 skipped, 1 failed**. The single failure is
`GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`, expecting an updated constant of
`1eaefe51a347154b158b652ec5baa0660d05a9f1a4dc9224f200e0ade3a5482b`. **This is pre-existing stale-constant drift,
not caused by this spike** — verified by running the same test on a clean detached worktree of `9243de5` with the
spike folder absent, which fails with the *byte-identical* expected hash. It is the same class of drift Story
22.1 reported on `main`.

An earlier run of the same suite also showed a transient failure in
`SiteGeneratorGitInsightsTests.GenerateAll_TwoRunsProduceIdenticalHubMarkup` (missing `git-insights.html` in a
temp dir) which did **not** reproduce on re-run — flaky, and noted here rather than buried.
