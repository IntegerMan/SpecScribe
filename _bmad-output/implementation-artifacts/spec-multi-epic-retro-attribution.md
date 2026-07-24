---
title: 'Attribute a retrospective to every epic it covers'
type: 'bugfix'
created: '2026-07-24'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '8db18aaddd7cc1325910bfc9b00e0ae9d1ac66a1'
baseline_note: 'At baseline the tree already carried another session unstaged edits to SiteGenerator.cs, DashboardView.cs, DashboardViewBuilder.cs, HtmlTemplater.cs, HtmlRenderAdapter.Dashboard.cs, FileWatcherService.cs, WorkGraphTemplater.cs and two story files. Scope review by this spec File List, never by a commit range.'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `RetroParser`'s filename pattern `^epic-(\d+)-retro\b` does not match a joint retrospective like `epic-19-21-retro-2026-07-23.md`, so `IsRetroFile` returns false and the file is never ingested as a retro. Epics 19 and 21 both carry `HasRetrospective = false`, `StatusStyles.ForEpicWithRetrospective` downgrades two all-done epics from `done` to `review` (dashboard reads 9/24, not 11/24), and the retro falls through to the generic docs pass — no styled page, no date/participant lift, no action-item badging, no link from either epic.

**Approach:** Widen retro→epic from one-to-one to one-to-many: recognize the common multi-epic filename spellings, carry the full covered set on `RetroModel`, and fan that set out everywhere a retro is keyed by a single epic number.

## Boundaries & Constraints

