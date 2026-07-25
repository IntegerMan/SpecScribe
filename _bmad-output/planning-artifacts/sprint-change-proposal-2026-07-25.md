---
title: "Sprint Change Proposal — SonarCloud Continuous Analysis + Optional External-Analysis Insights"
date: 2026-07-25
workflow: correct-course
mode: batch
author: Matthew-Hope Eland (owner-directed)
status: proposed
scope_classification: Moderate (backlog reorganization — two new epics, eleven new stories, two amended stories)
---

# Sprint Change Proposal — 2026-07-25

## 1. Issue Summary

### 1.1 Trigger

Owner-directed capability request (not a defect, not a discovered technical limitation). The owner asked for
**five distinct threads** of work to be seated:

1. An epic for integrating **SonarCloud into this project's CI**, so every commit on `main` is auto-analyzed and
   the analysis results are actually **scanned** (acted on), not merely displayed on a third-party dashboard.
2. **Spike + implementation stories** for how that analysis data reaches **AI agents working in spec-driven-development
   frameworks**.
3. An **ideation story** for how Sonar could *optionally* integrate into SpecScribe the product.
4. A **spike** for that product integration, a **configuration story**, and **stories for the integration points**.
5. A story for **investigating future service integration points** generally.

Governing constraint, in the owner's words: **"Sonar should be optional in the tool but useful for developing the tool."**

### 1.2 Owner clarifications captured at elicitation

Four decisions were locked before analysis:

- **D1 — Two epics, not one.** The dev-time half (SpecScribe's own CI) and the product half (optional Sonar as a
  SpecScribe capability) become **Epic 25** and **Epic 26** respectively. They have different requirement coverage,
  different gates, and different urgency; Epic 25 is wanted now, Epic 26 can sit in backlog.
- **D2 — The "agent data" thread is mostly INBOUND, and it is visual.** The owner's actual want is to
  *"visualize analysis findings alongside code, epics, stories, directories, or requirements."* Additionally, the owner
  selected the **framework-neutral contract** reading: the mechanism should not be Sonar-shaped or BMad-shaped.
  The owner also noted **"we could potentially fold in code analysis warnings as well, but that gets to be language
  dependent"** — compiler/analyzer warnings as a *second source class*.
- **D3 — The data-source posture is the spike's to decide,** with a ratified ADR either way. The owner did **not**
  pre-decide SonarCloud-web-API vs on-disk-export.
- **D4 — Batch review mode.**

### 1.3 What D2 actually changes about the shape of this proposal

D2 is load-bearing and reframes thread 2. "Provide analysis data to AI agents" read on its face as a *tooling* concern
(an MCP server, a digest file). The owner's clarification makes it a **data-model** concern: findings must attach to
**entities SpecScribe already models** — files, directories, epics, stories, requirements — which is precisely the
entity set Epic 26's "integration points" stories would surface anyway.

The consequence: the two threads **share one contract**. The framework-neutral findings model that Epic 25's spike
defines for agents is the same model Epic 26's surfaces render for humans. This proposal therefore places the
**contract** in Epic 25 (Story 25.3, spike + ADR) and the **human-visual surfaces** in Epic 26, with an explicit
cross-epic dependency rather than two independently-invented models. The "language dependent" caveat is handled by
making the model **source-agnostic from the first line** (Sonar is instance #1, not the schema), and by scoping
additional source classes to Story 26.7.

### 1.4 Evidence and current-state baseline

| Fact | Evidence |
|---|---|
| The repository has **no build/test CI at all** today | `.github/workflows/` contains exactly one file, `publish-docs-live-pages.yml` (a Pages publish workflow). No build, no test, no analysis. |
| The story that *would* provide it is unstarted and scheduled **last** | `16-2-continuous-integration-build-and-test-gate: backlog` (`sprint-status.yaml`). Epic 16 runs at the very end of the roadmap, after Epic 17's hardening sign-off (epics.md Overview delivery-sequence note). |
| The repo is **public and GitHub-hosted** | `git remote -v` → `git@github.com:IntegerMan/SpecScribe.git`. SonarCloud's free OSS tier and GitHub App integration path both apply; no private-source-exposure question for *this* repo. |
| SpecScribe already models every entity the owner wants findings attached to | `FileInsight` (`src/SpecScribe/GitMetrics.cs:169`), `CodeFileTemplater` (`src/SpecScribe/CodeFileTemplater.cs:18`), `PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`, the shipped story↔file miner from Story 21.3), the Epic 19 work graph, and the Epic 21 traceability matrix. **The attach points exist; nothing new must be modelled to hang findings on them.** |
| Configuration parity has a shipped seam | `SettingsResolver` (`src/SpecScribe/SettingsResolver.cs:63`) from Story 5.2 — three-way provenance, `--show-config`, `.specscribe` persistence, walk-up discovery. |
| This would be SpecScribe's first **outbound network** capability | PRD NFR-3: *"Repository and artifact analysis runs locally by default; no remote telemetry is required for core operation."* PRD §5 Non-Goals: *"Building a hosted SaaS with account management and remote data processing in v1."* |

---

## 2. Change Analysis Checklist

Executed per `.claude/skills/bmad-correct-course/checklist.md`.

### Section 1 — Understand the Trigger and Context

| ID | Item | Status | Finding |
|---|---|---|---|
| 1.1 | Triggering story | **[N/A]** | No triggering story. Owner-directed capability request, mid-Epic-20/24 execution. |
| 1.2 | Core problem, categorized | **[x]** | **New requirement emerged from stakeholder (owner).** Two problems, one request: (a) SpecScribe has *no* automated quality analysis of its own code and none is scheduled until the last epic; (b) SpecScribe surfaces git-derived and planning-derived signal but has no channel for **external analysis signal**, and the owner wants that signal visible against code, directories, and planning entities alike. |
| 1.3 | Evidence | **[x]** | Table in §1.4. Notably: zero build/test CI exists today, and the only story that would add it sits `backlog` behind the entire roadmap. |

### Section 2 — Epic Impact Assessment

