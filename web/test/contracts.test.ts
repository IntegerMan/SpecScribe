import { describe, expect, it, vi } from 'vitest'
import { dashboardContract, enforce, insightContract, type ContractPage } from '../ir/contracts'

/**
 * Owner decision D6's regression test. [Story 23.4]
 *
 * The defect: Story 23.3 made "the dashboard carries no Hierarchy Explorer" a build-failing `throw`. That
 * fails the build of any project whose dashboard legitimately has no chart, and it is the ONE route that broke
 * in Story 23.5's two-IR experiment (CORA rendered 32/33). Story 23.5 attributed the open item to Story 23.3
 * and left it; D6 moves it here and fixes it.
 *
 * Reproducing it with the real two-IR harness would need a second project's IR on disk, which is not a
 * dependency a unit test should carry. A synthetic chart-less page reproduces the exact failing condition and
 * is reproducible on any machine — so that is what this asserts.
 */
const withChart = (twin: boolean): ContractPage => ({
  path: 'index.html',
  needsHierarchyEngine: true,
  region: {
    mainInnerHtml: twin
      ? '<div data-hierarchy="sunburst"></div><div class="ss-hierarchy-twin"><ul><li>Epic 1</li></ul></div>'
      : '<div data-hierarchy="sunburst"></div>',
  },
})

const withoutChart = (): ContractPage => ({
  path: 'index.html',
  needsHierarchyEngine: false,
  region: { mainInnerHtml: '<section class="dashboard-panels"><p>No roadmap data.</p></section>' },
})

describe('dashboardContract', () => {
  it('does NOT error when the project simply has no chart — the D6 fix', () => {
    const violations = dashboardContract(withoutChart())
    expect(violations.filter((v) => v.severity === 'error')).toEqual([])
  })

  it('still WARNS about a chart-less dashboard, so a real regression is not silent', () => {
    // The diagnostic the old throw provided is worth keeping: in a project that does have epics, a missing
    // explorer IS a regression. A warning keeps that visible without failing anyone else's build.
    const violations = dashboardContract(withoutChart())
    expect(violations.map((v) => v.code)).toEqual(['dashboard-no-explorer'])
    expect(violations[0]!.severity).toBe('warn')
  })

  it('does not run the text-twin check when there is no chart', () => {
    // Otherwise the project-independence defect just moves from one check to the next: a chart-less project
    // has no twin either, and would fail on THAT instead.
    const violations = dashboardContract(withoutChart())
    expect(violations.map((v) => v.code)).not.toContain('chart-without-text-twin')
  })

  it('ERRORS when a chart ships without its text twin — ADR 0013 is not project-dependent', () => {
    const violations = dashboardContract(withChart(false))
    expect(violations).toHaveLength(1)
    expect(violations[0]!.code).toBe('chart-without-text-twin')
    expect(violations[0]!.severity).toBe('error')
  })

  it('is clean when a chart ships with its twin', () => {
    expect(dashboardContract(withChart(true))).toEqual([])
  })
})

/**
 * ⚠️ The three holes the Story 23.4 code review found in this contract (findings F-3, F-7, D-2).
 *
 * Each of these passed before, and each is a way the one deliberately-FATAL check in the file could be
 * satisfied by something that is not a text equivalent — or skipped entirely on a page that has a chart.
 */
describe('the twin contract cannot be satisfied by something that is not a twin [D-2]', () => {
  const chartWith = (twinMarkup: string): ContractPage => ({
    path: 'index.html',
    needsHierarchyEngine: true,
    region: { mainInnerHtml: `<div data-hierarchy="sunburst"></div>${twinMarkup}` },
  })

  it('rejects an EMPTY twin element — with scripts off it renders nothing, same as no twin', () => {
    const v = dashboardContract(chartWith('<div class="ss-hierarchy-twin"></div>'))
    expect(v.map((x) => x.code)).toEqual(['chart-without-text-twin'])
  })

  it('rejects a twin whose list items have all vanished', () => {
    const v = dashboardContract(chartWith('<div class="ss-hierarchy-twin"><ul></ul></div>'))
    expect(v.map((x) => x.code)).toEqual(['chart-without-text-twin'])
  })

  it('rejects the class name appearing only inside an HTML comment', () => {
    const v = dashboardContract(chartWith('<!-- ss-hierarchy-twin was here -->'))
    expect(v.map((x) => x.code)).toEqual(['chart-without-text-twin'])
  })

  it('rejects the class name appearing only in a data-* attribute value', () => {
    const v = dashboardContract(chartWith('<div data-twin-target="ss-hierarchy-twin"></div>'))
    expect(v.map((x) => x.code)).toEqual(['chart-without-text-twin'])
  })

  it('accepts a twin that actually carries text', () => {
    const v = dashboardContract(chartWith('<div class="ss-hierarchy-twin"><ul><li>Epic 1</li></ul></div>'))
    expect(v).toEqual([])
  })
})

