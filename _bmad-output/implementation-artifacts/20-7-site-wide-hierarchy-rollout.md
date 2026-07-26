---
baseline_commit: 1116e134e1708a34a902d59c18f06fbf7c118e9c
---

# Story 20.7: Site-Wide Rollout — Every Sunburst and Treemap Through the Component

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) (+ its Story 20.4 addendum) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
**Blocked by:** **Story 20.6 must land first.** ADR 0013 §3 is a hard per-surface gate: this story retires the first SVG, and it may only retire one on a surface 20.6's audit record has cleared. 20.6 is `ready-for-dev` and not started.
**Also blocked by:** Story 20.5 reaching `done` — it is `review`, and its continuing verify round is **uncommitted in this tree right now**, across `HierarchyExplorer.cs`, `DashboardViewBuilder.cs`, `SunburstExplorer.cs`, `specscribe.js`/`.css` and `HierarchyExplorerTests.cs` (see § Concurrent work).
**Gates:** Story 24.2 (Epic 24 must not add renderers 4/5/6 to a file whose existing ones are being deleted) and the new Story 20.9.
**Baseline commit:** `1116e13`

## Story

As a maintainer eliminating the drift ADR 0010 §6 could not prevent,
I want the hierarchy call sites converted to the component and their superseded renderers deleted,
so that exactly one implementation of a hierarchy chart exists in the codebase.

---

## ⛔ Read first — what this story IS and IS NOT

**IS:** convert **five** surfaces to the Story 20.5 component — dashboard, epics index, epic detail, story detail, Impact Map — **retire their server-rendered SVG**, delete the three planning `Charts.cs` entry points and 20.2's client explorer, and take the webview's documented text-twin degradation.

**IS NOT** (each belongs to a named story; doing it here breaks a ratified gate or blows the scope the owner set):

