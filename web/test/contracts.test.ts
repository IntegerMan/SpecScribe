import { describe, expect, it, vi } from 'vitest'
import { dashboardContract, enforce, type ContractPage } from '../ir/contracts'

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
    expect(violations.map((v) => v.code)).not.toContain('dashboard-chart-without-text-twin')
  })

  it('ERRORS when a chart ships without its text twin — ADR 0013 is not project-dependent', () => {
    const violations = dashboardContract(withChart(false))
    expect(violations).toHaveLength(1)
    expect(violations[0]!.code).toBe('dashboard-chart-without-text-twin')
    expect(violations[0]!.severity).toBe('error')
  })

  it('is clean when a chart ships with its twin', () => {
    expect(dashboardContract(withChart(true))).toEqual([])
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
})
