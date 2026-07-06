---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md
  - _bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md
  - _bmad-output/specs/spec-specscribe/rendering-architecture.md
  - _bmad-output/specs/spec-specscribe/SPEC.md
  - _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md
  - README.md
  - src/SpecScribe/SiteGenerator.cs
  - src/SpecScribe/HtmlTemplater.cs
  - src/SpecScribe/BmadCommands.cs
---

# SpecScribe - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for SpecScribe, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Implement a framework adapter contract that maps each supported framework into one shared projection model without rewriting the core HTML templating pipeline.
FR2: Preserve first-class BMad support so current BMad artifacts in this repository parse and render correctly across releases.
FR3: Add Spec Kit baseline support so representative current-version Spec Kit repositories render without fatal errors.
FR4: Add GSD and GSD-Pi baseline support so representative repositories render key planning and tracking artifacts without fatal errors.
FR5: Generate coherent navigation, index, and progress dashboards across all discovered artifact classes.
FR6: Cross-link requirements, stories, and ADR references when IDs are detectable, while avoiding broken links for unresolved IDs.
FR7: Render core markdown authoring patterns used in spec-driven artifacts, including Mermaid blocks and task lists.
FR8: Provide reliable watch-mode regeneration when source files change, including rapid successive edits.
FR9: Compute and display baseline git pulse metrics (last commit timestamp, 30-day commit count, top changed files) when available.
FR10: Support optional deeper git insights (for example hotspots and change coupling) as independently toggleable analysis.
FR11: Analyze canonical agent/workflow files to surface structural insights such as planning coverage, artifact freshness, and gaps, with memlog as optional enrichment.
FR12: Deliver a CLI-first workflow for one-shot generation and watch mode, with auto-discovery defaults plus explicit path overrides.
FR13: Provide a follow-on VS Code webview surface that reuses shared parsing and projection logic and remains read-only in v1.
FR14: Provide project tree views and structural visualizations in generated outputs so users can inspect directory/layout shape and trace where planning and implementation artifacts live.

### NonFunctional Requirements

NFR1: Baseline generation performance remains responsive for local OSS repositories, with deeper analytics separated from baseline runs.
NFR2: Generation is resilient to partial, malformed, unsupported, or missing artifacts and degrades gracefully with non-fatal notices.
NFR3: Operation is local-first and privacy-preserving, requiring no remote telemetry for core behavior.
NFR4: Architecture is extensible so new framework adapters can be added without core rewrites.
NFR5: Source files are read with shared access and watch mode must not hold write locks on observed files.
NFR6: Cross-surface accessibility semantics (keyboard drill behavior, labels, status text redundancy) are contractual behavior, not optional styling.
NFR7: Feature configurability parity is required across interactive menu flows and equivalent CLI parameters, with directory-scoped settings persistence.

### Additional Requirements

- Implement a shared-core, adapter-per-surface architecture where parsing, projection, enrichment, and view-model shaping run once and delivery varies by host.
- Define and enforce host-neutral view models as the boundary contract between core logic and delivery adapters.
- Resolve effective settings once per run from directory-scoped settings plus run overrides, preserving provenance.
- Keep optional insight providers non-blocking so insight failures never block baseline generation.
- Treat watch-mode recomputation scope as an explicit unit and broaden rebuild scope when topology changes require coherence.
- Keep IDE helper actions explicit and read-only; helpers may generate commands/prompts but must not mutate planning artifacts.
- Share interaction-state semantics across static HTML and webview surfaces while allowing host-specific update transport.
- Preserve current generation footprint that already renders epics, stories, requirements, ADR pages, and linkified requirement references.
- Preserve atomic full rebuild behavior for full generation runs to prevent orphaned outputs from rename/delete drift.
- Preserve targeted regeneration entry points for epics and ADRs in watch mode to balance coherence and responsiveness.
- Maintain ADR rendering as a full-set refresh to keep ADR cross-links and index cards consistent.
- Maintain source-citation linkification and requirement-ID linkification during page rendering.
- Keep existing BMad support fully intact while broadening to bMad proper and other frameworks; current next-step command mapping is strongly GDS-oriented and requires generalization.
- Keep stylesheet delivery self-contained in tool packaging so runtime does not depend on loose asset files.
- Include directory-structure insight surfaces (tree-style views and related structural summaries) as first-class dashboard/navigation affordances when source data exists.

