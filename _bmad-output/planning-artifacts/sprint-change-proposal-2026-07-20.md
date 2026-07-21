# Sprint Change Proposal — 2026-07-20

**Title:** JSON Data-Layer as Canonical Intermediate Representation (IR) + Incremental, Event-Driven Generation
**Author:** Matthew-Hope Eland (owner) with Correct-Course facilitation
**Mode:** Batch
**Scope classification:** **Moderate** (backlog/architecture change — new ADR + one future epic; **no code changes now**)
**Trigger type:** Strategic architecture direction-setting (owner-initiated re-open)
**Disposition:** **Design-now, build-later** — lock the direction firmly; do **not** disturb the reopened Epic 7 (7.9–7.12) work in flight.

---

## Section 1 — Issue Summary

The owner re-opened the long-standing delivery-architecture question — *"static HTML site generator vs. JSON files consumed by a consistent core SPA"* — with four motivating concerns:

1. **Generation time** on large inputs.
2. **Scale**: repos with many commits / files / epics / stories, and the **raw byte size of the emitted HTML**.
3. **VS Code extension difficulties** a JSON-based approach could alleviate (weighted, per owner: **payload size/perf** and **live-update/delta friction**; **largely anticipatory**, not acute pain today).
4. A possible future **client/server model or incremental generation** that updates data **only on new events**.

**This is not a re-litigation of [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md).** That ADR (Story 6.6 measurement spike, ratified 2026-07-10) already settled the expensive question — *do we port the C# core to TypeScript?* → **No** (~14,200 LOC + 676 tests; Markdig-fidelity and deep-git parsing are the flagged risks; deferred, gated on a WASM-git-bridge proof). It also shipped two additive pieces since:

- **JSON+SPA delivery adapter** — live as a third `IRenderAdapter` (Story 6.7: 219→12 files, byte-identical).
- **npx channel** — proven and shipped (Story 16.8).

**The genuinely open decision** — which ADR 0006 explicitly deferred — is whether the JSON data-layer should stop being an *optional output form* and become the **canonical intermediate representation** every surface projects from, and whether to invest in **incremental, event-driven generation** (and a possible client/server future) on top of it.

**Owner decision captured this session:** **JSON IR as the canonical model.**

### Evidence base (from ADR 0006's spike, measured against this repo 2026-07-10)

| Owner concern | Does "make SPA primary" fix it? | What actually fixes it |
|---|---|---|
| Generation time | ❌ No — dominated by ingest + `git` subprocess (~3.2s), **not** HTML writing; the SPA adapter runs the same full pipeline | **Incremental generation** (recompute only changed scope) — concern #4 |
| Raw HTML byte size | ❌ Mostly no — inline chart SVG is **69.3% of the dashboard body / 58.9% of epics**; JSON shipping pre-rendered SVG cuts file count, **not** bytes. True byte win needs the TS chart port (**deferred, correctly**) | Client-rendered charts (deferred TS port) |
| File count at scale | ✅ Yes — already delivered (Story 6.7, few files) | Shipped |
| VS Code payload/delta | ⚠️ Partly — a JSON IR the webview *consumes* + delta push addresses both named concerns | JSON IR + incremental delta transport |

**Crux:** three of the four concerns are *not* solved by choosing "SPA over static." They are solved by a single architectural move — **elevating the JSON data-layer to the canonical IR and making generation incremental/event-driven over it** — which does *not* require the deferred TS port, because charts remain **pre-rendered SVG carried inside the IR**.

---

## Section 2 — Impact Analysis

### 2.1 Architecture (primary impact — [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md))

The direction is a **natural extension of existing invariants, not a break**:

- **AD-2** (host-neutral view models are the contract between core and adapters) — the JSON IR is simply the **serialized, durable form of AD-2's view models**. This decision *promotes* AD-2 from an in-process contract to a persisted/transportable one. **No conflict; it hardens AD-2.**
- **AD-5** (watch mode treats changed scope as the unit of recomputation) — already commits the project to incremental recomputation. Concern #4 *operationalizes AD-5* into an event-driven delta pipeline. **Consistent.**
- **AD-8** (interaction-state contract is canonical; update **transport** is adapter-specific) — already anticipates a delta/push transport distinct from static hydration. A client/server delta channel is an AD-8-conformant transport. **Consistent.**
- **AD-1 / AD-4 / AD-6 / NFR6** — unchanged. C# stays the single core (AD-1); insights stay additive (AD-4); read-only holds (AD-6); **static HTML remains the JS-optional accessibility baseline (NFR6)** — now reframed as *a projection of the IR* rather than the sole output.

