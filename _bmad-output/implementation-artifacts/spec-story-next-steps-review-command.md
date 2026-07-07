---
title: 'Story Next Steps: scope to this story and carry the code-review id'
type: 'feature'
created: '2026-07-06'
status: 'done'
route: 'one-shot'
---

# Story Next Steps: scope to this story and carry the code-review id

## Intent

**Problem:** A story detail page's "Next Steps" panel suggested epic/project-level moves that don't belong there — drafting the *next* story (`create-story`) and running a `retrospective` — and its code-review suggestion rendered as a bare command with no story id, so the reader had to hand-edit `/bmad-code-review` into `/bmad-code-review 2.1`.

**Approach:** Narrow `BmadCommands.ForStory` so a story page only ever suggests actions on *this* story: every branch's `code-review` now carries `story.Id` (rendering the exact `/bmad-code-review 2.1`), and the done/review branch drops `create-story` (next story) and `retrospective`, leaving code-review alone. The fall-through for an unplanned story still keeps `create-story` with the story's *own* id (drafting the story being viewed) and `check-implementation-readiness` for `.1` stories. Epic and project panels (`ForEpic`, `ForProject`) still surface `create-story`/`retrospective` where they belong.

## Suggested Review Order

- Design intent — the doc comment stating a story page only acts on *this* story
  [`BmadCommands.cs:128`](../../src/SpecScribe/BmadCommands.cs#L128)

- The done/review branch is now code-review-only (with id); `create-story`/`retrospective` removed
  [`BmadCommands.cs:156`](../../src/SpecScribe/BmadCommands.cs#L156)

- `ready` and `in-progress` branches pass `story.Id` to code-review for the exact command
  [`BmadCommands.cs:138`](../../src/SpecScribe/BmadCommands.cs#L138)

- The kept exception: unplanned-story fall-through still drafts the story's own plan
  [`BmadCommands.cs:163`](../../src/SpecScribe/BmadCommands.cs#L163)

- Regression guard: review-status story yields only code-review-with-id, withholding create-story/retrospective even when installed
  [`ModuleContextTests.cs:151`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L151)

- Supporting tests: in-progress carries the id; unplanned story keeps its own create-story
  [`ModuleContextTests.cs:170`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L170)
