---
title: 'Close two Story 3.7 deferred-debt items: Sankey canvas overflow and dashboard per-epic text-equivalent'
type: 'chore'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
baseline_commit: d8ee85e
---

## Intent

**Problem:** Story 3.7's 2026-07-09 code review deferred two items: `Charts.RequirementFlow`'s Sankey height stays pinned at a fixed constant even once its `unitH` hits a 2px floor at large requirement counts (overflowing the header/footer), and the dashboard requirements panel has no per-epic/per-status text-equivalent for the breakdown a sighted user gets by hovering the Sankey's ribbons — unlike `requirements.html`'s requirement cards.

**Approach:** Grow `RequirementFlow`'s SVG canvas to the tallest actual column instead of a fixed constant, and add a new visually-hidden (`sr-only`) per-epic × per-status breakdown list wired into the dashboard requirements panel outside the Flow/Grid tab toggle so it's always in the accessibility tree.

## Suggested Review Order

**Sankey canvas overflow fix**

- `RequirementFlow` computes the tallest of the three node columns' actual plotted heights and grows `height`/`viewBox`/column-centering to that value instead of the fixed `usableH` constant; small projects (`usableH` still wins the `Max`) stay byte-identical.
  [`Charts.cs:2559`](../../src/SpecScribe/Charts.cs#L2559)

- Regression test at 200 requirements against one epic (past the ~150-req floor threshold), asserting the SVG height grows past the small-project baseline.
  [`ChartsTests.cs:2148`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2148)

**Dashboard per-epic text-equivalent**

- New `Charts.RequirementFlowTextEquivalent` renders an `sr-only` `<ul>` listing each covering epic's distinct-requirement count broken down by canonical implementation state (same order as the Sankey's state column). Blind Hunter review caught the epic parameter going unused (bare "Epic 1" instead of "Epic 1 (Foundation)", unlike the Sankey's own node tooltip) — patched to look up the epic title.
  [`Charts.cs:2709`](../../src/SpecScribe/Charts.cs#L2709)

- Wired into the dashboard requirements panel, outside both toggled `req-view-flow`/`req-view-grid` panes so it's exposed regardless of the selected tab.
  [`HtmlRenderAdapter.Dashboard.cs:360`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L360)

- Tests covering the per-epic/per-status breakdown content and the empty-requirements no-op case.
  [`ChartsTests.cs:2166`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2166)

- Both deferred items marked resolved with an evidence trail; one new item deferred (cosmetic local-logic duplication between `RequirementFlow` and `RequirementFlowTextEquivalent`, flagged by the same review pass).
  [`deferred-work.md:5`](../../_bmad-output/implementation-artifacts/deferred-work.md#L5)
