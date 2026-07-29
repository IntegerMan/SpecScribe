# ADR 0027: Watch-Mode Rebuild Scope Is Decided by One Named Classifier, and Topology Escalates

**Status:** Proposed (authored 2026-07-28; ratification is the owner's)
**Date:** 2026-07-28
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0008 — JSON Data-Layer as Canonical IR & Incremental, Event-Driven Generation](0008-json-ir-canonical-and-incremental-generation.md) (**this ADR operationalizes its §Decision 3**, and answers the incremental-recompute correctness risk its §Consequences named as primary); `ARCHITECTURE-SPINE.md` § **AD-5** (the invariant this makes executable); [ADR 0024 — SPA and Webview Are Filtered Projections of One Region Seam](0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) (whose AC #5 fix closed the work-graph half of the same measurement); Stories 22.1 (measured the gap), 22.4 (closed the parity half), 22.5 (this ADR)

**Numbering note.** `docs/adrs/` ends at **0026** on disk, verified by directory listing at authoring time rather than assumed. **`0019` remains claimed-but-unwritten** by Story 18.3 (per [ADR 0023](0023-agent-facing-analysis-observation-contract.md)'s numbering note) and **`0020` is written**. `0027` is the first uncontested slot. Story 22.5's own file predicted `0024`; that slot was taken by Story 22.4 the same day.

**Evidence.** Every claim below is a byte-diff of incremental watch output against a full `GenerateAll` of the identical post-change source tree, folded through the normalization the `GoldenContentFingerprint` gate already uses. The harness is `tests/SpecScribe.Tests/IncrementalOracleParityTests.cs` and runs on every suite run.

## Context

`ARCHITECTURE-SPINE.md` § AD-5 says watch mode "may rebuild narrowly when safe, but topology changes can trigger a broader refresh to keep output coherent." ADR 0008 §Decision 3 restates it as "the C# core recomputes only the changed scope."

**Nothing in the code ever decided "when safe."** `FileWatcherService.RunDebouncedPass` picked a route from the changed path's *family* — data source, ADR, epics-related, or generic — and each route then rebuilt whatever it happened to own. The scope question was answered implicitly, in five places, by omission. There was also no test anywhere in the suite comparing an incremental route's output to a full regeneration, so the omission was unobservable.

Story 22.1 measured it with a throwaway probe and found the narrow routes 3×–84× faster than a full rebuild — and not all faithful. Its headline was a `RegenerateEpics` divergence visible **even with no source change at all**: 56 stale pages, an inflated work graph on every epic page. That half was closed by Story 22.4's AC #5 (one shared resolver-href map), which this ADR's harness re-measured and confirmed: both no-op controls are now byte-identical to a cold generate.

What survived is the part AD-5 actually names. Re-measured against the oracle at Story 22.5, on a fixture built specifically to exercise the paths (a `deferred-work.md` carrying resolver refs, a `route: one-shot` spec):

| change class | route it took | divergence from the oracle |
|---|---|---|
| content edit, generic doc | `GenerateOne` | `code-map.html` |
| content edit, story artifact | `RegenerateEpics` | `code-map.html` |
| add doc | `GenerateOne` | `code-map.html` |
| rename doc | `RemoveFor` + `GenerateOne` | `code-map.html` |
| delete story artifact | `RegenerateEpics` | `code-map.html`, `sprint.html` |
| delete ADR | `RegenerateAdrs` | `code-map.html` |

Three findings matter more than the individual rows.

**1. The stranded set was never a topology-only problem.** Story 22.1 listed `code-map.html` under add/rename/delete and warned its list was a lower bound (it ran without `.git`, so per-commit pages, hotspot/coupling insights and the impact map were structurally invisible to it). The bound was hiding a *content* case: the Code Map is a treemap of the source walk, and the walk carries each file's **line count** — which sizes every cell and is stated in the page's own subtitle. Editing one tracked file, changing nothing else, is already enough to make the cached page wrong.

**2. Escalation alone does not produce a coherent site, because `GenerateAll` was not idempotent on a reused generator.** Watch mode holds one `SiteGenerator` for the session, so an escalated rebuild is `GenerateAll` called an n-th time on an instance that already has models; a cold `generate` starts from nulls. `GenerateAll` cleared `_docs` but not `_epicsModel`, `_requirements`, `_cadence`, `_counts`, `_progress`, `_referenceMap`, or the `_artifactHrefByRepoRel` cache whose own comment claimed it was "built once per generation run" with nothing enforcing the per-run part. Deleting `epics.md` and escalating still left `cadence.html` and `traceability.html` **orphaned** — written from a model whose source no longer existed — and the Code Map still linking story artifacts to `epics/story-N-M.html` pages that were gone. This class was invisible to every existing test because they all build a fresh generator.

**3. A bespoke teardown that is correct is not automatically oracle-faithful.** Story 5.3 AC #3 gave `epics.md`-deleted its own tested teardown with a more honest reporting contract than a full rebuild has. Exempting it from escalation was tried first, then diffed: **16 stale, 3 missing**. The missing pages are the point — once `epics.md` is gone the story artifacts stop being consumed by the epics family and a full rebuild renders them as ordinary docs, which no teardown *of the epics family* could produce.

## Decision

### 1. One named classifier answers scope, before the family question

`SiteGenerator.ClassifyRebuildScope(string) → RebuildScope` is the single place the rebuild-scope rule lives. `FileWatcherService.RunDebouncedPass` consults it **before** dispatching on family, because which family a path belongs to is not the deciding fact — a topology change strands surfaces no family route re-renders.

The rule, in full:

> A change is **topology** when the file's existence at fire time disagrees with whether the last completed pass rendered it. Everything else is **content**.

Deliberately one rule rather than one per family: "did the page set change" is the question every stranded surface actually turns on, and a family-shaped rule needs re-deciding each time a family is added.

Ground truth at fire time, never the watcher event kind — the discipline `RunDebouncedPass` already applied, because one editor save emits Changed/Created/Deleted in any order before the debounce settles.

`IsDataSource` keeps its precedence, unchanged and first: `sprint-status.yaml` lives under `implementation-artifacts/` and would otherwise be claimed by `IsEpicsRelated` and routed to `RegenerateEpics`, which by design never re-parses sprint state.

### 2. Topology escalates through the mechanism that already exists

`RebuildScope.Full` dispatches to `SiteGenerator.RegenerateTopology`, which is already "collapse `GenerateAll`'s event list to one event" — so the escalated pass reports **one coherent `GenerationEvent`**, not a flood of per-page events into the watch log. No second full-rebuild path is introduced, and nothing wraps it in an outer lock (`GenerateAll` takes `_gate` itself).

**Escalation rather than per-surface invalidation is the point of the decision, not an implementation detail.** A hand-maintained "these surfaces need refreshing when X changes" table would have started incomplete — Story 22.1's list is explicitly a lower bound — and rotted silently from there, exactly as the original omission did. A full rebuild wipes the output root, so there is nothing left to enumerate and nothing to keep in sync.

### 3. A full rebuild renders from source alone

`GenerateAll` drops every model derived from a previous pass before rebuilding (`ResetDerivedStateForFullRebuild`). This does not contradict the partial-failure caching rule that keeps the last good models when a mid-edit save fails to parse: that rule protects the **incremental** routes, which leave the rest of the tree in place and need the cached models to keep linkifying against it. `GenerateAll` has already deleted the output root, so a retained stale model can only write a page whose source is gone.

On a fresh generator every reset field is already at the value assigned, so a cold `generate` — and `GoldenContentFingerprint` — cannot move. This narrows only what a second pass on a live generator inherits.

### 4. A narrow route must refresh every whole-tree surface that content can move

Staying narrow is not permission to skip a surface. `code-map.html` and `risk-quadrant.html` are projected from the source walk, and the walk's line counts move on an ordinary save, so the three narrow routes re-walk and rewrite them (`RefreshCodeSurfaces`) — the narrow counterpart of the existing `RefreshCoverage`.

Escalating instead would have been the smaller change and the wrong one: a save is the dominant edit class in a live session, and rebuilding the whole site on every save is precisely the cost the narrow routes exist to avoid.

### 5. A class may stay narrow only if it is *proven* byte-identical

`tests/SpecScribe.Tests/IncrementalOracleParityTests.cs` drives the shipped watch dispatch through `FileWatcherService.RunDebouncedPass` for six change classes plus two no-op controls, then diffs against a cold `GenerateAll` of the identical tree. A class that cannot be proven escalates instead. The normalization is shared with `GoldenContentFingerprint` (`GoldenNormalization`) — a second copy folding one extra token is a hole in whichever gate lacks it, and Story 22.1's private copy had already drifted.

## Consequences

**Deleting `epics.md` now reports `<directory change>` instead of "epics.md removed; N stale page(s) deleted".** A real loss of log honesty, accepted because the alternative was 16 stale and 3 missing pages. `ClearEpicsFamilyOutputs` and its 8 tests are untouched and still reachable through the public `RegenerateEpics` API, which is how those tests drive it; only what the **watch dispatch** selects has narrowed.

**Topology changes cost a full rebuild.** This is the AD-5 trade taken deliberately. Content edits — the dominant class — keep the narrow route and its measured win, now paying one additional source walk for the code surfaces.

**The narrow/full split is now a testable contract rather than an emergent behaviour.** Adding a surface projected from the whole tree means either making the narrow routes refresh it or letting the harness turn red; adding a new artifact family inherits the existing rule with no new decision.

**`RebuildScope` is public API.** It is the vocabulary a future scope decision extends. A third value (e.g. a family-scoped middle tier) is a change to this ADR, not a local edit.

## Alternatives considered

**Per-surface invalidation** — refresh exactly the stranded surfaces on each change class. Rejected under owner decision D3's reasoning: the stranded list is a measured lower bound, a hand-maintained inventory rots silently, and the failure mode is a stale page nobody notices rather than a loud error.

**Escalate everything.** Correct, and it surrenders the entire 3×–84× incremental win on the commonest operation in a watch session.

**Converge `GenerateAll` onto the incremental routes' behaviour** instead of the reverse. Rejected on the ground that makes the whole measurement possible: a full generate of the post-change tree is coherent *by construction*, which is what qualifies it as the oracle. Changing it changes the measuring instrument.
