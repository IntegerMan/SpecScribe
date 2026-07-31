#!/usr/bin/env node
// `npm run check:parity` — Story 23.6 AC #3's content-drift gate, and the replacement for
// `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`.
//
// ── Why this script exists ──────────────────────────────────────────────────────────────────────────────
//
// Story 23.6 deletes the C# page writer. That takes `GoldenContentFingerprint`'s subject with it AND makes
// `measure:parity` itself vacuous — its `goldenRoot` is the directory C# used to write `.html` into, so
// afterwards every row takes the `NO GOLDEN` branch, `measured` is empty, and the script exits 0. A harness
// reporting success while measuring nothing is precisely the failure ADR 0033 §Decision 5 names.
//
// This is the replacement. It renders a PINNED corpus and compares two digests per route against a committed
// oracle. Read `parity-lib.mjs`'s header for why the corpus is pinned and why there are two digests; the
// short version is that over this repository's own docs a live content digest cannot tell "the content moved"
// from "the renderer moved", and the old oracle hashed `<main>` only, so it was blind to exactly the chrome
// this story deletes.
//
// ── What it asserts ─────────────────────────────────────────────────────────────────────────────────────
//
//   mainSha   the `<main>` region still hashes to what C# produced (Story 23.4's proven lineage)
//   pageSha   the WHOLE PAGE still hashes to the pinned renderer snapshot — the gate over `<title>`, meta,
//             the favicon, the footer, `<script src>` tags, the nav toggle, the Mermaid init and the
//             Hierarchy/Graph anti-flash handshakes
//
// The corpus is frozen, so a failure is ALWAYS a rendering change. There is no "regenerate because content
// moved" path, which is the whole point: this gate cannot be red for a reason unrelated to the change under
// test (ADR 0033 §Decision 2).
//
// ── Loudness (ADR 0033 §Decision 5) ─────────────────────────────────────────────────────────────────────
//
// Modelled on `RegionCompositionCorpusProof`, which the ADR names as the reference: it asserts the deep-git
// surfaces exist BEFORE trusting a delta count, so a partial run cannot report a vacuous "0 deltas". Here,
// before any result is trusted: the oracle must exist, parse, and carry routes with BOTH digests; every
// pinned route must render; and every family the oracle claims must still be covered by a measured route.
//
// ── Regeneration ────────────────────────────────────────────────────────────────────────────────────────
//
//   npm run pin:parity     a command producing a reviewable per-route diff, never a hex-literal bump
//
// Usage:
//   npm run check:parity
//
// Requires a `build:package` artefact at `web/.output` (asserted, with the fix named). Zero npm dependencies.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { mainRegion, normalizeVolatile, pad, readOrNull } from './harness-lib.mjs'
import { assessRun, classifyRoute, parityDigest, ParityOracleError, validateOracle } from './parity-lib.mjs'
import { withRenderer } from './render-lib.mjs'

const CORPUS = resolve(process.cwd(), 'fixtures', 'parity-corpus')
const ORACLE = resolve(process.cwd(), 'measurements', 'parity-pinned.json')
const ARTEFACT = resolve(process.cwd(), '.output')

// ── Loudness gate A: the oracle itself ──────────────────────────────────────────────────────────────────

const raw = readOrNull(ORACLE)
if (raw === null) {
  console.error('')
  console.error(`check:parity — the committed oracle is ABSENT at ${ORACLE}.`)
  console.error('  This gate has no comparison basis, which is a hard failure and not a pass (ADR 0033 §5).')
  console.error('  Regenerate it with:  npm run pin:parity')
  console.error('')
  process.exit(1)
}

let oracle
let routes
let families
try {
  oracle = JSON.parse(raw)
  ;({ routes, families } = validateOracle(oracle, 'measurements/parity-pinned.json'))
} catch (err) {
  console.error('')
  console.error(`check:parity — ${err instanceof ParityOracleError ? err.message : `the oracle is unreadable: ${err.message}`}`)
  console.error('  Regenerate it with:  npm run pin:parity')
  console.error('')
  process.exit(1)
}

// ── Render the pinned corpus ────────────────────────────────────────────────────────────────────────────

