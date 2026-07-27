# Story 20.6 — Text-Twin Audit Record (the ADR 0013 §3 per-surface gate)

**Audited:** 2026-07-26 · **Auditor:** dev-story (Story 20.6) · **Baseline commit:** `86b35c2`
**Authority:** [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) §2 (twin contract), §3 (hard
per-surface gate), §4 (supersedes ADR 0010 §2), §6 (fingerprint replacement).

> **This file is Story 20.7's permission slip and its work list.**
> 20.7 may retire a surface's server-rendered chart SVG **only** where this record says **PASS**.
> Where it says **FAIL**, the sentence *"This surface keeps its server-rendered SVG"* appears explicitly, and the
> specific missing facts are enumerated so 20.7 implements against a list rather than re-auditing.

---

## Verdict summary

| # | Surface | (a) Server-rendered | (b) Complete | (c) Navigable | (d) Non-color | Verdict |
|---|---|---|---|---|---|---|
| 1 | **Dashboard** `index.html` | PASS | PASS | PASS | PASS | ✅ **PASS — cleared for 20.7** |
| 2 | **Epics index** `epics.html` | PASS | **FAIL** | PARTIAL | PASS | ⛔ **FAIL — keeps its SVG** |
| 3 | **Epic detail** `epics/epic-N.html` | PASS | **FAIL** | **FAIL** | PASS | ⛔ **FAIL — keeps its SVG** |
| 4 | **Story detail** `epics/story-N-M.html` | PASS | PASS | N/A | PASS | ✅ **PASS — cleared for 20.7** |
| 5 | **Code Map** `code-map.html` | PASS | PASS¹ | PASS | PASS | ✅ **PASS — cleared for 20.7** |
| 6 | **Git Insights** `git-insights.html` | **FAIL** | **FAIL** | **FAIL** | **FAIL** | ⛔ **FAIL — keeps its SVG** |
| 7 | **Impact Map** `impact-map.html` | PASS | PASS | PASS | PASS | ✅ **PASS — the exemplar** |

¹ one bounded gap named in §5; it does not block retirement.

**Four surfaces cleared. Three keep their SVG.** Per owner decision D2 only surface 1 was *fixed* here; a recorded
FAIL plus a retained SVG is the designed outcome, not a defect.

---

## Method — and proof it was actually applied

Per CLAUDE.md § Verification and AC#1, every verdict below was taken **in a live browser with JavaScript
disabled**, not from a test assertion and not by grepping HTML.

**How JS was disabled.** The audit site was served with a `Content-Security-Policy: script-src 'none'` header
(harness: `scratchpad/jsoff_server.py`, launch config `twin-audit-20-6-jsoff`). This was chosen over stripping
`<script>` tags because the document arrives **byte-identical** — so predicate (a) stays meaningful — the DOM is
built identically, and `<script type="application/json">` data islands are not executable and therefore survive,
exactly as they do with JS switched off in browser settings. Verified beforehand that the generated site emits
**zero `<noscript>` elements**, so the one behaviour CSP does not reproduce is not in play.

**Proof JS was genuinely off** (dashboard, measured live):

| Probe | Value | Why it proves execution was blocked |
|---|---|---|
| `documentElement[data-ss-hierarchy-boot]` | `false` | `HierarchyExplorer.BootScript` sets this **synchronously in the head**. Unset ⇒ even the inline script never ran. |
| `window.Plotly` | `undefined` | The 1.2 MB vendored bundle never executed. |
| `.ss-hierarchy` host child count | `0` | No chart mounted. |
| `[data-hierarchy-mounted]` / `[data-hierarchy-failed]` | absent / absent | Neither handshake outcome fired. |
| server SVG present | `true` | The owner-D1 fallback *is* the page — ADR 0013's designed JS-off state. |

> ⚠️ **Console capture was empty despite CSP blocking three scripts.** This independently re-confirms Story 20.4's
> warning that CSP violations do **not** appear in browser console captures. Every verdict here was therefore taken
> by asking the **DOM**, never the console.

**The four predicates**, applied identically to all seven surfaces:

- **(a) Server-rendered** — present with all script execution blocked. Not "present in the DOM after load".
- **(b) Complete** — every fact the chart conveys is stated: **membership** (which nodes exist), **magnitude** (as a
  number, not an angle), **structure** (as nesting, not as rings). Enumerated from the chart's own payload/SVG and
  **set-diffed** against the twin. A count match was never accepted as a set match.