**Required architecture action:** a **new ADR** (proposed **ADR 0008**) recording: (a) the JSON IR as canonical representation, (b) static HTML / SPA / webview as **co-equal projections** of that IR, (c) incremental event-driven generation as the target generation model, (d) an explicit **non-goal**: this does **not** reopen the TS core port (ADR 0006 stands) — charts stay pre-rendered SVG *in* the IR.

### 2.2 Epic impact

- **Current in-flight work (Epic 7 reopened: 7.9–7.12): NOT affected.** Design-now/build-later disposition protects it. No resequencing.
- **New epic required — Epic 22 (Delivery Evolution): JSON IR + Incremental Event-Driven Generation.** Seated as **backlog** now; not scheduled.
- **Epic 6 (VS Code):** benefits later (webview becomes an IR consumer with delta push) but needs **no change now**; ADR 0005/0006 stand.
- **Epic 20 (Interactive Explorer — first client-JS surface)** and **Epic 21 (Value/Correlation viz):** *soft dependency* — both are client-JS surfaces that would consume the IR. Recommend they **remain sequenced ahead of / independent from** Epic 22, but Epic 22's IR schema should be informed by their data needs (note as a cross-reference, not a blocker).
- **No epics are invalidated or removed.**

### 2.3 PRD impact

- **MVP is NOT affected.** The current portal (static HTML + optional SPA/webview) already satisfies the PRD. This is a post-MVP architecture-evolution investment.
- **NFR6 (accessibility / JS-optional baseline):** preserved and re-affirmed — the IR decision *keeps* static HTML as a first-class projection, not a `<noscript>` afterthought. No PRD requirement changes; a clarifying note may be added that static HTML is "a projection of the canonical IR" rather than "the output."
- **NFR4 (additive adapters), NFR9 (reproducible CI):** consistent — the IR is produced by the same C# core; reproducibility is unchanged (arguably improved, since the IR is a stable serialization boundary).

### 2.4 UX impact

- **None now.** No user-visible surface changes under design-now/build-later. When Epic 22 is built, the SPA experience may become the *default* surface a user lands on, with static HTML as the guaranteed fallback — a UX decision to be made *within* Epic 22, not here.

### 2.5 Other artifacts

- **rendering-architecture.md** — Client-Side Enhancement Policy stays valid; add a note that the JSON IR is the serialized view-model contract and that the SPA/webview consume it. (Deferred to Epic 22 kickoff.)
- **Testing:** the IR becomes a valuable **golden/serialization boundary** — the existing `SectionViewModelSerializationTests` round-trip pattern generalizes to the whole IR. (Epic 22 concern.)
- **CI/CD, packaging (Epic 16):** unchanged now; a future client/server mode would add a long-lived-process deployment shape, but that is explicitly *later*.

---

## Section 3 — Recommended Path Forward

**Selected approach: Option 1 — Direct Adjustment (additive), Hybrid with a design-lock.**

Concretely:

1. **Ratify a firm architectural direction now via a new ADR (0008)** — the "firm direction down the road" the owner asked for. This locks the model without spending build effort.
2. **Seat Epic 22 (backlog, unscheduled)** — "Delivery Evolution: JSON IR + Incremental Event-Driven Generation" — as the container for the eventual build.
3. **Do not build now; do not disturb Epic 7.** When Epic 22 is later picked up, it **must open with a measurement spike** mirroring Story 6.6 (de-risk incremental-recompute correctness + IR-delta transport with numbers before committing implementation stories).

### Rationale

