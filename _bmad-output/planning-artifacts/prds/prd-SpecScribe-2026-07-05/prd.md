---
title: SpecScribe
status: final
created: 2026-07-05
updated: 2026-07-05
---

# PRD: SpecScribe
*Working title - confirm.*

## 0. Document Purpose
This PRD defines the product contract for SpecScribe as a hobby open-source project that prioritizes practical usefulness over enterprise ceremony. It is written for the maintainer, contributors, and downstream workflows that may produce UX, architecture, and epics. The document is structured around Glossary terms, feature groups with globally stable FR IDs, explicit non-goals, and scoped assumptions tagged inline. It anchors on direct user guidance and observed current product behavior, with [README.md](README.md) and [docs/adrs/0001-spec-driven-development-framework.md](docs/adrs/0001-spec-driven-development-framework.md) as primary context; the existing brief in [_bmad-output/planning-artifacts/briefs/brief-SpecScribe-2026-07-05/brief.md](_bmad-output/planning-artifacts/briefs/brief-SpecScribe-2026-07-05/brief.md) is treated as secondary context only.

## 1. Vision
SpecScribe turns dense, agent-oriented planning artifacts into a human-readable status surface that helps people understand what is happening in a project without manually traversing dozens of markdown files. The core bet is that spec-driven development should improve clarity, not bury it.

The near-term product identity is CLI-first: one command to generate a static project portal, and one command to keep it current while files evolve. The generated site should remain easy to host, share, and inspect offline, with structure that maps to how spec-driven teams think (epics, stories, requirements, ADRs, and progress).

Longer-term, SpecScribe extends this same information model into an in-editor experience. The VS Code extension is a follow-on surface, not a replacement for the static site, and should reuse core parsing and projection logic rather than creating a separate product brain. [ASSUMPTION: Extension delivery begins as read-only visibility before any authoring or workflow controls.]

## 2. Target User

### 2.1 Jobs To Be Done
- As a solo open-source maintainer, I want instant visibility into project progress so I can decide what to do next quickly.
- As a contributor, I want to understand requirements, story status, and architectural decisions without reading every raw planning file.
- As a reviewer or community member, I want a browsable project narrative that shows momentum, scope, and decision history.
- As a spec-driven team member, I want one place that reconciles artifacts from multiple frameworks into a coherent view.

### 2.2 Non-Users (v1)
- Organizations needing hosted multi-tenant governance, RBAC, and enterprise compliance workflows.
- Teams expecting SpecScribe to replace issue trackers, project management suites, or BI platforms.
- Users looking for a markdown editor or requirements authoring tool.

### 2.3 Key User Journeys
- **UJ-1. Matt checks project health before starting a coding session.** Matt runs SpecScribe in watch mode, opens the generated dashboard, and uses epic, requirement, and ADR links to identify the most constrained next action.
- **UJ-2. A new contributor evaluates the repository before opening a PR.** The contributor opens the generated site, follows cross-links between stories and requirements, and quickly understands scope boundaries and current progress.
- **UJ-3. A maintainer shares progress externally.** The maintainer publishes static output and uses the generated pages to communicate status and decisions without requiring others to parse raw framework artifacts.

## 3. Glossary
- **Artifact Source** - The directory tree containing spec-driven markdown inputs that SpecScribe parses.
- **Framework Adapter** - Parser and mapping logic that converts one framework's artifacts into SpecScribe's common model.
- **Projection Model** - Internal normalized representation used to generate dashboard and detail pages.
- **Generated Portal** - The static HTML output set produced by SpecScribe.
- **Watch Mode** - Continuous regeneration mode triggered by source-file changes.
- **Insight Module** - Optional analysis pass that computes repository and artifact intelligence (for example, git activity and agent-file signals).
- **Agent Files** - Common coding-agent artifacts and logs (for example, specs, stories, memory files, workflow outputs) scanned for structural/project insights.

## 4. Features