| Not this story | Whose it is | Why |
|---|---|---|
| Converting **Code Map** and **Git Insights ownership** | **Story 20.9** (added by this story's owner decision D1) | Both carry a live client-side *colorize dimension* the component has no concept of — 7 dimensions × 4 filter variants and 4 live ownership modes. That is new component capability, not a port. |
| Deleting `CodeTreemap`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeOwnershipTreemap` | Story 20.9 | They are still the live implementation of two surfaces. |
| Deleting `initCodeMapPanel` / `initOwnershipSunburst` | Story 20.9 | Same. |
| Retiring an SVG on a surface **20.6 recorded as FAIL** | 20.6 fixes it, or 20.9 does as it converts | ADR 0013 §3 is per-surface and blocking. |
| The **ADR 0005 CSP amendment** | Story 23.4 | ADR 0012 §5: landed **once**, not twice. Owner decision D3 takes the pre-authorized fallback instead. |
| Deleting `Charts.SunburstCompanionList` | Nobody — it **stays** | Story 20.6 D4: a designed visible navigation panel, not an accessibility artifact. |
| Pulling `RelatedWork.MaxEntriesPerGroup` | Owner, after this story reports the post-retirement number | Owner decision D5. |
| The richer dashboard details pane | Story 20.8 | Independent of this rollout; both touch the dashboard, so read § Concurrent work. |

---

## Owner decisions locked at create-story (2026-07-25)

Five, elicited against the real working tree and the live code at all seven call sites. **D1 amends this story's acceptance criteria** (and `epics.md` in the same change, per CLAUDE.md § Decision records); D2–D5 constrain how they are met.

**D1 — Scope: split by risk. Five surfaces here, two in a new Story 20.9.**
This story converts the four **planning** surfaces (dashboard, epics index, epic detail, story detail — all on the `--status-*` token model the component already speaks) **plus the Impact Map**, which is already client-rendered and already ships the exemplar text twin, so its conversion is a JS swap rather than an SVG retirement.
**Story 20.9** grows the component a colorize-dimension contract and converts **Code Map** and **Git Insights ownership**, deleting the remaining four `Charts.cs` entry points and two JS renderers.
Rationale, and it is a measured one rather than a feeling: Code Map renders **7 colorize dimensions × 4 filter variants = 8 charts** with a client-side dimension switch that re-fills every rect (`CodeMapTemplater.cs:196-218`, `specscribe.js:939`), and Git Insights ownership renders **4 live modes** plus a contributor select and a staleness threshold (`GitInsightsTemplater.cs:140-151`, `specscribe.js:1211`). Neither is expressible in today's `HierarchyNode.StatusClass`. **Epic 20 AC#2's "exactly one implementation" is satisfied at 20.9, not here** — say so in the Completion Notes rather than implying the epic's goal landed.

**D2 — Selector: standardize the ordering, not the default shape.**
Every selector reads **Sunburst | Treemap** in that order site-wide — the divergence AC#1 names, and the ordering Story 20.5 already fixed. **Which shape is checked on load stays per-instance config** (`HierarchyExplorerConfig.Shape`): `sunburst` for the four planning surfaces, `treemap` for the Impact Map. Nothing a visitor sees on load changes; only the control's label order does. A deep file tree reads better as rectangles and demoting that would be a regression dressed as consistency.

**D3 — Webview: the text twin, as the documented accepted degradation.**
`WebviewRenderAdapter` keeps its `data-island` strip and its `asset.js` exception (it deliberately ships no `specscribe.js` at all), so a converted surface has no chart there — and after this story no SVG either. The webview therefore presents the **server-rendered text twin**, which is exactly the fallback **ADR 0012 §5 and ADR 0013 §7 both pre-authorize**. The ADR 0005 CSP amendment stays with Story 23.4, where ADR 0012 §5 requires it to land once. **This closes Story 20.5's D4, which deferred the decision here.** Accepted cost: webview surfaces lose the chart picture until 23.4. The 20.4 spike proved Plotly renders under the byte-verbatim shipped policy, so this is a sequencing choice, not a technical limit — record it that way.

**D4 — Impact Map hierarchy: epic → directory → file, and filtering hides epic subtrees.**
The epic becomes a real level, so the existing multi-select is showing/hiding root children — something the component can do generically — and Plotly's own drill-in then scopes to one epic for free. The chart's shape finally **matches its text twin**, which is already epic-grouped (`Charts.ImpactMapBody`).
**Visible change, and it is a real one:** a file touched by three epics draws three times, once under each, instead of merging into one tile. Root-level churn therefore reads as *total attributed churn*, not *distinct-file churn*. The legend and the framing sentence must say so; do not let two numbers disagree silently. Flag it at the owner's verify round.

**D5 — Payload ceiling: measure after the SVG retires, then decide.**
`RelatedWork.MaxEntriesPerGroup` stays at 12. Report the real post-retirement dashboard byte count and the rail's share of it. Today's **283,263 B of 742,107 B (38.2%)** is a *pre-retirement* figure and this story deletes the SVG out from under it, so capping now would be acting on a number about to change. Pulling the lever is the owner's call.

---

## Acceptance Criteria

*Amended by owner decision D1 from "all seven surfaces" to the five named below; the residue moves to Story 20.9. The amendment is recorded in `epics.md` under Story 20.7 in the same change. Everything else is verbatim.*

1.
**Given** the Epic 20 rollout inventory, **scoped by D1 to: dashboard, epics index, epic detail, story detail, Impact Map**
**When** the rollout completes
**Then** every one of those surfaces renders through the Story 20.5 component with a single consistent selector **ordering** and idiom — ending the current divergence where the Impact Map orders *Treemap, Sunburst* and every other toggle disagrees (per D2 the *default shape* remains per-instance config)
**And** each converted surface is verified in a live browser (CLAUDE.md § Verification), with its Story 20.6 twin audit passing before its SVG is retired.

2.
**Given** the superseded implementations **in this story's scope**
**When** the rollout completes
**Then** `Charts.cs`'s three **planning** hierarchy entry points (`Sunburst`, `EpicSunburst`, `TaskSunburst`) and `specscribe.js`'s 20.2 explorer (`initSunburstExplorers` / `initSunburstExplorer`) and the Impact Map's arc renderer (`renderSunburst` / `arcPath`) are removed
**And** no remaining code path constructs a **planning** sunburst or the Impact Map's shapes by any other route (verified by search, not assumed — a symbol's absence is confirmed, per the shared-main verification rule)
**And** the four Code Map / ownership entry points and `initCodeMapPanel` / `initOwnershipSunburst` are **left standing and named in the Completion Notes as Story 20.9's**, so a reader cannot mistake a partial rollout for a complete one.

3.
**Given** the VS Code webview and the SPA adapter
**When** the converted surfaces render on those hosts
**Then** HTML/SPA/webview parity holds via the existing `RenderParity` harness
**And** per owner decision D3 the webview presents the text twin as the documented accepted degradation (ADR 0012 §5 / ADR 0013 §7), registered in `HostRenderExceptions` rather than left as a silent divergence.

---

## 🔴 Four findings that change the work — read before planning

All four are code-verified 2026-07-25 against the live tree. **None of them appears in the epic, in either ADR, or in Story 20.5's or 20.6's task text.** Each one is work this story must do that AC#1 does not name.

### F1 — Deleting the three entry points also deletes the legend, and the shipped legend-emphasis interaction dies with the SVG

`SunburstLegend` is emitted **inside** the entry points being deleted: `Charts.cs:614` (`Sunburst`), `:1102` (`EpicSunburst`), `:1226` (`TaskSunburst`). Delete them and all four planning surfaces lose their legend outright.

Worse, and less obvious: the Story 3.5 **legend-emphasis** behavior is pure CSS keyed on the SVG's own wedges —
`.sunburst-panel:has(.sb-review-item:hover) .sb-seg:not(.sb-review) { … }` (`StylesheetTests.cs:426`). Plotly draws `path.surface`, not `.sb-seg`, and Story 20.5 established that **CSS cannot even stroke a Plotly sector here**. So once the SVG goes, those rules match nothing and hovering a legend chip does nothing. The guard test `Script_DoesNotImplementLegendEmphasis` (`StylesheetTests.cs:452`) forbids re-implementing it from JS *including in comments*.

**What this story must do:** lift the legend into the component's framed block, which is what ADR 0012 §2 requires anyway (*"One framing block — Story 10.2 legend … supplied by the component, not hand-written per call site"*). `BuildSunburstLegendItems` (`Charts.cs:954`) is `private` and must become reachable. The **drilled-legend filtering** — `[data-explorer][data-sb-scope] .sunburst-legend .sb-legend-item { display: none }` plus the `data-tok-*` reveals (`StylesheetTests.cs:466-474`) — acts on legend items, not on sectors, and **survives unchanged**; keep it working, it is the more valuable half. The hover-emphasis loss is **Open Question #1**: recommended answer is accept and record it, but it is a visible interaction the owner has been using, so raise it rather than bury it.

**Two smaller casualties in the same blast radius, both easy to lose silently.** (a) The `.sunburst-hint` ring-explainer these entry points also emit — *"Inner ring: tasks &middot; outer ring: subtasks…"* (`Charts.cs:1232`) — is a genuine reader aid and disappears with them; decide deliberately whether it becomes part of the component's framing or is subsumed by `Charts.WhyText`. (b) Their **empty states**: `Sunburst` returns `<div class="chart-empty">Nothing to chart yet.</div>` and `TaskSunburst` returns `"No tasks tracked for this story yet."`, while `HierarchyExplorer.Render` returns `""` for an empty model. On the dashboard and story detail the panel is conditional so `""` is correct; **on the epics index it is not conditional** (`HtmlRenderAdapter.Epics.cs:31-33` renders the panel unconditionally), so a project with no epics would go from an honest note to a bare heading. Preserve the honest empty state per surface (NFR8) — a missing panel is not an empty state.

### F2 — Three of the five surfaces have no projector, and `HierarchyExplorer` has exactly one

`HierarchyExplorer.ProjectDashboard` (`HierarchyExplorer.cs:130`) is the only projection that exists, and it is dashboard-shaped. This story needs three more, each a **pure projection over the view model the call site already holds** — no `ProjectCounts` re-count, no second walk (ADR 0012's whole reason for existing):

| Surface | Today | Needs |
|---|---|---|
| Epics index | `Charts.Sunburst(model, followUps, unplanned)` — the **same datasource** as the dashboard | Reuse `ProjectDashboard` with a different `DomId`, `Mode: Navigate`, and its own `HashKey`. No new projector. |
| Epic detail | `Charts.EpicSunburst(epic, hrefBuilder, size:320, followUps, unplanned)` (`Charts.cs:1003`) | **New** `ProjectEpic` — one epic's stories, its epic-level follow-ups, and story-child deferred; the `hrefBuilder` closure becomes per-node `Href` in the payload. |
| Story detail | `Charts.TaskSunburst(tasks, size:280, deferred)` (`Charts.cs:1128`) | **New** `ProjectStoryTasks` — task → subtask, plus the Deferred parent and its items. Note this datasource has **no `--status-*` lifecycle**: its legend is `pending`/`done` (+ `followup-open`/`followup-done`), which `Charts.SunburstLocalStatusLabel` and `StatusStyles.StoryLabel` must be checked against, not assumed. |
| Impact Map | client-rendered from `impact-map-data` | **New** `ProjectImpactMap` — D4's epic → directory → file tree. |

Every one of these must produce a payload that satisfies the four 20.4 findings *by construction*: one root, no `null` values, `parent == Σ children`, and an emitted `branchvalues` matching (`HierarchyExplorer.BranchValues`).

### F3 — The Impact Map needs two capabilities the component does not have

D4 removes the *merge-on-filter* problem but not these two:

**(a) A second color family.** Impact tiles are colored by a **5-level sequential commit ramp** (`.impact-level-1` … `.impact-level-5`), not by `--status-*`. The component's resolver is hard-coded: `STATUS_CLASS` maps `statusClass` → an `sb-*` class and `tokenFor` resolves it by putting `class="sb-seg " + cls` on a throwaway `<path>` (`specscribe.js:1782-1812`).
**Recommended generalization, and keep it small:** have the server emit the node's **resolvable class list** (e.g. `colorClass: "sb-seg sb-done"` for planning, `"impact-tm-tile impact-level-3"` for the Impact Map) and have `tokenFor` apply that string verbatim. `statusClass` stays for the twin, the accessible name and the `data-tok-*` publication. **Never re-type a token value in JS** (AD-7) — the whole point is that a token change moves the chart with it.

**(b) A client-side node filter.** The epic multi-select must hide epic subtrees and re-plot. Under D4 that is generic: filter the node list to the selected roots and their descendants, re-run the parent roll-up, `Plotly.react`. Put it on the component, config-gated, not in an Impact-Map-shaped branch — a surface-specific escape hatch inside the shared component is precisely the drift this epic exists to end.

Both are **also what Story 20.9 will build on**, so design them for two consumers, not one.

### F4 — 58 test references point at the three entry points being deleted

`Charts.Sunburst(` appears **44** times in `tests/`, `Charts.EpicSunburst` **11**, `Charts.TaskSunburst` **3**. Many assert on SVG path geometry that is about to stop existing. They are not all deletions: the ones asserting a *fact* (this epic appears, this story links there, this weight is honest) should be **rewritten against the payload and the twin**, which is exactly the coverage ADR 0013 §6 and Story 20.6 Task 4.2 move the net to. The ones asserting *geometry* go. Budget for this explicitly; it is the largest single chunk of the story and the easiest to under-estimate.

`SunburstCompanionList` has **18** test references and **8** source references and is **not** being deleted — do not sweep it up.

---

## Tasks / Subtasks

### Task 0 — Entry conditions (blocking)

- [ ] 0.1 Confirm **Story 20.6 is `done`** and read `20-6-text-twin-audit.md`. It is this story's per-surface permission slip. **Retire no SVG on a surface its record does not clear.** If a surface in D1's five is recorded FAIL, fix its twin here as part of that surface's conversion (20.6 D2 explicitly hands per-surface twin fixes to this story) — and re-verify it live with JS off before deleting anything.
- [ ] 0.2 Confirm **Story 20.5 is `done`** and that its `SunburstExplorer.cs` `expandDenseEpics` change is committed. At this story's draft time it was **uncommitted** in the working tree.
- [ ] 0.3 `git status` before starting. Another session's work has been in this tree at the start of every Epic 20 story. **Never `git reset --hard` / `git checkout --` / `git clean`.**

### Task 1 — Generalize the component for its second and third consumers (AC: #1) — F3

- [ ] 1.1 **Color family.** Add a per-node resolvable class list to `HierarchyNode` (recommended name `ColorClass`) and have the JS `tokenFor` apply it verbatim instead of composing `"sb-seg " + STATUS_CLASS[…]`. Planning surfaces emit `"sb-seg sb-<token>"` so **their resolved colors are byte-identical to today** — assert that, don't assume it. `StatusClass` stays and keeps driving the twin, the accessible name, and the `data-tok-*` publication that the pure-CSS drilled legend consumes.
- [ ] 1.2 Keep the `PATTERN` hatching channel working for the four non-lifecycle statuses (`specscribe.js:1791`) and make it resolve from the same class list, so a second family can carry its own non-color channel later (UX-DR17: never color alone).
- [ ] 1.3 **Node filter.** Add a config-gated client filter: given a set of visible root ids, project the node list to those roots plus descendants, re-run the parent roll-up **client-side using the same rule the emitter uses** (children win — a filtered parent must not keep an unfiltered total), and `Plotly.react`. Announce the change through the existing `.ss-hierarchy-live` region. Generic — no surface name appears in it.
- [ ] 1.4 **Re-run Story 20.5's survival predicate after a filter change.** It is a re-render the a11y layer must survive, and it is a *new* one the 20.5 verification never exercised: sectors > 0, `role="treeitem"` on every sector, non-empty `aria-label` on every sector, **exactly one `tabindex="0"`**. Clamp the roving index — the sector count shrinks on filter exactly as it does on drill (20.4's sixth finding, 20.5 Task 5.6).

### Task 2 — Lift the legend into the component (AC: #1) — F1

- [ ] 2.1 Make the legend part of `HierarchyExplorer.Render`'s framed block (ADR 0012 §2). Expose what `BuildSunburstLegendItems` (`Charts.cs:954`) computes rather than duplicating it — one source for the legend's item set, as today.
- [ ] 2.2 Emit the **same markup** (`.sunburst-legend` / `.sb-legend-item` / `.sb-<token>-item`) so `Stylesheet_HasDrilledLegendScopeRules` and the `data-tok-*` reveal keep working untouched. **Do not weaken `Script_DoesNotImplementLegendEmphasis`** — the script names no legend class, in code or in comments (`StylesheetTests.cs:452-462`).
- [ ] 2.3 Verify live that drilling still filters the legend to the tokens on screen. That is the interaction that survives; prove it rather than assuming the attribute publication is enough.
- [ ] 2.4 Record the **hover-emphasis loss** honestly in the Dev Agent Record and raise it at the verify round (Open Question #1). Do not describe the legend as "preserved" if half its behavior is gone.

### Task 3 — Convert the dashboard and retire its SVG (AC: #1)

- [ ] 3.1 Delete the `fallbackHtml` argument at the dashboard call site and delete `DashboardViewBuilder.RetainedSunburstHtml` (`:162` — its own doc comment says *"Story 20.7 deletes this method"*). Keep `panelClass: "chart-panel sunburst-panel …"` and `panelAttributes: " data-explorer"` exactly — the `.sunburst-panel` class carries three `StylesheetTests` assertions and `data-explorer` is the root the takeover handshake and the 20.3 rail both key on.
- [ ] 3.2 The takeover handshake becomes vestigial once there is nothing to take over. Simplify it deliberately: `data-explorer-ready` was **20.2's** guard, and 20.2's block is deleted in Task 7. Decide whether anything still reads it (the 20.3 rail's re-sync path reads `data-sb-scope`, not this) and remove it if nothing does — **verified by search, not by memory**.
- [ ] 3.3 `Charts.SunburstCompanionList` and the 20.3 rail **stay**. The component twin's presentation is whatever 20.6 Task 3.1's knob set it to (`ScreenReaderOnly` on this surface per 20.6 D4) — do not re-decide it here.
- [ ] 3.4 Verify live with JS **on** and **off** before considering the surface converted.

### Task 4 — Convert the epics index (AC: #1)

- [ ] 4.1 `HtmlRenderAdapter.Epics.cs:31-33` — replace the hand-written `<div class="chart-panel sunburst-panel"><h3>Project at a Glance</h3>` + `Charts.Sunburst(...)` with the component's framed block. **Route it through `EpicsIndexView`** (a new `HierarchyExplorerHtml` field built in `EpicsViewBuilder.BuildIndex`), never ad-hoc string-building in the adapter — AD-2, and the exact thing the 21.1 review had to patch.
- [ ] 4.2 Same datasource as the dashboard, so reuse `ProjectDashboard` — **do not fork the projection**. Different `DomId`, `Mode: HierarchyMode.Navigate` (this surface has no details pane), its own `HashKey`, and its own `Size`.
- [ ] 4.3 `Charts.SunburstCompanionList` at `:36-42` **stays** exactly as it is.

### Task 5 — Convert epic detail (AC: #1) — new projector

- [ ] 5.1 New `HierarchyExplorer.ProjectEpic` over `EpicView`'s data. Resolve each story's destination once, in the projector, from the same expression the call site uses today (`view.Prefix + (story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id))`, `HtmlRenderAdapter.Epics.cs:209`) — the `Func<StoryInfo,string>` closure becomes payload `Href`. Do not invent a second destination rule; Story 9.13's contract is the one that holds.
- [ ] 5.2 Cover what `EpicSunburst` actually draws — epic-level follow-ups and story-child deferred, not just stories. Diff the projected node set against the SVG's before deleting the SVG; `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` is the pattern to copy for this surface.
- [ ] 5.3 `Mode: Navigate`. `Size` from config; the SVG's 320 is a starting point, not a requirement — this chart now labels and drills, so verify legibility live rather than porting the number.
- [ ] 5.4 If 20.6 recorded this surface's twin as **FAIL** (its hypothesis was FAIL — `role="img"` + the page's story cards), the twin fix lands here, before the SVG goes.

### Task 6 — Convert story detail (AC: #1) — new projector

- [ ] 6.1 New `HierarchyExplorer.ProjectStoryTasks` over `view.Tasks` + `view.DeferredFromThis`: task → subtask, plus the Deferred parent and its items, mirroring what `TaskSunburst` draws (`Charts.cs:1128-1235`).
- [ ] 6.2 **This datasource's statuses are not the lifecycle vocabulary** — its legend is `pending`/`done` plus `followup-open`/`followup-done` (`Charts.cs:1220-1225`). Check `HierarchyExplorer.StatusLabelFor` produces sane prose for them; `StatusStyles.StoryLabel` is not obviously right for a *task*. If it is not, the fix belongs in `Charts.SunburstLocalStatusLabel` — the one source — never a second phrasing invented here (20.3's live round caught exactly that class of drift).
- [ ] 6.3 Handle the empty case (F1's second casualty): `TaskSunburst` returns a "No tasks tracked for this story yet." note and the panel is conditional on `view.Tasks.Count > 0 || view.DeferredFromThis.Count > 0` (`:547`). The component returns `""` for an empty model. Keep the honest empty state; do not let it become a missing panel (NFR8). **Check the same question on the epics index**, whose panel is *not* conditional.
- [ ] 6.4 `Mode: Navigate`; deferred segments keep their click-through.

### Task 7 — Convert the Impact Map (AC: #1) — D4

- [ ] 7.1 New `HierarchyExplorer.ProjectImpactMap`: root → **epic** → directory → file. Sizes from churn; per-node `ColorClass` from the existing commit-level ramp so the colors are the shipped ones. File `Href` from `file.CodePageHref` prefixed exactly as today (`ImpactMapTemplater.cs:144`).
- [ ] 7.2 Wire the existing epic multi-select to Task 1.3's filter. **Keep the shipped controls markup** (`.impact-epic-filter`, the sprint-board dropdown) — this story changes what drives the chart, not the control vocabulary.
- [ ] 7.3 **State D4's counting change where a reader sees it.** A file under three epics now contributes three times, so root churn is *total attributed churn*. Put it in the framing sentence / legend (`Charts.WhyText`, `.impact-legend`) so the chart and the epic-grouped list below cannot appear to disagree. This is the visible product change in this story — flag it for the verify round.
- [ ] 7.4 `Shape: "treemap"` (D2 — default shape stays per-surface), selector ordered Sunburst-then-Treemap like everywhere else.
- [ ] 7.5 The `<details open>` epic-grouped list stays and stays **open by default** for JS-off; the component may collapse it once mounted, exactly as `initImpactMap` does today (`specscribe.js:1432`). 20.6 D1 keeps this twin rather than replacing it with the generic nested list — **do not replace it**.
- [ ] 7.6 Delete `impact-map-data` and its payload builder once the component's island carries the same facts; two islands on one page is the drift, not the fix.

### Task 8 — Delete the superseded implementations and prove it by search (AC: #2)

- [ ] 8.1 Delete `Charts.Sunburst`, `Charts.EpicSunburst`, `Charts.TaskSunburst` and any helper that becomes unreachable **only** because of them.
- [ ] 8.2 **Keep** `SunburstStoryWeight`, `SunburstEpicWeight`, `SunburstNoPlanStoryWeight`, `SunburstEpicAggregates`/`SunburstOrphanAggregates`/`SunburstUnplannedAggregates`, `SunburstCompanionList`, `BuildSunburstLegendItems`, `SunburstLocalStatusLabel`, `Framed`/`ChartMeta`/`WhyText`. The projector and the legend depend on them, and **AC#4 of Story 20.5 lives inside `SunburstNoPlanStoryWeight`** — deleting or re-flooring it silently un-does the owner's no-plan average bump. `SunburstGlanceSize` is the one to *check* rather than assume: after `RetainedSunburstHtml` goes, confirm by search whether anything still reads it and delete it if nothing does — keeping a dead constant is the same sin as deleting a live one, in the other direction.
- [ ] 8.3 Delete `specscribe.js`'s `initSunburstExplorers` / `initSunburstExplorer` (`:2388`, `:2401`) and the Impact Map's `renderSunburst` / `arcPath` (`:1573`, `:1585`). Delete `Charts.SunburstExplorerIsland` / `SunburstExplorerData` / `SunburstExplorerDataId` and the `.sb-explorer-*` CSS family once nothing references them.
- [ ] 8.4 **Leave standing, and name them in the Completion Notes as Story 20.9's:** `CodeTreemap`, `CodeMapSunburst`, `CodeOwnershipSunburst`, `CodeOwnershipTreemap`, `initCodeMapPanel`, `initOwnershipSunburst`.
- [ ] 8.5 **Prove absence by search, not by assumption** (CLAUDE.md § Concurrent work — a `Charts.cs` edit has silently vanished in this repo). Grep for every deleted symbol name across `src/`, `tests/`, and the extension shim, and record the searches and their zero results in the Debug Log. A build that compiles is not the same evidence.
- [ ] 8.6 Check `SunburstExplorerNode` / `SunburstExplorerNodes`: the projector still uses the walk, so it survives — but its `expandDenseEpics` flag and the `Projector_NodeSet_EqualsTheWedgesTheSvgDrew` invariant both exist to keep the payload honest **against an SVG that no longer exists**. Decide deliberately what that invariant becomes (recommended: retarget it at the twin, which is now the thing that must not disagree) and say so; do not just delete the guard.

### Task 9 — Hosts and parity (AC: #3) — D3

- [ ] 9.1 Add a `HostRenderExceptions` entry for the webview's chart degradation, in the house style of the existing four: name the surface, the fact, and a reviewable reason citing ADR 0012 §5 / ADR 0013 §7 and pointing at Story 23.4 as where the amendment lands. **Amend the existing `data-island` entry's reasoning too** — it currently says *"The static SVG chart and its Story 9.13 links are untouched, so no information is lost"* (`HostRenderException.cs:49-56`). After this story that sentence is **false**; the twin is what carries the information. A stale reason in the exceptions registry is worse than no reason.
- [ ] 9.2 Verify the webview surface actually reaches the twin: the island strip is a regex over `<script type="application/json">` (`WebviewRenderAdapter.cs:79-83`) and the twin is `<details>`/`<div>` markup, so it should survive — **confirm it**, and confirm the twin's links resolve under the webview's path rewriting.
- [ ] 9.3 SPA: the island and twin must survive content capture. Extend the existing `SiteGeneratorSpaTests` island-survives-capture test rather than adding a parallel one; confirm `specscribe:content-swapped` re-init still mounts every converted instance (there are now up to five distinct ones, and the epics index and dashboard can both be in a session).
- [ ] 9.4 `RenderParity` / `RenderSectionParityTests` green across `html`, `spa`, `webview`.
- [ ] 9.5 `SunburstExplorerTests.WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks` asserts the webview keeps **the chart**. That is no longer true. Rewrite it to assert the webview keeps **the twin and its links** — do not delete it; it is the test that proves D3's degradation is a degradation and not a hole.

### Task 10 — Tests (AC: #1, #2, #3)

- [ ] 10.1 Work through the 58 references (F4). Rewrite fact-asserting tests against the payload and twin; delete geometry-asserting ones. Report the split in the Completion Notes — how many rewritten, how many deleted, and what coverage genuinely went away.
- [ ] 10.2 Per new projector (`ProjectEpic`, `ProjectStoryTasks`, `ProjectImpactMap`), assert the four 20.4 invariants: exactly one root, no `null` in values, `parent == Σ children`, emitted `branchvalues == HierarchyExplorer.BranchValues`.
- [ ] 10.3 Assert **AC#4 of Story 20.5 still holds after conversion**: a no-plan story's payload value equals `Charts.SunburstNoPlanStoryWeight(model, geometry)` on the dashboard and the epics index. Epic 20's own AC text calls this out — *"Story 20.7's conversion must carry the average-bump forward"* — and it is verified visually in Task 11, not only by assertion.
- [ ] 10.4 A **rollout-completeness** test in the spirit of the anti-drift invariants this epic keeps producing: assert no source file outside `HierarchyExplorer` constructs a planning hierarchy chart. Name the Code Map / ownership survivors as an explicit allowlist with a `[Story 20.9]` comment, so the allowlist shrinking to empty is 20.9's finish line and cannot be quietly widened instead.
- [ ] 10.5 Assert the planning surfaces' resolved sector colors are unchanged by Task 1.1's generalization.
- [ ] 10.6 Keep `Script_DoesNotImplementLegendEmphasis` and `Stylesheet_HasDrilledLegendScopeRules` green.
- [ ] 10.7 Per converted surface, assert the **honest empty state** survives (F1): no epics → the epics index still says something rather than rendering a bare heading; no tasks → the story-detail note still appears. And keep the shipped privacy guard green — `displayModeBar:false`, no `sendDataToCloud`, no `cdn.plot.ly` / `plotly.com` string — now that four more instances construct a Plotly config.
- [ ] 10.8 **Do not unit-test the JS** — SSR-first, no JS harness. Task 11 is the verification for Tasks 1, 7 and 9's client behavior. Say so plainly rather than implying coverage that does not exist (20.5 Task 7.6's discipline).

### Task 11 — Live-browser verification, per surface (AC: #1) and the fingerprint

- [ ] 11.1 Generate to `SpecScribeOutput/` **with `--deep-git`** — without it the Impact Map does not render at all. Never `--output docs/live`. Serve via `.claude/launch.json` → `specscribe-output`, port 8099.
- [ ] 11.2 **Per converted surface**, with JS **on**: chart renders, selector switches shape in place, drill/breadcrumb/Escape work, hash round-trips, zero console errors, and the ten-step survival predicate holds. Five surfaces means five passes — record them separately. A single "verified" for the set is not evidence.
- [ ] 11.3 **Per converted surface**, with JS genuinely **off** in the browser (not assumed): the twin is present, complete, navigable, non-color; the chart host does not leave a chart-sized blank box; no inert controls are visible. This is the ADR 0013 §3 gate being exercised, and it is the last moment before the SVG is gone.
- [ ] 11.4 **AC#4 as a visual assertion:** un-drafted stories render at a typical, clickable size — measure a real sector's sweep, as 20.5 did (median 3,926 px², minimum 2,123 px²), not "it looks fine".
- [ ] 11.5 **Colorway audit** on every converted surface, built at runtime from the shipped cascade — including the Impact Map's new ramp family. Zero foreign colors, text fills included. `spike/plotly/probe-src/explorer.js` is gone (20.5 Task 1.6 deleted it); the working implementation now lives in the 20.5 verification notes — re-derive from the shipped `.sb-*`/`.impact-*` cascade, never from a typed token value.
- [ ] 11.6 **Take screenshots.** The owner has **still never seen a pixel of this chart** — 20.4 and 20.5 both owed one and both were beaten by a pane that would not composite. This story retires the SVG, so the picture the owner has never seen becomes the only picture. Try hard; if it still refuses, say so and fall back to computed-geometry evidence.
- [ ] 11.7 **Golden fingerprint.** It moves substantially — ADR 0013 §6 measured chart SVG at **69.3% of the dashboard body** and this story deletes it. Regenerate, **confirm stable across two repeated runs**, and **name whose concurrent changes it sits on top of**. The replacement assertions are 20.6's and must already be in place (ADR 0013 §6: they land *before* the first retirement); confirm they are, and that they still fail when the payload or twin regresses — a replacement net that cannot fail is not a net.
- [ ] 11.8 **Report the post-retirement byte numbers (D5):** dashboard total, the rail's share, the twin's share, the island's share, and the net delta against the pre-retirement 742,107 B. Compare against the 20.4 spike's **−4,787,124 B** portal projection and its **amortised** reading — this story converts small surfaces plus one large one, and 20.5 measured the real per-node cost at **roughly double the spike's 195.4 B/node**. Say whether the projection is holding.
- [ ] 11.9 Full suite, real numbers. Two git-fixture tests are known to flake under parallel load (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything you caused.

---

### Review Findings

*(populated by code-review at epic end — Epic 20's review runs once every story is complete and the owner is satisfied)*

---

## Dev Notes

### Why this story exists, in one paragraph

ADR 0010 §6 required one shared charting engine and it did not hold: three concurrent sessions produced three arc renderers, three divergent `Treemap | Sunburst` toggles, and seven `Charts.cs` hierarchy entry points. Story 20.5 built the component that makes that hard to reinvent; this story is the subtraction that makes it *true* — it removes the alternatives rather than merely adding a better one. It is also the first story in the epic that actually **deletes** a server-rendered chart, which is why ADR 0013 §3's gate and Story 20.6's audit stand in front of it.

### Architecture compliance

- **ADR 0012 §2** — one component is the only route to a hierarchy chart. This story makes that true for the planning family and the Impact Map; Story 20.9 finishes it. §2 also puts the **legend** inside the component's framing block, which F1 turns from a nicety into required work.
- **ADR 0012 §3** — `navigate` on the four converted non-dashboard surfaces, `select` on the dashboard (unchanged from 20.5). Drill-in stays a distinct affordance from activation.
- **ADR 0012 §5** — the CSP amendment lands **once**, with Story 23.4. Owner decision D3 takes the pre-authorized text-twin fallback instead of landing it twice.
- **ADR 0012 §6** — presentation is SpecScribe's tokens, never Plotly's colorways; Task 1.1's generalization must not become a place where a color value gets typed.
- **ADR 0012 §7 / ADR 0010 §3** — data computed once at generation time and embedded. Task 1.3's client filter re-projects an **already-embedded** payload; it must never re-derive from live state.
- **ADR 0013 §2/§3** — the twin contract and the hard per-surface live JS-off gate. This story exercises the gate; it does not get to reinterpret it.
- **ADR 0013 §6** — the fingerprint replacement lands **before** the first retirement. That is 20.6's deliverable and this story's entry condition (Task 0.1, 11.7).
- **ADR 0013 §7** — the webview text-twin fallback, exercised here (D3).
- **ADR 0002 / AD-2** — payload and config are host-neutral view-model data. Build them in the emitter and route them through the view models (`EpicsIndexView`, `EpicView`, `StoryView`), never ad-hoc in `HtmlRenderAdapter`.
- **NFR-5 as amended by ADR 0013** — JS-off may lose the visualization; it must never lose **information** or **navigation**.
- **UX-DR5/6/7/16/17/18/21** — and UX-DR17 specifically for Task 1.2: a second color family still may not signal by color alone.
- **FR31** — generation-time determinism; an identical from-scratch regen.

### Anti-patterns to prevent

1. **Deleting an entry point before its surface's twin has passed 20.6's audit live with JS off.** That is the one gate this whole story is downstream of.
2. **Deleting `SunburstNoPlanStoryWeight`, `SunburstStoryWeight` or `SunburstEpicWeight` as "part of the sunburst".** They are the projector's inputs and they carry Story 20.5 AC#4's owner-directed no-plan bump.
3. **Deleting `SunburstCompanionList` or the 20.3 rail.** Both stay (20.6 D4).
4. **Sweeping up the Code Map / ownership entry points because they look like the same family.** They are Story 20.9's and they are still live.
5. **Forking the dashboard projection for the epics index.** Same datasource, same walk, one projector.
6. **Re-deriving a destination rule in a new projector.** Story 9.13's contract is the one that holds; lift the existing expression.
7. **Re-typing a token value in JS** to make the second color family work (AD-7). Resolve through the live cascade.
8. **Putting an Impact-Map-shaped branch inside the shared component.** Config-gated generic capability, or it is the drift again.
9. **Re-implementing legend emphasis from JS.** A guard test forbids it, including in comments. Publish state; CSS decides — and if CSS can no longer reach the mark, that is a loss to *report*, not to route around.
10. **Claiming the rollout is complete.** It is complete at Story 20.9. Epic 20 AC#2's "exactly one implementation" is not satisfied by this story.
11. **Proving a symbol is gone by "the build passed".** Grep, and record the searches.
12. **Locking a fingerprint captured off one run or a stale build**, or without naming whose concurrent work it sits on.
13. **`git reset --hard` / `git checkout --` / `git clean`.** This has already destroyed real work mid-story in this repo.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `HierarchyExplorer.Render` / `IslandHtml` / `TextTwinHtml` | `HierarchyExplorer.cs:301,362,414` | the whole framed block; extend it, never build a second one |
| `HierarchyExplorer.BranchValues` | `HierarchyExplorer.cs:117` | assert against the constant, never a literal `"total"` |
| `HierarchyExplorer.Reparent` / `RollUpParentValues` | `HierarchyExplorer.cs:187` | the one place Findings A and C are satisfied; every new projector goes through it |
| `HierarchyExplorer.StatusLabelFor` / `Charts.SunburstLocalStatusLabel` | `HierarchyExplorer.cs:511`, `Charts.cs:945` | prose status — one vocabulary for chart, twin and legend |
| `Charts.Framed` + `ChartMeta` + `WhyText` | `Charts.cs:42,153` | the Story 10.2 framing block; never hand-write a "why this matters" sentence |
| `specscribe:explorer-select` | `specscribe.js`, consumed by the 20.3 rail | detail `{nodeId, label, root}`; `nodeId` null at root scope |
| `specscribe:content-swapped` | `specscribe-spa.js` | every content-enhancing block listens; five instances now depend on it |
| `data-sb-scope` + `data-tok-<status>` | script → stylesheet | keeps the pure-CSS drilled legend and its guard test working |
| `HostRenderExceptions.Registry` | `HostRenderException.cs:23` | the ONLY legitimate way a surface diverges; an unregistered divergence is a bug |
| `RenderParity` / `RenderSectionParity` | `RenderParity.cs:170,383` | the parity harness AC#3 names |
| `AssetManifest.HierarchyEngineNeeded` + `SiteGenerator.EnsureHierarchyEngine` | 20.5's asset seam | flag computed from the rendered body; **disk is the truth**, not the in-memory copied flag (20.5's watch-session defect) |
| `Charts.ImpactMapBody` + `<details open>` | `ImpactMapTemplater.cs:56-62` | 20.6 F4's reference twin — audited and kept, not replaced |

### Files being modified — current state

*Every line reference below was verified 2026-07-25 against the live tree. **Verify each one again before relying on it** — another session is editing this tree.*

- **`src/SpecScribe/HierarchyExplorer.cs` (516 lines) — UPDATE.** `ProjectDashboard` `:130` is the only projector. `Reparent` `:187` and `RollUpParentValues` `:219` are generic and reusable. `Render` `:301` takes `fallbackHtml` — **this story removes that parameter** once no surface has a fallback. `IslandHtml` `:362`, `TextTwinHtml` `:414`, `StatusLabelFor` `:511`.
- **`src/SpecScribe/Charts.cs` (4,896 lines) — UPDATE (subtractive + one visibility change).** Delete `Sunburst` `:451`, `EpicSunburst` `:1003`, `TaskSunburst` `:1128`. `SunburstLegend` `:792` and `BuildSunburstLegendItems` `:954` are **private** and called from inside all three — they must survive and become reachable by the component (F1). `SunburstCompanionList` `:660` **stays**. `CodeTreemap` `:2939`, `CodeMapSunburst` `:3496`, `CodeOwnershipSunburst` `:3957`, `CodeOwnershipTreemap` `:4016` are **Story 20.9's — do not touch**.
- **`src/SpecScribe/SunburstExplorer.cs` (partial `Charts`) — UPDATE.** `SunburstExplorerNodes` `:83` survives as the projector's walk (note its **uncommitted** `expandDenseEpics` parameter). `SunburstExplorerData` `:216`, `SunburstExplorerIsland` `:234`, `SunburstExplorerDataId` `:62` are 20.2's island and go with 20.2's JS block.
- **`src/SpecScribe/DashboardViewBuilder.cs` (404 lines) — UPDATE.** `BuildHierarchyExplorerHtml` `:125` drops its `fallbackHtml` argument; `RetainedSunburstHtml` `:162` is **deleted** (its own comment says so).
- **`src/SpecScribe/HtmlRenderAdapter.Epics.cs` (716 lines) — UPDATE.** `:31-33` epics-index sunburst; `:204-211` epic-detail sunburst; `:546-553` story-detail sunburst. All three currently hand-write their own `<div class="chart-panel sunburst-panel"><h3>…` — the component supplies that now.
- **`src/SpecScribe/EpicsView.cs` / `EpicsViewBuilder.cs` — UPDATE.** New `HierarchyExplorerHtml` fields on `EpicsIndexView` (`:87`), the epic-detail view and the story-detail view, built in the builder (AD-2).
- **`src/SpecScribe/ImpactMapTemplater.cs` (155 lines) — UPDATE.** `BuildInteractiveBody` `:73-154` — the controls, the two mount points, the `impact-map-data` island. `<details open>` fallback `:56-62` **stays**.
- **`src/SpecScribe/assets/specscribe.js` (2,942 lines) — UPDATE.** Component block `:1691-2372`; `tokenFor`/`STATUS_CLASS`/`PATTERN` `:1782-1820` (Task 1.1). Delete `initSunburstExplorers` `:2388` / `initSunburstExplorer` `:2401`. Rework `initImpactMap` `:1415` and delete its `renderSunburst` `:1573` / `arcPath` `:1585`. **`initCodeMapPanel` `:939` and `initOwnershipSunburst` `:1211` are Story 20.9's — do not touch.**
- **`src/SpecScribe/assets/specscribe.css` — UPDATE.** `.sb-explorer-*` `:162-188` goes with 20.2's block; `.sb-seg`/`.sb-<status>` `:2972-3030` and `.sunburst-legend` `:3032` **stay** (the legend and the token cascade the component resolves against both depend on them).
- **`src/SpecScribe/HostRenderException.cs` — UPDATE.** The `data-island` entry `:49-56` carries a reason that becomes false; a new chart-degradation entry joins it (Task 9.1).
- **`src/SpecScribe/WebviewRenderAdapter.cs` — READ, then a doc correction.** `JsonDataIsland` `:79-83` and the comment at `:70-74` both describe the retained SVG.
- **Tests — UPDATE, heavily.** `SunburstExplorerTests`, `HierarchyExplorerTests`, `StylesheetTests` (`:419-474`), `SiteGeneratorAdapterTests` (fingerprint + `GoldenOutputInventory`), `SiteGeneratorSpaTests`, `RenderParityTests`, `RenderSectionParityTests`, and the 58 references from F4.

### Project Structure Notes

No new page, no new nav entry, no new dependency, no new asset. Net **subtraction** in `Charts.cs` and `specscribe.js`; net addition of three projectors and two component capabilities. One new file expected: none required, though `HierarchyExplorer.cs` may warrant splitting per-surface projectors into a partial if it grows past comfortable — a judgement call, not a requirement.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; JS is verified in a live browser and its *content* asserted by string tests over the shipped asset (`StylesheetTests` is the established pattern for both CSS and JS guards). Golden fingerprint = `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` — regenerate deliberately, confirm stability across two runs, record provenance. **Note the coverage shift this story causes:** ADR 0013 §6 measured chart SVG at 69.3% of the dashboard body, and after this story the fingerprint no longer sees it. 20.6's replacement assertions are what stands in its place; confirm they can actually fail.

### Previous story intelligence

**Story 20.5 (`review`)** — the component, and four facts that land directly here. (a) Its **round-2 owner verify** added `shortLabel` (Plotly's `uniformtext` draws every label at ONE size, so a long title silences the chart), per-sector contrast text colors, the portal's own `data-tip-html` tooltip in place of Plotly's hovercard, a `Detail` sentence that replaced the reader-facing word "weight", and a selection ring on `marker.line` because **CSS genuinely cannot stroke a Plotly sector**. (b) A **transition on a Plotly-owned property reads back through `getComputedStyle` as never applied** in a non-compositing pane — it cost three wrong diagnoses; the CSS says why it must not come back. (c) The dashboard mount **added** bytes (island 31,404 B + twin 24,168 B = +10.9%), so **budget roughly double the spike's 195.4 B/node**. (d) A watch-session topology change wipes the output root and deletes an asset the in-memory "copied" flag still claims — `EnsureHierarchyEngine` treats the **disk** as truth. Its uncommitted `expandDenseEpics` change is owner-directed: the component drills, so it expands dense epics the static SVG collapsed.

**Story 20.6 (`ready-for-dev`, NOT STARTED)** — this story's gate. Its pre-audit hypotheses for the five surfaces here: dashboard **PARTIAL→fixed there**, epics index **PARTIAL→fixed there**, epic detail **FAIL**, story detail **PROBABLE FAIL**, Impact Map **PROBABLE PASS (the exemplar)**. So expect to fix **epic detail and story detail twins here** as you convert them — 20.6 D2 hands per-surface twin fixes to this story explicitly. Its F1 is why `SunburstCompanionList` can never be the dashboard's twin (epic-level only; `Charts.cs:668` omits done epics with no open follow-ups).

**Story 20.4 (`done`, code-reviewed)** — the measured facts: plotly.js **3.7.0** pinned, standard bundle **1,223,515 B**, `--strict` buys nothing, **CSP violations do not appear in console captures** (a test that greps the console passes while the chart is blank — ask the DOM), promises resolve **off an animation frame** so hang everything on `plotly_afterplot`, and the **−4,787,124 B** portal delta is **amortised** — `code-map.html` and `git-insights.html` alone exceed the whole net, which is precisely why those two are Story 20.9's and why *this* story should not expect a byte win.

**Story 20.3 (`done`)** — `specscribe:explorer-select` is the seam; the rail re-syncs on init from `data-sb-scope`, so publishing that attribute is not optional decoration.

**Story 20.2 (`done`, 22 review patches)** — `SVGAElement` has no `.click()`; an SVG `<a>` at `display:none` **stays focusable**; `kind` ≠ `ring`; story ids come from `### Story N.M:` headings with **no dedupe anywhere**, so a projector must not assume uniqueness. Most of this stops mattering when the SVG goes — which is itself worth stating in the Completion Notes.

**Story 21.3 (`done`)** — built the Impact Map and its `<details open>` twin under exactly the assumption ADR 0013 later made universal.

**Owner workflow (`CLAUDE.md`)** — the post-implementation round where the owner verifies rendered behavior is the **designed gate**, not rework. D4's counting change and F1's emphasis loss will both draw commentary. Expect it; leave both easy to adjust.

### Git intelligence summary

Baseline `1116e13`. The recent history is Story 25.1 (SonarCloud CI), the Epic 25.5/25.6 + Epic 27 seating that landed mid-draft, and `bcca682` ("18.1, 18.2, 20.5, 20.8, 5.3") — a single commit carrying five stories, which is structural here because code review runs at epic end. **Scope any later review of this story by its own File List and declared symbols, never by a commit range** (CLAUDE.md § Scoping a code review), and state the exclusion in the review record.

Two things from the recent history bear on this story directly: `98a90c6` made the golden fingerprint **portable** by fixing checkout *and* date dependence — do not reintroduce either when you regenerate it; and Story 25.1 stood up the repo's **first build/test CI**, so a regression here now fails a gate rather than only a local run.

### Latest technical information

**Nothing needs re-researching, and that is itself the instruction.** plotly.js is **pinned at 3.7.0 (MIT, released 2026-07-03)** and Story 20.5 checked for newer on 2026-07-25 — the same day this story was drafted — and found none. **A version bump invalidates every measured number in the 20.4 spike and must be its own decision, not a side effect of this rollout.** Do not upgrade the vendored bundle here.

Two 3.7.0 facts stay load-bearing through the conversion and must survive it on every new instance, not just the dashboard's: **`displayModeBar: false`** is a privacy requirement rather than a cosmetic default (3.7.0's `sendDataToCloud` button uploads the chart to Plotly Cloud), and `plotlyServerURL: ''` / `topojsonURL: ''` keep the portal offline-capable (NFR-3). The guard test asserting the shipped JS sets `displayModeBar:false` and names no `sendDataToCloud`, `cdn.plot.ly` or `plotly.com` must stay green.

