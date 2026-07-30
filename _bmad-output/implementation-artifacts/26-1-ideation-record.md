# Story 26.1 — Ideation Record: Where Analysis Observations Belong in the Portal

**Status:** complete · **Date:** 2026-07-29 · **Facilitated round; every selection below is the owner's.**
**Story baseline:** `8a2fb83` · **Session HEAD:** `630ae25` · **Analysis revision measured:** `630ae25`
**Durable deliverable:** this record. It is the input to Stories 26.2–26.7; each has a handoff section in § 8.
**Contract consumed:** [ADR 0023](../../docs/adrs/0023-agent-facing-analysis-observation-contract.md) (Accepted) — not redefined here.

> **Ships no production code.** No `src/`, `tests/`, `web/`, or `extension/` edits. `GoldenContentFingerprint`
> unmoved — see § 10.

---

## 0. Executive summary

Seven candidate surfaces were put to the owner as named directions. Six are **IN**, one (**S4**, requirement pages)
is **explicitly OUT**, and one (**S7**, the traceability matrix) is **deferred to Story 26.6 with the OUT
recommendation standing** — the owner's call, and the one place AC #1's scope is not fully closed (§ 7).

The load-bearing selections:

| Surface | Selection | Owner's direction |
|---|---|---|
| **S1** code file page | **B — Third Insight Panel** | An `insight-panel` inside the existing Insights grid, sibling to churn and contributors |
| **S2** code map / directory | **A — Seventh Dimension**, with **two selectable weightings** | Observation density as a Hierarchy Explorer dimension; the "All files" table is the twin |
| **S3** epic + story pages | **B — A Chip Per Row**, with **a rollup sentence** | Count chips inside Code Areas Touched; the rollup carries the approximateness caveat |
| **S4** requirement pages | **A — Explicitly out** | ADR 0023 § Decision 5 refused the requirement edge; the record honors it |
| **S5** the hub | **Owner-invented: hub landing + two child pages** | Supersedes the menu. Landing page teases the most actionable; leaderboard and inbox are children |
| **S6** dashboard | **A — Quality Strip** | 21.1/21.2 strip parity, with a UX-DR23 sentence form |
| **S7** traceability matrix | **Deferred to 26.6** | Recommendation OUT stands; 26.6 decides |

The three decisions upstream records assigned to this story by name are settled:

- **`relatedLocations` cap = 5**, uniform across surfaces, with a mandatory explicit truncation count (§ 5.1).
- **Fan-out presentation = total-count chip + a rollup sentence** — the direction 26.5 derives its bounding rule
  from (§ 5.2).
- **Severity collapse = none. Four levels on every surface, and the `BLOCKER` is not surfaced** (§ 5.3).

Two further site-wide decisions:

- **The noun is "Analysis Observations".** The existing story-page `Review Findings` section is **re-parented now**
  under one **Quality** parent heading, with Analysis Observations as its sibling subsection (§ 6). This moves
  shipped markup and is new work for 26.5.
- **AC #3: non-Sonar source classes are IN SCOPE AS-IS, with no engine distinction rendered** (§ 9). This is not a
  deferral — 56 % of the current payload is *already* non-Sonar (`external_roslyn`), which the story file did not
  know when it was written.

**AC #2's default case (§ 7.2): idiom (a) — absent — everywhere.** An unconfigured project gets no nav entry, no
dashboard strip, no code-page panel, no chips, byte-identical baseline output, and no network call.

---

## 1. What was measured, and at which revision

Digest regenerated with `node tools/analysis-digest/index.mjs` on 2026-07-29. Per CLAUDE.md § Analysis
observations, the read-time rule was applied: `git rev-parse HEAD` = `630ae25` = `provenance.evaluatedAtRevision`,
`commitsBehind: 0`. `isStale: true` with `staleReasons: ["working-tree-dirty"]` — the expected steady state here
(CLAUDE.md § Concurrent work), so cited line numbers below are approximate and every symbol was re-verified by
name (§ 2).

### 1.1 Density — refreshed against the story's authoring-time figures

| Measure | Story quoted (`755bd7a`) | **Live (`630ae25`)** |
|---|---|---|
| Unresolved observations | 1,488 | **1,534** |
| `error` / `warning` / `note` / `none` | 120 / 979 / 389 / 0 | **125 / 1,013 / 396 / 0** |
| Distinct rules | 86 | **86** |
| Files with observations | 201 | **208** |
| Unlocated | 0 | **0** |
| Largest single file | `SiteGenerator.cs`, 88 | **`SiteGenerator.cs`, 88** (shard 100,721 B) |
| `relatedLocations` max | 52 | **52** — `csharpsquid:S3776` in `Charts.cs` |
| Carry any secondaries | 15.5 % | **15.4 % — 237 of 1,534** |
| Digest size | 1.34 MB | **1.69 MB** (index 32,442 B · median shard 4,246 B) |

The story's warning that these numbers move is confirmed a third time: 25.3 recorded 121/960/385, 25.4 recorded
120/979/389, this round records 125/1,013/396. **Every surface must derive its counts at generation time; no
figure in this record may be hard-coded into a template.**

### 1.2 Three measurements that changed the round

**(a) The `BLOCKER` still exists, and it is one missing test assertion.** `csharpsquid:S2699` at
`tests/SpecScribe.Tests/ChartsTests.cs:338` — *"Add at least one assertion to this test case."* Its
`severity.provider[]` carries `{axis: "mqr", softwareQuality: "MAINTAINABILITY", severity: "BLOCKER"}` and
`{axis: "legacy", severity: "BLOCKER", type: "CODE_SMELL"}`; it normalizes to `error` / label `Error`. Surfacing
`BLOCKER` as its own tier would add Sonar-specific value coupling to a deliberately source-agnostic model in order
to promote one missing test assertion above 124 other errors. **This is why the owner declined it** (§ 5.3).

**(b) The test tree is 39 % of all observations and contains one error.** Directory rollup:

| Observations | error / warning / note | Files | Directory |
|---:|---|---:|---|
| 869 | 113 / 386 / 370 | 96 | `src/SpecScribe` |
| **599** | **1 / 598 / 0** | 94 | `tests/SpecScribe.Tests` |
| 36 | 9 / 12 / 15 | 11 | `web/scripts` |
| 13 | 2 / 2 / 9 | 1 | `extension/src` |
| 9 | 0 / 7 / 2 | 1 | `src/SpecScribe/assets` |
| 7 | 0 / 7 / 0 | 4 | `web/pages`, `web/components`, `web/ir` |

A **raw-count** density visual therefore points at the test tree as the worst region in the repository, which is
false in every sense a reader would mean it. This measurement is the direct cause of the owner choosing **two
selectable weightings** for S2 rather than the story's stated raw-count default (§ 3.2).

**(c) 56 % of the payload is already non-Sonar.** Rule IDs are engine-prefixed, and the split is:

| Engine | Observations | error / warning / note | Where |
|---|---:|---|---|
| `external_roslyn` | **859** | 0 / 859 / 0 | `src` 263, `tests` 596 |
| `csharpsquid` | 609 | 114 / 125 / 370 | `src` 606, `tests` 3 |
| `javascript` | 34 | 7 / 12 / 15 | `web` |
| `typescript` | 18 | 2 / 7 / 9 | `extension` 13, `web` 5 |
| `css` | 10 | 0 / 8 / 2 | `src/SpecScribe/assets` 9, `web` 1 |
| `jssecurity` | 2 | 2 / 0 / 0 | `web` |
| `Web` | 2 | 0 / 2 / 0 | `web` |