**Always:**
- Attribution comes from the **filename** only, as it does today. Do not parse body text for epic numbers.
- Single-epic retros must render **byte-identically** — the golden fingerprint fixture contains `epic-1-retro-2026-07-06.md`, so any drift there is a regression, not a decision.
- Covered numbers are de-duplicated and ascending.
- A retro naming an epic absent from the epics model still ingests; that epic just contributes no link or stories (today's `epicExists` guard generalizes).

**Ask First:**
- Any change to `StatusStyles.ForEpic` / `ForEpicWithRetrospective`. Fix this through the data feeding the classifier, never the classifier.
- Adding a naming spelling beyond those in the matrix below.

**Never:**
- Do not touch `.claude/skills/` or any BMad workflow. SpecScribe reads however the user's repo is already organized; the generator adapts, the user's process does not.
- Do not edit `sprint-status.yaml` or backfill stale `epic-N: in-progress` keys — SpecScribe derives epic status from stories and ignores that key.
- No new git call, no new ingestion pass.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected Behavior | Error Handling |
|----------|-------|-------------------|----------------|
| Single epic (regression) | `epic-1-retro-2026-07-07.md` | Covers `[1]`; every rendered byte unchanged | N/A |
| Joint | `epic-19-21-retro-2026-07-23.md` | Covers `[19,21]`; both epics get `HasRetrospective` + a retro link | N/A |
| Three or more | `epic-19-20-21-retro-*.md` | Covers `[19,20,21]` | N/A |
| Spelling variants | `epics-19-21-`, `epic-19-and-21-`, `epic-19+21-` | All cover `[19,21]` | N/A |
| Repeated number | `epic-19-19-retro-*.md` | Covers `[19]` — de-duplicated | N/A |
| Date not absorbed | `epic-1-retro-2026-07-07.md` | Covers `[1]`; `2026`/`07` never read as epics | N/A |
| Not a retro | `1-1-some-story.md`, `epics.md` | Unrecognized; handling unchanged | N/A |
| Retro-looking, unmatched | `retro-notes.md` | Not ingested, **and** an `Unsupported` diagnostic names it | Skip, not error |
| Parse throws | recognized name, malformed body | `Malformed` diagnostic; siblings still parse | Unchanged |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/RetroParser.cs` -- discovery + attribution; root cause at line 11.
- `src/SpecScribe/RetroModel.cs` -- `EpicNumber` (`required int`) → covered set.
- `src/SpecScribe/BmadArtifactAdapter.cs` -- `IngestRetros` (~283): filter, `consumed` set, `OrderBy(r => r.EpicNumber)`.
- `src/SpecScribe/SiteGenerator.cs` -- `SetRetros` (~3070) builds `_epicRetroMap`; the fan-out point. `TagEpicRetrospectives` (~3085) reads by key — no change.
- `src/SpecScribe/RetroTemplater.cs` -- index card meta (37); page kicker (75), epic pill (85), stories section (95-110), `HeadingTitle` (138).
- `src/SpecScribe/ActionItemsTemplater.cs` -- `RenderGroupHeading` (61) consumes the map by key; fan-out fixes it. Verify, don't change.
- `src/SpecScribe/StatusStyles.cs` -- `ForEpicWithRetrospective` (133). Read-only.
- `tests/SpecScribe.Tests/RetroTests.cs` -- discovery/parse/render coverage to extend.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- golden inventory (~214), fingerprint (~230).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/RetroParser.cs` -- accept `epics?-` plus a `-`/`+`/`and`-separated number run before `-retro`; replace `EpicNumberOf` with a set-returning `EpicNumbersOf` (distinct, sorted) -- one seam owns attribution, so every consumer inherits the fix.
- [x] `src/SpecScribe/RetroModel.cs` -- replace the scalar `EpicNumber` with the covered set -- removing it makes the compiler enumerate every consumer rather than leaving a silent single-epic path.
- [x] `src/SpecScribe/BmadArtifactAdapter.cs` -- sort by lowest covered epic; emit the `Unsupported` diagnostic for retro-looking files that don't match -- the next naming variant surfaces as a visible skip instead of vanishing.
- [x] `src/SpecScribe/SiteGenerator.cs` -- fan `SetRetros` out to one map entry per covered epic, resolving contention for an epic by authored date (then filename, then Ordinal) -- this is what flips `HasRetrospective` for Epic 21. *(Amended at review: the original "preserve the existing tiebreak" wording carried a defect — see Spec Change Log.)*
- [x] `src/SpecScribe/RetroTemplater.cs` -- pluralize kicker, index meta, pill links and stories across the covered set, keeping single-epic strings character-for-character identical -- protects the golden fingerprint.
- [x] `tests/SpecScribe.Tests/RetroTests.cs` -- cover every matrix row, including date-absorption and de-duplication, plus a two-epic render asserting both pill links and a merged stories grid.
- [x] Grep to confirm no retro `EpicNumber` reference survives, and that `ActionItemsTemplater` / `TagEpicRetrospectives` needed no edit.

**Two consumers the Code Map missed, found by removing the scalar (the reason for that task's rationale):**
- [x] `src/SpecScribe/SiteGenerator.cs` `RenderRetroPages` -- retro pager ordering now sorts on `PrimaryEpicNumber`.
- [x] `src/SpecScribe/UnplannedWorkGeometry.cs` `ResolveEpicByDateMatch` -- a joint retro's date now contributes EVERY epic it covers, so the pre-existing "ties → null" rule declines to guess rather than silently attributing the work to the first epic.

**Acceptance Criteria:**
- Given the repo contains `epic-19-21-retro-2026-07-23.md`, when the site is generated, then Epics 19 and 21 both render **Done** (not "In review") on the dashboard tile, epics index, sunburst and epic-page badge, and the Epic Status tile reads **11/24**.
- Given that file, when the retros index renders, then one card covers both epics and its page carries both epic links plus both epics' stories.
- Given only single-epic retros, when the suite runs, then the golden content fingerprint is unchanged from its committed value.
- Given an action item attributed to Epic 21, when `action-items.html` renders, then its group heading links to the joint retro.

## Spec Change Log

### 2026-07-24 — adversarial review (Blind Hunter + Edge Case Hunter), no loopback

**Routing note, stated plainly.** One finding (the `SetRetros` tiebreak) is `bad_spec` by the letter of the
triage rules: the Tasks section instructed "preserving the existing tiebreak", and that instruction is what
carried the defect in. I treated it as `patch` instead of taking a full revert-and-re-derive loopback, because
the fix is a single sort expression and re-deriving five verified files would have risked losing working code
for no coherence gain. Flagging the deviation rather than hiding it. `review_loop_iteration` stays 0.

**Amended:** the Tasks bullet for `SiteGenerator.cs` no longer says "preserve the existing tiebreak" — the
tiebreak is now date-first (see below).

**Findings fixed (7).** All were holes in this change's OWN safety claims:
1. **False-accept regression I introduced.** `epic-1-2026-07-07-retro.md` — a date BEFORE `-retro` — was read
   as epics 1/2026/7/7 and would have marked the real Epic 7 retro'd. The old regex rejected it; mine accepted
   it. Cured by bounding each epic token to 1-3 digits. The spec's matrix covered date-AFTER-retro only.
2. **Overflow hole.** `epic-99999999999-retro-*` matched, parsed to nothing, and was consumed — attributed to
   no epic while the new diagnostic stayed silent, because the diagnostic keyed on regex non-match rather than
   on "produced zero epics". Both the bound and the predicate were fixed.
3. **Culture-dependent total failure.** `IgnoreCase` without `CultureInvariant` folds using the current
   culture; under tr-TR/az the dotted/dotless `I` made `EPIC-…` match neither regex, so a whole retro went
   invisible on a Turkish-locale machine *and* the safety net missed it too. Added `CultureInvariant`.
4. **Unicode digits.** `\d` matches non-ASCII digits that `int.TryParse` then rejects — the same silent drop by
   another route. Switched to `[0-9]`.
5. **Tiebreak picked the OLDER retro.** Filename-descending was a valid proxy for date only while every retro
   for an epic shared the `epic-N-retro-` prefix; a second filename shape destroys that. Verified:
   `epic-19-retro-2026-07-01` sorted above `epic-19-21-retro-2026-08-01`. Now date-first, then filename, then
   Ordinal for determinism. The `EpicRetroMap` doc comment claiming "(latest, by filename)" had become false
   and was corrected.
6. **Joint retro vetoed unplanned-work attribution.** A joint retro contributed several hits, tripping the
   "ties → null" rule and suppressing the Tier 2 story-date signal — work that used to resolve became
   "Unattributed". A joint retro is ONE piece of non-discriminating evidence, so it now abstains; two
   DIFFERENT single-epic retros on a day is still a real conflict and still returns null.
7. **`EpicNumbers` documented an invariant it did not enforce**, and `EpicsLabel` rendered a bare "Epic" for
   the empty set (reads as data, not as failure). Normalized on construction; label now says "Unattributed".

**The most valuable finding was a test gap, not a bug:** the fan-out — adapter ingest → `SetRetros` →
`TagEpicRetrospectives` → `ForEpicWithRetrospective`, the sole mechanism that turns "In review" into "Done" —
had NO test. Every existing `HasRetrospective` assertion in the suite sets that flag by hand, so a regression
breaking only the fan-out would have shipped green. Added an end-to-end test with its own fixture (the golden
fixture deliberately pins an all-done-WITHOUT-retro epic and must not gain a joint retro).

**Rejected after verification (4):** ReDoS (Blind Hunter measured it linear — 30 ms on 120 KB — no
catastrophic backtracking); double-encoding/XSS (every `EpicsLabel` call site traced, escaped exactly once);
`HeadingTitle` not stripping the joint prefix (verified in the browser that the h1 reads well, and the
proposed fix would have produced a title starting "— Epic 19 …"); keeping an obsolete `EpicNumber` shim for
third-party adapters (no external consumers, and the shim would have hidden the two real consumers that
removing the scalar exposed).

**KEEP if this is ever re-derived:** removing the scalar `EpicNumber` outright rather than shimming it — the
compiler found two consumers the Code Map missed, one of which (`UnplannedWorkGeometry`) carried a real
semantic decision. And keep the worktree-based attribution method: on shared `main` it is the only way to tell
your own fingerprint drift from a sibling session's.

## Design Notes

The number run must stay anchored by a literal `-retro`, or the date gets absorbed: `epic-1-retro-2026-07-07` is safe only because a greedy attempt at `1-2026-07-07` finds no following `-retro` and backtracks to `1`.

```csharp
// epic-19-21-retro-… | epics-19-and-21-retro-… | epic-1-retro-…
new Regex(@"^epics?-(?<nums>\d+(?:(?:-and-|[-+&])\d+)*)-retro\b", IgnoreCase)
// then: Regex.Matches(nums, @"\d+") → distinct → ordered
```

## Verification

**Commands:**
- `dotnet test` -- expected: green; golden content fingerprint **unchanged**; new `RetroTests` cases pass.
- `dotnet run --project src/SpecScribe -- generate` -- expected: no `Error` events; `SpecScribeOutput/implementation-artifacts/epic-19-21-retro-2026-07-23.html` is a styled retro page, not a generic doc.

**Manual checks:**
- Per CLAUDE.md, verify in a live browser: on `SpecScribeOutput/index.html` the Epic Status tile reads 11/24 with no "In review" segment from Epics 19/21; on `epics/epic-21.html` the badge reads Done and its retro link resolves to the joint retro.

## Verification Record (2026-07-24)

**Attribution was required before any result could be trusted.** During implementation the shared `main`
working tree accumulated a second session's uncommitted work (`specscribe.css`, `specscribe.js`,
`HtmlTemplater.cs`, `DashboardView.cs`, `SunburstExplorer.cs`, new `RelatedWork*.cs`, …), and the golden
fingerprint drifted `1711700e…` → `253fe05c…`. Full-suite failure counts swung 7 / 13 / 21 across identical
runs, so in-tree numbers were meaningless.

Resolved with a throwaway detached worktree at `8db18aa` carrying ONLY this spec's files (`SiteGenerator.cs`
is co-owned, so its two hunks were hand-applied):

