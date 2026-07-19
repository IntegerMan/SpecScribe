---
title: 'Epic 8 deferred — path Ordinal accept, watch divergence notice, dup ids, message cap, funnel pins, ForStatus traps, LegendKey DRY, surface-coverage split'
type: 'bugfix'
created: '2026-07-18T17:31:09-04:00'
status: 'done'
baseline_commit: 'eff7041'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Eight open Epic 8 deferred items still leave silent Ordinal path-date misses (accepted), watch-mode divergence notice gaps, collapsed duplicate story ids, unbounded DivergenceMessage strings, under-asserted 8.3 funnel/Defined-vs-Tracked surfaces, fuzzy ForStatus inventing stages (`incomplete`→done), LegendKey label drift risk, and a single Epic 6 surface-coverage action that conflates the Epic 8 instance with the standing rule.

**Approach:** Close all eight under this spec: accept Ordinal git-date keys with ledger note; re-emit Unsupported divergence on the watch ledger rebuild; surface duplicate defined ids on the same channel; cap DivergenceMessage lists; pin funnel drafted == StoriesDefined and Defined≠Tracked under drift; tighten ForStatus against substring traps; DRY LegendKey words through label helpers; split the surface-coverage action into instance vs standing rule.

## Boundaries & Constraints

**Always:**
- Item 1 (git date path map): keep `StringComparer.Ordinal` in `ProgressCalculator.BuildGitFileDateMap` — matches the git layer / Story 3.1 Ordinal path-key policy. Ledger-resolve as accepted; do not flip to IgnoreCase in this pass.
- Watch divergence: when incremental paths null `_counts` and rebuild (`RegenerateEpics` → `WriteIndex`/`WriteSprint`/`WriteActionItems`), emit the same Unsupported `AdapterDiagnostic` + `DivergenceMessage` GenerateAll uses. No double-emit on full generate. Notice into that path’s events list is enough — do not invent a separate diagnostics-page watch rewrite unless already on the path.
- Duplicate defined story ids in `epics.md`: detect before/while building the defined-id set; keep first-wins membership for reconcile; surface duplicates on the Unsupported / divergence channel (message names the duplicated id). Do not abort generation.
- `DivergenceMessage`: each untracked/orphan id list is capped (first 10, deterministic order already used) with `+N more` when truncated; totals in the sentence stay accurate.
- Tests: extend generation-level 8.3 coverage so Story Pipeline funnel drafted total equals `StoriesDefined`, and under an orphan/untracked fixture Defined vs Tracked stay distinct on index + sprint (prefer stable aria/class hooks over brittle prose regex).
- `ForStatus`: stop substring traps that invent lifecycle stages (`incomplete` must not become `done`). Prefer normalized exact/synonym matching shaped like `ForSprint` for known BMad/story Status phrases; empty/blank still → `drafted`; unknown → `unrecognized`. Keep real synonyms that today correctly map (`done`, `complete`/`completed`, `ready-for-dev`, `in progress` / `in-progress`, `review`, `draft`/`drafted`, `active`/`wip`).
- `LegendKey`: legend words come from existing label helpers (`StoryLabel` / requirement labels / retired wording) — no parallel inline stage→word switch that can drift. Teaching order of `LegendStages` unchanged; visible words stay identical.
- Surface-coverage action: split into two `action_items` — (a) Epic 8 instance executed → `done`; (b) standing surface-coverage gate for future net-new epics → separate status (`open` or `done` with standing-rule comment, matching the Epic 6 process-rule sibling shape). Mark the deferred bullet resolved.
- Mark all eight deferred bullets RESOLVED citing `spec-epic8-deferred-debt-cleanup`.

**Ask First:**
- Switching any git path map (including `BuildGitFileDateMap`) to `OrdinalIgnoreCase` / git-wide IgnoreCase.
- Treating duplicate defined story ids as a hard ingest error that aborts generation.
- Broadening ForStatus synonym inventory beyond today’s live BMad/story Status vocabulary.
- Changing LegendStages teaching order or legend copy/meaning text.

