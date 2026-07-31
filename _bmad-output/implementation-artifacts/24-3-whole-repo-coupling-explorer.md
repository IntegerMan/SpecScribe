---
baseline_commit: 70b72ab9fda25da8b4b469baa964b7cf51eb6eea
---

# Story 24.3: Whole-Repo Coupling Explorer (Force-Directed Galaxy) — Dedicated Page

Status: blocked

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## ⛔ Gate — ONE open gate, and it is in flight right now

**Story 24.2 is `in-progress` in another session as of 2026-07-30** (`dev-story 24.2`, baseline `94b8e56`, executing at HEAD `70b72ab`). This story consumes **two files 24.2 creates and neither exists yet** — verified by `ls` at `70b72ab`:

| Symbol 24.3 reuses | Created by | Exists at `70b72ab`? |
|---|---|---|
| `src/SpecScribe/CouplingLayout.cs` — the deterministic generation-time solver | Story 24.2 Task 2 | ❌ **No** |
| `src/SpecScribe/RelationshipGraph.cs` — the graph component (skeleton, island, legend, twin, `ContainsHost`) | Story 24.2 Task 3 | ❌ **No** |
| the `specscribe.js` graph renderer + `plotly_afterplot` a11y layer | Story 24.2 Task 4 | ❌ **No** |
| the `AssetManifest` Plotly flag for the graph family | Story 24.2 Task 8 | ❌ **No** |

Building any of them here would be the exact reinvention Epic 24 exists to prevent. **Do not `dev-story` this key until 24.2 reaches `review`.** The moment it does, flip to `ready-for-dev` — nothing else gates this story.

**When you do start: grep-verify every one of those four before writing a line against them** (CLAUDE.md § Concurrent work; [[shared-main-concurrent-edit-loss-verify-after-edit]]). 24.2 may have renamed a symbol during its own verify round. This story names the shapes 24.2's task list specifies; the *shipped* shape is the authority.

## Story

As a tech lead assessing architectural entanglement,
I want a dedicated page showing the whole repository's co-change network,
so that I can see the project's hidden coupling structure and its worst cross-boundary offenders in one explorable map.

## Acceptance Criteria

1. **Given** deep-git coupling data and JavaScript available
   **When** the whole-repo explorer renders on its own page
   **Then** it draws the repo's file co-change network (node = file sized by change frequency, edge = coupling weighted/styled by the Story 24.1 confidence, cross-boundary couples emphasized **by dash pattern, width band, node shape and accessible text — never by hue**, ADR 0030 §5), with interactive pan/zoom, hover/focus detail, node → code-page navigation, and a **support/confidence threshold** clutter control that **hides and never re-lays-out** (ADR 0030 §4)
   **And** it carries a Story 10.2 legend + analysis window + framing sentence via `Charts.Framed` / `Charts.WhyText(ChartMetric.ChangeCoupling)`, and is reachable from the Insights nav (FR27) on the deep-git gate.

   > **AC #1 amendment, owner-approved at create-story 2026-07-30 (D4).** The epic's original wording paired the threshold with **"directory grouping/collapse"**. Collapsing files into directory nodes is a genuinely **different node set**, and ADR 0030 §4 ratified that filters *hide, never re-lay-out* — so a collapse control needs a **second precomputed layout**, not a filter. The owner **deferred collapse to a follow-up**. This story ships the threshold control only, and **AC #1 therefore ships partially satisfied against the epic's literal wording** — recorded here rather than quietly narrowed, and a `deferred-work.md` entry is a gating subtask (Task 8).

2. **Given** a large repository or a JavaScript-off visitor (NFR8, performance)
   **When** the full network would be too dense or cannot hydrate
   **Then** the view stays legible via the threshold control and the Code/Process lens, and **degrades to the server-rendered text twin** — a complete, navigable, non-color coupled-pairs enumeration of the Story 24.1 data
   **And** generation stays within the deep-git performance envelope and remains generation-time deterministic (FR31) — byte-identical output on a from-scratch CI regen, verified **across separate processes**, not by in-process repetition.

   > **AC #2 amendment, owner-approved at create-story 2026-07-30.** The epic's original wording said "degrades to a **static, bounded SVG summary plus** a readable coupled-pairs table". [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) §1/§4 supersedes that: the text twin **is** the no-JS contract, and ADR 0010 §2's "a useful chart must render with JS off" no longer holds. Shipping an SVG *and* the interactive graph is the dual-renderer option ADR 0013's options table explicitly rejected — the same amendment Story 24.2's AC #2 took.

3. **Given** `deep-analytics.html` already renders a whole-repo change-coupling node-link graph today (`Charts.CouplingGraph`, a static ring-layout SVG, with `Charts.CouplingTable` beneath it) and owner decision **D1** puts the explorer on its own page
   **When** `Charts.CouplingGraph` is retired from `deep-analytics.html`
   **Then** that surface's server-rendered text twin has first been **audited complete** against ADR 0013 §2's four properties — server-rendered · complete · navigable · non-color — **in a live browser with JavaScript disabled**, not by test assertion alone
   **And** the golden-fingerprint replacement required by ADR 0013 §6 covers the new page: assertions land on the **embedded payload, the component configuration, and the twin**, never on SVG path geometry.

4. **Given** owner decision **D2** — the default threshold is **adaptive**, and overridable
   **When** the explorer is generated
   **Then** the support floor is **auto-tuned at generation time** by raising it from `GitMetrics.CouplingMinSupport` until the solved graph fits a declared node **and** edge budget, and that choice is **overridable from the CLI and the `.specscribe` config file** with `--show-config` provenance like every other resolved setting
   **And** the framing sentence **states the floor that was chosen and what it hid** ("showing couples sharing ≥ N commits — raised from 2 to keep the map legible; M pairs below that are in the table"), so a reader never infers the repository is exactly this entangled — the spike's explicit hand-off.

## Owner decisions taken at create-story (2026-07-30)

Elicited up front per CLAUDE.md § Story lifecycle step 1, so the verify round does not spend a round on them. **Do not re-litigate these in dev-story; implement them.**

