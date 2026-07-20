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
  - docs/MissingFeatures.md
  - docs/Epic3UXFeedback.md
  - docs/UserJourneys.md
---
<!-- 2026-07-09 extension run: FR20–FR31, NFR8, UX-DR21–UX-DR30 extracted from the site-wide UX review; Epics 8–10 with Stories 8.2–8.8, 9.1–9.6, 10.1–10.6 created; final validation in progress. -->

# SpecScribe - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for SpecScribe, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

The epics are ordered by delivery phase: (1) a polished, richly-functional BMad-only portal, (2) deeper insight surfaces and UX, (3) the editor surface (VS Code companion) plus code-and-git exploration, (4) the framework-agnostic foundation and per-framework/module expansion, and finally the release run-up: (5) reliable CLI operations and configuration, (6) a pre-publication code-hardening and release-readiness review, and (7) release engineering and the community preview launch.

**Delivery sequencing (numbers are stable IDs, not run order).** Per the project's append-only / no-renumber convention, epic numbers are permanent identifiers and do not imply execution order. The end-of-roadmap run order is: **Epic 5 (Reliable CLI Operations & Configuration) → Epic 17 (Code Hardening & Release-Readiness Review) → Epic 16 (Release Engineering & Community Preview Launch)** — finalize the operational surface, harden it for public and private codebases, then publish. Epic 18 (BMad Module & Expansion Coverage) is exploratory and sequences alongside the framework-coverage Epics 11–15, not on the release-blocking path. Epic 6's native-integration additions (Stories 6.8–6.12) complete before the hardening pass.

<!-- Delivery-sequence note + phase reorder added 2026-07-11 (SCP 2026-07-11, correct-course): owner-directed end-of-roadmap order (CLI → hardening → publication). Numbers unchanged per append-only convention; run order carried in this note + sprint-status.yaml (the operational truth). -->

_(Requirements FR35–FR36, NFR10 and Epics 17–18 were appended 2026-07-11 — see the correct-course provenance comments below.)_

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

FR20: Publish one canonical status lifecycle per entity type (requirement / epic / story) in the projection model, with each framework's native vocabulary mapped to it at the adapter layer; route every rendered badge through the `--status-*` token system and provide a status-legend affordance reachable from any badge.
FR21: Derive all entity counts (stories, deferred items, action items, and similar) from a single generator-side source of truth consumed by every widget, so summary counts and detail views can never disagree.
FR22: Requirement (FR/NFR) detail pages list their covering stories with current status, completing the requirement → epic → story hop using existing coverage-map data.
FR23: Provide NFR and UX-DR coverage maps parallel to the FR coverage map, with per-item state or a stated verification approach.
FR24: Coverage reporting distinguishes "deferred on purpose" from "unmapped" as separate states with distinct treatment.
FR25: Next-step commands are state-aware: each lifecycle state surfaces one primary recommended command plus any applicable alternate/unhappy-path actions (for example correct-course mid-sprint, retro on done), never surfaces commands inapplicable to the current state, and the command surface per state is adapter-supplied rather than hard-coded.
FR26: Story pages surface a verification-evidence strip (tasks done, tests green, verified date) near the status badge.
FR27: Insight pages (git insights, deep analytics, action items, deferred work) get stable top-nav entry points; the retired Structure page loses its nav slot.
FR28: Every chart carries a legend with real values, its analysis time window, and one framing sentence of why the metric matters.
FR29: Provide a glossary / "how to read this portal" page, first-use acronym expansion, and one-line captions on surfaced commands, with framework-specific vocabulary supplied via the adapter contract.
FR30: Follow-up items (action items, deferred work) carry provenance, resolution criteria, and a link to the resolving story/spec, with de-duplication across source retros; these surfaces degrade gracefully when a framework lacks the underlying artifact types.
FR31: Recency signals ("last updated" markers on dashboard widgets and story cards) are derived entirely at generation time from git timestamps and artifact change logs — no per-visitor state, and a from-scratch CI regeneration produces identical output.

<!-- FR20–FR31 added 2026-07-09 from the site-wide UX review (docs/MissingFeatures.md, docs/Epic3UXFeedback.md, docs/UserJourneys.md); sync back into the PRD for full traceability when convenient. -->

FR32: Provide release engineering — reproducible packaging of the CLI to its chosen distribution channel(s), driven by a tag-triggered release pipeline that attaches release artifacts and supports preview/pre-release channels.
FR33: Package and publish the read-only VS Code extension to the VS Code Marketplace as a preview, dependent on the Epic 6 extension surface existing.
FR34: Provide release-facing documentation — install/upgrade instructions, a changelog, and a stated versioning/pre-release policy for community consumption.

<!-- FR32–FR34 added 2026-07-10 (SCP 2026-07-10, correct-course) to seat Epic 16 (Release Engineering & Community Preview Launch); sync back into the PRD §4.4 for full traceability when convenient. -->

