# Story 17.3: Performance and Efficiency Pass

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- created 2026-08-07 (create-story 17.3) at baseline_commit 15336f4. Every line number in this file was
     resolved at that revision and WILL drift — re-resolve by SYMBOL, never by `:NNN`. -->

**baseline_commit:** `15336f4` (`Merge branch 'worktree-code-review-23-2-fourth-pass'`)

## Story

As a user running SpecScribe on a real, sometimes-large repository,
I want the known performance and efficiency debts addressed before release,
So that generation and the live webview stay responsive at realistic scale.

## Acceptance Criteria

Reproduced verbatim from `epics.md` § Epic 17 → Story 17.3. **Read the ⚠ table under AC #1 before acting on any
of its four named examples** — three are closed and the fourth is mostly closed.

1.
**Given** the performance debts recorded across the feature epics
**When** the efficiency pass runs
**Then** the highest-impact items are addressed or explicitly accepted with rationale — the webview's full-site re-render per change (ADR 0005 §3 scoped re-render / warm-renderer follow-up), unbounded git-log/heatmap payloads on mature repos, redundant per-fragment renderer-swap scans, and missing recursion-depth guards on the tree/treemap renderers
**And** baseline generation performance (NFR1) is measured before and after, with deep analytics still separated from baseline runs.

> ⚠ **Three of AC #1's four named items are CLOSED at HEAD, and the fourth is ~80 % closed.**
> Verified by reading the code at `15336f4`, not by trusting the ledger:
>
> | AC #1 says | Actual state at HEAD |
> |---|---|
> | "the webview's full-site re-render per change (ADR 0005 §3)" | **Mostly closed by Epic 22.** Story 22.5 (`done`) shipped the incremental regeneration engine + `ClassifyRebuildScope` (ADR 0027); Story 22.6 (`done`) shipped `specscribe webview --serve --serve-delta`, and `extension.ts` folds `DeltaFrame`s onto the cached payload (`applyDeltaFrame`, `extension.ts:219`). The live-push comment at `extension.ts:847` cites *"AD-8, ADR 0005 §3"* by name. **Measured**: a one-file edit ships **2.72 % of the IR / 4.09 % of the webview payload**, stable across two runs. **Two residuals survive** — see § A. |
> | "unbounded git-log/heatmap payloads on mature repos" | **Bounded — and not where the AC implies.** `GitMetrics` caps both fetches: `log -M --name-only --pretty=format: -n 200` (`GitMetrics.cs:459`) and `log --numstat … -n 300` (`:631`). `CommitHeatmap`/`DeliveryCadenceHeatmap` are calendar-windowed (~15-week floor, never past today). The real at-scale defect is a **byte** problem elsewhere, not a commit-count problem — see § B. |
> | "redundant per-fragment renderer-swap scans" | **Closed 2026-07-18** (`spec-epic2-deferred-debt-cleanup`, `deferred-work.md:719`). `UseMermaidCodeBlocks`/`UseCommentAnnotations` now install **once** via `DocumentRendererWrappersExtension : IMarkdownExtension` on the static `Pipeline` (`MarkdownConverter.cs:27`, `:101-112`), not per `RenderInline`/`RenderBlock` call. |
> | "missing recursion-depth guards on the tree/treemap renderers" | **Closed — all three recursive emitters are capped.** `CodeMap.cs:421` `MaxDepth = 32` (guard `:571`); `CodeMapTemplater.cs:588` `MaxTreeDepth = 12` (guard `:411`); `HierarchyExplorer.cs:1047` twin emitter caps at `depth < 12` **and** carries a `visiting` cycle set. |
>
> **One ledger correction falls out of the fourth row, and it matters more than the row itself.**
> `deferred-work.md:739` closed this item as *"moot — `AppendTreeNode`/`ProjectTree` no longer exist anywhere in
> `src/`"*. **That closure reason is false at HEAD**: `AppendTreeNode` was reintroduced in `CodeMapTemplater.cs`
> (`:377`, `:407`, `:431`) by the Code Map work. The item is genuinely closed — but because the *new* emitter
> shipped with a depth cap, not because the symbol is gone. A ledger entry closed on a disappearance that
> reversed is exactly the stale-record shape 17.4 AC #2 says to resolve **against the code**. Fix the entry.
>
> The AC's *intent* — address the highest-impact performance debts, measured — is live and well-supplied. Its
> *illustrations* are a 2026-07 snapshot. **This is the third consecutive Epic 17 story whose AC #1 examples are
> stale** (17.1: 3/3 closed; 17.2: 3/3 closed; 17.3: 3/4 closed). That is a signal about `epics.md`, not a
> coincidence — raised as **Q1**.

