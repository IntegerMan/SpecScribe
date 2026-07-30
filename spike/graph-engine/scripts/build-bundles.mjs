// Story 24.6 Task 3/5 — build and measure the candidate engine bundles.
//
// The honest comparison is a CUSTOM, TREE-SHAKEN, SINGLE-CLASSIC-SCRIPT build, not a published dist size:
//   * IIFE format, because R9's hard constraint is one classic `<script nonce>` with no ES-module static imports
//     (Story 23.1 measured that a nonce does not propagate to a module's static imports, so they get blocked).
//   * minify + no sourcemap, matching how `plotly-hierarchy.min.js` and `prism.js` actually ship.
//   * every size reported as a multiple of the ALREADY-ACCEPTED `prism.js` (100,409 B), which is this project's
//     own answer to "how big a vendored dependency have we already said yes to".
//
// Candidate (a) — "Plotly scatter + generation-time layout" — deliberately has NO entry here. That is the finding,
// not an omission: `src/SpecScribe/assets/plotly-hierarchy.min.js` is already vendored and embedded, and it already
// registers the `scatter` trace, so its marginal cost is zero bytes. The script asserts that rather than assuming it.

import { build } from 'esbuild'
import { gzipSync, brotliCompressSync } from 'node:zlib'
import { mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs'
import { dirname, resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const repoRoot = resolve(root, '..', '..')
const distDir = join(root, 'dist')
const measDir = join(root, 'measurements')
mkdirSync(distDir, { recursive: true })
mkdirSync(measDir, { recursive: true })

/** The shipped yardsticks, read from disk rather than pasted, so a drift shows up as a moved multiple. */
const YARDSTICKS = {
  'prism.js': join(repoRoot, 'src/SpecScribe/assets/prism.js'),
  'specscribe.js': join(repoRoot, 'src/SpecScribe/assets/specscribe.js'),
  'plotly-hierarchy.min.js': join(repoRoot, 'src/SpecScribe/assets/plotly-hierarchy.min.js'),
}
const yard = {}
for (const [name, path] of Object.entries(YARDSTICKS)) {
  if (!existsSync(path)) throw new Error(`yardstick missing: ${path}`)
  const bytes = readFileSync(path)
  yard[name] = { min: bytes.length, gzip: gzipSync(bytes).length }
}
const PRISM = yard['prism.js'].min

// R2, asserted rather than inherited as folklore: the SHIPPED bundle registers `scatter`.
const plotlySrc = readFileSync(YARDSTICKS['plotly-hierarchy.min.js'], 'utf8')
const registered = [...new Set(
  [...plotlySrc.matchAll(/moduleType:"trace"[\s\S]{0,300}?name:"([a-z0-9]+)"/g)].map((m) => m[1]),
)].sort()

const CANDIDATES = {
  // (b) ECharts, limited to the three Epic 24 graph shapes. SVG renderer, because a canvas renderer emits no
  //     per-node DOM for a roving-tabindex layer to attach to (Task 6).
  'echarts-graph-svg': `
    import * as echarts from 'echarts/core'
    import { GraphChart, ChordChart } from 'echarts/charts'
    import { TooltipComponent, LegendComponent, TitleComponent } from 'echarts/components'
    import { SVGRenderer } from 'echarts/renderers'
    echarts.use([GraphChart, ChordChart, TooltipComponent, LegendComponent, TitleComponent, SVGRenderer])
    globalThis.echarts = echarts
  `,
  // The canvas variant, purely to price the SVG renderer's cost.
  'echarts-graph-canvas': `
    import * as echarts from 'echarts/core'
    import { GraphChart, ChordChart } from 'echarts/charts'
    import { TooltipComponent, LegendComponent, TitleComponent } from 'echarts/components'
    import { CanvasRenderer } from 'echarts/renderers'
    echarts.use([GraphChart, ChordChart, TooltipComponent, LegendComponent, TitleComponent, CanvasRenderer])
    globalThis.echarts = echarts
  `,
  // The UNIFICATION candidate: one engine covering Epic 20's hierarchy family AND Epic 24's graph family.
  // If this is chosen, ADR 0012 is superseded and plotly-hierarchy.min.js is retired.
  'echarts-unified-svg': `
    import * as echarts from 'echarts/core'
    import { GraphChart, ChordChart, SunburstChart, TreemapChart, HeatmapChart } from 'echarts/charts'
    import { TooltipComponent, LegendComponent, TitleComponent, GridComponent, VisualMapComponent } from 'echarts/components'
    import { SVGRenderer } from 'echarts/renderers'
    echarts.use([GraphChart, ChordChart, SunburstChart, TreemapChart, HeatmapChart,
      TooltipComponent, LegendComponent, TitleComponent, GridComponent, VisualMapComponent, SVGRenderer])
    globalThis.echarts = echarts
  `,
  // (c) the dedicated-graph-library family. Serves 24.2/24.3; serves NEITHER 24.4's chord NOR 24.5's matrix.
  cytoscape: `
    import cytoscape from 'cytoscape'
    globalThis.cytoscape = cytoscape
  `,
}

const results = []
for (const [name, source] of Object.entries(CANDIDATES)) {
  const entry = join(distDir, `${name}.entry.mjs`)
  writeFileSync(entry, source, 'utf8')
  const outfile = join(distDir, `${name}.min.js`)
  await build({
    entryPoints: [entry],
    outfile,
    bundle: true,
    minify: true,
    format: 'iife',
    platform: 'browser',
    target: ['es2019'],
    legalComments: 'none',
    sourcemap: false,
    logLevel: 'error',
  })
  const bytes = readFileSync(outfile)
  const src = bytes.toString('utf8')
  results.push({
    candidate: name,
    min: bytes.length,
    gzip: gzipSync(bytes).length,
    brotli: brotliCompressSync(bytes).length,
    xPrismMin: +(bytes.length / PRISM).toFixed(2),
    xPrismGzip: +(gzipSync(bytes).length / PRISM).toFixed(2),
    // R9's hard constraints, counted statically over the emitted artifact rather than assumed from config.
    csp: {
      newFunction: (src.match(/new Function\(/g) ?? []).length,
      functionCtor: (src.match(/Function\((["'`])/g) ?? []).length,
      evalCalls: (src.match(/\beval\(/g) ?? []).length,
      dynamicImport: (src.match(/\bimport\(/g) ?? []).length,
      esmStaticImport: (src.match(/(^|\n)\s*import\s.*from\s/g) ?? []).length,
      esmExport: (src.match(/(^|\n)\s*export\s/g) ?? []).length,
      fetch: (src.match(/\bfetch\(/g) ?? []).length,
      xhr: (src.match(/XMLHttpRequest/g) ?? []).length,
      webSocket: (src.match(/WebSocket/g) ?? []).length,
      cdnUrls: (src.match(/https?:\/\/[a-z0-9.-]*(cdn|unpkg|jsdelivr)[a-z0-9.\/-]*/gi) ?? []).length,
    },
  })
}

const out = {
  measuredAt: 'session',
  note: 'All sizes are custom IIFE tree-shaken builds via esbuild, minified, no sourcemap.',
  toolchain: { node: process.version, esbuild: '0.24.2' },
  versions: {
    echarts: JSON.parse(readFileSync(join(root, 'node_modules/echarts/package.json'), 'utf8')).version,
    cytoscape: JSON.parse(readFileSync(join(root, 'node_modules/cytoscape/package.json'), 'utf8')).version,
  },
  yardsticks: yard,
  plotlyShipped: {
    path: 'src/SpecScribe/assets/plotly-hierarchy.min.js',
    bytes: yard['plotly-hierarchy.min.js'].min,
    gzip: yard['plotly-hierarchy.min.js'].gzip,
    registeredTraceModules: registered,
    scatterRegistered: registered.includes('scatter'),
    marginalCostOfScatterBytes: 0,
  },
  candidates: results,
}
writeFileSync(join(measDir, 'bundles.json'), JSON.stringify(out, null, 2), 'utf8')

console.log(`\nR2 CHECK — shipped plotly-hierarchy.min.js registers: ${registered.join(', ')}`)
console.log(`           scatter registered: ${registered.includes('scatter') ? 'YES → marginal cost 0 B' : 'NO'}`)
console.log(`\nyardstick prism.js = ${PRISM.toLocaleString()} B\n`)
console.log('candidate                    min        gzip     ×prism(min) ×prism(gz)  evalish  fetch  esm')
for (const r of results) {
  const evalish = r.csp.newFunction + r.csp.functionCtor + r.csp.evalCalls
  console.log(
    `${r.candidate.padEnd(24)} ${String(r.min).padStart(9)} ${String(r.gzip).padStart(9)}  ` +
    `${String(r.xPrismMin).padStart(9)} ${String(r.xPrismGzip).padStart(9)}  ` +
    `${String(evalish).padStart(7)} ${String(r.csp.fetch).padStart(6)} ${String(r.csp.esmStaticImport).padStart(4)}`,
  )
}
console.log(`\nwrote measurements/bundles.json`)
