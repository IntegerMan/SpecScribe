# ADR 0029: A Bounded Unscoped Layer for Shared Primitive Classes

**Status:** **Accepted** 2026-08-07 (authored 2026-07-29 by Story 23.2's re-review follow-up; ratified by the
owner at Story 23.2's fourth review pass)
**Date:** 2026-07-29 (authored) · 2026-08-07 (ratified; § Admissions has moved since authoring — the
allowlist grew to two on 2026-08-07, before ratification)
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0018](0018-transitional-ir-content-style-layer.md) — specifically its property **3 (Scoped)**, which states that *every* rule in the generated layer is nested under `.ir-content` so it "cannot reach a template-authored component even by accident". This ADR carves out a bounded, enumerated exception and leaves the other three properties intact.
**Relates to:** [ADR 0009](0009-frontend-framework-for-projection-layer.md) (the projection layer); [ADR 0016](0016-ir-carries-rendered-prose-html.md) (why injected markup exists); [ADR 0026](0026-generated-layers-derive-from-templates-not-project-data.md) (generated layers derive from templates, not project data — the allowlist here is a template-side constant, not harvested from a run); AD-7 in ARCHITECTURE-SPINE.md; Story 23.2 (the token bridge and the component library), Story 23.4 (retires both layers)

## Context

ADR 0018 gave the Nuxt app a generated, bounded, scoped stylesheet for **injected** IR content. Scoping was the property that made it safe: a monolith-derived rule nested under `.ir-content` cannot leak into the app's own components.

Story 23.2's component library then hit the case that property forbids. `ListRow.Chip` (`src/SpecScribe/ListRow.cs`) emits:

```html
<span class="list-row-chip pill">Epic 1</span>
```

Every visual property of that chip — Courier, `0.03em` tracking, `0.2rem 0.7rem`, the `999px` radius, `--warm-white`, `--ink-faded` — comes from `.pill` in `specscribe.css`. `.pill` is **shared vocabulary**: it is used by ADR status chips, the sprint board, list rows, and roughly a dozen other surfaces.

`ListRow.vue` is the Vue counterpart of that primitive, and it is **template-authored**. So:

- the scoped layer emits `.ir-content .pill`, which matches injected markup and **never** a template-authored component; and
- the app imports only `tokens.css`, `base.css` and the generated sheets — never the monolith.

That left one option inside the existing rules, and the code took it: hand-retype `.pill`'s declarations inside the SFC. It drifted exactly as a second definition does. Story 23.2's 2026-07-28 re-review found the Vue chip declaring **serif** instead of Courier, no letter-spacing, `0.1rem 0.55rem` instead of `0.2rem 0.7rem`, and `--parchment`/`--ink-light` instead of `--warm-white`/`--ink-faded` — inside a file whose own header calls itself "the Vue counterpart of `ListRow.Render`". The review corrected the **values** and restored the `pill` class to the element, then recorded what it could not fix:

> Dropping the properties in favour of the class alone ships an unstyled chip (confirmed before reverting). There is no channel for shared non-IR primitive classes.

Deleting the copy required a channel that did not exist. This ADR is that channel. The owner chose it on 2026-07-29 over accepting the copy until 23.4, and over inverting the source of truth so `web/` owns the primitives' CSS (which would reverse Story 23.2's locked decision while the C# renderer still writes every page — that inversion belongs to Story 23.4 if it happens at all).

## Decision

**`npm run extract:ir-content` emits a second sheet, `web/assets/shared-primitives.css`, whose rules are UNSCOPED — bounded by an explicit allowlist rather than by usage.**

The three properties of ADR 0018 that are not scoping are *strengthened*, not relaxed:

**1. Bounded by an ALLOWLIST, not by usage.** `SHARED_PRIMITIVES` in `web/scripts/ir-content-lib.mjs` is a hand-authored constant — **two entries: `pill` and `skip-link`** (see § Admissions). Nothing enters this layer by being used somewhere; it enters only by being named. A rule is carried **only when every class it names is on the allowlist**, so `.pill.status-draft` and `.pill.pill-link` stay in the scoped layer where they belong: those are IR content's variants, not shared component vocabulary. The bound is therefore *tighter* than the scoped layer's, which is usage-driven.

**2. Generated and gated in both directions, by the same run.** `npm run check:ir-content` re-derives and diffs **both** sheets and the manifest, and names which sheet drifted. Demonstrated red on a hand-edited shared rule and on an absent sheet, and green again.

**3. UNSCOPED — the amendment.** Rules are emitted verbatim, at document scope, so a template-authored component can use the class. A rule inside a conditional at-rule keeps its condition.

**4. Enumerated, from both sides.** The manifest gains a `sharedPrimitives` block naming every rule that moved, the allowlist itself, and the admission test. The rule *also* stays in the scoped layer's `rules` list with `carried: false` and a reason pointing at the new block — so a rule that moved does not silently drop off the list Story 23.4 retires.

**Exactly one definition.** A rule that lands in the shared layer is **removed** from the scoped layer rather than duplicated into both. An unscoped `.pill` still matches inside `.ir-content`, so injected markup is unaffected — and the app now has one definition of `.pill` where it previously had two (one generated, one hand-typed).

### The admission test

A class qualifies **only** if:

1. a C# primitive emits it, **and**
2. a template-authored Vue component consumes it.

A class that appears only in injected markup is already covered by the scoped layer and **must not** be added. Growing this list is an architectural decision, not a convenience — which is why the list lives next to this reasoning and is published in the manifest.

### Admissions