**Never:**
- Absorb Epic 10 / Epic 19 backlog work or KnownIndexGroups adrs/retros debt.
- Rewrite Markdig, invent a new `--status-*` token, or change ProjectCounts as the portal count authority.
- Flip Ordinal path keys “just for Windows case” without a git-layer policy.
- Drop the unrecognized safety net or map empty Status away from drafted.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Watch rebuild + drift | `_counts` null after `RegenerateEpics`; epics/sprint diverge | Unsupported diagnostic with DivergenceMessage on that path’s events | no throw |
| Dup defined id | two stories share Id in epics.md | first-wins set; Unsupported names the duplicate | generation continues |
| Huge drift lists | >10 untracked and/or orphans | message lists 10 + `+N more`; counts in prose are full totals | N/A |
| Funnel pin | GenerateAll with known StoriesDefined | funnel drafted total == Stories defined tile | N/A |
| Drift surfaces | orphan tracked and/or untracked defined | Defined ≠ Tracked on index + sprint | N/A |
| ForStatus trap | status `incomplete` | `unrecognized` (not `done`) | N/A |
| ForStatus synonym | `Ready for Dev` / `completed` / blank | `ready` / `done` / `drafted` | N/A |
| LegendKey DRY | any LegendStages entry | word == label helper for that stage | N/A |
| Surface split | sprint-status action_items | two actions: Epic 8 instance done; standing rule separate | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/ProgressCalculator.cs` — `BuildGitFileDateMap` Ordinal (accept / no change)
- `src/SpecScribe/SiteGenerator.cs` — GenerateAll divergence emit (~353–363); `RegenerateEpics` nulls `_counts` (~540); `WriteIndex`/`WriteSprint`/`WriteActionItems` rebuild without events
- `src/SpecScribe/ProjectCounts.cs` — `Reconcile` ToHashSet; `DivergenceMessage`; `HasDivergence`
- `src/SpecScribe/StatusStyles.cs` — `ForStatus` Contains classifiers; `LegendKey` inline word switch; `StoryLabel` / `RequirementLabel` / `ForSprint`
- `tests/SpecScribe.Tests/ProjectCountsTests.cs` — DivergenceMessage + Reconcile pins
- `tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs` — 8.3 generation surface asserts
- `tests/SpecScribe.Tests/StatusStylesTests.cs` — ForStatus + LegendKey
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — Epic 6 surface-coverage action (~339–342)
- `_bmad-output/implementation-artifacts/deferred-work.md` — eight Epic 8 bullets (8-8 / 8-3 / 8-2 / 8-1 sections)

## Tasks & Acceptance

**Execution:**
- [x] Item 1 — ledger-only accept Ordinal git-date path keys (no ProgressCalculator comparer change) — git-layer consistency
- [x] `src/SpecScribe/SiteGenerator.cs` -- re-emit Unsupported divergence when watch rebuilds `_counts` -- close notice gap
- [x] `src/SpecScribe/ProjectCounts.cs` -- detect duplicate defined ids + cap DivergenceMessage lists -- honesty under bad/large inputs
- [x] `tests/SpecScribe.Tests/ProjectCountsTests.cs` -- dup-id notice + capped message pins -- I/O matrix
- [x] `tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs` -- funnel drafted == StoriesDefined; Defined≠Tracked under drift on index+sprint -- close 8.3 coverage gap
- [x] `src/SpecScribe/StatusStyles.cs` -- harden ForStatus; LegendKey words via label helpers -- stop invented stages + label drift
- [x] `tests/SpecScribe.Tests/StatusStylesTests.cs` -- `incomplete`→unrecognized; synonym still maps; legend word == helper -- pin classifiers/DRY
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- split surface-coverage instance vs standing rule -- machine-readable
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- resolve eight bullets with this spec key -- ledger truth

**Acceptance Criteria:**
- Given case-only path mismatch risk, when this work lands, then Ordinal git-date keys remain and the deferred bullet is marked accepted/resolved with rationale.
- Given watch-mode epics regenerate with divergent epics/sprint, when `_counts` is rebuilt, then an Unsupported divergence diagnostic is emitted (GenerateAll still emits exactly once).
- Given a duplicated story id in epics.md, when building ProjectCounts, then membership is first-wins and an Unsupported/divergence notice names the duplicate without aborting.
- Given >10 untracked or orphan ids, when formatting DivergenceMessage, then each listed side shows at most 10 ids plus `+N more` while totals stay correct.
- Given GenerateAll fixtures for agree and drift, when asserting HTML, then funnel drafted equals Stories defined, and Defined vs Tracked remain distinct under drift on index and sprint.
- Given status text `incomplete`, when classifying with ForStatus, then the stage is unrecognized; known synonyms and blank→drafted still hold.
- Given LegendKey render, when reading stage words, then each word comes from the shared label helper for that stage (order unchanged).
- Given the Epic 6 surface-coverage action, when reading sprint-status.yaml, then Epic 8 instance and standing rule are separate action items with clear statuses.
- Given the eight deferred bullets, when this work lands, then each is marked resolved citing `spec-epic8-deferred-debt-cleanup`.

## Spec Change Log

## Design Notes

- Item 1 closes as accepted Ordinal consistency (same call as Story 3.1 top-files case half) — IgnoreCase is Ask First / git-wide only.
- Watch notice: prefer one helper used by GenerateAll + RegenerateEpics (e.g. ensure ledger + optional events sink) so Write* rebuilds stay dumb and events stay on the path that owns the list.
- ForStatus hardening should kill the `Contains("complete")` ⊆ `incomplete` class of bugs without orphaning common Status prose; mirror ForSprint’s normalize-then-match shape where practical.
- Surface-coverage split mirrors the Epic 6 “standing rule; re-demonstrated…” sibling — two rows beat one overloaded comment.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~ProjectCounts|FullyQualifiedName~SiteGeneratorSprint|FullyQualifiedName~StatusStyles"` -- expected: all pass
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green