- **Matches owner appetite** (design-now, build-later) exactly.
- **Additive and invariant-preserving** — extends AD-2/AD-5/AD-8 rather than breaking them; no seed-level namespace split required (NFR4).
- **Avoids the expensive trap** — does not pay the ~14,200-LOC TS port; the byte-size concern is honestly acknowledged as *only* solvable by that deferred port, so it is **not** promised here.
- **Unifies four concerns under one move** — gen-time (incremental), file-count (IR+SPA, already shipped), VS Code payload/delta (IR consumer + delta push), client/server future (IR + delta transport foundation).
- **Cheap to reverse** — if the direction proves wrong, the IR is a serialization of view models that already exist in-process; abandoning it costs an adapter, not a rewrite.

### Alternatives considered & rejected

- **Option 2 — Rollback:** N/A. Nothing recently shipped needs reverting; Story 6.7 (SPA adapter) and 16.8 (npx) are consistent with this direction and are *kept*.
- **Option 3 — MVP review:** N/A. MVP is unaffected; this is post-MVP evolution.
- **"Make SPA primary, static = `<noscript>` only":** rejected — violates the spirit of NFR6 and buys less than the IR framing (bytes don't shrink; it's the narrower of the two options the owner was offered and not the one chosen).
- **Reopen the TS port:** rejected — ADR 0006 stands; owner did not select this.

### Effort / risk (for the eventual Epic 22, not now)

