---
title: 'Render epics.md user-story comment blocks as their own annotation block'
type: 'bugfix'
created: '2026-07-11'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '0a0d0f707c012c39799cf15d3d658033739bbc4a'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An HTML comment (`<!-- ... -->`) authored above the As-a/I-want/So-that lines of a story in `epics.md` renders wrong on epic story cards and "not yet drafted" placeholder pages: `ParseStory` collapses the whole user-story region into one line via `JoinUserStoryLines`, which turns the block comment into inline text merged into the italic user-story blurb. Because these comments contain `--` sequences (e.g. `--status-*`), Markdig refuses to parse them as an inline HTML comment, so the literal `<!--` and `-->` markers leak into the visible prose. The existing `HtmlBlockCommentRenderer` (which already strips markers and emits `<aside class="md-comment">`) never fires because the comment is no longer block-level.

**Approach:** In `ParseStory`, separate leading/standalone HTML comment lines in the user-story region from the narrative lines. Render the comment(s) through `MarkdownConverter.RenderBlock` (so the block-comment renderer emits a clean, marker-free `.md-comment` aside) and the narrative through the existing `RenderInline(JoinUserStoryLines(...))`. Carry the rendered comment as a new opaque-fragment field so both the epic story card and the placeholder page emit it as its own sibling block, above — not inside — the `.user-story` blurb.

## Boundaries & Constraints

**Always:**
- Keep the comment content rendered (markdown inside it — links, emphasis — still resolves) with the `<!--`/`-->` markers stripped, styled by the existing `.md-comment` block.
- The comment renders as its own block, a sibling that precedes the `.user-story` / `.story-lead` blurb, not nested inside it.
- Keep the epic-card render path and the placeholder-page render path in visual/structural agreement; if a byte-parity render exists for either, keep both sides identical.
- Preserve exact HTML/whitespace shape expected by byte-parity and golden-fingerprint tests; regenerate committed fingerprints only for the intended render change.

**Ask First:**
- Whether the comment block should also surface on drafted (fully authored) story pages. Current read: those already render correctly (their `## Story` blurb goes through `RenderBlock`, not `JoinUserStoryLines`), so this spec leaves them untouched.

**Never:**
- Do not change how comments render inside `RenderBlock`/`RenderDocumentHtml` or the `HtmlBlockCommentRenderer`/`HtmlInlineCommentRenderer` themselves — the block renderer is already correct; the fix is upstream in `ParseStory`.
- Do not drop, summarize, or reorder the comment content.
- Do not edit `epics.md` source to work around the rendering.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Leading comment + narrative | `<!-- note -->` block then `As a…/I want…/So that…` lines | `.md-comment` aside (markers stripped) rendered as its own block, then the `.user-story` blurb with only the narrative | N/A |
| Comment with `--` inside | comment body contains `--status-*` | Rendered by the block renderer (block comments permit `--`); markers stripped, no literal `<!--`/`-->` in output | N/A |
| No comment | only narrative lines | Unchanged: single `.user-story` blurb, no note block emitted | N/A |
| Multi-line comment | comment spans several indented lines | Joined as one block comment, whitespace/indentation inside collapsed harmlessly; one `.md-comment` aside | N/A |
| Comment only, no narrative | comment lines but no As-a lines | `.md-comment` block emitted; `.user-story` blurb omitted (empty) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsParser.cs` -- `ParseStory` (~L420) collects `userStoryLines` and calls `JoinUserStoryLines`; the fix partitions comment vs narrative here and populates the new field. `JoinUserStoryLines` (~L507) stays for narrative only.
- `src/SpecScribe/EpicsModel.cs` -- `StoryInfo` (~L7); add the rendered-comment fragment field.
- `src/SpecScribe/EpicsView.cs` -- `StoryCardView` (~L18) and `StoryPlaceholderView` (~L208) view models; add matching opaque-fragment field.
- `src/SpecScribe/EpicsViewBuilder.cs` -- `BuildStoryCard` (~L86) and `BuildStoryPlaceholder` (~L153) copy the new field from `StoryInfo`.
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- `AppendStoryCard` (~L228) and `RenderStoryPlaceholderBody` (~L372) emit the comment block before the `.user-story` div.
- `src/SpecScribe/CommentAnnotationRenderer.cs` / `MarkdownConverter.cs` -- reference only: `HtmlBlockCommentRenderer` + `RenderBlock` already produce the desired `.md-comment` aside. No change.
- `tests/SpecScribe.Tests/` -- `HtmlRenderAdapterTests`, `SectionViewModelSerializationTests`, `RenderSectionParityTests`/`RenderViewModelTests`/`RenderSpaParityTests`, and the golden-fingerprint test; update expectations/fingerprints for the new render.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/EpicsModel.cs` -- add `UserStoryNoteHtml` (string, default `""`) opaque-fragment field to `StoryInfo` -- carries the pre-rendered comment block.
- [x] `src/SpecScribe/EpicsParser.cs` -- in `ParseStory`, walk the user-story region with a `<!--`…`-->` state machine, splitting comment lines from narrative lines; set `UserStoryNoteHtml = RenderBlock(commentMd)` (empty when no comment) and keep `UserStoryHtml = RenderInline(JoinUserStoryLines(narrativeLines))` -- separates the block comment from the blurb at the source.
- [x] `src/SpecScribe/EpicsView.cs` -- add matching `UserStoryNoteHtml` (default `""`) to `StoryCardView` and `StoryPlaceholderView` -- opaque fragment for the render layer.
- [x] `src/SpecScribe/EpicsViewBuilder.cs` -- copy `story.UserStoryNoteHtml` into both views -- wires model → view.
- [x] `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- in `AppendStoryCard` and `RenderStoryPlaceholderBody`, when `UserStoryNoteHtml` is non-empty emit it as a sibling block immediately before the `.user-story`/`.story-lead` div -- renders the comment as its own block. (Placeholder: wrap in a `.story-lead` for width alignment, mirroring the blurb.)
- [x] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- added two tests: a leading `--`-containing comment yields a marker-free `.md-comment` block with a clean narrative-only `.user-story` blurb, and a comment-free story yields an empty note. Existing parity/serialization/golden tests pass unchanged (defaulted field, no fingerprint drift).

**Acceptance Criteria:**
- Given a story in `epics.md` with a leading multi-line HTML comment containing `--`, when its epic-card and placeholder pages render, then the comment appears as a marker-free `.md-comment` block above the user-story blurb and no literal `<!--`/`-->` text appears anywhere on the page.
- Given a story with no comment, when it renders, then output is byte-identical to before this change (no empty note block, no stray wrapper).
- Given the full test suite, when run, then all tests pass with the golden fingerprint regenerated to reflect only the intended comment-rendering change.

## Design Notes

Root cause is `JoinUserStoryLines` flattening the region: block-level `<!-- ... -->` becomes inline, and CommonMark inline HTML comments forbid `--`, so Markdig emits the markers literally. `RenderBlock` on the isolated comment hits `HtmlBlockCommentRenderer`, which permits `--` and strips markers.

Partition sketch (in `ParseStory`, replacing the current single collect-then-join loop):

```
bool inComment = false;
foreach raw line in [startIdx+1 .. userStoryEnd):
    t = line.Trim()
    if !inComment && t.StartsWith("<!--"): commentLines.Add(t); inComment = !t.Contains("-->"); continue
    if inComment: commentLines.Add(t); if t.Contains("-->") inComment = false; continue
    if t.Length > 0: narrativeLines.Add(t)
