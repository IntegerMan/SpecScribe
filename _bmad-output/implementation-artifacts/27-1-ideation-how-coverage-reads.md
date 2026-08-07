---
baseline_commit: c73ebcb # HEAD at authoring time (2026-08-07)
epic: 27
frs: [FR42]
nfrs: [NFR12, NFR8]
ux_drs: [UX-DR17, UX-DR21, UX-DR22] # Epic 27's declared floor; § "The wider UX-DR set" adds six more that bind
depends_on: [] # 27.1 is the first story in Epic 27 and has no in-epic predecessor
blocks: [27-2, 27-3, 27-4, 27-5, 27-6] # every Epic 27 surface story starts from this record's selections
ships_product_code: false # NO code. NO `src/`, `web/`, `tests/`, `extension/` edits.
adrs: [0010, 0012, 0013, 0026, 0031, 0033, 0034] # hierarchy component, text-twin gate, closed-domain seeding, gates, the IR
touches:
  - "_bmad-output/implementation-artifacts/27-1-ideation-record.md" # NEW — the deliverable
  - "_bmad-output/implementation-artifacts/27-1-ideation-how-coverage-reads.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/planning-artifacts/epics.md" # ONLY if a selection changes 27.3–27.6 scope (see Task 7)
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**, NOT docs/adrs/** (unless Task 8 fires)
---

# Story 27.1: IDEATION — How Coverage Should Read Across the Portal

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the owner,
I want to decide deliberately how coverage should read on each surface before any of it is built,
So that the implementation stories start from named visual direction instead of discovering it in a
post-implementation revision round.

## Acceptance Criteria

Verbatim from `epics.md` § *Story 27.1*. This story does not extend them — every decision below lands *inside*
these three ACs.

1.
**Given** the surfaces the owner named — the Code Map treemap, the sunburst / Hierarchy Explorer, and code file pages
**When** the ideation round runs
**Then** it produces for each surface a concrete proposal covering placement, density, empty state, and **how coverage reads without color** (UX-DR17), with **2–3 named design directions** offered per surface and the owner's selection recorded
**And** it names which surfaces are **in** for Stories 27.4–27.6 and which are explicitly **out**, so those stories have a closed scope.

2.
**Given** coverage is a continuous 0–100% value while the portal's existing encodings are categorical status tokens
**When** the visual direction is chosen
**Then** it states whether coverage gets a **new** scale or reuses an existing token family, and if new, how it stays distinguishable from the six `--status-*` tokens that already carry stage meaning
**And** it decides what an **unknown**-coverage file looks like — a file absent from the report is not the same as a file at 0%, and conflating them would be a lie the eye cannot catch.

3.
**Given** the hierarchy surfaces already encode weight and status
**When** coverage is added to them
**Then** the proposal states what coverage **replaces or coexists with**, honoring UX-DR21's one-primary-representation rule rather than stacking a third meaning onto one wedge.

## ⛔ Read first — what this story is, and the four traps

**This story ships NO code.** No `src/`, `web/`, `tests/`, or `extension/` edit; no generated output. If you find
yourself editing a `.cs`, `.vue`, or `.css` file, you have left the story.

**The deliverable is one file:** `_bmad-output/implementation-artifacts/27-1-ideation-record.md`, following the
project's report convention — `26-1-ideation-record.md` is the direct sibling and the closest shape to copy;
`25-3-spike-report.md` and `23-5-packaging-strategy-report.md` are the other two.

### Trap 1 — you are the *facilitator*, not the decider

The project's standing rule is that a new visual surface gets its silhouette chosen by the **owner**, not the dev
(CLAUDE.md § Story lifecycle step 1; precedent Stories 9.2, 7.12, 21.1). Normally that elicitation happens at
*create-story* time. **Story 27.1, like 26.1 before it, is the exception: this story IS the elicitation round.**

§ *Candidate design directions* below is a **pre-researched menu**, built so the owner's round is a **selection**
exercise rather than an exploratory one. **Nothing in it is decided.** Present each direction with its named
trade-off, take the owner's pick, and record the pick *and the reasoning* — including any direction the owner
invents on the spot, which supersedes the menu. (In Story 26.1 the owner invented one of the seven outright.)

### Trap 2 — do NOT settle the vocabulary here. It is Story 27.2's, and it is worse than epics.md says

Epic 27's own note calls the naming collision "load-bearing" and assigns it to **27.2 AC #3**, which must fix it
**via a ratified ADR before any surface ships**. Your round has to *describe* surfaces without *naming* the
feature. Use an explicit placeholder (this story file uses **⟪TERM⟫**) and hand 27.2 the collision inventory at
§ *The vocabulary collision is bigger than epics.md records*.

**Why this matters more than it looks:** epics.md names three colliding symbols. This story's analysis found
**six**, and the sharpest one is not in epics.md at all — the portal *already ships an `<h2>` reading literally
"Test coverage"*. If your round casually adopts "Test coverage" as the user-facing term, you have pre-empted
27.2's ADR with the one phrase that is already taken.

### Trap 3 — unknown is the MAJORITY case here, not the edge case

AC #2 treats "a file absent from the report" as a distinction to preserve. The measured reality is stronger than
that: on this repository **most nodes on the Code Map have no coverage figure at all** (§ *The measurements*,
finding M2). Story 26.1 hit the identical shape — 100 % of observations are unattached in a default run, and
ADR 0023 § Consequences had to say *"surfaces must be designed for that being the normal case, not the
exception."* **Design for unknown first and covered second, not the reverse.**

### Trap 4 — the shipped sequential ramp has the opposite polarity to coverage

Every existing ramp dimension on the Code Map means **more colour = more activity = more worth your attention**.
Coverage inverts that: more coverage is *better*, i.e. *less* worth your attention. Reusing the gold ramp as-is
would make a well-tested file glow exactly like a churn hotspot. § *AC #2 — the scale question* works this
through; it is the single most consequential thing in the round and it has a clean resolution the owner should be
offered explicitly.

## Context & Scope

Epic 27 makes test coverage an **optional insight provider** — AD-4 applied to a **purely local** provider: a
coverage report already on disk. No network, no credential, no service dependency. That is the sharp difference
from Epic 26, and it is why NFR12's tension does not arise here except for the one optional link-out.

Story 27.1 runs first because Stories 27.4, 27.5 and 27.6 each cite *"Story 27.1's chosen direction"* directly in
their acceptance criteria. Without this record they cannot start.

**Owner-directed scope, already set (2026-07-26):** rollups and analytics, **not per-line marks**. Per-file and
per-directory percentages with covered/total **line counts** carried as numbers. Per-line gutter marks are
explicitly **out** — Story 27.6 AC #3 revisits that on evidence. Do not reopen it in this round; record it as a
standing constraint the directions must respect.

**What this story does NOT decide.** Every one of these belongs to another story, and claiming it here creates
exactly the cross-story drift CLAUDE.md § Decision records warns about:

- **Report formats, discovery, path mapping, staleness disclosure, and the vocabulary** → **Story 27.2** (spike +
  ratified ADR). Your directions must survive **any** of Cobertura / OpenCover / lcov and **either** an explicit
  setting or convention-based discovery. Do not pre-judge.
- **The metric model** (per-file record, line-weighted directory rollups, the unknown representation in *data*)
  → **Story 27.3**. You decide what unknown *looks like*; 27.3 decides how it is *represented*.
- **The ranking rule for churn × coverage** → **Story 27.6**. You decide the visual direction the rule must
  serve; 27.6 derives the rule from it. (This is the same split ADR 0023 used between 26.1 and 26.5.)
- **Findings/observations** → Epic 26, deliberately separate.

## The measurements — design density against these, not against intuition

Taken **2026-08-07** from SonarCloud's public API for `IntegerMan_SpecScribe`
(`api/measures/component_tree`, `metricKeys=line_coverage,uncovered_lines,lines_to_cover`, `qualifiers=FIL`).
**The project is public and the endpoint answers anonymously** — no token, no `gh auth` (Story 25.5 § 3 verified
this). Repo file counts from `git ls-files` at `c73ebcb`.

> ⚠️ **Re-measure before quoting any of it.** Story 25.5 recorded the project figure moving 89.8 → 87.6 → 89.7
> across days, and 26.1 watched its own counts move three times in a week. Task 1 refreshes these and records the
> revision measured at.

> ⚠️ **These are SonarCloud's numbers, and Sonar's headline `coverage` metric is NOT line coverage.** It blends
> lines and branches: `(covered_lines + covered_conditions) / (lines_to_cover + conditions_to_cover)`. Everything
> below uses `line_coverage` specifically, because that is the figure a Cobertura/OpenCover report actually
> carries per file and therefore the one Epic 27 will render. Story 25.5 § 2 is the full reconciliation; do not
> re-derive it and do not compare a per-file line-coverage figure against the project `coverage` badge.

### M1 — the distribution is pathologically top-heavy

Of the **162** files carrying a `line_coverage` figure:

| band | files | share |
|---|---:|---:|
| exactly 100 % | 72 | 44 % |
| 90–100 % | 62 | 38 % |
| 75–90 % | 12 | 7 % |
| 50–75 % | 2 | 1 % |
| 25–50 % | 3 | 2 % |
| 0–25 % | 1 | 1 % |
| exactly 0 % | 10 | 6 % |

**134 of 162 files (83 %) sit at ≥ 90 %.** A from-zero five-level linear ramp puts 83 % of the coloured
population in the top bucket and the map reads as a near-uniform block. This is the *same* failure the shipped
code already documents for the date dimensions — `HierarchyExplorer.Projectors.cs`, `CodeMapDimensions`:
*"Absolute day-numbers are ~739,000 and differ by hundreds, so a from-zero ramp would put every file in the top
bucket"* — which is why `RampWindow` exists. **But a data-relative window is the wrong fix for a percentage**
(see M4). This tension is real, it has shipped precedent on both sides, and it is the owner's to resolve.

### M2 — unknown is the majority, by a wide margin

| population | count |
|---|---:|
| tracked files in the repo (`git ls-files`) | **1,352** |
| of those, code-ish (`.cs .ts .js .mjs .vue .css`) | **411** |
| files SonarCloud analyses at all | **238** |
| files carrying a `line_coverage` figure | **162** |

The Code Map renders the repository's file tree, not the analysed subset. So on the map, **a coverage figure
exists for roughly one node in eight**. Even restricted to code files it is **162 / 411 ≈ 39 %**.

Within the analysed set alone, **76 of 238 files (32 %) have no coverage measure**, and they are not junk:
50 `js`, 13 `json`, 5 `css`, 4 `ts`, 2 `yaml`, **2 `cs`**. Unknown spans real source files in real languages.

**Consequence for AC #2:** "what does unknown look like" is not a polish question about an edge state. It is the
question of what **most of the surface** looks like. A direction that makes unknown visually quiet makes the
whole map quiet; a direction that makes unknown loud makes the whole map loud. Ask the owner to choose against
*this* ratio, not against an imagined one.

### M3 — "0 %" usually means "not a test target", not "risk"

The ten files at exactly 0 %, largest first:

| lines to cover | file |
|---:|---|
| 767 | `extension/src/extension.ts` |
| 60 | `src/SpecScribe/Program.cs` |
| 36 | `src/SpecScribe/GenerationReporter.cs` |
| 15 | `web/nuxt.config.ts` |
| 12 | `extension/esbuild.js` |
| 10 | `web/ir/adapter.client.ts` |
| 6 | `web/utils/measure-rows.ts` |
| 2 | `web/components/IrMain.ts` |
| 1 | `web/components/IrHtml.ts` |
| 1 | `web/vitest.config.ts` |

Entry points, a console reporter, build configs, a VS Code activation shim. And the largest single testing debt
in the repository by uncovered lines is:

| uncovered | coverage | file |
|---:|---:|---|
| 767 | 0.0 % | `extension/src/extension.ts` |
| 323 | 90.6 % | `src/SpecScribe/SiteGenerator.cs` |
| 321 | 37.5 % | `src/SpecScribe/Commands.cs` |
| 184 | 1.6 % | `src/SpecScribe/ConsoleUi.cs` |
| 151 | 38.1 % | `src/SpecScribe/NuxtPrerender.cs` |
| 84 | 31.1 % | `src/SpecScribe/ConfigCommand.cs` |

**Note `SiteGenerator.cs`: 90.6 % covered and the second-largest absolute debt in the repo.** A percentage-only
encoding renders it as "fine". This is the strongest single argument for the covered/total **counts** the owner
already asked to keep — and it is a direct argument for one of the treemap directions below.

This is the same class of finding as Story 26.1's: there, a raw-count density tree named the *test tree* the
worst region in the repository, which was false, and it changed the owner's selection. Expect the equivalent
here — **a naive "low coverage = bad" encoding names the CLI entry point the worst file in SpecScribe.**

### M4 — line-weighted vs mean-of-percentages barely diverges *on this repo*

Story 27.3 AC #1 requires line-weighted directory rollups, *"never a mean of percentages, which would let a
3-line file outvote a 3,000-line one."* **The rule is correct. This repository will not demonstrate it:**

| directory | files | line-weighted | mean-of-% | delta |
|---|---:|---:|---:|---:|
| `src/SpecScribe` | 150 | 93.2 % | 93.8 % | **+0.6** |
| `web/ir` | 4 | 66.4 % | 64.5 % | −1.9 |

Carry this into the 27.3 handoff explicitly: **27.3 must prove the line-weighting with a synthetic fixture, not
by pointing at SpecScribe's own numbers**, or it will "verify" a rule against data where both formulas agree to
within a rounding step and learn nothing.

### M5 — the repo-wide figures, for the record

Across the 162 measured files: **28,562 lines to cover, 2,727 uncovered → 90.45 % line coverage.** Compare only
against Sonar's `line_coverage`, never its blended `coverage`. `src/SpecScribe` alone is **93.2 %** line-weighted.

## AC #2 — the scale question, worked through

This is the load-bearing decision of the round. Three shipped facts frame it.

### Fact 1 — the `--status-*` tokens are out, and the codebase already says so twice

There are exactly **six** stage tokens (`specscribe.css`): `--status-pending`, `--status-drafted`,
`--status-ready`, `--status-active`, `--status-review`, `--status-done`, plus `--status-deferred` and
`--status-unrecognized` outside the stage set. The stylesheet already refuses to route non-lifecycle signals
through them, in two separate comments:

> *"Code mass/churn is NOT a lifecycle stage, so nothing here routes through the `--status-*` tokens (those stay
> the single stage→colour source)."* — the Code Map block

> *"Also deliberately off the `--status-*` lifecycle tokens (file type is not a lifecycle signal)."* — the
> file-type palette

**Coverage is not a lifecycle stage.** The precedent is settled: it does not use the stage tokens. AC #2's "how
does it stay distinguishable from the six `--status-*` tokens" is therefore answered by *not being in that family
at all* — but the record must still say so, because AC #2 asks.

### Fact 2 — there are three shipped scale kinds, and one is already a percentage

`HierarchyDimensionKind` (`src/SpecScribe/HierarchyExplorer.cs`) ships `Categorical`, `Ramp`, `RampWindow`,
`Cutoff`, `Roster` and others. The three that matter:

| kind | how it buckets | shipped user | why |
|---|---|---|---|
| `Ramp` | from zero, over the file set | change frequency, churn, avg change size, co-change | counts are meaningful from zero |
| `RampWindow` | scaled to the set's own [min,max] | recency of first/last change | *"a from-zero ramp would put every file in the top bucket"* |
| **`Cutoff`** | **fixed ascending cut points** (`new[] { 25, 50, 75 }`) | **ownership dominant-author share — a percentage** | *"a share percentage is meaningful on its own scale, so 76–100 % means the same thing on every repo's chart"* |

**`Cutoff` is the exact shipped precedent for a percentage dimension, and its recorded reasoning transfers to
coverage verbatim.** A coverage figure means the same thing on every repository; it must not be rescaled to the
local maximum, or "green" would mean "best in this repo" rather than "well tested". **A data-relative
`RampWindow` for coverage would be a lie of the same family AC #2 is trying to prevent.**

That settles *kind*. It does **not** settle *cut points* — and M1 says 25/50/75 puts 83 % of files in the top
bucket. Candidate cut sets to put to the owner: `25/50/75` (reuse ownership's, consistent), `50/75/90`,
`60/80/95` (spreads *this* repo's population), or a named risk-oriented set. Whatever is chosen must be
**fixed and repo-independent**.

### Fact 3 — polarity, and the clean resolution (Trap 4)

The gold sequential ramp `level-0 … level-4` is shared: *"Levels reuse the SAME values as the commit heatmap so
the two sequential ramps can never desync."* On every current consumer, **more gold = more activity = look
here**. Coverage reverses that: high coverage is good.

Two coherent options, and the owner should be asked directly:

- **(P1) Encode the deficit, not the virtue.** Colour by *uncovered-ness* (`100 − coverage`, or uncovered line
  count). Then "more gold = more attention" holds unchanged, the ramp is reused honestly, and no new token family
  is needed. The legend and accessible name say "uncovered", so nothing is ambiguous. This also composes with
  treemap direction **C** below and with 27.6's risk framing.
- **(P2) A new, distinct family for coverage**, polarity as-is (more colour = better covered). Costs a new token
  set that must be distinguishable from both the gold ramp and the categorical file-type hues, and it introduces
  the portal's first "more colour is good" scale — a reading the rest of the site does not use.

**Recommendation to present, not to impose: P1.** It is cheaper, it reuses a token family the project already
guards against desync, it inverts nothing about how the portal is read, and it makes the map answer the question
the epic actually asks — *where is the testing debt* — rather than *where is the virtue*.

### The unknown state — the non-color answer already ships

AC #2's second half has a shipped, contract-level solution; **do not invent a second one**:

- `HierarchyDimension.NoneClass` defaults to `"level-none"`, and `.codemap-cell.level-none` is
  `fill: var(--parchment-dark); fill-opacity: 0.55` — documented as *"a file with no git record (neutral, clearly
  'no data' — never the sole signal)"*.
- The accessible-name contract already carries it: `RampText["none"] = "no data for {label}"`, distinct from
  `["value"] = "{label}: {level}"`. **UX-DR17 is satisfied by the component contract, not by a rendering
  convention a surface could forget** — your job is to make sure no direction *discards* it.
- `.codemap-cell.type-other` adds `stroke-dasharray: 2 1` — a **non-color** channel for the unknown bucket,
  mirroring `--status-unrecognized-hatch`'s recorded principle that *"the unknown bucket looks distinct, not like
  a 6th invented hue"*.

So: **unknown = neutral fill + dashed stroke + "no data" in the accessible name**, three channels, none of them
colour-alone. A file at 0 % gets a real ramp level and a real number. The two can never be confused.

⚠️ **But M2 makes this a density decision, not just a token decision.** If ~7 nodes in 8 are `level-none`, a
dashed stroke on every one of them is visual noise across the whole map. Ask the owner whether unknown nodes
should be (i) neutral + dashed everywhere, (ii) neutral only, with the dash reserved for the text twin and
tooltip, or (iii) filtered out of the coverage view entirely via the existing precomputed-panel filter mechanism.
Option (iii) is cheap — the Code Map's exclusion checkboxes are **pure CSS over four precomputed panels, needing
no script at all** — but it hides the honest "we don't know" answer, which is the CLAUDE.md
*"absent means UNKNOWN, never clean"* violation in visual form. That is a genuine trade-off, not a lookup.

## The vocabulary collision is bigger than epics.md records

Epic 27's note names three colliding symbols. **Six were found.** Hand this whole table to Story 27.2 — it owns
the ADR (27.2 AC #3), and its job is materially harder than epics.md implies.

| # | What already means "coverage" | Where | What it actually means |
|---|---|---|---|
| 1 | `ArtifactCoverage` class, `SiteGenerator.RefreshCoverage()`, the dashboard **"Planning Artifacts"** panel | `src/SpecScribe/ArtifactCoverage.cs`, `SiteGenerator.cs`, `Charts.cs` | which planning-artifact families a project has |
| 2 | `DashboardView.Coverage` | `src/SpecScribe/DashboardView.cs` | same as #1, on the view model |
| 3 | `<h3>Coverage</h3>` on the epic card | `src/SpecScribe/RequirementsTemplater.cs` | requirement → epic coverage |
| 4 | **`<h2>Test coverage</h2>`, `id="ta-coverage"`, `.ta-section.ta-coverage`** | `src/SpecScribe/TestArtifactsTemplater.cs` | **BMAD test-artifact priority coverage against a module's "coverage oracle" — nothing to do with executed lines** |
| 5 | The whole `.coverage-*` CSS namespace (~30 rules: `-card`, `-chip`, `-meter`, `-meter-fill`, `-pct`, `-grid`, `-family`, `-freshness`, `-cta`, …) | `src/SpecScribe/assets/specscribe.css` | #1 and #3 — and it **already collides with itself**: `.coverage-card` is defined **twice**, once for the dashboard panel and again for the requirements mosaic |
| 6 | The traceability **coverage** matrix (Story 21.1) | `src/SpecScribe/TraceabilityTemplater.cs` | requirement × covering-epic grid |

**#4 is the one that changes the answer.** The portal already ships a page section headed *literally* "Test
coverage", on a **test-related page**, meaning something entirely different. Confusion there is maximal, not
incidental. And #5 means Epic 27 cannot simply take `.coverage-*` for its CSS — the namespace is occupied and
internally inconsistent already.

Also note the **meter idiom in #5 is exactly what a coverage percentage wants**: `.coverage-meter` /
`.coverage-meter-fill` / `.coverage-pct` is a shipped, styled percentage bar. It belongs to planning-artifact
coverage. Reusing its *visual form* under a different class name is legitimate and cheap; reusing its *class
names* is the collision. Say which you mean.

**Your obligation in this round:** use **⟪TERM⟫** as a placeholder throughout, record that the noun is 27.2's,
and hand over this table. Do not let the round settle on a word.

## Candidate design directions — the menu to run past the owner

**Present these; do not pick them.** Every direction is checked against a real attach point verified at
`c73ebcb`. For each surface record: **the selection · placement · density · the empty/unknown states · how
coverage reads without colour · reuse-vs-new-page (UX-DR21) · in/out for 27.4–27.6.**

---

### S1 — Code file pages → Story 27.4

The page is assembled in `src/SpecScribe/CodeFileTemplater.cs`: a `List<CodeTab>` of at most four —
**Insights** (`BuildInsightsPanel`) → **Relationships** → **History** → **Code** (always present) — with empty
panels dropped and the first survivor default-checked. `BuildInsightsPanel` emits a
`<div class="insight-panels">` grid of `<section class="insight-panel code-insight-block">` siblings.

> ⚠️ **The one-tab invariant.** When only one tab survives, the source renders bare with **no tab strip at all**.
> A coverage panel that is present-but-empty on an unmeasured file would put a tab strip on pages that have never
> had one. Whichever direction wins must say what happens there.

> ⚠️ **Tabs are assembled at TWO call sites**, not one — Story 26.1 § Completion Note 9 found the second. Verify
> both before any direction that adds a tab.

> ⚠️ **The Epic 26 coordination constraint (27.4 AC #2) is LIVE and its ordering is still open.** Story 26.1 is
> `review`; **26.2 is `ready-for-dev` and 26.3–26.6 are all `backlog`.** Epic 26 has therefore *decided* its
> code-page direction but **shipped no surface**. Every Epic 27 story is `backlog`. **Which epic lands second is
> genuinely undetermined**, so this record must be written to survive **either** order — exactly as 26.1 made its
> selections posture-independent so 26.2 could not invalidate them.
>
> What Epic 26 already chose (26.1 record, S1 = **B, "Third Insight Panel"**): observations go in an
> `insight-panel` inside the existing Insights grid, *not* a new tab, *not* gutter marks. 26.1 explicitly noted
> *"S1 = B is directly compatible with [27.4 AC #2]: whichever of Epic 26 / Epic 27 lands second extends the same
> Insights grid."* **A sibling `insight-panel` in that same grid is the reading of "extends the existing section"
> that both epics have already converged on.** A *second grid*, or a new tab, would not be.

| | Direction | Placement | Trade-off |
|---|---|---|---|
| **A** | **"Fourth Insight Panel"** | A new `insight-panel` in the existing grid, sibling to churn, contributors and (later) observations | Cheapest and most consistent; the grid already degrades to absent by returning `""`, so the unconfigured case is free. Converges with 26.1's S1 = B. Cramped if the panel must carry %, counts, unknown state *and* the link-out. |
| **B** | **"Header meta-pill + panel"** | A compact figure in the page header meta row (always visible, tab-independent) **plus** the panel for detail | Coverage is visible without selecting a tab — and on a page whose default tab is Insights only *sometimes*, that matters. Two representations of one dataset on one page is UX-DR21 pressure and must be argued, not assumed. |
| **C** | **"Shared quality panel"** | **One** `insight-panel` carrying both coverage and Epic 26's observations, under one heading | Strictest reading of 27.4 AC #2 — literally one section, impossible to drift into two. But it couples the two epics' delivery hard: whichever lands first builds a container the second must edit, and the panel has no honest empty state when only one of the two is configured. |

**Deep-link mechanics already exist** and must be reused: `#L{n}` anchors on `.code-line` spans, and a `:target`
on a source line forces the Code panel forward **in pure CSS**, so a deep link survives the default tab. Route
every file→page link through `SiteGenerator.CodeItemHref` — a null return means "no page", so a dead link is
structurally impossible.

**`FileInsight`** (`src/SpecScribe/GitMetrics.cs` — `ChangeCount`, `Contributors`, `CoupledFiles`, `History`,
`TotalContributors`) is the seam 27.3 AC #2 requires coverage to extend. `CoupledFile` is the shipped precedent
for a per-file, confidence-bearing sub-record.

**The link-out (27.4 AC #3)** is NFR12's: absent when unconfigured, never broken or placeholder. Its URL
derivation is **27.2 AC #5's**, not yours — you decide only where the link sits and what it looks like when
absent.

---

### S2 — the Code Map treemap → Story 27.5

`CodeMapTemplater` renders four precomputed variant panels (the spec-dev / tests exclusion filters are **pure
CSS over precomputed panels — no script**), sharing one **"Colorize by"** dropdown, a legend bar, a
**Hierarchy Explorer** chart, and an **"All files" text table** configured `HierarchyTwinDisplay.External` —
**that table IS the chart's text twin**. Today's dimensions (`HierarchyExplorer.CodeMapDimensions`): six git
ramps (`changes`, `last`, `created`, `avgchange`, `churn`, `cochange`) plus `filetype`, degrading to `filetype`
alone without `--deep-git`. Size is always **lines of code**; directory rollup happens where `Lines` already
rolls up (`CodeMapNode` / `CodeMap.Build`).

| | Direction | What changes | Trade-off |
|---|---|---|---|
| **A** | **"Eighth Dimension"** | One new `HierarchyDimension` of kind `Cutoff` in `CodeMapDimensions`; the "All files" table gains a Coverage column as the twin | Zero new surfaces, UX-DR21 clean, smallest change, and `Cutoff` is the right kind (Fact 2). But coverage is only visible when the reader *selects* it — opt-in inside an opt-in — and per M2 the map goes ~7/8 neutral the moment they do. |
| **B** | **"Coverage-first panel"** | A dedicated coverage variant/panel with coverage as its baked default colorize, alongside the existing four | Coverage is visible without a menu interaction, and the panel can carry its own legend copy and unconfigured notice. Multiplies the precomputed panel count (already four) and puts a second hierarchy over the same tree — the UX-DR21 pressure the rule exists to prevent. |
| **C** | **"Size by uncovered lines"** | Colour untouched; a mode where the treemap is **sized** by uncovered lines instead of total lines | The only direction that answers *"where is the testing debt"* directly and **sidesteps M1 entirely** — it encodes an absolute count, not a top-heavy percentage, so `extension.ts` (767), `SiteGenerator.cs` (323) and `Commands.cs` (321) become the three biggest rectangles on the map, which is the truth. Unknown files get zero area — honest, they contribute no *known* debt. Composes with **P1**. But it changes the map's **size key**, its single most stable convention, and the component may not support a per-dimension size swap without work 27.5 would have to scope. |

**Any hierarchy direction inherits ADR 0012** (one datasource, one selector, one framing block, one text twin)
and its Addendum: one synthesized root required; `branchvalues: 'total'` invalid because parent weight ≠ Σ
children; `null` in `values` silently renders nothing, so branch values must be `0`. **ADR 0012 § Decision 6** is
the directly reusable pattern for signal-without-colour: status is carried on **three independent channels —
fill token, hatch, and the word in the accessible name.**

`Charts.ChartMeta` / `Charts.Framed` supply the real-value legend, analysis window and framing sentence **by
construction** (FR28) — and **`ChartMetric` must gain a member with its `WhyText` case**, because Story 10.2
AC #2 forbids hand-rolled "why this matters" copy at call sites. Note the shipped precedent for an honest
unconfigured notice: the Code Map already emits `role="note"` reading *"Git change data is unavailable (run with
`--deep-git` …)"*, placed **outside** the legend deliberately — *a fact about the DATA, not chrome for a chart*.
**Coverage needs its exact analogue** and it should be written in this round, not improvised in 27.5.

---

### S3 — the sunburst / Hierarchy Explorer → Story 27.5

The Code Map's sunburst and treemap are **the same component** with a shape selector, so S2's selection largely
determines S3. What S3 adds is a distinct question: **which other hierarchy surfaces are in scope?** Git
Insights ships an ownership sunburst with its own `OwnershipDimensions`; Deep Analytics has its own surfaces.
Epic 27's AC names "the sunburst / Hierarchy Explorer" without enumerating instances.

**Ask explicitly, and close the list.** The recommendation to present: **only the Code Map's own
treemap/sunburst pair is IN**; the Git Insights ownership sunburst is **OUT** (it answers "who owns this", a
different dataset, and adding coverage there is the third meaning on one wedge that AC #3 exists to prevent).
AC #1 requires the OUT list to be named *with its reason* — this is the clearest candidate, the same role S7
played in Story 26.1.

**ADR 0013 is a hard obligation here, and 27.5 AC #2 restates it:** the server-rendered **text twin must carry
the coverage figures too** — a twin that omits the new signal silently breaks the contract that made the SVG
retirement acceptable. The twin's five properties: server-rendered, complete, navigable, non-colour, not
visually redundant, *"verified in a live browser with JavaScript disabled, not by test assertion alone."*

⚠️ **The byte-cost measurement is not optional and not negligible.** 27.5 AC #2 requires the per-node cost be
**measured** against Story 20.7's budget — Story 20.5 found the twin cost **~180 B/node** that its own spike
never modelled. Your directions differ sharply in cost: a percentage plus covered/total counts per node is
several times a single ramp level. **Say in the record which fields each direction puts on a node**, so 27.5
measures the right thing rather than discovering it.

⚠️ **ADR 0031 changed the sequencing, not the requirement.** ADR 0013's hard *per-story* gate is retired for new
work; text-twin standardization is **Epic 28's**. 27.5 AC #2 still binds. Read `docs/adrs/` before declaring any
rule is being crossed — CLAUDE.md § Decision records records Story 21.3 getting exactly this wrong.

---

### S4 — the coverage × churn surface → Story 27.6

27.6 ranks files that "change often AND are poorly tested", reusing `GitMetrics.TryComputeDeep` — **not** a
second git traversal. You own the visual direction; **27.6 owns the ranking rule** and must be able to derive it
from what you record.

| | Direction | Primary representation | Trade-off |
|---|---|---|---|
| **A** | **"Risk scatter"** | Churn on one axis, coverage (or uncovered lines) on the other; the dangerous quadrant is a corner | **A size × churn log-scaled scatter already ships** (`specscribe.css`, the Deep Analytics scatter block), so the idiom, its styling and its twin pattern are all precedent rather than new work. Scatters are hard to read at 400+ points and need a stated point-inclusion rule. |
| **B** | **"Ranked table"** | A top-N table ordered by a stated, defensible rule | Most honest against 27.6 AC #1's *"stated and defensible, not an unexplained composite score"*, cheapest, and inherently accessible. No shape at a glance. |
| **C** | **"Named quadrants"** | Scatter or grid with the four quadrants **labelled in words** ("changes often, poorly tested" / "stable and untested" / …) | Turns the composite into language rather than a score, which is exactly what AC #1 asks for, and the label is the non-colour channel by construction. Quadrant boundaries are themselves an unexplained threshold unless stated. |

⚠️ **M3 is the trap here.** Ranking naively by churn × (100 − coverage) puts `Program.cs`, `ConsoleUi.cs` and
`extension.ts` at the top — files that are 0 % *by design*. Story 26.1's equivalent finding (the test tree
reading as the worst region) changed the owner's selection. Ask directly: **does the surface need a notion of
"deliberately untested", and if so does it come from configuration, convention, or nothing at all?** If the
answer is "nothing", the record must say the ranking will surface entry points and the reader is expected to know
that — an acceptable answer, but only if it is *recorded* rather than discovered.

⚠️ **Degradation is two-sided** (27.6 AC #2): coverage without `--deep-git`, and churn without a coverage report.
Both must be absent-not-broken. Name what each half-configured case shows.

---

### S5 — dashboard signal → recommend **OUT**, but record the reason

The owner's named surfaces are the treemap, the sunburst/Hierarchy Explorer and code file pages. A dashboard
strip or tile is **not** among them. Epic 26 *did* take one (S6 = "Quality Strip").

Recommend **OUT for Epic 27**, recorded with its reason: the dashboard already carries thirteen sections and a
Quality Strip inbound from 26.6, and a coverage tile would be the portal's second quality-ish dashboard element
before the first has shipped. AC #1 requires the OUT list to be named — do not leave this one merely unmentioned.

⚠️ **If the owner overrides and wants it in:** every dashboard panel is **`wm-show-*` gated** across five workflow
modes (Story 26.1 § Completion Note 9). An undeclared panel is invisible in *every* mode. That is a shipped trap,
not a theoretical one.

## AC #1's second half — what an unconfigured project sees

**This is the default case for every user and it deserves its own section of the record, not a sentence.** The
portal has three shipped idioms; name which applies where:

| idiom | mechanism | shipped example |
|---|---|---|
| **(a) Absent** | producer returns `""`; consumer's `if (…Html.Length > 0)` drops the whole panel | `CodeFileTemplater.BuildInsightsPanel`; `EpicsViewBuilder`; `DashboardView` |
| **(b) Honest empty state** | surface must exist anyway → `<div class="chart-empty">` with a **specific** sentence | `Charts`' chart-empty; the Work Graph tab's always-present empty |
| **(c) Unconfigured-source notice** | source off but surface still renders → `role="note"` explaining how to enable, placed **outside** the legend | the Code Map's `--deep-git` notice |

The expected answer for an unconfigured project is **(a) everywhere** — no coverage dimension in the dropdown, no
panel, no column, baseline output unchanged. **Confirm it explicitly** rather than leaving it inferred, and say
which surfaces use **(b)** or **(c)** in the configured-but-empty case. Note that (c) is the *right* idiom for
the Code Map: it already uses exactly that shape for `--deep-git`, and coverage is the same class of optional
signal.

⚠️ **Do not conflate three different zero states.** (1) no report configured, (2) report configured but this file
absent from it, (3) file present with 0 % coverage. CLAUDE.md's cardinal rule — *"absent means UNKNOWN, never
clean"* — makes rendering (1) and (2) identically a lie, and 27.4 AC #4 makes distinguishing (2) from (3) a hard
requirement. **UX-DR24 is the shipped precedent** for distinguishing two superficially-identical zero states
(backlog vs ready-for-dev tooltips).

## ⚠️ A gate Story 27.3 is told to use no longer exists

Story 27.3 AC #3 requires byte-identical output when no report is configured, *"proven by the golden fingerprint
being unmoved."*

**`GoldenContentFingerprint` is retired.** ADR 0034 (Story 23.6) deleted it with its subject, the C# `.html`
writer; `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` carries only its tombstone comment, and CLAUDE.md
states it plainly.

Its nominal successor is `npm run check:parity` — but **`check:parity` structurally cannot see a C#-side
change.** Its corpus IR is frozen, so anything the C# region composer emits differently renders from the *pinned*
input and the gate stays green. CLAUDE.md records this as verified on 2026-08-01: a change that removed an
element from the shared nav on every page left all 24 routes byte-identical.

**Coverage lands on the C# side.** So 27.3's byte-identity claim has **no gate that can currently prove it**, and
a dev reading AC #3 literally will either hunt for a deleted test or run a green gate that proves nothing.

**This is a finding for the record, and it needs an owner decision** (Task 7): either 27.3 AC #3 is amended to
name a real mechanism — the honest candidates are a targeted IR-level assertion over a no-report run, or a new
gate authored under **ADR 0033**'s rules (localize failure to a named artifact, scoped so a sibling story cannot
turn it red, proven deterministic across machines and CI OSes before pinning) — or the AC is restated as a
scope-based proof, as Story 25.5 did (*"unmoved **by this story**, proven by scope, not by a hash you cannot
own"*). **If AC #3 is amended, `epics.md` AND `sprint-status.yaml` change in the same commit** (CLAUDE.md
§ Decision records).

## The wider UX-DR set (Epic 27's declared three are the floor)

Beyond UX-DR17 / UX-DR21 / UX-DR22:

- **UX-DR19** — *"a non-color text equivalent of every metric"*, promoted to contract by ADR 0013 § 2. This is
  the treemap's own requirement and coverage inherits it.
- **UX-DR23** — paired counts restated as a sentence. **"1,240 / 1,371 lines covered" needs its sentence form**,
  not just two numbers — and 27.4 AC #1's "auditable" counts are exactly a paired count.
- **UX-DR24** — distinguishing two superficially-identical zero states. This *is* the unknown-vs-0 % problem.
- **UX-DR25** — one portal-wide date token. Relevant if 27.2 decides report staleness is disclosed.
- **UX-DR27** — `file:line` references render as styled chips, never raw syntax in prose.
- **FR20** — every rendered badge routes through the `--status-*` token system with a reachable status legend. A
  coverage chip **must not fork the shipped status-pill idiom** — and per Fact 1 it must not join the stage
  tokens either. Say how it does both.
- **FR28** — every chart carries a legend with real values, its analysis window, and one framing sentence.
- **FR21** — counts route through the single generator-side count source, never a new tally.

**ADR 0026 § Decision 2 is a trap worth naming:** where a class is drawn from a closed domain, **seed the domain,
not the observed subset.** A generated style layer emitting only the coverage levels today's report happens to
produce would silently ship unstyled buckets for a project whose report contains them. Coverage levels are a
closed set once cut points are fixed — **say so**, so 27.5 does not learn it the hard way.

## Tasks / Subtasks

- [ ] **Task 1 — Refresh the measurements before quoting any of them** (AC: #1, #2)
  - [ ] Re-run the SonarCloud query (public, anonymous, no token):
        `curl -s "https://sonarcloud.io/api/measures/component_tree?component=IntegerMan_SpecScribe&metricKeys=line_coverage,uncovered_lines,lines_to_cover&qualifiers=FIL&ps=500"`
  - [ ] Recompute M1 (band distribution), M2 (unknown ratio vs `git ls-files`), M3 (the 0 % list and the
        largest-uncovered list), M4 (line-weighted vs mean-of-% per directory), M5 (repo totals).
  - [ ] Record the **revision** measured at and today's `git rev-parse --short HEAD`. Cite `line_coverage`, never
        the blended `coverage` — Story 25.5 § 2.
  - [ ] *Optional, only if a direction turns on data Sonar does not carry:* `pwsh tools/coverage/Get-Coverage.ps1`.
        ⚠️ It runs the full suite. The local suite is **flaky** (a 3 s git-subprocess timeout against a measured
        6,496 ms cold read; 25.1 saw 9/3/1/18 failures across four identical runs), a **red run still emits a
        lower, plausible-looking report**, and a concurrent session's test host will make it fail outright. Prefer
        the API.
- [ ] **Task 2 — Re-verify every attach point by symbol, not by line number** (AC: #1)
  - [ ] CLAUDE.md § Concurrent work: another session may have moved anything. Every symbol here was verified at
        `c73ebcb`; grep before quoting.
  - [ ] Confirm the six-entry vocabulary-collision table still holds, especially
        `TestArtifactsTemplater`'s `<h2>Test coverage</h2>` and the **duplicate `.coverage-card`** definition.
  - [ ] Confirm `HierarchyDimensionKind.Cutoff` and the ownership `Cutoffs: new[] { 25, 50, 75 }` precedent.
  - [ ] Re-check Epic 26's shipped state (`sprint-status.yaml`): **which epic will land second is a live input to
        S1** and may have changed since authoring.
- [ ] **Task 3 — Prepare the elicitation, one surface at a time** (AC: #1)
  - [ ] For each of S1–S5, restate the 2–3 named directions **with refreshed numbers substituted**, so the owner
        chooses against real density.
  - [ ] Have the AC #2 material (§ *the scale question*) ready as a **separate, first** decision — S2/S3 cannot be
        chosen coherently before polarity and cut points are settled.
- [ ] **Task 4 — Settle AC #2 explicitly, before the surface round** (AC: #2)
  - [ ] **Scale family:** confirm coverage is off the `--status-*` stage tokens, and record *why* (Fact 1).
  - [ ] **Scale kind:** `Cutoff` (fixed, repo-independent) vs anything data-relative. Record the reasoning, not
        just the pick.
  - [ ] **Cut points:** a concrete ascending set, chosen against M1's top-heavy distribution.
  - [ ] **Polarity:** P1 (encode the deficit, reuse the gold ramp) vs P2 (new family, "more is better"). This
        decides whether a new token family is needed at all.
  - [ ] **Unknown:** the three channels (neutral fill · dashed stroke · "no data" in the accessible name) and,
        given M2, whether the dash applies on the map or only in the twin/tooltip — or whether unknown nodes are
        filterable.
  - [ ] Record how a **0 % file** is unambiguously distinct from an **unknown** one, in both the chart and the twin.
- [ ] **Task 5 — Run the owner round and record selections** (AC: #1, #3)
  - [ ] **Mechanic:** one surface at a time, S1 → S5, each a discrete choice with its named trade-off — not five
        questions at once. S1 and S2 are load-bearing; S5 can be settled by recommendation-plus-confirmation.
  - [ ] Per surface capture: **selection · placement · density · unknown/empty states · coverage-without-colour ·
        reuse-vs-new-page (UX-DR21) · in/out for 27.4–27.6.**
  - [ ] Capture the **reasoning**, not just the pick — 27.3/27.4/27.5/27.6 must derive detail decisions without
        re-asking.
  - [ ] An owner-invented direction **supersedes** the menu; record it as such.
  - [ ] **AC #3 specifically:** for each hierarchy surface, record what coverage **replaces or coexists with**.
        The Code Map's colorize dropdown means coexistence is the default mechanism — but *size* is a separate
        channel and direction C touches it. Say which channels coverage occupies and which it leaves alone.
  - [ ] Record the **per-node fields** each selected direction puts in the text twin, so 27.5 can measure the byte
        cost against Story 20.7's budget rather than discover it.
- [ ] **Task 6 — Answer the default case explicitly** (AC: #1)
  - [ ] A dedicated section stating what an **unconfigured** project sees on every surface, mapped to idioms
        (a)/(b)/(c), and confirming baseline output is unchanged and no test run is ever invoked.
  - [ ] State the three distinct zero states and how each renders.
- [ ] **Task 7 — Write `27-1-ideation-record.md` and close the scope** (AC: #1, #2, #3)
  - [ ] Deliverable at `_bmad-output/implementation-artifacts/27-1-ideation-record.md`, following the
        `26-1-ideation-record.md` shape: executive summary → measurements → AC #2 decisions → per-surface
        decisions → the default case → handoffs.
  - [ ] A **handoff section per downstream story** (27.2, 27.3, 27.4, 27.5, 27.6) saying exactly what it inherits
        — the mechanism that made the 25.3 and 26.1 reports usable.
  - [ ] Hand **27.2** the full six-entry vocabulary-collision table, and state that the noun is 27.2's to fix and
        that this record deliberately used a placeholder.
  - [ ] Hand **27.3** two things: M4 (line-weighting will **not** demonstrate itself on this repo — use a
        synthetic fixture) and § *A gate Story 27.3 is told to use no longer exists*.
  - [ ] Hand **27.5** the per-node twin field list and the ADR 0012/0013 obligations.
  - [ ] Hand **27.6** the M3 "0 % by design" problem and the owner's answer to it.
  - [ ] A **closed IN / OUT list**. AC #1 is not met by an IN list alone. If a candidate is deferred rather than
        closed (as S7 was in 26.1), say so plainly in the record rather than implying closure.
  - [ ] **If a selection changes 27.3–27.6's scope, amend `epics.md` AND `sprint-status.yaml` in the same change**
        — a change recorded in only one artifact is a drift bug (CLAUDE.md § Decision records). The 27.3 AC #3
        fingerprint problem is the most likely amendment.
- [ ] **Task 8 — ADR trigger check** (AC: #1, #2)
  - [ ] **Read `docs/adrs/` first.** Story 21.3 declared it was crossing a project rule that a two-day-old ADR
        already permitted. A ratified ADR outranks project memory.
  - [ ] A visual-direction record normally does **not** trigger an ADR — **Epic 27's ADR is 27.2's by design.**
        But propose one if a selection would *amend* a prior record: ADR 0012 (a new hierarchy obligation or a
        change to the size key — **direction S2-C would**), ADR 0013 / 27.5 AC #2 (a new text-twin obligation),
        or ADR 0026 (closed-domain seeding for the coverage levels).
  - [ ] Record the conditions that *would* fire one, so a later story cannot miss them.
- [ ] **Task 9 — Verify the no-code contract** (AC: all)
  - [ ] `git status --porcelain -- src/ tests/ web/ extension/` is empty **for this story's changes**. Expect a
        dirty tree from concurrent sessions — prove it by **attribution** (`git status --porcelain --
        _bmad-output/`), never by a clean status, and **never** by `git reset --hard`, `git checkout --`, or
        `git clean`.
  - [ ] No generation run is required. If you ran one for any reason, say so and say what you measured.
  - [ ] Update `sprint-status.yaml` for `27-1-…` and add a `## Change Log` entry.

## Dev Notes

### Working conditions (CLAUDE.md, non-negotiable)

- **Another agent may be editing the same files right now.** Verify after every edit; grep for a symbol before
  relying on it. **Never** `git reset --hard`, `git checkout --`, or `git clean` — this has destroyed real work here.
- Treat every cited line number as approximate and **confirm by symbol**.
- This story needs no build and no generate, so the non-incremental-rebuild and two-generates rules do not apply —
  but if you deviate and run a generate, read CLAUDE.md § *Changing `specscribe.css`?* first.

### Citation discipline

**Cite ADRs by symbol/section, never by line number** — ADR 0015's refs drifted within one day. Story files
survive via `baseline_commit`; ADRs do not.

⚠️ **Two requirement-numbering hazards.** (1) FR42, FR20, FR21, FR27, FR28, NFR12, NFR8 live in **`epics.md`**
§ Functional / NonFunctional Requirements — **not** in `prd.md`, which uses a separate, independently numbered
`FR-n`/`NFR-n` scheme. (2) UX-DR17/19/21–25/27 also live in **`epics.md`** § UX Design Requirements; the
`ux-designs/` folder contains **zero** occurrences of the string `UX-DR`. Read `epics.md` for both.

### Reading the analysis digest, if you touch it

`.specscribe/analysis/` is gitignored and refreshed by hand (`node tools/analysis-digest/index.mjs`). Go straight
to the shard for a file you care about; `index.json` is the repo-wide view only; reading everything costs 1.34 MB.
**Absent means UNKNOWN, never clean.** This story does not need it — the coverage data comes from the Sonar
measures API, a different endpoint.

### Project Structure Notes

- The deliverable lands in `_bmad-output/implementation-artifacts/`, alongside `26-1-ideation-record.md`. This
  story writes **no** file under `src/`, `tests/`, `web/`, `extension/`, or `docs/adrs/` (unless Task 8 fires).
- **Story 27.2 (spike) is next** and consumes this record's selections as the surfaces its ingestion posture must
  feed. Do not start 27.2's work here — in particular, do not settle formats, discovery, path mapping, staleness,
  the link-out URL derivation, or the vocabulary.

### Testing standards

No tests. `ships_product_code: false`. Verification is Task 9: attribution-based proof that no product directory
changed, and no generation run.

## Previous Story Intelligence

There is no Story 27.0. The load-bearing predecessors are in Epics 25 and 26.

**From Story 26.1 (ideation, `review`) — the direct pattern to copy.** Read
`26-1-ideation-where-findings-belong-in-the-portal.md` *and* `26-1-ideation-record.md` before starting. What
transfers:

- **The facilitator stance and the menu-as-selection-exercise mechanic** worked; the owner invented one direction
  outright (the three-page hub), which superseded the menu. Expect and welcome that.
- **Measurement changed three decisions.** Its § Completion Note 5 is the model: the raw-count default in its own
  story file was *wrong* once measured, because the test tree held 39 % of observations and one error. **M1/M2/M3
  here are the equivalent, and they likewise contradict the naive reading.**
- **Its S1 = B (Third Insight Panel)** is the Epic 26 side of the code-page coordination. Its record explicitly
  noted compatibility with 27.4 AC #2.
- **Two structural traps it found** that apply directly: code-page tabs are assembled at **two** call sites, and
  **every dashboard panel is `wm-show-*` gated** across five workflow modes.
- **It amended `epics.md` and `sprint-status.yaml` in the same change** for six structural amendments — the
  pattern Task 7 requires.
- **Its Task 8 literal check did not pass**, and correctly so: concurrent sessions had ~20 uncommitted files under
  `src/`/`tests/`/`web/`. It proved the no-code contract by **attribution**. Do the same.

**From Story 25.5 (local coverage report, `review`)** — the coverage-domain predecessor, and it inverted two
things its own upstream stories had written down:

- **Sonar's `coverage` ≠ line coverage.** Blended lines + branches. Compare `line_coverage` to line coverage and
  `branch_coverage` to branch coverage, scoped to a directory. A comparison across formulas "discovers" a
  discrepancy that is arithmetic.
- **The collection path** is `coverlet.collector` 6.0.4 → `coverage.opencover.xml`, already emitted by
  `dotnet test`; ReportGenerator 5.5.11 (pinned in `.config/dotnet-tools.json`) only renders it. **OpenCover was
  chosen because SonarScanner for .NET reads `sonar.cs.opencover.reportsPaths` and does not document Cobertura
  support for C#.** ⚠️ **This is a live input to Story 27.2 AC #1**, whose epics.md note calls *Cobertura* the
  cross-ecosystem default. Both are true and they are in tension — carry it into the 27.2 handoff.
- **`web/` has a separate collector** (`@vitest/coverage-v8` → lcov, gitignored `web/coverage/`). ReportGenerator
  *can* ingest both; merging was **declined and priced**, not overlooked. A multi-format, multi-stack repo is the
  normal case, not an exotic one — 27.2's format decision should know this repo already needs two.
- **Stale `TestResults/<guid>/` dirs silently merge into a wrong, plausible number**, which is why the local
  script deletes its raw directory every run.

**From Story 21.1 (traceability, done)** — two live-browser defects the test suite structurally could not see: a
CSS containment leak causing ~2,031 px of phantom scroll, and a same-specificity cascade tie lost by source order.
CLAUDE.md § Verification exists because of these. **Whatever is chosen here must be verified in a live browser by
27.4/27.5, not by test assertion** — this round should say so in each handoff.

## Git Intelligence

`HEAD` = `c73ebcb` (`Merge branch 'worktree-story-16-2-dev'`), tree clean at authoring time. Recent history is
merge commits from per-story worktrees (`worktree-story-16-2-dev`, `worktree-story-17-1-create`,
`worktree-story-23-2-close-ir-content`, `worktree-story-16-3-create`), i.e. **several stories in flight in
parallel branches**.

Consequences:

- **Scope any later review of this story by its own File List and declared symbols, never by a commit range**
  (CLAUDE.md § Scoping a code review). Where a file appears in more than one in-flight story's File List,
  attribute **by hunk**.
- **Worktrees are in active use** here, which contradicts CLAUDE.md § Concurrent work's older claim that isolation
  is unavailable. Recent stories were created and developed in `.claude/worktrees/`. This story needs none of the
  worktree recipe (it runs no `generate`, no build, and no gate), but if you deviate: the renderer-path defect
  that used to require `SPECSCRIBE_RENDERER_DIR` was **fixed by Story 16.3** (`NuxtPrerender.IsRepoRoot` now
  accepts `.git` as a file as well as a directory) — **do not apply that old workaround.** Its two companion
  preconditions are unchanged and still required: `npm ci` in `web/` must run with `SPECSCRIBE_PACKAGE_BUILD=1`,
  and `generate` must carry `--deep-git` before `extract:ir-content`. `web/CONVENTIONS.md` § 10 is the authority.
  Separately, `check:ir-content` is red in a *fresh* worktree because with no IR nearly everything is pruned —
  environmental; **never regenerate that baseline.**
- **Epic 23 is still landing** (23.6 `in-progress`), so the rendering path is mid-migration: ADR 0034 makes the IR
  the product and the site rendered from it, and the C# `.html` writer is being retired. **This is why the S1–S3
  attach points are described as C# region composers rather than page writers**, and it is why 27.4/27.5 must
  confirm where their markup actually lands before building. Say this in the handoffs.

## References

- Story ACs and epic framing — `_bmad-output/planning-artifacts/epics.md` §§ *Epic 27*, *Story 27.1*–*27.6*, and
  the epic's authoring comment (naming collision, Epic 26 coordination, scope discipline)
- The sibling ideation round — `_bmad-output/implementation-artifacts/26-1-ideation-where-findings-belong-in-the-portal.md`
  and `26-1-ideation-record.md` (esp. § S1, § Completion Notes 5 and 9, § 11 amendments)
- Coverage-domain predecessor, the Sonar-formula reconciliation, and the collection path —
  `_bmad-output/implementation-artifacts/25-5-local-coverage-report.md`; `tools/coverage/README.md`
- Hierarchy component contract — `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`
  §§ Decision 2, 3, 6, 7, Addendum
- Text-twin contract and the JS-off gate — `docs/adrs/0013-text-twin-is-the-no-js-contract.md` §§ Decision 1, 2, 3;
  sequencing amended by `docs/adrs/0031-text-twin-standardization-moves-to-its-own-epic.md`
- Closed-domain seeding for generated layers — `docs/adrs/0026-generated-layers-derive-from-templates-not-project-data.md` § Decision 2
- Gate design rules (any new gate 27.3 might need) — `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`
- The IR is the product; `GoldenContentFingerprint` retired — `docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md` § Decision, § Consequences
- Requirements and UX-DRs — `_bmad-output/planning-artifacts/epics.md` §§ Functional Requirements (FR20, FR21,
  FR27, FR28, FR42), NonFunctional Requirements (NFR8, NFR12), UX Design Requirements (UX-DR17, 19, 21, 22, 23,
  24, 25, 27)
- AD-4 — `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- Project working conventions — `CLAUDE.md` §§ Concurrent work, Which gate is which, Story lifecycle,
  Scoping a code review, Decision records, Analysis observations, Verification
- Attach points (verified at `c73ebcb`, by symbol) — `src/SpecScribe/CodeFileTemplater.cs`
  (`CodeTab`, `BuildInsightsPanel`, `AppendTabs`, the one-tab branch); `src/SpecScribe/GitMetrics.cs`
  (`FileInsight`, `CoupledFile`); `src/SpecScribe/CodeMapTemplater.cs` (`AppendColorizeControls`,
  `AppendFileTable`, the `--deep-git` `role="note"`); `src/SpecScribe/CodeMap.cs` (`CodeMapNode`, `Build`,
  `IsSpecDevPath`, `IsTestPath`); `src/SpecScribe/HierarchyExplorer.cs` (`HierarchyDimension`,
  `HierarchyDimensionKind`, `HierarchyTwinDisplay`); `src/SpecScribe/HierarchyExplorer.Projectors.cs`
  (`CodeMapDimensions`, `OwnershipDimensions`, `RampText`); `src/SpecScribe/Charts.cs` (`ChartMetric`,
  `ChartMeta`, `Framed`, the artifact-coverage card builder); `src/SpecScribe/ArtifactCoverage.cs`;
  `src/SpecScribe/TestArtifactsTemplater.cs` (`ta-coverage`); `src/SpecScribe/RequirementsTemplater.cs`;
  `src/SpecScribe/TraceabilityTemplater.cs`; `src/SpecScribe/DashboardView.cs` (`Coverage`);
  `src/SpecScribe/SiteGenerator.cs` (`RefreshCoverage`, `CodeItemHref`); `src/SpecScribe/assets/specscribe.css`
  (the `--status-*` block, the Code Map ramp block, the file-type palette, the `.coverage-*` namespace, the
  size × churn scatter block)
- Measurements — SonarCloud `api/measures/component_tree` for `IntegerMan_SpecScribe`, 2026-08-07 (public,
  anonymous); `git ls-files` at `c73ebcb`

## Open Questions Raised at Create-Story (non-blocking — fold into the owner round)

These surfaced during analysis and have no answer in any existing artifact. Each has a workable default, but each
changes a downstream story if answered differently.

1. **Polarity — the biggest one.** Does coverage colour encode the **deficit** (P1: reuse the gold ramp, "more
   colour = more attention", nothing about the portal's reading changes) or the **virtue** (P2: a new family,
   the portal's first "more colour is good" scale)? *Default if unanswered:* **P1**, and record P2's cost.
2. **Cut points against a top-heavy distribution.** `25/50/75` reuses ownership's shipped set but puts 83 % of
   this repo's files in one bucket (M1). *Default if unanswered:* reuse `25/50/75` for consistency and record the
   bunching as a known, accepted cost — but this is worth one real minute of the owner's time.
3. **Unknown density (M2).** With ~7 nodes in 8 unknown, does the dashed-stroke non-colour channel apply on the
   map, or only in the twin and tooltip — or are unknown nodes filterable out of the coverage view? *Default:*
   neutral fill on the map, dash and "no data" wording in the twin/tooltip, no filter.
4. **"Deliberately untested" (M3).** Does any surface need a notion of files that are 0 % *by design*
   (`Program.cs`, `ConsoleUi.cs`, `extension.ts`)? If not, the ranking in 27.6 will name the CLI entry point the
   riskiest file in SpecScribe. *Default:* no such notion; record that the ranking surfaces entry points and the
   reader is expected to know it.
5. **Treemap size key (S2 direction C).** Is changing what the treemap is *sized* by acceptable at all, or is
   "lines of code" an invariant of that surface? This determines whether direction C is even on the table, and
   whether Task 8's ADR 0012 trigger fires. *Default:* colour-only; size stays lines.
6. **Which hierarchy instances are in scope (S3).** Code Map treemap + sunburst only, or also the Git Insights
   ownership sunburst? *Default:* Code Map only; ownership sunburst explicitly OUT with its reason.
7. **Does this record survive Story 27.2's format decision?** The directions were written to be
   format-independent, and 25.5 shows this repo already needs **two** formats (OpenCover for C#, lcov for `web/`).
   The record should say so explicitly, so 27.2 is not blocked on re-running this round.
8. **Story 27.3 AC #3's retired gate.** Amend the AC now (in `epics.md` **and** `sprint-status.yaml`), or leave it
   for 27.3 to resolve? *Recommendation:* amend now — 27.1 is the story that found it, and leaving a known-dead
   gate in an AC is how a dev loses an afternoon.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-08-07: Story created. Context assembled from Epic 27, the Story 26.1 ideation story and record (the direct
  sibling pattern), Story 25.5's coverage-domain findings, and a verified attach-point inventory across the code
  page, Code Map, Hierarchy Explorer, and analytics surfaces. **Real coverage density measured** from SonarCloud's
  public API at 2026-08-07 (162 measured files of 1,352 tracked; 83 % at ≥ 90 %; unknown is the majority state on
  the Code Map) so the owner's round is a selection exercise against real data. Three findings not present in any
  upstream artifact are recorded: the vocabulary collision is **six** symbols and includes a shipped
  `<h2>Test coverage</h2>` meaning something unrelated; the shipped sequential ramp has the **opposite polarity**
  to coverage, with a clean resolution (encode the deficit); and Story 27.3 AC #3 cites the **retired**
  `GoldenContentFingerprint` whose successor structurally cannot see a C#-side change. Status → ready-for-dev.
</content>
