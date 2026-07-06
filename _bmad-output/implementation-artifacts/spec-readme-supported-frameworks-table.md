---
title: 'README supported & planned frameworks table'
type: 'chore'
created: '2026-07-06'
status: 'done'
route: 'one-shot'
---

# README supported & planned frameworks table

## Intent

**Problem:** The README described framework support inconsistently — the intro implied BMad's game-dev module was handled while the Roadmap listed it (and Spec Kit) as planned, and there was no single at-a-glance view of which frameworks and versions SpecScribe renders today versus what is planned.

**Approach:** Add a "Supported frameworks" table near the top of the README listing supported frameworks with versions (BMad Method 6.10.0, BMad GDS / Game Dev Studio 0.6.0) and planned frameworks (GitHub Spec Kit, GSD, GSD-Pi, Superpowers), then reconcile the Roadmap so it points at the table for framework status and retains only feature-level plans.

## Suggested Review Order

1. [`README.md` — Supported frameworks table](../../README.md) — verify the supported rows (BMad 6.10.0, GDS 0.6.0) and planned rows match intent; confirm GDS vs GSD naming is correct.
2. [`README.md` — Roadmap section](../../README.md) — confirm the framework bullets were removed without losing the feature-level roadmap items, and the cross-link to the table resolves.
