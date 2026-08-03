<script setup lang="ts">
/**
 * The shared IR-backed page: head projection + region injection + chart boot. [Story 23.3 AC #2/#5/#6/#7]
 *
 * The four migrated families wrap this rather than repeating it. Writing four near-identical components to
 * make the migration look bigger would be the wrong kind of honesty — what actually differs per family is
 * declared by the family components around this one, and everything they share lives here once.
 */
import { relativePrefix, site, type IrPage } from '#ir'
import type { IrFamily } from '../../ir/families'
import IrHtml from '../IrHtml'
import IrMain from '../IrMain'
import PageShell from '../PageShell.vue'

const props = withDefaults(
  defineProps<{
    page: IrPage
    /**
     * Which migrated family this is, or `pass-through`. Surfaces the classification in the DOM
     * (`data-ir-family`) so the harnesses and a live inspection can both tell a migrated page from one
     * that is only here to make the link graph resolve.
     *
     * The union lives in `ir/families.ts` alongside `resolveFamily`, so the classifier and the renderable
     * set cannot drift apart — adding a family to the table without giving it a component is then a type
     * error rather than a page that silently renders as a pass-through. [Story 23.4 AC #1]
     */
    family: IrFamily
  }>(),
  {},
)

const page = props.page

// `v-html` never executes injected <script> tags, so an executable island in the IR would arrive as dead
// markup — the page would look fine and quietly do nothing. Fail at build time instead. Today the emitter
// only ships inert `type="application/json"` data islands, which is exactly what the explorer reads.
if (page.hasExecutableIsland) {
  throw new Error(
    `IR page "${page.path}" carries an EXECUTABLE script island. Injected markup never runs scripts, so it ` +
      `would be silently inert here. Give it a real home in the Nuxt layer (see the chart boot below) ` +
      `before shipping this page.`,
  )
}

/**
 * Every asset href is written against the page's OWN depth — the same `../` prefix `PathUtil.RelativePrefix`
 * computes on the C# side. This is what "routes are the IR's paths verbatim" buys: the head's links and the
 * injected content's links use one and the same relative scheme, and neither is ever rewritten.
 */
const prefix = relativePrefix(page.path)
const bust = site.assetVersion ? `?v=${site.assetVersion}` : ''

/**
 * The head projection. [AC #5]
 *
 * Field-by-field against `PathUtil.RenderHeadOpen`:
 *   charset, viewport ......... Nuxt emits both by default
 *   <title> ................... IR `head.title`
 *   description ............... IR `head.description` (the emitter already applied the title fallback)
 *   og:type/title/description . constant + the two above, the same derivation the C# side hard-codes
 *   favicon ................... copied off the generated site (see adapter — a named gap for Epic 22)
 *   script ?v= ................ reproduced verbatim
 *   stylesheet ?v= ............ DELIBERATE DIFFERENCE — Nuxt owns CSS delivery here, and it links the app's
 *                               own generated layer (tokens + base + ir-content) instead of the 7,041-line
 *                               monolith. Linking `specscribe.css` would reverse 23.2's central decision to
 *                               get out of the monolith. Recorded in the parity report.
 *   extraHead ................. Prism's pair, when the page carries highlighted code. The IR has no
 *                               structured extraHead projection; this is derived from the markup.
 */
