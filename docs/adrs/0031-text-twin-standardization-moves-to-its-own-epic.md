# ADR 0031: Text-Twin / JS-Off Standardization Moves to Its Own Epic, Not a Per-Story Gate

**Status:** Accepted (owner-ratified 2026-07-29, at the Epic 20 retrospective)
**Date:** 2026-07-29
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (**amends Decision 3 / Ratified decision 3**); [ADR 0030](0030-epic-24-graph-engine.md) (resolves its named open item — "retiring `Charts.ReferenceGraph`'s SVG is gated on an ADR 0013 §3 text-twin audit that no Epic 24 story owns"); Epic 20 Stories 20.6 (text-twin audit) and 20.9 (built Git Insights a twin it never had); seats **Epic 28** (unscheduled, no stories yet)

## Context

ADR 0013 promoted the text twin from convention to contract and imposed a **hard per-surface gate**: no surface retires its server-rendered SVG until its twin is audited complete, in a live browser with JavaScript disabled. That gate was satisfied inline, per-surface, inside Epic 20 itself — Story 20.6 audited seven surfaces (4 pass, 3 fail/keep-SVG at the time), and Story 20.9 later built Git Insights a twin it had never had, as part of finishing that surface's own rollout.

Two things surfaced at the Epic 20 retrospective (2026-07-29):

1. **The gate has no owner going forward.** ADR 0030, ratified the day before this one, already named the gap explicitly: Epic 24's force-directed graph views want to retire `Charts.ReferenceGraph`'s SVG, but "no Epic 24 story owns" the text-twin audit that ADR 0013 §3 requires first. The pattern that produced this — a cross-cutting accessibility contract enforced per-story, by whichever story happens to touch a surface — is the same *convention-not-component* shape ADR 0010 §6 tried and failed at for the arc-rendering math, which is exactly what ADR 0012 replaced with one shared component.
2. **The project has exactly one confirmed consumer right now.** The owner's stated priority is a polished, minimal happy-path core UX, proven out before broadening. Auditing every new hierarchy/graph surface's text twin inline, as a condition of that surface shipping, pulls story work toward accessibility breadth instead of UX depth — a trade the owner is not choosing to make at this stage.

## Decision

**1. ADR 0013's hard per-story gate is retired.** A story that ships a new or changed hierarchy/graph surface is **no longer required** to build and audit that surface's text twin in the same story as a condition of completion. NFR-5's wording is **unchanged** — information and navigation must still survive JS-off, visualization need not, provided a server-rendered text equivalent eventually carries the information — but *when* that equivalent is built is decoupled from *when the feature ships*.

**2. Text-twin/JS-off standardization becomes the scope of one dedicated epic — Epic 28 (seated by this ADR, unscheduled, no stories yet).** It follows the same shape Epic 20 itself used for the chart engine: prove a standardized, reusable twin pattern on **one** representative surface first, verify it live (JS disabled, screen reader, keyboard), then broaden to the remaining surfaces as its own scoped rollout — not reinvented per-story, per-surface, by whichever epic happens to be touching that surface at the time.

**3. This resolves ADR 0030's open item directly.** Epic 24 is not required to own a text-twin audit for `Charts.ReferenceGraph`'s retirement; that audit belongs to Epic 28. Epic 24 may ship its graph views with the current SVG left in place, or with a known-incomplete twin, as tracked debt — not a silent gap.

**4. A surface without an audited twin is tracked debt, not a silent regression.** Any surface shipped after this ADR without a complete twin is recorded in `sprint-status.yaml` / `epics.md` as owed to Epic 28, the same way Story 20.9 recorded Git Insights' missing twin before fixing it. This is what keeps the deferral honest rather than indistinguishable from simply forgetting.

**5. Already-audited surfaces are not regressed.** Dashboard, story detail, Code Map (per Story 20.6/20.9), Impact Map, and Git Insights keep the twins they already have; this ADR changes the gating for *new* work, not standing coverage.

## Consequences

**Positive**
- Unblocks ordinary feature and polish work from carrying a per-story accessibility-audit tax while the project has one confirmed consumer.
- Consolidates twin authorship into one place instead of re-deriving the pattern per-surface, per-epic — avoiding a twin-shaped repeat of the three-hand-rolled-arc-renderers mistake ADR 0012 exists to end.
- Gives Epic 24 (and any future hierarchy/graph work) an explicit answer instead of an unowned gate.
- Debt is named and tracked, not merely dropped.

**Negative / trade-offs**
- **JS-off and screen-reader visitors lose information parity on any surface shipped between now and Epic 28's rollout of that surface.** This is a real, accepted product concession — the same category of trade-off ADR 0013 itself named, extended in time rather than removed.
- Epic 28 is unscheduled. If it is never seated with real stories, the debt it owns has no forcing function — the owner accepts this risk explicitly rather than resolving it here.
- Two consumers (Epic 24 today, any future epic touching an existing surface) must remember to record debt rather than silently ship a gap — a discipline this ADR asks for but cannot enforce mechanically.

## Options considered

| Option | Verdict |
|---|---|
| **Keep ADR 0013's hard per-story gate** | Rejected by the owner 2026-07-29. Correct for a multi-consumer product; costs more than it's worth while there is one confirmed consumer and the priority is core-UX depth. |
| **Drop the gate entirely, no successor owner** | Rejected. Same failure ADR 0030 already named — an unowned obligation drifts indefinitely and a "someday" epic with no seat is indistinguishable from abandonment. |
| **One dedicated epic proves the pattern once, then rolls out** (chosen) | Mirrors Epic 20's own spike → component → site-wide-rollout shape for the chart engine; gives the debt a named owner without blocking current feature work. |

## Ratified decisions (2026-07-29)
1. ADR 0013 Decision 3 / Ratified decision 3 (the hard per-story gate) is **retired** for new work; NFR-5's wording is unchanged.
2. **Epic 28** is seated in `epics.md` and `sprint-status.yaml` as the dedicated, unscheduled home for text-twin/JS-off standardization — no stories written yet.
3. ADR 0030's open item (Epic 24 owning `Charts.ReferenceGraph`'s text-twin audit) is resolved: it does not; Epic 28 does.
4. A surface shipped without a complete twin after this ADR is recorded as tracked debt owed to Epic 28, not left silent.
5. Already-audited surfaces (dashboard, story detail, Code Map, Impact Map, Git Insights) are unaffected.

## References
- **The clause it amends:** [ADR 0013](0013-text-twin-is-the-no-js-contract.md) Decision 3 / Ratified decision 3.
- **The open item it resolves:** [ADR 0030](0030-epic-24-graph-engine.md)'s named gap — no Epic 24 story owns `Charts.ReferenceGraph`'s text-twin audit.
- **The epic it seats:** Epic 28 (`_bmad-output/planning-artifacts/epics.md`), unscheduled, no stories yet.
- **The retrospective that raised it:** Epic 20 retrospective, 2026-07-29 (`_bmad-output/implementation-artifacts/epic-20-retro-2026-07-29.md`).
