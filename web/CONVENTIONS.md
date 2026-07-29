# Component conventions — `web/`

How to author components in SpecScribe's Vue/Nuxt presentation layer. Written by Story 23.2 for the stories
that follow it; Story 23.3 is the first consumer.

Architecture lives elsewhere: [ADR 0009](../docs/adrs/0009-frontend-framework-for-projection-layer.md)
(Vue + Nuxt 3, universal/SSR, full prerender), [ADR 0013](../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
(the text twin is the no-JS contract), and AD-7 in
[ARCHITECTURE-SPINE.md](../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) (SpecScribe owns
content-semantic tokens; host chrome is host-owned).

---

## 1. Tokens are generated. Never hand-edit them.

`assets/tokens.css` is a **verbatim extraction** of **every top-level `:root` block** in
`src/SpecScribe/assets/specscribe.css`. That C# stylesheet is the single source of truth for SpecScribe's
presentation tokens (AD-7). The extracted file is a copy, never a second definition.

> Every top-level block, plural, since 2026-07-28. The extractor took only the **first** until then, so when
> the Impact Map added a second `:root` (the `--impact-lvl-*` ramp) that family silently never crossed — and
> because the gate ran the same one-block extractor on both sides, it could not disagree about tokens neither
> side looked at. It printed "OK — 36 tokens in sync" throughout. A `:root` nested inside an at-rule is *not*
> carried: that is a viewport-conditional override, and this app owns its own breakpoints.

```bash
npm run extract:tokens   # regenerate after ANY token change in the C# stylesheet
npm run check:tokens     # drift gate — exits non-zero when the two diverge
```

`check:tokens` reports drift at token granularity (added / removed / value-changed), so a failing log's first
line already says which kind of change happened. It also fails on a comment-only edit, because the file is a
verbatim copy by contract and a hand-edit is precisely what the gate exists to catch.

**Every colour and every duration in a component comes from `var(--…)`.** No component re-types a token value.
This is what makes AC #1's "no duplicated or hand-re-typed definitions" enforceable rather than aspirational —
the only way the Vue app's values can disagree with the shipped portal's is for the gate to fail.

What deliberately does **not** cross the bridge: the webview theme remaps in `specscribe-webview-theme.css`.
Those are host-owned chrome under AD-7.

### Why not just import `specscribe.css`?

The 23.1 spike did exactly that, and it works. It is still the wrong shape: pulling a 7,041-line monolith into
every page keeps alive the fragility class Epic 23 exists to end. That stylesheet has already had a
single mistyped comment silently close early and take ~1,000 rules with it, invisible to the entire test
suite. Scoped components plus a thin generated token layer is the trade this epic is making.

The app's own base layer (`assets/base.css`) is the deliberate minimum that a token file alone cannot supply:
reset, page typography, focus ring, and the global reduced-motion block. It is hand-authored but every value
in it resolves through a token. **Component rules do not go there.**

---

## 2. `<style scoped>` for anything the template authored

Default to scoped styles in every SFC. Vue rewrites each selector to a `[data-v-*]` attribute selector, so a
component's rules cannot leak into a sibling. That containment is the point of the migration.

A child component's **root element** does receive the parent's scope attribute, so a parent may position a
child it renders (`.swatch-row > .status-badge { … }` works). It may not reach *inside* the child — that is
the child's business.

---

## 3. Styling injected content needs `:deep()` — this one is load-bearing

**Scoped styles do not reach `v-html`-injected markup.** Vue stamps the `data-v-*` attribute onto
template-authored elements only; HTML that arrives as a string is never stamped, so a scoped rule targeting it
matches nothing and fails **silently** — no error, no warning, just unstyled content.

This is Story 23.1's spike finding 4, and it binds Story 23.3 directly, because 23.3 injects the IR's rendered
prose. `/design-system` carries a live worked example of both halves side by side — the failing control and
the working fix — under "Styling injected content".

```vue
<template>
  <div class="prose" v-html="page.contentHtml" />
</template>

<style scoped>
/* Does NOT apply: compiles to `.prose .callout[data-v-*]`, and the injected node has no such attribute. */
.prose .callout { border-left: 3px solid var(--rust-light); }

/* Applies: `:deep()` drops the attribute requirement on the descendant part of the selector. */
.prose :deep(.callout) { border-left: 3px solid var(--rust-light); }
</style>
```

Either `:deep()` or a global stylesheet will work. Prefer `:deep()` scoped to the injecting component — it
keeps the blast radius at one component instead of the whole app, which is the property a global sheet gives
away.

---

## 4. Measured: use build-time data. Neither `useAsyncData` nor `<NuxtIsland>`.

AC #4's experiment. Three routes render **identical markup from identical data** (200 story-shaped rows
through `ListRow` + `StatusBadge`) and differ only in how the data reaches the component. Re-measured
2026-07-28 with `npm run generate && npm run measure:payload` on **Nuxt 4.5.1 / Vue 3.5.40 / Node 24.11.1**;
the run is committed at [`measurements/payload.json`](measurements/payload.txt) so the numbers are checkable
rather than quoted:

