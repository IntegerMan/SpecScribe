// SpecScribe's production-intent Nuxt app (Epic 23, Stories 23.2 + 23.3). Nuxt 4 — see `package.json`.
//
// Universal/SSR + full prerender — ADR 0009's ratified Axis 1 = Option B. `nuxt generate` must emit
// fully-rendered HTML per route (NFR-5/NFR6), never a hydration shell.
//
// NOT in SpecScribe.slnx — but this app IS wired into `specscribe generate`: `NuxtPrerender.cs` boots
// `web/.output/` as part of a run, and under ADR 0034 no C# code path emits a content page, so Node renders
// every page a user sees. (This comment read "NOT wired into `specscribe generate`" until Story 23.2's
// fourth review pass; 23.5 answered the packaging question and 23.6 retired the C# writer.)

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname as dirnameOf, resolve as resolvePath, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
// The ONE sanctioned relative import of the adapter: config runs in Node, before any alias exists, and the
// route table has to come from the IR manifest (Story 23.3 AC #4). App code imports `#ir` instead — see the
// alias block below for why that distinction is load-bearing rather than stylistic.
import { PACKAGE_BUILD, site } from './ir/adapter'

const IR_ADAPTER = fileURLToPath(new URL('./ir/adapter.ts', import.meta.url))
const IR_ADAPTER_CLIENT = fileURLToPath(new URL('./ir/adapter.client.ts', import.meta.url))

/**
 * The prerender route table, straight from the IR manifest. [Story 23.3 AC #4]
 *
 * Routes are the IR's output-relative paths VERBATIM, with a leading slash: `/index.html`, `/epics.html`,
 * `/epics/epic-3.html`. This is load-bearing, not cosmetic — it is the reason no href is ever rewritten.
 * The IR's content carries relative links written against each page's own depth (`../specscribe.js`,
 * `../epics.html`, `code/Foo.cs.html`); mirroring the emitter's path space means every one of them resolves
 * unchanged, so the injected strings stay byte-identical and the parity comparison stays honest. Rewriting
 * links into a clean extension-less route space was considered and rejected for exactly that reason.
 *
 * The consequence is that Nuxt's file-based routing cannot express these routes at all (there is no valid
 * `pages/epics.html.vue`), which is why everything funnels through one `pages/[...path].vue` catch-all.
 */
const irRoutes = site.paths.map((p) => `/${p}`)

/**
 * Dev knob: prerender only the first N IR routes. Unset (0) means all of them, which is what every gate and
 * every published number is produced from. It exists because a full run is 1,042 routes and iterating on
 * the pipeline one whole site at a time is how a fast mistake becomes a slow one.
 *
 * Never set in CI or when producing a measurement — `measure:parity` and `check:links` both report the
 * route count they saw, so a truncated run cannot be published as a full one by accident.
 */
const routeLimit = Number(process.env.SPECSCRIBE_IR_ROUTE_LIMIT ?? 0)
const prerenderIrRoutes = routeLimit > 0 ? irRoutes.slice(0, routeLimit) : irRoutes

