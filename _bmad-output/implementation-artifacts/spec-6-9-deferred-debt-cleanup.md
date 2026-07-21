---
title: 'Story 6.9 deferred-debt cleanup'
type: 'bugfix'
created: '2026-07-21T00:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Story 6.9 deferred-debt cleanup

## Intent

**Problem:** Story 6.9's code review left 5 items open in `deferred-work.md`, all tagged `6-9-native-project-outline-tree-view-and-status-bar`: (1) `resolveWorkspacePath`'s containment guard allegedly doesn't dereference symlinks; (2) `toolCommandLine` doesn't escape embedded quote characters; (3) `getOrCreateTerminal` reuses the "SpecScribe" terminal by name only, with no check for a still-running process; (4) `openGeneratedSite` has no containment guard on `configuredOutputRoot`, unlike `resolveWorkspacePath`; (5) multi-root workspaces get no detection/support, with no user-facing explanation.

**Approach:** Investigated all 5 before touching code. Item 1 was **already fixed** — confirmed via `git log -S realpathSync` that commit `875eb0f` (2026-07-12, Story 6.10, before this cleanup ever started) already rewrote `resolveWorkspacePath` to resolve both `root` and `target` through `fs.realpathSync` and compare the real paths; `deferred-work.md` had simply never been updated. Items 2, 3, 4, and 5 got real fixes:
- **Item 2** (`quoteCommandArg`, new): `toolCommandLine` now escapes any embedded `"` as `\"` in addition to the existing whitespace-triggered quoting.
- **Item 3** (`getOrCreateTerminal`/`stageCommandLine`, new): a module-level `busyTerminals` `WeakSet`, populated via `vscode.window.onDidStartTerminalShellExecution`/`onDidEndTerminalShellExecution` (feature-detected — guarded so an older-but-engines-satisfying VS Code host without this API can't crash activation), plus marking a terminal busy the instant a command is staged into it (before Enter, since shell integration's own start event only fires after). `getOrCreateTerminal` now skips a busy "SpecScribe" terminal and creates a fresh one instead of concatenating onto it. Degrades to the prior always-reuse behavior wherever shell integration is unavailable.
- **Item 4** (`resolveOpenableFile`, new, replacing a bare `fs.existsSync`): resolves the target through `fs.realpathSync` and requires `.isFile()`, matching `resolveWorkspacePath`'s rigor — but deliberately does NOT add a repo-root containment check, since an out-of-repo `configuredOutputRoot`/`--output` is a supported, intentional escape hatch (Story 6.8 AC #3, R2.4); blocking it would regress a feature, not close a gap. A broader containment policy question is Epic 17.2's remit.
- **Item 5** (`bindWorkspace`, new): a one-time-per-session, purely informational message when more than one workspace folder is open, naming the folder actually bound. Multi-root support itself stays out of scope (unchanged).

All 5 entries in `deferred-work.md` updated: 1 struck through as already-resolved-but-unmarked, 4 struck through as newly resolved.

**Adversarial review (Blind Hunter) and follow-up hardening.** A review pass surfaced real gaps, patched before commit: (a) the new shell-integration listeners were registered unconditionally even though this API graduated to stable after the extension's declared `engines.vscode` floor — an older-but-satisfying host lacking it would throw and crash the whole activation, not just the busy-terminal feature; fixed with a `typeof` feature-detection guard. (b) busy-tracking only updated on the shell-integration *start* event (post-Enter), so two staged-but-unexecuted commands in quick succession would still land on the same terminal; fixed by extracting a shared `stageCommandLine` helper that marks the terminal busy at staging time, before Enter. (c) `resolveOpenableFile` validated the realpath-resolved target but `openGeneratedSite` was opening the original (possibly symlinked) path — a TOCTOU gap; fixed by returning and opening the resolved real path. (d) the multi-root notice's wording implied a problem even when folder[0] was already the correct project; reworded to be purely informational. Three findings were reviewed and accepted as documented residual limitations rather than patched (each added as its own new `deferred-work.md` entry): `quoteCommandArg`'s escaping is POSIX/bash-style, not universally shell-correct (PowerShell/cmd have different quoting rules); the busy-terminal guard is scoped to the terminal object, not to "a command SpecScribe itself staged," so a repurposed or crashed terminal can stay marked busy for the rest of the session; and the one-time multi-root notice is a session-wide latch that won't re-fire if the bound folder changes later. All three are narrow, low-severity, and disproportionate to fully close for a deferred-debt cleanup pass.

## Suggested Review Order

**Already-resolved correction**
- `resolveWorkspacePath`'s symlink dereferencing was fixed by Story 6.10, not by this change — this pass only corrects the stale `deferred-work.md` note.
  [`deferred-work.md:578`](./deferred-work.md#L578)

**Terminal command-line quoting**
- `quoteCommandArg` escapes embedded `"`; doc comment is explicit about the POSIX/bash-only scope.
  [`extension.ts:1241`](../../extension/src/extension.ts#L1241)

**Terminal reuse vs. busy detection**
- `stageCommandLine` marks busy at staging time; `getOrCreateTerminal` skips busy terminals; shell-integration listeners are feature-detected.
  [`extension.ts:1147`](../../extension/src/extension.ts#L1147)

**Generated-site open hardening**
- `resolveOpenableFile` replaces a bare `existsSync`, resolving symlinks and requiring a real file, without blocking the intentional out-of-repo `--output` case.
  [`extension.ts:1108`](../../extension/src/extension.ts#L1108)

**Multi-root notice**
- `bindWorkspace` shows a one-time, non-alarmist notice naming the bound folder when more than one root is open.
  [`extension.ts:280`](../../extension/src/extension.ts#L280)

**Tracking**
- All 5 story-6-9 deferrals addressed in `deferred-work.md`: 1 discovered already-resolved, 4 newly resolved; 3 new narrow residual gaps found by review logged as fresh deferred items.
  [`deferred-work.md:578`](./deferred-work.md#L578)
