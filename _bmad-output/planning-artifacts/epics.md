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

The epics are ordered by delivery phase: (1) a polished, richly-functional BMad-only portal, (2) deeper insight surfaces and UX, (3) expansion to additional frameworks, (4) reliable operations/configuration and OSS-ready documentation, and (5) the editor surface plus code-and-git exploration.

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
FR14: Provide a source-code treemap and related structural visualizations in generated outputs so users can inspect codebase shape, code mass (lines of code), and git-derived change signals (change frequency, creation/last-modified recency, average change size).
FR15: Render project source/code files as browsable in-portal pages and resolve source citations and code references (for example `[Source: path:line]` and "View source" links) to those pages rather than raw or dead links.
FR16: Provide temporal/timeline views of project activity, including per-date activity pages, and link dates (commit dates, heatmap cells, artifact timestamps) to them.
FR17: Add adapter coverage for additional spec-driven frameworks (for example SpecFlow, Squad, and Superpowers) through the shared adapter contract.
FR18: Provide OSS-ready onboarding and reference documentation (getting started, configuration/CLI reference, and contribution guidance) for community sharing.
FR19: Provide advanced code-and-git coverage on code pages (for example history/blame annotations and change-coupling/hotspots) as an opt-in extension of code exploration.

<!-- FR15–FR19 added post-PRD (2026-07-06) to seat the reordered roadmap (Epics 4/5/7); sync back into the PRD for full traceability when convenient. -->

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
- Include a source-code structural visualization (a treemap sized by lines of code and colorized by git-derived change signals) as a first-class navigation affordance when source and git data exist.

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
UX-DR19: Implement a readable, interactive source-structure visualization — a treemap of the code tree sized by lines of code and colorable by git-derived signals (change frequency, creation/last-modified recency, average change size), with rich hover/focus tooltips, directory drill/zoom with breadcrumb, focusable rectangles carrying descriptive labels, and a non-color text equivalent of every metric.
UX-DR20: Include high-impact but purposeful visual polish for insight modules (for example animated transitions, visual summaries, and drill paths) without violating performance or accessibility constraints.

### FR Coverage Map

FR1: Epic 4 - Shared adapter contract and projection model for multi-framework ingestion.
FR2: Epics 1 & 2 - Preserve and complete first-class BMad parsing and rendering behavior.
FR3: Epic 4 - Spec Kit baseline ingestion and projection coverage.
FR4: Epic 4 - GSD and GSD-Pi baseline ingestion and projection coverage.
FR5: Epics 1 & 2 - Coherent navigation/dashboards plus complete artifact-class representation.
FR6: Epic 1 - Requirements, story, and ADR cross-linking integrity.
FR7: Epics 1 & 2 - Markdown fidelity including Mermaid, task lists, and comment annotations.
FR8: Epic 5 - Reliable watch regeneration and rapid-edit safety.
FR9: Epic 3 - Baseline git pulse metrics in the portal.
FR10: Epic 3 - Optional deeper git analytics toggle path.
FR11: Epic 3 - Agent and workflow structural insights with freshness and gap signals.
FR12: Epic 5 - CLI-first generate and watch with auto-discovery and explicit overrides.
FR13: Epic 6 - Read-only VS Code webview reusing shared core logic.
FR14: Epic 7 - Source-code treemap (LOC-sized, git-colorized) as a structural visualization.
FR15: Epic 7 - In-portal code file browsing and source-citation linking to code pages.
FR16: Epic 7 - Activity timeline and per-date pages linked from dates.
FR17: Epic 4 - Additional framework adapters (SpecFlow, Squad, Superpowers) via the shared contract.
FR18: Epic 5 - OSS onboarding and reference documentation.
FR19: Epic 7 - Advanced code-and-git coverage on code pages.

## Epic List

### Epic 1: High-Clarity BMad Portal Experience
Deliver a polished, immediately useful portal for current BMad projects so maintainers and contributors can understand status, traceability, and progress at a glance.
**FRs covered:** FR2, FR5, FR6, FR7

