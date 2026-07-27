/**
 * The neutral IR shape the rest of `web/` speaks. [Story 23.3 AC #3]
 *
 * Types only — this module compiles to nothing, which is the point: BOTH `adapter.ts` (server, reads the
 * filesystem) and `adapter.client.ts` (browser stub) need these declarations, and the stub must not reach
 * for the real adapter to get them. It did, briefly, and a `export type { … } from './adapter'` was enough
 * to drag `node:fs` into the client graph and fail the build — the exact leak the split exists to prevent.
 *
 * Emitter-side field names (`outputRelativePath`, `chunk`, `siteTitle`) stay in `adapter.ts`. Nothing in
 * here is allowed to know them.
 */
export interface IrNavItem {
  label: string
  path: string
}

export interface IrCrumb {
  label: string
  /** Null for the current, unlinked crumb. */
  path: string | null
}

/**
 * The per-page head projection Story 22.2 added to the IR (its AC #5). Two fields, because the emitter
 * already resolved the derivation rule `PathUtil.RenderHeadOpen` applies — og:title mirrors the title,
 * og:description mirrors the description, og:type is constant, and the favicon is a constant data URI.
 */
export interface IrHead {
  title: string
  description: string
}

/**
 * One page's content region, split back into the three parts the emitter concatenated.
 *
 * ⚠️ This split is the story's trap #1. `SpaDelivery.ExtractContentRegion` returns
 * `navMarkup + [wayfinding] + <main id="main-content">…</main>` — the `<main>` ELEMENT, not just its body.
 * `PageShell` emits its own `<main id="main-content">` and skip link, so injecting the region whole
 * produces a nested `<main>` and a duplicate `id`: an a11y defect and a parity failure at once.
 */
export interface IrRegion {
  /** The site nav, verbatim. Goes in `PageShell`'s `#nav` slot. */
  navHtml: string
  /** Breadcrumb + sibling pager, verbatim. Empty on pages that carry none (the dashboard). */
  wayfindingHtml: string
  /** The `<main>` open tag's attributes AFTER `id="main-content"` — e.g. ` class="dashboard"`, or ''. */
  mainAttributes: string
  /** The same attributes parsed, for `v-bind` — so `PageShell` reproduces the golden open tag exactly. */
  mainAttrs: Record<string, string>
  /** Everything BETWEEN `<main …>` and `</main>`. This is what gets injected. */
  mainInnerHtml: string
  /** True when the `page-wayfinding` wrapper was re-opened to repair the IR's unbalanced slice — see below. */
  wayfindingRepaired: boolean
}

export interface IrPage {
  path: string
  title: string
  head: IrHead
  breadcrumb: IrCrumb[]
  parent: string | null
  children: string[]
  region: IrRegion
  /** The page carries at least one inert `<script type="application/json">` data island. */
  hasDataIsland: boolean
  /**
   * The page carries a script the BROWSER WOULD RUN. Always false through `v-html`, which never executes
   * injected scripts — recorded so a future schema change that starts shipping executable islands is loud
   * rather than silently inert. [Story 23.3 Task 7]
   */
  hasExecutableIsland: boolean
  /**
   * The page carries a Hierarchy Explorer mount point, so it needs the charting engine and the anti-flash
   * boot marker. Detected off the `[data-hierarchy]` attribute `initHierarchyExplorers` itself selects on —
   * the same selector, so the two can never disagree about which pages have a chart. [AC #7]
   */
  needsHierarchyEngine: boolean
  /**
   * The page needs the Prism pair that `CodeFileTemplater` adds through `RenderHeadOpen`'s `extraHead`.
   *
   * ⚠️ DERIVED, not read: the IR carries a page's title and description but no structured `extraHead`
   * projection, so there is nothing authoritative to consume. Deriving it from the markup was tried and is
   * worse — `class="language-…"` appears in prose code fences on ~20 pages the C# side does NOT highlight,
   * and misses 16 code pages that carry no such class. The path family is what the templater itself keys
   * on, so it reproduces the shipped behaviour exactly. Recorded as a named gap for Epic 22.
   */
  needsPrism: boolean
}

export interface IrSite {
  title: string
  /** The site root's page path, e.g. `index.html`. */
  entry: string
  nav: IrNavItem[]
  /** Every page path in the manifest, in emitter order. This IS the prerender route table. [AC #4] */
  paths: string[]
  schemaVersion: number
  /** The `?v=` cache-bust token the golden head carries on both shared assets. */
  assetVersion: string
  /** The favicon `data:` URI, COPIED off the generated site rather than re-typed here. */
  faviconDataUri: string
  /**
   * The anti-flash boot marker's inline script body, COPIED off the generated site. [AC #7]
   *
   * It sets `data-ss-hierarchy-boot` on `<html>` and clears it on a timeout, which is what lets
   * `specscribe.css` hide the server-rendered fallback SVG only while a mount is actually in flight. It is
   * emitted at CHROME level, deliberately outside the content region the IR captures, so it is missing
   * under Nuxt unless we re-emit it — and its absence degrades silently to a visible flash-then-swap.
   * Empty when the static site was not generated alongside the IR.
   */
  hierarchyBootScript: string
}

