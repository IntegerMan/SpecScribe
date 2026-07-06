---
title: Product Brief - SpecScribe
status: draft
created: 2026-07-05
updated: 2026-07-05
---

# Product Brief: SpecScribe

## Executive Summary

SpecScribe is an open-source utility that converts the output of spec-driven development workflows into a human-readable project portal. It can run once or watch artifact folders and continuously regenerate a linked HTML site, helping teams quickly understand delivery progress, decision history, and implementation state without manually stitching together many markdown files.

Today, SpecScribe is strongest on BMad Game Development Studio artifacts and includes lightweight git-derived insights. The next product phase expands parser and rendering support to additional ecosystems (Spec Kit, GSD/GSD-Pi, and related variants), making SpecScribe a framework-agnostic lens for spec-first teams.

Longer term, SpecScribe can offer the same information architecture inside a VS Code extension using webviews, giving users in-editor visibility into epics, stories, requirements, and ADR context while they build.

## The Problem

Spec-driven methods generate large volumes of markdown intended for AI agents and process-heavy contributors, not for fast human situational awareness. Teams can lose time answering simple questions:

- What is in flight versus blocked?
- Which requirements are covered by current stories?
- What architecture decisions have changed and why?
- Where is real progress happening across docs and code?

The raw artifacts are valuable, but they are fragmented across directories, inconsistent in style, and hard to skim. This makes status communication and decision continuity harder than it should be.

## The Solution

SpecScribe ingests artifact directories and produces a styled, cross-linked static site with dashboards and detail views tailored to development planning artifacts.

Core experience:

- Run once for a point-in-time snapshot, or run in watch mode for continuous regeneration.
- Parse framework-specific structures into consistent navigation and status views.
- Cross-link stories, requirements, ADRs, and source references for traceability.
- Render markdown details (including mermaid and task checklists) in a readable format.
- Surface lightweight git activity context next to artifact progress.

## Product Scope And Phasing

### Phase 1 (Current Foundation)

- BMad GDS ingestion and rendering.
- Epic/story pages with progress and status extraction.
- Requirements and ADR rendering.
- Mermaid and checklist support.
- Basic git pulse/commit-derived metrics.

### Phase 2 (Framework Expansion)

- Spec Kit parser and page model support.
- GSD/GSD-Pi parser and page model support.
- Shared abstraction layer so multiple framework adapters map into a common UI model.
- Improved robustness around mixed-framework repositories.

### Phase 3 (In-Editor Experience)

- [ASSUMPTION] VS Code extension shell with webview-hosted dashboard.
- [ASSUMPTION] Side-panel navigation for epics/stories/requirements/ADRs.
- [ASSUMPTION] Click-through from editor context to relevant rendered records.
- [ASSUMPTION] Optional local generation pipeline reused from CLI core.

## Who This Serves

Primary users:

- Solo builders and small teams using spec-driven workflows who need fast context while shipping.
- Tech leads and product leads who need readable planning-state summaries.
- Contributors onboarding onto a repo with existing planning artifacts.

Secondary users:

- Open-source evaluators reviewing project maturity.
- Stakeholders who need progress visibility but do not want to parse raw spec files.

## What Makes This Different

- Dogfooding advantage: the project renders the same class of artifacts it is built with, accelerating quality feedback.
- Workflow-native framing: focuses on preserving relationships between epics, stories, requirements, and ADRs, not just markdown-to-HTML conversion.
- Live-watch mode: continuously updated project portal without manual republishing.
- Tooling portability: same core value can exist as standalone HTML output and potentially inside the editor.

## Success Criteria

Near-term (product quality):

- Framework support breadth: successful rendering of at least BMad, Spec Kit, and GSD/GSD-Pi baseline artifacts.
- Traceability quality: requirement, story, and ADR cross-links resolve without broken links in generated output.
- Reliability: watch mode recovers cleanly from rapid file edits and partial writes.

Adoption and utility:

- [ASSUMPTION] Active usage across multiple real repositories beyond the core repo.
- [ASSUMPTION] Reduced time-to-answer for common status questions during team check-ins.
- [ASSUMPTION] Positive maintainer feedback on readability and onboarding value.

## Risks And Open Questions

- Adapter complexity risk: framework divergence may push parser maintenance costs up.
- Semantics risk: similar artifacts across frameworks may encode status differently.
- UX scope risk: extension development could dilute focus if attempted before parser abstractions are stable.
- Open question: what minimum parity level defines "supported" for each framework?
- Open question: should VS Code extension ship read-only first, or include configuration/generation controls immediately?

## Strategic Vision

SpecScribe becomes the default readability layer for spec-driven development: one consistent, trustworthy interface across planning frameworks, available both as generated project docs and in-editor context. If successful, teams spend less energy reconstructing state from raw artifacts and more energy making decisions and shipping.
