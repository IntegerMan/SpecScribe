import { existsSync, readFileSync } from 'node:fs'

// The route table comes from the IR itself — `manifest.pages` IS the complete route list, which is also the
// answer to the crawlLinks problem below.
//
// This FAILS rather than falling back. A silent fallback (it used to default to `['/dashboard']`) meant that
// forgetting `npm run ir` produced a plausible-looking 1-route build that measure.mjs would then score as if it
// were the full run — the wrong failure mode for a harness whose entire output is wall-clock and weight figures.
const irFile = new URL('./ir-data/ir.json', import.meta.url)
if (!existsSync(irFile)) {
  throw new Error('spike/nuxt-ir: ir-data/ir.json is missing — run `npm run ir` before `npm run generate`.')
}
const parsedIr = JSON.parse(readFileSync(irFile, 'utf8'))
if (!parsedIr.pages || typeof parsedIr.pages !== 'object') {
  throw new Error('spike/nuxt-ir: ir-data/ir.json has no `pages` map — regenerate with `npm run ir`.')
}
const routes: string[] = Object.keys(parsedIr.pages)

// Story 23.1 spike config. Universal/SSR + full prerender — ADR 0009's ratified Axis 1 = Option B.
// `nuxt generate` must emit fully-rendered HTML per route (NFR6), not a hydration shell.
export default defineNuxtConfig({
  compatibilityDate: '2026-07-23',
  ssr: true,
  telemetry: false,
  devtools: { enabled: false },

  // SPIKE_NO_PAYLOAD_EXTRACTION=1 inlines the hydration payload into the HTML instead of emitting a sibling
  // `_payload.json`. Measured because it is the one lever that removes the payload FETCH — and the webview CSP
  // has no `connect-src` at all (default-src 'none'), so that fetch is unconditionally blocked there.
  experimental: {
    // Accept the usual truthy spellings, not just the literal '1' — a `SPIKE_NO_PAYLOAD_EXTRACTION=true` run
    // silently measured the DEFAULT build and would have been reported as the payload-inlined variant.
    payloadExtraction: !['1', 'true', 'yes'].includes(
      (process.env.SPIKE_NO_PAYLOAD_EXTRACTION ?? '').trim().toLowerCase(),
    ),
  },

  app: {
    head: {
      // The shipped stylesheet, copied verbatim by scripts/extract-ir.mjs — the token source of truth (AD-7).
      link: [{ rel: 'stylesheet', href: '/specscribe.css' }],
    },
  },

  nitro: {
    prerender: {
      // SPIKE FINDING: `crawlLinks: true` fails hard here. Nitro's crawler walks EVERY <a href> in the
      // rendered HTML — including the links inside the v-html'd IR content (`spike/delivery`, `code/…`,
      // sibling story pages) — and aborts the build on the first 404. Injected host-rendered HTML brings its
      // own link graph, which the crawler cannot distinguish from Nuxt routes. The route table must come from
      // the IR manifest instead (which is exactly what it is: `manifest.pages` is the complete route list).
      crawlLinks: false,
      routes: ['/', ...routes],
    },
  },
})
