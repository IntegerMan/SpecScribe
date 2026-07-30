<script setup lang="ts">
/**
 * Retrospectives — `retros.html` and any `retros/{slug}.html` detail page. [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `RetroTemplater` (`BuildIndexPage` / `BuildPage`).
 *
 * **Injected vocabulary this component owns:** the retro index list and, on a detail page, the retro sections
 * plus the action-item list that links out to `follow-ups/`.
 *
 * **The index body starts at its doc-header** (Story 23.4 finding 1), and detail pages carry an
 * `EntityPager` — so their wayfinding band renders in the wrapped `.page-wayfinding` shape while the index's
 * renders bare. Both shapes reach the region identically now that Story 22.4 unified the marker, which is why
 * `web/ir/adapter.ts` no longer needs its `wayfindingRepaired` repair pass.
 *
 * ⚠️ **One retro can be the LATEST for several epics** (a joint retrospective), so the epic→retro map is
 * many-to-one. Anything that renders "the retro for epic N" must read that map rather than assuming a
 * one-to-one relationship.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="retro" />
</template>
