---
baseline_commit: 40c7ee96f197a7907dbf8c8fe80c8e5c8fb575a3
---

# Story 18.5: Priority BMad Module Baseline Coverage

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a team using a BMad module beyond BMM,
I want my module's core planning and tracking artifacts interpreted in the portal,
so that I can track progress without switching tools or losing module-specific work.

## Why this story exists (read first)

Story 18.1's spike recommended **Test Architect (TEA)** as the priority coverage module and Story 18.2 landed
the identity foundation that gates it (`_bmad/{code}/` identity, `BmadModule.Unmodeled`, `RankCandidates`).
This story is the coverage half — the ACs that were 18.2's before the 2026-07-25 scope split, carried verbatim.

**Read this before you read 18.1's coverage map.** Live research during create-story (2026-07-27) corrected
three of that map's load-bearing claims. The map is still the right recommendation; its *facts about TEA* were
built from doc-site prose and are wrong in ways that change this story's shape:

| 18.1 claimed | Verified reality (2026-07-27) | Consequence |
|---|---|---|
| TEA writes `traceability-matrix.csv` and `nfr-report.md` | `{test_artifacts}/traceability-matrix.md` and `{test_artifacts}/nfr-assessment.md` — **markdown, and different stems** | A `.csv` parser would find nothing. Filenames below are the pinned set. |
| "TEA artifacts are only found when they happen to fall inside the scanned source root" | TEA's `module.yaml` declares `test_artifacts` default `{output_folder}/test-artifacts`, and `{output_folder}` **IS** `ForgeOptions.SourceDirName` (`_bmad-output`) | In a default install TEA's markdown **already renders today** as generic pages. This story is *interpret and label*, not *make visible*. |
| The `test_artifacts` config key is "the first thing 18.2 must resolve" | The default resolves inside SourceRoot with no config read at all | Reading `_bmad/tea/config.yaml` is **out of scope** (see Non-goals) — it only matters under an overridden path. |

And one thing 18.1 never saw at all: **TEA writes two JSON artifacts SpecScribe structurally cannot see.**
`SiteGenerator` discovers sources with `Directory.EnumerateFiles(_options.SourceRoot, "*.md", SearchOption.AllDirectories)`
[SiteGenerator.cs:4480]. `gate-decision.json` — which carries the actual PASS/CONCERNS/FAIL verdict, the single
most decision-relevant thing TEA produces — and `e2e-trace-summary.json` are never discovered, never rendered,
and never diagnosed. This is the same class of defect as Story 18.4's invisible `forge-report.html`.

**The realistic target repo is BMM + TEA, not TEA-only.** TEA's own `module-help.csv` declares
`bmad-testarch-atdd` as `preceded-by: bmad-create-story:create` — TEA is *designed* to compose with BMM's
story workflow, not replace it. Optimize the design for the dual-install case (the case 18.2 just made safe),
and treat TEA-only as the degradation path that must stay honest.

## Owner design decisions (elicited at create-story — do not re-litigate)

Four calls the owner made up front. Each closes a real fork; none is a default.

**D1 — Two surfaces, not one.** Coverage-tier labeling lands on **both** a new **Test Artifacts list page**
(`test-artifacts.html`, ListRow grammar per Story 10.8, nav entry and page omitted entirely when no TEA
artifacts exist) **and** a **Module Coverage panel on the dashboard**. The panel is the at-a-glance signal;
the page is the per-artifact detail. Build the panel generically enough that a second covered module drops in
without a rewrite — but do **not** build a second module in this story.

**D2 — Parse `traceability-matrix.md` into the existing surfaces.** Not render-and-label, not summarize-only.
TEA's matrix feeds SpecScribe's Story 21.1 traceability surface where a join is possible, so TEA coverage
compounds with what SpecScribe already has rather than starting a parallel island. **See "The join is the hard
part" below — the join is genuinely unreliable and the design must degrade honestly, not fake it.**

**D3 — Widen source discovery to the two TEA JSON filenames.** `gate-decision.json` and
`e2e-trace-summary.json` are read by exact filename and their verdict surfaced. This makes SpecScribe ingest a
non-markdown source for the first time — **it is a cross-cutting contract change and requires an ADR** (see
Task 7). Do not bolt in an ad-hoc reader.

**D4 — `ArtifactCoverage` (ADR 0015 Decision 5a) is NOT this story.** A TEA-only repo's dashboard still
asserts eight missing BMM families. That is seated as **new Story 18.6**, created alongside this one in
`epics.md` and `sprint-status.yaml`. Do not fix it here; do not silently widen scope into it.

## Acceptance Criteria

1.
**Given** the priority module(s) chosen by Story 18.1's coverage map
**When** generation runs against a representative repository for that module
**Then** the module's core planning and tracking artifacts render without fatal failures via the shared adapter contract, each discovered artifact labeled rendered, summarized, or unsupported
**And** output stays coherent alongside the existing BMM and framework surfaces, with BMM support fully intact.

