---
title: 'Sunburst remaining-work hierarchy'
type: 'feature'
created: '2026-07-17'
status: 'done'
baseline_commit: 98c994f06096c997590409c0fde921df561c5137
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Project and epic sunbursts bury hierarchy under a task fringe, paint deferred/follow-ups as story peers (so code-review debt never sits under its story), and leave many quick-dev items in Unplanned even when sprint timing points at an epic — so unplanned and deferred work look untrustworthy.

**Approach:** Drop tasks from rendered rings while weighting story wedges by task count; nest story-sourced deferred under their parent story; keep retro actions and attributed quick-dev as epic children; best-effort date windows to attach quick-dev to an epic when text cues fail.

## Boundaries & Constraints

**Always:**
- Keep ledger-agreed counts (Story 8.3); geometry membership must not double-count.
- No new authoring schema / frontmatter / yaml fields (Epic 9). Attribution from existing `source_spec`, headings, action-item `epic:`, and existing dates only.
- Both `Charts.Sunburst` and `Charts.EpicSunburst` get the same hierarchy rules (epic page has no project Unplanned root).
- Unique-only date attribution: ambiguous ties → leave Unplanned / unattributed (never guess).
- Prefer text/story-key cues over date heuristics for quick-dev epic placement.
- Distinct treatment for follow-up / direct-change wedges (labels + classes, never color-only).
- NFR8: omit empty synthetic roots and empty child rings.

**Ask First:**
- Changing sprint-board Unplanned lane membership beyond what sunburst attribution already implies.
- Any new visual token beyond existing `.sb-followup-*` / `.sb-unplanned` vocabulary.

**Never:**
- Reintroduce task wedges or `.sb-noplan` task fringe on either sunburst.
- Fabricate story parents for retro action items (yaml has epic only).
- New deferred-work.md or quick-dev frontmatter conventions.
- Touch Story Pipeline / refinement funnel / task-only `TaskSunburst`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Story sizing | Story with TasksTotal=12 beside TasksTotal=0 peer | Larger story takes ~12× angular weight vs max(1,0)=1 for empty | N/A |
| No tasks anywhere | All stories TasksTotal=0 | Equal min weight 1; no task fringe | N/A |
| CR deferred | Deferred `SourceStoryId`/`SourceKey` → story N.M | Outer-ring child under that story's sweep; not a middle peer | Bad key → epic-level or Unplanned via existing resolve |
| Retro action | `SprintActionItem` with `epic:` | Middle-ring epic child (peer of stories), not under a story | Missing epic → Follow-ups orphan |
| Quick-dev text | Filename/title names Story/Epic | Under that epic's middle ring | N/A |
| Quick-dev date | `AuthoredDate` uniquely matches one epic's retro `DateText` or a story `LastUpdatedDate` in one epic | Under that epic | Multi-epic / no date → Unplanned |
| Empty deferred children | Story has no nested deferred | No outer fringe on that sweep | N/A |
| Zero unplanned | No open unattributable QD/deferred | Omit Unplanned root | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` -- `Sunburst`, `EpicSunburst`, `AppendStorySlot`, task outer arcs, legend/hint builders
- `src/SpecScribe/FollowUpGeometry.cs` -- deferred slots, epic attribution; needs `SourceStoryId` (or equivalent) for nesting
- `src/SpecScribe/UnplannedWorkGeometry.cs` -- `ResolveQuickDevEpic`, timing cues; extend with retro/story dates
- `src/SpecScribe/DeferredWorkParser.cs` -- already yields `SourceStoryId` / `SourceKey` on groups
- `src/SpecScribe/WorkInventory.cs` -- `QuickDevEntry.AuthoredDate`
- `src/SpecScribe/ProgressCalculator.cs` / `StoryInfo` -- `TasksTotal`, `LastUpdatedDate`
- `src/SpecScribe/RetroModel.cs` -- `DateText` per epic retro
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` / `HtmlRenderAdapter.Epics.cs` -- sunburst call sites (signatures only if needed)
- `src/SpecScribe/assets/specscribe.css` -- `.sb-*`, hints; drop task-fringe assumptions in copy if any
- `tests/SpecScribe.Tests/ChartsTests.cs` -- ring/aria/href assertions
- `tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs` -- attribution
- Golden / parity tests as fingerprints move

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/FollowUpGeometry.cs` -- Carry parent story id on deferred slots from parser `SourceStoryId`/`SourceKey`; expose story-child vs epic-level deferred partitions -- nesting needs a parent id, not epic alone
- [x] `src/SpecScribe/UnplannedWorkGeometry.cs` -- After existing text cues, unique-day match `AuthoredDate` to epic retro `DateText` and/or stories' `LastUpdatedDate` in a single epic; ties stay null -- date-based quick-dev epic attribution
- [x] `src/SpecScribe/Charts.cs` -- Remove task/noplan outer rendering from both sunbursts; weight story middle wedges by `max(1, TasksTotal)`; draw story-child deferred in the outer ring under parent story sweeps; keep retro actions + attributed QD as epic middle peers; update legend/hint copy -- core visual hierarchy
- [x] `src/SpecScribe/HtmlRenderAdapter.*.cs` / `SiteGenerator.cs` -- Pass any new geometry inputs (e.g. retros for date resolve) without recounting ledgers -- plumbing only
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` + `UnplannedWorkGeometryTests.cs` (+ golden/parity as needed) -- Cover I/O matrix: no task arcs, weighted stories, nested deferred, date attribution unique/tie -- lock behavior

