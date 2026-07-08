# Story 5.3: Watch Regeneration Safety and Scope-Aware Rebuilds

Status: ready-for-dev

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

- [ ] **Task 1 — Watch the sprint status file, not just `*.md` (AC: #4)**
  - [ ] `FileWatcherService.CreateWatcher` hard-codes `Filter = "*.md"`. Add a second filter (or a second watcher
    on `options.SourceRoot` with `Filter = "sprint-status.yaml"`, `IncludeSubdirectories = true`) so changes to
    the well-known sprint file are observed. Route its debounced fire to a new `SiteGenerator` method (see Task 2)
    rather than `GenerateOne`/`IsEpicsRelated` (a `.yaml` file is not markdown and must not go through
    `MarkdownConverter`).
  - [ ] Keep the existing `.md` watcher on `SourceRoot`/`AdrSourceRoot` untouched — this is an additive watcher,
    not a filter change, so `*.md` behavior is unaffected.

- [ ] **Task 2 — `SiteGenerator.RegenerateSprint()`: targeted sprint refresh (AC: #4)**
  - [ ] Add a `GenerationEvent RegenerateSprint()` method (mirrors `RegenerateAdrs`'s shape): under `_gate`,
    re-parse `SprintSourcePath` into `_sprint`, then re-run `WriteSprint(nav)`, `WriteActionItems(nav)`, and
    `WriteIndex(nav)` (the home widget reads `_sprint`) using the cached `_nav`/`_epicsModel`/`_docs` — do **not**
    re-run the epics phase or the full file scan. `_nav` itself must be rebuilt too (via `BuildNav`) because
    `SiteNav.Build`'s `hasSprint` parameter is derived from `SprintAvailable`, which changes when the yaml
    appears/disappears — reuse the same `BuildNav(sourceRelatives)` call `RegenerateEpics` already makes,
    sourcing `sourceRelatives` from `EnumerateSourceFiles()`.
  - [ ] If `_sprint` becomes null (file deleted or now malformed) and a `sprint.html` / `action-items.html` /
    `retros.html` exist on disk from a prior pass, delete them — `WriteSprint`/`WriteActionItems` already no-op
    (return without writing) when `_sprint`/`open` is null/empty, which today only matters on a fresh
    `GenerateAll` (which wipes `OutputRoot` first); in the incremental watch path nothing currently deletes a
    page that was previously written and is now stale. Add that deletion here (Task 4 below adds a small shared
    helper for "delete this output file if the model says it shouldn't exist").
  - [ ] Wire `FileWatcherService`'s new sprint-file debounce branch (Task 1) to call `_generator.RegenerateSprint()`.

- [ ] **Task 3 — `epics.md` deletion must clean up its whole output subtree (AC: #3)**
  - [ ] `RegenerateEpics()` currently: if `FindEpicsSourceFile(files)` is null, it calls `WriteIndex(nav)` and
    returns a `Skipped` event — but `epics.html`, `epics/*.html`, `requirements.html`, `requirements/*.html`, and
    `sprint.html` (which links into the epics model) are left on disk from the prior run, and `_epicsModel`,
    `_progress`, `_requirements` stay populated with stale in-memory data even though their source is gone.
  - [ ] When `epicsSourceFile is null` in `RegenerateEpics()`: delete `epics.html` if present, delete the
    `epics/` output directory if present (mirrors the existing `Directory.Delete(epicsDir, recursive: true)`
    pattern already used inside `GenerateEpicsInternal`), delete `requirements.html` and the `requirements/`
    directory if present, clear `_epicsModel = null`, `_progress = null`, `_requirements = null`, then call
    `RegenerateSprint()` (Task 2) so the sprint page — which reads `_epicsModel` for story links — also
    re-resolves against "no epics." `SprintTemplater.RenderIndex(SprintStatus sprint, EpicsModel? epics, ...)`
    already takes a nullable `EpicsModel` (verified against current signature), so no guard is needed — the page
    stays and degrades gracefully (no story/epic links) rather than disappearing. Finally `WriteIndex(nav)`.
  - [ ] Return a `GenerationEvent(GenerationOutcome.Removed, "epics.md", sw.Elapsed, "epics.md removed")` in this
    branch instead of the current `Skipped` — this is a real destructive change to the output tree, not a no-op.

- [ ] **Task 4 — Directory-level topology changes escalate to a full rebuild (AC: #2, #5)**
  - [ ] `FileSystemWatcher.Filter = "*.md"` does not match a bare directory name on rename/create/delete, so a
    whole-folder operation (e.g. renaming `implementation-artifacts/` itself, or deleting a subfolder full of
    stories) currently produces **no watcher event at all** for the folder operation — only individual contained
    files might (platform-dependent; on Windows a folder rename typically does not enumerate its children as
    separate events). Add a second, filter-less `FileSystemWatcher` per watched root
    (`NotifyFilter = NotifyFilters.DirectoryName`, `IncludeSubdirectories = true`) whose `Created`/`Deleted`/
    `Renamed` handlers debounce a **sentinel key** (e.g. `"<topology>"` — a constant, not a file path) through the
    existing `_pending` dictionary, distinct from any real file path.
  - [ ] When the topology-sentinel timer fires, do **not** attempt to classify a single path (there isn't one
    that means anything) — call a new `SiteGenerator.RegenerateAll()`-equivalent entry point that reuses
    `GenerateAll`'s full-rebuild body (wipe `OutputRoot`, rescan, rebuild everything) under `_gate`, and surface
    it as a single `GenerationEvent(GenerationOutcome.Updated, "<directory change>", elapsed, "full rebuild")` to
    `_onEvent`. This is the concrete meaning of "rebuild scope escalates when required for coherence" in AC #2 —
    directory topology is exactly the case the per-file incremental path structurally cannot handle correctly.
  - [ ] Keep this escalation coarse and rare-path: it should not fire for ordinary file edits (those still go
    through the existing per-file/`IsEpicsRelated`/`IsAdr` routing), only for actual directory create/rename/
    delete events. Debounce it with the same `ForgeOptions.DebounceInterval` so a burst of directory churn (e.g.
    an IDE rename-refactor touching many nested files) still collapses to one rebuild.

- [ ] **Task 5 — Burst-safety hardening pass (AC: #1, #6)**
  - [ ] Audit (do not rewrite from scratch — see Dev Notes "already correct" list below) that every write path
    reachable from a debounce fire still goes through `SiteGenerator._gate`. Task 2's `RegenerateSprint` and Task
    4's full-rebuild entry point must both take the same lock as `GenerateOne`/`RegenerateEpics`/`RegenerateAdrs`
    — no new unlocked write path.
  - [ ] Confirm (via a new test, Task 6) that concurrent debounce fires for *different* files — e.g. a story file
    and `epics.md` changing in the same burst — never interleave writes: because both routes take `_gate`, the
    two regenerations serialize; the test should assert the final on-disk state is fully coherent (no partial
    HTML, no file left mid-write) regardless of fire order, not that a specific order occurred.
  - [ ] No functional change is expected here if the audit finds everything already gated — this task exists to
    add the regression test coverage that AC #1/#6 currently lack, not to introduce new locking.

- [ ] **Task 6 — Tests (AC: #1–#6)**
  - [ ] New `FileWatcherServiceTests.cs` (temp-dir fixture, mirrors `SiteGeneratorSprintTests`'s style): construct
    a real `FileWatcherService` over a temp `_bmad-output`/`docs/adrs` pair with a real `SiteGenerator`, `Start()`
    it, and drive scenarios with short `Task.Delay`s past `ForgeOptions.DebounceInterval`:
    - editing an existing story file → `GenerateOne` path fires, output updates;
    - deleting `epics.md` → `epics.html`/`epics/`/`requirements.html`/`requirements/` are gone, home index has no
      epics widget, no exception;
    - adding/editing/removing `sprint-status.yaml` → `sprint.html` appears/updates/disappears accordingly;
    - renaming a whole subdirectory of story files → full-rebuild path fires and every renamed file's page exists
      at its new location with no orphan at the old one.
  - [ ] Extend `SiteGeneratorSprintTests.cs` or add `SiteGeneratorEpicsRemovalTests.cs` (headless, no watcher) for
    Task 3's `RegenerateEpics()` deletion branch directly — call `GenerateAll()` with an epics.md present, then
    delete it from the temp source dir and call `RegenerateEpics()` directly, asserting the output files are
    gone and the returned event is `Removed`.
  - [ ] A concurrency test for Task 5: spin up N threads (or `Parallel.For`) each calling a different
    `SiteGenerator` write method (`GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`) against the same instance
    and temp output dir; assert no exception, no torn/empty HTML file, and a final `GenerateAll()` pass (ground
    truth) produces output byte-identical in structure (same file set) to what the concurrent run converged to
    for any file that didn't change between the two passes.
  - [ ] Run the full suite: `dotnet test` from repo root; all existing tests stay green — especially
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Open Questions (for the maintainer — non-blocking; sensible defaults chosen)

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