| class | admitted | why it passed the test |
| --- | --- | --- |
| `pill` | 2026-07-29, Story 23.2 review follow-up | `ListRow.Chip` emits `class="list-row-chip pill"`; `ListRow.vue` renders the same chip. The hand-typed second definition was deleted rather than corrected again. |
| `skip-link` | 2026-08-07, Story 23.2 third review pass | `PageShell.vue` renders a template-authored `<a class="skip-link">` while the C# chrome emits the same class into injected markup — both halves satisfied by construction. |

`skip-link` was admitted to **fix a live defect**, not for tidiness, and the defect is worth recording because it is the first one this layer's absence actually caused. `PageShell.vue` carried its own scoped `.skip-link`. `IrSurface.vue` puts `class="ir-content"` on PageShell's **own root element**, so the generated `.ir-content .skip-link` (0,2,0) and the scoped `.skip-link[data-v-…]` (0,2,0) tied, and the winner was decided by Vite's chunk ordering rather than by anything in the code. Both outcomes broke UX-DR16 on every IR-backed route:

- PageShell wins → its `z-index: 10` sits beneath `.ir-content .site-nav`'s `z-index: 100` (sticky, opaque), so the focused skip link renders *behind the nav bar*;
- the generated rule wins → `position: absolute` returns, and with no positioned ancestor the offsets resolve against the **document**, so a keyboard user who has scrolled focuses a link rendered off-screen at the top of the page.

Admitting the class collapses both to one unscoped definition and **removes** the scoped `.ir-content .skip-link` from `ir-content.css`, so there is no longer a pair to tie. The source rule in `specscribe.css` was corrected from `absolute` to `fixed` in the same change, which is what stops the promotion from regressing the fix the 2026-07-28 re-review had made in `PageShell.vue`. Note the direction of the lesson: the cascade argument in § Cascade order below reasons about a component's scoped rule *outranking* an unscoped primitive, and that reasoning is sound — but it does not cover the case where the scope class is applied *to* the component's root, which turns a descendant selector into a competitor at equal specificity.

### Cascade order

`nuxt.config.ts` imports `shared-primitives.css` **before** `ir-content.css`. The cascade already agrees without relying on order — every `.ir-content …` selector is at least `(0,2,0)` against an unscoped primitive's `(0,1,0)`, and a component's own scoped rule (`.list-row-chip[data-v-…]`, `(0,2,0)`) also outranks it — but source order settles any future tie in favour of the scoped layer, and it is what lets `ListRow.vue` keep `flex-shrink` while inheriting the shared look.

## Consequences

**Good.**

- The hand-typed copy of `.pill` is **deleted**, not merely corrected. The drift class is closed rather than patched, which is the difference between the 2026-07-28 fix and this one.
- Shared vocabulary now crosses the C#/Vue boundary the same way tokens do: generated, verbatim, gated. A component author cannot get `.pill` wrong by re-typing it, because there is nothing to re-type.
- The exception is countable and visible from both directions in one manifest.

**Bad, and accepted.**

- **ADR 0018's containment property is no longer absolute.** An unscoped rule can, by construction, reach any element carrying that class anywhere in the app. The mitigation is that the set is a published, gated allowlist of **two** — not that the risk is absent. This is a real reduction in a property that was previously guaranteed, and the list has now grown once, which is the thing to watch: each admission is individually defensible and the property erodes anyway if nobody counts.
- Two generated CSS layers become three sheets total (plus the token bridge). Story 23.4 retires both derived layers, so the count is transitional.
- A variant of a shared class is reachable only inside `.ir-content`: `.pill.pill-link` stays scoped, so a Vue component writing `class="pill pill-link"` would get the base look and not the link treatment. That is the all-or-nothing rule behaving correctly and conservatively; the fix, if it is ever needed, is to add `pill-link` to the allowlist deliberately.

**Neutral.**

- The layer is transitional on the same schedule as ADR 0018's: when the C# renderer stops writing pages, `web/` owns authored styles and both derived sheets are deleted.

## Alternatives considered

**Accept the copy until Story 23.4.** Rejected by the owner. It leaves a hand-typed second definition of shared vocabulary in place for the remainder of the epic, having just been caught drifting once — and the correction had no gate to keep it correct.

**Invert the source of truth: `web/` owns the primitives' CSS and the portal's is generated from it.** Rejected *for now*. It is plausibly the end state, but it reverses Story 23.2's locked owner decision (the C# stylesheet is the single source per AD-7) while the C# renderer still writes all ~1,400 pages. It belongs to Story 23.4, after the renderer stops.

**Emit the shared rules into `ir-content.css` as an unscoped section.** Rejected: that sheet's banner and its ADR both promise every rule is scoped. A separate file whose own banner leads with "⚠️ UNSCOPED" cannot be misread by someone opening it.

**Duplicate `.pill` into both layers (scoped and unscoped).** Rejected: the entire point is to stop having two definitions. An unscoped rule already reaches injected markup, so the scoped copy would be redundant as well as duplicative.

**Hand-author a `:deep()` or `:global()` rule in `ListRow.vue`.** Rejected: `:global(.pill) { … }` still requires typing `.pill`'s declarations by hand, which is the second definition this ADR removes. `CONVENTIONS.md` §6 also forbids per-SFC escapes of exactly this kind.

## Note on ADR 0018's property 4

ADR 0018 describes the manifest as naming every carried rule "with its line span in the source stylesheet". **Line spans were removed from the committed manifest by Story 23.5**, because they moved on any unrelated `specscribe.css` edit and reddened CI with an 865-line diff while the generated sheet stayed byte-identical (the same committed-fields rule this project applies elsewhere). Rules are identified by `selector` plus `within`. That clause in 0018 is stale and is corrected here rather than left to drift, since ratifying these two ADRs together is now one decision.
