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
  isSharedPrimitive,
  MIGRATED,
  scopeSelector,
  selectorAttributes,
  selectorTokens,
  SHARED_PRIMITIVES,
  stripComments,
} from '../scripts/ir-content-lib.mjs'

describe('isMigrated / MIGRATED', () => {
  /**
   * ⚠️ These assertions were INVERTED by Story 23.4, and the inversion is the point.
   *
   * Story 23.3 bounded the extraction to four families and this suite pinned that bound — `about.html`,
   * `code/**` and `adrs/**` all asserted `false`, correctly, because those pages were `PassThroughSurface` and
   * not claimed as migrated.
   *
   * Story 23.4 migrated them. At that moment the narrow bound stopped being conservative and became a silent
   * defect: the extractor carried rules for four families while the router rendered fourteen, so ~58 % of the
   * classes those 1,276 pages emit had no rule at all and the elements simply rendered bare. Widening the bound
   * moved class coverage from 42 % to 100 % while still dropping 393 of 1,814 source rules as unused — so the
   * layer is still bounded, still `.ir-content`-scoped and still gated. See `ir-content-lib.mjs`'s note and
   * ADR 0018 §Addendum.
   */
  it('now drives extraction off EVERY IR page, not four families', () => {
    for (const p of [
      'index.html',
      'epics.html',
      'epics/epic-3.html',
      'epics/story-23-5.html',
      // The pages that used to be excluded — all migrated by Story 23.4.
      'about.html',
      'code/src/SpecScribe/Charts.cs.html',
      'adrs/0006-delivery.html',
      'follow-ups/action-1-thing.html',
      'commit/deadbee.html',
      'requirements/fr25.html',
    ]) {
      expect(isMigrated(p), p).toBe(true)
    }
  })

  it('exposes the widened bound as a single named predicate', () => {
    // One place decides the bound, so narrowing it again means editing this and reading the note above.
    expect(Object.keys(MIGRATED)).toEqual(['wholeSite'])
    expect(MIGRATED.wholeSite('anything.html')).toBe(true)
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
      [
        'carriedKeyframes',
        'carriedRules',
        'carriedSelectors',
        'droppedRoot',
        'generatedBytes',
        // How many rules moved to the UNSCOPED shared layer. Moves only when that layer moves. [ADR 0029]
        'sharedRules',
      ].sort(),
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
    // `carried: false` now has TWO causes, and conflating them would let a shared-layer handoff be counted as
    // a root-level drop. Partition by the recorded reason so each stat is checked against its own cause.
    const notCarried = manifest.rules.filter((r) => !r.carried)
    const droppedRoot = notCarried.filter((r) => r.reason.startsWith('root-level rule'))
    const handedToShared = notCarried.filter((r) => r.reason.startsWith('shared primitive'))
    const keyframes = carried.filter((r) => r.selector.startsWith('@keyframes '))
    expect(droppedRoot).toHaveLength(manifest.stats.droppedRoot)
    expect(handedToShared).toHaveLength(manifest.stats.sharedRules)
    // Every not-carried rule falls into one of the two known causes — no silent third reason.
    expect(droppedRoot.length + handedToShared.length).toBe(notCarried.length)
    expect(keyframes).toHaveLength(manifest.stats.carriedKeyframes)
    expect(carried.length - keyframes.length).toBe(manifest.stats.carriedRules)
  })
})

// ── The unscoped shared-primitive layer ────────────────────────────────────────────────────────────────────
//
// ADR 0029 permits a BOUNDED set of rules to be emitted UNSCOPED, so template-authored Vue components can use
// shared vocabulary (`.pill`) instead of hand-retyping its declarations. The hand-retyped copy is exactly what
// drifted before this layer existed — serif instead of Courier, wrong padding, wrong tokens — so the
// properties worth pinning are the ones that keep the layer bounded and singular.
describe('isSharedPrimitive', () => {
  it('accepts a selector whose every class is on the allowlist', () => {
    expect(isSharedPrimitive('.pill')).toBe(true)
  })

  it('rejects a compound that names anything off the allowlist', () => {
    // The all-or-nothing rule. `.pill.status-draft` is an ADR-status variant that only injected markup uses,
    // so unscoping it would grow the global layer by association — the exact creep the allowlist prevents.
    expect(isSharedPrimitive('.pill.status-draft')).toBe(false)
    expect(isSharedPrimitive('.pill.pill-link')).toBe(false)
    expect(isSharedPrimitive('.list-row-chip.pill')).toBe(false)
    expect(isSharedPrimitive('.pill .ss-icon')).toBe(false)
  })

  it('rejects selectors that name no class, and any that name an id', () => {
    expect(isSharedPrimitive('table td')).toBe(false)
    expect(isSharedPrimitive(':root')).toBe(false)
    expect(isSharedPrimitive('#main-content .pill')).toBe(false)
  })

  it('ignores pseudo-classes when deciding, so a state variant of a shared class still qualifies', () => {
    // `selectorTokens` strips pseudos, so `.pill:hover` is still "only .pill" — correct: a shared primitive's
    // own hover state belongs with it, unlike a compound with a second real class.
    expect(isSharedPrimitive('.pill:hover')).toBe(true)
  })
})

