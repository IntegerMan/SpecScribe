---
baseline_commit: 2e4cb4cbef30a1094954ba27294192683bc902d2
seated_by: SCP 2026-07-11 (correct-course) — FR35, VS Code Native-Integration Recommendations
---

# Story 6.8: Extension Discoverability, Workspace Trust, and Command Surface

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a VS Code user with a spec-driven repository,
I want the SpecScribe extension to announce itself and offer more than one way in — activating on project detection, contributing menus and direct-open/refresh commands, opening beside my editor, and declaring a safe workspace-trust posture,
so that I can discover and drive SpecScribe natively instead of having to already know that a single hidden command exists.

## ✅ NOT GATED — prerequisites are DONE (unlike Story 6.5)

This story's prerequisites — [Story 6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (the webview runtime + `extension/` shim + `specscribe webview` CLI) and [Story 6.5](6-5-host-aware-theming-and-explicit-helper-actions.md) (theming + helper) — are both **`done`**. **The extension exists in the repo right now** ([extension/package.json](../../extension/package.json), [extension/src/extension.ts](../../extension/src/extension.ts)). This story **modifies that shipped extension**; it does not build one. Start immediately — there is no "read first / do not start" gate.

## What this story is (and isn't)

This is a **manifest + TypeScript-routing story**. Every AC is delivered by editing `extension/package.json` (VS Code manifest) and `extension/src/extension.ts` (the shim), reusing the **existing spawn/panel machinery**. There is **no new rendering, no new C# view model, no markdown parsing**. One small, optional core payload addition is discussed under AC #3 (open-generated-site) — that is host-delivery of core-emitted *data* (allowed by ADR 0005 §1), not rendering.

**The five non-negotiable Epic 6 native-integration constraints** ([docs/VSCodeIntegrationRecommendations.md §2](../../docs/VSCodeIntegrationRecommendations.md)) hold throughout:

1. **Thin shim, no rendering brain** (ADR 0005 AD-1/AD-2). The extension parses no markdown, holds no project knowledge. Project *detection* is by **path existence only** (`fs.existsSync`) — that is not parsing, so AD-2 holds.
2. **Read-only end to end** (AD-6, ADR 0003). No command in this story writes a project artifact or mutates settings. Generate/Watch/scaffold are **staged into a terminal for the user to run** (SpecScribe never presses Enter).
3. **VS Code settings carry HOST concerns only** (ADR 0003). This story's new setting (`specscribe.openLocation`) is a host concern. Do **not** mirror project behavior (source/ADR roots, deep-git, project name) into VS Code settings — that lives in the directory-scoped `.specscribe` file.
4. **The generated HTML surface stays byte-identical.** This story touches no C# rendering, so the golden fingerprint is unaffected by construction. If you add the optional output-root payload field (AC #3), re-run the golden test to confirm it stays green.
5. **Status surfaces derive from the six core-emitted `--status-*` stages, never VS Code's 3-severity palette.** *Not exercised by this story* — 6.8 adds no status-bearing native surface (that is Story 6.9's tree view / status bar). Recorded so you don't accidentally introduce one.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 6.8._

1. **Given** a workspace that contains SpecScribe artifacts (detected by path existence only, no content parsing)
   **When** the folder is opened
   **Then** the extension activates, sets a `specscribe.projectDetected` context key, and its menu/command contributions appear only in such repos (gated by `when` clauses)
   **And** repos without spec artifacts see no SpecScribe noise.

2. **Given** the extension spawns a workspace-adjacent binary and honors a `toolPath` setting
   **When** the manifest declares workspace-trust capabilities
   **Then** untrusted workspaces cannot override `toolPath` (declared via `capabilities.untrustedWorkspaces` with `restrictedConfigurations`), closing the tool-resolution attack surface, while user/machine-level values still apply
   **And** this posture is in place before the Story 16.5 Marketplace publish.

