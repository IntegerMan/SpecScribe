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
<!-- 2026-07-21 extension run: Epic 22 (Stories 22.1–22.6) and Epic 23 (Stories 23.1–23.5) formalized from the SCP 2026-07-20 "candidate stories" prose into full ### Story sections with Given/When/Then ACs, so SpecScribe's UI story parser resolves them. Both epics remain backlog/unscheduled/spike-first per ADR 0008/0009; no new FRs/NFRs; sprint-status.yaml already had matching backlog slugs. -->

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

FR41: Optionally ingest external code-analysis findings (SonarCloud first) from a configured source and surface them alongside the entities SpecScribe already models — code files, directories, epics, stories, and requirements — through one source-agnostic findings model, so a project's quality signal is readable in the same place as its delivery signal. The integration is disabled by default and every surface degrades to absent-not-broken when it is unconfigured, disabled, or unavailable.

<!-- FR41 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed) to seat Epic 26 (optional external
     code-analysis insights in the portal, Sonar first). Sync into the PRD when convenient — same treatment as
     FR37–FR40. Note FR37–FR40 are declared at their epics' Epic List entries rather than in this list; FR41 is
     listed here because it is a product capability with cross-epic coverage. -->

FR42: Optionally ingest a test-coverage report produced by the user's own test run and surface per-file and per-directory coverage against the code entities SpecScribe already models — as rollups on code file pages and as an encoding on the treemap/sunburst hierarchy surfaces — alongside the other code-analysis signal, with a link out to the external analysis tool's page for that file where one is configured. Coverage is read from a report the user already generates; it is disabled by default, never runs or requires a test execution itself, and every surface degrades to absent-not-broken when no report is configured or the report cannot be parsed.

<!-- FR42 added 2026-07-26 (owner-directed, this session) to seat Epic 27 (test-coverage insights in the generated
     portal). Deliberately NOT given its own NFR: the opt-in / absent-not-broken posture is AD-4 ("optional insight
     providers may enrich output but never own baseline success"), and the only external-service touch — the link out
     to the analysis tool's per-file page — is already governed by NFR12. Adding an NFR13 that restated either would
     be the drift class CLAUDE.md warns about.
     SCOPE BOUNDARY vs FR41/Epic 26: FR41 owns FINDINGS (discrete, severity-bearing items). FR42 owns COVERAGE (a
     per-file/per-directory METRIC). They attach to the same entities and the owner wants them read together, which
     is a coordination requirement recorded in both epics — not a reason to merge them: coverage has a purely LOCAL,
     credential-free ingestion path that findings do not, and findings have a severity model that coverage does not.
     NAMING COLLISION — load-bearing: `ArtifactCoverage.cs` / `RefreshCoverage()` / the dashboard's "Planning
     Artifacts" panel ALREADY own the word "coverage" in this codebase, meaning PLANNING-ARTIFACT coverage. Story
     27.2 must fix a distinct vocabulary before any surface ships, or the portal will show two unrelated things both
     labelled "coverage". Same class as the still-unresolved PRD-vs-epics.md NFR numbering collision above. -->

### NonFunctional Requirements

NFR1: Baseline generation performance remains responsive for local OSS repositories, with deeper analytics separated from baseline runs.
NFR2: Generation is resilient to partial, malformed, unsupported, or missing artifacts and degrades gracefully with non-fatal notices.
NFR3: Operation is local-first and privacy-preserving, requiring no remote telemetry for core behavior.
NFR4: Architecture is extensible so new framework adapters can be added without core rewrites.
NFR5: Source files are read with shared access and watch mode must not hold write locks on observed files.
NFR6: Cross-surface accessibility semantics (keyboard drill behavior, labels, status text redundancy) are contractual behavior, not optional styling.

<!-- ⚠️ NUMBERING COLLISION, surfaced 2026-07-24 (correct-course, SCP 2026-07-24) — recorded, NOT yet resolved.
     This list and the PRD's § 8 list are numbered INDEPENDENTLY and disagree:
       • PRD "NFR-5 (Progressive enhancement)" — the JS-optional / no-JS baseline requirement.
       • epics.md "NFR5" (above) — shared-access file reads / watch-mode write locks. A DIFFERENT requirement.
       • epics.md "NFR6" (this line) — cross-surface accessibility semantics.
     Stories and ADRs across Epics 20/22/23/24 routinely cite "NFR6" when they mean the PRD's progressive-enhancement
     NFR-5 (e.g. "NFR6 JS-optional baseline", "NFR6 no-JS baselines unchanged"). Those citations point at the wrong
     entry in THIS list — the concept they mean lives only in the PRD, and this list has no progressive-enhancement
     NFR at all. The collision predates this SCP and was found while amending the PRD's NFR-5 per ADR 0013.
     ADR 0013 amends the PRD's NFR-5 ONLY. Nothing in this list changed. Resolving the collision (renumber, add the
     missing NFR here, or adopt one canonical list) is deliberately NOT bundled into this change — it would touch
     many stories' citations and deserves its own pass. Raised as an open item in SCP 2026-07-24. -->

NFR7: Feature configurability parity is required across interactive menu flows and equivalent CLI parameters, with directory-scoped settings persistence.
NFR8: Insight surfaces and guidance affordances (status vocabularies, next-step commands, glossary terms, empty-state hints, follow-up/debt artifact types) are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact.
NFR9: Release builds are reproducible and produced by CI from a clean checkout; publishing to any distribution channel is gated on a passing build + test run.

<!-- NFR9 added 2026-07-10 (SCP 2026-07-10, correct-course) for Epic 16. -->

NFR10: SpecScribe is hardened to run safely and correctly against both public and private codebases before community publication: generated output leaks no secrets or unintended private content, rendered surfaces are injection-safe, untrusted-workspace / tool-resolution attack surfaces are closed, dependencies are audited, and no personal-structure assumptions remain — verified by a dedicated pre-publication hardening review.

<!-- NFR10 added 2026-07-11 (SCP 2026-07-11, correct-course) for Epic 17 (Code Hardening & Release-Readiness Review), which runs after feature completion and before Epic 16's publication/cut stories. -->

NFR11: SpecScribe's own codebase is continuously analyzed by an automated code-quality service on every push to the default branch and on pull requests, with the analysis attached to a reproducible clean-checkout build+test run, and with findings triaged into the project's own backlog rather than only viewed on an external dashboard.

NFR12: External-service integrations are opt-in, offline-safe, and credential-safe: disabled by default, never required for baseline generation, generation succeeds unchanged when the service is unreachable or unconfigured, and no secret, token, or credential value is ever written into generated output or into a directory-scoped settings file that is committed.

<!-- NFR11–NFR12 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed): NFR11 seats Epic 25 (SonarCloud
     continuous analysis of SpecScribe's OWN codebase — dev-time, ships no product code); NFR12 is the cross-cutting
     opt-in / offline-safe / credential-safe posture every external-service integration must honor, and is AD-4
     ("optional insight providers may enrich output but never own baseline success") restated for a NETWORKED
     provider. Epic 26 is the first integration bound by it.
     ⚠️ These two entries are appended to THIS list only. The PRD § 8 NFR list is numbered independently and already
     disagrees with this one (see the NUMBERING COLLISION note above NFR7). This SCP deliberately does NOT resolve
     that collision — it remains its own open item, now with two more entries riding on the unresolved numbering. -->

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
FR38: Epic 20 - Standardized Hierarchy Explorer component (Plotly-based sunburst + treemap over one datasource, one selector, explicit `navigate`|`select` activation mode) used site-wide, plus the related-work pane and the dashboard details pane. Revised 2026-07-24 (SCP 2026-07-24) — was "a progressive enhancement over the static Story 10.7 sunburst"; ADR 0013 retires server-rendered chart SVG, so the text twin (not a static sunburst) is the no-JS contract. Design-locked by ADR 0012 + ADR 0013.
FR39: Epic 21 - Value & correlation insights (traceability coverage matrix, delivery cadence / cycle-time, planning↔code impact map) derived at generation time.
FR40: Epic 24 - File-level change-coupling intelligence (directional confidence/support/lift + cross-boundary emphasis) rendered as an accessible ranked list AND interactive multi-form relationship graphs (force-directed network, chord/arc, adjacency-matrix heatmap) at per-file and whole-repo scopes, derived at generation time from git history.
NFR10: Epic 17 - Pre-publication code hardening and security/privacy review for public + private codebase readiness.
FR41: Epic 26 - Optional external code-analysis findings surfaced against code, directories, and planning entities.
NFR11: Epic 25 - Continuous SonarCloud analysis of SpecScribe's own codebase on every push to main, with findings triaged into the project backlog.
NFR12: Epic 26 - Opt-in, offline-safe, credential-safe posture for external-service integrations.
FR42: Epic 27 - Optional test-coverage insights (per-file and per-directory rollups on code pages and hierarchy surfaces, with link-out to the external analysis tool).

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

### Epic 20: Interactive Project Explorer — Standardized Hierarchy Explorer on Plotly
One **Hierarchy Explorer** component — sunburst + treemap over the same datasource behind one selector, built on **Plotly.js** — used everywhere a sunburst or treemap appears today, with an explicit `navigate` | `select` activation mode; on the dashboard, `select` drives a details pane (high-level details, recommended-prompt button, view-more link). Includes the related-work pane. Design-locked by [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) + [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md); rewritten in place 2026-07-24 (SCP 2026-07-24).
**FRs covered:** FR38 (seat in PRD when convenient)

### Epic 21: Value & Correlation Insights — Traceability, Cadence, and Planning↔Code
High-impact displays that make product value legible and reveal correlations across work items and code: a traceability coverage matrix, delivery cadence / cycle-time, and a planning↔code impact map — all generation-time-derived.
**FRs covered:** FR39 (seat in PRD when convenient)

### Epic 22: Delivery Evolution — JSON IR + Incremental Event-Driven Generation
Make the serialized JSON data-layer the **canonical intermediate representation (IR)** all surfaces project from, and move generation to an **incremental, event-driven model** — without porting the C# analysis core. Design-locked by [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md); **backlog / unscheduled** (design-now, build-later).
**Status:** backlog · **NFRs:** NFR4, NFR6, NFR9

### Epic 23: Front-End Framework for the Projection Layer — Vue + Nuxt (SSR) over the IR
Replace the C# presentation/templating layer with a component-oriented **Vue + Nuxt 3 (universal/SSR)** front end that consumes the Epic 22 IR — for CSS modularity (scoped SFC styling), smaller/more-modular files, and a single renderer. NFR6 baseline preserved by Nuxt prerender; analysis stays in C# (ADR 0006 axis C not reopened). Design-locked by [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md); **backlog / unscheduled**, spike-first, sequences **after** Epic 22.
**Status:** backlog · **NFRs:** NFR6 · **Depends on:** Epic 22 (the IR it consumes)

### Epic 24: File Relationship & Change-Coupling Insights — Directional Metric + Multi-Form Coupling Graphs
Turn "what changes alongside this file" from a flat co-change list into a rigorous, richly visual relationship surface: upgrade coupling from raw symmetric counts to **directional strength** (confidence / support / lift) with cross-boundary "surprising coupling" emphasis, and render the relationships as **polished, interactive graphs in three complementary forms** — force-directed network, chord/arc diagram, and adjacency-matrix heatmap — at **two scopes**: a per-file ego graph on code pages and a whole-repo coupling explorer. The upgraded ranked list is retained as the accessible text-twin. All metrics derive at generation time from the existing `--deep-git` numstat parse (no new git calls); every interactive graph degrades to static SVG + text when JavaScript is off (NFR8).
**FRs covered:** FR40 (sync into PRD when convenient) · **UX-DRs:** UX-DR19, UX-DR20, UX-DR21 · **NFRs:** NFR8 · **Status:** backlog · unscheduled · **Source:** market research 2026-07-22 (git-activity file-level insights).

### Epic 25: Continuous Code-Quality Analysis for SpecScribe's Own Development (SonarCloud)
Put SpecScribe's own codebase under continuous automated analysis: every push to `main` and every pull request builds, tests, and is analyzed by SonarCloud on a clean checkout, with a quality gate that fails loudly and findings that are **triaged into this project's own backlog** rather than left on an external dashboard. Also defines — via a spike and one implementation — the **framework-neutral contract** by which analysis findings reach AI agents doing spec-driven development work, which Epic 26's human-facing surfaces then reuse. Dev-time only: **ships no product code.**
**NFRs covered:** NFR11 · **Status:** backlog · **Note:** pulls the CI build+test foundation ahead of Story 16.2, which is amended to extend this workflow rather than create a second one.

### Epic 26: Optional External Code-Analysis Insights — Findings Alongside Code, Directories, and Planning
Make external code-quality analysis an **optional insight provider** in SpecScribe (AD-4), so a user who has Sonar can see findings rendered against the entities the portal already models — code files, directories, epics, stories, and requirements — through **one source-agnostic findings model** that compiler/analyzer warnings and other services can ride later. Led by an owner-elicited ideation round (26.1) and a decision-first spike (26.2) that settles the ingestion posture, the credential design, and the NFR-3 local-first question with a ratified ADR before any surface is built. Optional in the tool; disabled by default; every surface degrades to absent-not-broken.
**FRs covered:** FR41 · **NFRs:** NFR12, NFR8 · **UX-DRs:** UX-DR17, UX-DR21, UX-DR22 · **Status:** backlog · unscheduled · **Depends on:** Story 25.3 (the findings contract), Epic 7 (code pages), Story 21.3 (`PlanningCodeImpact`, the shipped story↔file miner), Story 5.2 (`SettingsResolver`).

### Epic 27: Test-Coverage Insights — Per-File Coverage on Code Pages and Hierarchy Surfaces
Surface **test coverage** for the user's own codebase against the code entities SpecScribe already models — per-file and per-directory rollups on code file pages and as an encoding on the Code Map treemap and the Hierarchy Explorer — read from a report the user's own test run already produced. SpecScribe **never runs tests**. AD-4 applied to a purely LOCAL provider: no network, no credential, no service dependency in the baseline path, which is the sharp difference from Epic 26.
**FRs covered:** FR42 · **NFRs:** NFR12 (link-out only), NFR8 · **UX-DRs:** UX-DR17, UX-DR21, UX-DR22 · **Status:** backlog · unscheduled · **Depends on:** Epic 7 (code pages), Story 20.5 (Hierarchy Explorer), Story 7.6 (Code Map treemap).

<!-- Epic 27 added 2026-07-26 (owner-directed, during Story 25.1's dev pass). Owner scope call: ROLLUPS AND
     ANALYTICS, not per-line marks — covered/total line COUNTS are carried as numbers, but per-line gutter marks
     are out (Story 27.6 revisits that on evidence).
     KEPT SEPARATE FROM EPIC 26 DELIBERATELY, despite sharing surfaces: coverage is a per-file METRIC with a local,
     credential-free ingestion path; findings are discrete SEVERITY-BEARING items from a networked service. Merging
     would drag coverage into NFR12's credential/offline design for no benefit. The coordination requirement is
     real and is owned by Story 27.4 AC #2: whichever epic lands second EXTENDS the first's code-page section
     rather than adding a second one.
     NAMING COLLISION, LOAD-BEARING: ArtifactCoverage.cs / RefreshCoverage() / the "Planning Artifacts" panel
     already mean PLANNING-ARTIFACT coverage. Story 27.2 must fix a distinct vocabulary via ADR before any surface
     ships, or the portal shows two unrelated metrics both labelled "coverage".
     NO NEW NFR: the opt-in / absent-not-broken posture is AD-4 and the only external touch (link-out) is already
     governed by NFR12. An NFR13 restating either would be drift. -->
<!-- Epics 25–26 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed). Split per owner decision D1:
     Epic 25 = "useful for developing the tool" (dev-time CI, no product code); Epic 26 = "optional in the tool"
     (product capability). Owner decision D2 reframed the AI-agent thread as INBOUND and VISUAL — findings attach to
     entities SpecScribe already models — so the framework-neutral contract lands ONCE in Story 25.3 and Epic 26's
     surfaces consume it, rather than two epics inventing two findings models. Owner decision D3 left the ingestion
     posture (SonarCloud web API vs on-disk export) and the NFR-3 crossing to Story 26.2's spike + ADR.
     The model is SOURCE-AGNOSTIC from the first line — the owner's "we could potentially fold in code analysis
     warnings as well, but that gets to be language dependent" is exactly why Sonar must be instance #1, not the
     schema; additional source classes are scoped to Story 26.7.
     STRUCTURAL CONSEQUENCE: this repository has NO build/test CI today (.github/workflows/ holds only
     publish-docs-live-pages.yml) and the story that would add it — 16.2 — is backlog behind the entire roadmap.
     Story 25.1 therefore stands up the FIRST build+test workflow and Story 16.2 is AMENDED to extend it rather than
     create a second one. Append-only, no renumber. -->

<!-- Epic 24 added 2026-07-22 (owner-directed, from the git-activity file-level-insights market research —
     _bmad-output/planning-artifacts/research/market-git-activity-analysis-tools-file-level-insights-research-2026-07-22.md).
     The research found SpecScribe's file-level surfaces are already the right SHAPE (per-file contributors +
     coupled-files exist; ownership/bus-factor already shipped in Story 7.11) but the COUPLING metric is naive:
     raw symmetric co-change counts. Field standard (code-maat / CodeScene / association-rule research) is
     DIRECTIONAL coupling strength — confidence(A→B)=shared/ChangeCount[A], plus support floor and lift — which
     SpecScribe can compute from the CoChangePairs map it already builds (no new git call). Owner explicitly
     overruled the research's "keep it a list, skip the global graph" recommendation: wants it "as graphical and
     visual and polished as possible" — ALL THREE visual forms (force-directed network, chord/arc, adjacency
     matrix) at BOTH scopes (per-file ego + whole-repo), with the list kept as the accessible twin. Seated as a
     NEW epic (mirroring how Epics 19/20/21 were seated) because it's a coherent visual capability spanning
     several stories. Distinct from Epic 20 (interactive remaining-work sunburst explorer) and Epic 19 (work-item
     provenance graph): this is the CODE co-change relationship graph. Its interactive graphs build on Epic 20's
     client-JS interactivity boundary (Story 20.1 spike) and ADR 0010's zero-dependency JS posture. Story 24.2/24.3
     supersede/evolve the static Charts.ReferenceGraph seeded in Story 7.8. -->

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

<!-- Story 4.9 added 2026-08-06 (owner-directed at create-story 12.2) — a POST-RETROSPECTIVE amendment to Epic 4,
     not a renumber: 4.1/4.2/4.8 are untouched and Epic 4 reopens to in-progress (same pattern as Stories 7.9 and
     8.9). Provoked by a real repository: `C:/dev/CORA` authors its PRD and architecture with BMad
     (`_bmad/` + `_bmad-output/planning-artifacts/`) and then runs delivery entirely in GSD Core (`.planning/`),
     so two adapters' AppliesTo both return true on one tree. Story 12.2 lands the MINIMAL merge it needs
     (run every matching adapter, first-non-null-wins per single-valued family, displaced families diagnosed);
     this story owns the STRATEGIC answer, which 12.2 deliberately does not attempt. Belongs in Epic 4 because
     the question is about the shared adapter contract, not about any one framework. -->
### Story 4.9: Multi-Framework Coexistence Strategy Spike

As a maintainer whose repository uses more than one spec-driven framework at once,
I want a decided strategy for how SpecScribe behaves when several adapters recognize the same tree,
So that a mixed repository gets a coherent portal instead of an arbitrary winner or a silently dropped half.

**Acceptance Criteria:**

1.
**Given** representative repositories that carry more than one framework's markers (the motivating case: BMad for planning artifacts plus GSD Core for delivery, as in `C:/dev/CORA`)
**When** the coexistence question is surveyed against the shared adapter contract
**Then** a written strategy states, per `ArtifactBundle` family, how competing contributions resolve — precedence, merge, or explicit refusal — and what the reader is told about the choice
**And** the single-valued-field conflict (`Epics`, `Sprint`, `Requirements`, `Module`, `EpicsSourceFullPath`) is answered directly rather than deferred to per-framework judgment.

2.
**Given** SpecScribe resolves exactly one `SourceRoot`, which anchors both the `*.md` source enumeration and every source-relative output path
**When** two frameworks keep their artifacts in disjoint directories (`_bmad-output/` and `.planning/`)
**Then** the strategy decides whether source discovery becomes multi-rooted, and if so how output paths stay collision-free and stable
**And** the cost of each option to watch mode (AD-5), the canonical IR's route shape (ADR 0017), and the content-drift gates (ADR 0033) is stated, not assumed.

3.
**Given** the strategy changes a cross-cutting contract
**When** the spike concludes
**Then** it lands as one ADR amending the adapter-selection decision rather than as prose in a story file
**And** it names which of Story 12.2's minimal behaviors it supersedes, so the follow-through is a known, bounded change rather than a rediscovery.

<!-- Story 4.10 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — an append-only
     post-retrospective amendment to Epic 4, not a renumber: 4.1/4.2/4.8/4.9 are untouched (same pattern as
     Stories 4.9, 7.9 and 8.9). Provoked by Story 12.2: Story 12.1 built its GSD coverage map from vendor
     documentation, said so in its own Debug Log, and SIX of its eight derived claims failed on contact with
     one real repository — costing 12.2 five mid-story owner decisions against its own task text. The
     generalizing finding is 12.2 §F1: `extract:ir-content` prunes any rule whose selector is absent from the
     IR, and the extraction corpus is THIS repository's own IR, which is a BMad project — so markup only a
     non-BMad repo produces is pruned WITH THE GATE GREEN (measured: all five `.milestone-band*` rules).
     Every remaining framework epic hits this. Belongs in Epic 4 because the evidence standard is a property
     of the shared adapter contract, not of any one framework. -->
### Story 4.10: Framework Reference Corpus Contract

As a maintainer adding support for a new spec-driven framework,
I want a defined, obtainable set of real adopting repositories to research and verify against before implementation begins,
So that a framework's coverage map is built from how the framework is actually used rather than from its documentation, and rendered values are checked against known-correct projects.

**Acceptance Criteria:**

1.
**Given** framework support has repeatedly been scoped from vendor documentation and corrected during implementation
**When** the reference-corpus contract is written
**Then** it defines what qualifies as a reference repository — a project that USES the framework, explicitly not the framework's own source repository — and sets the target at three per framework, chosen for VARIANCE rather than for similarity
**And** it states the recorded-shortfall rule: when fewer than three qualifying public repositories can be found, the search evidence, the query used, and the substitute (a self-scaffolded `init` repository) are recorded, and the reduced confidence is declared on that framework's page rather than left silent.

2.
**Given** a corpus repository is a moving target and CI has no access to it
**When** a repository is admitted to the corpus
**Then** a committed manifest records its URL, the exact commit SHA inspected, its licence, its approximate size, and the specific variance it was chosen to contribute
**And** the contract states that corpus repositories are dev-time references only, never a test dependency, with every shape they reveal carried into temp-directory fixtures instead.

3.
**Given** a framework's marker directory or file is itself a hypothesis until confirmed
**When** corpus discovery runs
**Then** the contract prescribes the two-pass order — confirm the marker against the framework's own documentation and a scaffolded `init`, then search public repositories BY that confirmed marker — and records a repeatable discovery recipe
**And** the recipe is proven by running it for at least one framework and recording the resulting counts.

4.
**Given** `extract:ir-content` prunes any rule whose selector is absent from this repository's own BMad IR, so markup that only a non-BMad repository produces is silently dropped with every gate green
**When** the contract is written
**Then** it names `CONDITIONAL_CLASSES` seeding as the required step for any cross-framework markup, and states that `web/test/ir-content-harvest.test.mjs` — not the round-trip gate — is the layer that pins it
**And** the hazard is stated once, in a place the remaining framework epics inherit, rather than rediscovered per epic.

5.
**Given** this changes the evidence basis on which the shared adapter contract is extended
**When** the contract concludes
**Then** it lands as one ADR that Epics 11–15 inherit, related to ADR 0038 (adapter selection) and ADR 0041 (multi-framework coexistence) without superseding either
**And** the working convention is additionally recorded in `CLAUDE.md` so an agent that reads no ADR still meets it.

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

### Story 5.6: How to use SpecScribe — CLI Generate and Watch Guidance

<!-- Seeded 2026-07-20 from About SDD redesign: Help → "How to use SpecScribe" currently covers
     reading order + glossary only. Expand with SpecScribe product orientation and CLI generate/watch
     guidance once Epic 5's CLI surface (5.1–5.3) is the source of truth for flags and defaults. -->

As a first-time visitor opening the portal Help menu,
I want "How to use SpecScribe" to explain how to generate and refresh the site from the CLI (and where settings live),
So that demos and onboarding cover both reading the portal and producing it — without scattering CLI docs only in the README.

**Acceptance Criteria:**

1.
**Given** a full generate
**When** I open Help → How to use SpecScribe
**Then** the page still includes the honest reading-order and glossary sections (NFR8)
**And** it adds a concise "Generate with SpecScribe" section covering at least `generate` and `watch` with smart defaults aligned to Stories 5.1–5.3
**And** it links to About Spec-Driven Development for framework orientation (not duplicating that matrix).

2.
**Given** directory-scoped settings and/or CLI overrides from Story 5.2
**When** the How to use page describes configuration
**Then** it names the same effective settings surface users see on Diagnostics (Story 4.8)
**And** copy stays framework-agnostic in shared chrome (NFR8).

### Story 5.7: Fixed `--as-of <date>` Date-Page Cutoff Policy

