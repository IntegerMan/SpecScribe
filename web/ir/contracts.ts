/**
 * Per-family render contracts — the invariants a surface must not lose in migration. [Story 23.4]
 *
 * ## Why these are functions here and not `if`s inside the components
 *
 * Two reasons, and the second is the load-bearing one.
 *
 * 1. **Testability without a component harness.** `web/` runs on `nuxt` + `vue` + `vue-router` and nothing
 *    else (CONVENTIONS.md; ADR 0010's zero-dep posture), so there is no `@vue/test-utils` to mount a `.vue`
 *    file with — and adding one is explicitly out of scope. A contract expressed as a pure function over the
 *    IR page is testable with the vitest that is already here.
 * 2. **The distinction these encode is easy to get wrong, and getting it wrong breaks other people's
 *    projects.** Story 23.3 made "the dashboard has no Hierarchy Explorer" a build-failing `throw`. That
 *    reads like rigour and is actually a project-independence defect: whether a dashboard carries a sunburst
 *    depends on whether the TARGET project has roadmap data. It was the single route that failed Story 23.5's
 *    two-IR experiment (CORA: 32/33). Separating `severity: 'warn'` from `severity: 'error'` as data — rather
 *    than as the presence or absence of a `throw` statement — is what makes that judgement reviewable.
 *
 * **The rule for choosing a severity.** `error` is for a contract the IR itself can prove is violated —
 * something true of every project. `warn` is for a condition that is *indistinguishable from legitimate
 * absence* given only the IR. If a check cannot tell "this project has no chart" from "this project's chart
 * went missing", it must not fail the build.
 */

/** The minimum shape these contracts read. Deliberately narrower than `IrPage` so the tests need no fixture
 * of the full page type — a contract that only looks at three fields should only require three fields. */
export interface ContractPage {
  path: string
  needsHierarchyEngine: boolean
  region: { mainInnerHtml: string }
}

export interface ContractViolation {
  severity: 'warn' | 'error'
  /** Stable identifier, so a test asserts on this rather than on prose that will be reworded. */
  code: 'dashboard-no-explorer' | 'dashboard-chart-without-text-twin'
  message: string
}

/**
 * The dashboard's contract: ADR 0012 (one Hierarchy Explorer implementation) + ADR 0013 (the text twin IS the
 * no-JS contract).
 *
 * Returns every violation rather than throwing, so the caller decides what a `warn` does — and so a test can
 * assert that a chart-less dashboard produces **no error**, which is the actual D6 requirement.
 */
export function dashboardContract(page: ContractPage): ContractViolation[] {
  const violations: ContractViolation[] = []

  if (!page.needsHierarchyEngine) {
    // WARN, not error. See the severity rule above: a project with no roadmap data legitimately draws no
    // sunburst, and the IR cannot distinguish that from a regression in a project that should have one.
    violations.push({
      severity: 'warn',
      code: 'dashboard-no-explorer',
      message:
        `Dashboard "${page.path}" carries no [data-hierarchy] mount point, so no Hierarchy Explorer will ` +
        `render. That is EXPECTED for a project with no roadmap data to draw. If this project does have ` +
        `epics, it is a regression — either the explorer was removed from the dashboard (ADR 0012 ` +
        `§Decision 2 makes it the only route to a sunburst or treemap) or the capture stopped including it.`,
    })
    // Deliberately returns here: the twin check below is meaningless without a chart, and running it anyway
    // is precisely how the project-independence defect would move from one check to the next.
    return violations
  }

  if (!page.region.mainInnerHtml.includes('ss-hierarchy-twin')) {
    // ERROR: this one IS provable from the IR and holds for every project. A chart whose server-rendered
    // text equivalent is missing loses its content entirely with JavaScript off, and no amount of
    // chart-booting puts it back.
    violations.push({
      severity: 'error',
      code: 'dashboard-chart-without-text-twin',
      message:
        `Dashboard "${page.path}" carries a Hierarchy Explorer but no \`ss-hierarchy-twin\` text ` +
        `equivalent. ADR 0013 makes the twin the no-JS contract — a chart without one is not shippable.`,
    })
  }

  return violations
}

/**
 * Applies a contract the way a surface component wants it: errors throw (a build failure), warnings go to the
 * build log. Kept next to the contracts so every family enforces them identically.
 */
export function enforce(violations: ContractViolation[]): void {
  const fatal = violations.find((v) => v.severity === 'error')
  if (fatal) throw new Error(fatal.message)
  for (const v of violations) console.warn(`[specscribe] ${v.message}`)
}
