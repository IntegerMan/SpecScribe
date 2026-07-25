---
baseline_commit: 92fa58149253105a08dc458cfed05a95a989229b
---

# Story 20.5: The Hierarchy Explorer Component — One Datasource, One Selector, One Mode Contract

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (+ its Story 20.4 addendum) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
**Validated by:** [`20-4-spike-report.md`](./20-4-spike-report.md) — measurements, not predictions. Do not re-measure what it measured. **Read the code-reviewed version (2026-07-25);** its review added a sixth finding and two boundary corrections this story acts on.
**Baseline commit:** `9369ca4`

## Story

As a maintainer who wants site-wide chart changes to land in one place,
I want a single standardized component that renders a sunburst and a treemap over the same datasource behind one selector, with an explicit activation mode,
so that every hierarchy surface shares one implementation, one interaction grammar, and one place to add future features.

---

## ⛔ Read first — what this story IS and IS NOT

**IS:** build the Hierarchy Explorer — vendored Plotly bundle + a C# payload/scaffold emitter + one client component in `specscribe.js` — and mount it on **the dashboard only**, with the existing server-rendered SVG **kept underneath** as the live fallback.

**IS NOT** (each of these belongs to a named later story; doing it here breaks a ratified gate):

| Not this story | Whose it is | Why it must wait |
|---|---|---|
| Retiring **any** server-rendered chart SVG | 20.6 gate, then 20.7 | ADR 0013 §3 is a **hard per-surface gate** verified live with JS off. No twin has been audited yet. |
| The golden-fingerprint **replacement** assertions | 20.6 AC#2 | ADR 0013 §6 ties the replacement to the first SVG *retirement*. This story retires none. (It still **moves** the hash — see Task 8.) |
| Converting the other **six** call sites | 20.7 | Epic rollout inventory. This story ships the component and one mount. |
| Deleting the 7 `Charts.cs` entry points / 3 JS arc renderers | 20.7 AC#2 | They are still the live fallback. |
| Mounting in the **VS Code webview** | 20.7 (owner decision D4 below) | `WebviewRenderAdapter.cs:79` strips every JSON island; 20.7 owns `RenderParity` and the ADR 0005 CSP amendment lands jointly with Story 23.4. |
| The **ADR 0005 CSP amendment** | Story 23.4, jointly | ADR 0012 §5: landed **once**, not twice. |
| A dashboard **details pane** | 20.8 | Story 20.3 already shipped the card rail; 20.8 narrows to select-mode. This story feeds it. |
| Replacing / deleting `Charts.SunburstCompanionList` or the 20.3 rail | 20.6 / 20.7 | Both are live, both are part of the dashboard's twin story. Leave them standing. |

---

## Owner decisions locked at create-story (2026-07-25)

These four were explicitly handed to this story's elicitation by the 20.4 spike (§11) and are now settled. They constrain **how** the ACs below are met.

**D1 — Mount: dashboard, SVG kept underneath.** The server still emits `Charts.Sunburst(...)` unchanged. The component, **only on successful mount**, hides the SVG and renders Plotly in its place. Nothing retires, the fallback is real rather than theoretical, the owner can eyeball it on the real dashboard, and 20.7's deletion becomes a clean subtraction. Accepted cost: the SVG bytes stay until 20.7.

**D2 — Ring geometry: children win (honest tree).** A parent's value is the exact sum of its drawn children. Rings can never disagree and a child's angle is comparable across the whole chart. Accepted cost: some epic sweeps shift visibly from today's silhouette (today's `SunburstEpicWeight` also counts epic-level follow-up *peers* that are not drawn as children; 14 of 25 parents disagree — epic-1 is 42 vs its children's 50).

**D3 — Visual direction: "Labelled explorer".** Larger radius, Plotly in-sector labels where they fit, a breadcrumb bar above the chart, labelled treemap tiles. Closest to the owner's original *"click and drill into a directory… via Plotly and it's amazing"* request. **Accepted cost, and it is a real one:** a bigger chart competes with Story 20.3's card rail inside `.explorer-layout` (`grid-template-columns: minmax(0,1fr) minmax(240px,320px)`, stacking at ≤900px). Task 6.4 carries the layout plan; the owner verifies it in the iterate round.

**D4 — Webview: defer to 20.7.** This story records the decision as *owed*, does not make it, and changes nothing in `WebviewRenderAdapter.cs`. The existing `data-island` strip + static SVG remain the webview's behavior, and `SunburstExplorerTests.WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks` must keep passing unchanged.

---

## Acceptance Criteria

*Verbatim from [`epics.md` § Story 20.5](../planning-artifacts/epics.md). D1–D4 constrain how they are satisfied; they do not amend them.*

1.
**Given** the node payload shape Story 20.2 committed (`id`, `parentId`, `label`, weight, `statusClass`, `href`, `kind`)
**When** the component renders
**Then** both shapes read that **same** embedded payload — switching shapes never re-derives geometry, never re-counts against `ProjectCounts`, and never issues a fetch (`file://`-safe)
**And** it supplies one selector idiom, one Story 10.2 framing block (legend + analysis window + framing sentence), and one text twin, so no call site hand-writes any of them.

2.
**Given** ADR 0012's `navigate` | `select` mode contract
**When** a node is activated in `navigate` mode
**Then** it follows the node's `href` honoring the Story 9.13 destination contract (leaf → detail page, group → generated filtered list page)
**And** in `select` mode it raises a selection event **without navigating**, the selected node's own destination remains reachable, and the selection is announced to assistive technology.

3.
**Given** Plotly drills in on click by default
**When** the component wires activation
**Then** drill-in is a **distinct affordance** from activation — a node never silently both drills and activates — and breadcrumb drill-up plus URL-hash deep-linking work per UX-DR5/UX-DR6
**And** keyboard traversal, reduced-motion, and non-color status signalling all hold (UX-DR7, UX-DR17, UX-DR18), verified in a live browser.

4.
**Given** a node with no plan yet (a story with zero tasks and no nested deferred, whose true size is unknown) and the owner's 2026-07-24 "bump to average" decision
**When** the datasource projects that node's weight
**Then** the node is sized to the **average weight of the drafted nodes** — not a 1-unit sliver that reads as misleadingly trivial — while every drafted node keeps its honest weight (the floor only lifts, it never shrinks a real wedge), and a project with nothing drafted yet falls back to the historical 1-unit floor
**And** the component **preserves** this policy rather than re-deriving it: the interim SVG glance + Story 20.2 explorer island already ship it via `Charts.SunburstNoPlanStoryWeight` (threaded through `SunburstStoryWeight`/`SunburstEpicWeight`), so Story 20.7's conversion must carry the average-bump forward — verified in a live browser that un-drafted stories render at a typical, clickable size, not a hairline.

---

## 🔴 The four blocking data-contract defects — fix these FIRST

The 20.4 spike found four defects between the shipped 20.2 island and Plotly's hierarchy model. **All four are blocking, all four are cheap, none appears in ADR 0012 or the epic.** The hand-rolled SVG never surfaced them because it scales each ring independently and draws its centre as a decorative circle rather than a data node. Source: [spike report §7](./20-4-spike-report.md).

