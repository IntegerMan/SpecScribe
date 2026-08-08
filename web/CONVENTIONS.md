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
prose. Both halves are shown below — the failing control and the working fix.

_(Until 2026-08-07 this example was also rendered live at `/design-system`. That Vue route was retired — see
§ Status of this app — so the worked example lives here, which is where AC #5 says it must be demonstrated.)_

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

Either `:deep()` or a global stylesheet will work, and the choice is a real trade: `:deep()` keeps the blast
radius at one component, which is the property a global sheet gives away.

> ⚠️ **What actually shipped is the global sheet, not `:deep()` — read §10 and §10a before following the
> preference above.** [Story 23.2 review 2026-08-07] Stories 23.3/23.4 inject through `IrHtml.ts`
> (`createStaticVNode`) and style the result from **generated global sheets**: `ir-content.css`, scoped only
> by an `.ir-content` prefix, plus the deliberately **unscoped** `shared-primitives.css` of ADR 0029. That was
> the right call at that scale — a per-component `:deep()` cannot carry ~875 rules harvested from the C#
> monolith — but it means this section's preference describes the *small* case only. Use `:deep()` when your
> component injects a bounded fragment it owns; use the generated layers for IR content. This paragraph exists
> because a reader following §3 in isolation was being pointed away from the shipped architecture.

---

## 4. Measured: use build-time data. Neither `useAsyncData` nor `<NuxtIsland>`.

AC #4's experiment. Three routes render **identical markup from identical data** (200 story-shaped rows
through `ListRow` + `StatusBadge`) and differ only in how the data reaches the component. Re-measured
2026-07-28 with `npm run generate && npm run measure:payload` on **Nuxt 4.5.1 / Vue 3.5.40 / Node 24.11.1**;
the run is committed at [`measurements/payload.txt`](measurements/payload.txt) (and `payload.json`) so the
numbers are checkable rather than quoted:

_(Table synced to the committed record 2026-08-07. It had drifted ~2 KB per row: sibling Story 23.4 re-ran
the harness and left the table at the previous run. Ratios were unaffected, so the conclusion never moved —
but "checkable rather than quoted" is only true if someone checks, so this is the check.)_

| variant | HTML | payload | island JSON | total | vs control |
| --- | --- | --- | --- | --- | --- |
| A — `useAsyncData` | 121.2 KB | 44.5 KB | — | **165.8 KB** | 1.37× |
| B — `.server.vue` island | 120.9 KB | 0.3 KB | 121.0 KB | **242.2 KB** | 2.00× |
| C — module-scope control | 121.2 KB | 0.1 KB | — | **121.3 KB** | 1.00× |

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

The `/design-system` page used to hold the vocabulary as page data for its gallery. **That page was retired
on 2026-08-07** (Story 23.2 review) — it was never prerendered into a packaged build, so no user could reach
it, and it had begun to teach a *different* definition of `unmapped` than the portal did. The design-system
page users actually get is `design-system.html`, rendered from the C#-composed region through
`PortalMetaSurface`, which takes its vocabulary from `StatusStyles` and so cannot drift from it.

The union carries **all ten** of `StatusStyles.LegendStages`. Two of them have no `--status-*` token of their
own and borrow one — `unmapped` shares `--status-pending`, `retired` shares `--status-deferred` — and both
stay distinct by WORD. Never publish `--status-unmapped` or `--status-retired`; neither exists.

### ⚠️ The badge glyph is missing, and it has no owner — read this before using `StatusBadge`

`StatusStyles.Badge` emits **icon + word** and documents the portal rule as "color + icon + word, never
icon-only". `StatusBadge.vue` renders **the word only** — there is no icon prop, slot or sprite. UX-DR17 still
holds (the word is always present), but `ready`/`drafted` share a border colour and are separated in the
portal by glyph alone.

**Corrected 2026-08-07:** this paragraph used to add that "`deferred`/`retired` are byte-identical rule sets".
They are not — `.is-deferred` binds `var(--status-deferred)` and `.is-retired` binds `var(--border)`, so they
differ by border as well as by word. The claim was inherited from an earlier state of the component and was
one of the stated justifications for deferring the glyph, so it is corrected here rather than quietly dropped.

**The deferral to Story 23.3 is withdrawn, because 23.3 could never have discharged it.** The IR-backed
surfaces do not instantiate `StatusBadge` at all — they inject C#-rendered markup, which already carries the
glyph. After the `/design-system` retirement, `StatusBadge` has **no product consumer**: its only remaining
uses are the `/measure/*` payload fixtures. Treat it as a **fixture-grade component**, not a shipped
primitive, and do not build a product surface on it until either the glyph lands or the surface genuinely
needs a template-authored badge. `ChartPanel`, `ListRow` and `PageShell` are *not* in this position — they are
consumed by `/component-library`, `error.vue` and (for `PageShell`) every IR route through `IrSurface`.

