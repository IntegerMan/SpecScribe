#!/usr/bin/env node
// `npm run check:ir-content` — the drift gate for the IR-content stylesheet layer. [Story 23.3 AC #6]
//
// The sibling of `check:tokens`, and it exists for the same reason: a generated file that nothing verifies
// is a hand-editable file with a comment on top asking nicely. This re-derives both artifacts from the C#
// stylesheet and the IR, and exits non-zero on ANY divergence — a stale extraction, a hand-edited rule, or
// a manifest that no longer matches the sheet beside it.
//
// Proven in BOTH directions during Story 23.3 (see the story record): observed RED before extraction and
// RED on a hand-edited rule, not only green. A gate only ever seen passing is not a gate.

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

import { buildIrContentCss } from './ir-content-build.mjs'
import {
  OUT_CSS,
  OUT_MANIFEST,
  OUT_RUNTIME_CSS,
  OUT_SHARED_CSS,
  readCommitted,
  SOURCE_LABEL,
} from './ir-content-lib.mjs'

let expected
try {
  expected = await buildIrContentCss()
} catch (err) {
  console.error(`check:ir-content FAILED — could not re-derive the layer from ${SOURCE_LABEL}`)
  console.error(`  ${err.message}`)
  process.exit(1)
}

const actualCss = readCommitted(OUT_CSS)
const actualManifest = readCommitted(OUT_MANIFEST)
// The unscoped sibling is gated by the SAME run, not a second script: a shared primitive that drifted while
// only the scoped layer was checked would be invisible, and it is the layer template-authored components
// actually bind to. [ADR 0029]
const actualSharedCss = readCommitted(OUT_SHARED_CSS)
// The runtime-body layer is gated by the same run for the same reason, and it needs it MORE than its
// siblings: nothing else on the page styles the tooltip, so a drop here is invisible until a human hovers a
// chart sector. That is exactly how it shipped unstyled in the first place. [ADR 0039]
const actualRuntimeCss = readCommitted(OUT_RUNTIME_CSS)

if (actualCss === null || actualManifest === null || actualSharedCss === null || actualRuntimeCss === null) {
  const missing = [
    actualCss === null ? 'web/assets/ir-content.css' : null,
    actualSharedCss === null ? 'web/assets/shared-primitives.css' : null,
    actualRuntimeCss === null ? 'web/assets/runtime-body.css' : null,
    actualManifest === null ? 'web/assets/ir-content.manifest.json' : null,
  ].filter(Boolean)
  console.error('check:ir-content FAILED — the generated layer does not exist.')
  console.error(`  missing: ${missing.join(', ')}`)
  console.error('  Run `npm run extract:ir-content` to generate it.')
  process.exit(1)
}

const expectedCss = expected.css.replace(/\r\n/g, '\n')
const expectedSharedCss = expected.sharedCss.replace(/\r\n/g, '\n')
const expectedRuntimeCss = expected.runtimeCss.replace(/\r\n/g, '\n')
const expectedManifest = `${JSON.stringify(expected.manifest, null, 2)}\n`.replace(/\r\n/g, '\n')

if (
  actualCss === expectedCss &&
  actualSharedCss === expectedSharedCss &&
  actualRuntimeCss === expectedRuntimeCss &&
  actualManifest === expectedManifest
) {
  assertSheetsAreActuallyImported()
  console.log(
    `check:ir-content OK — ${expected.stats.carriedRules} rules + ${expected.stats.carriedKeyframes} ` +
      `keyframes scoped, ${expected.stats.sharedRules} shared primitive rule(s) and ` +
      `${expected.stats.runtimeRules} runtime body rule(s) unscoped, in sync with ` +
      SOURCE_LABEL,
  )
  console.log('check:ir-content OK — nuxt.config.ts imports all three generated sheets, in the required order')
  process.exit(0)
}

