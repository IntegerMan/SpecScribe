<script setup lang="ts">
/**
 * ⚠️ NOT A MIGRATION CLAIM. Story 23.4 owns these pages.
 *
 * Every page in the IR manifest prerenders (owner decision D3), because that is the only way the link graph
 * can be proven to resolve END TO END: the four migrated families link out to code pages, ADRs, follow-ups,
 * requirements and commits, and a link check that stopped at the migrated set would be measuring its own
 * route table rather than the site.
 *
 * So a pass-through gets the shell, the head projection and the injected region — and nothing family-
 * specific. It is a resolving destination, not a migrated surface. Nothing in this story's parity table
 * covers it, and `data-ir-family="pass-through"` marks it in the emitted HTML so no later reader can mistake
 * one for finished work.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="pass-through" />
</template>
