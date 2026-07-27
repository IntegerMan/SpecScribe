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
import IrSurface from './IrSurface.vue'

const props = defineProps<{ page: IrPage }>()

if (!props.page.needsHierarchyEngine) {
  throw new Error(
    'The dashboard IR region carries no [data-hierarchy] mount point. Either the Hierarchy Explorer was ' +
      'removed from index.html (ADR 0012 §Decision 2 makes it the only route to a sunburst or treemap), or ' +
      'the capture stopped including it. Both are regressions, not migration noise.',
  )
}

// ADR 0013: the text twin IS the no-JS contract. If it stopped being server-rendered, the JS-off page would
// lose the chart's content entirely and no amount of chart-booting here would put it back.
if (!props.page.region.mainInnerHtml.includes('ss-hierarchy-twin')) {
  throw new Error(
    'The dashboard IR region carries a Hierarchy Explorer but no `ss-hierarchy-twin` text equivalent. ' +
      'ADR 0013 makes the twin the no-JS contract — a chart without one is not shippable.',
  )
}
</script>

<template>
  <IrSurface :page="page" family="dashboard" />
</template>
