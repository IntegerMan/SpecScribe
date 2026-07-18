---
title: '9.7 deferred: story-child weight + FollowUpGeometry ledger assert'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: d7e8c2f3f93b5b9e112073d528b848deb0faa1ff
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** (1) Story wedges stay `max(1, TasksTotal)` while nested story-child deferred share that thin outer arc, so crowded parents never grow. (2) `FollowUpGeometry.From` documents ledger agreement with `ProjectCounts` but never asserts open action-item tallies at build time.

**Approach:** Grow story (and thus epic) angular weight by nested story-child deferred count; add a `Debug.Assert` that open action items from the list match `ProjectCounts.OpenActionItems`. Mark both 9.7 deferred-work entries resolved when done.

## Boundaries & Constraints

**Always:**
- Story weight formula becomes `max(1, TasksTotal + storyChildDeferredCount)` wherever stories are sized for sunburst sweep (project glance + epic detail).
- Epic weight remains the sum of story weights (plus existing epic-level peers already counted in `EpicSunburst.totalWeight`).
- Ledger open action-item count must match filtered `!IsDone` list length; follow existing `ProjectCounts.Build` `Debug.Assert` pattern.
- Equal split of outer children under a parent remains; only the parent sweep grows.
- Update hint/docs that still say stories are sized by tasks only when nested deferred are present.

**Ask First:**
- Changing project-glance epic weight to also include epic-level follow-up / action-item slot counts (that is the separate 9.13 deferral).
- Throwing (non-Debug) on ledger mismatch in Release builds.

**Never:**
- Equate `DeferredItems.Count` (or all-slot count) to `DeferredOpenItems` — aggregate wedge and resolved slots are legitimate.
- Reintroduce task / `.sb-noplan` fringe wedges.
- Absorb the 9.13 “epic weight ignores follow-up slots” or “TasksTotal==0 looks planned” deferrals.
- Change authoring schema, parsers, or StatCard ledger sources.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Crowded thin story | Epic detail: story A TasksTotal=1 with 6 story-child deferred; peer B TasksTotal=1 with 0 | A’s middle sweep ≫ B’s; outer children share A’s larger arc | N/A |
| Tasks-only sizing | Two stories, TasksTotal 12 vs 0, no nested deferred | Still ~12× vs weight 1 (unchanged) | N/A |
| Project glance | Same nested deferred; `nestStoryChildren:false` | Story/epic weight still includes nested count (thicker epic + aggregate); no per-story outer leaves | N/A |
| Zero nested | Story with tasks, zero story-child deferred | Weight stays `max(1, TasksTotal)` | N/A |
| Ledger agree | List has 2 open + 1 done; `OpenActionItems=2` | `From` succeeds; done wedges still available | N/A |
| Ledger mismatch | List open count ≠ `counts.OpenActionItems` | `Debug.Assert` fails (Debug builds) | Silent in Release |
| Aggregate deferred | `DeferredOpenItems>0`, empty model → 1 aggregate slot | No deferred-count assert failure; `DeferredOpenCount` from ledger | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` -- `Sunburst` / `EpicSunburst` `StoryWeight`; epic hint “sized by tasks”; nesting via `AppendWeightedStorySlot` / inline outer children
- `src/SpecScribe/FollowUpGeometry.cs` -- `From`, `OpenActionItems`, `StoryChildDeferred(epic, storyId)`, deferred aggregate path
- `src/SpecScribe/ProjectCounts.cs` -- `Debug.Assert` pattern to mirror; `OpenActionItems` / `DeferredOpenItems` sources
- `tests/SpecScribe.Tests/ChartsTests.cs` -- weight / story-child deferred coverage; add sweep-relative assert
- `tests/SpecScribe.Tests/` -- FollowUpGeometry / Charts happy paths; add Debug mismatch coverage if feasible
- `_bmad-output/implementation-artifacts/deferred-work.md` -- strike/resolve the two 9.7 entries
- `_bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md` -- optional note on resolved deferrals

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- Include `geometry.StoryChildDeferred(...).Count` in `StoryWeight` for both sunbursts; refresh epic hint copy when nested deferred affect sizing -- parent sweep grows with nested crowding
- [x] `src/SpecScribe/FollowUpGeometry.cs` -- After building slots, `Debug.Assert` open-from-list == `counts.OpenActionItems` (message with both values); do not assert deferred slot count vs ledger -- close the documented ledger gap
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- Assert crowded thin story gets larger sweep than equal-task peer; keep existing tasks-only ~12× behavior -- lock I/O matrix
- [x] `tests/SpecScribe.Tests/` (FollowUpGeometry or Charts) -- Happy-path ledger agree + aggregate deferred still builds; Debug mismatch covered if the suite already has Debug-assert patterns -- lock assert semantics
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Mark both 9.7 items resolved with date + short note -- close the deferral ledger

**Acceptance Criteria:**
- Given an epic sunburst where one story has many story-child deferred and a peer has the same `TasksTotal` but none, when rendered, then the crowded story’s angular sweep is larger and its outer children share that larger parent arc.
- Given project glance with nested deferred under a thin story, when rendered, then that story’s weight (and parent epic weight) includes the nested count even though leaves are aggregated.
- Given `FollowUpGeometry.From` with a full action-item list, when open filtered count disagrees with `ProjectCounts.OpenActionItems`, then Debug builds assert; Release still returns geometry.
- Given aggregate deferred (ledger open, no parseable slots), when `From` runs, then one navigable aggregate slot is emitted and no deferred-count assert fires.
- Given the two open 9.7 deferred-work bullets, when this work lands, then both are marked resolved in `deferred-work.md`.

## Design Notes

**Weight (both charts):**
```csharp
int StoryWeight(StoryInfo s) =>
    Math.Max(1, s.TasksTotal + geometry.StoryChildDeferred(epicNumber, s.Id).Count);