### 4.1 Multi-Framework Artifact Ingestion
**Description:** SpecScribe ingests framework outputs and maps them into one Projection Model so the Generated Portal remains coherent even as input styles differ. Realizes UJ-1, UJ-2, UJ-3.

**Functional Requirements:**

#### FR-1: Adapter contract with shared projection
SpecScribe can apply a Framework Adapter contract so each supported framework maps into the same Projection Model.

**Consequences (testable):**
- Adding a new adapter does not require rewriting the core HTML templating pipeline.
- Unsupported artifacts are ignored or surfaced as non-fatal notices, not hard failures.

#### FR-2: BMad support remains first-class
SpecScribe can fully parse and render current BMad-oriented artifacts used by this repository.

**Consequences (testable):**
- Existing BMad epic/story/requirements/ADR rendering behavior remains intact across releases.
- Regressions in BMad parsing are covered by automated tests.

#### FR-3: Spec Kit baseline support
SpecScribe can parse and project the current Spec Kit artifact set and use as many available artifacts as produce useful user-facing insight.

**Consequences (testable):**
- Representative current-version Spec Kit repositories render without fatal errors.
- For each discovered Spec Kit artifact, the Generated Portal either renders a useful view or emits a clear non-fatal unsupported-artifact notice.

#### FR-4: GSD/GSD-Pi baseline support
SpecScribe can parse and project GSD/GSD-Pi artifacts as broadly as available data supports, prioritizing information that improves user understanding of progress, scope, and activity.

**Consequences (testable):**
- Representative GSD/GSD-Pi repositories render key planning and tracking artifacts without fatal errors.
- Additional discovered artifacts are rendered or summarized with a declared coverage tier (rendered, summarized, unsupported), while unsupported files never block generation.

### 4.2 Generated Portal and Traceability
**Description:** The Generated Portal emphasizes readability, navigation, and cross-linked traceability over raw file dumps. Realizes UJ-1, UJ-2, UJ-3.

**Functional Requirements:**

#### FR-5: Coherent navigation and dashboards
SpecScribe can generate a coherent index, section navigation, and progress summaries across parsed artifacts.

**Consequences (testable):**
- The generated index page links to every major artifact class present in source input.
- Missing classes (for example, no ADRs) degrade gracefully.

#### FR-6: Requirements and decision traceability
SpecScribe can cross-link requirements, stories, and ADR references where IDs are detectable.

**Consequences (testable):**
- ADR markdown files are rendered into an ADR index and ADR detail pages in the Generated Portal.
- Recognized requirement IDs resolve to requirement detail pages.
- Broken links are avoided for unresolved IDs.

#### FR-7: Markdown fidelity for core authoring patterns
SpecScribe can render core markdown patterns used in SDD artifacts, including Mermaid blocks and task lists.

**Consequences (testable):**
- Mermaid blocks render on generated pages without manual post-processing.
- GitHub-style checklists appear with completion states.

#### FR-8: Reliable watch-mode regeneration
SpecScribe can regenerate output in Watch Mode when source files change, including rapid edits.

**Consequences (testable):**
- Regeneration completes without corrupting output during quick successive saves.
- Source files are read without write-lock side effects.

### 4.3 Repository and Artifact Insights
**Description:** Insight Modules provide context beyond document rendering, helping users interpret project momentum and risk. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-9: Baseline git pulse
SpecScribe can compute and display lightweight git activity context as part of the Generated Portal.

**Consequences (testable):**
- Dashboard includes, at minimum, last commit timestamp, 30-day commit count, and top changed files derived from local git history.
- Generation continues when git history is unavailable or command execution fails.

#### FR-10: Optional deeper git insights
SpecScribe can optionally compute deeper repository analytics (for example, hotspots and change coupling) in a way appropriate for local OSS usage. [ASSUMPTION: This is opt-in to control performance impact.]

**Consequences (testable):**
- Deep metrics can be toggled independently of baseline generation.
- On reference repositories, baseline generation time with deep metrics disabled does not regress more than 10% from current baseline behavior.

