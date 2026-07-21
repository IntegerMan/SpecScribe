---
title: 'Story 7.3 deferred-debt cleanup'
type: 'bugfix'
created: '2026-07-21T09:00:00-04:00'
status: 'in-review'
review_loop_iteration: 0
context: []
baseline_commit: '50c5185'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 7.3's code review left four open items in `deferred-work.md`: (1) watch-mode `GenerateOne`/`RegenerateEpics` never recompute the date-page/timeline artifact signal, only a full generate does; (2) `ArtifactLabel` silently swallows a read failure into a bare filename stem with no diagnostic; (3) two artifacts sharing a generic title (e.g. two docs both titled "Overview") render as visually indistinguishable adjacent list entries on a date page; (4) the unbounded `GitPulse.CommitsByDay` used for date pages can reference commits outside the 300-page-capped `_commitPages` window, so old-day hashes can never resolve to a commit detail link.

**Approach:** Extract the existing date-pages+timeline block into one shared `RefreshDatePagesAndTimeline` helper and call it from `GenerateOne` and `RegenerateEpics` (both branches) the same way `RefreshFollowUpSurfaces` already is, so watch-mode edits refresh the signal without a full regenerate. Give `ArtifactLabel` a read-failure diagnostic on the existing `GenerationEvent` channel. Render each date page's artifact-update `<li>` with a muted secondary line showing its href (already unique per artifact) so same-titled entries are visually distinguishable. Close item 4 as an accepted, already-safely-degrading design tradeoff (no code change) — widening the 300-commit-page cap to match the unbounded base pulse conflicts with Story 7.5's deliberate page-count bound. Mark all four items RESOLVED in `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- New `private void RefreshDatePagesAndTimeline(SiteNav nav, List<GenerationEvent> events, IGenerationReporter? reporter = null)` on `SiteGenerator` wraps exactly the existing `_timelinePath = null` / `BuildArtifactsByDay()` / `GenerateDatePagesInternal` / `GenerateTimelineInternal` block; `GenerateAll` calls it in place of the inline block (byte-identical behavior there).
- `GenerateOne` calls `RefreshDatePagesAndTimeline(nav, new List<GenerationEvent>())` after `RefreshFollowUpSurfaces`, before `WriteIndex`.
- `RegenerateEpics` calls it (passing its own `epicsEvents`/`skippedInventory`-adjacent local list) in BOTH the "epics file not found" skip branch and the normal success branch, before each branch's `WriteIndex`.
- `BuildArtifactsByDay` gains an optional `List<GenerationEvent>? events = null` parameter, threaded into `ArtifactLabel`. `ArtifactLabel` catches `IOException`/`UnauthorizedAccessException` specifically (not bare `Exception`) and, when `events` is non-null, appends one `GenerationEvent(GenerationOutcome.Skipped, sourceRelative, TimeSpan.Zero, "artifact title read failed, using filename: {message}")` before falling back to the stem.
- `CommitDayTemplater.RenderPage`'s artifact-update `<li>` gains a second, muted line rendering the artifact's `href` (already-escaped, already unique) beneath the label link — pure markup/CSS, no model change.
- New `.artifact-update-path` CSS rule beside the existing `.artifact-update-list` block, using `var(--ink-light)` (same muted token the section heading already uses).
- Strike through all four story-7-3 deferred entries with resolution notes; item 4 notes the accepted-tradeoff rationale, no code pointer.

**Ask First:** none — decisions above close all four items as written.

**Never:**
- Re-run git (`GitMetrics.TryCompute`/`TryComputeDeep`) from the new helper or from `GenerateOne` — it must only reuse already-cached `_progress` data.
- Raise `_commitPages`'s 300-commit cap or change `GitPulse.CommitsByDay`'s fetch window.
- Change `ActivityModel.GroupArtifactsByDay`'s dedup key or grouping/order rules.
- Add a second git call or new source of per-artifact metadata for the disambiguation line.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Watch-mode doc edit | `GenerateOne` on a tracked doc after `--deep-git` full generate | Timeline/date pages reflect the edit without a full regenerate | N/A |
| Watch-mode epics edit | `RegenerateEpics` success or skip branch | Date pages/timeline refreshed in both branches | N/A |
| Unreadable artifact | `ExtractArtifactTitle` throws `IOException`/`UnauthorizedAccessException` | Falls back to filename stem AND one Skipped event recorded | still never throws (AD-4) |
| Two same-titled artifacts, same day | Two docs titled "Overview", distinct hrefs | Both `<li>`s show "Overview" plus a distinct muted href line | N/A |
| Old commit beyond 300-page window | Date page references a hash outside `_commitPages` | Plain `<code>` hash (unchanged, already-safe degrade) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` — `GenerateAll`'s inline date-pages/timeline block (~309–319) → extracted `RefreshDatePagesAndTimeline`; call sites in `GenerateOne` (~414–428) and `RegenerateEpics` (~527–539, ~568–575); `BuildArtifactsByDay` (~1017) + `ArtifactLabel` (~1106) event threading
- `src/SpecScribe/CommitDayTemplater.cs` — artifact-update `<li>` rendering (~102–113)
- `src/SpecScribe/assets/specscribe.css` — `.artifact-update-list` block (~4083–4091)
- `tests/SpecScribe.Tests/SiteGeneratorDatePagesTests.cs` (or nearest existing date-page test file) — watch-mode refresh + label-failure-diagnostic pins
- `tests/SpecScribe.Tests/CommitDayTemplaterTests.cs` — disambiguation-line render pin
- `_bmad-output/implementation-artifacts/deferred-work.md` — story-7-3 section (~239–252)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- extract `RefreshDatePagesAndTimeline`; wire into `GenerateAll`, `GenerateOne`, both `RegenerateEpics` branches; add `events` param to `BuildArtifactsByDay`/`ArtifactLabel` with the Skipped-on-read-failure event
- [x] `src/SpecScribe/CommitDayTemplater.cs` -- render the muted href line under each artifact-update label
- [x] `src/SpecScribe/assets/specscribe.css` -- add `.artifact-update-path` styled with `var(--ink-light)`
- [x] `tests/SpecScribe.Tests/*` -- pin: `GenerateOne`/`RegenerateEpics` refresh the timeline/date-page artifact signal without a full `GenerateAll`; an unreadable artifact falls back to the stem and self-heals; the date page renders both label and href for a same-titled pair; `CommitDayTemplaterTests` updated for the new markup
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark all four story-7-3 items RESOLVED citing this spec's id (item 4 as an accepted tradeoff, no code pointer)

