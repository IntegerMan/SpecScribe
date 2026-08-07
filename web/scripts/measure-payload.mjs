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

import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, posix, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

import { assertFullRun } from './harness-lib.mjs'

// This harness was the ONLY one that did not make this call, while README.md promised "every harness
// hard-fails when it is set". The gap was reachable, not theoretical: `nuxt.config.ts` appends `/measure/*`
// to the prerender list AFTER the truncated `prerenderIrRoutes`, so the three measure routes survive
// `SPECSCRIBE_IR_ROUTE_LIMIT=5` — a truncated run produced a complete-looking table and COMMITTED it.
// [Story 23.2 review 2026-08-07]
assertFullRun('measure:payload')

const PUBLIC_DIR = fileURLToPath(new URL('../.output/public', import.meta.url))
const MEASUREMENTS_DIR = fileURLToPath(new URL('../measurements', import.meta.url))

/**
 * What this table does NOT measure. Printed AND committed, because the numbers are quoted in CONVENTIONS.md
 * as a standing recommendation for Story 23.3 and the 2026-07-28 re-review found the recommendation resting
 * on something the metric cannot see.
 */
const CAVEATS = [
  'CAVEATS — read before quoting these numbers:',
  '  · total = html + payload + island. `_nuxt/` CLIENT BUNDLE bytes are NOT counted.',
  '  · Variant C therefore measures "no data had to cross the boundary", NOT "build-time data is free":',
  '    its rows come from a deterministic generator (utils/measure-rows.ts) that is bundled into `_nuxt/`',
  '    and RE-RUN in the browser on hydration. Real IR content is not a generator, so C is a floor, not a',
  '    transferable recipe — 23.3 needed a #ir build-time resolver plus `noScripts: true` to get there.',
  '  · Island JSON dedupes across routes sharing a component+props hash; that does not help per-page content.',
]

// `islandComponents` names the `.server.vue` components a variant actually renders. Island responses live in
// a SHARED directory keyed by component + props hash, so attribution has to be declared — the previous
// `route.endsWith('island')` string test charged EVERY file under `__nuxt_island/` to variant B, meaning one
// unrelated `.server.vue` added anywhere in web/ would move the published 1.99x without variant B changing.
// `expectsPayload` records that the route keeps the Nuxt runtime (`noScripts: false` in nuxt.config.ts) and
// must therefore emit a payload file. Stated per variant rather than assumed, so that if a route is ever
// moved under `noScripts: true` the expectation is updated deliberately instead of silently reading as 0.
const VARIANTS = [
  { route: 'measure/async', label: 'A · useAsyncData', islandComponents: [], expectsPayload: true },
  { route: 'measure/island', label: 'B · server component', islandComponents: ['MeasureRows'], expectsPayload: true },
  { route: 'measure/static', label: 'C · static (control)', islandComponents: [], expectsPayload: true },
]

// The ENOENT test is scoped to the ROOT stat, deliberately. `walk` recurses, so wrapping the whole traversal
// in one `try` meant a broken symlink or a file removed mid-walk was reported as ".output/public not found"
// — sending the operator to regenerate a directory that exists and is complete. [Story 23.2 review 2026-08-07]
try {
  statSync(PUBLIC_DIR)
} catch (err) {
  if (err.code === 'ENOENT') {
    console.error('measure:payload — .output/public not found. Run `npm run generate` first.')
    process.exit(1)
  }
  throw err
}
const files = walk(PUBLIC_DIR)

// Island responses are emitted to a SHARED directory keyed by component + props hash, not under the route
// that used them — `__nuxt_island/<Component>_<hash>.json`. Attribute each file to the variant that declares
// the component, so the column reports bytes that variant actually ships and nothing else.
const islandFiles = files.filter((f) => f.rel.startsWith('__nuxt_island/'))

function islandBytesFor(components) {
  return islandFiles
    .filter((f) => components.some((c) => f.rel.slice('__nuxt_island/'.length).startsWith(`${c}_`)))
    .reduce((sum, f) => sum + f.size, 0)
}

// Anything under __nuxt_island/ that no variant claims is reported rather than folded into a variant's total.
// Silence here is how a shared directory turns into a wrong ratio.
const claimed = new Set(
  VARIANTS.flatMap((v) =>
    islandFiles
      .filter((f) => v.islandComponents.some((c) => f.rel.slice('__nuxt_island/'.length).startsWith(`${c}_`)))
      .map((f) => f.rel),
  ),
)
const unattributedIslands = islandFiles.filter((f) => !claimed.has(f.rel))

const rows = VARIANTS.map((v) => {
  const htmlSize = sizeOf(files, `${v.route}/index.html`) ?? sizeOf(files, `${v.route}.html`)
  const payloadJson = sizeOf(files, `${v.route}/_payload.json`)
  const payloadJs = sizeOf(files, `${v.route}/_payload.js`)
  const payload = (payloadJson ?? 0) + (payloadJs ?? 0)
  const island = islandBytesFor(v.islandComponents)
  const html = htmlSize ?? 0
  // `missing` is tracked SEPARATELY from a zero size. Every lookup used to end `?? 0` with the only guard
  // being "did EVERY row come back zero", so one route failing to prerender printed a plausible `0.00x` for
  // it — reading as "this shape ships nothing", the exact inversion of what the table is used to conclude.
  //
  // [Story 23.2 review 2026-08-07] The same reasoning now covers the PAYLOAD column — the column the whole
  // experiment is about. A route that keeps its runtime must emit a payload file; if it does not, the tool
  // has lost track of the filename (a Nuxt change) or the route silently inherited `noScripts: true` from
  // the `'/**'` rule, which sits one line from `/measure/**` in nuxt.config.ts. Either way `payload: 0` is a
  // measurement failure, not a result of zero — and left unguarded it prints variant A at 1.00x.
  const payloadMissing = v.expectsPayload && payloadJson === undefined && payloadJs === undefined
  // A variant that DECLARES island components must resolve bytes for them. `startsWith(`${c}_`)` is a
  // filename-shape assumption about Nuxt's `__nuxt_island/` output; if that shape changes, the column reads
  // `—`, the total is understated and the ratio moves, with a zero exit code.
  const islandMissing = v.islandComponents.length > 0 && island === 0
  return {
    ...v,
    html,
    payload,
    island,
    total: html + payload + island,
    missing: htmlSize === undefined,
    payloadMissing,
    islandMissing,
  }
})

