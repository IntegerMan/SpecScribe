---
title: 'Close two Story 3.6 deferred-debt items: coverage-card focus tooltip and stale story doc'
type: 'chore'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
baseline_commit: 1edc996
---

## Intent

**Problem:** Story 3.6's 2026-07-09 code review deferred two items: the no-href `ArtifactCoveragePanel` present-card lost keyboard/AT access to its `js-tip` focus tooltip (and its justifying comment was wrong), and the story markdown's AC/Tasks/Dev Notes still described the pre-pivot funnel design instead of the shipped cumulative Story Pipeline.

**Approach:** Restore `tabindex="0"` on the no-href present-card branch in `Charts.ArtifactCoveragePanel` with an accurate comment, add a regression test, then rewrite the story's AC #1, Subtasks 1.2/1.3, and Dev Notes to describe the shipped Story Pipeline design (keeping the superseded pre-pivot text inline as marked history for traceability).

## Suggested Review Order

**Coverage-card focus fix**

- Restores `tabindex="0"` for the no-href present card and corrects the comment to name the memlog tooltip as the reason.
  [`Charts.cs:1553`](../../src/SpecScribe/Charts.cs#L1553)

- Regression test asserting the no-href present card keeps `tabindex="0"` and its `js-tip` markup.
  [`ChartsTests.cs:3126`](../../tests/SpecScribe.Tests/ChartsTests.cs#L3126)

**Doc-drift cleanup**

- AC #1, Subtask 1.2/1.3, and Dev Notes rewritten to describe the shipped cumulative Story Pipeline, with pre-pivot text kept inline as marked-superseded history.
  [`3-6-refinement-funnel-on-the-dashboard.md:19`](../../_bmad-output/implementation-artifacts/3-6-refinement-funnel-on-the-dashboard.md#L19)

- Both deferred items marked resolved with an evidence trail.
  [`deferred-work.md:293`](../../_bmad-output/implementation-artifacts/deferred-work.md#L293)
