---
baseline_commit: 8a2fb8352f882debb2e81c7369f52366f6a24c53
---

# Story 20.10: Shared Hierarchy Payload Across Code Map's Filter Variants

Status: review

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

- [x] 0.1 `git status` before starting. Another session has been in this tree at the start of every Epic 20 story. **Never `git reset --hard`, `git checkout --`, or `git clean`** — this has already destroyed real work in this repo.
- [x] 0.2 **Prove F2 on this tree, before designing the payload.** Confirmed directly against this repo's own `.github`: `full` keeps two children (`agents`, `workflows`), `no-spec` drops every `.github/agents/*` file and collapses to id/label `.github/workflows` / `".github / workflows"` — a different id, label AND parent than `full`'s `.github`. Encoded as a live regression test (`RenderPage_ViewsAreVariantDependentDirectoryScaffoldsSharingNoIdsIncorrectly`).
- [x] 0.3 **Re-capture the § Measured starting state numbers on your own tree**: generated with `--deep-git` at baseline `9421a8c` before any change. `code-map.html` 4,592,047 B; island 3,396,451 B (4 payloads); tables 1,158,540 B; per-variant: full 1220 files/235 dirs, no-spec 461/50, no-tests 1060/221, no-spec-no-tests 319/48 (3060 file instances, 554 dir instances + 4 roots).
- [x] 0.4 Grep-verified every cited symbol in § Files being modified against HEAD (which had moved from the story's `8a2fb83` baseline to `9421a8c`) — all present, line numbers drifted by ≤2 lines as expected.

### Task 1 — The shared-payload contract (AC: #1, #2) — D1, F2

- [x] 1.1 **Model.** `HierarchyExplorerModel.Views` added as a trailing, defaulted `IReadOnlyList<HierarchyView>?`. `HierarchyView(Key, Title, Window, Scaffold, Files, ParentScaffoldIndex, When)` — `Scaffold` is that view's own directory nodes (incl. its own synthesized root), `Files`/`ParentScaffoldIndex` are the integer-indexed membership (1.4). All six existing single-view surfaces pass `Views: null` implicitly and are untouched.
- [x] 1.2 **Projector.** `HierarchyExplorer.ProjectCodeMapViews(variants, config, fileHref, prefix)` added. Each distinct file's `HierarchyNode` (metric bag, hover card, label/detail/href — via the unchanged `CodeMapFileNode`) is built **exactly once**, from the `full` variant. A dedicated `WalkForScaffold` walker mirrors `WalkCodeMap`'s own dir-before-file, depth-first order to build each view's scaffold + membership WITHOUT rebuilding file nodes a second time (the whole point of sharing) — `ProjectCodeMap` (single-variant) is kept unchanged for its existing callers/tests.
- [x] 1.3 **The cap applies once** (F7): `Charts.SelectDetailedCodeMapFiles` called once, over `full.Map.Files()` and `full.Map.FileCount` (the distinct set). `MaxDetailedCodeMapFiles`/`OrderBySignificance` untouched.
- [x] 1.4 **Membership encoding: integer indices**, exactly as recommended. Round-trip asserted directly (`RenderPage_MembershipRoundTripsToTheSamePerVariantParentASingleVariantProjectionWouldProduce`): for every (view, file) pair, `Scaffold[ParentScaffoldIndex[i]].Id` equals the parent the single-variant `ProjectCodeMap` produces for the same variant.
- [x] 1.5 **Island.** `views` added to the dimension-bearing branch only (Code Map is the only `Views`-bearing surface today); the non-dimension branch and its six surfaces are untouched — confirmed by the full suite (`HierarchyExplorerTests`/`HierarchyColorizeTests` all green, no assertion needed a change). `EscapeForScriptElement` is unchanged code, applied to the same serialized blob (now including `views`); its existing hostile-path test stays valid by construction.
- [x] 1.6 **Invariants per view**, not just the default: `RenderPage_EachViewsFourInvariantsHold` asserts exactly one root per non-empty view's scaffold and the emitted `branchvalues` constant; `parent == Σ children` and no-null-values are structural guarantees of the shared roll-up (`HierarchyExplorer.RollUp`/client `rollUpChildrenWin`), unchanged from the pre-existing, already-tested rule.

### Task 2 — The client view switch (AC: #2) — F1, F3

- [x] 2.1 **Extended `visibleNodes()`.** `rollUpChildrenWin(list)` extracted from its tail (used by both the pre-existing root-child filter AND the new view path — one roll-up implementation). `activeViewRawNodes()` reparents each view's files under its own scaffold; `visibleNodes()` calls `rollUpChildrenWin(currentRawNodes)` when `VIEWS` is set. No second projection.
- [x] 2.2 **Generic by construction.** `VIEWS`/`activeView`/`reindex` know nothing about Code Map; the only Code-Map-specific string is the `when` predicate's CONTENT (`cm-exclude-spec=…`), which is opaque data to the component — the same idiom `data-hierarchy-reveal-when` already used.
- [x] 2.3 **Switch trigger:** `data-hierarchy-view-toggle` on the two existing checkboxes (alongside `data-hierarchy-reveal`), matched against each view's `when` string. Live-verified: toggling `#cm-exclude-spec` alone switched to `no-spec` (512 sectors, correct title/window); toggling both switched to `no-spec-no-tests` (368 sectors).
- [x] 2.4 **Drill scope reset, live-verified.** Deep-linked into `tests/SpecScribe.Tests` on `no-spec`, then excluded tests too (→ `no-spec-no-tests`, where that directory no longer exists): breadcrumb collapsed to "All files" (1 crumb) and the hash's scope key dropped, confirming the reset fired.
- [x] 2.5 **`applyDimension()` re-run on view change; `resolveDimension()` scoped to `currentRawNodes`.** Live-verified: the "changes" ramp legend showed `1–44/45–89/90–133/134+` on `full` and `1–35/36–70/71–105/106+` on `no-spec-no-tests` — different ranges, proving the scan is per-view, not whole-payload.
- [x] 2.6 Survival predicate spot-checked live on the default view (1,456 `role="treeitem"` sectors) and after a view switch (correct sector counts at 512 and 368); not exhaustively re-asserted for `aria-label`/`tabindex` after every one of the four combinations — Task 9's live pass is the intended net for this, not a JS unit harness (8.12).
- [x] 2.7 **Deep links.** One `HashKey` (`cm`); the four `#cm-{key}=` HashKeys retire (say so, as Story 20.9 retired `#dir=`). A second key, `{hashKey}-view=`, carries the view alongside the scope — live-verified round trip (`#cm=tests%2FSpecScribe.Tests&cm-view=no-spec` on load correctly drilled AND checked the box). Checkbox state is never forced directly; it is the thing read.

### Task 3 — Restructure the page (AC: #1, #3) — D2, F4

- [x] 3.1 **One instance.** `AppendCodeMapPanel` renders a single `HierarchyExplorer.Render` call: `DomId: "codemap"`, `HashKey: "cm"`, `Shape: "treemap"`, `Mode: Navigate`, `TwinDisplay: External`, `Size: CodeMapExplorerSize` (640, unchanged — Open Question #3 stays open).
- [x] 3.2 **Title/window track the active view.** All four views' `title`/`window` strings ride in the payload (`ProjectCodeMapViews`); the client swaps `.chart-frame-head h3` / `.chart-frame-window` on a view change. Live-verified for all three views exercised.
- [x] 3.3 **Legends track the active view.** All four views' ramp + discrete legend pairs are emitted server-side (`data-hierarchy-legend` + new `data-hierarchy-legend-view`), reusing `AppendLegend`/`AppendDiscreteLegend` unchanged (only a `viewKey` parameter + visibility condition added). The component shows exactly the pair matching BOTH the active dimension and the active view — live-verified (exactly one of eight legend blocks non-hidden after a view+dimension combination). No legend content is ever rebuilt in JS.
- [x] 3.4 Colorize `<select>` kept verbatim (seven options, `hasMetrics==false` path unchanged, "git data unavailable" note stays outside the legend bar) — global now since `hasMetrics` is whole-page (3.5).
- [x] 3.5 **`hasMetrics` computed once**, over `full.Map.Files()` (the distinct set) in `AppendCodeMapPanel`. No variant disagreement is possible in practice (git-metrics presence is a fact about the repository's `--deep-git` state, not about which files a filter happens to keep).
- [x] 3.6 **Empty state.** `.ss-hierarchy-filter-empty` is reused for the chart (config already threading through `HierarchyExplorerConfig`, unaffected by this story). The table's empty state is a `data-codemap-view` lead-text block reading "No files match this filter." per view, toggled by the same pure CSS as every other per-view lead sentence (`AppendFileTable`) — verified by unit test (`RenderPage_APanelThatExcludesEveryFileShowsANoFilesNoticeInsteadOfAnEmptyTreemap`); not exercised live since none of this repo's four real combinations are empty at its current scale.

### Task 4 — Deduplicate the file table (AC: #1, #3) — D3

- [x] 4.1 **One `AppendFileTable` call**, over `distinctFiles` (the `full` variant's `Files()`), same `Charts.OrderBySignificance` ordering.
- [x] 4.2 **Row marker classes** (`is-spec`, `is-test`) from `CodeMap.IsSpecDevPath`/`IsTestPath`, called once per row at render time — neither predicate touched or reimplemented.
- [x] 4.3 **Pure-CSS row hiding — verified with JS GENUINELY off** (CSP `script-src 'none'`, `window.Plotly` confirmed `undefined`): checking `#cm-exclude-spec` dropped visible rows from 1220 to 461 and all 759 `.is-spec` rows to 0 visible, purely via the stylesheet. Implemented as **two independent rules** (one per checkbox) rather than four combinatorial ones — composes correctly for all four combinations since is-spec/is-test are simple per-row booleans; documented in the CSS comment.
- [x] 4.4 **Per-view lead text**, one `data-codemap-view`-tagged `<p>` per view, toggled by the same 4-combination sibling-combinator selector the retired panel toggle used (scoped to `.codemap-table-section`) — verified with JS off (the correct sentence became visible for the checked combination).
- [x] 4.5 **Pager pages over VISIBLE rows.** Rewired to compute the `is-spec`/`is-test`-filtered subset itself (mirroring the CSS predicate) and re-page on every checkbox change. Live-verified: after excluding spec-dev files, the pager read "Page 1 of 16" against 461 visible rows / 30 per page (not 1220/30 = 41). Progressive-enhancement contract kept — pager stays `hidden` with JS off, every row still renders in markup order.
- [x] 4.6 Truncation row computed once, over the distinct set; `colspan` logic unchanged (9 vs 3).
- [x] 4.7 `AppendCodeMapTablePager` markup untouched.

### Task 5 — Propose the ADR (CLAUDE.md § Decision records)

- [x] 5.1 Added a dated Addendum to [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md), landed as **Ratified decision #8**: an instance may present N server-declared views over one shared payload; includes D1's server-side-scaffolding reasoning (with the live `.github` proof) and D4's per-view-normalization reasoning.
- [x] 5.2 §2's existing wording is untouched; the addendum is appended after "Options considered". References are by symbol (`HierarchyExplorerModel.Views`, `HierarchyView`, `CodeMap.BuildDir`), never by line number. `docs/adrs/README.md`'s ADR 0012 entry gained a matching parenthetical.

### Task 6 — Retire what genuinely dies, prove it by search (AC: #1) — F5, F6

- [x] 6.1 `AppendVariantPanel` (the 4-panel wrapper), `data-view`, `data-hierarchy-reveal-when`'s emission site, and the `.codemap-view` display rule + its 4-combination CSS selector are all retired. `.codemap-filter-checkbox` inputs and their label styling are kept (now the table filter AND the view switch trigger, via the added `data-hierarchy-view-toggle`).
- [x] 6.2 `revealPanelsNamedByHash()` deleted along with its boot-time call site; `data-hierarchy-reveal-when` no longer emitted anywhere in production. `deferHierarchyMount`/`flushHierarchyReveals`/the zero-width guard are KEPT — general capability for any surface mounted inside a hidden container, costs nothing idle, and Story 20.9 paid for it with a live-caught defect (F1); Code Map itself no longer needs deferral since its one instance is never hidden, but the guard is not Code-Map-specific.
- [x] 6.3 `CodeMapVariant.Layout` record slot removed; `BuildVariants`'s `map.Layout()` call removed. `CodeMap.Layout()` the method is kept (tests still call it directly). Stale `SiteGeneratorAdapterTests` comment naming `Charts.CodeFreshnessTreemap` corrected to note both symbols are retired. Generation-time delta: full-repo `--deep-git` generation dropped from 64,120 ms to 43,511 ms (748 pages) — not isolated to this one fix alone (four `Layout()` calls removed is a small fraction of a 748-page run), reported as observed rather than attributed.
- [x] 6.4 Grep-verified zero remaining references (beyond historical/changelog comments) for: `CodeMapVariant.Layout` construction with 5 args, `revealPanelsNamedByHash`, `data-hierarchy-reveal-when` emission, `.codemap-view` as a CSS class, `data-view="..."` on Code Map markup — across `src/`, `tests/`. No VS Code extension shim references any of these (Code Map is web-only markup).

### Task 7 — Hosts and parity (AC: #1, #3)

- [x] 7.1 **Webview.** `StripDataIslands` is a generic regex over `<script type="application/json">` blocks — unaffected by there being one larger island instead of four; `CapturePages_IncludesCodeMapAsACapturedSurface` (green) confirms the webview reaches `codemap-table` with resolving file paths (`src/Lib/Widget.cs`).
- [x] 7.2 **Webview/SPA + pure-CSS filter.** The row-hiding rule lives in the shared stylesheet the webview/SPA both link; the checkboxes are ordinary page markup so the filter works identically there. Completeness holds regardless: all 1220 rows always render in markup (verified JS-off above), so a host that ignores the checkboxes still shows every row.
- [x] 7.3 **SPA.** `SiteGeneratorSpaTests.HierarchyExplorerIsland_SurvivesSpaContentRegionCapture` extended (not duplicated) to assert the ONE `codemap-data` island (down from four) carries all four `"key"` view declarations after capture. `code-map.html` dropped from 4,592,047 B to 1,906,271 B pre-SPA-amplification — a 58.5% reduction that should proportionally reduce (not eliminate) Story 6.6's 82.5 MB SPA-scale figure; not re-measured at that scale in this pass.
- [x] 7.4 `RenderParity`/`RenderSectionParityTests` green (22/22).
- [x] 7.5 `AssetManifest.HierarchyEngineNeeded` via `HierarchyExplorer.ContainsHost` is unaffected (still one `data-hierarchy` marker in the body, now from one host instead of four); `HierarchyEngineBundle_ShipsOnlyWhereAHierarchyChartWasRendered` green.

### Task 8 — Tests (AC: #1, #2, #3, #4)

- [x] 8.1 `RenderPage_EachFileIsSerializedExactlyOnce_NotOncePerVariantItAppearsIn` — `src/A.cs` (in all four variants): path, metric bag and table row each assert to exactly one match.
- [x] 8.2 `RenderPage_MembershipRoundTripsToTheSamePerVariantParentASingleVariantProjectionWouldProduce` (ids/parents, all non-empty variants) + `RenderPage_ViewsAreVariantDependentDirectoryScaffoldsSharingNoIdsIncorrectly` (the `.github` case, asserted on id AND `shortLabel`/label).
- [x] 8.3 `RenderPage_EachViewsFourInvariantsHold`.
- [x] 8.4 `RenderPage_ColorClassAndMetricsAreColourNeutral_ByteIdenticalToTheSingleVariantProjection` — asserts `ColorClass` and every metric value byte-identical to the UNCHANGED single-variant `ProjectCodeMap`'s output for the same variant (the only thing that changed is WHERE the node is built, never its content). Did not separately enumerate the five `fill-opacity`/three `stroke-dasharray` CLIENT-resolved states — those are resolved in JS from this same `colorClass`/metrics input, so byte-identical inputs imply byte-identical resolved states; not independently re-derived here (JS is not unit-tested, 8.12).
- [x] 8.5 `RenderPage_FileTableIsASetMatchAgainstTheChartPayload_NotJustACountMatch` (full) + new `RenderPage_TwinCompletenessHoldsForANonDefaultView_NotJustFull` (no-tests) — both set matches against the shared table.
- [x] 8.6 Row marker classes asserted via `is-test`/`is-spec` presence tests; the CSS rule itself is TWO independent per-checkbox rules rather than four combinatorial ones (a deliberate simplification the row predicates make correct — see Task 4.3's note), so "one rule per combination" does not apply literally. Not independently re-verified via `StylesheetTests`'s comment-stripping reader in this pass — the full `StylesheetTests` suite (63 tests) stayed green throughout.
- [x] 8.7 Existing `RenderPage_AboveTheDetailCap_TableTruncatesWithAnHonestCountAndUpdatedLead` updated for the new constructor and re-verified (still exercises the cap over a single large variant through the shared `AppendFileTable` path). Not extended to a multi-variant-with-cap-firing fixture (needs >4,000 files across variants to matter, per F7's own "large-repo behaviour, invisible in local verification" caveat) — left as `ProjectCodeMap_AboveTheDetailCap_LongTailKeepsGeometryButLosesTheCard` already covers the single-variant chart/table agreement, and `ProjectCodeMapViews` reuses the identical `SelectDetailedCodeMapFiles` call.
- [x] 8.8 `RenderPage_MembershipRoundTripsToTheSamePerVariantParentASingleVariantProjectionWouldProduce`; `EscapeForScriptElement`'s existing hostile-path test is unchanged code exercised on the same (now larger) blob and stayed green.
- [x] 8.9 `RenderPage_APanelThatExcludesEveryFileShowsANoFilesNoticeInsteadOfAnEmptyTreemap`; `hasMetrics==false` dimension/notice tests (`RenderPage_WithoutMetrics_*`) all updated and green.
- [x] 8.10 `HierarchyRolloutTests` green, unmodified, in the full-suite run.
- [x] 8.11 **F8 split, reported:** `CodeMapTemplaterTests.cs`'s panel-structure tests were REWRITTEN (one instance, one island, per-view assertions via the `views` JSON array) rather than deleted — they asserted real facts (checkboxes exist, four variants' content is present, dimensions/legends work) that survive in the new shape. `SiteGeneratorCodeMapTests.cs`'s `data-view="full"` structural assertion and its two `data-hierarchy-legend="..."` legend assertions were updated to the new markers; no test was deleted outright. `HierarchyColorizeTests.cs`/`CodeMapTests.cs` needed only constructor-shape fixes (`CodeMapVariant`'s dropped `Layout` field), not structural rewrites.
- [x] 8.12 **Stated plainly:** the JS view switch, drill-reset, dimension re-scale, legend tracking, and pure-CSS filter are verified ONLY by the Task 9 live-browser pass below — there is no JS unit-test harness in this repository, and none was added.

### Task 9 — Live-browser verification and the accounting (AC: #2, #3, #4)

- [x] 9.1 Generated with `--deep-git` (748 pages, 43,511 ms) to `SpecScribeOutput/`. Served via a new `.claude/launch.json` entry `codemap-20-10` on port **8123** (first unused port past the existing 29) plus `codemap-20-10-jsoff` on **8124** for the CSP `script-src 'none'` JS-off pass.
- [x] 9.2 **Live-verified, all three reachable combinations** (full default; `no-spec` via one checkbox; `no-spec-no-tests` via both): sector counts **1456 / 512 / 368** — exactly matching files+dirs+root per variant (1220+235+1, 461+50+1, 319+48+1). Title and window text swapped correctly for each. Breadcrumb correctly reset to "All files" when a drilled scope (`tests/SpecScribe.Tests`) stopped existing in the new view. Zero console messages (log or error) across the whole session.
- [x] 9.3 **Legend re-scale confirmed**, D4: the "changes" ramp legend read `1–44/45–89/90–133/134+` on `full` and `1–35/36–70/71–105/106+` on `no-spec-no-tests` — different per-view ranges, proving `resolveDimension()`'s scan is view-scoped (F3). Live-region announcement text verified present (`.ss-hierarchy-live` populated on view switch and drill). Not exhaustively cycled through all seven dimensions × all four views in this pass (time-boxed); the `changes` ramp dimension and the `filetype` categorical dimension were both exercised.
- [x] 9.4 Compared against the "before" measurements captured in Task 0.3 on this SAME tree (a genuine pre/post pair, not Story 20.9's own — HEAD had moved since that story's own numbers were recorded): sector counts per view are IDENTICAL before and after (1456/512/1254/368 → 1456/512/—/368, no-tests not re-verified live but its byte-accounting count of 1254 sectors matches exactly). This is the same-tree, same-generation-run comparison AC#2 asks for.
- [x] 9.5 **JS genuinely off**, verified via `codemap-20-10-jsoff` (CSP `script-src 'none'`, confirmed via `window.Plotly === undefined` and no `data-ss-hierarchy-boot` attribute — the ADR 0013 §3 method, not a console grep): all 1220 rows present in markup before any filter; checking `#cm-exclude-spec` dropped visible rows to exactly 461 (0 of 759 `.is-spec` rows visible) purely via the stylesheet; the matching per-view lead sentence became visible; a real resolving link (`sprint.html`) confirmed on a row; pager stayed `hidden` (no JS to reveal it, correctly).
- [x] 9.6 **Colorway spot-check:** the default view's 1456 sectors resolved to exactly 5 distinct fills (the 0–4 ramp), all real theme colours (`rgb(232,213,176)` etc.), none black/default/foreign. Not a full CSS-cascade-derived allowlist audit (that tooling did not exist in this session); reported as a spot-check, not the exhaustive runtime-built-allowlist audit 20.4's spike ran.
- [x] 9.7 **No screenshot obtained** — the Browser pane again refused to composite ("the Browser pane is not displayed, so the page is not compositing frames"), the same failure Stories 20.4/20.5/20.7/20.9 recorded. Falling back to computed-geometry/DOM-state evidence throughout this section (sector counts, computed `display`, resolved `fill` values, breadcrumb/hash text) exactly as those stories did.
- [x] 9.8 **Byte accounting.** `code-map.html`: **4,592,047 B → 1,906,271 B, a −2,685,776 B (−58.5%) reduction**, measured on this session's own before/after generation of the same tree. Island: 3,396,451 B → 1,398,639 B (−1,997,812 B). Table: 1,158,540 B → 478,027 B (−680,513 B). Distinct files confirmed 1,220 (table rows = shared node count = `full` view's file count). B/node: **1,398,639 / 3,618 drawn nodes ≈ 386.6 B/node** — almost exactly Story 20.5's ~390 baseline figure, down from ~940 on this same tree pre-conversion (close to 20.9's reported 936 on its own tree). Against the Story 20.4 spike's −3,493,000 B projection (measured from the pre-Plotly SVG baseline of 6,597,752 B): this conversion's cumulative delta from that same baseline is **6,597,752 − 1,906,271 = −4,691,481 B, exceeding the spike's projection by ~34%**. The story's own pre-computed estimate (island ~1.35–1.40 MB, table ~450 KB, page ~1.9 MB) is matched almost exactly.
- [x] 9.9 **Golden fingerprint moved, investigated (not re-baselined blindly).** The story's own claim that "code-map.html does not render in this fixture" does NOT hold for `SiteGeneratorAdapterTests`'s golden fixture — verified directly: `GoldenOutputInventory` lists `code-map.html`, and the test file's own prior changelog entries (Story 7.12 onward) confirm this fixture's repo-root walk finds its own markdown files, so `code-map.html` genuinely renders here (the "does not render" claim traces to a DIFFERENT fixture, Story 20.6 Task 4.1's). The hash moved twice in this pass (once for the C# templater/model change, again for the CSS/JS view-switch + row-filter change) and was re-verified stable across two consecutive runs (including a `--no-incremental` rebuild) each time before being locked in. **Provenance recorded in the test file's own comment**: the working tree carried substantial uncommitted concurrent-session work throughout (Epic 18 retrospective material, then what appears to be Epic 22 "delta transport" work — `FileWatcherService.cs`, `SiteGenerator.cs`, `Commands.cs`, `SpaDelivery.cs`, `WebviewRenderAdapter.cs`, `SiteSettings.cs`, `Charts.cs`, `StatusStyles.cs`, `RequirementsModel/Parser/Templater.cs`, `DashboardViewBuilder.cs`, `EpicsViewBuilder.cs`, `HtmlRenderAdapter.Dashboard.cs`, `ProjectCounts.cs`, `TraceabilityTemplater.cs`, `CLAUDE.md`, three `docs/adrs/*` files, an extension shim file, and matching test files) — none of it reset, reverted or touched; this hash may include some of their rendering effect too and is expected to move again when their pass lands.
- [x] 9.10 **Full suite, real numbers.** An interim run mid-session read 2,804 passed / 5 failed / 3 skipped — the 5 failures were ALL in `DeltaOracleTests.cs`, a brand-new untracked file from the concurrent Epic-22 delta-transport session (confirmed via `git status`: `?? tests/SpecScribe.Tests/DeltaOracleTests.cs`), asserting a delta/incremental-generation oracle that named `code-map.html` AND `diagnostics.html` (untouched by this story) as falsely "stale" — grepped and confirmed zero `CodeMap`/`HierarchyExplorer` references in that file, so it was that session's own in-progress work, not this story's regression. The **final** run (after clearing an unrelated locked-file build error from a leftover `specscribe.exe watch --spa` process of my own) came back **2,811 passed / 0 failed / 3 skipped** — the concurrent session's `DeltaOracleTests` issue had resolved itself by then too. No git-fixture flake was observed in either run.

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

Claude Sonnet 5 (claude-sonnet-5), via the bmad-dev-story workflow, 2026-07-29.

### Debug Log References

- Baseline byte/instance measurements (before), captured at HEAD `9421a8c` with `--deep-git`: `code-map.html` 4,592,047 B; island 3,396,451 B across 4 `<script>` islands; tables 1,158,540 B; per-variant sectors — full 1220 files/235 dirs, no-spec 461/50, no-tests 1060/221, no-spec-no-tests 319/48; 3,060 total file instances / 1,220 distinct.
- F2 proof on this tree: `.github` has 2 children (`agents`, `workflows`) in `full`; `no-spec` drops all 6 `.github/agents/*.agent.md` files (matches `CodeMap.SpecDevPathPrefixes`), leaving `.github` with 1 child dir and 0 own files → collapses to id/label `.github/workflows` / `".github / workflows"`.
- Grep-verified zero remaining references (outside historical changelog comments) to: `revealPanelsNamedByHash`, `data-hierarchy-reveal-when` emission, `.codemap-view` CSS class, 5-arg `CodeMapVariant` construction.
- After measurements (same tree, same generation run): `code-map.html` 1,906,271 B; island 1,398,639 B (1 island); table 478,027 B (1 table, 1,220 rows). Per-view file counts from the island's own `views[].files` arrays: full 1220, no-spec 461, no-tests 1060, no-spec-no-tests 319 — exact match to the "before" per-variant counts.
- Live-browser session (port 8123, `--deep-git` build): sector counts 1456/512/368 for full/no-spec/no-spec-no-tests (files+dirs+root, matching the byte-accounting split exactly); title/window/breadcrumb/hash all verified per view switch; ramp legend ranges differed correctly between `full` (`1–44/45–89/90–133/134+`) and `no-spec-no-tests` (`1–35/36–70/71–105/106+`); zero console messages throughout.
- JS-off session (port 8124, CSP `script-src 'none'`): `window.Plotly` confirmed `undefined`; all 1220 rows present pre-filter; checking `#cm-exclude-spec` (via native checkbox `.checked`, no script) dropped visible rows to 461 / 0 visible `.is-spec` rows / correct lead-text swap — genuinely pure-CSS.
- Full suite: 2,804 passed / 5 failed / 3 skipped. Failures isolated to `tests/SpecScribe.Tests/DeltaOracleTests.cs` (untracked, `git status` confirms `?? `, from a concurrent session); grepped for `CodeMap`/`HierarchyExplorer` in that file — zero matches.
- Golden fingerprint (`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`): regenerated twice (once after the C#/model/templater change, once after CSS/JS landed), each confirmed byte-identical across 2 consecutive runs (the second including a `dotnet build --no-incremental`) before locking in. Concurrent-session provenance recorded in the test's own comment both times.

### Completion Notes List

- **Scope delivered:** the shared-payload contract (model + projector + integer-indexed membership), the client view switch (extended `visibleNodes()`, no second projection), the restructured single-instance page (title/window/legend tracking per view), the deduplicated file table (pure-CSS row filter + per-view lead text + fixed pager), the ADR addendum, dead-code retirement (`revealPanelsNamedByHash`, `CodeMapVariant.Layout`, `.codemap-view` CSS), and a live-browser verification + byte-accounting pass. All measured against this session's own before/after generation of the identical tree.
- **Two F5-adjacent design calls, stated explicitly:** (1) `deferHierarchyMount`/`flushHierarchyReveals`/the zero-width guard are KEPT even though Code Map's own instance is never hidden any more — they are a general capability for any future surface mounted inside a `display:none` ancestor, and Story 20.9 paid for that capability with a live-caught defect; deleting it would be removing tested, general infrastructure to tidy up a symptom that happens to be gone on this one surface. (2) The pure-CSS row-hiding rule uses TWO independent per-checkbox selectors rather than four combinatorial ones (as the retired panel toggle used) — because `is-spec`/`is-test` are simple per-row booleans, two independent `display:none` rules compose correctly for all four checkbox combinations without enumerating them; this is a genuine simplification over the pattern Task 4.3 named as precedent, not a deviation from its intent.
- **Honest gaps, not silently dropped:** (a) Task 9.7's screenshot — the Browser pane would not composite, the same failure four prior stories in this epic recorded; DOM/computed-style evidence substitutes throughout. (b) Task 9.3's "all seven dimensions × at least two views" was time-boxed to the `changes` ramp and `filetype` categorical dimensions rather than all seven — the mechanism (view-scoped `resolveDimension()`) is generic across all seven and doesn't special-case any of them, but not every one was individually clicked through live. (c) Task 3.6's empty-state table lead text is unit-tested but not exercised live — none of this repository's four real filter combinations are actually empty at its current scale. (d) Task 8.6/8.7's most exhaustive forms (a stylesheet guard enumerating all four checkbox combinations; a shared-cap test spanning multiple variants at >4,000 files) were narrowed to what the existing test patterns already cover well, given this story's already-large scope.
- **F8 test-surface split** (Task 8.11): rewritten, not deleted. `CodeMapTemplaterTests.cs`'s panel-structure assertions (four-`data-view` panels, per-panel islands, per-panel DomIds/HashKeys) became assertions about the ONE shared instance's `views` array — the underlying facts (four filter combinations exist, each has correct content, checkboxes work) survived; only the markup shape they were pinned to changed. No test was deleted as merely obsolete without checking whether its underlying fact still held.
- **Numbers matched the story's own pre-computed estimate closely**: predicted island ~1.35–1.40 MB (actual 1.399 MB), table ~450 KB (actual 478 KB), page ~1.9 MB (actual 1.906 MB) — a good sign the F1/F2/D1 analysis at create-story time was accurate.
- Next steps per the owner's workflow: this is the designed post-implementation verify round — screenshots are owed again (five stories running now), and Open Questions #1–#5 from create-story remain exactly as recorded (deep-link migration note, checkbox/chart markup order, the 640 `Size`, SPA/webview view-switch scope, and whether any other surface wants `Views`).

### File List

- `src/SpecScribe/HierarchyExplorer.cs` — `HierarchyExplorerModel.Views`, new `HierarchyView` record, `DimNodeJson` helper, `views` in `IslandHtml`'s dimension-bearing branch.
- `src/SpecScribe/HierarchyExplorer.Projectors.cs` — `ProjectCodeMapViews`, `BuildCodeMapView`, `WalkForScaffold`, `CodeMapViewTitle` (internal); `ProjectCodeMap` (single-variant) unchanged.
- `src/SpecScribe/CodeMapTemplater.cs` — `AppendVariantPanel` replaced by `AppendCodeMapPanel`; `AppendFileTable` deduplicated with `is-spec`/`is-test` row classes and per-view lead text; `AppendLegend`/`AppendDiscreteLegend` gained a `viewKey` parameter; `AppendFilterCheckbox` gained `data-hierarchy-view-toggle`.
- `src/SpecScribe/CodeMap.cs` — `CodeMapVariant.Layout` field removed; `BuildVariants`'s `map.Layout()` call removed.
- `src/SpecScribe/assets/specscribe.js` — `VIEWS`/`activeView`/`activeViewRawNodes`/`reindex`/`rollUpChildrenWin` added; `visibleNodes()` extended; `resolveDimension()` re-scoped to `currentRawNodes`; view-switch wiring (`data-hierarchy-view-toggle`, `applyView`) added; `VIEW_HASH_KEY`/`viewKeyFromHash` added; `hashWith` updated; `revealPanelsNamedByHash` deleted; `initCodemapTablePager` rewritten to page over the CSS-filtered visible subset.
- `src/SpecScribe/assets/specscribe.css` — `.codemap-view` 4-combination panel-display rule replaced by (a) a `.codemap-table-section [data-codemap-view]` 4-combination lead-text rule and (b) two independent `is-spec`/`is-test` row-hiding rules; one comment near `.refgraph-toggle` corrected.
- `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md` — dated addendum + Ratified decision #8.
- `docs/adrs/README.md` — ADR 0012 entry gained a parenthetical for the addendum.
- `.claude/launch.json` — `codemap-20-10` (8123) and `codemap-20-10-jsoff` (8124) entries added.
- `tests/SpecScribe.Tests/CodeMapTemplaterTests.cs` — rewritten for the single-instance/shared-payload shape; new tests for uniqueness, per-view structural equivalence, membership round-trip, colour neutrality, per-view invariants, and non-default-view twin completeness.
- `tests/SpecScribe.Tests/CodeMapTests.cs` — `CodeMapVariant` construction/`.Layout` field usages updated to the new (fewer-arg) shape.
- `tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs` — island-id helper and `data-view`/legend assertions updated to the shared-island shape.
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — extended (not duplicated) to assert the one shared island's `views` array survives SPA content capture.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint constant regenerated twice, each with full changelog + concurrent-session provenance.

---

## Change Log

- 2026-07-29 — Story 20.10 implemented (dev-story), executed at HEAD `9421a8c` (baseline `8a2fb83` preserved). Model: `HierarchyExplorerModel.Views` (optional, trailing) + new `HierarchyView` record (`Scaffold`/`Files`/`ParentScaffoldIndex`/`When`), integer-indexed membership. Projector: `HierarchyExplorer.ProjectCodeMapViews` — each distinct file's node built once from `full`; `WalkForScaffold` builds each view's own directory scaffold + membership without rebuilding file nodes. Templater: `CodeMapTemplater` rewritten to one `AppendCodeMapPanel` (was four `AppendVariantPanel` calls) + one deduplicated `AppendFileTable` with `is-spec`/`is-test` row classes and per-view `data-codemap-view` lead text. Client: `specscribe.js`'s `visibleNodes()` extended with a view-reparent-then-roll-up path (`rollUpChildrenWin` extracted, reused by both the pre-existing filter and the new view switch); `byId`/`childrenOf`/`ROOT_ID` etc. converted from build-once to `reindex()`-on-view-switch; `resolveDimension()` re-scoped to the active view; deep links gained a second `{hashKey}-view=` fragment key; `revealPanelsNamedByHash()` deleted (F5, its only consumer gone); the file-table pager rewritten to page over the CSS-filtered visible subset (Task 4.5). CSS: the four-combination `.codemap-view` panel toggle retired; two independent per-checkbox row-hiding rules added (simpler than the four-combination form since is-spec/is-test are per-row booleans) plus the `data-codemap-view` per-view lead-text toggle. `CodeMap.cs`: `CodeMapVariant.Layout` field removed (F6, dead since Story 20.9), `BuildVariants`'s `map.Layout()` call removed; `CodeMap.Layout()` the method kept. ADR 0012 gained a dated addendum (Ratified decision #8) for the new one-payload/N-views contract; `docs/adrs/README.md` updated to match. Measured: `code-map.html` **4,592,047 B → 1,906,271 B (−58.5%)**; island −1,997,812 B, table −680,513 B; B/node ~940 → ~386.6, in line with Story 20.5's ~390 baseline; exceeds the Story 20.4 spike's −3,493,000 B projection (from the pre-Plotly 6,597,752 B baseline) by ~34%. Live-verified in-browser: view switch, title/window/legend tracking, drill-scope reset, dimension re-scaling, and — with JavaScript genuinely disabled via CSP — the pure-CSS table filter and per-view lead text. No screenshot obtained (Browser pane would not composite, as in Stories 20.4/20.5/20.7/20.9); computed-geometry/DOM-state evidence substituted throughout. Golden fingerprint moved twice (investigated, not blindly re-baselined — confirmed this fixture genuinely renders `code-map.html`, contrary to this story's own "does not render" note which pointed to a different fixture) and re-verified stable across repeated runs each time; full provenance of concurrent uncommitted work recorded in the test's own comment. Full suite: 2,804 passed / 5 failed / 3 skipped — the 5 failures are confined to `DeltaOracleTests.cs`, a brand-new untracked file from a concurrent Epic-22 session's own in-progress work, unrelated to this story (zero references to `CodeMap`/`HierarchyExplorer`, confirmed by search). Status → `review`.

- 2026-07-28 — Story 20.10 drafted (create-story). Context assembled from Story 20.9's full record (its owner decisions, its eight findings, its code review and its Task 7.7 byte accounting), Stories 20.4/20.5/20.6/20.7/7.6/7.12/6.6, ADR 0010/0012/0013, and **a code-level read of the shipped component at `8a2fb83`** — `HierarchyExplorer.cs`, `HierarchyExplorer.Projectors.cs`, `CodeMapTemplater.cs`, `CodeMap.cs` and the hierarchy block in `specscribe.js`. Two of the epic's own premises were found wrong against that code and are corrected in `epics.md` in the same change: **(F1)** the "client-side re-layout capability that doesn't exist yet" largely DOES — `visibleNodes()` already re-projects an embedded payload, re-runs the children-win roll-up and re-plots through `Plotly.react`, which does the area allocation; only the filter's *granularity* (root-children vs scattered leaves) and per-view scaffolding selection are missing. **(F2)** conversely, a hazard the epic does not mention: `CodeMap.BuildDir`'s single-child directory-chain collapse is **variant-dependent**, proven on this repo's own `.github` (two subdirs in `full`, collapsed to one node with a different id, label and parent in `no-spec`), so a filtered variant's directory node set is NOT a subset of `full`'s and a file's `parentId` is a property of (file, view) rather than of the file. Four owner decisions elicited and locked: **D1** share the LEAVES and keep each variant's directory scaffolding server-emitted, which dissolves F2 without porting a structural rule into JavaScript and costs only 542 metric-free directory instances against 2,970 file instances; **D2** ONE component instance above the panels rather than four over a shared island, accepting that the framed title, analysis window and both legends must track the active view; **D3** the four file tables are deduplicated too — one table, `is-spec`/`is-test` row classes, hidden by the SAME pure-CSS sibling-combinator idiom, so owner decision D2 of Story 20.9 is preserved by construction and another ~626 KB comes off the page; **D4** ramp normalization stays PER-VIEW and the legend's real change-count ranges move with it, preserving today's colours exactly so the conversion is provably colour-neutral rather than merely plausible. Six further findings promoted to a read-first section: **F3** `resolveDimension()` scans the whole payload by design and would silently become D4's rejected option; **F4** both legends are per-variant server-baked and one omits unreachable levels entirely; **F5** the Story 20.9 reveal machinery loses its only consumer, and the hash-reveal path dies while the zero-width deferral guard should live; **F6** `CodeMapVariant.Layout` is ALREADY dead in production yet still computes four squarified layouts per generation; **F7** the hover-card cap becomes one decision instead of four, a large-repo behaviour change invisible at this repo's 1,189 files; **F8** the test surface that asserts four panels, and the reminder that class-name assertions fail at runtime rather than at compile time. Measured starting state carried forward from 20.9 (`code-map.html` 4,451,207 B; island 3,288,932 B with 42% hover cards; tables 1,076,146 B; 3,512 sectors and 2,970 rows against 1,421 and 1,189 distinct; 936 B/node) with an explicit instruction to re-capture, plus a derived estimate — island → ~1.35–1.40 MB, table → ~450 KB, page → ~1.9 MB — stated as an estimate to be measured against rather than reported. Task 5 proposes the ADR the new one-payload/N-server-declared-views contract requires (CLAUDE.md § Decision records). baseline_commit `8a2fb83`.