**Out of Scope:**
- Individual productivity scoring or ranking.

#### FR-11: Agent-file structure insights
SpecScribe can analyze common Agent Files and workflow outputs to derive structural/project insights (for example, planning coverage, artifact freshness, and gaps), prioritizing primary source artifacts first and using memlog data as optional enrichment. [NOTE FOR PM: Evaluate whether GitStractor logic can be reused for repository scanning and summarization.]

**Consequences (testable):**
- V1 policy is Core + Orchestration: canonical Agent Files include PRD/brief/architecture/spec/epic/story/requirements artifacts and workflow output trees when present.
- Insight output identifies discovered artifact families and missing expected artifacts.
- Primary insight summaries are derivable from source artifacts even when memlog files are absent.
- Memlog and related journals are consumed only as secondary enrichment when available.
- Unknown or custom files do not cause generation failure.
- Full telemetry mode (Core + Orchestration + first-class memlog/journal/activity emphasis) is explicitly deferred to research/experimentation.

### 4.4 Distribution Surfaces
**Description:** CLI remains the primary delivery surface; extension support follows once the core model is stable. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-12: CLI-first user experience
SpecScribe can be operated via CLI for one-shot generation and watch workflows.

**Consequences (testable):**
- A user can generate output with zero required flags in an auto-discoverable supported repository, and with explicit source/output flags in non-default layouts.
- Help output documents command modes and major options.

#### FR-13: VS Code extension as follow-on surface
SpecScribe can expose the Generated Portal information model in a VS Code webview experience after CLI parity is stable. [ASSUMPTION: Initial extension release is read-only and local-first.]

**Consequences (testable):**
- Extension view can display core project status pages derived from local artifacts.
- Extension uses shared core parsing/projection logic, not duplicate parser implementations.
- Extension work begins when a maintainer relevance decision is recorded (in ADR or memlog) stating extension work is currently more valuable than incremental CLI/parser work, and no unresolved critical reliability regressions are known in active usage.

**Out of Scope:**
- Editing source planning artifacts directly in extension v1. [NON-GOAL for MVP]

## 5. Non-Goals (Explicit)
- Building a hosted SaaS with account management and remote data processing in v1.
- Replacing project management tools, issue trackers, or source control platforms.
- Implementing framework authoring flows (SpecScribe is a readability and insight layer, not a generator of upstream artifacts).
- Shipping extension-first while CLI parity and adapter stability remain unfinished.
- Producing people-ranking productivity metrics from git history.

## 6. MVP Scope

### 6.1 In Scope
- Stable CLI generate/watch workflows.
- First-class BMad support and improved robustness on mixed artifact inputs.
- Current-version Spec Kit and broad GSD/GSD-Pi artifact ingestion, favoring useful coverage over narrow baseline-only support.
- Generated Portal with coherent navigation, traceability links, ADR pages (already supported), Mermaid/task-list rendering.
- Baseline git pulse on dashboard.
- Initial artifact/agent-file insight pass focused on structure and completeness using Core + Orchestration policy, with memlog as secondary enrichment.

### 6.2 Out of Scope for MVP
- Perfect parity for every edge-case variant of every framework artifact on first release (iterate toward fuller parity through adapter hardening).
- Rich extension interaction patterns beyond read-only visibility. [NOTE FOR PM]
- Heavy analytics that materially slow default generation (deep git metrics remain optional).
- Full telemetry as the default insight mode (promoting memlog/journal/activity streams to first-class primary signals).
- Cloud sync, authentication, or collaborative editing.

## 7. Success Metrics

**Primary**
- **SM-1**: End-to-end generation reliability - In representative repositories, generate/watch complete without fatal error under normal edits. Validates FR-2, FR-5, FR-8, FR-12.
- **SM-2**: Traceability integrity - Recognized requirement/story/ADR references resolve correctly in generated output with low broken-link incidence. Validates FR-6, FR-7.

