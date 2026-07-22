# Sprint Change Proposal — Git Insights Ownership View Redesign

**Date:** 2026-07-21
**Deciders:** Matthew-Hope Eland
**Trigger story:** Story 7.11 (Code Ownership & Bus-Factor Insights), shipped to "review" earlier the same day
**Mode:** Incremental (three rounds of design-direction clarification with the owner before finalizing)

## 1. Issue Summary

Story 7.11 was implemented exactly as specified — a ranked HTML table on the Git Insights hub showing each
file's dominant-author share, contributor count, and a "Sole contributor:" bus-factor flag — and passed its full
test suite. On review, the owner found the result "underwhelming": narrow, long, and not graphical, for a
question ("where does complexity concentrate under one owner, where has no active contributor touched the
code") that is inherently spatial. The owner's message arrived in two parts: an initial ask that included giving
the feature its own page/nav entry, then an explicit amendment retracting that and confirming it should stay on
the existing Git Insights hub, replacing not just the new table but also the pre-existing "Files & Contributors"
master-detail table on the same page.

This is a **new/clarified requirement surfaced immediately after implementation**, not a technical failure or a
misunderstanding of the original AC — the original AC was met; the display choice it specified turned out not to
serve the underlying need.

## 2. Impact Analysis

**Epic Impact:** None. Story 7.11 stays inside Epic 7 (Code and Git Exploration); no epic is added, removed, or
resequenced.

**Story Impact:**
- **Story 7.11** — re-scoped wholesale (see Section 4, and the rewritten story file itself). Its prior
  implementation (still uncommitted in the working tree) is superseded; Task 0 of the rewrite removes it.
- **Story 7.12** (Code Freshness / Age Map, done/review) — not modified by this proposal, but its
  `Charts.CodeFreshnessSunburst` engine is the direct reuse target for 7.11's new chart, and the owner separately
  noted 7.12 is "also going" toward the same live-JS direction. Flagged as a likely, but not yet actioned,
  symmetrical follow-on story.
- **Story 20.1** (Interactive Explorer architecture spike, ready-for-dev) — its scope narrows. It no longer needs
  to decide "is client-side charting JS acceptable at all" (this proposal's ADR settles that for opt-in analytics
  surfaces); it retains its own still-open unknowns (shared JS budget, drill-in/exploration UX).

**Artifact Conflicts:**
- **PRD FR-10** ("no per-author productivity ranking or leaderboard") — no conflict, but now an explicit AC (#6)
  on the rewritten story, since a richer interactive surface makes it easier to accidentally drift toward a
  ranking without noticing.
- **PRD NFR-5** (progressive enhancement, JS-off baseline) — reinterpreted, not violated, for this one surface
  family: the new ADR requires a real, useful no-JS default rendering plus a full text-equivalent, with JS strictly
  additive. Baseline/default-generation pages are untouched.
- **PRD FR31** (generation-time determinism) — preserved via generation-time-embedded, bounded data; the ADR
  makes this explicit for both the server and client halves of the new design.
- **Architecture:** a new ADR (0010) is added — see Section 3. No existing ADR is invalidated; ADR 0009 already
  anticipated "Epic 20's first client-JS surface" in its Consequences section, so this isn't a surprise to the
  architecture record, just an earlier and different trigger than expected.
- **UI/UX specs:** `DESIGN.md`/`EXPERIENCE.md`'s "Interactive Sunburst" section describes the dashboard's
  epic/story delivery sunburst (a fixed 3-ring model), not this code-tree sunburst — no conflict, no edit needed.
  This story's chart is a sibling to Story 7.12's, both outside that original UX baseline (which predates Epic 7's
  git-insights features entirely).
- **Other artifacts:** no CI/deployment/IaC impact. Testing strategy gains one new open question (Task 6.4 of the
  rewritten story): this codebase has no browser/DOM-execution test harness, and the new client-JS recolor logic
  needs one, or an explicit documented gap.

## 3. Recommended Approach

**Selected: Option 1, Direct Adjustment — amend Story 7.11 in place, plus one new ADR.**

Story 7.11 was still `review`, not `done`, with no downstream consumers of its shipped shape — amending it in
place (rather than superseding it with a new story ID, or rolling back to some earlier state) is the cleanest
path. A rollback isn't warranted: nothing about the *prior* implementation was wrong or broken, it was simply the
wrong design for the actual need, and the fastest way to correct that is to rewrite the one story that owns it.
MVP scope is unaffected — this is a richer version of an already-in-scope insight surface, not new PRD scope.

The one genuinely architectural piece — permitting client-side charting JS for this surface family, which reverses
a long-standing project default and materially overlaps with Epic 20's reason for existing — is handled via a new
ADR (0010) rather than being buried as a story-embedded note, per this project's own established convention
(agents should proactively propose an ADR for architecture/cross-cutting decisions).

**Effort estimate:** Medium (was Low for the original 7.11 scope) — a full-tree per-file author data model
extension, a generalized/reused sunburst engine, and a new client-side JS control layer, versus the original's
single new HTML section over already-computed data.
**Risk level:** Low-Medium — no rollback risk (nothing shipped to users yet), but this is the codebase's first
real charting-JS surface, so its test-coverage story is genuinely new territory (flagged explicitly in the
rewritten story's Task 6.4 rather than glossed over).

## 4. Detailed Change Proposals

### Story 7.11 (full rewrite)

See [7-11-code-ownership-and-bus-factor-insights.md](../implementation-artifacts/7-11-code-ownership-and-bus-factor-insights.md)
for the complete rewritten Acceptance Criteria, Tasks/Subtasks, and Dev Notes. Summary of the shape change:

| | Before (shipped to review) | After (this proposal) |
|---|---|---|
| Display | Plain ranked HTML table | Interactive sunburst over the full source tree |
| Data scope | Top-N files only (`GitInsightsData.Files`, capped) | Every file (extends the uncapped `BuildCodeMapMetrics` accumulator) |
| Color modes | None (one static table) | Share % (sequential), top-N authors (discrete + Others), individual-author spotlight (unbounded), staleness (configurable threshold) |
| Also replaces | — | The pre-existing "Files & Contributors" master-detail table |
| JS | None | Live mode/spotlight/threshold controls (ADR 0010), with a required no-JS baseline |
| Chart engine | New table-rendering code | Reuses/generalizes Story 7.12's `Charts.CodeFreshnessSunburst` |

### New ADR 0010

See [0010-client-side-charting-js-for-opt-in-analytics-surfaces.md](../../docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md).
Rationale: permits client-side charting JS, but **only** for opt-in deep-analytics pages (Git Insights, Code Map
colorize views) — baseline/default pages stay zero-JS. Requires a real no-JS default-mode-plus-text-equivalent
baseline per surface (NFR-5 reinterpreted, not waived). Requires generation-time-embedded, bounded data (FR31
preserved). Narrows Epic 20 Story 20.1's spike to its remaining open unknowns. Requires one shared JS engine
across 7.11/7.12/future opt-in analytics surfaces, not independent reinvention per story.

### `docs/adrs/README.md`

Added the ADR 0010 index entry (one line, same format as 0001–0009).

### `sprint-status.yaml`

- `7-11-code-ownership-and-bus-factor-insights`: `review` → `ready-for-dev`, with a note explaining the re-scope
  and pointing at this proposal document and the story's own Change Log for full history.
- `20-1-interactive-explorer-architecture-spike`: unchanged status (`ready-for-dev`), annotated with a note that
  its spike scope has narrowed per ADR 0010.
- No epic entries added, removed, or renumbered (Section 2's finding: no epic impact).

### `epics.md`

**No change.** Story 7.11's epic-level AC language in `epics.md` is now looser/less specific than the story
file's rewritten AC — this is normal (epics.md sets outcome-level intent; story files carry the detailed,
current-truth AC) and does not need editing for this change, consistent with Section 2's "no epic-level change
needed" finding.

## 5. Implementation Handoff

**Scope classification: Moderate**, bordering on Major for the ADR piece specifically. The ADR decision itself
was owner-ratified live in this session (via the AskUserQuestion rounds), so the "PM/Architect" side of this
change is already resolved — what remains is Developer-agent work:

- **Developer agent (`bmad-dev-story`)**: execute the rewritten Story 7.11 top to bottom, starting with Task 0
  (remove the superseded implementation). Flag Task 6.4 (JS test-coverage strategy) back to the owner if a clear
  default isn't obvious once the client-JS design is concrete, rather than silently shipping untested JS or
  silently inventing a test harness the project hasn't used before.
- **No Product Owner backlog reorg needed** beyond the `sprint-status.yaml` edits already made as part of this
  proposal (Section 4).
- **Follow-up, not part of this handoff:** a symmetrical Story 7.12 JS-controls pass (owner-flagged, not yet
  scoped or approved) and Story 20.1's narrowed spike (unblocked by ADR 0010, but its own separate piece of work).

**Success criteria:** Story 7.11's rewritten ACs all satisfied, full test suite green (including the new
per-file-author-data and sunburst-geometry tests), golden content fingerprint regenerated and stable, and — per
AC #3 — the page verified usable with JavaScript disabled before the story is marked `review` again.
