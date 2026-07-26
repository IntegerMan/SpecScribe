// SpecScribe's production-intent Nuxt 3 app (Epic 23, Story 23.2).
//
// Universal/SSR + full prerender — ADR 0009's ratified Axis 1 = Option B. `nuxt generate` must emit
// fully-rendered HTML per route (NFR-5/NFR6), never a hydration shell.
//
// NOT wired into `specscribe generate` and NOT in SpecScribe.slnx. How this ships is Story 23.5's decision,
// which is sequenced AHEAD of 23.4 precisely because it is Epic 23's load-bearing unknown.

export default defineNuxtConfig({
  compatibilityDate: '2026-07-24',
  ssr: true,
  telemetry: false,
  devtools: { enabled: false },

  // The token bridge (AC #1) plus the app's own minimal base layer. tokens.css is GENERATED from the C#
  // stylesheet by `npm run extract:tokens` — never hand-edit it; `npm run check:tokens` fails on drift.
  // Deliberately NOT importing specscribe.css wholesale (the 23.1 spike's shape).
  css: ['~/assets/tokens.css', '~/assets/base.css'],

  experimental: {
    // Enables `.server.vue` server components / <NuxtIsland>, which AC #4 measures against the async-data
    // path's hydration-payload duplication. See scripts/measure-payload.mjs and CONVENTIONS.md.
    componentIslands: true,
  },

  nitro: {
    prerender: {
      // 23.1 spike finding: `crawlLinks: true` is unusable here. Nitro's crawler walks every <a href> in the
      // rendered HTML — including links inside v-html'd IR content — and aborts the build on the first 404.
      // The route table must be declared (later, from the IR manifest; today, by hand).
      crawlLinks: false,
      routes: [
        '/',
        '/design-system',
        // AC #4's payload experiment: three shapes of the SAME primitive, measured per-route.
        '/measure/async',
        '/measure/island',
        '/measure/static',
      ],
    },
  },
})
