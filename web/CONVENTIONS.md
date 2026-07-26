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

`assets/tokens.css` is a **verbatim extraction** of the `:root` block from
`src/SpecScribe/assets/specscribe.css`. That C# stylesheet is the single source of truth for SpecScribe's
presentation tokens (AD-7). The extracted file is a copy, never a second definition.

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
through `ListRow` + `StatusBadge`) and differ only in how the data reaches the component. Measured with
`npm run generate && npm run measure:payload` on Nuxt 3.21.9 / Vue 3.5.40 / Node 24.11.1:

| variant | HTML | payload | island JSON | total | vs control |
| --- | --- | --- | --- | --- | --- |
| A — `useAsyncData` | 125.5 KB | 44.5 KB | — | **170.0 KB** | 1.36× |
| B — `.server.vue` island | 125.1 KB | 3.1 KB | 121.8 KB | **250.0 KB** | 1.99× |
| C — build-time (control) | 125.4 KB | 0.1 KB | — | **125.4 KB** | 1.00× |

**The server-component shape lost, and it lost badly.** The 23.1 spike hypothesised that `<NuxtIsland>` would
avoid the hydration-payload duplication behind its measured 2.26× site weight. It does drain the route's
`_payload.json` (44.5 KB → 3.1 KB), but it then emits the island's **entire rendered HTML and its scoped CSS a
second time** into `__nuxt_island/<Component>_<hash>.json` so the client can re-fetch it. For content that is
static once prerendered, that is a payload *amplifier*: 1.99× against 1.36× for the thing it was supposed to
beat.

**Recommendation for 23.3: resolve IR data at build time, at module scope, with no data composable.** That is
variant C, it costs essentially nothing (0.1 KB of payload for a 125 KB page), and it is the shape 23.2's own
primitives already use. The IR is available at build time by construction — there is no reason for it to
arrive through a composable that exists to serve runtime fetching.

Two caveats, so the recommendation is not over-generalised:

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

## Status of this app

`web/` is production-intent but **not shipped yet**: it is not in `SpecScribe.slnx` and not wired into
`specscribe generate`. How the Node build reaches users is Story 23.5's decision, sequenced ahead of 23.4
because it is Epic 23's load-bearing unknown. `spike/nuxt-ir/` remains the throwaway 23.1 probe — read it for
context, but its token binding (wholesale `specscribe.css` import) is superseded by the extraction here.
