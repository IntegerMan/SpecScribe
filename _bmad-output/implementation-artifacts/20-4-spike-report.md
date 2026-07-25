# Story 20.4 — Plotly Engine-Adoption Spike: Report

**Date:** 2026-07-24 · **Story:** [20-4-plotly-engine-adoption-spike.md](./20-4-plotly-engine-adoption-spike.md) ·
**Validates:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
(already `Accepted`) · **Companion:** [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
**Probe:** [`spike/plotly/`](../../spike/plotly/) — throwaway, quarantined, **no production code shipped**

---

## Headline

| | |
|---|---|
| **Escalation trigger (a) — webview CSP** | **DID NOT FIRE.** Plotly renders completely under the byte-verbatim shipped webview policy, header- *and* meta-delivered. No `'unsafe-eval'`. No relaxation. |
| **Escalation trigger (b) — hard a11y failure** | **DID NOT FIRE.** UX-DR7 is **PASS (configured around)** and survives all six re-render events. UX-DR16/17/18 are **PASS**. **ADR 0012 is not reopened.** |
| **Net output size** | **−4,787,124 B across the whole portal** (a reduction), bundle already paid for. Break-even at **27** chart-carrying pages; the portal has **130**. |
| **Bundle** | **1,223,515 B** minified / **413,449 B** gzip — **12.2×** the already-accepted `prism.js` uncompressed, **4.1×** gzipped. |
| **`--strict`** | **Buys nothing here.** 7 bytes larger, byte-identical CSP profile. **Recommend the standard bundle.** |
| **Unexpected** | Four **data-contract** defects between the Story 20.2 island and Plotly's hierarchy model. All fixable, all must be fixed by Story 20.5. Section 7. |

---

## 1. Context and discipline

ADR 0012 was ratified on 2026-07-24 with the owner choosing ratify-now, on the condition that a spike supply the
numbers. This is that spike. It **measures**; it does not re-decide. Only a hard a11y failure could have reopened
the engine choice, and it did not occur.

Timebox: **1 session** against a suggested 2 days. All five axes were measured; nothing was reported as
unmeasured except the two boundaries named explicitly in §8.

**Provenance labels used throughout, following the convention Story 23.1's report had to be corrected into:**

| Label | Meaning |
|---|---|
| **[HARNESS]** | reproducible by running a script in `spike/plotly/scripts/`; the number is in `measurements/*.json` |
| **[SESSION]** | measured once, by hand, in a live browser or a live packaging run; recorded in `measurements/session.json` |
| **[PROJECTED]** | computed from a measured basis; the basis is always named |
| **[DESIGN-LEVEL]** | an analysis, not a measurement. Never presented as one. |

---

## 2. Method

* **Portal under measurement:** this repository, `generate --deep-git`, **679 pages / 1,274 files / 89,876,581 B**,
  written to a session-private directory rather than `SpecScribeOutput/`. Reason: a concurrent session
  regenerated `SpecScribeOutput/` mid-spike without `--deep-git`, deleting `git-insights.html` and
  `deep-analytics.html` out from under an in-flight measurement. Per CLAUDE.md § Concurrent work, the private
  directory is the fix; re-running generate against the shared one would have been a fight. **[SESSION]**
* **Whose changes this sits on top of:** the Release build was taken while a concurrent session had uncommitted
  edits to `HowToReadTemplater.cs`, `SiteNav.cs`, `SiteGeneratorHowToReadTests.cs`, `SiteNavTests.cs` and
  `SiteGeneratorAdapterTests.cs` in the tree. None of them touch `Charts.cs` or any hierarchy renderer, so the
  SVG-side numbers are unaffected; recorded here because CLAUDE.md requires it, not because it changes a figure.
* **SVG classification:** every `<svg>` in every emitted page, depth-tracked to its matching close, classified by
  class. **Only the five classes the seven entry points actually emit count as removable.** `ss-icon`,
  `ref-graph`, `work-graph`, `risk-quadrant`, `donut`, `heatmap`, `funnel` and `req-flow-svg` are excluded and
  reported separately, because counting them would inflate the win by ~22 MB.
* **CSP:** the policy string is **read out of `WebviewRenderAdapter.cs` at runtime** by both the probe builder and
  the server. It is never pasted. An upstream policy change cannot silently invalidate this report.
* **Tokens:** every color is resolved by `getComputedStyle` on a real element carrying the real shipped `.sb-*`
  class, through the real cascade over the **generated** `specscribe.css`. No token value is typed anywhere in
  the probe.
* **Payload:** the **real** `sunburst-explorer-data` island is lifted verbatim out of the generated dashboard.
  No synthetic fixture.

---

## 3. AC #1 — bundle, output size, offline, packaging

### 3.1 The bundle, and the R1 trace-floor correction

`npm run custom-bundle -- --traces sunburst,treemap,heatmap` reports its own resolved trace list:

```
traceList: [ 'heatmap', 'scatter', 'sunburst', 'treemap' ]
```

**R1 is confirmed empirically, and it is slightly worse than the story predicted.** The generated
`lib/index-specscribe-hierarchy.js` registers `core, heatmap, sunburst, treemap, calendars` — so the true floor is
**`core` (which contains `scatter`) + `heatmap` + `sunburst` + `treemap` + `calendars`**, five modules, not the
three ADR 0012 §1 names. `calendars` is pulled in unconditionally alongside `core`. This is a documentation
correction for the ADR, not a blocker.

| Artifact | min | min+gzip | min+brotli | ×`prism.js` (min) | ×`prism.js` (gzip) |
|---|---|---|---|---|---|
| **custom, standard** (4 traces) | **1,223,515** | **413,449** | 336,985 | **12.19×** | **4.12×** |
| custom, `--strict` (4 traces) | 1,223,522 | 413,451 | 337,105 | 12.19× | 4.12× |
| custom, `--strict`, heatmap dropped | 1,191,574 | 400,522 | 329,660 | 11.87× | 3.99× |
| upstream **full** bundle | 4,855,045 | 1,475,636 | 1,098,710 | 48.35× | 14.70× |
| upstream **full strict** bundle | 5,198,413 | 1,577,856 | 1,151,381 | 51.77× | 15.71× |

**[HARNESS]** — `node scripts/measure-bundle.mjs`, `measurements/bundle.json`.

Yardsticks: the shipped `src/SpecScribe/assets/prism.js` is **100,409 B** and `specscribe.js` is **116,165 B**.
(The story quoted `specscribe.js` at 98,114 B / 1,961 lines; it is now 116,165 B / 2,237 lines — Story 20.3's
card rail landed in between. Minor drift, recorded so the multiple above is checkable.)

Against Plotly's published v3.7.0 figures (4.6 MB min / 1.4 MB gz full; 1.5 MB gz full-strict) the four-trace
custom build lands at **25% of the full bundle minified** and **28% gzipped** — comfortably below both, as the
story expected.

### 3.2 `--strict` buys nothing for this trace set — recommend the standard bundle

This was billed as *"the single highest-leverage experiment"*. It resolved decisively, and against expectation.
The CSP-relevant construct inventory, counted over the emitted artifacts:

| construct | custom (standard) | custom (`--strict`) | upstream full | upstream full strict |
|---|---|---|---|---|
| `new Function(` | **0** | **0** | 1 | 1 |
| `Function('…')` | **0** | **0** | 3 | 3 |
| `eval(` | **0** | **0** | 0 | 0 |
| `import(` / ESM `import` / `export` | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| `fetch(` | **0** | **0** | 1 | 1 |
| `XMLHttpRequest` | 1 | 1 | 3 | 3 |
| `WebSocket` / `sendBeacon` | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| CDN / `plot.ly` URLs | 4 | 4 | 12 | 12 |

**[HARNESS]**

The `Function`-constructor paths that force `script-src 'unsafe-eval'` live in the **gl/regl** traces, which this
bundle does not contain. Excluding them removes the eval problem *before* `--strict` gets a chance to. `--strict`
is therefore **7 bytes larger with an identical construct profile** — pure ceremony for this trace list.

> **Recommendation to Story 20.5: vendor the STANDARD bundle.** Revisit only if a future trace (`scattergl`,
> `choropleth`) is ever added — those are the ones `--strict` exists for.

The 4 remaining `plot.ly` references were read out of the bundle and identified individually: the `topojsonURL`
default `https://cdn.plot.ly/un/` (a geo-trace default; geo is not in this bundle), the modebar logo's
`<a href="https://plotly.com/">` (removed by `displaylogo: false`), and two error-message strings. The single
`XMLHttpRequest` is d3's `d3.xhr`, reachable only from the topojson path. **None is exercised** — see §3.4.

### 3.3 Net output-size delta across a real portal — the headline number

| | Bytes | Provenance |
|---|---:|---|
| Σ hierarchy SVG removed, 130 pages | **8,484,973** | **[HARNESS]** |
| Σ payload added, 12,660 nodes | **2,474,334** | 1 page **[HARNESS]**, 129 **[PROJECTED]** |
| One vendored bundle, once | **1,223,515** | **[HARNESS]** |
| **NET** | **−4,787,124 (REDUCTION)** | |
| **Break-even page count** | **27** chart-carrying pages | |

The portal carries **130**. The bundle is amortised roughly **five times over**.

**Projection basis, stated plainly:** the one real island measures **23,258 B over 119 nodes = 195.4 B/node**. For
the 129 surfaces with no island yet, the payload is projected as *(that page's actually-drawn node count) ×
195.4 B*, where the node count is counted from the emitted markup (`sb-seg`, `codemap-cell|dir|wedge`,
`ownership-cell|wedge`). This is **conservative in the direction that hurts the case**: the measured basis comes
from the epic/story island, whose labels ("Story 1.1: Dashboard Navigation and Readability Foundation") are far
longer than the file paths a code-map node would carry. The real code-map payload will be smaller than projected.

Per-entry-point, across the whole portal **[HARNESS]**:

| Entry point | SVG class | instances | Σ bytes |
|---|---|---:|---:|
| `Charts.CodeTreemap` | `codemap` | 4 | 3,106,622 |
| `Charts.Sunburst` / `EpicSunburst` / `TaskSunburst` | `sunburst` | 128 | 1,798,845 |
| `Charts.CodeMapSunburst` | `codemap-sunburst` | 4 | 1,569,213 |
| `Charts.CodeOwnershipSunburst` | `ownership-sunburst` | 1 | 1,042,930 |
| `Charts.CodeOwnershipTreemap` | `ownership-treemap` | 1 | 967,363 |

All seven symbols were grep-verified present in `Charts.cs` (now 4,867 lines) before this table was trusted.

**The two surfaces that dominate:**

| Page | page bytes | hierarchy SVG | % of page | drawn nodes | projected payload | single-page delta |
|---|---:|---:|---:|---:|---:|---:|
| `code-map.html` | 5,682,933 | 4,675,835 | **82.3%** | 6,052 | 1,182,835 **[PROJECTED]** | **−3,493,000** |
| `git-insights.html` | 2,042,439 | 2,010,293 | **98.4%** | 2,556 | 499,558 **[PROJECTED]** | **−1,510,735** |

#### R4 — `code-map.html` is 5.68 MB today, not 82.5 MB

**Do not quote 82.5 MB as a current figure.** That was Story 6.6's at-scale peak on 2026-07-20; the
`Charts.MaxDetailedCodeMapFiles` cap landed 2026-07-21 (`deferred-work.md:453`). Measured today at `--deep-git`
scale on this repo: **5,682,933 B raw / 448,533 B gzipped**. The win is still the largest on the portal — it is
just 5.7 MB large, not 82.5 MB large, and the report says so because overstating it would be the easiest
available error.

#### What Story 20.7 does *not* remove

`ss-icon` (13,792,308 B), `ref-graph` (5,976,496 B), `work-graph` (696,538 B), `risk-quadrant` (653,680 B),
`donut` (275,795 B) and five smaller families are **outside Epic 20's rollout**. They are listed here so nobody
later reads "9.44% of the portal is hierarchy SVG" as "Epic 20 removes most of the portal's SVG". It does not.
`impact-map.html` already ships **zero** chart SVG (Story 21.3 made it client-rendered), so 20.7 gains nothing
there in bytes — only standardisation.

### 3.4 Offline / no outbound request

After driving the **full** interaction suite (drill-in, drill-up, both shape switches, two resizes,
`Plotly.react`, `Plotly.relayout`), the page had made exactly **four** resource requests **[SESSION]**:

```
/specscribe.css   /plotly.min.js   /explorer.js   /survival.js      externalOrigins: []
```

Zero fetches, zero telemetry, zero CDN, zero lazy chunk. Combined with the static inventory in §3.2 (no `fetch`,
no dynamic `import`, no ESM static imports, no `WebSocket`), the bundle is a **single classic script** with
nothing to load — which is also what makes one `<script nonce>` sufficient under the webview CSP (R7 #1), exactly
as `prism.js` already ships.

> **`file://` is the one axis this session could not measure directly.** The in-app preview pane refuses to give a
> live `file://` context (it renders such URLs as static snapshots), and no real Chrome was connected to this
> session. Everything a `file://` run would test — no CORS-blocked module imports, no `fetch` to a file URL, no
> remote asset — is **[HARNESS]**-measured above to be structurally absent, but the run itself is **owed**.
> Reproduce in one step: `start spike/plotly/probe/index.html` with networking disabled, and confirm 119 sectors.
> Given zero requests and zero module imports, a failure would be surprising; it is nevertheless unverified and is
> listed in §9 as such.

### 3.5 Packaging — measured (VSIX) vs design-level (binary, npx), per R5

| Channel | Status of the pipeline | Result |
|---|---|---|
| **VSIX** (Story 16.5) | **Real** — `extension/package.json:283` | **MEASURED** |
| **Self-contained binary** (Story 16.3) | Does not exist; `SpecScribe.csproj` packs as a .NET global tool | **DESIGN-LEVEL** |
| **npx** (Story 16.8) | Does not exist; ADR 0006 §C describes a wrapper that fetches the binary | **DESIGN-LEVEL** |

**VSIX [SESSION], measured by packaging twice:**

| | bytes | files |
|---|---:|---:|
| baseline | 1,978,282 | 29 |
| with the vendored bundle | 2,392,561 | 30 |
| **delta** | **+414,279 (+20.9%)** | +1 |

The VSIX zip deflates the bundle to **414,279 B** — within 830 bytes of its standalone gzip size, so the packaged
cost is simply "the gzip number". The measurement was taken by placing the bundle in `extension/bin/` (which the
VSIX includes) rather than by modifying `SpecScribe.csproj`, because this story ships no production code; both
routes put the same compressible bytes inside the same zip. Both artifacts were deleted afterwards; `git status`
on `extension/` is clean.

**Global-tool package [SESSION] + [PROJECTED]:** `dotnet pack -c Release` produces a **1,877,099 B** nupkg today.
The bundle would ride as an `<EmbeddedResource>` in `specscribe.dll` exactly like `prism.js`
(`SpecScribe.csproj:62-63`), and the nupkg is likewise a deflate archive, so the projected packed size is
**≈ 2,291,378 B (+22.1%)**. **This is design-level arithmetic, not a measurement.**

**npx [DESIGN-LEVEL]:** ADR 0006 §C's wrapper is ~1.5 KB and *fetches* the binary, so its cost is the binary's
delta. No number is invented for it.

**Conditional emission holds and must be preserved.** `SiteGenerator.cs:1983-1986` copies `prism.js`/`prism.css`
**inside a guard**, with the comment *"so a site with no code pages stays byte-identical (and the golden fixtures,
which cite no real repo files, never gain these assets)"*. The Plotly bundle must ride the same guard, keyed on
"this site emitted at least one hierarchy chart". This is a hard constraint on Story 20.7, not a nicety: without
it every golden fixture gains 1.2 MB.

---

## 4. AC #2 — the webview CSP axis (escalation trigger (a))

**Verdict: PASS. Trigger (a) did not fire. The ADR 0012 §5 text-twin fallback is not selected by this evidence.**

The script and style axes are reported **separately**, per R3.

| Variant | Renders? | Sectors | Plotly style rules applied | Foreign colors | Console errors |
|---|---|---:|---:|---:|---:|
| **Shipped policy, HTTP header** | **YES** | 119 | 64 | 0 | 0 |
| **Shipped policy, `<meta http-equiv>`** | **YES** | 119 | 64 | 0 | 0 |
| Shipped policy **minus** `style-src 'unsafe-inline'` | **YES (degraded)** | 119 | **0** | — | Plotly's own warning |
| Shipped policy, **wrong nonce** (partial relaxation) | **NO** | 0 | — | — | — |
| Scripts removed (JS off) | **NO** | 0 | — | — | — |

**[SESSION]** — `measurements/session.json § cspMatrix`.

### 4.1 Script axis — no `'unsafe-eval'` needed

The shipped `script-src 'nonce-…'` **alone is sufficient**. This follows from §3.2: the custom bundle contains
**zero** `new Function(`/`eval(` constructs, because those live in traces excluded from the build. The live run
confirms it: full render, full interaction, zero console errors.

**This means ADR 0012 §5's worry was about the wrong axis, and R2's premise — that `--strict` would be the lever —
was correct in mechanism but moot in practice: trace exclusion already did the job.**

### 4.2 Style axis — already satisfied, and not load-bearing anyway

R3 was right that `style-src 'unsafe-inline'` is **already in the shipped policy**, so Plotly's runtime style
injection is satisfied today. Two things are worth recording beyond that:

* Plotly injects **one** `<style id="plotly.js-style-global">` element and populates it via **`insertRule`**
  (64 rules), not via text content. It also writes ~243 inline `style=` attributes.
* Under a policy *without* `'unsafe-inline'`, the chart **still renders**. Plotly detects the block and logs
  `Cannot addRelatedStyleRule, probably due to strict CSP...`, losing only hover/cursor cosmetics. So even in the
  harder hypothetical the style axis is not a blocker.

Collapsing these two axes into one verdict, as the story warned, would have reported a gap that does not exist.

### 4.3 The partial-relaxation state — R7 #3 confirmed, and it is as bad as feared

With a nonce mismatch (the shape of a half-applied policy fix), the measured result is:

```
plotlyLoaded: false   chartInnerHTMLBytes: 0   svgsInChartRegion: 0   anchorsOnPage: 0
chart region: a 640 x 640 empty box
```

Under ADR 0013 there is **no server-rendered SVG beneath the chart**, so a half-fixed policy produces a **blank
rectangle**, not a degraded chart. This is the same failure class Story 23.1 measured as "148 SVGs → 0", now with
nothing underneath. The probe deliberately ships no text twin, so this measures the **worst case Story 20.6's
per-surface twin audit exists to prevent** — it is not a prediction about the shipped surfaces.

> Note for the eventual 23.4 regression test: **CSP violations did not surface in the browser tooling's console
> capture.** The blocked state was only detectable by asking the DOM what was there. A regression test that greps
> the console for CSP errors will pass while the chart is blank.

### 4.4 The island is stripped from webview documents today

`WebviewRenderAdapter.cs:79` removes every `<script type="application/json">` island from webview content, and
`:60-72` registers this as the `data-island` webview exception in `HostRenderExceptions`. The comment is explicit
that the island is dropped as dead weight (the webview ships no `specscribe.js`), **not** for CSP reasons.

**Consequence for Story 20.5/20.7:** for the webview to render a Plotly chart at all, that exception must be
**narrowed** — the hierarchy island must be kept and the bundle admitted as a nonced classic script — or the
webview takes the ADR 0012 §5 text-twin fallback by construction. The CSP does not force that choice; the current
stripping does. **That is a decision Story 20.5 owes, and this spike does not make it.**

### 4.5 Honesty boundary, inherited from Story 23.1 §Axis 3

This session **improves** on 23.1's boundary but does not eliminate it:

* ✅ **Meta delivery was tested**, not just header delivery — 23.1 could only test the header. Same verdict both ways.
* ❌ **`vscode-resource:` URI delivery is untested.** The probe served over `http://localhost`.
* ❌ **No Electron paint.** VS Code's webview is Chromium-in-Electron with its own resource-loading rules.
* ❌ **No real extension host.** The nonce was substituted by a stand-in server, not by the shim.

**The verdict above is a lower bound on the webview gap, not a characterization of it.** The remaining unknown is
narrow and named: whether `vscode-resource:` resolution of a 1.2 MB classic script inside Electron behaves like
`http://localhost`.

**The ADR 0005 CSP amendment was NOT authored here.** Per ADR 0012 §5 it lands **once**, jointly with Story 23.4's
owed amendment. This spike produces the evidence that amendment will cite — and the evidence is that **no
relaxation is required for the policy string itself**, which should make that amendment considerably smaller than
23.4's analysis assumed.

---

## 5. AC #2 — accessibility, PASS/FAIL (escalation trigger (b))

**Verdict: no FAIL. Escalation trigger (b) did not fire. ADR 0012 is not reopened.**

| UX-DR | Requirement | Verdict | Evidence |
|---|---|---|---|
| **UX-DR7** | Tab order, Enter/Space to drill, Escape to go up; per-node accessible names | **PASS (configured around)** | §5.1 — 10/10 survival steps intact |
| **UX-DR16** | Landmarks, whole-chart accessible name, announced state | **PASS** | §5.2 |
| **UX-DR17** | Status never signalled by color alone | **PASS** | §5.3 — three independent channels |
| **UX-DR18** | `prefers-reduced-motion`: transitions snap, nothing loops | **PASS (configured around)** | §5.4 |

Verdicts use the story's decision rule verbatim. "Partial", "mostly" and "with work" do not appear.

### 5.1 UX-DR7 — the crux

Plotly's hierarchy traces are **not** keyboard-focusable per node by default; that was known going in. The real
question was whether a roving-tabindex layer applied over Plotly's emitted `<path>` nodes **survives** Plotly
re-rendering them.

**The layer is applied entirely through `plotly_afterplot`, Plotly's public post-render event, over its emitted
DOM. No Plotly internal is patched, forked, or monkeyed.** That is what makes this "configured around" rather
than a fork.

Survival predicate, applied mechanically after each event: *sectors > 0 **and** `role="treeitem"` on every sector
**and** a non-empty `aria-label` on every sector **and** exactly one `tabindex="0"`.*

| # | Event | Sectors after | Layer reapplied | **INTACT** |
|---|---|---:|---|---|
| 0 | initial render | 119 | — | ✅ |
| 1 | keyboard reachability (focus lands, arrow moves) | 119 | — | ✅ |
| 2 | **drill-in** (Enter on an epic) | 7 | ✅ | ✅ |
| 3 | **drill-up** (Escape) | 119 | ✅ | ✅ |
| 4 | **shape switch** → treemap | 119 | ✅ | ✅ |
| 5 | drill-in *inside* treemap | 7 | ✅ | ✅ |
| 6 | shape switch back → sunburst | 7 | — | ✅ |
| 7 | **resize** (`Plotly.Plots.resize`) | 7 | ✅ | ✅ |
| 8 | **bare `Plotly.react`** the component did not initiate | 7 | ✅ | ✅ |
| 9 | **`Plotly.relayout`** | 7 | ✅ | ✅ |

**[SESSION]**, measured **under the shipped webview CSP** (the strictest realistic condition), and reproduced
identically without CSP. `measurements/session.json § a11ySurvival`.

Step 8 is the adversarial one and it is the reason the verdict is trustworthy: an update path the component did
**not** initiate still triggers `plotly_afterplot`, so the layer is restored. A layer that only survived the
component's own `redraw()` would have failed here, and per the decision rule that would have been a **FAIL**.

**Refinements 20.5 owes (none of them change the verdict):** `aria-level`, `aria-expanded` and `aria-posinset`
are not yet emitted; `role="tree"` sits on Plotly's `svg.main-svg` while the `treeitem`s are nested inside
`g.slice`, which is structurally acceptable but should be tightened; and the probe's Tab order is DOM order
rather than ring order.

### 5.2 UX-DR16

The Plotly container carries `role="tree"` and a real accessible name (`"Project progress hierarchy — sunburst"`,
regenerated on shape switch — verified changing to `"— treemap"` at step 4). Drill-scope changes are announced
through an `aria-live="polite"` region: measured values include `"Drilled into Epic 1: High-Clarity BMad Portal
Experience"` and `"Moved up to SpecScribe"`. Every one of the 119 sectors carries an accessible name of the form
`"<label> — <status>, weight <n>"`. **PASS.**

### 5.3 UX-DR17 — and a real Plotly gap, worked around

The shipped SVG distinguishes follow-up and no-plan wedges by **stroke dash** as well as fill — a non-color
channel. **Plotly's sunburst/treemap `marker.line` has no `dash` attribute; that channel does not exist.**

It has a better one: **`marker.pattern`**, per-sector hatching. Measured, status is carried on **three independent
channels**:

| Channel | Coverage |
|---|---|
| **Fill** — the six `--status-*` tokens plus the three chart-local ones, resolved through the real cascade | 119 / 119 sectors |
| **Hatch pattern** — replaces the shipped stroke-dash on the four non-lifecycle statuses | 46 / 46 sectors that need it (`noplan` 25, `followup-open` 10, `followup-done` 9, `unplanned` 2) |
| **Text** — the status word inside the accessible name | 119 / 119 sectors |

**PASS.** Nothing is signalled by color alone. Note this is *stronger* than the shipped chart, which has no
per-sector pattern.

### 5.4 UX-DR18 — the 750 ms constant, and how it is defeated

**Plotly's sunburst/treemap drill animation is a hard-coded module constant, not a configurable attribute:**
`src/traces/sunburst/constants.js` → `CLICK_TRANSITION_TIME: 750` (and its treemap twin), fed straight into
`attachFxHandlers` in `fx.js:270`. There is no entry for it in the trace attribute schema. A config-only search
would have concluded UX-DR18 was unreachable.

It is reachable through the **event** surface. `fx.js:240-252` shows the click handler consulting
`Events.triggerHandler(gd, 'plotly_sunburstclick', …)` and returning early when a handler returns `false` — and
the event data carries `nextLevel`, so the level can be re-applied by us.

Measured on a **real mouse click** on a real sector:

| | |
|---|---|
| `Plotly.animate` calls during the drill | **0** — the built-in 750 ms transition never ran |
| Level after click | `epic-2` — the drill still happened |
| a11y layer reapplied | ✅ |
| Duration with reduced-motion asserted | **0 ms** |
| Duration otherwise | **600 ms**, read from the shipped `--motion-entrance: 0.6s` |

**[SESSION]**. The level is re-applied with `Plotly.react`, which never animates — so under this design **the
drill snaps by construction and there is no transition left to suppress**. `prefers-reduced-motion` selects the
same instant path a fortiori. No CSS animation, no `@keyframes`, and **zero** SVG `<animate>` elements were found
anywhere in Plotly's output; nothing loops. **PASS (configured around).**

> **Seam disclosure:** the browser session cannot flip the OS/UA reduced-motion setting, so the 0 ms branch was
> exercised through an explicit test override on the same expression, not by the media query firing. The media
> query itself is the standard `matchMedia('(prefers-reduced-motion: reduce)')` idiom. Both branches of
> `reducedMotion() ? 0 : motionMs('--motion-entrance')` were shown to produce `0` and `600`.

---

## 6. AC #3 — tokens, not colorways

**Demonstrated, not asserted.** The final audit over the rendered DOM:

```
sectors: 119        foreignColors: []        textFills: { --status-unrecognized: 119 }
solid token fills:  done 44 · ready 16 · active 5 · review 4 · drafted 3 · unrecognized 1 (synthesized root)
patterned sectors:  46, every pattern's bgcolor and fgcolor a shipped token
```

**[SESSION]**. The allowlist it is checked against is **built at runtime** by resolving the shipped `.sb-*`
classes through the real cascade — no token value is typed in the probe, so a token change moves the allowlist
with it (the drift-free-by-construction discipline Story 23.1 established).

**Getting to zero required finding three leaks that a config-level assertion would have missed.** This is the
whole reason AC #3 says *demonstrated*:

1. **`marker.pattern` with no `bgcolor` paints its backing rect BLACK** — 67 occurrences inside the `<pattern>`
   defs. Fixed by supplying `marker.pattern.bgcolor` per sector.
2. **The root label alone took Plotly's default `rgb(68,68,68)`** — one element out of 119. `insidetextfont` was
   set; `outsidetextfont` and `layout.font.color` were not.
3. **Plotly emits the hatch `<path>` inside every `<pattern>` with a `stroke` but NO `fill`**, so SVG's initial
   value (black) is painted at `fill-opacity: 1` beneath every hatch — 21 occurrences. **There is no Plotly
   attribute for this.** It is fixed by one CSS rule, and the foreign-color count went **1 → 0** the moment it
   was applied:

   ```css
   .ss-hierarchy defs pattern > path { fill: none; }
   ```

   **Story 20.5's component must ship that rule.** Without it, black bleeds under every hatched sector.

Nothing in Plotly's color model prevents routing through the six-token status system
(`--status-*` → `marker.colors`, one array, per sector). `layout.colorway`, `sunburstcolorway`, `treemapcolorway`,
`extendsunburstcolors: false` and `extendtreemapcolors: false` are all set as belt-and-braces, but the per-sector
array is what actually does the work.

---

## 7. Unexpected findings — four data-contract defects Story 20.5 must fix

None of these appear in ADR 0012, the epic, or the story. All four are **blocking for the component** and all four
are cheap to fix. They are the most valuable thing this spike produced.

| # | Finding | Why the hand-rolled SVG never noticed | Fix |
|---|---|---|---|
| **A** | **Plotly hierarchy traces require exactly ONE root.** The 20.2 island is a **25-root forest** (24 epics + `unplanned`); Plotly refuses it: *"Multiple implied roots, cannot build sunburst hierarchy of trace 0."* | The shipped sunburst's centre is a **drawn circle**, not a data node | synthesize a project root in the component, or emit one from `SunburstExplorer.cs`. It also gives Escape-to-top and the breadcrumb somewhere to land. |
| **B** | **A single `null` in `values` silently collapses the ENTIRE hierarchy** to one calcdata point and renders nothing — no error, no console warning. Measured: calcdata `1 → 119` on changing `null` to `0`. | n/a | branch values must be `0` |
| **C** | **Parent weight ≠ Σ children.** 14 of 25 parents disagree (epic-1: 42 vs 50) because an epic's weight counts its stories while its emitted children *also* include `aggregate` follow-up nodes. `branchvalues: 'total'` is invalid and warns per parent. | the shipped SVG scales **each ring independently**; the rings never have to agree. Plotly is a single tree. | leaf-only values with `branchvalues: 'remainder'` — **or** make the island parent-inclusive. **20.5's call, and it is a visible-geometry decision, not a detail.** |
| **D** | **`npm run custom-bundle` is not available from the npm package.** `plotly.js@3.7.0` ships `lib/`, `src/`, `dist/` and `esbuild-config.js` but **not** `tasks/`, and `esbuild-config.js` requires `./tasks/util/constants.js`. Vendoring requires `git clone --branch v3.7.0 --depth 1`. | n/a | `tools/plotly-vendor/` cannot be a straight copy of `tools/prism-vendor/`'s `npm i` + `build.js` shape — its README must document the clone step |

A fifth, environmental: **Plotly resolves its own promises off an animation frame**, so `await Plotly.react(…)`
never settles in a non-compositing tab. `plotly_afterplot` is the reliable seam — and, per §5.1 step 8, the only
one that also fires for re-renders the component did not initiate. **Story 20.5 should hang the a11y layer on the
event, never on the promise.**

---

## 8. Supply-chain record for the Epic 17 / NFR10 audit

This is SpecScribe's **first third-party runtime dependency**.

| | |
|---|---|
| Package | `plotly.js` |
| Version | **3.7.0** |
| License | **MIT** |
| Committed artifact | one self-contained classic script, `plotly-specscribe-hierarchy.min.js`, 1,223,515 B |
| Transitive footprint of the artifact | **zero** — it is one file, no imports, no runtime resolution |
| Build-time footprint | 261 packages in the probe workspace; **throwaway and gitignored**, exactly like `tools/prism-vendor/node_modules` |
| `npm audit` on the plotly.js **clone**'s dev tree | 9 vulnerabilities (1 low, 1 moderate, 7 high) — **all in build-time devDependencies of the upstream repo, none in the emitted artifact.** Recorded so the eventual audit is not surprised by it. |
| Node in the shipped pipeline | **none.** The bundle is built by hand and committed, exactly like `prism.js`. `specscribe generate` still needs no Node. |

---

## 9. AC coverage — with explicit boundaries

| AC | Obligation | Status | Boundary |
|---|---|---|---|
| **#1** | custom-build size | ✅ **[HARNESS]** | true floor is 5 modules, not 3 (R1 corrected upward) |
| **#1** | both standard and `--strict`, min + gzip | ✅ **[HARNESS]** | — |
| **#1** | net output-size delta across a real portal | ✅ 1 page **[HARNESS]** + 129 **[PROJECTED]** | projection basis named; conservative direction stated |
| **#1** | `code-map.html` at its current size + historical peak | ✅ **[HARNESS]** | — |
| **#1** | loads offline, no outbound request | ✅ **[SESSION]** | — |
| **#1** | loads from `file://` | ⚠️ **NOT DIRECTLY MEASURED** | preview pane gives no live `file://` context; no Chrome connected. Structural evidence strong (0 requests, 0 imports). One-step repro given in §3.4. |
| **#1** | packaging, three channels | ✅ VSIX **[SESSION]**; binary/npx **[DESIGN-LEVEL]** | Epic 16 is entirely `backlog`; only VSIX has a pipeline |
| **#2** | renders under the webview CSP | ✅ **[SESSION]**, header **and** meta | `vscode-resource:` + Electron paint untested — verdict is a **lower bound** |
| **#2** | script axis and style axis reported separately | ✅ | — |
| **#2** | partial-relaxation state tested | ✅ **[SESSION]** | probe ships no text twin by design — worst case, not a prediction |
| **#2** | explicit PASS/FAIL per UX-DR7/16/17/18 | ✅ **[SESSION]** | UX-DR18's 0 ms branch exercised via a labelled test seam, not by the media query firing |
| **#3** | chart driven by `--status-*`, colorways disabled | ✅ **[SESSION]**, demonstrated | required 3 fixes; one needs CSS Plotly cannot express |
| **#3** | findings recorded onto ADR 0012 as an addendum | ✅ | ADR Status stays `Accepted` |

### What was NOT done, deliberately

* **No production code.** `src/SpecScribe/**` and `tests/**` were not modified by this story — see §10.
* **No `tools/plotly-vendor/`.** That is Story 20.5/20.7's to land.
* **No ADR 0005 CSP amendment.** Shared with Story 23.4; lands once (ADR 0012 §5).
* **No renderer, entry point, or SVG deleted.** That is Story 20.7, gated per-surface on Story 20.6.
* **No new tests.** No production code means nothing to unit-test; a test in `tests/**` would mean leaving the spike.
* **No pixel screenshot.** The preview pane never composited a frame in this session, so no image could be
  captured. All visual claims are computed-style, DOM-geometry and focus-model evidence — which is what CLAUDE.md's
  live-browser rule targets — but **a human eyeball on the rendered chart is still owed** and should happen during
  Story 20.5's create-story elicitation, where the silhouette is the owner's call anyway.
* **The golden fingerprint is not offered as evidence.** Per R9 it builds from a synthetic temp fixture and cannot
  move for a spike; Story 23.1 had to retract that exact claim as structurally vacuous.

---

## 10. Non-invasiveness

`git status` at the end of this story shows modifications to `src/SpecScribe/HowToReadTemplater.cs`,
`src/SpecScribe/SiteNav.cs`, `tests/SpecScribe.Tests/SiteNavTests.cs`,
`tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` and
`tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`. **None of these are this story's.** They belong to a
concurrent session working on `5-6-how-to-use-specscribe-cli-guidance` (whose story file and `epics.md` entry are
modified in the same tree). This story's complete File List is in the story record; it contains **no** path under
`src/` or `tests/`.

The load-bearing evidence for non-invasiveness is that File List, confirmed by `git status` showing this story's
own additions confined to `spike/plotly/**`, `_bmad-output/implementation-artifacts/20-4-*`,
`docs/adrs/0012-*.md` and one entry appended to `.claude/launch.json`. **Not** the golden fingerprint (R9).

---

## 11. What this hands forward

| To | Hand-off |
|---|---|
| **Story 20.5** (the component) | Vendor the **standard** bundle, not `--strict` (§3.2). Fix data-contract findings **A–D** before anything else (§7). Hang the a11y layer on **`plotly_afterplot`**, never on Plotly's promise. Ship the `defs pattern > path { fill: none }` rule (§6). Set `outsidetextfont` as well as `insidetextfont`. Cancel the drill via `return false` from `plotly_<type>click` and re-apply the level yourself (§5.4). Decide **C** — leaf-only weights vs a parent-inclusive island — as a *visible geometry* decision. Decide whether the webview keeps the island (§4.4). Add `aria-level`/`aria-expanded`/`aria-posinset` (§5.1). |
| **Story 20.6** (twin audit) | §4.3 is the argument for the gate: with no server SVG beneath, a blocked or half-blocked chart is a **blank box**. Also: **CSP violations do not appear in console captures** — the twin audit must assert on the DOM, not on the console. |
| **Story 20.7** (rollout) | Expect **−4.8 MB** across the portal, break-even at 27 of 130 pages. `code-map.html` −3.5 MB and `git-insights.html` −1.5 MB carry most of it; the 128 story/epic sunbursts are ~1.8 MB combined. `impact-map.html` gains **zero** bytes (already client-rendered). **Preserve conditional emission** (`SiteGenerator.cs:1983`) or every golden fixture gains 1.2 MB. |
| **Story 23.4 / ADR 0005** | The joint CSP amendment needs **no relaxation of the policy string**: the shipped `script-src 'nonce-…'` and `style-src 'unsafe-inline'` are already sufficient for a Plotly hierarchy chart, header- and meta-delivered. Remaining unknown is narrow: `vscode-resource:` + Electron. |
| **Epic 17 / NFR10** | §8. |
| **Epic 24** | ADR 0012 §4 leaves the graph engine open. Nothing here settles it — Plotly has no force-directed trace, so Story 24.2's engine remains a separate question. |
