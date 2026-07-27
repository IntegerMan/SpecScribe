---
baseline_commit: 40c7ee96f197a7907dbf8c8fe80c8e5c8fb575a3 # `40c7ee9` — HEAD at authoring time (2026-07-27)
epic: 25
frs: [FR41] # the model FR41 calls "one source-agnostic findings model"
nfrs: [NFR8, NFR12] # framework-agnostic shared rendering; opt-in/offline-safe/credential-safe
uxdrs: [UX-DR17] # severity is never color-alone
decides: docs/adrs/00NN-agent-facing-findings-contract.md # NEW ADR — this spike DECIDES. Number it at authoring
                                                          # time: docs/adrs/ ends at 0018 on disk, but 0019 AND
                                                          # 0020 are BOTH already claimed-but-unwritten (0019 by
                                                          # Story 18.3, 0020 by Story 18.5). 0021 is likely.
                                                          # VERIFY — the claims move daily on shared main.
depends_on: [] # no gates — may run parallel to 25.1/25.2. 25.2's live triage numbers are inherited, not required.
blocks: [25-4, 26-2, 26-3, 26-4, 26-5, 26-6] # the one place the findings model is defined, for BOTH epics
informs: [26-1, 26-7, 27-1] # 26.1's visual ideation, 26.7's provider survey, Epic 27's coverage provider
ships_product_code: false # THROWAWAY spike. No `src/`, no `tests/`, no `web/`, no `extension/`.
                          # `GoldenContentFingerprint` MUST NOT move.
timebox: ~2 days
deliverables:
  - "_bmad-output/implementation-artifacts/25-3-spike-report.md"
  - "docs/adrs/00NN-agent-facing-findings-contract.md (RATIFIED, not merely drafted)"
  - "spike/findings/** (disposable evidence; quarantined per spike/README.md)"
---

# Story 25.3: SPIKE — A Framework-Neutral Findings Contract for AI Agents in SDD Workflows

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer whose planning agents should know what the analyzer knows,
I want the shape of agent-consumable analysis findings decided once — framework-neutral and source-agnostic —
So that neither Epic 25's tooling nor Epic 26's surfaces invent a Sonar-shaped, BMad-shaped model that the other has to work around.

| | |
|---|---|
| **This spike does** | Decide the findings model, the attachment rule, and the delivery channel. Prove the model on a **second source class that never touched Sonar**. Author a **ratified ADR**. |
| **This spike does NOT** | Ship production code. Emit a real findings file for this repo (that is 25.4). Design a portal surface (that is 26.1). Decide ingestion posture or credentials (that is 26.2). Touch `src/**`, `tests/**`, `web/**`, `extension/**`. |

**Discipline:** decision-first, timeboxed, throwaway — same as Stories 6.3, 6.6, 20.1, 20.4, 22.1, 23.1, 24.6.
Suggested timebox **2 days**. If one axis eats the box, finish that axis and report the rest as *unmeasured*
rather than half-measuring all of them.

## ⛔ Read first — nine reconciliations against shipped code and live data

Each one changes what you would otherwise design, measure, or conclude. Every code reference was verified at
`40c7ee9` on 2026-07-27; every Sonar number was read live the same day.

### R1 — You are **not** on a blank page. SpecScribe already ships a diagnostics model.

| Symbol | File | What it is |
|---|---|---|
| `DiagnosticSeverity { Error, Warning, Info }` | `DiagnosticsTemplater.cs:12` | A **3-level normalized severity scale**, already rendered with the category word as text beside the badge — i.e. **already UX-DR17 compliant** |
| `DiagnosticNotice(Category, SourcePath, Message, Severity, AnchorRoot)` | `DiagnosticsTemplater.cs:46` | One row on the Story 4.8 diagnostics page |
| `AdapterDiagnostic(Category, RelativePath, Message, Anchor)` | `AdapterDiagnostic.cs:49` | The ingest-time typed form |
| `AdapterDiagnosticCategory { Unsupported, Malformed, Skipped, Error, Informational }` | `AdapterDiagnostic.cs:7` | The fine category vocabulary |
| `DiagnosticAnchorRoot { None, Source, Adr, Repo }` | `DiagnosticsTemplater.cs:25` | **The "which root is this path relative to" problem, already solved** — and already extended twice (Story 6.12 added `Adr`, Story 18.2 added `Repo`) |
| JSON-lines stderr channel | `Commands.cs:63,240,280` | `path` / `severity` / `message` / `fileAnchored` → VS Code `DiagnosticCollection` (`extension/src/extension.ts:249,1501`) |

**The reuse-vs-parallel question is a first-class ADR decision, not an implementation detail.** There is a real
argument on both sides and the ADR must record which won:

- **Reuse:** the severity scale, the anchor-root problem, the never-color-alone rendering, and an agent-facing
  serialization (JSON lines on stderr) all already exist and are proven.
