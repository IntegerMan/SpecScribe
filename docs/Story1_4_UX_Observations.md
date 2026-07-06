# Story 1.4 UX Observations — SpecScribe Generated Site Review

**Reviewed:** https://integerman.github.io/SpecScribe/index.html (live) plus the local `docs/live` build, `specscribe.css`, generator source (`Charts.cs`, `HtmlTemplater.cs`), and the current Story 1.4 spec.
**Date:** 2026-07-06
**Purpose:** Candidate scope expansions for Story 1.4 (Accessible High-Polish Interaction Baseline), focused on insight clarity ("where things stand, what's left, what's next, how the work has gone"), active effects, tooltips, charts, and color/accent polish.

Each item has an ID, an effort estimate (S/M/L), and a suggested disposition:
**Fix now** (bug, do regardless of story), **1.4** (fits this story's scope), **Epic 3** (belongs to Insight Surfaces / Story 3.1 Git Pulse / Story 3.5 Visual Language), or **Backlog**.

---

## A. Data correctness & trust (fix before polish — a dashboard that's wrong can't be polished)

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| A1 | **The live site's git stats are wrong.** Live shows "1 Commits / 1 active day" and a nearly-empty heatmap; the real numbers are 37 commits / 3 active days. Cause: `actions/checkout@v4` in `publish-docs-live-pages.yml` defaults to a shallow clone (`fetch-depth: 1`), so the generator only sees one commit. | Add `with: fetch-depth: 0` to the Checkout step. | S | **Fix now** |
| A2 | **"1 Commits" pluralization.** The stat-card label is hardcoded `"Commits"` (`HtmlTemplater.cs:128`). The heatmap tooltips and "active day(s)" already pluralize; the headline stat doesn't. | Pluralize the label (`Commit`/`Commits`), same for any other count-bearing labels ("1 stories", etc. — audit all StatCard callers). | S | **Fix now** |
| A3 | **The "Epic Status" donut contradicts the sunburst.** The donut reports all 5 epics as "Drafted" (legend: Drafted/Pending only) while the sunburst correctly shows Epic 1 as "In development" with two stories done. Two panels on the same page disagree about the flagship epic. | Derive epic status for the donut from the same story-roll-up logic the sunburst uses (done/in-development/drafted/pending), not from the epics file's draft flag. | M | **Fix now** |
| A4 | **The heatmap renders future days as "0 commits."** Cells for 2026-07-07 → 07-11 look identical to real zero-commit days. GitHub greys out / omits future days. | Skip or visually mute (e.g., no fill, no tooltip) days after the generation date. | S | 1.4 |
| A5 | **"64/103 Tasks done" over-promises.** The denominator only covers the 4 stories that have task plans (of 18), so the headline reads as "62% of the project is done." The `stat-sub` caveat helps but the big number dominates. | Reframe: "64/103 planned tasks" as the number, or pair it with a second stat ("4/18 stories planned"). Consider a stacked overall bar: done / planned-remaining / unplanned-estimated. | S | 1.4 |
| A6 | **"Progress by Epic" doesn't measure progress.** The mosaic donuts show *stories detailed*, so Epic 1 renders a 100% full gold ring while mid-development, and the center number is a story count that reads like a score. At a glance every epic looks either "complete" (full ring) or "complete-in-grey." | Make the ring show delivery status per story (done/active/ready/drafted segments, same palette as the sunburst) and keep "N/N detailed" as the sub-label only. | M | 1.4 |
| A7 | **Footer timestamp is bare.** "Generated … on 2026-07-06 11:32" — no timezone, no commit context. | Add timezone (or use ISO-8601), and ideally the short SHA the site was built from, linked to the commit. | S | Backlog |

## B. Status vocabulary & color consistency (accents/colors ask)

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| B1 | **Four vocabularies for "not started."** "Pending" (epic donut, sunburst legend), "Planned" (requirements), "Not yet detailed" (mosaic tooltips), "Drafted" (epic status). Readers must re-learn the taxonomy per panel. | Adopt the sunburst legend's six-stage lifecycle (Pending → Drafted → Ready for dev → In development → In review → Done) as the single vocabulary everywhere; requirements can alias "Planned" → "Pending." | M | 1.4 |
| B2 | **Color collision between Ready and Drafted.** In donuts, `.donut-seg.ready` and `.donut-seg.drafted` are both `--gold-light`, but the sunburst distinguishes them (`#e8d9a8` pale gold for drafted vs `--gold-light` for ready). Same status, different color per chart; two statuses, same color within one chart. | Give each lifecycle stage exactly one color token (`--status-drafted`, `--status-ready`, …) used by every chart, legend, badge, and accent bar. The CSS comment at line 515 already states this ambition ("Status semantics everywhere") — enforce it. | M | 1.4 |
| B3 | **"Pending" grey differs per chart.** Sunburst pending = `#b8b2a8`; donut pending swatch = `--parchment-deep` (#d4b896). | Same fix as B2 — tokenize. | S | 1.4 |
| B4 | **Zero-count legend rows add noise.** Requirements donuts list "Done (0), Ready (0), Deferred (0)" rows; the NFR donut is a single-color ring with a 4-row legend for 1 real category. | Suppress zero-count legend entries (or render them collapsed/dimmed). | S | 1.4 |
| B5 | **Quick-link cards are visually undifferentiated.** All 9 "Explore Key Views" cards share the same gold accent. | Accent by artifact family (planning = gold, architecture = teal, epics/stories = moss, requirements = rust) to build color literacy that pays off across the site. | S | 1.4 |

## C. Tooltips (the single biggest polish lever)

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| C1 | **Every chart tooltip is a native SVG `<title>`.** That means: ~1 second hover delay, unstyled OS-default appearance, no touch support at all, and inconsistency with the site's otherwise curated parchment aesthetic. The sunburst — the hero visual — hides all of its labels behind this. | Add a shared, on-brand tooltip: styled parchment card, instant on hover/focus, positioned near the pointer. HTML elements can do this CSS-only (`data-tooltip` + `::after`). SVG segments need a small shared script (~30 lines, no deps, progressive enhancement — keep `<title>` + `aria-label` as the no-JS fallback). ⚠️ The 1.4 spec's "no new JS interaction engine" guardrail should be consciously relaxed to permit *this one enhancement*; it is not the drill engine the guardrail targets. | M | **1.4 (flagship item)** |
| C2 | **Stat cards explain nothing.** "3 active days" (since when?), "4 with a task plan" (what's a task plan?), "Tasks done" (tasks per BMad story checklists). UX-DR4 already envisions stat tooltips. | Give each stat card a `data-tooltip` definition (CSS-only, instant, focus-accessible). | S | 1.4 |
| C3 | **Legends are inert.** Hovering "In development" in the sunburst legend does nothing. | CSS-only option: none (needs JS). With the C1 script present: hovering a legend item dims non-matching segments. Cheap once C1 lands. | S | Epic 3 / with C1 |
| C4 | **Touch users currently get zero chart detail.** `<title>` never fires on touch; segments navigate on first tap. | C1's tooltip script fixes this (first tap = tooltip, second = navigate, or long-press). At minimum, `aria-label`s (already in 1.4 scope) give screen-reader access. | — | with C1 |

## D. Active effects & micro-interactions

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| D1 | **Inconsistent hover grammar.** Index/quick-link/mosaic cards lift + shadow; now-next cards shadow only (no lift); stat cards are inert; donut segments and heatmap cells have no hover at all; sunburst segments only fade to 75% opacity. | Define one interaction grammar: *interactive → lift + accent-border + shadow; data-hover → highlight + tooltip; static → nothing.* Apply uniformly (now-next cards get the lift; donut segments get a stroke-width bump on hover like the sunburst's planned focus treatment). | S | 1.4 |
| D2 | **No entrance animation anywhere.** Progress bars are set to their final width inline; donuts and sunburst pop in fully drawn. A one-time draw-in (bar width 0→N%, donut `stroke-dashoffset` sweep) is the cheapest "high-polish" signal there is — pure CSS `@keyframes`, no JS. | Add load animations, **gated behind `@media (prefers-reduced-motion: no-preference)`** — this dovetails exactly with the reduced-motion block 1.4 already builds (AC #2). | S | 1.4 |
| D3 | **Sunburst hover is weak.** Opacity 0.75 fade reads as *dimming* the thing you're pointing at — backwards. | Invert: dim the *siblings*, emphasize the hovered segment (brighter fill or 2px `--gold-light` stroke — same treatment the 1.4 spec plans for `:focus-visible`, so hover and focus match). Sibling-dimming needs `.sunburst:hover .sb-seg { opacity: .5 }` + `.sb-seg:hover { opacity: 1 }` — CSS only. | S | 1.4 |
| D4 | **The "Now" card doesn't draw the eye.** The single most important answer on the page ("what's happening right now") is styled identically to its neighbors except for border color. | Give the in-development card slightly stronger treatment: tinted background wash, bolder accent, or (reduced-motion permitting) a very subtle pulsing border. | S | 1.4 |
| D5 | **Heatmap cells are inert.** No hover ring, tooltip is native-only. | Hover stroke + C1 tooltip. | S | with C1 |

## E. Charts

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| E1 | **The commit heatmap is a postage stamp.** Fixed `width="138" height="114"` inside a full-width panel — 11px cells, 8px labels. It's the primary "how has the work gone" visual and it's the smallest thing on the page. | Let it scale: `width: 100%; max-width: 420px; height: auto` (viewBox already exists), render more weeks (12–16), and add a headline ("37 commits · 3 active days · last commit 2026-07-06"). | S | 1.4 |
| E2 | **Single-value donuts carry no information.** "Epic Status: Drafted 5/5" and "Non-functional: Planned 7/7" are solid single-color rings — a chart form that only earns its space when there are ≥2 segments. | After A3 fixes the epic-status data, the epic donut becomes genuinely multi-segment. For requirements, consider compact stacked bars (one row per group) instead of two donuts — denser and comparable at a glance. | M | 1.4 / Epic 3 |
| E3 | **Donut center numbers read as scores but are totals.** "14" in the center of a mostly-grey requirements ring initially reads as "14 done." | Center the *done/total* fraction ("4/14") or a percentage; or move the total to the label. | S | 1.4 |
| E4 | **Sunburst outer-ring absence is ambiguous.** Only stories with task plans get an outer task arc; a first-time reader can't distinguish "no tasks planned" from "chart ran out of data." The italic hint helps but is below the fold of the chart. | Render a faint dashed placeholder arc for unplanned stories with tooltip "No task plan yet — run /bmad-create-story N.N." That also turns the gap into a call to action. | M | 1.4 |
| E5 | **No trend/velocity view — the "how has the work gone" question is only half answered.** The heatmap shows *when* commits happened, but nothing shows tasks/stories completing over time or pace. | A small "tasks completed per day" sparkline or cumulative burn-up would complete the story. This is squarely Story 3.1 (Baseline Git Pulse Insights) territory — don't pull into 1.4. | L | **Epic 3** |
| E6 | **Charts lack accessible names except the sunburst.** The donut SVGs and heatmap SVG have no `role="img"`/`aria-label` (the sunburst has both). The 1.4 spec covers segment `aria-label`s but should also cover whole-chart labels for the donuts/heatmap ("Commit activity: 37 commits across 3 active days, May 17–Jul 11"). | Add to 1.4 Task 2. | S | **1.4 (gap in current spec)** |

## F. Information architecture & "what's next" storytelling

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| F1 | **Now & Next is buried third.** The page's most valuable panel (what's active, what's next, what to draft) sits below a 9-card link grid. "Explore Key Views" duplicates the top nav *and* the index-card sections at the bottom — the same links exist in three places on one page. | Reorder: stats → Now & Next → Project at a Glance → progress panels; demote or slim "Explore Key Views" (it can become a compact single row of pills). | S | 1.4 |
| F2 | **Next Steps commands can't be copied in one click.** `/bmad-dev-story 1.3` is exactly the thing a user wants on their clipboard. | Add a copy button per command (tiny shared JS, static-safe; same progressive-enhancement budget as C1). Also fix the grammar: "implements it per its plan" → "implement it per its plan." | S | 1.4 |
| F3 | **No recency signal above the fold.** "Where do things stand" includes *when anything last happened*; that's only discoverable in the footer or by reading heatmap tooltips. | Add "last commit N days ago" to the Commits stat-sub, and/or "updated today" chips on the Now & Next cards. | S | 1.4 / Epic 3 |
| F4 | **Story 1.4's own page will be the proof.** Once 1.4 ships, the dashboard is the demo of the dashboard. Worth a final pass comparing each panel against the four questions: standing / left / next / how it's gone. | Use as the story's definition-of-polish checklist. | — | 1.4 |

## G. Platform & meta polish

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| G1 | **No favicon.** Browser default document icon in every tab; 404 for `/favicon.ico`. | Emit a small inline-SVG favicon (parchment quill/spark mark, gold on dark) from the generator. | S | 1.4 |
| G2 | **Home page `<title>` is just "SpecScribe"** (sub-pages are properly suffixed). No `<meta name="description">`, no Open Graph/Twitter tags anywhere — shared links render bare. | "SpecScribe — Project Dashboard" + description + minimal OG tags (title/description/type). | S | 1.4 |
| G3 | **No `404.html`.** GitHub Pages shows its generic 404 for stale/mistyped links. | Generate a themed 404 with links home. | S | Backlog |
| G4 | **No print styles.** PRDs/specs/ADRs are exactly the documents people print or PDF. | `@media print`: hide nav/TOC, white background, black ink. | S | Backlog |

## H. Accessibility findings beyond the current 1.4 spec

The spec already covers focus-visible, segment `aria-label`s, reduced motion, skip link, `<main>` landmark, and progressbar ARIA. Additional gaps found in this review:

| ID | Observation | Recommendation | Effort | Disposition |
|----|-------------|----------------|--------|-------------|
| H1 | **`.index-card-path` fails contrast catastrophically.** `#d4b896` on `#faf7f2` ≈ 1.6:1 (WCAG minimum is 4.5:1). Verified via computed styles. | Darken to at least `--ink-light` (#7a6250). | S | **1.4 (add to spec)** |
| H2 | **Tiny text in charts.** Heatmap day/month labels are 8px; `.sunburst-hint` is 0.72rem italic on parchment. | Bump heatmap labels with the E1 rescale; hint to 0.78rem non-italic. | S | 1.4 |
| H3 | **Whole-chart accessible names missing on donuts/heatmap** (see E6). | Add `role="img"` + `aria-label`. | S | 1.4 |

---

## Suggested Story 1.4 scope expansion (summary)

**Pull into 1.4** (keeps the story's "high-polish interaction baseline" identity, all S/M):
1. On-brand instant tooltip system replacing native `<title>`-only behavior (C1, C2, C4, D5) — *the flagship addition; requires consciously permitting one small progressive-enhancement script.*
2. Unified status vocabulary + one-color-per-status token system across all charts/badges/accents (B1–B3), plus zero-row legend suppression (B4).
3. Interaction grammar: consistent hover lift/accent everywhere, sunburst sibling-dim hover matching the focus treatment, "Now" card emphasis (D1, D3, D4).
4. Load animations for bars/donuts gated on `prefers-reduced-motion` (D2) — natural companion to AC #2.
5. Chart truthfulness: epic-status roll-up (A3), "Progress by Epic" showing real delivery status (A6), task-stat reframing (A5), heatmap future-day muting (A4), heatmap rescale + headline (E1), donut center fractions (E3).
6. Dashboard ordering: Now & Next promoted, Explore Key Views slimmed (F1); copy buttons + grammar fix on Next Steps (F2).
7. Accessibility additions the spec missed: `.index-card-path` contrast (H1), chart-level `aria-label`s on donuts/heatmap (E6/H3), chart text sizes (H2).
8. Head polish: favicon, home title, meta/OG tags (G1, G2).

**Fix immediately regardless of story** (data correctness): shallow-clone git stats in CI (A1), "1 Commits" pluralization (A2).

**Explicitly keep out of 1.4** (respect existing guardrails): the JS drill/zoom sunburst engine, dark mode/theme toggle (both already scoped out in the spec), velocity/burn-up charts and deep git analytics (Story 3.1), interactive legends and richer insight visuals (Story 3.5).
