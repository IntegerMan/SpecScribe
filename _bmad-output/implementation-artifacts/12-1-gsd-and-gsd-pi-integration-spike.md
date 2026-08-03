---
baseline_commit: b397084d7704e1df670eeaafd6ba2c11ae3a696a
---

# Story 12.1: GSD and GSD-Pi Integration Spike

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support GSD and GSD-Pi,
I want the GSD family's artifact set mapped against the shared adapter contract before coverage work begins,
so that baseline coverage starts with a defined scope, declared coverage tiers, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 (one per framework) were extracted 2026-07-10 into five appended, spike-led epics (11–15; see `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`) so each framework gets its own upfront mapping exercise instead of guessing. **This is the second of those five spikes** — Story 11.1 (Spec Kit) ran first (`ready-for-dev`); Epic 12 is currently `backlog` (this story creation moves it to `in-progress`). Epics 13–15 (SpecFlow, Squad, Superpowers) are untouched and repeat this same shape later.

**The one-line test for "is this in scope?":** if the change *surveys* GSD/GSD-Pi repos, *classifies* their artifacts against the existing `ArtifactBundle`/model shapes, *assigns a coverage tier* per mappable type, or *writes* a coverage map + non-goals list → in. If it *builds* a `GsdArtifactAdapter`, parses a single `.gsd/` file into a real model, or lands any `src/`/`tests/` change → out; that is Story 12.2 (GSD and GSD-Pi Baseline Adapter Coverage, not yet created).

**Precedent for this shape — read both:** Story 11.1 (`11-1-spec-kit-integration-spike.md`, `ready-for-dev`) is the immediately-preceding sibling spike with the identical AC skeleton — mirror its structure and its Completion-Notes-as-deliverable discipline. Story 19.1 (Work-Graph Model and Coverage Spike, `ready-for-dev`) is the older pure-tracing, no-code precedent. Neither built production code, and neither should this. If you find yourself wanting to write a branch and a scaffold, stop — you have drifted into 12.2.

## What makes this spike different from 11.1 (do not just copy Spec Kit's answers)

Story 12.1 is **not** a find-and-replace of 11.1. Four structural differences drive its findings, and getting them wrong is the primary failure mode:

1. **It covers TWO frameworks, not one.** GSD (Get Shit Done) and GSD-Pi are a *family*: GSD-Pi is GSD's successor/evolution and — verified at create-story — shares the same `.gsd/` marker, the same authoritative-SQLite-plus-markdown-projection model, and the same **Milestone → Slice → Task** hierarchy. **The central cross-framework decision this spike must resolve:** do GSD and GSD-Pi collapse to ONE adapter surface (one `AppliesTo` sniff, one ingest path, with per-variant tolerance) or genuinely diverge enough to need two? Do not silently assume either; decide it against real repos of *both* and state the consequence for 12.2.
2. **Coverage tiers are MANDATORY here, not optional.** 11.1 explicitly deferred "coverage tier" as Epic-12 vocabulary. Story 12.1's own AC #1 requires *"the target shared-model projection **and declared coverage tier** are named for each mappable type,"* and FR-4 defines the tier vocabulary directly: **rendered / summarized / unsupported** [requirements-catalog.md:18]. This spike must (a) fix a small, explicit tier vocabulary aligned to FR-4's three words, (b) assign one to every mappable/partially-mappable artifact type, and (c) hand 12.2 an agreed tier ladder. Tiers are orthogonal to the mappable/partially-mappable/unsupported *classification* — a type can be "mappable" yet declared tier "summarized" (rendered as a digest, not a full first-class page). Say which axis you mean each time.
3. **GSD is NOT markdown-native — its authoritative source is a gitignored SQLite DB.** `.gsd/gsd.db` is the single source of truth; the `.gsd/*.md` files (`PROJECT.md`, `REQUIREMENTS.md`, `DECISIONS.md`, `KNOWLEDGE.md`, `STATE.md`, and the `milestones/**/**.md` projections) are *refreshed from the DB* and the DB is gitignored. No prior framework (BMad, Spec Kit) had this. SpecScribe reads **markdown, never a database** — so this spike must resolve: are the `.md` projections reliably present/committed in a real GSD repo, or can they be stale/absent when only the gitignored DB is current? That reliability question decides whether GSD content is `rendered` (trust the md), `summarized` (md may lag), or gets an `Informational`/`Unsupported` notice (e.g. "`.gsd/` present but markdown projections absent — run the GSD sync command"). This is a genuine, framework-specific finding, not a copy of anything in 11.1.
4. **Several families Spec Kit left null are candidates for GSD.** Spec Kit expected `Sprint = null`, `Requirements = null`, `Retros = []`. GSD ships `STATE.md` (a status projection — a `Sprint` *candidate*, likely partially-mappable), `REQUIREMENTS.md` (a requirement contract — a `Requirements` *candidate*, verify whether it's FR-numbered or prose), and `S##-SUMMARY.md` per-slice execution summaries (a possible `Retros` analog — verify). Do **not** carry Spec Kit's "stays null" table over; re-derive every family for GSD against real repos.

## Acceptance Criteria

1. **Given** representative current-version GSD and GSD-Pi repositories, **when** the GSD family's artifact set is surveyed against the shared adapter contract's `ArtifactBundle` and projection model, **then** a written coverage map classifies each GSD/GSD-Pi artifact type as mappable, partially-mappable, or unsupported, **and** the target shared-model projection **and declared coverage tier** are named for each mappable type.

2. **Given** GSD/GSD-Pi conventions that exceed the shared projection model or that SpecScribe will deliberately not support, **when** the spike documents its findings, **then** framework-extra data is recorded as candidate projection extensions or explicit non-goals, **and** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2381-2399`]

## Context & Scope

### ⚠️ Name-collision trap: GSD ≠ GDS. Do not conflate them.

The codebase already ships a framework whose id is the near-anagram `gds`: **BMad GDS (Game Dev Studio)** — an installable *BMad module* (`_bmad/gds`) that rides `BmadArtifactAdapter` and is already **fully supported** [AboutSddTemplater.cs:13, 84, 115, 191-222]. **That is not this story.** Epic 12's targets are `gsd` and `gsd-pi` (Get Shit Done) — unrelated third-party frameworks, both `Supported: false` placeholders [AboutSddTemplater.cs:15-16]. When reading code, grep carefully: `AppendGdsBody`/`AboutSddGdsOutputPath`/`_bmad/gds` are the *supported BMad module*; `AboutSddGsdOutputPath`/`AboutSddGsdPiOutputPath`/`.gsd/` are *this spike's* targets. Mixing them up will produce a completely wrong coverage map.

### The contract this spike maps against (read the real code, not just the epic prose)

- **`IArtifactAdapter`** [src/SpecScribe/IArtifactAdapter.cs:19-38] — two methods: `AppliesTo(ForgeOptions, sourceFiles)` (cheap self-selection sniff, never throws) and `Ingest(ForgeOptions, sourceFiles, ProgressProjection?)` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics` instead).
- **`ArtifactBundle`** [src/SpecScribe/ArtifactBundle.cs:15-57] — the ONLY shape a new adapter must produce. Its fields, verbatim (line-verified against current main):
  | Field | Type | Line | Null/empty-safe? |
  |---|---|---|---|
  | `Module` | `ModuleContext` | 15 | Never null — absent detection is `ModuleContext.None` |
  | `Sprint` | `SprintStatus?` | 20 | Null when absent |
  | `Retros` | `IReadOnlyList<RetroModel>` | 25 | Empty when none |
  | `Epics` | `EpicsModel?` | 30 | Null when absent/unparseable |
  | `Requirements` | `RequirementsModel?` | 36 | Null when absent |
  | `EpicsSourceFullPath` | `string?` | 42 | For generic-page exclusion |
  | `StoryArtifactsById` | `IReadOnlyDictionary<string,string>` | 47 | Story id → detail-artifact path |
  | `ConsumedSourceRelatives` | `IReadOnlyCollection<string>` | 53 | Files claimed by dedicated surfaces |
  | `Diagnostics` | `IReadOnlyList<AdapterDiagnostic>` | 57 | Non-fatal problems |
