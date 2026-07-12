# SpecScribe Status — VS Code extension

A **read-only, live-updating in-editor view** of a SpecScribe project's dashboard and epics surfaces
(dashboard, epics index, epic pages, story pages, story placeholders — Story 6.4's navigable set).

All rendering happens in the **SpecScribe C# core**: the extension spawns `specscribe webview`, reads one JSON
bundle of finished CSP-safe HTML from stdout, injects the webview's `cspSource` + a script nonce, and assigns
`webview.html` once. Navigation and file-change refreshes are in-place `postMessage` content swaps — the panel
never resets. The shim parses no markdown, scrapes no generated site, and writes nothing (ADR 0005 / AD-1 /
AD-2 / AD-6).

## Running against this repo (F5 dev host)

1. Build the C# tool once: `dotnet build src/SpecScribe` (from the repo root).
2. `cd extension && npm install && npm run build`
3. Open the `extension/` folder in VS Code and press **F5** (Run Extension). In the dev host, open the
   SpecScribe repo (or any BMad project) as the workspace folder.
4. Point the extension at your built tool: in the dev host's settings, set
   `specscribe.toolPath` to `<repo>/src/SpecScribe/bin/Debug/net10.0/SpecScribe.dll` (a `.dll` is run via
   `dotnet`; an executable path or `specscribe` on PATH also works).
5. Run the command **SpecScribe: Open Status**.

Live push: edit any `_bmad-output/**/*.md` (or `docs/adrs/**/*.md`) file and the visible surface refreshes in
place, preserving your scroll position and drill context.

## F5 smoke checklist (manual — Story 6.8)

The manifest/menu/trust behaviors below have **no automated harness** (the extension has no TS test runner; the
CI gates are `tsc --noEmit` + esbuild + `.specscribe` JSON validity + the C# suite). Verify them by hand in the F5
dev host — this is the human step Stories 6.4/6.5 also flagged:

- **Discoverability (AC #1):** in a SpecScribe repo, the Command Palette lists SpecScribe: Open Dashboard / Open
  Epics / Refresh Status / Open Generated Site / Generate Full Site / Watch / Open Project Settings / Open Status.
  Open a **non-SpecScribe** folder (no `_bmad/config.toml`, no `_bmad-output/`) → **zero** SpecScribe entries.
- **Direct-open:** Open Dashboard shows the home surface; Open Epics opens the same panel to the epics index
  (in-place swap, no second panel). With the panel already open, either command reveals + navigates it.
- **Refresh:** Refresh Status re-renders in place; running it during an auto file-watch re-render does not
  double-spawn.
- **Menus (AC #1/#3):** right-click a file/folder under `_bmad-output/` → **Open Status** appears; right-click
  outside it → it does not. With an `_bmad-output/**/*.md` file active, the editor title (⋯ overflow) shows it too.
- **Workspace Trust (AC #2):** open an **untrusted** workspace that sets `specscribe.toolPath` in
  `.vscode/settings.json` to a decoy path → the extension **ignores** the workspace value (falls back to
  bundled/PATH) until you trust the workspace; your **user/machine** `toolPath` still applies.
- **Terminal handoff (AC #3):** Generate Full Site / Watch open a `SpecScribe` terminal with the command **staged
  but not executed** (you press Enter); Open Project Settings reveals `.specscribe` if present, else offers to
  stage the interactive setup — it never writes `.specscribe` itself.
- **Open Generated Site (AC #3):** with a generated `SpecScribeOutput/index.html` present it opens in the browser;
  absent, it shows the "generate first" notice.
- **Open beside (AC #3):** with `specscribe.openLocation` = `beside` (default) the panel opens next to the active
  editor; `active` reuses the focused column.
- **UX polish (AC #4):** first open shows a "SpecScribe: rendering…" progress notification; a bad
  `specscribe.toolPath` yields an actionable notification (**Set specscribe.toolPath** / **Retry**) alongside the
  error page; the panel tab carries the SpecScribe icon.

## F5 smoke checklist (manual — Story 6.9: native tree + status bar)

Same coverage boundary as above — the tree/status-bar/welcome surfaces have **no TS test harness**; the automated
gates are `tsc --noEmit` + esbuild + valid `package.json` + the C# suite (which covers the new core `outline`
export). Verify the native surfaces by hand in the F5 dev host:

- **Tree appears only in a SpecScribe repo (AC #1):** the **SpecScribe** icon shows in the activity bar; its
  **Project Outline** view lists epics, each expandable to its stories. Open a **non-SpecScribe** folder → the
  view shows the `viewsWelcome` guidance ("No SpecScribe project detected…"), not a dead/empty tree.
- **Stage icons carry the six-stage vocabulary (AC #1, constraint #5):** each node's icon color reflects its
  stage (done/review/active/ready/drafted/pending) via the contributed `specscribe.status.*` colors — **not** VS
  Code's pass/fail/warning severity palette. Toggle light / dark / high-contrast themes: the accents stay legible
  and mutually distinguishable, and the shape (filled/hollow circle, check, eye, slash) reinforces the color.
- **Labels/counts are core-decided:** epic rows read `Epic N: Title` with a `done/total` story count; story rows
  read `N.M Title` with a `done/total` task count (only when it has tasks). Nothing is computed in the extension.
- **Click reveals in the panel (AC #2):** clicking any epic/story node opens (or reveals) the **single** status
  panel and navigates it to that surface in place — no second panel, and it reuses the tree's already-loaded data
  (no extra render spawn).
- **Context actions are read-only (AC #2):** right-click a story with a drafted artifact → **Open Source File**
  opens its `_bmad-output/…md` in a read-only editor; a story with a helper command → **Copy Helper Prompt**
  copies the `/bmad-…` command to the clipboard (a toast confirms). Undrafted / done stories omit the respective
  action. Nothing writes a file or runs the command.
- **Status bar (AC #2):** a `$(checklist) SpecScribe: N active · M review` item shows once data has loaded;
  clicking it opens the status panel; its tooltip carries the fuller `done/total` counts. It is hidden in a
  non-SpecScribe repo.
- **Stale/error on BOTH surfaces (AC #2):** force a failure (e.g. set `specscribe.toolPath` to a bad path and
  Refresh) → the status bar switches to `$(warning) SpecScribe: data stale` with a warning background, and the
  tree shows a "⚠ Last refresh failed — showing cached data" node above the (still-visible) cached epics. Fix the
  path and Refresh → both clear.
- **Refresh & live push:** the outline's title-bar **Refresh** button re-renders; editing an `_bmad-output/**/*.md`
  file live-updates the tree, status bar, and open panel together (one coalesced spawn).

## F5 smoke checklist (manual — Story 6.10: reveal-source editor↔artifact bridge)

Same coverage boundary — reveal-source has **no TS test harness**; the automated gates are `tsc --noEmit` +
esbuild + valid `package.json` + the C# suite (which asserts the payload's per-surface `sourcePath`: a story → its
`.md` repo-relative, an epic/index/placeholder → `epics.md`, the dashboard → null, forward-slashed). Verify the
host-delivery by hand in the F5 dev host:

- **"Open source" button visibility (AC #1):** open the panel — on the **dashboard** the toolbar's **Open source**
  button is **hidden** (the dashboard aggregates many artifacts). Navigate to any **epic** or **story** surface
  (click a nav/drill link, or a tree node) → the button **appears**; navigate back to the dashboard → it hides
  again.
- **Reveal opens the right file, read-only (AC #1):** on a **story** surface, click **Open source** → its
  `_bmad-output/…/<n-m-…>.md` opens in an editor. On an **epic** or **epics-index** surface → `epics.md` opens.
  Nothing is written; it is a plain editor open.
- **One path convention (AC #1):** the tree's **Open Source File** context action (Story 6.9) opens the **same**
  file as the webview button for a given story — both join the core's repo-relative `sourcePath` to the workspace
  folder (no `_bmad-output` literal remains in the shim).
- **Line-capable code seam (AC #2, inert until Story 7.2):** temporarily hand-insert an anchor into a story's
  rendered body, e.g. `<a href="#" data-code-path="_bmad-output/planning-artifacts/epics.md" data-line="10">x</a>`
  (or set the attributes via the dev-host DOM), and click it → the file opens **at line 10** (the selection lands
  on that line). Confirms the `data-code-path`/`data-line` recognition Story 7.2 will ride. Remove the probe after.
- **Guard (read-only-within-workspace):** a `data-code-path` pointing outside the workspace (`../…` or an absolute
  path) or at a non-existent file is **rejected** — no editor opens, no error spew.

## What this folder is (and isn't)

- Self-contained: not part of the .NET solution, not part of the generated-site pipeline, no CI wiring.
- **Packaging/publish is Story 16.5's job** — the VSIX will bundle a self-contained `specscribe` binary under
  `bin/` (the resolution order in `src/extension.ts` already prefers it). `npm run package` exists only for
  local smoke tests; nothing here publishes. The **Marketplace** icon/metadata and walkthrough are also 16.5;
  the panel-tab icon under `media/` (Story 6.8) is a different, in-scope asset.
- Host-aware theming and helper actions shipped in **Story 6.5**; discoverability, the command/menu surface,
  Workspace Trust, and open-beside shipped in **Story 6.8**.
- The native activity-bar **tree view + status bar** shipped in **Story 6.9** (a new core `outline` export drives
  them; the shim maps it 1:1 with a pure stage→icon lookup and holds the shared payload for panel + tree + bar).
- The webview **reveal-source bridge** ("Open source" → open the surface's `.md` read-only) shipped in **Story
  6.10**, which also established the line-capable `revealSource` seam that Story 7.2's code citations and Story
  8.4's next-step command will ride (the `data-code-path`/`data-line` recognition is present but inert; the
  `stageCommand` terminal-handoff point is documented in `WebviewRenderAdapter.cs`, built in 8.4).
- Still out of this extension's scope: file-watch/multi-root hardening — the yaml/toml watch gap, core-derived
  watch roots, the subdir-open path caveat (repo root ≠ workspace folder), multi-root (Story 6.11).
