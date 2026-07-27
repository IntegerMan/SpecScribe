/**
 * The ONE file in `web/` that knows the shipped IR's field names. [Story 23.3 AC #3]
 *
 * ADR 0008 seated `spa/manifest.json` + `spa/pages-*.json` as SpecScribe's canonical intermediate
 * representation and Story 22.2 promoted that file set in place and stamped it `schemaVersion: 1`. This
 * module reads it and hands the rest of the app the neutral shape below. Nothing downstream of here may
 * mention `outputRelativePath`, `chunk`, `siteTitle`, or any other emitter-side name — so when the schema
 * next moves, exactly one file changes.
 *
 * ── The neutral shape the rest of `web/` is allowed to know about ──────────────────────────────────────
 *
 *   IrSite   { title, entry, nav: IrNavItem[], paths: string[], schemaVersion, assetVersion }
 *   IrNavItem{ label, path }
 *   IrCrumb  { label, path | null }
 *   IrHead   { title, description }
 *   IrPage   { path, title, head, breadcrumb: IrCrumb[], parent, children: string[],
 *              region: IrRegion, hasDataIsland, hasExecutableIsland }
 *   IrRegion { navHtml, wayfindingHtml, mainAttributes, mainInnerHtml, wayfindingRepaired }
 *
 * ── Server-only by construction ────────────────────────────────────────────────────────────────────────
 *
 * This module reads the filesystem at build time (CONVENTIONS.md §4 — variant C, measured at 1.00x against
 * `useAsyncData`'s 1.36x and `<NuxtIsland>`'s 1.99x). It is imported through the `#ir` alias, which
 * `nuxt.config.ts` re-points at `ir/adapter.client.ts` for the CLIENT build so `node:fs` can never reach a
 * browser bundle. Do not import this file by relative path — that bypasses the alias and the guarantee.
 */

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'

// ── Neutral types ────────────────────────────────────────────────────────────────────────────────────────
//
// Declared in `./types`, which compiles to nothing, so the CLIENT stub can re-export the same shape
// without importing this file (and with it `node:fs`).

export type { IrCrumb, IrHead, IrNavItem, IrPage, IrRegion, IrSite } from './types'

import type { IrPage, IrRegion, IrSite } from './types'

// ── Where the IR lives ───────────────────────────────────────────────────────────────────────────────────

/**
 * The generated output root that holds `spa/`. One configurable path, so nobody hand-edits a literal:
 * set `SPECSCRIBE_IR_DIR` to point at another checkout's output. The default is the repo's own
 * `SpecScribeOutput/` — the directory `specscribe generate` writes to by default, and the ONLY one this
 * project generates into (never `--output docs/live`, which is vestigial and gitignored).
 *
 * Regenerate it with:  dotnet run --project src/SpecScribe -- generate --spa
 *
 * ⚠️ Resolved from the WORKING DIRECTORY (`web/`), not from `import.meta.url`. This module is bundled into
 * Nitro's prerender output, where `import.meta.url` points at the emitted chunk several directories deeper
 * — a `new URL('../../SpecScribeOutput', import.meta.url)` default silently became `web/SpecScribeOutput`
 * and every route failed with "IR not found". Every entry point that loads this module (`nuxt.config.ts`,
 * `nuxt build`, and the `scripts/*` harnesses) runs from `web/`.
 */
export const IR_DIR = resolve(process.env.SPECSCRIBE_IR_DIR ?? resolve(process.cwd(), '..', 'SpecScribeOutput'))

/** The schema version this adapter was written against. A mismatch is reported, never silently tolerated. */
export const EXPECTED_SCHEMA_VERSION = 1

// ── Raw manifest shape (emitter names live HERE and nowhere else) ────────────────────────────────────────

interface RawCrumb {
  label: string
  outputRelativePath: string | null
}

interface RawScriptIsland {
  id: string | null
  kind: string
}

interface RawEntry {
  title: string
  chunk: string
  breadcrumb?: RawCrumb[]
  parent?: string | null
  children?: string[]
  head?: { title: string; description: string }
  scriptIslands?: RawScriptIsland[]
  contentHash?: string
  bytes?: number
}

interface RawManifest {
  schemaVersion?: number
  siteTitle: string
  entry: string
  nav?: { label: string; outputRelativePath: string }[]
  oversizedPages?: { path: string; chunkBytes: number }[]
  pages: Record<string, RawEntry>
}