userStoryNoteHtml = commentLines.Count > 0 ? RenderBlock(join(commentLines,"\n")) : ""
```

Follow the codebase convention that inherently-HTML prose is a *named opaque fragment* (see `EpicsView.cs` header comment), so `UserStoryNoteHtml` is carried, not re-modelled. Default it to `""` rather than `required` to avoid churning every test/record construction site; the "empty when absent" contract matches the existing `UserStoryHtml` doc.

## Verification

**Commands:**
- `dotnet build` -- expected: succeeds, no warnings introduced.
- `dotnet test` -- expected: all pass after fingerprint/parity/serialization expectations are updated.
- `dotnet run --project src/SpecScribe -- generate` then open the generated `epics/story-6-9.html` -- expected: comment renders as a `.md-comment` block, no `<!--`/`-->` visible, user-story blurb shows only the narrative.

**Manual checks (if no CLI):**
- Inspect the generated story 6.9 (and epic-6 overview card) HTML for `class="md-comment"` around the seat-mapping note and absence of `&lt;!--`/`<!--` literals.

## Suggested Review Order

**The parse-time split (design intent)**

- Entry point: peels the *leading* comment run off the user-story region; lazy match keeps post-`-->` narrative and never eats an unterminated `<!--`.
  [`EpicsParser.cs:446`](../../src/SpecScribe/EpicsParser.cs#L446)

- The anchored, Singleline regex that defines "leading comment(s)" — the one line that resolves the swallow/reorder/same-line edge cases at once.
  [`EpicsParser.cs:29`](../../src/SpecScribe/EpicsParser.cs#L29)

- Guard so an empty `<!-- -->` doesn't emit a hollow aside.
  [`EpicsParser.cs:530`](../../src/SpecScribe/EpicsParser.cs#L530)

**Rendering it as its own block**

- Placeholder page: the note is a `.story-lead` sibling emitted *above* the `.user-story` blurb, not nested inside it.
  [`HtmlRenderAdapter.Epics.cs:376`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L376)

- Epic story card: same treatment, emitted inside the card ahead of the blurb.
  [`HtmlRenderAdapter.Epics.cs:228`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L228)

**The carried fragment (data seam)**

- New opaque-fragment field on the domain model, defaulted `""` so comment-free stories stay byte-identical.
  [`EpicsModel.cs:20`](../../src/SpecScribe/EpicsModel.cs#L20)

- Mirrored onto both section view models and copied through the builder.
  [`EpicsView.cs:56`](../../src/SpecScribe/EpicsView.cs#L56)

**Coverage (peripherals)**

- Parser tests: the real story-6.9-shaped `--`-comment case plus unterminated / same-line-`-->` / empty edges.
  [`EpicsParserTests.cs:135`](../../tests/SpecScribe.Tests/EpicsParserTests.cs#L135)

- Render tests: note ordering above the blurb, and omission when empty.
  [`HtmlRenderAdapterTests.cs:120`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L120)
