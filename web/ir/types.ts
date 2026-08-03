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
  /**
   * Everything AFTER `</main>` in the region — normally empty. [Story 23.4]
   *
   * ⚠️ **This field exists because dropping it shipped a broken feature, silently, on the one page that uses
   * it.** `deep-analytics.html` emits its `:target` lightbox (`<div id="coupling-zoom" class="coupling-lightbox">`)
   * AFTER the landmark, because a `:target` overlay must not be inside the scrolling region it overlays. The
   * pre-23.4 C# slicer truncated at `</main>` and dropped it, so the page's "Expand" link resolved to nothing
   * in the SPA and the webview. Story 23.4's composed region restores it — and then this splitter dropped it
   * again one layer further on, for the same reason.
   *
   * Neither loss was visible to any harness: `measure:parity` compares `<main>` regions only, the link checker
   * sees a same-page `#` fragment as resolved, and a11y has nothing to say about a missing overlay. It was
   * found by opening the page in a browser and querying for `#coupling-zoom` — which is why CLAUDE.md makes
   * live verification a gate rather than a courtesy.
   */
  trailingHtml: string
  /**
   * The emitter degraded this page to nav-only: it carries no `<main id="main-content">` landmark, so there is
   * no page body to render. ADR 0024 §Decision 3 keeps such pages in the IR deliberately (the SPA retains what
   * the webview skips), which means a consumer MUST be able to skip one — before Story 22.4's code review
   * `splitContentRegion` threw instead, and because Nuxt prerenders from the manifest, a single landmark-less
   * page failed the WHOLE site build rather than degrading itself.
   *
   * This is NOT the deleted `wayfindingRepaired` flag: that recorded a consumer-side REPAIR of an emitter bug
   * (rightly deleted with the bug). This records an emitter STATE the ADR ratifies.
   */
  degraded: boolean
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
   * The page carries a relationship-graph mount point (Story 24.2's `data-relgraph`), so it needs the SAME
   * vendored plotly bundle the Hierarchy Explorer uses (ADR 0030 — `scatter` was already registered in it) plus
   * its own anti-flash boot marker, which is a distinct marker family so the two components' boot state cannot
   * be confused. [Story 23.6]
   *
   * ⚠️ Before this existed, a graph-only page rendered by Nuxt shipped NEITHER the engine nor the marker: the
   * C# writer emitted both from `page.Assets.GraphEngineNeeded`/`GraphBootInline`, and nothing on this side
   * reproduced them. Verified on the generated portal before the fix — a `data-relgraph` page carried zero
   * occurrences of `data-ss-relgraph-boot` and zero of `plotly-hierarchy.min.js`.
   */
  needsGraphEngine: boolean
  /**
   * The page carries at least one rendered mermaid diagram, so it needs the client-side init module.
   *
   * ⚠️ Nuxt had NO mermaid support at all before this. `HtmlRenderAdapter.Render` was the only emitter of
   * `Mermaid.InitScript()`, so every diagram on the Nuxt-rendered portal was already shipping as an inert
   * `<pre class="mermaid">` block — a live regression from the moment the prerender took over page writing,
   * found while auditing what Story 23.6's deletion would remove. [Story 23.6]
   */
  needsMermaid: boolean
  /**
   * The page carries a TOC sidebar, so it needs the active-section tracking script. Chrome-level in the golden
   * page for the same reason the boot markers are: the webview and SPA consume the body directly and must carry
   * no script, so it degrades there to the static TOC (NFR8). [Story 23.6]
   */
  needsToc: boolean
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
  /**
   * The site-level chrome below is READ FROM THE MANIFEST (`SpaDelivery.ManifestChrome`) since Story 23.6.
   *
   * ⚠️ It was previously SCRAPED out of the generated `index.html`, which made the renderer depend on the C#
   * HTML writer that Story 23.6 deletes — silently, since the scrape swallowed its own failure. Each value is
   * empty only when reading a manifest emitted before the field existed, and an empty value omits its tag.
   */

  /** The `?v=` cache-bust token both shared assets carry. */
  assetVersion: string
  /** The favicon `data:` URI — a 1 KB constant that would be free to drift if re-typed on this side. */
  faviconDataUri: string
  /**
   * The Hierarchy Explorer anti-flash boot marker's inline script body. [AC #7]
   *
   * It sets `data-ss-hierarchy-boot` on `<html>` and clears it on a timeout, which is what lets
   * `specscribe.css` hide the server-rendered fallback SVG only while a mount is actually in flight. It is
   * emitted at CHROME level, deliberately outside the content region the IR captures, so it is missing
   * under Nuxt unless we re-emit it — and its absence degrades silently to a visible flash-then-swap.
   */
  hierarchyBootScript: string
  /** The relationship graph's own anti-flash boot marker (`data-ss-relgraph-boot`) — same seam, same reason,
   * its own marker family so the two components' boot state cannot be confused. [Story 23.6/24.2] */
  graphBootScript: string
  /** The mermaid init module's body. Injected as `type="module"`: it is an ES-module import of mermaid from
   * the CDN, so it does not run as a classic script. [Story 23.6] */
  mermaidInitScript: string
  /** The TOC active-section tracker's body — an IntersectionObserver over `.toc-sidebar` links. [Story 23.6] */
  tocActiveSectionScript: string
}

