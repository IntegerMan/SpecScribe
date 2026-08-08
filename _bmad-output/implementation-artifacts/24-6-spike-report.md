# Story 24.6 — Epic 24 Graph-Engine Spike: Report

**Date:** 2026-07-29 · **Story:** [24-6-graph-engine-spike.md](./24-6-graph-engine-spike.md) ·
**Baseline:** `5a96f71` (story frontmatter) · **Executed at HEAD:** `630ae25`
**Decides:** [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md) — authored by this spike, closing
[ADR 0012 §4](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)'s named open question
**Companion:** [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) ·
**Inherits:** [20-4-spike-report.md](./20-4-spike-report.md)
**Probe:** [`spike/graph-engine/`](../../spike/graph-engine/) — throwaway, quarantined, **no production code shipped**

---

## Headline

| | |
|---|---|
| **Decision** | **Candidate (a) — Plotly `scatter` + a generation-time C# layout.** Marginal bundle cost **0 bytes**. ADR 0012 is **extended, not superseded**; **no new engine family**; SpecScribe acquires **no second runtime dependency**. |
| **Is node position data or presentation?** | **DATA.** Computed in C# at generation time, embedded as coordinates. Determinism **PASS** — 11 fixtures byte-identical across **3 separate processes**. |
| **R5's named weak point (filters change the node set)** | **Resolved, and not the way the story feared.** A confidence slider has **236 distinct edge sets** but only **17 distinct node sets** — so precompute-per-state is *not* viable (236 layouts × 460 KB ≈ 108 MB), while **fix the positions and let filters hide** is: survivors measurably **do not move**, in 44–75 ms. |
| **The at-scale answer nobody will like** | The whole-repo graph at the **shipped** support floor of 2 is **391 nodes / 4,864 edges**, median degree **14**, max degree **359** — one file coupled to **92%** of the graph. **It is a hairball.** Recommended 24.3 default: **support ≥ 5** (129 nodes / 937 edges). |
| **The ego graph is not a small graph either** | The natural hub's **uncapped** 1-hop neighbourhood is **360 nodes / 4,782 edges**. Story 24.2 **must** cap. Recommended: **top-20 by confidence** (21 nodes / 210 edges / 20,253 B). |
| **R3 — code-page double graph** | **SUPERSEDE (evolve in place), not coexist.** Story 24.1 already built the projection seam and left a **named handoff to 24.2** in a doc comment. Recorded in §8, including the **unowned ADR 0013 §3 twin audit**. |
| **ECharts is technically the better graph engine, and is still not recommended** | It wins on per-edge styling (**83** distinct widths vs 5 buckets), a **native `chord` series** (confirmed live), and a **smaller** unified bundle than the shipped Plotly. It loses on cost-of-change: adopting it supersedes ADR 0012 and re-opens a **complete, shipped, twin-audited Epic 20**. §5.4. |
| **Two ECharts defects found that a config-level review would not have** | (1) `echarts.init()` on a **zero-height container throws an uncaught TypeError**; (2) **all geometry is animation-frame-gated** while every accessibility attribute passes — an attribute-only audit certifies a chart drawing nothing. §6.3, §6.4. |
| **Cytoscape** | **FAIL on UX-DR7.** DOM census is **1 DIV + 3 CANVAS, zero SVG, zero per-node elements**. No attach point for a roving-tabindex layer. §5.5. |
| **Escalations** | **None fired.** No hard a11y FAIL for the chosen candidate. `correct-course` not invoked. |

---

## 0. Corrections from the code review of 2026-08-08 — read before any number below

Three adversarial layers reviewed this report and ADR 0030. **The decision survived: candidate (a) at 0 marginal
bytes is correct, and the 0 B is real.** The corrections below are to the *evidence and the wording*, and they are
recorded here rather than silently patched in place. Where a section is corrected, the correction is repeated
inline at that section. Items marked ⚠ change how a number should be read.

**⚠ 1 — Almost every live-browser number in §5 and §6 was measured on ONE fixture, and it is unrepresentative in
two independent ways.** `fixtures/ego-top20.json` is hubbed on
`_bmad-output/implementation-artifacts/sprint-status.yaml` — the file §7.3 singles out as coupled to **92%** of the
graph — where Epic 24's ego graph renders on **code pages**, whose hub is always a code file. Task 2 asked
explicitly for a code hub (*"`GitMetrics.cs` / `Charts.cs` / `SiteGenerator.cs`… A quiet file proves nothing"*);
`Program.PickHub` just takes max degree, which lands on the YAML. **And that fixture is a complete graph** — 21
nodes, 210 edges = C(21,2), every node at degree exactly 20. So the a11y survival series, the per-edge channel
census, the colour audit, the CSP render verdict and the filter timings were taken on a surface with no sparse
structure, no periphery and no separable clusters. Verdicts topology cannot affect (CSP, no `'unsafe-eval'`, the
DOM census, per-edge dash/width control) are unaffected; **legibility, the 20,253 B payload and anything about
reading order are exposed.** The report never names the hub. *Owner decision: annotate, do not re-measure.*

**⚠ 2 — "Top-20 by confidence" (§7.4) is arithmetically top-20 by raw co-change count.** The cap computes
`Confidence = Support / hubChanges` with `hubChanges` constant for a fixed hub — a monotone rescaling of support,
so the tiebreaker can never fire. The discriminating direction, `conf(neighbour → hub) = support /
changeCount[neighbour]`, is never used. ADR 0030 now says **"by support"**, and recommends 24.3 rank by true
neighbour→hub confidence.

**⚠ 3 — §6.2's Tab order is misattributed, and was never exercised.** The report says degree-descending
*"deliberately matching the text twin's order (Story 24.1's Q4 ordering)"*. **Story 24.1's Q4 settled on
*confidence*-descending**, not degree. The probe's own comment claims confidence while its code sorts
`(b.d - a.d) || (b.w - a.w) || path`. And because every node in the fixture has degree 20, the first key was a
constant tie, so the recommended order never actually ran. **Story 24.2 contradicted this hand-off** and was right
to: it used the server's own emission order, because the twin lists citing artifacts first and then coupled files,
which a degree ranking would disagree with. This is the one hand-off 24.2 did not honour.

**⚠ 4 — §6.3's Plotly container row overstates what was measured, in the direction that later mattered.** The
table lists three zero-size cases as *"OK, 21 points drawn every time"*.
`measurements/session.json § candidateA_plotlyScatter.containerRobustness` holds **two** zero-size cases plus a
*sized* control: `sized1100x640`, `zeroHeight`, `zeroWidthAndHeight`. **The zero-*width*-only case was never
measured for Plotly** — and Story 24.2 subsequently found that *"Plotly cannot lay out in a zero-width container,
and it does not complain: it draws a chart of the wrong size"*, which is exactly the code page's pure-CSS tab
condition and cost 24.2 deferred-mount, reveal-flush and resize machinery. Plotly's zero-size robustness is one of
four stated reasons for rejecting ECharts (§5.4), so the comparison was unfair in both directions. The measurement
that *was* taken counted **points**, not geometry or size — the same "assert on geometry, not attributes" error
§6.4 hands forward as a lesson.

**⚠ 5 — Determinism was proven on one platform only, and that boundary is not in §11.** The solver uses
`Math.Cos`/`Math.Sin` and `Math.Log` in the coordinate path; .NET does not guarantee bit-exactness for
transcendentals, and `dx*dx + dy*dy` is FMA-fusable on the host's ISA. FR31 is a *cross-platform* claim. ADR 0030
§3 now bans both hazards normatively and records the scope.

**⚠ 6 — Node positions have no stability across regenerations.** Initial placement is seeded from node *ordinal*
(`theta = 2π·i/n`), not identity, so one added file moves every node. FR31 still holds. ADR 0030 §2 now requires
identity-seeded placement.

**⚠ 7 — §7.2's rejection arithmetic is worst-cased and was unlabelled.** *"236 layouts × 2.6 s ≈ 10 minutes"* and
*"236 × 460,817 B ≈ 108 MB"* carry no provenance label in a report whose §1 promises every number has one. The
108 MB assumes each state duplicates the **entire** payload; a per-state precompute needs coordinates only
(≈1.3 MB, ~80× less), and the 10 minutes charges every filtered state the *unfiltered* solve time when §7.3 shows
129 nodes solving in 286 ms. **The conclusion still stands** — an FR layout is a function of nodes *and* edges, so
236 edge sets is the right denominator — but the figures are advocacy, not measurement. Relatedly,
`nodePositionsMoved: false` was a **hardcoded literal**, not a read-back: survivors not moving is true *by
construction*, not "provably".

**⚠ 8 — §2's `IsProcessish` disclaimer is false.** It reads *"a mismatch changes a **stroke**, not a
measurement."* The local classifier drives `ProcessEdges` **and the entire code-only filter**, so it moves the
46%-Process finding, the `whole-repo-code-only` row, and the Code-only lens ADR 0030 ratifies for 24.3. The real
classification was also reachable — `ClassifyCoupling` is private, but its *result* is public on
`CoupledPair.Kind` / `DirectedCouple.Kind`, widened by 24.1's own review so the surfaces could not disagree.