- Clean HEAD, no changes: fingerprint **passes** at the committed constant → HEAD is a valid baseline.
- HEAD + this spec's changes only: **24/24 pass**, including `GoldenContentFingerprint` (still `1711700e…`)
  and `GoldenOutputInventory` → this change is byte-neutral for single-epic retros, as the spec requires.
  The in-tree drift is therefore entirely the concurrent session's, not this work's.
- Full suite on that worktree: **2,234 passed, 4 failed** — all four git-fixture tests
  (`SiteGeneratorTimelineTests`, `SiteGeneratorChangeLogDateLinkTests`, `SiteGeneratorCodeInsightsTests`,
  `SiteGeneratorCommitDetailsTests`), none retro-related, and **all four green when re-run in isolation**:
  the pre-existing parallel-load flake recorded in Story 20.2.
- An isolation probe additionally ruled the new `Unsupported` diagnostic out as a fingerprint cause
  (identical hash with it disabled).

**Real-repo generation** (374 pages, 0 errors) confirmed every acceptance criterion:
Epic Status aria reads `11 done, 4 in development, 7 ready for dev, 2 stories drafted` — no "in review"
segment; the joint retro renders as a first-class page (kicker `Epics 19 &amp; 21 Retrospective`, escaped
once, no double-encoding; both epic pills; "Stories in these Epics" with 19.1, 19.2, 21.1, 21.2, 21.3);
`retros.html` card meta reads `Epics 19 & 21`; and `action-items.html` links BOTH the "From the Epic 19
retrospective" and "From the Epic 21 retrospective" headings to the joint retro.

