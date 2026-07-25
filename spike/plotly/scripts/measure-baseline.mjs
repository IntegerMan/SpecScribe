// Story 20.4 — AC #1 baseline: what does the SHIPPED portal spend on hand-rolled hierarchy-chart SVG today,
// and what does the Story 20.2 payload island cost, across the WHOLE generated site?
//
//   node scripts/measure-baseline.mjs [pathToSpecScribeOutput]
//
// Method (deliberately mirrors spike/nuxt-ir/scripts/measure.mjs, Story 23.1):
//   * Walk every .html under the output root. Never sample — `code-map.html` is 30x the median page, so any
//     sampling scheme silently mis-weights the one surface with the most to gain.
//   * Classify each <svg> by its class attribute. Only the SEVEN hierarchy entry points Story 20.7 deletes count
//     as removable; icons, donuts, funnels, heatmaps, the work-graph and the req-flow are NOT in Epic 20's scope
//     and counting them would inflate the win.
//   * Depth-track </svg> so a nested <svg> can never truncate an extraction.
//   * Measure the <script type="application/json" id="sunburst-explorer-data"> island where it actually exists.
//     Where it does not, report ABSENT — never zero, and never a silent projection.
//
// Everything this script prints is MEASURED. Projections live in the report, labelled, not here.

import { readFileSync, readdirSync, statSync, writeFileSync, mkdirSync, existsSync } from 'node:fs'
import { join, resolve, relative, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { gzipSync } from 'node:zlib'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(process.argv[2] ?? join(here, '..', '..', '..', 'SpecScribeOutput'))
if (!existsSync(root)) {
  console.error(`measure-baseline: output root not found: ${root}`)
  process.exit(1)
}

// The seven server-side hierarchy entry points from the story's rollout inventory, keyed by the class the
// emitted <svg> actually carries (verified against the generated portal, not assumed from source).
//   Charts.Sunburst              -> class="sunburst"          (dashboard, epics index, epic detail, story detail)
//   Charts.EpicSunburst          -> class="sunburst"
//   Charts.TaskSunburst          -> class="sunburst"
//   Charts.CodeTreemap           -> class="codemap"
//   Charts.CodeMapSunburst       -> class="codemap-sunburst"
//   Charts.CodeOwnershipSunburst -> class="ownership-sunburst"
//   Charts.CodeOwnershipTreemap  -> class="ownership-treemap"
const HIERARCHY_CLASSES = new Set([
  'sunburst',
  'codemap',
  'codemap-sunburst',
  'ownership-sunburst',
  'ownership-treemap',
])

// Explicitly OUT of Epic 20's rollout. Listed rather than implied so the exclusion is reviewable.
const NON_HIERARCHY_NOTE = 'ss-icon, icon, donut, heatmap, funnel, work-graph, req-flow-svg, site-nav-mark, specscribe-badge-mark'

const ISLAND_RE = /<script type="application\/json" id="sunburst-explorer-data"[^>]*>([\s\S]*?)<\/script>/

// The DRAWN NODE COUNT per hierarchy chart, which is what a Plotly payload would have to carry. Each entry is the
// element the shipped renderer emits per node, verified against the generated markup:
//   Charts.Sunburst / EpicSunburst / TaskSunburst -> <path class="sb-seg …">
//   Charts.CodeTreemap                            -> <rect class="codemap-file|codemap-dir">
//   Charts.CodeMapSunburst                        -> <path class="codemap-…-sunburst|codemap-dir-sunburst">
//   Charts.CodeOwnership{Sunburst,Treemap}        -> <path class="ow-…"> / <rect class="ow-…">
// Counted INSIDE the extracted SVG only, so page prose mentioning a class cannot inflate it.
// Verified by enumerating the class attributes inside each extracted SVG on the real portal, not guessed:
//   sunburst          -> sb-seg                                  (127 pages)
//   codemap treemap   -> codemap-cell / codemap-dir
//   codemap sunburst  -> codemap-wedge / codemap-dir-sunburst
//   ownership sunburst-> ownership-wedge / ownership-wedge-dir
//   ownership treemap -> ownership-cell / ownership-cell-dir
const NODE_MARKERS = [
  /class="[^"]*\bsb-seg\b/g,
  /class="[^"]*\bcodemap-(?:cell|dir|wedge|dir-sunburst)\b/g,
  /class="[^"]*\bownership-(?:cell|wedge|cell-dir|wedge-dir)\b/g,
]
function countNodes(svgHtml) {
  let n = 0
  for (const re of NODE_MARKERS) n += (svgHtml.match(re) || []).length
  return n
}

