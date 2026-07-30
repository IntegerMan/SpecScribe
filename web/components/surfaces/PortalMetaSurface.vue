<script setup lang="ts">
/**
 * Pages ABOUT the portal — `about`, `about-sdd*` (7), `how-to-read`, `design-system`, `diagnostics`.
 * 11 pages. [Story 23.4 AC #1]
 *
 * **Owning C# templaters:** `AboutTemplater`, `AboutSddTemplater` (`BuildHubPage` / `BuildFrameworkPage`),
 * `HowToReadTemplater`, `DesignSystemTemplater`, `DiagnosticsTemplater`. Grouped on a shared constraint rather
 * than a shared path, see below.
 *
 * **Injected vocabulary this component owns:** `header.doc-header` followed by plain prose sections, the SDD
 * framework comparison tables, the design-system token/status swatch grids and the diagnostics notice list.
 *
 * ⚠️ **The shared constraint that defines this family: NONE of these pages is reference-linkified, and for
 * these pages that is a semantic requirement rather than a corruption guard.** `how-to-read` and
 * `design-system` DEFINE the portal's vocabulary — the glossary terms and the status tokens — so running the
 * abbreviation expander over them would have the page self-expand the very terms it is teaching, wrapping its
 * own definitions in `<abbr title="…">`. `AboutTemplater`/`DesignSystemTemplater`/`HowToReadTemplater` each
 * carry a doc comment saying exactly this. So these regions show plain "FR"/"ADR" text where a doc-prose page
 * shows an `<abbr>`, and "restoring" it would be a regression.
 *
 * **`about.html` and `diagnostics.html` emit their doc-header BEFORE `<main>`**, which is why Story 23.4's
 * finding 1 required `BodyHtml` to start at `header.doc-header` rather than at the landmark — a body that
 * began at `<main>` would render the static page correctly while silently dropping the page's own title block
 * from the IR.
 */
import type { IrPage } from '#ir'
import IrSurface from './IrSurface.vue'

defineProps<{ page: IrPage }>()
</script>

<template>
  <IrSurface :page="page" family="portal-meta" />
</template>