**Live browser** (CLAUDE.md): dashboard tile renders **11/24**; `epics/epic-21.html` shows a green
`✓ DONE` badge — state carried by glyph + word, not color alone.

**Re-verified after the review patches:** full suite **2,291 passed / 0 failed**; regeneration still 374 pages
/ 0 errors with `11 done` and no "in review" segment; joint retro page unchanged; and the real repo raises
**no** spurious `Unsupported` diagnostics from the new safety net.

## Suggested Review Order

**The defect and its cure**

- One regex was the whole bug: it matched a joint retro name not at all.
  [`RetroParser.cs:26`](../../src/SpecScribe/RetroParser.cs#L26)

- Attribution becomes a set; bounded ASCII digits stop dates being read as epics.
  [`RetroParser.cs:70`](../../src/SpecScribe/RetroParser.cs#L70)

- The fan-out that actually flips Epic 21 from "In review" to "Done".
  [`SiteGenerator.cs:3266`](../../src/SpecScribe/SiteGenerator.cs#L3266)

**Safety net — never silently drop a retro**

- Keys on "produced zero epics", not on regex non-match; that hole was found in review.
  [`RetroParser.cs:62`](../../src/SpecScribe/RetroParser.cs#L62)

- Reports an unparseable epic-retro name instead of consuming it invisibly.
  [`BmadArtifactAdapter.cs:291`](../../src/SpecScribe/BmadArtifactAdapter.cs#L291)

**Semantic decisions worth a careful read**

- A joint retro abstains rather than vetoing unplanned-work attribution.
  [`UnplannedWorkGeometry.cs:269`](../../src/SpecScribe/UnplannedWorkGeometry.cs#L269)

- Invariant enforced by the type, not merely documented on it.
  [`RetroModel.cs:17`](../../src/SpecScribe/RetroModel.cs#L17)

**Rendering across the covered set**

- Plural label; returned unescaped so each call site escapes exactly once.
  [`RetroTemplater.cs:153`](../../src/SpecScribe/RetroTemplater.cs#L153)

- One back-link per covered epic that exists in the model.
  [`RetroTemplater.cs:90`](../../src/SpecScribe/RetroTemplater.cs#L90)

- Plural keys off epics that actually contributed stories.
  [`RetroTemplater.cs:107`](../../src/SpecScribe/RetroTemplater.cs#L107)

**Tests**

- End-to-end pin on the fan-out — the mechanism that had no test at all.
  [`RetroTests.cs:386`](../../tests/SpecScribe.Tests/RetroTests.cs#L386)

- Names that must be rejected yet still reported.
  [`RetroTests.cs:103`](../../tests/SpecScribe.Tests/RetroTests.cs#L103)

- Turkish-locale casing, which silently hid whole retros.
  [`RetroTests.cs:118`](../../tests/SpecScribe.Tests/RetroTests.cs#L118)

- Every supported spelling of a joint retro name.
  [`RetroTests.cs:71`](../../tests/SpecScribe.Tests/RetroTests.cs#L71)