**Provenance and citation corrections.** §4.1's shipped-Plotly gzip reads 413,461 B; `bundles.json` says
**414,130 B**. Every gzip ×-multiple divided by `prism.js`'s *minified* size (100,409 B) instead of its gzip
(**33,934 B**), understating gzip cost ~3× — corrected below and in ADR 0030; the error flattered the *rejected*
candidates, so the decision is unaffected. §7.3's five-point O(n²) fit and the whole degree distribution are
labelled **[HARNESS]** but exist in **no committed artifact** — the `--window all` run was never persisted, and
§13's recipe never passes `--window all`; they are **[SESSION]**. §7.2's *"independently recomputed in
JavaScript"* cross-check is recorded nowhere. The CSP policy is at `WebviewRenderAdapter.cs:64`, not `:140`;
the Plotly `<EmbeddedResource>` is at `SpecScribe.csproj:182`, not `:67` (`:67` is `</PropertyGroup>`) — both
propagated into ADR 0030 and Story 24.2's record, and this report takes explicit credit for fixing that exact
class of failure. §6.5's *"per R3"* means Story **20.4's** R3, not this story's.

**Accessibility evidence, scoped.** The survival predicate inspects only `[data-graph-node]`, so it cannot see
that **edge** accessible names misalign after any filter (the descriptor is built from the *unfiltered* edge list),
and it checks `tabindexZero === 1`, which stays satisfied when a re-render drops focus to `<body>`. Read "INTACT
11/11, 8/8 survived" as *the node layer survived*, not *focus was preserved*. **ECharts' UX-DR7 "PASS (configured
around)" was never tested against the survival rule** — one snapshot, no re-render series — and §6.4 establishes
that that snapshot was taken while the chart drew nothing; it should read **UNMEASURED**. **Candidate (a)'s
UX-DR18 cell cites `reducedMotion() ? 0 : 600` "both exercised", but those helpers are wired only in the ECharts
probe**; (a)'s pass rests solely on there being no animation to suppress. The colour audit asks *"is this **a**
token"*, not *"is this a **permitted** token"* — it passed on four `--status-*` tokens that
`RelationshipGraph.cs` declares off-limits on code surfaces; UX-DR17 is evidenced by the dash/width census, not by
the colour audit. Two Cytoscape cells read "not reached" where the report's own rule forbids non-verdicts.

**The probe has been pruned.** Per the owner's decision, `spike/graph-engine/` and its five `.claude/launch.json`
probe-server entries are removed — see §12 and §13. Its reproduce path had already decayed: Dependabot bumped the
tracked lockfile (esbuild 0.24.2 → 0.28.1) the day after the spike landed, so §13 no longer rebuilt §4.1's byte
figures; the CSP harness silently matched the `__CSP__` placeholder after `WebviewRenderAdapter` moved its policy
into a const, enforcing **no policy at all** with its own wrong-nonce control disabled; and the `file://`
"reproduce in one step" never worked as committed, because `probe/vendor/` is gitignored and the page carried a
literal `nonce="__NONCE__"`. **The numbers in this report are now the record of the measurement.**

---

## 1. Context and discipline

[ADR 0012 §4](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) deferred
Epic 24's graph engine to *"Epic 24's own spike"*. Epic 24 had no spike. This story is that spike, and it **decides**
— it does not merely validate. Timebox suggested 2 days; **spent: one session.** All four ACs were measured; every
axis that could not be measured is named in §11 rather than softened.

**Provenance labels, following the convention Story 23.1's report had to be corrected into and Story 20.4 reused:**

| Label | Meaning |
|---|---|
| **[HARNESS]** | reproducible by running a script in `spike/graph-engine/scripts/` or the C# probe; the number lands in `fixtures/*.json` or `measurements/*.json` |
| **[SESSION]** | measured once by hand in a live browser; recorded in `measurements/session.json` |
| **[PROJECTED]** | computed from a measured basis; the basis is always named |
| **[INHERITED-20.4]** | taken from `20-4-spike-report.md` rather than re-measured (R1) |
| **[DESIGN-LEVEL]** | an analysis, not a measurement. Never presented as one. |

---

## 2. Four of the story's ten reconciliations changed under it. Read this before any number.

The story was authored 2026-07-24. It executed 2026-07-29. **Epic 20 finished in between**, and that moves the
answer — mostly in candidate (a)'s favour.

### R1 is obsolete, in the good direction

Story 20.4 is **`done`**, `20-4-spike-report.md` exists and is inherited, and `spike/plotly/` has been cleaned up.
Epic 20 is complete through 20.9 with 20.10 in review. **Nothing about Plotly was re-measured** — §4 cites 20.4.

**Consequence for Story 24.2:** its Story 20.7 gate is now **satisfied** (20.7 is `done`). With 24.1 in `review`
and this spike closed, 24.2's three gates are all clear.

### R2 is no longer a projection. It is a shipped fact.

The story asked me to *"confirm the non-removability claim against the actual 20.4 build output."* I can do better:
**confirm it against production.** `src/SpecScribe/assets/plotly-hierarchy.min.js` is vendored (**1,223,563 B**),
embedded at [`SpecScribe.csproj:67`](../../src/SpecScribe/SpecScribe.csproj), and its registered trace modules are:

```
heatmap, scatter, sunburst, treemap
```

**[HARNESS]** — `measurements/bundles.json § plotlyShipped`, parsed out of the shipped asset by
`scripts/build-bundles.mjs`, which fails loudly if the file is missing.

> **`scatter` is already inside the shipped tool.** Candidate (a)'s marginal bundle cost is not "approximately
> zero" or "zero after amortisation." It is **zero bytes**, because the bytes are already there and already paid
> for by Epic 20.

### R6 is obsolete: Story 24.1 is implemented, so the probe renders the REAL metric

The story said `CoupledFile` / `DirectedCouple` were *"designed but unimplemented"* and told me to hand-derive the
metric. Story 24.1 is now `review`. Grep-verified present in `GitMetrics.cs`: `record CoupledFile`,
`record DirectedCouple`, `DeepGitPulse.DirectedCoupling`, `IsCrossBoundary`, `CouplingMinSupport`, `Lift`.

So the probe **references `src/SpecScribe` one-way and calls the shipped API** —
`GitMetrics.ParseNumstatLog`, `ParseNumstatRecords`, `BuildFileInsights(out coChangePairs)`,
`IsCrossBoundary`, `CouplingMinSupport` — instead of restating the metric. The fixture is the surface Epic 24 will
actually have, not an approximation of it. **`GitMetrics.cs` was not edited.**

One deliberate exception, flagged rather than smuggled: `ClassifyCoupling` is **private**, so the probe reproduces
its Code-vs-Process *path-shape* test locally (`Graph.IsProcessPair`). A mismatch there changes a **stroke**, not a
measurement. Story 24.2 should call the real classifier.

### R10 holds, and the working tree confirms why it matters

`GoldenContentFingerprint` cannot move for this story. More usefully: **ADR 0029 was already claimed by a
concurrent session** while this spike ran (`docs/adrs/0029-unscoped-shared-primitive-layer.md`, untracked,
README already updated). The story predicted exactly this — *"another session may have claimed one"* — so this
spike's ADR is **0030**. §12 scopes non-invasiveness to my own changes.

---

## 3. Method

* **Data under measurement:** this repository's real git history through the **production** deep-git fetch,
  copied verbatim from `GitMetrics.TryComputeDeep` (`-n 300`). Copied rather than called because `TryComputeDeep`
  wraps it in a 3-second budget that silently yields `null` on a cold run — a recorded hazard — and a probe must
  never measure a truncated window and call it scale. Measured: **300 commits, 714 files with insight,
  16,604 uncapped co-change pairs**. A **full-history** run (383 commits, 1,380 files, 19,694 pairs) is reported
  alongside as a scale sensitivity check.
* **Fixtures** are emitted as a JSON island shaped after the shipped `sunburst-explorer-data` island
  (`SunburstExplorer.cs`), so the byte comparison against 23.1's measured 20,915 B sunburst island is like-for-like.
* **Bundles** are custom **tree-shaken IIFE** builds via esbuild — minified, no sourcemap — because R9's hard
  constraint is one classic `<script nonce>` with no ES-module static imports. A published `dist` size would not
  be the honest comparison. Every size is reported as a multiple of the **already-accepted** `prism.js`
  (**100,409 B**), read from disk rather than pasted.
* **CSP: the policy string is read out of `WebviewRenderAdapter.cs` at runtime**, never pasted, and the probe
  server **fails loudly** if it cannot find it. This is an upgrade on Story 23.1's harness, whose inlined
  *"verbatim from :113"* comment had already drifted to **:140** by the time this story ran — the exact failure
  [[cite-adrs-by-symbol-not-line-number]] records.
* **Tokens** are resolved by `getComputedStyle` through the real cascade over the **generated**
  `SpecScribeOutput/specscribe.css`. **No token value is typed anywhere in the probe**, so a token change moves the
  allowlist with it.
* **Whose changes this sits on top of:** the tree carried a concurrent session's uncommitted edits to
  `src/SpecScribe/DesignSystemTemplater.cs`, `src/SpecScribe/assets/specscribe.css`, three test files, and 14
  `web/` files, plus an untracked `docs/adrs/0029-*`. The stylesheet is **under measurement** here (tokens are read
  from it), so this is recorded because CLAUDE.md requires it *and* because it could in principle move a colour
  value. It does not change any verdict: the audit checks *"is every painted colour a token"*, which is invariant
  to what the token's value happens to be.

