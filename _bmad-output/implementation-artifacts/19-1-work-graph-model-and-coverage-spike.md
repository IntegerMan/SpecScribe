# Story 19.1: Work-Graph Model and Coverage Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer who traces debt across reviews and stories,
I want the portal's entity types and directed edges inventoried and scoped before any visualization ships,
So that the graph has a defined node/edge vocabulary, cycle semantics, and non-goals rather than an ad-hoc diagram.

## Why this story exists (read first)

Seated 2026-07-17 / actioned by the Epic 9 retrospective (2026-07-18): Epic 9 made provenance *visible on pages* (deferred `source_spec` / Deferred-from groups, action-item `epic:`, quick-dev attribution, story↔requirement hops, code-page "Referenced by"), but multi-hop chains and circular-looking reverse links still live only as breadcrumbs and per-page reverse panels. Epic 19 turns those seams into a **queryable directed work graph**. This story is the **spike** — inventory first, build in 19.2.

**The one-line test for "is this in scope?":** if the change *traces* existing parsers/geometry/linkifiers and *writes* a coverage map + 19.2 recommendation → in. If it *builds* a graph page, SVG, SPA route, or new authoring schema → out; that is Story 19.2.

**Exploratory / not release-blocking.** Epic 19 depends on Epic 9 (follow-up provenance) and Epic 7 (code citations) as **data sources** — it does not block either, and both are already `done`.

## Acceptance Criteria

1.
**Given** existing provenance seams (deferred `source_spec` / Deferred-from headings, action-item `epic:`, quick-dev epic attribution, story↔requirement links, code citations)
**When** the spike inventories them
**Then** a written coverage map lists node types (at least: epic, story, quick-dev, deferred item, action item, retro, code file) and directed edge kinds (stemmed-from, resolves, covers, cites, raised-in), marks each as already derivable vs requiring new heuristics, and names cycles/ambiguous reverse-links as first-class queries
**And** deliberately out-of-scope edges (e.g. inventing story parents for retro actions) are listed with rationale.

2.
**Given** the spike's recommended first surface
**When** the spike documents findings
**Then** it proposes one primary visualization + query path for Story 19.2 (e.g. epic-scoped subgraph, cycle finder, or "path from deferred → epic") with success criteria and NFR8 absence rules when a project has no follow-up/code graph
**And** no new authoring schema is required for the MVP path.

## Context & Scope

### What already exists (reuse — do NOT rebuild)

These are the seams the coverage map must inventory by **tracing real code**, not by re-deriving from epics.md prose alone:

| Seam | Primary types / files | Derivable today |
|------|----------------------|-----------------|
| **Deferred provenance** | `DeferredWorkParser`, `DeferredWorkGroup` (`ProvenanceLabel`, `SourceStoryId`, `SourceKey`), `FollowUpRefs.SourceSpecFileFromText` / `StoryIdFromKey` | Deferred → story/spec (`stemmed-from`); deferred → resolving story (`resolves` via `RESOLVED in` / resolving href) |
| **Deferred geometry** | `FollowUpGeometry`, `FollowUpDeferredSlot` (`EpicNumber`, `SourceStoryId`, `SourceKey`, `DetailHref`) | Deferred → epic (resolved or orphan); quick-dev-sourced deferred inherit epic via `EnrichQuickDevDeferredEpics` |
| **Action items** | `SprintActionItem` (`EpicNumber`, `Action`, `Status`), `ActionItemsTemplater`, `EpicRetroMap` | Action item → epic; action item → retro page ("From Epic N retrospective"); cross-retro near-dupes → `raised-in` ("also raised in Epic N") |
| **Quick-dev / one-shot** | `WorkInventory.QuickDev` (`route: one-shot`), `UnplannedWorkGeometry.ResolveQuickDevEpic` | Quick-dev → epic (heuristic) or Unplanned; done parents resurfaced when open deferred still names them |
| **Requirement ↔ epic/story** | `RequirementsParser` coverage maps, `StoriesFor`, `RequirementLinkifier`, `StoryEpicLinkifier` | Requirement `covers` / is-covered-by epic (epic-granularity honesty caveat from 9.1); story↔epic structural containment |
| **Code citations** | `CodeReferenceScanner`, `CodeReferenceLinkifier`, `_codePages`, `_codeReverseMap`, `_citerToFiles`, `BuildReferencedBy` | Artifact `cites` → code file; code file ← citing artifacts (reverse panel) |
| **Deferred reverse panels** | `FollowUpGeometry.DeferredForSource`, `FollowUpRow.RenderDeferredFromArtifactPanel`, `StoryView.DeferredFromThis` / quick-dev chrome | Story/quick-dev ← deferred that name them (`SourceKey`); empty → omit panel |
| **Code co-change (related)** | `FileInsight.CoupledFiles`, `Charts.ReferenceGraph` (Story 7.8) | Code ↔ code co-change — **not** work-graph provenance; decide in/out of Epic 19 vocabulary explicitly |
| **Retros** | `_epicRetroMap`, retro pages under retros/ | Epic ↔ retro; action items claim "from Epic N retrospective" via map — **not** a per-item retro id |
| **Counts ledger** | `ProjectCounts`, Story 8.3 | Single source of open/deferred/direct counts — graph must not recount against this |
| **Nav/drill (not work graph)** | `BreadcrumbTrail`, SPA `Manifest.Parent/Children` | Page hierarchy only — do not treat as provenance edges |

