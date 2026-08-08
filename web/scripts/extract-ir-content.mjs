#!/usr/bin/env node
// `npm run extract:ir-content` — regenerates web/assets/ir-content.css from the C# stylesheet. [23.3 AC #6]
//
// Reads the IR's markup for the four migrated families, harvests the classes / ids / attributes / elements
// it actually uses, carries the matching rules out of `specscribe.css`, and re-emits them nested under
// `.ir-content`. See `ir-content-lib.mjs` for why this layer exists and what makes it bounded rather than
// a re-import of the monolith.
//
// Run after ANY change to `specscribe.css` or to what the migrated surfaces render;
// `npm run check:ir-content` fails the build if you forget.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { buildIrContentCss } from './ir-content-build.mjs'
import { MEASUREMENTS_DIR } from './harness-lib.mjs'
import {
  OUT_CSS,
  OUT_MANIFEST,
  OUT_RUNTIME_CSS,
  OUT_SHARED_CSS,
  RUNTIME_BODY_CLASSES,
  SCOPE,
  SHARED_PRIMITIVES,
  SOURCE_CSS,
  SOURCE_LABEL,
} from './ir-content-lib.mjs'

const { css, sharedCss, runtimeCss, manifest, dropped, stats } = await buildIrContentCss()

writeFileSync(OUT_CSS, css, 'utf8')
writeFileSync(OUT_SHARED_CSS, sharedCss, 'utf8')
writeFileSync(OUT_RUNTIME_CSS, runtimeCss, 'utf8')
writeFileSync(OUT_MANIFEST, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8')

const kb = (n) => `${(n / 1024).toFixed(1)} KB`

console.log('')
console.log(`extract:ir-content — from ${SOURCE_LABEL}, scoped under \`${SCOPE}\``)
console.log('')
console.log(`  migrated pages read     ${stats.migratedPages} (of ${stats.totalPages} in the IR)`)
console.log(`  source rules            ${stats.sourceRules}`)
console.log(`  rules carried           ${stats.carriedRules}  (${stats.carriedSelectors} selectors)`)
console.log(`  keyframes carried       ${stats.carriedKeyframes}`)
console.log(`  dropped — unused        ${stats.droppedUnused}`)
console.log(`  dropped — root-level    ${stats.droppedRoot}  (no descendant to scope; base.css covers these)`)
console.log('')
console.log(`  source stylesheet       ${kb(readFileSync(SOURCE_CSS).length)}`)
console.log(`  generated layer         ${kb(Buffer.byteLength(css))}  (${stats.reductionPct}% smaller)`)
console.log('')
console.log(`  pass-through coverage   NOT MEASURED — every IR page is inside the extraction bound, so there`)
console.log(`                          is no un-harvested set left to compare against. The old "100%" here`)
console.log(`                          was a division over an empty set, not a result. [23.4 review, F-12]`)
console.log('')
console.log(`  shared primitives       ${stats.sharedRules} rule(s), UNSCOPED, from the allowlist`)
console.log(`                          [${SHARED_PRIMITIVES.map((c) => `.${c}`).join(', ')}] — ADR 0029.`)
console.log(`                          ${kb(Buffer.byteLength(sharedCss))}. These are REMOVED from the scoped`)
console.log(`                          layer, not duplicated: one definition, reachable by Vue components.`)
console.log('')
console.log(`  runtime body classes    ${stats.runtimeRules} rule(s), UNSCOPED, from the allowlist`)
console.log(`                          [${RUNTIME_BODY_CLASSES.map((c) => `.${c}`).join(', ')}] — ADR 0039.`)
console.log(`                          ${kb(Buffer.byteLength(runtimeCss))}. specscribe.js attaches these OUTSIDE`)
console.log(`                          .ir-content, so a scoped rule could never match them.`)
// ── What was DROPPED, and why ────────────────────────────────────────────────────────────────────────────
//
// The one thing this script never used to say. `selectorIsUsed` silently discards any rule naming a class the
// harvest did not see, and four separate incidents shipped styling to nobody because of it: the sunburst's
// black fills, `owner-author-2`, the Code Map's id-bearing spec/test filter, and the tooltip + details-rail
// loss. In every case the gate was GREEN — `check:ir-content` re-derives through this same function, so it
// drops the rule identically on both sides and the diff is empty. A gate cannot catch a bug in its own
// derivation; a report can. [ADR 0039]
//
// Ranked by how many rules a single missing class cost, because that is the shape the incidents had: one
// class nobody could harvest, taking a whole family's styling down with it.
const byClass = new Map()
for (const d of dropped) {
  for (const c of d.missingClasses) byClass.set(c, (byClass.get(c) ?? 0) + 1)
  for (const id of d.missingIds ?? []) byClass.set(`#${id}`, (byClass.get(`#${id}`) ?? 0) + 1)
}
const ranked = [...byClass.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))

console.log('')
console.log(`  dropped — never harvested  ${dropped.length} selector(s), across ${ranked.length} absent token(s)`)
console.log('                          Most of this is CORRECT: the monolith styles surfaces this app does not')
console.log('                          render. It is listed because the same mechanism silently dropped the')
console.log('                          sunburst fills, the Code Map filter and the tooltip. Scan for a token')
console.log('                          that a MIGRATED surface applies at runtime — that one is a bug, and it')
console.log('                          belongs in CONDITIONAL_CLASSES or RUNTIME_BODY_CLASSES.')
console.log('')
for (const [name, n] of ranked.slice(0, 12)) {
  console.log(`    ${String(n).padStart(4)}  ${name.startsWith('#') ? name : `.${name}`}`)
}
if (ranked.length > 12) console.log(`    … and ${ranked.length - 12} more (full list in measurements/)`)

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(
  join(MEASUREMENTS_DIR, 'ir-content-drops.json'),
  `${JSON.stringify(
    {
      generatedBy: 'web/scripts/extract-ir-content.mjs',
      why:
        'Selectors `selectorIsUsed` discarded because a class or id was absent from the harvest. NOT a defect '
        + 'list — the monolith styles surfaces this app does not render. It exists because the same silent drop '
        + 'shipped the sunburst black-fill, owner-author-2, Code Map filter and tooltip/details-rail regressions '
        + 'with every gate green. Deliberately NOT in ir-content.manifest.json: it is a function of the whole '
        + 'source stylesheet and would redden the gate on edits that cannot move the emitted layer.',
      droppedSelectors: dropped.length,
      absentTokens: ranked.map(([token, rules]) => ({ token, rules })),
      dropped,
    },
    null,
    2,
  )}\n`,
)

console.log('')
console.log('  wrote web/assets/ir-content.css + web/assets/shared-primitives.css')
console.log('       + web/assets/runtime-body.css + web/assets/ir-content.manifest.json')
console.log('       + web/measurements/ir-content-drops.json')
console.log('')
