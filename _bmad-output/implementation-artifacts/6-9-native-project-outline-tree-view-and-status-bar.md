---
baseline_commit: 0a0d0f707c012c39799cf15d3d658033739bbc4a
seated_by: SCP 2026-07-11 (correct-course) — FR35, VS Code Native-Integration Recommendations
---

# Story 6.9: Native Project Outline — Tree View and Status Bar

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a VS Code user,
I want a persistent SpecScribe outline in the activity-bar sidebar and a status summary in the status bar,
so that I can glance at epic/story status and jump to any surface without opening the webview panel.

## ⚠️ GATED — build order dependency on Story 6.8

This story **extends the shim Story [6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) reshapes**, not the raw 6.4/6.5 shim. 6.8 is the "Now" wave; 6.9 is the "Next" wave ([docs/VSCodeIntegrationRecommendations.md §4](../../docs/VSCodeIntegrationRecommendations.md), lines 108–110). **Do not start 6.9 until [6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) is `done`.** 6.9 depends on three things 6.8 introduces:

1. **`specscribe.projectDetected` context key** (6.8 AC #1) — this story's `viewsWelcome` empty state and the tree/status-bar `when` clauses gate on it. Without it there is no "undetected workspace" signal to key the welcome view off.
2. **The parametrized direct-open path** (6.8 AC #3, `openStatus(context, initialSurface?)` + singleton reveal+push) — the tree's click→reveal reuses it verbatim. Do not fork a second panel-open path.
3. **The shared tool-resolution helper** extracted from `runRenderer` (6.8 Task 3) — the tree provider spawns the same tool the panel does; reuse that one helper so spawn resolution never drifts.

If, when this story is scheduled, 6.8 has slipped, **stop and flag it** rather than reimplementing 6.8's context key / direct-open / tool-resolution inline here (that would collide at merge). Prerequisites [6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (webview runtime + `specscribe webview` CLI) and [6.5](6-5-host-aware-theming-and-explicit-helper-actions.md) (theming + the six-stage accent tuning this story mirrors into contributed theme colors) are already **`done`**.

## What this story is (and isn't)

This is a **new native-surface story** — the first one in Epic 6 that renders SpecScribe status **outside the webview panel** (an activity-bar `TreeView` + a status-bar item). Per the Epic 2 retro rule ("split, don't absorb"), it is its own story precisely because it is a genuinely new structural surface, not a routing tweak.

Two layers of work:

1. **A new host-neutral `outline` export in the C# core** — epic/story records (id, title, status stage, counts, surface path, source artifact path, per-story helper command). This is **data, not rendering** — the "JSON export for a non-webview consumer" [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) §1 explicitly reserved (and [§2 of the recommendations](../../docs/VSCodeIntegrationRecommendations.md) confirms tree/status-bar text is the intended use of that clause). **No markdown parsing in TS, no HTML, no new view model** — the records derive from the *already-ingested* `EpicsModel` + `StatusStyles`.
2. **TypeScript host-delivery** — a `TreeDataProvider` mapping records → `TreeItem`s 1:1, a status-bar item, contributed `specscribe.status.*` theme colors, and `viewsWelcome`. The shim decides only *where VS Code shows it*; the core decided *what to say*.

### The five Epic 6 native-integration constraints ([§2](../../docs/VSCodeIntegrationRecommendations.md)) — **constraint #5 is LIVE for the first time**

1. **Thin shim, no rendering brain** (AD-1/AD-2). The `TreeDataProvider` maps core records to `TreeItem`s with **zero interpretation** — no status logic, no counting, no title derivation in TS. If you compute a stage, a count, or a label in TypeScript, it belongs in the core export instead.
2. **Read-only end to end** (AD-6, ADR 0003). Every tree/status-bar interaction is read-only: reveal a surface, open a markdown file (`showTextDocument`), copy a prompt to the clipboard. **Nothing writes a project artifact or mutates settings.**
3. **VS Code settings carry HOST concerns only** (ADR 0003). This story adds **no** new setting. (If you find yourself wanting one, it's probably project behavior that belongs in `.specscribe`.)
4. **The generated HTML surface stays byte-identical.** The `outline` export is a new field on the `webview` command's JSON payload (or a new `outline` command) — it is **not part of the generated site**, so the golden `GoldenContentFingerprint` is unaffected by construction. Re-run it anyway (Task 6) to be certain.
5. **Status surfaces derive from the six core-emitted `--status-*` stages, never VS Code's 3-severity palette.** **This is the story that first exercises constraint #5.** Tree-node status icons and the status-bar summary must carry the **stage** the core emits (`done`/`review`/`active`/`ready`/`drafted`/`pending`), colored by **contributed `specscribe.status.*` theme colors** that mirror the [Story 6.5 accent tuning](../../src/SpecScribe/assets/specscribe-webview-theme.css) — **not** `ThemeIcon`'s built-in `testing.iconPassed`/`problemsError`-style host severities (which would collapse the six-stage vocabulary the whole insight system depends on; memory: [specscribe-status-token-system]).

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 6.9 (lines 1126–1138)._

1. **Given** the rendering core exposes a host-neutral outline export (epic/story id, title, status stage, counts, surface path, source artifact path) — added as a new `outline` payload or `specscribe outline` command, not scraped HTML
   **When** the extension renders its activity-bar tree view
   **Then** epics and their stories appear as tree nodes mapped 1:1 from the export, with status conveyed by icons derived from the six core-emitted `--status-*` stages (via contributed `specscribe.status.*` theme colors, not VS Code's 3-severity palette)
   **And** an empty/undetected workspace shows a `viewsWelcome` guidance state rather than a dead view.

2. **Given** the tree view and a status-bar item
   **When** the user interacts with them (all read-only)
   **Then** clicking a node reveals that surface in the webview panel, context actions open the source markdown or copy the story's helper prompt, and the status-bar item shows a summary count (e.g. active/review) that opens the status panel
   **And** a failed refresh is shown as a stale/error indicator rather than silently wrong data.

### AC → Recommendation → Surface map

| AC | Recommendation(s) | Where it lands |
|---|---|---|
| #1 | R3.1 (activity-bar `TreeView`, core `outline` export, `specscribe.status.*` theme colors), R1.5 (`viewsWelcome`) | Core: `outline` on the `webview` payload ([Commands.cs](../../src/SpecScribe/Commands.cs) + a new `OutlineNode` model). Manifest: `contributes.viewsContainers.activitybar`, `contributes.views`, `contributes.colors`, `contributes.viewsWelcome`. `extension.ts`: `TreeDataProvider` + a shared payload provider. |
| #2 | R3.1 (interactions: reveal / open-source / copy-prompt), R3.2 (status-bar item + stale indicator) | `extension.ts`: node `command`/context-menu handlers reusing 6.8's direct-open path + `showTextDocument` + clipboard; a `StatusBarItem`; a stale/error state on both surfaces. Manifest: `contributes.commands` + `contributes.menus` (`view/item/context`, `view/title`). |

## Critical current-state facts the dev MUST internalize

Read [extension/src/extension.ts](../../extension/src/extension.ts) and [extension/package.json](../../extension/package.json) **as they stand after 6.8 merges** (the line numbers below are against the 6.4/6.5 baseline `0a0d0f7`; 6.8 will have moved them — re-anchor against the actual post-6.8 file). Then internalize the core seams:

1. **The payload is acquired panel-scoped today.** In the baseline, `runRenderer` is called only from inside `openStatus`, and `cache` is a local of that closure ([extension.ts:44–67](../../extension/src/extension.ts)). **A tree view needs the data with no panel open.** This story must promote payload acquisition to a **module-level shared provider** that both the panel and the tree read from (see Design decision "Shared payload provider"). 6.8 already begins this by hoisting a `activeController?.reload()` reference (6.8 Task 3 refresh command) — **extend that**, don't add a parallel cache.
2. **`push(target, reason)` swaps surfaces by output-relative path** ([extension.ts:69–75](../../extension/src/extension.ts)); `cache.surfaces[target]` keys are the C# `OutputRelativePath`s. The outline node's `surfacePath` must be **exactly one of those keys** so a tree click can `push()` to it. (Epic pages and every story page/placeholder ARE surfaces in the bundle — see `RenderWebviewSurfaces`, [SiteGenerator.cs:768–826](../../src/SpecScribe/SiteGenerator.cs).)
3. **`runRenderer` spawns `specscribe webview` and parses stdout JSON** ([extension.ts:188–225](../../extension/src/extension.ts)) with a 60 s timeout, UTF-8 stream decode, and in-flight coalescing (`loading ??= …`). The tree provider reuses this **exact** spawn (via 6.8's shared tool-resolution helper) — do not add a second spawn function.
4. **The `WebviewPayload` TS interface** ([extension.ts:22–34](../../extension/src/extension.ts)) mirrors the C# payload. Add an `outline` field to **both** the C# payload ([Commands.cs:64–70](../../src/SpecScribe/Commands.cs)) and this interface.
5. **`RenderWebviewSurfaces()` already iterates `_epicsModel.Epics` → `.Stories`** ([SiteGenerator.cs:788–818](../../src/SpecScribe/SiteGenerator.cs)) with the retro map and per-epic progress in hand. **The outline export is built from the same loop's inputs** — do not re-ingest; reuse `_epicsModel`, `_progress`, `EpicRetroMap`, and each surface's `OutputRelativePath`.
6. **`StoryInfo` already carries what a node needs** ([EpicsModel.cs:7–32](../../src/SpecScribe/EpicsModel.cs)): `Id`, `Title`, `Status` (raw), `ArtifactOutputPath` (the **surface path** to `push()`), `ArtifactSourcePath` (the **source `.md`** for "open source", relative to `_bmad-output/`), `TasksDone`/`TasksTotal` (counts). `EpicInfo` carries `Number`, `Title`, `Stories`, `HasRetrospective`.
7. **`StatusStyles` is the single stage classifier** ([StatusStyles.cs](../../src/SpecScribe/StatusStyles.cs)). Per-story stage = `StatusStyles.ForStory(story)` (→ `done`/`review`/`active`/`ready`/`drafted`). Per-epic stage for a **visual** surface = `StatusStyles.ForEpicWithRetrospective(epic)` (→ adds `pending`; downgrades an all-done-but-un-retro'd epic to `review`). The tree is a visual status surface, so **use `ForEpicWithRetrospective` for epic nodes** (memory: [story-6-2-section-view-models-live] harmonized every epic-status surface onto it). The canonical stage lists are `StatusStyles.StoryStages` (5) and `StatusStyles.EpicStages` (6) — iterate those, never hand-list, so a future stage can't be silently dropped.
8. **The webview-theme accents already exist as CSS tokens** ([specscribe-webview-theme.css:100–168](../../src/SpecScribe/assets/specscribe-webview-theme.css)) but are **unreachable from the extension host** — they live in a `<style>` bridge inside the webview. `ThemeIcon` color IDs only accept **contributed theme colors**. So this story must **re-declare** those per-theme accent values as `contributes.colors` (`specscribe.status.done` … `specscribe.status.pending`) in `package.json`, mirroring the 6.5 tuning for `light`/`dark`/`highContrast`/`highContrastLight`. This duplication is inherent to the platform (R3.1's "budget for it"); add a Completion Note tying the two sources together so a future accent re-tune updates both.
9. **The per-story helper prompt/command is core-composed today.** The webview's single toolbar button copies `WebviewHelpers.CodeReviewPrompt(siteTitle)` ([WebviewRenderAdapter.cs:86,106](../../src/SpecScribe/WebviewRenderAdapter.cs)). For a **per-story** "copy helper prompt" (AC #2), reuse `BmadCommands`' per-story command logic ([BmadCommands.cs:214–245](../../src/SpecScribe/BmadCommands.cs)) — `commands.Command("dev-story", story.Id)` / `"code-review"` keyed on status — so the node carries a ready-to-paste command string composed in C#. **Do not author the prompt/command in TypeScript** (AD-2). If the module exposes no matching command, the node's copy-prompt action is simply absent (nullable field).
10. **The current `webview` payload does NOT carry `repoRoot`.** [deferred-work.md:8](deferred-work.md) claims 6.4 added `repoRoot` and the extension anchors watchers to it — **but the committed [Commands.cs:64–70](../../src/SpecScribe/Commands.cs) payload has no `repoRoot`, and [extension.ts:141–142](../../extension/src/extension.ts) anchors watchers to `workspace.workspaceFolders[0]`, not a payload field.** Treat that deferred-work note as **stale/aspirational**, not fact. **Capture the real payload** (Task 0) before designing against it. (Watch-root derivation from core-resolved roots is R6.2 → **Story 6.11**, not this story — keep the existing workspace-folder watchers.)
11. **`engines.vscode` is `^1.90.0`** ([package.json:9–11](../../extension/package.json)). Every API this story uses — `contributes.viewsContainers`/`views`/`viewsWelcome`/`colors`, `window.createTreeView` / `registerTreeDataProvider`, `TreeItem`/`ThemeIcon` with a `ThemeColor`, `window.createStatusBarItem`, `EventEmitter`/`onDidChangeTreeData`, `showTextDocument` — is available at 1.90. **No engine bump.**
12. **No extension TS test harness exists** (scripts are `build`/`watch`/`typecheck`/`package`). Automated gates are `tsc --noEmit` + esbuild + JSON validity + the C# suite (for the new export + its test). Functional verification of the tree/status-bar/welcome is the **F5 manual smoke** (extend [extension/README.md](../../extension/README.md)). Be honest in Completion Notes about the coverage boundary.

## Design decisions captured at create-story

### The outline export shape (AC #1) — **Option A: a field on the `webview` payload (recommended)**

R3.1 offers two data paths: a separate `specscribe outline` command, **or (better)** an `outline` section on the existing `webview` payload. **Take Option A** — one spawn, one data path, no drift, and the shim already spawns `webview` for the panel so the tree reuses that cache.

Add to `RenderWebviewSurfaces()` (or a sibling method it calls) an `IReadOnlyList<OutlineEpic>` built from `_epicsModel` + `_progress` + `EpicRetroMap`, and carry it on `WebviewBundle`. Serialize it as `outline` on the payload in [Commands.cs](../../src/SpecScribe/Commands.cs).

**Proposed core model** (in `src/SpecScribe/`, `namespace SpecScribe;`, tagged `[Story 6.9]`):

```csharp
public sealed record OutlineStory(
    string Id, string Title, string Stage,       // Stage = StatusStyles.ForStory(story), e.g. "active"
    string? SurfacePath,                         // story.ArtifactOutputPath — the push() key (null → placeholder)
    string? SourcePath,                          // story.ArtifactSourcePath — the .md to showTextDocument (workspace-rel)
    int TasksDone, int TasksTotal,
    string? HelperCommand);                       // BmadCommands per-story command, or null if none

public sealed record OutlineEpic(
    int Number, string Title, string Stage,       // Stage = StatusStyles.ForEpicWithRetrospective(epic)
    string? SurfacePath,                          // the epic page's OutputRelativePath (the push() key)
    int StoriesTotal, int StoriesDone,
    IReadOnlyList<OutlineStory> Stories);

public sealed record ProjectOutline(
    IReadOnlyList<OutlineEpic> Epics,
    OutlineSummary Summary);                       // for the status bar — see below

public sealed record OutlineSummary(int Active, int Review, int Done, int Total);
```

- **`SurfacePath` must equal a `surfaces[...]` key** so the tree click can `push()` it. For epics/stories these are the `OutputRelativePath`s the bundle already emits — resolve them in the same loop that builds the surfaces (fact #5), not by re-deriving paths. A **placeholder** story (no artifact) has `SurfacePath` = the placeholder surface's key (it IS a surface — [SiteGenerator.cs:804–806](../../src/SpecScribe/SiteGenerator.cs)); it is still clickable.
- **`Stage` strings are the css-class stage names** — the exact keys the contributed `specscribe.status.<stage>` colors and the icon map are keyed on. Emit them from `StatusStyles`, never re-spell them in TS.
- **`Summary` is computed core-side** (R3.2 "computed core-side into the payload"): count stories by stage across all epics. "e.g. active/review" per the AC — expose at least `Active` and `Review`; include `Done`/`Total` so the status-bar text and tooltip have what they need without TS arithmetic.
- **`HelperCommand`** — reuse `BmadCommands`' per-story selection (fact #9). Prefer the single most-actionable command for the story's status (dev-story when ready/active, code-review when review/…); null when the module offers none.

**Option B (fallback, only if activation-time cost is a real problem):** a dedicated `specscribe outline` CLI command that runs `GenerateAll()` but **skips** `RenderWebviewSurfaces()` (no per-surface HTML render, no big JSON) and emits only the `ProjectOutline`. Cheaper for a tree-only consumer, but a **second data path** the panel and tree could drift on, and the tree click still needs the surface keys the webview bundle owns. **Do not take B unless** measured activation latency forces it; if you do, record it as a create-story deviation and keep the surface-key contract identical. **Recommend A.**

### Shared payload provider (the central refactor, AC #1 + #2)

The tree, the status bar, and the panel must all read **one** cached payload from **one** spawn, and all refresh together. Introduce a small module-level provider (extend 6.8's hoisted `activeController`, don't add a parallel one):

- **Owns** the cached `WebviewPayload`, the in-flight `loading` coalescer (hoisted out of `openStatus`), and a `vscode.EventEmitter` fired on every successful (re)load.
- **`getOutline()`** returns `cache?.outline` (undefined until first load).
- **The `TreeDataProvider`** subscribes its `onDidChangeTreeData` to the provider's change event; `getChildren()` reads `getOutline()`. VS Code calls `getChildren()` lazily when the view is first revealed — so **first data acquisition is naturally lazy** (a spawn on first reveal), not an eager activation cost. Show nothing (→ `viewsWelcome`) until the first payload resolves.
- **The status-bar item** subscribes to the same event and re-renders its text from `getOutline()?.summary`.
- **The panel** (`openStatus`) reads the same cache instead of its own local — so opening the panel after the tree has loaded reuses the cache (no second spawn).
- **File watchers** currently live inside `openStatus` and only exist while the panel is open ([extension.ts:130–148](../../extension/src/extension.ts)). For the tree to stay live with no panel, the watchers must move to the **provider's** lifetime (created when the provider is first activated, disposed in `deactivate`). **Keep the existing globs** (`_bmad-output/**/*.md`, `docs/adrs/**/*.md`) and the 400 ms debounce **exactly** — the yaml/toml watch-gap fix and core-derived roots are **Story 6.11 (R6.1/R6.2), explicitly OUT of scope here.** You are relocating the watcher's *ownership*, not changing *what* it watches.
- **Disposal discipline (do not regress 6.4/6.5):** the disposed-panel guards, the UTF-8 `setEncoding`, the random-sentinel `composeEntryHtml`, and the clipboard try/catch must all survive this refactor.

### Tree view contribution (AC #1)

- **`contributes.viewsContainers.activitybar`:** one container `{ id: "specscribe", title: "SpecScribe", icon: <bundled svg> }` (reuse/adjacent to the R7.3 panel-tab icon 6.8 bundles — an activity-bar icon is a monochrome SVG that VS Code tints).
- **`contributes.views`:** one tree view `{ id: "specscribe.outline", name: "Project Outline" }` in that container.
- **`when` gating:** gate the view/container on `specscribe.projectDetected` (6.8's key) so the SpecScribe activity-bar icon doesn't appear in non-SpecScribe repos. (If a `when` on the container is awkward, at minimum the `viewsWelcome` covers the undetected case — see below.)
- **`TreeDataProvider<OutlineNode>`** (a discriminated `{ kind: 'epic', epic } | { kind: 'story', story, epic }`): top level → epics (label `"Epic N: <title>"`, description = `StoriesDone/StoriesTotal`), children → stories (label `"N.M <title>"`, description = tasks `TasksDone/TasksTotal` when > 0). **Collapsible** epics, **leaf** stories. Map 1:1 — no filtering, no re-sorting (emit in the core's order).

### Status iconography — contributed theme colors, not host severities (AC #1, constraint #5)

- **`contributes.colors`:** six entries `specscribe.status.{done,review,active,ready,drafted,pending}`, each with `defaults` for `light`/`dark`/`highContrast`/`highContrastLight` copied from the [6.5 tuning](../../src/SpecScribe/assets/specscribe-webview-theme.css:109–168). (The css file also defines a `deferred` accent, but the outline only surfaces the story/epic stages — six is correct here; do not contribute `deferred` unless the outline ever emits it.)
- **Icon:** `TreeItem.iconPath = new vscode.ThemeIcon(<glyph>, new vscode.ThemeColor('specscribe.status.' + stage))`. Choose a **stable shape per stage** (e.g. a filled/hollow circle or a codicon like `circle-filled`/`circle-outline`/`check`/`eye`) so the shape channel reinforces color (UX-DR17: never color-only) — but keep it simple; the color IS the semantic layer. A single `stage → { icon, colorId }` map in TS is the only "logic" allowed, and it is a pure lookup on the core-emitted stage string.
- **NEVER** use `testing.iconPassed`, `problemsError`/`Warning`, or any built-in severity ThemeIcon — that collapses six stages onto three (constraint #5).

### `viewsWelcome` empty state (AC #1, R1.5)

`contributes.viewsWelcome` for view `specscribe.outline`, shown `when: !specscribe.projectDetected`: a short guidance message — "No SpecScribe project detected. Open a folder containing `_bmad-output`." with a docs/command link (e.g. a markdown link to a walkthrough — but **not** the walkthrough contribution itself, that's 16.5). Also cover the **detected-but-not-yet-loaded** and **empty-outline** cases gracefully (a "Loading…"/"No epics found" node or a second welcome clause) so a detected repo mid-spawn never shows a dead view.

### Interactions (AC #2) — all read-only

- **Click a node → reveal in the panel.** Set `TreeItem.command` to an internal command (e.g. `specscribe.revealSurface` with the node's `surfacePath` as arg) that calls **6.8's parametrized open path** (`openStatus(context, surfacePath)` → reveal + `push`). Reuse it; do not fork.
- **Context action "Open Source" (`view/item/context`)** on a node with a `SourcePath`: `vscode.window.showTextDocument(vscode.Uri.file(path.join(workspaceRoot, '_bmad-output', sourcePath)))` — read-only editor open, no mutation. This is the tree's own open-source; it shares the `showTextDocument` primitive with Story 6.10's webview reveal-source but is a distinct entry point (6.10 is OUT of scope). Resolve the path from the workspace root + the core-emitted relative `SourcePath` (fact #6). Absent `SourcePath` (undrafted story) → omit the action (`when` on a context-value).
- **Context action "Copy Helper Prompt"** on a node with a `HelperCommand`: write it to the clipboard via `vscode.env.clipboard.writeText` (+ the same info toast 6.5's `copyHelperText` shows). Absent → omit.
- **Use `TreeItem.contextValue`** (e.g. `"story-with-source"`, `"story-with-helper"`, `"epic"`) to `when`-gate which context actions appear per node.

### Status-bar item (AC #2, R3.2)

- `vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left)` with text like `$(checklist) SpecScribe: 3 active · 2 review` from `outline.summary` (core-computed — no TS counting). `command` = `specscribe.openStatus` (opens the panel). Tooltip: fuller counts (`Done/Total`).
- **Show only in a detected SpecScribe repo** — subscribe to the same detection the tree uses; hide (`statusBarItem.hide()`) when `!projectDetected` or when there's no data yet.
- **Stale/error indicator (AC #2 "failed refresh … not silently wrong data"):** when a (re)load **throws**, do not leave the last-good text looking current. Switch the status-bar item to a warning presentation (`$(warning) SpecScribe: data stale` + `backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground')`, tooltip naming the error) and set the tree to a stale affordance (e.g. a top-level "⚠ Last refresh failed — showing cached data" node, or a `viewsWelcome`/description marker). Clear it on the next successful load. **This is an explicit AC — a failed refresh must be visible on both surfaces.**

## Scope

### IN scope
- **Core (`src/SpecScribe/`, single project, `net10.0`, `namespace SpecScribe;`, `[Story 6.9]`):** a `ProjectOutline`/`OutlineEpic`/`OutlineStory`/`OutlineSummary` model; building it in/next to `RenderWebviewSurfaces()` from `_epicsModel` + `_progress` + `EpicRetroMap` + `StatusStyles` + `BmadCommands` (per-story helper command); adding `Outline` to `WebviewBundle`; serializing `outline` on the `webview` payload ([Commands.cs](../../src/SpecScribe/Commands.cs)); C# tests for the export (stage mapping, surface-key match, summary counts, placeholder story, retro-gated epic stage).
- **Manifest (`extension/package.json`):** `contributes.viewsContainers.activitybar` (+ activity-bar icon asset), `contributes.views` (`specscribe.outline`), `contributes.colors` (six `specscribe.status.*`), `contributes.viewsWelcome`, `contributes.commands` (reveal-surface internal + open-source + copy-helper-prompt + optional refresh-outline), `contributes.menus` (`view/item/context`, `view/title` for a refresh button), all `when`-gated on `specscribe.projectDetected` / `contextValue`.
- **Shim (`extension/src/extension.ts`):** the shared payload provider (hoist cache + `loading` + change emitter + relocate watchers out of `openStatus`); the `WebviewPayload.outline` interface field; the `TreeDataProvider` + registration; the status-bar item; the interaction handlers (reveal reuses 6.8's open path; open-source `showTextDocument`; copy-prompt clipboard); the stale/error state on both surfaces; a `stage → {icon,colorId}` lookup.
- **Docs:** extend [extension/README.md](../../extension/README.md) F5 checklist for the tree (appears only in a SpecScribe repo; epics→stories with stage icons; click reveals in panel; open-source opens the `.md`; copy-prompt copies; welcome view in a non-SpecScribe folder; status-bar count; forced-error shows the stale indicator on both).

### OUT of scope (do NOT start here)
- **Reveal-source *from the webview*** (a `revealSource` webview→host message) — **Story 6.10**. This story's "Open Source" is a **tree** context action only; do not add the webview message path or its payload metadata.
- **File-change reactivity hardening** — **Story 6.11**: the yaml/toml watch gap (R6.1), core-derived watch roots (R6.2), visibility-aware refresh (R6.3), multi-root (R3.4). **Keep the existing `_bmad-output/**/*.md` + `docs/adrs/**/*.md` globs, the 400 ms debounce, and `workspaceFolders[0]` exactly.** You may *relocate* watcher ownership to the provider (needed for a panel-less tree), but you may **not** change what/where it watches or add multi-root logic.
- **New VS Code settings.** No `contributes.configuration` here (constraint #3). The status bar and tree read core data; nothing is configurable this story.
- **Marketplace metadata / walkthrough / platform VSIX (R1.6/R1.4/R8.1)** — **Story 16.5**. The activity-bar icon (a functional contribution) IS in scope; `categories`/`keywords`/marketplace-`icon`/`contributes.walkthroughs` are not.
- **Diagnostics / Problems panel (R8.3)** — **Story 6.12**.
- **Scoped/warm re-render (R6.4, deferred-work.md:15)** — not this story; the provider still spawns a full `webview` render per refresh (coalesced), exactly as 6.4 does.
- **Webview nav-toggle a11y (R7.4 / deferred-work.md:20)** — still deferred (webview-content concern; this story adds no webview interaction).
- **New rendering, view models, markdown parsing in TS, package/namespace split, HTML-surface changes** (seed-level, forbidden). The `outline` records are data derived from the already-built `EpicsModel`, not a new render.

## Tasks / Subtasks

- [ ] **Task 0 — Baseline & payload reconnaissance** (prereq for AC #1)
  - [ ] Confirm Story [6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) is `done` in [sprint-status.yaml](sprint-status.yaml); if not, **halt and flag** (this story extends 6.8's shim — see the GATED notice).
  - [ ] Re-capture current `HEAD` for the byte-parity diff (the `main` auto-committer may sit between recorded `baseline_commit` `0a0d0f7` and HEAD — memory: [worktree-edits-must-target-worktree-path]).
  - [ ] Build (`dotnet build src/SpecScribe`) and run `specscribe webview` from the repo root; **capture the JSON**. Record the real payload shape (confirm it has **no** `repoRoot` and **no** `outline` yet — fact #10), and the exact **surface keys** (`OutputRelativePath`s) for the dashboard, epics index, an epic page, and a story page + a placeholder — the outline `SurfacePath`s must match these.

- [ ] **Task 1 — Core `outline` export** (AC: #1)
  - [ ] Add `ProjectOutline`/`OutlineEpic`/`OutlineStory`/`OutlineSummary` records (`src/SpecScribe/`, `[Story 6.9]`).
  - [ ] Build the outline in/beside `RenderWebviewSurfaces()` from `_epicsModel` + `_progress` + `EpicRetroMap`: per-epic stage via `StatusStyles.ForEpicWithRetrospective`, per-story stage via `StatusStyles.ForStory`; `SurfacePath` = each node's `OutputRelativePath` (resolved in the same loop, not re-derived); `SourcePath` = `story.ArtifactSourcePath`; counts from `TasksDone`/`TasksTotal` and story-stage tallies; `HelperCommand` via the `BmadCommands` per-story command selection ([BmadCommands.cs:214–245](../../src/SpecScribe/BmadCommands.cs)).
  - [ ] Compute `OutlineSummary` (active/review/done/total) core-side.
  - [ ] Add `Outline` to `WebviewBundle` ([WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs)); serialize `outline` on the payload ([Commands.cs:64–70](../../src/SpecScribe/Commands.cs)).

- [ ] **Task 2 — Shared payload provider refactor** (AC: #1, #2)
  - [ ] Hoist the payload cache, the in-flight `loading` coalescer, and a `vscode.EventEmitter` to a module-level provider (extend 6.8's `activeController`, don't fork). Expose `getOutline()` + `onDidChange`.
  - [ ] Relocate the `_bmad-output/**/*.md` + `docs/adrs/**/*.md` watchers (400 ms debounce) from `openStatus` to the provider's lifetime so the tree stays live with no panel. **Keep globs/debounce/`workspaceFolders[0]` unchanged** (watch hardening is 6.11).
  - [ ] Point `openStatus`/the panel at the shared cache; verify the in-flight guard and all disposed/UTF-8/sentinel/clipboard guards from 6.4/6.5 still hold.
  - [ ] Add `outline` to the `WebviewPayload` TS interface.

- [ ] **Task 3 — Tree view** (AC: #1)
  - [ ] Manifest: `viewsContainers.activitybar` (+ bundled monochrome activity-bar SVG), `views` (`specscribe.outline`), `contributes.colors` (six `specscribe.status.*` with light/dark/HC/HC-light defaults mirroring [6.5](../../src/SpecScribe/assets/specscribe-webview-theme.css)), `viewsWelcome` (`when: !specscribe.projectDetected` + a loading/empty clause).
  - [ ] `TreeDataProvider`: epics → stories, 1:1 from `getOutline()`; labels/descriptions from records only; `ThemeIcon(glyph, ThemeColor('specscribe.status.'+stage))` via a pure `stage→{icon,colorId}` lookup. Subscribe `onDidChangeTreeData` to the provider event. Register via `window.createTreeView`/`registerTreeDataProvider`.
  - [ ] Verify a non-SpecScribe folder shows the welcome state (or no container), and a detected repo mid-spawn shows a graceful loading state — never a dead view.

- [ ] **Task 4 — Interactions + status bar** (AC: #2)
  - [ ] Node `command` → internal `specscribe.revealSurface` → **6.8's** parametrized open path (reveal + `push(surfacePath)`).
  - [ ] `view/item/context`: "Open Source" (`showTextDocument` on the resolved `_bmad-output/<SourcePath>`, read-only) and "Copy Helper Prompt" (clipboard), each `when`-gated on `contextValue`. Undrafted/no-helper nodes omit the respective action.
  - [ ] Status-bar item from `outline.summary` (core-computed), `command: specscribe.openStatus`, tooltip with fuller counts; hidden when `!projectDetected`/no data.
  - [ ] Optional `view/title` "Refresh" button → the provider's reload (shares the in-flight guard).

- [ ] **Task 5 — Stale/error state** (AC: #2)
  - [ ] On a load/refresh **throw**: switch the status-bar item to a warning presentation (`$(warning)` + `statusBarItem.warningBackground` + error tooltip) AND surface a tree stale affordance (top node or welcome marker). Clear both on the next success. Verify by forcing a bad `toolPath`.

- [ ] **Task 6 — Verify, guard, document** (AC: all)
  - [ ] `cd extension && npm install && npm run typecheck && npm run build` — TS compiles, esbuild bundles clean, `package.json` is valid JSON and the manifest loads without activation errors.
  - [ ] `dotnet test` whole suite green **including `GoldenContentFingerprint`** ([SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)) — the `outline` payload field is not part of the generated site, so golden stays green (memory: [golden-diff-normalization-gotchas]). Add C# assertions: outline present on the payload, stages match `StatusStyles`, every `SurfacePath` matches a real surface key, summary counts correct, a placeholder story maps correctly, a retro-pending all-done epic reads `review`.
  - [ ] Confirm read-only end to end: grep the diff for any write API (`writeFile`, `fs.write*`, `workspace.applyEdit`, settings `.update(`, `SettingsStore` writes) — **none** in any code path. Clipboard + `showTextDocument` are the only host effects and both are read-only.
  - [ ] Extend [extension/README.md](../../extension/README.md) F5 checklist (list above). Note (as 6.4/6.5/6.8 did) the F5 smoke is a human step; be explicit in Completion Notes about the automated-coverage boundary (no TS test harness).

## Dev Notes

### This is data + host-delivery, not rendering — keep the shim brainless
The `outline` records are **derived from the already-ingested `EpicsModel`** and classified by the **existing `StatusStyles`**; the TypeScript maps them to `TreeItem`s with a pure per-stage lookup and nothing else. If you compute a status, a count, a label, or a path in TypeScript, you've crossed AD-1/AD-2 — push it into the core export. The one and only "logic" allowed in TS is `stage → {icon, colorId}`, and that's a constant map keyed on a string the core emitted.

### Constraint #5 is the review magnet — stages, not severities
This is the first native surface that shows status. The six contributed `specscribe.status.*` colors and the icon map must carry the **stage** vocabulary (`done`/`review`/`active`/`ready`/`drafted`/`pending`) — never `ThemeIcon`'s built-in `iconPassed`/`problemsError` severities. Reviewers will check this first (memory: [specscribe-status-token-system]). Keep the contributed color defaults faithful to the [6.5 accent tuning](../../src/SpecScribe/assets/specscribe-webview-theme.css) and note in Completion Notes that the two now co-define the accents (a future re-tune touches both).

### Read-only is a spine invariant (AD-6 / ADR 0003 / NFR-5)
Reveal a surface, open a `.md` read-only, copy a prompt — that is the whole interaction vocabulary. No node action writes an artifact or mutates a setting. The helper *command* is copied to the clipboard for the user to run themselves; the extension never runs it. Prove write-freedom in Task 6.

### The refactor is the risk, not the tree
Registering a `TreeDataProvider` is routine; the load-bearing change is promoting payload acquisition from **panel-scoped** to a **shared provider** that survives with no panel, drives three consumers, and refreshes them together — without regressing the 6.4/6.5 guards (disposed checks, UTF-8 streaming, random sentinel, clipboard try/catch, in-flight coalescing). Do this as a careful extension of 6.8's hoisted controller, not a rewrite. Watchers move ownership only; **what** they watch is frozen until 6.11.

### Reuse 6.8's seams — do not re-implement them
6.8 owns the `specscribe.projectDetected` context key, the parametrized direct-open path, and the shared tool-resolution helper. This story **consumes** all three. Re-implementing any of them here would collide at merge and duplicate the surface 17.2's hardening pass audits. If 6.8's shape differs from what this story assumed (e.g. the controller's method names), adapt to 6.8's actual API — 6.8 is the source of truth for the shim's post-"Now"-wave shape.

### Byte-parity: safe by construction
The only C# change is an additive payload field + its model; it emits no HTML, so the golden fingerprint is unaffected. Still run it (Task 6). Never add a `prefers-color-scheme` rule or any style to the base sheet as a side effect (the 6.5 trap; not relevant here, but the discipline stands).

### Previous-story intelligence (6.4 / 6.5 / 6.8)
- **6.4** established the shim, the `specscribe webview` JSON seam, the CSP posture, the in-flight `load()` coalescer, and the disposed/UTF-8 guards. **Do not regress** them ([extension.ts:57–58, 118, 133, 210–211](../../extension/src/extension.ts)).
- **6.5** added the clipboard `copyHelperText` helper, the per-call random sentinel in `composeEntryHtml` (a security fix — keep it random), the theme bridge (whose accent values this story re-declares as contributed colors), the clipboard try/catch, and confirmed `toolPath` `machine-overridable`.
- **6.8** (must be `done` first) reshapes the manifest + shim: activation on detection, the `projectDetected` key, direct-open commands, `openLocation`, the shared tool-resolution helper, cold-start progress, actionable error notification, and the panel-tab icon. This story builds directly on all of it.
- **Deferred items:** the `repoRoot`-in-payload claim (deferred-work.md:8) is **stale** — verify against the real payload, don't trust it (fact #10). Scratch-dir collision on concurrent same-repo spawns (deferred-work.md:21) and scoped re-render (deferred-work.md:15) are **not** this story.

### Non-negotiable invariants (architecture spine)
- **AD-1/AD-2** — thin shim, no rendering brain, no project knowledge; the outline is core-emitted data, the tree is pure host-delivery.
- **AD-6 / FR-17 / NFR-5** — read-only end to end; the copy-prompt/open-source/reveal actions never write.
- **ADR 0003** — directory-scoped `.specscribe` is the source of truth for project behavior; this story adds no VS Code settings.
- **ADR 0005** — C# decides what to say; the "JSON export for a non-webview consumer" clause is exactly what the `outline` field is (§1), not a violation.

### Risk centers (where review will focus)
1. **Constraint #5 (stage vs severity)** — contributed `specscribe.status.*` colors + a stage-keyed icon map; any built-in severity ThemeIcon fails the six-stage rule.
2. **The shared-provider refactor** — a panel-less data lifetime driving tree + status bar + panel, without regressing 6.4/6.5 guards or the in-flight coalescer.
3. **Surface-key fidelity** — every `SurfacePath` must match a real `surfaces[...]` key or a click silently opens the dashboard (Task 0 verifies against a real payload).
4. **Scope creep into 6.10/6.11** — no webview reveal-source message, no watch-gap/multi-root/core-derived-roots changes. Relocate watcher ownership only.
5. **A command path that writes** — any `applyEdit`/`fs.write`/settings `.update`/`SettingsStore` write breaks AD-6.
6. **Stale/error visibility (AC #2)** — a failed refresh must visibly degrade both the tree and the status bar, not leave last-good data looking current.
7. **Re-implementing 6.8's seams** — consume the context key / direct-open / tool-resolution helper; don't duplicate them.

### Project Structure Notes
- **Extension lives in `extension/`** — self-contained, NOT part of the .NET solution or the generated-site pipeline. TS in [extension/src/extension.ts](../../extension/src/extension.ts); manifest in [extension/package.json](../../extension/package.json); the activity-bar icon under `extension/` (e.g. `extension/media/`). esbuild bundles `src/extension.ts` → `dist/extension.js`.
- **Core changes** go in the single `src/SpecScribe/` project (`net10.0`, `Nullable enable`), `namespace SpecScribe;`, tagged `[Story 6.9]`, matching the heavy XML-doc style. **No new project, no namespace split** (seed-level, [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- **This session/story runs on `main`** (not a worktree) — edits target `C:\Dev\SpecScribe` directly; a background auto-committer runs on `main` (memory: [worktree-edits-must-target-worktree-path]). Keep commits coherent.
- Output dir is `SpecScribeOutput` (memory: [generate-output-dir-is-specscribeoutput]); never `--output docs/live`.
- **No extension TS test harness exists** — automated gates are `tsc --noEmit` + esbuild + JSON validity + the C# suite (for the export). Tree/status-bar/welcome verification is the F5 manual smoke; be honest about it in Completion Notes.

### References
- [epics.md](../planning-artifacts/epics.md) — Epic 6 goal (lines 843–847, FR13 + FR35); **Story 6.9 ACs (lines 1126–1138, source of truth)** and its seating comment (1113–1120); the Stories 6.8–6.12 constraints (1065–1073); Story 6.10 (1140–1163, reveal-source — OUT), Story 6.11 (1165–1190, watch hardening — OUT), Story 16.5 (Marketplace — icon/metadata OUT).
- [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md) — **primary source:** §2 constraints (esp. #5); R3.1 (tree view + `outline` export + `specscribe.status.*` colors, lines 60–64), R3.2 (status bar, 65), R1.5 (`viewsWelcome`, 46); §4 "Next" wave (110).
- [src/SpecScribe/Commands.cs](../../src/SpecScribe/Commands.cs) — `WebviewCommand` payload (64–70) to extend with `outline`; [SiteGenerator.cs:768–826](../../src/SpecScribe/SiteGenerator.cs) — `RenderWebviewSurfaces` (the loop that owns surface keys + models); [WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs) — add `Outline`; [EpicsModel.cs](../../src/SpecScribe/EpicsModel.cs) — `EpicInfo`/`StoryInfo` fields; [StatusStyles.cs](../../src/SpecScribe/StatusStyles.cs) — `ForStory`/`ForEpicWithRetrospective`/`StoryStages`/`EpicStages`; [BmadCommands.cs:214–245](../../src/SpecScribe/BmadCommands.cs) — per-story helper command; [WebviewHelpers.cs](../../src/SpecScribe/WebviewHelpers.cs) — the existing clipboard-helper pattern.
- [src/SpecScribe/assets/specscribe-webview-theme.css](../../src/SpecScribe/assets/specscribe-webview-theme.css) (lines 109–168) — the 6.5 six-stage accent tuning to mirror into `contributes.colors`.
- [extension/src/extension.ts](../../extension/src/extension.ts) — the shim (singleton, `openStatus`, `push`, `runRenderer`, `createPanel`, `composeEntryHtml`, watchers) as it stands **after 6.8**; [extension/package.json](../../extension/package.json) — manifest; [extension/README.md](../../extension/README.md) — F5 checklist to extend.
- Prior stories: [6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md), [6.5](6-5-host-aware-theming-and-explicit-helper-actions.md), **[6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) (prerequisite)**. [deferred-work.md](deferred-work.md) — lines 8 (stale `repoRoot` claim), 15 (scoped re-render, not here), 20 (nav a11y, out), 21 (scratch collision, not here).
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) (thin-shim seam, JSON-export-for-non-webview-consumer clause), [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md) (directory-scoped settings + read-only helpers), [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) (re-affirms C# path).
- VS Code API: [Tree View guide](https://code.visualstudio.com/api/extension-guides/tree-view) (`viewsContainers`/`views`/`TreeDataProvider`/`viewsWelcome`), [Theme Color reference](https://code.visualstudio.com/api/references/theme-color) + `contributes.colors` (contributed color IDs for `ThemeColor`), [Status Bar](https://code.visualstudio.com/api/extension-capabilities/extending-workbench#status-bar-item).
- Memory: [specscribe-status-token-system], [story-6-2-section-view-models-live] (epic-status surfaces on `ForEpicWithRetrospective`), [story-6-4-webview-runtime-live], [story-6-5-webview-theming-live], [epic-6-sequencing-and-6-5-theming], [golden-diff-normalization-gotchas], [worktree-edits-must-target-worktree-path], [generate-output-dir-is-specscribeoutput].

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-11 — Story drafted (create-story) from the VS Code Native-Integration Recommendations (FR35, [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md)) "Next" wave. Seats R3.1 (activity-bar TreeView: epics→stories with stage status, via a new core `outline` export — the ADR 0005 §1 non-webview-consumer clause), R3.2 (status-bar summary item + stale indicator), R1.5 (`viewsWelcome` empty state). **GATED on Story 6.8** (`done` required): consumes 6.8's `specscribe.projectDetected` context key, parametrized direct-open path, and shared tool-resolution helper; 6.4/6.5 already done. First Epic 6 surface to exercise constraint #5 — status icons derive from the six core-emitted `--status-*` stages via contributed `specscribe.status.*` theme colors (mirroring the 6.5 accent tuning), never VS Code's 3-severity palette. Core change is an additive `outline` field on the `webview` payload (Option A, over a separate `specscribe outline` command); the load-bearing shim change is promoting payload acquisition from panel-scoped to a shared provider driving tree + status bar + panel. Read-only end to end (reveal / open-source `showTextDocument` / copy-prompt clipboard); HTML surface byte-identical. Explicitly OUT: webview reveal-source (6.10), watch-gap/multi-root/core-derived roots (6.11), diagnostics (6.12), Marketplace metadata/walkthrough (16.5), new settings, scoped re-render. Status → ready-for-dev.
