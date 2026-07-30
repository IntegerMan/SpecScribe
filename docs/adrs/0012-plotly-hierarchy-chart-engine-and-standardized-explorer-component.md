# ADR 0012: Plotly.js as the Hierarchy-Chart Engine, and One Standardized Hierarchy Explorer Component

**Status:** Accepted (owner-ratified 2026-07-24, via correct-course; the engine-adoption spike is a validation step whose measurements are recorded back as an addendum — see "Spike validation" below)
**Date:** 2026-07-24
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0010 — Client-Side Charting JS for Opt-In Deep-Analytics Surfaces](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) (**supersedes §1 and §6**); [ADR 0005 — VS Code Webview Runtime](0005-vs-code-webview-runtime-and-packaging.md) (**amends** the CSP / "no scripts in the body" clause); [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (its necessary companion); [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) (AD-2 view models); Epic 20 (Interactive Project Explorer), Epic 24 (Change-Coupling Graphs, FR40), Epic 7 (Code Map / ownership / freshness), Epic 21 (Impact Map); memory `adr-consultation-gap-three-arc-renderers`

## Context

SpecScribe renders hierarchical data as sunbursts and treemaps on most of its analytical surfaces. That family has grown by accretion, and by 2026-07-23 it had produced three *independent* implementations of the same idea at three different layers:

**1. Seven server-side entry points** in `Charts.cs` (4,777 lines): `Sunburst`, `EpicSunburst`, `TaskSunburst`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeTreemap`, `CodeOwnershipTreemap`.

**2. Three independent client-side arc renderers** in `assets/specscribe.js` (1,961 lines) — `initOwnershipSunburst` (Story 7.11), `renderSunburst`/`arcPath` (Story 21.3), `initSunburstExplorer`/`annular`/`fullRing` (Story 20.2) — written by three concurrent sessions.

**3. Three independently built "Treemap | Sunburst" toggles** that already disagree with one another:

| Surface | Call site | Order | ID scheme |
|---|---|---|---|
| Code Map | `CodeMapTemplater.cs:182` | Treemap, Sunburst | per-variant generated |
| Git Insights (ownership) | `GitInsightsTemplater.cs:160` | **Sunburst, Treemap** | `ownership-view-*` |
| Impact Map | `ImpactMapTemplater.cs:126` | Treemap, Sunburst | `impact-view-*` |

ADR 0010 §6 already required "**ONE** shared engine/module across 7.11, 7.12, and any future opt-in analytics surface, not independently reinvented per story." **That rule did not hold.** The Epics 19+21 joint retrospective verified the violation and seated Story 20.4 to fix it by *extracting* shared arc math from the three hand-rolled renderers.

Two things make extraction the wrong remedy now:

- Extraction consolidates the *math* but leaves SpecScribe owning a bespoke charting engine — hover, drill-in, breadcrumb, transitions, hit-testing, and legend behavior all remain hand-written per surface, which is where the drift actually happened.
- The owner has already named the target twice. The Epic 20 epic note records the 2026-07-22 request verbatim — *"click and drill into a directory and filter down to that level… You can do this via Plotly and it's amazing"* — flagged there as needing "its own dependency-budget decision at spike time, not an assumed yes." This ADR is that decision.

The owner's 2026-07-23 direction is broader than the engine: **one standardized component** bundling a sunburst and a treemap over a single datasource behind a standard selector, used **everywhere**, with a **mode** governing what clicking a node does — so that site-wide changes and new features land in one place instead of seven.

There is a further constraint the codebase makes unavoidable: **UX-DR5, UX-DR6, and UX-DR7 originally specified exactly this** — an interactive multi-ring sunburst with hover tooltips, drill-down by epic and story, breadcrumb drill-up, URL-hash deep-linking, and Enter/Space/Escape keyboard drill. SpecScribe deliberately diverged to pure CSS (memory `charting-is-pure-svg-no-js`). Adopting a real charting engine does not invent new UX requirements; it **restores the originally-specified ones**.

## Decision

**1. Plotly.js is SpecScribe's hierarchy-chart engine.** It covers the `sunburst`, `treemap`, `icicle`, and `heatmap` trace families. It is **vendored locally and never loaded from a CDN** — the generated portal must keep working offline and from `file://` (NFR-3 local-first, and the portal is routinely opened as loose files).

**2. One component is the only path to a hierarchy chart.** Working name: **Hierarchy Explorer**. After the rollout, no page constructs a sunburst or treemap by any other route. Its contract:

- **One datasource per instance** — the hierarchical node shape Story 20.2 already committed (`id`, `parentId`, `label`, `value`/weight, `statusClass`, `href`, `kind`), embedded once at generation time. Both shapes read the *same* payload; switching shapes never re-derives or re-counts anything.
- **One selector** — a single ordering and control idiom site-wide, replacing the three divergent board-tab toggles above. This is UX-DR21 ("one primary representation per dataset, alternates demoted behind a toggle") made concrete rather than re-improvised per surface.
- **One framing block** — Story 10.2 legend, analysis window, and framing sentence, supplied by the component, not hand-written per call site.
- **One text twin** — mandatory, per [ADR 0013](0013-text-twin-is-the-no-js-contract.md).

**3. Node activation is governed by an explicit per-instance `mode`.** Exactly two are defined:

- **`navigate`** — activating a node follows its `href`, honoring the Story 9.13 destination contract (leaf → detail page, group → generated filtered list page). This is the behavior every current surface has.
- **`select`** — activating a node raises a selection event and **does not navigate**; other regions of the page bind to it. The dashboard uses this to drive a details pane.

Two rules make the modes safe rather than surprising:

- **Drill-in is a distinct affordance from activation.** Plotly's sunburst and treemap **drill in on click by default**; the component must intercept `plotly_sunburstclick` / `plotly_treemapclick` and suppress the default where the mode requires it. A node must never silently do two things at once.
- **`select` mode must not strand keyboard or assistive-technology users.** A selection-driven pane is a live region, and the selected node's own destination must remain reachable — the details pane carries the "view more" link precisely so `select` never removes navigation.

**4. Engine-family boundary — this supersedes ADR 0010 §6's single-engine rule.** Plotly owns **hierarchical** charts. It has **no force-directed layout and no chord/ribbon trace**, so it cannot serve Epic 24's Stories 24.2, 24.3, and 24.4. Rather than pretend one engine covers everything or let the tool dictate the product:

- Plotly is the engine for hierarchy (and, where it fits, the 24.5 adjacency matrix as a `heatmap` trace).
- **Epic 24's graph engine is a named open question**, deferred to Epic 24's own spike. It may be Plotly `scatter` with a hand-rolled layout, a second library, or bespoke — decided on evidence, not assumed here.
- **Two engine families are permitted. A third requires an ADR.** Every family must route through a component honoring the same mode / legend / text-twin contract, so the *discipline* is the invariant even when the renderer is not.

**5. The VS Code webview CSP clause of ADR 0005 is amended.** ADR 0005's "the body carries no scripts of its own" cannot survive a client-render engine. Amended narrowly: the webview may load the vendored engine and the component bootstrap under the existing nonce, and `style-src` must accommodate the runtime `<style>` Plotly injects. **This is the same ADR 0005 amendment Story 23.4 already owes — it must be landed once, not twice.** If the spike cannot achieve webview rendering under a CSP the owner accepts, webview surfaces render the text twin instead; that is a documented, accepted degradation, not a blocker on the rest of this change.

**6. Presentation tokens are SpecScribe's, not Plotly's.** The component drives every color from the existing `--status-*` and brand token families (AD-7). Plotly's default colorways are not permitted, and status must never be signalled by color alone (UX-DR17).

**7. Generation-time determinism and the no-ranking rule are unchanged.** ADR 0010 §3 stands: data is computed once at generation time and embedded — never re-derived client-side from live git state or wall-clock "now." ADR 0010 §4 stands: FR-10's no-productivity-ranking constraint is unaffected by rendering technology.

## Spike validation (direction is ratified; these are measured and recorded back as an addendum)

The direction in this ADR is decided. The Epic 20 engine-adoption spike is a **validation** step, not a ratification gate — but two of its findings can still force follow-up decisions, and the ADR names them explicitly so a bad measurement is surfaced, not swallowed:

1. **Bundle size** of a custom Plotly build limited to `sunburst` + `treemap` + `heatmap` (the full distribution is not acceptable).
2. **Net output-size delta** versus today's inline SVG across a real portal — including `code-map.html`, which has previously reached 82.5 MB. This is expected to be a *reduction*; it must be verified, not assumed.
3. **Webview CSP survival** (Decision 5), including whether the runtime `<style>` injection is tolerable under a nonce policy. **Escalation trigger:** if acceptable webview rendering is unreachable, the webview falls back to the text twin (Decision 5) — this does not reopen the engine choice.
4. **Keyboard and assistive-technology conformance** against UX-DR7 (Tab order, Enter/Space drill, Escape up), UX-DR16, and UX-DR17 — under ADR 0013 there is no server-rendered SVG to fall back to. **Escalation trigger:** a hard a11y failure Plotly cannot be configured around is the one finding that could force this ADR back open (e.g. toward the deferred ECharts option); the spike must report a11y conformance as an explicit pass/fail, not a polish note.
5. **Packaging** across all three channels: self-contained binary, npx (Story 16.8), and the VSIX (Story 16.5).
6. **Reduced-motion** conformance (UX-DR18) for Plotly's built-in transitions.

## Consequences

**Positive**
- Collapses three arc renderers, three divergent toggles, and seven server-side entry points into one component — the owner's stated goal of making site-wide changes and new features land in one place.
- Restores UX-DR5 / UX-DR6 / UX-DR7 as originally specified, instead of the pure-CSS approximation SpecScribe settled for.
- Hover, drill-in, breadcrumb, transitions, and hit-testing stop being per-surface hand-written code.
- New hierarchy surfaces (and icicle, a shape SpecScribe has never had) become nearly free.
- Replacing megabytes of inline SVG with a compact JSON payload plus one shared vendored asset is very likely a substantial output-size win.
- Ends the failure mode ADR 0010 §6 could not prevent: a shared *component* is far harder to accidentally reinvent than a shared *convention*.

**Negative / trade-offs**
- **SpecScribe's first third-party runtime dependency.** It must be vendored, audited under NFR10 (Epic 17), and packaged across three distribution channels — a permanent supply-chain and packaging obligation the zero-dependency posture did not carry.
- **Plotly's accessibility is its weakest dimension**, and under ADR 0013 there is no server-rendered SVG behind it. If the spike's a11y gate fails, this ADR does not ratify.
- The golden-HTML fingerprint — this project's primary chart regression net — stops covering chart output (see ADR 0013 §6).
- Reverses ADR 0005's "no scripts in the body" clause for the webview.
- **Two permitted engine families is a genuinely weaker invariant than one.** It is accepted deliberately: the alternative was letting Plotly's trace list decide whether Epic 24 ships force-directed and chord views.
- Story 20.4 as seated (extract shared arc math) is **invalidated** and must be replaced, not merely re-scoped.

## Options considered

| Option | Verdict |
|---|---|
| **Hand-rolled shared engine** (Story 20.4 as seated) | **Rejected.** It is the same "shared convention" remedy ADR 0010 §6 already tried and that three concurrent sessions defeated. It also buys none of the interaction behavior for free. |
| **Plotly for hierarchy; graph engine decided later** | **Chosen.** Owner-directed 2026-07-23. Best fit for the hierarchical family, honest about the Epic 24 gap. |
| **D3** | Rejected for this pass. More capable and more composable, but it is a toolkit rather than a chart library — every surface would still hand-write its chart, which is the problem being solved. |
| **ECharts** (hierarchy *and* force-directed *and* chord in one dependency) | **Considered and deferred**, not dismissed. It would preserve a true single-engine invariant. The owner chose Plotly-for-hierarchy on 2026-07-23 with the graph engine left open; **Epic 24's graph spike may legitimately reopen this**, and if it selects ECharts, superseding this ADR is the expected outcome rather than a failure. |

## Ratified decisions (2026-07-24)
1. **Plotly.js is SpecScribe's hierarchy-chart engine** — vendored locally, never CDN, `file://`-safe; custom build limited to `sunburst` + `treemap` + `heatmap`.
2. **One "Hierarchy Explorer" component is the only route to a sunburst or treemap** — one datasource, one selector, one framing block, one mandatory text twin ([ADR 0013](0013-text-twin-is-the-no-js-contract.md)); it replaces the three divergent board-tab toggles and the seven `Charts.cs` entry points.
3. **Node activation is governed by an explicit `navigate` | `select` mode**; drill-in is a distinct affordance from activation (the component intercepts Plotly's default click-to-drill); `select` never strands keyboard/AT users or removes navigation.
4. **ADR 0010 §6's single-engine rule is superseded** — two engine families are permitted (hierarchy = Plotly; Epic 24's graph engine = a named open question for its own spike), a third requires an ADR; every family routes through the same mode / legend / text-twin contract.
5. **ADR 0005's "no scripts in the body" CSP clause is amended** for the webview, landed once jointly with Story 23.4's owed amendment; if acceptable webview rendering is unreachable, the webview renders the text twin.
6. **Presentation is SpecScribe's tokens, never Plotly's colorways**; status is never color-only (UX-DR17). Generation-time determinism (ADR 0010 §3) and FR-10 no-ranking (ADR 0010 §4) are unchanged.
7. **Story 20.4 (extract shared arc math) is invalidated** and replaced by the component work; the engine-adoption spike validates bundle size, output-size delta, webview CSP, a11y (pass/fail), packaging, and reduced-motion, recorded back as an addendum.

## References
- **The rule this supersedes:** ADR 0010 §1 (baseline pages stay zero-JS) and §6 (one shared engine), and the verified three-renderer violation recorded in the Epics 19+21 joint retrospective.
- **The owner request this implements:** Epic 20 epic-body note dated 2026-07-22 (`_bmad-output/planning-artifacts/epics.md`), and the 2026-07-23 correct-course session.
- **Its necessary companion:** [ADR 0013](0013-text-twin-is-the-no-js-contract.md) — Plotly cannot server-render, so the no-JS contract must change with it.
- **The story it invalidates:** Story 20.4 (Shared Client-Side Geometry Engine), seated `backlog` 2026-07-23.
- **The CSP amendment it shares:** Story 23.4's owed ADR 0005 amendment (`_bmad-output/implementation-artifacts/23-1-spike-report.md`).

---

## Addendum — Story 20.4 spike validation (2026-07-24)

**Status stays `Accepted`.** This addendum records measurement against the six *Spike validation* items above; it
validates the ratified direction, it does not re-ratify it. **Neither escalation trigger fired.** Full evidence,
with per-number provenance, in [`20-4-spike-report.md`](../../_bmad-output/implementation-artifacts/20-4-spike-report.md);
the throwaway harness is `spike/plotly/`.

Measured against **plotly.js 3.7.0 (MIT)** and this repository generated at `--deep-git` scale (679 pages,
89,876,581 B).

| # | Item | Result |
|---|---|---|
| 1 | **Bundle size** | **1,223,515 B min / 413,449 B gzip** — 25% of the full bundle minified, 12.2× the already-accepted `prism.js`, 4.1× gzipped. Acceptable. |
| 2 | **Net output-size delta** | **−4,787,124 B across the portal — a REDUCTION, verified not assumed.** Break-even at **27** chart-carrying pages; the portal has **130**. `code-map.html` −3,493,000 B; `git-insights.html` −1,510,735 B. |
| 3 | **Webview CSP** | **PASS. Trigger did not fire.** Renders completely under the byte-verbatim shipped policy, **header- and `<meta>`-delivered**. No `'unsafe-eval'`. The style axis is satisfied and is not even load-bearing. **The §5 text-twin fallback is not selected by this evidence.** |
| 4 | **Keyboard / AT conformance** | **PASS. Trigger did not fire. This ADR is not reopened.** UX-DR7 **PASS (configured around)** — a roving-tabindex layer applied through `plotly_afterplot` survived all six re-render events (drill-in, drill-up, shape switch, drill inside treemap, resize, and a `Plotly.react`/`Plotly.relayout` the component did not initiate). UX-DR16 **PASS**, UX-DR17 **PASS**. |
| 5 | **Packaging** | **VSIX measured: +414,279 B (+20.9%)** on a 1,978,282 B baseline — the only channel with a real pipeline. Self-contained binary and npx are **design-level**: Epic 16 is entirely `backlog`. Global-tool nupkg baseline 1,877,099 B, projected ≈ +22.1%. |
| 6 | **Reduced motion (UX-DR18)** | **PASS (configured around).** Plotly's 750 ms drill is a **module constant** (`src/traces/sunburst/constants.js CLICK_TRANSITION_TIME`) with no public attribute — but the click is cancellable by returning `false` from `plotly_<type>click`, and the event carries `nextLevel`. Measured on a real click: **0 `Plotly.animate` calls**; the drill snaps by construction. No CSS animation, no `@keyframes`, zero SVG `<animate>` elements. |

### Corrections to this ADR's text

1. **§1 and Ratified decision #1 say the custom build is "limited to `sunburst` + `treemap` + `heatmap`". That is
   not achievable.** `npm run custom-bundle` resolves the trace list to `heatmap, scatter, sunburst, treemap` —
   `scatter` lives in `lib/core.js` and cannot be excluded from any bundle — and the generated index also
   registers `calendars`. **The true floor is `core` (incl. `scatter`) + `heatmap` + `sunburst` + `treemap` +
   `calendars`.** Documentation correction; not a blocker.
2. **§5's concern that "`style-src` must accommodate the runtime `<style>` Plotly injects" is already satisfied.**
   `WebviewRenderAdapter.cs:113` has granted `style-src 'unsafe-inline'` since ADR 0005. Measured further: with
   `'unsafe-inline'` *removed*, Plotly detects the block, logs `Cannot addRelatedStyleRule, probably due to
   strict CSP...`, and **still renders** — losing only hover/cursor cosmetics. The style axis was never the risk.
3. **The `--strict` bundle variant buys nothing for this trace set** and should not be adopted. It is 7 bytes
   larger with a byte-identical CSP-construct profile, because the `Function`-constructor paths it exists to
   avoid live in gl/regl traces this build already excludes. **Vendor the standard bundle.**
4. **`npm run custom-bundle` is not available from the npm package** — `plotly.js@3.7.0` ships no `tasks/`
   directory. Vendoring requires `git clone --branch v3.7.0 --depth 1`, so `tools/plotly-vendor/` cannot be a
   straight copy of `tools/prism-vendor/`'s shape.
5. **Story 20.7 must preserve conditional emission.** `SiteGenerator.cs:1983` copies `prism.js` only when
   in-portal code pages exist, "so a site with no code pages stays byte-identical". Without the same guard on the
   Plotly bundle, every golden fixture gains 1.2 MB.

### New constraints on §2's component contract (the 20.2 island is not directly consumable)

Four defects between the shipped `sunburst-explorer-data` island and Plotly's hierarchy model were found. All are
blocking for Story 20.5 and all are cheap; the hand-rolled SVG never surfaced them because it scales each ring
independently and draws its centre as a circle rather than a node.

- **One root required.** The island is a **25-root forest** (24 epics + `unplanned`) and Plotly refuses it
  outright. A project root must be synthesized — which is also where Escape-to-top and the breadcrumb land.
- **Parent weight ≠ Σ children.** 14 of 25 parents disagree, because an epic's weight counts its stories while
  its emitted children also include `aggregate` follow-up nodes. `branchvalues: 'total'` is invalid. Story 20.5
  must choose leaf-only weights or a parent-inclusive island — **a visible-geometry decision, not a detail**.
- **`null` in `values` silently renders nothing** (calcdata collapses to a single point, no error, no warning).
  Branch values must be `0`.
- **`plotly_afterplot`, never the returned promise.** Plotly resolves its promises off an animation frame, and
  the event is the only hook that also fires for re-renders the component did not initiate.

### Additions to §6 (tokens, not colorways)

Zero foreign colors reach the rendered DOM — **demonstrated over computed styles**, against an allowlist built at
runtime from the shipped `.sb-*` cascade. Reaching zero required three fixes a config-level assertion would have
missed: `marker.pattern` needs an explicit per-sector `bgcolor` (its backing rect is otherwise **black**);
`outsidetextfont` and `layout.font.color` must be set alongside `insidetextfont` (the root label alone took
Plotly's default); and **Plotly emits the hatch `<path>` inside every `<pattern>` with no `fill`**, so SVG's
initial black is painted beneath it. That last one **has no Plotly attribute** — the component must ship
`defs pattern > path { fill: none; }`.

Also: the shipped chart distinguishes follow-up and no-plan wedges by **stroke dash**, which Plotly's
`marker.line` cannot express. **`marker.pattern` hatching is the replacement** and is a stronger channel; with it,
status is carried on three independent channels (fill token, hatch, and the status word in every sector's
accessible name).

### Boundaries on this validation

- **`file://` was not directly exercised.** The session's preview pane gives no live `file://` context and no
  external browser was connected. Structural evidence is strong (4 same-origin requests after the full
  interaction suite; zero `fetch`, zero ESM imports, zero external origins), but the run is owed.
- **The CSP verdict is a lower bound on the webview gap.** `vscode-resource:` URI delivery and an Electron paint
  were not tested. This is narrower than Story 23.1's boundary — meta delivery *was* tested here — but it is real.
- **No screenshot exists.** The preview pane never composited a frame; all visual claims are computed-style,
  DOM-geometry and focus-model evidence. A human eyeball is owed at Story 20.5's create-story elicitation.
- **§5's ADR 0005 amendment was not authored here** (it lands once, jointly with Story 23.4). The evidence it will
  cite is that **no relaxation of the policy string is required**.
- **§4's Epic 24 graph engine remains open.** Plotly has no force-directed trace; nothing here settles Story 24.2.

---

## Addendum — Story 20.10: one payload, N server-declared views over it (2026-07-29)

**Status stays `Accepted`.** §2's contract text above is unchanged; this addendum ADDS a capability the
component did not have — a single instance may present several server-declared **views** over one shared
payload — rather than rewriting §2's wording. Authored by Story 20.10 (Code Map's four independently-serialized
filter-variant panels — `full` / `no-spec` / `no-tests` / `no-spec-no-tests` — collapsed into one chart instance
and one file table).

**The problem the addendum closes.** Story 20.9 converted Code Map to the component but measured the result at
only 57% of the Story 20.4 spike's projected saving: `code-map.html` serialized **3,512 chart nodes and 2,970
table rows against 1,421 distinct nodes and 1,189 distinct files** — a 2.47× duplication factor, because each of
the four filter panels independently re-serialized its own subset of the same codebase. §2's "one datasource per
instance" clause was correct for a surface with one view of its data; Code Map has four, and nothing in §2 said
what a *second* view of the *same* data costs.

**Decision.** `HierarchyExplorerModel` gains an optional `Views` list, trailing and defaulted so every existing
single-view call site is unaffected (six already-shipped islands do not move a byte). When present:

1. **Every file (or other leaf) is serialized EXACTLY ONCE**, in the model's shared `Nodes` bag — its metric bag,
   its hover card, its label/detail/href are each built once regardless of how many views contain it.
2. **A view's own structural (non-leaf) scaffold is NEVER shared across views.** Each `HierarchyView` carries its
   own directory nodes (or equivalent structural nodes) verbatim from the server, plus which shared leaves it
   contains and where each hangs *in that view* (`Files`/`ParentScaffoldIndex`, parallel integer-indexed arrays —
   not repeated path strings).
3. **The client selects one view and reparents its leaves under that view's own scaffold**, then rolls up through
   the SAME children-win rule (`HierarchyExplorer.RollUp`) every other instance already uses — extending
   `specscribe.js`'s existing `visibleNodes()` seam, not minting a second projection path.
4. **Per-view normalization stays per-view.** A ramp dimension's `[min,max]` scale re-resolves against the active
   view's own file subset on every view switch, and that view's own legend re-scales with it — preserving each
   view's shipped colours exactly rather than recolouring three of four panels as a side effect of deduplication.

**Why structural nodes are NOT deduplicated too (the one place D1's cost is paid, and it is small).** A directory
(or other grouping) node's identity is not stable across views when membership can change WHICH nodes collapse
into which — Code Map's proof: `CodeMap.BuildDir` collapses a single-child directory chain only while that
directory has no files of its own, so filtering files changes the condition. On this repository's own tree,
`.github` has two children (`agents`, `workflows`) in the `full` view; once `no-spec` drops every
`.github/agents/*` file, `.github` has one child directory and no files of its own and DOES collapse — to a
DIFFERENT id, label AND parent (`.github/workflows`, `".github / workflows"`). A file's structural parent is
therefore a property of **(file, view)**, never of the file alone. The alternative to keeping per-view scaffolds
was porting the collapse rule into JavaScript — a second copy of a structural rule, precisely the drift this ADR's
§2 exists to end — or accepting a filtered view rendering chains the server would have collapsed, a visible
fidelity regression. Keeping scaffolds server-emitted and per-view dissolves the problem at a measured, small
cost: on this repository, structural nodes across all four views (542 instances) are under a fifth the count of
distinct files (1,189), and a structural node carries no metrics, no hover card, and no href.

**Consequence for §2's "one datasource per instance" reading.** That clause is now read as "one PAYLOAD per
instance", not "one node list" — a views-bearing instance's data is still one thing the emitter builds once and
the client never fetches, re-derives, or re-counts; it is simply addressed through an extra one-of-N selector
(the active view) the same way the existing shape selector (sunburst/treemap) already is one-of-two.

**Non-goal, stated deliberately.** This is built for Code Map, its one consumer. No other surface is changed to
use `Views` speculatively — the same discipline that let Story 20.7's two-consumer node-filter resolver hold up
cleanly when Story 20.9 arrived as its second real consumer. A second `Views` consumer should prove the shape
generalizes rather than have it asserted here.

**Amendment (2026-07-29, Story 20.10 code review): a views-bearing island's structural values are NOT rolled up
server-side, and a consumer must roll up before trusting `branchvalues`.** Clause 3 above says the client reparents
and rolls up; what it did not say is the corollary for anything *other* than `specscribe.js` reading the island.
`ProjectCodeMapViews` deliberately does not call `RollUp`: a shared payload cannot carry N sets of structural
values without reintroducing exactly the duplication this addendum removes. So in a `Views`-bearing island —

- every structural (scaffold) node, including each view's synthesized root, carries `value: 0`; and
- every shared leaf carries `parentId: null`, because its parent is a property of (leaf, view);

while `config.branchvalues` is still emitted as `"total"`. Those are consistent only *after* a per-view reparent
and roll-up. Because the JSON IR **is** the canonical artifact every surface projects from
([ADR 0008](0008-json-ir-canonical-and-incremental-generation.md)) and
[ADR 0013 §5](0013-text-twin-is-the-no-js-contract.md) makes component configuration part of what the IR carries,
this is a **contract on the consumer, not a defect in the payload**: any IR consumer that renders a views-bearing
hierarchy must select a view, reparent its leaves under that view's scaffold, and roll up children-win — the same
three steps clause 3 names — before reading `value` or honouring `branchvalues`. A consumer that reads `nodes`
directly and skips those steps sees N parentless leaves and zero-sized groupings, and will render wrong with only a
console warning. Stated here because Epic 22's and Epic 23's IR consumers are in flight and nothing else records it.

Two emitter guards enforce what this addendum assumes, and both fail loudly rather than shipping a wrong island
(`HierarchyExplorer.IslandHtml`): a `Views`-bearing model with **no dimensions** is rejected, because the views
payload rides on the dimension-bearing island shape only and the non-dimension shape is byte-frozen for the six
already-shipped surfaces; and **duplicate view keys** are rejected, because a view key addresses both the per-view
legend blocks and the `{hashKey}-view=` deep-link fragment.

### Ratified addition to Ratified decisions (2026-07-29)
8. **A Hierarchy Explorer instance may present N server-declared VIEWS over one shared payload** (`HierarchyExplorerModel.Views`, optional, defaulted). Leaves are serialized once, in the shared payload; a view's own structural scaffold and its leaf membership/parentage are declared per view, never shared, because structural identity is a property of (leaf, view) when a grouping rule can collapse differently per view. The client selects a view, reparents, and rolls up through the SAME children-win rule every instance already uses. Per-view colour normalization (ramps, legends) stays per-view. **A views-bearing island's structural values are NOT rolled up server-side** (scaffold nodes ship `value: 0`, shared leaves ship `parentId: null`) while `branchvalues` is still `"total"`: selecting a view, reparenting and rolling up is a contract on EVERY consumer of the IR, not just on `specscribe.js` — see the 2026-07-29 amendment above. First and, for now, only consumer: Code Map's four filter combinations (Story 20.10).