---

## 4. AC #1 — the candidate matrix

### 4.1 Bundle sizes, measured

| Candidate | min | min+gzip | ×`prism.js` (min) | ×`prism.js` (gzip) | Engine-family consequence |
|---|---:|---:|---:|---:|---|
| **(a) Plotly `scatter` + generation-time C# layout** | **0** | **0** | **0.00×** | **0.00×** | **No new family.** Extends family 1. |
| (b) ECharts `graph`+`chord`, **SVG** renderer | 552,268 | 188,594 | 5.50× | 5.56× | **Second family** (or first-and-only if it also replaces Plotly) |
| (b′) ECharts `graph`+`chord`, canvas renderer | 544,779 | 184,890 | 5.43× | 5.45× | as above |
| (b″) **ECharts unified** — `graph`+`chord`+`sunburst`+`treemap`+`heatmap`, SVG | 657,660 | 223,108 | 6.55× | 6.57× | **Collapses to ONE family — supersedes ADR 0012** |
| (c) Cytoscape.js | 443,319 | 141,961 | 4.42× | 4.18× | **Second family**, and serves neither 24.4's chord nor 24.5's matrix |
| *(reference)* shipped `plotly-hierarchy.min.js` | 1,223,563 | 414,130 | 12.19× | 12.20× | family 1, **already paid for** |

**[HARNESS]** — `npm run bundles`, `measurements/bundles.json`. Versions: **echarts 6.1.0**, **cytoscape 3.34.0**,
esbuild 0.24.2, Node v24.11.1. All Apache-2.0 / MIT.

**Two corrections from the code review of 2026-08-08.** The `min` multiples divide by `prism.js` **minified**
(100,409 B), which is right; the `gzip` multiples originally divided by that same minified figure instead of
`prism.js` **gzipped** (**33,934 B**, recorded in `measurements/bundles.json § yardsticks`), understating every
gzip multiple roughly 3× — ECharts SVG read 1.88× where it is 5.56×. The figures above are corrected and are now
like-for-like. The error ran **in favour of the rejected candidates**, so it never threatened the decision:
candidate (a) is 0 B on both axes. Separately the shipped-Plotly gzip cell read **413,461 B**, a figure that
appears nowhere in the harness output; `bundles.json` reports **414,130 B** in both `yardsticks` and
`plotlyShipped`, and that is what is shown now.

**On the toolchain line:** `esbuild 0.24.2` is what produced these bytes. The spike's tracked lockfile was
subsequently bumped to `^0.28.1` by Dependabot (`f4f5629`, 2026-07-30), so a re-run would **not** have reproduced
these figures even before the probe was pruned. Treat the table as the record, not as something to regenerate.

Two things in that table deserve to be said out loud rather than left for a reader to notice:

1. **The SVG renderer is nearly free.** ECharts SVG costs **+7,489 B min / +3,704 B gzip** over canvas. Since a
   canvas renderer is an a11y dead end (§5.5), this is the cheapest important line in the table.
2. **ECharts unified is *smaller than the Plotly bundle it would replace*** — 657,660 B against 1,223,563 B, a
   **566 KB reduction**, while covering Epic 20's three hierarchy shapes *and* all four of Epic 24's. That is the
   strongest technical argument in this spike, and §5.4 explains why it still loses.

### 4.2 R9's hard constraints, counted statically over every emitted artifact

| Construct | echarts-graph-svg | echarts-unified-svg | cytoscape |
|---|---:|---:|---:|
| `new Function(` | 0 | 0 | 0 |
| `Function('…')` | 0 | 0 | **1** |
| `eval(` | 0 | 0 | 0 |
| dynamic `import(` | 0 | 0 | 0 |
| ESM static `import` / `export` | 0 / 0 | 0 / 0 | 0 / 0 |
| `fetch(` | 0 | 0 | 0 |
| `XMLHttpRequest` | 0 | 0 | 0 |
| `WebSocket` | 0 | 0 | 0 |
| CDN / unpkg / jsdelivr URLs | 0 | 0 | 0 |

**[HARNESS]**. Cytoscape's single hit is a guarded lodash-style `Function("return this")()`, reached only if both
the `freeGlobal` and `self` checks fail. **In a browser `self` short-circuits it, so it never executes and no
`'unsafe-eval'` is required — verified live (§5.5), not inferred from the static count.**

### 4.3 Coverage of the four Epic 24 shapes

| Shape | (a) Plotly scatter | (b) ECharts | (c) Cytoscape |
|---|---|---|---|
| 24.2 ego force-directed | ✅ generation-time layout, drawn as `scatter` lines+markers | ✅ `graph` + `layout:'none'` | ✅ `preset` layout |
| 24.3 whole-repo force-directed | ✅ same | ✅ same | ✅ same |
| 24.4 chord / arc | ❌ **no chord trace** — hand-drawn SVG arcs | ✅ **native `chord` series, confirmed live** (§5.3) | ❌ none |
| 24.5 adjacency matrix | ✅ **`heatmap` already in the shipped bundle** | ✅ `heatmap` | ❌ none |

24.4 is candidate (a)'s only real gap, and §10 says what to do about it.

---

## 5. AC #1/#2 — what the live browser showed

All of §5 is **[SESSION]**, `measurements/session.json`.

### 5.1 The environment boundary, stated first because it changes two readings

The in-app Browser pane **does not composite**: measured **0 `requestAnimationFrame` frames in 1,200 ms**, with
`document.visibilityState === "hidden"`. Anything an engine defers to an animation frame never advances here.

This is a **measurement** boundary, not a user-facing defect, and it is flagged at each point where it matters
(§5.2, §6.3, §6.4, §11). It is also the reason no screenshot exists (§11) — the same limitation Story 20.4 hit.

### 5.2 Candidate (a) draws synchronously; candidate (b) does not

Measured in the **same** stalled tab, so this is a like-for-like comparison:

| | edge geometry | node geometry | draws with zero frames? |
|---|---|---|---|
| **(a) Plotly scatter** | **210/210** paths with a non-empty `d` **and** a real bbox | **21/21** | **YES** |
| **(b) ECharts** (`animation:true`) | **0/210** — `d` is `""` | **0/21** — `transform: matrix(0,0,0,0,x,y)`, scale **zero** | **NO** |
| **(b) ECharts** (`animation:false`, `lazyUpdate:false`) | **210/210** | **21/21** | YES, in 62.1 ms |

**A measured correction to this probe's own first assumption:** Plotly emits **one `path.js-line` per
null-separated segment**, so per-edge *DOM* does exist (210 paths for 210 edges). What is trace-level is the
*style*. The honest statement is therefore **per-edge DOM: yes; per-edge style: no** — not "no per-edge DOM", which
is what the probe initially wrote down.

### 5.3 Per-edge non-colour channels (R7 / UX-DR17), read off computed styles

The shipped precedent is `Charts.CouplingGraph`: process coupling gets a **dashed stroke plus a `<title>` suffix**,
never a hue change. Both candidates clear it; they clear it differently.

| | (a) Plotly scatter | (b) ECharts |
|---|---|---|
| distinct dash patterns | **6** | 4 |
| distinct stroke widths | **5** (bucketed — trace-level) | **83** (genuinely per-edge) |
| distinct strokes (hues) | 2 | 2 |
| every process edge dashed | ✅ | ✅ |
| every edge carries accessible text | ✅ 210/210 | ✅ 210/210 |
| mechanism | 10 traces = 4 semantic classes × 3 width buckets | native `links[].lineStyle` |

**Candidate (a)'s width channel is quantised into buckets and that is a real, if modest, expressiveness loss.**
Confidence must be read from the tooltip and the text twin, not from stroke width alone. It does **not** breach
UX-DR17 — dash, width bucket, node shape and text are four independent non-colour channels — but 24.2/24.3 should
not promise a continuous width encoding.

**ECharts' native `chord` series is confirmed, not inherited from release notes:** 14 nodes / 37 links → **51
paths, every one carrying arc/curve commands**, plus 14 real text labels, zero errors, from the same fixture.

### 5.4 Why the better graph engine still loses

On the graph axis alone ECharts wins: per-edge styling, a native chord series, `aria.decal` patterns, an SVG
renderer for ~4 KB gzip, and a unified bundle **566 KB smaller than the Plotly bundle it would replace**. ADR 0012's
own options table pre-authorises choosing it and superseding ADR 0012 as *"the expected outcome rather than a
failure."* So this is not a case of declining a sanctioned outcome on principle. The reasons are concrete:

1. **Epic 20 is finished, not in flight.** 20.1–20.9 are `done`, 20.10 is in `review`. Adopting ECharts for the
   hierarchy family would re-open a spike (20.4), a component (20.5), a **text-twin audit** (20.6), a **site-wide
   rollout** (20.7), a details pane (20.8), and colorized hierarchies (20.9). The 566 KB saving is real; it does
   not buy that.
2. **Adopting ECharts for graphs ONLY makes things worse, not better.** It adds a **second** runtime dependency
   and a **second** engine family — 552,268 B on top of Plotly's 1,223,563 B — to obtain what candidate (a)
   already delivers for **0 B**. ADR 0012 permits a second family; permission is not a reason.
3. **Two defects a config-level review would not have found** (§6.3, §6.4), one of which is an **uncaught
   TypeError** in a failure mode SpecScribe actively creates.
