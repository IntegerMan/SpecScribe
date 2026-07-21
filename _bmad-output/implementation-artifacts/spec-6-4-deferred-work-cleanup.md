---
title: 'Story 6.4 deferred-work cleanup (webview runtime hardening + scoped re-render)'
type: 'chore'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '96fc5637cab8791b24fbf13e7c533dc0929631ab'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Eight items are open in `deferred-work.md` under Story 6.4's two review passes. Two are section-header lines with no distinct defect of their own (fold into the closure notes of the item beneath them, no code change). Five are narrow, verified-still-live hardening bugs in the webview runtime: the webview nav-toggle bridge (`WebviewRenderAdapter.cs`) drops keyboard/focus parity with the HTML surface's inline script; `Commands.RedirectOutputToScratch` keys the scratch dir by a hash of the repo root alone, so two concurrent same-repo `specscribe webview` spawns collide (`IOException`) and a case-differing repo path double-maps on case-sensitive filesystems; `SiteGenerator.RenderWebviewSurfaces` has no per-story guard around `BuildStoryPageFragments`, so a `.md` deleted between `GenerateAll()` and `RenderWebviewSurfaces()` throws and aborts the *entire* bundle instead of degrading that one story; and `extension.ts`'s `runRenderer` uses a bare `proc.kill()` (SIGTERM, unreliable against the `dotnet` host on Windows) with an uncapped stdout accumulator. The sixth item — scoped re-render — is the perf follow-up ADR 0005 §3 explicitly called out: every live-push spawns a **brand-new** `specscribe webview` process that reruns a full `GenerateAll()`, because nothing is cached across saves; `specscribe watch` already solves this exact problem for the plain CLI via `FileWatcherService`'s debounced `RegenerateEpics()` incremental path.

**Approach:** Fix the five narrow bugs at their cited call sites. For scoped re-render, add a persistent long-lived render mode (`specscribe webview --serve` or equivalent) that keeps one `SiteGenerator` alive and reuses the *existing* `FileWatcherService` debounce/`RegenerateEpics()` machinery instead of a fresh `GenerateAll()` per push; the extension keeps this one process alive for the panel's lifetime (spawn once, read a payload per regen) instead of respawning on every debounced change. Close all 8 `deferred-work.md` entries with the file's existing `RESOLVED`-strikethrough convention: 2 as non-actionable header closures, 6 as fixed.

## Boundaries & Constraints

**Always:**
- Keep Story 6.4's read-only guarantee (AC #6): the persistent process still never writes into the project's real configured output — scratch dir only, same as the one-shot spawn.
- Each of the 5 bug fixes stays scoped to its cited file — no drive-by refactors of surrounding code.
- The one-shot per-refresh spawn path keeps working unchanged; persistent mode is additive, not a replacement, so a runtime/extension version mismatch degrades to the existing behavior.
- `SerializePayload`'s JSON shape (`surfaces`/`outline`/etc.) is identical whether it comes from a one-shot spawn or a persistent-mode push — the extension's parsing code does not need two payload shapes.
- Update `deferred-work.md` in place using its existing convention (strikethrough + `**RESOLVED 2026-07-20** (...)` note); do not delete or renumber other entries.

**Ask First:** If reusing `FileWatcherService` for persistent mode requires a wire protocol beyond "one JSON payload per debounced regen on stdout, newline-terminated" (e.g., a request/response handshake, restart-on-crash policy in `extension.ts`), stop and confirm the shape before wiring `extension.ts`'s process-lifecycle management.

