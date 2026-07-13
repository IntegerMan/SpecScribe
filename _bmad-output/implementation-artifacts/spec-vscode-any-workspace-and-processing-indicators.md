---
title: 'VS Code extension: usable in any workspace + processing indicators'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
baseline_commit: '1a2b7f5b4a0f59bb6d07e5af48819f65a2459e85'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extension only activates/renders behind a `_bmad/config.toml` or `_bmad-output` marker, so a plain code repo (no bmad, no git) hits a "No SpecScribe project detected" dead-end — even though the core already renders value from source code (Code Map), the README, and git for any folder. Separately, work in flight (first render, refresh, rebuild) has almost no visible busy signal, so clicking "Open Dashboard" during the ~3.5 s spawn looks like nothing happened.

**Approach:** (A) Stop gating on bmad/git: activate in any workspace, always expose the views/commands, and give the `webview` path a non-throwing resolution that degrades to whatever the folder offers, with a designed "no epics yet" state instead of a dead-end. (B) Drive a shared busy affordance off the store so first-render and refresh show a visible spinner instead of an inert UI.

## Boundaries & Constraints

**Always:**
- Keep the shim thin (ADR 0005 / AD-1/AD-2): no markdown parsing, no view rendering, no project knowledge in TS. All visible content stays core-decided.
- Read-only end to end (AD-6): no new writes to project artifacts or settings; Generate/Watch stay staged-terminal handoffs.
- The CLI `generate`/`watch` keep their actionable `DirectoryNotFoundException` when no project is found (CLI honesty). Only the `webview`/extension path degrades.
- A real bmad project renders byte-identically to today (the tolerant path only changes behavior when the marker is ABSENT).
- Route every busy indicator through ONE store-owned signal — no per-command ad-hoc spinners that can disagree.

**Ask First:**
- The first-run copy/visual for a non-bmad workspace (what replaces the "not detected" welcome) and the exact busy-indicator presentation are owner-facing surfaces — confirm the picks at CHECKPOINT 1 before implementation.

**Never:**
- Do not make the extension run `generate`/`watch` automatically or write output on the user's behalf.
- Do not add multi-root support (still `workspaceFolders[0]`).
- Do not collapse the six `--status-*` stages onto host severities.
- No new core page or data-model change; reuse the existing degrade paths.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bmad project | `_bmad-output` present | Full dashboard/epics/outline, unchanged (byte-identical) | N/A |
| Plain git repo | source + `.git`, no `_bmad-output` | Renders README + Code Map (git ls-files) + git pulse; outline shows designed "no epics yet" state | N/A |
| Plain non-git folder | source files only | Renders README + Code Map via `FallbackCodeWalk`; git/epics sections omit cleanly | N/A |
| Empty folder | no README, no code, no bmad | Panel opens to a designed empty state; no crash, no error toast | Degrade, don't throw |
| No folder open | no `workspaceFolders` | Commands show existing "open a project folder first" message | N/A |
| Refresh running | manual Refresh mid-spawn | Status bar shows spinner ("rendering…") until settle, then count | Failure → existing stale indicator |
| Cold panel open | first spawn (~3.5 s) | Existing `withProgress` notification, now also reflected in status bar | Error page + actionable toast (unchanged) |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/ForgeOptions.cs:134` -- `Resolve()` throws when no `_bmad-output` found; add tolerant mode.
- `src/SpecScribe/SiteSettings.cs:45` -- `Resolve()` that `WebviewCommand` calls; add a tolerant sibling.
- `src/SpecScribe/Commands.cs:61` -- `WebviewCommand.Execute`, the only caller that should tolerate a missing source.
- `src/SpecScribe/SiteGenerator.cs` -- already degrades (source/ADR enum guarded 2254/2368; `EnumerateCodeFiles` git-free fallback 2273-2325; `GitMetrics.TryCompute` null off-git). No change expected — verify only.
- `extension/package.json` -- `activationEvents:15`, `viewsWelcome:120`, ~11 `when: specscribe.projectDetected` clauses.
- `extension/src/extension.ts` -- `DETECTION_MARKERS`/`projectDetected` (152/266); `SpecScribeStore` (481); `renderStatusBar` (764); `refreshCommand` (314); cold-start `withProgress` (425).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/ForgeOptions.cs` -- added optional `requireSource = true` param to `Resolve`; when false and no `_bmad-output` marker is found walking up, uses the start directory (cwd) as `RepoRoot` and `<RepoRoot>/_bmad-output` as `SourceRoot` (may not exist) instead of throwing. All other resolution unchanged.
- [x] `src/SpecScribe/SiteSettings.cs` -- added `ResolveTolerant()` mirroring `Resolve()` but passing `requireSource: false`.
- [x] `src/SpecScribe/Commands.cs` -- `WebviewCommand.Execute` now calls `settings.ResolveTolerant()`; `generate`/`watch` untouched.
- [x] `extension/package.json` -- `activationEvents` → `onStartupFinished`; `viewsWelcome` rewritten to "Open a folder…" (gated on `!specscribe.available` = no folder open); renamed the gate key `specscribe.projectDetected` → `specscribe.available` (meaning: a folder is open) so core commands + Shortcuts view are available in any workspace.
- [x] `extension/src/extension.ts` -- context key set on "a folder is open" (`folderOpen`), marker detection removed as a gate; tree renders a designed "No epics here — open the dashboard for this folder's code map & README" message node when the outline has zero epics.
- [x] `extension/src/extension.ts` -- added `isLoading` to `SpecScribeStore` and fire `dataChanged` at load START; `renderStatusBar` shows `$(sync~spin) SpecScribe: rendering…` while loading; wrapped `refreshCommand` in a `withProgress` window heartbeat.
- [x] `tests/SpecScribe.Tests/ForgeOptionsTests.cs` -- tolerant-resolve tests (non-bmad dir → cwd repo root + nonexistent source root, no throw; still walks up to a real bmad project). End-to-end degrade test added to `SiteGeneratorWebviewTests.cs` (a source-only non-bmad folder renders a valid bundle: README + code-map surfaces, empty outline) — a better home than `WebviewCommandTests` for a full-generation test.