**A — Plotly hierarchy traces require exactly ONE root.** The island is a **25-root forest** (24 epics + `unplanned` + `orphan`) and Plotly refuses it outright: *"Multiple implied roots, cannot build sunburst hierarchy of trace 0."*
→ **Synthesize a project root in the emitter** (not client-side — the payload should be valid on its own, and the twin needs the same tree). It is also where Escape-to-top and the breadcrumb land. Label it with the site title; `href` = `index.html` (or the instance's own root href).

**B — A single `null` in `values` silently renders NOTHING.** calcdata collapses to one point. **No error. No console warning.** Measured: calcdata went `1 → 119` on changing `null` to `0`.
→ Every node's value is a number. Branch/parent values are `0` or a real sum — never `null`, never absent. A test must assert no null reaches the serialized payload.

**C — Parent weight ≠ Σ children.** `branchvalues: 'total'` is invalid and warns per offending parent. **Resolved by owner decision D2: children win.**
→ The emitter computes each parent's value as the sum of its emitted children (leaves keep their honest weight). Emit `branchvalues: 'total'` **only** once the payload is genuinely parent-inclusive and self-consistent; if you instead leave parents at `0` you must use `branchvalues: 'remainder'`. **Pick one and assert it in a test** — a payload/`branchvalues` mismatch is the failure mode that renders a blank or wrong chart with only a console warning.

**D — `npm run custom-bundle` is not available from the npm package.** `plotly.js@3.7.0` ships `lib/`, `src/`, `dist/`, `esbuild-config.js` but **not** `tasks/`, and `esbuild-config.js` requires `./tasks/util/constants.js`.
→ `tools/plotly-vendor/` **cannot** be a straight copy of `tools/prism-vendor/`'s `npm i` + `build.js` shape. Its README must document the `git clone --branch v3.7.0 --depth 1` step. Exact recipe in Dev Notes.

**A fifth, environmental:** Plotly resolves its own promises **off an animation frame**, so `await Plotly.react(…)` never settles in a non-compositing tab. **Hang everything on `plotly_afterplot`, never on the returned promise** — and it is the only hook that also fires for re-renders the component did not initiate, which is what made UX-DR7 a genuine PASS.

---

## Tasks / Subtasks

### Task 1 — Vendor Plotly 3.7.0 (AC: #1)

- [x] 1.1 Create `tools/plotly-vendor/` following `tools/prism-vendor/`'s **discipline** (hand-run build, committed artifact, embedded resource, conditional copy) but **not** its shape — Finding D. Include `README.md` documenting the clone step, `package.json` (private, throwaway), `.gitignore` for `plotly-src/` + `node_modules/`, and a `build.mjs` that runs the custom-bundle and copies the artifact into `src/SpecScribe/assets/`.
- [x] 1.2 Build the **standard** bundle (not `--strict` — spike §3.2: 7 bytes larger, byte-identical CSP-construct profile, because the `Function`-constructor paths `--strict` exists to avoid live in gl/regl traces this build already excludes):
      `npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy`
      Expect the resolved trace list to be `heatmap, scatter, sunburst, treemap` + `calendars` — `scatter` lives in `lib/core.js` and **cannot** be excluded from any bundle. Expected artifact size **1,223,515 B**; a materially different number means you built something else.
- [x] 1.3 Commit the artifact as `src/SpecScribe/assets/plotly-hierarchy.min.js` and add `<EmbeddedResource Include="assets\plotly-hierarchy.min.js" />` to `src/SpecScribe/SpecScribe.csproj` beside the prism entries (~line 62), with the same style of comment stating it is copied **only when a hierarchy chart was rendered**.
- [x] 1.4 Add `ForgeOptions.HierarchyEngineScriptName = "plotly-hierarchy.min.js"` beside `CodeHighlightScriptName` (`ForgeOptions.cs:70`). **Never a CDN URL anywhere** (ADR 0012 §1, NFR-3).
- [x] 1.5 Record the supply-chain facts for NFR10/Epic 17 in the vendor README: `plotly.js` **3.7.0**, **MIT**, one self-contained classic script, **zero** transitive runtime footprint, `specscribe generate` still needs no Node. Upstream clone's dev tree reports 9 npm-audit findings (1 low / 1 moderate / 7 high) — **all build-time devDependencies of the upstream repo, none in the emitted artifact**; say so, so the eventual audit is not surprised.
- [x] 1.6 Delete `spike/plotly/` once `tools/plotly-vendor/` works (the spike README says so explicitly). Keep `20-4-spike-report.md` and the ADR addendum — those are the durable outputs. Remove the three `plotly-csp-*` entries from `.claude/launch.json`.

### Task 2 — The payload emitter: `HierarchyExplorer.cs` (AC: #1, #4)

- [x] 2.1 New `src/SpecScribe/HierarchyExplorer.cs` — a **host-neutral, pure projection** (AD-2), no `ProjectCounts` re-count, no second geometry, no git call:
      - `enum HierarchyMode { Navigate, Select }`
      - `record HierarchyNode(string Id, string? ParentId, string Label, int Value, string StatusClass, string StatusLabel, string? Href, string Kind)`
      - `record HierarchyExplorerConfig(string DomId, string Shape, HierarchyMode Mode, string HashKey, int Size, bool Labels, ChartMeta Meta)`
      - `record HierarchyExplorerModel(HierarchyExplorerConfig Config, IReadOnlyList<HierarchyNode> Nodes)`
      **`StatusLabel` is new and load-bearing:** the 20.4 probe put the *CSS class* into accessible names ("— done, weight 44"). UX-DR17/UX-DR19 want status as **prose**. Route it through the existing `StatusStyles` class→label functions — `StatusStyles.EpicLabel(cssClass)` for epic nodes, `StatusStyles.StoryLabel(cssClass)` for story nodes (`StatusStyles.cs:88,146`) — the same source `SunburstCompanionList` already uses, so the chart, the twin and the tile grid can never disagree. The three chart-local classes (`noplan`, `followup-open`, `followup-done`, `unplanned`) have no `StatusStyles` label; give them prose in the emitter and use the **same wording the SVG's existing aria labels already use** (`Charts.Sunburst`'s orphan/aggregate labels) rather than inventing a second phrasing — 20.3's live-browser round caught exactly that class of drift ("EpicEpic 19", "Story Story 19.1").
- [x] 2.2 Project the dashboard datasource by **reusing `Charts.SunburstExplorerNodes`'s logic**, not re-walking `EpicsModel`. Preferred: have `SunburstExplorerNodes` remain the single walk and add a thin adapter that maps its output to `HierarchyNode` + applies Finding A (root) and Finding C/D2 (parent = Σ children). **Do not fork the walk** — a second traversal is exactly the drift ADR 0012 exists to end, and `SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew` is the invariant that keeps payload and SVG honest while both are live.
- [x] 2.3 Finding A: synthesize the single root. Id `__project__` (or `root`); label = site title; `Kind = "project"`; `Href` = the instance root href.
- [x] 2.4 Finding C / D2: parent value = Σ of its emitted children, computed bottom-up over the emitted node list. Leaves keep their honest weight. **AC#4 is preserved by construction** because leaf weights still come from `SunburstStoryWeight(..., noPlanWeight)` with `noPlanWeight = SunburstNoPlanStoryWeight(model, geometry)` — do **not** recompute or re-floor them.
- [x] 2.5 Finding B: no `null` in any value. Assert it.
- [x] 2.6 Serialize as an inline island: `<script type="application/json" class="ss-hierarchy-data" id="{DomId}-data">`. **Do NOT reuse the id `sunburst-explorer-data`** — 20.2's island is still live and still read by 20.2's JS block until 20.7. Two instances on one page must not collide (per-`DomId` ids). `JsonSerializer`'s default encoder escapes `< > &`, so the payload is safe inside `<script>` (same reasoning as `Charts.SunburstExplorerIsland`).
- [x] 2.7 Emit the **component config** in the island alongside the nodes (shape, mode, hashKey, size, labels). ADR 0013 §5: the IR carries chart **data + component configuration** — this is that shape, arriving early. It is also what 20.6's replacement assertions will assert on.

### Task 3 — The server-rendered scaffold (AC: #1)