- **Separate:** these diagnostics describe **SpecScribe's own run** ("I could not parse your sprint-status.yaml").
  Analysis findings describe **the user's code** ("your regex has no timeout"). Same shape, different subject,
  different lifetime, different provenance. Conflating them puts a generator failure and a code smell in the
  same list.

Whichever you choose, say it plainly. Silently inventing a fifth severity enum in a repo that already has
`DiagnosticSeverity`, `GenerationOutcome`, `AdapterDiagnosticCategory`, and the `--status-*` stage vocabulary is
the failure mode this table exists to prevent.

### R2 — The word "findings" is **already taken** in this codebase.

`## Review Findings` is a **parsed** story-file section: `EpicsParser.cs:253` carves it, `EpicsView.cs:303`
carries it as `ReviewFindingsHtml`, and `EpicsTemplater.cs:128` renders it on every story page. A type named
`Finding` / `Findings` collides with an existing, rendered, user-visible concept — exactly the way
`ArtifactCoverage` (`ArtifactCoverage.cs:41`) already owns the word "coverage" that Epic 27 (FR42) now needs.

**Name the model deliberately and record the naming decision in the ADR.** If you keep "finding", say how a
reader tells a Sonar finding from a code-review finding on the same story page.

### R3 — Compiler/analyzer warnings **already reach Sonar**, so proving the model on them via Sonar proves nothing.

Story 25.2's baseline: **755 of 1,360** issues were `external_roslyn:*` — .NET SDK analyzer output the scanner
imports from the build, not SonarSource rules. If AC #1's "second, structurally different source class"
demonstration reads `external_roslyn:CA1861` out of `api/issues/search`, it has demonstrated that **Sonar's
normalizer works**. It has not demonstrated that the model is source-agnostic — which is the entire point.

**The proof must use raw analyzer output that never passed through Sonar.** The supported path:

```bash
dotnet build src/SpecScribe/SpecScribe.csproj -p:ErrorLog="roslyn.sarif%2Cversion=2.1"
```

Two traps, both real:

- `,` must be escaped as `%2c` on the MSBuild property or the version qualifier is parsed as a second property.
- **Building a solution with one `ErrorLog` path writes only the last project's log** (`dotnet/roslyn#24319`).
  Build one project at a time, or use a per-project path token. A single 3-result SARIF file from a 4-project
  solution is this bug, not a clean codebase.

### R4 — SARIF 2.1.0 is an **OASIS standard**, Roslyn emits it natively, and you must price it before inventing anything.

SARIF 2.1.0 (OASIS Standard, 27 March 2020; Errata 01, 28 August 2023) is the interchange format this problem
already has. Roslyn emits it. GitHub code scanning consumes it. SonarQube *imports* it
(`sonar.sarifReportPaths`). Designing a bespoke JSON shape without confronting SARIF is the "reinventing wheels"
disaster in its purest form.

**Diverging from SARIF is a legitimate outcome** — it has no notion of epic/story/requirement, its `level` enum
is 4-valued (`none`/`note`/`warning`/`error`), and a full SARIF log is verbose enough to be a poor agent-context
payload. But the ADR must say **which of three** it chose and why: *(a)* the contract **is** SARIF 2.1.0,
*(b)* the contract is a **named profile/subset** of SARIF plus SpecScribe-specific `properties`, or *(c)* a
deliberate divergence with the reasons stated. "We didn't consider it" is not available.

### R5 — Sonar carries **two severity axes that disagree**, and the one 25.2 triaged on is deprecated.

Live facets, `resolved=false`, read 2026-07-27 (**1,420 unresolved**, up from 1,360 the previous day — these
move on every push):

| Legacy `severities` | Count | | MQR `impactSeverities` | Count |
|---|---|---|---|---|
| INFO | **771** | | BLOCKER | 1 |
| MINOR | 370 | | HIGH | 111 |
| MAJOR | 167 | | MEDIUM | **935** |
| CRITICAL | 111 | | LOW | 373 |
| BLOCKER | 1 | | INFO | **0** |

**The 771 legacy-INFO issues are MEDIUM under MQR, and the MQR INFO band is empty.** Story 25.2's central triage
premise — "the INFO band is one bulk disposition" — is an artifact of which axis you read. A normalized scale
pinned to the legacy `severity` field and one pinned to `impacts[]` produce **opposite** triage orders on the
same 1,420 issues. Sonar has frozen the legacy fields (type and severity can no longer be edited on issues or
rules) and MQR is the forward model.

Two more shape facts from a live issue payload, both fatal to a naive scalar model:

- **`impacts` is an ARRAY** of `{softwareQuality, severity}` pairs. Today every issue happens to carry exactly
  one (the facet totals sum to 1,420), but that is a coincidence of the current rule set, not a guarantee. A
  single `severity` string cannot hold it losslessly.
- **`flows[]` carries secondary locations.** The sample `csharpsquid:S1192` issue at `Charts.cs:503` carried
  **seven** additional `textRange`s. A model with one `location` silently discards them — and SARIF's
  `relatedLocations`/`codeFlows` exist precisely because every serious analyzer has this.

