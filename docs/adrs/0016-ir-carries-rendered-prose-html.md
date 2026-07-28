# ADR 0016: The Canonical IR Carries Rendered Prose HTML — Amending ADR 0008 §Decision 1

**Status:** Proposed (authored 2026-07-26 by Story 22.2; ratification is the owner's)
**Date:** 2026-07-26
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0008 — JSON IR Canonical and Incremental Generation](0008-json-ir-canonical-and-incremental-generation.md) (**amends §Decision 1 and §Decision 4**); [ADR 0013](0013-text-twin-is-the-no-js-contract.md) §5 (already amended the *chart* half of the same clause — this ADR amends the *prose* half); [ADR 0009](0009-frontend-framework-for-projection-layer.md) (the Epic 23 consumer that depends on the outcome); [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) (the view-model contract this does **not** replace); [ADR 0024 — The SPA and the Webview Are Filtered Projections of ONE Region Seam](0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) (**exercises §Decision 4's grant** — Story 22.4 retired the duplicate builder, NOT the slicers; and its one-region-shape fix triggers **§Decision 5**'s `schemaVersion` bump, 1 → 2); Epic 22 (Stories 22.2–22.6), Epic 23 (Stories 23.1, 23.3, 23.4)

## Context

ADR 0008 §Decision 1 defines the canonical IR as carrying:

> AD-2's host-neutral view models plus **pre-rendered SVG chart fragments**.

Two spikes have since measured that clause, and each found half of it wrong.

**[ADR 0013](0013-text-twin-is-the-no-js-contract.md) §5 (ratified 2026-07-24) already amended the chart half.** Under ADR 0012 the hierarchy charts render client-side only, so there is no pre-rendered SVG left to carry; the IR carries chart **data and component configuration**, plus the server-rendered **text twin** that ADR 0013 §2 makes contract. That amendment is ratified and is not reopened here.

**The prose half is still unamended, and Story 23.1 measured that it is load-bearing.** The Nuxt feasibility spike consumed the shipped `SpaDelivery` output as a proxy IR and rendered four real surfaces from it. Its headline finding — that the ~889 LOC of custom Markdig renderers (reference chips, comment annotations, swatches, gherkin blocks, capability blocks) are a **non-risk** for Epic 23 — was proved by `v-html`-injecting the IR's content strings and measuring `<main>` **byte-identical** to the static golden on three of four surfaces, with zero re-serialization, attribute reordering, or self-closing-tag rewriting.

That result holds **only because the proxy IR carried whole rendered HTML**. The spike's own code review narrowed AC #2 to say so explicitly and escalated it to a hard requirement on Story 22.2. Building the IR to ADR 0008's literal wording instead would:

- **Discard the rendered prose**, reviving in full the custom-renderer fidelity risk ADR 0009 named and 23.1 measured away — every custom Markdig renderer would have to be re-implemented in Vue and kept byte-faithful forever.
- **Pull a ~4,691 LOC templater reimplementation into Epic 23's scope**, which ADR 0009 assumed it could avoid.

So the two ADRs currently disagree with the code, with each other's premises, and with the only empirical evidence anyone has gathered. Story 22.2 ships the IR that resolves it, and CLAUDE.md's ADR-trigger rule says a cross-cutting contract change must not stay buried in a spike report.

## Decision

**1. ADR 0008 §Decision 1 is amended.** The canonical IR carries, per page:

- **Markdig-rendered prose HTML as strings** — the page's own rendered content region (nav markup + breadcrumb + `<main id="main-content">`), produced by the C# core and travelling verbatim. **Not** re-modelled view models.
- **Chart data and component configuration**, plus the server-rendered **text twin** — per ADR 0013 §5 and §2, unchanged by this ADR and restated here only so §Decision 1 reads as one coherent clause.
- **Structured addressing and projection metadata** alongside the content: schema version, page index, nav graph, breadcrumb/drill parent-child graph, a head/meta projection, a declaration of embedded script islands, and a per-page content hash + byte size.

**2. AD-2's host-neutral view models are NOT retired.** They remain the shared rendering core's internal contract and the thing every adapter renders *from* (ADR 0002 stands, AD-1/AD-2 stand). What changes is only what the IR **transports**: the rendered output of that contract rather than a second, serialized copy of its inputs. `SectionViewModelSerializationTests` keeps proving the view models are plain data; Story 22.2's `CanonicalIrSerializationTests` proves the same of the IR document itself.

**3. ADR 0008 §Decision 4's "charts stay pre-rendered SVG inside the IR" is superseded** — by ADR 0013 §5, restated here because §Decision 4 was not edited when §Decision 1's chart half was amended. The rest of §Decision 4 stands: this does **not** reopen the C#→TS core port, and ADR 0006 is untouched.