| variant | HTML | payload | island JSON | total | vs control |
| --- | --- | --- | --- | --- | --- |
| A — `useAsyncData` | 119.3 KB | 44.5 KB | — | **163.9 KB** | 1.37× |
| B — `.server.vue` island | 119.0 KB | 0.3 KB | 119.0 KB | **238.4 KB** | 2.00× |
| C — module-scope control | 119.3 KB | 0.1 KB | — | **119.4 KB** | 1.00× |

_(The original 23.2 run on Nuxt 3.21.9 gave 1.36× / 1.99× / 1.00×. The conclusion survived the Nuxt 4 major
unchanged; the table had been left pinned to a version the app no longer used.)_

**The server-component shape lost, and it lost badly.** The 23.1 spike hypothesised that `<NuxtIsland>` would
avoid the hydration-payload duplication behind its measured 2.26× site weight. It does drain the route's
`_payload.json`, but it then emits the island's **entire rendered HTML and its scoped CSS a second time** into
`__nuxt_island/<Component>_<hash>.json` so the client can re-fetch it. For content that is static once
prerendered, that is a payload *amplifier*: 2.00× against 1.37× for the thing it was supposed to beat. **This
half of the experiment is sound and is the durable finding.**

### ⚠️ What variant C does and does not prove (corrected 2026-07-28)

The control is **not a data path**, and the original write-up over-read it. `pages/measure/static.vue` calls
`buildRows(200)` at module scope, but the route still hydrates, so the browser **re-runs that generator** from
`utils/measure-rows.ts` — 14 deterministic lines bundled into `_nuxt/`. And `measure-payload.mjs` totals
`html + payload + island` and **never counts `_nuxt/` chunks**, so those bytes are invisible to the table.

So variant C measures *"no data had to cross the boundary, because the data is a pure function"* — not
*"build-time resolution is free"*. Real IR content is not a pure function, and the difference is not academic:
getting there in 23.3 took a `#ir` Vite-environment resolver, a throwing browser stub, and
`routeRules: { '/**': { noScripts: true } }` (see §12) — machinery this measurement neither used nor implied.

**Recommendation for 23.3, restated honestly:** resolve IR data at build time and **ship the route with no
Nuxt runtime at all** (`noScripts: true`). A route with no scripts cannot carry a hydration payload, which
makes the guarantee structural instead of measured-and-hoped. Variant C is the *floor* that shape aims at, not
a recipe that transfers on its own. What the table does establish, firmly, is the ordering: **do not reach for
`<NuxtIsland>` for prerendered content, and do not route static data through `useAsyncData`.**

Two further caveats, so the recommendation is not over-generalised:

- Island JSON is keyed by component **and props hash**, so N routes rendering the same island with the same
  props share one file. That sharing does not help 23.3, whose per-page IR content differs on every route.
- Variant B is still the right tool for content that genuinely must be resolved per request at runtime. Epic
  23 has no such surface today; the whole site is prerendered.

