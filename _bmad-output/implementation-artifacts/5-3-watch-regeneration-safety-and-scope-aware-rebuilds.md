---
baseline_commit: 8db18aaddd7cc1325910bfc9b00e0ae9d1ac66a1
---

# Story 5.3: Watch Regeneration Safety and Scope-Aware Rebuilds

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer editing artifacts rapidly,
I want watch mode to regenerate safely under change bursts,
so that output stays coherent without blocking file edits, and structural changes (renames, deletes, new artifact
types appearing) never leave stale or broken pages behind.

## Acceptance Criteria

1. **Given** multiple rapid saves occur in watched sources
   **When** watch mode processes changes
   **Then** output remains consistent and non-corrupt
   **And** source files are read with shared access without write-lock side effects.

2. **Given** rename, delete, or topology changes happen
   **When** watch mode recomputes output
   **Then** stale pages are removed or refreshed appropriately
   **And** rebuild scope escalates when required for coherence.

### Derived / cross-cutting acceptance (from FR8, NFR2, NFR5, and gaps found while reading the current watch pipeline)

3. **Given** `epics.md` itself is deleted while watch mode is running
   **When** the debounced change fires
   **Then** the stale `epics.html`, the whole `epics/` output subtree, `requirements.html` + `requirements/`,
   `sprint.html`, and the nav's Epics/Requirements/Sprint entries are removed or rebuilt to reflect "no epics" —
   not left pointing at pages that no longer correspond to any source.

4. **Given** `sprint-status.yaml` is added, edited, or removed while watch mode is running
   **When** the change is detected
   **Then** `sprint.html`, the home index's Sprint widget, `action-items.html`, and the nav's Sprint entry are
   refreshed to match — today `FileWatcherService` only watches `*.md`, so this file class is invisible to watch
   mode entirely; that gap is closed by this story.

5. **Given** a whole directory is renamed, added, or deleted (not just an individual file) under either watched
   root
   **When** watch mode processes the change
   **Then** the rebuild scope escalates to a full regeneration (equivalent to the `generate` pipeline) rather than
   silently doing nothing — `FileSystemWatcher`'s `Filter = "*.md"` does not match a directory's own name, so a
   folder-level rename/delete/create is otherwise never observed at all.

6. **Given** a burst of saves touches many files at once (e.g. a bulk find/replace, a git checkout, or several
   BMad artifacts written by one workflow step)
   **When** the debounce settles
   **Then** the existing `SiteGenerator._gate` lock continues to serialize every regeneration call so no two
   writes race, and per-file debounce timers are not required to coalesce into a single pass to satisfy
   correctness — but redundant back-to-back full-scope rebuilds (e.g. `RegenerateEpics` firing once per touched
   story file in the same burst) are reduced where cheap to do so without changing observable behavior.

## Tasks / Subtasks

> **Tasks 1 and 2 were already shipped by Story 6.11** — this story's Dev Notes were written against a pre-6.11
> reading of the source. Both are checked as satisfied-by-existing-code, with the AC #4 gap that genuinely remained
> (no end-to-end watcher coverage, especially of the *removal* case) closed under Task 6. See Completion Notes for
> why a narrower `RegenerateSprint()` was deliberately not added.

