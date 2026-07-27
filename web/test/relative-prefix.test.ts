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
    expect(relativePrefixFor('/epics/epic-3.html')).toBe('../')
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
  const irPaths = [
    'index.html',
    'epics.html',
    'epics/epic-3.html',
    'epics/story-23-5.html',
    'adrs/0006-delivery-architecture-and-distribution.html',
    'code/src/SpecScribe/Charts.cs.html',
    'code/web/scripts/harness-lib.mjs.html',
    'specs/spec-specscribe/ARCHITECTURE-SPINE.html',
  ]

  it.each(irPaths)('agrees on %s', (irPath) => {
    expect(relativePrefixFor(`/${irPath}`)).toBe(relativePrefix(irPath))
  })
})
