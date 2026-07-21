---
baseline_commit: 21e41c5704d3b603434cfa8a6036fa373d380fae
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # ADR 0008 mandates Epic 22 open with this measurement spike (§Disposition); this story does NOT amend the ADR — it de-risks its build
gates: [22-2, 22-3, 22-4, 22-5, 22-6] # spike findings decide whether these proceed as scoped or are re-scoped (AC #3)
---

# Story 22.1: Spike — Incremental Recompute + IR-Delta Transport

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer evaluating whether incremental, event-driven generation is viable,
I want a measurement spike that recomputes only the changed scope and measures IR-delta transport **against this repo, decided by real numbers not argument**,
So that Epic 22's implementation stories (22.2–22.6) are scoped by evidence rather than assumption — mirroring exactly how Story 6.6 de-risked the delivery pivot before ADR 0006.

## Why this spike exists — READ FIRST

[ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) (Accepted, ratified 2026-07-20) **locked the direction**: the serialized JSON data-layer becomes the **canonical intermediate representation (IR)**; static HTML / SPA / webview become **co-equal projections** of it; and generation moves to an **incremental, event-driven model** that recomputes only the changed scope and emits **IR deltas**. But the ADR explicitly **did not schedule the build** — it seated Epic 22 as backlog and ruled that the epic **"must open with a measurement spike (mirroring Story 6.6) that de-risks incremental-recompute correctness and IR-delta transport before any implementation story."** [ADR 0008 §Disposition]

This story **is** that spike. Per ADR 0008 §Consequences, **"incremental-recompute correctness (stale/topology-change invalidation) is the primary technical risk — the reason Epic 22 is spike-gated."** The spike's single most valuable output is a defensible, numbers-backed answer to: **does recomputing only the changed scope stay correct — including AD-5's rule that topology changes (rename/delete) can force a broader refresh — or does correctness require a full-rebuild fallback?**

