---
title: 'Change Log entries link their leading date to the date page'
type: 'feature'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 2
context: []
baseline_commit: 12ecce126a6af041b0bca945fc3ed4e76af3589a
---

<!-- Target: 900–1300 tokens. Above 1600 = high risk of context rot.
     Never over-specify "how" — use boundaries + examples instead.
     Cohesive cross-layer stories (DB+BE+UI) stay in ONE file.
     IMPORTANT: Remove all HTML comments when filling this template. -->

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every story page's Change Log section leads each entry with a date ("- Jul 6, 2026: ..."), reformatted by `EpicsParser.SequenceChangeLog`, but rendered as plain text even when that exact day already has a generated `commits/{date}.html` page. A reader can't jump from "what happened on this day" to "what else happened that day."

**Approach:** Extend `SequenceChangeLog`/`ExtractNamedSectionHtml` to accept a guarded date→href resolver (mirroring the code page History tab's existing `dayHref` pattern) and wrap each entry's leading date in a link when a day page exists for it, plain text otherwise. Because story pages currently render before date pages in the generation pipeline (so `_commitDays` is empty at that point), add a lightweight early precompute of the day/href set from git data alone — sound because artifact-only days are already a documented subset of commit days, so the day *set* is identical whether or not `_docs` is populated yet.

## Boundaries & Constraints

**Always:** Only the leading date of a recognized `- YYYY-MM-DD: text` Change Log entry gets linked (the same shape `SequenceChangeLog` already recognizes); an unrecognized shape (table, free prose) is left exactly as today — no linkification attempted. Link only when a `commits/{date}.html` page actually exists for that exact date; otherwise render the reformatted date as plain text (today's behavior). Reuse the existing `commits/{date}.html` href convention and `PathUtil.RelativePrefix` prefixing — no new path scheme. `SequenceChangeLog`'s ordinal "(k of N)" marker logic is unchanged.

**Ask First:** Whether the early, git-only precompute of `_commitDays` (so it's populated before `RenderEpicsPages` runs) is acceptable, versus a different way to make day hrefs available at story-render time. Confirm before implementing if a simpler alternative is preferred.

**Never:** Do not reorder `GenerateDatePagesInternal`'s real (docs-dependent) run — only add a separate, minimal early lookup of dates+hrefs. Do not touch any other `EntityPager` family or the day-page pager direction. Do not extend this to ADR/requirement/plain-doc pages — only the story page's dedicated Change Log pipeline (`ExtractNamedSectionHtml` / `SequenceChangeLog`) is in scope.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | `--deep-git` on; entry dated 2026-07-06; that day's page was generated | Date renders as `<a href="…/commits/2026-07-06.html">Jul 6, 2026</a>` | N/A |
| No day page | `--deep-git` off, or gitPulse absent, or that specific date has no page | Date renders as plain reformatted text, unchanged from today | N/A |
| Same-day run | Two entries share a date that has a page | Both dates link to the same day page; "(1 of 2)"/"(2 of 2)" markers unaffected | N/A |
| Unrecognized shape | Change Log written as a table or free prose | Passed through untouched, exactly as `SequenceChangeLog` already degrades today | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsParser.cs` -- `SequenceChangeLog` (~L459) and `ExtractNamedSectionHtml` (~L406): add an optional `Func<DateOnly, string?>? dayHref` + prefix, emit a markdown link `[{PortalDates.Day(date)}]({prefix+href})` in place of the plain reformatted date when resolved. (Unchanged since loop 0/1 — KEEP.)
- `src/SpecScribe/SiteGenerator.cs` -- new guarded resolver `ChangeLogDayHref(DateOnly date)`: `Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, today).Contains(date) ? DayPageOutputPath(date) : null` when `_progress?.Git is { }`, else null. Reuses the exact function `GenerateDatePagesInternal` already calls to build its own day set (~L947) — no separate condition, no repo-relative path lookup, no deep-git branch. `BuildStoryPageFragments` (~L2069) passes it + `storyPrefix` into the Change Log's `ExtractNamedSectionHtml` call. Extract the shared `commits/{date}.html` output-path formula into one small private helper reused by both this resolver and `GenerateDatePagesInternal` (~L964/L971) so the two never drift.
- `tests/SpecScribe.Tests/ChangeLogSequencingTests.cs` -- extend with cases passing a `dayHref` resolver (linked / unlinked / same-day-run / unrecognized-shape never consults the resolver). (Unchanged since loop 0/1 — KEEP.)
- `tests/SpecScribe.Tests/SiteGeneratorChangeLogDateLinkTests.cs` -- new end-to-end coverage: a real story's Change Log date links when a git commit touched the repo on that day; stays plain text when there's no commit that day, INCLUDING a future-dated commit (the loop-2 fix).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/EpicsParser.cs` -- thread an optional `dayHref` resolver through `ExtractNamedSectionHtml`/`SequenceChangeLog`, linkifying only the recognized dated-bullet shape -- delivers the actual link
- [x] `src/SpecScribe/SiteGenerator.cs` -- add `ChangeLogDayHref` (reusing `Charts.LinkedCommitDays` directly, no bespoke condition), extract the shared `DayPageOutputPath` helper, wire both into `BuildStoryPageFragments` -- makes linking correct with zero pipeline reordering AND zero drift from what `GenerateDatePagesInternal` actually generates (same function, same "day <= today" filter, by construction)
- [x] `tests/SpecScribe.Tests/ChangeLogSequencingTests.cs` -- cover linked / unlinked / same-day-run / unrecognized-shape cases -- pins the I/O matrix
- [x] `tests/SpecScribe.Tests/SiteGeneratorChangeLogDateLinkTests.cs` -- end-to-end: git commit on the entry's date links, no git at all stays plain, git history on an unrelated day stays plain, a FUTURE-dated commit (clock skew / backdated-forward) stays plain -- the last case pins the loop-2 fix
- [x] Regenerate the golden HTML fingerprint constant(s) -- not needed: full suite (1712 tests) stayed green with no fingerprint drift; manually re-verified against this repo's own `--deep-git` generation, including the unrecognized-shape degrade case

**Acceptance Criteria:**
- Given `Charts.LinkedCommitDays` (the same function `GenerateDatePagesInternal` uses) includes a story's Change Log entry date, when the story page renders, then that entry's date is a link to `commits/{date}.html`.
- Given that date is NOT in `Charts.LinkedCommitDays`'s result (no git, no commit that day, or the date is in the future), when the story page renders, then the date renders as plain text exactly as today.
- Given a Change Log written as a table or free prose, when the story page renders, then it is unchanged — no linkification is attempted.

## Spec Change Log

- **2026-07-19, review_loop_iteration 1 (bad_spec):** Both Blind Hunter and Edge Case Hunter independently flagged that the first implementation's "precompute `_commitDays` early from git-only data" approach relied on a false invariant — `GitPulse` is bounded to 200 commits, `DeepGitPulse` to 300 ([GitMetrics.cs:441](../../src/SpecScribe/GitMetrics.cs#L441)), so artifact-only days are NOT always a subset of the shallow pulse's commit days as the original Design Notes claimed. This could have silently under-linked (or, per Edge Case Hunter, over-linked into a dead link on a day-page write failure) Change Log dates. Amended: Code Map, Tasks & Acceptance, and Design Notes to replace the early-precompute approach with a narrower, provably-safe per-artifact `ChangeLogDayHref` resolver that never produces a dead link (see Design Notes for the exact two-condition check). Known-bad state avoided: linking (or failing to link) a date inconsistently with what `GenerateDatePagesInternal` actually generates. **KEEP:** `SequenceChangeLog`'s markdown-link splice mechanics and all four `ChangeLogSequencingTests` linked/unlinked/same-day-run/unrecognized-shape cases from loop 0 carry over unchanged — only the resolver implementation and its wiring in `SiteGenerator.cs` are being re-derived. Code from loop 0 was reverted to `baseline_commit` before this re-derivation.

- **2026-07-19, review_loop_iteration 2 (bad_spec):** Both reviewers again independently converged on a real gap in the loop-1 resolver: it checked raw `_progress.Git.CommitsByDay`/`_progress.DeepGit.Commits` directly with no "date is not in the future" filter, while `GenerateDatePagesInternal` (via `Charts.LinkedCommitDays`) and `BuildArtifactsByDay` both explicitly exclude `day > today` ([SiteGenerator.cs:1061](../../src/SpecScribe/SiteGenerator.cs#L1061), [Charts.cs:2137](../../src/SpecScribe/Charts.cs#L2137)) — so a future-dated commit (clock skew, or a manually backdated-forward `--date`) touching the artifact would have produced a genuine dead link, contradicting the "never dead-link" claim. Separately, Blind Hunter traced the loop-1 Design Notes' "GitPulse bounded to 200 commits" claim to a misreading: the `git log` call that populates `CommitsByDay`/`DailySeries` ([GitMetrics.cs:275](../../src/SpecScribe/GitMetrics.cs#L275)) has NO `-n` limit at all — the `-n 200` bound belongs to a separate, unrelated `TopChangedFiles` fetch. Given `CommitsByDay` is actually full/unbounded history, it is *already* guaranteed to be a superset of anything the (300-commit-bounded) deep-git branch could see, making the loop-1 deep-git-specific condition (b) both unnecessary AND the source of duplicated future-date-guard risk. Amended: Code Map/Tasks/Design Notes to drop the deep-git branch entirely and have `ChangeLogDayHref` call `Charts.LinkedCommitDays` directly (the exact function `GenerateDatePagesInternal` itself uses) rather than re-implementing an equivalent condition — eliminating both the future-date gap and the incorrect "200 vs 300" premise in one simplification. **KEEP:** the markdown-link splice mechanics and `ChangeLogSequencingTests` from loop 0/1 still carry over unchanged. Code from loop 1 was reverted to `baseline_commit` before this re-derivation.

- **2026-07-19, review_loop_iteration 3, patch pass (both reviewers):** Both Blind Hunter and Edge Case Hunter flagged that `ChangeLogDayHref`'s doc comment overstated its guarantee ("can never drift apart") — it only consults `Charts.LinkedCommitDays` (the commit-day half of `GenerateDatePagesInternal`'s real day set), not `artifactsByDay` (the docs-derived half, unavailable at story-render time). This is the SAME residual gap already named and accepted in the Design Notes below ("only residual gap is under-linking... an accepted, documented narrower edge") — not a new dead-link bug: `LinkedCommitDays` is a provable SUBSET of the real day set (confirmed: `git.CommitsByDay` is unbounded full history per loop-1's finding, so `artifactsByDay.Keys`, sourced from the bounded ≤300-commit deep-git window, is always ⊆ it), so this can only ever under-link (safe degrade), never dead-link. Classified **patch**, not bad_spec: no revert, no re-derivation — tightened `ChangeLogDayHref`'s doc comment to state the subset guarantee precisely instead of implying full equivalence. No code behavior changed; review_loop_iteration was NOT incremented for this pass (patch findings don't trigger a loopback).

## Design Notes

**Review loop 1 finding:** the original approach — precompute `_commitDays` early from `_progress.Git` alone, reasoning that artifact-only days are "a documented subset of commit days" — was replaced with a per-artifact resolver checking two conditions directly (shallow `CommitsByDay`, or a deep-git commit touching this artifact's own path).

**Review loop 2 finding (both reviewers, independently):** that per-condition resolver still had a real gap — neither condition excluded a *future-dated* commit, while `GenerateDatePagesInternal`/`BuildArtifactsByDay` both explicitly do (`day > today` is skipped). A future-dated commit (clock skew, or a manually backdated-forward `--date`) touching the artifact would have produced a genuine dead link. Separately: the "GitPulse bounded to 200 commits" premise behind needing a second, deep-git-specific condition was itself wrong — `CommitsByDay` comes from an unbounded `git log` call ([GitMetrics.cs:275](../../src/SpecScribe/GitMetrics.cs#L275)); the `-n 200` bound belongs to an unrelated fetch.

**Corrected approach (final):** don't reimplement the day-set logic at all — call it. `ChangeLogDayHref(date)` is:
```
_progress?.Git is { } git && Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, DateOnly.FromDateTime(DateTime.Now)).Contains(date)
    ? DayPageOutputPath(date) : null
```
`Charts.LinkedCommitDays` is the exact same function `GenerateDatePagesInternal` calls to build its own day set (line ~947) — same "day <= today" filter, same source data, zero duplicated logic, zero drift possible. No repo-relative path lookup, no deep-git branch, no case-sensitivity concern, no second "today" computation to disagree with the real pass's. The only residual gap is under-linking (safe, same "plain text" degrade already allowed) when `artifactsByDay` (docs-derived, not available at story-render time) would have added a day beyond what `LinkedCommitDays` alone covers — an accepted, documented, narrower edge than either prior attempt.

**Accepted, documented residual risk:** a day-page write can still fail inside `GenerateDatePagesInternal`'s per-day try/catch after this resolver has already said "yes, link it" — an already-surfaced `GenerationOutcome.Error` event, not a new silent failure mode. Not engineered around here; flagging in case the human wants a compensating check in a follow-up.

**KEEP:** the markdown-link emission mechanics in `SequenceChangeLog` (the `[{dateText}]({hrefPrefix}{target})` splice, only on the recognized dated-bullet regex match, never touching unrecognized shapes) tested clean across both prior loops and carry over unchanged — only the *resolver* being passed in changes.

## Verification

**Commands:**
- `dotnet test` -- expected: all tests green, including the regenerated golden fingerprint(s)

**Manual checks (if no CLI):**
- Generate this repo's own site with `--deep-git`; open a story page whose Change Log entry's date has a `commits/{date}.html` page and confirm the date is a working link; confirm a date with no page still renders as plain text.

## Suggested Review Order

**The resolver (why this is safe against dead links)**

- Entry point: the guarded resolver, reused directly from the same day-set logic the real date-page generator uses — never a separately-computed forecast.
  [`SiteGenerator.cs:1350`](../../src/SpecScribe/SiteGenerator.cs#L1350)

- The single shared output-path formula so this resolver and the real page writer can never name a page differently.
  [`SiteGenerator.cs:1334`](../../src/SpecScribe/SiteGenerator.cs#L1334)

- Where the resolver gets threaded into story-page rendering, ahead of `storyPrefix` so the hrefs come out relative.
  [`SiteGenerator.cs:2079`](../../src/SpecScribe/SiteGenerator.cs#L2079)

**Markdown link emission (unchanged since the first pass — reviewed clean twice already)**

- Splices `[date](href)` in place of the plain reformatted date, only on the recognized dated-bullet shape.
  [`EpicsParser.cs:514`](../../src/SpecScribe/EpicsParser.cs#L514)

- The two new optional parameters threading the resolver through, defaulting to today's plain-text behavior.
  [`EpicsParser.cs:465`](../../src/SpecScribe/EpicsParser.cs#L465)

- `ExtractNamedSectionHtml`'s heading-gated pass-through — only "## Change Log" ever consults the resolver.
  [`EpicsParser.cs:408`](../../src/SpecScribe/EpicsParser.cs#L408)

**Tests**

- Pure splice-mechanics coverage: linked / unlinked / same-day-run / unrecognized-shape-never-consulted.
  [`ChangeLogSequencingTests.cs:81`](../../tests/SpecScribe.Tests/ChangeLogSequencingTests.cs#L81)

- End-to-end against a real git repo, including the review-loop-2 fix: a future-dated commit must stay plain text.
  [`SiteGeneratorChangeLogDateLinkTests.cs:1`](../../tests/SpecScribe.Tests/SiteGeneratorChangeLogDateLinkTests.cs#L1)
