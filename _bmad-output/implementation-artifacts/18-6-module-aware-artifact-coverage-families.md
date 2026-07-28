---
baseline_commit: 6017c2cd2d4e3928c4713ac369ac401fd9f1dbb7
---

# Story 18.6: Module-Aware Artifact Coverage Families

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a team using a BMad module other than BMM,
I want the dashboard's artifact-coverage panel to reflect my module's artifact families rather than BMad Method's,
so that the portal stops reporting eight missing artifacts my methodology never produces.

## Why this story exists (read first)

This is **ADR 0015 Decision 5a** — the last un-closed surface from the Story 18.1 spike's Finding 3b table, and
the only one of that table's rows marked *"Yes — and NOT closed by Decisions 1/2"*. Story 18.2 fixed module
**identity**; `ArtifactCoverage` never consults module identity at all, so the identity fix does not reach it.
Story 18.5 deliberately declined it (its owner decision **D4**: *"A TEA-only repo's dashboard still asserts eight
missing BMM families. That is seated as new Story 18.6."*).

`ArtifactCoverage.Specs` is a `private static readonly IReadOnlyList<FamilySpec>` of **eight hardcoded BMM
families** (PRD · Product Brief · Architecture · UX · Spec Kernel · Epics · Stories · Requirements), and
`ArtifactCoverage.Build` takes `sourceRelativePaths` alone — there is no `BmadModule` parameter anywhere in the
file. The class's own doc comment already names the seam this story cuts: *"THIS list is the coverage seam
Epic 4 generalizes — a future framework adapter swaps this family set, not the panel or the builder."*

### The defect is reachable, and here is the exact path (verified at create-story)

ADR 0015 asserted the defect but did not prove it was reachable past the existing omission gate. It is, and the
mechanism matters because it constrains the fix:

`ArtifactCoverage.IsEmpty` is `!Families.Any(f => f.Present)`, and the panel renders only on
`view.Coverage is { IsEmpty: false }` [`HtmlRenderAdapter.Dashboard.cs`, the `coverage-panel` block]. So a repo
where **none** of the eight match already omits the panel by Story 1.1 graceful omission. The naive reading —
"a TEA-only repo shows eight missing families" — is therefore **wrong**; it shows nothing.

But **`bmad-spec` is a Core skill, not a BMM skill.** Verified against this repo's own install:
`_bmad/core/module-help.csv` lists `Core,bmad-spec,Spec` (alongside `bmad-brainstorming`, `bmad-forge-idea`,
`bmad-index-docs`, `bmad-shard-doc`), while `_bmad/bmm/module-help.csv` does not. Core ships with **every**
module install. So a CIS-, TEA- or BMB-only repo that ran `/bmad-spec` has a genuine `specs/*/SPEC.md`, the
**Spec Kernel** family is Present, `IsEmpty` is false, and the panel renders **1 present + 7 missing BMM
families** on a project that never had a PRD, epics, stories or an FR/NFR catalog. The same holds for any
core-produced file that trips a family predicate.

**That is the defect this story closes**, and it is why the fix must key on module identity rather than on
tightening the present-count gate.

## Owner design decisions (elicited at create-story — do not re-litigate)

Three forks, each closed by the owner before drafting.

**D1 — A repo with NO detected BMad module keeps the eight families.** `ModuleContext.None` carries
`Module = BmadModule.Unknown` (no `_bmad/` directory, or nothing parsed). Only a **detected-but-unmodeled**
module loses the panel. An undetected repo asserts nothing about a methodology, and the panel's present/missing
data is still source-derived truth. This also keeps the existing generation-level tests green — see the trap
below.

**D2 — An `Unmodeled` primary declares an EMPTY family set and the panel omits entirely.** ADR 0015 Decision 5a
is taken literally. The Core-skill finding above is real and would justify a core-only family set (Spec Kernel),
but that would amend a ratified ADR and re-introduce a near-empty one-card panel. Rejected. **Do not** partially
implement it by "just hiding the missing cards" — the whole panel goes.

**D3 — Silent omission on the page, but it is logged.** No dashboard markup, no acknowledgement line, no new
`DashboardView` field — AC #1 says *"omits the panel entirely"* and the dashboard is already dense. But the
omission must be **recorded as a non-fatal diagnostic** so the diagnostics page explains it rather than the panel
vanishing without trace. (This is a deliberate departure from Story 18.2's Decision 2c, which rendered a *named
acknowledgement* where the glossary would be. The glossary slot is a prose section on an always-rendered
explainer page; this is a data panel on the dashboard.)

## Acceptance Criteria

1.
**Given** a repository whose primary BMad module is not BMad Method or Game Dev Studio
**When** the dashboard's artifact-coverage panel renders
**Then** the canonical family set is resolved from the detected module rather than from the hardcoded BMM list, so families the module does not produce are never reported as missing
**And** a module with no modeled family set omits the panel entirely rather than showing an empty or all-missing one (NFR8: absent, not misleadingly empty).

2.
**Given** a BMad Method or Game Dev Studio repository
**When** the change lands
**Then** the existing eight-family panel, its create-command affordances, and its freshness/staleness behavior are unchanged
**And** the existing test suite and the golden byte-parity gate stay green (or any intentional change is re-baselined).

[Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.6]

### Reading AC #1 against D1/D2 — the resolution table

This is the whole behavioral contract. Implement exactly this:

| `ModuleContext` state | `Module` | Family set | Panel | Diagnostic |
|---|---|---|---|---|
| No `_bmad/`, or nothing parsed (`ModuleContext.None`) | `Unknown` | the eight | renders (as today) | none |
| BMM primary | `BmadMethod` | the eight | renders (as today) | none |
| GDS primary | `GameDevStudio` | **the eight** — see the trap below | renders (as today) | none |
| `cis` / `tea` / `bmb` / any BMB-minted code | `Unmodeled` | **empty** | **omits** | one `Informational` (D3) |

**AC #2 locks GDS to the eight BMM families.** A Game Dev Studio repo produces `gdd.md`,
`narrative-design.md` and `game-architecture.md` (see `ModuleContext.GameDevStudioDocs`) — *not* a PRD or a
product brief — so today's panel is arguably wrong for GDS too. **Modeling a GDS-specific family set in this
story violates AC #2** and moves the golden fingerprint. It is a real observation; record it in Completion Notes
as a candidate follow-up and leave the behavior alone.

## The trap: five existing tests run with no `_bmad/` at all

`SiteGeneratorCoverageTests` builds its fixture with `Directory.CreateTempSubdirectory` and creates only
`_bmad-output/planning-artifacts`, `_bmad-output/implementation-artifacts` and `docs/adrs` — **no `_bmad/`
directory**. Every one of its five generation-level tests therefore runs against `ModuleContext.None` /
`BmadModule.Unknown`:

- `GenerateAll_PresentFamilyCardLinksToTheActualGeneratedPage`
- `GenerateAll_MalformedMemlogUpdatedDate_ContributesNoEnrichmentAndDoesNotThrow`
- `GenerateAll_MalformedMemlogAlongsideValidOne_ValidOneStillEnrichesItsFamily`
- `GenerateOne_RefreshesCoveragePanelWithoutAFullRegenerate`
- `RegenerateEpics_RefreshesCoveragePanelForNewlyAddedStoryArtifacts`

Under **D1** they stay green untouched — that is a large part of why D1 was chosen. **If you find yourself
adding `_bmad/bmm/module-help.csv` fixtures to make them pass, you have implemented the wrong decision.** The
same applies to `HtmlTemplaterTests`, `IconsTests` and `ChartsTests`, which call `ArtifactCoverage.Build`
directly with no module concept at all.

## Implementation shape (decided — do not re-derive)

### Where the family set lives: `ArtifactCoverage`, not `ModuleContext`

`ArtifactCoverage` already depends on `ModuleContext.WellKnownDocs`; the reverse dependency does not exist.
Putting a `FamiliesFor` switch on `ModuleContext` would force the private `FamilySpec` record (which carries
`Func<string, bool>` predicates) to become public and would invert the layering. Keep `FamilySpec` private and
add a private `SpecsFor(BmadModule)` inside `ArtifactCoverage`. The class comment already promised this seam.

### The three statics that must become module-aware

All three are BMM-family-set consumers and all three break the moment the set varies:

| Symbol | Today | Becomes |
|---|---|---|
| `ArtifactCoverage.Build(paths, mtimes, memlogs, today)` | reads `Specs` | takes `BmadModule` |
| `ArtifactCoverage.AllCandidatePaths(paths)` | reads `Specs` | takes `BmadModule` |
| `ArtifactCoverage.CreateStepKeys` (a `static readonly` dictionary) | derived from `Specs` at type-init | becomes `CreateStepKeysFor(BmadModule)` |

**Key on `BmadModule`, not the module code string.** Story 18.5 keys TEA coverage on the code `"tea"` because it
covers one specific unmodeled module; this story only needs modeled-vs-unmodeled plus which modeled one, and
`DocsFor(BmadModule)` / `GlossaryFor(BmadModule)` are the established precedent. Do not add a `BmadModule` enum
case for anything (ADR 0015 Decisions 1/2 are open-world on purpose).

**Make the new parameter REQUIRED — no default value.** ~28 test call sites will need a mechanical edit; take
that cost. A `BmadModule module = BmadModule.BmadMethod`-style default is precisely the "silently inherits BMM"
shape Epic 18 exists to kill, and it would let a future caller re-introduce the bug by omission.

### `SpecsFor` polarity — the thing a reviewer will try to "fix"

```
BmadModule.Unmodeled => empty,
_                    => the eight,   // Unknown (D1), BmadMethod, GameDevStudio (AC #2)
```

This is the **opposite** polarity from `ModuleContext.DocsFor` / `GlossaryFor`, whose `_ =>` arm returns empty.
That asymmetry is deliberate (D1) and must carry an explicit comment saying so with the D1 rationale, or the
next reader will "align" it and silently delete the panel from every non-BMad repo.

### The panel omission needs no new gate code

An empty `Families` list makes `IsEmpty` (`!Families.Any(f => f.Present)`) true, which the existing
`view.Coverage is { IsEmpty: false }` guard already honors. `CoverageMeter`'s `total > 0` guard is likewise
already safe. **Do not add a second gate** in `DashboardViewBuilder` or `HtmlRenderAdapter.Dashboard.cs` — one
omission rule, in one place. Verify this by test rather than by adding belt-and-braces.

`Charts.FamilyAccentClass` and `SiteGenerator.ResolveFamilyHref` are both `label switch` with `_ =>` fallbacks
and need no change for an empty or differing set. Confirm rather than assume.

### The D3 diagnostic

Follow `SiteGenerator.AppendCountDivergenceNotice` exactly — it is the in-file template for a non-adapter
diagnostic reaching the diagnostics page (`MapDiagnostics(new[]{ new AdapterDiagnostic(...) })` appended to the
run's `events` list).

- Category **`Informational`**. Do **not** invent a sixth `AdapterDiagnosticCategory`.
- Subject/anchor: reuse `ModuleContext.RepoRelativeCsv(code)` with `DiagnosticAnchorRoot.Repo`, matching 18.2's
  `ReportUnmodeledPrimary`. `RepoRelativeCsv` is **private** today — widen it to `internal`
  (`InternalsVisibleTo="SpecScribe.Tests"` is already declared in `SpecScribe.csproj`). **Do not duplicate the
  `_bmad/{code}/module-help.csv` literal** — that is a second source of truth for a path 18.2 centralized.
- Wording, distinct from 18.2's docs/glossary notice so the two do not read as duplicates:
  `Primary BMad module '{code}' ({label}) has no modeled planning-artifact family set, so the dashboard's Planning Artifacts panel is omitted.`
- **Cardinality: at most one per generate run** (ADR 0015 Decision 2d). Emit it from the `GenerateAll` events
  path, next to `AppendCountDivergenceNotice`. **Never from `BuildArtifactCoverage`**, which `RefreshCoverage()`
  calls on *every* watch incremental (`GenerateOne` / `RemoveFor` / `RegenerateEpics` / `RegenerateAdrs`) — that
  would accumulate a diagnostics row per keystroke, the exact failure 2d forbids. Check whether
  `RegenerateEpics` needs the same re-emission `AppendCountDivergenceNotice` gets there, and say which you chose
  and why in Completion Notes.
- Prefer a **separate** diagnostic over extending 18.2's `ReportUnmodeledPrimary` message: the subject is
  different, and `ModuleContext` should not learn what a dashboard panel is. If you conclude otherwise, record
  the reasoning — do not just do it.

## Tasks / Subtasks

- [x] **Task 1 — Confirm the baseline before changing anything (AC: #1, #2)**
  - [x] Re-read `src/SpecScribe/ArtifactCoverage.cs`, `SiteGenerator.BuildArtifactCoverage` /
        `ResolveFamilyHref` / `RefreshCoverage`, `Charts.CoverageMeter` / `ArtifactCoveragePanel` /
        `FamilyAccentClass`, and the `coverage-panel` block in `HtmlRenderAdapter.Dashboard.cs`. The tree is
        shared and Story 18.5 may be in flight in the same files.
  - [x] Re-verify the Core-skill finding against this repo's install: `_bmad/core/module-help.csv` contains
        `bmad-spec`, `_bmad/bmm/module-help.csv` does not. If upstream has moved, correct this story in place
        and say so — the reachability argument is the story's premise.
  - [x] Confirm `ModuleContext.None.Module == BmadModule.Unknown` and that `_module` is assigned
        (`_module = bundle.Module`) **before** `_coverage = BuildArtifactCoverage(sourceRelatives)` runs in
        `GenerateAll`. Both are true at baseline `6017c2c`; prove it, do not assume it.

- [x] **Task 2 — Red first: fixtures + failing assertions (AC: #1, #2)**
  - [x] Add a generation-level fixture with `_bmad/cis/module-help.csv` (real upstream bytes, provenance block
        with the commit SHA, per `ModuleContextTests.cs`'s convention and ADR 0015 Decision 7) **plus** a
        `_bmad-output/specs/spec-x/SPEC.md` — the exact reachable case from the premise above. Assert it renders
        the panel **today** (proving the defect) and omits it after the fix.
  - [x] Add a **BMM control** fixture (`_bmad/bmm/module-help.csv` + the eight families' files) asserting the
        panel and all eight cards are unchanged.
  - [x] Add a **GDS control** asserting the eight BMM families still render for `gds` (AC #2's explicit lock).
  - [x] Add a **no-`_bmad/`** assertion (D1) — the existing `SiteGeneratorCoverageTests` fixture already is
        this case; add one explicit test that names D1 so the behavior is protected by intent, not by accident.

- [x] **Task 3 — Cut the seam in `ArtifactCoverage` (AC: #1)**
  - [x] Add private `SpecsFor(BmadModule)` with the polarity and comment specified above; keep `FamilySpec`
        private.
  - [x] Thread a **required** `BmadModule` parameter through `Build` and `AllCandidatePaths`; replace the
        `CreateStepKeys` static dictionary with `CreateStepKeysFor(BmadModule)`.
  - [x] Update the class/`Specs` doc comments: the seam the comment promised is now cut, and the comment should
        say what governs the set (module identity) and what the `Unknown` arm means (D1).
  - [x] Mechanically update every call site — `SiteGenerator.BuildArtifactCoverage` (both `Build` calls and the
        `AllCandidatePaths` call) and the ~28 test call sites across `ArtifactCoverageTests`,
        `HtmlTemplaterTests`, `IconsTests`, `ChartsTests`. **`ChartsTests` needed no change** — it builds an
        `ArtifactCoverage` object initializer directly and never calls `Build`. 28 call sites in the other three.

- [x] **Task 4 — Wire module identity into the coverage build (AC: #1)**
  - [x] `BuildArtifactCoverage` passes `_module.Module`. Both the full-generate call
        (`GenerateAll`) and the watch `RefreshCoverage()` path go through it, so both become module-aware with
        one change — confirm the watch path in a test, since `_module` persists across incrementals.
  - [x] Do **not** call `ModuleContext.Detect` again. 18.2 made detection once-per-run on purpose; read
        `_module`.

- [x] **Task 5 — The D3 diagnostic (AC: #1)**
  - [x] Implement per the D3 spec above: `Informational`, `RepoRelativeCsv` + `DiagnosticAnchorRoot.Repo`,
        widened to `internal`, emitted once per generate run from the events path, never from
        `BuildArtifactCoverage`.
  - [x] Test both cardinality (one row, not N) and that a watch rebuild does not accumulate rows.
  - [x] Test that a BMM / GDS / no-`_bmad` repo emits **zero** such diagnostics.

- [x] **Task 6 — Regression + golden gate (AC: #2)**
  - [x] Run the full suite. **Known suite property, do not misread it:** a rotating subset of the deep-git test
        family (`SiteGeneratorTimelineTests`, `SiteGeneratorGitInsightsTests`, `GitMetricsFirstCommitDateTests`,
        …) fails under concurrent load and passes in isolation — Story 18.2 measured 19 such failures, all
        outside its own classes. Confirm any failure is in a class **this** story touches before calling the
        suite red.
  - [x] `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`
        [`SiteGeneratorAdapterTests`] **must not move.** This repo's primary is BMM, so output is byte-identical
        by construction — a moved fingerprint means the change leaked into the modeled path. If it moves,
        confirm the move is yours before re-baselining ([[golden-diff-normalization-gotchas]]) and confirm the
        new hash is stable across two repeated runs. **It did not move — no re-baseline needed.**

- [x] **Task 7 — Live-browser verification (CLAUDE.md § Verification)**
  - [x] Generate this repo (BMM) to `SpecScribeOutput/` and confirm the Planning Artifacts panel renders
        identically — eight cards, meter, chips, tooltips, create commands, no layout shift.
  - [x] Generate the unmodeled fixture and confirm in a real browser that the panel is **absent** — not empty,
        not zero-height, not a collapsed `.chart-panel` leaving a gap or an orphaned grid row in the dashboard
        layout. The suite structurally cannot see sub-pixel collapse; this is why the check exists.
  - [x] Confirm the diagnostics page shows the one `Informational` row with its word-bearing badge (status is
        never signalled by colour alone).
  - [x] Never `--output docs/live`. Add a `.claude/launch.json` entry following that file's convention if a
        preview slot is needed.

- [x] **Task 8 — Record the decisions (AC: #1, #2)**
  - [x] ADR 0015 Decision 5a is **implemented, not amended** — D2 takes it literally. **Do not author a new
        ADR.** If implementation forces a deviation from 5a, stop and propose an ADR amendment rather than
        shipping a divergence (CLAUDE.md § Decision records). **No deviation arose; no ADR authored.**
  - [x] Record in Completion Notes: the Core-skill reachability proof, the GDS-family-set observation as a
        candidate follow-up, and the `RegenerateEpics` re-emission choice from Task 5.

## Dev Notes

### Gating and sequencing — read this, `epics.md` overstates it

`epics.md`'s seating comment says this story is *"sequenced after 18.5 because 18.5 establishes the per-module
coverage model this story swaps the family set against."* **That dependency does not hold in code.** Story 18.5
builds a *different* surface — a new **Module Coverage** panel plus a `CoverageTier` vocabulary for TEA
artifacts — and touches neither `ArtifactCoverage.Specs` nor the Planning Artifacts panel. This story's real
gate is **Story 18.2**, which has landed (`review`): `BmadModule.Unmodeled`, `ModuleContext.Code`, `.IsUnmodeled`,
`.IsModeled`, `RepoRelativeCsv`, `DiagnosticAnchorRoot.Repo`, and detect-once-per-run are all present at HEAD.

Practical consequence: **18.6 is not blocked by 18.5.** If 18.5 runs first or concurrently, expect merge
friction — both stories touch `DashboardViewBuilder.cs`, `DashboardView.cs`, `HtmlRenderAdapter.Dashboard.cs`
and `SiteGenerator.cs`. Re-read those files immediately before editing and re-grep after
([[shared-main-concurrent-edit-loss-verify-after-edit]]). Sequencing is the owner's call; the technical fact is
that nothing here waits on 18.5.

**Naming hazard.** After 18.5 the dashboard carries a "Module Coverage" panel *and* a "Planning Artifacts"
(artifact-coverage) panel, while `ArtifactCoverage` already owns the word "coverage" — the same collision
[[coverage-epics-seeded-25-5-25-6-epic-27]] records against FR42. Do not rename either panel in this story; just
do not conflate them in code, comments or tests.

### What 18.2 already landed (do not rebuild it)

`BmadModule { Unknown, BmadMethod, GameDevStudio, Unmodeled }`; `ModuleContext.Code`, `.IsModeled`,
`.IsUnmodeled`; `ModuleForCode`, `ModeledModuleLabels`, `RankCandidates`, `ReservedModuleNames`,
`RepoRelativeCsv` (private), `DiagnosticAnchorRoot.Repo`; `CommandCatalog.Empty.ModuleLabel == ""`;
`Detect(repoRoot, sourceRelatives, List<AdapterDiagnostic>? diagnostics = null)`; detect-once-per-run
(`SiteGenerator.BuildNav` no longer re-detects).

### Non-goals (explicit — do not widen)

- **A GDS-specific family set.** AC #2 forbids it. Observe it, log it as a follow-up candidate, leave it.
- **A core-only family set for unmodeled modules.** Considered and rejected at create-story (D2), despite the
  Core-skill finding that would support it. Reopening it needs an ADR 0015 amendment.
- **Any TEA coverage work.** That is Story 18.5 in full — `TestArtifactDiscovery`, the tier vocabulary, the
  JSON ingest, `test-artifacts.html`, the Module Coverage panel. This story adds no discovery, no parser, no
  page, no nav entry.
- **A dashboard acknowledgement line for the omitted panel.** D3 chose silent-plus-logged. No new
  `DashboardView` property, no new markup, no new CSS.
- **Reading any module `config.yaml` / skill TOML.** Still the shared gap 18.4 and 18.5 both record and neither
  closes.
- **A `BmadModule` enum case for any module** (`Cis`, `Tea`, `TestArchitect`, …). ADR 0015 Decisions 1/2 are
  open-world by design; BMB mints arbitrary codes.
- **A second `IArtifactAdapter` or the adapter registry.** ADR 0015 Decision 6 settled it; Epics 11–15 own the
  registry question.
- **Changing `IsEmpty`, `PresentCount`, `MissingCount`, `StaleCount`, or the staleness threshold.** The coverage
  arithmetic is not what is wrong.
- **Retiring `epics.md`'s "strongly GDS-oriented" clause** (`:173`, also cited at `:3045`). Still an open owner
  item carried by 18.5 Task 8 — do not absorb it.

### Anti-patterns to prevent

- **Adding `_bmad/bmm/` fixtures to `SiteGeneratorCoverageTests` to make it pass.** That means you implemented
  the opposite of D1. Those five tests must pass untouched.
- **Giving the new `BmadModule` parameter a default value.** Silent BMM inheritance is the bug's shape.
- **Aligning `SpecsFor`'s `_ =>` arm with `DocsFor`/`GlossaryFor`'s empty default.** That deletes the panel from
  every non-BMad repo. The asymmetry is D1 and must be commented as such.
- **Adding a second omission gate** in `DashboardViewBuilder` or the render adapter. One rule, one place.
- **Emitting the D3 diagnostic from `BuildArtifactCoverage`.** `RefreshCoverage()` calls it on every watch
  incremental; you will get a diagnostics row per keystroke.
- **Duplicating the `_bmad/{code}/module-help.csv` literal** instead of widening `RepoRelativeCsv`.
- **Calling `ModuleContext.Detect` again.** Read `_module`.
- **Inventing a sixth `AdapterDiagnosticCategory`, or a new `--status-*` token**
  ([[specscribe-status-token-system]]).
- **Trusting a successful write.** Grep-verify every new symbol before relying on it; a zero-grep can be a
  transient mid-write read — confirm with `git diff HEAD` (CLAUDE.md § Concurrent work on shared `main`).
- **`git reset --hard` / `checkout --` / `clean`.** Another session's uncommitted work may be in the tree.

### Architecture compliance

- **AD-1 / AD-2** [`ARCHITECTURE-SPINE.md`] — one shared projection/rendering core; the family set is a
  *projection* concern resolved before rendering. `HtmlRenderAdapter.RenderDashboardBody` is the single
  dashboard body for both the HTML site and the webview (`HtmlTemplater` calls
  `HtmlRenderAdapter.Shared.RenderDashboardBody`), so **no branching in the adapter** — the view carries data,
  the adapter renders it (the Story 6.2 discipline the file's own doc comment states).
- **AD-4 / NFR2** — `BuildArtifactCoverage`'s never-throws contract (`catch { return ArtifactCoverage.Empty; }`)
  is preserved exactly; an insight provider never owns baseline generation success.
- **NFR8** [`epics.md`, search `NFR8:`] — *"surfaces degrade gracefully — absent, not broken or misleadingly
  empty — when a methodology lacks the corresponding artifact."* This story's whole subject is ADR 0015 §2's
  fourth case: **confidently wrong**.
- **ADR 0012 / ADR 0013** — not engaged. The panel is server-rendered HTML cards with no chart and no JS; this
  story adds neither, so the text-twin gate does not apply. Keep it that way.
- **CLAUDE.md** — status is never signalled by colour alone; the diagnostics badge carries the word.

### Testing standards

- xUnit, `tests/SpecScribe.Tests/`. Extend `ArtifactCoverageTests.cs` (pure) and `SiteGeneratorCoverageTests.cs`
  (generation-level); module fixtures follow `ModuleContextTests.cs`'s provenance-block convention with upstream
  commit SHAs (ADR 0015 Decision 7). 18.2 pinned `4a7522664ad4bf1c5338a1819144de458eaebecd` for the TEA repo;
  fetch CIS's own CSV for the unmodeled fixture rather than inventing one.
- Red-green: the reachability test (panel renders on an unmodeled repo with a `SPEC.md`) must **fail** before
  the fix, or the premise is not what this story says it is.
- Every rule in `SpecsFor` / `CreateStepKeysFor` must be assertable without disk access — the pure half stays
  pure.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-6-module-aware-artifact-coverage-families.md`
- Sprint key: `18-6-module-aware-artifact-coverage-families` — do not rename it.
- Expected modified: `src/SpecScribe/ArtifactCoverage.cs`, `src/SpecScribe/SiteGenerator.cs`,
  `src/SpecScribe/ModuleContext.cs` (visibility of `RepoRelativeCsv` only),
  `tests/SpecScribe.Tests/ArtifactCoverageTests.cs`, `SiteGeneratorCoverageTests.cs`, `HtmlTemplaterTests.cs`,
  `IconsTests.cs`, `ChartsTests.cs`, plus new module fixtures.
- Expected **new**: none in `src/`. If this story creates a new production file, re-read the shape section —
  it has probably grown into 18.5's scope.
- No structural scope change: no epic renumber, no story add/remove. `epics.md` and `sprint-status.yaml` need no
  structural edit beyond this story's status transition.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.6 (and its seating comment, whose 18.5
  dependency is reconciled above); § Story 18.1 AC #2; NFR8]
- [Source: `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md` — §2 blast-radius table row
  *"Dashboard artifact-coverage panel"*, **Decision 5a**, Decision 2a/2c/2d, Decision 6, Decision 7,
  Consequences *"Decision 5a re-opens `ArtifactCoverage`"*, Ratification item 1]
- [Source: `_bmad-output/implementation-artifacts/18-5-priority-bmad-module-baseline-coverage.md` — owner
  decision **D4**, § Non-goals first bullet, § "What 18.2 already landed"]
- [Source: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` — Finding 3b
  coverage table; `ArtifactCoverage` named as a fourth un-gated surface]
- [Source: `src/SpecScribe/ArtifactCoverage.cs` — `Specs`, `FamilySpec`, `Build`, `AllCandidatePaths`,
  `CreateStepKeys`, `IsEmpty`, `SelectCanonicalMatch`]
- [Source: `src/SpecScribe/SiteGenerator.cs` — `BuildArtifactCoverage`, `ResolveFamilyHref`, `RefreshCoverage`,
  `AppendCountDivergenceNotice`, `MapDiagnostics`, the `_module = bundle.Module` assignment]
- [Source: `src/SpecScribe/ModuleContext.cs` — `BmadModule`, `None`, `IsModeled`/`IsUnmodeled`, `DocsFor`,
  `GlossaryFor`, `ModuleForCode`, `ReportUnmodeledPrimary`, `RepoRelativeCsv`, `GameDevStudioDocs`]
- [Source: `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — the `view.Coverage is { IsEmpty: false }`
  guard; `src/SpecScribe/HtmlTemplater.cs` — `HtmlRenderAdapter.Shared.RenderDashboardBody`]
- [Source: `src/SpecScribe/Charts.cs` — `CoverageMeter`, `ArtifactCoveragePanel`, `FamilyAccentClass`,
  `BuildCoverageTip`]
- [Source: `tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs` (the no-`_bmad/` fixture);
  `ArtifactCoverageTests.cs`; `SiteGeneratorAdapterTests.cs` § `GenerateAll_GoldenContentFingerprint_…`;
  `ModuleContextTests.cs` (fixture provenance convention)]
- [Source: `_bmad/core/module-help.csv` and `_bmad/bmm/module-help.csv` (this repo's install, read 2026-07-27) —
  `bmad-spec` is a **Core** skill; the Core-skill reachability proof rests on this]
- [Source: `CLAUDE.md` — shared-main concurrency, decision records, live-browser verification, output dir]

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, dev-story workflow)

### Debug Log References

- **RED, proving the premise.** With the fix not yet written, the CIS fixture failed exactly as the story
  predicted: `Assert.DoesNotContain() Failure: Sub-string found ↓ (pos 12086) String: ···" class="chart-panel
  coverage-panel wm-pan"···`. Four of the six new generation tests failed; the BMM and GDS controls passed
  untouched, confirming the defect is confined to the unmodeled path.
- **Full suite, first run after the change:** 2625 passed / 0 failed / 3 skipped in 1m47s. None of the
  documented rotating deep-git flakes fired.
- **Golden gate:** `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` passed in the
  full run and again in isolation. Not re-baselined.
- **A LATER run in the shared tree showed 23 failures — none of them this story's, and the bisect proving it
  is recorded below.** Between the two runs a concurrent session's in-flight edits landed in `Charts.cs`,
  `CodeMapTemplater.cs`, `HierarchyExplorer.cs`, `HierarchyExplorer.Projectors.cs` and `assets/specscribe.js`,
  leaving `src/` **not compiling** at one point: `Charts.cs(2758): error CS0103: The name 'CompactMetricsTail'
  does not exist in the current context` (plus `FreshnessRecursionGuard`, `BuildOwnershipDataAttrs`). A grep
  moments later found `CompactMetricsTail` defined at `Charts.cs:2452` — the file changed between two reads,
  the exact transient mid-write hazard CLAUDE.md § shared `main` describes. The 23 failures were confined to
  `CodeMapTemplaterTests`, `SiteGeneratorCodeMapTests`, `GitInsightsTemplaterTests`, `SiteGeneratorWebviewTests`
  (code-map capture), `SiteGeneratorSpaTests` (hierarchy bundle) and the golden fingerprint — i.e. exactly the
  surfaces those five files own, and **no class this story touches**.
- **Bisect, following the Story 18.4 precedent, without touching the shared tree.** Built an isolated copy from
  `git archive HEAD` (pristine, working-copy changes excluded) and overwrote **only this story's eight files**
  from the working tree, at
  `<scratchpad>/bisect-18-6`. Full suite there: **2625 passed / 0 failed / 3 skipped**, twice consecutively
  (50s, 54s) — golden fingerprint green in both. That is the honest state of this story's work. The shared
  tree's 23 failures are the concurrent session's to resolve; nothing of theirs was reset, reverted or stashed.
- **Live sites generated:** this repo → `SpecScribeOutput/` (432 pages, errors=0); the CIS fixture →
  `<scratchpad>/cis-repo/SpecScribeOutput` (14 pages, errors=0); a partial-BMM fixture →
  `<scratchpad>/bmm-partial/SpecScribeOutput` (13 pages, errors=0) to exercise the Missing-chip and
  create-command affordances the full BMM portal cannot show (it is 8/8 present).

### Completion Notes List

**1. The Core-skill reachability proof holds at HEAD, and was re-verified against this repo's install.**
`_bmad/core/module-help.csv:12` carries `Core,bmad-spec,Spec,SP,…` and `_bmad/bmm/module-help.csv` carries no
`bmad-spec` row at all. Core ships with every module install, so a cis/tea/bmb repo that ran `/bmad-spec` owns a
genuine `specs/*/SPEC.md`. The RED run demonstrated the consequence directly rather than by argument: the CIS
fixture rendered `class="chart-panel coverage-panel …"` — 1 present + 7 missing BMad Method families — on a
project with no PRD, no epics, no stories and no FR/NFR catalog. Upstream had not moved; the story's premise
needed no correction.

**2. The GDS family-set observation — recorded as a follow-up candidate, deliberately NOT actioned.** Game Dev
Studio produces `gdd.md`, `narrative-design.md` and `game-architecture.md` (`ModuleContext.GameDevStudioDocs`),
not a PRD or a product brief, so today's panel is arguably wrong for GDS in the same *kind* of way it was wrong
for CIS — it asserts a vocabulary that module does not use. AC #2 forbids changing modeled-module behavior here,
and doing so would move the golden fingerprint. The deliberate lock is now pinned by two tests
(`Build_GameDevStudio_KeepsTheBmadMethodFamilySet`, `GenerateAll_GameDevStudioPrimary_KeepsTheEightBmadMethodFamilyPanel`)
whose comments say *why*, so a later reader cannot "fix" it by accident. **A GDS-specific family set is a real
candidate for a future story.** Note it is a smaller defect than the CIS one: GDS is `IsModeled`, so it keeps
its docs and glossary correctly — only the artifact-family vocabulary is BMM's.

**3. `RegenerateEpics` re-emission — chosen NOT to re-emit, and why.** `AppendCountDivergenceNotice` is
re-emitted from `RegenerateEpics` because the count ledger genuinely *changes* on a watch rebuild, so the
previously-emitted row can go stale. The detected module cannot change: Story 18.2 made detection once-per-run
and `_module` is immutable for the life of the `SiteGenerator`. A re-emission could therefore only ever
duplicate a row that is already correct — which is what ADR 0015 Decision 2d's at-most-one-per-run rule exists
to prevent. Pinned by `RegenerateEpics_AfterUnmodeledPrimary_DoesNotReEmitThePanelOmissionDiagnostic` and
`GenerateOne_AfterUnmodeledPrimary_DoesNotAccumulatePanelOmissionDiagnostics`.

**4. Live-browser findings — the omission is geometrically clean, which the suite could not have told us.**
On the CIS portal at 1280×900 the panel is absent from the DOM entirely (0 `.coverage-panel`, 0
`.coverage-grid`, 0 `.coverage-meter`, and the string "Planning Artifacts" appears nowhere in `innerText`). The
`.dashboard` section measures **104px, exactly equal to the sum of its visible children, with a 0px trailing
gap** — no collapsed `.chart-panel`, no zero-height residue, no orphaned row. It is also structurally
impossible for one to appear here: `.dashboard` computes to `display: block`, not `grid`. No horizontal
overflow (`scrollWidth` 1280 = `innerWidth` 1280). The two zero-height panels that *do* exist (Story Pipeline,
Git Pulse) are `display: none` from the pre-existing work-mode CSS and are unrelated to this story.

**5. BMM is untouched, verified three ways.** (a) The golden fingerprint did not move. (b) The full BMM portal
renders all eight cards in a 2-column grid (497px + 497px, 11.2px gap), heights 109–131px, every card carrying
its `js-tip` tooltip and a resolved href, meter reading `8/8 100%` with
`aria-label="Planning artifact coverage: 8 of 8 present"`, no horizontal overflow. (c) The partial-BMM fixture
confirms the affordances the full portal cannot show: seven `Missing` chips carrying the **word** (never colour
alone), create commands correctly resolved from the module's own catalog (`/bmad-prd`, `/bmad-product-brief`,
`/bmad-architecture`, `/bmad-ux`, `/bmad-create-story`), and Spec Kernel correctly showing **no** command —
BMad Method exposes no `spec` step, so that card degrades to guidance text exactly as before.

**6. The D3 diagnostic renders as specified.** The CIS portal's `diagnostics.html` carries exactly one matching
row: badge `Informational` (class `status-badge diag-info`, the word present in the text), subject
`_bmad/cis/module-help.csv`, message *"Primary BMad module 'cis' (Creative Intelligence Suite) has no modeled
planning-artifact family set, so the dashboard's Planning Artifacts panel is omitted."* Story 18.2's separate
docs/glossary notice appears alongside it and the two read as distinct facts, not duplicates — which was the
point of giving this one its own wording rather than extending `ReportUnmodeledPrimary`.

**7. Two small corrections to the story's own expectations, neither changing its shape.**
(a) `ChartsTests` needed no edit — it constructs `new ArtifactCoverage { Families = [...] }` directly and never
calls `Build`, so the "~28 call sites across `ArtifactCoverageTests`, `HtmlTemplaterTests`, `IconsTests`,
`ChartsTests`" is really 28 across the first three. (b) The module-aware generation tests went into a **new**
`SiteGeneratorModuleCoverageTests` class rather than into `SiteGeneratorCoverageTests`, because that fixture's
constructor writes `planning-artifacts/epics.md` for all five of its tests — a CIS-only repo would not have one,
and the reachability case needs the core-produced `SPEC.md` to be the *only* matching artifact. This also keeps
the "add no `_bmad/` fixtures here" trap structurally impossible to trip: the two concerns now live in two
files. `SiteGeneratorCoverageTests` gained exactly one test, which asserts `_bmad/` does **not** exist and then
names D1.

**8. No new production file, no second gate, no ADR.** `src/` gained no file. The panel omission rides entirely
on the existing `IsEmpty` → `view.Coverage is { IsEmpty: false }` guard; nothing was added to
`DashboardViewBuilder` or `HtmlRenderAdapter.Dashboard.cs`, and both were left byte-unchanged. `Charts.FamilyAccentClass`
and `SiteGenerator.ResolveFamilyHref` were confirmed (not assumed) to need no change: with an empty family set
neither loop body executes, so no label reaches either `label switch`, and both carry `_ =>` fallbacks anyway.
ADR 0015 Decision 5a was implemented literally with no deviation, so no ADR was authored or amended.

**9. ⚠️ Concurrent-session state at hand-off — the shared tree does NOT currently build, and that is not this
story (CLAUDE.md § shared `main`).** Another session is actively mid-write on
`src/SpecScribe/HierarchyExplorer.cs`, `HierarchyExplorer.Projectors.cs`, `Charts.cs`, `CodeMapTemplater.cs`
and `assets/specscribe.js` (Story 20.9-shaped sunburst/hierarchy work), and has separately added
`.gitattributes`, a Story 5.7 file, an unrelated `SiteGeneratorAdapterTests.cs` comment change (about
`.gitattributes` / `FoldLineEndings`), and edits to `epics.md` / `sprint-status.yaml`. **None of that is this
story's work and none of it is in its File List.** At the moment of the final regression run their `Charts.cs`
did not compile, which is why the shared-tree suite showed 23 failures; the isolated bisect above proves this
story's own work is 2625/0 twice over. **A reviewer should re-run the suite once the concurrent session
settles** — this story's verdict rests on the bisect, not on the shared tree's current red. Every symbol this
story added was grep-verified on disk and confirmed present in `git diff HEAD` after the fact.

### File List

- `src/SpecScribe/ArtifactCoverage.cs` — `Specs` → `BmadMethodSpecs`; new private `SpecsFor(BmadModule)`;
  required `BmadModule` parameter on `Build` and `AllCandidatePaths`; `CreateStepKeys` (static dictionary) →
  `CreateStepKeysFor(BmadModule)` over new `BmadMethodStepKeys` / `EmptyStepKeys`; class, `IsEmpty` and family-set
  doc comments rewritten to state what governs the set and why the `_ =>` polarity is inverted.
- `src/SpecScribe/SiteGenerator.cs` — `BuildArtifactCoverage` reads `_module.Module` and threads it through both
  `Build` calls, the `AllCandidatePaths` call and `CreateStepKeysFor`; new
  `AppendUnmodeledCoverageNotice(List<GenerationEvent>, ModuleContext)`, invoked once from `GenerateAll` beside
  `AppendCountDivergenceNotice`.
- `src/SpecScribe/ModuleContext.cs` — `RepoRelativeCsv` widened `private` → `internal` (visibility only), with
  the reason recorded in its doc comment.
- `tests/SpecScribe.Tests/SiteGeneratorModuleCoverageTests.cs` — **NEW.** Six generation-level tests: the CIS
  reachability case, D3 cardinality/anchor/wording, BMM and GDS controls, and the two watch-path
  non-accumulation cases. Carries upstream-pinned BMM / GDS / CIS `module-help.csv` fixtures per
  `ModuleContextTests`' provenance convention.
- `tests/SpecScribe.Tests/ArtifactCoverageTests.cs` — 25 `Build`/`AllCandidatePaths` call sites updated to name
  the module explicitly via a new `Bmm` alias; `CreateStepKeys` → `CreateStepKeysFor(Bmm)`; six new pure tests
  covering `Unmodeled` (empty set, and the core-`SPEC.md` case), `Unknown` (D1), `GameDevStudio` (AC #2),
  `AllCandidatePaths` agreement, and `CreateStepKeysFor`.
- `tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs` — one new test,
  `GenerateAll_NoBmadInstallAtAll_KeepsThePlanningArtifactsPanel`, asserting `_bmad/` is absent and naming D1.
  The five existing tests are **unmodified** and still run at `BmadModule.Unknown`.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — 2 `Build` call sites updated.
- `tests/SpecScribe.Tests/IconsTests.cs` — 1 `Build` call site updated.
- `.claude/launch.json` — three preview slots for Task 7: `bmm-control-18-6` (8114), `bmm-partial-18-6` (8116),
  `unmodeled-18-6` (8115).

## Change Log

- 2026-07-27 — Story 18.6 implemented (dev-story; story baseline `6017c2c`, HEAD at start `d1722f1`). ADR 0015
  **Decision 5a is closed** — the last un-closed surface from the 18.1 spike's Finding 3b table. The canonical
  artifact-family set is now resolved from the detected `BmadModule` via a private `ArtifactCoverage.SpecsFor`,
  and `Build` / `AllCandidatePaths` / `CreateStepKeysFor` all take the module as a **required** parameter with
  no default. **The premise was proved rather than assumed**: with the fix unwritten, the CIS + core-`SPEC.md`
  fixture rendered `class="chart-panel coverage-panel …"` — 1 present + 7 missing BMad Method families on a
  project that produces none of them — and the BMM/GDS controls passed untouched in the same run. All three
  owner decisions shipped literally: **D1** an undetected repo (`BmadModule.Unknown`) keeps the eight families,
  so `SiteGeneratorCoverageTests`' five no-`_bmad/` tests pass **unmodified** and D1 is now additionally pinned
  by an explicit test that names it; **D2** an `Unmodeled` primary declares an empty set and the whole panel
  omits through the `IsEmpty` gate that already existed — **no second gate was added** in `DashboardViewBuilder`
  or `HtmlRenderAdapter.Dashboard.cs`, both of which are byte-unchanged; **D3** the omission is silent on the
  page but recorded as exactly one `Informational` diagnostic anchored at `DiagnosticAnchorRoot.Repo` via the
  now-`internal` `ModuleContext.RepoRelativeCsv`. **`RegenerateEpics` deliberately does NOT re-emit** the
  notice (unlike the count-divergence one): the count ledger changes on a watch rebuild, the detected module
  cannot, so a re-emission could only duplicate a still-correct row. **The golden fingerprint did not move** and
  was not re-baselined; full suite **2625 passed / 0 failed / 3 skipped** on the first run with none of the
  documented deep-git flakes firing, and **2625 / 0 twice more in an isolated `git archive HEAD` + this story's
  eight files copy** after a concurrent session left the shared tree's `Charts.cs` non-compiling (bisect
  recorded in the Debug Log; nothing of theirs was reset or reverted). Live-browser verification confirmed what the suite structurally cannot
  see: on the unmodeled portal the `.dashboard` section measures **104px, exactly its visible children's height,
  with a 0px trailing gap** — no collapsed panel, no orphaned row, no horizontal overflow — while the BMM portal
  keeps all eight cards, its `8/8 100%` meter, tooltips, `Missing` chips carrying the word, and create commands
  resolved from the module's own catalog. **No new ADR** — 5a was implemented, not amended, and no deviation
  arose. Two of the story's own expectations were corrected in flight, neither changing its shape: `ChartsTests`
  needed **no** edit (it builds `ArtifactCoverage` by object initializer and never calls `Build`), and the
  module-aware generation tests went into a **new** `SiteGeneratorModuleCoverageTests` class because
  `SiteGeneratorCoverageTests`' constructor writes `epics.md` for every test, which a CIS-only repo would not
  have. **Recorded as a candidate follow-up, not actioned**: AC #2 locks Game Dev Studio to the BMad Method
  family set even though GDS produces `gdd.md` / `narrative-design.md` / `game-architecture.md` — a smaller
  instance of the same NFR8 defect, now pinned by two tests whose comments say the lock is deliberate.
- 2026-07-27 — Story 18.6 drafted (create-story, baseline `6017c2c`). Implements ADR 0015 Decision 5a, seated by
  Story 18.5's owner decision D4. **Proved the defect reachable**, which the ADR asserted but did not
  demonstrate: `IsEmpty` already omits an all-missing panel, so the "TEA-only repo shows eight missing families"
  reading is wrong — but `bmad-spec` is a **Core** skill shipped with every module install, so an unmodeled repo
  with a real `SPEC.md` renders 1 present + 7 missing BMM families, and that is the case the fix must close.
  **Found a trap**: `SiteGeneratorCoverageTests`' fixture creates no `_bmad/` at all, so its five generation
  tests run at `BmadModule.Unknown` — which is exactly the state owner decision **D1** preserves. Three owner
  decisions locked at create-story: D1 an undetected repo keeps the eight families (only a *detected*-unmodeled
  module loses the panel); D2 an `Unmodeled` primary declares an empty set and the panel omits entirely
  (ADR-literal, rejecting a core-only Spec-Kernel set despite the Core-skill finding); D3 silent omission on the
  page but recorded as one `Informational` diagnostic. **Reconciled `epics.md`'s seating comment**: the claimed
  dependency on Story 18.5 does not hold in code — 18.6's real gate is Story 18.2 (landed), and the two stories
  share only merge-friction surface on the dashboard files. Also recorded that AC #2 deliberately locks GDS to
  the eight BMM families even though GDS produces `gdd.md` / `narrative-design.md` / `game-architecture.md` —
  a candidate follow-up, not this story's work.
