/**
 * The ONE file in `web/` that knows the shipped IR's field names. [Story 23.3 AC #3]
 *
 * ADR 0008 seated `spa/manifest.json` + `spa/pages-*.json` as SpecScribe's canonical intermediate
 * representation and Story 22.2 promoted that file set in place and stamped it `schemaVersion` (1 then; 2
 * since Story 22.4 moved the content region's start marker — see `EXPECTED_SCHEMA_VERSION`). This
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
 *   IrRegion { navHtml, wayfindingHtml, mainAttributes, mainAttrs, mainInnerHtml, degraded }
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

/**
 * The schema version this adapter was written against. A mismatch is reported, never silently tolerated.
 *
 * ⚠️ Bumped to 2 by Story 22.4, which moved the content region's start marker to the wayfinding band's
 * OUTERMOST tag. Keep this in lockstep with `SpaDelivery.SchemaVersion` **and** with the twin constant in
 * `adapter.client.ts` — the check below only `console.warn`s, so a missed consumer is silent.
 */
export const EXPECTED_SCHEMA_VERSION = 2

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

/**
 * Package-build mode: build the RENDERER, bind it to no project. [Story 23.5 AC #4/#5]
 *
 * `nuxt.config.ts` imports this module at CONFIG-LOAD time to compute `nitro.prerender.routes`, which is
 * the one genuine build-time coupling between the artefact and a specific project — the render path itself
 * reads `IR_DIR` at module scope and is runtime-resolvable (verified in the built bundle, not inferred).
 * Without this flag the coupling has two costs that Story 23.5 measured:
 *
 *   · the artefact CANNOT BE BUILT AT ALL without an IR present, so a release pipeline would have to
 *     generate somebody's portal first just to produce a project-independent renderer; and
 *   · `nuxt build` prerenders the declared routes, so `.output/public` ships 1,060 of project A's baked
 *     HTML pages (68.0 MB) — and Nitro serves those static files AHEAD of the SSR route, so a prebuilt
 *     artefact pointed at project B silently returned project A's dashboard for `/index.html`. That is a
 *     WRONG ANSWER WITH A 200, which is the failure mode worth engineering against.
 *
 * With `SPECSCRIBE_PACKAGE_BUILD=1` the manifest is stubbed empty, the prerender route table collapses to
 * nothing, and the build emits the renderer plus its static assets and no project's pages. Nothing else in
 * this module changes: at SERVER RUNTIME the flag is absent, so the real IR loads from `SPECSCRIBE_IR_DIR`
 * exactly as it always has.
 */
export const PACKAGE_BUILD = process.env.SPECSCRIBE_PACKAGE_BUILD === '1'

/** The empty manifest a package build renders against. Schema-current so the version check stays quiet. */
const EMPTY_MANIFEST: RawManifest = {
  schemaVersion: EXPECTED_SCHEMA_VERSION,
  siteTitle: '',
  entry: '',
  nav: [],
  pages: {},
}

function loadManifest(): RawManifest {
  if (PACKAGE_BUILD) return EMPTY_MANIFEST
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
          `Or point SPECSCRIBE_IR_DIR at an output root that already has one.\n` +
          `To build the project-independent RENDERER instead, set SPECSCRIBE_PACKAGE_BUILD=1.`,
      )
    }
    throw err
  }
  return JSON.parse(text) as RawManifest
}

const manifest = loadManifest()

const actualSchemaVersion = manifest.schemaVersion ?? 0

if (actualSchemaVersion < EXPECTED_SCHEMA_VERSION) {
  // FATAL, not a warning. `SpaDelivery.SchemaVersion`'s own compatibility rule defines the integer as bumped
  // ONLY on a breaking change, so an IR strictly below what this adapter expects is by definition unreadable
  // — the fields below may be absent, renamed or differently shaped.
  //
  // This used to be the same `console.warn` the newer-version branch still uses, and that was a real hazard
  // rather than a pedantic one: Story 22.4 also deleted the consumer-side wayfinding repair on the grounds
  // that "the emitter no longer emits that shape". True of the CURRENT emitter — but a v1 IR on disk (a stale
  // generated portal, an older checked-out binary, a CI cache) still carries it, and with the repair AND the
  // balance throw both gone, warn-and-continue produced an unmatched `</div>` that re-parented `<main>` and
  // `<footer>` into the wayfinding band with no error anywhere. Failing here is what closes that path.
  // [Story 22.4 code review]
  throw new Error(
    `[ir/adapter] IR schemaVersion is ${manifest.schemaVersion ?? '(absent — pre-22.2)'}, but this adapter ` +
      `requires ${EXPECTED_SCHEMA_VERSION}. A LOWER version is a breaking mismatch under ` +
      `SpaDelivery.SchemaVersion's compatibility rule, not a tolerable one — the IR on disk predates fields ` +
      `this adapter reads. Regenerate it:  dotnet run --project src/SpecScribe -- generate --spa`,
  )
}