Until then, do not write a component or a doc that claims the icon channel exists. (An earlier version of
`StatusBadge.vue`'s header asserted UX-DR17 was "enforced BY THE COMPONENT'S SHAPE"; it was not, and
required-ness guards `undefined`, not `''` — nor `null`, which is what a JSON IR emits for an absent field.)

---

## 6. Accessibility and motion are structural, not per-component

- `PageShell` owns the skip link **element** and the `#main-content` landmark, so a new route cannot forget
  them. It does **not** own the skip link's CSS: since 2026-08-07 `.skip-link` is a shared primitive (ADR
  0029), so the one definition lives in `specscribe.css` and arrives via the unscoped
  `shared-primitives.css`. Do not add a `.skip-link` rule to a `<style scoped>` block — a scoped copy ties on
  specificity with the generated `.ir-content .skip-link` and the winner is decided by chunk order, which is
  the defect that prompted the promotion. `IrSurface` puts `class="ir-content"` on `PageShell`'s own root, so
  a descendant selector from the generated layer is a *competitor* here, not a non-match.
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

⚠️ **`extract:ir-content` is only ever correct for the corpus it was extracted from, and `--deep-git` is part
of that corpus.** The extractor **prunes** any rule whose selector names a class it cannot find in the IR, so a
regeneration run against a *narrower* portal silently strips rules the shipped site needs — with the gate
green afterwards, because the check re-derives through the same pruning. A shallow run omits the code-insights
history/relationships tabs, the relationship-graph swatches and the deep-analytics panels; CI measured
**-182 rules** without the flag and **-0** with it, and `publish-docs-live-pages.yml` publishes *with* it.
Always regenerate from a full run:

```sh
dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed the changed asset
cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci && npm run sync:assets && npm run build:package
cd .. && dotnet run --project src/SpecScribe --no-build -- generate --deep-git
cd web && npm run extract:ir-content && npm run check:ir-content
```

`SPECSCRIBE_PACKAGE_BUILD=1` on the install is what breaks the cycle: `postinstall: nuxt prepare` loads
`nuxt.config.ts`, which reads the IR manifest, which does not exist before the first generate. (Working from a
git worktree, also set `SPECSCRIBE_RENDERER_DIR` to *that* checkout's `web/.output` — the repo-root search
finds a `.git` **directory**, and a worktree's is a **file**, so `generate` otherwise looks for the renderer in
the main checkout and silently skips the prerender.)

**The build-lifecycle hook, and what it deliberately does not cover.** `prebuild` and `pregenerate` run
`check-tokens` unconditionally and `check-ir-content --if-ir` conditionally. The `--if-ir` flag runs the real
check when an IR is present and otherwise **skips loudly** (exit 0, three lines of warning). That asymmetry is
the point: `pregenerate` runs *before* the build that would produce an IR, so an unconditional gate would
hard-fail every cold build, while the mistake actually worth catching — editing `specscribe.css` and
re-running `generate` against an output root that already exists — always has one. `npm run check` stays
**unflagged**, so in CI (which always generates first) a missing IR is a hard failure rather than a silent
skip.

What makes it a transition rather than a re-import: it is **bounded** by measured selector usage, **generated**
and gated, **scoped** under `.ir-content` so it cannot reach a template-authored component, and **enumerated**
in `assets/ir-content.manifest.json`.

**↻ Story 23.4 changed two things here, and neither is what the story planned.**

**1. The extraction bound is now the WHOLE SITE, not four families.** Story 23.3 bounded it to the four families
it migrated and reported the shortfall for everything else as a coverage number, correctly — those pages were
`PassThroughSurface` and not claimed. Once Story 23.4 migrated the remaining **1,276** pages that bound became a
silent defect: the extractor carried rules for four families while the router rendered fourteen, so **~58 % of
the classes those pages emit had no rule at all** and the elements rendered **bare** — nothing failed, nothing
logged. Current numbers: **1,469** pages drive the extraction, **1,423 rules + 4 keyframes**, **393 of 1,814**
source rules still dropped as unused, **45 % smaller** than source, and pass-through class coverage **100 %**.
The size headline shrank; **containment and the gate did not**, and those are what ADR 0018 actually rests on.

