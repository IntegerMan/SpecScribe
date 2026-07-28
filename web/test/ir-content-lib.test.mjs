/**
 * `scripts/ir-content-lib.mjs` — the CSS reader behind the `ir-content.css` drift gate. [Story 23.3 AC #6]
 *
 * `web/assets/ir-content.css` is GENERATED from `src/SpecScribe/assets/specscribe.css` and committed, and
 * `npm run check:ir-content` fails the build when the two drift. Story 23.5 found that gate RED at HEAD —
 * drift introduced by a concurrent story and caught by nothing, because until this story nothing ran it.
 * Now that it runs in CI, the primitives it depends on are worth pinning: a scoping or comment-parsing bug
 * would either wave real drift through or fail the build on a false positive, and both are expensive.
 *
 * The comment-parsing case below is not hypothetical: a `--status-` token followed by a star and a slash,
 * written inside a CSS comment, once terminated that comment early and silently broke roughly a thousand
 * rules across the portal. (Writing that sequence literally in this docblock would end it here, too.)
 */
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import {
  isMigrated,
  MIGRATED,
  scopeSelector,
  selectorAttributes,
  selectorTokens,
  stripComments,
} from '../scripts/ir-content-lib.mjs'

describe('isMigrated / MIGRATED', () => {
  it('recognizes the four migrated families', () => {
    expect(isMigrated('index.html')).toBe(true)
    expect(isMigrated('epics.html')).toBe(true)
    expect(isMigrated('epics/epic-3.html')).toBe(true)
    expect(isMigrated('epics/story-23-5.html')).toBe(true)
  })

  it('rejects pages outside them', () => {
    expect(isMigrated('about.html')).toBe(false)
    expect(isMigrated('code/src/SpecScribe/Charts.cs.html')).toBe(false)
    expect(isMigrated('adrs/0006-delivery.html')).toBe(false)
  })

  it('does not let a nested path masquerade as an epic page', () => {
    // The family patterns forbid a `/` inside the leaf, so a deeper path cannot claim the family.
    expect(MIGRATED.epicDetail('epics/sub/epic-3.html')).toBe(false)
    expect(MIGRATED.storyDetail('epics/sub/story-1-1.html')).toBe(false)
  })

  it('does not treat a story page as an epic page', () => {
    expect(MIGRATED.epicDetail('epics/story-23-5.html')).toBe(false)
    expect(MIGRATED.storyDetail('epics/epic-3.html')).toBe(false)
  })
})

describe('stripComments', () => {
  it('removes a block comment', () => {
    expect(stripComments('a{b:c}/* gone */d{e:f}')).toBe('a{b:c}d{e:f}')
  })

  it('removes a multi-line comment', () => {
    expect(stripComments('a{}\n/* one\n   two */\nb{}')).toContain('b{}')
    expect(stripComments('a{}\n/* one\n   two */\nb{}')).not.toContain('two')
  })

  it('leaves a bare `*` in a selector alone', () => {
    expect(stripComments('*{box-sizing:border-box}')).toBe('*{box-sizing:border-box}')
  })

  it('does not treat a `/` outside a comment as an opener', () => {
    expect(stripComments('a{background:url(x/y.png)}')).toBe('a{background:url(x/y.png)}')
  })
})

describe('scopeSelector', () => {
  // Operates on ONE selector; callers split a selector list before calling.
  it('prefixes a plain selector with the scope', () => {
    expect(scopeSelector('.donut')).toBe('.ir-content .donut')
  })

  it('keeps a :root/html/body head OUTSIDE the scope rather than nesting it', () => {
    // `.ir-content :root .x` could never match — :root is the document element and the scope is inside it.
    // The head has to stay in front, with the scope injected after it.
    expect(scopeSelector(':root .sunburst')).toBe(':root .ir-content .sunburst')
    expect(scopeSelector('html .x')).toBe('html .ir-content .x')
  })

  it('carries a state attribute on the head through unchanged', () => {
    expect(scopeSelector(':root[data-ss-hierarchy-boot] .chart-panel')).toBe(
      ':root[data-ss-hierarchy-boot] .ir-content .chart-panel',
    )
  })

  it('returns null for a bare root head with nothing to scope', () => {
    // `:root` alone sets global custom properties; scoping it would silently drop the token layer.
    expect(scopeSelector(':root')).toBeNull()
  })
})