function* walk(dir) {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name)
    if (e.isDirectory()) yield* walk(p)
    else yield p
  }
}

// Extract every <svg …>…</svg> with correct nesting. Returns {cls, bytes, start}.
function extractSvgs(html) {
  const out = []
  const openRe = /<svg\b([^>]*)>/g
  let m
  while ((m = openRe.exec(html))) {
    const attrs = m[1]
    // Depth-walk from the end of this open tag to its matching close.
    let depth = 1
    let i = openRe.lastIndex
    const tagRe = /<svg\b[^>]*>|<\/svg>/g
    tagRe.lastIndex = i
    let t
    let end = -1
    while ((t = tagRe.exec(html))) {
      if (t[0] === '</svg>') {
        if (--depth === 0) { end = t.index + '</svg>'.length; break }
      } else depth++
    }
    if (end === -1) { out.push({ cls: '(UNCLOSED)', bytes: 0, broken: true }); continue }
    const clsMatch = attrs.match(/(^|\s)class="([^"]*)"/)
    const body = html.slice(m.index, end)
    out.push({
      cls: clsMatch ? clsMatch[2].trim() : '(no class)',
      bytes: Buffer.byteLength(body),
      nodes: countNodes(body),
    })
    // Skip past this element so nested <svg> are not re-counted as top-level.
    openRe.lastIndex = end
  }
  return out
}

const pages = []
let totalPortalBytes = 0
let totalFiles = 0
const classTotals = new Map()

for (const file of walk(root)) {
  totalFiles++
  const size = statSync(file).size
  totalPortalBytes += size
  if (!file.endsWith('.html')) continue

  const html = readFileSync(file, 'utf8')
  const svgs = extractSvgs(html)
  let hierarchyBytes = 0
  let otherSvgBytes = 0
  let hierarchyNodes = 0
  const hierarchyKinds = []
  for (const s of svgs) {
    const first = s.cls.split(/\s+/)[0]
    classTotals.set(first, (classTotals.get(first) ?? 0) + s.bytes)
    if (HIERARCHY_CLASSES.has(first)) {
      hierarchyBytes += s.bytes
      hierarchyNodes += s.nodes
      hierarchyKinds.push(first)
    } else otherSvgBytes += s.bytes
  }

  const island = html.match(ISLAND_RE)
  const islandBytes = island ? Buffer.byteLength(island[0]) : null

  if (hierarchyBytes > 0 || islandBytes !== null) {
    pages.push({
      page: relative(root, file).replace(/\\/g, '/'),
      pageBytes: size,
      hierarchyBytes,
      hierarchyNodes,
      hierarchyKinds,
      otherSvgBytes,
      islandBytes,
      hierarchyPctOfPage: +(100 * hierarchyBytes / size).toFixed(1),
    })
  }
}

pages.sort((a, b) => b.hierarchyBytes - a.hierarchyBytes)

const B = (n) => (n === null ? 'ABSENT' : n.toLocaleString())
const totalHierarchy = pages.reduce((n, p) => n + p.hierarchyBytes, 0)
const totalIsland = pages.reduce((n, p) => n + (p.islandBytes ?? 0), 0)
const islandPages = pages.filter((p) => p.islandBytes !== null).length
const hierarchyPages = pages.filter((p) => p.hierarchyBytes > 0).length

