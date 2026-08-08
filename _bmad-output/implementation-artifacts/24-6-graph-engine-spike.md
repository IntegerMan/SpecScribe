---
baseline_commit: 5a96f711c8f10654e011cac23a5823079634d565
decides: docs/adrs/00XX-epic-24-graph-engine.md # NEW ADR — this spike DECIDES, it does not merely validate. Contrast Story 20.4, which validates an already-ratified ADR 0012.
answers_open_question: docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md # §4 "Epic 24's graph engine is a named open question, deferred to Epic 24's own spike"
companion_decision: docs/adrs/0013-text-twin-is-the-no-js-contract.md # no server-SVG fallback → the a11y axis is decision-grade here too
informs: [24-2, 24-3, 24-4, 24-5]
blocks: [24-2, 24-3, 24-4] # 24.5 may ride Plotly heatmap regardless (ADR 0012 §4)
depends_on: [20-4] # reuse its measured Plotly bundle numbers + CSP/a11y harness rather than re-measuring
sequencing: 20.7 must land before 24.2 begins (SCP 2026-07-24) — unchanged; this spike may run BEFORE 20.7
execution_order: 24.1 → 24.6 (this) → 24.2 → 24.3 → 24.4/24.5 # numeric order ≠ execution order, as with Epic 23 (23.2→23.3→23.5→23.4)
---

# Story 24.6: Epic 24 Graph-Engine Spike — Force-Directed, Chord, and Matrix Under One Contract

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer about to build four interactive relationship views,
I want Epic 24's graph engine decided on measured evidence before Story 24.2 writes a line of rendering code,
So that the choice ADR 0012 §4 explicitly deferred to "Epic 24's own spike" is made once, in one place, with an ADR behind it — instead of being improvised inside an implementation story.

## ⛔ Read first — why this story exists at all

[ADR 0012 §4](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) reads:

> **Epic 24's graph engine is a named open question**, deferred to Epic 24's own spike. It may be Plotly `scatter`
> with a hand-rolled layout, a second library, or bespoke — decided on evidence, not assumed here.
> **Two engine families are permitted. A third requires an ADR.**

**Epic 24 had no spike.** Stories 24.1–24.5 are all implementation stories, so that named open question had no
owner. Left as-is, a dev agent implementing Story 24.2 would have had to select SpecScribe's **second third-party
runtime dependency** mid-implementation — precisely the failure mode [[adr-creation-trigger-gap-epic-10-retro]]
records and CLAUDE.md § Decision records forbids. This story is that missing spike, seated by `create-story`
on 2026-07-24 with owner approval.

| | |
|---|---|
| **This spike does** | Decide the engine for 24.2/24.3/24.4. Measure bundle, a11y, CSP, determinism, and at-scale legibility. **Author a new ADR** (or a reasoned ADR 0012 supersession). Resolve the code-page double-graph question below (R3). |
| **This spike does NOT** | Ship production code. Build the ego graph, the explorer page, the chord view, or the matrix. Vendor anything for real. Touch `src/SpecScribe/**` or `tests/**`. Land the ADR 0005 CSP amendment. |

**Discipline:** decision-first, timeboxed, throwaway — the same discipline as Stories 6.3, 6.6, 20.1, 22.1, 23.1,
20.4. Durable deliverables are **a spike report** at `_bmad-output/implementation-artifacts/24-6-spike-report.md`
and **a ratified ADR**. Everything else is disposable. Suggested timebox: **2 days**. If one axis eats the box,
finish that axis and report the rest as unmeasured rather than half-measuring all of them.

**Superseding ADR 0012 is a sanctioned outcome, not a failure.** ADR 0012's own options table records ECharts as
*"Considered and deferred, not dismissed… Epic 24's graph spike may legitimately reopen this, and if it selects
ECharts, superseding this ADR is the expected outcome rather than a failure."* Say the word plainly if the
evidence points there.

## 🔴 Reconciliations against shipped code — verified 2026-07-24, honor these

Each of these changes what you would otherwise measure, build, or conclude.

### R1 — Story 20.4 is running RIGHT NOW in a concurrent session. Inherit its numbers; do not re-measure Plotly.

`spike/plotly/` exists untracked in the working tree at authoring time (`package.json`, `plotly-src/`,
`scripts/`, `measurements/baseline.json`), alongside uncommitted `src/SpecScribe/` edits from another session.
Story 20.4 is **in flight**, not pending.

**Consequences, both directions:**
- **Take 20.4's Plotly measurements as given.** Its bundle sizes, `--strict` CSP verdict, `file://` result, and
  a11y pass/fail table are inputs to your comparison, not work to redo. If `20-4-spike-report.md` exists when you
  start, read it first and cite it. If it does not, say so and mark the Plotly column *inherited-pending*.
- **CLAUDE.md § Concurrent work applies at full force.** Do not `git reset --hard` / `git checkout --` /
  `git clean`. Grep-verify every symbol you rely on rather than trusting a read from this story. Expect
  `spike/plotly/` and `src/` to move under you.

### R2 — Plotly's `scatter` trace **cannot be excluded from any bundle**. The "Plotly scatter" option is therefore free.

Story 20.4's R1 records, from Plotly's own `CUSTOM_BUNDLE.md`, that `scatter` lives in `lib/core.js` and ships in
**every** bundle — so the real Epic 20 floor is `scatter + sunburst + treemap + heatmap`.

**This is the single most decision-relevant fact in this spike**, and it is easy to miss because 20.4 filed it as
a documentation correction. If Epic 20 ships Plotly at all, `scatter` is already paid for. Plotly's own published
network-graph recipe is exactly *edges as a `scatter` trace with `mode:'lines'`, nodes as a `scatter` trace with
`mode:'markers'`*, with the **layout computed externally** and fed in as coordinates. So:

> **Option "Plotly scatter + generation-time layout" has a marginal bundle cost of ZERO bytes.**

Any competing library must justify its bytes against that floor, not against nothing. **Confirm the
non-removability claim against the actual 20.4 build output** before you lean on it — do not inherit it as
folklore.

### R3 — The code page ALREADY has an ego graph. 24.2 must not add a second one.

Story 24.2's AC #2 says it "evolves the Story 7.8 `Charts.ReferenceGraph`." What is actually shipped is much more
than a fallback:

