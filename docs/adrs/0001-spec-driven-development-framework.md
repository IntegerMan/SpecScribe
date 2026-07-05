# ADR 0001: Adopt BMAD-METHOD as SpecScribe's Spec-Driven Development Framework

**Status:** Accepted
**Date:** 2026-07-05
**Deciders:** Matt Eland

## Context

SpecScribe is a tool that renders spec-driven-development (SDD) artifacts — PRDs, GDDs,
epics, stories, requirements inventories, ADRs, and mermaid diagrams — into a styled,
navigable, cross-linked static HTML site. Its whole reason to exist is that SDD frameworks
produce a wealth of markdown written for AI agents and power users, not for humans skimming
project status.

To grow SpecScribe deliberately rather than by ad-hoc feature accretion, the project needs
its *own* SDD framework: one methodology used to plan and drive SpecScribe's future
development, so the tool's own requirements, epics, stories, and decisions live as durable,
reviewable artifacts. Three candidates were considered:
[BMAD-METHOD](https://github.com/bmad-code-org/BMAD-METHOD),
[GitHub Spec Kit](https://github.com/github/spec-kit), and
[GSD-Pi](https://github.com/open-gsd/gsd-pi).

This decision is about **which framework SpecScribe adopts to develop itself (dogfooding)**.
It is explicitly **not** a decision about which frameworks SpecScribe supports as *input*:
the roadmap already commits to rendering BMad, Spec Kit, and GSD artifacts alike, and that
work proceeds independently of this ADR.

## Decision

Adopt **BMAD-METHOD** as the spec-driven development framework for planning and building
SpecScribe.

The dogfooding argument is decisive: SpecScribe already parses BMad artifacts end-to-end
(`EpicsParser`, `RequirementsParser`, `_bmad-output/` discovery in `ForgeOptions`, and ADR
handling in `SiteGenerator`). Authoring SpecScribe's own planning documents in BMad means
the tool renders its own repository *today* — turning the project into a live, self-hosted
demonstration of exactly what it does for everyone else. Choosing any other framework would
require building that framework's renderer *before* we could self-render at all, a
chicken-and-egg problem that stalls dogfooding at the starting line.

## Considered Options

### BMAD-METHOD (chosen)

An open-source agentic framework with specialized agents (Analyst, PM, Architect, Scrum
Master, Dev) that produce PRDs, architecture documents, epics, and stories, then shard those
plans into focused development units.

- **Pros:** Already fully renderable by SpecScribe, so dogfooding is immediate. Produces the
  richest artifact set (PRD/GDD, epics, stories, requirements traceability, ADRs, mermaid),
  which exercises the broadest slice of SpecScribe's rendering surface. Mature, agile,
  multi-epic structure suited to a project meant to grow.
- **Cons:** More process ceremony than a solo/small project strictly needs; the full
  agent-driven planning flow can be heavyweight for small changes.

### GitHub Spec Kit

A GitHub-backed toolkit built around a lightweight `constitution → specify → plan → tasks`
command flow that layers onto coding agents such as Copilot, Claude Code, and Codex.

- **Pros:** Lightweight and approachable; backed by GitHub; the `constitution.md` concept is
  a clean way to capture non-negotiable project principles.
- **Cons:** Thinner artifact set than BMad, so it exercises less of SpecScribe's renderer. Not
  yet parseable by SpecScribe — dogfooding would be blocked until that support is built.

### GSD-Pi

A local-first, terminal-driven system for autonomous, long-running agent sessions that
breaks work into milestones, slices, and tasks and stores project memory under `.gsd/`.

- **Pros:** Strong for long autonomous agent runs and worktree-aware Git automation; local
  project memory keeps requirements, decisions, and validation evidence together.
- **Cons:** Newest and still consolidating (migrated from `gsd-2` to `gsd-pi`). Its `.gsd/`
  artifacts are not yet renderable by SpecScribe, so it too blocks immediate dogfooding.

## Consequences

**Positive**

- SpecScribe renders its own project docs from day one — an immediate, zero-extra-work live
  demo running on the tool's own repository.
- Dogfooding BMad exercises the widest range of SpecScribe's rendering features, so real
  self-usage continuously surfaces gaps and drives coverage.
- The project inherits a mature, structured agile methodology for planning its roadmap.

**Negative / trade-offs**

- BMad's ceremony is heavier than Spec Kit's lightweight flow; we accept that overhead in
  exchange for the richer artifacts and immediate self-rendering.
- Choosing BMad as the *development* methodology does not discharge the roadmap commitment to
  render Spec Kit and GSD-Pi artifacts as *inputs*; those parsers still need to be built.
- This choice should be revisited once SpecScribe's Spec Kit and GSD-Pi renderers land — at
  that point the dogfooding advantage that decided this ADR would apply to any of the three.

## References

- [BMAD-METHOD](https://github.com/bmad-code-org/BMAD-METHOD)
- [GitHub Spec Kit](https://github.com/github/spec-kit)
- [GSD-Pi](https://github.com/open-gsd/gsd-pi)
