---
baseline_commit: b6964855dc9e06ce48857c98864cbcf594a752f1 # `b696485` — HEAD at authoring time (2026-07-28)
epic: 25
nfr: [NFR11, NFR12, NFR8]
frs: []
depends_on: [25-3] # the ratified contract (ADR 0023) and the channel selection
blocks: [] # Epic 26 consumes the same ADR, not this story's artifact
ships_product_code: false # dev-time only. The golden fingerprint MUST NOT move. No `src/` edits.
adrs: [0023, 0014, 0022] # the contract; the `.specscribe` folder it lands in; Node as a sanctioned toolchain
touches:
  - "tools/analysis-digest/**" # NEW — the emitter
  - "CLAUDE.md" # the agent-facing consumption instruction
  - "docs/SonarCloudSetup.md" # refresh command + the Sonar MCP complement
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**, NOT .github/workflows/**
---

# Story 25.4: Agent-Consumable Findings Channel for SpecScribe's Own SDD Workflow

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer running create-story and dev-story,
I want the current analysis findings available to my agents in the channel Story 25.3 selected,
So that planning and implementation passes account for known quality debt in the files they are about to touch.

## ⛔ Read first — the spike's source recommendation was corrected, and four live facts

Story 25.3 is authoritative on **what shape** the data has (ADR 0023, **Accepted**). It is *not* authoritative
on **where the bytes come from**: its §10.5/§11 recommendation rested on a premise that is false for this
repository, and the owner has re-decided. Everything below was verified against the live API and the live
working tree on **2026-07-28** at `b696485`.

### 1. ⚠ The token premise is FALSE — the source is a LOCAL fetch, not a CI artifact

Spike §10.5 ranked three sources and recommended **path 1 ("read a CI-produced artifact")** *specifically
because* it read path 2 ("call the Sonar API from the dev machine") as **"needs a token"**. That is wrong for
`IntegerMan_SpecScribe`: it is a **public free-tier project and every endpoint this story needs answers
anonymously.** Verified by direct call, not inferred:

| Endpoint | Anonymous result |
|---|---|
| `api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false` | ✅ `total: 1483`, full issue payloads |
| `api/project_analyses/search?project=IntegerMan_SpecScribe` | ✅ 5 analyses with `revision` fields |
| `api/rules/show?organization=integerman-github&key=csharpsquid:S6444` | ✅ full rule metadata |

This is not a discovery — `docs/SonarCloudSetup.md` § Triaging findings already drives the **entire shipped
triage method** with bare `curl` and no credential, and Story 25.2 performed a whole baseline triage that way.
The spike simply did not reconcile its channel table against the doc its own epic had already written.

**OWNER DECISION (2026-07-28, create-story): the emitter fetches the public API directly from the dev
machine.** AC #1's "writes no token value anywhere" is satisfied by construction — there is no token in the
picture at any point.

**Record this correction in the story record**, and note it as a **constraint gift** to Story 26.2 rather than
a constraint: 26.2's credential design keeps a genuinely open field, and it now knows a public project needs
no credential for read.

### 2. Path 1 was not merely unnecessary — it does not work as written

Two blockers the spike did not price, both worth recording so nobody re-proposes it casually:

- **CI produces no findings artifact today.** `.github/workflows/build-test-analyze.yml` runs
  `begin → build → test → end` and nothing else; Sonar's results live server-side. Path 1 required a **new CI
  step** that the spike costed at zero.
- **`SonarScanner end` only *submits* a background task.** A step querying `api/issues/search` immediately
  after `end` returns the **previous** analysis's issues unless it polls `api/ce/task` to completion. The
  digest would silently describe the wrong revision — the precise failure mode ADR 0023 Decision 6 exists to
  prevent.

**Do not add a CI step in this story.** `.github/workflows/**` is out of scope.

### 3. The numbers moved again — re-measure before you cite anything

| | 25.2 (07-26) | 25.3 authoring (07-27) | 25.3 spike (07-28 am) | **now (07-28 pm)** |
|---|---|---|---|---|
| Unresolved issues | 1,360 | 1,420 | 1,466 | **1,483** |
| Distinct rules | — | — | 76 | **86** |

~+50 issues/day. Every figure in the spike report is a **snapshot, not a constant**. Re-measure at
implementation time and put *your* numbers in the story record. Derive the distinct-rule count from the
**fetched issues themselves**, not from the `rules` facet — the facet is capped and will silently
under-report once the repo passes 100 distinct rules.

### 4. ⚠ The analysis is currently NOT stale — so the naive staleness test proves nothing

```
latest analysis revision : b6964855dc9e06ce48857c98864cbcf594a752f1
local HEAD               : b6964855dc9e06ce48857c98864cbcf594a752f1   ← identical
git status --porcelain   : (clean)
```

`isStale` will read `false` and `commitsBehind` `0` on a first run today. **That is not evidence the staleness
block works.** You must exercise the stale path deliberately — e.g. compute against the prior analysis
revision `d1722f17a6f9fefdb50d3aab91a9b8bca805f4e7`, which *is* an ancestor of HEAD and is **3 commits
behind** (`git rev-list --count d1722f17..HEAD` → `3`, verified). Record both states.

### 5. ✅ Creating `.specscribe/` at the repo root is SAFE — verified, do not spend a round on it

`.specscribe/` **does not exist in this checkout**, so the emitter creates it. The obvious fear — that an empty
`.specscribe/` with no `config.json` would shadow real settings via the Story 5.2 walk-up — was checked in the
source and **does not happen**:

- `SettingsStore.TryLoad` (`src/SpecScribe/SettingsStore.cs:151`) walks up and, when a candidate yields no
  readable config, **continues to the next ancestor** rather than stopping. Its own doc comment says so.
- `ReadConfigJson` (`:207`) returns `null` for a folder with no `config.json` — not an exception.
- `FindExisting` has **no callers outside `SettingsStore.cs`** (grepped across `src/`, `tests/`, `extension/`).

**Do NOT write a `config.json`** to make the folder "valid". It is not needed and would fabricate settings.

## Acceptance Criteria

Verbatim from `epics.md` § Story 25.4. **This story does not extend them** — every owner decision below lands
*inside* these two ACs, so no `epics.md` amendment is required by this story. (Contrast Story 23.3, whose 8 ACs
extended the epic's 2 and therefore had to amend `epics.md` in the same change.)

1.
**Given** Story 25.3's ratified contract and selected channel
**When** the channel is implemented
**Then** current findings for this repository are emitted in the contracted shape and are demonstrably
consumable by an agent during a real create-story or dev-story pass, with a worked example recorded
**And** the implementation honors NFR12: it is opt-in, produces nothing rather than failing when findings are
unavailable, and writes no token value anywhere.

2.
**Given** this is dev-time tooling, not a product feature
**When** it ships
**Then** it does not alter SpecScribe's generated portal output — the golden fingerprint is unmoved — and any
code added is quarantined from the generation critical path, with Epic 26 named as the epic that makes
findings a *product* surface
**And** staleness is honest: consumers can tell how old the analysis is and when it predates the working tree.

## Owner decisions locked at create-story (2026-07-28)

| # | Decision | Rationale |
|---|---|---|
| **D1** | **Source = local anonymous SonarCloud API fetch.** Not a CI artifact, not a token. | § Read-first 1 and 2. No credential, no CI change, no `gh auth`, no round-trip staleness, no `ce/task` polling trap. |
| **D2** | **Emitter = Node ESM under `tools/analysis-digest/`.** Not Python, not C#, and **never** `src/SpecScribe`. | ADR 0022 ratifies Node as a sanctioned build toolchain; `tools/` already holds exactly this shape (`plotly-vendor`, `prism-vendor`: `package.json` + a build script); `tools/**` is **already** in the `sonar.exclusions` list (`build-test-analyze.yml:191`); the Node version is already pinned (`web/.nvmrc` = **24.11.1**). No new runtime enters the repo. |
| **D3** | **Consumption seam = a `CLAUDE.md` section + `docs/SonarCloudSetup.md`.** No project-local skill. | `CLAUDE.md` is auto-loaded every session, so an agent consults the digest *unprompted* — a skill is only read when invoked. The repo currently has **zero** local non-BMad skills; inventing one is a new convention for a dev-time tool. |
| **D4** | **Provider = SonarCloud only.** The raw-Roslyn direction stays proven-but-unshipped. | **819 of the 1,483 Sonar issues already ARE the Roslyn results**, imported as `external_roslyn:*`. Adding raw SARIF would duplicate ~819 observations to gain ~15 plus 2 rules Sonar never imports. Keep `provider` in the payload and keep the mapper's provider seam so a second source is additive later — do **not** ship a second source now. |
| **D5** | **Attachment is emitted as `basis: "unavailable"` and is NOT computed.** | ADR 0023 Decision 5 mandates the block with a non-nullable `basis`; it does **not** mandate computing the join. Computing it requires `generate --deep-git` (6.5 s+, and the F7 silent-loss defect reproduced 8 times in the spike), and the 10× fan-out bounding rule is **explicitly Story 26.5's design decision and the owner's to approve**. The digest is file-keyed; an agent working a story already knows its files. |

## Tasks / Subtasks

- [ ] **Task 1 — Re-measure the inputs and pin them** (AC: #1)
  - [ ] Re-run the three anonymous endpoints; record today's `total`, distinct-rule count (from the issues,
        not the facet), latest `analysisRevision` + `analysisDate`, and local `HEAD`.
  - [ ] **Always pass `resolved=false`.** Unfiltered responses include CLOSED issues on paths the exclusion
        list removed — 1,598 vs 1,420 on 07-27 (`docs/SonarCloudSetup.md` § Triaging findings, Step 1).
  - [ ] Page with `ps=500` (3 pages at current volume). Note Sonar's hard `p × ps ≤ 10000` ceiling in a code
        comment so a future volume increase fails loudly rather than truncating.
  - [ ] Confirm `impacts` is present in the default payload (**verified present 2026-07-28** —
        `[{softwareQuality: MAINTAINABILITY, severity: MEDIUM}]`). If it ever goes missing, the mapper's
        fallback to the **frozen legacy axis** is a recorded loss, never a silent one.

- [ ] **Task 2 — Build the emitter at `tools/analysis-digest/`** (AC: #1, #2)
  - [ ] `package.json` + `index.mjs` (+ a short `README.md`), matching the `tools/plotly-vendor` shape.
        `"type": "module"`. **Zero runtime dependencies** — `fetch` and `node:child_process` are enough on
        Node 24.
  - [ ] Port the two mapping functions from `spike/findings/map_to_model.py` — `from_sonar` is the one this
        story needs; keep `from_sarif`'s seam shape in mind but **do not port it** (D4). The Python is
        **throwaway reference, not a dependency**: do not shell out to it, do not import it, do not move it.
  - [ ] Emit the ADR 0023 record exactly: `provider`, `rule{id,name,helpUri}`,
        `severity{normalized,label,provider[]}`, `location{path,startLine,startColumn,endLine,endColumn}`,
        `relatedLocations[]`, `message`, `attachment{basis,entities,confidence,entityCount}`.
  - [ ] **`severity.normalized` is derived from `impacts[]` (MQR), taking the MAX** — never the legacy axis
        (they disagree on **54.6%** of issues). `severity.provider` is an **array** carrying every MQR pair
        **plus** the legacy `{severity, type}` verbatim. `severity.label` (`Error`/`Warning`/`Note`/`None`)
        ships **in the payload** — UX-DR17 is satisfied by the contract, not by a renderer.
  - [ ] Pin `kind: "fail"` (ADR 0023 Decision 3).
  - [ ] **`location.path` is repo-relative and forward-slashed.** Sonar's `component` is `PROJECT:path` —
        split on the first `:`. A component with **no** `:` is a project-level issue with **no file**; route
        it to the unlocated shard (Task 4), never drop it.
  - [ ] Flatten `flows[]` (flows-*of*-locations, two levels) into flat `relatedLocations`. **No cap in the
        emitter** — capping is a *surface* concern (26.4); if you ever do cap, an explicit truncation count
        is mandatory and silent truncation is forbidden.
  - [ ] Drop deliberately, and say so in a comment: `assignee` (no people scoreboard), the Sonar `key`
        (server-assigned, **not stable across re-analysis of a moved line**), `hash`, `effort`/`debt`,
        `cleanCodeAttribute` (Sonar-only taxonomy — carrying it would make the model Sonar-shaped).

- [ ] **Task 3 — Provenance and honest staleness** (AC: #2)
  - [ ] Fetch the newest `analyses[0]` from `api/project_analyses/search` → `analysisRevision`, `analysisDate`.
  - [ ] Stamp `workingTreeRevision` from `git rev-parse HEAD` and `workingTreeDirty` from
        `git status --porcelain`.
  - [ ] `commitsBehind` = `git rev-list --count <analysisRevision>..HEAD`; **`null` when not computable**
        (the analysis revision may simply not exist locally on a shallow or unfetched clone).
  - [ ] **`isStale` FAILS CLOSED — it defaults to `true` whenever it cannot be computed.** A staleness field
        that fails open defeats its own purpose.
  - [ ] **A dirty tree is a staleness condition too.** Observation line numbers are anchored to
        `analysisRevision`; uncommitted edits move them. `workingTreeDirty: true` must be visible to the
        consumer and stated in the CLAUDE.md guidance.
  - [ ] ⚠ **Read-time staleness is the real requirement, and a frozen field cannot carry it.** A digest
        emitted at `X` with `isStale: false` becomes a **lie** the moment the next commit lands. Emit
        `provenance.evaluatedAtRevision` alongside `workingTreeRevision` and state the consumer rule
        explicitly in `CLAUDE.md`: *if `git rev-parse HEAD` differs from `evaluatedAtRevision`, the digest is
        stale regardless of what `isStale` says — re-run the emitter.*
  - [ ] Exercise **both** states and record them (§ Read-first 4): the not-stale case is today's default and
        proves nothing on its own.

- [ ] **Task 4 — Index + shard layout** (AC: #1)
  - [ ] `.specscribe/analysis/index.json` — the entry point. Carries `schema`, the full `provenance` block,
        `totals {observations, files, byLevel{}}`, and `files{}` mapping repo-relative path →
        `{count, byLevel{}, shard}`.
  - [ ] **Shards mirror the source tree**: `.specscribe/analysis/files/<repo-relative-path>.json` — e.g.
        `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`. An agent can
        construct the shard path from the file it is about to touch **without reading the index at all**.
        Still emit an explicit `shard` field per entry so the derivation never has to be guessed if a path
        needs escaping.
  - [ ] Project-level issues with no path go to `.specscribe/analysis/unlocated.json` and are counted in
        `totals`. They are a **routed population, never a residue**.
  - [ ] Size targets from the spike, to re-measure not to assume: index ~8.9 KB, **201** shards, median shard
        **3,691 B**, against a **1.49 MB** monolith. The design exists *because* the use case is "the files I
        am about to touch"; a typical three-file dev-story pass should read **~20 KB**.
  - [ ] **Write atomically** — build into a temp directory and swap, so an interrupted run never leaves a
        half-written digest that an agent reads as authoritative.

- [ ] **Task 5 — Rule metadata, fetched once and cached** (AC: #1)
  - [ ] `rule.name` and `helpUri` are **absent** from `api/issues/search`; each distinct rule needs
        `api/rules/show?organization=integerman-github&key=…`. **`organization` is required** — omitting it
        returns an error, not a rule. Budget **~86 calls** (was 76 two days ago).
  - [ ] Cache to `.specscribe/analysis/.rules-cache.json`, keyed by rule key. Rule metadata is near-static, so
        the cache is effectively permanent; only *new* rules cost a call.
  - [ ] Be polite: sequential or small bounded concurrency, not 86 parallel requests.
  - [ ] Inline the resolved `name`/`helpUri` into **every** observation. ADR 0023 Decision 3 rejected raw
        SARIF partly *because* a `result` carries only a `ruleIndex` and is not self-describing — do not
        reintroduce that flaw with an out-of-line catalogue.

- [ ] **Task 6 — NFR12: opt-in, and absent-not-broken** (AC: #1)
  - [ ] **Opt-in by invocation**: nothing runs unless the maintainer runs the command. No hook, no watcher,
        no `postinstall`, no MSBuild target.
  - [ ] ⚠ **On any fetch failure (offline, 404, 5xx, timeout), leave the existing digest untouched, print one
        clear line, and exit 0. NEVER write an empty or partial digest.** An empty digest reads as *"this
        code is clean"* — the single most dangerous output this tool could produce. Absent ≠ clean.
  - [ ] Distinguish the states the ADR requires to be distinguishable: **no digest** (never run / fetch
        failed) vs **digest with zero observations** (genuinely clean) vs **observations present but
        unattached** (`basis: "unavailable"`, always true here per D5).
  - [ ] Verify `git check-ignore -v .specscribe/analysis/index.json` — **already passes** today via
        `.gitignore:488`'s trailing-slash-free `.specscribe` entry (ADR 0014 anticipated exactly this).
        Verify it, do not assume it, and add **no** new ignore rule.

- [ ] **Task 7 — The agent-facing seam** (AC: #1) — D3
  - [ ] Add a short `CLAUDE.md` section (sibling to § Verification). It must say: read `index.json` first;
        read **only** the shards for files you are about to touch, never the whole digest; how to read the
        staleness block **including the read-time rule** from Task 3; that **absent means unknown, not
        clean**; and the one command to refresh it.
  - [ ] Document the refresh command in `docs/SonarCloudSetup.md`.
  - [ ] Spike §11 handoff (not AC-mandated, cheap, do it): recommend **SonarSource's official MCP server** in
        `docs/SonarCloudSetup.md` as an **interactive complement** — and name what it cannot do (Sonar's model
        only, needs a token, dies offline, cannot attach to planning entities, cannot see raw compiler
        output). **It is not the contract**, and the doc must not let a reader think it is.

- [ ] **Task 8 — The worked example** (AC: #1)
  - [ ] Record a **real** pass, not a hypothetical: an agent reading `index.json`, selecting shards for
        specific files, and changing what it said as a result. Good candidates by volume:
        `SiteGenerator.cs` (82 issues at 25.2 baseline), `Charts.cs` (76), `RenderParity.cs` (48 on 309 lines).
  - [ ] Record the **actual bytes read** for that pass against the 1.49 MB monolith — that ratio is the
        story's whole justification.

- [ ] **Task 9 — Verification and scope proof** (AC: #2)
  - [ ] `GoldenContentFingerprint` **unmoved**. Nothing under the output directory is touched, so this holds
        by construction — but state it, and confirm across **two** runs (a concurrent session can move it
        under you; CLAUDE.md § Concurrent work).
  - [ ] Full suite green, with pass/fail/skip counts recorded.
  - [ ] `git status` proof that **no** `src/`, `tests/`, `web/`, `extension/src/`, or `.github/workflows/`
        file changed.
  - [ ] Confirm `tools/**` remains Sonar-excluded so the emitter does not appear in the very findings list it
        produces.

## Dev Notes

### Absolute scope boundaries

- **Nothing under `src/SpecScribe`.** Not a subcommand, not a helper class, not a `SettingsResolver` key. A
  networked analysis path inside the product is **Epic 26's** subject and is governed by NFR12 and the PRD's
  NFR-3 local-first question that **Story 26.2 owns**. AC #2's "quarantined from the generation critical path"
  is satisfied only by staying out of the product entirely.
- **No `.github/workflows/` change** (§ Read-first 2).
- **No second story↔file mapping.** ADR 0023 Decision 5 forbids it. D5 means this story computes none at all.
- **Do not touch `spike/findings/`.** It is proven byte-inert by test and is 25.3's evidence. Port *from* it;
  leave it alone.

### Why the contract's shape is what it is (do not "simplify" these)

| Looks redundant | Why it is not |
|---|---|
| `severity.provider` is an **array** | 14 live issues carry **two** impacts. A scalar field is lossy **today**, not hypothetically. The `impactSeverities` facet counts *issues*, not *pairs*, and is structurally incapable of revealing this — only the payload shows it. |
| Both MQR **and** legacy severity carried | They disagree on **800 of 1,466** issues (54.6%). Dropping either makes two surfaces order the backlog differently by design. |
| A mandatory text `label` next to `normalized` | UX-DR17. In the payload, so no surface can forget it. |
| `attachment.basis` on every record, even as `"unavailable"` | Without it, an empty attachment array is the **same bytes** for "genuinely unattached", "never computed", and "attempted and failed". Deep-git has already dropped whole surfaces at `errors=0` in this project — the spike reproduced it **8 times** (739 pages on some runs, 436 on others). |
| Rule name/URI inlined per observation | A single observation handed to an agent must be self-describing. |

**The collapse cost, stated:** Sonar's 5 levels → SARIF's 4 means `BLOCKER` (1 on this repo) and `HIGH` both
become `error`. **The single BLOCKER is invisible at normalized granularity** and survives only in
`severity.provider`. Do not "fix" this — it is the price of an externally-specified scale, paid deliberately.

### Files to read before writing code

| Path | Why |
|---|---|
| `docs/adrs/0023-agent-facing-analysis-observation-contract.md` | **The contract.** Decisions 3–6 and 8 are normative for this story. |
| `_bmad-output/implementation-artifacts/25-3-spike-report.md` §§ 5, 9, 10.1, 10.5, 10.6, 11 | Severity, staleness, digest sizing, the corrected source ranking, the rule-metadata cost, the handoff. |
| `spike/findings/map_to_model.py` (`from_sonar`, `SONAR_MQR_TO_NORM`, `SEVERITY_LABEL`) | The working reference mapping. Port it; do not depend on it. |
| `docs/SonarCloudSetup.md` § Triaging findings | The shipped, credential-free API method — including `resolved=false` and the `organization`-required rule. |
| `tools/plotly-vendor/` (`package.json`, `build.mjs`, `README.md`) | The `tools/` convention to match. |
| `.github/workflows/build-test-analyze.yml:191` | Confirms `tools/**` is already Sonar-excluded. |
| `docs/adrs/0014-specscribe-settings-folder-format.md` | Why `.specscribe/` is a folder and why no ignore change is needed. |

### Anti-patterns this story is specifically at risk of

1. **Writing an empty digest when the fetch fails.** Turns "I couldn't reach Sonar" into "your code is clean".
   Task 6 forbids it.
2. **Normalizing from the legacy `severity` field** because it is the one at the top level of the payload.
   Reorders 54.6% of the backlog. Read `impacts[]`.
3. **Forgetting `resolved=false`** and triaging 100+ closed issues on excluded paths.
4. **Trusting a frozen `isStale: false`.** It ages into a lie on the next commit — Task 3's read-time rule.
5. **Computing attachment "while we're here."** It is 10× amplifying and the bounding rule is 26.5's and the
   owner's. D5.
6. **Adding raw Roslyn SARIF for "source-agnosticism".** 819 of 1,483 issues already *are* Roslyn results.
   D4 — the seam, not the second source.
7. **Reaching for Python because the prototype is Python.** D2 — an unratified runtime for a permanent tool.

### Testing standards

There is **no test project for `tools/`** and this story does not create one — `tools/plotly-vendor` and
`tools/prism-vendor` ship untested by the same reasoning, and `tools/**` is Sonar-excluded. Verification here
is **empirical and recorded**, per Task 9: real runs against the live API, both staleness states exercised,
measured byte counts, and the worked example. The C# suite must stay green because nothing in `src/` or
`tests/` changed — that is a **scope proof**, not a feature test.

### Project Structure Notes

- New: `tools/analysis-digest/{package.json,index.mjs,README.md}`. Matches the two existing `tools/` entries.
- Generated (gitignored, created at runtime): `.specscribe/analysis/{index.json,unlocated.json,.rules-cache.json}`
  and `.specscribe/analysis/files/**`.
- Modified: `CLAUDE.md`, `docs/SonarCloudSetup.md`, `sprint-status.yaml`.
- Node **24.11.1** per `web/.nvmrc`. Do not add a second Node pin.
- No `.gitignore` change (§ Read-first 5 / Task 6).

### References

- [Source: docs/adrs/0023-agent-facing-analysis-observation-contract.md#Decision] — Decisions 3 (SARIF profile
  + severity scale), 4 (provider values verbatim, path normalization), 5 (labelled attachment), 6
  (revision-first provenance), 7 (25.4 → sharded gitignored digest), 8 (enrichment-only).
- [Source: _bmad-output/planning-artifacts/epics.md#Story 25.4: Agent-Consumable Findings Channel for
  SpecScribe's Own SDD Workflow] — the two ACs, verbatim.
- [Source: _bmad-output/implementation-artifacts/25-3-spike-report.md#11. Handoff] — the 25.4 handoff block,
  **with its source line corrected by D1**.
- [Source: docs/adrs/0014-specscribe-settings-folder-format.md#Decision] — `.specscribe` is a folder; the
  ignore entry already covers it.
- [Source: docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md#Decision] — Node is
  sanctioned build-time tooling.
- [Source: docs/SonarCloudSetup.md#Triaging findings] — `resolved=false`; `organization` required on
  `api/rules/show`; the credential-free method.
- [Source: CLAUDE.md#Verification] — golden fingerprint discipline; confirm across repeated runs.
- [Source: CLAUDE.md#Concurrent work on shared `main`] — verify after every edit; expect the fingerprint to
  move under you.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