<!-- Seeded 2026-07-27 at the Epic 5 retrospective (owner decision on Story 5.5's Open Question #3).
     Story 5.5 delivered the three policies its AC #2 required *at least* — machine-local (default),
     utc, last-commit — and flagged a fixed explicit date as out of scope. The owner elected to add it.
     Story 5.5's Open Questions #1 and #2 were confirmed AS IMPLEMENTED at the same retrospective:
     the `--today-policy` name and its `machine-local`/`utc`/`last-commit` tokens stand, and
     `LastCommit` remains `series.Max(day)` (latest authored commit day) for symmetry with
     `LinkedCommitDays`. This story must therefore EXTEND that vocabulary, not re-open it. -->

<!-- 2026-07-27 (create-story, baseline d1722f17): three owner decisions locked, recorded here and on the
     `5-7-…` key in sprint-status.yaml. (D1) `--as-of <DATE>` is its own option and IMPLIES the fixed policy —
     the user does not also pass `--today-policy` — but it collapses onto the EXISTING single configured field:
     provenance, `.specscribe` persistence and the diagnostics row all carry one composite token
     `as-of:2026-07-27` on `today_policy`, so no new `SettingsResolver.Fields` constant and no second
     `SavedSettings` field are introduced. (D2) an `--as-of` date BEFORE the repo's first commit is ACCEPTED
     verbatim — an empty commit-date-page set is the correct answer for a historical snapshot — with no
     rejection and no warning. (D3) the date parses with forgiving `DateOnly.TryParse` pinned to
     `CultureInfo.InvariantCulture`, and the resolved ISO date is echoed on an ordinary run, not only under
     `--show-config`.
     AC DRIFT (recorded per the standing rule): D2 adds one requirement AC #1 does not name — `CommitHeatmap`'s
     accessible name and visible headline currently count the WHOLE series while the cells already stop at the
     cutoff, so a past `--as-of` would have them naming commits the grid does not show. Bounding both to the
     rendered window lands as AC #1a in the story file. It also fixes the pre-existing future-skew case, and it
     is a PORT of the pattern `Charts.DeliveryCadenceHeatmap` already ships, not a new design. The Git Pulse
     signal strip's equivalent overclaim is explicitly OUT of scope and raised as an owner question. -->

As a maintainer producing a portal for a review, a demo, or a historical snapshot,
I want to pin the date-page "today" cutoff to an explicit calendar date,
So that a regenerated portal reproduces the same date-page set regardless of when or where it is generated.

**Acceptance Criteria:**

1.
**Given** I supply an explicit date to the date-page today policy
**When** generation runs
**Then** that date is used as the single resolved `today` by every one of Story 5.5's five cutoff
consumers (`LinkedCommitDays`, date-page generation, artifact-skew, the heatmap grid, and the Git
Pulse guard) with no second resolution anywhere
**And** git commit times still render in each commit's authored offset (Story 10.4 honesty, unchanged)
**And** the dashboard's artifact-staleness `today` stays a separate value from the date cutoff, per
Story 5.5's `dateCutoff` separation.

2.
**Given** the explicit-date policy is set via CLI or persisted in `.specscribe/config.json`
**When** generation runs
**Then** it participates in the existing three-way provenance (`CommandLine` > `SavedSettings` >
`Default`) and appears on `--show-config` and the Diagnostics config log (Story 4.8) like every other
field, with interactive/CLI parity (NFR7 / Story 5.2)
**And** an unparseable or absent date is rejected at the same `SiteSettings.Validate()` gate the other
policy tokens use, with the same forgiving-vocabulary persistence treatment `DatePolicyJsonConverter`
already applies — a bad token must never fail whole-document deserialization and discard sibling
settings (the defect Story 5.5's code review fixed).

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

<!-- 2026-07-28: Epic 8 REOPENED (was `done` since its 2026-07-15 retrospective) to seat Story 8.9, per owner
     decision D4 at create-story. Story 22.3 was retired on 2026-07-27 and its `Status: retired` line renders as
     "Unrecognized" — Story 8.2 promoted `retired` to a first-class stage in the sprint-ledger classifier but never
     in the artifact-status classifier, so the word is canonical in one half of its own seam and unmapped in the
     other. `sprint-status.yaml` carries `epic-8: in-progress` + the `8-9-…` key in this same change. -->

### Story 8.9: `retired` Is a First-Class Story Status

As a maintainer reading the portal after retiring a story,
I want a retired story to read as **Retired** everywhere — badge, counts, epic roll-up, charts and diagnostics — instead of as an unrecognized status word,
So that a deliberate, documented planning decision stops being reported as a defect, and an epic that retires a story can still reach "done".

**Acceptance Criteria:**

1.
**Given** `StatusStyles.ForSprint` maps `retired` to a first-class stage while `StatusStyles.ForStatus` — the classifier that reads a story artifact's `Status:` line — has no arm for it
**When** a story artifact carries a retirement word
**Then** it classifies as `retired`, not `unrecognized`
**And** the retirement vocabulary (`retired`, `superseded`, `deprecated`, `cancelled`, `obsolete`, `wontfix`) lives in exactly one authored place, shared with `EpicsParser.RetirementKeyword`
**And** generation emits no "unrecognized status" diagnostic for it, while a genuinely unmapped word still gets one (Story 8.2 AC #3 is narrowed, not removed).

2.
**Given** Story 8.3's requirement that every count derive from one source
**When** the same retired story is tallied
**Then** the defined-story ledger and the sprint-tracked ledger name the same stage
**And** `retired` is a bucket in the story-stage partition every consumer iterates, with its own word and glyph — never colour alone (UX-DR17).

3.
**Given** an epic containing a retired story
**When** its status is rolled up
**Then** `retired` counts as a terminal stage: all-done-or-retired reads `done`, and an all-retired epic reads `retired`
**And** a retired story is never offered as the next unit of work.

<!-- Full context, the five measured consequences, six traps and the four owner decisions (D1 terminal stage ·
     D2 inline demoted card, no 7th --status token · D3 six-word shared vocabulary · D4 reopen Epic 8) are in
     `8-9-retired-is-a-first-class-story-status.md`. The golden fingerprint moved as expected
     (e384cbde… -> 9bf8ac05…; measured mover set: `specscribe.css` ONLY — no page moved, because this fixture has
     no retired story). Decisions ratified in
     [ADR 0025](../../docs/adrs/0025-retired-is-a-terminal-story-stage-in-both-classifiers.md): `retired` is a
     TERMINAL stage in both classifiers; the epic roll-up COUNTS it (deliberately diverging from
     `SprintTemplater.DeliveryWheel`, which excludes it — both rules are right); the six-word vocabulary is
     single-sourced; and `ForSprint` stays narrower than `ForStatus` on purpose, because `FreeTextBadge` routes
     ADR status lines through it. -->

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) to all four unstarted framework
     spikes (11.1, 13.1, 14.1, 15.1), inheriting Story 4.10's reference-corpus contract. Provoked by Story
     12.2: 12.1's map came from vendor docs and six of eight derived claims failed against one real repo. The
     2026-08-09 public-repo probe found ~6,248 files matching `path:.specify/memory filename:constitution.md`,
     so a three-repo Spec Kit corpus is readily available. -->

3.
**Given** a coverage map built from documentation is a hypothesis, not evidence
**When** the spike surveys the framework
**Then** a reference corpus of three real adopting repositories is selected and pinned per the Story 4.10 contract — each named with its commit SHA, its licence, and the variance it contributes — and every claim in the coverage map is marked as confirmed-against-corpus, contradicted, or unobservable
**And** where fewer than three qualifying public repositories exist, the search query, its result count, and the substitute used are recorded, and the reduced confidence is carried forward as a declared limit into the coverage story.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) to all five framework coverage
     stories (11.2, 12.3, 13.2, 14.2, 15.2), plus AC #6 on the already-implemented 12.2. This is the
     "verify we render EXPECTED VALUES for the sample projects" half of Story 4.10's contract: the spike
     selects and pins the corpus, the coverage story renders against all of it and writes the
     expected-versus-actual record. The fixture-derivation clause promotes Story 12.2's own successful
     practice (CORA is a reference, never a test dependency — CI has no such path) to a requirement. -->

3.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each — covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus repositories are never read by a test.

## Epic 12: GSD and GSD-Pi Coverage

Render key GSD and GSD-Pi planning and tracking artifacts coherently through Epic 4's shared adapter contract, so GSD teams keep progress and scope understandable in one portal. Led by an integration spike that scopes the GSD family's mapping and coverage tiers before baseline coverage. That spike (Story 12.1) established that GSD and GSD-Pi are **distinct products requiring two adapter surfaces**, not one framework with two variants, so baseline coverage is split one story per framework — GSD Core first.

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

<!-- Story 12.2 was SPLIT into 12.2 (GSD Core) + 12.3 (GSD Pi) on 2026-08-02, on Story 12.1's finding and the
owner's decision. The spike established that GSD and GSD-Pi are DISTINCT products, not two versions of one:
GSD Core is markdown-native under `.planning/` (no database, Milestone → Phase → Task) while GSD Pi is
SQLite-authoritative under `.gsd/` (markdown as rendered projections, Milestone → Slice → Task). Their
`AppliesTo` marker, authority model, mid-level noun, and path grammar all differ — only `STATE.md` overlaps — so
one story cannot deliver both. GSD Core goes first: it needs none of the projection-reliability machinery.
See 12-1-gsd-and-gsd-pi-integration-spike.md Completion Notes for the coverage map and the evidence. -->

### Story 12.2: GSD Core Baseline Adapter Coverage

As a team using GSD Core workflows,
I want key GSD Core artifacts rendered coherently,
So that progress and scope remain understandable in one portal.

**Acceptance Criteria:**

1.
**Given** a representative current-version GSD Core repository (a `.planning/` directory of plain Markdown and JSON)
**When** generation runs
**Then** key planning and tracking artifacts render without fatal errors
**And** output remains coherent with existing BMad and Spec Kit surfaces.

2.
**Given** partially supported GSD Core artifacts
**When** they are discovered
**Then** coverage tier labeling communicates interpretation boundaries clearly, reusing the existing CoverageTier vocabulary rather than a parallel scale
**And** unsupported items never block full-site generation.

3.
**Given** GSD Core's Milestone → Phase → Task hierarchy against the two-level epics/stories model
**When** artifacts are projected
**Then** the chosen level mapping and the synthesized story-id form are pinned by a test
**And** requirements are surfaced without claiming a coverage status GSD Core does not record.

<!-- ACs #4 and #5 added 2026-08-06 at create-story, recording five owner decisions taken against a REAL GSD Core
repository (`C:/dev/CORA`) rather than against the vendor documentation Story 12.1 had to rely on. The live repo
overturned several of the spike's derived assumptions — see the story file's "What the real repo changed" section.
D1: Phase → EpicInfo, Plan (`NN-YY-PLAN.md`) → StoryInfo, and Milestone gets its own surface (AC #4). D2: phase
numbers are decimal in practice (`02.1`, `04.5`, `999.1`) and `EpicInfo.Number` is an `int`, so phases take a
synthetic sequential ordinal and carry their real label in the title. D3: GSD Core requirement ids are
project-defined prefixes (`CONV-01`, `RAG-03`), unrepresentable by the closed `RequirementKind` enum whose `Id`
throws, so `REQUIREMENTS.md` is rendered as a document and `Requirements` stays null — AC #3's "without claiming a
coverage status GSD Core does not record" is satisfied by claiming none. D4: this story owns the two shared
prerequisites Story 12.1 found (AC #5). D5: a repo may carry several frameworks at once, so matching adapters MERGE
minimally here; the strategic answer is Story 4.9's. -->

4.
**Given** GSD Core groups its phases under named milestones (`v1.0`, `v2.0`) that carry their own completion state and progress roll-up
**When** the epics index is generated
**Then** phases render as banded groups under a milestone header carrying the milestone's name, state, and rolled-up phase and plan counts
**And** a framework with no milestone level renders exactly as it does today, byte-for-byte.

5.
**Given** SpecScribe selects a single hardcoded adapter and discovers its repo root by a hardcoded `_bmad-output` marker, so a non-BMad repository fails before any adapter is consulted
**When** generation runs against a GSD Core repository, and against a repository carrying both BMad and GSD Core markers
**Then** adapter selection and framework-neutral source-root discovery are in place, every matching adapter contributes, and a family that loses a merge conflict is reported as a non-fatal diagnostic rather than dropped silently
**And** a BMad-only repository's output is unchanged, with the decision recorded as one shared ADR the remaining framework epics inherit.

<!-- AC #6 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed). Story 12.2 shipped correctly but
     on a SINGLE, PRIVATE reference repository (`C:/dev/CORA`). Owner decision at correct-course: REOPEN the
     story (review -> in-progress) and widen its evidence rather than seat a follow-up, so the evidence stays
     with the story that owns the adapter. 12.2 has NOT been code-reviewed yet (Epic 12 reviews at epic end),
     so reopening now is cheaper than reopening after review. The public-repo probe run 2026-08-09 found
     ~1,932 files matching `path:.planning filename:ROADMAP.md "## Phases"` and ~2,547 matching
     `path:.planning/phases filename:PLAN.md`, so a three-repo GSD Core corpus is readily available. Task 12's
     work must be attributed BY HUNK where it touches files a sibling story may also hold (CLAUDE.md
     § Scoping a code review). -->

6.
**Given** GSD Core support was verified against exactly one repository, which is private and unavailable to CI or to any other contributor
**When** the reference corpus is widened to three repositories per the Story 4.10 contract — `C:/dev/CORA` plus at least two PUBLIC adopting repositories, pinned by commit SHA
**Then** generation runs cleanly against all three, an expected-versus-actual record is written for each, and any shape the adapter mishandles is either fixed or recorded as a declared boundary on the GSD framework page
**And** each newly-revealed shape is carried into `GsdCoreArtifactAdapterTests` as a temp-directory fixture, with the corpus repositories themselves never read by a test.

### Story 12.3: GSD Pi Baseline Adapter Coverage

As a team using GSD Pi workflows,
I want key GSD Pi artifacts rendered coherently,
So that progress and scope remain understandable in one portal.

**Acceptance Criteria:**

1.
**Given** a representative current-version GSD Pi repository (a `.gsd/` directory whose markdown is rendered from the authoritative `gsd.db`)
**When** generation runs
**Then** key planning and tracking artifacts render without fatal errors
**And** output remains coherent with existing BMad, Spec Kit, and GSD Core surfaces.

2.
**Given** a GSD Pi repository whose markdown projections are absent because planning documents are kept local-only
**When** generation runs
**Then** a non-fatal notice explains that only the database is current
**And** generation completes with the remaining surfaces intact, never reading the database.

3.
**Given** partially supported GSD Pi artifacts
**When** they are discovered
**Then** coverage tier labeling communicates interpretation boundaries clearly, reusing the existing CoverageTier vocabulary rather than a parallel scale
**And** unsupported items never block full-site generation.

4.
**Given** GSD Pi's Milestone → Slice → Task hierarchy against the two-level epics/stories model
**When** artifacts are projected
**Then** the chosen level mapping and the synthesized story-id form (`"{milestone}.{slice}"`) are pinned by a test
**And** requirements are surfaced without claiming a coverage status GSD Pi does not record.

<!-- AC #5 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — see the note on Story 11.2's
     AC #3. The 2026-08-09 probe found ~281 files matching `path:.gsd filename:STATE.md`, so a three-repo
     GSD Pi corpus is available. NOTE this story's extra hazard: GSD Pi is SQLite-authoritative and its
     markdown is a rendered projection that may be absent entirely (AC #2), so corpus selection must
     deliberately include one repo WITH the markdown projections committed and one WITHOUT. -->

5.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each — covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus repositories are never read by a test.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed), inheriting Story 4.10's
     reference-corpus contract. ⚠️ SPECFLOW IS ONE OF THE TWO FRAMEWORKS WHERE THE SHORTFALL RULE IS EXPECTED
     TO FIRE: the 2026-08-09 probe found ZERO public hits for `filename:.specflow-version` and ZERO for
     `filename:.specflow-config.json`, so this story's own marker hypothesis is unconfirmed and adopters
     cannot be searched for until pass 1 confirms what to search for. Recording the shortfall is the correct
     outcome here — do not work around it by treating `ceatoleii/specflow` itself as a reference repo. -->

3.
**Given** a coverage map built from documentation is a hypothesis, not evidence
**When** the spike surveys the framework
**Then** a reference corpus of three real adopting repositories is selected and pinned per the Story 4.10 contract — each named with its commit SHA, its licence, and the variance it contributes — and every claim in the coverage map is marked as confirmed-against-corpus, contradicted, or unobservable
**And** where fewer than three qualifying public repositories exist, the search query, its result count, and the substitute used are recorded, and the reduced confidence is carried forward as a declared limit into the coverage story.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — see the note on Story 11.2's
     AC #3. ⚠️ Story 13.1's corpus is expected to fall SHORT of three (zero public marker hits at
     2026-08-09); this AC is satisfied against whatever corpus 13.1 actually pinned, and the declared
     confidence limit it recorded must be carried onto the SpecFlow framework page (NFR8). -->

3.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each — covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus repositories are never read by a test.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed), inheriting Story 4.10's
     reference-corpus contract. The 2026-08-09 probe found ~5,144 files matching
     `path:.squad/agents filename:charter.md` and ~700 matching `path:.squad filename:routing.md`, so a
     three-repo Squad corpus is readily available. ⚠️ `bradygaster/squad` is the TOOL, not a reference repo —
     this story's current Task 2 points at it, which AC #3 supersedes. -->

3.
**Given** a coverage map built from documentation is a hypothesis, not evidence
**When** the spike surveys the framework
**Then** a reference corpus of three real adopting repositories is selected and pinned per the Story 4.10 contract — each named with its commit SHA, its licence, and the variance it contributes — and every claim in the coverage map is marked as confirmed-against-corpus, contradicted, or unobservable
**And** where fewer than three qualifying public repositories exist, the search query, its result count, and the substitute used are recorded, and the reduced confidence is carried forward as a declared limit into the coverage story.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — see the note on Story 11.2's
     AC #3. -->

3.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each — covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus repositories are never read by a test.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed), inheriting Story 4.10's
     reference-corpus contract. ⚠️ SUPERPOWERS IS THE SECOND FRAMEWORK WHERE THE SHORTFALL RULE IS EXPECTED TO
     FIRE, and structurally the hardest: this story's own finding #1 is that Superpowers is NEVER installed
     into the target repo (it is an agent plugin), and its only on-disk trace is a USER-OVERRIDABLE plan-path
     convention — so there is no reliable marker to search public repos by. The 2026-08-09 probe produced no
     usable query. `obra/superpowers` is the TOOL, not a reference repo; this story already records that its
     fetched material "documents the tool's own repository, not a downstream project's use of it." A recorded
     shortfall with the query and count IS the correct outcome. -->

3.
**Given** a coverage map built from documentation is a hypothesis, not evidence
**When** the spike surveys the framework
**Then** a reference corpus of three real adopting repositories is selected and pinned per the Story 4.10 contract — each named with its commit SHA, its licence, and the variance it contributes — and every claim in the coverage map is marked as confirmed-against-corpus, contradicted, or unobservable
**And** where fewer than three qualifying public repositories exist, the search query, its result count, and the substitute used are recorded, and the reduced confidence is carried forward as a declared limit into the coverage story.

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

<!-- AC #3 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — see the note on Story 11.2's
     AC #3. ⚠️ Story 15.1's corpus is expected to fall SHORT of three, and structurally so: Superpowers is
     never installed into the target repo, so there is no reliable marker to search adopters by. This AC is
     satisfied against whatever corpus 15.1 actually pinned, and the declared confidence limit it recorded
     must be carried onto the Superpowers framework page (NFR8). -->

3.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each — covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus repositories are never read by a test.

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

<!-- AMENDED 2026-07-25 (SCP 2026-07-25, correct-course): Story 25.1 stands up SpecScribe's FIRST build+test CI
     workflow, because SonarCloud analysis of a C# project must wrap a real build and the owner wants it useful
     DURING development — while this story sits backlog behind the entire roadmap. This story therefore no longer
     CREATES the gate; it HARDENS the Epic 25 workflow into a release-relevant required gate (branch protection,
     required-check status, release-branch coverage, and any release-specific matrix). Do NOT create a second
     build+test workflow — two workflows that both build and test is the exact drift class this project has
     repeatedly paid for. FR/NFR coverage is unchanged: this story still covers NFR9.
     Previous AC #1 wording: "Given a pull request or push to a release-relevant branch / When CI runs / Then it
     restores, builds, and executes the `tests/SpecScribe.Tests` suite on a clean checkout, and the job fails on any
     build or test failure." -->

As a maintainer,
I want every pull request and push to build and run the test suite in CI,
So that release builds start from a known-green baseline and regressions are caught before merge.

**Acceptance Criteria:**

1.
**Given** the build+test+analyze workflow established by Story 25.1
**When** this story runs
**Then** it is extended and configured as a **required** status check for `main` — covering pull requests and pushes — restoring, building, and executing the `tests/SpecScribe.Tests` suite on a clean checkout and failing on any build or test failure
**And** it does **not** introduce a second workflow that duplicates the build or test steps.

<!-- ⚠️ AMENDED 2026-08-08 (owner decision, Story 16.1 second code review). AC #1 read "release-relevant
     branches"; it now reads `main`. THIS IS A DEFERRAL, NOT A DELETION — see § Story 16.10.

     Why: ADR 0040 § Decision 9 was amended the same day to MERGE-TRIGGERED releasing. Stage A is the only
     tagger and it runs only on `main`, so a release branch has no path to a release at all — release-branch
     coverage would describe a capability the pipeline cannot use. ADR 0040 § Decision 2 carries the matching
     non-goal ("the preview is forward-fix only").

     Honesty note, because this AC belongs to a story that has ALREADY MERGED: Story 16.2 shipped `main`-only
     triggers, so the AC as originally worded was never satisfied. The second code review caught the
     mismatch between the shipped workflow, this AC and the ADR's new non-goal. Amending the AC to match what
     shipped — and seating the dropped capability on a named successor — is the honest resolution; silently
     leaving an unmet AC on a merged story is not. `.github/workflows/build-test-analyze.yml`'s header still
     names "release-branch coverage" as this story's job and is Story 16.2's to correct. -->

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

<!-- AC #2 AMENDED 2026-08-08 (Story 16.4 dev, owner decision) — the second clause was UNACHIEVABLE as
     originally written and is now stated as ADR 0040 §Decision 10 resolves it.

     It used to read: "a failed publish leaves no partially-released state (the pipeline is safe to re-run)".
     That cannot be built. The constraint is external and non-negotiable: nuget.org REJECTS a duplicate
     version and permits only unlisting, never deletion; npm rejects publishing over an existing version and
     its unpublish window is time-limited. A multi-channel release is therefore NOT transactional, and
     re-running the SAME tag is a request the registry will refuse — so "safe to re-run" could only ever mean
     "safe to re-run on a NEW tag".

     ADR 0040 §Decision 10 decides that policy (version burn / forward-only recovery) and adds the two
     mechanisms that make the reworded clause true rather than aspirational: a registry preflight that
     refuses a consumed version in seconds instead of 409-ing at the push step, and a DRAFT GitHub Release
     that brackets the irreversible registry pushes so a mid-flight failure leaves something deletable.
     Recorded here rather than as a note in the story file, per CLAUDE.md § Decision records. -->

2.
**Given** a `-preview` / pre-release tag
**When** the pipeline publishes
**Then** the release is marked as a pre-release / preview channel per Story 16.1's policy
**And** a failed publish is recoverable **forward** — the version number is consumed on first publish to any
channel and is never reused, so the pipeline is safe to re-run **on a new tag** (ADR 0040 §Decision 10)
**And** the pipeline refuses a version that is already consumed **before it builds anything**, rather than
failing at the push step
**And** the GitHub Release is created as a **draft** before the registry pushes and published only after they
succeed, so a failure in between leaves a deletable draft rather than an announced release pointing at
packages that do not exist.

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

<!-- 2026-08-07 (Story 16.1, ADR 0040 §Decision 11): NEW BLOCKING DEPENDENCY, cross-epic —
     BLOCKED ON STORY 23.3. Story 16.1's install probe found `EpicsIndexSurface.vue` HARD-THROWS
     when the epics index has no child pages, so a thin or non-BMad external adopter — the
     highest-weight first-run case for this epic — gets `errors=1` and no `epics.html`. That is
     precisely the "working install, not a broken link" this story exists to certify, so 16.7
     cannot pass its readiness check until 23.3 ships the fix. 23.3 keeps it because it owns the
     surface and already fixed the identical defect class one component over
     (`DashboardSurface.vue` handles its own empty case gracefully in the same run).
     Recorded here AND in sprint-status.yaml in the same change, per CLAUDE.md § Decision records:
     a new blocking edge between epics is a structural scope change even though no story was
     added, removed or renumbered. Story 16.1's Task 8 originally certified "no structural scope
     change"; that certification was corrected on this one point. -->