- **(c) Navigable** — every link a chart node offers exists in the twin **and resolves**. Checked against the
  filesystem, not eyeballed (harness: `scratchpad/check-twin-links.ps1`).
- **(d) Non-color** — every status readable as a word, every metric as a number (UX-DR17/UX-DR19).

**Two false FAILs were caught and retracted during the audit** — both worth recording, because either would have
sent 20.7 to fix something that is not broken:
1. SVG `<title>` text concatenates a parent group name and uses `—` inconsistently; naive label splitting reported
   the dashboard's `Unplanned` node as missing. It is present. Re-diffed on stable identifier prefixes.
2. Chart `<title>` carries **raw markdown** (backticks), while the page renders it as `<code>`. Un-normalized
   substring matching reported 67 of 83 story-detail facts missing; after normalizing both sides the true number
   is **1**. Story detail is a PASS, not the hypothesized FAIL.

---

## Surface 1 — Dashboard `index.html` ✅ PASS

- **Chart entry point:** `Charts.Sunburst` (retained D1 fallback) + the Story 20.5 `HierarchyExplorer` component.
- **Twin as shipped:** `HierarchyExplorer.TextTwinHtml`, `id="dashboard-hierarchy-twin"`.
  Presentation changed by **this story** to `sr-only` (owner D4) — see §Fix below.
- **Also on the page (unchanged, and deliberately not the twin):** `SunburstCompanionList` tile grid
  ("Remaining Work by Epic", 24 tiles) and the Story 20.3 rail.

| Predicate | Verdict | Evidence (measured live, JS off) |
|---|---|---|
| (a) Server-rendered | **PASS** | Twin present in the DOM with all script execution blocked. |
| (b) Complete | **PASS** | Payload **212** nodes ↔ twin **212** `<li>`; **true set diff: 0 missing, 0 extra**. Nesting depth **3** ⇒ structure. Magnitude present as prose `Detail` ("24 of 24 tasks done", "12 stories"). Status as a word. |
| (c) Navigable | **PASS** | 212 links / 206 unique targets, **0 unresolved** against the filesystem. |
| (d) Non-color | **PASS** | 11 distinct prose status words: *Whole project, Done, Done follow-up, Open follow-up, In development, In review, No task plan, Ready for dev, Stories drafted, Unrecognized, Direct change*. |

**Twin vs. the SVG the chart actually draws.** The SVG draws **145** segments, of which **7** are dense-epic
collapse summaries ("Epic 6: 12 stories (sized by …)") and **138** are real nodes. Every one of those 138 appears
in the twin. The twin's extra 74 entries are the individual stories the SVG's 7 summaries collapse — because the
component is built with `expandDenseEpics: true` while the retained SVG collapses. **The twin is a strict superset
of the SVG's node set**; a summary wedge is a rendering device, not a fact that can be lost.

**F1 re-confirmed live — and it is why D4 is right rather than a compromise.** `SunburstCompanionList` cannot be
this surface's twin: measured on `epics.html` (same call), the chart draws **27 epics / 87 stories / 39 follow-up
segments** and the tile grid emits **24 epics / 0 stories / no follow-up ring** — omitting **Epics 1, 2, 4 and 8**
exactly as the done-with-no-open-follow-ups rule at `Charts.cs:668` predicts. That omission is correct for a
"what's left" panel and disqualifying for a twin. Both therefore ship: the grid keeps its navigational product
value, the twin discharges the completeness contract.

### The fix applied here (Task 3)

- `HierarchyTwinDisplay { Details, ScreenReaderOnly }` added to `HierarchyExplorerConfig` as a **trailing,
  defaulted** field — every existing call site keeps compiling and keeps the D3 default.
- `TextTwinHtml` now emits either a closed `<details>` (D3 default) or a `<section class="ss-hierarchy-twin
  sr-only">` with an `aria-labelledby` heading (D4). **The listing inside is byte-identical in both modes** —
  presentation varies, the completeness contract does not.
- The dashboard call site sets `ScreenReaderOnly`. `SunburstCompanionList` and the 20.3 rail were **not touched**.