if (actualSchemaVersion > EXPECTED_SCHEMA_VERSION) {
  // Loud, not fatal: an ADDITIVE bump is legal under the emitter's own compatibility rule, so refusing to
  // build would be wrong. A silent mismatch would not be.
  console.warn(
    `[ir/adapter] IR schemaVersion is ${manifest.schemaVersion}, this adapter was written against ` +
      `${EXPECTED_SCHEMA_VERSION}. Re-read SpaDelivery.SchemaVersion's compatibility rule before trusting ` +
      `the fields below.`,
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

/**
 * The wrapper the static renderer opens around the breadcrumb + sibling pager.
 *
 * ⚠️ THE IR USED TO CARRY TWO DIFFERENT REGION SHAPES, and treating them as one produced broken markup that
 * no `<main>` comparison could see. Story 23.3 measured it: 187 re-rendered family pages carried the whole
 * band (balanced), while every CAPTURED page whose pager rendered non-empty was sliced by
 * `SpaDelivery.ExtractContentRegion` from `<div class="breadcrumb"` — INSIDE the wrapper — and so carried the
 * wrapper's closing `</div>` without its opener. On the real repo that was 594 of 1,400 pages. This adapter
 * detected that shape and prepended the missing opener, and threw on anything it still could not balance.
 *
 * **Story 22.4 fixed it at the emitter**: `ExtractContentRegion` now prefers the wrapper as its slice start,
 * so every emitted region is element-balanced and there is ONE shape. The repair and the throw are deleted —
 * a repair that can no longer fire is worse than no repair, because it is a second, drifting truth about a
 * boundary the emitter already owns.
 *
 * Getting this wrong is not a cosmetic error: prepending a second opener to an already-balanced region nested
 * `<main>` and `<footer>` INSIDE the wayfinding band on all 187 migrated pages — with the `<main>` region
 * still byte-identical, so parity, links and a11y all passed. It was caught by looking at real DOM geometry
 * in a browser, which is the whole reason CLAUDE.md requires it.
 *
 * The invariant now lives in two places that CANNOT drift from the emitter: `npm run check:a11y` asserts
 * `one-main` / `wayfinding-single` / `wayfinding-closed` over the emitted HTML, and
 * `SiteGeneratorSpaTests.EveryIrRegion_HasOneBalancedWayfindingBand_AndExactlyOneMainLandmark` asserts
 * balance over the WHOLE emitted IR on the C# side.
 */
const WAYFINDING_MARKER = '<div class="page-wayfinding"'

/**
 * Inverts `SpaDelivery.ExtractContentRegion` using the SAME markers it concatenated with.
 *
 * Fails loudly on a page that does not match. Emitting half a page would look like a rendering bug three
 * layers away from its cause, and the region is the one thing every downstream check depends on.
 */
export function splitContentRegion(contentHtml: string, path: string): IrRegion {
  const mainOpen = contentHtml.indexOf(MAIN_MARKER)
  if (mainOpen < 0) {
    // DEGRADE, don't throw. The emitter reduces a landmark-less page to nav-only rather than aborting the SPA
    // emit, and ADR 0024 §Decision 3 ratifies that the SPA KEEPS it (only the webview skips). Throwing here
    // made the two halves of that decision contradict each other: because Nuxt prerenders every route from the
    // manifest, one such page failed the ENTIRE site build. The consumer now mirrors the webview's own
    // `Degraded → continue` — a per-page degrade, which is what §Decision 3 always implied.
    // [Story 22.4 code review — owner decision DR2]
    return {
      navHtml: contentHtml,
      wayfindingHtml: '',
      mainAttributes: '',
      mainAttrs: {},
      mainInnerHtml: '',
      trailingHtml: '',
      degraded: true,
    }
  }
  const openTagEnd = contentHtml.indexOf('>', mainOpen)
  const mainClose = contentHtml.indexOf(MAIN_CLOSER, mainOpen)
  if (openTagEnd < 0 || mainClose < 0) {
    throw new Error(`IR page "${path}" has an unterminated <main> element.`)
  }

  // The emitter slices from the band's OUTERMOST marker (Story 22.4), so the wrapper is present whenever the
  // page has one and the breadcrumb is the whole band otherwise. Taking the EARLIEST of the two that precedes
  // <main> keeps this an inversion of the emitter's own rule rather than an assumption about which shape won.
  const wrapOpen = contentHtml.indexOf(WAYFINDING_MARKER)
  const crumbOpen = contentHtml.indexOf(CRUMB_MARKER)
  const candidates = [wrapOpen, crumbOpen].filter((i) => i >= 0 && i < mainOpen)
  const hasWayfinding = candidates.length > 0
  const bodyStart = hasWayfinding ? Math.min(...candidates) : mainOpen

  const navHtml = contentHtml.slice(0, bodyStart)
  const wayfindingHtml = hasWayfinding ? contentHtml.slice(bodyStart, mainOpen) : ''

  const mainAttributes = contentHtml.slice(mainOpen + MAIN_MARKER.length, openTagEnd)
  return {
    navHtml,
    wayfindingHtml,
    mainAttributes,
    mainAttrs: parseAttributes(mainAttributes, path),
    mainInnerHtml: contentHtml.slice(openTagEnd + 1, mainClose),
    // Everything after `</main>` — see IrRegion.trailingHtml. Normally '', but on deep-analytics.html it is
    // the `:target` lightbox, and dropping it here silently broke that page's "Expand" link.
    trailingHtml: contentHtml.slice(mainClose + MAIN_CLOSER.length),
    degraded: false,
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