**2. The layer is NOT retired, and "when it is empty" is unreachable as written.** Owner decisions D3/D5 asked
for retirement to empty. Measured: only **6.5 %** of carried rules are prose and authorable today; **93.5 %**
style bespoke vocabulary **injected as rendered HTML** across **651 classes**. So AC #4's **second branch** was
taken — residue enumerated with a named blocker per bucket:

```bash
npm run report:ir-content-residue   # → measurements/ir-content-residue.{txt,json} (committed)
```

| bucket | rules | what it waits on |
| --- | --- | --- |
| prose | 93 | **nothing** — authorable today |
| chart | 284 | **Epic 22** — the IR carries no structured chart data |
| card | 459 | **Epic 22** — the IR carries no per-family view models |
| chrome | 97 | **nothing — it NEVER empties.** Owner decision D2 + [ADR 0024](../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) keep C# composing nav + wayfinding + `<main>` permanently. These need a change of **provenance** (an owned sheet here), not deletion. |
| status | 91 | the **token bridge** — rules must not drift from the six `--status-*` tokens, and UX-DR17 is enforced by badge *shape*, so a partial re-author risks an a11y regression |
| other | 396 | **Epic 22** — uncategorized injected vocabulary |

**Do not "finish the retirement" by hand-copying monolith rules into components.** That is ADR 0018's explicitly
rejected alternative ("a second definition free to drift … it is not a migration, it is a rewrite"). The
remaining work is an **Epic 22 view-model ask**, recorded in
[ADR 0018 §Addendum](../docs/adrs/0018-transitional-ir-content-style-layer.md). **1,420** is the owner-visible
debt figure.

⚠️ **The harvest reads `trailingHtml` too.** It used to be `navHtml + wayfindingHtml + mainInnerHtml`, which
missed the region's post-`</main>` content — so `deep-analytics.html`'s `:target` lightbox rules were never
carried and the overlay rendered **permanently open** once the markup finally reached the IR. Any code that
reconstructs "the region" from its parts must use **all** the parts.

Attribute selectors deliberately do **not** bound the extraction. Nearly every one expresses runtime state
(`[data-ss-hierarchy-boot]`, `[data-hierarchy-ready]`, `[open]`) that is absent from server-rendered markup,
so requiring them drops the Hierarchy Explorer's anti-flash CSS — silently, with the page still rendering.

### The `.ir-content` scope cannot reach YOUR component — and when you need it to, see §10a

`.ir-content .pill` matches injected markup only. If a C# primitive emits a shared class that **your**
template also needs, §10a is the channel. Do not re-type the declarations.

---

## 10a. Shared primitive classes: the UNSCOPED `shared-primitives.css` layer

Ratified shape: [ADR 0029](../docs/adrs/0029-unscoped-shared-primitive-layer.md) (Proposed), which **amends
ADR 0018's scoping property**.

Some classes are **shared vocabulary** rather than injected content. `ListRow.Chip` (`src/SpecScribe/ListRow.cs`)
emits `class="list-row-chip pill"`, and every visual property of that chip — Courier, `0.03em` tracking,
`0.2rem 0.7rem`, the `999px` radius, `--warm-white`, `--ink-faded` — belongs to `.pill`. `ListRow.vue` is
template-authored, so §10's scoped layer can never reach it.

**What used to happen, and must not happen again.** `ListRow.vue` hand-retyped `.pill`'s declarations. It
drifted: serif instead of Courier, no letter-spacing, `0.1rem 0.55rem`, `--parchment`/`--ink-light`. Story
23.2's re-review corrected the values and found it could not remove the copy — deleting the properties left an
unstyled chip, because no channel existed. This layer is that channel, and the copy is now deleted.

```bash
npm run extract:ir-content   # writes BOTH ir-content.css and shared-primitives.css
npm run check:ir-content     # gates BOTH, and names which one drifted
```