> **⚠️ An a11y hazard this created, and how it was resolved — raise at the owner's verify round.**
> `.sr-only` is the clip-rect technique, so it deliberately stays in the accessibility tree — which is the point,
> and also a hazard: the dashboard twin carries **212 links**, and a clipped-but-focusable run that long is an
> invisible tab tunnel for a **sighted keyboard** user. Dropping the links from the tab order was rejected: that
> would break the *navigation* half of NFR-5, which is precisely what ADR 0013 says may never be lost. Instead
> `.ss-hierarchy-twin.sr-only:focus-within` un-clips the container the moment focus enters it — the pattern skip
> links use. Nothing is hidden from anyone; it stops being invisible once you are inside it.
> This is the same defect class Story 20.2's review caught live (an SVG `<a>` at `display:none` stays focusable)
> and that the suite structurally could not see.

---

## Surface 2 — Epics index `epics.html` ⛔ FAIL

**This surface keeps its server-rendered SVG.**

- **Chart entry point:** `Charts.Sunburst` (145 segments).
- **Component instance: NONE.** Verified: `epics.html` contains **0** `ss-hierarchy-data` islands, **0**
  `data-hierarchy` hosts, **0** `ss-hierarchy-twin`. Story 20.5's D1 mounted the component on the **dashboard
  only**.
- **Consequence for Task 3.2:** the "dashboard **and** epics-index" fix reduces to the dashboard alone. Story 20.6
  correctly did **not** mount a second instance here — that is 20.7's rollout, per Task 3.5's own instruction.
- **Twin as shipped:** `SunburstCompanionList` (24 tiles) plus the page's own epic list.

| Predicate | Verdict | Evidence |
|---|---|---|
| (a) Server-rendered | PASS | Tile grid and epic list present with JS off. |
| (b) Complete | **FAIL** | See enumerated gaps below. |
| (c) Navigable | PARTIAL | All 27 epic links resolve; **47 of 87** story nodes have no link outside the chart. |
| (d) Non-color | PASS | Epic list states prose status ("Done: 5", "5 of 5 done"). |

**Missing facts — 20.7's work list for this surface:**
1. **The entire story ring.** The chart draws **87** story segments; the tile grid states **0**. The page's own
   epic list carries only **40** story links, leaving **47** story nodes stated nowhere outside the chart.
2. **The follow-up ring.** **39** follow-up segments are drawn; none are enumerated as chart facts.
3. **Four epics absent from the tile grid** — Epics **1, 2, 4, 8** (`Charts.cs:668` omission). *Mitigated:* the
   page's own epic list does carry all 27 with prose status, so membership at epic level is covered by the page.
4. **Structure.** Which story belongs to which epic *as the chart groups it* is not stated.

