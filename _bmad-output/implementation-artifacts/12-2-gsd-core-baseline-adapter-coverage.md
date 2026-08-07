---
baseline_commit: 9580f62d3431cfc25cd67d5b8627a2b79e4aed50
---

# Story 12.2: GSD Core Baseline Adapter Coverage

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a team using GSD Core workflows,
I want key GSD Core artifacts rendered coherently,
so that progress and scope remain understandable in one portal.

## Why this story exists (read first)

Story 12.1 (`done`) mapped the GSD family against the shared adapter contract and found that **GSD Core and
GSD Pi are distinct products needing two adapter surfaces**, so the old combined story was split: **12.2 is GSD
Core only** (`.planning/`, markdown-native, no database), and 12.3 is GSD Pi (`.gsd/`, SQLite-authoritative).
GSD Core goes first because it needs none of the projection-reliability machinery.

**This is the FIRST story in the whole project to make a non-BMad repository generate at all.** Story 11.2
(Spec Kit) is still `backlog` and 11.1 is `ready-for-dev` with empty Completion Notes, so nothing has been
deferred to. That is why this story carries two shared prerequisites (AC #5) as well as its own coverage.

**The one-line test for "is this in scope?":** if the change makes a `.planning/` repo generate, projects GSD
Core artifacts into `ArtifactBundle`, or closes a prerequisite that blocks *any* non-BMad adapter → in. If it
touches `.gsd/`, `gsd.db`, GSD Pi, or Spec Kit parsing → out (12.3 / 11.2). If it decides the *strategic*
multi-framework policy → out, that is **Story 4.9**, seated by this create-story.

## ⚠️ What the real repo changed — read before trusting Story 12.1's coverage map

Story 12.1 could not obtain a generated GSD Core repository and said so plainly in its Debug Log: *"Every layout
claim below therefore rests on current vendor documentation, not on a directory listing… Story 12.2 must
re-confirm exact filenames against a generated repo before writing discovery globs."*

**That repository now exists: `C:/dev/CORA`** — a real GSD Core project, 168 files under `.planning/`, 11
phases, 58 plans, committed (not gitignored). It was inspected at create-story on 2026-08-06 and **it overturns
six of 12.1's claims.** Each row below was verified by command, not inferred:

| # | Story 12.1 said | The real repo says | Consequence |
|---|---|---|---|
| 1 | phases are `NN-slug`, zero-padded-2 → `EpicInfo.Number` parses cleanly | phase numbers are **decimal in practice**: `02.1-ui-foundation-and-style-system`, `04.5-conversation-embeddings-and-semantic-search`, and backlog `999.1/.2/.3` | `EpicInfo.Number` is `int` — 2 of 8 shipped v1 phases and the entire backlog are unrepresentable. **Owner decision D2** |
| 2 | *"Task → `TasksDone`/`TasksTotal` only… this is not a compromise at all for Core"* | **`PLAN.md` files carry no usable markdown task checkboxes.** Of 58 plans, only 25 have a `## Tasks` heading and those decompose into `<task type="auto">` XML blocks; across all 58 files there are **0 checked boxes and 39 unchecked ones**, every one of them a `## Verification` box left unchecked on plans whose work is finished | `TaskListParser.Parse` (requires `## Tasks` + `- [x]` lines, `TaskListParser.cs:11-22`) returns **0/0 for every plan**. 12.1's ruling is false for Core |
| 3 | requirement ids are *"stable IDs like `REQ-001`"* | ids are **project-defined prefixes**: `CONV-01`, `CAP-01`, `RET-01`, `GADM-01`, `TRST-01`, `CTX-01`, `VIZ-01`, `RAG-01`, `PLAT-01`, `PERS-01`, `CORR-01`, `FUT-01` — twelve distinct prefixes in one repo, none of them `REQ` | the prefix set is **open**, so it cannot be enumerated into `RequirementKind`. **Owner decision D3** |
| 4 | *"Neither GSD product publishes [a coverage map], so every requirement would land on `Unmapped` — actively misleading"* | `REQUIREMENTS.md` **does** ship a `## Traceability` table (`\| Requirement \| Phase \| Status \|`, 48 rows) **and** every phase in `ROADMAP.md` carries a `**Requirements**:` line **and** all 58 `PLAN.md` files carry a `requirements:` frontmatter list | a genuine three-level requirement→work map exists. But the table is **stale** (`*Last updated: 2026-05-03*`, still says `Pending` for phases ROADMAP marks complete) — a different honesty problem, not the one 12.1 predicted |
| 5 | (not raised) | **PLAN frontmatter is inconsistent across generations**: the `phase:` key takes **8 different encodings** in one repo — `01-identity-scope-and-boundaries` (slug), `"02.1"`, `"4.5"` (unpadded), `"5"` (unpadded), `"06"` (padded), `4` (bare int); only 17 of 58 carry an `id:` key; `plan: 01` is unquoted (YAML int `1`) in some files and `plan: "00"` quoted in others | **the filename is the only stable key.** Derive ids from `NN-YY-PLAN.md`, never from frontmatter. `requirements:` is the one key present in 58/58 |
| 6 | *"Slash commands `/gsd-*` (10 commands, in the runtime's config dir, **not in the repo**)"* | **67 commands are in the repo** at `.claude/commands/gsd/*.md`, plus `gsd-*` subagents at `.claude/agents/` | commands are discoverable. Still **out of scope** — `ModuleContext` is BMad-typed (see Gap 3) — but say so as a known ceiling, not as "not in the repo" |

**Two more findings with no 12.1 counterpart, both load-bearing:**

**7. Three completion signals disagree, and the story must not silently reconcile them.** For CORA:

- `ROADMAP.md` marks **58 of 58** plans `- [x]` — the only per-plan signal that exists for every plan
- `STATE.md` frontmatter says `completed_plans: 42`, `total_plans: 50`, `percent: 84`
- **42** `*-SUMMARY.md` files exist on disk; **16 plans marked `[x]` have no summary** (all of `06-*`, all of `04-03..07`, `02-00..02`)

Pick one authoritative per-plan signal, name it on the surface, and emit an `Informational` diagnostic when they
disagree. Do **not** average, and do not let SUMMARY-presence quietly override a declared `[x]`.

**8. `Status:` extraction finds nothing.** `EpicsParser.StatusLine` is `^Status:\s*(.+)$` (`EpicsParser.cs:23`);
**zero** GSD plans carry a `status:` key in frontmatter or a `Status:` line. So `StoryInfo.Status` is null →
`StatusStyles.ForStory` → `drafted`. Combined with finding #2, **every completed GSD plan would render as a
drafted story with no task plan** — precisely the defect class `BmadArtifactAdapter.BuildArtifactMap`'s doc
comment already warns about ("rendering a done story as deferred/no-plan", `BmadArtifactAdapter.cs:323-333`).

**`C:/dev/CORA` is a reference, not a test dependency.** Tests must never read it (CI has no such path). Follow
`BmadArtifactAdapterTests`' fixture style — `Directory.CreateTempSubdirectory` + `const string` file bodies —
and *derive* those fixture strings from CORA's real shapes, including the awkward ones (a decimal phase, a
`999.x` backlog phase, a plan with no `## Tasks`, a `[x]` plan with no SUMMARY).

## Owner decisions locked at create-story (2026-08-06)

These were elicited before drafting and are **not** open for the dev agent to relitigate.

