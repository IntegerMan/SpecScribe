#!/usr/bin/env node
// `npm run measure:parity` — Story 23.3 AC #1's oracle.
//
// For every migrated surface, compares the `<main>` region three ways:
//
//   golden -> IR     did the CAPTURE lose anything? (a delta here is Epic 22's, not this story's)
//   IR     -> Nuxt   did `v-html` + the region split survive the trip? (a delta here IS this story's)
//   golden -> Nuxt   the end-to-end claim.
//
// Splitting the comparison is the point. A single golden-vs-Nuxt number cannot tell a migration defect
// apart from an inherited capture defect, and Story 23.1's report had to reason about exactly that
// distinction from prose. It also runs the verbatim containment check — `emitted.includes(irMainInnerHtml)`
// — which is a stronger statement than equality after normalization: the IR's bytes are IN the page.
//
// Run `npm run generate` first; this script measures, it does not build.

import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  assertFullRun,
  excerpt,
  firstDifference,
  MEASUREMENTS_DIR,
  mainRegion,
  normalizeVolatile,
  pad,
  PUBLIC_DIR,
  readOrNull,
} from './harness-lib.mjs'
import { isMigrated, MIGRATED } from './ir-content-lib.mjs'

assertFullRun('measure:parity')

const ir = await import('../ir/adapter.ts')
const goldenRoot = ir.IR_DIR

const FAMILY_LABEL = {
  dashboard: 'index.html',
  epicsIndex: 'epics.html',
  epicDetail: 'epics/epic-{N}.html',
  storyDetail: 'epics/story-{id}.html',
}

const familyOf = (path) => Object.entries(MIGRATED).find(([, test]) => test(path))?.[0]

const surfaces = ir.site.paths.filter(isMigrated)
const rows = []
const deltas = []
let missingOutput = 0

for (const path of surfaces) {
  const page = ir.page(path)
  const goldenHtml = readOrNull(join(goldenRoot, path))
  const nuxtHtml = readOrNull(join(PUBLIC_DIR, path))

  if (!nuxtHtml) {
    missingOutput += 1
    rows.push({ path, family: familyOf(path), status: 'NOT PRERENDERED' })
    continue
  }
  if (!goldenHtml) {
    rows.push({ path, family: familyOf(path), status: 'NO GOLDEN' })
    continue
  }

  const goldenMain = normalizeVolatile(mainRegion(goldenHtml) ?? '')
  const nuxtMain = normalizeVolatile(mainRegion(nuxtHtml) ?? '')
  // The IR's own `<main>`, rebuilt from the split — `<main …>` + body + `</main>`.
  const attrs = page.region.mainAttributes
  const irMain = normalizeVolatile(`<main id="main-content"${attrs}>${page.region.mainInnerHtml}</main>`)

  const verbatim = nuxtHtml.includes(page.region.mainInnerHtml)

  const row = {
    path,
    family: familyOf(path),
    goldenBytes: goldenMain.length,
    irBytes: irMain.length,
    nuxtBytes: nuxtMain.length,
    goldenVsIr: goldenMain === irMain,
    irVsNuxt: irMain === nuxtMain,
    goldenVsNuxt: goldenMain === nuxtMain,
    verbatim,
    status: 'ok',
  }
  rows.push(row)

  if (!row.goldenVsNuxt || !row.verbatim) {
    const at = firstDifference(goldenMain, nuxtMain)
    deltas.push({
      path,
      // Which stage introduced it. This split is the point of measuring three ways: a delta the CAPTURE
      // introduced is Epic 22's and is inherited here; a delta between the IR and the emitted page is this
      // story's and is a failure.
      stage: row.irVsNuxt && verbatim ? 'capture (golden -> IR)' : 'migration (IR -> Nuxt)',
      verbatim,
      at,
      golden: at >= 0 ? excerpt(goldenMain, at) : '',
      nuxt: at >= 0 ? excerpt(nuxtMain, at) : '',
    })
  }
}

// ── Report ─────────────────────────────────────────────────────────────────────────────────────────────

const measured = rows.filter((r) => r.status === 'ok')
const byFamily = new Map()
for (const r of measured) {
  const f = byFamily.get(r.family) ?? { family: r.family, n: 0, gi: 0, irn: 0, gn: 0, verb: 0 }
  f.n += 1
  f.gi += r.goldenVsIr ? 1 : 0
  f.irn += r.irVsNuxt ? 1 : 0
  f.gn += r.goldenVsNuxt ? 1 : 0
  f.verb += r.verbatim ? 1 : 0
  byFamily.set(r.family, f)
}

const lines = []
const say = (s = '') => {
  lines.push(s)
  console.log(s)
}

