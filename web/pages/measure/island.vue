<script setup lang="ts">
/**
 * VARIANT B — the <NuxtIsland> / server-component path. Same 200 rows, same primitives; the data is
 * resolved inside MeasureRows.server.vue and never crosses into the client bundle.
 */
useHead({ title: 'Measure — server component island' })
</script>

<template>
  <PageShell title="Measure: server component" subtitle="200 rows rendered by a .server.vue island.">
    <ChartPanel title="Rows" window="variant B">
      <MeasureRows :count="200" />
    </ChartPanel>
  </PageShell>
</template>
