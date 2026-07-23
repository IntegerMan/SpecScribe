---
baseline_commit: c5b93734d56d618a7a117060a1f4a3917d2745aa
---

# Story 21.1: Traceability Coverage Matrix

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder judging project rigor,
I want a visual FR/NFR/UX-DR × covering-work grid,
so that coverage completeness and the exact gaps are legible in one glance instead of read line-by-line.

## Acceptance Criteria

1.
**Given** the Story 9.2 coverage data and the FR Coverage Map
**When** the matrix renders
**Then** requirements form one axis and covering stories/epics the other, each cell showing covered / deferred-on-purpose / unmapped via the canonical `--status-*` tokens
**And** it carries a Story 10.2-compliant legend and framing sentence, and cells deep-link to the requirement/story pages.

2.
**Given** a project with sparse or no requirement mapping (NFR8)
**When** the underlying data is thin
**Then** the matrix degrades to an honest state (e.g., "coverage not yet mapped") rather than a misleading empty grid
**And** it does not re-count items against the single-source counts (Story 8.3).

## Context & Scope

Epic 21 gives first-time visitors and stakeholders a few high-impact displays that make the product's value legible at a glance. Story 21.1 is the first: **a visual traceability matrix** — a requirement × covering-work grid that turns the line-by-line coverage prose (already rendered by Stories 9.1–9.3 on `requirements.html`) into a single scannable picture of "what has a delivering epic, and what is a gap."

**This is a rendering + wiring story, not a new data model.** Every input already exists:
- `RequirementsModel.Everything` (FR + NFR + UX-DR), each `RequirementInfo` carrying `CoverageEpicNumbers`, `Status`, `Deferred`, `Slug`, `Id`, `Kind`. [Story 9.2]
- `RequirementsParser.StoriesFor(req, epics)` resolves a requirement's covering epics → their stories (epic-level, best-effort). [Story 3.7]
- `ProjectCounts.RequirementsOverall` (a `RequirementSatisfaction`) is the **single source** for requirement-satisfaction counts. [Story 8.3 / 9.9]
- `Charts.Framed` / `ChartMeta` / `ChartMetric` / `WhyText` is the shared chart-frame that supplies the legend/window/why by construction. [Story 10.2]
- The status vocabulary and `--status-*` tokens route through `StatusStyles`. [memory `specscribe-status-token-system`]

### Owner-selected design directions (locked at create-story — do not re-litigate)

Elicited up front per the standing project rule that a new visual surface gets its silhouette chosen by the owner, not the dev (memory `create-story-elicit-visual-intent`; precedent Stories 9.2, 7.12):