say('')
say('Story 23.3 AC #1 — <main> region parity, per migrated surface family')
say('')
say(
  pad('family', 26) + pad('pages', 8) + pad('golden=IR', 12) + pad('IR=Nuxt', 12) + pad('golden=Nuxt', 14) + 'verbatim',
)
say('-'.repeat(84))
for (const [family, f] of byFamily) {
  say(
    pad(FAMILY_LABEL[family] ?? family, 26) +
      pad(f.n, 8) +
      pad(`${f.gi}/${f.n}`, 12) +
      pad(`${f.irn}/${f.n}`, 12) +
      pad(`${f.gn}/${f.n}`, 14) +
      `${f.verb}/${f.n}`,
  )
}
const t = measured.reduce(
  (a, r) => ({
    n: a.n + 1,
    gi: a.gi + (r.goldenVsIr ? 1 : 0),
    irn: a.irn + (r.irVsNuxt ? 1 : 0),
    gn: a.gn + (r.goldenVsNuxt ? 1 : 0),
    verb: a.verb + (r.verbatim ? 1 : 0),
  }),
  { n: 0, gi: 0, irn: 0, gn: 0, verb: 0 },
)
say('-'.repeat(84))
say(
  pad('TOTAL', 26) +
    pad(t.n, 8) +
    pad(`${t.gi}/${t.n}`, 12) +
    pad(`${t.irn}/${t.n}`, 12) +
    pad(`${t.gn}/${t.n}`, 14) +
    `${t.verb}/${t.n}`,
)
say('')

if (missingOutput > 0) {
  say(`⚠ ${missingOutput} migrated surface(s) were not found in .output/public — run \`npm run generate\`.`)
  say('')
}

const migrationDeltas = deltas.filter((d) => d.stage.startsWith('migration'))
const captureDeltas = deltas.filter((d) => d.stage.startsWith('capture'))

if (migrationDeltas.length === 0) {
  say('MIGRATION: no deltas. Every migrated surface renders the IR byte-for-byte, and every one of them')
  say('           contains the IR\'s `<main>` body verbatim.')
} else {
  say(`MIGRATION: ${migrationDeltas.length} surface(s) where the emitted page differs from the IR. These are`)
  say('           this story\'s to fix.')
  say('')
  for (const d of migrationDeltas.slice(0, 20)) {
    say(`  ${d.path}`)
    say(`    verbatim: ${d.verbatim}`)
    if (d.at >= 0) {
      say(`    at byte:  ${d.at}`)
      say(`    golden:   ${d.golden}`)
      say(`    nuxt:     ${d.nuxt}`)
    }
    say('')
  }
}
say('')

if (captureDeltas.length > 0) {
  say(`CAPTURE:   ${captureDeltas.length} surface(s) where the IR already differs from the static golden page,`)
  say('           reproduced faithfully here. INHERITED — owned by Epic 22, enumerated below with its cause.')
  say('')
  say('  Root cause, verified in `SiteGenerator.cs`: the dashboard/epics families are RE-RENDERED for the')
  say('  IR (`BuildSpaBundle`, :3044) rather than captured from the static pass\'s own output. The two')
  say('  passes run at different points in the pipeline and therefore see different work inventories:')
  say('')
  say('    · static  — `RenderEpicsPages` :2566 runs BEFORE the pages loop fills `_docs`, so it builds its')
  say('                follow-up inventory straight from source (`ResolveFollowUpWork(files)`) and passes')
  say('                an explicitly parsed deferred model.')
  say('    · IR      — `BuildSpaBundle` :3053 runs AFTER, so `WorkInventory.Build(_docs)` sees MORE items,')
  say('                and the 2-arg `BuildFollowUpGeometry` re-derives the deferred model itself.')
  say('')
  say('  The visible symptom is the per-story work graph: same story, different node and edge counts. Note')
  say('  which side is stale — the IR is the MORE complete render, so this is a latent defect in the static')
  say('  page rather than a loss in the capture.')
  say('')
  for (const d of captureDeltas.slice(0, 8)) {
    say(`  ${d.path}  @${d.at}`)
    say(`    golden: ${d.golden}`)
    say(`    ir/nuxt:${d.nuxt}`)
  }
  if (captureDeltas.length > 8) {
    say(`  … and ${captureDeltas.length - 8} more (all in measurements/parity.json).`)
  }
  say('')
}

say(`Surfaces measured: ${measured.length} of ${surfaces.length} migrated pages in the IR (no sampling).`)
say(`Golden root: ${goldenRoot}`)
say('')

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'parity.txt'), `${lines.join('\n')}\n`, 'utf8')
writeFileSync(
  join(MEASUREMENTS_DIR, 'parity.json'),
  `${JSON.stringify(
    {
      generatedBy: 'web/scripts/measure-parity.mjs',
      totals: t,
      migrationDeltas,
      captureDeltas,
      rows,
    },
    null,
    2,
  )}\n`,
  'utf8',
)
console.log('  wrote measurements/parity.txt + measurements/parity.json')
console.log('')

process.exit(migrationDeltas.length > 0 || missingOutput > 0 ? 1 : 0)
