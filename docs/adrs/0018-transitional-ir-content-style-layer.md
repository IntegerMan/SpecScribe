# ADR 0018: A Generated, Bounded, Self-Retiring Style Layer for Injected IR Content

**Status:** Proposed (authored 2026-07-27 by Story 23.3; ratification is the owner's)
**Date:** 2026-07-27
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0009](0009-frontend-framework-for-projection-layer.md) (the projection layer this styles); [ADR 0016](0016-ir-carries-rendered-prose-html.md) (the reason injected markup exists at all); [ADR 0010](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) §zero-dependency posture (why the extractor is hand-written); AD-7 in ARCHITECTURE-SPINE.md (SpecScribe owns content-semantic tokens); Story 23.2 (the token bridge this sits beside), Stories 23.3 and 23.4 (the story that retires it)

## Context

Story 23.2 made a deliberate decision that the Nuxt app imports **only** a generated token bridge from the C# side — never `specscribe.css` wholesale. The reasoning was concrete: that stylesheet is 7,041 lines, and it has already had a single mistyped comment close early and take ~1,000 rules with it, invisible to the entire test suite. Scoped component styles plus a thin token layer is the trade Epic 23 is making.

ADR 0016 then settled that the IR carries **whole rendered prose HTML**. Those strings are markup authored against exactly the monolith 23.2 walked away from. So Story 23.3 arrived at a genuine conflict:

- import the monolith and reverse 23.2's central decision in one line; or
- hand-author `:deep()` rules for ~7,000 lines of styling, which is a rewrite disguised as a migration; or
- ship migrated pages that are structurally correct and visually bare.

The 23.1 spike took the first option, which is part of why its result did not transfer. None of the three is acceptable as a standing position.

## Decision

**A fourth option: a second GENERATED bridge, beside the token bridge, with four properties that together make it a transition rather than a re-import.**

`web/assets/ir-content.css` is produced by `npm run extract:ir-content` from `src/SpecScribe/assets/specscribe.css`, and it is:

**1. Bounded by measured usage.** Only rules whose selectors are actually exercised by the markup of the **migrated** surface families are carried. Class names and ids bind the extraction; attribute selectors deliberately do not, because nearly every one of them expresses *runtime state* (`[data-ss-hierarchy-boot]`, `[data-hierarchy-ready]`, `[open]`) that by definition is absent from server-rendered markup — requiring them would silently drop the interaction CSS the Hierarchy Explorer's anti-flash handshake depends on. On this repo the result is **897 rules + 4 keyframes, 62 % smaller than the source**.

**2. Generated, never hand-authored, and gated in both directions.** `npm run check:ir-content` re-derives both the sheet and its manifest and fails on any divergence — a stale extraction, a hand-edited rule, or a source-side change nobody re-extracted. Story 23.3 demonstrated it red in three directions (file absent, rule hand-edited, source rule changed) and green again, because a gate only ever seen passing is not a gate.

**3. Scoped under the injecting wrapper.** Every rule is re-nested under `.ir-content`, with root-anchored selectors keeping their root part (`:root[data-ss-hierarchy-boot] .ir-content .chart-panel …`) so state selectors still work. Monolith rules therefore cannot reach a template-authored component even by accident — the containment property scoped components exist to provide is preserved for everything else in the app.

**4. Enumerated.** `web/assets/ir-content.manifest.json` names every source rule carried, with its line span in the source stylesheet. That list **is** the surface Story 23.4 has to retire. Implied debt is debt nobody pays.

**The extractor is hand-written against Node built-ins.** No npm CSS parser: `web/` runs on `nuxt` + `vue` + `vue-router` and the vendored Plotly build, and that zero-dependency posture is a deliberate project property (ADR 0010), not an accident to be spent on convenience.

**This layer is transitional and Story 23.4 owns its retirement.** It exists because injected markup exists. As surfaces stop being injected and become real components, the manifest shrinks; when it is empty, the layer and its gate are deleted.

## Consequences

**Good.**

- Migrated surfaces render correctly without the monolith, and 23.2's decision stands: the app still imports no hand-written copy of `specscribe.css`, and no value in it is re-typed.
- The blast radius is bounded by construction. A rule in this layer cannot affect a template-authored component, which is the property a wholesale import gives away.
- The debt is countable. "How much monolith is left?" has a number, and the number only moves when the generator says so.

**Bad, and accepted.**

- It **is** monolith-derived CSS, and pretending otherwise would be dishonest. The app now carries two generated bridges rather than one.
- Extraction is usage-driven, so a rule reachable only through markup no migrated page currently emits will be missing. The failure mode is an unstyled element, not an error — mitigated by regenerating whenever the surfaces change, and by the gate that fails when someone forgets.
- Pass-through pages get only whatever the migrated families already paid for (**48 %** of the classes they use, on this repo). That is reported as a number by the extractor rather than left implied, and it is the correct posture: those pages are Story 23.4's, and this story does not claim them.

**Neutral.**

- Source comments are stripped rather than carried. That removes the `*`+`/`-inside-a-comment hazard from the generated output by construction, and it means the generated sheet is not a place to look for the source's reasoning.

## Alternatives considered

**Import `specscribe.css` wholesale** (the 23.1 spike's shape). Rejected: it reverses ADR-level intent from Story 23.2 and keeps alive the fragility class Epic 23 exists to end.

**Hand-author `:deep()` rules per injecting component.** Rejected: ~7,000 lines of styling re-typed by hand is a second definition free to drift from the portal's own, which is the drift this epic exists to end — and it is not a migration, it is a rewrite.

**Ship unstyled and fix it in 23.4.** Rejected: a migration whose output cannot be looked at cannot be verified by the owner, and this project's verification gate is the owner looking at the rendered page.

**A PostCSS/CSS-parser dependency to do the extraction properly.** Rejected on ADR 0010's zero-dependency posture. Revisit if the extractor's hand-written selector handling starts producing wrong output rather than merely conservative output.