### Epic 2: Complete and Faithful BMad Artifact Representation
Surface and truthfully represent every BMad artifact class and work type — deferred and quick-dev work, specs, sprint status, planning documents, iconography, and authored comments — so the portal reflects the whole project rather than only epics and stories.
**FRs covered:** FR2, FR5, FR7

### Epic 3: Insight Surfaces
Add richer analytical insight — git momentum, planning coverage and freshness, and purposeful dashboard polish — so users can understand project shape, gaps, and momentum quickly.
**FRs covered:** FR9, FR10, FR11

### Epic 4: Multi-Framework Coverage Expansion
Expand beyond BMad to additional spec-driven frameworks (Spec Kit, GSD/GSD-Pi, SpecFlow, Squad, Superpowers) through one shared adapter contract, first generalizing the renderer away from any single project's personal structure.
**FRs covered:** FR1, FR3, FR4, FR17

### Epic 5: Reliable Operations, Configuration, and OSS Documentation
Make generation and watch dependable and easy to configure, and provide OSS-ready documentation, so the tool is trustworthy for daily use and ready to share with the broader community.
**FRs covered:** FR8, FR12, FR18

### Epic 6: VS Code Read-Only Companion Surface
Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects.
**FRs covered:** FR13

### Epic 7: Code and Git Exploration
Let users browse the project's code and history in-portal — turning source citations into navigable code pages and dates into activity timelines, with advanced code-and-git coverage as an opt-in depth.
**FRs covered:** FR14, FR15, FR16, FR19

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

_Scope note: this story is the accessibility + motion baseline (focus states, chart accessible names, skip link/landmark/progressbar ARIA, reduced motion, contrast). The dashboard's visual polish and truthfulness work split out into Story 1.5._

### Story 1.5: Dashboard Insight Polish and Visual Truthfulness

As a stakeholder scanning the dashboard,
I want charts and stats that are visually polished and tell the truth,
So that I can trust what I see and read it at a glance.

**Acceptance Criteria:**

1.
**Given** the dashboard renders stats and charts
**When** I view any panel
**Then** status is shown in one consistent color vocabulary with on-brand, instant tooltips reachable by keyboard, focus, and touch
**And** no chart overstates progress (epic status reflects the story roll-up, task counts are clearly scoped, and future dates are not shown as zero-activity).

2.
**Given** I am looking for what to do next
**When** the dashboard loads
**Then** the most active and next work is surfaced ahead of secondary link grids
**And** key next-step commands can be copied in a single action.

## Epic 2: Complete and Faithful BMad Artifact Representation

Surface and truthfully represent every BMad artifact class and work type — deferred and quick-dev work, specs, sprint status, planning documents, iconography, and authored comments — so the portal reflects the whole project rather than only epics and stories.

**FRs covered:** FR2, FR5, FR7

### Story 2.1: Accurate Work Representation and Authoring Guidance

As a maintainer using multiple BMad workflows,
I want the portal to represent all work types accurately and to guide me in adding more,
So that deferred items and quick-dev work stay visible and new contributors know how to extend the plan.

**Acceptance Criteria:**

1.
**Given** the project contains deferred-work notes and quick-dev spec artifacts alongside epics, stories, and tasks
**When** the site is generated
**Then** those work items are represented as first-class, navigable entries with their status
**And** task and progress figures account for them without misrepresenting epic or story completion.

2.
**Given** an epics or stories surface (including empty or partial states)
**When** I view it
**Then** clear inline guidance explains how to add an epic or a story, with the relevant commands
**And** sunburst and task visuals distinguish "no plan yet" from "no data" so gaps read as next actions.

### Story 2.2: First-Class Rendering of Spec Artifacts

As a maintainer using the spec-driven workflow,
I want the spec kernel and its companion documents surfaced as a first-class artifact class,
So that specs are navigable and understandable rather than dumped in a generic "Other" list.

**Acceptance Criteria:**

