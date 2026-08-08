<script setup lang="ts">
/**
 * A server component (`.server.vue`) — rendered as a <NuxtIsland> on the server and never hydrated.
 *
 * This is the shape AC #4 measures against the async-data path: the data is resolved here, server-side, so
 * it has no reason to appear in the page's hydration payload. Whether that actually holds is the experiment;
 * see scripts/measure-payload.mjs for the measured answer.
 */
import { buildRows } from '~/utils/measure-rows'

const props = withDefaults(defineProps<{ count?: number }>(), { count: 200 })

/**
 * `withDefaults` substitutes for `undefined` only, so `:count="null"` reached `Array.from({ length: null })`
 * and produced zero rows — an empty `<ul class="measure-list">`, i.e. an empty list landmark rather than no
 * list. A negative or non-integer value did the same. Clamped to a non-negative integer here, and the list
 * itself is omitted when there is nothing in it. [Story 23.2 review 2026-08-07]
 */
const rowCount = computed(() => {
  const n = Number(props.count)
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : 0
})
const rows = computed(() => buildRows(rowCount.value))
</script>

<template>
  <ul v-if="rows.length" class="measure-list">
    <ListRow v-for="row in rows" :key="row.id" :summary="row.summary" :chips="row.chips">
      <template #badge>
        <StatusBadge :stage="row.stage" :label="row.label" />
      </template>
    </ListRow>
  </ul>
</template>

<style scoped>
.measure-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
</style>
