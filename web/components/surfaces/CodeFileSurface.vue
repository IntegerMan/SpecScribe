<script setup lang="ts">
/**
 * Source-code pages — `code/{path}.html`, 264 of them. [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `CodeFileTemplater` (`BuildPage` / `BuildPlaceholderPage`).
 *
 * **Injected vocabulary this component owns:** the pure-CSS radio tab strip (Insights | Code), the
 * `.code-line` gutter listing whose `#L{n}` deep links work through `:has(.code-line:target)`, the
 * referenced-by panel and the coupling/insight panels.
 *
 * **Prism rides the generic head projection, not a branch here.** `IrSurface` emits the `prism.css`/`prism.js`
 * pair whenever `page.needsPrism` is set, which the emitter derives from the markup — so highlighting is a
 * property of the page, not of this component. Nothing to add.
 *
 * ⚠️ **Two things this family does NOT have, both easy to misread as regressions:**
 * 1. **No reference links.** These pages are written with `linkify: false` — running the code linkifier over
 *    rendered source would rewrite text inside the listing itself.
 * 2. **No golden byte coverage.** The golden fixture cites no real repo files, so it emits **no code page at
 *    all** and the `GoldenContentFingerprint` never covered these 264 pages. `RegionCompositionCorpusProof`
 *    is what actually gates them, because it runs against the real repo.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="code-file" />
</template>
