---
title: 'Story 6.11 deferred-debt cleanup'
type: 'bugfix'
created: '2026-07-21T00:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Story 6.11 deferred-debt cleanup

## Intent

**Problem:** Story 6.11 (file-change reactivity hardening) and its code review left 7 items open in `deferred-work.md`, all tagged `6-11-file-change-reactivity-hardening`: (1) core `watch`-mode can't re-brand on a `config.toml` project-name change without a restart; (2) the data-source route uses a full `GenerateAll()` (R6.4 scoped re-render, tracked separately); (3) the `_bmad` config-dir watcher only registers if the directory exists at `FileWatcherService` construction time; (4) `IsProjectConfigFile`'s doc comment overclaims "any depth" `_bmad`-segment watching; (5) `publishDiagnostics` (Story 6.12) allegedly doesn't reuse the `lastRepoRoot` convention; (6) `SerializeDiagnostics` (Story 6.12) allegedly assumes every source-anchored notice lives under `SourceRoot`; (7) `parseDiagnostics`/severity mapping (Story 6.12) silently downgrades any unrecognized severity to `Warning` with no `message`-type validation.

**Approach:** Investigated all 7 before touching code. Items 5 and 6 were **already fixed** by commits `fdf9056` and `8303e52` (both 2026-07-12, before this cleanup ever started) — confirmed by reading the current `publishDiagnostics`/`SerializeDiagnostics` source, which already carry the fix and a `[Review][Patch]` marker; `deferred-work.md` had simply never been updated to reflect it. Items 1 and 2 were re-reviewed and left **intentionally deferred**: item 2 is explicitly out of scope per the story itself ("split, don't absorb" — R6.4 tracks it); item 1's obvious-looking fix (mutate `ForgeOptions.SiteTitle` in place before re-render) turns out to need new state — a way to know whether an explicit `--project-name` CLI override was given, so a config.toml re-read never silently clobbers a deliberate override — which is a real contract change, not the small mutation it first appears to be. Items 3, 4, and 7 got real fixes:
- **Item 3** (`FileWatcherService.cs`): when `_bmad/` doesn't exist at construction, a new non-recursive watcher on the repo root (directory-name events only, filtered to `_bmad`) detects its later creation and registers the real config-dir watcher on demand via `OnConfigDirCreated` (internal, idempotent, live-started if the service is already running). A new `WatcherCount` test seam lets this be asserted deterministically without racing a real `FileSystemWatcher` event, matching the fixture's existing "no reliance on real FS-event timing" convention.
- **Item 4** (`SiteGenerator.cs`): `IsProjectConfigFile`'s doc comment now says plainly that it's classification-only and that only the repo-root `_bmad` dir is ever watched, instead of implying the classifier's "any depth" reach is matched by the watcher.
- **Item 7** (`extension.ts`): `parseDiagnostics` now admits a record only when `severity` is exactly `'error'` or `'warning'` and `message` is a string, dropping anything else instead of letting it fall through to `publishDiagnostics`'s ternary and get silently recolored as `'warning'`. `RawDiagnostic.severity` is typed `'error' | 'warning'` to match.

All 7 entries in `deferred-work.md` updated: 3 struck through as newly resolved, 2 struck through as already-resolved-but-unmarked, 2 left open with a re-review note explaining why they still don't get code changes.