**Secondary**
- **SM-3**: Framework breadth - Baseline Spec Kit and GSD/GSD-Pi repositories render core pages in addition to BMad. Validates FR-3, FR-4.
- **SM-4**: Insight usefulness - Users can answer at least three common status questions from dashboard + insights without opening raw markdown. Validates FR-9, FR-11.

**Counter-metrics (do not optimize)**
- **SM-C1**: Dashboard complexity - Avoid maximizing metric count at the expense of readability. Counterbalances SM-4.
- **SM-C2**: Analytics overhead - Avoid deep analysis defaults that degrade basic generate/watch responsiveness. Counterbalances SM-2 and SM-4.

## 8. Cross-Cutting Non-Functional Requirements
- **NFR-1 (Performance):** Default generation remains responsive for local OSS repositories; optional deep analysis must be separable from baseline runs.
- **NFR-2 (Resilience):** Partial or malformed artifacts should degrade gracefully without crashing full-site generation.
- **NFR-3 (Local-first privacy):** Repository and artifact analysis runs locally by default; no remote telemetry is required for core operation.
- **NFR-4 (Extensibility):** Adapter architecture supports adding frameworks without core rewrites.

## 9. Open Questions
- How should coverage tiers be communicated (fully rendered, summarized, unsupported) so users understand exactly what is and is not interpreted?
- Which additional Agent Files should be plugin candidates after v1 Core + Orchestration coverage is stable?
- Should deeper git analysis be exposed as a CLI flag, settings profile, or both?
- Which concrete relevance signals should trigger extension work first (for example, user demand, contributor bandwidth, or recurring in-editor navigation pain)?

## 10. Research and Experimentation Goals
- Evaluate Option 3 (Full telemetry mode) as an opt-in experiment after v1 Core + Orchestration stabilizes in active usage.
- Test whether memlog/journal/activity-first insights produce materially better user decisions than source-first summaries.
- Define promotion criteria for moving any experimental telemetry signals into default insight behavior.

## 11. Assumptions Index
- [ASSUMPTION] Deep git analytics remain opt-in.
- [ASSUMPTION] Extension delivery starts read-only and local-first before any authoring workflows.

## 12. Alignment Addendum (2026-07-05)

This addendum records accepted direction updates so PRD and SPEC remain synchronized.

### 12.1 Configuration and Settings
- All user-facing features must be configurable through interactive options and equivalent CLI parameters.
- This includes git insights and ADR coverage controls.
- Settings are persisted via a directory-scoped settings file for the active source repository.

### 12.2 Local-Only and Read-Only Posture
- Product posture remains local-only and read-only.
- Future IDE integration may include helper buttons that generate next-step prompts/commands (for example, code review prompts) but must not mutate source planning artifacts.

### 12.3 Documentation Communication Standard
- README must communicate user-facing options primarily via tables plus short descriptive text.

### 12.4 Framework and Module Coverage
- Support target is all current popular spec-driven-development frameworks and modules with explicit popularity criteria.

### 12.5 Concrete Relevance Signals (Extension Timing)
- "Concrete relevance signals" means measurable triggers for beginning extension work.
- Minimum gate: demand or workflow-friction evidence, plus reliability and maintainer-capacity readiness.

### 12.6 Dual-Output Architecture Planning
- Plan for one shared projection/rendering core that feeds both generated HTML files and a future VS Code webview.
- Keep parser/projection logic shared across surfaces; keep output adapters focused on delivery concerns.

### 12.7 Extension Relevance Fit with Current Workflow
- Extension timing should account for real editor usage mix.
- If work is predominantly in non-VS-Code desktop tooling, extension investment remains lower priority.
- If context switching to browser/watch management becomes frequent and VS Code-family usage rises, extension relevance increases.

### 12.8 Module Boundary Sketch
- A shared projection/rendering core should expose host-neutral view models.
- Delivery adapters should target static HTML and VS Code webview separately without duplicating parser/projection logic.
- Read-only helper actions in webview may generate prompts/commands but must not directly mutate planning artifacts.