- **Effort:** Medium–High (spike + IR schema + incremental recompute engine + delta transport + consumer wiring).
- **Risk:** Medium — incremental-recompute **correctness** (stale/topology-change invalidation, cf. AD-5's "topology changes can trigger broader refresh") is the primary technical risk; the Story-6.6-style spike is the mitigation.

---

## Section 4 — Detailed Change Proposals

### Change 4.1 — New ADR 0008 (proposed; to be written on approval)

```
ADR 0008: JSON Data-Layer as Canonical Intermediate Representation & Incremental, Event-Driven Generation
Status: Proposed → (Accepted on owner ratification)
Amends/Extends: ADR 0002 (view-model contract), ADR 0006 (delivery architecture) — extends, does not supersede

Decision:
  1. The serialized JSON data-layer (AD-2's host-neutral view models, plus pre-rendered SVG
     chart fragments) is the CANONICAL intermediate representation (IR) of a SpecScribe project.
  2. Static HTML, the SPA, and the VS Code webview are CO-EQUAL PROJECTIONS of the IR.
     Static HTML remains the JS-optional accessibility baseline (NFR6) — a projection, not
     the primary output and not a mere <noscript> fallback.
  3. Generation targets an INCREMENTAL, EVENT-DRIVEN model: the C# core recomputes only the
     changed scope (operationalizing AD-5) and emits IR deltas suitable for a future watch /
     client-server transport (conformant with AD-8).
  4. NON-GOAL: this does NOT reopen the C#→TS core port (ADR 0006 stands). Charts remain
     PRE-RENDERED SVG carried inside the IR; the byte-size reduction that would require the
     TS chart port is explicitly out of scope.

Consequences (positive): unifies gen-time/file-count/webview-payload/client-server concerns;
  hardens AD-2 into a persisted contract; keeps NFR6 free; additive (NFR4).
Consequences (trade-offs): does NOT shrink bytes (charts still ship); introduces an IR schema
  to version/maintain; incremental-recompute correctness is a real risk (spike-gated).
```

### Change 4.2 — Seat Epic 22 (epics.md + sprint-status.yaml)

```
## Epic 22: Delivery Evolution — JSON IR + Incremental Event-Driven Generation
Status: backlog (unscheduled; design-locked by ADR 0008; build-later)
Goal: Make the serialized JSON IR the canonical representation all surfaces project from,
      and move generation to an incremental, event-driven model, without porting the C# core.
Opens with: a measurement spike (mirror Story 6.6) de-risking incremental-recompute
      correctness + IR-delta transport BEFORE implementation stories are seated.
Candidate stories (illustrative — finalized at kickoff, not now):
  22.1  Spike: incremental recompute + IR-delta transport (measure correctness & latency)
  22.2  Canonical IR schema + versioning (serialize AD-2 view models + SVG fragments)
  22.3  IR-projection: static HTML rendered FROM the IR (prove parity with today's golden)
  22.4  IR-projection: SPA + webview as IR consumers (fold in Story 6.7 adapter)
  22.5  Incremental event-driven regeneration engine (operationalize AD-5 over the IR)
  22.6  (Optional/spike-gated) client-server delta channel — watch server pushes IR deltas
Cross-refs: informs/informed-by Epic 20 (first client-JS surface) & Epic 21 (value/correlation viz).
```

### Change 4.3 — Clarifying notes (deferred to Epic 22 kickoff, listed for traceability)

- ARCHITECTURE-SPINE.md: note IR as the serialized AD-2 contract; mark "static HTML = a projection."
- rendering-architecture.md: note SPA/webview consume the IR; Client-Side Enhancement Policy unchanged.
- PRD NFR6: optional clarifying phrase — "static HTML is a projection of the canonical IR."

---

## Section 5 — Implementation Handoff

**Scope classification: Moderate** (architecture direction + backlog seating; no code now).

| Role | Responsibility |
|---|---|
| **Architect** | Author **ADR 0008** from Change 4.1 (the design-lock). Owner ratifies status → Accepted. |
| **Product Owner** | Seat **Epic 22** as backlog in `epics.md` + `sprint-status.yaml` (Change 4.2). Record cross-refs to Epics 20/21. **Do not schedule; do not touch Epic 7.** |
| **Developer** | **No action now.** At future Epic 22 kickoff: run the Story-6.6-style spike first (Change 4.2, story 22.1) before any implementation story. |

**Success criteria for this correct-course:**
1. ADR 0008 written and ratified — the firm, durable direction exists.
2. Epic 22 seated as backlog, unscheduled, with the spike-first gate recorded.
3. Epic 7 (7.9–7.12) untouched and unblocked.
4. No PRD/MVP change; NFR6 explicitly preserved.

**Correct-course plan (if the direction later proves wrong):** the IR is a serialization of already-existing in-process view models, so abandoning it costs a delivery adapter, not a core rewrite — reversal is cheap. A hard requirement of "no native binary + live in-editor regen" would still route to the ADR-0006 WASM-git-bridge spike, unchanged by this decision.

---

## Checklist Status Summary

- **§1 Trigger & Context:** [x] Done — owner-initiated architecture re-open; evidence = ADR 0006 spike numbers + owner's four concerns.
- **§2 Epic Impact:** [x] Done — new Epic 22 (backlog); Epic 7 untouched; Epics 20/21 cross-ref; none invalidated.
- **§3 Artifact Conflict:** [x] Done — extends AD-2/AD-5/AD-8; no PRD/UX conflict; NFR6 preserved.
- **§4 Path Forward:** [x] Done — Option 1 (additive) + design-lock ADR; Options 2 & 3 N/A.
- **§5 Proposal Components:** [x] Done — this document.
- **§6 Final Review:** [x] Done — **owner approved 2026-07-20.**

---

## Addendum — Owner ratification (2026-07-20)

The owner **approved** this proposal, and additionally requested a front-end-framework ADR (CSS modularity / large-file / low-modularity concerns at the presentation layer). Both ADRs are now written and ratified:

- **ADR 0008 — Accepted.** JSON IR canonical + incremental event-driven generation → **Epic 22** (backlog, spike-first). No TS core port.
- **ADR 0009 — Accepted.** Front-end framework = **Vue + Nuxt 3, universal/SSR** over the IR; NFR6 baseline preserved by Nuxt prerender; ADR 0005's "rendering stays in C#" north star **relaxed for the presentation layer** (analysis stays C#, ADR 0006 axis C not reopened) → **Epic 23** (backlog, spike-first, sequences after Epic 22).

**Sequencing:** Epic 7 (7.9–7.12) in flight untouched → Epic 22 (IR) → Epic 23 (Vue/Nuxt over IR). Each epic opens with a measurement/feasibility spike. **IR byte-chunking constraint** recorded on Story 22.2 (Story 6.6 at-scale measure: `pages-root.json` 112.9 MB / `code-map.html` 82.5 MB at 1,461 pages — chunk by bytes, not page count).

**Handoff:** ADRs authored + ratified; Epics 22 & 23 seated as backlog in `epics.md` + `sprint-status.yaml`. No dev action until scheduled; each epic is spike-gated.
