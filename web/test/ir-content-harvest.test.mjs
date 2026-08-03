import { describe, expect, it } from 'vitest'

import { harvest } from '../scripts/ir-content-build.mjs'

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
