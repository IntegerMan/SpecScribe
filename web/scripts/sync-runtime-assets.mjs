#!/usr/bin/env node
// `npm run sync:assets` (and `npm run check:assets`) — the runtime-asset copy. [Story 23.3 AC #7, Task 7]
//
// The Nuxt app serves the portal's OWN client bundles, byte-for-byte. It does not reimplement them:
// ADR 0012 §Decision 2 makes "one Hierarchy Explorer component is the only route to a sunburst or treemap"
// an invariant, and a fork of `specscribe.js` living in `web/` would be a second implementation of exactly
// the thing that ADR exists to prevent — the one that already happened three times (ADR 0010 §6 asked for a
// single shared engine and got three arc renderers instead).
//
// COPY, never fork. Epic 20 is mid-flight around these very files (20.5 in review, 20.6/20.7/20.9 open, and
// 20.7 deletes the legacy arc renderers), so they WILL move under this app. A copy re-runs; a fork rots.
//
//   node scripts/sync-runtime-assets.mjs            copy
//   node scripts/sync-runtime-assets.mjs --check    verify the copy is current, exit non-zero on drift
//
// `web/public/` is gitignored: these are 1.4 MB of generated/vendored bytes with a single authoritative
// source in the same repo, so committing a second copy would add weight without adding a guarantee. The
// drift gate compares against the SOURCE on disk, which is the thing that must not diverge.

import { createHash } from 'node:crypto'
import { copyFileSync, mkdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const SOURCE_DIR = fileURLToPath(new URL('../../src/SpecScribe/assets/', import.meta.url))
const PUBLIC_DIR = fileURLToPath(new URL('../public/', import.meta.url))

/**
 * What the app serves, and why.
 *
 * Note what is NOT here: `specscribe.css`. Serving the 7,041-line monolith would reverse 23.2's central
 * decision in one line. The injected markup is styled by the generated, bounded `assets/ir-content.css`
 * instead — see `extract-ir-content.mjs`.
 */
const ASSETS = [
  ['specscribe.js', 'the portal client: initHierarchyExplorers + the specscribe:content-swapped seam'],
  ['plotly-hierarchy.min.js', 'ADR 0012\'s charting engine, vendored'],
  ['prism.css', 'syntax highlighting, on pages whose region carries highlighted code'],
  ['prism.js', 'syntax highlighting, on pages whose region carries highlighted code'],
]

const check = process.argv.includes('--check')
const sha = (file) => createHash('sha256').update(readFileSync(file)).digest('hex').slice(0, 12)

mkdirSync(PUBLIC_DIR, { recursive: true })

const drift = []
let copied = 0

for (const [name, why] of ASSETS) {
  const src = SOURCE_DIR + name
  const dst = PUBLIC_DIR + name

  let sourceHash
  try {
    sourceHash = sha(src)
  } catch (err) {
    if (err.code === 'ENOENT') {
      console.error(`sync:assets FAILED — source asset missing: src/SpecScribe/assets/${name} (${why})`)
      process.exit(1)
    }
    throw err
  }

  let destHash = null
  try {
    destHash = sha(dst)
  } catch (err) {
    if (err.code !== 'ENOENT') throw err
  }

  if (destHash === sourceHash) continue

  if (check) {
    drift.push({ name, sourceHash, destHash })
    continue
  }
  copyFileSync(src, dst)
  copied += 1
}

if (check) {
  if (drift.length === 0) {
    console.log(`check:assets OK — ${ASSETS.length} runtime assets in sync with src/SpecScribe/assets/`)
    process.exit(0)
  }
  console.error('check:assets FAILED — web/public/ has drifted from src/SpecScribe/assets/.')
  for (const d of drift) {
    console.error(
      d.destHash === null
        ? `  missing: ${d.name}`
        : `  stale:   ${d.name} (${d.destHash} here, ${d.sourceHash} at source)`,
    )
  }
  console.error('  Fix: run `npm run sync:assets`. Never hand-edit web/public/ — it is a copy by contract.')
  process.exit(1)
}

console.log(
  copied === 0
    ? `sync:assets — up to date (${ASSETS.length} assets in web/public/)`
    : `sync:assets — copied ${copied} of ${ASSETS.length} asset(s) into web/public/`,
)
