#!/usr/bin/env node
// `npm run check:tokens` — the drift gate. Re-extracts the tokens from the C# stylesheet and diffs against
// the committed web/assets/tokens.css, exiting non-zero on ANY divergence.
//
// This is what makes AC #1's "no duplicated or hand-re-typed definitions" enforceable rather than aspirational:
// the only way the Vue app's token values can disagree with the shipped portal's is for this check to fail.
// [Story 23.2 AC #1]

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

import { SOURCE_LABEL, declaredTokenNames, findRootBlocks, readCommittedTokensCss, renderTokensCss } from './tokens-lib.mjs'

/** Repo-relative label for the GENERATED file, so a fault in it is never attributed to the C# source. */
const TOKENS_LABEL = 'web/assets/tokens.css'

const WEB_ROOT = fileURLToPath(new URL('..', import.meta.url))

/**
 * Every consumer of a token in this app, so the bridge can be checked against what is USED and not only
 * against a hand-maintained list. [Story 23.2 review 2026-08-07]
 *
 * `REQUIRED_TOKENS` is a constant a human must remember to extend, guarding a failure whose entire nature is
 * that nobody notices: `tokens-lib.mjs`'s own comment records that renaming `--parchment-dark` once passed
 * every gate green while four badge stages lost their background. Deriving the check from the consumers
 * closes the loop — a token that this app references but the bridge does not carry is now a gate failure,
 * whether or not anyone thought to list it.
 */
const CONSUMER_DIRS = ['components', 'pages', 'server']
const CONSUMER_FILES = ['app.vue', 'error.vue', 'assets/base.css']

/**
 * GENERATED sheets are deliberately out of scope. `ir-content.css`, `shared-primitives.css` and
 * `runtime-body.css` are derived from `specscribe.css` and are already gated against it by
 * `check:ir-content`; their `var()` references are the C# stylesheet's business, not the bridge's. Scanning
 * them here produced only false positives — every one turned out to be either a `var(--x, fallback)` (which
 * degrades by design) or an element-scoped property the renderer sets inline, e.g. `style="--col:3"` in
 * `Charts.cs` and `style="--lane-count: N"` in `SprintTemplater.cs`.
 */
const GENERATED_SHEETS = ['ir-content.css', 'shared-primitives.css', 'runtime-body.css', 'tokens.css']

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
  const carried = new Set(blocks.flatMap((b) => declaredTokenNames(b.body)))

  const unresolved = findUnresolvedTokenRefs(carried)
  if (unresolved.length > 0) {
    console.error('check:tokens FAILED — this app references tokens the bridge does not carry:')
    for (const u of unresolved) {
      console.error(`  ${u.file}: ${u.tokens.join(', ')}`)
    }
    console.error('')
    console.error(`  These are \`var(--…)\` references that are neither declared in ${TOKENS_LABEL} nor`)
    console.error('  defined locally in the same file. A reference that resolves to nothing is')
    console.error('  invalid-at-computed-value-time: the property silently takes its initial value, which is')
    console.error('  how a renamed token blanks a component while every gate stays green.')
    console.error('  Either the token was renamed in the C# stylesheet, or this file should define it locally.')
    process.exit(1)
  }

  console.log(
    `check:tokens OK — ${count} tokens across ${blocks.length} \`:root\` block(s), in sync with ${SOURCE_LABEL}`,
  )
  console.log(`check:tokens OK — every \`var(--…)\` reference in web/ resolves to a carried or local token`)
  process.exit(0)
}

/**
 * `var(--x)` references in this app that resolve to neither a carried token nor a custom property the same
 * file declares.
 *
 * The same-file allowance is what makes this usable rather than noisy: components legitimately define their
 * own custom properties (`--list-row-accent` in ListRow.vue is the canonical case) and those are not the
 * bridge's business. Anything else referencing an undeclared name is a real dangling reference.
 */