describe('selectorTokens', () => {
  it('separates classes from ids', () => {
    expect(selectorTokens('.chart-panel .donut#main svg')).toEqual({
      classes: ['chart-panel', 'donut'],
      ids: ['main'],
    })
  })

  it('ignores classes that appear only inside an attribute selector', () => {
    // Attribute selectors are matched separately and must not leak into the class bound, or the extractor
    // would demand a class the markup never has and silently drop the rule.
    expect(selectorTokens('[data-x=".not-a-class"]').classes).toEqual([])
  })

  it('ignores pseudo-class arguments', () => {
    expect(selectorTokens('.a:has(.b)').classes).toEqual(['a'])
    expect(selectorTokens('.a:focus-visible').classes).toEqual(['a'])
  })

  it('returns empty lists for a bare element selector', () => {
    expect(selectorTokens('svg')).toEqual({ classes: [], ids: [] })
  })
})

describe('selectorAttributes', () => {
  it('extracts attribute names used in a selector', () => {
    const attrs = selectorAttributes('[data-hierarchy] .x[data-state="open"]')
    expect(attrs).toContain('data-hierarchy')
    expect(attrs).toContain('data-state')
  })

  it('returns an empty list when there are no attribute selectors', () => {
    expect(selectorAttributes('.a .b')).toEqual([])
  })
})

// ── The committed manifest must describe the CARRIED LAYER, never the whole source ──────────────────────
//
// `ir-content.manifest.json` is committed and `check:ir-content` compares it byte-for-byte. The rule that
// keeps that honest: a field belongs in the manifest only if changing it implies the emitted
// `ir-content.css` changed too. A field that can move on its own turns the gate red on a commit that could
// not possibly have affected the layer, and a gate that cannot stay green teaches people to re-run the
// extractor on reflex — which is exactly how a real drift gets committed unnoticed.
//
// Both classes of offender got in and were removed after they had already cost a red build:
//
//   1. WHOLE-CORPUS  [Story 23.5] — `migratedPages`, `totalPages`, `passThroughUncoveredClasses` moved
//      whenever anybody added a document anywhere in the ~1,100-page corpus.
//   2. WHOLE-SOURCE  — `sourceRules`, `droppedUnused`, `sourceBytes` and the per-rule `lines` span count or
//      locate rules the layer does NOT carry. Deleting 38 unused rules from `specscribe.css` shifted every
//      line span in the file and reddened CI with an 865-line diff while `ir-content.css` stayed
//      byte-identical.
//
// These assertions need no IR and no portal, so they run in the ordinary `npm test` loop rather than only
// in the expensive CI gate. They read the COMMITTED artifact, which is the thing the gate actually compares.
describe('ir-content.manifest.json committed fields', () => {
  const manifest = JSON.parse(readFileSync(new URL('../assets/ir-content.manifest.json', import.meta.url), 'utf8'))

  it('records no source line span on any rule', () => {
    const withLines = manifest.rules.filter((r) => 'lines' in r)
    expect(withLines).toEqual([])
  })

  it('carries only stats that move when the emitted layer moves', () => {
    // Whitelist, not a blocklist: a new source-wide counter should fail this the day it is added, and a
    // blocklist would silently wave it through.
    expect(Object.keys(manifest.stats).sort()).toEqual(
      ['carriedKeyframes', 'carriedRules', 'carriedSelectors', 'droppedRoot', 'generatedBytes'].sort(),
    )
  })

  it('identifies every rule by selector, the anchor that does not go stale', () => {
    expect(manifest.rules.length).toBeGreaterThan(0)
    for (const rule of manifest.rules) {
      expect(typeof rule.selector).toBe('string')
      expect(rule.selector.length).toBeGreaterThan(0)
      expect(typeof rule.carried).toBe('boolean')
    }
  })

  it('enumerates exactly the rules its own stats claim', () => {
    // Ties the two halves together: if a future change writes one and not the other, the manifest is
    // internally inconsistent and this fails without needing the C# stylesheet at all.
    const carried = manifest.rules.filter((r) => r.carried)
    const droppedRoot = manifest.rules.filter((r) => !r.carried)
    const keyframes = carried.filter((r) => r.selector.startsWith('@keyframes '))
    expect(droppedRoot).toHaveLength(manifest.stats.droppedRoot)
    expect(keyframes).toHaveLength(manifest.stats.carriedKeyframes)
    expect(carried.length - keyframes.length).toBe(manifest.stats.carriedRules)
  })
})
