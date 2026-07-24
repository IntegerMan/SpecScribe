<script setup lang="ts">
// Catch-all so the route table can come straight from the IR manifest (paths are arbitrarily deep, e.g.
// `planning-artifacts/ux-designs/…`), rather than being constrained to one path segment.
const route = useRoute()

// Fetched server-side at prerender time, so the emitted static HTML carries the full content (NFR6).
const { data } = await useAsyncData(`ir:${route.path}`, () => $fetch('/api/ir', { query: { path: route.path } }))

useHead({ title: () => data.value?.page.title ?? 'SpecScribe spike' })
</script>

<template>
  <SurfaceShell v-if="data" :page="data.page" :site="data.site" />
</template>