2.
**Given** module-specific artifacts the projection does not model
**When** they are discovered
**Then** they surface as explicit non-fatal notices (coverage-tier labeling where partial) and never block full-site generation
**And** any module-specific next-step-command vocabulary flows through the adapter contract rather than being hard-coded (NFR8).

[Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.5 — ACs carried verbatim from the pre-split Story 18.2]

**Reading AC #1's "labeled rendered, summarized, or unsupported."** That vocabulary comes from the PRD
(`_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md:81`) and is an **open question in both
the PRD (`:250`) and `SPEC.md` (`:118`)**: *"How should coverage tiers be communicated so users understand
interpretation boundaries?"* **This story answers it.** The vocabulary is currently unimplemented anywhere in
`src/` — grep confirms zero hits for `CoverageTier` / `summarized`. Define it once, as a real type, and record
the answer back into the PRD/SPEC open-question lines in the same change (see Task 8).

## The pinned TEA artifact contract

Fetched live 2026-07-27 from `bmad-code-org/bmad-method-test-architecture-enterprise`, `main` branch, read from
each skill's own `workflow.yaml`/`module.yaml` — **not** from doc-site prose, which is what misled 18.1.
Re-fetch and pin commit SHAs at dev time exactly as Story 18.2 did for its `module-help.csv` fixtures
(ADR 0015 Decision 7); the ecosystem moves fast and this table is a starting contract, not gospel.

`test_artifacts` resolution, from `src/module.yaml`:

```yaml
test_artifacts:
  prompt: "Where should test artifacts be stored? (test plans, coverage reports, quality audits)"
  default: "{output_folder}/test-artifacts"
  result: "{project-root}/{value}"
```

`module.yaml` also gives `code: tea`, `name: "Test Architect"` — note the **label divergence**: the installed
`module-help.csv`'s `module` column says **"Test Architecture Enterprise"**, and that CSV column is the only
on-disk module label SpecScribe reads (18.1 §2). Use whatever `CommandCatalog.ModuleLabel` resolves to; never
hard-code either string.

| Producing skill | Output path | Ext | Discoverable today? |
|---|---|---|---|
| `bmad-testarch-trace` | `{test_artifacts}/traceability-matrix.md` | md | ✅ generic page |
| `bmad-testarch-trace` | `{test_artifacts}/gate-decision.json` | **json** | ❌ **invisible** |
| `bmad-testarch-trace` | `{test_artifacts}/e2e-trace-summary.json` | **json** | ❌ **invisible** |
| `bmad-testarch-nfr` | `{test_artifacts}/nfr-assessment.md` | md | ✅ generic page |
| `bmad-testarch-test-review` | `{test_artifacts}/test-review.md` | md | ✅ generic page |
| `bmad-testarch-test-design` | `{test_artifacts}/test-design-architecture.md`, `test-design-qa.md`, `test-design-epic-{epic_num}.md`, `test-design/{project_name}-handoff.md` | md | ✅ generic page |
| `bmad-testarch-atdd` | `{test_artifacts}/atdd-checklist-{story_key}.md` | md | ✅ generic page |
| `bmad-testarch-framework` / `-ci` / `-automate` | framework scaffold, CI config, test suite — **executable output, not artifacts** | — | non-goal (18.1 §7) |
| `bmad-teach-me-testing` | progress file / session notes / certificate | md? | unpinned; treat as unsupported-with-notice |

### `gate-decision.json` (the slim signal — this is what the panel shows)

```json
{
  "schema_version": "0.1.0",
  "evaluated_at": "ISO8601",
  "repo": "string",
  "target": { "type": "story|epic|release|hotfix", "id": null, "label": null },
  "collection_status": "COLLECTED|WAIVED|RESTRICTED|INACCESSIBLE|DEFERRED_SHARED",
  "gate_basis": "priority_thresholds",
  "gate_status": "PASS|CONCERNS|FAIL|WAIVED",
  "rationale": "string",
  "p0_status": "MET|NOT_MET",
  "p1_status": "MET|PARTIAL|NOT_MET",
  "overall_status": "MET|NOT_MET",
  "critical_open": "number",
  "links": { "trace_report_path": "string", "trace_report_url": "string", "artifact_url": "string", "journey_evidence_url": "string" }
}
```

⚠️ **`target.id` and `target.label` are literally `null` in the schema's own example.** Do not build the join
on them (see below).

### `e2e-trace-summary.json` (the rich one)

Key fields: `schema_version`, `snapshot_at`, `repo`, `collection_mode`, `collection_status`,
`inventory_basis` (`acceptance_criteria` | `openapi_endpoints` | `user_journeys`), `gate_basis`, `source_sha`,
`decision_mode`, `confidence` (`high|medium|low`), `oracle{resolution_mode, confidence, sources, synthetic}`,
`coverage{inventory, priority_breakdown, by_level}`, `tests{files, cases, skipped_cases, fixme_cases,
pending_cases}`, `risk_summary{critical_open, high_open, medium_open, low_open}`, `heuristics`, `blockers[]`,
`recommendations[]`, `gate_status`, `gate_criteria`, `links`.

