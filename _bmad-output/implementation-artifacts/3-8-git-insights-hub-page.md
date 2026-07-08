# Story 3.8: Git Insights Hub Page

Status: backlog

<!-- Drafted 2026-07-08 during the Epic-3 git-insights re-plan (owner-directed). Kept `backlog` (not ready-for-dev) because it depends on Story 3.2's shared git data foundation + `--deep-git` gate landing first — see Dependencies. Validation optional: run validate-create-story before dev-story. -->

## Story

As a maintainer,
I want a dedicated aggregate "Git Insights" page,
so that I can explore repository activity in depth without cluttering the dashboard.

## Acceptance Criteria

1. **Given** deep git insights are enabled **When** generation completes **Then** the portal produces an aggregate Git Insights page summarizing file change frequency, activity over time, and contributor attribution **And** its tables can be sorted and filtered client-side as a progressive enhancement while remaining readable and navigable without JavaScript. [Source: epics.md#Story 3.8; PRD FR-10, NFR-5]
2. **Given** the Git Insights page references individual files and commits **When** I select an entry **Then** I navigate to the corresponding per-file or per-commit detail page **And** when deep insights are disabled the heavier hub and detail-page generation does not run and baseline generation performance is unaffected. [Source: epics.md#Story 3.8; PRD FR-10]

## Dependencies / Sequencing

- **Blocked on Story 3.2** for (a) the `--deep-git` opt-in flag (this hub is generated only when deep insights are enabled — AC #2's performance guarantee) and (b) the shared `git log --numstat` data foundation (file frequency, activity-over-time, contributor attribution all come from that one parse). Do not add a second git code path. [Source: 3-2 story Dev Notes — "Shared numstat foundation"]
- Per-file links (AC #2) resolve to 7.4's file pages and per-commit links to 7.5's commit pages; guard links on target availability until those land.
- Promote to `ready-for-dev` once 3.2 has merged (via create-story/validate-create-story).

## Tasks / Subtasks

- [ ] Task 1: Compute aggregate insight data (AC: #1)
  - [ ] Subtask 1.1: Extend the shared numstat parse (from 3.2) to also aggregate: per-file total change frequency, per-day (or per-week) activity series, and per-contributor change counts (attribution). Keep it a pure, testable helper in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) obeying the never-throw / 3s-timeout / UTF-8 / capped-history discipline. Contributor attribution is **permitted** (PRD non-goal amended 2026-07-08) but framed as "who changed what", never a ranked productivity leaderboard.
- [ ] Task 2: Render the hub page (AC: #1)
  - [ ] Subtask 2.1: Add a `GitInsightsTemplater` mirroring [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs): a synthesized page (no markdown source) that builds its own shell via `PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb`, output path e.g. `git/index.html`. Render the three sections as accessible tables/SVG, sorted at generation time (generation-time order is the source of truth).
  - [ ] Subtask 2.2: Wire emission into [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) mirroring `GenerateCommitDaysInternal` (own generation phase, `File.WriteAllText`, run `ApplyReferenceLinks`, record an entry type like `CommitDayEntry`). Gate the whole emission behind `_options.DeepGitAnalytics` so nothing runs when the flag is off (AC #2).
  - [ ] Subtask 2.3: Add a nav/breadcrumb entry point (e.g. a "Git" link) and a "View all git insights →" link from the consolidated dashboard panel (3.7), both guarded on the page existing.
- [ ] Task 3: Progressive-enhancement interactivity (AC: #1)
  - [ ] Subtask 3.1: Add sort/filter/expand as **progressive enhancement only** — extend the existing embedded script `src/SpecScribe/assets/specscribe.js` (shipped via `ForgeOptions.ScriptName`). Tables must be fully readable, ordered, and navigable with JavaScript disabled; JS only re-orders/filters already-present rows. Honor reduced-motion and non-color cues (1.4/1.5). [Source: PRD NFR-5; rendering-architecture.md § Client-Side Enhancement Policy]
- [ ] Task 4: Test coverage (AC: #1, #2)
  - [ ] Subtask 4.1: Pure-parser tests for the aggregation helper (frequency ordering/top-N, activity bucketing, attribution counts, malformed lines skipped, empty history → empty). Templater test that the hub renders the three sections with real data and degrades gracefully at zero data. Gate test: with `DeepGitAnalytics == false` the page is not emitted.

## Dev Notes

- **Reuse the detail-page precedent.** `CommitDayTemplater` + `SiteGenerator.GenerateCommitDaysInternal` are the exact pattern for a synthesized, static insight page (bespoke shell, `ApplyReferenceLinks`, recorded entry). Mirror it; do not invent a new page-emission path.
- **One git code path.** All hub data must come from the shared numstat parse introduced in 3.2 — no new `git log` invocation here. [Source: 3-2 story Dev Notes]
- **JS is the sanctioned relaxation for THIS surface.** Per the 2026-07-08 owner decision, insight surfaces may use progressive-enhancement JS (PRD NFR-5). This is the one place the no-JS default bends — and only as enhancement. See [[charting-is-pure-svg-no-js]].
- **Attribution, not ranking.** Contributor sections show who touched what as collaboration context; do not present a "most productive" ranking. [Source: PRD FR-10 Out of Scope, amended 2026-07-08]
- Watch the O(files²)/history bounds already flagged for git work ([deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md)); cap the window consistent with 3.1/3.2.

## References

- [Source: epics.md#Story 3.8] — user story + acceptance criteria.
- [Source: PRD FR-10 (amended 2026-07-08)] — navigable git surfaces + attribution-not-ranking.
- [Source: PRD NFR-5; rendering-architecture.md § Client-Side Enhancement Policy] — progressive-enhancement JS guardrails.
- [Source: src/SpecScribe/CommitDayTemplater.cs + SiteGenerator.cs GenerateCommitDaysInternal] — synthesized static-page precedent to mirror.
- [Source: src/SpecScribe/GitMetrics.cs] — never-throw git module + shared numstat parse (from 3.2) to aggregate.
- [Source: 3-2-optional-deep-git-analytics-controls.md] — the `--deep-git` gate + shared foundation this story depends on.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
