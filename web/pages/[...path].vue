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
import { resolveFamily, type IrFamily } from '~/ir/families'
import CodeFileSurface from '~/components/surfaces/CodeFileSurface.vue'
import CommitDaySurface from '~/components/surfaces/CommitDaySurface.vue'
import CommitDetailSurface from '~/components/surfaces/CommitDetailSurface.vue'
import DashboardSurface from '~/components/surfaces/DashboardSurface.vue'
import DocProseSurface from '~/components/surfaces/DocProseSurface.vue'
import EpicDetailSurface from '~/components/surfaces/EpicDetailSurface.vue'
import EpicsIndexSurface from '~/components/surfaces/EpicsIndexSurface.vue'
import FollowUpSurface from '~/components/surfaces/FollowUpSurface.vue'
import InsightSurface from '~/components/surfaces/InsightSurface.vue'
import PassThroughSurface from '~/components/surfaces/PassThroughSurface.vue'
import PortalMetaSurface from '~/components/surfaces/PortalMetaSurface.vue'
import RequirementSurface from '~/components/surfaces/RequirementSurface.vue'
import RetroSurface from '~/components/surfaces/RetroSurface.vue'
import SprintSurface from '~/components/surfaces/SprintSurface.vue'
import StoryDetailSurface from '~/components/surfaces/StoryDetailSurface.vue'

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
 * The branch, now a total map rather than a ternary ladder. [Story 23.4 AC #1]
 *
 * Classification lives in `ir/families.ts` (one table, testable for completeness against the real manifest);
 * this object only says which component renders each family. Keying it by the `IrFamily` union with an
 * exhaustive `Record` means adding a family to the classifier WITHOUT giving it a component is a type error,
 * not a page that silently renders as a pass-through — which was the one failure mode the ladder could hide.
 *
 * `pass-through` stays reachable on purpose: `resolveFamily` returns it for a path nothing claims, so an
 * unplanned page shape announces itself as `data-ir-family="pass-through"` instead of throwing.
 * `test/families.test.ts` asserts the real IR leaves that bucket EMPTY.
 */
const SURFACES: Record<IrFamily, Component> = {
  dashboard: DashboardSurface,
  'epics-index': EpicsIndexSurface,
  'epic-detail': EpicDetailSurface,
  'story-detail': StoryDetailSurface,
  'doc-prose': DocProseSurface,
  requirement: RequirementSurface,
  'follow-up': FollowUpSurface,
  'commit-detail': CommitDetailSurface,
  'commit-day': CommitDaySurface,
  'code-file': CodeFileSurface,
  insight: InsightSurface,
  'portal-meta': PortalMetaSurface,
  sprint: SprintSurface,
  retro: RetroSurface,
  'pass-through': PassThroughSurface,
}

const surface = SURFACES[resolveFamily(path, site.entry)]
</script>

<template>
  <component :is="surface" :page="page" />
</template>
