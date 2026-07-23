---
title: 'ADR 0011 — Directed-Graph Edge Direction (Carrier → Target)'
type: 'chore'
created: '2026-07-22'
status: 'done'
route: 'one-shot'
---

# ADR 0011 — Directed-Graph Edge Direction (Carrier → Target)

## Intent

**Problem:** Story 19.1's coverage spike picked a load-bearing edge-direction rule (§2: `From` = carrier of the reference, `To` = referenced — "carrier → target") but consciously wrote no ADR, deferring it with the trigger "escalate if/when the convention spreads beyond Epic 19." Epic 24 (change-coupling graphs, FR40) now inherits it, and repo memory records a standing "agents under-propose ADRs" lesson — so the convention is now a cross-surface decision living only in a done story's review notes.

**Approach:** Author a short ADR (0011, Accepted) that promotes §2 to a durable cross-surface invariant — every directed-graph surface states one explicit, source-anchored direction rule, never inferred; reverse views are forward-index inversions — and explicitly scopes how Epic 24's directional vs. symmetric coupling edges inherit the discipline. Documentation only: the convention was already in force, so no `src/**` change.

## Suggested Review Order

1. [docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md](../../docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md) — the new ADR. Start with **Decision** (the 3 layers: invariant / work-graph carrier→target / Epic 24 inheritance), then **Consequences** for the honest trade-offs (`covers` inversion; analogy-not-identity for coupling).
2. [docs/adrs/README.md](../../docs/adrs/README.md) — the index entry for 0011 (last row) matches the established one-line-summary style.
3. [19-1-work-graph-model-and-coverage-spike.md](19-1-work-graph-model-and-coverage-spike.md) — the source deferred item (Review Findings → `[Review][Defer]`) is annotated **RESOLVED via ADR 0011**, closing the loop.
