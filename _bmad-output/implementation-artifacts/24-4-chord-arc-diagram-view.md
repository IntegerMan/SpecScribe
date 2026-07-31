---
baseline_commit: 5a78ee751eec2f59217b809d1e93fb5273ac29df
---

# Story 24.4: Chord / Arc Diagram View of Coupling

Status: blocked

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## ⛔ Gate — ONE open gate: Story 24.3

This story adds a **view** to a surface Story 24.3 builds. Verified by `ls` at `5a78ee7`:

| Symbol 24.4 extends | Created by | Exists at `5a78ee7`? |
|---|---|---|
| `src/SpecScribe/CouplingExplorer.cs` — whole-repo model + the adaptive floor | Story 24.3 Task 2/3 | ❌ **No** |
| `src/SpecScribe/CouplingExplorerTemplater.cs` — the `coupling-explorer.html` page | Story 24.3 Task 5 | ❌ **No** |
| the **silhouette selector** and its N-views-over-one-payload mechanism | Story 24.3 Task 4 | ❌ **No** |
| the module/directory grouping used by the "constellation" silhouette | Story 24.3 Task 4 | ❌ **No** |
| `src/SpecScribe/CouplingLayout.cs`, `RelationshipGraph.cs` | Story 24.2 | ✅ **Yes** (24.2 `review`) |

**Do not `dev-story` this key until 24.3 reaches `review`.** 24.3's own gate (Story 24.2) is now **cleared** — 24.2 is `review`, `CouplingLayout.cs` and `RelationshipGraph.cs` both exist — so 24.3 is schedulable immediately and this gate is expected to be short-lived.

**Half of this story would technically build on 24.2 alone** (AC #3, the ego chord). Do **not** split it and ship that half early: the ring geometry, the module aggregation and the ribbon renderer are one body of code used by both surfaces, and building it twice is the reinvention this epic exists to prevent.

**When you do start: grep-verify 24.3's shipped symbols before writing a line against them** (CLAUDE.md § Concurrent work; [[shared-main-concurrent-edit-loss-verify-after-edit]]). This story names the shapes 24.3's task list specifies; the **shipped** shape is the authority, and 24.3's own verify round may have renamed things.

## Story

As a stakeholder who wants an elegant overview,
I want the coupling relationships also presentable as a chord/arc diagram,
so that a bounded set of files and their couplings reads as a single beautiful, symmetric figure.

## Acceptance Criteria

1. **Given** the whole-repo coupling explorer and JavaScript available
   **When** I switch to the **chord** representation on the explorer's existing silhouette selector
   **Then** the repository's **top-level modules** are arranged around a ring as arc segments sized by their share of the coupling, with **filled ribbons** connecting coupled modules — ribbon thickness driven by the summed Story 24.1 coupling between them, cross-boundary emphasis carried by **dash, ribbon outline, ordering and accessible text, never by hue** (UX-DR17, ADR 0030 §5) — offered as a **demoted alternate view behind the one selector** per UX-DR21, never as a second control and never as the page's primary representation
   **And** the ring is drawn by the **already-vendored Plotly `scatter` trace with `fill:'toself'`** over ribbon boundary geometry solved in C# at generation time (ADR 0030 §2, "node position is DATA"), at **zero marginal bundle bytes** and with **no new engine family and no new dependency**.

   > **AC #1 amendment, owner-approved at create-story 2026-07-30 (D2).** The epic's wording said "**files** are arranged around a ring". The owner chose **top-level modules, aggregated**. Three reasons, all load-bearing rather than aesthetic: at 24.3's auto-tune floor the file set is **129 nodes / 937 edges** and a 129-segment ring is the hairball the story exists to avoid; per-ribbon tooltip and `aria-label` identity requires **one Plotly trace per ribbon** (see Task 6), which is affordable at ≤ ~66 module pairs and is not at 937; and the module reading is what "an elegant overview" actually means for this dataset. **Individual files are never lost** — they remain fully enumerated in the shared text twin, which AC #2 extends rather than replaces.

2. **Given** the accessibility contract (UX-DR21, NFR8, ADR 0013)
   **When** the chord view renders
   **Then** the explorer's shared coupled-pairs text twin (Story 24.1 data, Story 24.3-rendered) **remains present and is never removed**, **and it gains a server-rendered module-pair summary** enumerating every arc and every ribbon the chord can draw — module A, module B, summed shared commits, pair count, and cross-boundary in words
   **And** with JavaScript off the surface is that twin, per [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) §1/§4 — **no static SVG fallback**.

   > **AC #2 amendments, owner-approved at create-story 2026-07-30.** Two. **(a)** The epic said "with JavaScript off the surface falls back to that table **plus, where feasible, a static SVG of the diagram**." ADR 0013 §4 supersedes that: the twin *is* the no-JS contract and shipping an SVG *and* the interactive chart is the dual-renderer option ADR 0013's options table explicitly rejected — the same amendment Stories 24.2 and 24.3 each took. **(b)** The module-pair summary is **added**, not inherited: ADR 0013 §2 requires **complete** — "no fact may exist only inside the chart" — and the aggregated module totals the ribbons draw are **not** recoverable from a file-pair listing by a reader. Aggregation creates new facts, and new facts need twin rows.

3. **Given** a code page's per-file relationship card (Story 24.2's ego graph) and owner decision **D3**
   **When** I switch that card to the chord representation
   **Then** the focal file's neighbourhood is drawn as the same module ring — arcs for the modules its coupled files and citing artifacts live in, ribbons carrying **both** the focal spokes **and** the ring-to-ring cross edges aggregated by module pair — through the **same** geometry and the **same** component, with the card's existing sr-only twin extended by the same module-pair summary
   **And** a neighbourhood that would degenerate (fewer than two distinct modules, so the ring is a single arc talking to itself) **suppresses the chord entry from the selector entirely** rather than drawing a meaningless figure — a selector entry that leads to nothing is worse than no entry.

4. **Given** [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md)'s explicitly named single gap — *"Plotly has no chord trace. Story 24.4's chord/arc view is therefore **not** served by this decision… 24.4 must either hand-draw SVG arcs… or come back and re-price a second engine **with an amendment to this ADR**"*
   **When** this story resolves that gap with `fill:'toself'` over generation-time geometry
   **Then** **ADR 0030 is amended in this same change** to record how the gap was closed, that the marginal bundle cost is still **zero bytes**, that ADR 0012 §4's allowance of a second engine family remains **unspent**, and that SpecScribe's third-party runtime dependency count is still **one**
   **And** the amendment states the one place the chord is genuinely **better** than the force-directed views: a filled ribbon's thickness is real geometry, not a trace-level `line.width`, so **ribbon weight escapes ADR 0030 §5's banding** and is continuous — which the legend must describe honestly instead of copying 24.2's "banded into 3 steps" wording.

