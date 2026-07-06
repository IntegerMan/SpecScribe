---
id: SPEC-specscribe
companions:
  - requirements-catalog.md
  - settings-and-signals.md
  - rendering-architecture.md
sources:
  - ../../planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only.

# SpecScribe

## Why

SpecScribe exists to turn dense, agent-oriented planning artifacts into a human-readable status surface so maintainers, contributors, and reviewers can understand project state quickly without traversing large markdown trees. The immediate priority is a reliable, local-only, CLI-first static portal where every feature can be controlled interactively or by command-line parameters and persisted by directory-scoped settings, with in-editor surfaces remaining read-only and helper-oriented.

## Capabilities

- **CAP-1**
  - **intent:** Ingest artifacts from current, popular spec-driven-development frameworks and modules into one projection model so mixed-framework repositories remain interpretable.
  - **success:** Representative repositories across supported popular frameworks/modules render core planning and tracking views without fatal generation failures.

- **CAP-2**
  - **intent:** Generate a readable static portal with coherent navigation and traceability across epics, stories, requirements, ADRs, and configurable ADR coverage outputs.
  - **success:** The portal index links discovered major artifact classes and recognized IDs resolve to stable pages without broken generated links.

- **CAP-3**
  - **intent:** Keep generated output current via reliable watch-mode regeneration during normal save/edit loops.
  - **success:** Rapid successive source edits regenerate without output corruption or source-file lock side effects.

- **CAP-4**
  - **intent:** Provide local insight modules for repository momentum and artifact-structure coverage with configurable git and ADR analysis depth without blocking baseline generation.
  - **success:** Dashboard output includes configurable git pulse and ADR coverage views, and insight failures degrade gracefully to non-fatal notices.

- **CAP-5**
  - **intent:** Deliver and prioritize a CLI-first surface now, then expose the same shared projection through separate presentation adapters for HTML files and a follow-on VS Code webview, with IDE interactions remaining read-only and helper-oriented.
  - **success:** Zero-required-flag CLI flows work in supported layouts, HTML and webview surfaces consume the same rendering core without duplicate parser logic, IDE helpers only generate prompt text/commands, and extension work starts when context-switch pain and editor usage patterns meet explicit relevance gates.

## Constraints

- Adapter architecture must decouple framework parsing from rendering so new frameworks do not require core template rewrites.
- Projection and rendering architecture must separate core document/view models from delivery adapters so HTML generation and VS Code webview can evolve without forking parsing logic.
- Partial, malformed, or unsupported artifacts must degrade gracefully via non-fatal notices instead of stopping full generation.
- Every user-facing feature, including git insights and ADR coverage, must be configurable through interactive options or CLI parameters and persistable in a directory-scoped settings file.
- Baseline generation must remain responsive; deeper analytics must be optional and independently toggleable.
- Core operation is local-first and must not require remote telemetry or hosted control planes.
- User-facing behavior and options must be documented in README tables with short explanatory descriptions.

## Non-goals

- Build a hosted SaaS control plane (accounts, RBAC, multi-tenant governance) in this effort.
- Replace issue trackers, project management suites, or source-control platforms.
- Add framework authoring/editing workflows; this contract is for readability and insight projection.

## Success signal

A maintainer can run default CLI generation or watch in representative supported popular framework repositories, configure all features (including git and ADR coverage) via interactive options or CLI flags with directory-scoped settings persistence, and answer near-term planning/status questions from the generated portal and documented README tables without reading raw artifact files; generation remains resilient, responsive, local-only, and read-only.

## Assumptions

- Deep git analytics remain opt-in to protect baseline responsiveness.
- Initial VS Code extension delivery is read-only and local-first before any authoring controls.

## Open Questions

- How should coverage tiers (rendered, summarized, unsupported) be communicated so users understand interpretation boundaries?
- Which additional agent-file families should be promoted to plugin candidates after core plus orchestration coverage stabilizes?
- Should deeper git analysis be configured by CLI flag, settings profile, or both?
- What popularity threshold (for frameworks/modules) determines "decent degree of popularity" for first-class adapter support?