3. **Given** the user wants to reach SpecScribe from the editor
   **When** command and menu contributions are used
   **Then** direct-open (Dashboard/Epics), refresh, open-generated-site, and explorer/editor-title "Open in SpecScribe Status" entries all route through the existing read-only spawn/panel path, the panel can open beside the active editor per a `specscribe.openLocation` host setting, and "Open Project Settings" reveals the directory-scoped settings file without SpecScribe writing it
   **And** generate/watch commands are staged into an integrated terminal for the user to run (SpecScribe never executes a write to the project's output).

4. **Given** cold start and error paths
   **When** the panel is opening or a spawn fails
   **Then** first paint shows a progress/heartbeat affordance and failures surface an actionable notification (set `toolPath` / retry), and the panel tab carries a SpecScribe icon
   **And** no recommendation in this story mutates a project artifact.

### AC → Recommendation → Surface map

| AC | Recommendation(s) | Where it lands |
|---|---|---|
| #1 | R1.1 (activate on detection + `setContext`), R1.2 (`when`-gated contributions), R1.3 (explorer/editor menus) | `package.json` `activationEvents` + `contributes.menus`; `extension.ts` `activate()` detection |
| #2 | R5.4 (Workspace Trust) | `package.json` `capabilities.untrustedWorkspaces` — **Marketplace prerequisite for [16.5](#story-relationships)** |
| #3 | R2.1 (direct-open), R2.2 (refresh), R2.3 (generate/watch terminal handoff), R2.4 (open generated site), R3.3 (open beside + `specscribe.openLocation`), R5.2 (open project settings) | `package.json` commands/menus/config; `extension.ts` command handlers reusing the spawn/panel path |
| #4 | R7.1 (cold-start progress), R7.2 (actionable error notification), R7.3 (panel icon) | `extension.ts` `openStatus`/`runRenderer`/`createPanel`; a bundled icon asset |

## Critical current-state facts the dev MUST internalize

Read [extension/src/extension.ts](../../extension/src/extension.ts) (241 lines, the WHOLE shim) and [extension/package.json](../../extension/package.json) end to end before writing anything. The specifics below shape every task:

1. **One command exists today.** `specscribe.openStatus` → `openStatus(context)` ([extension.ts:36–40, 44](../../extension/src/extension.ts)). `package.json` `contributes.commands` has exactly that one; `activationEvents` is `[]` (command-only activation) ([package.json:15, 18–24](../../extension/package.json)).
2. **The panel is a module-level singleton.** `let panel: vscode.WebviewPanel | undefined` ([extension.ts:42](../../extension/src/extension.ts)). `openStatus` early-returns and `panel.reveal()`s if it already exists ([extension.ts:50–53](../../extension/src/extension.ts)). Every "direct-open X" command must respect this: if the panel is open, `reveal()` + `push(targetSurface)` rather than spawn a second panel.
3. **`push(target, reason)` swaps surfaces in place** ([extension.ts:69–75](../../extension/src/extension.ts)) using `cache.surfaces[target]`, keyed by **output-relative path**. This is the mechanism for direct-open Dashboard vs Epics: same panel, different initial `push()` target. `cache.entry` is the dashboard entry path. **You must discover the epics surface's key** from the bundle (do not hard-code a guess — resolve it, see AC #3 notes).
4. **The panel opens in `ViewColumn.One`, hard-coded** ([extension.ts:155](../../extension/src/extension.ts) in `createPanel()`). R3.3 changes this to read `specscribe.openLocation`.
5. **Tool resolution lives in `runRenderer`** ([extension.ts:188–195](../../extension/src/extension.ts)): `configured toolPath` → bundled `bin/specscribe(.exe)` → `specscribe` on PATH; a `.dll` runs via `dotnet`. The Generate/Watch **terminal-handoff** commands (AC #3) must reuse this **exact** resolution to build the command string — extract it into a shared helper so the panel spawn and the terminal command never drift.
6. **The error path drops the singleton and sets `errorHtml`** ([extension.ts:109–117, 227–229](../../extension/src/extension.ts)). The error page is **script-free by design** (CSP `default-src 'none'`), so R7.2's actionable buttons go on a `showErrorMessage` **notification** (native buttons), *not* as in-page links. Keep the existing `errorHtml`; add the notification alongside it.
7. **`copyHelperText` clipboard handoff and the random-sentinel `composeEntryHtml`** ([extension.ts:77–89, 170–183](../../extension/src/extension.ts)) are the Story 6.5 read-only helper + a security fix (per-call random sentinel prevents `__NONCE__`/`__CSP_SOURCE__` corruption). **Do not regress either.**
8. **`toolPath` is `scope: "machine-overridable"`** ([package.json:31](../../extension/package.json)). Story 6.5's review confirmed this *tightens* the setting (workspace overrides need trust) vs the old default. R5.4 (AC #2) is the **manifest-level lock** that completes it — `restrictedConfigurations` makes VS Code ignore the *workspace-level* `toolPath` in untrusted workspaces (user/machine values still apply).
9. **`engines.vscode` is `^1.90.0`** ([package.json:9–11](../../extension/package.json)). Every API this story uses (`workspaceContains`, `capabilities.untrustedWorkspaces` [1.57+], `setContext`, `ViewColumn.Beside`, `createTerminal`/`sendText`, `window.withProgress`, `WebviewPanel.iconPath`, `editor/title` + `explorer/context` menus, `env.openExternal`, `workbench.action.openSettings`) is available at 1.90. **No engine bump.**
10. **The directory-scoped settings file is `.specscribe`** in the repo root ([SettingsStore.cs:30, 39](../../src/SpecScribe/SettingsStore.cs), `SettingsStore.FileName`). R5.2's "Open Project Settings" reveals that file. Detection markers for activation/`when` are `_bmad/config.toml` and/or `_bmad-output/` (the recommendation's chosen path-existence markers).
11. **The webview command redirects output to a temp scratch dir** ([Commands.cs:50–52, 78–93](../../src/SpecScribe/Commands.cs)) — so the payload does **not** currently carry the project's *real* generate output root. This matters for R2.4 (open-generated-site): see the AC #3 notes for the resolution.

## Design decisions captured at create-story

### Detection markers & the context key (AC #1)
- **`activationEvents`:** add `"workspaceContains:_bmad/config.toml"` and `"workspaceContains:_bmad-output"`. Either present → the extension activates. (Command activation is implicit in modern VS Code; `workspaceContains` must still be declared.)
- **Context key:** in `activate()`, detect by `fs.existsSync` on each workspace folder for `_bmad/config.toml` **or** `_bmad-output`, then `vscode.commands.executeCommand('setContext', 'specscribe.projectDetected', <bool>)`. Re-evaluate on `vscode.workspace.onDidChangeWorkspaceFolders` so a late-added folder flips the key. Path existence only — **no file reads, no parsing** (AD-2).
- **`when` gating:** every command's `commandPalette` menu entry and both context-menu entries carry `when: specscribe.projectDetected` (compose with resource filters for the menus). Non-SpecScribe repos then show zero SpecScribe entries (AC #1 "no noise").

### Workspace Trust posture (AC #2) — the exact manifest shape
```jsonc
"capabilities": {
  "untrustedWorkspaces": {
    "supported": "limited",
    "restrictedConfigurations": ["specscribe.toolPath"]
  }
}
```
Confirmed against current VS Code docs: `"limited"` = extension works in Restricted Mode but trust-sensitive bits are constrained; listing a setting in `restrictedConfigurations` makes VS Code **return only the user/machine-defined value in Restricted Mode**, ignoring any workspace-supplied override. Because `toolPath` is the path to the binary the shim spawns, a malicious workspace overriding it is a classic RCE vector — this closes it precisely while leaving the user's own value intact. This posture **must be present before Story 16.5's Marketplace publish** (its review bar checks for it, and [Story 17.2](../planning-artifacts/epics.md) verifies it — epics.md:2273). ([Workspace Trust Extension Guide](https://code.visualstudio.com/api/extension-guides/workspace-trust))

### Command surface (AC #3)
Add these commands (all `category: "SpecScribe"`), each routing through the existing read-only spawn/panel or a staged terminal — **no new render path**:

| Command id | Title | Behavior |
|---|---|---|
| `specscribe.openDashboard` | Open Dashboard | Open/reveal panel, initial surface = `cache.entry` (dashboard). |
| `specscribe.openEpics` | Open Epics | Open/reveal panel, initial surface = the **resolved** epics-index surface key. |
| `specscribe.refresh` | Refresh Status | Trigger the existing debounced reload + `push(current, 'refresh')`. |
| `specscribe.openGeneratedSite` | Open Generated Site | If the resolved output root's `index.html` exists → `env.openExternal`; else the existing toast. |
| `specscribe.generateSite` | Generate Full Site | Stage `<tool> generate` in an integrated terminal (`sendText(cmd, false)`) — user presses Enter. |
| `specscribe.watch` | Watch | Stage `<tool> watch` the same way. |
| `specscribe.openProjectSettings` | Open Project Settings | Reveal `.specscribe` via `showTextDocument` if present; else offer to scaffold via the CLI interactive flow staged in a terminal. |
| _(keep)_ `specscribe.openStatus` | Open Status | Unchanged entry point; also the handler reused by the explorer/editor menus. |

**Direct-open refactor (R2.1):** `openStatus` currently always opens to `cache.entry`. Parametrize the initial surface: give `openStatus`/the internal open path an optional `initialSurface?: string` and, after first paint (or on `reveal()` of an existing panel), `push(initialSurface ?? cache.entry, 'navigate')`. Dashboard passes `undefined`/entry; Epics passes the epics key.

**Resolving the epics surface key:** do **not** hard-code `"epics.html"`. The bundle's surface keys are the C# `OutputRelativePath`s ([Commands.cs:69](../../src/SpecScribe/Commands.cs)). Resolve the epics-index key at runtime from `cache.surfaces` (e.g. the key whose basename is the epics index — match on `/(^|\/)epics(\.html)?$/` or the known epics-index path, verified against a real payload) and fall back to `cache.entry` if absent (mirrors `push`'s existing fallback at [extension.ts:71](../../extension/src/extension.ts)). **Verify the actual key by running `specscribe webview` once and inspecting the JSON** (see Testing).

**Refresh command (R2.2):** the debounced `refresh` closure is currently local to `openStatus` ([extension.ts:130–140](../../extension/src/extension.ts)). Hoist a reload trigger so the command can invoke it — e.g. keep a module-level `activeController?: { reload(): void }` set when the panel opens and cleared on dispose, and have `specscribe.refresh` call `activeController?.reload()` (or open the panel first if none). Keep the in-flight `load()` coalescing intact — the manual refresh must share the same guard.

**Open Generated Site (R2.4) — output-root resolution (decision point):** the `webview` payload does not carry the real generate output root (it redirects to scratch, fact #11). Two options:
- **(A, recommended)** Add a `configuredOutputRoot` (workspace-relative) field to the `webview` JSON payload, sourced from `settings.Resolve()` **before** the scratch redirect ([Commands.cs:49–52](../../src/SpecScribe/Commands.cs)) — i.e. the root `generate` would use. The shim opens `<root>/index.html` if it exists. This keeps path resolution in the core (consistent with R6.2's "core stays authoritative") and is host-delivery of data, not rendering (ADR 0005 §1). Add a C# test for the new field and re-run the golden test (payload is not part of the generated site, so golden stays green).
- **(B, fallback)** Shim assumes the default `SpecScribeOutput/` under the workspace root (memory: output dir is `SpecScribeOutput`, never `docs/live`) and opens `<workspace>/SpecScribeOutput/index.html` if it exists. Simpler, but wrong if the project configured a non-default `OutputRoot` in `.specscribe`.

**Recommend (A).** Note that neither the webview command nor `generate` consults `.specscribe` today (R5.3 gap, fix seated in Story 5.2) — so even (A) resolves the *default* output unless the user passed `--output`; that is acceptable and correct for this story. If the resolved `index.html` is absent, keep the current helpful toast ([extension.ts:95–97](../../extension/src/extension.ts) pattern) rather than opening nothing.

**Generate/Watch terminal handoff (R2.3):** `vscode.window.createTerminal({ name: 'SpecScribe', cwd })` then `terminal.sendText(command, false)` — the `false` **stages** the command at the prompt without executing; the user presses Enter. Build `command` from the **shared tool-resolution helper** (fact #5): `<tool> generate` / `<tool> watch`, with `dotnet <dll> generate` when the resolved tool is a `.dll`. This is the letter of AD-6/ADR 0003: SpecScribe never runs a write to the project output; the explicit choice stays with the user. `showTextDocument`-style terminal reveal is fine. (If the owner later wants one-click execution, that is a recorded exception to constraint #2 — **not** the default here.)

**Open Project Settings (R5.2):** if `<workspace>/.specscribe` exists → `vscode.window.showTextDocument(Uri.file(path))`. If not → `showInformationMessage` with an action button that stages the CLI interactive flow (`<tool>` with no args → its "Configure paths" menu) in a terminal for the user to run, so the file is created by an explicit user action, never by the extension. The extension itself performs **no write** to `.specscribe`.

**Open beside (R3.3):** add `specscribe.openLocation` config (`enum: ["active","beside"]`, default `"beside"`, `scope: "window"`, host concern). In `createPanel()`, map it to `ViewColumn.Active` / `ViewColumn.Beside` instead of the hard-coded `ViewColumn.One`. Default `beside` = status next to the file you're editing (the recommendation's preferred default).

### Menus (AC #1 + #3)
- **`explorer/context`:** "Open in SpecScribe Status" on the `_bmad-output` folder/artifacts, `when: specscribe.projectDetected && resourcePath =~ /_bmad-output/` (or an appropriate `resourceFilename`/`resourceDirname` clause). Handler reuses `openStatus`.
- **`editor/title`:** "Open in SpecScribe Status" when the active editor is an artifact markdown, `when: specscribe.projectDetected && resourceExtname == .md && resourceDirname =~ /_bmad-output/`. Handler reuses `openStatus`.
- **`commandPalette`:** gate every new command with `when: specscribe.projectDetected` so they vanish outside SpecScribe repos.

### UX polish (AC #4)
- **R7.1 cold-start progress:** wrap the initial `load()` (the ~3.5 s cold spawn) in `vscode.window.withProgress({ location: ProgressLocation.Notification, title: 'SpecScribe: rendering…' }, …)`. The panel may show a lightweight inline "Rendering…" splash too, but the notification is the required heartbeat.
- **R7.2 actionable error:** in the `catch` ([extension.ts:111–116](../../extension/src/extension.ts)), in addition to `errorHtml`, `showErrorMessage('SpecScribe could not render…', 'Set specscribe.toolPath', 'Retry')` and route: `Set…` → `commands.executeCommand('workbench.action.openSettings', 'specscribe.toolPath')`; `Retry` → re-invoke the open path. Native buttons only (the error page stays script-free).
- **R7.3 panel icon:** bundle a small SVG (e.g. `extension/media/specscribe.svg`, or `{light,dark}` variants) and set `panel.iconPath` in `createPanel()`. This is the **editor-tab** icon — distinct from the **Marketplace** icon (R1.6), which is Story 16.5's job and out of scope here. `iconPath` is a tab affordance and does not require `localResourceRoots` (that governs webview *content* loading, which stays `[]`).

## Scope

### IN scope
- `extension/package.json`: `activationEvents` (2 `workspaceContains`), `capabilities.untrustedWorkspaces`, ~7 new `contributes.commands`, `contributes.menus` (`commandPalette` gating + `explorer/context` + `editor/title`), `contributes.configuration` (`specscribe.openLocation`).
- `extension/src/extension.ts`: `activate()` detection + `setContext` + workspace-folder-change re-eval; new command registrations + handlers (direct-open Dashboard/Epics, refresh, open-generated-site, generate/watch terminal handoff, open-project-settings); parametrized initial surface; `createPanel()` reads `openLocation` + sets `iconPath`; cold-start `withProgress`; actionable error notification; a shared tool-resolution helper extracted from `runRenderer`.
- **Optional core (AC #3 option A):** add `configuredOutputRoot` to the `webview` payload ([Commands.cs](../../src/SpecScribe/Commands.cs)) + a C# test; re-run golden.
- A bundled panel-tab icon asset under `extension/`.
- Extend [extension/README.md](../../extension/README.md)'s F5 checklist to smoke the new commands/menus, the workspace-trust behavior, open-beside, and the error/progress affordances.

### OUT of scope (do NOT start here)
- **Any new status-bearing native surface** — tree view, status bar (Story 6.9). No `contributes.views`/`viewsWelcome`/status-bar item here. Constraint #5 only matters once those exist.
- **Reveal-source / editor↔artifact bridges** (Story 6.10) and **file-change reactivity hardening / multi-root / derived watch roots** (Story 6.11). Keep the existing `_bmad-output/**/*.md` + `docs/adrs/**/*.md` watchers and `workspaceFolders[0]` exactly as they are — do not "fix" the yaml/toml watch gap or multi-root here (those are R6.1/R6.2/R3.4 → 6.11).
- **Marketplace metadata (R1.6), walkthrough (R1.4), platform-targeted VSIX (R8.1)** — Story 16.5. Do not change `categories`/`keywords`/`icon` (the *marketplace* icon) or add `contributes.walkthroughs` here. (The **panel-tab** icon R7.3 IS in scope; they are different icons.)
- **Routing `.specscribe` through the webview spawn (R5.3)** — Story 5.2. Do not make `WebviewCommand` consult `SettingsStore`.
- **`SpecScribe: Watch` one-click execution** — staged-terminal only (constraint #2). One-click would be a recorded owner exception; do not default to it.
- **Webview nav-toggle keyboard/focus a11y (R7.4 / deferred-work.md:20).** This is tracked debt that R7.4 flags as *fold-able* into "whichever story next touches the shim" — and 6.8 does touch the shim. But the owner seated 6.8's scope **without** it, and it is a webview-*content* interaction concern (not discoverability/commands/trust). **Leave it deferred** unless the owner explicitly folds it in; do not silently absorb it (Epic 2 retro: "split, don't absorb").
- **New rendering, view models, markdown parsing, package/namespace split** (seed-level, still forbidden).
- **HTML-surface changes of any kind** — this story emits none; the golden fingerprint must stay green.

## Tasks / Subtasks

- [x] **Task 0 — Baseline & payload reconnaissance** (prereq for AC #3)
  - [x] Re-capture current `HEAD` for the byte-parity diff (intervening auto-committer commits may sit between the recorded `baseline_commit` `2e4cb4c` and HEAD — memory: [worktree-edits-must-target-worktree-path]). HEAD at dev-start = `0a0d0f7`.
  - [x] Build the tool (`dotnet build src/SpecScribe`) and run `specscribe webview` from the repo root; **capture the JSON** and record the exact **surface keys**. Confirmed: dashboard `entry` = `index.html`; epics-index key = `epics.html`. Payload top-level keys were `siteTitle, entry, document, surfaces` — it did **NOT** carry `repoRoot` (the story's deferred-work.md:8 note is stale) and did **NOT** carry a configured output root (fact #11 confirmed). Resolved epics key at runtime with `/(^|\/)epics\.html$/`, not a hard-code.

- [x] **Task 1 — Activation, context key, and `when`-gated contributions** (AC: #1)
  - [x] `package.json`: added `activationEvents` `["workspaceContains:_bmad/config.toml", "workspaceContains:_bmad-output"]`.
  - [x] `extension.ts` `activate()`: `updateDetection()` checks each `workspace.workspaceFolders` via `fs.existsSync` on `_bmad/config.toml` OR `_bmad-output` (path existence only) → `setContext('specscribe.projectDetected', <bool>)`; subscribed to `onDidChangeWorkspaceFolders`.
  - [x] All 8 `commandPalette` entries and both context menus gated with `when: specscribe.projectDetected` (+ `_bmad-output`/`.md` resource clauses on the menus).

- [x] **Task 2 — Workspace Trust posture** (AC: #2)
  - [x] `package.json`: added `capabilities.untrustedWorkspaces` = `{ "supported": "limited", "restrictedConfigurations": ["specscribe.toolPath"] }` (exact shape).
  - [x] Recorded in Completion Notes: this is the Story 16.5 Marketplace prerequisite and the Story 17.2 verification target.

- [x] **Task 3 — Command surface: direct-open, refresh, open-generated-site** (AC: #3)
  - [x] Extracted `resolveTool()` (returns `{command, prefixArgs}`) shared by `runRenderer` spawn AND terminal handoff.
  - [x] Parametrized the open path with a `SurfaceTarget` (`'dashboard' | 'epics'`); registered `specscribe.openDashboard` (entry) and `specscribe.openEpics` (runtime-resolved epics key). Singleton respected: `reveal()` + `push` when already open (via `PanelController.reveal`).
  - [x] Registered `specscribe.refresh` → `PanelController.reload()` (module-level `active` controller ref, cleared on dispose), sharing the in-flight `load()` guard; opens the panel if none.
  - [x] Registered `specscribe.openGeneratedSite`: **option A** — added `configuredOutputRoot` to the payload; shim caches it in `lastConfiguredOutputRoot` (falls back to `SpecScribeOutput`), `env.openExternal(<root>/index.html)` if it exists, else a toast.

- [x] **Task 4 — Command surface: terminal handoff & project settings** (AC: #3)
  - [x] Registered `specscribe.generateSite` / `specscribe.watch`: `createTerminal({name:'SpecScribe', cwd})` + `sendText(cmd, false)` (staged), `cmd` from `toolCommandLine(resolveTool(...))`.
  - [x] Registered `specscribe.openProjectSettings`: `showTextDocument('.specscribe')` if present; else an info message offering to stage the CLI interactive flow. **No extension-side write to `.specscribe`.**

- [x] **Task 5 — Menus & open-beside** (AC: #1, #3)
  - [x] `contributes.menus`: `explorer/context` + `editor/title` reuse `specscribe.openStatus` (gated by `projectDetected` + `_bmad-output`/`.md` clauses). Note: VS Code menu items show the *command's* title, so the label is "Open Status" (no per-menu title override exists); handler reuses `openStatus`.
  - [x] Added `specscribe.openLocation` config (`enum beside|active`, default `beside`, `scope: window`). `createPanel()` maps it to `ViewColumn.Beside`/`ViewColumn.Active` (replaced hard-coded `ViewColumn.One`).

- [x] **Task 6 — UX polish: progress, error, icon** (AC: #4)
  - [x] Wrapped the cold-start `load()` in `withProgress` (Notification, "SpecScribe: rendering…").
  - [x] Added actionable `showActionableError` ("Set specscribe.toolPath" → `workbench.action.openSettings`; "Retry" → re-open) alongside the existing script-free `errorHtml`.
  - [x] Bundled `extension/media/specscribe.svg` and set `panel.iconPath` in `createPanel()`.

- [x] **Task 7 — Verify, guard, and document** (AC: all)
  - [x] `npm run typecheck` (clean) + `npm run build` (esbuild → `dist/extension.js`, 14.2 KB). `package.json` validated as well-formed JSON.
  - [x] Option A taken: `dotnet test` whole suite green — **735 passed / 0 failed** (was 733; +2 `WebviewCommandTests`), including the golden `GoldenContentFingerprint` (site byte-identical; payload is not part of the site). Added `WebviewCommand.ResolveConfiguredOutputRoot` + 2 assertions.
  - [x] Read-only confirmed: grep for `writeFile|fs.write|applyEdit|WorkspaceEdit|.update(|SettingsStore` over the shim → **none** in a command path. Only host-side effects are the 6.5 clipboard (unchanged) and staged `sendText(cmd, false)` (not executed).
  - [x] Extended [extension/README.md](../../extension/README.md) with an F5 smoke checklist (noted as a human step, as 6.4/6.5 did).

## Dev Notes

### This is routing, not rendering — keep the shim thin
Every visible byte still comes from the C# core; this story only adds **doors** (commands/menus) and **host affordances** (trust, progress, icon, open-location). If you find yourself parsing a `.md`, reading artifact *contents*, or building HTML in TypeScript, stop — that is the wrong layer (AD-1/AD-2). Detection is `existsSync` on paths, nothing more.

### Read-only is a spine invariant, not a preference (AD-6 / ADR 0003 / NFR-5)
No command here writes a project artifact or mutates settings. Generate/Watch/scaffold are **staged** in a terminal — SpecScribe never presses Enter. "Open Project Settings" reveals a file; it does not create or edit it (the CLI's own interactive flow, run by the user, does any creation). The clipboard helper from 6.5 stays as-is. Prove write-freedom in Task 7.

### Workspace Trust is the security-critical AC — get the manifest exactly right (AC #2)
The whole point of `restrictedConfigurations: ["specscribe.toolPath"]` is that an untrusted workspace's `.vscode/settings.json` cannot redirect the spawned binary. Verify behaviorally: open an untrusted workspace with a workspace-level `specscribe.toolPath` set to a decoy; confirm the extension does **not** use it (falls back to bundled/PATH) until you trust the workspace. This is the [16.5](../planning-artifacts/epics.md) Marketplace gate and the [17.2](../planning-artifacts/epics.md) hardening target — do not ship it approximately.

### Reuse the singleton and the spawn machinery — do not fork a second open path
Direct-open Dashboard/Epics, the explorer/editor menus, and Open Status all funnel through the same panel and the same `runRenderer`/`load`/`push` flow. The only variation is the **initial surface** and (for the menus) the **entry point**. One open path, parametrized — not four copies. The in-flight `load()` coalescing (deferred-work.md:9, "overlapping debounced re-renders race — RESOLVED in 6.4") must stay intact; the manual refresh shares it.

### Tool resolution must not drift between spawn and terminal
`runRenderer` resolves `toolPath → bundled → PATH` and picks `dotnet <dll>` vs `<exe>`. Generate/Watch terminal commands must resolve **identically** or a user whose panel works will get a "command not found" in the terminal. Extract one helper; use it in both places.

### Byte-parity: trivially safe unless you touch C#
This story emits no HTML, so the golden fingerprint (memory: [golden-diff-normalization-gotchas]) is unaffected by construction. The **only** way to break it is the optional `configuredOutputRoot` payload field — and even that is not part of the generated site, so the golden stays green; still run it (Task 7) to be certain. Never add `prefers-color-scheme` or any style to the base sheet as a side effect (that was the 6.5 trap; not relevant here, but the discipline stands).

### Project structure notes
- **Extension lives in `extension/`** — self-contained, NOT part of the .NET solution or the generated-site pipeline (README.md:26–28). TypeScript changes go in [extension/src/extension.ts](../../extension/src/extension.ts); manifest in [extension/package.json](../../extension/package.json); the icon asset under `extension/` (e.g. `extension/media/`). esbuild bundles `src/extension.ts` → `dist/extension.js` ([esbuild.js](../../extension/esbuild.js), [package.json:16, 38–41](../../extension/package.json)).
- **No extension TS test harness exists** (scripts are `build`/`watch`/`typecheck`/`package` only). Automated gates for this story are `tsc --noEmit` + `esbuild` + JSON validity + (if option A) the C# suite. Functional verification of the manifest/menus/trust is the **F5 manual smoke** (extend the README). Be honest in Completion Notes about what is and isn't automatically covered.
- **Any optional C# change** goes in the single `src/SpecScribe/` project (`net10.0`, `Nullable enable`), `namespace SpecScribe;`, tagged `[Story 6.8]`, matching the heavy XML-doc style. **No new project, no namespace split** (seed-level, [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- This session/story runs on **`main`** (not a worktree) — edits target `C:\Dev\SpecScribe` directly; there is a background auto-committer on `main` (memory: [worktree-edits-must-target-worktree-path]). Keep commits coherent.
- Output dir is `SpecScribeOutput` (memory: [generate-output-dir-is-specscribeoutput]); never `--output docs/live`.

### Previous-story intelligence (Stories 6.4 / 6.5)
- **6.4** established the shim, the `specscribe webview` JSON seam, the CSP posture (nonce-locked `script-src`, `data:`-only `img-src`, `style-src 'unsafe-inline'`), the in-flight `load()` guard, and repo-root-anchored watchers. Its review applied the toolPath-scope RCE fix, UTF-8 stdout streaming, disposed-panel guards, and content-safe nonce substitution. **Do not regress** the disposed guards ([extension.ts:57–58, 118, 133](../../extension/src/extension.ts)) or the UTF-8 `setEncoding` ([extension.ts:210–211](../../extension/src/extension.ts)) when you add code around them.
- **6.5** added the clipboard helper (`copyHelperText`), the per-call **random sentinel** in `composeEntryHtml` (a security fix — a fixed literal could collide with shell text; keep it random), the theme bridge, and wrapped the clipboard write in try/catch. Its review confirmed `scope: "machine-overridable"` on `toolPath` *tightens* the setting — R5.4 (this story) is the manifest lock that completes that hardening.
- **Deferred items now relevant:** scoped re-render (deferred-work.md:15) is **not** this story (Story 6.11/6.4-polish). Nav-toggle a11y (deferred-work.md:20) is R7.4 — see OUT of scope.

### Non-negotiable invariants (architecture spine)
- **AD-1/AD-2** — extension is a thin shim, no rendering brain, no project knowledge; detection is path-existence only.
- **AD-6 / FR-17 / NFR-5** — read-only end to end; helpers/commands generate-and-hand-off, never write.
- **ADR 0003** — directory-scoped `.specscribe` is the source of truth for project behavior; VS Code settings carry host concerns only (`specscribe.openLocation` qualifies).
- **ADR 0005** — C# renders; the shim injects two runtime values and relays messages; a JSON export for a non-webview consumer (here: `configuredOutputRoot`) is an intended use, not a violation.

### Risk centers (where review will focus)
1. **Workspace Trust correctness (AC #2)** — the highest-stakes item; verify behaviorally, not just by manifest presence. A wrong `supported`/`restrictedConfigurations` value leaves the RCE surface open and fails the 16.5 gate.
2. **Accidental scope creep into 6.9/6.10/6.11** — no tree view, no status bar, no reveal-source, no watch-gap fix. Stay in discoverability/commands/trust/polish.
3. **A command path that writes** — any `applyEdit`/`fs.write`/settings `.update`/`SettingsStore` write breaks AD-6. Terminal handoff and reveal-only are the whole game.
4. **Spawn/terminal tool-resolution drift** — one shared helper, or users get "works in panel, fails in terminal."
5. **Hard-coding the epics surface key** — resolve it from a real payload (Task 0); a wrong key silently opens the dashboard instead of epics.
6. **Regressing 6.4/6.5 guards** — disposed checks, UTF-8 streaming, random sentinel, clipboard try/catch must survive the edits.

### Project Structure Notes
- Alignment: extension manifest + shim additions sit entirely in `extension/`; the optional payload field is a one-line addition to `WebviewCommand` in the single `src/SpecScribe/` project. No new modules, no package split, no naming departures from the existing `specscribe.*` command/setting namespace.
- No detected conflicts: `engines.vscode ^1.90.0` already covers every API used; no dependency additions required (esbuild/typescript/@types already present).

### References
- [epics.md](../planning-artifacts/epics.md) — Epic 6 goal (line 843–847, FR13 + FR35) and **Story 6.8 ACs (lines 1075–1111, source of truth)**; the Stories 6.8–6.12 seating comment (1065–1073) and the §2 constraints; Story 16.5 (2122–2135, R5.4 is its prerequisite) and Story 17.2 (2273, verifies the trust posture).
- [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md) — **the primary source**: §2 constraints; R1.1–R1.3 (§3 lines 42–44), R2.1–R2.4 (49–56), R3.3 (66), R5.2/R5.4 (79, 81), R7.1–R7.3 (93–95); §4 "Now" wave (line 109).
- [extension/src/extension.ts](../../extension/src/extension.ts) — the whole shim (singleton, `openStatus`, `push`, `runRenderer`, `createPanel`, `composeEntryHtml`, `errorHtml`, `debounce`). [extension/package.json](../../extension/package.json) — manifest to extend. [extension/README.md](../../extension/README.md) — F5 checklist to extend.
- [src/SpecScribe/Commands.cs](../../src/SpecScribe/Commands.cs) — `WebviewCommand` (payload shape line 64–71; scratch redirect 78–93) for the optional `configuredOutputRoot` field. [src/SpecScribe/SettingsStore.cs](../../src/SpecScribe/SettingsStore.cs) — `.specscribe` file (`FileName`/`ResolvePath`) for R5.2. [src/SpecScribe/Program.cs](../../src/SpecScribe/Program.cs) — CLI command registration (`generate`/`watch`/`webview`).
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) (thin-shim seam, C# renders, JSON-export clause), [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md) (directory-scoped settings + read-only helpers), [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) (re-affirms the C# path).
- Prior stories: [6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (runtime + shim), [6.5](6-5-host-aware-theming-and-explicit-helper-actions.md) (theming + helper + `machine-overridable` review). [deferred-work.md](deferred-work.md) — lines 15 (scoped re-render, not here), 20 (nav a11y / R7.4, out of scope).
- VS Code API: [Workspace Trust Extension Guide](https://code.visualstudio.com/api/extension-guides/workspace-trust) (`capabilities.untrustedWorkspaces` / `restrictedConfigurations` semantics), [Extension Manifest](https://code.visualstudio.com/api/references/extension-manifest) (`activationEvents`, `contributes`, `capabilities`).
- Memory: [worktree-edits-must-target-worktree-path] (main auto-committer), [generate-output-dir-is-specscribeoutput], [golden-diff-normalization-gotchas], [story-6-4-webview-runtime-live], [story-6-5-webview-theming-live], [epic-6-vscode-spike-and-renumber].

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Dev Story workflow, 2026-07-11)

### Debug Log References

- Task 0 payload capture: `dotnet SpecScribe.dll webview` → top keys `siteTitle, entry, document, surfaces`; `entry = index.html`, epics-index key = `epics.html`; no `repoRoot`, no output-root field pre-change.
- Post-change payload: `configuredOutputRoot = "SpecScribeOutput"` (default; the shim spawns `webview` without `--output`, so option A and B coincide for this story — as the story anticipated).
- `npm run typecheck` clean; `npm run build` → `dist/extension.js` (14.2 KB). `dotnet test` → 735 passed / 0 failed / 0 skipped.

### Completion Notes List

- **Manifest/routing only, read-only end to end.** Every AC is delivered by `extension/package.json` + `extension/src/extension.ts`, reusing the existing spawn/panel/`push` machinery; plus one host-delivered core datum (`configuredOutputRoot`). No new rendering, view model, or markdown parsing. Grep confirmed no write API in any command path — only the unchanged 6.5 clipboard helper and staged (`sendText(cmd, false)`, un-executed) terminal commands.
- **HTML byte-identical (constraint #4).** The optional `configuredOutputRoot` is payload-only data, not part of the generated site; the golden `GoldenContentFingerprint` stayed green. No C# rendering was touched.
- **Workspace Trust (AC #2) — the security-critical item.** `capabilities.untrustedWorkspaces = { supported: "limited", restrictedConfigurations: ["specscribe.toolPath"] }` makes VS Code ignore a *workspace-level* `toolPath` override in Restricted Mode (closing the spawn-redirect RCE surface) while user/machine values still apply. **This is the Story 16.5 Marketplace publish prerequisite and the Story 17.2 hardening verification target (epics.md:2273).** Manifest presence is verified; the *behavioral* untrusted-workspace check is a human F5 step (README checklist) — the extension has no TS test harness.
- **One shared tool resolver.** `resolveTool()` (extracted from `runRenderer`) feeds both the panel spawn and the terminal-handoff command string via `toolCommandLine()`, so spawn and terminal can never drift (`dotnet <dll>` vs `<exe>` vs `specscribe` on PATH resolved once).
- **Epics key resolved, not hard-coded.** `resolveTarget()` matches `/(^|\/)epics\.html$/` against the live payload surface keys, falling back to `entry` (mirroring `push`'s fallback). Confirmed against a real payload in Task 0 (`epics.html`).
- **Singleton preserved + a `PanelController` seam.** Direct-open/refresh steer the one panel through `active.reveal(target)` / `active.reload()`; the in-flight `load()` coalescing guard is intact, so a manual Refresh during an auto re-render won't double-spawn. 6.4/6.5 guards preserved: disposed checks, UTF-8 `setEncoding`, per-call random sentinel in `composeEntryHtml`, clipboard try/catch.
- **Menu label nuance.** VS Code menu items render the *command's* title (there is no per-menu title override), so the explorer/editor "Open in SpecScribe Status" intent surfaces as **Open Status** by reusing `specscribe.openStatus`. Faithful to "handler reuses openStatus"; a distinct label would need a duplicate command id (not worth the extra surface).
- **Scope discipline.** No tree view / status bar (6.9), no reveal-source (6.10), no watch-gap/multi-root fix (6.11), no Marketplace metadata/walkthrough (16.5), no `.specscribe`-through-webview (5.2), nav-toggle a11y (R7.4) left deferred. The panel-tab icon (`media/specscribe.svg`) is the in-scope R7.3 asset, distinct from the Marketplace icon.
- **Automated coverage honesty.** The C# side is unit-tested (`WebviewCommandTests`, +2). The TS manifest/menu/trust/open-beside/progress/error behaviors have **no automated gate** (no TS test runner exists); they are verified via the extended README F5 checklist as a manual human step, exactly as Stories 6.4/6.5 documented.

### File List

- `extension/package.json` — modified (activationEvents, capabilities.untrustedWorkspaces, 7 new commands, contributes.menus, `specscribe.openLocation` config)
- `extension/src/extension.ts` — modified (detection + setContext + folder-change re-eval; 8 command handlers; `PanelController` seam; `resolveTool`/`toolCommandLine`; `createPanel` open-location + iconPath; cold-start `withProgress`; actionable error notification; `configuredOutputRoot` in payload interface)
- `extension/media/specscribe.svg` — new (panel-tab icon)
- `extension/README.md` — modified (F5 smoke checklist; scope/story notes updated)
- `src/SpecScribe/Commands.cs` — modified (`configuredOutputRoot` payload field + `WebviewCommand.ResolveConfiguredOutputRoot` helper)
- `tests/SpecScribe.Tests/WebviewCommandTests.cs` — new (2 tests for `ResolveConfiguredOutputRoot`)

## Change Log

- 2026-07-11 — Implemented (dev-story). `extension/package.json` + `extension/src/extension.ts` deliver all four ACs: activation on `_bmad/config.toml`/`_bmad-output` detection + `specscribe.projectDetected` context key + `when`-gated commands/menus (AC #1); `capabilities.untrustedWorkspaces` locking `specscribe.toolPath` in Restricted Mode — the 16.5 Marketplace prerequisite / 17.2 verification target (AC #2); direct-open Dashboard/Epics, Refresh, Open Generated Site (payload `configuredOutputRoot`, option A), staged Generate/Watch terminal handoff, Open Project Settings (reveal-only), open-beside via `specscribe.openLocation` (AC #3); cold-start `withProgress`, actionable error notification, panel-tab icon (AC #4). One shared `resolveTool()` for spawn+terminal; epics key runtime-resolved; singleton + 6.4/6.5 guards preserved. Read-only end to end; HTML byte-identical (golden green). Tests 733→735 (+2 `WebviewCommandTests`). Status → review.
- 2026-07-11 — Story drafted (create-story) from the VS Code Native-Integration Recommendations (FR35, [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md)) "Now" quick-dev wave. Scope: R5.4 (Workspace Trust — the 16.5 Marketplace prerequisite), R1.1–R1.3 (activation + context key + explorer/editor menus), R2.1–R2.4 (direct-open / refresh / generate-watch terminal handoff / open-generated-site), R3.3 (open-beside + `specscribe.openLocation`), R5.2 (open project settings), R7.1–R7.3 (cold-start progress, actionable error notification, panel icon). **Not gated** — 6.4/6.5 are done and the extension exists; this story edits `extension/package.json` + `extension/src/extension.ts` (+ optional one-field `WebviewCommand` payload addition for open-generated-site). Manifest/routing only, reusing the existing spawn/panel machinery — no new rendering, read-only end to end, HTML surface byte-identical. Workspace-Trust manifest shape confirmed against current VS Code docs. Explicitly OUT: tree view/status bar (6.9), reveal-source (6.10), watch-gap/multi-root (6.11), Marketplace metadata/walkthrough (16.5), `.specscribe`-through-webview (5.2/R5.3), nav-toggle a11y (R7.4 — deferred unless owner folds in). Status → ready-for-dev.
