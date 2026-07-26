---
baseline_commit: 611097d63ff1f8fa6b71c8d58158dd6e303e6991
---

# Story 18.1: BMad Module Landscape and Coverage Spike

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support BMad modules beyond BMM,
I want the BMad module/expansion ecosystem inventoried and each module's distinctive artifacts mapped against the shared adapter contract before any coverage work begins,
so that baseline coverage starts with a defined scope, a prioritized target module, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 were extracted 2026-07-10 into five appended, spike-led epics (11–15; one per **third-party** framework: Spec Kit, GSD/GSD-Pi, SpecFlow, Squad, Superpowers). **Epic 18 is a sixth, distinct exploration seated separately** (SCP 2026-07-11, correct-course): BMad's own **module and expansion ecosystem** beyond the BMM core that this very project already runs on. This is NOT another third-party-framework spike — it is asking "what else does the framework SpecScribe is built with/on top of ship, that SpecScribe doesn't yet render?"

**The one-line test for "is this in scope?":** if the change *surveys* BMad's own module ecosystem (BMad Builder, Test Architect, Creative Intelligence Suite, and confirms Game Dev Studio's already-supported status), *classifies* each module's distinctive observable artifacts against the existing `ArtifactBundle`/model shapes, or *writes* a coverage map + priority recommendation + non-goals list → in. If it *builds* a new adapter, parses a real module-specific file into a real model, extends `ModuleContext`/`BmadModule` with a new enum case, or lands any `src/`/`tests/` change → out; that is Story 18.2 (Priority BMad Module Baseline Coverage, not yet created — already seeded in epics.md but not detailed).

**Precedent for this shape — read all three, but do not just copy their answers:** Story 11.1 (`11-1-spec-kit-integration-spike.md`, `ready-for-dev`), Story 15.1 (`15-1-superpowers-integration-spike.md`, `ready-for-dev`) and Story 19.1 (`19-1-work-graph-model-and-coverage-spike.md`, `ready-for-dev`) are the closest siblings for the "coverage map, no production code" spike shape and its Completion-Notes-as-deliverable convention. **But this story is structurally different from all of them**: 11.1–15.1 each survey ONE third-party framework that is entirely absent from this repo's own tooling. Story 18.1 surveys BMad's OWN module family — and this repo already has two of BMad's modules installed (`core`, `bmm` — see `_bmad/_config/manifest.yaml`) and already fully supports two BMad modules in the rendered site (BMad Method and BMad GDS, both `Supported: true` in `AboutSddTemplater.Frameworks` — see Context & Scope below). The spike's job is to find and classify the modules NOT yet in that supported set.

## What's different here vs. a third-party-framework spike (do not just copy 11.1/15.1's answers)

1. **Two BMad modules are already fully supported — this spike surveys what's left, not a first framework encounter.** `AboutSddTemplater.Frameworks` [AboutSddTemplater.cs:10-18] already lists `("bmad", "BMad", ..., Supported: true)` and `("gds", "BMad GDS", ..., Supported: true)`. `README.md:19-20` confirms both ship today. Confirm this is still accurate at dev time, then focus the survey on the BMad-native modules that are NOT yet in that list.
2. **Module detection is already meaningfully generic, more so than any third-party framework spiked so far.** `ModuleContext.Detect` [ModuleContext.cs:194-245] reads the installed-module registry (`_bmad/_config/manifest.yaml`, parsed by `ReadInstalledModules` [ModuleContext.cs:247-257]) and falls back to scanning for any `module-help.csv` on disk [ModuleContext.cs:210-218] — it does NOT hardcode a `_bmad/bmm/` or `_bmad/gds/` path the way `BmadArtifactAdapter.AppliesTo` hardcodes `_bmad/` for BMad-as-a-whole [BmadArtifactAdapter.cs:76-77]. A new BMad module (e.g. `bmb`, `cis`, `tea`) already produces a `CommandCatalog` with real slash commands via `BuildContext` [ModuleContext.cs:291-357] parsing its `module-help.csv` — **without any code change** — IF that module's `module-help.csv` follows the existing CSV shape (`module`, `skill` columns). This is a load-bearing finding: some of "detection" is already solved generically; what's NOT generic is `BmadModule` itself being a closed enum with exactly two cases (`Unknown`, `BmadMethod`, `GameDevStudio` — [ModuleContext.cs:8]) and `WellKnownDocs`/`BmadMethodDocs`/`GameDevStudioDocs`/glossaries being hardcoded per-case switches [ModuleContext.cs:101-156]. Confirm precisely where the generic seam ends and the two-case hardcoding begins — do not assume either "it's all generic" or "none of it is."
3. **This repo's own skill roster is evidence, and possibly misleading evidence — read it carefully.** This project's own `.claude/skills/` roster includes skills whose names *sound* like they could belong to a Creative Intelligence Suite-style module (`bmad-brainstorming`, `bmad-forge-idea`, `bmad-prfaq`, `bmad-party-mode`, `bmad-domain-research`, `bmad-market-research`) — but `_bmad/_config/manifest.yaml` lists only `core` and `bmm` as installed modules, no `cis`. **Confirm whether these are BMM's own built-in ideation skills (bundled under the `bmm` module you already parse) or evidence that CIS conventions have already partially merged into BMM** — this distinction matters because if BMM already absorbed CIS-shaped functionality, a dedicated CIS module adapter might have less unique surface than web research suggests. Don't assume; check `_bmad/bmm/` on disk for where these skill files actually live.
4. **The candidate module list itself needs live verification, not just this story's hypothesis table.** The table below was built from web research (bmad-code-org/BMAD-METHOD README + DeepWiki + module doc sites), not from installing any of these modules into a real repo — treat it as a starting hypothesis, same caveat 15.1 gave its Superpowers table.

## Acceptance Criteria

1.
**Given** BMad's module and expansion ecosystem beyond the BMM core (for example BMad Builder, Creative Intelligence Suite, and game-dev/GDS-style expansions)
**When** the spike inventories it and surveys each module's artifact set against the shared adapter contract's `ArtifactBundle` and projection model
**Then** a written coverage map classifies each module's distinctive artifact types as mappable, partially-mappable, or unsupported (noting which are already covered by the existing BMM parsing), names the target shared-model projection for each mappable type, and recommends a priority module (or modules) to cover first
**And** the survey distinguishes BMad-native modules from the third-party frameworks already scoped by Epics 11–15.

2.
**Given** module conventions that exceed the shared projection model or that SpecScribe will deliberately not support
**When** the spike documents its findings
**Then** framework/module-extra data is recorded as candidate projection extensions or explicit non-goals, and deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit
**And** the current BMM-specific next-step-command mapping is assessed for generalization to other modules (per the "strongly GDS-oriented … requires generalization" note in Additional Requirements), giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2888-2913`]

## Context & Scope

### The contract this spike maps against (read the real code, not just this story's summary)

- **`IArtifactAdapter`** [src/SpecScribe/IArtifactAdapter.cs:19-38] — two methods: `AppliesTo(ForgeOptions, sourceFiles)` (cheap self-selection sniff, never throws) and `Ingest(ForgeOptions, sourceFiles, ProgressProjection?)` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics` instead).
- **`ArtifactBundle`** [src/SpecScribe/ArtifactBundle.cs:10-58] — the ONLY shape any adapter must produce:

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