**Depends on:** Story 17.4 (hardening sign-off gates the cut) · **Story 23.7** (the thin-repository
`errors=1` defect above must be fixed before readiness can pass). ⚠️ **RE-SEATED 2026-08-08** — this edge
originally pointed at **Story 23.3**, which closed `done` on 2026-08-08 without shipping the fix, orphaning
the gate. Owner decision at the Story 16.1 second code review: the work gets its own story,
**[Story 23.7](#story-237-empty-state-hardening-for-the-migrated-surfaces)**.

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

### Story 16.9: Composite GitHub Action for External-Project CI/CD Consumption

<!-- 2026-08-06: Seated from an external-consumption audit. The question asked was "what would it take right now
     for another project to generate SpecScribe reports in its CI/CD?" and the measured answer was: vendor this
     entire repository. There is no published channel (nuget.org/packages/SpecScribe → HTTP 404; every 16-* key
     here is backlog; ADR 0022 §5 states it outright — "No Epic 16 channel exists yet"), and since Story 23.6 no
     C# path writes content HTML, so `generate` also needs the prebuilt Nitro artefact that nothing ships. An
     external project must therefore run a second checkout, a ~200 MB `npm ci`, `sync:assets`, `build:package`,
     and set SPECSCRIBE_RENDERER_DIR by hand — six steps whose ORDER is load-bearing and whose failure modes are
     documented only in this repo's own workflow comments.

     WHY THIS IS A STORY AND NOT JUST DOCUMENTATION. The same audit found README.md's existing external recipe
     had been broken since 23.6 and nobody noticed, which is the failure publish-docs-live-pages.yml:63-69 already
     warned about in prose ("when a step acquires a new dependency, audit EVERY workflow that runs it"). A recipe
     that lives as copy-pasteable YAML in N consumers' repositories cannot be audited when a dependency changes;
     an Action can, because the ordering traps live in one versioned place this project owns. The README fix
     landed 2026-08-06 alongside this entry and is a stopgap, not the answer.

     DEPENDS ON STORY 16.3, AND ON ONE SPECIFIC THING WITHIN IT: the renderer artefact being IN the published
     package. `NuxtPrerender.ResolveArtefactDirectory` already probes `renderer/` beside the executable and calls
     it "the Epic 16 packaging shape" — the resolution logic exists and nothing populates it. Until it does, this
     Action can only build from source and inherits the whole toolchain; after it does, the Action collapses to
     install-and-run. Sequence accordingly.

     CONTAINER IMAGE — considered, deliberately NOT seated as its own story yet. A prebuilt image with .NET, Node
     and the renderer baked in solves the same problem for non-GitHub CI (Azure DevOps, GitLab, Jenkins), and the
     same 16.3 dependency governs it. Recorded here so it is not lost; promote it if a non-GitHub consumer appears
     or if the owner wants it decoupled from the Action. Full ACs via create-story when scheduled. -->

As a maintainer of a different spec-driven-development project,
I want to generate and publish a SpecScribe portal from my own CI with a small, versioned workflow step,
So that I can adopt SpecScribe without vendoring its source tree or reproducing its build ordering by hand.

**Acceptance Criteria:**

1.
**Given** a published CLI that carries its renderer (Story 16.3)
**When** an external project references the composite action at a released version
**Then** a single workflow step installs SpecScribe and generates the portal — no second checkout, no `npm ci`, and no `SPECSCRIBE_RENDERER_DIR` set by the consumer
**And** the action surfaces `--source`, `--adrs`, `--output`, `--project-name` and `--deep-git` as inputs, with the same defaults the CLI documents.

2.
**Given** the CLI and the renderer must match
**When** the action resolves a version
**Then** it pins both halves together as one released unit, so a consumer cannot combine a CLI and a renderer from different revisions
**And** the resolved version is echoed in the step log, since a portal that renders from a mismatched pair fails as wrong output rather than as an error.

3.
**Given** generation can partially fail
**When** any page reports an error
**Then** the action fails the step, preserving the CLI's existing `ExitCodes.Failure` contract rather than masking it
**And** a missing prerequisite (unsupported or absent Node) fails with the CLI's actionable message rather than an empty output root.

4.
**Given** consumers need to publish what was generated
**When** the action completes
**Then** it outputs the generated directory path for a following `upload-pages-artifact`/deploy step
**And** the repository documents the end-to-end example, replacing README.md's hand-rolled recipe rather than sitting beside it.

### Story 16.10: Release-Branch Coverage (post-preview)

<!-- ⛔ ADDED 2026-08-08 (owner decision, Story 16.1 second code review). STRUCTURAL: a new story, recorded
     here and in sprint-status.yaml in the same change per CLAUDE.md § Decision records.

     THIS STORY EXISTS SO A DEFERRAL IS NOT A DELETION. Story 16.2's AC #1 originally required the CI gate on
     "release-relevant branches". ADR 0040 § Decision 9 (amended 2026-08-08) made releasing MERGE-TRIGGERED
     from `main` — Stage A is the only tagger and runs only on `main` — so under the preview model a release
     branch has no path to a release, and § Decision 2 carries the matching non-goal. Story 16.2's AC was
     amended to `main` rather than left unmet, and the dropped capability lands here.

     DO NOT SCHEDULE THIS FOR THE PREVIEW. It is deliberately post-preview: forward-fix-only is what makes
     ADR 0040 § Decision 9's model total, and adding release branches before there is a stable release to
     branch FROM would add a path nothing uses. Its natural trigger is the `0.x` → `1.0.0` exit criterion in
     ADR 0040 § Decision 5, where "a defect in a released version that cannot wait for the next cut" becomes
     a real scenario for the first time. -->

**Depends on:** Story 16.2 (owns `build-test-analyze.yml`) · ADR 0040 § Decision 5's `0.x` → `1.0.0` exit
criterion reached, or an earlier owner decision that a hotfix path is needed.

As a maintainer supporting a released version,
I want a defect fixable without shipping everything else that has landed on `main` since,
So that a hotfix is a possibility rather than a forced choice between shipping unrelated work and shipping nothing.

**Acceptance Criteria:**

1.
**Given** `build-test-analyze.yml` triggers only on `main`
**When** this story runs
**Then** its `push` trigger covers the release-branch pattern and the gate is a **required** status check on those branches
**And** it still does not introduce a second workflow that duplicates the build or test steps (epics.md § Story 16.2, AMENDED 2026-07-25).

2.
**Given** ADR 0040 § Decision 9 Stage A tags only `main`
**When** a release branch must produce a release
**Then** the ADR is **amended** to say how — this story does not invent a second, undocumented release path
**And** ADR 0040 § Decision 2's "release branches and hotfixes" non-goal is amended in the same change, because removing a non-goal is as much a decision as adding one.

3.
**Given** forward-fix-only is what currently makes § Decision 9's model total
**When** release branches exist
**Then** the record states what replaces that totality — which commits may be tagged, and how a branch release's version relates to `main`'s — so the versioning scheme does not fork silently.

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
**And** local-first / no-remote-telemetry operation (NFR3) is re-confirmed for every code path added since it was last verified
**And** the audit scope explicitly includes the CI supply chain introduced by Epic 25 (the SonarScanner and any CI actions, plus the third-party service's access to the repository) and, if Epic 26 shipped, its external-service integration — verifying that no credential value reaches generated output or a committed settings file (NFR12), that the integration is off by default, and that the NFR3 re-confirmation accounts for the outbound network path Story 26.2's ADR authorized.

<!-- AC #2's third clause added 2026-07-25 (SCP 2026-07-25, correct-course): Epics 25/26 add a CI scanner, a
     third-party service with repository access, and (if 26 ships) a credentialed outbound integration — all new
     supply-chain and privacy surface. Without naming them here, this epic would audit a pre-Sonar tool. -->


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
**Given** the 13 consolidation clusters named by the 2026-08-07 deferred-work triage — each one root cause that several reviews filed separately because review scope is per-story by File List
**When** the burndown disposes of them
**Then** each cluster is dispositioned **once, as a cluster**, rather than as its individual member entries
**And** the clusters are: `BmadCommands` next-step classifiers routing on raw status strings instead of `StatusStyles`; the filesystem path-comparer policy (ADR-shaped — two existing precedents point opposite ways); unguarded `ToDictionary(e => e.Number)` at 5 sites; git-availability test policy (10 files hard-fail, 3 skip) plus its duplicated footer-strip regex in 8 files; the extension's absent TypeScript test harness; the webview first-paint prelude follow-on (10 items, one architectural split); the Story 24.1 review cluster (route to 24.2/24.3 while Epic 24 is in flight); byte-blind emitters and at-scale bounding (5 items, a failure mode this project has shipped twice); duplicated-constant hygiene; branding/iconography/theming; the ownerless `.claude/launch.json`; the zero-contributor Git Insights gate; and the last watch/SPA staleness residual
**And** a cluster whose members disagree (e.g. a stale citation beside a live one) is resolved against the code, not against the older record.

3.
**Given** the story candidates the same triage identified as belonging to no story today
**When** the burndown seats them
**Then** each is either seated as a real story with a named epic home, or explicitly accepted as a known limitation with the reason recorded
**And** two are treated as time-critical rather than queued: the `FileWatcherServiceTests.BurstOfSaves` flake — which **must be resolved before Story 16.2 lands**, since 16.2 makes that suite a required status check and a load-sensitive test inside one converts a busy runner into a blocked pull request — and `npm run check:ir-content`, believed to be shipping RED under a check that is already required
**And** the remainder are at minimum: the webview Problems wire collapsing `Informational` to `warning` (C# and extension halves must land together, or the notice is dropped entirely rather than mislabelled); `specscribe.js` invisible to all static analysis; the hierarchy dimension-contract validation; multi-epic retro attribution (which contains a real cross-epic correctness bug, not just a heuristic gap); the D2 docs landing page and its 13 mapped edge cases; the nib social-preview card; and webview navigation breadth.

4.
**Given** two items the triage could not resolve from the record and marked NEEDS RE-MEASUREMENT
**When** the burndown begins
**Then** both are measured before any decision rests on them, because each is currently neither confirmed good nor confirmed bad
**And** they are: ADR 0033 §Decision 4, undischarged because `check:parity`'s Ubuntu half was wired into `portability-probe` but never observed — the same single-OS evidence that let its removed predecessor gate ship and then diverge across three environments; and `check:ir-content`'s true state, which must be re-measured through the full load-bearing order (`dotnet build --no-incremental` → `generate` → `extract:ir-content` → `check:ir-content`), since an incremental build reuses a cached assembly, never re-embeds a changed asset, and yields a confidently wrong answer either way.

5.
**Given** the hardening work of this epic is complete
**When** the sign-off is produced
**Then** a release-readiness record states that structural, security/privacy, and performance reviews passed (or lists accepted limitations), and that the tool is cleared to run against public and private codebases
**And** this sign-off is the gate Epic 16's publish/cut stories (16.3+, 16.7) depend on.

<!-- 2026-07-18 (owner-directed, append-only): Story 17.5 seated — investigate oversized source files
     (notably specscribe.css) and propose a split/modularization path before more feature CSS accumulates. -->

<!-- 2026-08-07 (deferred-work triage, append-only): Story 17.4 ACs 2-4 added; the original AC 2 (sign-off)
     renumbered to 5, text unchanged. Rationale: AC 1 said "each open item", which sized the burndown at ~215
     bullets and gave no way to tell a duplicate from a defect. The triage read all 138 sections, verified the
     open items against HEAD, closed 13 as already handled (Story 20.7 deletions; ADR 0034 retiring
     GoldenContentFingerprint; and one _spaCapture item that was the same defect as an entry fixed and struck
     on 2026-07-21), and grouped ~55 more into 13 clusters. Net: 215 -> 202 open, with the real shape now
     legible. ACs 2-4 record that shape so the burndown is not re-derived from scratch. Full evidence per item
     is in deferred-work.md, which now carries a "How to read this file" preamble. -->

<!-- STANDING NOTE for whoever runs this story: deferred-work.md is 1,573 lines / ~300 KB and roughly 60%
     struck-through, and it is PARSED into rendered follow-up pages by DeferredWorkParser. Archiving the closed
     sections would shrink it substantially but costs the on-site audit trail; the triage deliberately left that
     call to the owner rather than making it. Cited line numbers throughout are approximate and must be
     re-resolved by symbol — Story 25.2's own record documents two of its citations moving within a day. -->

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

<!-- Story 18.2 REDEFINED 2026-07-25 (post-18.1 spike, owner-approved scope split): 18.1 found that the first
     unit of work in Epic 18 is not artifact coverage at all but a LIVE module-identity defect — ModuleContext
     infers the module from the skill prefix, and every first-party BMad module except GDS prefixes its skills
     `bmad-`, so CIS/TEA/BMB silently resolve to BmadMethod and inherit BMM's glossary site-wide; single-winner
     ChoosePrimary additionally strips ALL BMM commands from a genuine BMM repo on a manifest-order tie. 18.2 is
     therefore rescoped to that foundation (ADR 0015 Decisions 1/2/4), and its former artifact-coverage ACs move
     VERBATIM to the new Story 18.5, which 18.2 now gates. Sprint key changed
     18-2-priority-bmad-module-baseline-coverage → 18-2-bmad-module-identity-foundation; both artifacts updated
     in the same change. -->

### Story 18.2: BMad Module Identity Foundation

As a team using any BMad module other than BMM,
I want SpecScribe to identify my module correctly — or admit honestly that it does not model it —
So that the portal never asserts a vocabulary my project does not use, and installing a second module never silently degrades the module I already had.

**Acceptance Criteria:**

1.
**Given** a repository whose installed BMad module is not one SpecScribe models (for example `cis`, `tea`, `bmb`, or a BMad Builder-generated custom module)
**When** generation runs
**Then** the module is identified from its module **code** — the `_bmad/{code}/` directory name — rather than from a skill-id prefix, and resolves to an unmodeled identity that carries its real module label and its parsed command catalog while publishing **no** planning docs and **no** glossary
**And** neither the how-to-read glossary nor the site-wide abbreviation expansion presents BMad Method's vocabulary for it
**And** the situation is reported once as a non-fatal `Informational` diagnostic naming the code and label.

2.
**Given** a repository with more than one BMad module installed (for example BMM alongside Test Architect or the Creative Intelligence Suite)
**When** primary-module selection runs
**Then** BMad Method and Game Dev Studio are never demoted below an auxiliary module by installed-manifest ordering, so an existing BMM repository keeps its planning docs, glossary and next-step commands intact after a second module is installed
**And** the About-SDD "Detected" reporting and the selected primary module never contradict each other.

3.
**Given** the modules SpecScribe already supports
**When** the identity change lands
**Then** BMad Method and Game Dev Studio detection, docs, glossary and commands are unchanged, verified against **real** module `module-help.csv` content rather than synthetic fixtures
**And** the existing test suite and the golden byte-parity gate stay green (or any intentional change is re-baselined).

<!-- Stories 18.3–18.4 added 2026-07-19: BMad-authoring-tool integrations explored in chat (bmad-index-docs,
     bmad-forge-idea). 18.3 spike-led per the Epics 11–15/18.1 pattern. Run create-story when scheduled.

     CORRECTED 2026-07-27 (dev-story 18.4): the original seating claimed "18.4 depends on 18.3's pinned contract
     for its blurb-metadata half". IT DOES NOT, and 18.4 shipped without it. 18.3 is about `bmad-index-docs`'
     `index.md`; a forge workspace carries its OWN `goal:` in its own `.memlog.md` frontmatter, so the blurb half
     is satisfiable with no external contract. 18.4 was gated by nothing — not by 18.3, and not by 18.2 either
     (`bmad-forge-idea` ships in BMad's `core`, the one module `ModuleContext.Detect` excludes, so ideas never
     route through module identity — the same finding 18.3 records for `bmad-index-docs`). If the index.md
     follow-on ever lands, ideas may adopt it as OPTIONAL enrichment.

     FR COVERAGE, resolved 2026-07-27 (dev-story 18.4, Open Question 2 — owner default (c) taken, unanswered):
     FR36 covers BMad's "module and expansion ecosystem beyond the BMM core", but BOTH 18.3 and 18.4 target
     skills that ship in `core`, not in a module. The stretch is ACCEPTED AND NOTED rather than closed by
     widening FR36 or minting a new FR: the work is genuinely Epic 18's ("explore and baseline-cover BMad's own
     authoring surface"), and re-cutting a requirement to fit two delivered stories buys nothing. Revisit if a
     third core-skill surface lands — at that point core-skill artifact coverage is its own FR, not a stretch. -->

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

<!-- AC #4/#5/#6 seated 2026-07-27 (dev-story 18.4) from the four owner decisions elicited at create-story. They
     EXTEND AC #1 rather than replacing it, and are recorded here per CLAUDE.md's "structural scope changes land in
     epics.md AND sprint-status.yaml in the same change" rule. -->

4. **(extends AC #1 — owner decision D1)**
**Given** a discovered forge session workspace
**When** generation runs
**Then** the idea also gets a **synthesized detail page** built from `.memlog.md`'s chronology plus `forged-idea.md` when present, **and** the forge's own `forge-report.html` is carried into the output **verbatim** and linked from that detail page as "the original report".
**Rationale:** linking to `forged-idea.md` alone would leave every killed, clarified and in-progress idea with no destination at all — that file exists only on a hardened exit.

5. **(extends AC #1 — owner decision D3)**
**Given** two or more discovered ideas
**When** the list page renders
**Then** it is **grouped by verdict** — a section per verdict with a heading and a count, ordered Hardened → In progress → Killed — rather than one flat list
**And** a verdict with zero ideas emits **no section at all**, never an empty heading (NFR8).

6. **(new, safety)**
**Given** a `forge-report.html` in a discovered workspace
**When** generation carries it into the portal output
**Then** it is written **only** if it is self-contained and script-free; a report containing a script (or an inline event handler, `javascript:` URL, or embedding element), an external-origin subresource, or one exceeding the carry size cap is **not written**, the detail page renders without the report link, and exactly one `Skipped` diagnostic names which half of the gate it failed.
**Rationale:** the report is LLM-authored HTML landing verbatim inside the portal's own output directory; `SKILL.md` contracts it as self-contained but nothing enforces that. This also keeps the site inside ADR 0013 / NFR-5's JS-optional posture.

<!-- Story 18.5 added 2026-07-25 (post-18.1 spike, owner-approved scope split): carries the ORIGINAL Story 18.2
     acceptance criteria verbatim — only the story number and the now-resolved priority module changed. 18.1's
     coverage map selects Test Architect (TEA) as that priority module: it is the only candidate with structured,
     distinctively-named on-disk artifacts (traceability-matrix.csv, nfr-report.md) and they overlap surfaces
     SpecScribe already has (Story 21.1 traceability, Story 9.2 NFR coverage). CIS was assessed and deferred —
     its output declares a bare `output_folder` and already renders via the generic markdown pass, so a CIS
     module case would buy only a glossary. BMad Builder is a non-goal for artifact rendering (a meta-tool whose
     outputs are other modules' scaffolding) but drove Story 18.2's open-world requirement. GATED BY 18.2:
     covering a module is meaningless while module identity is still inferred from the skill prefix. Known
     prerequisite for this story: TEA writes to a `test_artifacts` output key that lives in `_bmad/tea/config.yaml`,
     and SpecScribe reads no module `config.yaml` at all today. -->

### Story 18.5: Priority BMad Module Baseline Coverage

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

<!-- Story 18.6 added 2026-07-27 (create-story 18.5, owner decision D4): ADR 0015 Decision 5a was left out of
     Story 18.2 (identity) and deliberately kept out of Story 18.5 (TEA coverage) so neither story grew a second
     concern. It is the last un-closed surface from the Story 18.1 spike's Finding 3b table: ArtifactCoverage.Specs
     hardcodes eight BMM families keyed off ModuleContext.WellKnownDocs and is built from sourceRelatives ALONE,
     with no reference to ModuleContext.Module at all — so the identity fix does not reach it and a non-BMM repo's
     dashboard asserts eight missing BMM artifact families it was never supposed to have. Sequenced after 18.5
     because 18.5 establishes the per-module coverage model this story swaps the family set against. -->

### Story 18.6: Module-Aware Artifact Coverage Families

As a team using a BMad module other than BMM,
I want the dashboard's artifact-coverage panel to reflect my module's artifact families rather than BMad Method's,
So that the portal stops reporting eight missing artifacts my methodology never produces.

**Acceptance Criteria:**

1.
**Given** a repository whose primary BMad module is not BMad Method or Game Dev Studio
**When** the dashboard's artifact-coverage panel renders
**Then** the canonical family set is resolved from the detected module rather than from the hardcoded BMM list, so families the module does not produce are never reported as missing
**And** a module with no modeled family set omits the panel entirely rather than showing an empty or all-missing one (NFR8: absent, not misleadingly empty).

2.
**Given** a BMad Method or Game Dev Studio repository
**When** the change lands
**Then** the existing eight-family panel, its create-command affordances, and its freshness/staleness behavior are unchanged
**And** the existing test suite and the golden byte-parity gate stay green (or any intentional change is re-baselined).

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

## Epic 20: Interactive Project Explorer — Standardized Hierarchy Explorer on Plotly

Turn the project's hierarchy charts into one fluid, explorable, **standardized** surface: a single **Hierarchy Explorer** component — sunburst and treemap over the same datasource behind one selector — built on **Plotly.js**, used *everywhere* a sunburst or treemap appears today, with an explicit **`navigate` | `select` mode** governing what activating a node does. On the dashboard, `select` mode drives a details pane carrying high-level details, the recommended-prompt button, and a view-more link. Paired with the work-graph related-work pane that shows what is related to the current selection.

**FRs covered:** FR38 (sync into PRD when convenient) · **NFRs:** NFR8, NFR-5 (as amended by ADR 0013) · **UX-DRs:** UX-DR5, UX-DR6, UX-DR7 (this epic *restores* the originally-specified interactive-sunburst UX SpecScribe had approximated in pure CSS), UX-DR16, UX-DR17, UX-DR18, UX-DR21 · **Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (Plotly + the component + the mode contract) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (the text twin is the no-JS contract) · **Depends on:** Epic 19 (work-graph edges) as its relationship source, the existing `FollowUpGeometry` / sunburst weights as its hierarchy source, and Story 20.2's committed payload/id contract as the component's datasource shape.

<!-- 2026-07-24 (correct-course, SCP 2026-07-24): Epic 20 REWRITTEN IN PLACE, owner-directed. The epic's original
     framing — "a progressive enhancement over the static Story 10.7 sunburst, which stays the no-JS baseline" — no
     longer holds: ADR 0013 retires server-rendered chart SVG entirely and makes the accessible TEXT TWIN the no-JS
     contract, so there is no static sunburst underneath to enhance. Stories 20.1–20.3 are UNCHANGED and land as
     seeded/shipped (20.2's payload/id contract is precisely what the new component consumes; 20.3's server-rendered
     Related block becomes the text-twin pattern). Story 20.4 is REPLACED — "extract the shared arc math from three
     hand-rolled renderers" is moot once Plotly owns arcs. New Stories 20.5–20.8 carry the component, the ADR 0013
     audit gate, the site-wide rollout, and the home details pane. Root cause this addresses: ADR 0010 §6 already
     required ONE shared engine and it did not hold — three arc renderers in specscribe.js, three DIVERGENT
     Treemap|Sunburst toggles (Code Map and Impact Map order Treemap-first, Git Insights orders Sunburst-first),
     and seven Charts.cs hierarchy entry points. A shared COMPONENT is far harder to accidentally reinvent than a
     shared CONVENTION. -->

**Rollout inventory (verified in code 2026-07-24; the "Converted by" column added 2026-07-25 when create-story 20.7's owner decision D1 split the rollout):**

| Surface | Call site | Server-side entry points | Converted by |
|---|---|---|---|
| Dashboard | `HtmlRenderAdapter.Dashboard.cs:54` | `Charts.Sunburst` + `SunburstCompanionList` | 20.7 |
| Epics | `HtmlRenderAdapter.Epics.cs:32` | `Charts.Sunburst` + `SunburstCompanionList` | 20.7 |
| Epic detail | `HtmlRenderAdapter.Epics.cs:208` | `Charts.EpicSunburst` | 20.7 |
| Story detail | `HtmlRenderAdapter.Epics.cs:550` | `Charts.TaskSunburst` | 20.7 |
| Impact Map | `ImpactMapTemplater.cs:126` | client-rendered treemap/sunburst (Story 21.3) | 20.7 |
| Code Map | `CodeMapTemplater.cs:152,158` | `Charts.CodeTreemap` + `CodeMapSunburst` | **20.9** |
| Git Insights (ownership) | `GitInsightsTemplater.cs:173,178` | `Charts.CodeOwnershipSunburst` + `CodeOwnershipTreemap` | **20.9** |

The split is by *colorize model*, not by convenience: the five 20.7 surfaces resolve their fills from a single
`--status-*` class per node, while the two 20.9 surfaces carry a **live client-side colorize dimension** the
component has no concept of — Code Map renders 7 dimensions × 4 filter variants (8 charts) with a switch that
re-fills every rect, and Git Insights ownership renders 4 live modes plus a contributor select and a staleness
threshold. **`SunburstCompanionList` is not in the inventory: it stays** (Story 20.6 D4).

<!-- Owner request logged 2026-07-22 (Story 7.11 design-feedback session, git-insights.html's Code Ownership
     sunburst): "click and drill into a directory and filter down to that level — at least in the sunburst. You
     can do this via Plotly and it's amazing." NOT implemented as part of 7.11 — explicitly deferred there and
     cross-referenced here because it's the same class of interaction this epic is scoping (click-to-drill,
     zoom, filter-by-selection on a sunburst), just requested against a DIFFERENT sunburst family: the code-
     structure/git-analytics sunbursts Epic 7 owns (Story 7.11's ownership sunburst, Story 7.12's freshness
     sunburst) rather than this epic's epic/story/follow-up remaining-work sunburst (Story 10.7). Two open
     questions for Story 20.1's spike to fold in when it names the interactivity budget/engine: (1) should that
     budget/engine generalize across BOTH sunburst families, or does Epic 7's family get its own follow-on story
     instead of piggybacking on this epic's scope; (2) the owner named Plotly specifically as the desired
     interaction model — a real charting-library dependency, which is a bigger departure than this codebase's
     current zero-dependency JS posture (ADR 0010) and would need its own dependency-budget decision at spike
     time, not an assumed yes. See Story 7.11's Change Log/Dev Agent Record for the full owner-feedback context. -->

<!-- 2026-07-24 (correct-course, SCP 2026-07-24): Story 20.1's remaining open question — "the JS size and dependency
     budget, and whether any framework is introduced" (AC #1) — is now ANSWERED BY ADR 0012, not by this spike: the
     dependency is Plotly, vendored locally. AC #2's degrade contract is SUPERSEDED by ADR 0013 — the no-JS baseline
     is no longer "the static Story 10.7 sunburst plus Story 9.13 linked pages" but the server-rendered TEXT TWIN.
     The story is left as-shipped rather than rewritten (its zero-dep recommendation was correct for what was known
     at the time, and Story 20.2 was built against it); read ADR 0012 + ADR 0013 as the current authority. Story 20.4
     now carries the measurement work this spike deferred. -->

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

<!-- Story 20.4 REPLACED 2026-07-24 (correct-course, SCP 2026-07-24, owner-directed). It was seated 2026-07-23 by the
     Epics 19+21 joint retrospective as "extract the shared arc/radial math from the three hand-rolled renderers into
     one module" — the remedy ADR 0010 §6 had already prescribed as a CONVENTION and that three concurrent sessions
     defeated. With ADR 0012 adopting Plotly, there is no hand-rolled arc math left to extract: the three renderers
     are DELETED by Story 20.7, not consolidated. 20.4 is re-tasked to the engine-adoption spike that ADR 0012's
     "Spike validation" section names. The original consolidation intent is not lost — it is fulfilled more strongly
     by Stories 20.5/20.7 (one component, not one math module). The sequencing constraint SURVIVES AND STRENGTHENS:
     this must land before Story 24.2, or Epic 24 adds renderers 4/5/6 to a file whose existing 3 are about to be
     deleted. -->

### Story 20.4: Plotly Engine-Adoption Spike — Vendoring, Budget, CSP, and Accessibility

As a maintainer adopting SpecScribe's first third-party runtime dependency,
I want Plotly's real cost and conformance measured against this codebase before the component is built on it,
So that ADR 0012's ratified direction is validated by numbers — and its two named escalation triggers fire early if they are going to fire at all.

**Acceptance Criteria:**

1.
**Given** ADR 0012's decision to vendor Plotly locally (never CDN, `file://`-safe)
**When** the spike produces a custom build limited to the `sunburst` + `treemap` + `heatmap` traces
**Then** it reports that build's size, and the **net output-size delta** against today's inline SVG across a real generated portal — including `code-map.html`, which has previously reached 82.5 MB
**And** it confirms the vendored asset loads offline and from `file://`, and reports the packaging impact across all three channels (self-contained binary, npx Story 16.8, VSIX Story 16.5).

2.
**Given** ADR 0012's two named escalation triggers
**When** the spike evaluates the webview and accessibility
**Then** it reports whether Plotly renders under the VS Code webview CSP (`script-src 'nonce-…'`, plus Plotly's runtime `<style>` injection) — a failure selects the ADR 0012 §5 text-twin fallback and does **not** reopen the engine choice
**And** it reports **explicit pass/fail** conformance against UX-DR7 (Tab order, Enter/Space drill, Escape up), UX-DR16, UX-DR17, and UX-DR18 reduced-motion — a hard a11y failure Plotly cannot be configured around is the one finding that reopens ADR 0012, and must be reported as such rather than as a polish note.

3.
**Given** ADR 0012's requirement that presentation stays SpecScribe's
**When** the spike renders a representative hierarchy
**Then** it demonstrates the chart driven entirely by the existing `--status-*` and brand tokens with Plotly's default colorways disabled
**And** its findings are recorded back onto ADR 0012 as an addendum (the ADR is already ratified — this validates, it does not gate).

### Story 20.5: The Hierarchy Explorer Component — One Datasource, One Selector, One Mode Contract

As a maintainer who wants site-wide chart changes to land in one place,
I want a single standardized component that renders a sunburst and a treemap over the same datasource behind one selector, with an explicit activation mode,
So that every hierarchy surface shares one implementation, one interaction grammar, and one place to add future features.

**Acceptance Criteria:**

1.
**Given** the node payload shape Story 20.2 committed (`id`, `parentId`, `label`, weight, `statusClass`, `href`, `kind`)
**When** the component renders
**Then** both shapes read that **same** embedded payload — switching shapes never re-derives geometry, never re-counts against `ProjectCounts`, and never issues a fetch (`file://`-safe)
**And** it supplies one selector idiom, one Story 10.2 framing block (legend + analysis window + framing sentence), and one text twin, so no call site hand-writes any of them.

2.
**Given** ADR 0012's `navigate` | `select` mode contract
**When** a node is activated in `navigate` mode
**Then** it follows the node's `href` honoring the Story 9.13 destination contract (leaf → detail page, group → generated filtered list page)
**And** in `select` mode it raises a selection event **without navigating**, the selected node's own destination remains reachable, and the selection is announced to assistive technology.

3.
**Given** Plotly drills in on click by default
**When** the component wires activation
**Then** drill-in is a **distinct affordance** from activation — a node never silently both drills and activates — and breadcrumb drill-up plus URL-hash deep-linking work per UX-DR5/UX-DR6
**And** keyboard traversal, reduced-motion, and non-color status signalling all hold (UX-DR7, UX-DR17, UX-DR18), verified in a live browser.

4.
**Given** a node with no plan yet (a story with zero tasks and no nested deferred, whose true size is unknown) and the owner's 2026-07-24 "bump to average" decision
**When** the datasource projects that node's weight
**Then** the node is sized to the **average weight of the drafted nodes** — not a 1-unit sliver that reads as misleadingly trivial — while every drafted node keeps its honest weight (the floor only lifts, it never shrinks a real wedge), and a project with nothing drafted yet falls back to the historical 1-unit floor
**And** the component **preserves** this policy rather than re-deriving it: the interim SVG glance + Story 20.2 explorer island already ship it via `Charts.SunburstNoPlanStoryWeight` (threaded through `SunburstStoryWeight`/`SunburstEpicWeight`), so Story 20.7's conversion must carry the average-bump forward — verified in a live browser that un-drafted stories render at a typical, clickable size, not a hairline.

<!-- 2026-07-25 (create-story 20.5): four owner decisions elicited and locked. They constrain HOW the ACs above are
     met; they do not amend them. Recorded here as well as in the story file because two of them are visible
     product choices, not implementation detail. (D1) MOUNT — the component lands on the DASHBOARD ONLY, with the
     server-rendered SVG KEPT UNDERNEATH as a live fallback: the component hides it and renders Plotly in its place
     only on SUCCESSFUL mount. Nothing retires, so ADR 0013 §3's per-surface gate and Story 20.6's fingerprint
     replacement stay intact, and Story 20.7's deletion becomes a clean subtraction. (D2) RING GEOMETRY — the 20.4
     spike's Finding C ("parent weight ≠ Σ children", 14 of 25 parents disagree) is resolved as CHILDREN WIN: a
     parent's value is the exact sum of its drawn children. Accepted cost: some epic sweeps shift visibly, because
     today's Charts.SunburstEpicWeight also counts epic-level follow-up PEERS that are not drawn as children.
     (D3) VISUAL DIRECTION — "Labelled explorer": larger radius, Plotly in-sector labels where they fit, a
     breadcrumb bar above the chart, labelled treemap tiles. Accepted cost: it competes with Story 20.3's card rail
     inside `.explorer-layout`, so the story carries a stacking-breakpoint plan the owner verifies in the iterate
     round rather than silently shrinking labels to preserve the rail. (D4) WEBVIEW — the "does the webview keep
     the island?" decision the spike assigned to 20.5 is DEFERRED TO STORY 20.7, which owns RenderParity and is
     where the ADR 0005 CSP amendment lands jointly with Story 23.4; WebviewRenderAdapter.cs is untouched by 20.5. -->

<!-- 2026-07-25 (Story 20.5 owner verify round 3): DENSE EPICS EXPAND IN THE COMPONENT. Recorded here at the
     2026-07-26 code review, which found this decision living ONLY in the story file — CLAUDE.md § Decision records
     requires a structural or visible product decision to land in epics.md too. The owner drilled into Epic 20 and
     found no component stories: Charts.StoryDensityCollapseThreshold is 8 and Epic 20 has exactly 8, so the
     projector emitted a single "8 stories" summary wedge. That collapse is a DRAWING CONSTRAINT of the fixed 380px
     static SVG, not a fact about the work — and the component is larger AND drills, so a drilled epic has the whole
     sweep to itself. Collapsing there hid exactly the stories the reader drilled in to find and made them
     unselectable, which is what select mode exists for. So Charts.SunburstExplorerNodes gains
     `expandDenseEpics` (default FALSE, so Story 20.2's SVG-parity contract and its test are untouched); the
     component and the rail's selectable set both pass TRUE. CONSEQUENCE, stated plainly: the component's node set
     DELIBERATELY diverges from the SVG's while both are live, so the AC #1 anti-drift invariant is now checked as a
     RECONCILIATION (swap the summary wedge for the stories replacing it and the two id sets agree again) rather
     than as raw equality. Weights are unaffected — the summary wedge's weight is exactly the sum of the stories
     that replace it, so Finding C / D2 still holds. Cost: 66 additional nodes on this project's dashboard. -->

<!-- 2026-07-26 (Story 20.5 code review): TWO OWNER DECISIONS. (1) THE COMPONENT OWNS ITS LEGEND. AC #1 requires the
     component to supply the framing block INCLUDING the legend, and it did not: Charts.Framed has no legend slot
     and the only legend on the dashboard came from inside Charts.Sunburst — i.e. inside the D1 fallback that Story
     20.7 DELETES, which would have taken the dashboard's legend with it. HierarchyExplorer.LegendHtml now emits one
     from the statuses the payload actually carries, and it describes the channel actually on screen: Plotly's
     marker.line has no dash, so the component signals the four non-lifecycle statuses with marker.pattern HATCHING
     rather than the SVG's stroke-dash, and the legend hatches to match. (2) CAP CARDS, NOT ENTRIES.
     RelatedWork.MaxEntriesPerGroup STAYS at 12; instead the rail's story-tier cards render behind one disclosure so
     a JS-off reader is not met by 179 stacked cards (416,433 B, 45.7% of the dashboard). Every card stays in the
     DOM — select mode may not fetch (AC #1, file://-safe) — so this caps VISIBLE height, not card existence, and
     ADR 0013's availability contract is satisfied by a disclosure the reader can open. This CLOSES Story 20.5's
     open question #3, which Story 20.3 had left as the owner's lever. -->


### Story 20.6: Text-Twin Audit and Golden-Fingerprint Replacement — the ADR 0013 Gate

As a maintainer retiring server-rendered chart SVG,
I want every affected surface's text twin audited complete and the chart regression net rebuilt before any SVG is deleted,
So that ADR 0013's no-JS contract is proven rather than assumed, and chart regressions stay caught through the transition.

**Acceptance Criteria:**

1.
**Given** ADR 0013's hard per-surface gate and the Epic 20 rollout inventory
**When** each surface's text twin is audited
**Then** every twin is server-rendered, complete (no fact exists only inside the chart), navigable (every link a chart node offers resolves), and non-color (UX-DR17/UX-DR19) — verified **in a live browser with JavaScript disabled**, not by test assertion alone (CLAUDE.md § Verification)
**And** any surface whose twin is incomplete is fixed here, or keeps its server-rendered SVG until it is — the gate is per-surface and blocking.

2.
**Given** `GoldenContentFingerprint` currently derives most of its signal from chart SVG (measured at 69.3% of the dashboard body by the Story 23.1 spike)
**When** charts become client-rendered
**Then** the replacement assertions cover what is now server-rendered — the embedded payload, the component configuration, and the text twin — and land in this story, before the first SVG retirement
**And** the regenerated fingerprint is confirmed stable across two repeated runs before being locked in, naming whose concurrent changes it sits on top of (CLAUDE.md § Concurrent work).

<!-- 2026-07-25 (create-story 20.6): four owner decisions elicited and locked. They constrain HOW the ACs above
     are met; they do not amend them. Recorded here as well as in the story file because two are visible product
     choices, not implementation detail. (D1) TWIN SHAPE = HYBRID — Story 20.5's HierarchyExplorer.TextTwinHtml is
     THE standard twin and is adopted per-surface as 20.7 converts, but Code Map's per-variant file table and the
     Impact Map's `<details open>` epic-grouped list are AUDITED AND KEPT: both are richer than the generic nested
     list (per-file git metrics; epic grouping), and replacing them would re-litigate two shipped reviewed designs.
     (D2) FIX SCOPE = audit all seven, fix ONE — this story fixes dashboard+epics only; the other five are recorded
     as FAILING and KEEP THEIR SVG, which is AC#1's own escape valve ("or keeps its server-rendered SVG until it
     is"). 20.7 fixes each twin as it converts that surface. A recorded FAIL is a deliverable here, not a defect.
     (D3) TWIN VISIBILITY = collapsed `<details>`, closed by default — matches what 20.5 already emits and ADR 0013
     §2 ("visually collapsed is explicitly acceptable — availability, not on-screen duplication"). (D4) DASHBOARD =
     KEEP BOTH — Charts.SunburstCompanionList stays visible and unchanged (it is a designed navigation panel, not
     an accessibility artifact) while the component twin goes `sr-only` there. This ANSWERS Story 20.5's Open
     Question #4, which explicitly deferred the decision to this story. D3 and D4 together require a new
     twin-presentation setting on HierarchyExplorerConfig, which does not exist yet.
     PRE-AUDIT FINDINGS (code-verified 2026-07-25, hypotheses for the live JS-off audit to confirm): the seven
     surfaces split roughly PASS/FAIL as Code Map + Impact Map probable-PASS, dashboard/epics PARTIAL, and epic
     detail + story detail + Git Insights FAIL. Two are load-bearing. (a) SunburstCompanionList can NEVER be the
     dashboard's twin: it is epic-level only — the sunburst's story ring and follow-up ring exist only inside the
     chart — and Charts.cs:668 deliberately OMITS any done epic with zero open follow-ups, a fact the chart draws
     and the grid does not state. That is what makes D4 correct rather than a compromise. (b) Git Insights has NO
     twin at all: Story 7.11 deleted both the files-and-contributors master-detail table AND the ranked ownership
     table, leaving only an aggregate aria-label sentence, while GitInsightsTemplater.cs:14-17 still states the
     page's no-JS contract as ADR 0010 §2's "a real, useful default-mode chart renders with JS off" — the exact
     clause ADR 0013 §4 SUPERSEDES. That surface's entire no-JS story rests on the SVG 20.7 wants to delete.
     ALSO CLOSED: this ADR-0013 Context names "the ownership and FRESHNESS views" as separate surfaces; verified
     that freshness is a COLORIZE DIMENSION of the Code Map (CodeMapTemplater.cs:204-205), not an eighth chart.
     The verified inventory is seven surfaces — note that Code Map renders FOUR filter variants, so code-map.html
     actually carries 8 charts and 4 tables through 20.7's conversion. -->

### Story 20.7: Site-Wide Rollout — Every Sunburst and Treemap Through the Component

As a maintainer eliminating the drift ADR 0010 §6 could not prevent,
I want the hierarchy call sites converted to the component and their superseded renderers deleted,
So that exactly one implementation of a hierarchy chart exists in the codebase.

**Acceptance Criteria** *(AC #1 and #2 AMENDED 2026-07-25 by owner decision D1 at create-story — the rollout splits by risk; the residue is Story 20.9, and Epic 20's "exactly one implementation" finishes there)*

1.
**Given** the Epic 20 rollout inventory, **scoped to: dashboard, epics index, epic detail, story detail, Impact Map**
**When** the rollout completes
**Then** every one of those surfaces renders through the Story 20.5 component with a single consistent selector **ordering** and idiom — ending the current divergence where the Impact Map orders *Treemap, Sunburst* and every other toggle disagrees (the *default shape* remains per-instance config: sunburst for the planning surfaces, treemap for the Impact Map)
**And** each converted surface is verified in a live browser (CLAUDE.md § Verification), with its Story 20.6 twin audit passing before its SVG is retired.

2.
**Given** the superseded implementations **in this story's scope**
**When** the rollout completes
**Then** `Charts.cs`'s three **planning** hierarchy entry points (`Sunburst`, `EpicSunburst`, `TaskSunburst`) and `specscribe.js`'s 20.2 explorer (`initSunburstExplorers`/`initSunburstExplorer`) and the Impact Map's arc renderer (`renderSunburst`/`arcPath`) are removed
**And** no remaining code path constructs a **planning** sunburst or the Impact Map's shapes by any other route (verified by search, not assumed — a symbol's absence is confirmed, per the shared-main verification rule)
**And** the four Code Map / ownership entry points and `initCodeMapPanel` / `initOwnershipSunburst` are left standing and named as Story 20.9's, so a partial rollout cannot be mistaken for a complete one.

3.
**Given** the VS Code webview and the SPA adapter
**When** the converted surfaces render on those hosts
**Then** HTML/SPA/webview parity holds via the existing `RenderParity` harness
**And** the webview presents the text twin as the documented accepted degradation (ADR 0012 §5 / ADR 0013 §7), registered in `HostRenderExceptions` rather than left as a silent divergence — the ADR 0005 CSP amendment stays with Story 23.4, where ADR 0012 §5 requires it to land once.

<!-- 2026-07-25 (create-story 20.7): five owner decisions elicited and locked. D1 AMENDS AC #1 and #2 above (the
     rollout splits, and Story 20.9 is added below); D2-D5 constrain HOW the ACs are met. Recorded here as well as
     in the story file because three of them are visible product choices. Elicited against a code-level read of all
     seven live call sites, not against this epic's inventory table.
     (D1) SCOPE = SPLIT BY RISK. This story converts the four PLANNING surfaces plus the IMPACT MAP (already
     client-rendered, already carrying the exemplar text twin, so its conversion is a JS swap rather than an SVG
     retirement). CODE MAP and GIT INSIGHTS OWNERSHIP move to the new Story 20.9, because both carry a live
     client-side COLORIZE DIMENSION the component has no concept of — Code Map renders 7 dimensions x 4 filter
     variants = 8 charts with a client-side dimension switch that re-fills every rect, and Git Insights ownership
     renders 4 live modes plus a contributor select and a staleness threshold. Neither is expressible in
     HierarchyNode.StatusClass. Consequence to state plainly: EPIC 20's "exactly one implementation of a hierarchy
     chart" is satisfied at 20.9, NOT at 20.7.
     (D2) SELECTOR = STANDARDIZE ORDERING ONLY. Sunburst | Treemap in that order site-wide (the divergence AC#1
     names, and the ordering 20.5 already fixed); WHICH shape is checked on load stays per-instance config. A deep
     file tree reads better as rectangles and demoting that would be a regression dressed as consistency.
     (D3) WEBVIEW = THE TEXT TWIN, the degradation ADR 0012 §5 and ADR 0013 §7 both pre-authorize. This CLOSES
     Story 20.5's D4, which deferred the decision here. The ADR 0005 CSP amendment stays with Story 23.4. Accepted
     cost: webview surfaces lose the chart picture until 23.4 — a sequencing choice, not a technical limit, since
     the 20.4 spike proved Plotly renders under the byte-verbatim shipped policy.
     (D4) IMPACT MAP = EPIC -> DIRECTORY -> FILE, and the epic multi-select becomes a generic subtree filter. The
     chart's shape then MATCHES its epic-grouped text twin (Charts.ImpactMapBody). VISIBLE CHANGE: a file touched
     by three epics draws three times, so root churn reads as TOTAL ATTRIBUTED churn rather than distinct-file
     churn — the legend and framing sentence must say so.
     (D5) PAYLOAD CEILING = MEASURE AFTER THE SVG RETIRES. RelatedWork.MaxEntriesPerGroup stays at 12; today's
     283,263 B of 742,107 B (38.2%) is a PRE-retirement figure and this story deletes the SVG out from under it.
     FOUR CODE-VERIFIED FINDINGS the epic, both ADRs, and Stories 20.5/20.6 all miss. (F1) `SunburstLegend` is
     emitted INSIDE all three entry points being deleted (Charts.cs:614, 1102, 1226), so the legend must move into
     the component's framing block — which ADR 0012 §2 required anyway — and the Story 3.5 legend HOVER-EMPHASIS
     dies regardless, because its CSS keys on `.sb-seg` while Plotly draws `path.surface`, with a guard test
     forbidding a JS re-implementation. (F2) Three of the five surfaces have NO PROJECTOR and HierarchyExplorer
     ships exactly one (ProjectDashboard); ProjectEpic, ProjectStoryTasks and ProjectImpactMap are new work.
     (F3) The Impact Map needs two capabilities the component lacks — a second COLOR FAMILY (its 5-level commit
     ramp; the resolver today hard-codes `"sb-seg " + STATUS_CLASS[...]`) and a client-side NODE FILTER — both of
     which Story 20.9 also needs, so they must be designed for two consumers. (F4) 58 test references point at the
     three entry points being deleted (44 / 11 / 3) and splitting them into rewrite-against-the-payload vs
     delete-as-geometry is the largest and most under-estimated chunk of the story. -->

### Story 20.8: Dashboard Details Pane — `select` Mode in Practice

> **AC #1 was DELIVERED EARLY, in Story 20.5, owner-directed 2026-07-25.** During 20.5's verify round the
> owner selected a story leaf on the live dashboard and got nothing actionable — no card, no command, no
> link — which broke the whole point of `select` mode ("find work I wanted to do and click their quick
> action buttons to copy text to my clipboard so I could start new Claude Code sessions"). The unlock was
> small rather than new: `BmadCommands.PrimaryStoryCommand`, the copy-to-clipboard badge and the card
> renderer all already existed, and Story 20.3 skipped stories only because *"a story wedge NAVIGATES on
> click … so a standalone story card would never be reachable via selection"* — the exact premise 20.5's
> `select` mode invalidated. So the rail now carries a card per SELECTABLE node (story leaves included),
> each with its single most-relevant BMad command and a view-more link, and the story→epic subject fold is
> gone so a story's relationships appear once.
>
> **What 20.8 still owns:** the RICHER pane — task-level detail, deferred children, relationship depth, the
> full per-story command set rather than one primary — plus the **payload ceiling decision this forced**:
> the rail is now **283,263 B of a 742,107 B dashboard (38.2%)**, up from 21.5%, because a card per story
> is a card per story. The lever remains `RelatedWork.MaxEntriesPerGroup` and it is the owner's to pull.

As a visitor exploring the project from the home page,
I want clicking a node in the explorer to populate a details pane beside it rather than navigating away,
So that I can survey the project's structure and read about each part without losing my place.

**Acceptance Criteria:**

1.
**Given** the dashboard explorer configured in `select` mode
**When** I activate a node
**Then** a details pane beside the chart populates with that node's high-level details, its **recommended-prompt button** (reusing the existing `BmadCommands` next-step command surface — not a second command vocabulary), and a **view-more link** to the node's own detail page
**And** the page does not navigate, and the selection is announced to assistive technology as a live region.

2.
**Given** ADR 0006's read-only helper constraint and the no-JS contract
**When** the pane renders
**Then** the recommended-prompt button remains a read-only helper that generates a prompt and never mutates planning artifacts (AD-6)
**And** with JavaScript unavailable the same details and links remain reachable through the server-rendered text twin, so `select` mode never becomes the only path to the information (ADR 0013).

3.
**Given** no selection yet, or a node with no details to show (NFR8)
**When** the pane would be empty
**Then** it shows a designed empty state per UX-DR22 rather than an incidental blank region
**And** the pane reuses Story 20.3's related-work groupings rather than introducing a second relationship vocabulary.

<!-- 2026-07-25 (create-story 20.8): three owner decisions elicited and locked. They constrain HOW the ACs above
     are met; they do not amend them. Elicited against the REAL working tree (Story 20.5's round-2 verify work
     uncommitted), not against this epic text — `RelatedWorkCards.cs` changed between two reads during that
     session. Recorded here as well as in the story file because two are visible product choices.
     (D1) PAYLOAD CEILING = RESTORE THE FOLD, story cards go MINIMAL. This is the ceiling decision the amendment
     above assigns to this story, and it REVERSES round 2's removal of the story→epic subject fold — an
     owner-directed decision taken hours earlier, before the measurement existed. A story's relationship groups
     return to living ONCE under its epic's card (Story 20.3's shipped design); the story's own card keeps title,
     summary, command affordance and "View details →" and carries NO relationship block. Measured starting point:
     283,263 B of a 742,107 B dashboard (38.2%), 104 cards, 78 of them stories, up from 101,435 B / 21.5%.
     `RelatedWork.MaxEntriesPerGroup` STAYS AT 12 — removing the duplication is the honest fix; truncating further
     trades away JS-off completeness for every node including epics. Accepted cost: a story's relationships are
     one click away (its epic's card, its own page, or work-graph.html) rather than on its card.
     (D2) RICHER CARD = more COMMAND and more CHILDREN, not more relationships. `BmadCommands.PrimaryStoryCommand`
     stays the always-visible primary badge; the full `BmadCommands.StoryCommands` set sits behind a COLLAPSED
     NATIVE <details> (JS-off openable) with the primary NEVER repeated inside it; the story's open deferred /
     action children are listed by name via `FollowUpGeometry.DeferredForSource`. This is the amendment's
     "task-level detail, deferred children, the full per-story command set rather than one primary" MINUS
     "relationship depth", which D1 deliberately removes. D1 and D2 pull in OPPOSITE directions by design — D1
     removes the heavy thing (348 relationship links on this portal), D2 adds light things (2-4 command entries
     and a short deferred list per story) — and the net must be MEASURED and reported honestly even if it is up.
     (D3) AGGREGATES = the follow-up ones get cards, `~summary` does not. `epic-N~open`, `epic-N~done`,
     `orphan~open`, `orphan~done`, `unplanned~open`, `unplanned~done` AND THE `unplanned` ROOT get cards (they
     link to real follow-up group pages and represent work with no other card); `epic-N~summary` resolves to its
     parent epic's card instead, because it is the epic restated and its href IS the epic page.
     GAP CLOSED THAT NEITHER 20.3 NOR 20.5 NOTICED, live on shipped reviewed code: `RelatedWork.IslandIdFor`
     maps only `WorkNodeKind.Epic` → `epic-{N}`/`orphan` and `WorkNodeKind.Story` → the bare story id, and
     `SynthesizeNode` returns null for anything else — so the `unplanned` ROOT has NO CARD AT ALL and drilling
     into Unplanned today shows the rail's designed empty state. The full selectable-id map is tabled in the story
     file, and the story carries a COMPLETENESS INVARIANT test (every selectable payload id resolves to a card or
     a named, tested redirect) — the rail's analogue of `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` — so the gap
     cannot recur silently on the next payload change.
     ALSO PINNED: AC #2 is stated precisely rather than loosely — the chart's `HierarchyExplorer.TextTwinHtml` and
     the RAIL's own JS-off stacked view carry DIFFERENT facts (the twin: label, prose status, Detail, resolving
     href; the rail: summary, command badge, view-more link), and the Dev Agent Record must say which surface
     carries which rather than claiming the twin carries the details. -->

<!-- 2026-07-27 (dev-story 20.8): D1, D2 and D3 all IMPLEMENTED as locked; all three ACs met and re-verified live.
     Two findings that change what a future reader should believe about this story, recorded here because both
     contradict text above.

     (1) **D1's PREMISE DID NOT SURVIVE MEASUREMENT, and D1's own number was stale.** The note above says the rail
     is 283,263 B of 742,107 B (38.2%) and that "removing the duplication is the honest fix". Measured on the real
     portal at dev-story start it was already **443,137 B of 878,971 B — 50.4%**, 187 cards, 372 relationship rows
     (the 38.2% predates 20.5's review round and a portal that has since grown to 27 epics). More importantly the
     duplication D1 targets was **worth ~9.7 KB, not ~200 KB**: dropping the fold in 20.5 did not render every
     relationship twice, it RELOCATED each set from the epic card to the story card and added one restated
     "Part of → Epic N" group per story. So D1 is right on design (one home per relationship set, 90 → 17
     relationship blocks, 0 on story cards) but it is NOT a payload-ceiling answer, and the ceiling question the
     amendment assigns to this story is therefore still OPEN.

     (2) **The net is UP, and by a lot — reported as required rather than smoothed.** Final rail: **604,948 B of a
     1,042,036 B dashboard (58.1%)**, 212 cards. Decomposed: D1 −9.7 KB, D3 +~9 KB (25 aggregate/root cards), D2
     **+162,470 B** — 144,726 B of it the 108 command disclosures alone, because every entry renders the shared
     `RenderCommandBadge` (copy button + inline SVG icon + a `send-menu` <details>) at ~1,340 B per disclosure.
     D2 costs ~17× what D1 saves. The rail is now the majority of the dashboard.

     LEVERS FOR THE OWNER'S VERIFY ROUND, none pulled here because all three are the owner's call and two are
     other stories' contracts: (a) `RelatedWork.MaxEntriesPerGroup` 12 → 8, still the stated lever, still unpulled
     per D1; (b) render the disclosure's entries with a LIGHTER affordance than the full command badge — the badge
     is a shared helper (AD-2, anti-pattern 2) so this is a `BmadCommands` decision, not a rail one; (c) drop the
     deferred-children list, the cheaper half of D2 at 17,744 B. Recommended: (b), which is where the bytes are.

     ALSO OBSERVED LIVE, out of scope and unfixed: on a story whose primary is the Address-deferred prompt, the
     badge's visible `<code>` is 744 characters clipped to a 213 px box inside a 320 px rail — the reader sees a
     fragment of what they are copying. `BmadCommands.RenderLabeledCommand` exists for exactly this and the story
     page already uses it for long prompts; switching the rail's PRIMARY badge is Story 20.5's contract, not this
     story's. And `RelatedWork.BuildGroups` dedupes by node id, so two distinct deferred items whose first 90
     summarized characters are identical render as two identical rows (one occurrence on this portal). -->

<!-- Story 20.9 ADDED 2026-07-25 (create-story 20.7, owner decision D1). It is the OTHER HALF of the rollout, not
     a new feature: the two surfaces whose conversion needs component capability that does not exist yet. Epic 20's
     "exactly one implementation of a hierarchy chart" finishes HERE. Also inherits Story 20.6's per-surface twin
     work for these two — 20.6 D2 audits all seven and fixes only dashboard+epics, and its F2 records that Git
     Insights has NO TWIN AT ALL (Story 7.11 deleted both prior ownership tables) while its own doc comment still
     states the superseded ADR 0010 §2 no-JS contract. That surface's entire no-JS story rests on the SVG this
     story deletes, which makes its twin a prerequisite rather than a polish item. -->

### Story 20.9: Colorized Hierarchies — Code Map and Git Insights Ownership Through the Component

As a maintainer finishing the rollout ADR 0012 §2 requires,
I want the two colorize-driven hierarchy surfaces converted to the component and their renderers deleted,
So that "exactly one implementation of a hierarchy chart exists in the codebase" is finally true rather than nearly true.

**Acceptance Criteria:**

1.
**Given** the component's single-`statusClass` color model and these two surfaces' live colorize dimensions
**When** the component is extended
**Then** it carries a **dimension contract**: a node may resolve its fill from any token family through the shipped cascade (never a re-typed token value, AD-7), a surface may offer several dimensions, and switching dimension re-colors in place without re-deriving geometry, re-counting against `ProjectCounts`, or issuing a fetch
**And** the non-color channel holds across every dimension (UX-DR17) — no state is signalled by hue alone.

2.
**Given** the Code Map (`CodeMapTemplater.cs` — 7 colorize dimensions × 4 filter variants = 8 charts, plus the drill breadcrumb and the per-variant file table)
**When** it is converted
**Then** every variant renders through the component with the standard selector ordering, its treemap default shape, and its drill behavior preserved
**And** its per-variant file table is **kept** as the text twin (Story 20.6 D1 — it is richer than the generic nested list, carrying per-file git metrics), audited complete before any SVG is retired.

3.
**Given** Git Insights ownership (`GitInsightsTemplater.cs` — 4 live modes, a contributor select, and a staleness threshold) which Story 20.6 F2 recorded as having **no text twin at all**
**When** it is converted
**Then** a complete, navigable, non-color twin is built for it **first**, verified live with JavaScript disabled, and only then is its SVG retired
**And** its stale ADR 0010 §2 progressive-enhancement doc comment is corrected to the ADR 0013 contract
**And** author information stays descriptive attribution in every mode, never a ranked scoreboard (FR-10, ADR 0010 §4 — unaffected by rendering technology).

4.
**Given** the last four `Charts.cs` hierarchy entry points (`CodeTreemap`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeOwnershipTreemap`) and the last two client renderers (`initCodeMapPanel`, `initOwnershipSunburst`)
**When** the conversion completes
**Then** they are removed, Story 20.7's rollout-completeness allowlist shrinks to empty, and no code path constructs a sunburst or treemap by any route other than the component — **verified by search, not assumed**
**And** the byte accounting is reported against the Story 20.4 spike's projection, since `code-map.html` (−3,493,000 B) and `git-insights.html` (−1,510,735 B) are where the entire portal-wide −4,787,124 B net delta actually lives.

### Story 20.10: Shared Hierarchy Payload Across Code Map's Filter Variants

<!-- Seated 2026-07-28 from Story 20.9's code review (a decision-needed finding the owner asked to be investigated and
     proposed as its own story rather than logged as a bare deferred-work bullet): Code Map's four filter-variant
     panels (full / no-spec / no-tests / no-spec-no-tests) each independently serialize every file in their subset's
     full metric bag + rich hover card, so a file with neither spec/dev nor test status is duplicated 4×. Task 7.7's
     own reported numbers (1421/487/1254/350 sectors per panel) show 3,512 total file-instances serialized across 4
     payloads against 1,421 distinct files — a 2.47× average duplication factor, and it is exactly the "the four
     payloads dominate the page" trigger condition Story 20.9's Open Question #1 pre-registered for revisiting the
     one-payload-per-panel decision (the island is ~74% of code-map.html's current bytes). Backlog, not yet spiked or
     estimated — this entry exists so the investigation's numbers aren't lost, not to commit to an approach. -->

As a maintainer who wants Code Map's byte cost to reflect real information rather than serialization overhead,
I want the four filter-variant panels to stop each independently re-serializing shared files' full payload,
So that `code-map.html`'s size reflects the number of distinct files analyzed, not the number of filter combinations that happen to include them.

**Why now:** Story 20.9's Task 7.7 byte accounting measured `code-map.html` landing at 57% of the Story 20.4 spike's projected saving (−2,146,545 B against a projected −3,493,000 B). The gap is explained almost entirely by rich per-file hover cards, which is expected and accepted — but a `code-review` investigation of the same numbers found that eliminating the four-panel duplication specifically (independent of the hover-card question) could plausibly close most of the remaining gap: roughly 1.96 MB of code-map.html's 4,451,207 B current total, landing at or past the spike's original projection.

**What makes this non-trivial, and why it wasn't done inline in 20.9** *(corrected 2026-07-28 at create-story against the shipped component — the original framing, written from the code review's investigation notes rather than from `specscribe.js`, was wrong in one direction and silent about a harder problem in the other)*: the client-side re-layout capability this entry originally said "doesn't exist yet" **largely does**. `visibleNodes()` already re-projects an embedded payload, re-runs the children-win parent roll-up client-side with the same rule the emitter uses, and re-plots through `Plotly.react`, which performs the area allocation; what is missing is only the filter's *granularity* (it keeps root children and their descendants, not scattered leaves) and per-view scaffolding selection. The genuinely hard part is elsewhere and was not identified until create-story: **`CodeMap.BuildDir`'s single-child directory-chain collapse is variant-dependent.** Proven on this repo's own `.github` — two subdirectories in `full`, but with `.github/agents/**` filtered out it collapses to one node with a different id, a different label and a different parent — so a filtered variant's DIRECTORY node set is not a subset of `full`'s, and a file's `parentId` is a property of (file, view) rather than of the file. A union-tree payload therefore cannot express all four structures without porting a structural rule into JavaScript. Real scope: a payload where each file node is serialized once and each variant's directory scaffolding is emitted server-side alongside a per-view membership mapping; an extension of the existing client filter (never a second projection path); per-view ramp normalization with its legend moving in step; a `DomId`/`HashKey` rework; new tests; and a re-measurement against Story 20.9's numbers.

**What this story does NOT touch:** owner decision D2 (Story 20.9) keeps the pure-CSS exclude-spec/exclude-tests toggle specifically because it is the one filter on the page that works with JavaScript off — but that guarantee is about the **file table** (the twin) remaining filterable without JS, not about the Plotly chart, which already requires JavaScript regardless of how many payloads back it. That guarantee is preserved by construction: the table stays filterable by pure CSS, at row level rather than panel level.

**Acceptance Criteria** *(the drafts below were refined at create-story 2026-07-28; AC#1 is widened to the file table per owner decision D3 — the four tables carry 2,970 rows against 1,189 distinct files, another ~626 KB of the same defect on the same page. See the [Story 20.10 story file](../implementation-artifacts/20-10-shared-hierarchy-payload-across-code-map-variants.md) for the four locked owner decisions and the eight code-verified findings.)*:

1. **Given** the four Code Map filter variants **When** a file appears in more than one variant **Then** its metric bag, its hover card and its file-table row are each serialized once, not once per variant it appears in — **and** each variant's own directory scaffolding is still emitted by the server, so no tree-structure rule is duplicated client-side.
2. **Given** a filter checkbox toggle **When** the active view changes **Then** the treemap/sunburst re-lays-out correctly for the newly-active subset, with the same node set, parent-child structure **including directory-chain collapse**, and rolled-up values a from-scratch server render of that subset would have produced (Story 20.4's four invariants still hold: exactly one root, no `null` in values, `parent == Σ children`, `branchvalues` correct) — and the resolved fills, hatches and accessible names are unchanged from what Story 20.9 shipped for that variant.
3. **Given** the pure-CSS exclude-spec/exclude-tests toggle (D2) **When** JavaScript is disabled **Then** the file table continues to filter correctly, showing exactly the selected variant's rows and no others, with twin completeness (ADR 0013 §2) holding for every variant and an honest empty state where a variant filters down to nothing.
4. **Given** the re-architected payload **Then** the measured byte delta on `code-map.html` is reported against both Story 20.9's post-conversion baseline (4,451,207 B) and the Story 20.4 spike's original projection (−3,493,000 B), with the island saving and the file-table saving reported separately and both isolated from any hover-card or encoding changes.

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

## Epic 22: Delivery Evolution — JSON IR + Incremental Event-Driven Generation

Elevate the serialized JSON data-layer from an optional output adapter (Story 6.7) to the **canonical intermediate representation (IR)** of a SpecScribe project — the durable, serialized form of the AD-2 host-neutral view models plus pre-rendered SVG chart fragments. Static HTML, the SPA, and the VS Code webview become **co-equal projections** of that IR (static HTML stays the JS-optional NFR6 baseline — a projection, not a `<noscript>` afterthought). Generation moves to an **incremental, event-driven model** that recomputes only the changed scope (operationalizing AD-5) and emits IR deltas suitable for a future watch / client-server transport (AD-8). Does **not** port the C# analysis core (ADR 0006 stands). ~~Charts stay pre-rendered SVG *in* the IR.~~ **Revised 2026-07-24 ([ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md), SCP 2026-07-24):** server-rendered chart SVG is retired, so the IR carries **chart data + component configuration** (plus the server-rendered text twin), not pre-rendered SVG markup. This is a **simplification** of this epic — the IR becomes a data document rather than a data-plus-markup document.

**Status:** backlog · unscheduled · design-locked by [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) · **NFRs:** NFR4 (additive), NFR6 (accessibility baseline preserved), NFR9 (reproducible CI) · **Source:** SCP 2026-07-20.

<!-- 2026-07-20 (correct-course, SCP 2026-07-20): Epic 22 seated backlog per ADR 0008 (design-now, build-later). Candidate Epic 23 — Front-End Framework for the Projection Layer (ADR 0009 — Proposed) — is spike-gated, sequences AFTER Epic 22's IR exists, and is NOT seated as a full epic until ADR 0009's rendering-topology (NFR6) choice is ratified. Epic 7 (7.9–7.12) is untouched. -->

**Disposition:** design-now/build-later. **Epic 22 opens with a measurement spike (22.1)** mirroring Story 6.6 — no implementation story is seated until the spike de-risks incremental-recompute correctness and IR-delta transport by numbers. This epic does **not** disturb the in-flight Epic 7 work.

**Candidate stories (illustrative — finalized at kickoff, not committed here):**

- **Story 22.1 — Spike: incremental recompute + IR-delta transport.** Measure changed-scope recompute correctness (incl. AD-5 topology-change invalidation) and delta latency against this repo; gates everything below.
- **Story 22.2 — Canonical IR schema + versioning.** Serialize the AD-2 view models + chart data/component config (per ADR 0013 — *not* pre-rendered SVG) into a versioned IR; generalize the `SectionViewModelSerializationTests` round-trip into an IR golden boundary. ~~**Known constraint:** the existing SPA path has a **byte-blind chunker** (Story 6.6 at-scale measure: `pages-root.json` reached 112.9 MB at 1,461 pages; `code-map.html` 82.5 MB) — the IR schema must chunk by **bytes**, not page count, so large repos don't ship monolithic payloads.~~ **STALE** — Story 22.1 measured `MaxChunkBytes = 2_000_000` already shipping; see the re-scope note on Story 22.2 below. Also amended by **ADR 0016** (the IR carries rendered prose HTML, not re-modelled view models).
- **Story 22.3 — Static HTML rendered from the IR.** Prove byte/behaviour parity with today's golden output when static HTML is a projection of the IR rather than a direct render.
- **Story 22.4 — SPA + webview as IR consumers.** Fold the Story 6.7 SPA adapter and the webview onto the canonical IR; retire duplicate data paths.
- **Story 22.5 — Incremental event-driven regeneration engine.** Operationalize AD-5 over the IR — recompute only changed scope, emit deltas.
- **Story 22.6 — (Optional, spike-gated) client-server delta channel.** A watch server that pushes IR deltas to connected consumers; the "updates only on new events" future.

**Cross-references:** Epic 20 (Interactive Explorer — SpecScribe's first client-JS surface) and Epic 21 (Value & Correlation viz) are natural IR consumers; the IR schema (22.2) should be informed by their data needs, but neither blocks nor is blocked by Epic 22. The presentation-layer follow-on is **Epic 23** (Vue + Nuxt over the IR — [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md)), which sequences after this epic.

<!-- 2026-07-21: formal Story 22.1-22.6 sections added (bmad-create-epics-and-stories continuation run) so the SpecScribe UI's story parser sees this backlog epic's stories; the prose "Candidate stories" bullets above are now superseded by these but left in place for the SCP-provenance narrative. Epic stays backlog/unscheduled — these stories carry no status until Epic 22 is kicked off. -->

### Story 22.1: Spike — Incremental Recompute + IR-Delta Transport

As a maintainer evaluating whether incremental, event-driven generation is viable,
I want a measurement spike that recomputes only the changed scope and measures IR-delta transport,
So that Epic 22's implementation stories are scoped by real numbers rather than assumption (mirroring Story 6.6).

**Acceptance Criteria:**

1.
**Given** this repo's full history and current artifact set
**When** the spike recomputes a single-file/single-artifact change
**Then** it measures and reports recompute correctness (including AD-5 topology-change invalidation cases) and wall-clock latency versus a full-regeneration baseline
**And** results are captured in a spike report artifact, mirroring Story 6.6's report.

2.
**Given** the spike explores an IR-delta transport
**When** a simulated change event is processed
**Then** delta payload size and latency are measured for at least one topology-change scenario (rename/delete) and one content-only change
**And** the report states whether incremental correctness holds without a full-rebuild fallback.

3.
**Given** the spike is a measurement exercise
**When** it completes
**Then** no production code changes ship from this story
**And** its findings gate whether Stories 22.2–22.6 proceed as scoped or are re-scoped.

### Story 22.2: Canonical IR Schema + Versioning

> **⚠ SCOPE RE-SCOPED — the story file's 7 ACs SUPERSEDE the 3 below** (recorded here and in `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records). See [`22-2-canonical-ir-schema-and-versioning.md`](../implementation-artifacts/22-2-canonical-ir-schema-and-versioning.md). Three things moved after these ACs were written:
>
> 1. **AC #2's premise below is STALE — the "byte-blind chunker" was already fixed before this story started.** Story 22.1 measured the shipped code: `SpaDelivery.MaxChunkBytes = 2_000_000` ships alongside `MaxPagesPerChunk = 75`, so the 112.9 MB `pages-root.json` cannot recur. The one real gap it found was narrower: a single page above the cap still produced an over-cap chunk (**3.08 MB measured against a 2 MB guard**). 22.1's gate re-aimed the story at **page-level delta addressing** + **capping oversized pages**. *(As built: the budget now counts the exact JSON-encoded bytes rather than raw UTF-8, so a multi-page chunk can no longer overshoot; the unsplittable single-page case is **declared** in the manifest's `oversizedPages` rather than left silent.)*
> 2. **Story 23.1 handed 22.2 a hard requirement and a live defect** — the IR must carry Markdig-rendered **prose HTML**, and the SPA/webview dashboard captures were dropping 5 anchors (3 of them `code/*.html` links) from the Git Pulse panel. Both folded in by owner direction 2026-07-23.
> 3. **Owner decisions 2026-07-25:** promote `spa/` **in place** (no `ir/` directory, no rename — that is 22.4's call); per-page **hash + byte size only**, no delta transport; and 22.2 **proposes the ADR**.
>
> The ADR trigger is discharged by **[ADR 0016 — The Canonical IR Carries Rendered Prose HTML](../../docs/adrs/0016-ir-carries-rendered-prose-html.md)** (Proposed), which amends ADR 0008 §Decision 1's prose half exactly as ADR 0013 §5 already amended its chart half.

As a maintainer building surfaces on top of a stable data contract,
I want the AD-2 view models and chart data/component configuration serialized into a versioned canonical IR,
So that static HTML, SPA, and webview can all project from one durable, chunked representation.

**Acceptance Criteria:**

1.
**Given** the existing AD-2 host-neutral view models
**When** they are serialized to the canonical IR
**Then** the IR includes a schema version
**And** chart **data + component configuration** (and the server-rendered text twin) are embedded rather than regenerated per-surface — per [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (2026-07-24), which retires pre-rendered SVG chart fragments; this AC previously required embedding those fragments.

2.
**Given** the Story 6.6 at-scale finding that `pages-root.json` reached 112.9 MB and `code-map.html` 82.5 MB at 1,461 pages
**When** the IR is chunked
**Then** chunking is byte-bounded (not page-count-bounded) so no single chunk exceeds a defined size ceiling.

3.
**Given** the existing `SectionViewModelSerializationTests` round-trip pattern
**When** the IR is round-tripped through serialize/deserialize
**Then** output is byte-identical, or documented-equivalent, to the source view models
**And** this becomes the IR's golden boundary test.

### Story 22.3: Static HTML Rendered from the IR

> **⛔ RETIRED 2026-07-27 (owner decision D4, create-story 23.4). Superseded by [Story 23.4](#story-234-migrate-remaining-surfaces--retire-the-c-htmlrenderadapter-for-content).**
> This story and 23.4 are **competing answers to the same question** — who renders static HTML from the IR.
> 22.3 answers "a C# IR-projection path, byte-identical to golden"; 23.4 answers "the Vue/Nuxt projection
> layer writes every `.html` and the C# page render is retired." Both cannot hold, and **Nuxt-over-IR is the
> ratified direction** ([ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md)); Story
> 23.3 has already shipped 189 surfaces on it with `<main>` byte-identical on 189/189. Keeping 22.3 alive
> would institutionalise the two-renderer drift Epic 23 exists to end.
> The ACs below are left in place for provenance and are **not to be implemented**. The same retirement is
> recorded on the `22-3-static-html-rendered-from-the-ir` key in `sprint-status.yaml` in the same change.
>
> **⚠️ Consequence for [Story 22.4](#story-224-spa--webview-as-ir-consumers):** Story 23.4 AC #3 deliberately
> **keeps one C# region-composition path** (nav + wayfinding + `<main>`) feeding the IR *and* the webview/SPA
> — because that path is what the IR is built from. 22.4's AC #3 ("the duplicate, non-IR data paths for SPA
> and webview are retired") must be read against that surviving shared path, not as a mandate to delete it.
> Story 23.4 Task 8 owns restating it.

As a maintainer relying on the JS-optional static HTML baseline,
I want static HTML rendered as a projection of the canonical IR rather than directly from the core pipeline,
So that NFR6's accessibility baseline is preserved while unifying all surfaces on one source of truth.

**Acceptance Criteria:**

1.
**Given** a project generated today via the direct `HtmlRenderAdapter` path
**When** the same project is generated via the IR-projection path
**Then** output is byte-identical to the existing golden baseline, or the diff is enumerated and justified.

2.
**Given** the IR-projection path is now available
**When** a full generation run executes
**Then** performance stays within Story 22.1's measured acceptable range, with no regression beyond the spike's stated tolerance.

3.
**Given** NFR6 (JS-optional baseline)
**When** static HTML is produced from the IR
**Then** it renders and is fully navigable without JavaScript, identical in this respect to the pre-IR baseline.

### Story 22.4: SPA + Webview as IR Consumers

> **⚠ SCOPE ADDITION 2026-07-27 (owner decision, create-story 22.3) — recorded in `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.**
>
> **22.4 inherits the two defects [Story 23.3](../implementation-artifacts/23-3-migrate-baseline-surfaces-dashboard-epics.md) handed back to Epic 22.** They were in retired [Story 22.3](#story-223-static-html-rendered-from-the-ir)'s scope by owner decision earlier the same day, and would otherwise be tracked nowhere:
>
> 1. **The 46-delta pipeline-ordering defect.** `RenderEpicsPages` is called at `SiteGenerator.cs:326`, the pages loop fills `_docs` at `:339`, and `BuildSpaBundle` reads `_docs.Values` at `:3052` — the epics pages render *before* `_docs` exists and the IR renders *after*. 23.3 measured the symptom as differing per-story work-graph node/edge counts across 46 surfaces, and named which side is stale: **the IR is the more complete render, so this is a latent defect in the static page, not a loss in the capture.** Diagnostics event ordering is load-bearing for the golden fingerprint (`SiteGenerator.cs:415-418`) — preserve it, and enumerate the resulting static-page byte delta rather than re-blessing the constant.
> 2. **The two-region-shapes defect.** The IR carries two region shapes: 187 re-rendered family pages carry the page-wayfinding wrapper, while ~853 captured pages slice from *inside* it (`SpaDelivery.ExtractContentRegion` starts at the breadcrumb) and are unbalanced by one element. 23.3's adapter detects both and throws on a band it cannot balance — that workaround is what 22.4 should be able to delete.
>
> **AC #3 below must also be restated.** Per the Story 22.3 retirement note, **[Story 23.4](#story-234-migrate-remaining-surfaces--retire-the-c-htmlrenderadapter-for-content) AC #3 deliberately keeps one C# region-composition path** (nav + wayfinding + `<main>`) feeding the IR and the webview/SPA. AC #3's *"the duplicate, non-IR data paths… are retired"* must therefore be scoped **against that surviving path**, not read as contradicting it. The retired [Story 22.3 file](../implementation-artifacts/22-3-static-html-rendered-from-the-ir.md) is kept as a reference and characterizes exactly that path — the 25-templater inventory, the `NavLocalContext` blocker (there is **no** `path → NavLocalContext` resolver; any path that stops slicing must thread it), eight traps, the ADR constraint table, and the ranked test-gate map.
>
> **⚠ SCOPE RE-SCOPED at create-story 2026-07-27 — the story file's 9 ACs SUPERSEDE the 3 below.** See [`22-4-spa-and-webview-as-ir-consumers.md`](../implementation-artifacts/22-4-spa-and-webview-as-ir-consumers.md); Task 10 there records the drift in both artifacts. Two things moved:
>
> 1. **AC #1 is already satisfied and near-vacuous.** `spa/manifest.json` + `spa/pages-*.json` **are** the IR — ADR 0008 seated that file set and Story 22.2 promoted it **in place** (no `ir/` directory, no rename). The SPA client already consumes the IR; there is nothing to migrate. The real duplication is that `BuildSpaBundle` and `RenderWebviewSurfaces` are two ~200-line builders sharing an identical prelude, an identical epics-family iteration and an identical captured-region loop.
> 2. **Owner decisions 2026-07-27:** **D1** — one region seam plus both inherited defects; the slicers **survive** (they remain the IR's producer for ~853 pages until Story 23.4 replaces them), so this story retires the *duplicate*, not the *slice*. **D2** — **22.4 runs BEFORE 23.4**, so 23.4 inherits one region producer to preserve and its *"delete the page render first and the IR goes dark for 82 % of the site"* circularity is answered in advance; 23.4's AC #7 restatement obligation is discharged by that ordering. **D3** — the **static** page moves to converge the 46-delta, honouring Story 23.3's measurement that the IR is the more complete render.
>
> **✅ DELIVERED 2026-07-28 (dev-story 22.4) — recorded here and in `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.** All 9 ACs met; **[ADR 0024](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md)** proposed (numbered 0024, **not** the 0023 the story file predicted — a concurrent session took 0023 for Story 25.3 and had it ratified the same day; `0019` remains claimed-but-unwritten). What actually shipped, against what was predicted:
>
> - **The seam is three members**, not one: `BuildSurfacePrelude` + `BuildFamilySurfaces` + `CapturedRegions` in `SiteGenerator.cs`. Both surfaces are filters over it. Webview parity measured against a **purpose-built baseline binary** (HEAD's three files restored into a copy of the live tree, so the diff isolates this story from a concurrent session's edits): **828/828 surfaces**, identical set and emission order, byte-identical `entryDocument`, **0** title and `SourcePath` differences.
> - **The two-region-shapes defect was bigger than the estimate.** epics.md said "~853 captured pages … unbalanced"; the measured figure is **594 of 1,400** IR pages unbalanced (the rest carry no pager, so their bare breadcrumb was already balanced). After the fix: **0 unbalanced**, wrapper-bearing regions 189 → 783, every mover **exactly +30 bytes** — the literal `<div class="page-wayfinding">\n` opener.
> - **The 46-delta's root cause was narrower than "pipeline ordering" implied**, and the named line numbers were stale (`:326`/`:339`/`:3052` → `:396`/`:409`/`:3150`). The whole divergence was `ResolveDeferredModel` passing an **empty `_docs`** to `FollowUpRefs.BuildHrefMap`, so every spec resolver's href came back null and `WorkGraph.BuildStory` dropped the resolver node **and** its edge. Fixed with a source-derived href-map overload — **no `_docs` pre-population**, so Trap 2's `alreadyExisted` flip never fires (`updated=0` on every run). Static/IR `<main>` divergence went **47 → 0** across 1,406 pages.
> - **The fix reaches further than the 46 story pages 23.3 measured:** 9 **epic** pages and `work-graph.html` carried the same dropped-resolver defect at epic scope (Epic 1: 13 items/12 links → 16/20). 23.3 never reported those.
> - **`schemaVersion` bumped 1 → 2** (ADR 0016 §Decision 5), with both `EXPECTED_SCHEMA_VERSION` constants moved in the same change.
> - **The golden fingerprint did NOT move.** AC #4's region change cannot touch static bytes (the fixture generates without `--spa`), and AC #5's ordering fix is legitimately **zero-delta on that fixture** — its deferred work carries no spec-doc resolver. Live-repo static delta enumerated page-by-page instead: **1,350 of 1,407 byte-identical**, the 57 movers being 46 story pages + 9 epic pages + `work-graph.html` + `app.html` (asset-version token, a baseline-build artifact). ⚠️ The constant has since moved to `06788c0f…` under a concurrent code review — the story file's `3171cf5c…` was already stale. **Read it from the file.**
> - **Trap 1 was already resolved** before this story began, in commit `811ba17` (after the story's baseline `6017c2c`), in the recommended "strip on both" direction. The unification preserves it and it is now asserted over the **whole** surface set, not per named page.

As a maintainer of the SPA and VS Code webview surfaces,
I want both surfaces to consume the canonical IR instead of their own duplicate data paths,
So that Story 6.7's SPA adapter and the webview stay consistent with static HTML by construction.

**Acceptance Criteria:**

1.
**Given** the Story 6.7 SPA adapter's current whole-site consolidation
**When** it is migrated to consume the IR
**Then** output remains byte-identical to its pre-migration golden baseline, per Story 6.7's own parity bar.

2.
**Given** the VS Code webview's existing view-model contract (Story 6.1)
**When** it is migrated to consume the IR
**Then** webview rendering remains read-only and unchanged in observable behavior.

3.
**Given** duplicate data paths existed before this story
**When** migration completes
**Then** the duplicate, non-IR data paths for SPA and webview are retired.

### Story 22.5: Incremental Event-Driven Regeneration Engine

> **⚠ SCOPE RE-SCOPED at create-story 2026-07-28 — the story file's 8 ACs SUPERSEDE the 3 below** (recorded here and in `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records). See [`22-5-incremental-event-driven-regeneration-engine.md`](../implementation-artifacts/22-5-incremental-event-driven-regeneration-engine.md). The re-scope is **mandated**, not discretionary: [Story 22.1's gate](../implementation-artifacts/22-1-spike-report.md) rules **"22.5 — RE-SCOPE (required)"**, because *"the measured facts forbid building the engine on the current narrow routes as-is."*
>
> 1. **This is a correctness story, not a performance story.** The latency case is already won and already shipped — 22.1 measured the narrow routes at **3×–84×** faster than a full rebuild. What it also measured is that `RegenerateEpics` **is not oracle-faithful even at no-op**: a 56-page work-graph over-count on every epic page (Epic 1: 16 items/20 links incrementally vs 13/12 from a full regen). `specscribe watch` therefore shows a different, inflated work-graph than `specscribe generate` until restart — **a live defect in the shipped tool today**, independent of the IR pivot. The gate's three required items are (a) fix `_workGraph` parity, (b) add topology-change invalidation for the cross-artifact seams no route refreshes, (c) escalate until (a)+(b) are proven against the byte-parity oracle.
> 2. **AC #1's "and re-emitted" half is deliberately NOT in scope (owner decision D2).** Every incremental route already calls `EmitSpaSite`, which rewrites the **whole** manifest and every chunk. AC #1 is read as *recompute*, not *emit incrementally*. Selective emission belongs with the transport it serves — [Story 22.6](#story-226-spike-gated-client-server-delta-channel), which was seeded the same day, is gated on **22.2**'s per-page `contentHash` rather than on this story, and **runs first**. The two are orthogonal: 22.5 makes recompute correct; 22.6 makes transport cheap.
> 3. **AC #2's "rebuild scope escalates as needed" is now specific (owner decision D3).** Full rebuild escalates for **topology** changes only; the narrow route is **kept** for content-only edits — including the epics/story family once parity is fixed — because a story-file save is the dominant edit class in this repo. Note that file-level topology does not escalate today: `RegenerateTopology` and `RegenerateFromDataSource` call `GenerateAll`, but an add/rename/delete of a single `.md` does not.
> 4. **AC #3's "equivalent to a full regeneration" becomes a permanent test (owner decision D4).** 22.1's oracle-diff harness is productionized into `tests/SpecScribe.Tests/`. **No test in the suite today compares an incremental route to a full regeneration** — which is exactly why the 56-page divergence shipped and stayed shipped.
> 5. **Sequencing (owner decision D1):** 22.5 is **gated on [Story 22.4](#story-224-spa--webview-as-ir-consumers)**, whose AC #5 fixes the *same* `_docs`-population ordering seam. The parity gap is **re-measured after 22.4 lands.**
>
>    ⚠️ **CORRECTED by Story 22.4's code review (2026-07-28) — this clause previously stated two things that are false against the shipped code, and 22.5 must not inherit either:**
>    - *"shares one `WorkInventory` across the epics-page, SPA and webview builders"* — **it does not.** 22.4 shipped a narrower fix: a shared *href map* (`FollowUpRefs.BuildHrefMap`'s pair-based overload, reached from `ResolveDeferredModel`). `RenderEpicsPages` still builds its own `WorkInventory` / `ProjectCounts` / `FollowUpGeometry` / `UnplannedWorkGeometry`. The review DID make the SPA and webview share **one** `BuildSurfacePrelude` instance (they previously built two equal ones), but `RenderEpicsPages` cannot join them without moving the golden fingerprint — it runs before the pages loop fills `_docs`, and relocating it reorders the diagnostics stream, which 22.4 AC #7 and 22.5 AC #6 both forbid. **That remaining third is 22.5's, and it is exactly 22.5's own Trap 1 (the nav-gate circularity).**
>    - *"the residue may be only the pre-nav `_workGraph` build, which 22.4 does not touch"* — **22.4 did touch it.** `BuildWorkGraphModel` calls `ResolveDeferredModel`, which is precisely the call 22.4 rerouted; its Completion Notes credit that path for moving 9 epic pages and `work-graph.html` (Epic 1: 13 items/12 links → 16/20). So the static side has ALREADY moved toward the docs-derived answer, and 22.5's Task 1 re-measure must start from that, not from Story 22.1's `811ba17` figures.
> 6. ⚠️ **22.1's stranded-surface list is a lower bound.** Its correctness matrix ran with **deep-git OFF**, so per-commit pages, hotspot/coupling insights, the impact map and git-derived cadence were structurally invisible to the diff. The re-run is deep-git ON.
>
> **⚠ DEV-STORY OUTCOME (2026-07-28) — three of the assumptions above were disproved by measurement. A later reader should start here, not from items 1–6.**
>
> - **The parity defect (AC #2) was already CLOSED by Story 22.4, not by this story.** 22.4's source-derived `FollowUpRefs.BuildHrefMap` overload made the pre-loop and post-loop resolver maps agree, which was the whole of what the two `_workGraph` builds disagreed about. Re-measured against the oracle, the `RegenerateEpics` **no-op control is byte-identical** to a cold `GenerateAll` where 22.1 measured **56** stale pages. Owner decision D1's gate did its job exactly as intended. Trap 1's nav-gate circularity was therefore never entered — nothing needed moving.
> - **`code-map.html` is a CONTENT-change staleness class, not only a topology one.** Item 6's "lower bound" warning was hiding this: the Code Map is a treemap of the source walk and the walk carries each file's **line count**, so editing one tracked file already makes the cached page wrong. Measured, it was the single surviving divergence on *every* change class — content-doc and content-story included. Escalating content edits was refused (owner D3); the three narrow routes re-walk and rewrite `code-map.html` + `risk-quadrant.html` instead.
> - **`GenerateAll` was not idempotent on a REUSED generator, so escalation alone did not produce a coherent site.** Watch holds one generator for the session; `GenerateAll` cleared `_docs` but not `_epicsModel`/`_requirements`/`_cadence`/`_counts`/`_progress`/`_referenceMap` or the `_artifactHrefByRepoRel` cache. Deleting `epics.md` and escalating still left `cadence.html` and `traceability.html` **orphaned**. Invisible to every existing test, because they all build a fresh generator.
> - **Trap 4 resolved AGAINST the exemption.** Keeping Story 5.3 AC #3's bespoke `epics.md`-deleted teardown out of escalation was tried first and diffed: **16 stale, 3 missing**. The missing pages are story artifacts that a full rebuild renders as ordinary docs once `epics.md` is gone — which no teardown *of the epics family* can produce. `epics.md` deletion now escalates, at the cost of the watch log reading `<directory change>`; `ClearEpicsFamilyOutputs` and its 8 tests are untouched and still reachable through `RegenerateEpics` directly.
> - **AC #8 answered YES: the narrow-route model changed architecturally** — rebuild scope is now decided by one named classifier (`SiteGenerator.ClassifyRebuildScope`) consulted BEFORE family routing. Recorded as **[ADR 0027](../../docs/adrs/0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md)** (not the `0024` this story predicted — Story 22.4 took that slot the same day).

As a maintainer running watch mode on a large or actively-changing repository,
I want generation to recompute only the changed scope and emit IR deltas,
So that AD-5's changed-scope principle is fully operationalized rather than partially honored.

**Acceptance Criteria:**

1.
**Given** a single-file content change during watch mode
**When** regeneration runs
**Then** only the affected IR scope is recomputed and re-emitted, consistent with the correctness bar established by Story 22.1's spike.

2.
**Given** a topology change (rename, delete, or structural move)
**When** regeneration runs
**Then** rebuild scope escalates as needed for coherence, per the existing Story 5.3 principle
**And** no stale or orphaned IR fragments remain.

3.
**Given** the incremental engine is active
**When** compared to full regeneration
**Then** output is equivalent, byte-identical or documented-equivalent, to what a full regeneration would produce for the same source state.

### Story 22.6: (Spike-gated) Client-Server Delta Channel

> **⚠️ The three ACs below are SUPERSEDED by the eight in
> [Story 22.6's story file](../implementation-artifacts/22-6-client-server-delta-channel.md)** (create-story
> 2026-07-28, baseline `811ba17`). They were written 2026-07-21, before Stories 22.1, 22.2, 23.1, 23.2, 23.3
> and 23.5 ran. Recorded here in the same change as `sprint-status.yaml`, per CLAUDE.md § Decision records.
>
> 1. **AC #1's "watch server" is not what this story builds — owner decision D2 (2026-07-28).** Read
>    literally it implies a new long-lived **network listener**, which
>    [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) §Consequences already
>    defers (*"a future client/server mode adds a long-lived-process deployment shape — explicitly later; not
>    decided here"*) and nothing since has un-deferred. What ships instead is **AD-8's own two clauses
>    verbatim**: *extension host push* — delta frames on the **existing** `specscribe webview --serve` NDJSON
>    stdout channel, behind a new `--serve-delta` opt-in — and *sidecar polling* — `spa/delta.json` written
>    beside the IR in watch mode only. No port is opened, no listener is bound, no new runtime is introduced,
>    and [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) is
>    untouched. **A `specscribe serve` HTTP/SSE server is explicitly out of scope** and would need its own ADR.
>
> 2. **The push channel already exists, and pushing the whole site is the defect this story closes.**
>    `WebviewCommand.RunServeLoop` ([Commands.cs:142](../../src/SpecScribe/Commands.cs)) has shipped since
>    Story 6.4's deferred item and re-serializes the **entire** payload on every debounced regen — a
>    one-character edit re-ships the whole site, measured by the extension's own guard comment at
>    *"~8 MB whole-site webview payload"*. The same shape holds on the SPA side: `EmitSpaSite` is called from
>    six sites and each rewrites the manifest, every chunk, the script and the entry shell.
>
> 3. **AC #3's spike gate is discharged as a hard abort (owner decision D4), and it is still live.**
>    [Story 22.1](#story-221-spike--incremental-recompute--ir-delta-transport) found transport *viable but
>    gated on 22.2* — which has since landed the per-page `contentHash` addressing — but its 25.3 %/39.9 %
>    figures were driven **only** through `RegenerateEpics`, whose own no-op over-count inflates them, and the
>    byte-perfect `GenerateOne` route was **never delta-measured**. The story's Task 1 re-measures all four
>    watch routes first; if a single-file `GenerateOne` edit's delta is not under 5 % of both the full IR and
>    the full webview payload, the story halts, publishes the measurement, and returns to `backlog` having
>    shipped no production code — AC #3 honored literally rather than rhetorically.
>
> 4. **Sequencing (owner decision D1): 22.6 runs BEFORE
>    [Story 22.5](#story-225-incremental-event-driven-regeneration-engine).** The delta is manifest *N* vs
>    manifest *N−1*, so no incremental engine is required underneath. 22.5 makes *recompute* cheap; 22.6 makes
>    *transport* cheap. They are orthogonal, and 22.1's gate named **22.2**, not 22.5, as 22.6's blocker.
>
> **⚠ DEV-STORY OUTCOME (2026-07-29) — Story 22.6 is implemented; the contract now lives in an ADR.**
> Recorded here in the same change as `sprint-status.yaml`, per CLAUDE.md § Decision records.
>
> - **THE GATE PASSED, and it was re-run until it was honest.** A `GenerateOne` single-file content edit costs
>   **2.72 % of the full IR** and **4.09 % of the full webview payload** (threshold 5 %), stable across two runs
>   to <0.03 pp. Against Story 22.1's **25.3–39.9 %** at chunk granularity — the difference is Story 22.2's
>   page-level addressing, not an accounting change. Full numbers, per route, in
>   [22-6-delta-measurement-report.md](../implementation-artifacts/22-6-delta-measurement-report.md).
>   ⚠ The harness's FIRST run reported a false PASS at 0.000 % because it edited an *ignored* dotfile, so the
>   route returned `Skipped` and rendered nothing — a delta of nothing measured against everything. Liveness
>   (dispatched as expected, not `Skipped`, >0 pages changed) is now part of the gate, not a precondition to it.
> - **The contract is [ADR 0028](../../docs/adrs/0028-delta-transport-is-a-sidecar-and-a-stream-never-a-server.md)**
>   (Accepted 2026-07-29), which also resolves ADR 0008 §Consequences' deferred long-lived-process question as a
>   **no**. The story file guessed `0024` as the next free slot; four ADRs landed in between, so it is **0028**.
> - **Two of the story's own Dev Notes were disproved by measurement and must not be inherited:**
>   1. **Trap 2's premise is false for one route.** `RegenerateFromDataSource` calls `GenerateAll()` on its FIRST
>      line and only afterwards inspects the events to decide what to report, so an unparseable
>      `sprint-status.yaml` returns `Skipped` **having already rewritten the entire IR**. A delta basis gated on
>      the reported outcome would emit a false *unchanged*. The basis is therefore captured at the **emit seam**,
>      never on the outcome. The NDJSON channel keeps the opposite rule for the opposite reason — two channels,
>      two bases, two different correct answers.
>   2. **The golden fingerprint DID move, and AC #4 needs reading precisely.** Every page's markup is unchanged
>      (pinned by a test asserting no static page carries the stamp), but Task 6's own instruction puts the
>      Quiet Stamp's rule in `specscribe.css`, which the fingerprint embeds. The drift was isolated: `078ef476…`
>      without this story's rule (a **concurrent session's** drift), `501ee958…` with it.
> - **⚠ A defect the test suite structurally could not see was caught by live browser verification**, exactly as
>   CLAUDE.md § Verification predicts. The topology degrade-to-full was first derived from the `trigger` LABEL; a
>   concurrent session's save overwrote that label between `RegenerateTopology` setting it and the emit reading
>   it, and the sidecar read `"full": false` while the watch log printed `<directory change> full rebuild`. The
>   signal is now a flag the route sets on itself. Re-verified live under the identical race.
> - **Accepted floor, recorded so it is not rediscovered:** `code-map.html` carries a whole-repo lines-of-code
>   total and therefore changes on EVERY content edit — ~807 KB encoded, the dominant term in every delta. No
>   content edit will produce a delta below ~1.2 % of the IR until that page changes. A page-design problem, not
>   a transport one.

As a maintainer wanting live-updating consumers of SpecScribe output,
I want an optional watch server that pushes IR deltas to connected consumers,
So that a future client/server model becomes possible without committing to it now.

**Acceptance Criteria (superseded — see the story file):**

1.
**Given** Story 22.1's spike found IR-delta transport viable
**When** this story is scheduled
**Then** a watch server pushes deltas conformant with AD-8's transport-is-adapter-specific principle.

2.
**Given** this capability is optional
**When** it is not enabled
**Then** baseline generation and existing watch mode behavior are entirely unaffected.

3.
**Given** this story is explicitly spike-gated
**When** Story 22.1 does not establish viability
**Then** this story remains deferred/unscheduled rather than implemented on assumption.

## Epic 23: Front-End Framework for the Projection Layer — Vue + Nuxt (SSR) over the IR

Replace SpecScribe's C# presentation/templating layer (~4,691 LOC templaters) with a component-oriented **Vue + Nuxt 3 (universal/SSR)** front end that renders from the Epic 22 canonical IR. Motivation is **presentation-layer maintainability** — scoped, component-local CSS (ending the monolithic-stylesheet fragility class, e.g. the `*/`-comment silent-truncation incident), smaller and more modular files, and a **single renderer** (removing the C#-template↔framework drift hazard). This re-opens ADR 0006's **axis B (rendering language) only** — analysis and the IR production stay in C# (ADR 0006 axis C not reopened). ~~Charts stay pre-rendered SVG *in* the IR.~~ **Revised 2026-07-24 ([ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md), SCP 2026-07-24):** chart SVG generation is retired from C# entirely — charts render client-side from IR data via the Epic 20 Hierarchy Explorer component. This **shrinks Story 23.4**, which no longer has to preserve a C# chart-SVG generator when retiring the `HtmlRenderAdapter`.

**Status:** backlog · unscheduled · spike-first · design-locked by [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) · **Depends on:** Epic 22 (consumes the IR) · **NFRs:** NFR6 (baseline preserved by Nuxt prerender), NFR4.

<!-- 2026-07-20 (correct-course, SCP 2026-07-20): ADR 0009 ratified — topology = universal/SSR, framework = Vue + Nuxt 3, C#-rendering north star relaxed for the presentation layer. Epic 23 seated backlog; sequences AFTER Epic 22's IR. Epic 7 untouched. -->

**Ratified direction (ADR 0009):** universal/SSR via **Nuxt 3** — prerender every route to static HTML at build (NFR6 baseline by construction) + minimal client hydration for interactivity; **Vue** with current modern layers (Vite, Nitro, TypeScript, scoped-SFC CSS). Client-only rendering was rejected (would break NFR6 + re-introduce dual-renderer drift).

**Candidate stories (illustrative — finalized at kickoff):**
- **Story 23.1 — Spike: Nuxt-over-IR feasibility.** Prove Nuxt SSG/prerender NFR6 baseline, scoped-CSS migration of one representative surface, chart-SVG injection, webview-CSP survival under a hydration nonce, Markdig-prose fidelity, and the cost of adding Node to the generation pipeline (reconcile with the ADR 0005/0006 self-contained-binary distribution).
- **Story 23.2 — Component library + design-token bridge.** Port the shared presentation tokens (status/motion families, AD-7) into scoped Vue components; establish the CSS module conventions.
- **Story 23.3 — Migrate baseline surfaces (dashboard, epics) to Vue/Nuxt over the IR**, proving parity with the golden output.
- **Story 23.5 — Packaging reconciliation** — Node build step in distribution (Epic 16 touchpoint); resolve the self-contained-binary vs. Node-toolchain story. **⚠️ RESEQUENCED AHEAD OF 23.4** by the Story 23.1 spike gate — see the note below.
- **Story 23.6 — Retire the C# HTML writer** — ⛔ **ADDED 2026-07-30** (owner decision **D7** at Story 23.4's dev-story session 4). The deletion of `HtmlRenderAdapter.Render`'s page composition and `WriteOutput`'s `.html` writes, **carved out of Story 23.4** so that story could close on the work it actually finished. Both original gates are **cleared as of create-story 2026-07-30**: the owner confirmed the verify-and-iterate pass over 23.4's 1,276 migrated pages is finished (D4), and the replacement content-drift gate satisfying [ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md) is decided — a new `check:parity` reading back the committed `web/measurements/parity.json` (D2). Status **`ready-for-dev`**, and the scope grew: `--spa` proved to be off by default, so the story now also makes the IR unconditional and drives the prerender from `generate` per ADR 0022 §Decision 3 (D1). See the story section below.
- **Story 23.7 — Empty-state hardening for the migrated surfaces** — ⛔ **ADDED 2026-08-08** (owner decision at the Story 16.1 second code review). `EpicsIndexSurface.vue` hard-throws when the epics index has no child pages, so a thin or non-BMad repository generates with `errors=1`. Originally routed to Story 23.3, which closed `done` without shipping it and orphaned the gate; it now has its own story so it cannot close silently. **Blocks Story 16.7.** See the story section below.
- **Story 23.4 — Migrate remaining surfaces + retire the C# `HtmlRenderAdapter` for content** (charts remain C#-SVG in the IR). ~~**Blocked until 23.5 lands.**~~ **UNBLOCKED 2026-07-27** — Story 23.5 is complete and [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) settles packaging: the Node toolchain is build/CI-time only, the shipped artefact is a project-independent 3.78 MB prebuilt `.output/` that renders any project's IR at server runtime, and the standalone binary takes a documented Node prerequisite. The 23.1 gate's binary ("client-rendered SPA *or* Node at run time") was **false** — see the [packaging strategy report](../implementation-artifacts/23-5-packaging-strategy-report.md).

<!-- 2026-07-23 (Story 23.1 spike gate, owner-confirmed in code review): EXECUTION ORDER IS 23.2 → 23.3 → 23.5 → 23.4.
     23.5 is promoted from an end-of-epic tidy-up to the epic's load-bearing unknown: prerendering is inherently
     PER-PROJECT (routes come from the user's own IR), so "build Nuxt at CI time and ship pre-built assets" does not
     cover rendering the user's project. Either the shipped artefact becomes a client-rendered SPA (forfeiting the
     NFR6 baseline 23.1 proved) or `specscribe generate` needs Node at run time (forfeiting the self-contained
     binary). 23.4 retires the C# renderer irreversibly and must not start before that is settled. 23.4 additionally
     owns the webview CSP change as a two-knob ATOMIC edit ('strict-dynamic' + payloadExtraction:false) and must
     PROPOSE AN ADR 0005 AMENDMENT — 'strict-dynamic' contradicts ADR 0005's "the body carries no scripts of its own".
     Full reasoning and measurements: _bmad-output/implementation-artifacts/23-1-spike-report.md -->


<!-- 2026-07-21: formal Story 23.1-23.5 sections added (bmad-create-epics-and-stories continuation run), same rationale as the Epic 22 note above. Epic stays backlog/unscheduled/spike-first, sequenced after Epic 22. -->

### Story 23.1: Spike — Nuxt-over-IR Feasibility

As a maintainer evaluating whether Vue + Nuxt 3 can replace the C# presentation layer,
I want a feasibility spike proving the riskiest technical assumptions before committing to migration,
So that Epic 23's implementation stories are scoped by evidence, mirroring Story 6.6/22.1's spike discipline.

**Acceptance Criteria:**

1.
**Given** ADR 0009's universal/SSR direction
**When** the spike builds a representative Nuxt prerender of one existing surface
**Then** it proves the NFR6 JS-optional baseline holds — the prerendered route is fully navigable without JavaScript.

2.
**Given** the spike migrates one representative surface to scoped-CSS Vue components, chart-SVG injection from the IR, and Markdig-derived prose
**When** the migrated surface is compared to the existing golden output
**Then** it verifies visual and functional parity for that surface.

3.
**Given** the VS Code webview's CSP constraints (Stories 6.5, 6.12)
**When** the spike evaluates hydration
**Then** it reports whether a hydration nonce survives the webview's CSP
**And** it reports the cost and impact of adding a Node build step to the generation pipeline against the self-contained-binary distribution model (ADR 0005/0006).

### Story 23.2: Component Library + Design-Token Bridge

As a maintainer preserving the antiquarian design system during the Vue/Nuxt migration,
I want the shared presentation tokens ported into scoped Vue components,
So that visual consistency — status/motion tokens, AD-7 — survives the framework change.

**Acceptance Criteria:**

1.
**Given** the existing `--status-*` and `--motion-*` token families
**When** they are ported to Vue
**Then** components consume the same token values, with no duplicated or drifted color/motion definitions.

2.
**Given** AD-7's presentation-token architecture
**When** the component library is established
**Then** CSS module/scoped-SFC conventions are documented for future component authors.

### Story 23.3: Migrate Baseline Surfaces (Dashboard, Epics) to Vue/Nuxt over the IR

<!-- ~~2026-08-07 (Story 16.1, ADR 0040 §Decision 11): THIS STORY NOW GATES STORY 16.7.~~
     ⛔ SUPERSEDED 2026-08-08 — THE GATE MOVED TO STORY 23.7. DO NOT re-route work here.

     What happened, recorded because the failure mode is instructive rather than embarrassing:
     Story 16.1 routed the `EpicsIndexSurface.vue` empty-epics hard-throw to this story on 2026-08-07,
     on the explicit reasoning that this story owned the surface and was at `review`, which in this
     project's lifecycle is an ITERATING state. That reasoning was sound when written. But this story
     then closed `done` at its own code review on 2026-08-08 WITHOUT shipping the fix, and that
     review's sprint-status note OVERWROTE the reciprocal seat 16.1 had placed on the 23-3 key — so
     the edge survived only on the 16-7 side, pointing at a closed story, with the defect unfixed.
     Nothing was watching for the state change. Caught by the Story 16.1 second code review.

     Owner decision (2026-08-08): the work gets its own story rather than reopening this one, so the
     gate cannot be silently closed again — closing Story 23.7 IS shipping the fix.
     See § Story 23.7 and § Story 16.7. This story keeps its `done` status and its own review record. -->

<!-- HISTORICAL, for the reader who arrives from a 2026-08-07 citation: the defect is that
     `EpicsIndexSurface.vue` HARD-THROWS when the epics index has no child pages, so a thin or
     non-BMad repository generates with `errors=1` and no `epics.html`. It is the same defect class
     this story DID fix on the sibling surface — `DashboardSurface.vue` handles its own empty case
     gracefully, in the same run — which is why the sibling was the obvious home for it. -->

As a maintainer validating the migration approach on real surfaces,
I want the dashboard and epics pages rendered via Vue/Nuxt from the canonical IR,
So that migration risk is proven on high-traffic surfaces before the remaining pages migrate.

**Acceptance Criteria:**

1.
**Given** the existing dashboard and epics golden output
**When** the Vue/Nuxt versions render from the IR
**Then** output achieves parity — byte-identical or documented-equivalent — with the pre-migration golden baseline.

2.
**Given** accessibility and reduced-motion conventions (Stories 1.4, 1.5, 3.5)
**When** the migrated surfaces render
**Then** those conventions are preserved without regression.

> **Scope drift recorded 2026-07-27 (create-story 23.3 → dev-story 23.3).** The story file's ACs **extend**
> the two above to eight. ACs 1–2 are these, sharpened with the harness that proves them; ACs 3–8 are the
> concrete scope the story was seeded with — the 23.1 spike gate's two additions (a **head projection**;
> **route-mapping the in-content link graph**) plus the six owner decisions locked at elicitation. The
> surface set is stated explicitly: **`index.html` + `epics.html` + `epics/epic-{N}.html` +
> `epics/story-{id}.html`** (189 pages), with every remaining page prerendered as a **pass-through** so the
> link graph is provable end to end — pass-throughs are Story 23.4's and are not a migration claim.
> Two ADRs came out of the implementation and both bind later stories:
> [**ADR 0017**](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md) — a projected page's route IS
> its IR `outputRelativePath` verbatim, and **no href inside IR content is ever rewritten** (this
> constrains Epic 22's path scheme, 23.4's surfaces and 23.5's packaging); and
> [**ADR 0018**](../../docs/adrs/0018-transitional-ir-content-style-layer.md) — `ir-content.css`, a
> generated, bounded, scoped, **enumerated** monolith extract for injected markup, whose manifest is
> literally the list **Story 23.4 has to retire**. Full detail in the story file's Dev Agent Record; the
> same drift is recorded on the `23-3` key in `sprint-status.yaml`.

### Story 23.4: Migrate Remaining Surfaces + Retire the C# HtmlRenderAdapter for Content

As a maintainer completing the presentation-layer migration,
I want all remaining surfaces migrated to Vue/Nuxt and the C# `HtmlRenderAdapter` retired for content rendering,
So that SpecScribe has a single renderer and no drift hazard between two templating systems.

**Acceptance Criteria:**

1.
**Given** Stories 23.2–23.3 established the pattern
**When** all remaining surfaces (requirements, ADRs, code pages, insight pages, and similar) are migrated
**Then** each achieves parity with its pre-migration golden baseline.

2.
**Given** migration is complete
**When** the C# `HtmlRenderAdapter` is retired for content rendering
**Then** charts render through the Epic 20 Hierarchy Explorer component from IR chart data, and the server-rendered text twin continues to be emitted per [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md).

<!-- 2026-07-24 (correct-course, SCP 2026-07-24): AC #2 previously read "chart SVG generation remains in C#, per
     ADR 0009's non-goal, and continues to be embedded in the IR." ADR 0013 retires server-rendered chart SVG, so
     that non-goal no longer exists — this is a SCOPE REDUCTION for 23.4: there is no C# chart-SVG generator left to
     carve out and preserve when the HtmlRenderAdapter is retired. The ADR 0005 CSP amendment 23.4 owes is now
     SHARED with ADR 0012's webview amendment and must be landed ONCE, not twice. -->

> **Scope drift recorded 2026-07-27 (create-story 23.4); revisited 2026-07-28.** The story file's ACs
> **extend** the two above to eight. It was seeded **`blocked`** on Story 23.5 and is now **`ready-for-dev`**:
> the packaging gate cleared when 23.5 landed [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md).
> ⚠️ **One gate replaced it — [Story 22.4](#story-224-spa--webview-as-ir-consumers) runs BEFORE 23.4**
> (owner D2), so 23.4 inherits **one** region producer rather than two, and the retired
> [Story 22.3 file](../implementation-artifacts/22-3-static-html-rendered-from-the-ir.md) is kept as the spec
> for 23.4's region-composition task. ACs 1–2 are these; ACs 3–8 carry the four owner decisions locked at
> elicitation plus the 23.1 spike gate's assignment of the ADR 0005 CSP amendment — which **remains 23.4's**
> and must land once, since ADR 0022 deliberately does not touch CSP.
> Full detail in [`23-4-…md`](../implementation-artifacts/23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md)
> and on the `23-4` key in `sprint-status.yaml`. The four decisions:
>
> 1. **D1 — seeded `blocked`.** The gate is 23.5, and specifically its Q2 (what the standalone binary does
>    when Node is absent), because 23.4 deletes the C# HTML writer that currently answers it.
> 2. **D2 — "retire the `HtmlRenderAdapter`" means C# stops WRITING `.html`.** It **keeps** a
>    region-composition path (nav + wayfinding + `<main>`) that feeds the IR and the webview/SPA. The
>    full-page assembly dies; the ~7,000 LOC of `*Templater.cs` that produce page bodies do **not**.
> 3. **D3 — full componentization of the remaining 857 pass-through pages**, retiring `ir-content.css` to
>    empty per [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) — or an enumerated
>    residue with a named blocker per rule.
> 4. **D4 — [Story 22.3](#story-223-static-html-rendered-from-the-ir) is RETIRED**; 23.4 is the answer to
>    "who renders static HTML from the IR." Recorded on 22.3 above in the same change.
>
> **The load-bearing finding:** for **857 of 1,046 pages the IR is produced by the very code this story
> retires** — `SpaDelivery.ExtractContentRegion` slices the region back out of the C# renderer's own
> full-page string, captured at `SiteGenerator.WriteOutput`. The region path must therefore be stood up and
> proven **byte-equal** *before* any deletion. Two further inversions the story pins: `GoldenContentFingerprint`
> must **move or be retired** here (23.3 asserted it stationary), and the owed **ADR 0005 CSP amendment is
> probably documentation-only** now that 23.3's `noScripts: true` removed the hydration 23.1's
> `'strict-dynamic'` finding was about.

<!-- ── DEV-STORY OUTCOME 2026-07-29 (structural changes recorded in BOTH artifacts in the same change, per
     CLAUDE.md § Decision records; the `23-4` key in sprint-status.yaml carries the same summary) ──────────── -->

> **↻ Dev-story outcome, 2026-07-29. Three of the seeded premises did not survive measurement, and two owner
> decisions are amended as a result. Recorded here because each changes a shared contract, not just this story.**
>
> **AC drift.** The epic states two ACs for this story; the story file carries **eight** (ACs 3–8 are the four
> owner decisions, the 23.1 gate's CSP assignment, and ADR 0018's retirement condition). Same shape as 23.3.
>
> 1. **The inventory was wrong by 40 %, in both directions over time.** Seeded as "857 pass-through of 1,046".
>    ⚠️ **Two counts, from two different runs — this item used to attribute the second to the first.** [Story 23.4
>    code review, finding F-14] The Task 1 `--deep-git --spa` generate measured **1,408 IR pages / 1,409 `.html`**
>    (session 3 part 1). The **1,469** figure is the later Task 5 parity corpus (session 3 part 2); it is larger
>    because the story's own artifacts are themselves rendered pages, so the corpus grew mid-story. Both were
>    correct when taken. Of the 1,469, 193 were already migrated and **1,276** were not. A default generate omits
>    `git-insights.html`, `deep-analytics.html`, `impact-map.html` and all 300 `commit/` pages, so any count taken
>    without `--deep-git` understates the story by ~300 pages.
> 2. **D3/D5 is AMENDED: `ir-content.css` is NOT retired, and its "when it is empty" condition is unreachable
>    as written.** Only **6.5 %** of the layer's rules are prose and authorable today; **93.5 %** style bespoke
>    vocabulary *injected as rendered HTML* across **651 classes**. Retiring those means either ADR 0018's
>    explicitly rejected hand-copy, a full visual redesign, or **structured per-family data in the IR — an Epic
>    22 ask**, raised rather than improvised (the escalation the story's own Dev Notes prescribe). One bucket,
>    the 97 `chrome` rules, **never empties**: owner decision D2 and ADR 0024 keep C# composing nav + wayfinding
>    + `<main>` permanently. AC #4's **second branch** is taken: residue enumerated per rule with a named
>    blocker (`npm run report:ir-content-residue`, committed), **[ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md)
>    amended with an addendum**, and **1,420** is the owner-visible debt figure.
>    ⚠️ A separate, *worse* defect was found and fixed in the same area: the extractor was still bounded to
>    Story 23.3's four families, so the 1,276 newly-migrated pages had only **42 %** of their classes styled and
>    the rest rendered **bare**. Widening it to the whole site took coverage to **100 %** while still dropping
>    393 of 1,814 source rules as unused — so the layer is still bounded, still scoped, still gated.
> 3. **`GoldenContentFingerprint` is NOT retired and did NOT move — deliberately, and AC #5 is satisfied by its
>    successor instead.** The C# page writer still ships, so the hash still covers something real and stayed
>    **stationary** all session (the correct assertion while templaters move). What AC #5 actually asked for —
>    "a fingerprint over the IR … does not exist yet" — now exists: **`GoldenIrFingerprint`**, hashing the
>    `spa/` manifest and chunks, landed in the *same* story that switched the IR's producer, so the drift gate
>    never lapses. Its capture caught real nondeterminism via the two-run rule (the manifest's derived
>    `contentHash` for `diagnostics.html` embeds the output path); stable across **three** runs after folding it.
> 4. **AC #3 LANDED; the DELETION did not, and that is a sequencing decision, not drift.** The IR is now built
>    from a region **composed** from each page's own `PageView` at the write seam — proven byte-equal to the old
>    `ExtractContentRegion` slice across **1,469 pages with 0 unexpected deltas** — and `SpaDelivery.Extract*`
>    survives only as that proof's oracle. But `HtmlRenderAdapter.Render` and the `.html` writes **remain**,
>    because deleting them destroys the live golden side that the owner's verify-and-iterate pass needs in order
>    to re-measure any change they ask for. Deletion should follow owner verification, not precede it.
> 5. **Owner decision D6 discharged and re-homed.** `DashboardSurface.vue`'s hard-throw on a chart-less
>    dashboard — the one route that failed Story 23.5's two-IR experiment (CORA **32/33**) — is fixed here and
>    Story 23.5's open-items row 1 moves from Story 23.3 to this story.
> 6. **The CSP amendment landed as [ADR 0032](../../docs/adrs/0032-csp-posture-after-the-projection-layer.md),
>    once**, discharging ADR 0012 §Decision 5. Measured verdict: **no relaxation of the policy string** — 23.3's
>    `noScripts: true` removed 23.1's hydration premise (0 Nuxt scripts, 0 `_payload.json` across 1,469 routes)
>    and the webview is not a Nuxt consumer anyway. It restates ADR 0005 §4's "the body carries no scripts of
>    its own" as an enforced claim about the **region** (0 executable, 163 inert data islands).
> 7. **⛔ THE DELETION IS RE-HOMED — owner decision D7, 2026-07-30 (dev-story session 4). Story 23.4 closes at
>    `review` WITHOUT it.** Item 4 above deferred "C# stops writing `.html`" pending owner verification; the owner
>    has now **descoped** it into **[Story 23.6](#story-236-retire-the-c-html-writer)**. Two findings drove it, both
>    measured in session 4:
>    - **AC #5's answer changed under the story after it was written.** The successor gate item 3 records —
>      **`GoldenIrFingerprint`** — was **removed** on 2026-07-30 (`70b72ab`): three different hashes across the
>      local box, CI-Windows and CI-Ubuntu for one identical commit. `GoldenContentFingerprint` is unaffected but
>      hashes **output `.html`**, so the deletion voids it too ⇒ **no content-drift gate on either side**. AC #5 is
>      satisfied in the record and **not** in the tree; 23.6 inherits the hole.
>    - **The blast radius is a story's worth of work.** The written document is the oracle for **four** gates: it
>      backs `_spaCapture`, without which `RegionCompositionDeltas()` — and therefore **both** proof gates 23.4
>      landed — goes **vacuous rather than red**; plus `GoldenContentFingerprint`, `GoldenOutputInventory`, and
>      `EnsureHierarchyEngine`'s host-marker scan. The good news traced in the same pass: `WritePage` composes the
>      region from the `PageView` *independently* of the page render, so the region path itself needs nothing.
>    - **The gate constraint is now a ratified-pending contract, not a story note:**
>      **[ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md)** (Proposed) — content-drift
>      gates must be targeted and regenerable; a whole-tree hash is not an acceptable shape for a new gate.
>
> 8. **Epic 22's `22-5`/`22-6` premises: intact, and now better supported.** Both stories have already landed
>    (`done` / `review`), and neither depended on C# writing `.html` — 22.5's incremental engine keys on the
>    classifier and 22.6's delta channel on the IR. Switching the IR's producer to a composed region *helps*
>    both: page title, breadcrumb and meta description now come from the view model instead of being regex-scraped
>    back out of finished HTML, so an incremental recompute no longer depends on re-parsing its own output. What
>    Epic 22 newly **owes** is item 2's view-model ask.

### Story 23.6: Retire the C# HTML Writer

> **⛔ Seeded 2026-07-30 by owner decision D7 at Story 23.4's dev-story session 4 — the deletion carved out of
> [Story 23.4](#story-234-migrate-remaining-surfaces--retire-the-c-htmlrenderadapter-for-content) rather than left
> as an open checkbox on a story that is otherwise complete.**
>
> **✅ Through `create-story` 2026-07-30 (baseline `5a78ee7`). Status `ready-for-dev`.** The
> [story file](../implementation-artifacts/23-6-retire-the-c-sharp-html-writer.md) carries **9 ACs**: the five
> below, sharpened, plus four added by the owner decisions elicited at create-story. The blocker below **is
> settled** — do not re-litigate it during dev.
>
> **Four owner decisions locked at elicitation:**
>
> - **D1 — the IR becomes UNCONDITIONAL, and `generate` shells out to Node to emit the `.html`.** Forced by a
>   finding the seeding note did not have: `--spa` is still **off by default** (`SiteSettings.cs:36`;
>   `_spaCapture`/`_spaPageViews` are allocated only when `EmitSpa || CapturePages`), so deleting the writer today
>   would leave a plain `specscribe generate` emitting nothing but `specscribe.css`/`.js`. This is
>   [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) §Decision 3 —
>   *"SpecScribe drives the prerender … one request per route from the manifest it just emitted"* — executed for
>   the first time. **→ new ACs #6 and #7.**
> - **D2 — the replacement content-drift gate is a new `check:parity` that READS BACK the committed
>   `web/measurements/parity.json`.** No C#-side digest gate. `measure:parity` only ever *wrote* that oracle;
>   nothing reads it, which is why the committed 1,469 rows are evidence and not yet a gate. **→ AC #3.**
> - **D3 — `RegionCompositionParityTests` and `RegionCompositionCorpusProof` are RETIRED**, reason recorded
>   in-file, together with the `SpaDelivery.Extract*` scrapers they are the last consumer of. **→ AC #2.**
> - **D4 — Story 23.4's verify-and-iterate pass over the 1,276 migrated pages is FINISHED**, which satisfies
>   AC #5's ordering gate and is what makes the deletion safe to start. **→ AC #5 marked satisfied.**
>
> **Four findings traced in the tree at `5a78ee7` that this section did not have:**
>
> 1. **`measure:parity` itself goes vacuous.** `goldenRoot = ir.IR_DIR` = `SpecScribeOutput/` — the directory C#
>    writes the `.html` into. With no golden, every row takes the `NO GOLDEN` branch, `measured` is empty,
>    `migrationDeltas` is empty, and the script exits **0**. The harness that produced this story's oracle would
>    report success while measuring nothing. `check:links` has the same one-sidedness.
> 2. **The four-gate table below is really SIX.** `CapturedRegions`' silent-gap guard
>    (`SiteGenerator.cs:3634`) and `RenderWebviewSurfaces`' long-tail gate (`:3720`) are both
>    `_spaCapture is not null` conditions that go **vacuous**. `:3720` is the dangerous one: it gates the *entire*
>    doc/ADR/requirement/sprint/retro webview surface set even though the body inside consumes the **composed**
>    producer, so a naive deletion silently shrinks the webview with no test failing.
> 3. **AC #1 names two write paths; there are FIVE.** `WriteOutput`, plus raw `File.WriteAllText` at `:3234`,
>    `:3249`, `:3261`, `:3268`, plus `WriteTextWithRetry` at `:4341` — the dashboard/epics families never joined
>    the `WritePage` seam. AC #1 is amended accordingly.
> 4. **The test blast radius is the largest single piece of the story:** ~261 `Path.Combine(Site, …)` reads
>    across 35 test files and ~300 templater `RenderX` call sites across 22. All mechanically substitutable to the
>    region, because Story 23.4 already split every templater into `BuildX` → `PageView`. **→ new AC #8**, which
>    forbids dropping an assertion merely because it stopped compiling.
>
> **Confirmed good, and worth stating:** `CapturedRegions` (`:3625`) iterates `_spaPageViews` only and reads
> nothing from the rendered document. The circularity that shaped all of 23.4 is genuinely broken in the tree.
>
> **Also owed (new AC #9):** a new ADR recording the output-contract inversion — the IR is the unconditional
> product and the static site is rendered from it by Node, with `--spa` retired — plus proposals to move
> **ADR 0022** and **ADR 0033** from `Proposed` to `Accepted`, this story being the first execution of the one and
> the first implementation of the other.

As a maintainer finishing owner decision D2,
I want `HtmlRenderAdapter.Render`'s full-page composition and `WriteOutput`'s `.html` writes deleted, with every
gate that depended on the written document re-pointed or retired with a stated reason,
So that Nuxt is the single writer of SpecScribe's HTML and no C# code path emits a content page.

**What 23.4 already did, so this story does not redo it:** all 25 templaters are on `PageView`; the IR is built
from a region **composed** from each page's own view model at the `WritePage` seam, proven byte-equal to the old
`ExtractContentRegion` slice across **1,469 pages with 0 unexpected deltas**; all 1,276 remaining pages are
migrated to 10 Vue family components with **0 pass-through**; the parity oracle is **captured and committed** as
per-page sha256 in `web/measurements/parity.json`. **The deletion is what remains, and only the deletion.**

**⚠️ The blocker to settle at create-story, not during dev.** 23.4's AC #5 successor gate `GoldenIrFingerprint`
was **removed** 2026-07-30 (`70b72ab`) for cross-platform nondeterminism, so **there is no content-drift gate over
the IR today**, and `GoldenContentFingerprint` — which covers the rendered `.html` — is voided by this story's own
deletion. Decide the replacement **before** deleting. It must satisfy
**[ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md)**: targeted, regenerable by
command, deterministic across CI operating systems, and loud rather than vacuous when its oracle vanishes. The
committed per-page `parity.json` oracle is the reference shape; wiring `measure:parity` into CI is this story's to
scope. `deferred-work.md:22` carries the same action.

**⚠️ The blast radius, traced in 23.4 session 4 — four gates hang off the written document, not one.**

| dies with the writer | what this story owes it |
| --- | --- |
| `_spaCapture` (the slice oracle) | `RegionCompositionDeltas()` loses its comparison basis ⇒ `RegionCompositionParityTests` **and** `RegionCompositionCorpusProof` go **vacuous, not red** — the worst failure mode. Re-point at the committed sha256 oracle or retire with a stated reason. |
| `GoldenContentFingerprint` | subject gone. This is AC #5's long-promised inversion; retire it, do not re-point it at the IR as another whole-tree hash (ADR 0033). |
| `GoldenOutputInventory` | pins the output file set; changes wholesale. Same treatment. |
| `EnsureHierarchyEngine`'s host-marker scan | reads `WritePage`'s returned document; re-derive from the view model. |

**The good news, also traced:** `WritePage` ([SiteGenerator.cs:3970](../../src/SpecScribe/SiteGenerator.cs:3970))
renders the document via `HtmlRenderAdapter.Shared.Render(page)` and composes the region **separately** from the
same `PageView`. The region path therefore needs nothing from the page render — the circularity that shaped all of
23.4 is already broken.

**Acceptance Criteria:**

1.
**Given** owner decision D2 — C# stops WRITING `.html` while still composing regions for the IR
**When** the retirement lands
**Then** `HtmlRenderAdapter.Render`'s full-page composition and `WriteOutput`'s content-HTML writes are gone and no
C# code path writes a content `.html`, **while** `RenderNavMarkup`, `RenderBreadcrumb`, `RenderWayfinding`,
`RenderDashboardBody` and `RenderEpicsBody` survive and continue to feed the region — and the webview and SPA keep
working through that same region path per [ADR 0024](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md).

2.
**Given** four gates depend on the written document
**When** the writer is deleted
**Then** each is **re-pointed or retired with a stated reason** — never left asserting against a vanished oracle. A
gate that silently passes because its basis is empty is a failure of this AC, not a pass; the enumeration above is
the checklist.

3.
**Given** [ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md)
**When** the replacement content-drift gate lands
**Then** it is **targeted** (a failure names the artifact), **regenerable by command** rather than constant-bump,
**proven deterministic across the CI operating systems** and not merely across two local runs, and **fails loudly
when its oracle is absent**. It lands **before or with** the deletion, so the drift gate never lapses.

4.
**Given** [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) makes Node a
generate-time runtime
**When** C# can no longer produce HTML at all
**Then** the documented Node prerequisite is verified to actually fire — a user without Node gets the actionable
startup error, not a silent empty output — and the consequence 23.5 stated plainly (**a user without Node cannot
generate at all**) is re-confirmed as still the accepted trade-off. Node detection itself remains Story 16.3's.

5.
**Given** the owner's verify-and-iterate pass is the design gate (CLAUDE.md § Story lifecycle)
**When** this story starts
**Then** it confirms the owner has finished verifying Story 23.4's 1,276 migrated pages first — because after the
deletion there is **no golden side left to generate**, and re-measuring anything they ask for stops being possible.
This is the reason 23.4 deferred the deletion twice; it is an ordering constraint, not a courtesy.

### Story 23.5: Packaging Reconciliation

As a maintainer responsible for SpecScribe's distribution,
I want the Node build step required by Nuxt reconciled with the existing self-contained-binary distribution model,
So that Epic 16's packaging/release story isn't broken by the presentation-layer migration.

**Acceptance Criteria:**

1.
**Given** ADR 0005/0006's self-contained-binary distribution and Epic 16's release pipeline
**When** the Node/Nuxt build step is introduced
**Then** a documented packaging strategy resolves how and when the Node toolchain runs — build-time only vs. runtime dependency.

2.
**Given** the npx channel (Story 16.8) and VS Code Marketplace packaging (Story 16.5/FR33)
**When** packaging is reconciled
**Then** both existing distribution channels continue to function without new runtime dependencies for end users.

<!-- 2026-07-27 (dev-story 23.5, baseline 86b35c2): STORY COMPLETE. The two ACs above are the epic's originals;
     the story file carries ACs 3-9, which are the concrete scope it was seeded with.

     DECISION: [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) — Proposed.
     Node is a build-time toolchain and a generate-time runtime, never a shipped toolchain. Full measured basis in
     the [packaging strategy report](../implementation-artifacts/23-5-packaging-strategy-report.md).

     THE 23.1 GATE'S BINARY WAS FALSE. It conflated the toolchain that BUILDS the projection layer with the runtime
     that RENDERS with it. Measured: build toolchain 201.9 MB with 14 native `.node` bindings requiring dlopen;
     shipped artefact 3.78 MB of PURE JS with ZERO native bindings. The two-IR experiment nobody had run — one
     artefact, built with NO IR present, isolated to a directory holding only `.output/` — rendered 1,056/1,056
     routes of this repo and 32/33 of a DIFFERENT project via SPECSCRIBE_IR_DIR, at ~4 ms/route (full pass 6.2 s vs
     `nuxt generate` 25-30 s vs the spike's ~130 s cold). Net new shipped bytes ~2.40 MB.

     ⚠️ SAY IT THE WAY THE EVIDENCE DOES [code review 2026-08-08, owner decision D1]: the HYPOTHESIS holds, and the
     HARNESS's own verdict is REFUTED — it applies a strict binary, so CORA's one HTTP 500 (`DashboardSurface.vue`
     throwing on a dashboard with no Hierarchy Explorer, raised to 23.3) refutes it. "CONFIRMED with one documented
     exception" is the accurate phrasing; the bare "CONFIRMED" this note used to carry contradicted the committed
     `two-ir.json`. Timings above are transcribed from that file (an earlier revision published a different run's).

     AC #2 CORRECTION: it names two channels and ADR 0012's formula has three; npx and the extension host BOTH run
     on Node by construction, so the standalone binary (16.3) is the only channel a Node dependency breaks. It takes
     a documented Node prerequisite (owner decision). And no Epic 16 channel is built yet — every `16-*` key is
     backlog — so "continue to function" is a design constraint on unbuilt channels, not a non-regression check.

     ALSO LANDED HERE (owner decisions 2026-07-27): Nuxt 3.21.9 -> 4.5.1 absorbed (EOL 2026-07-31), holding 23.3's
     contract exactly at 190/190 byte-identical `<main>`, 190/190 verbatim, 0 link regressions; `web/` wired into
     build-test-analyze.yml with setup-node + npm ci + the three drift gates + Vitest (80 tests, lcov to Sonar);
     asset URLs rewritten page-relative so the portal loads from `file://` (verified in a live browser at depth 0
     and depth 3) — `app.baseURL: './'` was evaluated and REJECTED because the correct prefix is per-page-depth.

     TWO FINDINGS RECORDED RATHER THAN SMOOTHED: (1) an artefact carrying prerendered pages returned PROJECT A'S
     DASHBOARD FOR PROJECT B with HTTP 200, because Nitro serves `public/` ahead of the SSR route -- fixed
     structurally by SPECSCRIBE_PACKAGE_BUILD=1 emptying the route table; (2) `DashboardSurface.vue` hard-throws on
     any project whose dashboard has no Hierarchy Explorer -- a real project-independence defect, RAISED to Story
     23.3 rather than patched here, per this story's scope instruction.

     AMENDS ADR 0006 §Decision: its "self-contained packaging ... stand[s]" clause no longer stands unqualified.
     The binary is still self-contained w.r.t. .NET, but acquires an external Node runtime prerequisite. ADR 0006's
     NFR6 ruling is UPHELD IN FULL -- nothing client-renders. -->

### Story 23.7: Empty-State Hardening for the Migrated Surfaces

<!-- ⛔ ADDED 2026-08-08 (owner decision at the Story 16.1 second code review). STRUCTURAL: a new story plus a
     new cross-epic blocking edge, recorded HERE and in sprint-status.yaml in the same change per
     CLAUDE.md § Decision records.

     WHY IT EXISTS. Story 16.1's packaging probe reproduced, twice, that `EpicsIndexSurface.vue` HARD-THROWS
     when the epics index has no child pages, so a thin or non-BMad repository generates with `errors=1` and
     no `epics.html`. 16.1 ships no product code, so it routed the fix to Story 23.3 — which owned the
     surface and was then at `review`, an iterating state in this project's lifecycle.

     WHY IT MOVED HERE. Story 23.3 closed `done` at its own code review on 2026-08-08 WITHOUT shipping the
     fix, and that review's sprint-status note overwrote the reciprocal seat 16.1 had placed on the 23-3 key.
     The routing argument ("`review` is an iterating state") expired the moment 23.3 left `review`, and
     nothing was watching for it. The Story 16.1 second code review caught the orphan; the owner's call was
     to give the work its own story rather than reopen a closed one or bury a Vue fix inside a
     launch-readiness story. **This is the story that keeps the edge honest: it cannot silently close, because
     closing it is the whole of its scope.**

     THE CORRECT BEHAVIOUR IS ALREADY MODELLED ONE COMPONENT OVER. `DashboardSurface.vue` handles its own
     empty case gracefully, fixed in the same run by Story 23.3 after Story 23.5 raised it the same way (see
     that story's "TWO FINDINGS RECORDED RATHER THAN SMOOTHED", finding 2, immediately above). This story is
     the sibling fix that did not land, plus a sweep for the same defect class across the other migrated
     surfaces so the pattern is closed rather than patched twice.

     Reciprocal seat recorded at § Story 16.7 and on both the `23-7` and `16-7` keys in sprint-status.yaml. -->

**Depends on:** nothing. **Blocks:** Story 16.7 (launch readiness cannot certify "a working install" while a
thin repository fails to generate).

As a maintainer publishing a preview that strangers will run on their own repositories,
I want every migrated surface to render an honest empty state instead of throwing,
So that a thin or non-BMad repository produces a working site rather than `errors=1`.

**Acceptance Criteria:**

1.
**Given** a repository whose epics index has no child pages
**When** `specscribe generate` runs
**Then** it completes with `errors=0` and produces an `epics.html` carrying an explicit empty state
**And** the failure Story 16.1 reproduced twice is covered by a regression test that is proven red before it is proven green.

2.
**Given** `DashboardSurface.vue` already handles its own empty case gracefully
**When** this story fixes `EpicsIndexSurface.vue`
**Then** the two surfaces present their empty states consistently — the same component, copy register and visual treatment, not two independent solutions.

3.
**Given** this defect class has now surfaced twice on two different surfaces (Story 23.5 → dashboard, Story 16.1 → epics index)
**When** this story closes
**Then** every other migrated surface has been audited for the same "hard-throw on empty collection" pattern, and the audit's result is recorded — including the surfaces found already safe, so a future reader knows the sweep was exhaustive rather than incidental.

4.
**Given** the thin-repository case is the first impression an external adopter gets
**When** the fix lands
**Then** it is verified against a real repository with no epics — not a fixture alone — per CLAUDE.md § Verification, and the drift gates are unmoved.

## Epic 24: File Relationship & Change-Coupling Insights — Directional Metric + Multi-Form Coupling Graphs

Turn "what changes alongside this file" from a flat co-change list into a rigorous, richly visual relationship surface. Two threads: (1) upgrade the coupling **metric** from raw symmetric co-change counts to **directional coupling strength** — confidence(A→B) = shared-changes / A's-changes, plus a minimum-support floor and lift — with **cross-boundary "surprising coupling"** emphasis (a file coupled to one in a distant directory is an architectural smell) layered on the existing Code-vs-Process classifier; and (2) render those relationships as **polished, interactive graphs in three complementary forms** (force-directed network, chord/arc diagram, adjacency-matrix heatmap) at **two scopes** — a per-file **ego graph** on code pages and a **whole-repo coupling explorer** page. The upgraded ranked list survives as the accessible text-twin every graph shares. All metrics are computed at generation time from the single `--deep-git` numstat parse SpecScribe already runs (`GitMetrics` / `DeepGitPulse.CoChangePairs` + `FileInsight.ChangeCount`) — **no new git calls**. Every interactive graph is a progressive enhancement that degrades to static SVG + a readable table when JavaScript is unavailable (NFR8), and every chart carries a Story 10.2-compliant legend, analysis window, and framing sentence.

**FRs covered:** FR40 (sync into PRD when convenient) · **UX-DRs:** UX-DR19 (rich hover/focus + non-color text equivalent of every metric), UX-DR20 (purposeful visual polish within perf/a11y limits), UX-DR21 (one primary representation per dataset, alternates behind a toggle; chart text-twins are contract) · **NFRs:** NFR8 · **Status:** backlog · unscheduled · **Source:** market research 2026-07-22 (git-activity file-level insights). · **Depends on:** the `--deep-git` numstat parse + `CoChangePairs` (Stories 3.2/3.8 — already built) as the data source; Stories 7.4/7.8 (per-file coupled-files list + static `Charts.ReferenceGraph`) as the surfaces it upgrades and evolves; **Epic 20's client-JS interactivity boundary** and, as revised 2026-07-24, **[ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) + [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)** as the foundation the interactive graphs (24.2–24.5) build on. Does not block Epic 20.

<!-- 2026-07-24 (correct-course, SCP 2026-07-24): TWO revisions to this epic's foundations, no AC rewrites needed
     beyond the two noted at 24.2 and 24.5.
     (1) ENGINE. ADR 0010's "zero-dependency JS posture" no longer describes the project: ADR 0012 adopts Plotly for
     the HIERARCHY family. Plotly has NO force-directed layout and NO chord/ribbon trace, so it cannot serve 24.2,
     24.3, or 24.4. ADR 0012 §4 therefore permits TWO engine families and names Epic 24's graph engine an EXPLICIT
     OPEN QUESTION for this epic's own spike — it may be Plotly `scatter` with a hand-rolled layout, a second
     library, or bespoke. Decide it on evidence; a THIRD family requires an ADR. Note ADR 0012 records ECharts as
     "considered and deferred, not dismissed" (it covers hierarchy AND force-directed AND chord in one dependency) —
     if this epic's spike selects it, SUPERSEDING ADR 0012 is the expected outcome, not a failure.
     (2) GATE. Story 20.4's "must land before 24.2" sequencing constraint SURVIVES but its content changed: 20.4 is
     now the Plotly engine-adoption spike, and the consolidation it was seated to perform is fulfilled by Stories
     20.5/20.7 (one component, then delete the three arc renderers). 24.2 must not start before 20.7 lands, or Epic
     24 adds renderers to a file whose existing renderers are about to be deleted.
     (3) 24.5's adjacency matrix may ride Plotly's `heatmap` trace — the one Epic 24 view the hierarchy engine
     already covers (ADR 0012 §4). -->

<!-- 2026-07-24 (create-story 24.2 → halted and re-seated): the "this epic's own spike" that ADR 0012 §4 hands the
     graph-engine decision to DID NOT EXIST — Stories 24.1–24.5 are all implementation stories, so the named open
     question had no owner and would have been answered implicitly inside Story 24.2's dev pass (the exact failure
     mode the Epic 10 retro's ADR-creation-trigger action item exists to prevent). **Story 24.6 is that spike**,
     added below with owner approval.

     EXECUTION ORDER ≠ NUMERIC ORDER: 24.1 → **24.6** → 24.2 → 24.3 → 24.4 / 24.5. A renumber was rejected because
     ADR 0012 §4 names Stories "24.2, 24.3, and 24.4" verbatim, as do `sprint-status.yaml` and project memory;
     Epic 23's documented non-numeric order (23.2→23.3→23.5→23.4) is the house precedent for this.

     CONSEQUENCE FOR 24.2: it now carries THREE gates, not two — Story 24.1 (the metric spine it renders),
     Story 20.7 (don't add renderers to a file whose existing ones are about to be deleted), and Story 24.6 (the
     engine). Its sprint-status key is `blocked` until all three clear. 24.5 is NOT gated by 24.6: ADR 0012 §4
     already frees it to ride Plotly's `heatmap` trace. -->

 Story 7.11 (Code Ownership & Bus-Factor) already shipped the "who changes this file" half — this epic is the "what changes alongside it" half, deliberately not re-doing ownership.

### Story 24.1: Directional Coupling Metric Foundation (Confidence, Support, Lift, Cross-Boundary) + Upgraded List

As a maintainer inspecting a file's relationships,
I want the "changes with" data expressed as directional coupling strength rather than a raw shared-commit count,
So that I can read "when I touch this file, I usually touch X" instead of an unnormalized, symmetric tally that makes always-churning files look coupled to everything.

**Acceptance Criteria:**

1.
**Given** the existing deep-git parse (`DeepGitPulse.CoChangePairs` + per-file `ChangeCount`)
**When** coupling is computed
**Then** each directed pair carries **confidence(A→B) = coChange(A,B) / ChangeCount[A]** (asymmetric — A→B and B→A may differ), **support** = shared-commit count with a configurable minimum-support floor that filters coincidental couples, and **lift** = confidence(A→B) ÷ (ChangeCount[B] ÷ analyzed-commits) so a file that changes every commit self-demotes
**And** all three are derived from the SAME single `--deep-git` numstat parse with no additional git invocation, and the existing Code-vs-Process (`ClassifyCoupling`) noise classification is preserved.

2.
**Given** a coupled pair whose two files live in different top-level directories/modules
**When** coupling is surfaced
**Then** the pair is flagged as **cross-boundary ("surprising") coupling** (higher architectural signal), distinct from same-directory coupling, using only the file paths already in hand
**And** this classification is available to every downstream surface (list and graphs) as a shared property, not recomputed per view.

3.
**Given** the per-file "Coupled files" list (Story 7.4 `FileInsight.CoupledFiles`) and the Git Insights hub coupling view (Story 3.8)
**When** they render with the new metric
**Then** each entry shows the directional confidence (e.g. "changes with **X** — 80%") and a one-sentence framing per Story 10.2, sorted by confidence (or lift) with the support floor applied, and cross-boundary couples visibly marked
**And** the list remains fully readable and navigable without JavaScript — it is the canonical accessible text-twin the graph stories (24.2–24.5) reuse rather than replace.

### Story 24.2: Per-File Ego Coupling Graph (Force-Directed) on Code Pages

As a developer opening one file's page,
I want an interactive node-link graph of that file and the files it changes with,
So that the relationship reads as a picture I can explore — not a flat list — answering "what changes alongside THIS file" at a glance.

**Acceptance Criteria:**

1.
**Given** a code page for a file with coupling data and JavaScript available
**When** the ego graph renders
**Then** the focal file sits at the center with its coupled neighbors (bounded to a sensible degree, 1–2 hops) as a force-directed node-link graph, nodes sized by change frequency and edges weighted/colored by the Story 24.1 confidence (cross-boundary edges emphasized), with rich hover/focus tooltips (UX-DR19) and nodes linking to their own code pages
**And** the graph reuses the Story 24.1 metric and routes through a component honoring the same mode / legend / text-twin contract as the Epic 20 Hierarchy Explorer — using **this epic's chosen graph engine** (ADR 0012 §4 names it an open question for Epic 24's own spike, since Plotly carries no force-directed layout), not a per-story reinvention.

<!-- 2026-07-24 (SCP 2026-07-24): this AC previously read "rather than introducing a new engine or dependency
     (ADR 0010)" — ADR 0012 supersedes that posture. The invariant is now the CONTRACT (one component, one mode
     grammar, mandatory text twin), not the absence of a dependency. Blocked until Story 20.7 lands. -->


2.
**Given** a JavaScript-off, reduced-motion, or assistive-technology visitor (NFR8)
**When** the ego graph cannot hydrate
**Then** it degrades to a static SVG rendering (evolving the Story 7.8 `Charts.ReferenceGraph`) plus the Story 24.1 ranked list as the text equivalent, with every node/edge metric available as non-color text
**And** a file with no qualifying couples shows a designed empty state, never a broken or misleading empty graph.

### Story 24.3: Whole-Repo Coupling Explorer (Force-Directed Galaxy) — Dedicated Page

As a tech lead assessing architectural entanglement,
I want a dedicated page showing the whole repository's co-change network,
So that I can see the project's hidden coupling structure and its worst cross-boundary offenders in one explorable map.

**Acceptance Criteria:**

1.
**Given** deep-git coupling data and JavaScript available
**When** the whole-repo explorer renders
**Then** it draws the repo's file co-change network (node = file sized by change frequency, edge = coupling weighted/colored by confidence, cross-boundary couples emphasized), with interactive pan/zoom, hover/focus detail, node → code-page navigation, and clutter controls (minimum support/confidence threshold and directory grouping/collapse) informed by the code-map at-scale lessons
**And** it carries a Story 10.2 legend + analysis window + framing sentence, and is reachable from the insight-pages nav (FR27) on the shared deep-git gate.

2.
**Given** a large repository or a JavaScript-off visitor (NFR8, performance)
**When** the full network would be too dense or cannot hydrate
**Then** the view stays legible via the threshold/grouping controls and degrades to a static, bounded SVG summary plus a readable coupled-pairs table (the Story 24.1 data) as the text equivalent
**And** generation stays within the deep-git performance envelope and remains generation-time deterministic (FR31) — identical output on a from-scratch CI regen.

### Story 24.4: Chord / Arc Diagram View of Coupling

As a stakeholder who wants an elegant overview,
I want the coupling relationships also presentable as a chord/arc diagram,
So that a bounded set of files and their couplings reads as a single beautiful, symmetric figure.

**Acceptance Criteria:**

1.
**Given** the whole-repo explorer (and, where it fits, the per-file ego view)
**When** I switch to the chord/arc representation
**Then** files are arranged around a ring (or along an axis) with ribbons connecting coupled files, ribbon weight/color driven by the Story 24.1 confidence and cross-boundary couples emphasized, offered as a demoted **alternate view behind a toggle** beside the force-directed network per UX-DR21 (one primary representation per dataset)
**And** the diagram uses a bounded, ranked subset (top couples by confidence/support) so the ring stays readable rather than a hairball.

2.
**Given** the accessibility contract (UX-DR21, NFR8)
**When** the chord/arc view renders
**Then** the shared coupled-pairs table (Story 24.1) remains present as its non-color text-twin and is never removed
**And** with JavaScript off the surface falls back to that table plus, where feasible, a static SVG of the diagram.

### Story 24.5: Adjacency-Matrix Heatmap View of Coupling

As an analyst facing a densely-coupled area,
I want an adjacency-matrix heatmap of coupling strength,
So that dense relationships that overwhelm a node-link graph read unambiguously as a grid.

**Acceptance Criteria:**

1.
**Given** the whole-repo explorer's coupling data
**When** I switch to the matrix representation
**Then** files label both axes and each cell is shaded by the coupling strength (Story 24.1 confidence) between its row/column files, with cross-boundary cells emphasized and a row/column ordering that clusters coupled files together, offered as a demoted **alternate view behind a toggle** per UX-DR21
**And** it carries a real-value legend and framing sentence per Story 10.2 (never a bare "low…high" gradient).

2.
**Given** the accessibility and scale constraints (UX-DR19, NFR8)
**When** the matrix renders
**Then** every cell's strength is available as non-color text (title/tooltip and the shared coupled-pairs table), the matrix is bounded to a readable set of the most-coupled files with an honest "+N more" disclosure, and it degrades to the readable table with JavaScript off
**And** the surface stays within the deep-git performance envelope and is generation-time deterministic (FR31).

### Story 24.6: Epic 24 Graph-Engine Spike — Force-Directed, Chord, and Matrix Under One Contract

> **Runs after Story 24.1 and BEFORE Story 24.2.** Numbered last, executed third — see the epic note above. This is
> the spike [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §4
> hands the graph-engine decision to. Decision-first, timeboxed (~2d), throwaway: **no production code**. Durable
> deliverables are `24-6-spike-report.md` and a **ratified ADR**.
>
> **DECIDED 2026-07-29 — [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md), Accepted.** Engine =
> the **already-vendored Plotly `scatter` trace** over a **generation-time C# layout**; marginal bundle cost
> **zero bytes** (the shipped `plotly-hierarchy.min.js` was measured to register `heatmap, scatter, sunburst,
> treemap`). **ADR 0012 is EXTENDED, not superseded** — no new engine family, no second dependency, and §4's
> allowance of a second family is left unspent. Node position is **data**, not presentation; determinism verified
> byte-identical across **3 separate processes**; filters **hide, never re-lay-out** (a confidence slider yields
> **236 distinct edge sets** vs 17 node sets, so precompute-per-state is not viable). ECharts 6.1.0 measured and
> rejected on **cost-of-change, not merit** (Epic 20 is complete; two live defects found). Cytoscape rejected on
> **UX-DR7** (canvas, zero per-node DOM). **Three things this did NOT resolve, each named in the ADR:** Plotly has
> no chord trace so **Story 24.4** must hand-draw arcs or amend ADR 0030; retiring `Charts.ReferenceGraph`'s SVG
> needs an **ADR 0013 §3 text-twin audit no Epic 24 story owns**; and `StripDataIslands` means the **webview cannot
> receive a graph payload today**. Report: `24-6-spike-report.md`.

As a maintainer about to build four interactive relationship views,
I want Epic 24's graph engine decided on measured evidence before Story 24.2 writes a line of rendering code,
So that the choice ADR 0012 §4 explicitly deferred to "Epic 24's own spike" is made once, in one place, with an ADR behind it — instead of being improvised inside an implementation story.

**Acceptance Criteria:**

1.
**Given** ADR 0012 §4's named open question and the "two engine families permitted, a third requires an ADR" rule
**When** the spike evaluates candidate engines for the force-directed (24.2, 24.3) and chord/arc (24.4) views
**Then** it reports, per candidate, a comparable table of **bundle size** (minified and min+gzip, as a multiple of the already-vendored `prism.js`), **license and provenance** (NFR10), **coverage of the four Epic 24 shapes**, **whether it is a single classic script with no runtime fetch and no ES-module imports**, and **whether adopting it would constitute a third engine family** requiring ADR 0012 to be superseded rather than extended
**And** the "Plotly `scatter` + generation-time layout" option is evaluated **as a first-class candidate at its true marginal cost** — Plotly's `scatter` trace cannot be excluded from any bundle, so if Epic 20 ships Plotly at all, that option costs zero additional bytes.

2.
**Given** ADR 0013 removed the server-rendered SVG that used to sit behind every chart
**When** the spike evaluates the leading candidate(s)
**Then** it reports **explicit PASS / PASS (configured around) / FAIL** conformance per UX-DR7, UX-DR16, UX-DR17 (including per-edge dash/width control, since cross-boundary emphasis may never be color-alone), and UX-DR18 (a force simulation that animates to rest must be able to snap under `prefers-reduced-motion`), verified in a **live browser** using Story 20.4's decision rule
**And** it reports whether the candidate renders under the byte-verbatim VS Code webview CSP, reporting the **script axis and style axis separately** and carrying Story 23.1's `<meta>`-vs-header honesty boundary.

3.
**Given** FR31 generation-time determinism (named by hand in Stories 24.3 and 24.5) and ADR 0010 §3's "computed once at generation time and embedded" (which ADR 0012 §7 leaves standing)
**When** the spike evaluates layout strategy
**Then** it answers whether node position is **data** (solved in C# at generation time, embedded as coordinates) or **presentation** (solved client-side), demonstrates a deterministic result across repeated runs, and reports what happens to determinism when Story 24.3's threshold/grouping clutter controls change the node set
**And** it reports at-scale legibility on **this repository at `--deep-git` scale** — node/edge counts after the Story 24.1 support floor, the point at which the whole-repo view becomes a hairball, and the bounding/threshold defaults Story 24.3 should ship with.

4.
**Given** CLAUDE.md § Decision records and the fact that this choice adds a runtime dependency or a new engine family
**When** the spike concludes
**Then** it lands a **ratified ADR** recording the decision, its options table, and its consequences — either a new ADR that ADR 0012 §4 hands off to, or an explicit supersession of ADR 0012 if the choice unifies both families (the ECharts outcome ADR 0012's own options table pre-authorizes)
**And** the report states what each finding hands to Stories 24.2/24.3/24.4/24.5, and resolves whether the Epic 24 ego graph **supersedes or coexists with** the shipped `Charts.ReferenceGraph` — which already renders a hub-and-spoke focal-file graph carrying a co-changed-file node population, four pre-rendered toggle variants, and cross-edges, and whose retirement would trigger an ADR 0013 §3 text-twin audit no Epic 24 story currently owns.

<!-- Epics 25–26 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed) — see the provenance comment in
     the Epic List above for the full rationale, the D1–D3 owner decisions, and the Story 16.2 consequence. -->

## Epic 25: Continuous Code-Quality Analysis for SpecScribe's Own Development (SonarCloud)

Put SpecScribe's own codebase under continuous automated analysis. Every push to `main` and every pull request builds, tests, and is analyzed by SonarCloud on a clean checkout; a quality gate reports pass/fail as a visible signal; and — the half that makes this more than a dashboard — findings are **triaged into this project's own backlog** using the existing FR30 provenance conventions, so Sonar produces work items the maintainer acts on rather than a page they stop visiting. The epic also defines, via a spike and one implementation, the **framework-neutral contract** by which analysis findings reach AI agents doing spec-driven-development work. That contract is deliberately seated here rather than in Epic 26 because both epics bind to it: Epic 25's agents and Epic 26's human-facing surfaces must consume **one** findings model, not two.

**Dev-time only — this epic ships no product code.** SpecScribe's generated portal output is unchanged by it; the golden fingerprint does not move.

**NFRs covered:** NFR11 · **Status:** backlog · **Depends on:** nothing — Story 25.1 is schedulable immediately.

**Structural note.** This repository has **no build/test CI today** — `.github/workflows/` contains only `publish-docs-live-pages.yml`. The story that would add one, **Story 16.2**, sits `backlog` behind the entire roadmap (Epic 16 runs last, after Epic 17's hardening sign-off). SonarCloud's C# analysis must wrap a real build, so **Story 25.1 stands up the repository's first build+test workflow** and **Story 16.2 is amended** to harden that workflow into a release-relevant required gate rather than create a second one. NFR9 coverage is unaffected.

### Story 25.1: SonarCloud Onboarding and Automated Analysis on Every Push to `main`

> Stands up SpecScribe's **first** build+test CI workflow. See the Story 16.2 amendment — 16.2 extends this workflow rather than creating a second one.

As the SpecScribe maintainer,
I want every push to `main` and every pull request to build, test, and be analyzed by SonarCloud on a clean checkout,
So that code-quality regressions surface automatically instead of being discovered during a hardening epic months later.

**Acceptance Criteria:**

1.
**Given** a push to `main` or a pull request
**When** CI runs
**Then** a workflow restores, builds, and executes `tests/SpecScribe.Tests` on a clean checkout, runs the SonarScanner for .NET wrapping that build (begin → build → test → end), and uploads results to a SonarCloud project bound to `IntegerMan/SpecScribe`
**And** the job fails on any build or test failure, and the workflow is independent of and does not disturb `publish-docs-live-pages.yml`.

2.
**Given** analysis requires a token
**When** the workflow authenticates
**Then** `SONAR_TOKEN` is read from a repository secret, no secret value is committed, and the workflow is safe on pull requests from forks (analysis is skipped or runs without the token rather than leaking it)
**And** the SonarCloud project's visibility and the free-OSS-tier terms are recorded in the story record.

3.
**Given** test coverage improves finding quality
**When** the analysis runs
**Then** the story records an explicit decision on coverage collection — collector, report format, upload path, and the measured effect on suite runtime for a ~2,350-test suite — either implementing it or recording why it is deferred, never leaving it unstated.

4.
**Given** the ~2,350-test suite has never executed outside the maintainer's Windows machine, and `GoldenContentFingerprint` is a byte-exact SHA-256 over all generated output
**When** the workflow first runs green
**Then** the story records which runner OS was chosen, the evidence for it (a full-suite pass with pass/fail/skip counts), and — if a non-Windows runner was attempted — every test that behaved differently there
**And** any test changed to make CI pass is listed individually with its root cause, so a portability bug is never disguised as a CI tweak; regenerating `GoldenContentFingerprint` to make CI green is not an available remedy.

<!-- AC #4 added 2026-07-25 (create-story 25.1). The three original ACs assumed the suite runs anywhere; it never
     has. This repository has no `.gitattributes`, so a Windows and a Linux checkout of the same commit genuinely
     differ in line endings, and the fingerprint constant was generated on Windows. Left implicit, the cheapest
     way to a green first CI run would have been to regenerate the fingerprint — converting a real portability
     finding into a silent maintenance edit on the exact constant CLAUDE.md § Verification warns about. This AC
     also bounds the story's only sanctioned reason to touch `tests/**`; `src/**` stays out of scope entirely. -->

### Story 25.2: Quality Gate and Findings Triage into the Project Backlog

As the SpecScribe maintainer,
I want the analysis results scanned and routed into this project's own backlog,
So that Sonar produces work items I actually act on rather than a dashboard I stop visiting.

**Acceptance Criteria:**

1.
**Given** an analysis run completes
**When** the quality gate evaluates
**Then** a defined gate (new-code conditions at minimum) reports pass/fail as a visible signal on the pull request
**And** the story records which conditions are enforcing vs advisory, and what a failing gate blocks.

2.
**Given** findings accumulate
**When** they are triaged
**Then** a documented, repeatable triage pass routes each material finding to a decision — fixed, scheduled into a named story, or explicitly accepted with rationale — and lands in `deferred-work.md` / `sprint-status.yaml` action items using the existing FR30 provenance conventions
**And** the initial baseline triage of the existing codebase is performed and its result recorded, so Epic 17's hardening pass inherits a known state rather than an unread dashboard.

3.
**Given** findings overlap Epic 17's scope
**When** triage runs
**Then** items matching Stories 17.1–17.3 (structural, security/privacy, performance) are tagged to those stories rather than duplicated
**And** anything Sonar reports that the project deliberately does not follow is recorded as a rule-level decision, not silently re-triaged every run.

### Story 25.3: SPIKE — A Framework-Neutral Findings Contract for AI Agents in SDD Workflows

> Decision-first, timeboxed (~2d), **throwaway — no production code**. Durable deliverables: `25-3-spike-report.md`
> and a **ratified ADR**. This spike's contract is consumed by Story 25.4 **and** by all of Epic 26 — it is the one
> place the findings model is defined.

As a maintainer whose planning agents should know what the analyzer knows,
I want the shape of agent-consumable analysis findings decided once — framework-neutral and source-agnostic —
So that neither Epic 25's tooling nor Epic 26's surfaces invent a Sonar-shaped, BMad-shaped model that the other has to work around.

**Acceptance Criteria:**

1.
**Given** the owner's framework-neutral requirement (NFR8)
**When** the spike defines the contract
**Then** it specifies a findings model keyed to **entities SpecScribe already projects** — file, directory, epic, story, requirement — carrying at minimum: source/provider, rule identity, severity **on a normalized scale with a text label** (never color-alone, UX-DR17), location, message, and provenance/analysis timestamp
**And** it demonstrates the model holds for a **second, structurally different source class** — compiler/analyzer warnings, which the owner flagged as language-dependent — proving Sonar is instance #1 and not the schema.

2.
**Given** findings must attach to planning entities, not just files
**When** the spike defines attachment
**Then** it specifies how a file-scoped finding reaches an epic/story/requirement, evaluating the **shipped** `PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`) commit/branch miner and the Epic 19 work graph as the join, and states honestly where the join is approximate or absent
**And** it states what happens to findings that attach to **no** planning entity (the common case) so they are never silently dropped.

3.
**Given** agents consume this in SDD workflows across frameworks
**When** the spike evaluates delivery channels
**Then** it compares — with a recommendation — at least: a generated agent-readable digest artifact, a field on the Epic 22 JSON IR, and an MCP-server surface; reporting for each the framework-neutrality (NFR8), the offline behavior, and whether it requires SpecScribe to gain a runtime it does not have
**And** it states which channel Story 25.4 implements and what it defers.

4.
**Given** CLAUDE.md § Decision records
**When** the spike concludes
**Then** it lands a **ratified ADR** recording the findings contract, its options table, and its consequences — this is a cross-cutting contract two epics bind to, and must not be settled inside an implementation story's dev pass
**And** the report states explicitly what it hands to Story 25.4 and to Stories 26.2–26.6.

### Story 25.4: Agent-Consumable Findings Channel for SpecScribe's Own SDD Workflow

As a maintainer running create-story and dev-story,
I want the current analysis findings available to my agents in the channel Story 25.3 selected,
So that planning and implementation passes account for known quality debt in the files they are about to touch.

**Acceptance Criteria:**

1.
**Given** Story 25.3's ratified contract and selected channel
**When** the channel is implemented
**Then** current findings for this repository are emitted in the contracted shape and are demonstrably consumable by an agent during a real create-story or dev-story pass, with a worked example recorded
**And** the implementation honors NFR12: it is opt-in, produces nothing rather than failing when findings are unavailable, and writes no token value anywhere.

2.
**Given** this is dev-time tooling, not a product feature
**When** it ships
**Then** it does not alter SpecScribe's generated portal output — the golden fingerprint is unmoved — and any code added is quarantined from the generation critical path, with Epic 26 named as the epic that makes findings a *product* surface
**And** staleness is honest: consumers can tell how old the analysis is and when it predates the working tree.

### Story 25.5: A Local, Browsable Coverage Report in One Command

> Dev-time tooling only. Ships NO product code and must not move the golden fingerprint.

As the SpecScribe maintainer,
I want to produce a browsable coverage report locally with a single documented command,
So that I can find untested code while I am working, without pushing a commit and opening SonarCloud to see it.

**Acceptance Criteria:**

1.
**Given** `coverlet.collector` 6.0.4 is already referenced and Story 25.1 already emits OpenCover from `dotnet test`
**When** the documented command is run
**Then** a browsable HTML coverage report is produced locally from that same collector and format — **no second coverage mechanism is introduced** — and the command is recorded in `README.md` alongside the existing `dotnet test` guidance
**And** the report output directory is gitignored, verified with `git check-ignore`, not assumed.

2.
**Given** CI already measures coverage at 89.8%
**When** the local report is generated
**Then** the local percentage is reconciled against the CI/SonarCloud figure and any discrepancy is explained rather than left as two numbers that disagree
**And** the story records the measured cost of generating the report, so the command's expense is known before it is recommended.

3.
**Given** this is dev-time tooling
**When** it ships
**Then** `GoldenContentFingerprint` is unmoved and nothing under `src/` changes.

### Story 25.6: Coverage and Quality Badges on the README

As a visitor evaluating SpecScribe,
I want the README to show current build, coverage, and quality-gate status at a glance,
So that the project's health is visible before I read a line of code.

**Acceptance Criteria:**

1.
**Given** SonarCloud publishes badge endpoints for this project
**When** badges are added to `README.md`
**Then** they render **green at the moment they land** — a permanently-red badge on the front page is worse than none — and each badge links to the surface that explains it
**And** the coverage badge shows the same figure the CI analysis reports, not a separately-computed one.

2.
**Given** the quality gate is Story 25.2's decision, not this story's
**When** a quality-gate badge is added
**Then** it is added **after** 25.2 has settled what the gate asserts, so the badge cannot advertise a gate that does not yet mean anything
**And** if 25.2 has not landed, the coverage and build badges may ship alone and the story says so explicitly.

3.
**Given** badges are external image requests
**When** they are added
**Then** the story records what each badge URL discloses about the project, confirming nothing private is implied by a public badge (NFR10's disclosure discipline).

## Epic 26: Optional External Code-Analysis Insights — Findings Alongside Code, Directories, and Planning

Make external code-quality analysis an **optional insight provider** in SpecScribe, so a user who has Sonar can see findings rendered against the entities the portal already models — code files, directories, epics, stories, and requirements — rather than in a separate tool. This is AD-4 ("optional insight providers may enrich output but never own baseline success") applied to a **networked** provider, which is why NFR12 exists: opt-in, offline-safe, credential-safe, and **disabled by default**.

The findings model is **source-agnostic from the first line**. Sonar is instance #1, not the schema — the owner's "we could potentially fold in code analysis warnings as well, but that gets to be language dependent" is exactly the pressure the model must survive. Additional source classes are surveyed in Story 26.7, not assumed here.

The epic is led by an owner-elicited **ideation** round (26.1) that fixes visual direction before any surface is built, and a decision-first **spike** (26.2) that settles the ingestion posture, the credential design, and the PRD NFR-3 local-first question with a ratified ADR. **The attach points already exist** — `CodeFileTemplater` (`src/SpecScribe/CodeFileTemplater.cs:18`), `FileInsight` (`src/SpecScribe/GitMetrics.cs:169`), `PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`), and `SettingsResolver` (`src/SpecScribe/SettingsResolver.cs:63`) — so no new entity modelling is required to hang findings on code, directories, or planning items.

**FRs covered:** FR41 · **NFRs:** NFR12, NFR8 · **UX-DRs:** UX-DR17 (severity is never color-alone), UX-DR21 (one primary representation per dataset; text twins are contract), UX-DR22 (designed empty states) · **Status:** backlog · unscheduled · **Depends on:** Story 25.3 (the findings contract it consumes), Epic 7 (code pages), Story 21.3 (`PlanningCodeImpact`), Story 5.2 (`SettingsResolver`).

**Execution order:** 26.1 → 26.2 → 26.3 → 26.4 / 26.5 / 26.6. Story 26.7 is independent and schedulable at any time.

<!-- 2026-07-25 (SCP 2026-07-25): THE NFR-3 CROSSING IS STORY 26.2's TO DECIDE, and is deliberately not pre-decided
     here. If 26.2's spike selects the SonarCloud web API, SpecScribe makes its first outbound network call and gains
     its first credential handling, which sits against the PRD's NFR-3 ("analysis runs locally BY DEFAULT; no remote
     telemetry is REQUIRED FOR CORE OPERATION") and brushes the § 5 Non-Goal on remote data processing. Note NFR-3's
     own qualifiers may already accommodate an opt-in, non-required integration — an amendment may prove to be a
     clarification rather than a concession. 26.2 must state which it is, plainly, and draft exact replacement text
     if an amendment is needed (the ADR 0013 / NFR-5 precedent: preserve prior wording + rationale inline; never
     frame a real product concession as a reinterpretation). -->

### Story 26.1: IDEATION — Where Analysis Findings Belong in the Portal

> Owner-elicited ideation, per the project's create-story visual-intent convention. Deliverable is a decision record
> naming the integration points and their visual direction — **no code**.

As the owner,
I want to decide deliberately where and how analysis findings should appear across the portal before any surface is built,
So that the integration-point stories start from named visual direction instead of discovering it in a post-implementation revision round.

**Acceptance Criteria:**

1.
**Given** the entity set the owner named — code, directories, epics, stories, requirements
**When** the ideation round runs
**Then** it produces, for each candidate surface, a concrete proposal covering placement, density, empty state, and how severity reads **without color** (UX-DR17), with **2–3 named design directions** offered for every new visual surface and the owner's selection recorded
**And** it names which candidates are **in** for Stories 26.4–26.6 and which are explicitly **out**, so the integration-point stories have a closed scope.

2.
**Given** the portal already carries substantial insight surfacing
**When** placement is chosen
**Then** the record states where findings **reuse** an existing surface (code pages, code map, traceability matrix, dashboard strip) versus where a **new** page is justified, and applies UX-DR21 (one primary representation per dataset)
**And** it states what a project **without** any analysis configured sees — the default case for every user.

3.
**Given** the owner's "we could potentially fold in code analysis warnings as well"
**When** scope is set
**Then** the record states whether non-Sonar source classes are in scope for Epic 26's surfaces or deferred to Story 26.7, with the language-dependence trade-off recorded rather than left implicit.

### Story 26.2: SPIKE — Ingestion Posture, Credential Design, and the NFR-3 Local-First Question

> Decision-first, timeboxed (~2d), **throwaway — no production code**. Durable deliverables: `26-2-spike-report.md`
> and a **ratified ADR**. **Gates Stories 26.3–26.6.**

As a maintainer about to give SpecScribe its first outbound network capability,
I want the ingestion posture and credential design decided on evidence with an ADR behind it,
So that the local-first question is answered once, in the open, rather than implied by whichever implementation story happens to land first.

**Acceptance Criteria:**

1.
**Given** the owner deferred the posture to this spike
**When** candidate sources are evaluated
**Then** it reports, per candidate — **SonarCloud web API**, **on-disk scanner report/export**, and **both** — the data available, freshness, offline behavior, credential requirement, rate limits, and the failure mode when the source is missing or stale
**And** it evaluates the on-disk path at its true cost, including whether a user without a SonarCloud account can get any value at all.

2.
**Given** PRD **NFR-3** ("analysis runs locally by default; no remote telemetry is required for core operation") and the § 5 Non-Goal on remote data processing
**When** the spike assesses the crossing
**Then** it states plainly whether the recommended posture **requires a PRD amendment** or is already accommodated by NFR-3's "by default" / "required for core operation" wording — and if an amendment is required, it drafts the exact replacement text with the prior wording and rationale preserved inline, following the ADR 0013 / NFR-5 precedent
**And** it does **not** treat a real product concession as a reinterpretation.

3.
**Given** any network posture needs a credential
**When** the spike designs credential handling
**Then** it specifies where the token lives (environment variable, directory-scoped `.specscribe` via `SettingsResolver` `src/SpecScribe/SettingsResolver.cs:63`, or external), proves no token value can reach generated output, `--show-config`, the diagnostics page, or a committed settings file, and states the private-repository posture
**And** it names the supply-chain surface any new dependency adds, handing it to Story 17.2 (NFR10).

4.
**Given** Story 25.3's contract and CLAUDE.md § Decision records
**When** the spike concludes
**Then** it lands a **ratified ADR** covering ingestion posture, credential design, and the AD-4 provider boundary, **consuming Story 25.3's findings model rather than defining a second one** — and stating explicitly if it must amend it
**And** the report states what it hands to Stories 26.3–26.6 and to Epic 22's IR schema (Story 22.2).

### Story 26.3: Analysis Integration Configuration (CLI, Interactive, and Settings Parity)

As a user,
I want to turn analysis integration on, point it at my project, and see where its configuration came from, using the same mechanisms as every other SpecScribe option,
So that it is not a special case I have to learn.

**Acceptance Criteria:**

1.
**Given** NFR7 configurability parity and AD-3
**When** the integration is configured
**Then** enablement and source configuration are available as CLI flags, in the interactive flow, and as directory-scoped `.specscribe` persistence — resolved once through `SettingsResolver` with three-way provenance visible in `--show-config`, per the Story 5.2 pattern
**And** the README documents the options as a table with short descriptive text (PRD §12.3).

2.
**Given** NFR12 and AD-4
**When** the integration is unconfigured, disabled, or the source is unreachable
**Then** baseline generation completes unchanged and non-fatally with a clear diagnostic, findings surfaces are **absent rather than broken or misleadingly empty** (NFR8/UX-DR22), and default generation performance does not regress (NFR1)
**And** **disabled is the default** — an existing user upgrading sees no behavior change and makes no network call.

3.
**Given** credentials
**When** configuration is surfaced
**Then** no token value appears in `--show-config`, the diagnostics page, generated output, or any file the tool writes into the repository — pinned by a regression test
**And** a misconfigured or expired credential produces an actionable message, never a stack trace or a silent empty surface.

### Story 26.4: Findings on Code Pages and the Code Map (File and Directory Scope)

As a developer browsing a file in the portal,
I want that file's analysis findings shown alongside its git and coupling signal,
So that quality context lives where I am already looking instead of in a separate tool.

**Acceptance Criteria:**

1.
**Given** an ingested findings set and a code page
**When** it renders
**Then** the file's findings appear on the page in the direction Story 26.1 selected, each showing rule, normalized severity **as text as well as any color** (UX-DR17/NFR8), message, and line — deep-linking to the existing `#L{n}` code anchor — attaching through the `CodeFileTemplater` / `FileInsight` seam (`src/SpecScribe/CodeFileTemplater.cs:18`, `src/SpecScribe/GitMetrics.cs:169`) rather than a parallel code-page pipeline
**And** a file with no findings shows a designed empty state, never a broken or misleading one.

2.
**Given** the directory-scope surface (code map / treemap)
**When** findings are surfaced there
**Then** directory-level aggregation is rendered per Story 26.1's direction with a Story 10.2-compliant real-value legend, analysis window, and framing sentence
**And** it honors the Hierarchy Explorer contract if it rides a hierarchy chart (ADR 0012) and carries a server-rendered text twin (ADR 0013 §3) verified JS-off in a live browser.

3.
**Given** deterministic generation (FR31)
**When** the surfaces render
**Then** output is stable across repeated runs from the same inputs, and the golden fingerprint move is intentional and re-baselined with a stability check across two runs (CLAUDE.md).

### Story 26.5: Findings on Planning Entities (Epics, Stories, Requirements)

As a stakeholder reading an epic or story,
I want to see the quality findings in the code that work touched,
So that "done" carries quality context and not only a status badge.

**Acceptance Criteria:**

<!-- AMENDED 2026-07-29 by Story 26.1's ideation record (§ 3.4, § 6, § 11 items 1-2), owner-selected:
     (a) REQUIREMENT PAGES ARE OUT. Story 26.1 surface S4 = "explicitly out", honoring ADR 0023 § Decision 5 —
         `requirement` is not a first-class attachment key, so observation → file → epic → requirement is two hops
         with the second at epic granularity only, composed on a join already amplifying 10.02x. AC #1's entity
         list is narrowed to epic and story pages accordingly. The prior wording read "epic, story, and requirement
         pages surface the findings in the code their work touched".
     (b) The chosen direction is S3 = B, "a chip per row" — a total-count chip on each file row INSIDE the existing
         Code Areas Touched block, plus ONE rollup sentence above the table which is where the mandatory
         approximateness caveat lives. See AC #4 for the re-parenting this story also owns. -->
1.
**Given** Story 25.3's attachment rule and the shipped `PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`)
**When** findings are attached to planning entities
**Then** epic and story pages surface the findings in the code their work touched — in the direction Story 26.1 selected (a total-count chip per file row inside the existing Code Areas Touched block, plus one rollup sentence above the table) — using the existing miner as the join, never a second, divergent story↔file mapping
**And** the attachment's **approximateness is stated on the surface** in that rollup sentence, following the Story 21.2 cycle-time precedent, so an inferred link is never presented as a tracked fact
**And** requirement pages surface nothing: Story 26.1 recorded them **explicitly out** with its reason, so their absence is a decision and not an omission.

2.
**Given** many findings attach to no planning entity
**When** the surfaces render
**Then** unattached findings are reachable from the hub (Story 26.6) and are never silently dropped, and an entity with no attributable findings shows a designed empty state distinguishable from "analysis not configured" (UX-DR22/NFR8)
**And** counts route through the existing single count source (FR21 / Story 8.3) rather than a new tally.

3.
**Given** NFR8 framework-agnosticism
**When** the surfaces render for a non-BMad project
**Then** they degrade to absent rather than broken where the framework lacks the underlying artifact types, with any framework-specific vocabulary supplied through the Epic 4 adapter contract.

<!-- AC #4 ADDED 2026-07-29 by Story 26.1's ideation record (§ 6, § 11 item 2), owner-selected. Story 26.1 settled
     the live vocabulary collision: ADR 0023 § Decision 1 locks the machine-ingested noun as "Analysis
     Observations", while story pages already render an authored-prose <h3>Review Findings</h3>. The owner chose to
     apply the re-parenting NOW rather than leave it a latent policy, so it is this story's work. -->
4.
**Given** ADR 0023 § Decision 1 locks the machine-ingested noun as "Analysis Observations" and story pages already render an authored-prose `Review Findings` section (`src/SpecScribe/HtmlRenderAdapter.Epics.cs`, `id="sec-review-findings"`)
**When** story pages render
**Then** both sit under one **Quality** parent heading as sibling subsections, so the human/machine distinction is structural rather than inferred from two similar headings — epic pages are unaffected, having no `Review Findings` equivalent
**And** the `sec-review-findings` anchor is preserved so existing deep links do not break, the chips and the rollup remain **one** count routed through the FR21 single generator-side count source rather than two tallies, and the resulting golden-fingerprint move — larger than the chips alone, because section nesting and TOC depth change on every story page carrying review prose — is intentional and re-baselined with a stability check across two runs (CLAUDE.md).

### Story 26.6: Analysis Hub Page and Dashboard Signal

As a maintainer,
I want one page that answers "what is the state of this project's code quality" and a compact dashboard signal pointing at it,
So that findings have a home and a 30-second summary.

**Acceptance Criteria:**

<!-- AMENDED 2026-07-29 by Story 26.1's ideation record (§ 3.5, § 11 item 3), OWNER-INVENTED direction that
     superseded the pre-researched menu: "I like the rule leaderboard and triage inbox approaches, but both might
     need separate pages with a highlight style widget teasing the most actionable." The hub is therefore THREE
     pages, not one. The prior wording read "a dedicated page presents the findings set in the direction Story 26.1
     selected". Still ONE nav entry (the landing page), following the Git Insights -> Deep Analytics precedent.
     Deliberately NO hierarchy chart on the hub: Story 26.1's S2 = A puts the portal's only observation hierarchy on
     the Code Map, and a second one over the same file tree is the exact UX-DR21 pressure the rule exists to
     prevent. -->
1.
**Given** an ingested findings set
**When** the hub renders
**Then** a **landing page** carries the highlight widget, the four normalized severity levels, and the provenance/staleness block, with two full-surface link cards (UX-DR9) into **two child pages** — a **rule leaderboard** ranking the distinct rules so a single fix that clears many occurrences is visible, and a **triage inbox** giving sortable/filterable access to every observation including those attached to no planning entity — all reachable from a single insight-pages nav entry (FR27) on the integration's own gate, mirroring the Git Insights hub pattern
**And** the highlight widget's ranking is reader-selectable across count, quality type, and a blended score, defaulting to blended, with the blended formula written out in the framing sentence (FR28) so the default ranking is auditable and its inputs limited to severity level, occurrence count, and file concentration
**And** every chart on it carries a Story 10.2 real-value legend, analysis window, and framing sentence, plus a text twin per ADR 0013 §3.

2.
**Given** the dashboard's 30-second-pulse contract (Epic 8)
**When** the signal renders
**Then** a compact strip summarizes quality state and links to the hub, following the Story 21.1/21.2 dashboard-strip placement pattern, without displacing existing pulse content
**And** it is absent — not empty — when the integration is disabled, which is the default.

3.
**Given** analysis data has an age
**When** any surface renders
**Then** the analysis timestamp is shown using the portal-wide date token (UX-DR25) and stale analysis is marked honestly rather than presented as current
**And** output remains generation-time deterministic (FR31).

<!-- AC #4 ADDED 2026-07-29 by Story 26.1's ideation record (§ 3.7, § 11 item 4), owner-directed. Story 26.1's
     candidate surface S7 (the traceability matrix) was the one surface the owner chose NOT to close in the ideation
     round: "keep it open for 26.6". The recommendation is OUT and its reasoning is recorded in that record; this AC
     makes the deferred decision this story's, so it cannot fall between the two. -->
4.
**Given** Story 26.1 deferred the traceability-matrix candidate (its surface S7) to this story rather than closing it, with a recorded recommendation of **out**
**When** this story's scope is set
**Then** it states whether a severity axis is added to `TraceabilityTemplater`'s requirement × covering-epic grid, weighing the two recorded arguments against it — that a third axis on a two-axis grid is a different chart, and that ADR 0023 § Decision 5 already refused the requirement edge (the same reasoning that made Story 26.1's S4 explicitly out) — plus the grid's placement in the **Delivery** nav group rather than Insights
**And** whichever way it lands, the decision and its reason are recorded so Epic 26 closes with a complete in/out list.

### Story 26.7: INVESTIGATION — Future External-Service Integration Points

> Investigation, timeboxed, **no production code**. Deliverable: a written landscape + recommendation record.
> Independent of the rest of Epic 26 — schedulable at any time.

As the maintainer deciding what SpecScribe should connect to next,
I want the broader external-signal landscape surveyed once,
So that the second and third integrations extend Story 26.2's provider boundary instead of each inventing their own.

**Acceptance Criteria:**

1.
**Given** Sonar as the first external provider
**When** the investigation surveys the landscape
**Then** it inventories candidate external signal sources — for example GitHub code scanning / Dependabot / Actions status, coverage services, dependency-vulnerability services, other quality platforms, and **local compiler/analyzer output** (the owner's language-dependent case) — recording for each the data available, auth requirement, offline behavior, and whether it fits Story 25.3's findings model unchanged
**And** it explicitly separates candidates that fit the existing model from those that would require a new one.

2.
**Given** NFR12 and AD-4
**When** the investigation assesses the provider boundary
**Then** it states whether Story 26.2's ingestion design generalizes to a **pluggable external-signal provider seam**, or whether each service needs bespoke work, with a concrete recommendation and the ADR trigger named if a seam is warranted
**And** it assesses the local-first and credential-sprawl cost of each additional integration honestly, including the case for stopping at one.

3.
**Given** this is exploratory
**When** it concludes
**Then** it produces a **prioritized** recommendation of which integrations (if any) to seat as future stories, with a stated "none of these" option, feeding `deferred-work.md` / the epic backlog rather than auto-seating stories
**And** it records what would have to be true for each candidate to become worth building.

## Epic 27: Test-Coverage Insights — Per-File Coverage on Code Pages and Hierarchy Surfaces

Surface **test coverage** for the user's own codebase against the code entities SpecScribe already models, so "how well tested is this?" is readable in the same place as "how often does this change?" and "what does this implement?". Coverage is read from a report the user's own test run already produces — SpecScribe **never runs tests** and never requires them to be run.

This is AD-4 ("optional insight providers may enrich output but never own baseline success") applied to a **purely local** provider: a coverage report on disk. That is the sharp difference from Epic 26 — no network call, no credential, no service dependency in the baseline path, so NFR12's tension does not arise here. The only external touch is the optional link out to the analysis tool's per-file page, which NFR12 already governs.

Owner-directed scope (2026-07-26): **rollups and analytics, not per-line marks.** Per-file and per-directory percentages, encoded onto the treemap/sunburst hierarchy surfaces and shown on code file pages, with covered/total **line counts** carried as numbers. Per-line covered/uncovered gutter marks are explicitly **out** — see Story 27.6 for how that decision gets revisited on evidence.

**The attach points already exist**, so no new entity modelling is required: `CodeFileTemplater` for the code page, `FileInsight` (`src/SpecScribe/GitMetrics.cs`) for the per-file record hotspots and coupling already ride, `CodeMap`/`HierarchyExplorer` for the treemap and sunburst, and `SettingsResolver` for opt-in configuration.

**FRs covered:** FR42 · **NFRs:** NFR12 (link-out only), NFR8 · **UX-DRs:** UX-DR17 (coverage is never color-alone), UX-DR21 (one primary representation per dataset; the text twin is contract), UX-DR22 (designed empty states) · **ADRs:** AD-4, ADR 0010, ADR 0012 / ADR 0013 · **Status:** backlog · unscheduled · **Depends on:** Epic 7 (code pages), Story 20.5 (the standardized Hierarchy Explorer), Story 7.6 (the Code Map treemap).

**Execution order:** 27.1 → 27.2 → 27.3 → 27.4 / 27.5 → 27.6. Stories 27.4 and 27.5 are parallelizable once 27.3's metric spine exists.

<!-- Epic 27 added 2026-07-26 (owner-directed, during Story 25.1's dev pass). Three things a later reader needs:

     1. NAMING COLLISION, LOAD-BEARING. `ArtifactCoverage.cs`, `SiteGenerator.RefreshCoverage()`, and the
        dashboard's "Planning Artifacts" panel ALREADY mean PLANNING-ARTIFACT coverage when they say "coverage".
        Ship test coverage under that same bare word and the portal shows two unrelated metrics both labelled
        "coverage". Story 27.2 must fix a distinct vocabulary BEFORE any surface is built. Same class as the
        unresolved PRD-vs-epics.md NFR numbering collision — a naming collision left implicit is paid for later
        at multiplied cost.

        CONCRETE INSTANCE, MEASURED (added 2026-08-09 by the Story 17.1 code review). The collision is not just
        prospective — it has ALREADY happened in CSS and the shipped layout depends on it. `specscribe.css`
        declares `.coverage-card` TWICE at top level, and the two blocks are two DIFFERENT components, not a
        duplicate to merge. Verified in a live browser: a block-2 card computes `flex-direction: column` sourced
        from block 1 alone, while block-1 cards compute `max-width: 460px` / `flex: 1 1 320px` /
        `align-items: flex-start` sourced from block 2 — i.e. each component is relying on declarations that
        leak from the other. Story 17.1 correctly REFUSED to merge them (its own task text told it to check
        first) and re-routed the decision here. Merging or scoping the two blocks moves ~120 elements across
        100+ pages, so 27.2's vocabulary fix must rename these components, not just the new test-coverage ones.
        Full diagnosis: deferred-work.md § "Deferred from: code review of
        17-1-structural-and-consistency-remediation-sweep (2026-08-09)".

     2. COORDINATION WITH EPIC 26, NOT MERGER. The owner asked for coverage 'in addition to other information from
        code analysis and a link to the external tool page' — i.e. coverage and Epic 26's findings share surfaces.
        They are kept as separate epics deliberately: coverage is a per-file METRIC with a local, credential-free
        ingestion path; findings are discrete SEVERITY-BEARING items arriving from a networked service. Merging
        would drag coverage into NFR12's credential/offline design for no benefit. But whichever epic lands second
        MUST extend the first's code-page section rather than add a second one — that is the drift class this
        project has repeatedly paid for. Story 27.4 owns that constraint explicitly.

     3. SCOPE DISCIPLINE. SpecScribe must never run the user's tests. It reads a report the user already produced.
        Any story that starts shelling out to `dotnet test` / `npm test` has left this epic. -->

### Story 27.1: IDEATION — How Coverage Should Read Across the Portal

> Owner-elicited ideation, per the project's create-story visual-intent convention. Deliverable is a decision record
> naming the surfaces and their visual direction — **no code**.

As the owner,
I want to decide deliberately how coverage should read on each surface before any of it is built,
So that the implementation stories start from named visual direction instead of discovering it in a post-implementation revision round.

**Acceptance Criteria:**

1.
**Given** the surfaces the owner named — the Code Map treemap, the sunburst / Hierarchy Explorer, and code file pages
**When** the ideation round runs
**Then** it produces for each surface a concrete proposal covering placement, density, empty state, and **how coverage reads without color** (UX-DR17), with **2–3 named design directions** offered per surface and the owner's selection recorded
**And** it names which surfaces are **in** for Stories 27.4–27.6 and which are explicitly **out**, so those stories have a closed scope.

2.
**Given** coverage is a continuous 0–100% value while the portal's existing encodings are categorical status tokens
**When** the visual direction is chosen
**Then** it states whether coverage gets a **new** scale or reuses an existing token family, and if new, how it stays distinguishable from the six `--status-*` tokens that already carry stage meaning
**And** it decides what an **unknown**-coverage file looks like — a file absent from the report is not the same as a file at 0%, and conflating them would be a lie the eye cannot catch.

3.
**Given** the hierarchy surfaces already encode weight and status
**When** coverage is added to them
**Then** the proposal states what coverage **replaces or coexists with**, honoring UX-DR21's one-primary-representation rule rather than stacking a third meaning onto one wedge.

### Story 27.2: SPIKE — Coverage Ingestion Contract, Path Mapping, and Vocabulary

> Decision-first, timeboxed, throwaway. NO production code. Durable deliverables are the spike report and a **ratified ADR**.

As the maintainer,
I want the coverage ingestion posture settled before any surface is built,
So that format support, path mapping, and the naming collision are decided once rather than re-litigated in every downstream story.

**Acceptance Criteria:**

1.
**Given** coverage reports come in several formats
**When** the spike runs
**Then** it selects which formats the first cut supports — **Cobertura, OpenCover, and lcov are the candidates**, and Cobertura is the cross-ecosystem default most tools emit — and states plainly which are deferred and why
**And** it decides how the report is located: explicit setting, convention-based discovery, or both, routed through `SettingsResolver` rather than a new configuration mechanism.

2.
**Given** a coverage report addresses files by its own path convention and SpecScribe addresses them by repo-relative path
**When** the mapping is designed
**Then** the spike proves it against a **real** report from a real repository, not a hand-written fixture, and states its failure mode when a path cannot be matched
**And** unmatched entries surface as a diagnostic rather than being silently dropped — a coverage surface that quietly omits half the codebase is worse than none. This is the lesson Story 25.1 paid for when an exclusion list looked complete and was ~26% wrong.

3.
**Given** `ArtifactCoverage` already owns the word "coverage" in this codebase for PLANNING-ARTIFACT coverage
**When** the vocabulary is fixed
**Then** the ADR names the distinct user-facing term and the distinct type/member names test coverage will use, and confirms no existing surface's label becomes ambiguous
**And** the decision is ratified as an ADR, not left as a note in a story file.

4.
**Given** SpecScribe must never run the user's tests
**When** the posture is recorded
**Then** the ADR states that ingestion is read-only over an existing report, opt-in, and absent-not-broken, and that a missing, stale, or malformed report degrades the surface rather than failing generation (AD-4, NFR2)
**And** it decides whether report **staleness** relative to the working tree is detectable and, if so, how it is disclosed — a confidently-rendered coverage figure computed from a month-old report is a quiet lie.

5.
**Given** the owner asked for a link out to the external analysis tool's page for a file
**When** that link is designed
**Then** the spike states how the target URL is derived and configured, and confirms it honors NFR12 — no credential, and the link is simply absent when unconfigured rather than rendering broken.

### Story 27.3: The Coverage Metric Spine

> Non-visual. Mirrors Story 24.1's pattern: the metric and its tests land first and gate every surface that renders it.

As the maintainer,
I want a tested per-file and per-directory coverage model before anything renders it,
So that the visual stories consume one authoritative metric instead of each computing its own.

**Acceptance Criteria:**

1.
**Given** Story 27.2's ratified ingestion contract
**When** a coverage report is ingested
**Then** a per-file record carries **covered lines, total coverable lines, and the derived percentage** — the counts the owner asked to keep, not the percentage alone — and rolls up to directories as a **line-weighted** aggregate, never a mean of percentages, which would let a 3-line file outvote a 3,000-line one
**And** a file present in the codebase but absent from the report is representable as **unknown**, distinctly from 0%.

2.
**Given** the portal already carries a per-file insight record
**When** coverage is added
**Then** it extends the existing `FileInsight` seam that hotspots and coupling already ride rather than introducing a parallel per-file model
**And** the metric is computed once at generation time and handed to every surface, per ADR 0010's precomputation rule.

3.
**Given** coverage is opt-in
**When** no report is configured
**Then** generated output is **byte-identical** to output produced before this story — proven by the golden fingerprint being unmoved — so the feature costs nothing to projects that do not use it.

### Story 27.4: Coverage on Code File Pages

As a developer reading a file in the portal,
I want that file's coverage shown alongside its other code-analysis signal, with a link to the external tool,
So that I can judge how well tested it is without leaving the page or opening another tool.

**Acceptance Criteria:**

1.
**Given** Story 27.3's metric and Story 27.1's chosen direction
**When** a code file page renders for a file present in the coverage report
**Then** it shows the coverage percentage **and** the covered/total line counts behind it, so the number is auditable rather than asserted
**And** coverage is conveyed by more than color (UX-DR17), with the accessible text equivalent every insight surface in this project owes.

2.
**Given** Epic 26 places findings on this same page
**When** whichever of the two epics lands second is implemented
**Then** it **extends the existing code-analysis section rather than adding a second one** — two independently-placed analysis blocks on one page is exactly the drift this project has repeatedly paid for
**And** the story states explicitly which epic landed first and what it inherited.

3.
**Given** a link to the external analysis tool is configured
**When** the page renders
**Then** the link targets that tool's page for **this file**, and when unconfigured the link is simply absent rather than broken or placeholder (NFR12).

4.
**Given** a file is absent from the report, or no report is configured
**When** the page renders
**Then** it shows a **designed empty state** distinguishing "not measured" from "0% covered" (UX-DR22), never a bare 0 or a blank space.

### Story 27.5: Coverage on the Treemap and Hierarchy Surfaces

As a developer surveying a codebase,
I want to see coverage across the whole file tree at once,
So that I can find poorly-tested areas without opening files one at a time.

**Acceptance Criteria:**

1.
**Given** Story 27.1's chosen encoding and Story 27.3's line-weighted rollups
**When** the Code Map treemap and the Hierarchy Explorer render with coverage available
**Then** coverage is encoded per the owner's selected direction at both file and directory level, and the encoding is **never color-alone** (UX-DR17)
**And** an **unknown**-coverage node is visually distinct from a low-coverage one.

2.
**Given** ADR 0013 makes the server-rendered text twin the no-JS contract for hierarchy surfaces
**When** coverage is added to a hierarchy surface
**Then** the twin carries the coverage figures too — a twin that omits the new signal silently breaks the contract that made the SVG retirement acceptable
**And** the per-node byte cost of the added coverage data is **measured** against Story 20.7's budget, not assumed negligible; Story 20.5 already found the twin cost ~180 B/node that its own spike never modelled.

3.
**Given** the hierarchy surfaces already encode weight and status
**When** coverage is added
**Then** it honors UX-DR21's one-primary-representation rule per Story 27.1's decision, and the existing status encoding is not silently displaced without that being the recorded choice.

### Story 27.6: Coverage × Churn — Finding Code That Changes Often and Is Poorly Tested

As a maintainer deciding where to spend testing effort,
I want to see which files combine high change frequency with low coverage,
So that I can target the code most likely to break rather than chasing a global percentage.

**Acceptance Criteria:**

1.
**Given** `--deep-git` already computes per-file churn and hotspots, and Story 27.3 provides coverage
**When** both signals are available
**Then** a surface ranks files by the combination — high churn with low coverage first — and the ranking rule is **stated and defensible**, not an unexplained composite score
**And** it reuses the existing deep-git metric path (`GitMetrics.TryComputeDeep`) rather than adding a second git traversal.

2.
**Given** this project's charting conventions
**When** the surface renders
**Then** it carries an accessible text equivalent and conveys risk by more than color, and degrades to absent-not-broken when either signal is missing — coverage without `--deep-git`, or churn without a coverage report.

3.
**Given** per-line coverage marks were explicitly scoped OUT of this epic
**When** this story is planned
**Then** it records whether the analytics work changed the case for per-line marks, so that decision is revisited **on evidence** rather than silently dropped or silently expanded.

## Epic 28: Text-Twin & JS-Off Standardization — One Proven Pattern, Then a Verified Rollout

Prove a single, standardized, reusable text-twin pattern on one representative hierarchy/graph surface, verify it live (JavaScript disabled, keyboard, screen reader), then broaden it to every remaining surface as its own scoped rollout — the same spike → component → site-wide-rollout shape Epic 20 used to standardize the chart engine itself, applied this time to the accessibility contract ADR 0013 made mandatory.

This epic exists because ADR 0013's hard per-surface gate ("no SVG retires until its twin is audited complete") had no durable owner once Epic 20 closed: ADR 0030 named the gap directly (no Epic 24 story owns `Charts.ReferenceGraph`'s text-twin audit), and the Epic 20 retrospective found the same gate pulling ordinary feature stories toward accessibility-audit breadth instead of core-UX depth while the project has exactly one confirmed consumer. [ADR 0031](../../docs/adrs/0031-text-twin-standardization-moves-to-its-own-epic.md) seats this epic and retires the per-story gate for new work; NFR-5's wording is unchanged.

**FRs covered:** none new · **NFRs:** NFR-5 (as amended by ADR 0013; delivery sequencing changed by ADR 0031, not the requirement) · **UX-DRs:** UX-DR17, UX-DR19, UX-DR21 · **Design-locked by:** [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (the contract) and [ADR 0031](../../docs/adrs/0031-text-twin-standardization-moves-to-its-own-epic.md) (the epic seat + gate removal) · **Status:** backlog · unscheduled · no stories yet · **Depends on:** Epic 20 (the Hierarchy Explorer component and Story 20.6/20.9's partial audit — dashboard, story detail, Code Map, Impact Map, and Git Insights already have twins and are not in scope for re-work); resolves the open item ADR 0030 named for Epic 24's `Charts.ReferenceGraph`.

<!-- Epic 28 seated 2026-07-29 (Epic 20 retrospective, owner-ratified via ADR 0031). No stories written yet —
     story breakdown (which surface proves the pattern first, what "standardized" means concretely, how the
     rollout is sequenced) is deferred to its own create-epics-and-stories / create-story pass, not decided here.
     Known already-audited surfaces per Story 20.6/20.9: dashboard, story detail, Code Map, Impact Map, Git
     Insights. Known gaps at seed time: epics index, epic detail (both FAIL in Story 20.6's audit, unaddressed
     since); Epic 24's force-directed graph views (ADR 0030's named gap); any surface added after 2026-07-29
     without an audited twin should be recorded here as debt owed to this epic, per ADR 0031 Decision 4. -->

