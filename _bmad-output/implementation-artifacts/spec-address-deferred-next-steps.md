---
title: 'Address deferred Next Steps on story and epic pages'
type: 'feature'
created: '2026-07-17T21:56:19-04:00'
status: 'done'
baseline_commit: '77452d54abb80fbe7a12f058c2b7836024d4978b'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Open deferred items reverse-linked to a story or epic still force one-off “Address with AI” visits. After code review, a done story can look “All done” while parked follow-ups remain, so batching that work feels daunting.

**Approach:** On story and epic Next Steps, add a quick-dev “Address deferred” action when open deferred items are associated. Scope: story = reverse-linked open items; epic = all open items under that epic (story-child + epic-level). When the entity is otherwise complete and deferred is the only meaningful next move, make that action prominent and replace the celebratory all-done panel with a “done with deferred work” panel. The copied prompt must carry enough identifiers for an agent to find each writeup in `deferred-work.md` / detail pages.

## Boundaries & Constraints

**Always:**
- Gate on open (`!Resolved`) deferred only; omit when zero or module lacks `quick-dev` (NFR8).
- Story scope = `FollowUpGeometry.DeferredForSource(story.Id)` open slots; epic scope = all open slots for that epic number (story-child + epic-level).
- Prompt lists each open item with discoverable cues: short body summary, provenance label / `SourceKey`, and detail href or deferred-work backlog pointer — not a vague “fix deferred somewhere.”
- Done story/epic with open deferred → distinct “done with deferred” Next Steps (Address deferred primary), not the celebratory all-done checkmark panel.
- Non-done story/epic with open deferred → Address deferred is demoted (alternate / Other actions); existing workflow primaries stay primary.
- Keep VS Code outline `StoryCommands` / `PrimaryStoryCommand` parity with the story page when deferred is passed through.

**Ask First:**
- Changing deferred provenance authoring (`source_spec` / Deferred-from headings).
- Adding a batch “Close deferred” action on story/epic pages (out of this intent unless requested).

**Never:**
- Invent deferred associations not already in the ledger / reverse index.
- Count resolved items or inflate epic/story completion.
- Auto-run commands or invent new IDE deep links.
- Change per-item deferred detail Next Steps (Address/Close) behavior.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Done story + open deferred | Story `done`; ≥1 open reverse-linked deferred; catalog has quick-dev | “Done with deferred” panel; Address deferred primary; prompt lists each item’s summary + provenance + detail/backlog cue | N/A |
| Done story + no open deferred | `done`; zero open (none or all resolved) | Existing celebratory all-done panel (+ correct-course hatch if exposed) | N/A |
| In-progress/review story + open deferred | Active/review primary already present | Address deferred appears as non-primary alternate | N/A |
| Epic rollup | Epic has open story-child and/or epic-level deferred | Epic Next Steps includes Address deferred covering **all** those open items | N/A |
| Done epic + open deferred | Epic status done; open deferred remain | Address deferred primary (no empty panel) | N/A |
| Review epic (retro pending) + deferred | Retro is primary | Address deferred demoted alternate | N/A |
| Module lacks quick-dev | Open deferred exist; catalog has no quick-dev | No Address deferred suggestion; other next steps unchanged | Silent omit |
| Zero deferred | No matching open slots | Unchanged Next Steps / all-done behavior | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadCommands.cs` -- `RenderNextSteps` / `ForStory` / `RenderAllDonePanel`; `RenderEpicNextSteps` / `ForEpic`; `StoryCommands` / `PrimaryStoryCommand`; mirror `ForDeferredItem` quick-dev gate + labeled badge
- `src/SpecScribe/EpicsViewBuilder.cs` -- `BuildStory` / `BuildEpic` already resolve deferred slots; filter open and pass into BmadCommands
- `src/SpecScribe/FollowUpGeometry.cs` -- `DeferredForSource`, `DeferredForEpicNumber` / epic-scoped `DeferredItems` (reuse; do not fork membership)
- `src/SpecScribe/SiteGenerator.cs` -- outline construction: pass open deferred into `StoryCommands` / `PrimaryStoryCommand`
- `tests/SpecScribe.Tests/ModuleContextTests.cs` -- unit coverage for done+deferred panel, demotion, epic rollup, NFR8 omit
- `tests/SpecScribe.Tests/SiteGeneratorOutlineTests.cs` -- StoryCommands parity for done+deferred when outline is built
- `tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs` (or nearest epic/story page generator test) -- end-to-end: deferred-work provenance → story/epic page shows Address deferred

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- Thread optional open-deferred slots into story/epic Next Steps + StoryCommands; add Address-deferred suggestion builder (discoverable multi-item prompt + DisplayLabel); replace all-done short-circuit with “done with deferred” panel when open deferred exist; promote only when it is the only meaningful action
- [x] `src/SpecScribe/EpicsViewBuilder.cs` -- Pass open story reverse-linked slots into `RenderNextSteps`; pass open epic-scoped slots into `RenderEpicNextSteps`
- [x] `src/SpecScribe/SiteGenerator.cs` -- Pass the same open story deferred into outline `StoryCommands` / `PrimaryStoryCommand`
- [x] `tests/SpecScribe.Tests/ModuleContextTests.cs` (+ outline/page tests as needed) -- Cover I/O matrix rows: done+deferred, done+none, demotion on active story, epic rollup, missing quick-dev omit

**Acceptance Criteria:**
- Given a done story with ≥1 open reverse-linked deferred item and a module exposing quick-dev, when the story page Next Steps render, then the celebratory all-done panel is absent and Address deferred is the primary action whose copy payload names the story and lists each open item with summary + provenance + a path/href cue.
- Given a done story with no open deferred, when Next Steps render, then the existing celebratory all-done panel still appears.
- Given a non-done story that already has a workflow primary and open deferred, when Next Steps render, then Address deferred is present but not primary.
- Given an epic with open deferred under any of its stories and/or at epic level, when epic Next Steps render, then one Address deferred action covers all those open items.
- Given open deferred but a catalog without quick-dev, when Next Steps render, then no Address deferred action appears.

## Design Notes

**Prompt shape (illustrative):** copy payload should look like:

```
/bmad-quick-dev Address open deferred work for Story 6.5 (3 items). Find writeups in deferred-work.md and follow-up detail pages:
1. [code review of 6-5-…] summary… → follow-ups/deferred-….html
2. …
```

Use `FollowUpRow.SummarizeFromHtml` for summaries; prefer each slot’s `DetailHref` when non-empty, else the geometry’s deferred list href / `deferred-work.md`. DisplayLabel: `Address deferred`.

**Done-with-deferred panel:** Keep Next Steps card grammar; lead with a short status line (e.g. story complete, N open deferred) then the primary Address deferred card — not the green all-done checkmark copy.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~BmadCommands|FullyQualifiedName~ModuleContext|FullyQualifiedName~Outline|FullyQualifiedName~StoryEpic"` -- expected: targeted suites green
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green before review