// ── Loading ──────────────────────────────────────────────────────────────────────────────────────────────

function loadManifest(): RawManifest {
  const file = join(IR_DIR, 'spa', 'manifest.json')
  let text: string
  try {
    text = readFileSync(file, 'utf8')
  } catch (err) {
    const e = err as NodeJS.ErrnoException
    if (e.code === 'ENOENT') {
      throw new Error(
        `IR not found at ${file}.\n` +
          `Generate it first:  dotnet run --project src/SpecScribe -- generate --spa\n` +
          `Or point SPECSCRIBE_IR_DIR at an output root that already has one.`,
      )
    }
    throw err
  }
  return JSON.parse(text) as RawManifest
}

const manifest = loadManifest()

if (manifest.schemaVersion !== EXPECTED_SCHEMA_VERSION) {
  // Loud, not fatal: an ADDITIVE bump is legal under the emitter's own compatibility rule (a monotonic
  // integer, bumped only on a breaking change), so refusing to build would be wrong. A silent mismatch
  // would not be.
  console.warn(
    `[ir/adapter] IR schemaVersion is ${manifest.schemaVersion ?? '(absent — pre-22.2)'}, this adapter was ` +
      `written against ${EXPECTED_SCHEMA_VERSION}. Re-read SpaDelivery.SchemaVersion's compatibility rule ` +
      `before trusting the fields below.`,
  )
}

/** Chunk file (IR-relative) -> its `{ path: contentHtml }` map. Lazy: the whole IR is ~90 MB on this repo. */
const chunkCache = new Map<string, Record<string, string>>()

function chunkContents(chunkFile: string): Record<string, string> {
  let hit = chunkCache.get(chunkFile)
  if (!hit) {
    hit = JSON.parse(readFileSync(join(IR_DIR, chunkFile), 'utf8')) as Record<string, string>
    chunkCache.set(chunkFile, hit)
  }
  return hit
}

// ── Region split (Task 2) ────────────────────────────────────────────────────────────────────────────────

const MAIN_MARKER = '<main id="main-content"'
const MAIN_CLOSER = '</main>'
const CRUMB_MARKER = '<div class="breadcrumb"'

const WAYFINDING_MARKER = '<div class="page-wayfinding"'

/**
 * The wrapper the static renderer opens around the breadcrumb + sibling pager.
 *
 * ⚠️ THE IR CARRIES TWO DIFFERENT REGION SHAPES, and treating them as one produces broken markup that no
 * `<main>` comparison can see. Measured across all 1,042 pages:
 *
 *   · 187 pages (the dashboard/epics FAMILIES) are re-rendered from their view models for the IR, so the
 *     region carries the whole wayfinding band, wrapper and all. Balanced.
 *   · 853 CAPTURED pages go through `SpaDelivery.ExtractContentRegion`, which starts its slice at
 *     `<div class="breadcrumb"` — INSIDE the wrapper. Those regions carry the wrapper's closing `</div>`
 *     without its opener and are unbalanced by one element.
 *
 * So the split point is the wrapper when the region has one, and the breadcrumb otherwise; the repair below
 * fires only for the second shape. Getting this wrong is not a cosmetic error: prepending a second opener
 * to an already-balanced region nested `<main>` and `<footer>` INSIDE the wayfinding band on all 187
 * migrated pages — with the `<main>` region still byte-identical, so parity, links and a11y all passed. It
 * was caught by looking at real DOM geometry in a browser, which is the whole reason CLAUDE.md requires it.
 *
 * `npm run check:a11y` now asserts the structure over the emitted HTML so it cannot come back quietly.
 *
 * The unbalanced captured shape stays a named gap for Epic 22: the emitter should slice from the wrapper.
 */
const WAYFINDING_OPEN = '<div class="page-wayfinding">\n'

/**
 * Inverts `SpaDelivery.ExtractContentRegion` using the SAME markers it concatenated with.
 *
 * Fails loudly on a page that does not match. Emitting half a page would look like a rendering bug three
 * layers away from its cause, and the region is the one thing every downstream check depends on.
 */