| ID | Item | Status | Finding |
|---|---|---|---|
| 2.1 | Current epic still completable? | **[x]** | Yes. Epics 20 (Hierarchy Explorer, `in-progress`), 24 (Coupling, `in-progress`), 5 (CLI, `in-progress`) are **untouched**. Nothing in this change alters an in-flight story's scope, ACs, or gates. |
| 2.2 | Epic-level changes needed | **[x]** | **Add two epics** (25, 26). No epic removed, redefined, or renumbered — the append-only / no-renumber convention holds. |
| 2.3 | Remaining epics reviewed for impact | **[!]** | **Three real impacts found.** (a) **Story 16.2** — a genuine duplication collision, see §2.a below. (b) **Story 17.2 / NFR10** — SonarCloud adds a third-party CI service and a scanner to the supply chain; hardening scope must name it. (c) **Epic 22 (JSON IR)** — if findings become a first-class portal entity, the canonical IR must carry them; noted for Story 22.2, no restructure needed now. |
| 2.4 | Epics invalidated or newly needed? | **[x]** | None invalidated. Two new epics needed — a Sonar surface is a coherent multi-story capability, matching how Epics 19/20/21/24 were each seated. |
| 2.5 | Order/priority changes | **[!]** | **Yes — one deliberate pull-forward.** Epic 25 pulls the CI build+test foundation ahead of Epic 16, because Sonar analysis has nothing to attach to without it and the owner wants it useful *during* development. Resolved by amending Story 16.2 rather than duplicating it. |

#### §2.a — The Story 16.2 collision (the one genuine conflict in this change)

Story 16.2 ("Continuous Integration Build & Test Gate") is `backlog` and owns *"restores, builds, and executes the
`tests/SpecScribe.Tests` suite on a clean checkout"*. Epic 25's analysis workflow needs exactly that — a scanner
without a build produces nothing useful for a C# project, and SonarCloud's C# analysis requires the build to run
*under* the scanner's begin/end wrapper.

Left unaddressed, this produces **two CI workflows that both build and test**, which is the drift class this project
has repeatedly paid for. The resolution: **Story 25.1 stands up the build+test+analyze workflow now**, and
**Story 16.2 is amended** from "create the gate" to "harden the existing Epic 25 workflow into a release-relevant
required gate." FR/NFR coverage is unchanged (16.2 still covers NFR9); only its starting assumption moves.

### Section 3 — Artifact Conflict and Impact Analysis

| ID | Item | Status | Finding |
|---|---|---|---|
| 3.1 | PRD conflicts | **[!]** | **One real crossing, deliberately not resolved in this SCP.** If Story 26.2's spike selects the SonarCloud **web API**, SpecScribe makes its first outbound network call, which sits against **NFR-3** ("analysis runs locally by default; no remote telemetry is required for core operation") and brushes the §5 Non-Goal on "remote data processing." Note NFR-3's own wording — *"by default"* and *"required for core operation"* — already accommodates an **opt-in, non-required** integration; an amendment may prove to be a clarification rather than a concession. **That determination belongs to 26.2's ADR, not to this proposal.** The new NFR12 (§4.2) encodes the guardrails either way. MVP scope is **not** affected — both new epics are post-MVP capability. |
| 3.2 | Architecture conflicts | **[x]** | **No conflict — a clean fit.** **AD-4** ("optional insight providers may enrich output but never own baseline success") is the exact contract an external-analysis provider needs, and NFR12 is AD-4 restated for a *networked* provider. **AD-3** (settings resolve once from directory scope + run overrides) governs Story 26.3. **AD-1/AD-2** are respected: findings enter the shared projection and reach every surface through host-neutral view models — no HTML-only or webview-only findings path. **ADR 0003** (directory-scoped settings) covers credential *location*; the credential-handling design is new and 26.2 owns it. |
| 3.3 | UI/UX conflicts | **[!]** | No conflict, but **three standing contracts bind every Epic 26 surface** and are written into its ACs: **UX-DR17 / NFR8** (severity must never be signalled by color alone — a Blocker/Critical/Major scale is exactly the trap), **ADR 0013 §3** (any chart carries a server-rendered text twin, gated by live-browser JS-off verification), and **Story 10.2 / FR28** (real-value legend + analysis window + one framing sentence per chart). Story 26.1's ideation round is where the owner sets visual direction up front, per the `create-story must elicit visual intent` convention. |
| 3.4 | Other artifacts | **[!]** | **CI/CD** — first build/test workflow in the repo (§2.a). **Testing strategy** — Story 25.1 must decide coverage-report generation and upload; SpecScribe's suite is ~2,354 tests, and a coverage collector changes test-run time. **Security/supply chain** — Story 17.2's audit scope grows. **Documentation** — Story 16.6 gains the Sonar configuration surface; README option tables per PRD §12.3. **Golden fingerprint** — every Epic 26 surface story moves it; expect concurrent-session drift per CLAUDE.md. |

### Section 4 — Path Forward Evaluation

| ID | Option | Verdict | Assessment |
|---|---|---|---|
| 4.1 | **Direct Adjustment** — new epics + stories in the existing plan | **Viable** | Effort **Medium**, risk **Low**. Purely additive: two epics, eleven stories, two amended stories. Nothing in flight is disturbed. Matches the established pattern (Epics 19/20/21/24 were all seated exactly this way). |
| 4.2 | **Rollback** | **Not viable** | Nothing to roll back. No completed work is invalidated by this change. |
| 4.3 | **PRD MVP Review** | **Not viable / not needed** | MVP (PRD §6.1) is BMad-first portal, CLI generate/watch, traceability, git pulse, agent-file insights. Both new epics are post-MVP capability, mirroring Epics 19–24. Reducing MVP would not serve this request. |
| 4.4 | **Selected path** | **Option 1 — Direct Adjustment** | Rationale: the request is additive capability, not a correction. The one genuine conflict (§2.a) is resolved by amending a single unstarted `backlog` story. Both architecturally interesting decisions (agent-facing findings contract; external-service ingestion posture + the NFR-3 question) are **routed to spikes that land ratified ADRs** — satisfying CLAUDE.md § Decision records rather than burying an architecture decision inside an implementation story's dev pass. This is the same correction Story 24.6 was created to make three days ago; applying it *before* the first line of code is the intended use. |

### Section 5 — Proposal Components

| ID | Item | Status |
|---|---|---|
| 5.1 | Issue summary | **[x]** §1 |
| 5.2 | Epic + artifact impact documented | **[x]** §2, §3 |
| 5.3 | Recommended path + rationale | **[x]** §4.4 |
| 5.4 | MVP impact + action plan | **[x]** MVP unaffected (§4.3); action plan §5 |
| 5.5 | Agent handoff plan | **[x]** §6 |

### Section 6 — Final Review and Handoff

| ID | Item | Status |
|---|---|---|
| 6.1 | Checklist complete | **[x]** All sections addressed; four `[!]` items each carry a named resolution. |
| 6.2 | Proposal accuracy verified | **[x]** Every cited symbol grep-verified against `src/` per CLAUDE.md; every cited story status read from `sprint-status.yaml`. |
| 6.3 | Owner approval | **[ ]** Pending — this document. |
| 6.4 | `sprint-status.yaml` updated | **[ ]** On approval — §4.5. |
| 6.5 | Next steps confirmed | **[ ]** On approval — §6. |

