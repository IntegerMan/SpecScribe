<script setup lang="ts">
/**
 * The app's error page — and the source of the `200.html` / `404.html` fallbacks `nuxt generate` emits for
 * static hosts. [Story 23.3 AC #2]
 *
 * Added because `npm run check:a11y` found them: Nuxt's built-in error template carries no `<html lang>`,
 * no skip link and no `<main>` landmark, so the two pages a visitor reaches by mistyping a URL were the
 * only two pages on the site failing every structural convention the other 1,047 hold. Routing them through
 * `PageShell` fixes all four in one move, because the shell is where those contracts live.
 *
 * Deliberately plain: it does not reach for the IR. An error page that needs the data layer to work is an
 * error page that fails when the data layer is what went wrong.
 */
const props = defineProps<{ error: { statusCode?: number; statusMessage?: string } }>()

const code = computed(() => props.error?.statusCode ?? 500)
const title = computed(() => (code.value === 404 ? 'Page not found' : 'Something went wrong'))

useHead({ title: () => `${title.value} — SpecScribe` })
</script>

<template>
  <PageShell :title="title" :subtitle="`HTTP ${code}`">
    <ChartPanel title="What happened">
      <p class="error-body">
        <template v-if="code === 404">
          There is no page at this address. The portal's pages mirror the generated site's own paths, so a
          link from outside may be pointing at something that has since been renamed.
        </template>
        <template v-else>
          {{ error?.statusMessage || 'The page could not be rendered.' }}
        </template>
      </p>
      <p class="error-body">
        <a href="/index.html">Go to the project dashboard</a>
      </p>
    </ChartPanel>
  </PageShell>
</template>

<style scoped>
.error-body {
  margin: 0 0 0.75rem;
  max-width: 42rem;
  text-wrap: pretty;
}
</style>
