---
baseline_commit: 261b3008545a066ae1b08174b77df5b4abd4fb73
---

# Story 20.9: Colorized Hierarchies — Code Map and Git Insights Ownership Through the Component

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (+ its Story 20.4 addendum) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
**Blocked by:** **Story 20.7 must land first** — it builds the two component capabilities this story extends (the class-list color resolver and the client-side node filter, its F3), and it deletes the three planning entry points so the rollout-completeness allowlist this story empties actually exists. 20.7 is `ready-for-dev` and is itself blocked by **20.6** (the ADR 0013 §3 gate) and by **20.5** reaching `done` (it is `review`).
**Finishes:** Epic 20 AC#2 — *"exactly one implementation of a hierarchy chart exists in the codebase."* It is not true until this story lands.
**Baseline commit:** `261b300`

## Story

As a maintainer finishing the rollout ADR 0012 §2 requires,
I want the two colorize-driven hierarchy surfaces converted to the component and their renderers deleted,
so that "exactly one implementation of a hierarchy chart exists in the codebase" is finally true rather than nearly true.

---

## ⛔ Read first — what this story IS and IS NOT

**IS:** grow the component a **dimension contract** (11 live dimensions across two surfaces), convert **Code Map** (4 variant panels × 2 shapes = 8 charts) and **Git Insights ownership** (2 charts, 4 live modes), **build Git Insights a text twin it has never had**, retire both surfaces' SVG, delete the last four `Charts.cs` hierarchy entry points and the last two client renderers, and report the byte accounting — because this is where the entire Story 20.4 spike win actually lives.

**IS NOT** (each belongs to a named story; doing it here breaks a gate or blows scope):

| Not this story | Whose it is | Why |
|---|---|---|
| Converting the dashboard, epics index, epic detail, story detail or the Impact Map | **Story 20.7** | Already done by the time this starts. If any is still unconverted, this story is starting too early. |
| Deleting `Charts.Sunburst` / `EpicSunburst` / `TaskSunburst`, `initSunburstExplorers`, `renderSunburst`/`arcPath` | Story 20.7 | Same. |
| Building the class-list color resolver and the client node filter from scratch | Story 20.7 Tasks 1.1/1.3 | 20.7's F3 says explicitly they are *"designed for two consumers"* — you are the second. **Extend them; do not mint a parallel mechanism.** |
| Replacing Code Map's per-variant file table | Nobody — it **stays** | Story 20.6 D1: audited and kept, because it is genuinely richer than the generic nested twin (per-file git metrics). It *is* this surface's twin. |
| Removing the pure-CSS exclude-spec / exclude-tests panel toggle | Nobody — it **stays** (owner decision D2) | It works with JS off today and must keep working. |
| Pulling `RelatedWork.MaxEntriesPerGroup` | Owner, after 20.7 reports the post-retirement dashboard number | Story 20.7 D5. Nothing on these two pages touches it. |
| A per-file ownership *scoreboard* | Nobody — forbidden | FR-10 / ADR 0010 §4: descriptive attribution in every mode, never a ranking. Unaffected by rendering technology. |
| Bumping the vendored Plotly bundle | Its own decision | Pinned at 3.7.0. A bump invalidates every measured number in the 20.4 spike. |

---

## Owner decisions locked at create-story (2026-07-26)

Three, elicited against a code-level read of both surfaces and a **live `--deep-git` generation run** on this tree (714 pages, 68.4 s), not against the epic's table.

**D1 — Dimension contract: per-node metric bag + a declarative per-dimension rule.**
The payload carries each node's **raw generation-time values** — the same ones the SVG already embeds as `data-*` (`changes`, `churn`, `first`, `last`, `cochanged`, `filetype`/`filetype-label`; `share`, `dominant`, `contributors`, `last`, `owner[]`) plus the panel-wide `topAuthors` roster and `asof` day. Each dimension **declares** how a node's value maps to a **class list**, and the component resolves that class list through the shipped cascade (AD-7 — never a re-typed token value).
Rationale, and it is a hard constraint rather than a preference: **two of the eleven dimensions cannot be precomputed at all.** Staleness takes a free 1–60 month threshold from a `<input type="number">`; spotlight takes an arbitrary contributor from a roster. A precomputed class-per-dimension payload would have to drop or freeze both. The bucketing math already lives in JS (`specscribe.js:956` mirrors `Charts.Bucket` exactly) — this contract does not move logic client-side, it **names** what is already there and makes it generic.
**Determinism is unaffected (ADR 0012 §7 / ADR 0010 §3):** every value in the bag is computed once at generation time and embedded. Nothing re-derives from live git state or wall-clock `now` — `asof` is the tree's most-recent commit day, not today.

**D2 — Code Map: keep the four panels, lazy-mount on reveal.**
Four component instances, one per filter variant. Nothing a visitor sees changes, and **the pure-CSS exclude-spec / exclude-tests toggle keeps working with JavaScript off** — which is the point: it is the only filter on this page that is not JS-gated, and trading it for a byte win would take information away from a no-JS visitor to make a chart cheaper.
Accepted cost, stated plainly: four Plotly instances and four payloads on one page. Measured today, that page carries **2,712 file nodes across the four variants** (677/variant average) plus 512 directory rects. See **F1** — the reveal hook is not optional, it is what makes this decision implementable at all.

