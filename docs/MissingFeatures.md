# SpecScribe Missing Features

Date: 2026-07-09
Sources: [Epic3UXFeedback.md](Epic3UXFeedback.md) (page-by-page portal review) and [UserJourneys.md](UserJourneys.md) (the seven journeys the portal exists to serve).

Where the feedback doc catalogs *defects in what exists*, this doc extracts the *capabilities that don't exist at all* — features whose absence leaves a journey unfinished. Each entry names the journey it unblocks and its coverage status against the current roadmap (epics 1–7) and the specs in `_bmad-output/implementation-artifacts/`:

- **NET-NEW** — no story or spec covers this today.
- **EXTENDS** — an existing story/spec covers part of it; the named gap remains.
- **ROADMAPPED** — already planned; listed only so this doc reads as a complete gap map.

---

## A. Traceability & status (Journeys 1, 2, 4 — the daily journeys and the differentiator)

### A1. Canonical status model with a portal-wide legend — NET-NEW
Three overlapping status vocabularies coexist (sprint lifecycle, epic/story authoring states, requirements states) with no published mapping between them (feedback T1). The missing feature is a single canonical lifecycle per entity type (requirement / epic / story), a documented mapping between them, every badge routed through the existing `--status-*` token system, and a status-legend popover reachable from any badge. Journeys 1, 2, and 4 all stall on vocabulary reconciliation today.

### A2. Story-level links on FR/NFR detail pages — NET-NEW
FR detail pages stop at the epic hop: no covering stories, no ACs, no code references (feedback, Requirements section). This is the weakest link in Journey 4 — the PRD → requirement → epic → story → code chain that is SpecScribe's core differentiator. The coverage-map data already exists in the generator; it just isn't projected onto requirement pages. Epic 7 (Story 7.2) will add the *code* end of the chain, but nothing roadmapped adds the *story* hop.

### A3. NFR and UX-DR coverage maps — NET-NEW
The epics page has an FR coverage map but no NFR or UX design-requirement counterpart, and the requirements page shows all seven NFRs as an undifferentiated "Planned" (feedback, Epics + Requirements sections). Missing: parallel (or combined) coverage maps, plus per-NFR verification statements when no implementation state exists yet. Journey 4's needs explicitly call for NFRs "traced with the same rigor as FRs."

### A4. "Deferred on purpose" vs "unmapped" distinction in coverage — NET-NEW
The requirements-flow diagram's "No coverage" state merges two very different situations: deliberately deferred and nobody-mapped-it. Missing: a split state (and matching visual treatment) so a Stakeholder reading Journey 4 doesn't misread intentional scope decisions as gaps.

### A5. Single authoritative count source — NET-NEW (data-integrity feature)
Home says "38 Stories defined" while its own sprint block sums to 39 (feedback T2). Missing: one generator-side source of truth for entity counts that every widget consumes, so summary counts and detail views cannot disagree. Journey 1's stated failure mode is exactly this reconciliation burden.

---

## B. Work selection & review (Journeys 2, 3 — daily and per-story)