### R6 — The attachment join is **gated on `--deep-git`** and can vanish silently.

`PlanningCodeImpact.Build` is called at `SiteGenerator.cs:357` and `:739`, both behind
`progress.DeepGit?.Commits is { Count: > 0 }`. In a **default run there is no join at all** — findings attach to
files and directories and nothing else. That is the common case, not the edge case.

Worse, the loss is silent: this project has already recorded a deep-git timeout dropping whole surfaces at
`errors=0` (memory: *GitMetrics 3s timeout — silent deep-git loss*). So "findings reach stories" can be true on
Monday and false on Tuesday with no diagnostic. AC #2's *"states honestly where the join is approximate or
absent"* has a concrete, unflattering answer here and the report must give it.

The join's own honesty limits, from its XML docs (`PlanningCodeImpact.cs:40-53`): it is a **two-tier
best-effort heuristic** over commit-message and merge-branch naming — Tier 2 is explicitly *"a linear-window
approximation of which commits this branch merged, deliberately NOT a parent-hash DAG walk."* It answers
*"which story's commits touched this file"* — **authorship history, not ownership**. A finding in
`SiteGenerator.cs` would attach to nearly every story in the project.

### R7 — "Requirement" is in AC #1's key list, but there is **no story→requirement edge** in this codebase.

