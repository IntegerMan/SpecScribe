---
baseline_commit: 8a2fb8352f882debb2e81c7369f52366f6a24c53
---

# Story 20.10: Shared Hierarchy Payload Across Code Map's Filter Variants

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (the component + the mode contract) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) (the text twin is the no-JS contract)
**Seated by:** Story 20.9's code review (2026-07-28) — a decision-needed finding the owner asked to be investigated and proposed as its own story. It closes Story 20.9's Open Question #1.
**Blocked by:** nothing. Story 20.9 is `done`; everything this story extends is in the shipped tree.
**Baseline commit:** `8a2fb83`

## Story

As a maintainer who wants Code Map's byte cost to reflect real information rather than serialization overhead,
I want the four filter-variant panels to stop each independently re-serializing shared files' full payload,
so that `code-map.html`'s size reflects the number of distinct files analyzed, not the number of filter combinations that happen to include them.

---

## ⛔ Read first — what this story IS and IS NOT

**IS:** collapse `code-map.html`'s **four** independently-serialized chart payloads and **four** independently-serialized file tables into **one of each**, with the four filter variants expressed as server-declared *views* over that one payload. Every file's metric bag, hover card and table row is serialized once. The chart becomes **one component instance**; the table becomes **one table** whose rows are hidden by the same pure-CSS checkbox toggle that switches panels today. Then measure, and report against both Story 20.9's post-conversion baseline and the Story 20.4 spike's original projection.

**IS NOT** (each of these breaks a gate, re-litigates a decision, or blows scope):

| Not this story | Why |
|---|---|
| Turning the exclude-spec / exclude-tests filter into a **JS** filter | Story 20.9 owner decision D2. It is the one filter on this page that works with JavaScript off, and it must still work with JavaScript off when this story lands — for the **file table**, which is this surface's ADR 0013 twin. |
| Changing what the chart looks like, what it colours, or which files it draws | This is a **serialization** change. Every panel must draw the same sectors, in the same places, in the same colours it draws today. Any visible difference is a regression to be found in Task 9, not a feature. |
| Replacing the file table with the component's generic nested twin | Story 20.6 D1 / Story 20.9's `HierarchyTwinDisplay.External`. The table is richer — it carries six git metrics per file as real cells. It gets **deduplicated**, not replaced. |
| Touching Git Insights ownership | One instance, one payload already. Nothing here applies to it. |
| Re-opening the rich hover cards | Story 20.9 measured them at 42% of the island and Story 20.5 made `.ss-tooltip` + `data-tip-html` the one tooltip system site-wide. AC#4 requires the duplication saving to be **isolated from** any hover-card change, which is impossible if you also change them. |
| Bumping the vendored Plotly bundle | Pinned at 3.7.0. A bump invalidates every measured number in the 20.4 spike and in Story 20.9's accounting, which is exactly what this story reports against. |
| Ranking or scoring anything | FR-10 / ADR 0010 §4 are unaffected by serialization. |

---

## Owner decisions locked at create-story (2026-07-28)

Four, elicited against a code-level read of `HierarchyExplorer`, its projectors and island, `CodeMapTemplater`, `CodeMap.BuildVariants` and the component block in `specscribe.js` at `8a2fb83`. **Two of them exist because the epic's stated premise is factually wrong about the shipped code** — see F1 and F2.

### D1 — Share the LEAVES; keep each variant's DIRECTORY scaffolding server-emitted

One island carrying **each file node exactly once** — its metric bag, hover card, label, detail, href, colour class — plus **all four variants' directory node lists verbatim from the server**, plus a per-variant membership mapping that says which files a variant contains and which directory each one hangs under *in that variant*.

Rationale, and it is a correctness constraint rather than a preference: **directory-chain collapse is variant-dependent** (F2). A union tree cannot express four different collapse structures, so the alternatives were to port `CodeMap.BuildDir`'s collapse rule into JavaScript — a second copy of a structural rule, exactly the drift ADR 0012 exists to end — or to accept filtered views rendering chains the server would have collapsed, which is a visible fidelity regression. Keeping the scaffolding server-emitted dissolves the problem: **the server stays the sole source of tree structure**, and the client only ever selects and rolls up.

It is also nearly free. Directory nodes carry no metrics, no hover card, no href and no detail; all four variants' scaffolding together is **542 node instances** against **2,970 file instances**, and the file instances are where every expensive field lives.

### D2 — ONE component instance, above the panels

A single chart panel, outside the four `.codemap-view` wrappers; the two checkboxes drive a client-side **view switch** on that one instance. One Plotly mount instead of four, one `DomId`, one `HashKey`.

Accepted consequences, stated plainly: the framed title and the analysis window must **track the active view** (they are per-variant strings today); the ramp and discrete legends must too (F3, F4); and the chart's variant state becomes JavaScript-driven. That last one costs nothing real — the chart already requires JavaScript regardless of how many payloads back it, and D2 of Story 20.9 was never about the chart. **The file table's no-JS filterability is the guarantee, and D4 below keeps it.**

### D3 — The file table is deduplicated too

One table, every distinct file once, each row carrying `is-spec` / `is-test` marker classes, hidden by the **same** `#cm-exclude-spec:checked ~ …` sibling-combinator idiom that switches panels today. The no-JS guarantee is preserved *by construction* — it is still pure CSS, still zero script.

Measured, this is not a rounding error: the four tables are **1,076,146 B** of `code-map.html` carrying **2,970 rows** against **1,189 distinct files**. Deduplicating them is roughly another **626 KB** on top of the island win, and leaving it would leave a second story's worth of the same defect on the same page.

### D4 — Ramp normalization stays PER-VARIANT, and the legend moves with it

The six numeric ramps re-resolve their `[min,max]` scale against the **active view's** file subset on every view switch, and the ramp legend's real change-count ranges re-render to match.

This preserves today's colours exactly, which is what makes the conversion provably colour-neutral and testable against Story 20.9's shipped output. The alternative — one scale across all four views — is arguably more honest in the abstract but would **recolour three of the four panels**, a visible regression against verified output, introduced as a side effect of a serialization change.

**The legend is not optional here.** `AppendLegend` bakes `Charts.CodeMapChangeLevelRange(l, maxChanges)` into each swatch from that variant's own `maxChanges`. A scale that moves while the legend does not is the "phantom / misdescribing legend entry" class that Stories 10.7 and 21.1 each closed. **They move together or the story is wrong.**

---

## Acceptance Criteria

*Refined at create-story from the drafts in [`epics.md` § Story 20.10](../planning-artifacts/epics.md), which were explicitly marked "draft — refine at create-story". `epics.md` is amended in the same change: draft AC#1 widened to the file table per D3, and the "What makes this non-trivial" paragraph corrected — the client-side re-layout capability it says does not exist largely does (F1).*

1.
**Given** the four Code Map filter variants
**When** a file appears in more than one variant
**Then** its metric bag, its hover card, its label/detail/href, and its file-table row are each serialized **exactly once** in `code-map.html`
**And** each variant's own directory scaffolding is still emitted by the server, so no tree-structure rule is duplicated client-side.

2.
**Given** a filter checkbox toggle
**When** the active view changes
**Then** the chart re-lays-out for the newly-active subset with the **same node set, the same parent-child structure including directory-chain collapse, and the same rolled-up values** a from-scratch server render of that variant would have produced
**And** the four Story 20.4 invariants still hold at every view: exactly one root, no `null` in `values`, `parent == Σ children`, `branchvalues` equal to `HierarchyExplorer.BranchValues`
**And** the fills, hatches, strokes and accessible names are **unchanged** from what Story 20.9 shipped for that variant, across all seven dimensions (D4).

3.
**Given** the pure-CSS exclude-spec / exclude-tests toggle (Story 20.9 owner decision D2)
**When** JavaScript is disabled
**Then** the file table continues to filter correctly, showing exactly the rows of the selected variant and no others
**And** the twin stays complete under ADR 0013 §2 for **every** variant — every file the chart would draw for that view has a row with a resolving link
**And** a variant that filters down to nothing still renders its honest "No files match this filter." notice with JavaScript off (NFR8).

