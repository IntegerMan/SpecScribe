---
baseline_commit: f6c58b0722a591353a7ad13ea9d7d8676deef8d7
---

# Story 4.1: Shared Framework Adapter Contract and Projection Path

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer supporting multiple frameworks,
I want a stable adapter contract into one projection model,
so that new framework support does not require rewriting core rendering.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 4.1 (lines 626–644)._

1. **Given** framework-specific parsers are added
   **When** adapters emit normalized records
   **Then** projection and rendering consume a shared host-neutral model
   **And** template and page generators remain framework-agnostic.

2. **Given** unsupported artifact shapes are encountered
   **When** parsing runs
   **Then** unsupported items are categorized and reported as non-fatal
   **And** successful artifacts still render.

## Scope Decision — READ FIRST (confirm or veto before dev)

This is the **foundational contract** for all of Epic 4 (Stories 4.3–4.7 each implement it) and it interlocks with Epic 6 (6.1 view-model contract). Getting the boundary right up front is the whole point of this story — a wrong seam here gets corrected at review across five later stories. The scope below is a deliberate, evidence-backed decision; **if you disagree, raise it before writing code, not at review** (Epic 3 retro action item: don't defer a defining decision to the dev and correct it later).

**This story establishes the INGESTION seam only** — the boundary the architecture spine calls Evolution Sequence step 1 ("Extract current projection and rendering logic into host-neutral core interfaces"): [rendering-architecture.md:111–117](../specs/spec-specscribe/rendering-architecture.md).

**IN scope**
- A named **`IArtifactAdapter`** contract (matches the interface sketch at [rendering-architecture.md:32–35](../specs/spec-specscribe/rendering-architecture.md)): given the source tree + `ForgeOptions`, discover and parse one framework's artifact families into a **normalized bundle + diagnostics**.
- A **normalized bundle** record type that carries the already-host-neutral parsed models the renderer consumes (epics/stories, requirements, sprint, retros, generic docs) — i.e. a typed container, not a re-modeling of them.
- A **`BmadArtifactAdapter`** — the FIRST and (this story) ONLY concrete adapter — that wraps the CURRENT BMad discovery + parsing (today inlined in `SiteGenerator`) and emits the bundle. Behavior must be **byte-for-byte identical** to today's output.
- A **categorized, non-fatal diagnostics channel** (`AdapterDiagnostic` with a severity/category: e.g. `Unsupported`, `Malformed`, `Skipped`) that the adapter emits and the run surfaces without aborting — formalizing today's ad-hoc `GenerationOutcome.Error` events into a typed contract (AC #2).
- `SiteGenerator.GenerateAll` routes its planning-artifact ingestion **through the adapter** instead of calling the parsers directly.

**OUT of scope (belongs to a later story — do NOT start it here)**
- **Decoupling personal-structure assumptions** — ADR location/format, hardcoded group-prefix/filename conventions, `"epics.md"`/`implementation-artifacts/` literals. That is **Story 4.2** ([epics.md:646–664](../planning-artifacts/epics.md)). 4.1 wraps the existing hardcoded discovery *as-is* behind the contract; 4.2 generalizes what's inside the wrapper.
- **New framework adapters** (Spec Kit, GSD, SpecFlow, Squad, Superpowers) — Stories 4.3–4.7. 4.1 delivers the contract they will implement, plus enough of a second-adapter thought-experiment to prove the seam is real, but ships zero new-framework parsing.
- **The downstream host-neutral VIEW-MODEL contract** (renderer → HTML/webview adapters) — that is **Story 6.1** ([epics.md:866–870](../planning-artifacts/epics.md)) / AD-2. 4.1 stops at the ingestion boundary (source → normalized bundle). Templaters keep consuming today's models unchanged.
- **Package/namespace split** (`SpecScribe.Core`, `SpecScribe.Adapters.BMad`, …). This is explicitly **seed, not invariant** — "the current monolithic implementation can be refactored as long as the shared-core contract stays intact" ([ARCHITECTURE-SPINE.md:98–101, 119–124](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). Keep everything in the single `SpecScribe` project/namespace; introduce the types as new files there. A physical split is a later, optional refactor and MUST NOT be attempted in this story.

**The one-line test for "is this in scope?":** if the change is about *the shape of the contract between parsing and rendering*, it's in. If it's about *making the parsing itself framework-tolerant* or *making the rendering host-tolerant*, it's out (4.2 / 6.x respectively).

## Tasks / Subtasks

- [x] **Task 1 — Define the adapter contract + record types** (AC: #1, #2)
  - [x] Add `IArtifactAdapter` with a single ingestion entry point, e.g. `ArtifactBundle Ingest(ForgeOptions options, IReadOnlyList<string> sourceRelativePaths)` (shape it to the sketch at [rendering-architecture.md:32–35](../specs/spec-specscribe/rendering-architecture.md): input = source file set + options; output = normalized records + warnings/errors). It must be able to answer "does this framework apply to this repo?" so 4.3+ adapters can self-select — add a `bool AppliesTo(...)` or equivalent capability check now (thin; BMad's is "an `_bmad/` dir exists", reusing `ModuleContext.Detect` signals).
  - [x] Add an `ArtifactBundle` record (host-neutral container) holding the already-parsed models the generator needs: `EpicsModel?`, `RequirementsModel?` (note: requirements are parsed from the same `epics.md` and depend on the epics model + progress — keep that ordering inside the adapter), `SprintStatus?`, `IReadOnlyList<RetroModel>`, and the `ModuleContext`. Plus the diagnostics list.
  - [x] Add `AdapterDiagnostic` (record) with at least: `Category` (enum: `Unsupported`, `Malformed`, `Skipped`, `Error`), `RelativePath`, `Message`. This is the typed form of AC #2's "categorized and reported as non-fatal."
  - [x] All new types live in the existing `SpecScribe` project/namespace as new `.cs` files. No new project. No package split.

- [x] **Task 2 — Implement `BmadArtifactAdapter`** (AC: #1)
  - [x] Move the BMad-specific discovery + parse currently inlined in `SiteGenerator.GenerateEpicsInternal`/`GenerateAll` behind this adapter: `EpicsParser.Parse`, `RequirementsParser.Parse`, `SprintStatusParser.ParseFile`, `RetroParser` detection+parse, and `ModuleContext.Detect`. Reference the exact current call sites: [SiteGenerator.cs:64](../../src/SpecScribe/SiteGenerator.cs) (sprint), [:90–92](../../src/SpecScribe/SiteGenerator.cs) (retros), [:482–512](../../src/SpecScribe/SiteGenerator.cs) (epics + requirements), [:927](../../src/SpecScribe/SiteGenerator.cs) (module detect).
  - [x] The adapter OWNS the ordering constraints that today live in the generator: retros parsed first (no epics model needed), epics parsed before requirements (requirements roll up from epics + progress). **Do not "fix" or generalize any of the hardcoded `"epics.md"` / `implementation-artifacts/` / retro-filename discovery** — carry it verbatim; generalizing it is Story 4.2.
  - [x] **Progress/git enrichment placement:** `ProgressCalculator.Compute` + `GitMetrics.TryCompute*` currently run inside the epics phase ([SiteGenerator.cs:499–504](../../src/SpecScribe/SiteGenerator.cs)). These are **projection/insight** concerns (AD-4), not ingestion. Keep them in the generator/projection path, running on the bundle's `EpicsModel` — do NOT pull git or progress into the adapter. (This keeps the ingestion↔projection boundary clean and the `--deep-git` gate untouched — see Dev Notes.)

- [x] **Task 3 — Route `SiteGenerator` through the adapter** (AC: #1)
  - [x] `GenerateAll` obtains the `ArtifactBundle` from the adapter, then feeds `EpicsModel`/`RequirementsModel`/`SprintStatus`/retros/module into the existing rendering path. The templaters (`EpicsTemplater`, `RequirementsTemplater`, `SprintTemplater`, `RetroTemplater`, `HtmlTemplater`) and every `Write*`/`Generate*Internal` method **must not change signatures or behavior** — that is AC #1's "template and page generators remain framework-agnostic," and it's already true today; this story must preserve it.
  - [x] Preserve the watch-mode incremental paths: `RegenerateEpics`, `RegenerateAdrs`, `GenerateOne`, `RemoveFor` must keep working. The cleanest bounded approach is to have the adapter expose the re-parse the watch paths need, or keep watch paths calling the same underlying parse the adapter now wraps — **do not regress watch behavior** (AD-5). Call this out explicitly in Completion Notes with how you kept parity.

- [x] **Task 4 — Wire the categorized non-fatal diagnostics** (AC: #2)
  - [x] Adapter collects `AdapterDiagnostic`s for artifacts it discovers but cannot fully interpret (unsupported shape) or that fail to parse (malformed) — **without throwing**. Today a per-file parse failure already degrades to a `GenerationOutcome.Error` event and the run continues ([SiteGenerator.cs:580–583, 618–626](../../src/SpecScribe/SiteGenerator.cs)); route those through the new diagnostic category so the channel is one typed thing, and confirm "successful artifacts still render" is preserved (it is today — keep it).
  - [x] Surface diagnostics on the existing reporting path (return them alongside `GenerationEvent`s, or map `AdapterDiagnostic` → a `GenerationEvent` with a category). Do NOT invent a new console UI — reuse the `IReadOnlyList<GenerationEvent>` return + `IGenerationReporter` surface ([GenerationReporter.cs](../../src/SpecScribe/GenerationReporter.cs)).
  - [x] Honor `IsIgnored` ([SiteGenerator.cs:1236–1243](../../src/SpecScribe/SiteGenerator.cs)) — ignored files are neither rendered nor reported as unsupported.

- [x] **Task 5 — Tests** (AC: #1, #2)
  - [x] `BmadArtifactAdapter` unit tests: a representative BMad source set produces a bundle with the expected `EpicsModel`/requirements/sprint/retros/module; ordering constraints hold; `AppliesTo` is true for a `_bmad/` repo and false without one.
  - [x] Diagnostics tests: a malformed/unparseable artifact yields a categorized `AdapterDiagnostic` (non-fatal) AND the sibling valid artifacts still appear in the bundle (AC #2).
  - [x] **Golden-output regression:** assert the full generated site is byte-for-byte unchanged vs. before the refactor for a fixture repo — this is the single most important test (AC #1's "remain framework-agnostic" = no rendered output changed). Reuse the existing `SiteGenerator*Tests` fixtures/patterns (see Testing Requirements). Add to the existing test project (`net10.0`, xUnit).

- [x] **Task 6 — Verify baseline unchanged end-to-end** (AC: #1, #2)
  - [x] Run `dotnet test` (whole suite green — 39 existing test files must still pass unchanged; if any existing assertion must change, that's a signal you altered rendered output — stop and reconsider).
  - [x] Generate this repo's own site and diff the output tree against a pre-change build: **zero diffs** expected. Output dir is `SpecScribeOutput` (NOT `docs/live`; see Project Structure Notes).

## Dev Notes

### What "shared host-neutral model" already means here (don't re-invent it)
The parsed models (`EpicsModel`, `RequirementsModel`, `ProgressModel`, `SprintStatus`, `RetroModel`, `DocModel`, `ArtifactCoverage`) are **already host-neutral C# records** — plain data, zero HTML, consumed by templaters that turn them into HTML. AC #1's "shared host-neutral model" is largely **already satisfied**; the gap this story fills is that there is **no named adapter boundary** producing them. Today `SiteGenerator` IS the BMad ingestion orchestrator with BMad knowledge inlined. This story extracts that into `IArtifactAdapter` + `BmadArtifactAdapter` so Stories 4.3–4.7 have a contract to implement. **Do not re-shape the existing models** — wrap, don't rewrite.

### The existing generalization seams — study these; they are the pattern to match
The codebase already practices "one classifier / one seam that Epic 4 generalizes." Your contract should feel like the natural home for these, not a competing abstraction:
- **`ArtifactCoverage.Specs`** ([ArtifactCoverage.cs:86–109](../../src/SpecScribe/ArtifactCoverage.cs)) — literally commented "THIS list is the coverage seam Epic 4 generalizes — a future framework adapter swaps this family set." The canonical-family set is the shape a framework varies. (You do NOT have to move it in 4.1 — but the adapter is its eventual owner; note the relationship.)
- **`ModuleContext.Detect`** ([ModuleContext.cs:124–175](../../src/SpecScribe/ModuleContext.cs)) — already data-driven module detection with graceful `None` fallback; comment at [:71–75](../../src/SpecScribe/ModuleContext.cs) says "folder layout varies; Epic 4 will generalize." This is essentially a proto-adapter-selector. Reuse it for `BmadArtifactAdapter.AppliesTo`.
- **`CommandCatalog`** ([ModuleContext.cs:19–52](../../src/SpecScribe/ModuleContext.cs)) — command prefixes already come from data (`module-help.csv`), not hard-coding. This is the model for "framework-specific content flows through the adapter" (NFR8).

### Non-negotiable invariants (from the architecture spine)
- **AD-1** ([ARCHITECTURE-SPINE.md:34–40](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): "framework parsing and projection live in one core pipeline; adapters only translate that core output into host delivery concerns." Your `IArtifactAdapter` is the **ingestion** adapter (source → records); it is NOT a delivery adapter. Keep the two ideas distinct in naming so nobody confuses this with the HTML/webview delivery adapters of AD-2/Epic 6.
- **AD-4** ([ARCHITECTURE-SPINE.md:58–64](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): insight providers (git pulse, coverage, ADR) are additive, non-blocking, independently toggleable. **Keep git/progress/coverage OUT of the ingestion adapter** — they enrich the model in the projection path, gated as today. This is why Task 2 keeps `ProgressCalculator`/`GitMetrics` in the generator.
- **NFR2 / NFR4 / NFR8** ([epics.md:76, 78, 82](../planning-artifacts/epics.md)): degrade gracefully on partial/malformed/missing (that's AC #2); extensible so new adapters need no core rewrite (that's the whole point of AC #1); framework-specific content flows through the contract and surfaces degrade to *absent, not broken*.
- **The `--deep-git` single-numstat discipline** (memory: deep-git-single-numstat-path): do not add git work; do not move the `_options.DeepGitAnalytics` gate at [SiteGenerator.cs:503](../../src/SpecScribe/SiteGenerator.cs). It stays exactly where it is.

### Current end-to-end pipeline (what you're wrapping)
`Commands.cs:22` → `new SiteGenerator(options)` → `ConsoleUi.cs:115` → `GenerateAll(reporter)`. Inside `GenerateAll` ([SiteGenerator.cs:41–192](../../src/SpecScribe/SiteGenerator.cs)) the order is: wipe output → scaffold → parse sprint → build nav (detect module) → README → parse retros → **epics phase** (parse epics + requirements, compute git+progress, render epic/story pages) → retro pages → generic pages → ADRs → commit-day pages → deep-analytics → git-insights hub → coverage → sprint page → structure → retro index → action items → index. **Only the parse steps move behind the adapter; every render/Write step stays put.**

### Risk centers (where reviews will focus)
1. **Watch-mode parity** — `RegenerateEpics`/`RegenerateAdrs`/`GenerateOne`/`RemoveFor` re-parse incrementally. If the adapter only supports a full ingest, watch paths must still re-parse correctly. Simplest safe path: keep the underlying parser calls reachable for watch, or give the adapter a scoped re-ingest. Document your choice.
2. **Output drift** — any change to rendered bytes fails AC #1. The golden-output test (Task 5) is your guardrail; run it early.
3. **Diagnostics double-reporting** — don't emit both a legacy `Error` event and a new diagnostic for the same failure.

### Project Structure Notes
- Single project: `src/SpecScribe/SpecScribe.csproj` (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). All new files go here in `namespace SpecScribe;`. **No new project, no namespace split** (seed-level, deferred — [ARCHITECTURE-SPINE.md:100–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- Tests: `tests/SpecScribe.Tests/` (xUnit 2.9, `net10.0`). Follow existing file-per-unit naming (`BmadArtifactAdapterTests.cs`, etc.).
- **Output dir is `SpecScribeOutput`** (memory: generate-output-dir-is-specscribeoutput). Never `--output docs/live`.
- This session/story runs on `main` (not a worktree). Edits target `C:\Dev\SpecScribe` directly. Note (memory: worktree-edits-must-target-worktree-path) there is a background auto-committer on main — expect edits to be committed/pushed; keep commits coherent.
- No `.editorconfig`-level surprises observed; match the heavy XML-doc-comment style of the surrounding files (every public type/member carries a `<summary>` explaining the *why*, often with a `[Story N.M]` tag — follow that convention and tag new members `[Story 4.1]`).

### References
- [epics.md:620–644](../planning-artifacts/epics.md) — Epic 4 goal + Story 4.1 ACs (source of truth).
- [epics.md:36](../planning-artifacts/epics.md) — FR1 (the requirement this story implements): "framework adapter contract that maps each supported framework into one shared projection model without rewriting the core HTML templating pipeline."
- [epics.md:76,78,82](../planning-artifacts/epics.md) — NFR2 (graceful degradation), NFR4 (extensible, no core rewrites), NFR8 (framework-specific content flows through the adapter; degrade to absent-not-broken).
- [rendering-architecture.md:12–61, 100–124](../specs/spec-specscribe/rendering-architecture.md) — layer boundaries, the `IArtifactAdapter`/`IProjectionPipeline` interface sketches, package boundaries (seed), and the Evolution Sequence (step 1 = this story).
- [ARCHITECTURE-SPINE.md:34–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1 (shared core), AD-2 (view-model contract = Epic 6, not here), AD-4 (insight providers non-blocking), "Seed, Not Invariant" (no package split required).
- [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) — current ingestion orchestrator; the parse call sites to wrap (see Task 2 line refs).
- [ArtifactCoverage.cs:80–117](../../src/SpecScribe/ArtifactCoverage.cs), [ModuleContext.cs](../../src/SpecScribe/ModuleContext.cs) — existing "seam Epic 4 generalizes" prior art to match.

## Dev Agent Record

### Agent Model Used

Claude Fable 5 (claude-fable-5) via Claude Code, 2026-07-09

### Debug Log References

- Byte-for-byte golden diff, run 1 (no-git fixture, frozen copy of this repo's artifacts, 156 output files): **0 diffs** after normalizing only the wall-clock footer (`on yyyy-MM-dd HH:mm`) and the build-derived asset cache-bust token (`?v=<ModuleVersionId>` — changes on every rebuild by design).
- Byte-for-byte golden diff, run 2 (same fixture with a frozen 2-commit git history, `--deep-git` on, baseline code built from a `f6c58b0` worktree, 160 output files): **0 diffs** after additionally normalizing a CRLF-vs-LF artifact in the mermaid bootstrap block — traced to `git worktree` checkout `autocrlf` re-normalizing `Mermaid.cs` (a file this story never touched; content identical per `git diff`), i.e. a comparison-methodology artifact, not a behavior change.
- Full suite: 598 tests passing (586 pre-existing, unchanged; 12 new).
- Smoke: generated this repo's own site to `SpecScribeOutput` (162 files, exit 0).

### Completion Notes List

- **Contract shape (Task 1):** `IArtifactAdapter` = `AppliesTo(options, sourceFiles)` + `Ingest(options, sourceFiles, projectProgress)`. The one deliberate addition to the story's sketch signature is the `ProgressProjection` delegate parameter: BMad's requirements parse REQUIRES the progress model (same-source roll-up), but progress/git are projection concerns that must stay in the generator (Task 2's placement rule / AD-4). The delegate resolves that tension — the generator supplies the enrichment, the adapter controls WHEN it runs, so the epics → progress → requirements ordering lives inside the adapter while every byte of git/progress code (and the `--deep-git` gate, untouched at `SiteGenerator.ComputeProgress`) stays in the projection path.
- **Ingest takes full paths** (not the sketch's `sourceRelativePaths`): the wrapped parsers and discovery all operate on full paths today; relatives are derived inside the adapter exactly as the generator derived them. Input is still "source file set + options" per the sketch.
- **`ArtifactBundle`** carries `Module`, `Sprint?`, ordered `Retros`, `Epics?` (story artifacts pre-resolved), `Requirements?`, `EpicsSourceFullPath?`, `StoryArtifactsById`, `ConsumedSourceRelatives`, `Diagnostics`. Generic docs stay in the per-file render pass (they are converted, not parsed-to-model); per-story artifact FRAGMENT extraction (task list, blurb split, AC sections) deliberately stays in the render loop — re-seating those is a 4.2/6.1 contract question, noted in `RenderEpicsPages` docs.
- **Partial-failure parity:** the old inline chain assigned `_epicsModel`/`_progress` after progress compute and `_requirements` after requirements parse, so a mid-chain exception left earlier models cached. Replicated exactly: the adapter builds the bundle progressively inside one try/catch, and the generator caches each model only when it was produced (`progress != null` gates epics+progress; `Requirements != null` gates requirements) and renders epics pages only when the whole chain completed — the same write set as before.
- **Watch-mode parity (Task 3, risk center #1):** `RegenerateEpics` now calls the adapter's scoped `IngestEpics` (a public BMad-specific member, deliberately NOT on the `IArtifactAdapter` contract) — the exact epics+artifacts+requirements re-parse it always did, without re-ingesting sprint/retro/module state it never refreshed. `GenerateOne`/`RemoveFor`/`RegenerateAdrs` don't touch planning-artifact parsing and are unchanged. Existing watch-path tests (`SiteGeneratorStoryEpicPagesTests`, `SiteGeneratorCoverageTests`) pass unchanged; a new generation-level regenerate test added.
- **Diagnostics (Task 4):** `AdapterDiagnostic(Category, RelativePath, Message)` surfaces via `SiteGenerator.MapDiagnostics` onto the existing `GenerationEvent` list — `Malformed`/`Error` → `Error` outcome (exactly how the epics parse failure already reported), `Unsupported`/`Skipped` → `Skipped`. No double-reporting: a failure the adapter caught is reported ONLY via its diagnostic; render-time failures keep their existing event paths. Two real emission sites today: a present-but-uninterpretable `sprint-status.yaml` → `Unsupported` (previously silent), and a throwing retro/epics parse → `Malformed` (a throwing retro previously aborted the whole run — now siblings render, which is AC #2 verbatim). `IsIgnored` moved to `PathUtil.IsIgnoredSourceFile` so the adapter honors the same ignore set (ignored files neither ingested nor diagnosed).
- **README linkify ordering preserved:** the README has always rendered BEFORE the epics parse (so it linkifies against the previous run's models — null on a first run). Ingest now happens earlier, so the generator deliberately defers caching the bundle's models until after the README render to keep those bytes identical.
- **AppliesTo:** `_bmad/` dir existence, per the story. Deliberately NOT consulted by `SiteGenerator` in 4.1 — a bare `_bmad-output` tree without an install (every test fixture) must keep rendering; the adapter registry of 4.3+ is its consumer.

### File List

- `src/SpecScribe/IArtifactAdapter.cs` — new: ingestion contract + `ProgressProjection` delegate
- `src/SpecScribe/ArtifactBundle.cs` — new: normalized host-neutral bundle record
- `src/SpecScribe/AdapterDiagnostic.cs` — new: diagnostic record + `AdapterDiagnosticCategory` enum
- `src/SpecScribe/BmadArtifactAdapter.cs` — new: first concrete adapter (wraps existing BMad discovery/parsing verbatim; owns `EpicsIngest` scoped re-ingest for watch)
- `src/SpecScribe/SiteGenerator.cs` — modified: `GenerateAll`/`RegenerateEpics` route through the adapter; `GenerateEpicsInternal` → render-only `RenderEpicsPages`; added `ComputeProgress`/`MapDiagnostics`/`SetRetros`; removed relocated discovery helpers (`SprintSourcePath`, `FindEpicsSourceFile`, `BuildArtifactMap`, `ParseRetros`, `ArtifactFilenamePattern`)
- `src/SpecScribe/PathUtil.cs` — modified: `IsIgnoredSourceFile` moved here (shared by generator + adapters)
- `tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs` — new: 9 adapter unit tests (bundle contents, ordering, `AppliesTo`, malformed/unsupported diagnostics, ignore filter)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — new: 3 generation-level tests (golden output inventory, AC #2 diagnostics surfacing with sibling rendering, watch-mode regenerate parity)

## Change Log

- 2026-07-09 — Story 4.1 implemented: `IArtifactAdapter`/`ArtifactBundle`/`AdapterDiagnostic` contract added; `BmadArtifactAdapter` wraps the existing BMad ingestion verbatim; `SiteGenerator` routes full and watch-mode generation through the adapter with byte-for-byte identical output (verified by before/after diffs of a frozen fixture, with and without git/deep-git). 12 new tests; 598 total passing.
