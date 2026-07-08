---
title: 'Triage & close Epic 1 deferred heatmap tech debt'
type: 'bugfix'
created: '2026-07-08'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'caef62540ce27ac436c8a2cf3d29e78ba40b778c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A retro action item (Epic 1 & Epic 2) asks to triage three deferred heatmap debt items before Epic 3 Story 3.2/3.5. Investigation shows only one is still open: (1) `Charts.HeatLevel` returns level 4 whenever the busiest day has ≤1 commit, so a uniform one-commit-per-day history paints every cell at max intensity — a visual-truthfulness violation. The other two are already fixed: (2) unmapped `ForEpic` classes dropping from the Epic Status donut was resolved 2026-07-06 (`spec-sunburst-epic-focus-and-ready-rollup`); (3) non-invariant heatmap dates were resolved when the heatmap was refactored to route all dates through the invariant `Charts.D`/`DReadable` helpers.

**Approach:** Fix the open bug so uniform low activity reads light, not maxed-out. Add proportionate regression guards for the two already-fixed items so they cannot silently come back — the goal is to *close* debt that has outlived two retrospectives, not re-observe it — then reconcile `deferred-work.md`.

## Boundaries & Constraints

**Always:** Charts stay pure inline SVG + CSS, no JS (`[[charting-is-pure-svg-no-js]]`). Route all status→color through `StatusStyles` and all heatmap dates through invariant helpers (`[[specscribe-status-token-system]]`). Preserve the anti–"green-creep" truthfulness rule. Keep the existing level-0..4 five-bucket legend and CSS unchanged.

**Ask First:** Whether to also flip the two retros' Action Item #1 and the `sprint-status.yaml` action-item entries to a closed/in-progress status (out of code scope; propose, don't assume).

