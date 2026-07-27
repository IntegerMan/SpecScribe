<script setup lang="ts">
/**
 * `epics.html` — the epics-and-stories index. [Story 23.3 AC #1, AC #2]
 *
 * See `DashboardSurface.vue` for why the family components are thin and what they are for.
 *
 * This family's contract is UX-DR17: the index is the site's densest status surface, and every status on it
 * must read as a WORD, never as colour alone. `StatusBadge` enforces that by shape for template-authored
 * markup (`label` is a required prop); injected markup has no such shape, so the guarantee is asserted here
 * and re-asserted over the emitted HTML by `npm run check:a11y`.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

const props = defineProps<{ page: IrPage }>()

// Every epic on the index is a link to its own page. A structural drop here would gut the link graph AC #4
// measures, and it would do it quietly — the page would still render, just with fewer ways out of it.
if (props.page.children.length === 0) {
  throw new Error(
    'The epics index IR entry declares no child pages. The epics tree is this story\'s primary navigable ' +
      'surface; an index with no children means the manifest\'s parent/child graph broke upstream.',
  )
}
</script>

<template>
  <IrSurface :page="page" family="epics-index" />
</template>
