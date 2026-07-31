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
//
// ⚠️ **SUPERSEDED BY `npm run check:parity` / `npm run pin:parity`. [Story 23.6]**
//
// This harness produced Story 23.4's evidence and it dies with the C# page writer. `goldenRoot` below is
// `ir.IR_DIR` — the directory C# writes the `.html` into — so once Story 23.6 Task 6 deletes the writer,
// `readOrNull(join(goldenRoot, path))` returns null for every page, every row takes the `NO GOLDEN` branch,
// `measured` is EMPTY, `migrationDeltas` is empty, and this script exits **0**. It reports success while
// measuring nothing, which is exactly the vacuous-oracle failure ADR 0033 §Decision 5 forbids.
//
// It is kept only until that deletion lands, as the record of how the oracle was produced. The successor is
// a PINNED corpus, because a live content digest over this repository's own docs cannot distinguish "the
// content moved" from "the renderer moved" — see `scripts/parity-lib.mjs`'s header for the measurement that
// established this (`goldenSha === irSha === nuxtSha` on all 1,469 rows).

import { createHash } from 'node:crypto'
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
import { resolveFamily } from '../ir/families.ts'

assertFullRun('measure:parity')

const ir = await import('../ir/adapter.ts')
const goldenRoot = ir.IR_DIR

/**
 * Story 23.4 widened this harness from Story 23.3's four migrated families to the WHOLE site. The family
 * labels are now the `IrFamily` union from `ir/families.ts` — the same classifier the router uses — so the
 * parity table and the rendered `data-ir-family` attribute can never disagree about what a family is.
 */
const FAMILY_LABEL = {
  dashboard: 'index.html',
  'epics-index': 'epics.html',
  'epic-detail': 'epics/epic-{N}.html',
  'story-detail': 'epics/story-{id}.html',
  'doc-prose': 'adrs|*-artifacts|specs|readme',
  requirement: 'requirements[/{id}].html',
  'follow-up': 'follow-ups/**|action-items',
  'commit-detail': 'commit/{hash}.html',
  'commit-day': 'commits/{date}.html|timeline',
  'code-file': 'code/**',
  insight: 'chart singletons (8)',
  'portal-meta': 'about|how-to-read|design-system',
  sprint: 'sprint.html',
  retro: 'retros[/{slug}].html',
  'pass-through': '⚠ UNMIGRATED',
}

const familyOf = (path) => resolveFamily(path, ir.site.entry)

/**
 * ⚠️ **The committed ORACLE, and the reason it is a hash and not a byte count. [Story 23.4 Task 5]**
 *
 * After this story retires the C# page writer there is **no golden side left to generate** — the comparison
 * this whole harness rests on becomes unrepeatable the moment the writer goes. So the golden region's digest
 * is recorded per page and `measurements/parity.json` is COMMITTED, making it the durable oracle a future run
 * can still check the IR and the emitted page against.
 *
 * A byte LENGTH would not do: the failure this has to survive is a rewrite that preserves length while
 * changing content, which is exactly what a markup or escaping change looks like. Length is a weak digest;
 * sha256 over the NORMALIZED region (wall clock, asset cache-bust and version token already neutralized, so
 * the hash is stable across runs) is a real one.
 */
const sha = (s) => createHash('sha256').update(s, 'utf8').digest('hex').slice(0, 16)

/**
 * EVERY IR page, not the migrated subset — AC #1 requires the table to cover all of them with no sampling.
 * `ir.site.paths` is the manifest's own key set, so this cannot silently miss a family the way a hand-kept
 * list would.
 */
const surfaces = ir.site.paths
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
    // The committed oracle — see `sha` above. Recorded for all three sides so a future run can tell WHICH
    // side moved once the golden site can no longer be regenerated.
    goldenSha: sha(goldenMain),
    irSha: sha(irMain),
    nuxtSha: sha(nuxtMain),
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
say('Story 23.4 AC #1 — <main> region parity, per surface family, WHOLE SITE')
say('')
say(
  pad('family', 36) + pad('pages', 8) + pad('golden=IR', 12) + pad('IR=Nuxt', 12) + pad('golden=Nuxt', 14) + 'verbatim',
)
say('-'.repeat(94))
for (const [family, f] of [...byFamily].sort((a, b) => b[1].n - a[1].n)) {
  say(
    pad(FAMILY_LABEL[family] ?? family, 36) +
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
say("-".repeat(94))
say(
  pad('TOTAL', 36) +
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