---

## 3. Recommended Approach

**Option 1 — Direct Adjustment.** Append **Epic 25** (dev-time SonarCloud, four stories) and **Epic 26** (optional
external-analysis insights in the product, seven stories) using the project's append-only / no-renumber convention.
Amend two existing stories (16.2, 17.2). Seat one FR and two NFRs. Route both architectural decisions to spikes that
land ratified ADRs.

- **Effort:** Medium — 11 new stories, 2 amended, 2 spike-owned ADRs. Epic 25 is small and immediately valuable.
- **Risk:** Low for Epic 25 (no product code; worst case a CI workflow is reverted). Medium for Epic 26 — it carries
  SpecScribe's first outbound network capability and first credential handling, both gated behind a spike + ADR.
- **Timeline:** No impact on Epics 20/24/5. Epic 25 is schedulable immediately with no gates. Epic 26 is
  backlog/unscheduled; its Story 26.2 should not start before Story 25.3's contract exists.

**Sequencing (numbers are stable IDs, not run order — house convention, cf. Epic 23's 23.2→23.3→23.5→23.4):**

```
Epic 25:  25.1 → 25.2 → 25.3 (spike, ADR) → 25.4
                             │
                             └── contract feeds ──┐
                                                  ▼
Epic 26:  26.1 (ideation) → 26.2 (spike, ADR) → 26.3 (config) → 26.4 / 26.5 / 26.6
          26.7 (investigation) — independent, schedulable any time
```

---

## 4. Detailed Change Proposals

### 4.1 `epics.md` — Requirements Inventory additions

**Functional Requirements** — append after FR36:

```
FR41: Optionally ingest external code-analysis findings (SonarCloud first) from a configured source and surface them
      alongside the entities SpecScribe already models — code files, directories, epics, stories, and requirements —
      through one source-agnostic findings model, so a project's quality signal is readable in the same place as its
      delivery signal. The integration is disabled by default and every surface degrades to absent-not-broken when it
      is unconfigured, disabled, or unavailable.
```

**NonFunctional Requirements** — append after NFR10:

```
NFR11: SpecScribe's own codebase is continuously analyzed by an automated code-quality service on every push to the
       default branch and on pull requests, with the analysis attached to a reproducible clean-checkout build+test
       run, and with findings triaged into the project's own backlog rather than only viewed on an external
       dashboard.

NFR12: External-service integrations are opt-in, offline-safe, and credential-safe: disabled by default, never
       required for baseline generation, generation succeeds unchanged when the service is unreachable or
       unconfigured, and no secret, token, or credential value is ever written into generated output or into a
       directory-scoped settings file that is committed.
```

**Provenance comment** to follow them:

```
<!-- FR41 + NFR11–NFR12 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed): FR41 seats Epic 26
     (optional external code-analysis insights in the portal, Sonar first); NFR11 seats Epic 25 (SonarCloud on
     SpecScribe's own CI); NFR12 is the cross-cutting opt-in/offline-safe/credential-safe posture every external
     service integration must honor — AD-4 restated for a NETWORKED provider. Sync FR41 into the PRD when convenient
     (same treatment as FR37–FR40).
     ⚠️ These NFRs are appended to THIS list only. The PRD § 8 NFR list is numbered independently and already
     disagrees with this one (see the NUMBERING COLLISION note above NFR7). This SCP deliberately does NOT resolve
     that collision — it remains its own open item, now with two more entries riding on the unresolved numbering. -->
```

**FR Coverage Map** — append:

```
FR41: Epic 26 - Optional external code-analysis findings surfaced against code, directories, and planning entities.
NFR11: Epic 25 - Continuous SonarCloud analysis of SpecScribe's own codebase on every push to main.
NFR12: Epic 26 - Opt-in, offline-safe, credential-safe posture for external-service integrations.
```

---

### 4.2 `epics.md` — Epic List entries

```markdown
### Epic 25: Continuous Code-Quality Analysis for SpecScribe's Own Development (SonarCloud)
Put SpecScribe's own codebase under continuous automated analysis: every push to `main` and every pull request builds,
tests, and is analyzed by SonarCloud on a clean checkout, with a quality gate that fails loudly and findings that are
**triaged into this project's own backlog** rather than left on an external dashboard. Also defines — via a spike and
one implementation — the **framework-neutral contract** by which analysis findings reach AI agents doing spec-driven
development work, which Epic 26's human-facing surfaces then reuse. Dev-time only: **ships no product code.**
**NFRs covered:** NFR11 · **Status:** backlog · **Note:** pulls the CI build+test foundation ahead of Story 16.2,
which is amended to extend this workflow rather than create a second one.

### Epic 26: Optional External Code-Analysis Insights — Findings Alongside Code, Directories, and Planning
Make external code-quality analysis an **optional insight provider** in SpecScribe (AD-4), so a user who has Sonar can
see findings rendered against the entities the portal already models — code files, directories, epics, stories, and
requirements — through **one source-agnostic findings model** that compiler/analyzer warnings and other services can
ride later. Led by an owner-elicited ideation round (26.1) and a decision-first spike (26.2) that settles the
ingestion posture, the credential design, and the NFR-3 local-first question with a ratified ADR before any surface
is built. Optional in the tool; disabled by default; every surface degrades to absent-not-broken.
**FRs covered:** FR41 · **NFRs:** NFR12, NFR8 · **UX-DRs:** UX-DR17, UX-DR21, UX-DR22 · **Status:** backlog ·
unscheduled · **Depends on:** Story 25.3 (the findings contract), Epic 7 (code pages), Story 21.3
(`PlanningCodeImpact`, the shipped story↔file miner), Story 5.2 (`SettingsResolver`).
```

Plus the provenance comment:

```
<!-- Epics 25–26 added 2026-07-25 (SCP 2026-07-25, correct-course, owner-directed). Split per owner decision D1:
     Epic 25 = "useful for developing the tool" (dev-time CI, no product code); Epic 26 = "optional in the tool"
     (product capability). Owner decision D2 reframed the AI-agent thread as INBOUND and VISUAL — findings attach to
     entities SpecScribe already models — so the framework-neutral contract lands ONCE in Story 25.3 and Epic 26's
     surfaces consume it, rather than two epics inventing two findings models. Owner decision D3 left the
     ingestion posture (SonarCloud web API vs on-disk export) and the NFR-3 crossing to Story 26.2's spike + ADR.
     The model is SOURCE-AGNOSTIC from the first line — the owner's "we could potentially fold in code analysis
     warnings as well, but that gets to be language dependent" is exactly why Sonar must be instance #1, not the
     schema; additional source classes are scoped to Story 26.7. Append-only, no renumber. -->
```

---

### 4.3 `epics.md` — New epic sections (full)

#### Epic 25 stories

**Story 25.1 — SonarCloud Onboarding and Automated Analysis on Every Push to `main`**

> Stands up SpecScribe's **first** build+test CI workflow. See the Story 16.2 amendment (§4.4) — 16.2 extends this
> workflow rather than creating a second one.

*As the SpecScribe maintainer, I want every push to `main` and every pull request to build, test, and be analyzed by
SonarCloud on a clean checkout, so that code-quality regressions surface automatically instead of being discovered
during a hardening epic months later.*

1. **Given** a push to `main` or a pull request **When** CI runs **Then** a workflow restores, builds, and executes
   `tests/SpecScribe.Tests` on a clean checkout, runs the SonarScanner for .NET wrapping that build (begin → build →
   test → end), and uploads results to a SonarCloud project bound to `IntegerMan/SpecScribe`
   **And** the job fails on any build or test failure, and the workflow is independent of and does not disturb
   `publish-docs-live-pages.yml`.
2. **Given** analysis requires a token **When** the workflow authenticates **Then** `SONAR_TOKEN` is read from a
   repository secret, no secret value is committed, and the workflow is safe on pull requests from forks (analysis
   is skipped or runs without the token rather than leaking it)
   **And** the SonarCloud project's visibility and the free-OSS-tier terms are recorded in the story record.
3. **Given** test coverage improves finding quality **When** the analysis runs **Then** the story records an explicit
   decision on coverage collection — collector, report format, upload path, and the measured effect on suite runtime
   for a ~2,350-test suite — either implementing it or recording why it is deferred, never leaving it unstated.

**Story 25.2 — Quality Gate and Findings Triage into the Project Backlog**

*As the SpecScribe maintainer, I want the analysis results scanned and routed into this project's own backlog, so
that Sonar produces work items I actually act on rather than a dashboard I stop visiting.*

1. **Given** an analysis run completes **When** the quality gate evaluates **Then** a defined gate (new-code
   conditions at minimum) reports pass/fail as a visible signal on the pull request
   **And** the story records which conditions are enforcing vs advisory, and what a failing gate blocks.
2. **Given** findings accumulate **When** they are triaged **Then** a documented, repeatable triage pass routes each
   material finding to a decision — fixed, scheduled into a named story, or explicitly accepted with rationale — and
   lands in `deferred-work.md` / `sprint-status.yaml` action items using the existing FR30 provenance conventions
   **And** the initial baseline triage of the existing codebase is performed and its result recorded, so Epic 17's
   hardening pass inherits a known state rather than an unread dashboard.
3. **Given** findings overlap Epic 17's scope **When** triage runs **Then** items matching Stories 17.1–17.3
   (structural, security/privacy, performance) are tagged to those stories rather than duplicated
   **And** anything Sonar reports that the project deliberately does not follow is recorded as a rule-level decision,
   not silently re-triaged every run.

**Story 25.3 — SPIKE: A Framework-Neutral Findings Contract for AI Agents in SDD Workflows**

> Decision-first, timeboxed (~2d), **throwaway — no production code**. Durable deliverables: `25-3-spike-report.md`
> and a **ratified ADR**. This spike's contract is consumed by Story 25.4 **and** by all of Epic 26.

*As a maintainer whose planning agents should know what the analyzer knows, I want the shape of agent-consumable
analysis findings decided once — framework-neutral and source-agnostic — so that neither Epic 25's tooling nor Epic
26's surfaces invent a Sonar-shaped, BMad-shaped model that the other has to work around.*

1. **Given** the owner's framework-neutral requirement (NFR8) **When** the spike defines the contract **Then** it
   specifies a findings model keyed to **entities SpecScribe already projects** — file, directory, epic, story,
   requirement — carrying at minimum: source/provider, rule identity, severity **on a normalized scale with a text
   label** (never color-alone, UX-DR17), location, message, and provenance/analysis timestamp
   **And** it demonstrates the model holds for a **second, structurally different source class** — compiler/analyzer
   warnings, which the owner flagged as language-dependent — proving Sonar is instance #1 and not the schema.
2. **Given** findings must attach to planning entities, not just files **When** the spike defines attachment **Then**
   it specifies how a file-scoped finding reaches an epic/story/requirement, evaluating the **shipped**
   `PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`) commit/branch miner and the Epic 19 work graph as
   the join, and states honestly where the join is approximate or absent
   **And** it states what happens to findings that attach to **no** planning entity (the common case) so they are
   never silently dropped.
