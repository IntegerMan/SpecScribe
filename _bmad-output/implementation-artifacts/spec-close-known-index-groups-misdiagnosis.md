---
title: 'Close KnownIndexGroups adrs/retros debt as misdiagnosed'
type: 'chore'
created: '2026-07-18'
status: 'done'
baseline_commit: 'df5c45794ca7c937a635f917b02bc89ee19c1dfb'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 4 Action #1 (and carry-forwards on Epics 6/8/9) claims `KnownIndexGroups` omitting `adrs`/`retros` permanently trips an "unrecognized top-level folder" notice for a normal BMad ADR directory and blocks Story 4.8's diagnostics all-clear. That path model is wrong: `UnrecognizedTopLevelFolders` only walks `SourceRoot` (`_bmad-output`); ADRs live on a separate `AdrSourceRoot` (`docs/adrs`) and never enter `sourceRelatives`; retros live under already-well-known `implementation-artifacts/`.

**Approach:** Close the debt as misdiagnosed — no whitelist change. Document the SourceRoot-only semantics in code, pin the correct model with a focused regression test, reconcile `deferred-work.md`, and mark the four related `sprint-status.yaml` action items `done`.

## Boundaries & Constraints

**Always:** Keep `KnownIndexGroups` as SourceRoot top-level recognition only. Preserve true-positive notices for unknown SourceRoot folders (e.g. `design-notes/`). Leave Story 4.8 all-clear behavior unchanged for the clean fixture.

**Ask First:** Whether any *other* open action item that merely mentions this debt in passing (but is not one of the four KnownIndexGroups rows) should also flip — only the four explicit KnownIndexGroups rows are in scope unless the human expands.

**Never:** Do not add `adrs`/`retros`/`docs` to `KnownIndexGroups`. Do not merge ADR enumeration into `sourceRelatives`. Do not redesign diagnostics, home-index bands, or golden fingerprints for a no-op whitelist. Do not invent a SourceRoot `retros/` convention BMad does not use.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal BMad layout | SourceRoot=`_bmad-output`; AdrSourceRoot=`docs/adrs`; retros under `implementation-artifacts/` | No unrecognized-folder notice for `adrs/`, `retros/`, or `docs/` | N/A |
| Unknown SourceRoot folder | SourceRoot child `design-notes/*.md` | Still emits one `Unsupported` notice for `design-notes/` | Non-fatal skip |
| Hypothetical SourceRoot `adrs/` | SourceRoot child `adrs/*.md` (not AdrSourceRoot) | Still unrecognized (out of scope to whitelist) | Non-fatal skip |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/DashboardViewBuilder.cs` — private `KnownIndexGroups` + `IsWellKnownTopLevelFolder` (relocated from HtmlTemplater in Story 6.2; titles unused post-declutter).
- `src/SpecScribe/HtmlTemplater.cs` — public `IsWellKnownTopLevelFolder` delegates to DashboardViewBuilder.
- `src/SpecScribe/SiteGenerator.cs` — `UnrecognizedTopLevelFolders(sourceRelatives)` first-segment check; fed only by `EnumerateSourceFiles(SourceRoot)`.
- `src/SpecScribe/ForgeOptions.cs` — separate `AdrSourceRoot` / `AdrFallbackProbeSubdirs` (canonical `docs/adrs`).
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — `GenerateAll_UnrecognizedTopLevelFolder_*`, `GenerateAll_CleanFixture_ProducesAboutAndAllClearDiagnostics`, and `GenerateAll_NormalBmadLayout_DoesNotEmitUnrecognizedNoticeForAdrsDocsOrRetros`.
- `_bmad-output/implementation-artifacts/deferred-work.md` — Story 4.8 deferred bullet (resolved as misdiagnosed).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — four KnownIndexGroups action rows (epics 4, 6, 8, 9) marked done.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/DashboardViewBuilder.cs` — expand the `KnownIndexGroups` / `IsWellKnownTopLevelFolder` XML docs to state: set recognizes SourceRoot tops only; ADR/retro *roots* are not SourceRoot children in normal BMad layout and must not be "fixed" by adding `adrs`/`retros` here — rationale: prevents the misdiagnosis from recurring.
- [x] `src/SpecScribe/SiteGenerator.cs` — one-sentence note on `UnrecognizedTopLevelFolders` that ADR files under `AdrSourceRoot` are outside this check — rationale: same guardrail at the call site.
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — add a test that a clean SourceRoot plus separate `docs/adrs` produces **zero** events whose message contains `unrecognized top-level folder` for `adrs/`, `docs/`, or `retros/` (may reuse/extend the clean-fixture setup) — rationale: pins the path model so a future "fix" cannot reintroduce a no-op whitelist as required debt.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — mark the Story 4.8 `KnownIndexGroups`/`adrs`/`retros` bullet resolved with pointer to this spec and the misdiagnosis summary — rationale: stop re-queuing.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — set status `done` (with short `# misdiagnosed; see spec-close-known-index-groups-misdiagnosis` comment) on all four KnownIndexGroups action items (Epic 4 schedule, Epic 6 fix, Epic 8 carry, Epic 9 carry) — rationale: closes the portal-visible debt chain.

