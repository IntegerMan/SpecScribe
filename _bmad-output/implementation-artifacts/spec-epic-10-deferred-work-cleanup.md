---
title: 'Epic 10 deferred-work cleanup (6 items)'
type: 'chore'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '96fc5637cab8791b24fbf13e7c533dc0929631ab'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Six real-but-not-now items from Epic 10 code reviews remain open in `deferred-work.md`: no test pins the `KeyViewGroupOrder` misfiling fallback; the sunburst dense-collapse legend can advertise a "no plan" swatch matching no visible wedge; `ReferenceChipRenderer.ProtectedSplit` doesn't shield `<kbd>`/`<samp>` inner text from chip rewriting; epic-preamble retirement HTML comments (before the first `### Story` heading) are never hoisted; `AbbreviationExpander` still wraps bare acronyms mid-reference when the separator is punctuation other than space/hyphen/en/em-dash (e.g. `ADR.0005`); and `groupByHref` in `SiteGenerator.WriteFollowUpDetails` has no guard confirming its three membership sources stay mutually exclusive. Two other items in the batch (pinned-active-item removal using value-equality; `WriteFollowUpDetails` computing `counts`/`geometry` unconditionally on zero follow-up work) turned out to already be resolved by later commits (`efe6e34`'s index-based removal; the existing `actionItems.Count == 0 && deferredPairs.Count == 0` early return) — those two get closed out in `deferred-work.md` as no-op, not re-implemented.

**Approach:** Fix each of the 6 still-open items narrowly at its cited file/line, add the regression test each was missing, and mark all 8 items (6 fixed + 2 already-resolved) closed in `deferred-work.md` with a `RESOLVED 2026-07-20` strikethrough note, following the existing convention in that file.

## Boundaries & Constraints

**Always:**
- Preserve byte-identical output for every page/scenario not touched by these 6 fixes — verify against the golden fingerprint after changes.
- Follow each item's own "evidence" note for the chosen fix shape; do not redesign beyond what's needed to close the gap.
- Mark deferred-work.md items closed using the existing `~~summary~~` **RESOLVED date** (`spec-slug`): ... convention already used elsewhere in that file.

**Ask First:** None anticipated — all 6 fixes are narrow and match their evidence notes' suggested direction.