## Suggested Review Order

**Count ledger honesty**

- Duplicate defined ids + capped DivergenceMessage (dup-only blames epics.md)
  [`ProjectCounts.cs:114`](../../src/SpecScribe/ProjectCounts.cs#L114)

- Shared Unsupported emit used by GenerateAll and RegenerateEpics
  [`SiteGenerator.cs:2564`](../../src/SpecScribe/SiteGenerator.cs#L2564)

- Watch path re-emits after follow-up surfaces rebuild `_counts`
  [`SiteGenerator.cs:540`](../../src/SpecScribe/SiteGenerator.cs#L540)

**Status classifiers**

- Exact/synonym ForStatus + token fallback (no incomplete→done)
  [`StatusStyles.cs:34`](../../src/SpecScribe/StatusStyles.cs#L34)

- LegendKey words via StoryLabel / RequirementLabel / SprintLabel
  [`StatusStyles.cs:309`](../../src/SpecScribe/StatusStyles.cs#L309)

**Process / ledger**

- Surface-coverage instance vs standing rule split
  [`sprint-status.yaml:344`](./sprint-status.yaml#L344)

- Eight Epic 8 bullets marked RESOLVED
  [`deferred-work.md`](./deferred-work.md)

**Pins**

- Dup-id + capped message + ForStatus traps + funnel/drift/watch asserts
  [`ProjectCountsTests.cs`](../../tests/SpecScribe.Tests/ProjectCountsTests.cs)
  [`StatusStylesTests.cs`](../../tests/SpecScribe.Tests/StatusStylesTests.cs)
  [`SiteGeneratorSprintTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs)
