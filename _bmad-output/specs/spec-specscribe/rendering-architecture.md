# Rendering Architecture Sketch

This companion defines module boundaries for a single shared rendering core that feeds both static HTML output and a VS Code webview.

## Goals

- Keep one parsing/projection brain.
- Support multiple delivery surfaces through adapters.
- Preserve local-only and read-only behavior.
- Avoid feature drift between HTML and webview surfaces.

## High-Level Layers

1. Ingestion Layer
- Discovers artifacts and routes files to framework adapters.
- Produces normalized source documents plus diagnostics.

2. Projection Layer
- Transforms normalized documents into a unified domain model.
- Applies cross-linking, traceability, progress, git/ADR insight enrichment, and coverage classification.

3. Rendering Core Layer
- Converts domain model into presentation-ready view models.
- Defines stable contracts for pages/views/components independent of output host.

4. Delivery Adapter Layer
- HTML adapter: writes static pages/assets.
- Webview adapter: serves view models and host integration glue for VS Code.

## Proposed Core Interfaces

## IArtifactAdapter
- Responsibility: Parse one framework/module artifact family into normalized records.
- Input: source file set + parse options.
- Output: normalized records + warnings/errors.

## IProjectionPipeline
- Responsibility: Build canonical project model from normalized records.
- Input: normalized records + effective settings.
- Output: project domain model + diagnostics.

## IViewModelRenderer
- Responsibility: Build presentation view models from domain model.
- Input: project domain model + display options.
- Output: typed page/view models + navigation graph + asset manifest.

## IRenderAdapter
- Responsibility: Deliver rendered view models to a host surface.
- Input: page/view models + assets + adapter options.
- Output: host-specific artifacts (files for HTML, messages/resources for webview).

## ISettingsProvider
- Responsibility: Resolve effective settings from directory-scoped file, CLI overrides, and interactive changes.
- Input: source directory + optional run overrides.
- Output: effective settings object + provenance.

## IInsightProvider
- Responsibility: Contribute optional insight fragments (git pulse, ADR coverage, other modules) without breaking baseline generation.
- Input: domain model + settings.
- Output: insight fragments + non-fatal diagnostics.

## Data Flow (Single Run)

1. Source discovery finds supported artifacts.
2. Framework adapters parse artifacts into normalized records.
3. Projection pipeline builds canonical domain model.
4. Insight providers enrich model according to settings.
5. Rendering core produces host-neutral view models.
6. Delivery adapter emits HTML files or webview payloads.

## Watch Flow

1. File change event enters ingestion queue.
2. Changed scope is reparsed incrementally when possible.
3. Projection and view model recomputation is scoped to impacted sections.
4. Adapter updates host output atomically.

## Feature Parity Rules

- New user-visible rendering features land in the rendering core first.
- Adapters only map existing core view models to host primitives.
- Any temporary adapter-only behavior must be marked as explicitly deferred parity work.

## Client-Side Enhancement Policy (Progressive Enhancement)

- The HTML surface has historically been pure static markup + inline SVG charts with no JavaScript (aside from a tiny tooltip/copy enhancer). That default stands for baseline pages and all chart primitives, which remain pure SVG.
- **Insight surfaces** (the aggregate Git Insights hub and comparable data-dense pages) MAY use JavaScript as a *progressive enhancement only* — e.g. client-side sorting, filtering, and expand/collapse of tables.
- Guardrails (align with PRD NFR-5 and the Story 1.4/1.5 accessibility conventions):
  - Core content and navigation MUST render and work with JavaScript disabled; JS only adds convenience, never information.
  - Server/generation-time ordering is the source of truth; client sorting is a re-ordering of already-present rows.
  - Text and non-color cues and reduced-motion support still apply to any enhanced surface.
  - Webview parity: the webview adapter must reach the same information without depending on the HTML surface's enhancement scripts.

## Read-Only IDE Helper Pattern

- Webview can expose helper buttons that generate prompt text or command lines.
- Helper actions do not edit source artifacts directly.
- Optional command handoff is explicit user action.

## Suggested Package Boundaries

- src/SpecScribe.Core
- src/SpecScribe.Adapters.BMad
- src/SpecScribe.Adapters.SpecKit
- src/SpecScribe.Adapters.Gsd
- src/SpecScribe.Rendering
- src/SpecScribe.Delivery.Html
- src/SpecScribe.Delivery.Webview
- src/SpecScribe.Cli

## Evolution Sequence

1. Extract current projection and rendering logic into host-neutral core interfaces.
2. Keep existing HTML generation as first concrete IRenderAdapter.
3. Add contract tests asserting identical view model semantics across adapters.
4. Add webview adapter once relevance gates are met.
5. Incrementally add helper buttons for prompt generation in webview, still read-only.

## Verification Targets

- Same source input yields equivalent navigation/traceability semantics in HTML and webview outputs.
- Adapter differences are only host concerns (routing, packaging, host APIs).
- With webview adapter disabled, existing CLI behavior/performance remains within current tolerances.