**Acceptance Criteria:**
- Given `--deep-git` is on and a full generate has run, when a tracked doc is edited in watch mode (`GenerateOne`), then its date page and the timeline reflect the change without a full regenerate.
- Given the same setup, when an epics-related file changes (`RegenerateEpics`, either branch), then the date pages/timeline are also refreshed.
- Given an artifact whose file read throws `IOException` or `UnauthorizedAccessException`, when its label is resolved, then the fallback stem is used AND one Skipped `GenerationEvent` is recorded.
- Given two artifacts sharing a label on the same day, when their date page renders, then each list entry shows its own href beneath the label so they're visually distinguishable.
- Given the four deferred entries, when this ships, then each is struck through with a resolution note.

## Design Notes

**Why a shared helper, not per-caller duplication:** `RefreshFollowUpSurfaces` already established this shape (one private method called from `GenerateAll`/`GenerateOne`/`RegenerateEpics`) for the analogous follow-up-surface staleness bug; reusing it keeps the fix consistent with the codebase's existing watch-mode-refresh pattern.

**Why item 4 gets no code change:** `_commitPages` is deliberately capped at the same ≤300-commit window as `GenerateCommitDetailsInternal`'s deep-git fetch (Story 7.5's page-count bound, `SiteGenerator.cs:1173`). `GitPulse.CommitsByDay` is intentionally unbounded (full history) so old date pages still exist and read correctly — they just can't link an old hash to a detail page, and already fall back to plain `<code>` text, which is the documented, non-broken degrade path. Widening the cap to eliminate the gap would reopen the exact page-count/perf risk 7.5 closed.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~DatePage|FullyQualifiedName~CommitDay|FullyQualifiedName~ActivityModel|FullyQualifiedName~GenerateOne|FullyQualifiedName~RegenerateEpics"` -- expected: all matching tests pass
- `dotnet test` -- expected: full suite green

## Suggested Review Order

**Shared watch-mode refresh helper**

- `RefreshDatePagesAndTimeline` extracted from `GenerateAll`'s inline block; behavior there unchanged.
  [`SiteGenerator.cs:309`](../../src/SpecScribe/SiteGenerator.cs#L309)

- `GenerateOne` calls the helper after `RefreshFollowUpSurfaces`.
  [`SiteGenerator.cs:414`](../../src/SpecScribe/SiteGenerator.cs#L414)

- `RegenerateEpics` calls the helper in both its skip and success branches.
  [`SiteGenerator.cs:509`](../../src/SpecScribe/SiteGenerator.cs#L509)

**Artifact-label read-failure diagnostic**

- `BuildArtifactsByDay`/`ArtifactLabel` thread an optional events sink; specific IO/permission catch records one Skipped event.
  [`SiteGenerator.cs:1017`](../../src/SpecScribe/SiteGenerator.cs#L1017)

**Same-title disambiguation**

- Date-page artifact-update list renders a muted href line under each label.
  [`CommitDayTemplater.cs:102`](../../src/SpecScribe/CommitDayTemplater.cs#L102)

- `.artifact-update-path` styled with the existing muted ink token.
  [`specscribe.css:4091`](../../src/SpecScribe/assets/specscribe.css#L4091)

**Tracking + tests**

- Four story-7-3 deferrals struck through; item 4 closed as an accepted tradeoff.
  [`deferred-work.md:241`](./deferred-work.md#L241)
