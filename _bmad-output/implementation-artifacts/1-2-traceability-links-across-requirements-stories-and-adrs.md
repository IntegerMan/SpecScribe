---
baseline_commit: 8e9244cdd8c87182d8804d1d50ff34c4f5992db7
---

# Story 1.2: Traceability Links Across Requirements, Stories, and ADRs

Status: done

## Story

As a contributor,
I want requirement IDs and source references to be linkified consistently,
so that I can move between planning artifacts without manual searching.

## Acceptance Criteria

1. **Given** requirement IDs appear in rendered content
   **When** I view generated pages
   **Then** recognized IDs resolve to requirement detail pages
   **And** unresolved IDs do not create broken links.

2. **Given** story artifacts include source citations and ADR references
   **When** I open epic and story pages
   **Then** citations resolve to the appropriate generated pages
   **And** ADR status and index cards remain consistent after regeneration.

## Tasks / Subtasks

- [x] Task 1: Preserve and complete requirement-ID traceability through the existing post-render linkification seam (AC: #1)
  - [x] Confirm recognized `FR#` and `NFR#` tokens are linkified only when present in `RequirementsModel.ById`
  - [x] Preserve anchor-aware behavior so tokens already inside `<a>` tags are never rewritten
  - [x] Preserve self-page skip behavior on requirement detail pages so a requirement never links to itself

- [x] Task 2: Ensure source citations and ADR references resolve through generated-page URLs rather than raw markdown paths (AC: #2)
  - [x] Keep `[Source: _bmad-output/...md]` citations flowing through `SourceLinkifier` using the generated-page reference map
  - [x] Confirm story artifact sections that already support source citations remain covered: blurb, remainder, acceptance criteria, dev-agent record, review findings, and change log
  - [x] Preserve ADR markdown-link rewriting so sibling ADR links, `_bmad-output` references, and `README.md` continue to resolve to generated HTML targets

- [x] Task 3: Keep reference-map and regeneration behavior coherent across all relevant page types (AC: #1, #2)
  - [x] Verify `epics.md` maps to `epics.html` and consumed implementation artifacts map to their story detail pages rather than generic mirrored output
  - [x] Confirm requirement linkification still runs across home, epics index, epic detail, story detail, requirements index/detail, ADR pages, and generic rendered pages after any changes
  - [x] Preserve full ADR-set regeneration so renamed or deleted ADRs cannot leave stale detail pages or stale index-card metadata behind

- [x] Task 4: Add regression coverage for traceability behavior at both unit and generation-integration levels (AC: #1, #2)
  - [x] Extend `LinkifierTests` for any uncovered edge cases introduced by the implementation
  - [x] Add focused generation-level tests that validate reference-map coverage and generated output routing for source citations and requirement links
  - [x] Add/extend ADR regeneration tests for stale-output safety when ADR files are changed, renamed, or removed

- [x] Task 5: Validate end-to-end rendered behavior with the real generation path (AC: #1, #2)
  - [x] Run focused tests for linkifiers and any new generation tests
  - [x] Run the site generation command and confirm generated story, requirement, and ADR pages render without broken-link regressions

## Developer Context Section

### Epic Context and Business Value

Epic 1 is the first user-facing quality gate for SpecScribe. Story 1.1 established navigation and dashboard wayfinding; Story 1.2 is the traceability layer that makes those pages useful for contributors who need to move from summary views into exact requirements, story plans, and decisions without manual searching.

This story directly realizes FR-6 from the PRD. If traceability is incomplete or inconsistent, the generated portal stops being a trustworthy project narrative and reverts to a set of isolated pages.

### Story Foundation Extract

- Primary concern: consistent, omission-safe cross-linking for requirement IDs, story source citations, and ADR references.
- User outcome: contributors can move from epic/story prose to requirement details and decision records in one click.
- Success boundary: recognized references become generated-page links; unresolved references remain plain text rather than producing broken navigation.
- Regeneration boundary: ADR edits, renames, or deletes must not leave stale pages or stale ADR index-card status data behind.

### Previous Story Intelligence

Story 1.1 established the pattern to follow for this artifact and for implementation scope control:

- Reuse the existing central seam instead of duplicating behavior in templates. Story 1.1 kept navigation logic centralized in `SiteNav`; Story 1.2 should keep traceability logic centralized in the existing linkifier/generation seam.
- Preserve omission-safe behavior. Story 1.1 explicitly avoided dead navigation entries; this story must apply the same principle to unresolved requirement IDs and source citations.
- Add regression-focused tests close to the behavior seam, then run a real generation pass to prove rendered output still works.
- Keep changes local-first and host-neutral so later HTML/webview parity is not made harder.

### Architecture Compliance

- Keep traceability semantics in the shared rendering pipeline rather than introducing host-specific link generation. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]
- Preserve the host-neutral view-model contract and avoid adapter-specific behavior drift for traceability semantics. [Source: `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`]
- Keep interaction and semantic consistency cross-surface; link meaning and target resolution should not diverge between static HTML and any future webview adapter. [Source: `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`]
- Maintain graceful degradation for malformed or unsupported artifacts. Unresolved references are non-fatal and must remain plain text. [Source: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`]
- Preserve watch/regeneration coherence for ADRs by treating the ADR set as the unit of rebuild when ADR source changes occur. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]

## Technical Requirements

- Requirement-ID linkification must continue to flow through `RequirementLinkifier.Linkify` as a post-render HTML pass.
- Only recognized IDs present in `RequirementsModel.ById` may be linked.
- Unknown or unresolved `FR#`/`NFR#` tokens must remain plain text; never emit speculative or broken links.
- Existing anchor-aware behavior must be preserved: content already inside `<a>` tags must not be rewritten.
- Requirement detail pages must continue skipping self-linkification via the existing `skipId` path.
- Source citations must continue to resolve through the generated-page reference map, not by linking raw markdown directly.
- Story pages already route source citations through multiple content slices; any refactor must preserve coverage for blurb, remainder, acceptance criteria, dev-agent record, review findings, and change log.
- ADR markdown-body links must continue rewriting from `.md` to generated `.html` destinations while preserving fragments.
- `epics.md` must keep resolving to `epics.html`, and consumed implementation artifacts must keep resolving to `epics/story-{id}.html` rather than generic mirrored output.
- ADR status extraction for index cards must remain consistent after regeneration.

## File Structure Requirements

Primary UPDATE candidates for this story:

- `src/SpecScribe/RequirementLinkifier.cs`
  - Current state: regex-based post-render requirement-ID linkifier with anchor-aware splitting and requirement lookup.
  - Story change focus: requirement-ID matching completeness, unresolved-ID safety, and any edge-case handling required by AC #1.
  - Must preserve: no rewrites inside existing anchors, case-insensitive requirement lookup, and self-page `skipId` behavior.

- `src/SpecScribe/SourceLinkifier.cs`
  - Current state: post-render source-citation linkifier that turns `_bmad-output/...md` references into generated-page links using a reference map.
  - Story change focus: ensure the reference map coverage is complete for story citations and related generated pages.
  - Must preserve: unresolved-path no-op behavior and fragment text remaining outside the linked path.

- `src/SpecScribe/AdrLinkRewriter.cs`
  - Current state: rewrites markdown-authored ADR-body links from `.md` to generated `.html` targets.
  - Story change focus: preserve complete ADR link target mapping and fragment safety while confirming story citations and ADR references remain coherent.
  - Must preserve: sibling ADR rewrites, `_bmad-output` path rewrites, and `README.md` to ADR index mapping.

- `src/SpecScribe/SiteGenerator.cs`
  - Current state: orchestrates generation, applies requirement linkification to rendered pages, builds the source-reference map, and performs full ADR-set regeneration.
  - Story change focus: verify and extend page-coverage, reference-map correctness, and regeneration safety where required by AC #2.
  - Must preserve: `_requirements` caching before linkification, page generation order, full ADR directory rebuild behavior, and generic single-page rendering for non-epics pages.

- `src/SpecScribe/RequirementsParser.cs`
  - Current state: parses the requirements inventory and coverage map from `epics.md` into `RequirementsModel`.
  - Story change focus: only if requirement-ID recognition gaps trace back to parser output rather than linkifier behavior.
  - Must preserve: case-insensitive ID lookup, FR/NFR split, and current coverage/status derivation.

- `src/SpecScribe/EpicsParser.cs`
  - Current state: extracts story artifact sections, acceptance criteria, dev-agent record, and named sections used by story rendering.
  - Story change focus: only if source-citation handling requires changes to artifact-section extraction.
  - Must preserve: acceptance-criteria extraction, AC deep-linking support, and current section-carving behavior.

Primary TEST candidates:

- `tests/SpecScribe.Tests/LinkifierTests.cs`
  - Current state: unit coverage for requirement-ID linking, source-link linking, and ADR-link rewriting.
  - Story change focus: extend only where new edge cases or bug fixes require it.
  - Must preserve: current expectations for unknown IDs/paths, no rewrites inside anchors, and fragment preservation.

- `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs`
  - Current state: exercises requirement parsing and progress rollups from realistic epics content.
  - Story change focus: use only if parser/output prerequisites need additional requirement-model coverage.
  - Must preserve: case-insensitive `ById` behavior and current requirement-status derivation assumptions.

- Add a focused generation-level test file if needed for `SiteGenerator` traceability routing/regeneration behavior rather than forcing broad refactors for private helper access.

## Library and Framework Requirements

- No new dependency is required for Story 1.2; stay within the existing .NET/Markdig/YamlDotNet stack.
- Keep linkification in the current post-render pipeline instead of introducing a parallel markdown-rendering extension unless a concrete defect proves the existing seam insufficient.
- Do not introduce browser-only or future webview-only logic into the core traceability behavior.

## Testing Requirements

- Preserve existing unit coverage in `tests/SpecScribe.Tests/LinkifierTests.cs` for:
  - known requirement-ID linkification,
  - unknown-ID no-op behavior,
  - no rewrites inside existing anchors,
  - self-page skip behavior,
  - partial-token non-matches,
  - source-citation no-op behavior for unknown paths,
  - fragment preservation,
  - ADR rewrite cases for sibling links, `_bmad-output` links, `README.md`, and fragments.
- Add focused coverage for the gaps surfaced by current analysis:
  - generated reference-map routing for `epics.md` and consumed story artifacts,
  - generation-level verification that story citation sections all receive source-link processing,
  - ADR stale-output safety on change/rename/delete scenarios.
- Run targeted tests for touched traceability behavior and then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Linkifier|FullyQualifiedName~Requirement|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

## UX and Accessibility Requirements

- Traceability links are part of the contributor reading flow; linked content must remain readable and keyboard-usable within the existing portal semantics. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`]
- Requirement backlinks are explicitly part of story/epic detail behavior in the UX spec. Recognized FR-IDs should auto-link; broken IDs should degrade gracefully rather than behaving like dead links. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`]
- Preserve the established link color and semantic styling system rather than introducing ad hoc traceability-specific styles. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]

## Reinvention and Regression Guardrails

- Do not duplicate traceability logic across templates; keep shared behavior in the existing linkifier/generation seam.
- Do not hardcode requirement URLs or generated story URLs in multiple places if `RequirementsModel`, `SiteNav`, and the reference map already own that routing knowledge.
- Do not convert unresolved references into anchors with placeholder targets.
- Do not bypass full ADR-set regeneration with a narrower path that can leave stale ADR pages or stale index metadata behind.
- Do not add public API surface solely to test private helpers if equivalent generation behavior can be asserted through focused integration tests.
- Do not regress Story 1.1 navigation, breadcrumbs, or omission-safe behavior while changing traceability routing.

## Git Intelligence Summary

- `b19beb0` implemented Story 1.1 using a pattern of small seam-preserving changes plus regression tests. Follow that style rather than broad rewrites.
- `767caab` added GitHub Pages publication for `docs/live`; generated-page links must therefore remain portable and static-host-safe.
- `8e9244c` updated YAML recently; avoid assuming sprint or config files are untouched elsewhere in the tree.

## Latest Technical Information

- No external version-driven change is required for this story. The implementation should stay on the current local stack and reinforce existing behavior rather than introducing new packages or alternate rendering infrastructure.

## Project Context Reference

- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md`
- Previous story: `_bmad-output/implementation-artifacts/1-1-dashboard-navigation-and-readability-foundation.md`
- Architecture spine: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- Rendering architecture: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`
- ADR 0002: `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`
- ADR 0004: `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`
- UX design: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`
- UX behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- create-story workflow run for story `1-2-traceability-links-across-requirements-stories-and-adrs`
- workflow activation resolved manually because `python3` was unavailable on Windows; repo memory confirms using `py -3` for BMAD helper scripts in this workspace
- planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Linkifier|FullyQualifiedName~Requirement|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Verify whether Story 1.2 can be satisfied entirely by tightening the existing post-render linkifier/reference-map pipeline before considering any structural refactor.
- If behavior gaps exist, change the smallest central seam that fixes all affected page types rather than patching individual templates.
- Add focused regression coverage around generated output routing and ADR regeneration coherence.
- Re-run targeted tests and a real generation pass before marking the story implementation complete.

### Completion Notes List

- Story context assembled from epics, PRD, UX design/experience, architecture spine, ADRs 0002 and 0004, prior story intelligence, current code seams, and recent git history.
- Current implementation already contains `RequirementLinkifier`, `SourceLinkifier`, `AdrLinkRewriter`, and `SiteGenerator.ApplyRequirementLinks`; this story should prefer strengthening and testing those seams over inventing new ones.
- Current test coverage is strong at the unit level for individual linkifiers; the main gaps are generation-level routing/reference-map coverage and ADR stale-output safety.
- ADR regeneration already rebuilds the full ADR output directory, which is a critical behavior to preserve for AC #2.
- Generated output is published to GitHub Pages, so all links must remain static-host-safe and relative-path correct.

#### Implementation (2026-07-06)

- Verified all three traceability seams satisfy the ACs as-is; no production code changes were required. The story was completed by adding the missing regression coverage and proving behavior end-to-end.
- Added generation-level regression tests in `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs` covering: known/unknown requirement-ID linkification on a generated story page, requirement self-link skip on the FR detail page, `epics.md`→`epics.html` citation routing, consumed-artifact citation routing to `epics/story-1-2.html`, generic mirrored citation routing to `planning-artifacts/prd.html`, ADR sibling/README cross-link rewriting plus status surfacing, and ADR stale-output safety on delete/rename/status-change via `RegenerateAdrs`.
- Extended `tests/SpecScribe.Tests/LinkifierTests.cs` with unit edge cases: repeated-ID linking, mixed known/unknown IDs in one string, no-op when the requirement set is empty, and multiple source citations in one blob.
- Full suite: 127 passing (was 113; +14 new). Real generation pass produced 19 pages with no errors.
- End-to-end verification confirmed in-scope surfaces (ADR pages, epic/story/requirements pages) contain zero residual `.md` links and that story citations resolve to `../epics.html`, `../epics/story-1-1.html`, and mirrored `.html` targets.
- Out-of-scope observation (not a regression, no code touched): the generic PRD page still contains a few hand-authored `[text](file.md)` links to files outside the generated set (repo `README.md`, ADR source paths, a brief). These are not `[Source:]` citations or ADR-body links and fall outside this story's traceability scope; flag for a future generic cross-doc linking story if desired.

### File List

- _bmad-output/implementation-artifacts/1-2-traceability-links-across-requirements-stories-and-adrs.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs
- tests/SpecScribe.Tests/LinkifierTests.cs

## Change Log

- 2026-07-05: Created Story 1.2 implementation context with traceability-specific architecture, code-seam, regeneration, and testing guidance.
- 2026-07-06: Implemented Story 1.2 as a verify-and-harden pass — confirmed the existing linkifier/reference-map/ADR-regeneration seams satisfy both ACs, added generation-level and unit regression coverage (14 new tests, 127 total passing), and validated a clean end-to-end generation pass. No production code changes required.
- 2026-07-06: Code review completed (0 decision-needed, 7 patch, 2 defer, 7 dismissed). No production defects; findings target test robustness/completeness of the new regression coverage.

## Review Findings

<!-- Adversarial code review 2026-07-06: Blind Hunter + Edge Case Hunter + Acceptance Auditor. This was a
test-only change; all findings concern the strength/coverage of the new tests, not shipped behavior (production
seams verified working, 127 tests green, clean generation pass). -->

- [x] [Review][Patch] Only 3 of the 6 story-citation slices are exercised — production linkifies blurb, remainder, review-findings, change-log, acceptance-criteria, and dev-agent-record (SiteGenerator.cs:361-369), but the fixture cited only blurb/AC/change-log. FIXED: fixture now cites a distinct target from all six slices and there is a routing assertion per slice (remainder→rendering.html, dev-agent-record→architecture.html, review-findings→brief.html added). [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] Absence-only assertions lack positive controls and can pass vacuously. FIXED: SkipsSelfLink now asserts FR6 is present as text; LeavesUnknownRequirementId now asserts FR99 is not any anchor's label; RemovesStalePageAndIndexCard now asserts the surviving 0001 card remains; ReflectsChangedStatus now asserts the old "Proposed" status is gone. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] No test inspected the returned GenerationEvent list for Error outcomes. FIXED: a `GenerateSite()` helper asserts `GenerateAll` produced no `GenerationOutcome.Error`, and each `RegenerateAdrs` call now asserts a non-Error outcome. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] Untouched-ADR survival after delete/rename was unasserted. FIXED: delete test asserts 0001 card + "Accepted" pill survive; rename test asserts 0001-first.html still exists. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] Fragment preservation was verified only at unit level. FIXED: added generation-level tests — a source citation `architecture.md#Overview` keeps `#Overview` outside the link, and an ADR cross-link `0002-second.md#Context` rewrites to `0002-second.html#Context`. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] Loose ADR status match. FIXED: assertion scoped to the actual status pill markup `status-accepted">Accepted</span>`. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Patch] Dispose() ran an unguarded `Directory.Delete`. FIXED: wrapped in best-effort try/catch for IOException/UnauthorizedAccessException so a transient file lock can't flake the run or leak the temp tree. [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs]
- [x] [Review][Defer] Case-insensitive requirement lookup is claimed as must-preserve but the input pattern `\b(FR|NFR)(\d+)\b` is uppercase-only (no IgnoreCase), so lowercase `fr6` is silently never matched; untested. Pre-existing production behavior, not introduced by this change. [src/SpecScribe/RequirementLinkifier.cs:17] — deferred, pre-existing
- [x] [Review][Defer] Multi-digit partial-token boundary (FR60 must not match FR6) is not pinned — the existing partial-token test covers only non-digit adjacency (FR1x/XFR1). Correct today via `\b...\d+\b`; a regression pin would be nice-to-have. [tests/SpecScribe.Tests/LinkifierTests.cs:61] — deferred, pre-existing