export function splitContentRegion(contentHtml: string, path: string): IrRegion {
  const mainOpen = contentHtml.indexOf(MAIN_MARKER)
  if (mainOpen < 0) {
    // The emitter degrades a landmark-less page to nav-only rather than aborting the SPA emit, so this is a
    // real (if rare) shape — but it is not something this app can render as a page.
    throw new Error(
      `IR page "${path}" carries no <main id="main-content"> landmark; its content region is nav-only. ` +
        `Fix the page's templater (every SpecScribe page has carried the Story 1.4 landmark since 1.4).`,
    )
  }
  const openTagEnd = contentHtml.indexOf('>', mainOpen)
  const mainClose = contentHtml.indexOf(MAIN_CLOSER, mainOpen)
  if (openTagEnd < 0 || mainClose < 0) {
    throw new Error(`IR page "${path}" has an unterminated <main> element.`)
  }

  // Prefer the wrapper as the split point; fall back to the breadcrumb for the captured shape that has no
  // wrapper opener. Taking the EARLIEST of the two that precedes <main> means neither shape is assumed.
  const wrapOpen = contentHtml.indexOf(WAYFINDING_MARKER)
  const crumbOpen = contentHtml.indexOf(CRUMB_MARKER)
  const candidates = [wrapOpen, crumbOpen].filter((i) => i >= 0 && i < mainOpen)
  const hasWayfinding = candidates.length > 0
  const bodyStart = hasWayfinding ? Math.min(...candidates) : mainOpen

  const navHtml = contentHtml.slice(0, bodyStart)
  let wayfindingHtml = hasWayfinding ? contentHtml.slice(bodyStart, mainOpen) : ''

  // Repair only when the slice is genuinely unbalanced, so the two shapes are handled by the same code and
  // a future emitter fix makes this stop firing on its own rather than double-wrapping.
  const opens = (wayfindingHtml.match(/<div\b/g) ?? []).length
  const closes = (wayfindingHtml.match(/<\/div>/g) ?? []).length
  const wayfindingRepaired = wayfindingHtml.length > 0 && closes === opens + 1
  if (wayfindingRepaired) {
    wayfindingHtml = WAYFINDING_OPEN + wayfindingHtml
  }
  const stillUnbalanced =
    wayfindingHtml.length > 0 &&
    (wayfindingHtml.match(/<div\b/g) ?? []).length !== (wayfindingHtml.match(/<\/div>/g) ?? []).length
  if (stillUnbalanced) {
    throw new Error(
      `IR page "${path}" has a wayfinding band this adapter cannot balance. Injecting it would nest ` +
        `<main> inside it — a DOM defect no <main> comparison can see. Region head: ` +
        `${JSON.stringify(wayfindingHtml.slice(0, 200))}`,
    )
  }

  const mainAttributes = contentHtml.slice(mainOpen + MAIN_MARKER.length, openTagEnd)
  return {
    navHtml,
    wayfindingHtml,
    mainAttributes,
    mainAttrs: parseAttributes(mainAttributes, path),
    mainInnerHtml: contentHtml.slice(openTagEnd + 1, mainClose),
    wayfindingRepaired,
  }
}

/**
 * `name="value"` pairs out of an open tag's attribute run.
 *
 * Every `<main>` this repo emits carries at most a single `class`, but parsing generically (and failing on
 * anything it cannot account for) means a templater that starts adding an attribute shows up as a build
 * error rather than as a silently dropped attribute and a parity delta nobody can source.
 */
function parseAttributes(raw: string, path: string): Record<string, string> {
  const attrs: Record<string, string> = {}
  const pattern = /\s+([A-Za-z_:][-A-Za-z0-9_:.]*)(?:="([^"]*)")?/g
  let consumed = 0
  for (const m of raw.matchAll(pattern)) {
    attrs[m[1]!] = m[2] ?? ''
    consumed += m[0].length
  }
  if (consumed !== raw.length) {
    throw new Error(
      `IR page "${path}" has <main> attributes this adapter cannot parse: ${JSON.stringify(raw)}. ` +
        `Reproducing the open tag exactly is what makes the <main> region compare byte-for-byte.`,
    )
  }
  return attrs
}

// ── Public reads ─────────────────────────────────────────────────────────────────────────────────────────

const paths = Object.keys(manifest.pages)