**Never:**
- Do not attempt cross-repo or cross-window process sharing — one persistent process belongs to exactly one webview panel/session.
- Do not change `WebviewRenderAdapter`'s CSP posture (still no non-nonce inline scripts) to fix the nav-toggle parity gap — extend the existing bundled bridge script.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Concurrent same-repo spawns | Two VS Code windows (or a manual CLI run) target the same repo simultaneously | Each spawn gets a distinct scratch dir | No `IOException` from a shared write path |
| Case-differing repo path | Same repo opened via two path-case variants on a case-sensitive FS | Scratch key does not case-fold | Two variants map to distinct (or correctly identical, OS-comparer-consistent) dirs |
| Story artifact deleted mid-render | `.md` removed between `GenerateAll()` and `RenderWebviewSurfaces()` | That one story's surface degrades to a placeholder | Bundle still completes; no unhandled exception |
| Renderer process hangs | `dotnet` host does not exit after `SIGTERM` | Escalates to a hard kill after a grace period | No orphaned process; stdout buffer stays bounded |
| Webview nav toggle, keyboard | User opens nav via toggle, presses Escape | Nav closes, focus returns to toggle (parity with HTML script) | N/A |
| Live-push regen (persistent mode) | Source file saved while panel is open | Payload reflects only the incremental `RegenerateEpics()`-scope change | Falls back to one-shot spawn if `--serve` unsupported/unavailable |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/WebviewRenderAdapter.cs` -- nav-toggle bridge script (~line 178) -- add Escape-close/return-focus + open-focuses-first-link parity with `HtmlRenderAdapter.NavToggleScript`
- `src/SpecScribe/Commands.cs` -- `RedirectOutputToScratch` (~line 250) -- unique-per-spawn scratch key (drop `ToUpperInvariant`, add a per-process discriminator) + `--serve` entry point reusing `FileWatcherService`
- `src/SpecScribe/SiteGenerator.cs` -- `RenderWebviewSurfaces`/`BuildStoryPageFragments` (~line 2380) -- per-story try/catch degrading to a placeholder on read failure, mirroring `RenderEpicsPages`'s degrade behavior
- `src/SpecScribe/FileWatcherService.cs` -- existing debounce/`RegenerateEpics()` loop -- reused as-is by the persistent serve mode, not modified
- `extension/src/extension.ts` -- `runRenderer` (~line 1090-1130) -- SIGKILL escalation after grace period + capped stdout accumulator; persistent-process spawn/keep-alive/dispose wiring for scoped re-render
- `_bmad-output/implementation-artifacts/deferred-work.md` -- close all 8 Story 6.4 entries (2 header closures, 6 fixed)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/WebviewRenderAdapter.cs` -- add keyboard (Escape-to-close-with-focus-return) and open-focuses-first-link handling to the webview nav-toggle bridge -- restores NFR6/AC#2 parity with the HTML surface
- [x] `src/SpecScribe/Commands.cs` -- make the scratch-dir key unique per concurrent spawn and drop the case-fold on the repo-root hash input -- eliminates same-repo collision and case-variant double-mapping
- [x] `src/SpecScribe/SiteGenerator.cs` -- wrap the per-story fragment build in `RenderWebviewSurfaces` with a try/catch that degrades to a placeholder page on read failure -- matches the resilience the HTML generation path already has
- [x] `extension/src/extension.ts` -- escalate `runRenderer`'s kill to SIGKILL after a grace period and cap the stdout accumulator -- prevents orphaned processes and unbounded memory growth
- [x] `src/SpecScribe/Commands.cs` + `src/SpecScribe/FileWatcherService.cs` + `extension/src/extension.ts` -- add a persistent `--serve`-style render mode reusing `FileWatcherService`'s debounced `RegenerateEpics()` path, and have the extension keep one process alive per panel instead of respawning per save -- delivers the ADR 0005 §3 scoped-re-render follow-up
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close all 8 Story 6.4 entries with the file's existing RESOLVED-strikethrough convention -- keeps the audit trail accurate
- [x] Add/extend unit tests for each of the 5 bug fixes (scratch-key uniqueness + case, per-story degrade-on-missing-artifact, kill-escalation/stdout-cap where testable) and at least one test proving persistent-mode output matches one-shot `SerializePayload` output for an equivalent state

**Acceptance Criteria:**
- Given two concurrent `specscribe webview` spawns on the same repo, when both run, then neither fails with an `IOException` from a shared scratch path.
- Given a repo path opened with two different letter-casings on a case-sensitive filesystem, when each is spawned, then the scratch key does not silently case-fold them together.
- Given a story `.md` artifact is deleted between `GenerateAll()` and `RenderWebviewSurfaces()`, when the bundle renders, then only that story's surface degrades to a placeholder and the rest of the bundle still completes.
- Given the renderer process does not exit after a `SIGTERM`, when the grace period elapses, then it is force-killed and no orphaned process remains.
- Given the webview nav toggle is open, when the user presses Escape, then the nav closes and focus returns to the toggle button.
- Given the panel is running in persistent-serve mode and a watched source file is saved, when the debounce fires, then the panel updates without a full-process respawn, and its payload shape matches the one-shot path's `SerializePayload` output.

## Spec Change Log

## Design Notes

Persistent mode is additive: the extension decides at spawn time whether to use one-shot (`specscribe webview`, current behavior, always available) or persistent (`specscribe webview --serve`, new). If the new mode isn't available (older CLI, older extension) the extension keeps working exactly as it does today — there is no hard cutover. The wire contract for a persistent regen push should reuse `WebviewCommand.SerializePayload` verbatim so the extension's existing JSON-parsing code needs no branch on which mode produced it.