## Owner decisions taken at create-story (2026-07-30)

Elicited up front per CLAUDE.md § Story lifecycle step 1, so the verify round does not spend a round on them. **Do not re-litigate these in dev-story; implement them.**

| # | Decision | Consequence for implementation |
|---|---|---|
| **D1 — Geometry / engine** | **Plotly `scatter` + `fill:'toself'`**, over ribbon boundary point-arrays solved in C# at generation time. | Closes ADR 0030's named gap at **zero marginal bytes** — `toself` is present in the shipped `plotly-hierarchy.min.js` (verified, 4 occurrences). No new engine family, no dependency, `scatter` already registered. **AC #4's ADR amendment is the governance half of this decision and is not optional.** "Arc diagram along an axis", "server-rendered static SVG chord" and "re-open ADR 0030 for ECharts" were offered and **not** chosen. |
| **D2 — Ring entity** | **Top-level modules, aggregated.** Arc per module, ribbon per module pair, weight = summed support. | Files stay in the twin, never on the ring. Makes one-trace-per-ribbon affordable (Task 6) and the figure legible. ⚠️ **A module set is a DIFFERENT node set from the explorer's file graph** — see "This is the second layout D4 deferred" below. "Files bounded to a ranked top-N" and "two-level module arcs with file ticks" were offered and not chosen. |
| **D3 — Placement** | **Explorer AND the per-file ego graph.** Both surfaces gain the chord as a demoted alternate. | Broader than the recommendation, so the degenerate-fan risk is real and AC #3 handles it by **suppression**, not by drawing something bad. The ego card has **no shape selector today** (24.2 shipped two filter checkboxes only) — this story adds one, using the same idiom, inside the same `hidden` control bar. "Explorer only" was offered and not chosen. |
| **D4 — Ranking / bound** | **Support**, riding **24.3's adaptive floor** unchanged. | One bound for both views: the chord aggregates exactly the edge set 24.3 already solved and already twinned, so the twin does not grow and the two views describe the same universe. ⚠️ **Do not introduce a second floor and do not touch `GitMetrics.CouplingMinSupport`.** "Confidence" and "lift" were offered and not chosen. |

## Tasks / Subtasks

