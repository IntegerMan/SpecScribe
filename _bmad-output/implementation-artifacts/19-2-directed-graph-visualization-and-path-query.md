# Story 19.2: Directed Graph Visualization and Path Query

Status: ready-for-dev

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

- [ ] **Task 0 — Ingest 19.1 findings (AC: #1, #2)**
  - [ ] Read `19-1-work-graph-model-and-coverage-spike.md` Completion Notes end-to-end.
  - [ ] Copy the locked edge table + direction convention + NFR8 gate into this story's Dev Agent Record (or a short `_bmad-output/` note) so implementers do not re-litigate vocabulary.
  - [ ] Confirm default MVP vs any spike override (cycle finder vs path query; requirement nodes in/out; `cites` in MVP or deferred).

- [ ] **Task 1 — Work-graph projection model (AC: #2)**
  - [ ] Add a pure projection type (e.g. `WorkGraph` / `WorkGraphBuilder`) that emits nodes + directed edges from **existing** models: `FollowUpGeometry` / `FollowUpDeferredSlot`, `UnplannedWorkGeometry`, `SprintActionItem` + `EpicRetroMap`, epic/story structure, coverage maps only if 19.1 includes `covers`, citation maps (`_citerToFiles` / `BuildReferencedBy`) only if 19.1 includes `cites`.
  - [ ] Node identity + href must match 19.1's identity table (epic → `epics/epic-N.html`, story → story page, deferred → `follow-ups/{slug}.html`, etc.). Guarded hrefs: link only when target page exists; non-link chip otherwise (mirror Epic 7).
  - [ ] **Do not** re-parse deferred markdown / sprint YAML / code citations at the graph layer. **Do not** invent open-item totals — if a count is shown, read `ProjectCounts` / geometry fields already agreed with the ledger.
  - [ ] Structural containment (story∈epic) only as 19.1 classified it (first-class edge vs implied).

- [ ] **Task 2 — Cycle / path query (AC: #1)**
  - [ ] Implement the query 19.1 chose (default: directed-cycle finder on the epic-scoped edge set).
  - [ ] Define cycle precisely: simple directed cycle among named node types. Breadcrumbs / SPA Parent/Children are **not** edges.
  - [ ] Surface results as scannable HTML (list of node/edge chains) plus an optional highlight on the SVG — text results must work with JS off (PRD progressive-enhancement).
  - [ ] When no cycles/paths: honest empty message on the query panel **only if the graph surface itself is present**; never show an empty whole-page chrome for zero-graph projects (NFR8).

- [ ] **Task 3 — Visualization + page (AC: #1, #2)**
  - [ ] New pure-SVG builder in `Charts.cs` (e.g. `Charts.WorkGraph`) — **not** a reuse of `Charts.ReferenceGraph`'s hub-and-spoke model (that is code-neighbourhood, not provenance). Reuse a11y/CSS *idioms* (role/aria, caps, sr-only list, token colors).
  - [ ] New templater + `SiteGenerator` writer via `WriteOutput` so SPA capture works (`main#main-content`).
  - [ ] Register `SiteNav` path constant + Insights (or Follow-ups) child **gated on the same has-graph signal** used to write the page — mirror Code Map / Action Items gating.
  - [ ] BreadcrumbTrail for page hierarchy only — do not treat trail as provenance edges.
  - [ ] Bound node/edge draw counts (document constants); sr-only / query text may enumerate fuller sets when visual is capped — do not silently drop from the accessible equivalent.

- [ ] **Task 4 — NFR8 + parity + dogfood (AC: #1, #2)**
  - [ ] Zero mappable edges / no follow-up+attribution signal → omit nav entry and skip page write (no empty Insights child, no dead link).
  - [ ] HTML and SPA both serve the same body content for the new page(s).
  - [ ] Webview: **out of MVP** (dashboard/epics only today) unless 19.1 Completion Notes explicitly promote it.
  - [ ] CLI: notices only; no graph render.
  - [ ] Dogfood on this repo: at least one epic with deferred/action provenance shows navigable nodes; synthesize a fixture cycle if live data has none (19.1 Task 3 may already note this).

- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] Unit tests for graph builder: edge projection from fixtures, cycle detection, empty → empty model.
  - [ ] Chart/SVG tests: deterministic layout, caps, direction markers, empty → `""`.
  - [ ] SiteNav / SiteGenerator tests: nav absent when no graph; page written when present; no broken local links on node hrefs.
  - [ ] Assert graph layer does not introduce a second open-item count (ledger fields unchanged / not re-tallied in builder).
  - [ ] Prefer focused unit tests over golden fingerprint churn; update fingerprints only when intentional.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-18 — Story 19.2 drafted (create-story). Depends on 19.1 coverage map; default MVP = epic-scoped directed SVG + cycle/path query; HTML+SPA; NFR8 omit; ProjectCounts/no new schema.