- [x] 3.1 `HierarchyExplorer.Render(model)` returns the **whole** framed block so no call site hand-writes any part of it: `Charts.Framed(meta, body, panelClass)` for the Story 10.2 framing (title + window + framing sentence via `Charts.WhyText`), the shape selector, the chart host, the breadcrumb bar, the live region, the island, and the text twin.
- [x] 3.2 **Selector** (D3): a `.board-tabs` radio pair — the existing idiom (`CodeMapTemplater.AppendShapeToggle`, `GitInsightsTemplater`) — ordered **Sunburst, then Treemap**. That single ordering is the divergence 20.7 AC#1 exists to end; fix it here so 20.7 has one idiom to copy. Radio `id`/`name` suffixed with `DomId` so instances coexist. **Emit it `hidden`** and let the component reveal it: switching a Plotly trace type requires script, so with JS off it would be an inert control — the exact convention `codemap-controls` / `codemap-drill` / `sb-explorer-drill` already follow.
- [x] 3.3 **Chart host:** `<div class="ss-hierarchy" id="{DomId}" data-hierarchy>` — empty at render time. Sized by CSS so a JS-on page does not reflow (Plotly fills its container).
- [x] 3.4 **Breadcrumb bar** above the chart, `hidden` at render, `aria-label="Zoom scope"`; plus `<div class="ss-hierarchy-live sr-only" aria-live="polite">`.
- [x] 3.5 **Text twin** — mandatory (ADR 0013 §2, AC#1). Server-rendered, **complete** (every node's label, prose status, and value), **navigable** (every node's `href` a real resolving `<a>`), **non-color**, nested by `parentId` so the hierarchy itself is legible. Ship it inside `<details class="ss-hierarchy-twin">` (visually collapsed is explicitly acceptable — availability, not on-screen duplication). Component classes are the component's own `.ss-hierarchy-*` family — **do not reuse 20.2's `.sb-explorer-*`**, so 20.7 can delete 20.2's markup and CSS cleanly.
- [x] 3.6 **Honest accounting note for 20.7:** this twin is *new server-rendered bytes* that the spike's −4,787,124 B projection did **not** model (it counted payload at 195.4 B/node only). Measure the twin's real cost on the dashboard and record it in the Dev Agent Record so 20.7's rollout accounting starts from a true number.
- [x] 3.7 New CSS in `src/SpecScribe/assets/specscribe.css` for `.ss-hierarchy*`. **One rule is non-negotiable and has no Plotly attribute:**
      `.ss-hierarchy defs pattern > path { fill: none; }`
      Plotly emits the hatch `<path>` inside every `<pattern>` with a `stroke` but **no `fill`**, so SVG's initial black paints beneath every hatched sector (21 occurrences measured). This single rule took the foreign-color count **1 → 0**.

### Task 4 — The client component in `specscribe.js` (AC: #1, #2, #3)

- [x] 4.1 New guarded block, opt-in on `[data-hierarchy]` — the same shape as `initSunburstExplorers` / `.codemap-view`. `try/catch` per root so a failure degrades to the untouched SVG rather than taking down the page.
- [x] 4.2 **The takeover handshake (D1) — get this exactly right:** the component block must run **before** 20.2's `initSunburstExplorers` in the IIFE, and on **successful mount only** set `data-explorer-ready="1"` on the panel root. That attribute is already 20.2's own skip guard, so:
      *success* → the component owns the chart, 20.2's block stands down, no new guard code needed;
      *failure* (Plotly missing, throw, no island) → the flag is never set, the SVG stays visible, and 20.2's drill-in takes over unchanged.
      No other coordination mechanism is acceptable — anything that hides the SVG before the mount succeeds can leave a page with no chart at all.
- [x] 4.3 **Hiding the SVG is a two-part operation, and the second part is the one that has already bitten this epic.** Setting `display:none` is not enough: **an SVG `<a>` at `display:none` STAYS FOCUSABLE** (unlike HTML) — the 20.2 review found a phantom tab stop this exact way, and only the live browser caught it. On takeover you must *also* set `tabindex="-1"` on **every** `.sunburst a` and `aria-hidden="true"` on the `<svg>`. The test suite structurally cannot see this; check the tab order in the browser.
- [x] 4.4 **Trace construction** — the spike's measured recipe, not a re-derivation:
      - `ids` / `parents` / `labels` / `values` from the island; `branchvalues` matching the payload (Finding C);
      - `marker.colors` — one entry per sector, resolved from the **shipped cascade** via `getComputedStyle` on a throwaway element carrying the real `.sb-*` class. **Never re-type a token value** (AD-7); a token change must move the chart with it;
      - `marker.pattern.shape` per sector for the four non-lifecycle statuses (`noplan`, `followup-open`, `followup-done`, `unplanned`) — this **replaces the shipped stroke-dash channel**, which Plotly's `marker.line` cannot express;
      - `marker.pattern.bgcolor` **explicitly per sector** — left unset, Plotly paints the pattern's backing rect **black** (67 occurrences measured);
      - `insidetextfont` **and** `outsidetextfont` **and** `layout.font.color` — with only `insidetextfont`, the root label alone took Plotly's default `rgb(68,68,68)`;
      - `layout.colorway` / `sunburstcolorway` / `treemapcolorway` / `extendsunburstcolors:false` / `extendtreemapcolors:false` as belt-and-braces (the per-sector array does the real work);
      - `sort: false` so draw order stays the emitter's order.
- [x] 4.5 **Config — privacy and offline are not defaults, they are settings.** `displayModeBar: false`, `plotlyServerURL: ''`, `topojsonURL: ''`, `showTips: false`, `scrollZoom: false`, `doubleClick: false`, `displaylogo: false`, `responsive: true`. **`displayModeBar: false` is load-bearing, not cosmetic:** plotly.js **3.7.0 (2026-07-03)** updated the `sendDataToCloud` modebar button to upload the chart to Plotly Cloud. For a local-first, offline-capable generator that button must never exist. Add a test asserting the script sets `displayModeBar:false` and names no `sendDataToCloud`.
- [x] 4.6 **Labels (D3):** `textinfo:'label'`, `insidetextorientation:'radial'` for the sunburst, `textinfo:'label+value'` for the treemap, and `uniformtext:{ mode:'hide', minsize:9 }` so labels that cannot fit are **hidden rather than shrunk to illegibility** (the probe used `mode:false` — change it). Bump the instance `Size` well above `SunburstGlanceSize` (380) for the dashboard; drive it from config, never a literal in the JS.
- [x] 4.7 **Drill-vs-activate (AC#3) — the grammar, stated once:** Plotly drills on click by default; **cancel it** by returning `false` from `plotly_sunburstclick` / `plotly_treemapclick` (the event carries `nextLevel`, so the component re-applies the level itself). Then apply, per node, exactly one action:
      - **node with children** → primary action (click / Enter / Space) = **DRILL IN**. Its own destination stays reachable via the breadcrumb's `Open page →` link once drilled (20.2's shipped, code-reviewed pattern) **and** via the text twin.
      - **leaf** → primary action = **ACTIVATE**: `navigate` mode follows `href` (Story 9.13 destination contract); `select` mode raises the selection event and does **not** navigate.
      - **Escape / breadcrumb crumb / centre** → **DRILL UP**.
      This is 20.2's reviewed grammar extended by mode — a deliberate reading of ADR 0012 §3's "drill-in is a distinct affordance," chosen because a per-sector secondary control is not expressible in an SVG sector and because it is the affordance already verified with users of this chart. **Flagged for owner verification** (Open Question #1).
- [x] 4.8 **Selection seam — adopt, never mint.** Publish `specscribe:explorer-select` with detail `{ nodeId, label, root }`, `nodeId` null at root scope — **the exact event Story 20.3 already listens for** (`RelatedWork`'s document-level listener). Story 20.3's record is explicit: *"Stories 20.5 and 20.8 must adopt this, not mint a second."* Also publish `data-sb-scope` + `data-tok-<status>` on the panel root exactly as 20.2 does, so the existing pure-CSS drilled-legend filtering keeps working and `Stylesheet_HasDrilledLegendScopeRules` stays satisfied.
      **Ordering hazard, already documented by 20.3:** the explorer block runs earlier in the IIFE, so its first selection event fires before the rail's listener exists — the rail re-syncs on init from `data-sb-scope`. Keep publishing that attribute or you silently break a deep-linked page.
- [x] 4.9 **Do not touch the legend from JS.** `StylesheetTests.Script_DoesNotImplementLegendEmphasis` asserts the script contains none of `emphasize`, `sunburst-legend`, `sb-legend-item` — **including in comments**. Publish state, let CSS decide. Do not weaken that guard; invert the design instead.
- [x] 4.10 **URL-hash deep-linking (UX-DR6):** keep the dashboard instance's existing `sb=` fragment key so links already shared keep working; make the key config-driven for 20.7's other instances. **Reuse 20.2's fragment and history semantics rather than re-deriving them** — never destroy other fragment pairs on zoom-out (it ate in-page anchors like `#glance`), and under the SPA use `replaceState` carrying the router's own `{path, fragment}` keys (a foreign state entry sends the SPA popstate handler down its "unknown state" path and tears the explorer down mid-interaction). Extract these helpers to component scope so 20.7's deletion of 20.2's block does not take them with it. Handle hostile/unknown hashes without throwing.
- [x] 4.11 **SPA re-init:** listen to `specscribe:content-swapped` (detail `{root}`) — the seam the 20.2 review introduced; every content-enhancing block must use it. `Plotly.purge` any stale instance before re-plotting: `innerHTML` swaps detach the graph div while `responsive:true` keeps a window listener, so a naive re-init leaks one per swap.
- [x] 4.12 **Reduced motion (UX-DR18):** Plotly's drill animation is the module constant `CLICK_TRANSITION_TIME: 750` (`src/traces/sunburst/constants.js`) with **no trace-schema attribute** — a config-only search concludes UX-DR18 is unreachable, and it is wrong. The cancel-and-re-apply path in 4.7 means the drill goes through `Plotly.react`, which **never animates**, so it snaps by construction and `prefers-reduced-motion` selects the same instant path a fortiori. Read any duration you do use from the shipped `--motion-*` tokens, never a literal.

### Task 5 — The accessibility layer (AC: #3)

- [x] 5.1 Apply the whole layer **through `plotly_afterplot` only** — Plotly's public post-render event, over its emitted DOM. **No Plotly internal is patched or forked.** That is what makes the spike's UX-DR7 verdict "PASS (configured around)" rather than "forked", and hanging it on the returned promise instead will hang silently in a non-compositing tab.
- [x] 5.2 Per sector: `role="treeitem"`, a non-empty `aria-label` of the form `"<label> — <prose status>, weight <n>"` (use `StatusLabel`, not the CSS class), and a roving `tabindex` with **exactly one `0`**. Container: `role="tree"` + a real accessible name that regenerates on shape switch ("… — sunburst" / "… — treemap").
- [x] 5.3 Keyboard: Arrow/Home/End rove, Enter/Space = the node's one primary action (4.7), Escape = drill up. Announce drill-scope changes through the `aria-live="polite"` region.
- [x] 5.4 **The refinements 20.5 owes** (the spike names them; none changed its verdict): emit `aria-level`, `aria-expanded`, `aria-posinset`/`aria-setsize`; tighten the structure so `role="tree"` and its `treeitem`s nest correctly rather than `tree` sitting on `svg.main-svg` with the items inside `g.slice`; and make Tab/rove order **ring order**, not DOM order.
- [x] 5.5 Re-run the spike's **survival predicate** yourself, mechanically, after each event: *sectors > 0 **and** `role="treeitem"` on every sector **and** a non-empty `aria-label` on every sector **and** exactly one `tabindex="0"`.* Ten audited snapshots, of which **eight are genuine re-render survival tests**: initial render and keyboard reachability are the baseline; drill-in, drill-up, shape switch, drill inside treemap, shape switch back, resize, a bare `Plotly.react` **the component did not initiate**, and `Plotly.relayout` are the eight. That `Plotly.react` step is the adversarial one and it is why the verdict is trustworthy — a layer that survives only its own `redraw()` is a **FAIL**.
- [x] 5.6 **Clamp the roving index on every re-render** — a defect the spike's own code review found in the probe. If the previously-focused sector's index exceeds the new (smaller) sector count after a drill, **no element receives `tabindex="0"` and the chart becomes unreachable by Tab** until an arrow key or a fresh click re-establishes focus. It did not fire in the measured run only because the tested epic's index happened to stay in bounds. Clamp to the new count; do not carry the probe's simplification forward. (Related and already known: 20.2's `setRoving` must clear tabindex on *every* wedge link before re-arming the visible ones.)
- [x] 5.7 **Test the case the spike explicitly did not:** two re-renders in flight at once — a second drill or a resize fired **before** the prior `plotly_afterplot` settles. The spike's harness awaited each step before firing the next and names this race as an untested boundary. A real visitor clicking quickly will hit it.
- [x] 5.8 Note honestly in the Dev Agent Record that, like the spike, this verification is **DOM-level** (`role`, `aria-label`, `tabindex`, live-region mutations) unless you actually run a screen reader. The spike's four a11y PASS verdicts rest on DOM and computed-style introspection, not on an NVDA/VoiceOver/JAWS session — do not imply more coverage than you have.

### Task 6 — Mount on the dashboard (AC: #2, #3, #4)

- [x] 6.1 In `HtmlRenderAdapter.Dashboard.cs` (~:54), replace the bespoke `chart-panel-header-row` + `<h3>Project at a Glance</h3>` with the component's framed scaffold, **keeping `.sunburst-panel` in `panelClass`** — the Story 3.5 legend emphasis CSS is `.sunburst-panel:has(.sb-<status>-item:hover) …` and dropping that class silently kills it (and three `StylesheetTests` assertions).
- [x] 6.2 The retained `Charts.Sunburst(..., nodeIds: true)` SVG, its legend, the `sb-explorer-*` scaffold and 20.2's island all stay exactly as they are (D1). The component's host + island + twin are added beside them.
- [x] 6.3 Mode = **`select`** on the dashboard (ADR 0012 §3: the dashboard drives a details pane). Story 20.3's rail already renders cards only for *selectable scopes* (epics + orphan/unplanned roots), so activating a **story** leaf raises a selection with no card and the rail shows its designed empty state — which is correct, already implemented, and exactly the "make story leaves selectable" narrowing 20.3's record recommends for 20.8.
- [x] 6.4 **Layout (D3's accepted cost):** the labelled explorer needs more width and height than `.explorer-layout`'s left column gives it at mid-size viewports. Raise the stacking breakpoint for this panel (e.g. stack the rail below at ≤1100px instead of ≤900px) so the labelled chart gets full width before it gets cramped, and verify the ≥1100px side-by-side case is genuinely legible. **Owner verifies in the iterate round** — do not silently shrink the labels to preserve the rail.
- [x] 6.5 Asset wiring, mirroring the Mermaid seam exactly: add `HierarchyEngineNeeded` to `AssetManifest` (computed from the rendered body, so the flag cannot disagree with what the page contains — the `MermaidNeeded`/`Mermaid.ContainsBlock` pattern at `HtmlTemplater.cs:201`), and inject `<script src="…plotly-hierarchy.min.js">` in `HtmlRenderAdapter.Render` beside the `page.Assets.MermaidNeeded` branch (`HtmlRenderAdapter.cs:44`) — **after** the body, before `</body>`, on the chrome-level seam so webview/SPA surfaces (which use `page.BodyHtml` directly) never carry it.
- [x] 6.6 **Preserve conditional emission.** Copy the bundle with `CopyEmbeddedAsset` inside a guard keyed on "this site rendered at least one hierarchy chart", exactly like `SiteGenerator.cs:1983-1986` does for prism ("*so a site with no code pages stays byte-identical*"). Wrap it in the same `try/catch` → `GenerationEvent(GenerationOutcome.Error, …)` so a missing embedded resource degrades instead of throwing out of the phase (NFR2). Unconditional emission puts 1.2 MB into every fixture.

### Task 7 — Tests (AC: #1, #2, #3, #4)

- [x] 7.1 New `tests/SpecScribe.Tests/HierarchyExplorerTests.cs`:
      - **exactly one root** in the payload (Finding A), and it is the synthesized project node;
      - **no `null`** anywhere in the serialized values (Finding B);
      - **parent value == Σ children** for every parent, and the emitted `branchvalues` matches that shape (Finding C / D2);
      - **AC#4:** a no-plan story's payload value equals `Charts.SunburstNoPlanStoryWeight(model, geometry)`, a drafted story keeps its honest weight, and a model with nothing drafted falls back to 1 (mirror the existing `SunburstExplorerTests.NoPlanStoryWeight_*` cases rather than inventing new fixtures);
      - **AC#1 anti-drift:** the component's node-id set equals the SVG's `data-node-id` set while both are live (the invariant `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` protects — extend, don't replace);
      - the payload is **valid JSON** with both `config` and `nodes`, and one page hosting two instances emits two distinct island ids;
      - **text twin completeness:** every node in the payload appears in the twin, with a prose status word and a non-empty `href` — the assertion 20.6's audit will build on;
      - empty model → no island, no host, no inert selector.
- [x] 7.2 `StylesheetTests` additions: `.ss-hierarchy defs pattern > path { fill: none; }` is present; `.ss-hierarchy*` rules exist. Keep `Script_DoesNotImplementLegendEmphasis` green (see 4.9).
- [x] 7.3 A script-content test asserting `displayModeBar:false` and the absence of `sendDataToCloud` / any `cdn.plot.ly` or `plotly.com` string in the shipped JS and CSS surfaces (privacy + NFR-3 offline).
- [x] 7.4 `RenderParity` / `RenderSpaParity`: the island and twin survive SPA content capture (extend the existing `SiteGeneratorSpaTests` island-survives-capture test rather than adding a parallel one). **`SunburstExplorerTests.WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks` must still pass unchanged** (D4).
- [x] 7.5 A conditional-emission test: a fixture with no hierarchy chart does not gain the bundle.
- [x] 7.6 **Do not unit-test the JS** — this codebase is SSR-first and has no JS test harness. Task 8 is the verification for everything in Tasks 4 and 5. Say so plainly in the Dev Agent Record rather than implying coverage that does not exist.

### Task 8 — Live-browser verification and the golden fingerprint (AC: #3, #4)

- [x] 8.1 Generate to `SpecScribeOutput/` (never `--output docs/live`) and serve it (`.claude/launch.json` → `specscribe-output`, port 8099). **CLAUDE.md § Verification applies at full force:** three defects this epic shipped were invisible to a 2,000+ test suite and caught only by looking at the rendered page.
- [x] 8.2 Verify, in the browser, with evidence recorded in the Debug Log: the chart renders; **un-drafted stories are typically sized and clickable, not hairlines** (AC#4 — this is a *visual* assertion, measure a sector's real sweep); the selector switches shape in place; drill-in / breadcrumb / drill-up / Escape work; the hash round-trips and Back behaves; the rail follows the selection; **zero console errors**; and the ten-step survival predicate from 5.5 holds.
- [x] 8.3 **Tab through the whole panel** and confirm there is no phantom tab stop on the hidden SVG (4.3). This is the specific defect class the suite cannot see.
- [x] 8.4 **Colorway audit over computed styles, not config.** Build the allowlist at runtime from the shipped `.sb-*` cascade (never type a token value), resolve `url(#pattern)` fills to the colors inside their `<pattern>` defs, and confirm **zero foreign colors** including text fills. The spike's `window.__probe.audit()` in `spike/plotly/probe-src/explorer.js` is the working implementation of this audit — read it before writing your own.
- [x] 8.5 **Take a screenshot.** The spike could not composite a frame and explicitly owes the owner a pixel view of this chart; D3 was chosen without one. If the pane again refuses to composite, say so and fall back to computed-geometry evidence — but try.
- [x] 8.6 **`file://` run — owed by the spike, cheap here.** Open the generated `index.html` directly from disk with networking disabled and confirm the chart renders and makes zero requests. Structural evidence is strong (zero `fetch`, zero ESM imports, zero external origins) but the run itself has never happened.
- [x] 8.7 **Golden fingerprint WILL move** (dashboard markup changes; the fixture has epics, so it gains the island, the twin, and the bundle). Regenerate, **confirm the hash is stable across two repeated runs**, and name **whose concurrent changes it sits on top of** (CLAUDE.md § Concurrent work). **Note for 20.6:** `FingerprintTree` (`SiteGeneratorAdapterTests.cs:1003`) `File.ReadAllText`s *every* file in the output tree, so the 1.2 MB minified bundle now flows through `NormalizeVolatile`'s regexes and into the hash. Recommend folding vendored assets to a `<vendored asset: name, N bytes>` token; 20.6 owns the broader replacement, but record the cost either way.
- [x] 8.8 Run the full suite and report the real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.

---

### Review Findings

*(populated by code-review at epic end — Epic 20's review runs once every story is complete and the owner is satisfied)*

---

## Dev Notes

### Why this story exists, in one paragraph

ADR 0010 §6 already required ONE shared charting engine and **it did not hold**: three concurrent sessions produced three independent arc renderers in `specscribe.js`, three divergent `Treemap | Sunburst` toggles that disagree on ordering, and seven hierarchy entry points in `Charts.cs`. The lesson the SCP drew is that a shared *convention* is easy to defeat and a shared *component* is much harder to accidentally reinvent. This story is that component. Everything else in Epic 20 is preparation for it (20.1–20.4) or consequence of it (20.6–20.8).

### The exact Plotly facts the spike measured — do not re-derive these

| Fact | Value / behavior |
|---|---|
| Version | **plotly.js 3.7.0**, MIT, released 2026-07-03. **Pin it.** A version bump invalidates every number below and must be its own decision. |
| Bundle (standard, 4 traces) | **1,223,515 B** min / **413,449 B** gzip — 12.19× `prism.js`, 25% of the full distribution |
| `--strict` | **7 bytes larger, identical CSP-construct profile. Do not use it.** |
| True trace floor | `core` (contains `scatter`, unremovable) + `heatmap` + `sunburst` + `treemap` + `calendars` — five modules, not the three ADR 0012 §1 names |
| Webview CSP | Renders fully under the **byte-verbatim shipped policy**, header **and** `<meta>` delivered. No `'unsafe-eval'`. `style-src 'unsafe-inline'` already granted and **not even load-bearing** (removing it still renders, losing only hover cosmetics) |
| CSP violations | **Do NOT appear in browser console captures.** A test that greps the console passes while the chart is blank. Ask the DOM. |
| a11y | UX-DR7 **PASS (configured around)** via `plotly_afterplot` — **8/8 re-render events survived**, 10/10 audited snapshots intact; UX-DR16/17 **PASS**; UX-DR18 **PASS (configured around)**. All verdicts are **DOM-level**: no real screen-reader session was run, Tab order was DOM order not ring order, and **overlapping re-renders were never raced** |
| Reduced motion | `CLICK_TRANSITION_TIME: 750` is a **module constant with no schema attribute**; cancel via `return false` from `plotly_<type>click` (event carries `nextLevel`). Measured: **0 `Plotly.animate` calls** on a real click |
| Promises | Resolved **off an animation frame** — `await Plotly.react(…)` never settles in a non-compositing tab |
| Colorways | Zero foreign colors reachable, but only after three fixes: per-sector `pattern.bgcolor`, `outsidetextfont` + `layout.font.color`, and the CSS `defs pattern > path { fill: none }` **that has no Plotly attribute** |
| Non-color channel | `marker.line` has **no `dash`** — `marker.pattern` hatching replaces the shipped stroke-dash and is a stronger channel |
| Net portal delta (20.7) | **−4,787,124 B**. Break-even at **27** of **130** chart-carrying pages — **read as *amortised*, not as "27 pages each clear the cost."** `code-map.html` (−3,493,000 B) and `git-insights.html` (−1,510,735 B) alone sum to **more than the entire portal-wide net delta**, so the other 128 pages net out to roughly break-even or worse in aggregate. The win is concentrated in a few large surfaces. Relevant here because **this story's dashboard mount is one of the many small ones** — expect it to *add* bytes, not save them |
| Packaging | VSIX **+414,279 B (+20.9%)** measured; binary/npx design-level only (Epic 16 is entirely `backlog`) |

### Vendoring recipe (Finding D — the npm package cannot do this)

```sh
cd tools/plotly-vendor
git clone --branch v3.7.0 --depth 1 https://github.com/plotly/plotly.js.git plotly-src
cd plotly-src && npm i --ignore-scripts
npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy
# then copy dist/plotly-specscribe-hierarchy.min.js -> src/SpecScribe/assets/plotly-hierarchy.min.js
```

`plotly-src/` and `node_modules/` are gitignored — throwaway, exactly like `tools/prism-vendor/node_modules`. Rebuild the .NET project so the embedded resource picks up the new file, then re-baseline the golden fingerprint deliberately.

### Architecture compliance

- **ADR 0012** — Plotly is the hierarchy engine, vendored locally, never CDN, `file://`-safe. ONE component is the only route to a sunburst or treemap. `navigate` | `select` mode. Drill-in distinct from activation. Presentation is SpecScribe's tokens, never Plotly's colorways. **ADR 0010 §3 survives** (§7): data computed once at generation time and embedded — never re-derived client-side from live git state or wall-clock "now". **ADR 0010 §4 survives**: FR-10's no-productivity-ranking constraint is unaffected by rendering technology.
- **ADR 0013** — the text twin is the no-JS contract: server-rendered, complete, navigable, non-color; visually collapsed is fine. **This story does not retire any SVG**, so the twin is not yet load-bearing — building it correctly now is what makes 20.6's audit a check rather than a rescue.
- **ADR 0012 §4** — two engine families permitted; a third needs an ADR. Nothing here touches Epic 24's still-open graph-engine question (Story 24.6).
- **ADR 0002 / AD-2** — the payload and config are **host-neutral view-model data**. Build them in the emitter, not in the adapter. Story 6.2's guardrail (a section's HTML comes from its view model, not from inside the adapter) applies: route the block through `DashboardView`, not ad-hoc string-building in `HtmlRenderAdapter.Dashboard.cs` — the 21.1 review had to patch exactly that.
- **ADR 0006 / AD-6** — read-only helpers only; nothing this story ships mutates a planning artifact.
- **NFR-5 as amended by ADR 0013** — JS-off may lose the *visualization*; it must never lose **information** or **navigation**.
- **UX-DR5/6/7/16/17/18/21** — this epic **restores** the originally-specified interactive sunburst UX that SpecScribe had approximated in pure CSS. UX-DR21 ("one primary representation, alternates behind a toggle") is what the single selector idiom makes concrete.
- **FR31** — generation-time determinism: identical output on a from-scratch CI regen.

### Anti-patterns to prevent

1. **Re-walking `EpicsModel` for a second time.** The payload must come from the existing single walk. A second traversal is the drift this whole epic exists to end.
2. **Re-typing a token value in JS.** Resolve `.sb-*` through the live cascade. A hard-coded `#d97706` survives a token change and lies about it.
3. **Minting a second selection event.** `specscribe:explorer-select` exists and 20.3 listens to it.
4. **Minting a second SPA re-init mechanism.** `specscribe:content-swapped` exists.
5. **Copying 20.2's arc math, ring factors, or `SbEpicInnerF`-style geometry into the component.** Plotly owns geometry now. Those constants exist to keep the *SVG* and the *20.2 island* in agreement and die with 20.7.
6. **Hiding the SVG with CSS alone.** SVG `<a>` at `display:none` stays focusable.
7. **Touching a legend node from JS.** Publish state; CSS decides. A guard test enforces it, including in comments.
8. **`branchvalues:'total'` over a payload that is not parent-inclusive.** Warns per parent and draws wrong.
9. **A `null` in `values`.** Renders nothing, silently.
10. **Retiring, deleting, or "cleaning up" any `Charts.cs` hierarchy entry point or `specscribe.js` arc renderer.** They are 20.7's, and they are this story's fallback.
11. **Grepping the console to prove the chart rendered.** CSP failures do not appear there.
12. **`git reset --hard` / `git checkout --` / `git clean`** to tidy the tree. Another session's uncommitted work is in it right now (see below). This has already destroyed real work mid-story.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `specscribe:explorer-select` | `specscribe.js` (20.2's `publishSelection`), consumed by the 20.3 rail | detail `{nodeId, label, root}`; `nodeId` null at root scope |
| `specscribe:content-swapped` | dispatched by `specscribe-spa.js` after every region swap | detail `{root}`; every content-enhancing block must listen |
| `data-explorer-ready` | 20.2's init guard, and this story's **takeover handshake** | set it **only** on successful mount |
| `data-sb-scope` + `data-tok-<status>` | published by the script, consumed by the stylesheet | keeps the pure-CSS drilled legend and its guard test working |
| `Charts.Framed` + `ChartMeta` + `Charts.WhyText` | `Charts.cs:42,153` | the Story 10.2 framing block — never hand-write a "why this matters" sentence |
| `StatusStyles` | the six `--status-*` tokens' single source | the prose status label the twin and accessible names need |
| `AssetManifest` + `MermaidNeeded` pattern | `AssetManifest.cs`, `HtmlRenderAdapter.cs:44`, `HtmlTemplater.cs:201` | flag computed from the rendered body so it cannot disagree with the page |
| `CopyEmbeddedAsset` conditional guard | `SiteGenerator.cs:1983-1986` | emit an asset only where it is used |
| `BmadCommands` | the read-only next-step command surface | 20.8's recommended-prompt button reuses it; do not build a second vocabulary |
| `RelatedWork.NodeText` / `EdgeVerb` | `RelatedWork.cs` | **the** single relationship vocabulary |

### Files being modified — current state

- **`src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` (~:20-90) — UPDATE.** Today: `AppendDashboardSection` emits the tile band, then a `.explorer-layout` grid (present only when `view.RelatedWorkHtml.Length > 0`) containing the `.chart-panel.sunburst-panel[data-explorer]` panel (bespoke header row, `sb-explorer-drill` breadcrumb scaffold, `Charts.Sunburst(..., nodeIds:true)`, `sb-explorer-live`, `Charts.SunburstExplorerIsland`) followed by the 20.3 rail, then the separate "Remaining Work by Epic" panel (`Charts.SunburstCompanionList`). **Preserve:** the `.sunburst-panel` class, the retained SVG and island, the rail, the companion panel, and the `hasRail` conditional grid.
- **`src/SpecScribe/Charts.cs` (4,867 lines) — UPDATE (additively).** `SunburstExplorerNodes` / `SunburstExplorerData` / `SunburstExplorerIsland` live in `SunburstExplorer.cs`; `Sunburst`, the `Sb*F` ring factors, `SunburstGlanceSize`, `SunburstNoPlanStoryWeight`, `SunburstStoryWeight`, `SunburstEpicWeight`, `SunburstCompanionList`, `SunburstLegend`, `Framed`, `ChartMeta`, `WhyText` live here. **Preserve every one of them** — 20.7 deletes the seven entry points, not this story.
- **`src/SpecScribe/SunburstExplorer.cs` (255 lines) — READ, extend carefully.** The 20.2 projector. Its `Edges` slot is **deliberately and permanently `[]`** — 20.3 established that the only translatable edge shape (`Contains`, story→epic) is already stated by `parentId`. Do not "finish" it.
- **`src/SpecScribe/assets/specscribe.js` (2,237 lines) — UPDATE (new block).** `initSunburstExplorers` at :1703, `initSunburstExplorer` at :1716, `publishSelection` at ~:2065, the 20.3 related-pane block at ~:2150. Three arc renderers coexist here (`initOwnershipSunburst` :1208, `renderSunburst` :1570, `initSunburstExplorer` :1716) — **all three are 20.7's to delete.**
- **`src/SpecScribe/assets/specscribe.css` — UPDATE (additively).** `.explorer-layout` at :215, `.sb-explorer-*` at :162-188, `.sb-seg`/`.sb-<status>` at :2972-3030, `.sunburst-legend` at :3032.
- **`src/SpecScribe/SpecScribe.csproj` (~:62) / `ForgeOptions.cs` (~:70) / `SiteGenerator.cs` (~:1983) / `AssetManifest.cs` / `HtmlRenderAdapter.cs` (~:44) — UPDATE**, each a small additive change following the prism/mermaid precedent.
- **`src/SpecScribe/WebviewRenderAdapter.cs` — DO NOT TOUCH** (D4).

*Verify each of these line references before relying on it — another session is editing this tree.*

### Project Structure Notes

New files: `src/SpecScribe/HierarchyExplorer.cs`, `src/SpecScribe/assets/plotly-hierarchy.min.js`, `tools/plotly-vendor/{README.md,package.json,.gitignore,build.mjs}`, `tests/SpecScribe.Tests/HierarchyExplorerTests.cs`. Deleted: `spike/plotly/` (Task 1.6). No new page, no new nav entry, no new NuGet package, no Node in the `specscribe generate` path.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; JS is verified in a live browser (Task 8) and its *content* is asserted by string tests over the shipped asset (`StylesheetTests` is the established pattern for both CSS and JS guards). Golden fingerprint = `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` — regenerate deliberately, confirm stability across two runs, and record provenance.

### Previous story intelligence

**Story 20.4 (the spike, `done` — code-reviewed 2026-07-25, 15 patches, no measured byte figure changed)** — the whole "exact Plotly facts" table above, the four blocking defects, and four hand-offs it named for this story specifically: vendor the standard bundle; hang the a11y layer on `plotly_afterplot`; ship the `defs pattern > path { fill: none }` rule; decide Finding C as *visible geometry* (done: D2). It also owes this story two things it could not do: a **pixel screenshot** and a **`file://` run** (Tasks 8.5, 8.6).

**Its code review added three things that land directly on this story** — read the report as it stands now, not as the dev pass left it: a **sixth finding** (the probe's roving index is not reclamped after a drill shrinks the sector count → no `tabindex="0"`, chart Tab-unreachable — Task 5.6); an explicit **untested boundary** (overlapping re-renders were never raced — Task 5.7); and a correction to how the break-even figure must be read (**amortised**, with the win concentrated in two large pages — so a small surface like the dashboard is expected to *add* bytes).

**Story 20.3 (`review`, owner-redesigned)** — the rail is a **card rail to the right of the sunburst**, cards exist only for **selectable scopes**, and `specscribe:explorer-select` is the named seam this story must adopt. Its open item is a real input here: the rail is **101,435 B of a 472,222 B dashboard (21.5%)**, all data, the direct cost of AC#2 on a 24-epic project — the payload-ceiling question 20.1's review deferred to 20.5/20.6. Your new island + twin add to that. Measure and report; the lever is `RelatedWork.MaxEntriesPerGroup = 12` and it is the owner's to pull.

**Story 20.2 (`done`, 22 review patches)** — the payload/id contract, and five durable facts that are all live hazards here: `SVGAElement` has **no `.click()`** (use `location.href = a.getAttribute("href")`); the `specscribe:content-swapped` seam; `kind` ≠ `ring`; **an SVG `<a>` at `display:none` stays focusable**; legend presentation stays pure CSS. Story ids come from `### Story N.M:` headings with **no dedupe anywhere**, so duplicate ids are reachable from authoring input — the projector keeps the first; your component must not assume uniqueness either.

**Story 20.1 (`done`)** — its degrade contract is **superseded** by ADR 0013 and its JS-budget question **answered** by ADR 0012. Its §1 edge-join rule was **wrong** (disjoint id schemes). Read the ADRs as the authority, not the spike.

**Owner workflow (`CLAUDE.md`)** — the post-implementation round where the owner verifies rendered behavior and comments extensively is the **designed gate**, not rework. D3 in particular will get commentary; expect it and leave the layout easy to tune.

### Git intelligence summary

Recent commits (`9369ca4` ← `5a96f71` ← `f9b52bd` ← `8db18aa`) each bundle several stories — Epic 20 work landed alongside Epic 5 CLI hardening and Epic 22/23/24 seeding. That is structural: code review runs at epic end, so **scope any later review by this story's own File List and declared symbols, never by a commit range.**

**A concurrent session is editing this tree right now** (`git status` at create-story: `20-3-…md`, `20-4-…md`, `5-1-…md`, `deferred-work.md` all modified). Consequences per CLAUDE.md § Concurrent work: grep-verify every symbol you add before relying on it (a `Charts.cs` edit has silently vanished this way); expect the build to be transiently broken by someone else's rename (Story 20.3 hit this twice) and **wait rather than reset**; expect the golden fingerprint to move under you and confirm stability across two runs before locking it.

### Latest technical information (researched 2026-07-25)

**plotly.js 3.7.0** (2026-07-03) is current; nothing newer surfaced. Two changelog items matter here:

1. **`sendDataToCloud` now uploads charts to Plotly Cloud.** The endpoint is reportedly not yet functional, but the button's intent is an outbound upload of the user's data. For a local-first generator this is a hard requirement, not a preference: **`displayModeBar: false`**, plus `plotlyServerURL: ''`, plus a test that the string `sendDataToCloud` never ships enabled. (Task 4.5, 7.3.)
2. A fix for stale `scattergl` error bars — irrelevant; `scattergl` is not in this bundle.

Also relevant: the bundle contains **four** `plot.ly` references, all identified and none exercised — the `topojsonURL` geo default (geo not in the build), the modebar logo anchor (removed by `displaylogo:false`), and two error strings. The single `XMLHttpRequest` is d3's `d3.xhr`, reachable only from the topojson path.

**Sources:** [plotly.js releases](https://github.com/plotly/plotly.js/releases) · [plotly.js CHANGELOG](https://github.com/plotly/plotly.js/blob/master/CHANGELOG.md) · [plotly.js on npm](https://www.npmjs.com/package/plotly.js?activeTab=readme)

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.5] — the four ACs verbatim, plus the seven-surface rollout inventory
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §2 component contract, §3 mode contract, §4 engine-family boundary, §6 tokens, §7 determinism, and the **Story 20.4 addendum** (corrections, the four data-contract constraints, the pattern-fill CSS rule)
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 twin contract, §3 the per-surface live JS-off gate, §5 IR carries data + config, §6 fingerprint replacement
- [Source: `_bmad-output/implementation-artifacts/20-4-spike-report.md`] — §3 bundle/size/packaging, §4 CSP, §5 a11y, §6 tokens, **§7 the four defects**, §8 supply chain, **§11 what it hands to this story**
- [Source: `_bmad-output/implementation-artifacts/20-3-related-work-side-pane-on-selection.md`] — the `specscribe:explorer-select` seam, the card-rail redesign, the payload-ceiling open item
- [Source: `_bmad-output/implementation-artifacts/20-2-zoomable-drill-in-sunburst-navigation.md`] — the payload/id contract and the 22 review patches
- [Source: `CLAUDE.md`] — § Concurrent work on shared `main`, § Verification, § Decision records
- Code: `SunburstExplorer.cs`, `Charts.cs:42,153,345,351,361,409,439,648,783`, `HtmlRenderAdapter.Dashboard.cs:54`, `HtmlRenderAdapter.cs:44`, `SiteGenerator.cs:1983`, `WebviewRenderAdapter.cs:79,113`, `specscribe.js:1703,1716,2065,2150`, `specscribe.css:215,2972`, `SpecScribe.csproj:62`, `ForgeOptions.cs:70`, `StylesheetTests.cs:452,466`, `SunburstExplorerTests.cs`, `spike/plotly/probe-src/explorer.js`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Drill-vs-activate grammar (Task 4.7).** Recommended and implemented as specified: a node with children drills, a leaf activates, its own destination stays reachable via the breadcrumb's `Open page →` link and the twin. This is 20.2's reviewed grammar extended by mode. The alternative — every node activates and drill is a separate control — has no expressible per-sector affordance in SVG.
2. **Should the sunburst or the treemap be the dashboard's default shape?** Recommended: **sunburst** (today's shape, and the selector is ordered Sunburst-first).
3. **The payload ceiling.** 20.3's rail is already 21.5% of the dashboard and this story adds an island plus a twin. Recommended: measure and report in this story, decide the ceiling in 20.6 where the fingerprint replacement makes the server-rendered surface explicit. The lever is `RelatedWork.MaxEntriesPerGroup`.
4. **Does the twin supersede `Charts.SunburstCompanionList` on the dashboard?** Recommended: **not here.** Both stand in this story; 20.6's audit is the right place to decide whether the "Remaining Work by Epic" panel *is* the dashboard's twin and the component's twin can be `sr-only` there.

---

## Dev Agent Record

### Agent Model Used

claude-opus-5 (dev-story, 2026-07-25)

### Debug Log References

**Live-browser session** — `.claude/launch.json` → `specscribe-output`, port 8099, over a real
`--output SpecScribeOutput` run (393 pages, 0 errors). All evidence below is measured in that session, not inferred.

**Vendoring (Task 1).** `tools/plotly-vendor/build.mjs --no-fetch` ran end-to-end against a real v3.7.0 clone and
produced **exactly 1,223,515 B** — the spike's number to the byte. Resolved trace list was
`heatmap, scatter, sunburst, treemap`, confirming `scatter` is unremovable. One deviation from the story's literal
recipe, forced by the platform: Node refuses to spawn `npm.cmd` without a shell since the CVE-2024-27980
mitigation (`spawnSync npm.cmd EINVAL`), so npm invocations pass `shell: true` on Windows with the argv-injection
hazard neutralised by every argument being a file-local constant. Documented at the call site.

**Payload (Task 2) — the four blocking findings, verified on the real 134-node dashboard payload:**
one root (`__project__`) · zero nulls in `values` · `parent == Σ children` for **every** parent · emitted
`branchvalues: "total"` matching that shape. Owner decision D2's stated cost is visible and correct: `epic-1` now
carries **50** (its children's sum) where `SunburstEpicWeight` gives 42.

**Mount + a11y (Tasks 4–6), initial page state:** Plotly 3.7.0, 134 sectors, `role="treeitem"` on 134/134,
non-empty prose `aria-label` on 134/134, exactly **one** `tabindex="0"`, host `role="tree"` named
"Project at a Glance — sunburst". Server SVG `display:none` **and** `aria-hidden="true"` **and** 0 of its `<a>`
elements left tabbable (Task 8.3 — the phantom-tab-stop class the suite structurally cannot see). Zero console
errors, zero window `error` events across every run below.

**Survival predicate (Task 5.5 / 8.2) — 10 snapshots, all INTACT; 8/8 re-render events reapplied the layer:**
initial · keyboard reachability (focus lands, arrows move) · drill-in via Enter · drill-up via Escape · shape
switch → treemap · drill inside treemap · shape switch back → sunburst · resize · **a bare `Plotly.react` the
component did not initiate** · `Plotly.relayout`. The clamp (Task 5.6) is what carries the drill: sectors go
134 → 7 and `tabindex="0"` count stays exactly 1.

**Overlapping re-renders (Task 5.7 — the boundary the spike explicitly never tested).** Four adversarial cases,
each with NO await between the triggering events: two drills back-to-back · a resize storm fired during a drill ·
a shape switch fired during a drill · keyboard reachability re-checked after all of it. **All four INTACT, zero
errors**, focus still lands and arrows still move afterwards.

**Colorway audit (Task 8.4) — allowlist built at runtime from the shipped `.sb-*` cascade, no token value typed.**
`url(#pattern)` fills resolved into their `<pattern>` defs. Result: **zero foreign colors**, including text fills
(134 text nodes, all `rgb(92,101,112)` = the shipped ink token). 57 patterned sectors across 57 pattern defs, none
painting black — the `.ss-hierarchy defs pattern > path { fill: none; }` rule doing its job.

**AC #4, as a *visual* assertion over real geometry (Task 8.2).** Every un-drafted story carries value **15** —
`Charts.SunburstNoPlanStoryWeight` for this model — and their rendered sectors measure a **median bounding area of
3,926 px² and a minimum of 2,123 px² (42×24 px)**. Drafted story sectors run down to 287 px² (23×3 px) and have a
*lower* median (3,702 px²). So the bump does exactly what the owner asked: un-drafted work is typically sized and
comfortably clickable, never a hairline, and no real wedge was shrunk to achieve it.

**`file://` run (Task 8.6 — owed by the spike, never previously performed).** Opened the generated `index.html`
directly from disk: chart renders fully, Plotly 3.7.0, 134 sectors, a11y layer intact, SVG hidden, and
**0 network requests / 0 non-`file:` resource entries**. Structural evidence is now an actual observation.

**JS-off degrade**, checked in a genuinely script-blocked sandbox iframe (`sandbox="allow-same-origin"`, no
`allow-scripts`): Plotly never runs · host stays `display:none` at **0 px** (no chart-sized blank box) · selector
and breadcrumb stay `[hidden]` (no inert controls) · the server SVG is fully visible with all 133 wedges and 133
tabbable links · the twin is **complete (134 items) and fully navigable (134/134 links carry an href)** with prose
status.

**Takeover handshake**, re-verified after the final build: `data-explorer-ready="1"` set, 20.2's centre control
never injected, 20.2's drill bar hidden, 20.2's island still present as inert data, swatch strip still displayed.

**Screenshot (Task 8.5) — NOT obtained, and it is still owed.** Four attempts; the Browser pane refused to
composite frames in this session (`the Browser pane is not displayed`), the same environmental limit the 20.4
spike hit. Everything above is computed-geometry and computed-style evidence over the live DOM. **The owner has
still never seen a pixel of this chart**, and D3 was chosen without one.

**Golden fingerprint (Task 8.7).** `9288bf55…` → **`9dad8c5b53148d883296fc68168635e565e8090db5284282a88060a5a3f844d2`**,
**confirmed identical across two repeated runs**. PROVENANCE: this tree also carries **another session's staged
Story 5.2 / ADR 0014 settings-folder work** (`SettingsStore.cs`, `SettingsResolver.cs`, `HowToReadTemplater.cs`,
`Commands.cs`, `ConsoleUi.cs`, `docs/adrs/0014-*`, plus their tests). Those touch rendered output, so this constant
sits on top of them — not on this story's files alone.

**Full suite: 2387 passed / 3 skipped / 0 failed** (1 m 41 s). No git-fixture flake appeared in the final run.

### Completion Notes List

**Two defects found by live verification that the suite could not see**, both fixed:

1. **Drill-up landed on the synthesized root *as a scope*.** Escape from an epic set `data-sb-scope="__project__"`
   and `#sb=__project__`, which would send Story 20.3's rail hunting for a card that cannot exist and put a
   meaningless id into a shareable link. The tree root is not a scope; "everything" has exactly one representation
   and it is the absence of one. Normalized in `drillTo` **and** in `scopeFromHash`.
2. **The breadcrumb rendered the project name twice** ("SpecScribe › SpecScribe › Epic 1"), because the ancestor
   walk passed through the root that the "top" crumb already represents.

**One defect found by the test suite, and it was a real one, not a fixture artifact.**
`ConcurrentRegenerations_…_ConvergeToCoherentOutput` failed *in isolation*: a topology change during a watch
session triggers a full rebuild that wipes and recreates the output root, deleting an asset the generator's
in-memory "already copied" flag still claimed. The freshly written `index.html` then pointed at a 1.2 MB script no
longer on disk — the chart silently vanishes mid-session and only a hard regenerate brings it back.
`EnsureHierarchyEngine` now treats the flag as an optimization and the **disk as the truth**. That test's
assertion message was also changed to report the symmetric difference by name; xUnit's set-differs output
truncates both sides after a few entries, which on a ~40-file portal named nothing.

**One deliberate addition beyond the task text, because D3 was otherwise not delivered.** Task 4.6 specifies
`uniformtext: { mode: 'hide', minsize: 9 }`, which draws every label at ONE size — the smallest that fits any
sector — and hides the rest. With the full titles the payload carries, that produced **2 labelled sectors out of 7
when drilled** and 8 of 134 at root: a "Labelled explorer" that hides five labels in seven is not one. The payload
now carries a `shortLabel` (identifier only — "Epic 1", "Story 20.5", "10 done") which is what gets **drawn**,
while the full title remains the hover heading, the twin's link text, and the accessible name. Measured after:
**7/7 labelled when drilled, 39/134 at root**, and `aria-label` values verified still full titles. Flagged for the
owner as a visible change to what the chart says.

**Honest accounting for Story 20.7's rollout (Task 3.6).** The spike's −4,787,124 B projection counted payload at
**195.4 B/node only** and modelled no twin. On the real dashboard: island **31,404 B** (234.4 B/node — higher than
the spike assumed, because of the added `statusLabel` and `shortLabel`), text twin **24,168 B** (180.4 B/node,
entirely unmodelled). Together **55,572 B of new server-rendered bytes, 9.8% of a 567,074 B dashboard**, which grew
**+10.9%**. Read plainly: **20.7 should budget roughly double the per-node server cost the spike assumed**, and this
mount behaves exactly as the amortised reading predicts — a small surface *adds* bytes. Story 20.3's rail remains
the larger line item and `RelatedWork.MaxEntriesPerGroup` is still the owner's lever (open question #3 — measured
here, decided in 20.6 as recommended).

**One recommendation from Task 8.7 carried out rather than deferred**, because it was a correctness risk and not
only a cost: `FingerprintTree` read *every* emitted file through `NormalizeVolatile`, whose regexes are written for
rendered markup and would now be pattern-matching 1.2 MB of minified JavaScript — a coincidental match there moves
the golden constant with no rendering change behind it. Vendored assets now fold to a
`<vendored asset: name, N bytes, sha256:…>` identity token (name/length/**and content hash**, so a same-size
re-vendor still flips the hash). Story 20.6 still owns the broader replacement.

**Scope honestly reported — what this story did NOT do**, each belonging to a named later story: no SVG retired, no
`Charts.cs` entry point or JS arc renderer deleted, no other call site converted, `WebviewRenderAdapter.cs`
untouched (D4 — `SunburstExplorerTests.WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks` passes
unchanged), no golden-fingerprint *replacement* assertions, no details pane, and
`Charts.SunburstCompanionList` + the 20.3 rail both left standing.

**Coverage claimed, precisely (Task 7.6, 5.8).** The JS is **not** unit-tested — this codebase is SSR-first with no
JS harness — so Tasks 4 and 5 rest entirely on the live-browser session above plus string guards over the shipped
asset. And, exactly as with the spike, **every accessibility verdict here is DOM-level** (`role`, `aria-label`,
`tabindex`, `aria-level`/`posinset`/`setsize`, live-region mutations, computed styles). **No screen reader was
run** — no NVDA, VoiceOver or JAWS session took place. Do not read these results as more than they are.

**Twin left collapsed by default**, unlike Story 20.3's pane which its review forced `open`. The cases differ: ADR
0013 §2 makes availability the contract rather than on-screen duplication, and this story keeps the server SVG, so
the twin is a second reading rather than the only one. Worth revisiting in 20.6 when the SVG retires.

### File List

**New**
- `src/SpecScribe/HierarchyExplorer.cs` — the component: mode/node/config/model records, the projection (Findings
  A/B/C), the framed scaffold, the island, the text twin
- `src/SpecScribe/assets/plotly-hierarchy.min.js` — vendored plotly.js 3.7.0 custom bundle (1,223,515 B)
- `tools/plotly-vendor/{README.md,package.json,.gitignore,build.mjs}` — the hand-run vendoring tool + supply-chain record
- `tests/SpecScribe.Tests/HierarchyExplorerTests.cs` — 19 tests

**Modified**
- `src/SpecScribe/Charts.cs` — `SunburstLocalStatusLabel` (one source for the four chart-local status words, now
  read by the swatch strip too); `ChartMetric.WorkHierarchy` + its `WhyText`; `Framed(…, panelAttributes)`
- `src/SpecScribe/DashboardView.cs` — `HierarchyExplorerHtml`
- `src/SpecScribe/DashboardViewBuilder.cs` — `BuildHierarchyExplorerHtml` + `RetainedSunburstHtml` (20.7 deletes both)
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — the panel is now the component's framed block
- `src/SpecScribe/HtmlRenderAdapter.cs` — chrome-level `<script src>` injection beside the Mermaid branch
- `src/SpecScribe/HtmlTemplater.cs` — `HierarchyEngineNeeded` computed from the rendered body
- `src/SpecScribe/AssetManifest.cs` — `HierarchyEngineNeeded`
- `src/SpecScribe/SiteGenerator.cs` — `EnsureHierarchyEngine` conditional copy + `_pendingAssetEvents` drain
- `src/SpecScribe/ForgeOptions.cs` — `HierarchyEngineScriptName`
- `src/SpecScribe/SpecScribe.csproj` — the embedded resource
- `src/SpecScribe/assets/specscribe.js` — the component block (runs before 20.2's, takeover handshake)
- `src/SpecScribe/assets/specscribe.css` — `.ss-hierarchy*` family, the pattern-fill rule, `.explorer-layout-labelled`
- `tests/SpecScribe.Tests/StylesheetTests.cs` — pattern-fill rule, `.ss-hierarchy*`, modebar/no-CDN, adopted-seams guards
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — island+twin SPA capture; conditional-emission test
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — inventory + regenerated fingerprint + vendored-asset fold
- `tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs` — named symmetric-difference failure message
- `.claude/launch.json` — removed the four `plotly-csp-*` spike entries

**Deleted**
- `spike/plotly/**` (Task 1.6 — the durable outputs are `20-4-spike-report.md` and the ADR 0012 addendum)

*Not this story's:* `epics.md`, `Commands.cs`, `ConsoleUi.cs`, `SettingsResolver.cs`, `SettingsStore.cs`,
`HowToReadTemplater.cs`, `docs/adrs/0014-*` and their tests are a concurrent session's Story 5.2 / ADR 0014 work,
present in this tree but not touched here.

---

## Change Log

- 2026-07-25 — Story 20.5 drafted (create-story). Comprehensive context assembled from ADR 0012 (+ its Story 20.4 addendum), ADR 0013, the 576-line 20.4 spike report, the shipped 20.2/20.3 code and their review records, and the live source. **Four owner decisions elicited and locked** (per CLAUDE.md's create-story visual-intent rule and the spike's explicit §11 hand-off): **D1** the component mounts on the dashboard with the server SVG kept underneath as a real fallback (no SVG retires — ADR 0013 §3's gate and 20.6's fingerprint replacement stay intact, and 20.7's deletion becomes a clean subtraction); **D2** ring geometry resolves Finding C as *children win* — a parent's value is the exact sum of its drawn children, accepting that some epic sweeps shift because today's `SunburstEpicWeight` counts epic-level follow-up peers that are not drawn as children (14 of 25 parents disagree); **D3** the visual direction is **"Labelled explorer"** — larger radius, in-sector labels where they fit, breadcrumb bar, labelled treemap tiles — with the accepted cost that it competes with Story 20.3's card rail inside `.explorer-layout` (Task 6.4 carries the breakpoint plan, the owner verifies); **D4** the webview decision the spike assigned to this story is **deferred to 20.7**, which owns `RenderParity` and where the ADR 0005 CSP amendment lands jointly with Story 23.4 — `WebviewRenderAdapter.cs` is untouched. The spike's **four blocking data-contract defects** are promoted to a read-first section with fixes (one synthesized root; no `null` in `values`; parent/`branchvalues` consistency; the clone-based vendoring recipe the npm package cannot provide) plus the fifth environmental one (Plotly resolves promises off an animation frame — hang everything on `plotly_afterplot`). A **takeover handshake** is specified that reuses 20.2's own `data-explorer-ready` guard so a failed mount can never leave a page with no chart, paired with the explicit reminder that **an SVG `<a>` at `display:none` stays focusable** — the phantom-tab-stop defect the 20.2 review found and the suite structurally cannot see. Web research surfaced one fact that changes a requirement rather than confirming one: plotly.js **3.7.0**'s `sendDataToCloud` modebar button now uploads charts to Plotly Cloud, making `displayModeBar: false` a privacy/NFR-3 requirement with its own test rather than a cosmetic default. Twelve anti-patterns and a nine-row seams table are recorded so the dev adopts existing contracts (`specscribe:explorer-select`, `specscribe:content-swapped`, `Charts.Framed`/`ChartMeta`/`WhyText`, `StatusStyles`, the `MermaidNeeded` asset-flag pattern, `SiteGenerator.cs:1983`'s conditional-emission guard) instead of minting parallel ones. Honest costs recorded rather than buried: the text twin is new server-rendered bytes the spike's −4.8 MB projection did not model; the 1.2 MB vendored bundle now flows through `FingerprintTree`'s `File.ReadAllText`; and the golden fingerprint will move on a tree a concurrent session is actively editing (`20-3`, `20-4`, `5-1`, `deferred-work.md` modified at draft time). D1–D4 are recorded in `epics.md` under Story 20.5 in the same change, per CLAUDE.md's rule that a decision must not live in only one artifact. baseline_commit `9369ca4`.
- 2026-07-25 — Story 20.4's **code review completed mid-draft** (status → `done`, 15 patches, no measured byte figure changed) and its three story-relevant additions were folded in rather than left to be rediscovered: a **sixth finding** — the probe's roving `focusIndex` is not reclamped after a drill shrinks the sector count, so no element receives `tabindex="0"` and the chart becomes **Tab-unreachable** (it did not fire only because the tested epic's index happened to stay in bounds) → new Task 5.6; an **explicit untested boundary** — overlapping re-renders were never raced, because the harness awaited each step before firing the next → new Task 5.7; and two precision corrections now carried in the facts table — UX-DR7 is **8/8 re-render events survived** (10/10 audited snapshots) rather than the looser "10/10 survival", all a11y verdicts are **DOM-level** with no real screen-reader session → new Task 5.8, and the **break-even figure is amortised, not a ranked list**: `code-map.html` and `git-insights.html` alone exceed the whole portal-wide net delta, so the remaining 128 pages net out to roughly break-even or worse — which means **this story's own dashboard mount is expected to add bytes, not save them**, and saying otherwise would be the easiest available error.
- 2026-07-25 — **Story 20.5 implemented (dev-story) → `review`.** All 60 subtasks complete; full suite **2387 passed / 3 skipped / 0 failed**. The Hierarchy Explorer ships as `HierarchyExplorer.cs` (host-neutral emitter + scaffold + text twin), a guarded `specscribe.js` block, and the `.ss-hierarchy*` CSS family, mounted on the dashboard only with the server SVG kept live beneath it (D1). All four blocking data-contract defects are fixed **and asserted**: one synthesized root, no `null` in `values`, `parent == Σ children` (D2 — `epic-1` now reads 50 against `SunburstEpicWeight`'s 42), and an emitted `branchvalues: "total"` that matches the payload's actual shape. Plotly 3.7.0 was vendored by a real end-to-end `tools/plotly-vendor/build.mjs` run producing **exactly 1,223,515 B**; the npm invocation needed `shell: true` on Windows (Node refuses to spawn `npm.cmd` otherwise since CVE-2024-27980), the one forced deviation from the story's literal recipe. **Live-browser verification carried the story, and found two defects the 2,387-test suite structurally could not see**: drill-up landed on the synthesized root *as a scope* (`#sb=__project__`, which would have sent 20.3's rail after a card that cannot exist), and the breadcrumb printed the project name twice. **The suite found a third, and it was real, not a fixture artifact**: a watch-session topology change wipes and recreates the output root, deleting an asset the generator's in-memory flag still claimed — leaving `index.html` pointing at a 1.2 MB script no longer on disk, so the chart silently vanished mid-session; `EnsureHierarchyEngine` now treats the disk as the truth. **One deliberate addition beyond the task text**: Task 4.6's `uniformtext` draws every label at one size and hides the rest, which with the payload's full titles left **2 of 7 sectors labelled when drilled** — not a "Labelled explorer" at all. The payload now carries a `shortLabel` that is what gets drawn, with the full title kept as the hover heading, the twin's link text and the accessible name; measured after, **7/7 drilled and 39/134 at root**. Evidence recorded rather than asserted: 10/10 survival snapshots INTACT with **8/8 re-render events** surviving (including a bare foreign `Plotly.react`), the spike's **untested overlapping-re-render boundary now tested** across four adversarial cases (all INTACT, zero errors), **zero foreign colours** including text fills over a runtime-built allowlist, **zero phantom tab stops** on the hidden SVG, the **`file://` run the spike owed** (renders fully, **0 network requests**), and AC #4 verified as a *visual* claim — un-drafted stories at value 15 render at a **median 3,926 px² / minimum 2,123 px²**, above drafted stories' median, never hairlines. Honest costs recorded rather than buried: the twin is **24,168 B** of server-rendered bytes the spike's −4.8 MB projection **did not model at all**, the island is 31,404 B, and the dashboard grew **+10.9%** — so **20.7 should budget roughly double the spike's 195.4 B/node**, and this small mount adds bytes exactly as the amortised reading predicts. Task 8.7's recommendation was carried out rather than deferred because it was a correctness risk, not only a cost: `FingerprintTree` now folds a vendored asset to a name/length/**sha256** identity token instead of pushing 1.2 MB of minified JavaScript through markup-shaped regexes. Golden fingerprint `9288bf55` → **`9dad8c5b`**, stable across two repeated runs, captured on a tree that also carries **another session's staged Story 5.2 / ADR 0014 settings work** — the constant sits on top of theirs, not on this story's files alone. **Two things are owed and neither is glossed**: the owner has **still never seen a pixel of this chart** (four screenshot attempts, the Browser pane refused to composite — the same environmental limit that beat the spike; everything here is computed-geometry and computed-style evidence), and **every a11y verdict is DOM-level with no screen-reader session behind it**. Nothing retired: no SVG, no `Charts.cs` entry point, no arc renderer, no other call site, and `WebviewRenderAdapter.cs` untouched (D4). baseline_commit `92fa581`.