**Acceptance Criteria:**
- Given a project sunburst, when rendered, then no task done/remaining or `.sb-noplan` task wedges appear; story angular size reflects `max(1, TasksTotal)`.
- Given deferred with resolvable `SourceStoryId`, when project or epic sunburst renders, then that item is an outer-ring child under that story — not a middle-ring peer of stories.
- Given a retro action item with `epic: N`, when sunbursts render, then it appears as an epic-level middle child of epic N (never forced under a story).
- Given a one-shot quick-dev whose `AuthoredDate` uniquely matches one epic via retro date or story `LastUpdatedDate` (and text cues fail), when geometry builds, then it sits under that epic — not Unplanned.
- Given ambiguous multi-epic date ties or missing dates, when geometry builds, then the item stays Unplanned / unattributed.
- Given epic detail sunburst, when rendered, then the same nesting and sizing rules apply (no project Unplanned root).

## Design Notes

**Ring silhouette (both charts):**
1. Inner (project only): epics — weight from sum of weighted stories + epic-level middle members.
2. Middle: stories (task-weighted) + epic-level peers (retro actions, attributed quick-dev, epic-only deferred without story parent).
3. Outer: **only** story-child deferred under each parent story’s sweep; omit arcs with no children.

**Parenting rules:**
- Story child: deferred whose `SourceStoryId` (or `StoryIdFromKey(SourceKey)`) resolves to a known story.
- Epic child: action items with `EpicNumber`; quick-dev with resolved epic; deferred with epic but no story parent.
- Orphans: unchanged Follow-ups (unattributed actions) / Unplanned (unattributable open QD + deferred) roots.

**Quick-dev resolve order:** existing text/filename/story-mention/deferred-name cues → unique `AuthoredDate` vs epic `RetroModel.DateText` → unique `AuthoredDate` vs story `LastUpdatedDate` owned by one epic → else null.

## Verification

**Commands:**
- `dotnet test --filter "FullyQualifiedName~ChartsTests|FullyQualifiedName~UnplannedWorkGeometry"` -- expected: pass
- `dotnet test` -- expected: pass (incl. golden/parity after regen if hashes move)

**Manual checks:**
- Home + epics index: no task fringe; deferred from a story CR sits under that story; Unplanned shrinks when date attribution lands.
- Epic 6 (or any epic with open follow-ups): story-child deferred under stories; retro items as epic peers.

## Suggested Review Order

**Task-weighted rings (no task fringe)**

- Project sunburst: stories sized by `max(1, TasksTotal)`; no task outer arcs
  [`Charts.cs:159`](../../src/SpecScribe/Charts.cs#L159)

- Weighted story wedge + outer story-child deferred (single pad)
  [`Charts.cs:401`](../../src/SpecScribe/Charts.cs#L401)

- Epic sunburst mirrors the same hierarchy without Unplanned root
  [`Charts.cs:533`](../../src/SpecScribe/Charts.cs#L533)

**Deferred parenting**

- Partition story-child vs epic-level deferred via `SourceStoryId`
  [`FollowUpGeometry.cs:148`](../../src/SpecScribe/FollowUpGeometry.cs#L148)

**Quick-dev date attribution**

- Cascaded unique-day resolve: retro first, then story `LastUpdatedDate`
  [`UnplannedWorkGeometry.cs:226`](../../src/SpecScribe/UnplannedWorkGeometry.cs#L226)

**Plumbing**

- Retros passed into `UnplannedWorkGeometry.From` for date resolve
  [`SiteGenerator.cs:1891`](../../src/SpecScribe/SiteGenerator.cs#L1891)

- Dead `commands` arg removed from sunburst call sites
  [`HtmlRenderAdapter.Dashboard.cs`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)

**Tests**

- I/O matrix coverage: weighting, nesting, hints, unknown story id
  [`ChartsTests.cs:2001`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2001)

- Date cascade + tie/precedence coverage
  [`UnplannedWorkGeometryTests.cs:443`](../../tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs#L443)