Top rules: `external_roslyn:CA1861` ×355, `external_roslyn:SYSLIB1045` ×283, `csharpsquid:S6444` ×160,
`csharpsquid:S1192` ×106, `csharpsquid:S3776` ×93, `external_roslyn:CA1859` ×76, `external_roslyn:CA1816` ×48.
**Roslyn analyzer output already flows through SonarCloud as an external engine**, and it is 100 % .NET-specific.
This reframes AC #3 entirely (§ 9).

### 1.3 `severity.provider[]` is richer than the story described — and it is what the hub sorts on

It is **not** a flat list of severity strings. It is an array of axis records, and two axes appear:

```
severity: {
  normalized: "note",  label: "Note",
  provider: [ { axis: "mqr",    softwareQuality: "MAINTAINABILITY", severity: "LOW" },
              { axis: "legacy", severity: "MINOR", type: "CODE_SMELL" } ] }
```

Distributions across 1,534 observations:

- `mqr.softwareQuality` — **MAINTAINABILITY 1,347 · SECURITY 164 · RELIABILITY 37** (14 observations carry more
  than one quality, which is why these sum above 1,534).
- `legacy.type` — **CODE_SMELL 1,358 · VULNERABILITY 164 · BUG 12**.
- The 164 SECURITY observations span 48 files and are dominated by a single rule: `csharpsquid:S6444` ×160
  (the remaining four are `csharpsquid:S4036`, `javascript:S4036`, `jssecurity:S8705`, `jssecurity:S8707`).

**This is the axis the hub's "type" sort reads** (§ 3.5). `helpUri` is present on all 1,534 — the 25.4 note that
`api/rules/show` has no `helpUri` field and the emitter synthesizes an organization permalink holds, and it means a
"learn more" link is safe to render on any surface.

### 1.4 `relatedLocations` distribution — the evidence behind the cap

1,297 of 1,534 observations carry **zero** secondaries. Of the 237 that carry any:

| Cap | Issues shown complete | Issues carrying a truncation notice |
|---:|---:|---:|
| 3 | 68 | 169 |
| **5** | **101** | **136** |
| 8 | 131 | 106 |
| 10 | 151 | 86 |
| 20 | 221 | 16 |

Long tail: single issues at 25, 27, 29, 30, 32, and one at **52**.

---

## 2. Attach points — re-verified by symbol at `630ae25`

Every attach point the story cited still exists. **Several line numbers have drifted**, and two structural facts
the story did not record change what 26.4/26.5 must do. Downstream stories should cite the symbol, not these
numbers (memory `cite-adrs-by-symbol-not-line-number`).

| Symbol | Story cited | **Verified at `630ae25`** |
|---|---|---|
| `CodeFileTemplater.BuildInsightsPanel` | `:215-244` | **`:197`** |
| `CodeFileTemplater` one-tab bail | `:110-116` | `:110-116` (unchanged) |
| `CodeFileTemplater` `.code-line` / `#L{n}` | `:153` | `BuildSource` at **`:138`**, lines emitted below it |
| `CodeMapTemplater.AppendFileTable` | `:313` | **`:306`** |
| `HierarchyExplorer.CodeMapDimensions` | `CodeMapTemplater.cs:201` | **`HierarchyExplorer.Projectors.cs:791`** — different file |
| `HierarchyTwinDisplay.External` on the code map | `:197-199` | **`CodeMapTemplater.cs:186-187`** |
| `SiteGenerator.CodeItemHref` | `:1849` | **`:2144`** |
| `EpicsViewBuilder.RenderCodeAreas` | `:384` | **`:387-388`**, two call sites (`:177` epic, `:322` story) |
| `Review Findings` section | `HtmlRenderAdapter.Epics.cs:614` | `:614` (unchanged), `id="sec-review-findings"` |
| `Code Areas Touched` TOC adds | `:252-256` / `:632-636` | `:252-256` (epic) / `:632-636` (story) — unchanged |
| `FileInsight` / `CoupledFile` | `GitMetrics.cs:183` / `:213` | unchanged |
| `ImpactFile` / `PlanningCodeImpactData` | `PlanningCodeImpact.cs:11` / `:21` | unchanged |
| `Charts.ChartMetric` / `ChartMeta` / `Framed` / `PlanningCodeImpactNote` | `:13` / `:47` / `:165` / `:84` | unchanged |
| `SiteNav.Build` `has…` flags | `:340-368` | `bool has…` params at **`:204-214`**, gates at `:315`, `:342`, `:347`, `:352` |

### 2.1 Two structural facts the story did not record

**(a) The code page assembles its tab list at TWO call sites, not one.** `CodeFileTemplater.cs:104-108` and again
at **`:786-790`**, each building its own `List<CodeTab>` in the same fixed order. S1 = B sidesteps this — a third
insight panel changes `BuildInsightsPanel` once and both call sites inherit it. **This is an additional argument for
the direction the owner picked**, and it is a trap for anyone who later revisits S1 = A: a fifth tab must be added
in both places or the code page and its sibling surface diverge silently.

**(b) Every dashboard panel is workflow-mode-gated.** Panels carry `wm-panel` plus a `wm-show-*` set, and
`specscribe.css:7041-7045` hides any `.wm-panel` lacking the checked mode's class. The five modes are **overview /
requirements / plan / develop / review**. S6's Quality Strip **must declare its mode set** or it will be invisible
in every mode. Recommendation to 26.6, for its own confirmation: `wm-show-develop wm-show-review` — matching
Delivery Cadence (`develop`, `track`) and the review-oriented reading of quality state. This is a detail decision,
not one settled here.

**(c) `Review Findings` exists on story pages only.** `view.ReviewFindingsHtml` is rendered in the story branch
(`HtmlRenderAdapter.Epics.cs:613-618`); the epic branch has no equivalent. The § 6 re-parenting therefore touches
story pages only, which is what the owner's selection assumed.

**(d) `Charts.ChartMetric` currently has nine members** (`ActivityCadence`, `FileChurn`, `ChangeCoupling`,
`RefactorRisk`, `CodeOwnership`, `RequirementTraceability`, `DeliveryCadence`, `PlanningCodeImpact`,
`WorkHierarchy`). S2 = A **requires a tenth**, with its `WhyText` case — Story 10.2 AC #2 forbids hand-rolled
"why this matters" copy at call sites.

---

## 3. Per-surface decisions

Each subsection records: **selection · placement · density · all four empty states · severity without color ·
reuse-vs-new-page (UX-DR21) · in/out for 26.4–26.6 · the reasoning.**

The four empty states, per ADR 0023 plus the portal's default:

1. **Not configured** — no `.specscribe/analysis/`, integration disabled. **The default for every user.**
2. **Configured, genuinely clean** — a real zero.
3. **Configured, observations exist but attach to no planning entity** (`basis: "none"`).
4. **Attachment never computed** (`basis: "unavailable"` — `--deep-git` off, which is **100 % of the current
   digest**, owner decision D5 on 25.4).

CLAUDE.md's cardinal rule governs all of them: **absent means UNKNOWN, never clean.** A surface rendering 1 and 2
identically is lying. UX-DR24 is the shipped precedent for distinguishing two superficially-identical zeroes.

### 3.1 S1 — Code file page → **IN, Story 26.4**

**Selection: B — "Third Insight Panel."**

- **Placement.** A new `insight-panel` inside the existing Insights grid built by
  `CodeFileTemplater.BuildInsightsPanel`, sibling to churn and contributors. **No new tab.**
- **Density.** The grid is cramped relative to 88 rows on the worst file, so the panel is a **bounded summary plus
  a "show all →" link** into the S5 inbox filtered to the file. The panel shows the file's total, the four-level
  breakdown, and a bounded list of the highest-severity items with `file:line` chips (UX-DR27) and `#L{n}` deep
  links. The bound is 26.4's to set; it is a presentation limit, and per § 5.1 it must emit an explicit
  "+ N more" count, never truncate silently.
