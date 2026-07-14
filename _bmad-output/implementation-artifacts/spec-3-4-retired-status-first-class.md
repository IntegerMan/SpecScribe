---
title: 'First-class retired sprint status (keep 3.4 ledger row)'
type: 'bugfix'
created: '2026-07-14'
status: 'done'
baseline_commit: 'f7456d4cdc716c7a8e961c7d8bbdf527b8a4f385'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Sprint ledger value `retired` (Story 3.4 kept on purpose after SCP 2026-07-08) maps to Unrecognized on the Now & Next / sprint board, while the epics.md sunburst correctly omits 3.4 because that story number is vacant. Readers see a false “unknown status” and a board↔sunburst mismatch.

**Approach:** Teach the sprint classifier a first-class `retired` stage (own board column, legend, counts), keep the `3-4-…: retired` yaml row, and keep retired **off** the sunburst (do not feed sunburst from sprint status or invent a retired epics story ring). Exclude retired from the sprint wheel “done / total” denominator so history does not inflate incomplete work.

## Boundaries & Constraints

**Always:**
- Map non-empty sprint status `retired` via `StatusStyles.ForSprint` to css class `retired` (not `unrecognized`, not requirements `deferred`).
- Keep `_bmad-output/implementation-artifacts/sprint-status.yaml` entry `3-4-interactive-tree-views-for-project-and-artifact-structure: retired`.
- Sunburst remains epics.md-only (`Charts.Sunburst` / `ForStory` / `StoryStages` unchanged for this change).
- Own Kanban column for Retired (after Done, before Unrecognized). Unrecognized stays for truly unmapped words.
- Wheel label `N / M done`: **M excludes retired**; Done count unchanged. Board still lists retired cards.
- Stage partition invariant: every yaml story row still lands in exactly one `TrackedStoryStages` bucket (including `retired`).
- Reuse deferred grey visual treatment for `retired` (shared `--status-deferred` / mirrored badge+lane styles); distinct StageMeaning and label “Retired”.
- Clearing unrecognized for `retired` must also stop the 8.2 Unsupported diagnostic for that row.

**Ask First:**
- Mapping any word other than exact `retired` (e.g. `superseded`, `cancelled`) onto this stage.
- Changing epics.md retirement HTML-comment UX (Story 10.5) or removing/renumbering the 3.4 yaml key.
- Putting retired segments on any sunburst/donut that is currently epics-defined.

**Never:**
- Merge sprint `retired` into requirements `deferred` semantics or drop the card by mapping to a stage with no board column.
- Add `retired` to `ForStatus` / `StoryStages` solely so defined mosaics grow — out of scope unless an artifact Status line needs it later.
- Delete the 3.4 sprint-status row or pretend the Defined≠Tracked orphan divergence must disappear (orphan 3.4 remains a truthful 8.3 signal).
- Silent fallback of `retired` → drafted/pending/done.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | yaml `3-4-…: retired` | ForSprint → `retired`; board Retired column shows the card; badge “Retired”; not Unrecognized; no Unsupported notice for this value | N/A |
| Wheel denom | 41 done, 1 retired, 88 other non-retired tracked | Label `41 / 89 done` (M = tracked − retired); retired may appear in tip as separate line, not inside M | N/A |
| Sunburst | epics.md has no 3.4 story (vacant comment only) | Project sunburst unchanged; no retired ring/segment from sprint | N/A |
| Still unknown | yaml `blocked` (or other unmapped) | Remains `unrecognized` column + diagnostic path | Non-fatal notice only |
| Absent status | empty/null sprint status | Unchanged → `pending` | N/A |
| Case | `Retired` / `RETIRED` | Normalize → `retired` stage | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/StatusStyles.cs` -- `ForSprint`, `SprintLabel`, `StageMeaning`, `LegendStages` / legend row; `IsUnrecognizedSprintStatus`
- `src/SpecScribe/SprintTemplater.cs` -- `StageOrder`, `BoardColumns`, `EmptyLaneCopy`, `RenderProgressWheel` (exclude retired from M)
- `src/SpecScribe/ProjectCounts.cs` -- `TrackedStageOrder` must include `retired` (partition Σ)
- `src/SpecScribe/Icons.cs` -- status glyph for `retired` (reuse deferred-equivalent is OK)
- `src/SpecScribe/assets/specscribe.css` -- `.status-badge.retired`, sprint lane/card accents mirroring deferred grey
- `tests/SpecScribe.Tests/StatusStylesTests.cs` -- flips InlineData `retired` → `retired`; legend/meaning/unrecognized helper
- `tests/SpecScribe.Tests/SprintTemplaterTests.cs` -- board lane list + wheel label with retired present
- `tests/SpecScribe.Tests/ProjectCountsTests.cs` -- partition still holds with a retired row
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- assert retired badge/lane hooks if added
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- **do not remove** the 3.4 `retired` row (verify only)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/StatusStyles.cs` -- map `retired` in ForSprint/SprintLabel/StageMeaning/LegendStages; ensure IsUnrecognizedSprintStatus is false -- first-class classifier
- [x] `src/SpecScribe/Icons.cs` -- ForStatus(`retired`) glyph -- UX-DR17 shape channel
- [x] `src/SpecScribe/SprintTemplater.cs` -- add Retired column + stage order; EmptyLaneCopy; RenderProgressWheel M excludes retired -- board + wheel semantics
- [x] `src/SpecScribe/ProjectCounts.cs` -- add retired to TrackedStageOrder -- partition honesty with yaml
- [x] `src/SpecScribe/assets/specscribe.css` -- retired badge/lane/legend swatch via deferred grey tokens -- visible distinct non-unrecognized treatment
- [x] `tests/SpecScribe.Tests/StatusStylesTests.cs` + `SprintTemplaterTests.cs` + `ProjectCountsTests.cs` (+ StylesheetTests if CSS hooks) -- cover I/O matrix edge cases
- [x] Manual regen sanity -- Now & Next: 3.4 in Retired not Unrecognized; sunburst still omits 3.4; wheel M excludes retired