1.
**Given** the project contains a specs folder with a SPEC kernel and companion documents (for example architecture spine, rendering architecture, requirements catalog, settings and signals)
**When** the site is generated
**Then** specs render under their own labeled section and navigation with clear titles
**And** they no longer fall into the generic "Other" bucket.

2.
**Given** spec documents cross-reference each other and other artifacts
**When** I open a spec page
**Then** its structure is readable (headings and table of contents) and recognized references resolve
**And** a missing or partial spec set degrades gracefully without broken navigation.

### Story 2.3: Sprint Status Page and Dashboard Widget

As a maintainer tracking delivery,
I want a sprint status view in the portal plus an at-a-glance widget on the home page,
So that I can see where every epic and story sits without opening the tracking file.

**Acceptance Criteria:**

1.
**Given** a sprint-status tracking file exists
**When** the site is generated
**Then** a sprint status page lists epics and stories with their lifecycle status (backlog → ready-for-dev → in-progress → review → done) and surfaces open retrospective action items
**And** missing or partial tracking data degrades gracefully without broken navigation.

2.
**Given** the dashboard home page
**When** it loads
**Then** a compact sprint widget summarizes current status (counts by lifecycle stage and what is in progress) and links to the full sprint page
**And** the widget is omitted cleanly when no tracking file exists.

### Story 2.4: Planning Artifacts Grouping, Status Badges, and PRD Prominence

As a reader arriving at the portal,
I want the planning artifacts organized meaningfully with the PRD front and center,
So that the most important planning documents are easy to find and their status is obvious at a glance.

**Acceptance Criteria:**

1.
**Given** planning artifacts of different kinds (product brief, PRD, PRD quality review, UX design, UX experience)
**When** I view the home planning section
**Then** artifacts are grouped meaningfully (for example the PRD as a prominent primary document, UX design and experience together, the brief distinct)
**And** each artifact's status is shown as a badge consistent with the site's status semantics, not plain text.

2.
**Given** the PRD has an associated quality-review / rubric document
**When** I view the planning section
**Then** the quality review does not appear as a standalone top-level card
**And** it is reachable as a branching/linked reference from the PRD (from the PRD card or its page).

### Story 2.5: Standardized Iconography for Artifact Types and Status

As a user scanning the portal,
I want consistent icons for standardized concepts where they aid recognition,
So that artifact types and statuses are quicker to parse without adding clutter.

**Acceptance Criteria:**

1.
**Given** recurring standardized concepts (artifact types, statuses, navigation sections)
**When** pages render
**Then** appropriate, consistent icons accompany labels where they aid recognition
**And** icons are always paired with text (never icon-only) so meaning is preserved for all users.

2.
**Given** the antiquarian design system and the accessibility conventions from Stories 1.4 and 1.5
**When** icons are used
**Then** they follow the established visual language and remain crisp and theme-consistent
**And** decorative icons are hidden from assistive technology while meaningful icons carry accessible labels.

### Story 2.6: Render Markdown Comments as Visible Annotations

As a reader of generated documents,
I want authored HTML comments surfaced as visible, de-emphasized annotations,
So that the context authors leave in comments (for example "sync this back into the PRD later") is not lost in the rendered portal.

**Acceptance Criteria:**

1.
**Given** a source document contains HTML comments (`<!-- ... -->`) that today render as invisible raw HTML
**When** the page is generated
**Then** those comments render as visible, de-emphasized annotations (italicized or blockquote-styled asides) in their original document position
**And** both multi-line block comments and inline comments render coherently.

2.
**Given** a document mixes prose, headings, and comments
**When** it renders
**Then** comment annotations use a consistent side-note style clearly distinct from body text and do not disrupt the surrounding markdown
**And** malformed, nested, or unterminated comments degrade non-fatally without breaking the page.

## Epic 3: Insight Surfaces

Add richer analytical insight — git momentum, planning coverage and freshness, and purposeful dashboard polish — so users can understand project shape, gaps, and momentum quickly.

**FRs covered:** FR9, FR10, FR11

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