1. **Placement — dedicated page + two compact previews.** Build a **dedicated `traceability.html` page** (`SiteNav.TraceabilityOutputPath`) that hosts the **full** matrix. Add a **compact coverage-strip preview** in two places — the **home dashboard** and the **`requirements.html` page** — each linking to the dedicated page (`View full traceability matrix →`). The dedicated page is the primary deliverable; the two previews are lightweight teasers, not second full matrices.
2. **Axis — requirements (rows) × covering EPICS (columns), with a covering-story rollup.** Columns are the **bounded** set of covering epics (~22 today), **not** stories. Per-story columns would be unreadably wide (88+) *and* would over-claim precision the data does not have — `CoverageEpicNumbers` is epic-level, and `StoriesFor` returns "any story in a covering epic," never a per-requirement story mapping (see the truthfulness caveat below). Each **covered** cell surfaces its **covering-story rollup** (the covering epic's stories, via `StoriesFor` scoped to that one epic) in the cell's rich tooltip + text equivalent — this is the "grouped: epics with story rollup" the owner chose.
3. **Cell states — literal 3-state (AC wording).** Each cell is exactly the AC's **covered / deferred-on-purpose / unmapped**, colored through `--status-*` tokens only. This is intentionally coarser than the six-tier `RequirementStatus` the rest of `requirements.html` uses — the matrix's job is one-glance legibility, so it collapses the six tiers into three. Reconcile the seam explicitly (see "The 3-state mapping" below) so the coarser vocabulary reads as a deliberate rollup, not a contradiction with the six-tier donuts one page over.

### The truthfulness caveat you MUST honor (the project's cardinal rule)

Coverage is **epic-level**. A "covered" cell means "an epic that covers this requirement exists," never "this requirement is N% done" or "these specific stories implement it." Frame every label and tooltip accordingly ("delivered by Epic N", "stories in the covering epic"), exactly as Stories 9.1–9.3 already do. The covering-story rollup is "the stories in the covering epic," not "the stories that implement this requirement." Never let the matrix imply a finer mapping than the data supports. [Source: `src/SpecScribe/RequirementsModel.cs:5-21`, `RequirementsParser.cs:314-327`; memory — Story 1.5 truthfulness, Epic 3 "parser over-claims" retro item]

### The 3-state mapping (single source — derive, do not hand-roll)

The three cell/summary states derive from the existing six-tier `RequirementStatus` — **do not invent a parallel classifier**:

| Matrix state | `--status-*` family | Derived from |
|---|---|---|
| **Covered** | a positive/neutral covered family (see below) | `req.CoverageEpicNumbers.Contains(thisEpic)` for a **cell**; for a **row/summary**: has ≥1 covering epic and not deferred (i.e. `Status` ∈ {Done, Active, Ready, Planned}) |
| **Deferred on purpose** | `deferred` (grey) | `req.Deferred` / `Status == RequirementStatus.Deferred` |
| **Unmapped** | `pending` (tan) — the same swatch Unmapped already uses portal-wide (owner decision #1, Story 9.3) | `req.CoverageEpicNumbers.Count == 0` and not deferred / `Status == RequirementStatus.Unmapped` |

- For the **summary strip + legend counts**, read `ProjectCounts.RequirementsOverall` (`RequirementSatisfaction`) and map: `covered = Done + Active + Ready + Planned`, `deferred = Deferred`, `unmapped = Unmapped`. This keeps the single-source guarantee (AC #2) — **no local `.Count(...)` over requirements for the headline numbers**. `RequirementSatisfaction.SatisfactionChipTiers` already groups Satisfied / In flight / Deferred on purpose / Unmapped; reuse that shape, collapsing Satisfied + In flight → "Covered". [Source: `src/SpecScribe/ProjectCounts.cs:24-77,217`]
- **Covered cell color:** pick ONE existing `--status-*` swatch for "covered" (recommended: the `done`/green or a neutral covered treatment) and keep it stable — the matrix is 3-state by owner decision, so covered cells do **not** re-encode the six tiers by color. The row's six-tier *badge* (if you show a per-row status chip) may still use `StatusStyles.RequirementBadge`, but the grid cells themselves stay 3-state. Confirm the exact covered swatch at review (it pins golden bytes + CSS). Whatever you choose, every state pairs color with text/icon — **never color-only** (UX-DR17).

## Tasks / Subtasks

- [x] **Task 1 — The matrix builder `Charts.TraceabilityMatrix` (AC: #1, #2)**
  - [x] Add `public static string TraceabilityMatrix(RequirementsModel reqs, EpicsModel epics, string prefix)` to [`Charts.cs`](src/SpecScribe/Charts.cs) (co-located with `RequirementFlow`/`RequirementStatusGrid`, its siblings — no new file). Rows = `reqs.Everything` (FR then NFR then UX-DR, source order); columns = the ordered covering-epic set. Reuse the existing column-key plumbing: `CoverageKeys(all)` / `ForCoverageKey` / `EpicTitlesByNumber` already compute "covering epic numbers ascending, then a No-coverage bucket" for the Sankey — extend/reuse them rather than re-deriving the epic axis. [Source: `Charts.cs:2744-2773`, `RequirementFlow` at `Charts.cs:2805`]
  - [x] Emit a **pure HTML table/grid** (a sibling of `RequirementStatusGrid`, which is HTML not SVG — [`Charts.cs:2696`]), no info-bearing JS. Sticky first column (the requirement id/label) and a horizontally scrollable body so the ~22-column grid never forces the page to scroll horizontally (wrap the grid in an `overflow-x:auto` container). Column headers = `Epic N` linking to `epics/epic-{n}.html`; row headers = `req.Id` linking to `requirements/{req.Slug}.html`.
  - [x] **Cells (3-state, per the mapping table above):**
    - Covered cell (this epic covers this requirement): the covered swatch + a marker glyph (icon, never color-only) + a rich `js-tip`/`data-tip` tooltip carrying `req.Id · Epic N` and the **covering-story rollup** — the covering epic's stories from `StoriesFor(req, epics)` filtered to this epic's `Stories` (id + title + `StatusStyles.StoryLabel`), honestly framed "Stories in Epic N (epic-level coverage)". Cell links to the epic page (or the requirement page — pick one, keep consistent; recommend the epic page since the column is the epic).
    - Non-covering cell (this epic does not cover this requirement): empty/blank neutral cell (not a state — absence of coverage by this specific epic is normal and must not read as a gap).
    - **Row-level state:** a deferred requirement's row carries the `deferred` treatment + word; a fully-unmapped requirement's row (zero covering epics, not deferred) carries the `unmapped`/"Not yet mapped" treatment. The per-row status marker uses `StatusStyles` (badge = icon + word) so the row is legible even with every cell blank.
  - [x] **Text equivalent (never diagram-/grid-only — the standing a11y rule).** The table itself is the text equivalent (semantic `<table>` with `<th scope="col">`/`<th scope="row">`, a `<caption>`, and per-cell `<title>`/aria as needed). If you render a non-`<table>` grid, provide an sr-only list mirroring the RequirementFlow/RequirementStatusGrid precedent. A screen-reader user must be able to learn, per requirement, which epics cover it and whether it is unmapped/deferred.
  - [x] **Degrade honestly (AC #2).** If `reqs.Everything` is empty, OR no requirement has any covering epic (every row would be unmapped), render a `chart-empty`-style honest note ("Coverage not yet mapped — no requirement is yet tied to a delivering epic.") instead of a misleading empty grid. Mirror the existing `chart-empty` pattern (`RequirementFlow` returns `<div class="chart-empty">Nothing to chart yet.</div>` for the empty case — [`Charts.cs:2808`]).

- [x] **Task 2 — Frame it (Story 10.2 compliance: legend + framing sentence) (AC: #1)**
  - [x] Add `ChartMetric.RequirementTraceability` to the enum in [`Charts.cs:13-25`] and a `WhyText` case: a metric-generic, framework-neutral (NFR8) sentence, e.g. *"A requirement with no delivering epic is a coverage gap; one that is deferred is a deliberate choice — the two look different so neither hides."* **Coordinate with Story 7.12** (`ready-for-dev`) which also adds a `ChartMetric.CodeFreshness` case to this same enum — if 7.12 lands first, add yours alongside; if this lands first, note the pending sibling. [Source: `Charts.cs:13-54`; memory `story-7-12-freshness-sunburst-seeded`]
  - [x] Render the matrix through `Charts.Framed(new ChartMeta(Title: "Traceability coverage", Window: null, Ranking: <covered-of-total caption>, Why: WhyText(ChartMetric.RequirementTraceability)), body)`. **No time window** (a coverage matrix has none — leave `Window` null; the frame omits the slot). The **ranking/summary caption** states the covered total, e.g. `"{covered} of {total} requirements have a delivering epic · {deferred} deferred · {unmapped} unmapped"` — sourced from `ProjectCounts.RequirementsOverall`, NOT a local recount (AC #2 / Story 8.3). [Source: `Charts.cs:82-100` `Framed`; `ProjectCounts.cs:118,217`]
  - [x] **Real-value legend (Story 10.2 AC1):** a 3-swatch legend (Covered / Deferred on purpose / Not yet mapped) each with its real count, colored via the same `--status-*` classes the cells use, so legend and cells can never disagree. This is chart-intrinsic — render it inside the builder (like the heatmap's own legend), not as a separate hand-rolled caption.

- [x] **Task 3 — Dedicated `traceability.html` page (AC: #1)**
  - [x] Add `SiteNav.TraceabilityOutputPath = "traceability.html"` and a new `TraceabilityTemplater.RenderPage(RequirementsModel, EpicsModel, ProgressModel, SiteNav, ProjectCounts)` mirroring the shell of a recent synthesized page — **`RiskQuadrantTemplater` (Story 7.10) is the freshest new-page precedent**: head open → nav bar → breadcrumb → `<main id="main-content">` → framed matrix → footer. Keep everything inside the single `<main id="main-content">` landmark. [Source: `src/SpecScribe/RiskQuadrantTemplater.cs` (mirror); `SiteGenerator.cs:2989-3015` `WriteRiskQuadrant`]
  - [x] Wire the write in `SiteGenerator`: a `WriteTraceability(nav)` that calls `WriteOutput(SiteNav.TraceabilityOutputPath, ApplyReferenceLinks(html, SiteNav.TraceabilityOutputPath))`, invoked from `GenerateAll` **after** `_requirements`/`_epicsModel`/`_counts` are assembled (near the requirements/sprint writes). `WriteOutput` auto-captures the page into `_spaCapture`, so the SPA + webview surfaces get it for free — **verify** it is not in any capture-exclusion path and add a coherence test (mirror the check Story 7.9 added for `code-map.html`). [Source: `SiteGenerator.cs:2575-2596` `WriteOutput`/`_spaCapture`; `SiteGenerator.cs:373,2996`]
  - [x] **Nav registration — Delivery group, shared `hasEpics` gate (no new flag).** Requirements and epics both come from `epics.md`, so gate the nav entry on the SAME `hasEpics` signal that already gates the "Requirements" entry (`SiteNav.Build`, [`SiteNav.cs:200-208`]) — add `delivery.Add(("Traceability", TraceabilityOutputPath))` + a `quickLinks` entry inside the existing `if (hasEpics)` block. This mirrors how Risk Quadrant reuses Code Map's gate ([`SiteNav.cs:231-238`]) so the page and its nav item can never dangle independently. Do **not** add a new `hasTraceability` bool.
  - [x] Confirm the Insights/Delivery local-context sub-header bar (`BuildNavLocalContextFor…`) still resolves for the new page if it participates in a group's local nav; if it doesn't need one, no action. [Source: `SiteNav.cs:411`]

- [x] **Task 4 — Compact previews on dashboard + requirements.html (AC: #1)**
  - [x] Add a compact **coverage strip** builder (`Charts.TraceabilityStrip(ProjectCounts.RequirementSatisfaction sat, string traceabilityHref)` or similar) — a single-row stacked bar or 3-chip summary (Covered / Deferred / Unmapped with counts, `--status-*` colored) plus a `View full traceability matrix →` link to `traceability.html`. Source counts from `RequirementsOverall` (single source). This is a teaser, **not** a second full matrix. Consider reusing `RequirementSatisfactionBar`/`RequirementSatisfactionChips` collapsed to 3 groups rather than inventing new markup. [Source: `RequirementsTemplater.cs:150-164`; `ProjectCounts.cs:37-57`]
  - [x] **requirements.html:** add the strip as a small additive section (a `section-divider` + the strip) — do NOT disturb the byte-identical FR flow/grid/donuts above it; place it near the existing "Non-functional & design coverage" section or the satisfaction band. [Source: `RequirementsTemplater.cs:56-116`]
  - [x] **dashboard:** add the strip as one compact panel in the insights/coverage region of the home page, gated on requirements existing (NFR8 — absent, not empty, when there are no requirements). Route through the section-view-model builder path (`DashboardViewBuilder` → adapter) so HTML/webview/SPA stay parity-identical — do NOT hand-append raw HTML in one adapter only. [Source: `DashboardViewBuilder.cs`; memory `story-6-2-section-view-models-live`; Story 8.1 cross-surface note in `8-3-…`]
  - [x] Both previews link to `traceability.html`; when the dedicated page is absent (no epics → no requirements → no page), the strip is absent too (shared gate).

- [x] **Task 5 — Tests (AC: #1, #2)**
  - [x] `ChartsTests`: `TraceabilityMatrix` renders a covered cell for a requirement whose `CoverageEpicNumbers` contains the column epic; a blank cell otherwise; a deferred requirement's row carries the deferred treatment/word; a fully-unmapped requirement's row carries "Not yet mapped"; covered-cell tooltip lists the covering epic's stories (rollup); the 3-state legend counts match `RequirementSatisfaction` mapping; column headers link to epic pages and row headers to requirement pages; **never color-only** (assert the state word/icon is present beside color).
  - [x] Degrade tests (AC #2): empty `Everything` → honest note, not an empty grid; all-unmapped → honest note; the summary caption reads `ProjectCounts.RequirementsOverall` and there is **no** independent requirement recount in the builder (assert the caption equals the ledger-derived numbers on a fixture where a naive recount would differ — or assert by construction the builder takes the satisfaction record, not the raw list, for its counts).
  - [x] `WhyText`: `RequirementTraceability` returns a non-empty, framework-neutral sentence (no repo/BMAD-specific string).
  - [x] Page + wiring: a `SiteGenerator*Tests` fixture with epics + requirements writes `traceability.html`, it appears in the Delivery nav, its cells deep-link correctly; a fixture with **no** epics writes **no** `traceability.html` and shows no nav entry (shared gate). SPA/webview coherence: `traceability.html` is captured (present in `_spaCapture`/the consolidated SPA output) — mirror Story 7.9's `code-map.html` coherence assertion.
  - [x] Golden + parity: `traceability.html` is new and the dashboard/requirements strips change committed pages → **regenerate the golden fingerprint** and eyeball the diff (new page + strip only). Keep `RenderParity`/SPA/webview suites green — the matrix is shared `Charts` output, identical across surfaces (no new `HostRenderException`). [memory `golden-diff-normalization-gotchas`]

- [x] **Task 6 — Verify end-to-end on this repo (AC: #1, #2)**
  - [x] `dotnet run` a full generate to `SpecScribeOutput/` (the default — never `--output docs/live`). Open `traceability.html`: the matrix shows FR/NFR/UX-DR rows × epic columns, covered cells filled + tooltips listing the covering epic's stories, deferred rows greyed, unmapped rows tan-flagged; the framed legend + why sentence + covered-of-total caption present. Open `requirements.html` and `index.html`: the compact coverage strips show and link to the full matrix. Confirm no page scrolls horizontally (grid scrolls inside its own container). Confirm the same page renders in `specscribe webview` and `--spa`. [memory `generate-output-dir-is-specscribeoutput`]
  - [x] Sanity-check the numbers against `requirements.html`'s existing satisfaction band — the strip's Covered/Deferred/Unmapped must AGREE with the band (same `RequirementsOverall` source), never a competing count.

## Dev Notes

### What exists today (read before touching)

- **`Charts.RequirementFlow`** ([`Charts.cs:2805`]) — the Sankey over FR+NFR that already partitions requirements by covering epic. Its private helpers (`CoverageKeys`, `ForCoverageKey`, `EpicTitlesByNumber`, `NoCoverage`, `NoCoverageKey`) are the ready-made epic-axis plumbing — **reuse them** (extend visibility to `Everything` scope if needed). Note the Sankey is FR+NFR-scoped (`reqs.All`); the matrix is `Everything`-scoped (FR+NFR+UX-DR) — a deliberate difference, since the matrix is the stakeholder coverage view where UX-DRs belong.
- **`Charts.RequirementStatusGrid`** ([`Charts.cs:2696`]) — the HTML (not SVG) status-block grid; the closest existing markup sibling to the matrix (rich `js-tip`/`data-tip` tooltip, deep-link, `--status-*` class, icon+id). Model the matrix cells on this.
- **`Charts.Framed` / `ChartMeta` / `WhyText`** ([`Charts.cs:13-100`]) — the mandatory Story 10.2 frame. The matrix MUST render through it (Title/Ranking/Why; Window omitted).
- **`ProjectCounts.RequirementsOverall`** (a `RequirementSatisfaction`, [`ProjectCounts.cs:24-77,118,217`]) — the single source for requirement-satisfaction counts. `SatisfactionChipTiers` already groups Satisfied/In-flight/Deferred/Unmapped; collapse Satisfied+In-flight → Covered.
- **`RequirementsParser.StoriesFor(req, epics)`** ([`RequirementsParser.cs:305`]) — the covering-story rollup source (epic-level, best-effort, never throws). Filter its output to a single epic's `Stories` for a per-cell rollup.
- **`StatusStyles`** — `ForRequirement`/`RequirementLabel`/`RequirementBadge`/`Badge`/`StoryLabel`/`ForEpic`/`EpicLabel` ([`StatusStyles.cs:161-352`]). The single stage→css/word/badge source; route every state through it. Never hard-code a status color.
- **`RiskQuadrantTemplater` + `WriteRiskQuadrant`** ([`RiskQuadrantTemplater.cs`; `SiteGenerator.cs:2989-3015`]) — the freshest "new synthesized insights page" precedent (page shell, `WriteOutput`, shared nav gate). Mirror its structure for `TraceabilityTemplater`/`WriteTraceability`.

### Guardrails & invariants (must follow)

- **Single-source counts (AC #2, Story 8.3).** Headline/legend/caption numbers come from `ProjectCounts.RequirementsOverall` — no `.Count(...)` over requirements at the render site for those. The matrix may iterate `Everything` to place cells (that's structure, not a competing count), but the *summary numbers* are ledger-sourced. Requirements are explicitly the `RequirementsModel`/`ProjectCounts` single source — do NOT fold them into a new count path. [Source: `8-3-…md` scope note; `9-9`/`ProjectCounts.cs`]
- **Epic-level honesty (cardinal rule).** "Covered" = a delivering epic exists, never a per-requirement or per-story completion claim; the story rollup is "stories in the covering epic," not "stories implementing this requirement." Frame all copy that way. [memory — Story 1.5 truthfulness]
- **Status is single-source.** Every state→color routes through `StatusStyles` + the six `--status-*` tokens. The 3-state collapse maps onto existing tokens (covered / `deferred` / `pending`), it does NOT introduce a 7th token or a raw hex. [memory `specscribe-status-token-system`]
- **Never color-only (UX-DR17).** Every cell/legend/row state pairs color with an icon and/or word.
- **Pure SVG/HTML + links, no info-bearing JS.** The matrix is generation-time HTML/CSS; the one sanctioned tooltip/copy script is not extended. Rich tooltips route through the shared `js-tip`/`data-tip` body-level node (never CSS `::after` inside an overflow container). [memory `charting-is-pure-svg-no-js`, `tooltip-clipping-use-ss-tooltip-node`]
- **No horizontal page scroll.** The ~22-column grid lives in its own `overflow-x:auto` container; the page body never scrolls sideways.
- **Deterministic (NFR8 / FR31 / CI reproducibility).** No dictionary-iteration-order dependence, no timestamps, no per-visitor state. Columns ordered by ascending epic number; rows in `Everything` source order. A from-scratch regen is byte-identical. Invariant formatting (`Charts.DReadable`/invariant `N()`) if any number is composed.
- **Degrade, don't break (NFR8).** Empty/all-unmapped → honest note. Missing epics for a covering number, an epic with no stories, a requirement with a phantom epic number — all skip/empty, never throw (mirror `StoriesFor` best-effort + the `chart-empty` guards).
- **Section-view-model discipline (Story 6.2).** The dashboard strip goes through `DashboardViewBuilder` → adapter so HTML/webview/SPA stay parity-identical; don't hand-append raw HTML in one adapter. The dedicated page and requirements strip are shared `Charts`/templater output (same across surfaces). [memory `story-6-2-section-view-models-live`]
- **Accessibility.** Semantic table (`caption`, `th scope`) or an sr-only text twin; everything inside `<main id="main-content">`; new interactive elements keyboard-reachable + focus-ring (Story 1.4). [memory `story-1-4-a11y-seams-for-1-5`]

### Project Structure Notes

- Primary code: `src/SpecScribe/Charts.cs` (new `TraceabilityMatrix` + `TraceabilityStrip` + `ChartMetric.RequirementTraceability` + `WhyText` case), new `src/SpecScribe/TraceabilityTemplater.cs`, `src/SpecScribe/SiteNav.cs` (new output-path const + Delivery nav entry), `src/SpecScribe/SiteGenerator.cs` (`WriteTraceability` + call site), `src/SpecScribe/DashboardViewBuilder.cs` (+ its view model) for the dashboard strip, `src/SpecScribe/RequirementsTemplater.cs` (requirements strip), `src/SpecScribe/assets/specscribe.css` (matrix + strip styles, `--status-*` tokens only). Tests in `tests/SpecScribe.Tests/`.
- Output dir is `SpecScribeOutput/` by default when you generate to verify — never `docs/live` (vestigial/gitignored). [memory `generate-output-dir-is-specscribeoutput`]
- If any new CSS is needed, add it to `src/SpecScribe/assets/specscribe.css` using `--status-*` tokens; `StylesheetTests` guards the stylesheet. **Watch the CSS-comment `*/` truncation gotcha** — never write `--status-*/…` (no space) in a comment; it silently closes the comment early. [memory `css-comment-star-slash-silent-truncation`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` over generated HTML strings — the established `ChartsTests`/`RequirementsAndProgressTests`/`SiteGeneratorTraceabilityTests` pattern. `TraceabilityMatrix`/`TraceabilityStrip` are pure functions over model records — unit-test them directly against strings. Generation-level tests build a temp `_bmad-output` and assert on emitted HTML + `GenerateAll` outcomes (`AssertNoErrors`).
- Drive the legend counts and the cell-state classification from the SAME helper/source in the test so a future change can't desync legend and cells.
- Regenerate `GoldenContentFingerprint` deliberately (new page + strips) and confirm the diff is only this story's surfaces. Confirm the baseline is green before starting so you don't inherit an unrelated red. [memory `golden-diff-normalization-gotchas`]

### Out of scope (do not build)

- **No new requirement/coverage data model, no new authoring schema.** Pure rendering over `RequirementsModel`/`ProjectCounts`/`StoriesFor`.
- **No per-story columns**, no per-requirement→story precision mapping (over-claims; owner rejected).
- **No delivery-cadence / cycle-time** (Story 21.2) and **no planning↔code impact map** (Story 21.3) — sibling stories, separate surfaces.
- **No changes to the six-tier requirement status vocabulary, the FR flow/grid/donuts, or the `ProjectCounts` ledger.** The matrix consumes them; it does not alter them.
- **No client-side interactivity** (zoom/drill) — that's Epic 20's territory; this stays static generation-time HTML/CSS.
- **No PRD edit** (FR39 PRD sync is deferred "when convenient" per SCP 2026-07-19).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:3105-3129`] — Epic 21 intent + Story 21.1 user story & ACs; FR39; NFR8; FR31.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md:46,59,69`] — Epic 21 seating; 21.1 has no hard dependency, schedulable independently.
- [Source: `_bmad-output/implementation-artifacts/9-2-nfr-and-ux-dr-coverage-maps.md`] — the coverage data model (`Everything`, `CoverageEpicNumbers`, epic-header reverse index, epic-level honesty caveat).
- [Source: `_bmad-output/implementation-artifacts/10-2-chart-metadata-standard.md`] — the `Charts.Framed`/`ChartMeta`/`WhyText`/`ChartMetric` frame the matrix must use.
- [Source: `_bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md`] — the `ProjectCounts` ledger + "requirements are their own single source" scope note (AC #2).
- [Source: `src/SpecScribe/Charts.cs:13-100,2696-2849`] — `ChartMetric`/`Framed`; `RequirementStatusGrid` (HTML cell sibling); `RequirementFlow` + epic-axis helpers to reuse.
- [Source: `src/SpecScribe/ProjectCounts.cs:24-77,118,217`] — `RequirementSatisfaction` (single-source summary counts, `SatisfactionChipTiers`).
- [Source: `src/SpecScribe/RequirementsParser.cs:305-312`] — `StoriesFor` (covering-story rollup).
- [Source: `src/SpecScribe/RequirementsModel.cs:22,88`] — `RequirementStatus` six tiers; `Everything`.
- [Source: `src/SpecScribe/StatusStyles.cs:161-352`] — canonical requirement/story/epic class + label + badge.
- [Source: `src/SpecScribe/SiteNav.cs:50,200-238,411`] — output-path const pattern; the `hasEpics` Delivery gate; Risk-Quadrant shared-gate precedent.
- [Source: `src/SpecScribe/SiteGenerator.cs:2575-2596,2989-3015`] — `WriteOutput`/`_spaCapture` auto-capture; `WriteRiskQuadrant` new-page precedent.
- [Source: `src/SpecScribe/RiskQuadrantTemplater.cs`] — the page-shell template to mirror.
- [Source: `src/SpecScribe/RequirementsTemplater.cs:56-164`] — requirements-page section structure + satisfaction band the strip sits near / agrees with.

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no failing runs requiring a debug log; `dotnet build`/`dotnet test` green throughout, `dotnet run -- generate` verified live against this repo (391 pages, 1 skipped, no errors).

### Completion Notes List

- Task 1: Added `Charts.TraceabilityMatrix(RequirementsModel, EpicsModel, string prefix)` — a real HTML `<table>` (row = `reqs.Everything`, columns = the ordered set of covering-epic numbers that actually resolve to a real epic, reusing the private `CoverageKeys`/`EpicTitlesByNumber` helpers `RequirementFlow` already had). Covered cells carry an icon + sr-only text + a rich tooltip naming the covering epic's own stories (capped at 8, "+N more" beyond that); non-covering cells are blank/neutral. Deferred/fully-unmapped rows carry a `StatusStyles.Badge` next to the row header. Degrades to a `chart-empty` honest note when `Everything` is empty or no requirement names a resolvable covering epic. The table itself is the text equivalent (caption + `th scope`) — no separate sr-only list needed.
- Task 2: Added `ChartMetric.RequirementTraceability` + its `WhyText` case (Story 7.12 never added a sibling enum case — its work shipped through `Charts.CodeMapSunburst` instead, so no coordination collision). Added `Charts.TraceabilityLegend`/`TraceabilityChips` (a private shared 3-chip renderer) so the dedicated page's legend and the teaser strip can never disagree — both derive from `ProjectCounts.RequirementSatisfaction`, never a local recount.
- Task 3: Added `TraceabilityTemplater.RenderPage` (mirrors `RiskQuadrantTemplater`'s page shell) + `SiteNav.TraceabilityOutputPath` + a Delivery nav entry inside the existing `hasEpics` block (no new flag) + `SiteGenerator.WriteTraceability`, called from `GenerateAll` right after `_counts` is built (needs the ledger for its ranking caption), mirroring `WriteRiskQuadrant`'s call-site pattern. Not wired into watch-mode `RegenerateEpics` — matches the existing precedent that Sprint/Code Map/Risk Quadrant are also full-run-only pages, not refreshed on a scoped epics re-ingest.
- Task 4: Added `Charts.TraceabilityStrip` (the same 3 chips + a link) wired into `RequirementsTemplater.RenderIndex` (additive section near the satisfaction band, FR flow/grid/donuts untouched) and into `HtmlRenderAdapter.Dashboard.cs`'s `AppendTraceabilityPanel` (called from the one shared `HtmlRenderAdapter` — Story 6.1's sole concrete render adapter — so HTML/webview/SPA stay parity-identical with no surface-specific hand-appended HTML).
- Task 5: New `ChartsTraceabilityTests.cs` (19 unit tests: cells, row states, links/prefix, table semantics, degrade, `WhyText`, legend/strip agreement, never-color-only) and `SiteGeneratorTraceabilityMatrixTests.cs` (8 generation-level tests: page+frame, nav entry, no-epics omission, degrade-but-still-writes, no broken links, both teaser strips, strip/band agreement). Added a `CapturePages_IncludesTraceabilityAsACapturedSurface` coherence test to `SiteGeneratorWebviewTests.cs` mirroring Story 7.9's `code-map.html` check. Fixed the pre-existing test churn the new nav entry/icon caused (`SiteNavTests`, `RenderParityTests`, `SiteGeneratorAdapterTests`' inventory + golden fingerprint) and added a new `Icons.ForConcept("Traceability")` glyph (a 3×3 grid) since every emitted nav label needs one (`IconsTests.ForConcept_EveryEmittedLabelHasAGlyph` caught the gap). Golden fingerprint regenerated and confirmed stable across repeated runs (see `golden-diff-normalization-gotchas`). Full suite: 2043 passed, 3 skipped (pre-existing symlink tests), 0 failed on the final run (one transient wall-clock-boundary flake in an unrelated pre-existing determinism test, confirmed to pass in isolation, not caused by this story).
- Task 6: Verified live via `dotnet run -- generate` against this repo (SpecScribeOutput/, never `docs/live`): `traceability.html` shows 75 of 76 requirements covered (1 unmapped — NFR3), the framed legend/why sentence/ranking caption render, covered cells are the green `--status-done` swatch with sticky row-header/corner cells and a horizontally-scrolling body (`document.body.scrollWidth <= window.innerWidth` confirmed — no page-level horizontal scroll). `requirements.html` and `index.html` both show the strip with **identical** Covered/Deferred/Not-yet-mapped counts (75/0/1) agreeing with the satisfaction band — no competing count. Also generated with `--spa` and confirmed `traceability.html` is captured in the SPA JSON bundle (`pages-root-3.json`, `manifest.json`). Generated output directories removed after verification (gitignored, not committed).

### File List

- `src/SpecScribe/Charts.cs` — `ChartMetric.RequirementTraceability` + `WhyText` case; `TraceabilityMatrix` (incl. row-header tooltip), `CoveredCellTip`, `TraceabilityChips`, `TraceabilityLegend`, `TraceabilityStrip`
- `src/SpecScribe/TraceabilityTemplater.cs` — new; `RenderPage`
- `src/SpecScribe/SiteNav.cs` — `TraceabilityOutputPath` const + Delivery nav entry inside the `hasEpics` block; `BuildDeliveryLocalContext`
- `src/SpecScribe/SiteGenerator.cs` — `WriteTraceability` + call site in `GenerateAll`
- `src/SpecScribe/RequirementsTemplater.cs` — `AppendTraceabilityStripSection` + call site in `RenderIndex`; `RenderNavBar` now passes `BuildDeliveryLocalContext`
- `src/SpecScribe/EpicsTemplater.cs` — `BuildIndexPage`'s `Nav` now passes `BuildDeliveryLocalContext`
- `src/SpecScribe/SprintTemplater.cs` — `RenderNavBar` now passes `BuildDeliveryLocalContext`
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — `AppendTraceabilityPanel` + call site in `AppendDashboardSection`
- `src/SpecScribe/Icons.cs` — `Icons.ForConcept("Traceability")` glyph
- `src/SpecScribe/assets/specscribe.css` — matrix/legend/strip styles (`.trace-*`), `--status-*` tokens only
- `tests/SpecScribe.Tests/ChartsTraceabilityTests.cs` — new
- `tests/SpecScribe.Tests/SiteGeneratorTraceabilityMatrixTests.cs` — new
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — new SPA/webview coherence test
- `tests/SpecScribe.Tests/SiteNavTests.cs` — updated expected nav-item lists (new "Traceability" entry); new `BuildDeliveryLocalContext_*` tests
- `tests/SpecScribe.Tests/RenderParityTests.cs` — updated expected nav-item lists
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — updated golden output inventory + regenerated golden content fingerprint (twice)

### Post-review owner feedback (same session)

- Added a rich `js-tip`/`data-tip` tooltip to each row header (requirement id + kind + text snippet), mirroring `RequirementStatusGrid`'s tooltip pattern, so hovering a bare "FR1" reveals what it actually is.
- Made `.trace-matrix-wrap` scroll vertically too (`max-height: calc(100vh - 16rem); overflow-y: auto`, alongside the existing `overflow-x: auto`) so the sticky `<thead>` column headers stay visible while paging through a long requirement list, instead of only working within a viewport-height page scroll.
- Confirmed the row-header (requirement id) column was already frozen via `.trace-row-head { position: sticky; left: 0 }` — verified live that it still holds during simultaneous horizontal + vertical scroll after the above change.
- New `SiteNav.BuildDeliveryLocalContext` (mirrors `BuildInsightsLocalContext`): the white sub-header band on the four Delivery-group index pages (`epics.html`, `requirements.html`, `traceability.html`, `sprint.html`) previously fell back to the generic full quick-links band — a near-duplicate of the dark nav bar's own Delivery/Insights/Follow-ups/Project/Help links sitting right below it. Wired into all four templaters' nav-bar call sites so the white band now shows just the Delivery family (Epics/Requirements/Traceability/Sprint) with the current page marked active, matching the existing Insights-page precedent.
- New tests: `SiteNavTests.BuildDeliveryLocalContext_*` (2), `ChartsTraceabilityTests.TraceabilityMatrix_RowHeader_CarriesARichTooltipNamingTheRequirement`. Golden fingerprint regenerated again (nav-bar markup shift on every page) and reconfirmed stable across repeated runs. Full suite: 2049 passed / 3 skipped (pre-existing). Verified live: row tooltip text, `overflow-y:auto`/`max-height` computed styles, sticky-row-head-during-scroll, and the Delivery white band on both `traceability.html` and `epics.html`.

### Post-deploy bug fix: phantom page scroll past the footer (same session)

- Owner reported the deployed `traceability.html` could be scrolled well past the footer into blank parchment space — a regression from the vertical-scroll change above.
- Diagnosed live against the actual deployed page (`https://integerman.github.io/SpecScribe/traceability.html`), not a local repro: used `javascript_tool` to compare `document.documentElement.scrollHeight` (2990px) against `document.body.scrollHeight` (959px, the CORRECT value matching the visible footer) — confirming the root `<html>` element had ~2031px of phantom scrollable space `<body>` didn't have.
- Bisected the cause by toggling CSS properties live via inline styles: ruled out `position: sticky` (removing it from all 97 sticky cells didn't change anything), ruled out `overflow-x`, ruled out a `min-height: 100vh` on `<body>`. Isolated it to `.trace-matrix-wrap`'s `contain: inline-size` (inherited from the shared `.table-scroll` class) — with a genuinely bounded (max-height + overflow-y) descendant inside it, that containment value lets the browser leak the descendant's full UNCLIPPED content height (~2680px) into the root element's scrollable-overflow region. Confirmed the fix live: adding `layout` to the containment (`contain: layout inline-size`) made `documentElement.scrollHeight` and `body.scrollHeight` match exactly (959px), while horizontal scroll, the frozen row-head column, and the sticky column headers all kept working (reverified with scrollTop/scrollLeft assertions on the live page).
- Applied the fix as `.table-scroll.trace-matrix-wrap { contain: layout inline-size; }` — a two-class selector, needed because `.table-scroll`'s own `contain: inline-size` rule appears later in the stylesheet and would otherwise win the cascade tie (same specificity) by source order and silently drop a same-specificity single-class override.
- Regenerated the golden fingerprint on top of a concurrent session's Story 21.2 (Delivery Cadence) changes that landed in the same file mid-session; verified stable across repeated runs. Full suite: 2080 passed / 3 skipped (one unrelated pre-existing wall-clock-boundary flake, confirmed passing in isolation — same category as an earlier flake, not caused by this change).

## Review Findings

_Code review 2026-07-22 (3 parallel adversarial layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor; scoped to 21.1's traceability symbols only — sibling stories 21.2/21.3/19.2 in the same commit range excluded). 3 patch (1 from a resolved decision-needed), 1 defer, 3 dismissed._

- [x] [Review][Patch] Phantom-covered requirement renders as a silent blank row (medium) [src/SpecScribe/Charts.cs:4228] — FIXED: row now classified via `resolvable = CoverageEpicNumbers.Any(epicsByNumber.ContainsKey)` (all-phantom → `unmapped` row) + a caution badge (`Icons.Caution()` + "Coverage dangling" + tooltip naming the dangling epic; reuses the tan `pending` swatch, no 7th token). Test `TraceabilityMatrix_PhantomCoveredRow_IsFlaggedAsDanglingNotSilentlyBlank` added. — A requirement whose `CoverageEpicNumbers` resolve to NO real epic (e.g. the story's own `FR3: Epic 99` phantom fixture) is classified `Planned` by `DeriveStatus` ([RequirementsParser.cs:350](src/SpecScribe/RequirementsParser.cs:350)) → ledger rolls it into `InFlight`→`covered` ([ProjectCounts.cs:35](src/SpecScribe/ProjectCounts.cs:35)), so the caption/legend/strips count it as "has a delivering epic." But `TraceabilityMatrix` classifies the row off `req.CoverageEpicNumbers.Count`: Count>0 → no badge, yet no `epicKeys` column resolves → every cell blank. The row is silent and unexplained. **Owner decision (2026-07-22): option 2 — flag it.** Add a simple caution icon + tooltip to any row whose covering epics all fail to resolve (`CoverageEpicNumbers.Count > 0 && !CoverageEpicNumbers.Any(epicsByNumber.ContainsKey)`), tooltip e.g. "Names Epic N, which no longer resolves — coverage is dangling." Additive presentation only (no ledger/six-tier vocab change, per out-of-scope): the row now explains itself instead of reading as an empty gap. Add a test asserting the phantom fixture's row carries the caution marker.

- [x] [Review][Patch] Dashboard traceability strip bypasses the `DashboardViewBuilder` section-view-model path (low) [src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:405] — FIXED: added `DashboardView.TraceabilityStripHtml`, built in `DashboardViewBuilder` (mirrors `CadenceStripHtml`); `AppendTraceabilityPanel` now consumes the view fragment instead of computing in-adapter. Byte-identical (golden fingerprint unchanged). — Task 4 / the Story 6.2 guardrail say route the dashboard strip through `DashboardViewBuilder` → a view-model field, "do NOT hand-append raw HTML in one adapter only." `AppendTraceabilityPanel` instead calls `Charts.TraceabilityStrip(...)` in-adapter; `DashboardViewBuilder` has no traceability field. The sibling 21.2 cadence strip in the same diff DID comply (`view.CadenceStripHtml`, [DashboardViewBuilder.cs:85](src/SpecScribe/DashboardViewBuilder.cs:85)) — so this is an internal inconsistency. Consumer impact nil (single concrete adapter → bytes identical across HTML/webview/SPA); it's a consistency/mechanism fix: add a `TraceabilityStripHtml` view-model field mirroring cadence.

- [x] [Review][Patch] All-deferred / all-phantom degrade note mislabels deferred as unmapped (low) [src/SpecScribe/Charts.cs:4200] — FIXED: the empty-note branch now reads "Every requirement is deferred on purpose — none is currently tied to a delivering epic." when every requirement is deferred, keeping the generic "Coverage not yet mapped" wording only for genuine unmapped gaps. Two tests added/strengthened. — When `epicKeys.Count == 0` because every requirement is deliberately Deferred (or all coverage is phantom), `TraceabilityMatrix` returns "Coverage not yet mapped — no requirement is yet tied to a delivering epic," conflating deliberate deferral with an unmapped gap — the exact distinction the matrix exists to preserve. Extreme edge (not in this repo). Fix: branch the empty note on `sat.Deferred`/`sat.Unmapped` so an all-deferred project reads as deferred-on-purpose, not "not yet mapped."

- [x] [Review][Defer] `.trace-matrix-wrap { max-height: calc(100vh - 16rem) }` hard-codes assumed chrome height [src/SpecScribe/assets/specscribe.css] — deferred, low-value responsive polish. The vertical-scroll cap assumes ~16rem of header/legend/caption above the grid; on a short/narrow viewport where the caption+legend wrap, usable matrix height can collapse. Owner already iterated this area live (the post-deploy `contain` fix) and verified current viewports; no clearly-correct replacement constant. Revisit on a future responsive pass.

_Dismissed (not persisted): (1) duplicate epic number throwing in `epics.Epics.ToDictionary(e => e.Number)` — `RequirementsParser.cs:55/307` already does the same non-defensive lookup upstream, so duplicate numbers blow up the requirements parse long before traceability renders; not novel, not reachable in isolation. (2) requirements.html showing the 4-chip satisfaction band and 3-chip traceability strip back-to-back — owner explicitly located the strip near the satisfaction band (design direction #1 / Task 4), and the strip adds the "→ view full matrix" CTA the band lacks. (3) mixed href-escaping (header hrefs interpolate `prefix`/`req.Slug` raw while strip/cell hrefs use `Html()`) — not exploitable (`epicNumber` is int, slugs are pre-sanitized) and consistent with the repo-wide slug-in-href convention._

## Change Log

- 2026-07-22: Implemented the requirement traceability coverage matrix (dedicated `traceability.html` + dashboard/requirements.html teaser strips) per the story's owner-elicited design directions. All 6 tasks complete; full test suite green (2043 passed / 3 skipped, unrelated pre-existing skips); verified live against this repo. Status → review.
- 2026-07-22 (same session, owner feedback): Added row-header requirement tooltips, made the matrix scroll vertically (not just horizontally) with the epic-column header staying pinned, confirmed the requirement-id column stays frozen through both scroll axes, and replaced the generic repeated-nav white band on all four Delivery-group pages with a proper `BuildDeliveryLocalContext`. Golden fingerprint regenerated again; full suite green (2049 passed / 3 skipped).
- 2026-07-22 (same session, owner-reported bug after deploy): Fixed a real browser bug the vertical-scroll change above introduced — the deployed `traceability.html` let the page scroll far past the footer into blank space. Reproduced and diagnosed live against the deployed site (`github.io/SpecScribe/traceability.html`) via direct DOM/computed-style inspection: `.trace-matrix-wrap`'s bounded `max-height`/`overflow-y:auto` scrollport, combined with the shared `.table-scroll` class's `contain: inline-size`, made the browser leak the table's full unclipped content height into the root `<html>` element's own scrollable-overflow region (`documentElement.scrollHeight` tracked the table's ~2680px unclipped height instead of the page's real ~960px). Bisected the exact cause live (toggling `contain` values in the browser) and fixed with `.table-scroll.trace-matrix-wrap { contain: layout inline-size; }` — the two-class selector is required to out-specificity `.table-scroll`'s own later same-property rule. CSS-only fix; row/column freezing and the internal scrollport all reverified working after the fix (both locally and on the live deployed page). Golden fingerprint regenerated on top of a concurrent session's Story 21.2 changes; full suite green (2080 passed / 3 skipped, one unrelated pre-existing wall-clock-boundary flake confirmed passing in isolation).
