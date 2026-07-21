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
