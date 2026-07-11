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

## What this folder is (and isn't)

- Self-contained: not part of the .NET solution, not part of the generated-site pipeline, no CI wiring.
- **Packaging/publish is Story 16.5's job** — the VSIX will bundle a self-contained `specscribe` binary under
  `bin/` (the resolution order in `src/extension.ts` already prefers it). `npm run package` exists only for
  local smoke tests; nothing here publishes.
- Host-aware theming and helper actions are **Story 6.5** — this panel deliberately renders with SpecScribe's
  own styling and offers no buttons.
