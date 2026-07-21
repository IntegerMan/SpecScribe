# Story 18.1: BMad Module Landscape and Coverage Spike

Status: ready-for-dev

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

- [ ] **Task 1 — Confirm the contract and existing BMad-module machinery against live code (AC: #1)**
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `ModuleContext.cs` (all 425 lines — this is the primary object of study), `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`, `AboutSddTemplater.cs`, `ArtifactCoverage.cs` in full — do not rely solely on this story's summary tables.
  - [ ] Confirm `AboutSddTemplater.Frameworks` [AboutSddTemplater.cs:10-18] and `README.md:19-24` still show BMad Method + BMad GDS both `Supported: true`/✅ — these are NOT this spike's targets.
  - [ ] Confirm (or correct) this story's claim that no cross-framework adapter registry exists (`SiteGenerator.cs:51`) and check whether any of Stories 11.1-15.1 has landed a registry conclusion (read their Completion Notes if `done`).
  - [ ] Precisely map where `ModuleContext`'s generic module-detection machinery (manifest/`module-help.csv` reading, `CommandCatalog`) ends and the two-case (`BmadMethod`/`GameDevStudio`) hardcoding (`WellKnownDocs`, glossaries) begins — this is the central architectural finding of Task 3/4.

- [ ] **Task 2 — Obtain and inspect representative BMad-module documentation/examples (AC: #1, #2)**
  - [ ] Fetch/inspect `github.com/bmad-code-org/BMAD-METHOD`'s README and module list to confirm the current roster of modules beyond BMM/GDS (this story's hypothesis: BMad Builder, Test Architect, Creative Intelligence Suite — reconfirm names/scope, the ecosystem moves fast).
  - [ ] For each candidate module, fetch its dedicated docs (e.g. `cis-docs.bmad-method.org`, `bmad-builder-docs.bmad-method.org`, or DeepWiki pages) to find concrete artifact shapes: file naming, default output paths, whether a `module-help.csv`/`module.yaml` pair exists per the generic pattern `ModuleContext` already expects.
  - [ ] Actively search for (or note the absence of) a real, downstream project that has installed one of these modules and produced real on-disk artifacts — distinguish "the framework's own docs/demo" from "a project that used the module," same caution 15.1 applied to Superpowers.
  - [ ] Resolve the Difference #3 puzzle: check `_bmad/bmm/` in this repo (or BMad Method's own docs) to determine whether `bmad-brainstorming`/`bmad-forge-idea`/`bmad-prfaq`/`bmad-party-mode`/`bmad-domain-research`/`bmad-market-research` are BMM-native skills or CIS-overlapping functionality, before concluding what CIS uniquely adds.

- [ ] **Task 3 — Answer the "extend vs. new adapter" question (AC: #1)**
  - [ ] State explicitly whether covering a new BMad module (e.g. CIS) means (a) extending `BmadArtifactAdapter`/`ModuleContext` with a new `BmadModule` case + doc/glossary arrays (most likely, since `AppliesTo`'s `_bmad/` marker already covers any BMad module), or (b) something registry-shaped is still needed — and why.
  - [ ] If (a), name the exact extension points: new `BmadModule` enum value, new `ModuleDoc[]` array, new `GlossaryTerm[]` array, new switch arms in `DocsFor`/`GlossaryFor` [ModuleContext.cs:118-123,151-156], and (if the module can coexist with BMM/GDS in one repo) a new `IsXPresent` helper mirroring `IsMethodPresent`/`IsGdsPresent` [ModuleContext.cs:162-170].
  - [ ] Assess whether `ChoosePrimary`'s tie-breaking heuristic [ModuleContext.cs:259-286] needs a third branch for the prioritized module, or whether it's fine for a niche module to lose ties to BMM/GDS by default.

- [ ] **Task 4 — Classify every discovered artifact type per candidate module (AC: #1)**
  - [ ] For each of BMad Builder, Test Architect, and Creative Intelligence Suite (plus any additional module found in Task 2), classify its distinctive artifacts as **mappable** (name the exact target: `ArtifactBundle` field + model type, or the `ModuleContext` doc/glossary extension point), **partially-mappable** (name what maps and what doesn't), or **unsupported** (name why) — noting explicitly that BMad Method and GDS are already covered, not re-classified.
  - [ ] Resolve whether any candidate module's output is close enough to `EpicsModel`/`RequirementsModel`/`SprintStatus`/`RetroModel` shape to reuse those models directly, or whether it's better modeled as pure `ModuleContext` doc/glossary/command additions with `Epics`/`Sprint`/`Requirements`/`Retros` staying null/empty (per NFR8, honest absence) — this is the central modeling question, mirroring 15.1's "does this map to StoryInfo, EpicInfo, or neither" puzzle but for BMad-native modules.
  - [ ] Recommend the single priority module to cover first (AC #1's "recommends a priority module (or modules)") with rationale (e.g. likelihood of real adoption, richness of distinctive on-disk artifacts, size of Story 18.2's resulting scope).

- [ ] **Task 5 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any candidate-module convention richer than the shared model (e.g. BMad Builder's generated `agent.yaml`/`module.yaml` scaffolding, CIS's `design-methods.csv` reference dataset), record it as either a candidate projection extension or an explicit non-goal with rationale.
  - [ ] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188,219-224,262-276] for tone/specificity.
  - [ ] Assess the current BMM-specific next-step-command mapping for generalization to other BMad modules (AC #2's second clause): confirm whether `CommandCatalog`/`BuildContext` [ModuleContext.cs:26-59,291-357] is already module-neutral (it parses `module-help.csv` generically today, keyed by `skill`/`module` columns, not hardcoded to BMM) or whether some caller still assumes BMM/GDS-only vocabulary — check `HowToReadTemplater`/`AboutSddTemplater`'s "Next Steps" panels and the "strongly GDS-oriented … requires generalization" phrasing this AC quotes from the epic's Additional Requirements (search `epics.md`/`architecture.md` for that exact phrase to find its origin before answering).

- [ ] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1-15.1 (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim against `SiteGenerator.cs:51`. State whether Epic 18 actually needs the registry (per Task 3's conclusion) or can proceed via extension alone — this may make Epic 18 the one framework epic that does NOT depend on the registry landing first, which is itself worth stating plainly.
  - [ ] If a registry is still needed for any part of Epic 18, defer to 11.1-15.1's conclusion/ADR if one exists by the time this spike is reviewed; do not propose a competing registry ADR.

- [ ] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (candidate-module table × classification × target projection/extension point + extend-vs-registry decision + non-goals + command-generalization assessment + priority recommendation + 18.2 scope boundary) into this story's **Completion Notes**, mirroring Story 11.1's/12.1's/15.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND none of the sibling spikes already covers it.

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
- Downstream story key (not created by this spike): `18-2-priority-bmad-module-baseline-coverage` (already seeded in epics.md with draft ACs, not yet detailed via create-story)
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-21 — Story 18.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map of BMad's own module ecosystem (BMad Builder, Test Architect, Creative Intelligence Suite) beyond the already-supported BMad Method/GDS, an explicit extend-vs-registry architectural finding (likely differs from the third-party spikes' assumption), a command-generalization assessment, and an 18.2 scope recommendation; no production code. First story of Epic 18 (BMad-native module exploration), distinct from the third-party-framework Epics 11-15; moves Epic 18 from `backlog` to `in-progress`.