**D3 — Git Insights twin: the component's generic nested twin.**
`HierarchyExplorer.TextTwinHtml` as it stands — directory → file nesting, each file carrying its dominant author, share %, contributor count and last-active date as **prose**, in a collapsed `<details>` (Story 20.6 D3's default). One twin implementation site-wide; no new markup family to maintain.
This deliberately does **not** restore the ranked table Story 7.11 deleted on owner feedback, and it is not a per-directory rollup — a rollup would be shorter and more readable but would **not enumerate every node the chart draws**, failing ADR 0013 §2's completeness predicate. Completeness is the whole reason this twin exists.

---

## Acceptance Criteria

*Verbatim from [`epics.md` § Story 20.9](../planning-artifacts/epics.md). D1–D3 constrain how they are met; they do not amend them.*

1.
**Given** the component's single-`statusClass` color model and these two surfaces' live colorize dimensions
**When** the component is extended
**Then** it carries a **dimension contract**: a node may resolve its fill from any token family through the shipped cascade (never a re-typed token value, AD-7), a surface may offer several dimensions, and switching dimension re-colors in place without re-deriving geometry, re-counting against `ProjectCounts`, or issuing a fetch
**And** the non-color channel holds across every dimension (UX-DR17) — no state is signalled by hue alone.

2.
**Given** the Code Map (`CodeMapTemplater.cs` — 7 colorize dimensions × 4 filter variants = 8 charts, plus the drill breadcrumb and the per-variant file table)
**When** it is converted
**Then** every variant renders through the component with the standard selector ordering, its treemap default shape, and its drill behavior preserved
**And** its per-variant file table is **kept** as the text twin (Story 20.6 D1 — it is richer than the generic nested list, carrying per-file git metrics), audited complete before any SVG is retired.

3.
**Given** Git Insights ownership (`GitInsightsTemplater.cs` — 4 live modes, a contributor select, and a staleness threshold) which Story 20.6 F2 recorded as having **no text twin at all**
**When** it is converted
**Then** a complete, navigable, non-color twin is built for it **first**, verified live with JavaScript disabled, and only then is its SVG retired
**And** its stale ADR 0010 §2 progressive-enhancement doc comment is corrected to the ADR 0013 contract
**And** author information stays descriptive attribution in every mode, never a ranked scoreboard (FR-10, ADR 0010 §4 — unaffected by rendering technology).

4.
**Given** the last four `Charts.cs` hierarchy entry points (`CodeTreemap`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeOwnershipTreemap`) and the last two client renderers (`initCodeMapPanel`, `initOwnershipSunburst`)
**When** the conversion completes
**Then** they are removed, Story 20.7's rollout-completeness allowlist shrinks to empty, and no code path constructs a sunburst or treemap by any route other than the component — **verified by search, not assumed**
**And** the byte accounting is reported against the Story 20.4 spike's projection, since `code-map.html` (−3,493,000 B) and `git-insights.html` (−1,510,735 B) are where the entire portal-wide −4,787,124 B net delta actually lives.

---

## 🔴 Eight findings that change the work — read before planning

All code-verified 2026-07-26 against the live tree at `261b300` (clean), plus a real `--deep-git` generation run. **None appears in the epic, in either ADR, or in Story 20.6's or 20.7's text.**

### F1 — Plotly cannot lay out in a `display:none` container, and three of the four Code Map panels always are

`.codemap-view { display: none; }` (`specscribe.css:4222`) with a sibling-combinator reveal from the two checkboxes (`:4223-4226`). Exactly one panel is visible at any moment; the other three are `display:none` and therefore **zero-width**.

The component ships `responsive: true` (`specscribe.js:1977`) and sets **only** the height (`root.style.height = (cfg.size || 380) + "px"`, `:2301`) — width comes from the container. A zero-width container gives Plotly nothing to lay out against, and `responsive: true`'s window-resize listener **does not fire on a CSS-only reveal**. Mount all four eagerly and you get three broken charts that look fine until someone ticks a checkbox.

**What owner decision D2 requires:** a **reveal hook**. Listen to `change` on `.codemap-filter-checkbox` (they are real `<input>` elements — the toggle is pure CSS for *styling*, but the elements still fire events), and on reveal either perform the newly-visible instance's first mount or call `Plotly.Plots.resize` on an already-mounted one. Deferred first mount is the better default: three charts never drawn is three charts' worth of work not done on a 6 MB page.

**Two things that get *simpler*, and must not be missed in the same pass:** the component's own selector replaces `.codemap-shape-treemap`/`.codemap-shape-sunburst` (`:4321-4323`) and `.ownership-view-sunburst`/`-treemap` (`:6243-6245`). Both of those `display:none` pairs **go away entirely** — one instance switches trace type in place. So 8 Code Map charts become **4 instances** and 2 ownership charts become **1**. That is the real shape of the conversion, and it is worth saying in the Completion Notes.

**Verify this one in the browser, not by reasoning.** Tick each of the four checkbox combinations and confirm the revealed chart has real width and real sectors. This is precisely the class of defect CLAUDE.md § Verification exists for.

### F2 — `tokenFor` composes only `{fill, stroke}`, and both new families carry `fill-opacity` and `stroke-dasharray`

`tokenFor` (`specscribe.js:1799`) builds a probe as `<svg class="sunburst"><path class="sb-seg {cls}">` and returns `{ fill: cs.fill, stroke: cs.stroke }`. Two separate problems:

**(a) The probe's class string is hard-coded to the `sb-seg` family.** Story 20.7 Task 1.1 generalizes it to apply a per-node class list verbatim — **that is the seam you extend.** Verified good news: none of the two new families' rules need an ancestor selector, so a bare class-list probe resolves them correctly:

```
.codemap-cell.level-0 …           specscribe.css:4150-4155      (6 rules)
.codemap-cell.type-csharp …       specscribe.css:4163-4170      (8 rules)
.ownership-wedge.level-1 …        specscribe.css:6263-6272      (shared with .ownership-cell)
.ownership-wedge.owner-author-0 … specscribe.css:6288-6295      (7 hues + "other")
.ownership-wedge.spotlight-touched / .owner-spotlight-off / .owner-fresh / .owner-stale
                                  specscribe.css:6303,6304,6308,6309
```

**(b) `fill-opacity` is a real part of five fills and `tokenFor` never reads it.** `.codemap-cell.level-0` is `fill-opacity: 0.35`; `.level-none` and `.type-other` are `0.55`; `.owner-author-other` is `0.55`; `.owner-spotlight-off` is `0.35`. Plotly needs one paint per sector. Read `fill-opacity` off the probe and compose it into the returned color, or those five states render at **full opacity** — a silent fidelity regression that no test in this repo can see. (`fillFor`'s existing `transparent` fallback at `:1811-1818` handles `fill:none`, not partial opacity — it is not the same problem.)

**(c) `stroke-dasharray` is a non-color channel Plotly's `marker.line` cannot express** — exactly the limit Story 20.5 hit, whose answer was `marker.pattern` hatching. Three states depend on it: `.codemap-cell.type-other` (`2 1`), `.ownership-wedge.owner-author-other` (`2 1`), `.ownership-wedge.owner-stale` (`2 1`). Plus `.spotlight-touched` layers `stroke: var(--ink); stroke-width: 1.2` on top of a level ramp — a *second* channel on the same node. `PATTERN_SHAPE` (`:1790`) currently has four `sb-*` keys; it must become part of the dimension contract, keyed by class rather than by `statusClass`. And keep the 20.4-addendum fixes alive: `marker.pattern` needs an explicit per-sector `bgcolor` or its backing rect is black, and the shipped `defs pattern > path { fill: none; }` rule must still apply here.

### F3 — The non-color channel on these surfaces is TEXT, and it is rewritten on every dimension switch

This is the part AC#1's "the non-color channel holds across every dimension" actually refers to, and it is easy to read as "add hatching and move on."

Both shipped renderers do **two** things on every switch:

1. **Rewrite each node's accessible name** from a snapshotted base label — `"… — change frequency: level 3 of 4"`, `"… — file type: C#"`, `"… — no data for churn"` (`specscribe.js:1030-1077`); `"62% dominant-author share"`, `"dominant contributor: Matt Eland"`, `"Matt Eland worked on this file (14 days ago)"`, `"not touched in 9+ months"` (`:1304-1379`). Note the care already taken in that text and preserve it: the bucket **level** is the honest equivalent of what the color encodes, never the raw day-number the color does not literally represent (`:1070-1073`); a spotlight absence says *"not among this file's most-active tracked contributors"*, never the stronger and sometimes-false *"has not worked on this file"* (`:1344-1348`); an unknown last-touch date is an explicit *"(date unknown)"*, never coerced into the oldest bucket (`:1351-1356`).
2. **Swap which legend block is visible**, so the legend can never disagree with what is colored — Code Map toggles ramp ↔ discrete (`:1014-1017`) and rewrites the ramp's caption; ownership shows exactly one of four (`:1275-1281`).

The component must carry both. **A dimension whose fill changes but whose accessible name does not is a UX-DR17 failure that ships green.**

One trap in the same area, already paid for once: `.ownership-legend[hidden] { display: none; }` (`specscribe.css:6329`) exists only because `.ownership-legend`'s own `display: flex` (`:6316`) has the same specificity as the UA `[hidden]` rule and author CSS wins the tie — all four legends rendered at once, owner-reported, until that extra-specificity selector was added. Any legend markup the component emits inherits that hazard.

### F4 — Two hash schemes, four instances on one page, and an existing deep-link vocabulary

Code Map's drill is `#dir=<path>` driven by its own `<nav class="codemap-drill">` breadcrumb (`CodeMapTemplater.cs:143-147`, wired `specscribe.js:1087-1194`, `history.pushState`-based, `popstate`-restored). The component's drill is `#{hashKey}=…` with `.ss-hierarchy-breadcrumb`.

Consequences:

- **Four instances on `code-map.html` need four distinct `DomId`s and four distinct `HashKey`s**, or their deep links collide. The variant key (`full` / `no-spec` / `no-tests` / `no-spec-no-tests`) is the natural discriminator and is already in the markup as `data-view`.
- `.codemap-drill` and its crumb rendering are **deleted** — the component supplies the breadcrumb. AC#2's "drill behavior preserved" means the *behavior*, not this markup.
- Existing `#dir=` links stop resolving. Recommended: **retire `#dir=` and say so** in the Completion Notes rather than contorting `hashKey` to preserve a scheme that was never documented as stable. Raised as Open Question #2.
- Ownership gets drill-in **for the first time** — and that is the epic's own logged owner request, quoted in `epics.md`: *"click and drill into a directory and filter down to that level — at least in the sunburst. You can do this via Plotly and it's amazing."* (2026-07-22, Story 7.11 design session). This story is where that lands. Treat it as a deliverable, not a side effect.

### F5 — `HierarchyExplorerConfig.Size` is one square int; the Code Map treemap is 1000 × 640

`CodeMap.DefaultWidth = 1000`, `DefaultHeight = 640` (`CodeMap.cs:403,406`), passed at `CodeMapTemplater.cs:152`. `HierarchyExplorerConfig.Size` is a single `int` and the component applies it as **height only**, letting width fill the container (`specscribe.js:2301`). That is probably fine for a wide file-tree treemap — width fills, height 640 — and it is *not* fine to port `1000` into `Size` as if it were a width. The ownership sunburst's `size: 480` is a genuine square and is right as-is.

**Do not port either constant on faith.** Both were chosen for a static SVG that neither labelled nor drilled. Set them, then measure legibility live (Open Question #3).

### F6 — This repo's ownership data is near-degenerate, and the golden fixture never renders either page

Measured on this tree, 2026-07-26, `generate --deep-git`:

```
data-top-authors = ["Matt Eland","dependabot[bot]"]
```

The solo-repo reframe (`GitInsightsTemplater.cs:120-130`) does **not** trip, so the chart renders — but the second contributor is a bot that has touched exactly **two** files (`spike/vscode/package.json`, `package-lock.json`, commit `9f373ff`). Practical consequences for AC#3's live verification:

- **"Top contributors"** shows two colors, one of them covering ~2 of 1,334 files.
- **The spotlight picker lists a bot.** It is built from the union of every wedge's own `data-owner` roster (`specscribe.js:1236-1242`) and is correct as specified — flag it for the owner rather than filtering bots unilaterally (Open Question #4).
- **Staleness is the only mode with real spread** and is therefore the one that genuinely exercises the live-threshold path.

**Verify all four modes anyway, and report what you actually saw** — "all four modes verified" reads as richer evidence than this repo can supply. Say the roster is two deep and one is a bot.

Second half, from Story 20.6 Task 4.1 and confirmed: **the golden fixture is not a git repo and cites no real files, so `code-map.html` and `git-insights.html` never render in it.** The golden fingerprint is not this story's regression net at all. The net is the templater/`Charts` tests plus live verification — say so plainly instead of leaning on a hash that never covered these pages.

### F7 — 58 entry-point references plus 68 class-name assertions — larger than 20.7's F4

Counted in `tests/` on 2026-07-26:

| Target | Refs | Disposition |
|---|---:|---|
| `Charts.CodeTreemap` | 20 | rewrite-vs-delete |
| `Charts.CodeMapSunburst` | 13 | rewrite-vs-delete |
| `Charts.CodeOwnershipSunburst` | 14 | rewrite-vs-delete |
| `Charts.CodeOwnershipTreemap` | 11 | rewrite-vs-delete |
| **entry points, total** | **58** | |
| `codemap-cell` (CSS class assertions) | 31 | the server stops emitting it |
| `ownership-wedge` | 20 | same |
| `ownership-cell` | 17 | same |
| **class-name assertions, total** | **68** | |
| `initCodeMapPanel` / `initOwnershipSunburst` (JS string guards) | 2 / 4 | rewrite against the component's block |

The 68 class-name assertions are the trap 20.7's F4 did not have: they do not name a deleted symbol, so they will not surface as compile errors — they will fail at runtime as "expected markup not found", and it will be tempting to delete them as obsolete. **Many of them assert a fact** (this file appears, this file links to its code page, this file-type category is represented, this wedge carries its owner data) and those facts move to the payload and the twin. Split rewrite-vs-delete deliberately and **report the split and what coverage genuinely went away** — same discipline 20.7 Task 10.1 sets.

### F8 — This story deletes the last hand-rolled arc geometry in C#, and the rich hover cards are the byte story

**Arc geometry:** `Charts.BuildSunburstSvg` (`:3650`) has exactly **two** callers — `CodeMapSunburst` (`:3515`) and `CodeOwnershipSunburst` (`:3985`). Both are this story's. The planning sunbursts 20.7 deletes use a different path entirely. So `BuildSunburstSvg` and its span math (`:3713`) become unreachable **here**, and `Charts.cs` sheds its polar geometry completely. Confirm by search and say so — it is the concrete form of "exactly one implementation."

**Hover cards:** measured today, `code-map.html` carries **2,712** `data-tip-html` cards and `git-insights.html` **2,220**. `Charts.cs:3325` already names them *"the single biggest per-point cost."* Story 20.5 established the portal's own `.ss-tooltip` + `data-tip-html` + `hoverinfo: "none"` as the one tooltip system site-wide, so these cards must survive the conversion — but as **JSON strings in the island** rather than doubly-escaped HTML attributes. Whether that is cheaper or dearer is a measurement, not an assumption, and it is where AC#4's accounting will actually land. Keep `SelectDetailedCodeMapFiles`'s cap discipline (`Charts.cs:2916`, `MaxDetailedCodeMapFiles = 4000`) — it is what stops a large repo reintroducing the bloat.

---

## Measured starting state (2026-07-26, `dotnet run --project src/SpecScribe -- generate --deep-git`)

714 pages in 68.4 s, 0 errors. **These are the "before" numbers AC#4 reports against — recapture them yourself before you start, this tree moves.**

| Page | Bytes now | 20.4 spike projection | Nodes drawn |
|---|---:|---:|---|
| `code-map.html` | **6,020,916** | −3,493,000 | 4 panels · 5,424 `codemap-cell` (2,712 files × 2 shapes) · 512 `codemap-dir` · 2,712 table rows · 2,712 tip cards |
| `git-insights.html` | **2,129,514** | −1,510,735 | 1,334 `ownership-wedge` + 1,334 `ownership-cell` (1,334 files × 2 shapes) · 448 dir nodes · 2,220 tip cards |
| `index.html` | 939,998 | — | (Story 20.7's; context only) |
| `impact-map.html` | 323,722 | — | (Story 20.7's; context only) |

Portal net the spike projected: **−4,787,124 B**. These two pages alone exceed it, which is why the spike's reading is *amortised* and why 20.7 was told not to expect a byte win — **this story is where the win either shows up or does not.** Story 20.5 measured the real per-node cost at roughly **double** the spike's 195.4 B/node, so budget the island against ~390 B/node and report the actual figure either way.

---

## Tasks / Subtasks

### Task 0 — Entry conditions (blocking)

- [ ] 0.1 Confirm **Story 20.7 is `done`**, and that its **Task 1.1 class-list color resolver** and **Task 1.3 client node filter** are in the shipped `specscribe.js`. Grep for them; do not infer from the story's status. They are this story's foundation and 20.7's F3 committed them to two consumers.
- [ ] 0.2 Confirm **Story 20.6 is `done`** and read `20-6-text-twin-audit.md`. Its per-surface record is your permission slip. Its hypotheses for these two: **Code Map PROBABLE PASS** (the file table is genuinely server-complete), **Git Insights FAIL — no twin at all**. Re-verify both live; a hypothesis is not a clearance.
- [ ] 0.3 `git status` before starting. Another session's work has been in this tree at the start of every Epic 20 story. **Never `git reset --hard` / `git checkout --` / `git clean`.**
- [ ] 0.4 Re-capture the § Measured starting state numbers on your own tree. Reporting AC#4 against a figure from this document rather than from your own "before" run is the easiest way to publish a wrong delta.

### Task 1 — The dimension contract (AC: #1) — D1, F2, F3

- [ ] 1.1 **Metric bag on `HierarchyNode`.** Add an optional named-value bag (recommended: `IReadOnlyDictionary<string, string>? Metrics`, string-valued so day-numbers, counts and the `owner` JSON all travel one way) serialized into the island beside the existing fields. Code Map emits `path/lines/filetype/filetype-label/changes/churn/first/last/cochanged`; ownership emits `share/dominant/contributors/last/owner`. **These are the exact `data-*` the SVG already carries** (`Charts.cs:3021-3029`, `:3606-3614`, `:3849-3856`) — lift them, do not re-derive.
- [ ] 1.2 **Panel-wide constants** go on `HierarchyExplorerConfig`, not repeated per node: ownership's `topAuthors` roster and `asof` day (today `data-top-authors`/`data-asof` on the SVG root, `Charts.cs:3982-3983`). `asof` is the tree's most-recent commit day — **never wall-clock `now`** (FR31).
- [ ] 1.3 **Dimension declarations.** Add a per-instance list of dimensions to the config: key, human label, the legend block it owns, and its kind (numeric-ramp / categorical / fixed-cutoff / roster-relative / threshold-relative). The client rule for each maps a node's metric bag → a **class list** + an **accessible-name suffix**. Port the shipped rules verbatim — bucketing must still mirror `Charts.Bucket` (`specscribe.js:956`), the date dimensions must still scale against the file set's own `[min,max]` window while counts scale against `max` from 0 (`:1047-1057`), share must keep its fixed 25/50/75 cutoffs (`:1310`), and spotlight must keep `spotlightRecencyLevel`'s 30/90/180-day cutoffs (`:1333-1338`).
- [ ] 1.4 **Extend the color resolver for opacity and hatch (F2).** Have `tokenFor` read `fill-opacity` off the probe and compose it into the returned paint — five states are wrong without it. Extend `PATTERN_SHAPE` to be keyed by class rather than by `statusClass`, covering `type-other`, `owner-author-other` and `owner-stale`. Keep the 20.4-addendum fixes: explicit per-sector `marker.pattern.bgcolor`, and the shipped `defs pattern > path { fill: none; }` rule.
- [ ] 1.5 **Accessible names track the active dimension (F3).** Snapshot each node's base name once; recompose on every switch. Port the honest wording already in place — bucket **level** not raw value, "not among this file's most-active tracked contributors" not "has not worked on this file", explicit "(date unknown)" rather than an oldest-bucket coercion.
- [ ] 1.6 **Legend swap is part of the contract (F3).** Exactly one legend block visible per active dimension, and the ramp caption tracks the dimension name. Reuse the shipped emitters — `Charts.OwnershipLegend` / `-TopAuthorsLegend` / `-SpotlightLegend` / `-StalenessLegend` (`Charts.cs:4076,4110,4136,4164`) and `CodeMapTemplater.AppendLegend` / `AppendDiscreteLegend` — routed through the component's framing block rather than re-written. Watch the `[hidden]` vs `display:flex` specificity trap.
- [ ] 1.7 **Re-run Story 20.5's survival predicate after a dimension change.** It is a re-render the a11y layer must survive and 20.7's filter work did not exercise it: sectors > 0, `role="treeitem"` on every sector, non-empty `aria-label` on every sector, **exactly one `tabindex="0"`**. Announce the switch through the existing `.ss-hierarchy-live` region.
- [ ] 1.8 Generic by construction: **no surface name appears anywhere in the contract.** A `if (surface === "codemap")` branch inside the shared component is the drift this epic exists to end.

### Task 2 — Convert the Code Map (AC: #2) — D2, F1, F4, F5

- [ ] 2.1 New `HierarchyExplorer.ProjectCodeMap` over a `CodeMapVariant`: directory → file, sized by `Lines`, metric bag per 1.1, `Href` from the guarded `fileHref` resolver prefixed exactly as today (`CodeMapTemplater.cs:308-311`) — a null return stays a plain, focusable node, **never a broken link** (Story 7.1's guard).
- [ ] 2.2 Satisfy the four Story 20.4 invariants **by construction**: exactly one root, no `null` in `values`, `parent == Σ children` (via `HierarchyExplorer.Reparent` / `RollUpParentValues`), and an emitted `branchvalues` equal to `HierarchyExplorer.BranchValues`.
- [ ] 2.3 **Four instances, distinct `DomId` and `HashKey` per variant (F4)** — key them off `variant.Key`. `Shape: "treemap"` (Story 20.7 D2 — default shape stays per-instance); selector ordered Sunburst-then-Treemap like everywhere else. Delete `AppendShapeToggle` (`CodeMapTemplater.cs:174`) and the `.codemap-shape-*` CSS (`specscribe.css:4321-4323`); the component's selector replaces both.
- [ ] 2.4 **The reveal hook (F1).** Listen to `change` on the two `.codemap-filter-checkbox` inputs; on reveal, first-mount or `Plotly.Plots.resize` the newly-visible instance. Config-gated and generic — the component learns "I may be mounted inside a container that is hidden at load", not "I am on the Code Map". **Verify all four combinations live.**
- [ ] 2.5 Delete `<nav class="codemap-drill">` and its breadcrumb markup (`:143-147`); the component supplies the breadcrumb. The **behavior** is what AC#2 preserves.
- [ ] 2.6 **Keep the colorize `<select>`** (`AppendColorizeControls`, `:196`) and its seven options as the surface's dimension picker, wired to Task 1.3. Keep it emitted `hidden` and revealed on mount — a no-JS visitor must never see an inert control, and the server-baked default must still be correct without it. Keep the `hasMetrics == false` path: file type is the only option and the baked default (`:211-214`), plus the "git data unavailable" note (`:136`).
- [ ] 2.7 **`AppendFileTable` (`:277`) and `AppendCodeMapTablePager` (`:365`) stay untouched** — that table is this surface's twin (Story 20.6 D1) and `initCodemapTablePager` only paginates it. Re-verify completeness live with JS off before retiring the SVG: every file present, every link resolving, enough directory context to stand in for the hierarchy. A pager is not truncation.
- [ ] 2.8 `Size` per F5 — set it, then check legibility live. Do not port `1000`/`640` as if `Size` were a width.
- [ ] 2.9 Preserve the honest empty state: `variant.Map.IsEmpty` renders *"No files match this filter."* (`:99-104`) and `HierarchyExplorer.Render` returns `""` for an empty model. A missing panel is not an empty state (NFR8).

### Task 3 — Build the Git Insights twin, then convert (AC: #3) — D3

**Order matters and AC#3 states it: the twin lands *first*, is verified live with JS off, and only then does the SVG go.**

- [ ] 3.1 New `HierarchyExplorer.ProjectOwnership` over `codeMap.Roots` + `topAuthors`: directory → file, sized by `Lines`, metric bag per 1.1, `Href` from the guarded `fileHref` resolver as today (`GitInsightsTemplater.cs:174`).
- [ ] 3.2 **The twin (D3).** `TextTwinHtml` as it stands, collapsed `<details>` (Story 20.6 D3's default — do not set `ScreenReaderOnly` here; there is no visible companion panel on this page to justify it). Its prose must carry what the chart conveys: dominant author, share %, contributor count, last-active date. Reuse `Charts.DescribeOwnershipFile` — one vocabulary for chart, twin and tooltip.
- [ ] 3.3 **Verify the twin against the chart's own node set** before deleting anything: enumerate the payload with JS on, diff it against the twin's entries with JS off. **A count match is not a set match** (Story 20.6 Task 1.3b).
- [ ] 3.4 **Correct the stale doc comment (`GitInsightsTemplater.cs:14-20`)** — it states the page's no-JS contract as ADR 0010 §2's *"a real, useful default-mode chart … renders and works with JS off"*, which **ADR 0013 §4 supersedes**. Replace it with the ADR 0013 contract and point at `20-6-text-twin-audit.md`. If Story 20.6 Task 2.4 already did this, confirm rather than redo — and grep for other sites still asserting the superseded clause.
- [ ] 3.5 One instance replaces both charts. `Shape: "sunburst"` (this surface's shipped default). Delete the `.board-tabs` block (`:157-164`) and the `.ownership-view-*` CSS (`specscribe.css:6243-6245`) — the component's selector replaces them.
- [ ] 3.6 **Keep the mode selector, contributor select and threshold input** (`:137-151`) as this surface's dimension controls, wired to Task 1.3. Keep them emitted `hidden` and revealed on mount. The contributor list is still built from the **union of every node's own roster, alphabetical** — never a top-N ranking (FR-10, `specscribe.js:1234-1242`).
- [ ] 3.7 **`FR-10` holds in every mode.** "Top contributors" is a color palette, not a leaderboard; the spotlight is a filter, not a score. No mode may sort or rank contributors by volume in reader-facing output. Assert it.
- [ ] 3.8 **Keep the solo-repo reframe** (`:120-130`) exactly as it is — the `codeMapContributorCount == 1` gate returns before any chart, and its reasoning (it must read the *codeMap's* contributor population, not `insights.ContributorCount`) is a reviewed fix from 2026-07-22. It is an honest empty state, not dead code.
- [ ] 3.9 `Size` — the shipped 480 is a genuine square and is a reasonable start; verify live (F5).

### Task 4 — Delete the superseded implementations and prove it by search (AC: #4)

- [ ] 4.1 Delete `Charts.CodeTreemap` (`:2939`), `CodeMapSunburst` (`:3496`), `CodeOwnershipSunburst` (`:3957`), `CodeOwnershipTreemap` (`:4016`).
- [ ] 4.2 **`Charts.BuildSunburstSvg` (`:3650`) and its span math (`:3713`) become unreachable here (F8)** — its only two callers are 4.1's. Confirm by search and delete. `Charts.cs` sheds its polar geometry entirely; say so in the Completion Notes, it is the concrete form of Epic 20's goal.
- [ ] 4.3 Delete `specscribe.js`'s `initCodeMapPanel` (`:939`, block `924-1195`) and `initOwnershipSunburst` (`:1211`, block `1197-1403`), and the `.codemap-cell`/`.codemap-dir`/`.ownership-wedge`/`.ownership-cell` CSS families once nothing references them. **Keep `initCodemapTablePager`** — it serves the twin.
- [ ] 4.4 **Keep**, and do not sweep up as "part of the chart": `MaxDetailedCodeMapFiles` (`:2895`), `SelectDetailedCodeMapFiles` (`:2916`), `ComputeMaxChanges`, `CodeMapChangeLevelRange`, `IsCodeMapChangeLevelUnreachable`, `OrderBySignificance`, `Bucket` (5 other callers outside `Charts.cs`), `BuildOwnershipCard`, `BuildOwnerJson`, `BuildOwnershipDataAttrs`, `DescribeOwnershipFile`, `OwnershipShareLevel`, `BuildTreemapCard`, the four `Ownership*Legend`, `CodeFileType.AllCategories`, `GitMetrics.BuildTopAuthors`, `Plural`, `Framed`/`ChartMeta`/`WhyText`. The projectors, the legends, the tooltips and the file table all depend on them.
- [ ] 4.5 **Empty Story 20.7's rollout-completeness allowlist.** 20.7 Task 10.4 seeds it with these six symbols under a `[Story 20.9]` comment. Removing the last entry — and leaving the *test* in place, now asserting an empty allowlist — is this story's finish line and the epic's. Do not delete the test with the allowlist.
- [ ] 4.6 **Prove absence by search, not by assumption** (CLAUDE.md § Concurrent work — a `Charts.cs` edit has silently vanished in this repo). Grep every deleted symbol and every deleted CSS class across `src/`, `tests/`, and the extension shim; record the searches and their zero results in the Debug Log. **A build that compiles is not the same evidence** — and the 68 class-name assertions in F7 are exactly the kind that compile fine and are wrong.

### Task 5 — Hosts and parity (AC: #2, #3)

- [ ] 5.1 The webview presents the **text twin** — Story 20.7 D3's documented accepted degradation (ADR 0012 §5 / ADR 0013 §7), already registered in `HostRenderExceptions` by 20.7 Task 9.1. Confirm these two surfaces are covered by that entry or extend it; an unregistered divergence is a bug.
- [ ] 5.2 Verify the webview reaches the twin: the island strip is a regex over `<script type="application/json">` (`WebviewRenderAdapter.cs`) and the twin is `<details>` markup, so it should survive — **confirm it**, and confirm the twin's file links resolve under the webview's path rewriting.
- [ ] 5.3 SPA: island and twin must survive content capture, and `specscribe:content-swapped` must re-init **all five** instances (four Code Map + one ownership). Extend the existing `SiteGeneratorSpaTests` island-survives-capture test rather than adding a parallel one. Note the known SPA scale hazard on this exact page (`code-map.html` measured 82.5 MB at SPA scale in Story 6.6) — report whether this conversion helps it.
- [ ] 5.4 `RenderParity` / `RenderSectionParityTests` green across `html`, `spa`, `webview`.

### Task 6 — Tests (AC: #1, #2, #3, #4)

- [ ] 6.1 Work through F7's 58 entry-point references **and** its 68 class-name assertions. Rewrite fact-asserting tests against the payload and the twin; delete geometry-asserting ones. **Report the split** — how many rewritten, how many deleted, what coverage genuinely went away.
- [ ] 6.2 Per new projector (`ProjectCodeMap`, `ProjectOwnership`), assert the four 20.4 invariants: exactly one root, no `null` in values, `parent == Σ children`, emitted `branchvalues == HierarchyExplorer.BranchValues`.
- [ ] 6.3 **Per dimension** (all eleven), assert the resolved class list is what the shipped renderer produced for the same input — the fills must be **unchanged** by the conversion, not merely plausible. Include the five `fill-opacity` states and the three `stroke-dasharray` states from F2 explicitly; they are the ones that regress silently.
- [ ] 6.4 **Per dimension**, assert the accessible name changes and carries the dimension's own text equivalent (F3). This is AC#1's non-color clause made testable.
- [ ] 6.5 A **completeness invariant** for the ownership twin: every payload node appears in the twin with a prose status and a resolving href — the analogue of `Projector_NodeSet_EqualsTheWedgesTheSvgDrew`, retargeted at the twin (Story 20.7 Open Question #2's recommended answer).
- [ ] 6.6 The **rollout-completeness test with an empty allowlist** (4.5). Assert no source file outside `HierarchyExplorer` constructs any hierarchy chart.
- [ ] 6.7 Keep the shipped privacy guard green now that five more instances construct a Plotly config: `displayModeBar: false`, `plotlyServerURL: ''`, `topojsonURL: ''`, and no `sendDataToCloud` / `cdn.plot.ly` / `plotly.com` string anywhere in the shipped JS.
- [ ] 6.8 Assert the honest empty states survive: `Map.IsEmpty` → "No files match this filter."; `codeMap.IsEmpty` → "No file change data available."; `codeMapContributorCount == 1` → the solo-repo note; `hasMetrics == false` → file type as the only dimension plus the git-unavailable note.
- [ ] 6.9 **Do not unit-test the JS** — SSR-first, no JS harness. Task 7 is the verification for Tasks 1–3's client behavior. Say so plainly rather than implying coverage that does not exist.

### Task 7 — Live-browser verification (AC: #2, #3) and the accounting (AC: #4)

- [ ] 7.1 Generate to `SpecScribeOutput/` **with `--deep-git`** — without it `git-insights.html` does not render at all and Code Map falls back to file-type-only. Never `--output docs/live`. Serve via `.claude/launch.json` → `specscribe-output`, port 8099.
- [ ] 7.2 **Code Map, JS on:** all **four** checkbox combinations — chart renders with real width and real sectors (F1), selector switches shape in place, drill/breadcrumb/Escape work, hash round-trips per instance, all **seven** dimensions recolor with legend and accessible names tracking, zero console errors, survival predicate holds. Four passes, recorded separately.
- [ ] 7.3 **Git Insights, JS on:** all **four** modes, the contributor select, and the threshold input at several values. Drill into a directory — the owner's original Story 7.11 request (F4). Record honestly that the roster is two deep and one is a bot (F6); do not report thin evidence as rich.
- [ ] 7.4 **Both surfaces, JS genuinely off in the browser** (not assumed): the twin is present, complete, navigable, non-color; the chart host leaves no chart-sized blank box; **no inert controls are visible** — the colorize select, the mode select, the contributor select and the threshold input must all still be `hidden`. For Code Map, confirm the **pure-CSS filter still works with JS off** (D2's whole justification) and that each variant's file table is complete. This is the ADR 0013 §3 gate being exercised, and it is the last moment before the SVG is gone.
- [ ] 7.5 **Colorway audit** on both surfaces, built at runtime from the shipped cascade — the `.codemap-cell.*` and `.ownership-*` families, including `fill-opacity`. Zero foreign colors, text fills included. Re-derive the allowlist from the shipped CSS, never from a typed token value.
- [ ] 7.6 **Take screenshots.** The owner has still never seen a pixel of this component — 20.4, 20.5 and 20.7 all owed one. Try hard; if the pane still refuses to composite, say so and fall back to computed-geometry evidence rather than skipping it quietly a fourth time.
- [ ] 7.7 **The byte accounting (AC#4).** Report per page: before, after, delta, and the split between island / twin / removed SVG. Compare against the spike's `code-map.html` −3,493,000 B and `git-insights.html` −1,510,735 B and the **−4,787,124 B** portal net. Say whether the projection held — and if the island came in nearer 20.5's measured ~390 B/node than the spike's 195.4 B/node, say that too. **This is the story that settles it**; do not report a directionally-pleasing number without the breakdown.
- [ ] 7.8 **Golden fingerprint.** It should barely move: the fixture is not a git repo, so neither page renders in it (F6). If it moves substantially, that is a signal something else changed — investigate rather than re-baselining. If you do regenerate, confirm stable across two repeated runs and **name whose concurrent changes it sits on top of**.
- [ ] 7.9 Full suite, real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.
- [ ] 7.10 **State in the Completion Notes that Epic 20 AC#2 is now satisfied**, and name the evidence: the empty allowlist, the zero-result searches, and `BuildSunburstSvg` gone. Three stories have had to say "not yet" — this is the one that gets to say yes, and it should say it with proof rather than by assertion.

---

### Review Findings

*(populated by code-review at epic end — Epic 20's review runs once every story is complete and the owner is satisfied)*

---

## Dev Notes

### Why this story exists, in one paragraph

Story 20.5 built the component; 20.7 converted the five surfaces whose color is a single lifecycle token. These two are what was left, and they were left because their color is not a property of the node — it is a property of the node **crossed with a dimension the reader is choosing right now**. Seven dimensions on one page, four modes plus two live inputs on the other. That is genuinely new component capability rather than a port, which is why owner decision D1 at Story 20.7 split them out. It is also where the entire Story 20.4 byte case lives: `code-map.html` at 6.02 MB and `git-insights.html` at 2.13 MB together exceed the whole projected portal saving, so "did Plotly pay for itself" is a question only this story can answer. And it is where Epic 20's stated goal stops being aspirational — after Task 4, `Charts.cs` has no hierarchy entry point and no arc geometry at all.

### Architecture compliance

- **ADR 0012 §2** — one component is the only route to a hierarchy chart. **This story is where that becomes true.** §2 also puts the legend inside the component's framing block, which Task 1.6 honors for six more legend blocks.
- **ADR 0012 §3** — `navigate` mode on both surfaces (a file node opens its code page). Drill-in stays a distinct affordance from activation; Plotly drills on click by default and the component must keep intercepting it, or a node silently does two things.
- **ADR 0012 §6** — presentation is SpecScribe's tokens, never Plotly's colorways. Task 1.4 is the single highest-risk place in this story for a color value to get typed; it must not.
- **ADR 0012 §7 / ADR 0010 §3** — data computed once at generation time and embedded. The metric bag is embedded values; a dimension switch is a pure re-read. **No fetch, no live git, no wall-clock `now`** — `asof` is the tree's most-recent commit day.
- **ADR 0010 §4 / FR-10** — descriptive attribution, never a productivity ranking. Rendering technology does not change this (Task 3.7).
- **ADR 0013 §2/§3** — the twin contract and the hard per-surface live JS-off gate. Git Insights has no twin today; building one is a **prerequisite**, not polish. **ADR 0013 §4 supersedes ADR 0010 §2** — Task 3.4 corrects the last site still asserting it.
- **ADR 0013 §6** — the fingerprint replacement is 20.6's and already landed. Note honestly that it does not cover these two pages (F6).
- **ADR 0002 / AD-2** — payload and config are host-neutral view-model data, built in the emitter, routed through the templaters. Never ad-hoc string-building in an adapter.
- **AD-7** — every color resolves through the shipped cascade. A re-typed token survives a token change and quietly lies about it.
- **NFR-5 as amended by ADR 0013** — JS-off may lose the visualization; it must never lose **information** or **navigation**. On Code Map that also means the pure-CSS filter keeps working (D2).
- **NFR-3** — offline/`file://`-capable: no CDN, no fetch, no external origin.
- **UX-DR17 / UX-DR19 / UX-DR21** — never color-only across **eleven** dimensions (F3); every metric has a non-color text equivalent; one primary representation with alternates behind the standard toggle.
- **FR31** — generation-time determinism; identical output on a from-scratch regen.
- **Story 7.1 link guard** — a `fileHref` resolver returning null leaves a plain, focusable node. Never a broken link, in either projector.

### Anti-patterns to prevent

1. **Building a second color resolver or a second client filter.** Story 20.7's F3 committed both to two consumers. You are the second. Extend.
2. **Mounting all four Code Map instances eagerly.** Three are `display:none` and Plotly cannot lay out in one (F1).
3. **Assuming a class-list probe is enough.** `fill-opacity` and `stroke-dasharray` are part of five and three fills respectively (F2), and neither is in `tokenFor`'s return today.
4. **Treating the non-color channel as "add hatching".** On these surfaces it is the per-dimension accessible name plus the legend swap (F3). A dimension whose fill changes and whose name does not is a UX-DR17 failure that ships green.
5. **Replacing Code Map's per-variant file table** with the generic nested twin. Story 20.6 D1 keeps it; it is richer.
6. **Turning the exclude-spec / exclude-tests filter into a JS filter.** Owner decision D2 keeps it pure CSS precisely because it is the one filter that works with JS off.
7. **Deleting the Git Insights SVG before its twin exists and has been verified live with JS off.** AC#3 states the order. It is the ADR 0013 §3 gate.
8. **Deleting the 68 class-name assertions as obsolete** because they no longer match (F7). Many assert a fact that moved to the payload or the twin.
9. **Sweeping up the surviving helpers** (`SelectDetailedCodeMapFiles`, `ComputeMaxChanges`, `Bucket`, the four ownership legends, `BuildOwnershipCard`…) as "part of the chart" (4.4). `Bucket` alone has five callers outside `Charts.cs`.
10. **Deleting the rollout-completeness *test* along with its allowlist.** The empty allowlist is the assertion (4.5).
11. **Ranking contributors** in any mode. FR-10 is unaffected by rendering technology.
12. **Reporting "all four ownership modes verified"** without saying the roster is two deep and one is a bot (F6).
13. **Reporting a byte delta without the island/twin/SVG breakdown.** This story is the spike's reckoning; a single pleasing number is not the deliverable.
14. **Proving a symbol is gone by "the build passed."** Grep, and record the searches.
15. **`git reset --hard` / `git checkout --` / `git clean`.** This has already destroyed real work mid-story in this repo.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `HierarchyExplorer.Render` / `IslandHtml` / `TextTwinHtml` | `HierarchyExplorer.cs:303,377,429` | the whole framed block; extend it, never build a second one |
| `HierarchyExplorer.Reparent` / `RollUpParentValues` | `HierarchyExplorer.cs:189,221` | the one place 20.4's Findings A and C are satisfied; every new projector goes through it |
| `HierarchyExplorer.BranchValues` | `HierarchyExplorer.cs:117` | assert against the constant, never a literal `"total"` |
| `HierarchyExplorer.ShortLabelFor` | `HierarchyExplorer.cs:534` | `uniformtext` draws every label at ONE size — a long path suppresses labels chart-wide |
| Story 20.7's class-list resolver + client node filter | `specscribe.js` (20.7 Tasks 1.1/1.3) | designed for two consumers; you are the second |
| `Charts.Bucket` | `Charts.cs` | the server's bucketing rule the client mirrors exactly (`specscribe.js:956`) |
| `Charts.SelectDetailedCodeMapFiles` / `MaxDetailedCodeMapFiles` | `Charts.cs:2916,2895` | the per-node detail cap; `null` is the "no cap" sentinel |
| `Charts.BuildOwnershipCard` / `BuildTreemapCard` / `DescribeOwnershipFile` | `Charts.cs` | the rich hover cards and their prose — one vocabulary for chart, tooltip and twin |
| `Charts.Ownership*Legend` ×4 | `Charts.cs:4076,4110,4136,4164` | the four mode legends; route them, don't rewrite them |
| `CodeMapTemplater.AppendFileTable` / `AppendCodeMapTablePager` | `CodeMapTemplater.cs:277,365` | Code Map's twin (20.6 D1) — untouched |
| `.ss-tooltip` + `data-tip-html` + `hoverinfo:"none"` | 20.5's tooltip decision | one tooltip system site-wide; swapping engine never swaps tooltip look |
| `specscribe:content-swapped` | `specscribe-spa.js` | five instances now depend on it |
| `AssetManifest.HierarchyEngineNeeded` + `SiteGenerator.EnsureHierarchyEngine` | 20.5's asset seam | **disk is the truth**, not the in-memory copied flag (20.5's watch-session defect) |
| `HostRenderExceptions.Registry` | `HostRenderException.cs` | the ONLY legitimate way a surface diverges |
| `GitMetrics.BuildTopAuthors` | `GitMetrics.cs` | the bounded discrete-palette roster; the spotlight picker is a separate, unbounded, alphabetical union |

### Files being modified — current state

*Every line reference below was verified 2026-07-26 against the live tree at `261b300`. **Verify each one again before relying on it** — another session may be editing this tree, and Story 20.7 lands between this draft and your start, moving several of these files substantially.*

- **`src/SpecScribe/HierarchyExplorer.cs` (561 lines) — UPDATE.** `HierarchyNode` `:41` gains the metric bag; `HierarchyExplorerConfig` `:65` gains the dimension list + panel constants (note Story 20.6 Task 3.1 also adds a twin-display field, and 20.7 removes `Render`'s `fallbackHtml`). `ProjectDashboard` `:130`, `Reparent` `:189`, `RollUpParentValues` `:221`, `Render` `:303`, `IslandHtml` `:377`, `TextTwinHtml` `:429`, `StatusLabelFor` `:556`. Two new projectors here (or in a partial if the file grows past comfortable).
- **`src/SpecScribe/Charts.cs` (4,896 lines) — UPDATE (heavily subtractive).** Delete `CodeTreemap` `:2939`, `CodeMapSunburst` `:3496`, `CodeOwnershipSunburst` `:3957`, `CodeOwnershipTreemap` `:4016`, and `BuildSunburstSvg` `:3650` + its span math `:3713` once their two callers go (F8). **Keep** `MaxDetailedCodeMapFiles` `:2895`, `SelectDetailedCodeMapFiles` `:2916`, `BuildTreemapCard`, `BuildOwnershipCard`, `BuildOwnerJson`, `BuildOwnershipDataAttrs` `:3849`, `DescribeOwnershipFile`, `OwnershipShareLevel`, `OwnershipLegend` `:4076`, `OwnershipTopAuthorsLegend` `:4110`, `OwnershipSpotlightLegend` `:4136`, `OwnershipStalenessLegend` `:4164`, `ComputeMaxChanges`, `CodeMapChangeLevelRange`, `IsCodeMapChangeLevelUnreachable`, `OrderBySignificance`, `Bucket`. The `data-*` builders at `:3021-3029` / `:3606-3614` / `:3849-3856` are the metric bag's source of truth — lift, do not re-derive.
- **`src/SpecScribe/CodeMapTemplater.cs` (373 lines) — UPDATE.** `RenderPage` `:23`; `AppendFilterCheckbox` `:72` **stays** (D2); `AppendVariantPanel` `:95` is the main rewrite — `AppendShapeToggle` `:174` **deleted** (component selector), `AppendColorizeControls` `:196` **kept and rewired**, `AppendLegend` `:233` / `AppendDiscreteLegend` `:254` **kept and routed**, the drill `<nav>` `:143-147` **deleted**, the two chart calls `:152,158` replaced by one component instance. `AppendFileTable` `:277` and `AppendCodeMapTablePager` `:365` **untouched** — that is the twin. Empty state `:99-104` preserved. Route the component HTML through the view model, not ad-hoc in the templater's string building where it can (AD-2).
- **`src/SpecScribe/GitInsightsTemplater.cs` (236 lines) — UPDATE.** Stale doc comment `:14-20` **corrected** (Task 3.4, AC#3). `AppendOwnershipSection` `:87`: solo-repo gate `:120-130` **untouched**, controls `:137-151` **kept and rewired**, `.board-tabs` `:157-164` **deleted**, the two chart calls `:174,179` replaced by one instance, the four legends `:186-189` **routed through the component's framing block**. `AppendActivitySection` `:203` (the commit heatmap) is **not a hierarchy chart** — do not touch it.
- **`src/SpecScribe/assets/specscribe.js` (2,953 lines) — UPDATE.** Component block `~1691-2372`: `STATUS_CLASS` `:1781`, `PATTERN_SHAPE` `:1790`, `tokenFor` `:1799`, `fillFor` `:1811`, config `responsive:true` `:1977`, `root.style.height` `:2301`. **Delete** `initCodeMapPanel` `:939` (block `924-1195`) and `initOwnershipSunburst` `:1211` (block `1197-1403`) — read both in full first; they contain the eleven dimension rules and the honest label wording Task 1.3/1.5 must port. **Keep** `initCodemapTablePager` `:920`.
- **`src/SpecScribe/assets/specscribe.css` — UPDATE.** Delete `.codemap-cell.*` `:4149-4178`, `.codemap-shape-*` `:4321-4323`, `.codemap-sunburst*` `:4326-4335`, `.ownership-wedge`/`.ownership-cell` families `:6261-6310`, `.ownership-view-*` `:6243-6245`. **Keep** the legend swatch rules (`.codemap-legend-swatch.*` `:4242+`, `.ownership-legend*` `:6316-6353` incl. the `[hidden]` specificity fix at `:6329`) — the legends survive, and `tokenFor` resolves the chart fills through the cell/wedge rules, so **delete those only after the component reads them**, not before.
- **`src/SpecScribe/CodeMap.cs` — READ.** `DefaultWidth` `:403` = 1000, `DefaultHeight` `:406` = 640, `Layout()` `:430` (F5).
- **`src/SpecScribe/WebviewRenderAdapter.cs` / `HostRenderException.cs` — READ, then confirm 20.7's entry covers these two surfaces.**
- **Tests — UPDATE, heavily.** F7's 58 + 68 references, plus `HierarchyExplorerTests`, `StylesheetTests` (the JS/CSS string guards), `SiteGeneratorSpaTests`, `RenderParityTests`, `RenderSectionParityTests`, and the Code Map / Git Insights templater tests.

### Project Structure Notes

No new page, no new nav entry, no new dependency, no new asset. Net **large subtraction** in `Charts.cs` (four entry points plus all remaining arc geometry) and `specscribe.js` (two renderers, ~470 lines); net addition of two projectors and one component capability. `HierarchyExplorer.cs` may warrant splitting per-surface projectors into a partial once it carries five — a judgement call, not a requirement.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; JS is verified in a live browser and its *content* asserted by string tests over the shipped asset (`StylesheetTests` is the established pattern for both CSS and JS guards). **The golden fingerprint is not this story's net** — the fixture is not a git repo, so neither page renders in it (F6). The net is the templater/`Charts` tests plus Task 7's live verification. Say so plainly rather than implying coverage that does not exist.

### Previous story intelligence

**Story 20.7 (`ready-for-dev`, NOT STARTED at draft time)** — this story's direct dependency and its foundation. Its **F3** commits the class-list color resolver and the client node filter to two consumers, naming this story as the second. Its **F1** moves the legend into the component's framing block, which is the seam Task 1.6 uses for six more legend blocks. Its **D2** fixes selector ordering site-wide with default shape per-instance — Code Map keeps treemap, ownership keeps sunburst. Its **D3** takes the webview text-twin degradation and registers it in `HostRenderExceptions`; confirm it covers these two rather than adding a second entry. Its **Task 10.4** seeds the rollout-completeness allowlist with exactly this story's six symbols. **If 20.7 has not landed, do not start** — you would be building its capabilities twice.

**Story 20.6 (`ready-for-dev`)** — the ADR 0013 §3 gate. Its **D1** keeps Code Map's per-variant file table as that surface's twin. Its **F2** is the reason AC#3 exists at all: Git Insights has no twin, and `GitInsightsTemplater.cs:14-20` still states the superseded ADR 0010 §2 contract. Its **F3** confirms Code Map's table is genuinely server-complete — every file ships as a plain `<tr>`; the pager is not truncation. Its **Task 4.1** records that the golden fixture never renders either of these pages.

**Story 20.5 (`review`)** — the component, and four facts that land here. (a) `uniformtext` draws every label at ONE size, so a long label suppresses labels chart-wide — `ShortLabelFor` exists for that, and a deep file path is exactly the case it was built for. (b) **CSS cannot stroke a Plotly sector**; the selection ring rides `marker.line`, and a `transition` on a Plotly-owned property reads back through `getComputedStyle` as never applied in a non-compositing pane. (c) The dashboard mount **added** bytes (island 31,404 B + twin 24,168 B), so budget the island against roughly **double** the spike's 195.4 B/node. (d) A watch-session topology change wipes the output root and deletes an asset the in-memory "copied" flag still claims — `EnsureHierarchyEngine` treats the **disk** as truth.

**Story 20.4 (`done`, code-reviewed)** — the measured facts: plotly.js **3.7.0** pinned, standard bundle **1,223,515 B**, **CSP violations do not appear in console captures** (a test that greps the console passes while the chart is blank — ask the DOM), promises resolve **off an animation frame** so hang everything on `plotly_afterplot`, `marker.pattern` needs an explicit `bgcolor` or its backing rect is black, Plotly emits the hatch `<path>` with no `fill` so `defs pattern > path { fill: none; }` must ship, and the **−4,787,124 B** portal delta is **amortised** — these two pages are where it lives.

**Story 7.12 (`done`)** — the owner-directed merge that made Code Map ONE panel with "what to view" (colorize) and "how to view it" (shape) as orthogonal axes, after they felt "artificially split across different surfaces." Preserve that framing; the component's selector is the "how" axis and the colorize select is the "what" axis.

**Story 7.11 (`done`)** — deleted **both** prior ownership tables on owner feedback ("the two chart forms plus their rich per-file tooltips are the surface now"), which is exactly why there is no twin. Owner decision D3 does not restore them; it adds the component's own twin. The same session logged the drill-in request F4 quotes.

**Story 6.6 (`done`)** — recorded `code-map.html` at **82.5 MB** at SPA scale as a perf defect. Task 5.3 should report whether this conversion moves it.

**Owner workflow (`CLAUDE.md`)** — the post-implementation round where the owner verifies rendered behavior is the **designed gate**, not rework. F6's thin roster and F4's retired `#dir=` scheme will both draw commentary. Leave both easy to adjust.

### Git intelligence summary

Baseline `261b300` ("20.5, 20.7, 22.2, 23.2") — a single commit carrying four stories' worth of work, which is structural here because code review runs at epic end. **Scope any later review of this story by its own File List and declared symbols, never by a commit range** (CLAUDE.md § Scoping a code review), and state the exclusion in the review record.

Two things from recent history bear on this story: `98a90c6` made the golden fingerprint **portable** by fixing checkout *and* date dependence — do not reintroduce either; and Story 25.1 stood up the repo's **first build/test CI**, so a regression here fails a gate rather than only a local run. The working tree was **clean** at `261b300` when this story was drafted — the first Epic 20 story for which that has been true. Do not assume it still is.

### Latest technical information

**Nothing needs re-researching, and that is the instruction.** plotly.js is pinned at **3.7.0** (MIT, released 2026-07-03); Story 20.5 checked for newer on 2026-07-25 and found none. **A version bump invalidates every measured number in the 20.4 spike and must be its own decision, not a side effect of this rollout.**

Three 3.7.0 facts stay load-bearing on all five new instances: **`displayModeBar: false`** is a privacy requirement, not a cosmetic default (3.7.0's `sendDataToCloud` button uploads the chart to Plotly Cloud); `plotlyServerURL: ''` / `topojsonURL: ''` keep the portal offline-capable (NFR-3); and **`Plotly.Plots.resize`** is the documented way to re-lay-out a plot whose container changed size without a window event — which is precisely F1's reveal hook.

The vendoring tool (`tools/plotly-vendor/`) is not touched: no new trace family is needed — `sunburst` and `treemap` are already in the bundle and are the only two shapes here.

### ⚠️ Concurrent work — read before you start

The tree was **clean** at `261b300` when this story was drafted, which is unusual for this epic and should not be assumed to persist. Three Epic 20 stories are `ready-for-dev` against overlapping files (20.6, 20.7, 20.8) and **20.7 must land in `HierarchyExplorer.cs`, `Charts.cs`, `specscribe.js` and `specscribe.css` before this story starts** — every line reference in this file will have moved by then.

Per CLAUDE.md § Concurrent work:

- **Grep-verify every symbol and line reference before relying on it.** A `Charts.cs` edit has silently vanished in this tree, and `RelatedWorkCards.cs` changed between two reads during Story 20.8's drafting.
- **Verify after every edit** — do not trust that a write landed because the tool returned success and the build passed.
- **Expect the build to be transiently broken by someone else's rename.** Wait; do not reset.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.9] — the four ACs verbatim; the rollout inventory's "Converted by" column and the colorize-model rationale for the split
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §2 the component contract, §3 the mode contract, §6 tokens not colorways, §7 generation-time determinism, and the Story 20.4 addendum (pattern `bgcolor`, `defs pattern > path`, `plotly_afterplot`)
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 the four-part twin contract, §3 the hard per-surface live JS-off gate, §4 supersedes ADR 0010 §2, §6 the fingerprint replacement
- [Source: `docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md`] — §3 embedded generation-time data, §4 the no-ranking rule (both stand); §2 superseded
- [Source: `_bmad-output/implementation-artifacts/20-7-site-wide-hierarchy-rollout.md`] — D1 (the split that created this story), D2 (selector ordering), D3 (webview), F3 (the two capabilities designed for two consumers), Task 10.4 (the allowlist this story empties)
- [Source: `_bmad-output/implementation-artifacts/20-6-text-twin-audit-and-fingerprint-replacement.md`] — D1 (Code Map's table kept), F2 (Git Insights has no twin; the stale doc comment), F3 (the table is server-complete), Task 4.1 (the fixture renders neither page)
- [Source: `_bmad-output/implementation-artifacts/20-5-hierarchy-explorer-component.md`] — the component as built, `shortLabel`, the tooltip decision, the real per-node byte cost
- [Source: `_bmad-output/implementation-artifacts/20-4-spike-report.md`] — the measured Plotly facts and the amortised byte reading this story reckons with
- [Source: `CLAUDE.md`] — § Concurrent work on shared `main`, § Verification (live browser), § Scoping a code review, § Decision records
- Code: `HierarchyExplorer.cs:41,65,117,130,189,221,303,377,429,534,556`, `Charts.cs:2895,2916,2939,3021,3325,3496,3606,3650,3713,3849,3957,3982,4016,4076,4110,4136,4164`, `CodeMapTemplater.cs:23,72,95,143,152,158,174,196,233,254,277,365`, `GitInsightsTemplater.cs:14,87,120,137,157,174,179,186,203`, `CodeMap.cs:403,406,430`, `specscribe.js:920,939,956,1014,1030,1070,1087,1162,1197,1211,1234,1275,1304,1333,1340,1364,1781,1790,1799,1811,1977,2301`, `specscribe.css:4149,4203,4222,4242,4321,4326,6241,6261,6288,6303,6316,6329`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Should the four Code Map instances share one payload?** Recommended: **no.** The four variants are precomputed filter combinations with genuinely different file sets, and D2 keeps them as four panels. One shared payload plus a client filter would be smaller but would put the filter behind JS, which is exactly what D2 declined. Revisit only if Task 7.7's measurement shows the four payloads dominate the page.
2. **Does `#dir=` deep-linking survive?** Recommended: **retire it**, and say so. It was never a documented stable scheme, the component's `hashKey` idiom replaces it, and contorting `hashKey` to preserve it would fork the deep-link vocabulary across surfaces. Raised because someone may have bookmarked one.
3. **Are 640 px (Code Map) and 480 px (ownership) still the right sizes?** Recommended: **raise both modestly and verify live.** Both were chosen for a static SVG that neither labelled nor drilled. A file-tree treemap that now shows in-sector labels needs room for them.
4. **Should `dependabot[bot]` be filtered out of the contributor roster?** Recommended: **not in this story.** It is a data question (which authors count as contributors) with a real answer either way, it affects the shipped SVG identically today, and folding it into a rendering conversion would hide a product decision inside a refactor. Flagged because the owner will see a bot in the spotlight picker during verification (F6) and should decide deliberately.
5. **Does ownership want the reveal hook too?** Recommended: **no** — its panel is not inside a `display:none` container; only the shape toggle was, and the component's selector replaces that. Stated so the answer is on the record rather than rediscovered.

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

---

## Change Log

- 2026-07-26 — Story 20.9 drafted (create-story). Context assembled from ADR 0012 (+ its 20.4 addendum), ADR 0013, ADR 0010 §3/§4, the Epic 20 rollout inventory as amended by Story 20.7's owner decision D1, Stories 20.4/20.5/20.6/20.7/7.11/7.12 as they actually stand, **a code-level read of both surfaces' emitters and both client renderers in the live tree at `261b300`**, and **a real `--deep-git` generation run** (714 pages, 68.4 s) so the byte accounting AC#4 demands starts from measured numbers rather than the spike's projection. Three owner decisions elicited and locked: **D1** the dimension contract is a **per-node metric bag plus a declarative per-dimension rule**, because two of the eleven dimensions — staleness (a free 1–60 month threshold) and spotlight (an arbitrary contributor) — cannot be precomputed as a finite class set at all, so a class-per-dimension payload would have to drop or freeze both; **D2** Code Map **keeps its four filter panels and lazy-mounts on reveal**, because the pure-CSS exclude-spec/exclude-tests toggle is the one filter on that page that works with JavaScript off and trading it for a byte win would take information from a no-JS visitor; **D3** Git Insights gets **the component's generic nested twin**, not a per-directory rollup (which would be shorter but would not enumerate every node the chart draws, failing ADR 0013 §2's completeness predicate) and not a restored per-file table (which would re-litigate a design Story 7.11 deleted on owner feedback). Eight code-verified findings are promoted to a read-first section: **F1** three of the four Code Map panels are always `display:none` and **Plotly cannot lay out in a zero-width container** — the component ships `responsive:true` and sets only height, and a CSS-only reveal fires no resize event, so D2 requires a reveal hook; the same pass also collapses 8 Code Map charts to **4 instances** and 2 ownership charts to **1**, because the component's own selector replaces two more `display:none` pairs. **F2** `tokenFor` composes only `{fill,stroke}` while five of the new families' states carry `fill-opacity` (0.35/0.55) and three carry `stroke-dasharray` — the first renders wrong silently, the second is a non-color channel Plotly's `marker.line` cannot express and must extend `PATTERN_SHAPE`; verified that a bare class-list probe does resolve both families, since none of their rules needs an ancestor. **F3** the non-color channel on these surfaces is **text, rewritten on every dimension switch** (per-node accessible names plus a legend swap), not hatching — a dimension whose fill changes and whose name does not is a UX-DR17 failure that ships green; the shipped wording's honesty (bucket level not raw value, "not among this file's most-active tracked contributors", explicit "(date unknown)") must be ported verbatim. **F4** two hash schemes collide — Code Map's `#dir=` vs the component's `hashKey` — and four instances on one page need four distinct `DomId`/`HashKey` pairs; ownership gains drill-in for the first time, which is the epic's own logged owner request from the Story 7.11 design session. **F5** `HierarchyExplorerConfig.Size` is a single square int applied as height while Code Map's treemap is 1000×640. **F6** measured today, this repo's ownership roster is `["Matt Eland","dependabot[bot]"]` — the solo-repo reframe does **not** trip so the chart renders, but the second contributor touched exactly two files, so three of four modes are near-degenerate and "all four modes verified" would overstate the evidence; and per 20.6 the golden fixture is not a git repo, so **neither page renders in it** and the fingerprint is not this story's net. **F7** **58** entry-point test references plus **68** CSS-class assertions (`codemap-cell` 31, `ownership-wedge` 20, `ownership-cell` 17) — larger than 20.7's F4, and the class-name half is the trap, because those fail at runtime rather than at compile time and many assert a fact that moves to the payload and twin. **F8** `Charts.BuildSunburstSvg` has exactly two callers, both this story's, so **this story deletes the last hand-rolled arc geometry in C#**; and the 2,712 + 2,220 rich hover cards are where the byte accounting actually lands. Measured starting state recorded so AC#4 is reported against real numbers: `code-map.html` **6,020,916 B** (spike projected −3,493,000) and `git-insights.html` **2,129,514 B** (spike projected −1,510,735) — together exceeding the whole −4,787,124 B portal net, which is why this is the story that settles whether the Plotly adoption paid for itself. Sequencing recorded as blocking: **Story 20.7 must land first** (its F3 capabilities are this story's foundation and its Task 10.4 allowlist is what this story empties), itself blocked by 20.6 and by 20.5 reaching `done`. baseline_commit `261b300`.
