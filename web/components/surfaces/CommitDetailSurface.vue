<script setup lang="ts">
/**
 * Per-commit detail pages — `commit/{shortHash}.html`, 300 of them. [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `CommitDetailTemplater.BuildPage`.
 *
 * **Injected vocabulary this component owns:** the commit header (subject, author, authored date), the
 * numstat file table with its added/removed columns, and the per-file links out to `code/` pages.
 *
 * ⚠️ **This whole family exists only under `--deep-git`, and it is the family most likely to be MISSING
 * rather than wrong.** `GitMetrics` has a hard-coded 3,000 ms budget for `git log --numstat` that has been
 * measured at 6,496 ms cold, and it loses **silently at `errors=0`** — taking all 300 of these pages with it
 * (memory `gitmetrics-3s-timeout-silent-deep-git-loss`). A projection run against a default generate simply
 * has no routes here; that is an absent-input condition, not a rendering bug, and the IR manifest is the
 * authority on which pages exist.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="commit-detail" />
</template>