### UX Design Requirements

UX-DR1: Implement a light-first antiquarian design system with tokenized color, typography, spacing, radius, and component semantics defined centrally.
UX-DR2: Add dark mode that preserves the same hue family and supports system preference plus persisted user override.
UX-DR3: Implement sticky navigation with active-link semantics, accessible theme toggle, and mobile drawer behavior with focus trap and dismiss controls.
UX-DR4: Implement dashboard stat cards with tooltip support, keyboard focusability, and clear metric definitions.
UX-DR5: Implement an interactive multi-ring sunburst with hover tooltips, drill-down by epic and story, breadcrumb drill-up, and scoped status updates.
UX-DR6: Serialize sunburst drill state into URL hash for deep-linking and back/forward navigation.
UX-DR7: Implement keyboard interaction for sunburst segments (Tab focus order, Enter/Space drill, Escape up) with descriptive aria-label values.
UX-DR8: Implement progress bars with viewport-triggered animation and reduced-motion compliance.
UX-DR9: Implement Now and Next cards as full-surface links with explicit empty states when no active work exists.
UX-DR10: Implement index-card interaction patterns with focus-visible states and consistent hover/elevation behavior.
UX-DR11: Implement story and epic detail conventions including kicker row, status pill, task completion summaries, and source-link affordances.
UX-DR12: Implement a generated timestamp/freshness indicator and watch-refresh behavior that updates status content in place.
UX-DR13: Implement responsive layout behavior for mobile, tablet, and desktop breakpoints, including sunburst scaling and stacked detail panels.
UX-DR14: Implement VS Code webview adaptation rules that reuse core interaction semantics while honoring host theme primitives and command-link behavior.
UX-DR15: Implement CLI feedback states for interactive and non-interactive terminals, including progress, warnings, errors, and machine-parseable summary output.
UX-DR16: Implement accessibility foundations including skip link, semantic landmarks, heading hierarchy, tooltip semantics, and progressbar ARIA attributes.
UX-DR17: Ensure status communication is never color-only; pair color with text labels/icons consistently.
UX-DR18: Ensure motion respects prefers-reduced-motion with near-instant transitions and no looping animation.
UX-DR19: Implement a readable, interactive tree-view experience for project and artifact structure (expand/collapse, focusable nodes, clear depth cues, and link-out to relevant pages/files).
UX-DR20: Include high-impact but purposeful visual polish for insight modules (for example animated transitions, visual summaries, and drill paths) without violating performance or accessibility constraints.

### FR Coverage Map

FR1: Epic 4 - Shared adapter contract and projection model for multi-framework ingestion.
FR2: Epic 1 - Preserve first-class BMad parsing and rendering behavior.
FR3: Epic 4 - Spec Kit baseline ingestion and projection coverage.
FR4: Epic 4 - GSD and GSD-Pi baseline ingestion and projection coverage.
FR5: Epic 1 - Coherent navigation, dashboards, and major artifact surfacing.
FR6: Epic 1 - Requirements, story, and ADR cross-linking integrity.
FR7: Epic 1 - Markdown fidelity including Mermaid and task list rendering.
FR8: Epic 2 - Reliable watch regeneration and rapid-edit safety.
FR9: Epic 3 - Baseline git pulse metrics in the portal.
FR10: Epic 3 - Optional deeper git analytics toggle path.
FR11: Epic 3 - Agent and workflow structural insights with freshness and gap signals.
FR12: Epic 2 - CLI-first generate and watch with auto-discovery and explicit overrides.
FR13: Epic 5 - Read-only VS Code webview reusing shared core logic.
FR14: Epic 3 - Tree views and structural visualizations in generated outputs.

## Epic List

### Epic 1: High-Clarity BMad Portal Experience
Deliver a polished, immediately useful portal for current BMad projects so maintainers and contributors can understand status, traceability, and progress at a glance.
**FRs covered:** FR2, FR5, FR6, FR7

### Epic 2: Reliable Local Operations and Config Control
Make generation and watch highly dependable and easy to run and configure so users can trust daily usage in real repositories.
**FRs covered:** FR8, FR12

