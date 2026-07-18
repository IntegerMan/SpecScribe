---
title: '9.2 deferred — coverage-map parser unify, Id fail-closed, row dict hoist, close obsolete'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: 'd7e8c2f3f93b5b9e112073d528b848deb0faa1ff'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open deferred items from Story 9.2 leave (1) Epic 1 UX-DR header tags awaiting owner confirmation, (2) `ParseUxDrs` as a near-copy of `ParseDefs` that can drift, (3) `AppendCoverageRow` rebuilding an epic dictionary per NFR/UX-DR row, (4) `RequirementInfo.Id` fail-open to `"NFR"` for unknown kinds, and (5) an unmet Task 7 FR HTML byte-identity assertion superseded by Story 9.3.

**Approach:** Confirm Epic 1 UX-DR1–13 and 16–18 mappings as-is (no trim). Unify UX-DR parsing onto the shared FR/NFR path. Hoist the epic `ToDictionary` once per coverage subgroup. Fail closed on unknown `RequirementKind` in `Id`. Close the Task 7 FR HTML baseline item as superseded by 9.3 — no new golden. Mark all five deferred bullets resolved.

## Boundaries & Constraints

**Always:**
- Owner decision: leave Epic 1 `**UX-DRs:**` header tags UX-DR1–13 and 16–18 unchanged in `epics.md`.
- Coverage resolve / status derive / `RequirementInfo` construction stay single-path for FR, NFR, and UX-DR after the unify.
- NFR/UX-DR coverage row HTML semantics and badges stay behaviorally identical (hoist is allocation-only).
- Unknown `RequirementKind` must not emit an NFR id; fail closed (throw or equivalent hard fail — never silent mislabel).
- Mark the five deferred-work.md bullets under the 9.2 review section resolved with this spec key.
- Close Task 7 as obsolete; do not restore a pre-9.3 byte-identical FR flow/grid/donut assertion.

**Ask First:**
- Changing sibling fail-open switches (`RequirementsTemplater` kindLabel, `Icons.ForRequirementKind`) beyond `RequirementInfo.Id`.
- Trimming or rewriting any Epic 1 UX-DR header tokens after this confirmation.

**Never:**
- New requirement kinds, authoring schemas, or coverage UI redesign.
- Inventing FR HTML golden baselines that fight Story 9.3 Unmapped-tier surfaces.
- Silent default of unknown kinds to NFR/FR/UX-DR prefixes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| UX-DR parse via shared path | `UX-DR12: …` in UX Design section | Same `RequirementInfo` as today (`Design`, coverage, status) | Skip non-matching lines |
| FR/NFR unchanged | Existing FR/NFR inventory | Byte-stable parse outcomes vs prior fixtures | Cross-kind lines still skipped |
| Coverage row hoist | Many NFR/UX-DR rows | Same HTML; dictionary built once per subgroup | Missing epic number → same as today |
| Known Id arms | Functional / NonFunctional / Design | `FR` / `NFR` / `UX-DR` + number | N/A |
| Unknown kind | Cast/forged kind outside enum arms | Must not return `"NFR"+n` | Fail closed (exception) |
| Epic 1 confirm | Current epics.md Epic 1 header | Tags left as-is; deferred item closed as confirmed | N/A |
| Task 7 close | Deferred FR HTML baseline note | Marked resolved superseded by 9.3; no new test | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/RequirementsParser.cs` -- shared `ParseDefs` + unified `DefLine` (`FR|NFR|UX-DR`); `ParseUxDrs` removed
- `src/SpecScribe/RequirementsTemplater.cs` -- `AppendNfrUxDrCoverageSection` builds epic dict once; `AppendCoverageRow` consumes it
- `src/SpecScribe/RequirementsModel.cs` -- `RequirementInfo.Id` fail-closed on unknown `RequirementKind`
- `_bmad-output/planning-artifacts/epics.md` -- Epic 1 header UX-DR1–13, 16–18 (confirm only; no edit)
- `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` -- parser + templater coverage fixtures
- `_bmad-output/implementation-artifacts/deferred-work.md` -- five bullets under "code review of 9-2-nfr-and-ux-dr-coverage-maps.md"

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/RequirementsParser.cs` -- Unify `ParseUxDrs` into the shared `ParseDefs` (or extracted helper) so regex/kind differ but coverage/status/`RequirementInfo` construction cannot drift; delete the near-copy body
- [x] `src/SpecScribe/RequirementsTemplater.cs` -- Build epic-number dictionary once in `AppendCoverageSubGroup` (or parent) and pass it into `AppendCoverageRow`; remove per-row `ToDictionary`
- [x] `src/SpecScribe/RequirementsModel.cs` -- Make `RequirementInfo.Id` fail closed on unknown `RequirementKind` (exhaustive known arms only; no NFR default)
- [x] `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` -- Lock UX-DR parse/coverage via shared path; coverage-row HTML still correct; add/adjust Id fail-closed coverage if testable without unsafe enum casting gymnastics
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Resolve all five 9.2 bullets with this spec key: (1) Epic 1 tags confirmed as-is, (2) parser unify, (3) dict hoist, (4) Id fail-closed, (5) Task 7 closed superseded by 9.3