- **`AdapterDiagnostic(Category, RelativePath, Message)`** with `enum AdapterDiagnosticCategory` [src/SpecScribe/AdapterDiagnostic.cs:7-31] — `Category` is one of `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested, e.g. a duplicate), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed). **These five categories are the entire non-fatal vocabulary this spike's "unsupported conventions" and "non-fatal notice they will emit" (AC #2) must map onto** — do not invent a sixth. (Note: these five diagnostic categories are distinct from AC #1's three *coverage tiers* — see difference #2 above; keep the two vocabularies separate in your findings.)
- **The one existing adapter to mirror, not copy verbatim:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344]. Read it end to end — it is the working example of "self-selection sniff → discover files by well-known name/location → parse each family independently → never let one family's failure kill the others → diagnose non-fatally." Its `AppliesTo` sniffs `_bmad/` at the repo root [BmadArtifactAdapter.cs:77]; the equivalent GSD signal is almost certainly a `.gsd/` directory (verify against real repos, see below).

### The load-bearing gap this spike must surface, not solve — and it is shared with 11.1

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:47-51] — with a comment stating plainly that *"the adapter registry that selects among `IArtifactAdapter` implementations arrives with Stories 4.3+."* Those stories are exactly the ones relocated into Epics 11–15, so **the registry has no owner today.** `ARCHITECTURE-SPINE.md` explicitly leaves this open: *"Exact adapter loading mechanics... are implementation seeds"* [`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:101`].

**Story 11.1 raises this exact same gap** (its Task 5). Do not independently re-derive a competing registry design or propose a *second* ADR for it. Instead: confirm the gap still exists, and record that it is a **shared prerequisite** — whichever baseline-coverage story lands first (11.2 Spec Kit or 12.2 GSD) must close it ONCE for all frameworks, not per-framework. Recommend (not build) a minimal shape (e.g. an ordered list of `IArtifactAdapter`s, first `AppliesTo` match wins, `BmadArtifactAdapter` stays the fallback), and explicitly defer to 11.1's registry conclusion if 11.1 reaches `done` before this spike is reviewed. Per this project's ADR-creation-trigger discipline ([[adr-creation-trigger-gap-epic-10-retro]]), if a genuine architecture fork is found, an ADR is warranted — but it should be **one** registry ADR, not one-per-spike. Coordinate; do not duplicate.

### The "host-neutral" models are less framework-neutral than they sound

Read these before assuming any GSD artifact maps cleanly:

- **`EpicsModel`/`EpicInfo`/`StoryInfo`** [src/SpecScribe/EpicsModel.cs] bake in BMad vocabulary directly into what the contract calls "host-neutral": `EpicStatus { Drafted, Pending }`, `EpicSection { VerticalSlice, FurtherDevelopment }` (a BMad epics.md convention), and `StoryInfo.Id` is hard-typed as BMad's `"N.M"` two-level numbering with a resolved `ArtifactOutputPath`/`ArtifactSourcePath` pointing at a discrete per-story markdown file. **GSD's hierarchy is three levels — Milestone → Slice → Task — not BMad's two (Epic → Story-with-task-checkboxes).** The central modeling question this spike must resolve explicitly: does a GSD *Milestone* map to `EpicInfo`, a *Slice* to `StoryInfo`, and *Tasks* to `StoryInfo.TasksDone`/`TasksTotal`? Or does the three-level shape not fit the two-level model and need a documented compromise (e.g. flatten, or record the third level as framework-extra)? Note the on-disk grain that *supports* a Slice≈Story reading: each slice has its own `S##-PLAN.md` + `S##-SUMMARY.md`, mirroring BMad's per-story artifact. Decide; do not leave ambiguous.
- **`RequirementsModel`/`RequirementInfo`** [src/SpecScribe/RequirementsModel.cs:1-73] is BMad's `FR`/`NFR`/`UX-DR` numbered-requirements-with-a-Coverage-Map convention, parsed from epics.md's "Requirements Inventory" section. **Unlike Spec Kit (which had no requirements file), GSD ships `.gsd/REQUIREMENTS.md` — a "requirement contract."** Verify against real repos whether it carries FR-style numbering (→ possibly mappable/partially-mappable) or is unnumbered prose (→ a lesser substitute or non-goal). Do not default to `Requirements = null` the way 11.1 did for Spec Kit.
- **`SprintStatus`** [src/SpecScribe/SprintStatus.cs] is literally `sprint-status.yaml`-shaped (a `development_status` map + `action_items`). **GSD ships `.gsd/STATE.md` — a "quick-glance status from database."** This is a `Sprint` *candidate*, but STATE.md is a DB-derived free-form projection, not a `development_status` YAML map — expect **partially-mappable at best**, likely summarized-tier, not a clean `SprintStatus` fill. Verify shape; do not assume `Sprint = null`, and do not assume a clean map either.
- **`RetroModel`** [src/SpecScribe/RetroModel.cs] is BMad's `epic-N-retro-*.md` convention. GSD has **no dedicated retrospective note** found at create-study — but `.gsd/milestones/M###/slices/S##/S##-SUMMARY.md` (per-slice execution summary) is a possible loose analog. Verify: is a slice summary a retro (→ maybe `Retros`), part of the slice/story projection (→ `Epics` slice), or framework-extra? Likely `Retros = []`, but confirm rather than assume.

