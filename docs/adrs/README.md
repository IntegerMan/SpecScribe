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
- [ADR 0006 — Delivery Architecture & Distribution: JSON + SPA + npx vs. C# Static-Site + Bundled-Binary](0006-delivery-architecture-and-distribution.md) — **Accepted** (amends ADR 0005; re-affirms C# core, adds npx + optional JSON/SPA adapter, defers the pure-TS pivot)
