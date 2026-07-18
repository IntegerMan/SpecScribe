---
title: '9.13 deferred — glance epic peer weight, no-plan middle ring, source-key unify'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: '8b54167778de81144a2e907a05ff4293a70a04ba'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** (1) Project-glance epic wedges still size only from story weights, so epics whose remaining work is mostly epic-level actions / deferred / attributed quick-dev get thin sweeps that crush the outer open/done aggregates. (2) After the hierarchy rewrite dropped the no-plan outer create-story fringe, `TasksTotal == 0` stories still paint as solid status wedges beside planned peers. (3) Provenance key stripping is copy-pasted across Charts / Sprint cards / FindQuickDev, and FindQuickDev still carries a dead stem/`.md` branch.

**Approach:** Grow glance `EpicWeight` by the same non–story-child peer set EpicSunburst already uses; restore dashed `.sb-noplan` on the middle story ring (no outer CTA); route all strip/normalize through `FollowUpGeometry.NormalizeSourceKey` and delete the tautology. Mark the three 9.13 deferred-work bullets resolved.

## Boundaries & Constraints

**Always:**
- Project-glance `EpicWeight = max(1, Σ StoryWeight + ForEpicNumber.Count + EpicLevelDeferred.Count + ForEpic(QD).Count)` — same peer set as EpicSunburst `totalWeight` extras; do not add story-child deferred a second time.
- StoryWeight stays `max(1, TasksTotal + StoryChildDeferred.Count)` (9.7); outer open/done aggregates and group hrefs stay unchanged.
- `TasksTotal == 0` stories use middle-ring `.sb-noplan` (dashed/muted) with aria/title naming “no task plan yet”; keep the story href; no create-story command in the title and no outer task-fringe arc.
- Apply the no-plan middle-ring treatment on both project glance and epic detail story wedges so the grammar stays consistent.
- One public strip helper: `FollowUpGeometry.NormalizeSourceKey`; Charts `DeferredSourceSuffix`, Sprint `NormalizeCardSourceKey`, and `FindQuickDev` call it (label layers stay local). Parser may map empty→null on top of the same strip.
- Delete the tautological `stem+".md" == bare+".md" && stem == bare` branch in `FindQuickDev`.
- Mark the three open 9.13 deferred-work bullets resolved with this spec key.

**Ask First:**
- Soft-cap / density clamp on peer-inflated epic wedges (otherwise leave unbounded like EpicSunburst peers).
- Reintroducing a create-story command string inside the no-plan tooltip.
- Changing `StoryWeight` floor or omitting `TasksTotal == 0` wedges entirely.

**Never:**
- Reintroduce the outer task-ring / create-story fringe arc removed by the hierarchy rewrite.
- Double-count story-child deferred inside glance `EpicWeight`.
- Absorb Story 10.7 drill-down / progressive density work.
- Change authoring schema, group-page membership, leaf hrefs, or ledger StatCard sources.
- Path-prefix stripping / `\`→`/` rewrites (separate deferred items).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Follow-up-heavy epic | Epic A: tasks thin, many epic-level actions/deferred/QD; Epic B: large TasksTotal, zero peers | A’s glance epic sweep visibly larger than task-only sizing would give; outer aggregates share A’s wider arc | N/A |
| Story-child only | Nested deferred under a story; no epic-level peers | Weight still from StoryWeight only — no double-count | N/A |
| Tasks-only peers | Two epics, different TasksTotal, no follow-ups | Relative sizing unchanged from today’s story-sum behavior | N/A |
| No-plan story | `TasksTotal == 0` on glance and epic detail | Middle path has `sb-noplan`; title/aria mention no task plan; still links to story page | N/A |
| Planned story | `TasksTotal > 0` | Status-colored middle wedge; no `sb-noplan` | N/A |
| Normalize + FindQuickDev | Keys with backticks / `.md` / `.html`; stem match | Same match as today via shared helper; dead `.md` branch gone | Null/whitespace → no match |
| Card/suffix labels | Story id or `spec-*` source keys | Still render `Story N.M` / spec labels after shared strip | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` — `Sunburst` `EpicWeight`; `AppendWeightedStorySlot` story class; `EpicSunburst` inline story path; `DeferredSourceSuffix`; `CountEpicFollowUpAggregates` (reference only)
- `src/SpecScribe/FollowUpGeometry.cs` — `NormalizeSourceKey`, `FindQuickDev`, `EpicLevelDeferred`, `ForEpicNumber` / story-child helpers
- `src/SpecScribe/UnplannedWorkGeometry.cs` — `ForEpic` (QD peers); already calls `NormalizeSourceKey`
- `src/SpecScribe/SprintTemplater.cs` — `NormalizeCardSourceKey`
- `src/SpecScribe/DeferredWorkParser.cs` — optional `NormalizeProvenanceKey` → shared strip
- `src/SpecScribe/assets/specscribe.css` — existing `.sb-noplan` rules
- `tests/SpecScribe.Tests/ChartsTests.cs` — weight + no-plan assertions (today assert `sb-noplan` absence)
- `tests/SpecScribe.Tests/` — add/extend FindQuickDev / NormalizeSourceKey coverage; Sprint/Charts label regressions
- `_bmad-output/implementation-artifacts/deferred-work.md` — resolve the three 9.13 bullets

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- Add epic-level peer counts into glance `EpicWeight`; paint `sb-noplan` for `TasksTotal == 0` in `AppendWeightedStorySlot` and EpicSunburst story paths; route `DeferredSourceSuffix` through `NormalizeSourceKey` -- close weight + no-plan deferrals
- [x] `src/SpecScribe/FollowUpGeometry.cs` -- `FindQuickDev` uses `NormalizeSourceKey`; delete tautological stem/`.md` branch -- single strip path
- [x] `src/SpecScribe/SprintTemplater.cs` -- `NormalizeCardSourceKey` strips via shared helper; keep Story-label layer -- dedupe
- [x] `src/SpecScribe/DeferredWorkParser.cs` -- Optional: `NormalizeProvenanceKey` delegates strip to shared helper (preserve null-vs-empty) -- stop parallel copies
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- Assert follow-up-heavy epic grows glance sweep vs task-only peer; assert `sb-noplan` present for empty-task stories (flip prior absence asserts); keep story-child no-double-count -- lock I/O matrix
- [x] `tests/SpecScribe.Tests/` -- Unit NormalizeSourceKey + FindQuickDev stem/`.md`/`.html`; Sprint/Charts label still correct -- lock cleanup
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Mark the three 9.13 items resolved with date + this spec key -- close the deferral ledger

