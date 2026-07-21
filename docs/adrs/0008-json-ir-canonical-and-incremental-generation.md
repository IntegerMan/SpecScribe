# ADR 0008: JSON Data-Layer as Canonical Intermediate Representation & Incremental, Event-Driven Generation

**Status:** Accepted (ratified by owner 2026-07-20)
**Date:** 2026-07-20
**Deciders:** Matthew-Hope Eland
**Extends:** [ADR 0002 — Shared Rendering Core & Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md), [ADR 0006 — Delivery Architecture & Distribution](0006-delivery-architecture-and-distribution.md) (extends; does **not** supersede either)
**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20.md` (Correct-Course)

## Context

[ADR 0006](0006-delivery-architecture-and-distribution.md) settled the expensive delivery question — *do we port the C# core to TypeScript?* → **No** — and shipped two additive pieces since: the **JSON+SPA delivery adapter** (Story 6.7, a third `IRenderAdapter`, 219→12 files, byte-identical) and the **npx channel** (Story 16.8). ADR 0006 explicitly **deferred** a further question: should the JSON data-layer stop being an *optional output form* and become the **canonical model** every surface projects from?

The owner re-opened that question (2026-07-20) with four motivations:

1. **Generation time** on large inputs.
2. **Scale** — many commits / files / epics / stories, and raw **HTML byte size**.
3. **VS Code difficulties** — webview **payload size/perf** and **live-update/delta friction** (owner-weighted as *largely anticipatory*, not acute pain today).
4. A possible future **client/server model or incremental generation** that updates data **only on new events**.

### What the measured evidence (ADR 0006 spike, this repo, 2026-07-10) says

- **Generation time is dominated by ingest + the `git` subprocess (~3.2 s), not HTML writing.** Changing the *output form* does not help gen-time; only **incremental recomputation** does.
- **Inline chart SVG is 69.3 % of the dashboard body / 58.9 % of epics.** A JSON layer shipping pre-rendered SVG cuts **file count**, not **bytes**. A true byte reduction requires porting chart generation to TS — the deferred, expensive path (ADR 0006). That byte win is therefore **out of scope here.**
- **File count** at Epic-7 scale (thousands of pages) is the real large-repo problem, and the JSON+SPA form already answers it.

**Crux:** three of the four concerns are *not* solved by "make the SPA primary." They are solved by one architectural move — **elevating the JSON data-layer to the canonical intermediate representation (IR) and making generation incremental/event-driven over it** — which does **not** require the deferred TS port, because charts remain **pre-rendered SVG carried inside the IR**.

This move is a natural extension of existing invariants, not a break:

- **AD-2** — host-neutral view models are the core↔adapter contract. The IR is simply the **serialized, durable, transportable form of AD-2's view models**. This decision *promotes* AD-2 from an in-process contract to a persisted one.
- **AD-5** — watch mode already treats *changed scope* as the unit of recomputation. Concern #4 *operationalizes* AD-5 into an event-driven delta pipeline.
- **AD-8** — interaction-state contract is canonical; **transport** is adapter-specific. A client/server delta channel is an AD-8-conformant transport.

## Decision

1. **The serialized JSON data-layer is the canonical intermediate representation (IR) of a SpecScribe project.** It carries AD-2's host-neutral view models plus **pre-rendered SVG chart fragments**. The C# core is its single producer (AD-1 unchanged).

2. **Static HTML, the SPA, and the VS Code webview are co-equal projections of the IR.** Static HTML remains the **JS-optional accessibility baseline (NFR6)** — reframed as *a projection of the IR*, **not** the sole output and **not** a mere `<noscript>` afterthought.

3. **Generation targets an incremental, event-driven model.** The C# core recomputes only the changed scope (operationalizing AD-5, including AD-5's rule that *topology changes may trigger a broader refresh*) and emits **IR deltas** suitable for a future watch / client-server transport (conformant with AD-8).

4. **NON-GOAL: this does not reopen the C#→TS core port.** ADR 0006 stands. Charts stay **pre-rendered SVG inside the IR**; the byte-size reduction that would require the TS chart port is explicitly out of scope. *(The presentation-layer rendering language is a separate question — see [ADR 0009](0009-frontend-framework-for-projection-layer.md).)*

### Disposition — design-now, build-later

This ADR **locks the direction**; it does not schedule the build. The eventual work is seated as **Epic 22 (backlog, unscheduled)** and **must open with a measurement spike** (mirroring Story 6.6) that de-risks **incremental-recompute correctness** and **IR-delta transport** before any implementation story. The reopened Epic 7 (7.9–7.12) work is **not** disturbed.

## Consequences

**Positive**
- Unifies gen-time (incremental), file-count (IR+SPA, already shipped), webview payload/delta (IR consumer + delta push), and the client/server future (IR + delta transport) under one additive move (NFR4).
- Hardens **AD-2** into a persisted, versioned contract and a natural golden/serialization test boundary (generalizing `SectionViewModelSerializationTests`).
- Keeps **NFR6** accessible baseline intact and reproducibility (NFR9) unchanged — same C# core, stable serialization boundary.
- **Cheap to reverse** — the IR serializes view models that already exist in-process; abandoning it costs a delivery adapter, not a core rewrite.

**Negative / trade-offs**
- Does **not** shrink bytes — chart SVGs still ship (byte win stays deferred with the TS chart port).
- Introduces an **IR schema** to version and maintain.
- **Incremental-recompute correctness** (stale/topology-change invalidation) is the primary technical risk — the reason Epic 22 is spike-gated.
- A future client/server mode adds a long-lived-process deployment shape (explicitly later; not decided here).

## References
- **Correct-Course record:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20.md`.
- **The delivery decision this extends:** [ADR 0006](0006-delivery-architecture-and-distribution.md); **the view-model contract it serializes:** [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md).
- **Presentation-layer follow-on:** [ADR 0009 — Front-End Framework for the Projection Layer](0009-frontend-framework-for-projection-layer.md).
- **Architecture:** [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1/AD-2 (shared core; adapters translate), AD-5 (changed-scope recomputation), AD-8 (canonical interaction state, adapter transport), NFR4 (additive), NFR6 (accessibility), NFR9 (reproducible CI).
- **Section view models the IR serializes:** Story 6.2; `DashboardView.cs` / `EpicsView.cs` + builders; `tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs`.
