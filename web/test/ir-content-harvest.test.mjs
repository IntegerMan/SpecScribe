import { describe, expect, it } from 'vitest'

import { harvest } from '../scripts/ir-content-build.mjs'
import { conditionalClassNames, selectorIsUsed } from '../scripts/ir-content-lib.mjs'

/** Same shape `harvest` fills, for the seeding tests below. */
const collectInto = (html) => {
  const into = { classes: new Set(), ids: new Set(), attributes: new Set(), elements: new Set() }
  harvest(html, into)
  return into
}

/**
 * `harvest` collects the classes, ids, attributes and elements the IR actually renders; `selectorIsUsed` then
 * drops any stylesheet rule naming something it did not see.
 *
 * ⚠️ WHY THIS FILE EXISTS AT ALL, given `check:ir-content` already guards the layer.
 *
 * `check:ir-content` re-derives `ir-content.css` through THIS SAME function and diffs the result against the
 * committed file. That makes it a superb guard against the committed file going stale — and structurally blind to
 * a bug in the derivation itself: extractor and gate agree by construction, so a rule wrongly dropped is dropped
 * in both and the gate stays green.
 *
 * It stayed green through a real one. `harvest` was written brace-free:
 *
 *     if (name === 'class') for (const c of value.split(/\s+/)) if (c) into.classes.add(c)
 *     else if (name === 'id' && value) into.ids.add(value)
 *
 * where the `else` binds to the inner `if (c)` rather than to `if (name === 'class')`. The id branch could only
 * run when the attribute was `class` AND one of its split tokens was falsy — never. `into.ids` was empty for the
 * entire site, so every id-bearing selector was dropped, and the Code Map's pure-CSS spec/test filter
 * (`#cm-exclude-spec:checked ~ …`) never reached the shipped stylesheet: the two checkboxes on the page did
 * nothing, with no failing test anywhere. Found 2026-08-01 by reading computed styles in a live browser.
 *
 * So these tests assert on the FUNCTION's output directly, which is the one thing the shared-derivation gate
 * cannot check.
 */
describe('harvest', () => {
  /** @returns {{classes: Set<string>, ids: Set<string>, attributes: Set<string>, elements: Set<string>}} */
  const collect = (html) => {
    const into = { classes: new Set(), ids: new Set(), attributes: new Set(), elements: new Set() }
    harvest(html, into)
    return into
  }

  it('collects ids — the branch a dangling else silently disabled', () => {
    const used = collect('<input type="checkbox" id="cm-exclude-spec" class="codemap-filter-checkbox">')

    expect(used.ids.has('cm-exclude-spec')).toBe(true)
    // …and the class on the SAME element still lands, i.e. the two branches are genuinely exclusive rather than
    // one having been fixed at the other's expense.
    expect(used.classes.has('codemap-filter-checkbox')).toBe(true)
  })

  it('collects ids from elements that carry no class at all', () => {
    // The regression's sharpest edge: with the dangling else, an id could only ever have been reached through the
    // `class` branch, so an element with no class was invisible twice over.
    const used = collect('<div id="site-nav-links"></div>')

    expect(used.ids.has('site-nav-links')).toBe(true)
  })

  it('ignores an empty id rather than seeding a blank token', () => {
    // `#` alone is not a selector, and a blank entry in `used.ids` would make `selectorIsUsed` answer nonsense.
    const used = collect('<div id=""></div>')

    expect(used.ids.has('')).toBe(false)
    expect(used.ids.size).toBe(0)
  })

  it('splits multi-class attributes and keeps every token', () => {
    const used = collect('<details class="codemap-tree-dir dir-all-spec dir-all-excluded"></details>')

    expect([...used.classes].sort()).toEqual(['codemap-tree-dir', 'dir-all-excluded', 'dir-all-spec'])
  })

  it('collects elements and attribute names alongside', () => {
    const used = collect('<table class="codemap-table"><tr class="codemap-table-row" data-codemap-view="full">')

    expect(used.elements.has('table')).toBe(true)
    expect(used.elements.has('tr')).toBe(true)
    expect(used.attributes.has('data-codemap-view')).toBe(true)
  })

  it('carries the exact markup the Code Map filter selectors depend on', () => {
    // The concrete regression, end to end at this layer: both checkbox ids AND the classes the filter's other
    // compound parts name. A selector is dropped unless EVERY token it names was harvested, so all five have to
    // be present for `#cm-exclude-spec:checked ~ .codemap-table-section .codemap-tree-dir.dir-all-spec` to survive.
    const used = collect(
      '<input type="checkbox" id="cm-exclude-spec">' +
      '<input type="checkbox" id="cm-exclude-tests">' +
      '<section class="chart-panel codemap-table-section">' +
      '<details class="codemap-tree-dir dir-all-spec"></details>' +
      '<tr class="codemap-table-row is-spec">',
    )

    for (const id of ['cm-exclude-spec', 'cm-exclude-tests']) expect(used.ids.has(id)).toBe(true)
    for (const cls of ['codemap-table-section', 'codemap-tree-dir', 'dir-all-spec', 'codemap-table-row', 'is-spec']) {
      expect(used.classes.has(cls)).toBe(true)
    }
  })
})

/**
 * The seeded half of the derivation. `harvest` answers "what does the IR render RIGHT NOW", and for a class whose
 * presence depends on project DATA — or, since Story 12.2, on which FRAMEWORK produced the project — that is the
 * wrong question. `CONDITIONAL_CLASSES` seeds the closed domain instead.
 *
 * ⚠️ These assertions exist because the round-trip gate cannot make them. `check:ir-content` re-derives through the
 * same seed list, so a class MISSING from it is missing on both sides and the diff is empty — the rule is simply
 * absent from the shipped stylesheet, with every gate green and the element rendering bare.
 */
describe('cross-framework conditional classes [Story 12.2]', () => {
  /**
   * The epics index renders milestone bands only for a framework that HAS a milestone level above the epic — GSD
   * Core. This repository is a BMad project, and the extraction corpus is this repository's own IR, so no harvest
   * run here can ever see this markup. That is a different gap from the data-conditional entries beside it: those
   * are absent because a condition is false today, these because the corpus is the wrong framework, and no amount
   * of regenerating changes it.
   *
   * Measured when the seed was added: without it all five rules were pruned and `check:ir-content` stayed GREEN,
   * so the bands would have shipped unstyled on a real GSD site.
   */
  it('seeds the milestone-band classes, which a BMad corpus can never harvest', () => {
    const seeded = new Set(conditionalClassNames())
    for (const cls of [
      'milestone-band',
      'milestone-band-header',
      'milestone-band-name',
      'milestone-band-meta',
      'milestone-band-empty',
    ]) {
      expect(seeded.has(cls)).toBe(true)
    }
  })

  /**
   * Seeding stays SELF-LIMITING, which is what keeps the list from carrying rules by association: a rule is
   * emitted only when EVERY token it names was seen or seeded. `.milestone-band .epic-overview` therefore rides on
   * `epic-overview` being genuinely present in the corpus, not on the seed alone.
   */
  it('does not seed the classes the band composes with', () => {
    const seeded = new Set(conditionalClassNames())
    expect(seeded.has('epic-overview')).toBe(false)
    expect(seeded.has('epic-chip')).toBe(false)

    const used = collectInto('<div class="epic-overview"><a class="epic-chip done"></a></div>')
    expect(selectorIsUsed('.milestone-band .epic-overview', used)).toBe(false)
    used.classes.add('milestone-band')
    expect(selectorIsUsed('.milestone-band .epic-overview', used)).toBe(true)
  })
})
