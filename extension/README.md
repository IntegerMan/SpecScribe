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

## What this folder is (and isn't)

- Self-contained: not part of the .NET solution, not part of the generated-site pipeline, no CI wiring.
- **Packaging/publish is Story 16.5's job** — the VSIX will bundle a self-contained `specscribe` binary under
  `bin/` (the resolution order in `src/extension.ts` already prefers it). `npm run package` exists only for
  local smoke tests; nothing here publishes. The **Marketplace** icon/metadata and walkthrough are also 16.5;
  the panel-tab icon under `media/` (Story 6.8) is a different, in-scope asset.
- Host-aware theming and helper actions shipped in **Story 6.5**; discoverability, the command/menu surface,
  Workspace Trust, and open-beside shipped in **Story 6.8**.
- Still out of this extension's scope: the native tree view / status bar (Story 6.9), editor↔artifact reveal
  bridges (Story 6.10), and file-watch/multi-root hardening (Story 6.11).
