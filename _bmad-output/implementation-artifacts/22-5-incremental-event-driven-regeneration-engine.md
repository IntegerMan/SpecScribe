---
baseline_commit: 811ba17
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # §Decision 3 — "the C# core recomputes only the changed scope (operationalizing AD-5, including AD-5's rule that topology changes may trigger a broader refresh)"
gated_by: 22-4-spa-and-webview-as-ir-consumers # owner D1 2026-07-28 — 22.4's AC #5 fixes the SAME `_docs`-ordering seam; 22.5 re-measures the parity gap AFTER it lands
scoped_by: 22-1-spike-incremental-recompute-and-ir-delta-transport # the spike's gate says "RE-SCOPE (required)" and names (a)/(b)/(c); this file is that re-scope
runs_after: 22-6-client-server-delta-channel # 22.6's own owner decision D1 (2026-07-28, same day) — recompute (22.5) and transport (22.6) are ORTHOGONAL and 22.6 runs first on 22.2's hashes; see § "Story 22.6 was seeded the same day and runs first"
owner_decisions: 2026-07-28 # D1 blocked on 22.4; D2 correctness-first, emit stays whole; D3 fix parity then keep the narrow epics route, full rebuild for topology; D4 productionize the oracle harness into the test suite
---

# Story 22.5: Incremental Event-Driven Regeneration Engine

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer running `specscribe watch` on a large or actively-changing repository,
I want the watch routes to recompute the changed scope **faithfully** — matching what a full regeneration would produce, and escalating when they cannot,
So that AD-5's changed-scope principle is operationalized on a **correct** foundation instead of the measured-divergent one shipping today.

## Why this story looks different from epics.md — READ FIRST

epics.md's three ACs were written 2026-07-21, **before Story 22.1 measured the thing they assume works.** Story 22.1's gate table says, in full:

> **22.5 — Incremental event-driven regeneration engine — RE-SCOPE (required).** The measured facts forbid building the engine on the current narrow routes as-is.

**This story's 8 ACs supersede epics.md's 3.** Task 11 records that drift in `epics.md` and `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.

### This is a correctness story, not a performance story

The latency case is already won and already shipped: the spike measured the narrow routes at **3×–84× faster** than a full rebuild. What it also measured is that **the fast routes are not all faithful**, and that one of them is wrong *even when nothing changed*:

| control (route run with NO source change, vs a cold full regen) | verdict | stale pages |
|---|---|---|
| `RegenerateAdrs` (no-op) | ✅ correct | 0 |
| **`RegenerateEpics` (no-op)** | ❌ **diverges** | **56** |

`specscribe watch` shows a different, inflated work-graph than `specscribe generate` until a full restart. That is a **live defect in the shipped tool today**, independent of the IR pivot — the spike report calls it out as worth a standalone fix or a fold-in here ([`22-1-spike-report.md` § Recommended follow-up](22-1-spike-report.md)). **This story is that fold-in.**

### epics.md AC #1's "re-emitted" is NOT the emit half — owner D2

> *"…only the affected IR scope is recomputed **and re-emitted**…"*

Read literally that demands selective chunk writing. It is deliberately **not** in scope. Every incremental route already calls `EmitSpaSite` ([`SiteGenerator.cs:573`](../../src/SpecScribe/SiteGenerator.cs), `:599`, `:715`, `:788`, `:1039`), which rebuilds the **whole** manifest + every chunk from current state. Owner decision D2 keeps it that way: **AC #1 is read as *recompute*, not *emit incrementally*.** Selective emission is 22.6's, alongside the transport it exists to serve — and Story 22.2 already shipped the addressing (per-page `contentHash` + `bytes`) that 22.6 will consume.

Adding selective emission here would stack a **second** silent-staleness class (a chunk not rewritten when it should have been) on top of the recompute defect this story exists to fix.

### Story 22.6 was seeded the same day — and runs FIRST

[`22-6-client-server-delta-channel.md`](22-6-client-server-delta-channel.md) was created in a parallel session on 2026-07-28 against the same baseline. **Read its frontmatter before starting.** Its own owner decision D1 says: *"recompute (22.5) and transport (22.6) are orthogonal"* — 22.6 is `gated_by` **22.2** (the per-page `contentHash` it diffs), **not** by this story, and it declares `runs_before: 22-5`. Story 22.1's gate agrees: it named **22.2**, not 22.5, as 22.6's blocker.

Three consequences you must plan for:

1. **This story does not gate 22.6, and must not wait for it either.** They touch different halves: 22.6 diffs manifest *N* against manifest *N−1* to produce a delta; this story makes the recompute that produces manifest *N* faithful. A correct delta over a wrong recompute is a fast, reliable way to ship the wrong bytes — which is why both exist.
2. ⚠️ **22.6 adds a new file to the output tree in watch mode: `spa/delta.json`.** If 22.6 has landed, the AC #5 oracle harness will see it — and it will differ between the incremental run and the cold oracle **by construction** (a cold full generate has no previous manifest to diff against). **Handle it explicitly**, the way Trap 5 handles `diagnostics.html`. A harness that quietly folds it away is hiding a class of output; a harness that does not is red on every case.
3. **The delta document's shape is a contract 22.5 may bind to.** 22.6's story says so directly: *"Story 22.5 and any future consumer bind to these names."* If this story needs to report changed scope, consume that contract rather than inventing a second one — and **do not write a second content-hash function**: `SpaDelivery.ContentHash` is `public static` and its doc comment already names 22.5/22.6 as its consumers.

Also note 22.6 declares `conflicts_with: 22-4` (it attaches to hook sites 22.4 moves). Since this story is *gated on* 22.4, that conflict resolves in 22.4's favour before you start — but it means the 22.6 file you read may describe pre-22.4 line numbers.

### The four owner decisions locked at elicitation (2026-07-28)

| # | Decision | Consequence |
|---|---|---|
| **D1** | **22.5 is gated on Story 22.4.** 22.4's AC #5 / Task 3 fixes the *same* `_docs`-population ordering seam and shares **one** `WorkInventory` between `RenderEpicsPages`, `BuildSpaBundle` and `RenderWebviewSurfaces`. | Task 1 **re-measures** the divergence after 22.4 lands. The gap may shrink to just the `_workGraph` build at [`:247`](../../src/SpecScribe/SiteGenerator.cs), which 22.4 does **not** touch. Do not start until 22.4 is `done`. |
| **D2** | **Correctness first; `EmitSpaSite` keeps rewriting the whole IR.** | No delta transport, no selective chunk writes, no client change. 22.6 owns that. |
| **D3** | **Fix parity, then KEEP the narrow epics route.** Full rebuild escalates for **topology** changes only. | A story-file save is the dominant edit class in this repo; escalating it to a full rebuild would cost the measured ~3.4× win on the commonest operation. Prove the narrow route against the oracle instead of surrendering it. |
| **D4** | **Productionize the spike's oracle-diff harness into `tests/SpecScribe.Tests/`.** | The 56-page divergence can never come back silently. The throwaway probe at [`spike/ir-incremental/`](../../spike/ir-incremental/README.md) (477 LOC) is the design, not the deliverable. |

### What "the oracle" means here

A **full `GenerateAll` of the identical post-change source tree is, by definition, coherent output.** Every correctness claim in this story is a byte-diff of *incremental route output* against *that oracle*, folding only the volatile tokens `GoldenContentFingerprint` already folds (`NormalizeVolatile` in [`SiteGeneratorAdapterTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)). The spike proved this is a real instrument, not a tautology: two independent full generates agree byte-for-byte on all 701 shared pages, so every staleness number is signal, not normalization noise.

