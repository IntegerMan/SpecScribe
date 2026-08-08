import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { afterAll, describe, expect, it } from 'vitest'

import { danglingKey, walk } from '../scripts/harness-lib.mjs'
import { classifyAgainstBaseline, decodeHref, isSkippable, scan } from '../scripts/links-lib.mjs'

/**
 * ⚠️ WHY THIS FILE EXISTS. [Story 23.3 code review 2026-08-08]
 *
 * `check:links` is the gate for Story 23.3 AC #4, and between Story 23.6 and this review it COULD NOT FAIL.
 * It classified links by comparing the Nuxt output against the golden site C# wrote; 23.6 deleted the C#
 * writer, both sides collapsed onto the same directory (`goldenFiles = nuxtFiles = siteFiles`), and the
 * gating bucket `!resolved && goldenResolved` reduced to `!x && x` — unreachable. Every dangling link was
 * filed as "inherited" and the script exited 0 unconditionally.
 *
 * Nothing could see it, because the classifier lived inline in a script that only ran against a freshly
 * generated 1,000-page site. So the classifier is now a pure exported function, and the FIRST test below is
 * the one that would have caught it: a newly dangling link must land in `newlyDangling`, which is what the
 * gate exits non-zero on.
 */

const fixtures = mkdtempSync(join(tmpdir(), 'specscribe-links-'))
afterAll(() => rmSync(fixtures, { recursive: true, force: true }))

/** Writes a tiny site and scans it the way `check:links` and `pin:links` both do. */
function site(name, pages) {
  const root = join(fixtures, name)
  for (const [path, html] of Object.entries(pages)) {
    const full = join(root, path)
    mkdirSync(join(full, '..'), { recursive: true })
    writeFileSync(full, html, 'utf8')
  }
  return scan(root, walk(root))
}

describe('classifyAgainstBaseline [the bucket that silently became unreachable]', () => {
  const scanned = site('new-dangle', {
    'index.html': '<a href="gone.html">x</a><a href="ok.html">y</a>',
    'ok.html': 'fine',
  })

  it('puts a dangling link that is NOT in the baseline into newlyDangling — the bucket the gate exits on', () => {
    const { newlyDangling, stillDangling } = classifyAgainstBaseline(scanned.links, new Set())

    expect(newlyDangling).toEqual([{ page: 'index.html', href: 'gone.html' }])
    expect(stillDangling).toEqual([])
    // The property the old gate lost: something dangling must make the gate fail.
    expect(newlyDangling.length > 0).toBe(true)
  })

  it('carries a dangling link that IS in the baseline without failing', () => {
    const known = new Set([danglingKey('index.html', 'gone.html')])
    const { newlyDangling, stillDangling, fixed } = classifyAgainstBaseline(scanned.links, known)

    expect(newlyDangling).toEqual([])
    expect(stillDangling).toEqual([{ page: 'index.html', href: 'gone.html' }])
    expect(fixed).toEqual([])
  })

  it('reports a baseline entry that now resolves as fixed, so the baseline can shrink', () => {
    const known = new Set([danglingKey('index.html', 'gone.html'), danglingKey('index.html', 'repaired.html')])
    const { fixed } = classifyAgainstBaseline(scanned.links, known)

    expect(fixed).toEqual([{ page: 'index.html', href: 'repaired.html' }])
  })

  it('keys by page AND href, so the same bad href on a new page is NOT exempted', () => {
    const two = site('same-href', {
      'index.html': '<a href="gone.html">x</a>',
      'other.html': '<a href="gone.html">x</a>',
    })
    const known = new Set([danglingKey('index.html', 'gone.html')])
    const { newlyDangling } = classifyAgainstBaseline(two.links, known)

    expect(newlyDangling).toEqual([{ page: 'other.html', href: 'gone.html' }])
  })

  it('does not let an href containing the baseline separator forge an exemption', () => {
    // The key is `page\thref`; neither an output path nor an href attribute can carry a raw tab, so a
    // crafted href cannot straddle the separator and match another page's entry.
    const { newlyDangling } = classifyAgainstBaseline(
      new Map([[danglingKey('a.html', 'b.html\tc.html'), null]]),
      new Set([danglingKey('a.html', 'b.html')]),
    )
    expect(newlyDangling).toEqual([{ page: 'a.html', href: 'b.html\tc.html' }])
  })
})

describe('scan [resolution rules the baseline depends on]', () => {
  it('resolves relative hrefs against the page\'s own directory, not the site root', () => {
    const scanned = site('relative', {
      'epics/epic-1.html': '<a href="../index.html">home</a><a href="story-1.html">s</a>',
      'index.html': 'root',
      'epics/story-1.html': 'story',
    })
    expect(scanned.counts.dangling).toBe(0)
    expect(scanned.links.get('epics/epic-1.html\t../index.html')).toBe('index.html')
  })

  it('resolves a directory-style target to its index.html, the way a static server serves it', () => {
    const scanned = site('dir-index', {
      'index.html': '<a href="guide/">g</a>',
      'guide/index.html': 'guide',
    })
    expect(scanned.counts.dangling).toBe(0)
  })

  it('ignores the fragment and query when resolving', () => {
    const scanned = site('frag', {
      'index.html': '<a href="ok.html#section?x=1">a</a><a href="ok.html?v=2">b</a>',
      'ok.html': 'ok',
    })
    expect(scanned.counts.dangling).toBe(0)
  })

  it('counts external, anchor-only and non-navigational hrefs as skipped, never as dangling', () => {
    const scanned = site('skips', {
      'index.html':
        '<a href="https://example.com">e</a><a href="#top">t</a><a href="mailto:a@b.c">m</a>'
        + '<a href="//cdn.example.com/x">p</a><a href="tel:+1">p</a>',
    })
    expect(scanned.counts.skipped).toBe(5)
    expect(scanned.counts.internal).toBe(0)
    expect(scanned.counts.dangling).toBe(0)
  })

  it('decodes entities before resolving, so an escaped href is not falsely dangling', () => {
    expect(decodeHref('a.html?x=1&amp;y=2')).toBe('a.html?x=1&y=2')
    expect(isSkippable('javascript:void(0)')).toBe(true)
    expect(isSkippable('epics.html')).toBe(false)
  })
})
