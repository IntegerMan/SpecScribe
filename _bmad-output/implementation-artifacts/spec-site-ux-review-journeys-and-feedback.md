---
title: 'Site-wide UX review: user journeys and Epic 3 feedback docs'
type: 'chore'
created: '2026-07-09'
status: 'done'
route: 'one-shot'
---

# Site-wide UX review: user journeys and Epic 3 feedback docs

## Intent

**Problem:** The live portal (https://integerman.github.io/SpecScribe/) had never been reviewed holistically — page-by-page UX/consistency issues and the user needs the portal serves were undocumented, leaving Epic 3+ polish work without a grounding artifact.

**Approach:** Crawl the live site page by page (12 page types), define the personas and seven key user journeys the portal serves, then write app-wide + per-page UX feedback graded by severity and tied back to those journeys. Findings were adversarially reviewed against the live site and repo; four factually wrong claims (sprint toggle, story TOCs, epic-lane links, epic breadcrumbs — all already shipped) were struck and the priority list rebuilt.

## Suggested Review Order

1. [UserJourneys.md](../../docs/UserJourneys.md) — read first: personas and the seven journeys are the lens the feedback uses. Skim Journey priorities at the end.
2. [Epic3UXFeedback.md](../../docs/Epic3UXFeedback.md) — application-wide themes T1–T8 are the load-bearing findings; per-page sections follow; the Suggested priority order at the bottom is the proposed action sequence.
3. Cross-check: the scope note in Epic3UXFeedback.md's header records what was NOT reviewed (deferred-work, retros pages) and that all counts are a 2026-07-09 snapshot.