- [ ] **Task 1 — ⛔ GATE CHECK, before anything else** (AC: all)
  - [ ] `ls src/SpecScribe/CouplingExplorer.cs src/SpecScribe/CouplingExplorerTemplater.cs` and confirm both exist. If not, **stop** — 24.3 has not landed. Report and halt.
  - [ ] Read **both files in full**, plus `RelationshipGraph.cs` and `CouplingLayout.cs`. Their shipped API is the authority, not this story's description of it.
  - [ ] **Find out exactly how 24.3 declared its three silhouettes.** This story adds a fourth entry to *that* mechanism. Grep the selector markup, the payload key that carries the coordinate sets, and the client's switch handler. **Extend it; do not add a parallel one** (ADR 0012 §2 "one selector idiom", and 24.3's own flag-forward: *"When 24.4 and 24.5 add chord and matrix, they must extend that same selector, not add a second one"*).
  - [ ] Re-read 24.3's **Completion Notes / File List** for decisions this story inherits: the `NodeBudget`/`EdgeBudget` values it actually shipped, the module-grouping rule its "constellation" silhouette used, what it did about the twin's byte cost (its open question #1), and whether it kept the `--coupling-floor` Configure prompt.
  - [ ] Re-run the analysis digest — it is **stale** (`node tools/analysis-digest/index.mjs`). See "Analysis observations".

- [ ] **Task 2 — Module aggregation, from data already in hand** (AC: #1, #2, D2, D4)
  - [ ] Aggregate the explorer's **already-solved, already-floored** edge set into module pairs. New code lives beside 24.3's model (`CouplingExplorer.cs`) or in its own file if that file is already large — say which and why.
  - [ ] **Do not add a git call, a second commit scan, a second parse, or a second support floor** ([[deep-git-single-numstat-path]]). The chord is a projection of the set 24.3 solved. If the chord needs an edge 24.3 filtered out, the answer is that the chord does not draw it — not a second fetch.
  - [ ] ⚠️ **`GitMetrics.BoundaryOf` — the module-identity function — is `private`** ([GitMetrics.cs:353](src/SpecScribe/GitMetrics.cs)). You need the module **name** for the arc label, and only `IsCrossBoundary` is public. **Widen `BoundaryOf` to `internal`/`public` (with its doc comment intact) or add a thin public accessor beside it. Do NOT write a second path-prefix rule** — a divergent one would make the ring's grouping disagree with the cross-boundary flag on the same pair, silently, on somebody else's repository.
  - [ ] Handle `BoundaryOf`'s two non-obvious returns explicitly: `string.Empty` = a **root-level** file (needs a real display label — "repository root", not a blank arc), `null` = **unknowable** (exclude from the ring; never invent a module). Test both.
  - [ ] Per module pair compute: **summed support**, **pair count**, **any-cross-boundary** (true by construction for A≠B; a same-module ribbon is the self-chord), and **Code/Process** mix via `GitMetrics.ClassifyCoupling` — never a re-derivation.
  - [ ] **Self-coupling is a real reading, not an edge case.** Files coupled *within* one module produce an A=A ribbon. Decide and state whether it draws as a loop on its own arc or is folded into the arc's own size; do not let it silently vanish — "this module mostly changes with itself" is the single most useful thing a chord says about a healthy module.
  - [ ] Arc size = the module's share of total drawn coupling, so the ring is proportional and sums to the whole. State the rounding rule; a ring that does not close is the geometry defect a reader notices first.
  - [ ] **Deterministic ordering by explicit ordinal sort of the module name.** ADR 0030 §3 is normative: no dictionary or set iteration order may reach a floating-point accumulation, and ring angles *are* a floating-point accumulation over module order.

- [ ] **Task 3 — Ribbon geometry in C#** (AC: #1, #4)
  - [ ] Solve the ring: arc segments (start/end angle per module, with a gap), then per ribbon a **closed boundary point array** — arc along module A's segment, quadratic/cubic curve across the interior to module B's segment, arc back, curve home. Sample the curves into points; `fill:'toself'` takes a polygon, not a path string.
  - [ ] **Sample count is a payload-size decision — declare it as a named const with a measured justification**, the way `CouplingLayout.CoordinateFormat` documents its 4 decimals. Too few points and the ribbon reads as a polygon; too many and the island bloats by ribbon count × samples × 2 coordinates.
  - [ ] **Format every coordinate through `CouplingLayout.Format`** ([CouplingLayout.cs:295](src/SpecScribe/CouplingLayout.cs)) — the one invariant-culture, 4-decimal formatter. Read its doc comment first: it explains exactly why coordinates round here and **confidence must never take this path** (4-decimal rounding was measured collapsing 453 distinct confidences into 452).
  - [ ] **ADR 0030 §3's determinism construction applies in full**: no `System.Random`, no `Dictionary`/`HashSet` iteration order into a float accumulation, no wall-clock, no environment, no parallelism, `CultureInfo.InvariantCulture` throughout.
  - [ ] **Do not resurrect `Charts.AnnularSector`.** Story 20.9 deleted it as *"the last hand-rolled arc geometry in this codebase"* and `HierarchyRolloutTests` **asserts by name that it and `BuildSunburstSvg` stay absent** ([Charts.cs:727-735](src/SpecScribe/Charts.cs)). This story's geometry is a **Plotly point array**, not an SVG path string, and it lives beside the graph family — not in `Charts.cs`. If you find yourself writing an SVG `A` arc command, stop: you have taken direction C, which was offered and not chosen.
  - [ ] `Charts.RibbonPath` ([Charts.cs:3562](src/SpecScribe/Charts.cs)) is the requirements-Sankey's **Cartesian** cubic band. Read it for the control-point idiom and the `F()` formatting discipline; **do not force it into polar** — a column band and a chord ribbon are different shapes.
  - [ ] **Solve cost:** report it. Ribbon geometry is O(pairs × samples) — trivially small beside 24.3's O(n²) force solve — so state the measured number and confirm it did not move the generation budget.

- [ ] **Task 4 — The explorer view** (AC: #1, #2, D2, D3)
  - [ ] Register the chord as a **fourth entry on 24.3's existing selector**, marked as the demoted alternate (UX-DR21). The three force-directed silhouettes stay the primary representation; the chord is the toggle.
  - [ ] **One payload.** The chord's arcs and ribbons are *derived* geometry over the same node/edge set, so it ships as an additional declared **view** — the shape [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)'s **2026-07-29 addendum** ratified: *"an instance may present N server-declared VIEWS over one shared payload."* Follow whatever 24.3 shipped for that; do not invent a second mechanism.
  - [ ] **Switching to chord must not re-fetch and must not re-solve.** Reduced motion: any transition snaps under `prefers-reduced-motion` from the `--motion-*` tokens ([[motion-token-system]]); **never `transition` a Plotly-owned property** ([[story-20-5-hierarchy-explorer-done]]).
  - [ ] **The legend must describe the channels the CHORD uses**, not the galaxy's. It is a different picture: arcs, ribbon thickness, ring order. ⚠️ **Ribbon thickness is continuous** (a filled polygon's width is real geometry, not a trace-level `line.width`), so **do not copy 24.2's "banded into 3 steps" wording** — that would be the misdescribing-legend class Stories 10.7 and 21.1 each closed, in the opposite direction. Emit legend entries only for channels actually present, the way [`RelationshipGraph.LegendHtml`](src/SpecScribe/RelationshipGraph.cs) already does.
  - [ ] **Framing (Story 10.2):** reuse the explorer's `Charts.Framed` + `Charts.ChartMeta` + `Charts.WhyText(ChartMetric.ChangeCoupling)` block. **One framing block for the instance, not one per view** — the views share a dataset.
  - [ ] **The AC #4 floor disclosure still applies to this view.** 24.3's framing sentence states the chosen floor and what it hid; the chord aggregates the same set, so the disclosure is inherited, not restated in different words.
  - [ ] **Designed empty state** when fewer than two modules clear the floor: suppress the chord entry from the selector (same rule as AC #3), never an empty ring.
  - [ ] Tooltips route through the body-level **`.ss-tooltip`** node, not a CSS `::after` ([[tooltip-clipping-use-ss-tooltip-node]]).
  - [ ] **Tokens, never Plotly colorways** (ADR 0012 §6): neutral ink/gold/border only; `--status-*` lifecycle tokens are **off-limits on code surfaces**. Ribbon fills need an opacity treatment that survives overlap without inventing a colour — state what you chose.

- [ ] **Task 5 — The ego chord** (AC: #3, D3)
  - [ ] Add the **first shape selector** to the code page's relationship card, inside the existing `hidden` `ss-relgraph-controls` bar ([RelationshipGraph.cs:332](src/SpecScribe/RelationshipGraph.cs)) so a JS-off reader never sees an inert control. Same selector idiom as the explorer's.
  - [ ] **The ego chord is NOT a fan, and the reason is measured.** 24.2's live pass counted **203 ring-to-ring cross edges** on `src/SpecScribe/Charts.cs` alone. Ribbons therefore carry **both** populations: focal→module spokes **and** neighbour→neighbour cross edges aggregated by module pair. A chord drawn from the focal spokes alone would be a fan and would deserve the criticism.
  - [ ] Both of 24.2's edge-visibility filters ("Group by epic", "Show relationships") must keep working in chord view — they hide, they never re-lay-out (ADR 0030 §4). **A hidden edge must not leave a ghost ribbon**, and re-showing must not move an arc.
  - [ ] **Degenerate suppression (AC #3):** fewer than two distinct modules in the neighbourhood ⇒ the chord entry is not emitted at all. Test it — a single-module file is common (a `tests/` file coupled only to `tests/` files).
  - [ ] Citing artifacts (`NodeKind.Artifact`, `EpicHub`) are not repository files and have no top-level module in the code sense. **Decide and state** whether they get their own arc ("planning artifacts"), ride their real path's module, or are excluded from the ring while staying in the twin. Do not let them silently fall into the `null`/unknowable bucket.
  - [ ] Extend the card's sr-only twin (`BuildRelationshipsTwin` in `CodeFileTemplater.cs`) with the same module-pair summary AC #2 defines. **One shared builder for both surfaces**, not two.

- [ ] **Task 6 — The client renderer, and its three traps** (AC: #1, #3)
  - [ ] ⚠️ **TRAP 1 — the a11y layer identifies the node trace as the LAST scatter trace.** `nodePaths()` does `traces[traces.length - 1]` ([specscribe.js:2894-2898](src/SpecScribe/assets/specscribe.js)) and the plot is assembled as `edgeTraces.concat([midTrace, nodeTrace])` ([specscribe.js:3053](src/SpecScribe/assets/specscribe.js)). **Ribbon traces appended after the node trace will silently redirect every node `aria-label`, `tabindex`, href and tooltip onto ribbon paths.** Every attribute assertion would still pass. Either insert ribbons **before** `midTrace`, or — better — make the selector explicit (select by trace `name`, which is already set) and **pin the choice with a test**, because the current selector is positional and a future view will hit this again.
  - [ ] ⚠️ **TRAP 2 — one `fill:'toself'` trace emits ONE `path.js-fill`.** Null-separated polygons in a single trace collapse into one DOM path with one style, so per-ribbon tooltip, `aria-label` and focus identity require **one trace per ribbon**. This is affordable **only because of D2**: ≤ ~66 module pairs (this repo has 14 top-level directories including root), versus 937 file edges. **Declare the trace-count ceiling as a const and report the measured count.** If a repository blows past it, bound the ribbons by summed support and say so in the ranking caption — never silently.
  - [ ] ⚠️ **TRAP 3 — the zero-width mount.** Plotly draws a wrong-size chart in a zero-width container and does not complain. The explorer page is visible at mount, but the **ego card's Relationships panel is `display:none` behind a pure-CSS radio tab** — that is exactly what `RelationshipGraph.RevealMarker` (`data-relgraph-reveal`) and the defer/flush machinery exist for. A chord switch that happens while hidden must defer the same way. Also mirror the **failure unwind** ([specscribe.js:1063-1080](src/SpecScribe/assets/specscribe.js)): a throw *after* `newPlot` succeeded previously left both charts mounted, the instance absent from the purge registry, and the ready flag set so re-init skipped that root forever.
  - [ ] The a11y layer must survive the view switch: roving tabindex **clamped on every reapply** (Story 20.4's sixth finding), one `tabindex="0"`, every drawn item a non-empty `aria-label`, and the live region announcing the switch.
  - [ ] **Reading order = the twin's order**, in chord view as in galaxy view. If the chord's ring order differs from the twin's row order, one of the two is wrong — say which and fix it, do not paper over it.

- [ ] **Task 7 — The twin's module-pair summary** (AC: #2, #3)
  - [ ] **Normative invariant:** every arc and every ribbon the chord can draw has a twin row. ADR 0013 §2 requires **complete**, and **aggregation creates facts** — "src ↔ tests share 214 commits across 61 pairs, cross-boundary" is not recoverable by a reader from 937 file rows.
  - [ ] **Server-render it.** The spike measured a client-built twin contributing **0 bytes** under a blocked script. It may be visually collapsed or `sr-only` — ADR 0013 §2 requires availability, not on-screen duplication; `<details>` is fine.
  - [ ] Every metric readable as **words**, never colour: summed shared commits, pair count, cross-boundary, Code/Process mix.
  - [ ] **The file-level twin is not replaced and not trimmed.** The module summary sits beside it. **Measure the added bytes and report them** — the module table is small (≤ ~66 rows) and should be a rounding error against 24.3's file twin, but 24.3's own open question was that the file twin may reach ~560–750 KB, so state your number rather than implying it is free.
  - [ ] Every link an arc offers must resolve in the twin (ADR 0013 §2 **navigable**). A module arc with no in-portal page renders a plain chip, never a dead link — through the same `Func<string,string?>` dual-mode resolver, where **a null return means "no page"**.

- [ ] **Task 8 — ⛔ The ADR 0030 amendment** (AC: #4)
  - [ ] **This is a gating deliverable, not paperwork.** ADR 0030 names this story by number in its "The one gap" section and says the gap must be closed by an amendment or by hand-drawn arcs. Leaving the ADR saying "24.4's chord view is unserved" after 24.4 ships served is exactly the stale-decision-record failure CLAUDE.md § Decision records exists to prevent.
  - [ ] Amend `docs/adrs/0030-epic-24-graph-engine.md` **in this change**: how the gap closed (`fill:'toself'` + generation-time boundary geometry), the still-zero marginal bundle cost, that ADR 0012 §4's second-family allowance stays **unspent**, that the dependency count is still **one**, and the **continuous-ribbon-weight exception** to §5's banding rule.
  - [ ] Update `docs/adrs/README.md`'s ADR 0030 entry to match. A README that still says the gap is open is the same defect one level up.
  - [ ] **Cite by symbol/section, never by line number** ([[cite-adrs-by-symbol-not-line-number]]) — ADR 0015's refs drifted within one day.
  - [ ] Do **not** mark ADR 0030 superseded and do **not** change its status. An amendment extends it, the way ADR 0030 itself extends ADR 0012.

- [ ] **Task 9 — Tests, determinism, and live-browser verification** (AC: all)
  - [ ] Unit tests: module aggregation (including root-level `string.Empty`, unknowable `null`, self-coupling, and a single-module degenerate set), arc proportions summing correctly, ribbon boundary point counts, the selector's suppression rule, and the twin's completeness **against the drawn arc/ribbon set** (the AC #2 invariant, asserted — not described).
  - [ ] **Determinism verified by repetition across SEPARATE PROCESSES**, not in-process and not by assertion (ADR 0030 §3; the spike verified byte-identical across **3 separate processes**, 11 fixtures). In-process repetition cannot see string-hash randomization, allocation-order effects, or tiered JIT changing float contraction.
  - [ ] **Assert on GEOMETRY, not attributes.** The spike's hand-off: an attribute-only audit certified an ECharts chart **drawing nothing** — every path `d=""`, every symbol `scale(0)` — while every a11y attribute passed. For ribbons that means: non-empty `d` on every `path.js-fill`, non-zero bounding boxes, and ribbon count matching the payload. Per Story 20.4, **do not assert on the console either**.
  - [ ] **Golden fingerprint will move — regenerate deliberately.** `dotnet build --no-incremental` **first** (embedded `.css`/`.js` assets are cached by an incremental build, so the hash you measure is stale), confirm **stable across two repeated runs**, and split the provenance — say whose changes yours sat on top of ([[golden-diff-normalization-gotchas]], CLAUDE.md § Concurrent work). **Never regenerate reflexively:** if it moved and you did not touch rendering, audit `GoldenNormalization.NormalizeVolatile` / `FoldToday` first.
  - [ ] ⚠️ **There is exactly ONE fingerprint gate now: `GoldenContentFingerprint`.** `GenerateAll_GoldenIrFingerprint_…` was **REMOVED on 2026-07-30** (commit `70b72ab`, [ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md); block comment at [SiteGeneratorAdapterTests.cs:1701](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)). **Story 24.3's task list still tells you to check both — that instruction is stale, do not chase it.** Never propose a whole-tree hash ([[adr-0033-content-drift-gates-are-targeted]]).
  - [ ] Land ADR 0013 §6-style assertions for the new view — on the **embedded geometry**, the **view configuration**, and the **twin** — following the `GoldenReplacement_*` idiom 24.2 established ([SiteGeneratorCodeInsightsTests.cs](tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs)). Three named tests, not a hash: a fingerprint says *something* moved; these say *what*.
  - [ ] **Live-browser verification is mandatory and is where the real defects will be** (CLAUDE.md § Verification). 24.2's live pass found **four** defects the suite structurally could not see; 24.1's found two. Verify: ribbons actually painted (not empty polygons), the ring closes, arc labels do not collide, the view switch does not move arcs or re-solve, per-ribbon tooltips and their **zero clipping ancestors**, real `ArrowRight`/`Enter`/`Escape` keys (**not** synthetic dispatch), focus-ring visibility, both ego filters in chord view, **mobile 375 px**, and the **JS-off state showing a complete twin including the module summary**.
  - [ ] Confirm both surfaces re-init after an **SPA navigation** (`specscribe:content-swapped`) and that removed mounts are purged.
  - [ ] Run `check:ir-content` — a new view with bespoke vocabulary may need `ir-content.css` rules, and its silent half has **shipped an unstyled tile** before ([[ir-content-drift-was-data-dependence]], ADR 0018/0026).
  - [ ] **Report the output-size delta** — island bytes for the ribbon geometry, twin bytes, `specscribe.js` and `specscribe.css` deltas, separately.

## Dev Notes

### What this story IS and is NOT

- **IS**: a **module-level chord view**, added as a demoted alternate on **two** existing surfaces (24.3's explorer selector, and a new selector on 24.2's ego card); filled ribbons drawn by Plotly `scatter` `fill:'toself'` over generation-time C# geometry; a module-pair extension to the shared text twin; and the **ADR 0030 amendment** that closes the ADR's one named gap.
- **IS NOT**: a new page, a new engine, a new dependency, a second selector, a second support floor, a change to `GitMetrics.CouplingMinSupport`, the adjacency matrix (24.5), directory **collapse** of the force-directed galaxy (still deferred — see below), or any change to the Story 24.1 metric.

### The engine gap is real, and D1 closes it without spending the second family

ADR 0030 is `Accepted` and its "one gap" section names **this story by number**. Two things follow.

**First, `fill:'toself'` is verified present in the shipped bundle.** `src/SpecScribe/assets/plotly-hierarchy.min.js` (1,223,563 B, plotly.js 3.7.0, MIT, embedded at `SpecScribe.csproj:67`) registers exactly `heatmap, scatter, sunburst, treemap`, and `toself` appears in it. A filled polygon over a point array is a `scatter` capability, so the marginal bundle cost is **zero bytes** — the same zero 24.2 and 24.3 ride.

**Second, ECharts is not on the table here.** ADR 0030 measured it, found it *technically the better graph engine* — including a **native `chord` series confirmed rendering live** — and rejected it on **cost-of-change, not merit**, recording that rejection as **time-dependent**. **Do not re-argue it inside this story.** If the ribbon work genuinely proves larger than D1 assumes, the correct move is a focused re-opening of ADR 0030 with §4.1's numbers, **never a quiet dependency**. That is the exact failure Story 24.6 exists to have prevented.

> **Read `docs/adrs/` before declaring you are crossing a project rule.** [[charting-is-pure-svg-no-js]] is **SUPERSEDED** for this family. Story 21.3 called its interactive treemap "a deliberate crossing of the pure-SVG, no-JS rule" citing a stale memory when ADR 0010 already permitted it ([[adr-consultation-gap-three-arc-renderers]]). The ratified ADR is the authority.

### ⚠️ This IS the second layout Story 24.3's D4 deferred — name it, do not quietly discharge it

24.3's owner decision **D4** deferred directory **collapse** because *"collapsing files into directory nodes is a genuinely different node set, and ADR 0030 §4 ratified that filters hide, never re-lay-out — so a collapse control needs a second precomputed layout, not a filter,"* and it wrote a `deferred-work.md` entry saying exactly that.

This story's D2 ring **is** a module-level node set with its own precomputed geometry. That is not a contradiction — a declared **view** with its own geometry is precisely the mechanism D4 said collapse would require, and ADR 0012's 2026-07-29 addendum ratified N views over one payload. But it means:

- **Cross-reference the D4 deferred-work entry** and record what this story did and did not deliver. It delivers a module-aggregated *chord*; it does **not** deliver a module-collapsed *force-directed galaxy*, which is a different picture with different geometry.
- **Do not mark D4 resolved.** If the owner wants collapse folded in on the strength of this work, that is a correct-course decision, not a dev-story one.
- The price D4 named — "two layouts ≈ 2× solve + 2× coordinate payload" — is much smaller here: module geometry is O(pairs × samples) over ~14 modules, not a second O(n²) force solve. **Report the real number**; it is evidence the owner will want.

### The one place the chord is genuinely better than the galaxy

ADR 0030 §5 quantises stroke width into bands because `scatter` **line style is a trace-level attribute** — 24.2 ships `WidthBands = 3` and its legend says so.

**A filled ribbon has no such limit.** Its thickness is real geometry: the arc span it occupies. So ribbon weight is **continuous**, and the chord can express what the galaxy cannot. Two consequences, both AC-bearing:

1. **The legend must describe a continuous channel**, not copy the "banded into 3 steps" wording. Misdescribing in the generous direction is still misdescribing.
2. **Record it in the ADR amendment** (AC #4). It is the strongest single argument that D1 was the right call, and it belongs in the decision record rather than only in a completion note.

Ribbon **outline** dash, however, is still trace-level — so cross-boundary emphasis by dash keeps the same discipline, and cross-boundary must **also** be spelled out in words (UX-DR17: no state signalled by colour alone).

### Existing surfaces to reuse — do not reinvent

| Need | Reuse | Location |
|---|---|---|
| The whole-repo model + adaptive floor | `CouplingExplorer` (**24.3**) — project from it, never re-floor | `src/SpecScribe/CouplingExplorer.cs` |
| The explorer page + its selector | `CouplingExplorerTemplater` (**24.3**) — extend the selector | `src/SpecScribe/CouplingExplorerTemplater.cs` |
| The component (skeleton, legend, island, twin enforcement, `ContainsHost`, `BootScript`) | `RelationshipGraph` (**24.2**) — extend, never clone | `src/SpecScribe/RelationshipGraph.cs` |
| Coordinate formatting | `CouplingLayout.Format` — the ONE invariant 4-decimal formatter | [CouplingLayout.cs:295](src/SpecScribe/CouplingLayout.cs) |
| Module identity | `GitMetrics.BoundaryOf` — **currently private; widen it, never re-derive** | [GitMetrics.cs:353](src/SpecScribe/GitMetrics.cs) |
| Cross-boundary flag | `GitMetrics.IsCrossBoundary` — computed once, shared (24.1 AC #2) | [GitMetrics.cs:375](src/SpecScribe/GitMetrics.cs) |
| Code/Process classification | `GitMetrics.ClassifyCoupling` — the real one, not the spike's approximation | [GitMetrics.cs:345](src/SpecScribe/GitMetrics.cs) |
| Support floor const | `GitMetrics.CouplingMinSupport` — read it, never move it, never re-literal it | [GitMetrics.cs:277](src/SpecScribe/GitMetrics.cs) |
| The file-level twin table | `Charts.CouplingTable` (24.1-upgraded) — the module summary sits BESIDE it | [Charts.cs:1770](src/SpecScribe/Charts.cs) |
| The ego card's twin | `CodeFileTemplater.BuildRelationshipsTwin` | `src/SpecScribe/CodeFileTemplater.cs` |
| Story 10.2 framing | `Charts.ChartMeta` + `Charts.Framed` + `Charts.WhyText(ChartMetric.ChangeCoupling)` | [Charts.cs:13-168](src/SpecScribe/Charts.cs) |
| Percent / plural formatting | `Charts.Percent`, `Charts.Plural` | `src/SpecScribe/Charts.cs` |
| Ribbon control-point idiom (Cartesian) | `Charts.RibbonPath` — read, do **not** force into polar | [Charts.cs:3562](src/SpecScribe/Charts.cs) |
| Control bar / reveal handshake | `ss-relgraph-controls` + `RelationshipGraph.RevealMarker`, `ss-hierarchy-controls` defer/flush | [RelationshipGraph.cs:332](src/SpecScribe/RelationshipGraph.cs), [specscribe.js:1092-1128](src/SpecScribe/assets/specscribe.js) |
| Tooltip | body-level `.ss-tooltip` via the `SEG` selector family | [specscribe.js:103-107](src/SpecScribe/assets/specscribe.js) |
| Asset flag | 24.2's `AssetManifest.GraphEngineNeeded` — derive from the rendered body via `ContainsHost`, never hand-set | `src/SpecScribe/AssetManifest.cs` |

### Measured numbers you can rely on

**This repository's module set** (`git ls-files`, top-level, 2026-07-30): `_bmad-output` 278 · `.claude` 235 · `.agents` 234 · `src` 154 · `tests` 135 · `web` 89 · `spike` 76 · `docs` 39 · `_bmad` 15 · `tools` 14 · `extension` 12 · `.github` 8 · root 7 · `.vscode` 3 · `.config` 1. **14 arcs at most**, and fewer after 24.3's floor — which is what makes D2's ring legible and Trap 2's one-trace-per-ribbon affordable.

**From Story 24.6's spike** (`-n 300`: 300 commits, 714 files, 16,604 uncapped pairs), the edge set this story aggregates:

| Support floor | Nodes | Edges | Payload B | C# solve |
|---:|---:|---:|---:|---:|
| 2 (shipped floor) | 391 | 4,864 | 460,817 | 2,611 ms |
| **5 (24.3 auto-tune lands here)** | **129** | **937** | **95,514** | **286 ms** |
| 8 | 73 | 429 | 45,252 | 98 ms |

Expect **62% cross-boundary** and **46% Process-class** edges, and `sprint-status.yaml` coupled to 92% of the graph at floor 2 — which is why the floor exists and why the framing sentence must say what it hid.

**From Story 24.2's live pass**: **203 ring-to-ring cross edges** on `Charts.cs`'s ego neighbourhood at the top-20 cap. This is the number that makes the ego chord a real figure rather than a fan.

### Previous-story intelligence (24.1 · 24.6 · 24.2 shipped · 24.3 pending)

- **The metric spine exists and is correct** (24.1): `CoupledFile`, `DirectedCouple`, `DeepGitPulse.DirectedCoupling`, `IsCrossBoundary`, `CouplingMinSupport`, `Lift()`, `Charts.Percent`.
- **24.1's Q4 is CLOSED** and does not need reopening. 24.2 measured 20 coupled files on `Charts.cs` at **15 distinct confidence values across 13%–75%** — confidence discriminates at 20 where it did not at 10 — so no ranking-policy change was proposed. The ego graph encodes confidence as **radius**, width as **banded shared commits**. **D4 ranks the chord by support, matching 24.3's floor**; do not introduce a third encoding.
- **24.2's live pass found FOUR defects the suite structurally could not see** (an unrevealed control bar, a legend rendering with JS off above a `display:none` host, nodes drawn outside the host from an inverted `scaleanchor`, and 20 overlapping markers). All four are rendered geometry or rendered honesty. **Expect the same class here** — a ring with labels is, if anything, more collision-prone.
  - Specifically inherited: the aspect lock **anchors x to y, not y to x** ([specscribe.js:2873](src/SpecScribe/assets/specscribe.js)); a ring must stay a circle, so do not touch that direction without re-measuring.
  - Specifically inherited: the legend and control bar are emitted `hidden` and revealed **on mount**. A chord selector entry that renders visible with JS off is defect #1 and #2 combined.
- **The deep-git 3s-timeout flake is real and silently produces no deep surfaces at all** ([[gitmetrics-3s-timeout-silent-deep-git-loss]]). It cost 24.1 two generation attempts. If a `--deep-git` run comes back with no coupling, **suspect the timeout before suspecting your code**.
- **Suite "flake" is usually a running preview server** ([[suite-flake-cause-is-a-running-preview-server]]) — git SPAWN starvation. Stop previews before the full suite. The browser pane also caps dev servers at **5 per folder across all chats**; verify over `file://`, and note `navigate` **strips the hash**.

### Webview and SPA

- **Webview:** `WebviewRenderAdapter.StripDataIslands` removes every `<script type="application/json">` island ([WebviewRenderAdapter.cs](src/SpecScribe/WebviewRenderAdapter.cs)), so **the webview cannot receive a graph payload today**. Take the **ADR 0013 §7 text-twin fallback** — the same call 24.2 and 24.3 took — and **verify the webview page ships no empty box and no inert selector**. Narrowing that exception is a joint decision with the hierarchy family and would want its own ADR (CLAUDE.md § Decision records). CSP itself is fine: `script-src 'nonce-…'` alone suffices, header **and** meta, no `'unsafe-eval'`. **Read the policy from `WebviewRenderAdapter.cs` at runtime rather than citing a line** — it drifted `:116 → :140` during the spike ([[cite-adrs-by-symbol-not-line-number]]).
- **SPA:** the `specscribe:content-swapped` seam re-inits components after a content swap ([[story-20-2-zoomable-drill-in-done]]); 24.2 verified its a11y layer survives 5/5 re-render events including a bare `Plotly.react` it did not initiate. The chord view must survive the same set.

### Preservation invariants — leave the system working end-to-end

- **Baseline output byte-identical WITHOUT `--deep-git`.** No coupling data → no chord, no selector entry, no asset flag. Verify, do not assume.
- **`GitMetrics.CouplingMinSupport` does not move.** Code pages and the Git Insights hub read it, and 24.1 already learned a floor change there is a site-wide visible behaviour change.
- **24.3's three silhouettes keep working unchanged**, and the force-directed galaxy stays the **primary** representation (UX-DR21).
- **24.2's ego graph keeps working unchanged** with both filters, in force-directed view.
- **The file-level text twin is never trimmed** to make room for the module summary (ADR 0013 §2 forbids a partial twin).
- **Every chart needs an accessible text equivalent, and no state may be signalled by colour alone** (CLAUDE.md § Verification, UX-DR17/19).
- Output dir is `SpecScribeOutput` ([[generate-output-dir-is-specscribeoutput]]). Never `--output docs/live`.

### Files being modified — read current state before editing

- `src/SpecScribe/CouplingExplorer.cs` — **24.3's file.** Module aggregation projection.
- `src/SpecScribe/CouplingExplorerTemplater.cs` — **24.3's file.** Fourth selector entry, chord legend, view registration.
- `src/SpecScribe/RelationshipGraph.cs` — **24.2's file.** The chord view + the ego shape selector; the legend's continuous-weight wording.
- `src/SpecScribe/CouplingLayout.cs` — **24.2's file.** Reuse `Format`; ring/ribbon geometry may live here or in a new sibling — **state which and why**.
- `src/SpecScribe/CodeFileTemplater.cs` — the ego card's selector + the twin's module summary. ⚠️ **12 open Sonar observations**, including two `S3776` cognitive-complexity errors and three `S107` (too many parameters) — do not add to them.
- `src/SpecScribe/GitMetrics.cs` — widen `BoundaryOf` (or add an accessor). **A one-line visibility change with a site-wide blast radius; grep every caller.**
- `src/SpecScribe/assets/specscribe.js` — ribbon traces, the view switch, and **Trap 1's trace-order fix**.
- `src/SpecScribe/assets/specscribe.css` — chord styles. ⚠️ **A `*/` inside a comment silently truncates ~1000 rules** ([[css-comment-star-slash-silent-truncation]]).
- `docs/adrs/0030-epic-24-graph-engine.md` + `docs/adrs/README.md` — **the AC #4 amendment (gating).**
- `_bmad-output/implementation-artifacts/deferred-work.md` — cross-reference 24.3's D4 entry.
- `src/SpecScribe/Charts.cs` — **ideally untouched.** ⚠️ **50 open Sonar observations**; already at its complexity ceiling, and Story 20.9 asserts by name that hand-rolled arc geometry stays out of it. If you find yourself editing it, re-read Task 3.

### Shared-main discipline (CLAUDE.md § Concurrent work)

**Assume another agent is editing these files right now.** `RelationshipGraph.cs`, `CouplingLayout.cs`, `specscribe.js` and `specscribe.css` all carry 24.2's fresh work and will carry 24.3's; 24.5 may start on the explorer while this story runs.

- **Grep-verify every new symbol after writing it** — a `Charts.cs` edit has silently vanished this way before ([[shared-main-concurrent-edit-loss-verify-after-edit]]; a zero-grep can also be a **transient mid-write**).
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**
- **Expect the golden fingerprint to move under you.** Establish causality before regenerating; bisect into a throwaway tree (`git archive HEAD` into the scratchpad) rather than resetting the shared tree.
- **Attribution by hunk, not by file** (CLAUDE.md § Scoping a code review): `RelationshipGraph.cs`, `specscribe.js` and `specscribe.css` will carry several stories' work in the same commit range. Record which hunks are yours.

### Analysis observations

`.specscribe/analysis/` was evaluated at **`bc7a379`** while HEAD is **`5a78ee7`** — per CLAUDE.md's read-time rule, **the digest is stale regardless of what `isStale` says** (it already reports `analysis-behind-working-tree` + `working-tree-dirty`). Re-run `node tools/analysis-digest/index.mjs` (Task 1) before trusting a line number. Read **shards**, not `index.json`: `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`. Known directionally at `bc7a379`: `Charts.cs` **50**, `CodeFileTemplater.cs` **12**, `SiteNav.cs` **9**. `RelationshipGraph.cs` and `CouplingLayout.cs` have **no shard** — they postdate the analysis, so that is **UNKNOWN, never clean**.

### Project Structure Notes

No new page, no new nav entry, no new CLI flag, no new dependency, no new engine family. Two existing surfaces gain a view; one ADR gains an amendment. Geometry may justify one new `src/SpecScribe/*.cs` file plus its test sibling — decide by size, and say which you chose. If working in a worktree, target the worktree path — `main` has a background auto-committer ([[worktree-edits-must-target-worktree-path]]).

### References

- [Source: docs/adrs/0030-epic-24-graph-engine.md] — **the engine decision and this story's charter.** §2 position-is-data · §3 **normative** determinism construction · §4 filters-hide-never-re-lay-out · §5 per-edge emphasis + width banding · **"The one gap, and how it is handled"** names Story 24.4 by number · "Consequences → Bad" prices the chord as unserved.
- [Source: docs/adrs/0013-text-twin-is-the-no-js-contract.md] — §1 amended NFR-5 · **§2 the four twin properties (server-rendered · complete · navigable · non-colour; collapsed/`sr-only` acceptable)** · §3 the per-surface gate · §4 supersedes ADR 0010 §2 · §6 fingerprint replacement · §7 webview fallback.
- [Source: docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md] — §2 component contract + "one selector idiom" · §3 `navigate`\|`select` mode grammar · §4 engine-family boundary · §6 tokens-not-colorways · §7 generation-time determinism · **2026-07-29 addendum: N server-declared views over one shared payload**.
- [Source: docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md] — why `GoldenIrFingerprint` is gone and why a whole-tree hash is never the answer.
- [Source: docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md] — edge direction convention.
- [Source: _bmad-output/implementation-artifacts/24-6-spike-report.md] — §4.1 the ECharts numbers a re-opening would need · §5.3 the native chord series measurement · §7.1 determinism · §7.3 at-scale table · **§10 the hand-off row addressed to Story 24.4**.
- [Source: _bmad-output/implementation-artifacts/24-3-whole-repo-coupling-explorer.md] — the surface this story extends; its **D3 ring-silhouette boundary** ("straight edges only — 24.4 owns ribbons"), its **D4 collapse deferral**, and its flag-forward that 24.4/24.5 must extend one selector.
- [Source: _bmad-output/implementation-artifacts/24-2-per-file-ego-coupling-graph.md] — the component and solver; its four live-only defects; the 203 cross-edge measurement; the Q4 resolution.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 24] — epic charter, FR40, UX-DR19/20/21, NFR8, execution order 24.1 → 24.6 → 24.2 → 24.3 → 24.4/24.5.
- [Source: src/SpecScribe/GitMetrics.cs] — `CodeMapMetrics` (70), `CoChangePairs` (82), `CouplingMinSupport` (277), `ClassifyCoupling` (345), **`BoundaryOf` (353, private)**, `IsCrossBoundary` (375).
- [Source: src/SpecScribe/RelationshipGraph.cs] — `HostMarker`/`RevealMarker` (38/54), `Size` (80), `BootScript` (91), `ContainsHost` (99), the model records (168-207), `WidthBands` (217), `StyleFor` (259), `Render` (304), `LegendHtml` (374), `IslandHtml` (439).
- [Source: src/SpecScribe/CouplingLayout.cs] — class remarks (the normative determinism clauses), `Solve` (97), `Format` (295), `CoordinateFormat` (74).
- [Source: src/SpecScribe/assets/specscribe.js] — tooltip `SEG` (103-107), zero-width defer/flush (1092-1128), failure unwind (1063-1080), **`nodePaths` last-trace assumption (2894-2898)**, aspect-anchor remarks (2865-2881), **trace assembly `edgeTraces.concat([midTrace, nodeTrace])` (3053)**.
- [Source: src/SpecScribe/Charts.cs] — `CouplingTable` (1770), `ChartMetric.ChangeCoupling` (20/63), `Framed`/`ChartMeta` (13-168), the **20.9 deletion note forbidding hand-rolled arc geometry** (727-735), `RibbonPath` (3562).
- Prior art: Story 10.2 (chart framing), Story 10.6 (the Code/Process lens), Story 10.7 / 21.1 (the misdescribing-legend class), Story 20.4 (the a11y decision rule + the unclamped roving index), Story 20.5 (never `transition` a Plotly-owned property), Story 20.9 (the arc-geometry deletion this story must not undo), Story 20.10 (N views over one payload), Story 23.4 (`PageView`, region composition).

### Open questions for the owner — do NOT block dev-start

1. **Self-coupling ribbons.** Does "src mostly changes with itself" draw as a loop on its own arc, or fold into the arc's size? Task 2 asks for a decision and a statement; raise the rendered result in the verify round.
2. **Citing artifacts on the ego ring.** Own arc, real-path module, or excluded-but-twinned? Task 5 asks for a decision; the right answer is likely visible only once it is drawn.
3. **Ribbon opacity under overlap.** Overlapping translucent ribbons are the classic chord reading, but SpecScribe paints from tokens, not a colorway. Show the owner the rendered result rather than picking silently.
4. **Whether the module chord makes 24.3's D4 collapse deferral worth re-opening.** This story produces the evidence (the real cost of a second precomputed layout); the decision is correct-course, not dev-story.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-30 | Story 24.4 created (baseline `5a78ee7`). **Status `blocked`, one gate: Story 24.3**, whose own gate is now cleared (24.2 is `review`, `CouplingLayout.cs` + `RelationshipGraph.cs` both exist) — flip to `ready-for-dev` when 24.3 reaches `review`. Four owner decisions elicited up front: **D1** Plotly `scatter` + **`fill:'toself'`** over generation-time C# ribbon geometry (verified present in the shipped bundle; closes ADR 0030's one named gap at zero marginal bytes, second engine family left unspent); **D2** the ring carries **top-level modules, aggregated**, not files — the reading "an elegant overview" actually needs, and the only thing that makes per-ribbon DOM identity affordable; **D3** the chord ships on **both** the explorer and the per-file ego card, so the ego card gains its first shape selector and AC #3 adds a degenerate-neighbourhood suppression rule; **D4** ranked by **support**, riding 24.3's adaptive floor so both views describe one universe and the twin does not grow. AC #1 amended (modules, not files) and AC #2 amended twice (ADR 0013 text-twin only, no static SVG; **plus a new module-pair twin summary**, because aggregation creates facts that would otherwise exist only inside the chart). AC #3 added for the ego chord; **AC #4 added for the gating ADR 0030 amendment**. Structural findings recorded: `GitMetrics.BoundaryOf` — the module-identity function the ring needs — is **private** and must be widened rather than re-derived; the client's a11y layer identifies the node trace **positionally as the last scatter trace**, so appended ribbon traces would silently redirect every node label; one `fill:'toself'` trace emits **one** `path.js-fill`, so per-ribbon identity needs one trace per ribbon (≤ ~66 module pairs vs 937 file edges — D2 is what makes it viable); a filled ribbon's thickness is real geometry, so **ribbon weight escapes ADR 0030 §5's banding** and the legend must not copy 24.2's "3 bands" wording; this module ring **is** the second precomputed layout 24.3's D4 said collapse would need, cross-referenced but deliberately not marked as discharging it; and `GoldenIrFingerprint` was **removed** on 2026-07-30, so 24.3's instruction to check both fingerprint gates is stale. |