3. **Given** agents consume this in SDD workflows across frameworks **When** the spike evaluates delivery channels
   **Then** it compares — with a recommendation — at least: a generated agent-readable digest artifact, a field on
   the Epic 22 JSON IR, and an MCP-server surface; reporting for each the framework-neutrality (NFR8), the offline
   behavior, and whether it requires SpecScribe to gain a runtime it does not have
   **And** it states which channel Story 25.4 implements and what it defers.
4. **Given** CLAUDE.md § Decision records **When** the spike concludes **Then** it lands a **ratified ADR** recording
   the findings contract, its options table, and its consequences — this is a cross-cutting contract two epics bind
   to, and must not be settled inside an implementation story's dev pass
   **And** the report states explicitly what it hands to Story 25.4 and to Stories 26.2–26.6.

**Story 25.4 — Agent-Consumable Findings Channel for SpecScribe's Own SDD Workflow**

*As a maintainer running create-story and dev-story, I want the current analysis findings available to my agents in
the channel Story 25.3 selected, so that planning and implementation passes account for known quality debt in the
files they are about to touch.*

1. **Given** Story 25.3's ratified contract and selected channel **When** the channel is implemented **Then** current
   findings for this repository are emitted in the contracted shape and are demonstrably consumable by an agent during
   a real create-story or dev-story pass, with a worked example recorded
   **And** the implementation honors NFR12: it is opt-in, produces nothing rather than failing when findings are
   unavailable, and writes no token value anywhere.
2. **Given** this is dev-time tooling, not a product feature **When** it ships **Then** it does not alter SpecScribe's
   generated portal output — the golden fingerprint is unmoved — and any code added is quarantined from the
   generation critical path, with Epic 26 named as the story that makes findings a *product* surface
   **And** staleness is honest: consumers can tell how old the analysis is and when it predates the working tree.