**Acceptance Criteria:**
- Given a project glance where one epic is follow-up-heavy (epic-level peers) and task-light beside a task-heavy peer with no peers, when rendered, then the follow-up-heavy epic’s angular sweep is larger than story-weights alone would produce and its open/done aggregate shares that wider arc.
- Given nested story-child deferred only (no epic-level peers), when glance weight is computed, then those nested slots are not added again on top of StoryWeight.
- Given a story with `TasksTotal == 0` on project glance or epic detail, when rendered, then its middle wedge uses `.sb-noplan` and names “no task plan yet”, with no outer create-story fringe.
- Given provenance keys needing strip/normalize, when Charts / Sprint cards / FindQuickDev match, then they share one helper and FindQuickDev has no dead stem/`.md` branch.
- Given the three open 9.13 deferred-work bullets, when this work lands, then all three are marked resolved in `deferred-work.md`.

## Design Notes

**Glance EpicWeight (mirror EpicSunburst peers, no double-count):**
```csharp
int EpicWeight(EpicInfo e) => Math.Max(1,
    e.Stories.Sum(s => StoryWeight(e, s))
    + geometry.ForEpicNumber(e.Number).Count
    + geometry.EpicLevelDeferred(e.Number, e.Stories.Select(s => s.Id)).Count
    + unplannedGeo.ForEpic(e.Number).Count);
```

**No-plan middle ring:** reuse existing `.sb-noplan` CSS on the story path when `TasksTotal == 0`; hierarchy Never still forbids the removed outer task fringe.

## Verification

**Commands:**
- `dotnet test` -- all tests green, including new weight / no-plan / normalize cases
- Grep `deferred-work.md` for the three 9.13 summaries -- all marked RESOLVED

**Manual checks (if no CLI):**
- Generate site; open Home glance — follow-up-heavy epic wedge wider; empty-task stories dashed on middle ring.

## Suggested Review Order

**Glance epic weight**

- Peer set mirrors EpicSunburst; story-child deferred stays only in StoryWeight.
  [`Charts.cs:179`](../../src/SpecScribe/Charts.cs#L179)

**No-plan middle ring**

- Empty-task stories paint `sb-noplan` with “no task plan yet” (no outer CTA).
  [`Charts.cs:435`](../../src/SpecScribe/Charts.cs#L435)

- Same treatment on epic-detail story wedges.
  [`Charts.cs:616`](../../src/SpecScribe/Charts.cs#L616)

- Legend + hover isolation when any no-plan wedge exists.
  [`Charts.cs:533`](../../src/SpecScribe/Charts.cs#L533)

- Dashed swatch + `:has` dimming for the new legend entry.
  [`specscribe.css:2504`](../../src/SpecScribe/assets/specscribe.css#L2504)

**Source-key unify**

- Repeated `.md`/`.html` strip (compound suffixes).
  [`FollowUpGeometry.cs:211`](../../src/SpecScribe/FollowUpGeometry.cs#L211)

- FindQuickDev uses shared strip; dead stem/`.md` branch gone.
  [`FollowUpGeometry.cs:395`](../../src/SpecScribe/FollowUpGeometry.cs#L395)

- Charts suffix + Sprint card labels + parser empty→null.
  [`Charts.cs:496`](../../src/SpecScribe/Charts.cs#L496)
  [`SprintTemplater.cs:633`](../../src/SpecScribe/SprintTemplater.cs#L633)
  [`DeferredWorkParser.cs:223`](../../src/SpecScribe/DeferredWorkParser.cs#L223)

**Tests & ledger**

- Action peers grow glance epic sweep.
  [`ChartsTests.cs:2468`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2468)

- Epic-level deferred peers grow glance epic sweep.
  [`ChartsTests.cs:2516`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2516)

- Nested deferred not double-counted vs tasks-only peer epic.
  [`ChartsTests.cs:2576`](../../tests/SpecScribe.Tests/ChartsTests.cs#L2576)

- Normalize + FindQuickDev unit coverage.
  [`FollowUpGeometryNormalizeTests.cs:16`](../../tests/SpecScribe.Tests/FollowUpGeometryNormalizeTests.cs#L16)

- Three 9.13 deferred bullets marked resolved.
  [`deferred-work.md:45`](./deferred-work.md#L45)