**This is a SPIKE — decision-first, timeboxed, throwaway (same discipline as Stories 6.3 and 6.6).** The durable deliverable is a **spike report artifact**, not shippable production code. **No production code changes ship from this story** (AC #3). The findings **gate** whether Stories 22.2–22.6 proceed as scoped or are re-scoped.

**Scope guard — three things this spike is NOT (resist all three):**
1. It is **not** the IR schema (that is Story 22.2 — this spike may sketch just enough of an IR shape to measure a delta, but does not design or version the canonical schema).
2. It is **not** the incremental regeneration *engine* (that is Story 22.5 — this spike measures whether such an engine can be correct, it does not build the production one).
3. It does **not** reopen the C#→TS core port. **ADR 0006 stands.** Charts stay pre-rendered SVG inside the IR; the byte-shrinking TS chart port is out of scope. [ADR 0008 Decision #4]

## Acceptance Criteria (spike — decision-first, throwaway, timeboxed)

Verbatim from [epics.md](../planning-artifacts/epics.md) Story 22.1, with measurement obligations made explicit:

1. **Changed-scope recompute correctness + latency measured.**
   **Given** this repo's full history and current artifact set,
   **When** the spike recomputes a single-file / single-artifact change,
   **Then** it measures and reports **recompute correctness (including AD-5 topology-change invalidation cases)** and **wall-clock latency** versus a full-regeneration baseline,
   **And** results are captured in a **spike report artifact**, mirroring Story 6.6's report.

2. **IR-delta transport measured across both change classes.**
   **Given** the spike explores an IR-delta transport,
   **When** a simulated change event is processed,
   **Then** **delta payload size and latency** are measured for **at least one topology-change scenario (rename/delete)** and **one content-only change**,
   **And** the report states **whether incremental correctness holds without a full-rebuild fallback** (and, if not, under which change classes the fallback is required).

3. **No production code ships; findings gate the epic.**
   **Given** the spike is a measurement exercise,
   **When** it completes,
   **Then** **no production code changes ship from this story** (`src/SpecScribe/**` and `tests/**` untouched; generated site byte-identical),
   **And** its findings **gate whether Stories 22.2–22.6 proceed as scoped or are re-scoped**, named explicitly in the report + Completion Notes.

## Tasks / Subtasks

- [ ] **Task 1 — Branch + baseline + quarantine** (AC: #3)
  - [ ] Work on an isolated spike branch (e.g. `spike/ir-incremental-22-1`) or worktree; do **NOT** develop on `main` (background auto-committer — memory: [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]). Confirm `baseline_commit` in frontmatter matches the HEAD you branch off (`21e41c5` at authoring time).
  - [ ] Reuse the established quarantine discipline ([spike/README.md](../../spike/README.md)): any throwaway code lives under `spike/ir-incremental/` (or similar) — nothing joins `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, or the site build. The generated site must stay byte-identical with or without the spike folder. (Note the correct rationale from the 6.6 review: quarantine holds because `SpecScribe.slnx` lists only `src/SpecScribe` + `tests/SpecScribe.Tests` and never references `spike/` — NOT because "there is no solution".)
  - [ ] A throwaway probe MAY reference `src/SpecScribe` as a project reference (like `spike/vscode/renderer` and `spike/delivery/exporter` do) to reuse the real view-model builders — this is how you measure the *real* recompute cost, not a toy. It must not modify `src/SpecScribe`.

- [ ] **Task 2 — Establish the full-regeneration baseline (the control)** (AC: #1)
  - [ ] Measure a **full `GenerateAll`** against this repo (with `--deep-git`, since git dominates gen-time): wall-clock, and the ingest-vs-git-subprocess-vs-render split. Reuse ADR 0006's already-measured figures where they still hold (**gen-time ~3.2 s dominated by ingest + the `git` subprocess, not HTML writing** — [ADR 0008 §Context], [sprint-change-proposal-2026-07-20.md:34]) rather than re-deriving them; add only what is new.
  - [ ] This is the **correctness oracle**: a full regenerate is, by definition, the coherent output. Every incremental result is judged byte-for-byte against the full-regen output of the *same* post-change source tree. (This is the same discipline the `GoldenContentFingerprint` gate enforces — memory: [[golden-diff-normalization-gotchas]].)

- [ ] **Task 3 — Axis: changed-scope recompute correctness (the primary risk)** (AC: #1)
  - [ ] Enumerate the **change classes** the spike must cover, driven by AD-5 ("watch mode may rebuild narrowly when safe, but **topology changes can trigger a broader refresh** to keep output coherent" — [ARCHITECTURE-SPINE.md:66–72](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)) and by the routes SpecScribe's watch mode *already* has:
    - **Content-only edit** of one artifact (e.g. a story body edit) → narrow rebuild via `SiteGenerator.RegenerateEpics` / `RegenerateFromDataSource` / `RegenerateAdrs`.
    - **Topology change — add** a new artifact (new story/epic/ADR) → affects nav graph, coverage tallies, prev/next pagers, breadcrumb parents.
    - **Topology change — rename** an artifact (changes its output path + every inbound link/citation/reference-graph edge).
    - **Topology change — delete** an artifact (dead links, orphaned reference-graph nodes, coverage recount, pager gaps).
  - [ ] For each class: run the existing watch-mode incremental route on the change, then run a full regenerate of the same post-change tree, and **diff the two output trees**. Report, per class: **is the incremental output byte-identical to the full-regen output?** If not, **enumerate exactly what went stale** (which pages, which cross-references). This is the correctness measurement AC #1 demands.
  - [ ] Explicitly probe the **known invalidation seams** already documented in `SiteGenerator`: the `IsDataSource` vs `IsEpicsRelated` routing (why `sprint-status.yaml` must NOT route to `RegenerateEpics`, which "by design never re-parses sprint state" — [SiteGenerator.cs:463–468](../../src/SpecScribe/SiteGenerator.cs)); the cross-artifact reference/citation graphs (`_referenceMap`, `_codeReverseMap` — a rename/delete elsewhere invalidates a page that itself did not change); coverage tallies and prev/next `EntityPager` (a topology change shifts siblings). These are the concrete places narrow rebuilds can leave stale output — the spike must show whether AD-5's "broader refresh on topology change" is *sufficient* or *insufficient* for each.

- [ ] **Task 4 — Axis: IR-delta transport (payload size + latency)** (AC: #2)
  - [ ] Sketch a **minimal IR shape** — just enough to serialize the changed scope and diff two versions (do **not** design the canonical schema; that is Story 22.2). The IR is the "serialized, durable form of AD-2's host-neutral view models plus pre-rendered SVG fragments" [ADR 0008 Decision #1] — reuse the real Story 6.2 section view models + the existing `SpaDelivery` region/chunk shape as the starting point rather than inventing one, so the measured delta reflects the real payload.
  - [ ] Process **at least two simulated change events** and measure the **delta payload size + latency** for each: **(a) one content-only change** and **(b) one topology change (rename or delete)**. Compare each delta against the cost of re-shipping the whole IR (or the whole affected chunk) — the delta is only worthwhile if it is materially smaller.
  - [ ] Feed the measurement with an honest at-scale view: reference the Story 6.6 at-scale finding — the current SPA chunker is **byte-blind** (`SpaDelivery.MaxPagesPerChunk = 75`, caps by **page count not bytes** — [SpaDelivery.cs:32–37](../../src/SpecScribe/SpaDelivery.cs)), which let `pages-root.json` reach **112.9 MB** and `code-map.html` **82.5 MB** at 1,461 pages (memory: [[story-6-6-deferred-cleanup-done-spa-at-scale-perf]]). A single-page content edit inside an over-large chunk re-ships the whole chunk — so **delta granularity vs chunk granularity is a real measurement**, and the finding directly informs whether Story 22.2's byte-bounded chunking is a hard prerequisite for a useful delta. (Fixing the chunker is 22.2's job — this spike only measures its impact on delta size.)

- [ ] **Task 5 — Write the spike report + gate the epic** (AC: #1, #2, #3)
  - [ ] Author a **spike report artifact** (see Dev Notes "Where the report lives"). Format mirrors Story 6.6's report/ADR: Context → Measured Evidence (per change class, with numbers) → Decision/Findings → Gate. Must render cleanly through SpecScribe itself if placed under a scanned path.
  - [ ] The report **must state** (AC #2): **whether incremental correctness holds without a full-rebuild fallback**, and if not, precisely which change classes require the fallback (e.g. "content-only edits are safely narrow; rename/delete require a broader refresh of the reference graph + coverage; a full-rebuild fallback is required for X").
  - [ ] The report **must name the gate** (AC #3): for each of Stories **22.2–22.6**, whether the spike's evidence lets it **proceed as scoped** or requires a **re-scope** — with the reason. (E.g. "22.5's changed-scope engine is viable for content edits; its topology-change path must be re-scoped to X"; "22.6 client-server delta channel is viable / not-yet-viable because Y".) This is the spike's contract with the rest of the epic.

- [ ] **Task 6 — Quarantine check & land only the decision** (AC: #3)
  - [ ] Confirm **no `src/SpecScribe/**/*.cs` or `tests/**` touched** (git-confirm the diff). Run the full suite — **all tests green including `GoldenContentFingerprint`** (the byte-parity gate proves the generated site is byte-identical). Read-only honored (AD-6): no spike path writes a source artifact.
  - [ ] The **only** artifacts that land on `main` are: the **spike report**, this **story record** (tasks / Dev Agent Record / Completion Notes / Change Log), the **sprint-status update** (22.1 → review → done, epic-22 in-progress), and optionally a `spike/README.md` addition documenting the quarantined probe. Throwaway probe code stays on the spike branch or quarantined under `spike/`, deletable with the branch.

## Dev Notes

### This is a SPIKE — decision-first, timeboxed, throwaway (same discipline as Stories 6.3 & 6.6)
The deliverable is a **numbers-backed spike report that gates 22.2–22.6**, not a shippable IR pipeline or regeneration engine. Build only as much throwaway code as it takes to make the correctness + delta decisions **defensible with real numbers against this repo**, then stop. **The single biggest trap is scope-creeping into an actual build** — the IR schema is 22.2, the regeneration engine is 22.5, the delta channel is 22.6. Resist all three. Spike-led de-risking of foundational moves is this project's established pattern (6.3 → ADR 0005, 6.6 → ADR 0006, Epics 11–18). Memory: [[adr-0005-reopened-delivery-arch-spike]], [[epic-4-adapter-contract-scope]].

### Ground the decision in ADR 0006/0008's own numbers (don't re-measure what's settled)
The delivery axes are **already decided** — this spike does **not** re-litigate them (memory: [[static-prerendering-vs-json-spa-architecture-question]]):
- **Gen-time is dominated by ingest + the `git` subprocess (~3.2 s), NOT HTML writing** [ADR 0008 §Context; SCP 2026-07-20:34]. This is *why* incremental recompute (not "SPA over static") is the lever — reuse this figure, don't re-derive it. It also means the spike should measure incremental latency **relative to that git-dominated baseline**: if a narrow rebuild still shells out to `git` for the whole repo, the win evaporates — measuring that is squarely in scope.
- **Inline chart SVG is 69.3 % of the dashboard body / 58.9 % of epics** [ADR 0008 §Context]. A JSON/IR layer shipping pre-rendered SVG cuts **file count, not bytes** — the byte win needs the deferred TS chart port (out of scope). So an IR **delta** that re-ships a page's chart SVGs is not small; measuring how much of a content-only delta is unchanged-SVG-mass vs actually-changed-data is a useful number.
- **The TS core port is settled: NO** (~14,200 LOC + 676 tests; Markdig fidelity + deep-git parsing are the flagged risks) [ADR 0006; ADR 0008 Decision #4]. Do not reopen it.

### The existing incremental machinery to measure against (this is the real system, not a toy)
SpecScribe's watch mode **already implements AD-5's changed-scope recomputation** — the spike measures *this*, honestly:
- **`SiteGenerator.GenerateAll`** [SiteGenerator.cs:137] — the full-regen control / correctness oracle.
- **`SiteGenerator.RegenerateEpics`** [SiteGenerator.cs:499] — narrow rebuild of the epics/story/coverage surfaces on an epics-related edit. **By design never re-parses sprint state** [SiteGenerator.cs:463–465].
- **`SiteGenerator.RegenerateFromDataSource`** [SiteGenerator.cs:593] — the non-`.md` data-source route (`sprint-status.yaml`, `_bmad/config.toml`) added in Story 6.11 precisely because those files mis-routed to `RegenerateEpics` (which skips sprint state) — a **real, shipped incremental-correctness fix** that is the perfect worked example of AD-5's invalidation subtlety. Read Story 6.11's record (memory: [[story-6-11-file-change-reactivity-live]]) — it is the canonical "narrow rebuild left output stale" case study.
- **`RegenerateAdrs`** [SiteGenerator.cs:634] — ADR refresh route.
- **Routing predicates** `IsDataSource` [SiteGenerator.cs:468] / `IsEpicsRelated` [SiteGenerator.cs:492] / `IsProjectConfigFile` [SiteGenerator.cs:475] — the decision layer that picks a route; a mis-route is a staleness bug. AD-5's "topology changes trigger broader refresh" is the escape hatch these routes rely on.
- **`FileWatcherService`** [FileWatcherService.cs] — the change-event source (filters, debounce). The spike simulates events; it need not drive the real watcher, but should respect its change-class taxonomy.
- **Cross-artifact graphs that break narrow rebuilds**: `_referenceMap` (traceability), `_codeReverseMap` (code "referenced by"), coverage tallies, and `EntityPager` prev/next — a rename/delete of artifact A can leave artifact B (which did not itself change) stale. These are the concrete topology-invalidation seams AC #1 targets.

### The IR delta shape — reuse, don't invent (schema is 22.2)
The IR is the **serialized form of AD-2's view models + pre-rendered SVG** [ADR 0008 Decision #1]. Two real seams to reuse so the measured delta reflects the production payload:
- **Story 6.2 section view models** — plain JSON-serializable data records that already round-trip losslessly through `System.Text.Json` ([SectionViewModelSerializationTests.cs](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs), the pattern ADR 0008 says 22.2 generalizes into the IR golden boundary). The 6.2 serialization gotcha applies: serialize the clean DATA records; carry chart/domain-carrier panels as pre-rendered inline-SVG strings, don't re-model them (memory: [[story-6-2-section-view-models-live]]).
- **`SpaDelivery` / `JsonSpaRenderAdapter`** — the shipped JSON+SPA form already produces `spa/manifest.json` + `spa/pages-*.json` content chunks [SpaDelivery.cs:141 `BuildDataFiles`; JsonSpaRenderAdapter.cs]. A "delta" is naturally *a diff between two versions of these files* — measure that, and measure how the **byte-blind chunker** (`MaxPagesPerChunk = 75`, page-count not byte-bounded [SpaDelivery.cs:37]) inflates a small content edit into a whole-chunk re-ship. That inflation number is the strongest single argument for Story 22.2's byte-bounded chunking.

### Architecture invariants that BOUND the spike (from the spine)
- **AD-1 / AD-2** ([ARCHITECTURE-SPINE.md:38–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one shared C# core; adapters translate view models without re-parsing `.md`. The IR is a *serialization of those same view models* — a delta probe must not re-parse sources to build the IR; it serializes what the core already produced. If the spike ever finds itself re-modelling source artifacts, it has left scope.
- **AD-5** (:66–72): changed scope is the unit of recomputation; **topology changes can trigger a broader refresh**. This is the invariant under test — the spike measures whether "narrow when safe, broader on topology change" is *correct* and names where it isn't.
- **AD-6 / read-only** (:74–80): no spike path writes source artifacts.
- **AD-8** (:90–96): interaction-state shape is shared; **transport is adapter-specific** — an IR-delta push channel is a legitimate AD-8 transport. The spike measures transport (payload/latency); it must not change interaction-state semantics.
- **NFR4** (additive, no core rewrite for new adapters), **NFR6** (JS-optional accessibility baseline — the IR keeps static HTML a co-equal projection, not a `<noscript>` afterthought), **NFR9** (reproducible CI — the byte-parity gate). [ADR 0008 §Consequences]
- **Seed, not invariant** (:98–103): package/namespace layout is a seed; the shared-core contract is the invariant. No new production project or namespace split on `main`.

### Where the report lives
Mirror Story 6.6, which produced **ADR 0006** as its durable output. **This spike does NOT author a new ADR** — ADR 0008 already made and ratified the decision; this spike de-risks the *build* of that decision. So the report is a **spike report artifact**, not an ADR. Recommended home: alongside this story record under `_bmad-output/implementation-artifacts/` (e.g. `22-1-spike-report.md`), or a `docs/` spike-report if the owner prefers it rendered as a portal page. Do **not** create `docs/adrs/0010-*.md` for this — reserve a new ADR only if the spike's findings genuinely *contradict* ADR 0008's assumptions and force an amendment (memory: [[adr-creation-trigger-gap-epic-10-retro]] — if that happens, propose the ADR explicitly rather than burying the reversal in the story record). If findings merely *scope* 22.2–22.6, the spike report + sprint-status re-plan is the right vehicle.

### Correctness measurement discipline (avoid a false "it works")
- The correctness oracle is a **full regenerate of the post-change tree**, compared byte-for-byte to the incremental output. Use the same normalization discipline the golden tests use — a diff that is only a footer clock / build-token difference is not a staleness bug (memory: [[golden-diff-normalization-gotchas]]). Confirm any "byte-identical" claim with a **repeated run** before trusting it (the stale-build first-captured-hash trap).
- Measure against **this repo with `--deep-git`** (the realistic, git-dominated case), and where possible extrapolate to Epic-7 scale (the +863-pages / ~1,060-files figure from 6.6) for the delta-vs-chunk argument — but the correctness claims must be grounded in *actual* diffs on this repo, not extrapolation.
- Output dir for any generate verification is `SpecScribeOutput` (memory: [[generate-output-dir-is-specscribeoutput]]); never `--output docs/live`.

### Project Structure Notes
- **Throwaway probe:** self-contained under a quarantined path (e.g. `spike/ir-incremental/`) with its own `.csproj` that may `<ProjectReference>` `src/SpecScribe` to reuse real builders — **not** added to `SpecScribe.slnx`, `dotnet build src/SpecScribe`, or the site build. Exists to be deleted with the branch.
- **No new production project or namespace split on `main`** (seed-level, forbidden — [ARCHITECTURE-SPINE.md:98–101]).
- **Branch/worktree, not `main`:** `main` has a background auto-committer; edits must target the worktree path if using a worktree (memory: [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]).

### References
- **The decision this spike de-risks the build of:** [ADR 0008 — JSON IR Canonical & Incremental Generation](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) (§Disposition mandates this spike; §Consequences names incremental-recompute correctness as the primary risk). Extends [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) (delivery axes settled, TS port deferred) and [ADR 0002](../../docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md) (the AD-2 view models the IR serializes).
- **The spike this one mirrors:** [6-6-delivery-architecture-and-distribution-spike.md](6-6-delivery-architecture-and-distribution-spike.md) (decision-first, throwaway, quarantine discipline, byte-parity gate, report-is-the-deliverable) + [spike/README.md](../../spike/README.md) (quarantine pattern) + [spike/delivery/](../../spike/delivery) (a probe that project-references `src/SpecScribe`).
- **The correct-course that seated Epic 22:** [sprint-change-proposal-2026-07-20.md](../planning-artifacts/sprint-change-proposal-2026-07-20.md) (evidence base at lines 30–39; spike-first gate at 89–90, 110, 177).
- **Epic + story text:** [epics.md Epic 22](../planning-artifacts/epics.md) (§3169 epic; §3192 Story 22.1 ACs; §3218 Story 22.2's byte-bounded-chunking constraint the delta measurement informs).
- **Existing incremental machinery:** [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) (`GenerateAll`:137, `RegenerateEpics`:499, `RegenerateFromDataSource`:593, `RegenerateAdrs`:634, `IsDataSource`:468, `IsEpicsRelated`:492); [FileWatcherService.cs](../../src/SpecScribe/FileWatcherService.cs); Story 6.11's incremental-correctness fix (memory: [[story-6-11-file-change-reactivity-live]]).
- **The IR delta payload seams to reuse:** [JsonSpaRenderAdapter.cs](../../src/SpecScribe/JsonSpaRenderAdapter.cs), [SpaDelivery.cs](../../src/SpecScribe/SpaDelivery.cs) (`BuildDataFiles`:141, `MaxPagesPerChunk`:37 — the byte-blind chunker), [SectionViewModelSerializationTests.cs](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs) (the lossless round-trip pattern 22.2 generalizes).
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) (AD-1/AD-2, AD-5, AD-6, AD-8, NFR4/6/9, Seed/no-split); [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md) (Delivery Adapter Layer, Evolution Sequence).
- **Memory:** [[static-prerendering-vs-json-spa-architecture-question]] (the decided architecture context + Epic 22 sequencing), [[story-6-6-deferred-cleanup-done-spa-at-scale-perf]] (the 112.9 MB / 82.5 MB byte-blind-chunker at-scale defect), [[story-6-11-file-change-reactivity-live]] (the real narrow-rebuild-staleness fix), [[golden-diff-normalization-gotchas]] (byte-parity + repeated-run discipline), [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]], [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR explicitly if findings force an ADR-0008 amendment).

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-21 — Story 22.1 drafted (create-story). Epic 22 opened (backlog → in-progress) with its ADR-0008-mandated measurement spike — the Story-6.6-style de-risking of **incremental-recompute correctness** (incl. AD-5 topology-change/rename/delete invalidation) and **IR-delta transport** (payload size + latency for one topology change + one content-only change), measured against this repo. Decision-first, throwaway, no production code ships; the durable deliverable is a **spike report** (NOT a new ADR — ADR 0008 already decided the direction) whose findings **gate** whether Stories 22.2–22.6 proceed as scoped or are re-scoped. Grounded in ADR 0006/0008's already-settled numbers (git-dominated gen-time, SVG-heavy bodies, TS port deferred); measures against the real shipped incremental machinery (`SiteGenerator.Regenerate*` routes + `IsDataSource`/`IsEpicsRelated` routing) and the byte-blind SPA chunker (`SpaDelivery.MaxPagesPerChunk=75`) whose 112.9 MB/82.5 MB at-scale defect the delta measurement informs (fixing it is 22.2's job). Does not disturb Epic 7.