- **`AdapterDiagnostic(Category, RelativePath, Message)`** with `enum AdapterDiagnosticCategory` [src/SpecScribe/AdapterDiagnostic.cs:7-32] — `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed). **This five-value vocabulary is the entire non-fatal notice set AC #2 must map onto** — do not invent a sixth.
- **The one existing adapter, and it is BMad's own:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344]. Unlike every third-party spike (11.1–15.1), this spike's target framework **is the same framework this adapter already implements** — the question isn't "how would we adapt BMad's conventions to the contract" (already answered) but "which of BMad's OWN modules besides the two already covered does this adapter (or a sibling) need to also parse."
- **`ModuleContext`** [src/SpecScribe/ModuleContext.cs:1-425] — the class actually doing today's BMad-module-family detection work. Read it in full; it is this spike's primary object of study:
  - `BmadModule` enum: `Unknown`, `BmadMethod`, `GameDevStudio` — only two real modules known today [ModuleContext.cs:8].
  - `ModuleContext.Detect` [ModuleContext.cs:194-245] reads `_bmad/_config/manifest.yaml` (`ReadInstalledModules`, [ModuleContext.cs:247-257]) for installed module names, falls back to any on-disk `module-help.csv` [ModuleContext.cs:210-218], and uses source-artifact shape only to break ties when multiple modules are installed (`ChoosePrimary`, [ModuleContext.cs:259-286], keying on `gdds/`/`gdd.md`/etc. path hints).
  - `BuildContext` [ModuleContext.cs:291-357] parses a module's `module-help.csv` (`module`, `skill` columns) into a `CommandCatalog` **generically** — this already works for any module whose CSV matches the shape, independent of the `BmadModule` enum.
  - `WellKnownDocs`/`DocsFor`/`GlossaryFor` [ModuleContext.cs:85-156] are the **hardcoded, per-module-enum-case** parts: `BmadMethodDocs` and `GameDevStudioDocs` are separate static arrays; adding a third module means adding a third `BmadModule` case + a third array + a third glossary + a third switch arm each in three places. This spike must name this exact seam as the extension point (or candidate projection extension) for whichever module(s) get prioritized.
  - `IsMethodPresent`/`IsGdsPresent` [ModuleContext.cs:162-170] are independent multi-install presence checks (a repo can have BOTH BMM and GDS installed simultaneously) used by `AboutSddTemplater`'s support matrix — note this dual-presence pattern as the shape any new module's presence check should probably follow.

### Where BMad Method + GDS support already lives (already covered — do not re-survey these)

- `AboutSddTemplater.Frameworks` [AboutSddTemplater.cs:10-18] — `bmad` and `gds` both `Supported: true` today; four more rows (`speckit`, `gsd`, `gsd-pi`, `superpowers`) are `false` placeholders for Epics 11-15's targets, NOT this epic's targets. Don't confuse the two lists.
- `README.md:19-24` — support table; BMad Method 6.10.0 and BMad GDS 0.6.0 both ✅ Supported; the four third-party frameworks 🧭 Planned.
- This project's own `_bmad/_config/manifest.yaml` (repo root) proves `bmm` + `core` are installed here — a live, in-repo example of BMad Method's own conventions, but **not** an example of any of the modules this spike needs to survey (no `bmb`/`cis`/`tea`/`gds` folder exists under this repo's own `_bmad/`).

### Candidate module landscape (hypothesis — confirm before writing the coverage map)

Built 2026-07-21 from `github.com/bmad-code-org/BMAD-METHOD`'s README plus module-specific doc sites (DeepWiki, `cis-docs.bmad-method.org`, `bmad-builder-docs.bmad-method.org`) fetched live during create-story — **not verified against a real repo that has actually installed and used any of these modules.** Re-verify, don't trust this blindly, exactly as 15.1 flagged for its Superpowers table:

| Module | Purpose (per README) | Install marker (hypothesis) | Distinctive artifacts (hypothesis) | Closest `ArtifactBundle` candidate |
|---|---|---|---|---|
| **BMad Builder (BMB)** | "Create custom BMad agents and workflows" — the meta-tool that generates new agents/skills/modules | `_bmad/bmb/module-help.csv` (same generic shape `ModuleContext` already reads) + a `module.yaml` (identity/config) per generated module | Generated `*.agent.yaml` source files, `module.yaml`, generated `module-help.csv` for the module-under-construction, workflow `.md`/`.yaml` definitions | Unclear — this module produces OTHER modules' scaffolding, not planning/tracking artifacts in the BMM sense; may be entirely out of `ArtifactBundle`'s scope (a meta-tool, not a project-tracking source) — confirm or reject this framing explicitly, don't assume it maps to `Epics`/`Sprint` |
| **Test Architect (TEA)** | "Risk-based test strategy and automation" | `_bmad/tea/module-help.csv` | Test-strategy documents, risk assessments; this project's own `bmad-create-story` skill already references it ("Optional: If Test Architect module installed, run `/bmad:tea:automate`") — evidence TEA is a recognized peer module in this project's own tooling despite not being installed here | Unclear — likely closer to a QA/testing artifact family with no current `ArtifactBundle` field; confirm whether it's a candidate extension or a clean non-goal |
| **Creative Intelligence Suite (CIS)** | "Innovation, brainstorming, design thinking" — ideation/design-thinking workflows (SCAMPER, reverse brainstorming, empathy/journey maps) | `_bmad/cis/module-help.csv` | Session artifacts at a documented default path shape `{output_folder}/analysis/brainstorming-session-{date}.md`-style dated session files; a `design-methods.csv` reference dataset | Closest candidate: a `RetroModel`-like or new dated-note shape — but confirm this project's own `bmad-brainstorming`/`bmad-forge-idea`/`bmad-prfaq` skills aren't already BMM-native equivalents before concluding CIS is wholly novel surface (see difference #3 above) |
| **Game Dev Studio (BMGD/GDS)** | Unity/Unreal/Godot game development | `_bmad/gds/module-help.csv` (already the case) | `gdd.md`, `narrative-design.md`, `game-architecture.md` | **Already supported** — `BmadModule.GameDevStudio`, `GameDevStudioDocs`, `GameDevStudioGlossary` [ModuleContext.cs:110-115,142-147] — confirm current, don't re-survey as if new |

**Do not treat this table as ground truth.** No real repo with BMB, TEA, or CIS actually installed was inspected — only the tool's own README/doc sites (the same "tool's repo vs. downstream adopter" caveat 15.1 raised for Superpowers applies with equal force here, arguably more so since even the *module's own* repo may not exist as a standalone example — CIS and BMB ship as expansion packs inside/alongside the main `bmad-code-org/BMAD-METHOD` distribution).

### The load-bearing gap this spike must surface, not solve — and it is shared with 11.1–15.1

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:51] — with a comment stating the registry that selects among `IArtifactAdapter` implementations "arrives with Stories 4.3+" (now relocated into Epics 11-15). **This is somewhat less load-bearing for Epic 18 than for the third-party spikes**, because a new BMad module most likely does NOT need a whole new `IArtifactAdapter` implementation — it is plausibly just an extension of the existing `BmadArtifactAdapter`/`ModuleContext` (new `BmadModule` enum case + new doc/glossary arrays), since `AppliesTo`'s marker is `_bmad/` as a whole [BmadArtifactAdapter.cs:76-77], not per-sub-module. **State this explicitly as a finding**: does Epic 18's coverage story extend the existing adapter, or does it also need the registry? This is the single most important architectural question this spike must answer, and it may have a different answer than 11.1-15.1's (which all assume a brand-new adapter per framework).

Confirm whether Story 11.1/12.1/13.1/14.1/15.1 have reached `done` with a landed registry conclusion (all five are `ready-for-dev` as of this writing, none `done`) before writing this finding — if one has landed a conclusion, defer to it rather than re-deriving. Per this project's ADR-creation-trigger discipline ([[adr-creation-trigger-gap-epic-10-retro]]), if a genuine architecture fork is found (e.g. "new module = new adapter" turns out false and something else is needed), propose it as a shared concern coordinated with the other five spikes — do not write a competing ADR.

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Adding a new `BmadModule` enum case, or a new `IArtifactAdapter`** — that's Story 18.2.
- **Parsing any real BMB/TEA/CIS artifact file** — the spike documents the target shape; it does not write a parser.
- **Extending `ArtifactBundle`/`ModuleContext`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Writing an ADR unless a genuine architecture fork is found** — coordinate with 11.1-15.1's shared registry finding rather than writing a sixth, competing one.
- **Re-surveying BMad Method or BMad GDS** — both already fully supported; confirm their current state, don't re-derive their coverage from scratch.
- **A new authoring schema** for any BMad module — SpecScribe reads each module's own existing conventions as-is.

## Tasks / Subtasks

- [x] **Task 1 — Confirm the contract and existing BMad-module machinery against live code (AC: #1)**
  - [x] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `ModuleContext.cs` (all 425 lines — this is the primary object of study), `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`, `AboutSddTemplater.cs`, `ArtifactCoverage.cs` in full — do not rely solely on this story's summary tables.
  - [x] Confirm `AboutSddTemplater.Frameworks` [AboutSddTemplater.cs:10-18] and `README.md:19-24` still show BMad Method + BMad GDS both `Supported: true`/✅ — these are NOT this spike's targets.
  - [x] Confirm (or correct) this story's claim that no cross-framework adapter registry exists (`SiteGenerator.cs:51`) and check whether any of Stories 11.1-15.1 has landed a registry conclusion (read their Completion Notes if `done`).
  - [x] Precisely map where `ModuleContext`'s generic module-detection machinery (manifest/`module-help.csv` reading, `CommandCatalog`) ends and the two-case (`BmadMethod`/`GameDevStudio`) hardcoding (`WellKnownDocs`, glossaries) begins — this is the central architectural finding of Task 3/4.

- [x] **Task 2 — Obtain and inspect representative BMad-module documentation/examples (AC: #1, #2)**
  - [x] Fetch/inspect `github.com/bmad-code-org/BMAD-METHOD`'s README and module list to confirm the current roster of modules beyond BMM/GDS (this story's hypothesis: BMad Builder, Test Architect, Creative Intelligence Suite — reconfirm names/scope, the ecosystem moves fast).
  - [x] For each candidate module, fetch its dedicated docs (e.g. `cis-docs.bmad-method.org`, `bmad-builder-docs.bmad-method.org`, or DeepWiki pages) to find concrete artifact shapes: file naming, default output paths, whether a `module-help.csv`/`module.yaml` pair exists per the generic pattern `ModuleContext` already expects.
  - [x] Actively search for (or note the absence of) a real, downstream project that has installed one of these modules and produced real on-disk artifacts — distinguish "the framework's own docs/demo" from "a project that used the module," same caution 15.1 applied to Superpowers.
  - [x] Resolve the Difference #3 puzzle: check `_bmad/bmm/` in this repo (or BMad Method's own docs) to determine whether `bmad-brainstorming`/`bmad-forge-idea`/`bmad-prfaq`/`bmad-party-mode`/`bmad-domain-research`/`bmad-market-research` are BMM-native skills or CIS-overlapping functionality, before concluding what CIS uniquely adds.

- [x] **Task 3 — Answer the "extend vs. new adapter" question (AC: #1)**
  - [x] State explicitly whether covering a new BMad module (e.g. CIS) means (a) extending `BmadArtifactAdapter`/`ModuleContext` with a new `BmadModule` case + doc/glossary arrays (most likely, since `AppliesTo`'s `_bmad/` marker already covers any BMad module), or (b) something registry-shaped is still needed — and why.
  - [x] If (a), name the exact extension points: new `BmadModule` enum value, new `ModuleDoc[]` array, new `GlossaryTerm[]` array, new switch arms in `DocsFor`/`GlossaryFor` [ModuleContext.cs:118-123,151-156], and (if the module can coexist with BMM/GDS in one repo) a new `IsXPresent` helper mirroring `IsMethodPresent`/`IsGdsPresent` [ModuleContext.cs:162-170].
  - [x] Assess whether `ChoosePrimary`'s tie-breaking heuristic [ModuleContext.cs:259-286] needs a third branch for the prioritized module, or whether it's fine for a niche module to lose ties to BMM/GDS by default.

- [x] **Task 4 — Classify every discovered artifact type per candidate module (AC: #1)**
  - [x] For each of BMad Builder, Test Architect, and Creative Intelligence Suite (plus any additional module found in Task 2), classify its distinctive artifacts as **mappable** (name the exact target: `ArtifactBundle` field + model type, or the `ModuleContext` doc/glossary extension point), **partially-mappable** (name what maps and what doesn't), or **unsupported** (name why) — noting explicitly that BMad Method and GDS are already covered, not re-classified.
  - [x] Resolve whether any candidate module's output is close enough to `EpicsModel`/`RequirementsModel`/`SprintStatus`/`RetroModel` shape to reuse those models directly, or whether it's better modeled as pure `ModuleContext` doc/glossary/command additions with `Epics`/`Sprint`/`Requirements`/`Retros` staying null/empty (per NFR8, honest absence) — this is the central modeling question, mirroring 15.1's "does this map to StoryInfo, EpicInfo, or neither" puzzle but for BMad-native modules.
  - [x] Recommend the single priority module to cover first (AC #1's "recommends a priority module (or modules)") with rationale (e.g. likelihood of real adoption, richness of distinctive on-disk artifacts, size of Story 18.2's resulting scope).

- [x] **Task 5 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [x] For any candidate-module convention richer than the shared model (e.g. BMad Builder's generated `agent.yaml`/`module.yaml` scaffolding, CIS's `design-methods.csv` reference dataset), record it as either a candidate projection extension or an explicit non-goal with rationale.
  - [x] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188,219-224,262-276] for tone/specificity.
  - [x] Assess the current BMM-specific next-step-command mapping for generalization to other BMad modules (AC #2's second clause): confirm whether `CommandCatalog`/`BuildContext` [ModuleContext.cs:26-59,291-357] is already module-neutral (it parses `module-help.csv` generically today, keyed by `skill`/`module` columns, not hardcoded to BMM) or whether some caller still assumes BMM/GDS-only vocabulary — check `HowToReadTemplater`/`AboutSddTemplater`'s "Next Steps" panels and the "strongly GDS-oriented … requires generalization" phrasing this AC quotes from the epic's Additional Requirements (search `epics.md`/`architecture.md` for that exact phrase to find its origin before answering).

- [x] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1-15.1 (AC: #1, #2)**
  - [x] Confirm the registry-gap claim against `SiteGenerator.cs:51`. State whether Epic 18 actually needs the registry (per Task 3's conclusion) or can proceed via extension alone — this may make Epic 18 the one framework epic that does NOT depend on the registry landing first, which is itself worth stating plainly.
  - [x] If a registry is still needed for any part of Epic 18, defer to 11.1-15.1's conclusion/ADR if one exists by the time this spike is reviewed; do not propose a competing registry ADR.

- [x] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [x] Write the coverage map (candidate-module table × classification × target projection/extension point + extend-vs-registry decision + non-goals + command-generalization assessment + priority recommendation + 18.2 scope boundary) into this story's **Completion Notes**, mirroring Story 11.1's/12.1's/15.1's convention.
  - [x] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND none of the sibling spikes already covers it.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live doc/web research, not code.** Evidence comes from reading `src/SpecScribe/*.cs` and fetching BMad's own module documentation — not from writing a prototype adapter or enum case. If you catch yourself editing `ModuleContext.cs`'s `BmadModule` enum, stop; that's 18.2.
- **This spike studies BMad's OWN ecosystem, not a third-party framework.** Don't reuse 11.1/12.1/13.1/14.1/15.1's "how do we adapt an unfamiliar framework's conventions" framing wholesale — this repo already runs on BMad and already has two of its modules fully supported; the unfamiliar part is specifically the OTHER modules (BMB, TEA, CIS, and whatever else the ecosystem currently ships).
- **No new authoring schema.** SpecScribe never asks BMad-module users to add SpecScribe-specific files — coverage must derive from each module's own existing conventions (`module-help.csv`, whatever artifact shape each module's docs describe).
- **Verify, don't assume — and distinguish the tool's own demo/docs from a downstream project's real usage.** This story's candidate-module table was built from `bmad-code-org/BMAD-METHOD`'s README and module-specific doc sites, not from a project that installed BMB/TEA/CIS and used them for real work.
- **NFR8.** Genuinely-absent artifact families for a niche module (e.g. `Sprint`/`Retros`/`Requirements` staying null/empty, or even `Epics` if no synthetic mapping fits) are honest absence, not gaps to fill.
- **Coverage tiers are NOT mandated by this story's own AC text** — AC #1's language is "mappable, partially-mappable, or unsupported" plus a named target projection, matching 11.1/15.1's phrasing (not 12.1's mandatory-tier variant). Don't force tier vocabulary in just because 12.1 used one.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future module coverage only translates into `ArtifactBundle` or `ModuleContext` extension points, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps candidate BMad-module source shapes onto that exact contract (or `ModuleContext`'s existing generic layer), nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future module-specific insight enrichment must stay additive/non-blocking, same as BMad's existing git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."*
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — don't let this spike commit to a package split ([[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- Re-surveying BMad Method or BMad GDS as if they were undiscovered — both are already `Supported: true`; confirm current state, don't re-derive their coverage.
- Assuming a new BMad module automatically needs a brand-new `IArtifactAdapter` and the not-yet-built registry, without first checking whether extending the existing `BmadArtifactAdapter`/`ModuleContext` (new enum case + doc/glossary arrays) is sufficient — this spike's Task 3 exists precisely to settle that, and the likely answer differs from 11.1-15.1's third-party-framework assumption.
- Treating this project's own `.claude/skills/bmad-*` roster as automatically CIS-equivalent without checking whether those skills live under the installed `bmm` module or would require the separate `cis` module.
- Treating BMad's README/doc-site descriptions of BMB/TEA/CIS as equivalent to inspecting a real downstream repo that installed and used them.
- Forcing a coverage-tier vocabulary into this spike's findings because 12.1 used one — this story's AC text doesn't require it.
- Designing or partially building any registry or new adapter inline instead of naming the extend-vs-registry decision as a Task 3/6 finding.
- Proposing a sixth, competing adapter-registry ADR instead of coordinating with 11.1-15.1's shared finding (if this spike concludes a registry is needed at all — Task 3 may conclude it is not).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md`
- Sprint key: `18-1-bmad-module-landscape-and-coverage-spike`
- Downstream story keys — **updated 2026-07-25 after this spike's findings drove an owner-approved scope split.**
  At drafting there was one seeded downstream story, `18-2-priority-bmad-module-baseline-coverage`. It became two:
  - `18-2-bmad-module-identity-foundation` (`ready-for-dev`) — the identity fix this spike surfaced (ADR 0015
    Decisions 1/2/4), which now **gates** coverage work.
  - `18-5-priority-bmad-module-baseline-coverage` (`backlog`) — the original 18.2 ACs verbatim, retargeted to TEA.
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 3/6 concludes a genuine architecture fork not already covered by 11.1-15.1's shared registry finding.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:275,2884-2913`] — Epic 18 intro, FR36, Story 18.1 (verbatim ACs quoted above) + Story 18.2 (downstream coverage story, ACs quoted for scope-boundary context).
- [Source: `_bmad-output/planning-artifacts/epics.md:88,198,299,307`] — FR36 seating note, delivery-sequence notes distinguishing Epic 18 from Epics 11-15.
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/specs/spec-specscribe/requirements-catalog.md:33`] — FR-19 "Popular-framework coverage policy" (the multi-framework capability's actual numbered requirement; cross-reference only, this spike doesn't need to resolve any citation drift the way 15.1 did).
- [Source: `src/SpecScribe/IArtifactAdapter.cs:19-38`] — the ingestion contract.
- [Source: `src/SpecScribe/ArtifactBundle.cs:10-58`, `AdapterDiagnostic.cs:7-43`] — the projection carrier and diagnostic vocabulary.
- [Source: `src/SpecScribe/BmadArtifactAdapter.cs:11-344`] — the one reference implementation; already IS the BMad adapter this spike's target modules would extend.
- [Source: `src/SpecScribe/ModuleContext.cs:1-425`] — the primary object of study: generic manifest/CSV-driven detection (`Detect`, `ReadInstalledModules`, `BuildContext`) vs. the hardcoded two-case (`BmadMethod`/`GameDevStudio`) `WellKnownDocs`/glossary switches.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18`] — the `Frameworks` roster; `bmad`/`gds` already `Supported: true`, the four third-party rows are Epics 11-15's targets, not this epic's.
- [Source: `src/SpecScribe/ArtifactCoverage.cs:79-84`] — the dashboard-level "coverage" concept (repo's own doc freshness), a different sense from this spike's artifact-classification coverage map; don't conflate.
- [Source: `src/SpecScribe/SiteGenerator.cs:47-51`] — the hardcoded single-adapter field; the shared registry gap (if applicable per Task 3).
- [Source: `src/SpecScribe/EpicsModel.cs:1-88`, `RequirementsModel.cs:1-102`, `RetroModel.cs:1-20`, `SprintStatus.cs:1-43`] — the "host-neutral" model shapes a candidate module's artifacts might or might not fit.
- [Source: `README.md:5-6,12-24`] — supported-frameworks table; BMad + GDS ✅, third-party frameworks 🧭 Planned.
- [Source: repo root `_bmad/_config/manifest.yaml`] — this project's own installed-module proof (`core` + `bmm` only, no `cis`/`bmb`/`tea`/`gds`) — live evidence that BMB/TEA/CIS are not installed here and must be researched externally.
- [Source: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`, `15-1-superpowers-integration-spike.md`] — closest sibling spikes; mirror structure and Completion-Notes-as-deliverable convention, but note the "already-familiar framework" difference called out above.
- [Web: `github.com/bmad-code-org/BMAD-METHOD` README, DeepWiki module pages, `cis-docs.bmad-method.org`, `bmad-builder-docs.bmad-method.org`, fetched live 2026-07-21] — candidate module roster (BMad Builder, Test Architect, Creative Intelligence Suite) and their hypothesized artifact shapes; treat as a starting hypothesis to reconfirm, not settled fact.
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — coordinate with siblings, don't duplicate).

### Git intelligence summary

No BMad Builder, Test Architect, or Creative Intelligence Suite code, docs, or prior exploration exist anywhere in this repo (confirmed via grep across `src/`, `docs/`, `epics.md`, and this repo's own `_bmad/_config/manifest.yaml`, which lists only `core`+`bmm`) — this spike starts from a clean slate on the module-survey side, but with substantially more existing BMad-native machinery to build on than any of the third-party spikes (11.1-15.1) had: a working `BmadArtifactAdapter`, and a `ModuleContext` class that already generically reads any module's installed-registry entry and `module-help.csv`. Recent commits (Epic 7 stories 7.9-7.11, Epic 10 retro work, sibling per-framework create-story sessions 11.1-15.1) are unrelated to this story's implementation scope; all five sibling framework spikes remain `ready-for-dev`, none `done`, so no registry conclusion exists yet to defer to.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Opus 5)

### Debug Log References

Detection behavior was **empirically verified**, not reasoned about. A throwaway console probe was built in
the session scratchpad (never in the repo; deleted after the run) referencing `src/SpecScribe/SpecScribe.csproj`,
which constructed eight synthetic repo fixtures under the OS temp dir — each with a real
`_bmad/_config/manifest.yaml` plus verbatim first rows of each module's **real** `module-help.csv` (fetched
from the module repos) — and called `ModuleContext.Detect` / `IsMethodPresent` / `IsGdsPresent` on each.
`git status` confirms the only file this story modified is the story file itself. The probe's raw output is
reproduced in Finding 3 below.

**Regression run — 3 pre-existing failures, none attributable to this story.** `dotnet test` reported
**2384 passed / 3 failed / 3 skipped (2390 total)**. This story changed **no** compiled code or assets (its only
edits are this markdown file and `sprint-status.yaml`), and all three failures were traced to a **concurrent
session's uncommitted work** in the shared working tree — the condition CLAUDE.md describes as an accepted
working condition. Per CLAUDE.md these were **left untouched**: no `git reset`/`checkout`/`clean`, and no
"fixing" of another session's in-flight story.

| Failing test | Attribution |
|---|---|
| `HierarchyExplorerTests.TextTwin_IsComplete_Navigable_NonColor_AndNestedByParent` | Uncommitted `src/SpecScribe/HierarchyExplorer.cs` (+58 lines, dated **2026-07-25**, comment: *"verify round named it exactly right — 'weight is a confusing value on the tooltip'"*) **deletes** the ` weight {n.Value}` span the test asserts via `Assert.Contains("weight ")`. The test simply has not been updated yet by that session. |
| `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_...` | Uncommitted `specscribe.css` (+42) / `specscribe.js` (+92). The test's own comment history shows the fingerprint moves on any every-page stylesheet/script byte change; its fixture "cites no real repo files", so this story's markdown edits cannot reach it. Matches the known *"expect the golden fingerprint to move under you"* condition. |
| `FileWatcherServiceTests.BurstOfSaves_CoalescesAndLeavesCoherentOutput` | **Passes in isolation** (re-run: 1 passed). Load/timing-sensitive under the full suite, plausibly aggravated by the concurrent session writing into the tree during the run. Not deterministic, not attributable here. |

### Completion Notes List

## Story 18.1 — BMad Module Landscape & Coverage Map (spike deliverable)

**Verdict up front:** Epic 18's first unit of work is **not** any module's artifacts. It is a
module-**identity** defect that is live today: a repo whose only installed BMad module is CIS, TEA, or BMB is
silently reported as **BMad Method** and is served BMM's entire glossary. The artifact-coverage question
(which module to render first) is real but secondary, and the answer to it is **TEA**.

---

### 1. Confirmations of this story's premises (Task 1)

Every claim below was checked against live code/files at baseline commit `611097d`, not taken from the story's
summary tables.

| Story premise | Verdict | Evidence |
|---|---|---|
| BMM + GDS both `Supported: true`, not this spike's targets | **Confirmed** | `AboutSddTemplater.cs:12-13`; `README.md:19-20` (BMad Method 6.10.0 ✅, BMad GDS 0.6.0 ✅) |
| The four third-party rows are Epics 11-15's targets | **Confirmed** | `AboutSddTemplater.cs:14-17` (`speckit`/`gsd`/`gsd-pi`/`superpowers`, all `false`) |
| No adapter registry exists | **Confirmed** | `SiteGenerator.cs:51` — `private readonly BmadArtifactAdapter _adapter = new();` with the "registry … arrives with Stories 4.3+" comment intact |
| No sibling spike has landed a registry conclusion to defer to | **Confirmed** | 11.1 / 12.1 / 13.1 / 14.1 / 15.1 are all `Status: ready-for-dev` with **empty** Completion Notes |
| `AppliesTo` markers `_bmad/` as a whole, not a sub-module | **Confirmed** | `BmadArtifactAdapter.cs:76-77` — `Directory.Exists(Path.Combine(options.RepoRoot, "_bmad"))` |
| Diagnostic vocabulary is exactly five values | **Confirmed** | `AdapterDiagnostic.cs:7-32` — `Unsupported`, `Malformed`, `Skipped`, `Error`, `Informational` |
| Difference #3: are `bmad-brainstorming` etc. BMM-native or CIS evidence? | **Resolved — BMM/Core-native** | see §2 |

**Correction to the story's framing.** The story states a new BMad module "already produces a `CommandCatalog`
with real slash commands … **without any code change** — IF that module's `module-help.csv` follows the existing
CSV shape." The CSV-reading half is true; the **module-identification** half is not. See Finding 3 — this is the
single most important correction this spike makes.

#### Where the generic seam actually ends (Task 1, final subtask)

Sharper than the story's description. The boundary is *inside* `BuildContext`, not between it and `WellKnownDocs`:

| Layer | Mechanism | Generic? |
|---|---|---|
| Install discovery | `ReadInstalledModules` reads `_bmad/_config/manifest.yaml`; disk fallback scans `_bmad/*/module-help.csv` (`ModuleContext.cs:204-218`) | ✅ **Fully generic** — any module code works, `core` correctly excluded |
| Presence checks | `IsModulePresent(repoRoot, code)` (`ModuleContext.cs:174-188`) | ✅ **Fully generic** — takes an arbitrary code; only the two public wrappers are hardcoded |
| CSV parse → `byStep` + `ModuleLabel` | `BuildContext` rows 291-345 | ✅ **Generic** — `ModuleLabel` comes through correctly for CIS/TEA/BMB (probe-verified) |
| **Module identification** | `BuildContext` **lines 346-348**: `prefix.StartsWith("gds") ? GameDevStudio : BmadMethod` | ❌ **Not generic — and not a graceful default.** A closed binary with no `Unknown` branch |
| Docs / glossary | `DocsFor` / `GlossaryFor` switches (`ModuleContext.cs:118-123, 151-156`) | ❌ Hardcoded per enum case (as the story said) |
| Primary selection | `ChoosePrimary` (`ModuleContext.cs:259-286`) | ⚠️ Directory-name based (`gds` prefix), then **manifest order** — see Finding 3b |

The load-bearing detail: **`BmadModule` has an `Unknown` case, and `BuildContext` never returns it.** Detection
either succeeds or falls through to `BmadMethod`. `ModuleContext.None` is only reachable when no CSV parses at
all — never when a *foreign but well-formed* module CSV parses.

---

### 2. Difference #3, resolved: the ideation skills are BMM/Core-native (Task 2)

The story flagged this repo's `bmad-brainstorming` / `bmad-forge-idea` / `bmad-prfaq` / `bmad-party-mode` /
`bmad-domain-research` / `bmad-market-research` as possible CIS evidence. Checked on disk — they split **two**
ways, and neither is CIS:

- **BMM-native** (`_bmad/bmm/module-help.csv`, module column `BMad Method`): `bmad-brainstorming` (row 12),
  `bmad-market-research` (13), `bmad-domain-research` (14), `bmad-prfaq` (17).
- **Core-native** (`_bmad/core/module-help.csv`, module column `Core`): `bmad-brainstorming`, `bmad-party-mode`,
  `bmad-forge-idea`, plus `bmad-spec`, `bmad-shard-doc`, the editorial/review skills, `bmad-customize`.
- Cross-checked against `_bmad/_config/skill-manifest.csv`, which carries an explicit `module` column
  (`"core"` / `"bmm"`) per skill. **No `cis` anywhere.**

**Consequence for CIS's uniqueness:** partially deflating, and confirmed from CIS's own manifest. CIS's real
`module-help.csv` lists `bmad-brainstorming` **as one of its own five skills** — the *same skill id* Core already
ships. So CIS is not wholly novel surface; it overlaps Core by at least one workflow. Its genuinely distinctive
skills are `bmad-cis-innovation-strategy`, `-problem-solving`, `-design-thinking`, `-storytelling`.

**Bonus finding (skill-id collisions across modules).** Because module A and module B can ship the *same* skill
id, `BuildContext`'s "first row wins for a given step" rule (`ModuleContext.cs:334-338`) is only safe *within*
one module's CSV. Any future work that merges catalogs across modules must decide a collision rule. Noted for 18.2.

**Structural finding — what actually lands on disk.** `_bmad/bmm/` in this repo contains **only** `config.yaml`
and `module-help.csv`; the 189 `bmm/…` paths in `_bmad/_config/files-manifest.csv` are not present as files
(this install routes skills to `.claude/skills/`). Two consequences:
1. **`_bmad/{code}/module-help.csv` is the only dependable per-module on-disk marker** — which is exactly what
   `ModuleContext` already keys on. SpecScribe picked the right signal; 18.2 should not switch to walking
   `_bmad/{code}/` subtrees.
2. **`module.yaml` is an installer-*source* file and is NOT installed.** It is tempting (it carries a clean
   `code:` + `name:`) but it is unavailable in a consuming repo. `_bmad/{code}/config.yaml` carries no module
   identity either. **The `module` column of `module-help.csv` is the only on-disk module label.**

---

### 3. The central finding: non-BMM modules silently impersonate BMad Method (Tasks 1, 3, 4)

#### 3a. Probe output (verbatim)

```
--- bmm-only ---   Module enum: BmadMethod       ModuleLabel: BMad Method                 /create-story: /bmad-create-story
--- gds-only ---   Module enum: GameDevStudio    ModuleLabel: Game Dev Studio             /create-story: /gds-create-story
--- cis-only ---   Module enum: BmadMethod  (!)  ModuleLabel: Creative Intelligence Suite /create-story: (null)
--- tea-only ---   Module enum: BmadMethod  (!)  ModuleLabel: Test Architecture Enterprise/create-story: (null)
--- bmb-only ---   Module enum: BmadMethod  (!)  ModuleLabel: BMad Builder                /create-story: (null)
```

For all three of `cis-only` / `tea-only` / `bmb-only`:
`Docs = prd.md, ARCHITECTURE-SPINE.md, brief.md, DESIGN.md, EXPERIENCE.md` and
`Glossary = FR, NFR, AC, ADR, PRD, spec kernel, …` — **BMM's, in full.**

**Root cause:** every first-party BMad module except GDS prefixes its skills `bmad-`. Verified against the real
`module-help.csv` of each module repo:

| Module | code | `module` column label | Skill ids | `prefix` computed | Identified as |
|---|---|---|---|---|---|
| BMM | `bmm` | BMad Method | `bmad-create-story` | `bmad` | BmadMethod ✅ |
| GDS | `gds` | Game Dev Studio | `gds-gdd`, `gds-create-story` | `gds` | GameDevStudio ✅ |
| CIS | `cis` | Creative Intelligence Suite | `bmad-cis-innovation-strategy` | `bmad` | BmadMethod ❌ |
| TEA | `tea` | Test Architecture Enterprise | `bmad-testarch-trace` | `bmad` | BmadMethod ❌ |
| BMB | `bmb` | BMad Builder | `bmad-bmb-setup`, `bmad-module-builder` | `bmad` | BmadMethod ❌ |

GDS is the *only* module whose skill prefix happens to equal its module code — which is precisely why the
current two-case switch has worked so far. **`prefix` is a coincidence, not a contract.**

> Note on a near-miss: BMad's docs advertise GDS commands as `/bmgd-gdd` / `/bmgd-narrative`, which would have
> broken `prefix.StartsWith("gds")`. Checked the real file rather than trusting the docs —
> `bmad-module-game-dev-studio/src/module-help.csv` uses `gds-*` throughout, and `src/module.yaml` says
> `code: gds`, `name: "BMGD: BMad Game Dev Studio"`. **`BMGD` is branding; `gds` is the code. Current GDS
> support is correct.** The repo's own tests use synthetic `gds-*` fixtures
> (`ModuleContextTests.cs:57-58`), so they would not have caught a divergence — 18.2 should pin fixtures to
> real module CSVs.

#### 3b. How far the harm actually propagates (bounded honestly)

| Surface | Affected? | Why |
|---|---|---|
| Nav / quick-link **module docs** | **No** | `SiteNav.cs:206-215` skips any `ModuleDoc` with no filename match in the source tree. A CIS repo has no `prd.md`, so no phantom links. Self-limiting. |
| **Command panels** ("Next Steps") | **No — degrades correctly** | Every `Command()` lookup misses → `null` → ~40 call sites omit the suggestion. Honest NFR8 absence. |
| **Glossary** on `how-to-read.html` | **YES — unconditional** | `HowToReadTemplater.AppendGlossary` gates only on `glossary.Count == 0` (`:176`). BMM's 10 terms render for a module that publishes none of them. |
| **Every rendered page** | **YES — unconditional** | `SiteGenerator.cs:4270` runs `AbbreviationExpander.Expand(html, _module.Glossary)`, wrapping FR/NFR/AC/ADR/PRD in `<abbr>` site-wide. |
| About-SDD "Detected" badge | Partly | `IsMethodPresent`/`IsGdsPresent` are independent and correct — a CIS-only repo shows neither as Detected. Consistent. |

So the defect is **narrower than "the portal lies everywhere"** but is a genuine NFR8 violation on the two
surfaces that are not file-gated: SpecScribe asserts a vocabulary the project does not use.

#### 3c. The dual-install regression (sharpest consequence)

`ChoosePrimary` returns `candidates.FirstOrDefault(c => !DirName(c).StartsWith("gds"))` — i.e. **manifest
order** decides among non-GDS modules. Probe-verified:

```
--- bmm+cis (bmm first in manifest) --- ModuleLabel: BMad Method   /create-story: /bmad-create-story   IsMethodPresent=True
--- cis+bmm (cis first in manifest) --- ModuleLabel: Creative Intelligence Suite  /create-story: (null)  IsMethodPresent=True
--- bmm+tea (tea first in manifest) --- ModuleLabel: Test Architecture Enterprise /create-story: (null)  IsMethodPresent=True
```

A repo that **genuinely has BMM installed** loses **all** BMM command suggestions across the entire portal
because a sibling module won a manifest-order tie — while `IsMethodPresent` still reports `True`, so the
About-SDD page says "BMad — Supported, Detected". That is an internally contradictory portal state, it is
install-order-dependent (therefore intermittent), and it will trigger the first time an owner adds TEA or CIS
to a BMM project. **This is the highest-severity item Epic 18 has to fix, and it is a regression to existing
BMM support, not new-module coverage.**

---

### 4. Extend vs. new adapter — answered (Tasks 3, 6)

**Answer: (a) extend. Epic 18 does NOT need the adapter registry.** Epic 18 is the one framework epic that can
proceed while the registry gap stays open — worth stating plainly to the other five spikes.

Rationale: `AppliesTo` markers `_bmad/` as a whole (`BmadArtifactAdapter.cs:76-77`), so *every* BMad module —
BMM, GDS, CIS, TEA, BMB, and any BMB-generated custom module — already self-selects into the existing adapter.
A second `IArtifactAdapter` would have an identical `AppliesTo`, making registry selection ambiguous rather
than helpful. **Do not build a registry for Epic 18, and do not propose a sixth competing registry ADR** —
defer wholly to whichever of 11.1-15.1 lands that decision first.

**Named extension points for 18.2** (exact, verified line refs):

1. `BmadModule` enum (`ModuleContext.cs:8`) — add cases. **But see the ADR proposal below: the enum being
   *closed and single-valued* is the real constraint.**
2. `BuildContext` **lines 346-348** — replace skill-prefix inference with **module-code inference from the
   containing directory name** (`Path.GetFileName(Path.GetDirectoryName(csvPath))`), which is already how
   `ChoosePrimary` and `IsModulePresent` identify modules and is the value that actually equals the module
   code (`bmm`/`gds`/`cis`/`tea`/`bmb`). **This one change is the fix for Finding 3.** An unrecognized code must
   map to `BmadModule.Unknown` with an empty doc/glossary set, not fall through to `BmadMethod`.
3. `DocsFor` switch (`:118-123`) + a new `ModuleDoc[]` array — *only if* the module publishes well-known docs.
   CIS/TEA/BMB publish **none** with fixed filenames, so for all three this array is legitimately empty.
4. `GlossaryFor` switch (`:151-156`) + a new `GlossaryTerm[]` array.
5. A new `IsXPresent` wrapper mirroring `:165`/`:170` — one line each; `IsModulePresent` is already generic.
6. `AboutSddTemplater.Frameworks` (`:10-18`) + its `detected` switches (`:38-43`, `:66-70`) + `SiteNav` output
   paths + the `README.md:19-24` table — these are **per-module hardcoded rosters** the story did not list as
   extension points, but any new module needs a row in each. `AboutSddTemplater`'s `detected` switch takes only
   two bools (`methodPresent`, `gdsPresent`) and its signature must widen.

**`ChoosePrimary` (Task 3, third subtask):** it needs more than "a third branch." Its current contract —
return exactly one winner — is the thing that breaks (Finding 3c). Minimum viable fix for 18.2: make
BMM/GDS **win** ties over auxiliary modules (invert today's accidental manifest-order behavior), so adding TEA
to a BMM repo cannot demote BMM. That is a small, safe change and it fixes the regression without waiting for
the larger multi-module redesign.

---

### 5. Per-module coverage map (Task 4)

Classification vocabulary is AC #1's — **mappable / partially-mappable / unsupported**. No tier vocabulary
imported from 12.1, per Dev Notes.

#### Already covered — confirmed current, not re-surveyed
- **BMad Method (`bmm`)** — `Supported: true`, fully covered.
- **BMad GDS (`gds`)** — `Supported: true`. Re-confirmed against the real module: its `module-help.csv` ships
  `gds-create-epics-and-stories`, `gds-sprint-planning`, `gds-sprint-status`, `gds-retrospective`,
  `gds-create-story`, `gds-dev-story` — i.e. GDS produces the *same artifact families* as BMM, which is why one
  adapter serves both. `module.yaml` confirms `code: gds`, version 0.6.0, matching `README.md:20`.

#### Test Architect / Test Architecture Enterprise (`tea`) — **partially-mappable. Recommended priority.**

| Distinctive artifact | Classification | Target projection |
|---|---|---|
| `traceability-matrix.csv` (from `bmad-testarch-trace`) | **Partially-mappable** | Genuinely close to `RequirementsModel` + SpecScribe's **existing** `traceability.html` (Story 21.1). CSV, not markdown — needs a parser; the requirement-id join key is unverified. |
| `nfr-report.md` (`bmad-testarch-nfr`) | **Partially-mappable** | SpecScribe already models NFRs (`RequirementKind.Design`/NFR handling, Story 9.2). Overlap is real. |
| test-design doc, review report, ATDD checklist, gate/release decision | **Unsupported (candidate extension)** | No `ArtifactBundle` field. A QA/quality artifact family with no analog. |
| framework scaffold, CI config, generated `*.spec.ts` | **Unsupported (non-goal)** | Executable/config output, not planning artifacts. |
| Epics / stories / sprint / retros | **N/A** | TEA produces none — `Epics`/`Sprint`/`Retros` stay null/empty. Honest NFR8 absence. |

**Blocking unknown for 18.2:** TEA writes to a `test_artifacts` output key (every row of its `module-help.csv`
`output-location` column). That key exists in **neither** core config nor `_bmad/bmm/config.yaml` — it would live
in `_bmad/tea/config.yaml`, which is generated at install time. **SpecScribe currently reads no module
`config.yaml` at all.** 18.2 must either read `_bmad/{code}/config.yaml` for output-path keys or accept that TEA
artifacts are only found when they happen to fall inside the scanned source root. This is a real prerequisite,
not a detail.

#### Creative Intelligence Suite (`cis`) — **mappable, but with near-zero marginal value**

| Distinctive artifact | Classification | Target projection |
|---|---|---|
| Innovation-strategy / problem-solving / design-thinking / storytelling session docs | **Mappable — already rendered today** | All five skills declare `output-location: output_folder`, i.e. the same `_bmad-output` root SpecScribe already walks. They are freeform markdown with **no fixed filenames** and are picked up by the existing generic-page pass **with zero code change**. |
| `design-methods.csv`, `innovation-frameworks.csv`, `solving-methods.csv`, `story-types.csv` | **Unsupported — non-goal** | Reference datasets shipped *with the skills* under `.claude/skills/`, not project output. Rendering them would surface tool internals as project content. |
| Epics / requirements / sprint / retros | **N/A** | CIS produces none. |

> The story's hypothesis of a dated `{output_folder}/analysis/brainstorming-session-{date}.md` path was **not
> confirmed**: CIS's own `module-help.csv` declares a bare `output_folder` with no subfolder and no filename
> convention. Correcting the hypothesis rather than carrying it forward.

**Therefore CIS is explicitly *not* recommended first.** Its outputs already render; a dedicated CIS module case
would buy a glossary and an About-SDD row, nothing more.

#### BMad Builder (`bmb`) — **unsupported as an artifact source; but see the important twist**

The story asked for this framing to be confirmed or rejected explicitly. **Confirmed:** BMB is a meta-tool. Its
outputs are *other modules'* scaffolding — `module.yaml`, `module-help.csv`, `SKILL.md`, `customize.toml`,
workflow step files (verified in `bmad-builder/skills/…/assets/` and `samples/`). It produces **no**
project-tracking artifacts. **Non-goal for artifact rendering.**

**The twist — this is the finding that matters most about BMB.** BMB's *whole purpose* is generating custom
modules with **arbitrary, user-chosen module codes**, each shipping a `module.yaml` (`code:`) and a
`module-help.csv`. Since `Detect` treats **any** non-`core` `_bmad/*/module-help.csv` as a candidate, a
BMB-generated custom module is *already* a live input to SpecScribe's detection today — and hits Finding 3
exactly like CIS/TEA do. **18.2 cannot enumerate a closed set of module codes.** It must handle "a module code I
have never heard of" as a first-class, well-behaved case: correct label, no docs, no glossary,
`BmadModule.Unknown`, no impersonation. **This is the strongest argument that Finding 3's fix is the real scope
of 18.2, and that the fix must be open-world rather than three more enum cases.**

#### Ecosystem roster correction (Task 2)

The story's three-module hypothesis is **incomplete**. The `bmad-code-org` org additionally publishes
`bmad-loop`, `bmad-automator`, `bmad-manticore`, `bmad-method-ui`, `bmad-method-wds-expansion`,
`bmad-utility-skills`, `bmad-module-template`, and a `bmad-plugins-marketplace`. These were **not** classified
here (out of the ACs' named scope, and several are plugins/marketplace entries rather than installed modules),
but their existence is itself the point: **the module set is open and growing**, which independently supports the
open-world conclusion above. Flagged for 18.2 scoping rather than silently omitted.

**Real-downstream-usage caveat (the check 15.1 demanded).** No repo that has actually *installed and used*
CIS/TEA/BMB was found. Evidence is each module's own source repo — one tier better than doc-site prose (real
`module-help.csv` / `module.yaml` bytes, and real detection behavior via the probe), but still not a downstream
adopter. `bmad-code-org/bmad-method-sample-data` was located and inspected as the closest candidate; it holds
spec-kernel, braindump, and brainstorming samples (`brainstorm.html`, `brainstorm-intent.md`) but **no
`_bmad/` install and no TEA/CIS/BMB module output**, so it does not settle artifact filenames. **TEA's and CIS's
concrete output filenames therefore remain the largest unverified area of this map.**

---

### 6. Priority recommendation (Task 4, final subtask)

**Sequence 18.2 as: fix identity first, then cover TEA.**

1. **Module identity + the dual-install regression (Finding 3).** Highest severity, smallest diff, and it is a
   fix to *existing shipped BMM support*, not new coverage. It is a prerequisite for every other module.
2. **TEA** as the priority *coverage* module. Rationale: (i) it is the only candidate with **structured,
   distinctively-named** on-disk artifacts (`traceability-matrix.csv`, `nfr-report.md`); (ii) those artifacts
   overlap surfaces SpecScribe **already has** (Story 21.1 traceability, Story 9.2 NFR coverage), so coverage
   compounds rather than starting a new surface; (iii) it is the module this project's own tooling already
   references (`bmad-create-story` suggests `/bmad:tea:automate`), making real adoption most likely.
   Its `test_artifacts` config-key dependency is the first thing 18.2 must resolve.
3. **CIS** — defer. Already renders via the generic pass; near-zero marginal value.
4. **BMB** — non-goal for artifact rendering; its requirement is absorbed into item 1 (open-world module codes).

**Recommended 18.2 scope boundary:** identity fix + open-world unknown-module handling + TEA artifact coverage.
**Out:** CIS/BMB module cases, any registry, any `ArtifactBundle` field addition.

---

### 7. Framework-extra data, non-goals, and diagnostic wording (Task 5)

**Candidate projection extensions** (recorded, not landed):
- A **quality/test artifact family** for TEA (test strategy, NFR report, traceability matrix, gate decisions).
  Would be a new `ArtifactBundle` field; the traceability matrix may instead fold into `RequirementsModel`.
- **Per-module output-path config** — reading `_bmad/{code}/config.yaml` for keys like `test_artifacts`.
- **Multi-module `ModuleContext`** — see the ADR proposal in §8.

**Explicit non-goals with rationale:**
- BMB-generated scaffolding (`module.yaml`, `SKILL.md`, `customize.toml`, workflow steps) — tool internals, not
  project artifacts.
- CIS/TEA reference datasets (`design-methods.csv`, `innovation-frameworks.csv`, `solving-methods.csv`,
  `story-types.csv`, `tea-index.csv`) — ship with the skills, not project output.
- TEA-generated test code, CI YAML, framework scaffolds — executable output; the repo's own code surfaces
  already cover source files.
- Any new authoring schema — unchanged project rule.

**Non-fatal notices, mapped onto the five-value vocabulary** (no sixth invented), drafted in the tone of
`BmadArtifactAdapter.cs:170-188, 219-224, 262-276`:

| Situation | Category | Drafted message |
|---|---|---|
| A `_bmad/{code}/module-help.csv` parses but `{code}` is not a module SpecScribe models | `Informational` | `"Detected BMad module '{code}' ({label}); SpecScribe has no module-specific docs or glossary for it, so those sections are omitted."` |
| Module CSV present but no usable `skill` column | `Unsupported` | `"module-help.csv has no 'skill' column; command suggestions for this module are omitted."` |
| Module CSV unreadable / parse exception | `Malformed` | `"Could not read module-help.csv: {message}"` |
| >1 non-GDS module installed; one chosen as primary | `Skipped` | `"{n} additional installed module(s) not used for command suggestions in favor of '{code}'."` |
| `_bmad/` unreadable (permissions/IO) | `Error` | *(existing behavior — `Detect` swallows to `None`; 18.2 should surface it instead of silently degrading)* |

The `Informational` row is the important one: it is exactly what turns Finding 3 from a silent lie into honest
absence, and it uses the category `AdapterDiagnostic.cs:26-31` was written for.

**Command-generalization assessment (AC #2, second clause).** The quoted note lives at
`epics.md:157` (Additional Requirements): *"current next-step command mapping is strongly GDS-oriented and
requires generalization."* **That note is now stale and should be retired.** The *mechanism* was generalized when
`CommandCatalog`/`BuildContext` became CSV-driven: `ModuleContext.cs:329-338` strips the prefix and keys on the
step remainder, so `/bmad-create-story` and `/gds-create-story` both resolve `create-story` with zero
module-specific code, and `Command()` returning `null` makes ~40 call sites omit cleanly. The residue is
narrower and worth naming precisely: the ~40 call sites (`BmadCommands.cs`, `SprintTemplater.cs`,
`EpicsViewBuilder.cs`, `HtmlRenderAdapter.Epics.cs`, `ActionItemsTemplater.cs`, `SiteGenerator.cs:4539`)
hardcode a **step vocabulary** — `create-story`, `dev-story`, `code-review`, `sprint-planning`, `sprint-status`,
`retrospective`, `correct-course`, `quick-dev`, `create-epics-and-stories`,
`check-implementation-readiness` — which is the BMM∩GDS planning vocabulary. CIS/TEA/BMB share **none** of it.

**Assessment: no generalization work is required for Epic 18.** Those call sites live on surfaces
(sprint board, epics, story pages) that only exist when there *are* epics/stories — which only BMM and GDS
produce. For a TEA/CIS repo the panels correctly vanish. Recommend **replacing** `epics.md:157`'s note with the
accurate residual statement rather than carrying a stale "GDS-oriented" claim forward. **Agreed scope boundary
for 18.2: command generalization is explicitly OUT.**

---

### 8. Architecture fork found — ADR proposed, not written (Tasks 3, 6)

Per this project's ADR-creation-trigger discipline, a genuine fork surfaced. It is **distinct from** the
adapter-registry question owned by 11.1-15.1, so proposing it does not duplicate or compete with theirs.

> **Proposed ADR — "BMad module identity is open-world and multi-valued."**
> **Context.** `ArtifactBundle.Module` carries exactly one `ModuleContext`, whose `Module` is one closed-enum
> value. Real BMad repos are increasingly multi-module (BMM + TEA + CIS), and BMB exists to mint modules with
> codes SpecScribe cannot know in advance. The current single-winner design produces both false identity
> (Finding 3a) and a live regression (Finding 3c).
> **Decision to ratify.** Module identity derives from the **module code** (directory name), not the skill
> prefix; an unrecognized code is a **first-class supported outcome** (`Unknown` + correct label + no docs/
> glossary + an `Informational` diagnostic), never a fallback to `BmadMethod`; and `ModuleContext` carries the
> **set** of installed modules with a designated primary, rather than a single winner.
> **Consequence.** Touches a cross-cutting contract (`ArtifactBundle.Module`), which is why it warrants an ADR
> rather than an owner-locked story note.

**Now drafted, at the owner's explicit request (2026-07-25), as
[`docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md`](../../docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md)
— `Status: Proposed`, awaiting ratification.** The spike itself did not author it; the owner asked for the draft
after reviewing these findings. It carries seven decisions (identify by module **code**; first-class `Unknown`
with an `Informational` diagnostic; multi-valued `ModuleContext`; BMM/GDS never demoted by a manifest-order tie;
per-module coverage stays an explicit act; Epic 18 extends rather than registers; detection fixtures pinned to
real module CSVs), an explicit non-goal that it must **not** become a sixth competing registry proposal, and
three open questions for ratification — chiefly whether Decisions 1/2/4 land in 18.2 as a prerequisite slice
with Decision 3 (the multi-valued contract change) deferred to its own story. The spike recommends that split.

### File List

_No production or test files (`src/**`, `tests/**`) were created, modified, or deleted. This story is a spike;
its deliverable is the coverage map in Completion Notes above._

- `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — 18-1 → `review`, plus `last_updated`)
- `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md` (**added**) — drafted at the owner's
  explicit request after the spike completed, per §8. `Status: Proposed`; not ratified.
- `docs/adrs/README.md` (modified — ADR 0015 index entry)

## Change Log

- 2026-07-25 — **ADR 0015 drafted at owner request** (post-completion follow-up, not spike scope):
  `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md`, `Status: Proposed`, plus its
  `docs/adrs/README.md` index entry. Formalizes §8's proposal. Still **no** `src/`/`tests/` changes. Explicit
  non-goal recorded in the ADR: it must not become a sixth competing adapter-registry proposal — that decision
  stays with Epics 11-15, and per its Decision 6 Epic 18 does not need it. Three open questions left for
  ratification, chiefly the recommended scope split (Decisions 1/2/4 as an 18.2 prerequisite slice; Decision 3,
  the multi-valued `ModuleContext` contract change, deferred to its own story).
- 2026-07-25 — Story 18.1 executed (dev-story) → `review`. Spike only; **no `src/`/`tests/` changes** (`git status`
  clean apart from this file). Coverage map written to Completion Notes. Headline outcome: the central deliverable
  turned out **not** to be an artifact-coverage table but a live module-**identity** defect —
  `ModuleContext.BuildContext:346-348` infers the module from the *skill prefix*, and every first-party BMad module
  except GDS prefixes its skills `bmad-`, so CIS/TEA/BMB (and any BMB-generated custom module) are silently
  identified as `BmadModule.BmadMethod` and served BMM's full glossary site-wide. Verified empirically with a
  scratchpad probe over eight fixtures built from the modules' **real** `module-help.csv` files, not by inspection.
  A dual-install regression falls out of the same root cause: with CIS or TEA ahead of BMM in manifest order,
  `ChoosePrimary` demotes BMM and **all** BMM command suggestions vanish while `IsMethodPresent` still reports
  `True`. Other outcomes: Epic 18 confirmed as the one framework epic that does **not** need the adapter registry
  (`AppliesTo` already markers `_bmad/` wholesale) — extend, don't register, and no competing ADR; Difference #3
  resolved (the ideation skills are BMM/Core-native, and CIS ships Core's `bmad-brainstorming` itself, so CIS is
  less novel than hypothesized); GDS re-confirmed correct after checking the real module against docs that
  advertise a `/bmgd-*` prefix (`module.yaml` says `code: gds` — BMGD is branding); priority recommendation is
  **identity fix first, then TEA** (structured `traceability-matrix.csv`/`nfr-report.md` that overlap SpecScribe's
  existing Story 21.1/9.2 surfaces), with CIS deferred (its output already renders via the generic pass) and BMB a
  non-goal for rendering — though BMB drives the requirement that unknown module codes be an open-world supported
  case. Two hypotheses corrected rather than carried forward: CIS has no dated `analysis/brainstorming-session-*`
  path (bare `output_folder`, no filename convention), and the ecosystem is larger than the story's three-module
  table. Two gaps recorded for 18.2: TEA writes to a `test_artifacts` config key SpecScribe never reads (it reads
  no module `config.yaml` at all), and `epics.md:157`'s "strongly GDS-oriented … requires generalization" note is
  now **stale** — the mechanism was generalized when `CommandCatalog` became CSV-driven, so command generalization
  is explicitly OUT of 18.2's scope. One genuine architecture fork found and **proposed, not written** (ADR:
  "BMad module identity is open-world and multi-valued") — distinct from 11.1-15.1's registry question, flagged
  for owner decision.
- 2026-07-21 — Story 18.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map of BMad's own module ecosystem (BMad Builder, Test Architect, Creative Intelligence Suite) beyond the already-supported BMad Method/GDS, an explicit extend-vs-registry architectural finding (likely differs from the third-party spikes' assumption), a command-generalization assessment, and an 18.2 scope recommendation; no production code. First story of Epic 18 (BMad-native module exploration), distinct from the third-party-framework Epics 11-15; moves Epic 18 from `backlog` to `in-progress`.