**Acceptance Criteria:**
- Given UX-DR inventory lines, when parse runs after unify, then Design requirements resolve coverage/status identically to today's fixtures with no separate near-copy path
- Given the NFR/UX-DR coverage section, when many rows render, then output matches prior semantics and the epic dictionary is not rebuilt per row
- Given only known requirement kinds, when `Id` is read, then prefixes stay FR/NFR/UX-DR; unknown kind never silently becomes NFR
- Given Epic 1's existing UX-DR header tags, when this work lands, then `epics.md` is unchanged and the deferred confirmation item is closed as owner-confirmed as-is
- Given the Task 7 FR HTML baseline deferred note, when this work lands, then it is marked resolved as superseded by Story 9.3 with no new byte-identity golden

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~RequirementsParserTests|FullyQualifiedName~RequirementsAndProgressTests"` -- expected: matching tests green
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green before done

## Design Notes

Prefer folding UX-DR into `ParseDefs` with a line matcher + fixed `RequirementKind.Design` + `withCategories: false` over a third parallel loop. `Id` fail-closed should use an explicit `NonFunctional` arm and `_ => throw …` (or discard pattern that the compiler treats as exhaustive after three arms — if the language still requires `_`, that arm must throw, not return NFR).

## Suggested Review Order

**Shared parse path**

- Unified `DefLine` accepts FR/NFR/UX-DR; Design calls the same `ParseDefs`
  [`RequirementsParser.cs:22`](../../src/SpecScribe/RequirementsParser.cs#L22)

- Design inventory routes through shared path (no `ParseUxDrs`)
  [`RequirementsParser.cs:61`](../../src/SpecScribe/RequirementsParser.cs#L61)

- Unknown prefix fails closed (mirrors `Id`)
  [`RequirementsParser.cs:268`](../../src/SpecScribe/RequirementsParser.cs#L268)

**Coverage row dictionary hoist**

- Build epic dict once per NFR/UX-DR coverage section
  [`RequirementsTemplater.cs:418`](../../src/SpecScribe/RequirementsTemplater.cs#L418)

**Id fail-closed**

- Explicit NFR arm; unknown kind throws
  [`RequirementsModel.cs:40`](../../src/SpecScribe/RequirementsModel.cs#L40)

**Tests & deferred close-out**

- Id/Slug throw on forged kind; Design section skips stray FR/NFR
  [`RequirementsAndProgressTests.cs:547`](../../tests/SpecScribe.Tests/RequirementsAndProgressTests.cs#L547)

- Five 9.2 bullets resolved; DefLine/CoverageMapLine consolidation deferred
  [`deferred-work.md:591`](./deferred-work.md#L591)
