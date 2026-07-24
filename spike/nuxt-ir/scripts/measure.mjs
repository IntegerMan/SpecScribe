// Produces the Story 23.1 spike report's PER-SURFACE numbers from the actual artifacts. Re-runnable:
// `npm run measure`.
//
//   node scripts/measure.mjs [pathToSpecScribeOutput]
//
// Axes:
//   1. NFR6      — is the prerendered HTML fully rendered, and does it survive with every <script> removed?
//   2. Parity    — does the IR's content survive v-html byte-for-byte, and does <main> match the golden page?
//   3. CSP       — inventory of every <script> Nuxt emits (inline vs external, nonced vs not).
//   4. Node cost — emitted asset weight and hydration payload weight, for the 4 measured surfaces.
//
// SCOPE (code review 2026-07-23): this harness produces Axis 1, Axis 2 and the Axis 3 script inventory, plus
// the 4-surface weight aggregate. It does NOT produce the report's wall-clock timings (`npm ci`, `nuxt
// generate`), the live-browser CSP matrix, or the full-site (918-route) weight table — those are session
// measurements taken outside this script and are labelled as such in the report's Method section. Do not claim
// "every number is reproducible with npm run measure".

import { readFileSync, existsSync, readdirSync, statSync, mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { SURFACES } from '../ir/adapter.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const appRoot = resolve(here, '..')
const outputRoot = resolve(process.argv[2] ?? join(appRoot, '..', '..', 'SpecScribeOutput'))
const pub = join(appRoot, '.output', 'public')

const ir = JSON.parse(readFileSync(join(appRoot, 'ir-data', 'ir.json'), 'utf8'))
const manifest = JSON.parse(readFileSync(join(outputRoot, 'spa', 'manifest.json'), 'utf8'))

const B = (n) => (n === null ? 'n/a' : n.toLocaleString())
const rows = []
const notes = []

function note(msg) {
  notes.push(msg)
  console.log(`  ! ${msg}`)
  process.exitCode = 1
}

function dirBytes(d) {
  // null (not 0) when absent, so "not measured" can never be published as a measured zero.
  if (!existsSync(d)) return null
  return readdirSync(d, { withFileTypes: true }).reduce(
    (n, e) => n + (e.isDirectory() ? (dirBytes(join(d, e.name)) ?? 0) : statSync(join(d, e.name)).size),
    0,
  )
}

// Strip every <script>…</script>. Approximates "JavaScript disabled" for the purpose of asking whether the
// document still carries its content and its links. NOTE: this is a markup-level proxy — it does not model
// inline event handlers, <noscript> swaps, or CSS that assumes hydration. The load-bearing NFR6 evidence is
// the live CSP-blocked browser run, not this number.
const stripScripts = (html) => html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '')

// The <main> region is the substantive content — the parity oracle. Chrome (nav, head, footer) differs between
// the static and SPA delivery forms for reasons that have nothing to do with Nuxt, so it is reported separately.
function mainRegion(html) {
  const m = html.match(/<main\b[^>]*>[\s\S]*<\/main>/i)
  return m ? m[0] : null
}

// The SAME normalization discipline the GoldenContentFingerprint gate uses (Task 4): neutralize the wall-clock
// footer, the ?v=<ModuleVersionId> asset cache-bust, the build-derived product version, CRLF and BOM. A diff
// that is only footer-clock/build-token noise is not a parity failure.
const normalizeVolatile = (s) =>
  s
    .replace(/^﻿/, '')
    .replace(/\r\n/g, '\n')
    .replace(/on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}/g, 'on <DATE>')
    .replace(/\?v=[0-9a-fA-F]+/g, '?v=<MVID>')
    .replace(/SpecScribe v[^<]+/g, 'SpecScribe v<VERSION>')

// The custom Markdig renderers ADR 0009 named as the ~889 LOC fidelity risk. Counted in <main> on BOTH the
// golden page and the Nuxt page — equal counts are the evidence that the renderers' output survives the trip.
const RENDERERS = {
  mermaid: /class="(?:language-)?mermaid"/g,
  refChips: /class="ref-chip/g,
  commentAnnot: /md-comment\b/g,
  swatches: /class="color-swatch"/g,
  gherkin: /gherkin-line/g,
  capability: /capability-card/g,
  tables: /class="md-table"/g,
  inlineSvg: /<svg\b/g,
}
const countRenderers = (s) =>
  Object.fromEntries(Object.entries(RENDERERS).map(([k, re]) => [k, (s.match(re) || []).length]))

