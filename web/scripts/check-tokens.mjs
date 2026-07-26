#!/usr/bin/env node
// `npm run check:tokens` — the drift gate. Re-extracts the tokens from the C# stylesheet and diffs against
// the committed web/assets/tokens.css, exiting non-zero on ANY divergence.
//
// This is what makes AC #1's "no duplicated or hand-re-typed definitions" enforceable rather than aspirational:
// the only way the Vue app's token values can disagree with the shipped portal's is for this check to fail.
// [Story 23.2 AC #1]

import { SOURCE_LABEL, declaredTokenNames, readCommittedTokensCss, renderTokensCss, sliceRootBlock } from './tokens-lib.mjs'

let expected
try {
  expected = renderTokensCss()
} catch (err) {
  console.error(`check:tokens FAILED — could not extract tokens from ${SOURCE_LABEL}`)
  console.error(`  ${err.message}`)
  process.exit(1)
}

const actual = readCommittedTokensCss()

if (actual === null) {
  console.error('check:tokens FAILED — web/assets/tokens.css does not exist.')
  console.error('  Run `npm run extract:tokens` to generate it.')
  process.exit(1)
}

if (actual === expected) {
  const count = declaredTokenNames(sliceRootBlock(actual).body).length
  console.log(`check:tokens OK — ${count} tokens in sync with ${SOURCE_LABEL}`)
  process.exit(0)
}

// Report the divergence at TOKEN granularity, not just "files differ" — a value drift and a renamed family
// need different fixes, and the first line of a failing CI log should already say which one happened.
const expectedTokens = tokenMap(expected)
const actualTokens = tokenMap(actual)

const added = [...expectedTokens.keys()].filter((k) => !actualTokens.has(k))
const removed = [...actualTokens.keys()].filter((k) => !expectedTokens.has(k))
const changed = [...expectedTokens.keys()].filter(
  (k) => actualTokens.has(k) && actualTokens.get(k) !== expectedTokens.get(k),
)

console.error(`check:tokens FAILED — web/assets/tokens.css has drifted from ${SOURCE_LABEL}.`)
for (const name of added) console.error(`  + ${name}: ${expectedTokens.get(name)} (in source, missing here)`)
for (const name of removed) console.error(`  - ${name}: ${actualTokens.get(name)} (here, gone from source)`)
for (const name of changed) {
  console.error(`  ~ ${name}: ${actualTokens.get(name)} -> ${expectedTokens.get(name)}`)
}
if (added.length + removed.length + changed.length === 0) {
  // Same token set and same values, so the difference is comment/whitespace/banner text. Still a divergence —
  // the file is a verbatim copy by contract, and a hand-edit is exactly what this gate exists to catch.
  console.error('  (token values identical — the difference is comments, ordering, or the generated banner;')
  console.error('   the file is a VERBATIM copy by contract, so a hand-edit still fails.)')
}
console.error('  Fix: run `npm run extract:tokens` and commit the result.')
process.exit(1)

/** Top-level custom-property name -> declared value, from a rendered tokens.css. */
function tokenMap(text) {
  const body = sliceRootBlock(text).body.replace(/\/\*[\s\S]*?\*\//g, '')
  const map = new Map()
  for (const m of body.matchAll(/(^|[\s;{])(--[A-Za-z0-9-]+)\s*:([^;]*);/g)) {
    map.set(m[2], m[3].trim())
  }
  return map
}
