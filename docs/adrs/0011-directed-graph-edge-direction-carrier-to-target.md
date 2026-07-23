# ADR 0011: Directed-Graph Edge Direction — Carrier → Target (`From` = the node that holds the reference)

**Status:** Accepted (owner-ratified 2026-07-22, escalated from the Story 19.1 spike deferral as the convention begins to spread beyond Epic 19)
**Date:** 2026-07-22
**Deciders:** Matthew-Hope Eland
**Relates to:** Epic 19 (Work Graph — Story 19.1 coverage spike §2, Story 19.2 directed-graph visualization); Epic 24 (Change-Coupling Graphs, FR40 — the first surface to inherit this rule); [ADR 0002 — Shared Rendering Core with Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md) (any graph builder is a projection over existing models); memory `story-19-1-work-graph-spike-done`, `adr-creation-trigger-gap-epic-10-retro`

## Context

Story 19.1 (work-graph model & coverage spike) inventoried SpecScribe's provenance seams and had to pick a **single edge-direction convention** so that Story 19.2's directed graph — and its queries (cycle finder, ambiguous reverse-link, multi-hop path) — would be built on a precise, non-inferred definition rather than an ad-hoc diagram. The spike chose, and documented once in §2 of its coverage map:

> **`From` = the node that physically carries the reference in the authored source; `To` = the node referenced.** (carrier → target)

The spike consciously wrote **no ADR** at the time — "no architectural fork appeared," a defensible call for a single, not-yet-built surface. Its own review (2026-07-22) deferred the ADR with an explicit trigger: *escalate if/when the convention spreads beyond Epic 19.* Two facts now satisfy that trigger:

1. **Epic 24 (change-coupling graphs, FR40) inherits it.** Epic 24 introduces a *directional coupling metric* (confidence / support / lift, cross-boundary) rendered as force-directed / chord / matrix graphs at ego and whole-repo scopes. It is a second directed-graph surface, and it needs a stated direction rule too — left unstated, two surfaces would grow two mental models of "which way does an arrow point?"
2. This project's own memory records a standing lesson — **agents under-propose ADRs** for cross-cutting decisions, burying them as owner-locked story notes (`adr-creation-trigger-gap-epic-10-retro`). The direction rule is load-bearing and now shared; a story-embedded note is the wrong home for it.

This ADR promotes the §2 convention to a durable, cross-surface decision. It records an existing, in-force convention (already guiding 19.2) — it does not change how any shipped code behaves today.

## Decision

**1. Cross-cutting invariant (all directed-graph surfaces).**
Every directed-graph surface in SpecScribe defines its edge direction by **one explicit, source-anchored rule stated at the surface** (in prose, legend, and the accessible text-equivalent). Direction is **never inferred per-render or left ambiguous**. Reverse / aggregation views (e.g. "Referenced by", "Deferred from this") are **inversions of a forward edge index — not new edges and not a second parse**: build them by reversing the forward map (`_codeReverseMap`, `FollowUpGeometry.DeferredForSource`), never by re-deriving the relationship.

**2. Work-graph instantiation (Epic 19 / Story 19.2): carrier → target.**
For the provenance work graph, `From` = the node that physically holds the pointer in the authored artifact; `To` = the node it points at. Provenance ("where did this come from?") is answered by walking **out-edges** from a node. This is the honest, implementation-aligned rule: it matches which side actually stores the reference, so no direction is guessed.

| Edge kind | `From` → `To` (carrier → target) | Reads naturally? |
|-----------|----------------------------------|------------------|
| `stemmed-from` | deferred item → source story/spec/quick-dev | yes |
| `resolves` (is-resolved-by) | deferred item → resolving story/spec | yes |
| `cites` | citing artifact (story/doc/ADR) → code file | yes |
| `raised-in` | action item → other epic's retro | yes |
| `covers` (covered-by) | requirement → covering epic | **inverted** — the coverage line lives on the requirement, so it is stored requirement→epic and **must be labelled "covered-by"** to stay legible |

