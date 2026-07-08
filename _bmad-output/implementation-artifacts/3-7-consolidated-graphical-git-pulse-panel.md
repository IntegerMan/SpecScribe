# Story 3.7: Consolidated, Graphical Git Pulse Panel

Status: ready-for-dev

<!-- Drafted 2026-07-08 during the Epic-3 git-insights re-plan (owner-directed). Validation optional: run validate-create-story before dev-story. -->

## Story

As a maintainer,
I want the dashboard's git activity consolidated into one richer, more graphical panel,
so that project momentum reads clearly at a glance and invites me to explore further.

## Acceptance Criteria

1. **Given** the dashboard currently shows commit activity and baseline git pulse as two separate panels **When** the dashboard renders with git history available **Then** a single consolidated git panel presents the activity heatmap together with the baseline signals (last commit, 30-day count, top changed files) **And** the presentation is more graphical than a plain list while preserving the accessibility and truthfulness conventions from Stories 1.4/1.5. [Source: epics.md#Story 3.7]
2. **Given** the consolidated panel and the git detail/insight surfaces exist **When** I interact with the panel **Then** its elements (for example top changed files and activity) link into the corresponding detail or insight pages when those pages are generated **And** when git history is unavailable the panel shows a single non-fatal fallback state. [Source: epics.md#Story 3.7; PRD FR-10]

## Dependencies / Sequencing

- **Independent of the deep-analytics gate** — this is a presentation refactor of the *baseline* pulse (FR-9 data that already exists), so it can ship before 3.2/3.8. It renders whenever `p.Git` is non-null, exactly like today's two panels.
- The AC #2 links to per-file (7.4) and per-commit (7.5) / hub (3.8) pages are **conditional**: link only when those targets are generated. Until then, keep the existing heatmap→`commits/{date}.html` links and render top files as plain text. Do not hard-depend on unbuilt pages.

## Tasks / Subtasks

- [ ] Task 1: Merge the two dashboard panels into one (AC: #1)
  - [ ] Subtask 1.1: In `AppendDashboard` ([HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs), the `chart-row` holding the Epic Status donut + "Commit Activity" heatmap, and the separate "Git Pulse" panel that follows it), combine the heatmap and the Git Pulse signals into a **single** `chart-panel` (e.g. heading "Git Pulse" or "Repository Activity"). Remove the now-redundant standalone "Commit Activity" panel heading. Keep the Epic Status donut where it is.
  - [ ] Subtask 1.2: Preserve the exact `p.Git is { } … : …` fallback so the whole consolidated panel degrades to one non-fatal empty state (reuse Story 3.1's `git-pulse-empty` `—` + tooltip copy; do not show two separate fallbacks). [Source: EXPERIENCE.md:169]
- [ ] Task 2: Make the panel more graphical (AC: #1)
  - [ ] Subtask 2.1: In [Charts.cs](../../src/SpecScribe/Charts.cs), enrich the presentation with **pure inline SVG** (no JS — chart primitives stay SVG per the established convention; see [[charting-is-pure-svg-no-js]]). Suggested: render the top-changed-files list as proportional horizontal bars (bar width ∝ change count) rather than a plain list, and keep the existing `CommitHeatmap` SVG as the activity visual. Reuse neutral chart CSS variables (parchment/ink/rust) — git activity is **not** a lifecycle status, so the `--status-*` tokens do not apply.
  - [ ] Subtask 2.2: Keep all accessibility affordances from 1.4/1.5: text labels and counts never color-only, focusable tooltips consistent with other chart panels, whole-chart aria-label on the heatmap. Reuse `Charts.Plural` / `Html`.
- [ ] Task 3: Make elements actionable where targets exist (AC: #2)
  - [ ] Subtask 3.1: When per-file pages (7.4) or the Git Insights hub (3.8) are generated, link each top-changed-file row to its file page and add a "View all git insights →" link to the hub. Guard each link on target availability so an unbuilt page never yields a broken link (mirror how Story 3.1 kept links conditional).
- [ ] Task 4: Test coverage (AC: #1, #2)
  - [ ] Subtask 4.1: Update [HtmlTemplaterTests.cs](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs): the consolidated panel renders heatmap + baseline signals together when `p.Git` is populated; the single fallback renders when `p.Git` is null; the old duplicate "Commit Activity" heading is gone. Reuse the `ProgressWithCommits` helper.

## Dev Notes

- **Reuse, don't reinvent.** `Charts.CommitHeatmap` and `Charts.GitPulsePanel` already exist ([Charts.cs](../../src/SpecScribe/Charts.cs)); this story recomposes them into one panel and upgrades the visuals — it does not add a new data source. Baseline git data flows via `ProgressModel.Git` from the single `GitMetrics.TryCompute` call ([SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)) — unchanged here.
- The two panels this story merges are the "Commit Activity" heatmap and the Story 3.1 "Git Pulse" panel in `AppendDashboard`. Do not touch the "Commits" stat card in the stat-grid (that headline number stays).
- Charts stay **pure SVG, no JS** (the NFR-5 progressive-enhancement JS relaxation is for the *hub page* 3.8, not for dashboard chart primitives). See [[charting-is-pure-svg-no-js]].
- Preserve the tooltip-overflow lift pattern Story 3.1 added (`.chart-panel:has(.git-pulse-empty…) { overflow: visible }`) if the empty state stays inside the consolidated panel.

## References

- [Source: epics.md#Story 3.7] — user story + acceptance criteria.
- [Source: PRD FR-10] — "Baseline pulse elements … link to these detail pages so git context is navigable" (the actionable requirement AC #2 realizes).
- [Source: src/SpecScribe/HtmlTemplater.cs — AppendDashboard] — the `chart-row` + "Git Pulse" panel to consolidate.
- [Source: src/SpecScribe/Charts.cs — CommitHeatmap, GitPulsePanel, Plural, Html] — helpers to recompose/enrich.
- [Source: tests/SpecScribe.Tests/HtmlTemplaterTests.cs — ProgressWithCommits] — templater test helper to extend.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
