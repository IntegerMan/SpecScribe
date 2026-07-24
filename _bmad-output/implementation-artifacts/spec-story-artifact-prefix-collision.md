---
title: 'Story-artifact prefix collision — a companion deliverable displaces its story'
type: 'bugfix'
created: '2026-07-23'
status: 'done'
baseline_commit: '2be7f6da54ad0376034e8823f251a245abec2779'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `BmadArtifactAdapter.BuildArtifactMap` keys story artifacts on the `^(\d+)-(\d+)-` filename prefix and assigns with `map[key] = path` — last-writer-wins over the directory enumeration. The prefix is not unique: a spike story sits beside its durable spike report (`23-1-spike-report.md` next to `23-1-spike-nuxt-over-ir-feasibility.md`), and the report sorts later, so it silently won the key. Every story surface then read the wrong file: no line-start `Status:` (the report writes `**Status:** Complete`, which `EpicsParser.ExtractStatus` cannot match) and no task checkboxes, so `ProgressCalculator` produced `TasksTotal == 0` and a null status. Story 23.1 — `done` in `sprint-status.yaml`, 34/34 tasks in its file — rendered as *Deferred, "no task plan yet"* on the epic page, the dashboard sunburst, and the Story 20.2 `#sb=epic-23` drill-in island; `story-23-1.html` was generated from the report body. Story 22.1 was hit identically. Views reading `sprint-status.yaml` were unaffected, which is why the defect looked like one broken page.

**Approach:** Resolve collisions explicitly instead of by enumeration order. Score each candidate on the two things the story surfaces actually consume — a parseable line-start `Status:` and at least one top-level task checkbox — and keep the higher scorer; break ties by ordinal filename compare so the result never depends on directory order. Report the displaced candidate as `AdapterDiagnosticCategory.Skipped`, the category already reserved for "superseded by a sibling".

## Boundaries & Constraints

**Always:**
- Scoring reads content, not filename shape — a companion deliverable may be named anything, and `-spike-report` is a convention rather than a rule.
- The two scored signals stay the two `ProgressCalculator.ReadArtifactProgress` consumes (`EpicsParser.ExtractStatus`, `TaskListParser.Parse`), so the winner is by construction the file that feeds the progress model.
- Tie-break is deterministic and enumeration-order-independent.
- Honor the adapter's NEVER-throws contract: an unreadable candidate scores 0, it does not abort the ingest.
- The losing candidate stays unconsumed and therefore still renders as its own generic page.

**Ask First:**
- Making the `{epic}-{story}-` prefix convention itself stricter (e.g. requiring a slug match against `epics.md`).
- Suppressing the `Skipped` diagnostic when the collision is a known/expected companion shape.

**Never:**
- Reintroduce order-dependent last-writer-wins.
- Read candidate files on the non-collision path (scoring is collision-only I/O).
- Change the `Status:` regex or the task parser to accommodate report formatting — the report is not a story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Story + companion report | `1-1-foundation.md` (Status + tasks) and `1-1-spike-report.md` (neither) | Story wins the key; report reported `Skipped` and renders as a generic page | N/A |
| Progress reads the winner | Same fixture | `story.Status == "in-progress"`, `TasksTotal == 1`, `TasksDone == 1` | N/A |
| Two story-shaped candidates | `1-1-alternate.md` and `1-1-foundation.md`, both scoring 2 | Ordinal filename compare picks `1-1-alternate.md`; identical under reversed enumeration | N/A |
| No collision | One artifact per key | Unchanged: assigned directly, no file read, no diagnostic | N/A |
| Unreadable candidate | Locked / missing file in a collision | Scores 0, loses; ingest continues | `IOException`/`UnauthorizedAccessException` caught |
| Oversized numeric group | `99999999999-1-overflow.md` | Still TryParse-skipped before the collision path (Story 4.1 review) | No throw |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadArtifactAdapter.cs` — `BuildArtifactMap` (collision branch + `Skipped` diagnostic), new `ChooseStoryArtifact` / `StoryArtifactScore`; `IngestEpics` call site now threads `options` + `diagnostics`
- `src/SpecScribe/ProgressCalculator.cs` — consumer (reference only; unchanged)
- `src/SpecScribe/EpicsParser.cs` — `ExtractStatus`, the line-start `Status:` signal (reference only; unchanged)
- `tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs` — three added cases

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BuildArtifactMap` -- Replace `map[key] = path` with a collision branch that calls `ChooseStoryArtifact` and emits a `Skipped` diagnostic for the loser -- kill order-dependence
- [x] `src/SpecScribe/BmadArtifactAdapter.cs` -- Add `ChooseStoryArtifact` (score, then ordinal filename tie-break) and `StoryArtifactScore` (status signal + task signal, never throws) -- content-based, not filename-based
- [x] `tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs` -- Companion loses + is reported; progress reads the story; two story-shaped candidates resolve identically under reversed enumeration -- lock the I/O matrix
- [x] Regenerate the real repo and confirm 22.1 / 23.1 -- prove the reported defect is gone

