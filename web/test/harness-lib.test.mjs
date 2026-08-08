/**
 * `scripts/harness-lib.mjs` — the shared primitives every `web/` gate compares with.
 *
 * These functions decide what counts as a parity FAILURE across `measure:parity`, `check:links` and Story
 * 23.5's `experiment:two-ir`. `normalizeVolatile` in particular is the one that can invalidate a whole
 * measurement in either direction: normalize too little and every run reports a false delta on the wall
 * clock alone; normalize too much and a real regression is scrubbed out before anyone sees it. Both
 * failure modes are silent, so the rules are pinned here rather than trusted.
 */
import { describe, expect, it } from 'vitest'
import { excerpt, firstDifference, kb, mainRegion, normalizeVolatile, pad } from '../scripts/harness-lib.mjs'

describe('mainRegion', () => {
  it('returns the whole landmark element including its tags', () => {
    expect(
      mainRegion('<div>x</div><main id="main-content" class="a">body</main><footer>f</footer>'),
    ).toBe('<main id="main-content" class="a">body</main>')
  })

  it('spans newlines', () => {
    expect(mainRegion('<main id="main-content">\nline\n</main>')).toBe(
      '<main id="main-content">\nline\n</main>',
    )
  })

  it('is greedy to the LAST </main>, so a nested one cannot truncate the region', () => {
    expect(mainRegion('<main id="main-content">a<main>b</main>c</main>')).toBe(
      '<main id="main-content">a<main>b</main>c</main>',
    )
  })

  it('returns null when there is no <main>', () => {
    expect(mainRegion('<div>no landmark here</div>')).toBeNull()
  })

  /**
   * Story 23.4 NARROWED this from `/<main\b/` to the full `id="main-content"` landmark, and these two cases
   * are why. The loose pattern reported a parity delta on a page that was byte-correct.
   */
  it('does NOT match a <main> that appears inside an attribute value', () => {
    // Real page, real failure: this repo has a deferred-work item whose own title is about the landmark
    // extraction, so its page carries the literal text `<main> body…` in its meta description. `<` needs no
    // escaping inside a quoted attribute value and Nuxt's `useHead` does not escape it (C#'s PathUtil.Html
    // does), so the extractor sliced a "region" out of the <head> and reported a false delta on
    // follow-ups/deferred-the-shared-6-7-landmark-extraction-truncates-a.html.
    const html =
      '<head><meta name="description" content="… truncates a <main> body… — SpecScribe"></head>' +
      '<body><main id="main-content">real body</main></body>'
    expect(mainRegion(html)).toBe('<main id="main-content">real body</main>')
  })

  it('returns null for a bare <main> with no id, rather than guessing', () => {
    // Every page this project emits carries the universal Story 1.4 landmark, and SpaDelivery's own
    // MainLandmarkMarker is the same string — so a bare <main> is content (a doc quoting HTML), not a region.
    // Matching structure rather than text is the rule; this is the third bug of that class here.
    expect(mainRegion('<main>quoted in a doc</main>')).toBeNull()
  })
})

describe('normalizeVolatile', () => {
  it('neutralizes the generated-on footer timestamp', () => {
    const a = 'Generated on July 27, 2026 at 2:15 UTC-05:00 by SpecScribe'
    const b = 'Generated on July 26, 2026 at 9:04 UTC-05:00 by SpecScribe'
    expect(normalizeVolatile(a)).toBe(normalizeVolatile(b))
  })

  it('neutralizes the ?v= asset cache-bust', () => {
    expect(normalizeVolatile('href="specscribe.css?v=eda58185"')).toBe(
      normalizeVolatile('href="specscribe.css?v=554a8db2"'),
    )
  })

  it('neutralizes the build-derived product version', () => {
    expect(normalizeVolatile('SpecScribe v0.1.0-preview+abc</span>')).toBe(
      normalizeVolatile('SpecScribe v0.1.0-preview+def</span>'),
    )
  })

  it('normalizes CRLF and strips a leading BOM', () => {
    expect(normalizeVolatile('﻿a\r\nb')).toBe('a\nb')
  })

  it('does NOT scrub a real content difference', () => {
    // The failure mode worth guarding: over-normalization silently passes a genuine regression.
    expect(normalizeVolatile('<p>4 work items</p>')).not.toBe(normalizeVolatile('<p>5 work items</p>'))
  })
})

describe('firstDifference', () => {
  it('returns -1 for identical strings', () => {
    expect(firstDifference('abc', 'abc')).toBe(-1)
  })

  it('returns the first differing offset', () => {
    expect(firstDifference('abcdef', 'abcXef')).toBe(3)
  })

  it('returns the shorter length when one string is a prefix of the other', () => {
    expect(firstDifference('abc', 'abcdef')).toBe(3)
    expect(firstDifference('abcdef', 'abc')).toBe(3)
  })

  it('handles empty input', () => {
    expect(firstDifference('', '')).toBe(-1)
    expect(firstDifference('', 'a')).toBe(0)
  })
})

describe('excerpt', () => {
  // Returns a JSON-QUOTED slice, deliberately: a diff excerpt is printed into a measurement report, and
  // quoting is what makes trailing whitespace and newlines visible instead of invisible.
  it('windows around the offset', () => {
    expect(excerpt('0123456789', 5, 2)).toBe('"3456"')
  })

  it('clamps at the string start rather than wrapping', () => {
    expect(excerpt('0123456789', 0, 3)).toBe('"012"')
  })

  it('clamps at the string end', () => {
    expect(excerpt('0123456789', 9, 3)).toBe('"6789"')
  })

  it('escapes newlines so a whitespace-only difference is visible', () => {
    expect(excerpt('a\nb', 1, 5)).toBe('"a\\nb"')
  })
})

describe('pad', () => {
  it('pads short values to the column width', () => {
    expect(pad('ab', 5)).toBe('ab   ')
  })

  it('does not truncate values longer than the width', () => {
    // Truncating here would silently corrupt a reported path in a measurement table.
    expect(pad('abcdefg', 3)).toBe('abcdefg')
  })

  it('stringifies non-strings', () => {
    expect(pad(42, 4)).toBe('42  ')
  })
})

describe('kb', () => {
  // These assertions used to be `toMatch(/^2/)` and `typeof … === 'string'` — one character and a return
  // type. `"2"`, `"20000 bytes"` and `"2 GB"` all satisfied the first; every possible return satisfied the
  // second. Coverage-line tests, not behavior tests, in a suite whose stated justification is Sonar's
  // coverage denominator — which is precisely the provenance that makes it worth asserting the real
  // contract instead. [Story 23.5 code review 2026-08-08]
  it('renders a byte count in kilobytes to one decimal place, with a unit', () => {
    expect(kb(2048)).toBe('2.0 KB')
    expect(kb(1024)).toBe('1.0 KB')
    expect(kb(1536)).toBe('1.5 KB')
  })

  it('rounds to one decimal rather than truncating', () => {
    expect(kb(1587)).toBe('1.5 KB')
    expect(kb(1638)).toBe('1.6 KB')
  })

  it('renders zero as an explicit 0.0 KB rather than an empty or NaN string', () => {
    expect(kb(0)).toBe('0.0 KB')
  })

  it('scales past a megabyte without switching units', () => {
    // Deliberate: the harness reports artefact sizes in KB throughout so two numbers are always comparable
    // by eye. A future change to auto-scale units would break every published measurement's comparability.
    expect(kb(1024 * 1024)).toBe('1024.0 KB')
  })
})