**Acceptance Criteria:**
- Given a sprint-status story row with status `retired`, when the portal generates, then the card is in a **Retired** lane (not Unrecognized), the badge/legend treat it as Retired, and no Unsupported unrecognized-status diagnostic is emitted for that value.
- Given tracked stories including at least one retired row, when the sprint progress wheel renders, then the `N / M done` denominator **excludes** retired counts while Done is unchanged.
- Given Story 3.4 remains vacant in epics.md and present as `retired` in yaml, when the project sunburst renders, then it does **not** gain a retired segment or otherwise surface 3.4.
- Given any other unmapped sprint word, when classified, then behavior remains Unrecognized (column + notice path).

## Spec Change Log

## Design Notes

`retired` ≠ `deferred`: deferred is “shelved for later” (requirements inventory); retired is “removed from the active plan, kept for ledger history.” Own css class + meaning; shared grey token only.

Sunburst OFF is free if we never touch `Charts.Sunburst` / `ForStatus` / `StoryStages`. Wheel OFF-from-denominator is an explicit `RenderProgressWheel` filter so partition can still count retired under `StoriesTracked`.

Orphan signal: 3.4 yaml-only continues 8.3 Defined≠Tracked divergence — intentional while the ledger row is kept.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~StatusStyles|FullyQualifiedName~SprintTemplater|FullyQualifiedName~ProjectCounts|FullyQualifiedName~Stylesheet"` -- expected: all matching tests green
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green (if filter suite passes and time allows)

**Manual checks:**
- Regenerate site; Home Now & Next: Story 3.4 under Retired; Unrecognized empty (or only true unknowns); sunburst center/legend unchanged re: 3.4; wheel `N / M` matches non-retired tracked total.

## Suggested Review Order

**Classifier**

- First-class `retired` stage — not Unrecognized, not requirements deferred
  [`StatusStyles.cs:167`](../../src/SpecScribe/StatusStyles.cs#L167)

- Legend + meaning keep retired distinct from deferred
  [`StatusStyles.cs:235`](../../src/SpecScribe/StatusStyles.cs#L235)

**Board & wheel**

- Retired column after Done, before Unrecognized
  [`SprintTemplater.cs:37`](../../src/SpecScribe/SprintTemplater.cs#L37)

- Wheel M excludes retired; all-retired yields empty (no `0 / 0`)
  [`SprintTemplater.cs:271`](../../src/SpecScribe/SprintTemplater.cs#L271)

- Partition honesty still counts retired under tracked stages
  [`ProjectCounts.cs:79`](../../src/SpecScribe/ProjectCounts.cs#L79)

**Chrome**

- Seven-column board grid so Retired + Unrecognized stay on one row
  [`specscribe.css:4068`](../../src/SpecScribe/assets/specscribe.css#L4068)

- Badge/lane accents reuse deferred grey token
  [`specscribe.css:1864`](../../src/SpecScribe/assets/specscribe.css#L1864)

**Tests**

- Lane placement + wheel denominator + all-retired empty
  [`SprintTemplaterTests.cs:325`](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs#L325)
