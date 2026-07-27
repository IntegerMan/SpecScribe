<script setup lang="ts">
/**
 * `epics/epic-{N}.html` — one epic. [Story 23.3 AC #1, AC #2, AC #4]
 *
 * See `DashboardSurface.vue` for why the family components are thin and what they are for.
 *
 * This family's contract is the wayfinding band: an epic page is a NESTED page, so it carries a breadcrumb
 * and a prev/next sibling pager, and both are load-bearing for navigability. The band is also where the
 * IR's slice is unbalanced (`ExtractContentRegion` starts at the breadcrumb, inside a wrapper it does not
 * carry the opener for) — the adapter repairs that and flags it, and this is where the flag is checked.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

const props = defineProps<{ page: IrPage }>()

if (!props.page.region.wayfindingHtml) {
  throw new Error(
    `Epic page "${props.page.path}" carries no breadcrumb/pager band. Nested pages have had one since ` +
      `Story 10.4; losing it costs the page both its trail home and its sibling navigation.`,
  )
}
</script>

<template>
  <IrSurface :page="page" family="epic-detail" />
</template>