**Recommended fix (20.7):** mount the component here with `TwinDisplay: ScreenReaderOnly` (the page already has a
visible epic list, so D4's reasoning applies) — this surface then inherits surface 1's PASS wholesale.

---

## Surface 3 — Epic detail `epics/epic-N.html` ⛔ FAIL

**This surface keeps its server-rendered SVG.** *(Audited on `epic-20.html`, 18 segments: 9 stories + 9 deferred
items.)*

- **Chart entry point:** `Charts.EpicSunburst`. **Twin as shipped:** none; the page's story cards + deferred list.

| Predicate | Verdict | Evidence |
|---|---|---|
| (a) Server-rendered | PASS | Story cards and deferred items present with JS off. |
| (b) Complete | **FAIL** | Membership passes (9/9 stories, 9/9 deferred items stated outside the chart, verified by DOM removal). **Magnitude and structure fail** — see below. |
| (c) Navigable | **FAIL** | The chart's 18 links are inside `role="img"` — see the cross-cutting finding. |
| (d) Non-color | PASS | Prose status words present on the cards. |

**Missing facts — 20.7's work list for this surface:**
1. **Magnitude: only 4 of 9 stories state their task count outside the chart.** The chart *sizes* every story by
   task count; for 5 stories that magnitude exists only inside the wedge.
2. **Structure: the story → deferred-item grouping is not stated.** The chart attributes each deferred item to its
   owning story ("(from Story 20.1)"); outside the chart only **3** of those attributions appear, and there is
   **no heading at all** introducing the deferred items as a group.
3. **The chart's whole accessible text is one string:** `aria-label="Epic story breakdown"` — for 18 nodes.

---

## Surface 4 — Story detail `epics/story-N-M.html` ✅ PASS

*Hypothesis was "PROBABLE FAIL". It passes.* The story document's **own rendered task list** is a genuine natural
twin (owner D1's "richer natural twin" case), and it is complete because it is the same source the chart is built
from.

- **Chart entry point:** `Charts.TaskSunburst`.

| Predicate | Verdict | Evidence (two stories sampled, deliberately one all-done and one all-open) |
|---|---|---|
| (a) Server-rendered | PASS | Task list present with JS off. |
| (b) Complete | **PASS** | `story-25-2` (38 segments, 0 of 8 tasks done): **38/38 stated outside the chart, 0 missing.** `story-20-5` (83 segments, all done): **82/83**; the single gap is the aggregate node *"Deferred: 2 open / 0 done"*, whose content is itself stated in the chart's `aria-label`. |
| (c) Navigable | N/A | Tasks are not pages; the chart offers only 3 links, all present in the page's own prose. |
| (d) Non-color | PASS | Chart carries prose status (`done` / `not done`); the page renders 38 real `<input type="checkbox">` elements — a state, never a hue. |

**Note for 20.7:** the sampled stories both had task plans. A story with **no** task plan should be re-checked when
that call site is converted, since the chart's "No task plan yet" node has no obvious counterpart in an empty list.

---

## Surface 5 — Code Map `code-map.html` ✅ PASS

*F3 confirmed with numbers.* **8 charts** (4 treemaps + 4 sunbursts, one pair per exclusion variant).

| Predicate | Verdict | Evidence |
|---|---|---|
| (a) Server-rendered | PASS | All four `AppendFileTable` tables present with JS off: **1115 / 378 / 975 / 256** rows. |
| (b) Complete | **PASS**¹ | **Exact set match, all 4 variants, both shapes: 0 chart file-nodes missing from the table.** Magnitude = the `Lines` column. Structure = the full directory path in each row. |
| (c) Navigable | PASS | 1115 links / 491 unique targets checked, **0 unresolved**. |
| (d) Non-color | PASS | `Type` column is a word; `Lines` a number. |

**The pager is not truncation** — verified live: **0 rows** have `display:none`, and rows 0, 500 and 1114 all
compute to `display: table-row`. JS only paginates.

**The variant toggle works with JS off — proven, not assumed.** The four variants are selected by two pure-CSS
checkboxes (`cm-exclude-spec`, `cm-exclude-tests`). Actuating them with all page script blocked switched the
visible view `[block,none,none,none]` → `[none,none,block,none]` → `[none,none,none,block]` and back, with each
variant's full table becoming non-zero height (36 845 → 32 223 → 8 485 px). No page script can have participated:
CSP blocked every one.

¹ **The one bounded gap (does not block retirement).** The charts draw **224 / 39 / 211 / 38** *directory*
aggregate nodes ("`.agents/skills` — 234 files") that the file table does not state as rows. The information is
derivable by summing the table's `Lines` within a path prefix, and every *file* is present. 20.7 should either add
a directory rollup row or record the omission as accepted.

---

## Surface 6 — Git Insights `git-insights.html` ⛔ FAIL — the hardest gap

**This surface keeps its server-rendered SVG.**

*F2 confirmed, and materially worse than the story's pre-read stated.*

- **Chart entry points:** `Charts.CodeOwnershipSunburst` + `Charts.CodeOwnershipTreemap`.
- **Twin as shipped: none. The page contains zero `<table>` elements.** Story 7.11 deleted both prior ownership
  tables (the files-and-contributors master-detail table and the plain ranked ownership table); nothing replaced
  them.

| Predicate | Verdict | Evidence (measured live, JS off) |
|---|---|---|
| (a) Server-rendered | **FAIL** | There is no twin to be server-rendered. |
| (b) Complete | **FAIL** | Charts carry **1 115 file nodes + 224 directory nodes**. Only **6** of those 1 115 files are linked anywhere outside an `<svg>`. Zero tables, zero lists in the ownership section. |
| (c) Navigable | **FAIL** | 1 115 links exist *inside* each chart but are pruned from the accessibility tree by `role="img"`; **6** are reachable outside it. |
| (d) Non-color | **FAIL** | Ownership is encoded as dominant-author-share **colour**. The legend gives colour bands ("76–100%") but **no per-file value in text**. The top-contributor, spotlight and staleness dimensions are colour-only *and* their selector is `hidden` without JS. |

**What a non-visual or JS-off visitor actually receives:** two sentences and a colour legend —
> *"Code ownership sunburst: directory structure sized by lines of code and colored by dominant-author commit
> share; 1115 files across 224 directories."*
> *"Code ownership treemap: each rectangle is a file sized by lines of code and colored by dominant-author commit
> share."*

**Additionally: the treemap emits `0` `<title>` elements** — not even a per-node tooltip. The sunburst emits 224
(directories only, not the 1 115 files).

**Missing facts — 20.7's work list:** every file's path, its line count, its dominant author, that author's commit
share, and the directory rollup — i.e. the entire dataset.

**Doc corrections applied by this story (Task 2.4):** `GitInsightsTemplater.cs`'s class summary stated the
superseded ADR 0010 §2 contract ("a real, useful default-mode chart renders and works with JS off") as this page's
whole no-JS story. Replaced with the ADR 0013 §4 contract, the measured audit result, and a pointer to this file.
A second site in the same file (`AppendOwnershipSection`'s "the required no-JS default, ADR 0010") was corrected
the same way.

> **Still-stale sites left for 20.7 — `Charts.cs` is READ ONLY for this story:**
> `Charts.cs:3947` ("the required pre-rendered no-JS default mode (AC #3)") and `Charts.cs:4113`
> ("…hidden without JS (ADR 0010)"). Both assert the ADR 0010 §2 reading that ADR 0013 §4 supersedes. 20.7 owns
> those symbols and should correct them as it converts the surface.

---

## Surface 7 — Impact Map `impact-map.html` ✅ PASS — the reference implementation

*F4 confirmed. Cite this as the pattern the failing surfaces should copy.*

- **Chart:** client-rendered treemap/sunburst (Story 21.3) reading `#impact-map-data`. With JS off there is no
  chart at all — which is exactly the world ADR 0013 creates, making this surface a live preview of the end state.
- **Twin as shipped:** `<details class="chart-panel impact-fallback" id="impact-fallback" **open**>` +
  `Charts.ImpactMapBody`, from `ImpactMapTemplater.cs`.

| Predicate | Verdict | Evidence |
|---|---|---|
| (a) Server-rendered | PASS | Present and **open** with JS off; 14 375 px tall — genuinely on screen, not merely in the DOM. |
| (b) Complete | **PASS** | Payload **993** file entries across **18** epics ↔ fallback **993** `<li>` under **18** group headings. **0 paths missing, 0 hrefs missing, 0 magnitudes unstated.** |
| (c) Navigable | PASS | 1 011 links / 246 unique targets, **0 unresolved**. |
| (d) Non-color | PASS | Every row states magnitude as numbers: *"src/SpecScribe/CodeMap.cs — 400 lines · 1 commit"*. |

**Why it is the exemplar:** it is `open` by default rather than collapsed, it groups by epic so **structure** is
carried by nesting, and it states **magnitude** as two explicit numbers per row. It was built (Story 21.3) under
the assumption the chart is client-only, and it is the only surface that already satisfies ADR 0013 §2 in full
without the component.

---

## Cross-cutting finding — `role="img"` prunes every in-chart link

Not a per-surface defect, so recorded once. Verified on the epic-detail chart: `role="img"` sits on the `<svg>`
itself and **contains all 18 of its `<a>` elements**. Per ARIA, `role="img"` prunes descendants from the
accessibility tree — so a screen-reader user receives the single `aria-label` string and **none** of the links.

Affected: epic detail (18 links), story detail (3), Code Map (8 charts × up to 1 115), Git Insights (2 × 1 115).

**Consequence:** on any surface whose twin is the chart's own markup, in-chart links **cannot** be counted toward
predicate (c). This is a structural argument for the component's separate text twin rather than for annotating the
SVG, and 20.7 should not attempt to discharge navigability by adding links inside a `role="img"` chart.

---

## Inventory discrepancy closed (Task 2.5)

ADR 0013 § Context names *"the ownership and **freshness** views"* as if they were two surfaces. **Freshness is not
a surface.** Verified at `CodeMapTemplater.cs:203–209`: "Recently changed" and "First changed" are two of **seven**
options in the Code Map's `Colorize by` `<select>`, which colours the *existing* treemap/sunburst.

Sharper still: that whole `.codemap-controls` block is emitted **`hidden`** and revealed only by JS, so with JS off
a visitor only ever sees the baked default (change frequency). The freshness *view* has no JS-off existence at all
— acceptable, because it is an enhancement over an audited surface rather than a surface of its own.

**The verified inventory is seven surfaces.** ADR 0013's prose and this record now agree.

---

## Environmental finding — the deep-git surfaces are marginal to generate

Recorded because it blocked this audit twice and will block 20.7's.

`GitMetrics` applies a hard-coded **3 000 ms** timeout to every git subprocess (`GitMetrics.cs:197`), with no CLI or
config knob. Measured on this machine: `git log --numstat` over this repo takes **6 496 ms cold** and **~2 450 ms
warm** — i.e. the warm case clears the budget by only ~18%, and the cold case fails outright.

When it fails, `TryComputeDeep` returns `null` and **`git-insights.html`, `impact-map.html` and the ~300 per-commit
pages are silently not written** — the run still reports `errors=0`, and `diagnostics.html` still says deep-git is
`on (--deep-git)`. Two audit generations produced 413 pages instead of 715 this way before the cause was found.

**Not fixed here** (out of scope, and `GitMetrics` is not this story's file). Flagged for the owner: a silent,
load-dependent loss of three surfaces that reports success is worth a story of its own.

### The same 3 000 ms timeout is what makes the git-fixture suite flaky

Story 20.6's own task text describes "two git-fixture tests known to flake under parallel load (a different one
each run, green in isolation, pre-existing and unclaimed)". Measured here, that description **understates it** and
the root cause is the same timeout:

| Full-suite run | Failures | Overlap with previous run |
|---|---|---|
| 1 (with a `--deep-git` generation running concurrently) | 19 | — |
| 2 (quieter) | 5 | different members |
| 3 (quiet) | 18 | different members again |

Every failure across all three runs is a test that shells out to `git`. The tell: `GitMetricsFirstCommitDateTests
.TryGetFirstCommitDate_ReturnsNull_ForNonexistentPath` — a trivial null check — took **34 s** and failed, because
~2 400 tests running in parallel starve `git` past its 3 s budget. The affected families pass **8/8 in isolation
in 22 s**.

This is not "two tests"; it is the whole deep-git fixture surface, and it is load-dependent rather than random.
Relevant to 20.7 because that story converts the Code Map and Git Insights call sites and will run this suite a
lot: **run deep-git families in isolation, or at reduced parallelism, before concluding anything is broken.**

---

## Audit environment

- Generated with `--deep-git` to an **isolated** output directory rather than `SpecScribeOutput/`. Reason: a
  concurrent session regenerated `SpecScribeOutput/` **without** `--deep-git` mid-audit, deleting `git-insights.html`
  and `impact-map.html` after they had been verified present. Per CLAUDE.md § Concurrent work the shared tree is
  not contested; an isolated copy was used instead. `--output docs/live` was never used.
- Served over HTTP (not `file://`) so link resolution and relative paths behave as they do in production.
- Harnesses kept in the session scratchpad: `jsoff_server.py`, `check-twin-links.ps1`.

---

## What Story 20.7 may now do

| Surface | 20.7 permission |
|---|---|
| Dashboard `index.html` | ✅ **May retire the server SVG.** |
| Story detail `epics/story-N-M.html` | ✅ **May retire the server SVG.** Re-check a story with no task plan. |
| Code Map `code-map.html` | ✅ **May retire the server SVG.** Consider the directory-rollup gap (§5 note ¹). |
| Impact Map `impact-map.html` | ✅ Already client-only; nothing to retire. Use as the reference twin. |
| Epics index `epics.html` | ⛔ **Keeps its SVG** until the story + follow-up rings are stated. |
| Epic detail `epics/epic-N.html` | ⛔ **Keeps its SVG** until per-story magnitude and the deferred-item grouping are stated. |
| Git Insights `git-insights.html` | ⛔ **Keeps its SVG** until a twin exists at all. The largest single piece of work in the rollout. |