**Acceptance Criteria:**
- Given a folder with no `_bmad-output` and no `.git`, when the extension activates and I open the dashboard, then a panel renders (README/Code Map/empty-epics state) with no error toast and no "not detected" dead-end.
- Given a real bmad project, when I open the dashboard, then output is byte-identical to today (no regression).
- Given a render or manual refresh is in flight, when I look at the status bar, then a spinning "rendering…" indicator is visible until it settles.
- Given the CLI `generate` is run outside any project, when it cannot find `_bmad-output`, then it still throws the actionable error (unchanged).

## Design Notes

Feasibility rests on the core already degrading once resolution succeeds: `GenerateAll` renders README + Code Map unconditionally and gates epics/requirements on the ingest chain producing models (SiteGenerator.cs:209). The one hard failure is `Resolve()` throwing — so the change surface is small: one tolerant flag + shim gating/activation.

Repo-root when the marker is absent: use the process cwd (the workspace folder). Predictable and git-free; `GitMetrics.TryListFiles` still works from a subdir. (Owner-decidable: prefer git top-level for wider Code Map coverage.)

Busy signal: `dataChanged` fires only on settle today. Firing it on load-start too (plus an `isLoading` getter) lets the status bar reflect in-flight work with zero new plumbing, reusing the fan-out the panel/tree/status bar already subscribe to.

## Verification

**Commands:**
- `dotnet build SpecScribe.sln` -- expected: builds clean.
- `dotnet test --filter "FullyQualifiedName~ForgeOptions|FullyQualifiedName~WebviewCommand"` -- expected: new tolerant-resolve + webview-payload tests pass; existing green.
- `cd extension && npm run typecheck` -- expected: no TS errors.

**Manual checks:**
- Launch the extension (F5) on a plain non-bmad folder: activity-bar view appears, Open Dashboard renders a real panel, status bar shows the spinner during the spawn then a summary/empty state — no "not detected" welcome.
- Repeat on this bmad repo: unchanged full experience.

## Suggested Review Order

**Goal A — tolerant resolution (the design entry point)**

- Start here: the one behavioral pivot — no throw when no marker, anchor on cwd instead.
  [`ForgeOptions.cs:145`](../../src/SpecScribe/ForgeOptions.cs#L145)
- The opt-in flag; default `true` keeps every CLI/library caller's throwing behavior.
  [`ForgeOptions.cs:121`](../../src/SpecScribe/ForgeOptions.cs#L121)
- Only the webview path opts in; `generate`/`watch` keep the throwing `Resolve()`.
  [`SiteSettings.cs:53`](../../src/SpecScribe/SiteSettings.cs#L53)
- The single caller wiring the tolerant resolve into the extension's spawn.
  [`Commands.cs:68`](../../src/SpecScribe/Commands.cs#L68)

**Goal A — shim de-gating (any workspace)**

- Gate is now "a folder is open," not a bmad marker — enables surfaces everywhere.
  [`extension.ts:263`](../../extension/src/extension.ts#L263)
- Empty outline reads as designed guidance, not an error/dead-end.
  [`extension.ts:740`](../../extension/src/extension.ts#L740)
- Activation broadened so the extension wakes in any workspace.
  [`package.json:16`](../../extension/package.json#L16)
- Welcome retargeted to "no folder open" (honest under the new gate).
  [`package.json:123`](../../extension/package.json#L123)

**Goal B — busy indicator (highest-risk interaction)**

- The review-patched guard: only settle-fires re-push the panel (no double swap / tab reset).
  [`extension.ts:426`](../../extension/src/extension.ts#L426)
- Load-start fan-out that lights the spinner (the subtle bit reviewers should scrutinize).
  [`extension.ts:543`](../../extension/src/extension.ts#L543)
- The busy state the spinner reads.
  [`extension.ts:513`](../../extension/src/extension.ts#L513)
- Status-bar spinner takes precedence over count/stale while rendering.
  [`extension.ts:794`](../../extension/src/extension.ts#L794)
- Manual refresh heartbeat, wording harmonized to "rendering…".
  [`extension.ts:320`](../../extension/src/extension.ts#L320)

**Tests (supporting)**

- Non-bmad folder → valid bundle: README + code-map surfaces, empty outline.
  [`SiteGeneratorWebviewTests.cs:514`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L514)
- Truly-empty folder degrade (review-added edge case).
  [`SiteGeneratorWebviewTests.cs:551`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L551)
- Tolerant resolve doesn't throw + still walks up to a real bmad project.
  [`ForgeOptionsTests.cs:40`](../../tests/SpecScribe.Tests/ForgeOptionsTests.cs#L40)
