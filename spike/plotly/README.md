# `spike/plotly` — Story 20.4 Plotly engine-adoption probe (throwaway)

Quarantined per [`spike/README.md`](../README.md). Nothing here joins `SpecScribe.slnx`, `dotnet build
src/SpecScribe`, `dotnet pack`, or the `extension/` bundle. **No production code was written by this story.**

The durable outputs are [`20-4-spike-report.md`](../../_bmad-output/implementation-artifacts/20-4-spike-report.md)
and the addendum on [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md).
Everything in this folder is evidence for those two documents and can be deleted once Story 20.5 lands.

## What is here

| Path | What it is |
|---|---|
| `plotly-src/` | a `--depth 1` clone of plotly.js **v3.7.0** plus its dev dependencies. **Gitignored.** The npm package `plotly.js` does **not** ship `tasks/`, so `npm run custom-bundle` is only available from a clone — see the report's Finding B. |
| `probe-src/explorer.js` | the SpecScribe-side adapter under test: island → Plotly trace, tokens from the cascade, the roving-tabindex a11y layer, the reduced-motion wiring |
| `probe-src/survival.js` | the UX-DR7 survival harness — drives each re-render event individually and re-audits the DOM after each |
| `probe/` | the assembled probe pages. **Generated — do not edit.** Rebuild with `node scripts/build-probe.mjs`. |
| `scripts/measure-baseline.mjs` | the AC #1 numbers: hierarchy SVG per page across the whole portal, the real island, the net delta, the break-even page count |
| `scripts/measure-bundle.mjs` | bundle sizes (min / gzip / brotli, as a multiple of the shipped `prism.js`) and the static CSP-construct inventory |
| `scripts/build-probe.mjs` | assembles `probe/` from **real** sources — the shipped stylesheet, the real dashboard island, and the CSP string read out of `WebviewRenderAdapter.cs`. Nothing is re-typed. |
| `scripts/csp-probe.mjs` | serves `probe/` under the byte-verbatim webview policy, in four variants. Same shape as Story 23.1's harness. |
| `measurements/baseline.json` | harness output — re-derivable |
| `measurements/bundle.json` | harness output — re-derivable |
| `measurements/session.json` | **session-measured** browser evidence that no script can produce (CSP matrix, focus survival, computed colors) |

## Reproduce

```sh
# 0. a portal to measure against (never --output docs/live)
dotnet run --project src/SpecScribe -c Release -- generate --deep-git --output <somewhere>

# 1. the bundles
cd spike/plotly && npm install
git clone --branch v3.7.0 --depth 1 https://github.com/plotly/plotly.js.git plotly-src
cd plotly-src && npm i --ignore-scripts
npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy
npm run custom-bundle -- --traces sunburst,treemap,heatmap --strict --out specscribe-hierarchy-strict

# 2. the numbers
cd .. && node scripts/measure-bundle.mjs
node scripts/measure-baseline.mjs <somewhere>

# 3. the probe, then the live browser
node scripts/build-probe.mjs <somewhere>
node scripts/csp-probe.mjs 5411 webview        # then open http://localhost:5411/nonced.html
node scripts/csp-probe.mjs 5412 off            # then open /webview-meta.html, /webview-partial.html, /nojs.html
node scripts/csp-probe.mjs 5413 no-inline-style
```

In the browser console: `window.__probe.audit()` for the colorway/a11y snapshot, `window.__runSurvival()` then
`window.__res` for the UX-DR7 survival table.

## Cleanup

`plotly-src/`, `node_modules/` and `probe/` are gitignored. Delete the whole folder when Story 20.5 lands its real
`tools/plotly-vendor/`.
