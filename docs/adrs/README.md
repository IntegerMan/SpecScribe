# Architecture Decision Records

This directory holds SpecScribe's Architecture Decision Records (ADRs) — short documents that
capture a significant decision, the context that forced it, the options weighed, and the
consequences we accept. They are hand-authored (not generated from `_bmad-output/`) and are
rendered into the live site by SpecScribe itself.

Each record is numbered by its filename prefix and carries a `**Status:**` line.

## Records

- [ADR 0001 — Adopt BMAD-METHOD as SpecScribe's Spec-Driven Development Framework](0001-spec-driven-development-framework.md) — **Accepted**
- [ADR 0002 — Preserve a Shared Rendering Core with Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md) — **Accepted**
- [ADR 0003 — Keep Settings Directory-Scoped and IDE Helpers Read-Only](0003-directory-scoped-settings-and-read-only-helpers.md) — **Accepted**
- [ADR 0004 — Preserve Cross-Surface Interaction Semantics and Host-Aware Theme Boundaries](0004-cross-surface-interaction-and-theme-contract.md) — **Accepted**
- [ADR 0005 — VS Code Webview Runtime: Core↔Extension Seam and Packaging](0005-vs-code-webview-runtime-and-packaging.md) — **Accepted** (amended by ADR 0006)
- [ADR 0006 — Delivery Architecture & Distribution: JSON + SPA + npx vs. C# Static-Site + Bundled-Binary](0006-delivery-architecture-and-distribution.md) — **Accepted** (amends ADR 0005; re-affirms C# core, adds npx + optional JSON/SPA adapter, defers the pure-TS pivot; **extended by ADR 0008**)
- [ADR 0007 — Deriving a Change's Visible Surface by Reading Standard BMAD Story Sections](0007-change-surface-descriptor-for-testing-and-footprint.md) — **Accepted** (read the guaranteed BMAD sections — File List, Acceptance Criteria, Tasks, Status/Change Log — to project a change's testable footprint; no artifact changes, portable to default BMAD)
- [ADR 0008 — JSON Data-Layer as Canonical Intermediate Representation & Incremental, Event-Driven Generation](0008-json-ir-canonical-and-incremental-generation.md) — **Accepted** (extends ADR 0002 + ADR 0006; JSON IR is canonical, all surfaces project from it, generation goes incremental/event-driven; does **not** reopen the TS core port; design-now/build-later → Epic 22)
- [ADR 0009 — Front-End Framework for the Projection Layer](0009-frontend-framework-for-projection-layer.md) — **Accepted** (component-oriented presentation over the ADR 0008 IR for CSS modularity; **Vue + Nuxt 3, universal/SSR** — NFR6 baseline preserved by Nuxt prerender; relaxes ADR 0005's C#-rendering north star for the presentation layer; re-opens ADR 0006 axis B / rendering-language only, analysis stays C#; spike-gated → Epic 23, after Epic 22's IR)
- [ADR 0010 — Client-Side Charting JS for Opt-In Deep-Analytics Surfaces](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) — **Accepted** (supersedes "pure SVG, no charting JS" for opt-in analytics pages only — Git Insights, Code Map colorize views; baseline/default pages stay zero-JS per NFR-5; NFR-5 reinterpreted as a required no-JS default-mode-plus-text-equivalent baseline per surface; FR31 preserved via generation-time-embedded bounded data; narrows Epic 20 Story 20.1's spike to its still-open unknowns; triggered by Story 7.11's correct-course, also affects Story 7.12)
- [ADR 0011 — Directed-Graph Edge Direction: Carrier → Target](0011-directed-graph-edge-direction-carrier-to-target.md) — **Accepted** (promotes Story 19.1 §2's edge-direction rule to a cross-surface invariant: every directed-graph surface states one explicit, source-anchored direction rule, never inferred; reverse views are forward-index inversions, not new edges; the work graph uses `From`=carrier / `To`=target with `covers` the one stored-inverted "covered-by" kind; Epic 24 coupling graphs inherit the discipline — directional metrics point antecedent→consequent, symmetric metrics render undirected; memorializes an in-force convention, no behavior change)