<!-- Story 3.4 retired 2026-07-08 (SCP 2026-07-08). The original artifact disclosure-tree was
     retired, and the source-code treemap it had been rewritten into moved to Story 7.6 (Epic 7) —
     its natural code+git home (LOC + per-file git metrics, drilling into Epic 7 code pages). FR14
     and UX-DR19 now sit in Epic 7. Story number 3.4 is intentionally vacant (as is 3.7). -->


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

### Story 3.6: Refinement Funnel on the Dashboard

As a stakeholder assessing planning maturity,
I want a funnel graphic showing epic → story → task refinement on the home page,
So that I can see how far the project has been refined at a glance.

**Acceptance Criteria:**

1.
**Given** epics, stories, and task plans have been parsed
**When** I view the dashboard
**Then** a funnel visualizes the refinement stages (epics → drafted stories → stories with a task plan → tasks) with counts at each stage
**And** the narrowing communicates how much detail work remains.

2.
**Given** the accessibility and truthfulness conventions established in Stories 1.4 and 1.5
**When** the funnel renders
**Then** each stage carries a text label and value (never color-only) and reduced-motion is respected
**And** an empty or early-stage project renders a sensible funnel rather than a broken or misleading one.

### Story 3.8: Git Insights Hub Page

As a maintainer,
I want a dedicated aggregate "Git Insights" page,
So that I can explore repository activity in depth without cluttering the dashboard.

**Acceptance Criteria:**

1.
**Given** deep git insights are enabled
**When** generation completes
**Then** the portal produces an aggregate Git Insights page summarizing file change frequency, activity over time, and contributor attribution
**And** its tables can be sorted and filtered client-side as a progressive enhancement while remaining readable and navigable without JavaScript.

2.
**Given** the Git Insights page references individual files and commits
**When** I select an entry
**Then** I navigate to the corresponding per-file or per-commit detail page
**And** when deep insights are disabled the heavier hub and detail-page generation does not run and baseline generation performance is unaffected.

## Epic 4: Multi-Framework Coverage Expansion

Expand beyond BMad to additional spec-driven frameworks (Spec Kit, GSD/GSD-Pi, SpecFlow, Squad, Superpowers) through one shared adapter contract, first generalizing the renderer away from any single project's personal structure.

**FRs covered:** FR1, FR3, FR4, FR17

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

### Story 4.2: Decouple Rendering from Personal Project-Structure Assumptions

As a maintainer of a BMad project that is organized differently from the tool author's own repositories,
I want generation to avoid hardcoded personal-structure assumptions,
So that my ADRs, folders, and groupings render correctly without matching one specific layout.

**Acceptance Criteria:**

1.
**Given** a BMad project whose ADRs, folder names, or artifact groupings differ from this repository's personal conventions
**When** the site is generated
**Then** rendering adapts to the detected structure rather than depending on fixed personal assumptions (ADR location/format, hardcoded group-prefix names, specific filenames)
**And** unrecognized structure degrades gracefully rather than mis-grouping or dropping content.

2.
**Given** ADRs authored in non-standard formats or locations
**When** they are parsed
**Then** recognized decision records still render with title, status, and links where derivable
**And** format and organization variance is handled tolerantly (non-fatal), without assuming a single numbering or directory scheme.

### Story 4.3: Spec Kit Baseline Adapter Coverage

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

### Story 4.4: GSD and GSD-Pi Baseline Adapter Coverage

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

### Story 4.5: SpecFlow Baseline Adapter Coverage

As a team using SpecFlow,
I want core SpecFlow artifacts interpreted in the portal,
So that I can track planning and specification progress without switching tools.

**Acceptance Criteria:**

1.
**Given** representative SpecFlow repositories
**When** generation runs
**Then** core planning and specification artifacts render without fatal failures via the shared adapter contract
**And** each discovered artifact is labeled rendered, summarized, or unsupported.

2.
**Given** unsupported SpecFlow artifact variants
**When** they are detected
**Then** they are surfaced as explicit non-fatal notices
**And** generation continues for supported content and remains coherent with other framework surfaces.

