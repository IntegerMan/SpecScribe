<script setup lang="ts">
/**
 * The chart/analytics singletons — 8 pages, and the highest-risk family per page in the whole migration.
 * [Story 23.4 AC #1, AC #2]
 *
 * **Owning C# templaters:** one each — `CadenceTemplater`, `CodeMapTemplater`, `DeepAnalyticsTemplater`,
 * `GitInsightsTemplater`, `ImpactMapTemplater`, `RiskQuadrantTemplater`, `TraceabilityTemplater`,
 * `WorkGraphTemplater`. Grouped because they share `Charts.Framed`'s panel vocabulary, which is the thing a
 * component can actually own; their bodies differ but their chrome does not.
 *
 * **Injected vocabulary this component owns:** `Charts.Framed`'s panel shell — heading, ranking caption, note,
 * body, "why this matters" — matching `ChartPanel.vue`'s slot order, plus the inline SVG charts and the
 * Hierarchy Explorer mount points.
 *
 * **Charts boot BY REUSE, and that is ADR 0012 §Decision 2, not convenience.** `IrSurface` loads the shipped
 * `specscribe.js` (copied, never forked — CONVENTIONS.md §11) which calls `initHierarchyExplorers(document)`
 * and re-runs on `specscribe:content-swapped`. Re-implementing the explorer in Vue would create exactly the
 * second implementation that ADR forbids.
 *
 * ⚠️ **Three things to know before touching this family:**
 * 1. **The two oversized pages live here.** `code-map.html` and `git-insights.html` are the manifest's only
 *    declared `oversizedPages` entries (3.45 MB and 2.65 MB of chunk). Componentizing, parity-diffing and
 *    prerendering each cost differently — plan for them rather than discovering them when a harness hangs.
 * 2. **`deep-analytics.html` carries content AFTER `</main>`** — a `:target` lightbox (`#coupling-zoom`). The
 *    pre-23.4 slicer truncated at `</main>` and dropped it, so its "Expand" link resolved to nothing in the
 *    IR; the composed region restores it (Story 23.4 Task 2, pinned by `RegionCompositionCorpusProof`).
 * 3. **Every chart needs its text twin** ([ADR 0013](../../../docs/adrs/0013-text-twin-is-the-no-js-contract.md))
 *    and no state may be signalled by colour alone (UX-DR17). The twin is server-rendered into the region, so
 *    it survives here by construction — but a JS-off pass is still the gate that proves it.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="insight" />
</template>
