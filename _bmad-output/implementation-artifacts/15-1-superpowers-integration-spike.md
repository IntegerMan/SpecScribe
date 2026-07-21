# Story 15.1: Superpowers Integration Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support Superpowers,
I want the Superpowers artifact set mapped against the shared adapter contract before coverage work begins,
so that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 (one per framework) were extracted 2026-07-10 into five appended, spike-led epics (11–15; see `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`) so each framework gets its own upfront mapping exercise instead of guessing. **This is the fourth of those five spikes to run, out of numeric order** — Epic 11 (Spec Kit) and Epic 12 (GSD/GSD-Pi) are both `in-progress` with their `.1` spikes `ready-for-dev`; Epics 13 (SpecFlow) and 14 (Squad) are still untouched `backlog`. This story creation moves Epic 15 from `backlog` to `in-progress` directly, skipping ahead of 13/14 — that is a deliberate scheduling choice by whoever ran `create-story 15.1`, not an error to correct.

**The one-line test for "is this in scope?":** if the change *surveys* Superpowers-adopting repos, *classifies* their observable artifacts against the existing `ArtifactBundle`/model shapes, or *writes* a coverage map + non-goals list → in. If it *builds* a `SuperpowersArtifactAdapter`, parses a single Superpowers plan file into a real model, or lands any `src/`/`tests/` change → out; that is Story 15.2 (Superpowers Baseline Adapter Coverage, not yet created).

**Precedent for this shape — read all three:** Story 11.1 (`11-1-spec-kit-integration-spike.md`, `ready-for-dev`) is the closest sibling — same single-framework AC skeleton, same "feature/plan doesn't cleanly nest into Epic→Story" modeling puzzle. Story 12.1 (`12-1-gsd-and-gsd-pi-integration-spike.md`, `ready-for-dev`) is the second sibling and shows how a spike surfaces a framework-specific structural surprise (there, DB-vs-markdown authority; here, see below). Story 19.1 (Work-Graph Model and Coverage Spike, `ready-for-dev`) is the older pure-tracing, no-code precedent both siblings cite. None of the three built production code, and neither should this. If you find yourself wanting to write a branch and a scaffold, stop — you have drifted into 15.2.

## What makes this spike different from 11.1/12.1 (do not just copy their answers)

Story 15.1 is **not** a find-and-replace of Spec Kit's or GSD's findings. Superpowers turns out to be structurally the most different of the three frameworks spiked so far — read this before touching the code:

1. **Superpowers is not installed INTO the target repo at all.** Spec Kit leaves a `.specify/` marker directory; GSD/GSD-Pi leave a `.gsd/` marker directory; BMad leaves `_bmad/`. Superpowers is a **coding-agent plugin** (Claude Code, Cursor, Codex, and others) — its skills live in the *agent's* plugin system, not copied into the consuming repo. Live-verified 2026-07-20 against `github.com/obra/superpowers`: installation is `/plugin install` (or per-harness equivalents), and "no directory or configuration file is copied into user projects." This means the self-selection signal `AppliesTo` normally relies on (a cheap marker-directory sniff, mirroring `BmadArtifactAdapter.AppliesTo` at `_bmad/` [BmadArtifactAdapter.cs:71-77]) **has no direct Superpowers analog.** This is the single most load-bearing finding of this spike — resolve it explicitly, don't gloss over it.
2. **The only durable on-disk trace is a plan-file convention, and it is user-configurable.** The `writing-plans` skill saves plans to `docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md` — but the framework's own docs note "user preferences can override this default location." A repo genuinely using Superpowers may keep plans somewhere else entirely, and a repo NOT using Superpowers could coincidentally have a `docs/plans/` folder with date-slug markdown files from an unrelated convention. `AppliesTo` must decide how to handle both false-negative and false-positive risk — do not assume the default path is reliable enough to gate detection on alone without discussing the tradeoff.
3. **No epic/story ID scheme at all — flatter than Spec Kit, not just "different."** Spec Kit at least has flat, independently-numbered feature folders (`specs/<NNN>-slug/`). Superpowers plan documents are dated, standalone files with **no folder grouping and no numeric ID** — just `Task 1`, `Task 2`, ... sequential headings with component names, inside one flat file. There is no per-feature folder the way Spec Kit or BMad's per-story artifact has. Decide explicitly whether one plan document maps to a `StoryInfo` (closest analog: one file, checkbox-tracked tasks, much like a BMad story's own Tasks/Subtasks section) or needs a different shape — and state plainly that there is **no `EpicInfo` analog at all** unless one is synthesized (e.g. "the whole plans folder is one implicit epic"), which is itself a decision worth stating and justifying or rejecting.
4. **Status tracking lives INSIDE the plan document, not in a separate sprint file.** Progress is tracked via `- [ ]` checkboxes on each task's TDD sub-steps within the same plan `.md` file — there is no `sprint-status.yaml` analog, no `development_status` map, no separate kanban artifact. Unlike GSD (which at least has `STATE.md` as a `Sprint` candidate), Superpowers appears to have **no separate status artifact whatsoever** — the plan file IS the status. Confirm this against a real plan example (`docs/plans/` in the `obra/superpowers` repo itself has dated examples) before concluding `Sprint = null` outright; consider whether checkbox completion ratio inside a plan doc is closer to `StoryInfo.TasksDone/TasksTotal` than to `SprintStatus`.

