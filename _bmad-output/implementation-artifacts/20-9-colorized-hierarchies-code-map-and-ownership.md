---
baseline_commit: 261b3008545a066ae1b08174b77df5b4abd4fb73
---

# Story 20.9: Colorized Hierarchies — Code Map and Git Insights Ownership Through the Component

Status: done

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

- [x] 0.1 Confirm **Story 20.7 is `done`**, and that its **Task 1.1 class-list color resolver** and **Task 1.3 client node filter** are in the shipped `specscribe.js`. Grep for them; do not infer from the story's status. They are this story's foundation and 20.7's F3 committed them to two consumers.
- [x] 0.2 Confirm **Story 20.6 is `done`** and read `20-6-text-twin-audit.md`. Its per-surface record is your permission slip. Its hypotheses for these two: **Code Map PROBABLE PASS** (the file table is genuinely server-complete), **Git Insights FAIL — no twin at all**. Re-verify both live; a hypothesis is not a clearance.
- [x] 0.3 `git status` before starting. Another session's work has been in this tree at the start of every Epic 20 story. **Never `git reset --hard` / `git checkout --` / `git clean`.**
- [x] 0.4 Re-capture the § Measured starting state numbers on your own tree. Reporting AC#4 against a figure from this document rather than from your own "before" run is the easiest way to publish a wrong delta.

### Task 1 — The dimension contract (AC: #1) — D1, F2, F3

- [x] 1.1 **Metric bag on `HierarchyNode`.** Add an optional named-value bag (recommended: `IReadOnlyDictionary<string, string>? Metrics`, string-valued so day-numbers, counts and the `owner` JSON all travel one way) serialized into the island beside the existing fields. Code Map emits `path/lines/filetype/filetype-label/changes/churn/first/last/cochanged`; ownership emits `share/dominant/contributors/last/owner`. **These are the exact `data-*` the SVG already carries** (`Charts.cs:3021-3029`, `:3606-3614`, `:3849-3856`) — lift them, do not re-derive.
- [x] 1.2 **Panel-wide constants** go on `HierarchyExplorerConfig`, not repeated per node: ownership's `topAuthors` roster and `asof` day (today `data-top-authors`/`data-asof` on the SVG root, `Charts.cs:3982-3983`). `asof` is the tree's most-recent commit day — **never wall-clock `now`** (FR31).
- [x] 1.3 **Dimension declarations.** Add a per-instance list of dimensions to the config: key, human label, the legend block it owns, and its kind (numeric-ramp / categorical / fixed-cutoff / roster-relative / threshold-relative). The client rule for each maps a node's metric bag → a **class list** + an **accessible-name suffix**. Port the shipped rules verbatim — bucketing must still mirror `Charts.Bucket` (`specscribe.js:956`), the date dimensions must still scale against the file set's own `[min,max]` window while counts scale against `max` from 0 (`:1047-1057`), share must keep its fixed 25/50/75 cutoffs (`:1310`), and spotlight must keep `spotlightRecencyLevel`'s 30/90/180-day cutoffs (`:1333-1338`).
- [x] 1.4 **Extend the color resolver for opacity and hatch (F2).** Have `tokenFor` read `fill-opacity` off the probe and compose it into the returned paint — five states are wrong without it. Extend `PATTERN_SHAPE` to be keyed by class rather than by `statusClass`, covering `type-other`, `owner-author-other` and `owner-stale`. Keep the 20.4-addendum fixes: explicit per-sector `marker.pattern.bgcolor`, and the shipped `defs pattern > path { fill: none; }` rule.
- [x] 1.5 **Accessible names track the active dimension (F3).** Snapshot each node's base name once; recompose on every switch. Port the honest wording already in place — bucket **level** not raw value, "not among this file's most-active tracked contributors" not "has not worked on this file", explicit "(date unknown)" rather than an oldest-bucket coercion.
- [x] 1.6 **Legend swap is part of the contract (F3).** Exactly one legend block visible per active dimension, and the ramp caption tracks the dimension name. Reuse the shipped emitters — `Charts.OwnershipLegend` / `-TopAuthorsLegend` / `-SpotlightLegend` / `-StalenessLegend` (`Charts.cs:4076,4110,4136,4164`) and `CodeMapTemplater.AppendLegend` / `AppendDiscreteLegend` — routed through the component's framing block rather than re-written. Watch the `[hidden]` vs `display:flex` specificity trap.
- [x] 1.7 **Re-run Story 20.5's survival predicate after a dimension change.** It is a re-render the a11y layer must survive and 20.7's filter work did not exercise it: sectors > 0, `role="treeitem"` on every sector, non-empty `aria-label` on every sector, **exactly one `tabindex="0"`**. Announce the switch through the existing `.ss-hierarchy-live` region.
- [x] 1.8 Generic by construction: **no surface name appears anywhere in the contract.** A `if (surface === "codemap")` branch inside the shared component is the drift this epic exists to end.

### Task 2 — Convert the Code Map (AC: #2) — D2, F1, F4, F5

