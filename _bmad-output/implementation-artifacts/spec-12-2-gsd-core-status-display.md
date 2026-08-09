---
title: 'Repair GSD Core lifecycle display when task tallies are absent'
type: 'bugfix'
created: '2026-08-08'
status: 'done'
review_loop_iteration: 0
baseline_commit: ad924ae647e894cb3a808d956cf3fba4f6ea5b15
context:
  - '{project-root}/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GSD Core plans correctly ingest their ROADMAP checkbox lifecycle, but every zero-checklist plan is rendered as "No task plan". In CORA this hides all completed plans behind the no-plan treatment.

**Approach:** Make display classification distinguish an absent task tally from an absent lifecycle. An explicit canonical status wins; no-plan remains reserved for unclassified zero-task stories.

## Boundaries & Constraints

**Always:** Preserve ROADMAP-derived GSD status; keep existing null-status no-plan behavior and geometry; use the shared `StatusStyles` seam; test only with temp fixtures or in-memory models.

**Ask First:** Change GSD parsing, synthesize tasks from XML blocks, alter no-plan geometry, or introduce a new lifecycle status.

**Never:** Depend on `C:\Dev\CORA` in automated tests, replace explicit lifecycle data with task-tally inference, or broaden into unrelated status refactoring.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Explicit lifecycle, no tally | `Status = "done"`, `TasksTotal = 0` | Done styling and lifecycle wording; no no-plan classification | N/A |
| No lifecycle, no tally | `Status = null`, `TasksTotal = 0` | Existing no-plan styling and "No task plan yet" wording | N/A |
| Normal tracked story | Explicit status with tasks | Existing lifecycle and progress behavior unchanged | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/StatusStyles.cs` -- canonical story lifecycle classification seam.
- `src/SpecScribe/SunburstExplorer.cs` -- dashboard/project hierarchy story-node status.
- `src/SpecScribe/HierarchyExplorer.Projectors.cs` and `src/SpecScribe/HierarchyExplorer.cs` -- hierarchy details and labels.
- `src/SpecScribe/SprintTemplater.cs` and `src/SpecScribe/RelatedWorkCards.cs` -- sprint and selected-story no-plan presentation.
- `tests/SpecScribe.Tests/*ExplorerTests.cs`, `SprintTemplaterTests.cs`, and `RelatedWorkTests.cs` -- regression coverage.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/StatusStyles.cs` -- expose the shared display-status decision: explicit status takes precedence over a zero tally.
- [x] Hierarchy, sprint, and related-work projection files -- consume the shared decision and use checklist-absence wording when lifecycle is known.
- [x] Focused test files -- cover explicit-done/zero-task and null-status/zero-task cases on each affected surface.

**Acceptance Criteria:**
- Given a GSD-derived story with `Status = "done"` and no Markdown tasks, when dashboard, epic hierarchy, sprint, or related-work views render, then it is shown as done and is not labelled as having no task plan.
- Given a zero-task story without explicit lifecycle status, when those views render, then the current no-plan treatment remains intact.
- Given an explicit lifecycle story with a normal task tally, when its views render, then existing lifecycle and progress output remains unchanged.

## Spec Change Log

## Design Notes

`TasksTotal == 0` represents unavailable Markdown checklist data, not plan existence. GSD Core exposes plan lifecycle through the ROADMAP checkbox, so it is authoritative for status display while the zero tally remains honest.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~SunburstExplorerTests|FullyQualifiedName~HierarchyExplorerTests|FullyQualifiedName~SprintTemplaterTests|FullyQualifiedName~RelatedWorkTests"` -- expected: focused rendering regressions pass.
- `dotnet build SpecScribe.slnx` -- expected: zero errors.

## Suggested Review Order

**Lifecycle Classification**

- Establishes the single display decision: explicit lifecycle overrides an absent checklist.
  [StatusStyles.cs:32](../../src/SpecScribe/StatusStyles.cs#L32)

- Applies that decision to the dashboard hierarchy source nodes.
  [SunburstExplorer.cs:111](../../src/SpecScribe/SunburstExplorer.cs#L111)

**Surface Semantics**

- Preserves no-plan geometry while separating known-status checklist absence.
  [HierarchyExplorer.Projectors.cs:95](../../src/SpecScribe/HierarchyExplorer.Projectors.cs#L95)

- Keeps dashboard detail copy consistent with the lifecycle-aware classification.
  [HierarchyExplorer.cs:449](../../src/SpecScribe/HierarchyExplorer.cs#L449)

- Removes sprint no-plan styling only when the matched story has a lifecycle.
  [SprintTemplater.cs:527](../../src/SpecScribe/SprintTemplater.cs#L527)

- Preserves related-work lifecycle labels while refining zero-task detail copy.
  [RelatedWorkCards.cs:284](../../src/SpecScribe/RelatedWorkCards.cs#L284)

**Regression Coverage**

- Pins the shared classifier's explicit-status and absent-status boundary.
  [StatusStylesTests.cs:46](../../tests/SpecScribe.Tests/StatusStylesTests.cs#L46)

- Covers dashboard, epic hierarchy, sprint, and related-work rendering behavior.
  [HierarchyExplorerTests.cs:207](../../tests/SpecScribe.Tests/HierarchyExplorerTests.cs#L207)
  [SprintTemplaterTests.cs:479](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs#L479)
  [RelatedWorkTests.cs:663](../../tests/SpecScribe.Tests/RelatedWorkTests.cs#L663)