- **D1 — Level mapping.** Phase → `EpicInfo`; Plan (`NN-YY-PLAN.md`) → `StoryInfo`; Task → nothing (see finding
  #2). **Milestone gets its own surface: grouped bands on the epics index** (AC #4), *not* a new route family.
  Rejected: Milestone → `EpicInfo` / Phase → `StoryInfo`, which would strand all 58 plan files and land the
  per-phase `**Requirements**:` map on a story when `RequirementInfo.CoverageEpicNumbers` is epic-level.
- **D2 — Decimal phase numbers.** `EpicInfo.Number` gets a **synthetic sequential ordinal** in ROADMAP order;
  the real label (`Phase 2.1: UI Foundation and Style System`) is carried in `EpicInfo.Title` and in the story
  id. Rejected: widening `EpicInfo.Number` to a string (touches sunburst, donut, sprint grouping, requirement
  roll-up, `StatusStyles`, work graph and the IR schema — its own story), and skipping decimal phases (drops 2
  of CORA's 8 shipped v1 phases plus the whole backlog).
- **D3 — Requirements.** `REQUIREMENTS.md` renders as a **document** (tier `Rendered`, through the generic
  `*.md` pass) and `ArtifactBundle.Requirements` stays **null**. No requirement pages, no coverage roll-up, no
  fabricated ids. Rejected: extending the requirement model (a shared-model + IR-schema change warranting its
  own ADR), and mapping `CONV-01` → `FR1` (renders an id GSD never wrote).
- **D4 — Prerequisites.** This story owns **both** of Story 12.1's blockers — the adapter registry and
  framework-neutral source-root discovery — closed once for all frameworks, plus **one shared ADR** that Epics
  11/13/14/15 inherit. The owner explicitly declined splitting them into a separate story.
- **D5 — Multi-framework repos.** A repo may carry several frameworks at once (CORA plans in BMad, delivers in
  GSD Core). Matching adapters **merge minimally** here. The strategic answer is **Story 4.9**, seated in
  `epics.md` + `sprint-status.yaml` by this create-story.

## Acceptance Criteria

1. **Given** a representative current-version GSD Core repository (a `.planning/` directory of plain Markdown
   and JSON), **when** generation runs, **then** key planning and tracking artifacts render without fatal
   errors, **and** output remains coherent with existing BMad and Spec Kit surfaces.

2. **Given** partially supported GSD Core artifacts, **when** they are discovered, **then** coverage tier
   labeling communicates interpretation boundaries clearly, reusing the existing `CoverageTier` vocabulary
   rather than a parallel scale, **and** unsupported items never block full-site generation.

3. **Given** GSD Core's Milestone → Phase → Task hierarchy against the two-level epics/stories model, **when**
   artifacts are projected, **then** the chosen level mapping and the synthesized story-id form are pinned by a
   test, **and** requirements are surfaced without claiming a coverage status GSD Core does not record.

4. **Given** GSD Core groups its phases under named milestones (`v1.0`, `v2.0`) that carry their own completion
   state and progress roll-up, **when** the epics index is generated, **then** phases render as banded groups
   under a milestone header carrying the milestone's name, state, and rolled-up phase and plan counts, **and** a
   framework with no milestone level renders exactly as it does today, byte-for-byte.

5. **Given** SpecScribe selects a single hardcoded adapter and discovers its repo root by a hardcoded
   `_bmad-output` marker, so a non-BMad repository fails before any adapter is consulted, **when** generation
   runs against a GSD Core repository, and against a repository carrying both BMad and GSD Core markers,
   **then** adapter selection and framework-neutral source-root discovery are in place, every matching adapter
   contributes, and a family that loses a merge conflict is reported as a non-fatal diagnostic rather than
   dropped silently, **and** a BMad-only repository's output is unchanged, with the decision recorded as one
   shared ADR the remaining framework epics inherit.

[Source: `_bmad-output/planning-artifacts/epics.md` § Story 12.2 — ACs #1–#3 verbatim from the epic; #4/#5 added
2026-08-06 recording decisions D1–D5, co-landed in `sprint-status.yaml` in the same change.]

## Context & Scope

### ⚠️ Name-collision trap: GSD ≠ GDS

`gds` is **BMad GDS (Game Dev Studio)** — an installable BMad module (`_bmad/gds`) that rides
`BmadArtifactAdapter` and is already fully supported. `gsd` is **GSD Core (Get Shit Done)**, this story's
target. When grepping: `AppendGdsBody` / `AboutSddGdsOutputPath` / `_bmad/gds` = the supported BMad module;
`AboutSddGsdOutputPath` / `.planning/` = this story.

### The contract to implement against

Read these in full before writing code; the line anchors were verified at create-story against baseline
`9580f62`, but a concurrent session may move them — **confirm by symbol, not by line** (CLAUDE.md § Concurrent
work).

- **`IArtifactAdapter`** [`src/SpecScribe/IArtifactAdapter.cs:19-38`] — `AppliesTo` (cheap marker sniff, never
  throws) and `Ingest` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics`).
- **`ArtifactBundle`** [`src/SpecScribe/ArtifactBundle.cs:10-58`] — `Module` (required, never null →
  `ModuleContext.None`), `Sprint?`, `Retros`, `Epics?`, `Requirements?`, `EpicsSourceFullPath?`,
  `StoryArtifactsById`, `ConsumedSourceRelatives`, `Diagnostics`.
- **`AdapterDiagnostic`** [`src/SpecScribe/AdapterDiagnostic.cs`] — five categories, `Unsupported` / `Malformed`
  / `Skipped` / `Error` / `Informational`. **Do not invent a sixth** (ADR 0020 § Decision 4 restates this).
- **`BmadArtifactAdapter`** [`src/SpecScribe/BmadArtifactAdapter.cs:11-412`] — the one working example. Mirror
  its shape: marker sniff → discover by well-known name → parse each family independently → one family's
  failure never kills its siblings → diagnose non-fatally. Note `ArtifactFilenamePattern` at line 55 is
  `^(?<epic>\d+)-(?<story>\d+)-`, which does **not** match `02.1-01-PLAN.md`.
- **`CoverageTier` / `CoverageTiers`** [`src/SpecScribe/TestArtifactsModel.cs:18-76`] — the tier vocabulary
  **already exists** (Story 18.5). Reuse it; never mint a scale, and never let a surface spell a tier itself.
  Note the semantics: `Rendered` = a full page exists and nothing beyond prose is interpreted; `Summarized` =
  a structured headline is additionally extracted (so `Summarized` is the *deeper* tier).

### The three axes, kept separate

1. **Classification** — mappable / partially-mappable / unsupported. *Does it fit the shared model?*
2. **Coverage tier** — `Rendered` / `Summarized` / `Unsupported`. *How deeply is it interpreted?*
3. **Diagnostic category** — one of the five. *What non-fatal notice fires?*

### The two prerequisites (AC #5) — precisely what is blocked

**Gap 1 — no adapter registry.** `SiteGenerator` holds one hardcoded field, `private readonly
BmadArtifactAdapter _adapter = new();` [`SiteGenerator.cs:~60-63`], with a comment saying the registry "arrives
with Stories 4.3+" — the stories relocated into Epics 11–15, so it has no owner. Two call sites consume it:
`_adapter.Ingest(...)` [`~501`] and `_adapter.IngestEpics(...)` [`~1437`].

> **The watch-path constraint is real, not a detail.** The field is the **concrete** `BmadArtifactAdapter`, not
> the interface, because `RegenerateEpics` needs its scoped `IngestEpics`/`EpicsIngest` re-ingest. A registry
> returning `IArtifactAdapter` breaks watch-mode incremental regeneration unless the scoped re-ingest is lifted
> onto the interface or the watch path degrades to a full re-ingest for adapters that lack it. **AD-5 says watch
> behaviour must not regress**, and ADR 0027 defines "when safe" as *proven byte-identical to a full rebuild*.
> Whichever route you take, prove it.

**Gap 2 — source-root discovery is BMad-hardcoded.** `ForgeOptions.SourceDirName` is the literal
`"_bmad-output"` [`ForgeOptions.cs:87`] and `Resolve` walks *up from the cwd looking for a directory containing
`_bmad-output`*, throwing `DirectoryNotFoundException` when there is none [`ForgeOptions.cs:157-173`]. **A pure
GSD repo fails before `AppliesTo` ever runs.** Two couplings ride along: `RepoRoot` is the parent of an explicit
`--source`, and `ReadProjectName` reads `_bmad/config.toml` and falls back to `DefaultSiteTitle =
"BMad Live Docs"` [`ForgeOptions.cs:86, 288-303`] — so a GSD site would be branded with a BMad default.

**Gap 3 — `ArtifactBundle.Module` is structurally unfillable by a non-BMad adapter.** `Module` is `required`,
but `ModuleContext` is BMad-typed to the bone — `BmadModule` is a closed enum and `Detect` keys on
`_bmad/{code}/`. A GSD adapter can only return `ModuleContext.None`, so **the About-SDD matrix's "Planning docs"
and "Commands" columns cannot be ticked for GSD** no matter how good the parsing is (and finding #6 above means
the commands genuinely exist and would populate a `CommandCatalog` beautifully). **This is a ceiling, not a
bug** — state it on the framework page rather than leaving two columns looking like unfinished work. Widening
`ModuleContext` is explicitly out of scope.

### The merge rule (D5) — minimal, and deliberately not the strategic answer

`AppliesTo` is a boolean per adapter, so a repo like CORA makes two adapters both say yes. The registry runs
**every** matching adapter in order and merges:

| Field | Merge rule |
|---|---|
| `Epics`, `Sprint`, `Requirements`, `EpicsSourceFullPath` | **first non-null wins**; a later adapter's non-null value is dropped **with a `Skipped` diagnostic** naming the adapter and the family |
| `Module` | first non-`ModuleContext.None` wins; ties resolve to the first |
| `Retros`, `Diagnostics` | concatenated in adapter order |
| `StoryArtifactsById` | union; a duplicate key is a `Skipped` diagnostic, never a silent overwrite |
| `ConsumedSourceRelatives` | union |

Emit one `Informational` diagnostic naming which adapters matched and which supplied each family. In CORA there
is **no conflict at all** — its `_bmad-output/` holds `prd.md` and `architecture.md` but no `epics.md`, no
`implementation-artifacts/`, and no `sprint-status.yaml` — so BMad contributes `Module` and its planning docs
while GSD Core contributes `Epics`. Order the registry with **specific markers before BMad's fallback**, so a
bare `_bmad-output` tree with no install keeps rendering exactly as today.

> **⚠️ The sharpest risk in this story, and the escalation trigger.** `ForgeOptions.SourceRoot` is
> **single-valued** and anchors *both* the `*.md` enumeration [`SiteGenerator.EnumerateSourceFiles`, `~6439`]
> *and* every source-relative path [`ToSourceRelative`, `~7120`: `Path.GetRelativePath(_options.SourceRoot,
> fullPath)`]. With `SourceRoot = .planning`, CORA's `_bmad-output/planning-artifacts/prd.md` relativizes to
> `..\_bmad-output\...`, which `PathUtil.EscapesRepoRoot` [`PathUtil.cs:31-34`] is designed to reject.
> **So bundle-level merging is cheap; file-discovery-level merging is not.**
>
> Do the bounded thing: resolve **one primary** `SourceRoot` by marker probe, merge at the `ArtifactBundle`
> level, and let the non-primary framework's documents render through whatever the primary root already sees.
> If that proves insufficient for AC #1 in CORA, **stop and escalate** — multi-rooted source discovery is
> explicitly **Story 4.9's** AC #2, not this story's. Record the finding; do not improvise a path scheme.

### Corrected coverage map — GSD Core (`.planning/`) → `ArtifactBundle`

Supersedes Story 12.1's table where they differ. Verified against `C:/dev/CORA` on 2026-08-06.

| GSD Core artifact | Path | Classification | Target projection | Tier | Diagnostic |
|---|---|---|---|---|---|
| Install marker | `.planning/` at repo root | mappable | `AppliesTo` signal (mirrors `_bmad/`) | n/a | none |
| Roadmap | `.planning/ROADMAP.md` | **mappable** | `Epics` → `EpicsModel` + `EpicsSourceFullPath` — **this is the epics source** | `Summarized` | `Malformed` on parse failure |
| Live state | `.planning/STATE.md` | **partially-mappable** | `Sprint` → `SprintStatus` (YAML frontmatter: `milestone`, `status`, `progress.{total,completed}_{phases,plans}`, `percent`) | `Summarized` | `Unsupported` when no per-phase status is recoverable |
| Requirements | `.planning/REQUIREMENTS.md` | **partially-mappable** | **none — `Requirements` stays null (D3)**; renders as a document via the generic pass | `Rendered` | none |
| Project overview | `.planning/PROJECT.md` | partially-mappable | no field; generic `*.md` page. Carries a `## Key Decisions` table (ADR-001…012) — a decision **register**, same ruling as GSD Pi's `DECISIONS.md`: ADR side-channel in spirit, **not mechanically reachable** (`ForgeOptions.AdrFallbackProbeSubdirs` expects one file per decision) | `Rendered` | none |
| Phase dir | `.planning/phases/NN-slug/` (NN may be decimal: `02.1`, `04.5`, `999.1`) | mappable | `EpicInfo` — story-artifact discovery root | n/a | none |
| Phase plan | `NN-YY-PLAN.md` | **mappable** | `StoryArtifactsById[id]` + `ConsumedSourceRelatives` | `Rendered` | `Skipped` on id collision |
| Phase summary | `NN-YY-SUMMARY.md` | partially-mappable | companion to the plan; **not** a `RetroModel` | `Rendered` | `Skipped` (loses the story-artifact slot to `-PLAN.md`) |
| Config | `.planning/config.json` | **unsupported** | none — the source scan is `*.md` (ADR 0020) | `Unsupported` | `Informational` |
| Codebase map | `.planning/codebase/*.md` (7 files) | unsupported | generic `*.md` pages | `Rendered` | none |
| Research | `.planning/research/*.md` (5 files) | unsupported | generic `*.md` pages | `Rendered` | none |
| Todos | `.planning/todos/{pending,completed}/YYYY-MM-DD-slug.md` | unsupported | generic `*.md` pages | `Rendered` | none |
| Per-phase companions | `NN-CONTEXT.md`, `NN-DISCUSSION-LOG.md`, `NN-RESEARCH.md`, `NN-PATTERNS.md`, `NN-UI-SPEC.md`, `NN-AI-SPEC.md`, `NN-VALIDATION.md`, `NN-VERIFICATION.md`, `NN-UAT.md`, `NN-HUMAN-UAT.md` | unsupported | generic `*.md` pages | `Rendered` | none |
| Slash commands | `.claude/commands/gsd/*.md` (67), `.claude/agents/gsd-*.md` | **unsupported** | `CommandCatalog` — blocked by Gap 3, **not** by discoverability | `Unsupported` | none |

**`Retros = []`, no diagnostic.** Unchanged from 12.1's ruling and its reasoning stands: `RetroModel` requires
participants and an `## Action Items` table, and `EpicInfo.HasRetrospective` gates the "In review → finished"
tier on every visual surface — so forcing a `-SUMMARY.md` into `RetroModel` would silently mark phases closed
out on the strength of a build log. Honest absence (NFR8).

### `ROADMAP.md` — the grammar to parse (this is the epics source)

Four sections matter. All four are present in CORA; treat any as optional.

```
## Phases                                  ← overview, grouped by milestone
### Milestone: v1.0 (completed 2026-05-27)
- [x] **Phase 2.1: UI Foundation and Style System** - <desc> (completed 2026-05-14)

## Milestone: v1.0 — Phase Details         ← per-phase detail
### Phase 5: Retrieval and Traceable Answers
**Goal**: …
**Depends on**: Phase 4
**Requirements**: RET-01, RET-02, …        ← requirement→phase map (D3: not projected)
**Success Criteria** (what must be TRUE):  ← 1..N numbered
**Plans**: 8 plans
Plans:
- [x] 05-00-PLAN.md — <desc>               ← the per-plan status signal (finding #7)

## Backlog                                 ← phases with no milestone
### Phase 999.1: Sentiment Analysis … (BACKLOG)

## Progress                                ← per-milestone roll-up table
### Milestone: v1.0 (completed 2026-05-27)
| Phase | Plans Complete | Status | Completed |
| 2.1. UI Foundation and Style System | 6/6 | Complete | 2026-05-14 |
```

Note the em-dash/hyphen inconsistency between `- [x] 01-01-PLAN.md - desc` and `- [x] 02-00-PLAN.md — desc`
(both occur), and that `## Progress` status words are `Complete` / `Not started`, which
`StatusStyles.Canonical` [`StatusStyles.cs:51-58`] maps `"complete"/"completed"` → `done` but has **no arm for
`"not started"`** — it falls through to the unrecognized tier. Map GSD's words explicitly in the adapter rather
than letting them render as `unrecognized`.

Plan filenames match `^(?<phase>\d+(?:\.\d+)?)-(?<plan>\d+)-PLAN\.md$` — a decimal-tolerant pattern, unlike
BMad's `ArtifactFilenamePattern`. **Story id is `{ordinal}.{plan}`** where `ordinal` is D2's synthetic phase
ordinal, so `02.1-03-PLAN.md` in CORA's third-listed phase becomes `"3.3"` and the `"N.M"` contract holds.

## Tasks / Subtasks

- [x] **Task 1 — Confirm the contract and the corrected map against live code and the live repo (AC: #1, #3)**
  - [x] Read in full: `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`,
    `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `SprintStatus.cs`, `RetroModel.cs`,
    `ForgeOptions.cs`, `TestArtifactsModel.cs` (CoverageTier), `ProgressCalculator.cs`, `TaskListParser.cs`,
    `StatusStyles.cs`. Confirm by symbol; line anchors in this story may have drifted.
  - [x] Re-verify the eight findings above against `C:/dev/CORA` — they are the basis of every decision here.
    If one no longer holds, **say so in Completion Notes before acting on it**.
  - [x] Re-read the `gds` vs `gsd` distinction in `AboutSddTemplater.cs` so the two are never conflated.

- [x] **Task 2 — Close Gap 2: framework-neutral source-root discovery (AC: #5)**
  - [x] Replace the single `SourceDirName` literal with an ordered marker set (`_bmad-output`, `.planning`,
    `.gsd`, `.specify`) probed by the same walk-up. Keep `_bmad-output` **first** so this repo's own resolution
    is byte-identical. — ⚠️ **Followed in intent, not to the letter: `_bmad-output` probes LAST.** See
    Completion Notes §D1.
  - [x] Neutralize `DefaultSiteTitle` for a non-BMad root (a GSD site must not be branded "BMad Live Docs") and
    keep `ReadProjectName`'s `_bmad/config.toml` read as one probe among others, not the only one.
  - [x] Keep the `requireSource: false` tolerant path (the webview/extension contract) working unchanged.
  - [x] Tests: a `.planning`-only temp tree resolves; a tree with neither still throws the actionable message;
    a `_bmad-output` tree resolves exactly as before.

- [x] **Task 3 — Close Gap 1: the adapter registry, with the merge rule (AC: #5)**
  - [x] Introduce an ordered `IReadOnlyList<IArtifactAdapter>`, **every** match runs, `BmadArtifactAdapter`
    last as the fallback. Implement the merge table above; every dropped contribution gets a `Skipped`
    diagnostic and the match set gets one `Informational`.
  - [x] Resolve the `IngestEpics` watch-path constraint explicitly (AD-5 / ADR 0027). State in the story record
    which route you took and how you proved watch output is unchanged for a BMad repo. — see Notes §D2.
  - [x] Tests: BMad-only repo → identical bundle to today; GSD-only repo → the GSD adapter's bundle;
    both-markers repo → merged bundle with the documented diagnostics; a repo matching nothing → today's
    fallback behaviour.

- [x] **Task 4 — `GsdCoreArtifactAdapter`: discovery and `AppliesTo` (AC: #1, #2)**
  - [x] `AppliesTo` = `.planning/` directory at `RepoRoot`. Cheap, never throws.
  - [x] Discover `ROADMAP.md`, `STATE.md`, `REQUIREMENTS.md`, `PROJECT.md` by exact name at the `.planning/`
    root; discover phase dirs under `.planning/phases/`; discover plans by the decimal-tolerant filename
    pattern. **Never read frontmatter for identity** (finding #5).
  - [x] Ignored working files are neither ingested nor diagnosed — re-filter through
    `PathUtil.IsIgnoredSourceFile` as `BmadArtifactAdapter.Ingest` does.

- [x] **Task 5 — Project `ROADMAP.md` → `EpicsModel` (AC: #1, #3)**
  - [x] Parse phases from `## Phases` + the `## Milestone: … — Phase Details` sections + `## Backlog`.
  - [x] Assign D2's synthetic ordinal to `EpicInfo.Number` in ROADMAP order; carry the real label in
    `EpicInfo.Title`. **Pin both the ordinal assignment and the `{ordinal}.{plan}` story-id form by a test**
    (AC #3), including a decimal phase and a `999.x` backlog phase.
  - [x] Map each `- [x] NN-YY-PLAN.md` line to a `StoryInfo`, resolve `StoryArtifactsById` to the plan file, and
    add both plan and summary to `ConsumedSourceRelatives`. — ⚠️ **the SUMMARY is deliberately NOT consumed**;
    the coverage map assigns it tier `Rendered`, which consuming it would contradict. See Notes §D3.
  - [x] Pick constant, honest values for `EpicStatus` and `EpicSection` (BMad epics.md conventions with no GSD
    analog) and say in the code comment that they are semantically empty for this framework.
  - [x] `Malformed` diagnostic on a `ROADMAP.md` that will not parse; siblings still ingest.

- [x] **Task 6 — Story status and task tally, honestly (AC: #1, #2, #3)**
  - [x] Do **not** rely on `TaskListParser`/`ExtractStatus` (findings #2 and #8): a GSD plan yields 0/0 tasks
    and a null status, which renders every finished plan as drafted-with-no-plan.
  - [x] Derive `StoryInfo.Status` from ROADMAP's per-plan checkbox, mapped onto the canonical vocabulary via
    `StatusStyles`. Leave `TasksDone`/`TasksTotal` at 0/0 — the badge is suppressed at `total == 0`, which is
    the honest outcome; **do not** synthesize a tally from `<task>` blocks in this story.
  - [x] Reconcile the three completion signals (finding #7): name ROADMAP's checkbox as authoritative, and emit
    one `Informational` diagnostic when `STATE.md`'s roll-up or the SUMMARY set disagrees with it.

- [x] **Task 7 — Project `STATE.md` → `SprintStatus` (AC: #1, #2)**
  - [x] Read the YAML frontmatter (`milestone`, `status`, `progress.*`); emit one `SprintEntry` per phase using
    the same synthetic ordinal so sprint and epics surfaces agree. — plus one per PLAN; see Notes §D4.
  - [x] `Unsupported` diagnostic when no per-phase status is recoverable; `Sprint = null` then, so the page,
    widget and nav gate omit cleanly.
  - [x] `ActionItems` is empty — GSD Core has no analog. Honest absence, no diagnostic.

- [x] **Task 8 — Milestone bands on the epics index (AC: #4)**
  - [x] Carry milestone grouping on `EpicsModel` as a new list defaulting to **empty**, so BMad is unchanged by
    construction. Surface it on `EpicsIndexView` and render the bands in `EpicsTemplater.BuildIndexPage`.
  - [x] Band header carries: milestone name, its state, its completion date when present, and rolled-up phase
    and plan counts. The band body is composed in C#, so the badge is the templater's — take the class from
    `StatusStyles` and always emit the status **word** alongside it, never colour alone (UX-DR17), the same
    guarantee `StatusBadge.vue` enforces by shape on the template-authored side.
  - [x] Empty state: a milestone with no phases must not render as a bare heading (NFR8, and the same trap
    `HierarchyExplorerHtml`'s doc comment records).
  - [x] **Byte-identical proof for BMad**: assert the epics index output for a BMad fixture is unchanged.
  - [x] New CSS goes in `src/SpecScribe/assets/specscribe.css` — **follow the regeneration order below or the
    rules are silently pruned.** — ⚠️ **the documented order was NOT sufficient**; see Notes §F1, the most
    important finding in this story.

- [x] **Task 9 — Framework page and support matrix honesty (AC: #2)**
  - [x] Flip `gsd` to `Supported: true` in `AboutSddTemplater.Frameworks` and give it a real body in place of
    `AppendComingSoonBody`, following `AppendBmadBody`'s shape (what it is → get started → SpecScribe support →
    commands → methodology). Read `fw.Url` from the roster; do not hardcode a literal (this was a 12.1 review
    patch).
  - [x] `AppendMatrixRow` / `AppendFamilySupportTable` take a single `bool supported` and tick all six nouns.
    **GSD needs per-noun granularity**: Epics & Stories ✓, Sprint ✓, Retros — honestly empty, Requirements ✗
    (D3), Planning docs ✗ and Commands ✗ (Gap 3). Widen both helpers; keep BMad/GDS all-✓ so their rows are
    unchanged.
  - [x] Say **in words** on the GSD page why Requirements, Planning docs and Commands are not ticked — an
    absent tick must read as a stated boundary, not as unfinished work (NFR8).
  - [x] Update `README.md`'s "Supported frameworks" table: GSD moves from `🧭 Planned` to supported, matching
    the shipped state.

- [x] **Task 10 — The shared ADR (AC: #5)**
  - [x] One ADR at `docs/adrs/0038-*.md` (0038 is the next free number; 0019 remains claimed-but-unwritten by
    two stories — do not take it), indexed in `docs/adrs/README.md`. Subject: **framework adapter selection,
    minimal multi-adapter merge, and framework-neutral source-root discovery** — one decision, not three.
  - [x] State what it supersedes (`SiteGenerator`'s hardcoded field comment; `ForgeOptions.SourceDirName` as a
    single literal), how it relates to AD-5/ADR 0027 (the watch constraint), and that the **strategic**
    multi-framework policy is deliberately deferred to Story 4.9.
  - [x] Per ADR 0033: any new gate must localize failure to a named artifact, be scoped so a sibling story
    cannot turn it red, and be proven deterministic before pinning. Prefer no new gate. — **no new gate added.**

- [x] **Task 11 — Tests and verification (AC: #1–#5)**
  - [x] New `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs` and `AdapterRegistryTests.cs` — **new files,
    not additions to files a concurrent session may be editing** (CLAUDE.md § hunk attribution; Story 12.1 did
    exactly this for the same reason).
  - [x] Fixtures via `Directory.CreateTempSubdirectory` + `const string` bodies, derived from CORA's real
    shapes. Cover explicitly: a decimal phase (`02.1`), a `999.x` backlog phase, a plan with no `## Tasks`
    heading, a `[x]` plan with no `-SUMMARY.md`, a `STATE.md` whose roll-up disagrees with ROADMAP, an
    unparseable `ROADMAP.md`, and a repo carrying **both** `_bmad-output/` and `.planning/`.
  - [x] Run the full suite. Per CLAUDE.md, if it is red, **establish causality before touching anything** —
    bisect with `git archive HEAD` into the scratchpad; never reset the shared tree.
  - [x] **Verify in a live browser** (CLAUDE.md § Verification): generate against `C:/dev/CORA` with
    `--source`/`--output` into `SpecScribeOutput/`, and inspect the epics index bands, the story cards' status
    and absent task badges, the sprint surface, the diagnostics page, and the GSD framework page. The test
    suite structurally cannot see layout collapse or containment leaks.

## Dev Notes

### The CSS regeneration order is load-bearing (Task 8)

Task 8 adds new markup **and** new rules. `extract:ir-content` **prunes** any rule whose selector names a class
or id it cannot find in the IR, so a stylesheet edit extracted from an IR that predates the markup is dropped
silently, with every gate green and the styles simply absent from the page. Run, in this order:

```sh
dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed the asset
dotnet run --project src/SpecScribe -- generate                  # IR now has the new markup
cd web && npm run extract:ir-content && npm run check:ir-content # derive from THAT IR
cd web && npm run build:package                                  # renderer bundles the CSS
dotnet run --project src/SpecScribe -- generate                  # render with it
```

Two generates, deliberately. `--no-incremental` is not optional: an incremental build reuses the cached assembly
and never re-embeds a changed asset, so what you measure — and what the browser serves — is stale.

### Which gate can see what

- **`npm run check:parity` cannot see a C#-side change.** Its corpus IR is frozen, so a change to the epics
  index composition renders from the *pinned* input and the gate stays green. A green parity run means "the
  renderer still behaves the same on the frozen fixture", never "my rendering change is safe". Cover Task 8
  with unit tests over `EpicsTemplater`/`EpicsViewBuilder` **and** live-browser inspection.
- **`check:ir-content` cannot catch a bug in its own derivation** — it re-derives through the same
  `harvest`/`selectorIsUsed` code, so a wrongly-pruned rule is pruned identically on both sides and the diff is
  empty. `web/test/ir-content-harvest.test.mjs` pins the derivation itself; extend it rather than trusting the
  round-trip.
- **Never regenerate a moved gate reflexively.** If a gate moves and you did not touch rendering, audit the
  harness first and establish causality by bisecting into a throwaway tree.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core. `GsdCoreArtifactAdapter`
  translates into `ArtifactBundle` and never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records. The milestone
  band is a **view-model** addition rendered by the templater, not markup composed in the adapter.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — insight enrichment stays additive and non-blocking; progress/git
  enrichment stays in the projection path and reaches the adapter through the `ProgressProjection` callback.
- **AD-5** [ARCHITECTURE-SPINE.md:66-74] + **ADR 0027** — watch behaviour must not regress; "safe" means proven
  byte-identical to a full rebuild. This is the registry's hardest constraint (Task 3).
- **ADR 0016 / 0034** — the IR carries rendered prose HTML and the site is rendered from it. The epics index
  body is composed in C# and carried through, so Task 8 needs **no Nuxt change and no IR schema bump**. Confirm
  that holds before assuming it.
- **ADR 0020** — a non-markdown source may be read only when module-declared, exact-filename, directory-scoped
  and presence-gated. `.planning/config.json` meets none of the machinery today (the presence gate is
  `ModuleContext.IsModulePresent`, which is BMad-keyed), which is why it is `Unsupported`/`Informational` here.
  Widening ADR 0020's gate to non-BMad frameworks is a real question — **name it, do not silently do it.**
- **Seed, not invariant** [ARCHITECTURE-SPINE.md:100-105] — "exact adapter loading mechanics" are explicitly
  open, which is what makes Task 3 legitimate. But the package/namespace split is **not** open: the project is
  single-project, single-namespace. Do not create `SpecScribe.Adapters.Gsd`.
- **NFR8** [epics.md:137] — absent, not broken or misleadingly empty. This story's whole posture: no fabricated
  requirement ids, no retros invented from build logs, no ticked matrix cells that are not real.
- **FR4** [epics.md:46] — *"Add GSD and GSD-Pi baseline support so representative repositories render key
  planning and tracking artifacts without fatal errors."*

### Anti-patterns to prevent

- **Trusting Story 12.1's coverage map over the live repo.** Eight of its claims were checked and six failed.
  The spike said so itself. Verify, then act.
- **Counting `- [x]` in a `PLAN.md`.** There are none (0 checked across 58 files). Use ROADMAP's checkbox.
- **Reading `phase:` from plan frontmatter.** Eight encodings, one repo. The filename is the key.
- **Mapping `CONV-01` onto `RequirementKind.Functional`.** `RequirementInfo.Id` would render `FR1` — an id GSD
  never wrote. D3 forbids it.
- **Letting a GSD status render as `unrecognized`.** `"Not started"` has no `StatusStyles.Canonical` arm; map
  GSD's words in the adapter.
- **Averaging or silently reconciling the three completion signals.** Name one, diagnose disagreement.
- **Improvising multi-rooted source discovery.** That is Story 4.9's AC #2. Escalate instead.
- **Widening `ModuleContext`, `RequirementKind`, or `EpicInfo.Number`.** Each is a shared-model change with its
  own ADR-sized blast radius, and each is explicitly out of scope here.
- **Proposing a second registry ADR.** One shared ADR (Task 10), which Epics 11/13/14/15 inherit.
- **Reading `C:/dev/CORA` from a test.** CI has no such path. Fixtures are temp dirs.
- **Silently reformatting or reflowing files a concurrent session may hold.** Prefer new files; attribute by
  hunk if you must share one.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/12-2-gsd-core-baseline-adapter-coverage.md`
- Sprint key: `12-2-gsd-core-baseline-adapter-coverage`
- Sibling: `12-3-gsd-pi-baseline-adapter-coverage` (`backlog`) — inherits this story's registry, source-root
  discovery, merge rule and ADR. Do not duplicate any of them there.
- Seated by this create-story: `4-9-multi-framework-coexistence-strategy-spike` (`backlog`, Epic 4), co-landed
  in `epics.md` and `sprint-status.yaml`.
- Expected new files: `src/SpecScribe/GsdCoreArtifactAdapter.cs`, an adapter-registry type,
  `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs`, `tests/SpecScribe.Tests/AdapterRegistryTests.cs`,
  `docs/adrs/0038-*.md`.
- Expected modified: `SiteGenerator.cs` (adapter field + both call sites), `ForgeOptions.cs` (marker set, site
  title), `EpicsModel.cs` / `EpicsView.cs` / `EpicsViewBuilder.cs` / `EpicsTemplater.cs` (milestone bands),
  `AboutSddTemplater.cs` (roster + per-noun matrix + GSD body), `assets/specscribe.css`, `README.md`,
  `docs/adrs/README.md`.
- **No new dependencies.** `YamlDotNet` 18.1.0 is already referenced [`SpecScribe.csproj:48`] and
  `SprintStatusParser` shows the house pattern: deserialize only the blocks you need, with line regexes for the
  scalars, so one malformed region cannot take the file down. Use it for `STATE.md`'s frontmatter. Markdig
  handles the prose. Do not add a NuGet or npm package; if one seems necessary, that is a signal the scope has
  drifted.
- **`.specscribe/analysis/` does not exist at create-story.** Absent means UNKNOWN, never clean. Run
  `node tools/analysis-digest/index.mjs` and read the shard for each file you touch before editing it.
- **This is a large story** — the owner declined splitting the prerequisites out. If it needs to land in stages,
  the natural seam is Tasks 2–3 + Task 10 (prerequisites and ADR) before Tasks 4–9 (coverage and surfaces).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 12, Story 12.2] — ACs #1–#3 verbatim; #4/#5 added
  2026-08-06 at create-story recording decisions D1–D5.
- [Source: `_bmad-output/planning-artifacts/epics.md:46, 137`] — FR4 and NFR8 exact wording.
- [Source: `_bmad-output/implementation-artifacts/12-1-gsd-and-gsd-pi-integration-spike.md`] — the coverage map
  this story corrects; its Completion Notes carry the two-adapter-surfaces decision, the `Retros = []` ruling
  (which stands), the `DECISIONS.md`/register ruling, and the three gaps. **Its Debug Log's residual-uncertainty
  note is the reason the CORA inspection was required.**
- [Source: `src/SpecScribe/IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`,
  `BmadArtifactAdapter.cs`] — the contract and its one reference implementation.
- [Source: `src/SpecScribe/EpicsModel.cs:9-10, 56`, `RequirementsModel.cs:3, 39, 43-49, 70`,
  `SprintStatus.cs:11`] — the int/enum ceilings behind decisions D2 and D3.
- [Source: `src/SpecScribe/ForgeOptions.cs:86-95, 130-206, 288-303`] — Gap 2 in full.
- [Source: `src/SpecScribe/SiteGenerator.cs:~60-63, ~501, ~1437, ~6439, ~7120`] — Gap 1, both ingest call sites,
  the `*.md` enumeration, and the single-root relativization anchor.
- [Source: `src/SpecScribe/TestArtifactsModel.cs:18-76`] — `CoverageTier`/`CoverageTiers`; reuse, never mint.
- [Source: `src/SpecScribe/TaskListParser.cs:11-22`, `EpicsParser.cs:23`, `StatusStyles.cs:51-58`,
  `ProgressCalculator.cs:20-76`] — findings #2 and #8 and the canonical status vocabulary.
- [Source: `src/SpecScribe/AboutSddTemplater.cs` — `Frameworks`, `AppendSupportMatrix`, `AppendMatrixRow`,
  `AppendFamilySupportTable`, `AppendComingSoonBody`, `AppendBmadBody`] — Task 9's surface.
- [Source: `docs/adrs/0020-module-declared-non-markdown-sources.md`] — the four-condition rule for non-markdown
  sources, and the "no sixth diagnostic category" restatement.
- [Source: `docs/adrs/0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md`,
  `0033-content-drift-gates-are-targeted-and-regenerable.md`, `0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md`,
  `0016-ir-carries-rendered-prose-html.md`] — the watch, gate and rendering constraints Tasks 3, 8 and 10 sit under.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2/AD-4/AD-5, § Seed Not Invariant]
- [Source: `tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs:13-118`] — the temp-dir fixture style Task 11 mirrors.
- [Repo: `C:/dev/CORA/.planning/`, inspected live 2026-08-06] — the representative current-version GSD Core
  repository AC #1 requires. **A reference, never a test dependency.**
- [Source: `CLAUDE.md` § Concurrent work, § Changing specscribe.css, § Which gate is which, § Verification]
- **Memory:** [[epic-4-adapter-contract-scope]] (foundation-only, no package split, spike-led per-framework
  pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — but
  ONE shared registry ADR).

### Git intelligence summary

Baseline `9580f62` (clean tree at create-story). Recent commits are Epic 23.6 close-out and CI/gate fixes
(`ir-content.css` regeneration with `--deep-git`, a sunburst palette seed fixing a CI flake) — unrelated to this
story's scope, but they confirm the content-drift gates are live and sensitive, which is why the CSS
regeneration order above is stated in full. The only GSD-related code in the repo is Story 12.1's roster pinning
in `AboutSddTemplater.cs` and its four `AboutSddFrameworkRosterTests`; those tests assert the two GSD products
stay distinct by marker directory and docs host, so **Task 9 must keep them passing** when it flips `gsd` to
supported. No adapter, registry, or parser work exists yet — this story starts from a clean slate on the GSD
side against a well-established contract with one working reference implementation.

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (1M context), `bmad-dev-story`, 2026-08-06. Worktree
`.claude/worktrees/story-12-2-gsd-core-adapter`, branch `worktree-story-12-2-gsd-core-adapter`, from `f9d7529`
(story creation, one commit past the recorded baseline `9580f62`).

### Debug Log References

**Green baseline established before any edit**: 2929 passed / 0 failed / 3 skipped.
**Final**: 2963 passed / 0 failed / 3 skipped (+34 new). Web: vitest 166 passed, `check:tokens`,
`check:assets`, `check:ir-content`, `check:parity` (24 routes / 14 families) all green.

**All eight of create-story's findings re-verified against `C:/dev/CORA` before acting on any of them.** Every
one held, by command not inference: 168 files under `.planning/`; decimal phase dirs `02.1`, `04.5` and backlog
`999.1/.2/.3`; **0 checked / 39 unchecked** checkboxes across all 58 `PLAN.md` files with only 25 carrying a
`## Tasks` heading; **0** `Status:`/`status:` lines anywhere in those 58 files; 42 `-SUMMARY.md` files against
ROADMAP's 58 `[x]` and `STATE.md`'s `completed_plans: 42 / total_plans: 50`. Nothing needed correcting.

**Two test-suite investigations, both proven NOT to be this story's** (CLAUDE.md § establish causality first):

1. `FileWatcherServiceTests` failed intermittently on full-suite runs (1–2 failures). **Not a regression.** The
   *failing test varied* across runs — `BurstOfSaves_CoalescesAndLeavesCoherentOutput`,
   `SprintStatusYaml_AddedThenEditedThenRemoved…`, `EditingAStoryFile_RegeneratesThroughTheOrdinaryMarkdownRoute`
   — which a deterministic regression cannot do; the class passed **3/3 in isolation**, and two consecutive full
   runs were green at 2963/2963. These use a real `FileSystemWatcher` against a 400 ms debounce, and this story
   added 34 tests to the parallel pool. Load-sensitivity, pre-existing, not caused by the registry swap.
2. Pristine `HEAD` bisected into `$CLAUDE_JOB_DIR/tmp/pristine` via `git archive` (never a reset of any tree):
   2929/2929 green there, and it reports the *same* `errors=1` on `generate --deep-git` that the worktree did —
   proving that error pre-existed this story. It was the renderer package not being built;
   `SPECSCRIBE_RENDERER_DIR` pointed at the worktree's own `web/.output` takes it to `errors=0`.

**Content-drift gate discipline.** `ir-content.css` was regenerated three times before being locked in, because
the first two runs were provably wrong and *both looked fine*:

- Run 1 (`generate` without `--deep-git`) removed **1,557 lines** of deep-git-only rules. The gate was GREEN,
  because `check:ir-content` re-derives from the same IR the extractor read.
- Run 2 (`--deep-git`, renderer unbuilt) added `.status-badge.diag-error` — false drift from the `errors=1`
  above, which would have reddened CI where the renderer *is* built.
- Run 3 (`--deep-git`, `SPECSCRIBE_RENDERER_DIR` set, `errors=0`) produced a purely additive diff: **7 selectors
  added, 0 removed.** Confirmed **stable across two repeated runs** (identical SHA-256) before locking, per
  CLAUDE.md.

### Completion Notes List

**HEADLINE — a GSD Core repository generates, for the first time in this project.** `C:/dev/CORA` produced
**160 pages, 0 errors**, branded **"CORA"** (not "BMad Live Docs"), with 14 phases across 4 milestone bands and
58 plans as stories. Before this story it did not get as far as an adapter: `ForgeOptions.Resolve` threw
`DirectoryNotFoundException` while resolving paths.

**LIVE BROWSER VERIFICATION: PASS**, and it earned its place — see §F2. Real computed styles on the generated
CORA site: all 4 bands `background: rgb(250,247,242)` (`--warm-white`), `1px solid` border, `6px` radius,
header `display: flex`, heights 288/181/162/201 px (no collapse), chip grid `display: grid` at 6 columns with
168×99 px chips, `scrollWidth == clientWidth` (no horizontal overflow), no band overlapping its neighbour, every
band badge carrying its WORD ("Done"/"Drafted", UX-DR17), 0 task badges on epic-3, and the GSD page's `n/a` cell
`font-style: italic` **with** its `aria-label`. Screenshots captured.

#### Decisions taken that differ from the task text — each stated, none silent

**§D1 — `_bmad-output` probes LAST, not first.** Task 2 said to keep it first "so this repo's own resolution is
byte-identical". The stated GOAL is preserved exactly; the ordering is not. `_bmad-output` is an *output* folder
(a BMad project writes one regardless of what else is present) while `.planning`/`.gsd`/`.specify` are framework
*install* markers, so probing it first makes it a universal winner. **CORA carries BOTH** — its `_bmad-output`
holds 6 planning documents, its `.planning` holds 168 files / 11 phase dirs / 58 plans. Following the letter
would have resolved `SourceRoot` to the six-file tree and left every GSD artifact outside it, where
`PathUtil.EscapesRepoRoot` rejects the paths — i.e. AC #1 unmeetable. Ordering by specificity costs nothing in
the case the instruction protected: a BMad-only repo (this one included) has no other marker, and
`Resolve_BmadOutputTree_ResolvesExactlyAsBefore` pins that. It also matches the same story's own registry
guidance verbatim ("specific markers before BMad's fallback"). Recorded in ADR 0038 §Decision 3.

**§D2 — the watch constraint: LIFTED onto the interface, not degraded.** `IArtifactAdapter` gains `IngestEpics`
and `EpicsIngest` is promoted from a nested record to a top-level one. Of the two sanctioned routes, this is the
one that makes the guarantee *structural*: BMad's implementation is the same method body at the same call site,
so watch output for a BMad repo is byte-identical **by construction**, not by a measurement that could drift
(ADR 0027's definition of "safe"). `AdapterRegistry.IngestEpics` returns a single matching adapter's result
verbatim; `IngestEpics_ResolvesTheSameOwnerAsAFullIngest` pins that a scoped pass and a full build agree about
the epics owner for both a BMad and a GSD repo.

**§D3 — `-SUMMARY.md` is NOT added to `ConsumedSourceRelatives`.** Task 5's bullet and the coverage-map table
disagree; the table is the more specific statement and it wins. It assigns the summary tier **`Rendered`**,
which means "a full page exists" — and `ConsumedSourceRelatives` is documented as "consumed into a *dedicated
surface*", which the summary is not (the plan is the story artifact). Consuming it would have deleted its page
and made the declared tier false. The `Skipped` diagnostic the map asks for is still emitted, recording that the
plan won the story-artifact slot.

**§D4 — the sprint ledger carries story entries as well as phase entries.** Task 7 says "one `SprintEntry` per
phase". `SprintTemplater.GroupByEpic` buckets by kind and renders story rows beneath each epic, so an
epic-only ledger would have produced a sprint page whose entire body is empty — the "misleadingly empty" surface
NFR8 forbids, which is worse than honest omission. Both kinds derive from the one authoritative signal
(ROADMAP's checkbox), so no second source of truth is introduced.

**§D5 — one line changed in `ProgressCalculator`,** the only edit to a shared file that a sibling story might
hold: `story.Status = status` became `story.Status = status ?? story.Status`. Without it the projection callback
silently erased the status the GSD adapter derived from ROADMAP and every finished plan rendered as a drafted
story with no task plan (finding #8's defect, reproduced exactly). **BMad is unaffected byte-for-byte**:
`EpicsParser` never sets `StoryInfo.Status` (only the `EpicStatus` enum), so the value is null on entry for
every BMad story and `null ?? null` is the assignment this replaced.

#### Findings a reviewer should not have to rediscover

**§F1 — ⚠️ THE DOCUMENTED CSS REGENERATION ORDER CANNOT SAVE CROSS-FRAMEWORK MARKUP. This is the most
important finding here.** `extract:ir-content` prunes any rule whose selector names a class absent from the IR,
and the extraction corpus is **this repository's own IR — and this repository is a BMad project**. Milestone
bands only ever render for a framework that HAS a milestone level, so no harvest run here can see them. Measured,
not theorised: with the stylesheet edit in place and the order followed exactly, **all five `.milestone-band*`
rules were pruned and `check:ir-content` stayed GREEN** — the bands would have shipped unstyled on a real GSD
site with no gate able to see it. Fixed by seeding `CONDITIONAL_CLASSES` in `web/scripts/ir-content-lib.mjs`,
the existing seam for exactly this (the sunburst-black-fill and `owner-author-2` incidents), and pinned by two
new cases in `web/test/ir-content-harvest.test.mjs` — which is the layer the round-trip gate structurally cannot
check. **Every remaining framework epic (11, 12.3, 13, 14, 15) will hit this**; the seed list now says so in a
comment. `.sdd-check--na` needed no seeding, because the About-SDD page *is* generated for this repo.

**§F2 — the live-browser check found a real scope failure before it found a pass.** The first run over `file://`
reported 19 problems: transparent backgrounds, zero borders, `display: block` headers, 17 px collapsed chips.
Cause: Nuxt emits `<link rel="stylesheet" … crossorigin>`, and Chromium refuses `crossorigin` stylesheets over
`file://` (CORS: "only supported for protocol schemes: chrome, data, http, https"), so **every** stylesheet
failed to load — pre-existing `.epic-overview` grid included. Served over HTTP the same page passes cleanly.
**⚠️ Worth an owner decision, and NOT this story's to fix:** the same `crossorigin` attribute is on
SpecScribe's own generated `index.html`, so a portal opened directly from disk in Chromium renders unstyled.
NFR-3 / ADR 0012 §1 say the portal must render offline from `file://`. Pre-existing, unrelated to this story's
changes, and reported rather than patched.

**§F3 — the merge case is live, not hypothetical.** CORA carries `_bmad/` (with `bmm`, `core`, `tea`, …),
`_bmad-output/` and `.planning/`, so BOTH adapters match on a real repository. D5's prediction held exactly:
GSD Core supplies the epics family (its ROADMAP is the only epics source inside the resolved source root) and
BMad supplies the module identity, with one `Informational` naming who supplied what and a second naming the
marker that did not become primary. The escalation trigger the story armed (§"the sharpest risk") was reached
and resolved **within** the sanctioned bounded answer — one primary `SourceRoot`, bundle-level merge, the
non-primary framework's loose documents diagnosed as not rendering. No improvised path scheme; Story 4.9's
question is untouched.

**§F4 — `.status-badge.diag-error` is a false-drift hazard in the ir-content gate.** It is emitted only when a
run reports errors, so whether it appears in the committed sheet depends on the regenerating machine's
environment rather than on any code change. It is *not* in `CONDITIONAL_CLASSES`. Not fixed here (it belongs to
whoever owns that surface), but it will bite the next person who regenerates with an unbuilt renderer.

**§F5 — ADR 0037 is missing from `docs/adrs/README.md`'s index.** Noticed while adding 0038's entry. Left alone
deliberately, per CLAUDE.md's hunk-attribution rule: it belongs to the story that wrote it, and silently
adopting another story's hunk is what makes review attribution fail.

**§F6 — GSD story status words render lowercase ("done") on story cards.** Verified this is **pre-existing,
shared behaviour, not new**: SpecScribe's own BMad site renders `done => 'done'` on its story badges too (the
card shows the raw status word; only epic badges route through `StatusStyles.EpicLabel`). Left as-is for
consistency rather than special-casing GSD.

**Not done, and deliberately so:** `ModuleContext` not widened (Gap 3 — stated as a ceiling on the GSD page,
where the 67 in-repo `/gsd-*` commands are named as blocked by the model rather than by discoverability); ADR
0020's non-markdown gate not widened (`config.json` reported as uninterpreted, per the map); `RequirementKind`
and `EpicInfo.Number` not widened; no new content-drift gate (ADR 0033); no new NuGet or npm dependency.

### File List

**New — source**
- `src/SpecScribe/GsdCoreArtifactAdapter.cs`
- `src/SpecScribe/AdapterRegistry.cs`
- `src/SpecScribe/EpicsIngest.cs` (promoted from a nested `BmadArtifactAdapter` record)

**New — tests**
- `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs` (20 tests)
- `tests/SpecScribe.Tests/AdapterRegistryTests.cs` (14 tests)

**New — docs**
- `docs/adrs/0038-framework-adapter-selection-and-neutral-source-root-discovery.md`

**Modified — source**
- `src/SpecScribe/IArtifactAdapter.cs` (adds `IngestEpics` to the contract)
- `src/SpecScribe/BmadArtifactAdapter.cs` (nested `EpicsIngest` removed; `AppliesTo` doc updated — method bodies unchanged)
- `src/SpecScribe/ForgeOptions.cs` (`SourceDirNames`, marker walk-up, `ResolveSiteTitle`/`ReadProjectDocTitle`)
- `src/SpecScribe/SiteGenerator.cs` (adapter field → `AdapterRegistry`; `gsdPresent` threaded to About-SDD)
- `src/SpecScribe/ProgressCalculator.cs` (one line — see §D5)
- `src/SpecScribe/EpicsModel.cs` (`MilestoneInfo`, `EpicsModel.Milestones`)
- `src/SpecScribe/EpicsView.cs` (`MilestoneBandView`, `EpicsIndexView.Milestones`)
- `src/SpecScribe/EpicsViewBuilder.cs` (`BuildMilestoneBands`)
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` (`AppendMilestoneBands` + the branch)
- `src/SpecScribe/AboutSddTemplater.cs` (`FamilySupport`/`FamilyMatrix`, `AppendGsdCoreBody`, roster `Supported: true`)
- `src/SpecScribe/Mermaid.cs` (`SddGsdCoreDiagram`)
- `src/SpecScribe/assets/specscribe.css` (milestone-band block; `.sdd-check--na`)

**Modified — web**
- `web/scripts/ir-content-lib.mjs` (`CONDITIONAL_CLASSES` cross-framework seed — see §F1)
- `web/test/ir-content-harvest.test.mjs` (2 new seeding tests)
- `web/assets/ir-content.css`, `web/assets/ir-content.manifest.json` (regenerated: +7 selectors, 0 removed)

**Modified — docs**
- `README.md` (GSD → Supported, with the per-family caveat; Roadmap line)
- `docs/adrs/README.md` (ADR 0038 index entry)
- `_bmad-output/implementation-artifacts/12-2-gsd-core-baseline-adapter-coverage.md` (this file)
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-08-06 — **Story 12.2 implemented (dev-story, worktree `story-12-2-gsd-core-adapter` off `f9d7529`).**
  `ready-for-dev → in-progress → review`. All 11 tasks complete; ACs #1–#5 satisfied. **A GSD Core repository
  generates for the first time** (`C:/dev/CORA` → 160 pages, 0 errors, branded "CORA", 14 phases in 4 milestone
  bands, 58 plans as stories). Tests 2929 → 2963 (+34, 0 failures); all four web gates green; live-browser
  computed-style verification passed. Both shared prerequisites closed (adapter registry with D5's merge;
  framework-neutral source-root discovery) plus ADR 0038, which Epics 11/12.3/13/14/15 inherit. **Four decisions
  differ from the task text and are recorded in Completion Notes §D1–§D5** — most consequentially that
  `_bmad-output` probes LAST rather than first (following Task 2's stated goal rather than its letter, because
  the reference repo carries both markers and the letter would have made AC #1 unmeetable). **The most important
  finding is §F1**: the documented CSS regeneration order structurally cannot save markup only a non-BMad repo
  produces — measured, all five band rules were pruned with the gate GREEN — which every remaining framework
  epic will hit. Two pre-existing issues found and reported rather than patched: the `crossorigin` attribute
  makes the portal render unstyled from `file://` in Chromium (§F2, NFR-3-relevant), and ADR 0037 is missing
  from the ADR index (§F5).
- 2026-08-06 — Story 12.2 drafted (create-story, baseline `9580f62`). Ultimate context engine analysis completed
  — comprehensive developer guide created. **Drafted against a REAL GSD Core repository (`C:/dev/CORA`, 168
  files under `.planning/`) rather than vendor documentation, which Story 12.1 explicitly flagged as required
  before writing discovery globs — and six of that spike's derived claims did not survive contact**: decimal
  phase numbers vs an `int` `EpicInfo.Number`; zero usable task checkboxes across all 58 `PLAN.md` files (so
  12.1's "Task → TasksDone/TasksTotal is not a compromise at all for Core" is false); open-ended requirement id
  prefixes rather than `REQ-001`, against a `RequirementKind` whose `Id` throws; an FR→Phase traceability table
  that *does* exist (but is stale); eight different encodings of the `phase:` frontmatter key, making the
  filename the only stable id; and the `/gsd-*` commands being in-repo after all. Two further findings with no
  12.1 counterpart: three completion signals that disagree (ROADMAP 58/58, STATE.md 42/50, 42 SUMMARY files),
  and no `Status:` line anywhere — which together would have rendered every finished plan as a drafted story
  with no task plan. Five owner decisions elicited up front (D1 Phase→Epic/Plan→Story with milestone bands on
  the epics index; D2 synthetic ordinal for decimal phases; D3 render `REQUIREMENTS.md`, leave `Requirements`
  null; D4 this story owns both shared prerequisites plus one shared ADR; D5 minimal multi-adapter merge). ACs
  #4 and #5 added to `epics.md` and **new Story 4.9 (Multi-Framework Coexistence Strategy Spike) seated in Epic
  4**, both co-landed in `sprint-status.yaml` in the same change per the decision-records rule.