4.
**Given** the re-architected payload
**Then** the measured byte delta on `code-map.html` is reported against **both** Story 20.9's post-conversion baseline (4,451,207 B) and the Story 20.4 spike's original projection (−3,493,000 B), with the **island** saving and the **file-table** saving reported separately and both isolated from any hover-card or encoding change
**And** the resulting B/node figure is stated against the spike's 195.4, Story 20.5's ~390 and Story 20.9's measured 936.

---

## 🔴 Eight findings that change the work — read before planning

All code-verified 2026-07-28 against the live tree at `8a2fb83`. **F1 and F2 contradict the epic's own text**, which was written from the code review's investigation notes rather than from the shipped component.

### F1 — The "capability that doesn't exist yet" mostly EXISTS. Read `visibleNodes()` before you design anything

`epics.md` says a shared payload "requires the CLIENT to recompute area allocation for a filtered subset on every checkbox toggle — a capability that doesn't exist yet", and that "Story 20.7's client-side node filter handles drill-in visibility … it does not re-lay-out a treemap for a genuinely different node subset."

**That is wrong about the shipped code.** `specscribe.js`'s `visibleNodes()` already:

- projects the embedded payload to a kept subset,
- **re-runs the parent roll-up bottom-up with the same children-win rule the emitter uses** (`specscribe.js`, the block headed *"The node filter (config-gated)"* — its own comment says exactly this), and
- re-plots through `Plotly.react`, which recomputes the squarified/annular layout from `ids`/`parents`/`values`.

Plotly does the area allocation. Nothing needs to be ported for that.

**What is genuinely missing is two smaller things:**

1. **Granularity.** The filter keeps *root children* and their descendants (`if (n.parentId === ROOT_ID && filterState[n.id]) keep[n.id] = true`). Code Map's variants remove **scattered leaves** anywhere in the tree, and prune directories that empty out. Different predicate, same machinery.
2. **Scaffolding selection.** A view switch must swap in that variant's directory nodes as well as its files (F2 / D1).

**Extend `visibleNodes()`; do not mint a second projection path.** The existing roll-up loop is the one that already satisfies Story 20.4 Finding C, and a second one is how two views start disagreeing.

### F2 — Directory-chain collapse is VARIANT-DEPENDENT, and there is a proof in this repository

`CodeMap.BuildDir` collapses a chain while `cur.Files.Count == 0 && cur.Dirs.Count == 1`, joining labels (`"a / b"`) and taking the **deepest** segment's path as the node id. Filtering files changes both conditions, so it changes the collapse.

The concrete case, verified against this repo's own tree and `CodeMap.SpecDevPathPrefixes` (`{ ".agents", ".claude", "_bmad", "_bmad-output", ".github/agents" }`):

| Variant | `.github` renders as |
|---|---|
| `full` | node id `.github`, label `.github`, with children `agents` and `workflows` |
| `no-spec` | `.github/agents/*.agent.md` are filtered out, so `.github` has one subdirectory and no files → **collapses**: node id `.github/workflows`, label `.github / workflows` |

So across variants: a directory id present in one view is **absent** in another; the *same* id `.github/workflows` carries a **different label and a different parentId** depending on the view; and therefore **a file's `parentId` is not a property of the file** — it is a property of (file, view).

Three consequences you must design for:

