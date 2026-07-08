# Story 3.1: Baseline Git Pulse Insights on Dashboard

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want lightweight git activity metrics in the portal,
so that I can assess project momentum at a glance.

## Acceptance Criteria

1. **Given** git history is available **When** I view the dashboard **Then** I see last commit timestamp, 30-day commit count, and top changed files **And** values are derived from local repository history. [Source: epics.md#Story 3.1; PRD FR-9]
2. **Given** git history is unavailable or fails to load **When** generation runs **Then** generation still succeeds **And** dashboard shows a non-fatal fallback state. [Source: epics.md#Story 3.1; PRD FR-9]

## Tasks / Subtasks

- [ ] Task 1: Extend `GitPulse`/`GitMetrics` with the three new signals (AC: #1, #2)
  - [ ] Subtask 1.1: Add `LastCommitTimestamp` (`DateTime`) to the `GitPulse` record in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs). Derive it from data already parsed by `ParseLog` — do **not** add a new git call for this. The last active day's commit list (`CommitsByDay[LastCommitDate]`) is ordered newest-first (per `GitMetricsTests.ParseLog_GroupsCommitsByDayInAscendingOrderWithAuthorAndTime`), so its first entry's `Time` combined with `LastCommitDate` gives the exact last-commit timestamp. Simplest: capture the raw `DateTime stamp` for the very first parsed log line inside `ParseLog` (git emits newest-first) and return it alongside the existing tuple, or thread it through `TryCompute` directly from the first line of `logText`.
  - [ ] Subtask 1.2: Add `Last30DayCommitCount` (`int`) to `GitPulse`. Compute by summing `DailySeries` entries where `Day` falls within the last 30 days of "today" (`DateOnly.FromDateTime(DateTime.Now)`), inclusive. This needs **no new git call** — the existing single `git log` call already fetches full history.
  - [ ] Subtask 1.3: Add `TopChangedFiles` (`IReadOnlyList<(string Path, int ChangeCount)>`) to `GitPulse`. This *does* require a new git invocation (name-only log), e.g. `git log --name-only --pretty=format:`. **Cap the window** (e.g. `-n 200` or `--since=90.days`) — [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md) already flags that uncapped `git log` on the heatmap risks the 3s `RunGit` timeout on large repos; don't repeat that mistake for a new call. Parse into a pure, testable helper (mirror `ParseLog`'s pattern: static method taking raw git output, returning parsed data, skipping malformed lines) so it's unit-testable without a repo. Sort by frequency descending, take top 5.
  - [ ] Subtask 1.4: Keep `GitMetrics.TryCompute` never-throwing — wrap the new git call in the same try/catch-and-return-null discipline as the existing calls. If the new call fails but the original `rev-list`/`log` calls succeeded, prefer degrading `TopChangedFiles` to an empty list rather than nulling the whole `GitPulse` (partial data beats no data, consistent with AD-4's "insight providers are additive, non-blocking").
  - [ ] Subtask 1.5: Update the two existing `new GitPulse(...)` call sites for the new positional fields — [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) (production construction inside `TryCompute`) and [HtmlTemplaterTests.cs:164](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs) (`ProgressWithCommits` test helper). These are the *only* two construction sites in the repo (confirmed via search) — the record change is otherwise additive and won't ripple further.

- [ ] Task 2: Render the Git Pulse panel on the dashboard (AC: #1, #2)
  - [ ] Subtask 2.1: Add a rendering helper in [Charts.cs](../../src/SpecScribe/Charts.cs) (mirror `CommitHeatmap`'s pattern: pure inline SVG/HTML + CSS, no JS) that renders last commit timestamp, 30-day commit count, and the top-changed-files list. Reuse `Charts.Plural` for count labels and the existing `Html()` escaping helper.
  - [ ] Subtask 2.2: Call the new helper from `AppendDashboard` in [HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs) (~line 246-253, alongside the existing "Commit Activity" heatmap panel in the `chart-row`). Do not duplicate the existing "Commits" stat card (`p.Git.TotalCommits`, line 201-204) or the heatmap (line 250-252) — this is a **new, additional** panel surfacing the three specific signals from AC #1 that neither existing element currently shows (30-day rolling count, top changed files, exact last-commit timestamp vs. the existing "Xd ago" recency string).
  - [ ] Subtask 2.3: Match the empty-state copy exactly from the UX spec: `"—"` with tooltip `"Run in a git repository to enable commit stats"` [Source: EXPERIENCE.md:169]. Follow the same `p.Git is { } git ? ... : ...` fallback pattern used for the existing Commits stat card (line 201) and heatmap (line 250) so AC #2's "non-fatal fallback state" reads consistently with the rest of the dashboard when `GitMetrics.TryCompute` returns `null`.

- [ ] Task 3: Test coverage (AC: #1, #2)
  - [ ] Subtask 3.1: Add pure-parser tests in [GitMetricsTests.cs](../../tests/SpecScribe.Tests/GitMetricsTests.cs) for the new name-only-log parsing helper — cover: frequency ordering/top-N truncation, malformed/empty lines skipped (never-throw), and a repo with zero file changes.
  - [ ] Subtask 3.2: Add a test proving `Last30DayCommitCount` correctly excludes commits older than 30 days from `DailySeries` (boundary case: exactly 30 days ago is included, 31 days ago is not).
  - [ ] Subtask 3.3: Add `HtmlTemplaterTests.cs` coverage for the new panel: renders real data when `p.Git` is populated, and renders the exact `"—"` / tooltip fallback copy when `p.Git` is `null`.

## Dev Notes

- **Reuse, don't reinvent.** `GitMetrics.TryCompute` in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) already shells out to git once (`rev-list --count HEAD` + one `log` call) and produces `GitPulse` with `TotalCommits`, `ActiveDays`, `FirstCommitDate`, `LastCommitDate`, `DailySeries`, and `CommitsByDay`. This is consumed today by the dashboard's "Commits" stat card and "Commit Activity" heatmap ([HtmlTemplater.cs:201-204, 249-253](../../src/SpecScribe/HtmlTemplater.cs)) via `ProgressModel.Git` ([ProgressModel.cs:32](../../src/SpecScribe/ProgressModel.cs)), threaded from `SiteGenerator.GenerateEpicsInternal` ([SiteGenerator.cs:392](../../src/SpecScribe/SiteGenerator.cs): `GitMetrics.TryCompute(_options.RepoRoot)`, called once, not per-page). This story **extends** that existing pipeline — it does not build a parallel one.
- `GitMetrics` is a never-throw, best-effort module by design (comment at [GitMetrics.cs:20-22](../../src/SpecScribe/GitMetrics.cs)): any git failure (missing binary, not a repo, timeout) yields `null`, and callers already treat that as "no git data" rather than an error. Preserve this contract for the new fields — see AD-4 in the architecture spine: "Optional insight providers may enrich output but never own baseline success" [Source: ARCHITECTURE-SPINE.md#AD-4].
- `RunGit` has a hardcoded 3-second `Timeout` ([GitMetrics.cs:25](../../src/SpecScribe/GitMetrics.cs)). A new uncapped `git log --name-only` call on a large repo risks blowing this budget — [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md) already flags the *existing* heatmap log call as an uncapped-history risk for mature repos; don't add a second uncapped call. Bound the new call (commit count or time-window flag).
- `GitPulse` is a positional `record` (`GitMetrics.cs:12-18`). Adding fields changes its constructor signature. Only two call sites construct it directly (`GitMetrics.cs` production code, `HtmlTemplaterTests.cs:164` test helper) — both must be updated, but nothing else in the codebase constructs `GitPulse` directly, so the blast radius is small and bounded.
- Windows encoding gotcha already solved for you: `RunGit` sets `StandardOutputEncoding = Encoding.UTF8` (`GitMetrics.cs:119`) so non-ASCII commit subjects/filenames don't get OEM-codepage-mangled. Reuse `RunGit` for the new git call rather than writing a second process-invocation helper.
- Culture-invariant date parsing matters: the existing `ParseLog` uses `DateTime.TryParseExact(..., CultureInfo.InvariantCulture, ...)` because a culture-sensitive parse under non-Gregorian calendars (Thai Buddhist, Persian) would corrupt every date (`GitMetrics.cs:76-77`, tested in `GitMetricsTests.ParseLog_IsCultureInvariant`). Apply the same discipline to any new date handling.
- Charts are pure inline SVG/HTML + CSS variables, **no JS** (`Charts.cs:6-8` class doc comment) — this is a deliberate, established project convention, not an oversight. Follow it for the new Git Pulse panel.
- `Charts.StatCard` (`Charts.cs:14-19`) already supports an on-brand CSS tooltip via `data-tooltip` + `tabindex="0"` — this is exactly the mechanism needed for the AC #2 empty-state tooltip; reuse it rather than inventing a new tooltip pattern.
- The UX spec's empty-state table gives the *exact* required copy for AC #2: `"—"` with tooltip `"Run in a git repository to enable commit stats"` [Source: EXPERIENCE.md:169]. Use this verbatim — it's a specified string, not a suggestion.
- PRD FR-9 explicitly scopes this story: "Dashboard includes, **at minimum**, last commit timestamp, 30-day commit count, and top changed files derived from local git history. Generation continues when git history is unavailable or command execution fails." [Source: prd.md:122-127]. FR-10 (deeper git analytics, opt-in) is **out of scope** — that's Story 3.2, not this one. Do not build toggle/settings plumbing here.
- PRD explicitly excludes "Producing people-ranking productivity metrics from git history" [Source: prd.md:178] — "top changed files" means file paths + change counts, not per-author rankings or leaderboards.
- No architecture.md exists for this project; the closest analog is [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) and its companion [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md). Both describe a target shared-core/adapter seed architecture (`IInsightProvider`, package splits like `SpecScribe.Core`) that **does not exist yet** — the current codebase is the single monolithic `src/SpecScribe` project. Per the spine's "Seed, Not Invariant" section: "the current monolithic implementation can be refactored as long as the shared-core contract stays intact" [Source: ARCHITECTURE-SPINE.md:100]. Do **not** attempt to introduce `IInsightProvider` or restructure into the seed package layout as part of this story — extend `GitMetrics.cs` in place, matching the existing code's actual shape, not the aspirational one.

### Project Structure Notes

- All changes land in the existing single-project layout: `src/SpecScribe/GitMetrics.cs` (model + computation), `src/SpecScribe/Charts.cs` (rendering helper), `src/SpecScribe/HtmlTemplater.cs` (dashboard wiring), `tests/SpecScribe.Tests/GitMetricsTests.cs` and `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` (tests). No new files or folders are needed — this is a pure extension of an established, already-integrated feature area.
- No conflicts detected between epics.md's AC wording and the PRD/UX docs — all three agree on last commit timestamp, 30-day count, and top changed files, plus the same non-fatal-fallback framing.

### References

- [Source: epics.md#Story 3.1 (lines 393-417)] — Epic 3 goal statement, FRs covered (FR9-FR11, FR14), and this story's user story + acceptance criteria.
- [Source: prd.md#FR-9 (lines 122-127)] — "Baseline git pulse": exact minimum-signal wording ("last commit timestamp, 30-day commit count, and top changed files") and the non-fatal-failure requirement.
- [Source: prd.md#FR-10 (lines 129-131)] — Deeper/opt-in git analytics is explicitly out of scope for this story (belongs to Story 3.2).
- [Source: prd.md line 178] — Explicit non-goal: no people-ranking productivity metrics from git history.
- [Source: prd.md line 187] — "Baseline git pulse on dashboard" listed under MVP in-scope.
- [Source: EXPERIENCE.md line 169] — Exact empty-state copy: `"—"` with tooltip `"Run in a git repository to enable commit stats"`.
- [Source: ARCHITECTURE-SPINE.md#AD-4] — Insight providers (git pulse named explicitly) are additive/non-blocking and never own baseline success.
- [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant] — Current monolithic implementation is intentional; don't force the seed package/interface split in this story.
- [Source: src/SpecScribe/GitMetrics.cs] — Existing `GitPulse` record, `GitMetrics.TryCompute`, `ParseLog`, and `RunGit` (3s timeout, UTF-8 stdout, never-throw contract) to extend.
- [Source: src/SpecScribe/HtmlTemplater.cs:186-271] — `AppendDashboard`, including the existing Commits stat card (201-204) and Commit Activity heatmap panel (249-253) this story's new panel sits alongside.
- [Source: src/SpecScribe/ProgressModel.cs:32] / [src/SpecScribe/SiteGenerator.cs:392] — How `GitPulse` flows from a single git invocation into the render pipeline via `ProgressModel.Git`.
- [Source: src/SpecScribe/Charts.cs:6-19] — Pure-SVG-no-JS chart convention; `StatCard`'s existing tooltip mechanism to reuse.
- [Source: tests/SpecScribe.Tests/GitMetricsTests.cs] — Existing pure-parser test pattern (feed raw git-format text, assert parsed structure, cover malformed-line skipping and culture invariance) to mirror for the new top-changed-files parser.
- [Source: tests/SpecScribe.Tests/HtmlTemplaterTests.cs:149-172] — `ProgressWithCommits` helper constructing `GitPulse` directly; must be updated for the new positional fields.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md lines 36-38] — Prior review already flagged uncapped `git log` history as a scaling risk; don't repeat it for the new top-changed-files git call.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
