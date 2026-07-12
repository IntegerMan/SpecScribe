---
baseline_commit: b8d2a5e16453c07989a719d2aa74c218281ab1a5
seated_by: SCP 2026-07-11 (correct-course) — FR35, VS Code Native-Integration Recommendations
---

# Story 6.10: Editor ↔ Artifact Bridges (Reveal-Source)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a VS Code user,
I want to jump from a surface in the webview straight to the artifact that produced it,
so that the portal and my files feel like one thing rather than two disconnected views.

## ⚠️ GATED — build order in the Epic 6 "Next" wave

This story **extends the shim as it stands after Stories [6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) and [6.9](6-9-native-project-outline-tree-view-and-status-bar.md) merge**, plus the webview machinery from [6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md)/[6.5](6-5-host-aware-theming-and-explicit-helper-actions.md). Build order within the "Next" wave is **6.9 → 6.10 → 6.11** ([docs/VSCodeIntegrationRecommendations.md §4](../../docs/VSCodeIntegrationRecommendations.md), lines 108–110). **Prerequisites 6.4/6.5 are `done`.** At create-story time **6.8 is `review`** and **6.9 is `ready-for-dev`**.

- **Hard dependency — 6.8 (`done` required):** this story edits the same `extension.ts` message loop, `resolveTool`, and `specscribe.projectDetected` posture that 6.8 reshapes. Re-anchor every line number below against the **actual post-6.8 file** (they are cited against the current `main` `b8d2a5e`, which already carries 6.8's shim changes — `openDashboard`/`openEpics`/`refresh`/`stageTerminalCommand`/`resolveTool` are present).
- **Build after 6.9 (strongly recommended):** 6.9 and 6.10 both touch `WebviewSurface`, the `webview` payload's `surfaces` serialization, and the `WebviewPayload` TS interface. Building 6.10 **after** 6.9 avoids a merge collision on those shared seams and lets 6.10 **harmonize the source-path convention** (see Design decision "One source-path convention"). If 6.9 has slipped when this story is scheduled, coordinate rather than reimplementing 6.9's `WebviewSurface`/payload changes here.
- **Not a hard dependency on 6.9's tree/shared-provider:** reveal-source works entirely inside the open panel's existing message loop and per-surface cache. This story needs **no** tree view, **no** status bar, and **no** shared payload provider. It reuses 6.9's `showTextDocument` primitive concept but through the **webview** entry point, which 6.9 explicitly left out of scope (6.9 OUT-of-scope: "Reveal-source *from the webview* … **Story 6.10**").

## What this story is (and isn't)

This is the **editor ↔ artifact bridge**: a webview affordance ("Open source") that opens the markdown file a surface was rendered from, via `vscode.window.showTextDocument` — read-only navigation, nothing written. R4.1 calls it "the biggest 'the portal and my files are one thing' moment available" ([recommendations R4.1](../../docs/VSCodeIntegrationRecommendations.md), line 71).

Two deliverables:

1. **AC #1 — reveal-source (fully built):** the `webview` payload carries a **source-artifact path per surface**; a webview control posts a `revealSource` host message; the shim opens that `.md` read-only. The path is **core-resolved repo-relative** (like the existing `configuredOutputRoot` datum), so the host joins the workspace folder and makes **no duplicated path assumption**.
2. **AC #2 — the structured-link seam (guaranteed to exist, not fully populated):** make the `revealSource` protocol **line-capable** and structure the bridge's link dispatch so **Epic 7 code citations (R4.2 → Stories 7.1/7.2)** and the **Story 8.4 next-step command (R4.3)** plug into it by emitting structured data attributes — **without re-architecting the bridge**. The owning stories emit their links; this story only guarantees the host-side seam is there for them to ride. Per the 7.1 seating note: *"the webview re-targeting itself rides Story 6.10's link seam"* ([epics.md:1229](../planning-artifacts/epics.md)).

### The five Epic 6 native-integration constraints ([§2](../../docs/VSCodeIntegrationRecommendations.md))

1. **Thin shim, no rendering brain** (AD-1/AD-2). The shim posts a message and calls `showTextDocument` — it parses nothing, derives no path structure, holds no project knowledge. The **source path is computed in C#** and carried in the payload; the shim only joins it to the workspace folder (exactly as it already does for `configuredOutputRoot`).
2. **Read-only end to end** (AD-6, ADR 0003, FR-17, NFR-5). `showTextDocument` **opens an editor; it never writes**. No `applyEdit`, no `fs.write*`, no settings `.update`. Opening a file for reading is the entire new capability.
3. **VS Code settings carry HOST concerns only** (ADR 0003). This story adds **no** new setting.
4. **The generated HTML surface stays byte-identical.** The reveal control is **webview-only chrome** in the `WebviewRenderAdapter` document shell (the `.ss-webview-toolbar`, alongside 6.5's copy-prompt button) — it is **never** in the shared body/nav/breadcrumb the HTML surface emits. The golden `GoldenContentFingerprint` is unaffected by construction (memory: [golden-diff-normalization-gotchas]). Re-run it anyway (Task 6).
5. **Status stages, not host severities.** Not exercised by this story (no status iconography here); the constraint stands but there is nothing to route.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 6.10 (lines 1151–1163)._

1. **Given** the webview payload carries source-artifact paths on its surface/section metadata
   **When** I trigger "reveal source" on a surface or section in the webview
   **Then** a `revealSource` host message opens that markdown file via `showTextDocument` (read-only navigation, no mutation)
   **And** the path resolution reuses the core-resolved roots (no duplicated path assumptions).

2. **Given** future code-citation (Epic 7) and next-step-command (Story 8.4) surfaces
   **When** those surfaces emit links
   **Then** the core emits them with structured data attributes (e.g. `data-code-path`/`data-line`, or command text) so the VS Code host can re-target them natively (editor at a line; command staged in a terminal), while the HTML surface keeps its portal/GitHub links
   **And** the re-targeting behavior itself is implemented in the owning stories (7.1/7.2, 8.4), this story only guarantees the seam exists.

### AC → Recommendation → Surface map

| AC | Recommendation | Where it lands |
|---|---|---|
| #1 | R4.1 (reveal-source: source path on surface metadata → `revealSource` → `showTextDocument`) | Core: `WebviewSurface.SourcePath` + `RenderWebviewSurfaces`/`WebviewSurfaceFor` compute it; `WrapDocument` `__SOURCE__` for the entry; `sourcePath` on the `webview` payload ([Commands.cs:75](../../src/SpecScribe/Commands.cs)); the "Open source" button + bridge in [WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs). Shim: `revealSource` handler (`showTextDocument`), `source` on the push message, the `SurfaceContent.sourcePath` interface field. |
| #2 | R4.2 (code citations → editor at a line; recorded now, ridden by 7.1/7.2), R4.3 (next-step command → terminal handoff; recorded now, ridden by 8.4) | Protocol: `revealSource` carries an optional `line`; the bridge's link dispatch recognizes `data-code-path`/`data-line` and re-targets to `revealSource{path,line}` — the host side 7.2 emits into. The command-staging extension point is **documented** (message shape + the 6.8 terminal primitive to reuse); its handler + link emission belong to **Story 8.4**. |

## Critical current-state facts the dev MUST internalize

Read [extension/src/extension.ts](../../extension/src/extension.ts), [src/SpecScribe/WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs), [src/SpecScribe/WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs), and [src/SpecScribe/Commands.cs](../../src/SpecScribe/Commands.cs) **as they stand after 6.8/6.9 merge** (line numbers below are against `main` `b8d2a5e` = post-6.8, pre-6.9; 6.9 will have added an `outline` field to the same payload/interface — re-anchor).

1. **The webview↔host message protocol is small and lives in two files.** Webview→host messages today: `copyHelperText`, `navigate`, `openExternal`, `ready` (bridge script, [WebviewRenderAdapter.cs:140–172,190](../../src/SpecScribe/WebviewRenderAdapter.cs); handled at [extension.ts:175–205](../../extension/src/extension.ts)). Host→webview: one `update` message ([extension.ts:157](../../extension/src/extension.ts); consumed at [WebviewRenderAdapter.cs:177–188](../../src/SpecScribe/WebviewRenderAdapter.cs)). **`revealSource` is a NEW webview→host message** added to both — one new `if` branch in `onDidReceiveMessage`, one new click branch in the bridge. Nothing else in the protocol changes.
2. **The webview-only toolbar already exists and is the correct home for the control.** `.ss-webview-toolbar` in `DocumentTemplate` ([WebviewRenderAdapter.cs:104–107](../../src/SpecScribe/WebviewRenderAdapter.cs)) already carries the 6.5 "Copy code-review prompt" `<button>`. Add the "Open source" `<button>` right beside it — a `<button>`, **not** an `<a>` (see fact #7). The toolbar is part of the shell (`WrapDocument`), **not** the swappable `RenderContent` region, so it never touches the HTML surface and never trips parity (fact #6).
3. **The surface container carries `data-path`; add `data-source`.** `#specscribe-surface[data-path="__PATH__"]` ([WebviewRenderAdapter.cs:108](../../src/SpecScribe/WebviewRenderAdapter.cs)) is the current surface's output-relative path, updated on every `update` swap ([WebviewRenderAdapter.cs:181](../../src/SpecScribe/WebviewRenderAdapter.cs)). Mirror it: add `data-source="__SOURCE__"`, update it on each swap from the push message's new `source` field, and toggle the toolbar button's visibility on it (a surface with no source — the dashboard — hides the button).
4. **The payload's `surfaces` are `{path: {title, content}}` today.** Serialized at [Commands.cs:75](../../src/SpecScribe/Commands.cs) from `WebviewBundle.Surfaces` (each a `WebviewSurface(OutputRelativePath, Title, ContentHtml)` — [WebviewBundle.cs:7](../../src/SpecScribe/WebviewBundle.cs)). Add `SourcePath` to the record and `sourcePath` to the serialized object; add `sourcePath?` to the TS `SurfaceContent` interface ([extension.ts:24–27](../../extension/src/extension.ts)).
5. **`RenderWebviewSurfaces` is where each surface's source is known.** [SiteGenerator.cs:769–828](../../src/SpecScribe/SiteGenerator.cs) iterates dashboard → epics-index → each epic → each story/placeholder, calling `WebviewSurfaceFor(page, …)` ([SiteGenerator.cs:832–838](../../src/SpecScribe/SiteGenerator.cs)). For a **story** surface the loop already holds `artifactFullPath` = the story `.md`'s **absolute** path (from `_storyArtifactsById[story.Id]` — [SiteGenerator.cs:803,811](../../src/SpecScribe/SiteGenerator.cs)). For **epic/index/placeholder** surfaces the source is `epics.md` (fact #8). The **dashboard** has no single source (null). Compute the repo-relative `SourcePath` here and pass it to `WebviewSurfaceFor`.
6. **The parity harness never inspects the toolbar.** `RenderParity` recovers facts only from `<nav class="site-nav">`, `<div class="breadcrumb">`, stat/chip/story-card/now-next/progress/quick-link/index-card markup ([RenderParity.cs:102–112,308–349](../../src/SpecScribe/RenderParity.cs)). A `<button>` in `.ss-webview-toolbar` matches **none** of these. 6.5 added its helper `<button>` to the same toolbar and needed **no** `HostRenderException` — the registry still holds exactly 3 webview + 1 spa entries ([HostRenderException.cs:23–52](../../src/SpecScribe/HostRenderException.cs)). **This story adds no registry entry** (verify parity stays green in Task 6). Use a `<button>`, not an `<a>`: `AnyAnchorHref`/`Anchor` ([RenderParity.cs:106,112](../../src/SpecScribe/RenderParity.cs)) scan every `<a href>`, and an `<a>` whose href happened to equal a drill child could perturb `ExtractChildDrillTargets`.
7. **`configuredOutputRoot` is the exact precedent for a core-resolved, host-joined path.** [Commands.cs:74,85–86](../../src/SpecScribe/Commands.cs): `ResolveConfiguredOutputRoot` = `Path.GetRelativePath(resolved.RepoRoot, resolved.OutputRoot).Replace('\\','/')` — "pure and side-effect-free so it is unit-testable without a spawn", and the shim joins it to `workspaceFolders[0]` ([extension.ts:299–300](../../extension/src/extension.ts)). **Do the source path the same way:** `Path.GetRelativePath(_options.RepoRoot, artifactAbsolute).Replace('\\','/')`, host joins `folder.uri.fsPath`. Same convention, same testability, **no new path assumption** (AC #1). This is why the source path is **repo-relative**, not `_bmad-output`-relative.
8. **`epics.md`'s absolute path is resolvable but not cached for the webview loop.** `RenderEpicsPages(epicsFullPath, …)` has it and derives `epicsSourceRelative = ToSourceRelative(epicsFullPath)` ([SiteGenerator.cs:630,640](../../src/SpecScribe/SiteGenerator.cs)), and it already caches `_storyArtifactsById`/`_referenceMap` there for the webview path ([SiteGenerator.cs:647–652](../../src/SpecScribe/SiteGenerator.cs)). **Cache the epics.md source the same way** (a `_epicsSourcePath` field set to the repo-relative epics.md there) so `RenderWebviewSurfaces` can map epic/index/placeholder surfaces to it. `ToSourceRelative` is **source-root**-relative; for the **repo**-relative form use `Path.GetRelativePath(_options.RepoRoot, epicsFullPath)` (fact #7).
9. **The bridge click handler is a single `document.addEventListener('click', …)` with `t.closest(...)` dispatch.** [WebviewRenderAdapter.cs:131–173](../../src/SpecScribe/WebviewRenderAdapter.cs) already branches: helper button → nav toggle → anchor. **Add branches, don't restructure:** (a) the toolbar reveal button → post `revealSource` with `#specscribe-surface`'s `data-source`; (b) in the anchor branch, **before** treating a click as navigate/openExternal, check `a.getAttribute('data-code-path')` (+ `data-line`) → post `revealSource{path, line}`. Branch (b) is the AC #2 seam: inert until 7.2 emits those attributes, then it "just works".
10. **`showTextDocument` is already used read-only in the shim.** [extension.ts:336](../../extension/src/extension.ts) (`openProjectSettings`) opens `.specscribe` via `vscode.window.showTextDocument(vscode.Uri.file(settingsPath))`. Reuse the exact primitive for `revealSource`. For a line target (AC #2): `showTextDocument(uri, { selection: new vscode.Range(line-1, 0, line-1, 0) })` (0-based; a citation's `data-line` is 1-based — convert).
11. **The workspace root / spawn cwd is in scope in the message handler.** `createController` closes over `cwd = folder.uri.fsPath` ([extension.ts:137](../../extension/src/extension.ts)); `onDidReceiveMessage` is registered inside it ([extension.ts:175](../../extension/src/extension.ts)). So `revealSource` resolves `path.join(cwd, msg.path)` with `cwd` already available — no new plumbing. (The subdir-open case where repo root ≠ workspace folder is the same limitation the watchers have today; it is **R6.2 → Story 6.11**, OUT of scope here — do not solve it, but do not regress it. See Dev Notes "The subdir-open caveat".)
12. **`engines.vscode` is `^1.90.0`** ([extension/package.json:9–11](../../extension/package.json)). `showTextDocument(uri, options)`, `TextDocumentShowOptions.selection`, and `Range` are all available at 1.90. **No engine bump.**
13. **No extension TS test harness exists** (scripts are `build`/`watch`/`typecheck`/`package`). Automated gates are `tsc --noEmit` + esbuild + JSON validity + the C# suite (for the payload `sourcePath` + its computation). Functional verification of reveal-source is the **F5 manual smoke** (extend [extension/README.md](../../extension/README.md)). Be honest about the coverage boundary in Completion Notes, as 6.4/6.5/6.8 did.

## Design decisions captured at create-story

### Source-path per surface (AC #1) — repo-relative, computed in `RenderWebviewSurfaces`

Add `string? SourcePath` to `WebviewSurface`. Compute per surface family in the existing loop (fact #5), always **repo-relative + forward-slashed** (fact #7):

| Surface | `SourcePath` |
|---|---|
| **Story (drafted)** | `Path.GetRelativePath(_options.RepoRoot, artifactFullPath).Replace('\\','/')` — the story `.md` (absolute path already in hand at [SiteGenerator.cs:811](../../src/SpecScribe/SiteGenerator.cs)). **The primary win.** |
| **Story placeholder** (undrafted) | `_epicsSourcePath` (epics.md) — the placeholder's source *is* the epic file. |
| **Epic page** | `_epicsSourcePath` (epics.md). |
| **Epics index** | `_epicsSourcePath` (epics.md). |
| **Dashboard** | `null` — no single source artifact (it aggregates many). The toolbar button is hidden on it. |

A single small helper keeps this uniform, e.g. `RepoRelative(string absolutePath) => PathUtil.NormalizeSlashes(Path.GetRelativePath(_options.RepoRoot, absolutePath))`. Emit `null` (not `""`) when there is no source, so the payload distinguishes "no source" from a computed value; serialize it as `sourcePath` (JSON omits/serializes null — the shim treats absent/empty/null identically: no button).

### The reveal control lives in the shell toolbar, not the body (AC #1, constraint #4 + fact #6)

Put an **"Open source"** `<button class="ss-reveal-src-btn">` in `.ss-webview-toolbar` beside the copy-prompt button. It carries no path itself; on click the bridge reads `#specscribe-surface[data-source]` (the current surface's source, updated per swap). Rationale:

- **Byte-identity + parity safety:** the toolbar is webview-only shell chrome (`WrapDocument`), never in the shared body — the HTML surface and the parity harness never see it (facts #2, #6).
- **One control, all surfaces:** a single toolbar button that reflects the current surface beats injecting a per-surface affordance into every content region (which would touch `RenderContent` and risk the section-fact harness).
- **Visibility:** hide the button when `data-source` is empty/absent (dashboard). Toggle it in the bridge on first paint and on each `update`.

Wire it:
- `WrapDocument` gains an optional `string? sourcePath = null` param; add `.Replace("__SOURCE__", PathUtil.Html(sourcePath ?? ""))`; add `data-source="__SOURCE__"` to `#specscribe-surface` and the button to the toolbar. `RenderWebviewSurfaces` passes the **entry** surface's `SourcePath` (dashboard → null → `""`) when it builds `entryDocument` ([SiteGenerator.cs:825](../../src/SpecScribe/SiteGenerator.cs)).
- `push()` ([extension.ts:152–158](../../extension/src/extension.ts)) adds `source: surface.sourcePath ?? ''` to the `update` message.
- The bridge's `update` handler ([WebviewRenderAdapter.cs:177–188](../../src/SpecScribe/WebviewRenderAdapter.cs)) sets `data-source` and toggles the button (e.g. `revealBtn.hidden = !src`).

### `revealSource` message + host handler (AC #1, line-capable for AC #2)

- **Bridge → host:** `vscode.postMessage({ type: 'revealSource', path: <data-source or data-code-path>, line: <optional 1-based> })`.
- **Host:** in `onDidReceiveMessage`, a new branch:
  ```ts
  if (msg?.type === 'revealSource' && typeof msg.path === 'string' && msg.path) {
    const target = resolveWorkspacePath(cwd, msg.path);       // guarded join — see below
    if (!target) return;                                       // rejected traversal / absolute
    const options = typeof msg.line === 'number' && msg.line > 0
      ? { selection: new vscode.Range(msg.line - 1, 0, msg.line - 1, 0) }
      : undefined;
    void vscode.window.showTextDocument(vscode.Uri.file(target), options);
    return;
  }
  ```
- **Read-only:** `showTextDocument` opens; it never writes. No other host effect.
- **Path guard (defense-in-depth, 17.2 posture):** `resolveWorkspacePath` joins `cwd + msg.path`, resolves it, and returns it **only if it stays within `cwd`** (reject a `..`-escape or an absolute override). The path is core-emitted and trusted, but the shim must not become a "open any file on disk" primitive on a hostile payload — a few lines, and it documents the read-only-within-workspace contract. Prefer opening only when `fs.existsSync(target)` too, so a stale path shows nothing rather than an error editor.

### One source-path convention — harmonize with 6.9's tree "Open Source" (AC #1 "no duplicated path assumptions")

Story 6.9's tree context action opens the source `.md` via `path.join(workspaceRoot, '_bmad-output', sourcePath)` where its `OutlineStory.SourcePath = story.ArtifactSourcePath` (relative to `_bmad-output/`). That hard-codes `_bmad-output` in TS — exactly the "duplicated path assumption" AC #1 forbids. **This story's source path is repo-relative and host-joined to the workspace folder only** (fact #7), matching `configuredOutputRoot`.

**Action:** if 6.9 has merged when this story runs, **align 6.9's tree open-source onto the same repo-relative convention** — a small, same-area change: make `OutlineStory.SourcePath` repo-relative (via the same `RepoRelative` helper) and drop the `_bmad-output` literal from the tree handler's `path.join`. One convention, one helper, no `_bmad-output` literal anywhere in TS. If 6.9 has **not** merged, leave a Dev Note pointing 6.9 at this convention and do not touch 6.9's code. Do **not** ship two conventions.

### AC #2 seam — line-capable reveal + documented extension points (guarantee it exists; don't populate it)

The AC is explicit: the re-targeting **behavior** (core-side link emission + its host recognition) is implemented in the **owning stories** (7.1/7.2 for code, 8.4 for commands); **this story only guarantees the seam exists**. Concretely, 6.10 delivers the seam and leaves the links to their owners:

- **Reveal/code seam (built here, ridden by 7.2):** `revealSource` carries an optional `line`; the host handler honors it (`showTextDocument` selection). The bridge's anchor branch recognizes `data-code-path`(+`data-line`) → `revealSource{path, line}`. 7.2 (per [epics.md:1251–1254](../planning-artifacts/epics.md), R4.2) emits code-citation links carrying those attributes for the webview while keeping the portal/GitHub `href` for the HTML surface — and "plugs into Story 6.10's reveal seam" with **no bridge change**. Including the recognition here (a few lines, directly F5-verifiable with a hand-inserted attribute) is what "rides 6.10's link seam" means ([epics.md:1229](../planning-artifacts/epics.md)); it is **inert** until 7.2 emits the attributes.
- **Command-staging seam (documented here, built in 8.4):** R4.3 (next-step command → **terminal handoff**) pairs a copy helper with "Open in Terminal": `createTerminal` + `sendText(command, /* execute: */ false)` — staged, user presses Enter (AD-6/ADR 0003). The shim **already has this exact primitive** in `stageTerminalCommand` ([extension.ts:314–323](../../extension/src/extension.ts)). **Document** the extension point: a future `stageCommand` webview→host message whose handler reuses that primitive, dispatched from a `data-ss-command` (or equivalent) attribute the 8.4 surface emits. **Do not build the handler or the emission here** — 8.4 owns the command surface and is the right home (R4.3: "an explicit AC in 8.4 or a small 6.x follow-on"). This story records the shape so 8.4 designs against it, not retrofits it.
- **What the HTML surface keeps:** unchanged — code citations keep their in-portal/GitHub `href` (Story 7.1/7.2), next-step commands keep their copy affordance (Story 8.4). The structured data attributes are **additive**, webview-intercepted, and never alter the static site (byte-identity holds).

## Scope

### IN scope
- **Core (`src/SpecScribe/`, single project, `net10.0`, `namespace SpecScribe;`, `[Story 6.10]`):** `WebviewSurface.SourcePath` (repo-relative, nullable); a `_epicsSourcePath` cache set in `RenderEpicsPages`; per-surface source computation in `RenderWebviewSurfaces`/`WebviewSurfaceFor`; the `RepoRelative` helper; `WrapDocument`'s `__SOURCE__` + `data-source` + the "Open source" toolbar button + the bridge additions (`revealSource` post on the button and on `data-code-path` anchors; `data-source` update + button toggle on swap); serialize `sourcePath` on the `webview` payload ([Commands.cs:75](../../src/SpecScribe/Commands.cs)); C# tests (payload carries `sourcePath`; story surface → its `.md` repo-relative; epic/index/placeholder → epics.md; dashboard → null; parity + golden unchanged).
- **Shim (`extension/src/extension.ts`):** `SurfaceContent.sourcePath?`; `source` on the `update`/push message; the `revealSource` handler (`showTextDocument`, read-only, line-capable, path-guarded); the workspace-path guard helper. No new command, no new setting, no manifest change (the button is in-webview, not a VS Code command).
- **Docs:** extend [extension/README.md](../../extension/README.md) F5 checklist (Open source button appears on a story/epic surface, hidden on the dashboard; clicking opens the correct `.md` read-only; a hand-inserted `data-code-path`/`data-line` anchor opens the file at the line). Note the automated-coverage boundary (no TS harness).

### OUT of scope (do NOT start here)
- **Code-citation link EMISSION (R4.2)** — **Stories 7.1/7.2.** This story adds the host-side recognition of `data-code-path`/`data-line`; 7.2 emits those attributes from the core. Do not build in-portal code pages or emit code links here.
- **Next-step-command terminal handoff EMISSION + handler (R4.3)** — **Story 8.4.** Document the `stageCommand` extension point; do not build its handler or emit its control.
- **Tree view / status bar / shared payload provider (R3.1/R3.2)** — **Story 6.9.** This story uses the existing panel-scoped cache and message loop; it neither adds nor requires the shared provider. (If 6.9 already landed it, reuse the panel's cache as-is; do not refactor.)
- **File-change reactivity hardening (R6.1/R6.2/R6.3), multi-root (R3.4)** — **Story 6.11.** Keep the existing watchers and `workspaceFolders[0]`/`cwd` resolution exactly. The subdir-open path-resolution caveat is 6.11's; note it, don't solve it.
- **New VS Code settings / commands / manifest contributions.** The reveal control is a webview button, not a `contributes.commands` entry (constraint #3). No `contributes.*` change.
- **Section-level reveal beyond surface-level.** AC #1 says "surface **or** section"; the surface-level reveal (one source per surface via the toolbar) fully satisfies it and is the R4.1 design. A per-section reveal (e.g. an AC block → its line in the `.md`) is a natural future extension of the same `revealSource{path,line}` seam — **do not build per-section anchors here** (that would require plumbing per-section source lines through `PageView`; no consumer yet).
- **New rendering/view models, markdown parsing in TS, package/namespace split, HTML-surface changes** (seed-level, forbidden). `SourcePath` is a datum on an existing record; the button is webview-only chrome.

## Tasks / Subtasks

- [x] **Task 0 — Baseline & payload reconnaissance** (prereq for AC #1)
  - [x] Confirm Story [6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) is `done` in [sprint-status.yaml](sprint-status.yaml); if not, **halt and flag** (this story extends 6.8's shim). Note whether [6.9](6-9-native-project-outline-tree-view-and-status-bar.md) has merged (it adds `outline` to the same payload/interface and a tree "Open Source" whose convention this story harmonizes — Design decision "One source-path convention").
  - [x] Re-capture `HEAD` for the byte-parity diff (the `main` auto-committer may sit ahead of `baseline_commit` `b8d2a5e` — memory: [worktree-edits-must-target-worktree-path]).
  - [x] `dotnet build src/SpecScribe`, run `specscribe webview` from the repo root, **capture the JSON**. Record the real payload shape (the `surfaces` map's `{title, content}` today, plus 6.9's `outline` if merged) and the exact **source `.md` path** a story surface should resolve to (e.g. `_bmad-output/implementation-artifacts/6-4-…md`), so the emitted `sourcePath` can be asserted against it.

- [x] **Task 1 — Core source-path plumbing** (AC: #1)
  - [x] Add `string? SourcePath` to `WebviewSurface` ([WebviewBundle.cs:7](../../src/SpecScribe/WebviewBundle.cs)), `[Story 6.10]`.
  - [x] Cache `_epicsSourcePath` (repo-relative epics.md via `Path.GetRelativePath(_options.RepoRoot, epicsFullPath)`) in `RenderEpicsPages` beside `_storyArtifactsById` ([SiteGenerator.cs:647–652](../../src/SpecScribe/SiteGenerator.cs)); add the field near [SiteGenerator.cs:758](../../src/SpecScribe/SiteGenerator.cs).
  - [x] Add a `RepoRelative(string absolutePath)` helper; compute each surface's `SourcePath` in `RenderWebviewSurfaces`/`WebviewSurfaceFor` per the Design-decision table (story `.md` / epics.md / null).
  - [x] Serialize `sourcePath` on the payload's surface objects ([Commands.cs:75](../../src/SpecScribe/Commands.cs)).

- [x] **Task 2 — Webview control + bridge** (AC: #1, seam for #2)
  - [x] `WrapDocument`: add `string? sourcePath = null` param, `__SOURCE__` replace (`PathUtil.Html(sourcePath ?? "")`), `data-source="__SOURCE__"` on `#specscribe-surface`, and the `<button class="ss-reveal-src-btn">Open source</button>` in `.ss-webview-toolbar`. `RenderWebviewSurfaces` passes the entry surface's `SourcePath` when building `entryDocument` ([SiteGenerator.cs:825](../../src/SpecScribe/SiteGenerator.cs)).
  - [x] Bridge: reveal button click → `postMessage({type:'revealSource', path: surface.getAttribute('data-source')})` (only when non-empty); `update` handler sets `data-source` from `m.source` and toggles the button (`hidden = !src`); anchor branch recognizes `data-code-path`(+`data-line`) → `revealSource{path, line}` **before** navigate/openExternal.
  - [x] Style the button to match `.ss-helper-btn` (reuse the existing toolbar button CSS; no new stylesheet — the theme bridge already themes the toolbar).

- [x] **Task 3 — Shim message handler** (AC: #1, line-capable for #2)
  - [x] `SurfaceContent.sourcePath?: string` ([extension.ts:24–27](../../extension/src/extension.ts)); include `source: surface.sourcePath ?? ''` in `push`'s `update` message ([extension.ts:157](../../extension/src/extension.ts)).
  - [x] Add the `revealSource` branch to `onDidReceiveMessage` ([extension.ts:175–205](../../extension/src/extension.ts)): guarded `resolveWorkspacePath(cwd, msg.path)` (reject `..`-escape/absolute; prefer `fs.existsSync`), then `showTextDocument(Uri.file(target), line? {selection})`. Read-only; no other effect.
  - [x] Convert a 1-based `data-line` to a 0-based `vscode.Range` for the selection.

- [x] **Task 4 — Harmonize the source-path convention** (AC: #1 "no duplicated path assumptions")
  - [x] If 6.9 merged: point its tree "Open Source" at the repo-relative `SourcePath` via the shared `RepoRelative` helper and remove the `_bmad-output` literal from its `path.join`. If not merged: add a Dev Note directing 6.9 to this convention. Never ship two conventions.

- [x] **Task 5 — Document the command-staging seam** (AC: #2)
  - [x] In Dev Notes / a code comment at the bridge dispatch, record the `stageCommand` extension point (message shape + reuse of `stageTerminalCommand`'s `createTerminal`+`sendText(cmd,false)` primitive) so Story 8.4 emits its control against a known seam. Do **not** build the handler.

- [x] **Task 6 — Verify, guard, document** (AC: all)
  - [x] `cd extension && npm install && npm run typecheck && npm run build` — TS compiles, esbuild bundles clean, `package.json` valid.
  - [x] `dotnet test` whole suite green **including `GoldenContentFingerprint`** ([SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)) — `sourcePath` is a payload field, not part of the generated site, so golden stays green (memory: [golden-diff-normalization-gotchas]). Confirm `RenderParity`/`RenderSectionParity` tests pass with **no** new `HostRenderException` (the button is unscanned toolbar chrome — fact #6). Add C# assertions: payload surface carries `sourcePath`; a story surface's `sourcePath` equals its `.md` repo-relative; epic/index/placeholder → epics.md repo-relative; dashboard → null; forward-slashed on Windows.
  - [x] Confirm read-only end to end: grep the diff for any write API (`writeFile`, `fs.write*`, `applyEdit`, settings `.update(`, `SettingsStore`) — **none**. `showTextDocument` (open) + the existing clipboard are the only host effects.
  - [x] Extend [extension/README.md](../../extension/README.md) F5 checklist (button appears on story/epic surfaces, hidden on dashboard; opens the correct `.md` read-only; a hand-inserted `data-code-path`/`data-line` anchor opens the file at the line). Be explicit in Completion Notes about the automated-coverage boundary (no TS harness).

### Review Findings

- [x] [Review][Patch] `revealSource` fails completely silently, unlike `openSource` which reports the identical failure [extension/src/extension.ts:330] — fixed: mirrors `openSource`'s `showErrorMessage` on a rejected/missing path
- [x] [Review][Patch] `showTextDocument` in the `revealSource` handler has no try/catch, unlike `openSource`'s identical call [extension/src/extension.ts:335] — fixed: wrapped in try/catch with the same error-message pattern
- [x] [Review][Patch] `resolveWorkspacePath`'s containment check doesn't resolve symlinks, so a workspace-local symlink can escape the workspace [extension/src/extension.ts:664] — fixed: containment now compares `fs.realpathSync`-resolved paths
- [x] [Review][Patch] `resolveWorkspacePath` never checks the target is a file, so a directory path resolves and is opened [extension/src/extension.ts:664] — fixed: added an `fs.statSync(...).isFile()` check
- [x] [Review][Patch] Containment check is a case-sensitive string comparison on a case-insensitive filesystem (Windows) [extension/src/extension.ts:668] — fixed: case-folds both sides on `process.platform === 'win32'`
- [x] [Review][Patch] `EntryDocument_CarriesTheHiddenRevealButton_AndEmptyDataSource` never asserts the `hidden` attribute is present [tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs:287] — fixed: added an assertion on the exact button markup including `hidden`
- [x] [Review][Defer] `RepoRelative` silently returns an absolute path (not repo-relative) when the artifact isn't under `RepoRoot` [src/SpecScribe/SiteGenerator.cs:226] — deferred, pre-existing convention (`ToSourceRelative`/`configuredOutputRoot`) never validated this either; low likelihood since paths derive from the repo scan itself

## Dev Notes

### The whole story is a datum + one message — keep the shim brainless
The source path is **computed in C#** (repo-relative, like `configuredOutputRoot`) and carried in the payload; the shim joins it to the workspace folder and calls `showTextDocument`. If you find yourself deriving a path *structure* in TypeScript — a `_bmad-output` literal, a repo-root guess, a source-vs-output mapping — you've crossed AD-1/AD-2; push it into the core datum. The one TS "logic" allowed is the workspace-containment guard (a security check, not project knowledge).

### Read-only is the spine invariant (AD-6 / ADR 0003 / FR-17 / NFR-5)
`showTextDocument` **opens** a file; it never writes. This story adds exactly one new capability — open a `.md` (optionally at a line) — and it is read-only by construction. Prove write-freedom in Task 6. The command-staging seam you document for 8.4 is likewise staged-not-executed (`sendText(cmd, false)`): SpecScribe never presses Enter.

### Byte-parity + parity harness: safe by placement
The reveal control is a `<button>` in the webview-only `.ss-webview-toolbar` shell — never in the shared body/nav/breadcrumb, never seen by the HTML surface or `RenderParity` (facts #2, #6). 6.5's helper button set the precedent and needed no registry entry; neither does this. Still run the golden + parity tests (Task 6). Never add a control (or any style) to the shared `RenderContent`/base stylesheet as a side effect.

### The subdir-open caveat (do not solve; do not regress)
The host resolves `path.join(cwd, sourcePath)` where `cwd = folder.uri.fsPath` and the core computed `sourcePath` relative to the resolved `RepoRoot`. When VS Code is opened **at the repo root** (the common case) these coincide and reveal is correct. When opened on a **subdirectory**, `RepoRoot` is an ancestor of `cwd` and the join is wrong — the **same** limitation the live-push watchers have today ([extension.ts:242](../../extension/src/extension.ts) anchors to the folder, not the resolved root). That is **R6.2 → Story 6.11** ("watched/resolved paths derived from the core-resolved roots carried in the payload"). Do not add a `repoRoot`-in-payload field here (deferred-work.md:8's claim that 6.4 added one is **stale** — the committed payload has none; fact #7 of Story 6.9). Note the caveat in Completion Notes; 6.11 fixes it uniformly for watchers and reveal together.

### AC #2 is a seam, not a feature — resist building the owners' work
The temptation is to "finish" code citations or terminal commands here. Don't. The AC is explicit that the re-targeting **behavior** is 7.1/7.2's and 8.4's; 6.10 guarantees the **host-side seam** so they ride it (7.1's seating note: "the webview re-targeting itself rides Story 6.10's link seam"). Ship exactly: line-capable `revealSource`, the `data-code-path`/`data-line` recognition (inert until 7.2 emits), and a documented `stageCommand` extension point for 8.4. No in-portal code pages, no command emission, no terminal handler.

### Harmonize, don't fork, the source-path convention
6.9's tree "Open Source" and this story's webview reveal-source both open a source `.md` via `showTextDocument`. They must use **one** convention: repo-relative from the core, host-joined to the workspace folder — no `_bmad-output` literal in TS (AC #1). If 6.9 landed first with the `_bmad-output`-join, align it here via the shared `RepoRelative` helper (Task 4). Two conventions for the same operation is the exact "duplicated path assumption" the AC forbids.

### Previous-story intelligence (6.4 / 6.5 / 6.8 / 6.9)
- **6.4** established the shim, the `specscribe webview` JSON seam, the message protocol (`navigate`/`openExternal`/`update`/`ready`), the in-flight `load()` coalescer, and the disposed/UTF-8 guards. **Do not regress** them ([extension.ts:145,147–150,225,427–428](../../extension/src/extension.ts)).
- **6.5** added the `.ss-webview-toolbar` + the `copyHelperText` clipboard handoff (whose toolbar this story extends), the per-call random sentinel in `composeEntryHtml` (keep it random), and the clipboard try/catch. The "Open source" button sits **beside** the copy-prompt button and follows its read-only handoff shape (post a message; the host does the rest).
- **6.8** (must be `done`) reshaped the shim: `projectDetected`, direct-open commands, `resolveTool`, `stageTerminalCommand` (the terminal primitive the AC #2 command seam will reuse), cold-start progress, actionable errors, the panel-tab icon. Re-anchor line numbers against the post-6.8 file.
- **6.9** (build before this) adds an `outline` field to the same payload/interface and a tree "Open Source" — **harmonize its path convention** (Task 4). It also confirms deferred-work.md:8's `repoRoot`-in-payload claim is stale (there is none).

### Non-negotiable invariants (architecture spine)
- **AD-1/AD-2** — thin shim, no rendering brain, no project knowledge; the source path is a core-emitted datum, the reveal is pure host-delivery.
- **AD-6 / FR-17 / NFR-5** — read-only end to end; `showTextDocument` opens, never writes; staged commands never execute.
- **ADR 0003** — directory-scoped `.specscribe` is the source of truth for project behavior; this story adds no VS Code settings.
- **ADR 0005** — C# decides what to say (renders the webview HTML, computes the source path); the shim substitutes host runtime values and relays messages. Carrying a resolved path datum in the payload is the "JSON export … a core-resolved datum, not rendering" clause (§1), exactly like `configuredOutputRoot`.

### Risk centers (where review will focus)
1. **Read-only proof** — any `applyEdit`/`fs.write`/settings `.update` breaks AD-6. `showTextDocument` must be the only new host effect.
2. **Path convention duplication** — a `_bmad-output` (or any path-structure) literal in TS violates AC #1. Repo-relative from core + workspace-folder join is the only allowed shape; harmonize 6.9.
3. **Byte-identity + parity** — the control must be webview-only toolbar chrome (a `<button>`), never in the shared body; golden + parity stay green with no new `HostRenderException`.
4. **Path-guard / traversal** — the shim must not become an "open any file" primitive; contain the resolved path within the workspace (defense-in-depth).
5. **Scope creep into 7.x/8.4** — no code-link or command emission; only the line-capable seam + the inert `data-code-path` recognition + the documented `stageCommand` point.
6. **Subdir-open regression** — resolve exactly as the watchers do today (`cwd`-joined); don't half-solve the repo-root case (that's 6.11) and don't regress the common case.
7. **Merge coordination with 6.9** — shared `WebviewSurface`/payload/interface edits; build after 6.9 and reconcile the `outline` + `sourcePath` fields cleanly.

### Project Structure Notes
- **Extension lives in `extension/`** — self-contained, NOT part of the .NET solution or the generated-site pipeline. TS in [extension/src/extension.ts](../../extension/src/extension.ts); esbuild bundles `src/extension.ts` → `dist/extension.js`.
- **Core changes** go in the single `src/SpecScribe/` project (`net10.0`, `Nullable enable`), `namespace SpecScribe;`, tagged `[Story 6.10]`, matching the heavy XML-doc style. **No new project, no namespace split** (seed-level, [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- **This session/story runs on `main`** (not a worktree) — edits target `C:\Dev\SpecScribe` directly; a background auto-committer runs on `main` (memory: [worktree-edits-must-target-worktree-path]). Keep commits coherent.
- Output dir is `SpecScribeOutput` (memory: [generate-output-dir-is-specscribeoutput]); never `--output docs/live`.
- **No extension TS test harness exists** — automated gates are `tsc --noEmit` + esbuild + JSON validity + the C# suite (for the payload `sourcePath` + computation). Reveal-source verification is the F5 manual smoke; be honest about it in Completion Notes.

### References
- [epics.md](../planning-artifacts/epics.md) — Epic 6 goal (lines 843–847, FR13 + FR35); **Story 6.10 ACs (lines 1151–1163, source of truth)** and its seating comment (1140–1145); Stories 6.8–6.12 constraints (1065–1073); Story 6.9 (1113–1138, tree "Open Source" — harmonize), Story 6.11 (1165–1190, watch hardening / subdir roots — OUT), Story 7.1 (1223–1229, R4.2 seam "rides 6.10's link seam"), Story 7.2 (1251–1254, R4.2 link resolution — emits `data-code-path`/`data-line`), Story 8.4 (1441–1445, R4.3 terminal handoff — OUT, documented seam).
- [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md) — **primary source:** §2 constraints, R4.1 (reveal-source, line 71), R4.2 (code citations → editor at a line, 72), R4.3 (next-step command → terminal handoff, 73), R4.4 (what NOT to build: content-aware CodeLens, 74); §4 "Next" wave order (110–111).
- [src/SpecScribe/WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs) — `DocumentTemplate` (toolbar 104–107, `#specscribe-surface` 108, bridge click 131–173, `update` handler 177–188), `WrapDocument` (76–87), `RenderContent` (62–69); [WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs) — `WebviewSurface` to extend; [SiteGenerator.cs:769–838](../../src/SpecScribe/SiteGenerator.cs) — `RenderWebviewSurfaces`/`WebviewSurfaceFor` (source computed here) + [:640,647–652,758](../../src/SpecScribe/SiteGenerator.cs) (epics.md path + the webview caches); [Commands.cs:64–86](../../src/SpecScribe/Commands.cs) — payload + `ResolveConfiguredOutputRoot` precedent; [WebviewHelpers.cs](../../src/SpecScribe/WebviewHelpers.cs) — the read-only helper pattern; [RenderParity.cs](../../src/SpecScribe/RenderParity.cs) + [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs) — parity scope (no new exception needed).
- [extension/src/extension.ts](../../extension/src/extension.ts) — the shim (`SurfaceContent`/`WebviewPayload` 24–40, `push` 152–158, `onDidReceiveMessage` 175–205, `showTextDocument` precedent 336, `stageTerminalCommand` primitive 314–323, workspace-folder join 299–300) as it stands **after 6.8/6.9**; [extension/package.json](../../extension/package.json) — `engines.vscode ^1.90.0` (no change); [extension/README.md](../../extension/README.md) — F5 checklist to extend.
- Prior stories: [6.4](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md), [6.5](6-5-host-aware-theming-and-explicit-helper-actions.md), **[6.8](6-8-extension-discoverability-workspace-trust-and-command-surface.md) (prerequisite)**, [6.9](6-9-native-project-outline-tree-view-and-status-bar.md) (build before; harmonize path convention). [deferred-work.md](deferred-work.md) — line 8 (stale `repoRoot`-in-payload claim), line 15 (scoped re-render, not here).
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) (thin-shim seam, core-resolved datum in payload), [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md) (read-only helpers, staged commands), [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) (re-affirms C# render path).
- VS Code API: [showTextDocument](https://code.visualstudio.com/api/references/vscode-api#window.showTextDocument) + [TextDocumentShowOptions.selection](https://code.visualstudio.com/api/references/vscode-api#TextDocumentShowOptions) (open at a line), [Webview messaging](https://code.visualstudio.com/api/extension-guides/webview#passing-messages-from-a-webview-to-an-extension).
- Memory: [story-6-4-webview-runtime-live], [story-6-5-webview-theming-live], [epic-6-sequencing-and-6-5-theming], [golden-diff-normalization-gotchas], [worktree-edits-must-target-worktree-path], [generate-output-dir-is-specscribeoutput], [epic-7-code-link-strategy] (7.1/7.2 code-link `#L{n}` + external base URL this seam serves).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Amelia / bmad-dev-story workflow), 2026-07-11.

### Debug Log References

- `dotnet build src/SpecScribe` → clean (0 warnings/errors).
- `dotnet run --project src/SpecScribe -- webview` reconnaissance (Task 0) BEFORE and AFTER: confirmed the payload's `surfaces` grew a `sourcePath` per surface (story → `_bmad-output/implementation-artifacts/<n-m-…>.md`; epic/index/placeholder → `_bmad-output/planning-artifacts/epics.md`; dashboard → `null`), the outline story `sourcePath` harmonized to repo-relative, and the entry document carries the hidden `ss-reveal-src-btn` + `data-source=""`.
- `dotnet test` → **762 passed, 0 failed** (was 761 pre-change; +4 new webview/serialization tests, −1 obsolete via the updated 6.9 outline assertion). Includes `GoldenContentFingerprint` (byte-identity held — `sourcePath` is a payload datum, not site HTML) and `RenderParity`/`Registry_CarriesExactlyTheThreeJustifiedWebviewChromeExceptions` (no new `HostRenderException`).
- One pre-existing adapter test (`WebviewRenderAdapterTests.Render_StampsTheSurfacePathTheBridgeResolvesLinksAgainst`) pinned the exact `#specscribe-surface` markup and was updated for the added `data-source=""` attribute (expected — the surface container gained one attribute).
- `cd extension && npm run typecheck && npm run build` → `tsc --noEmit` clean, esbuild bundles clean.
- Write-freedom scan of the working diff (`writeFile|fs.write|applyEdit|WorkspaceEdit|.update(|File.Write|SettingsStore`) → **NONE**. Only new host effect is `showTextDocument` (open, read-only).

### Completion Notes List

- **AC #1 — reveal-source (fully built):** `WebviewSurface.SourcePath` (repo-relative, nullable) computed per surface family in `RenderWebviewSurfaces` via a new `RepoRelative` helper (`Path.GetRelativePath(RepoRoot, …)` + forward slashes — the exact `configuredOutputRoot` convention, **no duplicated path assumption**); serialized as `sourcePath` on each payload surface object. `WrapDocument` gained a `string? sourcePath` param → `__SOURCE__` → `data-source` on `#specscribe-surface` + an **"Open source"** `<button class="ss-reveal-src-btn">` in the webview-only `.ss-webview-toolbar` (beside 6.5's copy-prompt button). The bridge posts `revealSource{path}`; the shim joins it to `workspaceFolders[0]` through a containment guard and calls `showTextDocument` **read-only**. The button hides when the surface has no source (the dashboard); the bridge toggles it on first paint and every in-place swap.
- **AC #2 — the seam (guaranteed, not populated):** `revealSource` is line-capable (`{path, line}`, 1-based → 0-based `vscode.Range`). The bridge's anchor branch recognizes `data-code-path`(+`data-line`) → `revealSource{path,line}` **before** navigate/openExternal — **inert until Story 7.2 emits those attributes**. The `stageCommand` terminal-handoff extension point (reuse of `stageTerminalCommand`'s `createTerminal` + `sendText(cmd, false)`) is **documented** at the bridge dispatch for **Story 8.4** to build; no handler or emission here.
- **Harmonized the source-path convention (Task 4):** 6.9's tree "Open Source" previously joined `path.join(root, '_bmad-output', sourcePath)` with a source-root-relative `OutlineStory.SourcePath`. Aligned onto the repo-relative convention: `OutlineStory.SourcePath` is now repo-relative (same `RepoRelative` helper), and the shim's `openSource` drops the `_bmad-output` literal, routing through the shared `resolveWorkspacePath` guard. **One convention, one helper, zero `_bmad-output` literal in TypeScript.**
- **Read-only end to end (AD-6/ADR 0003/FR-17/NFR-5):** `showTextDocument` opens; the documented command seam stages (`sendText(cmd, false)`) — SpecScribe never writes and never presses Enter.
- **Byte-identity + parity safe by placement:** the control is a `<button>` in the webview-only shell toolbar (`WrapDocument`), never in the shared body/nav/breadcrumb. Golden fingerprint unchanged; parity green; **no new `HostRenderException`** (registry still 3 webview + 1 spa).
- **Path guard (defense-in-depth, 17.2 posture):** `resolveWorkspacePath` rejects `..`-escapes, absolute overrides, and non-existent targets — the shim can't become an "open any file on disk" primitive on a stale/hostile payload.
- **Automated-coverage boundary (honest):** the extension has **no TS test harness** — automated gates are `tsc --noEmit` + esbuild + JSON validity + the C# suite (which covers the payload `sourcePath` + its per-family computation + serialization). Functional reveal-source verification is the **F5 manual smoke** (extended in `extension/README.md`); **one F5 smoke in real VS Code remains** (button visibility on story/epic vs hidden on dashboard; opens the correct `.md` read-only; the hand-inserted `data-code-path`/`data-line` anchor opens at the line; guard rejects out-of-workspace).
- **Subdir-open caveat (NOT solved; NOT regressed):** the host joins `sourcePath` under `workspaceFolders[0]`; when VS Code opens a subdirectory (repo root ≠ workspace folder) the join is wrong — the **same** limitation the watchers carry today (**Story 6.11**). No `repoRoot`-in-payload field added; the common repo-root-open case is correct.

### File List

- `src/SpecScribe/WebviewBundle.cs` — `WebviewSurface.SourcePath` (repo-relative, nullable) added to the record.
- `src/SpecScribe/SiteGenerator.cs` — `RepoRelative` helper; `_epicsSourcePath` cache set in `RenderEpicsPages`; per-surface source computation in `RenderWebviewSurfaces` (story `.md` / epics.md / null); harmonized outline `OutlineStory.SourcePath` to repo-relative; `WebviewSurfaceFor` gains `string? sourcePath`; entry-document wrap passes the dashboard's (null) source.
- `src/SpecScribe/WebviewRenderAdapter.cs` — `WrapDocument` `string? sourcePath` param + `__SOURCE__`; `data-source` on `#specscribe-surface`; the "Open source" toolbar button; bridge additions (reveal-button click, `data-code-path`/`data-line` recognition, documented `stageCommand` seam, `data-source` refresh + button toggle on swap).
- `src/SpecScribe/Commands.cs` — serialize `sourcePath` on each payload surface object.
- `src/SpecScribe/ProjectOutline.cs` — `OutlineStory.SourcePath` doc updated (now repo-relative).
- `src/SpecScribe/assets/specscribe-webview-theme.css` — `.ss-reveal-src-btn` shares the toolbar-button look (secondary-button palette with primary fallback); distinct class so the helper branch can't misfire.
- `extension/src/extension.ts` — `SurfaceContent.sourcePath?`; `source` on the push `update` message; `revealSource` handler (guarded, line-capable, read-only); `resolveWorkspacePath` containment guard; `openSource` harmonized off the `_bmad-output` literal; header/interface doc updates.
- `extension/README.md` — Story 6.10 F5 smoke checklist + scope note.
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — 3 new tests (per-family `sourcePath`, camelCase serialization + dashboard null, entry-doc hidden reveal button + `data-source=""` + seam present).
- `tests/SpecScribe.Tests/SiteGeneratorOutlineTests.cs` — 6.9 outline `SourcePath` assertion updated to repo-relative (harmonization).
- `tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs` — `#specscribe-surface` assertion updated for the added `data-source=""`.

## Change Log

- 2026-07-12 — **Code review (parallel adversarial + edge-case + acceptance-audit).** Acceptance Auditor: all ACs and risk centers verified faithfully implemented — read-only proof clean (no write API anywhere in the diff), path-convention harmonized with 6.9 (`_bmad-output` literal fully removed from TS), byte-identity/parity untouched (registry still 3 webview + 1 spa), AC #2 seam genuinely inert, subdir-open behavior unchanged. 6 patches applied from the Blind Hunter + Edge Case Hunter layers, all in `resolveWorkspacePath`/`revealSource` (`extension/src/extension.ts`): silent-failure and missing-try/catch on `revealSource` now mirror `openSource`'s error-message pattern; the containment guard now resolves symlinks (`fs.realpathSync`) before comparing, rejects directories (`fs.statSync(...).isFile()`), and case-folds on Windows; a C# test gap (`EntryDocument_CarriesTheHiddenRevealButton_...`) now asserts the `hidden` attribute itself, not just the class name. 1 low-severity item deferred (`RepoRelative`'s silent absolute-path fallback on a cross-drive/misconfigured `RepoRoot` — see deferred-work.md). Two automated-layer findings investigated and dismissed as false positives: a `vscode` null-guard "gap" already covered by an earlier guard in the same click handler, and an "unverified" live-push concern resolved by tracing that there's only one `postMessage({type:'update',...})` call site. `tsc --noEmit` + esbuild clean; targeted `SiteGeneratorWebviewTests` (11) green. Full-suite `GoldenContentFingerprint` run showed unrelated drift traced to other uncommitted work already in the tree (Story 6.12 diagnostics, not this story's diff). Status → done.
- 2026-07-11 — **Implemented (dev-story).** AC #1 reveal-source fully built: per-surface repo-relative `SourcePath` on the `webview` payload (computed in `RenderWebviewSurfaces` via a `RepoRelative` helper mirroring `configuredOutputRoot`), a webview-only "Open source" toolbar `<button>` posting a `revealSource` host message, the shim opening the `.md` read-only via `showTextDocument` (workspace-folder join through a containment guard — no duplicated path assumption). AC #2 seam established: line-capable `revealSource{path,line}` + inert `data-code-path`/`data-line` recognition (Story 7.2 rides it), `stageCommand` terminal-handoff point documented for Story 8.4. Harmonized 6.9's tree "Open Source" onto the one repo-relative convention (dropped the `_bmad-output` literal from TypeScript). Read-only end to end (write-API scan clean; only `showTextDocument`). HTML surface byte-identical — control is webview-only toolbar chrome, no new `HostRenderException`, golden + parity green. 762 C# tests pass (+3 new, 1 pre-existing markup assertion + the 6.9 outline `SourcePath` assertion updated); `tsc --noEmit` + esbuild clean. One F5 manual smoke in real VS Code remains (README checklist). Status → review.
- 2026-07-11 — Story drafted (create-story) from the VS Code Native-Integration Recommendations (FR35, [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md)) "Next" wave. Seats **R4.1** (reveal-source: the `webview` payload carries a core-resolved **repo-relative** source-artifact path per surface — mirroring `configuredOutputRoot`; a webview-only "Open source" `<button>` in the `.ss-webview-toolbar` posts a `revealSource` host message; the shim opens the `.md` read-only via `showTextDocument`, joining the workspace folder — **no duplicated path assumption**), and establishes the **R4.2/R4.3 structured-link seam** (a line-capable `revealSource{path,line}` + the bridge recognizing `data-code-path`/`data-line`, inert until Story 7.2 emits; the terminal-command `stageCommand` extension point documented for Story 8.4 to build). **Hard-gated on Story 6.8** (`done` required — same shim message loop / `resolveTool` / `projectDetected`); **build after Story 6.9** (shared `WebviewSurface`/payload/interface edits + harmonize the source-path convention off 6.9's `_bmad-output`-join tree action onto the repo-relative form); 6.4/6.5 done. Read-only end to end (`showTextDocument` opens, never writes; staged commands never execute); HTML surface byte-identical (control is webview-only toolbar chrome — no `RenderParity` impact, no new `HostRenderException`). Explicitly OUT: code-link/command **emission** (7.1/7.2, 8.4), tree/status-bar/shared-provider (6.9), watch-hardening / subdir-root resolution (6.11), per-section reveal anchors, new settings/commands/manifest. Status → ready-for-dev.