- **Empty states.** (1) **Absent** — `BuildInsightsPanel` returns `""` and the whole panel drops; on a file whose
  only content was observations the Insights tab itself drops, and the page is byte-identical to a run that never
  had analysis. (2) **Honest empty inside a present panel** — when analysis is configured and the file is clean,
  the panel renders with a specific sentence ("No observations from SonarCloud at <revision>"), never omitted,
  because omission would read as state 1. (3) and (4) do not arise: S1 is file-scoped and needs no attachment.
- **Severity without color.** The mandatory `severity.label` from the payload ships as text on every row
  (ADR 0023 § Decision 3 — UX-DR17 is satisfied by the contract, not by a convention a surface could forget).
  Severity chips route through the `--status-*` token system (FR20) and must not fork the shipped status-pill
  idiom.
- **UX-DR21.** Reuse. One representation of the file's observations on this page.
- **Reasoning.** Cheapest and most consistent; the Insights grid already degrades to absent by returning `""`, so
  empty state 1 is free and byte-identity holds without new machinery. Critically, it **preserves the one-tab
  invariant** at `CodeFileTemplater.cs:110-116` — a present-but-empty findings tab would have put a tab strip on
  uncited files that have never had one. It also avoids the two-call-site trap in § 2.1(a), and it **does not fork
  Epic 27's gutter ruling** — Epic 27 has already ruled per-line gutter marks OUT for coverage, and Epic 27
  AC 27.4 #2 binds whichever epic lands second to extend the first's code-page section rather than add another.
  S1 = B is directly compatible with that: whichever of Epic 26 / Epic 27 lands second extends the same Insights
  grid.

### 3.2 S2 — Code map / directory scope → **IN, Story 26.4 AC #2**

**Selection: A — "Seventh Dimension," with TWO selectable weightings.**

- **Placement.** A new observation dimension in `HierarchyExplorer.CodeMapDimensions`
  (`HierarchyExplorer.Projectors.cs:791`); the existing "All files" table (`AppendFileTable`) gains an Observations
  column and **remains the chart's text twin** — the code map is already configured
  `HierarchyTwinDisplay.External` (`CodeMapTemplater.cs:186-187`), so the twin obligation is met by the table, and
  the component must continue to emit no second twin.
- **Density and the weighting decision.** Two selectable weightings, both offered as dimensions:
  **"observation count"** and **"severity weight."** The reason is § 1.2(b): at 599 observations and one error, a
  raw-count tree makes `tests/SpecScribe.Tests` look like the worst region in the repository. Severity weight
  points at `src/SpecScribe`, which is the honest answer. Offering both lets the reader see that the two views
  disagree, which is itself the useful signal.
  - **The weights must be published in the legend** (FR28 real values) or the chart is unauditable. 26.4 chooses
    the actual multipliers; this record requires only that they be visible.
  - **Directory aggregation is a sum over the file scope** — there is no directory attachment in the model
    (25.3 § 11 → 26.4). Rollup rides where `Lines` already rolls up, in `CodeMapNode` / `CodeMap.Build`.
  - Because two dimensions are added, the **precomputed variant payload grows twice**; 26.4 must check this
    against the Story 6.6 byte-blind-chunker and `code-map.html` size findings before it ships.
- **Empty states.** (1) **Absent** — the dimensions are not registered at all, the table has no Observations
  column, and `code-map.html` is byte-identical. (2) **Configured and clean** — the dimension is registered and
  the tree renders with all-zero weights; the framing sentence states the zero explicitly. Per ADR 0012
  § Addendum, `null` in `values` silently renders nothing, so **branch values must be `0`, never `null`**, and
  `branchvalues: 'total'` remains invalid because parent weight ≠ Σ children. (3) and (4) do not arise —
  directory scope is derived from file paths, not from planning attachment.
- **Severity without color.** ADR 0012 § Decision 6's three independent channels are reused verbatim: **fill
  token, hatch, and the status word in the accessible name.** The Observations column in the twin carries the
  four-level breakdown as text.
- **UX-DR21.** Reuse, and this is the reason the owner did not take direction B — a second hierarchy over the same
  file tree on the hub is exactly the pressure UX-DR21 exists to prevent. **Consequence: S5's selection
  deliberately contains no hierarchy chart** (§ 3.5), which keeps this clean.
- **Requires:** a new `Charts.ChartMetric` member with its `WhyText` case (§ 2.1(d)), and
  `Charts.ChartMeta` / `Charts.Framed` supply the real-value legend, analysis window, and framing sentence by
  construction. `Charts.PlanningCodeImpactNote` is the shipped precedent for a provenance caveat in the `Note`
  slot — the analysis staleness note belongs there.

### 3.3 S3 — Epic and story pages → **IN, Story 26.5**

**Selection: B — "A Chip Per Row," plus a rollup sentence** (the rollup is the § 5.2 fan-out decision).

- **Placement.** No new section. Each impacted file row inside the existing **Code Areas Touched** block
  (`EpicsViewBuilder.cs:387-388`, rendered on both epic and story pages) gains an observation-count chip. One
  rollup sentence sits above the table inside the same block.
- **Density.** The chip carries the file's **total** observation count. The rollup sentence carries the aggregate
  and the mandatory caveat — see § 5.2 for the exact content and why the total, not a breakdown, was chosen.
- **Empty states.** (1) **Absent** — no chips, no rollup, `CodeAreasHtml` unchanged, byte-identical.
  (2) **Configured and clean** — a file row with zero observations shows **no chip** (a "0" chip on every row is
  noise); the rollup sentence states the zero for the whole entity, which is where the distinction from state 1
  lives. (3) `basis: "none"` — the entity genuinely attaches to nothing: the Code Areas Touched block is already
  absent in that case, so the rollup cannot render there; unattached observations reach the S5 hub and are
  **never silently dropped** (26.5 AC #2). (4) `basis: "unavailable"` — **the normal case, 100 % of the current
  digest.** The rollup sentence must say attachment was *not computed*, not that nothing attached. These two are
  different facts and rendering them identically is the § 3 cardinal-rule violation.
- **Severity without color.** Chips carry a text count and, where a level is named, the payload's
  `severity.label`. No level is signalled by chip color alone.
- **UX-DR21.** Strictest read — one representation, one dataset, no new heading. This is the direction's main
  virtue.
- **Reasoning.** Direction A's weakness was a heading adjacent to `Review Findings`; direction B's weakness was
  having no home for the approximateness caveat. Adding the rollup sentence removes B's weakness without
  reintroducing A's. The approximateness statement is **mandatory on the surface** (26.5 AC #1, the Story 21.2
  cycle-time precedent) — `ImpactFile` carries no numeric confidence, so approximateness is carried by the
  `AttributedCommitCount` / `TotalAnalyzedCommits` pair and by prose.
- **Counts route through the FR21 single generator-side count source** — the chips and the rollup must not become
  two independent tallies.

### 3.4 S4 — Requirement pages → **OUT, with the reason recorded**

**Selection: A — "Explicitly out."**

- **The reason.** ADR 0023 § Decision 5 already refused the edge: `requirement` is **not** a first-class
  attachment key. `TraceabilityTemplater` is a requirement→**epic** matrix, so `observation → file → epic →
  requirement` is two hops with the second at epic granularity only, composed on a join already amplifying
  **10.02×**. *"The schema will not imply an edge that does not exist."* Rendering it would compound two
  approximations on a page whose entire current voice is precise coverage prose
  (`RequirementsTemplater.cs:205`, four mutually-exclusive coverage branches).
- **Consequence: 26.5's AC #1 must be amended.** It currently reads *"epic, story, and requirement pages surface
  the findings"*. See § 11.
