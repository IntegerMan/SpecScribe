# ADR 0039: A Second Bounded Unscoped Layer, for Runtime-Attached Body-Level Classes

**Status:** Proposed (authored 2026-08-06 from the owner's verify round on the sunburst surfaces; ratification is the owner's)
**Date:** 2026-08-06
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0029](0029-unscoped-shared-primitive-layer.md) — it carved ONE bounded exception to ADR 0018's property **3 (Scoped)** and drew its admission test around shared *markup* vocabulary. This ADR adds a SECOND exception, with a different admission test, for classes that have no markup end at all.
**Relates to:** [ADR 0018](0018-transitional-ir-content-style-layer.md) (the scoped layer and its four properties); [ADR 0026](0026-generated-layers-derive-from-templates-not-project-data.md) (generated layers derive from templates, not project data — both allowlists are template-side constants); [ADR 0033](0033-content-drift-gates-are-targeted-and-regenerable.md) (a new gate must localize failure to a named artifact); [ADR 0034](0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) (retiring the C# writer is what made this reachable); [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (the Hierarchy Explorer, whose tooltip this is)

## Context

ADR 0018's property 3 says every rule in the generated layer is nested under `.ir-content`, so a monolith-derived rule cannot reach a template-authored component. ADR 0029 found the first case that property forbids — `.pill`, shared vocabulary a C# primitive emits and a Vue component consumes — and carved a bounded, allowlisted, unscoped layer for it.

There is a second case, and it is not a widening of the first. It has no markup end on either side.

`specscribe.js` builds the portal's shared tooltip at runtime:

```js
tip = document.createElement("div");
tip.className = "ss-tooltip";
document.body.appendChild(tip);            // <-- document.body, not the content wrapper
```

`document.body` is **outside** the `.ir-content` wrapper. So `.ir-content .ss-tooltip` — which is what the extractor emits — can never match that node, no matter what the harvest saw or how the seed lists are tuned. The same is true of the two rich cards rendered into it as `innerHTML`: the Hierarchy Explorer's (`ss-hierarchy-card*`, built by `tipCardFor`) and the Code Map's (`codemap-card*`, built server-side by `Charts.BuildTreemapCard`).

**What this cost, and why it went unnoticed.** Before [ADR 0034](0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) the portal was written by the C# renderer, which links `specscribe.css` whole — every rule present, nothing scoped, tooltip fine. Retiring that writer made the Nuxt-rendered pages the product, and those load only `tokens.css`, `base.css`, `shared-primitives.css` and `ir-content.css`. `sync-runtime-assets.mjs` deliberately never ships `specscribe.css` ("serving the 7,041-line monolith would reverse 23.2's central decision in one line"). So on 2026-08-06 the owner's verify round found the sunburst had no hover tooltip at all: the tip node was still built, still filled with the right card markup, still shown on hover — and completely unstyled. Verified across every commit in the file's history: these rules have **never** been in `ir-content.css`. This did not regress from an edit; it regressed from the renderer swap, silently, with every gate green.

**Why the harvest is not the frame to reason in.** The obvious reading is "the harvest can't see runtime classes, so seed them" — and for one of the two families that is wrong. `codemap-card*` **is** harvested: its markup is server-built into the `data-tip-html` attribute, so the extractor found it and carried its rules into the scoped layer perfectly happily. They were dead there anyway, because the markup they style is only ever `innerHTML`'d into a body-level node. A class can be fully visible to the harvest and still need to be unscoped. **Containment is the question, not visibility.**

The converse also holds, and bounds this ADR. The Hierarchy Explorer stamps plenty of *other* classes at runtime — `ss-hierarchy-sector` on every Plotly sector, `ss-hierarchy-probe` on the colour probe, `ss-hierarchy-sw` on the legend swatches, `ss-hierarchy-crumb` on the breadcrumb, and `is-related-current` on the details rail's matching card. Every one of those nodes is a descendant of `.ir-content`. They were dropped for a different reason (the harvest genuinely cannot see them) and they need a different fix (seeding into `CONDITIONAL_CLASSES`, where they now are). **"Runtime-applied" is not the test either.**

## Decision

**1. A second bounded unscoped layer exists: `RUNTIME_BODY_CLASSES`, emitted to `web/assets/runtime-body.css`.**

Its members' rules are emitted verbatim, never nested under `.ir-content`, and — exactly as with ADR 0029 — **removed** from the scoped layer rather than duplicated into it, so the app keeps exactly one definition of each.

**2. The admission test is CONTAINMENT, not visibility and not provenance.**

> Is this class only ever applied to a node that is provably **outside** `.ir-content`?

Neither "the harvest cannot see it" (`codemap-card*` is seen and still qualifies) nor "JavaScript applies it" (`ss-hierarchy-sector` is applied by JS and does **not** qualify) is sufficient. A class that fails this test and is merely invisible to the harvest belongs in `CONDITIONAL_CLASSES`, in the scoped layer, where it already has a home.

**3. The two allowlists stay separate, and disjoint.**

They are not merged into one "unscoped" list. They answer different questions and admit on different tests, and merging them would let a reviewer approve a `.pill`-shaped addition while silently widening the tooltip escape hatch, or the reverse. Disjointness is guard-tested rather than asserted: if a class appeared on both, the builder's three-way partition would emit its rule into whichever predicate ran first — an order-dependent, silent choice that would quietly end "exactly one definition".

**4. Containment is preserved by the same all-or-nothing rule ADR 0029 uses.** A rule is carried only when **every** class its selector names is on the allowlist, and it may name no id. `.ss-tooltip .related-card` therefore stays scoped: one of its classes is page content, and a selector reaching from the tip node back into the page is exactly what must not escape.

**5. Re-parenting the tooltip was considered and REJECTED.** The cheaper-looking fix is to move the node under `.ir-content` and keep one unscoped layer — which is precisely what the sunburst-black-fill fix did for the colour probe, so the precedent is real. It does not transfer:

- `.ss-tooltip` is `position: absolute; z-index: 300` **so that** it layers above the sticky nav and clamps to the viewport instead of being clipped by whatever ancestor it would otherwise sit inside. The stylesheet says so where the rule is defined, and the code map's treemap card depends on it: a tooltip inside a scrolling chart panel is cut off by it.
- `specscribe.js` computes the tip's coordinates in **page** space, adding `scrollX`/`scrollY`, on the assumption of a body-positioned ancestor.

The probe host has neither property — it is a hidden, zero-size node whose only job is to be read by `getComputedStyle`, so moving it cost nothing. Moving the tooltip would trade a styling bug for a positioning-and-clipping bug, and would do it on the one surface (the code map) where the clipping is worst.

**6. Drops become reportable.** `selectorIsUsed` discards a rule whose classes were never harvested and records nothing anywhere — no manifest entry, no log line, no failing gate. That silence is now this layer's signature failure: the sunburst's black fills, `owner-author-2`, the Code Map's id-bearing spec/test filter, and this tooltip/details-rail loss all shipped that way. `extract:ir-content` now reports every dropped selector with the token that caused it, ranked by how many rules one absent token took down, to `web/measurements/ir-content-drops.json` and the console.

This is deliberately **reported, not gated**. It is a function of the whole source stylesheet, so committing it to `ir-content.manifest.json` would redden CI on any `specscribe.css` edit that cannot possibly have moved the emitted layer — the exact failure the manifest's committed-fields rule already exists to prevent, and the one that teaches people to re-run the extractor on reflex.

**7. The derivation is pinned by unit test, not by the round-trip gate.** `check:ir-content` re-derives through the same `harvest` / `selectorIsUsed` / seed-list code the extractor uses, so a rule wrongly dropped is dropped identically on both sides and the diff is empty. It cannot catch a bug in its own derivation — it stayed green through every incident above. `web/test/ir-content-harvest.test.mjs` asserts on the functions' output directly, including both directions of the details-rail reveal rule, so "the seed is what makes this survive" is proven rather than assumed.

## Consequences

**Positive**
- Restores the portal's one tooltip system on every surface that uses it — the Hierarchy Explorer and the Code Map — and the dashboard's details rail, which had been going blank on selection.
- Names the real distinction (containment) rather than the apparent one (harvest visibility), so the next case is classified correctly instead of by analogy to whichever incident is freshest.
- Turns this layer's signature silent failure into a line a human reads at extraction time.
- `runtime-body.css` is its own artifact with its own gate line, so a failure localizes to a named file ([ADR 0033](0033-content-drift-gates-are-targeted-and-regenerable.md)).

**Negative / trade-offs**
- **A second unscoped layer is a genuinely weaker containment story than one.** ADR 0018's property 3 now has two carve-outs rather than one. Accepted because the alternative is not "one exception" but "a body-level node that cannot be styled at all".
- **Two allowlists are two things to keep bounded**, and their disjointness is now a property something has to check. It is guard-tested, but it is real surface area that did not exist before.
- A fourth generated stylesheet in `nuxt.config.ts`'s `css` array, and a fourth artifact for Story 23.4's eventual retirement to account for.
- The admission test needs a human to answer "provably outside `.ir-content`" — it is not mechanically checkable. The guard tests bound the *shape* of the list, not the *judgment* that put a class on it.

## Options considered

| Option | Verdict |
|---|---|
| **Second bounded unscoped layer, containment as the admission test** | **Chosen.** Keeps the tooltip's deliberate body-level placement, keeps one definition per rule, and names the distinction that actually separates the cases. |
| Re-parent the tooltip under `.ir-content` | **Rejected** — Decision 5. Trades a styling bug for a clipping and positioning bug, worst on the surface with the most clipping. |
| Add the classes to `SHARED_PRIMITIVES` | **Rejected.** They fail its admission test outright (no Vue component consumes them), and that list's own comment forbids growth beyond it. Widening it by fiat would leave the project with one list and no test. |
| Ship `specscribe.css` to the Nuxt app | **Rejected.** Reverses Story 23.2's central decision and ADR 0018 wholesale to fix five rules, and `sync-runtime-assets.mjs` already refuses it in as many words. |
| Hand-author the tooltip's CSS in `web/` | **Rejected** — ADR 0018's explicitly rejected alternative: a second definition free to drift, which is the failure `.pill` already demonstrated. |

## References
- The rule this amends: [ADR 0029](0029-unscoped-shared-primitive-layer.md), and through it ADR 0018 property 3.
- What made it reachable: [ADR 0034](0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) — the Nuxt-rendered pages became the product.
- The precedent that does not transfer: the sunburst-black-fill fix, which moved the colour probe under the content wrapper (`specscribe.js`, `tokenFor`'s probe host).
- The component whose tooltip this is: [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §2, "one text twin / one framing block" — one tooltip system site-wide is the same invariant.
