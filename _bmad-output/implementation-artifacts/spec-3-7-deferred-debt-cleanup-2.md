---
title: 'Epic 3 Deferred-Debt Cleanup — Round 2'
type: 'refactor'
created: '2026-07-20'
status: 'done'
route: 'one-shot'
---

# Epic 3 Deferred-Debt Cleanup — Round 2

## Intent

**Problem:** `deferred-work.md` listed five open Epic 3 items. Investigation showed four were already resolved by the prior `spec-3-7-deferred-debt-cleanup` pass (commit `4fa0b3f`) — Sankey height scaling, the dashboard per-epic text-equivalent, and the Story 3.6 review items — just never marked resolved in the tracking doc. The one genuinely open item was that same pass's own review follow-up: `Charts.RequirementFlowTextEquivalent` duplicated `RequirementFlow`'s `Sentinel`/`NoCoverage`/coverage-partition/epic-label logic.

**Approach:** Extracted the duplicated logic into five shared private static members on `Charts` (`NoCoverageKey`, `NoCoverage`, `CoverageKeys`, `ForCoverageKey`, `EpicTitlesByNumber`) used by both `RequirementFlow` and `RequirementFlowTextEquivalent`, then closed out the four stale tracking entries in `deferred-work.md` with resolution notes pointing at the commit/spec that already shipped the fix.

## Suggested Review Order

- Five new shared statics single-source the sentinel, no-coverage predicate, ordered coverage-key list, per-key membership, and epic-title lookup.
  [`Charts.cs:2502`](../../src/SpecScribe/Charts.cs#L2502)

- `RequirementFlow` now calls the shared helpers instead of its own local `Sentinel`/`NoCoverage`/inline epic-key and title-dict logic; `epicKeys` here still excludes the sentinel (unchanged semantics).
  [`Charts.cs:2575`](../../src/SpecScribe/Charts.cs#L2575)

- `RequirementFlowTextEquivalent` now calls the same shared helpers; its `coverageKeys` intentionally includes the sentinel (the breakdown lists "No coverage" as its own row) — renamed from `epicKeys` to make that different-from-its-sibling semantics explicit.
  [`Charts.cs:2761`](../../src/SpecScribe/Charts.cs#L2761)

- Four Epic 3 tracking entries struck through with resolution notes: the RequirementFlowTextEquivalent duplication (this change), and three already-shipped items from `spec-3-7-deferred-debt-cleanup` (Sankey height, dashboard text-equivalent) that were never marked resolved.
  [`deferred-work.md:9`](../../_bmad-output/implementation-artifacts/deferred-work.md#L9)
