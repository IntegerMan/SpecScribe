# SpecScribe User Journeys

Date: 2026-07-09
Source: page-by-page review of the live portal at https://integerman.github.io/SpecScribe/

SpecScribe turns spec-driven-development artifacts (BMAD-style PRDs, epics, stories, sprint status, git history) into a static, human-readable portal. The portal's value is answered questions: every page should exist because it answers one of the questions below faster than opening the raw markdown. These journeys are the lens for the UX feedback in [Epic3UXFeedback.md](Epic3UXFeedback.md).

## Personas

- **The Driver** — the solo developer / tech lead running spec-driven development with AI agents. Owns the repo, runs the `/bmad-*` commands, and uses the portal as mission control between agent sessions. Today this is the primary (often only) user.
- **The Stakeholder** — a manager, client, or teammate who does not run the tooling. Gets a link to the published portal and wants progress, scope, and risk without learning BMAD vocabulary.
- **The New Contributor** — a developer (human or AI agent) joining mid-project. Needs to absorb vision, architecture, conventions, and "what's in flight" before touching code.
- **The Reviewer** — anyone validating that a story claiming "done" actually satisfies its acceptance criteria; often the Driver wearing a different hat, sometimes a peer.

## Journey 1: "Where does the project stand right now?" (daily pulse)

**Persona:** Driver (daily), Stakeholder (weekly).
**Entry:** Home dashboard.
**Path:** Home → Now & Next sprint board → Progress by Epic → (optionally) Sprint page for the full board.

The Driver opens the portal at the start of a session to re-orient: what's done, what's in review, what's next. Success is a 30-second scan — counts, the sprint board, and the epic ring chart should agree with each other and with `sprint-status.yaml`. Anything requiring the user to reconcile two widgets that show the same data differently is a failure of this journey.

**Needs:** one authoritative status vocabulary; no contradictions between summary counts and detail views; "what changed since I last looked" signals (recency, last-updated).

## Journey 2: "What should I (or my agent) work on next?" (work selection)

**Persona:** Driver.
**Entry:** Home dashboard "Now & Next" board and "Next Steps" commands / Sprint page.
**Path:** Home or Sprint → next ready story → story page → copy the `/bmad-*` command (or Open in Cursor) → leave the portal.

This is the portal's most action-oriented journey: it ends with a command being pasted into an agent session. The story page must make readiness obvious (status, task plan present, dependencies met) and put the right next command in front of the user without hunting.

**Needs:** unambiguous "ready" signals; exactly one recommended next command per state; copy/deeplink affordances that work; stories lacking task plans clearly separated from actionable ones.

## Journey 3: "Did this story actually get done right?" (review & verification)

**Persona:** Reviewer.
**Entry:** Story page for an in-review story (from sprint board or epic page).
**Path:** Story page → acceptance criteria → Dev Agent Record / completion notes → change log → `/bmad-code-review` command → back to sprint board.

The Reviewer reads the ACs, checks the dev record's claims against them, and either runs the adversarial review command or spot-checks code. Story pages are long by design (they are the full spec), so this journey depends on within-page navigation and clear visual separation between the *contract* (ACs) and the *claim* (dev record).

**Needs:** ACs visually distinct and easy to diff against completion notes; test counts and verification evidence surfaced near the top; on-page TOC on every long page; the review command explained, not just displayed.

## Journey 4: "Where did this requirement go?" (traceability)

**Persona:** Driver, Stakeholder, Reviewer.
**Entry:** Requirements page or a PRD cross-reference.
**Path:** PRD / Requirements index → FR/NFR detail page → covering epic(s) → stories → (future, Epic 7) code citations.

The chain PRD → requirement → epic → story → code is SpecScribe's core differentiator. Today the chain is strong at the top (requirements flow diagram, FR coverage map) and weak at the bottom: FR detail pages stop at the epic level, and code linkage arrives with Epic 7. The journey succeeds when a Stakeholder can click from "FR6" to the stories delivering it without reading an epics document.

**Needs:** story-level links on requirement pages; consistent status vocabulary between requirements ("Planned", "Partially implemented") and stories ("backlog", "ready for dev"); NFRs and UX design requirements traced with the same rigor as FRs.

## Journey 5: "Get me up to speed" (onboarding)

**Persona:** New Contributor, Stakeholder (first visit).
**Entry:** Home, usually via a shared link.
**Path:** Home → Readme → PRD (vision + requirements) → Architecture / ADRs → Epics → current sprint.

A first-time visitor doesn't know BMAD, doesn't know what FR/NFR/AC mean, and doesn't know which of the nine nav items to read first. The portal currently assumes fluency. Onboarding succeeds when the home page itself suggests a reading order and the vocabulary is either self-explanatory or defined in place.

**Needs:** a suggested reading order ("Explore Key Views" partially does this); acronyms expanded on first use or via tooltip/glossary; artifact pages that state what kind of document they are and why it matters.

## Journey 6: "Where is the risk in this codebase?" (health & hotspots)

**Persona:** Driver, Tech-lead Stakeholder.
**Entry:** Home Git Pulse → Git Insights hub; Deep Analytics when `--deep-git` was used.
**Path:** Home Git Pulse → git-insights (files, contributors, activity heatmap, day drilldowns) → deep-analytics (coupling, hotspots).

Git-derived insights answer "what files churn, what changes together, when was work done." The audience needs interpretation, not just data: a hotspot list matters because churn correlates with defects; a coupling pair matters when the files *shouldn't* be coupled. Charts without legends or framing leave the journey unfinished.

**Needs:** legends and time windows on every chart; one sentence of "why this matters" per section; these pages reachable from the top nav, not only via dashboard deep links.

## Journey 7: "What's hanging over us?" (debt & follow-ups)

**Persona:** Driver, especially at retro/planning time.
**Entry:** Home "Direct & Quick-Dev Work" section.
**Path:** Home → deferred-work / action-items pages → source retros → route items into backlog.

Deferred work and open retro action items are the project's memory of promises. The journey succeeds when each item shows where it came from, why it was deferred, and what resolving it looks like — and when resolved items visibly leave the list.

**Needs:** provenance and resolution criteria per item; a path from action item to the story/spec that resolves it; counts on the dashboard that match the detail pages.

## Journey priorities

Journeys 1 and 2 run daily and should win any design conflict. Journey 3 runs per story. Journey 4 is the differentiator that justifies SpecScribe over a plain wiki. Journeys 5–7 are periodic but decide whether anyone beyond the Driver ever adopts the portal.
