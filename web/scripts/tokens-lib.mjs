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
  // ⚠️ This list must name every token the SHIPPED PRIMITIVES BIND TO, not just one per family. The original
  // list guarded the extraction TARGET ("did we latch onto the right rule?") but not the CONSUMPTION SURFACE,
  // so renaming `--parchment-dark` in the C# stylesheet passed every gate green while four of nine badge
  // stages lost their background to invalid-at-computed-value-time — transparent chips on a cream page.
  // When a component starts binding a new token, add it here. [Story 23.2 re-review 2026-07-28]
  '--parchment-dark', // StatusBadge base + .is-pending/.is-deferred/.is-retired; design-system retired swatch
  '--ink-faded', // ChartPanel framing sentence
  '--moss', // StatusBadge .is-done text
  '--gold', // StatusBadge .is-ready/.is-drafted text
  '--rust-light', // ChartPanel .chart-frame-note left rule
  '--status-drafted',
  '--status-ready',
  '--status-active',
  '--status-review',
  '--status-done',
  '--status-deferred',
  // The stage FILL tints, paired with the accents above. StatusBadge binds all four; they exist BECAUSE this
  // list caught their absence — the component had substituted --parchment for four distinct portal tints
  // because the source rules held inline hexes the bridge could not carry. [Story 23.2 re-review 2026-07-28]
  '--status-done-bg',
  '--status-active-bg',
  '--status-review-bg',
  '--status-ready-bg', // .is-ready AND .is-drafted — one fill, as the portal pairs them
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

/** 1-based line number of a character offset. */
function lineOf(css, index) {
  return css.slice(0, index).split('\n').length
}

/**
 * Index of the `}` matching the `{` at `braceAt`, comment-aware.
 *
 * Naive brace counting is not safe here: the `:root` block is heavily commented and a comment containing a
 * brace would silently truncate the copy. Tracking comment state makes the scan structural.
 */
function matchBrace(css, braceAt, label) {
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
      if (depth === 0) return i
    }
    i += 1
  }

  throw new Error(
    `token bridge: ':root' block at ${label}:${lineOf(css, braceAt)} is unterminated (unbalanced braces)`,
  )
}

/**
 * Every **top-level** `:root { … }` rule in a stylesheet, in source order, comment-aware throughout.
 *
 * ⚠️ This used to be a `css.search(/(^|\n):root\s*\{/)` that returned the FIRST match and stopped. Two bugs
 * came out of that one line, and both failed OPEN — the shape a drift gate must never have:
 *
 *   1. `specscribe.css` grew a second top-level `:root` (the Impact Map's `--impact-lvl-1`…`-5` ramp,
 *      Story 21.3). It never crossed the bridge — and because `check-tokens.mjs` ran the SAME one-block
 *      extractor on both sides, the two could not disagree about tokens neither of them looked at. The gate
 *      printed "OK — 36 tokens in sync" while a whole family silently did not exist in the Vue app.
 *   2. Finding the block was comment-BLIND while scanning it was comment-AWARE. A comment whose text began a
 *      line with `:root {` would have set the open brace inside the comment and sliced from the wrong offset
 *      — the same class as the star-slash-in-a-custom-property truncation that once killed ~1,000 rules here.
 *      (Spelling that sequence out is itself the bug, in CSS and in this docblock alike, so it is not spelled.)
 *
 * A single comment-aware pass fixes both: the selector buffer only accumulates outside comments, so a
 * commented-out `:root {` cannot be mistaken for a real one.
 *
 * **Deliberately top-level only.** A `:root` nested inside an at-rule (today: the `--nav-offset: 5.5rem`
 * override inside `@media (max-width: …)`) is a VIEWPORT-CONDITIONAL override, not a token definition — the
 * Nuxt app owns its own layout and must not inherit the portal's breakpoints. The base value still crosses,
 * because it is declared in the first block.
 *
 * @returns {{ body: string, startLine: number, endLine: number }[]} each block's INNER text (between the
 *   braces, exclusive) and the 1-based line span it occupied in the source.
 */
