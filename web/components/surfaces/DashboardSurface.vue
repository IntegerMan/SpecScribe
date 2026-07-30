<script setup lang="ts">
/**
 * `index.html` — the project dashboard. [Story 23.3 AC #1, AC #7]
 *
 * The family components are deliberately thin. Under owner decision D2 (hybrid) the proxy IR carries whole
 * rendered HTML per page and NO view models, so there is nothing here to rebuild as Vue that would not be
 * a duplicate of markup the IR already ships. What each family component adds is its own CONTRACT: the
 * invariant that family must not lose in migration, asserted at build time so a regression fails the build
 * instead of being found by looking. When 22.2's successors ship real view models, this is where a family's
 * Vue treatment lands — the seam exists now so 23.4 does not have to invent it.
 *
 * The dashboard's contract is ADR 0012 + ADR 0013: it carries the one Hierarchy Explorer, and that chart
 * has a server-rendered text twin that survives with JavaScript off.
 */
import type { IrPage } from '#ir'
import { dashboardContract, enforce } from '../../ir/contracts'
import IrSurface from './IrSurface.vue'

const props = defineProps<{ page: IrPage }>()

/**
 * The dashboard's contract: ADR 0012 (one Hierarchy Explorer) + ADR 0013 (the text twin IS the no-JS
 * contract). Both live in `ir/contracts.ts` as data, with an explicit severity each, because the severity is
 * the reviewable decision — and because a pure function is testable with the vitest already here, whereas
 * mounting this `.vue` would need a component harness `web/` deliberately does not depend on (ADR 0010).
 *
 * ⚠️ **A missing Hierarchy Explorer is a WARNING, not an error. [owner decision D6]**
 * It was a hard `throw` (Story 23.3), and that is a **project-independence defect**: whether a dashboard
 * carries a sunburst depends on whether the *target project* has roadmap data. It was the single route that
 * failed Story 23.5's two-IR experiment (CORA: **32/33**). Story 23.5 attributed the open item to Story 23.3
 * and left it; 23.4 touches this surface anyway, so it is fixed here and that open-items row is re-homed to
 * this story. A chart-less project needs no placeholder markup — its dashboard body is complete as the IR
 * describes it. The missing-text-twin check stays **fatal**, because unlike the other it is provable from the
 * IR for every project. See `test/contracts.test.ts`.
 */
enforce(dashboardContract(props.page))
</script>

<template>
  <IrSurface :page="page" family="dashboard" />
</template>
