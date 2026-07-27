<script setup lang="ts">
/**
 * The single catch-all. ALL IR routing goes through here. [Story 23.3 AC #3, AC #4, Task 3]
 *
 * Why one catch-all rather than Nuxt's file-based routing: routes are the IR's output-relative paths
 * VERBATIM, extension and all — `/index.html`, `/epics.html`, `/epics/epic-3.html`. Nuxt's page files
 * cannot express those (there is no valid `pages/epics.html.vue`), and the alternative — rewriting the IR's
 * paths into a clean extension-less route space — would force every injected `<a href>` to be rewritten
 * too, which is precisely what AC #1's byte-parity claim cannot survive.
 *
 * The IR data is resolved HERE, at module scope through `#ir`, with no data composable: CONVENTIONS.md §4's
 * measured variant C (1.00x, against 1.36x for `useAsyncData` and 1.99x for `<NuxtIsland>`). `#ir` is
 * re-pointed at a throwing stub for the client build and these routes are prerendered with
 * `noScripts: true`, so none of it reaches a browser.
 */
import { hasPage, page as irPage, site } from '#ir'
import DashboardSurface from '~/components/surfaces/DashboardSurface.vue'
import EpicDetailSurface from '~/components/surfaces/EpicDetailSurface.vue'
import EpicsIndexSurface from '~/components/surfaces/EpicsIndexSurface.vue'
import PassThroughSurface from '~/components/surfaces/PassThroughSurface.vue'
import StoryDetailSurface from '~/components/surfaces/StoryDetailSurface.vue'

/** `epics/epic-{N}.html` — `EpicsViewBuilder`'s path shape. */
const EPIC_DETAIL = /^epics\/epic-[^/]+\.html$/
/** `epics/story-{id}.html`, dots already replaced by dashes — `StoryEpicLinkifier.StoryPagePath`. */
const STORY_DETAIL = /^epics\/story-[^/]+\.html$/

const route = useRoute()

/**
 * `/` resolves to the manifest's entry page, so the site root works. Everything else is the route path with
 * its leading slash removed — which, because routes mirror IR paths, IS the IR key. No mapping table.
 */
const raw = Array.isArray(route.params.path) ? route.params.path.join('/') : String(route.params.path ?? '')
const path = raw === '' ? site.entry : raw

if (!hasPage(path)) {
  // The route table is built from the manifest, so a miss means the two disagree — a bug, not a 404 to be
  // rendered politely.
  throw createError({ statusCode: 404, statusMessage: `No IR page for "${path}"`, fatal: true })
}

const page = irPage(path)

/**
 * The branch. The four migrated families get their own component; everything else falls through to a
 * pass-through, which exists so the link graph resolves end to end and is explicitly 23.4's to upgrade.
 */
const surface =
  path === site.entry
    ? DashboardSurface
    : path === 'epics.html'
      ? EpicsIndexSurface
      : EPIC_DETAIL.test(path)
        ? EpicDetailSurface
        : STORY_DETAIL.test(path)
          ? StoryDetailSurface
          : PassThroughSurface
</script>

<template>
  <component :is="surface" :page="page" />
</template>
