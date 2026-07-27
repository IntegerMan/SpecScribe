/**
 * `splitContentRegion` — the inverse of `SpaDelivery.ExtractContentRegion`. [Story 23.3 AC #1/#2]
 *
 * ── Why this function is worth pinning ─────────────────────────────────────────────────────────────────
 *
 * THE IR CARRIES TWO DIFFERENT REGION SHAPES, and Story 23.3 recorded what happens when they are treated as
 * one: `<main>` ends up NESTED inside the wayfinding band on 187 pages, producing broken markup that no
 * `<main>`-region comparison can detect — every harness passed while the DOM was corrupt. The two shapes:
 *
 *   · RE-RENDERED pages (the dashboard/epics families) carry the whole wayfinding band, wrapper and all.
 *     Balanced.
 *   · CAPTURED pages go through `ExtractContentRegion`, which begins its slice INSIDE the wrapper at
 *     `<div class="breadcrumb"`. Those regions carry the wrapper's closing `</div>` without its opener and
 *     are unbalanced by exactly one element.
 *
 * The repair is deliberately conditional — it fires only when the slice is genuinely unbalanced — so that a
 * future emitter fix makes it stop firing on its own instead of double-wrapping. That conditionality is the
 * behaviour most at risk from a well-meaning edit, so it is asserted in both directions here.
 *
 * These fixtures are hand-built rather than read from a generated portal, because `vitest.config.ts` runs
 * with `SPECSCRIBE_PACKAGE_BUILD=1` and there is deliberately no IR on disk during a unit run.
 */
import { describe, expect, it } from 'vitest'
import { splitContentRegion } from '../ir/adapter'

const NAV = '<nav class="site-nav" aria-label="Document navigation"><a href="index.html">Home</a></nav>'
const CRUMB = '<div class="breadcrumb"><a href="../index.html">Home</a></div>'
const PAGER = '<nav class="entity-pager"><a href="epic-2.html">Prev</a></nav>'
const MAIN_OPEN = '<main id="main-content" class="doc" data-ir-family="epicDetail">'
const BODY = '<h1>Epic 3</h1><p>Body copy.</p>'

/** The RE-RENDERED shape: the wayfinding wrapper is present and balanced. */
const reRendered = `${NAV}<div class="page-wayfinding">\n${CRUMB}${PAGER}</div>${MAIN_OPEN}${BODY}</main>`

/** The CAPTURED shape: the slice starts at the breadcrumb, so the wrapper's `</div>` has no opener. */
const captured = `${NAV}${CRUMB}${PAGER}</div>${MAIN_OPEN}${BODY}</main>`

describe('splitContentRegion — the re-rendered shape', () => {
  const region = splitContentRegion(reRendered, 'epics/epic-3.html')

  it('keeps the nav ahead of the wayfinding band', () => {
    expect(region.navHtml).toBe(NAV)
  })

  it('captures the whole balanced band without repairing it', () => {
    expect(region.wayfindingRepaired).toBe(false)
    expect(region.wayfindingHtml).toBe(`<div class="page-wayfinding">\n${CRUMB}${PAGER}</div>`)
  })

  it('reproduces the <main> open tag attributes verbatim', () => {
    // Byte-for-byte reproduction of the open tag is what makes the parity comparison honest.
    expect(region.mainAttributes).toBe(' class="doc" data-ir-family="epicDetail"')
    expect(region.mainAttrs).toEqual({ class: 'doc', 'data-ir-family': 'epicDetail' })
  })

  it('extracts the <main> body without its wrapper tags', () => {
    expect(region.mainInnerHtml).toBe(BODY)
  })
})

describe('splitContentRegion — the captured shape', () => {
  const region = splitContentRegion(captured, 'adrs/0006-delivery.html')

  it('repairs the missing wrapper opener rather than nesting <main> inside the band', () => {
    expect(region.wayfindingRepaired).toBe(true)
    expect(region.wayfindingHtml.startsWith('<div class="page-wayfinding">\n')).toBe(true)
  })

  it('leaves the band balanced after the repair', () => {
    const opens = (region.wayfindingHtml.match(/<div\b/g) ?? []).length
    const closes = (region.wayfindingHtml.match(/<\/div>/g) ?? []).length
    expect(closes).toBe(opens)
  })

  it('produces the same nav and body as the re-rendered shape', () => {
    expect(region.navHtml).toBe(NAV)
    expect(region.mainInnerHtml).toBe(BODY)
  })
})

describe('splitContentRegion — pages with no wayfinding band', () => {
  it('treats the whole prefix as nav and leaves the band empty', () => {
    const region = splitContentRegion(`${NAV}${MAIN_OPEN}${BODY}</main>`, 'index.html')
    expect(region.wayfindingHtml).toBe('')
    expect(region.wayfindingRepaired).toBe(false)
    expect(region.navHtml).toBe(NAV)
    expect(region.mainInnerHtml).toBe(BODY)
  })

  it('ignores a breadcrumb that appears AFTER <main> rather than splitting on it', () => {
    // A breadcrumb inside the body is content, not wayfinding. Splitting on it would move part of the
    // page into the band and change what `<main>` contains.
    const html = `${NAV}${MAIN_OPEN}${BODY}<div class="breadcrumb">inside</div></main>`
    const region = splitContentRegion(html, 'about.html')
    expect(region.wayfindingHtml).toBe('')
    expect(region.mainInnerHtml).toBe(`${BODY}<div class="breadcrumb">inside</div>`)
  })
})

describe('splitContentRegion — refusals', () => {
  it('refuses a page with no <main> landmark', () => {
    // The emitter degrades a landmark-less page to nav-only rather than aborting, so this shape is real —
    // it just is not something this app can render as a page.
    expect(() => splitContentRegion(`${NAV}<p>orphan</p>`, 'broken.html')).toThrow(/no <main id="main-content">/)
  })

  it('refuses an unterminated <main>', () => {
    expect(() => splitContentRegion(`${NAV}${MAIN_OPEN}${BODY}`, 'broken.html')).toThrow(/unterminated <main>/)
  })

  it('refuses a band it cannot balance instead of nesting <main> inside it', () => {
    // Two unmatched closers: the conditional repair adds exactly one opener, so this stays unbalanced and
    // must fail loudly rather than emit a corrupt DOM.
    const html = `${NAV}${CRUMB}${PAGER}</div></div>${MAIN_OPEN}${BODY}</main>`
    expect(() => splitContentRegion(html, 'bad.html')).toThrow(/cannot balance/)
  })

  it('refuses <main> attributes it cannot reproduce exactly', () => {
    const html = `${NAV}<main id="main-content" class='single-quoted'>${BODY}</main>`
    expect(() => splitContentRegion(html, 'odd.html')).toThrow(/attributes this adapter cannot parse/)
  })
})