**The allowlist is the boundary.** `SHARED_PRIMITIVES` in `scripts/ir-content-lib.mjs` — **two** entries
today: `pill` and `skip-link`. (`skip-link` was admitted by Story 23.2's third review pass, when the portal's
rule and `PageShell`'s scoped copy were found tying on specificity with the winner decided by chunk order;
ADR 0029's § Admissions table records it. This sentence said "one entry today" for a week afterwards — and
ADR 0029's § Consequences names exactly that failure: the containment property "erodes anyway if nobody
counts", so the count is load-bearing, not trivia.) A rule is carried only when **every** class its selector
names is on the list, so `.pill.status-draft`
and `.pill.pill-link` stay scoped. A shared rule is **removed** from `ir-content.css`, never duplicated into
both: an unscoped rule still matches inside `.ir-content`, so the app has exactly one definition.

**Adding to the list is an architectural decision.** The admission test, both halves required:

1. a C# primitive emits the class, **and**
2. a template-authored Vue component consumes it.

A class that appears only in injected markup is §10's, not this. If you are reaching for this list to avoid
writing a component's own styles, you want `<style scoped>` (§2) instead.

**Using it in a component.** Put the shared class on the element and write **none** of its look:

```vue
<span class="list-row-chip pill">{{ chip }}</span>

<style scoped>
/* Layout only. `.pill` supplies every visual property from the shared layer. */
.list-row-chip { flex-shrink: 0; }
</style>
```

Your scoped rule (`.list-row-chip[data-v-…]`, specificity `(0,2,0)`) outranks the unscoped `.pill` `(0,1,0)`,
so you can still override deliberately — but adding a *look* property back re-opens the drift. Change
`specscribe.css` and re-extract instead.

⚠️ **Known limit:** a variant of a shared class is not in this layer. `class="pill pill-link"` gets `.pill`'s
base look and **not** the link treatment, because `pill-link` is not on the allowlist. That is the
all-or-nothing rule behaving conservatively; fix it by adding `pill-link` deliberately, not by re-typing it.

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

↻ Re-measured at whole-site scale by Story 23.4: **0 `_payload.json` and 0 Nuxt runtime `<script>` tags across
all 1,469 IR routes.** Match against real `<script>` **tags**, never as a substring — several `code/**` pages
render source files that *mention* `_payload.json` and `window.__NUXT__` as prose, and a substring test failed
six of them. [ADR 0032](../docs/adrs/0032-csp-posture-after-the-projection-layer.md) rests on this measurement.

---

## 13. One family per OWNING TEMPLATER, and the classifier is a table

Ratified shape: Story 23.4 AC #1. Every IR path resolves to an `IrFamily` in `ir/families.ts`; the router
(`pages/[...path].vue`) maps that to a component through an exhaustive `Record<IrFamily, Component>`.

**Families are keyed to the C# templater that produces the markup, NOT to the path prefix.** One family per
prefix yields eleven near-identical wrappers, which is the wrong kind of honesty — what a family component can
legitimately own is the markup *vocabulary* its family injects, and that vocabulary comes from a templater. So
`adrs/`, `implementation-artifacts/`, `planning-artifacts/`, `specs/`, `readme.html` and `project-context.html`
are all `HtmlTemplater.BuildDocPage` ⇒ **one** `DocProseSurface`; and `timeline.html` groups with `commits/**`
because `TimelineTemplater` and `CommitDayTemplater` share the activity-list vocabulary, despite unrelated paths.

Two rules follow, and both exist to make a silent gap impossible:

- **The map is exhaustive over the union.** Adding a family to the classifier without giving it a component is a
  **type error**, not a page that quietly renders as `pass-through`.
- **Completeness is asserted against the REAL manifest** (`test/families.test.ts`): every page in the generated
  IR must resolve, and the `pass-through` bucket must be **empty**. A hand-written fixture would only ever prove
  the table matches itself. A pass-through renders correctly, links correctly and passes every other harness —
  it is invisible except to a deliberate count.

**Family components wrap `IrSurface`; they never duplicate it.** Head projection, region injection and chart boot
live there once. What a family adds is its classification and its own vocabulary contract.

## 14. The C# region contract: nav + wayfinding + `<main>` + trailing

Ratified shape: Story 23.4 AC #3, [ADR 0024](../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md).

C# no longer slices the region out of a rendered document — it **composes** it from each page's own `PageView`
at the write seam, and `splitContentRegion` inverts that into four parts:

```
region = navHtml + wayfindingHtml + <main …>mainInnerHtml</main> + trailingHtml
```

⚠️ **`trailingHtml` is not optional, and omitting it has broken the same page three times.**
`deep-analytics.html` emits a `:target` lightbox **after** `</main>` (a `:target` overlay must not sit inside the
region it overlays). The C# slicer truncated there, then the TS splitter did, then the CSS extractor's harvest
did — each independently, each silently. No harness can see it: `measure:parity` compares `<main>` regions only,
`check:links` treats a same-page `#fragment` as resolved, `check:a11y` has no opinion about a missing overlay. It
was found by opening the page in a browser and querying for `#coupling-zoom`.

**So: any code that reconstructs "the region" from its parts must use ALL the parts** — and `trailingHtml` must
render **outside** `<main>`, or it breaks both the overlay and the one-landmark a11y invariant.

Also note what the region does **not** carry, now enforced rather than observed: **no executable script.** Inert
`<script type="application/json">` data islands are fine (163 of them ship; the Hierarchy Explorer reads them),
but `IrSurface` **throws at build time** on an executable island rather than shipping a page that `v-html` would
render silently inert. ADR 0032 restates ADR 0005 §4 around this.

---

## 15. `check:links` gates against a PINNED BASELINE — and once could not fail at all

`npm run check:links` fails when the generated site gains a dangling internal link that is **not already in**
`web/measurements/links-baseline.json`. Re-pin with `npm run pin:links`, which rewrites that file as sorted
`"<page>\t<href>"` entries so a re-pin is a **reviewable diff naming the links**, not a count bump
(ADR 0033 §Decision 3).

**Why a baseline instead of "zero dangling links".** The site carries ~1,000 dangling internal links nothing in
Epic 23 caused: links to *source* files (`…/epics.md`) the portal never rewrites to their `.html` page, and a
renderer bug emitting **nested anchors** (`<a href="../../<a href="…">…</a>">`) from a link rewriter running
twice. A gate that failed on the absolute count would be red from its first run and stay red, which teaches
people to ignore it. The baseline is **accepted debt, not endorsement** — the gate reports baseline entries
that have started resolving so the list shrinks as the debt is paid.

**⚠️ The cautionary history, because it is the most useful thing in this section.** This gate originally
compared two trees and failed on a *regression*: a link that resolved in the golden site C# wrote and dangled
in the Nuxt output. Story 23.6 deleted the C# page writer, so both sides collapsed onto the same directory
(`goldenFiles = nuxtFiles = siteFiles`) — but the classifier and the exit condition were left untouched.
Because `scan()` is deterministic in `(root, files)`, the two link maps became *the same map*, the gating
bucket `!resolved && goldenResolved` reduced to `!x && x`, and **the gate could not fail for any number of
dangling links**. It reported every one of them as "inherited — not this story's" and exited 0. Nothing caught
it because the classifier lived inline in a script that only ran against a freshly generated 1,000-page site.

Two rules came out of that, and they generalize past this gate:

1. **When you delete one side of a comparison, re-derive what the comparison now means.** Removing the golden
   tree silently turned a difference gate into a tautology. The `(skipped)`-style symptom was that everything
   still looked green.
2. **A classifier that cannot be called with two hand-written inputs cannot be tested, and will rot.**
   `scan()` and the classifier now live in `scripts/links-lib.mjs`, shared by the gate and `pin:links` so the
   two cannot disagree about what "dangling" means, and `test/links-lib.test.mjs` pins the exact bucket that
   became unreachable.

**One honest limitation.** Unlike `check:parity`, this gate **cannot be frozen** — a link check has to run over
the live site, so a sibling story that adds a page with a broken link *will* turn it red. That is the gate
working, but a red run here is not automatically your bug: the failure names the page, so check whose surface
it is before assuming.

---

## Status of this app

**Corrected 2026-08-07.** This section used to read *"`web/` is production-intent but **not shipped yet**: it
is not in `SpecScribe.slnx` and not wired into `specscribe generate`."* The first half is still true; **the
second is not, and has not been since ADR 0034.** `web/` **is** the shipping path:

- `src/SpecScribe/NuxtPrerender.cs` boots `web/.output/` (or `renderer/` beside the executable) as part of
  `specscribe generate`.
- Per [ADR 0034](../docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md), **no C# code path emits a content
  `.html`** any more — `SiteGenerator.WritePage` says so in as many words. Node renders every content page,
  so Node is a hard prerequisite of `generate`, not an optional accelerator.
- `web/` is still absent from `SpecScribe.slnx`, which is a build-system fact, not a shipping one.

The practical consequence for a reader of this document: it is not a description of a side experiment. It is
the authority for the code that renders every page a user sees.

`spike/nuxt-ir/` remains the throwaway 23.1 probe — read it for context, but its token binding (wholesale
`specscribe.css` import) is superseded by the extraction here.

### Retired surfaces

- **`pages/design-system.vue` — retired 2026-08-07** (Story 23.2 third review pass). It was never in the
  `PACKAGE_BUILD` prerender list, so no user could reach it, while it duplicated — and had begun to
  contradict — the `design-system.html` page rendered from the C#-composed region. Its `:deep()` worked
  example moved to §3; its status vocabulary was a showcase fixture, and `StatusStyles` remains the source of
  truth. AC #6's "a parallel Nuxt route becomes the portal's design-system surface" is therefore **withdrawn
  rather than pending** — ADR 0034 made the C#-composed region the thing Node renders, so there was no
  convergence left to wait for.
