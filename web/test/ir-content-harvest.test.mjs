import { readFileSync } from 'node:fs'

import { describe, expect, it } from 'vitest'

import { harvest } from '../scripts/ir-content-build.mjs'
import {
  conditionalClassNames,
  isRuntimeBodyClass,
  isSharedPrimitive,
  missingTokens,
  readBlocks,
  RUNTIME_BODY_CLASSES,
  selectorIsUsed,
  SHARED_PRIMITIVES,
  SOURCE_CSS,
  stripComments,
} from '../scripts/ir-content-lib.mjs'

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

/**
 * Classes `specscribe.js` applies at RUNTIME. Same silent-loss mechanism as the block above, reached by a third
 * route — and the one that actually shipped: on 2026-08-06 the hover tooltip was an unstyled `<div>` and the
 * dashboard's details rail went BLANK when a node was selected, because the rules those behaviours depend on
 * name classes that only ever exist after the page has run.
 *
 * ⚠️ Each assertion here is one the round-trip gate structurally cannot make. `check:ir-content` re-derives
 * through this same seed list, so a class missing from it is missing on both sides, the diff is empty, and the
 * styling is simply absent from the shipped site. That is not hypothetical for any entry below.
 */
describe('runtime-applied classes [ADR 0039]', () => {
  /**
   * INSIDE `.ir-content`, so these belong in the scoped layer and are seeded like any other conditional class.
   *
   * `is-related-current` is the one that cost the most. `specscribe.js` toggles it onto the single details-rail
   * card matching the selected node, and the pruned rule was the `display: block` that un-hides it. Its
   * `display: none` sibling names only classes the server renders, so IT survived — leaving a stylesheet that
   * hides every card and reveals none. Selecting a sunburst node emptied the rail.
   */
  it('seeds the classes specscribe.js applies inside the content wrapper', () => {
    const seeded = new Set(conditionalClassNames())
    for (const cls of [
      'is-related-current', //   the details rail's reveal hook
      'ss-hierarchy-sector', //  the tooltip/hover/focus hook on every Plotly sector
      'ss-hierarchy-probe', //   the colour probe; without it no-plan resolves to the Pending colour
      'ss-hierarchy-sw', //      the legend swatches that explain the hatching
      'ss-hierarchy-crumb', //   the drill breadcrumb
      'slice', //                Plotly's own, on the sector group
      'surface', //              Plotly's own, on the sector path — together these carry the focus ring
    ]) {
      expect(seeded.has(cls)).toBe(true)
    }
  })

  /**
   * The concrete regression, asserted at the layer that caused it: with the seed the reveal rule survives
   * derivation, and without it it does not. Both directions, because "it passes now" is not evidence that the
   * seed is what makes it pass.
   */
  it('carries the details-rail reveal rule, and drops it without the seed', () => {
    const REVEAL = '[data-related-ready] .related-card[data-related-node].is-related-current'
    // What the server actually renders: the card, but never the JS-applied state class.
    const used = collectInto(
      '<div data-related-pane><div class="related-card" data-related-node="epic-20"></div></div>',
    )

    expect(selectorIsUsed(REVEAL, used)).toBe(false)
    expect(missingTokens(REVEAL, used).classes).toEqual(['is-related-current'])

    for (const name of conditionalClassNames()) used.classes.add(name)
    expect(selectorIsUsed(REVEAL, used)).toBe(true)

    // And its `display: none` counterpart never needed the seed — which is exactly why the pair broke
    // ASYMMETRICALLY and left the rail hiding everything.
    const HIDE = '[data-related-ready] .related-card[data-related-node]'
    expect(selectorIsUsed(HIDE, collectInto('<div class="related-card" data-related-node="x"></div>'))).toBe(true)
  })

  /**
   * OUTSIDE `.ir-content`, so seeding alone would not have been enough: the rule would be carried and then
   * nested under an ancestor the node does not have. These go to the unscoped layer instead.
   */
  it('routes the body-level tooltip families to the unscoped layer', () => {
    for (const sel of ['.ss-tooltip', '.ss-hierarchy-card', '.ss-hierarchy-card-kind', '.codemap-card-name']) {
      expect(isRuntimeBodyClass(sel)).toBe(true)
      // Never claimed by the OTHER unscoped layer — the two answer different questions.
      expect(isSharedPrimitive(sel)).toBe(false)
    }
  })

  /**
   * Same all-or-nothing containment rule the shared-primitive layer has. A selector that reached from the tip
   * node back into page content must NOT escape scoping just because one of its classes is on the list.
   */
  it('keeps the unscoped layer bounded — every named class must be on the allowlist', () => {
    expect(isRuntimeBodyClass('.ss-tooltip .related-card')).toBe(false)
    expect(isRuntimeBodyClass('.ss-hierarchy-card .pill')).toBe(false)
    expect(isRuntimeBodyClass('#main-content .ss-tooltip')).toBe(false)
    expect(isRuntimeBodyClass('div')).toBe(false)
    // A compound of two allowlisted classes is still in.
    expect(isRuntimeBodyClass('.ss-tooltip.ss-hierarchy-card')).toBe(true)
  })

  /**
   * The two unscoped allowlists must stay disjoint. If a class ever appeared on both, the builder's three-way
   * partition would emit its rule into whichever layer it tested first — a silent, order-dependent choice, and
   * "exactly one definition" would quietly stop being true.
   */
  it('keeps the two unscoped allowlists disjoint', () => {
    const shared = new Set(SHARED_PRIMITIVES)
    expect(RUNTIME_BODY_CLASSES.filter((c) => shared.has(c))).toEqual([])
  })

  /**
   * `missingTokens` is the reporting companion, and it has to agree with the function it explains: whenever
   * `selectorIsUsed` says no because of a token, `missingTokens` must name that token. A drift between them
   * would make the drop report point at the wrong class, which is worse than not reporting at all.
   */
  it('names the token that caused a drop, agreeing with selectorIsUsed', () => {
    const used = collectInto('<div class="sunburst-panel"></div>')

    expect(selectorIsUsed('.sunburst-panel .sb-seg', used)).toBe(false)
    expect(missingTokens('.sunburst-panel .sb-seg', used).classes).toEqual(['sb-seg'])
    expect(missingTokens('.sunburst-panel', used).classes).toEqual([])
    expect(missingTokens('#cm-exclude-spec .sunburst-panel', used).ids).toEqual(['cm-exclude-spec'])
  })
})