console.log('='.repeat(112))
console.log(`STORY 20.4 BASELINE — ${root}`)
console.log('='.repeat(112))
console.log(`files on disk            : ${B(totalFiles)}`)
console.log(`total portal bytes       : ${B(totalPortalBytes)}`)
console.log(`pages with hierarchy SVG : ${B(hierarchyPages)}`)
console.log(`pages with 20.2 island   : ${B(islandPages)}`)
console.log(`Σ hierarchy SVG bytes    : ${B(totalHierarchy)}   (${(100 * totalHierarchy / totalPortalBytes).toFixed(2)}% of the portal)`)
console.log(`Σ 20.2 island bytes      : ${B(totalIsland)}`)
console.log(`excluded SVG families    : ${NON_HIERARCHY_NOTE}`)

console.log('\nTop pages by removable hierarchy SVG')
console.log(['page', 'page bytes', 'hier SVG', '% of page', 'island', 'kinds'].map((h, i) => h.padEnd([46, 13, 13, 11, 11, 30][i])).join(''))
for (const p of pages.slice(0, 25)) {
  console.log([
    p.page.padEnd(46),
    B(p.pageBytes).padEnd(13),
    B(p.hierarchyBytes).padEnd(13),
    `${p.hierarchyPctOfPage}%`.padEnd(11),
    B(p.islandBytes).padEnd(11),
    [...new Set(p.hierarchyKinds)].join(',').padEnd(30),
  ].join(''))
}

console.log('\nPer-entry-point totals across the whole portal')
const perKind = new Map()
for (const p of pages) for (const k of p.hierarchyKinds) perKind.set(k, (perKind.get(k) ?? 0) + 1)
for (const k of HIERARCHY_CLASSES) {
  console.log(`  ${k.padEnd(20)} instances: ${String(perKind.get(k) ?? 0).padStart(5)}   Σ bytes: ${B(classTotals.get(k) ?? 0)}`)
}

console.log('\nNon-hierarchy SVG families present (NOT removed by Story 20.7 — reported so the win is not overstated)')
for (const [cls, bytes] of [...classTotals].sort((a, b) => b[1] - a[1])) {
  if (HIERARCHY_CLASSES.has(cls)) continue
  console.log(`  ${cls.padEnd(26)} Σ bytes: ${B(bytes)}`)
}

// gzip view — the wire cost, which is what a hosted portal actually pays.
const gz = (s) => gzipSync(Buffer.from(s), { level: 9 }).length
console.log('\nWire-cost sanity (gzip) on the two extremes')
for (const name of ['code-map.html', 'index.html']) {
  const f = join(root, name)
  if (!existsSync(f)) continue
  const raw = readFileSync(f)
  console.log(`  ${name.padEnd(16)} raw ${B(raw.length).padStart(11)}   gzip ${B(gz(raw)).padStart(10)}`)
}

/* ==========================================================================================================
 * AC #1's headline: Σ(SVG removed) − Σ(payload added) − (one bundle), across the WHOLE portal.
 *
 * MEASURED   : every SVG byte above; the one real island (23,018 B / 118 nodes) emitted by SunburstExplorer.cs;
 *              the custom bundle on disk.
 * PROJECTED  : the payload for the 128 surfaces that have no island yet. Basis = the MEASURED per-node cost of
 *              the real island, times the node count actually DRAWN in that page's chart. Every projected figure
 *              is labelled; none is presented as a measurement (the correction Story 23.1's report had to absorb).
 * ======================================================================================================== */
const bundlePath = join(here, '..', 'plotly-src', 'dist', 'plotly-specscribe-hierarchy.min.js')
const bundleMin = existsSync(bundlePath) ? statSync(bundlePath).size : null
const bundleGz = existsSync(bundlePath) ? gz(readFileSync(bundlePath)) : null
if (bundleMin === null) {
  console.error(`\n⚠️  WARNING: bundle not found at ${bundlePath} — NET figure below silently excludes the one-time bundle cost. Build it first (see README § Reproduce).`)
}