The vendoring tool (`tools/plotly-vendor/`) is not touched by this story: no new trace family is needed — `sunburst` and `treemap` are already in the bundle, and the Impact Map's shapes are the same two.

### ⚠️ Concurrent work — read before you start

**The tree moved twice during this story's own drafting session, and that is the point of this section.** It opened at `cd7f302` with one modified file; by the time the story was written a new commit had landed (`1116e13`, seating Epic 25.5/25.6 and a new Epic 27) and the uncommitted set had grown to:

```
 M src/SpecScribe/HierarchyExplorer.cs
 M src/SpecScribe/DashboardViewBuilder.cs
 M src/SpecScribe/SunburstExplorer.cs        (owner-directed `expandDenseEpics`, +20 lines)
 M src/SpecScribe/HtmlRenderAdapter.cs
 M src/SpecScribe/assets/specscribe.{js,css}
 M tests/SpecScribe.Tests/HierarchyExplorerTests.cs
 M _bmad-output/implementation-artifacts/sprint-status.yaml
 ?? web/
```

That is **Story 20.5's continuing verify round still in flight**, touching every file this story is about. Story 20.5 is `review`, Story 20.8 is `ready-for-dev` against the same dashboard, and Story 20.6 must land between now and this story starting.

Consequences per CLAUDE.md § Concurrent work:

- **Grep-verify every symbol and line reference in this file before relying on it.** A `Charts.cs` edit has silently vanished in this tree before, and `RelatedWorkCards.cs` changed between two reads during Story 20.8's drafting session.
- **Expect the golden fingerprint to have moved under you.** Confirm stability across two repeated runs and name the provenance.
- **Expect the build to be transiently broken by someone else's rename.** Wait, do not reset.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.7] — the three ACs and the seven-surface rollout inventory, as amended by D1 in the same change
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §2 the component contract (incl. the legend in the framing block), §3 the mode contract, §5 the CSP amendment and its text-twin fallback, §6 tokens, §7 determinism, and the Story 20.4 addendum
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 the twin contract, §3 the hard per-surface live JS-off gate, §6 the fingerprint replacement, §7 the webview fallback
- [Source: `_bmad-output/implementation-artifacts/20-5-hierarchy-explorer-component.md`] — the component as built, the round-2 owner verify changes, the honest byte accounting, and D4's deferral of the webview decision to this story
- [Source: `_bmad-output/implementation-artifacts/20-6-text-twin-audit-and-fingerprint-replacement.md`] — the audit inventory, D1/D2 (per-surface twin fixes come here), F1/F2/F3/F4
- [Source: `_bmad-output/implementation-artifacts/20-4-spike-report.md`] — the measured Plotly facts and the amortised byte reading
- [Source: `CLAUDE.md`] — § Concurrent work on shared `main`, § Verification, § Scoping a code review, § Decision records
- Code: `HierarchyExplorer.cs:117,130,187,301,362,414,511`, `Charts.cs:451,614,660,792,945,954,1003,1102,1128,1226`, `SunburstExplorer.cs:62,83,216,234`, `DashboardViewBuilder.cs:125,162,180,185`, `HtmlRenderAdapter.Epics.cs:31,204,546`, `ImpactMapTemplater.cs:56,73,144`, `CodeMapTemplater.cs:139,152,158,196`, `GitInsightsTemplater.cs:14,140,174,179`, `WebviewRenderAdapter.cs:70,79`, `HostRenderException.cs:23,49`, `RenderParity.cs:170,383`, `specscribe.js:939,1211,1415,1573,1691,1782,2388,2401`, `StylesheetTests.cs:419,426,452,466`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Legend hover-emphasis dies with the SVG (F1).** Recommended: **accept and record it.** The drilled-legend filtering — the half that answers "what's left in this scope" — survives. Re-creating hover-dim would require JS to reach Plotly sectors, which the `Script_DoesNotImplementLegendEmphasis` guard exists to prevent. This is a visible interaction loss on a surface the owner uses; it deserves an explicit yes/no rather than a silent regression.
2. **What becomes of `Projector_NodeSet_EqualsTheWedgesTheSvgDrew`?** Recommended: **retarget it at the text twin.** The invariant's job is that the payload never claims or omits something the reader can see; the twin is now that reader-visible surface. Deleting the guard because its counterpart went away would remove the anti-drift net at exactly the moment it matters most.
3. **Should the epic-detail and story-detail charts keep their small sizes (320 / 280)?** Recommended: **raise them modestly and verify live.** Those numbers were chosen for a static SVG that neither labelled nor drilled. Do not port a constant that was a constraint.
4. **Does the Impact Map keep both shapes at all?** Recommended: **yes.** D2 keeps treemap as its default and the selector standardizes ordering only. Raised because D4's epic level makes the sunburst substantially more readable than it was, and the owner may prefer it as the default after seeing it.

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

