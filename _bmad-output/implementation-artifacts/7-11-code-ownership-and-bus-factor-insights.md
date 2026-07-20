# Story 7.11: Code Ownership & Bus-Factor Insights

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer assessing project resilience,
I want to see how concentrated authorship is across the codebase,
so that knowledge silos ("only one person has touched this") become visible before they become a risk.

## Acceptance Criteria

1.
**Given** deep-git author attribution
**When** the ownership view renders
**Then** each file or area shows its dominant-author share and contributor count, and single-author concentrations are flagged as bus-factor risks using the existing sole-contributor vocabulary (`GitInsightsTemplater`)
**And** entries link to their code page (Story 7.2 seam).

2.
**Given** a solo-maintainer repo (the common OSS case, NFR8)
**When** ownership would trivially be "one person everywhere"
**Then** the surface reframes honestly (e.g., "single-maintainer project") rather than flagging every file as a bus-factor risk
**And** the classification is generation-time deterministic (FR31).

## Tasks / Subtasks

- [ ] Task 1 — Add a `ChartMetric.AuthorConcentration` case + `WhyText` framing sentence (AC: #1)
  - [ ] Subtask 1.1: In [Charts.cs](src/SpecScribe/Charts.cs:13) add `AuthorConcentration` to the `ChartMetric` enum (alongside `ActivityCadence`/`FileChurn`/`ChangeCoupling`).
  - [ ] Subtask 1.2: Add its `WhyText` case (~[Charts.cs:37](src/SpecScribe/Charts.cs:37)) — one generic, framework-neutral sentence, e.g. "Files with a single dominant author are a knowledge-silo risk if that person leaves or moves on." (NFR8: no project-specific wording.)

- [ ] Task 2 — Render the "Ownership & Bus-Factor" section on the Git Insights hub (AC: #1, #2)
  - [ ] Subtask 2.1: In [GitInsightsTemplater.cs](src/SpecScribe/GitInsightsTemplater.cs), add a third section (after `AppendFilesAndContributorsSection`, before or after `AppendActivitySection` — pick one placement and be consistent) rendered via a new private `AppendOwnershipSection(StringBuilder sb, GitInsightsData insights, Func<string,string?>? fileHref)` method, called from `RenderPage` alongside the other two `Append*Section` calls.
  - [ ] Subtask 2.2: Build a ranked table over `insights.Files` (already the top-N `FileChangeStat` list, change-count desc — reuse this ordering, do not recompute or re-fetch). For each file with `Contributors.Count > 0`: dominant share = `Contributors[0].Commits / (double)file.Changes` (Contributors is already sorted commits-desc — see [GitMetrics.cs:718-723](src/SpecScribe/GitMetrics.cs:718)), contributor count = `TotalContributors` (the full distinct-author count, not the capped `Contributors.Count` — mirrors the truncation-safe pattern already used at [GitInsightsTemplater.cs:208](src/SpecScribe/GitInsightsTemplater.cs:208) and [GitInsightsTemplater.cs:223](src/SpecScribe/GitInsightsTemplater.cs:223)).
  - [ ] Subtask 2.3: Flag single-author files (`TotalContributors <= 1`) as bus-factor risks, reusing the exact "Sole contributor:" wording convention already established at [GitInsightsTemplater.cs:208](src/SpecScribe/GitInsightsTemplater.cs:208) — do not invent new risk copy that duplicates it.
  - [ ] Subtask 2.4: AC #2 solo-repo reframe — gate on `insights.ContributorCount == 1` (the SAME global distinct-author check already used for the Story 10.6 hub-wide softening at [GitInsightsTemplater.cs:154](src/SpecScribe/GitInsightsTemplater.cs:154)). When true, render one honest "single-maintainer project" statement for the whole section instead of a table where every row is flagged as at-risk (a table of all-red risk flags in a solo repo is noise, not signal).
  - [ ] Subtask 2.5: Each row's file name links via the existing `fileHref` resolver already threaded into `RenderPage` (bound to `CodeItemHref` at the [SiteGenerator.cs:1911](src/SpecScribe/SiteGenerator.cs:1911) call site — Story 7.2/7.1 seam) using the same guarded-link-or-plain-text discipline as the rest of this file (`GuardedLink`, [GitInsightsTemplater.cs:275](src/SpecScribe/GitInsightsTemplater.cs:275)) — no new resolver, no dead links.
  - [ ] Subtask 2.6: Wrap the section in `Charts.Framed` with a `ChartMeta` using the new `AuthorConcentration` `WhyText` (Story 10.2 standard — match the `Title`/`Window`/`Ranking` slot usage already in `AppendFilesAndContributorsSection`).
  - [ ] Subtask 2.7: Empty state — `insights.Files.Count == 0` → the same `chart-empty` pattern used elsewhere on this page (e.g. [GitInsightsTemplater.cs:101](src/SpecScribe/GitInsightsTemplater.cs:101)), never an empty table.

- [ ] Task 3 — NFR8 shallow/non-git degrade (AC: #2)
  - [ ] Subtask 3.1: Confirm (and add a test proving) that when `--deep-git` is off — `git-insights.html` is not generated at all (existing gate) — this new section never partially renders. No new gating logic should be needed; this is a regression check, not new code.

- [ ] Task 4 — CSS (only if new markup classes are introduced)
  - [ ] Subtask 4.1: Reuse existing `.gi-*` table/row classes where the shape matches the Files & Contributors table; add new classes only for the risk-flag badge/pill and the solo-repo statement, following the existing `--status-*` token system ([Story: specscribe-status-token-system]) — do NOT invent a 7th ad-hoc color; a risk flag should read via an existing status tone (e.g. the "at-risk"/attention tone already used elsewhere), not a bespoke hue.
  - [ ] Subtask 4.2: Add/extend `StylesheetTests` assertions for any new CSS selector, scoped narrowly (mirror the pattern at [GitInsightsTemplaterTests.cs](tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs) and sibling `StylesheetTests` files — a scoped regex/selector match, not a repo-wide substring).

- [ ] Task 5 — Tests (AC: #1, #2)
  - [ ] Subtask 5.1: `GitInsightsTemplaterTests.cs` — extend `SampleInsights()` or add a second fixture with a mix of single- and multi-contributor files; assert the ownership section renders dominant-author share, contributor count, the "Sole contributor:" risk flag on the single-author file, and a guarded link when `fileHref` resolves (mirror the existing guarded-link test pattern in this file).
  - [ ] Subtask 5.2: Add a solo-repo fixture (`ContributorCount: 1`, every file `TotalContributors: 1`) and assert the section renders the single-maintainer reframe statement, NOT a table of all-flagged rows.
  - [ ] Subtask 5.3: Add a zero-files fixture and assert the empty state (no `<table>` markup) per Subtask 2.7.
  - [ ] Subtask 5.4: `Charts.cs` — if a `ChartsTests.cs`/`WhyTextTests` convention exists, add coverage that `WhyText(ChartMetric.AuthorConcentration)` returns non-empty, framework-neutral text.
  - [ ] Subtask 5.5: Regenerate the golden content fingerprint (git-insights.html body changes) — locate the current assertion in `SiteGeneratorAdapterTests.cs` (search `GoldenContentFingerprint`/`git-insights`) and update the expected hash; confirm the diff is CSS/HTML-only, no unrelated drift, per [Golden-diff normalization gotchas].

## Dev Notes

**Where this renders — already decided, do not re-litigate the surface.** The Git Insights hub (`git-insights.html`, `GitInsightsTemplater.RenderPage`) is the correct and only home for this feature. It already computes exactly the data this story needs — `GitInsightsData.Files` is a `List<FileChangeStat>`, each carrying `Contributors` (sorted commits-desc, capped) and `TotalContributors` (the true distinct-author count) — via `GitMetrics.BuildInsights` ([GitMetrics.cs:660](src/SpecScribe/GitMetrics.cs:660)). **No new git call, no new data model, no new page.** This is purely a new rendering section over data that already exists and is already threaded through the `RenderPage(insights, git, nav, fileHref, commitHref)` call at [SiteGenerator.cs:1911](src/SpecScribe/SiteGenerator.cs:1911).

**Do not build a second contributor-attribution pass.** `FileInsight` (used by the Story 7.4 "Advanced coverage" section on code pages, [GitMetrics.cs:148](src/SpecScribe/GitMetrics.cs:148)) is a *different* per-file record computed by a *different* method (`ComputeFileInsights`) for a *different* surface. This story uses `GitInsightsData.Files` / `FileChangeStat`, not `FileInsight`. Don't conflate the two or add a redundant computation.

**Dominant-author share math:** `Contributors` is already `OrderByDescending(commits).ThenBy(name)` at generation time ([GitMetrics.cs:718-723](src/SpecScribe/GitMetrics.cs:718)), so `Contributors[0]` IS the dominant author whenever the list is non-empty — no re-sort needed. Share = `Contributors[0].Commits / (double)file.Changes`. This is accurate even though `Contributors` is capped (top 12 by default) because `file.Changes` counts all commits touching the file regardless of the cap, and the dominant author is definitionally in the top slot.

**Reuse, don't rephrase, the sole-contributor vocabulary.** The epic AC explicitly calls out reusing `GitInsightsTemplater`'s existing wording. "Sole contributor:" already exists at [GitInsightsTemplater.cs:208](src/SpecScribe/GitInsightsTemplater.cs:208) for the per-file contributor panel. Use the same string for the bus-factor flag rather than inventing "Bus factor: 1" or similar — consistency of vocabulary across the page matters more than novelty.

**Solo-repo reframe reuses an existing pattern, not new detection logic.** `insights.ContributorCount == 1` (global distinct authors across the whole analyzed window) already gates a hub-wide copy change at [GitInsightsTemplater.cs:154](src/SpecScribe/GitInsightsTemplater.cs:154) (Story 10.6 AC2b). Reuse that exact condition for AC #2 — do not compute a separate "is this a solo repo" check.

**Story 10.2 chart-metadata standard:** every framed chart/section carries `Charts.ChartMeta` (Title/Window/Ranking/Why/Note) via `Charts.Framed` or the `FrameWindowSlot`/`FrameRankingSlot`/`FrameWhySlot` helpers — see how `AppendFilesAndContributorsSection` uses `Charts.FrameWhySlot(Charts.WhyText(Charts.ChartMetric.FileChurn))` ([GitInsightsTemplater.cs:97](src/SpecScribe/GitInsightsTemplater.cs:97)). This story needs its own `ChartMetric` case (`AuthorConcentration`) since none of the existing three (`ActivityCadence`/`FileChurn`/`ChangeCoupling`) fit "author concentration" framing.

**Story 7.2 code-page link seam:** the guarded-link discipline is already fully established in this file — `GuardedLink` ([GitInsightsTemplater.cs:275](src/SpecScribe/GitInsightsTemplater.cs:275)) and the `fileHref?.Invoke(file.Path)` pattern used at [GitInsightsTemplater.cs:131-134](src/SpecScribe/GitInsightsTemplater.cs:131) and [GitInsightsTemplater.cs:232-236](src/SpecScribe/GitInsightsTemplater.cs:232). Copy this pattern exactly: resolver returns a target → real `<a>`; no resolver/no target → plain escaped text. Never emit a dead link.

**NFR8 degrade:** `git-insights.html` is only generated when `--deep-git` produced data (existing gate at the `SiteGenerator.cs:1911` call site's enclosing `if`). This story adds a section to an already-gated page — no new gating decision needed for the shallow/non-git case (Task 3 is a regression check, not new logic). The only new degrade case this story introduces is the solo-maintainer reframe (AC #2), which is a rendering branch, not an omission.

**FR31 (generation-time determinism):** because everything here derives from the already-fetched, already-sorted `GitInsightsData` (itself built once per generation run from git log output), the classification is deterministic by construction — no wall-clock "now", no per-visitor state. Nothing extra is required to satisfy FR31 beyond not introducing any of those.

### Previous Story Intelligence (Story 7.8, most recent completed Epic 7 story)

Story 7.8 (`7-8-related-files-in-the-reference-graph.md`, done) is on a different surface (the code-page reference graph, not the Git Insights hub) but its review carried two generally-applicable lessons:
- A rendering-only, no-new-git-call story like this one should NOT move the golden fingerprint's HTML/CSS shape unpredictably — regenerate it deliberately at the end (Task 5.5) and verify the diff is scoped to the new section, not a stray reformat.
- Keep new visual affordances (chips/badges/pills) distinguished by more than color alone (shape/text), consistent with this project's a11y convention — the "Sole contributor:" bus-factor flag should carry text, not just a colored dot.

### Git Intelligence (recent commits)

Recent Epic 7/10 work (`d274cee`, `1edc996`, `f0f30bd`) has been small, surgical diffs to individual templater files plus paired golden-fingerprint regeneration and targeted `*Tests.cs` additions — no large refactors. Follow the same shape: touch `Charts.cs` + `GitInsightsTemplater.cs` + their test files + the golden fixture, nothing else.

### Project Structure Notes

- No new files expected. Modified: [src/SpecScribe/Charts.cs](src/SpecScribe/Charts.cs) (new `ChartMetric` case + `WhyText`), [src/SpecScribe/GitInsightsTemplater.cs](src/SpecScribe/GitInsightsTemplater.cs) (new section), possibly `src/SpecScribe/wwwroot`/stylesheet source for new CSS classes (check where the `.gi-*` CSS currently lives — likely one shared stylesheet source file, not `GitInsightsTemplater.cs` itself).
- Tests: [tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs](tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs) (primary), the `StylesheetTests` file covering `.gi-*`/chart classes, and the `SiteGeneratorAdapterTests.cs` golden-fingerprint assertion.
- No conflicts with the concurrent Stories 7.10 (risk quadrant) and 7.12 (freshness map) — those are separate epics-note-grouped stories extending the same `GitMetrics` deep-git path but rendering elsewhere/differently; nothing in this story's file list should overlap theirs. If either lands first and touches `Charts.ChartMetric` or `GitInsightsTemplater.cs`, re-check for enum/section-ordering conflicts before finalizing this diff.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.11: Code Ownership & Bus-Factor Insights] (lines ~1491-1509)
- [Source: _bmad-output/planning-artifacts/epics.md#Stories 7.10–7.12 grouping note] (lines ~1466-1469) — shared constraints: extend `GitMetrics.TryComputeDeep`/`ParseNumstatLog`, reuse Story 7.2 code-page link seam + Story 10.2 chart-metadata standard, degrade on shallow/non-git/solo repos (NFR8)
- [Source: src/SpecScribe/GitMetrics.cs#FileChangeStat, GitInsightsData, BuildInsights] (lines 120-171, 660-738)
- [Source: src/SpecScribe/GitInsightsTemplater.cs] — existing hub page, sole-contributor vocabulary (line 208), solo-repo softening precedent (line 154), guarded-link pattern (line 275)
- [Source: src/SpecScribe/Charts.cs#ChartMetric, ChartMeta, WhyText, Framed] (lines 13-92)
- [Source: src/SpecScribe/SiteGenerator.cs#GitInsightsTemplater.RenderPage call site] (line 1911) — confirms `fileHref: CodeItemHref` already wired
- [Source: tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs] — existing fixture/assertion conventions to extend

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
