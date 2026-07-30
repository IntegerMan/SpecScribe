<script setup lang="ts">
/**
 * Requirement pages — `requirements.html` plus one detail page per FR/NFR/UX-DR (81 total).
 * [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `RequirementsTemplater` (`BuildIndexPage` / `BuildRequirementPage`).
 *
 * **Injected vocabulary this component owns:** the requirement identity block, the covering-epic list, the
 * satisfaction/status badge and the deferral-provenance note.
 *
 * **UX-DRs are first-class here.** `RequirementKind.Design` requirements render through this same family, so
 * anything this component styles must hold for a design requirement as well as an FR/NFR — the index is built
 * from `requirements.Everything`, not from the FR/NFR pair.
 *
 * ⚠️ **Each detail page skips its OWN id when linkified** (`skipRequirementId`), so FR25's page shows "FR25"
 * as plain text while every other page links it. That is deliberate — a page must not link to itself — and it
 * is carried through the region composition, not re-derived.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="requirement" />
</template>
