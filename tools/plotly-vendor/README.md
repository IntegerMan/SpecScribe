# Plotly vendoring — the Hierarchy Explorer engine

The Hierarchy Explorer (`HierarchyExplorer.cs` + the `[data-hierarchy]` block in `specscribe.js`) renders its
sunburst and treemap with a **vendored, locally-built** plotly.js custom bundle — never a CDN, so the generated
site works offline, from `file://`, on GitHub Pages, and inside the VS Code webview, and the global-tool package
stays self-contained. This is [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md)
§1 and PRD **NFR-3**, not a preference.

The shipped artifact is committed as an embedded resource:

- [`src/SpecScribe/assets/plotly-hierarchy.min.js`](../../src/SpecScribe/assets/plotly-hierarchy.min.js) —
  plotly.js **3.7.0**, custom bundle, `sunburst` + `treemap` + `heatmap`.

It is copied to the output root **only when the site rendered at least one hierarchy chart**, so a site with no
such chart stays byte-identical (the same conditional-emission discipline `prism.js` follows in `SiteGenerator`).

## Regenerate

```sh
cd tools/plotly-vendor
node build.mjs
```

Then rebuild the .NET project so the embedded resource picks up the new file, and re-baseline the golden
fingerprint deliberately (a vendored-asset change is expected to move it — `FingerprintTree` reads every emitted
file, this one included).

`plotly-src/` and its `node_modules/` are gitignored throwaway, exactly like `tools/prism-vendor/node_modules`.
`node build.mjs --no-fetch` reuses an existing clone instead of re-cloning.

## Why this is a git clone, not `npm i plotly.js`

**This folder deliberately does NOT have `tools/prism-vendor`'s shape.** It follows the same *discipline*
(hand-run build, committed artifact, embedded resource, conditional copy) but it cannot follow the same
*mechanism*, and the reason is a measured finding, not a preference:

> The published `plotly.js@3.7.0` npm package ships `lib/`, `src/`, `dist/` and `esbuild-config.js` — but **not**
> `tasks/`. `esbuild-config.js` requires `./tasks/util/constants.js`, so `npm run custom-bundle` cannot run from
> the package at all.

A `git clone --branch v3.7.0 --depth 1` of the upstream repo is therefore the only route to a custom bundle.
`build.mjs` performs the clone, the install, the bundle and the copy; the equivalent by hand is:

```sh
git clone --branch v3.7.0 --depth 1 https://github.com/plotly/plotly.js.git plotly-src
cd plotly-src && npm i --ignore-scripts
npm run custom-bundle -- --traces sunburst,treemap,heatmap --out specscribe-hierarchy
# then copy dist/plotly-specscribe-hierarchy.min.js -> ../../src/SpecScribe/assets/plotly-hierarchy.min.js
```

Two things about that command are worth knowing before you read its output as a surprise:

- **The resolved trace list is larger than the three requested** — `heatmap, scatter, sunburst, treemap`.
  `scatter` lives in `lib/core.js` and cannot be excluded from *any* plotly bundle. `calendars` rides along as a
  component. That is the true floor, not the three names ADR 0012 §1 lists.
- **Do not pass `--strict`.** Measured: the strict bundle is **7 bytes larger** with a byte-identical
  CSP-construct profile, because the `Function`-constructor paths `--strict` exists to remove live in the gl/regl
  traces this build already excludes. It buys nothing.

## Supply-chain record (NFR10 / Epic 17)

| Fact | Value |
|---|---|
| Package | `plotly.js` |
| Version | **3.7.0** (released 2026-07-03) — **pinned**; a bump invalidates every measurement in the Story 20.4 spike report and must be its own decision |
| License | **MIT** |
| Committed artifact | one self-contained classic script, 1,223,515 B minified (413,449 B gzip) |
| Transitive runtime footprint | **zero** — the bundle is standalone; nothing is fetched, imported, or resolved at runtime |
| Node in the `specscribe generate` pipeline | **none.** Built by hand and committed, exactly like `prism.js` |
| Remote origins reachable at runtime | **none.** `displayModeBar: false`, `plotlyServerURL: ''`, `topojsonURL: ''`, `displaylogo: false` — see below |
| `npm audit` on the upstream **clone**'s dev tree | 9 findings (1 low / 1 moderate / 7 high) — **all build-time devDependencies of the upstream repository, none of them in the emitted artifact.** Recorded here so the eventual audit is not surprised by it. |

### `displayModeBar: false` is a privacy requirement, not a cosmetic default

plotly.js **3.7.0** changed the modebar's `sendDataToCloud` button to upload the chart to Plotly Cloud. The
endpoint is reportedly not yet functional, but the button's *intent* is an outbound upload of the user's project
data. For a local-first generator that button must never exist, so the component disables the modebar outright
and a test asserts both that `displayModeBar:false` is set and that no `sendDataToCloud` handler ships.

The bundle contains four literal `plot.ly` references, all identified and none exercised: the `topojsonURL` geo
default (geo is not in this build), the modebar logo anchor (removed by `displaylogo:false`), and two error
strings. Its single `XMLHttpRequest` is d3's `d3.xhr`, reachable only from the topojson path.