## Acceptance Criteria

1. **Given** representative Superpowers-adopting repositories, **when** the Superpowers artifact set is surveyed against the shared adapter contract's `ArtifactBundle` and projection model, **then** a written coverage map classifies each Superpowers artifact type as mappable, partially-mappable, or unsupported, **and** the target shared-model projection is named for each mappable type.

2. **Given** Superpowers conventions that exceed the shared projection model or that SpecScribe will deliberately not support, **when** the spike documents its findings, **then** framework-extra data is recorded as candidate projection extensions or explicit non-goals, **and** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2521-2545`]

## Context & Scope

### The contract this spike maps against (read the real code, not just the epic prose)

- **`IArtifactAdapter`** [src/SpecScribe/IArtifactAdapter.cs:19-38] — two methods: `AppliesTo(ForgeOptions, sourceFiles)` (cheap self-selection sniff, never throws) and `Ingest(ForgeOptions, sourceFiles, ProgressProjection?)` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics` instead). **Read `AppliesTo`'s doc comment closely** [IArtifactAdapter.cs:21-27]: it explicitly allows the signal to be "a marker-directory/file sniff... or adapters whose signal is artifact shape rather than a marker directory" — Superpowers is exactly the case that second clause anticipates. Use it; don't invent a workaround.
- **`ArtifactBundle`** [src/SpecScribe/ArtifactBundle.cs:10-58] — the ONLY shape a new adapter must produce. Its fields, verbatim:
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
- **`AdapterDiagnostic(Category, RelativePath, Message)`** with `enum AdapterDiagnosticCategory` [src/SpecScribe/AdapterDiagnostic.cs:7-32] — `Category` is one of `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed). **These five categories are the entire non-fatal vocabulary this spike's "unsupported conventions" and "non-fatal notice they will emit" (AC #2) must map onto** — do not invent a sixth.
- **The one existing adapter to mirror, not copy verbatim:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344]. Read it end to end — it is the working example of "self-selection sniff → discover files by well-known name/location → parse each family independently → never let one family's failure kill the others → diagnose non-fatally." Its `AppliesTo` sniffs `_bmad/` at the repo root [BmadArtifactAdapter.cs:71-77] — Superpowers has no equivalent marker to sniff (see difference #1 above), so this spike cannot mirror that part of the pattern; it must propose an alternative.

### The load-bearing gap this spike must surface, not solve — and it is shared with 11.1 and 12.1

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:51] — with a comment stating plainly that *"the adapter registry that selects among `IArtifactAdapter` implementations arrives with Stories 4.3+."* Those stories are exactly the ones relocated into Epics 11–15, so **the registry has no owner today.** `ARCHITECTURE-SPINE.md` explicitly leaves this open: *"Exact adapter loading mechanics... are implementation seeds"* [`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:101`].

Both Story 11.1 and Story 12.1 already raise this exact same gap (their respective Task 5/Task 6). **Do not independently re-derive a competing registry design or propose a third ADR for it.** Instead: confirm the gap still exists (check whether either sibling has reached `done` with a landed conclusion — as of this writing both are `ready-for-dev`, not `done`), and record that it is a **shared prerequisite** across all five framework epics, closed once by whichever baseline-coverage story lands first. Recommend (not build) a minimal shape, and explicitly defer to 11.1's/12.1's conclusion if either reaches `done` first. Per this project's ADR-creation-trigger discipline ([[adr-creation-trigger-gap-epic-10-retro]]), if a genuine architecture fork is found, propose **one** shared registry ADR — do not duplicate a fork-specific ADR per spike.

**Superpowers adds a second, framework-specific candidate for an architecture-level decision:** if `AppliesTo` truly cannot rely on a marker directory (difference #1), decide whether that is (a) solvable within the existing `AppliesTo(options, sourceFiles)` signature via a content-shape heuristic on `sourceFiles`, or (b) a genuine contract limitation worth flagging as its own finding (not necessarily an ADR — judge whether it's a small adaptation of the existing seam or a real fork). Most likely (a): the signature already accepts `sourceFiles`, so a heuristic (e.g. "matches `docs/**/plans/*.md` with the `YYYY-MM-DD-slug.md` shape and the plan-header fields") is plausible without changing the contract — but state this explicitly rather than assuming.

### The "host-neutral" models are less framework-neutral than they sound

Read these before assuming any Superpowers artifact maps cleanly:

- **`EpicsModel`/`EpicInfo`/`StoryInfo`** [src/SpecScribe/EpicsModel.cs:1-40+] bake in BMad vocabulary directly into what the contract calls "host-neutral": `EpicStatus { Drafted, Pending }`, `EpicSection { VerticalSlice, FurtherDevelopment }` (a BMad epics.md convention), and `StoryInfo.Id` is hard-typed as BMad's `"N.M"` two-level numbering with a resolved `ArtifactOutputPath`/`ArtifactSourcePath` pointing at a discrete per-story markdown file. Superpowers has **no epic concept and no numbered story ID** (difference #3) — a plan document is closer in shape to a single `StoryInfo` (one file, `- [ ]` checkbox tasks, much like a BMad story's Tasks/Subtasks section) than to anything nested. Decide explicitly: does each plan file become a synthetic `StoryInfo` (with what `Id`? the date-slug string itself?), does the whole `docs/superpowers/plans/` folder become one synthetic `EpicInfo` containing all plans as its stories, or does neither fit well enough and this family should be `Epics = null`? State the decision and its consequences plainly; do not leave it ambiguous.
- **`RequirementsModel`/`RequirementInfo`** [src/SpecScribe/RequirementsModel.cs:1-102] is entirely BMad's `FR`/`NFR`/`UX-DR` numbered-requirements-with-a-Coverage-Map convention, parsed from epics.md's "Requirements Inventory" section. Superpowers has **no numbered-requirements convention** found in research — the closest thing is the brainstorming skill's upfront design-doc output (unnumbered prose, presented "in chunks"), which is not committed to a fixed file/path by the framework itself. Expect `Requirements = null` unless the spike finds a plausible substitute during real-repo verification.
- **`SprintStatus`** [src/SpecScribe/SprintStatus.cs:1-43] is literally `sprint-status.yaml`-shaped (a `development_status` map + `action_items`). Superpowers ships **no separate sprint/kanban/status file** (difference #4) — status lives as checkboxes inside each plan document itself. Expect `Sprint = null`, but verify: is the "whole-portfolio glance" role `SprintStatus` plays better served by rolling up checkbox completion across all plan files (an `Epics`/`StoryInfo` progress concern, not a `Sprint` one)? State which family (if any) absorbs that signal.
- **`RetroModel`** [src/SpecScribe/RetroModel.cs:1-20] is BMad's `epic-N-retro-*.md` convention. Superpowers has **no retrospective-note convention** found in research — expect `Retros = []` always, matching NFR8's "honest absence."

**Net implication to verify, not assume:** Superpowers coverage will likely land almost entirely on a repurposed slice of `Epics`/`StoryArtifactsById` (treating plan files as story-shaped, if that mapping is chosen at all), with `Sprint`/`Retros`/`Requirements` staying structurally absent — an even sparser bundle than Spec Kit's, because Superpowers additionally lacks any folder-per-feature grouping. Confirm or overturn this with real repos before writing the coverage map.

### Superpowers' real shape — freshly researched 2026-07-20 via `github.com/obra/superpowers`, treat as a starting hypothesis to confirm against real adopting repos

Live-checked (fetched `README`, `skills/writing-plans/SKILL.md`, `skills/using-superpowers/SKILL.md`, and the `docs/plans/` directory listing) during create-story — **the framework is under active development (examples dated Nov 2025–Jan 2026) and this table is built from doc fetches, not a cloned Superpowers-adopting project. Re-verify, don't trust this blindly:**

| Superpowers concept | Path / shape | Closest `ArtifactBundle` candidate | Notes |
|---|---|---|---|
| Framework install | Coding-agent plugin (`/plugin install`, per-harness) — **nothing copied into the target repo** | **No `AppliesTo` marker-directory analog** (difference #1) | The central open question this spike must resolve |
| Plan documents | `docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md` (default; user-overridable) | `StoryInfo`-shaped candidate: one file per plan, `- [ ]` checkbox tasks | No folder-per-feature grouping (flatter than even Spec Kit); path itself is a configurable convention, not a hard rule |
| Plan document header | H1 title + `Goal:` (1 sentence) + `Architecture:` (2-3 sentences) + `Tech Stack:` + `Global Constraints:` (copied verbatim from specs) | No 1:1 BMad analog — closest to a story's "As a / I want / so that" framing, richer | Candidate framework-extra or folded into a synthetic `StoryInfo` summary |
| Plan tasks | `Task 1`, `Task 2`, ... sequential numbered headings, each with **Files** (created/modified/test paths), **Interfaces** (consumed/produced signatures), **Steps** (`- [ ]` TDD checkboxes: test → verify fail → implement → verify pass → commit) | Closest analog to `StoryInfo.TasksDone`/`TasksTotal`, but richer (explicit file lists + interface contracts per task, no BMad equivalent) | Candidate framework-extra beyond simple task-count rollup |
| Brainstorming/design output | Presented interactively "in chunks short enough to actually read," precedes plan-writing — **no fixed committed file/path found in research** | Unclear — may not be a durable on-disk artifact at all | Verify against a real repo: does a design doc get saved anywhere, or does it live only in chat history? |
| Status tracking | Inline `- [ ]` checkboxes inside the plan document itself — no separate file | No `SprintStatus` analog (difference #4); possibly folds into per-"story" task-completion rollup instead | Verify whether any portfolio-level (multi-plan) status view exists |
| Sprint/kanban file | **None found** | `Sprint` stays null — not a gap, per NFR8 | Confirm against a real repo |
| Retrospective notes | **None found** | `Retros` stays empty — not a gap | Confirm against a real repo |
| Numbered FR/NFR requirements | **None found** | `Requirements` likely null | Confirm against a real repo |
| Skill files themselves (`skills/*/SKILL.md`) | Part of the Superpowers plugin, not the consuming repo's own artifacts | Out of scope — these are the agent's tooling, not the target project's planning docs, same as slash-command definitions are informational-only for Spec Kit | Do not attempt to ingest the plugin's own skill files as if they were project artifacts |

**Do not treat this table as ground truth.** No real, current Superpowers-adopting repository was inspected during create-story (only the framework's own repo, which is the tool's source, not an example of its output living in a consumer project). Obtain or construct a representative example (the `obra/superpowers` repo's own `docs/plans/` directory is the closest available real-world sample of plan-file shape, even though it documents Superpowers' own development rather than a downstream adopter) and confirm every row.

### Existing Superpowers surfaces already in the portal (placeholders — 15.2's eventual targets, not this spike's job to fill)

- **`AboutSddTemplater.cs`** carries a `Frameworks` roster including `("superpowers", "Superpowers", SiteNav.AboutSddSuperpowersOutputPath, false)` [AboutSddTemplater.cs:17], rendering a **support-matrix table across six nouns: Epics & Stories / Requirements / Sprint / Retros / Planning docs / Commands** [AboutSddTemplater.cs:93-121, row at 119], with a generic "Coming soon" body for unsupported frameworks [AboutSddTemplater.cs:224-231]. **This six-noun matrix is a ready-made, already-shipped vocabulary** — align the coverage map's artifact-type naming to it where it fits, but note plainly where Superpowers has genuinely nothing for a noun (e.g. likely "Commands" — Superpowers has skills, not slash-command-style next-step prompts the way BMad does) rather than forcing a fit.
- **`SiteNav.cs:71`** already defines `AboutSddSuperpowersOutputPath = "about-sdd-superpowers.html"` — the page route exists; content is a placeholder.
- **`README.md:24`** already lists `| Superpowers | — | 🧭 Planned |` in the "Supported frameworks" table (and line 177 groups it with Spec Kit/GSD/GSD-Pi as "planned framework support").
- **`ArtifactCoverage.cs:79-84`** has its own explicit comment: *"THIS list is the coverage seam Epic 4 generalizes — a future framework adapter swaps this family set, not the panel or the builder."* — a second, dashboard-level coverage concept (presence/freshness of a repo's OWN planning docs), distinct from this spike's artifact-classification coverage map. Don't conflate the two "coverage" senses when writing findings — name which one you mean each time.

**ADRs are a side-channel, not part of `ArtifactBundle` at all.** There is no dedicated ADR parser class and no `ArtifactBundle` field for them — `docs/adrs/*.md` are hand-authored and read via a separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside the `IArtifactAdapter` contract. Superpowers has no obvious ADR-shaped artifact in research (no `constitution.md`- or `DECISIONS.md`-equivalent found) — if real-repo verification turns one up, classify it explicitly (ADR side-channel / new field / out of scope) mirroring 11.1's `constitution.md` and 12.1's `DECISIONS.md` framing; otherwise state plainly that none was found.

**Coverage-map precedent.** Story 11.1's and 12.1's Completion Notes are the first two coverage maps written; this spike's should be the third and adopt the same shape (artifact-type × classification × target projection table + non-goals + shared registry-gap finding + baseline-story recommendation) so Epics 13–14 (and Epic 18's Story 18.1) can reuse it. **If 11.1 or 12.1 reach `done` before this spike is reviewed, read their Completion Notes and match structure**, especially their registry-gap wording.

### A citation caveat worth flagging to the dev, not silently "fixing"

`epics.md` attributes **FR17** to Epic 15 (and to Epics 13/14) [epics.md:58, 179, 257, 261, 265]: *"Add adapter coverage for additional spec-driven frameworks (for example SpecFlow, Squad, and Superpowers) through the shared adapter contract."* However, the canonical `requirements-catalog.md`'s own **FR-17** is a different, unrelated requirement — *"Read-only IDE helpers"* [requirements-catalog.md:31] — and that catalog's multi-framework capability (`CAP-1`) only explicitly enumerates `FR-1`–`FR-4` [requirements-catalog.md:7]. The closest catalog entry that actually matches Epic 15's intent is **FR-19 "Popular-framework coverage policy"** [requirements-catalog.md:33]: *"First-class support targets currently popular spec-driven frameworks/modules, with explicit published criteria for what counts as popular."* This is a numbering drift between the two planning documents, not something this spike should silently "correct" in either file — record the observation in Completion Notes so a maintainer can reconcile it later (likely `epics.md`'s FR17 references should read FR19, or `requirements-catalog.md` needs a proper FR-17-adjacent entry for per-framework coverage — either way, out of scope to fix here).

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building `SuperpowersArtifactAdapter`** or any parser — that's 15.2.
- **Designing the adapter registry** — name the shared gap, coordinate with 11.1/12.1, don't design or implement it here.
- **Extending `ArtifactBundle`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Writing an ADR unless a genuine architecture fork is found** — and if one is, it is ONE shared registry ADR (or a separate, narrowly-scoped `AppliesTo`-without-a-marker-directory finding if that turns out to be a real contract gap, not just a heuristic), coordinated with the siblings, not decided silently.
- **A new authoring schema** for Superpowers content — SpecScribe reads Superpowers' own existing conventions as-is; it never asks Superpowers users to add SpecScribe-specific files.
- **Ingesting the Superpowers plugin's own skill files** (`skills/*/SKILL.md`) as if they were the target project's planning artifacts — they are the agent's tooling, not the project's own docs.

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the contract shapes against live code (AC: #1)**
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables; they are a starting point, not a substitute for reading the code.
  - [ ] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:51`), and check whether Story 11.1 or 12.1 has already landed/recommended a registry shape (read their Completion Notes if `done`).
  - [ ] Re-read `IArtifactAdapter.AppliesTo`'s doc comment [IArtifactAdapter.cs:21-27] closely — confirm it genuinely permits a `sourceFiles`-shape-based signal (not just a marker-directory sniff) before assuming a contract change is needed.

