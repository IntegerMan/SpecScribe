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

Status: done

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

- [x] **Task 1 — Re-read the ground truth before designing anything (AC: #1, #2)**
  - [x] Re-run the live facet query in § Re-measure first. The count moved **1,360 → 1,420 in one day**; every
        number in this file is from 2026-07-27. If a figure here is wrong, say so in the report — do not quietly
        re-baseline. → **1,466 on 2026-07-28**; all figures restated in the report, none silently re-baselined.
  - [x] Read `AdapterDiagnostic.cs`, `DiagnosticsTemplater.cs:1-80`, and `Commands.cs:240-290` **in full** before
        proposing any severity enum (R1). Do not infer their shape from this story's table. → Found **F5**: the
        stderr channel is **2-level**, not 3 (`Info` → `"warning"`), which R1's table does not record.
  - [x] Read `PlanningCodeImpact.cs:40-60` XML docs for the join's own stated approximations (R6).
  - [x] Confirm `SiteGenerator.cs:357` and `:739` still gate `PlanningCodeImpact.Build` on `DeepGit.Commits` —
        a concurrent session may have moved them. Grep, do not assume (CLAUDE.md § Concurrent work). → **They
        moved: now `:388` and `:774`.** Gate condition unchanged.

- [x] **Task 2 — Produce raw analyzer output that never touched Sonar (AC: #1)**
  - [x] Emit SARIF 2.1 from a real build (R3). Escape the comma; build **one project at a time** or use a
        per-project path token (`dotnet/roslyn#24319`). → **A third trap found: an up-to-date build writes no
        SARIF at all** (`0 Warning(s)`, no file). `-t:Rebuild` required.
  - [x] Record the result count and sanity-check it against the ~755 `external_roslyn:*` issues Sonar imported.
        A wildly smaller number means the multi-project trap bit you, not that the code is clean. → src alone gave
        **261** (the trap); + tests gave **834**, reconciling with Sonar's **819** (CA1861 339 vs 338).
  - [x] Put the artifacts under `spike/findings/` — quarantined per `spike/README.md`, referenced by no `.slnx`,
        contributing nothing to the shipped tool. → Inertness **tested**, not assumed (report § 13.3).

- [x] **Task 3 — Price SARIF before designing a schema (AC: #1, #4)**
  - [x] Map a real Sonar issue **and** a real Roslyn SARIF result into your candidate model. Record what each
        direction loses. → Executed on **1,466 + 834 real records** by `spike/findings/map_to_model.py`.
  - [x] Answer R4's three-way question — **is** SARIF / **profile of** SARIF / **deliberate divergence** — with
        reasons, in the ADR's options table. → **Profile of SARIF 2.1.0** (ADR Decision 3).
  - [x] Settle the severity axis (R5): legacy vs `impacts[]`, the array, and the collapse cost. → **`impacts[]`**;
        **54.6 % of issues differ by axis**; **14 issues carry two impacts today** and the facet cannot reveal it.
  - [x] Settle multi-location (`flows[]` / SARIF `relatedLocations`). → Carried, flattened, capped with an explicit
        truncation count. Measured **source-class dependent**: 15.5 % vs 0.1 %, max **52**.
  - [x] Settle naming (R2) and reuse-vs-parallel (R1). → **`AnalysisObservation`**; **parallel**, not merged.

- [x] **Task 4 — Define attachment, gates and all (AC: #2)**
  - [x] Specify `finding → file → {directory, story, epic, requirement}` with the hop count and approximation
        stated per edge. → Report § 7; **`requirement` is NOT a key** (two hops, epic-granular).
  - [x] State the `--deep-git`-off behavior explicitly, and how the payload advertises that attachment was
        unavailable rather than empty. → **100 % unattached** by default; mandatory non-nullable `attachment.basis`.
  - [x] Evaluate the work graph and say plainly if it cannot be the join (it carries no file nodes). → Confirmed,
        **and it has no requirement nodes either** — contrary to this story's description.
  - [x] Define the unattached route and name Story 26.6's hub as its destination. → **728 (31.7 %)** deep-git on,
        **2,300 (100 %)** off.

- [x] **Task 5 — Compare channels and recommend (AC: #3)**
  - [x] Build the comparison table. **Four rows minimum**: digest artifact, Epic 22 IR field, Sonar's official
        MCP server, a SpecScribe-emitted MCP surface. → **Five rows** (the shipped stderr channel added as baseline).
  - [x] Per row: framework-neutrality, offline behavior, new-runtime cost, **fingerprint impact**, staleness
        honesty, and whether an agent can consume a *subset* (the 25.4 use case). → Report § 10, plus a
        BMad-neutrality row and a credential row.
  - [x] Recommend **for Story 25.4** and **for Epic 26** separately if the answers differ. Say so if they do. →
        **They differ.** 25.4 → sharded digest; Epic 26 → IR field.
  - [x] State what 25.4 defers. → Report § 10.4.

- [x] **Task 6 — Write the report (AC: #1–#4)**
  - [x] `_bmad-output/implementation-artifacts/25-3-spike-report.md`. Follow the structure of
        [24-6](24-6-graph-engine-spike.md)'s and [23-1](23-1-spike-report.md)'s reports: findings numbered and
        citable, negatives reported as loudly as positives, unmeasured axes named as unmeasured. → **F1–F7**
        numbered; **§ 14 names seven unmeasured axes.**
  - [x] Include a **§ Handoff** naming, per story: 25.4, 26.2, 26.3, 26.4, 26.5, 26.6 — and what each receives.
  - [x] Add a note to **26.1** (visual ideation): the severity vocabulary and text labels it must render, so it
        does not invent a second one. Add a note to **26.7**: whether the contract generalizes to a pluggable
        provider seam or is bespoke per service. → Both written; 26.7 gets "proven on two, not proven general".
  - [x] Add a note to **Epic 27** (FR42, coverage): coverage is a per-file *metric*, not a finding — say whether
        it rides this contract or is deliberately outside it. Epic 27 was kept separate for a reason; confirm or
        challenge that reason on evidence. → **Separation upheld**, with the *uncovered-lines range* named as the
        one genuine edge for Epic 27 to decide rather than inherit.

- [x] **Task 7 — Author and ratify the ADR (AC: #4)**
  - [x] Verify the next free number (0019 is claimed by 18.3, unwritten). Author `docs/adrs/00NN-*.md` in the
        house format: Status line, Context, Decision(s), Options considered, Consequences. → **The story's guess
        of 0021 was stale**: 0020/0021/0022 all landed since authoring. `0019` still claimed-but-unwritten by
        **both** 18.3 **and** 22.3. **ADR 0023** authored.
  - [x] Add the one-line entry to `docs/adrs/README.md`. → Added; index now lists 22 records.
  - [x] **Get owner ratification.** Status `Accepted`, not `Proposed`, before this story goes to review.
        → ✅ **Ratified by the owner during this dev pass, 2026-07-28.** ADR 0023 and its `README.md` index entry
        both read **Accepted**. This makes 0023 the **first Accepted ADR since 0015** — 0016–0018 and 0020–0022 all
        remain Proposed.

- [x] **Task 8 — Prove the spike shipped nothing (AC: all)**
  - [x] `git status` — no `src/`, `tests/`, `web/`, `extension/` edits attributable to this story. If a
        concurrent session's edits are in the tree, say so and leave them (CLAUDE.md: never `git reset --hard`,
        `git checkout --`, or `git clean`). → **A concurrent session's Story 22.4 work is in the tree and was
        left untouched.** `Commands.cs` entered the diff *mid-pass*.
  - [x] Full suite green and `GoldenContentFingerprint` **unmoved**. If it moved, either you edited `src/` or a
        sibling session did — determine which and record it. → **2,658 passed / 0 failed / 3 skipped**;
        fingerprint test green standalone and in-suite.
  - [x] Confirm `spike/findings/` is referenced by no project file and the generated site is byte-identical with
        and without it (the `spike/README.md` guarantee). → **Tested, 0 differences** (report § 13.3). Required
        normalizing the per-run footer stamp first, and surfaced **F7** (the deep-git silent loss) along the way.

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

claude-opus-5 (dev-story, 2026-07-28)

### Debug Log References

- Live Sonar re-measure: `api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false` — **1,466**
  unresolved (1,360 → 1,420 → 1,466 over three days).
- Raw SARIF: `dotnet build <proj> -t:Rebuild -p:ErrorLog=<abs>%2cversion=2.1`, **one project at a time**.
  `src` 261 + `tests` 573 = **834** results.
- Two-way mapping: `python spike/findings/map_to_model.py <scratch>` — 1,466 Sonar + 834 SARIF → one model.
- Channel sizing: `python spike/findings/measure_channels.py <scratch>`.
- Attachment: `specscribe generate --deep-git`, then parsed `impact-map.html`'s embedded hierarchy JSON
  (1,166 nodes) and all 162 generated story pages.
- Suite: `dotnet test` → **2,658 passed / 0 failed / 3 skipped**.
- Inertness: two identical `generate` runs → 0 diff after normalizing the footer stamp; `spike/findings/` parked →
  0 diff.

### Completion Notes List

All nine required items are discharged; the one open item is owner ratification.

1. **The findings model, named deliberately, reuse-vs-parallel recorded (AC #1, R1, R2).** The record is
   **`AnalysisObservation`** — "finding" is taken by the *parsed* `## Review Findings` story section
   (`EpicsParser.cs:253` → `EpicsView.cs:303` → `<h3>` at `HtmlRenderAdapter.Epics.cs:609`), exactly where Story
   26.5 wants to put analysis results. **Parallel, not merged**, split on subject. The losing argument is recorded
   in both report § 2 and ADR Decision 2. *New finding beyond R1's table:* `Commands.SerializeDiagnostics` is
   **2-level** — `DiagnosticSeverity.Info` silently becomes `"warning"` — so "reuse the existing serialization" is
   cheaper on paper than in fact.
2. **SARIF answered three ways (AC #1, R4).** **A named profile of SARIF 2.1.0.** Not *is* (no planning
   vocabulary; **2.6×** the bytes at 1,793 B/result vs 678; results carry only a `ruleIndex` into an out-of-line
   rule catalogue, so one result is not self-describing); not *diverges* (forfeits Roslyn/GitHub/Sonar interop).
   Confirmed live: OASIS Standard + Errata 01, 28 Aug 2023, `level` 4-valued defaulting to `warning`. *Also found:*
   SARIF's `result.kind` is a **separate** axis — pinned to `fail` rather than left undefined.
3. **Severity axis settled (AC #1, R5).** Read **`impacts[]`**; carry every provider value verbatim; normalize to
   SARIF's `level` enum so the raw-SARIF direction is lossless. **54.6 % (800/1,466) of issues land on a different
   normalized level depending on the axis.** ⚠ **The story's own R5 inference is wrong**: the `impactSeverities`
   facet **counts issues, not impact pairs**, so it can never reveal the array — and **14 issues carry two impacts
   today**. A scalar severity is lossy on live data now. Collapse cost stated: BLOCKER (1) and HIGH (120) merge,
   so the **single BLOCKER is invisible** at normalized granularity.
4. **Second source class proven on RAW Roslyn SARIF, both losses listed (AC #1, R3).** 834 results that never
   touched Sonar, reconciling to Sonar's 819 imports. Independence is honest: the *acquisition path* is fully
   independent, the *rule content* overlaps — but **2 rules appear only in raw SARIF** and **37 `csharpsquid`
   rules only in Sonar**. Losses tabulated both directions (report § 8.2–8.3). *New finding:* SARIF's
   `artifactLocation.uri` is an **absolute `file://` URI carrying the build machine's path** — normalization is a
   correctness requirement, not tidiness.
5. **Attachment specified with the gate and its silent-loss mode (AC #2, R6, R7).** ⚠ **The gate sites moved** —
   `SiteGenerator.cs:388`/`:774`, not `:357`/`:739`. **The headline is F3:** the join amplifies — observation-weighted
   fan-out **7.33 epics / 10.02 stories**, 1,572 attached observations → **15,758 story edges**, `specscribe.css` →
   **64 stories**, `SiteGenerator.cs` → **18 of 19 epics**, 67 % of attached observations landing on ≥5 epics.
   Story 26.5's "use the existing miner" is correct **and insufficient**. `requirement` is **not** a key.
6. **Unattached route named with the expected count (AC #2).** Story 26.6's hub. **728 (31.7 %)** with `--deep-git`
   on; **2,300 (100 %)** with it off — the default.
7. **Channel table, five rows, separate recommendations (AC #3, R8, R9).** 25.4 → **sharded gitignored digest**
   (8.9 KB index + median 3.7 KB shards vs 1.49 MB whole), fingerprint-safe. Epic 26 → **IR field**, which moves
   the fingerprint 25.4 forbids and 26.4 expects. Sonar's official MCP server **confirmed live** (supports Cloud,
   documents Claude Code, needs a token) — **adopt as a complement, never as the contract**. SpecScribe-emitted MCP
   **deferred to its own ADR**.
8. **ADR authored and indexed (AC #4).** ⚠ **Numbering: the story's guess of 0021 was stale** — 0020/0021/0022 all
   landed between authoring and this pass, while **`0019` remains claimed-but-unwritten by BOTH 18.3 and 22.3**.
   **[ADR 0023](../../docs/adrs/0023-agent-facing-analysis-observation-contract.md)**, listed in `docs/adrs/README.md`.
   ADRs cited by symbol/section, never line number.
9. **Handoffs written (AC #4).** 25.4 and 26.2–26.6, plus notes to 26.1 (severity vocabulary and labels only — no
   layout), 26.7 ("proven on two, not proven general"), and Epic 27 (separation **upheld on evidence**, with the
   *uncovered-lines range* named as the one real edge).

**✅ Ratified.** AC #4 requires the ADR **Accepted, not Proposed**, before this story goes to review, because six
downstream stories bind to it. **The owner ratified ADR 0023 during this dev pass (2026-07-28)**; the record and
its `docs/adrs/README.md` index entry both read **Accepted**. Worth noting for the epic retrospective: **0023 is
the first Accepted ADR since 0015** — 0016, 0017, 0018, 0020, 0021, and 0022 all still sit at Proposed, several of
them also load-bearing for in-flight epics.

**Bonus finding, F7 — the deep-git silent loss reproduced live.** `generate --deep-git` was run 8× and returned
**739 pages on some runs and 436 on others** — the no-deep-git page set, all ~304 `commit/*.html` gone — at
**`errors=0` every time**. Cited from project memory in § 7.4, now first-hand. It makes `attachment.basis`
load-bearing rather than defensive: without it, a consumer sees "attaches to no story" on 100 % of observations on
a run that reported success. **Corollary for 25.4/26.5: never cache attachment across runs without its `basis`.**

**Shared-`main` conditions.** A concurrent session's Story 22.4 work (`SpaDelivery.cs`, `SiteGeneratorSpaTests.cs`,
`Commands.cs`, `web/ir/*`) was in the tree throughout and was **left untouched**; `Commands.cs` entered the diff
mid-pass. No destructive git commands were run. The full suite is green **with** those changes present.

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

**Added (durable):**

- `_bmad-output/implementation-artifacts/25-3-spike-report.md`
- `docs/adrs/0023-agent-facing-analysis-observation-contract.md`

**Added (disposable — `spike/findings/`, quarantined per `spike/README.md`, inertness tested):**

- `spike/findings/roslyn-specscribe.sarif` (572,435 B, 261 results)
- `spike/findings/roslyn-tests.sarif` (922,781 B, 573 results)
- `spike/findings/map_to_model.py` (the two-way mapping — AC #1's demonstration)
- `spike/findings/measure_channels.py` (digest sizing — AC #3's numbers)

**Modified:**

- `docs/adrs/README.md` (one appended index entry for ADR 0023)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status + notes)
- `_bmad-output/implementation-artifacts/25-3-agent-facing-findings-contract-spike.md` (this file)

**Untouched, as required:** everything under `src/`, `tests/`, `extension/`, `web/`, and `SpecScribe.slnx`.
Modifications present in those paths belong to a **concurrent session's Story 22.4** work and were left alone.

**Not committed:** `SpecScribeOutput/` (gitignored, verified `git check-ignore` → `.gitignore:490`),
`spike/findings/__pycache__/`.

### Change Log

| Date | Change |
|---|---|
| 2026-07-27 | Story created by `create-story` at baseline `40c7ee9`. Nine reconciliations recorded against shipped code and live Sonar data. |
| 2026-08-07 | `code-review` pass, three layers in parallel (Blind Hunter, Edge Case Hunter, Acceptance Auditor). Scoped by File List; `docs/adrs/README.md` attributed **by hunk** — only the ADR 0023 index line is in scope, the 0020/0021/0022/0024 lines belong to Stories 18.5/18.4/23.5/22.4. All four ACs confirmed substantively met and all nine checklist items discharged by durable content, not merely claimed. The ADR's structural decisions hold; the **evidentiary layer beneath them** does not. 7 decision-needed, 11 patch, 2 deferred, 9 dismissed. See § Review Findings. |
| 2026-07-28 | `dev-story` pass at session HEAD `06b300c`. Tasks 1–8 complete except owner ratification of ADR 0023. Contract decided: **`AnalysisObservation`**, a named profile of SARIF 2.1.0, parallel to `DiagnosticNotice`, severity read from `impacts[]` and normalized to SARIF's `level` enum. Evidence: 1,466 live Sonar issues + 834 raw Roslyn SARIF results mapped both ways. Seven numbered findings (F1–F7), seven axes named unmeasured. Four of this story's own facts corrected: the gate sites moved (`:388`/`:774`), the `impactSeverities` facet cannot reveal the impacts array (and 14 issues carry two), the work graph has no requirement nodes either, and the ADR number is 0023 not 0021. Suite 2,658 passed / 0 failed / 3 skipped; `GoldenContentFingerprint` unmoved; `spike/findings/` proved byte-inert. |

### Review Findings

<!-- Deliberately an h3. `## Review Findings` is a PARSED section (EpicsParser -> EpicsView.ReviewFindingsHtml
     -> <h3> on the story page) — this story's own R2. An h2 here would inject the code review into the portal. -->

**Code review 2026-08-07** — three layers in parallel. Scoped by this story's File List per CLAUDE.md § Scoping a
code review; `docs/adrs/README.md` attributed **by hunk** (only the ADR 0023 index line — the 0020/0021/0022/0024
lines are Stories 18.5/18.4/23.5/22.4 and are excluded). The two `.sarif` files were verified by property (counts,
sizes, path leakage), not read line by line. Everything else in commits `c1a6ee5` / `b696485` excluded.

All four ACs are substantively met and all nine of the checklist items above are discharged by real content in the
durable deliverables. The ADR's *structural* decisions — parallel-not-merged, profile-not-SARIF, labelled
attachment, revision-first provenance — are sound and well argued. What did not hold up is the **evidentiary layer
beneath them**, and the number of load-bearing fields a *ratified* contract leaves undefined.

#### Decision needed

- [x] [Review][Decision] **The attachment and sizing corpus double-counts ~819 defects** — § 1.1 measures 834 raw SARIF against Sonar's 819 `external_roslyn:*` imports and calls them reconciled (CA1861 339 vs 338), then §§ 7 and 10.1 **union** both sets with no dedup (`map_to_model.py:250-251`, `measure_channels.py:36`) for N = 2,300 where the distinct population is ~1,481. Inflated: 1,765 (76.7 %), 1,572 (68.3 %), 728 (31.7 %), 535 (23.3 %), the 12,931 / **15,758** edges and the 7.33 / **10.02** means — **including F3, "the single most consequential handoff in this report"** — plus all of § 10.1's sizing (1.49 MB, 201 shards, 8.9 KB index, 3.7 KB median). ADR Context fact 3, Decision 7 and the README entry restate them as measured fact. Riding along: the **2.6× SARIF claim compares indented JSON against minified JSON**; 1,793 B/result is unreproducible from the shipped files (1,455,266 ÷ 834 = **1,745**); "18 of 19 epics" uses the subset-with-data as denominator (§ 7.2 records 19 of **27** epic pages). Compounding in the **opposite** direction: the sized record omits the mandatory `attachment` and `provenance` blocks and carries `rule.name`/`helpUri` = null on all 1,466 Sonar rows (`:148-149`) — the field § 4 calls "the single biggest agent-ergonomics change". Net error unknown. **Options:** (a) re-measure with dedup — but `sonar_p1..3.json` was never committed and `resolved=false` is as-of-now, so that snapshot is unrecoverable; (b) caveat the ADR/report/README and demote the figures to order-of-magnitude; (c) accept and record the limitation.
- [x] [Review][Decision] **The contract defines no observation identity and no deduplication rule** — the spike measured ~819 same-defect duplicates across its own two providers; § 11 tells 26.7 to build "pluggable normalizers, one shared `AnalysisObservation`"; § 10 recommends running Sonar's MCP server *alongside* the digest. ADR 0023 never uses the word "duplicate", and Decision 4 drops Sonar's `key` as unstable — sound — without putting anything in its place. 26.4 and 26.6 each invent a merge rule or ship every Roslyn defect twice, and no two digests can be compared for new-vs-resolved. Not a legitimate deferral: it is the one thing multi-provider support cannot be built without, and the spike had the data to settle it.
- [x] [Review][Decision] **`attachment.basis` cannot produce the three-way distinction it exists for** — Decision 5 mandates it so consumers can separate *genuinely unattached* / *never computed* / *attempted and failed*, but **F7's own evidence defeats it**: the deep-git timeout returns `errors=0` **and zero commits**, byte-identical at the cited gate (`progress?.DeepGit?.Commits is { Count: > 0 }`) to deep-git being off. The enum also mixes one *method* value with two *outcome* values, so "ran, mined, matched nothing" is legally expressible two ways; partial success (the 300-commit horizon) has no value; and attachment mined against a different revision than the observations describe — the normal case, since `isStale: true` is expected — has no representation.
- [x] [Review][Decision] **AC #1's source-agnosticism proof is weaker than the ADR and README claim** — (i) the `_lost` ledger is **hand-written, not derived**: nothing enumerates the provider's key set and subtracts the mapped ones. `from_sonar` makes 8 `lost.append` calls; `from_sarif` makes 4 plus one **unconditional constant** (`:212`) which is what manufactures § 8.3's "No analogue at all | **834**" row — a hard-coded string in a table of measurements. Never counted on the SARIF side: `suppressions[]`, `baselineState`, `rank`, `fingerprints`, `codeFlows`, `logicalLocations`, `region.snippet`, per-location `message`, `rule.fullDescription`; on the Sonar side: `creationDate`/`updateDate`, `status`/`resolution`, `author`, `quickFixAvailable`. So § 8.3's "**The asymmetry is the finding**" is substantially an artifact of the instrument — and it is the sole evidence offered for AC #1. (ii) § 8.1 concedes the two sources are the same analyzer family, exactly what **R3** warned would prove nothing, yet the README entry reads "**genuinely independent** second source class" and the ADR says "disjoint serializations, disjoint severity scales" without noting they are the same analyzers. The qualifying concession stays in the body; the unqualified claim travels.
- [x] [Review][Decision] **`severity.provider` — the ADR's designated escape hatch — is specified only in Sonar's vocabulary** — Decision 4 defines it as `{softwareQuality, severity}` pairs plus legacy `severity`/`type`; all three are Sonar concepts, and the ADR never states the shape for a non-Sonar producer. The SARIF branch emits an unrelated `{axis: "sarif", level, defaultLevel}` (`:220-221`) the ADR does not describe. Yet the ADR routes the **entire stated collapse cost** through this field ("the single BLOCKER … survives only in `severity.provider`") and § 11 tells 26.6 to read it for the dashboard. Six stories parse a field whose only documented schema is one provider's.
- [x] [Review][Decision] **BMad-neutrality is asserted in a table cell and specified nowhere** — AC #3 bound it explicitly ("state what the contract does in a repo using Spec Kit, GSD, or no framework at all"). The entire treatment is one cell at report `:469`. Neither report nor ADR contains "Spec Kit", "GSD", "no framework", or any reference to Epics 11–15. Contradicted by the schema too: `basis` is **mandatory and non-nullable**, yet none of its values means "this repo has no planning model" — `unavailable` conflates that with "deep-git was off" — and `epics`/`stories` are BMad vocabulary with no omission rule. NFR8 is the property the spike exists to establish.
- [x] [Review][Decision] **SARIF `suppressions[]` is never read and the contract never mentions it** — the Sonar side is filtered `resolved=false`; the SARIF side has no equivalent, so a `#pragma warning disable` or `[SuppressMessage]` diagnostic enters the model indistinguishable from an open one and reaches story pages via 26.5. For a record whose stated posture is "a third party's claim about the code, not a verdict the project has accepted", ingesting the developer's explicit *rejection* of that claim is a product decision, not an oversight to patch silently.

#### D1 resolved — re-measured with deduplication, 2026-08-07

Owner chose **re-measure**. Executed by [`spike/findings/remeasure_dedup.py`](../../spike/findings/remeasure_dedup.py),
which paginates to exhaustion and **refuses to report a corpus it knows is truncated** (the shipped
`map_to_model.py` capped at 3 pages = 1,500; the live backlog is now **1,755**, so that cap would silently truncate
today — the deferred self-invalidation finding, confirmed in practice).

**This is a fresh measurement at today's revision, not a reconstruction of 2026-07-28.** `sonar_p1..3.json` was
never committed and `resolved=false` is an as-of-now query, so the original 1,466-issue snapshot is unrecoverable.
The raw-SARIF half *is* the committed 2026-07-28 evidence.

**The double-count is confirmed and large.** Matching raw SARIF results against live Sonar issues:

| Dedup key | Overlap | Distinct population | Inflation of the naive union |
|---|---|---|---|
| `(rule, path, line)` — exact | 390 | 2,199 | 17.7 % |
| `(rule, path)` — line-drift tolerant | **810** | **1,779** | **45.5 %** |

The exact key undercounts because the SARIF is 10 days older than the live Sonar data and lines have moved; the
looser key's **810** reconciles with the report's own 995 `external_roslyn:*` imports and its § 1.1 claim of ~819.
So the true overlap sits near the upper bound, and `map_to_model.py`'s union inflates by roughly **45 %**, not the
~35 % first estimated. Applied to the report's own corpus, N = 2,300 corresponds to ~1,481 distinct.

**But the sizing error runs the other way, and the other way wins.** Isolating each correction:

| Corpus | Record | Observations | Whole digest | B/obs | Median shard |
|---|---|---|---|---|---|
| union | truncated (as shipped) | 2,589 | 1.26 MB | 511 | 2,822 B |
| **distinct** | truncated | 2,199 | 1.09 MB | 520 | 2,267 B |
| union | **full ADR 0023 record** | 2,589 | 2.48 MB | 1,003 | 5,289 B |
| **distinct** | **full ADR 0023 record** | **2,199** | **2.12 MB** | **1,012** | **4,243 B** |

Adding the mandatory `attachment` and `provenance` blocks and a populated `rule.name`/`helpUri` **doubles** the
per-observation cost (511 → 1,012 B). Dedup removes ~15 % of rows. **Net: the digest is larger than § 10.1 reports,
not smaller** — so "net error unknown" resolves as *the truncated-record error dominated the double-count*.

> ⚠ **This partially invalidates ADR 0023 Decision 3's first measured reason for rejecting plain SARIF.**
> The "**2.6×** the bytes at 1,793 B/result vs 678" comparison is indented JSON against minified JSON.
> Like for like: the SARIF minifies to **838,725 B = 1,006 B/result** (indentation alone is **42.4 %** of the file),
> against **1,012 B** for a full ADR-0023 observation. **The ratio is ~1.0×, not 2.6× — the profile is not smaller
> than SARIF at all.** Decision 3's *other* reasons stand untouched (no planning vocabulary; a `result` carries only
> a `ruleIndex` into an out-of-line catalogue and so is not self-describing), and they are sufficient on their own —
> but the byte argument must be withdrawn rather than restated.

Also corrected: `1,793 B/result` is not reproducible from the shipped files — 557,605 + 897,661 = 1,455,266 ÷ 834 =
**1,745 B**.

#### Patch

- [x] [Review][Patch] Withdraw the 2.6× byte argument from ADR 0023 Decision 3, the options table and the README entry; like-for-like it is ~1.0× (SARIF minified 1,006 B/result vs a full observation 1,012 B). Decision 3's other two reasons stand and are sufficient [docs/adrs/0023-agent-facing-analysis-observation-contract.md:50]
- [x] [Review][Patch] Correct § 10.1's sizing to the deduplicated, full-record figures (2,199 observations, 2.12 MB whole, 1,012 B/obs, 4,243 B median shard) and state that the shipped figures omitted the mandatory `attachment`/`provenance` blocks [25-3-spike-report.md]
- [x] [Review][Patch] F3's executive-summary row crosses granularities — 1,765 (epic-attached) paired with 15,758 (story edges); § 7.3 correctly pairs 1,572 ↔ 15,758 and 1,765 ↔ 12,931, so a summary reader computes 8.9× instead of 10.02× [25-3-spike-report.md:25]
- [x] [Review][Patch] § 7.6's directory breakdown is the wrong population — `src` 264 + `tests` 234 + `web` 37 = **535**, the *epic*-granularity unattached set, under the *story*-granularity 728 sentence; same root cause as the F3 swap [25-3-spike-report.md:358-360]
- [x] [Review][Patch] § 14 misquotes § 11 to manufacture a hedge § 11 does not contain — § 14 claims the 26.7 note says "proven on two", but § 11 reads "The contract **does generalize** — proven, not asserted". The ADR's Consequences already carry the honest wording; align § 11 to it [25-3-spike-report.md:625, :765]
- [x] [Review][Patch] `Commands.SerializeDiagnostics` does not resolve — the symbol is `WebviewCommand.SerializeDiagnostics` (class `Commands.cs:78`, member `:565`); appears in ADR Decision 2, the options table, the README entry and report § 2, while § 13.1 boasts "cited **by symbol**". F5's substance is confirmed live at `Commands.cs:593` [docs/adrs/0023-agent-facing-analysis-observation-contract.md:42]
- [x] [Review][Patch] "`<h3>Review Findings</h3>` on **every** story page" is false — guarded by `if (view.ReviewFindingsHtml.Length > 0)`, and review findings are an epic-end activity, so most story pages lack it. The bolded "every" carries ADR **Decision 1**, the naming decision, and appears identically in report § 3 and the README entry [src/SpecScribe/HtmlRenderAdapter.Epics.cs:680]
- [x] [Review][Patch] `directory` — one of AC #1's five named keys — is dropped with no record in the ADR (zero occurrences of "director*"); `requirement` got a full options-table rejection row, `directory` needs the same [docs/adrs/0023-agent-facing-analysis-observation-contract.md:110-123]
- [x] [Review][Patch] `relatedLocationsTruncated` is mandated by Decision 4 but named only in the disposable report, and its semantics (count dropped vs original total) are undefined — guaranteeing an off-by-one at an unstated boundary across 26.4 and 26.6 [docs/adrs/0023-agent-facing-analysis-observation-contract.md:69]
- [x] [Review][Patch] `tags` is in neither state — report § 8.2 says Sonar tags are "folded into the optional `tags` field", but `observation()` has no `tags` key, `from_sonar` appends them to `_lost`, and the ADR neither defines the field nor lists it among Decision 4's deliberate drops [spike/findings/map_to_model.py:67-83, :140-141]
- [x] [Review][Patch] `spike/README.md` has no `spike/findings` section — both `spike/vscode` and `spike/graph-engine` carry one naming the durable output and when the evidence may be deleted; 1.46 MB of committed SARIF has nothing on disk recording it is disposable. The inertness *guarantee* holds (`SpecScribe.slnx` matches "spike" zero times); the *index* does not [spike/README.md]
- [x] [Review][Patch] ADR 0023 schema-completeness pass — fields left undefined for six binding implementers: `confidence` constrained but never enumerated (and its "for epic or story" qualifier names an unreachable case); `entityCount` a single scalar over a **two-granularity** attachment (7.33 epics vs 10.02 stories) and undefined when `basis` is `unavailable`; `severity.normalized` undefined when the provider supplies none; `location.path` declared non-null but null in practice for project-level issues, with no behavior on violation; the repo-relative rule stated for `location.path` and **silently not extended** to `relatedLocations[].path`; the sharded digest's shard key, filename encoding, traversal sanitization, MAX_PATH and case-collision rules all unspecified; `level: none` unreachable from Sonar while a `None` label is mandated; `kind` pinned to `fail` with no disposition for a non-`fail` input; rule identity not namespaced across providers with `provider` an unenumerated free string and no tool version anywhere [docs/adrs/0023-agent-facing-analysis-observation-contract.md]
- [x] [Review][Patch] An **Accepted** ADR binds a load-bearing decision to a still-**Proposed** one — AC #4 required 0023's ratification on the grounds that "a Proposed ADR is not a contract they can bind to", yet Decision 7 routes Epic 26 to the IR field and the Amendment-surface clause binds that to **ADR 0016, still Proposed**. Same argument, unapplied. (0002/0011/0014 are Accepted; 0020 was Proposed at authoring, ratified 2026-07-29) [docs/adrs/0023-agent-facing-analysis-observation-contract.md:102, :145]

#### Deferred

- [x] [Review][Defer] Throwaway-script robustness — crashes on the **clean-repo / zero-findings path** (five `ZeroDivisionError` sites, `sar_obs[0]` `IndexError`, a bare `next()` `StopIteration`, `median([])`), i.e. the state a 25.4 implementer most needs to see is the one that aborts; no `encoding=` on six `open()` calls (cp1252 mojibake on the host it ran on); unchecked `subprocess.run` return codes making the staleness probe fail **open** (blank "commit(s) BEHIND" on a shallow clone, meaningless count after a force-push); hard-coded 3-page pagination capping input at **1,500** against 1,466 growing ~50/day, so the evidence self-invalidates within a day while reporting the truncated total as complete; a missing intermediate page silently unionised at exit 0; `%20`-only decoding where § 8.4 specifies un-percent-encoding; the `except ValueError` fallback re-emitting the absolute build-machine path — the exact leak Decision 4 forbids — which the "0 paths escaped" check is structurally blind to since it tests only a `..` prefix; `relatedLocations` in the SARIF direction never re-rooted or decoded at all; negative `ruleIndex` → `rules[-1]`; unknown severity silently normalizing to the **quietest** level (fails open, contradicting the fail-closed posture mandated for `isStale`); missing SARIF `level` ignoring `rule.defaultConfiguration.level`, falsifying "lossless on severity" on that path [spike/findings/map_to_model.py] — deferred: `spike/findings/` is disposable, quarantined and tested byte-inert; the contract consequences are captured in the schema-completeness patch and the corpus decision
- [x] [Review][Defer] § 12's "0023 is the first Accepted ADR since 0015 — 0016–0018 and 0020–0022 all remain Proposed" has aged out — ADR 0020 and 0021 were ratified 2026-07-29 at the Epic 18 retrospective, one day after this report [25-3-spike-report.md] — deferred: correct when written; worth a line at the Epic 25 retro

#### Dismissed (9)

Recorded so a future review does not re-raise them: the 836 `file:///C:/…` URIs in the committed SARIF (`spike/findings/**` is spec-sanctioned evidence); ratification being self-attested (no violation, unverifiable from the diff by construction); stale line-number citations in §§ 3 and 7.1 (CLAUDE.md treats lines as approximate under concurrent work — the cite-by-symbol defects are patched above); the README entry restating the contract in prose (inherits, no independent defect); F1/F6/F7 being quantifications of the story's own R5/R6 rather than discoveries (the body is explicit — "The story's R5 **holds** and is now quantified" — only the § 0 table strips the hedge); self-congratulatory phrasing; the `relatedLocations` fidelity asymmetry (Sonar 6 keys, SARIF 2, no message — contract half patched, script half deferred); the Sonar MCP row stated as fact in the ADR while § 14 marks it unrun (the claims are drawn from published docs and are accurate); assorted crash-on-malformed-input paths folded into the deferred script item.
