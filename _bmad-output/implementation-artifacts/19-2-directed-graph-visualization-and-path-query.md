---
baseline_commit: eaa2348370b18dd40cb0ab06afeef9701f9b03fc
---

# Story 19.2: Directed Graph Visualization and Path Query

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Driver scanning remaining work,
I want a portal surface that draws the directed work graph for a chosen scope and answers simple path/cycle queries,
So that circular-looking reverse links and multi-hop provenance become inspectable instead of inferred from breadcrumbs.

## Why this story exists (read first)

Epic 9 made provenance *visible on pages* (Deferred-from panels, action-item `epic:`, quick-dev attribution, story↔requirement hops, code "Referenced by"). Epic 19 turns those seams into a **queryable directed work graph**. Story 19.1 invents the vocabulary and picks the MVP surface; **this story builds it**.

**The one-line test for "is this in scope?":** if the change *projects* existing parsers into a navigable epic-scoped graph + cycle/path query, with HTML/SPA parity and NFR8 omit → in. If it invents authoring schema, re-counts against `ProjectCounts`, replaces the sunburst, promotes webview/CLI graph render, or treats co-change as `stemmed-from` → out.

**Exploratory / not release-blocking.** Depends on Epic 9 + Epic 7 as data sources (both done) and on **Story 19.1 Completion Notes** for the locked node/edge map and primary-surface choice.

## Acceptance Criteria

