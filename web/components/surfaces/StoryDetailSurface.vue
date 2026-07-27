<script setup lang="ts">
/**
 * `epics/story-{id}.html` — one story. [Story 23.3 AC #1, AC #2, AC #4]
 *
 * See `DashboardSurface.vue` for why the family components are thin and what they are for.
 *
 * Path shape is `StoryEpicLinkifier.StoryPagePath`: the story id with dots replaced by dashes, so Story
 * 23.3 is `epics/story-23-3.html`. The catch-all matches on that shape, which is why the rule lives in one
 * place (`pages/[...path].vue`) rather than being re-derived per surface.
 *
 * This family's contract is the trail home: a story page must know its epic. That relationship is the one
 * the manifest carries structurally (`parent`), and it is what makes the epics tree a tree rather than a
 * flat pile of pages.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

const props = defineProps<{ page: IrPage }>()

if (!props.page.parent) {
  throw new Error(
    `Story page "${props.page.path}" has no parent in the IR manifest. Every story belongs to an epic; a ` +
      `null parent means the epics parser or the manifest's drill graph broke upstream.`,
  )
}
</script>

<template>
  <IrSurface :page="page" family="story-detail" />
</template>