describe('the contract reads the WHOLE region, not just <main> [F-7]', () => {
  it('finds a twin that the emitter placed AFTER </main>', () => {
    // Story 23.4 introduced `trailingHtml` precisely because `deep-analytics.html` puts content after the
    // landmark. Probing `mainInnerHtml` alone made this page look like a chart with no twin.
    const page: ContractPage = {
      path: 'deep-analytics.html',
      needsHierarchyEngine: true,
      region: {
        mainInnerHtml: '<div data-hierarchy="treemap"></div>',
        trailingHtml: '<div class="ss-hierarchy-twin"><ul><li>Module A</li></ul></div>',
      },
    }
    expect(dashboardContract(page)).toEqual([])
  })
})

describe('insightContract [F-3]', () => {
  const insight = (twin: boolean): ContractPage => ({
    path: 'impact-map.html',
    needsHierarchyEngine: true,
    region: {
      mainInnerHtml: twin
        ? '<div data-hierarchy="treemap"></div><div class="ss-hierarchy-twin"><ul><li>A</li></ul></div>'
        : '<div data-hierarchy="treemap"></div>',
    },
  })

  it('holds the 8 chart singletons to ADR 0013 — until this review they were gated by nothing', () => {
    expect(insightContract(insight(false)).map((v) => v.code)).toEqual(['chart-without-text-twin'])
    expect(insightContract(insight(false))[0]!.severity).toBe('error')
  })

  it('is clean when the twin is present', () => {
    expect(insightContract(insight(true))).toEqual([])
  })

  it('says NOTHING about an insight page with no chart — unlike the dashboard, that is unremarkable', () => {
    // Most of these pages draw inline SVG from `Charts.Framed` and are complete with no explorer at all, so a
    // missing mount point is not even weak evidence of a regression. No warn, no error.
    const noChart: ContractPage = {
      path: 'cadence.html',
      needsHierarchyEngine: false,
      region: { mainInnerHtml: '<section class="chart-panel"><svg></svg></section>' },
    }
    expect(insightContract(noChart)).toEqual([])
  })
})

describe('enforce', () => {
  it('throws on an error violation', () => {
    expect(() => enforce(dashboardContract(withChart(false)))).toThrow(/no-JS contract/)
  })

  it('warns but does not throw on a warn violation', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    try {
      expect(() => enforce(dashboardContract(withoutChart()))).not.toThrow()
      expect(warn).toHaveBeenCalledOnce()
    } finally {
      warn.mockRestore()
    }
  })

  it('does nothing at all when the contract is clean', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    try {
      enforce(dashboardContract(withChart(true)))
      expect(warn).not.toHaveBeenCalled()
    } finally {
      warn.mockRestore()
    }
  })

  it('LOGS the warnings before it throws, and reports every error [F-16]', () => {
    // The old shape threw on `find(...)` first, so the `console.warn` loop was unreachable whenever any error
    // was present: the build failed with one message and the diagnostics that would explain it were discarded.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    try {
      expect(() =>
        enforce([
          { severity: 'warn', code: 'dashboard-no-explorer', message: 'context that explains the failure' },
          { severity: 'error', code: 'chart-without-text-twin', message: 'first fatal' },
          { severity: 'error', code: 'chart-without-text-twin', message: 'second fatal' },
        ]),
      ).toThrow(/first fatal[\s\S]*second fatal/)
      expect(warn).toHaveBeenCalledOnce()
    } finally {
      warn.mockRestore()
    }
  })
})
