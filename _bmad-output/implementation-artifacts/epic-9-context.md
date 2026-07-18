# Epic 9 Context: Traceability and Review Follow-Through

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Complete the requirement → epic → story chain so stakeholders can hop from any requirement to its delivering stories, reviewers can judge a "done" claim in one glance (evidence strip, distinct ACs, collapsed digression), and follow-up items (action items, deferred work, unplanned/one-off specs) carry provenance and resolution paths — including first-class visibility in the primary remaining-work geometry (sunburst) and coherent Driver workflows for authoring and satisfaction status. Serves daily Driver journeys, review, traceability, and debt follow-through.

## Stories

- Story 9.1: Requirement Pages Link to Their Covering Stories
- Story 9.2: NFR and UX-DR Coverage Maps
- Story 9.3: Deferred-on-Purpose vs Unmapped Coverage States
- Story 9.4: Verification Evidence Strip on Story Pages
- Story 9.5: Distinct Acceptance-Criteria Blocks and Collapsed Dev Notes
- Story 9.6: Follow-Up Item Provenance and Resolution Paths
- Story 9.7: Open Follow-Ups in the Remaining-Work Geometry
- Story 9.8: Authoring and Delivery Workflow Coherence
- Story 9.9: Requirement Satisfaction Status at a Glance
- Story 9.10: Scannable Follow-Up List Pages
- Story 9.11: Follow-Up Detail Pages and Deep Links
- Story 9.12: Unplanned and One-Off Work in Geometry and Sprint
- Story 9.13: Generated Filtered Follow-Up Group Pages and Sunburst Click Destinations

## Requirements & Constraints

- Requirement (FR/NFR) detail pages list covering stories with current status from existing coverage-map data — no new authoring burden.
- NFR and UX-DR coverage maps match FR rigor; per-item state or a stated verification approach (not undifferentiated "Planned").
- Coverage distinguishes "deferred on purpose" from "unmapped" as separate states; never color-only; link to deferral source when one exists.
- Story pages show a verification-evidence strip near the status badge (tasks, tests, verified date); missing evidence is visibly absent, not omitted.
- Acceptance criteria render as visually distinct blocks via existing tokens; long-page dev-notes/dev-record collapse by default while preserving on-page TOC.
- Follow-up items carry provenance, resolution criteria, and links to resolving story/spec; de-dupe or cross-link duplicates across retros; order by age/blocking, not flat identical affordances.
- Open follow-ups appear as first-class remaining work in the sunburst (and sibling "what's left" summaries) — countable, navigable, not mislabeled as stories; distinct treatment never color-only.
- Unplanned/one-off work (e.g. quick-dev one-shots, deferred items with no epic attribution) gets a dedicated synthetic sunburst root and matching sprint-board lane; prefer epic attribution when provenance allows.
- List pages stay scan-first (shared row grammar); heavy detail lives on stable per-item detail pages with human-readable slug URLs derived from existing text (no new authoring schema).
- Sunburst group wedges land on generated filtered group list pages — never the unfiltered whole-site dump; leaf wedges land on item/story/spec detail.
- Holistic satisfaction status (dashboard/requirements hub) answers satisfied / deferred / unmapped / in flight in one scan, reflecting delivering-story lifecycle honestly.
- Driver path (requirements → create-story → development → review) must be coherent: close dead ends; extend existing next-step and empty-state seams; no parallel command system.
- Framework-specific guidance and follow-up artifact types flow through the adapter contract; surfaces degrade to absent (not empty-but-present or broken) when artifacts do not exist.
- Traceability integrity remains a product success criterion: recognized requirement/story links resolve with low broken-link incidence.

## Technical Decisions

- **No new authoring schema** for follow-ups, slugs, or unplanned attribution — derive from existing coverage maps, provenance, frontmatter, and sprint data.
- **Canonical status** routes through Epic 8's `StatusStyles` / `--status-*` system; no parallel vocabulary or colors for satisfaction or coverage states.
- **Single count ledger** — follow-up and remaining-work counts must agree with Epic 8's generator-side `ProjectCounts` (e.g. `OpenActionItems`); never a parallel recount.
- **Ownership splits:** 9.6 owns provenance/resolution on follow-up surfaces; 9.7 owns sunburst/geometry inclusion; 9.10 owns list compression; 9.11 owns detail pages + stable deep links; 9.13 owns generated filtered group pages and click destinations. Journey stories (9.8–9.9) compose gaps without absorbing page-level deliverables of 9.1–9.3.
- Shared-core / adapter-per-surface: framework-specific status labels, commands, and debt artifact types map at the adapter layer; shared rendering stays methodology-agnostic.
- Filtered follow-up group pages are **generated dedicated pages** (owner-locked), not hash/query filters on the full list.
- NFR/verification without per-story implementation state should show a stated verification approach rather than fake uniformity.

## UX & Interaction Patterns

- Status and coverage distinctions are never color-only; pair color with text/labels consistently; use designed empty/honest-absence treatment when evidence or artifacts are missing.
- Sunburst is the primary remaining-work attention surface: hover tooltips, drill breadcrumb, keyboard/ARIA labels; follow-up and unplanned wedges must be visually distinct (shape, label, or ring) from stories/tasks.
- Acceptance criteria: bordered/tinted blocks from existing design tokens; audit/extend existing AC-panel polish rather than duplicating.
- Dev notes/dev record: collapsed by default on long story pages; expand on demand; keep "On this page" TOC invariant.
- Follow-up list pages share one sibling grammar (action items ↔ deferred work); group/order from 9.6 remains legible without expanding rows.
- Empty states and next-step commands: designed, adapter-supplied hints; one primary recommended path per lifecycle state.
- Visual changes that affect create-story / silhouette should elicit owner-selected directions up front (Epic 3/7/8 visual-intent practice).

## Cross-Story Dependencies

- **Depends on Epic 8:** canonical status model (`StatusStyles`), single `ProjectCounts` ledger, next-step commands (8.5), designed empty states (8.6) — foundation for 9.2/9.7–9.10/9.12 and workflow coherence (9.8).
- **Within epic:** 9.1–9.3 feed 9.9; 9.3's deferred/unmapped states refine 9.1 empty messaging; 9.6 → 9.10 → 9.11; 9.7 navigability completes via 9.11 (+ 9.13 for group wedges); 9.12 Unplanned root ties sunburst and sprint board to the same residual set; 9.13 consumes 9.10 row grammar and 9.11 detail URLs.
- **Downstream:** Epic 19 (directed work graph) depends on Epic 9 follow-up provenance as a data source but does not block Epic 9.
- **Coordination:** Epic 10 may relocate Action Items / Deferred Work in nav; Epic 9 owns content, provenance, and geometry — not top-nav IA.