[`CodeFileTemplater.cs:409-470`](../../src/SpecScribe/CodeFileTemplater.cs) renders a *"Referenced by"* relationship
card containing [`Charts.ReferenceGraph`](../../src/SpecScribe/Charts.cs) (`:2341`) — a **hub-and-spoke graph with the
focal file at the center** and, on a ring, **two node populations**: citing artifacts (gold circles, solid spokes)
and **co-changed files** (neutral diamonds, dashed spokes). It also carries **four pre-rendered variants** driven
by two pure-CSS toggles (`RefGraphVariants` at `CodeFileTemplater.cs:401`, consumed at `:465` — `flat-flat` / `epic-flat` / `flat-rel` / `epic-rel`, i.e. *"Group by
epic"* × *"Show relationships"*), plus story↔related-file and related↔related cross-edges
([`SiteGenerator.cs` `BuildStoryRelatedEdges`/`BuildRelatedRelatedEdges`](../../src/SpecScribe/SiteGenerator.cs)),
an artifact-node cap of 14 with honest "+N more", and an sr-only citer list.

**That is already a static ego coupling graph.** So the real question 24.2 faces is not "what engine draws a new
graph" but:

> Does the Epic 24 ego graph **supersede** `ReferenceGraph` — absorbing its related-file population, its four
> variants, and its cross-edges into one interactive surface — or does it sit **beside** it, leaving the code page
> with two relationship graphs?

Answer this in the report. It materially changes 24.2's size, and it interacts with ADR 0013: if the Epic 24 graph
supersedes `ReferenceGraph`, then retiring that SVG is gated on a **text-twin audit** exactly like Story 20.6's,
which nothing in Epic 24 currently owns. **Surface that gap explicitly** — do not quietly assume 20.6 covers it
(20.6's scope is the hierarchy surfaces).

### R4 — Nothing in this codebase does force-directed layout today. Every existing graph is deterministic and hand-placed.

Verified in `Charts.cs` and `assets/specscribe.js` (2,237 lines):

| Graph | Layout | Notes |
|---|---|---|
| `CouplingGraph` `:2231` | Fixed circle, nodes ordered by degree | Ring positions from `-π/2 + 2πi/n`. Fully deterministic. |
| `ReferenceGraph` `:2341` | Hub-and-spoke ring | Deterministic; four variants pre-rendered. |
| `WorkGraph` `:2655` | Layered DAG | Story 19.2. |
| `initImpactMap` / `initSunburstExplorer` / `initOwnershipSunburst` | Arc/rect geometry | Client-side, but geometric — no physics. |

There is **no physics simulation, no d3-force, no iterative solver** anywhere in the repo. Whatever you pick
introduces a genuinely new class of behavior. Which leads directly to:

### R5 — ADR 0010 §3 survives ADR 0012 and it constrains *where the layout runs*.

> "Data is computed **once at generation time and embedded** — never re-derived client-side from live git state
> or wall-clock 'now.'" — ADR 0010 §3, explicitly restated as still standing by ADR 0012 §7.

A client-side force simulation is **iterative and seed-sensitive**. Two questions fall out, and they are the
architectural crux of this spike:

1. **Is node position "data" (→ generation time, C#) or "presentation" (→ client)?** ADR 0010 §3 is about *data*,
   so a client-side layout is not automatically a violation — but a graph whose node positions differ run-to-run
   is in tension with **FR31 generation-time determinism**, which Stories 24.3 AC#2 and 24.5 AC#2 both name by
   hand. Resolve this and say which reading you applied.
2. **Can the layout be computed in C# at generation time and embedded as coordinates?** A seeded
   Fruchterman-Reingold / Barnes-Hut pass over a bounded node set is a few hundred lines of deterministic C#, it
   costs zero client bytes, it makes FR31 trivially true, and it reduces the client's job to *drawing points and
   lines* — which R2 says Plotly already does for free. **Prototype this. It is the option most likely to be
   right and least likely to be considered.**

Note the honest counter-argument: a precomputed layout gives up interactive drag/re-settle, and 24.3's AC #1 asks
for "clutter controls (minimum support/confidence threshold and directory grouping/collapse)" — filtering nodes
**changes the graph**, so either the layout must re-run client-side or every filter state must be precomputed
(the `RefGraphVariants` four-variant idiom in R3 is exactly that pattern, at a scale that may not survive a
continuous threshold slider). **Test the filter interaction explicitly; it is where this option most plausibly
breaks.**

### R6 — Story 24.1 is `ready-for-dev`, not done. The metric this spike renders does not exist yet.

`CoupledFile(Path, Support, Confidence, Lift, CrossBoundary, Kind)` and the hub's directed-couples projection are
**designed but unimplemented** ([24-1](./24-1-directional-coupling-metric-foundation.md) Tasks 2–3). Today's shape
is the symmetric tuple `IReadOnlyList<(string Path, int CoChanges)>`.

**Build your probe against 24.1's designed record shape, not today's tuple** — you are sizing the surface Epic 24
will actually have. Where you need real data, derive `confidence`/`support`/`cross-boundary` yourself from
`DeepGitPulse.CoChangePairs` + `FileInsight.ChangeCount` in throwaway probe code. Do **not** implement 24.1 as a
side effect of this spike; if you find yourself editing `GitMetrics.cs`, you have left the spike.

Also note 24.1's four open owner questions (Q1–Q4) are still open. Your report should flag any that your engine
recommendation would constrain — particularly **Q1** (whether the hub view goes directed), since a directed graph
needs arrowheads and an undirected one does not.

### R7 — "Cross-boundary emphasis" cannot be color, and that constrains the engine.

Every one of Stories 24.2–24.5 requires cross-boundary couples "emphasized," and UX-DR17/UX-DR19 forbid
color-alone signalling. The shipped precedent is in `CouplingGraph` `:2278-2283`: process-coupling gets a **dashed
stroke** plus a `<title>` suffix, never a hue change. `ReferenceGraph` distinguishes its two populations by
**shape AND edge style**.

So the engine must expose, per-edge: **dash pattern**, **width**, and **accessible text**. That is a real
selection criterion — some renderers style edges only en-masse. Check it per candidate; do not assume.

### R8 — The engine must survive the SPA re-init seam and the tooltip node.

Two shipped seams every enhancement block must use, both from prior Epic 20 work:

- **`specscribe:content-swapped`** ([`specscribe.js:1712`](../../src/SpecScribe/assets/specscribe.js), `:2234`) —
  the SPA swaps `<main>` without a page load, so every initializer must re-run on this event.
  [[story-20-2-zoomable-drill-in-done]]
- **`specscribe:explorer-select`** (`:2072`, `:2161`) — the Story 20.3 selection seam that drives the related-work
  card rail. ADR 0012 §3's `select` mode is built on it. If the Epic 24 graph offers `select`, it uses **this**
  event, not a new one.
- **Rich tooltips route through the body-level `.ss-tooltip` node**, never CSS `::after` — those clip inside
  `chart-panel`/board overflow contexts. [[tooltip-clipping-use-ss-tooltip-node]]

An engine that owns its own tooltip DOM and cannot be redirected is a finding.

### R9 — The vendoring, embedding, and conditional-emission path is fixed. Any candidate must fit it.

- `tools/prism-vendor/{build.js,package.json,README.md}` — hand-run build, **committed artifact**, `node_modules`
  throwaway/gitignored. Node is **build-time-only and developer-side**; `specscribe generate` must never need it.
- [`SpecScribe.csproj:62`](../../src/SpecScribe/SpecScribe.csproj) — `<EmbeddedResource Include="assets\prism.js" />`
- [`SiteGenerator.cs:1985-1986`](../../src/SpecScribe/SiteGenerator.cs) — `CopyEmbeddedAsset(...)` **conditionally**,
  so sites without the relevant pages stay byte-identical.

**Size yardstick:** shipped `assets/prism.js` = **100,409 B**; `assets/specscribe.js` = **116,165 B**;
`assets/specscribe.css` = **295,896 B**. Report every candidate as a multiple of `prism.js` — it is this project's
own answer to "how big a vendored dependency have we already accepted."

**Hard constraint:** a **single classic `<script nonce>` file**, no ES-module static imports (Story 23.1 measured
that a nonce does **not** propagate to a module's static imports, so they get blocked under the webview CSP), no
runtime fetch (`default-src 'none'` with no `connect-src`), `file://`-safe, offline.

### R10 — Do not chase the golden fingerprint.

`GoldenContentFingerprint` builds from a synthetic `Directory.CreateTempSubdirectory` fixture and never walks the
repo ([`SiteGeneratorAdapterTests.cs:19`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)), so adding
`spike/graph-engine/` **cannot** move it. Story 23.1 offered "the hash is unchanged" as evidence of
non-invasiveness and had to retract it as *structurally vacuous*. The load-bearing evidence is that `git` shows no
`src/` or `tests/` file modified **by you** — and R1 means other files WILL be modified by someone else, so scope
that claim to your own changes. [[golden-diff-normalization-gotchas]]

## Acceptance Criteria

> These ACs are authored by this story (Epic 24 had no spike section) and are mirrored into
> [epics.md](../planning-artifacts/epics.md) § Story 24.6 in the same change.

1.
**Given** ADR 0012 §4's named open question and the "two engine families permitted, a third requires an ADR" rule
**When** the spike evaluates candidate engines for the force-directed (24.2, 24.3) and chord/arc (24.4) views
**Then** it reports, per candidate, a comparable table of: **bundle size** (minified and min+gzip, as a multiple of `prism.js`), **license and provenance** (NFR10), **coverage of the four Epic 24 shapes** (ego force-directed, whole-repo force-directed, chord/arc, adjacency matrix), **whether it is a single classic script with no runtime fetch and no ES-module imports** (R9), and **whether adopting it would constitute a third engine family** requiring ADR 0012 to be superseded rather than extended
**And** the "Plotly `scatter` + generation-time layout" option is evaluated **as a first-class candidate at its true marginal cost** (R2), not dismissed as a fallback.

2.
**Given** ADR 0013 removed the server-rendered SVG that used to sit behind every chart
**When** the spike evaluates the leading candidate(s)
**Then** it reports **explicit PASS / PASS (configured around) / FAIL** conformance per **UX-DR7** (Tab order, Enter/Space activate, Escape up), **UX-DR16** (accessible name, announced state), **UX-DR17** (never color-alone — including per-edge dash/width control per R7), and **UX-DR18** (`prefers-reduced-motion`: a force simulation that animates to rest **must** be able to snap), using Story 20.4's decision rule, verified in a **live browser**
**And** it reports whether the candidate renders under the byte-verbatim VS Code webview CSP from [`WebviewRenderAdapter.cs:116`](../../src/SpecScribe/WebviewRenderAdapter.cs), reporting the **script axis and style axis separately**, and carrying Story 23.1's honesty boundary (`<meta>` delivery ≠ header delivery; `vscode-resource:` untested).

3.
**Given** FR31 generation-time determinism (named by hand in Stories 24.3 AC#2 and 24.5 AC#2) and ADR 0010 §3's "computed once at generation time and embedded"
**When** the spike evaluates layout strategy
**Then** it answers whether node position is treated as **data** (computed in C# at generation time, embedded as coordinates) or **presentation** (solved client-side), demonstrates a **deterministic** result under the chosen strategy across repeated runs, and reports what happens to determinism when 24.3's **threshold/grouping clutter controls change the node set** (R5)
**And** it reports at-scale legibility and performance on **this repository at `--deep-git` scale** — node/edge counts after the Story 24.1 support floor, the point at which the whole-repo view becomes a hairball, and what bounding/threshold defaults 24.3 should ship with.

4.
**Given** CLAUDE.md § Decision records and the fact that this choice adds a runtime dependency or a new engine family
**When** the spike concludes
**Then** it lands a **ratified ADR** recording the decision, its options table, and its consequences — either a new ADR that ADR 0012 §4 hands off to, or an explicit **supersession** of ADR 0012 if the choice unifies both families (the sanctioned ECharts outcome)
**And** the report states what each finding hands to Stories 24.2/24.3/24.4/24.5, and resolves R3's code-page double-graph question with a recommendation.

## Tasks / Subtasks

- [x] **Task 1 — Branch, quarantine, inherit** (AC: #1)
  - [x] Work on an isolated spike branch or worktree (e.g. `spike/graph-engine-24-6`). Do **NOT** develop on `main` — background auto-committer plus at least one concurrent session (R1). If you use a worktree, **re-root every relative path at the worktree** ([[worktree-edits-must-target-worktree-path]]).
  - [x] All throwaway code lives under `spike/graph-engine/` per [`spike/README.md`](../../spike/README.md). Nothing joins `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, or the `extension/` bundle. Add `node_modules/`, `dist/` to the spike `.gitignore`.
  - [x] **Read [`20-4-spike-report.md`](./20-4-spike-report.md) first if it exists** (R1). Extract: chosen Plotly bundle variant + size, whether `scatter` is genuinely non-removable, the `--strict` CSP verdict, the a11y pass/fail table, and the `file://` result. Cite them; do not re-measure them. If the report does not exist yet, mark the Plotly column *inherited-pending* and proceed — do not block.
  - [x] Record toolchain (Node/npm versions) and confirm the Node-is-developer-side-only property holds for every candidate's build step (R9).

- [x] **Task 2 — Build the real data fixture** (AC: #1, #3)
  - [x] Generate this repo at `--deep-git` scale into `SpecScribeOutput/` ([[generate-output-dir-is-specscribeoutput]] — never `--output docs/live`).
  - [x] In throwaway probe code, derive **24.1's designed shape** from `DeepGitPulse.CoChangePairs` + `FileInsight.ChangeCount` + `AnalyzedCommits` (R6): directed `confidence = coChange(A,B)/ChangeCount[A]`, `support`, `lift`, and a `CrossBoundary` flag from differing first path segments. **Do not edit `GitMetrics.cs`.**
  - [x] Emit two fixtures: an **ego** set (one hub file + neighbours, the 24.2 shape) and a **whole-repo** set (the 24.3 shape) at the Story 24.1 default support floor of 2. Record node/edge counts for both, plus the counts at floors 3 and 5 — this is AC #3's at-scale input and 24.3's default-threshold evidence.
  - [x] Pick the ego hub deliberately: a genuinely hub-like file (`GitMetrics.cs` / `Charts.cs` / `SiteGenerator.cs` are the obvious candidates). A quiet file proves nothing about legibility.

- [x] **Task 3 — Candidate matrix** (AC: #1)
  - [x] Evaluate at minimum: **(a) Plotly `scatter` + generation-time C# layout** (R2/R5 — zero marginal bytes), **(b) Apache ECharts** (`graph` + force, native `chord` series since v6, plus `sunburst`/`treemap`/`heatmap` — the single-engine unification candidate), **(c) a small dedicated graph library** (Cytoscape.js ≈112 kB gz, or Sigma.js+graphology), **(d) bespoke SVG + a layout solver**. Add or drop candidates with a stated reason.
  - [x] For each: bundle size min and min+gzip **as a multiple of `prism.js` (100,409 B)**; license + provenance for the eventual NFR10 audit (Epic 17); coverage of all four Epic 24 shapes; single-classic-script / no-fetch / no-ESM-imports verdict (R9); per-edge dash + width + accessible-text control (R7); tooltip-DOM redirectability (R8).
  - [x] **Score the engine-family consequence explicitly.** Plotly-scatter = *no new family* (extends family 1). A dedicated graph library = *the second permitted family*. ECharts = *collapses to one family but supersedes ADR 0012*. Anything beyond that = *a third family and needs its own ADR*. State which column each candidate lands in — this is a governance fact, not a preference.

- [x] **Task 4 — Prototype the generation-time layout option** (AC: #3) — the option most likely to be right
  - [x] Implement a seeded deterministic force layout (Fruchterman-Reingold or Barnes-Hut) in **throwaway C#** under `spike/graph-engine/`, over the Task 2 fixtures. Emit `{id, x, y, r, label, href, kind}` nodes + `{a, b, confidence, support, crossBoundary}` edges as a JSON island shaped like the shipped `sunburst-explorer-data` island ([`SunburstExplorer.cs:62`](../../src/SpecScribe/SunburstExplorer.cs)).
  - [x] Render it with **Plotly `scatter`** — edges `mode:'lines'`, nodes `mode:'markers'` (Plotly's own documented network recipe). Confirm per-edge dash/width control (R7) and that the tooltip can route to `.ss-tooltip` (R8).
  - [x] **Prove determinism:** run the layout ≥3× and diff the emitted coordinates byte-for-byte. Any drift is a FAIL for AC #3 — hunt the source (unordered dictionary iteration, `Random` without a fixed seed, floating-point accumulation order) rather than papering over it.
  - [x] **Test the filter interaction (R5's named weak point):** apply 24.3's threshold/grouping controls and record what happens. Precomputed-per-state, re-solve-client-side, or reposition-nothing-and-just-hide — say which is viable and at what node count each stops being viable.
  - [x] Record the **payload byte size** for both fixtures. Compare against the SVG the equivalent static graph would cost, and against 23.1's measured 20,915 B sunburst island.

- [x] **Task 5 — Prototype the leading library candidate** (AC: #1, #2)
  - [x] Stand up the same two fixtures in the strongest library candidate from Task 3 (expected: ECharts, given the v6 `chord` series covers 24.4 natively and `graph`+force covers 24.2/24.3).
  - [x] Build a **tree-shaken/custom bundle limited to the series actually needed** and report its true size — the honest comparison is a custom build, not the full distribution.
  - [x] Render **all four Epic 24 shapes** from the one fixture if the candidate claims to cover them. A claimed capability that needs a plugin, a paid tier, or a fork is a finding.
  - [x] Confirm it can be driven entirely from SpecScribe's `--status-*` and brand tokens with the library's default palette disabled — **demonstrated by computed styles, not asserted from config** (ADR 0012 §6, AD-7, [[specscribe-status-token-system]]).

- [x] **Task 6 — Accessibility pass/fail** (AC: #2) — the highest-stakes axis
  - [x] Verify in a **live browser** (CLAUDE.md § Verification — the suite structurally cannot see focus survival, computed color, or CSP behavior). Produce a table with a literal **PASS / PASS (configured around) / FAIL** per UX-DR7/16/17/18 per candidate, applying Story 20.4's decision rule verbatim (PASS = documented config alone; PASS (configured around) = post-render DOM augmentation over the emitted SVG plus public events, **surviving** re-render; FAIL = requires forking internals, or the augmentation is destroyed with no supported hook).
  - [x] **UX-DR7 is the crux, and it is harder for a graph than for a sunburst.** A node-link graph has no natural reading order. Determine whether per-node keyboard focus is reachable, what order Tab visits nodes in (recommend one — e.g. confidence-descending, matching the text twin's order), and whether the focus layer survives filter changes, a resize, and a re-layout.
  - [x] **UX-DR18:** a force simulation that animates to rest is exactly what `prefers-reduced-motion` targets. Find the concrete knob that renders the settled state immediately with no animation, and confirm it can be driven from the media query at runtime ([[motion-token-system]]). "It settles quickly" is not a pass.
  - [x] **UX-DR17 / R7:** with the default palette disabled, confirm cross-boundary and process-coupling remain readable via **dash pattern, width, shape, and text** — never hue. Mirror the shipped `CouplingGraph` `:2278-2283` precedent.
  - [x] **Canvas vs SVG matters here.** A canvas renderer emits no per-node DOM, so a roving-tabindex layer has nothing to attach to. If a candidate offers an SVG renderer mode, test **that** mode and report the size/perf cost of choosing it.
  - [x] **If any verdict is FAIL, say so in the report's opening summary** and escalate via `correct-course` rather than softening it ([[adr-creation-trigger-gap-epic-10-retro]]).

- [x] **Task 7 — Webview CSP, offline, and `file://`** (AC: #2)
  - [x] Replay the **byte-verbatim** policy string from [`WebviewRenderAdapter.cs:116`](../../src/SpecScribe/WebviewRenderAdapter.cs) over the probe output in a real browser, reusing Story 23.1's [`csp-probe.mjs`](../../spike/nuxt-ir/scripts/csp-probe.mjs) harness shape (and 20.4's, if it extended it).
  - [x] Report the **script axis** and **style axis separately** (`style-src 'unsafe-inline'` is already granted; the live question is whether `script-src 'nonce-…'` alone suffices or `'unsafe-eval'` is needed).
  - [x] Render from a local `file://` path with **no server and networking disabled**. Any fetch, CDN reference, or sibling-chunk `import` is a finding (NFR-3 local-first).
  - [x] Account for [`WebviewRenderAdapter.cs:82`](../../src/SpecScribe/WebviewRenderAdapter.cs) stripping every `<script type="application/json">` island — state what would have to change for the webview to receive the graph payload at all, or record that Epic 24 surfaces take the ADR 0013 §7 text-twin fallback in the webview.
  - [x] **Carry Story 23.1's honesty boundary:** `<meta>` delivery ≠ header delivery, `vscode-resource:` untested, no Electron paint. Your verdict is a **lower bound**.
  - [x] **Do NOT author or land the ADR 0005 CSP amendment** — it lands once, jointly with Story 23.4 (ADR 0012 §5).

- [x] **Task 8 — Resolve the code-page double-graph question** (AC: #4) — R3
  - [x] Read [`CodeFileTemplater.cs:409-470`](../../src/SpecScribe/CodeFileTemplater.cs) and `Charts.ReferenceGraph` (`:2341`) in full, including `RefGraphVariants` and the `SiteGenerator` cross-edge builders. Enumerate exactly what `ReferenceGraph` renders today.
  - [x] Recommend **supersede** or **coexist**, with reasoning. If supersede: state which of `ReferenceGraph`'s capabilities the Epic 24 graph must absorb (citing-artifact population, epic grouping, story↔related and related↔related cross-edges, the 14-node cap + "+N more", the sr-only citer list) — and note that retiring that SVG needs an ADR 0013 §3 **text-twin audit** that no Epic 24 story currently owns.
  - [x] Size the consequence for Story 24.2 either way. This is the biggest single driver of 24.2's scope.

- [x] **Task 9 — Write the report and land the ADR** (AC: #1, #2, #3, #4)
  - [x] Write `_bmad-output/implementation-artifacts/24-6-spike-report.md`, mirroring [`23-1-spike-report.md`](./23-1-spike-report.md) / [`22-1-spike-report.md`](./22-1-spike-report.md): Context · Method · Measured Evidence (per axis) · Candidate matrix · Findings · **AC coverage table with explicit boundaries** · What was NOT done.
  - [x] **Label every number's provenance** — harness-derived / session-measured / projected / **inherited from 20.4**. Story 23.1's report had to be corrected post-review for exactly this failure; do not repeat it.
  - [x] **Author the ADR** (`docs/adrs/00XX-epic-24-graph-engine.md`, next free number — check `docs/adrs/` at write time, another session may have claimed one): Context (ADR 0012 §4's handoff) · Decision · Options considered table · Consequences · Ratified decisions. Follow the ADR 0012/0013 shape. Add it to `docs/adrs/README.md`.
    - If the choice **unifies** both families (ECharts), the ADR **supersedes ADR 0012** and must say so in its header and in ADR 0012's own status line — this is sanctioned.
    - If the choice **extends** family 1 (Plotly scatter), the ADR records that no new family was added and ADR 0012 §4's open question is closed, not superseded.
    - If the choice **adds** family 2, the ADR records it as the second permitted family under ADR 0012 §4 and states plainly that a third now requires yet another ADR.
  - [x] State what each finding hands to **24.2** (ego graph + the R3 verdict), **24.3** (whole-repo explorer + threshold defaults + at-scale limits), **24.4** (chord — native series or hand-drawn?), **24.5** (whether it still rides Plotly `heatmap` per ADR 0012 §4, or joins the Epic 24 engine).
  - [x] Verify by `git` that **you** modified no `src/SpecScribe/**` or `tests/**` file (R1 means others may have). Do not offer the golden fingerprint as evidence (R10).

- [x] **Task 10 — Completion Notes** (AC: #1–#4)
  - [x] Record: the chosen engine and its family consequence; whether ADR 0012 was superseded, extended, or left intact; the a11y pass/fail table; the CSP verdict with its boundary; the determinism verdict and layout-strategy decision; node/edge counts at each support floor; the R3 supersede/coexist recommendation; whether any escalation fired; and the timebox actually spent.

### Review Findings

**Code review 2026-08-08** (`worktree-code-review-24-6`, reviewed at `e8a689d`). Three adversarial layers —
Blind Hunter, Edge Case Hunter, Acceptance Auditor — **scoped by this story's own File List, not a commit range**,
per CLAUDE.md § Scoping a code review. The work landed in bundled commit `240afae` ("Mapping work", 82 files /
+9,169), which also carries **ADR 0029 and Stories 20.10 / 22.2 / 25.x plus 14 `web/` files — all excluded**.
Scoped diff: 41 files, +5,841 / −73.

**Verdict in one line:** the decision itself (ADR 0030 — Plotly `scatter` over a generation-time C# layout, family 1
extended, no second dependency) is **sound and survives every layer**; the 0-byte marginal cost is real and was
confirmed against the shipped asset. What does not survive intact is a **cluster of evidence defects** — the single
fixture behind almost every AC #2 number is unrepresentative, three ratified recommendations say something other
than what they do, and the report's own provenance discipline is breached in five places.

**AC coverage:** #1 PARTIALLY MET · #2 PARTIALLY MET · #3 MET (provenance caveat) · #4 MET.
**Constraints clean:** all eight prohibitions honoured (no `src/**`/`tests/**` in either diff, nothing vendored,
`GitMetrics.cs` untouched, one-way `ProjectReference`, quarantine real — `GraphEngineSpike.csproj` genuinely absent
from `SpecScribe.slnx` and CI builds the `.slnx` explicitly rather than globbing, no ADR 0005 amendment, no
`docs/live`, golden fingerprint refused). Structural-scope rule satisfied — `epics.md` **and** `sprint-status.yaml`
both moved in the same commit, and the deliberately-unseated twin-audit gap reads identically in all four artifacts.
The Task 1 `main`-vs-worktree deviation is **justified and properly flagged** in three places. Four of the five
"owed, named rather than softened" items are carried into the durable report with equal prominence.
**Story 24.2 honoured four of five hand-offs**; the fifth is D-item 3 below.

#### Decisions needed (owner)

- [ ] [Review][Decision] **The load-bearing ego fixture is unrepresentative in two independent ways, and the report never says so** — `spike/graph-engine/fixtures/ego-top20.json` is hubbed on `_bmad-output/implementation-artifacts/sprint-status.yaml`: the exact file report §7.3 denounces as the graph's pathology ("coupled to 359 of 391 nodes — 92% of the graph… mostly shows the project's bookkeeping"). Task 2 was explicit — *"Pick the ego hub deliberately: a genuinely hub-like file (`GitMetrics.cs` / `Charts.cs` / `SiteGenerator.cs`)… A quiet file proves nothing about legibility"* — and `Program.PickHub` (`Program.cs:202`) simply takes max degree, which lands on the YAML. The task is ticked `[x]`. Second: that fixture is a **complete graph** — verified 21 nodes, 210 edges = C(21,2), every node degree exactly 20. Story 24.2 renders on **code pages**, where the hub is always a code file. So the surface behind the a11y survival series, the per-edge channel census, the colour audit, the CSP render verdict, the filter timings **and the ratified top-20 default** is not the surface Epic 24 ships, and has no sparse structure, no periphery and no separable clusters — the conditions under which a force layout and a roving-tabindex order are actually hard. The report names the hub **zero times**. Options: (a) re-run the probe with a code-file hub and re-measure §5–§7; (b) annotate report + ADR with the boundary and leave the numbers standing; (c) accept as-is.
- [ ] [Review][Decision] **"Top-20 by confidence" is arithmetically identical to top-20 by raw co-change count — the ratified ranking is a no-op** — `Program.BuildCappedEgo` (`Program.cs:271-280`) computes `Confidence = (double)Support / hubChanges`, then `OrderByDescending(Confidence).ThenByDescending(Support).ThenBy(path)`. `hubChanges` is **constant** for a fixed hub, so the confidence sort is a monotone rescaling of the support sort and the tiebreaker can never fire. The discriminating direction — `conf(neighbour → hub) = support / changeCount[neighbour]`, which is the entire reason Story 24.1 built a *directional* metric — is never used for the cap. ADR 0030 § Bad (*"top-20 by confidence recommended"*) and report §7.4 therefore ratify "top-20 by co-change count" under a different name, a ranking that systematically favours the highest-churn files — which is why the resulting node list is dominated by exactly the bookkeeping §7.3 tells 24.3 to filter out, with no equivalent lens recommended for 24.2. Options: (a) amend ADR 0030 to say "top-20 by support"; (b) change the recommendation to true neighbour→hub confidence and re-measure; (c) accept.
- [ ] [Review][Decision] **Cross-platform determinism is unmeasured and — uniquely among the boundaries — unnamed, while ADR §3's normative rule omits the two actual hazards** — `Layout.Solve` uses `Math.Cos`/`Math.Sin` (`Program.cs:518-520`) and `Math.Log` (`:563`) in the coordinate path. .NET guarantees IEEE bit-exactness for `+ − × ÷` and `Math.Sqrt` (used at `:523/544/561/573`, fine) but **not** for transcendentals — they route to the platform math library and can differ across Windows/glibc/musl and x64/ARM64. `dx*dx + dy*dy` is also an FMA-fusable pattern RyuJIT contracts based on the *host's* ISA. ADR §3's normative list closes `System.Random`, iteration order, wall-clock, environment, parallelism and formatting — and stops there. FR31 is *"identical output on a from-scratch CI regen"*; the evidence is three processes on **one machine, one OS, one SDK, one arch**. §11 names seven boundaries with real candour and does not name this one, so ADR 0030 ships *"FR31 determinism is **structural**, not aspirational"* with a hole under it. This is the same hazard class the ADR bans `System.Random` for, in the ADR's own words. Options: (a) amend ADR §3 to cover transcendentals + FP contraction and add the boundary to §11; (b) add the boundary only; (c) accept.
- [ ] [Review][Decision] **Node positions have no stability across regenerations, and the ADR ratifies "position is DATA" without addressing it** — `Program.cs:512-521`: `theta = 2π·i/n` plus jitter from a single sequential `XorShift` consumed in index order. Node **identity never enters the seed**. Add, rename or delete one file — or move the support floor — and *n* changes, every index shifts, and every node's start and final position changes. FR31 (same input → same output) still holds, but "position is data" gives no *positional* stability: a reader's mental map of the graph resets on every commit and the embedded payload diffs wholesale on every regeneration. Not raised anywhere in the report or ADR. Options: (a) amend ADR 0030 to require identity-seeded initial placement; (b) hand to 24.3 as a named open item; (c) accept.
- [ ] [Review][Decision] **ADR §4's "filtering hides, never re-lays-out" is ratified on threshold evidence alone, and does not cover half the control surface the ADR itself names** — ADR 0030's Context (`:48`) names 24.3's clutter controls as *"a support/confidence threshold **and directory grouping**"*, then §4 (`:79-81`) resolves the axis using only threshold evidence. `FilterProbe.Run` (`Program.cs:676-726`) varies confidence and nothing else; no probe of grouping/collapse exists anywhere. When 24.3 collapses `src/SpecScribe/**` into one group node, that node has **no precomputed coordinate** — the layout was solved over *file* nodes only — so 24.3 must either invent a position client-side (violating ADR §2 "no client-side solver" and ADR 0010 §3) or re-solve (violating §4). Options: (a) amend §4 to scope it explicitly to threshold filtering and hand grouping to 24.3 as an open item; (b) probe grouping now and extend the rule; (c) accept.
- [ ] [Review][Decision] **The declared-throwaway spike is now permanent, unpinned infrastructure on the live dependency-alert surface** — `spike/graph-engine/package-lock.json` is **tracked and absent from the File List entirely**, and Dependabot has already acted on it: `f4f5629` *"Bump esbuild from 0.24.2 to 0.28.1 in /spike/graph-engine"* landed 2026-07-30, the day after the spike. `package.json` now reads `"esbuild": "^0.28.1"` while `measurements/bundles.json § toolchain.esbuild` records `0.24.2`, so §13's step 3 (`npm install && npm run bundles`) **no longer reproduces** the 552,268 / 657,660 / 443,319 B figures in §4.1 and in ADR 0030's options table. Separately, four probe HTML files the File List calls *"generated, gitignored"* are tracked (`.gitignore` lists only `node_modules/ dist/ bin/ obj/ probe/vendor/ echarts-src/`), and five `graph-24-6-*` probe-server entries remain seated in the shared `.claude/launch.json`. Report §12 asserts *"Deleting `spike/graph-engine/` leaves the shipped tool byte-identical."* Options: (a) prune `spike/graph-engine/` and keep only the report + ADR; (b) keep it, correct the File List, pin the toolchain and record the drift; (c) keep as-is.

#### Patches

- [ ] [Review][Patch] ADR 0030 is stale: its "still open" `StripDataIslands` item was removed outright by ADR 0036, and ADR 0032 §2 contests the premise [`docs/adrs/0030-epic-24-graph-engine.md:193-196`]
- [ ] [Review][Patch] CSP probe is now silently vacuous — the regex matches the `__CSP__` placeholder so the loud-failure guard never fires, `SHIPPED` becomes the literal `"__CSP__"`, every variant no-ops and `wrong-nonce` (the control) equals `webview`; assert `default-src` + both placeholders, read the `CspPolicy` const [`spike/graph-engine/scripts/csp-probe.mjs:36-48`]
- [ ] [Review][Patch] Tab-order hand-off is wrong and the probe comment contradicts its own code: Story 24.1's Q4 settled on **confidence**-descending, not degree-descending; correct §6.2/§10 and record 24.2's server-emission-order deviation as the correct reading [`spike/graph-engine/probe/templates/plotly-scatter.html:169-174`]
- [ ] [Review][Patch] R5's verdict rests on a hardcoded literal — `nodePositionsMoved: false` is a constant in the `.then()`, never a read-back; replace "provably"/"measurably do not move" with "by construction", and label the 10-min / 108-MB rejection projections `[PROJECTED]` and worst-case (per-state precompute needs coordinates only, ≈1.3 MB) [`spike/graph-engine/probe/plotly-scatter.html:328`]
- [ ] [Review][Patch] §7.3's five-point O(n²) fit and the whole degree distribution are labelled `[HARNESS]` but exist in no committed artifact — the `--window all` run was never persisted; relabel or commit it [`_bmad-output/implementation-artifacts/24-6-spike-report.md:520-549`]
- [ ] [Review][Patch] §2's disclaimer *"a mismatch changes a stroke, not a measurement"* is false — `IsProcessish` drives `ProcessEdges` and the entire code-only filter (the 46%-Process finding and the lens ratified for 24.3); the real classification was reachable via public `CoupledPair.Kind`/`DirectedCouple.Kind` [`spike/graph-engine/layout/Program.cs:239,445`]
- [ ] [Review][Patch] The colour audit asks "is this **a** token", not "is this a **permitted** token" — it passed on four `--status-*` tokens that `RelationshipGraph.cs:236` declares off-limits on code surfaces; also note UX-DR17 is evidenced by the dash/width census, not by the colour audit [`spike/graph-engine/probe/harness.js`]
- [ ] [Review][Patch] ECharts' UX-DR7 "PASS (configured around)" was never tested against the report's own survival rule (single snapshot, no re-render series) — and that snapshot was taken while the chart drew nothing, which §6.4 itself establishes; mark unmeasured in §6.1 and name it in §11 [`_bmad-output/implementation-artifacts/24-6-spike-report.md:289`]
- [ ] [Review][Patch] UX-DR18 PASS for candidate (a) cites `reducedMotion() ? 0 : 600` "both exercised", but those helpers are wired only in the **ECharts** probe; drop the second clause [`_bmad-output/implementation-artifacts/24-6-spike-report.md:292`]
- [ ] [Review][Patch] Line-number citations are wrong and propagated into ADR 0030 and 24.2's record: the CSP policy is at `WebviewRenderAdapter.cs:64` not `:140`, and the Plotly `<EmbeddedResource>` is at `SpecScribe.csproj:182` not `:67` (`:67` is `</PropertyGroup>`) — the report takes explicit credit for fixing this exact failure [`_bmad-output/implementation-artifacts/24-6-spike-report.md:66,399`]
- [ ] [Review][Patch] A `[HARNESS]`-labelled figure contradicts the harness: §4.1 gives shipped Plotly gzip as 413,461 B; `bundles.json` says 414,130 B in both places [`_bmad-output/implementation-artifacts/24-6-spike-report.md:144`]
- [ ] [Review][Patch] Every gzip multiple divides by prism's **minified** size (100,409 B) rather than its gzip (33,934 B, recorded in `bundles.json`) — ECharts SVG is 5.56× gzip, not 1.88×; the error direction flatters the *rejected* candidates so the decision is unthreatened, but the column header says `×prism.js (gzip)` and prism's gzip is never published [`_bmad-output/implementation-artifacts/24-6-spike-report.md:137-144`]
- [ ] [Review][Patch] Candidate (d) has no report row and no drop reason (Task 3 required one for any dropped candidate), yet carries three unmeasured ✅s in the ADR's options table under a footnote reading "measured this session"; Sigma.js/graphology likewise never mentioned or dismissed [`docs/adrs/0030-epic-24-graph-engine.md:114-116`]
- [ ] [Review][Patch] §13 "Reproducing every number" reproduces **no** §5/§6 figure — i.e. none of AC #2; the §7.2 "independently recomputed in JavaScript" cross-check is recorded nowhere; and the `file://` "reproduce in one step" is broken as committed (`probe/vendor/` is gitignored and absent, and the page still carries a literal `nonce="__NONCE__"`) [`_bmad-output/implementation-artifacts/24-6-spike-report.md:687,767-786`]
- [ ] [Review][Patch] `verify-determinism.mjs` never checks its own headline: run 1's file list is the comparison set, there is no expected-count assert (so zero fixtures prints PASS via `[].every()`), `RUNS` has no minimum (so `… 1` passes trivially), and a thrown run leaves a stale *passing* `determinism.json` on disk [`spike/graph-engine/scripts/verify-determinism.mjs:41,59-60`]
- [ ] [Review][Patch] `marginalCostOfScatterBytes: 0` is written unconditionally beside `scatterRegistered`, despite the header comment claiming the script "asserts that rather than assuming it" [`spike/graph-engine/scripts/build-bundles.mjs:143`]
- [ ] [Review][Patch] Hand 24.3 the graph states the probe never exercised: an **orphan node** (last edge filtered) stays painted, stays in tab order and keeps a stale pre-filter `aria-label`; the **empty graph** is unreachable in the probe (`foreach (var threshold in confidences)` only iterates observed values) with no fixture below 9 nodes; and a single **NaN** silently collapses every node to 0.5 while emitting valid JSON [`spike/graph-engine/layout/Program.cs:592-607,707`]
- [ ] [Review][Patch] Record the a11y boundaries the survival predicate structurally cannot see: edge `aria-label`s misalign after **any** filter (the predicate only inspects `[data-graph-node]`), and focus drops to `<body>` on re-render while held (the predicate checks `tabindexZero === 1`, which stays satisfied) — both under the "INTACT 11/11, 8/8 survived" headline [`spike/graph-engine/probe/plotly-scatter.html:218-236,350`]
- [ ] [Review][Patch] Three minor report corrections: two Cytoscape a11y cells read "not reached" instead of a literal verdict, contra the report's own *"'Partial', 'mostly' and 'with work' do not appear"*; §6.5 cites "R3" for the CSP-axis separation, which is Story **20.4's** R3, not this story's; §4.2's R9 table has no Plotly column (legitimately inheritable from 20.4, but uncited) [`_bmad-output/implementation-artifacts/24-6-spike-report.md:159,291,397`]

#### Deferred

- [x] [Review][Defer] `FilterProbe` throws `InvalidOperationException` on `supportBreakpoints.Max()` when no pair meets the support floor (shallow clone, `--window 1`, fresh repo), after writing all 11 fixtures but before `scale.json` [`spike/graph-engine/layout/Program.cs:723-725`] — deferred, throwaway probe code
- [x] [Review][Defer] `RunGit` sets `RedirectStandardError = true` with no reader and blocks in `WaitForExit` — deadlocks if git exceeds the stderr pipe buffer [`spike/graph-engine/layout/Program.cs:330-339`] — deferred, throwaway probe code
- [x] [Review][Defer] Probe text twin is truncated at 200 with no "+N more" and never rebuilt after a filter (`dataset.built` makes it idempotent) [`spike/graph-engine/probe/plotly-scatter.html:248-257`] — deferred, throwaway probe code; 24.2 server-renders its twin

#### Dismissed (5)

`BuildAside`, `ReferenceGraph`'s second call site missed by §8's "read in full" — 24.2 caught it and has since
retired `ReferenceGraph` entirely, so §8 describes a surface that no longer exists · unguarded island `JSON.parse`
crashing instead of falling back — trigger removed with `StripDataIslands` (ADR 0036) · integer emission bypassing
`InvariantCulture` — benign on .NET Core for non-negative `G`-formatted values · roving clamp measured against data
length rather than DOM length — probe-only, superseded by 24.2's shipped-and-reviewed clamp · "the determinism
harness does not really use separate processes" (a review intake lead) — **refuted**: `execFileSync` genuinely
spawns a fresh `dotnet run` per iteration into an isolated `.determinism/run-N/`, and the `scale.json` exclusion is
implemented with its stated wall-clock rationale.

## Dev Notes

### What "decide on evidence" means here, in one paragraph

ADR 0012 made a deliberate trade: it accepted **two engine families** rather than let Plotly's trace list decide
whether Epic 24 ships force-directed and chord views at all. That trade only pays off if the second family is
chosen on merit. The failure mode is not picking the "wrong" library — it is picking one *implicitly*, inside an
implementation story, without an ADR, which is exactly how three arc renderers ended up in one file
([[adr-consultation-gap-three-arc-renderers]]). The deliverable is a **decision with a paper trail**, and the most
valuable outcome may well be discovering that **no new dependency is needed at all** (R2 + R5).

### The four shapes this engine must serve

| Story | Shape | Scope | Notes |
|---|---|---|---|
| 24.2 | Force-directed ego graph | One file + 1–2 hops | Nodes sized by change frequency, edges by confidence. Must reconcile with `ReferenceGraph` (R3). |
| 24.3 | Force-directed whole-repo galaxy | Whole repo | Pan/zoom, threshold + directory-grouping clutter controls, node→code-page nav. The scale test. |
| 24.4 | Chord / arc diagram | Bounded top-N | Demoted alternate behind a toggle (UX-DR21). ECharts v6 has a native `chord` series; Plotly does not. |
| 24.5 | Adjacency-matrix heatmap | Bounded, clustered | **May ride Plotly `heatmap` regardless** (ADR 0012 §4) — it is the one Epic 24 view the hierarchy engine already covers. Do not let it distort the graph-engine choice. |

### Latest technical information (researched 2026-07-24 — verify before relying on any figure)

- **Apache ECharts 6.0.0** (released July 2025) introduced a **native `chord` series** for relationship networks,
  alongside the existing `graph` series with force layout, plus `sunburst`, `treemap`, and `heatmap`. Full bundle
  ≈1.8 MB min / ≈520 kB gz; **tree-shaken custom builds land ≈100–300 kB gz** depending on series imported. Apache
  2.0 license. It also offers an **SVG renderer** in addition to canvas — materially relevant to Task 6, because a
  canvas renderer emits no per-node DOM for a roving-tabindex layer to attach to.
  → This is the candidate that would collapse SpecScribe to **one** engine family covering both Epic 20 and
  Epic 24, at the cost of superseding ADR 0012. ADR 0012's options table pre-authorizes exactly that outcome.
- **ECharts accessibility is not a free pass.** It ships `aria.enabled` (auto-generated chart descriptions, since
  v4) and `aria.decal` (pattern fills for color-blind distinguishability, since v5 — which maps well onto R7's
  never-color-alone requirement). But there is a long-standing open issue,
  [apache/echarts#18585 *"ECharts claims to be accessible, but is not keyboard accessible"*](https://github.com/apache/echarts/issues/18585).
  **Treat keyboard traversal as unproven and test it directly** — under ADR 0013 there is no SVG behind it.
- **Plotly's own network-graph recipe** is edges as a `scatter` trace with `mode:'lines'` and nodes as a `scatter`
  trace with `mode:'markers'`, with the **layout computed externally** and fed in as coordinates (their published
  examples use NetworkX server-side). Plotly has **no built-in force layout and no chord trace** — but combined
  with R2's non-removable `scatter`, this is the zero-marginal-cost path.
- **Cytoscape.js** ≈112 kB gzipped — the richest dedicated graph toolkit (layouts and graph algorithms as
  first-class API), MIT. **Sigma.js + graphology** is a renderer/data split that scales well to large graphs.
  Both are *second-family* choices: they solve 24.2/24.3 well and leave 24.4's chord and 24.5's matrix unserved.
- Sources: [ECharts v6 features](https://echarts.apache.org/handbook/en/basics/release-note/v6-feature/) ·
  [ECharts ARIA best practices](https://echarts.apache.org/handbook/en/best-practices/aria/) ·
  [apache/echarts#18585](https://github.com/apache/echarts/issues/18585) ·
  [Plotly network graphs](https://plotly.com/python/network-graphs/) ·
  [Cytoscape.js size](https://bundlephobia.com/package/cytoscape)

### Architecture compliance

- **ADR 0012 §4** — the clause this story discharges. Two families permitted; a third needs an ADR; **every family
  routes through a component honoring the same mode / legend / text-twin contract**. That contract is the
  invariant, not the renderer.
- **ADR 0012 §3** — `navigate` | `select` mode grammar. The Epic 24 graph must adopt it, and `select` must use the
  shipped `specscribe:explorer-select` seam (R8), not a parallel one.
- **ADR 0012 §6 / AD-7** — presentation is SpecScribe's `--status-*` and brand tokens; the library's default
  palette is never permitted. Import the shipped stylesheet; never re-type a token value.
- **ADR 0013** — the text twin is the no-JS contract, and it is **Story 24.1's ranked coupled-file list** for
  every Epic 24 surface. Server-rendered, complete, navigable, non-color. There is no SVG beneath the chart.
- **ADR 0010 §3** (stands per ADR 0012 §7) — computed once at generation time and embedded. This is R5's crux.
- **ADR 0010 §4** (stands) — FR-10's no-productivity-ranking constraint is unaffected by rendering technology.
  A coupling graph shows *files*, not people; Story 7.11 owns ownership and Epic 24 deliberately does not re-do it.
- **ADR 0011** — edge direction is carrier→target. Coupling confidence is **directional** (A→B ≠ B→A, Story 24.1
  AC#1), so if the graph draws direction, it must be consistent with this convention and with 24.1's Q1 answer.
- **NFR-3 local-first** — vendored, never CDN, `file://`-safe, offline. Non-negotiable.
- **NFR-5 as amended** (PRD §8, 2026-07-24) — information and navigation survive JS-off; **visualization need
  not**, given a server-rendered text equivalent.
- **NFR10 supply-chain** (Epic 17) — if this adds a dependency it is the project's **second**. Record version,
  license, and artifact provenance.
- **FR31** — generation-time determinism; identical output on a from-scratch CI regen. AC #3's subject.

### Anti-patterns to prevent

- **Picking an engine without an ADR.** The entire reason this story exists.
- **Dismissing "Plotly scatter" as a fallback** before pricing it at its true marginal cost of zero (R2).
- **Re-measuring Plotly** instead of inheriting Story 20.4's numbers (R1).
- **Implementing Story 24.1** because the probe needs the metric. Derive it in throwaway code (R6).
- **Adding a second ego graph to the code page** without resolving R3.
- **Reporting a11y as prose.** "Mostly accessible with some work" is not a verdict — use the table.
- **Testing a canvas renderer and reporting SVG-renderer a11y**, or vice versa. Say which mode you measured.
- **Letting 24.5's matrix drive the graph-engine choice** — ADR 0012 §4 already frees it to ride Plotly `heatmap`.
- **Shipping production code, vendoring for real, or touching `src/`+`tests/`.** That is 24.2 onward.
- **Landing the ADR 0005 CSP amendment** — shared with Story 23.4; lands once.
- **Working on `main`** — background auto-committer plus a concurrent 20.4 session (R1).
- **Generating to `docs/live`** — vestigial and gitignored.
- **Offering the golden fingerprint as evidence of non-invasiveness** (R10).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/24-6-graph-engine-spike.md`
- Sprint key: `24-6-graph-engine-spike`
- **Durable deliverables:** `_bmad-output/implementation-artifacts/24-6-spike-report.md` + a ratified ADR under
  `docs/adrs/` (next free number; add to `docs/adrs/README.md`)
- Throwaway probe: `spike/graph-engine/**` (quarantined per [`spike/README.md`](../../spike/README.md))
- Sibling spike in flight: `spike/plotly/**` (Story 20.4) — read, do not disturb
- Vendoring precedent: `tools/prism-vendor/{build.js,package.json,README.md}` → `src/SpecScribe/assets/prism.js`;
  embedding at `SpecScribe.csproj:62`; conditional emission at `SiteGenerator.cs:1985-1986`
- Payload island precedent: `src/SpecScribe/SunburstExplorer.cs:62` (`SunburstExplorerDataId`)
- Surfaces 24.2–24.5 will touch (read-only for this story): `Charts.ReferenceGraph` `:2341`, `Charts.CouplingGraph`
  `:2231`, `Charts.CouplingTable` `:2193`, `CodeFileTemplater.cs:409-470`, `DeepAnalyticsTemplater.cs`,
  `SiteNav.cs` (deep-git-gated Insights nav group)
- Webview CSP + island stripping: `WebviewRenderAdapter.cs:116` (policy), `:82` (island strip regex)
- CSP probe harness to reuse: `spike/nuxt-ir/scripts/csp-probe.mjs`
- Sanctioned cross-surface divergence registry (where a webview text-twin fallback would eventually be recorded —
  **not by this story**): `src/SpecScribe/HostRenderException.cs`

### Testing standards summary

- **No new tests ship.** This story adds no production code. If you are writing in `tests/**`, you have left the
  spike.
- **Live-browser verification is mandatory** for Tasks 4–7 (CLAUDE.md § Verification). The suite structurally
  cannot see keyboard focus survival, computed colors, CSP behavior, canvas-vs-SVG DOM, or what a JS-off visitor
  gets — and all three defects that shipped past a 2,158-test green suite were caught only by looking at the
  rendered page.
- Run the suite once at the end to confirm you did not disturb it. Given R1, a failure may belong to the
  concurrent session — verify against a clean worktree of `baseline_commit` before spending time on it.
- **Determinism (AC #3) is tested by repetition, not assertion:** run the layout ≥3× and diff the bytes.

### Previous story intelligence

- **Story 20.4 (`ready-for-dev`, IN FLIGHT — R1):** The direct sibling. Its Plotly measurements are this spike's
  inherited baseline, and its **a11y decision rule** (PASS / PASS-configured-around / FAIL) is reused verbatim so
  the two spikes' verdicts are comparable. Its R1 (`scatter` is non-removable) is this spike's R2 and the single
  highest-leverage fact available.
- **Story 20.1 (`done`, spike):** Recommended zero-dependency client JS — **superseded by ADR 0012**; do not treat
  it as authority. Two cautionary lessons that transfer directly: its **edge-join rule was wrong** (`epic-20`/`20.2`
  vs `e20`/`s20.2` were disjoint → zero matches), so **verify your node-id join against real data before drawing
  anything**; and its "zero-dependency" premise was already false (`prism.js` was vendored). It also **overran its
  byte budget** (13,602 B vs 8–10 KB estimated) — treat size estimates skeptically.
  [[story-20-1-interactive-explorer-spike-seeded]]
- **Story 23.1 (`done`, spike):** The methodological parent for CSP replay, provenance labelling, and the "honesty
  boundary" section. Its post-review corrections are the mistakes not to repeat: a claimed-reproducible number that
  was not, a structurally vacuous fingerprint claim, a warm-build headline hiding a 3× cold path.
  [[story-23-1-nuxt-over-ir-spike-seeded]]
- **Story 22.1 (`done`, spike):** Report-structure ancestor; no production code, durable output was the report.
- **Story 20.2 (`done`) / 20.3 (`review`):** Shipped `SunburstExplorer.cs`, the payload-island idiom, the
  `specscribe:content-swapped` re-init seam, and the `specscribe:explorer-select` selection seam (R8). 20.3's
  owner redesign into a **card rail beside the chart** is the house idiom for "graph + ranked detail" — relevant
  because Story 24.1's ranked list is Epic 24's text twin and could occupy the same rail.
  [[story-20-2-zoomable-drill-in-done]] · [[story-20-3-related-work-pane-done]]
- **Story 21.3 (`done`):** Cited a stale memory over a two-day-old ADR that already permitted what it thought it
  was crossing. **Read `docs/adrs/` before declaring you are crossing a project rule.**
  [[adr-consultation-gap-three-arc-renderers]]
- **Story 10.7 (`done`):** Sunburst navigability at project scale — the prior art for "this chart becomes
  illegible at real repo size," directly relevant to AC #3's hairball threshold. Its `SunburstCompanionList`
  tile-grid is a precedent for bounding a dense view honestly.
- **Story 6.6 (`done`, spike):** Where SpecScribe's at-scale perf defects were found (`code-map.html` 82.5 MB,
  byte-blind chunker) — the reason AC #3 asks for at-scale numbers up front rather than after 24.3 ships.

### Git intelligence summary

HEAD at create-story is `5a96f71` ("20.3, 22.1, 23.2, 5.5"). Preceding: `f9b52bd` (20.3, 5.3), `8db18aa`
(20.1, 20.2, 20.4, 5.2 — the commit that seated the rewritten 20.4), `6e12d0d` (SCP 2026-07-24 course correction
on Epic 20 — where ADRs 0012/0013 landed), `0f0af50` (Epics 19+21 retro).

**The working tree is NOT clean at authoring time** — `src/SpecScribe/HowToReadTemplater.cs`, `SiteNav.cs`, and
three test files are modified, and `spike/plotly/` is untracked. **Assume a concurrent session** (CLAUDE.md
§ Concurrent work): grep-verify any symbol you rely on rather than trusting a prior read, never `git reset --hard`
/ `git checkout --` / `git clean`, and scope any "I changed nothing in `src/`" claim to **your own** changes.
Commits routinely bundle several stories, so scope any later review by this story's own File List, not a commit
range.

### References

- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md` — **§4** (the open question this story discharges; two families permitted, a third needs an ADR); §2 (the component contract every family must honor); §3 (`navigate`|`select`); §6 (tokens not colorways); §7 (ADR 0010 §3/§4 stand); § Options considered (the ECharts "considered and deferred, not dismissed" clause that sanctions superseding it)]
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md` — §1 amended NFR-5; §2 twin is contract; §3 the hard per-surface twin audit gate (relevant to R3); §6 golden-fingerprint replacement; §7 webview fallback]
- [Source: `docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md` — §3 generation-time computation (R5's crux), §4 no-ranking; §1/§2/§6 superseded by ADRs 0012/0013]
- [Source: `docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md` — edge-direction convention, relevant if the coupling graph draws direction]
- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 24 — epic charter, FR40, UX-DR19/20/21, NFR8; Stories 24.1–24.5 ACs; the 2026-07-24 SCP note naming the engine an open question and the 20.7 gate]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-24.md` — the correct-course session that produced ADRs 0012/0013 and revised Epic 24's foundations]
- [Source: `_bmad-output/implementation-artifacts/24-1-directional-coupling-metric-foundation.md` — the `CoupledFile` record shape this spike renders against (R6), the metric formulas, and open owner questions Q1–Q4]
- [Source: `_bmad-output/implementation-artifacts/20-4-plotly-engine-adoption-spike.md` — R1 (`scatter` non-removable → this story's R2), R2 (`--strict` CSP bundle), R3 (`style-src 'unsafe-inline'` already granted), R8 (the `prism-vendor` precedent + 100,409 B yardstick), and the a11y decision rule reused here]
- [Source: `_bmad-output/implementation-artifacts/23-1-spike-report.md` — CSP replay method, honesty boundary, provenance labelling, AC-coverage-table convention]
- [Source: `src/SpecScribe/Charts.cs` — `CouplingGraph` :2231 (ring layout + dashed process edges, the never-color-alone precedent), `CouplingTable` :2193, `ReferenceGraph` :2341 + `RefGraphArtifactNodeCap` :2325, `WorkGraph` :2655]
- [Source: `src/SpecScribe/CodeFileTemplater.cs:355-470` — the shipped "Referenced by" relationship card, `RefGraphVariants`, and the sr-only citer list (R3)]
- [Source: `src/SpecScribe/SiteGenerator.cs` — `BuildStoryRelatedEdges` :2178 / `BuildRelatedRelatedEdges` :2211 (call sites :2040-2041); `CopyEmbeddedAsset` conditional emission (~:1779-1780)]
- [Source: `src/SpecScribe/GitMetrics.cs` — `DeepGitPulse` :35, `CoChangePairs` :82, `FileInsight` :169, `ClassifyCoupling` :271, `CouplingFileSetCap` :203, `ParseNumstatLog` coupling :541, `BuildFileInsights` :802, `CoChangeCount` :900]
- [Source: `src/SpecScribe/assets/specscribe.js` — `specscribe:content-swapped` :1712/:2234, `specscribe:explorer-select` :2072/:2161, `initSunburstExplorer` :1716, `initRelatedPanes` :2166 (R8)]
- [Source: `src/SpecScribe/SunburstExplorer.cs:62` — `SunburstExplorerDataId`, the shipped payload-island idiom to mirror]
- [Source: `src/SpecScribe/WebviewRenderAdapter.cs:116` (CSP policy string), `:82` (JSON-island strip regex)]
- [Source: `src/SpecScribe/SiteNav.cs` — the deep-git-gated Insights nav group Story 24.3's page would join]
- [Source: `tools/prism-vendor/` + `src/SpecScribe/SpecScribe.csproj:62` — vendoring/embedding precedent; `assets/prism.js` = 100,409 B size yardstick]
- [Source: `spike/README.md` — quarantine discipline; `spike/nuxt-ir/scripts/csp-probe.mjs` — the CSP harness to reuse]
- [Source: `CLAUDE.md` — § Verification (live-browser rule), § Concurrent work on shared `main`, § Decision records (propose an ADR without being asked; structural scope changes land in `epics.md` AND `sprint-status.yaml` together)]

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`) — dev-story, 2026-07-29. Baseline `5a96f71` (preserved from create-story);
**executed at HEAD `630ae25`**.

### Debug Log References

* **Report:** [`24-6-spike-report.md`](./24-6-spike-report.md) — every number, with provenance labels.
* **ADR:** [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md) — **Accepted**, registered in `docs/adrs/README.md`.
* **Measurements:** `spike/graph-engine/measurements/{bundles,determinism,session}.json`, `fixtures/scale.json`.
* **Reproduce:** `spike/graph-engine/README.md` § Run it.

**Four of the story's ten reconciliations moved under it** — Epic 20 finished between create-story (07-24) and
dev-story (07-29):

* **R1 obsolete, favourably.** 20.4 is `done`; its report was inherited and **no Plotly number was re-measured**.
  `spike/plotly/` is gone. **Story 24.2's Story 20.7 gate is now satisfied.**
* **R2 upgraded from projection to shipped fact.** The story asked me to confirm non-removability against 20.4's
  build output; instead it is confirmed against **production**: `src/SpecScribe/assets/plotly-hierarchy.min.js`
  (1,223,563 B, embedded at `SpecScribe.csproj:67`) registers exactly `heatmap, scatter, sunburst, treemap`.
* **R6 obsolete.** Story 24.1 is `review`, so `CoupledFile` / `DirectedCouple` / `DirectedCoupling` /
  `IsCrossBoundary` / `CouplingMinSupport` all exist (grep-verified). The probe **calls the shipped API** instead of
  hand-deriving the metric. `GitMetrics.cs` was not edited. One flagged exception: `ClassifyCoupling` is private, so
  the probe reproduces its path-shape test locally — that changes a stroke, not a measurement.
* **R10 vindicated concretely.** **ADR 0029 was claimed by a concurrent session while this spike ran**, so this
  ADR is **0030**.

**Three probe-level mistakes, kept in the record because each looked like a library defect:**

1. **`lazyUpdate:true` on `chart.setOption` defers to an animation frame that never arrives** in a non-compositing
   tab. It presented as "ECharts renders nothing". Corrected to `lazyUpdate:false`.
2. **Classifying ECharts link paths by a `d`-shape regex** silently misclassified all 210 links as symbols during
   the pre-animation frame, when `d` is still empty. Corrected to classify by paint role (`fill="none"`).
3. **The colour audit over-reported 7 foreign colours on Plotly**, every one non-painting (4 `clipPath` rects in
   `defs`, a `fill-opacity:0` background, 2 axis paths with no `d`). Added an explicit `isPainted` predicate; the
   report gives **both** the raw and the painting count, so the 0 is defensible rather than lucky.

**Suite:** the shared tree **does not build** — `SiteGenerator.cs:1453` calls `RefreshSourceInventory`, which exists
only in stale compiled binaries. **Not this story's.** Attribution: my File List contains no `src/` or `tests/`
path; the concurrent diff is **498 insertions** across `HierarchyExplorer*` / `CodeMapTemplater` /
`DesignSystemTemplater` / `SiteGenerator` / `specscribe.css` with **zero** matches for
`coupling|CoupledFile|DirectedCouple|graph-engine|24.2|24.6`. Verified per CLAUDE.md by `git archive HEAD` into the
scratchpad (never touching the shared tree): **2,812 passed / 0 failed / 3 skipped** at `630ae25`, and the solution
still builds clean **with `spike/graph-engine/` present** — solution projects are only `SpecScribe.csproj` and
`SpecScribe.Tests.csproj`.

**Deviation from Task 1, flagged not buried:** worked on `main` under `spike/graph-engine/` rather than a branch or
worktree. CLAUDE.md § Concurrent work states isolation "is not available and is not the fix"; R1's reason for
isolation no longer holds; every file this story creates is new and exclusively its own; a worktree would have needed
a second full `dotnet build` for the probe's `ProjectReference`, and
[[worktree-edits-must-target-worktree-path]] records a real path-re-rooting defect; and every prior spike lives
committed under `spike/` on `main`. Report §12.

### Completion Notes List

**Decision — ADR 0030, Accepted: Plotly `scatter` + a generation-time C# layout. Marginal bundle cost ZERO bytes.**
ADR 0012 is **extended, not superseded** (its status stays `Accepted`); **no new engine family** — §4's allowance of
a *second* family is left **unspent**, a third still needs its own ADR — and **no second runtime dependency**.

* **Layout strategy: node position is DATA.** Solved once in C# at generation time, embedded as coordinates. No
  client force simulation, no iterative solver. **Determinism PASS: 11 fixtures byte-identical across 3 SEPARATE
  PROCESSES** (in-process repetition cannot see string-hash randomisation or JIT float contraction). Construction is
  normative in the ADR: no `System.Random` (its algorithm may change between .NET versions, so determinism would
  expire silently on an SDK bump) and **no dictionary/set iteration order may reach a float accumulation**, because
  floating-point addition is not associative.
* **R5's named weak point resolved, and it inverts the story's worry.** A confidence slider yields **236 distinct
  EDGE sets** but only **17 distinct NODE sets** — independently recomputed in JS from the emitted fixture.
  Counting node sets alone was the trap: an FR layout is a function of nodes **and edges**, so precompute-per-state
  needs 236 layouts (~108 MB, ~10 min) and is **not viable**. **Fix the positions and let filters hide** is:
  measured `nodePositionsMoved: false` at 44–75 ms, and survivors not jumping is better UX, not a compromise.
* **a11y — UX-DR7 PASS (configured around); UX-DR16/17/18 PASS.** Layer applied only via public `plotly_afterplot`;
  **no internal patched or forked**. **11/11 snapshots INTACT, 8/8 re-render events survived**, including the
  adversarial **bare `Plotly.react` the component did not initiate** and the shipped
  **`specscribe:content-swapped`** seam. **Real** `ArrowRight` / `Enter` / `Escape` verified; `Enter` fired the
  shipped **`specscribe:explorer-select`** seam, not a parallel event. **Story 20.4's sixth finding (unclamped roving
  index) is fixed by construction here — 24.2 must keep the clamp.** Tooltip is body-level `.ss-tooltip`, zero
  clipping ancestors. **0 painted foreign colours** (7 raw, all provably non-painting).
* **CSP: no relaxation needed.** Renders under the byte-verbatim shipped policy **header AND meta**;
  `script-src 'nonce-…'` alone suffices for **every** candidate; **no `'unsafe-eval'`**; and
  `style-src 'unsafe-inline'` was shown **not load-bearing** (renders correctly without it). Policy is **read out of
  `WebviewRenderAdapter.cs` at runtime** — note it has drifted from the story's `:116` to **`:140`**. Wrong-nonce =
  **blank box**, with a client-built twin contributing **0 bytes** — the concrete argument for a **server-rendered**
  twin.
* **At scale, the answer nobody will like.** Whole-repo at the shipped floor of 2 = **391 nodes / 4,864 edges**,
  median degree 14, **max degree 359** — `sprint-status.yaml` coupled to **92%** of the graph; **46%** of edges
  Process-class, **62%** cross-boundary. It is a hairball, and unfiltered it mostly shows the project's own
  bookkeeping — an *insight-quality* finding, not just legibility. **Recommend 24.3 default support ≥ 5 + Code-only
  lens** (129 / 937 / 95,514 B / 286 ms); hairball threshold **≈150 nodes**. Solver is **O(n²)** (2.6 s at 391 nodes,
  4.2 s at 489, projecting ~17 s at 1,000) — bound nodes or use Barnes–Hut above ~500. Cost is paid once by the
  generator, never by a reader.
* **The ego graph is not small either: 360 nodes uncapped.** Story 24.2 **must** cap. **Recommend top-20 by
  confidence** — 21 nodes / 210 edges / **20,253 B**, almost exactly 23.1's measured 20,915 B sunburst island.
* **ECharts 6.1.0 rejected on cost-of-change, NOT merit — recorded as time-dependent.** It is the better graph
  engine: per-edge `lineStyle` (**83** distinct widths vs 5 bands), a **native `chord` series confirmed rendering
  live** (51 real arc paths, 14 labels), `aria.decal`, an SVG renderer for ~4 KB gzip, and a unified bundle
  **566 KB SMALLER** than the Plotly bundle it would replace — an outcome ADR 0012's options table explicitly
  pre-authorised. Rejected because **Epic 20 is complete** (20.1–20.9 `done`, incl. a twin audit and a site-wide
  rollout); adopting it for graphs *only* would add a second dependency **and** family to buy what candidate (a)
  gives for 0 B; and two live defects were found. **Had Epic 20 still been in flight the recommendation would
  plausibly have inverted** — the ADR says so, so a future reopening re-prices rather than re-argues.
* **Two ECharts defects a config-level review would have missed.** (1) **`echarts.init()` on a zero-height container
  throws an uncaught `TypeError`** — reproduced deterministically after presenting as intermittent; SpecScribe
  actively creates that condition via `specscribe:content-swapped`, and **Plotly survived every zero-size case**.
  (2) **All geometry is animation-frame-gated**: at initial render every link path has `d=""` and every symbol
  `scale(0)` **while every a11y attribute passes** — so an attribute-only audit certifies a chart drawing nothing.
  `animation:false` + `lazyUpdate:false` is both the UX-DR18 knob and the way to make it measurable.
  **Hand-off: assert on geometry, not attributes** (and per 20.4, not on the console either).
* **Cytoscape 3.34.0 eliminated on accessibility before bytes — UX-DR7 FAIL.** DOM census **1 `<div>` + 3
  `<canvas>`, zero SVG, zero per-node elements**: nothing for a roving-tabindex layer to attach to, and no live SVG
  renderer exists (`cytoscape-svg` is an *export* plugin). Its one static eval-class construct is a guarded
  `Function("return this")()` that **never executes** in a browser — verified live, so **no `'unsafe-eval'`**.
* **R3 resolved: SUPERSEDE, not coexist.** The code page already has exactly **one** relationship surface and it is
  already the coupled-file surface. Decisive evidence is in shipped code: **Story 24.1 built the `ToGraphNodes`
  projection seam and its doc comment self-attributes the graph to Story 24.2** ("stays a 24.2 concern, so its
  signature deliberately does not drift here"). **Recorded as an explicit handoff** per CLAUDE.md § Scoping a code
  review, so `RelatedNode`'s metric members and `ToGraphNodes` cannot fall between the two reviews. 24.2 must absorb
  six capabilities; the **four pure-CSS variants** are the biggest scope driver — helped by report §7.2, since both
  toggles are *edge-visibility* toggles and visibility is exactly what filters can change without moving a node.
* **⚠️ Gap surfaced, not seated: retiring `ReferenceGraph`'s SVG needs an ADR 0013 §3 text-twin audit that NO Epic 24
  story owns** (20.6's scope was the hierarchy surfaces). Recommend seating it as 24.7 or folding an explicit
  twin-audit task into 24.2 — **the owner's call, not mine.**
* **Story 24.4 is candidate (a)'s one real gap.** Plotly has **no chord trace**: hand-draw SVG arcs — reading
  `docs/adrs/` first, since three arc renderers already exist — or **amend ADR 0030**. Explicitly **not** improvise a
  dependency inside an implementation story. **Story 24.5 unchanged**: rides Plotly `heatmap`, confirmed registered.
* **Escalations: none fired.** No hard a11y FAIL for the chosen candidate; `correct-course` not invoked.
* **Timebox: one session** against a suggested 2 days. **No production code**; no new tests (a test under `tests/**`
  would mean leaving the spike). Golden fingerprint **not** offered as evidence (R10).
* **Owed, named rather than softened:** live `file://` run (the pane refuses a live `file://` context — the same
  limitation 20.4 hit; structural evidence is 0 fetch / 0 ESM / 0 CDN across every page and bundle); **no
  screenshot** (the pane never composited — measured **0 rAF frames in 1,200 ms**); **`Tab` traversal itself** (the
  focus model and real arrow/Enter/Escape are verified); no screen-reader run; `vscode-resource:` + Electron
  untested, so the webview verdict is a **lower bound**; ECharts force determinism **UNMEASURED** (both runs stalled
  identically — not reported as determinism); and `StripDataIslands` means the **webview cannot receive a graph
  payload today** — the same open decision 20.4 §4.4 left for hierarchies, to be decided once for both.

### File List

**Durable deliverables**

- `_bmad-output/implementation-artifacts/24-6-spike-report.md` — **new**, the spike report
- `docs/adrs/0030-epic-24-graph-engine.md` — **new**, the ratified ADR (**Accepted**). Numbered 0030, not 0029:
  a concurrent session claimed 0029 (`0029-unscoped-shared-primitive-layer.md`) while this spike ran
- `docs/adrs/README.md` — **modified** (one appended index entry for ADR 0030; append-only, because a concurrent
  session was editing this file for ADR 0029)

**Planning / status**

- `_bmad-output/planning-artifacts/epics.md` — **modified** (a DECIDED outcome note on the Story 24.6 block; its ACs
  were already mirrored at create-story, so no AC text changed)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — **modified** (`24-6-graph-engine-spike`:
  `ready-for-dev` → `in-progress` → `review`; `last_updated`)
- `_bmad-output/implementation-artifacts/24-6-graph-engine-spike.md` — **modified** (this record: checkboxes, Dev
  Agent Record, File List, Change Log, Status)

**Throwaway probe — all new, quarantined under `spike/graph-engine/`**

- `spike/graph-engine/README.md`
- `spike/graph-engine/.gitignore` (`node_modules/`, `dist/`, `bin/`, `obj/`, `probe/vendor/`, `echarts-src/`)
- `spike/graph-engine/package.json`
- `spike/graph-engine/layout/GraphEngineSpike.csproj` — **not** in `SpecScribe.slnx`; one-way `ProjectReference` to
  `src/SpecScribe` so the fixture reads the real Story 24.1 metric
- `spike/graph-engine/layout/Program.cs` — fixture builder + seeded deterministic Fruchterman–Reingold layout +
  the filter-state probe
- `spike/graph-engine/scripts/build-bundles.mjs` — candidate bundle measurement + the R2 shipped-`scatter` assertion
- `spike/graph-engine/scripts/build-probes.mjs` — inlines a fixture as a data island, copies vendored assets
- `spike/graph-engine/scripts/csp-probe.mjs` — serves the probe under the webview CSP read from source at runtime
- `spike/graph-engine/scripts/verify-determinism.mjs` — cross-process determinism check
- `spike/graph-engine/probe/harness.js` — shared measurement surface
- `spike/graph-engine/probe/templates/plotly-scatter.html` — candidate (a)
- `spike/graph-engine/probe/templates/echarts-graph.html` — candidate (b)
- `spike/graph-engine/probe/templates/cytoscape-graph.html` — candidate (c)
- `spike/graph-engine/fixtures/*.json` — 11 emitted fixtures + `scale.json`
- `spike/graph-engine/measurements/bundles.json` — **[HARNESS]**
- `spike/graph-engine/measurements/determinism.json` — **[HARNESS]**
- `spike/graph-engine/measurements/session.json` — **[SESSION]**
- *(generated, gitignored: `spike/graph-engine/node_modules/`, `dist/`, `probe/vendor/`, `probe/*.html`,
  `layout/bin/`, `layout/obj/`, `.determinism/`)*

**Other**

- `spike/README.md` — **modified** (one appended section pointing at `spike/graph-engine/`)
- `.claude/launch.json` — **modified** (five appended probe-server entries: `graph-24-6-csp` 8131,
  `graph-24-6-meta` 8132, `graph-24-6-nocsp` 8133, `graph-24-6-wrongnonce` 8134, `graph-24-6-nostyle` 8135)

**NOT modified by this story: no path under `src/SpecScribe/**` or `tests/**`.** The tree shows concurrent-session
edits to `Charts.cs`, `CodeMapTemplater.cs`, `DesignSystemTemplater.cs`, `HierarchyExplorer.cs`,
`HierarchyExplorer.Projectors.cs`, `SiteGenerator.cs`, `assets/specscribe.css` and three test files — **none of them
this story's** (Dev Agent Record § Suite gives the attribution evidence).

## Change Log

- 2026-07-29 — Story 24.6 **implemented and moved to `review`** (dev-story; baseline `5a96f71`, executed at HEAD `630ae25`). **Decision: [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md), Accepted — Epic 24's force-directed views use the already-vendored Plotly `scatter` trace over a generation-time C# layout, at a marginal bundle cost of ZERO bytes.** ADR 0012 is **extended, not superseded** (status stays `Accepted`), **no new engine family** is added — §4's allowance of a *second* family is left **unspent** and a third still needs its own ADR — and SpecScribe acquires **no second runtime dependency**. Numbered 0030 because a concurrent session claimed 0029 mid-spike, exactly as R10 predicted. **Four of the story's ten reconciliations had moved under it**, because Epic 20 completed between create-story and dev-story: **R1** obsolete (20.4 `done`, its numbers inherited, `spike/plotly/` gone — and Story 24.2's Story 20.7 gate is now satisfied); **R2 upgraded from projection to shipped fact** — rather than confirming non-removability against 20.4's build output, it was confirmed against **production**, where `src/SpecScribe/assets/plotly-hierarchy.min.js` (1,223,563 B, embedded at `SpecScribe.csproj:67`) registers exactly `heatmap, scatter, sunburst, treemap`; **R6** obsolete (24.1 is `review`, so the probe **calls** the shipped `CoupledFile`/`DirectedCouple`/`IsCrossBoundary`/`CouplingMinSupport` API instead of hand-deriving the metric — `GitMetrics.cs` untouched); **R10** vindicated. **Node position is DATA, not presentation:** solved once in C# at generation time and embedded, with **no client force simulation**; determinism **PASS — 11 fixtures byte-identical across 3 SEPARATE PROCESSES**, and the ADR makes the construction normative (no `System.Random`, whose algorithm may change between .NET versions; no dictionary/set iteration order reaching a float accumulation, since floating-point addition is not associative). **R5's named weak point resolved, and it inverted the story's worry:** a confidence slider yields **236 distinct EDGE sets** but only **17 distinct NODE sets** (independently recomputed in JS from the emitted fixture), so counting node sets alone was the trap — an FR layout is a function of nodes *and edges*, making precompute-per-state 236 layouts (~108 MB, ~10 min) and **not viable**, while **fixing the positions and letting filters hide** measured `nodePositionsMoved: false` at 44–75 ms. **Accessibility: UX-DR7 PASS (configured around), UX-DR16/17/18 PASS** — the layer rides only Plotly's public `plotly_afterplot` with **no internal patched or forked**, **11/11 snapshots INTACT and 8/8 re-render events survived** including the adversarial bare `Plotly.react` and the shipped `specscribe:content-swapped` seam; **real** `ArrowRight`/`Enter`/`Escape` verified, with `Enter` firing the shipped `specscribe:explorer-select` seam; **0 painted foreign colours** (7 raw, all provably non-painting — a hardened `isPainted` predicate now reports both counts so the 0 is defensible rather than lucky); and Story 20.4's sixth finding, the unclamped roving index, is **fixed by construction** here. **CSP needs no relaxation:** renders under the byte-verbatim shipped policy **header AND meta**, `script-src 'nonce-…'` alone suffices for every candidate, **no `'unsafe-eval'`**, and `style-src 'unsafe-inline'` was shown **not load-bearing** — with the policy **read out of `WebviewRenderAdapter.cs` at runtime** (it has drifted from the story's `:116` to `:140`). The wrong-nonce state is a **blank box** whose client-built twin contributed **0 bytes**, which is the concrete argument for a **server-rendered** twin. **At scale the answer is unflattering:** the whole-repo graph at the shipped support floor of 2 is **391 nodes / 4,864 edges** with median degree 14 and **max degree 359** (`sprint-status.yaml` coupled to **92%** of the graph), **46%** of edges Process-class and **62%** cross-boundary — a hairball that unfiltered mostly shows the project's own bookkeeping, which is an *insight-quality* finding rather than only a legibility one; recommended 24.3 default is **support ≥ 5 with the Code-only lens on** (129/937/95,514 B), the hairball threshold is **≈150 nodes**, and the O(n²) solver (2.6 s at 391 nodes, projecting ~17 s at 1,000) needs a node bound or Barnes–Hut above ~500. **The ego graph is not small either — 360 nodes uncapped — so Story 24.2 must cap**, recommended top-20 by confidence (21/210/**20,253 B**, almost exactly 23.1's measured 20,915 B sunburst island). **Apache ECharts 6.1.0 was measured and rejected on cost-of-change, NOT merit, and that is recorded as time-dependent:** it is the better graph engine (per-edge `lineStyle` with **83** distinct widths vs 5 bands, a **native `chord` series confirmed rendering live** — 51 real arc paths and 14 labels, an SVG renderer for ~4 KB gzip, and a unified bundle **566 KB SMALLER** than the Plotly bundle it would replace, an outcome ADR 0012's options table explicitly pre-authorised), but **Epic 20 is complete** (20.1–20.9 `done`, including a text-twin audit and a site-wide rollout) so superseding ADR 0012 would reopen all of it, adopting it for graphs *only* would add a second dependency **and** family to buy what candidate (a) gives for 0 B, and **two defects a config-level review would have missed** were found live: **`echarts.init()` on a zero-height container throws an uncaught `TypeError`** (reproduced deterministically after presenting as intermittent — and SpecScribe actively creates that condition via `specscribe:content-swapped`, where Plotly survived every zero-size case), and **all geometry is animation-frame-gated** so at initial render every link path carries `d=""` and every symbol `scale(0)` **while every a11y attribute passes**, meaning an attribute-only audit certifies a chart drawing nothing (hand-off: **assert on geometry, not attributes**). **Cytoscape.js 3.34.0 was eliminated on accessibility before bytes — UX-DR7 FAIL:** its DOM census is **1 `<div>` + 3 `<canvas>`, zero SVG, zero per-node elements**, leaving a roving-tabindex layer nothing to attach to, with no live SVG renderer available; its single static eval-class construct is a guarded `Function("return this")()` that **never executes** in a browser, verified live. **R3 resolved: SUPERSEDE, not coexist** — the code page already carries exactly one relationship surface and it is already the coupled-file surface, and the decisive evidence is in shipped code, since **Story 24.1 built the `ToGraphNodes` projection seam and its doc comment self-attributes the graph to Story 24.2** ("stays a 24.2 concern, so its signature deliberately does not drift here"); **that handoff is recorded explicitly** per CLAUDE.md § Scoping a code review so `RelatedNode`'s metric members and `ToGraphNodes` cannot fall between the two reviews. **⚠️ One gap was surfaced and deliberately NOT seated:** retiring `Charts.ReferenceGraph`'s SVG is gated on an **ADR 0013 §3 text-twin audit that no Epic 24 story owns** (Story 20.6's scope was the hierarchy surfaces) — recommended as a new 24.7 or an explicit task in 24.2, the owner's call. **Story 24.4 is candidate (a)'s one real gap** (Plotly has no chord trace: hand-draw arcs, reading `docs/adrs/` first since three arc renderers already exist, or amend ADR 0030 — never improvise a dependency inside an implementation story), and **Story 24.5 is unchanged**, still riding Plotly `heatmap`. **No escalation fired**; timebox **one session** against a suggested two days; **no production code and no new tests**, with the golden fingerprint deliberately not offered as evidence. **The shared tree does not build** — `SiteGenerator.cs:1453` calls `RefreshSourceInventory`, which exists only in stale binaries — and that break is **not this story's**: the concurrent diff is 498 insertions across hierarchy/design-system/watch-mode files with zero Epic-24 matches, and a `git archive HEAD` throwaway tree (never touching the shared tree) runs **2,812 passed / 0 failed / 3 skipped** and still builds the solution clean with `spike/graph-engine/` present. **Task 1 deviation flagged, not buried:** worked on `main` under `spike/graph-engine/` rather than a branch or worktree, because CLAUDE.md § Concurrent work states isolation "is not available and is not the fix", R1's reason for it no longer holds, every file created is new and exclusively this story's, and every prior spike lives committed under `spike/` on `main`. **Owed and named rather than softened:** live `file://` run (the pane refuses a live `file://` context — the same limitation 20.4 hit), no screenshot (the pane never composited — measured **0 rAF frames in 1,200 ms**), `Tab` traversal itself, a screen-reader run, and `vscode-resource:`/Electron — so the webview verdict is a **lower bound**; ECharts force-layout determinism is **UNMEASURED** (both runs stalled identically and it is not reported as determinism); and `StripDataIslands` means the **webview cannot receive a graph payload today**, the same open decision 20.4 §4.4 left for hierarchies, to be decided once for both.

- 2026-07-24 — Story 24.6 **created** (create-story, owner-approved) as the missing Epic 24 graph-engine spike. `create-story 24.2` was halted on discovering that [ADR 0012 §4](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) defers Epic 24's graph engine to *"Epic 24's own spike"* while Epic 24 contained **no spike story** — leaving SpecScribe's second potential runtime dependency to be selected inside an implementation story, the exact failure [[adr-creation-trigger-gap-epic-10-retro]] records. Owner chose *"seed a spike story first"* over folding the decision into 24.2 or pre-deciding ECharts. Numbered **24.6 but executed after 24.1 and before 24.2** — a renumber was rejected because ADR 0012 §4 names Stories "24.2, 24.3, and 24.4" verbatim, as do `sprint-status.yaml` and project memory; Epic 23's documented non-numeric execution order (23.2→23.3→23.5→23.4) is the house precedent. Ten reconciliations recorded against shipped code, four of which change the answer: **(R2)** Plotly's `scatter` trace cannot be excluded from any bundle (per Story 20.4's R1), and Plotly's own documented network recipe is scatter-lines + scatter-markers with an **externally computed layout** — so "Plotly scatter + generation-time layout" has a marginal bundle cost of **zero bytes** and must be priced as a first-class candidate, not a fallback; **(R3)** the code page **already ships an ego graph** — `Charts.ReferenceGraph` renders the focal file hub-and-spoke with a co-changed-file node population, four pre-rendered toggle variants, and cross-edges — so 24.2's real question is supersede-vs-coexist, not which engine draws a new graph, and superseding it triggers an unowned ADR 0013 §3 text-twin audit; **(R5)** ADR 0010 §3's "computed once at generation time" survives ADR 0012 and collides with iterative force layout, making "is node position data or presentation?" the architectural crux — a seeded C# layout would make FR31 trivially true and cost zero client bytes, but 24.3's threshold/grouping clutter controls change the node set and are where that option most plausibly breaks; **(R1)** Story 20.4 is **in flight in a concurrent session** (`spike/plotly/` untracked, sibling `src/` edits present), so its Plotly numbers are inherited rather than re-measured and CLAUDE.md § Concurrent work applies at full force. Web-researched current library facts: **Apache ECharts 6.0.0 ships a native `chord` series** alongside `graph`+force, `sunburst`, `treemap`, and `heatmap` (tree-shaken ≈100–300 kB gz, Apache 2.0, offers an SVG renderer) — the one candidate that collapses SpecScribe to a single engine family, at the cost of superseding ADR 0012, which ADR 0012's own options table pre-authorizes; counterweighted by the long-open [apache/echarts#18585](https://github.com/apache/echarts/issues/18585) keyboard-accessibility issue, decision-grade under ADR 0013 because no server-rendered SVG sits behind the chart any more. Story 20.4's PASS / PASS-configured-around / FAIL a11y decision rule is reused verbatim so the two spikes' verdicts are comparable.