`FollowUpDeferredSlot` is the existing proto-edge record to project from; 19.2 must not invent a parallel graph type.

**3. Epic 24 inheritance (directional coupling graphs).**
Change-coupling graphs adopt the same discipline under the invariant in (1). The carrier→target *phrasing* is about authored provenance; a statistical coupling edge has a *computed* direction, so Epic 24 states its own concrete rule under this ADR rather than assuming the provenance wording transfers verbatim:

- **Directional metrics** (e.g. confidence / lift where "a change in A predicts a change in B", A→B): `From` = the **antecedent** (A, the predictor), `To` = the **consequent** (B). Walking out-edges from A then answers "what do A's changes tend to drag along?" — the direct analogue of walking out-edges for provenance.
- **Symmetric metrics** (raw co-change support, undirected coupling): render as **undirected edges**, not fake-directed arrows. Do not manufacture a direction the metric does not carry.

## Consequences

**Positive**
- One rule for arrow meaning across every directed surface — a reader (or a new graph story) never has to reverse-engineer which way an edge points.
- Reverse panels stay cheap: they are index inversions of the forward edges, so there is exactly one authored edge set per surface and no risk of a forward view and its reverse view disagreeing.
- Epic 24 starts from a decided precedent instead of re-litigating direction from zero — and is explicitly told where the provenance phrasing does and does not transfer.
- The rule is anchored to what the source data physically holds, so no edge direction is inferred or guessed.

**Negative / trade-offs**
- `covers` is stored inverted relative to how one says it in English ("epic *covers* requirement"), so it depends on the "covered-by" label to read correctly — a small, permanent legibility tax the ADR accepts in exchange for source-anchored honesty (the coverage line genuinely lives on the requirement).
- The bridge from "carrier → target" (authored provenance) to "antecedent → consequent" (statistical coupling) is an **analogy, not an identity**. Epic 24 must state its own direction rule explicitly (as in Decision 3) rather than copying the provenance sentence — the shared thing is the *discipline* (one explicit, source-anchored rule; never inferred), not the literal phrase.
- Any reader of a bare arrow must know the convention; mitigated by house style already requiring sr-only node/edge lists and legend text alongside every chart, which is where each surface states its rule.

## Ratified decisions (2026-07-22)
1. All directed-graph surfaces define edge direction by one explicit, source-anchored rule stated at the surface; direction is never inferred or left ambiguous.
2. Reverse / aggregation views are inversions of the forward edge index — not new edges, not a second parse.
3. The work graph (Epic 19 / 19.2) uses **carrier → target** (`From` holds the reference; `To` is referenced); provenance = out-edges. `covers` is the one stored-inverted kind and is labelled "covered-by".
4. Epic 24 coupling graphs inherit the discipline: directional metrics point antecedent → consequent; symmetric metrics render undirected. Epic 24 states its concrete direction rule under this ADR.
5. This ADR memorializes an in-force convention — it changes no shipped behavior today.

## References
- **Source of the convention:** `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md` § "2. Edges" / "Direction convention (Task 2, picked once, load-bearing for 19.2)"; the review's [Review][Defer] item that escalated this ADR.
- **Downstream surface it governs first:** Story 19.2 (Epic-Scoped Provenance Subgraph) — see the spike's §5 recommendation and feed-forward stub.
- **The inheriting epic:** Epic 24 (Change-Coupling Graphs, FR40) — `_bmad-output/planning-artifacts/epics.md`; memory `epic-24-change-coupling-graphs-seeded`.
- **Architecture basis:** [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) — a graph builder is a pure projection over existing models (`FollowUpGeometry`, citation maps), not a per-surface re-parse.
- **The lesson that made this an ADR, not a note:** memory `adr-creation-trigger-gap-epic-10-retro` (agents should proactively propose ADRs for cross-cutting decisions).