### Epic 3: Insight Surfaces and Tree-View Discovery
Add richer analytical insight, including tree views and structural visualizations, so users can understand project shape, gaps, and momentum quickly.
**FRs covered:** FR9, FR10, FR11, FR14

### Epic 4: Multi-Framework Coverage Expansion
Expand beyond BMad to include Spec Kit and GSD/GSD-Pi so mixed-framework teams can use one coherent portal.
**FRs covered:** FR1, FR3, FR4

### Epic 5: VS Code Read-Only Companion Surface
Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects.
**FRs covered:** FR13

<!-- Repeat for each epic in epics_list (N = 1, 2, 3...) -->

## Epic 1: High-Clarity BMad Portal Experience

Deliver a polished, immediately useful portal for current BMad projects so maintainers and contributors can understand status, traceability, and progress at a glance.

**FRs covered:** FR2, FR5, FR6, FR7

### Story 1.1: Dashboard Navigation and Readability Foundation

As a project maintainer,
I want a coherent landing page and navigation model,
So that I can find key project views in seconds.

**Acceptance Criteria:**

1.
**Given** a generated site with epics, requirements, and ADR content
**When** I open the home page
**Then** I see a clear dashboard with links to Epics, Requirements, ADRs, and source-derived pages
**And** missing artifact classes are omitted gracefully without broken navigation entries.

2.
**Given** any generated page
**When** I inspect navigation and breadcrumbs
**Then** active-page state and breadcrumb path are correct
**And** navigation remains usable on desktop and mobile breakpoints.

### Story 1.2: Traceability Links Across Requirements, Stories, and ADRs

As a contributor,
I want requirement IDs and source references to be linkified consistently,
So that I can move between planning artifacts without manual searching.

**Acceptance Criteria:**

1.
**Given** requirement IDs appear in rendered content
**When** I view generated pages
**Then** recognized IDs resolve to requirement detail pages
**And** unresolved IDs do not create broken links.

2.
**Given** story artifacts include source citations and ADR references
**When** I open epic and story pages
**Then** citations resolve to the appropriate generated pages
**And** ADR status and index cards remain consistent after regeneration.

### Story 1.3: Markdown Fidelity for Core Artifact Patterns

As a reviewer,
I want markdown patterns rendered faithfully,
So that generated pages preserve planning intent and implementation context.

**Acceptance Criteria:**

1.
**Given** source artifacts contain Mermaid blocks and task checklists
**When** the site is generated
**Then** Mermaid diagrams render client-side and checklists show completion states
**And** rendering works without manual post-processing.

2.
**Given** story details include acceptance-criteria references
**When** I open a story page
**Then** AC references deep-link to criteria anchors
**And** links include readable tooltip context when available.

### Story 1.4: Accessible High-Polish Interaction Baseline

As a user scanning project status,
I want interactive dashboard components that are both striking and accessible,
So that I can quickly understand progress regardless of input method.

**Acceptance Criteria:**

1.
**Given** the dashboard contains interactive cards and charts
**When** I use keyboard navigation
**Then** all interactive elements are focusable with visible focus states
**And** drill and hover alternatives are available without pointer-only interaction.

2.
**Given** motion preferences vary by user
**When** reduced-motion preference is enabled
**Then** non-essential animation is minimized
**And** information remains clear without relying on animation.

## Epic 2: Reliable Local Operations and Config Control

Make generation and watch highly dependable and easy to run and configure so users can trust daily usage in real repositories.

**FRs covered:** FR8, FR12

### Story 2.1: CLI Generate and Watch Modes with Smart Defaults

As a maintainer,
I want one-shot generate and continuous watch commands with sensible defaults,
So that I can produce and refresh docs quickly in real projects.

**Acceptance Criteria:**

1.
**Given** a supported repository layout
**When** I run generate or watch with no required flags
**Then** source and output roots are auto-discovered
**And** generation succeeds with clear terminal feedback.

2.
**Given** a non-standard repository layout
**When** I supply explicit source, ADR, and output options
**Then** those overrides are honored for the run
**And** help output documents available command options clearly.

