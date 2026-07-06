---
title: 'Deployed dashboard reflects full git history in commit activity'
type: 'bugfix'
created: '2026-07-06'
status: 'done'
route: 'one-shot'
review_loop_iteration: 0
context: []
---

## Intent

**Problem:** The deployed GitHub Pages dashboard shows only a single commit in its "Commits" stat card and one lone cell in the Commit Activity heatmap, contradicting GitHub's own activity graph (38 commits). The CI build checks out the repo with `actions/checkout@v4` at its default shallow depth (`fetch-depth: 1`), so `GitMetrics` sees only the tip commit — one day (Mon, 2026-07-06), which is exactly the single dark cell users observe.

**Approach:** Set `fetch-depth: 0` on the checkout step so CI clones the full history, letting the dashboard's git metrics reflect the real project timeline. Empirically verified: a `--depth 1` clone yields `rev-list --count HEAD == 1` (single day), a full clone yields `38` across three days — matching GitHub.

## Suggested Review Order

- CI checkout now fetches full history so dashboard git metrics stop truncating to the tip commit.
  [`publish-docs-live-pages.yml:34`](../../.github/workflows/publish-docs-live-pages.yml#L34)