**`schema_version` is `"0.1.0"` on both.** Gate every parse on the major version and emit a `Skipped`
diagnostic — not a crash, not a silent misparse — on an unrecognized one.

### `traceability-matrix.md` structural grammar (from `trace-template.md`)

Phase 1: `Priority | Total Criteria | FULL Coverage | Coverage % | Status` summary table (P0–P3); a detailed
mapping section whose columns are **Oracle Item | Mapped Test(s) | Coverage Status (`FULL|PARTIAL|NONE|
UNIT-ONLY|INTEGRATION-ONLY`) | Test Level | Priority**; gap-analysis subsections; a "Coverage by Test Level"
table (E2E | API | Component | Unit). Phase 2 appends the gate decision, ending in a
`{PASS | CONCERNS | FAIL | WAIVED}` statement.

## The join is the hard part — read this before implementing D2

SpecScribe's `traceability.html` is a **requirement × covering-epic** matrix
(`Charts.TraceabilityMatrix(requirements, epics, prefix)` [TraceabilityTemplater.cs]), built from
`RequirementsModel`, which is parsed **from `epics.md`'s "## Requirements Inventory"** [RequirementsModel.cs:24-25].
TEA's matrix is an **oracle-item × test** matrix keyed by P0–P3 priority. **These are different axes.**

`step-03-map-criteria.md` states the criterion ID format is **not specified** — an Oracle Item is
"formal requirement, endpoint/spec item, or synthetic journey identifier", and `inventory_basis` tells you
which. So:

- `inventory_basis: acceptance_criteria` in a **BMM + TEA** repo → oracle items are BMM story ACs, and
  `RequirementsParser.StoriesFor` / `RequirementsModel.ById` give a real path from AC → story → epic →
  requirement. **This is the only case where a join into `traceability.html` is defensible.**
- `openapi_endpoints` / `user_journeys`, or `oracle.synthetic == true`, or `confidence != "high"` → **there is
  no requirement join.** Render the TEA coverage as its own dimension on the Test Artifacts page and say so.
  Do **not** invent a mapping.

**Design rule (non-negotiable):** a TEA row may only appear on `traceability.html` when its oracle item resolves
to an id present in `RequirementsModel.ById` (`FR#`/`NFR#`/`UX-DR#`) **or** to a story id present in
`EpicsModel`. Anything else is `unsupported` with a notice. A confident-looking but wrong traceability claim is
the exact failure mode Story 21.1's review already caught once (*"phantom-covered req counted 'covered' but
drawn BLANK"* — [[story-21-1-code-review-done]]). Prefer an honest gap to a fabricated link.

**Cite ADR 0019 (Proposed, authored by Story 18.3): "LLM-Generated Artifacts Are Enrichment-Only Inputs, Never
Authoritative Ones."** Every TEA artifact here is LLM-authored. `traceability-matrix.md` is therefore an
**enrichment** input to the traceability surface, never the authority for it: SpecScribe's own FR→epic coverage
stays the primary signal, TEA's test coverage layers on top and is visibly attributed to TEA. If 18.3's ADR is
still unwritten at dev time, this story's ADR (Task 7) must state the same constraint rather than contradict it.
Do **not** author a competing ADR on that point.

## Context & Scope

### What 18.2 already landed (do not rebuild it)

`ModuleContext` after Story 18.2 (`review`, baseline `86b35c2`) — verified present at HEAD:

- `BmadModule { Unknown, BmadMethod, GameDevStudio, Unmodeled }`; `ModuleContext.Code`, `.IsModeled`,
  `.IsUnmodeled`; `CommandCatalog.HasLabel`; `CommandCatalog.Empty.ModuleLabel == ""`.
- `ModuleContext.Detect(repoRoot, sourceRelatives, List<AdapterDiagnostic>? diagnostics = null)` — the
  diagnostics sink exists; `BmadArtifactAdapter.Ingest` already passes its list.
- `RankCandidates` (replaced `ChoosePrimary`) — BMM/GDS can never be demoted by manifest order.
- `ReservedModuleNames` / `IsReservedModuleName`, `ModeledModuleLabels`, `CodeOf`, `ModuleForCode`,
  `RepoRelativeCsv`, `DiagnosticAnchorRoot.Repo`.
- **Detect-once-per-run**: `SiteGenerator.BuildNav` no longer re-detects. Read `_module`; do not call `Detect`
  again.

**TEA does not become a `BmadModule` enum case.** ADR 0015 Decision 1/2 make identity open-world on purpose —
BMB mints arbitrary codes, so no closed enumeration can be correct. TEA stays `Unmodeled` in a TEA-only repo
and is simply *present* in a BMM+TEA repo. Coverage is keyed on the **module code string `"tea"`**, not on a
new enum value. (Adding `BmadModule.TestArchitect` is an explicit anti-pattern below.)

### The precedent to copy: Story 18.4, in flight right now