#### Epic 26 stories

**Story 26.1 — IDEATION: Where Analysis Findings Belong in the Portal**

> Owner-elicited ideation, per the project's `create-story must elicit visual intent` convention. Deliverable is a
> decision record naming the integration points and their visual direction — **no code**.

*As the owner, I want to decide deliberately where and how analysis findings should appear across the portal before
any surface is built, so that the integration-point stories start from named visual direction instead of discovering
it in a post-implementation revision round.*

1. **Given** the entity set the owner named — code, directories, epics, stories, requirements — **When** the ideation
   round runs **Then** it produces, for each candidate surface, a concrete proposal covering placement, density,
   empty state, and how severity reads **without color** (UX-DR17), with **2–3 named design directions** offered for
   every new visual surface and the owner's selection recorded
   **And** it names which candidates are **in** for Stories 26.4–26.6 and which are explicitly **out**, so the
   integration-point stories have a closed scope.
2. **Given** the portal already carries substantial insight surfacing **When** placement is chosen **Then** the
   record states where findings **reuse** an existing surface (code pages, code map, traceability matrix, dashboard
   strip) versus where a **new** page is justified, and applies UX-DR21 (one primary representation per dataset)
   **And** it states what a project **without** any analysis configured sees — the default case for every user.
3. **Given** the owner's "we could potentially fold in code analysis warnings as well" **When** scope is set **Then**
   the record states whether non-Sonar source classes are in scope for Epic 26's surfaces or deferred to Story 26.7,
   with the language-dependence trade-off recorded rather than left implicit.

**Story 26.2 — SPIKE: Ingestion Posture, Credential Design, and the NFR-3 Local-First Question**

> Decision-first, timeboxed (~2d), **throwaway — no production code**. Durable deliverables:
> `26-2-spike-report.md` and a **ratified ADR**. Gates Stories 26.3–26.6.

*As a maintainer about to give SpecScribe its first outbound network capability, I want the ingestion posture and
credential design decided on evidence with an ADR behind it, so that the local-first question is answered once, in
the open, rather than implied by whichever implementation story happens to land first.*

1. **Given** the owner deferred the posture to this spike (decision D3) **When** candidate sources are evaluated
   **Then** it reports, per candidate — **SonarCloud web API**, **on-disk scanner report/export**, and **both** — the
   data available, freshness, offline behavior, credential requirement, rate limits, and the failure mode when the
   source is missing or stale
   **And** it evaluates the on-disk path at its true cost, including whether a user without a SonarCloud account can
   get any value at all.
2. **Given** PRD **NFR-3** ("analysis runs locally by default; no remote telemetry is required for core operation")
   and the §5 Non-Goal on remote data processing **When** the spike assesses the crossing **Then** it states plainly
   whether the recommended posture **requires a PRD amendment** or is already accommodated by NFR-3's "by default" /
   "required for core operation" wording — and if an amendment is required, it drafts the exact replacement text with
   the prior wording and rationale preserved inline, following the ADR 0013 / NFR-5 precedent
   **And** it does **not** treat a real product concession as a reinterpretation.
3. **Given** any network posture needs a credential **When** the spike designs credential handling **Then** it
   specifies where the token lives (environment variable, directory-scoped `.specscribe` via `SettingsResolver`
   `src/SpecScribe/SettingsResolver.cs:63`, or external), proves no token value can reach generated output,
   `--show-config`, the diagnostics page, or a committed settings file, and states the private-repository posture
   **And** it names the supply-chain surface any new dependency adds, handing it to Story 17.2 (NFR10).
4. **Given** Story 25.3's contract and CLAUDE.md § Decision records **When** the spike concludes **Then** it lands a
   **ratified ADR** covering ingestion posture, credential design, and the AD-4 provider boundary, consuming Story
   25.3's findings model rather than defining a second one — and stating explicitly if it must amend it
   **And** the report states what it hands to Stories 26.3–26.6 and to Epic 22's IR schema (Story 22.2).

**Story 26.3 — Analysis Integration Configuration (CLI, Interactive, and Settings Parity)**

*As a user, I want to turn analysis integration on, point it at my project, and see where its configuration came
from, using the same mechanisms as every other SpecScribe option, so that it is not a special case I have to learn.*

1. **Given** NFR7 configurability parity and AD-3 **When** the integration is configured **Then** enablement and
   source configuration are available as CLI flags, in the interactive flow, and as directory-scoped `.specscribe`
   persistence — resolved once through `SettingsResolver` with three-way provenance visible in `--show-config`, per
   the Story 5.2 pattern
   **And** the README documents the options as a table with short descriptive text (PRD §12.3).
2. **Given** NFR12 and AD-4 **When** the integration is unconfigured, disabled, or the source is unreachable
   **Then** baseline generation completes unchanged and non-fatally with a clear diagnostic, findings surfaces are
   **absent rather than broken or misleadingly empty** (NFR8/UX-DR22), and default generation performance does not
   regress (NFR1)
   **And** **disabled is the default** — an existing user upgrading sees no behavior change and makes no network call.
3. **Given** credentials **When** configuration is surfaced **Then** no token value appears in `--show-config`, the
   diagnostics page, generated output, or any file the tool writes into the repository — pinned by a regression test
   **And** a misconfigured or expired credential produces an actionable message, never a stack trace or a silent
   empty surface.

**Story 26.4 — Findings on Code Pages and the Code Map (File and Directory Scope)**

*As a developer browsing a file in the portal, I want that file's analysis findings shown alongside its git and
coupling signal, so that quality context lives where I am already looking instead of in a separate tool.*

1. **Given** an ingested findings set and a code page **When** it renders **Then** the file's findings appear on the
   page in the direction Story 26.1 selected, each showing rule, normalized severity **as text as well as any color**
   (UX-DR17/NFR8), message, and line — deep-linking to the existing `#L{n}` code anchor — attaching through the
   `CodeFileTemplater` / `FileInsight` seam (`src/SpecScribe/CodeFileTemplater.cs:18`,
   `src/SpecScribe/GitMetrics.cs:169`) rather than a parallel code-page pipeline
   **And** a file with no findings shows a designed empty state, never a broken or misleading one.
2. **Given** the directory-scope surface (code map / treemap) **When** findings are surfaced there **Then**
   directory-level aggregation is rendered per Story 26.1's direction with a Story 10.2-compliant real-value legend,
   analysis window, and framing sentence
   **And** it honors the Hierarchy Explorer contract if it rides a hierarchy chart (ADR 0012) and carries a
   server-rendered text twin (ADR 0013 §3) verified JS-off in a live browser.
