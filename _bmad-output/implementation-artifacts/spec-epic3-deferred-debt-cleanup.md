---
title: 'Epic 3 deferred-debt cleanup: memlog ancestor-matching coverage + hermetic coverage-panel staleness'
type: 'chore'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 107f23c
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two open Epic 3 deferred-debt items remain real: (1) `SiteGenerator.BuildMemlogMap`'s
directory-prefix/closest-ancestor `.memlog.md` selection has zero test coverage anywhere, and (2)
`HtmlRenderAdapter.RenderDashboardBody` reads `DateTime.Now` internally to compute artifact-coverage
staleness, so `HtmlTemplaterTests.RenderIndex_RendersPlanningCoveragePanelWithPresentDateAndMissingChip`
only passes by clock coincidence — it starts asserting the wrong staleness state once real "now" crosses
2026-07-21 (30 days after its fixture's fixed `modified` date). Two other items originally in this batch
(a supposedly-duplicated `SunburstLegend` tuple array, and a `RequirementLinkifier` attribute-corruption
gap) were investigated and found already resolved by prior work — no code changes needed for those; only
the deferred-work ledger needs closing entries.

**Approach:** Extract `BuildMemlogMap`'s pure ancestor-selection loop into a testable static method and add
unit tests for its edge cases. Thread an optional `today` parameter through
`HtmlRenderAdapter.RenderDashboardBody` (default `DateOnly.FromDateTime(DateTime.Now)`, unchanged production
behavior) so the existing test can inject a fixed date instead of depending on the wall clock. Close out all
four ledger entries in `deferred-work.md` (two fixed here, two closed as already-resolved).

## Boundaries & Constraints

**Always:**
- Production HTML output for real `specscribe generate` runs must be byte-identical (no behavioral change to
  live generation) — the `today` parameter must default to today's real date.
- `BuildMemlogMap`'s ancestor-selection semantics (StartsWith containment, longest-dir-length tie-break,
  root-memlog-only-when-no-scoped-memlog gate) must not change — only extract-for-testability, no logic edits.
- Follow the file's existing `~~struck~~ **RESOLVED ...**` convention when closing ledger entries.

**Ask First:** None anticipated — both fixes are additive/refactor-only with no behavioral ambiguity.

**Never:**
- Do not fix the equal-dir-length tie-break's OS-dependent nondeterminism (that's a separate, un-deferred
  bug the ledger doesn't currently name) — only add a test that documents current behavior, don't change it.
- Do not touch `RequirementLinkifier.cs`, `StoryEpicLinkifier.cs`, or `Charts.cs`'s `SunburstLegend`/
  `BuildSunburstLegendItems` — those two items are closed as already-resolved, not touched further.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Two memlogs, equal ancestor depth | `.memlog.md` under two sibling dirs of identical dir-string length, both ancestors of a family's source path | Deterministic per current stable-sort behavior (first-enumerated wins); test pins the case, does not "fix" it | N/A |
