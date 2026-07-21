# Story 11.1: Spec Kit Integration Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support Spec Kit,
I want the Spec Kit artifact set mapped against the shared adapter contract before coverage work begins,
so that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 (one per framework) were extracted 2026-07-10 into five appended, spike-led epics (11–15; see `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`) specifically so each framework gets its own upfront mapping exercise instead of guessing. **This is the first of those five spikes to run** — Epic 11 is currently `backlog` (this story creation moves it to `in-progress`); Epics 12–15 (GSD/GSD-Pi, SpecFlow, Squad, Superpowers) are untouched and will repeat this same shape later.

**The one-line test for "is this in scope?":** if the change *surveys* Spec Kit repos, *classifies* artifacts against the existing `ArtifactBundle`/model shapes, or *writes* a coverage map + non-goals list → in. If it *builds* a `SpecKitArtifactAdapter`, parses a single line of Spec Kit markdown into a real model, or lands any `src/`/`tests/` change → out; that is Story 11.2 (Spec Kit Baseline Adapter Coverage, not yet created).

**Precedent for this shape:** Story 19.1 (Work-Graph Model and Coverage Spike, `ready-for-dev`) is the closest prior example of a pure-tracing, no-code spike whose deliverable is a coverage map inside Completion Notes — mirror its discipline, not Story 6.3's (that spike built throwaway extension code on a branch; this one should need none). If you find yourself wanting to write a branch and a scaffold, stop — you have drifted into 11.2.

## Acceptance Criteria

1. **Given** representative current-version Spec Kit repositories, **when** the Spec Kit artifact set is surveyed against the shared adapter contract's `ArtifactBundle` and projection model, **then** a written coverage map classifies each Spec Kit artifact type as mappable, partially-mappable, or unsupported, **and** the target shared-model projection is named for each mappable type.

2. **Given** Spec Kit conventions that exceed the shared projection model or that SpecScribe will deliberately not support, **when** the spike documents its findings, **then** framework-extra data is recorded as candidate projection extensions or explicit non-goals, **and** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2335-2353`]

## Context & Scope

### The contract this spike maps against (read the real code, not just the epic prose)

- **`IArtifactAdapter`** [src/SpecScribe/IArtifactAdapter.cs:19-38] — two methods: `AppliesTo(ForgeOptions, sourceFiles)` (cheap self-selection sniff, never throws) and `Ingest(ForgeOptions, sourceFiles, ProgressProjection?)` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics` instead).
- **`ArtifactBundle`** [src/SpecScribe/ArtifactBundle.cs:10-58] — the ONLY shape a new adapter must produce. Its fields, verbatim:
  | Field | Type | Null/empty-safe? |
  |---|---|---|
  | `Module` | `ModuleContext` | Never null — absent detection is `ModuleContext.None` |
  | `Sprint` | `SprintStatus?` | Null when absent |
  | `Retros` | `IReadOnlyList<RetroModel>` | Empty when none |
  | `Epics` | `EpicsModel?` | Null when absent/unparseable |
  | `Requirements` | `RequirementsModel?` | Null when absent |
  | `EpicsSourceFullPath` | `string?` | For generic-page exclusion |
  | `StoryArtifactsById` | `IReadOnlyDictionary<string,string>` | Story id → detail-artifact path |
  | `ConsumedSourceRelatives` | `IReadOnlyCollection<string>` | Files claimed by dedicated surfaces |
  | `Diagnostics` | `IReadOnlyList<AdapterDiagnostic>` | Non-fatal problems |
