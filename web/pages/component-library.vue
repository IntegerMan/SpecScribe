<script setup lang="ts">
/**
 * The app's own developer landing page. Moved off `/` by Story 23.3: the site root now resolves to the IR
 * manifest's entry page (the project dashboard), because this is a documentation portal and its root has to
 * be the portal — not a index of the toolkit that renders it.
 */
useHead({ title: 'SpecScribe — component library' })
</script>

<template>
  <PageShell
    title="Component library"
    subtitle="SpecScribe's Vue/Nuxt presentation layer (Epic 23). Not yet wired into `specscribe generate` — packaging is Story 23.5."
  >
    <ChartPanel title="Routes">
      <ul class="route-list">
        <ListRow summary="The portal itself, rendered from the IR: the dashboard and the whole epics tree (Story 23.3)." primary-href="/index.html" primary-label="Project dashboard" />
        <!-- The `/design-system` Vue showcase was retired 2026-08-07 (Story 23.2 review). The design-system
             page users actually get is `design-system.html`, rendered from the C#-composed region through
             `PortalMetaSurface` — it is reachable from the portal's own Help nav, not from here. -->
        <ListRow summary="The design system: token families and every primitive, in every state. Rendered from the IR like every other portal page." primary-href="/design-system.html" primary-label="Design system" />
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
