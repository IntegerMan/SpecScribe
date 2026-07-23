---
baseline_commit: 21e41c5704d3b603434cfa8a6036fa373d380fae
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # ADR 0008 mandates Epic 22 open with this measurement spike (§Disposition); this story does NOT amend the ADR — it de-risks its build
gates: [22-2, 22-3, 22-4, 22-5, 22-6] # spike findings decide whether these proceed as scoped or are re-scoped (AC #3)
---

# Story 22.1: Spike — Incremental Recompute + IR-Delta Transport

Status: review

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

- [x] **Task 1 — Branch + baseline + quarantine** (AC: #3)
  - [x] Work on an isolated spike branch (e.g. `spike/ir-incremental-22-1`) or worktree; do **NOT** develop on `main` (background auto-committer — memory: [[worktree-edits-must-target-worktree-path]], [[shared-main-concurrent-edit-loss-verify-after-edit]]). Confirm `baseline_commit` in frontmatter matches the HEAD you branch off (`21e41c5` at authoring time). **Done:** dedicated worktree `.claude/worktrees/spike-ir-incremental-22-1` on branch `spike/ir-incremental-22-1`, branched off `main` @ `b9582a4`. `baseline_commit` preserved as `21e41c5` (authoring value); immaterial since no `src/` change ships.
  - [x] Reuse the established quarantine discipline ([spike/README.md](../../spike/README.md)): any throwaway code lives under `spike/ir-incremental/` (or similar) — nothing joins `SpecScribe.slnx`, `dotnet build src/SpecScribe`, `dotnet pack`, or the site build. The generated site must stay byte-identical with or without the spike folder. **Done:** probe under `spike/ir-incremental/`; `SpecScribe.slnx` unchanged (lists only `src/SpecScribe` + `tests/SpecScribe.Tests`); `spike/.gitignore` excludes bin/obj.
  - [x] A throwaway probe MAY reference `src/SpecScribe` as a project reference (like `spike/vscode/renderer` and `spike/delivery/exporter` do) to reuse the real view-model builders — this is how you measure the *real* recompute cost, not a toy. It must not modify `src/SpecScribe`. **Done:** `SpecScribe.IrIncrementalSpike.csproj` `<ProjectReference>`s `src/SpecScribe`; drives the real `SiteGenerator`; `src/` untouched.

- [x] **Task 2 — Establish the full-regeneration baseline (the control)** (AC: #1)
  - [x] Measure a **full `GenerateAll`** against this repo (with `--deep-git`): wall-clock, and the deep-git-subprocess share. **Done + premise updated:** real repo now = 1,211 pages; full `GenerateAll` warm ≈ **31.5 s** (deep-git on) / **27.0 s** (off) → the deep-git increment is **~14.3 %** of gen-time, NOT dominant. ADR 0006/0008's "~3.2 s, git-dominated" held at 198 pages; at ~6× scale, page rendering dominates. Reported in the spike report Axis 1.
  - [x] This is the **correctness oracle**: every incremental result is judged byte-for-byte against the full-regen output of the *same* post-change source tree, using the same normalization the `GoldenContentFingerprint` gate uses (ported `NormalizeVolatile`). **Done:** determinism verified (two full generates agree on all 701 shared pages — the `content-doc` case = 0 diffs).

- [x] **Task 3 — Axis: changed-scope recompute correctness (the primary risk)** (AC: #1)
  - [x] Enumerate the **change classes** (content-only edit; topology add/rename/delete) across the routes watch mode already has (`RegenerateEpics` / `GenerateOne` / `RemoveFor` / `RegenerateAdrs` / `RegenerateFromDataSource`). **Done:** 6 change classes measured (content-story, content-doc, add-doc, delete-story, rename-doc, delete-adr) dispatched through the exact `FileWatcherService` predicate order, plus 2 **no-op controls**.
  - [x] For each class: run the incremental route, then a full regenerate of the same post-change tree, and **diff the two output trees**; enumerate exactly what went stale. **Done:** matrix in the report. **Load-bearing result:** `RegenerateEpics` diverges from the oracle **even with no source change** (56-page work-graph over-count on every epic) — the incremental-recompute correctness risk ADR 0008 named, measured.
  - [x] Explicitly probe the **known invalidation seams** (`IsDataSource`/`IsEpicsRelated` routing; `_referenceMap`/`_codeReverseMap` cross-artifact graphs; coverage tallies + `EntityPager`). **Done:** delete-adr strands the citing story page (`epics/story-9-4.html`) + ADR code-view pages + orphans a README (reference-graph + code-tree seams); every topology change strands `code-map.html`; delete-story strands `cadence.html`. Reported.

- [x] **Task 4 — Axis: IR-delta transport (payload size + latency)** (AC: #2)
  - [x] Sketch a **minimal IR shape** reusing the real `SpaDelivery` manifest + content chunks (do not design the canonical schema — that is 22.2). **Done:** IR = shipped `SpaDelivery` output (23 chunks, 48.3 MB for 1,211 pages).
  - [x] Process **≥2 change events** (one content-only, one topology) and measure delta payload size + latency vs re-shipping the whole affected chunk. **Done:** content edit → 9 chunks / **19.3 MB (39.9 % of IR)**; topology delete → 6 chunks / **12.2 MB (25.3 %)** — chunk-level delta is NOT small.
  - [x] Feed the measurement with the Story 6.6 at-scale finding (byte-blind chunker). **Done + premise corrected:** the byte-blind chunker is **already fixed** — `SpaDelivery.MaxChunkBytes = 2 MB` ships today ([SpaDelivery.cs:56,194](../../src/SpecScribe/SpaDelivery.cs)). Remaining gap: single oversized pages still exceed the cap (measured 3.08 MB chunk). This directly re-scopes 22.2 (from "add byte bounds" → "add page-level delta addressing").

- [x] **Task 5 — Write the spike report + gate the epic** (AC: #1, #2, #3)
  - [x] Author a **spike report artifact** (Context → Measured Evidence → Findings → Gate). **Done:** [`22-1-spike-report.md`](22-1-spike-report.md).
  - [x] The report **states** whether incremental correctness holds without a full-rebuild fallback. **Done:** holds for content-only generic-doc edits; does NOT hold for the epics/story family (route not oracle-faithful even at no-op) nor any topology change (cross-artifact surfaces stranded) → fallback required for those.
  - [x] The report **names the gate** for 22.2–22.6. **Done:** 22.2 proceed re-scoped; 22.3/22.4 proceed as scoped; 22.5 **re-scope required** (parity fix + topology invalidation, full-rebuild fallback until proven); 22.6 gated on 22.2's finer delta granularity.

- [x] **Task 6 — Quarantine check & land only the decision** (AC: #3)
  - [x] Confirm **no `src/SpecScribe/**/*.cs` or `tests/**` touched** (git-confirmed: only `spike/ir-incremental/` is new). Full suite run — **2162 passed / 3 skipped / 1 failed**. The one failure is `GoldenContentFingerprint`, and it is **pre-existing on `main` @ `b9582a4`, independent of this spike**: the identical actual hash `3e0d2bd3…` reproduces on a clean `main` checkout with none of these changes (stale pinned constant on this runner — memory: [[golden-diff-normalization-gotchas]]; same "moot post-merge" drift the 19.2 review noted). My work changes **zero rendered bytes** — the fixture is an isolated non-git temp tree that never scans the added report. Constant deliberately **not** bumped (unrelated rendering-baseline change).
  - [x] The **only** artifacts that land on `main`: the **spike report**, this **story record**, the **sprint-status update**, and the quarantined **probe** under `spike/ir-incremental/`. Throwaway probe deletable with the branch.

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

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- Probe: `spike/ir-incremental/` — `dotnet run --project spike/ir-incremental/SpecScribe.IrIncrementalSpike.csproj -c Release -- --repo <repo> --out <scratch>` (full matrix) and `--mode controls` (no-op controls). Emits `report.json`.
- Golden byte-parity confirmation: `dotnet test SpecScribe.slnx -c Release --filter "FullyQualifiedName~GoldenContentFingerprint"` on `main` @ `b9582a4` reproduces the same actual hash `3e0d2bd3…` — proving the failure is a pre-existing stale-constant drift, not a drift introduced by this spike.

### Completion Notes List

- **Durable deliverable:** [`22-1-spike-report.md`](22-1-spike-report.md) (spike report, NOT a new ADR — ADR 0008 already decided the direction; this spike de-risks its build). Structure mirrors Story 6.6.
- **Method:** the probe drives the REAL shipped `SiteGenerator` against a mutable copy of this repo's own artifacts. Correctness = incremental watch-route output diffed byte-for-byte against a cold full-regen oracle of the identical post-change tree, folding only the per-run/build noise the `GoldenContentFingerprint` gate folds. No `.md` re-model, no `.html` scrape (AD-1/AD-2). Determinism verified (two full generates agree on all 701 shared pages).
- **Headline finding (AC #1):** `RegenerateEpics` is NOT oracle-faithful **even with no source change** — a 56-page work-graph over-count on every epic (e.g. Epic 1: 16 items/20 links incremental vs 13/12 oracle). This is a pre-existing watch-mode fidelity gap in the shipped tool (Story 19.2 work-graph on the incremental path) and the concrete form of the risk ADR 0008 flagged as primary. Probable cause: `GenerateAll` builds `_workGraph` from source before `_docs` is populated; `RegenerateEpics` rebuilds it after `_docs`+`SyncDeferredDocFromDisk`, from a different (doubled) follow-up inventory.
- **Correctness verdict (AC #2):** holds only for content-only edits of generic docs (`GenerateOne` = byte-perfect). Does NOT hold for the epics/story family, nor any topology change — the latter strand the cross-artifact surfaces no narrow route refreshes: **Code Map, delivery cadence, reference-graph citations, ADR code-view pages**, plus orphan/prune gaps on delete. A full-rebuild fallback (or targeted invalidation + work-graph parity fix) is required for those classes.
- **Latency:** incremental is a large real win (3×–84× vs full). At current scale (1,211 pages, ~6× the ADR-0006 measurement) the deep-git increment is only ~14 % of gen-time — page rendering now dominates, updating ADR 0008's "git-dominated ~3.2 s" premise.
- **IR-delta:** the "byte-blind chunker" premise (from Story 6.6) is **stale** — `SpaDelivery.MaxChunkBytes = 2 MB` ships today. Remaining gap: single oversized pages still exceed the cap. Chunk-level delta is coarse (a 1-line edit re-ships 39.9 % of a 48 MB IR) → 22.6 needs 22.2 to deliver page-level delta addressing first.
- **Gate (AC #3):** 22.2 proceed RE-SCOPED (byte-cap done; aim at page-level delta granularity + oversized-page cap); 22.3 proceed as scoped; 22.4 proceed as scoped; **22.5 RE-SCOPE required** (fix `_workGraph` parity + add topology-change invalidation for the cross-artifact seams; full-rebuild fallback for topology/epics until proven — adopt this spike's oracle-diff harness as the acceptance test); 22.6 viable but gated on 22.2's finer delta.
- **Follow-up outside Epic 22:** the `RegenerateEpics` work-graph over-count is a live `specscribe watch` defect today (independent of the IR pivot) — worth a standalone fix or folding into 22.5's parity task.
- **AC #3 — no production code shipped:** `src/SpecScribe/**` and `tests/**` untouched (git-confirmed); probe quarantined under `spike/ir-incremental/`, not in `SpecScribe.slnx`/build/pack; site byte-identical (same golden hash before/after). Full suite: 2162 passed / 3 skipped / 1 pre-existing golden-constant failure reproduced on clean `main`.
- **`baseline_commit`:** preserved at authoring value `21e41c5` per dev-story Step 4 (worktree actually branched off `b9582a4` = current `main` HEAD); immaterial for a spike that ships no `src/` change.

### File List

- `spike/ir-incremental/SpecScribe.IrIncrementalSpike.csproj` (new — quarantined throwaway probe project, references `src/SpecScribe`, not in `SpecScribe.slnx`)
- `spike/ir-incremental/Program.cs` (new — the measurement probe: latency, correctness matrix + no-op controls, IR-delta)
- `spike/ir-incremental/README.md` (new — probe run instructions + quarantine note)
- `_bmad-output/implementation-artifacts/22-1-spike-report.md` (new — the durable spike report / epic gate)
- `_bmad-output/implementation-artifacts/22-1-spike-incremental-recompute-and-ir-delta-transport.md` (this story record: tasks, Dev Agent Record, Change Log, Status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (22-1 → review; epic-22 → in-progress)

## Change Log

- 2026-07-23 — Story 22.1 developed (dev-story). Built the quarantined measurement probe `spike/ir-incremental/` driving the real `SiteGenerator`; measured incremental-recompute correctness (6 change classes + 2 no-op controls, incremental-route output diffed byte-for-byte against a cold full-regen oracle), full-vs-incremental latency, and IR-delta payload size via the shipped `SpaDelivery` chunks. Authored the durable [spike report](22-1-spike-report.md) that gates 22.2–22.6 (22.2 re-scoped; 22.3/22.4 as scoped; 22.5 re-scope required; 22.6 gated on 22.2). Headline: `RegenerateEpics` diverges from the full-regen oracle even with no change (56-page work-graph over-count) — the primary correctness risk ADR 0008 named, measured. No production code shipped (`src`/`tests` untouched; site byte-identical). Status → review.
- 2026-07-21 — Story 22.1 drafted (create-story). Epic 22 opened (backlog → in-progress) with its ADR-0008-mandated measurement spike — the Story-6.6-style de-risking of **incremental-recompute correctness** (incl. AD-5 topology-change/rename/delete invalidation) and **IR-delta transport** (payload size + latency for one topology change + one content-only change), measured against this repo. Decision-first, throwaway, no production code ships; the durable deliverable is a **spike report** (NOT a new ADR — ADR 0008 already decided the direction) whose findings **gate** whether Stories 22.2–22.6 proceed as scoped or are re-scoped. Grounded in ADR 0006/0008's already-settled numbers (git-dominated gen-time, SVG-heavy bodies, TS port deferred); measures against the real shipped incremental machinery (`SiteGenerator.Regenerate*` routes + `IsDataSource`/`IsEpicsRelated` routing) and the byte-blind SPA chunker (`SpaDelivery.MaxPagesPerChunk=75`) whose 112.9 MB/82.5 MB at-scale defect the delta measurement informs (fixing it is 22.2's job). Does not disturb Epic 7.
