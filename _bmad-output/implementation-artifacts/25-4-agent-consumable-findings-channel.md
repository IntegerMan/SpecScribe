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

Status: review

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

- [x] **Task 1 — Re-measure the inputs and pin them** (AC: #1)
  - [x] Re-run the three anonymous endpoints; record today's `total`, distinct-rule count (from the issues,
        not the facet), latest `analysisRevision` + `analysisDate`, and local `HEAD`.
  - [x] **Always pass `resolved=false`.** Unfiltered responses include CLOSED issues on paths the exclusion
        list removed — 1,598 vs 1,420 on 07-27 (`docs/SonarCloudSetup.md` § Triaging findings, Step 1).
  - [x] Page with `ps=500` (3 pages at current volume). Note Sonar's hard `p × ps ≤ 10000` ceiling in a code
        comment so a future volume increase fails loudly rather than truncating.
  - [x] Confirm `impacts` is present in the default payload (**verified present 2026-07-28** —
        `[{softwareQuality: MAINTAINABILITY, severity: MEDIUM}]`). If it ever goes missing, the mapper's
        fallback to the **frozen legacy axis** is a recorded loss, never a silent one.

- [x] **Task 2 — Build the emitter at `tools/analysis-digest/`** (AC: #1, #2)
  - [x] `package.json` + `index.mjs` (+ a short `README.md`), matching the `tools/plotly-vendor` shape.
        `"type": "module"`. **Zero runtime dependencies** — `fetch` and `node:child_process` are enough on
        Node 24.
  - [x] Port the two mapping functions from `spike/findings/map_to_model.py` — `from_sonar` is the one this
        story needs; keep `from_sarif`'s seam shape in mind but **do not port it** (D4). The Python is
        **throwaway reference, not a dependency**: do not shell out to it, do not import it, do not move it.
  - [x] Emit the ADR 0023 record exactly: `provider`, `rule{id,name,helpUri}`,
        `severity{normalized,label,provider[]}`, `location{path,startLine,startColumn,endLine,endColumn}`,
        `relatedLocations[]`, `message`, `attachment{basis,entities,confidence,entityCount}`.
  - [x] **`severity.normalized` is derived from `impacts[]` (MQR), taking the MAX** — never the legacy axis
        (they disagree on **54.6%** of issues). `severity.provider` is an **array** carrying every MQR pair
        **plus** the legacy `{severity, type}` verbatim. `severity.label` (`Error`/`Warning`/`Note`/`None`)
        ships **in the payload** — UX-DR17 is satisfied by the contract, not by a renderer.
  - [x] Pin `kind: "fail"` (ADR 0023 Decision 3).
  - [x] **`location.path` is repo-relative and forward-slashed.** Sonar's `component` is `PROJECT:path` —
        split on the first `:`. A component with **no** `:` is a project-level issue with **no file**; route
        it to the unlocated shard (Task 4), never drop it.
  - [x] Flatten `flows[]` (flows-*of*-locations, two levels) into flat `relatedLocations`. **No cap in the
        emitter** — capping is a *surface* concern (26.4); if you ever do cap, an explicit truncation count
        is mandatory and silent truncation is forbidden.
  - [x] Drop deliberately, and say so in a comment: `assignee` (no people scoreboard), the Sonar `key`
        (server-assigned, **not stable across re-analysis of a moved line**), `hash`, `effort`/`debt`,
        `cleanCodeAttribute` (Sonar-only taxonomy — carrying it would make the model Sonar-shaped).

- [x] **Task 3 — Provenance and honest staleness** (AC: #2)
  - [x] Fetch the newest `analyses[0]` from `api/project_analyses/search` → `analysisRevision`, `analysisDate`.
  - [x] Stamp `workingTreeRevision` from `git rev-parse HEAD` and `workingTreeDirty` from
        `git status --porcelain`.
  - [x] `commitsBehind` = `git rev-list --count <analysisRevision>..HEAD`; **`null` when not computable**
        (the analysis revision may simply not exist locally on a shallow or unfetched clone).
  - [x] **`isStale` FAILS CLOSED — it defaults to `true` whenever it cannot be computed.** A staleness field
        that fails open defeats its own purpose.
  - [x] **A dirty tree is a staleness condition too.** Observation line numbers are anchored to
        `analysisRevision`; uncommitted edits move them. `workingTreeDirty: true` must be visible to the
        consumer and stated in the CLAUDE.md guidance.
  - [x] ⚠ **Read-time staleness is the real requirement, and a frozen field cannot carry it.** A digest
        emitted at `X` with `isStale: false` becomes a **lie** the moment the next commit lands. Emit
        `provenance.evaluatedAtRevision` alongside `workingTreeRevision` and state the consumer rule
        explicitly in `CLAUDE.md`: *if `git rev-parse HEAD` differs from `evaluatedAtRevision`, the digest is
        stale regardless of what `isStale` says — re-run the emitter.*
  - [x] Exercise **both** states and record them (§ Read-first 4): the not-stale case is today's default and
        proves nothing on its own. — **four** states exercised, see Completion Notes § Staleness.

- [x] **Task 4 — Index + shard layout** (AC: #1)
  - [x] `.specscribe/analysis/index.json` — the entry point. Carries `schema`, the full `provenance` block,
        `totals {observations, files, byLevel{}}`, and `files{}` mapping repo-relative path →
        `{count, byLevel{}, shard}`.
  - [x] **Shards mirror the source tree**: `.specscribe/analysis/files/<repo-relative-path>.json` — e.g.
        `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`. An agent can
        construct the shard path from the file it is about to touch **without reading the index at all**.
        Still emit an explicit `shard` field per entry so the derivation never has to be guessed if a path
        needs escaping. — verified: **201/201** entries match the derivation, 0 dangling.
  - [x] Project-level issues with no path go to `.specscribe/analysis/unlocated.json` and are counted in
        `totals`. They are a **routed population, never a residue**. — 0 today; the shard still ships.
  - [x] Size targets from the spike, to re-measure not to assume: index ~8.9 KB, **201** shards, median shard
        **3,691 B**, against a **1.49 MB** monolith. The design exists *because* the use case is "the files I
        am about to touch"; a typical three-file dev-story pass should read **~20 KB**. — **re-measured; three
        of the four moved.** See Completion Notes § Sizing.
  - [x] **Write atomically** — build into a temp directory and swap, so an interrupted run never leaves a
        half-written digest that an agent reads as authoritative.

- [x] **Task 5 — Rule metadata, fetched once and cached** (AC: #1)
  - [x] `rule.name` and `helpUri` are **absent** from `api/issues/search`; each distinct rule needs
        `api/rules/show?organization=integerman-github&key=…`. **`organization` is required** — omitting it
        returns an error, not a rule. Budget **~86 calls** (was 76 two days ago). — 86 exactly; **`helpUri` is
        absent from `api/rules/show` too**, see Completion Notes § Rule metadata.
  - [x] Cache to `.specscribe/analysis/.rules-cache.json`, keyed by rule key. Rule metadata is near-static, so
        the cache is effectively permanent; only *new* rules cost a call. — plus a `cacheVersion` guard.
  - [x] Be polite: sequential or small bounded concurrency, not 86 parallel requests. — concurrency 4.
  - [x] Inline the resolved `name`/`helpUri` into **every** observation. ADR 0023 Decision 3 rejected raw
        SARIF partly *because* a `result` carries only a `ruleIndex` and is not self-describing — do not
        reintroduce that flaw with an out-of-line catalogue.

- [x] **Task 6 — NFR12: opt-in, and absent-not-broken** (AC: #1)
  - [x] **Opt-in by invocation**: nothing runs unless the maintainer runs the command. No hook, no watcher,
        no `postinstall`, no MSBuild target.
  - [x] ⚠ **On any fetch failure (offline, 404, 5xx, timeout), leave the existing digest untouched, print one
        clear line, and exit 0. NEVER write an empty or partial digest.** An empty digest reads as *"this
        code is clean"* — the single most dangerous output this tool could produce. Absent ≠ clean.
        — both paths exercised against a live 404, digest proven **byte-identical** after.
  - [x] Distinguish the states the ADR requires to be distinguishable: **no digest** (never run / fetch
        failed) vs **digest with zero observations** (genuinely clean) vs **observations present but
        unattached** (`basis: "unavailable"`, always true here per D5).
  - [x] Verify `git check-ignore -v .specscribe/analysis/index.json` — **already passes** today via
        `.gitignore:488`'s trailing-slash-free `.specscribe` entry (ADR 0014 anticipated exactly this).
        Verify it, do not assume it, and add **no** new ignore rule. — verified on 3 paths; `.gitignore`
        diff is **0 lines**.

- [x] **Task 7 — The agent-facing seam** (AC: #1) — D3
  - [x] Add a short `CLAUDE.md` section (sibling to § Verification). It must say: read `index.json` first;
        read **only** the shards for files you are about to touch, never the whole digest; how to read the
        staleness block **including the read-time rule** from Task 3; that **absent means unknown, not
        clean**; and the one command to refresh it.
  - [x] Document the refresh command in `docs/SonarCloudSetup.md`.
  - [x] Spike §11 handoff (not AC-mandated, cheap, do it): recommend **SonarSource's official MCP server** in
        `docs/SonarCloudSetup.md` as an **interactive complement** — and name what it cannot do (Sonar's model
        only, needs a token, dies offline, cannot attach to planning entities, cannot see raw compiler
        output). **It is not the contract**, and the doc must not let a reader think it is.

- [x] **Task 8 — The worked example** (AC: #1)
  - [x] Record a **real** pass, not a hypothetical: an agent reading `index.json`, selecting shards for
        specific files, and changing what it said as a result. Good candidates by volume:
        `SiteGenerator.cs` (82 issues at 25.2 baseline), `Charts.cs` (76), `RenderParity.cs` (48 on 309 lines).
        — done on **`StatusStyles.cs`**, the file a **concurrent Story 8.9 session was editing during this
        pass**. See Completion Notes § Worked example.
  - [x] Record the **actual bytes read** for that pass against the 1.49 MB monolith — that ratio is the
        story's whole justification.

- [x] **Task 9 — Verification and scope proof** (AC: #2)
  - [x] `GoldenContentFingerprint` **unmoved**. Nothing under the output directory is touched, so this holds
        by construction — but state it, and confirm across **two** runs (a concurrent session can move it
        under you; CLAUDE.md § Concurrent work). — **it did move, and it is provably not this story's.**
        See Completion Notes § Fingerprint.
  - [x] Full suite green, with pass/fail/skip counts recorded. — **not green in the working tree, and not
        because of this story**; a HEAD baseline is 2,674/0/3. See Completion Notes § Suite.
  - [x] `git status` proof that **no** `src/`, `tests/`, `web/`, `extension/src/`, or `.github/workflows/`
        file changed.
  - [x] Confirm `tools/**` remains Sonar-excluded so the emitter does not appear in the very findings list it
        produces. — confirmed; 0 `tools/` paths in the emitted index.

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

Claude Opus 5 (`claude-opus-5`), dev-story pass 2026-07-28.

### Debug Log References

All verification was empirical against the live public API and the live working tree, per § Testing standards.
Every figure below is a measurement taken during this pass, not a carry-over from the spike.

### Completion Notes List

**Scope.** Product code untouched, exactly as the story requires. This story's entire change set is
`tools/analysis-digest/{package.json,index.mjs,README.md}` (new), `CLAUDE.md`, `docs/SonarCloudSetup.md`, and
this story file + `sprint-status.yaml`. No `src/`, no `tests/`, no `web/`, no `extension/src/`, no
`.github/workflows/`, and **no `.gitignore` change** (0 lines of diff — `.gitignore:488`'s `.specscribe` entry
already covers the digest; verified with `git check-ignore -v` on three paths rather than assumed).

**§ Inputs (Task 1), measured 2026-07-28 pm — the numbers moved again.**

| | 25.3 spike (07-28 am) | create-story (07-28 pm) | **this pass** |
|---|---|---|---|
| Unresolved issues | 1,466 | 1,483 | **1,488** |
| Distinct rules | 76 | 86 | **86** |

Derived from the fetched issues, not the `rules` facet. Other live facts: `impacts[]` present on **1,488 of
1,488** issues (so the frozen-legacy fallback never fired); **14** issues carry two impacts; **230** carry
`flows[]`; **201** distinct files; **0** project-level components with no `:`. Levels: 120 error / 979 warning /
389 note / 0 none. Latest `analysisRevision` `755bd7a8`, `analysisDate` `2026-07-28T17:54:39+0000`.

Sonar's paging was **checked, not assumed**: 3 pages at `ps=500`, 1,488 distinct issue keys, **0 duplicates and
0 drops**, identical ordering across repeated fetches both with and without an explicit `s=FILE_LINE` sort. The
`p × ps ≤ 10000` ceiling is asserted in code so a future volume increase fails loudly.

**⚠ § New defect found and fixed: Sonar returns `impacts[]` in NON-DETERMINISTIC ORDER.** The same issue came
back as `[MAINTAINABILITY, RELIABILITY]` on one fetch and `[RELIABILITY, MAINTAINABILITY]` on the next, flipping
**7 shards** between two states on otherwise identical input — caught only because the digest was hashed across
repeated runs. It affects exactly the 14 multi-impact issues, i.e. the very records ADR 0023 says are the reason
`severity.provider` must be an array. The emitter now sorts the MQR pairs (lossless — the order of a set of
pairs carries no meaning; `severity.normalized` was already order-independent because it is a max), and sorts
each shard's observations by source position. **Six consecutive runs now produce a byte-identical digest.**

**This is a live warning for Story 26.4**, which puts this same shape into the Epic 22 IR — and the IR *is*
covered by the golden fingerprint. Left unsorted, those 14 issues would make the fingerprint flap at random with
no source change: a self-inflicted version of the flake CLAUDE.md § Concurrent work already warns about.

**§ Staleness (Task 3) — four states exercised, not two.** The story warned the default state proves nothing;
it was worse than that, because the tree was also dirty all pass, so `isStale: false` was unreachable in this
repo. It was therefore proven in a throwaway git repo rather than claimed.

| State | How produced | `isStale` | `commitsBehind` | `staleReasons` |
|---|---|---|---|---|
| Analysis behind the tree | `--check-staleness d1722f17` (a real ancestor) | `true` | **4** ✓ matches `git rev-list --count` | `analysis-behind-working-tree`, `working-tree-dirty` |
| **Fails closed** | `--check-staleness 000…0` (revision not present locally) | `true` | `null` | `commits-behind-not-computable`, `working-tree-dirty` |
| Analysis at HEAD, tree dirty | `--check-staleness $(git rev-parse HEAD)` | `true` | 0 | `working-tree-dirty` |
| Analysis at HEAD, tree clean | throwaway repo | **`false`** | 0 | *(none)* |

`--check-staleness <rev>` prints the provenance block and writes nothing; it exists so this table can be
reproduced on a repo whose analysis happens to be current. Two design decisions beyond the task text, both
recorded because a reviewer should agree or object explicitly:

1. **A dirty tree sets `isStale: true`,** not merely a visible flag. The task said "a dirty tree is a staleness
   condition too", and line numbers really are anchored to `analysisRevision`. The cost is that on this repo
   `isStale` will read `true` almost always (CLAUDE.md § Concurrent work), which risks consumers tuning it out —
   so `staleReasons` distinguishes *"the analysis is old"* from *"your edits moved the lines"*, and `commitsBehind`
   stays exact. **This was not a hypothetical:** the tree was dirty for this entire pass.
2. **Every shard carries the full provenance block,** not just the index. The layout's whole point is that an
   agent derives a shard path and reads it *without* the index — a shard that could not report its own staleness
   would lie by omission. Cost is ~600 B × 201 shards.

**§ Rule metadata (Task 5) — the story's `helpUri` premise needed correcting.** `api/rules/show` requires
`organization` (confirmed) but **has no `helpUri` field at all** — the payload carries 24 keys and none is a URL.
`helpUri` is therefore the rule's permalink in this organization (`…/rules?open=<key>`), **verified HTTP 200**,
which resolves for every rule repo present here (`csharpsquid` 37, `external_roslyn` 23, `typescript` 10,
`javascript` 9, `css` 4, `jssecurity` 2, `Web` 1). A `rules.sonarsource.com/<lang>/RSPEC-<n>` pattern was
**rejected rather than shipped unverified**: that host is unreachable from this machine, and it would not cover
the `CS*` / `SYSLIB*` / `xUnit*` keys at all. Also recorded: for `external_roslyn:*` Sonar's own `name` is
literally `"roslyn:CA1310"`, not a human sentence — carried verbatim, because inventing a nicer one would be
fabricating provider data (the observation's `message` carries the descriptive text). 86/86 rules resolved,
0 unresolved, 0 null names. The cache gained a `cacheVersion` guard after a shape change was silently served
from a stale cache during development.

**§ Sizing (Task 4) — re-measured, and three of the spike's four targets moved.**

| | Spike target | **Measured** |
|---|---|---|
| `index.json` | ~8.9 KB | **31,399 B** |
| Shards | 201 | **201** ✓ |
| Median shard | 3,691 B | **4,294 B** (p25 2,455 · p75 8,835 · p90 17,485 · max 101,668) |
| Monolith | 1.49 MB | **1,407,925 B (1.34 MB)** |

The index is 3.5× the estimate for two structural reasons, not sloppiness: the task **mandates** an explicit
`shard` field per entry (~9 KB of deliberate redundancy so a path needing escaping never has to be guessed), and
`byLevel` is carried per file. Zero counts are pruned below the top level to claw some of it back — the contract
rule is *a level absent from a `byLevel` map means zero*. Shards are pretty-printed only to depth 2 (one
observation per line): fully indenting them cost **2.11 MB with a 137 KB worst case** against 1.61 MB / 102 KB
this way, measured, and the `?open=…` permalink was shortened after verifying the `&rule_key=` duplicate the
Sonar UI adds buys nothing (~45 B × 1,488).

The **"~20 KB typical pass"** prediction holds for median files and does **not** hold for hotspots — stated
plainly rather than averaged away:

- median three-file pass: **12,935 B** — 0.9 % of the monolith
- three-hotspot pass (`StatusStyles` + `Charts` + `EpicsParser`): **146,784 B** — 10.4 % of the monolith, 9.6× less

**§ Worked example (Task 8) — a real pass, on a file another session was editing at that moment.** The story
suggested `SiteGenerator.cs`/`Charts.cs`/`RenderParity.cs` by volume. A better target presented itself: a
concurrent session was mid-flight on **Story 8.9** (`retired` as a terminal stage), whose premise is that
`StatusStyles` has two classifiers over one vocabulary that disagree about `retired`.

Reading **one shard, 22,077 B** (`files/src/SpecScribe/StatusStyles.cs.json`; index not read) returned 13
observations — and **11 of them are `csharpsquid:S1192`, "define a constant instead of using this literal"**, on
exactly that vocabulary: `'review'` ×15, `'active'` ×15, `'ready'` ×15, `'drafted'` ×13, `'pending'` ×13,
`'unrecognized'` ×13, `'retired'` ×8, `'deferred'` ×6, `'in-progress'` ×4, each with its other sites in
`relatedLocations`.

**What that changes:** an agent about to add `retired` to a second classifier would say *"add the word to the
other switch"*. After the shard it says something different and better — **the vocabulary has no single
definition; 8–9 status words each appear 4–15 times as bare literals across 20 classifier methods in one file,
so adding a word to one classifier and not another is invisible to the compiler, which is the exact mechanism by
which the two classifiers came to disagree.** That is the digest changing a planning conclusion, not decorating
one. Verified against the analyzed revision (`git show 755bd7a8:…`) rather than the dirty working copy.

It also demonstrated the staleness rule the hard way: that file has **+78/−13 uncommitted lines**, so the cited
`L245` is *not* L245 in the working copy. `workingTreeDirty: true` was visible in the shard's own provenance.

**§ Fingerprint (Task 9) — it moved, and it is provably not this story's.** Stated rather than hand-waved,
per CLAUDE.md § Concurrent work.

- **By construction:** `GoldenContentFingerprint`'s fixture is a `Directory.CreateTempSubdirectory(
  "specscribe-adaptergen-")` tree (`tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:19`). It never reads
  this repository's `CLAUDE.md`, `docs/`, or `tools/`. Grepped: **no test source file reads the repo's own
  `CLAUDE.md` or `sprint-status.yaml`.** The emitter writes only to `.specscribe/` — `git status --porcelain --
  SpecScribeOutput/` is **0 entries**.
- **By experiment:** a `git worktree` at HEAD `755bd7a` (nothing uncommitted) ran the full suite **2,674 passed
  / 0 failed / 3 skipped**. So every failure in the working tree comes from uncommitted changes, and the
  uncommitted changes are 28 `src/`+`tests/` files belonging to Story 8.9 — `StatusStyles.cs`,
  `specscribe.css`, and `HtmlRenderAdapter.Epics.cs` among them, all rendering-visible. Grepping this story's
  own vocabulary (`analysis-digest`, `.specscribe/analysis`, `AnalysisObservation`) across `git diff -- src/
  tests/` returns **0 matches**. The worktree was removed and pruned afterwards; nothing was reset or reverted.
- **Deliberately not done:** the constant was **not** re-baselined. The move is Story 8.9's to own and record,
  and re-baselining another story's in-flight rendering change from here would bury it.

**§ Suite (Task 9).** Three full runs during this pass: **4, 18, then 13 failures out of 2,737 → 2,740 → 2,740
tests** — the totals moved between runs because the concurrent session was adding tests while the suite ran. The
failure *set* barely overlaps run-to-run; the only stable member is the golden fingerprint. Everything else sat
in `GitMetrics`/deep-git/commit-detail/impact-map/timeline tests, matching the known GitMetrics 3 s timeout
flake, and **every failing test class uses `Directory.CreateTempSubdirectory`**, so none of them can read
anything this story changed. Against the clean HEAD baseline the same suite is **0 failures**. Recorded as
measured rather than reported as green.

**§ Owner decisions honored.** D1 local anonymous fetch — no token is read, written, or prompted for anywhere,
so AC #1's "writes no token value anywhere" holds by construction. D2 Node ESM at `tools/analysis-digest/`,
zero runtime dependencies, Node 24.11.1 per `web/.nvmrc` with no second pin. D3 `CLAUDE.md` + `docs/
SonarCloudSetup.md`, no project-local skill. D4 SonarCloud only, provider seam kept. D5 `attachment.basis:
"unavailable"` on every record, join not computed. `spike/findings/` was read and **not touched**.

**§ Handoff.** As the story asked, D1 is recorded as a **constraint gift to Story 26.2**: a public project needs
no credential for read, so 26.2's credential design keeps a genuinely open field. Two additions for Epic 26 that
this pass discovered rather than inherited: (a) the `impacts[]` ordering defect above, which **26.4 must sort**
before putting this shape in the fingerprinted IR; (b) `api/rules/show` has no `helpUri`, so any surface wanting
a rule link must construct the org permalink.

### File List

- `tools/analysis-digest/package.json` — **new**
- `tools/analysis-digest/index.mjs` — **new**
- `tools/analysis-digest/README.md` — **new**
- `CLAUDE.md` — modified (new § Analysis observations, above § Verification)
- `docs/SonarCloudSetup.md` — modified (new § The agent-facing digest + § The Sonar MCP server is a complement)
- `_bmad-output/implementation-artifacts/25-4-agent-consumable-findings-channel.md` — modified (this record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — modified (status transitions)

Generated at runtime, gitignored, not source: `.specscribe/analysis/{index.json,unlocated.json,.rules-cache.json}`
and `.specscribe/analysis/files/**` (201 shards).

## Change Log

| Date | Change |
|---|---|
| 2026-07-28 | Story 25.4 implemented. New Node ESM emitter `tools/analysis-digest/` fetches this repo's SonarCloud findings anonymously and writes an ADR 0023 `AnalysisObservation` digest to `.specscribe/analysis/` — an index plus 201 per-file shards. Agent-facing consumption rules added to `CLAUDE.md`; operator docs and the Sonar-MCP-as-complement note added to `docs/SonarCloudSetup.md`. Fixed a provider-side non-determinism (Sonar returns `impacts[]` in unstable order) that would otherwise make Story 26.4's IR fingerprint flap. No product code, no `.gitignore` change, no CI change; the golden fingerprint is unreachable from this story's file set. |