```
Project glance already folds story weights into `EpicWeight`; epic detail already adds epic-level peers separately — do not double-count story-child deferred as peers.

**Assert:** Mirror `ProjectCounts.Build` — `using System.Diagnostics; Debug.Assert(...)`. Only open action items. Deferred open tallies stay ledger-sourced; slot cardinality is intentionally independent.

## Verification

**Commands:**
- `dotnet test` -- all tests green, including new weight + ledger cases
- Grep `deferred-work.md` for the two 9.7 summaries -- both marked RESOLVED

**Manual checks (if no CLI):**
- Generate site / open an epic with crowded story-child deferred; confirm parent wedge visibly larger than a same-task peer without nested deferred.

## Suggested Review Order

**Story angular weight**

- Parent epic number drives nested count so glance weight matches hint gating.
  [`Charts.cs:176`](../../src/SpecScribe/Charts.cs#L176)

- Same formula on epic detail; nested children share the grown parent sweep.
  [`Charts.cs:585`](../../src/SpecScribe/Charts.cs#L585)

- Hint copy switches when any story-child deferred exists.
  [`Charts.cs:361`](../../src/SpecScribe/Charts.cs#L361)

**Ledger assert**

- Debug-only open-list vs `ProjectCounts.OpenActionItems`; deferred slots not equated.
  [`FollowUpGeometry.cs:120`](../../src/SpecScribe/FollowUpGeometry.cs#L120)

**Tests & deferral ledger**

- Epic detail: crowded vs peer sweep + child shares parent.
  [`ChartsTests.cs:2372`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2372)

- Project glance weight growth with aggregated leaves.
  [`ChartsTests.cs:2420`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2420)

- Happy-path ledger agree for open action items.
  [`ChartsTests.cs:2465`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2465)

- Both 9.7 deferrals marked resolved; new floor-swallow note deferred.
  [`deferred-work.md:5`](./deferred-work.md#L5)