### Node types the coverage map MUST name (AC #1 minimum)

| Node type | Canonical identity today | Detail page / href pattern |
|-----------|--------------------------|----------------------------|
| epic | `EpicInfo.Number` | `epics/epic-N.html` |
| story | `StoryInfo.Id` (`N.M`) | `epics/story-N-M.html` (or `ArtifactOutputPath`) |
| quick-dev | `spec-*.md` stem / `QuickDevEntry.OutputPath` | generated doc page |
| deferred item | slug from `FollowUpSlug` / deferred detail | `follow-ups/…` detail (Story 9.11) |
| action item | sprint `action_items` entry + epic | detail via 9.11 slug when present; else list row |
| retro | epic number → retro page | via `EpicRetroMap` |
| code file | repo-relative path | `code/<path>.html` (in-portal) or external `#L{n}` |
| *(optional to classify)* requirement FR/NFR | `RequirementInfo.Id` | requirements detail pages — include if "covers" edges are in MVP |

### Edge kinds the coverage map MUST name (AC #1 vocabulary)

Use these **names** in the written map (even if the spike recommends synonyms or splits):

| Edge kind | Intended meaning | Likely source seam |
|-----------|------------------|--------------------|
| `stemmed-from` | A arose because of B (deferred from story/spec/review; residual quick-dev parent) | Deferred-from / `source_spec` / SourceKey |
| `resolves` | A closes or is closed by B | `RESOLVED in`, resolving href, resolving story/spec in text |
| `covers` | Work delivers a requirement (or epic covers FR) | Coverage maps / `StoriesFor` (epic-granularity caveat) |
| `cites` | Artifact references a code file (or reverse) | Code citation linkifier + `BuildReferencedBy` |
| `raised-in` | Same obligation surfaced in another retro/context | Action-item cross-link heuristic |

Also document **structural** edges the portal already assumes (story∈epic, item∈retro group) even if they are not named in AC #1 — the spike must say whether they are first-class graph edges or implied containment.

### First-class queries the map MUST name (AC #1)