console.log('='.repeat(110))
console.log('AXIS 1+2 — NFR6 baseline and parity, per surface')
console.log('='.repeat(110))
console.log(
  ['route', 'prerender', 'js-off', 'payload', 'IR bytes', 'verbatim?', 'main=?', 'svg', 'a[href]']
    .map((h, i) => h.padEnd([14, 11, 11, 11, 11, 11, 8, 6, 8][i]))
    .join(''),
)

for (const surface of SURFACES) {
  const route = surface.route
  const htmlPath = join(pub, route.slice(1), 'index.html')
  if (!existsSync(htmlPath)) {
    note(`${route}: prerendered file MISSING — run \`npm run generate\` first`)
    continue
  }
  const irPage = ir.pages[route]
  if (!irPage) {
    note(`${route}: absent from ir-data/ir.json — run \`npm run ir\``)
    continue
  }
  const html = readFileSync(htmlPath, 'utf8')
  const irHtml = irPage.contentHtml

  // Parity 1: does the IR's content string survive Vue's v-html byte-for-byte into the emitted HTML?
  const verbatimInNuxt = html.includes(irHtml)

  const goldenPath = join(outputRoot, surface.irPath)
  if (!existsSync(goldenPath)) note(`${route}: golden page missing at ${surface.irPath} — parity not measured`)
  const golden = existsSync(goldenPath) ? readFileSync(goldenPath, 'utf8') : null

  // Parity 2: compare the <main> REGIONS directly, under the golden gate's normalization.
  //
  // This replaces an earlier substring-containment check (`golden.includes(irHtml)`), whose comment claimed the
  // two regions were "transitively byte-identical". That inference was invalid: containment proves the IR
  // string is PRESENT in golden, but cannot detect content golden has that the IR does not — which is exactly
  // the additive-loss failure class the dashboard's 277 B delta belongs to.
  const goldenMain = golden ? mainRegion(golden) : null
  const nuxtMain = mainRegion(html)
  const irMain = mainRegion(irHtml)
  if (golden && !goldenMain) note(`${route}: no <main> found in the golden page`)
  if (!nuxtMain) note(`${route}: no <main> found in the prerendered page`)

  const nGolden = goldenMain === null ? null : normalizeVolatile(goldenMain)
  const nNuxt = nuxtMain === null ? null : normalizeVolatile(nuxtMain)
  const nIr = irMain === null ? null : normalizeVolatile(irMain)
  const mainIdenticalGoldenIr = nGolden !== null && nIr !== null ? nGolden === nIr : null
  const mainIdenticalNuxtIr = nNuxt !== null && nIr !== null ? nNuxt === nIr : null

  // Renderer-output survival, golden <main> vs Nuxt <main>.
  const goldenRenderers = goldenMain ? countRenderers(goldenMain) : null
  const nuxtRenderers = nuxtMain ? countRenderers(nuxtMain) : null

  const jsOff = stripScripts(html)
  const payloadPath = join(pub, route.slice(1), '_payload.json')
  // A missing _payload.json is NOT zero hydration cost — under SPIKE_NO_PAYLOAD_EXTRACTION the payload is
  // inlined into the HTML instead. Distinguish the two rather than scoring the inlined variant as free.
  const payload = existsSync(payloadPath)
    ? statSync(payloadPath).size
    : html.includes('__NUXT_DATA__')
      ? null // inlined — counted inside `prerender` above, not separable here
      : 0

  rows.push({
    route,
    prerender: Buffer.byteLength(html),
    jsOff: Buffer.byteLength(jsOff),
    payload,
    payloadInlined: payload === null,
    irBytes: Buffer.byteLength(irHtml),
    verbatimInNuxt,
    goldenMainBytes: goldenMain ? Buffer.byteLength(goldenMain) : null,
    irMainBytes: irMain ? Buffer.byteLength(irMain) : null,
    nuxtMainBytes: nuxtMain ? Buffer.byteLength(nuxtMain) : null,
    mainIdenticalGoldenIr,
    mainIdenticalNuxtIr,
    goldenRenderers,
    nuxtRenderers,
    golden: golden ? Buffer.byteLength(golden) : null,
    // Counted on the whole js-off DOCUMENT (chrome included), which is why these exceed the <main>-scoped
    // renderer counts below — 148 vs 115 inline SVGs on the dashboard is that difference, not a contradiction.
    svg: (jsOff.match(/<svg\b/g) || []).length,
    links: (jsOff.match(/<a\s[^>]*href=/g) || []).length,
    probe: surface.probe,
  })

  const r = rows.at(-1)
  console.log(
    [
      route.padEnd(14),
      B(r.prerender).padEnd(11),
      B(r.jsOff).padEnd(11),
      (r.payloadInlined ? 'inlined' : B(r.payload)).padEnd(11),
      B(r.irBytes).padEnd(11),
      String(r.verbatimInNuxt).padEnd(11),
      String(r.mainIdenticalNuxtIr).padEnd(8),
      String(r.svg).padEnd(6),
      String(r.links).padEnd(8),
    ].join(''),
  )
}

