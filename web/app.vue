<script setup lang="ts">
/**
 * `lang="en"` on `<html>` — the golden portal declares it (`PathUtil.RenderHeadOpen`) and a document with
 * no language is a real accessibility defect, not a cosmetic head difference: a screen reader falls back to
 * the user's system voice and reads English prose with the wrong phoneme set. Nuxt emits no `lang` by
 * default, so it is declared once here rather than per surface. [Story 23.3 AC #2, AC #5]
 */
useHead({ htmlAttrs: { lang: 'en' } })
</script>

<template>
  <NuxtPage />
</template>