- [x] 2.1 New `HierarchyExplorer.ProjectCodeMap` over a `CodeMapVariant`: directory → file, sized by `Lines`, metric bag per 1.1, `Href` from the guarded `fileHref` resolver prefixed exactly as today (`CodeMapTemplater.cs:308-311`) — a null return stays a plain, focusable node, **never a broken link** (Story 7.1's guard).
- [x] 2.2 Satisfy the four Story 20.4 invariants **by construction**: exactly one root, no `null` in `values`, `parent == Σ children` (via `HierarchyExplorer.Reparent` / `RollUpParentValues`), and an emitted `branchvalues` equal to `HierarchyExplorer.BranchValues`.
- [x] 2.3 **Four instances, distinct `DomId` and `HashKey` per variant (F4)** — key them off `variant.Key`. `Shape: "treemap"` (Story 20.7 D2 — default shape stays per-instance); selector ordered Sunburst-then-Treemap like everywhere else. Delete `AppendShapeToggle` (`CodeMapTemplater.cs:174`) and the `.codemap-shape-*` CSS (`specscribe.css:4321-4323`); the component's selector replaces both.
- [x] 2.4 **The reveal hook (F1).** Listen to `change` on the two `.codemap-filter-checkbox` inputs; on reveal, first-mount or `Plotly.Plots.resize` the newly-visible instance. Config-gated and generic — the component learns "I may be mounted inside a container that is hidden at load", not "I am on the Code Map". **Verify all four combinations live.**
- [x] 2.5 Delete `<nav class="codemap-drill">` and its breadcrumb markup (`:143-147`); the component supplies the breadcrumb. The **behavior** is what AC#2 preserves.
- [x] 2.6 **Keep the colorize `<select>`** (`AppendColorizeControls`, `:196`) and its seven options as the surface's dimension picker, wired to Task 1.3. Keep it emitted `hidden` and revealed on mount — a no-JS visitor must never see an inert control, and the server-baked default must still be correct without it. Keep the `hasMetrics == false` path: file type is the only option and the baked default (`:211-214`), plus the "git data unavailable" note (`:136`).
- [x] 2.7 **`AppendFileTable` (`:277`) and `AppendCodeMapTablePager` (`:365`) stay untouched** — that table is this surface's twin (Story 20.6 D1) and `initCodemapTablePager` only paginates it. Re-verify completeness live with JS off before retiring the SVG: every file present, every link resolving, enough directory context to stand in for the hierarchy. A pager is not truncation.
- [x] 2.8 `Size` per F5 — set it, then check legibility live. Do not port `1000`/`640` as if `Size` were a width.
- [x] 2.9 Preserve the honest empty state: `variant.Map.IsEmpty` renders *"No files match this filter."* (`:99-104`) and `HierarchyExplorer.Render` returns `""` for an empty model. A missing panel is not an empty state (NFR8).

### Task 3 — Build the Git Insights twin, then convert (AC: #3) — D3

**Order matters and AC#3 states it: the twin lands *first*, is verified live with JS off, and only then does the SVG go.**

- [x] 3.1 New `HierarchyExplorer.ProjectOwnership` over `codeMap.Roots` + `topAuthors`: directory → file, sized by `Lines`, metric bag per 1.1, `Href` from the guarded `fileHref` resolver as today (`GitInsightsTemplater.cs:174`).
- [x] 3.2 **The twin (D3).** `TextTwinHtml` as it stands, collapsed `<details>` (Story 20.6 D3's default — do not set `ScreenReaderOnly` here; there is no visible companion panel on this page to justify it). Its prose must carry what the chart conveys: dominant author, share %, contributor count, last-active date. Reuse `Charts.DescribeOwnershipFile` — one vocabulary for chart, twin and tooltip.
- [x] 3.3 **Verify the twin against the chart's own node set** before deleting anything: enumerate the payload with JS on, diff it against the twin's entries with JS off. **A count match is not a set match** (Story 20.6 Task 1.3b).
- [x] 3.4 **Correct the stale doc comment (`GitInsightsTemplater.cs:14-20`)** — it states the page's no-JS contract as ADR 0010 §2's *"a real, useful default-mode chart … renders and works with JS off"*, which **ADR 0013 §4 supersedes**. Replace it with the ADR 0013 contract and point at `20-6-text-twin-audit.md`. If Story 20.6 Task 2.4 already did this, confirm rather than redo — and grep for other sites still asserting the superseded clause.
- [x] 3.5 One instance replaces both charts. `Shape: "sunburst"` (this surface's shipped default). Delete the `.board-tabs` block (`:157-164`) and the `.ownership-view-*` CSS (`specscribe.css:6243-6245`) — the component's selector replaces them.
- [x] 3.6 **Keep the mode selector, contributor select and threshold input** (`:137-151`) as this surface's dimension controls, wired to Task 1.3. Keep them emitted `hidden` and revealed on mount. The contributor list is still built from the **union of every node's own roster, alphabetical** — never a top-N ranking (FR-10, `specscribe.js:1234-1242`).
- [x] 3.7 **`FR-10` holds in every mode.** "Top contributors" is a color palette, not a leaderboard; the spotlight is a filter, not a score. No mode may sort or rank contributors by volume in reader-facing output. Assert it.
- [x] 3.8 **Keep the solo-repo reframe** (`:120-130`) exactly as it is — the `codeMapContributorCount == 1` gate returns before any chart, and its reasoning (it must read the *codeMap's* contributor population, not `insights.ContributorCount`) is a reviewed fix from 2026-07-22. It is an honest empty state, not dead code.
- [x] 3.9 `Size` — the shipped 480 is a genuine square and is a reasonable start; verify live (F5).

### Task 4 — Delete the superseded implementations and prove it by search (AC: #4)

- [x] 4.1 Delete `Charts.CodeTreemap` (`:2939`), `CodeMapSunburst` (`:3496`), `CodeOwnershipSunburst` (`:3957`), `CodeOwnershipTreemap` (`:4016`).
- [x] 4.2 **`Charts.BuildSunburstSvg` (`:3650`) and its span math (`:3713`) become unreachable here (F8)** — its only two callers are 4.1's. Confirm by search and delete. `Charts.cs` sheds its polar geometry entirely; say so in the Completion Notes, it is the concrete form of Epic 20's goal.
- [x] 4.3 Delete `specscribe.js`'s `initCodeMapPanel` (`:939`, block `924-1195`) and `initOwnershipSunburst` (`:1211`, block `1197-1403`), and the `.codemap-cell`/`.codemap-dir`/`.ownership-wedge`/`.ownership-cell` CSS families once nothing references them. **Keep `initCodemapTablePager`** — it serves the twin.
- [x] 4.4 **Keep**, and do not sweep up as "part of the chart": `MaxDetailedCodeMapFiles` (`:2895`), `SelectDetailedCodeMapFiles` (`:2916`), `ComputeMaxChanges`, `CodeMapChangeLevelRange`, `IsCodeMapChangeLevelUnreachable`, `OrderBySignificance`, `Bucket` (5 other callers outside `Charts.cs`), `BuildOwnershipCard`, `BuildOwnerJson`, `BuildOwnershipDataAttrs`, `DescribeOwnershipFile`, `OwnershipShareLevel`, `BuildTreemapCard`, the four `Ownership*Legend`, `CodeFileType.AllCategories`, `GitMetrics.BuildTopAuthors`, `Plural`, `Framed`/`ChartMeta`/`WhyText`. The projectors, the legends, the tooltips and the file table all depend on them.
- [x] 4.5 **Empty Story 20.7's rollout-completeness allowlist.** 20.7 Task 10.4 seeds it with these six symbols under a `[Story 20.9]` comment. Removing the last entry — and leaving the *test* in place, now asserting an empty allowlist — is this story's finish line and the epic's. Do not delete the test with the allowlist.
- [x] 4.6 **Prove absence by search, not by assumption** (CLAUDE.md § Concurrent work — a `Charts.cs` edit has silently vanished in this repo). Grep every deleted symbol and every deleted CSS class across `src/`, `tests/`, and the extension shim; record the searches and their zero results in the Debug Log. **A build that compiles is not the same evidence** — and the 68 class-name assertions in F7 are exactly the kind that compile fine and are wrong.

### Task 5 — Hosts and parity (AC: #2, #3)

- [x] 5.1 The webview presents the **text twin** — Story 20.7 D3's documented accepted degradation (ADR 0012 §5 / ADR 0013 §7), already registered in `HostRenderExceptions` by 20.7 Task 9.1. Confirm these two surfaces are covered by that entry or extend it; an unregistered divergence is a bug.
- [x] 5.2 Verify the webview reaches the twin: the island strip is a regex over `<script type="application/json">` (`WebviewRenderAdapter.cs`) and the twin is `<details>` markup, so it should survive — **confirm it**, and confirm the twin's file links resolve under the webview's path rewriting.
- [x] 5.3 SPA: island and twin must survive content capture, and `specscribe:content-swapped` must re-init **all five** instances (four Code Map + one ownership). Extend the existing `SiteGeneratorSpaTests` island-survives-capture test rather than adding a parallel one. Note the known SPA scale hazard on this exact page (`code-map.html` measured 82.5 MB at SPA scale in Story 6.6) — report whether this conversion helps it.
- [x] 5.4 `RenderParity` / `RenderSectionParityTests` green across `html`, `spa`, `webview`.

### Task 6 — Tests (AC: #1, #2, #3, #4)

- [x] 6.1 Work through F7's 58 entry-point references **and** its 68 class-name assertions. Rewrite fact-asserting tests against the payload and the twin; delete geometry-asserting ones. **Report the split** — how many rewritten, how many deleted, what coverage genuinely went away.
- [x] 6.2 Per new projector (`ProjectCodeMap`, `ProjectOwnership`), assert the four 20.4 invariants: exactly one root, no `null` in values, `parent == Σ children`, emitted `branchvalues == HierarchyExplorer.BranchValues`.
- [x] 6.3 **Per dimension** (all eleven), assert the resolved class list is what the shipped renderer produced for the same input — the fills must be **unchanged** by the conversion, not merely plausible. Include the five `fill-opacity` states and the three `stroke-dasharray` states from F2 explicitly; they are the ones that regress silently.
- [x] 6.4 **Per dimension**, assert the accessible name changes and carries the dimension's own text equivalent (F3). This is AC#1's non-color clause made testable.
- [x] 6.5 A **completeness invariant** for the ownership twin: every payload node appears in the twin with a prose status and a resolving href — the analogue of `Projector_NodeSet_EqualsTheWedgesTheSvgDrew`, retargeted at the twin (Story 20.7 Open Question #2's recommended answer).
- [x] 6.6 The **rollout-completeness test with an empty allowlist** (4.5). Assert no source file outside `HierarchyExplorer` constructs any hierarchy chart.
- [x] 6.7 Keep the shipped privacy guard green now that five more instances construct a Plotly config: `displayModeBar: false`, `plotlyServerURL: ''`, `topojsonURL: ''`, and no `sendDataToCloud` / `cdn.plot.ly` / `plotly.com` string anywhere in the shipped JS.
- [x] 6.8 Assert the honest empty states survive: `Map.IsEmpty` → "No files match this filter."; `codeMap.IsEmpty` → "No file change data available."; `codeMapContributorCount == 1` → the solo-repo note; `hasMetrics == false` → file type as the only dimension plus the git-unavailable note.
- [x] 6.9 **Do not unit-test the JS** — SSR-first, no JS harness. Task 7 is the verification for Tasks 1–3's client behavior. Say so plainly rather than implying coverage that does not exist.

### Task 7 — Live-browser verification (AC: #2, #3) and the accounting (AC: #4)

- [x] 7.1 Generate to `SpecScribeOutput/` **with `--deep-git`** — without it `git-insights.html` does not render at all and Code Map falls back to file-type-only. Never `--output docs/live`. Serve via `.claude/launch.json` → `specscribe-output`, port 8099.
- [x] 7.2 **Code Map, JS on:** all **four** checkbox combinations — chart renders with real width and real sectors (F1), selector switches shape in place, drill/breadcrumb/Escape work, hash round-trips per instance, all **seven** dimensions recolor with legend and accessible names tracking, zero console errors, survival predicate holds. Four passes, recorded separately.
- [x] 7.3 **Git Insights, JS on:** all **four** modes, the contributor select, and the threshold input at several values. Drill into a directory — the owner's original Story 7.11 request (F4). Record honestly that the roster is two deep and one is a bot (F6); do not report thin evidence as rich.
- [x] 7.4 **Both surfaces, JS genuinely off in the browser** (not assumed): the twin is present, complete, navigable, non-color; the chart host leaves no chart-sized blank box; **no inert controls are visible** — the colorize select, the mode select, the contributor select and the threshold input must all still be `hidden`. For Code Map, confirm the **pure-CSS filter still works with JS off** (D2's whole justification) and that each variant's file table is complete. This is the ADR 0013 §3 gate being exercised, and it is the last moment before the SVG is gone.
- [x] 7.5 **Colorway audit** on both surfaces, built at runtime from the shipped cascade — the `.codemap-cell.*` and `.ownership-*` families, including `fill-opacity`. Zero foreign colors, text fills included. Re-derive the allowlist from the shipped CSS, never from a typed token value.
- [x] 7.6 **Take screenshots.** The owner has still never seen a pixel of this component — 20.4, 20.5 and 20.7 all owed one. Try hard; if the pane still refuses to composite, say so and fall back to computed-geometry evidence rather than skipping it quietly a fourth time.
- [x] 7.7 **The byte accounting (AC#4).** Report per page: before, after, delta, and the split between island / twin / removed SVG. Compare against the spike's `code-map.html` −3,493,000 B and `git-insights.html` −1,510,735 B and the **−4,787,124 B** portal net. Say whether the projection held — and if the island came in nearer 20.5's measured ~390 B/node than the spike's 195.4 B/node, say that too. **This is the story that settles it**; do not report a directionally-pleasing number without the breakdown.
- [x] 7.8 **Golden fingerprint.** It should barely move: the fixture is not a git repo, so neither page renders in it (F6). If it moves substantially, that is a signal something else changed — investigate rather than re-baselining. If you do regenerate, confirm stable across two repeated runs and **name whose concurrent changes it sits on top of**.
- [x] 7.9 Full suite, real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.
- [x] 7.10 **State in the Completion Notes that Epic 20 AC#2 is now satisfied**, and name the evidence: the empty allowlist, the zero-result searches, and `BuildSunburstSvg` gone. Three stories have had to say "not yet" — this is the one that gets to say yes, and it should say it with proof rather than by assertion.

---

### Review Findings

*Scoped to this story's own File List within commit `811ba17` (not the full `d1722f1..HEAD` range — three later commits touch the same files for sibling stories 5.7, 20.7-continuation, 22.4, 23.4, verified unrelated and excluded). Sibling Story 18.6 work bundled into the same commit's `SiteGenerator.cs`/`.claude/launch.json` (`AppendUnmodeledCoverageNotice`, `ArtifactCoverage.Build`/`CreateStepKeysFor` signature widening, the `bmm-*-18-6`/`unmodeled-18-6` launch entries) was verified genuinely orthogonal to Code Map/Git Insights and excluded from scoring. Edge Case Hunter layer failed (session usage limit) and did not run — this review reflects Blind Hunter + Acceptance Auditor only.*

**Decisions — resolved by owner (2026-07-28)**

- [x] [Review][Decision] Code Map deep-link hash into a non-default filter panel is silently dropped on page load — **Owner resolution: fix now.** Moved to Patch below (auto-reveal the matching panel on a hash match at load). Each of the four Code Map panels gets its own `HashKey`/`DomId` (F4), but only the default "full" panel mounts at load; the other three sit in `hierarchyPending` (`specscribe.js:1062-1099`) until their filter checkbox is manually toggled. `scopeFromHash()` (`specscribe.js:1964`) only runs inside each instance's own init, so a URL fragment naming a scope inside e.g. the "no-tests" panel does nothing on a fresh navigation.
- [x] [Review][Decision] Code Map's 4 filter-variant payloads duplicate shared files' metric bag + hover card up to 4x, and the story's own pre-registered revisit trigger for this appears to have fired — **Owner resolution: investigated now, proposed as a new backlog story rather than implemented inline.** Open Question #1 recommended not sharing one payload across the four panels, but said to "revisit only if Task 7.7's measurement shows the four payloads dominate the page." Investigation confirmed the trigger: `code-map.html`'s 1421/487/1254/350 per-panel sector counts sum to 3,512 file-instances across 4 payloads vs 1,421 distinct files (2.47× average duplication, matching the reported 936 B/node exactly). Estimated potential saving ≈1.96 MB (~44% of the page) if duplication were eliminated — but doing so requires client-side treemap re-layout for a filtered subset, a capability that doesn't exist today, so it is out of scope for a same-session patch. **Seated as [Story 20.10](../planning-artifacts/epics.md#story-2010-shared-hierarchy-payload-across-code-maps-filter-variants)** (backlog) with the investigation's numbers carried forward; Open Question #1 above is now CLOSED as "investigated, follow-up scoped separately" rather than left open.

**Patch**

- [x] [Review][Patch] Code Map deep-link hash into a non-default filter panel is silently dropped on page load [`specscribe.js:1062-1099,1954-1978`] — **applied.** `CodeMapTemplater.AppendVariantPanel` now emits `data-hierarchy-reveal-when="cm-exclude-spec=0|1;cm-exclude-tests=0|1"` on each `.codemap-view` wrapper, declaring which checkbox states reveal it. A new boot-time `revealPanelsNamedByHash()` in `specscribe.js` reads each not-yet-visible instance's own data-island `hashKey` (no mount required), finds a match against `location.hash`, sets the declared checkbox states, and flushes the deferred-mount queue — before `initHierarchyExplorers(document)` runs. Never forces `display` directly, so the checkboxes stay the honest "what's active" affordance. Golden fingerprint moved (new attribute renders even on the fixture's empty panels) — confirmed stable across 2 runs and updated in `SiteGeneratorAdapterTests.cs` with provenance noted.

- [x] [Review][Patch] No test asserts the new chart payload still honors `MaxDetailedCodeMapFiles` [`HierarchyExplorer.Projectors.cs:461`] — **applied.** Added `HierarchyColorizeTests.ProjectCodeMap_AboveTheDetailCap_LongTailKeepsGeometryButLosesTheCard`: cap+5 files, asserts every file still gets a node with real geometry, only the top-`cap` most-significant keep `TipHtml`, and the least-significant file is the one that loses its card.
- [x] [Review][Patch] Code Map's twin (file table) has no set-match completeness test, unlike Git Insights' twin [`tests/SpecScribe.Tests/CodeMapTemplaterTests.cs`, cf. `GitInsightsTemplaterTests.cs:242-255`] — **applied.** Added `CodeMapTemplaterTests.RenderPage_FileTableIsASetMatchAgainstTheChartPayload_NotJustACountMatch`, mirroring the Git Insights predicate: every `path` in the "full" panel's island must resolve to a row in that same panel's table.
- [x] [Review][Patch] Stale XML doc comment in `Charts.cs` still names the deleted `initOwnershipSunburst` [`Charts.cs:3131-3135`] — **applied.** `OwnershipTopAuthorsLegend`'s doc comment now describes the component's `data-hierarchy-legend` dimension-driven swap instead of the retired renderer.
- [x] [Review][Patch] `.claude/launch.json`: two preview entries collide on port 8114 [`.claude/launch.json`] — **already fixed by a concurrent session's `/bmad-code-review 18.6` run** (`codemap-20-9` moved to port 8119 in the working tree while this review was in progress; confirmed live, not reapplied here). This story's own `codemap-20-9` entry and the concurrently-added sibling Story 18.6 `bmm-control-18-6` entry had both bound port 8114 (both added in commit `811ba17`).

**Verification:** `dotnet build` clean (0 errors). Isolated run of every test class touched by these patches plus the golden fingerprint (`CodeMapTemplaterTests`, `HierarchyColorizeTests`, `GitInsightsTemplaterTests`, `ChartsTests`, `StylesheetTests`, `HierarchyRolloutTests`, `SiteGeneratorCodeMapTests`, `SiteGeneratorSpaTests`, `SiteGeneratorGitInsightsTests`, `SiteGeneratorWebviewTests`) — **365 passed / 0 failed / 1 skipped** (a pre-existing symlink test skip, unrelated). A full-suite run under heavy concurrent load (another session's own test run competing for git subprocesses on the same host) produced a burst of git-CLI-contention failures across deep-git/timeline/commit-detail/impact-map tests — none in files this patch touches, and consistent with this repo's documented flaky-under-load class; not re-chased given the isolated, load-free run above is clean.

**Deferred**

- [x] [Review][Defer] Zero-contributor Git Insights case renders full ownership controls with an empty roster, unguarded [`GitInsightsTemplater.cs:176`] — deferred, pre-existing. `AppendOwnershipSection` gates on `codeMapContributorCount == 1` but not `== 0`; if every file's contributor data is empty while `codeMap.IsEmpty` is false, the full mode-selector/contributor-select UI renders wired to an empty roster. Narrow, no crash, pre-existing gate structure just reachable via a different chart engine now.
- [x] [Review][Defer] A single reveal-triggering checkbox change re-resizes every mounted hierarchy chart on the page [`specscribe.js:1071-1098`] — deferred, pre-existing design tradeoff. The delegated `change` listener's `flushHierarchyReveals()` loops over the entire `hierarchyMounts` array and calls `Plotly.Plots.resize` on every already-mounted instance, not just the panel whose checkbox changed. Each call is cheap and guarded; inefficiency, not incorrectness.
- [x] [Review][Defer] `HierarchyDimensionKind` is an unvalidated string; an unrecognized kind silently no-ops [`specscribe.js:1304-1368,1396-1397`] — deferred, pre-existing contract gap. None of the shipped 11 dimensions hit this path (verified by the story's own test), so it's latent rather than active, but nothing protects a future third consumer of the "designed for two consumers" contract from a silent typo in `kind`.
- [x] [Review][Defer] `HierarchyDimension.Key` uniqueness within one `Dimensions` list is unenforced [`specscribe.js:1287-1291`] — deferred, pre-existing contract gap. `activeDim()`'s linear scan means a future duplicate key would silently make the second dimension unreachable via the dropdown, with no error. Same latent-robustness class as the kind-validation finding above.

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

1. **Should the four Code Map instances share one payload?** ~~Recommended: no...~~ **CLOSED 2026-07-28 (code review):** the revisit trigger fired — Task 7.7 measured the island at ~74% of `code-map.html`'s page weight, and the four payloads carry 2.47× average duplication (3,512 file-instances / 1,421 distinct files). Investigated rather than implemented inline (client-side treemap re-layout for a filtered subset doesn't exist today); seated as **Story 20.10** (backlog) with the investigation's numbers carried forward. Note the "client filter puts it behind JS" objection does not actually apply here — the chart already requires JS regardless of payload count; only the file table's no-JS filterability (D2's real justification) is unaffected either way.
2. **Does `#dir=` deep-linking survive?** Recommended: **retire it**, and say so. It was never a documented stable scheme, the component's `hashKey` idiom replaces it, and contorting `hashKey` to preserve it would fork the deep-link vocabulary across surfaces. Raised because someone may have bookmarked one.
3. **Are 640 px (Code Map) and 480 px (ownership) still the right sizes?** Recommended: **raise both modestly and verify live.** Both were chosen for a static SVG that neither labelled nor drilled. A file-tree treemap that now shows in-sector labels needs room for them.
4. **Should `dependabot[bot]` be filtered out of the contributor roster?** Recommended: **not in this story.** It is a data question (which authors count as contributors) with a real answer either way, it affects the shipped SVG identically today, and folding it into a rendering conversion would hide a product decision inside a refactor. Flagged because the owner will see a bot in the spotlight picker during verification (F6) and should decide deliberately.
5. **Does ownership want the reveal hook too?** Recommended: **no** — its panel is not inside a `display:none` container; only the shape toggle was, and the component's selector replaces that. Stated so the answer is on the record rather than rediscovered.

---

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), dev-story workflow, 2026-07-27.

### Debug Log References

**Task 0 — entry conditions, verified by grep rather than by status.**

- **0.1 — Story 20.7 is `review`, not `done`, and its work IS in the committed tree.** The blocking condition
  that matters is the capability, not the label, and Task 0.1 said to grep for it. Confirmed present at
  `specscribe.js`: `tokenFor(classList)` applying a per-node class list VERBATIM to the probe (Task 1.1's
  resolver), the generic root-subtree `applyFilter` (Task 1.3's node filter), and
  `HierarchyRolloutTests` carrying the six-symbol rollout allowlist under a `[Story 20.9]` comment (Task 10.4).
  20.6 and 20.5 are both `done`. Started on that basis.
- **0.3 — `git status` before starting:** 2 modified files (`SiteGeneratorAdapterTests.cs`,
  `web/scripts/ir-content-build.mjs`) plus an untracked `.gitattributes`. NOT clean. See § Concurrent work below
  for what arrived during the story.
- **0.4 — baseline re-captured on THIS tree** (`d1722f1`, not the story's `261b300`): 733 pages in 45.8 s,
  0 errors; `code-map.html` **6,597,752 B**, `git-insights.html` **2,296,875 B`. Both had grown ~9% and ~8%
  since the story was drafted, which is exactly why Task 0.4 says to recapture rather than report against a
  figure from the document.

**Task 4.6 — absence proved by search, not by "it compiled".** Every deleted symbol and CSS class swept across
`src/`, `tests/` and the extension shim, excluding `bin`/`obj`/`TestResults`. Zero live references remain; every
surviving textual hit is either a comment explaining the deletion at the site it left behind, or a
`DoesNotContain` assertion pinning the absence:

| Searched | Live refs | Notes |
|---|---:|---|
| `Charts.CodeTreemap` / `CodeMapSunburst` / `CodeOwnershipSunburst` / `CodeOwnershipTreemap` | **0** | 6 hits, all prose |
| `BuildSunburstSvg`, `AnnularSector` | **0** | 8 hits: 3 prose, 2 assertions, 3 explaining the deletion |
| `SunburstWedgeInfo`, `CollectFreshnessStats`, `CountFreshnessFiles`, `WalkSunburstWedges`, `DescribeCodeMapCell`, `CodeMapCellData`, `FreshnessSunburstMaxDepth`, `AppendShapeToggle` | **0** | no hits at all |
| `initCodeMapPanel`, `initOwnershipSunburst`, `recolorShare`, `recolorTopAuthors`, `recolorSpotlight`, `recolorStaleness`, `spotlightRecencyLevel` | **0** | asserted absent in `HierarchyRolloutTests` + `StylesheetTests` |
| C#-emitted `codemap-shape*`, `ownership-view-*`, `codemap-drill`, `codemap-crumb`, `codemap-sunburst`, `ownership-sunburst`, `ownership-treemap`, `cs-sunburst-radio`, `ownership-treemap-radio`, `ownership-cell` | **0** | |

A `StripComments`-equivalent was added to `StylesheetTests` (`ReadStylesheetRules`) for the same reason
`HierarchyRolloutTests` already had one: two absence guards tripped on my own explanatory CSS comment, and a
guard a comment can trip is a guard that gets weakened the first time it fires for the wrong reason.

**Live verification (Task 7).** `--deep-git` generation to `SpecScribeOutput/`, served over `file://` (the
project's `preview_start` dev-server slots were all held by other chats; `file://` is a legitimate target here
because NFR-3 requires the portal to work from one). Evidence is computed geometry and DOM state, not a
screenshot — see the honest note in the Completion Notes.

### Completion Notes List

**Epic 20 AC#2 is now satisfied: exactly one implementation of a hierarchy chart exists in the codebase.** Three
stories have had to say "not yet". The evidence, rather than the assertion:

1. **The rollout-completeness allowlist is empty**, and the test is still there asserting it
   (`HierarchyRolloutTests.NoSourceFileOutsideTheComponent_ConstructsAPlanningHierarchyChart`). Its `retired`
   list now carries all seven entry points — Story 20.7's three planning ones and this story's four.
2. **The zero-result searches** in the Debug Log above.
3. **`Charts.cs` has shed its polar geometry completely.** `BuildSunburstSvg` had exactly the two callers F8
   predicted, both this story's — and deleting them exposed something F8 understated: **`Charts.AnnularSector`,
   the donut-slice path builder, was ALREADY DEAD.** Story 20.7 retired its last caller and nothing noticed,
   because an unused private method is not a build warning. It is gone too, and both names are asserted absent
   so the state cannot quietly regress.

---

#### What the conversion actually collapsed

8 Code Map charts became **4 instances**; 2 ownership charts became **1**. Two `display:none` CSS pairs
(`.codemap-shape-*`, `.ownership-view-*`) disappeared entirely, because the component's own selector re-types
the trace in place rather than swapping between two pre-rendered pictures. That is the real shape of this
conversion and it is worth saying plainly: the affordance was kept and the mechanism was deleted.

#### Three findings that turned out different from the story's

- **F2(a) and F2(b) were ALREADY SOLVED by Story 20.7, before this story started.** `tokenFor` already applies
  the class list verbatim, and it already reads `fill-opacity` and composites it into the returned paint
  (`withOpacity`). The story predicted both as work. Only **F2(c)** — the three `stroke-dasharray` states — was
  outstanding, and it needed three new `PATTERN_SHAPE` keys, not a re-keying: 20.7 had already made that map
  class-keyed and matched against the whole class list.
- **F2(c) needed a fourth channel the story did not name.** `.spotlight-touched` layers
  `stroke: var(--ink); stroke-width: 1.2` on top of a level ramp, and `marker.line` — unlike the dash — CAN
  express that. So `tokenFor` now also resolves `stroke-width`, and `marker.line` became per-sector. Verified
  live: the spotlight dimension really does draw `stroke-width: 1.2` alongside the family's own `0.5`. Every
  already-shipped family declares one uniform stroke (`.sb-seg` is warm-white at 1), so this is byte-identical
  for them — it only becomes visible where a state genuinely differs.
- **F8's byte story was right about WHERE and wrong about HOW MUCH** — see the accounting below.

#### F1 confirmed, and a defect of my own caught only by looking

F1 is real: three of the four Code Map panels are `display:none` and Plotly lays out nothing in a zero-width
container without complaint. The reveal hook is a delegated `change` listener on `[data-hierarchy-reveal]`, with
the deferral condition **measured** rather than declared, so no surface name reaches the shared component.

**But my first implementation measured the wrong element and broke every chart on the site.** I gated on
`root.clientWidth`, and `.ss-hierarchy` is itself `display: none` until the component reveals it — so the host is
zero-width for *every* instance at that moment, and the gate deferred the dashboard too. The suite was fully
green. It took one `javascript_exec` against the real page to see four hosts at `ready: null, sectors: 0`. The
fix measures the PANEL, which is the nearest ancestor laid out before any script runs. This is precisely the
class of defect CLAUDE.md § Verification exists for, and it would have shipped.

#### Two gaps this conversion exposed in surrounding code

- **The vendored engine never reached either page.** Both build their own document rather than going through
  `PageView`/`AssetManifest`, so `HtmlRenderAdapter`'s flag-driven `<script>` emission does not apply — the exact
  miss Story 20.7 made on four surfaces at once, where every layer below the browser was green while nothing
  mounted. Both templaters now emit the tag conditionally (the Impact Map's seam), and `WriteCodeMap` /
  `WriteGitInsights` each call `EnsureHierarchyEngine`, because a repo with source but no epics renders these
  pages and no dashboard chart. Caught by `SiteGeneratorSpaTests.HierarchyEngineBundle_ShipsOnlyWhereAHierarchy
  ChartWasRendered`, which 20.7 wrote for exactly this.
- **The webview's island strip never ran on CAPTURED pages.** `WebviewRenderAdapter.RenderContent` strips inline
  JSON islands, and the `data-island` host exception describes the webview as carrying none — but a
  `WriteOutput`-synthesized page becomes a webview surface by slicing its `<main>` out of captured HTML, a path
  that never called it. Pre-existing, and previously ~470 KB; **after this story it would have been 4.5 MB** of
  payload inlined into an editor panel that ships no `specscribe.js` and can never read a byte of it. The strip
  now applies to that path too — **after** the nav-only degrade check, which is a reference comparison that
  stripping first would have silently defeated.

#### Deviations from the story's instructions, each deliberate

- **Task 4.4's keep-list named `BuildOwnershipDataAttrs`; it is deleted.** It built the SVG's escaped
  attribute STRING, and the payload reads `BuildOwnerJson` directly, so it had no reachable caller. Keeping an
  unreachable builder is not the same as keeping a helper. `OwnershipFileInfo.DataAttrs` went with it.
- **`CompactMetricsTail` was on my delete list and is KEPT** — the compiler caught that the Risk Quadrant's own
  past-the-cap branch calls it, so it was never chart-exclusive. Exactly the anti-pattern 9 the story warns about,
  caught the cheap way.
- **Task 4.3 says delete the `.codemap-cell` / `.ownership-wedge` CSS families; their FILL rules are KEPT.** They
  are now the component's colour SOURCE — `tokenFor` resolves each node's class list against them, which is what
  keeps AD-7 true and no colour value typed into the JS. What went is everything that was about drawing an SVG:
  the viewport, the hover/focus strokes, the `<a>` wrapper rules, the whole `.ownership-cell` family (the class is
  no longer emitted at all). The story's own File List note anticipated this ("delete those only after the
  component reads them"); the rules the component reads are the ones that stay, permanently.
- **`#dir=` is retired, per Open Question #2's recommendation.** Each panel has a distinct `DomId` and `HashKey`
  (`codemap-{variant}` / `cm-{variant}`), asserted distinct across the whole SPA bundle.
- **Each panel's framed TITLE absorbed the old `.codemap-view-note`.** Four identically-titled panels in one
  document told apart only by a checkbox state is worse than four titles that say which filter they are.

#### Task 7.7 — the byte accounting (AC#4). The spike's projection did NOT hold, and here is why

| Page | Before | After | Delta | Spike projected |
|---|---:|---:|---:|---:|
| `code-map.html` | 6,597,752 | **4,451,207** | **−2,146,545** | −3,493,000 |
| `git-insights.html` | 2,296,875 | **1,569,710** | **−727,165** | −1,510,735 |
| **Both** | 8,894,627 | **6,020,917** | **−2,873,710** | −5,003,735 |

**61% of projection on the Code Map, 48% on Git Insights — 57% overall.** The split, which is the part that
explains it:

| Page | Island | of which hover cards | Twin | File table | B/node | B/node ex-card |
|---|---:|---:|---:|---:|---:|---:|
| `code-map.html` | 3,288,932 | 1,379,388 (42%) | 0 (External) | 1,076,146 | **936** | 544 |
| `git-insights.html` | 1,193,573 | 428,291 (36%) | 340,660 | — | **840** | 539 |

The Story 20.4 spike measured **195.4 B/node**; Story 20.5 measured roughly **double** that in practice and told
this story to budget ~390. **The real figure here is 840–936 B/node, about 2.2–2.4× even 20.5's number** — and
the reason is that these nodes are not planning nodes. Each carries a metric bag of up to nine raw values AND a
rich per-file hover card that `.ss-tooltip` requires to survive the engine swap (Story 20.5's tooltip decision).
Strip the cards and the payload is ~540 B/node, still well above the spike. **The spike's per-node figure was
measured on a datasource with neither.** That is the honest reading of "did the Plotly adoption pay for itself":
it did, clearly, on these two pages — but at a little over half the projected saving, and the projection was
optimistic because it generalized from a cheaper kind of node.

One change materially moved this number and is worth naming: **`System.Text.Json`'s default encoder escapes every
`<`, `>` and `&` to a six-byte `\uXXXX`**, and the largest field in these payloads is HTML. That was 2.8 MB of
`code-map.html` on its own. The dimension-bearing payload now uses relaxed encoding plus a narrow, explicit
neutralization of the only two sequences that can end or re-frame a `<script type="application/json">` element
(`</` → `<\/`, `<!` → escaped). Both the safety property and the round-trip are asserted, including a hostile
path literally containing `</script><img src=x onerror=...>`. **The change is gated on "does this instance
declare dimensions"**, so the six surfaces Story 20.7 converted keep byte-identical islands and the golden
fingerprint does not move for a reason unrelated to this work.

`index.html` (+7,247 B) and `impact-map.html` (+5,694 B) also moved. That is **not** a rendering change: this
portal documents its own repository, and this story changed the source tree those pages count. Reported rather
than netted silently.

#### Task 7.8 — the golden fingerprint did NOT move

`GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` passes **unchanged**, no
regeneration, confirming F6 and Story 20.6 Task 4.1: the golden fixture is not a git repository and cites no real
files, so neither converted page renders in it. **The fingerprint was never this story's regression net**, and
the new `HierarchyColorizeTests` says so in its own class comment rather than leaving a reader to infer coverage
that does not exist.

#### Task 7.2/7.3/7.4 — what was actually verified live, and what the data could not show

**Code Map, JS on — all four checkbox combinations, recorded separately.** Each revealed panel mounts with real
width and real sectors (`full` 1421, `no-spec` 487, `no-tests` 1254, `no-spec-no-tests` 350), each at 590×640,
each with exactly one `tabindex="0"`, zero empty `aria-label`s, and a file table whose row count matches its
chart (1189 / 440 / 1036 / 305). Story 20.5's survival predicate holds in all four. **All seven dimensions**
switch in place: fills change, the legend swaps ramp↔discrete, the caption tracks the dimension, the live region
announces, and every accessible name carries the dimension suffix
(`"src/SpecScribe/Charts.cs — C#, 3,860 lines · 89 changes — change frequency: level 2 of 4"`). The `type-other`
hatch resolves on exactly the 53 sectors labelled "file type: Other", and Plotly's pattern carries the explicit
per-sector `bgcolor` the 20.4 addendum requires (`rgb(232,213,176)` at 0.55 — the fill WITH its opacity composed
in, not a black backing rect).

**Git Insights, JS on — all four modes, the contributor select and the threshold input.** Legends swap, the
argument controls show/hide per dimension, the honest wording appears verbatim, and drill-in works on this
surface **for the first time** — the epic's own logged owner request from the Story 7.11 design session.

**And now the part F6 told me not to overstate.** This repo's roster is two deep and one of them is a bot
(`["Matt Eland","dependabot[bot]"]`), so "top contributors" paints two colours, one of them covering ~2 files.
The spotlight picker lists the bot and, being alphabetical, **defaults to it** — flagged for the owner rather
than filtered unilaterally (Open Question #4). **Staleness is worse than F6 predicted.** F6 called it "the only
mode with real spread"; measured on my tree it has **none**: the deep-git window spans 0.6 months end to end, so
no file is stale at any threshold ≥ 1 month, and 547 of 1,189 files carry no last-touch date at all. The mode is
correct and exercised; it is simply not *discriminating* on this repository. "All four modes verified" would
have been true and misleading.

**Both surfaces, JavaScript genuinely off** (scripts stripped from a served copy, not assumed): zero inert
controls render, no legend renders, no boot placeholder, no chart-sized blank box, and — the ADR 0013 §3 gate —
Git Insights' twin carries **all 1,189 files as resolving links, a SET match against the payload with 0 missing**
(not a count match; Story 20.6 Task 1.3b), each with prose ownership. Code Map's pure-CSS filter **still works
with JS off**, which is owner decision D2's entire justification, and each variant's table is complete with all
nine columns and every path linked.

#### Task 7.6 — still no screenshot, and this is the fourth time

The Browser pane will not composite in this harness (`screenshot failed: the Browser pane is not displayed`;
`window.innerWidth` reads 0). As instructed, I am saying so rather than skipping it quietly: the evidence above is
computed geometry and DOM state read out of the live page, which is strictly weaker than a picture for judging
legibility. **Sizes are consequently unverified by eye** — Code Map is set to 640 (the SVG's own height, applied
as height-capped-to-width) and ownership raised 480 → 560 per Open Question #3's "raise modestly". Both are
one-line config changes if the owner's verify round disagrees.

#### Task 6.1 — the test split, and what coverage genuinely went away

**44 deleted, ~33 rewritten, 21 new.** Deleted: every `CodeTreemap_*` / `CodeMapSunburst_*` /
`CodeOwnershipSunburst_*` / `CodeOwnershipTreemap_*` method in `ChartsTests.cs` — they assert SVG geometry
(`viewBox`, rect `x`/`y`/`width`, arc `d` paths, ring radii, `<title>` markup, focus-ring markup) about renderers
that no longer exist. Rewritten rather than deleted: everything asserting a FACT that moved — this file is
charted, this file links to its code page, this file-type category is represented, this wedge carries its owner
data, the cap discipline holds, the empty states are honest.

**What genuinely went away, stated rather than glossed:** server-side assertions on chart GEOMETRY. Nothing now
verifies in C# that a sector is drawn where it should be, because nothing in C# draws one. That moved to the
browser, and Task 6.9's rule is why it is stated here instead of being papered over with a test that implies
otherwise: **there is no JS harness in this repo, and the client rules are not unit-tested.** What replaces the
geometry tests is the CONTRACT — `HierarchyColorizeTests` pins the four Story 20.4 invariants per projector, the
eleven dimension declarations (every cut point, prefix, none-class, off-class and hatch key), that every class a
rule can produce is one the shipped cascade actually paints, the five `fill-opacity` states, the three
`stroke-dasharray` states, and every outcome's wording — plus live verification for the resolution of that
contract into pixels.

#### Task 7.9 — the suite

**2,619 passed, 0 failed, 3 skipped**, twice in a row on a rebuilt binary. Neither run tripped the two
known-flaky git-fixture tests, so I have nothing to distinguish from work of my own.

#### ⚠ Concurrent work in this tree (CLAUDE.md § Concurrent work on shared `main`)

A second session landed **Story 18.6 / module-aware artifact coverage** work while this story ran:
`ArtifactCoverage.cs`, `ModuleContext.cs`, `HowToReadTemplater.cs`, a new
`SiteGeneratorModuleCoverageTests.cs`, edits to `SiteGeneratorAdapterTests.cs`, plus `epics.md`,
`deferred-work.md`, ADR 0015 and the 18-2/18-6 story files. **None of it is in this story's File List** and none
of it was touched. It IS in the tree that produced the green suite above and the passing fingerprint — and
notably, the fingerprint constant was not edited by either session and still matches, so neither story's work is
rendering-visible in that fixture. Nothing was reset, checked out or cleaned.

### File List

**Source — updated**

- `src/SpecScribe/HierarchyExplorer.cs` — `HierarchyNode.Metrics`/`.TipHtml`; new `HierarchyDimension`,
  `HierarchyDimensionKind`, `HierarchyDimensionArg`; `HierarchyExplorerConfig.Dimensions`/`.Constants`;
  `HierarchyTwinDisplay.External`; the gated compact island shape + `EscapeForScriptElement`.
- `src/SpecScribe/HierarchyExplorer.Projectors.cs` — `ProjectCodeMap`, `ProjectOwnership`, the shared
  `WalkCodeMap`, and the eleven dimension declarations (`CodeMapDimensions`, `OwnershipDimensions`).
- `src/SpecScribe/Charts.cs` — **net −688 lines.** Deleted `CodeTreemap`, `CodeMapSunburst`,
  `CodeOwnershipSunburst`, `CodeOwnershipTreemap`, `BuildSunburstSvg`, `AnnularSector`, `WalkSunburstWedges`,
  `CollectFreshnessStats`, `CountFreshnessFiles`, `SunburstWedgeInfo`, `DescribeCodeMapCell`, `CodeMapCellData`,
  `AppendTreemapDir`/`AppendTreemapFile`, `BuildOwnershipDataAttrs`, the three `Freshness*` constants. Added
  `BuildTopAuthorsJson`; widened `BuildTreemapCard`, `BuildOwnershipCard`, `BuildOwnerJson`,
  `DescribeOwnershipFile`, `OwnershipFileInfo` to `internal`; added an `attributes` slot to the four
  `Ownership*Legend` emitters.
- `src/SpecScribe/CodeMapTemplater.cs` — four component instances; `data-hierarchy-reveal`; colorize select and
  both legends rewired; drill `<nav>` and shape toggle deleted; file table and pager untouched.
- `src/SpecScribe/GitInsightsTemplater.cs` — one instance replacing two charts; controls and four legends routed;
  `.board-tabs` deleted; the stale ADR 0010 §2 doc comments corrected on both the class and the section.
- `src/SpecScribe/assets/specscribe.js` — the dimension contract, the reveal hook, per-sector stroke resolution,
  three new `PATTERN_SHAPE` keys; `initCodeMapPanel` and `initOwnershipSunburst` deleted.
- `src/SpecScribe/assets/specscribe.css` — retired-SVG rules removed, colour-source rules kept and re-documented;
  new `.ss-hierarchy-legends`.
- `src/SpecScribe/SiteGenerator.cs` — `EnsureHierarchyEngine` for both pages; the island strip on the captured
  webview path.
- `src/SpecScribe/WebviewRenderAdapter.cs` — `StripDataIslands` exposed.
- `src/SpecScribe/HostRenderException.cs` — `hierarchy-chart` extended for the two surfaces.

**Tests — added**

- `tests/SpecScribe.Tests/HierarchyColorizeTests.cs` (new, 18 methods / 20 cases).

**Tests — updated**

- `ChartsTests.cs` (44 geometry tests deleted), `CodeMapTemplaterTests.cs`, `GitInsightsTemplaterTests.cs`,
  `StylesheetTests.cs`, `HierarchyRolloutTests.cs`, `SiteGeneratorCodeMapTests.cs`,
  `SiteGeneratorGitInsightsTests.cs`, `SiteGeneratorWebviewTests.cs`, `SiteGeneratorSpaTests.cs`.

**Other**

- `.claude/launch.json` — a preview entry for this session.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status only.

---

## Change Log

- 2026-07-27 — **Story 20.9 implemented (dev-story). Epic 20 AC#2 is satisfied: exactly one implementation of a
  hierarchy chart now exists in the codebase**, evidenced by an EMPTY rollout-completeness allowlist (the test
  kept, now asserting emptiness), zero-result searches for every deleted symbol and CSS class across `src/`,
  `tests/` and the extension shim, and `Charts.cs` shedding its polar geometry entirely. The component grew a
  **dimension contract** (owner decision D1: a per-node raw metric bag plus a declarative per-dimension rule,
  because staleness takes a free 1–60 month threshold and the spotlight an arbitrary contributor, so neither can
  be precomputed as a finite class set); **Code Map** converted to four instances — 8 charts collapsed to 4,
  keeping its pure-CSS filter (D2) and its file table as the twin (20.6 D1, expressed as a new named
  `HierarchyTwinDisplay.External`); **Git Insights ownership** converted to ONE instance — 2 charts collapsed to
  1 — and **gained the text twin Story 20.6 recorded it as never having had** (D3), built and verified live with
  JavaScript off BEFORE its SVG was retired, as AC#3 requires. Four `Charts` entry points, `BuildSunburstSvg`,
  and both client renderers deleted. **Entry conditions were verified by grep, not by status: Story 20.7 is
  `review` rather than `done`, but its class-list resolver, its client node filter and its six-symbol allowlist
  are all in the committed tree.**
  **Findings that differed from the story's:** F2(a) and F2(b) were **already solved by 20.7** (`tokenFor`
  already applies a verbatim class list and already composites `fill-opacity`), so only F2(c)'s three
  `stroke-dasharray` states needed work — plus a fourth channel the story did not name, `.spotlight-touched`'s
  own stroke, which `marker.line` CAN express and which made per-sector stroke resolution necessary. F8
  understated itself: **`Charts.AnnularSector` was already dead before this story**, left behind by 20.7 with
  zero callers, which only a search could have shown. `CompactMetricsTail` was NOT chart-exclusive (the Risk
  Quadrant calls it) and was restored — anti-pattern 9, caught by the compiler. `BuildOwnershipDataAttrs` was
  deleted despite Task 4.4's keep-list, because the payload reads `BuildOwnerJson` directly and it had no
  reachable caller.
  **Two gaps in surrounding code that this conversion exposed:** the vendored engine `<script>` never reached
  either page (both build their own document, so the `AssetManifest` flag never applies — the same miss 20.7 made
  on four surfaces), and the webview's inline-island strip **never ran on captured pages**, which was ~470 KB of
  dead payload before this story and would have been **4.5 MB** after it. Both fixed; the second brought the code
  up to the already-registered `data-island` host exception rather than adding a new divergence.
  **A defect of my own, caught only by looking at the rendered page:** the reveal hook first gated on the HOST's
  width, and `.ss-hierarchy` is itself `display:none` until mount — so it deferred every chart on the site while
  the full suite stayed green. Fixed to measure the panel. Exactly the class of defect CLAUDE.md § Verification
  exists for.
  **AC#4, the byte accounting, and it does not flatter the spike:** `code-map.html` **6,597,752 → 4,451,207
  (−2,146,545)** against a projected −3,493,000; `git-insights.html` **2,296,875 → 1,569,710 (−727,165)** against
  −1,510,735 — **57% of projection overall**. The split explains it: islands are 3,288,932 B and 1,193,573 B, of
  which 42% and 36% are the rich hover cards `.ss-tooltip` requires to survive the engine swap, giving **840–936
  B/node against the spike's 195.4 and Story 20.5's measured ~390**. The spike's figure was measured on planning
  nodes that carry neither a nine-value metric bag nor a per-file card. Plotly paid for itself here, at a little
  over half the projected rate, and the projection was optimistic because it generalized from a cheaper node. One
  change moved this materially: `System.Text.Json`'s default encoder escapes every `<`/`>`/`&` to six bytes and
  the biggest field in these payloads is HTML (2.8 MB on code-map alone), so the dimension-bearing payload now
  uses relaxed encoding plus a narrow neutralization of `</` and `<!` — the only two sequences that can end or
  re-frame a `<script type="application/json">` element — asserted for both safety and round-trip, including a
  hostile path containing `</script><img src=x onerror=…>`. **Gated on "declares dimensions"**, so the six
  surfaces 20.7 converted keep byte-identical islands.
  **The golden fingerprint did NOT move** — no regeneration — confirming F6 and 20.6 Task 4.1: the fixture is not
  a git repo and neither page renders in it, so it was never this story's net. Live-verified across all four
  Code Map checkbox combinations, all seven Code Map dimensions, all four ownership modes plus both live inputs,
  and both surfaces with JavaScript genuinely disabled (twin complete as a **SET** match — 1,189/1,189 files, 0
  missing — and the pure-CSS filter still working). **Reported honestly rather than richly: this repo's roster is
  two deep and one is a bot which the alphabetical picker DEFAULTS to, and staleness has NO spread at all here
  (the deep-git window is 0.6 months end to end), which is more degenerate than F6 predicted.** Screenshots still
  impossible in this harness — a fourth story owing one — so sizes (Code Map 640, ownership raised 480 → 560) are
  unverified by eye and are one-line config changes. `#dir=` retired per Open Question #2. Tests: **44 geometry
  tests deleted, ~33 rewritten against the payload and the twin, 21 new**; the coverage that genuinely went away
  is server-side chart GEOMETRY, and there is no JS harness, which the new `HierarchyColorizeTests` states in its
  own class comment rather than implying coverage that does not exist. Suite **2,619 passed / 0 failed / 3
  skipped, twice consecutively**; neither known git-fixture flake fired. A concurrent session's Story 18.6
  module-coverage work is in this tree and is excluded from the File List.
- 2026-07-26 — Story 20.9 drafted (create-story). Context assembled from ADR 0012 (+ its 20.4 addendum), ADR 0013, ADR 0010 §3/§4, the Epic 20 rollout inventory as amended by Story 20.7's owner decision D1, Stories 20.4/20.5/20.6/20.7/7.11/7.12 as they actually stand, **a code-level read of both surfaces' emitters and both client renderers in the live tree at `261b300`**, and **a real `--deep-git` generation run** (714 pages, 68.4 s) so the byte accounting AC#4 demands starts from measured numbers rather than the spike's projection. Three owner decisions elicited and locked: **D1** the dimension contract is a **per-node metric bag plus a declarative per-dimension rule**, because two of the eleven dimensions — staleness (a free 1–60 month threshold) and spotlight (an arbitrary contributor) — cannot be precomputed as a finite class set at all, so a class-per-dimension payload would have to drop or freeze both; **D2** Code Map **keeps its four filter panels and lazy-mounts on reveal**, because the pure-CSS exclude-spec/exclude-tests toggle is the one filter on that page that works with JavaScript off and trading it for a byte win would take information from a no-JS visitor; **D3** Git Insights gets **the component's generic nested twin**, not a per-directory rollup (which would be shorter but would not enumerate every node the chart draws, failing ADR 0013 §2's completeness predicate) and not a restored per-file table (which would re-litigate a design Story 7.11 deleted on owner feedback). Eight code-verified findings are promoted to a read-first section: **F1** three of the four Code Map panels are always `display:none` and **Plotly cannot lay out in a zero-width container** — the component ships `responsive:true` and sets only height, and a CSS-only reveal fires no resize event, so D2 requires a reveal hook; the same pass also collapses 8 Code Map charts to **4 instances** and 2 ownership charts to **1**, because the component's own selector replaces two more `display:none` pairs. **F2** `tokenFor` composes only `{fill,stroke}` while five of the new families' states carry `fill-opacity` (0.35/0.55) and three carry `stroke-dasharray` — the first renders wrong silently, the second is a non-color channel Plotly's `marker.line` cannot express and must extend `PATTERN_SHAPE`; verified that a bare class-list probe does resolve both families, since none of their rules needs an ancestor. **F3** the non-color channel on these surfaces is **text, rewritten on every dimension switch** (per-node accessible names plus a legend swap), not hatching — a dimension whose fill changes and whose name does not is a UX-DR17 failure that ships green; the shipped wording's honesty (bucket level not raw value, "not among this file's most-active tracked contributors", explicit "(date unknown)") must be ported verbatim. **F4** two hash schemes collide — Code Map's `#dir=` vs the component's `hashKey` — and four instances on one page need four distinct `DomId`/`HashKey` pairs; ownership gains drill-in for the first time, which is the epic's own logged owner request from the Story 7.11 design session. **F5** `HierarchyExplorerConfig.Size` is a single square int applied as height while Code Map's treemap is 1000×640. **F6** measured today, this repo's ownership roster is `["Matt Eland","dependabot[bot]"]` — the solo-repo reframe does **not** trip so the chart renders, but the second contributor touched exactly two files, so three of four modes are near-degenerate and "all four modes verified" would overstate the evidence; and per 20.6 the golden fixture is not a git repo, so **neither page renders in it** and the fingerprint is not this story's net. **F7** **58** entry-point test references plus **68** CSS-class assertions (`codemap-cell` 31, `ownership-wedge` 20, `ownership-cell` 17) — larger than 20.7's F4, and the class-name half is the trap, because those fail at runtime rather than at compile time and many assert a fact that moves to the payload and twin. **F8** `Charts.BuildSunburstSvg` has exactly two callers, both this story's, so **this story deletes the last hand-rolled arc geometry in C#**; and the 2,712 + 2,220 rich hover cards are where the byte accounting actually lands. Measured starting state recorded so AC#4 is reported against real numbers: `code-map.html` **6,020,916 B** (spike projected −3,493,000) and `git-insights.html` **2,129,514 B** (spike projected −1,510,735) — together exceeding the whole −4,787,124 B portal net, which is why this is the story that settles whether the Plotly adoption paid for itself. Sequencing recorded as blocking: **Story 20.7 must land first** (its F3 capabilities are this story's foundation and its Task 10.4 allowlist is what this story empties), itself blocked by 20.6 and by 20.5 reaching `done`. baseline_commit `261b300`.