---

## Change Log

- 2026-07-25 — Story 20.7 drafted (create-story). Context assembled from ADR 0012 (+ its 20.4 addendum) and ADR 0013, the Epic 20 rollout inventory, Stories 20.5/20.6/20.8 as they actually stand, and a **code-level read of all seven call sites in the live tree** rather than of the epic's table. **Five owner decisions elicited and locked**, one of which amends the acceptance criteria: **D1** the rollout **splits by risk** — this story converts the four planning surfaces **plus the Impact Map** (already client-rendered, already carrying the exemplar twin), while **Code Map and Git Insights ownership move to a new Story 20.9**, because both carry a live client-side colorize dimension the component has no concept of (7 dimensions × 4 filter variants = 8 charts; 4 live ownership modes + contributor select + staleness threshold) and that is new component capability, not a port — so **Epic 20 AC#2's "exactly one implementation" is satisfied at 20.9, not here**; **D2** the selector standardizes **ordering only** (Sunburst | Treemap everywhere), with the **default shape staying per-instance config** so a deep file tree keeps its treemap; **D3** the **webview takes the text twin** as the documented accepted degradation that ADR 0012 §5 and ADR 0013 §7 both pre-authorize, leaving the ADR 0005 CSP amendment with Story 23.4 where it must land once — this **closes Story 20.5's deferred D4**; **D4** the converted Impact Map draws **epic → directory → file** so the multi-select becomes a generic subtree filter and the chart finally matches its epic-grouped twin, at the accepted cost that a file touched by three epics draws three times and root churn reads as *total attributed* churn; **D5** the payload ceiling is **measured after the SVG retires**, not capped now, because deleting the SVG changes the denominator under 20.3's 38.2% figure. Four code-verified findings that the epic, both ADRs, and Stories 20.5/20.6 all miss are promoted to a read-first section: **F1** `SunburstLegend` is emitted *inside* all three entry points being deleted (`Charts.cs:614,1102,1226`), so the legend must move into the component's framing block (which ADR 0012 §2 required anyway) — and the Story 3.5 **hover-emphasis** interaction dies regardless, because its CSS keys on `.sb-seg` and Plotly draws `path.surface`, with a guard test forbidding a JS re-implementation; **F2** three of the five surfaces have **no projector at all** and `HierarchyExplorer` ships exactly one, so `ProjectEpic`, `ProjectStoryTasks` and `ProjectImpactMap` are new work, each of which must satisfy the four 20.4 data-contract findings by construction; **F3** the Impact Map needs **two capabilities the component lacks** — a second color family (its 5-level commit ramp; today's resolver hard-codes `"sb-seg " + STATUS_CLASS[…]`) and a client-side node filter — both of which Story 20.9 will also need, so they must be designed for two consumers; **F4** **58 test references** point at the three entry points being deleted (44 / 11 / 3), and the split between rewrite-against-the-payload and delete-as-geometry is the largest and most under-estimated chunk of the story. Sequencing recorded as blocking rather than advisory: **Story 20.6 has not started** and is the ADR 0013 §3 gate that grants per-surface permission to retire an SVG, and its pre-audit rates **epic detail FAIL and story detail PROBABLE FAIL**, so their twin fixes land here as those surfaces convert. Concurrent work recorded as evidence rather than as boilerplate: **the tree moved twice during this drafting session** — it opened at `cd7f302` with one modified file and closed at `1116e13` (a new commit seating Epic 25.5/25.6 and Epic 27) with Story 20.5's continuing verify round uncommitted across `HierarchyExplorer.cs`, `DashboardViewBuilder.cs`, `SunburstExplorer.cs`, `specscribe.js`/`.css` and `HierarchyExplorerTests.cs`, i.e. across every file this story is about. Story 20.8 is `ready-for-dev` against the same dashboard, and the golden fingerprint will move substantially because ADR 0013 §6 measured chart SVG at **69.3% of the dashboard body** — exactly the coverage 20.6's replacement assertions must already be standing in for before this story deletes anything. baseline_commit `1116e13`.