## Verification

**Commands:**
- `dotnet test` -- expected: full suite green, including new tests for the 5 bug fixes and the persistent-mode payload-parity test
- `dotnet build extension` / relevant TS build+lint -- expected: `extension.ts` changes compile and lint clean

**Manual checks (if no CLI):**
- Open the extension against this repo, trigger a save while the panel is open, and confirm the nav toggle's Escape/focus behavior in the actual webview (CSP-restricted environment), since automated coverage for the CSP-blocked bridge script is necessarily limited.

## Suggested Review Order

**Scoped re-render (persistent `--serve` mode)**

- Entry point: the one-shot vs. persistent branch, and where a serve-mode run hands off to the loop below.
  [`Commands.cs:97`](../../src/SpecScribe/Commands.cs#L97)

- The persistent loop: reuses `FileWatcherService`'s existing debounce/`RegenerateEpics()` path; a lock serializes concurrent debounce callbacks so two files changed in the same window can't interleave stdout writes, and `Skipped`/`Error` outcomes are excluded from re-render (review patch).
  [`Commands.cs:118`](../../src/SpecScribe/Commands.cs#L118)

- The new `--serve` CLI flag.
  [`SiteSettings.cs:41`](../../src/SpecScribe/SiteSettings.cs#L41)

- `PersistentRenderer`: the long-lived connection that parses one NDJSON payload per line, with a size-capped line buffer and single-fire teardown detection (review patch — a post-first-payload death no longer goes undetected).
  [`extension.ts:1272`](../../extension/src/extension.ts#L1272)

- `SpecScribeStore.loadViaPersistent`: starts/reuses the connection; a death after streaming at least one payload triggers an automatic recovery reload instead of staling forever (review patch).
  [`extension.ts:579`](../../extension/src/extension.ts#L579)

- Payload-shape parity test proving the persistent path's post-incremental-regen payload has the same wire shape as the one-shot path's.
  [`SiteGeneratorWebviewTests.cs:644`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L644)

**Webview runtime hardening (5 narrow bugs)**

- `ScratchKey`: folds case only on a case-insensitive filesystem (Windows) rather than never — an initial "never fold" pass would have reintroduced dir-accumulation on this project's primary OS (review patch).
  [`Commands.cs:320`](../../src/SpecScribe/Commands.cs#L320)

- `RedirectOutputToScratch`: the exclusive scratch-dir lock, rooted in a static field with `DeleteOnClose` (review patch — a discarded `FileStream` local was GC-collectible, silently releasing the lock early).
  [`Commands.cs:339`](../../src/SpecScribe/Commands.cs#L339)

- `RenderWebviewSurfaces`'s per-story degrade-to-placeholder on a deleted/ACL-denied artifact, instead of aborting the whole bundle.
  [`SiteGenerator.cs:2386`](../../src/SpecScribe/SiteGenerator.cs#L2386)

- Nav-toggle bridge: opening focuses the first nav link; a document-level `keydown` listener closes on Escape and returns focus to the toggle.
  [`WebviewRenderAdapter.cs:178`](../../src/SpecScribe/WebviewRenderAdapter.cs#L178) · [`WebviewRenderAdapter.cs:239`](../../src/SpecScribe/WebviewRenderAdapter.cs#L239)

- `runRenderer`'s `abort` helper: kill-escalation to SIGKILL and a stdout size cap, both still rejecting the promise immediately rather than waiting on the (possibly slow) kill — the timeout's original error latency is preserved (review patch).
  [`extension.ts:1169`](../../extension/src/extension.ts#L1169) · [`extension.ts:1199`](../../extension/src/extension.ts#L1199)

**Peripherals**

- Deferred-work audit trail: all 8 Story 6.4 entries closed (2 header closures, 6 fixed), with the review-round corrections noted inline.
  [`deferred-work.md`](./deferred-work.md)

- Scratch-key case-fold test, updated for OS-aware behavior.
  [`WebviewCommandTests.cs:120`](../../tests/SpecScribe.Tests/WebviewCommandTests.cs#L120)

- Story-artifact-deleted-mid-render regression test.
  [`SiteGeneratorWebviewTests.cs:213`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L213)

- Nav-toggle keyboard/focus parity test.
  [`WebviewRenderAdapterTests.cs:386`](../../tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs#L386)