const realIsland = pages.find((p) => p.islandBytes !== null)
const islandNodes = realIsland ? JSON.parse(readFileSync(join(root, realIsland.page), 'utf8').match(ISLAND_RE)[1]).nodes.length : null
const bytesPerNode = realIsland && islandNodes ? realIsland.islandBytes / islandNodes : null
if (!realIsland) {
  console.error(`\n⚠️  WARNING: no page under ${root} carries a sunburst-explorer-data island — every projected payload below is silently priced at 0 B/node. Generate against a repo where Story 20.2's island is emitted.`)
}

console.log('\n' + '='.repeat(112))
console.log('NET OUTPUT-SIZE DELTA (AC #1 headline)')
console.log('='.repeat(112))
console.log(`bundle (custom, standard, min)      : ${B(bundleMin)} B   gzip ${B(bundleGz)} B      [MEASURED]`)
console.log(`real 20.2 island                    : ${B(realIsland?.islandBytes ?? null)} B over ${B(islandNodes)} nodes  = ${bytesPerNode ? bytesPerNode.toFixed(1) : 'n/a'} B/node   [MEASURED]`)

let projectedPayload = 0
let projectedNodes = 0
for (const p of pages) {
  if (p.hierarchyBytes === 0) continue
  if (p.islandBytes !== null) { projectedPayload += p.islandBytes; projectedNodes += islandNodes; continue }
  projectedNodes += p.hierarchyNodes
  projectedPayload += Math.round(p.hierarchyNodes * (bytesPerNode ?? 0))
}
const netRaw = totalHierarchy - projectedPayload - (bundleMin ?? 0)
const breakEvenPages = bundleMin && totalHierarchy > projectedPayload
  ? Math.ceil(bundleMin / ((totalHierarchy - projectedPayload) / hierarchyPages))
  : null

console.log('')
console.log(`Σ hierarchy SVG removed             : ${B(totalHierarchy)} B   over ${B(hierarchyPages)} pages          [MEASURED]`)
console.log(`Σ payload added                     : ${B(projectedPayload)} B   over ${B(projectedNodes)} nodes  [1 MEASURED + ${hierarchyPages - islandPages} PROJECTED]`)
console.log(`one vendored bundle (min, once)     : ${B(bundleMin)} B                                  [MEASURED]`)
console.log(`NET                                 : ${netRaw >= 0 ? '−' : '+'}${B(Math.abs(netRaw))} B  (${netRaw >= 0 ? 'REDUCTION' : 'INCREASE'})`)
console.log(`break-even page count               : ${B(breakEvenPages)} pages carrying a hierarchy chart`)

const cm = pages.find((p) => p.page === 'code-map.html')
if (cm) {
  const cmPayload = Math.round(cm.hierarchyNodes * (bytesPerNode ?? 0))
  console.log('')
  console.log(`code-map.html alone: page ${B(cm.pageBytes)} B, hierarchy SVG ${B(cm.hierarchyBytes)} B (${cm.hierarchyPctOfPage}%),`)
  console.log(`  ${B(cm.hierarchyNodes)} drawn nodes -> projected payload ${B(cmPayload)} B; single-page delta −${B(cm.hierarchyBytes - cmPayload)} B`)
  console.log(`  (historical peak for this page was 82,500,000 B at Story 6.6 scale, 2026-07-20, before`)
  console.log(`   Charts.MaxDetailedCodeMapFiles landed 2026-07-21 — do NOT quote 82.5 MB as a current figure)`)
}

mkdirSync(join(here, '..', 'measurements'), { recursive: true })
writeFileSync(
  join(here, '..', 'measurements', 'baseline.json'),
  JSON.stringify({ root, totalFiles, totalPortalBytes, totalHierarchy, totalIsland, hierarchyPages, islandPages, bundleMin, bundleGz, islandNodes, bytesPerNode, projectedPayload, projectedNodes, netRaw, breakEvenPages, pages, classTotals: Object.fromEntries(classTotals) }, null, 2),
)
console.log('\nwrote measurements/baseline.json')