### Story 2.2: Directory-Scoped Settings with Interactive and CLI Parity

As a repeat user,
I want settings persisted per repository and overridable per run,
So that I can keep preferred behavior without hidden global side effects.

**Acceptance Criteria:**

1.
**Given** I configure settings interactively
**When** I run generation later in the same repository
**Then** configured defaults are reused from directory-scoped settings
**And** behavior matches equivalent CLI arguments.

2.
**Given** I pass CLI overrides for a run
**When** generation starts
**Then** the effective config resolves once with overrides taking precedence
**And** provenance is available for diagnostics.

### Story 2.3: Watch Regeneration Safety and Scope-Aware Rebuilds

As a developer editing artifacts rapidly,
I want watch mode to regenerate safely under change bursts,
So that output stays coherent without blocking file edits.

**Acceptance Criteria:**

1.
**Given** multiple rapid saves occur in watched sources
**When** watch mode processes changes
**Then** output remains consistent and non-corrupt
**And** source files are read with shared access without write-lock side effects.

2.
**Given** rename, delete, or topology changes happen
**When** watch mode recomputes output
**Then** stale pages are removed or refreshed appropriately
**And** rebuild scope escalates when required for coherence.

## Epic 3: Insight Surfaces and Tree-View Discovery

Add richer analytical insight, including tree views and structural visualizations, so users can understand project shape, gaps, and momentum quickly.

**FRs covered:** FR9, FR10, FR11, FR14

### Story 3.1: Baseline Git Pulse Insights on Dashboard

As a maintainer,
I want lightweight git activity metrics in the portal,
So that I can assess project momentum at a glance.

**Acceptance Criteria:**

1.
**Given** git history is available
**When** I view the dashboard
**Then** I see last commit timestamp, 30-day commit count, and top changed files
**And** values are derived from local repository history.

2.
**Given** git history is unavailable or fails to load
**When** generation runs
**Then** generation still succeeds
**And** dashboard shows a non-fatal fallback state.

### Story 3.2: Optional Deep Git Analytics Controls

As an advanced user,
I want deeper git analytics available as an opt-in mode,
So that I can inspect hotspots without degrading default performance.

**Acceptance Criteria:**

1.
**Given** deep analytics are disabled
**When** baseline generation runs
**Then** default performance remains within defined responsiveness expectations
**And** deep analysis does not run implicitly.

2.
**Given** deep analytics are enabled explicitly
**When** generation completes
**Then** additional insights are surfaced distinctly from baseline metrics
**And** failures in deep analysis remain non-fatal.

### Story 3.3: Agent and Workflow Structure Coverage Insights

As a contributor,
I want visibility into planning artifact coverage and freshness,
So that I can identify missing or stale process artifacts quickly.

**Acceptance Criteria:**

1.
**Given** canonical planning and workflow files exist
**When** insights are computed
**Then** the portal reports discovered artifact families and key missing families
**And** freshness or staleness indicators are shown clearly.

2.
**Given** memlog and related journals are present
**When** structure insights run
**Then** memlog data is used as optional enrichment
**And** source-artifact-derived insights remain primary.

### Story 3.4: Interactive Tree Views for Project and Artifact Structure

As a project reviewer,
I want interactive tree views of directory and artifact structure,
So that I can inspect project organization and navigate to relevant content fast.

**Acceptance Criteria:**

1.
**Given** a generated portal with multiple artifact families
**When** I open the tree-view surface
**Then** I can expand and collapse nodes by depth
**And** each node has clear visual hierarchy cues and labels.

2.
**Given** I use keyboard and screen reader navigation
**When** I traverse the tree
**Then** tree items are focusable with announced state (expanded or collapsed)
**And** selecting a node can route to the related page or context target.

### Story 3.5: Flashy but Purposeful Insight Visual Language

As a stakeholder consuming status quickly,
I want insight visuals to feel impressive but still actionable,
So that demos and day-to-day usage both benefit from clarity and impact.

**Acceptance Criteria:**

1.
**Given** insight modules render charts and drill paths
**When** the page loads and interactions occur
**Then** transitions communicate state changes clearly
**And** motion remains bounded, meaningful, and performance-safe.

