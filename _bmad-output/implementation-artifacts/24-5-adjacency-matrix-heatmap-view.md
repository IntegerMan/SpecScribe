---
baseline_commit: e8641338948d4a328589dd596b933fa69e6024da
---

# Story 24.5: Adjacency-Matrix Heatmap View of Coupling

Status: blocked

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## ⛔ Gate — ONE open gate: Story 24.3

This story adds a **view** to a surface Story 24.3 builds. Verified by `ls` at `e864133`:

| Symbol 24.5 extends | Created by | Exists at `e864133`? |
|---|---|---|
| `src/SpecScribe/CouplingExplorer.cs` — whole-repo model + the adaptive floor + the solved node/edge set | Story 24.3 Task 2/3 | ❌ **No** |
| `src/SpecScribe/CouplingExplorerTemplater.cs` — the `coupling-explorer.html` page | Story 24.3 Task 5 | ❌ **No** |
| the **silhouette selector** and its N-views-over-one-payload mechanism | Story 24.3 Task 4 | ❌ **No** |
| the module/directory grouping used by the "constellation" silhouette | Story 24.3 Task 4 | ❌ **No** |
| `src/SpecScribe/CouplingLayout.cs`, `RelationshipGraph.cs` | Story 24.2 | ✅ **Yes** (24.2 `review`) |

**Do not `dev-story` this key until 24.3 reaches `review`.** 24.3's own gate (Story 24.2) is **cleared** — 24.2 is
`review` and both files exist — so 24.3 is schedulable immediately and this gate is expected to be short-lived.

**Story 24.4 is NOT a gate (owner decision D4), and that is a deliberate concurrency choice.** 24.4 adds a *chord*
entry to the same selector, in the same files, at the same time. Read "Shared-main discipline" before you start:
`CouplingExplorerTemplater.cs`, `specscribe.js` and `specscribe.css` will carry both stories' hunks in one commit
range, and the review at epic end scopes **by hunk, not by file** (CLAUDE.md § Scoping a code review).

**When you do start: grep-verify 24.3's shipped symbols before writing a line against them** (CLAUDE.md § Concurrent
work; [[shared-main-concurrent-edit-loss-verify-after-edit]]). This story names the shapes 24.3's task list
specifies; the **shipped** shape is the authority, and 24.3's own verify round may have renamed things.

## Story

As an analyst facing a densely-coupled area,
I want an adjacency-matrix heatmap of coupling strength,
so that dense relationships that overwhelm a node-link graph read unambiguously as a grid.

## Acceptance Criteria

1. **Given** the whole-repo coupling explorer and JavaScript available
   **When** I switch to the **matrix** representation on the explorer's existing silhouette selector
   **Then** repository **files** label both axes, ordered into **top-level-module blocks** with visible block
   separators and module band labels, and each cell is shaded by the **directional Story 24.1 confidence** from its
   **row** file to its **column** file — offered as a **demoted alternate view behind the one selector** per
   UX-DR21, never as a second control and never as the page's primary representation
   **And** it carries a **real-value legend** stating the actual confidence range each shade means
   ("26–50% of the row file's changes"), per Story 10.2 — **never a bare "low…high" gradient**, and never Plotly's
   own continuous colorbar
   **And** the grid is drawn by the **already-vendored Plotly `heatmap` trace** (ADR 0030 §6, ADR 0012 §4) at
   **zero marginal bundle bytes**, with **no new engine family and no new dependency**.

   > **AC #1 amendment, owner-approved at create-story 2026-07-30 (D1 + D3).** The epic said "files label both axes"
   > and "a row/column ordering that clusters coupled files together" without saying how. The owner chose **files
   > with module block separators**, ordered **module-blocked, ranked within**. This is load-bearing, not cosmetic:
   > it is what discharges AC #3 (see below), because an off-diagonal block **is** a cross-boundary couple — so
   > position carries the emphasis a heatmap cannot carry with hue.

