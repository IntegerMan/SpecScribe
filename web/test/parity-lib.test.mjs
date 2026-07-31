// Unit tests for the pinned content-drift gate's decision logic. [Story 23.6 AC #3]
//
// This gate replaces `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`, so its own
// correctness matters more than usual: a classifier that errs in the PERMISSIVE direction produces a green
// tick over drifted output, which ADR 0033 § Context argues is worse than having no gate at all. The cases
// below are therefore weighted toward the vacuous-pass failure modes rather than the obvious ones.

import { describe, expect, it } from 'vitest'
import {
  assessRun,
  classifyRoute,
  composeIrMain,
  foldBuildAssets,
  parityDigest,
  ParityOracleError,
  validateOracle,
} from '../scripts/parity-lib.mjs'

const route = (over = {}) => ({
  path: 'index.html',
  family: 'dashboard',
  mainSha: 'aaaaaaaaaaaaaaaa',
  pageSha: 'bbbbbbbbbbbbbbbb',
  ...over,
})

describe('parityDigest', () => {
  it('is a 16-char hex slice of sha256', () => {
    const d = parityDigest('hello')
    expect(d).toHaveLength(16)
    expect(d).toMatch(/^[0-9a-f]{16}$/)
    expect(d).toBe('2cf24dba5fb0a30e')
  })

  it('separates content a byte LENGTH could not — the failure the oracle exists to survive', () => {
    // A length-preserving rewrite is exactly what a markup or escaping change looks like.
    expect('<b>x</b>'.length).toBe('<i>x</i>'.length)
    expect(parityDigest('<b>x</b>')).not.toBe(parityDigest('<i>x</i>'))
  })
})

describe('composeIrMain', () => {
  it('rebuilds the landmark the way the oracle records it, and normalizes', () => {
    const out = composeIrMain({ mainAttributes: ' class="doc"', mainInnerHtml: '<p>hi</p>' }, (s) =>
      s.replace(/\r\n/g, '\n'),
    )
    expect(out).toBe('<main id="main-content" class="doc"><p>hi</p></main>')
  })
})

describe('foldBuildAssets — the build-token fold that keeps pageSha portable', () => {
  it('folds Nuxt content-hashed chunk names, which are a property of the BUILD not the page', () => {
    const html = '<link rel="stylesheet" href="./_nuxt/PageShell.Ys9LGDmo.css"><script src="./_nuxt/B1mWaxmE.js">'
    expect(foldBuildAssets(html)).toBe(
      '<link rel="stylesheet" href="./_nuxt/PageShell.<HASH>.css"><script src="./_nuxt/B1mWaxmE.js">',
    )
  })

  it('makes two builds of the SAME page agree — the whole reason it exists', () => {
    // Without this the gate reports CHROME DRIFT on every route whenever the artefact is rebuilt elsewhere,
    // which is a failure unrelated to the change under test and exactly what ADR 0033 §Decision 2 forbids.
    const a = '<link href="./_nuxt/entry.DRLacYXT.css"><link href="./_nuxt/ChartPanel.6vcVUsK8.css">'
    const b = '<link href="./_nuxt/entry.aB3dEfG7.css"><link href="./_nuxt/ChartPanel.zZ9yX8w7.css">'
    expect(foldBuildAssets(a)).toBe(foldBuildAssets(b))
  })

  it('still catches a chunk being RENAMED, ADDED or DROPPED — the fold is narrow on purpose', () => {
    const base = '<link href="./_nuxt/PageShell.Ys9LGDmo.css">'
    // stem changed
    expect(foldBuildAssets(base)).not.toBe(foldBuildAssets('<link href="./_nuxt/PageShim.Ys9LGDmo.css">'))
    // extension changed
    expect(foldBuildAssets(base)).not.toBe(foldBuildAssets('<link href="./_nuxt/PageShell.Ys9LGDmo.js">'))
    // an extra chunk appearing
    expect(foldBuildAssets(base)).not.toBe(foldBuildAssets(`${base}<link href="./_nuxt/Extra.Ys9LGDmo.css">`))
    // a chunk disappearing
    expect(foldBuildAssets(base)).not.toBe(foldBuildAssets(''))
  })

  it('leaves non-_nuxt assets alone — C# owns those four and their names are stable', () => {
    // specscribe.css / specscribe.js / prism.js / plotly-hierarchy.min.js are emitted by C# with fixed names.
    // Folding them would blind the gate to a real asset change.
    const html = '<link href="./specscribe.css"><script src="./plotly-hierarchy.min.js">'
    expect(foldBuildAssets(html)).toBe(html)
  })

  it('does not swallow a multi-segment stem', () => {
    expect(foldBuildAssets('./_nuxt/some.long.name.Ys9LGDmo.css')).toBe('./_nuxt/some.long.name.<HASH>.css')
  })
})

