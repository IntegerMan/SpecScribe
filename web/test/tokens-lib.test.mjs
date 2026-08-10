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
import {
  declaredTokenNames,
  findRootBlocks,
  renderTokensCss,
  sliceRootBlock,
  SOURCE_CSS,
} from '../scripts/tokens-lib.mjs'

/**
 * The extractor took only the FIRST `:root` block until the 2026-07-28 re-review. When the Impact Map added a
 * second one (`--impact-lvl-1`…`-5`), that family silently never crossed the bridge — and because
 * `check-tokens.mjs` ran the same one-block extractor on BOTH sides, the two could not disagree about tokens
 * neither of them looked at. The gate printed "OK — 36 tokens in sync" the entire time it was wrong.
 *
 * That is a gate failing OPEN, which is the one failure mode a gate may never have, so it is pinned here from
 * several directions.
 */
describe('findRootBlocks', () => {
  it('finds EVERY top-level :root block, not just the first', () => {
    const css = ':root {\n  --a: 1;\n}\n.x { color: red }\n:root {\n  --b: 2;\n}\n'
    const blocks = findRootBlocks(css)
    expect(blocks).toHaveLength(2)
    expect(blocks.flatMap((b) => declaredTokenNames(b.body))).toEqual(['--a', '--b'])
  })

  it('does NOT descend into a :root nested in an at-rule — that is a viewport override, not a definition', () => {
    const css = ':root {\n  --a: 1;\n}\n@media (max-width: 560px) {\n  :root { --a: 2; }\n}\n'
    expect(findRootBlocks(css)).toHaveLength(1)
  })

  it('ignores a :root that only appears inside a comment', () => {
    // Finding the block used to be comment-BLIND while scanning it was comment-AWARE, so a comment beginning
    // a line with `:root {` set the open brace inside the comment and sliced from the wrong offset.
    const css = '/* the tokens live in\n:root {\n which is near the top */\n:root {\n  --real: 1;\n}\n'
    const blocks = findRootBlocks(css)
    expect(blocks).toHaveLength(1)
    expect(declaredTokenNames(blocks[0].body)).toEqual(['--real'])
  })

  it('does not mistake an attribute-qualified :root[…] rule for a token block', () => {
    const css = ':root[data-boot] .panel { display: none }\n:root {\n  --a: 1;\n}\n'
    expect(findRootBlocks(css)).toHaveLength(1)
  })

  it('finds every TOP-LEVEL :root block of the real stylesheet, and the tokens they declare', () => {
    // Title is deliberately narrower than the previous "wherever it is declared", which overclaimed:
    // `findRootBlocks` SKIPS a `:root` nested in `@media` by design (see its docblock), and specscribe.css
    // declares `--nav-offset` in exactly such a block — so that token is genuinely not carried and the old
    // title described coverage this test does not have.
    //
    // `expect(blocks.length).toBeGreaterThan(0)` was also near-vacuous. It is replaced with an INDEPENDENT
    // oracle: count top-level block openings with a plain line-anchored regex rather than re-deriving through
    // `findRootBlocks`, which would compare the function against itself. `^:root \{` cannot match the
    // `@media`-nested block because that one is indented — which is precisely the distinction under test.
    // [Story 17.1 code review]
    const raw = readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n')
    const blocks = findRootBlocks(raw)
    expect(blocks).toHaveLength((raw.match(/^:root \{/gm) ?? []).length)
    expect(blocks.length).toBeGreaterThan(0)

    const all = blocks.flatMap((b) => declaredTokenNames(b.body))
    expect(all).toContain('--status-done')
    expect(all).toContain('--impact-lvl-3') // absent from tokens.css before the 2026-07-28 fix
  })
})

describe('renderTokensCss', () => {
  it('emits every block the source declares, so no token is stranded on the C# side', () => {
    // This assertion HAS TO run against a synthetic multi-block source, not the real sheet.
    //
    // The original `> 1` form read the real stylesheet, so Story 17.1's legitimate `:root` merge turned it red
    // while the extractor was entirely correct. Its replacement compared the emitted block count against
    // `findRootBlocks(source).length` — but `renderTokensCss` BUILDS its output by mapping over that very same
    // `findRootBlocks` call, so both sides of the comparison moved together and NO input could make it fail.
    // Concretely: reverting the extractor to `blocks.slice(0, 1)` — the exact Story 23.2 fail-open regression
    // this test exists to catch, the one where "the gate printed 'OK — 36 tokens in sync' the entire time it
    // was wrong" — passed the whole suite. That is an assertion-free test in the same shape as the S2699
    // BLOCKER Story 17.1 removed from the C# suite.
    //
    // The fixture AUGMENTS the real source rather than replacing it: `renderTokensCss` refuses any input
    // missing a REQUIRED_TOKEN, so a bare two-property fixture never reaches the code under test. Appending
    // one extra top-level block keeps every required token present while guaranteeing a second block, and it
    // genuinely fails if any block is dropped. [Story 17.1 code review]
    const source = readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n')
    const augmented = `${source}\n:root {\n  --synthetic-second-block: 1;\n}\n`
    // Independent oracle again — counted off the INPUT text, never re-derived through `findRootBlocks`.
    const expectedBlocks = (augmented.match(/^:root \{/gm) ?? []).length
    expect(expectedBlocks).toBeGreaterThan(1) // the fixture must really be multi-block for this to mean anything

    const emitted = renderTokensCss(augmented)
    expect(emitted).toContain('--impact-lvl-3') // from the real block; absent from tokens.css before 2026-07-28
    expect(emitted).toContain('--synthetic-second-block') // dies under `blocks.slice(0, 1)`
    expect(emitted.match(/^:root \{/gm)).toHaveLength(expectedBlocks)
  })

  it('refuses to emit when a property is declared twice — the gate cannot reason about which copy drifted', () => {
    // `tokenMap` is last-wins, so a drift confined to the FIRST copy compares equal and is misreported as a
    // comment-only difference. Failing loudly beats a gate that quietly cannot see half its input.
    const css = ':root {\n  --dupe: 1;\n}\n:root {\n  --dupe: 2;\n}\n'
    expect(() => renderTokensCss(css)).toThrow(/declared more than once/)
  })
})

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
