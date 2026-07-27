import { createStaticVNode, defineComponent } from 'vue'

/**
 * Injects a run of IR-rendered HTML with NO wrapper element of its own. [Story 23.3 Task 2/4]
 *
 * `v-html` needs a host element to set `innerHTML` on, and every one of those hosts would be an extra node
 * the golden page does not have. That matters in three places at once:
 *
 *   - the `<main>` region is compared byte-for-byte, so a wrapper inside it is an outright parity failure;
 *   - the portal's stylesheet has direct-child selectors (`.site-nav > …`, `.dashboard > …`), which a
 *     wrapper silently breaks — the class of failure the suite structurally cannot see;
 *   - an extra generic `<div>` around the site nav muddies the document's landmark structure.
 *
 * `createStaticVNode` is the same primitive Vue's own compiler uses for hoisted static markup: the SSR
 * renderer pushes the string verbatim into the output stream. IR-backed routes are prerendered with
 * `noScripts: true` and never hydrate (see `nuxt.config.ts`), so the client-side node-claiming half of a
 * static vnode never runs.
 *
 * The injected string cannot introduce script execution: `SpaDelivery` classifies every script in the
 * region and this repo's are all inert `type="application/json"` data islands — see `IrPage.hasDataIsland`
 * / `hasExecutableIsland`, and `IrSurface.vue`, which asserts the executable case never appears.
 */
export default defineComponent({
  name: 'IrHtml',
  props: {
    html: { type: String, required: true },
  },
  setup(props) {
    return () => createStaticVNode(props.html, 1)
  },
})
