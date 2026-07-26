#!/usr/bin/env node
// `npm run measure:payload` — AC #4's experiment.
//
// The 23.1 spike measured the Nuxt output at 2.26x the C# site's weight and traced the overhead ENTIRELY to
// hydration payload: anything reaching a component through an async data source is serialized into
// `_payload.json` by construction. It did NOT measure the <NuxtIsland>/server-component alternative, which
// is what this script does — on three routes that render identical markup from identical data and differ
// only in how that data reaches the primitive.
//
// Run `npm run generate` first (this script measures, it does not build), then read the table it prints
// against the recommendation recorded in CONVENTIONS.md.

import { readdirSync, statSync } from 'node:fs'
import { join, posix, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const PUBLIC_DIR = fileURLToPath(new URL('../.output/public', import.meta.url))

const VARIANTS = [
  { route: 'measure/async', label: 'A · useAsyncData' },
  { route: 'measure/island', label: 'B · server component' },
  { route: 'measure/static', label: 'C · static (control)' },
]

let files
try {
  files = walk(PUBLIC_DIR)
} catch (err) {
  if (err.code === 'ENOENT') {
    console.error('measure:payload — .output/public not found. Run `npm run generate` first.')
    process.exit(1)
  }
  throw err
}

// Island responses are emitted to a SHARED directory keyed by component + props hash, not under the route
// that used them. Attributing them by route would silently credit variant B with zero bytes it actually
// ships, so they are collected separately and reported as their own column.
const islandBytes = files
  .filter((f) => f.rel.startsWith('__nuxt_island/'))
  .reduce((sum, f) => sum + f.size, 0)

const rows = VARIANTS.map((v) => {
  const html = sizeOf(files, `${v.route}/index.html`) ?? sizeOf(files, `${v.route}.html`) ?? 0
  const payload =
    (sizeOf(files, `${v.route}/_payload.json`) ?? 0) + (sizeOf(files, `${v.route}/_payload.js`) ?? 0)
  const island = v.route.endsWith('island') ? islandBytes : 0
  return { ...v, html, payload, island, total: html + payload + island }
})

if (rows.every((r) => r.total === 0)) {
  console.error('measure:payload — no measure routes found in .output/public. Did `npm run generate` succeed?')
  process.exit(1)
}

const control = rows.find((r) => r.route === 'measure/static')

console.log('')
console.log('Story 23.2 AC #4 — hydration-payload shape, 200 identical rows per route')
console.log('')
console.log(pad('variant', 24) + pad('html', 12) + pad('payload', 12) + pad('island', 12) + pad('total', 12) + 'vs control')
console.log('-'.repeat(84))
for (const r of rows) {
  const ratio = control && control.total > 0 ? `${(r.total / control.total).toFixed(2)}x` : '—'
  console.log(
    pad(r.label, 24) +
      pad(kb(r.html), 12) +
      pad(kb(r.payload), 12) +
      pad(r.island ? kb(r.island) : '—', 12) +
      pad(kb(r.total), 12) +
      ratio,
  )
}
console.log('')
console.log(`total emitted output: ${kb(files.reduce((s, f) => s + f.size, 0))} across ${files.length} files`)
console.log('')

function walk(dir) {
  const out = []
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    const st = statSync(full)
    if (st.isDirectory()) out.push(...walk(full))
    else out.push({ rel: relative(PUBLIC_DIR, full).split(sep).join(posix.sep), size: st.size })
  }
  return out
}

function sizeOf(all, rel) {
  return all.find((f) => f.rel === rel)?.size
}

function kb(bytes) {
  return bytes === 0 ? '0' : `${(bytes / 1024).toFixed(1)} KB`
}

function pad(s, n) {
  return String(s).padEnd(n)
}