export function findRootBlocks(css, label = SOURCE_LABEL) {
  const blocks = []
  let i = 0
  let inComment = false
  let selector = ''
  // Preludes of the blocks currently open around `i`, outermost first. This exists so a `:root` found at
  // depth > 0 can be judged by WHAT encloses it rather than by the bare fact that something does.
  const stack = []

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
    if (ch === '{') {
      const prelude = selector.trim()
      if (preludeNamesRoot(prelude)) {
        if (stack.length === 0) {
          // A GROUPED prelude (`:root, :host { … }`) declares the same tokens for more than one subject.
          // Copying the body out under a bare `:root` would silently change what the sheet means, and
          // skipping it would drop a whole token family while `check:tokens` still printed "in sync" —
          // because both sides run this same extractor, so neither can see a block neither looks at.
          // Fail loudly instead; splitting the rule in the source is a one-line, reviewable change.
          if (prelude !== ':root') {
            throw new Error(
              `token bridge: ${label}:${lineOf(css, i)} has the grouped prelude \`${prelude}\`. The extractor ` +
                `carries only a rule whose prelude is exactly \`:root\`, because copying a grouped rule out ` +
                `under a bare \`:root\` would change its meaning. Split it into its own \`:root { … }\` rule, ` +
                `or extend this extractor deliberately — do not let the family cross by accident.`,
            )
          }
          const end = matchBrace(css, i, label)
          blocks.push({ body: css.slice(i + 1, end), startLine: lineOf(css, i), endLine: lineOf(css, end) })
          i = end + 1
          selector = ''
          continue
        }

        // Nested `:root`. Inside `@media` this is the established viewport-override shape (`--nav-offset` at
        // a breakpoint) and is deliberately NOT carried: the bridge publishes the unconditional values.
        // Inside any OTHER at-rule it is very likely a real token definition, and silently dropping it is the
        // same fail-open bug as the grouped prelude above. `ir-content-build.mjs` already treats
        // `@media|@supports|@layer|@container` as four distinct conditional forms; the bridge now does too.
        const enclosing = stack[stack.length - 1]
        const atRule = /^@([a-zA-Z-]+)/.exec(enclosing)
        const kind = atRule ? atRule[1].toLowerCase() : null
        if (kind !== 'media') {
          throw new Error(
            `token bridge: ${label}:${lineOf(css, i)} declares a \`:root\` block inside \`${enclosing.trim()}\`. ` +
              `Only \`@media\` nesting is understood (a viewport override, deliberately not carried). ` +
              `A \`:root\` inside \`@layer\`/\`@supports\`/\`@container\` — or inside another rule — is most ` +
              `likely a real token family, and dropping it silently would leave \`check:tokens\` green while ` +
              `the tokens never reached the app. Decide explicitly: hoist it, or teach the extractor.`,
          )
        }
      }
      stack.push(prelude)
      selector = ''
      i += 1
      continue
    }
    if (ch === '}') {
      stack.pop()
      selector = ''
      i += 1
      continue
    }
    // Ends a statement without opening a block: a top-level at-rule (`@charset "…";`) or a declaration
    // inside a rule we are scanning through.
    if (ch === ';') {
      selector = ''
      i += 1
      continue
    }
    selector += ch
    i += 1
  }

  return blocks
}

/**
 * Does this prelude name `:root` as one of its comma-separated selectors?
 *
 * Exact-match per selector, deliberately: `html:root` and `:root[data-boot]` are NOT the unconditional token
 * block and must not be treated as one. Grouping is detected here so the caller can refuse it loudly rather
 * than fail open — the bug this whole module has been bitten by twice.
 */
function preludeNamesRoot(prelude) {
  return prelude.split(',').some((s) => s.trim() === ':root')
}