console.log('\n<main> byte comparison (normalized: footer clock, ?v= cache-bust, product version, CRLF, BOM)')
console.log(
  ['route', 'golden main', 'IR main', 'Nuxt main', 'golden=IR', 'Nuxt=IR']
    .map((h, i) => h.padEnd([14, 13, 13, 13, 11, 9][i]))
    .join(''),
)
for (const r of rows) {
  console.log(
    [
      r.route.padEnd(14),
      B(r.goldenMainBytes).padEnd(13),
      B(r.irMainBytes).padEnd(13),
      B(r.nuxtMainBytes).padEnd(13),
      String(r.mainIdenticalGoldenIr).padEnd(11),
      String(r.mainIdenticalNuxtIr).padEnd(9),
    ].join(''),
  )
}

console.log('\nCustom Markdig renderer survival, golden <main> vs Nuxt <main> (counts must match)')
const rk = Object.keys(RENDERERS)
console.log(['route', ...rk].map((h, i) => h.padEnd(i === 0 ? 14 : 14)).join(''))
for (const r of rows) {
  if (!r.goldenRenderers || !r.nuxtRenderers) continue
  console.log(
    [
      r.route.padEnd(14),
      ...rk.map((k) => `${r.goldenRenderers[k]}=${r.nuxtRenderers[k]}`.padEnd(14)),
    ].join(''),
  )
  for (const k of rk) {
    if (r.goldenRenderers[k] !== r.nuxtRenderers[k]) note(`${r.route}: renderer '${k}' count differs`)
  }
}

// The spike index page is the JS-off NAVIGATION probe: its links must be real <a href> in the emitted HTML,
// and each target must exist as its own prerendered file on disk (no client router required).
//
// LIMIT (code review 2026-07-23): this covers only the spike's OWN index links. The migrated surface's ~570
// in-content links are NOT resolved here and do NOT resolve against this route space — the IR's hrefs are
// `code/…`, `adrs/…` while the routes are `/dashboard`, `/adr-0006`, … That is the same mismatch that makes
// `crawlLinks: true` abort the build. So this measures "the prerendered baseline RENDERS without JS", not
// "the migrated surface is fully NAVIGABLE without JS". Route-mapping the IR's link graph is Story 23.3 scope.
const indexHtml = readFileSync(join(pub, 'index.html'), 'utf8')
const indexJsOff = stripScripts(indexHtml)
const navTargets = SURFACES.map((s) => ({
  route: s.route,
  anchorInHtml: indexJsOff.includes(`href="${s.route}"`),
  fileOnDisk: existsSync(join(pub, s.route.slice(1), 'index.html')),
}))
console.log('\nJS-off navigation (spike index → each surface; NOT the surfaces\' own in-content links):')
for (const t of navTargets) {
  console.log(`  ${t.route.padEnd(14)} <a href> present: ${t.anchorInHtml}   prerendered file on disk: ${t.fileOnDisk}`)
}