3. **Given** deterministic generation (FR31) **When** the surfaces render **Then** output is stable across repeated
   runs from the same inputs, and the golden fingerprint move is intentional and re-baselined with a stability check
   across two runs (CLAUDE.md).

**Story 26.5 — Findings on Planning Entities (Epics, Stories, Requirements)**

*As a stakeholder reading an epic or story, I want to see the quality findings in the code that work touched, so that
"done" carries quality context and not only a status badge.*

1. **Given** Story 25.3's attachment rule and the shipped `PlanningCodeImpact`
   (`src/SpecScribe/PlanningCodeImpact.cs:54`) **When** findings are attached to planning entities **Then** epic,
   story, and requirement pages surface the findings in the code their work touched, using the existing miner as the
   join — never a second, divergent story↔file mapping
   **And** the attachment's **approximateness is stated on the surface**, following the Story 21.2 cycle-time
   precedent, so an inferred link is never presented as a tracked fact.
2. **Given** many findings attach to no planning entity **When** the surfaces render **Then** unattached findings are
   reachable from the hub (Story 26.6) and are never silently dropped, and an entity with no attributable findings
   shows a designed empty state distinguishable from "analysis not configured" (UX-DR22/NFR8)
   **And** counts route through the existing single count source (FR21 / Story 8.3) rather than a new tally.
3. **Given** NFR8 framework-agnosticism **When** the surfaces render for a non-BMad project **Then** they degrade to
   absent rather than broken where the framework lacks the underlying artifact types, with any framework-specific
   vocabulary supplied through the Epic 4 adapter contract.

**Story 26.6 — Analysis Hub Page and Dashboard Signal**

*As a maintainer, I want one page that answers "what is the state of this project's code quality" and a compact
dashboard signal pointing at it, so that findings have a home and a 30-second summary.*

1. **Given** an ingested findings set **When** the hub renders **Then** a dedicated page presents the findings set in
   the direction Story 26.1 selected — reachable from the insight-pages nav (FR27) on the integration's own gate,
   mirroring the Git Insights hub pattern — with sortable/filterable access to every finding including those attached
   to no planning entity
   **And** every chart on it carries a Story 10.2 real-value legend, analysis window, and framing sentence, plus a
   text twin per ADR 0013 §3.
2. **Given** the dashboard's 30-second-pulse contract (Epic 8) **When** the signal renders **Then** a compact strip
   summarizes quality state and links to the hub, following the Story 21.1/21.2 dashboard-strip placement pattern,
   without displacing existing pulse content
   **And** it is absent — not empty — when the integration is disabled, which is the default.
3. **Given** analysis data has an age **When** any surface renders **Then** the analysis timestamp is shown using the
   portal-wide date token (UX-DR25) and stale analysis is marked honestly rather than presented as current
   **And** output remains generation-time deterministic (FR31).

**Story 26.7 — INVESTIGATION: Future External-Service Integration Points**

> Investigation, timeboxed, **no production code**. Deliverable: a written landscape + recommendation record.

*As the maintainer deciding what SpecScribe should connect to next, I want the broader external-signal landscape
surveyed once, so that the second and third integrations extend Story 26.2's provider boundary instead of each
inventing their own.*

