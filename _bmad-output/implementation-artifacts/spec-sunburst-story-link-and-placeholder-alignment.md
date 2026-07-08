---
title: 'Sunburst undrafted-story links and placeholder page alignment fix'
type: 'bugfix'
created: '2026-07-08'
status: 'done'
route: 'one-shot'
review_loop_iteration: 0
context: []
---

# Sunburst undrafted-story links and placeholder page alignment fix

## Intent

**Problem:** The project-wide sunburst chart's story segments linked undrafted (not-yet-built) stories to their epic page instead of the story's own placeholder detail page. Separately, that placeholder page's "← Back to Epic" link sat outside the page's 860px-centered content column, rendering flush against the far-left edge instead of aligning under the header/note/AC panel above it.

**Approach:** Point the sunburst's undrafted-story fallback href at `StoryEpicLinkifier.StoryPagePath(story.Id)` — the same path `SiteGenerator` already writes a placeholder page to — instead of the epic page. Wrap the placeholder page's back-link in its own `.dashboard-narrow` section, matching how the AC panel above it is already wrapped.

## Suggested Review Order

**Sunburst link fix**

- The one-line fallback that sent undrafted-story segments to the wrong page; now resolves to the always-generated placeholder page.
  [`Charts.cs:174`](../../src/SpecScribe/Charts.cs#L174)

- New regression test asserting the sunburst links an undrafted story to its placeholder page, not the epic page.
  [`ChartsTests.cs:61`](../../tests/SpecScribe.Tests/ChartsTests.cs#L61)

**Placeholder page alignment fix**

- The back-link now wrapped in `.dashboard-narrow` so it lines up with the rest of the page's centered content instead of sitting at the raw `<main>` edge.
  [`EpicsTemplater.cs:385`](../../src/SpecScribe/EpicsTemplater.cs#L385)

- Extended existing alignment test to also assert the back-link's wrapper.
  [`HtmlTemplaterTests.cs:346`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L346)
