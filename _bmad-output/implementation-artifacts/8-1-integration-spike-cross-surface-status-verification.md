# Story 8.1: Integration Spike — Cross-Surface Status Verification

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want a quick hands-on check that Epic 8's canonical status vocabulary, counts, and next-step commands can actually reach every live surface — HTML/web, the VS Code extension + webview, and the CLI console summary — before any of Epic 8's seven stories start,
So that a rework doesn't surface mid-epic the way it might have without a spike (matching the precedent set by Stories 6.3 and 6.6).

## Why this story exists (read first)

Inserted 2026-07-14 during the Epic 7 retrospective, via `correct-course`. Epic 6's retrospective (2026-07-12) committed to a process rule: **every net-new epic verifies the feature reaches all live UI surfaces before dev starts** (Action Item #3), following the spike-led pattern already used for 6.3 (VS Code integration) and 6.6 (delivery architecture). Epic 8's seven stories (originally 8.1–8.7, renumbered 8.2–8.8 in this same change) were already drafted in full when that rule was made — none had started dev, which made this the last clean window to seat the spike ahead of them.

This is a **light** spike, not a full architecture investigation like 6.3. Epic 8 isn't introducing a new render surface (unlike Epic 6's webview) — it's extending the *existing* shared view-model path (`StatusStyles`, the `--status-*` tokens, `HtmlRenderAdapter`/`WebviewRenderAdapter`/SPA) that Epic 6 already proved reaches all three surfaces. The job here is to **verify that's still true** before Epic 8 leans on it further, and to flag any surface-specific gap early rather than at each story's review.

**The one-line test for "is this in scope?":** if the change *traces* existing code to confirm/deny cross-surface reach, or *documents* a gap for a specific downstream story → in. If it *builds* any of Epic 8's actual features (status legend, count consolidation, next-step commands, etc.) → out; that's Stories 8.2–8.8.

## Acceptance Criteria

1.
**Given** the current `StatusStyles`/`--status-*` token system, the shared view-model contract (Story 6.1), and the webview/SPA render adapters (Stories 6.4, 6.7)
**When** a status word, count, or badge is projected today
**Then** this spike confirms (by tracing actual code, not assumption) that all three live surfaces — `HtmlRenderAdapter`, `WebviewRenderAdapter`, and the CLI's `ConsoleUi` summary — read from the same single source, and names any surface that does not.

2.
**Given** Epic 8's planned additions (a status legend affordance — 8.2; a single count source — 8.3; paired progress/readiness — 8.4; state-aware next-step commands — 8.5; designed empty states — 8.6; one primary view per dataset — 8.7; generation-time recency signals — 8.8)
**When** each is mapped against the three live surfaces
**Then** the spike records, per surface, whether the addition is expected to reach it automatically (because it rides the shared `HtmlRenderAdapter.RenderStoryBody`/view-model path), needs surface-specific work, or is HTML-only by design (and why)
**And** any surface gap found is fed into the owning story's Dev Notes before that story starts.

3.
**Given** the spike's findings
**When** it concludes
**Then** no production code changes land from this story — it is a tracing/verification pass, not a build — and its output is a short findings note appended to this story's Completion Notes (no new ADR required unless a surface gap forces an architectural choice; if one does, escalate via `correct-course` rather than deciding it inline here).

## Tasks / Subtasks

- [ ] **Task 1 — Trace the current cross-surface status path (AC: #1)**
  - [ ] Read `StatusStyles.cs`, the `--status-*` tokens in `specscribe.css`, and confirm `HtmlRenderAdapter`, `WebviewRenderAdapter` (Story 6.4), and the SPA adapter (Story 6.7) all consume the same view models with no surface re-deriving its own status word or color.
  - [ ] Confirm the CLI's `ConsoleUi.PrintInitialSummary` (non-fatal notices, counts) draws from the same generation data, not a parallel calculation.
  - [ ] Note any surface that reads status/counts through a different path than the other two.
- [ ] **Task 2 — Map Epic 8's seven stories against the three surfaces (AC: #2)**
  - [ ] For each of Stories 8.2–8.8, note (in a short table) whether its deliverable is: (a) shared-path — reaches all three surfaces automatically once built once; (b) surface-specific — needs dedicated work per surface (e.g. a webview-only or CLI-only rendering); (c) HTML-only by design (e.g. a chart or layout that intentionally doesn't apply to the CLI summary).
  - [ ] Where a gap is found, add a short note to that story's own file (Dev Notes section) pointing back here, so the owning dev doesn't discover it mid-story.
- [ ] **Task 3 — Record findings, no production changes (AC: #3)**
  - [ ] Write findings directly into this story's Completion Notes below.
  - [ ] If a genuine architectural gap is found (not just a "surface-specific work needed" note), flag it to the Project Lead and consider a light `correct-course` — don't silently decide it here.

## Dev Notes

- This spike should be quick — it's a tracing exercise over code that already exists and was already proven cross-surface-capable by Epic 6 (`RenderParity`, the shared `IRenderAdapter` contract). Don't rebuild or refactor anything found here; that belongs to the owning story if a gap surfaces.
- Read-only: no source artifacts should be touched by this story beyond the findings note and any Dev Notes additions to sibling story files.
- If nothing unexpected is found, that is a valid and useful outcome — record "no gaps found" plainly rather than manufacturing findings.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 8 goal + Story 8.1 ACs (inserted 2026-07-14)]
- [Source: `_bmad-output/implementation-artifacts/epic-6-retro-2026-07-12.md` — Action Item #3, the process rule this story seats]
- [Source: `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/assets/specscribe.css`] — the single classifier + token source to trace
- [Source: `src/SpecScribe/HtmlRenderAdapter.cs`, `WebviewRenderAdapter` (Story 6.4), the SPA adapter (Story 6.7)] — the three render surfaces
- [Source: `src/SpecScribe/ConsoleUi.cs`] — the CLI summary surface
- [Source: `src/SpecScribe/RenderParity.cs`] — the existing cross-surface parity harness this spike leans on rather than duplicates

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-14 — Story created (correct-course, epic-7 retrospective). Seats Epic 6 Retrospective Action Item #3 for Epic 8. Renumbered Epic 8's existing Stories 8.1–8.7 to 8.2–8.8 in the same change (`epics.md` and `sprint-status.yaml` updated together per Epic 6 Action Item #2).
