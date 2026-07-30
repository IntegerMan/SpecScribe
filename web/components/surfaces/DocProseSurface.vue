<script setup lang="ts">
/**
 * Doc-prose pages — the single largest family this story migrates. [Story 23.4 AC #1]
 *
 * **Owning C# templater:** `HtmlTemplater.BuildDocPage` (one templater, six path shapes: `adrs/`,
 * `implementation-artifacts/`, `planning-artifacty/`, `specs/`, `readme.html`, `project-context.html`).
 * That is why they share one component rather than six — see `ir/families.ts` for the boundary rule.
 *
 * **Injected vocabulary this component owns:** `header.doc-header` (+ `h1`, the frontmatter chips),
 * `article.doc-body` (arbitrary Markdig output — headings, paragraphs, lists, tables, blockquotes, fenced
 * code) and the optional `nav.toc-sidebar` companion rail.
 *
 * ⚠️ **The Markdig body cannot be decomposed into components, and that is a ratified position, not a
 * shortcut.** [ADR 0016](../../../docs/adrs/0016-ir-carries-rendered-prose-html.md) puts *rendered prose
 * HTML* in the IR precisely because arbitrary markdown has no fixed element set to model. So this family is
 * the one that most needs authored prose styling under owner decision **D5** — and per CONVENTIONS.md §3
 * those rules must be written inside `:deep()`, because a plain scoped rule matches injected markup
 * NOWHERE and fails **silently**.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="doc-prose" />
</template>
