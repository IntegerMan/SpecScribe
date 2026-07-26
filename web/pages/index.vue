<script setup lang="ts">
useHead({ title: 'SpecScribe — component library' })
</script>

<template>
  <PageShell
    title="Component library"
    subtitle="SpecScribe's Vue/Nuxt presentation layer (Epic 23). Not yet wired into `specscribe generate` — packaging is Story 23.5."
  >
    <ChartPanel title="Routes">
      <ul class="route-list">
        <ListRow summary="The design system: token families and every primitive, in every state." primary-href="/design-system" primary-label="Design system" />
        <ListRow summary="Payload measurement: the same primitive rendered three ways (Story 23.2 AC #4)." :chips="['/measure/async', '/measure/island', '/measure/static']" />
      </ul>
    </ChartPanel>
  </PageShell>
</template>

<style scoped>
.route-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
</style>
