/**
 * The depth rule that makes the emitted portal work from `file://`. [Story 23.5 AC #7]
 *
 * SpecScribe's portal is a relative file tree, routinely opened from `file://` and copied to a USB stick
 * (ADR 0012 §Decision 1; EXPERIENCE.md:270). Nuxt's default root-absolute `/_nuxt/…` asset URLs break both
 * that and any subdirectory deployment, so `server/plugins/relative-asset-urls.ts` rewrites them to a
 * page-relative prefix. Get the depth wrong and every page below the root silently loses its stylesheet —
 * a defect the test suite structurally cannot see (which is why CLAUDE.md also requires a live-browser
 * check), so the arithmetic itself is pinned here.
 */
import { describe, expect, it } from 'vitest'
import { relativePrefixFor } from '../server/utils/relative-prefix'
import { relativePrefix } from '../ir/adapter'

describe('relativePrefixFor — route space', () => {
  it('is empty at the site root', () => {
    expect(relativePrefixFor('/')).toBe('')
    expect(relativePrefixFor('/index.html')).toBe('')
  })

  it('climbs one level per directory for .html routes', () => {
    expect(relativePrefixFor('/epics.html')).toBe('')
    expect(relativePrefixFor('/epics/epic-3.html')).toBe('../')
    expect(relativePrefixFor('/adrs/0012-plotly.html')).toBe('../')
    expect(relativePrefixFor('/code/src/SpecScribe/Charts.cs.html')).toBe('../../../')
  })

  it('adds a level for EXTENSION-LESS routes, which Nitro writes as <route>/index.html', () => {
    // The trap this test exists for: `/design-system` is written to `design-system/index.html`, one
    // directory deeper than the route string suggests. Treating it like a file gives `''` and 404s.
    expect(relativePrefixFor('/design-system')).toBe('../')
    expect(relativePrefixFor('/component-library')).toBe('../')
    expect(relativePrefixFor('/measure/async')).toBe('../../')
  })

  it('ignores a query string', () => {
    // This assertion used to carry NO query string — it was a byte-for-byte duplicate of the case above, so
    // the `split('?')` branch had zero coverage while appearing tested: deleting the split left the suite
    // green. A gate that cannot fail for its stated reason. [Story 23.5 code review 2026-08-08]
    expect(relativePrefixFor('/epics/epic-3.html?v=2')).toBe('../')
    expect(relativePrefixFor('/measure/async?debug=1')).toBe('../../')
  })

  it('ignores a fragment', () => {
    // Same class as the query string: `#risks` made the route miss the `.html` branch entirely and yielded
    // '../../' — one level too deep — for a page that is one directory down.
    expect(relativePrefixFor('/epics/epic-3.html#risks')).toBe('../')
    expect(relativePrefixFor('/epics/epic-3.html?v=2#risks')).toBe('../')
  })

  it('strips a TRAILING slash instead of counting it as a directory', () => {
    // Vue Router is non-strict by default, so `GET /component-library/` resolves to a real 200 page. The
    // empty final segment was counted as a directory, giving '../../' where the output file
    // `component-library/index.html` needs '../' — and every asset on that response 404'd, which is exactly
    // the failure the module exists to prevent. [Story 23.5 code review 2026-08-08]
    expect(relativePrefixFor('/component-library/')).toBe('../')
    expect(relativePrefixFor('/design-system/')).toBe('../')
    expect(relativePrefixFor('/measure/async/')).toBe('../../')
    expect(relativePrefixFor('/epics/epic-3.html/')).toBe('../')
  })

  it('tolerates a doubled leading slash rather than counting it as depth', () => {
    expect(relativePrefixFor('//epics/epic-3.html')).toBe('../')
  })

  it('is case-insensitive about the .html extension', () => {
    expect(relativePrefixFor('/epics/EPIC-3.HTML')).toBe('../')
  })
})

describe('agreement with the adapter (and, through it, PathUtil.RelativePrefix)', () => {
  // Three implementations of one rule — C# `PathUtil.RelativePrefix`, `ir/adapter.ts`'s `relativePrefix`,
  // and the Nitro plugin's route-space variant — is one too many. This pins the two JS ones together so a
  // change to either fails here instead of silently diverging in emitted markup.
  //
  // ⚠️ READ WHAT THIS CAN AND CANNOT DO. It pins AGREEMENT, not correctness: both functions could share the
  // same bug and stay green. The depth arithmetic itself is pinned by the route-space suite above.
  //
  // It used to be eight hand-written literals, which could not detect divergence at depth ≥4, on a segment
  // containing a dot, or on an extension-less route — and extension-less is the one case the module docblock
  // says they differ on BY DESIGN, i.e. precisely where an unintended divergence would hide. The corpus is
  // now generated across depth 0–6 and the documented divergence is pinned as an explicit expectation rather
  // than left as an untested assumption. [Story 23.5 code review 2026-08-08]
  const realIrPaths = [
    'index.html',
    'epics.html',
    'epics/epic-3.html',
    'epics/story-23-5.html',
    'adrs/0006-delivery-architecture-and-distribution.html',
    'code/src/SpecScribe/Charts.cs.html',
    'code/web/scripts/harness-lib.mjs.html',
    'specs/spec-specscribe/ARCHITECTURE-SPINE.html',
  ]

  // Depth 0–6, including segments carrying dots (`Charts.cs.html`, `harness-lib.mjs.html` are real shapes)
  // and a doubled extension, which the literal list could not reach.
  const generatedIrPaths = Array.from({ length: 7 }, (_, depth) => {
    const dirs = Array.from({ length: depth }, (_, i) => `seg${i}.dir`)
    return [...dirs, 'Page.name.with.dots.html'].join('/')
  })

  it.each([...realIrPaths, ...generatedIrPaths])('agrees on %s', (irPath) => {
    expect(relativePrefixFor(`/${irPath}`)).toBe(relativePrefix(irPath))
  })

  it('diverges by exactly one level on extension-less routes, as documented', () => {
    // The adapter never sees these (every IR route carries `.html` verbatim), and the route-space function
    // must add a level because Nitro writes `<route>/index.html`. The divergence is intentional — pinned so
    // that a change which ACCIDENTALLY aligns or widens it fails here instead of silently altering markup.
    for (const route of ['design-system', 'component-library', 'measure/async', 'a/b/c']) {
      const routeSpace = relativePrefixFor(`/${route}`)
      const adapterSpace = relativePrefix(route)
      expect(routeSpace).toBe(`../${adapterSpace}`)
    }
  })
})
