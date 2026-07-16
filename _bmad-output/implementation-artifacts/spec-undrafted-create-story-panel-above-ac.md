---
title: 'Undrafted create-story panel above AC'
type: 'feature'
created: '2026-07-16'
status: 'done'
route: 'one-shot'
---

# Undrafted create-story panel above AC

## Intent

**Problem:** On not-yet-drafted story placeholder pages, the create-story command panel sat below the Acceptance Criteria panel, so the primary next action was buried under criteria content.

**Approach:** Render the create-story pending-note panel above the Acceptance Criteria panel on story placeholder pages so the draft action is visible first.

## Suggested Review Order

**Placeholder layout**

- Create-story note now emits before the AC panel on undrafted story pages.
  [`HtmlRenderAdapter.Epics.cs:554`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L554)

**Regression pin**

- Unit test asserts note-before-AC order and that the note is not nested inside the AC list.
  [`HtmlRenderAdapterTests.cs:167`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L167)