function findUnresolvedTokenRefs(carried) {
  const findings = []
  for (const file of consumerFiles()) {
    const text = readFileSync(file, 'utf8')
    // Comments would otherwise contribute references that no rule actually makes.
    const code = text.replace(/\/\*[\s\S]*?\*\//g, '')
    const local = new Set([...code.matchAll(/(--[A-Za-z0-9-]+)\s*:/g)].map((m) => m[1]))
    // Only references with NO fallback. `var(--x, #3d6b35)` degrades to the fallback by design and is not a
    // dangling reference — treating it as one produced seven false positives on the first run of this check.
    const referenced = new Set(
      [...code.matchAll(/var\(\s*(--[A-Za-z0-9-]+)\s*\)/g)].map((m) => m[1]),
    )
    const missing = [...referenced].filter((t) => !carried.has(t) && !local.has(t)).sort()
    if (missing.length > 0) {
      findings.push({ file: relative(WEB_ROOT, file).replace(/\\/g, '/'), tokens: missing })
    }
  }
  return findings
}

function consumerFiles() {
  const out = []
  for (const name of CONSUMER_FILES) {
    const p = join(WEB_ROOT, name)
    try {
      statSync(p)
      out.push(p)
    } catch {
      // An optional root file (error.vue) may not exist; that is not a token problem.
    }
  }
  for (const dir of CONSUMER_DIRS) {
    walkInto(join(WEB_ROOT, dir), out)
  }
  return out.filter((p) => !GENERATED_SHEETS.some((g) => p.endsWith(g)))
}

function walkInto(dir, out) {
  let entries
  try {
    entries = readdirSync(dir, { withFileTypes: true })
  } catch {
    return
  }
  for (const e of entries) {
    const p = join(dir, e.name)
    if (e.isDirectory()) {
      walkInto(p, out)
    } else if (/\.(vue|css|ts)$/.test(e.name)) {
      out.push(p)
    }
  }
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
 * ⚠️ The value is terminated by `;`, by the end of the block, **or by the start of the next declaration**.
 * CSS permits the final declaration in a rule to omit its semicolon, and the original `([^;]*);` regex
 * silently skipped it — so dropping the trailing `;` on `--motion-stagger` and changing its value produced
 * empty added/removed/changed sets, and a real value drift on a motion token was reported as "the difference
 * is comments, ordering, or the generated banner". [Story 23.2 re-review 2026-07-28]
 *
 * ⚠️ The `\n\s*--name:` lookahead closes the OTHER half of that hole, found on 2026-08-07: a missing
 * semicolon *mid-block* is worse than one at the end. With `--a: 1` (no `;`) followed by `--b: 2;`, the old
 * pattern ran `--a`'s value on to the next `;` and returned `--a -> "1\n  --b: 2"` with **`--b` absent
 * entirely** — so the report named `--a` as changed with a multi-line value and listed `--b` as missing,
 * pointing the operator at a token nobody had touched. A custom-property value cannot legally contain the
 * start of another custom-property declaration, so the lookahead is safe as well as sufficient.
 *
 * Duplicate names within one file are a hard failure rather than last-wins: `renderTokensCss` already refuses
 * a source that declares one twice, but the COMMITTED file reaches here without that guard, and last-wins is
 * exactly the resolution that would hide a drift confined to the first copy.
 */
function tokenMap(text, label) {
  const blocks = findRootBlocks(text, label)
  if (blocks.length === 0) {
    throw new Error(`no top-level ':root {' rule found in ${label}`)
  }
  const map = new Map()
  const duplicates = []
  for (const block of blocks) {
    const body = block.body.replace(/\/\*[\s\S]*?\*\//g, '')
    for (const m of body.matchAll(
      /(^|[\s;{])(--[A-Za-z0-9-]+)\s*:([^;]*?)(?=;|$|\n\s*--[A-Za-z0-9-]+\s*:)/g,
    )) {
      if (map.has(m[2])) duplicates.push(m[2])
      map.set(m[2], m[3].trim())
    }
  }
  if (duplicates.length > 0) {
    throw new Error(
      `${label} declares ${[...new Set(duplicates)].join(', ')} more than once. The drift report cannot say ` +
        `which copy moved, so this is a hard failure rather than a last-wins guess — de-duplicate the file.`,
    )
  }
  return map
}
