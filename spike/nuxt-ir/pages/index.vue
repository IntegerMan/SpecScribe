<script setup lang="ts">
import { SURFACES } from '~/ir/adapter.mjs'

useHead({ title: 'SpecScribe — Story 23.1 Nuxt-over-IR spike' })
</script>

<template>
  <div class="index">
    <h1>Story 23.1 — Nuxt-over-IR feasibility spike</h1>
    <p>
      Four SpecScribe surfaces rendered by Nuxt 3 (universal/SSR, fully prerendered) from the proxy IR
      (<code>spa/manifest.json</code> + <code>spa/pages-*.json</code>). Throwaway — the deliverable is the report.
    </p>
    <!-- Plain anchors via NuxtLink render as real <a href> in the prerendered HTML, so this index is navigable
         with JavaScript disabled. That is the NFR6 evidence for in-page navigation. -->
    <ul>
      <li v-for="s in SURFACES" :key="s.route">
        <NuxtLink :to="s.route">{{ s.route }}</NuxtLink> — {{ s.probe }}
        <span class="src">({{ s.irPath }})</span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.index {
  max-width: 48rem;
  margin: 0 auto;
  padding: 2rem 1rem;
}

.src {
  opacity: 0.6;
  font-size: 0.8rem;
}
</style>