let verdicts
try {
  verdicts = await withRenderer({ outputDir: ARTEFACT, irDir: CORPUS, port: 3321 }, async (fetchRoute) => {
    const out = []
    for (const route of routes) {
      let status
      let html
      try {
        ;({ status, html } = await fetchRoute(route.path))
      } catch (err) {
        out.push(classifyRoute(route, { unmeasurable: `request failed: ${err.message}` }))
        continue
      }
      if (status !== 200) {
        out.push(classifyRoute(route, { unmeasurable: `HTTP ${status}` }))
        continue
      }
      const main = mainRegion(html)
      if (main === null) {
        out.push(classifyRoute(route, { unmeasurable: 'no <main id="main-content"> landmark — an empty shell' }))
        continue
      }
      out.push(
        classifyRoute(route, {
          mainSha: parityDigest(normalizeVolatile(main)),
          pageSha: parityDigest(normalizeVolatile(html)),
        }),
      )
    }
    return out
  })
} catch (err) {
  console.error('')
  console.error(`check:parity — the renderer could not be driven:\n  ${err.message}`)
  console.error('')
  process.exit(1)
}

// ── Report ──────────────────────────────────────────────────────────────────────────────────────────────

const a = assessRun(verdicts, families)

console.log('')
console.log('Story 23.6 AC #3 — check:parity, the pinned content-drift gate')
console.log('')
console.log(`  corpus:   ${CORPUS}  (frozen — any move here is a RENDERING change)`)
console.log(`  oracle:   ${ORACLE}  (lineage: ${oracle.lineage})`)
console.log(`  artefact: ${ARTEFACT}`)
console.log('')
console.log(`  ${pad('routes pinned', 22)}${a.pinned}`)
console.log(`  ${pad('rendered', 22)}${a.measured}`)
console.log(`  ${pad('families covered', 22)}${families.length - a.missingFamilies.length} of ${families.length}`)
console.log('')

if (a.unmeasurable.length > 0) {
  console.error(`✗ ${a.unmeasurable.length} pinned route(s) did not render. The gate refuses to report a result`)
  console.error('  over a shrunken basis (ADR 0033 §5).')
  console.error('')
  for (const u of a.unmeasurable.slice(0, 20)) console.error(`    ${u.path}  [${u.family}]\n      ${u.why}`)
  if (a.unmeasurable.length > 20) console.error(`    … and ${a.unmeasurable.length - 20} more`)
  console.error('')
}

if (a.mainDrift.length > 0) {
  console.error(`✗ REGION DRIFT — ${a.mainDrift.length} route(s) whose <main> no longer hashes to what C# produced.`)
  console.error('  The corpus is frozen, so the input did not move: this is a rendering change in the region.')
  console.error('')
  for (const d of a.mainDrift.slice(0, 25)) {
    console.error(`    ${d.path}  [${d.family}]`)
    console.error(`      expected ${d.expected}  ->  rendered ${d.actual}`)
  }
  if (a.mainDrift.length > 25) console.error(`    … and ${a.mainDrift.length - 25} more`)
  console.error('')
}

if (a.chromeDrift.length > 0) {
  console.error(`✗ CHROME DRIFT — ${a.chromeDrift.length} route(s) whose <main> is correct but whose PAGE moved.`)
  console.error('  The difference is outside the region: <title>, meta, favicon, footer, <script src>, the nav')
  console.error('  toggle, the Mermaid init, or a Hierarchy/Graph anti-flash handshake.')
  console.error('')
  for (const d of a.chromeDrift.slice(0, 25)) {
    console.error(`    ${d.path}  [${d.family}]`)
    console.error(`      expected ${d.expected}  ->  rendered ${d.actual}`)
  }
  if (a.chromeDrift.length > 25) console.error(`    … and ${a.chromeDrift.length - 25} more`)
  console.error('')
}

if (a.missingFamilies.length > 0) {
  console.error(`✗ ${a.missingFamilies.length} surface family(-ies) the oracle claims are no longer covered by any`)
  console.error('  measured route — the remaining routes would still report "0 drift", which is a partial run')
  console.error('  wearing a green tick.')
  console.error('')
  for (const f of a.missingFamilies) console.error(`    ${f}`)
  console.error('')
}

if (!a.ok) {
  console.error('  If the change is intended, re-pin and review the per-route diff:')
  console.error('    npm run pin:parity')
  console.error('')
  console.error('check:parity FAILED')
  console.error('')
  process.exit(1)
}

console.log(`✓ ${a.measured} pinned route(s) across ${families.length} families render byte-identically:`)
console.log('  the <main> region still matches the C# lineage, and the whole page still matches the pinned')
console.log('  renderer snapshot (chrome included).')
console.log('')
