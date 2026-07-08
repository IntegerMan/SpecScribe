---
title: 'Represent task and subtask counts consistently'
type: 'bugfix'
created: '2026-07-07'
status: 'done'
route: 'one-shot'
review_loop_iteration: 0
context: []
---

# Represent task and subtask counts consistently

## Intent

**Problem:** Story task tallies (`StoryInfo.TasksDone`/`TasksTotal`) were computed by scanning every `- [x]`/`- [ ]` checkbox line in a story artifact regardless of section or indentation — so the "tasks" figure shown on the home page, epic page, story task badges, and the sunburst outer ring was actually the combined tasks+subtasks (and any stray checkboxes under other headings) count, not the task count. The per-story `TaskSunburst` center number had the same problem: it summed tasks and subtasks together but labeled the result "tasks".

**Approach:** `ProgressCalculator.ReadArtifactProgress` now uses the existing `TaskListParser` (already used for the per-story task/subtask breakdown) to count only top-level tasks under the `## Tasks` heading. `Charts.TaskSunburst`'s center number now shows the top-level task tally too, consistent with every other page; subtask completion stays visible via the outer ring and hover tooltips instead of being folded into the headline number.

## Suggested Review Order

**Aggregate task tally (home page, epic page, sunburst outer ring)**

- Switches from a flat checkbox line-scan to `TaskListParser`, which scopes to the `## Tasks` heading and separates top-level tasks from indented subtasks.
  [`ProgressCalculator.cs:71`](../../src/SpecScribe/ProgressCalculator.cs#L71)

**Per-story task sunburst (tasks ring + subtasks ring)**

- Center headline switches from combined tasks+subtasks to top-level task tally only, matching the rest of the app.
  [`Charts.cs:360`](../../src/SpecScribe/Charts.cs#L360)
- aria-label and rendered center text updated to the same top-level tally.
  [`Charts.cs:367`](../../src/SpecScribe/Charts.cs#L367)
  [`Charts.cs:395`](../../src/SpecScribe/Charts.cs#L395)