- Directory nodes cannot be shared across views by id. Namespace them per view, or carry them in per-view lists (D1's shape).
- A shared file node cannot carry a single `parentId`. Its parent must come from the view's membership mapping.
- `EnumerateCodeFiles` applies **no extension filter** (`git ls-files`, then anything text-readable), so `.agent.md` and `.yml` are both in the map — this case is live, not theoretical. Confirm it by running `CodeMap.BuildVariants` on this tree and diffing the directory id/label sets before you write code; if it does *not* reproduce, say so, because D1's whole justification rests on it.

### F3 — The ramp scale is computed over the WHOLE payload, once per dimension switch — not per filter change

`resolveDimension()` scans `NODES` for `[min,max]` and its comment is explicit: *"The scan spans the WHOLE payload, not the drilled or filtered view."* Today that is correct **because each panel's payload IS one variant**. With one shared payload it silently becomes "all four variants at once", which is D4's rejected option arriving by accident.

Two things follow:

- `resolveDimension()` must scan the **active view's** nodes, and
- `applyDimension()` must **re-run on a view change**, not only on a dimension change. It does not today (nothing was dimension-bearing *and* filterable before this story).

`Charts.Bucket`'s rule is mirrored by hand in `specscribe.js`'s `bucket()`. Do not touch either — only the set being scanned changes.

### F4 — Both legends are per-variant server-baked strings, and one of them is data-dependent

- **Ramp legend** (`CodeMapTemplater.AppendLegend`): each swatch's label is `Charts.CodeMapChangeLevelRange(l, maxChanges)` from **that variant's** `Charts.ComputeMaxChanges(variant.Map.Roots)`, and `Charts.IsCodeMapChangeLevelUnreachable` may **omit** a level entirely. So the four variants can have different swatch *counts*, not just different numbers.
- **Discrete legend** (`AppendDiscreteLegend`): lists only the categories **present in that variant's file set**, deliberately — "so a repo with no config files doesn't show an unused Config & Data swatch".

With one instance, both must track the active view. Cheapest honest route: keep emitting all four variants' legend blocks server-side (they are small — swatches and short labels, not per-file data) and have the component show the pair belonging to the active view, the same way it already shows exactly one of several `data-hierarchy-legend` blocks per dimension. **Do not rebuild legend content in JavaScript** — that would put `CodeMapChangeLevelRange`'s arithmetic in a second place.

Watch the specificity trap Story 20.9 F3 records: `.ownership-legend[hidden] { display: none; }` exists only because an author `display:flex` ties with the UA `[hidden]` rule. Any legend markup gaining a `hidden` toggle inherits that hazard.

### F5 — The reveal machinery loses its only consumer. Decide deliberately; do not sweep it up

Under D2 the chart is always visible, so nothing is mounted inside a `display:none` container any more. Searched at `8a2fb83`, the only consumers of the Story 20.9 F1 reveal machinery are this page:

| Symbol | Consumers |
|---|---|
| `data-hierarchy-reveal` (attribute) | `CodeMapTemplater.AppendFilterCheckbox` only |
| `data-hierarchy-reveal-when` | `CodeMapTemplater.AppendVariantPanel` only |
| `revealPanelsNamedByHash()` | boot only, reads `data-hierarchy-reveal-when` |
| `deferHierarchyMount` / `flushHierarchyReveals` / `hierarchyPending` | the zero-width guard `if (!hierarchyPanelOf(root).clientWidth)` |

`revealPanelsNamedByHash` and `data-hierarchy-reveal-when` become genuinely unreachable and should go, along with their tests. **The zero-width deferral guard is a different case** — it is the component's general answer to "I may be mounted inside a hidden container", it costs nothing when nothing defers, and Story 20.9 paid for it with a live-caught defect. Recommendation: **keep the deferral guard, delete the hash-reveal path**, and state the split and the reasoning in the Completion Notes. This is anti-pattern 9 territory from Story 20.9 (`CompactMetricsTail` was on a delete list and had a live caller) — grep before deleting anything, and grep again after.

### F6 — `CodeMapVariant.Layout` is ALREADY dead in production, and it is computed four times per generation

`CodeMap.BuildVariants` calls `map.Layout()` for every variant — the squarified tiling algorithm, four times, over the whole file set. Searched at `8a2fb83`, the only remaining references to `CodeMapVariant.Layout` are **tests** (`CodeMapTests`, `CodeMapTemplaterTests`) and one stale comment in `SiteGeneratorAdapterTests` naming a `Charts.CodeFreshnessTreemap` that **no longer exists**. Story 20.9 retired the SVG that consumed it and left the slot behind.

It is squarely on this story's theme — stop paying four times for one codebase — and it is generation-time cost, not bytes. **Verify by search first**, then remove the `Layout` slot from the record and the `Layout()` call from `BuildVariants`; keep `CodeMap.Layout()` the method if a test still needs it, and say so. Report the generation-time delta on `code-map.html`'s phase. If a live production consumer turns up, leave it alone and record that instead.

### F7 — The hover-card cap becomes ONE decision instead of four

`Charts.SelectDetailedCodeMapFiles(files, totalFileCount)` returns `null` (no cap) below `MaxDetailedCodeMapFiles = 4000` and otherwise keeps the top 4,000 by significance. Today each variant caps independently against its own count; deduplicated, the cap applies once against the distinct-file count.

At this repository's scale (1,189 files) the cap never fires, so **you will not see this in local verification** — it is a large-repo behaviour change. It is a *better* rule (one file, one decision, and the chart and table cannot disagree about which files are "detailed"), but it must be asserted rather than assumed: Story 20.9's own review added `ProjectCodeMap_AboveTheDetailCap_LongTailKeepsGeometryButLosesTheCard` for exactly this, and its shared-payload analogue is Task 8's.

The same cap drives `AppendFileTable`'s `shown`/`omittedCount` truncation row. With one table, the "+N more files not shown" row and its `colspan` are computed once. Keep them.

### F8 — What the test surface actually asserts about four panels

Counted at `8a2fb83`:

| Target | Count | Note |
|---|---:|---|
| `codemap-` class assertions in `CodeMapTemplaterTests.cs` | 34 | panel structure, `data-view`, the reveal attributes, table markup |
| `codemap-` assertions in `SiteGeneratorCodeMapTests.cs` | 8 | end-to-end page shape |
| `HierarchyColorizeTests.cs` | 560 lines | the seven dimension declarations, the four 20.4 invariants per projector, the cap test |
| `HierarchyExplorerTests.cs` | 831 lines | island shape, twin, `Render` framing |
| `HierarchyRolloutTests.cs` | 525 lines | the **empty** rollout allowlist — Epic 20 AC#2's assertion. Do not disturb it. |
| `CodeMapTests.cs` | 514 lines | `BuildVariants`, collapse, `Layout` (F6) |

Story 20.9's F7 lesson applies unchanged: **class-name assertions do not fail at compile time.** They fail as "expected markup not found", and many of them assert a *fact* — this file is charted, this file links to its code page, this variant excludes tests — that survives into the new shape. Split rewrite-vs-delete deliberately and **report the split and what coverage genuinely went away**.

---

## Measured starting state

From Story 20.9's Task 7.7 accounting, measured on `d1722f1`. **HEAD is `8a2fb83`, several commits later, and this portal documents its own repository — so these numbers have moved. Re-capture them on your own tree before you start (Task 0.3); reporting AC#4 against a figure from this document is the easiest way to publish a wrong delta.**

| `code-map.html` | Bytes | Instances | Distinct |
|---|---:|---:|---:|
| Whole page | 4,451,207 | — | — |
| Island (4 payloads) | 3,288,932 | 3,512 nodes | 1,421 |
| — of which hover cards | 1,379,388 (42%) | | |
| File tables (4) | 1,076,146 | 2,970 rows | 1,189 |
| B/node | 936 | | |
| B/node excluding cards | 544 | | |

Per-variant, from Story 20.9's live verification:

| Variant | Sectors | of which files | of which dirs | Table rows |
|---|---:|---:|---:|---:|
| `full` | 1,421 | 1,189 | 232 | 1,189 |
| `no-spec` | 487 | 440 | 47 | 440 |
| `no-tests` | 1,254 | 1,036 | 218 | 1,036 |
| `no-spec-no-tests` | 350 | 305 | 45 | 305 |
| **Total** | **3,512** | **2,970** | **542** | **2,970** |

**Estimate this story should be measured against — derived from the split above, not from a projection.** Deduplicating 2,970 file instances to 1,189 while keeping all 542 directory instances should take the island to roughly **1.35–1.40 MB** (a ~1.9 MB saving) and the table to roughly **450 KB** (a ~626 KB saving), landing `code-map.html` near **1.9 MB**. Against Story 20.9's own "before" of 6,597,752 B that is a cumulative **−4.7 MB** — past the Story 20.4 spike's −3,493,000 B projection for this page. **State the real numbers, not these.** If the measured saving comes in materially below this, the membership encoding is probably the reason (Task 1.4) and it is worth saying so rather than reporting a pleasing number without a cause.

---

## Tasks / Subtasks

### Task 0 — Entry conditions (blocking)

- [ ] 0.1 `git status` before starting. Another session has been in this tree at the start of every Epic 20 story. **Never `git reset --hard`, `git checkout --`, or `git clean`** — this has already destroyed real work in this repo.
- [ ] 0.2 **Prove F2 on this tree, before designing the payload.** Run `CodeMap.BuildVariants` over this repo's file list (a throwaway test or a `dotnet run` scratch path) and diff each variant's directory node **ids and labels** against `full`'s. Record the actual divergences. D1's entire justification is this divergence; if it does not reproduce, stop and re-raise, because the cheaper union-tree design would then be available.
- [ ] 0.3 **Re-capture the § Measured starting state numbers on your own tree**: `dotnet run --project src/SpecScribe -- generate --deep-git`, then page bytes, island bytes, table bytes, per-variant sector and row counts. These are AC#4's "before".
- [ ] 0.4 Grep-verify every symbol and line reference in § Files being modified before relying on it (CLAUDE.md § Concurrent work).

### Task 1 — The shared-payload contract (AC: #1, #2) — D1, F2

- [ ] 1.1 **Model.** Extend `HierarchyExplorerModel` with an optional per-view dimension — recommended: a `Views` list of `(Key, Title, Window, Scaffold, Membership)` where `Scaffold` is that variant's **directory** `HierarchyNode`s verbatim from `CodeMapVariant.Map` and `Membership` maps each contained file to its parent directory *in that view*. Trailing and defaulted so every existing call site keeps compiling and the six 20.7 surfaces keep emitting byte-identical islands.
- [ ] 1.2 **Projector.** A shared-payload projector over `IReadOnlyList<CodeMapVariant>` replacing the per-variant `ProjectCodeMap(variant, …)` call. Walk each variant's `Map` through the existing `WalkCodeMap` — **one walk implementation, not a second** — emitting directory nodes into that view's scaffold and recording file membership. Build each **file node once**, from the `full` variant (it is the superset), through the unchanged `CodeMapFileNode`: same lifted metric bag keys, same units, same `Charts.BuildTreemapCard`, same Story 7.1 link guard (a null `fileHref` leaves a plain focusable node, never a broken link).
- [ ] 1.3 **The cap applies once** (F7): `Charts.SelectDetailedCodeMapFiles` over the distinct file set and its count. Keep `MaxDetailedCodeMapFiles` and `OrderBySignificance` exactly as they are — the chart and the table must still agree about which files are detailed.
- [ ] 1.4 **Membership encoding is a byte decision, so measure it.** Recommended: **integer indices** — files referenced by their index in the shared node array, directories by index in that view's scaffold — rather than repeating path strings, which would put ~2,970 long strings back into the payload. Whatever you choose, assert a **round-trip**: for every (view, file) pair the decoded parent equals the `parentId` the server's own per-variant projection produces.
- [ ] 1.5 **Island.** Emit the views alongside the nodes. Keep the two-shape gate in `IslandHtml` intact — the dimension-bearing branch's relaxed encoding plus `EscapeForScriptElement`, the non-dimension branch byte-identical — so the six 20.7 surfaces and the golden fingerprint do not move for a reason unrelated to this story. Re-assert `EscapeForScriptElement`'s hostile-path case (`</script><img src=x onerror=…>`), which now travels once instead of four times.
- [ ] 1.6 **`branchvalues` and the four 20.4 invariants are per-view properties now.** Assert them for **every** view, not just the default: exactly one root, no `null` in values, `parent == Σ children`, emitted `branchvalues == HierarchyExplorer.BranchValues`.

### Task 2 — The client view switch (AC: #2) — F1, F3

- [ ] 2.1 **Extend `visibleNodes()`; do not mint a second projection.** It already re-runs the children-win roll-up and re-plots through `Plotly.react` (F1). Add a view mode: select the active view's scaffold + its files, resolve each file's parent from the membership map, then run the **existing** roll-up loop unchanged.
- [ ] 2.2 **Generic by construction.** The component learns "this instance has N declared views and one is active"; it must not learn what a Code Map variant is. No surface name in `specscribe.js` — Story 20.9 Task 1.8's rule, and the drift ADR 0012 exists to end.
- [ ] 2.3 **The switch trigger is the existing checkbox pair.** They are real `<input>`s that already fire `change` and already carry a delegated listener. Declare the view→checkbox-state mapping in markup the way `data-hierarchy-reveal-when` already does (`cm-exclude-spec=0|1;cm-exclude-tests=0|1`) so the component reads a declaration rather than a surface rule.
- [ ] 2.4 **Drill scope survives a view switch honestly.** A drilled directory that does not exist in the newly-active view must reset to the top rather than leave Plotly pointing at a missing level — `applyFilter`'s existing `if (state.level && filterState && !next[state.level]) state.level = null;` is the precedent. Announce through `.ss-hierarchy-live`.
- [ ] 2.5 **Re-run `applyDimension()` on a view change** (F3, D4) so the ramp re-scales against the active view, and scope `resolveDimension()`'s `[min,max]` scan to the active view's nodes rather than the whole payload.
- [ ] 2.6 **Re-run Story 20.5's survival predicate after a view change**, not only after a dimension change: sectors > 0, `role="treeitem"` on every sector, non-empty `aria-label` on every sector, **exactly one `tabindex="0"`**.
- [ ] 2.7 **Deep links.** One `HashKey` now. `#cm-full=…` and its three siblings retire — say so in the Completion Notes, exactly as Story 20.9 retired `#dir=`. If a scope is deep-linked, the view it belongs to must be selected; the natural encoding is the view key in the fragment alongside the scope. Never force `display` directly — the checkbox state is the reader-visible "which filter is active" affordance and must stay in sync.

### Task 3 — Restructure the page (AC: #1, #3) — D2, F4

- [ ] 3.1 **One component instance**, rendered above the filter panels through `HierarchyExplorer.Render` as today. One `DomId`, one `HashKey`, `Shape: "treemap"`, `Mode: Navigate`, `TwinDisplay: External`, `Size: CodeMapExplorerSize` (640 — Story 20.9 left it unverified by eye; do not change it here, it is Open Question #3's business).
- [ ] 3.2 **Framed title and analysis window track the active view.** `VariantTitle` and the `ChartMeta.Window` string are per-variant today. Declare all four in the payload's view list; the component swaps them. Story 20.9's reasoning holds — a panel that does not say which filter it is, is worse than one that does.
- [ ] 3.3 **Both legends track the active view** (F4). Emit all four variants' ramp + discrete legend blocks server-side (small: swatches and short labels, no per-file data) and show the pair belonging to the active view × active dimension. **Never rebuild legend content in JS.** Reuse `AppendLegend` / `AppendDiscreteLegend` unchanged; only their framing and visibility change. Mind the `[hidden]` vs `display:flex` specificity trap.
- [ ] 3.4 **Keep the colorize `<select>`** and its seven options as the dimension picker, inside the component's hidden control bar, revealed on mount. Keep the `hasMetrics == false` path intact: file type is the only option, it is the baked default, and the "git data unavailable" note stays **outside** the legend bar because it is a fact about the data, not chrome for a chart.
- [ ] 3.5 **`hasMetrics` is now a whole-page property**, not a per-variant one. It is `files.Any(f => f.Metrics is not null)` per variant today. Compute it once over the distinct file set and check that no variant disagrees in a way that matters; if one can, say what you did.
- [ ] 3.6 **The honest empty state moves.** `variant.Map.IsEmpty` currently renders *"No files match this filter."* server-side per panel. With one instance it must appear when the **active view** is empty. Reuse the shipped `.ss-hierarchy-filter-empty` element (it exists for exactly this, with its own visible-message reasoning) rather than minting a second, and **also** keep a server-rendered, pure-CSS-toggled notice for the table so a JS-off visitor still sees it (AC#3). A missing panel is not an empty state (NFR8).

### Task 4 — Deduplicate the file table (AC: #1, #3) — D3

- [ ] 4.1 **One `AppendFileTable` call** over the distinct file set, ordered by `Charts.OrderBySignificance` as today — a subset of that ordering is the same relative order, so no view's reading order changes.
- [ ] 4.2 **Row marker classes** from the same predicates the variants are built from: `CodeMap.IsSpecDevPath` / `CodeMap.IsTestPath`, called once per file. **Do not re-implement either predicate** — they are `public static` and are the single place that filtering happens.
- [ ] 4.3 **Pure-CSS row hiding** using the same `#cm-exclude-spec:checked ~ …` sibling-combinator idiom the panel toggle uses. The checkboxes must remain plain unwrapped siblings at the same nesting level as the thing they control — the comment at `CodeMapTemplater.BuildPage` explains why nothing may become a common ancestor of both. **Verify with JS genuinely off**, not by reasoning: this is D2's guarantee and AC#3's gate.
- [ ] 4.4 **Per-view header, lead text and counts.** "Every file in the treemap" / "The N most significant files" and the row counts are per-variant strings. Emit all four, toggled by the same CSS. Cheap, and it keeps them correct with JS off.
- [ ] 4.5 **`initCodemapTablePager` must page over VISIBLE rows.** It counts rows against `data-page-size` today; with hidden rows in the DOM it would page over a set the reader cannot see. Fix it, and keep its progressive-enhancement contract: every row always renders in the markup, in order, as the complete no-JS truth.
- [ ] 4.6 **The truncation row** ("+N more files not shown") is computed once now (F7). Keep it, keep its `colspan` correct for both `hasMetrics` shapes.
- [ ] 4.7 **`AppendCodeMapTablePager` markup itself stays as-is.** It is emitted `hidden` and sits after the table so a no-JS visitor never sees inert controls.

### Task 5 — Propose the ADR (CLAUDE.md § Decision records)

- [ ] 5.1 This grows the component a **new cross-cutting contract**: one instance may present N server-declared *views* over one payload, with the server retaining sole authority over tree structure per view. That is a shared-architecture change, so **propose an ADR without being asked** — either a new ADR or an amendment to ADR 0012 §2. Include D1's reasoning (why the scaffolding stays server-side) and D4's (why normalization stays per-view), because both are the kind of decision a later reader will otherwise try to "simplify".
- [ ] 5.2 If it lands as an amendment, keep ADR 0012's existing §2 wording intact and add rather than rewrite; cite it **by symbol, not line number** (project memory: ADR refs have drifted within a day).

### Task 6 — Retire what genuinely dies, prove it by search (AC: #1) — F5, F6

- [ ] 6.1 The four `.codemap-view` wrappers, `data-view`, `data-hierarchy-reveal-when`, `VariantTitle`'s per-panel emission site and the `.codemap-view` display rules in `specscribe.css` — retire whatever the new structure genuinely no longer emits. **Keep the two `.codemap-filter-checkbox` inputs and their label styling**; they are now the table filter and the view switch.
- [ ] 6.2 **`revealPanelsNamedByHash()` and `data-hierarchy-reveal-when`** lose their only consumer (F5) — delete, with their tests. **Keep** `deferHierarchyMount` / `flushHierarchyReveals` / the zero-width guard and say why in the Completion Notes: it is the component's general answer to being mounted inside a hidden container, it costs nothing idle, and Story 20.9 paid for it with a live-caught defect.
- [ ] 6.3 **`CodeMapVariant.Layout`** (F6) — verify by search that no production code reads it, then drop the record slot and the `map.Layout()` call from `BuildVariants`. Keep `CodeMap.Layout()` the method if tests still need it. Fix the stale `SiteGeneratorAdapterTests` comment naming the non-existent `Charts.CodeFreshnessTreemap`. Report the generation-time delta.
- [ ] 6.4 **Prove absence by search, not by "it compiled"** (CLAUDE.md § Concurrent work — a `Charts.cs` edit has silently vanished in this repo, and F8's class-name assertions compile fine while being wrong). Grep every deleted symbol and CSS class across `src/`, `tests/` and the extension shim; record the searches and their zero results in the Debug Log.

### Task 7 — Hosts and parity (AC: #1, #3)

- [ ] 7.1 **Webview.** `WebviewRenderAdapter.StripDataIslands` removes the island — including on the `WriteOutput`-synthesized captured-page path Story 20.9 fixed. Confirm the strip still finds the (now single, larger-per-node but far smaller overall) island, and that the webview reaches the **table**, with its file links resolving under the webview's path rewriting. The `hierarchy-chart` host exception already covers this surface; an unregistered divergence is a bug.
- [ ] 7.2 **Webview/SPA and the pure-CSS filter.** The row-hiding rules live in the shipped stylesheet; confirm what the webview and SPA actually do with them. **Completeness is the contract** — every row present — and the filter is an enhancement on top, so a host where the checkboxes do nothing is acceptable if and only if all rows are visible. Verify which it is rather than assuming.
- [ ] 7.3 **SPA:** island and table must survive content capture, and `specscribe:content-swapped` must re-init the **one** remaining Code Map instance. Extend the existing `SiteGeneratorSpaTests` island-survives-capture test rather than adding a parallel one. Story 6.6 measured `code-map.html` at **82.5 MB** at SPA scale — report whether this conversion moves it.
- [ ] 7.4 `RenderParity` / `RenderSectionParityTests` green across `html`, `spa`, `webview`.
- [ ] 7.5 `AssetManifest.HierarchyEngineNeeded` still resolves through `HierarchyExplorer.ContainsHost` on the built body — one host instead of four, so the gate must still find it. `SiteGeneratorSpaTests.HierarchyEngineBundle_ShipsOnlyWhereAHierarchyChartWasRendered` is the guard.

### Task 8 — Tests (AC: #1, #2, #3, #4)

- [ ] 8.1 **Serialization uniqueness (AC#1):** for a fixture where a file appears in all four variants, assert its `path`, its metric bag, its hover card and its table row each appear **exactly once** in the rendered page. This is the story's central assertion — make it the one that fails loudest.
- [ ] 8.2 **Per-view structural equivalence (AC#2):** for each of the four views, the decoded node set + parent map + rolled-up values must equal what the per-variant server projection produces from the same `CodeMapVariant`. **Assert on ids AND labels** — F2's `.github` case is precisely a label-and-parent divergence that an id-only comparison would miss.
- [ ] 8.3 **The four 20.4 invariants per view** (1.6), not just for the default view.
- [ ] 8.4 **Colour neutrality (AC#2, D4):** per view × per dimension, the resolved class list equals what Story 20.9's shipped per-panel payload produced for the same input. Include the five `fill-opacity` states and the three `stroke-dasharray` states explicitly — they are the ones that regress silently.
- [ ] 8.5 **Twin completeness per view (AC#3):** every file the chart draws for a given view has a table row with a resolving href — a **set** match, not a count match (Story 20.6 Task 1.3b). Retarget the existing `RenderPage_FileTableIsASetMatchAgainstTheChartPayload_NotJustACountMatch` at each of the four views.
- [ ] 8.6 **Pure-CSS filter correctness:** assert the emitted row marker classes match `IsSpecDevPath`/`IsTestPath` for every file, and that the stylesheet carries a rule for each of the four checkbox combinations (the `StylesheetTests` CSS-guard pattern, using its comment-stripping reader so an explanatory comment cannot satisfy a guard).
- [ ] 8.7 **The cap, shared** (F7): the analogue of `ProjectCodeMap_AboveTheDetailCap_LongTailKeepsGeometryButLosesTheCard` over the shared payload — every file still gets a node with real geometry in every view it belongs to, only the top-`cap` most significant keep `TipHtml`, and the chart and table agree on which.
- [ ] 8.8 **Membership round-trip** (1.4) and **`EscapeForScriptElement`** safety including the hostile path.
- [ ] 8.9 **Honest empty states survive:** an empty active view → "No files match this filter." with JS off; `codeMap.IsEmpty` → the page's own empty state; `hasMetrics == false` → file type as the only dimension plus the git-unavailable note.
- [ ] 8.10 **Keep `HierarchyRolloutTests`'s empty allowlist green** — it is Epic 20 AC#2's assertion and nothing here may disturb it.
- [ ] 8.11 **Work through F8's assertion surface.** Rewrite fact-asserting tests against the new shape; delete structure-asserting ones. **Report the split and what coverage genuinely went away.**
- [ ] 8.12 **Do not unit-test the JS** — there is no JS harness in this repo. Task 9 is the verification for Task 2's client behaviour. Say so plainly rather than implying coverage that does not exist.

### Task 9 — Live-browser verification and the accounting (AC: #2, #3, #4)

- [ ] 9.1 Generate to `SpecScribeOutput/` **with `--deep-git`** — without it the Code Map falls back to file-type-only and six of the seven dimensions are untestable. Never `--output docs/live`. Serve via a `.claude/launch.json` entry; **pick an unused port** (8099/8104/8105 and several others are taken, and Story 20.9's review already had to fix a port collision between two concurrent sessions).
- [ ] 9.2 **JS on, all four checkbox combinations, recorded separately:** the chart re-lays-out, sector counts match the per-variant counts in § Measured starting state, the framed title/window and both legends track the view, the drill breadcrumb behaves, zero console errors, and Story 20.5's survival predicate holds after **every** switch.
- [ ] 9.3 **All seven dimensions × at least two views:** fills change, the ramp re-scales to the active view (D4), the legend's swatch ranges move with it, accessible names carry the dimension suffix, and the live region announces.
- [ ] 9.4 **Compare against Story 20.9's output, not against plausibility.** Keep a pre-change generated copy and diff the resolved fills and sector counts per view. AC#2 says *unchanged*, and "looks right" is not that.
- [ ] 9.5 **JS genuinely off** (scripts stripped from a served copy, not assumed): the table filters correctly on all four combinations, every row's link resolves, the empty state renders where a view is empty, no inert controls are visible, and no chart-sized blank box. **This is the ADR 0013 §3 gate and D2's justification being exercised.**
- [ ] 9.6 **Colorway audit** built at runtime from the shipped cascade — zero foreign colours, text fills included, `fill-opacity` included. Re-derive the allowlist from the shipped CSS, never from a typed token value.
- [ ] 9.7 **Take screenshots.** The owner has never seen a pixel of this component — Stories 20.4, 20.5, 20.7 and 20.9 all owed one and none delivered. Try hard; if the pane still refuses to composite, say so explicitly and fall back to computed-geometry evidence rather than skipping it quietly a fifth time.
- [ ] 9.8 **The byte accounting (AC#4).** Report before / after / delta for `code-map.html`, split into **island** and **file table**, with the duplication saving isolated from everything else. State B/node against 195.4 (spike), ~390 (20.5) and 936 (20.9). Say whether the spike's −3,493,000 B projection for this page is now met, and by how much.
- [ ] 9.9 **Golden fingerprint.** It should not move: the fixture is not a git repo and cites no real files, so `code-map.html` does not render in it (Story 20.6 Task 4.1, confirmed by 20.9). **If it moves, investigate rather than re-baselining** — the likely cause would be an accidental change to the non-dimension island branch, which would mean six other surfaces moved too. If you do regenerate, confirm stable across two repeated runs and **name whose concurrent changes it sits on top of**.
- [ ] 9.10 Full suite, real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.

---

## Dev Notes

### Why this story exists, in one paragraph

Story 20.9 converted the Code Map to the component and measured the result: `code-map.html` came in at **57% of the Story 20.4 spike's projected saving**, and the investigation that followed found why. The page serializes **3,512 chart nodes and 2,970 table rows against 1,421 distinct nodes and 1,189 distinct files** — a 2.47× duplication factor, because four filter variants each independently serialize their own subset of the same codebase. That is not information; it is the cost of a 2021 decision (pre-render all four combinations so the toggle needs no JavaScript) meeting a 2026 one (the chart is now a client-side Plotly instance that requires JavaScript anyway). The no-JS guarantee that decision bought is real and is kept — but it belongs to the **file table**, which is this surface's ADR 0013 twin, and a table's rows can be hidden with the same CSS that hides a whole panel. This story keeps the guarantee and deletes the duplication.

### Architecture compliance

- **ADR 0012 §2** — one component is the only route to a hierarchy chart. This story adds a capability to it; it must not add a Code-Map-shaped branch inside it. Task 5 proposes the ADR that records the new contract.
- **ADR 0012 §6** — presentation is SpecScribe's tokens, never Plotly's colorways. No colour value may be typed in JS (AD-7); every fill still resolves through the shipped cascade via `tokenFor`'s class-list probe.
- **ADR 0012 §7 / ADR 0010 §3** — data computed once at generation time and embedded. A view switch is a pure re-read of an already-embedded payload. **No fetch, no live git, no wall-clock `now`.**
- **ADR 0013 §2/§3** — the twin's completeness contract and the hard per-surface **live** JS-off gate. The twin here is the file table, and its per-view completeness is AC#3.
- **ADR 0013 §5** — the IR carries chart data **plus component configuration**; the view list is configuration and belongs in the island.
- **ADR 0002 / AD-2** — payload and config are host-neutral view-model data, built in the emitter and routed through the templater. Never ad-hoc string-building in an adapter.
- **NFR-5 as amended by ADR 0013** — JS-off may lose the visualization; it must never lose **information** or **navigation**. On this page that also means the pure-CSS filter keeps working (D2 of Story 20.9).
- **NFR-3** — offline / `file://`-capable: no CDN, no fetch, no external origin.
- **NFR8** — a missing panel is not an empty state. An empty view says so.
- **UX-DR17 / UX-DR19 / UX-DR21** — never colour-only; every metric keeps its non-colour text equivalent across all seven dimensions; one primary representation with alternates behind the standard toggle.
- **FR31** — generation-time determinism: identical output on a from-scratch regen.
- **FR-10 / ADR 0010 §4** — descriptive attribution, never a ranking. Unaffected by serialization, and it stays that way.
- **Story 7.1 link guard** — a `fileHref` resolver returning null leaves a plain, focusable node. Never a broken link.

### Anti-patterns to prevent

1. **Writing a second client-side projection path** instead of extending `visibleNodes()`. It already does the roll-up and the re-plot (F1). A second one is how two views start disagreeing about a parent's value.
2. **Porting `CodeMap.BuildDir`'s collapse rule to JavaScript.** D1 exists specifically so that a structural rule stays in exactly one language.
3. **Assuming a filtered variant's node set is a subset of `full`'s.** For directories it is not (F2), and an id-only comparison will not catch it.
4. **Letting `resolveDimension()` keep scanning the whole payload.** It silently becomes the rejected D4 option (F3).
5. **Rebuilding legend content in JS.** `CodeMapChangeLevelRange`'s arithmetic and `IsCodeMapChangeLevelUnreachable`'s omission rule stay server-side (F4).
6. **Turning the table filter into a JS filter.** Owner decision D2 of Story 20.9 is about the table, and D3 keeps it pure CSS by construction.
7. **Paging the table over rows the reader cannot see** (4.5).
8. **A `if (surface === "codemap")` branch inside the shared component.** Story 20.9 Task 1.8's rule; the drift this epic exists to end.
9. **Sweeping up the zero-width deferral guard** with the hash-reveal path (F5). One is dead, the other is a general capability paid for by a live-caught defect.
10. **Deleting F8's class-name assertions as obsolete** because they no longer match. Many assert a fact that survives into the new shape.
11. **Changing the hover cards** while measuring the duplication saving. AC#4 requires the two to be isolated.
12. **Moving the golden fingerprint via the non-dimension island branch.** If it moves, six unrelated surfaces moved (9.9).
13. **Reporting a single pleasing byte number** without the island / table split. This story is the second half of the spike's reckoning.
14. **Proving a symbol is gone by "the build passed."** Grep, and record the searches.
15. **`git reset --hard` / `git checkout --` / `git clean`.** This has already destroyed real work mid-story in this repo.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `visibleNodes()` + its roll-up loop | `specscribe.js`, the config-gated node-filter block | already re-projects an embedded payload and re-runs children-win; **extend it** |
| `HierarchyExplorer.Reparent` / `RollUp` / `RollUpParentValues` | `HierarchyExplorer.cs` | the server-side children-win rule the client mirrors; every projector ends here |
| `HierarchyExplorer.BranchValues` | `HierarchyExplorer.cs` | assert against the constant, never a literal `"total"` |
| `HierarchyExplorer.WalkCodeMap` | `HierarchyExplorer.Projectors.cs` | the ONE depth-first walk, directories before contents — parent-before-child order is what the roll-up, the client filter and the twin all rely on |
| `HierarchyExplorer.CodeMapFileNode` / `CodeMapDimensions` | `HierarchyExplorer.Projectors.cs` | the lifted metric bag and the seven dimension declarations — unchanged by this story |
| `HierarchyExplorer.IslandHtml`'s two-shape gate | `HierarchyExplorer.cs` | dimension-bearing branch relaxed-encoded, non-dimension branch byte-identical |
| `HierarchyExplorer.EscapeForScriptElement` | `HierarchyExplorer.cs` | the only two sequences that can re-frame a `<script type="application/json">` |
| `HierarchyExplorer.ShortLabelFor` | `HierarchyExplorer.cs` | `uniformtext` draws every label at ONE size — a long path suppresses labels chart-wide |
| `CodeMap.IsSpecDevPath` / `IsTestPath` | `CodeMap.cs` | the single place variant filtering happens; the row marker classes call these |
| `Charts.SelectDetailedCodeMapFiles` / `MaxDetailedCodeMapFiles` / `OrderBySignificance` | `Charts.cs` | the per-node detail cap and its ordering; `null` is the "no cap" sentinel |
| `Charts.ComputeMaxChanges` / `CodeMapChangeLevelRange` / `IsCodeMapChangeLevelUnreachable` | `Charts.cs` | the ramp legend's real ranges and its omission rule — server-side, per view |
| `Charts.BuildTreemapCard` | `Charts.cs` | the rich hover card; one tooltip vocabulary for chart, table and twin |
| `Charts.Bucket` ↔ `specscribe.js`'s `bucket()` | both | mirrored by hand and asserted; do not touch either |
| `.ss-tooltip` + `data-tip-html` + `hoverinfo:"none"` | Story 20.5's tooltip decision | one tooltip system site-wide |
| `.ss-hierarchy-filter-empty` | `HierarchyExplorer.Render` (behind `cfg.Filterable`) | the visible "you filtered everything out" message — reuse, don't mint a second |
| `specscribe:content-swapped` | `specscribe-spa.js` | one instance depends on it now |
| `AssetManifest.HierarchyEngineNeeded` + `HierarchyExplorer.ContainsHost` | 20.5's asset seam | **disk is the truth**, not the in-memory copied flag |
| `HostRenderExceptions.Registry` | `HostRenderException.cs` | the ONLY legitimate way a surface diverges |

### Files being modified — current state

*Line references verified 2026-07-28 against `8a2fb83`. **Verify each again before relying on it** — another session may be editing this tree.*

- **`src/SpecScribe/HierarchyExplorer.cs` (1,022 lines) — UPDATE.** `HierarchyNode` `:70`; `HierarchyDimension` `:187`; `HierarchyTwinDisplay` `:207`; `HierarchyExplorerConfig` `:265`; `HierarchyExplorerModel` `:281`; `BranchValues` `:321`; `Reparent` `:417`; `RollUp` `:454`; `Render` `:545`; `CompactIslandJson` `:696`; `EscapeForScriptElement` `:722`; `IslandHtml` `:726` (the two-shape gate at `:775`); `TextTwinHtml` `:859`; `ContainsHost` `:981`. The view list lands on the model and in the island.
- **`src/SpecScribe/HierarchyExplorer.Projectors.cs` (765 lines) — UPDATE.** `ProjectCodeMap` `:449` becomes (or gains) a shared-payload form; `CodeMapDirNode` `:482`, `CodeMapFileNode` `:486` unchanged in substance; `WalkCodeMap` `:618` is the one walk. `ProjectOwnership` `:540` is **not touched**.
- **`src/SpecScribe/CodeMapTemplater.cs` (418 lines) — UPDATE, the main rewrite.** `BuildPage` `:39`; `AppendFilterCheckbox` `:111` (keep the inputs, drop `data-hierarchy-reveal-when`'s consumer); `CodeMapExplorerSize` `:122`; `AppendVariantPanel` `:141` collapses into one instance + per-view declarations; `VariantTitle` `:216` becomes a per-view string in the payload; `AppendColorizeControls` `:233` kept; `AppendLegend` `:272` / `AppendDiscreteLegend` `:298` kept and emitted per view; `AppendFileTable` `:322` deduplicated (D3); `CodeMapTablePageSize` `:401`; `AppendCodeMapTablePager` `:410` markup unchanged.
- **`src/SpecScribe/CodeMap.cs` (582 lines) — UPDATE (small).** `CodeMapVariant` `:117` loses its `Layout` slot (F6); `IsTestPath` / `SpecDevPathPrefixes` `:180` / `IsSpecDevPath` `:189` **unchanged and reused**; `Build` `:226`; `BuildVariants` `:295` drops the `Layout()` call; `BuildDir` `:358` is the collapse rule F2 is about — **read it, do not change it**; `Files()` `:382`.
- **`src/SpecScribe/assets/specscribe.js` (2,458 lines) — UPDATE.** `hierarchyMounts` `:980`; the zero-width guard `:1013`; `hierarchyPending` / `deferHierarchyMount` / `flushHierarchyReveals` `:1062-1099` (**keep**); `revealPanelsNamedByHash` `:1111` (**delete**, F5); `initHierarchyExplorer` `:1145`; `bucket()` `:1290`; `classifyNode` `:1343`; `resolveDimension` `:1410` (F3); `filterState` / `visibleNodes` `:1558-1600` (**the seam to extend**); `buildTrace` `:1602`; `scopeFromHash` `:2003`; `redraw` `:2036`; the filter control block `:2224-2255`; the boot call `:2319`.
- **`src/SpecScribe/assets/specscribe.css` — UPDATE.** `.codemap-filter-checkbox` note `:1926`; the filter-checkbox block `:4328-4331`; `.codemap-view { display: none }` `:4347` and the four sibling-combinator reveal rules `:4348-4351` — **these are the idiom Task 4.3 reuses for table rows**; keep the legend swatch rules.
- **`src/SpecScribe/SiteGenerator.cs` — READ.** `EnumerateCodeFiles` `:5116` (no extension filter — F2's `.agent.md` / `.yml` point), `_codeFiles` `:159`, `WriteCodeMap`'s `EnsureHierarchyEngine` call.
- **`src/SpecScribe/WebviewRenderAdapter.cs` / `HostRenderException.cs` — READ**, then confirm the existing `hierarchy-chart` entry still covers this surface (7.1).
- **Tests — UPDATE, heavily.** `CodeMapTemplaterTests.cs` (373), `SiteGeneratorCodeMapTests.cs` (450), `HierarchyColorizeTests.cs` (560), `HierarchyExplorerTests.cs` (831), `CodeMapTests.cs` (514), `StylesheetTests.cs`, `SiteGeneratorSpaTests.cs`, `SiteGeneratorWebviewTests.cs`, `RenderParityTests` / `RenderSectionParityTests`. **`HierarchyRolloutTests.cs` (525) — do not disturb its empty allowlist.**

### Project Structure Notes

No new page, no new nav entry, no new dependency, no new asset. Net **subtraction** in `CodeMapTemplater.cs` (four panels → one chart + one table), in `specscribe.js` (the hash-reveal path), and in the emitted page (~2.5 MB). Net addition of a view concept on the model/config and a membership encoding. `HierarchyExplorer.Projectors.cs` already exists as the partial for per-surface projectors — the shared-payload Code Map projector belongs there, not in `HierarchyExplorer.cs`.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; JS is verified in a live browser and its *content* asserted by string tests over the shipped asset (`StylesheetTests` is the established pattern for both CSS and JS guards, and its comment-stripping reader exists because two absence guards were once satisfied by an explanatory comment). **The golden fingerprint is not this story's regression net** — the fixture is not a git repo, so `code-map.html` does not render in it. The net is the templater / projector tests plus Task 9's live verification. Say so plainly rather than implying coverage that does not exist.

### Previous story intelligence

**Story 20.9 (`done`, code-reviewed)** — this story's direct parent, and the source of every measured number here. Five things land directly: (a) its **D2** keeps the pure-CSS toggle, and this story must keep it for the table; (b) its **F1** is why the reveal machinery exists and why F5 must not sweep all of it away; (c) its **Task 7.7** is the byte accounting this story reports against, including the `System.Text.Json` relaxed-encoding change and its two-shape island gate; (d) its **first reveal-hook implementation measured the wrong element and broke every chart on the site while the full suite stayed green** — the class of defect that only looking at the rendered page catches; (e) its review added `data-hierarchy-reveal-when` + `revealPanelsNamedByHash` for a deep-link case that **this story's single instance dissolves**.

**Story 20.7 (`done`)** — built the class-list colour resolver and the client node filter, and its F3 committed both to *two consumers*. Story 20.9 was the second; **you are the third**, which is the moment a "designed for two consumers" contract either generalizes cleanly or reveals that it did not. Its two deferred robustness findings are relevant here: `HierarchyDimensionKind` is an unvalidated string that silently no-ops, and `HierarchyDimension.Key` uniqueness within one list is unenforced. A view-key collision would be the same latent class — enforce it in the emitter.

**Story 20.6 (`done`)** — **D1** keeps Code Map's per-variant file table as this surface's twin *because it is richer*, and **F3** confirmed it is genuinely server-complete (every file ships as a plain `<tr>`; the pager is not truncation). Both survive D3: deduplicating rows changes neither claim. Its **Task 4.1** records that the golden fixture never renders this page.

**Story 20.5 (`done`)** — the component. Four facts land: `uniformtext` draws every label at one size, so `ShortLabelFor` is load-bearing on deep file paths; CSS cannot stroke a Plotly sector (the ring rides `marker.line`); the island costs roughly **double** the spike's per-node figure on planning nodes and far more here; and `EnsureHierarchyEngine` treats **disk** as truth, not the in-memory copied flag.

**Story 20.4 (`done`)** — plotly.js **3.7.0** pinned; the four payload invariants; **CSP violations do not appear in console captures** (a test that greps the console passes while the chart is blank — ask the DOM); promises resolve off an animation frame, so hang everything on `plotly_afterplot`; `marker.pattern` needs an explicit per-sector `bgcolor`; and the **−4,787,124 B** portal delta is *amortised*, which is why this page keeps having to settle it.

**Story 7.12 (`done`)** — the owner-directed merge that made Code Map ONE panel with "what to view" (colorize) and "how to view it" (shape) as orthogonal axes, after they felt "artificially split across different surfaces". D2 of this story adds a third axis — "which files" — and the same reasoning applies: it belongs on the same surface, not in four surfaces.

**Story 7.6 (`done`)** — wrote the four-variant pre-render and its justification: *"rather than relaying out the treemap client side — which would need a second, JS-ported copy of the squarified algorithm and risks the two implementations silently diverging."* **That reasoning was correct and is now obsolete**: Plotly owns the tiling, so there is no algorithm to port (F1). The part of it that survives is the no-JS filter, which D3 keeps. Update that doc comment rather than leaving it asserting a superseded rationale.

**Story 6.6 (`done`)** — recorded `code-map.html` at **82.5 MB** at SPA scale as a perf defect. Task 7.3 should report whether this conversion moves it.

**Owner workflow (`CLAUDE.md`)** — the post-implementation round where the owner verifies rendered behaviour is the **designed gate**, not rework. The retired per-variant deep links and the collapsed panel structure will both draw commentary; leave both easy to adjust.

### Git intelligence summary

Baseline `8a2fb83` ("Addressed build failure"), on top of four batch commits from 2026-07-28 (`82880ba` afternoon, `755bd7a` lunch, `b696485` morning, `06b300c` overnight). That cadence is structural in this repo — code review runs at epic end, so a single commit routinely carries several stories' work. **Scope any later review of this story by its own File List and declared symbols, never by a commit range** (CLAUDE.md § Scoping a code review), and state the exclusion in the review record.

The working tree at drafting carried two unrelated modifications (`README.md`, `AboutSddTemplater.cs`) belonging to another session. Do not touch them, and do not clean them.

### Latest technical information

**Nothing needs re-researching, and that is the instruction.** plotly.js is pinned at **3.7.0** (MIT, released 2026-07-03); Story 20.5 checked for newer on 2026-07-25 and found none, and Story 20.9 re-affirmed the pin. **A version bump invalidates every measured number this story reports against and must be its own decision, not a side effect.**

Three 3.7.0 facts stay load-bearing: `displayModeBar: false` is a **privacy** requirement, not a cosmetic default (3.7.0's `sendDataToCloud` button uploads the chart to Plotly Cloud); `plotlyServerURL: ''` / `topojsonURL: ''` keep the portal offline-capable (NFR-3); and **`Plotly.react`** re-lays-out a trace from new `ids`/`parents`/`values` without animating — which is precisely what a view switch is. Keep the shipped privacy guard green.

The vendoring tool (`tools/plotly-vendor/`) is not touched: no new trace family is needed.

### ⚠️ Concurrent work — read before you start

Per CLAUDE.md § Concurrent work on shared `main`:

- **Grep-verify every symbol and line reference before relying on it.** A `Charts.cs` edit has silently vanished in this tree, and `RelatedWorkCards.cs` changed between two reads during Story 20.8's drafting.
- **Verify after every edit** — do not trust that a write landed because the tool returned success and the build passed.
- **Expect the build to be transiently broken by someone else's rename.** Wait; do not reset.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**
- **Pick an unused `.claude/launch.json` port.** Story 20.9's review had to fix a collision between two concurrent sessions on 8114.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.10] — the draft ACs this story refines, the seating comment, and the byte investigation carried forward from Story 20.9's review
- [Source: `_bmad-output/implementation-artifacts/20-9-colorized-hierarchies-code-map-and-ownership.md`] — D1 the dimension contract, **D2 the pure-CSS filter**, F1 the reveal hook, F7 the class-name-assertion trap, Task 7.7's byte accounting and its per-variant sector/row counts, and the review finding that seated this story
- [Source: `_bmad-output/implementation-artifacts/20-6-text-twin-audit-and-fingerprint-replacement.md`] — D1 (the file table is the twin), F3 (it is server-complete), Task 4.1 (the fixture renders neither page)
- [Source: `_bmad-output/implementation-artifacts/20-7-site-wide-hierarchy-rollout.md`] — F3 (the resolver and the node filter, "designed for two consumers"), D2 (selector ordering), D3 (the webview twin degradation)
- [Source: `_bmad-output/implementation-artifacts/20-4-spike-report.md`] — the four payload invariants, the pinned engine, and the amortised byte projection this story finishes settling
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §2 the component contract, §3 the mode contract, §6 tokens not colorways, §7 generation-time determinism
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 the twin contract, §3 the hard per-surface live JS-off gate, §5 the IR carries data + configuration
- [Source: `docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md`] — §3 embedded generation-time data, §4 the no-ranking rule
- [Source: `CLAUDE.md`] — § Concurrent work on shared `main`, § Verification (live browser), § Scoping a code review, § Decision records
- Code: `HierarchyExplorer.cs:70,187,207,265,281,321,417,454,545,696,722,726,775,859,981`, `HierarchyExplorer.Projectors.cs:449,482,486,540,618`, `CodeMapTemplater.cs:39,111,122,141,216,233,272,298,322,401,410`, `CodeMap.cs:117,180,189,226,295,358,382`, `Charts.cs:2458,2479`, `specscribe.js:980,1013,1062,1111,1145,1290,1343,1410,1558,1602,2003,2036,2224,2319`, `specscribe.css:1926,4328,4347,4348`, `SiteGenerator.cs:159,5116`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Do the per-variant deep links need a migration note?** Recommended: **no, retire them and say so** — the same call Story 20.9 made for `#dir=`. `#cm-full=` and its three siblings shipped on 2026-07-27 and were never documented as stable. The single `HashKey` should still encode the view alongside the scope so a shared link lands on the right filter.
2. **Should the chart panel sit above or below the filter checkboxes?** Recommended: **checkboxes first, then the chart, then the table** — the checkboxes now govern both, so they read as page-level controls rather than as something belonging to one panel. Worth a look in the verify round; it is a markup-order change, not a redesign.
3. **Is 640 still the right `Size`?** Recommended: **leave it here.** Story 20.9 set it and could not verify it by eye (no screenshot, four stories running). Changing it inside a serialization story would confound the visual comparison AC#2 depends on. It stays Story 20.9's Open Question #3.
4. **Should the view switch also drive the SPA/webview?** Recommended: **no** — those hosts get the complete table, and the filter is an enhancement. Stated so the answer is on the record rather than rediscovered (7.2).
5. **Does anything else on the site want server-declared views over one payload?** Recommended: **do not generalize speculatively.** Build it for one consumer, name the contract in Task 5's ADR, and let a second consumer prove the shape — the same discipline that made Story 20.7's two-consumer resolver hold up when Story 20.9 arrived.

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

---

## Change Log

- 2026-07-28 — Story 20.10 drafted (create-story). Context assembled from Story 20.9's full record (its owner decisions, its eight findings, its code review and its Task 7.7 byte accounting), Stories 20.4/20.5/20.6/20.7/7.6/7.12/6.6, ADR 0010/0012/0013, and **a code-level read of the shipped component at `8a2fb83`** — `HierarchyExplorer.cs`, `HierarchyExplorer.Projectors.cs`, `CodeMapTemplater.cs`, `CodeMap.cs` and the hierarchy block in `specscribe.js`. Two of the epic's own premises were found wrong against that code and are corrected in `epics.md` in the same change: **(F1)** the "client-side re-layout capability that doesn't exist yet" largely DOES — `visibleNodes()` already re-projects an embedded payload, re-runs the children-win roll-up and re-plots through `Plotly.react`, which does the area allocation; only the filter's *granularity* (root-children vs scattered leaves) and per-view scaffolding selection are missing. **(F2)** conversely, a hazard the epic does not mention: `CodeMap.BuildDir`'s single-child directory-chain collapse is **variant-dependent**, proven on this repo's own `.github` (two subdirs in `full`, collapsed to one node with a different id, label and parent in `no-spec`), so a filtered variant's directory node set is NOT a subset of `full`'s and a file's `parentId` is a property of (file, view) rather than of the file. Four owner decisions elicited and locked: **D1** share the LEAVES and keep each variant's directory scaffolding server-emitted, which dissolves F2 without porting a structural rule into JavaScript and costs only 542 metric-free directory instances against 2,970 file instances; **D2** ONE component instance above the panels rather than four over a shared island, accepting that the framed title, analysis window and both legends must track the active view; **D3** the four file tables are deduplicated too — one table, `is-spec`/`is-test` row classes, hidden by the SAME pure-CSS sibling-combinator idiom, so owner decision D2 of Story 20.9 is preserved by construction and another ~626 KB comes off the page; **D4** ramp normalization stays PER-VIEW and the legend's real change-count ranges move with it, preserving today's colours exactly so the conversion is provably colour-neutral rather than merely plausible. Six further findings promoted to a read-first section: **F3** `resolveDimension()` scans the whole payload by design and would silently become D4's rejected option; **F4** both legends are per-variant server-baked and one omits unreachable levels entirely; **F5** the Story 20.9 reveal machinery loses its only consumer, and the hash-reveal path dies while the zero-width deferral guard should live; **F6** `CodeMapVariant.Layout` is ALREADY dead in production yet still computes four squarified layouts per generation; **F7** the hover-card cap becomes one decision instead of four, a large-repo behaviour change invisible at this repo's 1,189 files; **F8** the test surface that asserts four panels, and the reminder that class-name assertions fail at runtime rather than at compile time. Measured starting state carried forward from 20.9 (`code-map.html` 4,451,207 B; island 3,288,932 B with 42% hover cards; tables 1,076,146 B; 3,512 sectors and 2,970 rows against 1,421 and 1,189 distinct; 936 B/node) with an explicit instruction to re-capture, plus a derived estimate — island → ~1.35–1.40 MB, table → ~450 KB, page → ~1.9 MB — stated as an estimate to be measured against rather than reported. Task 5 proposes the ADR the new one-payload/N-server-declared-views contract requires (CLAUDE.md § Decision records). baseline_commit `8a2fb83`.
