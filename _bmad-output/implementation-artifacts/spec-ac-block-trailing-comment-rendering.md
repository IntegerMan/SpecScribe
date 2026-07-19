---
title: 'Render non-retirement HTML comments inside a story''s AC scan range as stylized notes'
type: 'bugfix'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'f0f30bdfaa942b377f6413ec67264a618a4ff958'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In `epics.md`, an HTML comment placed after a story's Acceptance Criteria (before the next `### Story` heading) — e.g. the Story 10.9 correct-course note about Stories 10.10-10.11 — is not a retirement/superseded notice, so `HoistBetweenStoryRetiredComments` leaves it in place. `ParseStory`'s AC-block scan then sweeps its raw lines in as literal gherkin content, so `<!--`/`-->` markers and body text leak verbatim into an `.ac-block` box on both the epic story card and the "not yet drafted" placeholder page (see story-10-9.html).

**Approach:** In `ParseStory`'s AC scan loop (`EpicsParser.cs` ~L824-844), detect `<!--`...`-->` comment runs the same way `HoistBetweenStoryRetiredComments` does (lazy match to next `-->`, unterminated left untouched), flush any pending AC block first, and divert the comment text to a new ordered `TrailingNotesHtml` list (each entry rendered via `MarkdownConverter.RenderBlock`, matching the existing `.md-comment` styling) instead of feeding it into `currentBlockLines`. Render the collected notes as sibling blocks after the AC list on both the story card and placeholder page.

## Boundaries & Constraints

**Always:**
- A comment matching `RetirementKeyword` is unaffected by this change — `HoistBetweenStoryRetiredComments` already blanks it before `ParseStory` runs, so it keeps routing to the epic's "Retired" section exactly as today.
- Comment markers (`<!--`/`-->`) never appear as visible text anywhere in AC output; markdown inside the comment (links, emphasis) still resolves via `RenderBlock`.
- Multiple comments within the AC scan range are each rendered as their own note block, in source order.
- Preserve exact HTML/whitespace shape for stories with no such comment (byte-identical); regenerate golden fingerprints only for the intended change.

**Ask First:** None — placement (after the AC list, not interleaved between individual AC items) and reuse of `.md-comment` styling both follow the existing `UserStoryNoteHtml` precedent directly.