useHead({
  title: page.head.title,
  meta: [
    { name: 'description', content: page.head.description },
    { property: 'og:type', content: 'website' },
    { property: 'og:title', content: page.head.title },
    { property: 'og:description', content: page.head.description },
  ],
  link: [
    ...(site.faviconDataUri ? [{ rel: 'icon', href: site.faviconDataUri }] : []),
    ...(page.needsPrism ? [{ rel: 'stylesheet', href: `${prefix}prism.css${bust}` }] : []),
  ],
  script: [
    /**
     * The chart boot, BY REUSE. [AC #7, ADR 0012 §Decision 2]
     *
     * This is the shipped `specscribe.js` — the same file the portal serves, copied (never forked) by
     * `npm run sync:assets`. It calls `initHierarchyExplorers(document)` at load and re-runs on the
     * existing `specscribe:content-swapped` event; no new API is needed and none is added. Re-implementing
     * the explorer in Vue would create the second implementation ADR 0012 exists to prevent.
     */
    { src: `${prefix}specscribe.js${bust}`, defer: true },
    ...(page.needsPrism ? [{ src: `${prefix}prism.js${bust}`, defer: true }] : []),
    /**
     * The anti-flash boot marker. Chrome-level in the golden page (emitted just before `<main>`, outside the
     * captured region), so it is absent here unless we re-emit it — and without it the fallback SVG paints
     * first and is then swapped, which is exactly the flash the marker exists to prevent. The script body
     * is copied off the generated site, not re-typed.
     */
    ...(page.needsHierarchyEngine && site.hierarchyBootScript
      ? [{ innerHTML: site.hierarchyBootScript, tagPosition: 'head' as const }]
      : []),
    /**
     * The relationship graph's own anti-flash marker, on the same head seam and for the same reason.
     * [Story 23.6 / 24.2] Absent entirely before Story 23.6 — see `IrPage.needsGraphEngine`.
     */
    ...(page.needsGraphEngine && site.graphBootScript
      ? [{ innerHTML: site.graphBootScript, tagPosition: 'head' as const }]
      : []),
    /**
     * The charting engine, at body close and unversioned — the golden page's exact placement, which also
     * guarantees `Plotly` is defined before the deferred `specscribe.js` runs its auto-init.
     *
     * EITHER flag pulls it and a page carrying both a hierarchy and a graph still emits exactly one tag —
     * the same `||` the C# writer applied, because ADR 0030 put both components in this one bundle.
     */
    ...(page.needsHierarchyEngine || page.needsGraphEngine
      ? [{ src: `${prefix}plotly-hierarchy.min.js`, tagPosition: 'bodyClose' as const }]
      : []),
    /**
     * The TOC active-section tracker, at body close — it queries `.toc-sidebar` immediately and returns if
     * absent, so it must run after the region is in the DOM. [Story 23.6]
     */
    ...(page.needsToc && site.tocActiveSectionScript
      ? [{ innerHTML: site.tocActiveSectionScript, tagPosition: 'bodyClose' as const }]
      : []),
    /**
     * The mermaid init, at body close. `type: 'module'` is load-bearing, not decoration: the body is an ES
     * `import` of mermaid from the CDN and does nothing as a classic script. `startOnLoad: true` means it
     * renders every `<pre class="mermaid">` itself once the module evaluates.
     *
     * ⚠️ Nuxt shipped NO mermaid init at all before Story 23.6, so every diagram on the rendered portal was an
     * inert code block. See `IrPage.needsMermaid`.
     */
    ...(page.needsMermaid && site.mermaidInitScript
      ? [{
          innerHTML: site.mermaidInitScript,
          type: 'module',
          tagPosition: 'bodyClose' as const,
        }]
      : []),
  ],
})
</script>

<template>
  <!--
    `ir-content` falls through onto PageShell's root element and is the scope `assets/ir-content.css` is
    generated under (AC #6) — every rule in that sheet is a `.ir-content …` descendant, so the extracted
    monolith rules cannot reach template-authored components.

    `chrome="nav-only"`: the injected region already carries the site nav and the page's own h1. A second
    h1 from the shell would be a duplicate heading and a duplicate title.
  -->
  <PageShell
    class="ir-content"
    chrome="nav-only"
    :title="page.title"
    :data-ir-family="family"
    :data-ir-path="page.path"
  >
    <template #nav>
      <IrHtml :html="page.region.navHtml" />
      <IrHtml v-if="page.region.wayfindingHtml" :html="page.region.wayfindingHtml" />
    </template>

    <!-- The `<main>` landmark and its body, both verbatim. See IrMain for why it is not PageShell's. -->
    <IrMain :attrs="page.region.mainAttrs" :html="page.region.mainInnerHtml" />

    <!--
      Content the region carries AFTER `</main>` — normally nothing. It MUST render outside the landmark, not
      inside it: the one page that uses this is `deep-analytics.html`, whose `:target` lightbox has to sit
      outside the scrolling region it overlays. Injecting it into `<main>` would satisfy "the markup is on the
      page" while breaking both the overlay and the single-landmark a11y invariant.
      See IrRegion.trailingHtml — dropping this shipped a dead "Expand" link twice over. [Story 23.4]
    -->
    <IrHtml v-if="page.region.trailingHtml" :html="page.region.trailingHtml" />

    <template #footer>
      <!--
        The golden footer sits OUTSIDE `<main>` and is not part of the captured region (it carries the
        wall-clock generation timestamp the golden gate normalizes away). Not reproduced; recorded as a
        named gap alongside the rest of the chrome projection.
      -->
      <p>Rendered from the SpecScribe IR.</p>
    </template>
  </PageShell>
</template>