2. **Given** the accessibility and scale constraints (UX-DR19, NFR8, ADR 0013)
   **When** the matrix renders
   **Then** the explorer's shared coupled-pairs text twin (Story 24.1 data, Story 24.3-rendered) **remains present
   and is never removed or trimmed**, every drawn cell's strength is available as non-colour text there and in its
   hover/focus detail, and the grid is **bounded to a readable set** of the most-coupled files with an **honest
   "+N more" disclosure** naming what is not drawn
   **And** with JavaScript off the surface **is** that twin, per [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
   §1/§4 — **no static SVG fallback**
   **And** the surface stays within the deep-git performance envelope and is generation-time deterministic (FR31),
   verified by repetition **across separate processes** (ADR 0030 §3).

   > **AC #2 amendment, owner-approved at create-story 2026-07-30.** The epic said the matrix "degrades to the
   > readable table with JavaScript off", which is already the ADR 0013 position — recorded here only so the
   > *absence* of a static SVG is a stated decision rather than an omission. This is the same amendment Stories
   > 24.2, 24.3 and 24.4 each took.

3. **Given** UX-DR17 and CLAUDE.md § Verification — *no state may be signalled by colour alone* — and the fact that
   a heatmap's **only native channel is hue**
   **When** cross-boundary cells are emphasized and the matrix is read with colour removed
   **Then** every distinction the grid draws survives: cross-boundary is carried by **block position** (an
   off-diagonal block is by construction a cross-boundary pair), by a **non-hue overlay channel** (marker glyph,
   cell gap, or block outline — state which you chose and why), and **by words** in every cell's accessible name and
   in the twin
   **And** shade itself is never the sole carrier of a value: the exact confidence is readable from hover, from
   focus, and from the twin, exactly as ADR 0030 §5 requires of the banded stroke widths on the other views.

4. **Given** UX-DR7 (per-item Tab focus, Enter/Space activate, Escape, descriptive `aria-label`) and the fact that
   **Plotly's `heatmap` trace rasterizes to a single `<image>` element with ZERO per-cell DOM** — the exact
   property on which ADR 0030 **rejected Cytoscape**
   **When** the matrix mounts
   **Then** per-cell identity is restored by an **overlaid `scatter` marker trace** carrying one DOM node per drawn
   cell, so the shipped `plotly_afterplot` a11y layer applies unchanged: roving `tabindex`, one `tabindex="0"`,
   a non-empty `aria-label` per drawn cell, real `ArrowRight`/`Enter`/`Escape` keys, and the live region announcing
   the view switch
   **And** the completion notes state plainly that this is a **Plotly-heatmap-specific accommodation**, not a
   property the trace has on its own — so a later reader does not conclude the heatmap trace passes UX-DR7 by itself.

   > **AC #4 added at create-story 2026-07-30.** ADR 0030 §6 says "24.5 is unchanged: the adjacency-matrix heatmap
   > rides Plotly's `heatmap` trace". That is true about **bundle cost** and false about **accessibility**, and
   > nothing upstream had noticed: §4.3 of the spike scored 24.5 as `✅ heatmap already in the shipped bundle`
   > purely on trace availability. Verified at `e864133` against the shipped
   > `src/SpecScribe/assets/plotly-hierarchy.min.js`: it contains `heatmaplayer`, `createImage`, `toDataURL` and
   > `image/png` — the heatmap is painted into a canvas and emitted as one `<image>`. **This is the single most
   > consequential finding in this story**, and it is why AC #4 exists rather than being discovered during dev.

## Owner decisions taken at create-story (2026-07-30)

Elicited up front per CLAUDE.md § Story lifecycle step 1, so the verify round does not spend a round on them.
**Do not re-litigate these in dev-story; implement them.**

| # | Decision | Consequence for implementation |
|---|---|---|
| **D1 — Matrix entity** | **Files on both axes, with module block separators** and module band labels. | The epic's literal reading, kept. 24.4 took the *module* aggregation for its ring, so choosing modules here too would leave the file-level density — this story's entire premise — unrendered anywhere in the portal. Cost: separator `shapes` + band `annotations` + label-collision work, and its own draw bound (Task 3). "Top-level modules (≤14)" and "plain files, no separators" were offered and **not** chosen. |
| **D2 — Cell value** | **Directional confidence(row → column). The matrix is ASYMMETRIC.** | The epic names confidence. Asymmetry is the matrix's whole reason to exist: "touching A usually means touching B, but not the reverse" is a reading **neither the force-directed views nor the chord can express** — both are symmetric. `Charts.CouplingTable` is **already directed** (`FromPath`/`ToPath`/`Confidence`), so the twin's row set and the cell set are the same set for free. ⚠️ **The legend and the framing sentence must say the matrix is directed**, or a reader will assume the mirror. "Support, symmetric" and "confidence shade + support opacity" were offered and not chosen. |
| **D3 — Ordering** | **Module-blocked, ranked within.** Group by `GitMetrics.BoundaryOf` (ordinal), rank within each block by total coupling. | Discharges AC #1's "clusters coupled files together" **and** AC #3's colour problem in one move: an off-diagonal block **is** a cross-boundary pair, so position becomes the emphasis channel. Trivially deterministic (ADR 0030 §3). ⚠️ Uses the **same** module function 24.4 must widen — see Task 2. "Seriation by clustering" and "degree-rank descending" were offered and not chosen. |
| **D4 — Placement / sequencing** | **Explorer only.** Gate on **24.3 alone**, not on 24.4. | Not on the ego card: a matrix destroys the "this file, at the centre" reading the whole card is built around. Gating on 24.3 alone lets this run **concurrently with 24.4** — accepted deliberately, with the shared-file discipline below as the mitigation. "Serialize behind 24.4" and "explorer AND the ego card" were offered and not chosen. |

## Tasks / Subtasks

- [ ] **Task 1 — ⛔ GATE CHECK, before anything else** (AC: all)
  - [ ] `ls src/SpecScribe/CouplingExplorer.cs src/SpecScribe/CouplingExplorerTemplater.cs` and confirm both exist.
        If not, **stop** — 24.3 has not landed. Report and halt.
  - [ ] Read **both files in full**, plus `RelationshipGraph.cs` and `CouplingLayout.cs`. Their shipped API is the
        authority, not this story's description of it.
  - [ ] **Find out exactly how 24.3 declared its three silhouettes.** This story adds an entry to *that* mechanism.
        Grep the selector markup, the payload key that carries the per-view data, and the client's switch handler.
        **Extend it; do not add a parallel one** (ADR 0012 §2 "one selector idiom", and 24.3's own flag-forward:
        *"When 24.4 and 24.5 add chord and matrix, they must extend that same selector, not add a second one"*).
  - [ ] **Check whether Story 24.4 has already landed a chord entry.** If it has, your entry is the *fifth*, and its
        selector idiom is the shipped precedent — follow it rather than 24.3's description of it. If it has not,
        expect it to arrive mid-flight and see "Shared-main discipline".
  - [ ] Re-read 24.3's **Completion Notes / File List** for decisions this story inherits: the `NodeBudget` /
        `EdgeBudget` values it actually shipped, the floor it auto-tuned to, its module-grouping rule, what it did
        about the twin's byte cost (its open question #1), and whether `--coupling-floor` gained a Configure prompt.
  - [ ] Re-run the analysis digest — it is **stale** (`node tools/analysis-digest/index.mjs`). See
        "Analysis observations".

- [ ] **Task 2 — The matrix model: ordering, blocks, and the bound** (AC: #1, #2, D1, D3)
  - [ ] Project the explorer's **already-solved, already-floored** node/edge set into a matrix model. New code lives
        beside 24.3's model (`CouplingExplorer.cs`) or in its own file if that file is already large — **say which
        and why**.
  - [ ] **Do not add a git call, a second commit scan, a second parse, or a second support floor**
        ([[deep-git-single-numstat-path]]). The matrix is a *projection* of the set 24.3 solved. If the matrix wants
        a pair 24.3 filtered out, the answer is that the matrix does not draw it — not a second fetch.
  - [ ] ⚠️ **`GitMetrics.BoundaryOf` — the module-identity function — is `private`**
        ([GitMetrics.cs:353](src/SpecScribe/GitMetrics.cs)); only `IsCrossBoundary` is public. **Story 24.4 Task 2
        is already instructed to widen it. Check whether it has, and reuse.** If it has not, widen it to
        `internal`/`public` (doc comment intact) or add a thin accessor beside it, and say in the completion notes
        that you did so — so 24.4 finds it rather than doing it twice. **Do NOT write a second path-prefix rule**: a
        divergent one would make the block grouping disagree with the cross-boundary flag on the same pair,
        silently, on somebody else's repository.
  - [ ] Handle `BoundaryOf`'s two non-obvious returns explicitly: `string.Empty` = a **root-level** file (needs a
        real block label — "repository root", never a blank band), `null` = **unknowable** (exclude from the grid;
        never invent a module). Test both.
  - [ ] **Ordering (D3), and it is normative:** files sorted by (module name, ordinal) → within a module, by total
        coupling descending → ties broken by ordinal path. **Both axes take the SAME order**, or the diagonal stops
        being the diagonal. Materialize through an **explicit ordinal sort** before anything numeric happens
        (ADR 0030 §3 — no dictionary or set iteration order may reach a rendered artifact).
  - [ ] **The draw bound is this story's own and must be declared as a const with a measured justification.**
        24.3's auto-tune lands on **129 nodes** on this repository → **16,641 cells** and 129 labels per axis. That
        is neither readable nor a sensible payload. Bound the drawn file set (recommend ~40×40 = 1,600 cells) by
        **total coupling within the ordered set**, keeping whole module blocks coherent where possible.
  - [ ] **The "+N more" disclosure is AC #2 and is not a footnote.** State how many files and how many directed
        pairs are in the twin but not on the grid, in the server-rendered ranking caption — the same idiom
        `RelationshipGraph`'s caption already uses ("21 further citing artifacts are listed in full below but not
        drawn"). Silent truncation reads as "this is everything".
  - [ ] **The diagonal is a real decision, not an edge case.** A file's confidence with itself is meaningless.
        Decide and **state**: drawn as a null/gap cell, or as a labelled "self" band. Do not let it render as a
        `0%` cell that reads as "these two files never change together".
  - [ ] Per drawn cell compute from the shipped 24.1 spine, **never re-derived**: `Confidence`, `Support`, `Lift`,
        `CrossBoundary` (`GitMetrics.IsCrossBoundary`), `Kind` (`GitMetrics.ClassifyCoupling`). `GitMetrics.Lift`
        is the **one** place the divide-by-zero guard lives and it returns `null`, never `NaN`/`Infinity` — which
        reach markup as literal text.

- [ ] **Task 3 — The heat ramp, its token gap, and the real-value legend** (AC: #1, #3)
  - [ ] **Bucket confidence onto the shipped 1–4 gold ramp with FIXED cut points, not data-relative quartiles.**
        The precedent and its reasoning already exist: `Charts.OwnershipShareLevel` buckets a share percentage at
        `≤25 / ≤50 / ≤75 / else` and its doc comment says exactly why — *"a share percentage is already meaningful
        on its own scale, so '76–100%' means the same thing on every repo's chart, never a moving target."*
        Confidence is the same kind of number. **Do not use `Charts.HeatThresholds`** — that is the commit-heatmap's
        data-relative quartile split over an unbounded count, and it would make the same shade mean different things
        on different repositories.
  - [ ] ⚠️ `Charts.HeatLevel`, `HeatThresholds`, `IsHeatLevelUnreachable`, `FormatHeatRange` and
        `OwnershipShareLevel` are **all `private`**; only `HeatLevelRange` is public and
        `CodeMapChangeLevelRange` / `IsCodeMapChangeLevelUnreachable` are `internal`. You need a **confidence**
        level function and a **confidence** range-label function, and neither exists. **Write them once, beside the
        others in `Charts.cs`, with a doc comment that names why they are fixed-cut-point rather than quartile** —
        do not inline a switch at the call site and do not "reuse" a count-shaped function on a ratio.
  - [ ] ⚠️ **THE TOKEN GAP. `--heat-1` and `--heat-2` DO NOT EXIST.** Levels 1 and 2 are literal hex
        `#ecd18f` / `#dfb455`, repeated across **five** CSS families —
        `.heatmap-cell`, `.heatmap-legend-swatch`, `.codemap-cell`, `.codemap-legend-swatch`, `.risk-point`,
        `.ownership-wedge`, `.ownership-legend-swatch` ([specscribe.css:4272, 4375, 4458, 4752, 4810, 6393, 6450](src/SpecScribe/assets/specscribe.css)).
        Levels 3 and 4 are `var(--gold-light)` / `var(--gold)`. **ADR 0012 §6 forbids a literal colour in a Plotly
        payload** — colours travel as **token names** resolved client-side through the real cascade, which is what
        makes the chart follow a theme switch. Two ways out; **pick one and state why**:
    - **Recommended — declare `--heat-1` / `--heat-2` and point the seven existing rules at them.** Mechanical,
      greppable, byte-identical rendering, and it makes the payload uniform with every other Epic 24 island. It is a
      site-wide CSS touch, so **verify the seven rules render unchanged in a live browser** before believing it.
    - Client-side probe: resolve the ramp off a hidden element carrying the existing `.heatmap-legend-swatch.level-N`
      classes. No CSS change, but it invents a second colour-resolution mechanism the codebase does not have.
  - [ ] ⚠️ **A `*/` inside a CSS comment silently truncates ~1000 rules** ([[css-comment-star-slash-silent-truncation]]).
        If you touch `specscribe.css`, re-check the rule count after.
  - [ ] **Use a STEPPED (discrete) Plotly colorscale matching the four buckets exactly**, with **fixed
        `zmin: 0`, `zmax: 1`** — a data-relative auto-scale would silently re-mean every shade per repository, which
        is the same defect the fixed cut points exist to prevent.
  - [ ] **`showscale: false`.** Plotly's own colorbar is a continuous gradient with no real-value labels — exactly
        the bare "low…high" ramp AC #1 forbids. The legend is SpecScribe's, server-rendered, emitted `hidden` and
        revealed on mount (the `RelationshipGraph.LegendHtml` handshake), with one swatch per **reachable** level and
        its real range as text. Skip unreachable levels the way `IsHeatLevelUnreachable` / 
        `IsCodeMapChangeLevelUnreachable` already do — several indistinguishable "—" swatches side by side is the
        phantom-entry class Stories 10.7 and 21.1 each closed.
  - [ ] **The legend must state that the matrix is DIRECTED** (D2). "Row → column: how often the row file's changes
        also touch the column file." Without that sentence a reader assumes a symmetric matrix and misreads every
        asymmetric pair — which is the one thing this view exists to show.
  - [ ] **Tokens, never Plotly colorways** (ADR 0012 §6): the neutral ink/gold/parchment/border family only.
        `--status-*` lifecycle tokens are **off-limits on code surfaces**.

- [ ] **Task 4 — Blocks, separators, and axis labels** (AC: #1, #3, D1)
  - [ ] Draw module block separators as layout **`shapes`** and module band labels as **`annotations`** — both are
        core Plotly layout features, verified present in the shipped bundle (8 and 15 occurrences respectively), so
        still **zero marginal bytes**. Cell gaps via **`xgap`/`ygap`** give the grid its rules for free.
  - [ ] **Axis tick labels: basenames, full path elsewhere.** 40 repository paths per axis will collide. Use
        `tickvals`/`ticktext` with basenames, disambiguate collisions deterministically (a shared basename is common
        — `index.ts`, `README.md`), and put the full path in the cell's `aria-label`, its hover text and the twin.
        **A truncated label that hides which file it is is a correctness defect, not a cosmetic one.**
  - [ ] **`hoverongaps: false`** so an empty cell does not offer a hover card for a relationship that does not exist.
  - [ ] **Hover text must NOT be one composed sentence per cell.** 24.2 measured composed-per-edge sentences at
        **30,820 B — 56% of a 55,012 B island** for 203 edges; a 1,600-cell grid is eight times that population.
        Use `hovertemplate` with `customdata`, or the config-level phrase-template idiom
        `RelationshipGraph.PhraseFor` already established — **the wording still authored once, in C#**. Report the
        measured island bytes either way.
  - [ ] **Designed empty state** when fewer than two files clear the floor: **suppress the matrix entry from the
        selector entirely** rather than drawing an empty grid. A selector entry that leads to nothing is worse than
        no entry — the same rule Story 24.4's AC #3 takes for a degenerate ring.
  - [ ] Tooltips route through the body-level **`.ss-tooltip`** node, not a CSS `::after`
        ([[tooltip-clipping-use-ss-tooltip-node]]).

- [ ] **Task 5 — The explorer view registration** (AC: #1, #2)
  - [ ] Register the matrix as an entry on **24.3's existing selector**, marked as the demoted alternate (UX-DR21).
        The force-directed galaxy stays the **primary** representation.
  - [ ] **One payload.** The matrix's z-grid and cell metadata are *derived* over the same node/edge set, so it
        ships as an additional declared **view** — the shape [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)'s
        **2026-07-29 addendum** ratified: *"an instance may present N server-declared VIEWS over one shared
        payload."* Follow whatever 24.3 shipped; do not invent a second mechanism.
  - [ ] **Switching to matrix must not re-fetch and must not re-solve.** Reduced motion: any transition snaps under
        `prefers-reduced-motion` from the `--motion-*` tokens ([[motion-token-system]]); **never `transition` a
        Plotly-owned property** ([[story-20-5-hierarchy-explorer-done]]).
  - [ ] **Framing (Story 10.2):** reuse the explorer's `Charts.Framed` + `Charts.ChartMeta` +
        `Charts.WhyText(ChartMetric.ChangeCoupling)` block. **One framing block for the instance, not one per view**
        — the views share a dataset.
  - [ ] **The AC #4 floor disclosure from 24.3 still applies to this view.** 24.3's framing sentence states the
        chosen floor and what it hid; the matrix draws a subset of that same set, so the disclosure is **inherited**,
        and this view's own "+N more" is **added** on top of it — not restated in different words.
  - [ ] Every file a cell or axis label offers must resolve through the same `Func<string,string?>` dual-mode
        resolver (`SiteGenerator.CodeItemHref`), where **a null return means "no page" → plain text, never a dead
        link**.

- [ ] **Task 6 — The client renderer, and its four traps** (AC: #1, #3, #4)
  - [ ] ⚠️ **TRAP 1 — THE HEATMAP HAS NO PER-CELL DOM.** Verified at `e864133` against the shipped bundle:
        `heatmaplayer` + `createImage` + `toDataURL` + `image/png`. Plotly rasterizes the grid into a canvas and
        emits **one `<image>`**. There is nothing to focus, nothing to label, nothing to hover per cell. This is the
        **exact** property on which ADR 0030 rejected Cytoscape (*"canvas, zero per-node DOM"*, UX-DR7).
        **The fix is AC #4's overlay: a `scatter` marker trace at every drawn cell centre**, which restores one
        `<path>` per cell and lets the shipped `plotly_afterplot` a11y layer apply unchanged. `scatter` is already
        registered, so this is still zero marginal bytes. It is the same "auxiliary invisible trace" idiom ADR 0030
        already names for per-edge hover on the force views.
  - [ ] ⚠️ **TRAP 2 — the a11y layer identifies the node trace POSITIONALLY as the LAST scatter trace.**
        `nodePaths()` does `traces[traces.length - 1]` ([specscribe.js:2894-2898](src/SpecScribe/assets/specscribe.js))
        and the plot is assembled as `edgeTraces.concat([midTrace, nodeTrace])`
        ([specscribe.js:3053](src/SpecScribe/assets/specscribe.js)). A view switch that leaves the heatmap trace or
        the overlay in the wrong slot **silently redirects every `aria-label`, `tabindex`, href and tooltip onto the
        wrong element — and every attribute assertion still passes.** Story 24.4 is instructed to hit this same
        trap. **Make the selector explicit (select by trace `name`, which is already set) and pin the choice with a
        test.** If 24.4 already did it, reuse; if not, do it here and say so in the completion notes so 24.4 finds
        it. Two stories independently patching a positional selector is worse than either.
  - [ ] ⚠️ **TRAP 3 — the view switch must not orphan the a11y layer or the purge registry.** Mirror the
        **failure unwind** ([specscribe.js:1063-1080](src/SpecScribe/assets/specscribe.js)): a throw *after*
        `newPlot` succeeded previously left both charts mounted, the instance absent from the purge registry, and
        the ready flag set so re-init skipped that root forever.
  - [ ] ⚠️ **TRAP 4 — the aspect lock.** 24.2 anchors **x to y**, not y to x
        ([specscribe.js:2873](src/SpecScribe/assets/specscribe.js)) after a live defect where a wide-short panel
        pushed nodes outside the host. **A matrix is square by definition** — an N×N grid whose cells are not square
        misreads as weighted. Confirm the aspect handling the matrix view needs and **do not change the shared
        anchor direction without re-measuring the galaxy view**, which depends on it.
  - [ ] The a11y layer must survive the view switch: roving tabindex **clamped on every reapply** (Story 20.4's
        sixth finding), one `tabindex="0"`, every drawn cell a non-empty `aria-label`, and the live region
        announcing the switch.
  - [ ] **Reading order = the twin's order**, in matrix view as in galaxy view. If the matrix's cell traversal order
        differs from the twin's row order, one of the two is wrong — say which and fix it, do not paper over it.

- [ ] **Task 7 — The twin** (AC: #2, #3)
  - [ ] **Normative invariant: every drawn cell has a twin row.** ADR 0013 §2 requires **complete** — "no fact may
        exist only inside the chart".
  - [ ] **This is cheaper here than it was for 24.4, and the reason is worth stating.** `Charts.CouplingTable` is
        **already directed** — `FromPath`, `ToPath`, `Together`, `Confidence`, `Kind` with **text** Process and
        Cross-boundary badges, lift on the cell title ([Charts.cs:1770](src/SpecScribe/Charts.cs)). D2's cell value
        **is** a `DirectedCouple` row. So the cell set and the twin's row set are the same set by construction, and
        **the twin should need no new rows at all**. Verify that rather than assuming it.
  - [ ] **What IS new and does need disclosure:** the **block structure** and the **ordering**. "These files were
        grouped into N module blocks; here they are, in order" is a fact created by this view and not recoverable
        from a flat pair list. Add it as a small server-rendered summary beside the table (visually collapsed or
        `sr-only` is fine — ADR 0013 §2 requires availability, not on-screen duplication; `<details>` is fine).
        Measure its bytes and report them.
  - [ ] **The file-level twin is not replaced and not trimmed.** 24.3's own open question is that it may reach
        ~560–750 KB. **State your measured number** rather than implying the addition is free.
  - [ ] Every metric readable as **words**, never colour: confidence, support, cross-boundary, Code/Process.
  - [ ] Every link a cell or axis label offers must resolve in the twin (ADR 0013 §2 **navigable**).

- [ ] **Task 8 — Tests and determinism** (AC: all)
  - [ ] Unit tests: the module-blocked ordering (including root-level `string.Empty`, unknowable `null`, a
        single-module set, and a shared-basename collision), both axes taking the same order, the confidence→level
        buckets at their exact cut points, the range-label text, unreachable-level skipping, the draw bound and its
        "+N more" arithmetic reconciling exactly, the diagonal's chosen treatment, the selector's suppression rule,
        and the twin's completeness **against the drawn cell set** (the AC #2 invariant, asserted — not described).
  - [ ] **Determinism verified by repetition across SEPARATE PROCESSES**, not in-process and not by assertion
        (ADR 0030 §3; the spike verified byte-identical across **3 separate processes**, 11 fixtures). In-process
        repetition cannot see string-hash randomization, allocation-order effects, or tiered JIT changing float
        contraction.
  - [ ] **Assert on GEOMETRY, not attributes.** The spike's hand-off: an attribute-only audit certified an ECharts
        chart **drawing nothing** — every path `d=""`, every symbol `scale(0)` — while every a11y attribute passed.
        For a heatmap that means: the `<image>` in `.heatmaplayer` has a **non-empty `xlink:href` and a non-zero
        bounding box**, and the **overlay marker count matches the drawn cell count**. Per Story 20.4, **do not
        assert on the console either**.
  - [ ] Land ADR 0013 §6-style assertions for the new view — on the **embedded grid data**, the **view
        configuration** (stepped colorscale, `zmin`/`zmax`, `showscale:false`, token names, no `--status-*`), and
        the **twin** — following the `GoldenReplacement_*` idiom 24.2 established
        ([SiteGeneratorCodeInsightsTests.cs](tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs)). Three named
        tests, not a hash: a fingerprint says *something* moved; these say *what*.
  - [ ] **Golden fingerprint will move — regenerate deliberately.** `dotnet build --no-incremental` **first**
        (embedded `.css`/`.js` assets are cached by an incremental build, so the hash you measure is stale), confirm
        **stable across two repeated runs**, and split the provenance — say whose changes yours sat on top of
        ([[golden-diff-normalization-gotchas]], CLAUDE.md § Concurrent work). **Never regenerate reflexively:** if it
        moved and you did not touch rendering, audit `GoldenNormalization.NormalizeVolatile` / `FoldToday` first.
  - [ ] ⚠️ **There is exactly ONE fingerprint gate now: `GoldenContentFingerprint`.** `GenerateAll_GoldenIrFingerprint_…`
        was **REMOVED on 2026-07-30** (commit `70b72ab`, [ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md);
        block comment at [SiteGeneratorAdapterTests.cs:1701](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)).
        **Story 24.3's task list still tells you to check both — that instruction is stale, do not chase it.** Never
        propose a whole-tree hash ([[adr-0033-content-drift-gates-are-targeted]]).
  - [ ] `coupling-explorer.html` will **not** join `GoldenOutputInventory` — that fixture is not a git repo, so no
        `--deep-git` page renders there. Do not "fix" the inventory by adding it. The fingerprint still moves, via
        `specscribe.css` / `specscribe.js`.

- [ ] **Task 9 — Live-browser verification, and the size report** (AC: all)
  - [ ] **Live-browser verification is mandatory and is where the real defects will be** (CLAUDE.md § Verification).
        24.2's live pass found **four** defects the suite structurally could not see; 24.1's found two. Verify:
    - [ ] the grid is actually **painted** — non-empty `<image>`, non-zero bbox, correct cell count;
    - [ ] cells are **square** and the block separators land on real block boundaries;
    - [ ] axis labels do not collide and every one is identifiable;
    - [ ] **the whole reading survives colour removal** (AC #3) — apply a greyscale filter and confirm cross-boundary
          is still legible from position, overlay and words;
    - [ ] real `ArrowRight`/`Enter`/`Escape` keys (**not** synthetic dispatch), focus-ring visibility on a cell, and
          the roving index clamped;
    - [ ] per-cell tooltips and their **zero clipping ancestors**;
    - [ ] the view switch does not re-solve, does not re-fetch, and does not move the galaxy's nodes on return;
    - [ ] **mobile 375 px** — a 40×40 grid on a phone is the hardest case in this story;
    - [ ] the **JS-off state showing a complete twin**, over `file://`
          ([[browser-pane-five-server-cap-file-url-fallback]] — note `navigate` **strips the hash**);
    - [ ] the **webview** page ships no empty box and no inert selector entry.
  - [ ] Confirm the surface re-inits after an **SPA navigation** (`specscribe:content-swapped`) and that removed
        mounts are purged.
  - [ ] Run `npm run check:ir-content` **from `web/`** (the script lives in `web/package.json`, not the repo root)
        — a new view with bespoke vocabulary may need `ir-content.css` rules, and its silent half has **shipped an
        unstyled tile** before ([[ir-content-drift-was-data-dependence]], ADR 0018/0026).
  - [ ] **Report the output-size delta** — island bytes for the grid, twin bytes, `specscribe.js` and
        `specscribe.css` deltas, separately.

## Dev Notes

### What this story IS and is NOT

- **IS**: a **file-level, module-blocked, asymmetric adjacency-matrix view**, added as a demoted alternate on
  **one** existing surface (24.3's explorer selector); drawn by Plotly's `heatmap` trace with a **stepped, fixed-cut-point**
  gold ramp and a **real-value** legend; per-cell accessibility restored by an **overlaid `scatter` trace**; and a
  small block-structure extension to the shared text twin.
- **IS NOT**: a new page, a new engine, a new dependency, a second selector, a second support floor, a change to
  `GitMetrics.CouplingMinSupport`, a change to the Story 24.1 metric, the chord view (24.4), the ego card (D4), or
  directory **collapse** of the force-directed galaxy (24.3's D4, still deferred).

### ⚠️ The headline finding: `heatmap` is in the bundle, but it has no DOM

ADR 0030 §6 reads *"24.5 is unchanged: the adjacency-matrix heatmap rides Plotly's `heatmap` trace, already
registered in the shipped bundle, exactly as ADR 0012 §4 permitted."* The spike's §4.3 scored 24.5
`✅ heatmap already in the shipped bundle`. **Both statements are about bundle cost, and both are true.** Neither is
about accessibility, and nothing upstream checked.

Verified at `e864133` against `src/SpecScribe/assets/plotly-hierarchy.min.js` (1,223,517 B, plotly.js 3.7.0, MIT,
embedded at `SpecScribe.csproj:67`): the bundle contains `heatmaplayer`, `createImage`, `toDataURL` and
`image/png`. **Plotly's heatmap paints into a canvas and emits a single `<image>`.** Zero cells in the DOM.

That is the *same* property that got Cytoscape rejected in ADR 0030's options table — *"canvas, zero per-node DOM"*,
failing UX-DR7. It does not reopen the engine decision (the bundle is already shipped and the fix is free), but it
does mean **AC #4's overlay `scatter` trace is load-bearing, not a nicety**, and it means a completion note must say
so plainly. A later reader who finds "24.5 rides `heatmap`" in the ADR and nothing else will reasonably conclude the
trace was a11y-complete on its own.

The build's trace set is deliberate and greppable: `tools/plotly-vendor/build.mjs` sets
`const TRACES = 'sunburst,treemap,heatmap'` with a comment saying `heatmap` *"rides along because the portal's
calendar/heat surfaces are on the same Epic 20 rollout"*. **Those surfaces never used it** — `Charts.CommitHeatmap`
and `Charts.DeliveryCadenceHeatmap` are both hand-rolled SVG. **This story is the first actual use of the `heatmap`
trace in the portal.** Say so in the completion notes; it is the kind of fact that decays into folklore.

### Why the matrix is the only Epic 24 view that can show direction

Story 24.1 built a **directed** metric — `confidence(A→B) = shared / A's own changes` — and both directions of a
pair are emitted because they carry different confidence. Three of the four Epic 24 views throw that away:

| View | Encoding | Directional? |
|---|---|---|
| 24.2 ego graph | radius = confidence, width band = shared commits | Partly — only from the focal file outward |
| 24.3 galaxy | edge width band = shared commits | No |
| 24.4 chord | ribbon thickness = summed support | No |
| **24.5 matrix (this story)** | **cell shade = confidence(row → column)** | **Yes — the transpose is a different cell** |

A row that is dark while its transpose is pale says *"changing this file usually drags that one along, but not the
reverse"* — an architectural finding that no symmetric view can express. **That is the argument for D2, and it is
why the legend and framing sentence must state the matrix is directed.** A reader who assumes symmetry will read
every asymmetric pair backwards half the time.

### The colour problem, and why D3 solves it

A heatmap's only native channel is hue. UX-DR17 and CLAUDE.md § Verification both forbid state signalled by colour
alone. That tension is real and AC #3 exists because of it.

**D3's module-blocked ordering dissolves the hard half.** With both axes ordered by module, the grid partitions into
blocks: **on-diagonal blocks are within-module couples; off-diagonal blocks are cross-boundary couples, by
construction.** So the single most important non-colour distinction this view draws is carried by **position** — the
strongest channel available, and free.

What remains:

- **Shade → value.** Never sole-carrier: hover, focus `aria-label` and the twin all give the exact percentage. This
  is the same discipline ADR 0030 §5 applies to the banded stroke widths, and the legend's fixed real-value ranges
  make the shade itself honest rather than relative.
- **Per-cell cross-boundary emphasis beyond the block.** Pick one non-hue channel — an overlay marker glyph (you are
  already drawing an overlay trace for AC #4, so this is nearly free), a wider `xgap`/`ygap` at block boundaries, or
  a block outline `shape`. **State which and why.**

### Existing surfaces to reuse — do not reinvent

| Need | Reuse | Location |
|---|---|---|
| The whole-repo model + adaptive floor | `CouplingExplorer` (**24.3**) — project from it, never re-floor | `src/SpecScribe/CouplingExplorer.cs` |
| The explorer page + its selector | `CouplingExplorerTemplater` (**24.3**) — extend the selector | `src/SpecScribe/CouplingExplorerTemplater.cs` |
| The component (skeleton, legend/control reveal, island, twin enforcement, `ContainsHost`, `BootScript`) | `RelationshipGraph` (**24.2**) — extend, never clone | `src/SpecScribe/RelationshipGraph.cs` |
| Module identity | `GitMetrics.BoundaryOf` — **private today; 24.4 is also told to widen it. Coordinate, never re-derive** | [GitMetrics.cs:353](src/SpecScribe/GitMetrics.cs) |
| Cross-boundary flag | `GitMetrics.IsCrossBoundary` — computed once, shared (24.1 AC #2) | [GitMetrics.cs:375](src/SpecScribe/GitMetrics.cs) |
| Code/Process classification | `GitMetrics.ClassifyCoupling` — the real one, not the spike's approximation | [GitMetrics.cs:345](src/SpecScribe/GitMetrics.cs) |
| Lift | `GitMetrics.Lift` — the ONE divide-by-zero guard; returns `null`, never `NaN`/`Infinity` | `src/SpecScribe/GitMetrics.cs` |
| Support floor const | `GitMetrics.CouplingMinSupport` — read it, never move it, never re-literal it | [GitMetrics.cs:277](src/SpecScribe/GitMetrics.cs) |
| The directed twin table | `Charts.CouplingTable` (24.1-upgraded) — **already `FromPath`/`ToPath`/`Confidence`**, so D2's cells ARE its rows | [Charts.cs:1770](src/SpecScribe/Charts.cs) |
| Fixed-cut-point bucketing precedent + its rationale | `Charts.OwnershipShareLevel` (`≤25/≤50/≤75/else`, private) | `src/SpecScribe/Charts.cs` |
| Real-value legend-range precedent | `Charts.HeatLevelRange` (public) · `Charts.CodeMapChangeLevelRange` (internal) · `IsHeatLevelUnreachable` (private) | [Charts.cs:193, 2592](src/SpecScribe/Charts.cs) |
| Percent / plural formatting | `Charts.Percent`, `Charts.Plural` | `src/SpecScribe/Charts.cs` |
| Story 10.2 framing | `Charts.ChartMeta` + `Charts.Framed` + `Charts.WhyText(ChartMetric.ChangeCoupling)` | [Charts.cs:13-168](src/SpecScribe/Charts.cs) |
| Per-kind phrase templates (the anti-bloat idiom) | `RelationshipGraph.PhraseFor` + the island's `config.kinds` | [RelationshipGraph.cs:149, 502](src/SpecScribe/RelationshipGraph.cs) |
| Control bar / reveal handshake | `ss-relgraph-controls` + `RelationshipGraph.RevealMarker`, `ss-hierarchy-controls` defer/flush | [RelationshipGraph.cs:332](src/SpecScribe/RelationshipGraph.cs), [specscribe.js:1092-1128](src/SpecScribe/assets/specscribe.js) |
| Tooltip | body-level `.ss-tooltip` via the `SEG` selector family | [specscribe.js:103-107](src/SpecScribe/assets/specscribe.js) |
| Asset flag | 24.2's `AssetManifest.GraphEngineNeeded` — derive from the rendered body via `ContainsHost`, never hand-set | `src/SpecScribe/AssetManifest.cs` |

### Plotly features verified present in the shipped bundle (all zero marginal bytes)

Counted at `e864133` in `src/SpecScribe/assets/plotly-hierarchy.min.js`:

| Feature | Why this story needs it | Occurrences |
|---|---|---:|
| `heatmap` trace | the grid | 8 |
| `scatter` trace | the AC #4 per-cell overlay | 8 |
| `shapes` | module block separators | 8 |
| `annotations` | module band labels | 15 |
| `hovertemplate` / `customdata` | per-cell text without n² sentences | 8 / 5 |
| `zmin` / `zmax` | the fixed 0..1 scale | 2 / 2 |
| `showscale` | suppressing Plotly's continuous colorbar | 2 |
| `xgap` / `ygap` | cell gaps as grid rules | 1 / 1 |
| `tickvals` / `ticktext` | basename axis labels | 3 / 2 |
| `hoverongaps` | no hover card on an empty cell | 1 |

### Measured numbers you can rely on

**From Story 24.6's spike** (`-n 300`: 300 commits, 714 files, 16,604 uncapped pairs) — the set 24.3 hands you:

| Support floor | Nodes | Edges | Payload B | C# solve | Cells if drawn n×n |
|---:|---:|---:|---:|---:|---:|
| 2 (shipped floor) | 391 | 4,864 | 460,817 | 2,611 ms | 152,881 |
| **5 (24.3 auto-tune lands here)** | **129** | **937** | **95,514** | **286 ms** | **16,641** |
| 8 | 73 | 429 | 45,252 | 98 ms | 5,329 |

**The rightmost column is this story's problem and Task 2's bound is the answer.** A matrix is O(n²) in *cells*
where the graph is O(edges) — 129 nodes / 937 edges is a perfectly good galaxy and an unreadable 16,641-cell grid
with 258 axis labels. At a ~40-file bound: 1,600 cells, ~80 labels, and a cell population comparable to 24.2's
already-measured island.

**This repository's module set** (`git ls-files`, top-level, 2026-07-30): `_bmad-output` 278 · `.claude` 235 ·
`.agents` 234 · `src` 154 · `tests` 135 · `web` 89 · `spike` 76 · `docs` 39 · `_bmad` 15 · `tools` 14 ·
`extension` 12 · `.github` 8 · root 7 · `.vscode` 3 · `.config` 1. **≤14 blocks**, fewer after 24.3's floor — which
is what makes D1's separators legible rather than a lattice.

Expect **62% cross-boundary** and **46% Process-class** edges, and `sprint-status.yaml` coupled to 92% of the graph
at floor 2 — which is why the floor exists and why 24.3's framing sentence must say what it hid.

### Previous-story intelligence (24.1 · 24.6 · 24.2 shipped · 24.3 pending · 24.4 concurrent)

- **The metric spine exists and is correct** (24.1): `CoupledFile`, `DirectedCouple`, `DeepGitPulse.DirectedCoupling`,
  `IsCrossBoundary`, `CouplingMinSupport`, `Lift()`, `Charts.Percent`.
- **24.1's Q4 is CLOSED.** 24.2 measured 20 coupled files on `Charts.cs` at **15 distinct confidence values across
  13%–75%** — confidence discriminates at 20 where it did not at 10 — so no ranking-policy change was proposed.
  **D2 uses confidence for the cell value, which is consistent with 24.2's radius encoding.** Do not introduce a
  fourth encoding.
- ⚠️ **Confidence must never be rounded through `CouplingLayout.Format`.** Its doc comment records the measurement:
  4-decimal rounding collapsed **453 distinct confidences into 452**. That formatter is for *coordinates*, where a
  thousandth of a pixel is invisible. **Collapsing two confidences is a lie.** Cell values reach the reader through
  `Charts.Percent` and the twin, at their own precision.
- **24.2's live pass found FOUR defects the suite structurally could not see** — an unrevealed control bar, a legend
  rendering with JS off above a `display:none` host, nodes drawn outside the host from an inverted `scaleanchor`,
  and 20 overlapping markers. All four are rendered geometry or rendered honesty. **Expect the same class here.**
  Specifically inherited: the legend and control bar are emitted `hidden` and revealed **on mount** — a matrix
  legend that renders visible with JS off is defect #2 repeated.
- **The deep-git 3s-timeout flake is real and silently produces no deep surfaces at all**
  ([[gitmetrics-3s-timeout-silent-deep-git-loss]]). It cost 24.1 two generation attempts. If a `--deep-git` run comes
  back with no coupling, **suspect the timeout before suspecting your code**.
- **Suite "flake" is usually a running preview server** ([[suite-flake-cause-is-a-running-preview-server]]) — git
  SPAWN starvation. Stop previews before the full suite. The browser pane also caps dev servers at **5 per folder
  across all chats**; verify over `file://`, and note `navigate` **strips the hash**.

### Webview and SPA

- **Webview:** `WebviewRenderAdapter.StripDataIslands` removes every `<script type="application/json">` island
  ([WebviewRenderAdapter.cs](src/SpecScribe/WebviewRenderAdapter.cs)), so **the webview cannot receive a graph
  payload today**. Take the **ADR 0013 §7 text-twin fallback** — the same call 24.2, 24.3 and 24.4 each took — and
  **verify the webview page ships no empty box and no inert selector entry**. Narrowing that exception is a joint
  decision with the hierarchy family and would want its own ADR (CLAUDE.md § Decision records). CSP itself is fine:
  `script-src 'nonce-…'` alone suffices, header **and** meta, no `'unsafe-eval'`. **Read the policy from
  `WebviewRenderAdapter.cs` at runtime rather than citing a line** — it drifted `:116 → :140` during the spike
  ([[cite-adrs-by-symbol-not-line-number]]).
- **SPA:** the `specscribe:content-swapped` seam re-inits components after a content swap
  ([[story-20-2-zoomable-drill-in-done]]); 24.2 verified its a11y layer survives 5/5 re-render events including a
  bare `Plotly.react` it did not initiate. The matrix view must survive the same set.

### Preservation invariants — leave the system working end-to-end

- **Baseline output byte-identical WITHOUT `--deep-git`.** No coupling data → no matrix, no selector entry, no asset
  flag. Verify, do not assume.
- **`GitMetrics.CouplingMinSupport` does not move.** Code pages and the Git Insights hub read it, and 24.1 already
  learned a floor change there is a site-wide visible behaviour change.
- **24.3's three silhouettes keep working unchanged**, and the force-directed galaxy stays the **primary**
  representation (UX-DR21). **24.4's chord entry, if it has landed, keeps working unchanged.**
- **The file-level text twin is never trimmed** to make room for the block summary (ADR 0013 §2 forbids a partial
  twin).
- **The seven existing `level-1`/`level-2` CSS rules must render byte-identically** if you take Task 3's token
  route. Verify in a browser, not by reasoning.
- **Every chart needs an accessible text equivalent, and no state may be signalled by colour alone**
  (CLAUDE.md § Verification, UX-DR17/19).
- Output dir is `SpecScribeOutput` ([[generate-output-dir-is-specscribeoutput]]). Never `--output docs/live`.

### Files being modified — read current state before editing

- `src/SpecScribe/CouplingExplorer.cs` — **24.3's file.** The matrix projection, ordering and bound.
- `src/SpecScribe/CouplingExplorerTemplater.cs` — **24.3's file.** Selector entry, matrix legend, view registration.
  ⚠️ **24.4 edits this too.**
- `src/SpecScribe/RelationshipGraph.cs` — **24.2's file.** Only if the component genuinely needs widening; prefer
  the declared-view mechanism 24.3 shipped.
- `src/SpecScribe/Charts.cs` — the confidence level + range functions. ⚠️ **50 open Sonar observations**; already at
  its complexity ceiling. Two small pure functions with doc comments are the right size; anything larger belongs
  beside the matrix model instead. **Do not add to `Charts.cs`'s complexity findings.**
- `src/SpecScribe/GitMetrics.cs` — widen `BoundaryOf` **only if 24.4 has not already**. A one-line visibility change
  with a site-wide blast radius; grep every caller. ⚠️ 8 open observations.
- `src/SpecScribe/assets/specscribe.js` — the matrix renderer, the overlay trace, the view switch, and **Trap 2's
  trace-selector fix**. ⚠️ **24.4 edits this too. No Sonar shard — UNKNOWN, never clean.**
- `src/SpecScribe/assets/specscribe.css` — matrix styles, and the `--heat-1`/`--heat-2` tokens if you take that
  route. ⚠️ **24.4 edits this too.** ⚠️ **A `*/` inside a comment silently truncates ~1000 rules**
  ([[css-comment-star-slash-silent-truncation]]).
- `src/SpecScribe/AssetManifest.cs` — reuse 24.2's graph flag; **do not add a second.** No Sonar shard.

### Shared-main discipline (CLAUDE.md § Concurrent work) — sharper here than usual

**D4 deliberately lets this story run concurrently with Story 24.4.** Three of your files are on 24.4's list too
(`CouplingExplorerTemplater.cs`, `specscribe.js`, `specscribe.css`), and two more may be (`GitMetrics.cs`'s
`BoundaryOf` widening, `CouplingExplorer.cs`). Both stories also extend **the same selector**.

- **Grep-verify every new symbol after writing it** — a `Charts.cs` edit has silently vanished this way before
  ([[shared-main-concurrent-edit-loss-verify-after-edit]]; a zero-grep can also be a **transient mid-write**).
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**
- **Expect the golden fingerprint to move under you** from 24.4's session. Establish causality before regenerating;
  bisect into a throwaway tree (`git archive HEAD` into the scratchpad) rather than resetting the shared tree.
- **Attribution by hunk, not by file** (CLAUDE.md § Scoping a code review). Record which hunks are yours in the
  File List, and name any you inherited from 24.4 so neither review skips them.
- **Three shared obligations both stories carry — do whichever is still undone, and SAY SO:** widening
  `GitMetrics.BoundaryOf`, fixing `nodePaths()`'s positional last-trace selector, and adding a selector entry.
  Doing any of them twice, differently, is worse than either story doing it once.

### Analysis observations

`.specscribe/analysis/` was evaluated at **`bc7a379`** while HEAD is **`e864133`** — per CLAUDE.md's read-time rule,
**the digest is stale regardless of what `isStale` says** (it already reports `analysis-behind-working-tree` +
`working-tree-dirty`). Re-run `node tools/analysis-digest/index.mjs` (Task 1) before trusting a line number. Read
**shards**, not `index.json`: `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`.
Known directionally at `bc7a379`: `SiteGenerator.cs` **91**, `Charts.cs` **50**, `SiteNav.cs` **9**,
`GitMetrics.cs` **8**. `specscribe.js`, `AssetManifest.cs`, `RelationshipGraph.cs` and `CouplingLayout.cs` have
**no shard** — they postdate the analysis or are untracked by it, so that is **UNKNOWN, never clean**.

### Project Structure Notes

No new page, no new nav entry, no new CLI flag, no new dependency, no new engine family, no ADR amendment (ADR 0030
§6 already ratifies this view — unlike 24.4, which had a named gap to close). One existing surface gains a view.
The matrix model may justify one new `src/SpecScribe/*.cs` file plus its test sibling — decide by size, and say
which you chose. If working in a worktree, target the worktree path — `main` has a background auto-committer
([[worktree-edits-must-target-worktree-path]]).

### References

- [Source: docs/adrs/0030-epic-24-graph-engine.md] — the engine decision. **§6 names this story and ratifies the
  `heatmap` trace** · §2 position-is-data · §3 **normative** determinism construction · §4 filters-hide-never-re-lay-out ·
  §5 per-edge emphasis + the "never sole-carrier" discipline · the options table's **Cytoscape rejection on
  zero-per-node-DOM**, which is the precedent AC #4 turns on.
- [Source: docs/adrs/0013-text-twin-is-the-no-js-contract.md] — §1 amended NFR-5 · **§2 the four twin properties
  (server-rendered · complete · navigable · non-colour; collapsed/`sr-only` acceptable)** · §3 the per-surface gate ·
  §4 supersedes ADR 0010 §2 · §6 fingerprint replacement · §7 webview fallback.
- [Source: docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md] — §2 component
  contract + "one selector idiom" · §3 `navigate`\|`select` mode grammar · **§4 engine-family boundary, which
  already freed the `heatmap` trace for this story** · §6 tokens-not-colorways · §7 generation-time determinism ·
  **2026-07-29 addendum: N server-declared views over one shared payload**.
- [Source: docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md] — why `GoldenIrFingerprint` is gone
  and why a whole-tree hash is never the answer.
- [Source: _bmad-output/implementation-artifacts/24-6-spike-report.md] — **§4.3 the four-shape coverage table
  (which scores 24.5 on trace availability alone — see the headline finding)** · §7.1 determinism · §7.3 at-scale
  table · **§10 the hand-off row addressed to Story 24.5**.
- [Source: _bmad-output/implementation-artifacts/24-3-whole-repo-coupling-explorer.md] — the surface this story
  extends; its adaptive-floor spec, its `NodeBudget`/`EdgeBudget`, its twin-completeness invariant, and its
  flag-forward that 24.4/24.5 must extend **one** selector.
- [Source: _bmad-output/implementation-artifacts/24-4-chord-arc-diagram-view.md] — the **concurrent** sibling; its
  D2 module aggregation, its `BoundaryOf`-widening instruction, and its Trap 1 (the same positional trace selector).
- [Source: _bmad-output/implementation-artifacts/24-2-per-file-ego-coupling-graph.md] — the component and its
  island idiom; its four live-only defects; the payload-halving measurement; the `GoldenReplacement_*` test idiom.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 24] — epic charter, FR40, UX-DR19/20/21, NFR8, execution
  order 24.1 → 24.6 → 24.2 → 24.3 → 24.4/24.5.
- [Source: src/SpecScribe/GitMetrics.cs] — `CodeMapMetrics` (70), `CoChangePairs` (82), `CoupledFile` (213),
  `DirectedCouple` (229), `CouplingMinSupport` (277), `CouplingKind` (308), `ClassifyCoupling` (345),
  **`BoundaryOf` (353, private)**, `IsCrossBoundary` (375).
- [Source: src/SpecScribe/Charts.cs] — `HeatLevelRange` (193, public), `CouplingTable` (**1770, already directed**),
  `CodeMapChangeLevelRange` (2592, internal), `OwnershipShareLevel` (~2645, private, **the fixed-cut-point
  precedent**), `HeatThresholds` (3579, private), `HeatLevel` (3614, private), `Percent` (3640),
  `ChartMetric.ChangeCoupling` (20/63), `Framed`/`ChartMeta` (13-168).
- [Source: src/SpecScribe/RelationshipGraph.cs] — `HostMarker`/`RevealMarker` (38/54), `Size` (80), `BootScript`
  (91), `ContainsHost` (99), `PhraseFor` (149), the model records (168-207), `WidthBands` (217), `StyleFor` (259),
  `Render` (304), `LegendHtml` (374), `IslandHtml` (439).
- [Source: src/SpecScribe/CouplingLayout.cs] — class remarks (the normative determinism clauses), `Solve` (97),
  **`Format` (295) and `CoordinateFormat` (74) — read the doc comment on why confidence must NOT take this path**.
- [Source: src/SpecScribe/assets/specscribe.js] — tooltip `SEG` (103-107), zero-width defer/flush (1092-1128),
  failure unwind (1063-1080), **`nodePaths` last-trace assumption (2894-2898)**, aspect-anchor remarks (2865-2881),
  trace assembly (3053).
- [Source: src/SpecScribe/assets/specscribe.css] — the level-1..4 ramp across seven rule families (4272, 4375,
  4458, 4751-4755, 4810, 6393, 6450).
- [Source: tools/plotly-vendor/build.mjs] — `TRACES = 'sunburst,treemap,heatmap'` and the comment explaining why
  `heatmap` was included for surfaces that never used it.
- Prior art: Story 3.2 (`deep-analytics.html`'s coupling table), Story 7.11 (`OwnershipShareLevel`'s fixed cut
  points), Story 10.2 (chart framing + real-value legends), Story 10.6 (the Code/Process lens), Story 10.7 / 21.1
  (the misdescribing-legend class), Story 20.4 (the a11y decision rule + the unclamped roving index), Story 20.5
  (never `transition` a Plotly-owned property), Story 20.10 (N views over one payload), Story 23.4 (`PageView`,
  region composition).

### Open questions for the owner — do NOT block dev-start

1. **The diagonal.** Gap, or a labelled self band? Task 2 asks for a decision and a statement; the right answer is
   likely visible only once it is drawn. Raise the rendered result in the verify round.
2. **The draw bound.** ~40×40 is a recommendation from the 129-node measurement, not a measured optimum. Show the
   owner the rendered grid at the number you chose, with the "+N more" figure visible.
3. **The `--heat-1`/`--heat-2` token route.** Recommended, but it touches seven shipped CSS rules across four
   unrelated surfaces. Confirm before landing it, or take the probe route and say why.
4. **The per-cell cross-boundary channel** (AC #3) — overlay glyph, gap, or block outline. Overlapping non-hue
   channels on a dense grid are easy to over-egg; show the rendered result rather than picking silently.
5. **Whether the asymmetry reads.** D2's entire premise is that a dark cell beside a pale transpose is legible as a
   finding. If it is not — if readers keep reading the matrix as symmetric — that is a legend/framing problem worth
   a round, not a reason to change the metric.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-30 | Story 24.5 created (baseline `e864133`). **Status `blocked`, one gate: Story 24.3**, whose own gate is cleared (24.2 is `review`, `CouplingLayout.cs` + `RelationshipGraph.cs` both exist) — flip to `ready-for-dev` when 24.3 reaches `review`. Four owner decisions elicited up front: **D1** **files** on both axes **with module block separators** (not modules — 24.4 already took the module reading, so modules here would leave file-level density unrendered anywhere); **D2** cell = **directional confidence(row→column)**, so **the matrix is ASYMMETRIC** and is the **only** Epic 24 view that can express direction at all — 24.2/24.3/24.4 are all symmetric; **D3** **module-blocked ordering, ranked within**, which discharges both AC #1's clustering requirement and AC #3's colour problem, because an off-diagonal block **is** a cross-boundary couple; **D4** **explorer only**, gated on 24.3 alone and therefore **deliberately concurrent with 24.4**. AC #1 amended (files + block separators + module-blocked order + real-value legend, never Plotly's colorbar); AC #2 amended (ADR 0013 text twin only, no static SVG). **AC #3 added** for the colour problem — a heatmap's only native channel is hue and UX-DR17 forbids colour-alone. **AC #4 added for the headline structural finding: Plotly's `heatmap` trace rasterizes to a single `<image>` with ZERO per-cell DOM** — verified at `e864133` in the shipped bundle (`heatmaplayer`, `createImage`, `toDataURL`, `image/png`) — which is the **exact** property on which ADR 0030 **rejected Cytoscape** under UX-DR7. ADR 0030 §6's "24.5 is unchanged: it rides `heatmap`" and the spike's §4.3 `✅` are both true about **bundle cost** and silent about **accessibility**; per-cell identity must be restored by an overlaid `scatter` trace (still zero marginal bytes). Further structural findings recorded: a matrix is O(n²) in **cells** where the graph is O(edges), so 24.3's 129-node auto-tune yields **16,641 cells and 258 axis labels** and this story needs its own declared draw bound; `Charts.CouplingTable` is **already directed**, so D2's cell set and the twin's row set are the same set for free and the twin needs no new pair rows — only a block-structure summary, because **aggregation and ordering create facts**; the confidence ramp must use **fixed cut points** (`OwnershipShareLevel`'s shipped precedent and its stated rationale) rather than `HeatThresholds`' data-relative quartiles, and `HeatLevel`/`HeatThresholds`/`OwnershipShareLevel` are **all private** so two new functions are needed; **`--heat-1`/`--heat-2` DO NOT EXIST** — levels 1–2 are literal hex repeated across seven CSS rules in four surfaces, and ADR 0012 §6 forbids a literal colour in a Plotly payload; confidence must **never** be rounded through `CouplingLayout.Format` (measured collapsing 453 distinct confidences into 452); and `heatmap` was vendored for calendar surfaces that **never used it**, making this the first actual use of the trace in the portal. Three obligations are **shared with the concurrent Story 24.4** — widening `GitMetrics.BoundaryOf`, fixing `nodePaths()`'s positional last-trace selector, and extending the one selector — and the story instructs whichever runs second to reuse rather than repeat. |