**Never:**
- Do not touch `AppendLocalContextBand`'s pinned-active-item logic (item 1) — already fixed by commit `efe6e34`.
- Do not touch `WriteFollowUpDetails`'s `counts`/`geometry` computation ordering (item 8) — already covered by the existing early return.
- Do not redesign the sunburst legend/dense-collapse system beyond suppressing the orphaned "no plan" swatch.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Misfiled QuickLink group | `QuickLinks.Group` is a value not in `KeyViewGroupOrder` (e.g. misspelled) | Nav entry falls into "Project" group, pinned by test | N/A |
| Dense-collapse all-no-plan | 8+-story epic, every zero-task story falls inside the collapsed summary wedge | Legend "no plan" swatch is suppressed (no orphaned legend item) | N/A |
| `<kbd>`/`<samp>` chip text | Page HTML contains `<kbd>path.ext:5</kbd>` | Inner text left unrewritten, no `<span class="ref-chip">` injected inside the tag | N/A |
| Epic preamble retirement comment | `<!-- RETIRED ... -->` sits before the epic's first `### Story` heading | Comment is hoisted into `RetiredNoticesHtml`, blanked from goal/meta text | N/A |
| Punctuation-separated ADR reference | Page text contains `ADR.0005`, `ADR:0005`, `ADR/0005` | Acronym stays unwrapped (no `<abbr>` mid-reference) | N/A |
| Follow-up group membership | Same href appears in two of `FollowUpGroupPages.Enumerate`'s three sources | Test/assert catches the collision instead of silently last-write-wins | Documented failure, not a silent misfile |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/HtmlRenderAdapter.cs:186-189` -- `KeyViewGroupOrder` fallback; needs test only, no code change
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` -- add fallback-group test
- `src/SpecScribe/Charts.cs:435-436,753-760,898-899` -- sunburst legend + dense-collapse `hasNoPlan` computation
- `tests/SpecScribe.Tests/ChartsTests.cs` -- add dense-collapse-all-no-plan legend test
- `src/SpecScribe/ReferenceChipRenderer.cs:25-28` -- `ProtectedSplit` regex, missing `<kbd>`/`<samp>` open-close capture
- `tests/SpecScribe.Tests/ReferenceChipRendererTests.cs` -- add kbd/samp protection test
- `src/SpecScribe/EpicsParser.cs:658-769` -- `HoistBetweenStoryRetiredComments` call site scoped to `storyStarts[0].Index..bodyEnd`, excludes preamble
- `tests/SpecScribe.Tests/EpicsParserTests.cs` -- add preamble-retirement-comment test
- `src/SpecScribe/AbbreviationExpander.cs:45-49` -- trailing lookahead only covers space/hyphen/en/em-dash separators
- `tests/SpecScribe.Tests/AbbreviationExpanderTests.cs` -- add punctuation-separator test
- `src/SpecScribe/SiteGenerator.cs:3350-3355` -- `groupByHref` dictionary build, no disjointness guard
- `tests/SpecScribe.Tests/FollowUpGroupPagesTests.cs` -- add disjointness-guard/assert test (moved here from the planned FollowUpSurfacesTests.cs — this file already hosts `Enumerate`-focused tests)
- `_bmad-output/implementation-artifacts/deferred-work.md` -- close all 8 items with `RESOLVED 2026-07-20` notes

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/HtmlRenderAdapter.cs` -- no code change (fallback already correct) -- confirmed behavior, added test
- [x] `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` -- added `RenderNav_OffHome_UnrecognizedQuickLinkGroup_FallsBackToProjectGroup`, asserting an unrecognized `Group` string renders under the "Project" pill/panel
- [x] `src/SpecScribe/Charts.cs` -- added `hasVisibleNoPlan` (set only in the non-dense per-story rendering branch), replacing the `epics.Any(...)`-over-everything `hasNoPlan` at the project-glance legend call site -- suppresses a legend swatch that would match no visible wedge on dense-collapsed epics
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- added `Sunburst_DenseEpic_AllNoPlanStoriesCollapsed_SuppressesOrphanedNoPlanLegendSwatch` (8-story epic, every story zero-task), asserting the "No task plan" legend item is absent
- [x] `src/SpecScribe/ReferenceChipRenderer.cs` -- extended `ProtectedSplit` with `<kbd\b[^>]*>.*?</kbd>|<samp\b[^>]*>.*?</samp>` alternatives (same shape as the existing `<code>`/`<pre>` captures) so inner text is shielded, not just the tag boundary
- [x] `tests/SpecScribe.Tests/ReferenceChipRendererTests.cs` -- added `BareFileLine_InsideKbdSpan_IsUntouched` / `BareFileLine_InsideSampSpan_IsUntouched`
- [x] `src/SpecScribe/EpicsParser.cs` -- widened the `HoistBetweenStoryRetiredComments` scan start from `storyStarts[0].Index` to `idx + 1` (right after the epic heading) so preamble comments are scanned too; the existing "next non-blank is a story heading or EOF" guard already prevents false positives on ordinary preamble prose
- [x] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- added `Parse_RetirementComment_InEpicPreamble_IsHoistedNotSweptIntoGoalText`
- [x] `src/SpecScribe/AbbreviationExpander.cs` -- widened the trailing lookahead to `(?![\s\-–—./:]*\d{2,})`, also treating `.`, `:`, `/` as numbered-reference separators
- [x] `tests/SpecScribe.Tests/AbbreviationExpanderTests.cs` -- added `Expand_SkipsNumberedReferences_DotColonOrSlashThenDigits` covering `ADR.0005`, `ADR:0005`, `ADR/0005`, plus a bare-"ADR"-still-expands case
- [x] `src/SpecScribe/SiteGenerator.cs` -- added a `Debug.Assert` at the `groupByHref` build loop confirming no href is assigned to two different `FollowUpGroupSpec`s
- [x] `tests/SpecScribe.Tests/FollowUpGroupPagesTests.cs` -- added `Enumerate_MixedInventory_MembershipSourcesAreMutuallyExclusiveByDetailHref` (relocated here from the planned FollowUpSurfacesTests.cs, alongside existing `Enumerate`-focused tests), asserting `FollowUpGroupPages.Enumerate`'s three sources never share a `DetailHref` across specs for a realistic mixed inventory
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- marked all 8 named items (the 6 fixed above, plus items 1 and 8 as already-resolved) with `~~summary~~` **RESOLVED 2026-07-20** notes matching the file's existing convention
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- regenerated and re-locked the golden content fingerprint (Charts.cs's `hasVisibleNoPlan` change shifted this fixture's dense-epic legend output; isolated via stash-bisection against a concurrent session's unrelated in-flight changes on the same working tree, and against my other 5 fixes, before locking in — stable across 3 repeated runs)

**Acceptance Criteria:**
- Given an unrecognized `QuickLinks.Group` value, when the key-views band renders, then the entry appears under "Project" and a test pins this.
- Given an 8+-story epic where every no-plan story is inside the collapsed wedge, when the sunburst legend renders, then no orphaned "no plan" legend swatch appears.
- Given a `path.ext:5`-shaped chip pattern inside `<kbd>` or `<samp>`, when `ReferenceChipRenderer` runs, then the text inside those tags is left unrewritten.
- Given a retirement HTML comment in an epic's preamble (before the first `### Story` heading), when `EpicsParser` parses the epic, then the comment is hoisted into `RetiredNoticesHtml` and blanked from goal/meta text.
- Given page text containing `ADR.0005`, `ADR:0005`, or `ADR/0005`, when `AbbreviationExpander` runs, then the acronym is not wrapped in `<abbr>` mid-reference.
- Given the full test suite and a fresh `specscribe generate` run against this repo, when compared to the current golden fingerprint, then only the intentionally-changed pages (sunburst legend on dense epics, `<kbd>`/`<samp>` chip pages, epic pages with preamble retirement comments, ADR-referencing pages) differ — regenerate and re-lock the fingerprint.

## Spec Change Log

## Design Notes

Items 1 (`AppendLocalContextBand` pinned-active-item value-equality) and 8 (`WriteFollowUpDetails` unconditional `counts`/`geometry`) were investigated during planning and found already resolved: item 1 by commit `efe6e34` (index-based `RemoveAt` replaced the `Where(i => i != pinnedActive)` value-equality removal); item 8 because the existing `if (actionItems.Count == 0 && deferredPairs.Count == 0) return;` guard at the top of `WriteFollowUpDetails` already prevents the `counts`/`geometry`/`groupSpecs` computation whenever there is genuinely zero follow-up work — and when `actionItems.Count > 0`, that computation is no longer wasted since Story 10.10 wired `actionLocalContext` through the same `groupByHref` map. No code change needed for either; both get closed in `deferred-work.md` as no-op resolutions.

## Verification

**Commands:**
- `dotnet test` -- expected: full suite green, including the 6 new tests
- `dotnet run --project src/SpecScribe -- generate` (or the project's existing generate command) against this repo -- expected: only the anticipated pages change vs. the current golden fingerprint; regenerate and commit the updated fingerprint if intentional-only diffs are confirmed

## Suggested Review Order

**Sunburst dense-collapse legend (item 3)**

- Tracks a no-plan wedge only when it's actually un-collapsed and drawn, so the legend never advertises a swatch matching no visible wedge.
  [`Charts.cs:324`](../../src/SpecScribe/Charts.cs#L324)

- Pins the all-collapsed suppression case.
  [`ChartsTests.cs:861`](../../tests/SpecScribe.Tests/ChartsTests.cs#L861)

- Code-review patch: pins the mixed dense+sparse case the OR-across-epics logic depends on.
  [`ChartsTests.cs:884`](../../tests/SpecScribe.Tests/ChartsTests.cs#L884)

**kbd/samp chip shielding (item 4)**

- Extends the protected-tag alternation so `<kbd>`/`<samp>` inner text is never rewritten, matching `<code>`/`<pre>`.
  [`ReferenceChipRenderer.cs:26`](../../src/SpecScribe/ReferenceChipRenderer.cs#L26)

- Pins both tags stay untouched.
  [`ReferenceChipRendererTests.cs:64`](../../tests/SpecScribe.Tests/ReferenceChipRendererTests.cs#L64)

**Epic-preamble retirement comments (item 5)**

- Widens the hoist scan to start right after the epic heading and run unconditionally, so preamble comments (including on zero-story epics) are hoisted, not just between-story ones.
  [`EpicsParser.cs:774`](../../src/SpecScribe/EpicsParser.cs#L774)

- Pins the one-story preamble case.
  [`EpicsParserTests.cs:307`](../../tests/SpecScribe.Tests/EpicsParserTests.cs#L307)

- Code-review patch: pins the zero-story edge case the unconditional call exists for.
  [`EpicsParserTests.cs:358`](../../tests/SpecScribe.Tests/EpicsParserTests.cs#L358)

**Numbered-reference punctuation separators (item 6)**

- Two-shape lookahead: loose separators (space/hyphen/dash) before digits, or a tight dot/colon/slash directly abutting digits — deliberately narrower than a blanket widening so ordinary sentence punctuation still expands.
  [`AbbreviationExpander.cs:48`](../../src/SpecScribe/AbbreviationExpander.cs#L48)

- Pins the tight-citation skip cases (`ADR.0005`, `ADR:0005`, `ADR/0005`).
  [`AbbreviationExpanderTests.cs:84`](../../tests/SpecScribe.Tests/AbbreviationExpanderTests.cs#L84)

- Code-review patch: pins that a sentence-final period or a prose colon (followed by a space) still expands normally.
  [`AbbreviationExpanderTests.cs:105`](../../tests/SpecScribe.Tests/AbbreviationExpanderTests.cs#L105)

**Follow-up group disjointness guard (item 7)**

- Makes the three-source mutual-exclusivity invariant observable in Debug builds instead of silently letting the last-write-wins indexer hide a collision.
  [`SiteGenerator.cs:3373`](../../src/SpecScribe/SiteGenerator.cs#L3373)

- Pins the invariant at the `Enumerate` data level (the assert's actual precondition) across a realistic mixed inventory.
  [`FollowUpGroupPagesTests.cs:152`](../../tests/SpecScribe.Tests/FollowUpGroupPagesTests.cs#L152)

**Peripherals**

- Test-only pin for item 2 (`KeyViewGroupOrder` fallback) — no production code change needed.
  [`HtmlRenderAdapterTests.cs:1123`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L1123)

- Golden content fingerprint re-locked; the comment records the stash-bisection that isolated the sunburst legend change as the sole cause among this pass's 6 fixes.
  [`SiteGeneratorAdapterTests.cs:650`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L650)

- All 8 named deferred-work items (6 fixed, 2 already-resolved) closed with `RESOLVED 2026-07-20` notes; one new item deferred (`AbbreviationExpander` backslash separator, speculative/out of scope).
  [`deferred-work.md`](deferred-work.md)
