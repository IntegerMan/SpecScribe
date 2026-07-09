# Sprint Change Proposal — Story 3.4 Reinterpretation

**Date:** 2026-07-08
**Author:** Matthew-Hope Eland (via Correct Course workflow)
**Mode:** Batch
**Trigger:** Story 3.4 was significantly misinterpreted during implementation.
**Decision taken (this session):** Replace Story 3.4 with the intended source-code treemap; deliver the full vision in one story.

---

## Section 1 — Issue Summary

**Problem statement.** Story 3.4 ("Interactive Tree Views for Project and Artifact Structure") was implemented as a **zero-JS native `<details>`/`<summary>` disclosure tree of the `_bmad-output/**/*.md` artifact file set** (page `structure.html`, status `review`). The intended feature was fundamentally different: a **treemap of the project's source code**, with rectangles **sized by lines of code**, **colorized by a selectable git dimension** (change frequency, relative creation date, relative last-modified date, commit count, average change size), plus **rich hover tooltips** and **zoom/drill**.

**How it was discovered.** Author review of the delivered `structure.html` surface against original intent.

**Root cause.** The epics.md acceptance criteria are written in literal disclosure-tree language — *"expand and collapse nodes by depth"*, *"focusable with announced state (expanded or collapsed)"* — which a treemap does not do (a treemap zooms, it does not expand/collapse `<details>`). FR14 asks for two distinct things — *"tree views **and** structural visualizations"* — and only the first, narrower half was built. The story context then reinforced the disclosure-tree reading and explicitly excluded a source-code walk. Nothing in the delivered code is defective; it correctly built the wrong feature.

