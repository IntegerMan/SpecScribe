---
title: 'Reference graph: epic grouping + cross-relationship edges'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '0c59e0a2cb062e6a5aaaae420154bbc9a00ecb00'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The code-page reference graph (Story 7.8, `Charts.ReferenceGraph` + `CodeFileTemplater`) shows a flat ring of citing-artifact nodes (stories/epics/ADRs) and a flat ring of related co-changed files around the center file, but it never shows which epic a citing story belongs to, nor any relationship between the outer-ring nodes themselves (a story that also cites a related file; two related files that are themselves frequently co-changed).

**Approach:** Add two independent, pure-CSS opt-in toggles on the code page's Relationships view: (1) "Group by epic" nests citing-artifact nodes under their parent epic (file → epic → story hub layout) instead of a flat ring; (2) "Show relationships" draws extra edges between outer-ring nodes — story↔related-file when that story also cites the related file, and related-file↔related-file when the pair is itself frequently co-changed. All 4 combinations are pre-rendered server-side (mirroring the existing Code Map `BuildVariants` pattern) and swapped via checkbox + CSS sibling-combinator — no JavaScript.

## Boundaries & Constraints

**Always:**
- No JavaScript; toggles are plain `<input type="checkbox">` + CSS `~` sibling-combinator selectors switching between pre-rendered `<div data-view="...">` SVG variants, matching `CodeMapTemplater.AppendFilterCheckbox` / `CodeMap.BuildVariants` (`src/SpecScribe/CodeMap.cs`, `specscribe.css:2566-2571`).
- Epic resolution reuses `_epicsModel` (`EpicsModel.Epics[].Stories[].ArtifactOutputPath`/`StoryEpicLinkifier.StoryPagePath`) already loaded in `SiteGenerator` — match each citing artifact's `OutputUrl` against a story's page path to get its epic number/title. No new parsing of titles/paths.
- Related-file↔related-file co-change counts reuse the same numstat parse already run for `--deep-git` (`GitMetrics.ParseNumstatLog`'s existing pair-count map) — expose the already-computed pair counts (currently capped to top 10 in `DeepGitPulse.Coupling`) for arbitrary pair lookups; do **not** add a second git call or re-scan commits.
- Degrade gracefully: no `--deep-git` / no insight → both toggles render their "off" state only (today's flat graph), never throw, never a dead link.
- Citing-artifact and related-file node styling (shape/color/edge from Story 7.8) is unchanged; only ring position (grouped) and extra edges (relationships) are new.
- Escape all derived strings (`PathUtil.Html`), keep layout deterministic (golden/parity stability).
- Neutral tokens only for any new edge/grouping visuals — no `--status-*`, no new gold usage beyond existing artifact nodes.

**Ask First:** none anticipated — visual layout, grouping semantics, and toggle mechanism were locked via user clarification before this spec was written.

**Never:**
- No new git fetch or a second commit-log scan — only expose/query the existing pair-count structure.
- No JS-based toggle.
- No change to the citing-artifact or related-file node shapes/colors themselves (Story 7.8 design stays locked).
- No epic grouping for the related-file (co-change) population — that population is ungrouped; only its edges change under "Show relationships".

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Both toggles off (default) | Flag combo `(epic=off, rel=off)` | Byte-identical to today's Story 7.8 graph | N/A |
| Epic grouping on, one epic | All citing stories share one epic | Single epic hub node with all story nodes nested under it | N/A |
| Epic grouping on, citing artifact has no epic (ADR/doc) | Non-story citer | Rendered at top level (not nested under any epic hub), unchanged from flat style | N/A |
| Relationships on, a citing story also cites a related file | Story S cites center file F and related file R | Edge drawn between S's node and R's node | N/A |
| Relationships on, two related files are co-changed with each other | Related files R1, R2 also paired in coupling data | Edge drawn between R1 and R2 nodes | N/A |
| Relationships on, no cross-relationships exist | No overlaps found | No extra edges; toggle renders identically to off-state for this file | N/A |
| `--deep-git` off / no insight | No `FileInsight` | Both toggles present but relationships/grouping data unavailable → checkboxes still render, "on" state degrades to the same flat/no-extra-edge output as "off" | Never throw |
| Hub-file with artifact cap overflow | >`RefGraphArtifactNodeCap` citers | Capped/overflow behavior from 7.8 preserved inside each epic hub too (per-epic or global cap — implementer picks one deterministic rule and documents it) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs:1121-1244` -- `ReferenceGraph`: extend to accept optional epic-hub grouping data for the artifact population and an optional list of extra ring-to-ring edges; render 4 deterministic layout variants.
- `src/SpecScribe/CodeFileTemplater.cs:328-428` (`BuildAside`/`BuildRelationshipsCard`), `:248-265` (`BuildRelatedNodes`) -- thread epic info per citing artifact and cross-relationship edge lists into the graph call; emit the 4 pre-rendered variants + 2 checkboxes; extend sr-only list with epic grouping/edge info.
- `src/SpecScribe/SiteGenerator.cs` (`BuildReferencedBy` ~1523-1539, `RenderPage` call ~1461-1489) -- resolve each citer's epic via `_epicsModel`; compute story↔related-file and related-file↔related-file overlaps from `_codeReverseMap` and the coupling pair data; pass through to `CodeFileTemplater.RenderPage`.
- `src/SpecScribe/GitMetrics.cs` (`DeepGitPulse`, `ParseNumstatLog` ~line 365+) -- expose the existing uncapped pair-count map (not just top-10 `Coupling`) so arbitrary file-pair co-change lookups are possible without a new scan.
- `src/SpecScribe/assets/specscribe.css:2566-2571` (pattern reference) -- add checkbox + sibling-combinator CSS for the 2x2 view variants and any new edge/hub visual classes (neutral tokens).
- `tests/SpecScribe.Tests/ChartsTests.cs`, `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs` -- extend with grouping/relationship coverage.

## Tasks & Acceptance

**Execution:**
- [ ] `src/SpecScribe/GitMetrics.cs` -- expose uncapped co-change pair lookup from the existing numstat parse -- lets SiteGenerator query "are R1 and R2 co-changed" without a new git call
- [ ] `src/SpecScribe/SiteGenerator.cs` -- resolve epic per citer via `_epicsModel`; compute story↔related-file and related-file↔related-file overlaps; thread into `CodeFileTemplater.RenderPage` -- supplies the new grouping/edge data
- [ ] `src/SpecScribe/Charts.cs` -- extend `ReferenceGraph` (or add a sibling method) to render epic-hub nesting and extra cross edges, producing 4 deterministic variants -- the actual new visuals
- [ ] `src/SpecScribe/CodeFileTemplater.cs` -- wire two checkboxes + 4 pre-rendered `data-view` panels; extend sr-only list -- server-side variant assembly, a11y parity
- [ ] `src/SpecScribe/assets/specscribe.css` -- checkbox/sibling-combinator CSS + any new hub/edge classes -- pure-CSS toggle, neutral styling
- [ ] `tests/SpecScribe.Tests/ChartsTests.cs`, `CodeFileTemplaterTests.cs`, `SiteGeneratorCodeInsightsTests.cs` -- cover all 4 combinations, degradation, escaping, cap interaction
- [ ] Regenerate golden content fingerprint (CSS + code-page HTML will shift)

**Acceptance Criteria:**
- Given both toggles off, when a code page renders, then the graph is byte-identical to pre-existing Story 7.8 output.
- Given "Group by epic" is on and citing stories span two epics, when the graph renders, then story nodes are nested under two distinct epic hub nodes between the center file and the stories, with non-story citers (ADRs/docs) unaffected.
- Given "Show relationships" is on and a citing story also cites a related file, when the graph renders, then an edge appears between that story's node and that related file's node, in addition to their existing center-spokes.
- Given "Show relationships" is on and two related files are also co-changed with each other, when the graph renders, then an edge appears between those two related-file nodes.
- Given `--deep-git` is off or no `FileInsight` exists, when the code page renders, then both checkboxes are present but toggling them produces no visual change (graceful degradation), and no exception is thrown.
- Given any grouping/relationship data, when rendered, then the sr-only text equivalent enumerates epic membership and cross-edges so assistive tech has the same information as sighted users.

## Spec Change Log

## Design Notes

Mirror `CodeMap.BuildVariants`: precompute all 4 `(epic × relationships)` combinations server-side per code page and switch visibility via two checkboxes using the same multi-checkbox sibling-combinator CSS idiom already proven for Code Map's 2x2 exclude filters (`specscribe.css:2566-2571`), just with `data-view="flat-flat|epic-flat|flat-rel|epic-rel"` instead of Code Map's `full|no-spec|no-tests|no-spec-no-tests`. Epic hub nodes get a distinct neutral chip (not gold, not diamond) between center and story rings. Cross edges (relationships) should visually differ from center-spokes (e.g. a lighter/thinner neutral line) so all three edge kinds (solid gold spoke, dashed related spoke, new cross edge) stay distinguishable.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests` -- expected: all green, including new grouping/relationship/degradation/escaping cases
- `dotnet run --project src/SpecScribe --deep-git` -- expected: a well-coupled, multi-epic-cited file's code page shows both checkboxes; toggling each changes the graph as described; sr-only list stays complete

**Manual checks (if no CLI):**
- Open a code page for a file cited by stories from ≥2 epics with `--deep-git` on; verify epic-hub nesting reads clearly in both light/dark and the relationship edges don't collide with existing spokes on a hub file near the artifact cap.

## Suggested Review Order

**Epic-hub grouping (the graph's new layout math)**

- Entry point — the extended signature carrying the two new opt-in populations (`refEpics`/`groupByEpic`/`crossEdges`/`relatedEdges`), all additive and defaulting to the pre-existing flat behavior.
  [`Charts.cs:1121`](../../src/SpecScribe/Charts.cs#L1121)

- One forward pass buckets each citer into its own top-level slot or a shared epic hub, in input order — deterministic, no secondary sort, non-story citers untouched.
  [`Charts.cs:1165`](../../src/SpecScribe/Charts.cs#L1165)

- Epic hub chip rendering, sized from the shortened label (mirrors the center chip) after review flagged the original hardcoded box width.
  [`Charts.cs:1322`](../../src/SpecScribe/Charts.cs#L1322)

- Where the citing artifacts + epic data get resolved once per file and threaded into the render call.
  [`SiteGenerator.cs:1604`](../../src/SpecScribe/SiteGenerator.cs#L1604)

**"Show relationships" cross edges (the new relationship data)**

- Draws the two new edge kinds (story↔related-file, related-file↔related-file) in a visually distinct dash-dot style so no edge kind reads as ambiguous.
  [`Charts.cs:1290`](../../src/SpecScribe/Charts.cs#L1290)

- Story↔related-file overlap: which citing stories also cite one of this file's co-changed neighbors.
  [`SiteGenerator.cs:1634`](../../src/SpecScribe/SiteGenerator.cs#L1634)

- Related-file↔related-file overlap: reuses the existing numstat pair tally exposed as `CoChangePairs` — no new git call.
  [`SiteGenerator.cs:1667`](../../src/SpecScribe/SiteGenerator.cs#L1667)

- The exposed, canonicalized pair lookup this all rests on.
  [`GitMetrics.cs:771`](../../src/SpecScribe/GitMetrics.cs#L771)

**Wiring + the pure-CSS toggle (no JS)**

- The 4 precomputed toggle-combination variants, rendered server-side and switched by two checkboxes.
  [`CodeFileTemplater.cs:397`](../../src/SpecScribe/CodeFileTemplater.cs#L397)

- Assembles the graph + the sr-only accessible-text equivalent (epic membership + cross-edges) for every node.
  [`CodeFileTemplater.cs:423`](../../src/SpecScribe/CodeFileTemplater.cs#L423)

- The sibling-combinator CSS driving the 4-variant show/hide, keyed on checkbox class (not id).
  [`specscribe.css:927`](../../src/SpecScribe/assets/specscribe.css#L927)

**Tests + golden fingerprint (peripheral)**

- Two-population/epic-grouping/cross-edge coverage, including the byte-identity-when-off and cap-before-bucketing cases.
  [`ChartsTests.cs`](../../tests/SpecScribe.Tests/ChartsTests.cs)

- Templater-level coverage for the sr-only equivalent and guarded links.
  [`CodeFileTemplaterTests.cs`](../../tests/SpecScribe.Tests/CodeFileTemplaterTests.cs)

- Regenerated golden content fingerprint (CSS/HTML-driven drift only).
  [`SiteGeneratorAdapterTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)
