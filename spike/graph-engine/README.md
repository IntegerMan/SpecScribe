# `spike/graph-engine` — Story 24.6 Epic 24 Graph-Engine Spike

**Throwaway.** Quarantined per [`spike/README.md`](../README.md): not in `SpecScribe.slnx`, not built by
`dotnet build src/SpecScribe`, not packed, not in the `extension/` bundle. Verified: the solution contains only
`SpecScribe.csproj` and `SpecScribe.Tests.csproj`, and it builds clean with this directory present.
**Deleting this whole directory leaves the shipped tool byte-identical.**

The **durable outputs** are
[`24-6-spike-report.md`](../../_bmad-output/implementation-artifacts/24-6-spike-report.md) and
**[ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md)**. The code here is the evidence behind them and can be
deleted once Story 24.2 lands.

## What it decided

Epic 24's force-directed views use the **already-vendored Plotly `scatter` trace** over a **generation-time C#
layout** — marginal bundle cost **zero bytes**. ADR 0012 is **extended, not superseded**; no new engine family, no
second runtime dependency. See the ADR for the options table and consequences.

## Layout

| Path | What |
|---|---|
| `layout/` | Throwaway C#: builds fixtures from the **real Story 24.1 metric** and solves a seeded, deterministic Fruchterman–Reingold layout. Holds a **one-way** `ProjectReference` to `src/SpecScribe` so it calls the shipped `GitMetrics` API instead of restating it. Nothing in `src/` references it back. |
| `fixtures/` | Emitted node/edge payload islands, shaped after the shipped `sunburst-explorer-data` island, plus `scale.json`. |
| `scripts/build-bundles.mjs` | Builds and measures the candidate engines as custom tree-shaken **IIFE** bundles, and asserts R2 against the **shipped** `plotly-hierarchy.min.js`. |
| `scripts/build-probes.mjs` | Inlines a fixture into each probe page as a data island and copies the vendored assets. |
| `scripts/csp-probe.mjs` | Serves `probe/` under the webview CSP, **read out of `WebviewRenderAdapter.cs` at runtime** — never pasted. Fails loudly if it cannot find the policy. |
| `scripts/verify-determinism.mjs` | Runs the C# probe in **separate processes** and hashes every fixture. |
| `probe/templates/*.html` | One page per candidate: Plotly `scatter`, ECharts (SVG renderer), Cytoscape. |
| `probe/harness.js` | Shared measurement surface — token allowlist built at runtime, colour audit with a **painting** predicate, mechanical a11y survival predicate, geometry audit, tooltip/seam checks. |
| `measurements/` | `bundles.json` **[HARNESS]** · `determinism.json` **[HARNESS]** · `session.json` **[SESSION]** |

`node_modules/`, `dist/`, `bin/`, `obj/` are gitignored here.

## Run it

```sh
# 1. fixtures + deterministic layout (3 in-process runs)
dotnet build spike/graph-engine/layout/GraphEngineSpike.csproj
dotnet run --project spike/graph-engine/layout/GraphEngineSpike.csproj --no-build -- \
  --repo . --out spike/graph-engine/fixtures --runs 3

cd spike/graph-engine

# 2. determinism ACROSS PROCESSES — the load-bearing check
node scripts/verify-determinism.mjs 3

# 3. candidate bundles + the R2 shipped-`scatter` assertion
npm install && npm run bundles

# 4. assemble the probe pages
node scripts/build-probes.mjs ego-top20     # or ego-top8 | ego-top40 | whole-repo-support-5 | …

# 5. serve under the byte-verbatim webview CSP
node scripts/csp-probe.mjs 8131 webview header
#   variants: webview | no-style-inline | wrong-nonce | unsafe-eval | off
#   delivery: header | meta
```

Registered `.claude/launch.json` entries: `graph-24-6-csp` (8131), `graph-24-6-meta` (8132),
`graph-24-6-nocsp` (8133), `graph-24-6-wrongnonce` (8134), `graph-24-6-nostyle` (8135).

## Two traps this probe fell into, kept here so the next reader doesn't

1. **`echarts.init()` on a zero-height container throws an uncaught `TypeError`.** It presented as an *intermittent*
   failure on page load and reproduces deterministically once you vary the container size. Plotly survives every
   zero-size case.
2. **ECharts geometry is animation-frame-gated.** At initial render every link path carries `d=""` and every symbol
   `scale(0)` — **while every accessibility attribute passes**. `animation:false` with **`lazyUpdate:false`** renders
   the settled state synchronously; passing `lazyUpdate:true` defers to a frame that never arrives in a
   non-compositing tab, which looks exactly like a rendering defect. **Assert on geometry, not attributes.**

Related: the in-app Browser pane does **not** composite (measured 0 rAF frames in 1,200 ms,
`visibilityState: "hidden"`), so no screenshot was possible and ECharts' force-layout determinism is **unmeasured**
rather than confirmed. Both boundaries are named in the report.