**Never:** Do not rebuild the heatmap scale into a different bucketing model, add new heat levels, or change the `--status-*` / heat-level CSS tokens. Do not touch Epic 3 story files. Do not window/cap git history (separate deferred item, its own intent).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Uniform single-commit history | every active day count == 1 (maxCount == 1) | active cells render `level-1` (light), never `level-4` | N/A |
| Normal graded history | maxCount ≥ 2 | ratio buckets unchanged (0.25/0.5/0.75 → level 1/2/3/4) | N/A |
| Empty / zero-commit day | count == 0 | `level-0` (unchanged) | N/A |
| All five epic roll-up classes present | epics rolling up to done/active/ready/drafted/pending | Epic Status donut buckets all five; no epic silently dropped | N/A |
| Non-invariant ambient culture | heatmap rendered under a non-Gregorian culture | cell titles + month labels format identically to invariant | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs:685` -- `HeatLevel(count, maxCount)`: the `maxCount <= 1 → return 4` collapse (the open bug). Dates already route through `D`/`DReadable` (`:662`, `:666`), both `InvariantCulture` — item (3) already fixed here.
- `src/SpecScribe/StatusStyles.cs:49,54,67` -- `StoryStages` (existing single-source pattern to mirror), `ForEpic` (5 outputs), `EpicLabel`. Add an `EpicStages` list as the single source of the five epic classes.
- `src/SpecScribe/HtmlTemplater.cs:317-333` -- `AppendEpicStatusPanel`: hardcodes the five donut buckets. Refactor to iterate `StatusStyles.EpicStages` so a future `ForEpic` tier can't silently drop.
- `tests/SpecScribe.Tests/ChartsTests.cs` -- heatmap test conventions (assert on `level-N` / `<title>` strings).
- `tests/SpecScribe.Tests/StatusStylesTests.cs` -- `ForEpic`/`EpicLabel` coverage to extend.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- ledger to reconcile.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- in `HeatLevel`, change the `maxCount <= 1` branch to `return 1;` (was `4`); update the adjacent comment to state a uniform single-commit history reads as light, not maxed. Rationale: closes item (1).
- [x] `src/SpecScribe/StatusStyles.cs` -- add `public static readonly IReadOnlyList<string> EpicStages = new[] { "done", "active", "ready", "drafted", "pending" };` with an XML-doc note mirroring `StoryStages`, as the single source of `ForEpic`'s output set. Rationale: hardens item (2).
- [x] `src/SpecScribe/HtmlTemplater.cs` -- rewrite `AppendEpicStatusPanel`'s five hardcoded segment tuples to iterate `StatusStyles.EpicStages` (`EpicLabel(stage)`, `Count(stage)`, `stage`), preserving current order and output. Rationale: makes the silent-drop regression structurally impossible.
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- add tests: uniform single-commit series renders `heatmap-cell level-1` and contains no `level-4`; a graded series still produces `level-4` for its busiest day; add an invariant-date guard asserting a known date's cell `<title>` equals `DReadable(day)` and month label equals the invariant `MMM`. Rationale: pins items (1) and (3).
- [x] `tests/SpecScribe.Tests/StatusStylesTests.cs` -- add a test that every value `ForEpic` can return is a member of `EpicStages`, and that `EpicLabel` returns a non-empty label for each `EpicStages` member. Rationale: guards item (2).
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark the `HeatLevel` item resolved (source: this spec), confirm the `ForEpic` item resolved-and-hardened, and record the heatmap date-formatting item resolved (refactored to invariant `D`/`DReadable`). Rationale: stops the item recurring across retros.

**Acceptance Criteria:**
- Given a repo whose busiest day has exactly one commit, when the heatmap renders, then every active cell is `level-1` and no cell is `level-4`.
- Given a repo with at least one 2+-commit day, when the heatmap renders, then the existing ratio-based levels are unchanged and the busiest day is `level-4`.
- Given epics rolling up to all five `ForEpic` classes, when the Epic Status donut renders, then all five are bucketed and the segment total equals the epic count (no silent drop), driven by `EpicStages`.
- Given the full test suite, when `dotnet test` runs, then all tests pass with zero regressions and the new guards are present.

## Design Notes

`HeatLevel` is relative (busiest day = darkest). The bug is the degenerate case where the busiest day is a single commit: relative scaling then paints a sparse project as maximally busy. `return 1` keeps a single commit reading as light activity consistently — in a busy repo a 1-commit day already lands at `level-1` (ratio ≤ 0.25). The branch is only reachable when `count == maxCount == 1` (future days are suppressed upstream; `count ≤ maxCount` always). `EpicStages` mirrors the existing `StoryStages` one-authored-list pattern, converting "resolved but re-regressable" into "cannot drift."

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests` -- expected: all tests pass, including the new heatmap-level, invariant-date, and `EpicStages` guards.
- `dotnet build` -- expected: clean build, no warnings introduced.

## Suggested Review Order

**Heatmap truthfulness fix (the one open bug)**

- Entry point — the collapse fix: a uniform ≤1-commit history now reads light (level 1), not maxed.
  [`Charts.cs:692`](../../src/SpecScribe/Charts.cs#L692)

**Epic-status drop-proofing (hardening the resolved `ForEpic` item)**

- Single authored list of `ForEpic`'s five outputs — mirrors `StoryStages`, the anti-drift source.
  [`StatusStyles.cs:71`](../../src/SpecScribe/StatusStyles.cs#L71)

- Donut now iterates `EpicStages` instead of five hardcoded buckets, so a future tier can't silently drop.
  [`HtmlTemplater.cs:329`](../../src/SpecScribe/HtmlTemplater.cs#L329)

**Regression guards (tests)**

- Uniform single-commit → light, and graded history still reaches level 4.
  [`ChartsTests.cs:415`](../../tests/SpecScribe.Tests/ChartsTests.cs#L415)

- Invariant date formatting pinned (verifies item 3 stays resolved).
  [`ChartsTests.cs:454`](../../tests/SpecScribe.Tests/ChartsTests.cs#L454)

- Binds `ForEpic` outputs ↔ `EpicStages` both directions, with distinct-label check.
  [`StatusStylesTests.cs:67`](../../tests/SpecScribe.Tests/StatusStylesTests.cs#L67)