**4. The IR is `spa/` promoted in place.** `spa/manifest.json` + `spa/pages-*.json` **are** the canonical IR — extended, not replaced. There is no separate `ir/` directory and no second capture path, so a fidelity defect can only ever exist in one place. Renaming the directory, and retiring any now-duplicate data path, is Story 22.4's call and deliberately not made here.

**5. The IR is versioned, with a monotonically increasing integer.** `schemaVersion` bumps on a breaking change to the manifest or chunk shape (a removed or renamed field, a changed type, a changed meaning, a change to how a content region is delimited); an additive field does not bump it. The pre-22.2 unversioned form is version 0 by implication.

## Consequences

**Positive**
- Story 23.1's central finding survives: the custom Markdig renderers stay a non-risk, and Epic 23 avoids a ~4,691 LOC templater reimplementation.
- One rendering implementation, one capture path. Static HTML, the SPA, the webview, and Nuxt project from the same bytes rather than from three drifting captures.
- The prose fidelity question stops being re-litigated per surface: `v-html` of an IR string is measurably byte-faithful, which is a far cheaper guarantee than per-renderer parity tests in a second language.
- Makes ADR 0008 and ADR 0013 agree with each other and with what ships.

**Negative / trade-offs**
- **The IR is a data-plus-markup document, not a pure data document** — the opposite of the direction ADR 0013 §5's "the IR becomes a data document rather than a data-plus-markup document" anticipated for charts. That framing was correct for charts and is wrong for prose, and this ADR says so plainly rather than pretending the two halves are symmetric.
- **A consumer cannot re-style prose structurally** without either re-rendering from source or post-processing HTML. Scoped CSS in particular does **not** reach `v-html`'d markup without `:deep()` — Story 23.1 measured it and Story 23.2 demonstrated the fix live. That is a real constraint on Epic 23's component library, and it is the price of byte-faithfulness.
- **Rendered HTML is larger than the view models would be**, and it pins the IR to HTML as a projection target. A non-HTML consumer (a future terminal or PDF projection) would get markup it must strip.
- **The prose contract now has a versioning obligation**: a change to how the C# core renders prose is a change to the IR's payload, even when no field moves. `schemaVersion` deliberately does *not* bump for that — the content of a string is not its shape — so prose regressions stay the golden gate's job, not the schema's.

## Options considered

| Option | Verdict |
|---|---|
| **Amend §Decision 1 to carry rendered prose HTML** | **Chosen.** Matches what ships, preserves the only measured result, and keeps Epic 23's scope as ADR 0009 assumed it. |
| **Build the IR to ADR 0008's literal wording (view models + SVG)** | Rejected. Revives the custom-renderer fidelity risk 23.1 measured away and adds a ~4,691 LOC reimplementation to Epic 23 — for a purity that no consumer has asked for. |
| **Carry BOTH — rendered HTML and the view models** | Rejected. Two representations of the same page that must never disagree is precisely the drift class ADR 0012 and ADR 0013 exist to end, and it inflates an IR whose delta size Story 22.1 already measured as the binding constraint. |
| **Leave the contradiction in the spike report, unamended** | Rejected on the CLAUDE.md ADR-trigger rule: a cross-cutting contract change recorded only in a story or spike is exactly the failure that rule was written for. |

## Ratified decisions

*None yet — this ADR is **Proposed**. Ratification is the owner's, not the dev agent's.*

## References
- **The clause it amends:** [ADR 0008](0008-json-ir-canonical-and-incremental-generation.md) §Decision 1 (and §Decision 4's SVG non-goal).
- **The ADR that already amended the chart half:** [ADR 0013](0013-text-twin-is-the-no-js-contract.md) §5 (IR carries chart data + component configuration) and §2 (the text twin is contract).
- **The consumer that depends on the outcome:** [ADR 0009](0009-frontend-framework-for-projection-layer.md); Story 23.1's spike report (`_bmad-output/implementation-artifacts/23-1-spike-report.md`) — Axis 2 (byte-identical `<main>` parity, the custom-renderer counts) and § *Follow-ups outside this story* (the hard requirement on 22.2).
- **The measurement that scoped the addressing half:** Story 22.1's spike report (`_bmad-output/implementation-artifacts/22-1-spike-report.md`) — chunk-granularity delta cost, and the over-cap single-page chunk.
- **The view-model contract this does NOT replace:** [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md); `tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs`.
- **The story that implements it:** Story 22.2 (`_bmad-output/implementation-artifacts/22-2-canonical-ir-schema-and-versioning.md`); `src/SpecScribe/SpaDelivery.cs`; `tests/SpecScribe.Tests/CanonicalIrSerializationTests.cs`.
- **Architecture:** [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2 (shared core; adapters translate, never reinterpret), AD-8 (canonical interaction state, adapter-specific transport), NFR4 (additive), NFR9 (reproducible CI).
- **The rule that required this ADR to exist:** `CLAUDE.md` § Decision records.