/**
 * The FIRST top-level `:root` block. Retained for callers that only need the primary palette block; prefer
 * {@link findRootBlocks} anywhere completeness matters.
 *
 * @returns {{ body: string, startLine: number, endLine: number }}
 */
export function sliceRootBlock(css, label = SOURCE_LABEL) {
  const blocks = findRootBlocks(css, label)
  if (blocks.length === 0) {
    throw new Error(`token bridge: no top-level ':root {' rule found in ${label}`)
  }
  return blocks[0]
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
  const blocks = findRootBlocks(css)
  if (blocks.length === 0) {
    throw new Error(`token bridge: no top-level ':root {' rule found in ${SOURCE_LABEL}`)
  }
  const spans = blocks.map((b) => `${b.startLine}-${b.endLine}`).join(', ')

  const names = blocks.flatMap((b) => declaredTokenNames(b.body))
  const declared = new Set(names)

  // A property declared twice across the extracted blocks is a real ambiguity, not a curiosity: `tokenMap`
  // resolves last-wins, so a drift confined to the FIRST copy would compare equal and be misreported as a
  // comment-only difference. Fail rather than carry a set the gate cannot reason about.
  const seen = new Set()
  const duplicates = [...new Set(names.filter((n) => (seen.has(n) ? true : (seen.add(n), false))))]
  if (duplicates.length > 0) {
    throw new Error(
      `token bridge: ${duplicates.length} custom propert${duplicates.length === 1 ? 'y is' : 'ies are'} ` +
        `declared more than once across the ':root' blocks of ${SOURCE_LABEL} (lines ${spans}): ` +
        `${duplicates.join(', ')}. The drift gate cannot distinguish which copy moved — de-duplicate first.`,
    )
  }

  const missing = REQUIRED_TOKENS.filter((t) => !declared.has(t))
  if (missing.length > 0) {
    throw new Error(
      `token bridge: the ${blocks.length} extracted ':root' ${blocks.length === 1 ? 'block' : 'blocks'} ` +
        `(${SOURCE_LABEL}:${spans}) ${blocks.length === 1 ? 'is' : 'are'} missing ${missing.length} required ` +
        `${missing.length === 1 ? 'token' : 'tokens'}: ${missing.join(', ')}. Either the extractor latched ` +
        `onto the wrong rule, or a token family was renamed in the C# stylesheet — reconcile before regenerating.`,
    )
  }

  // ⚠️ No source LINE SPAN in this banner, deliberately. This file is committed and `check:tokens` compares
  // it byte-for-byte, so anything baked in here that can move without a token moving turns the gate red on
  // an unrelated commit — inserting one rule above `:root` in the C# stylesheet would have done it. (The
  // sibling ir-content manifest reddened CI exactly that way; see the committed-fields rule in
  // `ir-content-build.mjs`.) The span is still reported in the thrown error above, which is not committed.
  const banner = [
    '/* GENERATED FILE — DO NOT EDIT.',
    ` * Extracted verbatim from EVERY top-level \`:root\` block of ${SOURCE_LABEL}`,
    ' * by `npm run extract:tokens`. That C# stylesheet is the single source of truth for SpecScribe\'s',
    ' * presentation tokens (AD-7); this file is a copy, never a second definition.',
    ' *',
    ' * Re-run `npm run extract:tokens` after ANY token change in the C# stylesheet.',
    ' * `npm run check:tokens` fails the build when this file and the source diverge.',
    ' *',
    ' * Host CHROME remaps (specscribe-webview-theme.css) are deliberately NOT carried here — those are',
    ' * host-owned under AD-7. Only content-semantic tokens cross this bridge.',
    ' *',
    ' * A `:root` nested inside an at-rule is a VIEWPORT-CONDITIONAL override, not a token definition, and is',
    ' * deliberately not carried: the Nuxt app owns its own breakpoints. Base values still cross.',
    ' */',
  ].join('\n')

  const body = blocks.map((b) => `:root {${b.body}}`).join('\n\n')
  return `${banner}\n${body}\n`
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
