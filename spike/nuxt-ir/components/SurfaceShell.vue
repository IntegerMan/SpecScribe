<script setup lang="ts">
// The parallel copy of the C# presentation shell. Deliberately thin: the IR already carries the page's
// rendered content (nav + main), so the Vue side supplies chrome + injection, not re-rendering.
defineProps<{
  page: { path: string; title: string; contentHtml: string; probe?: string }
  site: { title: string }
}>()
</script>

<template>
  <div class="shell">
    <header class="shell-head">
      <p class="shell-kicker">{{ site.title }} · Story 23.1 Nuxt-over-IR spike</p>
      <h1 class="shell-title">{{ page.title }}</h1>
      <p class="shell-meta">
        IR page <code>{{ page.path }}</code> · probe: {{ page.probe }} ·
        <NuxtLink to="/">back to spike index</NuxtLink>
      </p>
    </header>

    <StatusLegend />

    <!-- AC #2: the IR's pre-rendered content — including every chart SVG and every Markdig custom-renderer
         block — injected as trusted build-time data. Nothing is re-rendered in JS. -->
    <div class="ir-content" v-html="page.contentHtml" />
  </div>
</template>

<style scoped>
.shell {
  max-width: 72rem;
  margin: 0 auto;
  padding: 1.5rem 1rem 4rem;
}

.shell-head {
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 2px solid var(--status-active, #2e6b7a);
}

.shell-kicker {
  margin: 0;
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  opacity: 0.7;
}

.shell-title {
  margin: 0.2rem 0;
  font-size: 1.5rem;
}

.shell-meta {
  margin: 0;
  font-size: 0.8rem;
  opacity: 0.8;
}

/* NOTE (spike finding): `scoped` styles do NOT reach v-html-injected markup — Vue only stamps the data-v-*
   attribute onto template-authored elements. Reaching the IR's own markup needs `:deep()` (below) or a global
   stylesheet. This is the single biggest ergonomic surprise for the 23.2/23.3 migration. */
.ir-content :deep(.site-nav) {
  outline: 1px dashed var(--status-drafted, #e8d9a8);
  outline-offset: 2px;
}
</style>
