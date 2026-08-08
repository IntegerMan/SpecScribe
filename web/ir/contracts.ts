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
 *
 * ## ⚠️ Three corrections from the Story 23.4 code review (2026-08-08) — read before editing
 *
 * 1. **The twin check reads the WHOLE region, not just `<main>`.** Story 23.4 introduced
 *    `IrRegion.trailingHtml` for content the emitter puts after `</main>` — and then kept probing
 *    `mainInnerHtml` alone. A chart mount landing in trailing content therefore reported "no chart", which
 *    both skipped the boot script and made the fatal twin check below unreachable. The domain is
 *    `regionHtml()` and it must stay that way.
 * 2. **The twin check asserts CONTENT, not a substring.** `includes('ss-hierarchy-twin')` was satisfied by an
 *    empty `<div class="ss-hierarchy-twin"></div>`, by a twin whose `<ul>` had lost every `<li>`, and by the
 *    bare string sitting in an HTML comment. An 18-character substring is not the no-JS contract.
 * 3. **The twin contract is NOT dashboard-specific.** It was enforced on `index.html` alone while the eight
 *    `insight` singletons carried chart mounts with no gate at all — against a project-wide rule (CLAUDE.md:
 *    "Every chart needs an accessible text equivalent"). `hierarchyTwinContract` is the shared rule; the
 *    dashboard adds only its own `no-explorer` warning on top.
 */

/** The minimum shape these contracts read. Deliberately narrower than `IrPage` so the tests need no fixture
 * of the full page type — a contract that only looks at these fields should only require these fields. */
export interface ContractPage {
  path: string
  needsHierarchyEngine: boolean
  region: { mainInnerHtml: string; trailingHtml?: string }
}

export interface ContractViolation {
  severity: 'warn' | 'error'
  /** Stable identifier, so a test asserts on this rather than on prose that will be reworded. */
  code: 'dashboard-no-explorer' | 'chart-without-text-twin'
  message: string
}

/** The class the server-rendered text equivalent carries. */
const TWIN_CLASS = 'ss-hierarchy-twin'

/**
 * Everything the emitter put in the region — `<main>`'s inner HTML AND anything after `</main>`.
 *
 * See correction 1 above. `trailingHtml` is optional on the interface only so a test fixture may omit it;
 * the real `IrRegion` always carries a string.
 */
function regionHtml(page: ContractPage): string {
  return `${page.region.mainInnerHtml}\n${page.region.trailingHtml ?? ''}`
}

/**
 * The twin element's inner HTML, or `null` when there is no twin element at all.
 *
 * Comments are stripped first so the bare class name inside `<!-- … -->` cannot satisfy the contract, and the
 * class is matched inside a real `class="…"` attribute so a `data-*` value carrying the same string cannot
 * either. Both were live holes before the 23.4 review.
 */
function twinInnerHtml(html: string): string | null {
  const withoutComments = html.replace(/<!--[\s\S]*?-->/g, '')
  const open = new RegExp(`<([a-z]+)\\b[^>]*\\bclass="[^"]*\\b${TWIN_CLASS}\\b[^"]*"[^>]*>`, 'i')
  const m = open.exec(withoutComments)
  if (!m) return null
  const start = m.index + m[0].length
  const close = withoutComments.indexOf(`</${m[1]}>`, start)
  if (close < 0) return null
  return withoutComments.slice(start, close)
}

/**
 * A twin that actually carries text. Tags are stripped and the remainder must be non-blank — an empty twin,
 * or one whose list items all vanished, is the same loss with JavaScript off as no twin at all, which is the
 * thing ADR 0013 makes the contract about.
 */
function hasSubstantiveTwin(html: string): boolean {
  const inner = twinInnerHtml(html)
  if (inner === null) return false
  return inner.replace(/<[^>]*>/g, '').trim().length > 0
}

/**
 * The rule for EVERY family that can carry a Hierarchy Explorer: a chart whose server-rendered text
 * equivalent is missing or empty loses its content entirely with JavaScript off, and no amount of
 * chart-booting puts it back. [ADR 0013]
 *
 * This IS provable from the IR and holds for every project — the antecedent is "this page has a chart mount",
 * which the IR states — so it is an `error`. Note it is gated on a chart existing, which is correct: a page
 * with no chart owes no twin. What made that gating dangerous before was the narrow domain (correction 1),
 * not the gating itself.
 */
export function hierarchyTwinContract(page: ContractPage): ContractViolation[] {
  if (!page.needsHierarchyEngine) return []
  if (hasSubstantiveTwin(regionHtml(page))) return []
  return [
    {
      severity: 'error',
      code: 'chart-without-text-twin',
      message:
        `Page "${page.path}" carries a Hierarchy Explorer but no \`${TWIN_CLASS}\` text equivalent with ` +
        `content. ADR 0013 makes the twin the no-JS contract — a chart without one is not shippable. ` +
        `An EMPTY twin element counts as missing: with scripts off it renders nothing.`,
    },
  ]
}

/**
 * The dashboard's contract: ADR 0012 (one Hierarchy Explorer implementation) + ADR 0013 (the text twin IS the
 * no-JS contract).
 *
 * Returns every violation rather than throwing, so the caller decides what a `warn` does — and so a test can
 * assert that a chart-less dashboard produces **no error**, which is the actual D6 requirement.
 */
export function dashboardContract(page: ContractPage): ContractViolation[] {
  if (!page.needsHierarchyEngine) {
    // WARN, not error. See the severity rule above: a project with no roadmap data legitimately draws no
    // sunburst, and the IR cannot distinguish that from a regression in a project that should have one.
    // The twin rule below is vacuous without a chart, so there is nothing further to say about this page.
    return [
      {
        severity: 'warn',
        code: 'dashboard-no-explorer',
        message:
          `Dashboard "${page.path}" carries no [data-hierarchy] mount point, so no Hierarchy Explorer will ` +
          `render. That is EXPECTED for a project with no roadmap data to draw. If this project does have ` +
          `epics, it is a regression — either the explorer was removed from the dashboard (ADR 0012 ` +
          `§Decision 2 makes it the only route to a sunburst or treemap) or the capture stopped including it.`,
      },
    ]
  }
  return hierarchyTwinContract(page)
}

/**
 * The chart/analytics singletons. They carry the same explorer mounts the dashboard does, so they owe the
 * same twin — and until the 23.4 review they were enforced by nothing at all.
 *
 * There is deliberately no `no-explorer` warning here: unlike the dashboard, most of these pages draw inline
 * SVG from `Charts.Framed` and are complete without an explorer, so "no mount point" is not even weak
 * evidence of a regression.
 */
export function insightContract(page: ContractPage): ContractViolation[] {
  return hierarchyTwinContract(page)
}

/**
 * Applies a contract the way a surface component wants it: errors throw (a build failure), warnings go to the
 * build log. Kept next to the contracts so every family enforces them identically.
 *
 * ⚠️ Warnings are emitted BEFORE the throw, and every error is reported in one message. The previous shape
 * threw on `find(...)` first, so a fatal violation silently discarded the warnings that would have explained
 * it and hid the 2nd..nth error behind the first.
 */
export function enforce(violations: ContractViolation[]): void {
  for (const v of violations) {
    if (v.severity === 'warn') console.warn(`[specscribe] ${v.message}`)
  }
  const fatal = violations.filter((v) => v.severity === 'error')
  if (fatal.length > 0) {
    throw new Error(fatal.map((v) => v.message).join('\n'))
  }
}