### Story 4.6: Squad Baseline Adapter Coverage

As a team using Squad,
I want core Squad artifacts interpreted in the portal,
So that I can track planning progress without switching tools.

**Acceptance Criteria:**

1.
**Given** representative Squad repositories
**When** generation runs
**Then** core planning and tracking artifacts render without fatal failures via the shared adapter contract
**And** each discovered artifact is labeled rendered, summarized, or unsupported.

2.
**Given** unsupported Squad artifact variants
**When** they are detected
**Then** they are surfaced as explicit non-fatal notices
**And** generation continues for supported content and remains coherent with other framework surfaces.

### Story 4.7: Superpowers Baseline Adapter Coverage

As a team using Superpowers,
I want core Superpowers artifacts interpreted in the portal,
So that I can track planning progress without switching tools.

**Acceptance Criteria:**

1.
**Given** representative Superpowers repositories
**When** generation runs
**Then** core planning and tracking artifacts render without fatal failures via the shared adapter contract
**And** each discovered artifact is labeled rendered, summarized, or unsupported.

2.
**Given** unsupported Superpowers artifact variants
**When** they are detected
**Then** they are surfaced as explicit non-fatal notices
**And** generation continues for supported content and remains coherent with other framework surfaces.

## Epic 5: Reliable Operations, Configuration, and OSS Documentation

Make generation and watch dependable and easy to configure, and provide OSS-ready documentation, so the tool is trustworthy for daily use and ready to share with the broader community.

**FRs covered:** FR8, FR12, FR18

### Story 5.1: CLI Generate and Watch Modes with Smart Defaults

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

### Story 5.2: Directory-Scoped Settings with Interactive and CLI Parity

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

### Story 5.3: Watch Regeneration Safety and Scope-Aware Rebuilds

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

### Story 5.4: OSS Onboarding and Reference Documentation

As a prospective adopter from the OSS community,
I want clear getting-started, configuration, and contribution documentation,
So that I can install, run, configure, and contribute to SpecScribe without insider knowledge.

**Acceptance Criteria:**

1.
**Given** a new user arrives at the repository
**When** they follow the documentation
**Then** getting-started steps, a configuration/CLI reference, and contribution guidance are complete, accurate, and current
**And** examples reflect real, working commands.

2.
**Given** the documentation coexists with the tool and generated portal
**When** it is produced
**Then** docs stay consistent with actual behavior (options, defaults, commands) and are easy to keep in sync
**And** missing or partial docs are surfaced rather than silently absent.

## Epic 6: VS Code Read-Only Companion Surface

Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects.

**FRs covered:** FR13

### Story 6.1: Shared View-Model Contract for HTML and Webview Adapters

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

### Story 6.2: Read-Only VS Code Dashboard and Epics Experience

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

### Story 6.3: Host-Aware Theming and Explicit Helper Actions

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

## Epic 7: Code and Git Exploration

Let users browse the project's code and history in-portal — turning source citations into navigable code pages and dates into activity timelines, with advanced code-and-git coverage as an opt-in depth — so the portal explains not just what is planned, but what exists and what happened when.

**FRs covered:** FR14, FR15, FR16, FR19

### Story 7.1: In-Portal Code File Browsing

As a reviewer,
I want project source files rendered as readable pages,
So that I can inspect referenced code without leaving the portal.

**Acceptance Criteria:**

1.
**Given** the project has source files referenced by planning or implementation artifacts
**When** the site is generated
**Then** referenced code files render as syntax-readable, navigable pages
**And** non-referenced or excluded files are omitted gracefully without broken navigation.

2.
**Given** a rendered code file page
**When** I open it
**Then** I can navigate to specific lines via stable anchors
**And** the page degrades safely for very large, binary, or unreadable files.

### Story 7.2: Source-Citation and Comment Linking to Code Pages

As a contributor,
I want source citations and "View source" links to resolve to in-portal code pages,
So that traceability leads somewhere useful instead of to a raw or dead link.