**Acceptance Criteria:**
- Given a story artifact beside a companion sharing its `{epic}-{story}-` stem, when the adapter builds the artifact map, then the story wins the key regardless of which file the enumeration yields last.
- Given that collision, when the ingest completes, then exactly one `Skipped` diagnostic names the displaced file and the winner, and it surfaces on the diagnostics page.
- Given the collision, when progress is computed, then status and task tallies come from the story file, so a `done` story renders `done` with its real task count rather than "no task plan yet".
- Given two equally story-shaped candidates, when the map is built twice under opposite enumeration orders, then both runs pick the same file.
- Given no collision, when the map is built, then behavior and output are unchanged (no candidate file is read).

## Design Notes

**Why score on status + tasks rather than on the filename:** those are precisely the two fields `ReadArtifactProgress` extracts. Scoring on them makes the tie-break self-consistent with the consumer — the file that can actually answer "what status, how many tasks" is the file the story pages should render. A `-spike-report` suffix rule would have fixed today's two cases and missed the next companion shape.

**Why `Skipped`:** `AdapterDiagnosticCategory.Skipped` is documented as "deliberately not ingested (e.g. superseded by a sibling). Reserved for adapters that must choose between candidates" — this is that case, and it means the discarded candidate is visible on the Story 4.8 diagnostics page instead of vanishing.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests` -- 2,174 passed / 3 skipped / 0 failed, including the three added cases; `GenerateAll_GoldenContentFingerprint_…` unaffected (its fixture has no prefix collision)

**Manual checks:**
- Regenerated this repo: `epics/epic-23.html` carries `aria-label="Story 23.1: Spike — Nuxt-over-IR Feasibility — done, 34/34 tasks"`; the dashboard explorer island node for `23.1` is `"statusClass":"done"`; `epics/epic-22.html` shows 22.1 as `review, 6/6 tasks`; `epics/story-23-1.html` renders the story's `Tasks / Subtasks`; both spike reports now render at `implementation-artifacts/*-spike-report.html` and appear on `diagnostics.html` as skipped.

## Suggested Review Order

**Collision resolution**

- Collision branch replaces last-writer-wins; loser reported rather than dropped.
  [`BmadArtifactAdapter.cs:312`](../../src/SpecScribe/BmadArtifactAdapter.cs#L312)

- Scoring mirrors what `ProgressCalculator` consumes; ordinal tie-break keeps it order-independent.
  [`BmadArtifactAdapter.cs:360`](../../src/SpecScribe/BmadArtifactAdapter.cs#L360)

**Coverage**

- Three cases: companion loses + diagnostic, progress reads the story, deterministic tie-break.
  [`BmadArtifactAdapterTests.cs:383`](../../tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs#L383)
