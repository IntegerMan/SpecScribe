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
const rows = buildRows(props.count)
</script>

<template>
  <ul class="measure-list">
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