/**
 * The sheets have a gated CONTENT and, until now, a completely ungated DELIVERY PATH.
 * [Story 23.2 review 2026-08-07]
 *
 * ADR 0029 removed the base `.pill` rule from the scoped layer entirely, so its only route into a page is one
 * line in `nuxt.config.ts`. Delete or reorder that line and every `.pill` on the site — injected chips AND
 * `ListRow.vue`'s, which deliberately declare no look properties — renders unstyled, while this gate, the
 * token gate, the parity gate and the unit tests all stay green: every one of them reads the sheet FILE, and
 * none reads the config. `skip-link` joined the same layer in this change, which doubles the exposure.
 *
 * Order is load-bearing and asserted, not just presence: `shared-primitives.css` must come BEFORE
 * `ir-content.css` so a scoped `.ir-content …` rule still wins over an unscoped primitive.
 */
function assertSheetsAreActuallyImported() {
  const configPath = fileURLToPath(new URL('../nuxt.config.ts', import.meta.url))
  const config = readFileSync(configPath, 'utf8').replace(/\/\/[^\n]*/g, '')
  const at = (name) => config.indexOf(`assets/${name}`)

  const missing = ['tokens.css', 'shared-primitives.css', 'ir-content.css'].filter((n) => at(n) === -1)
  if (missing.length > 0) {
    console.error('check:ir-content FAILED — nuxt.config.ts does not import: ' + missing.join(', '))
    console.error('  A generated sheet that no page imports is styling nothing. The sheet files themselves')
    console.error('  are in sync, which is exactly why nothing else catches this.')
    process.exit(1)
  }

  if (at('shared-primitives.css') > at('ir-content.css')) {
    console.error('check:ir-content FAILED — nuxt.config.ts imports shared-primitives.css AFTER ir-content.css.')
    console.error('  Order is load-bearing: the UNSCOPED shared layer must come first so a scoped')
    console.error('  `.ir-content …` rule still overrides it. See ADR 0029 § Cascade order.')
    process.exit(1)
  }
}

// Report at RULE granularity, not "files differ" — a re-extraction and a hand-edit need different fixes,
// and the first line of a failing log should already say which happened.
console.error(`check:ir-content FAILED — the generated layer has drifted from ${SOURCE_LABEL}.`)

reportSheet('ir-content.css', actualCss, expectedCss)
reportSheet('shared-primitives.css', actualSharedCss, expectedSharedCss)
reportSheet('runtime-body.css', actualRuntimeCss, expectedRuntimeCss)

/** Rule-granularity diff for one generated sheet. Silent when that sheet is in sync. */
function reportSheet(label, actual, expectedText) {
  if (actual === expectedText) return
  const actualRules = ruleMap(actual)
  const expectedRules = ruleMap(expectedText)
  const added = [...expectedRules.keys()].filter((k) => !actualRules.has(k))
  const removed = [...actualRules.keys()].filter((k) => !expectedRules.has(k))
  const changed = [...expectedRules.keys()].filter(
    (k) => actualRules.has(k) && actualRules.get(k) !== expectedRules.get(k),
  )
  console.error(`  ${label}: +${added.length} rule(s), -${removed.length}, ~${changed.length} changed`)
  for (const s of added.slice(0, 8)) console.error(`    + ${s}`)
  for (const s of removed.slice(0, 8)) console.error(`    - ${s}`)
  for (const s of changed.slice(0, 8)) console.error(`    ~ ${s}`)
  const shown = Math.min(added.length, 8) + Math.min(removed.length, 8) + Math.min(changed.length, 8)
  const total = added.length + removed.length + changed.length
  if (total > shown) console.error(`    … and ${total - shown} more`)
  if (total === 0) {
    console.error('    (same rule set and same declarations — the difference is the banner, ordering or')
    console.error('     whitespace; this file is GENERATED by contract, so a hand-edit still fails.)')
  }
}

if (actualManifest !== expectedManifest) {
  console.error('  ir-content.manifest.json: out of sync with the sheet it documents.')
}

console.error('  Fix: run `npm run extract:ir-content` and commit the result.')
process.exit(1)

/** Scoped selector -> its declaration block, from a generated sheet. */
function ruleMap(text) {
  const map = new Map()
  const body = text.slice(text.indexOf('*/') + 2)
  for (const m of body.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    map.set(m[1].trim().replace(/\s+/g, ' '), m[2].trim().replace(/\s+/g, ' '))
  }
  return map
}
