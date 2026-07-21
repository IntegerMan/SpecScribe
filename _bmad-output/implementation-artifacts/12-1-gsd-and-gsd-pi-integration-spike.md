# Story 12.1: GSD and GSD-Pi Integration Spike

Status: ready-for-dev

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

- [ ] **Task 1 — Confirm the contract shapes against live code (AC: #1)**
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables; they are a starting point, not a substitute for reading the code.
  - [ ] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:47-51`), and check whether Story 11.1 has already landed/recommended a registry shape (read its Completion Notes if `done`).
  - [ ] Re-read the `gds` vs `gsd`/`gsd-pi` distinction in `AboutSddTemplater.cs` so you never conflate BMad GDS (supported) with GSD (this spike).

- [ ] **Task 2 — Obtain and inspect representative current-version GSD AND GSD-Pi repositories (AC: #1, #2)**
  - [ ] Fetch `https://docs.opengsd.net/pi/concepts/project-structure.md` (the GSD-Pi filenames create-study could not resolve) and confirm GSD-Pi's exact `.gsd/` layout.
  - [ ] Inspect the GSD (`gsd-build/gsd-2`) and GSD-Pi (`open-gsd/gsd-pi`) repos/templates (or a real `.gsd/`-initialized sample) to confirm exact file names, folder depth (`.gsd/milestones/M###/slices/S##/`), the `.gsd/` marker, and the gitignore reality of `gsd.db` — the table above is a hypothesis from doc-page fetches, not a repo inspection.
  - [ ] Confirm the exact numbering/naming convention for `M###` milestones and `S##` slices (zero-padding? sequential per-repo?).
  - [ ] Determine whether the `.gsd/*.md` projections are reliably committed in a real repo, or can be stale/absent when only the gitignored `gsd.db` is current (difference #3) — this decides tiers and diagnostics.

- [ ] **Task 3 — Resolve the GSD vs GSD-Pi relationship (AC: #1)**
  - [ ] State explicitly whether GSD and GSD-Pi share one adapter surface (same `AppliesTo` sniff + ingest, per-variant tolerance) or need two, with the on-disk evidence and the consequence for Story 12.2's scope.
  - [ ] Note any GSD-Pi-only or GSD-only artifacts/conventions that would force divergence.

- [ ] **Task 4 — Classify every discovered artifact type with a declared coverage tier (AC: #1)**
  - [ ] Fix a small coverage-tier vocabulary aligned to FR-4 (**rendered / summarized / unsupported**) [requirements-catalog.md:18] and define what each tier means for SpecScribe output.
  - [ ] For each GSD/GSD-Pi artifact type (`.gsd/` marker, `gsd.db`, PROJECT.md, REQUIREMENTS.md, DECISIONS.md, KNOWLEDGE.md, STATE.md, milestones/roadmaps, slices/plans, slice summaries, tasks), classify as **mappable** (name the exact target: `ArtifactBundle` field + model type/record) / **partially-mappable** (name what maps and what doesn't) / **unsupported** (name why), AND assign a **coverage tier**.
  - [ ] Resolve explicitly whether a GSD *Milestone* → `EpicInfo`, *Slice* → `StoryInfo`, *Task* → `StoryInfo.TasksDone/TasksTotal`, or the three-level shape needs a documented compromise — the central modeling question (see "host-neutral models" caveat).
  - [ ] Classify `.gsd/DECISIONS.md` explicitly: ADR side-channel, new `ArtifactBundle` field, or out of scope — mirror 11.1's `constitution.md` classification.
  - [ ] State explicitly, confirmed against real repos: is `Requirements` mappable-via-REQUIREMENTS.md or null, is `Sprint` partially-mappable-via-STATE.md or null, is `Retros` empty or fed by slice summaries — do NOT copy Spec Kit's null table.

- [ ] **Task 5 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any GSD convention richer than the shared model (e.g. `gsd.db` DB-authority, KNOWLEDGE.md Rules/Patterns/Lessons, the milestone/slice/task three-level grain, DB-derived projection staleness), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale).
  - [ ] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188, 219-224, 262-276] for tone/specificity. In particular, draft the notice for "`.gsd/` present but markdown projections absent/stale (DB is authoritative)".

- [ ] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1 (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim against `SiteGenerator.cs`. State plainly that Story 12.2 cannot wire in a second adapter without SOME selection mechanism, that this gap is **shared with 11.1** (not GSD-specific), and that whichever coverage story lands first closes it once for all frameworks.
  - [ ] Recommend (not build) a minimal registry shape, and defer to 11.1's conclusion/ADR if it exists. Do NOT propose a second, GSD-specific registry ADR.

- [ ] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (artifact-type × classification × target projection × coverage tier table + GSD/GSD-Pi relationship decision + non-goals + shared registry-gap finding + 12.2 recommendation) into this story's **Completion Notes**, mirroring Story 11.1's / 19.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND 11.1 has not already covered it — coordinate to keep it a single shared registry ADR.

### Review Findings

_(populated during code-review)_

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
- Downstream story key (not created by this spike): `12-2-gsd-and-gsd-pi-baseline-adapter-coverage`
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-20 — Story 12.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map (with declared coverage tiers) + GSD↔GSD-Pi relationship decision + 12.2 scope recommendation; no production code. Second story of the five per-framework spike-led epics (11–15); covers TWO frameworks (GSD + GSD-Pi) and introduces the mandatory coverage-tier axis.