/**
 * Three CHROME values the IR does not carry, COPIED off the generated entry page rather than re-typed here.
 *
 * - the `?v=` cache-bust: the emitting assembly's module version id, which nothing on this side can compute
 *   (the IR deliberately omits it — a per-page copy would churn every page's bytes on every build);
 * - the favicon `data:` URI: a 1 KB constant that would be a second definition free to drift if re-typed;
 * - the Hierarchy Explorer's anti-flash boot script, which lives outside the captured content region.
 *
 * All three degrade to empty (and an omitted tag) when the static site was not generated alongside the IR.
 * They are ONE named gap handed to Epic 22: the IR projects a page's head TITLE and DESCRIPTION, but not
 * the surrounding chrome — asset links, favicon, boot marker, footer. Until it does, this adapter reads the
 * shipped values instead of inventing them, and the story record lists what it had to reach for.
 */
function readGoldenChrome(): { assetVersion: string; faviconDataUri: string; hierarchyBootScript: string } {
  try {
    const entryHtml = readFileSync(join(IR_DIR, manifest.entry), 'utf8')
    const head = entryHtml.slice(0, entryHtml.indexOf('</head>'))
    const boot = /<script>((?:(?!<\/script>)[\s\S])*data-ss-hierarchy-boot[\s\S]*?)<\/script>/.exec(entryHtml)
    return {
      assetVersion: /specscribe\.css\?v=([0-9a-fA-F]+)/.exec(head)?.[1] ?? '',
      faviconDataUri: /<link rel="icon" href="([^"]*)">/.exec(head)?.[1] ?? '',
      hierarchyBootScript: boot?.[1] ?? '',
    }
  } catch {
    return { assetVersion: '', faviconDataUri: '', hierarchyBootScript: '' }
  }
}

const goldenHead = readGoldenChrome()

export const site: IrSite = {
  title: manifest.siteTitle,
  entry: manifest.entry,
  nav: (manifest.nav ?? []).map((n) => ({ label: n.label, path: n.outputRelativePath })),
  paths,
  schemaVersion: manifest.schemaVersion ?? 0,
  assetVersion: goldenHead.assetVersion,
  faviconDataUri: goldenHead.faviconDataUri,
  hierarchyBootScript: goldenHead.hierarchyBootScript,
}

/** True when the manifest knows this page path. */
export function hasPage(path: string): boolean {
  return Object.hasOwn(manifest.pages, path)
}

const pageCache = new Map<string, IrPage>()

/** One page, fully resolved. Throws on an unknown path — the route table comes from `site.paths`. */
export function page(path: string): IrPage {
  const cached = pageCache.get(path)
  if (cached) return cached

  const entry = manifest.pages[path]
  if (!entry) {
    throw new Error(`IR has no page "${path}". The route table is built from the manifest; this is a bug.`)
  }
  const contentHtml = chunkContents(entry.chunk)[path]
  if (contentHtml === undefined) {
    throw new Error(`IR chunk "${entry.chunk}" does not contain page "${path}" the manifest assigned to it.`)
  }

  const islands = entry.scriptIslands ?? []
  const region = splitContentRegion(contentHtml, path)
  const resolved: IrPage = {
    path,
    title: entry.title,
    head: {
      title: entry.head?.title ?? entry.title,
      description: entry.head?.description ?? entry.title,
    },
    breadcrumb: (entry.breadcrumb ?? []).map((c) => ({ label: c.label, path: c.outputRelativePath ?? null })),
    parent: entry.parent ?? null,
    children: entry.children ?? [],
    region,
    hasDataIsland: islands.some((i) => i.kind === 'data'),
    hasExecutableIsland: islands.some((i) => i.kind === 'executable'),
    // Matched as a real ATTRIBUTE inside a tag, not as a substring: four `code/**` pages render this very
    // file's source, where the same text appears entity-escaped as prose. A substring test loaded 1.22 MB
    // of charting engine onto each of them for a chart that does not exist.
    needsHierarchyEngine: /<[^>]*\sdata-hierarchy(?=[\s>=])/.test(region.mainInnerHtml),
    needsPrism: path.startsWith('code/'),
  }
  pageCache.set(path, resolved)
  return resolved
}

/**
 * The `../`-prefix a page at `path` needs to reach the output root — the same rule `PathUtil.RelativePrefix`
 * applies on the C# side, and the reason routes mirror IR paths verbatim: an href written against this
 * prefix resolves unchanged under Nuxt.
 */
export function relativePrefix(path: string): string {
  const depth = path.split('/').length - 1
  return '../'.repeat(depth)
}
