# Requirements Catalog

This companion carries load-bearing detail that would bloat the kernel while remaining necessary for implementation and verification.

## Capability to Requirement Mapping

- **CAP-1 (multi-framework ingestion):** FR-1, FR-2, FR-3, FR-4.
- **CAP-2 (generated portal and traceability):** FR-5, FR-6, FR-7.
- **CAP-3 (watch-mode reliability):** FR-8.
- **CAP-4 (insight modules):** FR-9, FR-10, FR-11.
- **CAP-5 (distribution surfaces):** FR-12, FR-13.

## Functional Requirement Detail

- **FR-1 Adapter contract with shared projection:** New adapters must plug into the existing projection model and avoid core template rewrites.
- **FR-2 BMad first-class support:** Existing BMad parsing and rendering behavior remains intact and regression-tested.
- **FR-3 Spec Kit baseline support:** Current-version Spec Kit artifacts render useful views; unsupported artifacts produce non-fatal notices.
- **FR-4 GSD/GSD-Pi baseline support:** Core planning/tracking artifacts render without fatal failures; additional artifacts are tiered as rendered, summarized, or unsupported.
- **FR-5 Coherent navigation and dashboards:** Index and section navigation cover discovered major artifact classes and degrade gracefully when classes are missing.
- **FR-6 Requirements and decision traceability:** Detectable requirement/story/ADR IDs resolve to generated detail views; unresolved IDs do not emit broken links.
- **FR-7 Markdown fidelity:** Mermaid and task-list authoring patterns render correctly without manual post-processing.
- **FR-8 Reliable watch mode:** Successive saves regenerate safely without output corruption or source lock side effects.
- **FR-9 Baseline git pulse:** Dashboard includes at least last commit timestamp, 30-day commit count, and top changed files when git data is available.
- **FR-10 Optional deeper git insights:** Deep metrics are independently toggleable and do not materially regress baseline generation when disabled.
- **FR-11 Agent-file structure insights:** Core plus orchestration artifact families are detected with freshness/gap signals; memlog and related journals are optional enrichment, not primary dependency.
- **FR-12 CLI-first UX:** One-shot and watch workflows are available from CLI with default auto-discovery and explicit path overrides.
- **FR-13 Follow-on extension surface:** Extension consumes shared core projection logic and starts after explicit relevance and reliability gate decisions.
- **FR-14 Full feature configurability:** Every user-facing feature is configurable via interactive options and equivalent CLI parameters.
- **FR-15 Git and ADR coverage controls:** Git analytics depth and ADR coverage/reporting controls are exposed through interactive and CLI configuration surfaces.
- **FR-16 Directory-scoped settings persistence:** Effective settings can be stored and loaded from a settings file located in or selected for the active source directory.
- **FR-17 Read-only IDE helpers:** IDE integration remains read-only but may offer helper buttons that generate next-step prompts/commands (for example, code review prompts) without writing project artifacts.
- **FR-18 README communication contract:** README contains tabular option references and concise descriptions for user-facing behavior and configuration.
- **FR-19 Popular-framework coverage policy:** First-class support targets currently popular spec-driven frameworks/modules, with explicit published criteria for what counts as popular.
- **FR-20 Dual-target presentation architecture:** A shared rendering core produces presentation-ready view models consumed by both static HTML output and a VS Code webview adapter.
- **FR-21 Adapter parity policy:** New rendering features must be implemented once in the shared core and exposed to both HTML and webview adapters unless explicitly deferred.

## Non-Functional Requirements

- **NFR-1 Performance:** Default local generation remains responsive; expensive analytics are optional.
- **NFR-2 Resilience:** Partial/malformed artifacts degrade gracefully without terminating full-site generation.
- **NFR-3 Local-first privacy:** Core capabilities run locally with no required remote telemetry.
- **NFR-4 Extensibility:** Framework support expands through adapter additions rather than core rewrites.
- **NFR-5 Local-only and read-only posture:** Core and extension behavior remain local-only and avoid mutating source planning artifacts.
- **NFR-6 Architecture maintainability:** HTML and webview surfaces must avoid parser or projection duplication that would create behavioral drift.

## Success Metrics and Counter-Metrics

- **SM-1 Reliability:** Generate/watch completes without fatal errors under normal edits in representative repositories.
- **SM-2 Traceability integrity:** Recognized requirement/story/ADR links resolve with low broken-link incidence.
- **SM-3 Framework breadth:** Baseline Spec Kit and GSD/GSD-Pi support render core pages in addition to BMad.
- **SM-4 Insight usefulness:** Users can answer common status questions from portal plus insights without opening raw markdown.
- **SM-5 Configuration completeness:** Users can configure all documented features using either interactive options or CLI parameters with no feature gaps.
- **SM-6 Documentation clarity:** README option tables and descriptions let a new user configure common workflows without external docs.
- **SM-C1 Dashboard complexity (counter):** Avoid maximizing metric count at readability's expense.
- **SM-C2 Analytics overhead (counter):** Avoid deep-analysis defaults that degrade baseline responsiveness.

## Concrete Relevance Signals (Extension Timing)

- **Signal A - Active usage pull:** At least 3 recurring users or maintainers request in-IDE read-only visibility over a sustained period (for example, over 30 days).
- **Signal B - Workflow friction:** The maintainer repeatedly context-switches to browser pages or external viewers, or needs watch-mode setup management often enough that in-IDE read-only status surfaces would reduce friction.
- **Signal C - Reliability gate:** No unresolved critical parser/generation regressions exist in active CLI usage.
- **Signal D - Capacity gate:** Maintainer bandwidth can support extension maintenance without stalling core adapter/reliability work.
- **Signal E - Editor-share gate:** VS Code-family editor usage is high enough that extension investment benefits real daily workflow, rather than primarily external desktop tools.
- **Go rule:** Begin extension increments only when A or B is true, C and D are true, and E is not contradictory to current workflow reality.

## Explicitly Deferred

- Hosted SaaS and governance capabilities.
- Tool replacement ambitions for issue tracking and project management.
- Extension-first strategy before CLI/parsing reliability is stable.
- Full telemetry mode as default insight behavior.