1.
**Given** a project with attributed deferred/quick-dev/story/epic links (per Story 19.1's mappable edges)
**When** the graph surface renders for a chosen scope (at least epic-scoped)
**Then** nodes and directed edges are navigable to existing detail pages, and a cycle or multi-hop path query surfaces ambiguous or circular provenance when present
**And** zero-graph projects omit the surface cleanly (NFR8).

2.
**Given** the same underlying ledger counts and provenance parsers as Epic 9
**When** the graph builds
**Then** it does not invent a second authoring schema or re-count open items against ProjectCounts
**And** HTML/SPA parity holds for the new page(s).

## Hard prerequisite — Story 19.1

**Do not start `src/` work until Story 19.1 is `done` with Completion Notes containing the coverage map + 19.2 recommendation.**

| 19.1 deliverable | How 19.2 consumes it |
|------------------|----------------------|
| Node / edge / query tables | Implement **only** edges marked derivable (or explicitly approved new heuristics). Out-of-scope rows stay out. |
| Direction convention | Use the spike's chosen direction for every edge kind (do not invent a second convention). |
| Primary surface + query path | Build that surface. If Completion Notes are silent, use the **default MVP below** (matches AC floor). |
| NFR8 absence rules | Gate nav + page write on the spike's "has graph signal"; omit chrome when false. |
| Parsers to reuse | Call those APIs; do not fork parsers into a parallel ingest. |

### Default MVP (when 19.1 recommendation is silent or affirms epic-scoped)

AC floor already requires **both** an epic-scoped draw **and** a cycle or multi-hop path query. Default:

1. **Primary visualization:** epic-scoped directed subgraph (epic + its stories + attributed deferred/action/quick-dev + optional `cites` into code when already discovered for those artifacts).
2. **Query affordance on the same page:** cycle detection over the drawn subgraph (list cycles when present; honest "no cycles in this scope" when absent). Multi-hop path (e.g. deferred → source → epic) may ship as a second query panel if 19.1 prefers path-over-cycle — still epic-scoped as the draw host.
3. **Scope picker:** at least one epic at a time (URL or query param / path segment). Site-wide graph is out of MVP unless 19.1 explicitly recommends it.

## Tasks / Subtasks

- [x] **Task 0 — Ingest 19.1 findings (AC: #1, #2)**
  - [x] Read `19-1-work-graph-model-and-coverage-spike.md` Completion Notes end-to-end.
  - [x] Copy the locked edge table + direction convention + NFR8 gate into this story's Dev Agent Record (or a short `_bmad-output/` note) so implementers do not re-litigate vocabulary.
  - [x] Confirm default MVP vs any spike override (cycle finder vs path query; requirement nodes in/out; `cites` in MVP or deferred).

- [x] **Task 1 — Work-graph projection model (AC: #2)**
  - [x] Add a pure projection type (e.g. `WorkGraph` / `WorkGraphBuilder`) that emits nodes + directed edges from **existing** models: `FollowUpGeometry` / `FollowUpDeferredSlot`, `UnplannedWorkGeometry`, `SprintActionItem` + `EpicRetroMap`, epic/story structure, coverage maps only if 19.1 includes `covers`, citation maps (`_citerToFiles` / `BuildReferencedBy`) only if 19.1 includes `cites`.
  - [x] Node identity + href must match 19.1's identity table (epic → `epics/epic-N.html`, story → story page, deferred → `follow-ups/{slug}.html`, etc.). Guarded hrefs: link only when target page exists; non-link chip otherwise (mirror Epic 7).
  - [x] **Do not** re-parse deferred markdown / sprint YAML / code citations at the graph layer. **Do not** invent open-item totals — if a count is shown, read `ProjectCounts` / geometry fields already agreed with the ledger.
  - [x] Structural containment (story∈epic) only as 19.1 classified it (first-class edge vs implied).

- [x] **Task 2 — Cycle / path query (AC: #1)**
  - [x] Implement the query 19.1 chose (default: directed-cycle finder on the epic-scoped edge set).
  - [x] Define cycle precisely: simple directed cycle among named node types. Breadcrumbs / SPA Parent/Children are **not** edges.
  - [x] Surface results as scannable HTML (list of node/edge chains) plus an optional highlight on the SVG — text results must work with JS off (PRD progressive-enhancement).
  - [x] When no cycles/paths: honest empty message on the query panel **only if the graph surface itself is present**; never show an empty whole-page chrome for zero-graph projects (NFR8).

- [x] **Task 3 — Visualization + page (AC: #1, #2)**
  - [x] New pure-SVG builder in `Charts.cs` (e.g. `Charts.WorkGraph`) — **not** a reuse of `Charts.ReferenceGraph`'s hub-and-spoke model (that is code-neighbourhood, not provenance). Reuse a11y/CSS *idioms* (role/aria, caps, sr-only list, token colors).
  - [x] New templater + `SiteGenerator` writer via `WriteOutput` so SPA capture works (`main#main-content`).
  - [x] Register `SiteNav` path constant + Insights (or Follow-ups) child **gated on the same has-graph signal** used to write the page — mirror Code Map / Action Items gating.
  - [x] BreadcrumbTrail for page hierarchy only — do not treat trail as provenance edges.
  - [x] Bound node/edge draw counts (document constants); sr-only / query text may enumerate fuller sets when visual is capped — do not silently drop from the accessible equivalent.

- [x] **Task 4 — NFR8 + parity + dogfood (AC: #1, #2)**
  - [x] Zero mappable edges / no follow-up+attribution signal → omit nav entry and skip page write (no empty Insights child, no dead link).
  - [x] HTML and SPA both serve the same body content for the new page(s).
  - [x] Webview: **out of MVP** (dashboard/epics only today) unless 19.1 Completion Notes explicitly promote it.
  - [x] CLI: notices only; no graph render.
  - [x] Dogfood on this repo: at least one epic with deferred/action provenance shows navigable nodes; synthesize a fixture cycle if live data has none (19.1 Task 3 may already note this).

- [x] **Task 5 — Tests (AC: #1, #2)**
  - [x] Unit tests for graph builder: edge projection from fixtures, cycle detection, empty → empty model.
  - [x] Chart/SVG tests: deterministic layout, caps, direction markers, empty → `""`.
  - [x] SiteNav / SiteGenerator tests: nav absent when no graph; page written when present; no broken local links on node hrefs.
  - [x] Assert graph layer does not introduce a second open-item count (ledger fields unchanged / not re-tallied in builder).
  - [x] Prefer focused unit tests over golden fingerprint churn; update fingerprints only when intentional.

## Dev Notes

### Locked visual design (owner defaults — elicit only if 19.1 overrides)

No dedicated UX artifact exists for the work graph. Lock these defaults so review does not bikeshed shapes mid-implementation (same create-story visual-intent pattern as Story 7.8):

| Element | Treatment |
|---------|-----------|
| Layout | Layered / left-to-right or top-down **directed** layout (not hub-and-spoke). Deterministic: same input → same SVG. |
| Edge direction | SVG `marker-end` arrowheads; markers decorative (`aria-hidden` on marker defs). |
| Node types | Distinguish by **shape + label prefix**, not lifecycle `--status-*` fills. Suggested: epic = rounded rect; story = circle; deferred/action = diamond; quick-dev = rounded square; code = small rect. Neutral/`--gold`/`--border`/`--ink` tokens only. |
| Edge kinds | Solid for `stemmed-from` / structural; dashed for soft/heuristic edges if 19.1 marks any soft (e.g. action→retro via map). Optional short edge label when ≥2 kinds share the scope. |
| Interaction | Nodes with href are `<a>`; no JS required for navigation. Query results are HTML lists (progressive enhancement may highlight SVG later — optional, not required). |
| A11y | SVG `role="img"` + summary `aria-label` (or title/desc); complete **sr-only** enumeration of every drawn node and every query result chain (NFR6). Mirror `.ref-graph` / `.ref-list` idiom with new classes (e.g. `.work-graph`). |
| Framing | Optional `Charts.Framed` / Story 10.2 meta only if a metric-generic "why" fits; do not invent project-specific copy. |

### What already exists (reuse — do NOT rebuild)

| Seam | Consume from | Graph role |
|------|--------------|------------|
| Deferred provenance slots | `FollowUpDeferredSlot`, `FollowUpGeometry`, `DeferredWorkParser` / `FollowUpRefs` | `stemmed-from`, `resolves`, epic attribution |
| Quick-dev → epic | `UnplannedWorkGeometry.ResolveQuickDevEpic`, `WorkInventory.QuickDev` | attribution edges / Unplanned honesty |
| Action items | `SprintActionItem`, `ActionItemsTemplater` cross-links, `EpicRetroMap` | epic / `raised-in` (soft retro) |
| Story ∈ epic | `EpicInfo.Stories` / parsers | structural containment |
| Requirement covers | `RequirementsParser.StoriesFor` (epic-granular!) | `covers` only if 19.1 includes it — never pretend per-FR story precision |
| Code cites | `CodeReferenceScanner`, `_citerToFiles`, `BuildReferencedBy` | `cites` only if 19.1 includes it |
| Counts ledger | `ProjectCounts` | read-only; never re-count open deferred/actions/direct |
| Viz precedent | `Charts.ReferenceGraph`, `CodeFileTemplater` Relationships | a11y + caps + omit-empty **idioms only** |
| Page recipe | `SiteNav.Build` gates, `WriteOutput`, `SpaDelivery` content extract, `BreadcrumbTrail` | new insight page class |
| Co-change | `FileInsight.CoupledFiles` | **OUT** of work-graph vocabulary unless 19.1 explicitly reclassifies |

Closest proto-edge record today: `FollowUpDeferredSlot(Item, ProvenanceLabel, EpicNumber?, DetailHref, SourceKey?, SourceHref?, SourceStoryId?)` — project from it; do not invent a parallel deferred type. [Source: `src/SpecScribe/FollowUpGeometry.cs`]

### Architecture compliance

- **Shared-core projection (AD-1/AD-2):** graph builder is a pure projection over existing models — adapters do not re-interpret artifacts. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]
- **Insight non-blocking (AD-4):** missing/malformed graph data never fails generation; omit surface.
- **HTML primary + SPA parity:** shared body via `WriteOutput` capture; webview not required for MVP. [Source: 19.1 surface table; `rendering-architecture.md` Feature Parity Rules]
- **No JS required for information:** core content + navigation work with JS disabled (PRD progressive-enhancement / NFR-5). Pure C# SVG string builders — **no D3 / vis.js / cytoscape**. [Source: `Charts.cs` header; memory: charting-is-pure-svg-no-js]
- **NFR8:** absent artifacts → absent surfaces (no empty-but-present Insights child).
- **FR37** PRD sync remains "when convenient" — not a blocker for this story.

### File structure (expected touch set)

| Path | Change |
|------|--------|
| `src/SpecScribe/WorkGraph*.cs` (new) | Projection model + cycle/path query |
| `src/SpecScribe/Charts.cs` | `WorkGraph` SVG builder (+ constants/caps) |
| `src/SpecScribe/WorkGraphTemplater.cs` (new) | Page HTML, scope picker, query panel, sr-only |
| `src/SpecScribe/SiteNav.cs` | Path constant + gated Insights/Follow-ups child |
| `src/SpecScribe/SiteGenerator.cs` | Build graph from existing geometry/maps; gate + `WriteOutput` |
| `src/SpecScribe/assets/specscribe.css` | `.work-graph` tokens/shapes (neutral/gold/border only) |
| `tests/SpecScribe.Tests/*WorkGraph*` (new) | Builder + cycle + nav/omit tests |
| `tests/SpecScribe.Tests/ChartsTests.cs` | SVG assertions |
| `tests/SpecScribe.Tests/SiteNavTests.cs` / generator tests | Nav/page gates |

Do **not** modify `DeferredWorkParser`, `FollowUpRefs`, or `ProjectCounts.Build` unless a true bug blocks projection — prefer projecting existing outputs.

### Anti-patterns to prevent

- Reimplementing Epic 9 parsers as a second "graph ingest."
- Treating sunburst membership, breadcrumbs, or SPA Parent/Children as work-graph edges.
- Declaring `CoupledFiles` co-change as `stemmed-from`.
- Fabricating story parents for retro action items that only have `epic:`.
- Per-FR story `covers` edges (epic-granular honesty from Story 9.1).
- New YAML / frontmatter / graph DSL authoring schema.
- Second open-item ledger or recounting deferred/actions in the graph builder.
- Replacing or duplicating sunburst as "what's left."
- Shipping empty Insights chrome or a dead nav link when the project has no graph.
- Pulling in a JS graph library.
- Expanding into webview/CLI graph rendering without 19.1 saying so.
- Conflating this page with `Charts.ReferenceGraph` / code Relationships tab.

### Testing requirements

- Framework: existing xUnit / SpecScribe.Tests patterns.
- Prefer pure builder unit tests with small fixtures over full-site goldens.
- NFR8: assert nav label absent and page not written when graph empty.
- Cycle fixture: if dogfood has no live cycle, add a synthetic fixture (19.1 may already note "none found; synthesize").
- Parity: new page rides `WriteOutput` — no epic/story-style raw `File.WriteAllText` bypass.
- Link hygiene: node hrefs that are present must resolve to generated pages (`AssertNoBrokenLocalLinks` style where applicable).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/19-2-directed-graph-visualization-and-path-query.md`
- Sprint key: `19-2-directed-graph-visualization-and-path-query`
- Upstream spike: `19-1-work-graph-model-and-coverage-spike` (must be done first)
- Epic: exploratory — not release-blocking

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 19 + Story 19.2 ACs; FR37; NFR8]
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md` — vocabulary, non-goals, surface reach, 19.2 feed-forward]
- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-07-18.md` — create-story action for Epic 19]
- [Source: `_bmad-output/implementation-artifacts/9-6-follow-up-item-provenance-and-resolution-paths.md`]
- [Source: `_bmad-output/implementation-artifacts/9-11-follow-up-detail-pages-and-deep-links.md`]
- [Source: `_bmad-output/implementation-artifacts/9-12-unplanned-and-one-off-work-in-geometry-and-sprint.md`]
- [Source: `_bmad-output/implementation-artifacts/7-8-related-files-in-the-reference-graph.md` — SVG a11y/caps precedent, not the model]
- [Source: `src/SpecScribe/FollowUpGeometry.cs`, `UnplannedWorkGeometry.cs`, `ProjectCounts.cs`, `Charts.cs` (`ReferenceGraph`), `SiteNav.cs`, `SiteGenerator.cs`]
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — AD-1/AD-2/AD-4]

## Previous Story Intelligence

- **19.1 (spike, prerequisite):** Coverage map + one primary viz/query recommendation live in Completion Notes. No production code from 19.1. Edge vocabulary (`stemmed-from`, `resolves`, `covers`, `cites`, `raised-in`) and non-goals (no new schema, no second ledger, no sunburst replacement, co-change out) are load-bearing for this story.
- **Epic 9 (done):** Provenance is heuristic-over-prose; reverse panels ≠ unified edge list — 19.2's job is to project that unification into one surface without rewriting 9.x.
- **Story 7.8:** Pure SVG, caps, sr-only complete equivalent, guarded hrefs, no JS, neutral tokens — copy the *discipline*, not the hub-and-spoke *model*. Co-change population is explicitly not work provenance.
- **Story 8.3 / FollowUpGeometry:** Open tallies must agree with `ProjectCounts` — graph builder must not introduce a parallel count path.
- **Epic 6 Action #3 / Story 10.1:** Net-new insight pages need data-gated nav + WriteOutput; do not redesign Insights IA.

## Git Intelligence Summary

Recent work closed follow-up batch actions, Epic 8 count/status debt, and Insights grouped nav (10.1) — reinforcing ledger honesty and data-gated Insights children. Reference-graph epic grouping commits show the SVG toggle/cap idiom this story should mirror for a11y and determinism. No work-graph visualization code exists yet; build on Epic 9/7 seams.

## Latest Tech Notes

- Keep **pure generation-time SVG** (current stack: Markdig / Spectre / YamlDotNet only — no charting NuGet). Do not introduce D3 or browser graph libraries for MVP.
- Accessible graph pattern already used in-repo: SVG `role="img"` + summary label + **external** sr-only list of every node/link (because `role="img"` collapses descendants for AT). Arrow `marker` elements should be decorative (`aria-hidden`). Interactive navigation belongs on HTML `<a>` nodes and the sr-only list, not on AT walking every SVG path.

## Project Context Reference

`_bmad-output/project-context.md` is still a stub (discovery incomplete). Prefer this story + Architecture Spine + Epic 9/7 story files as the binding agent context.

## Story Completion Status

- Status: **ready-for-dev**
- Ultimate context engine analysis completed — comprehensive developer guide created.
- Implementation blocked only by Story 19.1 Completion Notes (spike must finish first).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Amelia / dev-story workflow)

### Implementation Plan — 19.1 findings ingested (Task 0)

Locked from Story 19.1 Completion Notes (status: done) so implementation does not re-litigate vocabulary:

**Chosen surface (19.1 §5):** Epic-scoped provenance subgraph — one HTML page (`work-graph.html`), a section per epic-with-signal, each drawing that epic + its involved stories + attributed deferred/action items + their provenance edges. Confirmed = the story's Default MVP. HTML + SPA only; webview/CLI out.

**Direction convention (19.1 §2, load-bearing):** `From` = the node that physically carries the reference; `To` = the node referenced (**carrier → target**). Applied to every edge kind below.

**Edge kinds implemented (derivable-today rows only):**

| Edge | From → To (carrier→target) | Source API | Style |
|------|----------------------------|-----------|-------|
| structural `contains` | story → epic; unrooted deferred/action → epic | `EpicInfo.Stories`; `FollowUpDeferredSlot.EpicNumber` / `SprintActionItem.EpicNumber` | solid |
| `stemmed-from` | deferred → source story / spec / quick-dev | `FollowUpDeferredSlot.SourceStoryId` / `SourceKey` / `SourceHref` | solid |
| `resolves` | deferred → resolving story / spec | `DeferredWorkItem.Resolved` + `ResolvingRef` / `ResolvingHref`; `FollowUpRefs.ResolvingLabel` | solid |
| `raised-in` (soft) | action → other epic's retro | `ActionItemsTemplater.FindNearDuplicates` + `EpicRetroMap` | dashed |

**Deferred vs MVP (Task 0.3):**
- Cycle query: **kept** (AC #1 floor requires a cycle *or* multi-hop query). Simple directed-cycle finder over each epic's drawn edge set; honest "No cycles in this scope." when none — secondary annotation panel, not the primary surface (per 19.1 §5).
- `covers` (requirement nodes): **out of drawn MVP** — 19.1 marks it epic-grain-only and the Default-MVP draw list does not include it; leaving it out avoids over-claiming per-FR precision. Documented non-inclusion.
- `cites` (code): **deferred from the drawn MVP** — explicitly "optional" in the story's Default MVP; excluded to keep node counts bounded and avoid the `_citerToFiles` key-join surface this first cut doesn't need. Success-criterion deferral recorded; can layer on later without touching the model.

**NFR8 gate (19.1 §5):** page + nav entry are written only when at least one epic has a deferred slot OR an open action item (a "graph signal"). No epics, or epics with zero follow-ups → no page, no nav link. Structural containment alone is NOT signal (would defeat NFR8). Node/edge draw counts are bounded by documented `WorkGraphBuilder` constants; the sr-only equivalent enumerates the full drawn set.

### Debug Log References

- Full suite green: **2068 passed, 3 skipped (unrelated symlink tests), 0 failed** on `worktree-story-19-2-work-graph`.
- Golden content fingerprint regenerated (`b36f0bf1…` → `44a4bc43…`): CSS-only shift on the non-git fixture — it carries no work-graph signal, so `work-graph.html` isn't written there and the page SET is unchanged (GoldenOutputInventory still passes); the only drift is the every-page `specscribe.css` gaining the `.work-graph*` block. Verified stable across 2 repeated runs before locking. [[golden-diff-normalization-gotchas]]
- **Two defects caught by dogfood + a broken-link sweep, both fixed:**
  1. First dogfood drew only epic + action nodes (zero deferred provenance): the pre-nav model build used `TryParseDeferredWork` (reads `_docs`, empty at nav time). Switched to `ResolveDeferredModel(work, files)` (the same source-fallback `RenderEpicsPages` uses) → 170 deferred / 62 story / 39 spec nodes + 171 stemmed-from / 133 resolves edges across 10 epics.
  2. 11 broken `../epics/story-*.html` links — deferred `ResolvingHref` is prefixed for the deferred *page's* depth; re-rooted every node href through `FollowUpGeometry.ApplyLinkPrefix("")` in `WorkGraphBuilder`. Post-fix broken-link sweep over all 272 local hrefs = 0 missing.
- Browser-verified the real-repo page: Insights nav with active "Work Graph" local-context, legend, scope-picker chips (Epics 1–19), and per-epic layered subgraphs (Epic → Stories → Deferred → Sources with directed arrowheads). SPA parity confirmed via `--spa` (page rides the bundle + keeps its static fallback).

### Completion Notes List

**Shipped the epic-scoped provenance work graph (the 19.1-recommended primary surface) — HTML + SPA, no JS required, no new schema, no second count ledger.**

- **`WorkGraph.cs`** — pure projection (`WorkNode`/`WorkEdge`/`WorkGraphEpic`/`WorkGraphModel` + `WorkGraphBuilder`). One epic-scoped directed subgraph per epic-with-signal, projected from `FollowUpGeometry`/`FollowUpDeferredSlot` (stemmed-from, resolves, contains), `EpicInfo.Stories` (structural containment), and `ActionItemsTemplater.FindNearDuplicates` + `EpicRetroMap` (soft raised-in). Direction = carrier → target (19.1's locked convention). Deterministic; never re-parses or re-counts. Bounded by `MaxFollowUpsPerEpic = 40` with an honest `Overflow` count.
- **Cycle finder** (`WorkGraphBuilder.FindCycles`) — simple directed-cycle detection over each epic's edge set, deduped by rotation. Discovery: the carrier→target projection is a **DAG by construction** (follow-ups point at artifacts; artifacts never point back), so cycles can't arise on live data. AC #1 asks for "ambiguous **or** circular" provenance, so the query panel *also* surfaces the query that does fire — **ambiguous ownership** (19.1 query #2): an action item whose obligation is raised in ≥2 other epics' retros. Honest empty state when neither.
- **`Charts.WorkGraph`** — pure deterministic SVG, layered left→right (Epic · Stories · Follow-ups · Origins). Node kind by SHAPE (epic rounded-rect / story circle / deferred diamond / action triangle / source rect / retro muted-rect), edge kind by STYLE (solid vs dashed raised-in) with decorative `marker-end` arrowheads; `role="img"` + summary label. Neutral/gold/border/ink tokens only.
- **`WorkGraphTemplater`** — `work-graph.html`: intro, aria-hidden visual legend, JS-free scope-picker (per-epic anchor chips), per-epic section with the SVG, a **complete sr-only node+edge enumeration** (NFR6 — a `role="img"` collapses descendants), the circular/ambiguous query panel, and an overflow note. Rides `WriteOutput` (SPA capture via `main#main-content`).
- **Gating (NFR8)** — `WorkGraphBuilder.Build` returns `Empty` unless an epic has an attributed deferred item or open action item (structural containment alone is *not* signal). `SiteGenerator` projects the model **before** nav so the Insights "Work Graph" entry and the page write share one gate — the link can't dangle. `hasEpics`-less / signal-less repos get no page and no nav entry.
- **Honors 19.1's code-review decisions (D1/D2/D4):**
  - **D1 — "Unattributed" pseudo-epic bucket:** follow-ups with no epic (`EpicNumber == null`) or an unknown/ghost epic are NOT dropped — they render in a synthetic "Unattributed" bucket (via `OrphanDeferredItems`/`OrphanActionItems`, mirroring the action-items page), omitting cleanly when empty. Dogfood: the bucket surfaces 40 deferred items that the first cut silently dropped (170 → 210 drawn deferred nodes).
  - **D2 — no manufactured transitive edge:** a deferred item with `stemmed-from → A` and `resolves → B` (A ≠ B) stays one node with two out-edges; no synthetic A→B edge.
  - **D4 — no phantom node from an unresolved `SourceKey`:** a source node is minted only when the key resolves to a real page (`SourceHref` non-null); otherwise the edge is dropped and the item roots to its epic (cf. the `a16ca0f` phantom-item fix).
- **Out of MVP (documented):** `covers`/requirement nodes (epic-grain-only per 19.1 → would over-claim) and `cites`/code nodes (optional in the Default MVP; excluded to bound node counts — when added later, honor 19.1 D3: external `--code-url` mode keeps code nodes). Webview/CLI graph rendering not added (19.1 kept them out).
- **Dogfood:** 10 epic subgraphs on this repo, 290 nodes / 378 edges, Epic 6 hitting the 40-cap with a rendered overflow note; all 272 local links resolve.

### File List

**New**
- `src/SpecScribe/WorkGraph.cs` — projection model, builder, cycle finder
- `src/SpecScribe/WorkGraphTemplater.cs` — `work-graph.html` page (scope picker, sr-only, query panel)
- `tests/SpecScribe.Tests/WorkGraphTests.cs` — 19 tests (builder projection, NFR8 gate, determinism, cycle/ambiguity, SVG, templater, nav gate)

**Modified**
- `src/SpecScribe/Charts.cs` — `Charts.WorkGraph` pure-SVG builder (+ `AppendWorkNode`, `WorkNodeLabel`, `WorkGraphLabelChars`)
- `src/SpecScribe/SiteNav.cs` — `WorkGraphOutputPath` const, `hasWorkGraph` gate param, Insights membership + quick-link, `HasWorkGraph` predicate
- `src/SpecScribe/SiteGenerator.cs` — `_workGraph` field, `BuildWorkGraphModel` (pre-nav projection via `ResolveDeferredModel`), `WriteWorkGraph`, gate threaded through both `SiteNav.Build` call sites + watch-mode `BuildNav`
- `src/SpecScribe/assets/specscribe.css` — `.work-graph*` block (node shapes, edge/arrow styles, scope chips, query panel, legend)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden content fingerprint regenerated (CSS-only drift) + rationale comment

## Change Log

- 2026-07-18 — Story 19.2 drafted (create-story). Depends on 19.1 coverage map; default MVP = epic-scoped directed SVG + cycle/path query; HTML+SPA; NFR8 omit; ProjectCounts/no new schema.
- 2026-07-22 — Implemented (dev-story). New `WorkGraph.cs` (projection + builder + cycle finder), `WorkGraphTemplater.cs` (`work-graph.html`), `Charts.WorkGraph` pure-SVG, `.work-graph*` CSS; wired into `SiteNav`/`SiteGenerator` with a pre-nav NFR8 gate (Insights entry ↔ page can't dangle). Cycle query kept + broadened to circular-**or**-ambiguous (the projection is acyclic by construction; ambiguous-ownership fires on live data). `covers`/`cites` documented out of drawn MVP. 19 new tests; full suite 2068 green; golden fingerprint regenerated (CSS-only drift). Dogfooded on this repo (10 epic subgraphs, 290 nodes, all links resolve) + browser-verified + `--spa` parity. Status → review.