4. **`scatter` is unremovable.** Even choosing ECharts everywhere, Plotly's `scatter` would have shipped anyway if
   Plotly shipped at all. Candidate (a) spends bytes that are already spent.

**If Epic 20 were still in flight, this spike's recommendation would plausibly invert.** That is worth recording,
because it means the decision is a function of *timing*, not only of merit — and if ADR 0012 is ever reopened for
an independent reason, ECharts should be re-priced with §4.1's numbers in hand.

### 5.5 Cytoscape — UX-DR7 FAIL, on a DOM census

Cytoscape loads and runs under the byte-verbatim shipped policy with **zero errors**, correctly consumes the
generation-time coordinates via its `preset` layout, and does **not** trip `'unsafe-eval'`. Its DOM is:

```
{ "DIV": 1, "CANVAS": 3 }     svgs: 0     per-node DOM elements: 0
```

**A canvas renderer emits no per-node DOM, so a roving-tabindex layer has nothing to attach to.** Reaching UX-DR7
would mean building and continuously synchronising a parallel overlay of focusable elements over the canvas on
every pan, zoom and filter — that is writing a second renderer, not configuring around the first. Per Story 20.4's
decision rule (FAIL = *requires forking internals, or the augmentation has no supported hook*), this is a **FAIL**.
Cytoscape core ships no live SVG renderer; `cytoscape-svg` is an **export** plugin. Candidate (c) is eliminated on
accessibility before bytes are discussed.

---

## 6. AC #2 — accessibility and CSP, per candidate

Verdicts use **Story 20.4's decision rule verbatim**, so the two spikes are comparable:
**PASS** = documented configuration alone · **PASS (configured around)** = post-render DOM augmentation over the
emitted output plus public events, **surviving** re-render · **FAIL** = requires forking internals, or the
augmentation is destroyed with no supported hook. *"Partial"*, *"mostly"* and *"with work"* do not appear.

### 6.1 The table

| UX-DR | Requirement | **(a) Plotly scatter** | **(b) ECharts** | **(c) Cytoscape** |
|---|---|---|---|---|
| **UX-DR7** | Tab order, Enter/Space activate, Escape up; per-node names | **PASS (configured around)** — §6.2, 11/11 INTACT, real keys verified | **PASS (configured around)** — layer applies over emitted SVG | **FAIL** — no per-node DOM (§5.5) |
| **UX-DR16** | Accessible name, announced state | **PASS** — `role="graphics-document"` + name; `aria-live` announced *"Selected src/SpecScribe/SiteGenerator.cs"*, *"View reset to all 21 files"* | **PASS** — same layer | **FAIL** — nothing to name |
| **UX-DR17** | Never colour alone | **PASS** — 6 dashes / 5 widths / shape / text; **0 painted foreign colours** | **PASS** — 4 dashes / 83 widths / text; **0 foreign, raw or painted** | not reached |
| **UX-DR18** | `prefers-reduced-motion` snaps | **PASS** — no animation exists to suppress; the drill is a `restyle`, `reducedMotion() ? 0 : 600` both exercised | **PASS (configured around)** — `animation:false` + `lazyUpdate:false` renders settled synchronously in 62.1 ms (§6.4) | not reached |

The a11y layer for both (a) and (b) is applied **only** through public post-render events —
`plotly_afterplot` and ECharts' `rendered`. **No engine internal is patched, forked or monkeyed**, which is what
makes these "configured around" rather than forks.

### 6.2 UX-DR7 is the crux, and candidate (a) clears it

A node-link graph has no natural reading order, so one had to be **chosen**: **degree-descending, then weight, then
ordinal path** — deliberately matching the text twin's order (Story 24.1's Q4 ordering) rather than the DOM order
Plotly happens to emit. **Recommend 24.2/24.3 adopt the same rule**, so the twin and the graph agree.

Survival predicate, applied mechanically after every event: *nodes > 0 **and** every node carries a role **and**
every node carries a non-empty `aria-label` **and** exactly one node holds `tabindex="0"`.*

| # | Event | Nodes | **INTACT** |
|---|---|---:|---|
| 0 | initial render | 21 | ✅ |
| 1 | focus lands on a node | 21 | ✅ |
| 2 | arrow ×3 | 21 | ✅ |
| 3 | Enter activate | 21 | ✅ |
| 4 | confidence filter 50% (`Plotly.restyle`) | 21 | ✅ |
| 5 | resize (`Plotly.Plots.resize`) | 21 | ✅ |
| 6 | **bare `Plotly.react` the component did not initiate** | 21 | ✅ |
| 7 | `Plotly.relayout` | 21 | ✅ |
| 8 | **`specscribe:content-swapped`** (the shipped SPA re-init seam) | 21 | ✅ |
| 9 | Escape reset | 21 | ✅ |
| 10 | filter back to 0% | 21 | ✅ |

**11/11 snapshots INTACT; 8/8 re-render events survived** (steps 3–10). Step 6 is the adversarial one and the
reason the verdict is trustworthy: an update path the component did **not** initiate still fires
`plotly_afterplot`, so the layer is restored. Under the decision rule, a layer surviving only the component's own
redraw would have been a **FAIL**.

**Real keyboard events, not synthetic dispatch:**

| Key | Result |
|---|---|
| `ArrowRight` | moved focus `deferred-work.md` → `SiteGenerator.cs` |
| `Enter` | activated: announced *"Selected src/SpecScribe/SiteGenerator.cs"* **and fired the shipped `specscribe:explorer-select` seam** (R8) — not a parallel event |
| `Escape` | reset: announced *"View reset to all 21 files"* |
| `Tab` | **NOT VERIFIED** — see §11 |

**Story 20.4's sixth finding is fixed by construction here, not carried forward.** 20.4 recorded that its probe's
roving `focusIndex` was not reclamped after a drill shrank the node count, leaving the chart Tab-unreachable. This
probe clamps on every reapply. **Story 24.2 must keep that clamp.**

Tooltips route through the body-level `.ss-tooltip` node (R8): `isBodyLevel: true`, **zero clipping ancestors**,
no CSS `::after`.

### 6.3 ECharts defect 1 — `init()` on a zero-height container throws

This surfaced as an **intermittent uncaught `TypeError: Cannot read properties of null (reading '0')`** on initial
page load. It reproduced on two ports and not a third, then **0/6 times** when the identical option object was
replayed from the console with a sized container. Bisected to a deterministic cause:

| Container | Result |
|---|---|
| 1100 × 640 | OK, 231 paths |
| 1100 × **0** | **THROWS** `TypeError: Cannot read properties of null (reading '0')` |
| **0 × 0** | **THROWS** — same |
| 0 × 640 (zero width only) | OK — renders at width 100 |
| *Plotly, the two zero-size cases measured* | **OK, 21 points** (`zeroHeight`, `zeroWidthAndHeight`) |

> **⚠ Corrected 2026-08-08 by code review.** This row previously read *"Plotly, all three zero-size cases — OK, 21
> points drawn every time."* `measurements/session.json § candidateA_plotlyScatter.containerRobustness` contains
> only `sized1100x640` (a **sized control**, not a zero-size case), `zeroHeight` and `zeroWidthAndHeight`. **The
> zero-*width*-only case was never measured for Plotly** — and it is the one that mattered: Story 24.2 later found
> that *"Plotly cannot lay out in a zero-width container, and it does not complain: it draws a chart of the wrong
> size,"* which is precisely the code page's pure-CSS tab state (`display:none` at mount ⇒ zero width) and cost
> 24.2 deferred-mount, reveal-flush and resize machinery. Because Plotly's zero-size robustness is one of the four
> stated reasons for rejecting ECharts (§5.4), the comparison was unfair in **both** directions. Note also that
> what was measured is a **point count**, not geometry or size — the same attributes-not-geometry error §6.4 hands
> forward as a lesson. **The ECharts finding is unaffected:** its zero-height `TypeError` was reproduced
> deterministically and is a hard throw, not a silent mis-size.

**It is not a CSP failure** — it reproduces with CSP entirely **off**. It is a *layout race*: on some loads `#chart`
had not yet been laid out when the inline script ran.

Why this matters more for SpecScribe than for a typical app: the generator injects charts into arbitrary page
positions, and the shipped **`specscribe:content-swapped`** seam re-initialises into a `<main>` that is *mid-swap* —
a prime zero-height moment. The failure mode is an **uncaught TypeError, not a graceful no-op**, and under ADR 0013
there is no SVG beneath the chart, so a reader gets a blank box plus a console error. Mitigable with a
size guard plus re-init on resize — so a hazard with a mandatory guard, not a FAIL.

### 6.4 ECharts defect 2 — geometry is animation-gated while every a11y attribute passes

At the instant of initial render with `animation:true`, the emitted SVG contains **every** path — and:

* all **210** link paths have `d=""`;
* all **21** symbol paths have `transform: matrix(0,0,0,0,x,y)` — **scale zero**;
* and the a11y snapshot reports **`INTACT: true`** — 21 roles, 21 non-empty accessible names, exactly one
  `tabindex="0"` — with **zero** console errors and correct per-edge `stroke-dasharray` on all 210 links.

> **An audit that checks attributes but never geometry certifies a fully-conformant ECharts graph that is drawing
> nothing.**

Bisected cleanly, ruling out `aria.decal` and the palette override:

| Option | paths with geometry |
|---|---|
| `animation:false`, no aria | **231 / 231** |
| `animation:true`, no aria | 21 / 231 |
| `animation:false` + `aria.decal` | **231 / 231** |
| `animation:true` + `aria.decal` | 21 / 231 |
| `animation:false` + decal + `color[]` | **231 / 231** |
| `animation:true` + decal + `color[]` | 21 / 231 |

In a compositing browser this resolves within a frame and **no reader ever sees it** — so it is not a user-facing
defect. The hazard is to **automated verification**: exactly the same class as Story 20.4's note that *CSP
violations did not appear in console captures*. It is recorded here because Story 23.4 owes a CSP regression test
and Epic 24 owes a twin audit, and both would pass against a blank chart.

`animation:false` with **`lazyUpdate:false`** renders the settled state synchronously (62.1 ms). One switch answers
UX-DR18 and makes the engine measurable. **`lazyUpdate` must be false** — passing `true` defers the update to a
frame that never arrives, and this probe made exactly that mistake first; it looked identical to a rendering defect.

### 6.5 The webview CSP axis — script and style reported separately, per R3

The policy is read from `WebviewRenderAdapter.cs` at runtime (now at **:140**, not the story's `:116`).

| Variant | Delivery | (a) Plotly scatter | (b) ECharts |
|---|---|---|---|
| **Shipped policy, byte-verbatim** | **HTTP header** | **RENDERS** — 0 errors, 210/210 geometry, a11y INTACT, 0 foreign colours | **RENDERS** — 0 errors, SVG renderer (1 svg / 0 canvas) |
| **Shipped policy, byte-verbatim** | **`<meta http-equiv>`** | **RENDERS** — 0 errors, 210/210 geometry, a11y INTACT, 0 foreign colours | not separately re-measured (§11) |
| Shipped policy **minus** `style-src 'unsafe-inline'` | header | **RENDERS** — 210/210, a11y INTACT, 0 foreign colours, 0 errors. The probe's own inline `<style>` **was** blocked (1 stylesheet survives, not 2); the chart was unaffected because tokens come from the **external** sheet | — |
| Shipped policy, **wrong nonce** (partial relaxation) | header | **BLANK BOX** — see below | — |
| `script-src` + `'unsafe-eval'` | — | **not needed by any candidate** | **not needed** |

**Script axis:** the shipped `script-src 'nonce-…'` **alone is sufficient** for every candidate. No
`'unsafe-eval'`. **Style axis:** `style-src 'unsafe-inline'` is already granted **and is not load-bearing** for
candidate (a) — it renders correctly without it. Collapsing the two axes would have reported a gap that does not
exist, exactly as R3 warned.

**The partial-relaxation state, mirroring Story 20.4 §4.3 — and it is worse here in one specific way:**

```
harnessLoaded: false   plotlyLoaded: false   chartInnerHTMLBytes: 0   svgsInChart: 0   graphNodes: 0
textTwinBytes: 0       islandStillInDom: 20,253 B (dead weight)
```

The probe builds its text twin **client-side**, so under a half-applied policy the fallback contributed
**zero bytes**. That is not a criticism of the probe — it is the measurement. **Epic 24's text twin must be
server-rendered** (ADR 0013 §2 already requires it); this quantifies what "must" buys: 0 bytes versus a complete
ranked list. §10 hands this to 24.2/24.3.

**Honesty boundary, inherited from Story 23.1 §Axis 3 and Story 20.4 §4.5 — narrowed but not eliminated:**

* ✅ **Meta delivery was tested** for candidate (a), not just header delivery. Same verdict both ways.
* ✅ **The policy was read from source at runtime**, so it cannot silently drift out from under this verdict.
* ❌ **`vscode-resource:` URI delivery is untested.** The probe served over `http://localhost`.
* ❌ **No Electron paint. No real extension host.** The nonce came from a stand-in server, not the shim.
* ❌ **`WebviewRenderAdapter` strips every `<script type="application/json">` island** (`StripDataIslands`), so
  **the webview cannot receive a graph payload at all today.** Story 20.4 §4.4 raised this for the hierarchy
  family and left it to 20.5; the same decision recurs for Epic 24 and **this spike does not make it** — §10.

**The verdict above is a lower bound on the webview gap, not a characterization of it.**
**The ADR 0005 CSP amendment was NOT authored here** — per ADR 0012 §5 it lands once, jointly with Story 23.4.
This spike's contribution to it is that **no relaxation of the policy string is required.**

---

## 7. AC #3 — layout strategy, determinism, and scale

### 7.1 Node position is DATA. Determinism: PASS.

The layout is a seeded Fruchterman–Reingold pass in throwaway C# over the real Story 24.1 metric, emitting
`{id, p, l, x, y, w, d, b}` nodes and `{a, b, s, cab, cba, lift, xb, k}` edges.

Every source of run-to-run variation was closed deliberately, and the reasons are the interesting part:

* **No `System.Random`.** A private xorshift128+ with a compile-time seed. `Random`'s algorithm is documented as an
  implementation detail that may change between .NET versions — which would make a "deterministic" layout
  deterministic only until the SDK moved under it, precisely the silent break AC #3 asks about.
* **No dictionary or `HashSet` iteration ever reaches a float.** Every collection is materialised through an
  explicit **ordinal sort** first, because .NET's dictionary order is an implementation detail and floating-point
  addition is **not associative** — an order change moves the last bits of every coordinate.
* No wall-clock, no environment, no parallelism; all formatting through `InvariantCulture` with a fixed format.

| Check | Result |
|---|---|
| In-process, 3 runs | **byte-identical** |
| **3 SEPARATE PROCESSES, 11 fixtures** | **byte-identical — PASS** |

**[HARNESS]** — `node scripts/verify-determinism.mjs 3`, `measurements/determinism.json`. Separate processes are
the load-bearing check: in-process repetition cannot see string-hash randomisation, allocation-order effects, or
tiered JIT changing floating-point contraction. `scale.json` is **excluded** because it carries wall-clock solve
timings — a **stated exclusion**, not a convenience.

**One incidental data finding:** the payload rounds coordinates and confidences to 4 decimals, and that rounding
**collapses distinct confidence values** — 452 distinct values survive in the emitted fixture where 453 exist
upstream. Harmless at this precision, but it means the payload's precision is a **data** decision, not a cosmetic
one. 24.2 should choose it deliberately.

### 7.2 R5's named weak point: the filter interaction. Measured, and it inverts the story's worry.

The story flagged this as *"where this option most plausibly breaks"*: filtering **changes the graph**, so either
the layout re-runs client-side or every filter state is precomputed.

A confidence slider is continuous, but the graph only changes at a value where an edge drops out. Over the real
data:

| | count |
|---|---:|
| distinct one-directional confidence values | **453** |
| **distinct slider breakpoints = distinct EDGE sets** | **236** |
| distinct **NODE** sets | **17** |
| distinct support values (max) | 56 (max support **116**) |

**[HARNESS]** — `fixtures/scale.json § filterStates`, and **independently recomputed in JavaScript from the
emitted fixture** as a cross-check: 236 and 17, matching exactly.

**Counting node sets alone would have been the trap.** 17 is a small, cheap-looking number and it is the wrong
one: a Fruchterman–Reingold layout is a function of nodes **and edges**, so precompute-per-state must be keyed on
the **edge** set. Node sets are stable only because a node rarely loses its *last* edge.

| Strategy | Verdict |
|---|---|
| **Precompute one layout per reachable state** | **NOT VIABLE.** 236 layouts × 2.6 s ≈ **10 minutes** of generation time and 236 × 460,817 B ≈ **108 MB** of payload. The `RefGraphVariants` four-variant idiom does not survive a continuous slider. |
| **Re-solve client-side on every filter change** | Rejected — it reintroduces a client solver, forfeits FR31, and costs 2.6 s per change at whole-repo scale. |
| **✅ Solve ONCE at the most inclusive threshold; filters HIDE, never move** | **VIABLE and recommended.** Measured: `nodePositionsMoved: **false**` at both 50% and 0%, in **75.2 ms** and **44.3 ms**. Zero extra payload, FR31 trivially true, and survivors do not jump — which is *better* UX than a re-settling graph, not a compromise. |

### 7.3 At-scale: the whole-repo view is a hairball at the shipped default

`-n 300` window (production's own fetch): 300 commits, 714 files, 16,604 uncapped pairs. **[HARNESS]**

| Fixture | Support floor | Nodes | Edges | Cross-boundary | Process | Payload B | Max degree | Components | C# solve |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| whole-repo | **2 (shipped)** | **391** | **4,864** | 3,032 | 2,239 | 460,817 | **359** | 6 | 2,611 ms |
| whole-repo | 3 | 267 | 2,247 | 1,368 | 942 | 227,936 | 243 | 1 | 1,177 ms |
| whole-repo | **5** | **129** | **937** | 582 | 350 | **95,514** | 116 | 2 | 286 ms |
| whole-repo | 8 | 73 | 429 | 267 | 174 | 45,252 | 65 | 1 | 98 ms |
| whole-repo | 12 | 50 | 222 | 139 | 97 | 24,952 | 41 | 1 | 46 ms |
| whole-repo **code-only** | 2 | 230 | 2,625 | 1,274 | 0 | 245,974 | 197 | 2 | 908 ms |
| ego, 1 hop **uncapped** | 2 | **360** | **4,782** | 2,978 | 2,201 | 449,346 | 359 | 1 | 2,223 ms |
| ego, 2 hop uncapped | 2 | 381 | 4,859 | 3,032 | 2,235 | 459,047 | 359 | 1 | 2,564 ms |
| ego, **top-8** (shipped cap) | 2 | 9 | 36 | 27 | 26 | **4,297** | 8 | 1 | 3.0 ms |
| ego, **top-20** | 2 | **21** | **210** | 119 | 74 | **20,253** | 20 | 1 | 15.1 ms |
| ego, top-40 | 2 | 41 | 650 | 353 | 185 | 58,861 | 40 | 1 | 53.7 ms |

Degree distribution at floor 2: **median 14**, p95 **82**, max **359**, 14 nodes at degree ≥ 100, 34 at ≤ 3.

**The top of the graph is the project's own bookkeeping, not its code:**

| Degree | Changes | Path |
|---:|---:|---|
| **359** | 179 | `_bmad-output/implementation-artifacts/sprint-status.yaml` |
| 290 | 156 | `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` |
| 277 | 114 | `src/SpecScribe/SiteGenerator.cs` |
| 272 | 141 | `src/SpecScribe/assets/specscribe.css` |
| 236 | 118 | `_bmad-output/implementation-artifacts/deferred-work.md` |
| 182 | 92 | `src/SpecScribe/Charts.cs` |

`sprint-status.yaml` is coupled to **359 of 391 nodes — 92% of the graph** — because every `dev-story` touches it.
**46% of all edges are Process-class and 62% are cross-boundary.** An unfiltered whole-repo coupling graph mostly
shows the project's bookkeeping, which is a **finding about insight quality**, not only about legibility. The
Story 10.6 Code/Process lens is the shipped answer and 24.3 should use it: code-only nearly **halves** the graph
(391 → 230 nodes, 4,864 → 2,625 edges).

**Full-history sensitivity check** (383 commits, 1,380 files, 19,694 pairs): 489 nodes / 6,559 edges at floor 2,
solving in **4,183 ms**. Fitting the five real data points, the solver scales as **≈ O(n²)**:
(489/308)² = 2.52 against a measured 4,183/1,583 = 2.64.

| n | measured solve |
|---:|---:|
| 55 | 59 ms |
| 78 | 120 ms |
| 145 | 386 ms |
| 308 | 1,583 ms |
| 489 | 4,183 ms |

**[PROJECTED]** from that fit: **~1,000 nodes ≈ 17 s; ~2,000 nodes ≈ 70 s.** That is a **generation-time budget**
problem on a repo larger than this one, and it belongs to 24.3: either bound the node count (which the threshold
does anyway) or switch to Barnes–Hut above ~500 nodes. Note this cost is paid **once by the generator, never by a
reader** — which is the whole point of treating position as data.

### 7.4 Recommended defaults for Story 24.3 and Story 24.2

| Surface | Recommendation | Why |
|---|---|---|
| **24.3 whole-repo default** | **support ≥ 5**, Code-only lens **on** | 129 nodes / 937 edges / 95,514 B / 286 ms. Floor 2 is 391 nodes with one 359-degree node — unreadable, and 460,817 B. |
| **24.3 hairball threshold** | **≈ 150 nodes** | Above it, median degree 14 with hubs at 100+ produces a solid mass. Floor 5 sits just under; floor 3 (267 nodes) is already over. |
| **24.3 solver bound** | Bound nodes, or Barnes–Hut **above ~500** | O(n²): ~17 s at 1,000 nodes. |
| **24.2 ego default** | **top-20 by support** *(corrected — see below)* | 21 nodes / 210 edges / **20,253 B** — almost exactly 23.1's measured 20,915 B sunburst island, so it costs what an already-accepted payload costs. The uncapped neighbourhood is **360 nodes**. |
| **24.2 hard floor** | Never uncapped | The shipped `FileInsightCoupledCap = 8` gives 4,297 B if 20 is judged too dense. |
| **Both** | Filters **hide**, never re-layout | §7.2 — **threshold filtering only**; directory grouping is unprobed and open (ADR 0030 §4) |
| **Both** | Tab order = **text-twin order** | §6.2 — the *principle* holds; the **degree-descending rule does not** (see below) |

> **⚠ Two corrections from the code review of 2026-08-08, both to recommendations this table ratifies.**
>
> **"Top-20 by confidence" was arithmetically top-20 by raw co-change count.** `BuildCappedEgo` computes
> `Confidence = Support / hubChanges`; `hubChanges` is constant for a fixed hub, so ordering by confidence is a
> monotone rescaling of ordering by support and the `ThenByDescending(Support)` tiebreaker can never fire. The
> directional quantity that makes Story 24.1's metric worth having — `conf(neighbour → hub) = support /
> changeCount[neighbour]` — was never used for the cap. Ranking by support systematically favours the highest-churn
> files, which is why the resulting node list is dominated by exactly the bookkeeping §7.3 tells 24.3 to filter out.
> **Story 24.3 should rank by neighbour→hub confidence and say so.** Story 24.2 has already shipped its cap.
>
> **"Tab order = text-twin order" is right; "degree-descending" is not.** §6.2 attributed degree-descending to
> Story 24.1's Q4, which actually settled on **confidence**-descending. Because every node in the measured fixture
> has degree 20, the recommended ordering was a constant tie and never ran. Story 24.2 deviated to the server's own
> emission order and was correct to — the twin lists citing artifacts first, then coupled files, and a degree
> ranking would disagree with the listing directly beneath the chart. **Take the principle, not the rule.**

---

## 8. AC #4 / R3 — the code-page double-graph question: **SUPERSEDE**

### 8.1 What `ReferenceGraph` actually renders today

Verified by reading `CodeFileTemplater.cs` and `Charts.cs` in full, not from the story's summary:

* A **hub-and-spoke** graph with the focal file at the centre, in a **Relationships tab** that
  `BuildRelationshipsPanel`'s own doc comment calls **"the single relationship surface (AC #2)"** — the old visible
  *"Often changed with"* list was **already removed**, its text equivalent moved to the card's **sr-only** list.
* **Two node populations:** citing artifacts (solid-spoke gold circles) and **co-changed files** (dashed-spoke
  neutral diamonds), linked to their code pages.
* **Four pre-rendered variants** (`RefGraphVariants`: `flat-flat` / `epic-flat` / `flat-rel` / `epic-rel`) switched
  by two pure-CSS checkboxes — *"Group by epic"* × *"Show relationships"*.
* **Cross-edges**: story↔related-file and related↔related (`BuildStoryRelatedEdges` / `BuildRelatedRelatedEdges`).
* An artifact-node **cap of 14** with an honest *"+N more"*, and a **toggle-agnostic sr-only list** that always
  enumerates epic membership and cross-edges *"so assistive tech never has less information than the richest
  sighted view."*

### 8.2 Story 24.1 already answered this, and left a named handoff

The decisive evidence is in shipped code, not in reasoning. Story 24.1 widened the private `RelatedNode` record to
carry `Support`, `Confidence`, `Lift`, `CrossBoundary`, and added `ToGraphNodes` as an explicit **projection seam**
down to the 4-tuple `Charts.ReferenceGraph` consumes. Its doc comment says why:

> *"Kept as a named type rather than widening the tuple `Charts.ReferenceGraph` accepts — the graph reads only the
> first four members and **stays a 24.2 concern**, so its signature deliberately does not drift here."*

**Per CLAUDE.md § Scoping a code review, this is a handoff and it is recorded here so it cannot fall between the
two stories:** `RelatedNode`'s metric members and `ToGraphNodes` live in **24.1's** File List but are
**self-attributed to Story 24.2**. Story 24.2's review must cover them; 24.1's review should note them as handed on.

### 8.3 Recommendation

**SUPERSEDE — Story 24.2 evolves `ReferenceGraph` in place. It does not add a second graph.**

1. There is already exactly **one** relationship surface on the code page, and it is already the coupled-file
   surface. A second ego graph would put two in one tab.
2. Story 24.1 already plumbed the metric to the node model and pointed the seam at 24.2.
3. The interaction upgrade (hover, select, filter) attaches to the **same** node populations.

**What 24.2 must absorb, or explicitly decide to drop:** the citing-artifact population · epic grouping ·
story↔related and related↔related cross-edges · the 14-node cap with *"+N more"* · the sr-only citer list ·
and the **four pure-CSS variants** — which is the biggest single scope driver, because four pre-rendered
server-side variants and one interactive client-side graph are different architectures. §7.2's finding helps:
*"Show relationships"* and *"Group by epic"* are **edge-visibility** toggles, and edge visibility is exactly what
filters can do **without moving a node**.

**⚠️ The gap this opens, surfaced explicitly and owned by nobody:** retiring that SVG is gated on an
**ADR 0013 §3 per-surface text-twin audit**, and **no Epic 24 story owns one**. Story 20.6's audit scope was the
**hierarchy** surfaces. The code page's twin must be verified complete for **both** populations — citers *and*
coupled files, including epic membership and cross-edges — **server-rendered**, before the SVG goes. §6.5 measured
what skipping this costs: a client-built twin contributes **0 bytes** under a half-applied CSP.
**Recommend seating this as a story (24.7) or folding an explicit twin-audit task into 24.2 with the owner's
agreement.** It is not in scope for this spike to seat it.

---

## 9. Supply-chain record for the Epic 17 / NFR10 audit

**The recommendation adds NO dependency.** SpecScribe's third-party runtime dependency count stays at **one**
(`plotly.js` 3.7.0, MIT, recorded in Story 20.4 §8). Nothing new to vendor, no `tools/*-vendor/` to add, no
`<EmbeddedResource>` to register, no conditional-emission guard to write. `specscribe generate` still needs no Node.

Recorded for completeness, since a future reopening would need it: **echarts 6.1.0, Apache-2.0**; **cytoscape 3.34.0,
MIT**. The probe workspace installed **6 packages**, throwaway and gitignored.

---

## 10. What this hands forward

| To | Hand-off |
|---|---|
| **Story 24.2** (ego graph) | Engine: **Plotly `scatter`** over generation-time C# coordinates — **0 new bytes**. **SUPERSEDE `ReferenceGraph`** (§8) and absorb its six capabilities; the four pure-CSS variants are the scope driver. Default **top-20 by confidence** (20,253 B). Hang the a11y layer on **`plotly_afterplot`**, never on a promise. **Clamp the roving index on every reapply.** Tab order = text-twin order. Use the real `ClassifyCoupling`, not the probe's path-shape approximation. **Server-render the twin.** Call `IsCrossBoundary` / `CouplingMinSupport`, don't re-derive. |
| **Story 24.3** (whole-repo explorer) | Default **support ≥ 5 + Code-only** (129/937/95,514 B). Hairball threshold **≈150 nodes**. Filters **hide, never re-layout** for the *threshold* axis, in 44–75 ms. O(n²) solver: bound nodes or use Barnes–Hut above ~500. Expect **62% cross-boundary / 46% Process** edges and a 359-degree `sprint-status.yaml`; say so in the UI rather than letting a reader think the repo is that entangled. **Use the real `ClassifyCoupling` result** via `CoupledPair.Kind` / `DirectedCouple.Kind` — the code-only lens above was computed with a local path-shape approximation (§2 correction). **Rank by neighbour→hub confidence**, not support. |
| **Story 24.3** — *added by the 2026-08-08 code review* | Six things this spike did **not** settle, now named rather than inherited silently. **(1) Directory grouping is unprobed** — a collapsed group node has no precomputed coordinate, and both escapes are closed by ADR 0030 (§2/§4); likely answer is a second generation-time solve over the collapsed graph, but it is unmeasured. **(2) Seed placement from node identity, not ordinal** — the spike's `theta = 2π·i/n` means one added file moves every node, so the payload diffs wholesale and a reader's mental map resets each commit (ADR 0030 §2). **(3) No transcendental in the coordinate path and no host-decided FP contraction**, and verify determinism on **two operating systems** — the spike proved it on one machine only (ADR 0030 §3). **(4) Define the orphan-node state** — when the last edge of a node is filtered out, the spike left it painted, in tab order, with a stale pre-filter accessible name. **(5) Define the empty state** — a threshold above maximum confidence was unreachable in the probe and no fixture below 9 nodes exists, so the empty graph's live-region text, message and focus target are unspecified. **(6) Guard NaN in the solver** — one NaN silently collapses every node onto 0.5 and still emits well-formed JSON, so no gate can see it. |
| **Story 24.4** (chord) | Plotly has **no chord trace**. Either hand-draw SVG arcs — and **read `docs/adrs/` first**: three arc renderers already exist and [[adr-consultation-gap-three-arc-renderers]] is about exactly this — or re-price ECharts with §4.1's numbers. **Do not improvise a second engine inside 24.4**; that is what this spike exists to prevent. If the arc work looks large, a focused re-opening of ADR 0030 is the correct move, not a quiet dependency. |
| **Story 24.5** (matrix) | **Unchanged: rides Plotly `heatmap`**, which §2 confirms is registered in the shipped bundle. ADR 0012 §4 already freed it and nothing here changes that. |
| **The unowned twin audit** | §8.3. Needs an owner before any `ReferenceGraph` SVG is retired. |
| **Story 23.4 / ADR 0005** | The joint CSP amendment needs **no relaxation of the policy string** for a graph either — `script-src 'nonce-…'` alone suffices, no `'unsafe-eval'`, and `style-src 'unsafe-inline'` is **not load-bearing**. Same conclusion 20.4 reached for hierarchies, now measured for graphs, header **and** meta. |
| **Webview** | `StripDataIslands` means the webview **cannot receive a graph payload today**. Either narrow that exception or Epic 24 surfaces take the ADR 0013 §7 text-twin fallback there. **This spike does not decide it** — it is the same open decision 20.4 §4.4 left for the hierarchy family, and it should be decided once for both. |
| **Any future verification harness** | ECharts' geometry is animation-frame-gated while all a11y attributes pass (§6.4); Plotly draws synchronously. **Assert on geometry, not attributes** — and per 20.4, not on the console either. |
| **Epic 17 / NFR10** | §9 — nothing to add. |

---

## 11. AC coverage — with explicit boundaries

| AC | Obligation | Status | Boundary |
|---|---|---|---|
| **#1** | per-candidate comparable table: bundle min + gzip as ×`prism.js` | ✅ **[HARNESS]** | custom tree-shaken IIFE builds, not published dist sizes |
| **#1** | license + provenance (NFR10) | ✅ | §9 — recommendation adds nothing |
| **#1** | coverage of all four Epic 24 shapes | ✅ | §4.3 — (a) does not serve 24.4's chord |
| **#1** | single classic script / no fetch / no ESM imports | ✅ **[HARNESS]** static + **[SESSION]** live | §4.2, §5 |
| **#1** | engine-family consequence per candidate | ✅ | §4.1 — a governance column, not a preference |
| **#1** | Plotly `scatter` priced as first-class at its true marginal cost | ✅ **[HARNESS]** | **0 B, confirmed against the SHIPPED asset** (§2), stronger than the story asked for |
| **#2** | explicit PASS / PASS (configured around) / FAIL per UX-DR7/16/17/18 | ✅ **[SESSION]** | §6.1. UX-DR18's 0 ms branch exercised via a labelled test seam, not by the media query firing — the session cannot flip the OS setting |
| **#2** | verified in a live browser | ✅ **[SESSION]** | pane does not composite (§5.1); flagged wherever it changes a reading |
| **#2** | renders under byte-verbatim webview CSP | ✅ **[SESSION]** header **and** meta for (a) | policy read from source at runtime |
| **#2** | script axis and style axis separately | ✅ **[SESSION]** | §6.5 — style axis proven **not** load-bearing |
| **#2** | Story 23.1's honesty boundary carried | ✅ | `vscode-resource:` untested, no Electron paint, no real extension host |
| **#3** | data-vs-presentation answered | ✅ | **DATA** (§7.1) |
| **#3** | determinism demonstrated across repeated runs | ✅ **[HARNESS]** | **3 separate processes**, 11 fixtures byte-identical; `scale.json` excluded (wall-clock) — a stated exclusion |
| **#3** | what happens to determinism when clutter controls change the node set | ✅ **[HARNESS]** + **[SESSION]** | §7.2 — 236 edge sets vs 17 node sets; recommended strategy makes it moot |
| **#3** | at-scale legibility + perf at `--deep-git` scale | ✅ **[HARNESS]** | §7.3 — two windows, five floors, degree distribution |
| **#3** | node/edge counts after the 24.1 support floor | ✅ **[HARNESS]** | floors 2/3/5/8/12 |
| **#3** | hairball point + 24.3 threshold defaults | ✅ | §7.4 — ≈150 nodes; support ≥ 5 |
| **#4** | ratified ADR with options table + consequences | ✅ | **[ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md)** — **extends**, does not supersede, ADR 0012 |
| **#4** | what each finding hands to 24.2/24.3/24.4/24.5 | ✅ | §10 |
| **#4** | R3's double-graph question resolved with a recommendation | ✅ | §8 — **SUPERSEDE**, plus the unowned twin-audit gap |

### What was NOT measured, and what was NOT done

**Not measured — named, not inferred to be safe:**

* **`file://` was NOT run live.** The Browser pane refuses a live `file://` context (it renders such URLs as static
  snapshots) — the **same** limitation Story 20.4 recorded, still owed. Structural evidence is strong and
  **[HARNESS]**-measured: every probe page has **0** absolute URLs, 0 protocol-relative, 0 root-relative, 3
  relative refs, 0 `fetch`, 0 dynamic `import`, 0 ESM script tags; every candidate bundle has 0 `fetch`,
  0 `import()`, 0 ESM imports, 0 CDN URLs, 0 XHR, 0 WebSocket. **Reproduce in one step:** open
  `spike/graph-engine/probe/plotly-scatter.html` from disk with networking disabled and confirm 21 nodes / 210 edges.
* **`Tab` traversal itself.** Native tab-sequence traversal did not fire in this automation context. The focus
  **model** is verified — roving `tabindex`, `.focus()` landing on a Plotly-emitted `<path>`, and **real**
  `ArrowRight` / `Enter` / `Escape` key events all working. The OS `Tab` keypress reaching the node is **owed**.
* **No screenshot / no pixel verification.** The pane never composited a frame (§5.1). Every visual claim rests on
  computed styles, DOM geometry (`d`, `getBBox`, `transform`) and the focus model. **A human eyeball is still
  owed**, and Story 24.2's create-story elicitation is where the silhouette is the owner's call anyway.
* **No real screen-reader run.** All a11y verdicts rest on DOM structure and computed styles, never an
  NVDA/VoiceOver/JAWS session. The decision rule only requires DOM-level conformance, so the verdicts stand.
* **ECharts under meta delivery and the style/wrong-nonce variants** were not separately re-measured; those rows
  are candidate (a) only. Candidate (b) was measured under the shipped policy with header delivery, and its
  initial-render throw was proven **not** CSP-related by reproducing it with CSP off.
* **ECharts force-layout determinism is UNMEASURED.** Two runs returned identical coordinates, but with **0 rAF
  frames** that is indistinguishable from both runs stalling identically, so it is **not** reported as
  determinism. Moot for the recommendation, which uses `layout:'none'`.
* **No overlapping/rapid re-render race.** Each of §6.2's eleven steps was triggered and awaited before the next.
  Same boundary Story 20.4 named.

**Not done, deliberately:**

* **No production code.** `src/SpecScribe/**` and `tests/**` were **not modified by this story** — §12.
* **No new tests.** No production code means nothing to unit-test; a test under `tests/**` would mean leaving the spike.
* **Nothing vendored for real.** No `tools/echarts-vendor/`, no `<EmbeddedResource>`, no asset copied into `src/`.
* **Story 24.1 was not implemented or edited.** `GitMetrics.cs` untouched; the probe **calls** its shipped API.
* **No ADR 0005 CSP amendment.** Shared with Story 23.4; lands once (ADR 0012 §5).
* **No `ReferenceGraph` change, no renderer retired, no twin audit performed.** §8 recommends; 24.2 executes.
* **The twin-audit gap was surfaced, not seated.** Seating a story is the owner's call.
* **The golden fingerprint is NOT offered as evidence.** Per R10 it builds from a synthetic temp fixture and cannot
  move for a spike; Story 23.1 had to retract that exact claim as structurally vacuous.

---

## 12. Non-invasiveness

`git status` at the end of this story shows modifications to `src/SpecScribe/DesignSystemTemplater.cs`,
`src/SpecScribe/assets/specscribe.css`, `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`,
`tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs`, `tests/SpecScribe.Tests/StylesheetTests.cs`, 14 files
under `web/`, four other story files, `epics.md`, `deferred-work.md`, and an untracked
`docs/adrs/0029-unscoped-shared-primitive-layer.md` + `web/assets/shared-primitives.css`. **None of these are this
story's.** They belong to at least one concurrent session (the ADR 0029 / shared-primitive-layer work).

This story's own changes are confined to:

* `spike/graph-engine/**` — new, throwaway, quarantined
* `_bmad-output/implementation-artifacts/24-6-spike-report.md` — this file
* `_bmad-output/implementation-artifacts/24-6-graph-engine-spike.md` — the story record
* `docs/adrs/0030-epic-24-graph-engine.md` + one appended line in `docs/adrs/README.md`
* `_bmad-output/planning-artifacts/epics.md` — the mirrored Story 24.6 AC block
* `_bmad-output/implementation-artifacts/sprint-status.yaml` — status transitions
* `.claude/launch.json` — five appended probe-server entries

**The load-bearing evidence is that File List, confirmed by `git status`.** Not the golden fingerprint (R10).

> **⚠ 2026-08-08, code review — the quarantine held, but the containment did not.** Everything below was verified
> and is accurate as far as it goes: the project really is absent from `SpecScribe.slnx` (CI builds the `.slnx`
> explicitly rather than globbing `**/*.csproj`, so the spike could not have reached the build gate), the
> `ProjectReference` really is one-way, and deleting the directory really did leave the shipped tool
> byte-identical. What the claim missed is that **directory quarantine is not dependency quarantine.**
> `spike/graph-engine/package.json` and `package-lock.json` were **tracked** — and absent from the story's File
> List — so GitHub's automatic security updates picked them up immediately: `f4f5629` *"Bump esbuild from 0.24.2 to
> 0.28.1 in /spike/graph-engine"* landed on 2026-07-30, one day after the spike. A declared-throwaway probe had
> become a permanent dependency-alert surface guarding nothing shippable, and the bump silently invalidated §13's
> reproduce path. The File List also declared `probe/*.html` *"generated, gitignored"* when `.gitignore` never
> listed them and four were tracked. **Per the owner's decision, `spike/graph-engine/` and the five
> `graph-24-6-*` entries in `.claude/launch.json` have been removed.**

**Quarantine, verified:** `spike/graph-engine/layout/GraphEngineSpike.csproj` is **not** in `SpecScribe.slnx`, is
not built by `dotnet build src/SpecScribe`, is not packed, and is not in the `extension/` bundle. It holds a
**one-way** `ProjectReference` to `src/SpecScribe` so the fixture reads the real Story 24.1 metric; nothing in
`src/SpecScribe` references it back. `node_modules/`, `dist/`, `bin/`, `obj/` are gitignored inside the spike.
Deleting `spike/graph-engine/` leaves the shipped tool byte-identical.

### A deliberate deviation from Task 1, recorded rather than quietly taken

**Task 1 said to work on an isolated spike branch or worktree. I worked on `main`, under `spike/graph-engine/`.**
Reasons, in order of weight:

1. **CLAUDE.md § Concurrent work on shared `main`** states plainly: *"The primary machine cannot run parallel git
   worktrees, so isolation is not available and is not the fix. This is an accepted working condition."*
2. **R1's stated reason for isolation no longer holds** — Story 20.4 finished; it is not in flight.
3. **Every file this story creates is new and exclusively its own**, so the collision surface a branch would
   protect is empty. The only shared files touched are the two the workflow requires (`sprint-status.yaml`,
   the story record) and `.claude/launch.json`, all append-only edits.
4. A worktree would have required a second full `dotnet build` of `src/SpecScribe` for the probe's
   `ProjectReference`, and [[worktree-edits-must-target-worktree-path]] records a real path-re-rooting defect.
5. Every prior spike (`spike/vscode`, `spike/nuxt-ir`, `spike/ir-incremental`, `spike/delivery`, …) lives
   committed under `spike/` on `main`. Directory quarantine, not branch isolation, is the house pattern.

Flagged for the owner as a judgment call, not presented as compliance.

---

## 13. Reproducing every number — SUPERSEDED, the probe has been pruned

> **⚠ 2026-08-08, code review.** `spike/graph-engine/` has been **removed** by owner decision, together with its
> five `.claude/launch.json` probe-server entries. The commands below no longer run, and are kept only as a record
> of how the numbers were obtained. **This report and ADR 0030 are the durable record of the measurement.**
>
> The section's title was never accurate in any case, and that is part of why pruning was chosen. It reproduced
> the fixtures, the determinism check, the bundles and the probe pages — but **not one figure in §5 or §6**, which
> is the whole of AC #2: the a11y survival table, the per-edge channel census, the CSP matrix, the container rows,
> the ECharts bisections and the colour audit were hand-transcribed `[SESSION]` observations with nothing driving
> or persisting them. §7.2's *"independently recomputed in JavaScript"* cross-check was likewise never recorded.
>
> Three of the five commands had also decayed since the spike ran:
> * **Step 3** — Dependabot bumped the tracked lockfile to esbuild `^0.28.1` (`f4f5629`, the day after this spike
>   landed) while the recorded toolchain is `0.24.2`, so it would no longer rebuild §4.1's byte figures.
> * **Step 5** — `WebviewRenderAdapter` moved its policy into a `CspPolicy` const, leaving `content="__CSP__"` in
>   the template. The harness regex still **matched** that placeholder, so its loud-failure guard never fired and
>   it served `Content-Security-Policy: __CSP__` — one unrecognised directive, i.e. **no policy enforced at all** —
>   with the `wrong-nonce` control silently reduced to a copy of the `webview` variant. The harness's own comment
>   claimed *"an upstream policy change cannot silently invalidate this report."* It could, and it did.
> * **§11's `file://` "reproduce in one step"** never worked as committed: the page loads `./vendor/…`, and
>   `probe/vendor/` is gitignored and absent, with a literal `nonce="__NONCE__"` still in the markup. Step 4 had to
>   run first.
>
> None of this invalidates the measurements as taken — the CSP verdict was recorded when the policy was still
> inline in the template, and was correct then. It invalidates the claim that they could be re-derived on demand.

```sh
# 1. the C# fixture builder + deterministic layout (references src/SpecScribe one-way)
dotnet build spike/graph-engine/layout/GraphEngineSpike.csproj
dotnet run --project spike/graph-engine/layout/GraphEngineSpike.csproj --no-build -- \
  --repo . --out spike/graph-engine/fixtures --runs 3

# 2. determinism ACROSS PROCESSES (the load-bearing check)
cd spike/graph-engine && node scripts/verify-determinism.mjs 3

# 3. candidate bundles + the R2 shipped-scatter assertion
npm install && npm run bundles

# 4. assemble the probe pages (fixture inlined as a data island, assets copied)
node scripts/build-probes.mjs ego-top20

# 5. serve under the byte-verbatim webview CSP, read from WebviewRenderAdapter.cs at runtime
node scripts/csp-probe.mjs 8131 webview header     # then also: webview meta | no-style-inline | wrong-nonce | off
```
