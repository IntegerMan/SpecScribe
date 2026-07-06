---
baseline_commit: f264778df6eb9b54a2e2c50d870be6b62975c93a
---

# Story 1.1: Dashboard Navigation and Readability Foundation

Status: done

## Story

As a project maintainer,
I want a coherent landing page and navigation model,
so that I can find key project views in seconds.

## Acceptance Criteria

1. **Given** a generated site with epics, requirements, and ADR content
   **When** I open the home page
   **Then** I see a clear dashboard with links to Epics, Requirements, ADRs, and source-derived pages
   **And** missing artifact classes are omitted gracefully without broken navigation entries.

2. **Given** any generated page
   **When** I inspect navigation and breadcrumbs
   **Then** active-page state and breadcrumb path are correct
   **And** navigation remains usable on desktop and mobile breakpoints.

## Tasks / Subtasks

- [x] Task 1: Preserve and harden top-level navigation composition (AC: #1, #2)
  - [x] Confirm nav item generation remains data-driven and omission-safe for missing classes (`ADRs`, `Epics`, `Requirements`, source-derived pages)
  - [x] Ensure nav remains coherent across home, epics, requirements, story pages, and ADR pages
  - [x] Verify no dead links are emitted when source classes are absent

- [x] Task 2: Ensure dashboard discoverability and coherent entry points (AC: #1)
  - [x] Keep Home dashboard as the project landing surface with explicit route into Epics & Stories and Requirements
  - [x] Confirm ADR index and source-derived cards are discoverable from Home when present
  - [x] Keep graceful-empty behavior for unavailable classes

- [x] Task 3: Enforce active-link and breadcrumb correctness on all generated pages (AC: #2)
  - [x] Validate active nav highlighting and `aria-current="page"` on every page type
  - [x] Validate breadcrumb trails from Home -> section -> detail pages
  - [x] Ensure breadcrumb and nav URLs resolve correctly from nested paths

- [x] Task 4: Deliver mobile-usable navigation baseline (AC: #2)
  - [x] Implement/complete mobile navigation behavior for small breakpoints as specified by UX (drawer/hamburger semantics or equivalent usable pattern)
  - [x] Ensure keyboard and focus behavior is preserved when mobile nav is open/closed
  - [x] Confirm no regressions to desktop sticky nav behavior

- [x] Task 5: Add regression-focused automated tests (AC: #1, #2)
  - [x] Add tests for nav omission behavior and active-link semantics
  - [x] Add tests for breadcrumb output for representative page depths
  - [x] Add tests for dashboard key link presence/absence under varying content availability

## Developer Context Section

### Epic Context and Business Value

Epic 1 is the first user-facing quality gate for SpecScribe. This story establishes navigation and readability foundations that every later story depends on (traceability links, markdown fidelity, and interaction polish).

If navigation is unclear or brittle at this stage, later visual and insight features become harder to discover and less trustworthy.

### Story Foundation Extract

- Primary concern: coherent information architecture and predictable wayfinding.
- Scope: generated static site navigation, dashboard entry points, breadcrumb correctness, and mobile usability baseline.
- Out of scope for this story: deep interaction mechanics of advanced visualizations beyond baseline usability.

### Architecture Compliance

- Follow shared rendering core boundaries; do not introduce host-specific logic into core page composition. [Source: _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md#architecture-decisions]
- Preserve local-first, read-only behavior. [Source: _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md#inherited-invariants]
- Keep accessibility semantics contractual (keyboard behavior, labels, status redundancy). [Source: _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md#inherited-invariants]
- Keep navigation/state semantics coherent with future cross-surface contract direction (AD-8). [Source: _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md#ad-8-adopted-interaction-state-contract-is-canonical-update-transport-is-adapter-specific]

## Technical Requirements

- Navigation items must be conditionally included based on discovered content classes; no broken links when class is absent.
- Home dashboard must present clear routing to Epics, Requirements, ADRs, and source-derived content when present.
- Active nav state must be deterministic and assign `aria-current="page"` for current page.
- Breadcrumb trail must reflect true document path semantics and be correct for nested routes (`epics/...`, `requirements/...`, `adrs/...`).
- Mobile breakpoint behavior must remain usable and not degrade keyboard navigation.

## File Structure Requirements

Primary UPDATE candidates for this story:

- `src/SpecScribe/SiteNav.cs`
  - Current state: central nav construction (`Build`) and rendering (`RenderNavBar`, `RenderBreadcrumb`) with active-link support.
  - Story change focus: nav composition rules, active-state guarantees, and responsive behavior support hooks.
  - Must preserve: omission-safe nav construction, prefix-aware relative links, `aria-current` behavior.

- `src/SpecScribe/HtmlTemplater.cs`
  - Current state: home/dashboard and generic page rendering, with nav + breadcrumb injection.
  - Story change focus: dashboard discoverability and key links under content-available/missing conditions.
  - Must preserve: dashboard chart/content composition, existing page metadata and rendering flow.

- `src/SpecScribe/EpicsTemplater.cs`
  - Current state: epics index/detail/story pages include nav and breadcrumb wiring.
  - Story change focus: ensure wayfinding consistency and breadcrumb correctness across epics hierarchy.
  - Must preserve: story/epic page structure used by parsers and AC/linkification features.

- `src/SpecScribe/SiteGenerator.cs`
  - Current state: source discovery, page generation orchestration, nav build input population.
  - Story change focus: only if needed for dashboard/nav link source discoverability.
  - Must preserve: full rebuild semantics, consumed-artifact routing, ADR regeneration behavior.

- `src/SpecScribe/assets/specscribe.css`
  - Current state: sticky nav and responsive baseline (`@media (max-width: 640px)`), no full mobile drawer/focus-trap implementation yet.
  - Story change focus: mobile nav usability and readability baseline alignment.
  - Must preserve: existing token system and overall visual language.

Potential NEW test files:

- `tests/SpecScribe.Tests/SiteNavTests.cs`
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`

## Library and Framework Requirements

- No new dependency is required for Story 1.1; implement with existing stack unless a concrete blocker appears.
- Current package set is compatible with target runtime `net10.0` and should be preserved for this story:
  - `Markdig` 1.3.2
  - `Spectre.Console` 0.57.2
  - `Spectre.Console.Cli` 0.55.0
  - `YamlDotNet` 18.1.0
- Mermaid integration currently uses CDN `mermaid@11`; avoid changing this in this story unless directly required by navigation/readability scope.

## Testing Requirements

- Add/extend unit tests that verify:
  - nav item omission when inputs are missing,
  - active-page assignment and `aria-current`,
  - breadcrumb trails for home/section/detail pages,
  - dashboard routing links presence when data exists.
- Run all existing tests to prevent regressions in parsing/rendering:
  - `dotnet test`

## UX and Accessibility Requirements

- Preserve light-first antiquarian visual system and status semantics. [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md]
- Ensure sticky nav usability on desktop and mobile breakpoints. [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md#sticky-navigation]
- Keep keyboard and ARIA semantics for navigational elements, breadcrumbs, and focus behavior. [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md#accessibility-floor]

## Reinvention and Regression Guardrails

- Do not duplicate navigation logic outside `SiteNav`; compose through existing model.
- Do not hardcode page paths in multiple template files when shared constants already exist (`SiteNav` constants).
- Do not regress graceful-missing behavior for absent artifact classes.
- Do not break story artifact parsing section contracts (`## Story`, `## Acceptance Criteria`, `## Dev Agent Record`) while adjusting page templates.

## Project Context Reference

- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md`
- Architecture spine: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- Rendering architecture: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`
- UX design: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`
- UX behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow run on 2026-07-05 for story key `1-1-dashboard-navigation-and-readability-foundation`
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~SiteNavTests|FullyQualifiedName~HtmlTemplaterTests"` (red/green cycle for this story)
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` (full regression pass)

### Implementation Plan

- Extend `SiteNav.RenderNavBar` with mobile navigation toggle semantics and keyboard-safe open/close behavior while preserving omission-safe link generation.
- Add explicit dashboard quick links in `HtmlTemplater` driven directly from nav composition so discoverability stays data-driven and omission-safe.
- Keep active-link and breadcrumb semantics canonical by retaining route-relative path handling and adding explicit current-page semantics in breadcrumbs.
- Expand regression coverage with focused nav and dashboard template tests.

### Completion Notes List

- Story context assembled from epics + PRD + architecture spine + rendering architecture + UX docs + current codebase guardrails.
- Previous-story intelligence not applicable (this is the first story in Epic 1).
- Implemented mobile nav toggle/hamburger behavior for small breakpoints with keyboard close (`Escape`) and focus return to toggle.
- Added dashboard quick-link panel to improve entry-point discoverability for Epics, Requirements, ADRs, and source-derived pages when available.
- Preserved omission-safe nav composition and verified no dead links are emitted for unavailable classes.
- Enforced breadcrumb current-page semantics with `aria-current="page"` and verified nested relative URL behavior.
- Added regression tests in `SiteNavTests` and `HtmlTemplaterTests` for omission behavior, active semantics, breadcrumb depth, and dashboard link presence/absence.
- Full regression suite passed: 83 tests.

### File List

- _bmad-output/implementation-artifacts/1-1-dashboard-navigation-and-readability-foundation.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/SpecScribe/SiteNav.cs
- src/SpecScribe/HtmlTemplater.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/SiteNavTests.cs
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs

## Change Log

- 2026-07-05: Implemented Story 1.1 navigation and dashboard discoverability baseline; added mobile nav semantics, breadcrumb current-page semantics, and regression tests.