describe('validateOracle — loudness gate A (ADR 0033 §Decision 5)', () => {
  it('accepts a well-formed oracle and reports the families it claims to cover', () => {
    const { routes, families } = validateOracle({
      routes: [route(), route({ path: 'a.html', family: 'doc-prose' })],
    })
    expect(routes).toHaveLength(2)
    expect(families).toEqual(['dashboard', 'doc-prose'])
  })

  it('THROWS on an empty route set rather than reporting "no drift"', () => {
    expect(() => validateOracle({ routes: [] })).toThrow(ParityOracleError)
  })

  it('THROWS when there is no routes array at all', () => {
    expect(() => validateOracle({})).toThrow(ParityOracleError)
    expect(() => validateOracle(null)).toThrow(ParityOracleError)
  })

  it('THROWS on a route missing EITHER digest rather than skipping it silently', () => {
    // A half-populated oracle is the shape a partially-failed regeneration leaves behind.
    expect(() => validateOracle({ routes: [route({ mainSha: undefined })] })).toThrow(/mainSha/)
    expect(() => validateOracle({ routes: [route({ pageSha: undefined })] })).toThrow(/pageSha/)
  })

  it('names the source in its message so a failure is actionable', () => {
    expect(() => validateOracle({ routes: [] }, 'measurements/parity-pinned.json')).toThrow(
      /measurements\/parity-pinned\.json/,
    )
  })
})

describe('classifyRoute', () => {
  it('passes when both digests match', () => {
    expect(classifyRoute(route(), { mainSha: 'aaaaaaaaaaaaaaaa', pageSha: 'bbbbbbbbbbbbbbbb' }).kind).toBe('ok')
  })

  it('reports main-drift when the REGION moved — the C# lineage the story must preserve', () => {
    const v = classifyRoute(route(), { mainSha: 'ffffffffffffffff', pageSha: 'bbbbbbbbbbbbbbbb' })
    expect(v.kind).toBe('main-drift')
    expect(v.expected).toBe('aaaaaaaaaaaaaaaa')
    expect(v.actual).toBe('ffffffffffffffff')
  })

  it('reports chrome-drift when only the WHOLE PAGE moved — the surface the old oracle could not see', () => {
    // <title>, meta, favicon, footer, <script src>, the nav toggle, the Mermaid init and the Hierarchy/Graph
    // anti-flash handshakes all live outside <main>. Deleting HtmlRenderAdapter.Render deletes their only C#
    // emitter, and nothing hashed them before this gate.
    const v = classifyRoute(route(), { mainSha: 'aaaaaaaaaaaaaaaa', pageSha: 'cccccccccccccccc' })
    expect(v.kind).toBe('chrome-drift')
  })

  it('reports a region change as main-drift ALONE, not also as chrome-drift', () => {
    // A region change necessarily moves the whole-page digest too. Naming both would report one defect twice
    // and bury which layer produced it.
    const v = classifyRoute(route(), { mainSha: 'ffffffffffffffff', pageSha: 'ffffffffffffffff' })
    expect(v.kind).toBe('main-drift')
  })

  it('carries path and family on every verdict so a failure NAMES the page (§Decision 1)', () => {
    for (const live of [
      { mainSha: 'x', pageSha: 'y' },
      { unmeasurable: 'HTTP 500' },
    ]) {
      const v = classifyRoute(route({ path: 'epics/epic-3.html', family: 'epic-detail' }), live)
      expect(v.path).toBe('epics/epic-3.html')
      expect(v.family).toBe('epic-detail')
    }
  })
})

describe('assessRun — loudness gates B and C', () => {
  const ok = (path, family) => ({ path, family, kind: 'ok' })

  it('passes a clean run that covers every claimed family', () => {
    const a = assessRun([ok('a.html', 'dashboard'), ok('b.html', 'doc-prose')], ['dashboard', 'doc-prose'])
    expect(a.ok).toBe(true)
    expect(a.measured).toBe(2)
  })

  it('gate B — FAILS when a pinned route could not be rendered at all', () => {
    const a = assessRun(
      [ok('a.html', 'dashboard'), { path: 'b.html', family: 'doc-prose', kind: 'unmeasurable', why: 'HTTP 500' }],
      ['dashboard', 'doc-prose'],
    )
    expect(a.ok).toBe(false)
    expect(a.unmeasurable).toHaveLength(1)
    expect(a.measured).toBe(1)
  })

  it('gate C — FAILS when a whole family silently vanished from the measured set', () => {
    // The remaining routes would still report "0 drift". That is the partial-run failure mode wearing a
    // green tick — the exact shape RegionCompositionCorpusProof guards against for the deep-git surfaces.
    const a = assessRun([ok('a.html', 'dashboard')], ['dashboard', 'code-file'])
    expect(a.ok).toBe(false)
    expect(a.missingFamilies).toEqual(['code-file'])
  })

  it('gate C — an unmeasurable route does NOT count as covering its family', () => {
    const a = assessRun(
      [ok('a.html', 'dashboard'), { path: 'c.html', family: 'code-file', kind: 'unmeasurable', why: 'gone' }],
      ['dashboard', 'code-file'],
    )
    expect(a.missingFamilies).toEqual(['code-file'])
  })

  it('FAILS an empty run rather than passing it', () => {
    expect(assessRun([], []).ok).toBe(false)
  })

  it('surfaces main-drift and chrome-drift separately in the report', () => {
    const a = assessRun(
      [
        { path: 'a.html', family: 'dashboard', kind: 'main-drift' },
        { path: 'b.html', family: 'doc-prose', kind: 'chrome-drift' },
      ],
      ['dashboard', 'doc-prose'],
    )
    expect(a.ok).toBe(false)
    expect(a.mainDrift).toHaveLength(1)
    expect(a.chromeDrift).toHaveLength(1)
  })
})
