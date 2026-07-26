<script setup lang="ts">
/**
 * VARIANT A — the async-data path. The 23.1 spike's shape, and the one it measured at 2.26x site weight
 * (overhead entirely hydration payload).
 *
 * Anything reaching a component through `useAsyncData` is serialized into the route's `_payload.json` BY
 * CONSTRUCTION, so the rows are shipped twice: once as rendered HTML, once as data for a hydration that a
 * fully-static page never needed.
 */
import { buildRows } from '~/utils/measure-rows'

const { data: rows } = await useAsyncData('measure-rows', async () => buildRows(200))

useHead({ title: 'Measure — async data' })
</script>

<template>
  <PageShell title="Measure: async data" subtitle="200 rows reaching the primitive through useAsyncData.">
    <ChartPanel title="Rows" window="variant A">
      <ul class="measure-list">
        <ListRow v-for="row in rows ?? []" :key="row.id" :summary="row.summary" :chips="row.chips">
          <template #badge>
            <StatusBadge :stage="row.stage" :label="row.label" />
          </template>
        </ListRow>
      </ul>
    </ChartPanel>
  </PageShell>
</template>

<style scoped>
.measure-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
</style>