⚠️ **A concurrent session is implementing Story 18.4 as this story is written.** Uncommitted at
`40c7ee9`: new `src/SpecScribe/IdeaDiscovery.cs`, `IdeasModel.cs`, `Memlog.cs`; modified `SiteNav.cs`,
`SiteGenerator.cs`, `DashboardViewBuilder.cs`, `specscribe.css`, `specscribe.js`. Per CLAUDE.md: **do not
`git reset --hard` / `checkout --` / `clean`**, and grep-verify every symbol you add before relying on it.

18.4's shape is the shape this story follows — it is the same problem (a BMad module's output living inside
SourceRoot, needing its own list page and its own folder-group key):

- **IO / logic split**: `IdeaDiscovery` (IO, walks SourceRoot) + a pure derivation type beside it — *"the same
  IO/logic split `ArtifactCoverage` / `WorkInventory` / `ProgressCalculator` already use."* Mirror it:
  `TestArtifactDiscovery` + `TestArtifactDerivation` + `TestArtifactsModel`.
- **Folder-group key**: `IdeaDiscovery.WorkspaceRootDirName = "forge"` is registered as a
  `DashboardViewBuilder` group key *"so `forge/` stops reading as an unrecognized top-level folder."*
  **`test-artifacts/` needs the same treatment** — otherwise `UnrecognizedTopLevelFolders`
  [SiteGenerator.cs:3387] emits *"unrecognized top-level folder; its documents render in their own home-index
  section"* for a folder this story now models. That notice firing is a **regression signal**, not cosmetic.
- **Top-level landing path**: `SiteNav.IdeasOutputPath = "ideas.html"` is deliberately top-level, not
  `ideas/index.html`, to sidestep `SiteGenerator.RegenerateAdrs`' `landingPathAlreadyWritten` collision guard.
  **Use `test-artifacts.html`, same rationale.**
- **Never throws (AD-4 / NFR2)**: any failure degrades to an empty model or drops one artifact with a
  categorized non-fatal diagnostic.