- **What would change the answer.** A first-class requirement→file attachment in the model — which would be an
  ADR 0023 amendment, not a 26.5 detail.

### 3.5 S5 — The analysis hub → **IN, Story 26.6. Owner-invented direction; supersedes the menu.**

The owner declined the three-way menu choice: *"I like the rule leaderboard and triage inbox approaches, but both
might need separate pages with a highlight style widget teasing the most actionable."*

**Selection: a hub landing page plus two child pages.**

- **Placement and structure.**
  - **`analysis.html` — the landing page.** Short. The highlight widget, the four-level severity breakdown, the
    provenance/staleness block, and **two full-surface link cards (UX-DR9)** into the children. One nav entry, in
    the Insights group, following the Git Insights → Deep Analytics precedent.
  - **The rule leaderboard child.** The **86 rules** as the primary axis, ranked, drilling to occurrences. This is
    where "fix `CA1861` once and clear 355 occurrences" becomes visible.
  - **The triage inbox child.** All **1,534** observations, sortable and filterable — 26.6 AC #1's *"sortable /
    filterable access to every finding including those attached to no planning entity."* This is the routed
    destination for unattached observations: *"a routed population, never a residue."*
- **The highlight widget's ranking — reader-selectable, default blended.** The reader picks **count**, **type**, or
  **blended score**; **blended is the default.**
  - "Type" reads `severity.provider[]`'s `mqr.softwareQuality` / `legacy.type` axes (§ 1.3). **This is a
    deliberate, single-surface acceptance of Sonar-specific value coupling on an otherwise source-agnostic
    model** — it is confined to the hub and must not spread to S1/S2/S3/S6. It also partly answers create-story
    Open Question 2: provider coupling is accepted *for the quality axis*, and separately declined for `BLOCKER`
    (§ 5.3).
  - **The blended formula must be written out in the framing sentence (FR28)** or the default ranking is
    unauditable. 26.6 chooses the formula; this record requires it be published on the surface, and that its
    inputs be limited to level, occurrence count, and file concentration.
- **Density.** A 1,534-row inbox needs **UX-DR28** (a grouped on-page TOC) and Story 10.9's client-light
  sort/filter pattern. Splitting the leaderboard out of the inbox is what makes both readable — this was the
  owner's stated reason for rejecting a single page.