- **`AdapterDiagnostic(Category, RelativePath, Message)`** [src/SpecScribe/AdapterDiagnostic.cs:7-43] — `Category` is one of `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested, e.g. a duplicate), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed). **These five categories are the entire non-fatal vocabulary this spike's "unsupported conventions" and "non-fatal notice they will emit" (AC #2) must map onto** — do not invent a sixth.
- **The one existing adapter to mirror, not copy verbatim:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344]. Read it end to end — it is the working example of "self-selection sniff → discover files by well-known name/location → parse each family independently → never let one family's failure kill the others → diagnose non-fatally." Its `AppliesTo` sniffs `_bmad/` at the repo root [BmadArtifactAdapter.cs:76-77]; the equivalent Spec Kit signal is almost certainly a `.specify/` directory (verify against real repos, see below).

### The load-bearing gap this spike must surface, not solve

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:47-51] — with a comment stating plainly: *"the adapter registry that selects among `IArtifactAdapter` implementations arrives with Stories 4.3+."* Those stories are exactly the ones that got relocated into Epics 11–15, so **the registry has no owner today.** `ARCHITECTURE-SPINE.md` explicitly leaves this open: *"Exact adapter loading mechanics... are implementation seeds"* [`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:101`].

This spike does **not** need to design or build that registry — but AC #1/#2's "coverage story an agreed scope boundary" is incomplete if it silently assumes Spec Kit coverage can land without one. **Task 5 below requires the spike to name this gap explicitly** as a prerequisite Story 11.2 (or a small standalone story) must close, and to recommend — not implement — a minimal shape (e.g. an ordered list of `IArtifactAdapter`s, first `AppliesTo` match wins, `BmadArtifactAdapter` stays the fallback). Per this project's standing convention ([[adr-creation-trigger-gap-epic-10-retro]] memory; the project's own retro action item), if the spike concludes this is a genuine architectural fork (not just a small addition) — **propose an ADR, do not bury the decision in Completion Notes prose.** If it looks like a small, obvious seam extension instead, say so and let 11.2 build it inline.

### The "host-neutral" models are less framework-neutral than they sound

Read these before assuming any Spec Kit artifact maps cleanly:

- **`EpicsModel`/`EpicInfo`/`StoryInfo`** [src/SpecScribe/EpicsModel.cs] bake in BMad vocabulary directly into what the contract calls "host-neutral": `EpicStatus { Drafted, Pending }`, `EpicSection { VerticalSlice, FurtherDevelopment }` (a BMad epics.md convention with no obvious Spec Kit analog), and `StoryInfo.Id` is hard-typed as BMad's `"N.M"` two-level numbering with a resolved `ArtifactOutputPath`/`ArtifactSourcePath` pointing at a discrete per-story markdown file. Spec Kit has no epic/story two-level hierarchy — it has flat, independently-numbered feature folders (`specs/003-chat-system/`), each with its OWN `spec.md` + `plan.md` + `tasks.md` (see below). Don't assume a Spec Kit "feature" is a story OR an epic without deciding which — that decision (or "neither, needs a new shape") is this spike's job.
- **`RequirementsModel`/`RequirementInfo`** [src/SpecScribe/RequirementsModel.cs:1-73] is entirely BMad's `FR`/`NFR`/`UX-DR` numbered-requirements-with-a-Coverage-Map convention, parsed from epics.md's "Requirements Inventory" section. Spec Kit has **no numbered-requirements convention** (confirmed live below) — expect `Requirements = null` for Spec Kit unless the spike finds a plausible substitute (e.g. `spec.md`'s "Functional Requirements" prose section, without FR-numbering).
- **`SprintStatus`** [src/SpecScribe/SprintStatus.cs] is literally `sprint-status.yaml`-shaped (a `development_status` map + `action_items`). Spec Kit ships **no sprint/kanban/status-tracking file** (confirmed live below) — expect `Sprint = null` always, not a gap to fill.
- **`RetroModel`** [src/SpecScribe/RetroModel.cs] is BMad's `epic-N-retro-*.md` convention. Spec Kit has **no retrospective-note convention** (confirmed live below) — expect `Retros = []` always.

**Net implication to verify, not assume:** Spec Kit coverage will likely land almost entirely on the `Epics`/`StoryArtifactsById` slice of the bundle (if a mapping is chosen at all), with `Sprint`/`Retros`/`Requirements` staying structurally absent (which is fine — NFR8 says absent-not-broken) rather than "unsupported." Confirm or overturn this with real repos before writing the coverage map.

### Spec Kit's real shape — freshly verified 2026-07-20, treat as a starting hypothesis to confirm against real repos (per AC #1's literal "representative current-version Spec Kit repositories")

Live-checked against github.com/github/spec-kit (README + spec-driven.md) during create-story — **this tool's conventions can and do shift; re-verify, don't trust this table blindly:**

| Spec Kit concept | Path / shape | Closest ArtifactBundle candidate |
|---|---|---|
| Install marker | `.specify/` at repo root (`memory/`, `templates/overrides/`, `extensions/templates/`, `presets/templates/`) | `AppliesTo` self-selection signal (mirrors `_bmad/`) |
| Constitution | `.specify/memory/constitution.md` — project governance/principles | No BMad analog; candidate `ModuleContext`-style doc, or framework-extra |
| Per-feature folder | `specs/<NNN>-<feature-slug>/` (e.g. `specs/003-chat-system/`), one per feature branch | Closest thing to an "epic," but flat/independently-numbered, not BMad's nested epic→story |
| Feature spec | `specs/<NNN>-slug/spec.md` — requirement definitions | No 1:1 `EpicInfo`/`StoryInfo` fit — decide explicitly |
| Implementation plan | `specs/<NNN>-slug/plan.md` | — |
| Task breakdown | `specs/<NNN>-slug/tasks.md` — executable checklist | Closest analog to `StoryInfo.TasksDone`/`TasksTotal`, but flat within one feature, not per-story |
| Supporting docs | `research.md`, `data-model.md`, `contracts/`, `quickstart.md` (all optional, same folder) | Likely framework-extra / non-goal for MVP coverage |
| Slash commands (2026) | `/speckit.constitution`, `/speckit.specify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.implement`, plus optional `/speckit.clarify`, `/speckit.analyze`, `/speckit.checklist`, `/speckit.converge`, `/speckit.taskstoissues` | Informational only — SpecScribe reads artifacts, not commands |
| Sprint/kanban file | **None found** | `Sprint` stays null — not a gap |
| Retrospective notes | **None found** | `Retros` stays empty — not a gap |
| Numbered FR/NFR requirements | **None found** — `spec.md` has prose "Functional Requirements," unnumbered | `Requirements` likely null, or a lesser prose-only substitute — spike decides |

**Do not treat this table as ground truth.** Fetch/clone at least one real, current Spec Kit-initialized repo (or the spec-kit templates themselves) and confirm every row — the AC's "representative current-version" language exists precisely because this tool evolves and prose docs under-specify exact paths.

### Existing Spec Kit surfaces already in the portal (placeholders — 11.2's eventual targets, not this spike's job to fill)

Spec Kit is not fully unmentioned in the codebase — these are pre-existing placeholders this spike should be aware of (and 11.2 will eventually wire up), but none of them do any actual parsing:

- **`AboutSddTemplater.cs`** carries a `Frameworks` roster (`bmad`, `gds`, `speckit` [`Supported: false`], `gsd`, `gsd-pi`, `superpowers`) rendering a **support-matrix table across six nouns: Epics & Stories / Requirements / Sprint / Retros / Planning docs / Commands** [AboutSddTemplater.cs:10-18, 96-121], with a generic "Coming soon" body for unsupported frameworks [AboutSddTemplater.cs:224-231]. **This six-noun matrix is a ready-made, already-shipped vocabulary** — align the coverage map's artifact-type naming to it where it fits, rather than inventing a parallel taxonomy.
- **`SiteNav.cs:68`** already defines `AboutSddSpecKitOutputPath = "about-sdd-speckit.html"` — the page route exists; content is a placeholder.
- **`README.md`**'s "Supported frameworks" table already lists `[GitHub Spec Kit](https://github.com/github/spec-kit) | — | 🧭 Planned`.
- **`ArtifactCoverage.cs:79-81`** has its own explicit comment: *"THIS list is the coverage seam Epic 4 generalizes — a future framework adapter swaps this family set, not the panel or the builder."* — a second, dashboard-level coverage concept (presence/freshness of a repo's OWN planning docs), distinct from this spike's artifact-classification coverage map. Don't conflate the two "coverage" senses when writing findings — name which one you mean each time.

**ADRs are a side-channel, not part of `ArtifactBundle` at all.** There is no dedicated ADR parser class and no `ArtifactBundle` field for them — `docs/adrs/*.md` are hand-authored and read via a separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside the `IArtifactAdapter` contract. **This matters directly for Spec Kit's `constitution.md`** (a governance/decision-record document with no obvious BMad analog): classify it explicitly as (a) belonging in the ADR side-channel, (b) a new `ArtifactBundle` field/model, or (c) out of scope for now — don't leave it unclassified.

**A closer coverage-map precedent than 19.1: Epic 18's Story 18.1** (`epics.md`, ~line 2897, still `backlog` — not yet written) uses near-identical AC language to this story ("a written coverage map classifies each module's distinctive artifact types as mappable, partially-mappable, or unsupported... names the target shared-model projection... recommends a priority module to cover first"). No concrete coverage-map document exists on disk yet anywhere in this repo for either epic — **this spike's Completion Notes will be the first one actually written**, and its shape should be reusable by Epics 12-15 and 18 afterward. Note also that "coverage tier" (a graded label beyond mappable/partial/unsupported) is language Epic 12 (GSD/GSD-Pi) introduces, not a requirement of this story's own AC text — adopt it only if it genuinely helps, don't force it in to match Epic 12 prematurely.

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building `SpecKitArtifactAdapter`** or any parser — that's 11.2.
- **Designing the adapter registry** — name the gap (Task 5), don't design or implement it here.
- **Extending `ArtifactBundle`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Writing an ADR unless a genuine architecture fork is found** — see the registry-gap guidance above.
- **A new authoring schema** for Spec Kit content — SpecScribe reads Spec Kit's existing conventions as-is.

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the contract shapes against live code (AC: #1)**
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables; they are a starting point, not a substitute for reading the code.
  - [ ] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:47-51`).

- [ ] **Task 2 — Obtain and inspect representative current-version Spec Kit repositories (AC: #1, #2)**
  - [ ] Fetch/inspect the current github/spec-kit templates (or a real `specify init`-scaffolded sample project) to confirm exact file names, folder depth, and the `.specify/` marker shape — the table above is a hypothesis from a single doc-page fetch, not a repo inspection.
  - [ ] Note the exact numbering/naming convention for `specs/<NNN>-slug/` folders (zero-padding? sequential per-repo? branch-name-derived?).
  - [ ] Confirm whether `research.md`/`data-model.md`/`contracts/`/`quickstart.md` are consistently present or genuinely optional-per-feature.

- [ ] **Task 3 — Classify every discovered artifact type (AC: #1)**
  - [ ] For each Spec Kit artifact type (constitution, feature spec, plan, tasks, research/data-model/contracts/quickstart), classify as **mappable** (name the exact target: `ArtifactBundle` field + model type/record), **partially-mappable** (name what maps and what doesn't), or **unsupported** (name why).
  - [ ] Resolve explicitly whether a Spec Kit "feature" folder maps to `EpicInfo`, to `StoryInfo`, to neither, or needs a new shape — do not leave this ambiguous, it is the central modeling question (see "host-neutral models" caveat above).
  - [ ] Classify `.specify/memory/constitution.md` explicitly: ADR side-channel, new `ArtifactBundle` field, or out of scope — see "ADRs are a side-channel" note above; don't leave it unclassified.
  - [ ] State explicitly, per the caveat table above: is `Sprint` null (no Spec Kit analog), is `Retros` empty (no analog), is `Requirements` null or a lesser prose-only substitute — confirmed against real repos, not assumed.

- [ ] **Task 4 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any Spec Kit convention richer than the shared model (e.g. constitution, research/data-model/contracts/quickstart docs, the extension/preset/override template layering), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale).
  - [ ] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188, 219-224, 262-276] for tone/specificity.

- [ ] **Task 5 — Name the adapter-registry gap as an explicit finding (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim above against `SiteGenerator.cs`. State plainly in Completion Notes that Story 11.2 cannot wire in a second adapter without SOME selection mechanism existing, and recommend (not build) a minimal shape.
  - [ ] Decide: is this a genuine architecture fork warranting a proposed ADR, or a small, obvious seam extension 11.2 can build inline? Say which, and why (mirror the ADR-trigger discipline this project already applies at Story 6.3).

- [ ] **Task 6 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (Nodes/Types table + non-goals + registry-gap finding + 11.2 recommendation) into this story's **Completion Notes**, mirroring Story 19.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 5 concludes a genuine fork exists.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live repo inspection, not code.** Unlike Story 6.3 (which built a throwaway extension to prove feasibility), this spike's evidence comes from reading `src/SpecScribe/*.cs` and reading/fetching real Spec Kit repos — not from writing a prototype adapter. If you catch yourself scaffolding `SpecKitArtifactAdapter.cs`, stop; that's 11.2.
- **No new authoring schema.** SpecScribe never asks Spec Kit users to add SpecScribe-specific files — coverage must derive from Spec Kit's own existing conventions.
- **Verify, don't assume.** This story's "Spec Kit's real shape" table was built from two doc-page fetches during create-story, not a cloned repo — the AC's "representative current-version" language exists because this is exactly the kind of assumption that goes stale. Confirm every row.
- **NFR8.** Absent artifact families (`Sprint`, `Retros`, likely `Requirements`) are not gaps to fill — they're correct, honest absence. Don't recommend inventing a sprint-tracking or retrospective convention for Spec Kit; that would violate the framework-neutral principle (NFR8 exact wording below).

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future Spec Kit adapter only translates into `ArtifactBundle`, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps Spec Kit source shapes onto that exact contract, nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future Spec Kit-specific insight enrichment must stay additive/non-blocking, same as BMad's git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."* This is the literal rule behind "Sprint=null/Retros=empty is correct, not a gap."
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — do not let this spike accidentally commit to `src/SpecScribe.Adapters.SpecKit` as a real package (that's an aspirational sketch in `rendering-architecture.md:104`, not a current rule; the project is still single-namespace, single-project — [[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- Assuming a Spec Kit "feature" is 1:1 an `EpicInfo` or a `StoryInfo` without stating the decision and its consequences.
- Recommending SpecScribe invent a sprint-tracking or retrospective file convention for Spec Kit users — NFR8 says absence is honest, not a defect.
- Designing or partially building the adapter registry inline instead of naming it as a Task 5 finding for 11.2 (or a dedicated small story) to close.
- Silently committing to a package/namespace split (`SpecScribe.Adapters.SpecKit`) — that's an aspirational sketch, not current architecture.
- Treating this story's own hypothesis table as verified fact instead of confirming it against a real, current Spec Kit repo.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`
- Sprint key: `11-1-spec-kit-integration-spike`
- Downstream story key (not created by this spike): `11-2-spec-kit-baseline-adapter-coverage`
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 5 concludes a genuine architecture fork (registry design) — if so, follow the ADR 0004/0005 format (`docs/adrs/`, numbered next as 0008, indexed in `docs/adrs/README.md`) and escalate rather than deciding silently, per this project's ADR-creation-trigger discipline.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:44,165,247-249,2329-2373`] — FR3, Epic 4/11 goal lines, Epic 11 + Story 11.1/11.2 detail (verbatim ACs quoted above).
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`] — why Epics 11-15 exist, the fixed framework→epic mapping (11=Spec Kit/FR3), the "X.1 spike, X.2 coverage" pattern.
- [Source: `src/SpecScribe/IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`] — the contract + its one reference implementation.
- [Source: `src/SpecScribe/EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`] — the "host-neutral" model shapes, BMad-specific vocabulary baked in.
- [Source: `src/SpecScribe/SiteGenerator.cs:47-51`] — the hardcoded single-adapter field; the registry gap.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2/AD-4, Seed-not-invariant] — the invariants this spike must respect and the open seeds it must not over-commit.
- [Source: `_bmad-output/specs/spec-specscribe/rendering-architecture.md:100-109`] — the aspirational (not current) per-framework package sketch, `src/SpecScribe.Adapters.SpecKit` named there.
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md`] — the closest prior pure-tracing spike; mirror its Completion-Notes-as-deliverable convention.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18,96-121,224-231`] — the existing six-noun (Epics&Stories/Requirements/Sprint/Retros/Planning docs/Commands) framework support-matrix + "Coming soon" Spec Kit placeholder; `src/SpecScribe/SiteNav.cs:68` — the already-routed `about-sdd-speckit.html` page; `README.md` "Supported frameworks" table — Spec Kit listed "🧭 Planned."
- [Source: `src/SpecScribe/ArtifactCoverage.cs:79-81`] — the dashboard-level "coverage" concept (repo's own doc freshness), a different sense from this spike's artifact-classification coverage map; don't conflate.
- [Source: `_bmad-output/planning-artifacts/epics.md` ~line 2897, Epic 18 Story 18.1] — a second, near-identical coverage-map AC precedent (also unwritten); "coverage tier" is Epic 12 vocabulary, not required here.
- [Source: github.com/github/spec-kit README + spec-driven.md, fetched live 2026-07-20] — current `.specify/` layout, `specs/<NNN>-slug/` per-feature shape, `/speckit.*` slash commands; treat as a hypothesis to reconfirm, not settled fact.
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions rather than burying them in a story), [[static-prerendering-vs-json-spa-architecture-question]] (unrelated open question, do not conflate).

### Git intelligence summary

No Spec Kit code, docs, or prior exploration exist anywhere in this repo (confirmed via grep across `docs/`, `epics.md`) — this spike starts from a genuinely clean slate on the Spec Kit side, with a well-established adapter contract and one working reference implementation (`BmadArtifactAdapter`) on the SpecScribe side. Recent commits (Epic 10 cleanup, retro work) are unrelated to this story's scope.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-20 — Story 11.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + 11.2 scope recommendation; no production code. First story of Epic 11 (first of the five per-framework spike-led epics, 11-15, to actually start).