**Acceptance Criteria:**

1.
**Given** artifacts contain source citations (for example `[Source: path:line]`) and view-source links
**When** pages render
**Then** recognized references link to the corresponding code file page, including a line anchor when a line is cited
**And** unresolved references degrade to plain text without broken links.

2.
**Given** a code reference resolves to a code page
**When** I follow it
**Then** I land on the cited file at the cited location
**And** I can navigate back to the citing artifact.

### Story 7.3: Activity Timeline and Date Pages

As a maintainer,
I want a timeline of project activity with per-date pages,
So that I can see what happened on any given day.

**Acceptance Criteria:**

1.
**Given** git history and artifact timestamps are available
**When** I view the timeline surface
**Then** activity is shown over time and each active date links to a date page
**And** dates with no activity are not misrepresented as activity.

2.
**Given** a date page
**When** I open it
**Then** it summarizes what happened that day (commits and artifact changes) and links back to the related epics, stories, code pages, and per-commit detail pages
**And** it degrades gracefully when history is unavailable.

### Story 7.4: Advanced Code and Git Coverage

As an advanced user exploring the codebase,
I want deeper code-and-git coverage on code pages,
So that I can see how files have changed and where change concentrates.

**Acceptance Criteria:**

1.
**Given** code pages and git history are available
**When** advanced coverage is enabled
**Then** code pages surface history/blame-style annotations, per-file change frequency, contributor attribution (who changed the file, not a productivity ranking), and change-coupling/hotspot signals as an opt-in extension
**And** baseline code and portal generation performance is unaffected when it is disabled.

2.
**Given** git history is unavailable or partial
**When** advanced coverage runs
**Then** it degrades non-fatally
**And** code pages still render their baseline content.

### Story 7.5: Per-Commit Detail Pages

As a contributor,
I want a page for each significant commit,
So that I can read what changed and why without leaving the portal.

**Acceptance Criteria:**

1.
**Given** git history is available and detail pages are enabled
**When** I open a commit's page
**Then** it shows the commit subject, full commit message body, author and date, and the files changed with per-file line churn
**And** recognized references in the message (for example "Story N.M" or "FR-9") link to their artifacts.

2.
**Given** a commit page lists changed files and its author
**When** I follow those links
**Then** file entries lead to the corresponding file page and the author is shown as attribution, never as a productivity ranking
**And** page generation is bounded and degrades non-fatally when history is unavailable or partial.

### Story 7.6: Source Code Treemap for Codebase Exploration

As a project reviewer exploring an unfamiliar codebase,
I want a treemap of the source tree sized by lines of code and colorable by git-derived change signals,
So that I can see at a glance where the code mass and the churn live, and drill into any area.

**Acceptance Criteria:**

1.
**Given** a repository with source files
**When** I open the code-map surface
**Then** a treemap renders each source file as a rectangle whose area is proportional to its line count, nested within its directory
**And** the layout is deterministic, with directory labels and clear boundaries.

2.
**Given** deep-git analysis is available
**When** I choose a colorize dimension
**Then** files are shaded by that dimension — change frequency (commit count), relative creation date, relative last-modified date, or average change size — on a non-lifecycle sequential scale with a legend
**And** when git data is unavailable the treemap still renders sized-by-LOC with a neutral fill and a clear notice (graceful degradation).

3.
**Given** I hover or focus a rectangle
**When** the tooltip appears
**Then** it shows the file path, line count, and available git metrics
**And** selecting a file routes to its in-portal code page (Story 7.1) when available, and I can zoom into a directory and back out via a breadcrumb, with drill state deep-linkable (mirroring the sunburst conventions).

4.
**Given** keyboard and screen-reader navigation
**When** I traverse the treemap
**Then** rectangles are focusable with descriptive labels announcing name and metric value
**And** color is never the sole signal (every metric is available as text)
**And** reduced-motion is respected, preserving the Story 1.4/1.5 conventions and NFR6.
