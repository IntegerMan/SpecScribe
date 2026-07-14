---
title: 'Chronological commit pager direction + code-history date links'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Chronological commit pager direction + code-history date links

## Intent

**Problem:** The commit-day pages (`commits/{date}.html`) and per-commit pages (`commit/{hash}.html`) rendered their `EntityPager` Prev/Next in list-display order (newest-first), so "Next" pointed at an *older* day/commit — backwards from how a reader expects to walk forward in time. Separately, a code file's History tab listed each change's date as plain text with no way to jump to that day's commit-day page.

**Approach:** Flip Prev/Next to calendar direction (Prev = earlier, Next = later) for just these two chronological pager sites, leaving every other `EntityPager` family (epics, stories, ADRs, retros, code files) on its existing display-order rule. Add a guarded `dayHref` resolver to the code page's History tab so each row's Date cell links to its `commits/{date}.html` page when one was generated, plain text otherwise.

## Suggested Review Order

**Pager direction**

- Day pages now walk `days` (already ascending) directly instead of a reversed copy — Prev/Next fall out of the existing order for free.
  [`SiteGenerator.cs:861`](../../src/SpecScribe/SiteGenerator.cs#L861)

- Commit-detail pages keep their newest-first `slots` list as the source of truth and swap the raw pager's two sides, rather than maintaining a second reversed list in sync with it.
  [`SiteGenerator.cs:1087`](../../src/SpecScribe/SiteGenerator.cs#L1087)

- `EntityPager`'s doc comment now documents this as a named, scoped exception rather than an inviolable rule.
  [`EntityPager.cs:9`](../../src/SpecScribe/EntityPager.cs#L9)

**History tab date links**

- Date pages now generate before code pages (mirrors why commit-detail pages already generate first) so `_commitDays` is populated when a code page's History tab resolves a date link.
  [`SiteGenerator.cs:269`](../../src/SpecScribe/SiteGenerator.cs#L269)

- New guarded resolver: a date → its day page, output-relative, null when no page exists for that date.
  [`SiteGenerator.cs:1207`](../../src/SpecScribe/SiteGenerator.cs#L1207)

- The History tab's Date cell links through the resolver when present, plain text otherwise (mirrors the existing commit-hash cell).
  [`CodeFileTemplater.cs:299`](../../src/SpecScribe/CodeFileTemplater.cs#L299)

**Tests**

- New coverage: date-page pager direction, commit-detail pager direction, and the History tab's guarded date link.
  [`SiteGeneratorTimelineTests.cs:192`](../../tests/SpecScribe.Tests/SiteGeneratorTimelineTests.cs#L192)
  [`SiteGeneratorCommitDetailsTests.cs:121`](../../tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs#L121)
  [`CodeFileTemplaterTests.cs:318`](../../tests/SpecScribe.Tests/CodeFileTemplaterTests.cs#L318)
