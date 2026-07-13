---
title: 'Honest, Consistent, Navigable Portal Dates (7.3 fix + 10.4)'
type: 'feature'
created: '2026-07-13'
status: 'done'
baseline_commit: '094a73d191888fb86ba1307b151230a82a9046dc'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/7-3-activity-timeline-and-date-pages.md'
  - '{project-root}/_bmad-output/implementation-artifacts/10-4-consistent-dates-and-event-sequencing.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two related date defects. (1) **Bug:** the activity timeline / date pages attribute "N artifacts updated" from filesystem mtime (`File.GetLastWriteTime`), which collapses to the checkout/generation day — so the most-recent date falsely claims nearly every artifact changed then (screenshot: "Mon, Jul 13, 2026 — 1 commit · 112 artifacts updated"). (2) **UX / consistency (Story 10.4):** dates are rendered ~9 different ways (footer 12-hour vs Git-Pulse 24-hour vs heatmap abbreviations vs bare ISO), two unlabeled "local" clocks coexist, ADR listings show no dates, same-day change-log events have no order cue, and dates that sit "in the context of a change" are bare unlinked text you cannot click through to the day they describe.

**Approach:** Attribute artifacts to the **actual git commit day** they changed (from the existing `--deep-git` per-file `DeepGitPulse.Commits` data; drop the claim entirely when git can't verify it — never mtime). Introduce a single `PortalDates` formatter as the sole source of every human date/clock string, route every surface through it with **one date token, one 24-hour time token, and a coherent labeled-timezone treatment**, add dates + one-line summaries to ADR listings and ordinal sequence markers to same-day change-log runs, and make activity/change-context dates **link to their date page** (`commits/{iso}.html`) guarded on that page existing. This pass **executes Story 10.4** (its standalone story is superseded) and fixes the 7.3 artifact-day bug together, since both are the single goal "trustworthy, navigable, consistent dates."

## Boundaries & Constraints

**Always:**
- Artifact-change days come **only** from `DeepGitPulse.Commits` (commit `Timestamp` → day; `Files` intersected with recognized artifacts). When `DeepGit` is null, emit **no** artifact-updated signal — never fabricate from `File.GetLastWriteTime`.
- **One formatter:** after this story `PortalDates` is the only place in `src/` that formats a human date or clock string. Machine tokens are the sole exceptions: ISO hrefs/filenames (`commits/2026-07-04.html`) and the git parse format `--date=format:%Y-%m-%dT%H:%M`. A reviewer greps `src/` for stray `ToString("…[yMd]…")`.
- **Determinism:** all formatting pure + `InvariantCulture`, byte-identical across machines/locales/cultures (same discipline `GitMetrics` documents for non-Gregorian calendars). `DateOnly` values stay zone-free calendar dates.
- **Timezone honesty:** git commit times stay in the commit's **authored offset** (`--date=format:`, never `format-local:` or UTC); the generation clock stays machine-local; **each clock is labeled** so a reader can tell the generation clock from the commit clock and knows each one's zone.
- **One 24-hour time-of-day token** (`HH:mm`) so footer and Git Pulse stop disagreeing.
- **Guarded date links:** a date links to `commits/{iso}.html` **only** when that day is in the generated date-page set (the union day set the generator already computes); otherwise plain formatted text. Never a dead link.
- **Widen the golden `FooterClock` normalization regex BEFORE regenerating the fingerprint constant** (else the volatile clock leaks into the hash → machine-dependent flake).
- **Degrade to absent/as-is (NFR8):** no ADR date/summary → card shows title (+status) only, no empty line; unrecognized change-log shape → render exactly as today (never reorder/drop); unparseable authored date → left verbatim.
- Neutral tokens only (never `--status-*` for dates/sequence markers); no color-only signals; no information-bearing JavaScript; HTML-escape every derived string; webview + SPA render byte-identically (shared page HTML + view models).

**Ask First:**
- The one `DayFormat` token (recommend `"MMM d, yyyy"` → "Jul 9, 2026") and the timezone-label shape (recommend: footer labeled via `TimeZoneInfo.Local`; git times **captioned once** near where they appear — "times shown in each commit's local zone" — rather than a per-row offset suffix). These are Story 10.4's owner review checkpoints — confirm at CHECKPOINT before mass-wiring call sites.
- The ADR summary source (recommend first `## Context` paragraph, collapsed to one line) and the change-log sequence-marker styling (recommend a compact "(k of N)" cue).

**Never:**
- **No new git call and no `--numstat` beyond the existing shared deep fetch** — the artifact-day fix consumes the SAME `DeepGitPulse.Commits`; baseline-without-`--deep-git` simply omits the artifact signal (timeline/date pages still render from the baseline commit day set).
- No `--date=format-local:`, no conversion of any time to UTC/viewer-local, no reformatting of machine tokens (heatmap href/filename ISO, git parse format).
- No re-sorting or dropping of change-log content (markers annotate existing order only).
- No new "changed since last visit" recency markers; no nav grouping (10.1), chart-metadata (10.2), glossary (10.3), or doc-legibility (10.5) work; no new authoring schema (derive from what artifacts already contain); no dangling top-nav Timeline entry.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Accurate artifact day | `--deep-git` on; artifact X in commits on Jul 6 and Jul 9 | Date pages `commits/2026-07-06.html` and `…-07-09.html` each list X under "Artifacts updated"; timeline rows show the true per-day count | N/A |
| Deep-git off, git present | `GitPulse` present, `DeepGit` null | Timeline + date pages render from commit days; **no** "Artifacts updated" section, no "M artifacts updated" in summaries | N/A |
| No git at all | non-git dir, `Git`/`DeepGit` null | **No** `timeline.html`, no date pages, no dashboard link (was previously a fabricated mtime timeline) | Non-fatal; rest of site generates |
| Non-artifact file changed | git commit touched `src/Foo.cs` (not in `_referenceMap`) | Not counted as an "artifact updated" (only reference-map artifacts count) | Skipped silently |
| Date on an active day | "last commit" headline / Git-Pulse date whose day has a date page | Rendered via `PortalDates` **and** linked to `commits/{iso}.html` | N/A |
| Freshness date (mtime, not git-verified) | coverage card "Updated {day}" (`ArtifactCoverage.LastModified`) | Rendered via `PortalDates`, **always plain text** (no link, any day) — mtime isn't the git-verified day a `commits/{iso}.html` page describes | N/A |
| ADR with date+context | body has `**Date:** 2026-07-10` + `## Context` para | Card shows formatted date + one-line summary | N/A |
| ADR missing both | body has neither date nor context | Card shows title (+status) only; no empty line | Degrade to absent |
| Same-day change-log run | two `- 2026-07-06:` entries, one `- 2026-07-08:` | Jul-6 entries get "(1 of 2)"/"(2 of 2)" markers, dates reformatted; Jul-8 gets none; order preserved | N/A |
| Unrecognized change log | change log is a table / free prose | Rendered exactly as today; no markers, no reformat, no reorder | Degrade as-is |
| Clock format | footer + Git-Pulse both render a time | Both 24-hour `HH:mm`; footer zone-labeled; golden `FooterClock` regex widened so fingerprint stays machine-stable | N/A |
| Non-Gregorian culture | `CurrentCulture = th-TH` during a `PortalDates` call | Output byte-identical to invariant | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/PortalDates.cs` -- **NEW.** The single date/time formatter: `Day(DateOnly)`, `Day(DateTime)`, `DayWithWeekday(DateOnly)`, `IsoDay(DateOnly)`, `Timestamp(DateOnly, hhmm, zoneLabel?)`, one `DayFormat` const. Pure, `InvariantCulture`, IO-free.
- `src/SpecScribe/SiteGenerator.cs` -- `BuildArtifactsByDay` (rewrite: mtime → git-derived from `_progress.DeepGit.Commits`); `ArtifactLabel` (reuse); the union-day computation in `GenerateDatePagesInternal`/`GenerateTimelineInternal`; a new guarded `DateHref(DateOnly)` resolver over the generated date-page day set; `ExtractAdrDate`/`ExtractAdrSummary` (beside `ExtractAdrStatus` ~2108) wired into `new AdrEntry(...)` (~608).
- `src/SpecScribe/GitMetrics.cs` -- `DeepCommit.Timestamp`+`Files` are the artifact-day source; per-commit `HH:mm` display routes through `PortalDates`; **do not** touch the `--date=format:` parse contract.
- `src/SpecScribe/Charts.cs` -- `D`→`PortalDates.IsoDay` (byte-identical); `DReadable`/month-label→`PortalDates`; heatmap "last commit" headline (541) and Git-Pulse last-commit (627) route through `PortalDates` **and** wrap the date in a guarded date link. Freshness lines (846/909/912) route through `PortalDates` but deliberately **stay plain text** (no link, corrected post-review 2026-07-13): that date is filesystem mtime (`ArtifactCoverage.LastModified`, pre-existing Story 3.3 signal), not the git-verified day the date-page set is built from, so linking it to `commits/{iso}.html` would overstate what the linked day actually explains.
- `src/SpecScribe/PathUtil.cs` -- `RenderFooter` clock (111): 12h → `PortalDates` 24h + `TimeZoneInfo.Local` label; preserve "formatted once here" + `relativePrefix` math.
- `src/SpecScribe/CommitDayTemplater.cs` -- commit times + the day heading via `PortalDates`; prev/next date labels link via `DateHref`.
- `src/SpecScribe/AdrModel.cs` -- `AdrEntry` gains `DateOnly? Date`, `string? Summary`.
- `src/SpecScribe/DashboardViewBuilder.cs` / `HtmlRenderAdapter.Dashboard.cs` -- `CardMeta` date normalization; `BuildAdrCard` + Adr branch of `AppendIndexCard` render date (via `PortalDates.Day`) + summary, reusing muted `<p>`/`.index-card-path` grammar.
- `src/SpecScribe/RetroParser.cs` / `RetroTemplater.cs` -- retro `DateText` normalized through `PortalDates` when parseable; free text left verbatim.
- `src/SpecScribe/EpicsParser.cs` -- change-log carve (116-138): tolerant same-day sequence pass + `PortalDates.Day` reformat inside the opaque `ChangeLogHtml` fragment (view-model shape unchanged).
- `src/SpecScribe/assets/specscribe.css` -- sequence-marker + ADR meta + linked-date styles, neutral tokens.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- widen `FooterClock` regex (255) **then** regenerate `GoldenContentFingerprint` (247, currently `2289fa09…`); `GoldenOutputInventory` loses `timeline.html` + the fixture date page (non-git fixture no longer emits them).
- `tests/SpecScribe.Tests/{ActivityModelTests,SiteGeneratorTimelineTests,SiteGeneratorCommitDetailsTests}.cs` -- update for git-derived artifact days (no more mtime artifact-only day).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/PortalDates.cs` -- added the single formatter (`Day`/`DayWithWeekday`/`IsoDay`/`MonthShort`/`TimeOfDay`/`Timestamp`/`ReformatAuthored`/`LocalZoneLabel` + `DayFormat`/`TimeFormat` consts); pure + invariant.
- [x] `src/SpecScribe/SiteGenerator.cs` (BuildArtifactsByDay) -- rewritten to derive `(day, label, href)` from `_progress.DeepGit.Commits` with the repo↔source path reconciliation; `File.GetLastWriteTime` path deleted; empty without deep-git. **Bug fixed** (verified: today went from a fabricated "112 artifacts" to a real 7).
- [x] Date-link guarding -- implemented as **direct guarded links at the two visible change-context sites** (heatmap headline + Git-Pulse last-commit) rather than a threaded generator-wide `DateHref` resolver: both dates are definitionally linked commit days, so a local `linkedSet.Contains`/`day <= today` guard is correct and simpler. Timeline/prev-next already linked. (Deviation from the planned resolver; same AC — never a dead link.)
- [x] `src/SpecScribe/{PathUtil,Charts,GitMetrics,CommitDayTemplater,DashboardViewBuilder,RetroParser?,RetroTemplater}.cs` -- every human date/clock routes through `PortalDates`; change-context dates linked where the render context is anchor-capable; grep-guard confirmed clean (no human-date `ToString` outside `PortalDates`). (`RetroParser` untouched — retro dates normalize at the templater/card display sites.)
- [x] `src/SpecScribe/PathUtil.cs` + `tests/…/SiteGeneratorAdapterTests.cs` -- footer → 24h + `PortalDates.LocalZoneLabel`; `FooterClock` regex widened first, fingerprint regenerated (`17f693b8…`), diff eyeballed.
- [x] `src/SpecScribe/{AdrModel,SiteGenerator,DashboardViewBuilder,HtmlRenderAdapter.Dashboard}.cs` -- `AdrEntry.Date/Summary` + tolerant `ExtractAdrDate`/`ExtractAdrSummary` (Context-first-paragraph, H1-tail fallback), rendered on the card from the one extraction; null-safe.
- [x] `src/SpecScribe/EpicsParser.cs` -- tolerant same-day change-log sequence markers (`SequenceChangeLog`, "(k of N)") + `PortalDates.Day` reformat inside `ChangeLogHtml`; degrades table/prose to as-is; never reorders/drops. (Verified: story 1.2's two Jul 6 entries → "1 of 2"/"2 of 2".)
- [x] `src/SpecScribe/assets/specscribe.css` -- `.index-card-meta`/`.index-card-summary`/`.date-link`/`.git-pulse-zone-note`, neutral tokens.
- [x] `tests/SpecScribe.Tests/*` -- added `PortalDatesTests` (incl. th-TH/fa-IR invariance), `ChangeLogSequencingTests`, ADR date/summary + superseded/deprecated tests, `CommitHeatmap`/`GitPulsePanel` link tests, git-derived-artifact-day test; updated timeline/commit-details/golden/footer-normalizer tests. **994 pass, 0 fail.**

**Acceptance Criteria:**
- Given `--deep-git`, when the portal generates, then each date page's "Artifacts updated" section and each timeline row's "M artifacts updated" reflect only the artifacts that actually changed in that day's commits (no all-artifacts-on-today collapse).
- Given no deep-git data, when the portal generates, then no artifact-updated signal is shown anywhere (and with no git at all, no timeline/date pages are emitted) — nothing is fabricated from mtime.
- Given dates appear across the portal, when it generates, then a single `PortalDates` token formats every human date, one 24-hour token formats every clock time, and the generation clock and git-commit clock are each zone-legible and distinguishable (no stray `ToString` date format survives outside `PortalDates`).
- Given a date sits in the context of a change and its day has a date page, when rendered, then it is standard-formatted and links to `commits/{iso}.html`; when its day has no date page, it is standard-formatted plain text (never a dead link).
- Given the ADR index, when it generates, then cards carry dates and one-line summaries sourced from the ADR bodies (absent when the body has neither), and superseded/deprecated statuses render distinctly from Accepted on both the ADR page and the card.
- Given a change-log run sharing a date, when rendered, then ordinal sequence markers order the run (unique dates unmarked; unrecognized shapes unchanged and never reordered).
- Given two generation runs over identical input, when compared, then output is byte-identical (determinism), and the golden fingerprint was regenerated deliberately after widening the `FooterClock` normalization.

### Review Findings

- [x] [Review][Patch] (resolved decision — drop the ambiguous format) Removed `M/d/yyyy` from `PortalDates.AuthoredDayFormats` — ISO + named-month formats already cover legitimate authored dates unambiguously; a day-first slash date now degrades to verbatim instead of silently misparsing. [src/SpecScribe/PortalDates.cs:67]
- [x] [Review][Patch] (resolved decision — leave unlinked, fix the spec) Corrected the Code Map bullet: freshness lines (846/909/912) deliberately stay plain text (filesystem mtime, not the git-verified date-page day); updated the I/O & Edge-Case Matrix row to match. [Code Map + matrix sections of this spec]
- [x] [Review][Patch] `GitPulsePanel`'s date-link guard now checks actual `LinkedCommitDays` membership (the same set the heatmap uses) instead of a bare `day <= today` comparison. [src/SpecScribe/Charts.cs:643]
- [x] [Review][Patch] `SequenceChangeLog`'s `HasInterveningBullet` now only counts a bullet as intervening when its indentation is ≤ the item bullets' own indentation, so a nested sub-bullet no longer breaks a legitimate same-day run. [src/SpecScribe/EpicsParser.cs:352,357]
- [x] [Review][Patch] `BuildArtifactsByDay`'s `repoRelToArtifact` now uses `TryAdd` (first-write-wins, explicit) instead of an overwriting indexer on repo-relative path collisions. [src/SpecScribe/SiteGenerator.cs:860]
- [x] [Review][Patch] `ExtractAdrDate`'s `## Date` heading fallback now keeps scanning lines within the section (stopping only at the next heading) instead of breaking unconditionally after the first line. [src/SpecScribe/SiteGenerator.cs:2649-2660]
- [x] [Review][Patch] `ExtractAdrSummary`'s H1-tail fallback now splits on the LAST dash occurrence (both em/en-dash and spaced-hyphen variants) instead of the first, so an earlier unrelated dash (e.g. a numeric range) no longer garbles the summary. [src/SpecScribe/SiteGenerator.cs:2710,2723]
- [x] [Review][Patch] (no code change — reviewed and accepted as a documented trade-off) `CollapseSummary`'s numbered-prefix stripper has no reliable heuristic to distinguish a real list marker from a sentence that happens to start with "N. " without full markdown context; any fix would trade one false positive for another, so left as-is. [src/SpecScribe/SiteGenerator.cs:2731]

**Verification:** `dotnet build` clean; `dotnet test` 997/997 green (golden `GoldenContentFingerprint` regenerated deliberately — see `SiteGeneratorAdapterTests.cs:252-259` — and eyeballed via a real `--deep-git` generate: freshness dates confirmed plain text, GitPulse last-commit link confirmed working, ADR 0006's dash-in-title summary confirmed correctly resolved via the last-dash fix).

## Spec Change Log

**2026-07-13 — Adversarial review (Blind Hunter + Edge Case Hunter, independent subagents).** No `intent_gap` / `bad_spec` (no loopback). **Patches applied** (10): (1) migrated `CommitDetailTemplater`'s per-commit `ToString("HH:mm")` to `PortalDates.TimeOfDay` — the one portal site my `[yMd]` grep-guard missed (time-only letters); documented `ConsoleUi`'s console clock as the sole out-of-scope exception; (2) restored per-entry AD-4 try/catch in `BuildArtifactsByDay` (one malformed path no longer discards the whole artifact signal); (3) unified date-parse tolerance behind a single `PortalDates.TryParseDay` (ADR and retro/doc paths can't disagree; removed the divergent `AdrDateFormats`); (4) `TryParseAdrDate` now strips a trailing `(...)`/`;` note instead of space-splitting, so multi-word dates with trailing prose still parse; (5) future-skewed commits are **skipped** (not clamped to today) in `BuildArtifactsByDay`, matching `LinkedCommitDays` so an artifact is never attributed to a date page whose commit list omits that commit; (6) `SequenceChangeLog` run detection now requires source-adjacency (an intervening bullet with a different/unparseable date breaks a same-day run — no false "(k of N)"); (7) `CollapseSummary` strips leading list/quote/table/image markers; (8) surrogate-pair-safe truncation; (9) H1-tail summary fallback accepts en-dash + spaced hyphen, not just em-dash; (10) corrected a misleading "always linked" comment on the heatmap headline guard. **Deferred** (2, → deferred-work.md): OrdinalIgnoreCase path map on case-sensitive filesystems; rename-follow under-attribution. **Rejected** (2): change-log heading exact-match (consistent with the pre-existing carve); per-context weekday styling difference (both route through `PortalDates`). 997 tests green; golden byte-parity held (patches were edge-case-only or byte-identical); real generation re-verified.

## Design Notes

**Git-derived attribution collapses the union (important):** because an artifact only changes *inside a commit*, git-derived artifact days are always ⊆ commit days. So the 7.3 "artifact-only day" case disappears and the date-page day set is effectively the commit-day set: date pages continue to come from `LinkedCommitDays` (baseline `GitPulse`, no deep-git needed), and `--deep-git` only enriches each existing date page/timeline row with an accurate "Artifacts updated" detail. Keep `ActivityModel.UnionDays` for robustness, but expect `artifactsByDay.Keys ⊆ commitDays`. The **pure non-git** path now honestly emits nothing (this intentionally reverses 7.3's mtime-driven `timeline.html` + date page in the non-git golden fixture — the owner chose "drop the claim when git can't verify it").

**Repo-relative ↔ source-relative reconciliation is the correctness crux of the bug fix.** `_referenceMap` keys are `SourceRoot`-relative; `DeepFileChange.Path` is `RepoRoot`-relative and they can differ (SpecScribe may run on a subdir). Round-trip via full paths (`Combine(RepoRoot, gitPath)` → `GetRelativePath(SourceRoot, …)` → `NormalizeSlashes`) exactly as the code-page discovery (`DiscoverCodeReferences`, repoFull/sourceFull) and ADR extraction already reconcile roots. A file outside `SourceRoot` or not in `_referenceMap` simply isn't an "artifact updated."

**`PortalDates` stays href-agnostic.** It returns strings only; the guarded `<a href>` wrapping lives at the render sites using the generator's `DateHref` resolver (mirrors `CommitHref`). This keeps the formatter pure/IO-free and testable, and keeps link-existence logic with the generator that owns the day set.

**Timezone policy (owner review checkpoint — Story 10.4):** keep git times in the commit's authored offset and the footer in machine-local, but label each so "generated … 17:14 (−04:00)" and captioned commit-local times are self-describing. Do **not** switch to `format-local:`/UTC (breaks cross-machine determinism the golden fingerprint pins, and misrepresents author-local commit times).

## Verification

**Commands:**
- `dotnet test` -- expected: all green; new `PortalDatesTests` (incl. th-TH invariance), ADR/change-log/ADR-state/git-derived-artifact/`DateHref` tests pass; `GoldenContentFingerprint` matches the deliberately-regenerated constant.
- `dotnet run --project src/SpecScribe -- --deep-git` (default `SpecScribeOutput/`; never `--output docs/live`) -- expected: a real generate to inspect.

**Manual checks:**
- `SpecScribeOutput/timeline.html` + a `commits/{date}.html`: "M artifacts updated" reflects only that day's real commit changes (today no longer shows ~all artifacts); each date is one standard format and links through to its page.
- `index.html`: footer + Git-Pulse clocks share one 24-hour zone-legible format; the two clocks are distinguishable; ADR cards show date + one-line summary; a story page with two same-date change-log entries shows ordinal markers.
- Grep `src/` for `ToString("` with date/time format letters → only `PortalDates` (and machine-token exceptions) remain.
- `specscribe webview` and `--spa` render the same date output (shared HTML + view models).

## Suggested Review Order

**The bug fix — git-derived artifact days (start here)**

- Entry point: artifacts attributed to the day git actually changed them, replacing the mtime collapse; per-entry AD-4 guards, future-skew skip, root reconciliation.
  [`SiteGenerator.cs:842`](../../src/SpecScribe/SiteGenerator.cs#L842)
- The pure grouping/union this feeds (unchanged; artifact days are now ⊆ commit days).
  [`ActivityModel.cs:8`](../../src/SpecScribe/ActivityModel.cs#L8)

**The single date/time formatter (AC1 seam)**

- Every human date/clock routes through here; one token, 24h, zone label, shared tolerant parse.
  [`PortalDates.cs:72`](../../src/SpecScribe/PortalDates.cs#L72)
- Footer clock → 24h + machine-local zone label (the golden `FooterClock` regex was widened for this).
  [`PathUtil.cs:128`](../../src/SpecScribe/PathUtil.cs#L128)
- `D`/`DReadable` delegate to `PortalDates`; heatmap "last commit" headline becomes a guarded date link.
  [`Charts.cs:545`](../../src/SpecScribe/Charts.cs#L545)
- Git-Pulse last-commit: `PortalDates.Timestamp`, guarded date link, one zone caption.
  [`Charts.cs:643`](../../src/SpecScribe/Charts.cs#L643)
- Per-commit times single-sourced (byte-identical): day page + detail page.
  [`GitMetrics.cs:256`](../../src/SpecScribe/GitMetrics.cs#L256)

**ADR listing dates + one-line summaries**

- Tolerant date extract (bold line / `## Date` / frontmatter; trailing-prose safe, shared parser).
  [`SiteGenerator.cs:2641`](../../src/SpecScribe/SiteGenerator.cs#L2641)
- Summary from the first `## Context` paragraph (marker-/surrogate-safe), H1-tail fallback.
  [`SiteGenerator.cs:2686`](../../src/SpecScribe/SiteGenerator.cs#L2686)
- Model + card render of date/summary (parity-safe via the shared `IndexCardView`).
  [`AdrModel.cs:20`](../../src/SpecScribe/AdrModel.cs#L20)
  [`HtmlRenderAdapter.Dashboard.cs:398`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L398)

**Same-day change-log sequencing**

- Tolerant "(k of N)" run detection with source-adjacency guard; degrades tables/prose to as-is.
  [`EpicsParser.cs:299`](../../src/SpecScribe/EpicsParser.cs#L299)

**Peripherals (styling + tests)**

- Neutral-token styles for `.date-link` / `.git-pulse-zone-note` / ADR meta+summary.
  [`specscribe.css`](../../src/SpecScribe/assets/specscribe.css)
- New/updated tests: formatter, sequencing, ADR extraction, git-derived attribution, golden regen.
  [`PortalDatesTests.cs`](../../tests/SpecScribe.Tests/PortalDatesTests.cs)
  [`SiteGeneratorTimelineTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorTimelineTests.cs)