**Acceptance Criteria:**
- Given a normal BMad generate (SourceRoot + separate AdrSourceRoot), when diagnostics/events are inspected, then no unrecognized-folder notice names `adrs/`, `docs/`, or `retros/`.
- Given an unknown SourceRoot folder such as `design-notes/`, when generate runs, then the existing structure notice still fires exactly once.
- Given the four KnownIndexGroups action rows in `sprint-status.yaml`, when this work lands, then each is `status: done` with a comment pointing at this spec.
- Given the Story 4.8 deferred bullet, when `deferred-work.md` is read, then it is marked resolved (not deleted).

## Design Notes

The Epic 4 write-up assumed `docs/adrs` appears as a SourceRoot top-level segment. It does not: `--source` defaults to `_bmad-output`, and ADRs are a second root. Whitelisting `adrs`/`retros` would only silence a notice when those names are literally SourceRoot children — an atypical layout this product does not promise — and would not move Story 4.8 all-clear on real projects (other notices can still block all-clear). Closing without a whitelist change is the honest resolution.

## Verification

**Commands:**
- `dotnet test --filter "FullyQualifiedName~SiteGeneratorAdapterTests"` — expected: green, including the new path-model pin and existing unrecognized-folder + all-clear tests.
- `dotnet test` — expected: full suite green (no golden regen expected; docs/status-only + comments + one adapter test).

**Manual checks:**
- Open generated `action-items.html` / follow-up detail for the Epic 4 item after regen — status badge should read done.

## Suggested Review Order

**Path-model docs (no logic change)**

- SourceRoot-only KnownIndexGroups semantics + do-not-whitelist guardrail
  [`DashboardViewBuilder.cs:12`](../../src/SpecScribe/DashboardViewBuilder.cs#L12)

- Public IsWellKnownTopLevelFolder carries the same guardrail
  [`DashboardViewBuilder.cs:28`](../../src/SpecScribe/DashboardViewBuilder.cs#L28)

- UnrecognizedTopLevelFolders notes AdrSourceRoot is outside the walk
  [`SiteGenerator.cs:2597`](../../src/SpecScribe/SiteGenerator.cs#L2597)

**Debt close**

- Story 4.8 deferred bullet marked resolved as misdiagnosed
  [`deferred-work.md:285`](deferred-work.md#L285)

- Four carry-forward action items marked done with spec pointer
  [`sprint-status.yaml:330`](sprint-status.yaml#L330)

**Pin test**

- Asserts adrs/docs/retros stay non-well-known; clean fixture emits no structure notices
  [`SiteGeneratorAdapterTests.cs:589`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L589)
