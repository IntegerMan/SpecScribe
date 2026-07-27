/**
 * Stops Nuxt asking the prerenderer for a `_payload.json` beside every IR page. [Story 23.3 AC #4, AC #8]
 *
 * Two reasons, and the first one is fatal rather than cosmetic:
 *
 * 1. IT COLLIDES WITH THE PAGE ITSELF. Payload extraction appends an `x-nitro-prerender` response header
 *    naming `<route>/_payload.json`, and nitro enqueues it. Because routes here are the IR's paths verbatim
 *    — `/epics.html`, not `/epics` — the payload's parent directory is the same name as the page file, and
 *    the prerender dies with `EEXIST: file already exists, mkdir '…/about-sdd-bmad.html'`.
 *
 * 2. IT WOULD BE DEAD WEIGHT. These routes carry `noScripts: true` (see `nuxt.config.ts`): no Nuxt runtime
 *    ships, nothing hydrates, and a payload no client can fetch is bytes with no reader — 1,042 of them.
 *
 * Deliberately NOT solved by turning `experimental.payloadExtraction` off globally: Story 23.2's AC #4
 * measurement compares three data-loading shapes by their `_payload.json` weight, and a global switch would
 * silently make that table unreproducible. Scoped to the `.html` route space, which IS the IR route space —
 * the app's own routes (`/design-system`, `/measure/*`) have no extension and keep their payloads.
 */
export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('beforeResponse', (event) => {
    const path = event.path.split('?')[0]!
    // `/` is the site root, which resolves to the IR's entry page and is therefore an IR route too.
    if (path !== '/' && !path.endsWith('.html')) return
    removeResponseHeader(event, 'x-nitro-prerender')
  })
})
