# Sprint Change Proposal — 2026-07-19

**Trigger type:** Owner-directed backlog expansion (feature vision), not a defect or blocker.
**Prepared by:** Correct-Course workflow with Matthew-Hope Eland.
**Scope classification:** **Moderate** — additive backlog reorganization, no rework of shipped code, no replan of in-flight epics. Route to PO/DEV for scheduling.

---

## Section 1 — Issue Summary

The owner asked to analyze SpecScribe for high-value additions and seat them as backlog items, driven by a concrete product vision:

- An **interactive, drill-in sunburst** that zooms into a node, reveals nested children, and lets you navigate back out — paired with a **side pane of related nodes**.
- **More insights derived from code.**
- **More polished list pages.**
- The **white-bar top navigation is underused and ill-suited for interior pages** — it should be used effectively everywhere, with context that differs per page/page-type.
- **More charts/displays** that show product value and surface correlations across work items and codebases.

Discovery found much of the vision already partially seated, so the change **sharpens and connects existing seeds** rather than duplicating them.

## Section 2 — Impact Analysis

**Existing seeds the vision builds on (not duplicated):**

- `Charts.Sunburst` / `EpicSunburst` — a *static, pure-SVG, two-level* sunburst with click-to-page links. No zoom/drill-in.
- **Story 10.7** (ready-for-dev, *in active development*) — sunburst navigability at scale, staying within the static model. **Kept as the no-JS baseline** the new explorer enhances.
- **Story 9.13** (done) — locks click destinations (leaf → detail page, group → filtered list page). New surfaces honor this contract.
- **Epic 19** (backlog, spike-led) — the directed work-graph model; becomes the **data source for the related-work side pane**.
- **Story 9.10** (done) — a follow-up list *row grammar*, generalized into a shared list primitive.
- **Story 10.1** `RenderNavMarkup` seam (3-surface parity) + **Story 10.5** grouped TOC + `EntityPager` prev/next — the foundation the nav redesign extends.

**Architectural decision (owner-approved):** the interactive explorer introduces SpecScribe's **first substantial client-side interactive surface**, a deliberate, one-time, budgeted crossing of the project's "pure SVG, no JS" value. All interactive work degrades to server-rendered/static baselines (NFR8). The client-light list sort/filter (Story 10.9) reuses this same JS budget rather than a second stack.

**Artifacts changed:** `epics.md` (new epics + stories + FR38/FR39 coverage lines), `sprint-status.yaml` (backlog registration). PRD sync for FR37–FR39 deferred "when convenient," matching the established pattern.

## Section 3 — Recommended Approach

**Direct adjustment (additive).** Five clusters seated as backlog:

| # | Cluster | Placement |
|---|---------|-----------|
| 1 | Interactive drill-in sunburst + related-work side pane | **New Epic 20** (20.1 spike, 20.2 explorer, 20.3 side pane) |
| 2 | Code risk/ownership/freshness insights | **Epic 7** stories 7.10–7.12 |
| 3 | Polished list pages (shared grammar + client-light sort/filter) | **Epic 10** stories 10.8–10.9 |
| 4 | Context-aware white-bar navigation + section-nav coherence | **Epic 10** stories 10.10–10.11 (folded in per owner, not a new epic) |
| 5 | Value & correlation visualizations | **New Epic 21** (21.1 matrix, 21.2 cadence/cycle-time, 21.3 planning↔code) |

**Numbering note:** the nav cluster folding into Epic 10 freed epic number 21, so the insights epic is seated as **Epic 21** (no phantom gap).

**Honesty guardrails carried throughout:** determinism (FR31 — no per-visitor "now"), NFR8 degrade-to-empty/omit, no re-counting against the single-source counts (Story 8.3), no fabricated cycle-time, "complexity" kept as a size proxy (no smuggled analyzer).

## Section 4 — Detailed Change Proposals

See `epics.md` for full Given/When/Then acceptance criteria. Summary of additions:

- **Epic 20 — Interactive Project Explorer:** 20.1 Architecture Spike (interactivity boundary + payload + degrade contract) · 20.2 Zoomable Drill-In Sunburst Navigation · 20.3 Related-Work Side Pane on Selection.
- **Epic 7 (extended):** 7.10 Refactor-Target Risk Quadrant (Churn × Size) · 7.11 Code Ownership & Bus-Factor Insights · 7.12 Code Freshness / Age Map. All extend the single `--deep-git` numstat path.
- **Epic 10 (extended):** 10.8 Unified List-Page Grammar · 10.9 Client-Light Sort/Group/Filter · 10.10 Context-Aware Navigation Bar (the white bar carries page-type-specific contents) · 10.11 Sticky Section Nav & Breadcrumb Coherence.
- **Epic 21 — Value & Correlation Insights:** 21.1 Traceability Coverage Matrix · 21.2 Delivery Cadence & Story Cycle-Time · 21.3 Planning ↔ Code Impact Map.
- **Requirements:** FR38 (Epic 20), FR39 (Epic 21) added to the FR Coverage Map; PRD sync deferred.

## Section 5 — Implementation Handoff

- **Scope:** Moderate → **Product Owner / Developer** for sequencing.
- **Sequencing guidance:**
  - **Epic 19 (work-graph spike 19.1)** should land before Epic 20's side pane (20.3), since 20.3 consumes its edges.
  - **Epic 20 is spike-led** — run Story 20.1 (create-story → dev) before 20.2/20.3 to fix the JS budget and degrade contract.
  - **Story 10.9** depends on the Epic 20 interactivity budget existing.
  - Epics 7.10–7.12, 10.8/10.10/10.11, and 21.1 have no hard dependency and can be scheduled independently.
- **Next step:** run `create-story` per story when scheduled (spikes 20.1 and 19.1 first).
- **Success criteria:** each story ships behind its NFR8 degrade path with HTML/webview/SPA parity and generation-time determinism preserved.