`TraceabilityTemplater` plots requirements against **epics** (`TraceabilityTemplater.cs:25` — *"Requirement-to-epic
traceability matrix"*). `PlanningCodeImpact` yields epic and story. So:

- `finding → file → story` — one approximate hop (R6), deep-git gated.
- `finding → file → epic → requirement` — **two** hops, the second at **epic granularity only**.

AC #1 names `requirement` as a first-class key. Either the model carries it and the report states it is a
two-hop epic-granular derivation, or the model does not and the report says so. Do not let the word sit in a
schema table implying an edge that does not exist.

### R8 — An **official SonarQube MCP Server already exists**, and it inverts AC #3's third option.

SonarSource ships an official MCP server (`SonarSource/sonarqube-mcp-server`, Docker image
`sonarsource/sonarqube-mcp`) that connects an AI agent to a SonarQube/SonarCloud project — issues, quality gate,
hotspots, coverage — and **explicitly supports Claude Code**, the owner's own agent.

This is not the option AC #3 imagines. The MCP row is therefore **not** "SpecScribe builds and ships an MCP
server, gaining a runtime it does not have." It is at minimum a two-row comparison:

| MCP variant | SpecScribe code | What the agent receives |
|---|---|---|
| **Adopt Sonar's official server** | **Zero** | **Sonar's** model — Sonar-shaped, Sonar-only, requires the service reachable |
| **SpecScribe emits its own MCP surface** | A new runtime + server lifecycle SpecScribe has today (verified: no MCP dependency anywhere in the repo) | This spike's source-agnostic model |

The first is nearly free **and forfeits the exact property this spike exists to establish**. Say that out loud.
It may still be the right answer *for Story 25.4 today* while the contract remains the right answer for Epic 26 —
"adopt the free thing now, keep the contract for the surfaces" is a legitimate recommendation, but it must be
argued, not stumbled into.

### R9 — The Epic 22 IR channel **moves the golden fingerprint**, which Epic 25 forbids.

The IR is `spa/`, promoted in place (ADR 0016). Its real shape, from `SpaDelivery.cs:47,558-565`:

```
SchemaVersion = 1   // bumps on breaking shape changes only
Manifest(SchemaVersion, SiteTitle, Entry, Nav[], OversizedPages[], Pages{path -> ManifestEntry})
ManifestEntry(Title, Chunk, Breadcrumb[], Parent, Children[], Head, ScriptIslands[], ContentHash, Bytes)
```

**The IR is generated output.** Anything added to it changes generated bytes, and Story 25.4 AC #2 and Epic 25's
own charter both require the golden fingerprint to be **unmoved**. So the IR-field option is structurally
unavailable to Story 25.4 — while being a natural fit for Epic 26, which expects the fingerprint to move
(26.4's sprint-status note says so explicitly). AC #3's comparison must separate *"best channel for 25.4"* from
*"best channel for Epic 26"* rather than picking one winner for a question that has two different answers.

Also note: `ManifestEntry` carries a per-page `ContentHash`, and `SpaDelivery` chunks pages — a findings payload
attached per page would ride that chunking, not sit in one blob. And Story 23.4 is currently **blocked** with
857/1046 pages' IR still produced by the code it retires; do not design against an IR shape that is mid-migration
without saying so.

---

## Acceptance Criteria

### AC #1 — A findings model keyed to entities SpecScribe already projects, proven on a second source class

**Given** the owner's framework-neutral requirement (NFR8)
**When** the spike defines the contract
**Then** it specifies a findings model keyed to **entities SpecScribe already projects** — file, directory, epic,
story, requirement — carrying at minimum: source/provider, rule identity, severity **on a normalized scale with a
text label** (never color-alone, UX-DR17), location, message, and provenance/analysis timestamp
**And** it demonstrates the model holds for a **second, structurally different source class** — compiler/analyzer
warnings, which the owner flagged as language-dependent — proving Sonar is instance #1 and not the schema.

Binding clarifications:

- **The second source class must never have passed through Sonar (R3).** Raw Roslyn SARIF from
  `dotnet build -p:ErrorLog=...` is the sanctioned path. Reading `external_roslyn:*` out of the Sonar API does
  not satisfy this AC.
- **"Demonstrates" means a mapping executed on real data, both ways.** Take a real Sonar issue and a real Roslyn
  SARIF result, project both into the model, and record **what was lost** in each direction. A schema table with
  no worked example is not a demonstration.
- **The normalized severity scale must confront R5.** State which Sonar axis it reads (legacy `severity` or
  `impacts[]`), what happens to the array, and what a 4-level SARIF `level` and a 5-level Sonar scale collapse to.
  If the target scale is SpecScribe's existing 3-level `DiagnosticSeverity`, say what the collapse costs.
- **State the reuse-vs-parallel decision from R1 explicitly**, with the losing argument recorded.
- **Name the model deliberately (R2).**
- **Multi-location findings (R5, `flows[]`) get an explicit answer** — carried, truncated, or dropped. Silence is
  a decision made by accident.

### AC #2 — Attachment to planning entities, with the join's failure modes stated honestly

**Given** findings must attach to planning entities, not just files
**When** the spike defines attachment
**Then** it specifies how a file-scoped finding reaches an epic/story/requirement, evaluating the **shipped**
`PlanningCodeImpact` (`src/SpecScribe/PlanningCodeImpact.cs:54`) commit/branch miner and the Epic 19 work graph as
the join, and states honestly where the join is approximate or absent
**And** it states what happens to findings that attach to **no** planning entity (the common case) so they are
never silently dropped.

Binding clarifications:

- **The `--deep-git` gate (R6) must be stated as a first-class limitation, not a footnote.** In a default run
  the join does not exist. Say what the model does then.
- **Name the silent-loss mode.** Deep git can fail on a timeout at `errors=0`. If attachment silently degrades
  from "attached to 4 stories" to "attached to nothing" with no diagnostic, that is a designed-in dishonesty —
  say how the contract prevents it (a provenance flag on the payload is the obvious candidate).
- **The requirement key gets the R7 treatment** — two hops, epic-granular, or absent.
- **Evaluate the work graph honestly too.** `WorkGraphBuilder` / `WorkNode` / `WorkEdge` (`WorkGraph.cs:39,42,105`)
  model epic↔story↔requirement relationships but carry **no file nodes** — `WorkNodeKind` is a planning
  vocabulary. It is a join *between planning entities*, not from code to planning. If it cannot be the join,
  say that rather than listing it as an evaluated option and moving on.
- **Unattached is the common case, and it is where Story 26.6's hub gets its content.** Route it, name the
  destination, and say the count you expect (on this repo, with `--deep-git` off, it is 100 %).

### AC #3 — Channels compared with a recommendation, on real trade-offs

**Given** agents consume this in SDD workflows across frameworks
**When** the spike evaluates delivery channels
**Then** it compares — with a recommendation — at least: a generated agent-readable digest artifact, a field on
the Epic 22 JSON IR, and an MCP-server surface; reporting for each the framework-neutrality (NFR8), the offline
behavior, and whether it requires SpecScribe to gain a runtime it does not have
**And** it states which channel Story 25.4 implements and what it defers.

Binding clarifications:

- **The MCP row is two rows (R8).** Adopting Sonar's official server is an existing, zero-code option that
  forfeits source-agnosticism. Price it as a real candidate.
- **The IR row must carry the fingerprint consequence (R9)** and must separate the 25.4 answer from the Epic 26
  answer. One recommendation for two different constraint sets is a wrong answer to at least one of them.
- **The digest row needs a concrete artifact**, not a category — where it lands, what format, how big for this
  repo's 1,420 findings, whether an agent can consume it without reading all of it (the 25.4 use case is *"the
  files I am about to touch"*, not *"the whole project"*), and whether it is gitignored or committed.
- **"Offline behavior" is not optional prose.** For each channel: what does an agent see with no network, with a
  stale artifact, and with no analysis ever run? NFR12 and AD-4 ("optional insight providers may enrich output
  but never own baseline success") both bind here.
- **Framework-neutrality means BMad-neutral too, not just Sonar-neutral.** The attachment keys (epic/story/
  requirement) are BMad vocabulary. State what the contract does in a repo using Spec Kit, GSD, or no framework
  at all — Epics 11–15 exist because that is a real case.

### AC #4 — A ratified ADR, and an explicit handoff

**Given** CLAUDE.md § Decision records
**When** the spike concludes
**Then** it lands a **ratified ADR** recording the findings contract, its options table, and its consequences —
this is a cross-cutting contract two epics bind to, and must not be settled inside an implementation story's dev pass
**And** the report states explicitly what it hands to Story 25.4 and to Stories 26.2–26.6.

Binding clarifications:

- **Ratified, not drafted.** ADRs 0016/0017/0018 all sit at **Proposed**. This one is consumed by six downstream
  stories; a Proposed ADR is not a contract they can bind to. Get the owner's ratification before status → review.
- **Number it at authoring time, and do not trust this file's guess.** `docs/adrs/` ends at **0018** on disk, but
  **two numbers past it are already claimed and unwritten**: **0019** by Story 18.3 (*"LLM-Generated Artifacts Are
  Enrichment-Only Inputs"*) and **0020** by Story 18.5 (the TEA JSON-filename widening, seated 2026-07-27 by a
  concurrent session **after** this story was drafted). **0021 is the likely number.** Grep
  `_bmad-output/implementation-artifacts/**` for `docs/adrs/00` before you claim one — on shared `main` this
  moves daily, and two ADRs with the same number is the drift bug CLAIMED-but-unwritten numbering invites.
- **Add the entry to `docs/adrs/README.md`** in the established one-line-with-consequences style — the index is
  hand-maintained and an ADR missing from it is invisible.
- **Cite ADRs by symbol/section title, never by line number** (memory: *cite-adrs-by-symbol-not-line-number*).
- **State the amendment surface.** If the contract constrains ADR 0016's IR shape or AD-4's provider boundary,
  say so in the ADR rather than leaving a downstream story to discover it.

## Tasks / Subtasks

- [ ] **Task 1 — Re-read the ground truth before designing anything (AC: #1, #2)**
  - [ ] Re-run the live facet query in § Re-measure first. The count moved **1,360 → 1,420 in one day**; every
        number in this file is from 2026-07-27. If a figure here is wrong, say so in the report — do not quietly
        re-baseline.
  - [ ] Read `AdapterDiagnostic.cs`, `DiagnosticsTemplater.cs:1-80`, and `Commands.cs:240-290` **in full** before
        proposing any severity enum (R1). Do not infer their shape from this story's table.
  - [ ] Read `PlanningCodeImpact.cs:40-60` XML docs for the join's own stated approximations (R6).
  - [ ] Confirm `SiteGenerator.cs:357` and `:739` still gate `PlanningCodeImpact.Build` on `DeepGit.Commits` —
        a concurrent session may have moved them. Grep, do not assume (CLAUDE.md § Concurrent work).

- [ ] **Task 2 — Produce raw analyzer output that never touched Sonar (AC: #1)**
  - [ ] Emit SARIF 2.1 from a real build (R3). Escape the comma; build **one project at a time** or use a
        per-project path token (`dotnet/roslyn#24319`).
  - [ ] Record the result count and sanity-check it against the ~755 `external_roslyn:*` issues Sonar imported.
        A wildly smaller number means the multi-project trap bit you, not that the code is clean.
  - [ ] Put the artifacts under `spike/findings/` — quarantined per `spike/README.md`, referenced by no `.slnx`,
        contributing nothing to the shipped tool.

- [ ] **Task 3 — Price SARIF before designing a schema (AC: #1, #4)**
  - [ ] Map a real Sonar issue **and** a real Roslyn SARIF result into your candidate model. Record what each
        direction loses.
  - [ ] Answer R4's three-way question — **is** SARIF / **profile of** SARIF / **deliberate divergence** — with
        reasons, in the ADR's options table.
  - [ ] Settle the severity axis (R5): legacy vs `impacts[]`, the array, and the collapse cost.
  - [ ] Settle multi-location (`flows[]` / SARIF `relatedLocations`).
  - [ ] Settle naming (R2) and reuse-vs-parallel (R1).

- [ ] **Task 4 — Define attachment, gates and all (AC: #2)**
  - [ ] Specify `finding → file → {directory, story, epic, requirement}` with the hop count and approximation
        stated per edge.
  - [ ] State the `--deep-git`-off behavior explicitly, and how the payload advertises that attachment was
        unavailable rather than empty.
  - [ ] Evaluate the work graph and say plainly if it cannot be the join (it carries no file nodes).
  - [ ] Define the unattached route and name Story 26.6's hub as its destination.

- [ ] **Task 5 — Compare channels and recommend (AC: #3)**
  - [ ] Build the comparison table. **Four rows minimum**: digest artifact, Epic 22 IR field, Sonar's official
        MCP server, a SpecScribe-emitted MCP surface.
  - [ ] Per row: framework-neutrality, offline behavior, new-runtime cost, **fingerprint impact**, staleness
        honesty, and whether an agent can consume a *subset* (the 25.4 use case).
  - [ ] Recommend **for Story 25.4** and **for Epic 26** separately if the answers differ. Say so if they do.
  - [ ] State what 25.4 defers.

- [ ] **Task 6 — Write the report (AC: #1–#4)**
  - [ ] `_bmad-output/implementation-artifacts/25-3-spike-report.md`. Follow the structure of
        [24-6](24-6-graph-engine-spike.md)'s and [23-1](23-1-spike-report.md)'s reports: findings numbered and
        citable, negatives reported as loudly as positives, unmeasured axes named as unmeasured.
  - [ ] Include a **§ Handoff** naming, per story: 25.4, 26.2, 26.3, 26.4, 26.5, 26.6 — and what each receives.
  - [ ] Add a note to **26.1** (visual ideation): the severity vocabulary and text labels it must render, so it
        does not invent a second one. Add a note to **26.7**: whether the contract generalizes to a pluggable
        provider seam or is bespoke per service.
  - [ ] Add a note to **Epic 27** (FR42, coverage): coverage is a per-file *metric*, not a finding — say whether
        it rides this contract or is deliberately outside it. Epic 27 was kept separate for a reason; confirm or
        challenge that reason on evidence.

- [ ] **Task 7 — Author and ratify the ADR (AC: #4)**
  - [ ] Verify the next free number (0019 is claimed by 18.3, unwritten). Author `docs/adrs/00NN-*.md` in the
        house format: Status line, Context, Decision(s), Options considered, Consequences.
  - [ ] Add the one-line entry to `docs/adrs/README.md`.
  - [ ] **Get owner ratification.** Status `Accepted`, not `Proposed`, before this story goes to review.

- [ ] **Task 8 — Prove the spike shipped nothing (AC: all)**
  - [ ] `git status` — no `src/`, `tests/`, `web/`, `extension/` edits attributable to this story. If a
        concurrent session's edits are in the tree, say so and leave them (CLAUDE.md: never `git reset --hard`,
        `git checkout --`, or `git clean`).
  - [ ] Full suite green and `GoldenContentFingerprint` **unmoved**. If it moved, either you edited `src/` or a
        sibling session did — determine which and record it.
  - [ ] Confirm `spike/findings/` is referenced by no project file and the generated site is byte-identical with
        and without it (the `spike/README.md` guarantee).

## Dev Notes

### § Re-measure first

The project is public; no token needed. Numbers move on every push.

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&ps=1&facets=rules,types,severities,impactSeverities,cleanCodeAttributeCategories"
```

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&ps=1" | python3 -m json.tool
```

**Always pass `resolved=false`** — without it the endpoint returns ~148 extra CLOSED issues on files Story 25.1's
exclusion widening removed from analysis (25.2 § Read first, fact 4). Rule names resolve with
`api/rules/show?organization=integerman-github&key=<rule>`; the `organization` parameter is **required**.

### § The live issue payload, annotated

One real unresolved issue, fetched 2026-07-27 — this is the shape you are mapping *from*:

| Field | Value | Note for the model |
|---|---|---|
| `key` | `AZ-ksuKYdNye8HC2CD5p` | Server-assigned, opaque, **not stable across a re-analysis of a moved line** |
| `rule` | `csharpsquid:S1192` | `{repo}:{id}` — the natural "rule identity" |
| `component` | `IntegerMan_SpecScribe:src/SpecScribe/Charts.cs` | **`PROJECT:path`** — must be split to get a repo-relative path |
| `line` / `textRange` | 503 / start+end line+offset | SARIF carries the same, richer |
| `flows[]` | **7 secondary locations** | R5 — a single-location model drops all seven |
| `severity` / `type` | `MINOR` / `CODE_SMELL` | **Legacy, frozen** |
| `cleanCodeAttribute` / `…Category` | `DISTINCT` / `ADAPTABLE` | MQR taxonomy |
| `impacts[]` | `[{MAINTAINABILITY, LOW}]` | **Array** — R5 |
| `effort` / `debt` | `4min` | No SpecScribe analogue; carry or drop deliberately |
| `tags[]` | `["design"]` | |
| `creationDate` / `updateDate` | ISO-8601 | Provenance — but see staleness below |
| `assignee` | `IntegerMan@github` | **Do not carry.** No people-scoreboard (memory: *market-research-git-activity-file-insights*) |

**Staleness is not `updateDate`.** Story 25.4 AC #2 requires consumers to tell *"when the analysis predates the
working tree."* The honest field is the **analysis revision** (`api/project_analyses/search` gives the SCM
revision per analysis), compared against local `HEAD`. An ISO timestamp alone cannot answer it. Design the
provenance block for that question, not for the timestamp.

### § What Story 25.4 can and cannot do, so your recommendation is implementable

| Constraint | Source | Consequence for the channel choice |
|---|---|---|
| Golden fingerprint **unmoved** | 25.4 AC #2, Epic 25 charter | Rules out the IR field for 25.4 (R9) |
| Quarantined from the generation critical path | 25.4 AC #2 | A `SiteGenerator` hook is out; a separate command or an out-of-band artifact is in |
| Opt-in; produces **nothing** rather than failing | 25.4 AC #1, NFR12, AD-4 | The channel must have a designed "no analysis configured" state |
| No token value written anywhere | 25.4 AC #1, NFR12 | If the channel calls the API, credential handling appears **here** — but 26.2 owns that design. Say which parts 25.4 needs early and flag the ordering |
| Staleness honest | 25.4 AC #2 | See above — revision, not timestamp |

**The credential ordering is a real risk and this spike should name it.** 26.2 owns credential design, but 25.4
runs *first* and may need a token to fetch findings. Either 25.4's channel reads something already on disk (a CI
artifact, a downloaded export), or 25.4 needs a slice of 26.2's design ahead of time. Decide which and say so —
discovering it inside 25.4's dev pass is how a spike's contract gets amended by an implementation story.

### § Prior art to read before designing (in this order)

1. `src/SpecScribe/AdapterDiagnostic.cs` (53 lines) and `src/SpecScribe/DiagnosticsTemplater.cs:1-80` — R1.
2. `src/SpecScribe/PlanningCodeImpact.cs:40-60` — the join's self-declared approximations.
3. `src/SpecScribe/SpaDelivery.cs:33-47, 545-565` — the IR's real manifest shape, R9.
4. [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md) § *Decision* — what the IR transports and
   the `SchemaVersion` bump rule.
5. `spike/README.md` — the quarantine guarantee your evidence must satisfy.
6. [24-6-graph-engine-spike.md](24-6-graph-engine-spike.md) — the house shape for a decision-first spike story
   whose deliverable is an ADR.
7. [18-3-bmad-index-docs-contract-spike.md](18-3-bmad-index-docs-contract-spike.md) — the house shape for a spike
   that defines a **contract**, including a negative recommendation delivered cleanly and a failure taxonomy.

### § External references worth confirming, not trusting

Confirm each at spike time; versions and availability move.

- **SARIF 2.1.0** — OASIS Standard, 27 Mar 2020; Errata 01, 28 Aug 2023. <https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html>
- **Roslyn error-log format** — <https://github.com/dotnet/roslyn/blob/main/docs/compilers/Error%20Log%20Format.md>;
  multi-project `ErrorLog` trap: `dotnet/roslyn#24319`.
- **SonarQube MCP Server (official)** — <https://github.com/SonarSource/sonarqube-mcp-server>; docs at
  <https://docs.sonarsource.com/sonarqube-mcp-server/about-the-mcp-server>. Verify it supports **SonarQube
  Cloud** (not Server only) and what its tool surface returns before pricing R8's first row.
- **Sonar MQR mode / Clean Code taxonomy** — <https://docs.sonarsource.com/sonarqube-cloud/deprecations-and-removals>
  for the legacy severity/type deprecation state at spike time.

### § What this story must NOT do

- **No production code.** No `src/`, `tests/`, `web/`, `extension/` edits. `GoldenContentFingerprint` unmoved.
- **Must not emit a real findings artifact for this repo.** That is Story 25.4. A worked example inside
  `spike/findings/` is evidence; a committed digest at a durable path is 25.4's implementation.
- **Must not design a portal surface.** Story 26.1 is an owner-elicited ideation round and this spike must not
  pre-empt its design directions. Hand it the severity vocabulary; do not hand it a layout.
- **Must not decide ingestion posture or credentials.** That is 26.2 — including the NFR-3 local-first question.
  If your channel recommendation implies a posture, **say so as a constraint on 26.2**, do not decide for it.
- **Must not add a SpecScribe runtime dependency.** ADR 0012 permits two chart-engine families; an MCP server or
  an HTTP client is a different axis entirely and needs its own ADR — which, if your recommendation requires one,
  is a thing to name, not to slip in.
- **Must not `git reset --hard`, `git checkout --`, or `git clean`.** A concurrent session's uncommitted work is
  routinely in this tree (CLAUDE.md). At authoring time it carried ~26 modified files across `src/`, `web/`, and
  `_bmad-output/`.

### Project Structure Notes

- **Added (durable):** `_bmad-output/implementation-artifacts/25-3-spike-report.md`,
  `docs/adrs/00NN-agent-facing-findings-contract.md`, one line in `docs/adrs/README.md`.
- **Added (disposable):** `spike/findings/**` — SARIF samples, mapping scripts, comparison notes. Quarantined
  per `spike/README.md`: no `.slnx` reference, no build participation, zero effect on generated output.
- **Modified:** `_bmad-output/implementation-artifacts/sprint-status.yaml` (status + any action items).
- **Untouched:** everything under `src/`, `tests/`, `extension/`, `web/`, `SpecScribe.slnx`.
- **No new visual surface.** This spike ships no product code, so the create-story visual-direction elicitation
  does not apply. The visual design of findings surfaces is **Story 26.1's**, explicitly and by owner direction.
  This spike's only obligation to it is the severity vocabulary and its text labels (UX-DR17).

### Testing

There is no unit test for a contract decision. The evidence for this story is:

1. **A raw SARIF file** in `spike/findings/`, produced by a real `dotnet build`, with its result count recorded
   and the multi-project trap ruled out (R3).
2. **Two worked mappings** — one live Sonar issue, one raw Roslyn SARIF result — projected into the model, each
   with an explicit loss list (AC #1).
3. **The channel comparison table**, with the fingerprint and offline columns filled for every row (AC #3).
4. **The attachment analysis run against this repo's real data**: how many of today's ~1,420 findings attach to a
   story with `--deep-git` on, and how many with it off (expected: zero). A number, not a claim (AC #2).
5. **A ratified ADR** at `Accepted`, present in `docs/adrs/README.md` (AC #4).
6. Full suite green; `GoldenContentFingerprint` unmoved; `spike/findings/` provably inert.

### References

- Epic + ACs: [epics.md § Story 25.3](../planning-artifacts/epics.md) (lines 4418–4452); Epic 25 charter at line 4342
- Requirement: **FR41** — [epics.md:91](../planning-artifacts/epics.md); **NFR8** — [epics.md:137](../planning-artifacts/epics.md);
  **UX-DR17** — [epics.md:195](../planning-artifacts/epics.md); **NFR12** — Epic 26 header, [epics.md:365](../planning-artifacts/epics.md)
- Architecture: **AD-4** *"Optional insight providers may enrich output but never own baseline success"* —
  [ARCHITECTURE-SPINE.md § AD-4](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)
- Consumers, read them before recommending: [epics.md § Story 25.4](../planning-artifacts/epics.md) (lines 4454–4472),
  § Story 26.2 (the ADR that must not define a second model), §§ 26.3–26.6
- Sibling context, and the source of every Sonar number: [25-2-quality-gate-and-findings-triage.md](25-2-quality-gate-and-findings-triage.md)
  — especially § The baseline and § The output format is a parsed contract;
  [25-1-sonarcloud-onboarding-and-ci-analysis.md](25-1-sonarcloud-onboarding-and-ci-analysis.md) § Handoff
- Origin + owner decisions D2/D3: [sprint-change-proposal-2026-07-25.md](../planning-artifacts/sprint-change-proposal-2026-07-25.md)
- Shipped code this spike must reconcile against: `src/SpecScribe/AdapterDiagnostic.cs`,
  `src/SpecScribe/DiagnosticsTemplater.cs`, `src/SpecScribe/PlanningCodeImpact.cs`, `src/SpecScribe/WorkGraph.cs`,
  `src/SpecScribe/SpaDelivery.cs`, `src/SpecScribe/Commands.cs`, `extension/src/extension.ts`
- IR contract: [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md);
  route space: [ADR 0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md)
- Spike conventions: [spike/README.md](../../spike/README.md); house shape:
  [24-6-graph-engine-spike.md](24-6-graph-engine-spike.md), [18-3-bmad-index-docs-contract-spike.md](18-3-bmad-index-docs-contract-spike.md)
- The durable Sonar documentation home: [docs/SonarCloudSetup.md](../../docs/SonarCloudSetup.md)
- Working conventions (shared `main`, no destructive git, verify-after-edit, ADR triggers, live-browser
  verification): [CLAUDE.md](../../CLAUDE.md)
- Live dashboard: <https://sonarcloud.io/project/overview?id=IntegerMan_SpecScribe>

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

<!-- Required by the ACs — do not mark this story done without all nine:
     1. The findings model, named deliberately, with the reuse-vs-parallel decision against the shipped
        DiagnosticSeverity/DiagnosticNotice recorded either way (AC #1, R1, R2)
     2. The SARIF question answered three ways: is / profile of / diverges from, with reasons (AC #1, R4)
     3. The severity axis settled — legacy vs impacts[], the array, the collapse cost (AC #1, R5)
     4. A second source class proven on RAW Roslyn SARIF that never touched Sonar, with both mappings' losses
        listed (AC #1, R3)
     5. Attachment specified, with the --deep-git gate and its silent-loss mode stated, and the requirement key's
        two-hop epic-granular reality named (AC #2, R6, R7)
     6. The unattached route named, with the expected count on this repo (AC #2)
     7. The channel table with four rows minimum, separate recommendations for 25.4 and Epic 26 if they differ,
        fingerprint column filled (AC #3, R8, R9)
     8. A RATIFIED ADR (Accepted, not Proposed), correctly numbered, listed in docs/adrs/README.md (AC #4)
     9. Handoffs written for 25.4 and 26.2-26.6, plus notes to 26.1, 26.7, and Epic 27 (AC #4)
-->

### File List

### Change Log

| Date | Change |
|---|---|
| 2026-07-27 | Story created by `create-story` at baseline `40c7ee9`. Nine reconciliations recorded against shipped code and live Sonar data. |