- 18.4's own doc comment already cites this story: *"SpecScribe reads NO BMad skill/module TOML or
  `config.yaml` at all today (the same gap Story 18.5 **records** for TEA's `test_artifacts` key)."* Records —
  not closes. Keep it that way.

**Expect merge friction on `SiteNav.cs`, `SiteGenerator.cs` and `DashboardViewBuilder.cs`.** All three are open
in the 18.4 session. Re-read them immediately before editing and re-grep after.

### The diagnostic vocabulary is closed at five values

`AdapterDiagnosticCategory` = `Unsupported`, `Malformed`, `Skipped`, `Error`, `Informational`
[AdapterDiagnostic.cs:7-32]. **Do not invent a sixth.** Mapping for this story:

| Situation | Category |
|---|---|
| A TEA artifact family SpecScribe does not model (teach-me-testing output, framework scaffold) | `Unsupported` |
| A TEA JSON whose `schema_version` major is unrecognized | `Skipped` |
| A TEA JSON that is present but will not parse | `Malformed` |
| TEA's matrix present but its oracle basis admits no requirement join | `Informational` |
| `test_artifacts` overridden outside SourceRoot, so nothing was found | `Informational` |

Anchor root: TEA artifacts live under **SourceRoot**, so `DiagnosticAnchorRoot.Source` (the default) is correct.
`DiagnosticAnchorRoot.Repo` is 18.2's addition for `_bmad/{code}/…` paths — **do not** reuse it here.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md] — one shared projection/rendering core. TEA coverage translates into the
  bundle/model layer; it never forks rendering.
- **AD-2** — the adapter boundary is source → normalized records. TEA ingest belongs inside
  `BmadArtifactAdapter.Ingest` (its `AppliesTo` already markers `_bmad/` wholesale — 18.1 §4), **not** a second
  `IArtifactAdapter` and **not** a registry. Epic 18 is the one framework epic that does not need the registry;
  do not reopen Epics 11–15's question.
- **AD-4** — module-specific enrichment stays additive and non-blocking.
- **NFR8** [epics.md:137]: *"…surfaces degrade gracefully — absent, not broken or misleadingly empty — when a
  methodology lacks the corresponding artifact."* A BMM-only repo must see **zero** change: no panel, no page,
  no nav entry, no notice.
- **ADR 0013 / ADR 0012** — if the Module Coverage panel carries any chart, it needs a server-rendered text
  twin, audited in a live browser with JS off. Prefer no chart: a counts-and-badges panel discharges D1 without
  entering the twin gate at all.
- **Status must never be signalled by colour alone** (CLAUDE.md). A `PASS|CONCERNS|FAIL|WAIVED` gate badge
  carries the **word**, exactly as 18.2's `Informational` badge does (`status-badge diag-info`).

## Tasks / Subtasks

- [ ] **Task 1 — Re-pin the TEA contract against upstream before writing any parser (AC: #1)**
  - [ ] Re-fetch `src/module.yaml`, `src/module-help.csv`, and the `workflow.yaml` of `bmad-testarch-trace`,
        `-nfr`, `-test-review`, `-test-design`, `-atdd` from
        `bmad-code-org/bmad-method-test-architecture-enterprise`. Record the **commit SHA per file** in the test
        fixture provenance block, following `ModuleContextTests.cs`'s existing header convention (ADR 0015
        Decision 7). 18.2's pinned SHA for that repo was `4a7522664ad4bf1c5338a1819144de458eaebecd`.
  - [ ] Fetch `src/workflows/testarch/bmad-testarch-trace/steps-c/step-05-gate-decision.md` and
        `trace-template.md` and confirm the two JSON schemas and the matrix grammar above are still current.
  - [ ] Correct this story's table in place if upstream has moved, and say so in Completion Notes.
        **Do not implement against a stale table.**

- [ ] **Task 2 — Build the fixture repository the ACs require (AC: #1 — both clauses, incl. "BMM support fully intact")**
  - [ ] There is no repo anywhere that has actually run TEA — 18.1 searched and found none, and this repo has
        no `_bmad/tea/`. The "representative repository" is a **fixture you construct**, exactly as 18.2 did.
  - [ ] Build **two** fixtures: **(a) BMM + TEA** — the realistic case, and the one the join design targets —
        and **(b) TEA-only**, the honest-degradation case. Each carries a real `_bmad/tea/module-help.csv`
        (all 10 rows, verbatim upstream) plus a `_bmad-output/test-artifacts/` tree with the pinned filenames.
  - [ ] Author fixture artifact *contents* from `trace-template.md`'s real grammar and the real JSON schemas —
        not from imagination. Include at least one `inventory_basis: acceptance_criteria` case (joinable) and
        one `user_journeys` / `synthetic: true` case (must NOT join).
  - [ ] Add a **BMM-only** control fixture asserting byte-identical output to today — this is AC #1's "BMM
        support fully intact" clause and the cheapest regression net you will get.

- [ ] **Task 3 — Coverage-tier vocabulary as a real type (AC: #1, #2)**
  - [ ] Introduce the tier vocabulary once — `rendered` / `summarized` / `unsupported` — as a typed enum plus a
        label/description pair, following `StatusStyles`' one-classifier discipline. It is currently nowhere in
        `src/`.
  - [ ] Assign a tier per discovered artifact: `rendered` = a full page exists for it; `summarized` = its
        headline (gate verdict, coverage %, NFR category table) is extracted onto a surface but the file is not
        fully modeled; `unsupported` = discovered and named, nothing interpreted.
  - [ ] Every tier must be **derivable and testable without disk access** (pure derivation half).

- [ ] **Task 4 — `TestArtifactDiscovery` + `TestArtifactsModel` (AC: #1, #2)**
  - [ ] IO half walks SourceRoot for the pinned filename set; pure half applies every classification rule.
        Mirror `IdeaDiscovery`/`IdeaDerivation`'s split and its never-throws contract.
  - [ ] Gate discovery on the **module being present**, not on filenames alone: a repo with a coincidental
        `test-review.md` and no `_bmad/tea/` must produce nothing. Use 18.2's presence machinery
        (`ModuleContext`'s installed-module set / `IsModulePresent`) — do not write a new presence check.
  - [ ] Populate `ArtifactBundle.ConsumedSourceRelatives` for every markdown artifact the Test Artifacts page
        now owns, so the generic-pages pass does not double-render it. **Verify against the real generated
        site**, not just the model — this is where 18.4's `forge/` work and this story most plausibly collide.
  - [ ] Register `test-artifacts` as a `DashboardViewBuilder` folder-group key so
        `UnrecognizedTopLevelFolders` stops flagging it. Assert the notice's absence in a test.

- [ ] **Task 5 — JSON ingest for the two TEA JSON files (AC: #1) [ADR-gated — see Task 7]**
  - [ ] Read `gate-decision.json` and `e2e-trace-summary.json` **by exact filename under the discovered
        `test-artifacts` root only** — do not widen the global `*.md` glob to `*.json`, and do not walk the
        whole tree for JSON. Scope is the narrowest thing that satisfies D3.
  - [ ] Gate on `schema_version` major; unknown → `Skipped` diagnostic, no parse attempt. Malformed → `Malformed`.
        Neither ever aborts the run.
  - [ ] `System.Text.Json` only — no new package dependency (the project has none for this and ADR 0010 records
        a zero-dependency posture for tooling).
  - [ ] Surface `gate_status` + `rationale` + `critical_open` on the Module Coverage panel; surface the fuller
        `e2e-trace-summary.json` coverage/priority/level breakdowns on the Test Artifacts page.
  - [ ] `target.id`/`target.label` are nullable — render the gate without them; never key anything on them.

- [ ] **Task 6 — The two surfaces (AC: #1, #2) [D1]**
  - [ ] **`test-artifacts.html`** — top-level output path (`SiteNav.TestArtifactsOutputPath`), ListRow grammar
        per Story 10.8 (`ListRow.Render` / `.Chip` / `.PrimaryLink` / `.EmptyState`), one row per discovered
        artifact: title, tier badge (word, not colour), producing skill, link through to its page.
        Nav entry **and** page omitted entirely when the model is empty — match `IdeasOutputPath`'s gating and
        `SiteNav`'s existing optional-surface conventions.
  - [ ] **Module Coverage panel on the dashboard** — built in `DashboardViewBuilder` into a new `DashboardView`
        property, rendered by `HtmlRenderAdapter.RenderDashboardBody`. Data-only in the view; **no branching in
        the adapter** (the Story 6.2 discipline the file's own doc comment states). Shows the module label, the
        gate badge, the tier counts, and a link to `test-artifacts.html`. Absent for a BMM-only repo.
  - [ ] Make the panel's shape module-agnostic (keyed on module code + a coverage model), so 18.6 and a future
        second module reuse it. **Do not implement a second module.**
  - [ ] Reuse existing tokens only — no new `--status-*` token (the six are the single stage→colour source,
        [[specscribe-status-token-system]]). A gate verdict maps onto existing status tokens **plus** its word.

- [ ] **Task 7 — Propose the ADR for non-markdown source ingestion (AC: #1) [D3]**
  - [ ] `SiteGenerator`'s `*.md`-only source scan is a cross-cutting contract; reading JSON sources changes it.
        CLAUDE.md requires an ADR proposed without being asked for exactly this.
  - [ ] **Number it `0020`.** `0019` is claimed-but-unwritten by Story 18.3
        (`18-3-…-spike.md:985`, *"LLM-Generated Artifacts Are Enrichment-Only Inputs"*) and is not yet in
        `docs/adrs/`. Verify before writing; if 18.3's ADR has landed as 0019, take the next free number.
  - [ ] Scope it narrowly: *when* SpecScribe may read a non-markdown source (module-declared, exact-filename,
        schema-versioned, inside SourceRoot), what it must do on version drift, and the explicit non-goal that
        this is **not** a general "ingest any JSON" seam. State its relationship to 18.3's proposed ADR 0019
        rather than contradicting it.
  - [ ] Add its `docs/adrs/README.md` index entry in the same change. ⚠️ Story 18.1's review found an earlier
        edit **blanked ADR 0014's entry** — re-read the whole file after editing and confirm every prior entry
        still has its parenthetical.
  - [ ] Cite by **symbol/quote anchor, not line number** ([[cite-adrs-by-symbol-not-line-number]] — ADR 0015's
        refs drifted within one day).

- [ ] **Task 8 — Close the PRD/SPEC open question; carry the two open owner items (AC: #1) [D4]**
  - [ ] Record the coverage-tier answer back into the two open-question lines
        (`prds/prd-SpecScribe-2026-07-05/prd.md:250`, `specs/spec-specscribe/SPEC.md:118`) — located by quote,
        not line number.
  - [ ] **Story 18.6 is already seated** — create-story landed *Story 18.6: Module-Aware Artifact Coverage
        Families* in `epics.md` (after Story 18.5, with its provenance comment) **and** the
        `18-6-module-aware-artifact-coverage-families: backlog` key in `sprint-status.yaml`, in the same change.
        Nothing to create; just do not absorb its scope, and confirm both artifacts still carry it before you
        finish (the tree is shared).
  - [ ] **Owner proposal already open, do not lose it:** Story 18.2 Completion Note §8 asks to retire
        `epics.md`'s stale *"strongly GDS-oriented … requires generalization"* clause (`:173`), which
        **Story 18.1's AC #2 also cites** (`epics.md:3045`). Both sites must move together. Surface it; the
        deletion is the owner's call, not this story's.

- [ ] **Task 9 — Tests (AC: #1, #2)**
  - [ ] Red-green: write the failing assertions first, as 18.2 did (13 tests before the fix).
  - [ ] Pure-derivation unit tests for tier assignment, gate parsing, schema-version gating, and the
        join-admissibility rule (including the negative cases: synthetic oracle, non-`acceptance_criteria`
        basis, unresolvable id).
  - [ ] Generation-level tests over both fixtures: page present/absent, nav entry present/absent, panel
        present/absent, `ConsumedSourceRelatives` suppression, no `UnrecognizedTopLevelFolders` notice.
  - [ ] **BMM-only control**: assert output unchanged. Golden byte-parity gate stays green — or, if it moves,
        prove the move is yours before re-baselining ([[golden-diff-normalization-gotchas]]; 18.2's worktree
        byte-compare technique is the reliable method, and `git status` under-reported a concurrent mid-write
        for several minutes during that story).

- [ ] **Task 10 — Live-browser verification (CLAUDE.md § Verification)**
  - [ ] The suite structurally cannot see CSS containment leaks, sub-pixel collapse, or DOM corruption. Verify
        in a real browser: the Test Artifacts page (rows render, tier badges carry words, links resolve, empty
        state never renders alongside rows), the dashboard panel (no layout collapse, no horizontal body
        scroll), and the **BMM-only** portal (panel and nav entry genuinely absent, glossary/legend/`<abbr>`
        intact).
  - [ ] Generate to `SpecScribeOutput/` or an explicit scratch dir. **Never `--output docs/live`.**
  - [ ] Add a `.claude/launch.json` entry following that file's existing convention if a preview slot is needed
        (18.2 used `tea-identity-18-2`, port 8108).

## Dev Notes

### Non-goals (explicit — do not widen)

- **`ArtifactCoverage`'s BMM family set.** ADR 0015 Decision 5a → new Story 18.6 (D4). A TEA-only repo will
  still show eight missing BMM families when this story ships. That is known and accepted.
- **Reading `_bmad/tea/config.yaml` (or any module `config.yaml` / skill TOML).** The default `test_artifacts`
  lands inside SourceRoot, so the common case needs no config read. An overridden path gets one `Informational`
  notice and nothing else. Closing this needs a cross-cutting config-reading decision shared with 18.4's
  identical `forge_output_path` gap — not a reader bolted on here.
- **A `BmadModule.TestArchitect` enum case.** ADR 0015 Decisions 1/2 are open-world on purpose.
- **An `AboutSddTemplater.Frameworks` row (or `README.md` support-table row) for TEA.** 18.1 named the roster
  as extension point 6, but its `detected` switch takes exactly two bools (`methodPresent`, `gdsPresent`), its
  `Id` is not the module code (`"bmad"` vs `bmm`), and `RenderFrameworkPage`'s
  `Frameworks.First(f => f.Id == frameworkId)` **throws** rather than degrades on an unknown id. Widening it is
  ADR 0015 **Decision 3c**, which sits under the deferred multi-valued-`ModuleContext` decision. Coverage of
  TEA's *artifacts* does not require claiming TEA as a *supported framework* on the About-SDD matrix — and
  claiming it there while `Frameworks` still can't express the module code would create the fourth
  mutually-contradicting module surface this epic has been closing. Leave the roster alone.
- **A second `IArtifactAdapter` or the adapter registry.** 18.1 §4 settled this; Epics 11–15 own the registry.
- **CIS or BMB coverage.** CIS already renders via the generic pass (18.1 §5); BMB is a non-goal for artifact
  rendering.
- **Command-vocabulary generalization.** ADR 0015 records the mechanism as already module-neutral and the
  residual step vocabulary as needing no work; the always-rendered legend was closed by 18.2's Decision 2c
  gating. AC #2's second clause is satisfied by *not hard-coding* TEA commands — every TEA command must come
  through `CommandCatalog.Command(step)` off the parsed CSV, never a literal `/bmad-testarch-*` string.
- **A general "ingest any JSON" seam.** Task 5 is exact-filename, module-scoped, schema-versioned. Anything
  broader belongs to the ADR's non-goals, not this implementation.
- **Retiring `epics.md`'s "strongly GDS-oriented" clause.** Surface it (Task 8); the owner decides.

### Anti-patterns to prevent

- Implementing against 18.1's `traceability-matrix.csv` / `nfr-report.md` filenames. **They are wrong.**
- Assuming TEA artifacts are invisible today. The markdown already renders; double-rendering it as both a
  generic page and a Test Artifacts page is the likely defect, not absence.
- Widening the global source glob from `*.md` to `*.md;*.json`. That ingests every unrelated JSON in the tree.
- Joining TEA oracle items into `traceability.html` on a guess. `target.id` is nullable, the criterion ID
  format is unspecified upstream, and Story 21.1's review already caught a phantom-covered requirement rendering
  as covered-but-blank. Admissible joins only.
- Treating an LLM-generated matrix as authoritative over SpecScribe's own FR→epic coverage (ADR 0019, proposed).
- Adding a sixth `AdapterDiagnosticCategory`, or a new `--status-*` token.
- Signalling the gate verdict by colour alone.
- Calling `ModuleContext.Detect` again. 18.2 made detection once-per-run on purpose; re-detecting silently
  re-introduces the bug where the undiagnosed detection wins.
- Hard-coding either `"Test Architecture Enterprise"` or `"Test Architect"`. Read `CommandCatalog.ModuleLabel`.
- Fixing 18.4's in-flight files, or reverting anything you did not write. Grep-verify after every edit; a
  zero-grep can be a transient mid-write read — confirm with `git diff HEAD`
  ([[shared-main-concurrent-edit-loss-verify-after-edit]]).

### Testing standards

- xUnit, `tests/SpecScribe.Tests/`. New files: `TestArtifactDiscoveryTests.cs` (IO/fixture-backed) and a pure
  derivation test class; extend `SiteGenerator*Tests` for the generation-level assertions.
- **Known suite property, do not misread it:** a rotating subset of the deep-git test family
  (`SiteGeneratorTimelineTests`, `SiteGeneratorGitInsightsTests`, `GitMetricsFirstCommitDateTests`, …) fails
  under concurrent load and passes in isolation — 18.2 measured 19 failures across a full run, all outside its
  own classes, all passing individually. Confirm any failure is in a class **this** story touches before
  treating the suite as red.
- Fixture provenance block with per-file upstream commit SHAs, as `ModuleContextTests.cs` does.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-5-priority-bmad-module-baseline-coverage.md`
- Sprint key: `18-5-priority-bmad-module-baseline-coverage` — **unchanged**; do not rename it.
- **Gate:** Story 18.2 is `review`, not `done`. Its code is in `main` and this story builds on it directly. If
  18.2's review produces patches to `ModuleContext`/`HowToReadTemplater`, rebase this story's assumptions onto
  them rather than working around them.
- Expected new files: `src/SpecScribe/TestArtifactDiscovery.cs`, `TestArtifactsModel.cs` (+ a derivation type),
  `TestArtifactsTemplater.cs`, `docs/adrs/0020-*.md`.
- Expected modified: `SiteNav.cs`, `SiteGenerator.cs`, `DashboardViewBuilder.cs`, `DashboardView.cs`,
  `HtmlRenderAdapter.Dashboard.cs`, `ArtifactBundle`-populating path in `BmadArtifactAdapter.cs`,
  `docs/adrs/README.md`, `epics.md`, `sprint-status.yaml`, PRD + `SPEC.md` open-question lines.
- **Structural scope change in this story:** Story 18.6 is created. `epics.md` and `sprint-status.yaml` must
  move together.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.5; § Additional Requirements `:173`; NFR8 `:137`]
- [Source: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` §4 extend-vs-registry, §5 per-module coverage map, §6 priority recommendation, §7 non-goals + diagnostic wording]
- [Source: `_bmad-output/implementation-artifacts/18-2-bmad-module-identity-foundation.md` § Dev Agent Record §§3–8]
- [Source: `_bmad-output/implementation-artifacts/18-3-bmad-index-docs-contract-spike.md:985` — proposed ADR 0019]
- [Source: `_bmad-output/implementation-artifacts/18-4-forged-ideas-list-page.md`; in-flight `src/SpecScribe/IdeaDiscovery.cs`]
- [Source: `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md` — Decisions 1, 2, 4 (landed), 3 and 5a (deferred), 6 (no registry), 7 (pin fixtures)]
- [Source: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md:81, :250`; `specs/spec-specscribe/SPEC.md:118` — the coverage-tier open question]
- [Source: `src/SpecScribe/SiteGenerator.cs:4480` (`*.md`-only source scan), `:3387` (`UnrecognizedTopLevelFolders`), `:3552-3560` (traceability page gating)]
- [Source: `src/SpecScribe/RequirementsModel.cs`; `TraceabilityTemplater.cs`; `ArtifactCoverage.cs:85-108`; `AdapterDiagnostic.cs:7-32`; `ArtifactBundle.cs`; `ListRow.cs`; `DashboardView.cs`; `DashboardViewBuilder.cs`]
- [Source: `tests/SpecScribe.Tests/ModuleContextTests.cs:65-111` — upstream fixture provenance convention + the pinned TEA CSV]
- [Upstream, fetched 2026-07-27, `bmad-code-org/bmad-method-test-architecture-enterprise@main`: `src/module.yaml`, `src/module-help.csv`, `src/workflows/testarch/bmad-testarch-{trace,nfr,test-design,test-review,atdd}/workflow.yaml`, `bmad-testarch-trace/trace-template.md`, `bmad-testarch-trace/steps-c/step-{03-map-criteria,05-gate-decision}.md`]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-27 — Story 18.5 drafted (create-story, baseline `40c7ee9`). Carries the pre-split Story 18.2 ACs
  verbatim, retargeted to Test Architect (TEA) per Story 18.1's coverage map. **Three of 18.1's TEA facts were
  corrected against upstream**: the filenames are `traceability-matrix.md` / `nfr-assessment.md` (not
  `traceability-matrix.csv` / `nfr-report.md`); `test_artifacts` defaults to `{output_folder}/test-artifacts`
  and `{output_folder}` IS SpecScribe's SourceRoot, so TEA's markdown **already renders today** and reading
  `_bmad/tea/config.yaml` is out of scope rather than a prerequisite; and TEA also writes `gate-decision.json`
  + `e2e-trace-summary.json`, which the `*.md`-only source scan structurally cannot see — the same
  invisible-artifact class as Story 18.4's `forge-report.html`. Also established that the realistic target repo
  is **BMM + TEA**, not TEA-only (TEA's own CSV declares `bmad-testarch-atdd` as `preceded-by:
  bmad-create-story:create`). Four owner decisions locked at create-story: both a Test Artifacts list page and
  a dashboard Module Coverage panel; parse `traceability-matrix.md` into the existing traceability surface;
  widen discovery to the two TEA JSON filenames (ADR-gated); and ADR 0015 Decision 5a split out to a new Story
  18.6 rather than absorbed here. Recorded the join hazard plainly — TEA's matrix is an oracle-item × test
  matrix while `traceability.html` is requirement × epic, the criterion ID format is unspecified upstream, and
  `gate-decision.json`'s `target.id` is nullable — so joins are admissible only against a resolvable
  `RequirementsModel`/`EpicsModel` id, everything else degrades to a named non-fatal notice. Gated by Story
  18.2 (`review`); shaped after Story 18.4's in-flight `IdeaDiscovery` precedent, whose files are uncommitted
  in the shared tree.