Independently of which variant wins: under the shipped webview CSP (`default-src 'none'`, no `connect-src`)
the payload fetch is blocked outright, so any shape that *depends* on it is broken there regardless of weight.
That knob is Story 23.4's, as a two-part atomic edit — see the 23.1 spike report.

---

## 5. One vocabulary, one owner

Components style a status; they do not know what statuses exist.

`StatusBadge` takes `stage`, `label` and `meaning` as props, and **`label` is required** — so a status can
never be rendered as colour alone (UX-DR17). What it deliberately does *not* contain is a stage→word or
stage→meaning map. That vocabulary belongs to the data layer (C# `StatusStyles` today, the canonical IR from
23.3 on). A parallel copy in JS would be a second status vocabulary free to drift from the one the portal
renders — the exact class of drift this epic exists to end.

The `/design-system` page holds the vocabulary as page data for its gallery. That is a showcase fixture, not
a source of truth; 23.3 replaces it with the IR.

The union carries **all ten** of `StatusStyles.LegendStages`. Two of them have no `--status-*` token of their
own and borrow one — `unmapped` shares `--status-pending`, `retired` shares `--status-deferred` — and both
stay distinct by WORD. Never publish `--status-unmapped` or `--status-retired`; neither exists.

### ⚠️ Open dependency on 23.3: the badge glyph

`StatusStyles.Badge` emits **icon + word** and documents the portal rule as "color + icon + word, never
icon-only". `StatusBadge.vue` renders **the word only** — there is no icon prop, slot or sprite. UX-DR17 still
holds (the word is always present), but two pairs the portal separates by glyph do not separate here:
`ready`/`drafted` share a border colour, and `deferred`/`retired` are byte-identical rule sets.

Supplying the glyph is deferred to **Story 23.3**, where the stage→icon mapping gains a data source in the
canonical IR. Until then, do not write a component or a doc that claims the icon channel exists. (An earlier
version of `StatusBadge.vue`'s header asserted UX-DR17 was "enforced BY THE COMPONENT'S SHAPE"; it was not,
and required-ness guards `undefined`, not `''`.)

---

## 6. Accessibility and motion are structural, not per-component

- `PageShell` owns the skip link and the `#main-content` landmark, so a new route cannot forget them.
- Motion is declared **only** through `--motion-*` tokens. Do not write literal durations.
- The `prefers-reduced-motion` reduce block lives once, globally, in `assets/base.css`. Components do **not**
  each carry their own — a per-SFC copy is exactly the drift the token family exists to prevent.
- Never signal state by colour alone. Every status carries its word; the unrecognized stage additionally
  carries a hatch texture so it differs from the six real stages by more than hue.
- Wide content scrolls inside its own container. `ChartPanel` is `overflow-x: auto` for this reason — a wide
  table must never make the page body scroll sideways.
- That global reduce block neutralises **delay as well as duration**. Clamping duration alone is the trap:
  with `animation-fill-mode: both` an element is held at its `from` keyframe — usually `opacity: 0` — for the
  whole delay, so a list staggered by `--motion-stagger` would show a reduce-motion reader seconds of blank
  page. Content missing, not merely still.

### CSS modules