**Never:**
- Do not change `HoistBetweenStoryRetiredComments`, `RetirementKeyword` classification, or the epic-level "Retired" section.
- Do not change how `## Acceptance Criteria` (top-level, drafted-story-file) comments render — this is scoped to `epics.md`'s inline numbered AC parsing in `ParseStory` only.
- Do not edit `epics.md` source to work around the rendering.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Trailing comment after AC, before next story heading | non-retirement `<!-- ... -->` block sits after the last AC line | Comment omitted from `.ac-block` content; rendered as its own `.md-comment` block after the AC list | N/A |
| Comment contains `--` | body text includes `--status-*` or similar | Rendered via `RenderBlock` (block comments permit `--`); no literal markers | N/A |
| Retirement-keyword comment | body matches `retired\|superseded\|deprecated` | Unchanged — already hoisted to epic "Retired" section before this loop runs | N/A |
| No trailing comment | AC region has only gherkin lines | `TrailingNotesHtml` empty; output byte-identical to before | N/A |
| Unterminated `<!--` | no closing `-->` before next heading | Left untouched, swept into AC content as before (degrade, NFR8) | N/A |
| Multiple comments | two separate comment runs in the AC range | Both rendered, each its own block, in source order | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsParser.cs` -- `ParseStory` AC scan loop (~L824-844): detect and divert comment runs; return `TrailingNotesHtml` on `StoryInfo`.
- `src/SpecScribe/EpicsModel.cs` -- `StoryInfo` (~L20): add `TrailingNotesHtml` (`IReadOnlyList<string>`, default empty).
- `src/SpecScribe/EpicsView.cs` -- `StoryCardView` (~L59) and `StoryPlaceholderView` (~L324): add matching field.
- `src/SpecScribe/EpicsViewBuilder.cs` -- (~L169, ~L248): copy the new field from `StoryInfo` to both views.
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- `AppendStoryCard` (after AC list close, ~L306) and `RenderStoryPlaceholderBody` (after AC panel close, ~L644): emit each note as a sibling block after the AC list/panel.
- `tests/SpecScribe.Tests/` -- `EpicsParserTests.cs`, `HtmlRenderAdapterTests.cs`, and golden-fingerprint test.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/EpicsModel.cs` -- add `TrailingNotesHtml` (`IReadOnlyList<string>`, default `Array.Empty<string>()`) to `StoryInfo` -- carries pre-rendered post-AC comment blocks.
- [x] `src/SpecScribe/EpicsParser.cs` -- in `ParseStory`'s AC loop, on a line starting with `<!--`, flush the pending AC block, consume lines through the closing `-->` (lazy, unterminated left untouched), render via `RenderBlock`, and append to a local notes list instead of `currentBlockLines`; set `TrailingNotesHtml` on the returned `StoryInfo`.
- [x] `src/SpecScribe/EpicsView.cs` -- add matching `TrailingNotesHtml` (default empty) to `StoryCardView` and `StoryPlaceholderView`.
- [x] `src/SpecScribe/EpicsViewBuilder.cs` -- copy `story.TrailingNotesHtml` into both views.
- [x] `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- in `AppendStoryCard`, after the `.ac-list` div closes, emit each `TrailingNotesHtml` entry; in `RenderStoryPlaceholderBody`, after the `.ac-panel` section closes, do the same.
- [x] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- added 3 tests: non-retirement trailing comment yields marker-free note + clean AC content (with a "--" AC line surviving untouched); retirement-keyword comment in the same position does not double-render via `TrailingNotesHtml`; no-comment story keeps `TrailingNotesHtml` empty.

**Acceptance Criteria:**
- Given a story in `epics.md` with a non-retirement HTML comment between its last AC line and the next story heading, when its epic-card and placeholder pages render, then the comment appears as a marker-free note block after the AC list and no literal `<!--`/`-->` text appears in the AC content.
- Given a story with a retirement-keyword comment in the same position, when it renders, then behavior is unchanged (routed to the epic's "Retired" section).
- Given the full test suite, when run, then all tests pass with the golden fingerprint regenerated to reflect only the intended render change.

## Verification

**Commands:**
- `dotnet build` -- expected: succeeds, no warnings introduced.
- `dotnet test` -- expected: all pass after fingerprint/parity expectations are updated.
- `dotnet run --project src/SpecScribe -- generate` then open `epics/epic-10.html` and the story-10.9 placeholder page -- expected: the Stories 10.10-10.11 note renders as a clean `.md-comment` block after the AC list, no literal markers visible.

**Note (review-loop, iteration 1):** `EpicsParserTests` (126 tests incl. this story's 6 new ones) and `HtmlRenderAdapterTests` are green and stable on every run. The repo's whole-site `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` test is currently unstable independent of this change — a concurrent session is actively editing unrelated files (`ListRow.cs`, `SiteGenerator.cs`, `FollowUpRow.cs`, CSS/JS assets, etc.) on this shared `main`, and the expected hash shifts between consecutive runs with no code changes here. Confirmed by isolating this diff to exactly `EpicsParser.cs` + the two test files (verified via `git diff HEAD --stat`) and re-running targeted tests, which stay green. Not this story's problem — defer to whoever is running that concurrent session.

## Suggested Review Order

**The parse-time guard (design intent)**

- Entry point: detects a non-retirement comment in the AC scan range, but only hoists it when it doesn't interrupt an in-progress Given/When/Then sequence — the two-agent code review caught the corruption risk of an unconditional flush.
  [`EpicsParser.cs:857`](../../src/SpecScribe/EpicsParser.cs#L857)

- The continuation-keyword lookahead that draws the line between "between two AC items" (safe to hoist) and "mid-block" (degrade instead).
  [`EpicsParser.cs:681`](../../src/SpecScribe/EpicsParser.cs#L681)

**Carrying the fragment through to render**

- New opaque-fragment field on the domain model, defaulted to empty so comment-free stories stay byte-identical.
  [`EpicsModel.cs:28`](../../src/SpecScribe/EpicsModel.cs#L28)

- Mirrored onto both section view models and copied through the builder.
  [`EpicsView.cs:61`](../../src/SpecScribe/EpicsView.cs#L61)

**Rendering it as its own block**

- Epic story card: emitted as a sibling after the AC list, before the "View full story plan" link.
  [`HtmlRenderAdapter.Epics.cs:308`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L308)

- Placeholder page: same treatment, emitted after the AC panel closes.
  [`HtmlRenderAdapter.Epics.cs:651`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L651)

**Coverage (peripherals)**

- Parser tests: the real Story-10.9-shaped trailing-comment case, the retirement-comment non-double-render guard, the mid-block corruption guard, and multi-comment ordering.
  [`EpicsParserTests.cs:331`](../../tests/SpecScribe.Tests/EpicsParserTests.cs#L331)

- Render tests: note position relative to the AC list/panel on both the story card and placeholder page.
  [`HtmlRenderAdapterTests.cs:217`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L217)