1. **Given** Sonar as the first external provider **When** the investigation surveys the landscape **Then** it
   inventories candidate external signal sources — for example GitHub code scanning / Dependabot / Actions status,
   coverage services, dependency-vulnerability services, other quality platforms, and **local compiler/analyzer
   output** (the owner's language-dependent case) — recording for each the data available, auth requirement, offline
   behavior, and whether it fits Story 25.3's findings model unchanged
   **And** it explicitly separates candidates that fit the existing model from those that would require a new one.
2. **Given** NFR12 and AD-4 **When** the investigation assesses the provider boundary **Then** it states whether
   Story 26.2's ingestion design generalizes to a **pluggable external-signal provider seam**, or whether each service
   needs bespoke work, with a concrete recommendation and the ADR trigger named if a seam is warranted
   **And** it assesses the local-first and credential-sprawl cost of each additional integration honestly, including
   the case for stopping at one.
3. **Given** this is exploratory **When** it concludes **Then** it produces a **prioritized** recommendation of which
   integrations (if any) to seat as future stories, with a stated "none of these" option, feeding
   `deferred-work.md` / the epic backlog rather than auto-seating stories
   **And** it records what would have to be true for each candidate to become worth building.
```

---

### 4.4 `epics.md` — Amendments to existing stories

**Amendment 1 — Story 16.2** (Continuous Integration Build & Test Gate). Status `backlog`; not started.

> **OLD** (AC #1): *"**Given** a pull request or push to a release-relevant branch **When** CI runs **Then** it
> restores, builds, and executes the `tests/SpecScribe.Tests` suite on a clean checkout, and the job fails on any
> build or test failure."*
>
> **NEW** — prepend an amendment note; ACs then read against the existing workflow:
>
> ```
> <!-- AMENDED 2026-07-25 (SCP 2026-07-25, correct-course): Story 25.1 stands up SpecScribe's FIRST build+test CI
>      workflow, because SonarCloud analysis of a C# project must wrap a real build and the owner wants it useful
>      DURING development — while this story sits backlog behind the entire roadmap. This story therefore no longer
>      CREATES the gate; it HARDENS the Epic 25 workflow into a release-relevant required gate (branch protection,
>      required-check status, release-branch coverage, and any release-specific matrix). Do NOT create a second
>      build+test workflow — two workflows that both build and test is the exact drift class this project has
>      repeatedly paid for. FR/NFR coverage is unchanged: this story still covers NFR9. -->
> ```
>
> AC #1 becomes: *"**Given** the build+test+analyze workflow established by Story 25.1 **When** this story runs
> **Then** it is extended and configured as a **required** status check for release-relevant branches — covering
> pull requests and pushes — failing on any build or test failure, **without introducing a second workflow that
> duplicates the build or test steps**."*
>
> AC #2 is unchanged.
>
> **Rationale:** removes the §2.a duplication collision. The gate requirement (NFR9) is preserved; only its starting
> assumption moves from "no CI exists" to "CI exists and needs hardening."

**Amendment 2 — Story 17.2** (Security and Privacy Hardening), AC #2. Status `backlog`; not started.

> **OLD** (AC #2, in part): *"…and third-party dependencies (C# and the extension's npm tree) are audited for known
> vulnerabilities **And** local-first / no-remote-telemetry operation (NFR3) is re-confirmed for every code path added
> since it was last verified."*
>
> **NEW** — append to AC #2:
>
> *"…**And** the audit scope explicitly includes the CI supply chain introduced by Epic 25 (the SonarScanner and any
> CI actions, plus the third-party service's access to the repository) and, if Epic 26 shipped, its external-service
> integration — verifying that no credential value reaches generated output or a committed settings file (NFR12),
> that the integration is off by default, and that the NFR-3 re-confirmation accounts for the outbound network path
> Story 26.2's ADR authorized."*
>
> **Rationale:** NFR10 requires SpecScribe be safe on public and private codebases. A CI scanner, a third-party
> service with repository access, and a credentialed outbound integration are all new supply-chain and privacy
> surface that the hardening epic must be told about explicitly. Without this, Epic 17 audits a pre-Sonar tool.

---

### 4.5 `sprint-status.yaml` — `development_status` additions

Appended after `epic-24-retrospective: optional`, following the file's existing comment-annotation style:

```yaml
  epic-25: backlog # SonarCloud continuous analysis of SpecScribe's OWN codebase (SCP 2026-07-25, owner-directed).
                   # Dev-time only, ships NO product code. Schedulable IMMEDIATELY — no gates. Stands up the repo's
                   # FIRST build+test CI workflow (today .github/workflows/ has only publish-docs-live-pages.yml),
                   # which is why Story 16.2 is AMENDED to extend it rather than create a second one.
  25-1-sonarcloud-onboarding-and-ci-analysis: backlog # build+test+SonarScanner on clean checkout, push-to-main + PRs;
                   # SONAR_TOKEN as repo secret, fork-PR safe; coverage-collection decision must be RECORDED not left
                   # unstated (~2,350-test suite → real runtime cost). Repo is public (IntegerMan/SpecScribe) → free
                   # OSS tier + GitHub App path apply; no private-source-exposure question for THIS repo.
  25-2-quality-gate-and-findings-triage: backlog # the "results are SCANNED" half — gate on new code + a repeatable
                   # triage pass routing findings into deferred-work.md / action_items using FR30 provenance.
                   # Baseline triage of the existing codebase is REQUIRED so Epic 17 inherits a known state.
                   # Items matching 17.1/17.2/17.3 are TAGGED to those stories, not duplicated.
  25-3-agent-facing-findings-contract-spike: backlog # ⭐ LOAD-BEARING FOR BOTH EPICS. Decision-first, ~2d, throwaway,
                   # NO production code; deliverables = 25-3-spike-report.md + a RATIFIED ADR. Defines the
                   # SOURCE-AGNOSTIC, framework-neutral findings model keyed to entities SpecScribe already projects
                   # (file/dir/epic/story/requirement). MUST prove the model holds for a 2nd source class (compiler/
                   # analyzer warnings — the owner's language-dependent case) so Sonar is instance #1, not the schema.
                   # Attachment join = the SHIPPED PlanningCodeImpact (PlanningCodeImpact.cs:54) + Epic 19 work graph.
                   # Channel options compared: digest artifact vs Epic 22 IR field vs MCP. Epic 26's Story 26.2
                   # CONSUMES this contract — it must not define a second one.
  25-4-agent-consumable-findings-channel: backlog # implements 25.3's selected channel for THIS repo's own SDD
                   # workflow; must NOT move the golden fingerprint (dev-time tooling, not a product surface);
                   # staleness must be honest. Depends on 25.3.
  epic-25-retrospective: optional

  epic-26: backlog # unscheduled — optional external code-analysis insights in the PORTAL (SCP 2026-07-25, FR41 +
                   # NFR12). "Optional in the tool" half of the owner's ask. Design-now/build-later, mirroring how
                   # Epics 22/23 were seated. Owner decision D2: findings must be VISUALIZED alongside code,
                   # directories, epics, stories, requirements — the attach points ALL EXIST already
                   # (CodeFileTemplater.cs:18, GitMetrics.cs:169 FileInsight, PlanningCodeImpact.cs:54,
                   # SettingsResolver.cs:63). Carries SpecScribe's FIRST outbound network capability and FIRST
                   # credential handling — both gated behind 26.2's spike + ADR.
                   # EXECUTION ORDER: 26.1 → 26.2 → 26.3 → 26.4/26.5/26.6. 26.7 is independent.
  26-1-ideation-where-findings-belong-in-the-portal: backlog # OWNER-ELICITED, no code. Per the create-story
                   # visual-intent convention: 2-3 NAMED design directions per new visual surface, owner selects.
                   # Must close the scope for 26.4-26.6 and state what a project with NO analysis configured sees
                   # (the default case for every user). Severity may NEVER be color-alone (UX-DR17/NFR8).
  26-2-ingestion-posture-and-credential-spike: backlog # ⛔ GATES 26.3-26.6. Decision-first, ~2d, throwaway, NO
                   # production code; deliverables = 26-2-spike-report.md + a RATIFIED ADR. Owner decision D3 left
                   # the posture OPEN: SonarCloud web API vs on-disk export vs both. THE NFR-3 QUESTION IS THIS
                   # STORY'S: must state plainly whether the recommended posture needs a PRD amendment or is already
                   # accommodated by NFR-3's "by default" / "required for core operation" wording — and if an
                   # amendment IS needed, draft exact replacement text preserving the prior wording + rationale
                   # inline (the ADR 0013 / NFR-5 precedent), NOT framed as a reinterpretation. Also owns credential
                   # design (no token value in --show-config / diagnostics / output / committed settings) and hands
                   # the supply-chain surface to 17.2. CONSUMES 25.3's contract; does not define a second one.
  26-3-analysis-integration-configuration: backlog # CLI + interactive + .specscribe parity via SettingsResolver
                   # (Story 5.2 pattern, 3-way provenance in --show-config); README option TABLE (PRD §12.3).
                   # DISABLED BY DEFAULT — an upgrading user sees no behavior change and makes no network call.
                   # Depends on 26.2.
  26-4-findings-on-code-pages-and-code-map: backlog # file + directory scope; attaches through CodeFileTemplater /
                   # FileInsight, NOT a parallel code-page pipeline; deep-links the existing #L{n} anchor; directory
                   # aggregation carries a Story 10.2 real-value legend + a text twin (ADR 0013 §3) verified JS-off
                   # in a LIVE BROWSER. Golden fingerprint WILL move. Depends on 26.2 + 26.3.
  26-5-findings-on-planning-entities: backlog # epics/stories/requirements via the SHIPPED PlanningCodeImpact join —
                   # never a second story↔file mapping. Approximateness must be STATED ON THE SURFACE (Story 21.2
                   # cycle-time precedent). Unattached findings (the common case) must reach the 26.6 hub, never be
                   # silently dropped. Counts route through the FR21 single count source. Depends on 26.2 + 25.3.
  26-6-analysis-hub-page-and-dashboard-signal: backlog # dedicated hub on the insight-pages nav (FR27) mirroring the
                   # Git Insights hub; compact dashboard strip per the 21.1/21.2 placement pattern; ABSENT not empty
                   # when disabled (the default). Analysis timestamp via the portal-wide date token (UX-DR25); stale
                   # analysis marked honestly. Depends on 26.2 + 26.3.
  26-7-future-service-integration-investigation: backlog # INDEPENDENT — schedulable any time. Surveys GitHub code
                   # scanning/Dependabot, coverage + vuln services, other quality platforms, and LOCAL compiler/
                   # analyzer output (the owner's language-dependent case). Key question: does 26.2's design
                   # generalize to a PLUGGABLE external-signal provider seam, or is each service bespoke? Must carry
                   # an explicit "none of these" option and name the ADR trigger if a seam is warranted. Feeds
                   # deferred-work.md / the epic backlog — does NOT auto-seat stories.
  epic-26-retrospective: optional
```

Plus a `last_updated` prepend recording this SCP, per the file's convention.

---

### 4.6 PRD

**No PRD edit is proposed in this change.** Following the FR37–FR40 precedent, FR41 is seated in `epics.md` with a
"sync into the PRD when convenient" note. The one PRD change that *may* become necessary — the NFR-3 / §5 Non-Goal
amendment — is **deliberately deferred to Story 26.2's ADR**, because whether it is needed depends on a posture that
has not been decided. Pre-amending the PRD for a network capability that the spike might not select would be
recording a concession the project has not made.

### 4.7 ADRs

**No ADR is written in this change.** Two are *proposed and assigned owners*, per CLAUDE.md § Decision records:

| ADR | Owner | Decides |
|---|---|---|
| Agent-facing findings contract | **Story 25.3** (spike) | The source-agnostic, framework-neutral findings model; entity attachment rule; agent delivery channel. Cross-cutting: two epics bind to it. |
| External-analysis ingestion posture | **Story 26.2** (spike) | SonarCloud API vs on-disk export; credential design; the AD-4 networked-provider boundary; whether PRD NFR-3 needs amending. |

Both are decision-first spikes with throwaway code and a ratified ADR as the durable deliverable — the Story 20.4 /
24.6 / 6.6 pattern. Seating them *now*, before implementation, is the ADR-creation-trigger action item from the Epic
10 retrospective working as intended.

---

## 5. MVP Impact and Action Plan

**MVP is not affected.** PRD §6.1's MVP (BMad-first portal, CLI generate/watch, traceability, git pulse, agent-file
insights) is untouched. Both epics are post-MVP capability, consistent with Epics 19–24.

**Action plan, in order:**

1. Apply the `epics.md` edits: FR41 / NFR11 / NFR12 + provenance, FR Coverage Map rows, two Epic List entries, the
   full `## Epic 25` and `## Epic 26` sections, and the two amendments (16.2, 17.2).
2. Apply the `sprint-status.yaml` edits in the **same change** — a structural scope change recorded in only one
   artifact is a drift bug (CLAUDE.md).
3. Schedule **Story 25.1** first. It has no gates, no dependencies, and delivers the repository's first build/test CI —
   value independent of everything else in this proposal.
4. Run **Story 25.3** before **Story 26.2**. The findings contract must exist before the ingestion spike consumes it.
5. Leave Epic 26 unscheduled until the owner wants it; 26.1's ideation round is its natural opening move.

**Dependencies and gates:**

| Story | Gate |
|---|---|
| 25.1 | none — schedulable immediately |
| 25.2 | 25.1 |
| 25.3 | none (can run parallel to 25.1/25.2) |
| 25.4 | 25.3 |
| 26.1 | none |
| 26.2 | 25.3 (the contract it consumes) |
| 26.3–26.6 | 26.2 |
| 26.7 | none — independent |
| **16.2** | **amended** — now extends the Story 25.1 workflow |

---

## 6. Implementation Handoff

**Scope classification: Moderate** — backlog reorganization plus two amended stories. Not Minor (this adds two epics,
seats a requirement and two NFRs, and changes an existing story's premise). Not Major (no replan; no in-flight work
disturbed; MVP untouched; the architecture spine absorbs it through AD-4 without amendment).

| Recipient | Responsibility |
|---|---|
| **Product Owner / Developer** (this session, on approval) | Apply §4.1–§4.5 to `epics.md` and `sprint-status.yaml` in one change. |
| **Developer** (`create-story`, when scheduled) | Story 25.1 first. Then 25.2, 25.3. Elicit visual intent at 26.1 per convention. |
| **Architect / Developer** (spikes) | Stories 25.3 and 26.2 — each lands a **ratified ADR**, not just a report. |

**Success criteria:**

- Every push to `main` and every PR builds, tests, and is analyzed; findings reach this project's backlog with a
  recorded triage decision, not just an external dashboard.
- Exactly **one** build+test CI workflow exists in the repository.
- Two ratified ADRs exist before any Epic 26 surface code is written.
- The findings model is demonstrably source-agnostic — proven against a second source class, not asserted.
- With the integration disabled (the default), generation is byte-identical to today and makes no network call.

---

## 7. Open Items Raised (deliberately not bundled)

1. **NFR numbering collision (pre-existing, still unresolved).** `epics.md` and the PRD number their NFR lists
   independently and disagree — PRD NFR-5 is progressive enhancement; `epics.md` NFR5 is file locks, NFR6 is
   accessibility semantics. This SCP appends **NFR11 and NFR12 to the `epics.md` list only** and does **not** resolve
   the collision; two more entries now ride on the unresolved numbering. First recorded in SCP 2026-07-24; it still
   deserves its own pass.
2. **PRD NFR-3 / §5 Non-Goal amendment.** Deferred to Story 26.2's ADR (§4.6). Flagged here so it is not discovered
   mid-implementation.
3. **Coverage collection cost.** Story 25.1 AC #3 forces the decision rather than deferring it, but the runtime cost
   of a coverage collector on a ~2,350-test suite is unmeasured. It could make the CI loop meaningfully slower.
4. **Concurrent-session hazard.** Per CLAUDE.md, Epic 20 and Epic 24 work is live on shared `main`. These edits touch
   `epics.md` and `sprint-status.yaml`, both high-traffic. Verify each write landed before relying on it.
