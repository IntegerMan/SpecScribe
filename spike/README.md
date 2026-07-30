# `spike/` — throwaway feasibility probes (not shipped)

Everything under `spike/` is **disposable**. It is deliberately quarantined: no `.sln` references it, it is not
part of `src/SpecScribe`'s build or `dotnet pack`, and it contributes **no** rendering path to the shipped
`specscribe` tool. The generated site is byte-identical with or without this folder.

## `spike/vscode` — Story 6.3 VS Code Integration Spike

Proves the core↔extension seam for the eventual read-only VS Code webview (Epic 6). The **durable output is
[ADR 0005](../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md)** — the code here is the evidence that
backs it, and can be deleted once Story 6.4 (the runtime) lands.

- `renderer/` — a C# console app (`specscribe-webview-spike`) that references `src/SpecScribe` and renders the
  **dashboard + epics** surfaces to webview-safe HTML from the SAME host-neutral view models the HTML surface uses
  (`DashboardViewBuilder`/`EpicsViewBuilder` → `HtmlRenderAdapter`). No scraping, no `.md` re-parse. Prints JSON
  `{ dashboard, epics, dashboardBody, epicsBody, siteTitle }` on stdout; `--out DIR` writes the two docs for
  eyeballing.
- `src/extension.ts` — the ~180-line "irreducible" TS shim: register command → open `WebviewPanel` → spawn the
  renderer → inject `cspSource` + a `nonce` → live-push on `_bmad-output/**/*.md` change. Renders nothing itself.

### Run it

```bash
# 1. build + run the renderer against this repo (writes dashboard.html / epics.html)
dotnet run --project spike/vscode/renderer -- "." --out spike-out

# 2. build the extension shim
cd spike/vscode && npm install && npm run build

# 3. try it in a real VS Code (the one step a headless environment can't do):
#    open spike/vscode in VS Code, press F5, run the "SpecScribe: Open Status (Spike)" command.
#    The shim spawns `dotnet renderer.dll` by default; override with SPECSCRIBE_SPIKE_RENDERER.
```

### What was proven vs. what wasn't

Everything up to `webview.html = <string>` is evidence-backed (data path, CSP survival, shim compile/bundle,
spawn + JSON, packaging sizes + latency). The single unproven step — actual pixel paint + live refresh inside VS
Code's Electron webview — needs one manual `F5` run and is called out in ADR 0005. See the ADR for the full
findings and the seated Story 6.4 scope.

## `spike/graph-engine` — Story 24.6 Epic 24 Graph-Engine Spike

Decides Epic 24's graph engine, the open question [ADR 0012](../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §4
handed to "Epic 24's own spike". The **durable outputs are
[ADR 0030](../docs/adrs/0030-epic-24-graph-engine.md)** and
[`24-6-spike-report.md`](../_bmad-output/implementation-artifacts/24-6-spike-report.md); the code here is the
evidence behind them and can be deleted once Story 24.2 lands. See
[`graph-engine/README.md`](graph-engine/README.md) for how to run it.

Outcome: the **already-vendored Plotly `scatter` trace** over a **generation-time C# layout** — marginal bundle cost
**zero bytes**, no new engine family, no second runtime dependency.
