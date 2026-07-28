/**
 * `scripts/tokens-lib.mjs` — the design-token bridge. [Story 23.2 AC #1]
 *
 * `src/SpecScribe/assets/specscribe.css` is the single source of truth for SpecScribe's presentation
 * tokens (AD-7); `web/assets/tokens.css` is a GENERATED copy, and `npm run check:tokens` fails the build
 * when they diverge. Story 23.5 put that gate into CI, so the extractor underneath it now decides whether
 * builds pass.
 *
 * The case worth pinning hardest is comment-awareness. The `:root` block is heavily commented, and this
 * repo has already been bitten once by a star-slash sequence appearing inside a CSS comment and terminating
 * it early — roughly a thousand rules broke silently. Naive brace counting has the same failure shape: a
 * brace inside a comment truncates the copy, the generated file looks plausible, and tokens vanish from
 * every page. So the scanner tracks comment state, and that is asserted here directly.
 */
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { declaredTokenNames, renderTokensCss, sliceRootBlock, SOURCE_CSS } from '../scripts/tokens-lib.mjs'

describe('sliceRootBlock', () => {
  it('returns the block body exclusive of its braces', () => {
    const { body } = sliceRootBlock(':root {\n  --a: 1;\n}\n.x { color: red }')
    expect(body).toBe('\n  --a: 1;\n')
  })

  it('reports the 1-based line span the block occupied', () => {
    const { startLine, endLine } = sliceRootBlock('/* lead */\n\n:root {\n  --a: 1;\n}\n')
    expect(startLine).toBe(3)
    expect(endLine).toBe(5)
  })

  it('is not truncated by a brace inside a comment', () => {
    // The whole reason the scanner tracks comment state rather than counting braces.
    const css = ':root {\n  /* an unbalanced } in prose */\n  --a: 1;\n}\n'
    expect(sliceRootBlock(css).body).toContain('--a: 1;')
  })

  it('is not confused by a nested block', () => {
    const css = ':root {\n  --a: 1;\n  @media (x) { --b: 2; }\n  --c: 3;\n}\n.after {}'
    const { body } = sliceRootBlock(css)
    expect(body).toContain('--c: 3;')
    expect(body).not.toContain('.after')
  })

  it('does not latch onto a `:root` that is not at the start of a line', () => {
    // `html:root { … }` is a different rule; matching it would extract the wrong block.
    const css = 'html:root { --wrong: 1; }\n:root {\n  --right: 1;\n}\n'
    expect(sliceRootBlock(css).body).toContain('--right')
    expect(sliceRootBlock(css).body).not.toContain('--wrong')
  })

  it('refuses a stylesheet with no :root rule', () => {
    expect(() => sliceRootBlock('.x { color: red }')).toThrow(/no top-level ':root \{' rule/)
  })

  it('refuses an unterminated block rather than returning a partial copy', () => {
    expect(() => sliceRootBlock(':root {\n  --a: 1;\n')).toThrow(/unterminated/)
  })
})

describe('declaredTokenNames', () => {
  it('lists custom properties in declaration order', () => {
    expect(declaredTokenNames('\n  --b: 1;\n  --a: 2;\n')).toEqual(['--b', '--a'])
  })

  it('ignores custom properties that appear only inside a comment', () => {
    // A commented-out token is not declared; counting it would let a real removal pass the required-token
    // check and ship a stylesheet missing a whole family.
    expect(declaredTokenNames('\n  /* --gone: 1; */\n  --here: 2;\n')).toEqual(['--here'])
  })

  it('does not mistake a var() REFERENCE for a declaration', () => {
    expect(declaredTokenNames('\n  --a: var(--b);\n')).toEqual(['--a'])
  })

  it('returns an empty list for a body with no tokens', () => {
    expect(declaredTokenNames('\n  color: red;\n')).toEqual([])
  })
})

describe('renderTokensCss', () => {
  it('refuses to render when a required token family is missing', () => {
    // The failure this guards: the extractor latching onto the wrong rule, or a token family being renamed
    // in the C# stylesheet. Either would produce a plausible-looking file that silently drops tokens.
    expect(() => renderTokensCss(':root {\n  --not-a-real-token: 1;\n}\n')).toThrow(/missing/)
  })

  it('names the source line span in the failure, so the wrong rule can be identified', () => {
    expect(() => renderTokensCss(':root {\n  --nope: 1;\n}\n')).toThrow(/specscribe\.css:1-3/)
  })

  // ── The committed output must not encode WHERE it came from, only WHAT it carries ──────────────────────
  //
  // `tokens.css` is committed and `check:tokens` compares it byte-for-byte. Anything baked into it that can
  // move without a token moving turns the gate red on a commit that could not have affected the bridge —
  // and a gate that cannot stay green teaches people to re-run the extractor on reflex, which is how a real
  // drift gets committed unnoticed. The sibling ir-content manifest reddened CI exactly that way (an
  // unrelated `specscribe.css` edit shifted every line span it recorded), so this is a proven failure mode,
  // not a hypothetical one. The banner used to carry `(lines {start}-{end})`; these pin that it does not
  // come back.
  describe('is invariant to where the :root block sits in the source', () => {
    const source = readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n')

    it('renders identical bytes when unrelated rules are inserted above :root', () => {
      const shifted = `.zzz-unrelated {\n  color: red;\n}\n\n${source}`
      expect(sliceRootBlock(shifted).startLine).not.toBe(sliceRootBlock(source).startLine)
      expect(renderTokensCss(shifted)).toBe(renderTokensCss(source))
    })

    it('records no source line span in the generated banner', () => {
      expect(renderTokensCss(source)).not.toMatch(/lines \d+-\d+/)
    })
  })
})
