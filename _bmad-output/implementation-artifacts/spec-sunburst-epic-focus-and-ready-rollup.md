---
title: 'Project sunburst focuses on epics and shows the ready-for-dev rollup'
type: 'bugfix'
created: '2026-07-06'
status: 'done'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The project sunburst's center reads "35 STORIES" when the chart's headline should be the 7 epics it is organized around. Separately, an epic whose stories are only *ready-for-dev* (none yet in development) rolls up to the "drafted" color/label instead of "ready for dev", so `StatusStyles.ForEpic` never reports the ready tier that the story layer already distinguishes.

**Approach:** Change the project sunburst center to report the epic count with an "epic(s)" label, and add a `ready` tier to the epic status ladder (between `active` and `drafted`) with a matching label and the two missing CSS variants, so an epic with ready stories reads as ready everywhere `ForEpic` is consumed.

## Boundaries & Constraints

**Always:** Keep charts pure inline SVG + CSS — no JS. `ForEpic` stays the single source of truth; all consumers (epic pages, requirements, BMAD suggestions) keep working. Preserve the anti–"green-creep" rule: ready/drafted read gold, never green. Ready rolls up on *any* ready story when none is further along, mirroring the existing "any active → active" rule.

**Ask First:** Changing the epic-ring weighting, ring structure, or removing the story/task rings (only the center text and epic color change here).

**Never:** Altering story-level or requirement-level status semantics. Introducing a new "ready" shade for epic chips/badges — those intentionally share gold with drafted; only the sunburst ring shade and the epic label text change.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sunburst center | Model with 7 epics / 35 stories | Center num = `7`, label = `epics` | N/A |
| Single epic | Model with 1 epic | Center num = `1`, label = `epic` (singular) | N/A |
| Epic ready rollup | Epic, stories ready-for-dev, none in dev | `ForEpic` → `ready`; label "Ready for dev"; sunburst ring uses `sb-ready` (gold) | N/A |
| Mixed drafted + ready | Epic with one null-status story + one ready story | `ForEpic` → `ready` | N/A |
| Truly drafted | Epic, all stories drafted/unknown, none ready | `ForEpic` → `drafted` (unchanged) | N/A |
| Any story in dev/review/done | Epic with a ready story + an in-dev story | `ForEpic` → `active` (unchanged) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` -- `Sunburst` center text (lines ~183–185); switch from story total to epic count.
- `src/SpecScribe/StatusStyles.cs` -- `ForEpic` ladder + `EpicLabel`; add the `ready` tier.
- `src/SpecScribe/assets/specscribe.css` -- add `.epic-status.ready` and `.epic-chip.ready` (share the existing gold `drafted` styling). `.status-badge.ready` and `.sb-ready` already exist.
- `tests/SpecScribe.Tests/StatusStylesTests.cs` -- update the drafted/ready rollup expectations; add EpicLabel("ready") coverage.
- `tests/SpecScribe.Tests/ChartsTests.cs` -- assert the sunburst center reports epic count + "epics".
- `src/SpecScribe/BmadCommands.cs` / `EpicsTemplater.cs` / `RequirementsParser.cs` -- consumers of `ForEpic`; verify only, no change needed (fallback handles `ready`; requirement rollup only branches on `"done"`).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/StatusStyles.cs` -- in `ForEpic`, after the `active` check add `if (storyClasses.Any(c => c == "ready")) return "ready";` before the `drafted` fallback; add `"ready" => "Ready for dev"` to `EpicLabel`.
- [x] `src/SpecScribe/Charts.cs` -- in `Sunburst`, replace the `storiesTotal` center number with `epics.Count` and the label `stories` with `Plural(epics.Count, "epic", "epics")`; remove the now-unused `storiesTotal`.
- [x] `src/SpecScribe/assets/specscribe.css` -- add `.epic-status.ready` to the gold `.epic-status.drafted` rule and `.epic-chip.ready` to the gold `.epic-chip.drafted` rule.
- [x] `src/SpecScribe/HtmlTemplater.cs` (beyond the original code map) -- add a `ready` segment to the Epic Status donut's `AppendEpicStatusPanel` roll-up, else epics that now roll up to `ready` would drop out of the donut (the deferred "unmapped `ForEpic` class" finding). The donut now buckets all five `ForEpic` outputs.
- [x] `tests/SpecScribe.Tests/StatusStylesTests.cs` -- mixed drafted+ready now expects `ready`; added all-ready → `ready`, all-drafted → `drafted`, and a full `EpicLabel` tier theory including `"ready" => "Ready for dev"`.
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- added a test that `Sunburst` renders the epic count + `epics` in the center (and singular `epic` for one epic), and not `stories`.

**Completion:** Implemented 2026-07-06. Full suite 204 passing; regenerated dashboard verified in-browser — center reads "7 / epics"; Epic 2 (ready-for-dev stories) inner ring renders bright-gold `sb-ready` `rgb(212,160,23)`, distinct from drafted's pale `rgb(232,217,168)`, matching its stories.

**Acceptance Criteria:**
- Given a project model with 7 epics and 35 stories, when the project sunburst renders, then its center shows `7` above `epics`.
- Given an epic whose stories are ready-for-dev with none in development, when `ForEpic` runs, then it returns `ready` and `EpicLabel` returns "Ready for dev", and the sunburst epic segment uses `sb-ready`.
- Given every existing `ForEpic` consumer, when the epic is `ready`, then epic pages and requirement rollups still render without error (chips/badges show gold; requirement status unchanged).

## Verification

**Commands:**
- `dotnet test` -- expected: all tests pass, including the updated StatusStyles and Charts tests.

**Manual checks:**
- Regenerate a dashboard and confirm the project sunburst center reads "7 / epics" and the ready epic's ring segment is the gold `sb-ready` shade with a "Ready for dev" tooltip/aria label.
