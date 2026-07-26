// Shared extraction logic for the token bridge (Story 23.2, AC #1).
//
// The C# stylesheet stays the single source of truth for presentation tokens (AD-7). This module lifts its
// `:root { … }` block VERBATIM — a pure copy, never a re-typed literal — so the Vue app and the generated
// portal can only ever disagree if someone edits the generated file, which `check-tokens.mjs` then catches.
//
// Deliberately NOT the 23.1 spike's move of importing the whole 7,000-line `specscribe.css`: dragging the
// monolith into every Nuxt page would keep alive exactly the fragility class Epic 23 exists to end (see the
// `*/`-in-a-custom-property comment truncation incident, which silently killed ~1,000 rules).

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

/** The single source of truth. Resolved from this file so the scripts work from any cwd. */
export const SOURCE_CSS = fileURLToPath(
  new URL('../../src/SpecScribe/assets/specscribe.css', import.meta.url),
)

/** The generated artifact, committed so the drift check has something to diff against. */
export const TOKENS_CSS = fileURLToPath(new URL('../assets/tokens.css', import.meta.url))

/** Repo-relative label for the source, used in the generated banner (never an absolute machine path — that
 *  would make the generated file differ per checkout and turn every teammate's run into a false drift hit). */
export const SOURCE_LABEL = 'src/SpecScribe/assets/specscribe.css'

// A verbatim copy is only trustworthy if it copied the RIGHT block. A brace-matcher that latched onto some
// other rule would still produce a file that diffs clean against itself forever, so the extraction asserts the
// families it is supposed to be carrying. One name per family plus every stage of the lifecycle vocabulary —
// the set 23.3's primitives bind to.
const REQUIRED_TOKENS = [
  '--status-pending',
  '--status-drafted',
  '--status-ready',
  '--status-active',
  '--status-review',
  '--status-done',
  '--status-deferred',
  '--status-unrecognized',
  '--status-unrecognized-hatch',
  '--motion-fast',
  '--motion-entrance',
  '--motion-entrance-long',
  '--motion-ease',
  '--motion-stagger',
  // The brand palette the status/motion tokens resolve against — without these the status tokens that are
  // themselves `var(--gold-light)` / `var(--teal)` aliases would dangle.
  '--parchment',
  '--cream',
  '--warm-white',
  '--ink',
  '--ink-light',
  '--border',
  '--moss-light',
  '--teal',
  '--teal-deep',
  '--gold-light',
  '--rust',
  '--shadow',
]

/**
 * Slices the first top-level `:root { … }` rule out of a stylesheet, comment-aware.
 *
 * Naive brace counting is not safe here: the `:root` block is heavily commented and a future comment
 * containing a brace would silently truncate the copy. Tracking comment state makes the scan structural.
 *
 * @returns {{ body: string, startLine: number, endLine: number }} the block's INNER text (between the
 *   braces, exclusive) and the 1-based line span it occupied in the source.
 */
export function sliceRootBlock(css) {
  const open = css.search(/(^|\n):root\s*\{/)
  if (open < 0) {
    throw new Error(`token bridge: no top-level ':root {' rule found in ${SOURCE_LABEL}`)
  }
  const braceAt = css.indexOf('{', open)
  let i = braceAt + 1
  let depth = 1
  let inComment = false

  while (i < css.length) {
    if (inComment) {
      if (css.startsWith('*/', i)) {
        inComment = false
        i += 2
        continue
      }
      i += 1
      continue
    }
    if (css.startsWith('/*', i)) {
      inComment = true
      i += 2
      continue
    }
    const ch = css[i]
    if (ch === '{') depth += 1
    else if (ch === '}') {
      depth -= 1
      if (depth === 0) break
    }
    i += 1
  }

  if (depth !== 0) {
    throw new Error(`token bridge: ':root' block in ${SOURCE_LABEL} is unterminated (unbalanced braces)`)
  }

  const body = css.slice(braceAt + 1, i)
  const startLine = css.slice(0, braceAt).split('\n').length
  const endLine = css.slice(0, i).split('\n').length
  return { body, startLine, endLine }
}

/** Every custom property declared at the top level of an extracted block body, in declaration order. */
export function declaredTokenNames(body) {
  const stripped = body.replace(/\/\*[\s\S]*?\*\//g, '')
  return [...stripped.matchAll(/(^|[\s;{])(--[A-Za-z0-9-]+)\s*:/g)].map((m) => m[2])
}

/**
 * Renders the generated `tokens.css` from the current source stylesheet.
 *
 * Line endings are normalized to `\n` so a CRLF checkout can't report drift against an LF-committed file
 * (the same normalization the C# golden-fingerprint test applies for the same reason).
 */
export function renderTokensCss(cssText = readFileSync(SOURCE_CSS, 'utf8')) {
  const css = cssText.replace(/\r\n/g, '\n')
  const { body, startLine, endLine } = sliceRootBlock(css)

  const declared = new Set(declaredTokenNames(body))
  const missing = REQUIRED_TOKENS.filter((t) => !declared.has(t))
  if (missing.length > 0) {
    throw new Error(
      `token bridge: the extracted ':root' block (${SOURCE_LABEL}:${startLine}-${endLine}) is missing ` +
        `${missing.length} required token(s): ${missing.join(', ')}. Either the extractor latched onto the ` +
        `wrong rule, or a token family was renamed in the C# stylesheet — reconcile before regenerating.`,
    )
  }

  const banner = [
    '/* GENERATED FILE — DO NOT EDIT.',
    ` * Extracted verbatim from the \`:root\` block of ${SOURCE_LABEL} (lines ${startLine}-${endLine})`,
    ' * by `npm run extract:tokens`. That C# stylesheet is the single source of truth for SpecScribe\'s',
    ' * presentation tokens (AD-7); this file is a copy, never a second definition.',
    ' *',
    ' * Re-run `npm run extract:tokens` after ANY token change in the C# stylesheet.',
    ' * `npm run check:tokens` fails the build when this file and the source diverge.',
    ' *',
    ' * Host CHROME remaps (specscribe-webview-theme.css) are deliberately NOT carried here — those are',
    ' * host-owned under AD-7. Only content-semantic tokens cross this bridge.',
    ' */',
    ':root {',
  ].join('\n')

  return `${banner}${body}}\n`
}

/** The committed generated file, line-ending-normalized for comparison. Null when it doesn't exist yet. */
export function readCommittedTokensCss() {
  try {
    return readFileSync(TOKENS_CSS, 'utf8').replace(/\r\n/g, '\n')
  } catch (err) {
    if (err.code === 'ENOENT') return null
    throw err
  }
}
