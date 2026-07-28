---
baseline_commit: 6017c2cd2d4e3928c4713ac369ac401fd9f1dbb7
---

# Story 18.6: Module-Aware Artifact Coverage Families

Status: ready-for-dev

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

- [ ] **Task 1 — Confirm the baseline before changing anything (AC: #1, #2)**
  - [ ] Re-read `src/SpecScribe/ArtifactCoverage.cs`, `SiteGenerator.BuildArtifactCoverage` /
        `ResolveFamilyHref` / `RefreshCoverage`, `Charts.CoverageMeter` / `ArtifactCoveragePanel` /
        `FamilyAccentClass`, and the `coverage-panel` block in `HtmlRenderAdapter.Dashboard.cs`. The tree is
        shared and Story 18.5 may be in flight in the same files.
  - [ ] Re-verify the Core-skill finding against this repo's install: `_bmad/core/module-help.csv` contains
        `bmad-spec`, `_bmad/bmm/module-help.csv` does not. If upstream has moved, correct this story in place
        and say so — the reachability argument is the story's premise.
  - [ ] Confirm `ModuleContext.None.Module == BmadModule.Unknown` and that `_module` is assigned
        (`_module = bundle.Module`) **before** `_coverage = BuildArtifactCoverage(sourceRelatives)` runs in
        `GenerateAll`. Both are true at baseline `6017c2c`; prove it, do not assume it.

- [ ] **Task 2 — Red first: fixtures + failing assertions (AC: #1, #2)**
  - [ ] Add a generation-level fixture with `_bmad/cis/module-help.csv` (real upstream bytes, provenance block
        with the commit SHA, per `ModuleContextTests.cs`'s convention and ADR 0015 Decision 7) **plus** a
        `_bmad-output/specs/spec-x/SPEC.md` — the exact reachable case from the premise above. Assert it renders
        the panel **today** (proving the defect) and omits it after the fix.
  - [ ] Add a **BMM control** fixture (`_bmad/bmm/module-help.csv` + the eight families' files) asserting the
        panel and all eight cards are unchanged.
  - [ ] Add a **GDS control** asserting the eight BMM families still render for `gds` (AC #2's explicit lock).
  - [ ] Add a **no-`_bmad/`** assertion (D1) — the existing `SiteGeneratorCoverageTests` fixture already is
        this case; add one explicit test that names D1 so the behavior is protected by intent, not by accident.

- [ ] **Task 3 — Cut the seam in `ArtifactCoverage` (AC: #1)**
  - [ ] Add private `SpecsFor(BmadModule)` with the polarity and comment specified above; keep `FamilySpec`
        private.
  - [ ] Thread a **required** `BmadModule` parameter through `Build` and `AllCandidatePaths`; replace the
        `CreateStepKeys` static dictionary with `CreateStepKeysFor(BmadModule)`.
  - [ ] Update the class/`Specs` doc comments: the seam the comment promised is now cut, and the comment should
        say what governs the set (module identity) and what the `Unknown` arm means (D1).
  - [ ] Mechanically update every call site — `SiteGenerator.BuildArtifactCoverage` (both `Build` calls and the
        `AllCandidatePaths` call) and the ~28 test call sites across `ArtifactCoverageTests`,
        `HtmlTemplaterTests`, `IconsTests`, `ChartsTests`.

- [ ] **Task 4 — Wire module identity into the coverage build (AC: #1)**
  - [ ] `BuildArtifactCoverage` passes `_module.Module`. Both the full-generate call
        (`GenerateAll`) and the watch `RefreshCoverage()` path go through it, so both become module-aware with
        one change — confirm the watch path in a test, since `_module` persists across incrementals.
  - [ ] Do **not** call `ModuleContext.Detect` again. 18.2 made detection once-per-run on purpose; read
        `_module`.

- [ ] **Task 5 — The D3 diagnostic (AC: #1)**
  - [ ] Implement per the D3 spec above: `Informational`, `RepoRelativeCsv` + `DiagnosticAnchorRoot.Repo`,
        widened to `internal`, emitted once per generate run from the events path, never from
        `BuildArtifactCoverage`.
  - [ ] Test both cardinality (one row, not N) and that a watch rebuild does not accumulate rows.
  - [ ] Test that a BMM / GDS / no-`_bmad` repo emits **zero** such diagnostics.

- [ ] **Task 6 — Regression + golden gate (AC: #2)**
  - [ ] Run the full suite. **Known suite property, do not misread it:** a rotating subset of the deep-git test
        family (`SiteGeneratorTimelineTests`, `SiteGeneratorGitInsightsTests`, `GitMetricsFirstCommitDateTests`,
        …) fails under concurrent load and passes in isolation — Story 18.2 measured 19 such failures, all
        outside its own classes. Confirm any failure is in a class **this** story touches before calling the
        suite red.
  - [ ] `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`
        [`SiteGeneratorAdapterTests`] **must not move.** This repo's primary is BMM, so output is byte-identical
        by construction — a moved fingerprint means the change leaked into the modeled path. If it moves,
        confirm the move is yours before re-baselining ([[golden-diff-normalization-gotchas]]) and confirm the
        new hash is stable across two repeated runs.

- [ ] **Task 7 — Live-browser verification (CLAUDE.md § Verification)**
  - [ ] Generate this repo (BMM) to `SpecScribeOutput/` and confirm the Planning Artifacts panel renders
        identically — eight cards, meter, chips, tooltips, create commands, no layout shift.
  - [ ] Generate the unmodeled fixture and confirm in a real browser that the panel is **absent** — not empty,
        not zero-height, not a collapsed `.chart-panel` leaving a gap or an orphaned grid row in the dashboard
        layout. The suite structurally cannot see sub-pixel collapse; this is why the check exists.
  - [ ] Confirm the diagnostics page shows the one `Informational` row with its word-bearing badge (status is
        never signalled by colour alone).
  - [ ] Never `--output docs/live`. Add a `.claude/launch.json` entry following that file's convention if a
        preview slot is needed.

- [ ] **Task 8 — Record the decisions (AC: #1, #2)**
  - [ ] ADR 0015 Decision 5a is **implemented, not amended** — D2 takes it literally. **Do not author a new
        ADR.** If implementation forces a deviation from 5a, stop and propose an ADR amendment rather than
        shipping a divergence (CLAUDE.md § Decision records).
  - [ ] Record in Completion Notes: the Core-skill reachability proof, the GDS-family-set observation as a
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

### Debug Log References

### Completion Notes List

### File List

## Change Log

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