### B1. State-aware next-step commands, including the `done` state — EXTENDS
`spec-hide-code-review-button-ready-for-dev`, `spec-story-next-steps-review-command`, and `spec-home-next-steps-label-and-code-review` cover parts of this, but the gap observed live is the **done** state: a completed story still surfaces `/bmad-code-review` as the next step, implying review never happened. Missing: exactly one recommended command per lifecycle state (Journey 2's stated need), with done mapping to retro or "no action needed."

### B2. Verification-evidence strip on story pages — NET-NEW
Test counts and completion claims live deep in the Dev Agent Record. Missing: a compact strip near the status badge — e.g. "5/5 tasks · 586 tests green · verified 2026-07-09" — so the Reviewer's first glance (Journey 3) answers "does the claim have evidence?" without scrolling a 6,700-word page.

### B3. Visually distinct AC blocks — EXTENDS
Journey 3 depends on diffing the *contract* (ACs) against the *claim* (dev record). ACs currently blend into surrounding prose. `spec-ac-panel-and-story-card-polish` overlaps here — audit what it shipped before scoping; the remaining feature is a bordered/tinted AC block via existing tokens, plus default-collapsed Dev Notes on the longest pages.

### B4. Readiness explanation affordances — NET-NEW
"Backlog" and "Ready for dev" both read as "not started" to anyone but the Driver. Missing: column-header tooltips on the sprint board ("Ready = task plan exists and dependencies met") and clear separation of planless stories from actionable ones — Journey 2's "unambiguous ready signals."

---

## C. Insight surfaces (Journey 6)

### C1. "Insights" top-nav entry — NET-NEW
Git Insights, Deep Analytics, Action Items, and Deferred Work are reachable only via dashboard deep links (feedback T3). Journeys 6 and 7 have no stable entry point from interior pages. Missing: an Insights nav item grouping git-insights + deep-analytics, and Action Items / Deferred Work seated under Sprint or a "Follow-ups" entry. Companion change: remove or redirect the retired Structure page's nav slot (its replacement, the source treemap, is ROADMAPPED as Story 7.6).

### C2. Chart metadata standard: legend + time window + framing sentence — EXTENDS
Every chart should carry a legend with real values, the analysis window ("recent history" is not a number), and one sentence of "why this matters" (churn ≈ defect risk; unexpected coupling ≈ hidden dependency). `spec-commit-heatmap-contrast-and-day-drilldown` covers heatmap contrast only; the missing feature is the portal-wide standard. This is the difference between Journey 6 delivering insight versus trivia.

### C3. Process-coupling vs code-coupling annotation — NET-NEW
Deep Analytics' top coupled pair (`sprint-status.yaml ↔ specscribe.css`, 29×) is an artifact of committing generated/status files alongside code. Missing: a classification or note distinguishing process-coupling from code-coupling so readers don't draw wrong architectural conclusions.

### C4. Dead-zone handling on the activity heatmap — NET-NEW
Months of empty cells before the first commit render as ambiguous emptiness. Missing: a project-start annotation ("First commit Jul 4") or window trimming.

---

## D. Onboarding & audience reach (Journey 5 — decides adoption beyond the Driver)

### D1. Glossary / "How to read this portal" page — NET-NEW
FR/NFR, AC, ADR, BMad, `/bmad-*`, "spec kernel," "quick-dev" all appear undefined (feedback T6). Missing: acronym expansion on first use per page (`<abbr>` suffices), a one-line caption under each surfaced command ("Runs an adversarial review of this story's code"), and a short orientation page linked from Home's Explore Key Views.

### D2. Suggested reading order for first-time visitors — EXTENDS
Explore Key Views partially does this, but a first-time visitor still faces nine nav items with no sequence. Missing: an explicit "start here" reading order (Readme → PRD → Architecture → Epics → current sprint) on Home, matching Journey 5's path.

### D3. Empty-state design system — NET-NEW
Empty states currently read as clutter or error (feedback T4). Missing: consolidated per-epic banners for repeated CLI hints ("3 stories need task plans — run /bmad-create-story"), intentional-looking empty columns ("Nothing in progress — pick from Ready"), and a single copy-able command affordance per context.

---

## E. Memory & follow-ups (Journey 7)

### E1. Action-item provenance and resolution links — NET-NEW
Action items carry no resolution criteria and no destination; two items even reference the same Epic 1 heatmap debt from different retros without cross-linking. Missing: per-item provenance (source retro), resolution criteria, a link to the backlog entry or spec that will close it, and de-duplication across retros. Journey 7 succeeds when resolved items visibly leave the list.

### E2. Recency / "what changed since I last looked" signals — NET-NEW
Journey 1 explicitly needs change-since-last-visit cues; nothing surfaces them today. Story 7.3 (Activity Timeline and Date Pages) is adjacent but ROADMAPPED for a different purpose — the missing piece is lightweight last-updated / recently-changed markers on dashboard widgets and story cards.

---

## F. Presentation infrastructure (cross-cutting polish that compounds)

### F1. One-primary-view-per-page pattern — EXTENDS
The sprint page's By Status / By Epic radio toggle is the proven pattern (feedback T2); Home's requirements triple-render and Deep Analytics' coupling triple-render need it. Constraint: never cut a chart's text-twin table — that's the portal's accessibility contract (Story 3.7 ACs).

### F2. Date-format token and event sequencing — NET-NEW
Formats vary across pages; the ADR index shows no dates; same-day change-log entries have no ordering cue (feedback T7). Missing: one date token used everywhere, dates + one-line summaries on ADR listings, and sequence markers for same-day events.

### F3. Reference-rendering treatment — NET-NEW
Raw `[[wiki-link]]` names and `file:line` syntax leak into rendered prose. Missing: styled chips or a references appendix. (Epic 7's code citation linking — Story 7.2 — will make these clickable; the rendering treatment is the separate missing piece.)

### F4. Grouped, collapsible TOC for long planning artifacts — NET-NEW
The PRD's "On this page" TOC lists 28+ flat entries. Missing: collapsible grouping under parent sections. Keep the on-every-long-page TOC invariant as templates evolve.

### F5. `[ASSUMPTION: …]` tag styling — EXTENDS
Semantically important, visually plain. Extends Story 2.6's annotation-comments treatment to assumption tags.

### F6. Retired-work presentation — NET-NEW
Story 3.4's retirement notice sits inline among active stories. Missing: a collapsed "Retired" section on epic pages that preserves history without clutter — a pattern that will recur as the roadmap evolves.

### F7. Contributor-aware phrasing — NET-NEW (small)
"People to talk to about this file" repeated per row is noise in a solo repo. Missing: suppress or reword when there's a single contributor.

---

## Priority order

Follows the journey priorities (1–2 daily, 3 per-story, 4 the differentiator, 5–7 adoption-deciding) and the feedback doc's suggested order:

1. **A1 canonical status model + A5 count source** — every journey touches status and counts; highest leverage.
2. **B1 state-aware commands + B4 readiness affordances + D3 empty states** — Journey 2 ends in these daily.
3. **A2 FR story links** (with A3/A4 as natural follow-ons) — completes the Journey 4 chain with data the generator already has.
4. **B2 verification strip + B3 AC blocks** — makes Journey 3 fast.
5. **C1 Insights nav (and Structure demotion)** — cheap; un-orphans Journeys 6–7.
6. **C2 chart metadata standard** — then remaining C/D/E/F items as opportunistic work.

Before writing new stories for any EXTENDS item, audit the named specs in `_bmad-output/implementation-artifacts/` — several (`spec-ac-panel-and-story-card-polish`, `spec-hide-code-review-button-ready-for-dev`, `spec-story-next-steps-review-command`, `spec-home-next-steps-label-and-code-review`, `spec-commit-heatmap-contrast-and-day-drilldown`, `spec-sunburst-*`) partially overlap and should be extended, not duplicated.