- [ ] **Task 2 — Obtain and inspect representative Superpowers-adopting repositories (AC: #1, #2)**
  - [ ] Fetch/inspect the `obra/superpowers` repo's own `docs/plans/` directory (real dated plan-file examples) and the `writing-plans`/`using-superpowers`/`brainstorming` skill files to confirm exact plan-document structure — this story's table is a hypothesis from doc-page fetches, not a downstream-repo inspection.
  - [ ] Actively search for (or construct, if none is publicly findable) a real project that has *adopted* Superpowers as a coding-agent plugin and therefore has its own `docs/superpowers/plans/` (or user-configured equivalent) — distinguishing "the tool's own repo" from "a project that used the tool" matters here more than for Spec Kit/GSD, since Superpowers leaves so little else behind.
  - [ ] Confirm whether a brainstorming-phase design document is ever committed to disk, and if so, where and in what shape.
  - [ ] Confirm whether any multi-plan/portfolio-level status view exists anywhere in a real adopting repo (difference #4).

- [ ] **Task 3 — Resolve the `AppliesTo` self-selection problem (AC: #1)**
  - [ ] State explicitly how a `SuperpowersArtifactAdapter.AppliesTo` would work given there is no marker directory — propose the concrete heuristic (e.g. path-glob + filename-shape + header-field sniff on `sourceFiles`) or conclude that reliable self-selection isn't feasible and document the consequence (e.g. requiring explicit opt-in configuration rather than auto-detection).
  - [ ] Assess false-positive risk (a non-Superpowers repo that happens to have dated markdown files under a `docs/plans/`-shaped path) and false-negative risk (a genuine Superpowers repo using a user-overridden plan location) and state which risk this spike judges more important to guard against, with rationale.

- [ ] **Task 4 — Classify every discovered artifact type (AC: #1)**
  - [ ] For each Superpowers artifact type (plan document as a whole, its header fields, its per-task Files/Interfaces/Steps sections, any brainstorming-phase design doc if found), classify as **mappable** (name the exact target: `ArtifactBundle` field + model type/record), **partially-mappable** (name what maps and what doesn't), or **unsupported** (name why).
  - [ ] Resolve explicitly whether a Superpowers plan document maps to `StoryInfo`, to a synthetic `EpicInfo`, to neither, or needs a new shape — this is the central modeling question (see difference #3 and the "host-neutral models" section above); do not leave it ambiguous.
  - [ ] State explicitly, confirmed against research (and real-repo verification where possible): is `Sprint` null (checkbox-in-plan status doesn't map to a separate `SprintStatus`), is `Retros` empty, is `Requirements` null — per NFR8, absence here is expected to be even more pervasive than for Spec Kit, but confirm rather than assume.

- [ ] **Task 5 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any Superpowers convention richer than the shared model (e.g. per-task Files/Interfaces sections, the Goal/Architecture/Tech Stack/Global Constraints plan header, TDD-step-level checkbox granularity), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale).
  - [ ] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188, 219-224, 262-276] for tone/specificity.

- [ ] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1 and 12.1 (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim against `SiteGenerator.cs`. State plainly that Story 15.2 cannot wire in another adapter without SOME selection mechanism, that this gap is **shared across all five framework epics** (not Superpowers-specific), and that whichever coverage story lands first closes it once for all frameworks.
  - [ ] Recommend (not build) a minimal registry shape, and defer to 11.1's/12.1's conclusion/ADR if either exists by the time this spike is reviewed. Do NOT propose a fourth, Superpowers-specific registry ADR.
  - [ ] Separately record the Task 3 `AppliesTo`-without-a-marker-directory finding — decide whether it's a small heuristic within the existing contract (most likely) or a genuine fork worth its own narrowly-scoped ADR proposal, and say which.

- [ ] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (artifact-type × classification × target projection table + `AppliesTo` self-selection decision + non-goals + shared registry-gap finding + FR-numbering citation caveat + 15.2 recommendation) into this story's **Completion Notes**, mirroring Story 11.1's and 12.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND neither sibling has already covered it — coordinate to keep it a single shared registry ADR (plus, if warranted, one narrowly-scoped `AppliesTo`-heuristic finding, not a full ADR, unless Task 3 concludes it's genuinely architecture-level).

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live repo/doc inspection, not code.** Evidence comes from reading `src/SpecScribe/*.cs` and reading/fetching the Superpowers framework's own repo plus (ideally) a real adopting project — not from writing a prototype adapter. If you catch yourself scaffolding `SuperpowersArtifactAdapter.cs`, stop; that's 15.2.
- **No new authoring schema.** SpecScribe never asks Superpowers users to add SpecScribe-specific files — coverage must derive from Superpowers' own existing conventions (the `docs/superpowers/plans/` default, or whatever a real repo actually uses).
- **Verify, don't assume — and verify the framework's own repo is not the same thing as a downstream adopter.** This story's "real shape" table was built from `obra/superpowers`'s own docs/skills/example-plans, not from a project that installed Superpowers as a plugin and used it. That distinction matters more here than for Spec Kit or GSD, both of which leave installable scaffolding directly in the consuming repo.
- **NFR8.** Genuinely-absent artifact families (`Sprint`, `Retros`, `Requirements`, and possibly `Epics` itself if no synthetic mapping is chosen) are honest absence, not gaps to fill. Don't recommend inventing conventions Superpowers lacks.
- **Coverage tiers are NOT mandated by this story's own AC text** (unlike Epic 12's Story 12.1, whose AC #1 explicitly requires a declared tier). Story 15.1's ACs mirror 11.1's language exactly — "mappable, partially-mappable, or unsupported" plus a named target projection, no tier requirement. Adopt tier vocabulary only if it genuinely clarifies a finding; don't force it in to match 12.1.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future Superpowers adapter only translates into `ArtifactBundle`, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps Superpowers source shapes onto that exact contract, nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future Superpowers-specific insight enrichment must stay additive/non-blocking, same as BMad's git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."*
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — do not let this spike commit to `src/SpecScribe.Adapters.Superpowers` as a real package (the project is still single-namespace, single-project — [[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- Assuming Superpowers must have a marker-directory `AppliesTo` signal because every prior framework did — it genuinely doesn't (difference #1), and papering over that with a false-confidence marker sniff would misclassify repos.
- Treating `obra/superpowers`'s own repository (the tool's source) as equivalent to a downstream project that adopted the tool — they are different things, and this story's research necessarily leaned on the former.
- Silently "fixing" the FR17 vs FR-17/FR-19 citation drift between `epics.md` and `requirements-catalog.md` instead of recording it as a finding for a maintainer to reconcile.
- Forcing a coverage-tier vocabulary into this spike's findings because 12.1 used one — this story's AC text doesn't require it.
- Designing or partially building the adapter registry inline instead of naming it as a Task 6 finding shared with 11.1/12.1.
- Silently committing to a package/namespace split (`SpecScribe.Adapters.Superpowers`) — aspirational sketch, not current architecture.
- Proposing a fourth, Superpowers-specific adapter-registry ADR instead of coordinating one shared registry decision with 11.1/12.1.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/15-1-superpowers-integration-spike.md`
- Sprint key: `15-1-superpowers-integration-spike`
- Downstream story key (not created by this spike): `15-2-superpowers-baseline-adapter-coverage`
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 6 concludes a genuine architecture fork (registry design, or possibly the `AppliesTo`-without-a-marker-directory question) not already covered by 11.1/12.1 — if so, it is ONE shared registry ADR (`docs/adrs/`, next number, indexed in `docs/adrs/README.md`), coordinated with the siblings, escalated rather than decided silently.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:58,179,257,261,263-265,2521-2545`] — FR17 line, Epic 4/11-15 goal lines, Epic 15 intro, Story 15.1 (verbatim ACs quoted above) + Story 15.2 (downstream coverage story, its ACs quoted for scope-boundary context).
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/specs/spec-specscribe/requirements-catalog.md:7,31,33`] — CAP-1's actual FR-1..FR-4 enumeration, FR-17 "Read-only IDE helpers" (unrelated to Epic 15 despite the shared number), FR-19 "Popular-framework coverage policy" (the likely intended requirement) — the citation-drift finding.
- [Source: `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`] — why Epics 11-15 exist, the fixed framework→epic mapping (15 = Superpowers), the "X.1 spike, X.2 coverage" pattern, the spike AC template.
- [Source: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`] — the closest sibling spike (single-framework, feature-folder-vs-epic/story modeling puzzle); mirror its structure and Completion-Notes-as-deliverable convention.
- [Source: `_bmad-output/implementation-artifacts/12-1-gsd-and-gsd-pi-integration-spike.md`] — the second sibling spike; shows how a framework-specific structural surprise (there: DB-vs-markdown authority; here: no-marker-directory `AppliesTo`) gets surfaced as a first-class finding.
- [Source: `src/SpecScribe/IArtifactAdapter.cs:19-38`] — the contract, especially `AppliesTo`'s doc comment permitting a `sourceFiles`-shape signal, not only a marker-directory sniff.
- [Source: `src/SpecScribe/ArtifactBundle.cs:10-58`, `AdapterDiagnostic.cs:7-43`, `BmadArtifactAdapter.cs:11-344`] — the projection carrier, diagnostic vocabulary, and the one reference implementation (line anchors verified against current main at create-story).
- [Source: `src/SpecScribe/EpicsModel.cs:1-40+`, `RequirementsModel.cs:1-102`, `RetroModel.cs:1-20`, `SprintStatus.cs:1-43`] — the "host-neutral" model shapes, BMad-specific vocabulary baked in.
- [Source: `src/SpecScribe/SiteGenerator.cs:47-51`] — the hardcoded single-adapter field; the shared registry gap.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18,93-121,224-231`] — the framework roster (`superpowers` `Supported: false` at line 17/119), the six-noun support matrix, and the "Coming soon" placeholder body.
- [Source: `src/SpecScribe/SiteNav.cs:71`] — the already-routed `about-sdd-superpowers.html` page route (placeholder content); `README.md:24,177` — "Supported frameworks" table, Superpowers listed "🧭 Planned."
- [Source: `src/SpecScribe/ArtifactCoverage.cs:79-84`] — the dashboard-level "coverage" concept (repo's own doc freshness), a different sense from this spike's artifact-classification coverage map; don't conflate.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1 (34-40) / AD-2 (42-48) / AD-4 (58-64), Seed-not-invariant (98-102)] — the invariants this spike must respect and the open seeds it must not over-commit.
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md`] — the older pure-tracing spike; Completion-Notes-as-deliverable convention.
- [Web: `github.com/obra/superpowers` README, `skills/writing-plans/SKILL.md`, `skills/using-superpowers/SKILL.md`, `docs/plans/` directory listing, fetched live 2026-07-20] — plugin-not-installed-in-target-repo adoption model, `docs/superpowers/plans/YYYY-MM-DD-<feature-name>.md` plan convention (user-overridable), plan-header fields, per-task Files/Interfaces/Steps structure, inline checkbox status tracking, no sprint/retro/requirements-numbering convention found. Treat as a hypothesis to reconfirm against a real adopting repo, not settled fact — the fetched material documents the tool's own repository, not a downstream project's use of it.
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — but ONE shared registry ADR, coordinated with 11.1/12.1, not per-spike).

### Git intelligence summary

No Superpowers code, docs, or prior exploration exist anywhere in this repo beyond the placeholder About-SDD page/roster entries and the README's "Planned" row (confirmed via grep across `src/`, `docs/`, `epics.md`) — this spike starts from a clean slate on the Superpowers side, with a well-established adapter contract and one working reference implementation (`BmadArtifactAdapter`) on the SpecScribe side. Stories 11.1 (Spec Kit) and 12.1 (GSD/GSD-Pi) are the immediately-preceding sibling spikes (both `ready-for-dev`, neither `done` yet) and the closest structural templates; their registry-gap findings are shared prerequisites, not independently re-derivable here. Recent commits (Epic 10 cleanup, retro work, 11.1/12.1 create-story) are unrelated to this story's implementation scope.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-20 — Story 15.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + `AppliesTo` self-selection decision + FR-numbering citation caveat + 15.2 scope recommendation; no production code. Fourth story of the five per-framework spike-led epics (11–15) to be created, run out of numeric order ahead of 13/14; structurally the most divergent framework surveyed so far (no per-repo marker, no epic/story ID scheme, status tracked inline).
