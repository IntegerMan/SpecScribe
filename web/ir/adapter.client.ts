/**
 * The CLIENT-build stand-in for `#ir`. [Story 23.3 AC #3, AC #8]
 *
 * `ir/adapter.ts` reads the IR off disk at build time — the shape CONVENTIONS.md §4 measured at 1.00x
 * hydration payload, against 1.36x for `useAsyncData` and 1.99x for `<NuxtIsland>`. Reading the filesystem
 * is a server-only capability, so `nuxt.config.ts` re-points the `#ir` alias at THIS file for the client
 * build. The effect is structural rather than conventional: `node:fs` and the IR's ~90 MB of content cannot
 * reach a browser bundle even by accident, because the module that touches them is not in it.
 *
 * Nothing here ever executes. Every IR-backed route carries `noScripts: true` (see `nuxt.config.ts`), so
 * those pages ship no Nuxt runtime at all and never hydrate — the prerendered HTML plus the portal's own
 * `specscribe.js` IS the delivered page. This file exists so the client build RESOLVES, and it throws
 * rather than returning plausible-looking empties, so a future route that quietly starts hydrating IR
 * content fails loudly instead of blanking the page.
 */

// From `./types`, NOT from `./adapter`. A type-only re-export of the real adapter is enough to put it in
// the client module graph — Rollup then follows its `node:fs` / `node:path` / `node:url` imports and the
// browser build fails outright ("resolve is not exported by __vite-browser-external"). The shared
// types module compiles to nothing, so it can be imported from both sides safely.
import type { IrPage, IrSite } from './types'

export type { IrCrumb, IrHead, IrNavItem, IrPage, IrRegion, IrSite } from './types'

const WHY =
  'The IR is resolved at build time and IR-backed routes are prerendered with `noScripts: true`, so this ' +
  'code path must never run in a browser. If you are seeing this, a route started hydrating IR content — ' +
  'fix the route rule rather than making this stub return data (that would put the IR in the client bundle).'

function unavailable(what: string): never {
  throw new Error(`[ir/adapter.client] ${what} is not available on the client. ${WHY}`)
}

export const IR_DIR = ''
/** Twin of `adapter.ts`'s constant — must move in the SAME change. Bumped to 2 by Story 22.4. */
export const EXPECTED_SCHEMA_VERSION = 2

export const site: IrSite = new Proxy({} as IrSite, {
  get: (_t, prop) => unavailable(`site.${String(prop)}`),
})

export function hasPage(_path: string): boolean {
  return unavailable('hasPage()')
}

export function page(_path: string): IrPage {
  return unavailable('page()')
}

export function splitContentRegion(_contentHtml: string, _path: string): never {
  return unavailable('splitContentRegion()')
}

/** Pure string arithmetic with no IR dependency — safe to keep real, so a client-side caller still works. */
export function relativePrefix(path: string): string {
  return '../'.repeat(path.split('/').length - 1)
}