/**
 * `readBlocks` reports a `startLine`/`endLine` span for every block. Story 23.3's code review replaced the
 * O(n²) implementation of that span — `css.slice(0, offset).split('\n').length`, a full prefix copy and split
 * on every call — with an index built once and binary-searched. On the real stylesheet that took `readBlocks`
 * from 445 ms to 7 ms, 99% of which was this one helper.
 *
 * These tests pin the SEMANTICS the rewrite had to preserve, stated independently of either implementation:
 * the line number of an offset is the count of newlines strictly before it, plus one. They belong in this
 * file for the reason its header gives — `check:ir-content` re-derives through the same code it checks, so it
 * cannot see a bug in the derivation. A span that silently shifted would land in the manifest's `within`
 * bookkeeping and in every drop report, pointing reviewers at the wrong line.
 */
describe('readBlocks line spans [Story 23.3 code review]', () => {
  /** The definition the old slice-and-split implementation computed, kept as an oracle. */
  const lineOfOracle = (css, offset) => css.slice(0, offset).split('\n').length

  it('reports 1-based lines that match a slice-and-count oracle, including the first line', () => {
    const css = 'a { color: red }\n\nb {\n  color: blue\n}\n@media screen {\n  c { top: 0 }\n}\n'
    for (const b of readBlocks(css)) {
      expect(b.startLine).toBe(lineOfOracle(css, css.indexOf(b.prelude)))
      expect(b.endLine).toBeGreaterThanOrEqual(b.startLine)
    }
    expect(readBlocks(css)[0].startLine).toBe(1)
  })

  it('starts a block at its prelude, not at the whitespace before it', () => {
    // The indent width used to be measured by slicing the whole remaining stylesheet twice.
    const css = 'x { a: 1 }\n\n\n     \n  y { b: 2 }\n'
    const [, second] = readBlocks(css)
    expect(second.prelude).toBe('y')
    expect(second.startLine).toBe(5)
  })

  it('agrees with the oracle on every block of the real stylesheet', () => {
    const css = stripComments(readFileSync(SOURCE_CSS, 'utf8').replace(/\r\n/g, '\n'))
    const blocks = readBlocks(css)
    // Guards the guard: a corpus this thin would make the agreement below vacuous.
    expect(blocks.length).toBeGreaterThan(500)

    let cursor = 0
    for (const b of blocks) {
      const at = css.indexOf(b.prelude, cursor)
      if (at < 0) continue
      expect(b.startLine).toBe(lineOfOracle(css, at))
      cursor = at + b.prelude.length
    }
    // Spans are monotonic in source order — the property a shifted index would break first.
    for (let k = 1; k < blocks.length; k += 1) {
      expect(blocks[k].startLine).toBeGreaterThanOrEqual(blocks[k - 1].startLine)
    }
  })
})
