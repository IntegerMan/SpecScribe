import { createStaticVNode, defineComponent, h, type PropType } from 'vue'

/**
 * The `<main id="main-content">` landmark for an IR-backed page, reproduced BYTE-FOR-BYTE. [23.3 AC #1/#2]
 *
 * Why this is not just `<main>` inside `PageShell`. Two things Vue adds to a template-authored element made
 * the `<main>` region fail parity, and neither is visible in the source:
 *
 *   1. `PageShell.vue` has `<style scoped>`, so Vue stamps EVERY element in its template with that SFC's
 *      `data-v-*` attribute — including `<main>`, whether or not a rule targets it. The golden open tag is
 *      `<main id="main-content" class="info-page">`; the shell's was
 *      `<main id="main-content" class="info-page" data-v-3520e30f>`.
 *   2. Slot content renders as a FRAGMENT, and Vue's SSR renderer brackets fragments with `<!--[-->` and
 *      `<!--]-->` anchors so client hydration can find their boundaries. Those landed immediately inside
 *      `<main>` and immediately before `</main>` — 32 bytes of hydration bookkeeping inside the exact region
 *      this story compares.
 *
 * A render function in a component with no `<style>` block emits neither: no scoped attribute to add, and
 * an element's children array is not a fragment. The result is the golden tag and the golden body, and
 * nothing else.
 *
 * This component owns the `#main-content` landmark for IR routes the way `PageShell` owns it for the app's
 * own routes — one component, one place it is spelled, and `npm run check:a11y` asserts over the emitted
 * HTML that every page ends up with exactly one.
 */
export default defineComponent({
  name: 'IrMain',
  props: {
    /** The golden `<main>` open tag's attributes beyond `id`, from `IrRegion.mainAttrs`. */
    attrs: { type: Object as PropType<Record<string, string>>, default: () => ({}) },
    /** The region between `<main …>` and `</main>`, verbatim. */
    html: { type: String, required: true },
  },
  setup(props) {
    // `id` first, then the golden's own attributes: Vue's SSR renderer emits props in insertion order, and
    // the golden emits `id` first too.
    return () => h('main', { id: 'main-content', ...props.attrs }, [createStaticVNode(props.html, 1)])
  },
})