| # | Decision | Consequence for implementation |
|---|---|---|
| **D1 — Placement** | **New page `coupling-explorer.html`**, its own Insights nav entry on the deep-git gate. **`Charts.CouplingGraph` is RETIRED** from `deep-analytics.html`; its `Charts.CouplingTable` **stays there** and the two pages cross-link. | Exactly **one** whole-repo coupling renderer survives — the same SUPERSEDE call 24.2 took for `ReferenceGraph`, and the anti-pattern ADR 0013 rejected. "Supersede in place on deep-analytics.html" and "new page + keep the static graph" were both offered and **not** chosen. Cost: a `SiteNav` const, a gate signal, a page write, breadcrumb/IR route, tests, and the AC #3 twin audit. |
| **D2 — Default threshold** | **Adaptive auto-tune**, plus a **CLI flag and `.specscribe` setting** to override. | The floor is a *computed* default, not a constant. See "The adaptive floor" in Dev Notes for the exact algorithm, the budgets, and the ⚠️ **explorer-scoped** rule. Fixed "support ≥ 5 + Code-only" and "support ≥ 5, lens off" were offered and not chosen. |
| **D3 — Silhouette** | **All three offered silhouettes ship as a user toggle**: free force-directed galaxy · directory-grouped constellation · ring-of-modules. | **Three coordinate sets over ONE shared node/edge payload** — the marginal payload is ~2 coordinate arrays, not 3× the page. One selector (mirroring `HierarchyExplorer`'s sunburst\|treemap shape selector, ADR 0012 §2 "one selector idiom"), **not three controls**. ⚠️ The ring option overlaps Story 24.4 — see "The ring silhouette collides with 24.4" below. |
| **D4 — Directory collapse** | **Deferred to a follow-up.** Threshold control only. | AC #1 ships partially satisfied against the epic's literal wording; that is stated in AC #1 and a `deferred-work.md` entry is gating subtask **Task 8**. "Two precomputed layouts" and "grouping as emphasis only" were offered and not chosen. |

## Tasks / Subtasks

- [ ] **Task 1 — ⛔ GATE CHECK, before anything else** (AC: all)
  - [ ] `ls src/SpecScribe/CouplingLayout.cs src/SpecScribe/RelationshipGraph.cs` and confirm both exist. If not, **stop** — 24.2 has not landed. Report and halt.
  - [ ] Read **both files in full** before designing anything. Their shipped API is the authority, not this story's description of it.
  - [ ] Grep for the `specscribe.js` graph renderer, its mount registry entry, and the `AssetManifest` graph flag. Confirm each exists and note its actual name.
  - [ ] Re-read 24.2's **Completion Notes / File List** — it made at least three decisions this story inherits: `BuildAside`'s citations-only path, the coordinate/confidence rounding precision, and whether the width bands encode confidence or **lift** (24.1's open Q4). **Match whatever 24.2 shipped; do not diverge.**
  - [ ] Re-run the analysis digest — it is stale (`node tools/analysis-digest/index.mjs`). See "Analysis observations".

- [ ] **Task 2 — Whole-repo graph model from the data already in hand** (AC: #1, #2, #4)
  - [ ] New file `src/SpecScribe/CouplingExplorer.cs` (model + page-agnostic projection). Pure, repo-free, no I/O, no git.
  - [ ] **The inputs already exist and are UNCAPPED — do not add a git call, a second commit scan, or a second parse** ([[deep-git-single-numstat-path]]):
    - `DeepGitPulse.CoChangePairs` — *"The full (uncapped) canonical unordered file-pair co-change count map"* ([GitMetrics.cs:82](src/SpecScribe/GitMetrics.cs)). **This**, not `DeepGitPulse.Coupling`.
    - `DeepGitPulse.CodeMapMetrics[path].Changes` — *"ONE entry per file that appears anywhere in the analyzed window — deliberately NOT top-N truncated"* ([GitMetrics.cs:70](src/SpecScribe/GitMetrics.cs)). This is the confidence denominator and the node-size channel.
    - `DeepGitPulse.AnalyzedCommits` — the lift denominator and the honest window size (never a hard-coded 300).
  - [ ] ⚠️ **`DeepGitPulse.Coupling` and `DeepGitPulse.DirectedCoupling` are TOP-10** (`ParseNumstatLog(topCoupling: 10)`, [GitMetrics.cs:605](src/SpecScribe/GitMetrics.cs)). They are the *hub's* surfaces. Using either here yields a ten-pair "whole-repo" graph — a silent, plausible-looking wrong answer.
  - [ ] ⚠️ **`CodeMapMetrics` is `{ get; set; }` and `SiteGenerator` may clear it.** Guard for empty and degrade to the designed empty state; never divide by a missing change count.
  - [ ] Compute per directed pair: **confidence**, **support**, **lift**, **cross-boundary**, **Code/Process kind** — by **calling the shipped 24.1 spine**, never re-deriving:

    | Need | Call | Never |
    |---|---|---|
    | cross-boundary | `GitMetrics.IsCrossBoundary` | a fresh path-prefix compare |
    | Code vs Process | `GitMetrics.ClassifyCoupling` | the spike's path-shape approximation |
    | lift | `GitMetrics.Lift` | an inline divide — it is the **one** place the divide-by-zero guard lives and it returns `null`, never `NaN`/`Infinity` (which reach markup as literal text) |
    | support floor const | `GitMetrics.CouplingMinSupport` | a literal `2` |
    | percent formatting | `Charts.Percent` | a hand-rolled format |

  - [ ] Reuse the `DirectedCouple` record (24.1) rather than a parallel shape, unless its members genuinely do not fit — say why in a comment if you introduce anything new.

- [ ] **Task 3 — The adaptive floor + its config surface** (AC: #4, D2)
  - [ ] Implement the auto-tune exactly as specified in "The adaptive floor" in Dev Notes: raise the floor from `CouplingMinSupport` until **nodes ≤ `NodeBudget` AND edges ≤ `EdgeBudget`**, both declared consts with the spike numbers cited in their doc comments.
  - [ ] ⚠️ **EXPLORER-SCOPED, ABSOLUTELY.** Do **not** change `GitMetrics.CouplingMinSupport` and do **not** re-parse with a different `minSupport`. That const feeds the per-file "Coupled files" list on **every code page** and the Git Insights hub — Story 24.1 already learned that a floor change there is a site-wide visible behaviour change. The explorer filters the already-parsed `CoChangePairs` **downstream**; nothing outside `coupling-explorer.html` may move.
  - [ ] CLI: add `--coupling-floor <auto|N>` to `SiteSettings` ([SiteSettings.cs](src/SpecScribe/SiteSettings.cs)) with a `[Description]` that says **explorer-scoped** in so many words. Default `auto`.
  - [ ] Settings file: thread it through the full pipeline — `SavedSettings` field + `IsEmpty`, `SettingsStore.Capture` (**persist-only-the-non-default**: `auto` is the default, so persist `null` unless explicitly overridden — the discipline `DeepGit`/`IncludeReadme`/`TodayPolicy` each document at [SettingsStore.cs:220](src/SpecScribe/SettingsStore.cs)), `SettingsStore.ApplyTo` (CLI wins over file), `CliOverrides.Capture`, `SettingsResolver.Fields`, and the `--show-config` provenance line ([SettingsResolver.cs](src/SpecScribe/SettingsResolver.cs)).
  - [ ] Decide and **state in the completion notes** whether the interactive `Configure` command menu gains a prompt ([Commands.cs:728](src/SpecScribe/Commands.cs) is the idiom — note its "default to the current value so re-running Configure never silently flips it" discipline). Recommended: **no prompt** — this is a specialist tuning knob, not a path/branding choice, and every menu entry is a permanent maintenance surface.
  - [ ] An invalid value must be **rejected loudly at resolve time** (the `--today-policy` precedent), never silently coerced.
  - [ ] **Determinism:** the auto-tune is a pure function of the parsed window, so FR31 holds — but the chosen floor is now **data-dependent**, which means the golden fingerprint moves when the repo's history moves. Note that in the completion notes; it is the same class as [[ir-content-drift-was-data-dependence]].

- [ ] **Task 4 — Three silhouettes, one payload** (AC: #1, D3)
  - [ ] Solve **three** coordinate sets over the **same** node/edge set, all through `CouplingLayout` (24.2's solver) — extend it, do not fork it:
    1. **Free galaxy** — unconstrained seeded Fruchterman–Reingold.
    2. **Directory-grouped constellation** — files pre-clustered into spatial neighbourhoods by top-level directory, so a module reads as an island and a cross-boundary couple is visibly a **long** connector. (This is the *layout* half of the collapse D4 deferred — it groups **position**, it does not change the node set.)
    3. **Ring-of-modules** — top-level directories as arc segments, files on their own module's arc. **Straight edges only** — see the 24.4 collision note.
  - [ ] **ADR 0030 §3's determinism construction is NORMATIVE and applies to all three.** 24.2 already implemented it in `CouplingLayout`; extending the solver must not smuggle in a violation: no `System.Random`; **no `Dictionary`/`HashSet` iteration order may reach a floating-point accumulation** (materialize through an explicit ordinal sort first — float addition is not associative); no wall-clock, no environment, no parallelism; `CultureInfo.InvariantCulture` with a fixed format string.
  - [ ] **One payload, three coordinate arrays.** Node identity, path, label, degree, change count, and the entire edge set are **shared**; only `x`/`y` vary. Emitting three full graphs instead is a ~3× page-weight defect.
  - [ ] **One selector, not three controls** (ADR 0012 §2). Mirror `HierarchyExplorer`'s shape selector; it rides **inside** the component's `hidden` control bar so a JS-off reader never sees an inert control (the `ss-hierarchy-controls` / `codemap-controls` convention, [HierarchyExplorer.cs:613](src/SpecScribe/HierarchyExplorer.cs), [CodeMapTemplater.cs:206-208](src/SpecScribe/CodeMapTemplater.cs)).
  - [ ] **Switching silhouette must not re-solve and must not re-fetch** — it swaps the coordinate array on the existing traces. Reduced motion: any transition snaps under `prefers-reduced-motion`, driven from the `--motion-*` tokens ([[motion-token-system]]); **never `transition` a Plotly-owned property** ([[story-20-5-hierarchy-explorer-done]]).
  - [ ] **Measure and report the real solve cost of all three.** The spike measured **286 ms** for one free-galaxy solve at 129 nodes. Three is not three times one (the ring is O(n log n), effectively free), but report the actual total.

- [ ] **Task 5 — The page** (AC: #1, #2)
  - [ ] New file `src/SpecScribe/CouplingExplorerTemplater.cs`. Follow `RiskQuadrantTemplater` / `DeepAnalyticsTemplater` exactly — they are the closest siblings: a synthesized page (no markdown source) that builds its own shell and returns a **`PageView`** from `BuildPage`, with `RenderPage` as the HTML projection of the same model.
  - [ ] `Nav = nav.ToNavigationView(outputPath, nav.BuildInsightsLocalContext(outputPath))` — the shared Insights sub-header band ([DeepAnalyticsTemplater.cs:138](src/SpecScribe/DeepAnalyticsTemplater.cs), and the same line in `CodeMapTemplater` / `GitInsightsTemplater` / `RiskQuadrantTemplater` / `WorkGraphTemplater`). Skipping it drops the page onto the generic quick-links band — the exact defect Story 10.1 fixed.
  - [ ] Render the graph through **`RelationshipGraph`** (24.2's component). If its API genuinely cannot express a non-ego graph, **extend it** — a second component is the reinvention ADR 0012 §2 forbids. Say in the completion notes what you had to add.
  - [ ] **Mode is `navigate`** (ADR 0012 §3) — activating a node follows its `href` to that file's code page. `select` and a details pane are **not** in scope; if the verify round wants one it must use the shipped `specscribe:explorer-select` seam, never a parallel event (ADR 0030 §1).
  - [ ] Link resolution goes through the same `Func<string,string?>` dual-mode resolver the other surfaces use (`SiteGenerator.CodeItemHref`) — **a null return means "no in-portal page" → plain chip, never a dead link.**
  - [ ] **Designed empty state** when no couple clears the floor — never a blank box, never a misleading empty graph. `Charts.CouplingGraph`'s own `"No significant change coupling detected."` and `RiskQuadrantTemplater`'s threshold empty state are the precedents.
  - [ ] **Framing (Story 10.2):** `Charts.Framed` + `Charts.ChartMeta` + **`Charts.WhyText(ChartMetric.ChangeCoupling)`** ([Charts.cs:63](src/SpecScribe/Charts.cs)) — do **not** hand-roll "why" copy at the call site. `ChartMeta.Ranking` carries the ranking caption; `ChartMeta.Note` carries the AC #4 floor disclosure.
  - [ ] **The legend must describe the channel actually on screen.** ADR 0030 §5: `scatter` line style is a **trace-level** attribute, so per-edge styling means one trace per style class and stroke width is **quantized into bands**. A legend showing a continuous scale beside a banded chart is the "misdescribing entry" class Stories 10.7 and 21.1 each closed. **Confidence must be legible from the tooltip and the twin, never from width alone.**
  - [ ] Tooltips route through the body-level **`.ss-tooltip`** node, not a CSS `::after` ([[tooltip-clipping-use-ss-tooltip-node]]).
  - [ ] **Tokens, never Plotly colorways** (ADR 0012 §6). Neutral ink/gold/border tokens only — the `--status-*` lifecycle tokens are **off-limits on code surfaces**.

- [ ] **Task 6 — The text twin, and the invariant that bounds this whole story** (AC: #2)
  - [ ] **Normative invariant, and the single most consequential design rule in this story:** *the twin's row set and the solved node/edge set are **the same set**, by construction.* ADR 0013 §2 requires **complete** — "no fact may exist only inside the chart". Because the threshold control can reveal every edge the layout was solved at, the twin must cover **the most inclusive reachable state**, not the default view. Bounding the solve therefore bounds the twin, and Task 3's budgets are the one lever for both.
  - [ ] **Server-render it.** The spike measured a **client-built twin contributing 0 bytes** under a half-applied CSP.
  - [ ] It may be **visually collapsed or `sr-only`** — ADR 0013 §2 requires availability, not on-screen duplication. `<details>` is fine.
  - [ ] Reuse **`Charts.CouplingTable`** (24.1-upgraded: directional, Together/Confidence columns, lift on the cell title, Process + Cross-boundary **text** badges, [Charts.cs:1770](src/SpecScribe/Charts.cs)) rather than a second table. If a per-node grouping reads better at this scale, extend it — do not clone it.
  - [ ] ⚠️ **MEASURE THE TWIN'S BYTES AND REPORT THEM.** At the spike's floor-5 fixture this is **937 edges**. A `CouplingTable` row is roughly 600–800 B rendered, so the twin plausibly lands at **~560–750 KB — larger than the 95,514 B payload it accompanies.** This is a real page-weight decision, not a footnote. It is well under `SpaDelivery.MaxChunkBytes` (**2,000,000**, [SpaDelivery.cs:112](src/SpecScribe/SpaDelivery.cs)) so it will not join `oversizedPages` — but it would make this one of the heaviest pages in the portal (true inventory: **1,408 pages**, [[specscribe-true-page-inventory-1408]]). **Report the measured number and raise it to the owner in the verify round**; if it is unacceptable the lever is Task 3's `EdgeBudget`, not a partial twin.
  - [ ] Every link a node offers must be present and resolve in the twin (ADR 0013 §2 **navigable**), and every metric readable as text (**non-color**).
  - [ ] **Reading order = the twin's order**, and the graph's roving-tabindex order must match it (spike §6.2). Twin and graph must agree.

- [ ] **Task 7 — ⛔ GATING: audit `deep-analytics.html`'s twin, then retire `Charts.CouplingGraph`** (AC: #3)
  - [ ] **This task gates the deletion. Nothing is removed until the audit passes.**
  - [ ] Audit `deep-analytics.html`'s server-rendered twin against ADR 0013 §2's four properties **in a live browser with JavaScript disabled** ([[browser-pane-five-server-cap-file-url-fallback]] — verify over `file://` rather than stopping another session's server; note `navigate` **strips the hash**, which matters because this page's `:target` zoom lightbox is hash-driven). CLAUDE.md § Verification applies with full force: *the test suite structurally cannot see what a JS-off visitor actually gets.*
  - [ ] The candidate twin is `Charts.CouplingTable`, already on the page. **Confirm it is complete against what the SVG draws** — both are fed from `deep.Coupling`/`deep.DirectedCoupling` (top-10), so this should pass cheaply. **Prove it; do not assume it.** Record the result in the Dev Agent Record. **An incomplete twin keeps its SVG** — that is the ADR's rule, and reporting the gap is the correct outcome, not a failure of the story.
  - [ ] Then delete `Charts.CouplingGraph` and its dependents:
    - `Charts.CouplingGraph` ([Charts.cs:1824](src/SpecScribe/Charts.cs)) — ⚠️ `Charts.cs` carries **49 open Sonar observations**; it is already at its complexity ceiling. This deletion helps; do not add to it.
    - **TWO call sites**, both in `DeepAnalyticsTemplater`: [:62](src/SpecScribe/DeepAnalyticsTemplater.cs) (the framed panel) and **[:127](src/SpecScribe/DeepAnalyticsTemplater.cs) (the `#coupling-zoom` lightbox)**. 24.2 was bitten by a second, non-obvious `ReferenceGraph` call site; this is the same shape. Missing the second ships a compile error or a silently empty lightbox.
    - The `coupling-expand` / `#coupling-zoom` lightbox affordance itself — it exists to expand an SVG that no longer exists. **Its removal is a documented parity delta**: `DeepAnalyticsTemplater.BuildPage`'s doc comment records that this page's body *deliberately extends past `</main>`* because the lightbox is a sibling of the landmark (Story 23.4 AC #1/#3). **Removing it changes that**; update the doc comment or the claim goes stale.
    - CSS: `.coupling-graph`, `.coupling-expand`, `.coupling-legend`, `.coupling-lightbox-panel .coupling-graph` ([specscribe.css:4568, 4609, 4622, 4623, 4710](src/SpecScribe/assets/specscribe.css)). ⚠️ **A CSS comment containing `*/` silently truncates ~1000 rules** ([[css-comment-star-slash-silent-truncation]]).
    - **9 test references across `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs`** (`CouplingGraph_EmitsOneEdgePerPairAndOneNodePerDistinctFile`, `_DegeneratesToFriendlyNoteWhenEmpty`, `_FileHref_WrapsResolvedNodeInSvgAnchorOnly`, `_ProcessEdgeIsDashedWithTitleSuffix`, and `RenderPage_RendersCouplingGraphListAndHotspots`). Count them, rewrite or delete deliberately — **do not leave orphaned dead tests**.
  - [ ] Add the cross-link: `deep-analytics.html` points at the explorer. Keep `Charts.CouplingTable` on `deep-analytics.html` — it is that page's twin and its ranked list, and moving it would strip the page.
  - [ ] **Land the ADR 0013 §6 fingerprint replacement for the new surface** in this same change: assertions on the **embedded payload**, the **component configuration**, and the **twin**. If 24.2 already established that assertion idiom, follow it rather than inventing a second one.

- [ ] **Task 8 — Wiring, nav, and the deferred-work record** (AC: #1, D4)
  - [ ] `SiteNav`: add `CouplingExplorerOutputPath = "coupling-explorer.html"` beside its siblings ([SiteNav.cs:46-53](src/SpecScribe/SiteNav.cs)), with the same doc-comment discipline they all carry — *shared between the generator (writes the file) and the templater/nav (link to it) so the two can never disagree.*
  - [ ] Nav gate: add it to the **Insights** group in `Build` ([SiteNav.cs:340-368](src/SpecScribe/SiteNav.cs)) with a `quickLinks` entry. **Gate on a coupling-present signal, not merely on deep-git**: `progress?.DeepGit?.CoChangePairs.Count > 0`. Nav is built **after** the ingest callback computes progress, so the signal is available — the same ingest-before-nav ordering `hasDeepAnalytics`/`hasGitInsights` rely on. Follow the Work Graph precedent (its own signal) rather than the Risk Quadrant precedent (reuses a sibling's).
  - [ ] `SiteGenerator`: write the page in the insight-pages block beside the deep-analytics write ([SiteGenerator.cs:588-601](src/SpecScribe/SiteGenerator.cs)) via `WritePage(...)`, with the **same non-fatal try/catch + `GenerationEvent`** shape. Note the deep-analytics block **nulls `_progress.DeepGit` on failure** so the dashboard link cannot dangle — decide the analogous unwind here and state it (the nav entry was already emitted by then, so a failed write leaves a dangling Insights link unless you handle it; that is the accepted-tradeoff the nav method's own remarks document, but say which side you took).
  - [ ] `AssetManifest`: the page needs the Plotly bundle — reuse **24.2's graph-family flag**, do not add a second. Derive it from the **rendered body** via the `ContainsHost` idiom, never hand-set ([HierarchyExplorer.cs:1098](src/SpecScribe/HierarchyExplorer.cs); `Mermaid.ContainsBlock`). *A flag derived from the page cannot disagree with the page.*
  - [ ] Boot handshake: follow `HierarchyExplorer.BootScript`'s shape ([HierarchyExplorer.cs:1091](src/SpecScribe/HierarchyExplorer.cs)) — **including the timeout that removes the marker**, which is what keeps hide-first honest when the bundle is blocked. The boot marker is **chrome-level and must not land inside the IR content region** (`JsonSpaRenderAdapter.RenderContent` composes nav + wayfinding + body only; Story 23.4 Trap 3).
  - [ ] **Write the D4 deferred-work entry** in `_bmad-output/implementation-artifacts/deferred-work.md`, in the file's own `source_spec` / `summary` / `evidence` shape: directory grouping/collapse deferred from Epic 24 AC #1, why (ADR 0030 §4 — collapse is a different node set, so it needs a second precomputed layout, not a filter), and the measured price (two layouts ≈ 2× solve + 2× coordinate payload; the 236-state combinatorics the spike rejected came from the **continuous slider**, not from one discrete toggle).

- [ ] **Task 9 — Tests, determinism, and live-browser verification** (AC: #1, #2, #3, #4)
  - [ ] Unit tests: the model projection, the adaptive floor at several synthetic distributions (**including the degenerate cases: zero pairs, one pair, and a graph that never fits the budget at any floor**), the config resolution precedence (CLI beats file beats auto), and the twin's completeness against the solved set.
  - [ ] **Determinism must be verified by repetition across SEPARATE PROCESSES**, not by assertion and not in-process — in-process repetition cannot see string-hash randomization, allocation-order effects, or tiered JIT changing float contraction (ADR 0030 §3; spike §7.1 verified byte-identical across **3 separate processes**, 11 fixtures).
  - [ ] `GoldenOutputInventory` ([SiteGeneratorAdapterTests.cs:180-200](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)): **`coupling-explorer.html` will NOT join it** — that fixture is not a git repo, so no `--deep-git` page renders there (the comment at [:697](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) says exactly this for `git-insights.html`). **The golden fingerprint will still move**, via `specscribe.css` and the `deep-analytics.html` deletions. Do not "fix" the inventory by adding the page.
  - [ ] **Golden fingerprint WILL move — regenerate deliberately.** `dotnet build --no-incremental` **first** (embedded `.css`/`.js` assets are cached by an incremental build, so the hash you measure is stale), confirm **stable across two repeated runs**, and split the provenance — say whose changes yours sat on top of ([[golden-diff-normalization-gotchas]], CLAUDE.md § Concurrent work). **Never regenerate reflexively:** if it moved and you did not touch rendering, audit `GoldenNormalization.NormalizeVolatile` / `FoldToday` first. `GoldenIrFingerprint` (new in 23.4) is a second gate — check both.
  - [ ] **Live-browser verification is mandatory and is where the real defects will be** (CLAUDE.md § Verification). Verify: the graph at **129 nodes** actually reads (label collision, hub crowding), all three silhouettes switch without a re-solve and without nodes jumping, the threshold control hides without re-laying-out, real `ArrowRight`/`Enter`/`Escape` keys (**not** synthetic dispatch), focus-ring visibility, the tooltip's zero clipping ancestors, and the **JS-off state showing a complete twin**.
  - [ ] **Assert on GEOMETRY, not attributes.** The spike's hand-off: an attribute-only audit certified an ECharts chart that was **drawing nothing** (every path `d=""`, every symbol `scale(0)`) while every a11y attribute passed. And per Story 20.4, **do not assert on the console either.**
  - [ ] Confirm the page mounts after an **SPA navigation** (`specscribe:content-swapped`) and that removed mounts are purged.
  - [ ] **Report the total output-size delta across a real `--deep-git` portal** — page bytes, payload bytes, twin bytes, separately.

## Dev Notes

### What this story IS and is NOT

- **IS**: a new `coupling-explorer.html` page; the whole-repo force-directed view over the **uncapped** `CoChangePairs` data; three silhouettes behind one selector; an adaptive, configurable support floor; and — by owner decision D1 — the **retirement of `Charts.CouplingGraph`** from `deep-analytics.html` behind an ADR 0013 §3 twin audit.
- **IS NOT**: the ego graph (24.2, in flight), the chord/arc view (24.4), the adjacency matrix (24.5), directory **collapse** (D4, deferred), a new engine, a new dependency, or any change to `GitMetrics.CouplingMinSupport` or the metric itself (24.1, shipped). Not the ownership/bus-factor half either (Story 7.11 — do not touch it).

### The engine is decided. Do not re-open it.

[ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md), **Accepted 2026-07-29**: the already-vendored **Plotly `scatter`** trace over a **generation-time C# layout**. `src/SpecScribe/assets/plotly-hierarchy.min.js` (**1,223,563 B**, plotly.js **3.7.0**, MIT, embedded at `SpecScribe.csproj:67`) was measured to register exactly `heatmap, scatter, sunburst, treemap` — so `scatter` is already in the shipped tool and the marginal bundle cost is **zero bytes**. **ADR 0012 is EXTENDED, not superseded**; §4's allowance of a second engine family is left **unspent**; a third still requires its own ADR; SpecScribe's third-party runtime dependency count stays at **one**.

ECharts was measured and **rejected on cost-of-change, not merit**, and that rejection is recorded as **time-dependent**. **Do not re-argue it inside this story.** If the work genuinely demands it, the correct move is a focused re-opening of ADR 0030, never a quiet dependency.

> **Read `docs/adrs/` before declaring you are crossing a project rule.** [[charting-is-pure-svg-no-js]] is **SUPERSEDED** for this family. Story 21.3 described its interactive treemap as "a deliberate crossing of the pure-SVG, no-JS rule" citing a stale memory, when ADR 0010 already permitted it ([[adr-consultation-gap-three-arc-renderers]]). The ratified ADR is the authority.

### The adaptive floor (D2) — exact specification

```
floor = GitMetrics.CouplingMinSupport                 // 2, the shipped floor — never mutate it
while (floor < MaxAutoFloor
       && (NodeCount(floor) > NodeBudget || EdgeCount(floor) > EdgeBudget))
    floor++
```

- **`NodeBudget = 150`** — the spike's measured hairball threshold. Above it, a median degree of 14 with hubs at 100+ produces a solid mass.
- **`EdgeBudget = 1000`** — bounds both the payload *and*, via the Task 6 invariant, the **twin**.
- **`MaxAutoFloor`** — a declared ceiling so a pathological repo terminates; if the graph still does not fit, ship the empty/threshold state honestly rather than an unreadable one.
- Both budgets are **declared consts with the spike numbers in their doc comments**, not magic literals.

**On this repository (`-n 300`) this lands on floor 5** — 129 nodes / 937 edges / 95,514 B / 286 ms — which is exactly the spike's independent hand-off recommendation. That agreement is a useful sanity check on the implementation, not a coincidence to rely on.

**Why adaptive and not a constant:** the shipped floor of 2 gives **391 nodes / 4,864 edges / 460,817 B / 2,611 ms**, with `sprint-status.yaml` coupled to **359 of 391 nodes — 92% of the graph** — because every `dev-story` touches it.

**The Code/Process lens is a CLIENT filter, not part of the bound.** The auto-tune runs over the **full** edge set (the most inclusive solved state); the Story 10.6 Code-only lens then **hides** within it (ADR 0030 §4), defaulting **on** per the spike. Do not fold the lens into the solve — that would make the solved set depend on a client control, and the twin would have to follow.

**Say what was filtered.** The spike's explicit hand-off: *"say so in the UI rather than letting a reader think the repo is that entangled."* Expect **62% cross-boundary** and **46% Process-class** edges. AC #4's framing sentence is that disclosure and it is not optional.

### Measured numbers you can rely on (Story 24.6, this repository, `-n 300` — 300 commits, 714 files, 16,604 uncapped pairs)

| Support floor | Nodes | Edges | Payload B | Max degree | Components | C# solve |
|---:|---:|---:|---:|---:|---:|---:|
| **2 (shipped)** | **391** | **4,864** | 460,817 | **359** | 6 | 2,611 ms |
| 3 | 267 | 2,247 | 227,936 | 243 | 1 | 1,177 ms |
| **5 (auto-tune lands here)** | **129** | **937** | **95,514** | 116 | 2 | **286 ms** |
| 8 | 73 | 429 | 45,252 | 65 | 1 | 98 ms |
| 12 | 50 | 222 | 24,952 | 41 | 1 | 46 ms |
| **code-only**, floor 2 | 230 | 2,625 | 245,974 | 197 | 2 | 908 ms |

Degree distribution at floor 2: **median 14**, p95 **82**, max **359**; 14 nodes at degree ≥ 100, 34 at ≤ 3. Filter interaction: **44–75 ms** with `nodePositionsMoved: false`. A confidence slider yields **236 distinct edge sets** vs only **17 node sets** — *counting node sets alone is the trap*, because a Fruchterman–Reingold layout is a function of nodes **and** edges.

### ⚠️ The O(n²) generation-time budget — a real constraint on a bigger repo

The solver is **O(n²) as implemented**. Measured: 55 nodes = 59 ms · 78 = 120 ms · 145 = 386 ms · 308 = 1,583 ms · 489 = 4,183 ms. **Projected: ~1,000 nodes ≈ 17 s; ~2,000 ≈ 70 s.** ADR 0030's stated consequence hands this to *this story*: **bound the node count or adopt Barnes–Hut above ~500 nodes.**

**Task 3's `NodeBudget` is that bound**, and it is why the budget must be applied to the **solved** set, not merely to the default view — a slider that can reach a 391-node state means a 391-node solve. **State in the completion notes that the bound is the mitigation and Barnes–Hut was not needed** (or implement it and say why it was). This cost is paid **once by the generator, never by a reader** — which is the whole point of treating position as data.

### ⚠️ The ring silhouette collides with Story 24.4 — scope it deliberately

D3's **ring-of-modules** puts files around a circle with couplings crossing the interior. Story 24.4's chord/arc view puts files around a ring with **ribbons** connecting them. These are close enough to collide.

**The boundary this story takes:** 24.3's ring is a **layout of the same node-link renderer** — straight, banded, dash-styled edges, identical trace structure to the other two silhouettes, zero new geometry code. **24.4 remains distinct**: ribbon geometry (hand-drawn SVG arcs), a bounded ranked subset, and the demoted-alternate-view treatment UX-DR21 describes. **Do not draw ribbons here.** Record this boundary in the completion notes so 24.4 inherits it rather than rediscovering it.

**And the selector question 24.4/24.5 inherit:** this story establishes **one** selector listing the three silhouettes. When 24.4 and 24.5 add chord and matrix, they must **extend that same selector**, not add a second one — ADR 0012 §2's "one selector idiom" and UX-DR21's "one primary representation per dataset, alternates behind a toggle". Flag it forward.

### Existing surfaces to reuse — do not reinvent

| Need | Reuse | Location |
|---|---|---|
| The solver | `CouplingLayout` (**24.2**) — extend, never fork | `src/SpecScribe/CouplingLayout.cs` |
| The component | `RelationshipGraph` (**24.2**) — extend, never clone | `src/SpecScribe/RelationshipGraph.cs` |
| Whole-repo pair data | `DeepGitPulse.CoChangePairs` — **uncapped**; `CoChangeCount` canonicalizes pair order for you | [GitMetrics.cs:82](src/SpecScribe/GitMetrics.cs) |
| Per-file change counts | `DeepGitPulse.CodeMapMetrics[path].Changes` — **untruncated**, one entry per file in the window | [GitMetrics.cs:70](src/SpecScribe/GitMetrics.cs) |
| Cross-boundary flag | `GitMetrics.IsCrossBoundary` — **call it, never re-derive it** (24.1 AC #2: computed once, shared) | [GitMetrics.cs:375](src/SpecScribe/GitMetrics.cs) |
| Code/Process classification | the **real** `GitMetrics.ClassifyCoupling`, not the spike's path-shape approximation | [GitMetrics.cs:345](src/SpecScribe/GitMetrics.cs) |
| Lift | `GitMetrics.Lift` — the one divide-by-zero guard; returns `null`, never `NaN`/`Infinity` | GitMetrics.cs |
| Support floor const | `GitMetrics.CouplingMinSupport` — shared const, not a literal | [GitMetrics.cs:277](src/SpecScribe/GitMetrics.cs) |
| The twin table | `Charts.CouplingTable` (24.1: directional + Confidence + text badges) | [Charts.cs:1770](src/SpecScribe/Charts.cs) |
| Story 10.2 framing | `Charts.ChartMeta` + `Charts.Framed` + `Charts.WhyText(ChartMetric.ChangeCoupling)` | [Charts.cs:13-168](src/SpecScribe/Charts.cs) |
| Percent / plural formatting | `Charts.Percent`, `Charts.Plural` | Charts.cs |
| Page shell + `PageView` | `RiskQuadrantTemplater` / `DeepAnalyticsTemplater` — closest siblings | those files |
| Insights sub-header band | `nav.BuildInsightsLocalContext(outputPath)` | [SiteNav.cs:545](src/SpecScribe/SiteNav.cs) |
| Control bar / reveal handshake | `ss-hierarchy-controls` + `data-hierarchy-reveal` defer/flush | [HierarchyExplorer.cs:613](src/SpecScribe/HierarchyExplorer.cs), [specscribe.js:1092-1128](src/SpecScribe/assets/specscribe.js) |
| Tooltip | body-level `.ss-tooltip` via the `SEG` selector family | [specscribe.js:103](src/SpecScribe/assets/specscribe.js) |
| Config plumbing | `SiteSettings` → `SettingsStore.Capture/ApplyTo` → `CliOverrides` → `SettingsResolver.Fields` → `--show-config` | those files |

### The zero-width mount trap — less likely here, still check

24.2's headline trap is that the code page's Relationships tab is a pure-CSS radio panel, `display:none` at mount, and **Plotly draws the wrong size rather than complaining**. On a dedicated page the graph is visible at mount, so the acute form does not apply — **but the deferred-mount machinery is still the right thing to ride**: `deferHierarchyMount` / `flushHierarchyReveals` measure width on the **panel**, not the host ([specscribe.js:1038-1043, 1092-1128](src/SpecScribe/assets/specscribe.js)), and a host plotted while visible and later resized gets `Plotly.Plots.resize`. If any silhouette or filter control ever lands inside a collapsed region, the trap returns.

Also mirror the **failure unwind** ([specscribe.js:1063-1080](src/SpecScribe/assets/specscribe.js)): a throw *after* `Plotly.newPlot` succeeded previously left the reader with both charts mounted, the instance absent from the purge registry, and the ready flag still set so re-init skipped that root forever.

### Webview and SPA

- **Webview:** `WebviewRenderAdapter.StripDataIslands` removes every `<script type="application/json">` island ([WebviewRenderAdapter.cs:101](src/SpecScribe/WebviewRenderAdapter.cs)), so **the webview cannot receive a graph payload today**. Take the **ADR 0013 §7 text-twin fallback** — the same call 24.2 took — and **verify the webview page does not ship an empty box**. Narrowing the exception is a joint decision with the hierarchy family and would want its own ADR (CLAUDE.md § Decision records). CSP itself is fine: `script-src 'nonce-…'` alone suffices, header **and** meta, no `'unsafe-eval'`, and `style-src 'unsafe-inline'` is **not** load-bearing. **Read the policy from `WebviewRenderAdapter.cs` at runtime rather than citing a line** — it drifted `:116 → :140` during the spike ([[cite-adrs-by-symbol-not-line-number]]).
- **SPA:** the `specscribe:content-swapped` seam re-inits components after a content swap ([[story-20-2-zoomable-drill-in-done]]); the spike verified the a11y layer survives it **8/8**, including a bare `Plotly.react` the component did not initiate.
- **IR content styling:** a new page with bespoke vocabulary may need `ir-content.css` rules — `check:ir-content` drift has been red from ordinary sprint work before, and the silent half **shipped an unstyled tile** ([[ir-content-drift-was-data-dependence]], ADR 0018/0026). Run the check.

### Preservation invariants — leave the system working end-to-end

- **Baseline output byte-identical WITHOUT `--deep-git`.** No coupling data → no page, no nav entry, no asset flag. Verify, do not assume.
- **`GitMetrics.CouplingMinSupport` does not move.** Code pages and the Git Insights hub read it.
- **`deep-analytics.html` keeps its hotspots and its `CouplingTable`** — only the SVG graph and its lightbox leave.
- **Every chart needs an accessible text equivalent, and no state may be signalled by color alone** (CLAUDE.md § Verification, UX-DR17/19).
- `CouplingFileSetCap = 50`'s bulk-commit skip already excludes merge/vendored sweeps from pair counts — **inherited for free, do not re-implement**.
- Output dir is `SpecScribeOutput` ([[generate-output-dir-is-specscribeoutput]]). Never `--output docs/live`.

### Previous-story intelligence (24.1 shipped · 24.6 shipped · 24.2 in flight)

- **The metric spine exists and is correct** (24.1): `CoupledFile`, `DirectedCouple`, `DeepGitPulse.DirectedCoupling`, `IsCrossBoundary`, `CouplingMinSupport`, `Lift()`, `Charts.Percent` all shipped.
- **24.1's open Q4, now yours at 129 nodes:** on this repo the visible top-10 all come back at **100% confidence**, so a confidence channel does no ranking work in the visible window — while **lift** genuinely separates those rows (15.0× vs 2.16×) but is tooltip-only. **24.2 was told to measure this at 20 nodes and raise it rather than change ranking policy unilaterally.** Check what 24.2 concluded and **match it**; if 24.2 deferred, measure at 129 and raise it to the owner. Do not diverge from the ego graph's encoding — two surfaces disagreeing about what edge width means is worse than either choice.
- **24.1's live pass caught two defects the suite structurally could not see**, both pure rendered geometry. Expect the same class here — a full-width page is more forgiving than 24.2's ~455px panel, but 129 nodes is far denser than 21.
- **The deep-git 3s-timeout flake is real and silently produces no deep surfaces at all** ([[gitmetrics-3s-timeout-silent-deep-git-loss]]). It cost 24.1 two generation attempts. If a `--deep-git` run comes back with no coupling, **suspect the timeout before suspecting your code**.
- **Suite "flake" is usually a running preview server** ([[suite-flake-cause-is-a-running-preview-server]]) — git SPAWN starvation. Stop previews before the full suite. The browser pane also caps dev servers at **5 per folder across all chats**.

### Files being modified — read current state before editing

- `src/SpecScribe/CouplingExplorer.cs` — **NEW.** Model + projection + the adaptive floor.
- `src/SpecScribe/CouplingExplorerTemplater.cs` — **NEW.** The page (`BuildPage` → `PageView`, `RenderPage` projection).
- `src/SpecScribe/CouplingLayout.cs` — **24.2's file.** Extended with the two extra silhouettes.
- `src/SpecScribe/RelationshipGraph.cs` — **24.2's file.** Extended for the whole-repo shape + the silhouette selector.
- `src/SpecScribe/SiteNav.cs` — output-path const + Insights group entry + quick link.
- `src/SpecScribe/SiteGenerator.cs` — the page write, its gate, and its failure unwind.
- `src/SpecScribe/SiteSettings.cs`, `SettingsStore.cs`, `SettingsResolver.cs` (+ `Commands.cs` if a Configure prompt is added) — the `--coupling-floor` surface.
- `src/SpecScribe/DeepAnalyticsTemplater.cs` — **two** `CouplingGraph` call sites + the lightbox + the `BuildPage` doc comment's past-`</main>` claim.
- `src/SpecScribe/Charts.cs` — `CouplingGraph` retired (Task 7, **gated**). ⚠️ **49 open Sonar observations**; already at its complexity ceiling.
- `src/SpecScribe/AssetManifest.cs` — reuse 24.2's graph flag for the new page.
- `src/SpecScribe/assets/specscribe.js` — silhouette switching + the threshold filter.
- `src/SpecScribe/assets/specscribe.css` — new page styles; deletion of the four `coupling-*` SVG rules. ⚠️ **`*/` inside a comment silently truncates ~1000 rules** ([[css-comment-star-slash-silent-truncation]]).
- `_bmad-output/implementation-artifacts/deferred-work.md` — the D4 collapse entry.

### Shared-main discipline (CLAUDE.md § Concurrent work)

**Story 24.2 is editing `Charts.cs`, `CodeFileTemplater.cs`, `specscribe.js`, `specscribe.css`, `AssetManifest.cs` and `GitMetrics.cs` right now.** Five of those are on this story's list too. This is the exact condition CLAUDE.md § Concurrent work describes:

- **Grep-verify every new symbol after writing it** — a `Charts.cs` edit has silently vanished this way before ([[shared-main-concurrent-edit-loss-verify-after-edit]]; a zero-grep can also be a **transient mid-write**).
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**
- **Expect the golden fingerprint to move under you from 24.2's session.** Establish causality before regenerating; bisect into a throwaway tree (`git archive HEAD` into the scratchpad) rather than resetting the shared tree.
- **Attribution by hunk, not by file** (CLAUDE.md § Scoping a code review): `Charts.cs`, `specscribe.js` and `specscribe.css` will carry both stories' work in the same commit range. Record which hunks are yours.

### Analysis observations

`.specscribe/analysis/` was last evaluated at **`630ae25`** while HEAD is **`70b72ab`** — per CLAUDE.md's read-time rule, **the digest is stale regardless of what `isStale` says**. Re-run `node tools/analysis-digest/index.mjs` (Task 1) before trusting a line number. Read **shards**, not `index.json`: `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`. Known directionally: `Charts.cs` **49** observations, `CodeFileTemplater.cs` **12** (incl. two `S3776` cognitive-complexity errors). Absent means **UNKNOWN, never clean**.

### Project Structure Notes

Two new `src/SpecScribe/*.cs` files plus their test siblings; one new output page; one new CLI/settings field; everything else lands in existing files. **No new dependency, no new engine family, no new nav group.** If working in a worktree, target the worktree path — `main` has a background auto-committer ([[worktree-edits-must-target-worktree-path]]).

### References

- [Source: docs/adrs/0030-epic-24-graph-engine.md] — **the engine decision.** §1 engine · §2 position-is-data · §3 **normative** determinism construction · §4 filters-hide-never-re-lay-out · §5 per-edge emphasis + width banding · §6 24.5 unchanged. "Bad, or at least costly" names the O(n²) budget and the hairball, both handed to **this story**.
- [Source: _bmad-output/implementation-artifacts/24-6-spike-report.md] — §7.1 determinism · §7.2 the 236-vs-17 filter finding · **§7.3 at-scale table and degree distribution** · **§7.4 the 24.3 defaults** · §10 the hand-off table.
- [Source: docs/adrs/0013-text-twin-is-the-no-js-contract.md] — §1 amended NFR-5 · **§2 the four twin properties (server-rendered · complete · navigable · non-color; collapsed/`sr-only` is acceptable)** · **§3 the hard per-surface gate** · §4 supersedes ADR 0010 §2 · §6 fingerprint replacement · §7 webview fallback.
- [Source: docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md] — §2 component contract · §3 `navigate`\|`select` mode grammar · §4 engine-family boundary · §6 tokens-not-colorways · §7 generation-time determinism.
- [Source: docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md] — edge direction convention.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 24] — epic charter, FR40, UX-DR19/20/21, NFR8, execution order 24.1 → 24.6 → 24.2 → 24.3 → 24.4/24.5.
- [Source: _bmad-output/implementation-artifacts/24-2-per-file-ego-coupling-graph.md] — the component and solver this story consumes; its D1–D4 and its `ReferenceGraph` supersession.
- [Source: _bmad-output/implementation-artifacts/24-1-directional-coupling-metric-foundation.md] — the metric spine and its four owner answers.
- [Source: src/SpecScribe/GitMetrics.cs] — `DeepGitPulse` (35), `CodeMapMetrics` (70), `CoChangePairs` (82), `DirectedCoupling` (97), `CouplingFileSetCap` (265), `CouplingMinSupport` (277), `ClassifyCoupling` (345), `IsCrossBoundary` (375), `TryComputeDeep` (569), `ParseNumstatLog` (604).
- [Source: src/SpecScribe/Charts.cs] — `CouplingTable` (1770), `CouplingGraph` (1824), `ChartMetric.ChangeCoupling` (20/63), `Framed`/`ChartMeta` (13-168).
- [Source: src/SpecScribe/DeepAnalyticsTemplater.cs] — `BuildPage` (30) and the **two** `CouplingGraph` call sites (62, 127).
- [Source: src/SpecScribe/SiteNav.cs] — output-path consts (25-80), Insights group assembly (340-368), `BuildInsightsLocalContext` (545).
- [Source: src/SpecScribe/SiteGenerator.cs] — the insight-page write block (588-601), `WritePage` (3970), `TryComputeDeep` call (4411).
- [Source: src/SpecScribe/HierarchyExplorer.cs] — `Render` (594), `LegendHtml` (712), `IslandHtml` (810), `TextTwinHtml` (976), `BootScript` (1091), `ContainsHost` (1098).
- [Source: src/SpecScribe/assets/specscribe.js] — tooltip `SEG` (103), Hierarchy Explorer block (998+), zero-width defer/flush (1092-1128), failure unwind (1063-1080).
- [Source: src/SpecScribe/SpaDelivery.cs] — `MaxChunkBytes` (112) and the `oversizedPages` declared exception (100-115).
- Prior art: Story 3.2 (`deep-analytics.html` and the graph being retired), Story 3.8 (the hub's coupling view), Story 7.10 (`risk-quadrant.html` — the closest "new Insights page" precedent), Story 10.1 (the Insights local-context band), Story 10.2 (chart framing), Story 10.6 (the Code/Process lens), Story 19.2 (`work-graph.html` — a new page with its own gate signal), Story 20.5/20.7/20.9/20.10 (the component and its shared-payload idiom), Story 20.4 (the a11y decision rule), Story 23.1 (payload-size baseline), Story 23.4 (`PageView`, region composition, `GoldenIrFingerprint`).

### Open questions for the owner — do NOT block dev-start

1. **The twin's byte cost.** Task 6's invariant (twin set == solved set) puts ~937 rows on the page, plausibly **~560–750 KB** — larger than the payload it accompanies. **Measure it and raise the real number.** If it is unacceptable, the lever is `EdgeBudget`, not a partial twin (ADR 0013 §2 forbids that).
2. **Confidence vs lift in the width bands.** 24.1's Q4, inherited. **Match whatever 24.2 shipped**; propose a change rather than taking one, and never let the two surfaces disagree.
3. **`deep-analytics.html`'s identity after the graph leaves.** It becomes hotspots + the coupling table + a cross-link. Worth confirming in the verify round that it still earns its own Insights entry rather than folding into the hub.
4. **The Configure-menu prompt for `--coupling-floor`** — recommended *out*; confirm.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-30 | Story 24.3 created (baseline `70b72ab`). **Status `blocked`, one gate: Story 24.2 is `in-progress` right now and creates `CouplingLayout.cs` + `RelationshipGraph.cs`, neither of which exists at `70b72ab`** — flip to `ready-for-dev` when 24.2 reaches `review`. Four owner decisions elicited up front: **D1** new page `coupling-explorer.html` with `Charts.CouplingGraph` **retired** from `deep-analytics.html` (one whole-repo renderer, mirroring 24.2's `ReferenceGraph` supersession); **D2** an **adaptive** support floor auto-tuned to node/edge budgets, overridable via a new `--coupling-floor` CLI flag and `.specscribe` setting, **explorer-scoped** so `GitMetrics.CouplingMinSupport` never moves; **D3** all three silhouettes (free galaxy · directory-grouped constellation · ring-of-modules) behind **one** selector over **one** shared payload with three coordinate sets; **D4** directory **collapse deferred** to a follow-up, so AC #1 ships partially satisfied against the epic's literal wording and a `deferred-work.md` entry is a gating subtask. AC #2 amended (ADR 0013 §1/§4: text twin, no static SVG fallback — same amendment 24.2 took). AC #3 added for the `deep-analytics.html` twin audit + the ADR 0013 §6 fingerprint replacement. AC #4 added for the adaptive floor and its honest disclosure. Key structural findings recorded: the whole-repo inputs (`CoChangePairs`, `CodeMapMetrics`) are **already uncapped and already parsed** so no new git call is needed, while `Coupling`/`DirectedCoupling` are **top-10 traps**; the twin-completeness invariant (twin set == solved set) is what bounds both the O(n²) solve and the page weight; and the ring silhouette's boundary against Story 24.4 is scoped to straight edges. |
