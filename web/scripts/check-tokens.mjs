#!/usr/bin/env node
// `npm run check:tokens` — the drift gate. Re-extracts the tokens from the C# stylesheet and diffs against
// the committed web/assets/tokens.css, exiting non-zero on ANY divergence.
//
// This is what makes AC #1's "no duplicated or hand-re-typed definitions" enforceable rather than aspirational:
// the only way the Vue app's token values can disagree with the shipped portal's is for this check to fail.
// [Story 23.2 AC #1]

import { SOURCE_LABEL, declaredTokenNames, findRootBlocks, readCommittedTokensCss, renderTokensCss } from './tokens-lib.mjs'

/** Repo-relative label for the GENERATED file, so a fault in it is never attributed to the C# source. */
const TOKENS_LABEL = 'web/assets/tokens.css'

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
  // Count across EVERY block, not just the first — the one-block count was how a whole missing token family
  // still reported "in sync". [Story 23.2 re-review 2026-07-28]
  const blocks = findRootBlocks(actual, TOKENS_LABEL)
  const count = blocks.reduce((n, b) => n + declaredTokenNames(b.body).length, 0)
  console.log(
    `check:tokens OK — ${count} tokens across ${blocks.length} \`:root\` block(s), in sync with ${SOURCE_LABEL}`,
  )
  process.exit(0)
}

// Report the divergence at TOKEN granularity, not just "files differ" — a value drift and a renamed family
// need different fixes, and the first line of a failing CI log should already say which one happened.
//
// ⚠️ Both parses are guarded, and each names the file it actually read. Unguarded, a committed tokens.css whose
// `:root {` header had been stripped — a bad merge, a truncated write, precisely what this gate exists to catch
// — threw an UNCAUGHT stack trace naming the C# SOURCE stylesheet, sending the operator to debug the wrong file.
let expectedTokens
let actualTokens
try {
  expectedTokens = tokenMap(expected, SOURCE_LABEL)
} catch (err) {
  console.error(`check:tokens FAILED — could not parse the tokens extracted from ${SOURCE_LABEL}`)
  console.error(`  ${err.message}`)
  process.exit(1)
}
try {
  actualTokens = tokenMap(actual, TOKENS_LABEL)
} catch (err) {
  console.error(`check:tokens FAILED — ${TOKENS_LABEL} is present but could not be parsed.`)
  console.error(`  ${err.message}`)
  console.error('  This is the generated file, not the source. Run `npm run extract:tokens` to rewrite it.')
  process.exit(1)
}

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

/**
 * Top-level custom-property name -> declared value, across every `:root` block of a rendered tokens.css.
 *
 * ⚠️ The value is terminated by `;` **or by the end of the block**. CSS permits the final declaration in a
 * rule to omit its semicolon, and the old `([^;]*);` regex silently skipped it — so dropping the trailing `;`
 * on `--motion-stagger` and changing its value produced empty added/removed/changed sets, and a real value
 * drift on a motion token was reported as "the difference is comments, ordering, or the generated banner".
 * [Story 23.2 re-review 2026-07-28]
 */
function tokenMap(text, label) {
  const blocks = findRootBlocks(text, label)
  if (blocks.length === 0) {
    throw new Error(`no top-level ':root {' rule found in ${label}`)
  }
  const map = new Map()
  for (const block of blocks) {
    const body = block.body.replace(/\/\*[\s\S]*?\*\//g, '')
    for (const m of body.matchAll(/(^|[\s;{])(--[A-Za-z0-9-]+)\s*:([^;]*?)\s*(?:;|$)/g)) {
      map.set(m[2], m[3].trim())
    }
  }
  return map
}