2.
**Given** changes intended purely to improve efficiency
**When** they land
**Then** rendered output stays byte-identical (or intentional changes are re-baselined) and the test suite stays green
**And** any item left unaddressed is recorded as an accepted known limitation rather than dropped silently.

> ⚠ **"Byte-identical" has no gate that can prove it for this story's changes.** `GoldenContentFingerprint` was
> retired (ADR 0034 / Story 23.6) and its replacement `check:parity` **cannot see a C#-side change** — its corpus
> IR is frozen. Almost all of this story's work is C#-side. See § *The gate AC #2 leans on is blind to this story*.

## Scope

**In scope**

- The two unmeasured webview residuals: cold-spawn warm-up and the `--serve` fallback path (§ A).
- The at-scale **byte**-bounding cluster — 5 items, canonical at `deferred-work.md:1146-1154` (§ B).
- NFR1 before/after measurement of baseline generation, reusing the existing `SPECSCRIBE_PHASE_TIMING` seam (§ C).
- Re-confirming deep-analytics separation from baseline runs (`--deep-git`, AD-4) (§ D).
- The Sonar performance/allocation band — **as a measurement candidate list, not a work order** (§ E).

**Out of scope — and who owns it instead**

| Not this story | Owner |
|---|---|
| The ~300-issue maintainability band (`S1192`/`S3776`/`S3358`/`S107`/`S125`…); SSOT clusters; dead code | **Story 17.1** |
| ReDoS (`S6444`), `S4036` tool resolution, CSP, dependency + CI supply-chain audit | **Story 17.2** |
| Disposing the 13 clusters; seating story candidates; the absent TS test harness; `check:ir-content` / ADR 0033 §4 re-measurement | **Story 17.4** (its ACs 2–4) |
| `specscribe.css` / `SiteGenerator.cs` file-scale split | **Story 17.5** |
| Retiring `HtmlRenderAdapter*.cs` | **Story 23.6** (`in-progress`) — do not optimize code it is deleting (§ E) |

**Boundary with 17.1 and 17.2.** All three touch `src/SpecScribe/*.cs`, and `SiteGenerator.cs`, `GitMetrics.cs`
and `Charts.cs` appear in more than one work-list. Per CLAUDE.md § *Scoping a code review*, **attribute by hunk,
not by file**, and say so in the record. A `ToDictionary` guard is 17.1's; a regex timeout is 17.2's; a byte
ceiling is yours.

## The actual work-list (verified at `15336f4`)

### A. The webview re-render: Epic 22 closed the big half; two residuals are unmeasured

**What is already done, and must not be rebuilt.** ADR 0005 §3 asked the runtime to *"scope the re-render like
`SiteGenerator.RegenerateEpics` does (re-ingest only what changed) to bring the ~1.8 s full re-render toward
sub-second"*. Two shipped stories answer it:

- **Story 22.5** (`done`) — the incremental event-driven regeneration engine; `ClassifyRebuildScope` decides
  narrow-vs-full before family routing, and topology escalates (**ADR 0027**).
- **Story 22.6** (`done`) — `specscribe webview --serve --serve-delta`. `extension.ts` recognizes a `DeltaFrame`
  (`:209`) and folds it onto the held payload (`applyDeltaFrame`, `:219`), preserving the documented invariant
  that *"a live-pushed `--serve` payload and a one-shot spawn payload are indistinguishable"* (`:176`).

**Measured, and the number is good** (`22-6-delta-measurement-report.md`, 2026-07-29, two runs ~40 min apart):
a single-file content edit through `GenerateOne` ships **2.72 % of the full IR** and **4.09 % of the full webview
payload**; inter-run drift under 0.03 pp. **Do not re-litigate this.**

**The two residuals — both real, both unmeasured, and both yours:**

1. **The cold spawn is still ~3.5 s, and the "warm renderer" half of ADR 0005 §3 was never built.**
   `extension.ts:884-890` wraps the first spawn in a progress notification precisely because *"the first spawn is
   cold (~3.5 s)"*. Delta transport makes **subsequent** updates cheap; it does nothing for first paint. ADR 0005
   §3's follow-up named *scoped re-render* **and** *warm renderer* — the first shipped, the second did not.
2. **The `--serve` fallback silently reverts to full one-shot spawns per change.** `extension.ts:983-985` sets a
   permanent "serve unavailable" latch when a `--serve` attempt exits before its first payload (older core, or a
   spawn failure), after which every refresh is a **full** spawn — exactly the behaviour AC #1 names. Nobody has
   measured how often this latch trips or what the degraded path costs.