FR35: Provide native VS Code host-integration surfaces beyond the read-only webview panel — extension discoverability/activation, an expanded command surface, native surfaces (a project-outline tree view and status-bar summary), editor↔artifact bridges, and file-change reactivity hardening — all read-only and rendered from core-emitted data (per ADR 0005's JSON-export clause), so the extension feels native without moving rendering out of the C# core or introducing authoring side effects.
FR36: Explore and provide baseline coverage for BMad's own module and expansion ecosystem beyond the BMM core already supported (for example BMad Builder, Creative Intelligence, and game-dev / GDS-style expansions), mapping each module's distinctive artifacts to the shared adapter contract (Epic 4) so BMad users on non-BMM modules see their planning artifacts represented.

<!-- FR35–FR36 added 2026-07-11 (SCP 2026-07-11, correct-course): FR35 seats the VS Code Native-Integration Recommendations (docs/VSCodeIntegrationRecommendations.md, R1–R8) as Epic 6 host-integration surface growth; FR36 seats Epic 18 (BMad module/expansion exploration), distinct from the third-party-framework Epics 11–15. Sync back into the PRD when convenient. -->

### NonFunctional Requirements

NFR1: Baseline generation performance remains responsive for local OSS repositories, with deeper analytics separated from baseline runs.
NFR2: Generation is resilient to partial, malformed, unsupported, or missing artifacts and degrades gracefully with non-fatal notices.
NFR3: Operation is local-first and privacy-preserving, requiring no remote telemetry for core behavior.
NFR4: Architecture is extensible so new framework adapters can be added without core rewrites.
NFR5: Source files are read with shared access and watch mode must not hold write locks on observed files.
NFR6: Cross-surface accessibility semantics (keyboard drill behavior, labels, status text redundancy) are contractual behavior, not optional styling.
NFR7: Feature configurability parity is required across interactive menu flows and equivalent CLI parameters, with directory-scoped settings persistence.
NFR8: Insight surfaces and guidance affordances (status vocabularies, next-step commands, glossary terms, empty-state hints, follow-up/debt artifact types) are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact.
NFR9: Release builds are reproducible and produced by CI from a clean checkout; publishing to any distribution channel is gated on a passing build + test run.

<!-- NFR9 added 2026-07-10 (SCP 2026-07-10, correct-course) for Epic 16. -->

NFR10: SpecScribe is hardened to run safely and correctly against both public and private codebases before community publication: generated output leaks no secrets or unintended private content, rendered surfaces are injection-safe, untrusted-workspace / tool-resolution attack surfaces are closed, dependencies are audited, and no personal-structure assumptions remain — verified by a dedicated pre-publication hardening review.

<!-- NFR10 added 2026-07-11 (SCP 2026-07-11, correct-course) for Epic 17 (Code Hardening & Release-Readiness Review), which runs after feature completion and before Epic 16's publication/cut stories. -->

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
UX-DR21: Each page presents one primary representation per dataset, with alternate views demoted behind a toggle (the sprint page's By Status / By Epic radio-toggle pattern); chart text-twin tables are accessibility contract and are never removed.
UX-DR22: Empty states are designed, not incidental: per-epic consolidated CLI-hint banners (hint text adapter-supplied), intentional empty-column copy, and one copy-able command affordance per context.
UX-DR23: Task progress and workflow state are always paired wherever both appear (for example "5/5 tasks · awaiting review"), and dual-count epic badges are restated as sentences.
UX-DR24: Readiness is self-explanatory: column-level tooltips distinguish backlog from ready-for-dev, and stories lacking task plans are visually separated from actionable ones.
UX-DR25: One date-format token is used portal-wide; ADR listings gain dates and one-line summaries; events sharing a date get sequence markers.
UX-DR26: Acceptance criteria render as visually distinct blocks via existing tokens, and dev-record/dev-notes sections collapse by default on long story pages.
UX-DR27: Wiki-link and file:line reference syntax renders as styled chips or a references appendix, never as raw syntax in prose.
UX-DR28: Long-artifact "On this page" TOCs group subsections under collapsible parents, and every long page keeps an on-page TOC.
UX-DR29: Assumption tags ([ASSUMPTION: …]) are styled via the annotation-comment treatment, and retired work renders in a collapsed section that preserves history without cluttering active lists.
UX-DR30: Insight-chart context polish: distinguish process-coupling from code-coupling in coupling views, annotate or trim pre-project heatmap dead zones, and suppress or reword multi-contributor phrasing when only one contributor exists.

<!-- UX-DR21–UX-DR30 added 2026-07-09 from the site-wide UX review (docs/MissingFeatures.md, docs/Epic3UXFeedback.md, docs/UserJourneys.md). -->

### FR Coverage Map

FR1: Epic 4 - Shared adapter contract and projection model for multi-framework ingestion.
FR2: Epics 1 & 2 - Preserve and complete first-class BMad parsing and rendering behavior.
FR3: Epic 11 - Spec Kit integration spike and baseline ingestion/projection coverage.
FR4: Epic 12 - GSD and GSD-Pi integration spike and baseline ingestion/projection coverage.
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
FR17: Epics 13–15 - Additional framework adapters (SpecFlow, Squad, Superpowers) via the shared contract, each with an integration spike.
FR18: Epic 16 - OSS onboarding and reference documentation (moved from Epic 5 on 2026-07-11; Story 5.4 removed, folded into Story 16.6).
FR19: Epic 7 - Advanced code-and-git coverage on code pages.
FR20: Epic 8 - Canonical status lifecycle per entity type with adapter-layer vocabulary mapping and status legend.
FR21: Epic 8 - Single generator-side count source consumed by every widget.
FR22: Epic 9 - Requirement detail pages list covering stories with status.
FR23: Epic 9 - NFR and UX-DR coverage maps parallel to the FR map.
FR24: Epic 9 - Deferred-on-purpose vs unmapped as distinct coverage states.
FR25: Epic 8 - State-aware next-step commands (primary + unhappy-path, adapter-supplied).
FR26: Epic 9 - Verification-evidence strip on story pages.
FR27: Epic 10 - Insight pages in top nav; Structure nav slot retired.
FR28: Epic 10 - Chart metadata standard (legend, time window, framing sentence).
FR29: Epic 10 - Glossary / portal-orientation page with adapter-supplied vocabulary.
FR30: Epic 9 - Follow-up item provenance, resolution criteria, and de-duplication.
FR31: Epic 8 - Generation-time recency signals from git/change-log data.
FR32: Epic 16 - Release engineering: reproducible CLI packaging and tag-triggered release pipeline.
FR33: Epic 16 - VS Code extension packaging and Marketplace publication (depends on Epic 6).
FR34: Epic 16 - Release-facing documentation, changelog, and versioning policy.
FR35: Epic 6 - Native VS Code host-integration surfaces (discoverability, commands, tree view/status bar, editor bridges, reactivity), seated from the VS Code Native-Integration Recommendations (docs/VSCodeIntegrationRecommendations.md).
FR36: Epic 18 - BMad module/expansion coverage exploration and baseline via the shared adapter contract.
FR37: Epic 19 - Directed work graph across epics, stories, quick-dev, deferred work, reviews, and code (queryable provenance).
FR38: Epic 20 - Interactive project explorer (drill-in zoomable sunburst + related-work side pane) as a progressive enhancement over the static Story 10.7 sunburst.
FR39: Epic 21 - Value & correlation insights (traceability coverage matrix, delivery cadence / cycle-time, planning↔code impact map) derived at generation time.
NFR10: Epic 17 - Pre-publication code hardening and security/privacy review for public + private codebase readiness.

## Epic List

### Epic 1: High-Clarity BMad Portal Experience
Deliver a polished, immediately useful portal for current BMad projects so maintainers and contributors can understand status, traceability, and progress at a glance.
**FRs covered:** FR2, FR5, FR6, FR7 · **UX-DRs:** UX-DR1, UX-DR2, UX-DR3, UX-DR4, UX-DR5, UX-DR6, UX-DR7, UX-DR8, UX-DR9, UX-DR10, UX-DR11, UX-DR12, UX-DR13, UX-DR16, UX-DR17, UX-DR18

### Epic 2: Complete and Faithful BMad Artifact Representation
Surface and truthfully represent every BMad artifact class and work type — deferred and quick-dev work, specs, sprint status, planning documents, iconography, and authored comments — so the portal reflects the whole project rather than only epics and stories.
**FRs covered:** FR2, FR5, FR7

### Epic 3: Insight Surfaces
Add richer analytical insight — git momentum, planning coverage and freshness, and purposeful dashboard polish — so users can understand project shape, gaps, and momentum quickly.
**FRs covered:** FR9, FR10, FR11 · **UX-DRs:** UX-DR20 · **NFRs:** NFR1

### Epic 4: Framework-Agnostic Adapter Foundation
Establish the framework-neutral seam every other framework builds on: one shared adapter contract into the projection model, rendering decoupled from any single project's personal structure, and generation diagnostics — so per-framework coverage epics (11–15) attach without reworking the core templating pipeline. Per-framework coverage moved to its own spike-led epics on 2026-07-10.
**FRs covered:** FR1 · **NFRs:** NFR2, NFR4

### Epic 5: Reliable CLI Operations and Configuration
Make generation and watch dependable and easy to configure, so the tool is trustworthy for daily use. Sequences late in the roadmap (immediately before the Epic 17 hardening pass) so the operational surface is finalized just before hardening and release. OSS onboarding/reference documentation moved to Epic 16 (2026-07-11).
**FRs covered:** FR8, FR12 · **UX-DRs:** UX-DR15 · **NFRs:** NFR5, NFR7

### Epic 6: VS Code Read-Only Companion Surface
Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects.
**FRs covered:** FR13 · **UX-DRs:** UX-DR14 · **NFRs:** NFR6

### Epic 7: Code and Git Exploration
Let users browse the project's code and history in-portal — turning source citations into navigable code pages and dates into activity timelines, with advanced code-and-git coverage as an opt-in depth.
**FRs covered:** FR14, FR15, FR16, FR19 · **UX-DRs:** UX-DR19

### Epic 8: Dashboard Command Center — Trustworthy Status at a Glance
Give the Driver an accurate 30-second pulse and a friction-free path to the next unit of work: one canonical status vocabulary everywhere, counts that always agree, progress and workflow state paired, readiness self-explanatory, and state-aware next-step commands (one primary plus applicable unhappy-path actions). Optimizes the home dashboard for the daily journeys (1–2).
**FRs covered:** FR20, FR21, FR25, FR31 · **UX-DRs:** UX-DR21, UX-DR22, UX-DR23, UX-DR24 · **NFRs:** NFR8

### Epic 9: Traceability and Review Follow-Through
Complete the requirement → epic → story chain so a Stakeholder can click from any requirement to its delivering stories, a Reviewer can judge a "done" claim in one glance, and follow-up items carry provenance and resolution paths — including visibility in the primary remaining-work geometry (sunburst) and coherent Driver/Stakeholder workflows for authoring and satisfaction status. Serves the daily Driver journeys (1–2), the review journey (3), the traceability differentiator (4), and debt follow-through (7).
**FRs covered:** FR22, FR23, FR24, FR26, FR30 · **UX-DRs:** UX-DR26 · **NFRs:** NFR8
<!-- 2026-07-15 (epic-8 retrospective): Stories 9.7–9.9 seated — sunburst/remaining-work follow-ups, authoring/delivery workflow coherence, requirement-satisfaction status at a glance. -->

### Epic 10: Portal Legibility for Every Audience
Make every surface navigable and correctly interpretable by first-time visitors, non-BMAD stakeholders, and tech leads: insight pages reachable from the nav, every chart self-explaining (legend, time window, why-it-matters), vocabulary defined in place, and consistent dates, references, and TOC treatment. Serves onboarding (5) and health-insight (6) journeys — the adoption deciders.
**FRs covered:** FR27, FR28, FR29 · **UX-DRs:** UX-DR25, UX-DR27, UX-DR28, UX-DR29, UX-DR30 · **NFRs:** NFR8

### Epic 11: Spec Kit Coverage
Interpret core Spec Kit artifacts in the portal via the shared adapter contract (Epic 4), led by an integration spike that maps Spec Kit's artifact set to the projection model and pins down unsupported conventions and framework-specific data before baseline coverage begins.
**FRs covered:** FR3

### Epic 12: GSD and GSD-Pi Coverage
Render key GSD and GSD-Pi planning and tracking artifacts coherently alongside other frameworks, led by an integration spike that scopes the GSD family's mapping, coverage tiers, and out-of-model data before baseline coverage lands.
**FRs covered:** FR4

### Epic 13: SpecFlow Coverage
Interpret core SpecFlow specification and planning artifacts through the shared adapter contract, led by an integration spike that maps SpecFlow's artifact set to the projection model and records deliberately-unsupported conventions and framework-extra data.
**FRs covered:** FR17

### Epic 14: Squad Coverage
Interpret core Squad artifacts through the shared adapter contract, led by an integration spike that maps Squad's artifact set to the projection model and identifies unsupported conventions and framework-extra data.
**FRs covered:** FR17

### Epic 15: Superpowers Coverage
Interpret core Superpowers artifacts through the shared adapter contract, led by an integration spike that maps Superpowers' artifact set to the projection model and identifies unsupported conventions and framework-extra data.
**FRs covered:** FR17

### Epic 16: Release Engineering & Community Preview Launch
Everything needed to put a preview build of SpecScribe in the community's hands and keep shipping updates reliably: a reproducible build/test gate, packaged and published CLI distribution, a tag-triggered release pipeline, VS Code Marketplace publication of the read-only extension, OSS onboarding plus release-facing documentation with a changelog and versioning policy, and a preview-launch readiness cut. Led by a packaging-strategy spike (Story 16.1) that fixes the distribution channel(s), versioning/pre-release policy, and publishing prerequisites before the release stories run. Runs last in delivery order, after the Epic 17 hardening sign-off.
**FRs covered:** FR32, FR33, FR34, FR18 · **NFRs:** NFR9 · **Depends on:** Epic 6 (for Story 16.5), Epic 17 (hardening sign-off gates the cut).

### Epic 17: Code Hardening & Release-Readiness Review
A dedicated pre-publication pass to remediate structural weaknesses, inconsistencies, and inefficiencies; close security and privacy gaps so the tool is safe on both public and private codebases; and burn down or explicitly accept the deferred-work and retro-action backlog — producing a release-readiness sign-off that gates Epic 16's publication and cut. Sequences after feature completion (Epics 1–15, 18) and Epic 5, and before Epic 16's publish stories.
**NFRs covered:** NFR10 (also touches NFR1 performance, NFR4 extensibility).

### Epic 18: BMad Module & Expansion Coverage Exploration
Extend first-class BMad support beyond the BMM core to BMad's own module and expansion ecosystem (for example BMad Builder, Creative Intelligence, and game-dev / GDS-style expansions), led by a landscape-and-coverage spike that maps each module's distinctive artifacts to Epic 4's shared adapter contract before baseline coverage. Distinct from the third-party-framework Epics 11–15; exploratory, not release-blocking.
**FRs covered:** FR36

### Epic 19: Directed Work Graph — Traceability Across Artifacts
Make the directed relationships among epics, stories, quick-dev, deferred work, retrospectives/code reviews, and code navigable as a queryable graph — so provenance chains, cycles, and "what stemmed from what" stop living only as breadcrumbs and reverse-link panels.
**FRs covered:** FR37 (seat in PRD when convenient)

### Epic 20: Interactive Project Explorer — Drill-In Sunburst with Related-Work Pane
Turn the static remaining-work sunburst into a fluid, explorable map: click a wedge to zoom in and reveal nested children, breadcrumb back out, with a live related-work side pane. SpecScribe's first rich client-interactive surface; a progressive enhancement over the static Story 10.7 sunburst.
**FRs covered:** FR38 (seat in PRD when convenient)

### Epic 21: Value & Correlation Insights — Traceability, Cadence, and Planning↔Code
High-impact displays that make product value legible and reveal correlations across work items and code: a traceability coverage matrix, delivery cadence / cycle-time, and a planning↔code impact map — all generation-time-derived.
**FRs covered:** FR39 (seat in PRD when convenient)

<!-- Epics 17–18 added 2026-07-11 (SCP 2026-07-11, correct-course): Epic 17 = pre-publication hardening (NFR10), gates Epic 16's cut; Epic 18 = BMad-native module exploration (FR36), distinct from framework Epics 11–15. Append-only, no renumber. Run create-story per story when scheduled (17.1 / 18.1 spike first). -->
<!-- Epic 19 added 2026-07-17: directed work-graph visualization + query across reviews/stories/epics/deferred/code. Exploratory insight surface; spike-led. -->
<!-- Epics 20–21 added 2026-07-19 (SCP 2026-07-19, correct-course): Epic 20 = interactive drill-in sunburst explorer + related-work pane (first rich client-interactive surface, enhances static Story 10.7); Epic 21 = value & correlation insights (traceability matrix, cadence/cycle-time, planning↔code map). Same SCP also added Stories 7.10–7.12 (code risk/ownership/freshness insights) and 10.8–10.11 (unified list grammar + client-light sort/filter + context-aware white-bar nav + sticky section nav). Append-only; the contextual-nav cluster folded into Epic 10 rather than a new epic, freeing 21 for the insights epic. -->

<!-- 2026-07-11: Story 5.4 (OSS onboarding/reference docs) removed from Epic 5 and folded into Epic 16 Story 16.6; FR18 coverage moved Epic 5 → Epic 16. -->

<!-- 2026-07-11: Epic 6 native-integration Stories 6.8–6.12 seated from docs/VSCodeIntegrationRecommendations.md (FR35); existing Stories 5.2/7.1/7.2/8.5/16.5/6.7 annotated with the recommendation IDs they own. -->

<!-- 2026-07-11: Delivery run order (numbers are stable IDs, not execution order) — feature work (Epics 1–4, 6–15) and exploratory Epic 18 → Epic 5 (CLI) → Epic 17 (hardening) → Epic 16 (publication). See the Overview delivery-sequence note; sprint-status.yaml is the operational truth. -->

<!-- Epics 8–10 added 2026-07-09 from the site-wide UX review; Epic 8 is foundational for 9–10 (status model + count source) and these are candidates to run ahead of Epic 4, with all framework-specific content structured as adapter-supplied data per NFR8. -->
<!-- Epics 11–15 added 2026-07-10: per-framework coverage stories 4.3–4.7 extracted into their own spike-led epics (append-only, no renumber). Each epic's Story X.1 is a Framework Integration Spike scoping the mapping to Epic 4's adapter contract; X.2 is the migrated baseline coverage. -->

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
     and UX-DR19 now sit in Epic 7. Story number 3.4 is intentionally vacant; 3.7 was filled
     2026-07-09 by Requirements Flow and Status Blocks. -->


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

### Story 3.6: Story Pipeline Funnel on the Dashboard

<!-- Redirected 2026-07-09 (owner review of the first funnel build): the original epic→story→task
     refinement framing read as requirements maturation, not implementation progress, and its counts
     grow down the pipeline (no honest narrowing). The funnel now shows STORIES flowing through
     delivery stages with cumulative counts; the requirements-maturation vision moved to Story 3.7. -->

As a stakeholder assessing implementation progress,
I want a sideways funnel showing stories flowing through the delivery pipeline (drafted → ready for dev → in development → in review → done) on the home page,
So that I can see how much of the planned work has progressed toward done at a glance.

**Acceptance Criteria:**

1.
**Given** epics and their stories have been parsed with per-story delivery statuses
**When** I view the dashboard
**Then** a funnel visualizes the pipeline stages with a cumulative count at each stage (stories that have reached at least that stage)
**And** the counts are monotonically non-increasing so the narrowing is genuine and communicates how much work remains in flight.

2.
**Given** the accessibility and truthfulness conventions established in Stories 1.4 and 1.5
**When** the funnel renders
**Then** each stage carries a text label and value (never color-only), the cumulative reading is stated in text, and reduced-motion is respected
**And** an empty or early-stage project renders a sensible funnel rather than a broken or misleading one.

### Story 3.7: Requirements Flow and Status Blocks

<!-- Added 2026-07-09 (owner direction during Story 3.6 review): the requirements-maturation
     visualization that Story 3.6 originally drifted toward, now scoped properly. NOTE the data gap:
     FR coverage today is epic-level only (FR Coverage Map → CoverageEpicNumber in RequirementsParser);
     FR↔story links are textual linkification, not a data model. AC 2's flow view requires a
     structured FR→story mapping as part of this story's scope. -->

As a stakeholder tracking requirements maturation,
I want each FR/NFR shown as a colorized status block and a Sankey-style flow of functional requirements from definition through epic coverage into implementation states,
So that I can see how requirements are maturing from definition to delivered at a glance.

**Acceptance Criteria:**

1.
**Given** the requirements inventory has been parsed
**When** I view the requirements page or the dashboard requirements panel
**Then** FRs and NFRs render as a grid of colorized status blocks driven by the shared status tokens
**And** each block carries its id and a text/tooltip status so state is never color-only.

2.
**Given** functional requirements trace into epics and stories
**When** the requirements flow view renders
**Then** a Sankey-style diagram shows FRs flowing from definition through epic coverage into implementation states, backed by a structured FR→story mapping established by this story (extending the epic-level coverage map)
**And** unmapped or deferred requirements appear as honest, labeled flows rather than being dropped.

3.
**Given** the accessibility, truthfulness, token, and motion conventions from Stories 1.4, 1.5, and 3.5
**When** these visualizations render
**Then** they inherit those conventions in full (status tokens only, text alternatives, reduced-motion seams, no overstated progress).

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

## Epic 4: Framework-Agnostic Adapter Foundation

Establish the framework-neutral foundation that additional spec-driven frameworks build on: one shared adapter contract into the projection model, rendering decoupled from any single project's personal structure, and generation diagnostics for degraded runs. Per-framework coverage (Spec Kit, GSD/GSD-Pi, SpecFlow, Squad, Superpowers) is delivered by the spike-led Epics 11–15, which attach to this contract without reworking the core templating pipeline.

**FRs covered:** FR1

<!-- 2026-07-10: Stories 4.3–4.7 (per-framework coverage) extracted into spike-led Epics 11–15 (append-only, no renumber). Epic 4 now holds only the framework-agnostic foundation: 4.1 adapter contract, 4.2 de-personalization, 4.8 diagnostics. -->



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

<!-- Story 4.8 added 2026-07-10: spun out of Story 4.2 so partial/degraded generation is detectable in the
     output itself, not only in console scrollback. Consumes the AdapterDiagnostic channel from Story 4.1. -->
### Story 4.8: Generation Diagnostics and Configuration Log Page

As a maintainer running SpecScribe on a project whose structure or framework differs from the defaults,
I want a generated page that records the run's warnings, skipped or unsupported artifacts, and effective configuration,
So that silent or partial degradation is detectable in the output itself rather than only in console scrollback.

**Acceptance Criteria:**

1.
**Given** a generation run that emits non-fatal diagnostics (unsupported, malformed, or skipped artifacts)
**When** the site is generated
**Then** a diagnostics page lists each notice with its category, source path, and message
**And** the page is reachable from the site (nav or dashboard) and degrades to a clean all-clear state when there are no notices.

2.
**Given** a completed run
**When** the diagnostics page is generated
**Then** it records the effective configuration and detection results (source root, resolved ADR location, output directory, deep-git flag, detected framework/module)
**And** this information is derived entirely at generation time with no remote calls, consistent with local-first operation.

## Epic 5: Reliable CLI Operations and Configuration

Make generation and watch dependable and easy to configure, so the tool is trustworthy for daily use. Sequences late in delivery order (immediately before the Epic 17 hardening pass) so the operational surface is finalized just before hardening and release.

**FRs covered:** FR8, FR12

<!-- 2026-07-11 (SCP 2026-07-11, correct-course): Epic 5 retitled (OSS docs removed). Story 5.4 (OSS Onboarding and Reference Documentation) removed and folded into Epic 16 Story 16.6; FR18 coverage moved Epic 5 → Epic 16. The Story 5.4 slot is intentionally vacant (append-only, no renumber). -->

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

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — owns VS Code recommendation R5.3: the `webview` spawn
     (like `generate`/`watch`) calls `SiteSettings.Resolve()` directly and never consults `SettingsStore`, so a
     repo with saved custom source/ADR/deep-git settings renders with DEFAULTS in the webview today. This story's
     AC #1 parity promise ("configured defaults reused from directory-scoped settings; behavior matches CLI") must
     route `Resolve()` through the settings store for ALL commands, and add a webview-parity test. -->

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

<!-- Story 5.4 (OSS Onboarding and Reference Documentation) removed 2026-07-11 (SCP 2026-07-11, correct-course)
     and folded into Epic 16 Story 16.6 (Release-Facing Documentation, which now OWNS onboarding/reference
     content rather than deferring to 5.4). FR18 coverage moved Epic 5 → Epic 16. Story number 5.4 is
     intentionally vacant per the append-only / no-renumber convention. Original ACs preserved in the
     Sprint Change Proposal (sprint-change-proposal-2026-07-11.md) and now carried by Story 16.6 AC #1/#3. -->

### Story 5.5: Configurable Date-Page "Today" Cutoff (Timezone Policy)

<!-- Seeded 2026-07-20 from Story 10.4 code review: LinkedCommitDays membership uses machine-local
     DateTime.Now as "today" while commit days stay author-offset — rare day-boundary mismatch near TZ
     edges. Owner chose keep machine-local as the default now; expose the policy as a directory-scoped +
     CLI setting when Epic 5 lands (parity with 5.2). -->

As a maintainer generating the portal across machines or timezones,
I want to choose how SpecScribe decides which calendar day is "today" when linking and generating date pages,
So that date-page membership stays predictable for my team's timezone policy without changing the author-offset honesty of commit times.

**Acceptance Criteria:**

1.
**Given** the default configuration (no override)
**When** the portal generates date pages and date links
**Then** "today" remains the generating machine's local calendar day (Story 10.4 status quo)
**And** git commit times continue to render in each commit's authored offset (never `format-local:` / UTC conversion).

2.
**Given** I set a directory-scoped setting and/or CLI override for the date-page today policy
**When** generation runs
**Then** the chosen policy is applied consistently to `LinkedCommitDays`, date-page generation, and guarded date links
**And** at least these policies are supported: machine-local (default), UTC calendar day, and an author-local-derived cutoff (e.g. max series / last-commit day)
**And** effective config + provenance appear on the diagnostics/config log surface (Story 4.8) with interactive/CLI parity (NFR7 / Story 5.2).

## Epic 6: VS Code Read-Only Companion Surface

Expose the same shared projection in a read-only VS Code webview for in-editor visibility without introducing authoring side effects, and grow the extension's native host-integration surface (discoverability, commands, tree view/status bar, editor bridges, reactivity) so it feels native — all read-only and rendered from core-emitted data.

**FRs covered:** FR13, FR35

<!-- 2026-07-11 (SCP 2026-07-11, correct-course): FR35 + Stories 6.8–6.12 added to seat the VS Code
     Native-Integration Recommendations (docs/VSCodeIntegrationRecommendations.md, R1–R8). Stories 6.1–6.7
     unchanged. The two Epic 6 invariants hold throughout: rendering stays in C#, the extension stays
     read-only (ADR 0005 AD-1/AD-2, ADR 0003 AD-6). Several recommendations seat in OTHER stories they
     belong to (annotated in place): R5.3→5.2, R4.2→7.1/7.2, R4.3→8.5, R1.4/R1.6/R8.2→16.5, R8.3→6.7. -->

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

As a maintainer,
I want the dashboard and epics page bodies decomposed into shared, host-neutral section view models in the rendering core (HTML adapter re-rendering them byte-for-byte identically),
So that a future VS Code webview can render those two surfaces from the same typed data rather than scraping the HTML surface.

<!-- 2026-07-10: Story 6.2 was SPLIT at create-story (owner-confirmed). It now covers ONLY the
     rendering-core body decomposition (former AC #1). The webview RUNTIME (former AC #2 + #3 — the
     in-editor webview UI + live host-push) relocated to the new Story 6.4, because no VS Code
     extension exists in the repo yet (greenfield surface, new tech stack) and fusing it here would
     absorb a new structural surface into one un-reviewable story (Epic 2 retro: "split, don't
     absorb"). The AC #1 authoring note below already anticipated this seam. -->

<!-- 2026-07-10: AC #1 added to name the dashboard/epics page-BODY decomposition as an explicit
     foundational task of this story, not an implicit consequence of AC #2. Story 6.1 delivers the
     view-model contract + shared page CHROME (nav/breadcrumb/shell) but deliberately leaves page
     bodies opaque; the dashboard + epics bodies are the only bodies a webview consumer renders, so
     their decomposition into shared section view models lands HERE. Per the Epic 2 retro rule
     ("split, don't absorb a new structural surface"), it is surfaced as its own AC/scope line so the
     structural work is reviewed on its own terms rather than buried inside the runtime-webview ACs. -->

**Acceptance Criteria:**

1.
**Given** Story 6.1's view-model contract carries page bodies as opaque payloads
**When** the dashboard and epics surfaces are prepared for the webview
**Then** the dashboard and epics page bodies are decomposed into shared, host-neutral section view models in the rendering core
**And** the HTML adapter re-renders them byte-for-byte identically (parity harness green)
**And** no other page body is decomposed (only the surfaces a webview consumer renders).

<!-- Former AC #2 (webview display) and AC #3 (live host-push) relocated 2026-07-10 to Story 6.4. -->

### Story 6.3: Host-Aware Theming and Explicit Helper Actions — RENUMBERED to Story 6.5

<!-- 2026-07-10: RENUMBERED 6.3 → 6.5 at create-story (owner-directed). This is a SEQUENCING fix, not a
     scope change. Host theming + helper actions both presuppose a rendering VS Code webview, which does
     NOT exist until Story 6.4 (the webview runtime) ships — so this story must sort AFTER 6.4, not before
     it. Rather than carry the "runs after 6.4 despite sorting before it" footnote indefinitely (the note
     6.4's split already had to add), the story number now matches the dependency order. Append-only /
     no-renumber-of-6.4 per project convention (like 4.8 out of 4.2, Epics 11-15, and 6.4 out of 6.2): 6.4
     keeps its number, the theming story moves to the next free slot (6.5), and this 6.3 slot is retired
     with this breadcrumb. Full ACs + content now live under Story 6.5 below. -->

### Story 6.4: Read-Only VS Code Webview Runtime for Dashboard and Epics

<!-- 2026-07-10: Split out of Story 6.2 at create-story (append-only, no renumber per project
     convention — like 4.8 out of 4.2 and Epics 11-15). Carries the former Story 6.2 AC #2 + #3 (the
     actual webview runtime + live host-push). AC #1 here is the JSON view-model export that the
     webview consumes — the owner-chosen data path (chosen over "run the tool and load the generated
     HTML" and "a second HTML-ish render adapter"). DEPENDS ON Story 6.2 (the section view models it
     serializes). SEQUENCING: runs AFTER 6.2 and BEFORE 6.3 (host theming depends on the webview
     existing), even though its number sorts after 6.3. Context: there is NO VS Code extension in the
     repo yet — greenfield surface, new tech stack (TypeScript/extension host/webview). Backlog: run
     create-story to detail it when scheduled. -->

As a VS Code user,
I want an in-editor status surface for dashboard and epics that stays live as the project changes,
So that I can inspect project state without context-switching to a browser.

**Acceptance Criteria:**

1.
**Given** Story 6.2's section view models describe the dashboard and epics surfaces as host-neutral data
**When** the webview needs that data
**Then** the rendering core exposes a JSON view-model export of those section view models
**And** the export carries the section data itself (not scraped HTML) with no dependence on the HTML surface's enhancement scripts.

2.
**Given** the extension opens the status webview
**When** project data is loaded
**Then** dashboard and epics views display with the same core interaction-state semantics as HTML
**And** in-editor navigation is responsive and readable.

3.
**Given** source artifacts change while the webview is open
**When** host updates are pushed
**Then** visible status refreshes in place without full panel reset
**And** drill/breadcrumb context remains coherent.

### Story 6.5: Host-Aware Theming and Explicit Helper Actions

<!-- 2026-07-10: RENUMBERED from Story 6.3 (owner-directed sequencing fix — see the retired 6.3 breadcrumb
     above). ACs verbatim from the former Story 6.3. DEPENDS ON Story 6.4 (the webview runtime this story
     themes and hangs helper buttons on) and Story 6.2 (the section view models 6.4 renders). Sequences
     LAST in Epic 6. -->

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

### Story 6.6: Delivery Architecture & Distribution Spike

<!-- 2026-07-10: Appended via correct-course (SCP 2026-07-10 delivery-architecture, owner-directed). Seated in
     Epic 6 because it reopens ADR 0005 (an Epic 6 artifact) and gates 6.4/6.5, though its scope is
     APPLICATION-WIDE (not webview-only). Immediately after ADR 0005 was Accepted on the "rendering stays in
     C#, bundle a 73 MB self-contained binary" premise, the owner leaned toward a JSON data layer + a
     client-side SPA distributed via npx. This spike MEASURES that direction rather than committing to it
     (mirrors the Story 6.3 spike pattern). Its deliverable is ADR 0006, which supersedes-or-reaffirms ADR
     0005. Stories 6.4 + 6.5 and Epic 16 packaging (16.1/16.3/16.4/16.5) are frozen pending ADR 0006. Full ACs
     + tasks live in the story file 6-6-delivery-architecture-and-distribution-spike.md. Note: epics.md's
     Epic 6 numbering reconciliation (host-theming still headed "Story 6.3" above; no spike entries) remains
     the pre-existing deferred follow-up — sprint-status.yaml is the operational truth. -->

As the SpecScribe maintainer,
I want a hands-on spike that measures whether SpecScribe's delivery architecture should pivot toward a JSON data layer + a small client-side renderer (SPA) distributed via npx — versus the current C# static-site generator + (per ADR 0005) a bundled self-contained binary — decided by real numbers and recorded as ADR 0006,
So that we either commit to the pivot with evidence (and re-plan Epics 6/16) or re-affirm the C# path knowing exactly what we're trading away, before any code is rewritten and before Story 6.4 is built on a premise that may not hold.

**Acceptance Criteria (spike — decision-first, throwaway):**

1.
**Given** a thinnest end-to-end slice (C# core emits a JSON data layer for the dashboard + epics section view models; a minimal client renderer renders them)
**When** it runs against this repo
**Then** the spike measures output-file count vs. today's static site (extrapolated to Epic-7 scale), total + JSON byte size (and whether chunking is needed), and client render/interaction performance at the largest realistic dataset.

2.
**Given** the owner wants npx-executable distribution
**When** the spike prototypes it
**Then** it proves at least the npm-wrapper-around-native-binary path (`npx` runs the self-contained tool with no .NET SDK), measuring package size, cold-run latency, and the cross-platform story, compared against `dnx`/`dotnet tool` and a hypothetical full-TS CLI.

3.
**Given** "pure TypeScript for the application" implies porting the analysis core
**When** the spike assesses it
**Then** it enumerates the C# surface a port would replace (parsers, projection, GitMetrics/deep-git, coverage, charts, the 667-test suite) with an effort/risk estimate, and evaluates coupling-breakers (WASM-compiled core callable from Node; or a pre-generated-JSON model) — without performing a production port.

4.
**Given** the measured evidence
**When** the spike concludes
**Then** a new `docs/adrs/0006-*.md` records the decision across all four axes (output form, rendering language, analysis language, distribution), explicitly supersedes-or-reaffirms ADR 0005, and rules on the accessibility posture (NFR6 / progressive-enhancement) for any JS-rendered surfaces
**And** docs/adrs/README.md is updated (and ADR 0005 gets a supersede note if superseded).

5.
**Given** ADR 0006's decision
**When** the spike concludes
**Then** it names the concrete follow-on: pivot → a correct-course re-planning Epics 6 (6.4/6.5) and 16 (packaging → npm/npx) and whether the C#-core-port is its own epic; re-affirm → unfreeze 6.4/6.5 and 16.1.

6.
**Given** a spike produces throwaway code
**When** it lands
**Then** no production pivot merges to `main` as product (quarantined under `spike/` or branch-only), the generated site stays byte-identical, and read-only (AD-6) is honored.

### Story 6.7: JSON + Client-Renderer (SPA) Delivery Adapter

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — carries VS Code recommendation R8.3: keep the WebviewBundle
     payload shape compatible with this story's JSON data-layer schema, so the webview can OPTIONALLY consume
     committed/CI-generated JSON for instant first paint (with the live spawn refreshing behind it). Design note
     only — a JSON-only consumer cannot regenerate, so the binary remains the live path (the ADR 0006 trade-off). -->

<!-- 2026-07-10: Seated by ADR 0006 (Accepted) as an ADDITIVE delivery option — see docs/adrs/0006. The 6.6
     spike proved the file-count concern (Epic-7 scale reaches thousands of files) is real, and that a JSON +
     client-renderer output form addresses it WITHOUT porting the C# core: it is a second C# IRenderAdapter over
     the shared view models. Rendering stays in C#; the static-HTML surface remains the accessible baseline. Full
     ACs via create-story when scheduled. Depends on Story 6.1 (IRenderAdapter seam) + Story 6.2 (section view
     models). Note: this does NOT reduce bytes (chart SVGs still ship) — only file count; a true byte reduction
     would require the deferred TS port (ADR 0006 option D). -->

As a maintainer generating a portal for a large repository,
I want an optional delivery form that emits a JSON data layer plus a small client-side renderer instead of thousands of static HTML files,
So that file-count-heavy projects (Epic-7 scale) stay manageable while rendering remains in the C# core and the accessible static-HTML fallback is preserved.

**Acceptance Criteria:**

1.
**Given** the shared section view models (Story 6.2) and the `IRenderAdapter` seam (Story 6.1)
**When** the JSON+SPA delivery adapter runs
**Then** it emits a JSON data layer (with charts as pre-rendered inline SVG) plus a small client renderer that renders the surfaces from it, as a second concrete `IRenderAdapter` — with rendering staying in C# and no core port.

2.
**Given** NFR6 and the progressive-enhancement policy (JS never the sole carrier of information)
**When** the JSON+SPA form is produced
**Then** a static/`noscript` fallback is shipped (the C# core already emits the pre-rendered HTML), so core content and navigation work with JavaScript disabled.

3.
**Given** this is an additive output form
**When** it is selected
**Then** the existing static-HTML surface and the golden byte-parity gate are unaffected (opt-in; no change to default generation).

<!-- Stories 6.8–6.12 added 2026-07-11 (SCP 2026-07-11, correct-course) to seat the VS Code Native-Integration
     Recommendations (docs/VSCodeIntegrationRecommendations.md). Each story names the R-items it delivers.
     Constraints (per §2 of that doc): rendering stays in C#; the extension stays read-only; VS Code settings
     carry HOST concerns only (project behavior stays in directory-scoped settings, ADR 0003); the generated
     HTML surface stays byte-identical (golden fingerprint unaffected); status surfaces derive from the six
     core-emitted --status-* stages, never re-mapped onto VS Code's 3-severity palette. Delivery order per the
     doc's §4 waves: 6.8 (the "Now" quick-dev wave, incl. the Workspace-Trust hole that MUST close before the
     16.5 Marketplace publish) → 6.9–6.11 (the "Next" story-sized wave) → 6.12 (diagnostics, rides Story 4.8).
     All complete before the Epic 17 hardening pass. Run create-story per story when scheduled. -->

### Story 6.8: Extension Discoverability, Workspace Trust, and Command Surface

<!-- Seats recommendations R5.4 (Workspace Trust — must land before Story 16.5 Marketplace publish),
     R1.1–R1.3 (activation events, context keys, explorer/editor menus), R2.1–R2.4 (direct-open / refresh /
     generate-watch terminal-handoff / open-generated-site commands), R3.3 (open-beside + specscribe.openLocation),
     R5.2 (open project settings), R7.1–R7.3 (cold-start progress, actionable error notification, panel icon).
     All manifest/routing changes reusing the existing spawn/panel machinery — no new rendering. -->

As a VS Code user with a spec-driven repository,
I want the extension to announce itself and offer more than one way in — activating on project detection, contributing menus and direct-open/refresh commands, and declaring a safe workspace-trust posture,
So that I can discover and drive SpecScribe natively instead of having to already know a single command exists.

**Acceptance Criteria:**

1.
**Given** a workspace that contains SpecScribe artifacts (detected by path existence only, no content parsing)
**When** the folder is opened
**Then** the extension activates, sets a `specscribe.projectDetected` context key, and its menu/command contributions appear only in such repos (gated by `when` clauses)
**And** repos without spec artifacts see no SpecScribe noise.

2.
**Given** the extension spawns a workspace-adjacent binary and honors a `toolPath` setting
**When** the manifest declares workspace-trust capabilities
**Then** untrusted workspaces cannot override `toolPath` (declared via `capabilities.untrustedWorkspaces` with `restrictedConfigurations`), closing the tool-resolution attack surface, while user/machine-level values still apply
**And** this posture is in place before the Story 16.5 Marketplace publish.

3.
**Given** the user wants to reach SpecScribe from the editor
**When** command and menu contributions are used
**Then** direct-open (Dashboard/Epics), refresh, open-generated-site, and explorer/editor-title "Open in SpecScribe Status" entries all route through the existing read-only spawn/panel path, the panel can open beside the active editor per a `specscribe.openLocation` host setting, and "Open Project Settings" reveals the directory-scoped settings file without SpecScribe writing it
**And** generate/watch commands are staged into an integrated terminal for the user to run (SpecScribe never executes a write to the project's output).

4.
**Given** cold start and error paths
**When** the panel is opening or a spawn fails
**Then** first paint shows a progress/heartbeat affordance and failures surface an actionable notification (set `toolPath` / retry), and the panel tab carries a SpecScribe icon
**And** no recommendation in this story mutates a project artifact.

### Story 6.9: Native Project Outline — Tree View and Status Bar

<!-- Seats R3.1 (activity-bar TreeView: epics → stories with status, via a new core JSON outline export —
     the ADR 0005 §1 "JSON export for a non-webview consumer" clause), R3.2 (status-bar summary item), and
     R1.5 (viewsWelcome empty state). Requires contributing six specscribe.status.* theme colors (light/dark/
     highContrast, mirroring the Story 6.5 accent tuning) so ThemeIcon-based status icons stay on the semantic
     --status-* stages rather than host severities. New structural surface — its own story per "split, don't
     absorb". -->

As a VS Code user,
I want a persistent SpecScribe outline in the sidebar and a status summary in the status bar,
So that I can glance at epic/story status and jump to any surface without opening the webview panel.

**Acceptance Criteria:**

1.
**Given** the rendering core exposes a host-neutral outline export (epic/story id, title, status stage, counts, surface path, source artifact path) — added as a new `outline` payload or `specscribe outline` command, not scraped HTML
**When** the extension renders its activity-bar tree view
**Then** epics and their stories appear as tree nodes mapped 1:1 from the export, with status conveyed by icons derived from the six core-emitted `--status-*` stages (via contributed `specscribe.status.*` theme colors, not VS Code's 3-severity palette)
**And** an empty/undetected workspace shows a `viewsWelcome` guidance state rather than a dead view.

2.
**Given** the tree view and a status-bar item
**When** the user interacts with them (all read-only)
**Then** clicking a node reveals that surface in the webview panel, context actions open the source markdown or copy the story's helper prompt, and the status-bar item shows a summary count (e.g. active/review) that opens the status panel
**And** a failed refresh is shown as a stale/error indicator rather than silently wrong data.

### Story 6.10: Editor ↔ Artifact Bridges (Reveal-Source)

<!-- Seats R4.1 (reveal-source from the webview → showTextDocument). Also establishes the structured-link
     seam that R4.2 (Epic 7 code citations → showTextDocument at a line) and R4.3 (Story 8.5 next-step
     command → terminal handoff) plug into — those two implement in their owning stories (7.1/7.2, 8.5),
     annotated there. Read-only: opens editors, changes nothing. -->

As a VS Code user,
I want to jump from a surface in the webview straight to the artifact that produced it,
So that the portal and my files feel like one thing rather than two disconnected views.

**Acceptance Criteria:**

1.
**Given** the webview payload carries source-artifact paths on its surface/section metadata
**When** I trigger "reveal source" on a surface or section in the webview
**Then** a `revealSource` host message opens that markdown file via `showTextDocument` (read-only navigation, no mutation)
**And** the path resolution reuses the core-resolved roots (no duplicated path assumptions).

2.
**Given** future code-citation (Epic 7) and next-step-command (Story 8.5) surfaces
**When** those surfaces emit links
**Then** the core emits them with structured data attributes (e.g. `data-code-path`/`data-line`, or command text) so the VS Code host can re-target them natively (editor at a line; command staged in a terminal), while the HTML surface keeps its portal/GitHub links
**And** the re-targeting behavior itself is implemented in the owning stories (7.1/7.2, 8.5), this story only guarantees the seam exists.

### Story 6.11: File-Change Reactivity Hardening

<!-- Seats R6.1 (the shipped live-data DEFECT: non-.md sources — sprint-status.yaml, _bmad/config.toml —
     never trigger refresh, in BOTH the extension globs and the core FileWatcherService Filter="*.md" plus its
     three .md-enforcing sites; already recorded in deferred-work.md), R6.2 (derive watch roots from the core's
     resolved source/ADR roots instead of hardcoded globs), R6.3 (visibility-aware refresh: mark dirty while
     hidden, render on reveal). R6.4 scoped-re-render is the ADR 0005 §3 follow-up already tracked in
     deferred-work; fold it here or leave as its noted 6.4 polish item. -->

As a VS Code user with the status panel open,
I want edits to every data source the portal reads to refresh the view — not just markdown,
So that the panel never silently shows stale sprint or config data.

**Acceptance Criteria:**

1.
**Given** the "stays live" promise (Story 6.4 AC #3) and that `sprint-status.yaml` / `_bmad/config.toml` feed the dashboard
**When** those non-`.md` sources change while the panel is open
**Then** the view refreshes for them — fixed in both layers: the extension watch globs and the core `FileWatcherService` (its `Filter`, its debounce re-guard, and its fire-time dispatch, which needs a "regenerate the surfaces this feeds" route for yaml/toml rather than the `.md`-only artifact routes)
**And** the fix is a reviewed change, not a drive-by glob edit (per the deferred-work note).

2.
**Given** a repository configured with non-default source/ADR roots (Story 5.1/5.2)
**When** the extension sets up its file watchers
**Then** the watched paths are derived from the core-resolved roots carried in the webview payload (workspace-relative), not the hardcoded `_bmad-output/`/`docs/adrs/` globs
**And** a hidden panel marks itself dirty and re-renders once on reveal rather than re-spawning per change while hidden.

### Story 6.12: Native Diagnostics — Problems Panel Integration

<!-- Seats R8.4 (map the per-artifact generation errors the `specscribe webview` command already streams on
     stderr into VS Code Diagnostics on the offending files). Depends on Story 4.8's diagnostics work for the
     core-owned structured (JSON-lines: path/message/severity) format; rides with or after 4.8. Pure data
     transport — arguably the most "native" integration in the recommendations. -->

As a VS Code user,
I want SpecScribe's per-artifact generation warnings to appear in the Problems panel on the offending files,
So that broken or unsupported artifacts surface where every other tool's errors live.

**Acceptance Criteria:**

1.
**Given** the core emits per-artifact generation notices in a structured, core-owned format (JSON lines: path, message, severity — the same channel Story 4.8's diagnostics page consumes)
**When** the extension receives them
**Then** it maps each to a VS Code `Diagnostic` anchored to the offending artifact file, clearing them when a later run resolves the notice
**And** this remains pure read-only data transport (no artifact is modified).

2.
**Given** Story 4.8 owns the diagnostics format and page
**When** this story is scheduled
**Then** it consumes that format rather than defining a parallel one, and degrades cleanly (no diagnostics surfaced) when the core emits none
**And** it stays coherent with the diagnostics page so the two never disagree.

## Epic 7: Code and Git Exploration

Let users browse the project's code and history in-portal — turning source citations into navigable code pages and dates into activity timelines, with advanced code-and-git coverage as an opt-in depth — so the portal explains not just what is planned, but what exists and what happened when.

**FRs covered:** FR14, FR15, FR16, FR19

### Story 7.1: In-Portal Code File Browsing

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — carries VS Code recommendation R4.2 (with 7.2): design the
     code-citation links so a host can re-target them. Emit code links with structured data attributes
     (data-code-path, data-line) so the VS Code webview can map a click to showTextDocument(file, {selection:line})
     instead of an in-portal code page, while the HTML surface keeps its portal/GitHub links. Design the seam in
     here, don't retrofit; the webview re-targeting itself rides Story 6.10's link seam. -->

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

3.
**Given** a rendered code file page for a file cited by one or more artifacts
**When** I open it
**Then** the page leads with a relationship view — a node-link graph of the artifacts that reference the file, each node linking to that artifact — and treats the source itself as secondary supporting detail
**And** the reference relationships are also available as a plain text list (never colour- or image-only), and the per-line anchors stay reachable so citation deep links continue to land.

4.
**Given** a rendered code file page for a recognized language
**When** I open it with JavaScript enabled
**Then** the source is syntax-highlighted by language (detected from the file extension), with multi-line constructs coloured correctly
**And** with JavaScript disabled — or for an unrecognized file type — the page still renders as legible monospace with working line numbers and line anchors (highlighting is a pure progressive enhancement, vendored offline, not a CDN dependency).

### Story 7.2: Source-Citation and Comment Linking to Code Pages

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — owns VS Code recommendation R4.2 (link resolution): when this
     story defines how source citations resolve to code pages, emit the structured data attributes (data-code-path,
     data-line) that let the VS Code host re-target citations to native editor navigation. Pairs with Story 7.1
     and plugs into Story 6.10's reveal seam. -->

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

### Story 7.7: External Source Linking and Auto-Detection

As a maintainer whose repository is hosted on a platform like GitHub or GitLab,
I want each in-portal code page to link out to the same file's hosted source, with the base URL detected automatically,
So that readers can reach the canonical, syntax-highlighted source without my having to hand-configure a URL.

**Acceptance Criteria:**

1.
**Given** a repository with a recognizable hosting remote, or a GitHub Pages / CI deployment context
**When** the site is generated without an explicit source-URL override
**Then** the external source base is derived automatically from the git remote or the deployment environment
**And** an explicit override always takes precedence, and an unrecognizable or absent remote degrades to in-portal-only with no error.

2.
**Given** an external source base is configured or detected
**When** code pages are generated
**Then** in-portal code pages are still generated and each gains an additive "view source online" link to the hosted file
**And** source citations continue to resolve to the in-portal pages — the external base is additive, never a replacement — and the setting is reachable from both the CLI and the interactive menu (NFR7).

### Story 7.8: Related Files in the Reference Graph

As a reviewer exploring a code file,
I want the file's reference graph to also show the files it most frequently changes alongside,
So that I can see a file's real neighbourhood — the artifacts that cite it and the code that co-evolves with it — in one view.

**Acceptance Criteria:**

1.
**Given** deep-git analysis is available (the change-coupling / co-change data SpecScribe already computes)
**When** a code page's reference graph renders
**Then** the graph also includes nodes for the files most frequently changed together with this file, visually distinguished from the citing-artifact nodes and linking to those files' code pages
**And** each related-file node carries a rich tooltip (the file and its co-change strength), and the graph degrades to citations-only when deep-git data is unavailable.

2.
**Given** the graph now carries both citing-artifact and related-file nodes with tooltips and clickthroughs
**When** the page renders
**Then** the graph is the single relationship surface — no redundant visible list duplicating what the nodes already convey
**And** an accessible text equivalent of every node/link is still present for assistive tech (NFR6/UX-DR16), and node/edge counts stay bounded so a hub file's graph remains legible.

<!-- 2026-07-18 (owner-directed, append-only): Story 7.9 seated — Code Map file-type colorize with a
     discrete/categorical palette (orthogonal to Story 10.6's coupling process-path heuristic). -->

### Story 7.9: Code Map File-Type Colorize (Discrete Palette)

As a reviewer exploring an unfamiliar codebase on the Code Map,
I want a colorize dimension that paints tiles by **file type / language** using a **discrete (categorical) color scheme**,
So that I can see at a glance where C#, TypeScript, CSS, config, and other kinds of mass live — without confusing that view with sequential churn/recency ramps.

**Acceptance Criteria:**

1.
**Given** the Code Map (Story 7.6) with its existing sequential git-metric colorize dimensions
**When** I choose a **File type** (or equivalent) colorize dimension
**Then** each file tile is filled from a **discrete palette** keyed by extension/language family (not a sequential ramp like change-frequency or recency)
**And** a legend lists each category with its swatch and a human label, and color is never the sole signal (path + type remain available as text / tooltip / table).

2.
**Given** unknown or rare extensions
**When** the dimension renders
**Then** they map to a documented "Other" (or similar) bucket rather than inventing unbounded colors
**And** the dimension degrades cleanly when the map has no files (existing empty/neutral path), and reduced-motion / a11y conventions from Story 7.6 are preserved.

3.
**Given** this dimension is categorical
**When** it is implemented
**Then** it does **not** change Story 10.6's coupling process-vs-code classifier (orthogonal concern) and does not require rewriting the sequential metric dimensions
**And** HTML + webview + SPA stay coherent on the shared code-map surface.

<!-- Stories 7.10–7.12 added 2026-07-19 (SCP 2026-07-19, correct-course): correlation/risk code insights on top of
     the existing deep-git signals (churn, size, author, last-commit). All extend GitMetrics.TryComputeDeep /
     ParseNumstatLog (the single --deep-git numstat path — no second git log), reuse the Story 7.2 code-page link
     seam and the Story 10.2 chart-metadata standard, and degrade on shallow/non-git/solo repos (NFR8). -->

### Story 7.10: Refactor-Target Risk Quadrant (Churn × Size)

As a tech lead deciding where to invest cleanup,
I want files plotted by how often they change against how large they are,
So that the high-churn, high-size quadrant surfaces refactor targets instead of me guessing.

**Acceptance Criteria:**

1.
**Given** deep-git numstat change-frequency data and per-file size already computed
**When** the quadrant renders
**Then** each file is a point on change-frequency × size axes with the high/high quadrant visually flagged as elevated risk
**And** points link to their code page via the Story 7.2 seam, with a Story 10.2-compliant legend, axes, and framing sentence.

2.
**Given** a shallow or non-git repo, or a repo too small to be meaningful (NFR8)
**When** the underlying data is thin
**Then** the chart is omitted or shows a designed empty state rather than an axis of one dot
**And** "complexity" remains a **size proxy only** — this story does not add a cyclomatic-complexity analyzer; a real complexity metric would be a separate story.

### Story 7.11: Code Ownership & Bus-Factor Insights

As a maintainer assessing project resilience,
I want to see how concentrated authorship is across the codebase,
So that knowledge silos ("only one person has touched this") become visible before they become a risk.

**Acceptance Criteria:**

1.
**Given** deep-git author attribution
**When** the ownership view renders
**Then** each file or area shows its dominant-author share and contributor count, and single-author concentrations are flagged as bus-factor risks using the existing sole-contributor vocabulary (`GitInsightsTemplater`)
**And** entries link to their code page (Story 7.2 seam).

2.
**Given** a solo-maintainer repo (the common OSS case, NFR8)
**When** ownership would trivially be "one person everywhere"
**Then** the surface reframes honestly (e.g., "single-maintainer project") rather than flagging every file as a bus-factor risk
**And** the classification is generation-time deterministic (FR31).

### Story 7.12: Code Freshness / Age Map

As a newcomer orienting to a codebase,
I want to see which areas are actively evolving versus long-untouched,
So that I can tell load-bearing hot code from stable or possibly-dead corners.

**Acceptance Criteria:**

1.
**Given** each file's last-commit date from the deep-git path
**When** the freshness map renders
**Then** files are shaded by recency of last change, reusing the `--status-*` / heat token system (not a new palette) with a real-value legend per the Story 10.2 chart-metadata standard
**And** color is never the sole signal (path + date remain available as text / tooltip).

2.
**Given** generation-time determinism (FR31, NFR3)
**When** freshness is computed
**Then** it derives from git timestamps only — no per-visitor "now" drift — and a from-scratch CI regeneration produces identical output
**And** non-git repos omit the surface cleanly (NFR8).

## Epic 8: Dashboard Command Center — Trustworthy Status at a Glance

Give the Driver an accurate 30-second pulse and a friction-free path to the next unit of work: one canonical status vocabulary everywhere, counts that always agree, progress and workflow state paired, readiness self-explanatory, and state-aware next-step commands. Optimizes the home dashboard for the daily journeys (1–2) defined in docs/UserJourneys.md.

**FRs covered:** FR20, FR21, FR25, FR31 · **UX-DRs:** UX-DR21, UX-DR22, UX-DR23, UX-DR24 · **NFRs:** NFR8

<!-- 2026-07-14 (epic-7 retrospective, correct-course): Story 8.1 inserted per Epic 6 Retrospective Action
     Item #3 (every net-new epic verifies cross-surface reach before dev starts). Stories 8.1-8.7 were already
     drafted with none started, so this was the last clean window - same pattern as Stories 6.3/6.6. Renumbered
     8.1-8.7 -> 8.2-8.8 in the same change (sprint-status.yaml and story files updated together). -->

### Story 8.1: Integration Spike — Cross-Surface Status Verification

As the SpecScribe maintainer,
I want a quick hands-on check that Epic 8's canonical status vocabulary, counts, and next-step commands can actually reach every live surface — HTML/web, the VS Code extension + webview, and the CLI console summary — before any of Epic 8's seven stories start,
So that a rework doesn't surface mid-epic the way Epic 6's webview/theming work would have without its own spikes (6.3, 6.6).

**Acceptance Criteria:**

1.
**Given** the current `StatusStyles`/`--status-*` token system, the shared view-model contract (Story 6.1), and the webview/SPA render adapters (Stories 6.4, 6.7)
**When** a status word, count, or badge is projected today
**Then** this spike confirms (by tracing actual code, not assumption) that all three live surfaces — `HtmlRenderAdapter`, `WebviewRenderAdapter`, and the CLI's `ConsoleUi` summary — read from the same single source, and names any surface that does not.

2.
**Given** Epic 8's planned additions (a status legend affordance, a single count source, paired progress/readiness, state-aware next-step commands, empty states, one primary view per dataset, recency signals)
**When** each is mapped against the three live surfaces
**Then** the spike records, per surface, whether the addition is expected to reach it automatically (because it rides the shared `HtmlRenderAdapter.RenderStoryBody`/view-model path), needs surface-specific work, or is HTML-only by design (and why)
**And** any surface gap found is fed into the owning story's Dev Notes before that story starts.

3.
**Given** the spike's findings
**When** it concludes
**Then** no production code changes land from this story — it is a tracing/verification pass, not a build — and its output is a short findings note appended to this story's Completion Notes (no new ADR required unless a surface gap forces an architectural choice).

### Story 8.2: Canonical Status Model with Portal-Wide Legend

As a maintainer scanning any page,
I want every status badge to use one canonical vocabulary per entity type,
So that I never have to mentally map between competing status words.

**Acceptance Criteria:**

1.
**Given** the projection model defines one canonical lifecycle per entity type (requirement, epic, story)
**When** any framework's artifacts are projected
**Then** the framework's native status vocabulary maps to the canonical lifecycle at the adapter layer, with the mapping documented
**And** no framework-specific status label is hard-coded in shared rendering (NFR8).

2.
**Given** any badge, chart segment, or legend renders a status
**When** the page is generated
**Then** the status routes through the `--status-*` token system so a given state always gets the same word and the same color everywhere
**And** a status-legend affordance reachable from any badge explains what each stage means.

3.
**Given** an adapter encounters a native status with no canonical mapping
**When** projection runs
**Then** the entity renders in a visible "unrecognized" state rather than being silently mislabeled
**And** generation completes with a non-fatal notice.

### Story 8.3: Single Source of Truth for Every Count

As a maintainer doing the daily pulse,
I want all summary counts derived from one generator-side source,
So that summary widgets and detail views can never disagree.

**Acceptance Criteria:**

1.
**Given** entity counts (stories, epics, deferred items, action items) appear on multiple surfaces
**When** the portal is generated
**Then** every widget consumes the same generator-side count source
**And** a dashboard total always equals the sum of its own breakdown segments.

2.
**Given** a dashboard card links to a detail page
**When** I follow the link
**Then** the count on the card matches what the detail page shows
**And** the historical 38-vs-39 story-count class of clash is structurally impossible.

### Story 8.4: Paired Progress and Readiness Semantics

As a maintainer,
I want task progress and workflow state always shown together,
So that "5/5 tasks done" while in review reads as one coherent fact, not a contradiction.

**Acceptance Criteria:**

1.
**Given** a story surface shows task completion and the story has a workflow state
**When** both are available
**Then** they render paired (for example "5/5 tasks · awaiting review") everywhere both appear
**And** epic dual-count badges restate as sentences (for example "6 of 7 done, 1 in review").

2.
**Given** the sprint board columns Backlog and Ready for dev
**When** I hover or focus a column header
**Then** a tooltip distinguishes them (for example "Ready = task plan exists and dependencies met")
**And** stories lacking task plans are visually separated from actionable ones.

### Story 8.5: State-Aware Next-Step Command Surface

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — carries VS Code recommendation R4.3: in the webview, pair the
     existing copy-command helper with "Open in Terminal" (createTerminal + sendText(command, execute:false) — the
     command is STAGED at a prompt and the user presses Enter). Preserves the AD-6/ADR 0003 read-only ruling
     (SpecScribe never executes; the explicit choice stays with the user) while feeling native. Worth an explicit
     AC here so the read-only ruling is recorded; the webview wiring rides Story 6.10's link seam. -->

As a maintainer selecting work,
I want the portal to recommend one primary command per lifecycle state plus applicable unhappy-path actions,
So that I copy the right command without hunting.

**Acceptance Criteria:**

1.
**Given** a story in any lifecycle state
**When** its next-step commands render
**Then** exactly one primary recommended command shows, plus applicable alternate/unhappy-path actions (for example correct-course mid-sprint, retro on done)
**And** commands inapplicable to the state never render — a done story no longer surfaces code-review as the next step.

2.
**Given** the command surface is adapter-supplied data (NFR8)
**When** a framework lacks a command workflow
**Then** the next-step section degrades to absent rather than showing wrong or empty commands
**And** each surfaced command carries a one-line caption explaining what it does.

3.
**Given** existing next-steps specs (spec-hide-code-review-button-ready-for-dev, spec-story-next-steps-review-command, spec-home-next-steps-label-and-code-review)
**When** this story is implemented
**Then** it audits and extends that shipped behavior rather than duplicating it.

### Story 8.6: Designed Empty States

As a stakeholder viewing a shared portal,
I want empty sections to read as intentional guidance,
So that zero-counts and repeated CLI hints do not read as errors or clutter.

**Acceptance Criteria:**

1.
**Given** multiple stories in an epic lack task plans
**When** the epics page renders
**Then** per-story CLI hints consolidate into one banner per epic with a single copy-able command affordance
**And** hint text is adapter-supplied, not hard-coded (NFR8).

2.
**Given** a sprint board column is empty
**When** the board renders
**Then** the column shows intentional guidance copy (for example "Nothing in progress — pick from Ready")
**And** empty states are visually styled as designed states, not bare zero-counts.

### Story 8.7: One Primary View per Dashboard Dataset

As a maintainer doing a 30-second scan,
I want each dataset shown one primary way with alternates demoted behind a toggle,
So that I never reconcile multiple renderings of the same data.

**Acceptance Criteria:**

1.
**Given** the home dashboard currently renders requirements three ways
**When** the page is generated
**Then** the coverage matrix is the single primary representation, with alternates demoted behind a toggle or links
**And** the sprint page's By Status / By Epic radio-toggle is the reused pattern.

2.
**Given** any chart with an accessibility text-twin table
**When** views are consolidated
**Then** the text-twin table is never removed (accessibility contract per Story 3.7)
**And** duplicated story-count displays across a page are consolidated to one.

### Story 8.8: Generation-Time Recency Signals

As a maintainer returning to the portal,
I want "last updated" markers on dashboard widgets and story cards,
So that I can spot recent movement without diffing pages.

**Acceptance Criteria:**

1.
**Given** git timestamps and artifact change logs are available at generation time
**When** the dashboard and story cards render
**Then** they carry "last updated" recency markers derived solely from that input data
**And** a from-scratch CI regeneration of the same inputs produces identical output (no per-visitor or cross-build state).

2.
**Given** a source lacks git data or change-log entries
**When** generation runs
**Then** the affected surface shows no recency marker rather than a wrong one
**And** generation remains non-fatal.

## Epic 9: Traceability and Review Follow-Through

Complete the requirement → epic → story chain so a Stakeholder can click from any requirement to its delivering stories, a Reviewer can judge a "done" claim in one glance, and follow-up items carry provenance and resolution paths — including visibility in the primary remaining-work geometry (sunburst) and coherent Driver/Stakeholder workflows for authoring and satisfaction status. Serves the daily Driver journeys (1–2), the review journey (3), the traceability differentiator (4), and debt follow-through (7) defined in docs/UserJourneys.md.

**FRs covered:** FR22, FR23, FR24, FR26, FR30 · **UX-DRs:** UX-DR26 · **NFRs:** NFR8

<!-- 2026-07-15 (epic-8 retrospective, correct-course): Stories 9.7–9.9 appended. 9.7 extends FR30 into the
     sunburst / remaining-work geometry (not absorbed into 9.6). 9.8–9.9 are journey-shaped holistic passes
     for authoring/delivery workflow and requirement-satisfaction status. epics.md + sprint-status.yaml
     updated together (Epic 6 process rule). -->
<!-- 2026-07-16 (correct-course, follow-up density + deep-link opportunity surfaced by 9.6/9.7): Stories 9.10–9.11
     appended. 9.10 makes the dense action-items / deferred-work LIST pages scannable; 9.11 adds a per-item DETAIL
     page (shared template, stable human-readable slug URLs) so 9.7's sunburst wedges + 9.10's list cards deep-link
     into a single item. Extends FR30; does not absorb 9.6 (provenance/resolution owner). epics.md + sprint-status.yaml
     updated together (Epic 6 process rule). -->

### Story 9.1: Requirement Pages Link to Their Covering Stories

As a stakeholder tracing a requirement,
I want FR/NFR detail pages to list the stories delivering them with current status,
So that I can go from a requirement ID to its stories without reading an epics document.

**Acceptance Criteria:**

1.
**Given** a requirement covered by one or more stories in the coverage map
**When** its detail page renders
**Then** the page lists each covering story with its canonical status, linked to the story page
**And** the listing is built from existing coverage-map data with no new authoring burden.

2.
**Given** a requirement with no covering stories
**When** its detail page renders
**Then** the page states that explicitly rather than omitting the section
**And** the statement distinguishes deferred from unmapped when Story 9.3's states are available.

### Story 9.2: NFR and UX-DR Coverage Maps

As a maintainer,
I want NFR and UX design requirements traced with the same rigor as FRs,
So that non-functional obligations are not second-class.

**Acceptance Criteria:**

1.
**Given** the epics page shows an FR coverage map
**When** the page renders
**Then** parallel (or combined) coverage maps exist for NFRs and UX-DRs
**And** they use the same canonical status vocabulary as Story 8.2.

2.
**Given** an NFR with no per-story implementation state
**When** its coverage renders
**Then** it shows a stated verification approach instead of an undifferentiated "Planned"
**And** per-item granularity replaces whole-section uniform status.

### Story 9.3: Deferred-on-Purpose vs Unmapped Coverage States

As a stakeholder reading coverage,
I want deliberate deferrals distinguished from unmapped gaps,
So that I do not misread intentional scope decisions as oversights.

**Acceptance Criteria:**

1.
**Given** a requirement without active coverage
**When** coverage reporting renders
**Then** "deferred on purpose" and "unmapped" render as distinct states with distinct visual treatment
**And** the distinction is never color-only.

2.
**Given** a deliberately deferred item
**When** its coverage state renders
**Then** it links to the deferral source (retro, change proposal, or deferred-work entry) when one exists
**And** the requirements-flow diagram and its accessibility text twin both carry the split.

### Story 9.4: Verification Evidence Strip on Story Pages

As a reviewer,
I want tasks, tests, and verification evidence surfaced near the status badge,
So that I can judge a "done" claim in one glance instead of excavating the dev record.

**Acceptance Criteria:**

1.
**Given** a story page whose dev record contains task completion, test counts, and verification dates
**When** the page renders
**Then** a compact evidence strip (for example "5/5 tasks · 586 tests green · verified 2026-07-09") appears near the status badge
**And** the strip links to the full dev-record section.

2.
**Given** a story with missing evidence
**When** the strip renders
**Then** missing evidence is visibly absent (for example "no test evidence recorded") rather than the strip being omitted
**And** the honest-absence signal uses the designed empty-state treatment.

### Story 9.5: Distinct Acceptance-Criteria Blocks and Collapsed Dev Notes

As a reviewer,
I want acceptance criteria visually distinct from surrounding prose and dev notes collapsed by default on long pages,
So that I can diff the contract against the claim quickly.

**Acceptance Criteria:**

1.
**Given** a story page with acceptance criteria
**When** the page renders
**Then** ACs render as bordered/tinted blocks using existing design tokens, clearly distinct from body prose
**And** the treatment audits and extends spec-ac-panel-and-story-card-polish rather than duplicating it.

2.
**Given** a long story page with dev-notes/dev-record sections
**When** the page renders
**Then** those sections collapse by default and expand on demand
**And** the "On this page" TOC invariant is preserved.

### Story 9.6: Follow-Up Item Provenance and Resolution Paths

As a maintainer at retro time,
I want every action item and deferred-work entry to show where it came from and what closes it,
So that promises visibly leave the list when resolved.

**Acceptance Criteria:**

1.
**Given** an action item or deferred-work entry
**When** it renders
**Then** it carries provenance (source retro or story) and resolution criteria
**And** it links to the resolving story or spec when one exists.

2.
**Given** multiple items referencing the same underlying obligation across retros
**When** the follow-ups page renders
**Then** they are merged or explicitly cross-linked
**And** items are ordered by age or blocking status rather than flattened by identical affordances.

3.
**Given** a framework without retro or deferred-work artifact types
**When** the portal generates
**Then** these surfaces degrade to absent rather than empty-but-present (NFR8).

### Story 9.7: Open Follow-Ups in the Remaining-Work Geometry

As a maintainer scanning what's left to work on,
I want open action items and retro commitments represented in the sunburst and related remaining-work surfaces,
So that process follow-through is visible in the same primary attention surface as stories and tasks — not only on the dedicated follow-ups pages.

**Acceptance Criteria:**

1.
**Given** open retrospective action items (and deferred-work entries when present) exist in the project
**When** the epic/project remaining-work geometry renders (sunburst and any sibling "what's left" summaries that feed the Driver's daily scan)
**Then** those open follow-ups appear as first-class remaining work — countable and navigable into their detail/provenance surfaces
**And** counts agree with the Story 8.3 `ProjectCounts` ledger (`OpenActionItems` and related) rather than a parallel recount.

2.
**Given** follow-up items are not stories or tasks
**When** the visualization is designed
**Then** they are not silently mislabeled as stories; the treatment is distinct (shape, label, or ring) and never color-only
**And** Story 9.6 remains the provenance/resolution owner on follow-up pages — this story does not absorb 9.6's card/grouping work.

3.
**Given** a project with zero open action items and no deferred-work surface
**When** generation runs
**Then** the sunburst/remaining-work geometry degrades cleanly (no empty fake wedges, no broken links) per NFR8.

### Story 9.8: Authoring and Delivery Workflow Coherence

As a maintainer using SpecScribe to drive work,
I want the portal's create-story, next-step, empty-state, and related Driver surfaces to form one coherent workflow from requirements gathering through story creation and development,
So that the tool actively guides daily journeys rather than only reflecting completed artifacts.

**Acceptance Criteria:**

1.
**Given** the existing next-step command surface (Story 8.5), designed empty states (Story 8.6), and undrafted/create-story affordances
**When** this story audits the Driver path (requirements → story creation → development → review)
**Then** gaps, dead ends, and contradictory guidance are identified and closed with concrete portal changes
**And** the work extends those shipped seams rather than duplicating a parallel command/empty-state system.

2.
**Given** a maintainer starting from Home or an epic with undrafted / ready / in-progress work
**When** they follow the portal's primary recommended path
**Then** each step's primary affordance matches the lifecycle state and leads to the next sensible unit of work
**And** framework-specific commands remain adapter-supplied (NFR8) with degrade-to-absent when a step is unsupported.

3.
**Given** visual or interaction changes this story introduces
**When** create-story / implementation proceeds
**Then** owner-selected silhouette directions are elicited up front (Epic 3/7/8 visual-intent practice) and not re-litigated at review.

### Story 9.9: Requirement Satisfaction Status at a Glance

As a stakeholder or reviewer,
I want a holistic reading of requirement satisfaction status across the portal,
So that I can judge whether requirements are satisfied without assembling the picture from disconnected pages.

**Acceptance Criteria:**

1.
**Given** FR/NFR/UX-DR coverage data and covering-story links (Stories 9.1–9.3)
**When** the portal presents satisfaction status (dashboard and/or requirements hub surfaces)
**Then** a maintainer can answer "what is satisfied, deferred on purpose, unmapped, or in flight?" in one coherent scan
**And** status vocabulary routes through Story 8.2's canonical `StatusStyles` / `--status-*` system — no parallel words or colors.

2.
**Given** a requirement with covering stories
**When** satisfaction status renders
**Then** it reflects delivering-story lifecycle honestly (including in-progress / review, not only done-vs-not)
**And** missing coverage uses Story 9.3's deferred-vs-unmapped distinction when that story has landed (coordinate; do not re-implement the tier).

3.
**Given** this is a holistic pass over surfaces that Stories 9.1–9.3 also touch
**When** scope is planned at create-story
**Then** it does not absorb 9.1–9.3's page-level deliverables; it composes and closes journey-level gaps those stories leave
**And** empty/absent framework coverage degrades per NFR8.

### Story 9.10: Scannable Follow-Up List Pages

As a maintainer scanning what's left,
I want the Action Items and Deferred Work list pages to read as a fast, uniform overview instead of a dense wall of detail,
So that I can see everything outstanding at a glance and drill into the one item I care about.

**Acceptance Criteria:**

1.
**Given** the Action Items and Deferred Work pages carry provenance, resolution links, cross-links, and (for action items) a Resolve-with-AI command per item today (Story 9.6)
**When** the list page renders
**Then** each entry is compressed to a scan-first row — a short title/summary, its status, its source (epic/retro or deferral source), and one primary link — with the heavy per-item detail moved off the index (to the Story 9.11 detail page, or behind a per-row disclosure when 9.11 has not landed)
**And** the two pages share one list grammar so they read as siblings.

2.
**Given** many items exist across several sources
**When** the page renders
**Then** items stay grouped and ordered as Story 9.6 established (by source retro / deferral source, age within) and the grouping is legible at a glance without expanding anything
**And** counts continue to agree with the Story 8.3 `ProjectCounts` ledger — no parallel recount.

3.
**Given** a framework with no retros or no deferred-work note
**When** the portal generates
**Then** the pages degrade to absent rather than empty-but-present (NFR8), exactly as today.

### Story 9.11: Follow-Up Detail Pages and Deep Links

As a maintainer following a link from the sunburst or a list row,
I want each action item and deferred-work item to have its own stable page,
So that I can deep-link to a single follow-up and read its full provenance and resolution context in one place.

**Acceptance Criteria:**

1.
**Given** an action item or a deferred-work item
**When** the portal generates
**Then** that item has its own detail page (or a stable per-item anchor) carrying its full provenance, resolution criteria, resolving-story/spec links, and cross-links — the detail that Story 9.10 moved off the list index
**And** action-item and deferred-item detail pages share one template, differing only in grouping / where-it-came-from framing.

2.
**Given** an item's detail page URL
**When** the same project regenerates (with the item unchanged)
**Then** the URL is a stable, human-readable slug derived from the item's existing text/source — not a positional index — so bookmarks and deep links survive reordering and regeneration
**And** no new authoring schema is introduced (slugs are derived by best-effort heuristic over text already authored, per the load-bearing Epic 9 principle).

3.
**Given** the Story 9.7 sunburst follow-up geometry and the Story 9.10 list rows
**When** an item is clicked
**Then** they link to that item's detail URL (completing 9.7's "navigable into their detail/provenance surfaces" AC), and the counts and set of items shown remain the Story 8.3 ledger's
**And** these surfaces degrade to absent when the underlying artifacts do not exist (NFR8).

### Story 9.12: Unplanned and One-Off Work in Geometry and Sprint

As a maintainer scanning remaining work,
I want quick-dev / one-shot specs and other unattributable one-offs to appear as first-class unplanned work — both on the project sunburst and on the sprint board —
So that parked direct work is visible beside the epic plan instead of vanishing into an opaque Follow-ups bucket or living only as a Home tile.

**Acceptance Criteria:**

1.
**Given** open quick-dev (`route: one-shot`) specs and/or deferred items whose provenance cannot resolve to an epic
**When** the project sunburst renders
**Then** those items appear under a dedicated synthetic root slice (e.g. Unplanned / Direct work), separate from epic-attributed stories and from retro action items that do have an epic
**And** when provenance or sprint timing can identify an epic, the item prefers that epic's story ring over the Unplanned root
**And** counts remain ledger-agreed (Story 8.3); NFR8 omits the Unplanned slice when empty.

2.
**Given** the same unplanned / one-off set
**When** the sprint board renders
**Then** those items also appear in an unplanned / one-off lane (or equivalent board grouping) so the sprint view and the sunburst describe the same residual work
**And** no new authoring schema is required — attribution derives from existing provenance, frontmatter, and sprint data.

### Story 9.13: Generated Filtered Follow-Up Group Pages and Sunburst Click Destinations

As a maintainer clicking a sunburst wedge,
I want every click to land on either that item's detail page or a generated list page that contains only the relevant group,
So that group wedges never dump me into the full deferred-work or action-items dump.

**Acceptance Criteria:**

1. **OWNER-LOCKED — generated filtered pages (not hash/query filters on the full list).**
**Given** a follow-up group that appears in the sunburst (an epic's attributed follow-ups, the Unplanned / Direct root, unattributed action items, etc.)
**When** the site generates
**Then** a dedicated filtered list page is written for that group (e.g. under `follow-ups/…`, sibling to Story 9.11 detail pages), using the shared Story 9.10 row grammar
**And** the page lists only that group's items; NFR8: no empty group pages.

2.
**Given** the project or epic sunburst
**When** a leaf wedge is clicked (story, action item, deferred item, quick-dev item)
**Then** it links to that item's detail page (Story 9.11 / story page / spec page)
**And** when a group wedge is clicked (epic follow-up aggregate, Unplanned root, Follow-ups slice), it links to that group's generated filtered list page — never the unfiltered whole-site deferred-work or action-items index.

## Epic 10: Portal Legibility for Every Audience

Make every surface navigable and correctly interpretable by first-time visitors, non-BMAD stakeholders, and tech leads: insight pages reachable from the nav, every chart self-explaining, vocabulary defined in place, and consistent dates, references, and TOC treatment. Serves the onboarding (5) and health-insight (6) journeys defined in docs/UserJourneys.md — the adoption deciders.

**FRs covered:** FR27, FR28, FR29 · **UX-DRs:** UX-DR25, UX-DR27, UX-DR28, UX-DR29, UX-DR30 · **NFRs:** NFR8

### Story 10.1: Insights Navigation and Structure Page Retirement

As a returning user on any interior page,
I want insight pages reachable from the top nav,
So that Git Insights, Deep Analytics, and follow-ups do not require a round-trip through Home.

**Acceptance Criteria:**

1.
**Given** the portal has git-insights and deep-analytics pages
**When** navigation renders
**Then** an "Insights" nav entry groups them
**And** Action Items and Deferred Work are reachable under Sprint or a "Follow-ups" entry.

2.
**Given** the Structure page's scope was retired (2026-07-08 correct-course)
**When** navigation renders
**Then** Structure no longer holds a top-nav slot (removed or redirected) until the Epic 7 treemap replaces it
**And** nav entries render only when the corresponding data exists, so shallow repos get no dead links (NFR8).

### Story 10.2: Chart Metadata Standard

As a tech lead reading insight charts,
I want every chart to carry a legend with real values, its time window, and one framing sentence,
So that charts deliver insight rather than trivia.

**Acceptance Criteria:**

1.
**Given** any chart in the portal
**When** it renders
**Then** it carries a legend with real value ranges (not only "Less … More"), the analysis time window as a number, and one sentence of why the metric matters
**And** ranked lists state their ranking metric (for example "top 50 of 781 by commit count").

2.
**Given** the standard is implemented
**When** a new chart is added
**Then** the metadata comes from a shared chart-frame by construction, not per-chart copy
**And** the work extends spec-commit-heatmap-contrast-and-day-drilldown rather than duplicating it.

### Story 10.3: Glossary and In-Place Vocabulary

As a first-time visitor,
I want unfamiliar terms defined in place and a suggested reading order,
So that I can orient without prior methodology knowledge.

**Acceptance Criteria:**

1.
**Given** a first visit to the portal
**When** I open Home
**Then** a linked "How to read this portal" page defines the portal vocabulary and suggests a reading order
**And** acronyms (FR/NFR, AC, ADR) expand on first use per page via abbr semantics.

2.
**Given** glossary terms and command captions are framework-specific
**When** the portal generates for any supported framework
**Then** that vocabulary is adapter-supplied, never hard-coded in shared rendering (NFR8)
**And** frameworks without equivalent concepts simply omit those glossary entries.

### Story 10.4: Consistent Dates and Event Sequencing

As a reader correlating events,
I want one date format everywhere with sequencing for same-day events,
So that recency and order are never ambiguous.

**Acceptance Criteria:**

1.
**Given** dates appear across pages (cards, heatmaps, change logs, ADRs)
**When** the portal generates
**Then** one date-format token is used portal-wide
**And** ADR listings gain dates and one-line summaries sourced from the ADR bodies.

2.
**Given** multiple change-log events share a date
**When** they render
**Then** sequence markers order them
**And** superseded/deprecated ADR states render distinctly from Accepted when they arrive.

### Story 10.5: Document Rendering Legibility

As a reader of long artifacts,
I want references, annotations, and navigation rendered as designed elements,
So that raw syntax and flat TOCs do not obstruct reading.

**Acceptance Criteria:**

1.
**Given** prose containing [[wiki-link]] names or file:line reference syntax
**When** the page renders
**Then** references render as styled chips or collect into a references appendix, never as raw syntax
**And** [ASSUMPTION: …] tags are styled via the Story 2.6 annotation treatment.

2.
**Given** a long artifact with many sections
**When** its "On this page" TOC renders
**Then** subsections group under collapsible parents
**And** the on-page-TOC invariant for long pages is preserved.

3.
**Given** retired or superseded work items (for example a retired story)
**When** their parent page renders
**Then** they render in a collapsed section that preserves history without cluttering active lists.

### Story 10.6: Insight-Chart Context Polish

As a tech lead interpreting analytics,
I want misleading chart contexts corrected,
So that I do not draw wrong conclusions from artifacts of the data.

**Acceptance Criteria:**

1.
**Given** change-coupling analysis includes generated or status files
**When** coupling views render
**Then** process-coupling is distinguished from code-coupling with an explanatory note
**And** the classification generalizes across repositories rather than naming SpecScribe-specific files (NFR8).

2.
**Given** an activity heatmap whose window predates the first commit
**When** it renders
**Then** the dead zone is annotated (for example "First commit Jul 4") or the window is trimmed
**And** single-contributor files suppress or reword multi-contributor phrasing (for example "People to talk to").

### Story 10.7: Sunburst Navigability at Project Scale

As a maintainer scanning remaining work on a large project,
I want the project and epic sunbursts to stay readable and drillable when dozens of stories and follow-ups share a ring,
So that wedge density never becomes a wall of unreadable slices and I can still reach the item I care about.

**Acceptance Criteria:**

1.
**Given** a project sunburst whose story/follow-up ring has enough peers that individual wedges become hard to hit or read
**When** it renders
**Then** the chart offers a clear navigability path — for example progressive drill-down (project → epic → story/follow-up), a companion scannable list, focus/hover emphasis that survives keyboard, or an alternate density mode — rather than relying on ever-tinier SVG wedges alone
**And** leaf and group click destinations remain the Story 9.13 contract (detail page vs generated filtered group page) — this story does not invent a parallel navigation scheme.

2.
**Given** an epic-scoped sunburst with a large attributed follow-up set
**When** a maintainer opens that epic
**Then** follow-ups remain attributable and reachable without collapsing into an opaque orange band
**And** the solution degrades cleanly when follow-ups are absent (NFR8) and does not invent a new authoring schema.

<!-- Stories 10.8–10.9 added 2026-07-19 (SCP 2026-07-19, correct-course): list-page polish. 10.8 generalizes Story
     9.10's follow-up row grammar into one shared list primitive across every index; 10.9 layers client-light
     sort/group/filter as a progressive enhancement reusing the Epic 20 interactivity budget (not a second JS stack).
     Route status through the canonical --status-* tokens (Story 8.2) and counts through Story 8.3's single source. -->

### Story 10.8: Unified List-Page Grammar Across Every Index

As a stakeholder scanning any index page,
I want every list page — requirements, stories, epics, follow-ups, code files, ADRs, commits — to share one scannable row grammar,
So that I learn the pattern once and read every list the same way.

**Acceptance Criteria:**

1.
**Given** the Story 9.10 follow-up row grammar as the seed
**When** it is generalized into a shared list primitive
**Then** each index renders through it with consistent row anatomy (primary label, status badge via the canonical `--status-*` tokens, key metadata, deep link) and a designed empty state (Story 8.6)
**And** it does not re-count items against the single-source counts (Story 8.3).

2.
**Given** HTML, webview, and SPA surfaces
**When** a list renders
**Then** all three stay coherent
**And** no index invents a one-off row layout outside the shared primitive.

### Story 10.9: Client-Light Sort, Group & Filter on List Pages

As a maintainer hunting one item in a long list,
I want to sort, group, and text-filter a list page in place,
So that a hundred-row index becomes reachable without scrolling the whole thing.

**Acceptance Criteria:**

1.
**Given** a list page with JavaScript available
**When** I sort (status / date / name), toggle grouping, or type into a filter
**Then** rows reorder or hide live client-side within the Epic 20 interactivity budget (not a second client stack)
**And** the controls are keyboard-operable with `aria` state.

2.
**Given** JavaScript off (NFR8)
**When** the page loads
**Then** it renders in a sensible server-defined default order with every row present
**And** the sort/group/filter controls are a progressive enhancement, never a gate on seeing the data.

<!-- Stories 10.10–10.11 added 2026-07-19 (SCP 2026-07-19, correct-course): contextual-wayfinding redesign, folded
     into Epic 10 per owner. The white bar itself becomes context-aware — same bar, page-type-specific contents —
     rather than gaining a separate sidebar rail. Owner intent: "the white bar used effectively throughout, with
     different context on each page or page type." Built on the Story 10.1 RenderNavMarkup seam (3-surface parity),
     the existing EntityPager prev/next, and Story 10.5's grouped TOC. -->

### Story 10.10: Context-Aware Navigation Bar

As a reader on any page,
I want the top navigation bar to carry navigation relevant to where I am,
So that the white bar earns its space on every page instead of only working on the home dashboard.

**Acceptance Criteria:**

1.
**Given** every page type (home, epic, story, requirement, code file, follow-up, ADR, commit, insight)
**When** the nav is defined
**Then** a page-type → nav-content mapping specifies what the bar surfaces on each — home keeps the global journey groups; an epic page surfaces its stories; a code page surfaces sibling files / sections; a requirement page surfaces its family; a follow-up page surfaces its group — all built from data already in the view models via the Story 10.1 `RenderNavMarkup` seam (no new authoring schema).

2.
**Given** that mapping
**When** an interior page renders
**Then** the bar shows its page-type-appropriate contents with the active item marked, a page with no meaningful local context (NFR8) falls back cleanly to the global nav rather than an empty bar
**And** HTML, webview, and SPA stay coherent through the shared seam.

### Story 10.11: Sticky Section Nav & Breadcrumb Coherence

As a reader on a long interior page,
I want sticky in-page section navigation plus consistent breadcrumb and prev/next controls,
So that orientation and traversal feel the same everywhere instead of improvised per page.

**Acceptance Criteria:**

1.
**Given** a long page (extending Story 10.5's grouped TOC)
**When** it renders
**Then** a sticky section nav tracks the current section, and breadcrumb plus the existing `EntityPager` prev/next are unified into one coherent wayfinding treatment across page types.

2.
**Given** keyboard and reduced-motion users
**When** they use section or breadcrumb navigation
**Then** focus and scroll behavior honor the existing a11y and reduced-motion conventions
**And** there is no per-visitor state (FR31 determinism).

<!-- Epics 11–15 added 2026-07-10: per-framework coverage extracted from Epic 4 (Stories 4.3–4.7) into their own
     spike-led epics (append-only, no renumber). Each epic's Story X.1 is a Framework Integration Spike that scopes
     the mapping to Epic 4's shared adapter contract — classifying artifacts as mappable/partial/unsupported and
     recording framework-extra data and deliberately-unsupported conventions — before the migrated baseline
     coverage story (X.2) runs. FRs: 11 → FR3, 12 → FR4, 13–15 → FR17. -->

## Epic 11: Spec Kit Coverage

Interpret core Spec Kit artifacts in the portal through Epic 4's shared framework adapter contract, so Spec Kit teams can track planning progress without switching tools. Led by an integration spike that scopes the mapping and its boundaries before baseline coverage.

**FRs covered:** FR3

### Story 11.1: Spec Kit Integration Spike

As a maintainer preparing to support Spec Kit,
I want the Spec Kit artifact set mapped against the shared adapter contract before coverage work begins,
So that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** representative current-version Spec Kit repositories
**When** the Spec Kit artifact set is surveyed against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each Spec Kit artifact type as mappable, partially-mappable, or unsupported
**And** the target shared-model projection is named for each mappable type.

2.
**Given** Spec Kit conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework-extra data is recorded as candidate projection extensions or explicit non-goals
**And** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

### Story 11.2: Spec Kit Baseline Adapter Coverage

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

## Epic 12: GSD and GSD-Pi Coverage

Render key GSD and GSD-Pi planning and tracking artifacts coherently through Epic 4's shared adapter contract, so GSD teams keep progress and scope understandable in one portal. Led by an integration spike that scopes the GSD family's mapping and coverage tiers before baseline coverage.

**FRs covered:** FR4

### Story 12.1: GSD and GSD-Pi Integration Spike

As a maintainer preparing to support GSD and GSD-Pi,
I want the GSD family's artifact set mapped against the shared adapter contract before coverage work begins,
So that baseline coverage starts with a defined scope, declared coverage tiers, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** representative GSD and GSD-Pi repositories
**When** the GSD family's artifact set is surveyed against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each GSD/GSD-Pi artifact type as mappable, partially-mappable, or unsupported
**And** the target shared-model projection and declared coverage tier are named for each mappable type.

2.
**Given** GSD/GSD-Pi conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework-extra data is recorded as candidate projection extensions or explicit non-goals
**And** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

### Story 12.2: GSD and GSD-Pi Baseline Adapter Coverage

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

## Epic 13: SpecFlow Coverage

Interpret core SpecFlow specification and planning artifacts through Epic 4's shared adapter contract, so SpecFlow teams can track progress without switching tools. Led by an integration spike that scopes the mapping and its boundaries before baseline coverage.

**FRs covered:** FR17

### Story 13.1: SpecFlow Integration Spike

As a maintainer preparing to support SpecFlow,
I want the SpecFlow artifact set mapped against the shared adapter contract before coverage work begins,
So that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** representative SpecFlow repositories
**When** the SpecFlow artifact set is surveyed against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each SpecFlow artifact type as mappable, partially-mappable, or unsupported
**And** the target shared-model projection is named for each mappable type.

2.
**Given** SpecFlow conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework-extra data is recorded as candidate projection extensions or explicit non-goals
**And** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

### Story 13.2: SpecFlow Baseline Adapter Coverage

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

## Epic 14: Squad Coverage

Interpret core Squad artifacts through Epic 4's shared adapter contract, so Squad teams can track planning progress without switching tools. Led by an integration spike that scopes the mapping and its boundaries before baseline coverage.

**FRs covered:** FR17

### Story 14.1: Squad Integration Spike

As a maintainer preparing to support Squad,
I want the Squad artifact set mapped against the shared adapter contract before coverage work begins,
So that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** representative Squad repositories
**When** the Squad artifact set is surveyed against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each Squad artifact type as mappable, partially-mappable, or unsupported
**And** the target shared-model projection is named for each mappable type.

2.
**Given** Squad conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework-extra data is recorded as candidate projection extensions or explicit non-goals
**And** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

### Story 14.2: Squad Baseline Adapter Coverage

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

## Epic 15: Superpowers Coverage

Interpret core Superpowers artifacts through Epic 4's shared adapter contract, so Superpowers teams can track planning progress without switching tools. Led by an integration spike that scopes the mapping and its boundaries before baseline coverage.

**FRs covered:** FR17

### Story 15.1: Superpowers Integration Spike

As a maintainer preparing to support Superpowers,
I want the Superpowers artifact set mapped against the shared adapter contract before coverage work begins,
So that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** representative Superpowers repositories
**When** the Superpowers artifact set is surveyed against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each Superpowers artifact type as mappable, partially-mappable, or unsupported
**And** the target shared-model projection is named for each mappable type.

2.
**Given** Superpowers conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework-extra data is recorded as candidate projection extensions or explicit non-goals
**And** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

### Story 15.2: Superpowers Baseline Adapter Coverage

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

<!-- Epic 16 added 2026-07-10 (SCP 2026-07-10, correct-course): release engineering for the community
     preview. New, additive scope — no existing epic changed. Spike-led first story (16.1) per the Epics
     11–15 pattern. Story 16.5 (Marketplace publish) depends on Epic 6's extension existing. FRs: FR32–FR34;
     NFR9. Run create-story per story when scheduled (16.1 first). -->

## Epic 16: Release Engineering & Community Preview Launch

Everything needed to put a preview build of SpecScribe in the community's hands and keep shipping updates reliably: a reproducible build/test gate, packaged and published CLI distribution, a tag-triggered release pipeline, VS Code Marketplace publication of the read-only extension, release-facing documentation with a changelog and versioning policy, and a preview-launch readiness cut.

**FRs covered:** FR32, FR33, FR34 · **NFRs:** NFR9
**Depends on:** Epic 6 (for Story 16.5 — the extension must exist to be published).

### Story 16.1: Release & Distribution Packaging Spike

As a maintainer preparing a community preview,
I want the distribution channels, versioning policy, and publishing prerequisites decided and written down before release stories begin,
So that packaging work starts with an agreed scope and no surprise blockers.

**Acceptance Criteria:**

1.
**Given** the CLI can ship via multiple channels
**When** the spike evaluates them
**Then** a written decision records the chosen CLI channel(s) — NuGet `dotnet` global tool (already wired in SpecScribe.csproj) and/or self-contained per-OS binaries — with rationale and explicit non-goals.

2.
**Given** publishing requires accounts and secrets
**When** the spike documents prerequisites
**Then** it inventories every required secret/credential (NuGet API key, VS Marketplace publisher + PAT), where each is stored as a repository/environment secret, and any code-signing decision
**And** no secret value is committed to the repository.

3.
**Given** a preview release differs from a stable one
**When** the spike defines policy
**Then** it records the versioning + pre-release scheme (for example `0.x` / `-preview` tags), the changelog format, and what "preview" promises and does not promise to consumers.

### Story 16.2: Continuous Integration Build & Test Gate

As a maintainer,
I want every pull request and push to build and run the test suite in CI,
So that release builds start from a known-green baseline and regressions are caught before merge.

**Acceptance Criteria:**

1.
**Given** a pull request or push to a release-relevant branch
**When** CI runs
**Then** it restores, builds, and executes the `tests/SpecScribe.Tests` suite on a clean checkout, and the job fails on any build or test failure.

2.
**Given** the gate is green
**When** a maintainer reviews the pull request
**Then** the build/test status is visible as a required signal
**And** the workflow is independent of, and does not disturb, the existing GitHub Pages publish workflow.

### Story 16.3: CLI Packaging and Publication

As a prospective user,
I want SpecScribe published to its chosen distribution channel,
So that I can install and run it with a documented one-line command.

**Acceptance Criteria:**

1.
**Given** Story 16.1's channel decision
**When** packaging runs
**Then** the CLI is produced as the chosen artifact(s) — a NuGet global-tool package and/or self-contained per-OS executables — reproducibly from the repository, with the version derived from the release tag rather than a hard-coded csproj value.

2.
**Given** a produced package
**When** a user follows the documented install path (for example `dotnet tool install -g SpecScribe`)
**Then** the `specscribe` command runs and `--version`/`--help` report correctly
**And** the packaged README/license render on the package listing.

### Story 16.4: Tag-Triggered Release Pipeline

As a maintainer cutting a release,
I want pushing a release tag to build, verify, package, and publish automatically,
So that releases are one action and never depend on a local machine's state.

**Acceptance Criteria:**

1.
**Given** a release or pre-release tag is pushed
**When** the release pipeline runs
**Then** it builds and tests on a clean checkout, packages per Story 16.3, publishes to the chosen channel(s), and attaches the release artifacts to the corresponding GitHub Release
**And** publishing is gated on the build+test step passing (NFR9).

2.
**Given** a `-preview` / pre-release tag
**When** the pipeline publishes
**Then** the release is marked as a pre-release / preview channel per Story 16.1's policy
**And** a failed publish leaves no partially-released state (the pipeline is safe to re-run).

### Story 16.5: VS Code Extension Packaging and Marketplace Publication

<!-- Depends on Epic 6: the extension surface (esp. Story 6.4 runtime) must exist before it can be
     packaged/published. Keep blocked/backlog until Epic 6 delivers the extension. -->

<!-- 2026-07-11 (SCP 2026-07-11, correct-course) — owns VS Code recommendations R1.4 (contributes.walkthroughs
     first-run onboarding — the single best Marketplace-launch onboarding lever), R1.6 (Marketplace metadata
     polish: real categories, keywords, icon, repository, README with screenshots — already implied by AC #1),
     and R8.2 (platform-specific VSIX targets: `vsce package --target win32-x64` etc. so the Marketplace serves
     each user only their platform's build, turning ADR 0005's ~73 MB-per-RID from a multiplied download into a
     single-RID one). PREREQUISITE: the Workspace-Trust posture (R5.4) in Story 6.8 must be in place before this
     publish — it is a Marketplace review-bar item. -->

As a VS Code user,
I want the read-only SpecScribe extension available from the Marketplace,
So that I can install it without building from source.

**Acceptance Criteria:**

1.
**Given** the Epic 6 extension exists
**When** the extension is packaged
**Then** a valid VSIX is produced reproducibly with a Marketplace-ready manifest (publisher, display name, description, icon, categories, repository link) and versioning aligned to Story 16.1's policy.

2.
**Given** the VSIX and a configured publisher
**When** a release publishes the extension
**Then** it appears on the VS Code Marketplace as a read-only preview and installs cleanly
**And** publication is automatable (extends the Story 16.4 pipeline or a parallel job) rather than a manual one-off.

3.
**Given** Epic 6 is not yet complete
**When** this story is scheduled
**Then** it remains blocked/backlog and is not started until the extension surface exists.

### Story 16.6: OSS Onboarding, Release-Facing Documentation, Changelog, and Versioning Policy

<!-- 2026-07-11 (SCP 2026-07-11, correct-course): absorbed the removed Story 5.4 (OSS onboarding/reference
     docs). This story now OWNS both onboarding/reference content (FR18) and release-facing docs (FR34);
     AC #2/#3 below carry the former 5.4 ACs. -->

As a community adopter,
I want getting-started and configuration/CLI reference documentation alongside install/upgrade instructions, a changelog, and a stated versioning policy,
So that I can install, run, configure, and contribute to SpecScribe without insider knowledge, and adopt the preview confidently while tracking what changes between releases.

**Acceptance Criteria:**

1.
**Given** the chosen distribution channels
**When** the release docs are produced
**Then** the README (and Marketplace listing, if applicable) carry accurate install, upgrade, and quick-start instructions using real commands
**And** a `CHANGELOG.md` following the Story 16.1 format exists and is updated per release
**And** `--help`/`--version` output is audited to match the docs.

2.
**Given** a new user or contributor arrives at the repository (former Story 5.4 scope, FR18)
**When** they follow the documentation
**Then** getting-started steps, a configuration/CLI reference, and contribution guidance are complete, accurate, and current
**And** examples reflect real, working commands.

3.
**Given** the documentation coexists with the tool and generated portal (former Story 5.4 scope, FR18)
**When** it is produced
**Then** docs stay consistent with actual behavior (options, defaults, commands) and are easy to keep in sync, with distribution-facing concerns (install/upgrade, changelog, versioning/pre-release policy, Marketplace listing copy) integrated rather than duplicated
**And** missing or partial docs are surfaced rather than silently absent.

### Story 16.7: Preview Launch Readiness and Cut

As a maintainer,
I want a final readiness pass before announcing the preview,
So that the first public impression is a working install, not a broken link.

**Acceptance Criteria:**

1.
**Given** the pipeline and docs are in place
**When** the readiness checklist runs
**Then** the CLI install path is verified end-to-end from the published artifact on a clean environment (and the extension install if Epic 6 shipped), the LICENSE and contribution/onboarding links resolve, and the preview version/tag is set per Story 16.1's policy.

2.
**Given** readiness passes
**When** the preview is cut
**Then** release notes are published for the tag and the announcement points at working install instructions
**And** any items intentionally excluded from the preview are recorded as known limitations rather than silent gaps.

### Story 16.8: npx Distribution via npm-Wrapped Native Binary

<!-- 2026-07-10: Seated by ADR 0006 (Accepted) as an ADDITIVE distribution channel — see docs/adrs/0006. The 6.6
     spike PROVED this end-to-end: a ~1.5 KB npm wrapper (esbuild/Biome pattern, via optionalDependencies) resolves
     and spawns the self-contained native binary, so `npx specscribe` generated all 196 files with NO .NET SDK
     present. Promotes that proven wrapper into a real channel. Aligns with / feeds Story 16.3 (CLI packaging) — the
     native binary it wraps is the same self-contained publish 16.3 produces. Full ACs via create-story when scheduled. -->

As a prospective user in the JS/spec-driven-dev ecosystem,
I want to run SpecScribe via `npx` with no .NET SDK installed,
So that trying and using the tool (locally or in CI) is as low-friction as any Node CLI.

**Acceptance Criteria:**

1.
**Given** the self-contained native binary produced by Story 16.3
**When** the npm-wrapper package is published
**Then** `npx <package>` resolves and runs the correct per-OS binary (via `optionalDependencies`/platform packages) and generates the site with no .NET SDK or runtime installed.

2.
**Given** npx is an additive channel
**When** it ships
**Then** the `dotnet tool` channel remains available for .NET users, versioning stays aligned with Story 16.1's policy, and the wrapper's per-RID binary matrix is documented (size/latency trade-offs per ADR 0006).

<!-- Epic 17 added 2026-07-11 (SCP 2026-07-11, correct-course): pre-publication hardening. Runs after feature
     completion (Epics 1–15, 18) and Epic 5, and BEFORE Epic 16's publish/cut stories — its sign-off (Story 17.4)
     gates the community preview. NFR10. Append-only, no renumber. Run create-story per story when scheduled. -->

## Epic 17: Code Hardening & Release-Readiness Review

A dedicated pre-publication pass to get SpecScribe ready to work reliably and safely with both public and private codebases: remediate structural weaknesses, inconsistencies, and inefficiencies accumulated across the feature epics; close security and privacy gaps; and burn down or explicitly accept the deferred-work and retro-action backlog — ending in a release-readiness sign-off that gates Epic 16's publication and cut. This epic reviews and remediates existing code; it does not add product features.

**NFRs covered:** NFR10 (also touches NFR1 performance, NFR4 extensibility).
**Sequencing:** after Epics 1–15 and 18 (features) and Epic 5 (CLI); before Epic 16 Stories 16.3+ (publish) and 16.7 (cut).

### Story 17.1: Structural and Consistency Remediation Sweep

As the SpecScribe maintainer preparing for public release,
I want a deliberate sweep for structural weaknesses, inconsistencies, and duplication across the C# core, the extension shim, and the stylesheet,
So that the codebase is coherent and maintainable before outside contributors and users depend on it.

**Acceptance Criteria:**

1.
**Given** the code accumulated across the feature epics
**When** the structural review runs
**Then** it identifies and remediates structural weaknesses and inconsistencies — duplicated single-source-of-truth violations (for example the twin sunburst legend tuples, the divergent `scroll-margin-top` clearance values, the icon key/label dual-representation), dead or unreachable code, and naming/token drift — with each fix pinned by a test or an explicit rationale for deferral
**And** the golden byte-parity gate and full test suite stay green (remediation must not change rendered output unless a change is intentional and re-baselined).

2.
**Given** items already recorded in `deferred-work.md` as maintainability/consistency debt
**When** this sweep triages them
**Then** each is either fixed here or carried forward with a recorded decision, and no fix silently regresses another surface
**And** the review covers the extension TypeScript shim and the CSS, not only the C# core.

### Story 17.2: Security and Privacy Hardening for Public and Private Repos

As the SpecScribe maintainer,
I want the tool audited and hardened so it is safe to run on both public and private codebases,
So that neither a hostile public repo nor a sensitive private one can produce an unsafe or leaky result.

**Acceptance Criteria:**

1.
**Given** SpecScribe renders untrusted repository content into HTML and a VS Code webview
**When** the security review runs
**Then** output-injection surfaces are closed — HTML-escaping is complete and consistent (for example the unescaped detail-page `<h1>` titles, `StatusStyles.Badge`'s un-escaped `cssClass`, and the `RequirementLinkifier` attribute-injection exposure recorded in deferred-work), the webview CSP/nonce posture is verified, and the untrusted-workspace / `toolPath` tool-resolution attack surface is closed (Story 6.8's Workspace-Trust posture is present and effective)
**And** each closed hole is pinned by a regression test.

2.
**Given** SpecScribe may run on a private codebase
**When** the privacy review runs
**Then** generated output is confirmed to leak no secrets or unintended private content beyond what the source artifacts already expose, no personal-structure assumptions remain that would misrender or drop a differently-organized repo (Epic 4 de-personalization verified end to end), and third-party dependencies (C# and the extension's npm tree) are audited for known vulnerabilities
**And** local-first / no-remote-telemetry operation (NFR3) is re-confirmed for every code path added since it was last verified.

### Story 17.3: Performance and Efficiency Pass

As a user running SpecScribe on a real, sometimes-large repository,
I want the known performance and efficiency debts addressed before release,
So that generation and the live webview stay responsive at realistic scale.

**Acceptance Criteria:**

1.
**Given** the performance debts recorded across the feature epics
**When** the efficiency pass runs
**Then** the highest-impact items are addressed or explicitly accepted with rationale — the webview's full-site re-render per change (ADR 0005 §3 scoped re-render / warm-renderer follow-up), unbounded git-log/heatmap payloads on mature repos, redundant per-fragment renderer-swap scans, and missing recursion-depth guards on the tree/treemap renderers
**And** baseline generation performance (NFR1) is measured before and after, with deep analytics still separated from baseline runs.

2.
**Given** changes intended purely to improve efficiency
**When** they land
**Then** rendered output stays byte-identical (or intentional changes are re-baselined) and the test suite stays green
**And** any item left unaddressed is recorded as an accepted known limitation rather than dropped silently.

### Story 17.4: Deferred-Work Burndown and Release-Readiness Sign-off

As the SpecScribe maintainer,
I want every open deferred-work item and retrospective action triaged to a decision, and a release-readiness sign-off produced,
So that the community preview ships from a known, deliberate state rather than an unreviewed backlog.

**Acceptance Criteria:**

1.
**Given** the `deferred-work.md` backlog and the open `sprint-status.yaml` retrospective action items
**When** the burndown runs
**Then** each open item is resolved, scheduled into a specific story, or explicitly accepted as a documented known limitation — with none left in an ambiguous open state
**And** items resolved by Stories 17.1–17.3 are closed in the same pass (per the Epic 3 retro rule: close items when the fix ships).

2.
**Given** the hardening work of this epic is complete
**When** the sign-off is produced
**Then** a release-readiness record states that structural, security/privacy, and performance reviews passed (or lists accepted limitations), and that the tool is cleared to run against public and private codebases
**And** this sign-off is the gate Epic 16's publish/cut stories (16.3+, 16.7) depend on.

<!-- 2026-07-18 (owner-directed, append-only): Story 17.5 seated — investigate oversized source files
     (notably specscribe.css) and propose a split/modularization path before more feature CSS accumulates. -->

### Story 17.5: Large-File Investigation (CSS and Kindred Hotspots)

As the SpecScribe maintainer preparing the codebase for outside contributors,
I want a deliberate investigation of oversized source files — especially `src/SpecScribe/assets/specscribe.css` and any C#/TS peers that repeatedly absorb every feature change —
So that we have a concrete, sequenced plan to split or modularize them before release hardening locks the shape in.

**Acceptance Criteria:**

1.
**Given** the current `specscribe.css` (and a shortlist of other large/hotspot files identified by size + change frequency)
**When** the investigation runs
**Then** it records measured size (lines / bytes), ownership hotspots (which features keep appending), coupling risks (regen/golden impact, webview theming bridge), and 2–3 viable modularization options (e.g. layer split by domain: base tokens / chrome / charts / code-pages / insights) with trade-offs
**And** it does **not** perform a big-bang rewrite in this story — findings + a recommended sequence are the deliverable (implementation may land here only for a thin, reversible first slice if the recommendation is unambiguous and tests stay green).

2.
**Given** Stories 17.1 (structural sweep) and 17.3 (performance) may overlap
**When** this investigation concludes
**Then** its recommendations are fed into 17.1/17.3 Dev Notes (or scheduled follow-on tasks) so the hardening epic does not rediscover the same debt
**And** any accepted "leave as-is for preview" decision is explicit with rationale (not silent).

<!-- Epic 18 added 2026-07-11 (SCP 2026-07-11, correct-course): BMad-native module/expansion exploration
     (FR36), distinct from the third-party-framework Epics 11–15. Spike-led (18.1) per the Epics 11–15 pattern.
     Exploratory — sequences alongside 11–15, not on the release-blocking path. Run create-story when scheduled. -->

## Epic 18: BMad Module & Expansion Coverage Exploration

Extend first-class BMad support beyond the BMM core (already supported) to BMad's own module and expansion ecosystem — for example BMad Builder, Creative Intelligence, and game-dev / GDS-style expansions — so BMad users working in non-BMM modules see their planning and tracking artifacts represented in the portal. Delivered through Epic 4's shared adapter contract and led by a landscape-and-coverage spike, mirroring the spike-led Epics 11–15. Distinct from those third-party-framework epics: this is the BMad-native module surface. Exploratory, not release-blocking.

**FRs covered:** FR36

### Story 18.1: BMad Module Landscape and Coverage Spike

As a maintainer preparing to support BMad modules beyond BMM,
I want the BMad module/expansion ecosystem inventoried and each module's distinctive artifacts mapped against the shared adapter contract before any coverage work begins,
So that baseline coverage starts with a defined scope, a prioritized target module, and no surprise conventions.

**Acceptance Criteria:**

1.
**Given** BMad's module and expansion ecosystem beyond the BMM core (for example BMad Builder, Creative Intelligence, and game-dev / GDS-style expansions)
**When** the spike inventories it and surveys each module's artifact set against the shared adapter contract's ArtifactBundle and projection model
**Then** a written coverage map classifies each module's distinctive artifact types as mappable, partially-mappable, or unsupported (noting which are already covered by the existing BMM parsing), names the target shared-model projection for each mappable type, and recommends a priority module (or modules) to cover first
**And** the survey distinguishes BMad-native modules from the third-party frameworks already scoped by Epics 11–15.

2.
**Given** module conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework/module-extra data is recorded as candidate projection extensions or explicit non-goals, and deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit
**And** the current BMM-specific next-step-command mapping is assessed for generalization to other modules (per the "strongly GDS-oriented … requires generalization" note in Additional Requirements), giving the coverage story an agreed scope boundary.

### Story 18.2: Priority BMad Module Baseline Coverage

As a team using a BMad module beyond BMM,
I want my module's core planning and tracking artifacts interpreted in the portal,
So that I can track progress without switching tools or losing module-specific work.

**Acceptance Criteria:**

1.
**Given** the priority module(s) chosen by Story 18.1's coverage map
**When** generation runs against a representative repository for that module
**Then** the module's core planning and tracking artifacts render without fatal failures via the shared adapter contract, each discovered artifact labeled rendered, summarized, or unsupported
**And** output stays coherent alongside the existing BMM and framework surfaces, with BMM support fully intact.

2.
**Given** module-specific artifacts the projection does not model
**When** they are discovered
**Then** they surface as explicit non-fatal notices (coverage-tier labeling where partial) and never block full-site generation
**And** any module-specific next-step-command vocabulary flows through the adapter contract rather than being hard-coded (NFR8).

<!-- Stories 18.3–18.4 added 2026-07-19: BMad-authoring-tool integrations explored in chat (bmad-index-docs,
     bmad-forge-idea). 18.3 spike-led per the Epics 11–15/18.1 pattern. 18.4 depends on 18.3's pinned contract
     for its blurb-metadata half but stands alone for the Ideas list surface. Run create-story when scheduled. -->

### Story 18.3: BMad Index-Docs Contract Spike

As a maintainer wanting per-doc descriptions in the portal,
I want bmad-index-docs' generated index.md format inventoried and pinned as a parseable contract,
So that SpecScribe can consume it as a blurb/metadata source for doc pages without depending on an unstable prose format.

**Acceptance Criteria:**

1.
**Given** bmad-index-docs' current output across representative repos
**When** the spike inventories the index.md entry format (line shape, path resolution, description length/style, edge cases like missing docs or nested folders)
**Then** a written contract documents the exact entry grammar SpecScribe should parse, flags any repo-to-repo inconsistencies found, and recommends whether to parse it as-is or request a stricter emission mode from bmad-index-docs.

2.
**Given** the pinned contract
**When** the spike identifies the seam
**Then** it recommends which SpecScribe surface(s) should carry the parsed blurb metadata (doc nav/TOC entries and/or a docs landing page) and the fallback behavior when index.md is absent, stale, or references a moved/deleted file
**And** the follow-on implementation story has an agreed scope boundary.

### Story 18.4: Forged Ideas List Page

As a team using bmad-forge-idea to pressure-test ideas before they become product briefs,
I want forged idea artifacts (hardened or killed) rendered as a list page in the portal,
So that idea-stage lineage and rationale are visible alongside requirements/epics rather than lost in standalone files.

**Acceptance Criteria:**

1.
**Given** bmad-forge-idea's output artifacts (or a defined contract for identifying them) in a repository
**When** generation runs
**Then** a new Ideas list page renders each discovered idea with its title, verdict (hardened/killed/in-progress), and a link through to the persona-objections/rationale content, using the existing ListRow primitive per Story 10.8's list-page grammar.

2.
**Given** an idea that later produced a product brief, PRD, or epic
**When** the list page renders
**Then** it links forward to that downstream artifact where discoverable, so the idea's fate is traceable without manual cross-referencing.

3.
**Given** no forge-idea artifacts exist in a repository
**When** generation runs
**Then** the Ideas page/nav entry is omitted entirely rather than showing an empty page, matching existing optional-surface conventions elsewhere in the portal.

<!-- Epic 19 added 2026-07-17: directed work graph across epics/stories/quick-dev/deferred/reviews/code.
     Spike-led. Exploratory — not release-blocking. Run create-story when scheduled. -->

## Epic 19: Directed Work Graph — Traceability Across Artifacts

Make the directed relationships among epics, stories, quick-dev / one-shot work, deferred-work items, retrospectives and code-review provenance, and source code navigable as a first-class graph — so a Driver or Reviewer can see and query "what stemmed from what," detect cycles or ambiguous reverse-links, and explore beyond breadcrumbs and per-page reverse panels.

**FRs covered:** FR37 (sync into PRD when convenient) · **NFRs:** NFR8 · **Depends on:** Epic 9 (follow-up provenance), Epic 7 (code citations) as data sources — does not block either.

### Story 19.1: Work-Graph Model and Coverage Spike

As a maintainer who traces debt across reviews and stories,
I want the portal's entity types and directed edges inventoried and scoped before any visualization ships,
So that the graph has a defined node/edge vocabulary, cycle semantics, and non-goals rather than an ad-hoc diagram.

**Acceptance Criteria:**

1.
**Given** existing provenance seams (deferred `source_spec` / Deferred-from headings, action-item `epic:`, quick-dev epic attribution, story↔requirement links, code citations)
**When** the spike inventories them
**Then** a written coverage map lists node types (at least: epic, story, quick-dev, deferred item, action item, retro, code file) and directed edge kinds (stemmed-from, resolves, covers, cites, raised-in), marks each as already derivable vs requiring new heuristics, and names cycles/ambiguous reverse-links as first-class queries
**And** deliberately out-of-scope edges (e.g. inventing story parents for retro actions) are listed with rationale.

2.
**Given** the spike's recommended first surface
**When** the spike documents findings
**Then** it proposes one primary visualization + query path for Story 19.2 (e.g. epic-scoped subgraph, cycle finder, or "path from deferred → epic") with success criteria and NFR8 absence rules when a project has no follow-up/code graph
**And** no new authoring schema is required for the MVP path.

### Story 19.2: Directed Graph Visualization and Path Query

As a Driver scanning remaining work,
I want a portal surface that draws the directed work graph for a chosen scope and answers simple path/cycle queries,
So that circular-looking reverse links and multi-hop provenance become inspectable instead of inferred from breadcrumbs.

**Acceptance Criteria:**

1.
**Given** a project with attributed deferred/quick-dev/story/epic links (per Story 19.1's mappable edges)
**When** the graph surface renders for a chosen scope (at least epic-scoped)
**Then** nodes and directed edges are navigable to existing detail pages, and a cycle or multi-hop path query surfaces ambiguous or circular provenance when present
**And** zero-graph projects omit the surface cleanly (NFR8).

2.
**Given** the same underlying ledger counts and provenance parsers as Epic 9
**When** the graph builds
**Then** it does not invent a second authoring schema or re-count open items against ProjectCounts
**And** HTML/SPA parity holds for the new page(s).

<!-- Epic 20 added 2026-07-19 (SCP 2026-07-19, correct-course): interactive project explorer — the owner's drill-in
     zoomable sunburst + related-work side pane. SpecScribe's first rich client-interactive surface; a progressive
     enhancement over the static Story 10.7 sunburst (which stays the no-JS baseline and is in active dev). Consumes
     Epic 19's work-graph edges + the existing Charts.Sunburst/FollowUpGeometry weights. Spike-led. FR38. -->

## Epic 20: Interactive Project Explorer — Drill-In Sunburst with Related-Work Pane

Turn the static remaining-work sunburst into a fluid, explorable map of the whole project: click a wedge to zoom into it in place, reveal its nested children, and breadcrumb back out — paired with a live side pane that shows the work-graph nodes related to whatever is selected. SpecScribe's first rich client-interactive surface; it degrades cleanly to the static Story 10.7 sunburst and Story 9.13 linked pages when JavaScript is unavailable (NFR8).

**FRs covered:** FR38 (sync into PRD when convenient) · **NFRs:** NFR8 · **Depends on:** Epic 19 (work-graph edges) as its relationship source, the existing `Charts.Sunburst`/`EpicSunburst` + `FollowUpGeometry` weights as its hierarchy source, and Story 6.7 (SPA adapter) as prior art for a JS delivery surface. Story 10.7 is the static baseline this enhances — not retired.

### Story 20.1: Interactive Explorer Architecture Spike

As a maintainer introducing the project's first rich client-interactive surface,
I want the client-interactivity boundary, data payload, and degrade-to-static contract scoped before any explorer ships,
So that we cross the "pure SVG, no JS" line deliberately and once, with a named budget rather than by accretion.

**Acceptance Criteria:**

1.
**Given** the existing static sunburst geometry and Epic 19's directed-edge model
**When** the spike defines the explorer's data contract
**Then** it specifies a single generation-time payload (node hierarchy + related-edge adjacency) that the client hydrates, names the JS size and dependency budget and whether any framework is introduced, and confirms the payload reuses `FollowUpGeometry` / sunburst weights rather than deriving a second geometry.

2.
**Given** JavaScript-off, reduced-motion, and assistive-technology visitors
**When** the spike documents the degrade contract
**Then** the static Story 10.7 sunburst plus Story 9.13 linked pages remain the no-JS baseline, and the interactive layer is defined as a progressive enhancement over that exact markup — not a parallel site or a second authoring schema — with HTML/SPA parity rules named for any new payload.

### Story 20.2: Zoomable Drill-In Sunburst Navigation

As a maintainer exploring a large project,
I want to click a sunburst wedge to zoom into it and reveal its nested children, then breadcrumb back out,
So that I can traverse epic → story → follow-up depth in place without losing my orientation or opening a new page for every hop.

**Acceptance Criteria:**

1.
**Given** the rendered explorer with JavaScript available
**When** I activate a wedge (click, Enter, or Space)
**Then** the chart re-centers on that node, expands its children into the rings, and shows a breadcrumb trail of the current zoom scope
**And** activating the center or a breadcrumb crumb navigates back outward without a full page load.

2.
**Given** keyboard and screen-reader users
**When** they traverse the explorer
**Then** focus order, roving-tabindex wedge navigation, and `aria` live announcements of the current zoom scope all work
**And** a wedge's terminal open action still honors the Story 9.13 destination contract (leaf → detail page, group wedge → generated filtered list page), so the explorer does not invent a parallel navigation scheme.

### Story 20.3: Related-Work Side Pane on Selection

As a Driver inspecting one item,
I want a side pane that lists the work-graph nodes related to my current selection,
So that "what stemmed from what" is visible beside the map instead of buried in per-page reverse panels.

**Acceptance Criteria:**

1.
**Given** a selected explorer node and Epic 19's directed edges
**When** the pane renders
**Then** it groups related nodes by edge kind (stemmed-from, resolves, covers, cites, raised-in), each entry linking to its detail page
**And** the pane updates as the selection changes, reusing Epic 19's edges and Epic 9's parsers without re-counting open items against ProjectCounts.

2.
**Given** a selection with no work-graph edges, or a JavaScript-off visitor (NFR8)
**When** the pane would otherwise be empty or unhydrated
**Then** an empty selection shows a designed empty state
**And** with JS off the relationship data is still delivered as a server-rendered "Related" block, never JS-gated.

<!-- Epic 21 added 2026-07-19 (SCP 2026-07-19, correct-course): value & correlation insights — cross-cutting displays
     that make product value legible and surface correlations across work items AND code. Distinct from Epic 7's
     code-only signals (Stories 7.10–7.12) and from the graph/explorer surfaces (Epics 19/20). Seated as Epic 21
     (the number freed when the contextual-nav cluster folded into Epic 10). All derive at generation time from
     existing artifacts + git (FR31); degrade cleanly when data is absent (NFR8). Spike-optional. FR39. -->

## Epic 21: Value & Correlation Insights — Traceability, Cadence, and Planning↔Code

Give first-time visitors and stakeholders a few high-impact displays that make the product's value legible at a glance and reveal correlations across work items and code: a visual traceability matrix, delivery-cadence signals, and a planning-to-code impact map. All derived at generation time from existing artifacts + git (FR31 determinism), degrading cleanly when the underlying data is absent (NFR8).

**FRs covered:** FR39 (sync into PRD when convenient) · **NFRs:** NFR8, and FR31 (generation-time determinism) · **Depends on:** Story 9.2 (requirement-coverage data), Epic 7 (code citations / git commit→file data), and Epic 19 (work-graph edges) as data sources — does not block any of them.

### Story 21.1: Traceability Coverage Matrix

As a stakeholder judging project rigor,
I want a visual FR/NFR/UX-DR × covering-work grid,
So that coverage completeness and the exact gaps are legible in one glance instead of read line-by-line.

**Acceptance Criteria:**

1.
**Given** the Story 9.2 coverage data and the FR Coverage Map
**When** the matrix renders
**Then** requirements form one axis and covering stories/epics the other, each cell showing covered / deferred-on-purpose / unmapped via the canonical `--status-*` tokens
**And** it carries a Story 10.2-compliant legend and framing sentence, and cells deep-link to the requirement/story pages.

2.
**Given** a project with sparse or no requirement mapping (NFR8)
**When** the underlying data is thin
**Then** the matrix degrades to an honest state (e.g., "coverage not yet mapped") rather than a misleading empty grid
**And** it does not re-count items against the single-source counts (Story 8.3).

### Story 21.2: Delivery Cadence & Story Cycle-Time

As a maintainer reflecting on throughput,
I want to see how work has flowed over time — completion cadence and, where derivable, story cycle-time,
So that delivery rhythm becomes a visible property of the project.

**Acceptance Criteria:**

1.
**Given** git history and story / sprint-status change data
**When** the cadence view renders
**Then** it shows completion-over-time and, where first-touch → done dates are derivable, a cycle-time distribution, each clearly labeled with its analysis window per Story 10.2.

2.
**Given** projects where transition history isn't reliably derivable (NFR8, honesty)
**When** cycle-time can't be trusted
**Then** that metric is omitted or explicitly marked approximate rather than fabricated
**And** the whole surface is generation-time deterministic (FR31) — no per-visitor "now" drift, identical output on a from-scratch CI regen.

### Story 21.3: Planning ↔ Code Impact Map

As someone connecting plans to reality,
I want to see which code areas each epic/story actually touched,
So that "what did this work change" becomes visible instead of inferred.

**Acceptance Criteria:**

1.
**Given** Epic 7 code citations and git commit→file data attributed to stories/epics
**When** the impact map renders
**Then** it correlates planning items with the code areas their commits touched (e.g., epic → touched files / areas), navigable to both the story and the code pages, reusing Epic 19's edges rather than a second schema.

2.
**Given** no commit-to-story attribution available (NFR8)
**When** the correlation can't be built
**Then** the surface is omitted cleanly
**And** it never re-counts open items against ProjectCounts.
