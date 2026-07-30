<script setup lang="ts">
/**
 * The sprint board — `sprint.html`. One page. [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `SprintTemplater.BuildIndexPage`.
 *
 * **Injected vocabulary this component owns:** the by-column and by-epic board layouts, the story cards with
 * their `StatusBadge` stage pills, and the progress wheel.
 *
 * **Its own family of one, deliberately.** The board's column/card vocabulary is shared with the dashboard's
 * "Now & Next" widget rather than with any other long-tail page (that widget *becomes* the 3-per-column sprint
 * board when sprint data exists), so folding it into `doc-prose` or `insight` would put its styling in the
 * wrong owner. One page is the right size for a family when nothing else emits its markup.
 *
 * ⚠️ **Absent when there is no sprint data, and absent means absent** — a present-but-malformed
 * `sprint-status.yaml` parses to null and the page, the nav entry and the dashboard widget all omit together
 * (NFR2 graceful degradation, NFR8 "absent, not an empty page"). Never render a placeholder here.
 *
 * ⚠️ **`StatusBadge` requires an explicit `label`** and carries no stage→word map by design (UX-DR17 enforced
 * by shape) — so no state is signalled by colour alone. Anything this component adds must keep that true.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="sprint" />
</template>
