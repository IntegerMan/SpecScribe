<script setup lang="ts">
/**
 * VARIANT C — the control. Same 200 rows, resolved at module scope with no data composable at all.
 *
 * Worth measuring alongside the other two because it is the shape 23.2's OWN primitives use (template-
 * authored showcase data), and it establishes the floor the other variants are compared against.
 */
import { buildRows } from '~/utils/measure-rows'

const rows = buildRows(200)

useHead({ title: 'Measure — static' })
</script>

<template>
  <PageShell title="Measure: static" subtitle="200 rows resolved at module scope — no data composable.">
    <!-- `window` is the NUMERIC analysis window, not a free-text label. [Story 23.2 review 2026-08-07] -->
    <ChartPanel title="Rows — variant C (module-scope control)">
      <ul class="measure-list">
        <ListRow v-for="row in rows" :key="row.id" :summary="row.summary" :chips="row.chips">
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