export default defineNuxtConfig({
  compatibilityDate: '2026-07-24',
  ssr: true,
  telemetry: false,
  devtools: { enabled: false },

  // The token bridge (23.2 AC #1), the app's own minimal base layer, the two UNSCOPED layers — shared
  // primitives (ADR 0029) and runtime body-level classes (ADR 0039) — and the scoped IR-content layer
  // (23.3 AC #6). All four generated sheets come from the C# stylesheet — never hand-edit any of them;
  // `npm run check:tokens` and `npm run check:ir-content` fail on drift. Deliberately NOT importing
  // specscribe.css wholesale (the 23.1 spike's shape).
  //
  // ORDER IS LOAD-BEARING. shared-primitives.css comes BEFORE ir-content.css so the scoped layer can override
  // it: every `.ir-content …` selector is at least (0,2,0) against an unscoped primitive's (0,1,0), so the
  // cascade already agrees, and source order settles the case where a future shared rule ties. A component's
  // own `<style scoped>` also outranks it — `.list-row-chip[data-v-x]` beats `.pill` — which is what lets
  // ListRow keep its layout properties while inheriting the shared look.
  //
  // runtime-body.css sits with it, and its ordering is NOT load-bearing in the same way: nothing in the
  // scoped layer can name `.ss-tooltip` or the cards inside it, because the builder removes those selectors
  // from that layer rather than duplicating them. It is placed here so both unscoped layers read together.
  css: [
    '~/assets/tokens.css',
    '~/assets/base.css',
    '~/assets/shared-primitives.css',
    '~/assets/runtime-body.css',
    '~/assets/ir-content.css',
  ],

  /**
   * `#ir` is the ONLY specifier app code uses to reach the IR. [Story 23.3 AC #3]
   *
   * Declared for TypeScript ONLY. It is deliberately NOT a Nuxt `alias`: Vite's own alias plugin runs ahead
   * of every user plugin, including `enforce: 'pre'` ones, so an alias entry would resolve `#ir` to the
   * server adapter before the environment-aware resolver below ever sees it — and drag `node:fs` into the
   * browser bundle. Resolution belongs to the plugin; this entry only teaches the editor and `vue-tsc`.
   */
  typescript: {
    tsConfig: {
      compilerOptions: {
        paths: { '#ir': ['../ir/adapter'] },
      },
    },
  },

  vite: {
    plugins: [
      {
        /**
         * Resolves `#ir` per BUILD ENVIRONMENT: the real adapter for SSR/prerender, the throwing stub for
         * the browser.
         *
         * ⚠️ Why a plugin and not the obvious `vite:extendConfig` alias override. Under Nuxt's Vite
         * Environment API the hook is called once per environment with `{ ...config, environments }` — a
         * SHALLOW spread, so `config.resolve` is the SAME OBJECT both times. Mutating `resolve.alias`
         * inside an `isClient` branch therefore lands on the server build too, and the prerenderer renders
         * every IR route against the stub: 1,041 identical `[500] Server Error` lines with no message
         * attached (which is why `server/plugins/report-render-errors.ts` now exists).
         *
         * `resolveId` runs inside a known environment, so it cannot make that mistake.
         */
        name: 'specscribe:ir-environment-resolver',
        enforce: 'pre' as const,
        resolveId(this: { environment?: { name: string } }, id: string, _importer: string | undefined, options?: { ssr?: boolean }) {
          if (id !== '#ir') return null
          const isBrowser = this.environment ? this.environment.name === 'client' : !options?.ssr
          return isBrowser ? IR_ADAPTER_CLIENT : IR_ADAPTER
        },
      },
    ],
  },

  features: {
    // 1,042 prerendered pages sharing one large generated stylesheet: inlining would copy ir-content.css
    // into every page. Linked once, cached once.
    inlineStyles: false,
  },

  experimental: {
    // Enables `.server.vue` server components / <NuxtIsland>, which 23.2 AC #4 measured against the
    // async-data path. Kept so `npm run measure:payload` still reproduces that table.
    componentIslands: true,
  },

  routeRules: {
    /**
     * IR-backed routes ship NO Nuxt runtime. [Story 23.3 AC #8]
     *
     * These pages are fully prerendered content whose only interactivity is the portal's own vanilla
     * `specscribe.js` (ADR 0012's one Hierarchy Explorer, loaded by us — see `IrSurface.vue`). There is
     * nothing for Vue to hydrate, and hydrating would be actively wrong: the IR content is resolved at
     * build time and is deliberately not in the client bundle, so a hydration pass would find no data and
     * blank the page. `noScripts` makes the delivered page exactly what the server rendered.
     *
     * It also makes AC #8's payload assertion structural rather than measured-and-hoped: a route with no
     * scripts cannot carry a hydration payload at all.
     */
    '/**': { noScripts: true },
    // The app's OWN routes keep the runtime: the three `/measure/*` routes exist to reproduce 23.2 AC #4's
    // payload table, which is meaningless without it, and `/component-library` is the developer landing page.
    // (`/design-system` was retired on 2026-08-07 — see the prerender list below.)
    '/component-library': { noScripts: false },
    '/measure/**': { noScripts: false },
  },

  nitro: {
    prerender: {
      // 23.1 spike finding 8: `crawlLinks: true` is unusable here. Nitro's crawler walks every <a href> in
      // the rendered HTML — including links inside v-html'd IR content — and aborts the build on the first
      // 404. The route table is declared from the manifest instead, which is the correct design anyway.
      crawlLinks: false,
      /**
       * EMPTY under `SPECSCRIBE_PACKAGE_BUILD=1`. [Story 23.5 AC #5]
       *
       * A package build produces the project-INDEPENDENT renderer: no project's routes are baked in, and
       * `.output/public` therefore carries only real static assets. This matters for correctness, not just
       * weight — Nitro serves `public/` static files AHEAD of the SSR route, so a prebuilt artefact that
       * shipped project A's `/index.html` returned A's dashboard when pointed at project B, with a 200.
       * Story 23.5 measured exactly that before this branch existed.
       */
      routes: PACKAGE_BUILD
        ? []
        : [
            // The site root resolves to the manifest's entry page. Both `/` and `/index.html` are emitted: they
            // write the same file, and the second form is what the IR's own hrefs link to.
            '/',
            ...prerenderIrRoutes,
            // The app's own routes (not IR-backed).
            //
            // `/design-system` is deliberately ABSENT. [Story 23.2 review 2026-08-07] The Vue showcase route
            // was retired: under `PACKAGE_BUILD` this list is empty, so the route was never prerendered into
            // a shipped portal, and the design-system page a user actually gets is `design-system.html`,
            // rendered from the C#-composed region through `PortalMetaSurface`. Keeping a second, divergent
            // design-system page that no user could reach was a permanent duplication rather than the
            // transitional one AC #6 assumed. The `:deep()` worked example it carried now lives in
            // `CONVENTIONS.md` §3, which is where AC #5 says the convention must be demonstrated.
            '/component-library',
            '/measure/async',
            '/measure/island',
            '/measure/static',
          ],
    },
  },

  hooks: {
    /**
     * Writes the pages Nitro's path-traversal guard refuses to write. [Story 23.3 AC #4]
     *
     * Nitro's `canWriteToDisk` rejects any route whose path CONTAINS the substring `..` — not a `..` path
     * SEGMENT, the substring. SpecScribe emits a code page per repository file, so a source file whose name
     * legitimately contains two dots in a row produces a route Nitro silently declines: it renders the
     * page, logs `(skipped)`, and writes nothing. This repo has exactly one
     * (`code/spike/nuxt-ir/pages/[...surface].vue.html`, a Vue catch-all from the 23.1 spike), and it
     * surfaced as the only four migration regressions `npm run check:links` found — links the golden site
     * resolves and the Nuxt output did not.
     *
     * The page is already rendered by the time this hook runs, so the fix is to write it. The guard's real
     * intent — never escape `publicDir` — is enforced properly here by resolving the path and checking
     * containment, which is what the substring test was standing in for.
     *
     * Registered through `nitro:init` rather than a `hooks` key inside `nitro`: Nuxt does not forward that
     * key to the Nitro instance, so a hook declared there never fires — silently, with the route still
     * logged as `(skipped)` and the file still missing.
     */
    'nitro:init'(nitro) {
      const publicDir = resolvePath(nitro.options.output.publicDir)
      nitro.hooks.hook('prerender:generate', (route) => {
        if (!route.fileName?.includes('..') || route.error || route.contents == null) return
        // `fileName` arrives with a LEADING SLASH (`/code/…/foo.html`). On Windows `path.resolve` reads
        // that as absolute and hands back `C:\code\…`, which then fails the containment check below and
        // writes nothing — silently, exactly like the bug this hook exists to fix.
        const target = resolvePath(publicDir, route.fileName.replace(/^[/\\]+/, ''))
        if (target !== publicDir && !target.startsWith(publicDir + sep)) return
        mkdirSync(dirnameOf(target), { recursive: true })
        writeFileSync(target, route.contents)
      })
    },
  },
})
