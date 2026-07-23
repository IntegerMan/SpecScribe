---
baseline_commit: c5b93734d56d618a7a117060a1f4a3917d2745aa
---

# Story 19.1: Work-Graph Model and Coverage Spike

Status: done

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

- [x] **Task 1 — Inventory node types against live code (AC: #1)**
  - [x] Trace each minimum node type to its identity + detail href builder (table above). Note null/absent cases (no deferred note, no action_items, external code mode, missing retro).
  - [x] Confirm requirement nodes: in or out of MVP vocabulary with rationale (covers edges are epic-granular per Story 9.1 honesty caveat).
  - [x] Record any extra node types discovered (ADR, commit, date page, follow-up *group* page) as in-scope, deferred, or out-of-scope.

- [x] **Task 2 — Inventory directed edges: derivable vs new heuristic (AC: #1)**
  - [x] For each edge kind (`stemmed-from`, `resolves`, `covers`, `cites`, `raised-in` + structural containment), cite the concrete API (file + type/method) that already emits the relationship **or** mark "needs new heuristic" with what input would be required.
  - [x] Call out **direction convention** once (recommend: edge points from dependent → origin for `stemmed-from`, or document the opposite — pick one and stick to it for 19.2).
  - [x] Flag edges that today are **UI-only reverse panels** (e.g. code "Referenced by") vs **forward-authored** (Deferred-from) — both are graph edges; the spike names the asymmetry.

- [x] **Task 3 — Cycle & ambiguity queries (AC: #1)**
  - [x] From this repo's live `deferred-work.md` + `sprint-status.yaml` action_items, list 2–3 concrete ambiguous or cyclic-looking examples (or explicitly state "none found; synthesize fixture cases for 19.2 tests").
  - [x] Define what "cycle" means for MVP (simple directed cycle among named node types) and what is *not* a cycle (breadcrumb up-link ≠ graph edge).

- [x] **Task 4 — Out-of-scope edge list with rationale (AC: #1)**
  - [x] Start from the seed non-goals above; add any edges the inventory tempted you to invent (e.g. story→action-item parent, FR→individual story without epic grain).
  - [x] Each out-of-scope row needs one-sentence rationale tied to no-new-schema / honesty / NFR8.

- [x] **Task 5 — Recommend one primary 19.2 surface + query path (AC: #2)**
  - [x] Choose **exactly one** primary visualization + query path among (or justify an equal alternative):
    1. Epic-scoped subgraph (nodes/edges for one epic + attributed follow-ups/code cites from its stories)
    2. Cycle finder (site-wide or epic-scoped)
    3. Path query "deferred → … → epic" (multi-hop from a follow-up detail)
  - [x] Write success criteria for that choice (what a Driver can answer in one visit).
  - [x] Write NFR8 absence rules: zero follow-up/code graph → omit surface cleanly (no empty chrome, no dead nav link). Mirror existing `WorkInventory.IsEmpty` / WriteActionItems early-return patterns.
  - [x] Affirm **no new authoring schema** for the MVP path; list which existing parsers 19.2 must call (not fork).
  - [x] Note HTML/SPA parity expectation and webview/CLI non-goals for the chosen surface.
  - [x] Optional: 2–3 sentence feed-forward into `19-2-*.md` Dev Notes once that story file exists (or leave a stub section here for create-story 19.2).

- [x] **Task 6 — Record findings; no production code (AC: #1, #2)**
  - [x] Write the coverage map + recommendation into this story's **Completion Notes** (same convention as Story 8.1).
  - [x] Do **not** land production `src/**` / `tests/**` changes from this story. Throwaway notes under `_bmad-output/` are OK if useful; prefer Completion Notes as the canonical deliverable.
  - [x] No new ADR unless a genuine architectural fork appears (escalate via `correct-course` rather than deciding silently). FR37 PRD sync remains "when convenient" — not a blocker for this spike.

### Review Findings

_Code review 2026-07-22 (3 parallel layers: Blind Hunter / Edge Case Hunter / Acceptance Auditor; all claims re-verified against source at HEAD `cec0932`). Auditor verdict: **PASS on both ACs and all spike constraints** — the coverage map's core conclusions (fully derivable, no new schema, epic-scoped subgraph) hold. Findings below are refinements to make the map trustworthy for 19.2. No `src/**` fix is implied — every patch edits this document._

**Decision-needed (owner call required before 19.2 build):**

- [x] [Review][Decision] Epic-scoped surface has no host for Unplanned / unattributed (epic = null) nodes — The chosen §5 surface is strictly per-epic, and its NFR8 rules only cover "empty epic" and "no epics." But unattributed action items (`SprintActionItem.EpicNumber == null` → "Unattributed" bucket, `ActionItemsTemplater.cs:93,256`; also skipped by `FindNearDuplicates` at `:135`) and Unplanned quick-dev (`ResolveQuickDevEpic` → null → `UnplannedWorkGeometry`) carry real `stemmed-from`/`resolves`/`cites` edges yet belong to no epic page. As written, exactly the work with the most orphaned provenance silently vanishes from the graph. Decide: give Unplanned an "Unattributed" pseudo-epic bucket (mirroring the action-items page), or explicitly declare it out-of-scope for the MVP and say so in §5.

- [x] [Review][Decision] Collapse / cycle-model semantics under-specified for distinct endpoints — MVP "cycle" is defined over *collapsed node identities* (§3), but the only fixture (ex. 1) demonstrates collapse solely for the self-loop case A == B. For a deferred item with `stemmed-from → A` and `resolves → B`, A ≠ B, the collapse transform is undefined: does it manufacture a transitive A→B edge (which would fabricate cycles the authored provenance never had) or keep the item as its own node with two out-edges (safe, but then real resolver-loops need the item retained)? Also, ex. 1's causal story over-narrates — the self-loop arises purely from the `stemmed-from` self-reference (`source_spec == heading`, `deferred-work.md:26-37`); the `resolves` edge is not needed to close it. Confirm the collapse rule and correct the fixture narration so 19.2's cycle finder is built on a precise definition.

**Patch (unambiguous doc corrections):**

- [x] [Review][Patch] External-code-mode NFR8 rule is factually wrong [19-1-…spike.md §5 NFR8 + Nodes "code file" row] — Doc claims "external-only code mode → in-portal code nodes simply absent." But `_codePages` is populated **unconditionally** (Story 7.7 made `--code-url` additive; `SiteGenerator.cs:255-258`), so in-portal code pages/nodes exist even in external mode — only the render-time link target differs. The doc appears to have relied on the stale "empty in external mode" comment at `SiteGenerator.cs:86`, which describes `_codeReferenced` (a different set). Correct the rule so 19.2 doesn't code an "external ⇒ suppress code nodes" absence path that drops real nodes.

- [x] [Review][Patch] Harden the unresolved-`SourceKey` caveat into an explicit anti-phantom guardrail [19-1-…spike.md known-seam caveats / §4] — The map flags "path-prefixed SourceKey join gaps" as an ambiguity case but never states what the graph *does* with an unresolvable key (`FindQuickDev` null + no epic + no story). Given this repo just fixed a phantom-item bug of exactly this shape (commit `a16ca0f`, "DeferredWorkParser treating bare source_spec bullets as phantom items"), add a guardrail: an unresolved `SourceKey` must **never** mint a node from the raw string — drop the edge or attach to a single "unresolved" sink.

- [x] [Review][Patch] Add a code-file node cap; scale-exclusion is inconsistent [19-1-…spike.md §1 exclusions + §5 "bounded"] — §1 excludes commit/date/group pages because they "scale with the target repo, not planning artifacts," yet §1 *keeps* code-file nodes whose `cites` targets scale with the target repo identically, and §5 calls the subgraph "bounded" with no cap. The `Charts.ReferenceGraph` `+N more` cap the doc already cites (feed-forward stub) must be promoted into a real §1/§5 node rule so an epic citing hundreds of files can't explode the graph.

- [x] [Review][Patch] Fix stale line-number citations in the Task-3 ambiguity example [19-1-…spike.md §3 example 2] — Cites `sprint-status.yaml` lines 330/338/358; actual are 338/346/366 (+8, from the Epic 24 block inserted after the trace). Content/quotes are correct; only the anchors drifted.

**Deferred (real, but belongs to a later story — not fixable in this spike):**

- [x] [Review][Defer] Consider a short ADR for the carrier→target direction convention [19-1-…spike.md §2] — deferred, revisit at 19.2 / Epic 24 create-story. The spike consciously decided no fork appeared (defensible for a single not-yet-built surface), but the convention is load-bearing and Epic 24 now inherits it; repo memory records a standing "agents under-propose ADRs" lesson. Escalate to an ADR if/when the convention spreads beyond Epic 19. **→ RESOLVED 2026-07-22 (quick-dev):** trigger met — Epic 24 (change-coupling graphs, FR40) now inherits it — so escalated to [ADR 0011 — Directed-Graph Edge Direction: Carrier → Target](../../docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md) (Accepted). The ADR promotes §2 to a cross-surface invariant and states how Epic 24's directional/symmetric coupling edges inherit the discipline. No `src/**` change (convention was already in force).

**Dismissed (1):** multi-epic requirement duplicating its `covers` edge across per-epic subgraphs — for a deliberately per-epic surface, showing a shared requirement in each covering epic's subgraph is expected behavior, not a defect.

**Resolution (2026-07-22):** Both decisions resolved by owner — D1 → render Unplanned/unattributed in an "Unattributed" pseudo-epic bucket; D2 → keep distinct-endpoint items as their own node (no manufactured transitive edge). All 6 patches applied to the Completion Notes above (§1 code-file node row, §3 cycle example + collapse rule, §5 NFR8 rules, feed-forward guardrails). No `src/**` changes. Spike deliverable amended in place; conclusions unchanged.

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

claude-opus-4-8 (Amelia / dev-story workflow)

### Debug Log References

Spike — tracing only, no build/test run required (Task 6 forbids `src/**` / `tests/**` changes). Seams confirmed by reading live code at baseline `c5b9373`:
`DeferredWorkParser.cs`, `FollowUpRefs.cs`, `FollowUpGeometry.cs`, `UnplannedWorkGeometry.cs`, `ActionItemsTemplater.cs`, `SprintStatus.cs`, `RequirementsParser.cs` / `RequirementsModel.cs`, `SiteGenerator.cs` (`DiscoverCodeReferences` / `BuildReferencedBy` / `_codeReverseMap` / `_citerToFiles` / `EpicRetroMap`).
Live examples pulled from this repo's own `_bmad-output/implementation-artifacts/deferred-work.md` and `sprint-status.yaml` `action_items`.

### Completion Notes List

**SPIKE OUTCOME: coverage map complete, no production code landed.** The work graph is fully derivable from shipping parsers/geometry — **no new authoring schema is required for a first visualization (AC #2 satisfied).** The one proto-edge record that already exists (`FollowUpDeferredSlot`) is the recommended projection seed; do not invent a parallel graph type in 19.2.

#### 1. Nodes — type | identity key | href builder | absent → omit?

| Node type | Canonical identity (confirmed) | Href builder | Absent → omit? |
|-----------|--------------------------------|--------------|----------------|
| epic | `EpicInfo.Number` | `epics/epic-N.html` | no epics → no graph (whole surface omits) |
| story | `StoryInfo.Id` (`N.M`) | `story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(id)` (`FollowUpRefs.BuildHrefMap`) | per-epic; empty epic still valid |
| quick-dev / one-shot | `spec-*` stem of `QuickDevEntry.OutputPath` | `QuickDevEntry.OutputPath` (via `FollowUpGeometry.FindQuickDev` / `ResolveSourceHref`) | none present → node class empty, no Unplanned root (`UnplannedWorkGeometry.HasUnplanned == false`) |
| deferred item | `FollowUpSlug` slug / `FollowUpDeferredSlot` | `FollowUpDeferredSlot.DetailHref` (9.11 detail or list fallback) | no `deferred-work.md` → `work.Deferred is null` → zero slots |
| action item | `SprintActionItem` (Action+Epic; ref-equality keyed) | `FollowUpGeometry.HrefFor` / `FollowUpSlug.OutputPath` | no `action_items:` → `WriteActionItems` early-returns |
| retro | epic number → retro page | `EpicRetroMap[epic]` (soft: keyed by epic, **not** a per-item retro id) | no retros → `EpicRetroMap` empty; action→retro edges downgrade to plain `<span>` (see `AppendCrossLinks`) |
| code file | repo-relative path (Ordinal-cased) | `code/<path>.html` (`_codePages`) or external `#L{n}` (`--code-url`) | no citations (and not git-widget-surfaced) → in-portal node absent. **External `--code-url` mode does NOT remove the node** — `_codePages` is populated unconditionally (Story 7.7 made `--code-url` additive; only the render-time link target changes). Per-epic code nodes capped (`+N more`, see §5). [code-review 2026-07-22] |
| requirement (FR/NFR/UX-DR) | `RequirementInfo.Id` | requirements detail page | **IN MVP as node, but only for epic-grain `covers` edges** — see Task 1 decision below |

**Extra node types discovered (Task 1.3 classification):**
- **ADR** — cites code the same as stories (`DiscoverCodeReferences` scans the ADR tree via `EnumerateAdrFiles`; ADR-only citations resolve through `_codeReverseMap`). **In-scope as a `cites` source node** for 19.2 (it already participates in the citation graph); not a distinct provenance origin beyond that.
- **commit / date page** (Story 7.3/7.5) — **out of scope.** These scale with the *target* repo, not planning artifacts, and are already deliberately excluded from the SPA parity set (`SiteGenerator` owner-decision exclusion of `_codePages` ∪ `_commitDays` ∪ `commit/`). Treating them as graph nodes would explode node count unboundedly.
- **follow-up *group* page** (`follow-ups/group-*.html`, Story 9.13) — **out of scope as a node**; it is a filtered *view* of deferred/unplanned members, not an identity. Its members are already nodes.

**Task 1 requirement-node decision:** requirement nodes are **IN** the MVP vocabulary but carry **only epic-granular `covers` edges** (`RequirementInfo.CoverageEpicNumbers` → epic; `RequirementsParser.StoriesFor` expands to *every* story in the covering epic, not the specific stories for that FR — the Story 9.1/3.7 honesty caveat, documented verbatim on `RequirementInfo` and `StoriesFor`). 19.2 **must not** draw requirement→individual-story edges; that would fabricate per-FR precision the data does not have. Rationale: honesty over precision (Dev Notes constraint).

#### 2. Edges — kind | from → to | derivable? (API) | new heuristic? | notes

**Direction convention (Task 2, picked once, load-bearing for 19.2):**
> **`From` = the node that physically carries the reference; `To` = the node referenced.** (carrier → target)

This is the *honest, implementation-aligned* rule — it matches which side actually holds the pointer in the authored source, so no direction is inferred. Provenance ("where did this come from?") is answered by walking **out-edges** from a deferred/action node. It reads naturally for 4 of 5 kinds; `covers` is the one inversion (stored requirement→epic, labelled "covered-by") because the coverage line lives on the requirement.

| Edge kind | From → To (carrier → target) | Derivable today? | Concrete API |
|-----------|------------------------------|------------------|--------------|
| `stemmed-from` | deferred item → source story/spec/quick-dev | ✅ derivable | `DeferredWorkGroup.SourceKey` / `SourceStoryId` / `SourceStoryHref`; `FollowUpRefs.SourceSpecFileFromText` + `StoryIdFromKey`; projected on `FollowUpDeferredSlot.SourceKey/SourceStoryId/SourceHref` |
| `stemmed-from` (residual quick-dev parent) | deferred item → parent quick-dev | ✅ derivable | `FollowUpGeometry.FindQuickDev` + `EnrichQuickDevDeferredEpics`; `UnplannedWorkGeometry` resurfaced done parents |
| `resolves` (is-resolved-by) | deferred item → resolving story/spec | ✅ derivable | `DeferredWorkItem.Resolved` + `ResolvingRef`/`ResolvingHref`; `FollowUpRefs.ResolvingStoryIdFromText` (`RESOLVED in N.M` / backtick token) |
| `covers` (covered-by) | requirement → covering epic | ✅ derivable (**epic-grain only**) | `RequirementInfo.CoverageEpicNumbers`; `RequirementsParser.StoriesFor` (epic→its stories, honesty caveat) |
| `cites` | citing artifact (story/doc/ADR) → code file | ✅ derivable (forward-authored) | `_citerToFiles` (forward), `CodeReferenceScanner` / `CodeReferenceLinkifier` |
| `cites` (referenced-by) | code file ← citing artifact | ✅ derivable (**UI-only reverse panel today**) | `_codeReverseMap` + `SiteGenerator.BuildReferencedBy` — same data, reverse index; **asymmetry flagged below** |
| `raised-in` | action item → other epic's retro | ⚠️ heuristic (already shipping) | `ActionItemsTemplater.FindNearDuplicates` (Jaccard ≥ 0.45 AND ≥ 6 shared tokens) + `EpicRetroMap`; conservative, false-negative-biased |
| **structural**: story ∈ epic | story → epic | ✅ derivable (containment) | `EpicInfo.Stories` / `EpicFromStoryId` |
| **structural**: action/deferred ∈ epic | item → epic | ✅ derivable (attribution) | `SprintActionItem.EpicNumber`; `FollowUpDeferredSlot.EpicNumber` (+ `ResolveQuickDevEpic` heuristic tiers) |
| **structural**: item ∈ retro group | action item → epic-retro | ⚠️ soft | `EpicRetroMap[epic]` — epic-keyed, **not** a per-item retro id (see caveat) |

**Task 2 asymmetry call-out (required):** graph edges come in two authorship shapes and 19.2 must treat both as first-class:
- **Forward-authored** — the source data physically names the target: Deferred-from `source_spec:`, `RESOLVED in`, coverage map lines, in-body code citations. These are unambiguous.
- **UI-only reverse panels** — the target aggregates its inbound edges at render time with **no authored back-pointer**: code "Referenced by" (`BuildReferencedBy` over `_codeReverseMap`), story/quick-dev "Deferred from this" (`FollowUpGeometry.DeferredForSource`). These are *inversions of forward edges*, not new edges — 19.2 should build them by reversing the forward index, not by a second parse.

**`covers` structural note:** story↔epic containment and item↔epic attribution are **first-class graph edges** (they carry real identity joins), but retro membership is **implied containment via `EpicRetroMap`, not an authored per-item edge** — mark it soft; a retro node is epic-granular.

#### 3. Queries — name | inputs | success signal | fixture idea

| Query | Inputs | Success signal | Fixture idea |
|-------|--------|----------------|--------------|
| **Cycle finder** | node set + carrier→target out-edges | reports a directed cycle among named node types | see live self-loop below |
| **Ambiguous reverse-link** | a target node | lists >1 plausible origin | Epic 1 heatmap-debt (below) |
| **Multi-hop path** | a deferred/action node | walks deferred → source story → covering epic → FR | 7.11 chain (below) |

**Task 3 — live examples from this repo (not synthesized):**

1. **Cyclic-looking (self-loop when items collapse to source node):** In `deferred-work.md`, the `spec-6-9-deferred-debt-cleanup` items are **`RESOLVED … (spec-epic6-deferred-debt-cleanup)`** → a `resolves` edge to node `spec-epic6-deferred-debt-cleanup`. That very spec *also* appears as a Deferred-from heading (`## Deferred from: code review of spec-epic6-deferred-debt-cleanup`) whose items carry `source_spec: spec-epic6-deferred-debt-cleanup` → a `stemmed-from` edge **to the same node**. When deferred items are collapsed onto their source node, `spec-epic6-deferred-debt-cleanup` is both source and target of a `stemmed-from` edge — a direct self-loop arising from the source-equals-heading self-reference **alone** (the separate `resolves` edge from the `spec-6-9` items is not required to close it; the earlier "resolver-that-also-spawns" framing over-narrated a single-edge self-loop). [code-review D2, 2026-07-22] MVP "cycle" = a simple directed cycle over collapsed node identities; this is the canonical first fixture.
2. **Ambiguous reverse-link (multi-epic obligation):** the Epic 1 heatmap-debt action recurs at `epic: 1` (line 338), `epic: 2` (line 346, *"carried unaddressed across two retrospectives"*), and `epic: 3` (line 366, *"resolved via spec-epic1-heatmap-debt-triage but left open"*). `FindNearDuplicates` already emits `also raised in Epic N retrospective` cross-links between these. "Which retro owns this obligation?" has **no unique answer** — that is the ambiguity, not a bug.
3. **Multi-hop path:** deferred item `source_spec: 7-11-code-ownership-and-bus-factor-insights.md` → (`stemmed-from`) story **7.11** → (structural) **Epic 7** → (`covers`⁻¹) the FR(s) whose `CoverageEpicNumbers` include 7. One visit answers "what requirement is ultimately behind this open debt item?"

**MVP cycle definition:** a simple directed cycle among the named node types over the carrier→target edge set (deferred/action/quick-dev/story/epic/spec/code). **NOT a cycle:** a `BreadcrumbTrail` up-link or SPA `Manifest.Parent/Children` (page hierarchy, not provenance); a reverse panel viewed alongside its own forward edge (same edge, two renderings). **Collapse rule (distinct endpoints, code-review D2):** collapse merges only *identical* node identities; a single deferred item with `stemmed-from → A` and `resolves → B`, A ≠ B, is **kept as its own node with two out-edges** — 19.2 must NOT manufacture a transitive `A → B` edge (that would fabricate cycles the authored provenance never had). Collapse-to-source is safe only where source == resolver (the self-loop in example 1).

#### 4. Out of scope — edge/node | rationale

| Out-of-scope edge/node | One-sentence rationale |
|------------------------|------------------------|
| Story-parent for a retro action item (`epic:`-only) | Action items are epic/retro-scoped; fabricating a story node invents authored data that doesn't exist (no-new-schema / honesty). |
| Requirement → individual story `covers` | `StoriesFor` is epic-grain (9.1 caveat); per-FR story edges would over-claim precision (honesty). |
| Code co-change (`FileInsight.CoupledFiles` / `Charts.ReferenceGraph`) as `stemmed-from` | Evolutionary neighbourhood ≠ work provenance; conflating them mislabels correlation as causation (explicit vocabulary decision — **out for MVP**). |
| commit / date / follow-up-group pages as nodes | Scale with the target repo, not planning artifacts; already excluded from SPA parity — unbounded node growth (NFR8 / perf). |
| Second count ledger | `ProjectCounts` (`OpenActionItems` / `DeferredOpenItems` / `DirectChanges` / `RequirementsOverall`) is the single tally; a graph recount would drift (single-ledger invariant — `FollowUpGeometry` even `Debug.Assert`s this). |
| New authoring schema (graph YAML / frontmatter / DSL) | Forbidden for MVP; every edge above already derives from shipping prose/parsers (Epic 9 principle, AC #2). |
| Breadcrumb / SPA Parent-Children as provenance edges | Page-nav hierarchy, not work provenance (would pollute cycle detection). |
| Promoting graph page to webview | Webview is dashboard/epics-only today; net-new surface reach needs its own decision, not silent expansion of this spike (Epic 6 Action #3). |

#### 5. 19.2 recommendation — chosen surface | success criteria | NFR8 rules | parsers to reuse | surface reach

**Chosen primary surface (exactly one): Epic-scoped provenance subgraph** — for a single epic, render its stories + attributed deferred/action items + their `stemmed-from` / `resolves` / `cites` edges out to source stories/specs/code, with the epic-scoped `raised-in` cross-links. Ships as a dedicated **HTML page** (one per epic, or one page filtered by epic) reusing the exact `FollowUpGeometry.ForEpic(n)` / `DeferredForEpicNumber` / `StoryChildDeferred` scoping that already exists.

*Why this over the alternatives:* the **cycle finder** (alt 2) is a compelling *query* but a weak *first surface* — on a healthy repo it renders nearly empty (NFR8 risk), so it belongs as a secondary toggle/annotation on the subgraph, not the MVP page. The **path query** (alt 3) needs an interaction model (pick-a-node → trace) that pushes toward client JS — deliberately deferred to the Epic 20 interactive-explorer budget, not baked into 19.2. Epic-scoped subgraph is bounded (one epic's fan-out), reuses `ForEpic` scoping verbatim, degrades cleanly (an epic with no follow-ups/citations → no graph section), and answers real Driver questions statically. It also mirrors the placement pattern of every other standalone insight page (Traceability 21.1, Risk Quadrant, Code Map).

**Success criteria (what a Driver answers in one visit to an epic's graph):**
- "What open debt stemmed from this epic's stories, and did any of it get resolved (and by what)?"
- "Which code files do this epic's stories cite, and which stories share files?" (via reversed `_citerToFiles`)
- "Does any obligation here also live in another epic's retro?" (`raised-in`).

**NFR8 absence rules (mirror shipping patterns):**
- Zero follow-ups AND zero citations for an epic → **omit the graph section entirely** (no empty chrome), mirroring `WriteActionItems` early-return and `UnplannedWorkGeometry.HasUnplanned`/`GroupRootHref == null`.
- No epics at all → **no nav entry, no page** (same `hasEpics` gate the Delivery/Traceability nav group already uses).
- **Unplanned / unattributed nodes (epic = null)** → render in a synthetic **"Unattributed" pseudo-epic bucket** (action items with `EpicNumber == null`; quick-dev with no resolvable epic → `UnplannedWorkGeometry`), mirroring the action-items page's trailing Unattributed group (`ActionItemsTemplater.GroupByEpic`). These **MUST NOT be silently dropped** — orphaned provenance is a primary trace target — but the bucket omits cleanly when empty, same rule as an empty epic. [code-review D1, 2026-07-22]
- **Code-file node cap** → `cites` targets scale with the *target repo*, not planning artifacts (the same property that keeps commit/date pages out of §1). Bound per-epic code-file nodes with the `Charts.ReferenceGraph` `+N more` cap (house style) so an epic citing many files can't explode the subgraph. [code-review, 2026-07-22]
- External code mode → `cites` edges still resolve (to external `#L{n}`); **in-portal code nodes are STILL present** — `_codePages` is populated unconditionally (Story 7.7 additive; only the link target is chosen at render time), so 19.2 must **not** suppress code nodes in external mode. No dead links either way. [code-review, 2026-07-22 — corrects the original "nodes absent" claim]
- Retros absent → `raised-in` edges downgrade to non-link labels (already how `AppendCrossLinks` behaves).

**Parsers 19.2 MUST reuse (call, never fork):** `DeferredWorkParser` → `DeferredWorkModel`; `FollowUpGeometry.From/.ForEpic` (+ `FollowUpDeferredSlot` as the proto-edge record — project from it, do not invent a parallel type); `UnplannedWorkGeometry`; `FollowUpRefs` (all ref resolution); `ActionItemsTemplater.FindNearDuplicates` (`raised-in`); `RequirementsParser.StoriesFor` / `CoverageEpicNumbers` (`covers`); `_codeReverseMap` + `_citerToFiles` + `BuildReferencedBy` (`cites` both directions); `EpicRetroMap`. Counts stay on `ProjectCounts` — **no recount.**

**Surface reach for 19.2 (confirming the story's table):** **HTML + SPA only.** HTML is the primary host; SPA parity comes free via the shared `<main id="main-content">` body seam + `RenderParity` (same as every other standalone page). **Webview and CLI are non-goals** — no reason found to promote the graph into the webview's dashboard/epics-only surface; if a true cross-surface webview gate is ever wanted it is a *separate* decision (Epic 6 Action #3), not an expansion of 19.1 or 19.2's MVP.

**No new authoring schema affirmed.** No new ADR required — no architectural fork appeared; a future graph builder is a pure projection over existing models, consistent with ARCHITECTURE-SPINE AD-1/AD-2/AD-4. FR37 PRD sync remains "when convenient."

#### Feed-forward stub for `19-2-*.md` create-story

- **Title candidate:** Story 19.2 — Epic-Scoped Provenance Subgraph (directed work-graph visualization).
- **Data source:** project `FollowUpDeferredSlot` + reversed `_citerToFiles`/`_codeReverseMap` into an in-memory `{nodes, edges}` per epic; edge direction = carrier → target (§2).
- **Visual:** static SVG (pure-SVG house style, cf. `Charts.ReferenceGraph` a11y precedent — sr-only node/edge lists, node caps + "+N more") — **not** the Epic 20 interactive explorer.
- **Placement:** dedicated page under the Delivery/Insights nav group on the shared `hasEpics` gate; per-epic section or epic filter.
- **Guardrails:** no new schema, no second count ledger, epic-grain `covers` only, cycle detection as a secondary annotation (not the primary surface), NFR8 omit-on-empty; **unresolved `SourceKey` must never mint a phantom node from the raw string** (drop the edge or attach to a single "unresolved" sink — cf. the `a16ca0f` phantom-item fix); Unplanned/unattributed (epic = null) nodes go in an "Unattributed" pseudo-epic bucket (D1); distinct-endpoint items are not collapsed into a transitive edge (D2).

### File List

_Spike — no `src/**` or `tests/**` changes (Task 6). Only this story file's Dev Agent Record + frontmatter `baseline_commit` and `sprint-status.yaml` status were updated._

- `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md` (frontmatter, tasks, Dev Agent Record, status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status: ready-for-dev → in-progress → review)

## Change Log

- 2026-07-18 — Story 19.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + 19.2 recommendation; no production code.
- 2026-07-22 — Spike executed (dev-story). Coverage map (nodes/edges/queries/out-of-scope/recommendation) written to Completion Notes by tracing live code at baseline `c5b9373`. Direction convention fixed (carrier → target). 19.2 recommendation = epic-scoped provenance subgraph, HTML+SPA only, no new authoring schema, no new ADR. Live cycle/ambiguity/multi-hop examples sourced from this repo's own `deferred-work.md` + `sprint-status.yaml`. No `src/**`/`tests/**` changes. Status → review.
