<script setup lang="ts">
/**
 * Follow-up pages — the most populous family in the IR (412 pages). [Story 23.4 AC #1]
 *
 * **Owning C# templaters:** `FollowUpDetailTemplater` (`BuildActionPage` / `BuildDeferredPage`),
 * `FollowUpGroupTemplater`, `ActionItemsTemplater` and `DeferredWorkTemplater` — grouped because they emit
 * one vocabulary: the follow-up card, its provenance pills and the list-batch action pane.
 *
 * **Injected vocabulary this component owns:** `.follow-up-card`, `.follow-up-meta`, the epic/provenance
 * pills, `.list-batch` and its `data-copy` payload buttons.
 *
 * ⚠️ **These regions carry NO reference links, deliberately, and it must not read as a defect.** Every page in
 * this family is written with `linkify: false` on the C# side, because the Resolve/Address `data-copy`
 * payloads embed raw item text that can contain "Story N.M"/"Epic N"/"FR-N" — a linkifier wraps those in
 * `<a>` INSIDE the attribute value and corrupts the copyable command. So a follow-up page shows plain
 * "Story 23.4" where a doc-prose page shows a link, and that asymmetry is the correct behaviour.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="follow-up" />
</template>