**Evidence.**
- Delivered story: [3-4-interactive-tree-views-for-project-and-artifact-structure.md](implementation-artifacts/3-4-interactive-tree-views-for-project-and-artifact-structure.md) — Completion Notes describe a `<details>` tree over the artifact `*.md` set, *deliberately* no source walk, no LOC, no git colorization.
- Governing AC: [epics.md#Story 3.4 (lines 459-477)](epics.md) — expand/collapse + announced-state language.
- FR14: [epics.md:45](epics.md) — "tree views **and** structural visualizations".
- UX-DR19: [epics.md:102](epics.md) — "interactive tree-view experience (expand/collapse, focusable nodes, clear depth cues, link-out)".

---

## Section 2 — Impact Analysis

### Epic Impact
- **Epic 3 (Insight Surfaces and Tree-View Discovery):** goal unchanged ("understand project shape, gaps, and momentum quickly"). Story 3.4's *definition* changes from a disclosure tree to a treemap. No other Epic 3 story depends on 3.4's disclosure-tree output — 3.3 (coverage panel), 3.5 (motion), 3.6 (funnel), 3.8 (git hub) are independent.
- **No cross-epic ripple.** The treemap is a new consumer of the `--deep-git` data seam (Epic 3 already owns it via Story 3.2); it does not disturb Epics 1, 2, 4, 5, 6, 7.

### Story Impact
- **Story 3.4** — rewritten (ACs, title, dev context). The delivered disclosure-tree implementation is **reverted / repurposed** (see Section 4).
- **Delivered, now-superseded code** — `ProjectTree`, `Charts.ProjectStructureTree`, and the disclosure-specific tests are removed or replaced. The generic *integration seams* (page + nav gate + glyph) are **kept and repurposed** rather than thrown away.
- **No downstream stories block on this.**

### Artifact Conflicts
- **epics.md** — Story 3.4 ACs must be rewritten; **UX-DR19** must be amended (its literal expand/collapse tree language will no longer be met). **FR14** already covers the treemap under "structural visualizations" — optional wording tweak only.
- **PRD** ([prd-SpecScribe-2026-07-05/prd.md](prds/prd-SpecScribe-2026-07-05/prd.md)) — **no change needed.** The PRD has no FR14 tree requirement; the story's "PRD FR14" citation was loose. (FR14 was added at epic-creation time; back-syncing to the PRD is a separate, optional housekeeping task per the note at [epics.md:52](epics.md).)
- **UX design docs** ([ux-SpecScribe-2026-07-05/DESIGN.md, EXPERIENCE.md](ux-designs/ux-SpecScribe-2026-07-05)) — **no change needed.** They never reference the tree; UX-DR19 lives only in epics.md.
- **Spec kernel** ([spec-specscribe/SPEC.md, requirements-catalog.md](specs/spec-specscribe)) — **no change needed.** The kernel's FR-14 is a *different* requirement (feature configurability). The tree/treemap surface is not a kernel FR. Do **not** edit the spec kernel for this change.

### Technical Impact
- **New data inputs:** a **source-file walk** (the delivered story deliberately avoided this) and **per-file LOC**. Both are cheap and local (NFR1).
- **Git metrics reuse:** change frequency / commit count / avg change size / first+last commit date come from **extending the single `--deep-git` numstat pass** ([GitMetrics.ParseNumstatLog / TryComputeDeep](../../src/SpecScribe/GitMetrics.cs), which already yields per-file `Hotspots` change counts). Per the deep-git single-path discipline, the treemap becomes another *extender* of that one pass — **do not add a second `git log`**.
- **Rendering:** a squarified **SVG treemap** with a **non-lifecycle sequential color ramp** (NOT the `--status-*` tokens — structure is not a lifecycle stage; reuse the commit-heatmap ramp discipline) and a legend.
- **⚠ Architecture decision — zero-JS charting principle.** The intended features **zoom/drill** and **live colorize-dimension switching** realistically require JavaScript, which conflicts with the project's deliberate "charts are pure SVG + links, no JS" principle (only a small tooltip/copy script exists today). **This needs an explicit Architect ruling** (see Section 3). Tooltips should route through the body-level `js-tip`/`data-tip` node to avoid SVG/panel clipping.
- **Accessibility burden shifts.** The delivered `<details>` tree got keyboard focus + announced state *for free*. A treemap must earn NFR6/UX-DR16 compliance deliberately: focusable rectangles with descriptive aria-labels (mirroring sunburst UX-DR7), keyboard drill (Enter/Space/Escape), a **non-color text equivalent** of every metric, and reduced-motion for zoom (UX-DR18).

---

## Section 3 — Recommended Approach

**Chosen path: Direct Adjustment (replace-in-place).** Rewrite Story 3.4 to the treemap, revert the disclosure-tree visualization, and reuse the delivered integration scaffolding. This was the user's decision; it is also the lowest-waste replacement because the page/nav/gate/glyph seams are visualization-agnostic and match the intended surface exactly.

**Full vision in one story** (user decision): LOC sizing + all five colorize dimensions + rich tooltips + zoom, in a single Story 3.4 rewrite.

**One decision must be resolved before dev — escalate to Architect (Winston):**
> Does the treemap's **zoom/drill and colorize-dimension switching** warrant a scoped, progressively-enhancing JavaScript module, as a deliberate exception to the zero-JS charting principle? Recommended answer: **yes, scoped and degrading** — server-render the treemap sized-by-LOC with a default colorize dimension baked in (works with JS off), and layer zoom + dimension-switch + rich tooltips as progressive enhancement, with a text-equivalent metrics table as the accessible/no-JS fallback. This mirrors how the existing small tooltip/copy script was admitted as a bounded exception, and reuses the sunburst's drill + URL-hash-state conventions (UX-DR5/6/7).

**Effort:** Moderate–Large single story (new SVG treemap layout + git-metric extension + a11y text-equivalent + zoom/drill). **Risk:** medium — the a11y text-equivalent and the zero-JS exception are the two risk centers; both are de-risked by reusing the sunburst pattern and the heatmap ramp. **Timeline:** one story slot; no other story is blocked while it is in flight.

---

## Section 4 — Detailed Change Proposals

### 4A. epics.md — rewrite Story 3.4 (lines 459-477)

**OLD**
```
### Story 3.4: Interactive Tree Views for Project and Artifact Structure

As a project reviewer,
I want interactive tree views of directory and artifact structure,
So that I can inspect project organization and navigate to relevant content fast.

Acceptance Criteria:
1. Given a generated portal with multiple artifact families / When I open the tree-view
   surface / Then I can expand and collapse nodes by depth / And each node has clear visual
   hierarchy cues and labels.
2. Given I use keyboard and screen reader navigation / When I traverse the tree / Then tree
   items are focusable with announced state (expanded or collapsed) / And selecting a node
   can route to the related page or context target.
```

**NEW**
```
### Story 3.4: Source Code Treemap for Codebase Exploration

As a project reviewer exploring an unfamiliar codebase,
I want a treemap of the source tree sized by lines of code and colorable by git-derived
change signals,
So that I can see at a glance where the code mass and the churn live, and drill into any area.

Acceptance Criteria:
1. (Sizing & structure) Given a repository with source files / When I open the code-map
   surface / Then a treemap renders each source file as a rectangle whose area is proportional
   to its line count, nested within its directory / And the layout is deterministic, with
   directory labels and clear boundaries.
2. (Colorize dimensions) Given deep-git analysis is available / When I choose a colorize
   dimension / Then files are shaded by that dimension — change frequency (commit count),
   relative creation date, relative last-modified date, or average change size — on a
   non-lifecycle sequential scale with a legend / And when git data is unavailable the treemap
   still renders sized-by-LOC with a neutral fill and a clear notice (graceful degradation).
3. (Tooltips & zoom) Given I hover or focus a rectangle / Then a rich tooltip shows the file
   path, line count, and available git metrics / And I can zoom into a directory and back out
   via a breadcrumb, with drill state deep-linkable (mirroring the sunburst conventions).
4. (Accessibility & truthfulness) Given keyboard and screen-reader navigation / Then rectangles
   are focusable with descriptive labels announcing name and metric value / And color is never
   the sole signal (every metric is available as text) / And reduced-motion is respected,
   preserving the Story 1.4/1.5 conventions and NFR6.
```
**Rationale:** Aligns the story with author intent (codebase-exploration treemap) and the FR14 "structural visualizations" clause; makes the git-data dependency, graceful degradation, and a11y text-equivalent explicit.

### 4B. epics.md — amend UX-DR19 (line 102)

**OLD**
```
UX-DR19: Implement a readable, interactive tree-view experience for project and artifact
structure (expand/collapse, focusable nodes, clear depth cues, and link-out to relevant
pages/files).
```
**NEW**
```
UX-DR19: Implement a readable, interactive source-structure visualization — a treemap of the
code tree sized by lines of code and colorable by git-derived signals (change frequency,
creation/last-modified recency, average change size), with rich hover/focus tooltips, directory
drill/zoom with breadcrumb, focusable rectangles carrying descriptive labels, and a non-color
text equivalent of every metric.
```
**Rationale:** The delivered expand/collapse tree is being removed; UX-DR19 must describe the surface we are actually shipping so readiness checks and future stories reference the right experience.

### 4C. epics.md — FR14 (line 45) — OPTIONAL wording tweak

FR14 already reads *"project tree views **and** structural visualizations."* The treemap satisfies "structural visualizations." **Recommended:** leave FR14 as-is (still accurate) **or** optionally soften "tree views" → "structural/tree visualizations" to avoid implying a disclosure tree that no longer ships. Author to confirm during review.

### 4D. Story file — rewrite `3-4-...md`

Rewrite [3-4-interactive-tree-views-for-project-and-artifact-structure.md](implementation-artifacts/3-4-interactive-tree-views-for-project-and-artifact-structure.md) to the treemap definition above and reset Status `review → drafted` (or `backlog`). Recommend renaming the file to `3-4-source-code-treemap-for-codebase-exploration.md` for clarity. The new Dev Notes should carry the git-single-numstat-path, non-status-ramp, tooltip-node, and zero-JS-exception guidance from Section 2/3.

### 4E. Delivered code — revert / repurpose

From the delivered File List:

| File | Disposition |
| --- | --- |
| `src/SpecScribe/ProjectTree.cs` | **Replace** with the treemap model (`CodeMapNode` with per-node LOC + git metrics, size = Σ descendants). The trie-nesting / deterministic-ordering logic is reusable; the single-child-chain collapse and the artifact-`*.md` input are not. |
| `src/SpecScribe/Charts.cs` — `ProjectStructureTree` + `AppendTreeNode` | **Replace** with a squarified-treemap SVG renderer + colorize scale + legend. |
| `src/SpecScribe/Icons.cs` — `"Structure"` glyph | **Keep** (repurpose for the code-map nav item). |
| `src/SpecScribe/SiteNav.cs` — `StructureOutputPath`, `HasStructure`, `hasStructure` param, nav item + quick link | **Keep** the seam; consider relabeling nav/quick-link text "Structure" → "Code Map". |
| `src/SpecScribe/SiteGenerator.cs` — `WriteStructure`, gate, call site, page shell | **Keep** the page-write + `StructureAvailable` gate + try/catch→Empty scaffold; **repoint** the href-map/`_docs` input to a source-file walk + LOC + `--deep-git` metrics. |
| `src/SpecScribe/assets/specscribe.css` — structure-tree styles | **Replace** with treemap styles (rect fills, sequential ramp, legend, focus ring, tooltip). |
| `tests/.../ProjectTreeTests.cs` | **Replace** with treemap-model tests (sizing rollup, ordering, git-metric mapping, empty→Empty, never-throw). |
| `tests/.../ChartsTests.cs` (3 renderer tests) | **Replace** disclosure assertions with treemap SVG assertions (`<rect>`, legend, no-color-only text equivalent). |
| `tests/.../SiteGeneratorStructureTests.cs` | **Update** `<details>`-nesting assertions → treemap output + still-valid page-shell/nav/breadcrumb assertions. |
| `tests/.../SiteNavTests.cs` (2 nav tests) | **Keep** (nav gate is unchanged); relabel if nav text changes. |
| `tests/.../IconsTests.cs` (`"Structure"` case) | **Keep**. |
| `tests/.../StylesheetTests.cs` (CSS guard) | **Update** the guarded class name to a treemap class. |

**Rationale:** Preserve the visualization-agnostic integration (page + nav gate + glyph + degradation), replace only the model + renderer + styles + visualization-specific tests. Net-new: source walk, LOC, git-metric extension of the numstat pass, treemap layout, colorize ramp/legend, zoom/drill, a11y text-equivalent.

---

## Section 5 — Implementation Handoff

**Scope classification: Moderate** (single-story rewrite + revert + reimplement + two epics.md artifact amendments) **with one embedded Architect decision** (zero-JS charting exception).

**Routing:**
1. **Architect (Winston)** — rule on the scoped-JS exception for zoom + dimension-switch (Section 3). *Blocking for the renderer approach; do first.*
2. **Product Owner / author** — approve the epics.md Story 3.4 + UX-DR19 amendments (Section 4A/4B), confirm the optional FR14 tweak (4C), and the story-file rewrite/rename (4D).
3. **Developer (Amelia)** — execute the revert/repurpose + treemap implementation (Section 4E) once the story is rewritten and the JS ruling is in. Recommend running `create-story` (or a focused edit) to regenerate the 3.4 story context against the new ACs before `dev-story`.

**Success criteria:**
- `structure.html` (or `code-map.html`) renders a LOC-sized SVG treemap of the source tree.
- All five colorize dimensions selectable, on a non-`--status-*` sequential ramp with a legend; graceful sized-by-LOC fallback when `--deep-git` is off.
- Rich hover/focus tooltips via the body-level tip node; directory zoom/drill with breadcrumb + deep-linkable state.
- Focusable rectangles with descriptive labels; every metric available as text (color never sole signal); reduced-motion respected.
- Git metrics sourced by **extending the single numstat pass** — no second `git log`.
- Spec kernel untouched; full test suite green.