- **Empty states.** (1) **Not configured — no pages at all and no nav entry.** Nav gating follows
  `SiteNav.Build`'s `bool has…` pattern, evaluated against **the data signal at nav-build time, not against
  successful render** (`SiteNav.cs:204-214`, gates at `:315`/`:342`/`:347`/`:352`). (2) **Configured and clean** —
  the pages exist and carry `<div class="chart-empty">` honest empties with specific sentences (`Charts.cs:101`
  precedent, and the Work Graph tab's always-present honest empty). (3) `basis: "none"` and (4)
  `basis: "unavailable"` — **the hub is the only surface where both must be legible as distinct facts**, because
  it is the only surface with no entity precondition and it must work with `--deep-git` off. An "unattached"
  filter must distinguish *"attaches to nothing"* from *"attachment was not computed"*; today 100 % of records are
  the latter.
- **Severity without color.** `severity.label` as text throughout; the breakdown restated as a sentence per
  UX-DR23 (§ 3.6).
- **UX-DR21.** Three pages, one dataset — but **three different primary representations** (a teaser, a
  rule-ranking, an occurrence list), not three views of the same one. **No hierarchy chart on the hub**, which is
  what keeps this compatible with S2 = A owning the only observation hierarchy in the portal.
- **Provenance.** The landing page carries the analysis timestamp via the portal-wide date token (UX-DR25) and
  marks stale analysis honestly (26.6 AC #3). Staleness is **revision-first**: `analysisDate` can read "an hour
  ago" while the revision is commits behind, `isStale` fails closed, and `workingTreeDirty: true` is itself a
  staleness condition because line numbers are anchored to `analysisRevision`.
- **Consequence: 26.6's AC #1 must be amended** from *"a dedicated page"* to the three-page structure. See § 11.

### 3.6 S6 — Dashboard signal → **IN, Story 26.6 AC #2**

**Selection: A — "Quality Strip."**

- **Placement.** A `chart-panel` + `<h3>` + opaque-fragment strip, copying the shape shared by the Traceability
  (`HtmlRenderAdapter.Dashboard.cs:185`) and Delivery Cadence (`:190-195`) strips — `if (view.XStripHtml.Length >
  0)`. It must not displace existing pulse content (26.6 AC #2), taking the dashboard from thirteen sections to
  fourteen.
- **It must declare a `wm-show-*` mode set** (§ 2.1(b)) or it is invisible in every workflow mode. Recommended
  `wm-show-develop wm-show-review`, for 26.6 to confirm.
- **Density.** Room for the full four-level breakdown plus the staleness note — which is why the owner declined
  the single stat tile, and declined "tile + strip" as two representations of one dataset on one page.
- **UX-DR23 binds:** the paired counts must be restated as a sentence, not only as chips —
  *"1,534 observations · 125 errors · analysed <date>"* in sentence form, derived at generation time (§ 1.1).
- **Empty states.** (1) **Absent, not empty** — the strip does not render when the integration is disabled, which
  is the default (26.6 AC #2, explicitly). (2) **Configured and clean** — the strip renders and says zero.
  (3)/(4) do not arise; the strip is repo-scoped and needs no attachment.
- **Severity without color.** Text labels in the sentence form; chips route through `--status-*` (FR20) with a
  reachable status legend.
- **UX-DR21.** One representation on the dashboard. The strip **is** the dashboard's entry point to the hub, which
  answers create-story Open Question 4 by derivation rather than by separate decision: **the hub follows the Git
  Insights / Deep Analytics pattern — a nav entry and no separate dashboard quick-link tile** — because the
  Quality Strip already provides the dashboard presence and links onward. A quick-link tile in addition would be
  the "tile + strip" duplication the owner just declined.

### 3.7 S7 — Traceability matrix → **DEFERRED to Story 26.6; the OUT recommendation stands**

**Selection: "Keep it open for 26.6."**

- **The recommendation, recorded.** OUT. `TraceabilityTemplater.cs:12` is a requirement × covering-epic grid in
  the **Delivery** nav group (`SiteNav.cs:301`), not Insights. A severity axis makes it a three-axis grid, and
  ADR 0023 § Decision 5 already refused the requirement edge — the same reasoning that made S4 explicitly OUT.
- **The honest cost, stated.** AC #1 asks for a closed in/out list. With S7 deferred, **the OUT list is closed
  except for this one candidate**, whose disposition 26.6 decides. This is a named disposition rather than an
  omission, and it is the owner's call — but it is not the same thing as a fully closed scope, and this record
  does not claim otherwise.
- **What 26.6 needs in order to decide:** whether S4 = OUT should extend by symmetry (the same ADR 0023 argument
  applies verbatim), and whether the Delivery-vs-Insights nav-group mismatch is disqualifying on its own.

---

## 4. IN / OUT — the closed list

**IN for Epic 26:**

| Surface | Story | Direction |
|---|---|---|
| S1 code file page | 26.4 | B — Third Insight Panel |
| S2 code map + directory rollup | 26.4 AC #2 | A — Seventh Dimension, two selectable weightings |
| S3 epic pages | 26.5 | B — chip per row + rollup sentence |
| S3 story pages | 26.5 | B — chip per row + rollup sentence, **plus the § 6 re-parenting** |
| S5 hub landing page | 26.6 | Owner-invented — highlight widget + two UX-DR9 link cards |
| S5 rule leaderboard child page | 26.6 | 86 rules as the primary axis |
| S5 triage inbox child page | 26.6 | All observations, sortable/filterable, unattached routed here |
| S6 dashboard Quality Strip | 26.6 AC #2 | A — 21.1/21.2 strip parity |

**OUT for Epic 26:**

| Candidate | Reason |
|---|---|
| **S4 requirement pages** | ADR 0023 § Decision 5 — `requirement` is not a first-class attachment key; two hops, second at epic granularity, on a 10.02× join |
| **S1 direction C — per-line gutter marks** | Lines are anchored to `analysisRevision` and a dirty tree misplaces every mark; forks Epic 27's already-settled gutter ruling |
| **`BLOCKER` as a distinct tier** | § 5.3 — Sonar-specific coupling to promote one missing test assertion |
| **A second severity vocabulary anywhere** | 25.3 § 11; four normalized levels site-wide, no collapse |
| **A second hierarchy over the file tree** | UX-DR21; S2 = A owns the only observation hierarchy, so S5 has none |
| **Direct ingestion of non-Sonar analyzer output** | § 9 — deferred to 26.7. The `external_roslyn` rules already *in* the Sonar payload are IN |

**DEFERRED (disposition named, decision elsewhere):**

| Candidate | Decided by |
|---|---|
| **S7 traceability matrix** | Story 26.6. Recommendation OUT stands |

---

## 5. The three decisions upstream records assigned to this story

### 5.1 The `relatedLocations` cap = **5**, uniform, with a mandatory truncation count

Assigned by `25-3-spike-report.md` § 14 item 1 — *"a surface question with real data; Story 26.1's, with the
owner."*

- **The cap is 5 secondary locations, on every surface.** Not per-surface. A uniform cap means one rule to specify,
  one to test, and no surface where a reader has to wonder whether they are seeing everything.
- **Silent truncation is forbidden.** At cap 5, **136 of the 237 issues that carry secondaries** exceed it, so the
  truncation notice is the common case among multi-location issues and must be a first-class rendering: an explicit
  **"+ N more locations"** carrying the real remainder, never an ellipsis and never nothing. The single worst case
  is 52 (`csharpsquid:S3776` in `Charts.cs`) and would read "+ 47 more locations."
- **The cap is a presentation limit, not an ingestion limit.** The payload keeps every secondary; the surface shows
  five. **`relatedLocations` is deliberately NOT sorted** — a flow is an ordered sequence — so the five shown are
  the *first* five in flow order, not the "top" five. 26.4 must not reorder them.
- Each shown location renders as a `file:line` chip (UX-DR27), never raw syntax in prose, and routes through
  `SiteGenerator.CodeItemHref` so a null return means "no page" and a dead link is structurally impossible.

### 5.2 Fan-out presentation = **total-count chip + a rollup sentence**

ADR 0023 § Consequences hands the *visual direction* here and leaves the *bounding rule* to 26.5. The direction,
stated clearly enough for 26.5 to derive a rule from it:

- **Per file row: one chip showing that file's total observation count.** Not a per-level breakdown (too dense at
  10.02× fan-out), and not errors-only (which at 125 errors repo-wide would leave most rows blank and hide 1,409
  observations from planning pages entirely).
- **Above the table: one rollup sentence** carrying the aggregate, the four-level breakdown in UX-DR23 sentence
  form, and the mandatory approximateness caveat. Example shape — *"41 observations across 7 files · 6 errors ·
  file attribution inferred from commit and branch naming, not tracked."*
- **The rollup is where the caveat lives.** This is the whole reason it was added: direction B had no natural home
  for the Story 21.2-precedent approximateness statement that 26.5 AC #1 requires be *stated on the surface*.
- **The bounding rule 26.5 must derive:** the chip is a count, so no per-observation bound is needed on planning
  pages at all — the fan-out is absorbed by aggregation rather than by truncation. 26.5's rule therefore governs
  **which files appear as rows**, which is already `PlanningCodeImpact`'s existing behavior, not a new bound. The
  drill path for detail is the S5 inbox, filtered.
- **Counts route through the FR21 single generator-side count source** (26.5 AC #2) — chip and rollup are two
  renderings of one tally, never two tallies.

### 5.3 Severity collapse = **none. Four levels everywhere. `BLOCKER` not surfaced.**

25.3 § 11 → 26.1: *"Do not invent a second severity vocabulary. If four levels are too many for a surface, collapse
in the surface and say so."*

- **No surface collapses.** All four normalized SARIF levels — `error` / `warning` / `note` / `none` — on every
  surface including the cramped ones (S1's insight panel, S3's chips). One vocabulary site-wide, nothing to
  reconcile when moving between surfaces, and no "stated collapse" caveat to maintain in four places.
- **The `BLOCKER` is not surfaced as a tier.** It normalizes to `error` and stays there. The cost, recorded per
  25.3's instruction: Sonar's five levels collapse into SARIF's four, so `BLOCKER` and `HIGH` are indistinguishable
  at `severity.normalized`, and the one `BLOCKER` in this repository — a missing test assertion at
  `tests/SpecScribe.Tests/ChartsTests.cs:338` — reads as one of 125 errors. **That is the accepted cost.**
  Promoting it would have added Sonar-specific value coupling to every surface that read it, to elevate a missing
  test assertion.
  - Note the deliberate asymmetry with § 3.5: the hub *does* read `severity.provider[]` for its **quality-axis**
    sort. Provider coupling is accepted for the *quality dimension*, on *one* surface, and declined for the
    *severity dimension* everywhere. These are separate decisions and both are intentional.
- **`none` is styled even though it never appears here.** Severity is a **closed four-value domain**. ADR 0026
  § Decision 2 (Proposed): where a class is drawn from a closed domain, **seed the domain, not the observed
  subset.** A generated style layer emitting only the classes today's run happens to produce would ship an
  unstyled `none` — and an unstyled `error` — for a project whose run contains them. `none` is 0 in this
  repository and `error` is not, but the rule is the same: **26.4 seeds all four.** This is the single most likely
  way for Epic 26 to ship a silent visual defect.

---

## 6. The site-wide noun, and the re-parenting

**The noun is "Analysis Observations."** ADR 0023 § Decision 1 locks it: the machine-ingested, provider-attributed
population is never called "Findings" on a rendered surface. "Review Findings" remains the name of the *human,
authored* review prose that story pages already render.

**Owner selection: apply the re-parenting now, on story pages.**

- Story pages gain a single **Quality** parent heading. `Review Findings` becomes a subsection beneath it, and the
  Analysis Observations rollup (§ 5.2) becomes its sibling subsection.
- **This moves shipped markup.** `HtmlRenderAdapter.Epics.cs:613-618` currently emits
  `<section class="chart-panel review-findings" id="sec-review-findings"><h3>Review Findings</h3>` with a
  `Toc.Entry(2, …)`. Re-parenting changes the section nesting and the TOC depth on **every story page that has
  review prose**. The golden fingerprint **will** move for 26.5, beyond what the chips alone would have caused —
  and 26.5's re-baseline must be a deliberate, two-run stability check, on top of whichever sibling stories are
  in the tree at the time (CLAUDE.md § Concurrent work).
- **Epic pages are unaffected.** `ReviewFindingsHtml` is rendered in the story branch only; the epic branch has no
  equivalent (§ 2.1(c)).
- **One detail 26.5 must settle at its create-story, not here.** S3 = B puts the per-row chips *inside* Code Areas
  Touched (`id="sec-code-areas"`), while the rollup becomes an Analysis Observations subsection under the Quality
  parent. Those are two different sections. 26.5 decides whether Code Areas Touched moves under the Quality parent
  as well, or whether the rollup sits under Quality and links down to the chips — **with the hard constraint that
  the chips and the rollup remain one FR21 tally, not two.** This record names the decision rather than
  pre-empting it.
- **`id="sec-review-findings"` should be preserved** if at all possible: it is a stable anchor and existing
  deep links to it should not break.

---

## 7. AC #2 — reuse vs new pages, and what an unconfigured project sees

### 7.1 Reuse vs new (UX-DR21)

**Reused, no new page:** S1 (the Insights grid), S2 (the code map's dimensions and its existing twin table), S3
(the Code Areas Touched block), S6 (the dashboard's shipped strip shape).

**New pages — three, all on the hub (S5):** `analysis.html` plus the leaderboard and inbox children. The
justification is that they are the only surfaces with **no entity precondition**: they must work with `--deep-git`
off, and they are the routed destination for the unattached population, which by definition has no entity page to
live on. Every other decision reuses.

UX-DR21 is satisfied because the observation dataset has **one primary representation per surface**, and the only
duplicated shape — a hierarchy over the file tree — was deliberately kept to exactly one place (S2), which is why
S5 has no hierarchy chart.

### 7.2 What a project with no analysis configured sees — **idiom (a), absent, everywhere**

This is the default for every user, and it is the answer for every surface:

| Surface | Unconfigured behavior | Mechanism |
|---|---|---|
| Code file page | No observations panel. If it was the only Insights content, the Insights tab drops too, and the one-tab bail applies unchanged | `BuildInsightsPanel` returns `""` |
| Code map | No observation dimensions registered; no Observations column in the file table | dimensions list unchanged |
| Epic / story pages | No chips, no rollup, `Review Findings` **not** re-parented | `CodeAreasHtml` and the story branch unchanged |
| Requirement pages | Nothing — S4 is OUT in every configuration | n/a |
| Hub (3 pages) | Pages not generated; **no nav entry** | `SiteNav.Build` `bool has…` gate on the data signal |
| Dashboard | **Absent, not empty** — no strip, no tile | `if (view.XStripHtml.Length > 0)` |

**The acceptance test is byte-identity** with a run that never had analysis — the shipped idiom-(a) contract
(`CodeFileTemplater.cs`, `EpicsViewBuilder.cs:387`, `DashboardView.cs`). **And no network call is made.** Story
26.3 AC #2 makes that a requirement: *"an existing user upgrading sees no behavior change."*

**Where idiom (b) — honest empty state — applies instead:** only where a surface must exist regardless. That is
the S5 pages when analysis *is* configured but clean, and the S1 panel when analysis *is* configured and the file
is clean. Both use `<div class="chart-empty">` with a **specific** sentence naming the provider and revision, never
a generic "no data" — because a generic empty is indistinguishable from state 1 to a reader.

**Where idiom (c) — the unconfigured-source `role="note"` — applies:** nowhere in Epic 26's baseline. Idiom (c)
exists for a surface that renders while one optional source is off (`CodeMapTemplater.cs:171-174`,
`Charts.cs:3107`). Epic 26's surfaces are absent rather than degraded when the integration is off, so (c) has no
baseline role. **One candidate for 26.6 to consider:** on the hub, when analysis is configured but `--deep-git` is
off — empty state 4, the current 100 % case — a `role="note"` explaining that attachment was not computed and how
to enable it is exactly idiom (c)'s purpose. Placed **outside** any legend, deliberately: it is a fact about the
data, not chrome for a chart.

---

## 8. Handoff — what each downstream story inherits

### 8.1 → Story 26.2 (SPIKE: ingestion posture, credentials, NFR-3)

- **Nothing here is blocked on your answer, and nothing here constrains it.** Every direction was chosen to be
  **posture-independent** — none depends on whether observations arrive via the SonarCloud web API or an on-disk
  export. Confirming create-story Open Question 5: **no selection in this record changes if 26.2 picks either
  posture.** Do not re-run this round.
- What you must be able to feed: per-file observation lists (S1), a per-file count and per-level breakdown
  aggregable to directories (S2), a repo-wide list with rule identity (S5), a repo-wide four-level tally (S6), and
  `relatedLocations` in **flow order, unsorted** (§ 5.1).
- **`helpUri` is present on all 1,534 records** and is a synthesized organization permalink, not an API field
  (25.4). Surfaces may render "learn more" links; your posture must keep supplying it.
- Your ADR is Epic 26's ADR. This record proposes none (§ 10.2).

### 8.2 → Story 26.3 (configuration)

- **Disabled by default is load-bearing for every surface in this record.** § 7.2's byte-identity requirement and
  "no network call" are what your AC #2 has to guarantee.
- The nav gate is a `bool has…` flag on `SiteNav.Build` evaluated against **the data signal at nav-build time, not
  successful render** — so your configuration state must be readable at that point in the pipeline.

### 8.3 → Story 26.4 (code pages + code map)

- **S1 = B.** Third `insight-panel` inside `BuildInsightsPanel`. Preserve the one-tab invariant at
  `CodeFileTemplater.cs:110-116`. Bounded list + "show all →" into the S5 inbox filtered to the file.
- **S2 = A with two selectable weightings** — "observation count" and "severity weight" — as members of
  `CodeMapDimensions` in **`HierarchyExplorer.Projectors.cs:791`** (not `CodeMapTemplater.cs`, which is where the
  story file pointed). **Publish the weights in the legend** (FR28) or the chart is unauditable.
- **Add a tenth `Charts.ChartMetric` member with its `WhyText` case.** Story 10.2 AC #2 forbids hand-rolled "why
  this matters" copy at call sites.
- The code map is `HierarchyTwinDisplay.External` (`CodeMapTemplater.cs:186-187`) — **`AppendFileTable` IS the
  twin.** Add the Observations column there; do not let the component emit a second twin. Verify JS-off in a **live
  browser**, not by test assertion (ADR 0013 § Decision 3 is a hard gate; Story 21.1's two defects are why).
- ADR 0012 § Addendum: **branch values must be `0`, never `null`** (`null` silently renders nothing);
  `branchvalues: 'total'` is invalid; one synthesized root required. § Decision 6's three channels — fill token,
  hatch, status word in the accessible name — are your severity-without-color pattern.
- **Seed all four severity classes in any generated style layer** (§ 5.3, ADR 0026 § Decision 2). `none` is 0 here
  and will not be in every project.
- ⚠️ **`impacts[]` is returned by Sonar in NON-DETERMINISTIC order.** The emitter sorts it; seven shards flipped
  between two states on identical input. **You are putting this shape into the Epic 22 IR, and the IR IS covered by
  the golden fingerprint** — unsorted, the fingerprint flaps at random with no source change. Sort it.
  **`relatedLocations` is deliberately NOT sorted** — do not "fix" that by symmetry.
- Cap `relatedLocations` at **5** with an explicit **"+ N more locations"** count (§ 5.1).
- **The two-call-site trap:** tabs are assembled at `CodeFileTemplater.cs:104-108` **and** `:786-790`. S1 = B
  avoids it, but any deviation toward a new tab must touch both.
- **Two new dimensions grow the precomputed variant payload twice.** Check it against Story 6.6's byte-blind
  chunker and `code-map.html` size findings before shipping.
- **Epic 27 coordination (AC 27.4 #2):** whichever epic lands second extends the first's code-page section rather
  than adding a second one. S1 = B is directly compatible — both land in the Insights grid. **Per-line gutter
  marks are OUT for observations**, matching Epic 27's existing ruling.
- The golden fingerprint **will** move. Re-baseline with a two-run stability check, and say in the story record
  whose concurrent changes your regeneration sat on top of.

### 8.4 → Story 26.5 (planning entities)

- **S3 = B: a total-count chip per file row inside Code Areas Touched, plus one rollup sentence above the table.**
  The rollup carries the four-level breakdown in UX-DR23 sentence form **and the mandatory approximateness
  caveat** — that is what the rollup is for.
- **The fan-out is absorbed by aggregation, not truncation** (§ 5.2). Your bounding rule governs which file rows
  appear — existing `PlanningCodeImpact` behavior — not a per-observation cap. Detail drills to the S5 inbox.
- **`ImpactFile` has no numeric confidence.** Approximateness rides `AttributedCommitCount` /
  `TotalAnalyzedCommits` and prose, the Story 21.2 cycle-time precedent.
- **Empty states 3 and 4 are different facts.** `basis: "unavailable"` is **100 % of the current digest** because
  both `PlanningCodeImpact` call sites are gated on `--deep-git`, which is off by default (owner decision D5 on
  25.4). Your rollup must say attachment was *not computed* — not that nothing attached.
- **AC #1 must be amended: requirement pages are OUT** (§ 3.4, § 11). Do not build S4.
- **You own the § 6 re-parenting**, on **story pages only**: one **Quality** parent heading with `Review Findings`
  and the Analysis Observations rollup as sibling subsections. **This moves shipped markup and TOC depth** — the
  fingerprint move is larger than the chips alone. Preserve `id="sec-review-findings"` as an anchor. **Settle at
  create-story:** whether Code Areas Touched moves under Quality too, or the rollup links down to the chips —
  under the hard constraint that chips and rollup stay **one** FR21 tally.
- Counts route through the **existing single generator-side count source** (FR21 / Story 8.3), never a new tally.

### 8.5 → Story 26.6 (hub + dashboard)

- **The hub is THREE pages** (§ 3.5), not one: `analysis.html` landing + a rule-leaderboard child + a triage-inbox
  child. **Your AC #1 must be amended** from "a dedicated page" (§ 11).
- **Landing page:** highlight widget, four-level breakdown, provenance/staleness, and **two UX-DR9 full-surface
  link cards** into the children. **One nav entry**, Insights group, Git Insights → Deep Analytics precedent.
- **Highlight ranking is reader-selectable — count / type / blended — defaulting to blended.** "Type" reads
  `severity.provider[]`'s `mqr.softwareQuality` / `legacy.type` axes (§ 1.3): **the only accepted provider
  coupling in Epic 26, confined to this surface.** **Publish the blended formula in the framing sentence (FR28)**;
  limit its inputs to level, occurrence count, and file concentration.
- **The inbox is the routed destination for unattached observations** — never a residue, never silently dropped
  (26.5 AC #2). It must distinguish `basis: "none"` from `basis: "unavailable"` in its filters; **it must work with
  `--deep-git` off**, which is the default.
- ~1,534 rows needs **UX-DR28** (grouped on-page TOC) and Story 10.9's client-light sort/filter pattern.
- **No hierarchy chart on the hub** — S2 owns the only observation hierarchy (UX-DR21).
- **S6 = A, the Quality Strip:** copy the Traceability / Delivery Cadence shape at
  `HtmlRenderAdapter.Dashboard.cs:185` / `:190-195`. **Declare a `wm-show-*` mode set** or it is invisible in every
  workflow mode — recommended `wm-show-develop wm-show-review`, yours to confirm. **Absent, not empty, when
  disabled.** UX-DR23 sentence form for the breakdown. **No separate dashboard quick-link tile** — the strip is the
  dashboard presence (§ 3.6).
- **Provenance:** portal-wide date token (UX-DR25); staleness is **revision-first**; `isStale` fails closed;
  `workingTreeDirty: true` is itself a staleness condition. Consider idiom (c) — a `role="note"` outside any
  legend — for the `--deep-git`-off case (§ 7.2).
- **You decide S7** (§ 3.7): the traceability matrix. The recommendation is OUT and the reasoning is recorded.
- **Every figure in this record moves.** Derive counts at generation time; hard-code nothing (§ 1.1).

### 8.6 → Story 26.7 (future-integration investigation)

- **Your central question has changed shape.** `external_roslyn` is **56 % of the current payload** (§ 1.2(c)) —
  Roslyn analyzer output already reaches the portal *through* Sonar. So the landscape question is not "should we
  ingest analyzer output" but **"is there value in ingesting it DIRECTLY, given Sonar already carries it?"** —
  a narrower and more answerable question, and it should be asked that way.
- The language-dependence figure to carry: a non-.NET project loses the 859 `external_roslyn` observations,
  i.e. sees roughly **44 %** of what this repository shows. That is the concrete NFR8 degradation cost.
- Epic 26's surfaces render whatever the configured provider emits, with **no engine distinction** (§ 9). If you
  recommend a second provider, the engine question reopens as a *presentation* question, not just an ingestion one.

---

## 9. AC #3 — non-Sonar source classes: **IN SCOPE AS-IS, no engine distinction rendered**

The owner's original framing was *"we could potentially fold in code analysis warnings as well, but that gets to be
language dependent."* The refreshed measurement changes the question: **that has already happened.** 859 of 1,534
observations (56 %) are `external_roslyn:` rules — `CA1861`, `SYSLIB1045`, `CA1859`, `CA1816`, `CA1822` — Roslyn
analyzer output imported by SonarCloud as an external engine (§ 1.2(c)).

**Selection: Epic 26's surfaces render every rule identically regardless of engine. No engine facet, no engine
badge, no engine filter.**

- **This is not a deferral.** The non-Sonar source class in the payload today is fully in scope and fully
  rendered — it simply is not *distinguished*. A rule is a rule.
- **Why:** it keeps the model source-agnostic in **presentation** as well as in schema, matching ADR 0023's
  framing that Sonar is instance #1 rather than the schema. Rendering an engine facet would have made the portal's
  surfaces carry provider taxonomy that the contract deliberately abstracts away.
- **The language-dependence trade-off, written down as AC #3 requires:** the observation population is
  **language-composed, not language-neutral.** In this repository the engine split by tree is `src` → 606
  `csharpsquid` + 263 `external_roslyn` + 9 `css`; `tests` → 596 `external_roslyn` + 3 `csharpsquid`;
  `web` → 34 `javascript` + 5 `typescript` + 2 `jssecurity` + 2 `Web` + 1 `css`; `extension` → 13 `typescript`.
  **A non-.NET project sees roughly 44 % of what this one does** — the `external_roslyn` contribution vanishes
  entirely. Every surface therefore degrades in *density*, not in *correctness*: the panels, dimensions, chips,
  pages, and strip all still render, with smaller numbers. That is an acceptable NFR8 degradation because nothing
  becomes empty-but-present or broken; it is recorded here so no downstream story reads this repository's density
  as typical.
- **What is deferred to 26.7:** ingesting analyzer or compiler output **directly**, not routed through the
  configured provider. § 8.6 restates that question in its narrowed form.
- **What would change the answer:** a second provider whose rule identities collide with the first's, or a
  measured case where a reader was misled by two engines' rules sitting in one list undistinguished. Either makes
  the engine facet a presentation requirement rather than an option.

---

## 10. Task 7 and Task 8 — the ADR trigger, and proof this shipped nothing

### 10.1 ADRs read before claiming any rule was crossed

Per memory `adr-consultation-gap-three-arc-renderers`, `docs/adrs/` was read before asserting any project rule is
being crossed. Relevant state: ADR 0023 **Accepted** (consumed, not amended); ADR 0012 / ADR 0013 govern any
hierarchy and its twin; **ADR 0010 § 1/2/6 is superseded by 0012/0013, and JavaScript on opt-in deep-analytics
surfaces is already permitted** — so S5's sortable/filterable inbox and the code map's interactive dimensions cross
no rule. The highest existing ADR is **0028**; the next number is **0029**.

### 10.2 ADR trigger: evaluated, and declined — with the condition that would fire it

No selection in this record amends a ratified ADR:

- **ADR 0023** is consumed as-is. No selection requires a field the contract does not carry. S5's "type" sort
  reads `severity.provider[]`, which the contract already emits.
- **ADR 0012 / 0013** are *complied with*, not amended — S2 = A adds a dimension to an existing Explorer surface
  whose twin is already `External`, and S5 deliberately adds **no** hierarchy.
- **ADR 0026 § Decision 2** is *applied*, not amended (§ 5.3).
- Epic 26's own ADR is **Story 26.2's**, by design.

**What would fire the trigger, named so it is not missed later:** (a) if 26.5's resolution of the § 6 open detail
turns the "Quality parent heading" into a **portal-wide sectioning convention** binding surfaces beyond story
pages, that is a cross-cutting presentation contract and deserves an ADR; (b) if 26.6 finds the blended highlight
score needs inputs beyond level / count / concentration — anything provider-specific — that widens the provider
coupling this record confined to one surface, and deserves an ADR; (c) if 26.7 recommends a provider seam, that is
already named as its own ADR trigger.

### 10.3 The no-code contract — verified by attribution, not by a clean status

**Task 8 asks for a `git status` showing no `src/` / `tests/` / `extension/src/` / `web/` changes. The tree does
not show that, and the honest answer is that none of those changes are this story's.** Stating "clean" would be
false; stating the attribution is the correct form of the check under CLAUDE.md § Concurrent work.

- **This story's tracked changes are exactly three files**, all under `_bmad-output/`: this record (new),
  `epics.md`, and `sprint-status.yaml`. Confirmed with `git status --porcelain -- _bmad-output/`.
- **A concurrent session holds uncommitted work** in `src/SpecScribe/DesignSystemTemplater.cs`,
  `src/SpecScribe/assets/specscribe.css`, two `tests/SpecScribe.Tests/` design-system/stylesheet test files, and
  sixteen `web/` files (`tokens.css`, `ir-content*`, `ListRow.vue`, `StatusBadge.vue`, `nuxt.config.ts`,
  `design-system.vue`, the `web/scripts/` IR-content and token libraries) plus a new
  `web/assets/shared-primitives.css`. **This story authored none of them and touched none of them.** Per
  CLAUDE.md, that work was left strictly alone — no `git reset --hard`, no `git checkout --`, no `git clean`.
- **No generation run was required and none was performed**, so `GoldenContentFingerprint` was never measured,
  moved, or re-baselined by this story. This is the correct posture: with a concurrent session actively editing
  `specscribe.css` — an **embedded resource** — any fingerprint this story measured would have been reading
  somebody else's in-flight change, and an incremental build would not even have re-embedded it (memory
  `golden-diff-normalization-gotchas`).
- `.specscribe/analysis/` was regenerated (Task 1). It is **gitignored and dev-time only**, so it is not a
  repository change and does not appear in the File List.

---

## 11. Structural amendments required (CLAUDE.md § Decision records)

Two selections change downstream scope, so `epics.md` **and** `sprint-status.yaml` are both amended in this same
change — a change recorded in only one artifact is a drift bug.

| # | Artifact | Change | Cause |
|---|---|---|---|
| 1 | `epics.md` § Story 26.5 AC #1 | Remove requirement pages from the entity list; record S4 as explicitly OUT | § 3.4 |
| 2 | `epics.md` § Story 26.5 | Add the § 6 re-parenting as in-scope work on story pages | § 6 |
| 3 | `epics.md` § Story 26.6 AC #1 | "a dedicated page" → hub landing page + two child pages | § 3.5 |
| 4 | `epics.md` § Story 26.6 | Add the S7 traceability-matrix disposition as 26.6's to decide | § 3.7 |
| 5 | `sprint-status.yaml` 26.4 / 26.5 / 26.6 | Mirror all of the above, plus each story's inherited selection | all |
| 6 | `sprint-status.yaml` 26.7 | Record that its central question narrowed — `external_roslyn` is already 56 % of the payload, so the question is direct-vs-via-provider ingestion | § 1.2(c), § 8.6, § 9 |

Amendment 6 is a **scope refinement, not a scope change** — 26.7's acceptance criteria already cover "local
compiler/analyzer output," so no `epics.md` AC needed rewriting; what changed is the *shape of the question*, which
belongs in the story's context note.

---

## 12. Verification

| Check | Result |
|---|---|
| Digest refreshed and read-time staleness rule applied | ✅ `evaluatedAtRevision` = `HEAD` = `630ae25`, `commitsBehind: 0` |
| Every cited attach point re-verified **by symbol** | ✅ all exist; 5 line numbers drifted, corrected in § 2 |
| 2–3 named directions offered per surface, owner selected | ✅ S1–S7; **S5 owner-invented, supersedes the menu** |
| Per surface: placement · density · four empty states · severity-without-color · UX-DR21 · in/out | ✅ § 3.1–3.7 |
| Reasoning captured, not just the pick | ✅ § 3, each subsection |
| `relatedLocations` cap settled with mandatory truncation count | ✅ § 5.1 — cap 5, "+ N more locations" |
| Fan-out presentation direction stated for 26.5 | ✅ § 5.2 |
| Severity collapse settled per surface | ✅ § 5.3 — no collapse, four levels, no `BLOCKER` |
| Site-wide noun settled, collision resolved | ✅ § 6 |
| Closed IN **and** OUT list | ⚠️ § 4 — closed except **S7, deferred to 26.6 by owner decision**; stated, not hidden |
| Default (unconfigured) case answered per surface | ✅ § 7.2 — idiom (a) everywhere, byte-identity, no network call |
| AC #3 settled with the language-dependence trade-off recorded | ✅ § 9 |
| Per-downstream-story handoff | ✅ § 8.1–8.6 (26.2, 26.3, 26.4, 26.5, 26.6, 26.7) |
| ADR trigger checked, `docs/adrs/` read first | ✅ § 10.1–10.2 — declined, with the firing conditions named |
| No `src/` / `tests/` / `extension/src/` / `web/` changes **from this story** | ✅ § 10.3 — this story's tracked changes are 3 files, all under `_bmad-output/`. ⚠️ Those directories are **not** clean: a concurrent session holds ~20 files of design-system / IR-content work, untouched by this story and left strictly alone |
| Golden fingerprint unmoved | ✅ no generation run performed, so it was never measured or re-baselined — the correct posture with a concurrent session editing the embedded `specscribe.css` |

**Create-story open questions, all five resolved:**

1. Severity weighting for the density visual → **two selectable weightings** (§ 3.2), against the story's stated
   raw-count default. The 39 %-test-tree measurement is why.
2. Surface the `BLOCKER`? → **No** (§ 5.3), matching the story's default, with the collapse cost recorded.
3. The "Review Findings" adjacency → **one Quality parent heading, re-parented now on story pages** (§ 6).
4. Hub nav asymmetry → **nav entry, no dashboard quick-link tile**; derived from S6 = A rather than decided
   separately (§ 3.6).
5. Does the record survive a 26.2 posture change? → **Yes, explicitly** (§ 8.1). Every direction is
   posture-independent; 26.2 is not blocked on re-running this round.

---

## 13. What this record deliberately does not decide

- **Ingestion posture and credentials** → Story 26.2, including the PRD NFR-3 local-first question. Not pre-judged.
- **The findings data model** → ADR 0023, Accepted. Consumed, never redefined.
- **Configuration surface** → Story 26.3.
- **Coverage** → Epic 27 / FR42, deliberately separate. A per-file metric has no rule identity, message, severity,
  or location.
- **The fan-out bounding rule** → Story 26.5 derives it from § 5.2's direction.
- **The blended-score formula, the density weight multipliers, the S1 panel bound, and the `wm-show-*` mode set** →
  26.4 / 26.6 detail decisions, constrained but not fixed here.
- **The S7 traceability-matrix disposition** → Story 26.6, by owner decision.
