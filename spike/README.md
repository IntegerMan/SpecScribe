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

## `spike/graph-engine` — Story 24.6 Epic 24 Graph-Engine Spike — **PRUNED 2026-08-08**

Decided Epic 24's graph engine, the open question [ADR 0012](../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §4
handed to "Epic 24's own spike". The **durable outputs are
[ADR 0030](../docs/adrs/0030-epic-24-graph-engine.md)** and
[`24-6-spike-report.md`](../_bmad-output/implementation-artifacts/24-6-spike-report.md).

Outcome: the **already-vendored Plotly `scatter` trace** over a **generation-time C# layout** — marginal bundle cost
**zero bytes**, no new engine family, no second runtime dependency.

**The probe code is gone**, exactly as this entry originally anticipated (*"can be deleted once Story 24.2
lands"*) — 24.2 is `done`, and Story 24.6's code review removed the directory on 2026-08-08. Two reasons beyond
"it served its purpose", both worth remembering the next time a spike vendors a workspace:

* Its `package.json` / `package-lock.json` were **tracked**, so a directory-quarantined throwaway became a live
  Dependabot surface within a day (`f4f5629`, esbuild 0.24.2 → 0.28.1) — guarding nothing shippable, and silently
  invalidating the report's own bundle-reproduce path. **Directory quarantine is not dependency quarantine.**
* Its reproduce commands had decayed: the CSP harness began matching the `__CSP__` placeholder after
  `WebviewRenderAdapter` moved the policy into a const, serving no enforceable policy at all with its negative
  control disabled; and the `file://` recipe never worked as committed. See report §13.

**The report and ADR 0030 are now the record of the measurement.** Nothing in `src/` ever referenced this
directory, and the project referenced `src/SpecScribe` one-way only, so its removal changes no shipped byte.

## `spike/findings` — Story 25.3 Agent-Facing Findings Contract Spike

Decides the source-agnostic findings model that **both** Epic 25 and Epic 26 bind to. The **durable outputs are
[ADR 0023](../docs/adrs/0023-agent-facing-analysis-observation-contract.md)** and
[`25-3-spike-report.md`](../_bmad-output/implementation-artifacts/25-3-spike-report.md); everything in this folder is
the evidence behind them and can be deleted once Story 25.4 and Stories 26.2–26.6 have landed.

| File | What it is |
|---|---|
| `roslyn-specscribe.sarif` / `roslyn-tests.sarif` | 834 raw Roslyn SARIF 2.1 results (261 + 573) from `dotnet build -t:Rebuild -p:ErrorLog=<abs>%2cversion=2.1`, one project at a time. The **second source class** AC #1 required — these never passed through SonarCloud. Captured 2026-07-28. |
| `map_to_model.py` | The original two-way mapping, Sonar ⇄ raw SARIF ⇄ `AnalysisObservation`, with a per-direction loss ledger. AC #1's demonstration. |
| `measure_channels.py` | The original digest sizing. AC #3's numbers. |
| `remeasure_dedup.py` | The 2026-08-07 code-review re-measurement: deduplicates the two providers and sizes the **full** ADR 0023 record. Supersedes `measure_channels.py`'s figures. |
| `sonar-snapshot-2026-08-07.json` | Minimal field-subset snapshot of 1,755 live Sonar issues, so `remeasure_dedup.py` reproduces with no network. |

**Known defects in the two original scripts, deliberately left as-is** (recorded by the Story 25.3 code review and in
`deferred-work.md`): they crash on the zero-findings path, cap Sonar input at three pages (1,500 issues — already
exceeded by the live backlog), open files without an explicit encoding, and normalize SARIF paths in a way that can
re-emit an absolute build-machine path. `remeasure_dedup.py` fixes these; the originals are preserved unedited
because they are the artifact that produced the figures quoted in the report and the ADR.

**The quarantine guarantee.** Nothing here is referenced by `SpecScribe.slnx`, any `.csproj`, or any workflow under
`.github/`; nothing participates in the build; and the generated site is byte-identical with and without this folder
(tested — see report § 13.3, which required normalizing the per-run footer stamp first).
