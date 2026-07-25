// Story 20.4 — AC #1 bundle sizing + the CSP-relevant STATIC evidence (Task 2).
//
//   node scripts/measure-bundle.mjs
//
// Static analysis only. It answers "what does the artifact contain" — NOT "does the browser accept it".
// The live verdict is Task 6's browser run; this script exists so that verdict has something to explain.

import { readFileSync, statSync, existsSync, writeFileSync, mkdirSync } from 'node:fs'
import { join, dirname, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { gzipSync, brotliCompressSync } from 'node:zlib'

const here = dirname(fileURLToPath(import.meta.url))
const spikeRoot = join(here, '..')
const dist = join(spikeRoot, 'plotly-src', 'dist')
const prism = join(spikeRoot, '..', '..', 'src', 'SpecScribe', 'assets', 'prism.js')
const specscribeJs = join(spikeRoot, '..', '..', 'src', 'SpecScribe', 'assets', 'specscribe.js')

const BUNDLES = [
  ['plotly-specscribe-hierarchy.min.js', 'custom: scatter+sunburst+treemap+heatmap (standard)'],
  ['plotly-specscribe-hierarchy-strict.min.js', 'custom: scatter+sunburst+treemap+heatmap (--strict)'],
  ['plotly-specscribe-hier2-strict.min.js', 'custom: scatter+sunburst+treemap (--strict) — heatmap dropped'],
  ['plotly.min.js', 'upstream FULL bundle (reference)'],
  ['plotly-strict.min.js', 'upstream FULL strict bundle (reference)'],
]

// The CSP-relevant constructs. `new Function(` / `eval(` are what force script-src 'unsafe-eval'.
// Counted with word boundaries so `Function.prototype` and `.evaluate(` do not inflate the count.
const PROBES = {
  "new Function(": /\bnew Function\s*\(/g,
  "Function('...')": /(?<![.\w])Function\s*\(\s*['"`]/g,
  'eval(': /(?<![.\w$])eval\s*\(/g,
  'setTimeout("str")': /setTimeout\s*\(\s*['"`]/g,
  'import(': /(?<![.\w$])import\s*\(/g,
  'ESM static import': /(?:^|[;\n])\s*import\s[^(]/g,
  'export ': /(?:^|[;\n])\s*export\s/g,
  'fetch(': /(?<![.\w$])fetch\s*\(/g,
  'XMLHttpRequest': /XMLHttpRequest/g,
  'WebSocket': /\bWebSocket\b/g,
  'navigator.sendBeacon': /sendBeacon/g,
  'document.createElement("style")': /createElement\s*\(\s*['"]style['"]\s*\)/g,
  'insertRule(': /\binsertRule\s*\(/g,
  'cdn./plot.ly URL': /https?:\/\/[^"'`\s]*(?:cdn\.plot|plot\.ly|plotly\.com)/g,
}

const b = (n) => n.toLocaleString()
const rows = []

const PRISM_BYTES = existsSync(prism) ? statSync(prism).size : null
const SPECSCRIBE_JS_BYTES = existsSync(specscribeJs) ? statSync(specscribeJs).size : null

console.log('='.repeat(120))
console.log('STORY 20.4 — Plotly custom bundle sizing (plotly.js 3.7.0, MIT)')
console.log('='.repeat(120))
console.log(`in-repo yardstick: src/SpecScribe/assets/prism.js = ${b(PRISM_BYTES ?? 0)} B   specscribe.js = ${b(SPECSCRIBE_JS_BYTES ?? 0)} B\n`)
console.log(['artifact', 'min', 'min+gzip', 'min+br', 'xPrism(min)', 'xPrism(gz)'].map((h, i) => h.padEnd([46, 12, 12, 12, 13, 12][i])).join(''))

for (const [file, label] of BUNDLES) {
  const p = join(dist, file)
  if (!existsSync(p)) { console.log(`${file.padEnd(46)}(not built)`); continue }
  const src = readFileSync(p)
  const gz = gzipSync(src, { level: 9 }).length
  const br = brotliCompressSync(src).length
  rows.push({ file, label, min: src.length, gzip: gz, brotli: br,
    xPrismMin: PRISM_BYTES ? +(src.length / PRISM_BYTES).toFixed(2) : null,
    xPrismGz: PRISM_BYTES ? +(gz / PRISM_BYTES).toFixed(2) : null })
  const r = rows.at(-1)
  console.log([
    file.padEnd(46), b(r.min).padEnd(12), b(r.gzip).padEnd(12), b(r.brotli).padEnd(12),
    `${r.xPrismMin}x`.padEnd(13), `${r.xPrismGz}x`.padEnd(12),
  ].join(''))
  console.log(`  ${''.padEnd(2)}${label}`)
}

console.log('\n' + '='.repeat(120))
console.log('CSP-relevant construct inventory (STATIC — the live verdict is the Task 6 browser run)')
console.log('='.repeat(120))
const probeRows = {}
for (const [file] of BUNDLES) {
  const p = join(dist, file)
  if (!existsSync(p)) continue
  const src = readFileSync(p, 'utf8')
  const counts = {}
  for (const [name, re] of Object.entries(PROBES)) counts[name] = (src.match(re) || []).length
  probeRows[file] = counts
}
const names = Object.keys(PROBES)
const files = Object.keys(probeRows)
console.log('construct'.padEnd(34) + files.map((f) => basename(f, '.min.js').replace('plotly-', '').padEnd(30)).join(''))
for (const n of names) {
  console.log(n.padEnd(34) + files.map((f) => String(probeRows[f][n]).padEnd(30)).join(''))
}

// The trace list each bundle ACTUALLY registers, read out of the generated lib index rather than assumed.
console.log('\n' + '='.repeat(120))
console.log('Registered trace list (read from the generated lib/index-*.js, not assumed)')
console.log('='.repeat(120))
for (const idx of ['index-specscribe-hierarchy.js', 'index-strict-specscribe-hierarchy-strict.js', 'index-strict-specscribe-hier2-strict.js']) {
  const p = join(spikeRoot, 'plotly-src', 'lib', idx)
  if (!existsSync(p)) continue
  const src = readFileSync(p, 'utf8')
  const traces = [...src.matchAll(/require\('\.\/([a-z0-9-]+)'\)/g)].map((m) => m[1])
  console.log(`  ${idx.padEnd(46)} ${traces.join(', ')}`)
}

mkdirSync(join(spikeRoot, 'measurements'), { recursive: true })
writeFileSync(join(spikeRoot, 'measurements', 'bundle.json'),
  JSON.stringify({ plotlyVersion: '3.7.0', license: 'MIT', prismBytes: PRISM_BYTES, specscribeJsBytes: SPECSCRIBE_JS_BYTES, rows, probeRows }, null, 2))
console.log('\nwrote measurements/bundle.json')
