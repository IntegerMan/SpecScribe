<script setup lang="ts">
/**
 * Activity-by-day pages — `commits/{date}.html` (26) plus `timeline.html`. 27 pages. [Story 23.4 AC #1]
 *
 * **Owning C# templaters:** `CommitDayTemplater.BuildPage` and `TimelineTemplater.BuildPage`. Grouped despite
 * unrelated-looking paths because they emit one vocabulary — the dated activity list — and `timeline.html` is
 * simply its whole-project roll-up. This is the clearest case for `ir/families.ts`'s templater-not-prefix rule.
 *
 * **Injected vocabulary this component owns:** the per-day heading, the commit list with its short-hash links
 * out to `commit/` pages, and the artifact-touched list.
 *
 * ⚠️ **Dates here are GIT-derived, never filesystem mtime.** Story 7.3 removed the mtime signal because it
 * collapsed every artifact onto the checkout day; a repo whose git history cannot be read produces NEITHER
 * `timeline.html` NOR any `commits/` page. So an empty route set for this family is the honest degradation of
 * "drop the claim when git can't verify it", not a missing render — and aggregates are bound to `<= today` via
 * the shared `Charts.ResolveToday` cutoff, so a future-dated commit cannot stretch the axis.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="commit-day" />
</template>