1. **Cycles** — e.g. deferred stems from story A, resolving link points back to A (or A→B→A via cross-links); action-item cross-links that look circular.
2. **Ambiguous reverse-links** — multiple plausible parents (multi-epic tie left Unplanned; multi-match near-dupes; requirement covered by whole epic not per-story).
3. **Multi-hop path** — at least one example query shape for 19.2 (e.g. deferred → source story → covering epic → FR).

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Inventing story parents for retro action items** that only have `epic:` — action items are epic/retro-scoped; do not fabricate a story node.
- **New authoring schema** (YAML fields, frontmatter keys, graph DSL) — forbidden for MVP (Epic 9 principle; AC #2).
- **Second count ledger** — must not re-count open items against `ProjectCounts`.
- **Absorbing Epic 10 nav IA** — graph is an insight surface; nav placement may be a 19.2 note, not a redesign of Insights.
- **Replacing sunburst** — sunburst remains primary remaining-work geometry; graph is provenance/path exploration, not a second "what's left" chart.
- **Code co-change as work provenance** — `CoupledFiles` is evolutionary neighbourhood, not "stemmed-from"; spike should mark out-of-scope for MVP unless a compelling reason is recorded.
- **Building the visualization** — Story 19.2 only.

### Surfaces / process note (Epic 6 Action #3)

Epic 19 introduces a **net-new insight page class** in 19.2. This spike is model/coverage, not a full cross-surface integration spike — but AC #2's recommendation **must** state expected surface reach for 19.2:

| Surface | Expectation for 19.2 MVP (confirm or revise in findings) |
|---------|----------------------------------------------------------|
| HTML | Primary host for any new graph page |
| SPA | Parity via shared body / `RenderParity` (same as other standalone pages) |
| Webview | Dashboard/epics only today — graph page likely **HTML+SPA only** unless spike finds a reason to promote it |
| CLI | Notices only; no graph render |

If the recommendation needs a true surface-coverage gate before 19.2, say so explicitly in Completion Notes (do not silently expand 19.1 into building webview support).

## Tasks / Subtasks

- [ ] **Task 1 — Inventory node types against live code (AC: #1)**
  - [ ] Trace each minimum node type to its identity + detail href builder (table above). Note null/absent cases (no deferred note, no action_items, external code mode, missing retro).
  - [ ] Confirm requirement nodes: in or out of MVP vocabulary with rationale (covers edges are epic-granular per Story 9.1 honesty caveat).
  - [ ] Record any extra node types discovered (ADR, commit, date page, follow-up *group* page) as in-scope, deferred, or out-of-scope.

- [ ] **Task 2 — Inventory directed edges: derivable vs new heuristic (AC: #1)**
  - [ ] For each edge kind (`stemmed-from`, `resolves`, `covers`, `cites`, `raised-in` + structural containment), cite the concrete API (file + type/method) that already emits the relationship **or** mark "needs new heuristic" with what input would be required.
  - [ ] Call out **direction convention** once (recommend: edge points from dependent → origin for `stemmed-from`, or document the opposite — pick one and stick to it for 19.2).
  - [ ] Flag edges that today are **UI-only reverse panels** (e.g. code "Referenced by") vs **forward-authored** (Deferred-from) — both are graph edges; the spike names the asymmetry.

- [ ] **Task 3 — Cycle & ambiguity queries (AC: #1)**
  - [ ] From this repo's live `deferred-work.md` + `sprint-status.yaml` action_items, list 2–3 concrete ambiguous or cyclic-looking examples (or explicitly state "none found; synthesize fixture cases for 19.2 tests").
  - [ ] Define what "cycle" means for MVP (simple directed cycle among named node types) and what is *not* a cycle (breadcrumb up-link ≠ graph edge).

- [ ] **Task 4 — Out-of-scope edge list with rationale (AC: #1)**
  - [ ] Start from the seed non-goals above; add any edges the inventory tempted you to invent (e.g. story→action-item parent, FR→individual story without epic grain).
  - [ ] Each out-of-scope row needs one-sentence rationale tied to no-new-schema / honesty / NFR8.

- [ ] **Task 5 — Recommend one primary 19.2 surface + query path (AC: #2)**
  - [ ] Choose **exactly one** primary visualization + query path among (or justify an equal alternative):
    1. Epic-scoped subgraph (nodes/edges for one epic + attributed follow-ups/code cites from its stories)
    2. Cycle finder (site-wide or epic-scoped)
    3. Path query "deferred → … → epic" (multi-hop from a follow-up detail)
  - [ ] Write success criteria for that choice (what a Driver can answer in one visit).
  - [ ] Write NFR8 absence rules: zero follow-up/code graph → omit surface cleanly (no empty chrome, no dead nav link). Mirror existing `WorkInventory.IsEmpty` / WriteActionItems early-return patterns.
  - [ ] Affirm **no new authoring schema** for the MVP path; list which existing parsers 19.2 must call (not fork).
  - [ ] Note HTML/SPA parity expectation and webview/CLI non-goals for the chosen surface.
  - [ ] Optional: 2–3 sentence feed-forward into `19-2-*.md` Dev Notes once that story file exists (or leave a stub section here for create-story 19.2).

- [ ] **Task 6 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map + recommendation into this story's **Completion Notes** (same convention as Story 8.1).
  - [ ] Do **not** land production `src/**` / `tests/**` changes from this story. Throwaway notes under `_bmad-output/` are OK if useful; prefer Completion Notes as the canonical deliverable.
  - [ ] No new ADR unless a genuine architectural fork appears (escalate via `correct-course` rather than deciding silently). FR37 PRD sync remains "when convenient" — not a blocker for this spike.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing only.** Like Story 8.1: confirm/deny by reading code paths; do not refactor parsers "while you're there."
- **No new authoring schema.** MVP edges must derive from Deferred-from / `source_spec`, sprint `epic:`, quick-dev heuristics, coverage maps, and citation scanners already shipping.
- **Single ledger.** Story 19.2 will be judged against "does not invent a second authoring schema or re-count open items against `ProjectCounts`" — this spike must not recommend a parallel count model.
- **Honesty over precision.** Requirement→story coverage is epic-granular (Story 9.1). Graph edges that pretend per-FR story mapping are out of scope unless new authored data appears (it must not).
- **NFR8.** Absent artifacts → absent surfaces. The recommendation must specify omit-nav / omit-page behavior for zero-graph projects.

### Architecture compliance

- Shared-core projection; any future graph builder is a pure projection over existing models (`FollowUpGeometry`, `UnplannedWorkGeometry`, citation maps) — not a per-surface re-parse. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2]
- Insight surfaces are additive and non-blocking (AD-4). A missing graph must never fail generation.
- Graceful degradation for unsupported/malformed artifacts (Inherited Invariants).

### Suggested coverage-map shape (Completion Notes)

Use tables, not prose walls:

1. **Nodes** — type | identity key | href builder | absent → omit?
2. **Edges** — kind | from → to | derivable? (API) | new heuristic? | notes
3. **Queries** — name | inputs | success signal | fixture idea
4. **Out of scope** — edge/node | rationale
5. **19.2 recommendation** — chosen surface | success criteria | NFR8 rules | parsers to reuse | surface reach

### Known seam caveats (spike must classify, not "fix")

- **Action → retro** is UI-assumed via `EpicRetroMap[epic]`, not an authored per-item retro id — mark soft vs hard in the edge table.
- **Path-prefixed `SourceKey` join gaps** can empty reverse panels while prose still shows provenance — call out as ambiguity/query case (live deferred-work debt).
- **`covers` is epic-granular** — `StoriesFor` returns every story in covering epics; graph must not pretend per-FR story precision.
- **Closest proto-edge record today:** `FollowUpDeferredSlot` (`SourceKey`, `SourceStoryId`, `EpicNumber`, `Resolving*`, `DetailHref`) — project from it; do not invent a parallel type in the spike.

### Anti-patterns to prevent

- Reimplementing `DeferredWorkParser` / `FollowUpRefs` / `UnplannedWorkGeometry` as a second "graph ingest."
- Treating sunburst wedge membership, breadcrumbs, or SPA Parent/Children as the work-graph model.
- Declaring co-change / git coupling as `stemmed-from` without an explicit vocabulary decision.
- Requiring authors to add graph YAML for MVP.
- Expanding 19.1 into building `Charts.*` SVG or a new page templater.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md`
- Sprint key: `19-1-work-graph-model-and-coverage-spike`
- Downstream story key (not created by this spike): `19-2-directed-graph-visualization-and-path-query`
- No `src/` touches expected for 19.1

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 19 + Story 19.1 / 19.2 ACs]
- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-07-18.md` — create-story action for Epic 19]
- [Source: `_bmad-output/implementation-artifacts/epic-9-context.md` — Epic 19 downstream of Epic 9 provenance]
- [Source: `_bmad-output/implementation-artifacts/9-6-follow-up-item-provenance-and-resolution-paths.md` — deferred/action provenance seams]
- [Source: `_bmad-output/implementation-artifacts/9-12-unplanned-and-one-off-work-in-geometry-and-sprint.md` — quick-dev attribution]
- [Source: `_bmad-output/implementation-artifacts/7-2-source-citation-and-comment-linking-to-code-pages.md` — cites / Referenced by]
- [Source: `_bmad-output/implementation-artifacts/8-1-integration-spike-cross-surface-status-verification.md` — spike deliverable convention]
- [Source: `src/SpecScribe/DeferredWorkParser.cs`, `FollowUpRefs.cs`, `FollowUpGeometry.cs`, `UnplannedWorkGeometry.cs`, `CodeReferenceScanner.cs`, `SiteGenerator.BuildReferencedBy`]
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — shared core, graceful degrade, insight providers]
- [Source: epics.md NFR8 — degrade to absent]

### Previous story intelligence

- **Epic 9 (done):** Provenance is heuristic-over-prose; reverse panels and sunburst membership are separate from a unified edge list — 19.1's job is to name that unification without rewriting 9.x.
- **Story 8.1:** Findings live in Completion Notes; no ADR unless architecture fork; "no gaps" is a valid outcome.
- **Story 7.8:** Existing `Charts.ReferenceGraph` is a *code-file neighbourhood* viz — useful precedent for SVG a11y (sr-only lists, caps) but **not** the work-graph model; do not conflate in the coverage map.
- **Epic 6 Action #3:** Net-new surfaces need surface-reach clarity before build — capture in AC #2 recommendation, not by expanding this spike into implementation.

### Git intelligence summary

Recent commits (as of create-story) closed Epic 2/3 deferred debt and follow-up list batch actions — reinforcing that deferred/action provenance remains actively dogfooded. No work-graph code exists yet; spike starts from a clean slate on the *visualization* side with a rich seam inventory on the *data* side.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-18 — Story 19.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + 19.2 recommendation; no production code.