**Net implication to verify, not assume:** GSD coverage will likely center on the `Epics`/`StoryArtifactsById` slice (Milestone/Slice/Task), with `Requirements` and `Sprint` as *partially-mappable / summarized-tier* candidates (unlike Spec Kit's clean nulls) and `Retros` probably empty. Confirm or overturn with real repos of both GSD and GSD-Pi before writing the coverage map.

### The GSD family's real shape — freshly gathered 2026-07-20, treat as a starting hypothesis to confirm against real repos

Live-checked at create-story via the GSD docs (`docs.opengsd.net`) and the `gsd-build/gsd-2` getting-started doc. **These tools evolve and the GSD-Pi exact filenames were NOT fully obtainable at create-story (see the authoritative-source pointer below) — re-verify every row, do not trust this table blindly:**

| GSD concept | Path / shape (from GSD `gsd-2`) | Closest `ArtifactBundle` candidate | Notes / tier hypothesis |
|---|---|---|---|
| Install / state marker | `.gsd/` at repo root | `AppliesTo` self-selection signal (mirrors `_bmad/`, `.specify/`) | Confirm identical for GSD-Pi |
| Authoritative DB | `.gsd/gsd.db` (SQLite, **gitignored**) | **None — SpecScribe reads markdown, never a DB** | Drives the "are md projections reliable?" finding (difference #3) |
| Project overview | `.gsd/PROJECT.md` | `ModuleContext`-style doc, or planning-doc / framework-extra | rendered or summarized |
| Requirement contract | `.gsd/REQUIREMENTS.md` | `RequirementsModel` candidate — **verify FR-numbered vs prose** | partially-mappable likely |
| Decisions | `.gsd/DECISIONS.md` ("architectural decisions from memory") | ADR side-channel? new field? out of scope? — classify explicitly | see "ADRs are a side-channel" below |
| Knowledge | `.gsd/KNOWLEDGE.md` (Rules + Patterns/Lessons) | No BMad analog | framework-extra / non-goal candidate |
| Status | `.gsd/STATE.md` ("quick-glance status from database") | `SprintStatus` candidate — DB-derived, not a `development_status` map | partially-mappable / summarized |
| Milestone | `.gsd/milestones/M###/` + `M###-ROADMAP.md` (slice plan w/ deps) | `EpicInfo` candidate (top of the 3-level hierarchy) | central modeling decision |
| Slice | `.gsd/milestones/M###/slices/S##/` + `S##-PLAN.md` | `StoryInfo` candidate (per-slice artifact ≈ per-story md) | central modeling decision |
| Task | decomposed inside `S##-PLAN.md` | `StoryInfo.TasksDone`/`TasksTotal` candidate | verify grain |
| Slice summary | `.gsd/milestones/M###/slices/S##/S##-SUMMARY.md` | `Retros` analog? or part of slice projection? | verify |
| Hierarchy semantics | Milestone = shippable version (4–10 slices); Slice = demoable vertical capability (1–7 tasks); Task = context-window-sized unit | — | maps onto the Milestone→Slice→Task decision |
| Sprint/kanban YAML | **None found** (STATE.md is the closest) | `Sprint` partially via STATE.md, not a clean fill | — |
| Numbered FR/NFR | **Unconfirmed** — REQUIREMENTS.md exists; numbering unverified | `Requirements` partial or null — spike decides | — |

**Authoritative sources the dev MUST fetch (create-study could not fully resolve GSD-Pi's exact filenames):**
- GSD-Pi project structure (the missing piece): `https://docs.opengsd.net/pi/concepts/project-structure.md` — confirms GSD-Pi's exact `.gsd/` filenames and whether they match GSD `gsd-2`.
- GSD-Pi repo: `github.com/open-gsd/gsd-pi` (GSD "now continues as GSD Pi").
- GSD (predecessor) repo + docs: `github.com/gsd-build/gsd-2`, `docs.opengsd.net`, marketing `lets-gsd.com`.
- Best-effort: obtain a real `.gsd/`-initialized sample repo (or run the GSD/GSD-Pi init) to confirm committed-vs-gitignored file reality — the AC's "representative current-version repositories" language exists precisely because DB-vs-markdown reliability (difference #3) cannot be settled from docs alone.

### Existing GSD/GSD-Pi surfaces already in the portal (placeholders — 12.2's eventual targets, not this spike's job to fill)

- **`AboutSddTemplater.cs`** carries a `Frameworks` roster including `gsd` [`Supported: false`] and `gsd-pi` [`Supported: false`] [AboutSddTemplater.cs:15-16], rendering a **support matrix across six nouns: Epics & Stories / Requirements / Sprint / Retros / Planning docs / Commands** [AboutSddTemplater.cs:96-121], with a generic "Coming soon" body for unsupported frameworks [AboutSddTemplater.cs:224-231]. **This six-noun matrix is a ready-made, already-shipped vocabulary** — align the coverage map's artifact-type naming to it where it fits, rather than inventing a parallel taxonomy.
- **`SiteNav.cs:69-70`** already defines `AboutSddGsdOutputPath = "about-sdd-gsd.html"` and `AboutSddGsdPiOutputPath = "about-sdd-gsd-pi.html"` — both page routes exist; content is placeholder.
- **`README.md`**'s "Supported frameworks" table already lists `GSD | — | 🧭 Planned` and `GSD-Pi | — | 🧭 Planned` (note: **no canonical URL is recorded** for either — the dev must establish the source repos, they are not linked anywhere in the repo yet).
- **`ArtifactCoverage.cs:79-81`** has its own explicit comment about the dashboard-level "coverage" concept (presence/freshness of a repo's OWN planning docs), distinct from this spike's artifact-classification coverage map. Don't conflate the two "coverage" senses when writing findings — name which one you mean each time.

**ADRs are a side-channel, not part of `ArtifactBundle` at all.** There is no dedicated ADR parser class and no `ArtifactBundle` field for them — `docs/adrs/*.md` are hand-authored and read via a separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside the `IArtifactAdapter` contract. **This matters directly for GSD's `.gsd/DECISIONS.md`** (an "architectural decisions from memory" document, DB-derived): classify it explicitly as (a) belonging in the ADR side-channel, (b) a new `ArtifactBundle` field/model, or (c) out of scope for now — don't leave it unclassified. (This mirrors the exact question 11.1 raised for Spec Kit's `constitution.md`; reuse that framing.)

**Coverage-map precedent.** Story 11.1's Completion Notes will be the first coverage map actually written; 12.1's should be the second and adopt the same shape so Epics 13–15 (and Epic 18's Story 18.1, near-identical AC language, ~epics.md line 2897, still `backlog`) can reuse it. **If 11.1 is `done` before this spike is reviewed, read its Completion Notes and match its structure** (especially its registry-gap wording and its constitution.md classification, which GSD's DECISIONS.md parallels).

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building `GsdArtifactAdapter`** or any parser — that's 12.2.
- **Reading the SQLite `gsd.db`** — SpecScribe reads markdown, never a database; do not propose a DB reader. If the md projections are unreliable, that's a diagnostic/tier finding, not a reason to add a SQLite dependency.
- **Designing the adapter registry** — name the shared gap (Task 6), coordinate with 11.1, don't design or implement it here.
- **Extending `ArtifactBundle`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Proposing a second registry ADR** — if an ADR is warranted, it is ONE shared registry ADR coordinated with 11.1, not per-framework.
- **A new authoring schema** for GSD content — SpecScribe reads GSD's existing conventions as-is.

## Tasks / Subtasks

- [x] **Task 1 — Confirm the contract shapes against live code (AC: #1)**
  - [x] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables; they are a starting point, not a substitute for reading the code.
  - [x] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:47-51`), and check whether Story 11.1 has already landed/recommended a registry shape (read its Completion Notes if `done`).
  - [x] Re-read the `gds` vs `gsd`/`gsd-pi` distinction in `AboutSddTemplater.cs` so you never conflate BMad GDS (supported) with GSD (this spike).

- [x] **Task 2 — Obtain and inspect representative current-version GSD AND GSD-Pi repositories (AC: #1, #2)**
  - [x] Fetch `https://docs.opengsd.net/pi/concepts/project-structure.md` (the GSD-Pi filenames create-study could not resolve) and confirm GSD-Pi's exact `.gsd/` layout.
  - [x] Inspect the GSD (`gsd-build/gsd-2`) and GSD-Pi (`open-gsd/gsd-pi`) repos/templates (or a real `.gsd/`-initialized sample) to confirm exact file names, folder depth (`.gsd/milestones/M###/slices/S##/`), the `.gsd/` marker, and the gitignore reality of `gsd.db` — the table above is a hypothesis from doc-page fetches, not a repo inspection.
  - [x] Confirm the exact numbering/naming convention for `M###` milestones and `S##` slices (zero-padding? sequential per-repo?).
  - [x] Determine whether the `.gsd/*.md` projections are reliably committed in a real repo, or can be stale/absent when only the gitignored `gsd.db` is current (difference #3) — this decides tiers and diagnostics.

- [x] **Task 3 — Resolve the GSD vs GSD-Pi relationship (AC: #1)**
  - [x] State explicitly whether GSD and GSD-Pi share one adapter surface (same `AppliesTo` sniff + ingest, per-variant tolerance) or need two, with the on-disk evidence and the consequence for Story 12.2's scope.
  - [x] Note any GSD-Pi-only or GSD-only artifacts/conventions that would force divergence.

- [x] **Task 4 — Classify every discovered artifact type with a declared coverage tier (AC: #1)**
  - [x] Fix a small coverage-tier vocabulary aligned to FR-4 (**rendered / summarized / unsupported**) [requirements-catalog.md:18] and define what each tier means for SpecScribe output.
  - [x] For each GSD/GSD-Pi artifact type (`.gsd/` marker, `gsd.db`, PROJECT.md, REQUIREMENTS.md, DECISIONS.md, KNOWLEDGE.md, STATE.md, milestones/roadmaps, slices/plans, slice summaries, tasks), classify as **mappable** (name the exact target: `ArtifactBundle` field + model type/record) / **partially-mappable** (name what maps and what doesn't) / **unsupported** (name why), AND assign a **coverage tier**.
  - [x] Resolve explicitly whether a GSD *Milestone* → `EpicInfo`, *Slice* → `StoryInfo`, *Task* → `StoryInfo.TasksDone/TasksTotal`, or the three-level shape needs a documented compromise — the central modeling question (see "host-neutral models" caveat).
  - [x] Classify `.gsd/DECISIONS.md` explicitly: ADR side-channel, new `ArtifactBundle` field, or out of scope — mirror 11.1's `constitution.md` classification.
  - [x] State explicitly, confirmed against real repos: is `Requirements` mappable-via-REQUIREMENTS.md or null, is `Sprint` partially-mappable-via-STATE.md or null, is `Retros` empty or fed by slice summaries — do NOT copy Spec Kit's null table.

- [x] **Task 5 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [x] For any GSD convention richer than the shared model (e.g. `gsd.db` DB-authority, KNOWLEDGE.md Rules/Patterns/Lessons, the milestone/slice/task three-level grain, DB-derived projection staleness), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale).
  - [x] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188, 219-224, 262-276] for tone/specificity. In particular, draft the notice for "`.gsd/` present but markdown projections absent/stale (DB is authoritative)".

- [x] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1 (AC: #1, #2)**
  - [x] Confirm the registry-gap claim against `SiteGenerator.cs`. State plainly that Story 12.2 cannot wire in a second adapter without SOME selection mechanism, that this gap is **shared with 11.1** (not GSD-specific), and that whichever coverage story lands first closes it once for all frameworks.
  - [x] Recommend (not build) a minimal registry shape, and defer to 11.1's conclusion/ADR if it exists. Do NOT propose a second, GSD-specific registry ADR.

- [x] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [x] Write the coverage map (artifact-type × classification × target projection × coverage tier table + GSD/GSD-Pi relationship decision + non-goals + shared registry-gap finding + 12.2 recommendation) into this story's **Completion Notes**, mirroring Story 11.1's / 19.1's convention.
  - [x] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND 11.1 has not already covered it — coordinate to keep it a single shared registry ADR.

### Owner-directed scope addition (2026-08-02)

The spike's findings were reviewed by the owner mid-story, who **confirmed the roster pinning and directed that
this story land it, together with the plan updates**. That deliberately supersedes Task 7's "no production
code" / "no `src/**`/`tests/**` changes" constraint and the seed non-goal list — recorded here rather than
absorbed silently, since a spike shipping production code is exactly the drift the story warned about. Scope is
bounded to the roster/documentation pinning and the plan split; **no adapter, parser, or registry work** is in
this story, and Stories 12.2/12.3 still own all of that.

- [x] **Task 8 — Pin the framework roster to the correct products (owner-confirmed)**
  - [x] Extend `AboutSddTemplater.Frameworks` with a canonical `Url` and an identity `Blurb`, pinning
    `gsd` → GSD Core (`docs.opengsd.net/core`) and `gsd-pi` → GSD Pi (`docs.opengsd.net/pi`).
  - [x] Render both on the placeholder framework page so "Coming soon" states what the framework *is*, not only
    that it is absent (NFR8's honest-absence posture).
  - [x] Cover with tests that fail if the two products are ever conflated again, or if either URL is pointed back
    at the retired `gsd-build/gsd-2`.
  - [x] Leave the display `Label`s (`GSD`/`GSD-Pi`) unchanged — see the note below.

- [x] **Task 9 — Split the baseline-coverage story per framework (owner-directed)**
  - [x] Replace Story 12.2 ("GSD and GSD-Pi Baseline Adapter Coverage") with **12.2 GSD Core** + **12.3 GSD Pi**
    in `epics.md`, each with framework-specific ACs, and record why in an inline note.
  - [x] Land the matching `development_status` keys in `sprint-status.yaml` **in the same change**
    (CLAUDE.md § Decision records: a structural scope change recorded in only one artifact is a drift bug).
  - [x] Update Epic 12's intro to state the two-adapter-surfaces conclusion.

- [x] **Task 10 — Update the user-facing documentation**
  - [x] Link both GSD rows in `README.md`'s "Supported frameworks" table to their canonical docs.
  - [x] Add a short disambiguation note stating the two are distinct products, with their differing markers,
    authority models, and hierarchies, and that the retired `gsd-build/gsd-2` continues as GSD Pi.

**Deliberate non-change — the display labels.** `Label` stays `GSD`/`GSD-Pi` rather than becoming
`GSD Core`/`GSD Pi`. The labels are load-bearing for the nav pills, page titles and breadcrumbs, and
`SiteGeneratorHowToReadTests.cs:254-255` asserts the pill text — a file a concurrent session is actively editing,
so renaming would have put this story inside another story's hunks for a cosmetic gain. The disambiguation the
roster actually lacked is carried by `Url` + `Blurb`. **Renaming the labels is a separate display decision and is
left to the owner.**

### Review Findings

- [x] [Review][Patch] Owner resolved 2026-08-02: keep the widened `Url`/`Blurb` scope (`bmad`, `speckit`), but wire it up properly — `Url`/`Blurb` were populated for `bmad` and `speckit`, not just `gsd`/`gsd-pi` as Task 8 authorized [src/SpecScribe/AboutSddTemplater.cs:30-31, 33-37], and neither the File List nor Change Log disclosed it. `AppendBmadBody` never read `fw.Url` — it hardcoded its own identical literal [AboutSddTemplater.cs:208] — so the field was dead, and `AboutSdd_EveryFrameworkWithACanonicalUrl_RendersItAsALink` only passed for `bmad` by coincidence of two independently-maintained strings agreeing. `AppendGdsBody` also hardcodes a URL but `gds`'s roster `Url` was left `null`, an inconsistent application. **Fix:** make `AppendBmadBody` and `AppendGdsBody` read `fw.Url` instead of a hardcoded literal (add `gds`'s canonical URL to the roster too), and record the `bmad`/`speckit`/`gds` widening explicitly in this story's File List/Change Log.

- [x] [Review][Patch] Story 12.3 (GSD Pi) AC drops the "reuse CoverageTier, not a parallel scale" clause that 12.2 states [_bmad-output/planning-artifacts/epics.md:2647 vs 2679] — Story 12.2 AC #2 says "...reusing the existing CoverageTier vocabulary rather than a parallel scale," matching this story's own Completion Notes Recommendation #4 ("Reuse `CoverageTier`/`CoverageTiers`... Do not mint a tier scale") which applies to both GSD Core and GSD Pi, not just Core. Story 12.3's equivalent AC #3 omits the clause. Mirror 12.2's wording into 12.3 AC #3.

- [x] [Review][Patch] Story 12.3 (GSD Pi) has no AC pinning the Milestone/Slice → `EpicInfo`/`StoryInfo` id-synthesis choice in a test [_bmad-output/planning-artifacts/epics.md:2656-2680] — 12.2 AC #3 requires "the chosen level mapping and the synthesized story-id form are pinned by a test." This story's own Completion Notes Recommendation #6 says the `StoryInfo.Id` synthesis choice (GSD Pi's `"{milestone}.{slice}"` vs GSD Core's `"{phase}.{wave}"`) must be pinned "before writing discovery globs" for the family generally — i.e. Pi needs this too. Add an equivalent AC to 12.3.

- [x] [Review][Patch] Dangling `<see cref="RenderHub"/>` in `BuildHubPage`'s doc comment after `RenderHub`/`RenderFrameworkPage` were deleted in this same diff [src/SpecScribe/AboutSddTemplater.cs:57] — this diff removes the two public wrapper methods (confirmed no other callers exist in the codebase) but leaves the doc comment sentence "`<see cref="RenderHub"/>` is the unchanged HTML projection of this same model, so the bytes are identical" referencing a method that no longer exists. Update or remove the sentence. Also undisclosed in the File List (a hunk unrelated to the roster/`Url`/`Blurb` work, per CLAUDE.md's hunk-attribution guidance).

- [x] [Review][Patch] Stale downstream story key reference in this story's own "Project Structure Notes" [12-1-gsd-and-gsd-pi-integration-spike.md:245] — still reads "Downstream story key (not created by this spike): `12-2-gsd-and-gsd-pi-baseline-adapter-coverage`," the pre-split singular key. Task 9 replaced it everywhere else (`epics.md`, `sprint-status.yaml`) with `12-2-gsd-core-baseline-adapter-coverage` + `12-3-gsd-pi-baseline-adapter-coverage`, but this line inside the same file was not updated. A reader following this note looks up a key that no longer exists.

- [x] [Review][Defer] Story 12.1's own epics.md Acceptance Criteria were not revisited to reflect the owner-directed scope addition [_bmad-output/planning-artifacts/epics.md:2608-2620] — deferred, pre-existing framing issue, not a functional bug. Tasks 8-10 land production code (roster `Url`/`Blurb`, `epics.md`/`sprint-status.yaml` split, README updates), but the story's charter ACs in `epics.md` still read as pure-spike language ("a written coverage map classifies..."). The story file itself discloses the deviation clearly in its "Owner-directed scope addition" note and Change Log, so this is only misleading to a reader who reads `epics.md`'s AC block in isolation. Not blocking; noted for awareness in future spike-with-scope-addition stories.

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live repo inspection, not code.** Evidence comes from reading `src/SpecScribe/*.cs` and reading/fetching real GSD/GSD-Pi repos — not from writing a prototype adapter. If you catch yourself scaffolding `GsdArtifactAdapter.cs`, stop; that's 12.2.
- **Markdown only, never the DB.** GSD's authoritative store is SQLite; do not propose reading it. The spike's job is to decide whether the markdown *projections* are a reliable enough source and at what tier.
- **No new authoring schema.** SpecScribe never asks GSD users to add SpecScribe-specific files — coverage must derive from GSD's own existing `.gsd/` conventions.
- **Verify, don't assume — and verify BOTH frameworks.** The tables here were built from doc-page fetches (and GSD-Pi's exact filenames were unresolved at create-study). Confirm every row against real GSD *and* GSD-Pi repos; the two-framework relationship (Task 3) is a required finding, not a footnote.
- **NFR8.** Genuinely-absent artifact families are honest absence, not gaps to fill. Don't recommend inventing conventions GSD lacks. But do NOT over-apply this: GSD *has* REQUIREMENTS.md and STATE.md, so "absent" is not the default answer the way it was for Spec Kit — classify by evidence.

### Coverage-tier discipline (AC #1's distinctive requirement)

- FR-4 names the tier ladder: **rendered / summarized / unsupported** [requirements-catalog.md:18] — *"additional artifacts are tiered as rendered, summarized, or unsupported."* Adopt these three words; do not invent a parallel tier scale.
- Keep tiers (how richly rendered) distinct from classification (mappable/partial/unsupported) and from diagnostic categories (the five `AdapterDiagnosticCategory` values). A type can be *mappable* + tier *summarized*; an *unsupported* type emits an `Unsupported`/`Informational` diagnostic. State all three axes per artifact where they apply.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future GSD adapter only translates into `ArtifactBundle`, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps GSD source shapes onto that exact contract, nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future GSD-specific insight enrichment must stay additive/non-blocking, same as BMad's git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."*
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — do not let this spike commit to `src/SpecScribe.Adapters.Gsd` as a real package (the project is still single-namespace, single-project — [[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- **Conflating GSD (Get Shit Done) with BMad GDS (Game Dev Studio)** — the near-anagram is a real trap; GDS is already supported via `BmadArtifactAdapter`, GSD/GSD-Pi are unrelated.
- Copying Spec Kit's "Sprint/Requirements/Retros stay null" table onto GSD — GSD has STATE.md and REQUIREMENTS.md; re-derive per family.
- Proposing a SQLite `gsd.db` reader — SpecScribe reads markdown; DB-authority is a tier/diagnostic finding.
- Assuming GSD ↔ GSD-Pi are identical (or different) without on-disk evidence — Task 3 must decide it.
- Assuming a Milestone/Slice/Task level maps 1:1 to Epic/Story without stating the decision and its consequences for the two-level `EpicsModel`.
- Proposing a second, GSD-specific adapter-registry ADR instead of coordinating one shared registry decision with 11.1.
- Omitting coverage tiers (AC #1 requires them here, unlike 11.1).
- Silently committing to a package/namespace split (`SpecScribe.Adapters.Gsd`) — aspirational sketch, not current architecture.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/12-1-gsd-and-gsd-pi-integration-spike.md`
- Sprint key: `12-1-gsd-and-gsd-pi-integration-spike`
- Downstream story keys (not created by this spike): `12-2-gsd-core-baseline-adapter-coverage`, `12-3-gsd-pi-baseline-adapter-coverage`
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 6 concludes a genuine architecture fork (registry design) not already covered by 11.1 — if so, it is ONE shared registry ADR (`docs/adrs/`, next number, indexed in `docs/adrs/README.md`), coordinated with 11.1, escalated rather than decided silently.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:2375-2419`] — Epic 12 intro, FR4 line, Story 12.1 (verbatim ACs quoted above) + Story 12.2 (downstream coverage story, its ACs quoted for scope-boundary context).
- [Source: `_bmad-output/planning-artifacts/epics.md:45,166`] — FR4 statement (GSD/GSD-Pi baseline) and Epic 12 FR-coverage line.
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/specs/spec-specscribe/requirements-catalog.md:18,50`] — FR-4 tiered-artifact language (rendered/summarized/unsupported) + SM-3 framework-breadth success metric.
- [Source: `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`] — why Epics 11-15 exist, the fixed framework→epic mapping (12 = GSD/GSD-Pi / FR4), the "X.1 spike, X.2 coverage" pattern, the spike AC template.
- [Source: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`] — the immediately-preceding sibling spike; mirror its structure, its Completion-Notes-as-deliverable convention, its constitution.md classification (parallels DECISIONS.md), and its shared registry-gap finding.
- [Source: `src/SpecScribe/IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`] — the contract + its one reference implementation (line anchors verified against current main at create-study).
- [Source: `src/SpecScribe/EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`] — the "host-neutral" model shapes, BMad-specific vocabulary baked in.
- [Source: `src/SpecScribe/SiteGenerator.cs:47-51`] — the hardcoded single-adapter field; the shared registry gap.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18,84,96-121,191-231`] — the framework roster (`gsd`/`gsd-pi` `Supported:false`; `gds`=BMad GDS supported — the name-collision trap), the six-noun support matrix, and the "Coming soon" placeholder body.
- [Source: `src/SpecScribe/SiteNav.cs:69-70`] — the already-routed `about-sdd-gsd.html` / `about-sdd-gsd-pi.html` pages (placeholder content); `README.md` "Supported frameworks" table — GSD & GSD-Pi listed "🧭 Planned" with no canonical URL recorded.
- [Source: `src/SpecScribe/ArtifactCoverage.cs:79-81`] — the dashboard-level "coverage" concept (repo's own doc freshness), a different sense from this spike's artifact-classification coverage map; don't conflate.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2/AD-4, Seed-not-invariant] — the invariants this spike must respect and the open seeds it must not over-commit.
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md`] — the older pure-tracing spike; Completion-Notes-as-deliverable convention.
- [Web: `github.com/gsd-build/gsd-2` getting-started + `docs.opengsd.net`, fetched live 2026-07-20] — GSD's `.gsd/` layout (`gsd.db` authoritative SQLite gitignored; PROJECT/REQUIREMENTS/DECISIONS/KNOWLEDGE/STATE.md; `milestones/M###/slices/S##/` with ROADMAP/PLAN/SUMMARY), Milestone→Slice→Task hierarchy. Treat as a hypothesis to reconfirm.
- [Web: `github.com/open-gsd/gsd-pi` + `docs.opengsd.net/pi/concepts/project-structure.md`, referenced 2026-07-20] — GSD-Pi is GSD's successor sharing `.gsd/` + SQLite-authoritative + Milestone→Slice→Task; **exact GSD-Pi filenames unresolved at create-study — the dev must fetch the project-structure doc.**
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — but ONE shared registry ADR, coordinated with 11.1, not per-spike).

### Git intelligence summary

No GSD/GSD-Pi code, adapter, or prior exploration exists anywhere in this repo beyond the placeholder About-SDD pages and roster entries (confirmed via grep across `src/`, `docs/`, `epics.md`) — this spike starts from a clean slate on the GSD side, with a well-established adapter contract and one working reference implementation (`BmadArtifactAdapter`) on the SpecScribe side. Story 11.1 (Spec Kit) is the immediately-preceding sibling spike (`ready-for-dev`) and the closest structural template. Recent commits (Epic 10 cleanup, retro work, 11.1 create-story) are unrelated to this story's implementation scope.

## Dev Agent Record

### Agent Model Used

claude-opus-5[1m] (Opus 5, 1M context) — dev-story, 2026-08-02.

### Debug Log References

The spike proper (Tasks 1–7) ran no builds and produced no code: evidence is (a) direct reads of the contract
sources under `src/SpecScribe/`, and (b) live fetches of the Open GSD documentation on 2026-08-02 (URLs cited
inline below). The `src/`/`tests/` changes in the File List come from the **owner-directed scope addition**
(Tasks 8–10) and were developed red-green: `AboutSddFrameworkRosterTests.cs` was written first and failed to
compile against the roster (`'…' does not contain a definition for 'Url'`), then passed 4/4 once
`AboutSddTemplater.Frameworks` gained `Url`/`Blurb`.

**Regression suite — run, and the 19 failures proven NOT to be this story's (2026-08-02).**
`dotnet test tests/SpecScribe.Tests` in the shared working tree reported **19 failed / 2925 passed / 2944 total**.
This story changed only two markdown/YAML files, so per CLAUDE.md § Concurrent work ("establish causality first…
bisect into a throwaway tree, never by resetting the shared tree") I bisected rather than assuming:

1. `git archive HEAD` into the scratchpad (`scratchpad/head-tree`), ran the suite there → **2944 passed, 0 failed.**
   HEAD is green.
2. Overlaid **only this story's two files** onto that pristine tree and re-ran → **2944 passed, 0 failed.**

So the 19 failures were entirely attributable to another session's uncommitted working-tree changes, not to this
story. Corroborating detail: the sampled failure
(`SiteGeneratorHowToReadTests.HowToRead_SubtitleAndIntro_MentionGeneratingNotJustReading`, line 520) asserts on
page copy — "how to generate and refresh the site from…" — that is absent from the rendered nav, and
`tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` is one of ~35 test files showing `M` in `git status` at
session start. That is a test edited ahead of its `src/` counterpart in a sibling session. **Not touched, not
"fixed," and no baseline regenerated** — it is not this story's to resolve, and reflexive regeneration is exactly
what CLAUDE.md forbids.

**Final regression state after the owner-directed scope addition: 2948 passed / 0 failed** (the original 2944 plus
this story's 4 new tests). The sibling session landed its `src/` counterparts during this story, and the 19
failures resolved on their own — confirming the attribution above without this story touching any of them. Two
intermediate runs showed a single differing failure
(`FileWatcherServiceTests.WatchedSourceFileStaysWritableAndDeletableDuringRegeneration`,
`SiteGeneratorEpicsRemovalTests.RegenerateEpics_WhenEpicsFileDeleted_RemovesTheWholeEpicsOutputFamily`); both pass
3/3 in isolation and are filesystem-timing flakes under parallel load, in areas this story does not touch.

**Sources fetched 2026-08-02** — all reachable, all quoted from rather than paraphrased where load-bearing:

- `https://docs.opengsd.net/llms.txt` — the documentation index. **This is the file that broke the story's premise:**
  it enumerates *three* separate product lines — `core/`, `pi/`, and `browser/`.
- `https://docs.opengsd.net/core/introduction.md`, `core/installation.md`, `core/guides/new-project.md`,
  `core/concepts/planning-artifacts.md`, `core/concepts/workflow.md`, `core/guides/phase-lifecycle.md`,
  `core/commands/workflow-commands.md` — GSD Core.
- `https://docs.opengsd.net/pi/concepts/project-structure.md`, `pi/configuration/git-automation.md` — GSD Pi.
- `https://getshitdone.help/` and `https://getshitdone.help/gsd-directory/` — GSD 2 (`gsd-build/gsd-2`).
- `https://github.com/gsd-build/gsd-2/blob/main/docs/dev/architecture.md`, `deepwiki.com/gsd-build/gsd-2`,
  plus web search establishing that `gsd-build/gsd-2` "is no longer the active home… now continues as GSD Pi".

**Residual uncertainty, stated rather than papered over.** I could not obtain a real `.gsd/`- or
`.planning/`-initialized sample repository (both products generate their state on first run against a live model
provider; no committed fixture is published in either repo). Every layout claim below therefore rests on
*current vendor documentation*, not on a directory listing. The doc pages were internally consistent and
mutually corroborating, and the two structure pages (`pi/concepts/project-structure.md`,
`getshitdone.help/gsd-directory/`) both present explicit full-tree listings — but Story 12.2 must re-confirm
exact filenames against a generated repo before writing discovery globs. Specifically unresolved: GSD Core's
requirement-id format is documented as "stable IDs like `REQ-001`" on the planning-artifacts page but no example
file is published; and neither product documents its `STATE.md` grammar.

### Completion Notes List

## Coverage map — GSD family × shared adapter contract (Story 12.1 deliverable)

### ⚠️ Headline finding: the story's central premise is wrong, and it changes the answer

Story 12.1's create-story context states, as verified fact, that GSD and GSD-Pi "share the same `.gsd/` marker,
the same authoritative-SQLite-plus-markdown-projection model, and the same **Milestone → Slice → Task**
hierarchy," and frames the spike's central question as "do they collapse to ONE adapter surface … or genuinely
diverge?"

**That premise held for the product pair create-story actually looked at (GSD 2 → GSD Pi), but not for the
current-version pair the AC asks about.** The Open GSD lineup today is *three* products, not two versions of one:

| Product | Package | Marker dir | Authority | Hierarchy | Kind of thing |
|---|---|---|---|---|---|
| **GSD Core** | `@opengsd/gsd-core` | **`.planning/`** | **Plain Markdown + JSON. No database.** | Milestone → **Phase** → Task | Meta-prompting framework layered on your AI coding runtime (slash commands) — same *kind* of thing as BMad and Spec Kit |
| **GSD Pi** | `@opengsd/gsd-pi` | `.gsd/` | **SQLite `gsd.db` authoritative**; `.md` are projections | Milestone → **Slice** → Task | Standalone autonomous agent CLI |
| GSD Browser | — | — | — | — | Browser-automation MCP server. **Not a planning framework; out of scope entirely.** |

And `gsd-build/gsd-2` (the repo create-story surveyed, documented at `getshitdone.help`) is the **retired
predecessor** — "no longer the active home for GSD 2 development. The project now continues as GSD Pi." Its npm
package is literally named `gsd-pi`.

The AC says "representative **current-version** GSD and GSD-Pi repositories." Applied honestly, that means:

- **`gsd`** (SpecScribe's roster id, `Supported: false`) → **GSD Core**, `.planning/`, markdown-native.
- **`gsd-pi`** → **GSD Pi**, `.gsd/`, SQLite-authoritative.
- **GSD 2 is a third, retired layout** that neither roster entry should be pinned to.

This is not a quibble. It inverts three of the story's four stated "structural differences," and it makes the
family *more* tractable than create-story feared, not less — the product SpecScribe would call "GSD" turns out
to be the markdown-native one.

> **✅ CONFIRMED BY THE OWNER AND LANDED IN THIS STORY (2026-08-02).** `README.md`'s "Supported frameworks" table
> recorded **no canonical URL** for either GSD entry, and `AboutSddTemplater.Frameworks` carried only ids and
> labels — the ambiguity that produced this story's wrong premise, and which would have recurred in every
> downstream coverage story and framework page. The owner confirmed the pinning and directed this story to land
> it: `gsd` → `docs.opengsd.net/core` (GSD Core), `gsd-pi` → `docs.opengsd.net/pi` (GSD Pi), in **both** the
> roster and the README, with the placeholder framework pages now stating what each framework is. See Tasks 8–10.

### Task 3 — the GSD ↔ GSD-Pi relationship: **two adapter surfaces, not one**

**Decision: they do NOT collapse.** The evidence is not a matter of tolerance-tuning; every load-bearing
discovery input differs.

| Dimension | GSD Core | GSD Pi | Collapsible? |
|---|---|---|---|
| `AppliesTo` marker | `.planning/` at repo root | `.gsd/` at repo root | **No** — disjoint sniffs |
| Source of truth | Markdown + `config.json` on disk | `gsd.db` (SQLite); md are rendered projections | **No** — different reliability story, so different tiers |
| Mid-level noun | **Phase** (`.planning/phases/NN-slug/`) | **Slice** (`S##-` prefixed, flat in the milestone dir) | **No** — different vocabulary *and* different path grammar |
| Per-unit artifact path | `.planning/phases/NN-slug/NN-YY-PLAN.md` | `.gsd/milestones/M###/S##-PLAN.md` | **No** — one nests per unit, one flattens |
| Requirement ids | Documented as stable `REQ-001`-style | "capability contract", numbering undocumented | Partially |
| Project overview | `PROJECT.md` **exists** | **No `PROJECT.md`**; `CODEBASE.md` + `PREFERENCES.md` instead | **No** |
| Top-level roadmap | `ROADMAP.md` at `.planning/` root | none at root; per-milestone `M###-ROADMAP.md` | **No** |
| Status projection | `STATE.md` | `STATE.md` | Yes — the one genuine overlap |

Only `STATE.md` (name and rough purpose) and the word "Milestone" survive as shared. **Two `AppliesTo` sniffs and
two discovery grammars are required.** Whether that is two `IArtifactAdapter` classes or one class with two
internal discovery strategies is a 12.2 implementation choice with no contract consequence — the registry selects
on `AppliesTo`, and a single class can only return one boolean, so **two classes is the cleaner default**
(`GsdCoreArtifactAdapter`, `GsdPiArtifactAdapter`).

**Consequence for Story 12.2's scope: it is roughly double what its title implies.** Epic 12's Story 12.2 ("GSD
and GSD-Pi Baseline Adapter Coverage") is currently one `backlog` story covering both. Two disjoint discovery
grammars, two marker sniffs, and two tier ladders is not one story's worth of work at this project's story size.
**✅ SPLIT CONFIRMED BY THE OWNER AND EXECUTED IN THIS STORY (2026-08-02):** Story 12.2 is now **12.2 (GSD Core)**
and **12.3 (GSD Pi)**, with **GSD Core first** — it is markdown-native, so it needs none of the DB-projection
reliability machinery and lands the higher-value coverage sooner. Per CLAUDE.md § Decision records the change
co-landed in `epics.md` **and** `sprint-status.yaml`; see Task 9.

### The three axes, kept separate (as AC #1 and Dev Notes require)

This spike states three orthogonal things per artifact. Conflating them is the named failure mode:

1. **Classification** — mappable / partially-mappable / unsupported. *Does it fit the shared model?*
2. **Coverage tier** — `Rendered` / `Summarized` / `Unsupported`. *How deeply is it interpreted?*
3. **Diagnostic category** — one of the five `AdapterDiagnosticCategory` values. *What non-fatal notice fires?*

**Correction to the story's Task 4 instruction — the tier vocabulary already exists in code; do not mint one.**
The story asks this spike to "fix a small coverage-tier vocabulary aligned to FR-4." It is already fixed, as a
real closed type, shipped by **Story 18.5**: `enum CoverageTier { Rendered, Summarized, Unsupported }` plus
`static class CoverageTiers` (`Word`, `Description`, `AccentToken`, `Order`) in
`src/SpecScribe/TestArtifactsModel.cs:18-76`. Its doc comment says explicitly that it is "the PRD's
`rendered`/`summarized`/`unsupported` coverage-tier vocabulary, made a real type," answering a PRD open question.
It is already rendered as a legend on the About-SDD page (`AboutSddTemplater.cs:255-258`) and as per-artifact
badges on the Test Artifacts page. **Story 12.2 must reuse `CoverageTier`, not define a parallel scale** —
`CoverageTiers` holds the same one-classifier discipline `StatusStyles` holds for lifecycle, and the tier word
must never be spelled by a surface itself. Every tier below is one of those three enum values.

Note the tier semantics that Story 18.5 pinned, because they constrain the map below: `Rendered` means "a full
page exists and SpecScribe interprets nothing beyond rendering its prose" — it is *lower* interpretation depth
than `Summarized`, which means "a structured headline is additionally extracted." `CoverageTiers.Order` is
`Summarized, Rendered, Unsupported` (best-understood first). A file with no page and no extraction is
`Unsupported`, which is "an honest statement of the interpretation boundary," not a failure.

### Coverage map — GSD Core (`.planning/`) → `ArtifactBundle`

| GSD Core artifact | Path | Classification | Target projection | Tier | Diagnostic |
|---|---|---|---|---|---|
| Install marker | `.planning/` at repo root | mappable | `AppliesTo` self-selection signal (mirrors `_bmad/`) | n/a | none |
| Project overview | `.planning/PROJECT.md` | partially-mappable | No `ArtifactBundle` field for it. Renders via the generic `*.md` pass; a `ModuleDoc`-style "well-known planning doc" entry is the natural home, but `ModuleContext` is BMad-typed (see below) | `Rendered` | none |
| Requirements | `.planning/REQUIREMENTS.md` | **partially-mappable** | `Requirements` → `RequirementsModel` | `Summarized` | `Unsupported` when ids don't parse |
| Roadmap | `.planning/ROADMAP.md` | **mappable** | `Epics` → `EpicsModel` (**this is the epics source**) + `EpicsSourceFullPath` | `Summarized` | `Malformed` on parse failure |
| Live state | `.planning/STATE.md` | **partially-mappable** | `Sprint` → `SprintStatus` | `Summarized` | `Unsupported` when no per-phase status is recoverable |
| Phase dir | `.planning/phases/NN-slug/` | mappable | Story-artifact discovery root | n/a | none |
| Phase plan | `NN-YY-PLAN.md` | **mappable** | `StoryArtifactsById[id]` + `ConsumedSourceRelatives`; task checkboxes → `StoryInfo.TasksDone/TasksTotal` | `Rendered` | `Skipped` on id collision |
| Phase summary | `NN-YY-SUMMARY.md` | partially-mappable | Companion to the plan; **not** a `RetroModel` | `Rendered` | `Skipped` (loses the story-artifact slot to `-PLAN.md`) |
| Phase context | `NN-CONTEXT.md` | unsupported | generic `*.md` page only | `Rendered` | none |
| Discussion log | `NN-DISCUSSION-LOG.md` | unsupported | generic `*.md` page only | `Rendered` | none |
| Research | `NN-RESEARCH.md`, `.planning/research/` | unsupported | generic `*.md` page only | `Rendered` | none |
| Verification / UAT | `NN-VERIFICATION.md`, `NN-UAT.md` | **partially-mappable** | Closest analog to `Retros` (a per-phase closure verdict), but see the `Retros` ruling below | `Rendered` | none |
| Config | `.planning/config.json` | **unsupported** | none — the source scan is `*.md`, so a `.json` is structurally invisible (the same ADR 0020 constraint that made TEA's two JSON files `Summarized`-only) | `Unsupported` | `Informational` |
| Handoff | `.planning/continue-here.md` | unsupported | generic `*.md` page only | `Rendered` | none |
| Codebase map | `.planning/codebase/` | unsupported | generic `*.md` page only | `Rendered` | none |
| Debug / spikes / sketches / threads / seeds / todos | `.planning/{debug,spikes,sketches,threads,seeds,todos}/` | unsupported | generic `*.md` pages; `sketches/` is HTML → invisible to the `*.md` scan | `Rendered` / `Unsupported` (sketches) | `Informational` for `sketches/` |
| Slash commands | `/gsd-*` (10 commands, in the runtime's config dir, **not in the repo**) | **unsupported** | `ModuleContext.Commands` → `CommandCatalog`, but see the blocker below | `Unsupported` | `Informational` |

### Coverage map — GSD Pi (`.gsd/`) → `ArtifactBundle`

| GSD Pi artifact | Path | Classification | Target projection | Tier | Diagnostic |
|---|---|---|---|---|---|
| Install marker | `.gsd/` at repo root | mappable | `AppliesTo` self-selection signal | n/a | none |
| **Authoritative DB** | `.gsd/gsd.db` (SQLite, gitignored) | **unsupported — deliberate non-goal** | **none. SpecScribe reads markdown, never a database** | `Unsupported` | `Informational` (see draft below) |
| Live state | `.gsd/STATE.md` | **partially-mappable** | `Sprint` → `SprintStatus` | `Summarized` | `Unsupported` when no per-slice status is recoverable |
| Requirements | `.gsd/REQUIREMENTS.md` ("capability contract", **deep mode only**) | **partially-mappable** | `Requirements` → `RequirementsModel` | `Summarized` | `Unsupported` when unnumbered; **absent in non-deep-mode repos → `Requirements = null`, no diagnostic** |
| Decisions | `.gsd/DECISIONS.md` | **partially-mappable** | **ADR side-channel** — see the ruling below | `Rendered` | none |
| Knowledge | `.gsd/KNOWLEDGE.md` (Rules / Patterns / Lessons) | unsupported | generic `*.md` page only | `Rendered` | none |
| Codebase map | `.gsd/CODEBASE.md` | unsupported | generic `*.md` page only | `Rendered` | none |
| Preferences | `.gsd/PREFERENCES.md` (YAML-in-markdown) | **partially-mappable** | No field. But it carries `git.commit_docs`, which **explains** an absent-projections repo — read it only to choose the right diagnostic wording | `Rendered` | none |
| Milestone dir | `.gsd/milestones/M###/` | mappable | Epic-level grouping | n/a | none |
| Milestone roadmap | `M###-ROADMAP.md` | **mappable** | `Epics` → `EpicsModel`/`EpicInfo` (**per-milestone; there is no single root epics file** — see the modeling ruling) | `Summarized` | `Malformed` on parse failure |
| Milestone context | `M###-CONTEXT.md` | unsupported | generic `*.md` page only | `Rendered` | none |
| Milestone summary | `M###-SUMMARY.md` | **partially-mappable** | Closest analog to `Retros` — but see the `Retros` ruling | `Rendered` | none |
| Slice plan | `M###/S##-PLAN.md` | **mappable** | `StoryArtifactsById[id]` + `ConsumedSourceRelatives`; task defs → `StoryInfo.TasksDone/TasksTotal` | `Rendered` | `Skipped` on id collision |
| Slice assessment | `M###/S##-ASSESSMENT.md` (UAT verdict) | partially-mappable | Companion to the slice plan | `Rendered` | `Skipped` |
| Task summary | `M###/T##-SUMMARY.md` | unsupported | generic `*.md` page; the third level has no home in the two-level model | `Rendered` | none |
| Research | `.gsd/research/{STACK,FEATURES,ARCHITECTURE,PITFALLS}.md` | unsupported | generic `*.md` pages | `Rendered` | none |
| Reports | `.gsd/reports/M###-report.html` | **unsupported** | none — `.html`, invisible to the `*.md` scan | `Unsupported` | `Informational` |
| Runtime state | `.gsd/{exec,runtime,journal}/`, `.gsd-worktrees/`, `.gsd-backups/` | **unsupported — deliberate non-goal** | none; gitignored runtime | `Unsupported` | none (ignored, **not** diagnosed — mirrors `PathUtil.IsIgnoredSourceFile`) |

### The four rulings the story demanded explicitly

**1. Milestone → Slice/Phase → Task against the two-level `EpicsModel`: flatten the top two, drop the third to a tally.**

`EpicsModel` is two-level (`EpicInfo` → `StoryInfo`) and `StoryInfo.Id` is hard-typed to BMad's `"N.M"`
two-level numbering (`EpicsModel.cs:9-10`, and `BmadArtifactAdapter.ArtifactFilenamePattern` at line 55 parses
exactly `^(?<epic>\d+)-(?<story>\d+)-`). The GSD family is three-level. **Ruling:**

- **Milestone → `EpicInfo`.** GSD Pi's `M###` is zero-padded-3 and sequential; GSD Core's milestones live in
  `ROADMAP.md` and its phases are `NN-slug` (zero-padded-2). Both parse to `EpicInfo.Number` cleanly.
- **Slice (Pi) / Phase (Core) → `StoryInfo`.** Both have exactly the per-unit artifact grain
  (`S##-PLAN.md` / `NN-YY-PLAN.md`) that mirrors BMad's per-story markdown file, which is what
  `StoryArtifactsById` and every story surface actually consume.
- **Task → `StoryInfo.TasksDone`/`TasksTotal` only.** Tasks are decomposed *inside* the plan file in both
  products, exactly like BMad's `## Tasks / Subtasks` checkboxes — so this is not a compromise at all for Core.
  It **is** a real loss for GSD Pi, which additionally writes a per-task `T##-SUMMARY.md` file; that file has no
  home in the two-level model and is classified `unsupported`/`Rendered` above.
- **`StoryInfo.Id` compromise:** synthesize `"{milestone}.{slice}"` (e.g. `M002`/`S03` → `"2.3"`). This fits the
  existing `"N.M"` contract and every downstream consumer, at the cost of not round-tripping the zero-padding.
  GSD Core's `NN-YY-PLAN.md` has a *second* numeric level within the phase (execution waves) — recommend
  `"{phase}.{wave}"`, which means a Core "story" is a *plan*, not a *phase*. **12.2 must pick one and pin it in
  a test**; both are defensible and they are not interchangeable.
- **The honest cost, stated for NFR8:** in both products the epic-level noun (Milestone) is *coarser* than a BMad
  epic and the story-level noun is *finer*. `EpicStatus { Drafted, Pending }` and
  `EpicSection { VerticalSlice, FurtherDevelopment }` (`EpicsModel.cs:3-5`) are BMad epics.md conventions with no
  GSD analog — a GSD adapter must pick constant values for both and they will be semantically empty. That is
  framework-extra loss in the *reverse* direction (shared model too specific, not too general) and is the single
  strongest argument for the projection extension proposed below.

**2. `.gsd/DECISIONS.md` → ADR side-channel. Same ruling 11.1 should reach for `constitution.md`.**

ADRs are not part of `ArtifactBundle` at all — there is no field and no parser class; `docs/adrs/*.md` are read
through the separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside `IArtifactAdapter`.
`DECISIONS.md` is a *register* (many decisions in one file), whereas the ADR side-channel expects
*one file per decision* — `ForgeOptions.AdrFallbackProbeSubdirs` (lines 110-118) probes directories and
`HasMarkdownWithinOneLevel` counts `*.md` files. So `DECISIONS.md` **cannot** simply be pointed at by
`--adrs`. **Ruling: ADR side-channel in spirit, not mechanically reachable today.** Classify as
partially-mappable / `Rendered` (it gets a page via the generic pass), and record "split a decision *register*
into ADR entries" as a **candidate projection extension** (below) rather than forcing it. **Explicitly not a new
`ArtifactBundle` field** — that would fork the ADR concept across two channels.

**3. `Requirements`: mappable-via-`REQUIREMENTS.md` for both — but conditionally, and NOT the way Spec Kit was.**

Do not carry Spec Kit's `Requirements = null` over; both GSD products ship a `REQUIREMENTS.md`.

- **GSD Core:** documented as "Numbered requirements (v1, v2, out-of-scope) with stable IDs like `REQ-001`."
  That is a genuinely parseable id grammar — but it is **`REQ-`, not `FR`/`NFR`/`UX-DR`**, and `RequirementInfo.Id`
  is a computed property over a closed `RequirementKind` enum that *throws* on an unknown kind
  (`RequirementsModel.cs:43-49`). A `REQ-001` cannot be represented without either mapping it onto
  `RequirementKind.Functional` (lossy, and the rendered id would read `FR1`, which is **wrong on screen**) or
  extending the enum. **12.2 must not silently map `REQ-001` → `FR1`.**
- **GSD Pi:** `REQUIREMENTS.md` is **deep-mode only** — a normal Pi repo may not have one at all. Absent → 
  `Requirements = null` with **no diagnostic** (honest absence, NFR8), not an `Unsupported` notice.
- **Both:** `RequirementStatus` is rolled up from an FR→Epic *Coverage Map* section that is a BMad epics.md
  convention. Neither GSD product publishes one, so every requirement would land on `Unmapped` — which renders as
  "no covering epic at all — no plan exists yet," i.e. **actively misleading**. This is the strongest single
  argument for tier `Summarized` rather than `Rendered`: extract the requirement list, do **not** claim coverage
  status. 12.2 should default GSD requirements to `Planned` or introduce an explicit "coverage map absent" state
  rather than let `Unmapped` imply an oversight that does not exist.

**4. `Retros`: EMPTY for both. `Retros = []`.**

The story asked whether slice/milestone summaries feed `Retros`. **They do not.** `RetroModel` requires
`EpicNumbers`, `Title`, `DateText`, `Participants`, and a body whose `## Action Items` table gets badged
(`RetroModel.cs:17-38`), and `RetroParser` keys on the `epic-N-retro-*.md` filename convention. A GSD
`M###-SUMMARY.md` / `NN-YY-SUMMARY.md` is an *execution* summary — what got built and what deviated — with no
participants, no action-items table, and no retrospective ritual behind it. Forcing it into `RetroModel` would
populate a "Retrospectives" surface with content that is not a retrospective, and — because
`EpicInfo.HasRetrospective` gates the sunburst/donut/chip "In review → finished" tier — would silently mark
milestones *closed out* on every visual surface on the strength of a build log. That is exactly the
misleadingly-empty failure NFR8 forbids. **`Retros = []` for both products, no diagnostic** (honest absence).
GSD Pi's `S##-ASSESSMENT.md` (a UAT verdict) is the nearest thing to a *quality gate*, and if anything it belongs
with the Story 18.5 test-artifacts/gate surface, not with retros — noted as a 12.3 follow-up, not proposed here.

### AC #2 — framework-extra data: candidate projection extensions vs explicit non-goals

**Candidate projection extensions (recorded, NOT landed by this spike):**

1. **A third hierarchy level on `EpicsModel`.** Both products are Milestone → Slice/Phase → Task, and both write a
   per-task or per-wave artifact that the two-level model discards. This is now the *second* framework to want it
   (Epic 19's work-graph spike wants a general graph). Worth one design pass across 12.2/13.x rather than five
   per-framework compromises.
2. **A non-BMad `RequirementKind` / free-form requirement id.** Needed for `REQ-001` and for any framework whose
   ids aren't `FR`/`NFR`/`UX-DR`. Today `RequirementInfo.Id` throws on an unmodeled kind.
3. **A "coverage map absent" `RequirementStatus`.** So a framework without an FR→Epic map doesn't render every
   requirement as `Unmapped` (= "no plan exists"), which is false.
4. **Decision-register support** — one markdown file holding many decisions, projected into the existing ADR
   surface. Serves GSD's `DECISIONS.md` and (per 11.1) Spec Kit's `constitution.md`.
5. **Framework-neutral `ModuleContext`.** See the blocker below — this one is not optional if the About-SDD
   matrix's "Planning docs" and "Commands" columns are ever to be ticked for a non-BMad framework.

**Explicit non-goals (with rationale):**

- **Reading `gsd.db`.** SpecScribe reads markdown, never a database. Restated and reaffirmed: the DB-authority
  fact is a *tier and diagnostic* finding, not a reason to take a SQLite dependency.
- **`.gsd/reports/*.html` and `.planning/sketches/*.html`.** The source scan is `*.md` by construction
  (ADR 0020). Rendering a foreign tool's self-contained HTML report inside the portal would also mean adopting its
  styling and its scripts.
- **`.planning/config.json` and `.gsd/PREFERENCES.md` as *configuration*.** SpecScribe may read `PREFERENCES.md`'s
  `git.commit_docs` to choose a diagnostic's wording, but must not honor either file as settings — SpecScribe's
  own options come from `ForgeOptions`.
- **`.gsd/{exec,runtime,journal}/`, `.gsd-worktrees/`, `.gsd-backups/`.** Gitignored runtime state. **Ignored, not
  diagnosed** — matching the `IArtifactAdapter` contract's rule that "ignored working files are neither ingested
  nor diagnosed."
- **A new authoring schema.** Unchanged: SpecScribe never asks a GSD user to add SpecScribe-specific files.
- **Any `src/`/`tests/` change in this story.** Confirmed: none made.

### AC #2 — drafted non-fatal notices (all five categories are the whole vocabulary; no sixth invented)

Drafted in the register of `BmadArtifactAdapter`'s existing messages (lines 166-188, 213-225, 296-299) — name the
artifact, name the consequence, no blame, no imperative.

```text
Informational  .gsd/gsd.db
  "GSD Pi's authoritative store is a SQLite database; SpecScribe reads only the markdown projections
   alongside it, so anything not yet rendered to markdown is not shown."

Informational  .gsd
  "'.gsd/' is present but holds no markdown projections — GSD Pi's 'git.commit_docs: false' keeps planning
   documents local-only. Only the database is current; no GSD surfaces are generated."

Unsupported    .gsd/STATE.md            (and .planning/STATE.md)
  "state projection has no recoverable per-slice status; sprint surfaces are omitted"

Unsupported    .planning/REQUIREMENTS.md
  "requirement ids are not in a recognized numbering scheme; requirements are listed without coverage status"

Informational  .planning/config.json     (and .gsd/reports/M001-report.html, .planning/sketches/*.html)
  "recognized GSD artifact in a non-markdown format; SpecScribe's source scan reads markdown only,
   so it is named but not rendered"

Malformed      .planning/ROADMAP.md      (and .gsd/milestones/M001/M001-ROADMAP.md)
  <parser exception message>            // verbatim BmadArtifactAdapter.cs:166-167 shape

Skipped        .gsd/milestones/M001/S01-ASSESSMENT.md
  "Slice 2.1 matched more than one artifact filename; 'S01-PLAN.md' was ingested as the story artifact."
```

Note the second notice deliberately fires on the **directory**, not on a file — there is no file to anchor it to.
`AdapterDiagnostic.RelativePath` is a plain string and `DiagnosticAnchorRoot` already exists for exactly this kind
of "subject lives beside the source tree" case (`AdapterDiagnostic.cs:43-48`), so this needs no contract change,
but 12.2 should confirm the diagnostics page renders a directory path acceptably.

### Task 6 — the shared adapter-registry gap, plus a SECOND prerequisite the story did not anticipate

**Gap 1 — the adapter registry. Confirmed, unchanged, and shared with 11.1.**
`SiteGenerator.cs:59-63` still holds one hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();`
— with the comment "the adapter registry that selects among IArtifactAdapter implementations arrives with
Stories 4.3+." Those stories are the ones relocated into Epics 11–15, so **the registry still has no owner.**
Story 11.1 is still `ready-for-dev` with empty Completion Notes, so there is nothing yet to defer to.

*Recommended minimal shape (recommended, not built):* an ordered `IReadOnlyList<IArtifactAdapter>`, first
`AppliesTo` match wins, `BmadArtifactAdapter` last as the fallback so a bare `_bmad-output` tree with no install
keeps rendering exactly as today. One wrinkle 12.2 must handle and 11.2 does not: `SiteGenerator` holds the
adapter as the **concrete** `BmadArtifactAdapter` type, not the interface, "because the watch paths also need its
scoped epics re-ingest" (`IngestEpics`/`EpicsIngest`). A registry returning `IArtifactAdapter` therefore breaks
watch-mode incremental regeneration unless the scoped re-ingest is either lifted onto the interface or the watch
path degrades to a full re-ingest for non-BMad adapters. **AD-5 says watch behavior must not regress**, so this is
a real design constraint, not a detail.

**No second ADR proposed.** Per the story's instruction and CLAUDE.md's "read `docs/adrs/` before declaring a
crossing," this is ONE shared registry decision. Whichever of 11.2 / 12.2 lands first closes it for all
frameworks; if it warrants an ADR, that is one registry ADR, coordinated — not one per spike.

**Gap 2 — source-root discovery is BMad-hardcoded too. NEW; not named by 11.1 or by this story's context.**
`ForgeOptions.SourceDirName` is the literal `"_bmad-output"` (`ForgeOptions.cs:87`), and `Resolve` walks *up from
the cwd looking for a directory containing `_bmad-output`* to find the repo root, throwing
`DirectoryNotFoundException` when there is none (lines 154-173). **A pure GSD repo has no `_bmad-output`, so
`specscribe generate` fails before any adapter is consulted.** `AppliesTo` never runs. Two further BMad
couplings ride along: `RepoRoot` is derived as the *parent* of an explicit `--source`, and `ReadProjectName`
reads `_bmad/config.toml`, falling back to the site title `"BMad Live Docs"` (line 86) — so a GSD site would be
branded with a BMad default.

This means **the registry alone is not sufficient** to make Story 12.2 (or 11.2) deliver a working non-BMad site.
Both prerequisites must close together. The minimal fix is to let source-root discovery probe a framework-supplied
set of marker directories (`_bmad-output`, `.planning`, `.gsd`, `.specify`) rather than one hardcoded name, and to
neutralize the default site title. Recording it here as a **shared finding for the same coordinated decision**, on
the same "close it once for all frameworks, not per-framework" basis as Gap 1.

**Gap 3 — `ArtifactBundle.Module` is structurally unfillable by a non-BMad adapter.**
`Module` is `required` and never null, but `ModuleContext` is BMad-typed to the bone: `BmadModule` is a closed
enum of `Unknown`/`BmadMethod`/`GameDevStudio`/`Unmodeled` (`ModuleContext.cs:13-27`), `Code` is documented as
"the `_bmad/{code}/` install-directory name," and `Detect` keys on that directory. A GSD adapter can therefore
only return `ModuleContext.None` — which means **no command catalog, no glossary, no module planning docs**. This
is why the About-SDD six-noun matrix's **"Planning docs" and "Commands" columns cannot be ticked for GSD** by
Story 12.2 no matter how good its parsing is, even though GSD Core publishes ten well-defined `/gsd-*` slash
commands that would populate a `CommandCatalog` beautifully. Not a blocker for the other four nouns; it *is* a
hard ceiling on two of them, and 12.2 should say so on the framework page rather than leave the columns looking
like unfinished work.

### Recommendation for Story 12.2 (and a proposed 12.3)

1. **Confirm the roster→product pinning first** (`gsd` = GSD Core `.planning/`, `gsd-pi` = GSD Pi `.gsd/`), and
   record the canonical URLs in `README.md` and the framework roster. Everything else depends on it.
2. **Split 12.2 into 12.2 (GSD Core) and 12.3 (GSD Pi)**; do GSD Core first (markdown-native, no projection
   reliability problem, higher value per unit of work). Land the split in `epics.md` **and**
   `sprint-status.yaml` together.
3. **Close Gaps 1 + 2 together, once, in whichever coverage story lands first** — registry *and* framework-neutral
   source-root discovery. Handle the `IngestEpics` watch-path constraint (AD-5) explicitly.
4. **Reuse `CoverageTier`/`CoverageTiers`** (Story 18.5). Do not mint a tier scale.
5. **Target the four reachable nouns** — Epics & Stories, Requirements, Sprint, Retros(=honestly empty) — and say
   plainly on the framework page that Planning docs and Commands await a framework-neutral `ModuleContext`.
6. **Pin the `StoryInfo.Id` synthesis in a test** (`"{milestone}.{slice}"` vs `"{phase}.{wave}"`) before writing
   discovery globs.
7. **Re-verify every filename against a generated repo.** This map is documentation-derived; see the Debug Log's
   residual-uncertainty note.

### File List

The spike itself (Tasks 1–7) produced no code. The `src/`/`tests/` entries below are all from the **owner-directed
scope addition** (Tasks 8–10) and are confined to the framework roster — no adapter, parser, or registry work.

Added:

- `tests/SpecScribe.Tests/AboutSddFrameworkRosterTests.cs` — 4 tests pinning the two GSD products as distinct
  (marker directory + canonical docs host per entry), and forbidding either URL from pointing at the retired
  `gsd-build/gsd-2`. A **new file rather than an addition to `SiteGeneratorHowToReadTests.cs`**, deliberately: that
  file is being edited by a concurrent session, and CLAUDE.md's hunk-attribution rule makes a separate file the
  cleaner boundary for review.

Modified:

- `src/SpecScribe/AboutSddTemplater.cs` — `Frameworks` gains `Url` + `Blurb`; `AppendComingSoonBody` renders both.
  **Scope note (code review 2026-08-02):** Task 8 authorized pinning only `gsd`/`gsd-pi`, but the roster tuple
  widening also populated `Url` for `bmad` and `Url`+`Blurb` for `speckit` (Epic 11's subject) without disclosure.
  Owner reviewed and chose to keep the widened scope rather than revert it. As part of the same review,
  `AppendBmadBody`/`AppendGdsBody` were changed to read `fw.Url` instead of a hardcoded literal (previously
  `bmad`'s new `Url` field was dead code, and `gds` had no `Url` at all despite `AppendGdsBody` also hardcoding
  one) — `gds` now carries its canonical URL in the roster too.
- `README.md` — both GSD rows linked in the "Supported frameworks" table, plus a disambiguation note.
- `_bmad-output/planning-artifacts/epics.md` — Story 12.2 split into 12.2 (GSD Core) + 12.3 (GSD Pi); Epic 12
  intro records the two-adapter-surfaces conclusion.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story key → `in-progress` → `review`; the 12.2 key
  replaced by `12-2-gsd-core-baseline-adapter-coverage` + `12-3-gsd-pi-baseline-adapter-coverage`; `last_updated`.
- `_bmad-output/implementation-artifacts/12-1-gsd-and-gsd-pi-integration-spike.md` (this file — frontmatter
  `baseline_commit`, task checkboxes, Tasks 8–10, Dev Agent Record, File List, Change Log, Status)

## Change Log

- 2026-08-02 — **Code review (bmad-code-review).** Scoped to this story's own File List (6 files), sibling-session
  changes excluded per CLAUDE.md. 1 decision-needed, 4 patch, 1 defer, 6 dismissed. Owner resolved the
  decision-needed item (undisclosed `bmad`/`speckit` `Url`/`Blurb` widening) by keeping the widened scope and
  wiring it up properly. All 4 patches applied: (1) `AppendBmadBody`/`AppendGdsBody` now read `fw.Url` instead of
  a hardcoded literal, and `gds` gained its canonical URL in the roster (previously `null` despite
  `AppendGdsBody` hardcoding one) — the widening is disclosed in the File List above; (2) Story 12.3's AC #3
  gained the "reusing the existing CoverageTier vocabulary rather than a parallel scale" clause 12.2 already had;
  (3) Story 12.3 gained a new AC #4 pinning the Milestone→Slice story-id synthesis form (`"{milestone}.{slice}"`)
  by a test, mirroring 12.2 AC #3; (4) the dangling `<see cref="RenderHub"/>` left in `BuildHubPage`'s doc comment
  after this diff deleted `RenderHub`/`RenderFrameworkPage` was removed; (5) the stale pre-split downstream-key
  reference in this story's own Project Structure Notes was corrected to name both `12-2-gsd-core-...` and
  `12-3-gsd-pi-...`. 1 item deferred to `deferred-work.md` (Story 12.1's own `epics.md` AC block not revisited for
  the owner-directed scope addition — non-blocking, already disclosed elsewhere in this story). Verified:
  `dotnet build` clean (0 warnings/errors); `AboutSddFrameworkRosterTests` 4/4 pass; broader `AboutSdd*` suite
  12/12 pass. Status: `review` → `done`.

- 2026-08-02 — **Owner-directed scope addition** (same dev-story session, after the owner reviewed the spike's
  findings). The owner **confirmed the roster pinning** and directed this story to land it plus the plan updates,
  deliberately superseding Task 7's "no production code" constraint; scope bounded to the roster/documentation
  pinning and the plan split, with no adapter/parser/registry work (Tasks 8-10). Landed: (a)
  `AboutSddTemplater.Frameworks` gains a canonical `Url` and an identity `Blurb` — `gsd` → GSD Core
  (`docs.opengsd.net/core`), `gsd-pi` → GSD Pi (`docs.opengsd.net/pi`) — rendered on the placeholder framework
  pages so "Coming soon" states what each framework *is* rather than only that it is absent; (b) `README.md`'s
  framework table linked, with a disambiguation note naming the differing markers, authority models and
  hierarchies and recording that the retired `gsd-build/gsd-2` continues as GSD Pi; (c) **Story 12.2 split into
  12.2 (GSD Core, first) + 12.3 (GSD Pi)**, co-landed in `epics.md` **and** `sprint-status.yaml` per the
  decision-records rule, with framework-specific ACs and Epic 12's intro updated to the two-adapter-surfaces
  conclusion. New test file `tests/SpecScribe.Tests/AboutSddFrameworkRosterTests.cs` (4 tests, written red-green)
  pins the two products as distinct by marker directory and docs host, and fails if either URL is ever pointed
  back at the retired `gsd-2`. Deliberate non-change: the display `Label`s stay `GSD`/`GSD-Pi` — renaming them to
  `GSD Core`/`GSD Pi` is a separate owner display decision and would have put this story inside a concurrent
  session's hunks in `SiteGeneratorHowToReadTests.cs`. Full suite **2948 passed / 0 failed**.
- 2026-08-02 — Story 12.1 implemented (dev-story, baseline `b397084`). Spike complete; no production code.
  **Headline: the story's central premise was wrong and the coverage map corrects it.** Current-version GSD is
  **GSD Core** (`@opengsd/gsd-core`) — marker `.planning/`, plain markdown + JSON, **no SQLite**, hierarchy
  Milestone → **Phase** → Task — not the `.gsd/`+SQLite product create-story surveyed; that was `gsd-build/gsd-2`,
  the **retired** predecessor which "now continues as GSD Pi." Task 3 therefore resolves **two adapter surfaces,
  not one** (disjoint markers, authority models, mid-level nouns and path grammars; only `STATE.md` overlaps), and
  recommends splitting Story 12.2 into 12.2 (GSD Core, first) + 12.3 (GSD Pi) — recorded as an owner
  recommendation, not executed, per the epics.md/sprint-status.yaml co-landing rule. Second correction: the
  coverage-tier vocabulary **already exists in code** (`CoverageTier`/`CoverageTiers`, Story 18.5,
  `TestArtifactsModel.cs:18-76`) and is already rendered as an About-SDD legend — 12.2 reuses it rather than
  minting one. Rulings: Milestone→`EpicInfo`, Slice/Phase→`StoryInfo`, Task→`TasksDone/TasksTotal` only;
  `DECISIONS.md`→ADR side-channel in spirit but not mechanically reachable (it is a *register*, the side-channel
  expects one-file-per-decision); `Requirements` mappable-but-`Summarized` for both (GSD Pi's is deep-mode-only;
  `REQ-001` cannot be represented without extending the throwing `RequirementKind`; no FR→Epic coverage map means
  `Unmapped` would misleadingly read as "no plan exists"); **`Retros = []` for both** — forcing execution
  summaries into `RetroModel` would flip `EpicInfo.HasRetrospective` and silently mark milestones closed on every
  visual surface. Beyond 11.1's shared registry gap (confirmed; 11.1 still `ready-for-dev` so nothing to defer
  to yet), found **two further prerequisites**: `ForgeOptions` hardcodes `_bmad-output` as the repo-root discovery
  marker so `generate` fails in a pure GSD repo *before* any adapter is consulted (and defaults the site title to
  "BMad Live Docs"), and `ArtifactBundle.Module`/`ModuleContext` is BMad-typed to a closed enum keyed on
  `_bmad/{code}/`, making the About-SDD matrix's "Planning docs" and "Commands" columns structurally unfillable
  for GSD. No ADR proposed — the registry decision stays ONE shared decision coordinated with 11.1.
- 2026-07-20 — Story 12.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map (with declared coverage tiers) + GSD↔GSD-Pi relationship decision + 12.2 scope recommendation; no production code. Second story of the five per-framework spike-led epics (11–15); covers TWO frameworks (GSD + GSD-Pi) and introduces the mandatory coverage-tier axis.