describe('shared-primitives.css (generated, UNSCOPED)', () => {
  const sharedCss = readFileSync(new URL('../assets/shared-primitives.css', import.meta.url), 'utf8')
  const scopedCss = readFileSync(new URL('../assets/ir-content.css', import.meta.url), 'utf8')
  const manifest = JSON.parse(readFileSync(new URL('../assets/ir-content.manifest.json', import.meta.url), 'utf8'))
  /** The sheet's body, past the banner — so a selector mentioned in a comment cannot satisfy an assertion. */
  const body = (css) => css.slice(css.indexOf('*/') + 2)
  /** Every selector a generated sheet actually emits, one per comma-separated part, whitespace-normalized. */
  const selectorsOf = (css) =>
    [...body(css).matchAll(/([^{}]+)\{[^{}]*\}/g)]
      .flatMap((m) => m[1].split(','))
      .map((s) => s.trim().replace(/\s+/g, ' '))
      .filter(Boolean)

  it('has a non-empty allowlist, so the assertions below are not vacuous', () => {
    // ⚠️ Load-bearing. Every assertion in this block iterates the allowlist or the emitted rules, so with an
    // EMPTY allowlist they all pass without executing a single loop body — verified by emptying it during this
    // story and watching only the `isSharedPrimitive` unit tests go red. That is precisely the by-construction
    // vacuity the 23.2 re-review was called in to find, so it gets a guard rather than a comment.
    expect(SHARED_PRIMITIVES.length).toBeGreaterThan(0)
    expect(manifest.sharedPrimitives.rules.length).toBeGreaterThan(0)
  })

  it('carries no `.ir-content` scope on any rule — that is the whole point of the layer', () => {
    // If this ever fails the layer has silently become a second copy of its sibling, and every reason for it
    // to exist (reaching template-authored components) is gone.
    expect(body(sharedCss)).not.toContain('.ir-content')
  })

  it('holds exactly ONE definition of each shared class, not a duplicate in both sheets', () => {
    // `.pill` moved out of the scoped layer rather than being copied into both. An unscoped rule still matches
    // inside `.ir-content`, so injected markup keeps its styling from this one definition.
    const shared = selectorsOf(sharedCss)
    const scoped = selectorsOf(scopedCss)
    for (const cls of SHARED_PRIMITIVES) {
      expect(shared, `.${cls} must be defined in the shared layer`).toContain(`.${cls}`)
      expect(scoped, `.${cls} must NOT also be scoped — that would be two definitions`).not.toContain(
        `.ir-content .${cls}`,
      )
    }
  })

  it('emits every rule the manifest enumerates, and enumerates every rule it emits', () => {
    const emitted = [...body(sharedCss).matchAll(/([^{}]+)\{[^{}]*\}/g)].map((m) =>
      m[1].trim().replace(/\s+/g, ' ').replace(/,\s*/g, ', '),
    )
    expect(emitted).toEqual(manifest.sharedPrimitives.rules.map((r) => r.selector))
    expect(emitted).toHaveLength(manifest.sharedPrimitives.stats.rules)
  })

  it('records the handoff in the scoped layer rather than letting the rule vanish from its list', () => {
    // Story 23.4 retires both layers off ONE list. A rule that moved must still appear where it used to be,
    // with a reason — otherwise the migration surface silently shrinks.
    for (const rule of manifest.sharedPrimitives.rules) {
      const handoff = manifest.rules.find((r) => r.selector === rule.selector && !r.carried)
      expect(handoff, `no handoff recorded for ${rule.selector}`).toBeDefined()
      expect(handoff.reason).toContain('shared primitive')
    }
  })

  it('commits only stats that move when this sheet moves', () => {
    // The same committed-fields rule its sibling learned the hard way: a whole-corpus or whole-source counter
    // here would red CI on a commit that could not have touched the layer.
    expect(Object.keys(manifest.sharedPrimitives.stats).sort()).toEqual(['generatedBytes', 'rules'])
    expect(manifest.sharedPrimitives.stats.generatedBytes).toBe(Buffer.byteLength(sharedCss))
  })

  it('publishes the allowlist, because the allowlist IS the boundary', () => {
    expect(manifest.sharedPrimitives.allowlist).toEqual(SHARED_PRIMITIVES)
    expect(manifest.sharedPrimitives.unscoped).toBe(true)
  })
})