| No memlog candidates | Empty `.memlog.md` set in source tree | Family map has no entries for any family; no throw | N/A |
| Root-only memlog | One `.memlog.md` at source root, no scoped ones | Every family falls back to the root memlog's date | N/A |
| Root + scoped memlogs coexist | Root memlog plus a nested one | Root is excluded from the fallback; only families under the nested dir get a match | N/A |
| Non-ancestor substring dir | `.memlog.md` at `docs/foo`, family source at `docs/foobar/x.md` | No match (StartsWith requires trailing `/` separator) | N/A |
| Unparseable `updated:` date in a memlog | Malformed or missing `updated:` line | That memlog contributes no candidate; skipped silently | N/A |
| Coverage panel render, fixture `today` far from real clock | `ArtifactCoverage` built with a `today`/`modified` pair whose staleness is deterministic regardless of wall-clock date | Rendered HTML staleness state matches the fixture's baked-in date, not `DateTime.Now` | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` -- `BuildMemlogMap` (~3737-3788): extract the ancestor-selection `foreach` loop (~3774-3785) into a static, independently-testable method.
- `tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs` -- new tests for the extracted ancestor-selection method's edge cases (nearest existing coverage-family test file).
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- line 111, `var coverageToday = DateOnly.FromDateTime(DateTime.Now);` inside the coverage-panel branch; replace with a threaded parameter.
- `src/SpecScribe/HtmlRenderAdapter.cs` (partial `RenderDashboardBody` declaration) -- add optional `DateOnly? today = null` parameter.
- `src/SpecScribe/HtmlTemplater.cs` -- `BuildIndexPage`/`RenderIndex` (~166-178): thread an optional `today` param down to `RenderDashboardBody`.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- `RenderIndex_RendersPlanningCoveragePanelWithPresentDateAndMissingChip` (~506-538): pass the fixed `today` explicitly instead of relying on the wall clock.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- close all four ledger entries (story-3.3 x2, story-3.5 SunburstLegend, sprint-board-card-tooltip RequirementLinkifier) using the file's existing struck-through `RESOLVED` convention.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- extract `BuildMemlogMap`'s ancestor-selection loop into a static method (`SelectMemlogUpdatedByFamily`) with no behavior change -- makes the previously-untestable core logic directly unit-testable without disk I/O.
- [x] `tests/SpecScribe.Tests/ArtifactCoverageTests.cs` -- add `SiteGeneratorMemlogSelectionTests` covering every row of the I/O matrix above -- closes the "zero coverage" gap the deferred item names.
- [x] `tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs` -- add `GenerateAll_MalformedMemlogUpdatedDate_ContributesNoEnrichmentAndDoesNotThrow` for the unparseable-date row (needs real disk I/O, so it lives here rather than in the pure unit tests).
- [x] `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- add optional `DateOnly? today = null` param to `RenderDashboardBody`/`AppendDashboardSection`, defaulting to `DateOnly.FromDateTime(DateTime.Now)` inline, and use it instead of the hardcoded `DateTime.Now` read at the coverage-panel branch -- makes the render path hermetically testable while leaving production behavior unchanged.
- [x] `src/SpecScribe/HtmlTemplater.cs` -- thread an optional `today` param through `BuildIndexPage`/`RenderIndex` down to `RenderDashboardBody` -- gives tests (and any future caller) a way to pin the clock.
- [x] `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- update `RenderIndex_RendersPlanningCoveragePanelWithPresentDateAndMissingChip` to pass `today: new DateOnly(2026, 7, 8)` (matching its `ArtifactCoverage.Build` fixture) explicitly -- makes the test hermetic instead of coincidentally green.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- strike through and close all four entries under "code review of story-3.3", "code review of story-3.5" (SunburstLegend), and "code review of spec-sprint-board-card-tooltip-html-corruption" (RequirementLinkifier), citing this spec and the investigation findings (SunburstLegend already deduplicated via `BuildSunburstLegendItems`; `RequirementLinkifier.ProtectedSplit` already carries the same catch-all-tag guard as `StoryEpicLinkifier.ProtectedSplit`, confirmed byte-identical) -- closes the ledger.

**Acceptance Criteria:**
- Given the extracted ancestor-selection method and a hand-built memlog/family list matching each I/O-matrix row, when the method runs, then it returns the expected per-family date map for every row without touching disk.
- Given `RenderDashboardBody`/`RenderIndex` called with an explicit `today`, when the coverage panel renders, then staleness/freshness markup reflects that `today`, not the real wall clock.
- Given `RenderDashboardBody`/`RenderIndex` called with `today` omitted, when the coverage panel renders, then output is unchanged from current production behavior (byte-identical to today's live generation).
- Given the four ledger entries, when `deferred-work.md` is reviewed, then each is struck through with a `RESOLVED 2026-07-19` note pointing at this spec.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings, 0 errors.
- `dotnet test` -- expected: all tests pass, including new `SiteGeneratorTests` cases and the updated `HtmlTemplaterTests` case.

## Suggested Review Order

**Memlog ancestor-matching extraction**

- The pure ancestor-selection core, split out of `BuildMemlogMap` for testability, no logic change.
  [`SiteGenerator.cs:3777`](../../src/SpecScribe/SiteGenerator.cs#L3777)

- Direct unit coverage for every ancestor-matching edge case, including the documented tie-break.
  [`ArtifactCoverageTests.cs:285`](../../tests/SpecScribe.Tests/ArtifactCoverageTests.cs#L285)

- Real-disk fixture proving a malformed `.memlog.md` degrades gracefully without corrupting a sibling.
  [`SiteGeneratorCoverageTests.cs:92`](../../tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs#L92)

- Companion fixture for the unparseable-date `continue` branch alone.
  [`SiteGeneratorCoverageTests.cs:76`](../../tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs#L76)

**Hermetic coverage-panel staleness clock**

- Where the wall-clock read now defaults from an injectable `today` instead of being hardcoded.
  [`HtmlRenderAdapter.Dashboard.cs:113`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L113)

- The new optional parameter on the render entry point, defaulting to unchanged production behavior.
  [`HtmlRenderAdapter.Dashboard.cs:18`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L18)

- `today` threaded down through the templater's public entry point.
  [`HtmlTemplater.cs:166`](../../src/SpecScribe/HtmlTemplater.cs#L166)

- The test now pins its fixture's date instead of depending on the real clock.
  [`HtmlTemplaterTests.cs:528`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L528)

**Ledger closure**

- All four resolved/misdiagnosed ledger entries plus the one new deferred finding from review.
  [`deferred-work.md:5`](deferred-work.md#L5)