**Adversarial review (Blind Hunter) and follow-up hardening.** A review pass on item 3's implementation surfaced real correctness gaps in the first cut, patched before commit: (a) the registration flag was set *before* the fallible `FileSystemWatcher` construction, so a `_bmad`-deleted-mid-registration race could crash the watcher thread — fixed by moving the flag-set to after success and catching the construction failure as a reported `Error` event instead; (b) a `_bmad`-creation event already queued when `Dispose()` runs could leak a never-disposed watcher — fixed with a `_disposed` guard checked under the same lock `Dispose()` now sets it under; (c) the fallback repo-root watcher was left running forever after successful registration — fixed by retiring (disabling + disposing) it once the real config watcher is in place; (d) the new test never exercised the `_started == true` branch (the realistic production state, since `Start()` runs immediately after construction) — fixed by calling `Start()` first. Two findings were reviewed and intentionally left as-is rather than patched: the construction-to-`Start()` window and the lack of re-arm on a `_bmad` delete-then-recreate are both real but match the class's pre-existing, already-accepted narrowness (every watched entity has the same construction-to-`Start()` gap; the original deferred item was already scoped as "narrow, real projects have `_bmad` before `watch` starts") — fixing them would mean redesigning `Start()`/`Stop()` semantics for the whole class, out of proportion to a low-severity deferred item. `deferred-work.md`'s item-3 resolution note was reworded from an unqualified "RESOLVED" to "NARROWED, not eliminated" to record this honestly rather than implying the gap is now airtight.

## Code Map

- `src/SpecScribe/FileWatcherService.cs` — fallback repo-root watcher for a not-yet-existing `_bmad` dir; `OnConfigDirCreated` (internal, success-gated flag, retires the fallback watcher, reports construction failure instead of throwing); `WatcherCount` (internal, test-only); `Start`/`Stop`/`Dispose` lock around `_watchers`, `Dispose` sets a `_disposed` guard to prevent a post-teardown leak
- `src/SpecScribe/SiteGenerator.cs` — `IsProjectConfigFile` doc comment narrowed to match actual watch behavior
- `extension/src/extension.ts` — `RawDiagnostic.severity` typed `'error' | 'warning'`; `parseDiagnostics` validates `severity` and `message`, not just `path`
- `tests/SpecScribe.Tests/SiteGeneratorDataSourceTests.cs` — new test for the dynamic `_bmad`-dir registration
- `_bmad-output/implementation-artifacts/deferred-work.md` — story-6-11 sections (~597–610), all 7 items addressed (3 resolved, 2 marked already-resolved, 2 re-reviewed and left open)

## Suggested Review Order

**Dynamic `_bmad`-dir detection**
- Fallback watcher + on-demand registration, idempotent and live-start-aware.
  [`FileWatcherService.cs:37`](../../src/SpecScribe/FileWatcherService.cs#L37)
- Deterministic test via the internal `OnConfigDirCreated`/`WatcherCount` seam.
  [`SiteGeneratorDataSourceTests.cs:148`](../../tests/SpecScribe.Tests/SiteGeneratorDataSourceTests.cs#L148)

**Diagnostics severity/message validation**
- `parseDiagnostics` now drops malformed/unrecognized records instead of admitting them for `publishDiagnostics` to silently recolor.
  [`extension.ts:1252`](../../extension/src/extension.ts#L1252)

**Doc-comment correction**
- `IsProjectConfigFile` no longer overclaims watch depth beyond classification.
  [`SiteGenerator.cs:471`](../../src/SpecScribe/SiteGenerator.cs#L471)

**Tracking**
- All 7 story-6-11 deferrals addressed in `deferred-work.md`: 3 newly resolved, 2 discovered already-resolved (dated to their real fix commits), 2 re-reviewed and kept deferred with reasoning.
  [`deferred-work.md:597`](./deferred-work.md#L597)

## Verification

- `dotnet build src/SpecScribe` — clean, before and after the review hardening pass.
- `dotnet test tests/SpecScribe.Tests` — 1930/1935 passed (re-run after the hardening pass, including the updated `FileWatcherService_ConfigDirCreatedAfterConstruction_DynamicallyRegistersConfigWatcher`); the 5 failures (`IconsTests`, `GoldenContentFingerprint`, `GoldenOutputInventory`, 2× `HtmlTemplaterTests`) are pre-existing on `main` HEAD (`d9222c7`, in-progress Risk Quadrant work), reproduced identically with this change's files stashed out. No golden-fingerprint regression from this change (comment-only `SiteGenerator.cs` edit, non-rendering `FileWatcherService.cs` edit).
- `cd extension && npm run typecheck && npm run build` — clean, before and after the review hardening pass.
- Adversarial review (Blind Hunter) pass applied — see Intent § "Adversarial review and follow-up hardening."