- [x] **Task 1 — Watch the sprint status file, not just `*.md` (AC: #4)** — ALREADY DONE (Story 6.11)
  - [x] `FileWatcherService.CreateWatcher` hard-codes `Filter = "*.md"`. Add a second filter (or a second watcher
    on `options.SourceRoot` with `Filter = "sprint-status.yaml"`, `IncludeSubdirectories = true`) so changes to
    the well-known sprint file are observed. Route its debounced fire to a new `SiteGenerator` method (see Task 2)
    rather than `GenerateOne`/`IsEpicsRelated` (a `.yaml` file is not markdown and must not go through
    `MarkdownConverter`).
  - [x] Keep the existing `.md` watcher on `SourceRoot`/`AdrSourceRoot` untouched — this is an additive watcher,
    not a filter change, so `*.md` behavior is unaffected.

- [x] **Task 2 — `SiteGenerator.RegenerateSprint()`: targeted sprint refresh (AC: #4)** — SATISFIED BY
  `RegenerateFromDataSource` (Story 6.11); a narrower method deliberately NOT added, see Completion Notes
  - [x] Add a `GenerationEvent RegenerateSprint()` method (mirrors `RegenerateAdrs`'s shape): under `_gate`,
    re-parse `SprintSourcePath` into `_sprint`, then re-run `WriteSprint(nav)`, `WriteActionItems(nav)`, and
    `WriteIndex(nav)` (the home widget reads `_sprint`) using the cached `_nav`/`_epicsModel`/`_docs` — do **not**
    re-run the epics phase or the full file scan. `_nav` itself must be rebuilt too (via `BuildNav`) because
    `SiteNav.Build`'s `hasSprint` parameter is derived from `SprintAvailable`, which changes when the yaml
    appears/disappears — reuse the same `BuildNav(sourceRelatives)` call `RegenerateEpics` already makes,
    sourcing `sourceRelatives` from `EnumerateSourceFiles()`.
  - [x] If `_sprint` becomes null (file deleted or now malformed) and a `sprint.html` / `action-items.html` /
    `retros.html` exist on disk from a prior pass, delete them — `WriteSprint`/`WriteActionItems` already no-op
    (return without writing) when `_sprint`/`open` is null/empty, which today only matters on a fresh
    `GenerateAll` (which wipes `OutputRoot` first); in the incremental watch path nothing currently deletes a
    page that was previously written and is now stale. Add that deletion here (Task 4 below adds a small shared
    helper for "delete this output file if the model says it shouldn't exist").
  - [x] Wire `FileWatcherService`'s new sprint-file debounce branch (Task 1) to call `_generator.RegenerateSprint()`.

- [x] **Task 3 — `epics.md` deletion must clean up its whole output subtree (AC: #3)**
  - [x] `RegenerateEpics()` currently: if `FindEpicsSourceFile(files)` is null, it calls `WriteIndex(nav)` and
    returns a `Skipped` event — but `epics.html`, `epics/*.html`, `requirements.html`, `requirements/*.html`, and
    `sprint.html` (which links into the epics model) are left on disk from the prior run, and `_epicsModel`,
    `_progress`, `_requirements` stay populated with stale in-memory data even though their source is gone.
  - [x] When `epicsSourceFile is null` in `RegenerateEpics()`: delete `epics.html` if present, delete the
    `epics/` output directory if present (mirrors the existing `Directory.Delete(epicsDir, recursive: true)`
    pattern already used inside `GenerateEpicsInternal`), delete `requirements.html` and the `requirements/`
    directory if present, clear `_epicsModel = null`, `_progress = null`, `_requirements = null`, then call
    `RegenerateSprint()` (Task 2) so the sprint page — which reads `_epicsModel` for story links — also
    re-resolves against "no epics." `SprintTemplater.RenderIndex(SprintStatus sprint, EpicsModel? epics, ...)`
    already takes a nullable `EpicsModel` (verified against current signature), so no guard is needed — the page
    stays and degrades gracefully (no story/epic links) rather than disappearing. Finally `WriteIndex(nav)`.
  - [x] Return a `GenerationEvent(GenerationOutcome.Removed, "epics.md", sw.Elapsed, "epics.md removed")` in this
    branch instead of the current `Skipped` — this is a real destructive change to the output tree, not a no-op.
    *(Conditioned on something actually having been removed, so the "no epics.md at all" project keeps its
    long-standing `Skipped`.)*

- [x] **Task 4 — Directory-level topology changes escalate to a full rebuild (AC: #2, #5)**
  - [x] `FileSystemWatcher.Filter = "*.md"` does not match a bare directory name on rename/create/delete, so a
    whole-folder operation (e.g. renaming `implementation-artifacts/` itself, or deleting a subfolder full of
    stories) currently produces **no watcher event at all** for the folder operation — only individual contained
    files might (platform-dependent; on Windows a folder rename typically does not enumerate its children as
    separate events). Add a second, filter-less `FileSystemWatcher` per watched root
    (`NotifyFilter = NotifyFilters.DirectoryName`, `IncludeSubdirectories = true`) whose `Created`/`Deleted`/
    `Renamed` handlers debounce a **sentinel key** (e.g. `"<topology>"` — a constant, not a file path) through the
    existing `_pending` dictionary, distinct from any real file path.
  - [x] When the topology-sentinel timer fires, do **not** attempt to classify a single path (there isn't one
    that means anything) — call a new `SiteGenerator.RegenerateAll()`-equivalent entry point that reuses
    `GenerateAll`'s full-rebuild body (wipe `OutputRoot`, rescan, rebuild everything) under `_gate`, and surface
    it as a single `GenerationEvent(GenerationOutcome.Updated, "<directory change>", elapsed, "full rebuild")` to
    `_onEvent`. This is the concrete meaning of "rebuild scope escalates when required for coherence" in AC #2 —
    directory topology is exactly the case the per-file incremental path structurally cannot handle correctly.
  - [x] Keep this escalation coarse and rare-path: it should not fire for ordinary file edits (those still go
    through the existing per-file/`IsEpicsRelated`/`IsAdr` routing), only for actual directory create/rename/
    delete events. Debounce it with the same `ForgeOptions.DebounceInterval` so a burst of directory churn (e.g.
    an IDE rename-refactor touching many nested files) still collapses to one rebuild.
    *(Plus a new `IsUnderOutputRoot` guard — see Completion Notes #6: a nested `--output` would otherwise make
    each rebuild's own directory writes re-arm the topology timer forever.)*

- [x] **Task 5 — Burst-safety hardening pass (AC: #1, #6)** — audit found everything gated, but the new tests
  surfaced TWO real defects the audit could not (see Completion Notes #3)
  - [x] Audit (do not rewrite from scratch — see Dev Notes "already correct" list below) that every write path
    reachable from a debounce fire still goes through `SiteGenerator._gate`. Task 2's `RegenerateSprint` and Task
    4's full-rebuild entry point must both take the same lock as `GenerateOne`/`RegenerateEpics`/`RegenerateAdrs`
    — no new unlocked write path.
  - [x] Confirm (via a new test, Task 6) that concurrent debounce fires for *different* files — e.g. a story file
    and `epics.md` changing in the same burst — never interleave writes: because both routes take `_gate`, the
    two regenerations serialize; the test should assert the final on-disk state is fully coherent (no partial
    HTML, no file left mid-write) regardless of fire order, not that a specific order occurred.
  - [x] No functional change is expected here if the audit finds everything already gated — this task exists to
    add the regression test coverage that AC #1/#6 currently lack, not to introduce new locking.
    *⚠️ **This expectation did not hold.** No new locking was needed (the audit was right about `_gate`), but the
    tests found two genuine crash paths the locking discipline never covered: an unguarded `Timer` callback that
    turns any transient into process death, and a contended `CopyEmbeddedAsset`. Both fixed — Completion Notes #3.*

- [x] **Task 6 — Tests (AC: #1–#6)**
  - [x] New `FileWatcherServiceTests.cs` (temp-dir fixture, mirrors `SiteGeneratorSprintTests`'s style): construct
    a real `FileWatcherService` over a temp `_bmad-output`/`docs/adrs` pair with a real `SiteGenerator`, `Start()`
    it, and drive scenarios with short `Task.Delay`s past `ForgeOptions.DebounceInterval`:
    - editing an existing story file → `GenerateOne` path fires, output updates;
    - deleting `epics.md` → `epics.html`/`epics/`/`requirements.html`/`requirements/` are gone, home index has no
      epics widget, no exception;
    - adding/editing/removing `sprint-status.yaml` → `sprint.html` appears/updates/disappears accordingly;
    - renaming a whole subdirectory of story files → full-rebuild path fires and every renamed file's page exists
      at its new location with no orphan at the old one.
  - [x] Extend `SiteGeneratorSprintTests.cs` or add `SiteGeneratorEpicsRemovalTests.cs` (headless, no watcher) for
    Task 3's `RegenerateEpics()` deletion branch directly — call `GenerateAll()` with an epics.md present, then
    delete it from the temp source dir and call `RegenerateEpics()` directly, asserting the output files are
    gone and the returned event is `Removed`.
  - [x] A concurrency test for Task 5: spin up N threads (or `Parallel.For`) each calling a different
    `SiteGenerator` write method (`GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`) against the same instance
    and temp output dir; assert no exception, no torn/empty HTML file, and a final `GenerateAll()` pass (ground
    truth) produces output byte-identical in structure (same file set) to what the concurrent run converged to
    for any file that didn't change between the two passes.
  - [x] Run the full suite: `dotnet test` from repo root; all existing tests stay green — especially
    `SiteGeneratorSprintTests`, `SiteGeneratorStoryEpicPagesTests`, and `SiteGeneratorTraceabilityTests`, which
    exercise the epics/sprint machinery this story edits.

## Dev Notes

### ⚠️ Critical framing: most of "safety" already exists — this story closes specific, narrow gaps

Unlike a from-scratch feature, the debounce mechanism, the shared-read discipline, and the single-writer lock
are **already implemented and already correct** for the cases they cover. Reinventing them is the primary failure
mode to avoid. What's already done, verified by reading the current source:

1. **Debounce exists per-file** (`FileWatcherService._pending`, keyed by full path, `ForgeOptions.DebounceInterval`
   = 400ms) and decides the action from **ground truth at fire time** (`File.Exists` check), not from which
   event type triggered it — this already handles the classic "Changed then Deleted in the same burst" race
   correctly. Do not redesign this.
2. **Shared reads are already universal.** `MarkdownConverter.ReadAllTextShared` opens with
   `FileShare.ReadWrite | FileShare.Delete` and is used by every read path in `SiteGenerator`
   (`GenerateOneInternal` via `MarkdownConverter.Convert`, `GenerateAdrsInternal`, `GenerateEpicsInternal`,
   `SprintStatusParser.ParseFile`, `ProgressCalculator`, `RetroParser`). NFR5 is already satisfied for every file
   class this story touches. AC #1's second clause is a **regression guard to keep true**, not new work.
3. **A single lock (`SiteGenerator._gate`) already serializes every write-producing method**
   (`GenerateAll`, `GenerateOne`, `RemoveFor`, `RegenerateEpics`, `RegenerateAdrs`) — concurrent debounce fires
   from different timers already cannot interleave writes today. AC #1's "output remains consistent and
   non-corrupt" and AC #6 are largely **already true**; Task 5 exists to add test coverage proving it and to make
   sure the two *new* methods (Task 2, Task 4) don't accidentally bypass `_gate`.

The genuine gaps — the actual delta this story adds — are narrow:
- `epics.md` deletion leaves a stale output subtree (Task 3).
- `sprint-status.yaml` is invisible to the watcher entirely — wrong file extension for the filter (Tasks 1–2).
- Whole-directory rename/create/delete is invisible to the watcher entirely — doesn't match the `*.md` filter
  (Task 4).

### Current state of the files you will touch (read before editing)

- **`src/SpecScribe/FileWatcherService.cs`** — two `FileSystemWatcher`s today (`SourceRoot`, `AdrSourceRoot`),
  both `Filter = "*.md"`, `IncludeSubdirectories = true`. `Debounce(fullPath)` keys `_pending` by full path;
  `CreateTimer` decides the regeneration action at fire time via `_generator.IsAdr`/`IsEpicsRelated`/
  `File.Exists`. **Edit targets:** add a sprint-file watcher (Task 1) and a filter-less directory-topology
  watcher (Task 4) per root; both feed the same `_pending`/timer machinery with distinct keys so they still
  debounce and still route through `_onEvent`. **Must preserve:** the existing `.md` watchers' filter, the
  ground-truth-at-fire-time decision (don't decide from the event args), `Start()`/`Stop()`/`Dispose()`
  semantics.
- **`src/SpecScribe/SiteGenerator.cs`** — `_gate` object lock guards every write method; `_docs`/`_nav`/
  `_epicsModel`/`_progress`/`_requirements`/`_adrs`/`_sprint`/`_retros` are the in-memory cache the incremental
  methods read/mutate. `RegenerateEpics()` (line ~199) is the Task 3 edit target — currently no-ops on missing
  epics.md without cleanup. `WriteSprint`/`WriteActionItems`/`WriteRetroIndex` (lines ~594-625) already no-op
  cleanly when their model is null/empty, but only prevent *writing*, not *removing a previously-written* page —
  that asymmetry is what Task 2's cleanup addresses. **Edit targets:** new `RegenerateSprint()` method (Task 2)
  mirroring `RegenerateAdrs()`'s shape (`lock (_gate)` → reparse → re-render dependents → `WriteIndex`); a new
  full-rebuild entry point for Task 4 (can literally be `GenerateAll()` reused, or a thin wrapper — `GenerateAll`
  already does "wipe `OutputRoot`, rescan everything," which **is** the correct behavior for a directory-topology
  event; check whether calling it directly from the watcher, versus adding a distinctly-named method, better
  matches the existing `Regenerate*` naming convention before choosing). **Must preserve:** `_gate` locking
  discipline on every new/changed method, the "rebuild the whole output subtree for that artifact class" pattern
  already used by `GenerateAdrsInternal`/`GenerateEpicsInternal` (mirror it for the epics-deletion cleanup, don't
  invent a different pattern).
- **`src/SpecScribe/ForgeOptions.cs`** — `DebounceInterval = 400ms` (line 47) is the single source of truth for
  the debounce window; reuse it for the new sprint/topology watchers, don't hardcode a second value.
- **`src/SpecScribe/SprintTemplater.cs`, `ActionItemsTemplater.cs`, `SiteNav.cs`** — read but likely do not need
  edits; `SiteNav.Build`'s `hasSprint` parameter already exists and is what needs re-deriving on sprint
  appear/disappear (Task 2). Confirm `SprintTemplater.RenderIndex`'s signature/null-tolerance before assuming
  it needs a guard — read it before writing the epics-null branch in Task 3.

### What must be preserved (regression guard — the system must work end-to-end)

1. **Existing debounce/routing behavior for ordinary `.md` edits is unchanged** — a single story file save still
   goes through `GenerateOne`; an ADR edit still goes through `RegenerateAdrs`; an `epics.md`/
   `implementation-artifacts/*` edit still goes through `RegenerateEpics`. This story adds routes, it does not
   change the existing ones.
2. **`SiteGenerator._gate` remains the single writer lock.** Every new method added by this story takes it.
3. **Shared-read discipline (NFR5)** — no new read path opens a file exclusively; reuse
   `MarkdownConverter.ReadAllTextShared` for the sprint yaml (it already does, via `SprintStatusParser.ParseFile`)
   and for any new file class this story touches.
4. **Best-effort/graceful-degradation semantics (NFR2)** — a malformed or transiently-locked file during a watch
   fire must not crash the watch loop; `GenerateOneInternal`'s existing `catch (IOException)` "file busy, will
   retry" pattern and `SprintStatusParser`'s null-on-malformed pattern are the models to follow for any new error
   path.
5. **Watch loop keeps running under Ctrl+C / process exit** — `WatchCommand.RunWatchLoop`'s exit-signal handling
   is out of scope for this story; do not touch `Commands.cs`.
6. **Full `generate` (non-watch) behavior is untouched.** `GenerateAll()`'s existing full-rebuild semantics (wipe
   `OutputRoot`, rescan) are reused (Task 4), not modified.

### Scope boundaries — do NOT drift into sibling stories

- **Story 5.1 (CLI generate/watch modes, exit codes, non-interactive summary)** and **Story 5.2 (directory-scoped
  settings, `SettingsResolver`, provenance, `--show-config`)** are both `ready-for-dev`/in-flight and edit
  `Commands.cs`/`ConsoleUi.cs`/`SiteSettings.cs`. **This story does not touch those files at all** — everything
  needed lives in `FileWatcherService.cs` and `SiteGenerator.cs`. If a merge conflict surfaces anyway, flag it
  rather than guessing; there should be none by construction.
- **Epic 4 (framework generalization):** no alternate source-dir names or non-BMad layout heuristics — this
  story's directory-topology watcher reacts to *any* directory change under the existing configured roots, it
  does not add new root-discovery logic.
- **Do not build a generic "file system diff" abstraction.** The three new gaps (sprint yaml, epics deletion,
  directory topology) are handled as three targeted additions to the existing `IsAdr`/`IsEpicsRelated`-style
  routing, not as a rewritten watcher architecture.

### Requirements traceability

- **FR8** (epics.md:39): "Provide reliable watch-mode regeneration when source files change, including rapid
  successive edits." — the story's spine; AC #1/#6 and Task 5 are direct restatements.
- **NFR2** (epics.md:57): "Generation is resilient to partial, malformed, unsupported, or missing artifacts and
  degrades gracefully with non-fatal notices." — Task 3 (missing epics.md) and Task 2 (malformed/missing sprint
  yaml, already handled by `SprintStatusParser`) are direct applications.
- **NFR5** (epics.md:60): "Source files are read with shared access and watch mode must not hold write locks on
  observed files." — already satisfied (see "already correct" list); AC #1 pins it as a regression guard.

### Project Structure Notes

- All watch/generation code is flat under `src/SpecScribe/` — no new subfolder; `FileWatcherService.cs` and
  `SiteGenerator.cs` are edited in place, matching every prior story's convention in this codebase.
- Tests: `tests/SpecScribe.Tests/`, xUnit `[Fact]`, one file per subject, temp-dir fixtures via
  `Directory.CreateTempSubdirectory` with `IDisposable` cleanup (see `SiteGeneratorSprintTests.cs` for the
  closest existing pattern — source/adrs/site temp layout, `IDisposable.Dispose()` deleting the temp root).
  `FileWatcherServiceTests.cs` is new; it needs real filesystem events and real `Task.Delay`s past
  `ForgeOptions.DebounceInterval` (400ms) — keep individual delays short and bounded so the suite doesn't become
  slow/flaky; a few hundred ms per scenario is expected and acceptable given the debounce window itself.

### Technology / library specifics (verified against `SpecScribe.csproj`)

- **.NET `net10.0`**; `System.IO.FileSystemWatcher` (BCL) is the only watch primitive in use — no third-party
  file-watching library. `NotifyFilters.DirectoryName` (Task 4) is a BCL enum value already available; no new
  package needed.
- **`System.Collections.Concurrent.ConcurrentDictionary`** already backs `_pending` — reuse it for the new
  sentinel-keyed topology debounce (Task 4) rather than adding a second dictionary.
- No new NuGet dependency is expected for any task in this story.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.3] — story statement + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md:39] — FR8 (reliable watch regeneration, rapid edits).
- [Source: _bmad-output/planning-artifacts/epics.md:57] — NFR2 (graceful degradation).
- [Source: _bmad-output/planning-artifacts/epics.md:60] — NFR5 (shared reads, no write locks).
- [Source: src/SpecScribe/FileWatcherService.cs] — debounce + routing (primary edit target).
- [Source: src/SpecScribe/SiteGenerator.cs] — `_gate`-guarded regeneration methods (primary edit target;
  `RegenerateEpics`/`RegenerateAdrs`/`GenerateOne`/`RemoveFor`/`GenerateAll` are the patterns to mirror).
- [Source: src/SpecScribe/MarkdownConverter.cs:110] — `ReadAllTextShared` (NFR5 primitive, reuse as-is).
- [Source: src/SpecScribe/ForgeOptions.cs:47] — `DebounceInterval` (single source of truth, reuse).
- [Source: src/SpecScribe/SprintStatusParser.cs] — malformed/missing → null pattern to mirror for NFR2.
- [Source: tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs] — closest existing temp-dir fixture pattern.
- [Source: _bmad-output/implementation-artifacts/5-2-directory-scoped-settings-with-interactive-and-cli-parity.md]
  — sibling story; explicitly notes 5.3 owns `FileWatcherService`/`SiteGenerator.Regenerate*` and it must not
  touch them — confirms the file boundary above.

### Git Intelligence (recent work patterns)

- `FileWatcherService.cs` and `SiteGenerator.cs` were both last touched in `7ccba21` ("Iterating and planning")
  and before that `ae549d5`/`7aac29c`/`3efceca`/`5672289` — all planning/dev-work commits, none in-flight against
  these two files right now. No live merge risk from a concurrent story (5.1/5.2 explicitly stay out of these
  files per their own Dev Notes — see Scope boundaries above).
- Convention across the codebase: every artifact class (`docs`, ADRs, epics/stories, retros, sprint) gets its own
  `Regenerate*`/`Write*` method following the same shape — parse → render dependents → `WriteIndex`, all under
  `_gate`. Follow that shape exactly for `RegenerateSprint()` rather than inventing a new one.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- `dotnet build src/SpecScribe/SpecScribe.csproj` — green.
- `dotnet test --filter FullyQualifiedName~SiteGeneratorEpicsRemovalTests` — 8/8.
- `dotnet test --filter FullyQualifiedName~FileWatcherServiceTests` — 8/8.
- `dotnet test --filter FullyQualifiedName~FileWatcherServiceCrashGuardTests` — 11/11 (follow-up pass).
- Full suite, FINAL: **2277 passed / 4 failed / 4 skipped (2285)**. All 4 are the pre-existing git-fixture load
  flake and pass in isolation (35/35). `GoldenContentFingerprint` now PASSES — Story 20.3 regenerated it on their
  side, which confirms the drift was theirs and that leaving it alone was correct. All 27 tests added by this story
  pass, in isolation and in the full run.
- (Earlier full runs: 2263/8 before the crash-guard follow-up; 2254/17 before the concurrency-test parallelism cap.)

### Completion Notes List

**Two of the six tasks were already shipped by Story 6.11 — the story's Dev Notes predate it.**

Tasks 1 and 2 describe `FileWatcherService.CreateWatcher` as hard-coding `Filter = "*.md"` and call for a new
`RegenerateSprint()`. Both were closed before this story started:

- The watcher already admits `*.md`, `*.yaml`, `*.yml`, `*.toml` on `SourceRoot` via the `Filters` collection
  (`FileWatcherService.cs:40`), with a second `WatchedExtensions` gate inside `Debounce`.
- The dispatch already checks `IsDataSource` **first** and routes to `RegenerateFromDataSource`
  (`SiteGenerator.cs:659`), specifically so `sprint-status.yaml` cannot be claimed by `IsEpicsRelated` and
  routed to `RegenerateEpics`, which by design never re-parses sprint state (AD-5).
- `SiteGeneratorDataSourceTests.cs` already covers the classifier and the route.

**A narrower `RegenerateSprint()` was deliberately NOT added.** `RegenerateFromDataSource` runs a full
`GenerateAll()`, which wipes `OutputRoot` first — so it satisfies AC #4's *removal* half for free. The targeted
refresh the task sketches (re-parse `_sprint`, re-run `WriteSprint`/`WriteActionItems`/`WriteIndex`) would
re-introduce exactly the write-but-never-remove asymmetry Task 2's own second bullet asks to fix, since none of
those writers delete a previously-written page. Story 6.11 already recorded scoping this to the changed family as
a deferred *performance* follow-up (R6.4), explicitly "not correctness". Task 1/2's AC (#4) is instead closed with
the end-to-end coverage that was genuinely missing — add / edit / **remove** of `sprint-status.yaml` driven through
a real `FileSystemWatcher`.

**The genuine delta this story adds:**

1. **Task 3 — `epics.md` deletion cleanup** (`SiteGenerator.RegenerateEpics`, new `ClearEpicsFamilyOutputs`).
   Every epics-family writer no-ops when its model is null, which prevents *writing* a stale page but never
   *removes* one already on disk. Harmless under `GenerateAll` (it wipes `OutputRoot` first); the actual
   watch-mode bug. Now deletes `epics.html`, `epics/`, `requirements.html`, `requirements/`, plus
   `traceability.html`, `cadence.html`, `impact-map.html`, `work-graph.html` — the task named the first four, but
   all eight ride the same `_epicsModel is null` gate and are one staleness class. Nav is rebuilt *after* the
   clear (the Work Graph gate reads `_workGraph`), and returns `Removed` instead of `Skipped` — **but only when
   something was actually torn down**, so a project that simply has no `epics.md` keeps its long-standing
   `Skipped` no-op.

2. **Task 4 — directory-topology escalation** (`FileWatcherService.CreateDirectoryWatcher` +
   `SiteGenerator.RegenerateTopology`). A filter-less `NotifyFilters.DirectoryName` watcher per root; all its
   events debounce onto one sentinel key (`"<topology>"` — illegal in a Windows path, so it can never collide
   with a real file's key) and escalate to a full rebuild. Open Question #1: took the **named-wrapper** variant
   rather than calling `GenerateAll` from the watcher, so the log reads `<directory change> · full rebuild` and
   the naming matches the existing `Regenerate*` convention.

3. **Task 5 — two real hardening findings, both surfaced by the new tests, not by inspection.**
   - **The watch loop could be killed by any escaping exception.** The debounce `Timer` callback ran the
     regeneration routes unguarded on a ThreadPool thread — no caller, so an unhandled exception terminates the
     whole `watch` process rather than failing one rebuild. Now wrapped in `RunGuarded`, which converts it to an
     `Error` event (NFR2, and Dev Notes preserved-behavior #5).
   - **`CopyEmbeddedAsset` could throw a transient sharing violation.** It is re-run by *every* route, so it is
     the one file a live session rewrites on every save — the concurrency test reproduced a real
     `IOException: being used by another process` on `specscribe.js`. Now retries briefly and, if the file is
     already present and non-empty, accepts it (its bytes are fixed at build time, so an existing copy cannot be
     stale). Only a genuine never-succeeded first write still throws.

4. **A dangling-link defect the AC-#3 test caught that inspection did not.** The task list says to clear
   `_progress = null`. That is wrong in both directions: the dashboard's "Progress by Epic" mosaic reads
   `ProgressModel.PerEpic`, **not** `_epicsModel`, so leaving `_progress` intact keeps a panel of cards linking
   into the `epics/` subtree that was just deleted; but nulling it outright strips the Insights nav entries while
   the git-insights / deep-analytics pages (mined from git, not from `epics.md`) stay valid on disk — the same
   nav↔page divergence pointing the other way. `_progress` is now **split**: epic roll-ups zeroed, `Git`/`DeepGit`
   preserved.

5. **`sprint.html` / `action-items.html` degrade in place — re-rendered, not left alone.** Open Question #2's
   chosen default was "keep the page". "In place" has to mean re-rendered: both were written with live links into
   the epics pages the cleanup deletes, so leaving the old bytes swaps one dangling-link class for another. Both
   templaters already accept a null `EpicsModel`.

6. **New `IsUnderOutputRoot` guard.** The directory watcher introduces a hazard the file watchers never had:
   `GenerateAll` recreates the whole output tree, so an `--output` pointed *inside* a watched source root would
   have each rebuild re-arm the topology timer forever. Topology events under `OutputRoot` are ignored; a test
   asserts the rebuild count stops growing.

### Follow-up pass: the crash guard was INCOMPLETE as first shipped (owner-requested audit)

An audit of every callback in the codebase with the "runs on a thread with no caller" shape found that the whole
surface is `FileWatcherService` — and that this story's own first fix covered only part of it.

**`RunGuarded` guarded the generator call, then handed the result to `_onEvent` OUTSIDE the try.** `_onEvent` is a
caller-supplied delegate invoked on the same unguarded ThreadPool thread; in production it is
`ConsoleUi.LogEvent`, which writes to the console. A closed stdout — piping `specscribe watch` into a process that
exits first is the ordinary way to get one — throws `IOException` from that write and takes the process down.
**This failure is worse than the one the original fix addressed, because it fires on the SUCCESS path:** a
perfectly good rebuild kills the session while reporting itself. Every event now leaves through one `SafeNotify`
seam. The swallow there is deliberate and documented — the reporting channel is the thing that just failed, so
there is no second channel to report to, and losing a log line beats losing the session.

Also closed in the same pass:

- **The raw `FileSystemWatcher` event handlers were unguarded** (`Changed`/`Created`/`Deleted`/`Renamed` on all
  three watcher kinds). They run on the watcher's own dispatch thread — same no-caller situation. Bodies are tiny,
  but `new Timer` and the path helpers can throw under resource pressure, and "rare" is not "never" for a process
  meant to run for hours. All now route through `SafeHandle`.
- **`OnConfigDirCreated`'s `EnableRaisingEvents` sat outside its own try**, so a `_bmad` directory vanishing at
  exactly the wrong moment escaped. Covered by the same `SafeHandle` wrapper.
- Three scattered watcher-label literals promoted to `FileWatcherLabel` / `DirectoryWatcherLabel` /
  `BmadDirWatcherLabel`, now shared by the `Error` channel and the crash guard.

**Testability note — why the timer bodies became `internal` seams.** These guards only matter where there is no
caller, which is exactly what makes them hostile to test through a real `Timer`: a regression would not fail an
assertion, it would **take down the test host**, converting one broken guard into a lost suite run reported as
infrastructure noise rather than as the defect. The two timer bodies were extracted to `RunDebouncedPass(path)` /
`RunTopologyPass()` so tests drive them synchronously and an unguarded throw surfaces as an ordinary, attributable
failure. This mirrors the `OnConfigDirCreated` seam Story 6.11 added for the same reason. Timer callbacks are now
pure bookkeeping plus a call to the seam.

**11 tests in the new `FileWatcherServiceCrashGuardTests.cs`**, covering: reporter-throws on both passes and on the
error path (the nastiest ordering — route fails *and* reporter fails, where a route-only guard still dies);
route-throws reported as an `Error` event with correct artifact attribution; recovery once the fault clears; clean
`Dispose` after the guards have fired. Two counter-tests keep the guards honest — a normal pass must still report a
normal non-`Error` outcome and still write its page, so the suite cannot be satisfied by swallowing everything.

*One trap worth recording:* the first version of the fault injection created a **directory** named
`as-directory.md`, assuming the read would fail. It does not — `File.Exists` returns false for a directory, so the
route takes the `RemoveFor` branch and returns a tidy `Skipped`. That test passed while exercising nothing. The
injection now replaces the output **root** with a plain file, so `EnsureScaffold`'s `Directory.CreateDirectory`
throws — a realistic stand-in for the filesystem faults a long-running session actually meets.

**AC #6's optional coalescing clause was considered and NOT done — deliberately.** Its final sentence asks that
"redundant back-to-back full-scope rebuilds (e.g. `RegenerateEpics` firing once per touched story file in the same
burst) are reduced where cheap to do so **without changing observable behavior**." Those two conditions conflict.
`_pending` is keyed by full path, so the only cheap way to collapse an N-story-file burst into one `RegenerateEpics`
is to route every epics-related path onto a shared sentinel key — exactly the Task 4 mechanism. But that *is* an
observable change: the event stream would carry one event instead of N, and the per-file `RelativePath` each event
reports today (what `ConsoleUi.LogEvent` prints and what a watch user reads to confirm their save landed) would be
replaced by a sentinel. The correctness half of AC #6 is fully satisfied and now tested (`_gate` serializes; the
burst test proves convergence); the efficiency half is a genuine optimization but not a cheap one, and it belongs
with Story 6.11's already-deferred R6.4 scoped-re-render work rather than being smuggled in here. Flagging rather
than silently skipping.

**Scope held.** No edits to `Commands.cs`, `ConsoleUi.cs`, `SiteSettings.cs`, or `SettingsResolver.cs` (Stories
5.1 / 5.2), no new discovery heuristics (Epic 4), no new NuGet dependency, no rewritten watcher architecture.

**Full-suite accounting — 2263 passed / 8 failed / 3 skipped.** None of the 8 is a regression from this story:

- **1 × `GoldenContentFingerprint`** — Story 20.3's rendering drift, deliberately not regenerated (next paragraph).
- **7 × git-fixture tests** — the known pre-existing parallel-load flake. All 7 pass in isolation (51/51), and the
  **failing set differs run to run**: the first full run failed `SiteGeneratorGitInsightsTests` /
  `SiteGeneratorCodeInsightsTests` / `SiteGeneratorCodeMapTests` / `SiteGeneratorGroupedNavTests` /
  `SiteGeneratorHowToReadTests`, the second failed `SiteGeneratorCommitDetailsTests` /
  `GitMetricsFirstCommitDateTests` / `SiteGeneratorChangeLogDateLinkTests` instead — with most of the first set now
  green. A shifting failure set that is green in isolation is load, not breakage. Story 20.2's record already
  flagged this class ("two git-fixture tests flake under parallel full-suite load, a different one each run, both
  green in isolation — pre-existing, deserves its own pass").
- **Contribution owned and fixed:** the FIRST full run failed **17**, and the extra ~9 were partly mine — this
  story's concurrency test originally ran an uncapped `Parallel.For(0, 12)` of full rebuilds, saturating every core
  and starving the git tests (which spawn real `git` processes under their own timeouts). Capped to
  `MaxDegreeOfParallelism = 4`; the count dropped 17 → 8 and the run time 5m01s → 2m39s. Contention is still
  exercised — four concurrent writers interleave every route pair.
- **1 genuine expected-value update:** `SiteGeneratorSpaTests.RegenerateEpics_ReEmitsTheSpaSite_EvenWhenEpicsSource
  IsMissing` asserted `Skipped` for the delete-epics.md-after-a-full-gen case. AC #3 deliberately makes that
  `Removed`. Only the pinned outcome changed; the test's actual subject (the SPA manifest nav must not lag the
  rewritten index) is untouched and still passing.

**✅ RESOLVED — `GoldenContentFingerprint` now passes.** Story 20.3 regenerated the constant on their side, which
confirms the diagnosis below and that declining to regenerate it here was the right call. The original analysis is
kept because the reasoning is the reusable part:

**⚠️ `GoldenContentFingerprint` was RED, and was deliberately NOT regenerated.** It drifted to
`253fe05c…` — but the drift was Story 20.3's, not this story's, and 20.3 was mid-flight. Evidence:

- The fingerprint covers the shared embedded assets, and `src/SpecScribe/assets/specscribe.css`
  (+66 lines) / `specscribe.js` (+120 lines) are uncommitted 20.3 edits — the CSS hunks are literally headed
  `/* ---- Story 20.3: related-work pane ---- */`. 20.3 also added a `workGraph:` argument to
  `HtmlTemplater.RenderIndex`, which moves dashboard markup.
- **Nothing this story changed is reachable from `GenerateAll`.** `ClearEpicsFamilyOutputs`, `DeleteOutputFile`,
  `DeleteOutputDirectory` run only in `RegenerateEpics`' epics-missing branch; `RegenerateTopology` is only called
  by the new watcher; `FileWatcherService` isn't constructed by the golden fixture. The one shared touch,
  `CopyEmbeddedAsset`, writes the identical embedded-resource bytes — only its failure handling changed.

Regenerating now would lock a hash on top of another session's incomplete rendering work (it will move again when
they finish) and would silently claim their change as this story's — exactly the trap
[[golden-diff-normalization-gotchas]] and CLAUDE.md's shared-main section warn about. **Story 20.3 owns this
regeneration.** Flagging rather than papering over it.

**Concurrent-session note (per CLAUDE.md).** Story 20.3 was being implemented on shared `main` throughout this
story: `RelatedWork.cs`, `RelatedWorkTemplater.cs`, `DashboardView*.cs`, `HtmlRenderAdapter.Dashboard.cs`,
`WorkGraphTemplater.cs`, `HtmlTemplater.cs`, `specscribe.css/js` are all theirs, uncommitted, and a mid-flight
`RetroModel.EpicNumber` → `EpicNumbers` rename (`spec-multi-epic-retro-attribution.md`) broke the tree build
partway through this story. Nothing was reset or reverted; the suite was re-run once their refactor compiled. All
symbols added by this story were grep-verified present after every edit.

### File List

- `src/SpecScribe/SiteGenerator.cs` — modified (`RegenerateEpics` removal branch; new `ClearEpicsFamilyOutputs`,
  `DeleteOutputFile`, `DeleteOutputDirectory`, `RegenerateTopology`, `EpicsFamilyPages`, `EpicsFamilyDirectories`,
  `AssetWriteRetries`/`AssetWriteRetryDelayMs`; hardened `CopyEmbeddedAsset`)
- `src/SpecScribe/FileWatcherService.cs` — modified (`TopologySentinelKey`, `TopologyEventLabel`,
  `CreateDirectoryWatcher`, `DebounceTopology`, `IsUnderOutputRoot`, `CreateTopologyTimer`, `RunGuarded`; follow-up
  pass added `SafeNotify`, `SafeHandle`, `RunDebouncedPass`, `RunTopologyPass`, and the three watcher-label
  constants)
- `tests/SpecScribe.Tests/FileWatcherServiceCrashGuardTests.cs` — new (follow-up pass)
- `tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs` — new
- `tests/SpecScribe.Tests/FileWatcherServiceTests.cs` — new
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — modified (one pinned outcome `Skipped` → `Removed`; the
  test's subject is unchanged — see Full-suite accounting)

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-24 | Follow-up pass (owner-requested audit of the crash-guard class): the original `RunGuarded` fix was INCOMPLETE — it guarded the generator call but handed the result to `_onEvent` outside the try, so a throwing reporter (`ConsoleUi.LogEvent` on a closed stdout) still killed the process, and on the SUCCESS path. New `SafeNotify` is now the single exit for every event; new `SafeHandle` wraps all raw `FileSystemWatcher` handlers (previously unguarded on their dispatch thread) and `OnConfigDirCreated`'s `EnableRaisingEvents`. Timer bodies extracted to `RunDebouncedPass`/`RunTopologyPass` internal seams so a guard regression fails a test instead of taking down the test host. 11 new tests. |
| 2026-07-24 | Story 5.3 implemented. `epics.md` deletion now tears down its whole output family (8 pages + 2 subtrees) and reports `Removed`; new directory-topology watcher escalates folder create/rename/delete to a full rebuild via `SiteGenerator.RegenerateTopology()`. Tasks 1–2 found already shipped by Story 6.11 — closed with the end-to-end coverage that was missing rather than a redundant `RegenerateSprint()`. Two hardening defects fixed that the new tests surfaced: the debounce `Timer` callback ran unguarded (any transient killed the whole `watch` process) and `CopyEmbeddedAsset` could throw a transient sharing violation. `_progress` is split rather than nulled so the epic mosaic's links go without orphaning the git-insights pages. 16 new tests. |

## Open Questions (for the maintainer — non-blocking; sensible defaults chosen)

> **As-implemented resolutions** (all three still open for the owner to overrule; none is load-bearing):
>
> 1. **Took the named wrapper, not the bare `GenerateAll()`.** `SiteGenerator.RegenerateTopology()` exists so the
>    log reads `<directory change> · full rebuild` instead of a per-page event storm, and so the watcher stays thin
>    — it also matches the `Regenerate*` naming every other route uses. The rebuild body is `GenerateAll()`
>    unchanged, so the behaviour is exactly the sketched default; only the reporting differs.
> 2. **Degrade in place — but re-rendered, which the question's wording did not distinguish.** Leaving the existing
>    `sprint.html`/`action-items.html` bytes alone would have kept live `href="epics/epic-1.html"` links into the
>    subtree just deleted, so "keep the page" only makes sense as "rewrite the page with a null epics model". A
>    test pins it.
> 3. **Both roots, as proposed.** Symmetric with the `.md` watcher pair.
>
> A fourth decision the story did not anticipate: **the "Progress by Epic" mosaic forced `_progress` to be split
> rather than nulled** (Completion Notes #4). That one is not a preference — either alternative leaves a
> nav↔page divergence.

1. **Full-rebuild entry point for directory-topology changes (Task 4): reuse `GenerateAll()` directly, or add a
   distinctly-named wrapper?** Default chosen: reuse `GenerateAll()` as-is (it already does exactly "wipe
   `OutputRoot`, rescan everything," which is the correct response to a topology change) — the only new code is
   the watcher plumbing that calls it and reports the resulting events through `_onEvent`. If you'd rather have a
   named `RegenerateTopology()` wrapper for clearer log output ("full rebuild" vs. the generic `GenerateAll`
   event shape), say so; the routing is identical either way.
2. **`epics.md` deletion (Task 3): does the sprint page disappear entirely, or render an "epics unavailable"
   degraded state?** Default chosen: **degrade in place** — `SprintTemplater.RenderIndex` already accepts a
   nullable `EpicsModel`, so the sprint page is kept and simply loses story/epic cross-links rather than being
   deleted. Say so if you'd rather the page disappear entirely when epics.md is gone (a stricter reading of AC
   #3's "removed").
3. **Directory-topology watcher scope: both `SourceRoot` and `AdrSourceRoot`, or `SourceRoot` only?** Default
   chosen: both, symmetric with the existing per-root `.md` watcher pair — an ADR directory rename is just as
   much a topology change as a source directory rename. Say so if ADR directory renames are rare/out-of-scope
   enough to skip for now.
