---
baseline_commit: 8a2fb83 # HEAD at authoring time (2026-07-28)
epic: 26
frs: [FR41]
nfrs: [NFR12, NFR8]
ux_drs: [UX-DR17, UX-DR21, UX-DR22] # Epic 26's declared floor; §"The wider UX-DR set" adds seven more that bind
depends_on: [25-3] # ADR 0023 (Accepted) — the vocabulary and the three empty states
blocks: [26-2, 26-3, 26-4, 26-5, 26-6] # every Epic 26 surface story starts from this record's selections
ships_product_code: false # NO code. NO `src/` edits. The golden fingerprint MUST NOT move.
adrs: [0023, 0012, 0013, 0026] # the contract; hierarchy component; text-twin gate; generated-layer domain seeding
touches:
  - "_bmad-output/implementation-artifacts/26-1-ideation-record.md" # NEW — the deliverable
  - "_bmad-output/implementation-artifacts/26-1-ideation-where-findings-belong-in-the-portal.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/planning-artifacts/epics.md" # ONLY if a selection changes 26.4–26.6 scope (see Task 6)
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**, NOT docs/adrs/** (unless Task 7 fires)
---

# Story 26.1: IDEATION — Where Analysis Findings Belong in the Portal

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the owner,
I want to decide deliberately where and how analysis findings should appear across the portal before any surface is built,
So that the integration-point stories start from named visual direction instead of discovering it in a post-implementation revision round.

## Acceptance Criteria

1.
**Given** the entity set the owner named — code, directories, epics, stories, requirements
**When** the ideation round runs
**Then** it produces, for each candidate surface, a concrete proposal covering placement, density, empty state, and how severity reads **without color** (UX-DR17), with **2–3 named design directions** offered for every new visual surface and the owner's selection recorded
**And** it names which candidates are **in** for Stories 26.4–26.6 and which are explicitly **out**, so the integration-point stories have a closed scope.

2.
**Given** the portal already carries substantial insight surfacing
**When** placement is chosen
**Then** the record states where findings **reuse** an existing surface (code pages, code map, traceability matrix, dashboard strip) versus where a **new** page is justified, and applies UX-DR21 (one primary representation per dataset)
**And** it states what a project **without** any analysis configured sees — the default case for every user.

3.
**Given** the owner's "we could potentially fold in code analysis warnings as well"
**When** scope is set
**Then** the record states whether non-Sonar source classes are in scope for Epic 26's surfaces or deferred to Story 26.7, with the language-dependence trade-off recorded rather than left implicit.

## ⛔ Read first — what this story is, and the two traps

**This story ships NO code.** No `src/` edit, no test edit, no generated output. The golden fingerprint must be
unmoved at the end. If you find yourself editing a `.cs` file, you have left the story.

**The deliverable is one file:** `_bmad-output/implementation-artifacts/26-1-ideation-record.md`, following the
project's existing report convention (`23-5-packaging-strategy-report.md`, `25-3-spike-report.md`,
`25-5-local-coverage-report.md`).

### Trap 1 — you are the *facilitator*, not the decider

The project's standing rule is that a new visual surface gets its silhouette chosen by the **owner**, not the dev
(memory `create-story-elicit-visual-intent`; precedent Stories 9.2, 7.12, 21.1). Normally that elicitation happens
at *create-story* time and the story file carries **owner-selected** directions. **Story 26.1 is the exception:
this story IS the elicitation round.** Six surfaces × 2–3 directions is far more than a create-story preamble can
carry, which is exactly why the SCP seated it as its own story.

Section **§ Candidate design directions** below is a *pre-researched menu*, built so the owner's round is a
**selection** exercise rather than an exploratory one. **Nothing in it is decided.** Present each direction with
its named trade-off, take the owner's pick, and record the pick *and the reasoning* — including any direction the
owner invents on the spot, which supersedes the menu.

### Trap 2 — three of these decisions are ALREADY assigned to this story by upstream records

Do not treat them as optional extras. ADR 0023 and the 25.3 spike report hand them to you by name:

| Decision | Assigned by | What it needs |
|---|---|---|
| **The `relatedLocations` cap value** | `25-3-spike-report.md` § 14 item 1 — *"a surface question with real data; Story 26.1's, with the owner"* | One issue carries **52** secondary locations; 15.5 % carry any. Pick a per-surface cap. **Silent truncation is forbidden** — the cap must emit an explicit truncation count. |
| **The 10× fan-out presentation** | ADR 0023 § Consequences — *"the bounding rule itself is Story 26.5's design decision and the owner's to approve… this record deliberately stops short, because it is a presentation question and Story 26.1 owns visual direction"* | 26.5 owns the *rule*; you own the *visual direction the rule must serve*. State the direction clearly enough that 26.5 can derive its rule from it. |
| **Severity collapse, if any** | `25-3-spike-report.md` § 11 → 26.1 — *"Do not invent a second severity vocabulary. If four levels are too many for a surface, collapse in the surface and say so."* | Per surface: four levels, or a stated collapse. |

## Context & Scope

Epic 26 makes external code-quality analysis an **optional insight provider** — AD-4 applied to a *networked*
provider, which is why NFR12 exists (opt-in, offline-safe, credential-safe, **disabled by default**). Story 26.1
runs first because Epic 26's later stories (26.4 / 26.5 / 26.6) each cite *"the direction Story 26.1 selected"*
directly in their acceptance criteria. Without this record they cannot start.

**What this story does NOT decide** — every one of these is another story's, and claiming it here creates the
exact cross-story drift CLAUDE.md § Decision records warns about:

- **Ingestion posture and credentials** → Story 26.2 (spike + ratified ADR), including the PRD NFR-3 local-first
  question. Do not pre-judge SonarCloud-web-API vs on-disk export; the owner deliberately left it open (SCP
  decision D3). Design directions must survive **either** answer.
- **The findings data model** → already decided. ADR 0023 is **Accepted**. Consume it; never define a second one.
- **Configuration surface** → Story 26.3.
- **Coverage** → Epic 27 / FR42, deliberately separate. ADR 0023's non-goals: *"a per-file metric has no rule
  identity, message, severity, or location."*

## The contract you are designing against (ADR 0023, Accepted — cite by symbol, not line)

ADR 0023 (`docs/adrs/0023-agent-facing-analysis-observation-contract.md`) is the **Accepted** record — the first
Accepted ADR since 0015. Its shape is not negotiable here.

**The emitted record** (verified against the shipped emitter `tools/analysis-digest/index.mjs`, `mapSonarIssue`):

```
{ provider, kind: "fail",
  rule:     { id, name, helpUri },
  severity: { normalized, label, provider[] },
  location: { path, startLine, startColumn, endLine, endColumn },
  relatedLocations[], message,
  attachment: { basis, entities, confidence, entityCount } }
```

### Vocabulary — locked, and one of these is a live collision

| Rule | Source |
|---|---|
| **The word is "Observations", never "Findings"** on story pages | ADR 0023 § Decision 1. Story pages already render `<h3>Review Findings</h3>` (`src/SpecScribe/HtmlRenderAdapter.Epics.cs:614`, `id="sec-review-findings"`) — human, authored review prose. Two different things; "Analysis Observations" (machine, ingested, provider-attributed) vs "Review Findings" (human, authored). **The record must state the site-wide noun.** |
| Four normalized levels, SARIF `result.level` verbatim: `error` / `warning` / `note` / `none` | ADR 0023 § Decision 3 |
| **Mandatory text label ships in the payload** — `Error` / `Warning` / `Note` / `None` | ADR 0023 § Decision 3. **UX-DR17 is therefore satisfied by the contract, not by a rendering convention a surface could forget.** Your job is to make sure no direction *discards* the label, not to invent one. |
| Do not invent a second severity vocabulary; collapse in the surface and say so | 25.3 spike § 11 |

### The numbers to design density against

Measured **2026-07-28 at revision `755bd7a`** (`tools/analysis-digest/README.md` § Measurements):

- **1,488** unresolved observations · **120 error / 979 warning / 389 note / 0 none**
- **86** distinct rules · **201** files with observations · **0** unlocated
- Largest single file: `SiteGenerator.cs`, **88** observations (shard 101,668 B)
- **1 `BLOCKER`** exists and is **invisible at `severity.normalized`** — Sonar's five levels collapse into SARIF's
  four, so `BLOCKER` and `HIGH` both become `error`. It survives only in `severity.provider[]`. The 25.3 spike
  says: *"Consider surfacing it."* **That is a design question for this round.**
- `relatedLocations`: **15.5 %** of Sonar issues carry secondaries, **max 52** on one issue.

⚠️ **Re-measure before quoting.** The 25.3 spike recorded 121/960/385 and 25.4 recorded 120/979/389 — the same
repo, days apart. These numbers move. Refresh with `node tools/analysis-digest/index.mjs` and cite the revision
you measured at, per CLAUDE.md § Analysis observations.

### Attachment — the default case is "unattached", and that is the design problem

- `attachment.basis` ∈ `deep-git-commit-mining` / `unavailable` / `none`. **`unavailable` means "not computed
  here"; `none` means "genuinely unattached".** Different facts.
- `confidence` is **never `exact`** for epic or story.
- **Both `PlanningCodeImpact` call sites are gated on `--deep-git`, which is off by default. In a default run,
  100 % of observations are unattached.** ADR 0023 § Consequences: *"Surfaces must be designed for that being the
  normal case, not the exception."*
- Fan-out is **10.02×**, exposed via `entityCount`.
- **`requirement` is NOT a first-class attachment key.** ADR 0023 § Decision 5: `TraceabilityTemplater` is a
  requirement→**epic** matrix, so `observation → file → epic → requirement` is two hops with the second at epic
  granularity only, composed on a join already amplifying tenfold. *"The schema will not imply an edge that does
  not exist."* This constrains surface **S4** below hard.
- Unattached observations are *"a routed population, never a residue"* — their destination is Story 26.6's hub,
  *"the only findings surface with no entity precondition, which is why it must work with `--deep-git` off."*

### The empty states — there are FOUR, and they must be distinguishable

ADR 0023 names three; the portal's default adds a fourth, and AC #2 demands you answer it:

1. **Analysis not configured** — the default for every user. No `.specscribe/analysis/`, integration disabled.
2. **Configured, file/entity genuinely has no observations** — a real "clean" answer.
3. **Configured, observations exist but attach to no planning entity** (`basis: "none"`).
4. **Attachment was never computed** (`basis: "unavailable"` — i.e. `--deep-git` off).

CLAUDE.md § Analysis observations states the cardinal rule: **"Absent means UNKNOWN, never clean."** A surface
that renders states 1 and 2 identically is lying. UX-DR24 is the shipped precedent for distinguishing two
superficially-identical zero states (backlog vs ready-for-dev tooltips).

### Provenance / staleness — every surface inherits it

Staleness is **revision-first**, not date-first: `analysisDate` can read "an hour ago" while the revision is two
commits behind (measured, ADR 0023 § Decision 6). `isStale` **fails closed**. `workingTreeDirty: true` is itself a
staleness condition because **line numbers are anchored to `analysisRevision`** — a design consequence for any
direction that puts marks on specific source lines. Story 26.6 AC #3 requires the timestamp to use the portal-wide
date token (UX-DR25) and stale analysis to be marked honestly.

## Candidate design directions — the menu to run past the owner

**Present these; do not pick them.** Every direction below is checked against a real, verified attach point. For
each surface record: **the selection · placement · density · the four empty states · how severity reads without
color · reuse-vs-new-page (UX-DR21) · in/out for 26.4–26.6.**

---

### S1 — Code file page (file scope) → Story 26.4

Today's page is four panels assembled at `src/SpecScribe/CodeFileTemplater.cs:104-108` in a fixed order —
**Insights** (`BuildInsightsPanel`) → **Relationships** → **History** → **Code** (always present) — with empty
panels dropped and the first survivor default-checked.

> ⚠️ **The invariant that bites:** `CodeFileTemplater.cs:110-116` — when only one tab survives, the source renders
> bare with **no tab strip at all**. A findings panel that is present-but-empty on a clean uncited file would put
> a tab strip on pages that have never had one. Whichever direction is chosen must say what happens there.

> ⚠️ **Epic 27 coordination is binding here.** Epic 27 (coverage) AC 27.4 #2: *"whichever epic lands SECOND
> extends the first's code-page section rather than adding a second one."* Epic 27 has already ruled **per-line
> gutter marks OUT** for coverage. Choosing gutter marks for observations forks the two epics' treatment of the
> same gutter — record that consequence if the owner picks **C**.

| | Direction | Placement | Trade-off |
|---|---|---|---|
| **A** | **"Fifth Tab"** | A new `CodeTab` in the list at `:104-108` — full-width sortable table of the file's observations | Most room (88 observations on the worst file). Costs the one-tab invariant, and the tab's presence/absence itself signals "has observations", which conflates empty states 1 and 2. |
| **B** | **"Third Insight Panel"** | An `insight-panel` inside the existing Insights grid (`:215-244`), sibling to churn and contributors | Cheapest and most consistent — the grid already degrades to absent by returning `""` (`:199`, `:204-207`), so empty state 1 is free and byte-identity holds. Cramped at 88 rows; needs a "show all →" to the hub. |
| **C** | **"Gutter Marks + Header Pill"** | Severity markers on the `.code-line` spans at `:153`, plus a count meta-pill in the header (`:84`) | Only direction with true line fidelity, matching the data's `startLine`. But line numbers are anchored to `analysisRevision` — a dirty working tree (the normal state here, per CLAUDE.md § Concurrent work) misplaces every mark. Forks Epic 27's gutter ruling. |

**Deep-link mechanics are already solved** and must be reused, not re-invented: `#L{n}` anchors at `:153`, and a
`:target` on a source line forces the Code panel forward in CSS (`:118-119`), so a deep link survives the default
tab. Route every file→page link through `SiteGenerator.CodeItemHref` (`src/SpecScribe/SiteGenerator.cs:1849`) — a
null return means "no page", so a dead link is structurally impossible.

**`FileInsight`** (`src/SpecScribe/GitMetrics.cs:183` — `ChangeCount`, `Contributors`, `CoupledFiles`, `History`,
`TotalContributors`) is the shipped seam findings ride. Note `CoupledFile` (`GitMetrics.cs:213`) is the existing
precedent for a per-file, confidence-bearing item list — a findings list slots in the same way.

---

### S2 — Code map / directory scope → Story 26.4 AC #2

The code map (`src/SpecScribe/CodeMapTemplater.cs`) renders four precomputed variant panels, each with a colorize
dropdown, a legend bar, a **Hierarchy Explorer** chart, and an **"All files" text table** (`AppendFileTable`,
`:313`) whose columns today are `File | Lines | Type` plus six git columns when metrics exist. That table is
configured `HierarchyTwinDisplay.External` (`:197-199`) — **it IS the chart's text twin.**

Directory rollup would live where `Lines` already rolls up: `CodeMapNode` / `CodeMap.Build` (`src/SpecScribe/CodeMap.cs:16`, `:226`).
Note **no directory attachment exists in the model** — 25.3 § 11 → 26.4: *"Directory aggregation: sum over the
file scope."*

| | Direction | What changes | Trade-off |
|---|---|---|---|
| **A** | **"Seventh Dimension"** | Add an observation-density option to `HierarchyExplorer.CodeMapDimensions` (`CodeMapTemplater.cs:201`); the existing file table gains an Observations column as the twin | Zero new surfaces. UX-DR21 clean — one primary representation, one twin. Requires a severity→weight decision (is one `error` worth ten `note`s?), which is a real owner question. |
| **B** | **"Hub-Only Treemap"** | Code map untouched; a severity-weighted hierarchy lives on 26.6's hub instead | Keeps the code map about code shape. But two hierarchies over the same tree is exactly the UX-DR21 pressure the rule exists to prevent. |
| **C** | **"Table Column Only"** | An Observations column in `AppendFileTable`, no chart change at all | Cheapest, fully honest, smallest fingerprint move. No visual density signal — arguably under-serves "visualize findings alongside directories" (owner decision D2). |

Any hierarchy direction inherits **ADR 0012 § Decision 2** (one datasource, one selector, one framing block, one
text twin) and its **Addendum** constraints: one synthesized root required; `branchvalues: 'total'` is invalid
because parent weight ≠ Σ children; `null` in `values` silently renders nothing so branch values must be `0`. And
**ADR 0012 § Decision 6**: status/severity is carried on **three independent channels — fill token, hatch, and the
status word in the accessible name.** That is a directly reusable pattern for severity-without-color.

Whatever is chosen, `Charts.ChartMeta` / `Charts.Framed` (`src/SpecScribe/Charts.cs:47`, `:165`) supply the
real-value legend, analysis window, and framing sentence **by construction** — and **`ChartMetric` (`Charts.cs:13`)
must gain a member with its `WhyText` case**, because Story 10.2 AC #2 forbids hand-rolled "why this matters" copy
at call sites. `Charts.PlanningCodeImpactNote` (`Charts.cs:84`) is the shipped precedent for a provenance caveat
rendered in the `Note` slot.

---

### S3 — Epic and story pages → Story 26.5

Both pages end with a **"Code Areas Touched"** block fed by `PlanningCodeImpact`:
`src/SpecScribe/HtmlRenderAdapter.Epics.cs:252-256` (epic) and `:632-636` (story), each an opaque pre-rendered
fragment plus a conditional `toc.Add(...)`. **That block is the exact structural precedent** — including its
absent-not-empty behavior (`EpicsViewBuilder.cs:384` returns `string.Empty`).

The join produces `PlanningCodeImpactData` (`src/SpecScribe/PlanningCodeImpact.cs:21`) of `ImpactFile(Path,
CodePageHref, Churn, Commits)` (`:11`). **There is no numeric confidence on it.** Approximateness is carried by
the `AttributedCommitCount` / `TotalAnalyzedCommits` pair and by the mandatory prose caveat — the Story 21.2
cycle-time precedent that 26.5 AC #1 requires be *stated on the surface*.

| | Direction | Placement | Trade-off |
|---|---|---|---|
| **A** | **"Sibling Section"** | A new collapsed block immediately after Code Areas Touched, with its own TOC entry — level rollup + caveat + link to the hub | Room for a rollup and an honest caveat. UX-DR26 (dev-record sections collapse by default on long story pages) supports collapsed-by-default. Adds a heading adjacent to the existing "Review Findings" — **the naming collision must be resolved before this ships.** |
| **B** | **"A Chip Per Row"** | No new section — each impacted file row inside Code Areas Touched gains an observation-count chip | Strictest UX-DR21 read: one representation, one dataset. No new heading, no collision. No rollup summary, and no natural home for the caveat. |
| **C** | **"Hub Only"** | Planning pages get nothing; all attachment lives on 26.6's hub | Defensible: attachment is universally absent by default and fans out 10×. But it forfeits the epic's stated value — *"'done' carries quality context and not only a status badge"* — so this needs the owner to say it out loud. |

**The fan-out direction question to ask the owner explicitly** (ADR 0023 hands it here): at 10.02× fan-out, a
story showing every observation in every file its commits touched is noise. Does the owner want *top-N by
severity*, *counts-only with a drill link*, or *errors-only*? 26.5 turns the answer into a rule.

---

### S4 — Requirement pages → Story 26.5

`src/SpecScribe/RequirementsTemplater.cs:205` renders a header, lead text, and one Coverage block with four
mutually-exclusive branches (deferred / covered / unmapped / phantom-epic). **The page has no code-facing section
at all today** — anything here is genuinely new.

**ADR 0023 § Decision 5 already argues against it** (`requirement` is not a first-class attachment key; two hops,
second at epic granularity, on a 10× join). Only two directions are honest:

| | Direction | Trade-off |
|---|---|---|
| **A** | **"Explicitly out"** | Aligns with the Accepted contract. Record it as a *decision with a reason*, not an omission — AC #1 requires naming what is out. |
| **B** | **"Derived, doubly caveated"** | Delivers the owner's stated entity set in full. Requires a caveat compounding two approximations, on a page whose current voice is precise coverage prose. |

---

### S5 — The analysis hub → Story 26.6

A dedicated page, reachable from the insight-pages nav (FR27), mirroring **Git Insights**
(`src/SpecScribe/GitInsightsTemplater.cs:37`): kicker + `<h1>` + three meta pills, then `Append*Section` blocks.
It is **the only findings surface with no entity precondition, so it must work with `--deep-git` off** (25.3 § 11).
It is also where unattached observations are *routed*.

| | Direction | Primary representation | Trade-off |
|---|---|---|---|
| **A** | **"Triage Inbox"** | One long sortable/filterable table of all ~1,488, severity-grouped; charts demoted below | Best answer to 26.6 AC #1's *"sortable/filterable access to every finding"*. A 1,488-row page needs UX-DR28 (grouped on-page TOC) and Story 10.9's client-light sort/filter pattern. |
| **B** | **"Rule Leaderboard"** | The **86 rules** are the primary axis — rank by count/severity, drill to occurrences | Far denser and more actionable ("fix this rule once"). Buries the per-file view the code pages already serve. |
| **C** | **"Hierarchy First"** | A severity-weighted tree is primary (mirrors Git Insights' ownership sunburst), table is the twin | Most consistent with the existing hub. Inherits the full ADR 0012 contract and ADR 0013 § 3 gate. |

**Whichever wins:** ADR 0013 § Decision 3 is a **hard gate** — *"no surface retires its SVG before its twin is
audited complete… verified in a live browser with JavaScript disabled, not by test assertion alone."* The twin's
five properties (§ Decision 2) are server-rendered, complete, navigable, non-color, not visually redundant.

**Nav registration and gating** follow `src/SpecScribe/SiteNav.cs:340-368`: a `bool has…` flag on `SiteNav.Build`,
evaluated against the **data signal at nav-build time**, not against successful render. Note the shipped
asymmetry — Git Insights and Deep Analytics add a nav entry but **no** dashboard quick-link, while Code Map adds
both. The record should say which the hub follows.

---

### S6 — Dashboard signal → Story 26.6 AC #2

`src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:38` composes thirteen sections in order. The two Story 21.x strips
— Traceability (`:185`) and Delivery Cadence (`:190-195`) — share one shape: `if (view.XStripHtml.Length > 0)`
wrapping a `chart-panel` + `<h3>` + opaque fragment. **That is the template to copy.** There is also an existing
`"insights"` stat journey in the tile band (`:248`) that a single count tile could join.

| | Direction | Trade-off |
|---|---|---|
| **A** | **"Quality Strip"** | Full 21.1/21.2 parity, room for the four-level breakdown. Pushes the 13-section dashboard to 14; 26.6 AC #2 requires it not displace existing pulse content. |
| **B** | **"One Stat Tile"** | Cheapest, zero layout risk, joins a band that already exists. A single number cannot carry the four-level breakdown or the staleness note. |
| **C** | **"Tile + Strip"** | Complete, but two representations of one dataset on one page — UX-DR21 pressure. |

**UX-DR23 binds whichever wins:** paired counts are restated as a sentence (*"5/5 tasks · awaiting review"*), so
"120 error · 979 warning · 389 note" needs its sentence form, not just chips. And it must be **absent — not empty
— when disabled, which is the default.**

---

### S7 — Traceability matrix → recommend **OUT**, but record the reason

`src/SpecScribe/TraceabilityTemplater.cs:12` is a requirement × covering-epic grid in the **Delivery** nav group
(`SiteNav.cs:301`), not Insights. Adding a severity axis makes it a three-axis grid, and ADR 0023 already refused
the requirement edge. AC #1 requires naming what is out **with its reason** — this is the clearest candidate.

---

## AC #2's second half — what an unconfigured project sees

**This is the default case for every user and it must be its own section of the record, not a sentence.** The
portal has three shipped idioms; the record should name which applies where:

| Idiom | Mechanism | Shipped example |
|---|---|---|
| **(a) Absent** | Producer returns `""`; consumer's `if (…Html.Length > 0)` drops the whole panel. Acceptance test is **byte-identity** with a run that never had the data. | `CodeFileTemplater.cs:33-34`, `:92`, `:196`; `EpicsViewBuilder.cs:384`; `DashboardView.cs:146` |
| **(b) Honest empty state** | Surface must exist (a dedicated page, an always-present tab) → `<div class="chart-empty">` with a *specific* sentence. | `Charts.cs:101`; the Work Graph tab's always-present honest empty (`HtmlRenderAdapter.Epics.cs:258-263`) |
| **(c) Unconfigured-source notice** | Whole optional source off but surface still renders → `role="note"` explaining how to enable. Placed **outside** the legend, deliberately: *"a fact about the DATA, not chrome for a chart."* | `CodeMapTemplater.cs:171-174`; `Charts.cs:3107` |

The expected answer for an unconfigured project is **(a) everywhere** — no nav entry, no dashboard strip, no
sections, baseline output byte-identical, and **no network call**. Story 26.3 AC #2 makes that a requirement
(*"an existing user upgrading sees no behavior change"*). The record should confirm it explicitly rather than
leave it inferred, and should say which surfaces use **(b)** or **(c)** in the configured-but-empty cases.

## AC #3 — the non-Sonar source-class decision

The owner's words: *"we could potentially fold in code analysis warnings as well, but that gets to be language
dependent."* The Epic's own framing is that the model is **source-agnostic from the first line — Sonar is
instance #1, not the schema.** ADR 0023 already proves the model survives a second provider: it was designed
against Sonar *and* raw Roslyn SARIF, and the two differ sharply (15.5 % of Sonar issues carry secondary
locations vs 0.1 % of raw Roslyn results).

**The question for the owner is not whether the model supports it — it does — but whether Epic 26's *surfaces*
render a second source class, or whether that waits for Story 26.7's landscape survey.** Record the
language-dependence trade-off explicitly (a .NET-only analyzer produces a portal section that is empty for every
non-.NET project — an NFR8 degradation question), rather than leaving it implicit.

## The wider UX-DR set (Epic 26's declared three are the floor, not the ceiling)

Beyond UX-DR17 / UX-DR21 / UX-DR22, these bind and were surfaced by analysis:

- **UX-DR19** — *"a non-color text equivalent of every metric"*, promoted to contract by ADR 0013 § 2.
- **UX-DR23** — paired counts restated as sentences (the severity breakdown).
- **UX-DR24** — distinguishing two superficially-identical zero states. **This is the four-empty-states problem.**
- **UX-DR26** — long story pages collapse dev-record sections by default (S3 direction A).
- **UX-DR27** — `file:line` references render as styled chips, never raw syntax in prose (`location.path` +
  `startLine`, and every `relatedLocations` entry).
- **UX-DR28** — long pages keep a grouped on-page TOC (the ~1,488-row hub).
- **UX-DR9** — full-surface link cards with explicit empty states (dashboard precedent).
- **FR20** — every rendered badge routes through the `--status-*` token system with a reachable status legend. A
  severity chip **must not fork the shipped status-pill idiom** (memory `specscribe-status-token-system`).
- **FR28** — every chart carries a legend with real values, its analysis window, and one framing sentence.
- **FR21** — counts route through the single generator-side count source, never a new tally (26.5 AC #2).

**ADR 0026 § Decision 2 is a trap worth naming in the record** (Proposed, 2026-07-28): where a class is drawn from
a closed domain, **seed the domain, not the observed subset.** A generated style layer that emits only the
severity classes today's analysis run happens to produce would silently ship unstyled `none`/`error` for a project
whose run contains them. Severity is a closed four-value domain — say so, so 26.4 does not learn it the hard way.

## Tasks / Subtasks

- [x] **Task 1 — Refresh the numbers before you quote any of them** (AC: #1)
  - [x] Run `node tools/analysis-digest/index.mjs`; read `.specscribe/analysis/index.json` totals only (~31 KB — do **not** read the shards en masse; the whole digest is 1.34 MB).
  - [x] Apply CLAUDE.md's read-time staleness rule: compare `git rev-parse HEAD` against `provenance.evaluatedAtRevision`. Record the revision you measured at.
  - [x] Record the live level counts, distinct rule count, file count, unlocated count, worst-file observation count, `relatedLocations` max, and whether any `BLOCKER` still exists in `severity.provider[]`.
- [x] **Task 2 — Prepare the elicitation, one surface at a time** (AC: #1, #2)
  - [x] For each of S1–S7 above, restate the 2–3 named directions **with the refreshed numbers substituted**, so the owner is choosing against real density, not the authoring-time figures.
  - [x] Confirm each cited attach point still exists **by symbol, not line number** (CLAUDE.md § Concurrent work — a concurrent session may have moved it). Every symbol in this story was verified at `8a2fb83`; re-verify before quoting a line.
- [x] **Task 3 — Run the owner round and record selections** (AC: #1, #2)
  - [x] **Mechanic:** one surface at a time, in S1→S7 order, each as a discrete choice with its named trade-off — not seven questions at once. S1–S3 are the load-bearing ones; if the round has to be shortened, S4 and S7 are the two that can be settled by recommendation-plus-confirmation.
  - [x] Per surface capture: **selection · placement · density · all four empty states · severity-without-color · reuse-vs-new-page (UX-DR21) · in/out for 26.4–26.6**.
  - [x] Capture the **reasoning**, not just the pick — 26.4/26.5/26.6 need to derive detail decisions from it without re-asking.
  - [x] An owner-invented direction supersedes the menu; record it as such.
  - [x] Settle the three pre-assigned decisions: the **`relatedLocations` cap** (with its mandatory truncation count), the **fan-out presentation direction** for 26.5, and any **severity collapse** per surface.
  - [x] Settle the site-wide noun ("Analysis Observations") and how it coexists with the existing "Review Findings" heading on story pages.
- [x] **Task 4 — Answer AC #2's default case explicitly** (AC: #2)
  - [x] A dedicated section stating what an unconfigured project sees on every surface, mapped to idioms (a)/(b)/(c), and confirming byte-identical baseline output plus no network call.
- [x] **Task 5 — Settle AC #3** (AC: #3)
  - [x] Record the in-scope-or-deferred decision for non-Sonar source classes **with the language-dependence trade-off written down**, and what would have to be true to change the answer.
- [x] **Task 6 — Write `26-1-ideation-record.md` and close the scope** (AC: #1, #2, #3)
  - [x] Deliverable at `_bmad-output/implementation-artifacts/26-1-ideation-record.md`, following the `25-3-spike-report.md` shape: executive summary → per-surface decisions → the default case → source-class scope → handoff to 26.2–26.6.
  - [x] A **handoff section per downstream story** (26.2, 26.3, 26.4, 26.5, 26.6, 26.7) saying exactly what that story inherits — the mechanism that made 25.3's report usable.
  - [x] A closed **IN / OUT** list. AC #1 is not met by an IN list alone. — *IN and OUT lists are both closed at § 4, **except** S7, which the owner deferred to 26.6. Recorded as a named disposition with its recommendation, and the residual gap is stated in § 3.7, § 4, and § 12 rather than papered over.*
  - [x] **If a selection changes 26.4–26.6's scope, amend `epics.md` AND `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records — a change recorded in only one artifact is a drift bug). — *Six amendments, both artifacts, this change. Listed at record § 11.*
- [x] **Task 7 — ADR trigger check** (AC: #1)
  - [x] CLAUDE.md: *propose an ADR without being asked* for any decision that changes shared architecture, a cross-cutting contract, or amends a prior ADR. A visual-direction record normally does **not** trigger one — 26.2 owns Epic 26's ADR. **But** if a selection would amend ADR 0023 (e.g. requiring a field it does not carry) or ADR 0012/0013 (a new hierarchy or text-twin obligation), propose the ADR rather than burying it here. — *Evaluated and declined; the three conditions that WOULD fire it are named at record § 10.2.*
  - [x] Read `docs/adrs/` before declaring any project rule is being crossed (memory `adr-consultation-gap-three-arc-renderers`) — ADR 0010 § 1/2/6 is superseded by 0012/0013, and JS on opt-in deep-analytics surfaces is **already permitted**.
- [x] **Task 8 — Verify the no-code contract** (AC: all)
  - [x] `git status` shows **no** `src/`, `tests/`, `extension/src/`, or `web/` changes. — ⚠️ *Not literally true, and not because of this story. Those directories carry ~20 uncommitted files from a **concurrent session** (design-system + IR-content work). This story's tracked changes are exactly three files, all under `_bmad-output/`. Verified by **attribution**, not by a clean status — see record § 10.3.*
  - [x] Golden fingerprint unmoved — no generation run is required by this story. If you regenerated for any reason, confirm the fingerprint is unchanged and say so. — *No generation run performed, so the fingerprint was never measured or re-baselined. Deliberate: a concurrent session is editing `specscribe.css`, an embedded resource, so any hash measured here would have been reading somebody else's in-flight change.*
  - [x] Update `sprint-status.yaml` for `26-1-…` and add a `## Change Log` entry.

## Dev Notes

### Working conditions (CLAUDE.md, non-negotiable)

- **Another agent may be editing the same files right now.** Verify after every edit; grep for a symbol before
  relying on it. Never `git reset --hard`, `git checkout --`, or `git clean` — this has destroyed real work here.
- Expect `workingTreeDirty: true` in the digest provenance. Treat every cited line number as approximate and
  **confirm by symbol**.

### Reading the analysis digest correctly

`.specscribe/analysis/` is gitignored, dev-time only, refreshed by hand. Go straight to the shard for a file you
care about — the path is derivable (`src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`).
`index.json` is for the repo-wide view only. **Reading everything costs 1.34 MB.** No shard = no open
observations on that file. **Absent means UNKNOWN, never clean.**

### Citation discipline

**Cite ADRs by symbol/section, never by line number** — memory `cite-adrs-by-symbol-not-line-number`: ADR 0015's
refs drifted within one day. Story files survive via `baseline_commit`; ADRs do not.

⚠️ **Two requirement-numbering hazards.** (1) The FR/NFR numbers cited here (FR41, FR27, FR21, NFR12, NFR8, NFR1,
NFR7) live in **`epics.md` § Functional/NonFunctional Requirements**, not in `prd.md` — the PRD uses a separate,
independently numbered `FR-n`/`NFR-n` scheme, and epics.md records the collision in its own comments. (2)
UX-DR17/21/22/25 also live in **`epics.md` § UX Design Requirements** — the `ux-designs/` folder contains **zero**
occurrences of the string `UX-DR`. Read `epics.md` for both. (memory `nfr-numbering-collision-prd-vs-epics`)

### Project Structure Notes

- Deliverable lands in `_bmad-output/implementation-artifacts/`, alongside `25-3-spike-report.md`. This story
  writes **no** file under `src/`, `tests/`, `extension/`, `web/`, or `docs/adrs/` (unless Task 7 fires).
- Story 26.2 (spike) is the next story and consumes this record's selections as the surfaces its ingestion posture
  must be able to feed. Do not start 26.2's work here.

### Testing standards

No tests. `ships_product_code: false`. The verification for this story is Task 8: a clean `git status` over
product directories and an unmoved golden fingerprint.

## Previous Story Intelligence

There is no Story 26.0; the load-bearing predecessors are Epic 25's contract stories.

**From Story 25.3 (spike, DONE — ADR 0023 Accepted):** the vocabulary handoff in § 11 is addressed to *this
story by name* and is quoted in full above. § 14 assigns the `relatedLocations` cap here.

**From Story 25.4 (channel, status `review`):**
- ⚠️ **Sonar returns `impacts[]` in non-deterministic order.** The same issue came back as
  `[MAINTAINABILITY, RELIABILITY]` on one fetch and the reverse on the next, flipping 7 shards on identical input.
  The emitter sorts it. **This is a live hazard for Story 26.4**, which puts this shape into the Epic 22 IR — and
  the IR *is* covered by the golden fingerprint, so an unsorted array would make the fingerprint flap at random
  with no source change. `relatedLocations` is deliberately **not** sorted — a flow is an ordered sequence.
  Carry this warning into the record's 26.4 handoff.
- Owner decision **D5**: attachment is emitted as `basis: "unavailable"` and is **not computed**, because the
  fan-out bounding rule is 26.5's and the owner's. That is why every observation in the current digest reads
  "unavailable" rather than "none" — do not misread that as "nothing attaches".
- `api/rules/show` has **no `helpUri` field**; `helpUri` is synthesized as the rule's permalink in the
  organization. Relevant if a direction renders "learn more" links.

**From Story 21.1 (traceability, DONE)** — two live-browser defects the test suite structurally could not see: a
CSS containment leak causing ~2031px of phantom scroll, and a same-specificity cascade tie lost by source order.
CLAUDE.md § Verification exists because of these. Any direction chosen here must be verified in a live browser by
26.4/26.6, not by test assertion.

## Git Intelligence

`HEAD` = `8a2fb83` ("Addressed build failure"). Recent commits are batch commits (`Afternoon batch`, `Lunch
batch`, `Morning batch`) that each bundle several stories — the expected pattern here, because code review runs at
epic end (CLAUDE.md § Story lifecycle). **Scope any later review of this story by its own File List and declared
symbols, never by a commit range.** The digest's measurements were taken at `755bd7a` (`Lunch batch`), two
commits behind `HEAD`, so they are already stale by the revision-first rule — hence Task 1.

## References

- Story ACs and epic framing — `_bmad-output/planning-artifacts/epics.md` § *Epic 26* / § *Story 26.1*
- Owner decisions D1–D3, the ideation story's origin — `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-25.md` §§ 1.2, 1.3, 3
- The findings contract — `docs/adrs/0023-agent-facing-analysis-observation-contract.md` §§ Decision 1, 3, 4, 5, 6, 8, Consequences, Explicit non-goals
- Vocabulary + cap handoff — `_bmad-output/implementation-artifacts/25-3-spike-report.md` §§ 11, 14
- Live measurements, shard layout, `impacts[]` warning — `tools/analysis-digest/README.md`; `_bmad-output/implementation-artifacts/25-4-agent-consumable-findings-channel.md`
- Hierarchy component contract — `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md` §§ Decision 2, 3, 6, 7, Addendum
- Text-twin contract and the JS-off gate — `docs/adrs/0013-text-twin-is-the-no-js-contract.md` §§ Decision 1, 2, 3
- Closed-domain seeding for generated layers — `docs/adrs/0026-generated-layers-derive-from-templates-not-project-data.md` § Decision 2 (Proposed)
- Requirements and UX-DRs — `_bmad-output/planning-artifacts/epics.md` §§ Functional Requirements (FR20, FR21, FR27, FR28, FR41), NonFunctional Requirements (NFR1, NFR3, NFR7, NFR8, NFR12), UX Design Requirements (UX-DR9, 17, 19, 21, 22, 23, 24, 25, 26, 27, 28)
- AD-3 / AD-4 — `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- PRD NFR-3 and § 5 Non-Goals — `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Attach points (verified at `8a2fb83`) — `src/SpecScribe/CodeFileTemplater.cs:104-108`, `:110-116`, `:153`, `:215-244`; `src/SpecScribe/GitMetrics.cs:183`, `:213`; `src/SpecScribe/CodeMapTemplater.cs:201`, `:313`, `:171-174`; `src/SpecScribe/CodeMap.cs:16`, `:226`; `src/SpecScribe/HtmlRenderAdapter.Epics.cs:252-256`, `:614`, `:632-636`; `src/SpecScribe/PlanningCodeImpact.cs:11`, `:21`; `src/SpecScribe/RequirementsTemplater.cs:205`; `src/SpecScribe/GitInsightsTemplater.cs:37`; `src/SpecScribe/SiteNav.cs:340-368`; `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:38`, `:185`, `:190-195`, `:248`; `src/SpecScribe/TraceabilityTemplater.cs:12`; `src/SpecScribe/Charts.cs:13`, `:47`, `:84`, `:165`; `src/SpecScribe/SiteGenerator.cs:1849`; `src/SpecScribe/SettingsResolver.cs:63`
- Project working conventions — `CLAUDE.md` §§ Concurrent work, Story lifecycle, Decision records, Analysis observations, Verification

## Open Questions Raised at Create-Story (non-blocking — fold into the owner round)

These surfaced during analysis and have no answer in any existing artifact. They are not gates; each has a
workable default, but each changes a downstream story if answered differently.

1. **Severity weighting for any density visual (S2 direction A).** Is one `error` worth ten `note`s, or does
   density mean raw count? A count-weighted treemap and a severity-weighted one look completely different at
   979 warnings vs 120 errors. *Default if unanswered:* raw count, with severity as the colorize channel.
2. **Should the single `BLOCKER` be surfaced?** 25.3 says *"consider surfacing it."* Doing so means at least one
   surface reads `severity.provider[]` rather than `severity.normalized`, which is a real added coupling to
   Sonar-specific values on a model that is deliberately source-agnostic. *Default if unanswered:* no — keep
   surfaces on the normalized scale and record the collapse cost in the record.
3. **The "Review Findings" / "Analysis Observations" adjacency on story pages.** If S3 direction A wins, two
   similarly-named sections sit near each other. Rename, re-order, merge under one parent heading, or accept?
4. **Hub nav asymmetry.** Git Insights and Deep Analytics take a nav entry but **no** dashboard quick-link; Code
   Map takes both. Which does the analysis hub follow?
5. **Does the record itself need to survive a posture change?** If Story 26.2 selects on-disk export rather than
   the SonarCloud web API, does any selection here change? Directions were written to be posture-independent —
   the record should say so explicitly, so 26.2 is not blocked on re-running this round.

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), `/bmad-dev-story 26.1`, 2026-07-29. Session HEAD `630ae25` (story baseline was
`8a2fb83`, two commits behind — the frontmatter `baseline_commit` is preserved unchanged per the workflow).

### Debug Log References

- `node tools/analysis-digest/index.mjs` — digest regenerated at `630ae25`. Output:
  `1534 observations (125 error, 1013 warning, 396 note, 0 none) across 208 shards + 0 unlocated`;
  `provenance: analysis 630ae25 | tree 630ae25 DIRTY | commitsBehind 0 | isStale true [working-tree-dirty]`.
- Three throwaway measurement scripts in the session scratchpad (not in the repo) walked all 208 shards to derive
  what `index.json` does not carry: distinct rule count, `relatedLocations` distribution and cap trade-offs,
  `severity.provider[]` axis shapes, `BLOCKER` presence, the engine × directory split, and the directory rollup.
  Kept out of the repo deliberately — `ships_product_code: false`, and the numbers move (§ 1.1 of the record).
- Symbol re-verification by `grep`, not by line number, per CLAUDE.md § Concurrent work. Five cited lines had
  drifted since `8a2fb83`; corrections tabulated at record § 2.

### Completion Notes List

1. **Deliverable:** `_bmad-output/implementation-artifacts/26-1-ideation-record.md` (~61 KB, 13 sections), following
   the `25-3-spike-report.md` shape. Every selection in it is the owner's; this session facilitated.
2. **All seven surfaces settled.** S1 = B (Third Insight Panel) · S2 = A (Seventh Dimension) with **two selectable
   weightings** · S3 = B (chip per row) **+ a rollup sentence** · S4 = **explicitly OUT** · S5 = **owner-invented
   three-page hub** (landing + rule leaderboard + triage inbox), superseding the pre-researched menu · S6 = A
   (Quality Strip) · S7 = **deferred to 26.6** with the OUT recommendation standing.
3. **The three upstream-assigned decisions are settled:** `relatedLocations` cap = **5**, uniform, with a mandatory
   explicit "+ N more locations" count (136 of the 237 issues carrying secondaries exceed it, so the truncation
   notice is the *common* case and must be first-class); fan-out presentation = **total-count chip + rollup
   sentence**, from which 26.5 derives a rule governing which file *rows* appear rather than a per-observation cap;
   severity collapse = **none**, four levels on every surface, `BLOCKER` **not** surfaced.
4. **⚠️ AC #1's OUT list is closed except for one candidate.** The owner chose to keep S7 (the traceability matrix)
   open for 26.6 rather than close it in this round. That is a named disposition with a recorded recommendation, not
   an omission — but it is not the same thing as a fully closed scope, and the record says so plainly in three
   places (§ 3.7, § 4, § 12) rather than claiming otherwise. `epics.md` gained a new 26.6 AC #4 making the deferred
   decision that story's, so it cannot fall between the two.
5. **Three measurements changed the round away from the story file's own defaults.** (a) `tests/SpecScribe.Tests`
   is **39 % of all observations (599) and contains exactly one error** — a raw-count density tree therefore names
   the test tree the worst region in the repository, which is false; this is why S2 took two weightings instead of
   the story's stated raw-count default. (b) **859 of 1,534 (56 %) are already `external_roslyn:` rules** — Roslyn
   analyzer output *already* flows through SonarCloud, and it is 100 % .NET-specific, which reframed AC #3 from
   "should we add a second source class" to "should we distinguish the one already here". (c) **`severity.provider[]`
   is not a flat string list** but an array of axis records (`mqr.softwareQuality`, `legacy.type`), exposing a
   quality dimension the story file did not name — and that is what the hub's "type" sort reads.
6. **AC #3 = in scope as-is, no engine distinction rendered.** Not a deferral: the non-Sonar population in the
   payload is fully rendered, just not labelled by engine. The language-dependence trade-off is written down with a
   concrete figure — a non-.NET project sees roughly **44 %** of this repository's density, degrading in *density*
   rather than *correctness*, so nothing becomes empty-but-present or broken (NFR8).
7. **AC #2's default case = idiom (a), absent, everywhere.** No nav entry, no strip, no panel, no chips,
   byte-identical baseline output, and no network call. Idiom (b) applies only where a surface must exist anyway
   (the hub pages and the S1 panel when analysis *is* configured but clean), always with a **specific** sentence —
   a generic "no data" is indistinguishable from "not configured" to a reader, which is the CLAUDE.md
   "absent means UNKNOWN, never clean" violation.
8. **Site-wide noun settled, and the owner chose to act on it now.** "Analysis Observations" is the machine noun;
   story pages get one **Quality** parent heading with `Review Findings` and Analysis Observations as sibling
   subsections. This **moves shipped markup and TOC depth**, so 26.5's fingerprint move is larger than the chips
   alone — recorded as new work in `epics.md` 26.5 AC #4, with `id="sec-review-findings"` to be preserved as an
   anchor. Epic pages are unaffected: `ReviewFindingsHtml` renders in the story branch only.
9. **Two structural traps found that the story file did not know about.** Code-page tabs are assembled at **two**
   call sites (`CodeFileTemplater.cs:104-108` **and** `:786-790`) — an additional argument for S1 = B, and a trap
   for anyone who revisits S1 = A. And **every dashboard panel is `wm-show-*` gated**
   (`specscribe.css:7041-7045`, five modes) — an undeclared Quality Strip would be invisible in *every* workflow
   mode. Both carried into the 26.4 / 26.6 handoffs.
10. **ADR trigger evaluated and declined,** with `docs/adrs/` read first. Nothing here amends ADR 0023 (consumed),
    0012/0013 (complied with — S2 adds a dimension to an existing Explorer whose twin is already `External`, and
    S5 deliberately adds **no** hierarchy), or 0026 (applied). Epic 26's ADR is 26.2's by design. The three
    conditions that *would* fire one are named at record § 10.2 so they cannot be missed later.
11. **Six structural amendments landed in `epics.md` AND `sprint-status.yaml` in this same change** (record § 11):
    26.5 AC #1 narrowed + new AC #4; 26.6 AC #1 rewritten to three pages + new AC #4; sprint-status mirrors for
    26.4/26.5/26.6; and a 26.7 scope refinement.
12. **⚠️ Task 8's literal check does not pass, and it is not this story's doing.** `src/`, `tests/`, and `web/`
    carry ~20 uncommitted files from a **concurrent session** (design-system + IR-content work, including
    `specscribe.css` and a new `web/assets/shared-primitives.css`). This story authored none of them and left them
    strictly alone — no `git reset --hard`, no `git checkout --`, no `git clean`. Verified by **attribution**:
    `git status --porcelain -- _bmad-output/` shows exactly this story's three files.
13. **No generation run was performed, deliberately.** With a concurrent session editing `specscribe.css` — an
    embedded resource — any fingerprint measured here would have been reading somebody else's in-flight change, and
    an incremental build would not have re-embedded it anyway. The fingerprint was never measured, moved, or
    re-baselined by this story.
14. **All five create-story open questions resolved** (record § 12): severity weighting → two selectable weightings
    (against the stated default); `BLOCKER` → no; the Review Findings adjacency → re-parent now; hub nav asymmetry
    → nav entry, no quick-link tile, *derived* from S6 = A rather than decided separately; posture-independence →
    yes, explicitly, so 26.2 is not blocked on re-running this round.
15. **A standing hazard carried into the 26.4 handoff:** Sonar returns `impacts[]` in **non-deterministic order**
    (the emitter sorts it; seven shards flipped on identical input). 26.4 puts this shape into the Epic 22 IR, and
    the IR *is* covered by the golden fingerprint — unsorted, the fingerprint would flap at random with no source
    change. `relatedLocations` is deliberately **not** sorted; do not "fix" that by symmetry.

### File List

- `_bmad-output/implementation-artifacts/26-1-ideation-record.md` — **NEW.** The deliverable.
- `_bmad-output/planning-artifacts/epics.md` — MODIFIED. Story 26.5 AC #1 narrowed (requirement pages out) + new
  AC #4 (the Quality-heading re-parenting); Story 26.6 AC #1 rewritten (three-page hub + reader-selectable
  highlight ranking) + new AC #4 (26.6 decides S7). Each amendment carries a dated `<!-- AMENDED … -->` comment
  preserving the prior wording and its cause.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED. `26-1-…` → `review`; `last_updated` entry;
  inherited-selection context notes on 26-4 / 26-5 / 26-6; scope-refinement note on 26-7.
- `_bmad-output/implementation-artifacts/26-1-ideation-where-findings-belong-in-the-portal.md` — MODIFIED (this
  file): task checkboxes, Dev Agent Record, File List, Change Log, Status.

Not in the File List, deliberately: `.specscribe/analysis/` (regenerated in Task 1, but **gitignored and dev-time
only**), and the three measurement scripts (session scratchpad, never in the repo).

## Change Log

- 2026-07-29: **Ideation round run with the owner; story complete.** Digest refreshed at `630ae25` (1,534
  observations — 125/1,013/396/0 — vs the story's 1,488 at `755bd7a`, the third movement in days). All seven
  candidate surfaces settled: S1 = B, S2 = A with two selectable weightings, S3 = B plus a rollup sentence,
  S4 = explicitly out, S5 = an **owner-invented** three-page hub superseding the menu, S6 = A, S7 = deferred to
  26.6. The three upstream-assigned decisions settled: `relatedLocations` cap = 5 with a mandatory truncation
  count, fan-out = total-count chip + rollup, severity collapse = none and no `BLOCKER`. Site-wide noun =
  "Analysis Observations" with the story-page `Review Findings` re-parented under a Quality heading now. AC #3 =
  in scope as-is with no engine distinction, on the finding that 56 % of the payload is already `external_roslyn`.
  AC #2's default case = idiom (a) everywhere. Deliverable written; six structural amendments landed in `epics.md`
  and `sprint-status.yaml` in the same change; ADR trigger evaluated and declined with its firing conditions
  named. No product code, no generation run, fingerprint never measured. Status → review.
- 2026-07-28: Story created. Context assembled from Epic 26, SCP 2026-07-25, ADR 0023 (Accepted), the 25.3 spike report, the shipped 25.4 digest, and a verified attach-point inventory across the portal's code, directory, planning, hub, and dashboard surfaces. Candidate design directions pre-researched for seven surfaces so the owner's round is a selection exercise. Status → ready-for-dev.
