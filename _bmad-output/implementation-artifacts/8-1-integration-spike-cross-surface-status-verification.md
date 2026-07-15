---
baseline_commit: 03fd47503b1a39c72682df465399b38b3f690683
---

# Story 8.1: Integration Spike — Cross-Surface Status Verification

Status: done

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

- [x] **Task 1 — Trace the current cross-surface status path (AC: #1)**
  - [x] Read `StatusStyles.cs`, the `--status-*` tokens in `specscribe.css`, and confirm `HtmlRenderAdapter`, `WebviewRenderAdapter` (Story 6.4), and the SPA adapter (Story 6.7) all consume the same view models with no surface re-deriving its own status word or color.
  - [x] Confirm the CLI's `ConsoleUi.PrintInitialSummary` (non-fatal notices, counts) draws from the same generation data, not a parallel calculation.
  - [x] Note any surface that reads status/counts through a different path than the other two.
- [x] **Task 2 — Map Epic 8's seven stories against the three surfaces (AC: #2)**
  - [x] For each of Stories 8.2–8.8, note (in a short table) whether its deliverable is: (a) shared-path — reaches all three surfaces automatically once built once; (b) surface-specific — needs dedicated work per surface (e.g. a webview-only or CLI-only rendering); (c) HTML-only by design (e.g. a chart or layout that intentionally doesn't apply to the CLI summary).
  - [x] Where a gap is found, add a short note to that story's own file (Dev Notes section) pointing back here, so the owning dev doesn't discover it mid-story.
- [x] **Task 3 — Record findings, no production changes (AC: #3)**
  - [x] Write findings directly into this story's Completion Notes below.
  - [x] If a genuine architectural gap is found (not just a "surface-specific work needed" note), flag it to the Project Lead and consider a light `correct-course` — don't silently decide it here.

### Review Findings

- [x] [Review][Patch] Annotate tracing-only scope: smoke deferred for Epic 8 instance [`8-1-integration-spike-cross-surface-status-verification.md` Completion Notes + `sprint-status.yaml` Epic 6 action comment] — owner chose accept tracing-only (2026-07-14)
- [x] [Review][Patch] Reclassify AC #2 surface map into Task 2's a/b/c taxonomy [`8-1-integration-spike-cross-surface-status-verification.md:100`]
- [x] [Review][Patch] Add file/line anchors to Debug Log References [`8-1-integration-spike-cross-surface-status-verification.md:78`]
- [x] [Review][Defer] Epic 6 standing surface-coverage action lacks machine-readable instance-vs-standing status [`sprint-status.yaml:315`] — deferred, pre-existing

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

Composer (Cursor agent router)

### Debug Log References

- Traced `StatusStyles` → templaters / `PageView.BodyHtml` → `WebviewRenderAdapter.RenderContent` (`WebviewRenderAdapter.cs:62-68`) / `JsonSpaRenderAdapter.RenderContent` (`JsonSpaRenderAdapter.cs:42-48`) — identical nav + breadcrumb + body composition.
- Confirmed `HtmlRenderAdapter.Render` alone appends `PathUtil.RenderFooter` (`HtmlRenderAdapter.cs:27-40`, `PathUtil.cs:121`); webview/SPA `RenderContent` regions omit the footer.
- Confirmed webview inlines `specscribe.css` but does not load `specscribe.js` (`WebviewRenderAdapter.cs:14-16`, `:76-79`); SPA/HTML keep the script (`JsonSpaRenderAdapter.cs:14`). Tooltips / `data-copy` are progressive on HTML/SPA only.
- Confirmed `ConsoleUi.PrintInitialSummary` tallies `GenerationEvent` outcomes only — no lifecycle badge path (`ConsoleUi.cs:120-148`).
- Outline payload in `SiteGenerator` already uses `StatusStyles.ForStory` / `ForEpicWithRetrospective` (`SiteGenerator.cs:2049-2070`) — same classifier, not a fork.

### Completion Notes List

**Verdict:** Epic 6's shared render path still holds. Status words/colors are not re-derived per surface. No ADR / `correct-course` required — gaps are placement and progressive-enhancement caveats for owning stories.

**Scope note (code review 2026-07-14):** This spike is **tracing-only** per AC #1–#3. Epic 6's standing action also mentions a live-host smoke check; that smoke was **intentionally deferred** for the Epic 8 instance (not performed here). Future net-new epic spikes should still record smoke passed/skipped explicitly.

#### AC #1 — Current status path

| Surface | Status word / color | Counts |
|---|---|---|
| **HTML** (`HtmlRenderAdapter`) | `StatusStyles` → badges/charts in `BodyHtml`; colors via `--status-*` in `specscribe.css` | `ProgressCalculator` / view builders |
| **Webview** (`WebviewRenderAdapter`) | Same `PageView.BodyHtml` + same CSS inlined; no reclassification | Same body |
| **SPA** (`JsonSpaRenderAdapter`) | Same `RenderContent` region as webview; production CSS | Same body |
| **CLI** (`ConsoleUi.PrintInitialSummary`) | **Does not project lifecycle status** | Generation outcome tallies (`Generated`/`Updated`/`Skipped`/`Error`) from the same `GenerationEvent` list — not a parallel progress calculator |

Portal surfaces (HTML + webview + SPA) share one status seam. CLI shares the generation-event stream for notices/outcome counts, not the lifecycle vocabulary — **by design**, not a drift bug.

#### AC #2 — Epic 8 story × surface map

Taxonomy (Task 2): **(a)** shared-path — reaches portal surfaces once built once; **(b)** surface-specific — dedicated work per surface; **(c)** HTML-only by design.

| Story | Class | Notes |
|---|---|---|
| **8.2** Legend + classifier harden | **(a)** | Classifier/`Badge` in body = (a). Legend must land in the shared content region — footer-only would become **(c) by accident** (HTML shell only; webview/SPA omit footer), not by design. `js-tip` = HTML+SPA progressive; legend key is the webview-safe channel. CLI = notice channel for unrecognized status. |
| **8.3** `ProjectCounts` | **(a)** | Ledger → view builders → body. Divergence notice on CLI (same generation stream). |
| **8.4** Paired progress | **(a)** | Body/CSS shared. Column `js-tip` weak on webview — pair with non-JS affordance (progressive, not a second classifier). |
| **8.5** Next-step commands | **(b)** | Panel HTML fragment is (a); copy/`data-copy` (JS) and optional webview `stageCommand` are (b). Comment mis-labels owning story as 8.4. CLI does not render Next Steps. |
| **8.6** Empty states | **(a)** | Banner + empty lanes in body; board shared with home Now & Next. CLI out of scope (not (c)). |
| **8.7** One primary view | **(a)** | Pure CSS `:has()` toggle works on webview (no JS). CLI N/A. |
| **8.8** Recency markers | **(a)** | View-model dates in body; CLI N/A. |

Sibling Dev Notes updated: `8-2` … `8-8` each carry a "Cross-surface note from Story 8.1" block.

#### AC #3 — No production code

Zero changes under `src/` or `tests/`. Artifacts only: this story + sprint-status + sibling story Dev Notes.

### File List

- `_bmad-output/implementation-artifacts/8-1-integration-spike-cross-surface-status-verification.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/8-2-canonical-status-model-with-portal-wide-legend.md`
- `_bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md`
- `_bmad-output/implementation-artifacts/8-4-paired-progress-and-readiness-semantics.md`
- `_bmad-output/implementation-artifacts/8-5-state-aware-next-step-command-surface.md`
- `_bmad-output/implementation-artifacts/8-6-designed-empty-states.md`
- `_bmad-output/implementation-artifacts/8-7-one-primary-view-per-dashboard-dataset.md`
- `_bmad-output/implementation-artifacts/8-8-generation-time-recency-signals.md`

## Change Log

- 2026-07-14 — Story created (correct-course, epic-7 retrospective). Seats Epic 6 Retrospective Action Item #3 for Epic 8. Renumbered Epic 8's existing Stories 8.1–8.7 to 8.2–8.8 in the same change (`epics.md` and `sprint-status.yaml` updated together per Epic 6 Action Item #2).
- 2026-07-14 — Spike completed: cross-surface path verified; surface map written; gaps fed into 8.2–8.8 Dev Notes; no production code. Status → review.
- 2026-07-14 — Code review patches: tracing-only/smoke-deferred scope note; AC #2 map reclassified to (a)/(b)/(c); Debug Log file/line anchors. Status → done.