// A measurement with a hole in it is not a measurement. These numbers are quoted in CONVENTIONS.md as a
// standing architectural recommendation, so a partial run must fail loudly rather than publish a short table.
const missing = rows.filter((r) => r.missing)
if (missing.length > 0) {
  console.error('measure:payload FAILED — these measure routes are absent from .output/public:')
  for (const r of missing) console.error(`  - ${r.route}  (${r.label})`)
  console.error('  Run `npm run generate` and check it prerendered every /measure/* route before measuring.')
  process.exit(1)
}

const noPayload = rows.filter((r) => r.payloadMissing)
if (noPayload.length > 0) {
  console.error('measure:payload FAILED — these routes emitted no `_payload.json`/`_payload.js`:')
  for (const r of noPayload) console.error(`  - ${r.route}  (${r.label})`)
  console.error('  A hydrating route must carry a payload. Either the route inherited `noScripts: true`')
  console.error('  from the `/**` route rule, or Nuxt changed the payload filename this tool looks for.')
  console.error('  Reporting 0 here would print this shape as costing nothing, inverting AC #4.')
  process.exit(1)
}

const noIslands = rows.filter((r) => r.islandMissing)
if (noIslands.length > 0) {
  console.error('measure:payload FAILED — these variants declare island components but matched no files:')
  for (const r of noIslands) console.error(`  - ${r.route}  declares ${r.islandComponents.join(', ')}`)
  console.error(`  Looked under __nuxt_island/ for a name starting "<Component>_". Found ${islandFiles.length}`)
  console.error('  island file(s) total. If Nuxt changed that filename shape, fix `islandBytesFor` — do not')
  console.error('  publish a table whose island column is silently zero.')
  process.exit(1)
}

const control = rows.find((r) => r.route === 'measure/static')

/** Every line goes to stdout AND into the committed transcript, so the two can never disagree. */
const transcript = []
function say(line = '') {
  transcript.push(line)
  console.log(line)
}

say('')
say('Story 23.2 AC #4 — hydration-payload shape, 200 identical rows per route')
say('')
say(pad('variant', 24) + pad('html', 12) + pad('payload', 12) + pad('island', 12) + pad('total', 12) + 'vs control')
say('-'.repeat(84))
for (const r of rows) {
  const ratio = control && control.total > 0 ? `${(r.total / control.total).toFixed(2)}x` : '—'
  say(
    pad(r.label, 24) +
      pad(kb(r.html), 12) +
      pad(kb(r.payload), 12) +
      pad(r.island ? kb(r.island) : '—', 12) +
      pad(kb(r.total), 12) +
      ratio,
  )
}
say('')
if (unattributedIslands.length > 0) {
  say(
    `note: ${unattributedIslands.length} file(s) under __nuxt_island/ are claimed by no variant and are ` +
      `excluded from every total: ${unattributedIslands.map((f) => f.rel).join(', ')}`,
  )
}
say(`total emitted output: ${kb(files.reduce((s, f) => s + f.size, 0))} across ${files.length} files`)
say('')
say(`runtime: nuxt ${readPkgVersion('nuxt')} / vue ${readPkgVersion('vue')} / node ${process.versions.node}`)
say('')
for (const line of CAVEATS) say(line)
say('')

// The record is COMMITTED, like every other harness under measurements/. `web/README.md` promises it and
// `web/.gitignore` explains why ("Story 23.1 claimed reproducible numbers and it was not true at review
// time"). This script was the one harness that printed to stdout and wrote nothing, so its numbers were the
// ones nobody could check — which is how CONVENTIONS.md ended up pinning a table to a Nuxt version the app
// had since moved off. [Story 23.2 re-review 2026-07-28]
const record = {
  story: '23.2',
  ac: 4,
  runtime: {
    nuxt: readPkgVersion('nuxt'),
    vue: readPkgVersion('vue'),
    node: process.versions.node,
  },
  caveats: CAVEATS,
  control: control?.route ?? null,
  variants: rows.map((r) => ({
    route: r.route,
    label: r.label,
    htmlBytes: r.html,
    payloadBytes: r.payload,
    islandBytes: r.island,
    totalBytes: r.total,
    vsControl: control && control.total > 0 ? Number((r.total / control.total).toFixed(2)) : null,
  })),
  unattributedIslands: unattributedIslands.map((f) => ({ file: f.rel, bytes: f.size })),
}

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'payload.json'), `${JSON.stringify(record, null, 2)}\n`, 'utf8')
writeFileSync(join(MEASUREMENTS_DIR, 'payload.txt'), `${transcript.join('\n')}\n`, 'utf8')
console.log(`recorded: measurements/payload.json, measurements/payload.txt`)
console.log('')

function readPkgVersion(name) {
  try {
    return JSON.parse(readFileSync(new URL(`../node_modules/${name}/package.json`, import.meta.url), 'utf8'))
      .version
  } catch {
    return 'unknown'
  }
}

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
