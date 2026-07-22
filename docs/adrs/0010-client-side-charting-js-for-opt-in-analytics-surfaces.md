# ADR 0010: Client-Side Charting JS for Opt-In Deep-Analytics Surfaces — Superseding "Pure SVG, No Charting JS" for These Pages, and Its Relationship to Epic 20

**Status:** Accepted (owner-ratified 2026-07-21, via correct-course on Story 7.11)
**Date:** 2026-07-21
**Deciders:** Matthew-Hope Eland
**Relates to:** Epic 20 (Interactive Explorer — Story 20.1's client-JS budget-setting spike); [ADR 0009 — Front-End Framework for the Projection Layer](0009-frontend-framework-for-projection-layer.md) (§ Consequences already anticipated "Epic 20's first client-JS surface"); memory `charting-is-pure-svg-no-js`

## Context

Since early in the project, SpecScribe's charts have followed a deliberate, memorialized rule: **pure inline SVG + CSS, no charting JS.** The only JS that ships is one small, sanctioned progressive-enhancement script (`specscribe.js`) covering tooltips, table sort/filter, clipboard, and send-menu dismissal — never a chart-rendering or chart-recoloring engine. This was a conscious divergence from an earlier, more JS-heavy UX drill-spec baseline (the dashboard's "Interactive Sunburst" drill-in was originally specified with client-side re-render; SpecScribe instead ships pure-CSS `:target`/`:checked`-driven interactions everywhere a "toggle" or "drill" was needed — e.g. the Code Map's colorize-dimension picker, the requirements flow/grid view switch, the Git Insights master-detail file→contributor panel).

Epic 20 ("Interactive Explorer") was seeded specifically to be the place this default gets revisited: its Story 20.1 is an explicit **budget-setting spike** — "SpecScribe's FIRST client-JS surface" — meant to decide a JS size budget and dependency posture (zero-dep vs. a small framework) before any surface commits to real client-side interactivity. As of this ADR, 20.1 is `ready-for-dev`, not done.

Independently, Story 7.11 (Code Ownership & Bus-Factor Insights) shipped as a plain ranked table and, on owner review, was redirected (via `correct-course`) toward a graphical sunburst with a mode-selector: dominant-author share % (sequential), top-N authors (discrete + "Others"), an **unbounded individual-author spotlight**, and a **configurable staleness threshold** (months since any current contributor last touched the file). An unbounded author picker and a free-form threshold cannot be fully pre-rendered server-side the way the Code Map's colorize-dimension picker is (that picker only ever swaps between a small, fixed set of dimensions) — they need a live client-side control that recolors the chart from embedded data. The owner confirmed this directly, and additionally noted Story 7.12 (Code Freshness / Age Map) is "also going this direction" — i.e., this is not a one-off exception for 7.11 alone, but a direction for this whole "opt-in deep-analytics" chart family.

This is exactly the kind of decision this project's own convention says should get an ADR rather than living as a story-embedded, owner-locked note (see project memory on the ADR-creation trigger gap) — it reverses a previously-ratified default and it materially overlaps with Epic 20's reason for existing.

## Decision

**Client-side charting JS is now permitted for opt-in, deep-analytics surfaces** — pages/sections that only exist when `--deep-git` (or an equivalent opt-in analysis flag) produced data, currently: the Git Insights hub (`git-insights.html`) and the Code Map's freshness/ownership colorize views (`code-map.html`). This supersedes the blanket "pure SVG + CSS, no charting JS" default **for these specific opt-in surfaces only**.

1. **Baseline/default-generation pages are unchanged.** The dashboard, epics, requirements, ADRs, and every page that renders without an opt-in flag stay exactly as they are today: zero-JS-required, per NFR-5's progressive-enhancement floor. This ADR does not touch them.
2. **NFR-5 still applies to the opt-in surfaces themselves, via a required no-JS baseline.** Every chart that gains live JS controls must still render a fully usable default view with JS disabled: a pre-rendered default color mode (e.g. dominant-author share %) plus the existing accessible text-equivalent table/list convention already used alongside every chart in this codebase. JS is additive (mode switching, the unbounded author picker, the staleness threshold) — it must never be the only way to reach information that exists.
3. **Generation-time determinism (FR31) is preserved by embedding, not fetching.** Whatever data a live control needs (per-file author breakdown and commit counts, last-active dates) is computed once at generation time and embedded in the page (bounded inline JSON or `data-*` attributes) — the client never re-derives it from live git state or wall-clock "now." A regeneration with the same repo state must still produce identical embedded data.
4. **FR-10's no-productivity-ranking constraint is unaffected by the rendering technology.** Author coloring/spotlighting stays descriptive attribution (share %, last-active date) — never a ranked leaderboard — regardless of whether it's server-rendered or JS-driven.
5. **This decision answers part of Epic 20 Story 20.1's spike question** ("is client-side charting JS acceptable at all") **but not all of it.** 20.1 should be re-scoped around its still-open unknowns for its own surface: the JS size/dependency budget shared across these analytics surfaces going forward, and the interactive drill-in/node-link exploration UX specific to Epic 20 (which is a different interaction pattern — graph exploration, not chart recoloring). 20.1 is not redundant, but it no longer needs to re-litigate whether JS is permitted.
6. **A shared home for this JS is a build-time decision, not decided here.** Whether the new charting JS lives in the existing sanctioned `specscribe.js` or a new opt-in `specscribe-analytics.js` (loaded only on these opt-in pages, so default-page JS payload is unaffected) is left to the implementing story/stories — but it must be ONE shared engine/module across 7.11, 7.12, and any future opt-in analytics surface, not independently reinvented per story.

## Consequences

**Positive**
- Unlocks controls a pre-rendered, bounded toggle genuinely can't offer (an unbounded author picker across however many contributors a repo has; a free-form staleness threshold) — directly serves the "see where complexity concentrates under one owner, and where nobody currently active has touched the code" intent behind Story 7.11.
- Unifies 7.11 and 7.12 under one JS charting approach instead of one going pre-rendered-CSS and the other going JS, which would have left the codebase with two divergent interaction models for near-identical chart families.
- Gives Epic 20 a concrete, already-decided precedent (the "is JS allowed" question) to build on rather than starting its spike from zero.

**Negative / trade-offs**
- This is the codebase's first real charting-JS dependency — it grows the surface that needs its own accessibility treatment (client-recolored charts need their `aria-label`/text-equivalent content kept in sync with whatever mode is active, which is genuinely new work; the existing "color is never the sole signal" convention now has a live-updating case to prove out, not just a static one).
- Golden-HTML-fingerprint testing (this codebase's primary regression net for chart output) covers only the server-rendered no-JS baseline going forward for these surfaces — the JS-driven states need their own test strategy (e.g. a browser-level or DOM-simulation test), which this project hasn't needed before.
- Introduces a shared-engine coordination cost across 7.11/7.12 (and later Epic 20) that a single-story implementation wouldn't have — whichever story lands first effectively designs the shared module the others build on.

## Ratified decisions (2026-07-21)
1. Client-side charting JS is **permitted** for opt-in deep-analytics surfaces (Git Insights, Code Map colorize views) — **not** for baseline/default pages.
2. NFR-5's no-JS-baseline requirement is **reinterpreted, not waived**, for these surfaces: a real, useful default-mode chart + full text equivalent must render with JS off; JS only adds live mode/spotlight/threshold controls on top.
3. FR31 (generation-time determinism) is preserved via generation-time-embedded, bounded data — never client-side re-derivation from live git state.
4. Epic 20 Story 20.1's scope is narrowed to its still-open unknowns (shared JS budget, drill-in/exploration UX) — the "is JS allowed" question this ADR answers is no longer part of its spike.
5. 7.11, 7.12, and future opt-in analytics surfaces share **one** JS charting engine/module — not independently reinvented per story.

## References
- **Superseded default:** memory `charting-is-pure-svg-no-js` ("Charts are pure SVG + links, no JS — deliberate divergence from UX drill spec").
- **The epic this partially resolves:** Epic 20 (Interactive Explorer), Story 20.1 (client-JS budget-setting spike) — see `_bmad-output/planning-artifacts/epics.md` and memory `story-20-1-interactive-explorer-spike-seeded`.
- **Anticipated by:** [ADR 0009](0009-frontend-framework-for-projection-layer.md) § Consequences ("Epic 20's first client-JS surface").
- **Triggering stories:** Story 7.11 (Code Ownership & Bus-Factor Insights, re-scoped by this correct-course), Story 7.12 (Code Freshness / Age Map, noted by the owner as heading the same direction).
- **PRD constraints this does not relax:** FR-10 (no per-author productivity ranking/leaderboard); NFR-5 (progressive enhancement); FR31 (generation-time determinism).