> **Measure before building anything here.** Item 1 may be dominated by .NET startup, by ingest, or by the `git`
> subprocess calls — ADR 0005 §2 measured *"~1.8–2.0 s warm, ~3.5 s cold … dominated by ingest + the `git`
> subprocess calls, not .NET startup"*, but that was 2026-07-11, before the IR, before Nuxt, and before
> `NuxtPrerender` spawned Node. A warm-renderer design chosen without a current profile is a guess.

### B. The real at-scale defect: caps expressed in the wrong unit

This is the headline item and the largest genuine work in the story. It is **canonical** at
`deferred-work.md:1146-1154`, which states the shared failure mode precisely:

> **a cap expressed in the wrong unit (count, depth, or nothing) on an output whose real cost is bytes**

**This project has SHIPPED that failure twice** — an 82.5 MB `code-map.html` is the demonstrated case, not a
hypothetical one. Scale context for *this* repo, from the 22.6 report: **875 IR pages, 67.3 MB full IR, 44.8 MB
webview payload.** That is the "realistic scale" the user story names.

**The precedent fix already exists in this codebase and worked.** `SpaDelivery` carries
`MaxChunkBytes = 2_000_000` (`:112`) **alongside** `MaxPagesPerChunk = 75` (`:88`), and isolates an over-budget
page into its own dedicated chunk (`:472`, `:714`) — chosen comfortably above real-world size so default
generation stayed byte-identical. **That is the shape to apply.** Do not invent a second bounding idiom.

The five members, with what I verified at HEAD:

| # | Item | State at `15336f4` |
|---|---|---|
| 1 | Hierarchy Explorer's ≤8–10 KB budget governs hand-written **JS**, leaving the inline JSON island — the dimension that *does* grow with project size — unguarded | **Not verified this pass.** Read `HierarchyExplorer` before acting. |
| 2 | `TryCountCodeLines` has no per-file byte ceiling since the 1 MB skip was removed | **Live, and subtler than the ledger says.** `SiteGenerator.cs:3198` **streams** in 64 KB buffers, so it is bounded in *allocation* — but there is no early bail, so it is unbounded in *time* on a huge file. Fix the unit, do not re-add a memory fix. |
| 3 | The 82.5 MB `code-map.html` blow-up is only PARTIALLY resolved — tooltip-card cost capped, SVG rect-geometry cost not | **Not verified this pass.** `Charts.cs:2476` comments on `BuildTreemapCard` being "the single biggest per-point" cost past `MaxDetailedCodeMapFiles` (4,000, `Charts.cs:2172`), consistent with the ledger. Verify the geometry half yourself. |
| 4 | `FallbackCodeWalk` caps FILE count but traverses directories unbounded | **Live — confirmed by reading the loop.** `SiteGenerator.cs:6550`: the walk exits on `results.Count < MaxCodeMapFiles` (25,000, `:6466`). A huge tree with **few matching** files never reaches that cap, so it walks the entire tree — and can outrun the extension's 60 s hard kill. There is no directory-count or time bound. |
| 5 | No cap on epic sections concatenated onto the single `work-graph.html` page | **Not verified this pass.** `WorkGraph.cs` has a per-epic *follow-up draw* cap and a cycle `cap = 12` (`:419`), which are **different things** from a whole-page section cap. Do not read those as closing it. |

> **Honesty note on this table.** Items 2 and 4 I read at HEAD and confirmed. Items 1, 3 and 5 I did **not**
> individually verify — they are carried from the ledger. Given that three of AC #1's four examples turned out
> stale, **verify each before fixing it**, and record the answer either way. An item that turns out closed is a
> finding worth recording, not a wasted step.

### C. NFR1 measurement — the seam already exists; do not build a second one

AC #1 requires baseline generation performance *"measured before and after"*. There is **no performance test
harness** in `tests/SpecScribe.Tests/` — no perf/benchmark/scale test files, no BenchmarkDotNet.

**But the instrumentation is already there and is the right seam:**

```
SiteGenerator.cs:414-427   PhaseTimingEnabled — OFF unless SPECSCRIBE_PHASE_TIMING is set to something other than 0
SiteGenerator.cs:440-445   per-phase stderr line:  [phase] <name>  <ms> ms
SiteGenerator.cs:992       [phase] TOTAL GenerateAll  <ms> ms
```

Use it. A second timing mechanism is precisely the SSOT violation 17.1 is sweeping up.

**NFR1 is `epics.md:116`:** *"Baseline generation performance remains responsive for local OSS repositories, with
deeper analytics separated from baseline runs."* Note what it does **not** contain: a number. There is no
millisecond budget to pass or fail against, so "measured before and after" means **produce a comparable
before/after record**, not "hit a target". Establishing a defensible budget would be a new contract → **Q3**.

**Measurement discipline** (CLAUDE.md, and this project has been burned by every one of these):