## Acceptance Criteria

1. **The gate is re-measured after Story 22.4, not assumed.**
   **Given** Story 22.1's numbers were taken at `b9582a4` with **deep-git OFF**, and Story 22.4's AC #5 changes the `_docs`-ordering seam that causes the divergence,
   **When** this story starts,
   **Then** the oracle-diff matrix is re-run at the current baseline **with deep-git ON**, and the story records the **re-measured** per-route stale/orphaned/missing counts beside 22.1's,
   **And** the report states explicitly which of 22.1's findings 22.4 already closed and which survive,
   **And** ⚠️ the run **verifies `git-insights.html`, `deep-analytics.html`, `impact-map.html` and `commit/*.html` are actually present** before trusting any count — memory `gitmetrics-3s-timeout-silent-deep-git-loss`: `GitMetrics`' hard-coded 3,000 ms budget silently drops all of them at `errors=0` (6,496 ms measured cold), costing 3 surfaces and ~300 pages. A matrix run that lost them would size the topology inventory from an incomplete site and call it complete.

2. **`RegenerateEpics` is oracle-faithful at no-op.**
   **Given** the measured no-op divergence — `RegenerateEpics` renders **16 items / 20 links** on Epic 1 where `GenerateAll` renders **13 / 12**, across every epic and **56 pages** — caused by `ResolveFollowUpWork` ([`:4542`](../../src/SpecScribe/SiteGenerator.cs)) preferring `_docs.Values` when populated and falling back to source conversion when it is not,
   **When** `RegenerateEpics()` runs against an **unchanged** source tree on a live generator,
   **Then** its output is **byte-identical** to a cold `GenerateAll` of the same tree under the golden normalization — `0` stale, `0` orphaned, `0` missing,
   **And** the fix names **which side is canonical** and why, in one sentence, in the Completion Notes (see Dev Notes § "Which side is right" — Story 23.3's measurement says the `_docs`-derived side is the more complete render, and Story 22.4 D3 already moved the static page toward it),
   **And** a test asserts **per-epic work-graph node and edge counts are equal** between the two paths — the spike report explicitly asks for this: *"22.5's parity fix should add a node/edge assertion to the harness so this becomes a measured, regression-guarded number."*

3. **File-level topology changes escalate; nothing is left stale or orphaned.**
   **Given** `RegenerateTopology` ([`:961`](../../src/SpecScribe/SiteGenerator.cs)) and `RegenerateFromDataSource` ([`:988`](../../src/SpecScribe/SiteGenerator.cs)) already escalate to `GenerateAll`, but a **file**-level add / rename / delete does **not** — it routes to `GenerateOne` / `RemoveFor` / `RegenerateEpics`, which strand every cross-artifact surface no narrow route refreshes,
   **When** a `.md` source file is added, renamed or deleted during watch,
   **Then** the rebuild scope escalates far enough that the resulting output is **byte-identical to the oracle**, with **no orphaned output file** surviving a delete,
   **And** the escalation decision is made in **one** named classifier, not scattered across the routes, so the rule is readable and testable in one place,
   **And** the measured stranded set is closed — at minimum, from 22.1 (deep-git OFF, **a lower bound**): `code-map.html` (add/rename/delete — *no* route re-renders it), `cadence.html` (delete-story), the reference/citation seam (`epics/story-9-4.html`, `_referenceMap` / `_codeReverseMap`), the ADR code-view pages `code/docs/adrs/*.md.html`, and the orphaned `code/docs/adrs/README.md.html`; **plus** whatever AC #1's deep-git-ON re-run adds (expected: `commit/*.html`, hotspot/coupling on `git-insights.html`, `impact-map.html`, git-derived cadence).

4. **The narrow route survives for the classes proven safe — the latency win is not surrendered.**
   **Given** owner decision D3, and the spike's measurement that `GenerateOne` on a content-only generic-doc edit is already **byte-perfect (0 diffs)**,
   **When** the engine handles a content-only edit to a generic doc, an ADR, or (after AC #2) a story/epic artifact,
   **Then** it still takes the narrow route — **no** escalation to `GenerateAll`,
   **And** each such class is **proven** byte-identical to the oracle by the AC #5 harness before it is allowed to stay narrow; a class that cannot be proven escalates instead, and the story records which classes ended up on which side,
   **And** the story reports the post-fix incremental latency per class beside 22.1's figures (`content-doc` 2.0 s, `content-story` 4.7 s, `delete-story` 4.4 s, `delete-adr` 0.19 s, `add-doc` 1.1 s, `rename-doc` 1.2 s), so a parity fix that quietly turned every route into a full rebuild is visible as a number rather than hidden behind a green test.

5. **The oracle-diff harness becomes a real, permanent test.**
   **Given** owner decision D4 and the spike gate's *"the oracle-diff harness this spike built is the acceptance test 22.5 should adopt"*,
   **When** the suite runs,
   **Then** `tests/SpecScribe.Tests/` contains a test that, per change class, drives the **shipped watch route** on a live generator using [`FileWatcherService.RunDebouncedPass`](../../src/SpecScribe/FileWatcherService.cs)'s exact fire-time predicate order (`IsDataSource → IsAdr → IsEpicsRelated → GenerateOne/RemoveFor`), then runs a cold `GenerateAll` of the identical post-change tree, and diffs the two output trees byte-for-byte under the shared `NormalizeVolatile`,
   **And** it covers at least the six spike classes — `content-doc`, `content-story`, `add-doc`, `rename-doc`, `delete-story`, `delete-adr` — plus the **`RegenerateEpics` and `RegenerateAdrs` no-op controls**, which are what caught the defect in the first place,
   **And** `NormalizeVolatile` is **shared**, not re-implemented — a second copy that folds one extra token is a hole in the gate,
   **And** ⚠️ the harness accounts for `diagnostics.html`, which Story 22.2 found echoes the configured **output root** inside its own region and is therefore the one page whose bytes are output-path dependent (see Trap 5).

6. **Scope guard: byte parity holds and nothing outside the recompute path moves.**
   **Given** NFR4 (additive) and NFR9 (reproducible CI),
   **When** the full suite runs,
   **Then** `GoldenContentFingerprint` is unchanged — a full `GenerateAll` is the **oracle**, so this story changes the *incremental* paths to match it, never the reverse,
   **And** if it does move, that is a **defect to diagnose**, not a constant to re-bless: it would mean the full-generation output changed, which is outside this story's grant (Story 22.4 AC #5 already spent the one sanctioned static-page move),
   **And** `EmitSpaSite` still rewrites the whole IR, `SpaDelivery.SchemaVersion` is **not** bumped, no delta transport ships, and no client (`web/ir/*`) changes,
   **And** any regeneration that does prove necessary follows CLAUDE.md § Verification: stable across **two repeated runs**, naming the concurrent session's changes it sat on top of.

7. **Watch mode still degrades instead of dying.**
   **Given** Story 5.3 established that every watch route runs on a `Timer` callback with **no caller**, so an escaping exception terminates the whole `watch` process rather than failing one rebuild (NFR2),
   **When** the new escalation classifier and any new invalidation work run,
   **Then** they sit **inside** the existing `RunGuarded` / `SafeNotify` / `SafeHandle` envelope ([`FileWatcherService.cs:420-472`](../../src/SpecScribe/FileWatcherService.cs)) and take `_gate` on exactly the terms the current routes do,
   **And** an escalated full rebuild reports **one** coherent `GenerationEvent` (the shape `RegenerateTopology` and `RegenerateFromDataSource` already use), not a flood of per-page events into the watch log,
   **And** a test drives the escalation path through `RunDebouncedPass` synchronously — the seam Story 5.3 added precisely so a regression surfaces as a test failure instead of taking down the test host.

8. **The architectural finding is recorded where it belongs.**
   **Given** the spike report's standing instruction — *"if 22.5's parity work reveals the narrow-route model must change architecturally, propose an ADR at that point rather than in this report"* — and CLAUDE.md's rule that a cross-cutting contract change gets an ADR rather than a note buried in a story file,
   **When** the work lands,
   **Then** the story states explicitly whether the narrow-route model changed architecturally,
   **And** if it did — in particular if a **named escalation classifier** now decides rebuild scope, which is a new cross-cutting contract — an ADR is proposed recording the classifier, the proven-narrow class list, and the escalation rule, cross-referenced from `docs/adrs/README.md`, ADR 0008 §Decision 3 and the AD-5 entry in `ARCHITECTURE-SPINE.md`,
   **And** if it did **not**, that judgement is recorded with its reasoning so the next agent does not re-litigate it.
   ⚠️ **Numbering:** `0019` is claimed-but-unwritten by Story 18.3, `0020` is pre-claimed by Story 18.5, `0021`/`0022` are written, and **`0023` is pre-claimed by Story 22.4** (which runs first). Expect `0024` — **confirm by listing `docs/adrs/` at implementation time** and expect contention on `README.md`.

## Tasks / Subtasks

**Sequence matters.** Task 1 is measurement. Without a re-measured "after 22.4" baseline, every claim below is a claim rather than a number — and 22.4 may have already closed part of this.

- [ ] **Task 0 — Confirm the gate and re-verify every line number (AC: #1).**
  - [ ] Confirm `22-4-spa-and-webview-as-ir-consumers` is `done` in `sprint-status.yaml`. **If it is not, stop and raise it** — owner decision D1 gates this story on it, and 22.4's AC #5 edits the same seam.
  - [ ] Read 22.4's Completion Notes: what shape did its shared `WorkInventory` take, and did it touch [`:247`](../../src/SpecScribe/SiteGenerator.cs)?
  - [ ] ⚠️ Every line number in this file was measured at `811ba17`. `SiteGenerator.cs` is **5,488 lines** and moves under concurrent sessions — Story 22.3's numbers were ~40 lines stale within one day. **Grep for the symbol, never trust the number.**
  - [ ] ⚠️ Read the golden fingerprint **from the file**: it is `f4a7cbac5bee0fe56aa4ef9950a114a23acc8b2d59eb2e255e4b47e27873f0cd` at [`SiteGeneratorAdapterTests.cs:1242`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) as of `811ba17` — **not** 22.4's `3171cf5c…`, not 22.3's `7adbdb01…`, not 22.2's `91c3aeb4…`. It has moved four times in this epic's lifetime.

- [ ] **Task 1 — Re-run the oracle matrix at the current baseline, deep-git ON (AC: #1).**
  - [ ] Study [`spike/ir-incremental/Program.cs`](../../spike/ir-incremental/Program.cs) (477 LOC) and its [README](../../spike/ir-incremental/README.md) — this is the design for Task 6's test, and the fastest way to reproduce the numbers.
  - [ ] Run the matrix at `811ba17`+22.4 with **deep-git ON**. Generate to `SpecScribeOutput/` (the default) — **never** `--output docs/live`.
  - [ ] ⚠️ **Before trusting any count, assert the deep-git surfaces exist** (AC #1's `GitMetrics` trap). A default generate emits **1,046** IR pages and is missing them entirely at `errors=0`.
  - [ ] Record the re-measured stale/orphaned/missing per route **beside** 22.1's figures, and mark each 22.1 finding `closed-by-22.4` / `survives`.
  - [ ] Extend the stranded-surface inventory with whatever deep-git ON adds. 22.1's list is explicitly **a lower bound** — it ran without `.git`, so per-commit pages, hotspot/coupling insights, the impact map and git-derived cadence were **structurally invisible** to its diff.

- [ ] **Task 2 — Fix `_workGraph` parity (AC: #2).**
  - [ ] Root cause, verified at `811ba17` — read Dev Notes § "The parity defect, root-caused" before touching anything. In short: `ResolveFollowUpWork` ([`:4542`](../../src/SpecScribe/SiteGenerator.cs)) does `WorkInventory.Build(_docs.Values)` **first** and only falls back to source conversion when that yields nothing. `GenerateAll` clears `_docs` at [`:214`](../../src/SpecScribe/SiteGenerator.cs) and builds `_workGraph` at [`:247`](../../src/SpecScribe/SiteGenerator.cs) → gets the **source-fallback** inventory. `RegenerateEpics` runs `SyncDeferredDocFromDisk` at [`:754`](../../src/SpecScribe/SiteGenerator.cs) and builds `_workGraph` at [`:767`](../../src/SpecScribe/SiteGenerator.cs) with `_docs` **fully populated from the prior build** → gets the docs-derived inventory. Two inventories → two `FollowUpGeometry` → two `WorkGraphBuilder.Build` results.
  - [ ] ⚠️ **Trap 1 (the nav-gate circularity) constrains the obvious fix — read it before choosing an approach.** You cannot simply move the `:247` build later.
  - [ ] ⚠️ **Trap 2 (the `alreadyExisted` flip) applies to any `_docs` pre-population approach.**
  - [ ] Whichever side you converge on, converge on **one instance**, not two equal builds. An equal-but-separately-constructed inventory is the same defect waiting to drift again — this is exactly the lesson 22.4 Task 3 wrote down.
  - [ ] Add the per-epic node/edge equality assertion the spike report asks for.

- [ ] **Task 3 — Name the escalation classifier (AC: #3, #7).**
  - [ ] One method that answers *"what scope does this change event need?"* for a classified path — today the answer is spread across `FileWatcherService.RunDebouncedPass`'s predicate ternary ([`:387-397`](../../src/SpecScribe/FileWatcherService.cs)) and the routes' own internals.
  - [ ] Classify **content change vs topology change** on the same ground-truth-at-fire-time basis the current dispatch uses (`File.Exists`, not which watcher event fired — a save emits Changed/Created/Deleted in any order before the debounce settles).
  - [ ] Keep the existing precedence exactly: `IsDataSource` is checked **first** deliberately, because `sprint-status.yaml` lives under `implementation-artifacts/` and `IsEpicsRelated` would otherwise claim it and route to `RegenerateEpics`, which by design never re-parses sprint state.
  - [ ] Reuse the escalation *mechanism* that already exists — `RegenerateTopology` ([`:961`](../../src/SpecScribe/SiteGenerator.cs)) is already "collapse `GenerateAll`'s event list to one event." Do not add a second full-rebuild path.
  - [ ] Emit **one** coherent `GenerationEvent` per escalated pass (AC #7).

- [ ] **Task 4 — Close the stranded cross-artifact surfaces (AC: #3).**
  - [ ] With Task 3's classifier in place, a file-level add/rename/delete escalates — which closes `code-map.html`, `cadence.html`, the `_referenceMap`/`_codeReverseMap` citation seam, and the ADR code-view pages **by construction** rather than by five bespoke invalidations. Prefer that; targeted per-surface invalidation was explicitly **not** the chosen posture (owner D3), because 22.1 warns its stranded list is a lower bound and a hand-maintained inventory silently rots.
  - [ ] Verify the orphan case specifically: 22.1 measured `delete-adr` leaving an **orphaned** `code/docs/adrs/README.md.html`. `GenerateAll` wipes `OutputRoot` at [`:208-211`](../../src/SpecScribe/SiteGenerator.cs), so escalation closes this — assert it, don't assume it.
  - [ ] Verify `_spaCapture` stays coherent across escalation. The existing prune helpers ([`:883`](../../src/SpecScribe/SiteGenerator.cs), `:897`, `:933-950`, `:1311`) exist precisely because the in-memory capture does not self-heal the way the output tree does.

- [ ] **Task 5 — Prove which classes may stay narrow (AC: #4).**
  - [ ] Run every change class through Task 6's harness. `content-doc` was already byte-perfect; `content-story` should become so after Task 2.
  - [ ] Record the post-fix latency per class beside 22.1's figures. **A parity fix that quietly escalated everything is a regression, and a green test will not show it.**
  - [ ] State the final class → route → scope table in the Completion Notes.

- [ ] **Task 6 — Productionize the oracle harness into the test suite (AC: #5).**
  - [ ] New test file in `tests/SpecScribe.Tests/`. Reuse the shipped `NormalizeVolatile` — do **not** re-implement it.
  - [ ] Cover the six change classes **plus** the two no-op controls (`RegenerateEpics`, `RegenerateAdrs`). The no-op control is what found the defect; it is the single highest-value assertion in this story.
  - [ ] Drive routes through `FileWatcherService.RunDebouncedPass` so the test exercises the real fire-time predicate order, not a hand-written call sequence that could drift from dispatch.
  - [ ] ⚠️ Two full generates per class is slow. Use a small fixture (the existing `SiteGeneratorAdapterTests` fixture shape is the model), **not** this repo. Budget the runtime and report it — the suite is ~2,400 tests and already carries a rotating file-write-contention flake (Story 23.2).
  - [ ] ⚠️ Handle `diagnostics.html` (Trap 5), the non-git fixture's output-feedback hazard (Trap 6), and — if Story 22.6 has landed — `spa/delta.json`, which differs between the incremental run and the cold oracle by construction.

- [ ] **Task 7 — Full-suite + golden verification (AC: #6).**
  - [ ] `dotnet test SpecScribe.slnx`. `GoldenContentFingerprint` **unchanged** is the expected result — read the constant from the file first (Task 0).
  - [ ] Under `web/`: `npm run test`, `npm run check:a11y`, `npm run check:links`, `npm run check:ir-content`. These should be untouched by this story; a failure means scope leaked.
  - [ ] Confirm `SpaDelivery.SchemaVersion` is still `1` and `EXPECTED_SCHEMA_VERSION` in `web/ir/adapter.ts` / `adapter.client.ts` is unchanged.

- [ ] **Task 8 — Live watch-mode verification (AC: #2, #3, #4).**
  - [ ] Per CLAUDE.md § Verification, the suite structurally cannot see everything. Run `specscribe watch` against this repo, make one edit of each class, and **look at the rendered pages** — the work-graph counts on an epic page are the specific thing to read, since that is where the 56-page divergence lives.
  - [ ] Confirm the watch log reads sensibly on an escalated pass (one event, honest label) — the `<directory change>` label convention is the precedent.

- [ ] **Task 9 — Update the deferred-work entries this story closes (AC: #3).**
  - [ ] [`deferred-work.md`](deferred-work.md) carries a `_workGraph`-staleness entry from the Story 20.1 review that explicitly defers to this story. Close it or restate it.
  - [ ] The `ChunkDelta` under-count entry from the 22.1 review is **22.6's**, not this story's — leave it.

- [ ] **Task 10 — Propose the ADR, or record why not (AC: #8).** Confirm the number by listing `docs/adrs/` (expect `0024`).

- [ ] **Task 11 — Record the AC drift in `epics.md` AND `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records). Include the four owner decisions and the fact that epics.md AC #1's "re-emitted" half is 22.6's by decision D2, so a later reader does not treat 22.6 as duplicating this story.

## Dev Notes

### The parity defect, root-caused at `811ba17`

The single mechanism behind all 56 pages:

```
ResolveFollowUpWork(files)                          // SiteGenerator.cs:4542
  var fromDocs = WorkInventory.Build(_docs.Values.ToList());   // :4544  ← prefers _docs
  deferred = fromDocs.Deferred ?? TryConvertDeferredDoc(files) // :4546  ← source fallback
  quickDev = fromDocs.QuickDev.Count > 0 ? … : ConvertQuickDevFromSource(files)  // :4554
```

Its own doc comment states the intent plainly: *"Uses the populated `_docs` when available; otherwise (e.g. during `RenderEpicsPages`, before the pages loop fills `_docs`) locates and converts `deferred-work.md` and open `route: one-shot` specs from source read-only."* The fallback was written as a **degradation**. In `RegenerateEpics` it is not the fallback that runs — it is the primary — and nobody checked that the two produce the same answer.

| | `GenerateAll` (the oracle) | `RegenerateEpics` (the narrow route) |
|---|---|---|
| `_docs` state | **cleared** at [`:214`](../../src/SpecScribe/SiteGenerator.cs) | **fully populated** from the prior build |
| deferred sync | none before the graph build | `SyncDeferredDocFromDisk(files)` at [`:754`](../../src/SpecScribe/SiteGenerator.cs) |
| `_workGraph` built at | [`:247`](../../src/SpecScribe/SiteGenerator.cs) — before nav | [`:767`](../../src/SpecScribe/SiteGenerator.cs) — after re-ingest |
| inventory it sees | source-converted fallback | `_docs`-derived |
| Epic 1 result | **13 items / 12 links** | **16 items / 20 links** |

Both call the same `BuildWorkGraphModel` ([`:3726`](../../src/SpecScribe/SiteGenerator.cs)) → `ResolveFollowUpWork` → `FollowUpGeometry.From` → `WorkGraphBuilder.Build`. **The function is not the bug; its input is.**

### Which side is right

**The `_docs`-derived side.** Three independent lines of evidence agree:

1. Story 23.3 measured the same class of divergence across 46 surfaces and named it: *"the IR is the more complete render, so this is a latent defect in the static page, not a loss in the capture."*
2. Story 22.4's owner decision **D3** acted on that — *"the STATIC page moves to converge the 46-delta"* — i.e. the full-generation side was already judged the stale one for the follow-up geometry.
3. The source-conversion fallback is structurally lossier: `ConvertQuickDevFromSource` ([`:4567`](../../src/SpecScribe/SiteGenerator.cs)) re-reads and re-converts markdown from disk with its own `try`/`catch (IOException) { }` swallow, while `WorkInventory.Build(_docs.Values)` reads models the pipeline already built.

**But do not read that as "make `GenerateAll` match `RegenerateEpics`" without reading Trap 1.** And note the counts move the *other* way from what "more complete" suggests — 16/20 vs 13/12 is the *incremental* side being **larger**, which the spike calls an **over-count**, not a richer render. Resolving that apparent contradiction — is the docs-derived inventory more complete, or is it double-counting because `SyncDeferredDocFromDisk` ran on top of an already-populated `_docs`? — **is the first analytical task of the fix.** Task 1's re-measure after 22.4 is the instrument; do not choose a side before running it.

### Trap 1 — the nav-gate circularity (why you cannot just move the `:247` build later)

`_workGraph` is built **before** nav on purpose:

```
:247   _workGraph = BuildWorkGraphModel(bundle.Epics, progress, bundle.Requirements, files);
:248   var hasWorkGraph = !_workGraph.IsEmpty;
:265   var nav = SiteNav.Build(…, hasWorkGraph: hasWorkGraph, …);
:277   _nav = nav;
       … every page render consumes `nav` …
:409   foreach (var file in pageFiles) { events.Add(GenerateOneInternal(file, nav)); }   // ← _docs filled HERE (:3338)
```

The comment at `:244-246` states the invariant: *"Project the epic-scoped work graph now — BEFORE nav — so the Insights entry and the page write share one gate (a non-empty model)."* An empty model must yield **no nav entry and no page**; a nav entry pointing at a page that was never written is the exact dangling-link class Story 5.3 spent a whole story closing.

So the dependency really is circular: **nav needs `hasWorkGraph` → `hasWorkGraph` needs `_workGraph` → the docs-derived `_workGraph` needs `_docs` → `_docs` needs the pages loop → the pages loop needs nav.** Three routes out, in rough order of preference:

1. **Break the gate from the model.** Derive `hasWorkGraph` from a cheaper predicate that does not need the full geometry, keep the model build after `_docs`. Needs proof the cheap gate and `!_workGraph.IsEmpty` can never disagree, or the dangling-link class returns.
2. **Pre-populate `_docs` before nav** — but see Trap 2, and note this is the approach 22.4 Task 3 was steered away from for the same reason.
3. **Converge the other way**: make `RegenerateEpics` reproduce the source-fallback inventory the oracle sees. Simplest and lowest-risk, but it converges on the side 23.3 and 22.4 D3 both judged stale — so it would need an explicit owner-visible justification, and would put 22.4's static-page move and this fix in tension.

**Whichever route: `hasWorkGraph`, the page write, and the nav entry must keep sharing exactly one gate.**

### Trap 2 — the `alreadyExisted` flip (inherited from 22.4)

```csharp
var alreadyExisted = _docs.ContainsKey(relative);        // SiteGenerator.cs:3331
…
_docs[relative] = doc;                                    // :3338
var outcome = alreadyExisted ? GenerationOutcome.Updated : GenerationOutcome.Generated;
```

**Any** pre-population of `_docs` before the pages loop turns every `Generated` diagnostic into `Updated`. That moves the golden fingerprint for a reason entirely unrelated to the work inventory, and makes any page-by-page delta enumeration unreadable. Diagnostics event ordering is load-bearing for the fingerprint. If you pre-populate, thread the "was this a fresh write" signal separately rather than inferring it from dictionary membership.

### Trap 3 — `EmitSpaSite` is already called on every route (and stays that way)

`:554` (full), `:573` (`GenerateOne`), `:599` (`RemoveFor`), `:715` + `:788` (`RegenerateEpics`, both branches), `:1039` (`RegenerateAdrs`). Each rebuilds the **entire** manifest and every chunk. Under owner D2 that is unchanged — but it means **the IR inherits every recompute defect verbatim**, which is why fixing the recompute *is* fixing the IR here. It also means a full IR rewrite is not evidence the IR is correct.

### Trap 4 — `RegenerateEpics` already has its own bespoke topology teardown

The `ingest.SourceFullPath is null` branch ([`:683-731`](../../src/SpecScribe/SiteGenerator.cs)) handles *"epics.md was deleted or renamed away"* with a hand-written teardown — `ClearEpicsFamilyOutputs()` ([`:835`](../../src/SpecScribe/SiteGenerator.cs)) drops six models, deletes the six `EpicsFamilyPages` and the `epics/`+`requirements/` directories, rebuilds nav so the entries vanish, then **deliberately re-renders** `sprint.html` and `action-items.html` because they carried live links into the pages it just deleted.

This is Story 5.3 AC #3, and it is a **correct, deliberate, tested** behaviour ([`SiteGeneratorEpicsRemovalTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs), 8 tests) with a distinct reporting contract: it returns `Removed` with a page count only when it actually tore something down, so a project that simply never had an `epics.md` keeps reporting the long-standing `Skipped` no-op.

**A new escalation classifier must not swallow this branch.** Two failure modes, both easy to land:

- Escalating `epics.md`-deleted to `GenerateAll` would produce coherent output but **change the reported event** from `Removed`/`Skipped` to `Updated`, breaking those tests and the watch log's honesty.
- Leaving it alone while escalating *other* file-level deletes means two teardown paths with different rules — exactly the drift this story exists to end.

Decide explicitly which it is, and say so in the Completion Notes. Note that `_progress` here is **split, not nulled** — the epics-derived roll-ups go, the git-derived pulse stays — because nulling the git half would strip nav entries and orphan pages that are still valid. That asymmetry is load-bearing; do not "simplify" it.

### Trap 5 — `diagnostics.html` is output-path dependent

Story 22.2 ran this down: `diagnostics.html` echoes the configured **output root** inside its own region, so it is the one page whose `contentHash` differs machine-to-machine on identical input. The oracle harness runs the incremental route and the oracle into **two different output directories** — so this page will diff on every class unless handled. Story 22.2 also names a second, related trap it hit: *"my own harness using two different output dirs"* produced a false alarm. Read [`22-2-canonical-ir-schema-and-versioning.md`](22-2-canonical-ir-schema-and-versioning.md) § hash volatility before debugging a mysterious one-page diff.

### Trap 6 — a non-git fixture feeds its own output back in

Also from 22.2, found in passing: on a **non-git** fixture, `FallbackCodeWalk` skips dot-dirs / `bin` / `obj` / `node_modules` but **not the output dir** — so a nested output directory feeds run 1's HTML into run 2's code-map. The oracle harness runs two generates; if the fixture is not a git checkout and the output nests under the source, `code-map.html` will diff for reasons that have nothing to do with incremental correctness.

### Watch-mode safety envelope (Story 5.3) — do not step outside it

Every route runs on a `Timer` callback with **no caller**. Story 5.3's whole point: an unhandled exception there does not fail one rebuild, it **terminates the watch process**. Three guards, all in [`FileWatcherService.cs`](../../src/SpecScribe/FileWatcherService.cs), and new code must sit inside them:

| guard | line | what it catches |
|---|---|---|
| `RunGuarded` | `:420` | a throwing generator route → `Error` event |
| `SafeNotify` | `:444` | a throwing **reporter** (closed stdout) on the *success* path |
| `SafeHandle` | `:462` | a throw in the raw watcher event handler body |

And the memory `story-5-3-watch-safety-done` records the lesson that generalizes here: **a method that locks internally is not automatically safe for a new caller.** `GenerateAll` takes `_gate` itself, which is why `RegenerateTopology` ([`:964`](../../src/SpecScribe/SiteGenerator.cs)) deliberately takes **no** outer lock. A new escalation path that wraps `GenerateAll` inside `lock (_gate)` deadlocks the watch loop.

`RunDebouncedPass` and `RunTopologyPass` are `internal` **as test seams**, precisely so a regression surfaces as an ordinary test failure instead of taking down the whole test host. Use them (AC #7).

### Existing test coverage — and the hole

| file | tests | covers |
|---|---|---|
| [`FileWatcherServiceTests.cs`](../../tests/SpecScribe.Tests/FileWatcherServiceTests.cs) | 8 | dispatch, debounce, dynamic `_bmad` registration |
| [`FileWatcherServiceCrashGuardTests.cs`](../../tests/SpecScribe.Tests/FileWatcherServiceCrashGuardTests.cs) | — | the three guards above |
| [`SiteGeneratorEpicsRemovalTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs) | 8 | Story 5.3 AC #3 epics-family teardown |
| [`SiteGeneratorDataSourceTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorDataSourceTests.cs) | — | `RegenerateFromDataSource` |

**There is no test anywhere in the suite that compares an incremental route's output to a full regeneration.** That is exactly why a 56-page divergence shipped and stayed shipped. AC #5 closes it.

### Scope guards — what this story is NOT

1. **Not the delta channel.** No transport, no push, no long-lived server, no client consumption of a delta. That is Story 22.6 — seeded the same day, gated on 22.2 rather than on this story, and running first. Owner D2.
2. **Not selective IR emission.** `EmitSpaSite` keeps rewriting the whole manifest and every chunk. Do not "optimize" it while you are in there — see Trap 3 for why that would be a *second* staleness class, not a bonus.
3. **Not the static-page render.** Story 22.4 AC #5 already spent the one sanctioned static-page byte move. `GenerateAll` is this story's **oracle**; changing it changes the measuring instrument. If you believe full generation is wrong, that is a finding to record (AC #8), not a change to make.
4. **Not the region seam.** `SpaDelivery.ExtractContentRegion`, `ExtractNavMarkup`, `_spaCapture` and `CapturedNavMarkup` are 22.4's, and remain the IR's producer for ~853 pages until Story 23.4. Touch them only where an *invalidation* requires it, and say so.
5. **Not a schema change.** `SpaDelivery.SchemaVersion` stays at its current value; `EXPECTED_SCHEMA_VERSION` in `web/ir/adapter.ts` and `adapter.client.ts` stays put.
6. **Not a Nuxt/`web/` story.** Nothing under `web/` should change.
7. **Not a new dependency.** This is `net10.0` + xunit, using symbols that already exist. There is no library to add, no package to choose, and no version question — if you find yourself reaching for one, the design went wrong. The one "new" artifact is a test file whose reference implementation is already written at [`spike/ir-incremental/Program.cs`](../../spike/ir-incremental/Program.cs).

### Project Structure Notes

- All production changes land in `src/SpecScribe/SiteGenerator.cs` and `src/SpecScribe/FileWatcherService.cs`. No new project, no new adapter.
- The test harness is new in `tests/SpecScribe.Tests/`. `spike/ir-incremental/` stays quarantined and untouched — it is not in `SpecScribe.slnx` and contributes no shipped code path. Do not delete it until the productionized test is green; it is the reference implementation.
- Nothing under `web/` should change. If it does, scope leaked into 22.6 or 23.4.
- Generate to `SpecScribeOutput/` (the default). **Never** `--output docs/live` — vestigial and gitignored.

### Concurrent-work discipline (CLAUDE.md)

Another agent may be editing `SiteGenerator.cs` right now, and 22.4 will have just landed in it.

- **Grep-verify every symbol you add** before relying on it. A `Charts.cs` edit has silently vanished this way.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.**
- Expect the golden fingerprint to move under you for reasons that are not yours. Confirm any regenerated hash is stable across **two repeated runs** and say in the story record whose changes it sat on top of.

### References

- **The gate that re-scoped this story:** [`22-1-spike-report.md`](22-1-spike-report.md) — § Gate for Stories 22.2–22.6 (the 22.5 row states (a) parity, (b) topology invalidation, (c) full-rebuild fallback), § Axis 2 (the correctness matrix + no-op controls), § Recommended follow-up outside Epic 22.
- **The story record with the same findings:** [`22-1-spike-incremental-recompute-and-ir-delta-transport.md`](22-1-spike-incremental-recompute-and-ir-delta-transport.md).
- **The decision being operationalized:** [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) §Decision 3, and its §Consequences naming incremental-recompute correctness as *the primary technical risk*.
- **The invariant:** `ARCHITECTURE-SPINE.md` § AD-5 — *"watch mode may rebuild narrowly when safe, but topology changes can trigger a broader refresh to keep output coherent."*
- **The gating story:** [`22-4-spa-and-webview-as-ir-consumers.md`](22-4-spa-and-webview-as-ir-consumers.md) — AC #5 and Task 3 (the shared `WorkInventory`), Trap 2 (`alreadyExisted`), and its scope guard #1 which explicitly hands `RegenerateEpics`' non-oracle-faithfulness to **this** story.
- **The IR as shipped, and its addressing:** [`22-2-canonical-ir-schema-and-versioning.md`](22-2-canonical-ir-schema-and-versioning.md) — `schemaVersion`, per-page `contentHash` + `bytes` (built *for* 22.5/22.6), `oversizedPages`, and the `diagnostics.html` hash-volatility caveat.
- **The watch-safety contract:** [`5-3-watch-regeneration-safety-and-scope-aware-rebuilds.md`](5-3-watch-regeneration-safety-and-scope-aware-rebuilds.md) + `FileWatcherService.cs` doc comments; memory `story-5-3-watch-safety-done`.
- **Deep-git hazard:** memory `gitmetrics-3s-timeout-silent-deep-git-loss`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