Not used, deliberately. Scoped SFCs give the same containment with less indirection, and the styling that
cannot be scoped at all (`v-html`'d IR content, §3 and §10) is not something modules would solve either. If
you find yourself reaching for one, the answer is a scoped SFC or the `ir-content.css` layer.

---

## 7. Verifying a change

The C# test suite cannot see CSS containment leaks, sub-pixel collapse, or DOM corruption from markup
splicing. All three have shipped in this project and all three were caught only by looking at the rendered
page (CLAUDE.md § Verification).

```bash
npm run check:tokens                    # token drift gate
npm run generate                        # must prerender every route without error
npm run measure:payload                 # re-run if the data path changes
npm run dev                             # then inspect real computed styles in a browser
```

Inspect **computed** values, not source: confirm `var(--status-done)` actually resolves to the moss value on
a rendered badge, and confirm scoped styles are containing where you expect them to.

---

## 8. Routes are the IR's paths, verbatim. Nothing rewrites an href.

Ratified shape: [ADR 0017](../docs/adrs/0017-projection-routes-mirror-ir-paths.md) (Proposed).

A page's route is its IR `outputRelativePath` with a leading slash — `/index.html`, `/epics/epic-23.html`,
`.html` and all. The IR carries whole rendered markup (ADR 0016), and that markup contains the site's entire
link graph as relative hrefs: **88,695 internal links across 1,049 pages** on this repo. Mirroring the
emitter's path space means every one of them resolves unchanged.

**So no href is ever rewritten.** If a link dangles, it dangles in the generated portal too and the fix
belongs to the emitter. `npm run check:links` enforces exactly that distinction — it walks both trees and
gates on links that resolve in the golden site and dangle here, reporting inherited breakage separately
rather than blaming the migration for it.

Two consequences that look like bugs and are not:

- **All IR routing goes through one `pages/[...path].vue` catch-all.** Nuxt's file-based routing cannot
  express a route with a `.html` extension (there is no valid `pages/epics.html.vue`). The catch-all
  resolves `route.params.path` against the manifest and branches to a surface component by path.
- **Nitro silently refuses to write a route whose path contains `..`** — its `canWriteToDisk` guard is a
  substring test, not a path-segment test. SpecScribe emits a code page per repository file, so a source
  file with two consecutive dots in its name is rendered, logged `(skipped)`, and never written. The
  `nitro:init` hook in `nuxt.config.ts` writes those pages itself.

---

## 9. Splitting the IR's content region — the nested-`<main>` trap

`SpaDelivery.ExtractContentRegion` returns `navMarkup + [wayfinding] + <main id="main-content">…</main>` —
the `<main>` **element**, not just its body. `PageShell` emits its own. Injecting the region whole gives you
a nested `<main>`, a duplicate `id`, and two navs.

`ir/adapter.ts` splits the region back into `{ navHtml, wayfindingHtml, mainAttributes, mainAttrs,
mainInnerHtml }` using the same markers the emitter concatenated with, and fails loudly on a page it cannot
account for. Three things it taught, all of which cost a build to learn:

- **The IR carries TWO region shapes.** The 187 dashboard/epics-family pages are re-rendered from their view
  models, so their region carries the whole wayfinding band, wrapper and all. The 853 captured pages go
  through `ExtractContentRegion`, which slices from `<div class="breadcrumb"` — *inside* the wrapper — so
  their region carries the wrapper's closing `</div>` without its opener. Treating those as one shape
  double-opened the wrapper and nested `<main>` and `<footer>` inside the breadcrumb band on every migrated
  page. **The `<main>` region stayed byte-identical, so parity, link resolution and every a11y assertion
  passed.** It was visible only as real DOM geometry in a browser. `npm run check:a11y` now asserts the
  structure so it cannot come back quietly.
- **`<main>` must not be a template-authored element.** An SFC with `<style scoped>` stamps `data-v-*` onto
  every element in its template, and slot content renders as a fragment bracketed by Vue's `<!--[-->`
  hydration anchors. Both land inside the compared region. `IrMain.ts` is a render function in a
  style-less component for exactly this reason; `PageShell` yields `<main>` to it under `chrome="nav-only"`.
- **Injected runs need no wrapper element.** `IrHtml.ts` uses `createStaticVNode`, so nav, wayfinding and
  main body are spliced in with no host `<div>` of ours — which matters because the portal's stylesheet has
  direct-child selectors a wrapper would silently break.

---

## 10. Styling injected content: the `ir-content.css` layer (transitional)

Ratified shape: [ADR 0018](../docs/adrs/0018-transitional-ir-content-style-layer.md) (Proposed).

`tokens.css` styles **none** of the injected markup — the IR's prose is authored against the 7,041-line C#
monolith this app deliberately does not import (§1). Without a second layer every migrated page renders
structurally correct and visually bare.

```bash
npm run extract:ir-content   # regenerate after ANY change to specscribe.css OR to what the surfaces render
npm run check:ir-content     # drift gate — proven red three ways, not only green
```

What makes it a transition rather than a re-import: it is **bounded** by measured selector usage across the
migrated families (897 rules + 4 keyframes, 62 % smaller than source), **generated** and gated, **scoped**
under `.ir-content` so it cannot reach a template-authored component, and **enumerated** in
`assets/ir-content.manifest.json` — that list is what Story 23.4 retires. Pass-through pages get whatever
overlap the migrated families already paid for; the extractor prints that coverage (48 % here) as a number
rather than leaving it implied.

Attribute selectors deliberately do **not** bound the extraction. Nearly every one expresses runtime state
(`[data-ss-hierarchy-boot]`, `[data-hierarchy-ready]`, `[open]`) that is absent from server-rendered markup,
so requiring them drops the Hierarchy Explorer's anti-flash CSS — silently, with the page still rendering.

---

## 11. Runtime assets are COPIED. Never forked, never reimplemented.

```bash
npm run sync:assets    # copy specscribe.js, plotly-hierarchy.min.js, prism.{css,js} into public/
npm run check:assets   # drift gate against the source in src/SpecScribe/assets/
```

ADR 0012 §Decision 2 makes "one Hierarchy Explorer component is the only route to a sunburst or treemap" an
invariant — after ADR 0010 §6 asked for one shared engine and got three arc renderers instead. A Vue
re-implementation of the explorer would be precisely the second implementation that ADR exists to prevent.

So the Nuxt app **hosts** the shipped implementation rather than porting it: `IrSurface.vue` loads
`specscribe.js`, which calls `initHierarchyExplorers(document)` at load and re-runs on the existing
`specscribe:content-swapped` event. No new API was needed and none was added.

Two things that are easy to lose:

- **`v-html` never executes injected `<script>`.** Fine for the explorer's data island, which is inert
  `type="application/json"` the component reads out of the DOM — but it means nothing executable can arrive
  through IR content, so the boot must come from the Nuxt layer. The adapter surfaces
  `hasExecutableIsland`, and `IrSurface` throws on it rather than shipping a page that quietly does nothing.
- **The anti-flash boot marker is chrome-level.** `HierarchyExplorer.cs` emits it just before `<main>`,
  deliberately outside the captured region, so it is absent under Nuxt and its absence degrades silently to
  a visible flash-then-swap. It is re-emitted from the head, with the script body **copied** off the
  generated site rather than re-typed.

`web/public/` is gitignored: 1.4 MB whose authoritative source is in this same repo. The gate compares
against that source, not against a committed copy, so nothing is lost by not committing one.

---

## 12. IR-backed routes ship no Nuxt runtime

`routeRules` sets `noScripts: true` for everything except the app's own routes. These pages are fully
prerendered content whose only interactivity is the portal's own vanilla `specscribe.js`; there is nothing
for Vue to hydrate, and hydrating would be actively wrong — the IR is resolved at build time and is
deliberately absent from the client bundle, so a hydration pass would find no data and blank the page.

That absence is structural, not conventional. App code reaches the IR only through the `#ir` specifier, and
a Vite plugin resolves it per build environment: the real adapter for SSR/prerender, a throwing stub for the
browser. `#ir` is **not** a Nuxt `alias` — Vite's own alias plugin runs ahead of every user plugin, so an
alias entry resolves it to the server adapter before the environment-aware plugin sees it, and drags
`node:fs` into the browser bundle. The alias exists in `tsconfig` paths only, for the editor.

Measured consequence: **zero `_payload.json` files** for the 1,043 IR routes, and zero Nuxt `<script>` tags
on any of them. The whole prerendered site is **64.3 MB across 1,079 files** against the generated portal's
65.9 MB across 1,049 — smaller than the thing it projects, against the 23.1 spike's 2.26×.

---

## Status of this app

`web/` is production-intent but **not shipped yet**: it is not in `SpecScribe.slnx` and not wired into
`specscribe generate`. How the Node build reaches users is Story 23.5's decision, sequenced ahead of 23.4
because it is Epic 23's load-bearing unknown. `spike/nuxt-ir/` remains the throwaway 23.1 probe — read it for
context, but its token binding (wholesale `specscribe.css` import) is superseded by the extraction here.