- **`dotnet build --no-incremental` before any measured run.** `specscribe.css`/`.js` are embedded resources; an
  incremental build reuses the cached assembly and you will be timing a stale asset.
- **Two repeated runs minimum**, and report both. The 22.6 report does exactly this and is the house pattern.
- **Measure on a tree you can describe.** The 22.6 report measured with concurrent uncommitted work present and
  said so; run 1 saw 865 IR pages, run 2 saw 875. Name what moved under you.
- **`spike/delta-transport/` is the harness precedent** — quarantined (no `src/` code, no `.slnx` reference,
  generated site byte-identical with or without it), `--out` directory, machine-readable `report.json`, exit code
  0/1 as the gate. Model any new harness on it rather than adding perf code to `src/`.

### D. Deep-analytics separation — verify, do not rebuild

AC #1's closing clause: *"with deep analytics still separated from baseline runs."* This is an **inherited
invariant** (ARCHITECTURE-SPINE § AD-4: *"Optional insight providers may enrich output but never own baseline
success"*), carried by `--deep-git` / `settings.DeepGit` (`Commands.cs:762`, `:951`). The job is to **confirm it
still holds** for paths added since it was last checked and record the confirmation — not to re-engineer it.
Measure baseline generation **with `--deep-git` off**, which is the default and the thing NFR1 is about.

### E. The Sonar performance band — re-measured, and the ledger's framing is now wrong twice over

`deferred-work.md:1257` routes a *"performance and allocation band — 442 issues"* to this story, while recording
honestly that it is *"a candidate list for measurement, not a work order"*. Both halves of that need updating.

**Re-counted from the digest at authoring time:**

| rule | ledger said (2026-07-27) | digest says now | delta |
|---|---|---|---|
| `external_roslyn:CA1861` (constant arrays as arguments) | 326 | **409** | +83 |
| `external_roslyn:CA1859` (use concrete types) | 61 | **87** | +26 |
| `external_roslyn:CA1822` (member can be static) | 32 | **72** | +40 |
| `csharpsquid:S2325` (same finding as CA1822, different analyzer) | 23 | **23** | — |
| **total** | **442** | **591** | **+149** |

**Two facts reframe this band entirely, and both argue against sweeping it:**

1. **468 of the 591 (79 %) are in `tests/`, not product code.** Only **123** are in `src/`, spread over 43 files.
   The six densest files in the whole band are all test files (`CodeFileTemplaterTests.cs` 54,
   `SiteNavTests.cs` 52, `HtmlTemplaterTests.cs` 41, `GitMetricsCouplingTests.cs` 27,
   `RequirementsAndProgressTests.cs` 27, `HtmlRenderAdapterTests.cs` 19). **Allocation in test code has no bearing
   on NFR1 baseline generation performance.**
2. **36 of the 123 `src/` findings are in code Story 23.6 is deleting.** `HtmlRenderAdapter.Epics.cs` (16),
   `HtmlRenderAdapter.Dashboard.cs` (13) and `HtmlRenderAdapter.cs` (7) are the C# HTML writer that ADR 0034 /
   Story 23.6 retires — and 23.6 is `in-progress` with Tasks 1–7 done and only a visual browser pass left.
   Optimizing allocations in files scheduled for deletion is pure waste, and it will collide with 23.6's diff.

**So the genuinely relevant surface is ~87 INFO/MINOR findings across ~40 product files** — and even those are
*cost-of-dispatch* and *cost-of-allocation* rules with **no measured hotspot behind them**. The correct output
here is almost certainly: measure first (§ C), fix only what the profile implicates, and record the rest as an
accepted known limitation under AC #2's second clause. **Do not bulk-apply 591 analyzer suggestions.**

Per **ADR 0035 § Decision 5**, a rule-level suppression is **not** an acceptable route to closing these; and the
ledger notes suppression would additionally *"destroy the before-measurement 17.3 depends on"*. Record decisions
in `deferred-work.md` at per-item or per-band granularity instead.

## Tasks / Subtasks

**Sequencing is load-bearing.** Task 0 first, Task 1 second. Both AC #1 ("measured before and after") and § E's
conclusion depend on having a profile *before* any optimization lands. Do not skip to remediation.

- [ ] **Task 0 — Baseline before touching anything (AC: #1, #2)**
  - [ ] `git rev-parse HEAD` and record it as this story's real baseline (this file says `15336f4`).
  - [ ] Refresh the analysis digest: `node tools/analysis-digest/index.mjs`. At authoring time it was
        **stale by the read-time rule** — `evaluatedAtRevision` `c73ebcb` ≠ HEAD `15336f4`, `analysisRevision`
        `01acf5b1`, `commitsBehind: 15`, `isStale: true`. Every § E count will have moved again.
  - [ ] Re-count the four perf rules **and re-run the `src/` vs `tests/` split** — the 79 %-in-tests finding is
        the load-bearing one and it must be re-derived, not inherited.
  - [ ] `dotnet build SpecScribe.slnx --no-incremental`, then `dotnet test SpecScribe.slnx`; record the pass count
        as the pre-pass baseline (~2,932 passing per Story 23.6's last run).
  - [ ] `cd web && npm run check && npm run test` — record red/green **per gate** before you change anything. If a
        gate is already red at HEAD, say so in the Dev Agent Record; otherwise you cannot distinguish your
        breakage from inherited state.
  - [ ] Do **not** treat `npm run check:ir-content` as a health signal in a fresh worktree — it is red there for
        environmental reasons (no IR ⇒ nearly everything pruned). Its true state is **Story 17.4's** to establish.

- [ ] **Task 1 — Profile baseline generation, and publish the numbers (AC: #1)**
  - [ ] Run `generate` with `SPECSCRIBE_PHASE_TIMING=1`, **`--deep-git` off** (the default and NFR1's subject),
        after a `--no-incremental` build. Capture per-phase and `TOTAL GenerateAll`.
  - [ ] Repeat — **two runs minimum**, both reported. Name any concurrent work in the tree.
  - [ ] Run once **with** `--deep-git` to record the separation margin (§ D), and confirm AD-4 still holds: a deep
        provider failing must not fail baseline generation.
  - [ ] Publish a measurement record modelled on `22-6-delta-measurement-report.md` (units stated, reproduce
        command, two runs, caveats). This is the "before" half of AC #1 and the artifact 17.4's sign-off consumes.
  - [ ] **Let this profile choose the rest of the story's targets.** If the profile does not implicate the § E
        band, say so plainly and defer it — that is a valid and expected outcome.

- [ ] **Task 2 — Byte-bound the at-scale cluster (AC: #1, #2)** — the largest genuine item.
  - [ ] Apply the `SpaDelivery.MaxChunkBytes` **+** `MaxPagesPerChunk` idiom: a byte ceiling **alongside** the
        existing count/depth cap, set comfortably above real-world size so default generation stays byte-identical.
  - [ ] `FallbackCodeWalk` (`SiteGenerator.cs:6550`) — **confirmed live.** Bound the directory traversal itself,
        not just `results.Count`. Preserve the ordinal sort: it fixed a genuine cross-filesystem portability bug
        (NTFS vs ext4/APFS) and a fingerprint depends on it.
  - [ ] `TryCountCodeLines` (`:3198`) — **confirmed live.** Add a byte ceiling / early bail. It already streams,
        so this is a *time* bound, not a memory one; do not "fix" the allocation that is already fixed.
  - [ ] Verify items 1, 3 and 5 of § B's table individually before fixing them, and record whichever turn out
        closed.
  - [ ] Pin each bound with a test that fails when the cap is removed. A cap without a test re-rots.

- [ ] **Task 3 — The two webview residuals (AC: #1)**
  - [ ] **Measure first.** Profile a cold `specscribe webview` spawn at HEAD and attribute the ~3.5 s across .NET
        startup / ingest / `git` subprocesses / `NuxtPrerender`'s Node spawn. ADR 0005 §2's attribution predates
        the IR, Nuxt and Node — do not carry it forward unmeasured.
  - [ ] Decide the warm-renderer question **on that profile**. If startup is not the dominant term, a warm
        renderer is the wrong fix and saying so is the correct deliverable.
  - [ ] Characterize the `--serve` fallback latch (`extension.ts:983-985`): when does it trip, and what does the
        degraded full-spawn path cost? At minimum make the degradation observable rather than silent.
  - [ ] Do **not** re-engineer the delta channel. Story 22.6 measured it at 2.72 % / 4.09 %; it is settled.

- [ ] **Task 4 — Dispose the Sonar performance band deliberately (AC: #1, #2)**
  - [ ] Split the refreshed count `src/` vs `tests/` and exclude the `HtmlRenderAdapter*` findings as **Story
        23.6's to delete** — coordinate, do not optimize them.
  - [ ] Fix only what Task 1's profile implicates.
  - [ ] Record the remainder as an **accepted known limitation** with rationale (AC #2's second clause). No
        rule-level suppression — **ADR 0035 § Decision 5**.

- [ ] **Task 5 — Correct the records this story disproves (AC: #2)**
  - [ ] `deferred-work.md:739` — the `AppendTreeNode` entry is closed for a reason that reversed. Rewrite it:
        the symbol is back in `CodeMapTemplater.cs` and the item is closed because the new emitter has a depth cap.
  - [ ] `deferred-work.md:1257` — update 442 → the refreshed count, and record the `src`/`tests` split and the
        23.6 overlap so the next reader does not re-derive them.
  - [ ] Strike closed items **in place** with the resolution — never delete. `DeferredWorkParser` renders this
        file into the portal and the audit trail is load-bearing.
  - [ ] Record every unaddressed item as an accepted known limitation. AC #2 admits no third state.
  - [ ] Name whose concurrent changes your measurements sat on top of (CLAUDE.md § *Concurrent work*).

- [ ] **Task 6 — Prove nothing regressed (AC: #2)**
  - [ ] Re-run the Task 1 profile as the **"after"** half and publish the comparison.
  - [ ] `dotnet build SpecScribe.slnx --no-incremental` → `dotnet test SpecScribe.slnx`; compare to Task 0.
  - [ ] `cd web && npm run check && npm run test`.
  - [ ] **Read § *The gate AC #2 leans on is blind to this story* before reporting "byte-identical".**
  - [ ] Confirm any regenerated baseline is **stable across two repeated runs** before locking it in.

## Dev Notes

### The gate AC #2 leans on is blind to this story

This is the highest-risk misunderstanding available here, so it comes first.

AC #2 promises safety via *"rendered output stays byte-identical"*. There is **no gate that can prove that** for
this story's changes:

- `GoldenContentFingerprint` — **retired** (ADR 0034 / Story 23.6). `SiteGeneratorAdapterTests.cs` carries only
  its tombstone comment.
- `check:parity` — its corpus IR is **frozen** at `web/fixtures/parity-corpus/`, so a C#-side change renders from
  *pinned* input and the gate stays green. Verified 2026-08-01: removing an element from the shared nav on **every
  page** left all 24 routes byte-identical.

Nearly all of this story's work (§ B, § D, § E, and the C# half of § A) is C#-side. **A green `npm run check` is
not evidence that your optimization preserved output.** Real coverage:

| Change surface | What actually catches a regression |
|---|---|
| Byte caps in `SiteGenerator`/`Charts`/`CodeMap` (Task 2) | unit tests over the emitted region + **live-browser inspection** |
| Allocation/dispatch edits in `src/` (Task 4) | the C# suite, plus the Task 1/6 before-after profile |
| `web/` renderer | `check:parity` (this is what it *is* for) |
| `extension/src` (Task 3) | `npm run typecheck` only — **no TS test harness exists** (17.4's cluster) |

A byte cap is *designed* to change output past a threshold. Set it comfortably above real-world size — the
`MaxChunkBytes` precedent did exactly that so default generation stayed byte-identical — and state the threshold
and its rationale in the record.

### Architecture constraints you must not violate

- **AD-4 / ARCHITECTURE-SPINE** — optional insight providers are additive, non-blocking, independently toggleable,
  and **never own baseline success**. An optimization that makes deep analytics load-bearing for baseline
  generation is a contract break.
- **AD-5 / ADR 0027** — watch may rebuild narrowly only when proven byte-identical to a full rebuild of the same
  tree, decided by **one** named classifier before family routing; topology escalates. Do not add a second
  narrowing path beside `ClassifyRebuildScope`.
- **ADR 0028** — the delta transport is a **sidecar and a stream, never a server**. A warm renderer must not
  become a long-lived server process.
- **ADR 0005 §1** — the C# side stays a **stateless one-shot renderer** (spawn → render → exit). This is in direct
  tension with the "warm renderer" idea in AC #1, and that tension is the point of **Q2** — a persistent renderer
  would amend ADR 0005 and needs its own ADR, not a story note.
- **ADR 0035 § Decision 5** — a Sonar rule suppression is not an acceptable route to closing a finding.
- **ADR 0013 / NFR5** — the no-JS text-twin contract. Do not buy performance by making a surface JS-dependent.
- **ADR 0034** — the IR is the product and the site is rendered from it. An optimization must not change IR
  semantics to save bytes.

### Traps specific to this repository

- **Rebuild non-incrementally before any measurement.** `specscribe.css`/`.js` are embedded resources; an
  incremental build reuses the cached assembly and never re-embeds a changed asset. **For a perf story this is not
  a footnote — every number you take after an incremental build is measuring the wrong binary.**
- **Never regenerate a gate baseline reflexively.** If a gate moves and you did not touch rendering, audit the
  harness first — Epic 5 found the harness itself leaking a commit SHA. Bisect in a throwaway tree
  (`git archive HEAD` into the scratchpad), never by resetting the shared tree.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** Another session's uncommitted work may be in the
  tree. This has already destroyed real work mid-story.
- **Verify after every edit.** Grep for the symbol you just changed. A `Charts.cs` edit has silently vanished this
  way before — and `Charts.cs` is in this story's file list.
- **`check:ir-content` red in a fresh worktree is environmental**, not drift.
- **`specscribe generate` in a worktree**: the renderer-path defect was fixed by Story 16.3 — do **not** set
  `SPECSCRIBE_RENDERER_DIR`.
- **`FileWatcherServiceTests.BurstOfSaves` is a known load-sensitive flake** (17.4 AC #3, time-critical before
  16.2 lands). A perf story runs the suite under load repeatedly, so **you are more likely than most to hit it.**
  Re-run in isolation before believing it is your change.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live` — vestigial and gitignored.

### Analysis digest

`.specscribe/analysis/` is gitignored and lives only in the **main checkout**, not a fresh worktree. At authoring
time: `evaluatedAtRevision` `c73ebcb` vs HEAD `15336f4` → **stale by the read-time rule regardless of `isStale`**;
`analysisRevision` `01acf5b1`, `commitsBehind: 15`, `workingTreeDirty: false`. Totals: **1,755 observations across
231 files** (142 error / 1,179 warning / 434 note). Read shards, never the whole tree (index ~31 KB; everything
~1.34 MB). `attachment.basis` is `"unavailable"` on every record — the code→planning join is Story 26.5's.

### Testing

- xUnit, `tests/SpecScribe.Tests/`. **No perf/benchmark/scale test files exist** — `Stopwatch` appears only in
  `Commands.cs`, `SiteGenerator.cs` and `NuxtPrerender.cs`, i.e. product instrumentation, not tests.
- `web/` uses Vitest. **`extension/` has no TypeScript test harness at all** — that harness is **17.4's** cluster.
  If a Task 3 change needs a TS test, you cannot pin it today: assert from the C# suite or record the gap honestly.
- **Do not add a timing assertion to the C# suite.** A wall-clock threshold in a unit test is a flake generator on
  a shared runner, and this repo already has one load-sensitive flake it is trying to kill. Measurement belongs in
  a quarantined harness (`spike/delta-transport/` is the precedent) with a published report.

### Previous story intelligence (17.1 and 17.2)

Both are `ready-for-dev` and **not yet implemented**, so expect no code from either — but their create-story
records are directly reusable:

- **The stale-AC pattern is now three-for-three.** 17.1 found 3/3 of its AC #1 examples closed *and* the gate it
  named retired; 17.2 found 3/3 closed; this story finds 3/4 closed. Treat every AC illustration in Epic 17 as a
  2026-07 snapshot requiring verification (**Q1**).
- **17.1 proved citations drift at scale** — every one of its four CSS citations and its extension-regex citation
  had moved. This story's `AppendTreeNode` finding is the same lesson in a nastier form: a ledger entry closed on
  "the symbol no longer exists" when the symbol has since come back.
- **17.2 established the measure-before-fixing discipline** and the "if it does not reproduce, record that and
  stop" rule. Tasks 1 and 3 here follow it.
- **17.1 flags that Sonar findings are not all real** (`CapabilityStyler.cs`, `WorkGraph.cs` are dataflow blind
  spots). Expect the same for parts of the § E band — adjudicate, do not bulk-apply.
- Both raise the same sequencing concern this story inherits (**Q4**).

### Git intelligence

Recent commits at the baseline, most recent first:

```
15336f4  Merge branch 'worktree-code-review-23-2-fourth-pass'   <- baseline_commit
4571a2e  Merge branch 'worktree-code-review-24-2'
3b085e7  Code review of Story 24.2: per-file ego coupling graph
69c4fe7  Merge branch 'worktree-code-review-25-3'
cdfc382  Merge branch 'worktree-code-review-16-1'
```

Consequences:

- **CI is blocking.** `build-test-analyze` is a required check on `main` (Story 16.2). A red suite blocks merges.
- **Epic 23 is landing underneath you.** 23.2 is `in-progress`, 23.3/23.4/23.5 are `review`, and **23.6 is
  `in-progress` with only a visual browser pass left before `HtmlRenderAdapter*.cs` is deleted.** Coordinate before
  touching those three files (§ E).
- **Epic 24 is in flight** (24.2 just reviewed) and is graph/analytics work — a plausible source of new at-scale
  payload items during this story.
- Commits routinely **bundle sibling stories** because review runs at epic end. Record your File List precisely so
  the eventual review can scope by hunk.

### Project Structure Notes

- **Three code surfaces, three toolchains.** C# (`src/SpecScribe`, `tests/SpecScribe.Tests`) via
  `dotnet build SpecScribe.slnx`; the Nuxt renderer in `web/` (`npm run check`, `npm run test` → vitest); the VS
  Code shim in `extension/` (`npm run typecheck` → `tsc --noEmit`; **no test runner**).
- **Primary files, all existing (`UPDATE`, none new):** `src/SpecScribe/SiteGenerator.cs` (`FallbackCodeWalk`,
  `TryCountCodeLines`, `PhaseTimingEnabled`), `src/SpecScribe/Charts.cs`, `src/SpecScribe/CodeMap.cs`,
  `src/SpecScribe/CodeMapTemplater.cs`, `src/SpecScribe/HierarchyExplorer.cs`, `src/SpecScribe/SpaDelivery.cs`
  (the precedent, likely read-only), `src/SpecScribe/WorkGraph.cs`, `extension/src/extension.ts`,
  `_bmad-output/implementation-artifacts/deferred-work.md`.
- **Expect new:** cap-regression tests in `tests/SpecScribe.Tests/`, a measurement report alongside
  `22-6-delta-measurement-report.md`, and possibly one ADR (Q2).
- **CI** (`.github/workflows/build-test-analyze.yml`): `build-test-analyze` on `windows-latest` runs
  `dotnet build --no-incremental` → `dotnet test` → `npm ci` → `sync:assets` → `build:package` →
  `generate --deep-git` → `npm run check` → `npm run test:coverage`, wrapped in SonarScanner begin/end.
  `portability-probe` on `ubuntu-latest` is **non-gating**. Note CI generates **with** `--deep-git`, so CI timings
  are *not* an NFR1 baseline measurement.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 17 → Story 17.3] — ACs, verbatim above; 17.1/17.2/17.4/17.5 boundaries
- [Source: `_bmad-output/planning-artifacts/epics.md:116`] — NFR1, in full, and its lack of a numeric budget
- [Source: `docs/adrs/0005-vs-code-webview-runtime-and-packaging.md` §2, §3] — the ~1.8 s warm / ~3.5 s cold measurement, the stateless one-shot renderer, and the scoped-re-render/warm-renderer follow-up AC #1 cites
- [Source: `docs/adrs/0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md`] — narrow rebuild must be proven byte-identical; one classifier
- [Source: `docs/adrs/0028-delta-transport-is-a-sidecar-and-a-stream-never-a-server.md`] — bounds any warm-renderer design
- [Source: `docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md`] — retirement of `GoldenContentFingerprint`
- [Source: `docs/adrs/0035-sonarcloud-quality-gate-and-rule-decision-policy.md` §Decision 5] — suppression is not a route
- [Source: `_bmad-output/implementation-artifacts/22-6-delta-measurement-report.md`] — 2.72 % / 4.09 % delta, 875 pages / 67.3 MB IR / 44.8 MB payload, and the two-run measurement pattern
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:1146-1154`] — **canonical** byte-blind-emitter / at-scale-bounding cluster and the `MaxChunkBytes` precedent
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:739`] — the `AppendTreeNode` entry whose closure reason reversed
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:719`] — renderer-swap scans, resolved 2026-07-18
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:1257`] — the perf/allocation band routed to this story
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` § AD-4, AD-5, AD-8] — insight providers never own baseline success; changed scope is the unit of recomputation
- [Source: `CLAUDE.md` § *Which gate is which*, § *Concurrent work on shared main*, § *Verification*] — gate visibility, non-incremental rebuild rule, live-browser requirement

## Questions for the owner

Saved for after the story, per workflow. **None blocks `dev-story`**; each has a stated default.

1. **Three of AC #1's four examples are closed, and this is the third consecutive Epic 17 story where the AC
   illustrations are stale.** Amend `epics.md` to point at the live inventory (§ A/§ B) instead?
   *Default: leave `epics.md` alone; this file's ⚠ table carries the correction and 17.4 folds it into the
   burndown record.*
2. **A "warm renderer" would amend ADR 0005 §1's stateless one-shot contract and brush against ADR 0028's
   "never a server".** That is a cross-cutting contract change → **ADR candidate**, not a story note.
   *Default: profile first (Task 3); propose an ADR only if the measurement shows startup actually dominates.*
3. **NFR1 has no numeric budget**, so "measured before and after" can only produce a comparable record, not a
   pass/fail. Should this story propose a budget (which would be a new contract, and arguably an ADR)?
   *Default: publish the measurement record; do not invent a threshold.*
4. **Epic 17's stated sequencing ("after Epics 1–15/18 and Epic 5") is still unmet** — Epic 23 in particular is
   actively landing, and 23.6 deletes three files this story would otherwise optimize. Proceed anyway?
   *Default: proceed, coordinate with 23.6, and prefer invariant-shaped fixes (a cap plus a test) over one-time
   sweeps.* (Same question 17.1 and 17.2 raised.)
5. **The `--serve` fallback latch degrades silently to full spawns.** Is making that degradation *observable*
   (a status line / notice) in scope here, or is it webview-UX work for Epic 6?
   *Default: measure and record it here; surface it only if the fix is trivial.*

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
