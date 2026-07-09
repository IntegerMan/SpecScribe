---
title: 'Fix HTML corruption in sprint board card tooltips'
type: 'bugfix'
created: '2026-07-09'
status: 'done'
route: 'one-shot'
---

# Fix HTML corruption in sprint board card tooltips

## Intent

**Problem:** The home dashboard's "Now & Next" sprint board rendered a phantom, garbled card in the Backlog column — raw tooltip text ("Epic 3: Insight Surfaces Story 3.4: ...") spilling into the page as visible content, ending in a stray `">`. Root cause: `StoryEpicLinkifier` rewrites "Epic N"/"Story N.M" mentions into `<a>` links across the whole rendered page, guarding only against double-linking inside existing `<a>…</a>`/`<code>`/`<pre>`/`<svg>`/`<head>`/`<script>`/`<style>` spans. A fallback sprint card with no matching story (e.g. Story 3.4, retired from the epics model) renders as `<div data-tip="Epic 3: …">` rather than an `<a>`, so the linkifier injected a raw `<a>…</a>` into the `data-tip` attribute value — the injected `</a>`'s `>` closed the `div` tag early, and the browser rendered the rest of the tag's raw markup as text.

**Approach:** Extend `StoryEpicLinkifier.ProtectedSplit`'s regex alternation with a generic `<[^>]*>` catch-all (ordered last, after the specific element-pair alternatives), so the linkifier only ever rewrites mentions inside real text nodes, never inside any tag's markup/attributes — regardless of which element it is.

## Suggested Review Order

- Regex fix: protect every standalone tag, not just the seven whitelisted element pairs, so a mention inside any attribute (not just `data-tip`) can never be rewritten.
  [`StoryEpicLinkifier.cs:25`](../../src/SpecScribe/StoryEpicLinkifier.cs#L25)

- Regression test pinning the exact reported failure mode: a mention inside a non-anchor tag's attribute value.
  [`LinkifierTests.cs:303`](../../tests/SpecScribe.Tests/LinkifierTests.cs#L303)

- Precedence test guarding that the new generic alternative doesn't shadow the existing `<a>…</a>` alternative when an anchor sits next to other markup.
  [`LinkifierTests.cs:315`](../../tests/SpecScribe.Tests/LinkifierTests.cs#L315)