**Manual checks:**
- Generate site; open a done story that reverse-lists open deferred — Next Steps shows Address deferred (not All done); paste prompt into Cursor and confirm item cues are findable in `deferred-work.md`.
- Open an epic with mixed story-child + epic-level deferred — one Address deferred covers the full open set.

## Suggested Review Order

**Done-with-deferred panel**

- Entry: done stories with open deferred skip celebration and render Address deferred as primary.
  [`BmadCommands.cs:38`](../../src/SpecScribe/BmadCommands.cs#L38)

- Status line + primary card grammar for story/epic “complete but parked work.”
  [`BmadCommands.cs:752`](../../src/SpecScribe/BmadCommands.cs#L752)

- Multi-item prompt with summary, SourceKey, and detail-href cues for agent discovery.
  [`BmadCommands.cs:716`](../../src/SpecScribe/BmadCommands.cs#L716)

**Demotion + epic rollup**

- Non-done branches append Address deferred as an alternate (including pending epics).
  [`BmadCommands.cs:512`](../../src/SpecScribe/BmadCommands.cs#L512)

- Epic pages pass open story-child + epic-level deferred into Next Steps.
  [`EpicsViewBuilder.cs:114`](../../src/SpecScribe/EpicsViewBuilder.cs#L114)

- Story pages filter reverse-linked open slots into `RenderNextSteps`.
  [`EpicsViewBuilder.cs:202`](../../src/SpecScribe/EpicsViewBuilder.cs#L202)

**Outline parity**

- Primary is Address deferred when present; hatch never becomes primary without quick-dev.
  [`BmadCommands.cs:101`](../../src/SpecScribe/BmadCommands.cs#L101)

- Outline construction threads open deferred safely when `followUps` is null.
  [`SiteGenerator.cs:2127`](../../src/SpecScribe/SiteGenerator.cs#L2127)

**Tests**

- I/O matrix: done+deferred primary, demotion, epic rollup, NFR8 omit, hatch-not-primary.
  [`ModuleContextTests.cs:559`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L559)
