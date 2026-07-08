---
title: 'Hide the code-review action for ready-for-dev stories'
type: 'bugfix'
created: '2026-07-07'
status: 'done'
route: 'one-shot'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A story page's Next Steps panel offered a `code-review` action even while the story was still `ready-for-dev` — before any implementation existed to review.

**Approach:** Drop the `code-review` suggestion from the `ready` branch of `BmadCommands.ForStory`, leaving only `dev-story`. `code-review` still appears once a story moves to `in-progress`, `review`, or `done`.

</frozen-after-approval>

## Suggested Review Order

- The fix itself: the `ready` branch no longer adds a `code-review` suggestion, with an inline comment explaining why.
  [`BmadCommands.cs:176`](../../src/SpecScribe/BmadCommands.cs#L176)

- Doc comment on `ForStory` updated so it no longer implies every branch carries a code-review suggestion.
  [`BmadCommands.cs:166`](../../src/SpecScribe/BmadCommands.cs#L166)

- Updated assertion: a `ready-for-dev` story's rendered Next Steps no longer contains `/bmad-code-review`.
  [`ModuleContextTests.cs:131`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L131)

