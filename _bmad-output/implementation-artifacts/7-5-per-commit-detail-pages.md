# Story 7.5: Per-Commit Detail Pages

Status: backlog

<!-- Drafted 2026-07-08 during the Epic-3 git-insights re-plan (owner-directed). Kept `backlog` because it belongs to Epic 7 (Code & Git Exploration), depends on the shared git data foundation, and reads best after 7.3 (date pages) framing. Validation optional: run validate-create-story before dev-story. -->

## Story

As a contributor,
I want a page for each significant commit,
so that I can read what changed and why without leaving the portal.

## Acceptance Criteria

1. **Given** git history is available and detail pages are enabled **When** I open a commit's page **Then** it shows the commit subject, full commit message body, author and date, and the files changed with per-file line churn **And** recognized references in the message (for example "Story N.M" or "FR-9") link to their artifacts. [Source: epics.md#Story 7.5; PRD FR-10]
2. **Given** a commit page lists changed files and its author **When** I follow those links **Then** file entries lead to the corresponding file page and the author is shown as attribution, never as a productivity ranking **And** page generation is bounded and degrades non-fatally when history is unavailable or partial. [Source: epics.md#Story 7.5; PRD FR-10, non-goal amended 2026-07-08]

## Dependencies / Sequencing

- **Data foundation:** needs commit metadata + message **body** (`%b`) + per-file churn (`--numstat`). Extend the shared git parse (3.2) rather than adding a new path. Body is multi-line, so the fetch needs a record/field sentinel (e.g. `%x01`/`%x00`) so the pure parser can find commit and field boundaries unambiguously — mirror `ParseLog`'s tab-delimited discipline.
- **Bounding is contractual (AC #2):** generating a page per commit explodes on mature repos. Bound the set — reuse the `Charts.LinkedCommitDays` philosophy: generate pages only for commits within the capped window already fetched (and/or gate behind `--deep-git`). Do not generate unbounded history.
- **File links (AC #2)** resolve to 7.4's per-file pages; **reference links** reuse the existing `ApplyReferenceLinks` path (7.2 territory). Guard on target availability.
- Relates to 7.3 (date pages link to per-commit pages) and 3.8 (hub links to per-commit pages).

## Tasks / Subtasks

- [ ] Task 1: Fetch + parse commit detail data (AC: #1)
  - [ ] Subtask 1.1: Extend the shared bounded git fetch to include `%b` (body) and `--numstat` per-file added/deleted counts, with an unambiguous sentinel-delimited format. Parse in a pure, testable helper in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) (never-throw, skip malformed, 3s timeout, UTF-8 — reuse `RunGit`). Produce a `CommitDetail` record (hash, author, date, subject, body, files-with-churn).
- [ ] Task 2: Render per-commit pages (AC: #1)
  - [ ] Subtask 2.1: Add a `CommitDetailTemplater` mirroring [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) exactly (synthesized shell, breadcrumb, `doc-body`). Output path `commit/{shortHash}.html`. Render subject as `<h1>`, the full body as readable prose, an author+date meta pill (attribution framing), and a files-changed table with per-file +added / −deleted counts.
  - [ ] Subtask 2.2: Wire emission into [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) mirroring `GenerateCommitDaysInternal` (own phase, wipe+recreate `commit/` dir, `File.WriteAllText`, run `ApplyReferenceLinks` so "Story N.M"/"FR-9" mentions in subject/body link out, record a `CommitDetailEntry`). Gate/bound per Dependencies.
- [ ] Task 3: Cross-linking (AC: #1, #2)
  - [ ] Subtask 3.1: Link each changed-file row to its per-file page (7.4) when generated; link the commit's short hash from the existing `commits/{date}.html` day pages ([CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) currently renders the hash as plain `<code>` — make it a link when the commit page exists) and from the hub (3.8). Guard all links on target existence.
- [ ] Task 4: Test coverage (AC: #1, #2)
  - [ ] Subtask 4.1: Pure-parser tests: body with blank lines/multiple paragraphs survives; numstat +/- parsed; binary files (`-`/`-` numstat) handled; malformed lines skipped; empty → empty. Templater test: page renders subject/body/author/files+churn and reference links resolve. Bounding test: only the capped set of commit pages is emitted; unavailable history degrades non-fatally.

## Dev Notes

- **Reuse the detail-page precedent.** `CommitDayTemplater` + `SiteGenerator.GenerateCommitDaysInternal` + `CommitDayEntry` are the exact template/wiring/record trio to mirror for `commit/{hash}.html`. `ApplyReferenceLinks` already turns "Story N.M"/"FR25" mentions into links — reuse it for commit subjects/bodies (AC #1).
- **One git code path.** Extend the shared numstat/body parse (3.2) — do not add a second `git log` invocation. Bodies + numstat come from one bounded call.
- **Attribution, not ranking.** Show author as "who made this change"; never aggregate into a per-author leaderboard here. [Source: PRD FR-10 Out of Scope, amended 2026-07-08]
- **Bound history + page count.** Same 3s-timeout / mature-repo risk flagged in [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md) and Stories 3.1/3.2; cap the fetch and the generated-page set.
- **Escaping:** commit bodies are free text (may contain `<`, `>`, markup-like content) — route through the existing `PathUtil.Html` escaping as `CommitDayTemplater` does. Preserve paragraph breaks without letting raw HTML through.

## References

- [Source: epics.md#Story 7.5] — user story + acceptance criteria.
- [Source: PRD FR-10 (amended 2026-07-08)] — per-commit detail pages (subject, body, files + churn) + attribution-not-ranking.
- [Source: src/SpecScribe/CommitDayTemplater.cs, CommitDayEntry.cs] — the page template + entry record to mirror.
- [Source: src/SpecScribe/SiteGenerator.cs — GenerateCommitDaysInternal] — page-emission wiring + `ApplyReferenceLinks`.
- [Source: src/SpecScribe/GitMetrics.cs — RunGit, ParseLog] — never-throw git module + pure-parser pattern to extend for body+numstat.
- [Source: src/SpecScribe/Charts.cs — LinkedCommitDays] — the bounding philosophy for which commits get pages.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