2.
**Given** accessibility constraints apply
**When** flashy visual affordances are enabled
**Then** equivalent text and non-color cues remain present
**And** reduced-motion settings preserve full informational meaning.

## Epic 4: Multi-Framework Coverage Expansion

Expand beyond BMad to include Spec Kit and GSD/GSD-Pi so mixed-framework teams can use one coherent portal.

**FRs covered:** FR1, FR3, FR4

### Story 4.1: Shared Framework Adapter Contract and Projection Path

As a maintainer supporting multiple frameworks,
I want a stable adapter contract into one projection model,
So that new framework support does not require rewriting core rendering.

**Acceptance Criteria:**

1.
**Given** framework-specific parsers are added
**When** adapters emit normalized records
**Then** projection and rendering consume a shared host-neutral model
**And** template and page generators remain framework-agnostic.

2.
**Given** unsupported artifact shapes are encountered
**When** parsing runs
**Then** unsupported items are categorized and reported as non-fatal
**And** successful artifacts still render.

### Story 4.2: Spec Kit Baseline Adapter Coverage

As a team using Spec Kit,
I want core Spec Kit artifacts interpreted in the portal,
So that I can track planning progress without switching tools.

**Acceptance Criteria:**

1.
**Given** representative current-version Spec Kit repositories
**When** generation runs
**Then** core planning and tracking artifacts render without fatal failures
**And** each discovered artifact is labeled rendered, summarized, or unsupported.

2.
**Given** unsupported Spec Kit artifact variants
**When** they are detected
**Then** they are surfaced as explicit non-fatal notices
**And** generation continues for supported content.

### Story 4.3: GSD and GSD-Pi Baseline Adapter Coverage

As a team using GSD workflows,
I want key GSD and GSD-Pi artifacts rendered coherently,
So that progress and scope remain understandable in one portal.

**Acceptance Criteria:**

1.
**Given** representative GSD and GSD-Pi repositories
**When** generation runs
**Then** key planning and tracking artifacts render without fatal errors
**And** output remains coherent with existing BMad and Spec Kit surfaces.

2.
**Given** partially supported GSD artifacts
**When** they are discovered
**Then** coverage tier labeling communicates interpretation boundaries clearly
**And** unsupported items never block full-site generation.

## Epic 5: VS Code Read-Only Companion Surface

Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects.

**FRs covered:** FR13

### Story 5.1: Shared View-Model Contract for HTML and Webview Adapters

As a maintainer,
I want both HTML and VS Code surfaces powered by the same view-model contract,
So that feature semantics stay consistent and parser logic is not duplicated.

**Acceptance Criteria:**

1.
**Given** the rendering pipeline emits page and interaction models
**When** HTML and webview adapters consume them
**Then** core navigation, drill, and traceability semantics remain equivalent
**And** adapter-specific code only handles host delivery concerns.

2.
**Given** rendering behavior changes
**When** parity checks run
**Then** semantic regressions between surfaces are detectable
**And** differences are documented as host-specific exceptions only.

### Story 5.2: Read-Only VS Code Dashboard and Epics Experience

As a VS Code user,
I want an in-editor status surface for dashboard and epics,
So that I can inspect project state without context-switching to a browser.

**Acceptance Criteria:**

1.
**Given** the extension opens the status webview
**When** project data is loaded
**Then** dashboard and epics views display with the same core interaction-state semantics as HTML
**And** in-editor navigation is responsive and readable.

2.
**Given** source artifacts change while the webview is open
**When** host updates are pushed
**Then** visible status refreshes in place without full panel reset
**And** drill/breadcrumb context remains coherent.

### Story 5.3: Host-Aware Theming and Explicit Helper Actions

As a maintainer using multiple themes,
I want webview visuals to align with VS Code chrome while preserving SpecScribe semantics,
So that the experience feels native without losing product identity.

**Acceptance Criteria:**

1.
**Given** light, dark, and high-contrast VS Code themes
**When** the webview renders
**Then** host theme variables are respected for chrome and container surfaces
**And** status and insight semantics remain clear and accessible.

2.
**Given** helper actions are exposed in the webview
**When** I trigger a helper
**Then** it generates explicit commands or prompts only
**And** no source planning artifacts are mutated by the helper path.