console.log('\n' + '='.repeat(110))
console.log('AXIS 3 — script inventory (webview CSP surface)')
console.log('='.repeat(110))
const dashPath = join(pub, 'dashboard', 'index.html')
const scriptInventory = []
if (!existsSync(dashPath)) {
  note('AXIS 3 skipped — dashboard/index.html not generated')
} else {
  const dash = readFileSync(dashPath, 'utf8')
  // Sanity: every <script> start must have a matching close, or the inventory silently omits one.
  const starts = (dash.match(/<script(?=[\s>])/g) || []).length
  const scriptRe = /<script([^>]*)>([\s\S]*?)<\/script>/g
  let m
  let i = 0
  while ((m = scriptRe.exec(dash))) {
    const attrs = m[1].trim()
    // Anchor these to an attribute boundary: a bare /\ssrc=/ misses a leading `src` after the trim, and a bare
    // /nonce=/ matches `data-nonce=` — either would corrupt the AC #3 verdict this inventory backs.
    const inline = !/(^|\s)src=/.test(attrs)
    const nonced = /(^|\s)nonce=/.test(attrs)
    const typeMatch = attrs.match(/(^|\s)type=(?:"([^"]*)"|'([^']*)'|([^\s>]+))/)
    const type = typeMatch ? (typeMatch[2] ?? typeMatch[3] ?? typeMatch[4]) : 'text/javascript'
    const origin = attrs.includes('id="sunburst-explorer-data"') ? 'IR content (SpecScribe island)' : 'Nuxt'
    scriptInventory.push({ inline, nonced, type, bytes: Buffer.byteLength(m[2]), origin })
    console.log(
      `  ${String(++i).padStart(2)}. ${inline ? 'inline  ' : 'external'} nonce=${String(nonced).padEnd(5)} ` +
        `type=${type.padEnd(18)} bytes=${String(Buffer.byteLength(m[2])).padStart(7)}  ${origin}`,
    )
  }
  if (starts !== i) note(`script inventory incomplete: ${starts} <script> starts but ${i} matched pairs`)
}

console.log('\n' + '='.repeat(110))
console.log('AXIS 4 — Node-in-pipeline weight (4 measured surfaces; full-site figures are session-measured)')
console.log('='.repeat(110))
const spaDir = join(outputRoot, 'spa')
const spaEntries = readdirSync(spaDir, { withFileTypes: true }).filter((e) => e.isFile())
const spaBytes = spaEntries.reduce((n, e) => n + statSync(join(spaDir, e.name)).size, 0)
const nuxtAssets = dirBytes(join(pub, '_nuxt'))
const nodeModules = dirBytes(join(appRoot, 'node_modules'))
const measured = rows.filter((r) => r.golden !== null)
const totalPrerender = measured.reduce((n, r) => n + r.prerender, 0)
const totalPayload = measured.reduce((n, r) => n + (r.payload ?? 0), 0)
const totalGolden = measured.reduce((n, r) => n + r.golden, 0)

if (measured.length !== SURFACES.length) {
  note(`PARTIAL — the totals below cover ${measured.length} of ${SURFACES.length} surfaces`)
}

console.log(`  proxy IR (spa/, ${spaEntries.length} files)   : ${B(spaBytes)} bytes`)
console.log(`  extracted IR bundle               : ${B(statSync(join(appRoot, 'ir-data', 'ir.json')).size)} bytes`)
console.log(`  Nuxt client assets (_nuxt/)       : ${B(nuxtAssets)} bytes`)
console.log(`  node_modules (build-time only)    : ${B(nodeModules)} bytes`)
console.log(`  manifest page count (route table) : ${B(Object.keys(manifest.pages).length)} pages`)
console.log(`  prerendered routes on disk        : ${B(readdirSync(pub, { withFileTypes: true }).filter((e) => e.isDirectory()).length)} route dirs`)
console.log('')
console.log(`  ${measured.length} surfaces, C# static HTML        : ${B(totalGolden)} bytes`)
console.log(`  ${measured.length} surfaces, Nuxt prerendered HTML : ${B(totalPrerender)} bytes`)
console.log(`  ${measured.length} surfaces, hydration payloads    : ${B(totalPayload)} bytes  (fetched ON TOP of the HTML)`)
if (totalGolden === 0) {
  note('cannot compute the wire-cost ratio — no golden pages were measured')
} else {
  console.log(`  wire cost to hydrate vs C# static : ${((totalPrerender + totalPayload) / totalGolden).toFixed(2)}x`)
}

mkdirSync(join(appRoot, 'measurements'), { recursive: true })
writeFileSync(
  join(appRoot, 'measurements', 'measurements.json'),
  JSON.stringify(
    {
      generatedFrom: outputRoot,
      scope: 'per-surface only; timings, live CSP matrix and full-site weights are session-measured (see report)',
      rows,
      navTargets,
      scriptInventory,
      spaBytes,
      nuxtAssets,
      nodeModules,
      totalPrerender,
      totalPayload,
      totalGolden,
      notes,
    },
    null,
    2,
  ),
)
console.log(`\nwrote measurements/measurements.json${notes.length ? ` (${notes.length} problem(s) — exit 1)` : ''}`)
