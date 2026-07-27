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

import { readFileSync, writeFileSync } from 'node:fs'
import { buildIrContentCss } from './ir-content-build.mjs'
import { OUT_CSS, OUT_MANIFEST, SCOPE, SOURCE_CSS, SOURCE_LABEL } from './ir-content-lib.mjs'

const { css, manifest, stats } = await buildIrContentCss()

writeFileSync(OUT_CSS, css, 'utf8')
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
console.log(`  pass-through coverage   ${stats.passThroughCoveredPct}% of the classes the other`)
console.log(`                          ${stats.totalPages - stats.migratedPages} pages use are already carried.`)
console.log(`                          Those pages are Story 23.4's — this is reported, not claimed.`)
console.log('')
console.log('  wrote web/assets/ir-content.css + web/assets/ir-content.manifest.json')
console.log('')
