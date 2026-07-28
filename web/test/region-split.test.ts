/**
 * `splitContentRegion` — the inverse of `SpaDelivery.ExtractContentRegion`. [Story 23.3 AC #1/#2]
 *
 * ── Why this function is worth pinning ─────────────────────────────────────────────────────────────────
 *
 * THE IR USED TO CARRY TWO DIFFERENT REGION SHAPES, and Story 23.3 recorded what happens when they are
 * treated as one: `<main>` ends up NESTED inside the wayfinding band on 187 pages, producing broken markup
 * that no `<main>`-region comparison can detect — every harness passed while the DOM was corrupt. The two
 * shapes were:
 *
 *   · RE-RENDERED pages (the dashboard/epics families) carried the whole wayfinding band, wrapper and all.
 *     Balanced.
 *   · CAPTURED pages went through `ExtractContentRegion`, which began its slice INSIDE the wrapper at
 *     `<div class="breadcrumb"`. Those regions carried the wrapper's closing `</div>` without its opener
 *     and were unbalanced by exactly one element — 594 of this repo's 1,400 pages.
 *
 * **Story 22.4 collapsed them to ONE shape at the emitter**: `ExtractContentRegion` now slices from the
 * band's outermost marker, so a captured page with a pager carries the wrapper exactly like a re-rendered
 * one. The conditional repair and the "cannot balance" throw this file used to pin are DELETED — a repair
 * that can no longer fire is a second, drifting truth about a boundary the emitter owns.
 *
 * What is pinned now is that the split is a faithful INVERSION of the emitter's rule: outermost marker that
 * precedes `<main>`, band verbatim, `<main>` open tag reproduced byte-for-byte. The balance invariant itself
 * is asserted where it is now enforced — `SiteGeneratorSpaTests
 * .EveryIrRegion_HasOneBalancedWayfindingBand_AndExactlyOneMainLandmark` over the whole emitted IR, and
 * `npm run check:a11y`'s `one-main` / `wayfinding-single` / `wayfinding-closed` over the emitted HTML.
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

/**
 * The CAPTURED shape, post-22.4: the emitter slices from the wrapper, so this is now the SAME shape the
 * re-rendered path produces. Kept as a distinct fixture (different path, different describe) because the two
 * still travel through different producers in C# and this is what pins them to one shape here.
 */
const captured = `${NAV}<div class="page-wayfinding">\n${CRUMB}${PAGER}</div>${MAIN_OPEN}${BODY}</main>`

/** A page with no pager: the band is the bare breadcrumb, and it is balanced on its own. */
const bareCrumb = `${NAV}${CRUMB}${MAIN_OPEN}${BODY}</main>`

describe('splitContentRegion — the re-rendered shape', () => {
  const region = splitContentRegion(reRendered, 'epics/epic-3.html')

  it('keeps the nav ahead of the wayfinding band', () => {
    expect(region.navHtml).toBe(NAV)
  })

  it('captures the whole balanced band verbatim', () => {
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

  it('is the SAME shape the re-rendered path produces — no repair needed', () => {
    // The whole point of the 22.4 emitter fix: a captured page with a pager now arrives already balanced.
    expect(region.wayfindingHtml).toBe(`<div class="page-wayfinding">\n${CRUMB}${PAGER}</div>`)
    const opens = (region.wayfindingHtml.match(/<div\b/g) ?? []).length
    const closes = (region.wayfindingHtml.match(/<\/div>/g) ?? []).length
    expect(closes).toBe(opens)
  })

  it('produces the same nav and body as the re-rendered shape', () => {
    expect(region.navHtml).toBe(NAV)
    expect(region.mainInnerHtml).toBe(BODY)
  })
})

describe('splitContentRegion — a band with no pager', () => {
  const region = splitContentRegion(bareCrumb, 'about.html')

  it('takes the bare breadcrumb as the whole band', () => {
    expect(region.wayfindingHtml).toBe(CRUMB)
    expect(region.navHtml).toBe(NAV)
    expect(region.mainInnerHtml).toBe(BODY)
  })
})

describe('splitContentRegion — pages with no wayfinding band', () => {
  it('treats the whole prefix as nav and leaves the band empty', () => {
    const region = splitContentRegion(`${NAV}${MAIN_OPEN}${BODY}</main>`, 'index.html')
    expect(region.wayfindingHtml).toBe('')
    expect(region.navHtml).toBe(NAV)
    expect(region.mainInnerHtml).toBe(BODY)
  })

  it('ignores a breadcrumb that appears AFTER <main> rather than splitting on it', () => {
    // A breadcrumb inside the body is content, not wayfinding. Splitting on it would move part of the
    // page into the band and change what `<main>` contains. The emitter applies the same "precedes <main>"
    // rule, so this is an inversion of its behaviour, not an independent guess.
    const html = `${NAV}${MAIN_OPEN}${BODY}<div class="breadcrumb">inside</div></main>`
    const region = splitContentRegion(html, 'about.html')
    expect(region.wayfindingHtml).toBe('')
    expect(region.mainInnerHtml).toBe(`${BODY}<div class="breadcrumb">inside</div>`)
  })

  it('ignores a page-wayfinding wrapper that appears AFTER <main>', () => {
    // Same rule, the other marker — a design-system page documenting the wrapper is content, not wayfinding.
    const html = `${NAV}${MAIN_OPEN}${BODY}<div class="page-wayfinding">sample</div></main>`
    const region = splitContentRegion(html, 'design-system.html')
    expect(region.wayfindingHtml).toBe('')
    expect(region.mainInnerHtml).toBe(`${BODY}<div class="page-wayfinding">sample</div>`)
  })
})

describe('splitContentRegion — the degraded (landmark-less) shape', () => {
  // ADR 0024 §Decision 3 keeps a landmark-less page IN the IR (the SPA retains what the webview skips), so
  // this shape is real and a consumer must be able to skip it. It used to throw — and because Nuxt prerenders
  // every route from the manifest, that turned one bad page into a whole-site build failure, which is the
  // opposite of what §Decision 3 intends. [Story 22.4 code review — owner decision DR2]
  const region = splitContentRegion(`${NAV}<p>orphan</p>`, 'broken.html')

  it('flags it as degraded instead of throwing', () => {
    expect(region.degraded).toBe(true)
  })

  it('yields no page body, so a consumer that ignores the flag renders nothing rather than half a page', () => {
    expect(region.mainInnerHtml).toBe('')
    expect(region.wayfindingHtml).toBe('')
    expect(region.mainAttributes).toBe('')
    expect(region.mainAttrs).toEqual({})
  })

  it('keeps a well-formed page unflagged', () => {
    expect(splitContentRegion(`${NAV}${MAIN_OPEN}${BODY}</main>`, 'index.html').degraded).toBe(false)
  })
})

describe('splitContentRegion — refusals', () => {
  it('refuses an unterminated <main>', () => {
    expect(() => splitContentRegion(`${NAV}${MAIN_OPEN}${BODY}`, 'broken.html')).toThrow(/unterminated <main>/)
  })

  it('refuses <main> attributes it cannot reproduce exactly', () => {
    const html = `${NAV}<main id="main-content" class='single-quoted'>${BODY}</main>`
    expect(() => splitContentRegion(html, 'odd.html')).toThrow(/attributes this adapter cannot parse/)
  })
})
